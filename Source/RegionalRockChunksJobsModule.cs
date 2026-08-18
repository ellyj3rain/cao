using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Noise;

namespace ColonistAwareness
{
    // Regional map-generation jobs, slice 1: GenStep_RockChunks noise.
    //
    // The native step evaluates one six-octave Perlin (wrapped in ScaleBias)
    // for every map cell before its sequential Rand and spawning behavior. On
    // a regional aggregate that is millions of pure, independent evaluations
    // feeding a strictly ordered consumer. This module intercepts exactly the
    // one ModuleBase.GetValue(IntVec3) call site inside Generate, builds the
    // complete float grid once in bounded worker threads over the immutable
    // noise graph, and lets the untouched native loop consume that grid in
    // native cell order. The Perlin seed still comes from the native
    // Rand.Range call, every subsequent Rand call stays native, and each grid
    // value is produced by the same jitted ModuleBase.GetValue(IntVec3) code
    // the native call site would have executed, so results are bit-identical
    // and the downstream formation, selection, spawn, and rubble behavior is
    // unchanged.
    //
    // Worker threads touch only the immutable noise module graph and their
    // disjoint rows of the output array: no Rand, no Map, no Thing, no
    // grids, no listers, no regions, no Unity, no defs, no callbacks. Any
    // worker failure discards the unpublished grid and the call site falls
    // back to direct native evaluation. On regional maps the grid is
    // recomputed sequentially by default and compared bit-for-bit so every
    // live run carries its own sequential-versus-parallel equivalence
    // receipt.
    //
    // Installed by CARegionalRockChunksJobsInstaller with its own Harmony
    // instance; nothing here carries [HarmonyPatch], so the mod's PatchAll
    // cannot double-apply it, and an install failure is contained to this
    // module while the native path keeps working.

    // Pure grid builder. Deliberately free of Verse.Log, Rand, Map, Unity,
    // and static state so it can also be executed headlessly (against the
    // real Assembly-CSharp noise implementation) by the offline determinism
    // harness.
    public static class CARegionalRockChunksNoiseGrid
    {
        public const int MaxWorkers = 32;

        public static int DefaultWorkerCount()
        {
            int processors;
            try { processors = Environment.ProcessorCount; }
            catch { processors = 1; }
            return Clamp(processors - 2, 1, 8);
        }

        // Evaluates module.GetValue(new IntVec3(x, 0, z)) for every cell of a
        // sizeX by sizeZ map into index z * sizeX + x (CellIndicesUtility
        // order). workerCount 1 runs strictly sequentially on the calling
        // thread with no thread creation. Returns null and reports the first
        // failure if any worker throws; the output array is never published
        // partially filled.
        public static float[] Build(ModuleBase module, int sizeX, int sizeZ,
            int workerCount, out Exception failure)
        {
            failure = null;
            if (module == null || sizeX <= 0 || sizeZ <= 0
                || (long)sizeX * sizeZ > int.MaxValue)
            {
                failure = new ArgumentException(
                    "invalid noise grid request " + sizeX + "x" + sizeZ);
                return null;
            }
            try
            {
                float[] grid = new float[sizeX * sizeZ];
                int workers = Clamp(workerCount, 1,
                    Math.Min(MaxWorkers, sizeZ));
                if (workers == 1)
                {
                    FillRows(module, grid, sizeX, 0, sizeZ);
                    return grid;
                }

                int[] rowStart = new int[workers];
                int[] rowEnd = new int[workers];
                int baseRows = sizeZ / workers;
                int remainder = sizeZ % workers;
                int next = 0;
                for (int i = 0; i < workers; i++)
                {
                    rowStart[i] = next;
                    next += baseRows + (i < remainder ? 1 : 0);
                    rowEnd[i] = next;
                }

                Exception[] errors = new Exception[workers];
                Thread[] threads = new Thread[workers - 1];
                int started = 0;
                Exception setupFailure = null;
                try
                {
                    for (int i = 1; i < workers; i++)
                    {
                        int slot = i;
                        Thread thread = new Thread(() =>
                        {
                            try
                            {
                                FillRows(module, grid, sizeX,
                                    rowStart[slot], rowEnd[slot]);
                            }
                            catch (Exception error)
                            {
                                errors[slot] = error;
                            }
                        });
                        thread.IsBackground = true;
                        thread.Name = "CA-RegionalRockChunksNoise-" + slot;
                        threads[i - 1] = thread;
                        thread.Start();
                        started++;
                    }
                    try
                    {
                        FillRows(module, grid, sizeX, rowStart[0],
                            rowEnd[0]);
                    }
                    catch (Exception error)
                    {
                        errors[0] = error;
                    }
                }
                catch (Exception error)
                {
                    setupFailure = error;
                }
                finally
                {
                    // Join every worker whose Start completed, even when a
                    // later allocation/name/start operation failed. This is
                    // both the publication barrier and the containment gate:
                    // no worker can retain the unpublished grid after return.
                    for (int i = 0; i < started; i++)
                    {
                        Exception joinFailure =
                            JoinStartedWorker(threads[i]);
                        if (setupFailure == null && joinFailure != null)
                            setupFailure = joinFailure;
                    }
                }
                if (setupFailure != null)
                {
                    failure = setupFailure;
                    return null;
                }
                for (int i = 0; i < workers; i++)
                {
                    if (errors[i] == null) continue;
                    failure = errors[i];
                    return null;
                }
                return grid;
            }
            catch (Exception error)
            {
                // Includes grid/partition/error-array allocation failures.
                failure = error;
                return null;
            }
        }

        // A failed Join attempt is not proof that a started worker stopped.
        // Interrupted joins are retried. For any other join failure, retain
        // the first error but keep polling until IsAlive proves the worker no
        // longer owns the unpublished module/grid references. If even the
        // liveness query fails, return to Join and try the publication barrier
        // again; this method never reports completion without proof.
        private static Exception JoinStartedWorker(Thread thread)
        {
            Exception firstFailure = null;
            while (true)
            {
                try
                {
                    thread.Join();
                    return firstFailure;
                }
                catch (ThreadInterruptedException error)
                {
                    if (firstFailure == null) firstFailure = error;
                }
                catch (Exception error)
                {
                    if (firstFailure == null) firstFailure = error;
                    bool retryJoin = false;
                    while (!retryJoin)
                    {
                        try
                        {
                            if (!thread.IsAlive) return firstFailure;
                            Thread.Sleep(1);
                        }
                        catch (ThreadInterruptedException interrupted)
                        {
                            if (firstFailure == null)
                                firstFailure = interrupted;
                        }
                        catch (Exception stateError)
                        {
                            if (firstFailure == null)
                                firstFailure = stateError;
                            retryJoin = true;
                        }
                    }
                }
            }
        }

        private static void FillRows(ModuleBase module, float[] grid,
            int sizeX, int fromRow, int toRow)
        {
            for (int z = fromRow; z < toRow; z++)
            {
                int rowBase = z * sizeX;
                for (int x = 0; x < sizeX; x++)
                    grid[rowBase + x] = module.GetValue(new IntVec3(x, 0, z));
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatWord
        {
            [FieldOffset(0)] internal float value;
            [FieldOffset(0)] internal uint bits;
        }

        // FNV-1a-64 folded over each cell's IEEE-754 bit pattern, word-wise.
        // Bit patterns, not float comparisons, so the receipt is exact.
        public static ulong Checksum(float[] grid)
        {
            ulong hash = 14695981039346656037UL;
            FloatWord word = default(FloatWord);
            for (int i = 0; i < grid.Length; i++)
            {
                word.value = grid[i];
                hash = (hash ^ word.bits) * 1099511628211UL;
            }
            return hash;
        }

        // Index of the first bitwise difference, -1 when identical.
        public static int FirstMismatch(float[] left, float[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return -2;
            FloatWord a = default(FloatWord);
            FloatWord b = default(FloatWord);
            for (int i = 0; i < left.Length; i++)
            {
                a.value = left[i];
                b.value = right[i];
                if (a.bits != b.bits) return i;
            }
            return -1;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    internal sealed class CARegionalRockChunksJobsContext
    {
        internal Map map;
        internal bool active;
        internal string stepLabel;
        internal int sizeX;
        internal int sizeZ;
        internal int workerCount;
        internal bool forcedSequential;
        internal Stopwatch totalWatch;

        internal ModuleBase module;
        internal float[] grid;
        internal long buildMilliseconds = -1;
        internal long auditMilliseconds = -1;
        internal long sequentialBuildMilliseconds = -1;
        internal long noiseCallsServed;
        internal ulong parallelChecksum;
        internal ulong sequentialChecksum;
        internal bool verified;
        internal bool verifySkipped;
        internal string fallbackReason;

        internal int formationEntryThings;
        internal int formationEntryRubble;
        internal int formationCount;
        internal int chunkThings;
        internal int rubbleThings;
        internal int maxFormationLength;
        internal long reGrowthMilliseconds;
        internal int reGrowthCalls;
        internal int reGrowthNetThings;
        internal int reGrowthNetRubble;
        internal int reGrowthExceptions;
        internal readonly int[] lengthHistogram =
            new int[CARegionalRockChunksJobsPatches.LengthBucketLabels.Length];
    }

    internal sealed class CARegionalReGrowthTimingState
    {
        internal Map map;
        internal Stopwatch watch;
        internal int entryThings;
        internal int entryRubble;
        internal bool recorded;
    }

    internal static class CARegionalRockChunksJobsPatches
    {
        internal static readonly string[] LengthBucketLabels =
        {
            "0", "1", "2", "3", "4", "5-8", "9-16", "17-32", "33-64", "65+"
        };

        // Environment toggles, read once. CA_REGIONAL_JOBS=0 removes the
        // patches entirely at install time; the rest tune live behavior.
        internal static readonly bool Installed;
        internal static bool TranspilerApplied;
        internal static bool ReGrowthTimingInstalled;
        private static readonly int ConfiguredWorkers;
        private static readonly bool ForceSequential;
        private static readonly bool VerifyAgainstSequential;

        [ThreadStatic]
        private static Stack<CARegionalRockChunksJobsContext> activeContexts;

        static CARegionalRockChunksJobsPatches()
        {
            Installed = !string.Equals(
                Environment.GetEnvironmentVariable("CA_REGIONAL_JOBS"), "0",
                StringComparison.Ordinal);
            ForceSequential = string.Equals(
                Environment.GetEnvironmentVariable(
                    "CA_REGIONAL_JOBS_SEQUENTIAL"), "1",
                StringComparison.Ordinal);
            VerifyAgainstSequential = !string.Equals(
                Environment.GetEnvironmentVariable(
                    "CA_REGIONAL_JOBS_VERIFY"), "0",
                StringComparison.Ordinal);
            string workers = Environment.GetEnvironmentVariable(
                "CA_REGIONAL_JOBS_WORKERS");
            int parsed;
            ConfiguredWorkers = workers != null
                && int.TryParse(workers, out parsed)
                && parsed >= 1
                && parsed <= CARegionalRockChunksNoiseGrid.MaxWorkers
                    ? parsed
                    : 0;
        }

        internal static string PolicySummary()
        {
            return "workers="
                + (ForceSequential ? "1 (forced sequential)"
                    : ConfiguredWorkers > 0
                        ? ConfiguredWorkers + " (env override)"
                        : CARegionalRockChunksNoiseGrid.DefaultWorkerCount()
                            + " (processors "
                            + Environment.ProcessorCount + " - 2, clamped 1-8)")
                + "; sequential-verify "
                + (VerifyAgainstSequential ? "on" : "off")
                + "; env CA_REGIONAL_JOBS=0 disables at load, "
                + "CA_REGIONAL_JOBS_SEQUENTIAL=1 forces one worker, "
                + "CA_REGIONAL_JOBS_WORKERS=n overrides, "
                + "CA_REGIONAL_JOBS_VERIFY=0 skips the live equivalence pass";
        }

        private static CARegionalRockChunksJobsContext Current
        {
            get
            {
                Stack<CARegionalRockChunksJobsContext> stack = activeContexts;
                return stack != null && stack.Count > 0 ? stack.Peek() : null;
            }
        }

        // ---- GenStep_RockChunks.Generate ----

        internal static void GeneratePrefix(GenStep __instance, Map map)
        {
            if (activeContexts == null)
                activeContexts =
                    new Stack<CARegionalRockChunksJobsContext>();
            CARegionalRockChunksJobsContext context =
                new CARegionalRockChunksJobsContext();
            context.map = map;
            // Pushed before anything that can throw so the finalizer's pop
            // always balances, including under nested preview generation.
            activeContexts.Push(context);
            try
            {
                context.active = map != null
                    && CARegionalRiverPatchUtility.Active(map);
                if (!context.active) return;

                context.stepLabel =
                    (__instance?.GetType().FullName ?? "unknown")
                    + " [" + (__instance?.def?.defName ?? "no-def") + "]";
                context.sizeX = map.Size.x;
                context.sizeZ = map.Size.z;
                context.forcedSequential = ForceSequential;
                context.workerCount = ForceSequential ? 1
                    : ConfiguredWorkers > 0 ? ConfiguredWorkers
                    : CARegionalRockChunksNoiseGrid.DefaultWorkerCount();
                context.totalWatch = Stopwatch.StartNew();
                Log.Message("[CA][Regional][Jobs][RockChunks] starting "
                    + context.stepLabel + " for " + context.sizeX + "x"
                    + context.sizeZ + " map " + map.uniqueID + "; "
                    + PolicySummary());
            }
            catch (Exception error)
            {
                context.active = false;
                Log.Warning("[CA][Regional][Jobs][RockChunks] receipt "
                    + "prefix failed; native behavior retained: " + error);
            }
        }

        internal static void GeneratePostfix()
        {
            try
            {
                GenerateReceipts();
            }
            catch (Exception error)
            {
                Log.Warning("[CA][Regional][Jobs][RockChunks] completion "
                    + "receipt failed; generation unaffected: " + error);
            }
        }

        private static void GenerateReceipts()
        {
            CARegionalRockChunksJobsContext context = Current;
            if (context == null || !context.active) return;
            context.totalWatch.Stop();
            long total = context.totalWatch.ElapsedMilliseconds;
            long build = Math.Max(0, context.buildMilliseconds);
            long audit = Math.Max(0, context.auditMilliseconds);
            long consume = Math.Max(0, total - build - audit);
            Map map = context.map;

            Log.Message("[CA][Regional][Jobs][RockChunks] completed "
                + context.stepLabel + " in " + total + " ms for "
                + context.sizeX + "x" + context.sizeZ + " map " + map.uniqueID
                + ": parallel noise build " + (context.buildMilliseconds < 0
                    ? "not reached" : build + " ms")
                + " across " + context.workerCount + " workers"
                + (context.forcedSequential ? " (forced sequential)" : "")
                + ", grid audit " + (context.auditMilliseconds < 0
                    ? "not reached" : audit + " ms"
                        + (context.verifySkipped
                            ? " (checksum only)"
                            : " (sequential rebuild "
                                + context.sequentialBuildMilliseconds
                                + " ms plus checksums/comparison)"))
                + ", ordered native remainder " + consume + " ms, "
                + context.noiseCallsServed + " of "
                + ((long)context.sizeX * context.sizeZ)
                + " cell values served from the grid"
                + (context.fallbackReason == null ? ""
                    : "; FALLBACK " + context.fallbackReason));

            if (context.grid != null || context.fallbackReason != null)
                Log.Message("[CA][Regional][Jobs][RockChunks] checksums for "
                    + "map " + map.uniqueID + ": parallel="
                    + context.parallelChecksum.ToString("X16")
                    + (context.verifySkipped
                        ? "; sequential=skipped"
                        : "; sequential="
                            + context.sequentialChecksum.ToString("X16")
                            + "; bitIdentical=" + context.verified));

            Log.Message("[CA][Regional][Jobs][RockChunks] formations="
                + context.formationCount
                + " chunkThings=" + context.chunkThings
                + " rubbleFilthThings=" + context.rubbleThings
                + " maxFormationLength=" + context.maxFormationLength
                + " lengths [" + HistogramText(context.lengthHistogram)
                + "] for map " + map.uniqueID
                + " (chunkThings counts non-filth things spawned during "
                + "formation growth; rubble thickened onto existing filth "
                + "does not add a thing)");

            Log.Message("[CA][Regional][Jobs][RockChunks][ReGrowth] "
                + (ReGrowthTimingInstalled ? "timed" : "not installed")
                + " calls=" + context.reGrowthCalls
                + " elapsed=" + context.reGrowthMilliseconds + " ms"
                + " netThings=" + context.reGrowthNetThings
                + " netRubble=" + context.reGrowthNetRubble
                + " exceptions=" + context.reGrowthExceptions
                + " for map " + map.uniqueID);
        }

        internal static Exception GenerateFinalizer(Exception __exception)
        {
            Stack<CARegionalRockChunksJobsContext> stack = activeContexts;
            if (stack != null && stack.Count > 0)
            {
                CARegionalRockChunksJobsContext context = stack.Pop();
                if (context.active && __exception != null)
                    Log.Message("[CA][Regional][Jobs][RockChunks] aborted by "
                        + __exception.GetType().Name
                        + "; noise cache released, no partial state retained");
                context.grid = null;
                context.module = null;
                if (stack.Count == 0) activeContexts = null;
            }
            return __exception;
        }

        // Replaces the exact `callvirt ModuleBase.GetValue(IntVec3)` at
        // IL_00d8 of the native Generate. Two passes: the stream is only
        // mutated when exactly one site matches, otherwise the native IL is
        // returned untouched and the module keeps native behavior.
        internal static IEnumerable<CodeInstruction> GenerateTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            TranspilerApplied = false;
            MethodInfo nativeGetValue = AccessTools.Method(
                typeof(ModuleBase), nameof(ModuleBase.GetValue),
                new[] { typeof(IntVec3) });
            MethodInfo routed = AccessTools.Method(
                typeof(CARegionalRockChunksJobsPatches),
                nameof(RoutedNoiseValue));
            List<CodeInstruction> buffer =
                new List<CodeInstruction>(instructions);
            int matches = 0;
            for (int i = 0; i < buffer.Count; i++)
                if (buffer[i].Calls(nativeGetValue)) matches++;
            if (matches != 1)
            {
                Log.Error("[CA][Regional][Jobs][RockChunks] expected exactly "
                    + "one ModuleBase.GetValue(IntVec3) site in "
                    + "GenStep_RockChunks.Generate, found " + matches
                    + "; leaving native IL untouched");
                return buffer;
            }
            for (int i = 0; i < buffer.Count; i++)
            {
                if (!buffer[i].Calls(nativeGetValue)) continue;
                // Same stack shape: [module, cell] -> float32. Labels and
                // exception blocks on the instruction are preserved.
                buffer[i].opcode = OpCodes.Call;
                buffer[i].operand = routed;
                TranspilerApplied = true;
                break;
            }
            return buffer;
        }

        // The routed call site. Ordinary maps take the immediate native
        // delegation; regional maps consume the lazily built grid.
        internal static float RoutedNoiseValue(ModuleBase module, IntVec3 cell)
        {
            CARegionalRockChunksJobsContext context = Current;
            if (context == null || !context.active)
                return module.GetValue(cell);
            float[] grid = context.grid;
            if (grid == null)
            {
                if (context.fallbackReason != null)
                    return module.GetValue(cell);
                grid = BuildGrid(context, module);
                if (grid == null)
                    return module.GetValue(cell);
            }
            if (!ReferenceEquals(context.module, module)
                || cell.y != 0
                || (uint)cell.x >= (uint)context.sizeX
                || (uint)cell.z >= (uint)context.sizeZ)
                return module.GetValue(cell);
            context.noiseCallsServed++;
            return grid[cell.z * context.sizeX + cell.x];
        }

        private static float[] BuildGrid(
            CARegionalRockChunksJobsContext context, ModuleBase module)
        {
            Stopwatch buildWatch = Stopwatch.StartNew();
            Exception failure;
            float[] grid = CARegionalRockChunksNoiseGrid.Build(module,
                context.sizeX, context.sizeZ, context.workerCount,
                out failure);
            buildWatch.Stop();
            context.buildMilliseconds = buildWatch.ElapsedMilliseconds;
            if (grid == null)
            {
                context.fallbackReason = "noise grid build failed ("
                    + (failure?.GetType().Name ?? "unknown") + ": "
                    + (failure?.Message ?? "no detail")
                    + "); native per-cell evaluation retained";
                Log.Warning("[CA][Regional][Jobs][RockChunks] "
                    + context.fallbackReason);
                return null;
            }
            Stopwatch auditWatch = Stopwatch.StartNew();
            try
            {
                context.parallelChecksum =
                    CARegionalRockChunksNoiseGrid.Checksum(grid);

                if (VerifyAgainstSequential && context.workerCount > 1)
                {
                    Stopwatch sequentialWatch = Stopwatch.StartNew();
                    Exception verifyFailure;
                    float[] sequential = CARegionalRockChunksNoiseGrid.Build(
                        module, context.sizeX, context.sizeZ, 1,
                        out verifyFailure);
                    sequentialWatch.Stop();
                    context.sequentialBuildMilliseconds =
                        sequentialWatch.ElapsedMilliseconds;
                    if (sequential == null)
                    {
                        context.verifySkipped = true;
                        context.fallbackReason = "live sequential verification "
                            + "failed ("
                            + (verifyFailure?.GetType().Name ?? "unknown")
                            + ": "
                            + (verifyFailure?.Message ?? "no detail")
                            + "); parallel grid discarded and native "
                            + "per-cell evaluation retained";
                        Log.Warning("[CA][Regional][Jobs][RockChunks] "
                            + context.fallbackReason);
                        return null;
                    }
                    else
                    {
                        context.sequentialChecksum =
                            CARegionalRockChunksNoiseGrid.Checksum(sequential);
                        int mismatch = CARegionalRockChunksNoiseGrid
                            .FirstMismatch(grid, sequential);
                        context.verified = mismatch == -1;
                        if (!context.verified)
                        {
                            // Serve the sequential grid: it is the native
                            // result by construction. The receipt keeps both
                            // checksums.
                            Log.Error("[CA][Regional][Jobs][RockChunks] "
                                + "parallel noise grid diverged from "
                                + "sequential at index " + mismatch
                                + "; serving the sequential grid");
                            grid = sequential;
                        }
                    }
                }
                else
                {
                    context.verifySkipped = true;
                }
            }
            catch (Exception auditError)
            {
                context.verifySkipped = true;
                context.fallbackReason = "grid audit failed ("
                    + auditError.GetType().Name + ": "
                    + auditError.Message + "); parallel grid discarded "
                    + "and native per-cell evaluation retained";
                Log.Warning("[CA][Regional][Jobs][RockChunks] "
                    + context.fallbackReason);
                return null;
            }
            finally
            {
                auditWatch.Stop();
                context.auditMilliseconds = auditWatch.ElapsedMilliseconds;
            }
            context.module = module;
            context.grid = grid;
            return grid;
        }

        // ---- GenStep_RockChunks.GrowLowRockFormationFrom ----
        // Per-formation receipts from O(1) map-lister count deltas. The
        // map-wide lister updates natively during initial generation (the
        // CA registration optimization bypasses only per-region listers).

        internal static void GrowPrefix()
        {
            try
            {
                CARegionalRockChunksJobsContext context = Current;
                if (context == null || !context.active) return;
                Map map = context.map;
                context.formationEntryThings =
                    map.listerThings.AllThings.Count;
                context.formationEntryRubble = RubbleCount(map);
            }
            catch (Exception)
            {
                // Formation receipts must never break generation; the
                // per-formation delta simply reads as zero.
            }
        }

        internal static void GrowPostfix()
        {
            try
            {
                CARegionalRockChunksJobsContext context = Current;
                if (context == null || !context.active) return;
                Map map = context.map;
                int thingsDelta = map.listerThings.AllThings.Count
                    - context.formationEntryThings;
                int rubbleDelta = RubbleCount(map)
                    - context.formationEntryRubble;
                int length = Math.Max(0, thingsDelta - rubbleDelta);
                context.formationCount++;
                context.chunkThings += length;
                context.rubbleThings += Math.Max(0, rubbleDelta);
                if (length > context.maxFormationLength)
                    context.maxFormationLength = length;
                context.lengthHistogram[LengthBucket(length)]++;
            }
            catch (Exception)
            {
                // Same posture as GrowPrefix: receipts stay silent rather
                // than disturbing the native formation walk.
            }
        }

        // Optional compatibility instrumentation. ReGrowth patches the
        // native formation method with its own postfix, which may add large
        // and medium boulders, extra mineable chunks, rubble, despawns, and
        // ordered Rand work. Wrapping that exact postfix separates its cost
        // from native formation growth without taking a compile-time
        // dependency on ReGrowthCore.
        internal static void ReGrowthPostfixPrefix(Map map,
            out CARegionalReGrowthTimingState __state)
        {
            __state = null;
            try
            {
                CARegionalRockChunksJobsContext context = Current;
                if (context == null || !context.active
                    || map == null || !ReferenceEquals(context.map, map))
                    return;
                __state = new CARegionalReGrowthTimingState
                {
                    map = map,
                    entryThings = map.listerThings.AllThings.Count,
                    entryRubble = RubbleCount(map),
                    watch = Stopwatch.StartNew()
                };
            }
            catch (Exception)
            {
                // Diagnostics are optional and cannot impede ReGrowth.
                __state = null;
            }
        }

        internal static void ReGrowthPostfixPostfix(
            CARegionalReGrowthTimingState __state)
        {
            RecordReGrowthTiming(__state, false);
        }

        internal static Exception ReGrowthPostfixFinalizer(
            Exception __exception, CARegionalReGrowthTimingState __state)
        {
            RecordReGrowthTiming(__state, __exception != null);
            return __exception;
        }

        private static void RecordReGrowthTiming(
            CARegionalReGrowthTimingState state, bool failed)
        {
            if (state == null || state.recorded) return;
            state.recorded = true;
            try
            {
                state.watch.Stop();
                CARegionalRockChunksJobsContext context = Current;
                if (context == null || !context.active
                    || !ReferenceEquals(context.map, state.map))
                    return;
                context.reGrowthCalls++;
                context.reGrowthMilliseconds +=
                    state.watch.ElapsedMilliseconds;
                context.reGrowthNetThings +=
                    state.map.listerThings.AllThings.Count - state.entryThings;
                context.reGrowthNetRubble += RubbleCount(state.map)
                    - state.entryRubble;
                if (failed) context.reGrowthExceptions++;
            }
            catch (Exception)
            {
                // Timing/counting is evidence only. The wrapped exception,
                // if any, is returned unchanged by the finalizer.
            }
        }

        private static int RubbleCount(Map map)
        {
            return map.listerThings
                .ThingsOfDef(ThingDefOf.Filth_RubbleRock).Count;
        }

        private static int LengthBucket(int length)
        {
            if (length <= 4) return length;
            if (length <= 8) return 5;
            if (length <= 16) return 6;
            if (length <= 32) return 7;
            if (length <= 64) return 8;
            return 9;
        }

        private static string HistogramText(int[] histogram)
        {
            var text = new System.Text.StringBuilder(96);
            for (int i = 0; i < histogram.Length; i++)
            {
                if (i > 0) text.Append(' ');
                text.Append(LengthBucketLabels[i]).Append(':')
                    .Append(histogram[i]);
            }
            return text.ToString();
        }
    }

    // Self-contained installer. A distinct Harmony ID keeps these patches
    // separable from the main PatchAll set, and any failure here is caught so
    // the shipped mod keeps its native RockChunks behavior instead of losing
    // unrelated patches.
    [StaticConstructorOnStartup]
    internal static class CARegionalRockChunksJobsInstaller
    {
        private const string HarmonyId =
            "ellyj3rain.colonistawareness.regionaljobs";

        static CARegionalRockChunksJobsInstaller()
        {
            Harmony harmony = null;
            try
            {
                if (!CARegionalRockChunksJobsPatches.Installed)
                {
                    Log.Message("[CA][Regional][Jobs] disabled by "
                        + "CA_REGIONAL_JOBS=0; native RockChunks retained");
                    return;
                }
                harmony = new Harmony(HarmonyId);
                MethodInfo generate = AccessTools.Method(
                    typeof(GenStep_RockChunks),
                    nameof(GenStep_RockChunks.Generate));
                MethodInfo grow = AccessTools.Method(
                    typeof(GenStep_RockChunks), "GrowLowRockFormationFrom");
                if (generate == null || grow == null)
                    throw new MissingMethodException(
                        "GenStep_RockChunks.Generate or "
                        + "GrowLowRockFormationFrom not found");
                harmony.Patch(generate,
                    prefix: Method(nameof(
                        CARegionalRockChunksJobsPatches.GeneratePrefix),
                        Priority.First),
                    postfix: Method(nameof(
                        CARegionalRockChunksJobsPatches.GeneratePostfix),
                        Priority.Last),
                    transpiler: Method(nameof(
                        CARegionalRockChunksJobsPatches.GenerateTranspiler)),
                    finalizer: Method(nameof(
                        CARegionalRockChunksJobsPatches.GenerateFinalizer)));
                if (!CARegionalRockChunksJobsPatches.TranspilerApplied)
                    throw new InvalidOperationException(
                        "RockChunks noise call-site replacement was not "
                        + "applied");
                harmony.Patch(grow,
                    prefix: Method(nameof(
                        CARegionalRockChunksJobsPatches.GrowPrefix),
                        Priority.First),
                    postfix: Method(nameof(
                        CARegionalRockChunksJobsPatches.GrowPostfix),
                        Priority.Last));
                string reGrowth = TryInstallReGrowthTiming(harmony);
                Log.Message("[CA][Regional][Jobs] regional RockChunks noise "
                    + "jobs installed: parallel grid build on regional maps "
                    + "only, native Rand/traversal/spawn order retained; "
                    + CARegionalRockChunksJobsPatches.PolicySummary()
                    + "; " + reGrowth);
            }
            catch (Exception error)
            {
                bool rolledBack = false;
                if (harmony != null)
                {
                    try
                    {
                        harmony.UnpatchAll(HarmonyId);
                        rolledBack = true;
                    }
                    catch (Exception rollbackError)
                    {
                        Log.Error("[CA][Regional][Jobs] rollback failed: "
                            + rollbackError);
                    }
                }
                Log.Error("[CA][Regional][Jobs] install failed; "
                    + (rolledBack
                        ? "CA regional-jobs patches rolled back"
                        : "partial patch state could not be excluded")
                    + ": " + error);
            }
        }

        private static string TryInstallReGrowthTiming(Harmony harmony)
        {
            Type patchType = AccessTools.TypeByName(
                "ReGrowthCore."
                + "GenStep_RockChunks_GrowLowRockFormationFrom_Patch");
            if (patchType == null)
                return "ReGrowth RockChunks postfix not present";
            MethodInfo target = AccessTools.Method(patchType, "Postfix",
                new[] { typeof(IntVec3), typeof(Map) });
            if (target == null)
                return "ReGrowth RockChunks postfix type present but exact "
                    + "Postfix(IntVec3, Map) missing";
            try
            {
                harmony.Patch(target,
                    prefix: Method(nameof(CARegionalRockChunksJobsPatches
                        .ReGrowthPostfixPrefix), Priority.First),
                    postfix: Method(nameof(CARegionalRockChunksJobsPatches
                        .ReGrowthPostfixPostfix), Priority.Last),
                    finalizer: Method(nameof(CARegionalRockChunksJobsPatches
                        .ReGrowthPostfixFinalizer)));
                CARegionalRockChunksJobsPatches.ReGrowthTimingInstalled = true;
                return "ReGrowth RockChunks postfix timing installed";
            }
            catch (Exception error)
            {
                try
                {
                    harmony.Unpatch(target, HarmonyPatchType.All, HarmonyId);
                }
                catch (Exception rollbackError)
                {
                    Log.Warning("[CA][Regional][Jobs][ReGrowth] optional "
                        + "timing rollback failed: " + rollbackError);
                }
                Log.Warning("[CA][Regional][Jobs][ReGrowth] optional timing "
                    + "not installed: " + error);
                return "ReGrowth RockChunks postfix timing unavailable";
            }
        }

        private static HarmonyMethod Method(string name, int priority = -1)
        {
            HarmonyMethod method = new HarmonyMethod(AccessTools.Method(
                typeof(CARegionalRockChunksJobsPatches), name));
            if (priority >= 0) method.priority = priority;
            return method;
        }
    }
}
