using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Vanilla mixed-biome ecology repeatedly traverses the region graph for
    // local density, succession, and equal-distribution checks. Its cluster
    // pass also revisits every nearby plant for each viable placement. Regional
    // maps retain the native plant definitions and selection formulas, but feed
    // those local checks from bounded eight-cell spatial summaries.
    internal sealed class CARegionalPlantSpatialIndex
    {
        private const int BlockSize = 8;

        private sealed class ClusterAggregate
        {
            internal int count;
            internal long sumX;
            internal long sumZ;
        }

        private struct WeightedDistance
        {
            internal float distanceSquared;
            internal float weight;
        }

        private readonly Map map;
        private readonly WildPlantSpawner spawner;
        private readonly int blockColumns;
        private readonly int blockRows;
        private readonly float[] desiredPlantsByBlock;
        private readonly int[] plantCountsByBlock;
        private readonly Dictionary<ThingDef, int[]> plantCountsByDef =
            new Dictionary<ThingDef, int[]>();
        private readonly Dictionary<int, Dictionary<ThingDef,
            ClusterAggregate>> clusterAggregatesByBlock =
                new Dictionary<int, Dictionary<ThingDef,
                    ClusterAggregate>>();
        private readonly HashSet<int> indexedPlantIds = new HashSet<int>();
        private readonly Dictionary<ThingDef, List<WeightedDistance>>
            distanceScratch = new Dictionary<ThingDef,
                List<WeightedDistance>>();
        private readonly List<ThingDef> lowerOrderScratch =
            new List<ThingDef>();
        private readonly float densityFactor;
        private bool mixedBiome;
        private int totalPlants;

        internal CARegionalProjectionMapComponent Projection { get; }
        internal float WholeMapDesiredPlants { get; private set; }

        internal long clusterQueryCount;
        internal long radialCellsBypassed;
        internal long clusterAggregatesInspected;
        internal long saturationQueries;
        internal long successionQueries;
        internal long distributionQueries;
        internal long localBlocksInspected;
        internal long initializationMilliseconds;

        internal CARegionalPlantSpatialIndex(Map map,
            WildPlantSpawner spawner)
        {
            this.map = map;
            this.spawner = spawner;
            blockColumns = Mathf.CeilToInt(map.Size.x / (float)BlockSize);
            blockRows = Mathf.CeilToInt(map.Size.z / (float)BlockSize);
            desiredPlantsByBlock = new float[blockColumns * blockRows];
            plantCountsByBlock = new int[blockColumns * blockRows];
            densityFactor = spawner.CurrentPlantDensityFactor;
            Projection = map.GetComponent<
                CARegionalProjectionMapComponent>();
        }

        internal void Initialize()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            mixedBiome = MapGenUtility.IsMixedBiome(map);
            for (int z = 0; z < map.Size.z; z++)
            {
                for (int x = 0; x < map.Size.x; x++)
                {
                    var cell = new IntVec3(x, 0, z);
                    float desired = spawner.GetDesiredPlantsCountAt(cell,
                        densityFactor);
                    desiredPlantsByBlock[BlockKey(x / BlockSize,
                        z / BlockSize)] += desired;
                    WholeMapDesiredPlants += desired;
                }
            }
            List<Thing> existing = map.listerThings.ThingsInGroup(
                ThingRequestGroup.NonStumpPlant);
            for (int i = 0; i < existing.Count; i++)
                Add(existing[i] as Plant);
            timer.Stop();
            initializationMilliseconds = timer.ElapsedMilliseconds;
        }

        internal bool Owns(WildPlantSpawner candidate)
        {
            return ReferenceEquals(candidate, spawner);
        }

        internal bool Owns(Map candidate)
        {
            return ReferenceEquals(candidate, map);
        }

        internal bool SupportsDensity(float candidate)
        {
            return Mathf.Approximately(candidate, densityFactor);
        }

        internal void AddAt(IntVec3 cell)
        {
            Add(cell.GetPlant(map));
        }

        private void Add(Plant plant)
        {
            if (plant == null || !plant.Spawned || plant.Map != map
                || !indexedPlantIds.Add(plant.thingIDNumber))
                return;
            int key = BlockKey(plant.Position.x / BlockSize,
                plant.Position.z / BlockSize);
            totalPlants++;
            plantCountsByBlock[key]++;
            int[] byDef;
            if (!plantCountsByDef.TryGetValue(plant.def, out byDef))
            {
                byDef = new int[plantCountsByBlock.Length];
                plantCountsByDef.Add(plant.def, byDef);
            }
            byDef[key]++;

            if (plant.def?.plant?.GrowsInClusters != true) return;
            Dictionary<ThingDef, ClusterAggregate> block;
            if (!clusterAggregatesByBlock.TryGetValue(key, out block))
            {
                block = new Dictionary<ThingDef, ClusterAggregate>();
                clusterAggregatesByBlock.Add(key, block);
            }
            ClusterAggregate aggregate;
            if (!block.TryGetValue(plant.def, out aggregate))
            {
                aggregate = new ClusterAggregate();
                block.Add(plant.def, aggregate);
            }
            aggregate.count++;
            aggregate.sumX += plant.Position.x;
            aggregate.sumZ += plant.Position.z;
        }

        internal void Calculate(IntVec3 cell,
            Dictionary<ThingDef, float> output)
        {
            output.Clear();
            foreach (List<WeightedDistance> values in distanceScratch.Values)
                values.Clear();

            float radius = map.BiomeAt(cell)
                .MaxWildAndCavePlantsClusterRadius * 2f;
            int radialCount = GenRadial.NumCellsInRadius(radius);
            radialCellsBypassed += radialCount;
            clusterQueryCount++;
            BlockBounds(cell, radius, out int minBlockX,
                out int maxBlockX, out int minBlockZ, out int maxBlockZ);

            for (int blockZ = minBlockZ; blockZ <= maxBlockZ; blockZ++)
            {
                for (int blockX = minBlockX; blockX <= maxBlockX; blockX++)
                {
                    Dictionary<ThingDef, ClusterAggregate> block;
                    if (!clusterAggregatesByBlock.TryGetValue(
                            BlockKey(blockX, blockZ), out block))
                        continue;
                    float blockWeight = BlockWeight(blockX, blockZ,
                        cell, radius);
                    if (blockWeight <= 0f) continue;
                    foreach (KeyValuePair<ThingDef, ClusterAggregate> pair
                        in block)
                    {
                        clusterAggregatesInspected++;
                        ClusterAggregate aggregate = pair.Value;
                        if (aggregate == null || aggregate.count <= 0)
                            continue;
                        float centerX = aggregate.sumX
                            / (float)aggregate.count;
                        float centerZ = aggregate.sumZ
                            / (float)aggregate.count;
                        float dx = centerX - cell.x;
                        float dz = centerZ - cell.z;
                        List<WeightedDistance> distances;
                        if (!distanceScratch.TryGetValue(pair.Key,
                                out distances))
                        {
                            distances = new List<WeightedDistance>();
                            distanceScratch.Add(pair.Key, distances);
                        }
                        distances.Add(new WeightedDistance
                        {
                            distanceSquared = dx * dx + dz * dz,
                            weight = aggregate.count * blockWeight
                        });
                    }
                }
            }

            foreach (KeyValuePair<ThingDef, List<WeightedDistance>> pair
                in distanceScratch)
            {
                if (pair.Value.Count == 0) continue;
                pair.Value.Sort((left, right) =>
                    left.distanceSquared.CompareTo(right.distanceSquared));
                float totalWeight = 0f;
                for (int i = 0; i < pair.Value.Count; i++)
                    totalWeight += pair.Value[i].weight;
                float halfway = totalWeight * 0.5f;
                float accumulated = 0f;
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    accumulated += pair.Value[i].weight;
                    if (accumulated <= halfway) continue;
                    output[pair.Key] = pair.Value[i].distanceSquared;
                    break;
                }
            }
        }

        internal bool SaturatedAt(IntVec3 cell,
            float wholeMapNumDesiredPlants)
        {
            saturationQueries++;
            int radialCount = GenRadial.NumCellsInRadius(20f);
            if (wholeMapNumDesiredPlants
                    * (radialCount / (float)map.Area) <= 4f
                || (!mixedBiome && !map.BiomeAt(cell)
                    .wildPlantsCareAboutLocalFertility))
                return totalPlants >= wholeMapNumDesiredPlants;
            QueryLocal(cell, 20f, null, out float desired,
                out float actual, out var _);
            return actual >= desired;
        }

        internal float LocalDistributionWeight(IntVec3 cell,
            float commonalityPct, float radius, ThingDef plantDef)
        {
            distributionQueries++;
            QueryLocal(cell, radius, plantDef, out float desired,
                out float total, out float matching);
            if (desired * commonalityPct < 2f
                || total <= desired * 0.5f) return 1f;
            float relative = matching / total / commonalityPct;
            return Mathf.Lerp(7f, 1f, relative);
        }

        internal bool EnoughLowerOrderPlantsNearby(IntVec3 cell,
            float radius, ThingDef plantDef)
        {
            successionQueries++;
            BiomeDef biome = map.BiomeAt(cell);
            lowerOrderScratch.Clear();
            float commonality = 0f;
            for (int i = 0; i < biome.wildPlants.Count; i++)
            {
                ThingDef candidate = biome.wildPlants[i].plant;
                if (candidate?.plant == null
                    || candidate.plant.wildOrder
                        >= plantDef.plant.wildOrder) continue;
                commonality += spawner.GetCommonalityPctOfPlant(candidate);
                lowerOrderScratch.Add(candidate);
            }
            QueryLocal(cell, radius, null, out float desired,
                out var _, out var _);
            float required = desired * commonality;
            if (required < 4f) return true;
            float actual = CountLocal(cell, radius, lowerOrderScratch);
            return actual / required >= 0.57f;
        }

        private void QueryLocal(IntVec3 cell, float radius,
            ThingDef matchingDef, out float desired, out float total,
            out float matching)
        {
            desired = 0f;
            total = 0f;
            matching = 0f;
            int[] matchingByBlock = null;
            if (matchingDef != null)
                plantCountsByDef.TryGetValue(matchingDef,
                    out matchingByBlock);
            BlockBounds(cell, radius, out int minBlockX,
                out int maxBlockX, out int minBlockZ, out int maxBlockZ);
            for (int blockZ = minBlockZ; blockZ <= maxBlockZ; blockZ++)
            {
                for (int blockX = minBlockX; blockX <= maxBlockX; blockX++)
                {
                    float weight = BlockWeight(blockX, blockZ, cell, radius);
                    if (weight <= 0f) continue;
                    localBlocksInspected++;
                    int key = BlockKey(blockX, blockZ);
                    desired += desiredPlantsByBlock[key] * weight;
                    total += plantCountsByBlock[key] * weight;
                    if (matchingByBlock != null)
                        matching += matchingByBlock[key] * weight;
                }
            }
        }

        private float CountLocal(IntVec3 cell, float radius,
            List<ThingDef> defs)
        {
            if (defs == null || defs.Count == 0) return 0f;
            BlockBounds(cell, radius, out int minBlockX,
                out int maxBlockX, out int minBlockZ, out int maxBlockZ);
            float total = 0f;
            for (int blockZ = minBlockZ; blockZ <= maxBlockZ; blockZ++)
            {
                for (int blockX = minBlockX; blockX <= maxBlockX; blockX++)
                {
                    float weight = BlockWeight(blockX, blockZ, cell, radius);
                    if (weight <= 0f) continue;
                    int key = BlockKey(blockX, blockZ);
                    for (int i = 0; i < defs.Count; i++)
                    {
                        int[] counts;
                        if (plantCountsByDef.TryGetValue(defs[i], out counts))
                            total += counts[key] * weight;
                    }
                }
            }
            return total;
        }

        private void BlockBounds(IntVec3 cell, float radius,
            out int minBlockX, out int maxBlockX,
            out int minBlockZ, out int maxBlockZ)
        {
            int reach = Mathf.CeilToInt(radius + BlockSize * 0.71f);
            minBlockX = Mathf.Max(0, cell.x - reach) / BlockSize;
            maxBlockX = Mathf.Min(map.Size.x - 1, cell.x + reach)
                / BlockSize;
            minBlockZ = Mathf.Max(0, cell.z - reach) / BlockSize;
            maxBlockZ = Mathf.Min(map.Size.z - 1, cell.z + reach)
                / BlockSize;
        }

        private float BlockWeight(int blockX, int blockZ, IntVec3 cell,
            float radius)
        {
            float centerX = Mathf.Min(map.Size.x - 1,
                blockX * BlockSize + (BlockSize - 1) * 0.5f);
            float centerZ = Mathf.Min(map.Size.z - 1,
                blockZ * BlockSize + (BlockSize - 1) * 0.5f);
            float dx = centerX - cell.x;
            float dz = centerZ - cell.z;
            float halfDiagonal = BlockSize * 0.71f;
            float inner = Mathf.Max(0f, radius - halfDiagonal);
            float outer = radius + halfDiagonal;
            float distanceSquared = dx * dx + dz * dz;
            if (distanceSquared <= inner * inner) return 1f;
            if (distanceSquared >= outer * outer) return 0f;
            return Mathf.InverseLerp(outer * outer,
                inner * inner, distanceSquared);
        }

        private int BlockKey(int blockX, int blockZ)
        {
            return blockZ * blockColumns + blockX;
        }
    }

    [HarmonyPatch(typeof(GenStep_Plants), nameof(GenStep_Plants.Generate))]
    internal static class CARegionalPlantClusterIndexLifecyclePatch
    {
        [ThreadStatic] internal static CARegionalPlantSpatialIndex Active;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Map __0)
        {
            Active = null;
            Map map = __0;
            if (!CARegionalRiverPatchUtility.Active(map)) return;
            var index = new CARegionalPlantSpatialIndex(map,
                map.wildPlantSpawner);
            Active = index;
            try
            {
                index.Initialize();
            }
            catch
            {
                Active = null;
                throw;
            }
            Log.Message("[CA][Regional][Timing][Plants] initialized bounded "
                + "ecology spatial summaries in "
                + index.initializationMilliseconds + " ms for " + map.Size.x
                + "x" + map.Size.z + " map " + map.uniqueID
                + "; desired plants "
                + index.WholeMapDesiredPlants.ToString("F1"));
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Map __0)
        {
            CARegionalPlantSpatialIndex index = Active;
            if (index != null)
                Log.Message("[CA][Regional][Timing][Plants] bounded ecology "
                    + "index served " + index.clusterQueryCount
                    + " cluster, " + index.saturationQueries
                    + " saturation, " + index.successionQueries
                    + " succession, and " + index.distributionQueries
                    + " distribution queries; "
                    + "bypassed " + index.radialCellsBypassed
                    + " ThingGrid cell visits; inspected "
                    + index.clusterAggregatesInspected
                    + " cluster block summaries and "
                    + index.localBlocksInspected + " local density blocks for "
                    + __0.Size.x + "x" + __0.Size.z + " map "
                    + __0.uniqueID);
            Active = null;
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception)
        {
            Active = null;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(WildPlantSpawner),
        "CalculateDistancesToNearbyClusters")]
    internal static class CARegionalPlantClusterDistancePatch
    {
        private static readonly FieldInfo OutputField = AccessTools.Field(
            typeof(WildPlantSpawner), "distanceSqToNearbyClusters");

        [HarmonyPrefix]
        private static bool Prefix(WildPlantSpawner __instance, IntVec3 __0)
        {
            CARegionalPlantSpatialIndex index =
                CARegionalPlantClusterIndexLifecyclePatch.Active;
            if (index == null || !index.Owns(__instance)
                || OutputField == null)
                return true;
            var output = OutputField.GetValue(null)
                as Dictionary<ThingDef, float>;
            if (output == null) return true;
            index.Calculate(__0, output);
            return false;
        }
    }

    [HarmonyPatch(typeof(WildPlantSpawner),
        nameof(WildPlantSpawner.CheckSpawnWildPlantAt))]
    internal static class CARegionalPlantClusterIndexUpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(WildPlantSpawner __instance, IntVec3 __0,
            bool __result)
        {
            if (!__result) return;
            CARegionalPlantSpatialIndex index =
                CARegionalPlantClusterIndexLifecyclePatch.Active;
            if (index != null && index.Owns(__instance)) index.AddAt(__0);
        }
    }

    [HarmonyPatch(typeof(WildPlantSpawner),
        nameof(WildPlantSpawner.CurrentWholeMapNumDesiredPlants),
        MethodType.Getter)]
    internal static class CARegionalWholeMapDesiredPlantsPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(WildPlantSpawner __instance,
            ref float __result)
        {
            CARegionalPlantSpatialIndex index =
                CARegionalPlantClusterIndexLifecyclePatch.Active;
            if (index == null || !index.Owns(__instance)) return true;
            __result = index.WholeMapDesiredPlants;
            return false;
        }
    }

    [HarmonyPatch(typeof(WildPlantSpawner), "SaturatedAt")]
    internal static class CARegionalPlantSaturationPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(WildPlantSpawner __instance, IntVec3 __0,
            float __1, float __3, ref bool __result)
        {
            CARegionalPlantSpatialIndex index =
                CARegionalPlantClusterIndexLifecyclePatch.Active;
            if (index == null || !index.Owns(__instance)
                || !index.SupportsDensity(__1)) return true;
            __result = index.SaturatedAt(__0, __3);
            return false;
        }
    }

    [HarmonyPatch(typeof(WildPlantSpawner),
        "LocalPlantProportionsWeightFactor")]
    internal static class CARegionalPlantLocalDistributionPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(WildPlantSpawner __instance, IntVec3 __0,
            float __1, float __2, float __3, ThingDef __4,
            ref float __result)
        {
            CARegionalPlantSpatialIndex index =
                CARegionalPlantClusterIndexLifecyclePatch.Active;
            if (index == null || !index.Owns(__instance)
                || !index.SupportsDensity(__2)) return true;
            __result = index.LocalDistributionWeight(__0, __1, __3, __4);
            return false;
        }
    }

    [HarmonyPatch(typeof(WildPlantSpawner),
        "EnoughLowerOrderPlantsNearby")]
    internal static class CARegionalPlantSuccessionPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(WildPlantSpawner __instance, IntVec3 __0,
            float __1, float __2, ThingDef __3, ref bool __result)
        {
            CARegionalPlantSpatialIndex index =
                CARegionalPlantClusterIndexLifecyclePatch.Active;
            if (index == null || !index.Owns(__instance)
                || !index.SupportsDensity(__1)) return true;
            __result = index.EnoughLowerOrderPlantsNearby(__0, __2, __3);
            return false;
        }
    }

    // Thing.SpawnSetup registers a new thing in its touched regions before it
    // adds that thing to Map.spawnedThings. Vanilla nevertheless searches each
    // region's complete AllThings list to prove that this brand-new thing is not
    // already registered. On a regional map, plants, rubble, and loose chunks
    // turn that redundant linear membership check into a dominant generation
    // cost. Preserve the native region selection and ListerThings.Add path, but
    // omit only that impossible duplicate search for initial regional spawns.
    internal sealed class CARegionalInitialRegionRegistrationStats
    {
        internal readonly Map map;
        internal long initialThings;
        internal long regionAdds;
        internal long membershipComparisonsBypassed;
        internal long spawnedThingCapacityExtensions;

        internal CARegionalInitialRegionRegistrationStats(Map map)
        {
            this.map = map;
        }
    }

    [HarmonyPatch(typeof(MapGenerator),
        nameof(MapGenerator.GenerateContentsIntoMap))]
    internal static class CARegionalInitialRegionRegistrationLifecyclePatch
    {
        [ThreadStatic] private static Stack<
            CARegionalInitialRegionRegistrationStats> active;

        internal static CARegionalInitialRegionRegistrationStats For(Map map)
        {
            if (map == null || active == null || active.Count == 0) return null;
            CARegionalInitialRegionRegistrationStats stats = active.Peek();
            return stats != null && ReferenceEquals(stats.map, map)
                ? stats
                : null;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Map map)
        {
            if (active == null)
                active = new Stack<CARegionalInitialRegionRegistrationStats>();
            active.Push(CARegionalRiverPatchUtility.Active(map)
                && ReferenceEquals(MapGenerator.mapBeingGenerated, map)
                    ? new CARegionalInitialRegionRegistrationStats(map)
                    : null);
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Map map)
        {
            CARegionalInitialRegionRegistrationStats stats = For(map);
            if (stats == null) return;
            Log.Message("[CA][Regional][Timing][RegionRegistration] added "
                + stats.initialThings + " initial things across "
                + stats.regionAdds + " touched regions while bypassing "
                + stats.membershipComparisonsBypassed
                + " redundant linear membership comparisons and extending "
                + "the native spawned-thing capacity "
                + stats.spawnedThingCapacityExtensions + " times for "
                + map.Size.x + "x" + map.Size.z + " map " + map.uniqueID);
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception)
        {
            if (active != null && active.Count > 0) active.Pop();
            if (active?.Count == 0) active = null;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup))]
    internal static class CARegionalInitialThingSpawnScopePatch
    {
        [ThreadStatic] private static Stack<Thing> active;

        internal static bool Owns(Thing thing, Map map)
        {
            return thing != null
                && CARegionalInitialRegionRegistrationLifecyclePatch.For(map)
                    != null
                && active != null
                && active.Count > 0
                && ReferenceEquals(active.Peek(), thing);
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Thing __instance, Map map)
        {
            if (active == null) active = new Stack<Thing>();
            active.Push(CARegionalInitialRegionRegistrationLifecyclePatch
                    .For(map) != null
                ? __instance
                : null);
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception)
        {
            if (active != null && active.Count > 0) active.Pop();
            if (active?.Count == 0) active = null;
            return __exception;
        }
    }

    // ThingOwner uses 999,999 as its nominal infinite stack count, but its
    // generic TryAdd path still rejects Count >= maxStacks. Dense regional maps
    // can legitimately exceed that ceiling. Temporarily expose exactly one free
    // slot while the native map owner performs its normal add, then restore the
    // serialized value so saves do not inherit a mod-specific container limit.
    [HarmonyPatch(typeof(ThingOwner<Thing>),
        nameof(ThingOwner<Thing>.TryAdd),
        new[] { typeof(Thing), typeof(bool) })]
    internal static class CARegionalSpawnedThingCapacityPatch
    {
        private static readonly AccessTools.FieldRef<ThingOwner, int> MaxStacks =
            AccessTools.FieldRefAccess<ThingOwner, int>("maxStacks");

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(ThingOwner<Thing> __instance, Thing item,
            out int __state)
        {
            __state = int.MinValue;
            Map map = __instance?.Owner as Map;
            if (map == null || item == null
                || !ReferenceEquals(map.spawnedThings, __instance)) return;

            int prior = MaxStacks(__instance);
            if (__instance.Count < prior) return;
            if (CARegionalInitialRegionRegistrationLifecyclePatch.For(map)
                    == null
                && !CARegionalRiverPatchUtility.Active(map)) return;
            __state = prior;
            MaxStacks(__instance) = checked(__instance.Count + 1);
            CARegionalInitialRegionRegistrationStats stats =
                CARegionalInitialRegionRegistrationLifecyclePatch.For(map);
            if (stats != null) stats.spawnedThingCapacityExtensions++;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ThingOwner<Thing> __instance, int __state)
        {
            Restore(__instance, __state);
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(ThingOwner<Thing> __instance,
            int __state, Exception __exception)
        {
            Restore(__instance, __state);
            return __exception;
        }

        private static void Restore(ThingOwner<Thing> owner, int prior)
        {
            if (owner != null && prior != int.MinValue)
                MaxStacks(owner) = prior;
        }
    }

    [HarmonyPatch(typeof(RegionListersUpdater),
        nameof(RegionListersUpdater.RegisterInRegions))]
    internal static class CARegionalInitialRegionRegistrationPatch
    {
        [ThreadStatic] private static List<Region> touchedRegions;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Thing thing, Map map)
        {
            CARegionalInitialRegionRegistrationStats stats =
                CARegionalInitialRegionRegistrationLifecyclePatch.For(map);
            if (stats == null || thing == null
                || !ListerThings.EverListable(thing.def,
                    ListerThingsUse.Region)
                || !CARegionalInitialThingSpawnScopePatch.Owns(thing, map))
                return true;

            if (touchedRegions == null) touchedRegions = new List<Region>(4);
            try
            {
                RegionListersUpdater.GetTouchableRegions(thing, map,
                    touchedRegions);
                stats.initialThings++;
                for (int i = 0; i < touchedRegions.Count; i++)
                {
                    ListerThings lister = touchedRegions[i].ListerThings;
                    stats.membershipComparisonsBypassed +=
                        lister.AllThings.Count;
                    lister.Add(thing);
                    stats.regionAdds++;
                }
            }
            finally
            {
                touchedRegions.Clear();
            }
            return false;
        }
    }

    // AncientJunkClusters may consider an indoor-ruin group while locating each
    // scatter point. Vanilla answers that one proximity question by walking the
    // complete wall list for every candidate. Regional settlements can contain
    // thousands of walls, turning an otherwise local boolean test into a
    // forty-second global scan. Index the unchanged pre-step wall positions in
    // fixed blocks and retain the native terrain and exact-distance predicate.
    internal sealed class CARegionalAncientJunkWallIndex
    {
        private const int BlockSize = 32;

        private readonly Map map;
        private readonly int blockColumns;
        private readonly List<IntVec3>[] wallsByBlock;
        private readonly int wallCount;

        internal readonly long buildMilliseconds;
        internal long queryCount;
        internal long buildableCenterQueries;
        internal long wallComparisons;

        internal CARegionalAncientJunkWallIndex(Map map)
        {
            this.map = map;
            blockColumns = Mathf.CeilToInt(map.Size.x / (float)BlockSize);
            int blockRows = Mathf.CeilToInt(map.Size.z / (float)BlockSize);
            wallsByBlock = new List<IntVec3>[blockColumns * blockRows];

            var timer = System.Diagnostics.Stopwatch.StartNew();
            List<Thing> walls = map.listerThings.ThingsOfDef(ThingDefOf.Wall);
            wallCount = walls.Count;
            for (int i = 0; i < walls.Count; i++)
            {
                Thing wall = walls[i];
                IntVec3 position = wall.Position;
                int key = BlockKey(position.x / BlockSize,
                    position.z / BlockSize);
                List<IntVec3> block = wallsByBlock[key];
                if (block == null)
                {
                    block = new List<IntVec3>();
                    wallsByBlock[key] = block;
                }
                block.Add(position);
            }
            timer.Stop();
            buildMilliseconds = timer.ElapsedMilliseconds;
        }

        internal bool IndoorRuinSpot(CellRect rect)
        {
            queryCount++;
            IntVec3 center = rect.CenterCell;
            if (!center.GetTerrain(map).BuildableByPlayer) return false;
            buildableCenterQueries++;

            Vector2 diagonal = (new Vector2(rect.minX, rect.minZ)
                - new Vector2(rect.maxX, rect.maxZ)) * 2f;
            float distanceSquaredLimit = diagonal.sqrMagnitude;
            int reach = Mathf.CeilToInt(Mathf.Sqrt(distanceSquaredLimit));
            int minBlockX = Mathf.Max(0, center.x - reach) / BlockSize;
            int maxBlockX = Mathf.Min(map.Size.x - 1, center.x + reach)
                / BlockSize;
            int minBlockZ = Mathf.Max(0, center.z - reach) / BlockSize;
            int maxBlockZ = Mathf.Min(map.Size.z - 1, center.z + reach)
                / BlockSize;

            for (int blockZ = minBlockZ; blockZ <= maxBlockZ; blockZ++)
            {
                for (int blockX = minBlockX; blockX <= maxBlockX; blockX++)
                {
                    List<IntVec3> block = wallsByBlock[
                        BlockKey(blockX, blockZ)];
                    if (block == null) continue;
                    for (int i = 0; i < block.Count; i++)
                    {
                        wallComparisons++;
                        if (block[i].DistanceToSquared(center)
                            < distanceSquaredLimit)
                            return true;
                    }
                }
            }
            return false;
        }

        internal void LogReceipt(GenStep_ScatterGroup scatterer,
            bool completed)
        {
            long fullListComparisonBudget = buildableCenterQueries * wallCount;
            Log.Message("[CA][Regional][Timing][AncientJunkClusters] "
                + (completed ? "completed" : "aborted") + " with "
                + wallCount + " indexed walls built in " + buildMilliseconds
                + " ms; answered " + queryCount + " indoor-ruin queries by "
                + "inspecting " + wallComparisons + " local walls instead of "
                + "a worst-case full-list budget of "
                + fullListComparisonBudget + " comparisons for "
                + map.Size.x + "x" + map.Size.z + " map " + map.uniqueID
                + "; def " + (scatterer?.def?.defName ?? "unknown"));
        }

        private int BlockKey(int blockX, int blockZ)
        {
            return blockZ * blockColumns + blockX;
        }
    }

    [HarmonyPatch(typeof(GenStep_ScatterGroup),
        nameof(GenStep_ScatterGroup.Generate))]
    internal static class CARegionalAncientJunkWallIndexLifecyclePatch
    {
        [ThreadStatic]
        internal static CARegionalAncientJunkWallIndex Active;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(GenStep_ScatterGroup __instance, Map map)
        {
            Active = null;
            if (__instance?.def?.defName != "AncientJunkClusters"
                || !CARegionalRiverPatchUtility.Active(map)) return;
            Active = new CARegionalAncientJunkWallIndex(map);
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(GenStep_ScatterGroup __instance)
        {
            CARegionalAncientJunkWallIndex index = Active;
            Active = null;
            index?.LogReceipt(__instance, true);
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(GenStep_ScatterGroup __instance,
            Exception __exception)
        {
            CARegionalAncientJunkWallIndex index = Active;
            Active = null;
            index?.LogReceipt(__instance, false);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(GenStep_ScatterGroup), "IndoorRuinSpot")]
    internal static class CARegionalAncientJunkIndoorRuinSpotPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(CellRect rect, Map map,
            ref bool __result)
        {
            CARegionalAncientJunkWallIndex index =
                CARegionalAncientJunkWallIndexLifecyclePatch.Active;
            if (index == null) return true;
            __result = index.IndoorRuinSpot(rect);
            return false;
        }
    }

    // Vanilla's first terrain-scatter section materializes a candidate for every
    // map cell, randomizes that full array, and then compares every candidate
    // against the growing accepted list. That quadratic blue-noise pass is
    // tolerable on ordinary maps but monopolizes the main thread for minutes on
    // a multi-million-cell regional surface. Randomized four-cell candidate
    // blocks plus a fixed neighboring-block search preserve organic five-cell
    // minimum spacing in near-linear time. Scatter defs still own their ordinary
    // chance, size, rotation, terrain, and roof checks.
    internal static class CARegionalTerrainScatterPointCache
    {
        private const int CandidateBlockSize = 4;
        private const int NeighborBlockRadius = 2;
        private const int MinimumSpacingSquared = 25;
        private static readonly ConditionalWeakTable<Map, List<IntVec3>> Cache =
            new ConditionalWeakTable<Map, List<IntVec3>>();

        internal static List<IntVec3> For(Map map)
        {
            return Cache.GetValue(map, Build);
        }

        private static List<IntVec3> Build(Map map)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            int worldSeed = Verse.Find.World?.info?.Seed ?? 0;
            int seed = unchecked(map.ConstantRandSeed * 397 ^ worldSeed);
            int blockColumns = Mathf.CeilToInt(map.Size.x
                / (float)CandidateBlockSize);
            int blockRows = Mathf.CeilToInt(map.Size.z
                / (float)CandidateBlockSize);
            int blockCount = blockColumns * blockRows;
            var order = new int[blockCount];
            var acceptedPointByBlock = new int[blockCount];
            for (int i = 0; i < blockCount; i++)
            {
                order[i] = i;
                acceptedPointByBlock[i] = -1;
            }
            uint randomState = (uint)seed;
            if (randomState == 0U) randomState = 0x9E3779B9U;
            for (int i = blockCount - 1; i > 0; i--)
            {
                int swap = (int)(Next(ref randomState) % (uint)(i + 1));
                int prior = order[i];
                order[i] = order[swap];
                order[swap] = prior;
            }

            var points = new List<IntVec3>(Math.Max(16, blockCount / 2));
            for (int index = 0; index < order.Length; index++)
            {
                int blockId = order[index];
                int blockX = blockId % blockColumns;
                int blockZ = blockId / blockColumns;
                int baseX = blockX * CandidateBlockSize;
                int baseZ = blockZ * CandidateBlockSize;
                int blockWidth = Math.Min(CandidateBlockSize,
                    map.Size.x - baseX);
                int blockHeight = Math.Min(CandidateBlockSize,
                    map.Size.z - baseZ);
                uint hash = Hash(seed, blockX, blockZ);
                int x = baseX + (int)(hash % (uint)blockWidth);
                int z = baseZ + (int)((hash >> 16)
                    % (uint)blockHeight);
                bool tooClose = false;
                int minBlockX = Math.Max(0,
                    blockX - NeighborBlockRadius);
                int maxBlockX = Math.Min(blockColumns - 1,
                    blockX + NeighborBlockRadius);
                int minBlockZ = Math.Max(0,
                    blockZ - NeighborBlockRadius);
                int maxBlockZ = Math.Min(blockRows - 1,
                    blockZ + NeighborBlockRadius);
                for (int nearbyZ = minBlockZ;
                    nearbyZ <= maxBlockZ && !tooClose; nearbyZ++)
                {
                    for (int nearbyX = minBlockX;
                        nearbyX <= maxBlockX; nearbyX++)
                    {
                        int accepted = acceptedPointByBlock[
                            nearbyZ * blockColumns + nearbyX];
                        if (accepted < 0) continue;
                        IntVec3 other = points[accepted];
                        int dx = x - other.x;
                        int dz = z - other.z;
                        if (dx * dx + dz * dz
                            <= MinimumSpacingSquared)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                }
                if (tooClose) continue;
                acceptedPointByBlock[blockId] = points.Count;
                points.Add(new IntVec3(x, 0, z));
            }
            timer.Stop();
            Log.Message("[CA][Regional][Timing][TerrainScatter] built "
                + points.Count + " deterministic minimum-spaced points for "
                + map.Size.x + "x" + map.Size.z + " map " + map.uniqueID
                + " in " + timer.ElapsedMilliseconds
                + " ms instead of vanilla's quadratic whole-map pass");
            return points;
        }

        private static uint Hash(int seed, int x, int z)
        {
            unchecked
            {
                uint value = (uint)seed;
                value ^= (uint)x * 374761393U;
                value ^= (uint)z * 668265263U;
                value = (value ^ value >> 13) * 1274126177U;
                return value ^ value >> 16;
            }
        }

        private static uint Next(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }

    [HarmonyPatch(typeof(SectionLayer_TerrainScatter), "GetScatPoints")]
    internal static class CARegionalTerrainScatterPointPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Map __0, ref List<IntVec3> __result)
        {
            if (!CARegionalRiverPatchUtility.Active(__0)) return true;
            __result = CARegionalTerrainScatterPointCache.For(__0);
            return false;
        }
    }
}
