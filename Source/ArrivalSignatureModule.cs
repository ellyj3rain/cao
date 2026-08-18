using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Record how the player arrived. Drop pods are visible across the region;
    // a walk-in is known near the landing until later contact. The scenario's
    // arrival method supplies the same fact for vanilla and configured starts.
    [HarmonyPatch(typeof(GameComponentUtility),
        nameof(GameComponentUtility.StartedNewGame))]
    internal static class CAArrivalSignaturePatch
    {
        private static void Postfix()
        {
            CAArrivalSignature.OnNewGame();
        }
    }

    internal static class CAArrivalSignature
    {
        internal static void OnNewGame()
        {
            var comp = CAOrganizationWorldComponent.Current;
            if (comp == null || Find.Scenario == null) return;
            bool loud = false;
            string how = "walked into this land quietly";
            foreach (ScenPart part in Find.Scenario.AllParts)
            {
                var arrive = part as ScenPart_PlayerPawnsArriveMethod;
                if (arrive == null) continue;
                var method = Traverse.Create(arrive).Field("method")
                    .GetValue<PlayerPawnsArriveMethod>();
                if (method != PlayerPawnsArriveMethod.Standing)
                {
                    loud = true;
                    how = "fell from the sky in fire";
                }
            }
            Map map = Find.CurrentMap;
            IntVec3 cell = map != null
                ? (map.mapPawns.FreeColonistsSpawned.FirstOrDefault()
                    ?.Position ?? map.Center)
                : IntVec3.Invalid;
            comp.RecordArrival(loud, cell);
            comp.EnsureColony()?.Record("arrival", "we " + how
                + " - scenario: " + Find.Scenario.name);
            Log.Message("[CA] arrival signature: '" + Find.Scenario.name
                + "' - " + (loud
                    ? "LOUD; the region saw the sky burn"
                    : "quiet; known only near the landing"));
        }
    }
}
