using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Political Order states what a population considers proper. Faction
    // structure records realized social order. This bridge compares the two
    // and projects only realized practice into organization customs.
    // Ideoligion remains a separate native system.
    internal static class CAPoliticalBeliefPractice
    {
        internal static CAPoliticalBeliefs PoliticalBeliefsOf(
            Faction faction)
        {
            return CAFactionStateWorldComponent.Current?.Find(faction)
                ?.politicalBeliefs;
        }

        internal static CAPoliticalBeliefs PoliticalBeliefsOf(Pawn pawn)
        {
            if (pawn == null) return null;
            Map map = pawn.MapHeld;
            CARegionalWorldComponent world = CARegionalWorldComponent.Current;
            CARegionalPlan region = world?.FindRegionForMap(map);
            if (region != null)
            {
                foreach (CARegionalSettlementRecord record in world.ForMap(map))
                {
                    string assigned = CAPopulationProjection
                        .AssignedPopulationGroupOf(record, pawn);
                    int populationGroupKey;
                    if (!int.TryParse(assigned, out populationGroupKey)) continue;
                    CASettlementPopulationGroup populationGroup = record.populationGroups?
                        .FirstOrDefault(item => item != null
                            && item.key == populationGroupKey);
                    if (populationGroup == null) continue;
                    int sourceKey = populationGroup.politicalBeliefsFactionKey >= 0
                        ? populationGroup.politicalBeliefsFactionKey
                        : populationGroup.factionKey;
                    CAPoliticalBeliefs sourced = region.FactionPlan(sourceKey)
                        ?.politicalBeliefs;
                    if (sourced != null) return sourced;
                    if (!populationGroup.politicalBeliefsId.NullOrEmpty())
                    {
                        sourced = region.factions
                            .Where(item => item?.politicalBeliefs != null)
                            .Select(item => item.politicalBeliefs)
                            .FirstOrDefault(item => item.id
                                == populationGroup.politicalBeliefsId);
                        if (sourced != null) return sourced;
                    }
                }
            }
            return PoliticalBeliefsOf(pawn.Faction);
        }

        private static IReadOnlyList<string> CurrentKeys(
            List<CAAxisEntry> structure,
            CAPoliticalBeliefs beliefs, string axis)
        {
            IReadOnlyList<string> current = CAFactionAxes.KeysOf(structure,
                axis);
            return current.Count > 0 ? current : CAFactionAxes.KeysOf(
                beliefs?.positions, axis);
        }

        internal static CAFoundingArrangement ShapeDefault(
            CAPoliticalBeliefs beliefs, List<CAAxisEntry> structure,
            CAFoundingArrangement situational)
        {
            if (situational == null) return null;
            CAFoundingArrangement shaped = situational.Copy();
            bool changed = false;

            IReadOnlyList<string> ownership = CurrentKeys(structure, beliefs,
                CAFactionAxes.Ownership);
            bool privateProperty = ownership.Contains("private");
            bool pooledProperty = ownership.Any(value => value == "common"
                || value == "cooperative" || value == "state");
            if (privateProperty && !pooledProperty)
            { shaped.sharedSupplies = false; changed = true; }
            else if (pooledProperty && !privateProperty)
            { shaped.sharedSupplies = true; changed = true; }

            IReadOnlyList<string> work = CurrentKeys(structure, beliefs,
                CAFactionAxes.Work);
            bool requiredWork = work.Contains("duty");
            bool voluntaryWork = work.Any(value => value == "contract"
                || value == "organized" || value == "household");
            if (requiredWork && !voluntaryWork)
            { shaped.workRequired = true; changed = true; }
            else if (voluntaryWork && !requiredWork)
            { shaped.workRequired = false; changed = true; }

            IReadOnlyList<string> participation = CurrentKeys(structure,
                beliefs,
                CAFactionAxes.Participation);
            bool broadParticipation = participation.Any(value =>
                value == "universal" || value == "members");
            bool restrictedParticipation = participation.Any(value =>
                value == "standing" || value == "heads");
            if (broadParticipation && !restrictedParticipation)
            { shaped.foundersDecide = true; changed = true; }
            else if (restrictedParticipation && !broadParticipation)
            { shaped.foundersDecide = false; changed = true; }

            IReadOnlyList<string> leadership = CurrentKeys(structure, beliefs,
                CAFactionAxes.Leadership);
            bool designatedLeader = leadership.Any(value =>
                value == "single" || value == "council");
            bool sharedLeadership = leadership.Any(value =>
                value == "whole" || value == "none");
            if (designatedLeader && !sharedLeadership)
            { shaped.leaderRule = "chosen"; changed = true; }
            else if (sharedLeadership && !designatedLeader)
            { shaped.leaderRule = "none"; changed = true; }

            if (shaped.leaderRule == "none" && !shaped.foundersDecide)
            { shaped.foundersDecide = true; changed = true; }

            if (!changed) return situational;
            shaped.id = situational.id + "+faction";
            shaped.premise = situational.premise
                + " The faction's rules set the open terms.";
            return shaped;
        }

        internal sealed class CAFoundingBeliefReading
        {
            public string title;
            public string belief;
            public string adopted;
            public bool conforms;
            public bool Silent { get { return belief == null; } }
        }

        internal static List<CAFoundingBeliefReading>
            ReadAgainstPoliticalBeliefs(CAPoliticalBeliefs beliefs,
                CAFoundingArrangement arrangement)
        {
            var rows = new List<CAFoundingBeliefReading>();
            if (arrangement == null) return rows;
            bool commands = arrangement.leaderRule == "chosen";

            IReadOnlyList<string> leadership = CAFactionAxes.KeysOf(
                beliefs?.positions,
                CAFactionAxes.Leadership);
            bool single = leadership.Contains("single");
            bool shared = leadership.Any(value => value == "whole"
                || value == "none");
            rows.Add(new CAFoundingBeliefReading
            {
                title = "Leadership",
                belief = BeliefWords(beliefs, CAFactionAxes.Leadership),
                adopted = commands ? "one founder decides"
                    : "the founders decide together",
                conforms = leadership.Count > 0
                    && (!single || commands) && (!shared || !commands)
                    && !(single && shared)
            });

            IReadOnlyList<string> work = CAFactionAxes.KeysOf(
                beliefs?.positions,
                CAFactionAxes.Work);
            bool duty = work.Contains("duty");
            bool voluntary = work.Any(value => value == "contract"
                || value == "organized" || value == "household");
            rows.Add(new CAFoundingBeliefReading
            {
                title = "Work",
                belief = BeliefWords(beliefs, CAFactionAxes.Work),
                adopted = arrangement.workRequired
                    ? "work may be assigned" : "work is voluntary",
                conforms = work.Count > 0
                    && (!duty || arrangement.workRequired)
                    && (!voluntary || !arrangement.workRequired)
                    && !(duty && voluntary)
            });

            IReadOnlyList<string> participation = CAFactionAxes.KeysOf(
                beliefs?.positions,
                CAFactionAxes.Participation);
            bool broadVote = participation.Any(value => value == "universal"
                || value == "members");
            bool narrowVote = participation.Any(value => value == "standing"
                || value == "heads");
            rows.Add(new CAFoundingBeliefReading
            {
                title = "Participation",
                belief = BeliefWords(beliefs, CAFactionAxes.Participation),
                adopted = arrangement.foundersDecide
                    ? "every founder votes" : "founders do not all vote",
                conforms = participation.Count > 0
                    && (!broadVote || arrangement.foundersDecide)
                    && (!narrowVote || !arrangement.foundersDecide)
                    && !(broadVote && narrowVote)
            });

            IReadOnlyList<string> ownership = CAFactionAxes.KeysOf(
                beliefs?.positions,
                CAFactionAxes.Ownership);
            bool privateProperty = ownership.Contains("private");
            bool pooled = ownership.Any(value => value == "common"
                || value == "cooperative" || value == "state");
            rows.Add(new CAFoundingBeliefReading
            {
                title = "Supplies",
                belief = BeliefWords(beliefs, CAFactionAxes.Ownership),
                adopted = arrangement.sharedSupplies
                    ? "supplies are pooled" : "each founder keeps their own",
                conforms = ownership.Count > 0
                    && (!privateProperty || !arrangement.sharedSupplies)
                    && (!pooled || arrangement.sharedSupplies)
                    && !(privateProperty && pooled)
            });
            return rows;
        }

        private static string BeliefWords(CAPoliticalBeliefs beliefs,
            string axisKey)
        {
            string words = string.Join("; ", CAFactionAxes.OptionsOf(
                beliefs?.positions, axisKey).Select(option => option.Words));
            return words.NullOrEmpty() ? null : words;
        }

        internal static IEnumerable<string> StandardsHeldBy(Pawn pawn)
        {
            var seen = new HashSet<string>();
            foreach (string standard in PoliticalStandards(
                PoliticalBeliefsOf(pawn)))
                if (seen.Add(standard)) yield return standard;
        }

        internal static bool HasPoliticalBeliefs(CAPoliticalBeliefs beliefs)
        {
            return beliefs?.positions != null && beliefs.positions.Any(entry =>
                entry != null && entry.source
                    != (byte)CAAxisSource.Unset);
        }

        // These keys are standards used to judge acts. They are beliefs about
        // proper conduct, not proof that an organization has adopted them.
        internal static IEnumerable<string> PoliticalStandards(
            CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null) yield break;
            if (CAFactionAxes.HasOption(beliefs.positions,
                    CAFactionAxes.Ownership, "private"))
                yield return "private holdings";
            if (CAFactionAxes.HasOption(beliefs.positions,
                    CAFactionAxes.Ownership, "common")
                || CAFactionAxes.HasOption(beliefs.positions,
                    CAFactionAxes.Ownership, "cooperative"))
                yield return "property in common";

            if (CAFactionAxes.HasOption(beliefs.positions,
                    CAFactionAxes.Work, "duty"))
                yield return "required work";
            if (CAFactionAxes.HasOption(beliefs.positions,
                    CAFactionAxes.Work, "contract")
                || CAFactionAxes.HasOption(beliefs.positions,
                    CAFactionAxes.Work, "organized"))
                yield return "voluntary work";

            if (CAFactionAxes.HasOption(beliefs.positions,
                    CAFactionAxes.Participation, "universal"))
                yield return "every voice counts";

            if (CAFactionAxes.HasOption(beliefs.positions,
                    CAFactionAxes.Leadership, "single"))
                yield return "single leader";
            if (CAFactionAxes.HasOption(beliefs.positions,
                    CAFactionAxes.Leadership, "whole")
                || CAFactionAxes.HasOption(beliefs.positions,
                    CAFactionAxes.Leadership, "none"))
                yield return "shared leadership";

            if (CAFactionAxes.HasOption(beliefs.positions,
                    CAFactionAxes.WarConduct, "strength"))
                yield return "victors decide";
            if (CAFactionAxes.HasOption(beliefs.positions,
                    CAFactionAxes.WarConduct, "quarter"))
                yield return "surrender accepted";
            if (CAFactionAxes.HasOption(beliefs.positions,
                    CAFactionAxes.WarConduct, "combatants"))
                yield return "combatants only";
        }

        // Organization customs describe current practice. They follow the
        // realized social order, never Political Order by itself.
        internal static void ReconcileCurrentStructure(CAOrganization org,
            List<CAAxisEntry> structure)
        {
            if (org == null) return;
            const string source = "social order";
            var desired = new HashSet<string>(
                StructureCustoms(structure));
            for (int i = org.customs.Count - 1; i >= 0; i--)
            {
                CAOrganizationCustom existing = org.customs[i];
                if (existing == null) continue;
                if (existing.source != source) continue;
                if (!desired.Contains(existing.key))
                    org.customs.RemoveAt(i);
            }
            foreach (string custom in desired)
                Seed(org, custom, source);
        }

        private static IEnumerable<string> StructureCustoms(
            List<CAAxisEntry> structure)
        {
            if (CAFactionAxes.HasOption(structure,
                    CAFactionAxes.Ownership, "private"))
                yield return "private holdings";
            if (CAFactionAxes.HasOption(structure,
                    CAFactionAxes.Ownership, "common")
                || CAFactionAxes.HasOption(structure,
                    CAFactionAxes.Ownership, "cooperative"))
                yield return "property in common";

            if (CAFactionAxes.HasOption(structure, CAFactionAxes.Work,
                    "duty")) yield return "required work";
            if (CAFactionAxes.HasOption(structure, CAFactionAxes.Work,
                    "contract") || CAFactionAxes.HasOption(structure,
                    CAFactionAxes.Work, "organized"))
                yield return "voluntary work";

            if (CAFactionAxes.HasOption(structure,
                    CAFactionAxes.Participation, "universal"))
                yield return "every voice counts";

            if (CAFactionAxes.HasOption(structure,
                    CAFactionAxes.Leadership, "single"))
                yield return "single leader";
            if (CAFactionAxes.HasOption(structure,
                    CAFactionAxes.Leadership, "whole")
                || CAFactionAxes.HasOption(structure,
                    CAFactionAxes.Leadership, "none"))
                yield return "shared leadership";
        }

        private static void Seed(CAOrganization org, string custom,
            string source)
        {
            if (org.HasCustom(custom)) return;
            org.customs.Add(new CAOrganizationCustom
            {
                key = custom,
                source = source,
                adoptedTick = Find.TickManager.TicksGame
            });
        }
    }
}
