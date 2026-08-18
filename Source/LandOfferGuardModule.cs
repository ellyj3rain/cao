using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace ColonistAwareness
{
    // The engine's above-the-map ownership layer has a single faction slot
    // (Map.IsPlayerHome reads one parent Faction), so the Archonexus land
    // sale treats an entire regional map - other communities included - as
    // one sellable player settlement, and accepting it runs
    // MoveColonyUtility.MoveColonyAndReset, tearing the whole region down.
    // Until claims exist as objects, a wholesale land sale cannot mean
    // anything lawful on a regional save: block acceptance with a visible
    // reason in the quest window, and stop further sale cycles generating.
    internal static class LandOfferGuard
    {
        internal static bool RegionalSaveActive
        {
            get
            {
                CARegionalWorldComponent comp = CARegionalWorldComponent.Current;
                if (comp == null) return false;
                return comp.Regions.Count > 0 || comp.Records.Count > 0;
            }
        }

        internal static bool IsWholesaleLandSale(QuestScriptDef def)
        {
            if (def == null) return false;
            return def == QuestScriptDefOf.EndGame_ArchonexusVictory_FirstCycle
                || def == QuestScriptDefOf.EndGame_ArchonexusVictory_SecondCycle
                || def == QuestScriptDefOf.EndGame_ArchonexusVictory_ThirdCycle;
        }
    }

    [HarmonyPatch(typeof(QuestUtility), nameof(QuestUtility.CanAcceptQuest))]
    internal static class Patch_LandOfferGuard_Accept
    {
        private static void Postfix(Quest quest, ref AcceptanceReport __result)
        {
            if (!__result.Accepted || quest == null) return;
            if (!LandOfferGuard.IsWholesaleLandSale(quest.root)) return;
            if (!LandOfferGuard.RegionalSaveActive) return;
            __result = new AcceptanceReport(
                "The region is not a single sellable settlement - the land"
                + " carries other communities and their claims.");
        }
    }

    [HarmonyPatch(typeof(QuestNode_Root_ArchonexusVictory_Cycle), "TestRunInt")]
    internal static class Patch_LandOfferGuard_Generate
    {
        private static bool Prefix(ref bool __result)
        {
            if (!LandOfferGuard.RegionalSaveActive) return true;
            __result = false;
            return false;
        }
    }
}
