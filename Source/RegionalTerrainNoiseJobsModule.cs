using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Noise;

namespace ColonistAwareness
{
    // Regional map-generation jobs, slice 3: the general terrain-noise grid
    // service.
    //
    // Slice 1 proved the pattern on one noise consumer (RockChunks). Slice 2
    // and its a2-170 integration removed the despawn-side linear scans and
    // instrumented the ordered commit. The next still-unclaimed
    // snapshot-shaped costs are the remaining per-cell noise sweeps of the
    // terrain phase, all of which flow through the same
    // ModuleBase.GetValue(IntVec3) shape over immutable, pre-seeded module
    // graphs:
    //
    //   - GenStep_ElevationFertility.Generate evaluates a deep elevation
    //     graph (Perlin + two displacement stages, Scale/Rotate chains,
    //     QualityMode.High - several nested Perlins per cell) and a
    //     six-octave fertility Perlin for every map cell: exactly two
    //     GetValue(IntVec3) sites in the installed IL.
    //   - TerrainPatchMaker.TerrainAt evaluates each patch maker's Perlin
    //     per candidate cell during terrain selection; on mixed-biome
    //     regional maps every biome contributes its own makers. One site in
    //     TerrainAt plus one inside its minSize flood-fill lambda.
    //
    // Every Rand draw for these graphs happens during module construction,
    // before the per-cell loops (TerrainPatchMaker seeds lazily inside its
    // first TerrainAt call). The service keeps an initial request window
    // native, both ensuring that initialization and its Rand consumption have
    // happened in native order and avoiding a whole-map grid for sparse patch
    // makers; recurrent modules materialize after that window. The service
    // snapshots nothing but the module reference, fills the whole grid in
    // bounded workers via the slice-1 pure builder (same jitted
    // GetValue(IntVec3) per cell, so values are bit-identical), optionally
    // rebuilds sequentially and compares bit-for-bit as a live equivalence
    // receipt, and serves the untouched native loops in native order.
    // Traversal, Rand sequence, MapGenFloatGrid writes, terrain selection,
    // and every downstream consumer (including CA's own regional elevation
    // rescale) remain fully native.
    //
    // CA's regional rock-selection prefix also routes its RockNoises Perlins
    // through this service, removing the remaining repeated full-map noise
    // sweeps while leaving its native ordered winner selection intact.
    //
    // Same installation posture as slices 1-2: own Harmony ID, no
    // [HarmonyPatch] attributes, two-pass count-checked transpilers that
    // leave native IL untouched on any surprise, containment by degradation
    // to native evaluation, and receipts for every decision.

    public static class CARegionalNoiseGridService
    {
        private sealed class ModuleReferenceComparer :
            IEqualityComparer<ModuleBase>
        {
            internal static readonly ModuleReferenceComparer Instance =
                new ModuleReferenceComparer();

            public bool Equals(ModuleBase left, ModuleBase right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(ModuleBase value)
            {
                return RuntimeHelpers.GetHashCode(value);
            }
        }

        // Caps keep additional memory proportional to map area and bounded:
        // grids are one float per cell (6.6 MB at 1411x1175); a mixed-biome
        // regional map can reach elevation + fertility + one Perlin per
        // patch maker per biome.
        public const int MaxGridsPerScope = 16;
        public const long MaxTotalGridBytes = 160L * 1024L * 1024L;
        // A TerrainPatchMaker can be queried only a handful of times on a
        // mixed-biome map. Keep those calls native; materialize the whole-map
        // grid only after the module proves it can amortize at least the ideal
        // area/worker parallel build cost. This floor avoids tiny thresholds
        // on smaller regional maps.
        public const int MinimumRequestsBeforeGrid = 4096;

        internal sealed class GridEntry
        {
            internal float[] grid;
            internal bool degraded;
            internal string label;
            internal long buildMilliseconds;
            internal long auditMilliseconds;
            internal ulong parallelChecksum;
            internal ulong sequentialChecksum;
            internal bool audited;
            internal bool bitIdentical;
            internal long requests;
            internal long serves;
            internal long nativeWarmupCalls;
            internal long nativeFallbackCalls;
        }

        internal sealed class Scope
        {
            internal Map map;
            internal bool active;
            internal int sizeX;
            internal int sizeZ;
            internal int workerCount;
            internal long materializationThreshold;
            internal bool verify;
            internal readonly Dictionary<ModuleBase, GridEntry> entries =
                new Dictionary<ModuleBase, GridEntry>();
            internal long totalGridBytes;
            internal int builtGridCount;
            internal int refusedGrids;
            internal int unsafeGraphs;
            internal long nativeFallbackCalls;
            internal bool routerDegraded;
        }

        internal static readonly bool Installed;
        private static readonly int ConfiguredWorkers;
        private static readonly bool ForceSequential;
        private static readonly bool VerifyAgainstSequential;

        [ThreadStatic]
        private static Stack<Scope> scopes;

        static CARegionalNoiseGridService()
        {
            Installed = !string.Equals(
                Environment.GetEnvironmentVariable("CA_REGIONAL_NOISEGRIDS"),
                "0", StringComparison.Ordinal);
            ForceSequential = string.Equals(
                Environment.GetEnvironmentVariable(
                    "CA_REGIONAL_NOISEGRIDS_SEQUENTIAL"), "1",
                StringComparison.Ordinal);
            // The offline harness already exercises every routed engine
            // graph bit-for-bit at regional dimensions. A full sequential
            // rebuild here would pay the original cost again and erase the
            // runtime speedup, so live full-grid verification is explicit.
            VerifyAgainstSequential = string.Equals(
                Environment.GetEnvironmentVariable(
                    "CA_REGIONAL_NOISEGRIDS_VERIFY"), "1",
                StringComparison.Ordinal);
            string workers = Environment.GetEnvironmentVariable(
                "CA_REGIONAL_NOISEGRIDS_WORKERS");
            int parsed;
            ConfiguredWorkers = workers != null
                && int.TryParse(workers, out parsed) && parsed >= 1
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
                            .ToString())
                + "; sequential-verify "
                + (VerifyAgainstSequential ? "on" : "off")
                + "; caps " + MaxGridsPerScope + " grids / "
                + (MaxTotalGridBytes / (1024 * 1024)) + " MB per scope; "
                + "worker graphs fail closed to exact Perlin/Const/"
                + "Displace/Scale/Rotate/ScaleBias/Multiply types; "
                + "materialize after max(" + MinimumRequestsBeforeGrid
                + ", ceil(area/workers)) in-bounds requests; one worker stays "
                + "native; env "
                + "CA_REGIONAL_NOISEGRIDS=0 disables at load, "
                + "_SEQUENTIAL=1 forces one worker, _WORKERS=n overrides, "
                + "_VERIFY=1 enables the live full-grid bitwise rebuild";
        }

        private static Scope Current
        {
            get
            {
                Stack<Scope> stack = scopes;
                return stack != null && stack.Count > 0 ? stack.Peek() : null;
            }
        }

        internal static void ContentsPrefix(Map map, out bool __state)
        {
            __state = false;
            Scope scope = null;
            try
            {
                if (scopes == null) scopes = new Stack<Scope>();
                scope = new Scope();
                scope.map = map;
                scopes.Push(scope);
                __state = true;
                scope.active = map != null
                    && CARegionalRiverPatchUtility.Active(map);
                if (!scope.active) return;
                scope.sizeX = map.Size.x;
                scope.sizeZ = map.Size.z;
                scope.verify = VerifyAgainstSequential;
                scope.workerCount = ForceSequential ? 1
                    : ConfiguredWorkers > 0 ? ConfiguredWorkers
                    : CARegionalRockChunksNoiseGrid.DefaultWorkerCount();
                long cells = (long)scope.sizeX * scope.sizeZ;
                scope.materializationThreshold = scope.workerCount <= 1
                    ? long.MaxValue
                    : Math.Max(MinimumRequestsBeforeGrid,
                        (cells + scope.workerCount - 1)
                            / scope.workerCount);
                Log.Message("[CA][Regional][Jobs][NoiseGrids] scope opened "
                    + "for " + scope.sizeX + "x" + scope.sizeZ + " map "
                    + map.uniqueID + "; " + PolicySummary()
                    + "; scope materialization threshold="
                    + (scope.materializationThreshold == long.MaxValue
                        ? "native-only" : scope.materializationThreshold
                            .ToString()));
            }
            catch (Exception error)
            {
                if (scope != null) scope.active = false;
                try
                {
                    Log.Warning("[CA][Regional][Jobs][NoiseGrids] scope "
                        + "open failed; native evaluation retained: "
                        + error);
                }
                catch { }
            }
        }

        internal static Exception ContentsFinalizer(Exception __exception,
            bool __state)
        {
            if (!__state) return __exception;
            Stack<Scope> stack = scopes;
            if (stack == null || stack.Count == 0) return __exception;
            Scope scope = stack.Pop();
            if (stack.Count == 0) scopes = null;
            if (!scope.active) return __exception;
            try
            {
                long served = 0;
                long buildMs = 0;
                long auditMs = 0;
                int builtGrids = 0;
                int sparseNativeModules = 0;
                int degradedGrids = 0;
                int auditedGrids = 0;
                long nativeWarmups = 0;
                bool allAuditsIdentical = true;
                foreach (KeyValuePair<ModuleBase, GridEntry> pair in
                    scope.entries)
                {
                    GridEntry entry = pair.Value;
                    served += entry.serves;
                    nativeWarmups += entry.nativeWarmupCalls;
                    buildMs += entry.buildMilliseconds;
                    auditMs += entry.auditMilliseconds;
                    if (entry.degraded) degradedGrids++;
                    else if (entry.grid != null) builtGrids++;
                    else sparseNativeModules++;
                    if (entry.audited)
                    {
                        auditedGrids++;
                        if (!entry.bitIdentical)
                            allAuditsIdentical = false;
                    }
                    Log.Message("[CA][Regional][Jobs][NoiseGrids] "
                        + entry.label + " closed: requests="
                        + entry.requests + ", gridServes=" + entry.serves
                        + ", nativeWarmup=" + entry.nativeWarmupCalls
                        + ", nativeFallback=" + entry.nativeFallbackCalls
                        + ", buildMs=" + entry.buildMilliseconds
                        + ", auditMs=" + entry.auditMilliseconds
                        + ", state=" + (entry.degraded ? "degraded-native"
                            : entry.grid != null ? "grid" : "sparse-native"));
                }
                Log.Message("[CA][Regional][Jobs][NoiseGrids] scope closed "
                    + "for map " + scope.map.uniqueID + ": " + builtGrids
                    + " grids built (" + (scope.totalGridBytes
                        / (1024 * 1024)) + " MB, " + buildMs
                    + " ms parallel build, " + auditMs + " ms audits, "
                    + "sequentialAudit=" + (auditedGrids == 0 ? "off"
                        : auditedGrids + " grids, allBitIdentical="
                            + allAuditsIdentical)
                    + "), " + sparseNativeModules + " sparse-native, "
                    + degradedGrids + " degraded, "
                    + scope.refusedGrids + " refused by caps, "
                    + scope.unsafeGraphs + " unsafe graphs kept native, "
                    + served + " values served, "
                    + nativeWarmups + " native warm-up evaluations, "
                    + scope.nativeFallbackCalls
                    + " native fallback evaluations, routerNativeOnly="
                    + scope.routerDegraded
                    + (__exception != null
                        ? "; closed after " + __exception.GetType().Name
                        : ""));
            }
            catch (Exception receiptError)
            {
                Log.Warning("[CA][Regional][Jobs][NoiseGrids] scope receipt "
                    + "failed; generation unaffected: " + receiptError);
            }
            scope.entries.Clear();
            return __exception;
        }

        // The routed evaluation. Public: any per-cell full-map consumer of
        // an immutable, already-seeded module graph may call this instead of
        // module.GetValue(cell) and inherits the grid, the audits, and
        // native-exact fallback behavior.
        public static float RoutedValue(ModuleBase module, IntVec3 cell)
        {
            Scope scope = Current;
            if (scope == null || !scope.active)
                return module.GetValue(cell);
            if (cell.y != 0
                || (uint)cell.x >= (uint)scope.sizeX
                || (uint)cell.z >= (uint)scope.sizeZ)
            {
                scope.nativeFallbackCalls++;
                return module.GetValue(cell);
            }
            if (scope.routerDegraded)
            {
                scope.nativeFallbackCalls++;
                return module.GetValue(cell);
            }
            bool useNative = false;
            try
            {
                GridEntry entry;
                if (!scope.entries.TryGetValue(module, out entry))
                {
                    entry = new GridEntry();
                    entry.label = module.GetType().Name + "#"
                        + (scope.entries.Count + 1);
                    scope.entries.Add(module, entry);
                }
                entry.requests++;
                if (entry.degraded)
                {
                    entry.nativeFallbackCalls++;
                    scope.nativeFallbackCalls++;
                    useNative = true;
                }
                else if (entry.grid == null)
                {
                    if (entry.requests < scope.materializationThreshold)
                    {
                        entry.nativeWarmupCalls++;
                        useNative = true;
                    }
                    else
                    {
                        BuildEntry(scope, module, entry);
                        if (entry.degraded || entry.grid == null)
                        {
                            entry.nativeFallbackCalls++;
                            scope.nativeFallbackCalls++;
                            useNative = true;
                        }
                    }
                }
                if (!useNative)
                {
                    entry.serves++;
                    return entry.grid[cell.z * scope.sizeX + cell.x];
                }
            }
            catch (Exception error)
            {
                // Do not place module.GetValue inside this try/catch: native
                // evaluation must execute exactly once and any native
                // exception must retain its original behavior. Only router
                // bookkeeping and grid access can trigger this degradation.
                scope.routerDegraded = true;
                scope.nativeFallbackCalls++;
                try
                {
                    Log.Warning("[CA][Regional][Jobs][NoiseGrids] router "
                        + "bookkeeping failed; this generation scope is now "
                        + "native-only: " + error);
                }
                catch
                {
                    // Failure reporting must not replace native generation.
                }
            }
            return module.GetValue(cell);
        }

        private static void BuildEntry(Scope scope, ModuleBase module,
            GridEntry entry)
        {
            try
            {
                string graphReason;
                if (!WorkerSafeGraph(module, out graphReason))
                {
                    entry.degraded = true;
                    scope.unsafeGraphs++;
                    try
                    {
                        Log.Message("[CA][Regional][Jobs][NoiseGrids] "
                            + entry.label + " kept native because its "
                            + "module graph is not worker-safe: "
                            + graphReason);
                    }
                    catch { }
                    return;
                }
                long gridBytes = 4L * scope.sizeX * scope.sizeZ;
                if (scope.builtGridCount >= MaxGridsPerScope
                    || scope.totalGridBytes + gridBytes > MaxTotalGridBytes)
                {
                    entry.degraded = true;
                    scope.refusedGrids++;
                    Log.Message("[CA][Regional][Jobs][NoiseGrids] "
                        + entry.label + " refused by scope caps; native "
                        + "evaluation retained for this module");
                    return;
                }
                Stopwatch watch = Stopwatch.StartNew();
                Exception failure;
                float[] grid = CARegionalRockChunksNoiseGrid.Build(module,
                    scope.sizeX, scope.sizeZ, scope.workerCount,
                    out failure);
                watch.Stop();
                entry.buildMilliseconds = watch.ElapsedMilliseconds;
                if (grid == null)
                {
                    entry.degraded = true;
                    Log.Warning("[CA][Regional][Jobs][NoiseGrids] "
                        + entry.label + " parallel build failed ("
                        + (failure?.GetType().Name ?? "unknown") + ": "
                        + (failure?.Message ?? "no detail")
                        + "); native evaluation retained");
                    return;
                }
                entry.parallelChecksum =
                    CARegionalRockChunksNoiseGrid.Checksum(grid);
                if (scope.verify && scope.workerCount > 1)
                {
                    watch.Restart();
                    Exception verifyFailure;
                    float[] sequential = CARegionalRockChunksNoiseGrid
                        .Build(module, scope.sizeX, scope.sizeZ, 1,
                            out verifyFailure);
                    watch.Stop();
                    entry.auditMilliseconds = watch.ElapsedMilliseconds;
                    if (sequential == null)
                    {
                        entry.degraded = true;
                        Log.Warning("[CA][Regional][Jobs][NoiseGrids] "
                            + entry.label + " sequential audit rebuild "
                            + "failed ("
                            + (verifyFailure?.GetType().Name ?? "unknown")
                            + "); parallel grid discarded and native "
                            + "evaluation retained");
                        return;
                    }
                    entry.sequentialChecksum =
                        CARegionalRockChunksNoiseGrid.Checksum(sequential);
                    int mismatch = CARegionalRockChunksNoiseGrid
                        .FirstMismatch(grid, sequential);
                    entry.audited = true;
                    entry.bitIdentical = mismatch == -1;
                    if (!entry.bitIdentical)
                    {
                        // Serve the sequential grid: it is the native
                        // result by construction.
                        try
                        {
                            Log.Error("[CA][Regional][Jobs][NoiseGrids] "
                                + entry.label + " parallel grid diverged "
                                + "from sequential at index " + mismatch
                                + "; serving the sequential grid");
                        }
                        catch { }
                        grid = sequential;
                    }
                }
                entry.grid = grid;
                scope.builtGridCount++;
                scope.totalGridBytes += gridBytes;
                try
                {
                    Log.Message("[CA][Regional][Jobs][NoiseGrids] "
                        + entry.label + " built for map "
                        + scope.map.uniqueID + ": "
                        + entry.buildMilliseconds + " ms across "
                        + scope.workerCount + " workers, checksum "
                        + entry.parallelChecksum.ToString("X16")
                        + (entry.audited
                            ? ", sequential rebuild "
                                + entry.auditMilliseconds
                                + " ms bitIdentical="
                                + entry.bitIdentical
                            : ", audit off"));
                }
                catch { }
            }
            catch (Exception error)
            {
                entry.degraded = true;
                entry.grid = null;
                try
                {
                    Log.Warning("[CA][Regional][Jobs][NoiseGrids] "
                        + entry.label + " build error; native evaluation "
                        + "retained: " + error);
                }
                catch { }
            }
        }

        // ModuleBase is an open hierarchy and not every graph is pure:
        // Verse.Noise.Cache, for example, mutates a shared last-coordinate
        // cache inside GetValue. Worker evaluation is therefore permitted
        // only for the exact stateless engine node types exercised by the
        // final harness and present in the installed elevation, fertility,
        // patch-maker, and rock-selection graphs. Unknown subclasses stay
        // native instead of becoming an optimistic concurrency contract.
        private static bool WorkerSafeGraph(ModuleBase root,
            out string reason)
        {
            reason = null;
            try
            {
                var visiting = new HashSet<ModuleBase>(
                    ModuleReferenceComparer.Instance);
                var visited = new HashSet<ModuleBase>(
                    ModuleReferenceComparer.Instance);
                return WorkerSafeGraph(root, visiting, visited, out reason);
            }
            catch (Exception error)
            {
                reason = "inspection failed (" + error.GetType().Name
                    + ": " + error.Message + ")";
                return false;
            }
        }

        private static bool WorkerSafeGraph(ModuleBase module,
            HashSet<ModuleBase> visiting, HashSet<ModuleBase> visited,
            out string reason)
        {
            reason = null;
            if (module == null)
            {
                reason = "null module";
                return false;
            }
            if (module.IsDisposed)
            {
                reason = module.GetType().FullName + " is disposed";
                return false;
            }
            Type type = module.GetType();
            int expectedSources;
            if (type == typeof(Perlin) || type == typeof(Const))
                expectedSources = 0;
            else if (type == typeof(Displace)) expectedSources = 4;
            else if (type == typeof(Scale) || type == typeof(Rotate)
                || type == typeof(ScaleBias)) expectedSources = 1;
            else if (type == typeof(Multiply)) expectedSources = 2;
            else
            {
                visiting.Remove(module);
                reason = "unapproved exact type " + type.FullName;
                return false;
            }
            if (visited.Contains(module)) return true;
            if (!visiting.Add(module))
            {
                reason = "cycle at " + type.FullName;
                return false;
            }
            if (module.SourceModuleCount != expectedSources)
            {
                visiting.Remove(module);
                reason = type.FullName + " has " + module.SourceModuleCount
                    + " sources; expected " + expectedSources;
                return false;
            }
            for (int i = 0; i < expectedSources; i++)
            {
                ModuleBase child = module[i];
                if (WorkerSafeGraph(child, visiting, visited, out reason))
                    continue;
                visiting.Remove(module);
                return false;
            }
            visiting.Remove(module);
            visited.Add(module);
            return true;
        }
    }

    internal static class CARegionalTerrainNoisePatches
    {
        internal static bool ElevationFertilityTranspilerApplied;
        internal static bool TerrainAtTranspilerApplied;
        internal static bool TerrainAtLambdaTranspilerApplied;

        // GenStep_ElevationFertility.Generate holds exactly two
        // ModuleBase.GetValue(IntVec3) sites in the installed 1.6.4871 IL:
        // the elevation loop and the fertility loop. Both route to the
        // service; the surrounding Mathf.Min, grid writes, and traversal
        // stay native.
        internal static IEnumerable<CodeInstruction>
            ElevationFertilityTranspiler(
                IEnumerable<CodeInstruction> instructions)
        {
            int replaced;
            List<CodeInstruction> result = RouteGetValueSites(instructions,
                expected: 2, out replaced);
            ElevationFertilityTranspilerApplied = replaced == 2;
            if (!ElevationFertilityTranspilerApplied)
                Log.Warning("[CA][Regional][Jobs][NoiseGrids] expected two "
                    + "GetValue sites in GenStep_ElevationFertility"
                    + ".Generate, found " + replaced
                    + "; native IL untouched");
            return result;
        }

        // TerrainPatchMaker.TerrainAt: one site (the threshold evaluation).
        internal static IEnumerable<CodeInstruction> TerrainAtTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            int replaced;
            List<CodeInstruction> result = RouteGetValueSites(instructions,
                expected: 1, out replaced);
            TerrainAtTranspilerApplied = replaced == 1;
            if (!TerrainAtTranspilerApplied)
                Log.Warning("[CA][Regional][Jobs][NoiseGrids] expected one "
                    + "GetValue site in TerrainPatchMaker.TerrainAt, found "
                    + replaced + "; native IL untouched");
            return result;
        }

        // The minSize flood-fill predicate lambda holds the second
        // TerrainPatchMaker site.
        internal static IEnumerable<CodeInstruction>
            TerrainAtLambdaTranspiler(
                IEnumerable<CodeInstruction> instructions)
        {
            int replaced;
            List<CodeInstruction> result = RouteGetValueSites(instructions,
                expected: 1, out replaced);
            TerrainAtLambdaTranspilerApplied = replaced == 1;
            if (!TerrainAtLambdaTranspilerApplied)
                Log.Warning("[CA][Regional][Jobs][NoiseGrids] expected one "
                    + "GetValue site in the TerrainAt flood-fill lambda, "
                    + "found " + replaced + "; native IL untouched");
            return result;
        }

        // Two-pass shared rewriter: the stream is only mutated when the
        // match count equals the expectation for that exact method.
        private static List<CodeInstruction> RouteGetValueSites(
            IEnumerable<CodeInstruction> instructions, int expected,
            out int matches)
        {
            MethodInfo nativeGetValue = AccessTools.Method(
                typeof(ModuleBase), nameof(ModuleBase.GetValue),
                new[] { typeof(IntVec3) });
            MethodInfo routed = AccessTools.Method(
                typeof(CARegionalNoiseGridService),
                nameof(CARegionalNoiseGridService.RoutedValue));
            List<CodeInstruction> buffer =
                new List<CodeInstruction>(instructions);
            matches = 0;
            for (int i = 0; i < buffer.Count; i++)
                if (buffer[i].Calls(nativeGetValue)) matches++;
            if (matches != expected) return buffer;
            for (int i = 0; i < buffer.Count; i++)
            {
                if (!buffer[i].Calls(nativeGetValue)) continue;
                // Same stack shape: [module, cell] -> float32. Labels and
                // block markers on the instruction are preserved.
                buffer[i].opcode = OpCodes.Call;
                buffer[i].operand = routed;
            }
            return buffer;
        }
    }

    // Self-contained installer: own Harmony ID, no [HarmonyPatch]
    // attributes, rollback on required failure; each transpile target is
    // count-checked and individually reverts to native IL on any surprise.
    [StaticConstructorOnStartup]
    internal static class CARegionalTerrainNoiseInstaller
    {
        private const string HarmonyId =
            "ellyj3rain.colonistawareness.regionaljobs.noisegrids";

        static CARegionalTerrainNoiseInstaller()
        {
            Harmony harmony = null;
            try
            {
                if (!CARegionalNoiseGridService.Installed)
                {
                    Log.Message("[CA][Regional][Jobs][NoiseGrids] disabled "
                        + "by CA_REGIONAL_NOISEGRIDS=0; no patches applied");
                    return;
                }
                harmony = new Harmony(HarmonyId);
                MethodInfo contents = AccessTools.Method(
                    typeof(MapGenerator),
                    nameof(MapGenerator.GenerateContentsIntoMap));
                MethodInfo elevationFertility = AccessTools.DeclaredMethod(
                    typeof(GenStep_ElevationFertility),
                    nameof(GenStep_ElevationFertility.Generate),
                    new[] { typeof(Map), typeof(GenStepParams) });
                if (contents == null || elevationFertility == null)
                    throw new MissingMethodException(
                        "noise-grid scope or elevation/fertility target "
                        + "not found");
                harmony.Patch(contents,
                    prefix: Method(nameof(
                        CARegionalNoiseGridService.ContentsPrefix),
                        Priority.First),
                    finalizer: Method(nameof(
                        CARegionalNoiseGridService.ContentsFinalizer)));
                harmony.Patch(elevationFertility,
                    transpiler: new HarmonyMethod(AccessTools.Method(
                        typeof(CARegionalTerrainNoisePatches),
                        nameof(CARegionalTerrainNoisePatches
                            .ElevationFertilityTranspiler))));
                if (!CARegionalTerrainNoisePatches
                    .ElevationFertilityTranspilerApplied)
                    throw new InvalidOperationException(
                        "elevation/fertility noise call-site replacement "
                        + "was not applied");
                string optional = InstallTerrainPatchMaker(harmony);
                Log.Message("[CA][Regional][Jobs][NoiseGrids] regional "
                    + "terrain noise grids installed: elevation/fertility "
                    + "routed; " + optional + "; "
                    + CARegionalNoiseGridService.PolicySummary()
                    + "; regional rock-selection noises routed directly.");
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
                        Log.Error("[CA][Regional][Jobs][NoiseGrids] "
                            + "rollback failed: " + rollbackError);
                    }
                }
                Log.Error("[CA][Regional][Jobs][NoiseGrids] install failed; "
                    + (rolledBack
                        ? "all noise-grid patches rolled back"
                        : "partial patch state could not be excluded")
                    + ": " + error);
            }
        }

        private static string InstallTerrainPatchMaker(Harmony harmony)
        {
            string direct;
            try
            {
                MethodInfo terrainAt = AccessTools.DeclaredMethod(
                    typeof(TerrainPatchMaker),
                    nameof(TerrainPatchMaker.TerrainAt));
                if (terrainAt == null)
                {
                    direct = "TerrainAt missing";
                }
                else
                {
                    harmony.Patch(terrainAt,
                        transpiler: new HarmonyMethod(AccessTools.Method(
                            typeof(CARegionalTerrainNoisePatches),
                            nameof(CARegionalTerrainNoisePatches
                                .TerrainAtTranspiler))));
                    direct = CARegionalTerrainNoisePatches
                        .TerrainAtTranspilerApplied
                            ? "TerrainAt routed"
                            : "TerrainAt left native (site count)";
                }
            }
            catch (Exception error)
            {
                direct = "TerrainAt left native ("
                    + error.GetType().Name + ")";
            }

            string lambda;
            try
            {
                MethodInfo lambdaMethod = FindTerrainAtLambda();
                if (lambdaMethod == null)
                {
                    lambda = "flood-fill lambda not found";
                }
                else
                {
                    harmony.Patch(lambdaMethod,
                        transpiler: new HarmonyMethod(AccessTools.Method(
                            typeof(CARegionalTerrainNoisePatches),
                            nameof(CARegionalTerrainNoisePatches
                                .TerrainAtLambdaTranspiler))));
                    lambda = CARegionalTerrainNoisePatches
                        .TerrainAtLambdaTranspilerApplied
                            ? "flood-fill lambda routed"
                            : "flood-fill lambda left native (site count)";
                }
            }
            catch (Exception error)
            {
                lambda = "flood-fill lambda left native ("
                    + error.GetType().Name + ")";
            }
            return direct + "; " + lambda;
        }

        // The minSize flood-fill predicate compiles to an instance method
        // named "<TerrainAt>b__..." with signature (IntVec3) -> bool on
        // TerrainPatchMaker itself (verified in the installed IL). Resolved
        // structurally so a compiler-numbering change degrades to native
        // instead of patching the wrong method.
        private static MethodInfo FindTerrainAtLambda()
        {
            MethodInfo found = null;
            foreach (MethodInfo method in typeof(TerrainPatchMaker)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (!method.Name.StartsWith("<TerrainAt>b__",
                    StringComparison.Ordinal)) continue;
                if (method.ReturnType != typeof(bool)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1
                    || parameters[0].ParameterType != typeof(IntVec3))
                    continue;
                if (found != null) return null;
                found = method;
            }
            return found;
        }

        private static HarmonyMethod Method(string name, int priority = -1)
        {
            HarmonyMethod method = new HarmonyMethod(AccessTools.Method(
                typeof(CARegionalNoiseGridService), name));
            if (priority >= 0) method.priority = priority;
            return method;
        }
    }
}
