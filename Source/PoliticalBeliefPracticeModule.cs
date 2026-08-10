using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Political Beliefs state what a population considers proper. Faction
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

        private static string CurrentKey(List<CAAxisEntry> structure,
            CAPoliticalBeliefs beliefs, string axis)
        {
            return CAFactionAxes.KeyOf(structure, axis)
                ?? CAFactionAxes.KeyOf(beliefs?.positions, axis);
        }

        internal static CAFoundingArrangement ShapeDefault(
            CAPoliticalBeliefs beliefs, List<CAAxisEntry> structure,
            CAFoundingArrangement situational)
        {
            if (situational == null) return null;
            CAFoundingArrangement shaped = situational.Copy();
            bool changed = false;

            string ownership = CurrentKey(structure, beliefs,
                CAFactionAxes.Ownership);
            if (ownership == "private")
            { shaped.sharedSupplies = false; changed = true; }
            else if (ownership == "common" || ownership == "cooperative"
                || ownership == "state")
            { shaped.sharedSupplies = true; changed = true; }

            string work = CurrentKey(structure, beliefs,
                CAFactionAxes.Work);
            if (work == "duty")
            { shaped.workRequired = true; changed = true; }
            else if (work == "contract" || work == "organized"
                || work == "household")
            { shaped.workRequired = false; changed = true; }

            string participation = CurrentKey(structure, beliefs,
                CAFactionAxes.Participation);
            if (participation == "universal" || participation == "members")
            { shaped.foundersDecide = true; changed = true; }
            else if (participation == "standing" || participation == "heads")
            { shaped.foundersDecide = false; changed = true; }

            string leadership = CurrentKey(structure, beliefs,
                CAFactionAxes.Leadership);
            if (leadership == "single" || leadership == "council")
            { shaped.leaderRule = "chosen"; changed = true; }
            else if (leadership == "whole" || leadership == "none")
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

            string leadership = CAFactionAxes.KeyOf(beliefs?.positions,
                CAFactionAxes.Leadership);
            rows.Add(new CAFoundingBeliefReading
            {
                title = "Leadership",
                belief = leadership == "single"
                    ? "one leader should hold final authority"
                    : leadership == "whole" || leadership == "none"
                        ? "no permanent leader should hold final authority"
                        : leadership == "council" || leadership == "federated"
                            ? "leadership should be shared" : null,
                adopted = commands ? "one founder decides"
                    : "the founders decide together",
                conforms = leadership == "single" ? commands
                    : leadership == "whole" || leadership == "none"
                        ? !commands : false
            });

            string work = CAFactionAxes.KeyOf(beliefs?.positions,
                CAFactionAxes.Work);
            rows.Add(new CAFoundingBeliefReading
            {
                title = "Work",
                belief = work == "duty" ? "members owe required service"
                    : work == "contract" || work == "organized"
                        || work == "household"
                        ? "work should not be assigned by faction leaders"
                        : null,
                adopted = arrangement.workRequired
                    ? "work may be assigned" : "work is voluntary",
                conforms = work == "duty" ? arrangement.workRequired
                    : !arrangement.workRequired
            });

            string participation = CAFactionAxes.KeyOf(beliefs?.positions,
                CAFactionAxes.Participation);
            bool broadVote = participation == "universal"
                || participation == "members";
            rows.Add(new CAFoundingBeliefReading
            {
                title = "Participation",
                belief = participation == "universal"
                    ? "all residents should take part"
                    : participation == "members"
                        ? "all faction members should take part"
                        : participation == "standing"
                            ? "participation should require standing"
                            : participation == "heads"
                                ? "households should hold one voice" : null,
                adopted = arrangement.foundersDecide
                    ? "every founder votes" : "founders do not all vote",
                conforms = broadVote ? arrangement.foundersDecide
                    : !arrangement.foundersDecide
            });

            string ownership = CAFactionAxes.KeyOf(beliefs?.positions,
                CAFactionAxes.Ownership);
            bool pooled = ownership == "common" || ownership == "cooperative"
                || ownership == "state";
            rows.Add(new CAFoundingBeliefReading
            {
                title = "Supplies",
                belief = ownership == "private"
                    ? "personal property should remain private"
                    : pooled ? "essential supplies should be pooled"
                        : ownership == "mixed"
                            ? "private and shared ownership should coexist"
                            : null,
                adopted = arrangement.sharedSupplies
                    ? "supplies are pooled" : "each founder keeps their own",
                conforms = ownership == "private"
                    ? !arrangement.sharedSupplies
                    : pooled && arrangement.sharedSupplies
            });
            return rows;
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
            string ownership = CAFactionAxes.KeyOf(beliefs.positions,
                CAFactionAxes.Ownership);
            if (ownership == "private") yield return "private holdings";
            else if (ownership == "common" || ownership == "cooperative")
                yield return "property in common";

            string work = CAFactionAxes.KeyOf(beliefs.positions,
                CAFactionAxes.Work);
            if (work == "duty") yield return "required work";
            else if (work == "contract" || work == "organized")
                yield return "voluntary work";

            string participation = CAFactionAxes.KeyOf(beliefs.positions,
                CAFactionAxes.Participation);
            if (participation == "universal")
                yield return "every voice counts";

            string leadership = CAFactionAxes.KeyOf(beliefs.positions,
                CAFactionAxes.Leadership);
            if (leadership == "single") yield return "single leader";
            else if (leadership == "whole" || leadership == "none")
                yield return "shared leadership";

            string warConduct = CAFactionAxes.KeyOf(beliefs.positions,
                CAFactionAxes.WarConduct);
            if (warConduct == "strength") yield return "victors decide";
            else if (warConduct == "quarter")
                yield return "surrender accepted";
            else if (warConduct == "combatants")
                yield return "combatants only";
        }

        // Organization customs describe current practice. They follow the
        // realized social order, never political beliefs by themselves.
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
            string ownership = CAFactionAxes.KeyOf(structure,
                CAFactionAxes.Ownership);
            if (ownership == "private") yield return "private holdings";
            else if (ownership == "common" || ownership == "cooperative")
                yield return "property in common";

            string work = CAFactionAxes.KeyOf(structure,
                CAFactionAxes.Work);
            if (work == "duty") yield return "required work";
            else if (work == "contract" || work == "organized")
                yield return "voluntary work";

            string participation = CAFactionAxes.KeyOf(structure,
                CAFactionAxes.Participation);
            if (participation == "universal")
                yield return "every voice counts";

            string leadership = CAFactionAxes.KeyOf(structure,
                CAFactionAxes.Leadership);
            if (leadership == "single") yield return "single leader";
            else if (leadership == "whole" || leadership == "none")
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
