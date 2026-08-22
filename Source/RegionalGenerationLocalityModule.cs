using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Native animal map generation asks District.TouchesMapEdge for every
    // candidate. If later gensteps have dirtied a regional map, the first such
    // query rebuilds the complete room graph even though this genstep needs
    // only one fact: whether a same-region-type component reaches an edge.
    // Build that exact generation-local fact directly from the current grids.
    [HarmonyPatch(typeof(RCellFinder),
        nameof(RCellFinder.RandomAnimalSpawnCell_MapGen))]
    internal static class CARegionalAnimalSpawnLocalityPatch
    {
        [ThreadStatic] private static Map cachedMap;
        [ThreadStatic] private static bool[] touchesEdge;
        [ThreadStatic] private static bool[] reachesPlayerStart;

        [HarmonyPrefix]
        private static bool Prefix(Map map, ref IntVec3 __result)
        {
            if (map == null
                || Current.ProgramState != ProgramState.MapInitializing
                || MapGenerator.mapBeingGenerated != map
                || !CARegionalRiverPatchUtility.Active(map))
                return true;

            EnsureCache(map);
            int numStand = 0;
            int numDistrict = 0;
            int numTouch = 0;
            if (!CellFinderLoose.TryGetRandomCellWith(delegate(IntVec3 c)
            {
                if (!c.Standable(map))
                {
                    numStand++;
                    return false;
                }
                TerrainDef terrain = c.GetTerrain(map);
                if (terrain.avoidWander || terrain.dangerous)
                    return false;
                int index = map.cellIndices.CellToIndex(c);
                if ((uint)index >= (uint)touchesEdge.Length)
                {
                    numDistrict++;
                    return false;
                }
                if (!touchesEdge[index])
                {
                    numTouch++;
                    return false;
                }
                return true;
            }, map, 1000, out IntVec3 result))
            {
                result = CellFinder.RandomCell(map);
                Log.Warning("RandomAnimalSpawnCell_MapGen failed: numStand="
                    + numStand + ", numDistrict=" + numDistrict
                    + ", numTouch=" + numTouch + ". PlayerStartSpot="
                    + MapGenerator.PlayerStartSpot + ". Returning "
                    + result);
            }
            __result = result;
            return false;
        }

        internal static void Clear(Map map)
        {
            if (!ReferenceEquals(cachedMap, map)) return;
            cachedMap = null;
            touchesEdge = null;
            reachesPlayerStart = null;
        }

        internal static bool ReachesPlayerStart(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map)) return false;
            EnsureStartReachability(map);
            int index = map.cellIndices.CellToIndex(cell);
            return reachesPlayerStart != null
                && (uint)index < (uint)reachesPlayerStart.Length
                && reachesPlayerStart[index];
        }

        private static void EnsureCache(Map map)
        {
            int cellCount = map.cellIndices.NumGridCells;
            if (!ReferenceEquals(cachedMap, map))
            {
                cachedMap = null;
                touchesEdge = null;
                reachesPlayerStart = null;
            }
            if (ReferenceEquals(cachedMap, map)
                && touchesEdge != null && touchesEdge.Length == cellCount)
                return;

            Stopwatch watch = Stopwatch.StartNew();
            int width = map.Size.x;
            int height = map.Size.z;
            var types = new byte[cellCount];
            var reached = new bool[cellCount];
            var queue = new int[cellCount];

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cell = new IntVec3(x, 0, z);
                    int index = map.cellIndices.CellToIndex(cell);
                    types[index] = (byte)cell.GetExpectedRegionType(map);
                }
            }

            int head = 0;
            int tail = 0;
            for (int x = 0; x < width; x++)
            {
                Seed(new IntVec3(x, 0, 0));
                if (height > 1) Seed(new IntVec3(x, 0, height - 1));
            }
            for (int z = 1; z + 1 < height; z++)
            {
                Seed(new IntVec3(0, 0, z));
                if (width > 1) Seed(new IntVec3(width - 1, 0, z));
            }

            while (head < tail)
            {
                int index = queue[head++];
                IntVec3 cell = map.cellIndices.IndexToCell(index);
                Visit(cell.x - 1, cell.z);
                Visit(cell.x + 1, cell.z);
                Visit(cell.x, cell.z - 1);
                Visit(cell.x, cell.z + 1);
            }

            cachedMap = map;
            touchesEdge = reached;
            watch.Stop();
            Log.Message("[CA][Regional][Timing] animal edge-locality mask "
                + "classified " + cellCount + " cells and reached " + tail
                + " in " + watch.ElapsedMilliseconds + " ms for "
                + map.Size.x + "x" + map.Size.z + " map " + map.uniqueID
                + "; native animal selection no longer requires a dirty "
                + "global region rebuild");

            void Seed(IntVec3 cell)
            {
                int index = map.cellIndices.CellToIndex(cell);
                if (reached[index]
                    || !((RegionType)types[index]).Passable())
                    return;
                reached[index] = true;
                queue[tail++] = index;
            }

            void Visit(int x, int z)
            {
                if ((uint)x >= (uint)width || (uint)z >= (uint)height)
                    return;
                var cell = new IntVec3(x, 0, z);
                int index = map.cellIndices.CellToIndex(cell);
                if (reached[index]
                    || !((RegionType)types[index]).Passable())
                    return;
                reached[index] = true;
                queue[tail++] = index;
            }
        }

        private static void EnsureStartReachability(Map map)
        {
            int cellCount = map.cellIndices.NumGridCells;
            if (!ReferenceEquals(cachedMap, map))
            {
                cachedMap = null;
                touchesEdge = null;
                reachesPlayerStart = null;
            }
            if (ReferenceEquals(cachedMap, map)
                && reachesPlayerStart != null
                && reachesPlayerStart.Length == cellCount)
                return;

            Stopwatch watch = Stopwatch.StartNew();
            int width = map.Size.x;
            int height = map.Size.z;
            var reached = new bool[cellCount];
            var queue = new int[cellCount];
            PathGrid pathGrid = map.pathing.For(
                TraverseParms.For(TraverseMode.PassDoors)).pathGrid;
            IntVec3 start = MapGenerator.PlayerStartSpot;
            int head = 0;
            int tail = 0;

            if (MapGenerator.PlayerStartSpotValid && start.InBounds(map))
            {
                if (pathGrid.WalkableFast(start))
                    Seed(start);
                else
                    for (int i = 0; i < GenAdj.AdjacentCells.Length; i++)
                        Seed(start + GenAdj.AdjacentCells[i]);
            }

            while (head < tail)
            {
                IntVec3 cell = map.cellIndices.IndexToCell(queue[head++]);
                Visit(cell.x - 1, cell.z);
                Visit(cell.x + 1, cell.z);
                Visit(cell.x, cell.z - 1);
                Visit(cell.x, cell.z + 1);
            }

            cachedMap = map;
            reachesPlayerStart = reached;
            watch.Stop();
            Log.Message("[CA][Regional][Timing] animal colony-reachability "
                + "mask reached " + tail + " of " + cellCount + " cells in "
                + watch.ElapsedMilliseconds + " ms for " + map.Size.x + "x"
                + map.Size.z + " map " + map.uniqueID
                + "; animal entry selection retains native edge sampling "
                + "without rebuilding the global region graph");

            void Seed(IntVec3 cell)
            {
                if (!cell.InBounds(map)) return;
                int index = map.cellIndices.CellToIndex(cell);
                if (reached[index] || !pathGrid.WalkableFast(index)
                    || !cell.GetExpectedRegionType(map).Passable()) return;
                reached[index] = true;
                queue[tail++] = index;
            }

            void Visit(int x, int z)
            {
                if ((uint)x >= (uint)width || (uint)z >= (uint)height)
                    return;
                var cell = new IntVec3(x, 0, z);
                int index = map.cellIndices.CellToIndex(cell);
                if (reached[index] || !pathGrid.WalkableFast(index)
                    || !cell.GetExpectedRegionType(map).Passable()) return;
                reached[index] = true;
                queue[tail++] = index;
            }
        }
    }

    [HarmonyPatch(typeof(GenStep_Animals), nameof(GenStep_Animals.Generate))]
    internal static class CARegionalAnimalSpawnLocalityLifecyclePatch
    {
        [ThreadStatic] private static Stack<Map> activeMaps;

        internal static Map ActiveMap => activeMaps != null
            && activeMaps.Count > 0 ? activeMaps.Peek() : null;

        [HarmonyPrefix]
        private static void Prefix(Map map, out bool __state)
        {
            __state = map != null
                && Current.ProgramState == ProgramState.MapInitializing
                && MapGenerator.mapBeingGenerated == map
                && CARegionalRiverPatchUtility.Active(map);
            if (!__state) return;
            if (activeMaps == null) activeMaps = new Stack<Map>();
            activeMaps.Push(map);
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, Map map,
            bool __state)
        {
            CARegionalAnimalSpawnLocalityPatch.Clear(map);
            if (__state && activeMaps != null && activeMaps.Count > 0)
                activeMaps.Pop();
            if (activeMaps?.Count == 0) activeMaps = null;
            return __exception;
        }
    }

    // Coastal animal spawning calls this entry-cell routine after the normal
    // animal selector. Native code asks both Reachability.CanReachColony and
    // District.TouchesMapEdge, forcing a complete dirty room graph rebuild.
    // Preserve the same native edge sampler and validation order, but answer
    // those two generation-local facts from current grids.
    [HarmonyPatch(typeof(RCellFinder),
        nameof(RCellFinder.TryFindRandomPawnEntryCell))]
    internal static class CARegionalAnimalEntryCellLocalityPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref IntVec3 result, Map map,
            float roadChance, bool allowFogged,
            Predicate<IntVec3> extraValidator, ref bool __result)
        {
            if (!ReferenceEquals(
                    CARegionalAnimalSpawnLocalityLifecyclePatch.ActiveMap,
                    map))
                return true;

            __result = CellFinder.TryFindRandomEdgeCellWith(delegate(IntVec3 c)
            {
                if (!c.Standable(map) || c.GetTerrain(map).dangerous)
                    return false;
                if (!map.TileInfo.AllowRoofedEdgeWalkIn
                    && map.roofGrid.Roofed(c)) return false;
                if (!CARegionalAnimalSpawnLocalityPatch
                        .ReachesPlayerStart(map, c)) return false;
                if (!allowFogged && c.Fogged(map)) return false;
                return extraValidator == null || extraValidator(c);
            }, map, roadChance, out result);
            return false;
        }
    }

    [HarmonyPatch(typeof(Reachability),
        nameof(Reachability.CanReachMapEdge))]
    internal static class CARegionalAnimalEdgeReachabilityPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Reachability __instance, IntVec3 c,
            ref bool __result)
        {
            Map map = CARegionalAnimalSpawnLocalityLifecyclePatch.ActiveMap;
            if (map == null || !ReferenceEquals(map.reachability, __instance)
                || !c.InBounds(map) || !c.OnEdge(map)
                || !c.GetExpectedRegionType(map).Passable())
                return true;
            __result = true;
            return false;
        }
    }

}
