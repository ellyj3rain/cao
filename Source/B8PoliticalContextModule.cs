using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // Converts realized faction evidence into explicit per-axis scores. This
    // adapter owns no random choice and never touches player-authored beliefs.
    internal static class CAPoliticalContext
    {
        internal static CAPoliticalDerivationContext ForFaction(
            CARegionalPlan plan, CARegionalFactionPlan faction)
        {
            if (faction == null) return new CAPoliticalDerivationContext();
            string prefix = "faction " + faction.key + ": ";
            Faction resolved = faction.resolvedFaction;
            if (resolved == null && faction.source
                == CARegionalFactionSource.ExistingWorldFaction)
                resolved = CARegionalPlanUtility.FactionByLoadId(
                    faction.existingFactionLoadId);
            CAPoliticalDerivationContext context = FromFactionFacts(
                faction.ResolvedFactionDef, resolved,
                prefix + "realized facts");
            foreach (CAAxisDef axis in CAFactionAxes.Axes)
            {
                string actual = CAFactionAxes.KeyOf(
                    faction.factionStructure, axis.Key);
                if (actual != null)
                    context.Score(axis.Key, actual, 24,
                        prefix + "realized structure");
            }
            ScoreMeaning(context, faction.culture,
                CASocialSubjectRegistry.PublicVoice,
                CAFactionAxes.Participation, "universal", "standing", prefix);
            ScoreMeaning(context, faction.culture,
                CASocialSubjectRegistry.SharedProvision,
                CAFactionAxes.Support, "communal", "private", prefix);
            ScoreMeaning(context, faction.culture,
                CASocialSubjectRegistry.InheritedRank,
                CAFactionAxes.Status, "hereditary", "equal", prefix);
            ScoreMeaning(context, faction.culture,
                CASocialSubjectRegistry.QuarterGiven,
                CAFactionAxes.WarConduct, "quarter", "strength", prefix);
            return context;
        }

        internal static CAPoliticalDerivationContext ForFaction(Faction faction,
            CACulture culture, System.Collections.Generic.List<CAAxisEntry> structure)
        {
            string prefix = "world faction " + (faction?.Name ?? "unnamed")
                + ": ";
            CAPoliticalDerivationContext context = FromFactionFacts(
                faction?.def, faction, prefix + "realized facts");
            foreach (CAAxisDef axis in CAFactionAxes.Axes)
            {
                string actual = CAFactionAxes.KeyOf(structure, axis.Key);
                if (actual != null)
                    context.Score(axis.Key, actual, 24,
                        prefix + "realized structure");
            }
            ScoreMeaning(context, culture, CASocialSubjectRegistry.PublicVoice,
                CAFactionAxes.Participation, "universal", "standing", prefix);
            ScoreMeaning(context, culture, CASocialSubjectRegistry.SharedProvision,
                CAFactionAxes.Support, "communal", "private", prefix);
            return context;
        }

        private static CAPoliticalDerivationContext FromFactionFacts(
            FactionDef def, Faction faction, string source)
        {
            if (def == null) return new CAPoliticalDerivationContext();
            return CAPoliticalEvidenceContext.FromFacts(
                new CAPoliticalEvidenceFacts
                {
                    Source = source,
                    OpenRecruitment = def.rescueesCanJoin,
                    HereditaryStatus =
                        def.royalTitleInheritanceWorkerClass != null
                });
        }

        private static void ScoreMeaning(CAPoliticalDerivationContext context,
            CACulture culture, string subject, string axis, string positive,
            string negative, string prefix)
        {
            CACulturalMeaningResolution meaning = CACultureModel.Resolve(
                culture, subject);
            if (meaning.Contributions.Count == 0 || meaning.Approval == 0)
                return;
            int amount = System.Math.Max(2,
                System.Math.Abs(meaning.Approval) / 5);
            context.Score(axis, meaning.Approval > 0 ? positive : negative,
                amount, prefix + subject + " cultural approval "
                    + meaning.Approval);
        }
    }
}
