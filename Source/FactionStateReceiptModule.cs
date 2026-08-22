using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Developer receipt for the separation between native Ideoligion and CA
    // inherited Culture, Political Order, and represented institutions.
    internal static class CAFactionStateReceipt
    {
        internal static string Run()
        {
            var text = new StringBuilder();
            text.AppendLine("[CA][Faction] state receipt");
            CAFactionStateWorldComponent store = CAFactionStateWorldComponent.Current;
            if (store == null || Verse.Find.FactionManager == null)
                return text.Append("  no world - generate one first")
                    .ToString();

            List<Faction> factions = Verse.Find.FactionManager
                .AllFactionsListForReading
                .Where(CAFactionStateGenerator.UsesFactionState)
                .ToList();
            var before = factions.ToDictionary(faction => faction.loadID,
                IdeoligionSignature);
            string pass = CAFactionStateGenerator.RunWorldPass(
                "faction-state receipt");

            int complete = 0;
            int unsetBeliefs = 0;
            int completeStructures = 0;
            int acknowledgedIncompleteStructures = 0;
            int unacknowledgedUnsetStructures = 0;
            int changedIdeoligions = 0;
            foreach (Faction faction in factions)
            {
                CAFactionState record = store.Find(faction);
                if (record?.culture != null
                    && !record.culture.id.NullOrEmpty()
                    && record.politicalBeliefs != null)
                    complete++;
                if (record?.politicalBeliefs == null
                    || CAFactionAxes.CountByState(
                        record.politicalBeliefs.positions,
                        CAAxisSource.Unset) > 0)
                    unsetBeliefs++;
                bool structureUnset = record == null
                    || CAFactionAxes.CountByState(record.factionStructure,
                        CAAxisSource.Unset) > 0;
                if (!structureUnset) completeStructures++;
                else if (record?.institutionalStateIncomplete == true)
                    acknowledgedIncompleteStructures++;
                else unacknowledgedUnsetStructures++;
                if (before[faction.loadID] != IdeoligionSignature(faction))
                    changedIdeoligions++;
            }

            text.AppendLine("  " + pass);
            text.AppendLine("  eligible factions: " + factions.Count);
            text.AppendLine("  substantive Culture and political owners: "
                + complete);
            text.AppendLine("  factions with unset Political Order: "
                + unsetBeliefs);
            text.AppendLine("  factions with represented institutions: "
                + completeStructures);
            text.AppendLine("  factions explicitly awaiting institutional "
                + "evidence: " + acknowledgedIncompleteStructures);
            text.AppendLine("  factions with unacknowledged unset "
                + "institutions: " + unacknowledgedUnsetStructures);
            text.AppendLine("  Ideoligions changed: " + changedIdeoligions
                + " (expected 0)");
            text.Append("  RESULT: ").Append(changedIdeoligions == 0
                && complete == factions.Count
                && unsetBeliefs == 0
                && unacknowledgedUnsetStructures == 0
                    ? "PASS" : "FAIL");
            return text.ToString();
        }

        private static string IdeoligionSignature(Faction faction)
        {
            Ideo ideo = faction?.ideos?.PrimaryIdeo;
            if (ideo == null) return "none";
            string memes = string.Join(",", (ideo.memes
                    ?? new List<MemeDef>())
                .Where(meme => meme != null)
                .Select(meme => meme.defName).OrderBy(name => name));
            string precepts = string.Join(",", ideo.PreceptsListForReading
                .Where(precept => precept?.def != null)
                .Select(precept => precept.def.defName)
                .OrderBy(name => name));
            return ideo.id + "|" + ideo.name + "|" + memes + "|" + precepts;
        }
    }

    public static partial class CADebugActions
    {
        [DebugAction("Colonist Awareness",
            "Faction state receipt (Ideoligion read-only)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Entry)]
        private static void FactionStateReceipt()
        {
            Log.Message(CAFactionStateReceipt.Run());
        }
    }
}
