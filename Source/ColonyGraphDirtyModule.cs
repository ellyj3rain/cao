using HarmonyLib;
using Verse;

namespace ColonistAwareness
{
    // The colony's shape changes the moment the player changes it: a
    // wall closed, a door removed, a turret built. The graph is marked
    // stale here so it cannot describe an earlier settlement layout.
    [HarmonyPatch(typeof(Building), nameof(Building.SpawnSetup))]
    internal static class CAColonyGraphBuiltPatch
    {
        private static void Postfix(Building __instance)
        {
            if (__instance?.Map == null) return;
            __instance.Map.GetComponent<CAColonyGraphMapComponent>()
                ?.MarkDirty();
            __instance.Map.GetComponent<CASettlementGraphMapComponent>()
                ?.MarkDirtyAt(__instance.Position);
        }
    }

    [HarmonyPatch(typeof(Building), nameof(Building.DeSpawn))]
    internal static class CAColonyGraphRemovedPatch
    {
        private static void Prefix(Building __instance)
        {
            if (__instance?.Map == null) return;
            __instance.Map.GetComponent<CAColonyGraphMapComponent>()
                ?.MarkDirty();
            __instance.Map.GetComponent<CASettlementGraphMapComponent>()
                ?.MarkDirtyAt(__instance.Position);
        }
    }
}
