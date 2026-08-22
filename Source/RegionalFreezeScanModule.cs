using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Odyssey's freeze/thaw steady scan (TempTerrainManager.Tick ->
    // FreezeManager.DoCellSteadyEffects) visits area*0.0006 random cells
    // per tick and pays two GetTemperature and two GetRoom lookups per
    // visit before any early-out, so on a regional backing map the scan
    // costs the whole map area regardless of how much of it could ever
    // freeze or melt: measured 33.28 ms of a 59.35 ms quiet tick on the
    // 1246x1080 convergence fixture. A visit is provably a no-op unless
    // the visited cell's terrain canFreeze (the freezing trigger tests
    // CanFreeze on the visited cell itself) or ThinIce lies within the
    // 2.9-cell melt radius (melting only removes ThinIce found there).
    // On regional maps this screen skips exactly those no-op visits and
    // leaves every relevant visit fully native. Ordinary maps are never
    // touched.
    //
    // STATED DIVERGENCE: above 2C the native melt branch consumes one
    // Rand.Chance draw per visited cell before discovering there is no
    // ice to melt; skipped visits no longer consume that draw. The
    // runtime Rand stream is not a determinism contract (the generation
    // modules own generation-order guarantees), so the divergence is
    // accepted and named rather than reproduced at full scan cost.
    internal static class CARegionalFreezeScan
    {
        internal sealed class Mask
        {
            internal ulong[] bits;
        }

        private static readonly ConditionalWeakTable<Map, Mask> Masks =
            new ConditionalWeakTable<Map, Mask>();

        internal static readonly bool Disabled = string.Equals(
            Environment.GetEnvironmentVariable("CA_REGIONAL_FREEZESCAN"),
            "0", StringComparison.Ordinal);

        // The melt trigger reads a 2.9-cell radius, so a terrain write at
        // one cell can change the relevance of every cell that reads it.
        private const float MeltRadius = 2.9f;

        internal static bool VisitIsRelevant(Map map, IntVec3 c)
        {
            Mask mask = Masks.GetValue(map, Build);
            int index = map.cellIndices.CellToIndex(c);
            return (mask.bits[index >> 6] & (1UL << (index & 63))) != 0UL;
        }

        internal static void RefreshAround(Map map, IntVec3 c)
        {
            if (Disabled || map == null) return;
            if (!Masks.TryGetValue(map, out Mask mask)) return;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(c,
                MeltRadius, useCenter: true))
            {
                if (!cell.InBounds(map)) continue;
                Recompute(map, mask, cell);
            }
        }

        private static Mask Build(Map map)
        {
            var mask = new Mask();
            int area = map.cellIndices.NumGridCells;
            mask.bits = new ulong[(area + 63) >> 6];
            TerrainGrid grid = map.terrainGrid;
            int freezable = 0;
            List<IntVec3> ice = null;
            for (int i = 0; i < area; i++)
            {
                TerrainDef terrain = grid.TerrainAt(i);
                if (terrain.canFreeze)
                {
                    mask.bits[i >> 6] |= 1UL << (i & 63);
                    freezable++;
                }
                if (terrain == TerrainDefOf.ThinIce)
                {
                    if (ice == null) ice = new List<IntVec3>();
                    ice.Add(map.cellIndices.IndexToCell(i));
                }
            }
            if (ice != null)
                foreach (IntVec3 c in ice)
                    foreach (IntVec3 cell in GenRadial.RadialCellsAround(c,
                        MeltRadius, useCenter: true))
                    {
                        if (!cell.InBounds(map)) continue;
                        int index = map.cellIndices.CellToIndex(cell);
                        mask.bits[index >> 6] |= 1UL << (index & 63);
                    }
            Log.Message("[CA][Regional][FreezeScan] map " + map.uniqueID
                + ": freeze/thaw steady visits screened to relevant cells; "
                + freezable + " freezable and "
                + (ice == null ? 0 : ice.Count) + " ThinIce cells of "
                + area + " at build; terrain writes keep the screen exact; "
                + "env CA_REGIONAL_FREEZESCAN=0 disables at load");
            return mask;
        }

        private static void Recompute(Map map, Mask mask, IntVec3 cell)
        {
            bool relevant = map.terrainGrid.TerrainAt(cell).canFreeze;
            if (!relevant)
                foreach (IntVec3 near in GenRadial.RadialCellsAround(cell,
                    MeltRadius, useCenter: true))
                {
                    if (!near.InBounds(map)) continue;
                    if (map.terrainGrid.TerrainAt(near)
                        == TerrainDefOf.ThinIce)
                    {
                        relevant = true;
                        break;
                    }
                }
            int index = map.cellIndices.CellToIndex(cell);
            if (relevant)
                mask.bits[index >> 6] |= 1UL << (index & 63);
            else
                mask.bits[index >> 6] &= ~(1UL << (index & 63));
        }
    }

    [HarmonyPatch(typeof(FreezeManager),
        nameof(FreezeManager.DoCellSteadyEffects))]
    internal static class CAFreezeScanScreenPatch
    {
        private static readonly AccessTools.FieldRef<FreezeManager, Map>
            MapField = AccessTools.FieldRefAccess<FreezeManager, Map>("map");

        internal static bool Prefix(FreezeManager __instance, IntVec3 c)
        {
            if (CARegionalFreezeScan.Disabled) return true;
            if (!ModsConfig.OdysseyActive) return true;
            Map map = MapField(__instance);
            if (map == null || !CARegionalEngineRoot.IsRegional(map))
                return true;
            return CARegionalFreezeScan.VisitIsRelevant(map, c);
        }
    }

    // All three mutators funnel every freeze, melt, construction, and
    // generation terrain write, so refreshing the melt-radius
    // neighbourhood of each write keeps the screen exact at a cost paid
    // only when terrain actually changes.
    [HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.SetTerrain))]
    internal static class CAFreezeScanSetTerrainPatch
    {
        internal static void Postfix(TerrainGrid __instance, IntVec3 c)
        {
            CARegionalFreezeScan.RefreshAround(
                CAFreezeScanTerrainMap.Of(__instance), c);
        }
    }

    [HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.SetTempTerrain))]
    internal static class CAFreezeScanSetTempTerrainPatch
    {
        internal static void Postfix(TerrainGrid __instance, IntVec3 c)
        {
            CARegionalFreezeScan.RefreshAround(
                CAFreezeScanTerrainMap.Of(__instance), c);
        }
    }

    [HarmonyPatch(typeof(TerrainGrid),
        nameof(TerrainGrid.RemoveTempTerrain))]
    internal static class CAFreezeScanRemoveTempTerrainPatch
    {
        internal static void Postfix(TerrainGrid __instance, IntVec3 c)
        {
            CARegionalFreezeScan.RefreshAround(
                CAFreezeScanTerrainMap.Of(__instance), c);
        }
    }

    internal static class CAFreezeScanTerrainMap
    {
        private static readonly AccessTools.FieldRef<TerrainGrid, Map>
            MapField = AccessTools.FieldRefAccess<TerrainGrid, Map>("map");

        internal static Map Of(TerrainGrid grid)
        {
            return MapField(grid);
        }
    }
}
