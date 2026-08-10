using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Regional map-generation jobs, slice 4 (integration-hardened, exact
    // review seam): the plant frozen-term prediction screen.
    //
    // The screen predicts, from state frozen for the whole Plants step,
    // when PlantUtility.CanEverPlantAt must reject: fertility strictly
    // below the def minimum (unless the def ignores fertility), whole-map
    // per-def temperature, and per-cell pollution - the three
    // unconditional early-false terms of the installed method, evaluated
    // before any mutable read. CanEverPlantAt consumes no Rand.
    //
    // Seam (per independent exact review): no caller transpiler and no
    // Harmony bypass. The full AcceptanceReport overload of
    // PlantUtility.CanEverPlantAt carries a Priority.Last prefix and a
    // Priority.Last postfix, so the bool wrapper and every other patch -
    // including ReGrowth's monotonic terrain-rejection postfix - stay
    // in-chain. The scope is GenStep_Plants.Generate only.
    //
    // Modes:
    //   - Default (audit-only): the prefix predicts, the original ALWAYS
    //     runs, and the postfix - running after every other postfix -
    //     compares the prediction against the final post-Harmony result.
    //     Nothing is ever skipped, so the default mode is behaviorally
    //     inert and produces a complete mismatch census, not a sample.
    //   - Serve (CA_REGIONAL_PLANTMASK_SERVE=1, to be enabled only after
    //     a first zero-mismatch live gate): on a proven reject the last
    //     prefix sets blockingThing=null and __result=false and skips the
    //     original; postfixes still run, and the only allowlisted foreign
    //     postfix (ReGrowth's) can only flip results toward rejection, so
    //     a served false can never become a wrongly-served accept. The
    //     postfix still verifies the final result and degrades on any
    //     anomaly.
    //
    // Fail-closed compatibility gate, exact per method AND patch kind:
    // before the first prediction the scope enumerates the live Harmony
    // patch state of the frozen-term surface - both CanEverPlantAt
    // overloads, FertilityGrid.FertilityAt / CalculateFertilityAt,
    // GridsUtility.IsPolluted, PollutionGrid.IsPolluted. Every observed
    // (method, patch-kind, owner) must be explicitly allowlisted or the
    // screen degrades for the generation. "Helixien.ReGrowthCore" is
    // allowed for exactly two entries, each decompiled and proven:
    //   - CanEverPlantAt(AcceptanceReport) POSTFIX: monotonic extra
    //     terrain rejection (PlantExtension.terrainListToGrowOnly) over
    //     frozen terrain - can only turn accept into reject;
    //   - FertilityGrid.CalculateFertilityAt TRANSPILER: biome-specific
    //     terrain fertility over frozen state - and because this module's
    //     extraction calls the PATCHED FertilityAt, the snapshot already
    //     carries its values.
    // Any other patch by that owner, any patch on the bool overload or
    // the pollution reads, and any unknown owner fail the gate. This
    // per-entry proof plus the frozen-state argument is what grounds
    // exactness; the audit census is the live evidence stream, and in
    // serve mode the prediction paths were already required to survive a
    // zero-mismatch audited generation.
    //
    // Cost containment: predictions need one whole-map snapshot of the
    // native fertility/pollution reads. Extraction runs on the generation
    // thread only after a demand warmup (the step must actually issue
    // 4,096 in-scope evaluations first) and only under a checked byte
    // cap, so sparse or tiny steps never pay a full-map pass. There are
    // no worker threads in this module - the direct O(1) predictor
    // replaced the earlier eager per-def masks (a mask bit costs the same
    // nanoseconds as the direct comparison, and whole-map per-def builds
    // overcompute on mixed-biome maps), and the worker join machinery
    // went with them.
    //
    // Every acceleration failure - scope, gate, extraction, cache,
    // bookkeeping - converges to the unmodified native chain (prefix
    // returns true, postfix does nothing), preserving native exceptions;
    // the prediction block never wraps the original call. The scope
    // prefix hands its finalizer a bool __state that only becomes true
    // once this scope's push completed, so a failed nested prefix can
    // never pop an outer scope. Counters: audit-mode calls are native
    // calls, mismatches are never counted as served shortcuts, and calls
    // routed while degraded stay visible as degradedNativeCalls.

    // Pure decision logic, headless-tested by the offline harness.
    public static class CARegionalPlantRejectionTerms
    {
        // The frozen-term rejection predicate, mirroring the native order:
        // fertility strictly below the def minimum (unless the def ignores
        // fertility), then the pollution requirement.
        public static bool Rejects(float fertility, bool polluted,
            bool completelyIgnoreFertility, float fertilityMin,
            Pollution pollution)
        {
            if (!completelyIgnoreFertility && fertility < fertilityMin)
                return true;
            if (pollution == Pollution.CleanOnly) return polluted;
            if (pollution == Pollution.PollutedOnly) return !polluted;
            return false;
        }

        // Fail-closed allowlist decision, exact per method and patch kind.
        // allowedEntries hold "methodLabel|kind|owner" strings; every
        // observed owner must match one exactly. Null/empty owners are
        // reported, never ignored; a null allowlist rejects everything.
        public static void AppendForeignPatches(string methodLabel,
            string kind, IEnumerable<string> patchOwners,
            string[] allowedEntries, List<string> violations)
        {
            if (patchOwners == null) return;
            foreach (string owner in patchOwners)
            {
                bool allowed = false;
                if (owner != null && allowedEntries != null)
                {
                    string entry = methodLabel + "|" + kind + "|" + owner;
                    for (int i = 0; i < allowedEntries.Length; i++)
                    {
                        if (string.Equals(entry, allowedEntries[i],
                            StringComparison.Ordinal))
                        {
                            allowed = true;
                            break;
                        }
                    }
                }
                if (!allowed)
                    violations.Add(methodLabel + "/" + kind + " <- "
                        + (owner ?? "(null owner)"));
            }
        }
    }

    internal static class CARegionalPlantMaskPatches
    {
        private struct DefTerms
        {
            internal bool screen;
            internal bool temperatureRejected;
            internal bool completelyIgnoreFertility;
            internal float fertilityMin;
            internal Pollution pollution;
        }

        private sealed class Scope
        {
            internal Map map;
            internal bool active;
            internal bool degraded;
            internal int sizeX;
            internal int sizeZ;
            internal int cellCount;
            internal bool serveMode;
            internal bool compatibilityChecked;
            internal float[] fertility;
            internal ulong[] pollutedBits;
            internal long extractionMilliseconds = -1;
            internal long snapshotBytes;
            internal readonly Dictionary<ThingDef, DefTerms> defTerms =
                new Dictionary<ThingDef, DefTerms>();
            internal int temperatureRejectedDefs;
            internal long routedCalls;
            internal long warmupNativeCalls;
            internal long nativeCalls;
            internal long degradedNativeCalls;
            internal long auditPredictions;
            internal long auditConfirmed;
            internal long auditMismatches;
            internal long servedFalse;
            internal long serveAnomalies;

            internal void Degrade(string reason)
            {
                if (degraded) return;
                degraded = true;
                Log.Warning("[CA][Regional][Jobs][PlantMask] screen "
                    + "degraded to native evaluation: " + reason);
            }
        }

        internal const string HarmonyId =
            "ellyj3rain.colonistawareness.regionaljobs.plantmask";

        private const string SurfaceAcceptanceOverload =
            "PlantUtility.CanEverPlantAt(AcceptanceReport)";

        // Exact (method, kind, owner) allowlist. ReGrowth is proven for
        // precisely two entries; this module's own prefix/postfix pair on
        // the AcceptanceReport overload completes the set. Everything
        // else on the surface - including any future patch by these same
        // owners on another method or kind - fails the gate.
        internal static readonly string[] AllowedSurfaceEntries =
        {
            SurfaceAcceptanceOverload + "|postfix|Helixien.ReGrowthCore",
            "FertilityGrid.CalculateFertilityAt|transpiler|"
                + "Helixien.ReGrowthCore",
            SurfaceAcceptanceOverload + "|prefix|" + HarmonyId,
            SurfaceAcceptanceOverload + "|postfix|" + HarmonyId,
        };

        // Demand warmup: the Plants step must issue this many in-scope
        // evaluations before the whole-map snapshot is extracted, so
        // sparse steps never pay the full-map pass (mirrors the a2-171
        // noise-grid warmup convention).
        internal const int WarmupCallsBeforeExtraction = 4096;

        // Checked snapshot cap: 4 bytes fertility + 1/8 byte pollution
        // per cell must fit, with headroom, under this bound.
        internal const long MaxSnapshotBytes = 96L * 1024L * 1024L;

        internal static readonly bool Installed;
        private static readonly bool ServeModeRequested;

        [ThreadStatic]
        private static Stack<Scope> scopes;

        static CARegionalPlantMaskPatches()
        {
            Installed = !string.Equals(
                Environment.GetEnvironmentVariable("CA_REGIONAL_PLANTMASK"),
                "0", StringComparison.Ordinal);
            ServeModeRequested = string.Equals(
                Environment.GetEnvironmentVariable(
                    "CA_REGIONAL_PLANTMASK_SERVE"), "1",
                StringComparison.Ordinal);
        }

        internal static string PolicySummary()
        {
            return (ServeModeRequested
                    ? "SERVE mode (proven rejects skip the original; "
                        + "postfix chain still runs and re-verifies)"
                    : "audit-only mode (originals always run; every "
                        + "prediction compared against the final "
                        + "post-Harmony result)")
                + "; frozen-term surface gate allowing exactly "
                + AllowedSurfaceEntries.Length + " proven method/kind/owner "
                + "entries; extraction after "
                + WarmupCallsBeforeExtraction + " in-scope calls under a "
                + (MaxSnapshotBytes / (1024 * 1024)) + " MB checked cap; "
                + "env CA_REGIONAL_PLANTMASK=0 disables at load, "
                + "CA_REGIONAL_PLANTMASK_SERVE=1 enables serving only "
                + "after a first zero-mismatch live gate";
        }

        private static Scope Current
        {
            get
            {
                Stack<Scope> stack = scopes;
                return stack != null && stack.Count > 0 ? stack.Peek() : null;
            }
        }

        // ---- GenStep_Plants.Generate scope ----
        // __state becomes true only after THIS scope's push completed, so
        // the finalizer of a prefix that failed before pushing can never
        // pop an outer scope.

        internal static void PlantsPrefix(Map map, out bool __state)
        {
            __state = false;
            Scope scope;
            try
            {
                if (scopes == null) scopes = new Stack<Scope>();
                scope = new Scope();
                scope.map = map;
                scopes.Push(scope);
                __state = true;
            }
            catch (Exception error)
            {
                Log.Warning("[CA][Regional][Jobs][PlantMask] scope push "
                    + "failed; native evaluation retained: " + error);
                return;
            }
            try
            {
                scope.active = map != null
                    && CARegionalRiverPatchUtility.Active(map);
                if (!scope.active) return;
                scope.sizeX = map.Size.x;
                scope.sizeZ = map.Size.z;
                scope.cellCount = checked(scope.sizeX * scope.sizeZ);
                scope.serveMode = ServeModeRequested;
                Log.Message("[CA][Regional][Jobs][PlantMask] scope opened "
                    + "for " + scope.sizeX + "x" + scope.sizeZ + " map "
                    + map.uniqueID + "; " + PolicySummary());
            }
            catch (Exception error)
            {
                scope.active = false;
                Log.Warning("[CA][Regional][Jobs][PlantMask] scope open "
                    + "failed; native evaluation retained: " + error);
            }
        }

        internal static Exception PlantsFinalizer(Exception __exception,
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
                Log.Message("[CA][Regional][Jobs][PlantMask] scope closed "
                    + "for map " + scope.map.uniqueID + ": "
                    + scope.routedCalls + " in-scope evaluations = "
                    + scope.warmupNativeCalls + " warmup native + "
                    + scope.nativeCalls + " native (of which "
                    + scope.auditPredictions + " audited predictions: "
                    + scope.auditConfirmed + " confirmed, "
                    + scope.auditMismatches + " mismatches) + "
                    + scope.servedFalse + " served rejects ("
                    + scope.serveAnomalies + " serve anomalies) + "
                    + scope.degradedNativeCalls + " degraded native; "
                    + scope.defTerms.Count + " defs cached ("
                    + scope.temperatureRejectedDefs
                    + " whole-def temperature rejections); extraction "
                    + (scope.extractionMilliseconds < 0
                        ? "not triggered"
                        : scope.extractionMilliseconds + " ms, "
                            + scope.snapshotBytes + " bytes")
                    + (scope.degraded ? "; DEGRADED" : "")
                    + (__exception != null
                        ? "; closed after " + __exception.GetType().Name
                        : ""));
            }
            catch (Exception receiptError)
            {
                Log.Warning("[CA][Regional][Jobs][PlantMask] scope receipt "
                    + "failed; generation unaffected: " + receiptError);
            }
            scope.defTerms.Clear();
            scope.fertility = null;
            scope.pollutedBits = null;
            return __exception;
        }

        // ---- the in-chain patch pair on the AcceptanceReport overload ----
        // Prefix modes handed to the postfix through __state.

        private const int ModeInactive = 0;
        private const int ModeNative = 1;
        private const int ModeWarmupNative = 2;
        private const int ModeDegradedNative = 3;
        private const int ModeAudit = 4;
        private const int ModeServe = 5;

        internal static bool CanEverPlantAtPrefix(ThingDef plantDef,
            IntVec3 c, Map map, ref Thing blockingThing,
            bool canWipePlantsExceptTree, bool checkMapTemperature,
            ref AcceptanceReport __result, out int __state)
        {
            __state = ModeInactive;
            try
            {
                Scope scope = Current;
                if (scope == null || !scope.active
                    || !ReferenceEquals(map, scope.map)
                    || c.y != 0
                    || (uint)c.x >= (uint)scope.sizeX
                    || (uint)c.z >= (uint)scope.sizeZ)
                    return true;
                scope.routedCalls++;
                if (scope.degraded)
                {
                    __state = ModeDegradedNative;
                    return true;
                }
                if (scope.routedCalls <= WarmupCallsBeforeExtraction)
                {
                    __state = ModeWarmupNative;
                    return true;
                }
                if (!scope.compatibilityChecked)
                    VerifyCompatibilitySurface(scope);
                if (!scope.degraded && scope.fertility == null)
                    ExtractCellTerms(scope);
                if (scope.degraded)
                {
                    __state = ModeDegradedNative;
                    return true;
                }
                DefTerms terms = GetDefTerms(scope, plantDef);
                if (!terms.screen)
                {
                    __state = ModeNative;
                    return true;
                }
                bool reject = checkMapTemperature
                    && terms.temperatureRejected;
                if (!reject)
                {
                    int cell = c.z * scope.sizeX + c.x;
                    reject = CARegionalPlantRejectionTerms.Rejects(
                        scope.fertility[cell],
                        (scope.pollutedBits[cell >> 6]
                            >> (cell & 63) & 1UL) != 0,
                        terms.completelyIgnoreFertility,
                        terms.fertilityMin,
                        terms.pollution);
                }
                if (!reject)
                {
                    __state = ModeNative;
                    return true;
                }
                if (!scope.serveMode)
                {
                    // Audit-only: the original always runs; the postfix
                    // compares this prediction against the final result.
                    __state = ModeAudit;
                    return true;
                }
                // Serve mode: skip the original with the proven rejection.
                // All postfixes still run; the allowlisted foreign postfix
                // is monotonic toward rejection, and our own postfix
                // re-verifies the final result.
                blockingThing = null;
                __result = false;
                __state = ModeServe;
                return false;
            }
            catch (Exception accelerationError)
            {
                __state = ModeInactive;
                try
                {
                    Current?.Degrade("prefix acceleration failure ("
                        + accelerationError.GetType().Name + ": "
                        + accelerationError.Message + ")");
                }
                catch (Exception)
                {
                    // Degradation is best-effort; the native chain runs.
                }
                return true;
            }
        }

        internal static void CanEverPlantAtPostfix(ThingDef plantDef,
            IntVec3 c, ref AcceptanceReport __result, int __state,
            bool __runOriginal)
        {
            if (__state == ModeInactive) return;
            try
            {
                Scope scope = Current;
                if (scope == null) return;
                switch (__state)
                {
                    case ModeWarmupNative:
                        scope.warmupNativeCalls++;
                        break;
                    case ModeDegradedNative:
                        scope.degradedNativeCalls++;
                        break;
                    case ModeNative:
                        scope.nativeCalls++;
                        break;
                    case ModeAudit:
                        // An audit comparison is only meaningful against a
                        // result the original actually produced. If some
                        // other patch skipped the original, this is not a
                        // confirmed native audit: leave the chain result
                        // untouched and stop predicting.
                        if (!__runOriginal)
                        {
                            scope.Degrade("audit invalidated: the "
                                + "original was skipped by another patch "
                                + "for " + (plantDef?.defName ?? "null")
                                + " at " + c);
                            break;
                        }
                        // The original ran: this is a native call whose
                        // final post-Harmony result must confirm the
                        // prediction. A mismatch is never a served
                        // shortcut - it is a falsified assumption.
                        scope.nativeCalls++;
                        scope.auditPredictions++;
                        if (__result.Accepted)
                        {
                            scope.auditMismatches++;
                            scope.Degrade("audited prediction falsified: "
                                + "final result accepted "
                                + (plantDef?.defName ?? "null") + " at "
                                + c);
                        }
                        else
                        {
                            scope.auditConfirmed++;
                        }
                        break;
                    case ModeServe:
                        if (__result.Accepted)
                        {
                            // A later patch flipped a served rejection to
                            // accepted. The caller receives that accept;
                            // record and stop serving.
                            scope.serveAnomalies++;
                            scope.Degrade("served rejection flipped to "
                                + "accepted by a later patch for "
                                + (plantDef?.defName ?? "null") + " at "
                                + c);
                        }
                        else
                        {
                            scope.servedFalse++;
                        }
                        break;
                }
            }
            catch (Exception bookkeepingError)
            {
                // The chain's result stands, but bookkeeping that cannot
                // run also cannot supervise serve mode: degrade so later
                // calls return to native instead of silently retaining
                // shortcuts.
                try
                {
                    Current?.Degrade("postfix bookkeeping failure ("
                        + bookkeepingError.GetType().Name + ": "
                        + bookkeepingError.Message + ")");
                }
                catch (Exception)
                {
                    // Best-effort only.
                }
            }
        }

        // ---- fail-closed compatibility gate ----

        private static void VerifyCompatibilitySurface(Scope scope)
        {
            scope.compatibilityChecked = true;
            try
            {
                var violations = new List<string>();
                int patchesSeen = 0;
                patchesSeen += InspectSurfaceMethod(
                    AccessTools.Method(typeof(PlantUtility),
                        nameof(PlantUtility.CanEverPlantAt),
                        new[]
                        {
                            typeof(ThingDef), typeof(IntVec3), typeof(Map),
                            typeof(bool), typeof(bool)
                        }),
                    "PlantUtility.CanEverPlantAt(bool)", violations);
                patchesSeen += InspectSurfaceMethod(
                    AccessTools.DeclaredMethod(typeof(PlantUtility),
                        nameof(PlantUtility.CanEverPlantAt),
                        new[]
                        {
                            typeof(ThingDef), typeof(IntVec3), typeof(Map),
                            typeof(Thing).MakeByRefType(), typeof(bool),
                            typeof(bool), typeof(bool)
                        }),
                    SurfaceAcceptanceOverload, violations);
                patchesSeen += InspectSurfaceMethod(
                    AccessTools.Method(typeof(FertilityGrid),
                        nameof(FertilityGrid.FertilityAt)),
                    "FertilityGrid.FertilityAt", violations);
                patchesSeen += InspectSurfaceMethod(
                    AccessTools.Method(typeof(FertilityGrid),
                        "CalculateFertilityAt"),
                    "FertilityGrid.CalculateFertilityAt", violations);
                patchesSeen += InspectSurfaceMethod(
                    AccessTools.Method(typeof(GridsUtility),
                        nameof(GridsUtility.IsPolluted)),
                    "GridsUtility.IsPolluted", violations);
                patchesSeen += InspectSurfaceMethod(
                    AccessTools.Method(typeof(PollutionGrid),
                        nameof(PollutionGrid.IsPolluted)),
                    "PollutionGrid.IsPolluted", violations);
                if (violations.Count > 0)
                {
                    scope.Degrade("fail-closed: unproven Harmony patches "
                        + "on the frozen-term surface ["
                        + string.Join("; ", violations) + "]");
                    return;
                }
                Log.Message("[CA][Regional][Jobs][PlantMask] frozen-term "
                    + "surface verified for map " + scope.map.uniqueID
                    + ": " + patchesSeen + " surface patches, every "
                    + "(method, kind, owner) explicitly proven");
            }
            catch (Exception error)
            {
                // Fail closed: an unverifiable surface is a disabled
                // screen, never an assumed-safe one.
                scope.Degrade("compatibility verification failed ("
                    + error.GetType().Name + ": " + error.Message + ")");
            }
        }

        private static int InspectSurfaceMethod(MethodBase method,
            string label, List<string> violations)
        {
            if (method == null)
            {
                violations.Add(label + "/resolve <- (method not found)");
                return 0;
            }
            HarmonyLib.Patches info = Harmony.GetPatchInfo(method);
            if (info == null) return 0;
            int seen = 0;
            seen += InspectPatchList(info.Prefixes, label, "prefix",
                violations);
            seen += InspectPatchList(info.Postfixes, label, "postfix",
                violations);
            seen += InspectPatchList(info.Transpilers, label, "transpiler",
                violations);
            seen += InspectPatchList(info.Finalizers, label, "finalizer",
                violations);
            return seen;
        }

        private static int InspectPatchList(
            System.Collections.ObjectModel.ReadOnlyCollection<Patch> patches,
            string label, string kind, List<string> violations)
        {
            if (patches == null) return 0;
            var owners = new List<string>(patches.Count);
            foreach (Patch patch in patches) owners.Add(patch.owner);
            CARegionalPlantRejectionTerms.AppendForeignPatches(label, kind,
                owners, AllowedSurfaceEntries, violations);
            return owners.Count;
        }

        // ---- frozen-term extraction and the per-def cache ----

        // One gen-thread pass through the NATIVE fertility and pollution
        // reads, only after the demand warmup and under the checked byte
        // cap. Their semantics - edifice overrides, Biotech gating,
        // pollution caps, and the allowlisted ReGrowth biome-specific
        // fertility transpiler - are inherited, not reimplemented, and the
        // grids they read are frozen while GenStep_Plants spawns only
        // plants.
        private static void ExtractCellTerms(Scope scope)
        {
            try
            {
                int cells = scope.cellCount;
                long pollutionWords;
                long bytes;
                checked
                {
                    pollutionWords = ((long)cells + 63L) >> 6;
                    bytes = 4L * cells + pollutionWords * 8L;
                }
                if (cells <= 0 || bytes > MaxSnapshotBytes)
                {
                    scope.Degrade("snapshot cap: " + bytes
                        + " bytes for " + cells + " cells exceeds "
                        + MaxSnapshotBytes);
                    return;
                }
                Stopwatch watch = Stopwatch.StartNew();
                float[] fertility = new float[cells];
                ulong[] polluted = new ulong[checked((int)pollutionWords)];
                Map map = scope.map;
                FertilityGrid fertilityGrid = map.fertilityGrid;
                int sizeX = scope.sizeX;
                for (int z = 0; z < scope.sizeZ; z++)
                {
                    int rowBase = z * sizeX;
                    for (int x = 0; x < sizeX; x++)
                    {
                        IntVec3 cell = new IntVec3(x, 0, z);
                        int index = rowBase + x;
                        fertility[index] = fertilityGrid.FertilityAt(cell);
                        if (cell.IsPolluted(map))
                            polluted[index >> 6] |= 1UL << (index & 63);
                    }
                }
                watch.Stop();
                scope.extractionMilliseconds = watch.ElapsedMilliseconds;
                scope.snapshotBytes = bytes;
                scope.fertility = fertility;
                scope.pollutedBits = polluted;
                Log.Message("[CA][Regional][Jobs][PlantMask] frozen terms "
                    + "extracted for map " + map.uniqueID + " in "
                    + scope.extractionMilliseconds + " ms (" + cells
                    + " cells, " + bytes + " bytes, native "
                    + "fertility/pollution reads, after "
                    + scope.routedCalls + "-call warmup)");
            }
            catch (Exception error)
            {
                scope.Degrade("frozen-term extraction failed ("
                    + error.GetType().Name + ": " + error.Message + ")");
            }
        }

        private static DefTerms GetDefTerms(Scope scope, ThingDef plantDef)
        {
            DefTerms terms;
            if (plantDef != null
                && scope.defTerms.TryGetValue(plantDef, out terms))
                return terms;
            terms = default(DefTerms);
            PlantProperties plant = plantDef?.plant;
            if (plant != null)
            {
                terms.screen = true;
                terms.completelyIgnoreFertility =
                    plant.completelyIgnoreFertility;
                terms.fertilityMin = plant.fertilityMin;
                terms.pollution = plant.pollution;
                terms.temperatureRejected =
                    scope.map.TileInfo.MinTemperature
                        > plant.maxGrowthTemperature
                    || scope.map.TileInfo.MaxTemperature
                        < plant.minGrowthTemperature;
                if (terms.temperatureRejected)
                    scope.temperatureRejectedDefs++;
            }
            if (plantDef != null) scope.defTerms.Add(plantDef, terms);
            return terms;
        }
    }

    // Self-contained installer: own Harmony ID, no [HarmonyPatch]
    // attributes, rollback on failure of the required patch set.
    [StaticConstructorOnStartup]
    internal static class CARegionalPlantMaskInstaller
    {
        static CARegionalPlantMaskInstaller()
        {
            Harmony harmony = null;
            try
            {
                if (!CARegionalPlantMaskPatches.Installed)
                {
                    Log.Message("[CA][Regional][Jobs][PlantMask] disabled "
                        + "by CA_REGIONAL_PLANTMASK=0; no patches applied");
                    return;
                }
                harmony = new Harmony(
                    CARegionalPlantMaskPatches.HarmonyId);
                MethodInfo plantsGenerate = AccessTools.DeclaredMethod(
                    typeof(GenStep_Plants), nameof(GenStep_Plants.Generate),
                    new[] { typeof(Map), typeof(GenStepParams) });
                MethodInfo acceptanceOverload = AccessTools.DeclaredMethod(
                    typeof(PlantUtility),
                    nameof(PlantUtility.CanEverPlantAt),
                    new[]
                    {
                        typeof(ThingDef), typeof(IntVec3), typeof(Map),
                        typeof(Thing).MakeByRefType(), typeof(bool),
                        typeof(bool), typeof(bool)
                    });
                if (plantsGenerate == null || acceptanceOverload == null)
                    throw new MissingMethodException(
                        "GenStep_Plants.Generate or the AcceptanceReport "
                        + "CanEverPlantAt overload not found");
                harmony.Patch(plantsGenerate,
                    prefix: Method(nameof(
                        CARegionalPlantMaskPatches.PlantsPrefix),
                        Priority.First),
                    finalizer: Method(nameof(
                        CARegionalPlantMaskPatches.PlantsFinalizer)));
                // Priority.Last expresses the intent; the explicit
                // HarmonyAfter on the proven ReGrowth owner is the durable
                // ordering contract for both patch kinds.
                harmony.Patch(acceptanceOverload,
                    prefix: Method(nameof(CARegionalPlantMaskPatches
                        .CanEverPlantAtPrefix), Priority.Last,
                        after: new[] { "Helixien.ReGrowthCore" }),
                    postfix: Method(nameof(CARegionalPlantMaskPatches
                        .CanEverPlantAtPostfix), Priority.Last,
                        after: new[] { "Helixien.ReGrowthCore" }));
                Log.Message("[CA][Regional][Jobs][PlantMask] plant "
                    + "frozen-term prediction screen installed in-chain "
                    + "on the AcceptanceReport overload; "
                    + CARegionalPlantMaskPatches.PolicySummary());
            }
            catch (Exception error)
            {
                bool rolledBack = false;
                if (harmony != null)
                {
                    try
                    {
                        harmony.UnpatchAll(
                            CARegionalPlantMaskPatches.HarmonyId);
                        rolledBack = true;
                    }
                    catch (Exception rollbackError)
                    {
                        Log.Error("[CA][Regional][Jobs][PlantMask] "
                            + "rollback failed: " + rollbackError);
                    }
                }
                Log.Error("[CA][Regional][Jobs][PlantMask] install failed; "
                    + (rolledBack
                        ? "all plant-mask patches rolled back"
                        : "partial patch state could not be excluded")
                    + ": " + error);
            }
        }

        private static HarmonyMethod Method(string name, int priority = -1,
            string[] after = null)
        {
            HarmonyMethod method = new HarmonyMethod(AccessTools.Method(
                typeof(CARegionalPlantMaskPatches), name));
            if (priority >= 0) method.priority = priority;
            if (after != null) method.after = after;
            return method;
        }
    }
}
