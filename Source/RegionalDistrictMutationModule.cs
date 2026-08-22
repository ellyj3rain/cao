using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace ColonistAwareness
{
    // Region.District already checks that the destination differs from the
    // current district before it calls District.AddRegion/RemoveRegion. The
    // native methods nevertheless linearly prove membership again, and Remove
    // then performs a second identical linear search. During a scattered
    // regional graph repair those checks dominate assignment into the very
    // large outdoor district. Preserve list order and all native bookkeeping,
    // but rely on the setter's exact invariant while the updater is working.
    [HarmonyPatch(typeof(District), nameof(District.AddRegion))]
    internal static class CARegionalDistrictAddPatch
    {
        private static readonly AccessTools.FieldRef<District, List<Region>>
            Regions = AccessTools.FieldRefAccess<District, List<Region>>(
                "regions");
        private static readonly AccessTools.FieldRef<District, int>
            EdgeCount = AccessTools.FieldRefAccess<District, int>(
                "numRegionsTouchingMapEdge");

        [HarmonyPrefix]
        private static bool Prefix(District __instance, Region r)
        {
            if (!CARegionalRegionRebuildTimingPatch.IsRebuildingRegions)
                return true;

            List<Region> regions = Regions(__instance);
            regions.Add(r);
            if (r.touchesMapEdge)
            {
                ref int edgeCount = ref EdgeCount(__instance);
                edgeCount++;
            }
            if (regions.Count == 1)
                r.Map.regionGrid.allDistricts.Add(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(District), nameof(District.RemoveRegion))]
    internal static class CARegionalDistrictRemovePatch
    {
        private static readonly AccessTools.FieldRef<District, List<Region>>
            Regions = AccessTools.FieldRefAccess<District, List<Region>>(
                "regions");
        private static readonly AccessTools.FieldRef<District, int>
            EdgeCount = AccessTools.FieldRefAccess<District, int>(
                "numRegionsTouchingMapEdge");
        private static readonly FieldInfo CachedOpenRoofCount =
            AccessTools.Field(typeof(District), "cachedOpenRoofCount");
        private static readonly FieldInfo CachedExposedCount =
            AccessTools.Field(typeof(District), "cachedExposedCount");
        private static readonly FieldInfo CachedOpenRoofState =
            AccessTools.Field(typeof(District), "cachedOpenRoofState");
        private static readonly FieldInfo CachedExposedState =
            AccessTools.Field(typeof(District), "cachedExposedState");

        [HarmonyPrefix]
        private static bool Prefix(District __instance, Region r)
        {
            if (!CARegionalRegionRebuildTimingPatch.IsRebuildingRegions)
                return true;

            List<Region> regions = Regions(__instance);
            if (!regions.Remove(r))
            {
                Log.Error("Tried to remove region from District but this "
                    + "region is not here. region=" + r + ", district="
                    + __instance);
                return false;
            }
            if (r.touchesMapEdge)
            {
                ref int edgeCount = ref EdgeCount(__instance);
                edgeCount--;
            }
            if (regions.Count != 0) return false;

            __instance.Room = null;
            CachedOpenRoofCount?.SetValue(__instance, -1);
            CachedExposedCount?.SetValue(__instance, -1);
            (CachedOpenRoofState?.GetValue(__instance)
                as System.IDisposable)?.Dispose();
            CachedOpenRoofState?.SetValue(__instance, null);
            (CachedExposedState?.GetValue(__instance)
                as System.IDisposable)?.Dispose();
            CachedExposedState?.SetValue(__instance, null);
            __instance.Map?.regionGrid.allDistricts.Remove(__instance);
            return false;
        }
    }
}
