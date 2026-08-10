using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Regional map-generation jobs, slice 2: ordered-commit cost attribution
    // and the first exact commit accelerator.
    //
    // RS-007 shows the regional minutes live outside the already-parallelized
    // RockChunks noise: Plants 456,868 ms, AncientJunkClusters 236,034 ms
    // despite an effective wall index, RockChunks beyond twenty minutes with
    // stepped private-memory plateaus. Decompilation of the installed
    // 1.6.4871 engine falsifies the obvious per-spawn suspicion - the base
    // ThingOwner.Contains used by TryAdd is an O(1) holdingOwner check - and
    // instead grounds two ordered-commit costs:
    //
    //   1. Despawn bursts. ReGrowth's formation postfix despawns everything
    //      in each boulder rect, and every WipeMode.Vanish scatter spawn
    //      destroys the plants under it. Each despawn pays
    //      spawnedThings.Remove (List.LastIndexOf scan plus RemoveAt shift)
    //      and ListerThings.Remove (linear List.Remove passes over the
    //      per-def list and every matching request-group list, including
    //      AllThings at map scale).
    //   2. Per-cell plant work that the a2-152 bounded ecology index has not
    //      absorbed, whose split between CA index queries and native
    //      remainder is not yet measured.
    //
    // This module therefore ships two things, both regional-generation-scoped
    // and inert elsewhere:
    //
    //   A. Attribution receipts. Timed, counted wraps around the exact
    //      ordered-commit surfaces (GenSpawn.Spawn, WipeExistingThings,
    //      Thing.DeSpawn, ThingOwner<Thing>.TryAdd/Remove,
    //      ListerThings.Remove, FilthMaker.TryMakeFilth, the WildPlantSpawner
    //      hot methods, GenStep_ScatterGroup candidate evaluation, and both
    //      RegionAndRoomUpdater rebuild entry points with a first-trigger
    //      stack receipt), snapshotted at every genstep boundary so the next
    //      live specimen names each genstep's dominant ordered cost exactly.
    //   B. An exact spawned-things index. ThingOwner<Thing>.Remove locates
    //      the removed thing with innerList.LastIndexOf - a backward linear
    //      scan over up to a million entries. Because every mutation of the
    //      map's spawnedThings during generation flows through TryAdd
    //      (append) and Remove (order-preserving RemoveAt), the current index
    //      of any element is derivable from an append-order virtual index
    //      minus the count of earlier removals: a Fenwick tree answers that
    //      in O(log n). The list itself, its order, its contents, and the
    //      RemoveAt shift stay fully native - only the scan is replaced, the
    //      served index is self-verified by reference identity, a sampled
    //      audit compares against the native scan, and any anomaly degrades
    //      the scope permanently to the native path.
    //
    // Nothing here consumes Rand, reorders any list, changes any spawn or
    // despawn, or touches worker threads: slice 2 is measurement plus a
    // provably exact O(log n) replacement for one pure lookup inside the
    // ordered commit. Installed like slice 1 by a self-contained installer
    // with its own Harmony ID; no [HarmonyPatch] attributes, so PatchAll
    // cannot double-apply.

    // Exact current-index tracking for a list that only ever appends at the
    // end and removes via order-preserving RemoveAt. Pure data structure:
    // no Verse statics, no logging, no randomness - the offline harness
    // fuzzes it against a reference List using the real Verse.Thing equality
    // semantics.
    public sealed class CARegionalOrderedThingListIndex
    {
        private readonly Dictionary<Thing, int> virtualIndexOf;
        private int[] removedTree;
        private bool[] removedFlags;
        private int virtualCount;
        private int removedCount;

        public CARegionalOrderedThingListIndex(int capacity)
        {
            if (capacity < 16) capacity = 16;
            int size = NextSize(capacity + 1);
            virtualIndexOf = new Dictionary<Thing, int>(capacity);
            removedTree = new int[size];
            removedFlags = new bool[size];
        }

        public int Count => virtualCount - removedCount;

        // Seeds from the list's current order. Returns false (leaving the
        // tracker unusable) if the list holds reference-duplicates, which
        // the spawned-things contract forbids.
        public bool Rebuild(List<Thing> list)
        {
            virtualIndexOf.Clear();
            virtualCount = 0;
            removedCount = 0;
            if (removedTree.Length < list.Count + 1)
            {
                int size = NextSize(list.Count + 1);
                removedTree = new int[size];
                removedFlags = new bool[size];
            }
            else
            {
                Array.Clear(removedTree, 0, removedTree.Length);
                Array.Clear(removedFlags, 0, removedFlags.Length);
            }
            for (int i = 0; i < list.Count; i++)
            {
                Thing thing = list[i];
                if (thing == null || virtualIndexOf.ContainsKey(thing))
                    return false;
                virtualIndexOf.Add(thing, i);
            }
            virtualCount = list.Count;
            return true;
        }

        public bool NotifyAppended(Thing thing)
        {
            if (thing == null || virtualIndexOf.ContainsKey(thing))
                return false;
            virtualIndexOf.Add(thing, virtualCount);
            virtualCount++;
            if (virtualCount + 1 >= removedTree.Length) Grow();
            return true;
        }

        public bool NotifyRemoved(Thing thing)
        {
            int virtualIndex;
            if (thing == null
                || !virtualIndexOf.TryGetValue(thing, out virtualIndex)
                || removedFlags[virtualIndex])
                return false;
            virtualIndexOf.Remove(thing);
            removedFlags[virtualIndex] = true;
            for (int i = virtualIndex + 1; i < removedTree.Length;
                i += i & -i)
                removedTree[i]++;
            removedCount++;
            return true;
        }

        // A Fenwick update whose node chain exceeded the old array length
        // was silently truncated, which is only sound while every query
        // stays below that length. Growth therefore rebuilds the tree from
        // the raw removal flags so all nodes of the larger index space are
        // complete. Amortized O(n log n) over doublings.
        private void Grow()
        {
            int size = NextSize(virtualCount + 2);
            int[] tree = new int[size];
            bool[] flags = new bool[size];
            Array.Copy(removedFlags, flags, removedFlags.Length);
            for (int v = 0; v < virtualCount; v++)
            {
                if (!flags[v]) continue;
                for (int i = v + 1; i < size; i += i & -i)
                    tree[i]++;
            }
            removedTree = tree;
            removedFlags = flags;
        }

        // Current real index = virtual index minus removals at earlier
        // virtual positions.
        public bool TryCurrentIndexOf(Thing thing, out int index)
        {
            index = -1;
            int virtualIndex;
            if (thing == null
                || !virtualIndexOf.TryGetValue(thing, out virtualIndex))
                return false;
            int removedBefore = 0;
            for (int i = virtualIndex; i > 0; i -= i & -i)
                removedBefore += removedTree[i];
            index = virtualIndex - removedBefore;
            return true;
        }

        // Full exactness audit: count parity, membership parity, and every
        // tracked element found at its computed position by reference.
        public string AuditAgainst(List<Thing> list)
        {
            if (list == null) return "null list";
            if (Count != list.Count)
                return "count mismatch tracker=" + Count + " list="
                    + list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                int computed;
                if (!TryCurrentIndexOf(list[i], out computed))
                    return "untracked element at " + i;
                if (computed != i)
                    return "index mismatch at " + i + " computed=" + computed;
            }
            return null;
        }

        private static int NextSize(int needed)
        {
            int size = 1024;
            while (size < needed) size *= 2;
            return size;
        }
    }

    internal enum CASpawnPathMetric
    {
        GenSpawnSpawn = 0,
        WipeExistingThings,
        ThingDeSpawn,
        OwnerTryAdd,
        OwnerRemove,
        ListerRemoveMapLister,
        ListerRemoveOtherLister,
        FilthTryMake,
        RebuildAllRegions,
        RebuildDirtyRegions,
        PlantsCheckSpawn,
        PlantsSaturated,
        PlantsCanGrow,
        PlantsClusterDistances,
        PlantsSuccession,
        PlantsDistribution,
        ScatterCalculate,
        ScatterFindCell,
        Count
    }

    internal sealed class CARegionalSpawnPathScope
    {
        internal Map map;
        internal bool active;
        internal bool degraded;
        internal string degradeReason;
        internal ThingOwner<Thing> spawnedThings;
        internal List<Thing> spawnedInner;
        internal ListerThings mapLister;
        internal CARegionalOrderedThingListIndex tracker;
        internal bool indexEnabled;
        internal int auditStride;
        internal int genStepDepth;

        internal readonly long[] counts =
            new long[(int)CASpawnPathMetric.Count];
        internal readonly long[] ticks =
            new long[(int)CASpawnPathMetric.Count];
        internal readonly long[] stepCounts =
            new long[(int)CASpawnPathMetric.Count];
        internal readonly long[] stepTicks =
            new long[(int)CASpawnPathMetric.Count];

        internal long indexServed;
        internal long indexAudited;
        internal long indexAuditMismatches;
        internal long indexUnavailable;
        internal int spawnedAtScopeStart;
        internal int rebuildTracesLogged;

        internal void Accumulate(CASpawnPathMetric metric, long elapsed)
        {
            counts[(int)metric]++;
            ticks[(int)metric] += elapsed;
        }

        internal void Degrade(string reason)
        {
            if (degraded) return;
            degraded = true;
            degradeReason = reason;
            Log.Warning("[CA][Regional][Jobs][SpawnPath] spawned-things index "
                + "degraded to native scans: " + reason);
        }
    }

    internal static class CARegionalSpawnPathPatches
    {
        internal static readonly bool Installed;
        internal static bool RemoveTranspilerApplied;
        private static readonly bool IndexEnabledByEnv;
        private static readonly int AuditStride;

        [ThreadStatic]
        private static Stack<CARegionalSpawnPathScope> scopes;

        private static readonly double MillisecondsPerTick =
            1000.0 / Stopwatch.Frequency;

        static CARegionalSpawnPathPatches()
        {
            Installed = !string.Equals(
                Environment.GetEnvironmentVariable("CA_REGIONAL_SPAWNPATH"),
                "0", StringComparison.Ordinal);
            IndexEnabledByEnv = !string.Equals(
                Environment.GetEnvironmentVariable(
                    "CA_REGIONAL_SPAWNPATH_INDEX"), "0",
                StringComparison.Ordinal);
            string stride = Environment.GetEnvironmentVariable(
                "CA_REGIONAL_SPAWNPATH_AUDIT");
            int parsed;
            AuditStride = stride != null && int.TryParse(stride, out parsed)
                && parsed >= 0 && parsed <= 1000000
                    ? parsed
                    : 256;
        }

        internal static string PolicySummary()
        {
            return "spawned-things index "
                + (!IndexEnabledByEnv ? "off (env)"
                    : RemoveTranspilerApplied ? "on"
                    : "off (call-site unavailable)")
                + "; native-scan audit stride "
                + (AuditStride == 0 ? "off" : AuditStride.ToString())
                + "; env CA_REGIONAL_SPAWNPATH=0 disables at load, "
                + "CA_REGIONAL_SPAWNPATH_INDEX=0 keeps receipts but serves "
                + "native scans, CA_REGIONAL_SPAWNPATH_AUDIT=n samples every "
                + "nth served index against the native scan";
        }

        private static CARegionalSpawnPathScope Current
        {
            get
            {
                Stack<CARegionalSpawnPathScope> stack = scopes;
                return stack != null && stack.Count > 0 ? stack.Peek() : null;
            }
        }

        private static CARegionalSpawnPathScope ActiveScope
        {
            get
            {
                CARegionalSpawnPathScope scope = Current;
                return scope != null && scope.active ? scope : null;
            }
        }

        // ---- generation scope (MapGenerator.GenerateContentsIntoMap) ----

        internal static void ContentsPrefix(Map map, out bool __state)
        {
            __state = false;
            if (scopes == null)
                scopes = new Stack<CARegionalSpawnPathScope>();
            CARegionalSpawnPathScope scope = new CARegionalSpawnPathScope();
            scope.map = map;
            // Pushed before anything that can throw so the finalizer's pop
            // stays balanced under nested generation.
            scopes.Push(scope);
            __state = true;
            try
            {
                scope.active = map != null
                    && CARegionalRiverPatchUtility.Active(map);
                if (!scope.active) return;
                scope.spawnedThings = map.spawnedThings
                    as ThingOwner<Thing>;
                scope.spawnedInner =
                    scope.spawnedThings?.InnerListForReading;
                scope.mapLister = map.listerThings;
                if (scope.spawnedThings == null
                    || scope.spawnedInner == null)
                {
                    scope.Degrade("map.spawnedThings is not a "
                        + "ThingOwner<Thing>; index unavailable");
                }
                scope.spawnedAtScopeStart =
                    scope.spawnedInner != null ? scope.spawnedInner.Count : 0;
                scope.auditStride = AuditStride;
                scope.indexEnabled = IndexEnabledByEnv
                    && RemoveTranspilerApplied;
                if (scope.indexEnabled && !scope.degraded)
                {
                    scope.tracker = new CARegionalOrderedThingListIndex(
                        Math.Max(1024, scope.spawnedInner.Count * 2));
                    if (!scope.tracker.Rebuild(scope.spawnedInner))
                    {
                        scope.tracker = null;
                        scope.Degrade("seed rebuild rejected the initial "
                            + "spawned-things list");
                    }
                }
                Log.Message("[CA][Regional][Jobs][SpawnPath] scope opened "
                    + "for " + map.Size.x + "x" + map.Size.z + " map "
                    + map.uniqueID + " with " + scope.spawnedAtScopeStart
                    + " pre-spawned things; " + PolicySummary());
            }
            catch (Exception error)
            {
                scope.active = false;
                Log.Warning("[CA][Regional][Jobs][SpawnPath] scope open "
                    + "failed; receipts disabled for this generation: "
                    + error);
            }
        }

        internal static Exception ContentsFinalizer(Exception __exception,
            bool __state)
        {
            Stack<CARegionalSpawnPathScope> stack = scopes;
            if (!__state || stack == null || stack.Count == 0)
                return __exception;
            CARegionalSpawnPathScope scope = stack.Pop();
            if (stack.Count == 0) scopes = null;
            if (!scope.active) return __exception;
            try
            {
                LogScopeReceipts(scope, __exception);
            }
            catch (Exception receiptError)
            {
                Log.Warning("[CA][Regional][Jobs][SpawnPath] scope receipt "
                    + "failed; generation unaffected: " + receiptError);
            }
            scope.tracker = null;
            scope.spawnedInner = null;
            scope.spawnedThings = null;
            scope.mapLister = null;
            return __exception;
        }

        private static void LogScopeReceipts(CARegionalSpawnPathScope scope,
            Exception exception)
        {
            Map map = scope.map;
            string audit;
            if (scope.tracker != null && !scope.degraded)
            {
                string failure = scope.tracker.AuditAgainst(
                    scope.spawnedInner);
                audit = failure == null
                    ? "final full audit exact over "
                        + scope.spawnedInner.Count + " entries"
                    : "FINAL AUDIT FAILED: " + failure;
            }
            else if (scope.degraded)
            {
                audit = "degraded (" + scope.degradeReason + ")";
            }
            else
            {
                audit = "index disabled";
            }
            Log.Message("[CA][Regional][Jobs][SpawnPath] scope closed for "
                + "map " + map.uniqueID
                + (exception != null
                    ? " after " + exception.GetType().Name : "")
                + ": spawnedThings " + scope.spawnedAtScopeStart + " -> "
                + (scope.spawnedInner != null
                    ? scope.spawnedInner.Count.ToString() : "unknown")
                + "; index served=" + scope.indexServed
                + " sampledAudits=" + scope.indexAudited
                + " auditMismatches=" + scope.indexAuditMismatches
                + " unavailableLookups=" + scope.indexUnavailable
                + "; " + audit);
            Log.Message("[CA][Regional][Jobs][SpawnPath] generation totals "
                + "for map " + map.uniqueID + ": " + MetricsText(
                    scope.counts, scope.ticks, allMetrics: true));
        }

        // ---- per-genstep boundary receipts ----

        internal static void GenStepPrefix(GenStep __instance, Map __0)
        {
            CARegionalSpawnPathScope scope = ActiveScope;
            if (scope == null || !ReferenceEquals(scope.map, __0)) return;
            scope.genStepDepth++;
            if (scope.genStepDepth != 1) return;
            Array.Copy(scope.counts, scope.stepCounts, scope.counts.Length);
            Array.Copy(scope.ticks, scope.stepTicks, scope.ticks.Length);
        }

        internal static void GenStepPostfix(GenStep __instance, Map __0)
        {
            CARegionalSpawnPathScope scope = ActiveScope;
            if (scope == null || !ReferenceEquals(scope.map, __0)) return;
            if (scope.genStepDepth > 0) scope.genStepDepth--;
            if (scope.genStepDepth != 0) return;
            try
            {
                long[] deltaCounts =
                    new long[(int)CASpawnPathMetric.Count];
                long[] deltaTicks = new long[(int)CASpawnPathMetric.Count];
                bool any = false;
                for (int i = 0; i < deltaCounts.Length; i++)
                {
                    deltaCounts[i] = scope.counts[i] - scope.stepCounts[i];
                    deltaTicks[i] = scope.ticks[i] - scope.stepTicks[i];
                    if (deltaCounts[i] != 0) any = true;
                }
                if (!any) return;
                string label = (__instance?.GetType().FullName ?? "unknown")
                    + " [" + (__instance?.def?.defName ?? "no-def") + "]";
                Log.Message("[CA][Regional][Jobs][SpawnPath] " + label
                    + " for map " + scope.map.uniqueID + ": "
                    + MetricsText(deltaCounts, deltaTicks,
                        allMetrics: false)
                    + "; spawnedThings=" + (scope.spawnedInner != null
                        ? scope.spawnedInner.Count.ToString() : "unknown"));
            }
            catch (Exception error)
            {
                Log.Warning("[CA][Regional][Jobs][SpawnPath] genstep receipt "
                    + "failed; generation unaffected: " + error);
            }
        }

        private static string MetricsText(long[] counts, long[] ticks,
            bool allMetrics)
        {
            var text = new System.Text.StringBuilder(384);
            bool first = true;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] == 0 && !allMetrics) continue;
                if (counts[i] == 0 && ticks[i] == 0 && !allMetrics) continue;
                if (!first) text.Append("; ");
                first = false;
                text.Append(MetricLabel((CASpawnPathMetric)i))
                    .Append('=').Append(counts[i]).Append('/')
                    .Append((long)(ticks[i] * MillisecondsPerTick))
                    .Append("ms");
            }
            if (first) text.Append("no tracked activity");
            return text.ToString();
        }

        private static string MetricLabel(CASpawnPathMetric metric)
        {
            switch (metric)
            {
                case CASpawnPathMetric.GenSpawnSpawn: return "spawn";
                case CASpawnPathMetric.WipeExistingThings: return "wipe";
                case CASpawnPathMetric.ThingDeSpawn: return "despawn";
                case CASpawnPathMetric.OwnerTryAdd: return "ownerTryAdd";
                case CASpawnPathMetric.OwnerRemove: return "ownerRemove";
                case CASpawnPathMetric.ListerRemoveMapLister:
                    return "mapListerRemove";
                case CASpawnPathMetric.ListerRemoveOtherLister:
                    return "otherListerRemove";
                case CASpawnPathMetric.FilthTryMake: return "filth";
                case CASpawnPathMetric.RebuildAllRegions:
                    return "regionRebuildAll";
                case CASpawnPathMetric.RebuildDirtyRegions:
                    return "regionRebuildDirty";
                case CASpawnPathMetric.PlantsCheckSpawn: return "plantCheck";
                case CASpawnPathMetric.PlantsSaturated:
                    return "plantSaturated";
                case CASpawnPathMetric.PlantsCanGrow: return "plantCanGrow";
                case CASpawnPathMetric.PlantsClusterDistances:
                    return "plantClusters";
                case CASpawnPathMetric.PlantsSuccession:
                    return "plantSuccession";
                case CASpawnPathMetric.PlantsDistribution:
                    return "plantDistribution";
                case CASpawnPathMetric.ScatterCalculate:
                    return "scatterCalc";
                case CASpawnPathMetric.ScatterFindCell:
                    return "scatterFind";
                default: return metric.ToString();
            }
        }

        // ---- timed wrap plumbing ----
        // Prefixes stamp Stopwatch.GetTimestamp into __state (0 when the
        // scope is inactive; the monotonic timestamp is never 0 on a running
        // system). Postfixes accumulate inclusive time. Nested metrics are
        // reported as inclusive and interpreted arithmetically in the
        // report.

        private static long Stamp()
        {
            return ActiveScope != null ? Stopwatch.GetTimestamp() : 0;
        }

        private static void Record(CASpawnPathMetric metric, long state)
        {
            if (state == 0) return;
            CARegionalSpawnPathScope scope = ActiveScope;
            if (scope == null) return;
            scope.Accumulate(metric, Stopwatch.GetTimestamp() - state);
        }

        internal static void SpawnPrefix(out long __state)
        { __state = Stamp(); }
        internal static void SpawnPostfix(long __state)
        { Record(CASpawnPathMetric.GenSpawnSpawn, __state); }

        internal static void WipePrefix(out long __state)
        { __state = Stamp(); }
        internal static void WipePostfix(long __state)
        { Record(CASpawnPathMetric.WipeExistingThings, __state); }

        internal static void DeSpawnPrefix(out long __state)
        { __state = Stamp(); }
        internal static void DeSpawnPostfix(long __state)
        { Record(CASpawnPathMetric.ThingDeSpawn, __state); }

        internal static void FilthPrefix(out long __state)
        { __state = Stamp(); }
        internal static void FilthPostfix(long __state)
        { Record(CASpawnPathMetric.FilthTryMake, __state); }

        internal static void PlantsCheckPrefix(out long __state)
        { __state = Stamp(); }
        internal static void PlantsCheckPostfix(long __state)
        { Record(CASpawnPathMetric.PlantsCheckSpawn, __state); }

        internal static void PlantsSaturatedPrefix(out long __state)
        { __state = Stamp(); }
        internal static void PlantsSaturatedPostfix(long __state)
        { Record(CASpawnPathMetric.PlantsSaturated, __state); }

        internal static void PlantsCanGrowPrefix(out long __state)
        { __state = Stamp(); }
        internal static void PlantsCanGrowPostfix(long __state)
        { Record(CASpawnPathMetric.PlantsCanGrow, __state); }

        internal static void PlantsClustersPrefix(out long __state)
        { __state = Stamp(); }
        internal static void PlantsClustersPostfix(long __state)
        { Record(CASpawnPathMetric.PlantsClusterDistances, __state); }

        internal static void PlantsSuccessionPrefix(out long __state)
        { __state = Stamp(); }
        internal static void PlantsSuccessionPostfix(long __state)
        { Record(CASpawnPathMetric.PlantsSuccession, __state); }

        internal static void PlantsDistributionPrefix(out long __state)
        { __state = Stamp(); }
        internal static void PlantsDistributionPostfix(long __state)
        { Record(CASpawnPathMetric.PlantsDistribution, __state); }

        internal static void ScatterCalcPrefix(out long __state)
        { __state = Stamp(); }
        internal static void ScatterCalcPostfix(long __state)
        { Record(CASpawnPathMetric.ScatterCalculate, __state); }

        internal static void ScatterFindPrefix(out long __state)
        { __state = Stamp(); }
        internal static void ScatterFindPostfix(long __state)
        { Record(CASpawnPathMetric.ScatterFindCell, __state); }

        // Region rebuilds also capture who asked, once per entry point per
        // scope, because RS-007 still pays a 12,613 ms first rebuild inside
        // ScenParts that no receipt currently attributes.
        internal static void RebuildAllPrefix(out long __state)
        {
            __state = Stamp();
            TraceRebuild("RebuildAllRegionsAndRooms");
        }
        internal static void RebuildAllPostfix(long __state)
        { Record(CASpawnPathMetric.RebuildAllRegions, __state); }

        internal static void RebuildDirtyPrefix(out long __state)
        {
            __state = Stamp();
            TraceRebuild("TryRebuildDirtyRegionsAndRooms");
        }
        internal static void RebuildDirtyPostfix(long __state)
        { Record(CASpawnPathMetric.RebuildDirtyRegions, __state); }

        private static void TraceRebuild(string entryPoint)
        {
            CARegionalSpawnPathScope scope = ActiveScope;
            if (scope == null || scope.rebuildTracesLogged >= 2) return;
            scope.rebuildTracesLogged++;
            try
            {
                Log.Message("[CA][Regional][Jobs][SpawnPath] region rebuild "
                    + "entry " + entryPoint + " during regional generation "
                    + "of map " + scope.map.uniqueID + "; caller stack:\n"
                    + Environment.StackTrace);
            }
            catch (Exception)
            {
                // The trace is evidence only.
            }
        }

        // ---- ThingOwner<Thing> wraps: timing plus tracker maintenance ----

        internal static void OwnerTryAddPrefix(out long __state)
        { __state = Stamp(); }

        internal static void OwnerTryAddPostfix(ThingOwner<Thing> __instance,
            Thing item, bool __result, long __state)
        {
            Record(CASpawnPathMetric.OwnerTryAdd, __state);
            if (!__result) return;
            CARegionalSpawnPathScope scope = ActiveScope;
            if (scope == null || scope.tracker == null || scope.degraded)
                return;
            if (!ReferenceEquals(__instance, scope.spawnedThings)) return;
            // A merge-absorbed item returns true without being appended; the
            // appended case is discriminated by holdingOwner having been
            // bound to this owner.
            if (!ReferenceEquals(item?.holdingOwner, __instance)) return;
            try
            {
                if (!scope.tracker.NotifyAppended(item))
                    DegradeContained(scope,
                        "append notification rejected "
                        + (item?.ToStringSafe() ?? "null"));
            }
            catch (Exception)
            {
                DegradeContained(scope, "append tracking failed");
            }
        }

        internal static void OwnerRemovePrefix(out long __state)
        { __state = Stamp(); }

        internal static void OwnerRemovePostfix(ThingOwner<Thing> __instance,
            Thing item, bool __result, long __state)
        {
            Record(CASpawnPathMetric.OwnerRemove, __state);
            if (!__result) return;
            CARegionalSpawnPathScope scope = ActiveScope;
            if (scope == null || scope.tracker == null || scope.degraded)
                return;
            if (!ReferenceEquals(__instance, scope.spawnedThings)) return;
            try
            {
                if (!scope.tracker.NotifyRemoved(item))
                    DegradeContained(scope,
                        "removal notification missed "
                        + (item?.ToStringSafe() ?? "null"));
            }
            catch (Exception)
            {
                DegradeContained(scope, "removal tracking failed");
            }
        }

        // Replaces the single `callvirt List<Thing>.LastIndexOf(Thing)` at
        // IL_0027 of ThingOwner<Thing>.Remove. Buffered two-pass: the IL is
        // returned untouched unless exactly one site matches.
        internal static IEnumerable<CodeInstruction> OwnerRemoveTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            RemoveTranspilerApplied = false;
            MethodInfo nativeLastIndexOf = AccessTools.Method(
                typeof(List<Thing>), nameof(List<Thing>.LastIndexOf),
                new[] { typeof(Thing) });
            MethodInfo routed = AccessTools.Method(
                typeof(CARegionalSpawnPathPatches),
                nameof(RoutedLastIndexOf));
            List<CodeInstruction> buffer =
                new List<CodeInstruction>(instructions);
            int matches = 0;
            for (int i = 0; i < buffer.Count; i++)
                if (buffer[i].Calls(nativeLastIndexOf)) matches++;
            if (matches != 1)
            {
                Log.Warning("[CA][Regional][Jobs][SpawnPath] expected "
                    + "exactly one List<Thing>.LastIndexOf site in "
                    + "ThingOwner<Thing>.Remove, found " + matches
                    + "; leaving native IL untouched (receipts remain)");
                return buffer;
            }
            for (int i = 0; i < buffer.Count; i++)
            {
                if (!buffer[i].Calls(nativeLastIndexOf)) continue;
                // Same stack shape: [list, item] -> int32.
                buffer[i].opcode = OpCodes.Call;
                buffer[i].operand = routed;
                RemoveTranspilerApplied = true;
                break;
            }
            return buffer;
        }

        // The routed lookup. Exactness: the spawned-things list holds no
        // two entries that compare equal (TryAdd rejects an item whose
        // holdingOwner is already bound, and Thing equality is
        // thingIDNumber-based with live IDs unique), so the reference
        // occurrence's index is the LastIndexOf answer; the serve
        // self-verifies by reference identity at the computed position and
        // is additionally sample-audited against the native scan. Any
        // anomaly permanently degrades the scope to native scans.
        internal static int RoutedLastIndexOf(List<Thing> list, Thing item)
        {
            CARegionalSpawnPathScope scope = ActiveScope;
            if (scope == null || scope.degraded || scope.tracker == null
                || !ReferenceEquals(list, scope.spawnedInner))
                return list.LastIndexOf(item);
            try
            {
                int index;
                if (!scope.tracker.TryCurrentIndexOf(item, out index)
                    || (uint)index >= (uint)list.Count
                    || !ReferenceEquals(list[index], item))
                {
                    scope.indexUnavailable++;
                    DegradeContained(scope, "computed index invalid for "
                        + (item?.ToStringSafe() ?? "null"));
                }
                else
                {
                    scope.indexServed++;
                    if (scope.auditStride > 0
                        && scope.indexServed % scope.auditStride == 0)
                    {
                        int native = list.LastIndexOf(item);
                        scope.indexAudited++;
                        if (native != index)
                        {
                            scope.indexAuditMismatches++;
                            DegradeContained(scope,
                                "sampled audit mismatch computed=" + index
                                + " native=" + native);
                            return native;
                        }
                    }
                    return index;
                }
            }
            catch (Exception)
            {
                scope.indexUnavailable++;
                DegradeContained(scope, "indexed lookup failed");
            }
            return list.LastIndexOf(item);
        }

        private static void DegradeContained(CARegionalSpawnPathScope scope,
            string reason)
        {
            try
            {
                scope.Degrade(reason);
            }
            catch (Exception)
            {
                // Even logging must not let an advisory index failure escape
                // after the native owner has already mutated.
                scope.degraded = true;
                scope.degradeReason = reason;
            }
        }

        // ---- ListerThings.Remove timing, split map lister vs others ----

        internal static void ListerRemovePrefix(out long __state)
        { __state = Stamp(); }

        internal static void ListerRemovePostfix(ListerThings __instance,
            long __state)
        {
            if (__state == 0) return;
            CARegionalSpawnPathScope scope = ActiveScope;
            if (scope == null) return;
            scope.Accumulate(
                ReferenceEquals(__instance, scope.mapLister)
                    ? CASpawnPathMetric.ListerRemoveMapLister
                    : CASpawnPathMetric.ListerRemoveOtherLister,
                Stopwatch.GetTimestamp() - __state);
        }
    }

    // Self-contained installer, mirroring the slice-1 posture: own Harmony
    // ID, no [HarmonyPatch] attributes, rollback on required-patch failure,
    // optional wraps degrade individually.
    [StaticConstructorOnStartup]
    internal static class CARegionalSpawnPathInstaller
    {
        private const string HarmonyId =
            "ellyj3rain.colonistawareness.regionaljobs.spawnpath";

        static CARegionalSpawnPathInstaller()
        {
            Harmony harmony = null;
            try
            {
                if (!CARegionalSpawnPathPatches.Installed)
                {
                    Log.Message("[CA][Regional][Jobs][SpawnPath] disabled "
                        + "by CA_REGIONAL_SPAWNPATH=0; no patches applied");
                    return;
                }
                harmony = new Harmony(HarmonyId);
                InstallRequired(harmony);
                string optional = InstallOptional(harmony);
                Log.Message("[CA][Regional][Jobs][SpawnPath] ordered-commit "
                    + "attribution and spawned-things index installed; "
                    + CARegionalSpawnPathPatches.PolicySummary()
                    + "; optional wraps: " + optional);
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
                        Log.Error("[CA][Regional][Jobs][SpawnPath] rollback "
                            + "failed: " + rollbackError);
                    }
                }
                Log.Error("[CA][Regional][Jobs][SpawnPath] install failed; "
                    + (rolledBack
                        ? "all spawn-path patches rolled back"
                        : "partial patch state could not be excluded")
                    + ": " + error);
            }
        }

        private static void InstallRequired(Harmony harmony)
        {
            MethodInfo contents = AccessTools.Method(typeof(MapGenerator),
                nameof(MapGenerator.GenerateContentsIntoMap));
            MethodInfo ownerTryAdd = AccessTools.Method(
                typeof(ThingOwner<Thing>), nameof(ThingOwner<Thing>.TryAdd),
                new[] { typeof(Thing), typeof(bool) });
            MethodInfo ownerRemove = AccessTools.Method(
                typeof(ThingOwner<Thing>), nameof(ThingOwner<Thing>.Remove),
                new[] { typeof(Thing) });
            if (contents == null || ownerTryAdd == null
                || ownerRemove == null)
                throw new MissingMethodException(
                    "core spawn-path targets not found");
            harmony.Patch(contents,
                prefix: Method(nameof(
                    CARegionalSpawnPathPatches.ContentsPrefix),
                    Priority.First),
                finalizer: Method(nameof(
                    CARegionalSpawnPathPatches.ContentsFinalizer)));
            harmony.Patch(ownerTryAdd,
                prefix: Method(nameof(
                    CARegionalSpawnPathPatches.OwnerTryAddPrefix)),
                postfix: Method(nameof(
                    CARegionalSpawnPathPatches.OwnerTryAddPostfix),
                    Priority.Last));
            harmony.Patch(ownerRemove,
                prefix: Method(nameof(
                    CARegionalSpawnPathPatches.OwnerRemovePrefix)),
                postfix: Method(nameof(
                    CARegionalSpawnPathPatches.OwnerRemovePostfix),
                    Priority.Last),
                transpiler: Method(nameof(
                    CARegionalSpawnPathPatches.OwnerRemoveTranspiler)));
            if (!CARegionalSpawnPathPatches.RemoveTranspilerApplied)
                Log.Warning("[CA][Regional][Jobs][SpawnPath] spawned-things "
                    + "index call-site replacement not applied; receipts "
                    + "remain and Remove keeps the native scan");
        }

        private static string InstallOptional(Harmony harmony)
        {
            var installed = new List<string>();
            var missing = new List<string>();

            TryWrap(harmony, installed, missing, "spawn",
                AccessTools.Method(typeof(GenSpawn), nameof(GenSpawn.Spawn),
                    new[]
                    {
                        typeof(Thing), typeof(IntVec3), typeof(Map),
                        typeof(Rot4), typeof(WipeMode), typeof(bool),
                        typeof(bool)
                    }),
                nameof(CARegionalSpawnPathPatches.SpawnPrefix),
                nameof(CARegionalSpawnPathPatches.SpawnPostfix));
            TryWrap(harmony, installed, missing, "wipe",
                AccessTools.Method(typeof(GenSpawn),
                    nameof(GenSpawn.WipeExistingThings)),
                nameof(CARegionalSpawnPathPatches.WipePrefix),
                nameof(CARegionalSpawnPathPatches.WipePostfix));
            TryWrap(harmony, installed, missing, "despawn",
                AccessTools.Method(typeof(Thing), nameof(Thing.DeSpawn)),
                nameof(CARegionalSpawnPathPatches.DeSpawnPrefix),
                nameof(CARegionalSpawnPathPatches.DeSpawnPostfix));
            TryWrap(harmony, installed, missing, "listerRemove",
                AccessTools.Method(typeof(ListerThings),
                    nameof(ListerThings.Remove)),
                nameof(CARegionalSpawnPathPatches.ListerRemovePrefix),
                nameof(CARegionalSpawnPathPatches.ListerRemovePostfix));
            TryWrap(harmony, installed, missing, "filth",
                AccessTools.Method(typeof(FilthMaker),
                    nameof(FilthMaker.TryMakeFilth),
                    new[]
                    {
                        typeof(IntVec3), typeof(Map), typeof(ThingDef),
                        typeof(int), typeof(FilthSourceFlags), typeof(bool)
                    }),
                nameof(CARegionalSpawnPathPatches.FilthPrefix),
                nameof(CARegionalSpawnPathPatches.FilthPostfix));
            TryWrap(harmony, installed, missing, "rebuildAll",
                AccessTools.Method(typeof(RegionAndRoomUpdater),
                    nameof(RegionAndRoomUpdater.RebuildAllRegionsAndRooms)),
                nameof(CARegionalSpawnPathPatches.RebuildAllPrefix),
                nameof(CARegionalSpawnPathPatches.RebuildAllPostfix));
            TryWrap(harmony, installed, missing, "rebuildDirty",
                AccessTools.Method(typeof(RegionAndRoomUpdater),
                    nameof(RegionAndRoomUpdater
                        .TryRebuildDirtyRegionsAndRooms)),
                nameof(CARegionalSpawnPathPatches.RebuildDirtyPrefix),
                nameof(CARegionalSpawnPathPatches.RebuildDirtyPostfix));
            TryWrap(harmony, installed, missing, "plantCheck",
                AccessTools.Method(typeof(WildPlantSpawner),
                    nameof(WildPlantSpawner.CheckSpawnWildPlantAt)),
                nameof(CARegionalSpawnPathPatches.PlantsCheckPrefix),
                nameof(CARegionalSpawnPathPatches.PlantsCheckPostfix));
            TryWrap(harmony, installed, missing, "plantSaturated",
                AccessTools.Method(typeof(WildPlantSpawner), "SaturatedAt"),
                nameof(CARegionalSpawnPathPatches.PlantsSaturatedPrefix),
                nameof(CARegionalSpawnPathPatches.PlantsSaturatedPostfix));
            TryWrap(harmony, installed, missing, "plantCanGrow",
                AccessTools.Method(typeof(WildPlantSpawner),
                    "CalculatePlantsWhichCanGrowAt"),
                nameof(CARegionalSpawnPathPatches.PlantsCanGrowPrefix),
                nameof(CARegionalSpawnPathPatches.PlantsCanGrowPostfix));
            TryWrap(harmony, installed, missing, "plantClusters",
                AccessTools.Method(typeof(WildPlantSpawner),
                    "CalculateDistancesToNearbyClusters"),
                nameof(CARegionalSpawnPathPatches.PlantsClustersPrefix),
                nameof(CARegionalSpawnPathPatches.PlantsClustersPostfix));
            TryWrap(harmony, installed, missing, "plantSuccession",
                AccessTools.Method(typeof(WildPlantSpawner),
                    "EnoughLowerOrderPlantsNearby"),
                nameof(CARegionalSpawnPathPatches.PlantsSuccessionPrefix),
                nameof(CARegionalSpawnPathPatches.PlantsSuccessionPostfix));
            TryWrap(harmony, installed, missing, "plantDistribution",
                AccessTools.Method(typeof(WildPlantSpawner),
                    "LocalPlantProportionsWeightFactor"),
                nameof(CARegionalSpawnPathPatches.PlantsDistributionPrefix),
                nameof(CARegionalSpawnPathPatches
                    .PlantsDistributionPostfix));
            TryWrap(harmony, installed, missing, "scatterCalc",
                AccessTools.Method(typeof(GenStep_ScatterGroup),
                    "CalculateScatterInformation"),
                nameof(CARegionalSpawnPathPatches.ScatterCalcPrefix),
                nameof(CARegionalSpawnPathPatches.ScatterCalcPostfix));
            TryWrap(harmony, installed, missing, "scatterFind",
                AccessTools.DeclaredMethod(typeof(GenStep_ScatterGroup),
                    "TryFindScatterCell"),
                nameof(CARegionalSpawnPathPatches.ScatterFindPrefix),
                nameof(CARegionalSpawnPathPatches.ScatterFindPostfix));

            InstallGenStepBoundaries(harmony, installed, missing);

            return installed.Count + " installed ["
                + string.Join(",", installed) + "]"
                + (missing.Count == 0 ? ""
                    : "; missing [" + string.Join(",", missing) + "]");
        }

        private static void InstallGenStepBoundaries(Harmony harmony,
            List<string> installed, List<string> missing)
        {
            int patched = 0;
            var seen = new HashSet<MethodInfo>();
            foreach (Type type in GenStepTypes())
            {
                // Several concrete scatter steps inherit their effective
                // Generate implementation from an abstract base. Resolve the
                // inherited method and patch each implementation only once.
                MethodInfo generate = AccessTools.Method(type,
                    nameof(GenStep.Generate),
                    new[] { typeof(Map), typeof(GenStepParams) });
                if (generate == null || !seen.Add(generate)) continue;
                try
                {
                    harmony.Patch(generate,
                        prefix: Method(nameof(
                            CARegionalSpawnPathPatches.GenStepPrefix),
                            Priority.First),
                        postfix: Method(nameof(
                            CARegionalSpawnPathPatches.GenStepPostfix),
                            Priority.Last));
                    patched++;
                }
                catch (Exception)
                {
                    // An unpatched genstep only loses its boundary receipt.
                }
            }
            if (patched > 0)
                installed.Add("genstepBoundaries:" + patched);
            else
                missing.Add("genstepBoundaries");
        }

        private static IEnumerable<Type> GenStepTypes()
        {
            var types = new List<Type>();
            foreach (Assembly assembly in
                AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] assemblyTypes;
                try
                {
                    assemblyTypes = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException loadError)
                {
                    var partial = new List<Type>();
                    foreach (Type type in loadError.Types)
                        if (type != null) partial.Add(type);
                    assemblyTypes = partial.ToArray();
                }
                catch (Exception)
                {
                    continue;
                }
                foreach (Type type in assemblyTypes)
                {
                    if (type == null || type.IsAbstract) continue;
                    if (typeof(GenStep).IsAssignableFrom(type))
                        types.Add(type);
                }
            }
            return types;
        }

        private static void TryWrap(Harmony harmony, List<string> installed,
            List<string> missing, string label, MethodInfo target,
            string prefixName, string postfixName)
        {
            if (target == null)
            {
                missing.Add(label);
                return;
            }
            try
            {
                harmony.Patch(target,
                    prefix: Method(prefixName),
                    postfix: Method(postfixName, Priority.Last));
                installed.Add(label);
            }
            catch (Exception error)
            {
                missing.Add(label);
                Log.Warning("[CA][Regional][Jobs][SpawnPath] optional wrap "
                    + label + " not installed: " + error.GetType().Name
                    + ": " + error.Message);
            }
        }

        private static HarmonyMethod Method(string name, int priority = -1)
        {
            HarmonyMethod method = new HarmonyMethod(AccessTools.Method(
                typeof(CARegionalSpawnPathPatches), name));
            if (priority >= 0) method.priority = priority;
            return method;
        }
    }
}
