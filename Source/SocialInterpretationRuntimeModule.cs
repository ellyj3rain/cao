using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    public sealed class CASocialReactionRecord : IExposable
    {
        public string subjectKey;
        public string factIdentity;
        public int pawnId = -1;
        public string populationIdentity;
        public string organizationIdentity;
        public int approval;
        public int prestige;
        public int surprise;
        public int salience;
        public string knowledgeSource;
        public int tick = -1;
        public int influenceWeight = 100;
        public bool internalContradiction;
        public string contributions;

        public void ExposeData()
        {
            Scribe_Values.Look(ref subjectKey, "subjectKey");
            Scribe_Values.Look(ref factIdentity, "factIdentity");
            Scribe_Values.Look(ref pawnId, "pawnId", -1);
            Scribe_Values.Look(ref populationIdentity, "populationIdentity");
            Scribe_Values.Look(ref organizationIdentity,
                "organizationIdentity");
            Scribe_Values.Look(ref approval, "approval", 0);
            Scribe_Values.Look(ref prestige, "prestige", 0);
            Scribe_Values.Look(ref surprise, "surprise", 0);
            Scribe_Values.Look(ref salience, "salience", 0);
            Scribe_Values.Look(ref knowledgeSource, "knowledgeSource");
            Scribe_Values.Look(ref tick, "tick", -1);
            Scribe_Values.Look(ref influenceWeight, "influenceWeight", 100);
            Scribe_Values.Look(ref internalContradiction,
                "internalContradiction", false);
            Scribe_Values.Look(ref contributions, "contributions");
        }

        internal CAPawnSocialInterpretation ToInterpretation()
        {
            return new CAPawnSocialInterpretation
            {
                SubjectKey = subjectKey,
                FactIdentity = factIdentity,
                PawnIdentity = pawnId.ToString(),
                PopulationIdentity = populationIdentity,
                OrganizationIdentity = organizationIdentity,
                Approval = approval,
                Prestige = prestige,
                Surprise = surprise,
                Salience = salience,
                KnowledgeSource = knowledgeSource,
                Tick = tick,
                InfluenceWeight = influenceWeight,
                InternalContradiction = internalContradiction
            };
        }
    }

    // This world component persists interpretations of facts already owned by
    // authoritative ledgers. It is not a second act ledger and cannot create
    // knowledge. Reactions are keyed by existing act identity and informed pawn.
    public sealed class CASocialReactionWorldComponent : WorldComponent
    {
        private const int EvidenceRunGap = 2 * 60000;
        private const int RetainTicks = 30 * 60000;
        private int campaignSchemaVersion =
            CACampaignCompatibilityKernel.CurrentBoundaryVersion;
        private int legacyAuthoringDataEpoch =
            CACampaignCompatibilityKernel.LegacyB10AuthoringEpoch;
        private List<CASocialReactionRecord> reactions =
            new List<CASocialReactionRecord>();

        public CASocialReactionWorldComponent(World world) : base(world) { }

        internal static CASocialReactionWorldComponent Current =>
            Find.World?.GetComponent<CASocialReactionWorldComponent>();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref campaignSchemaVersion,
                "CA_socialReactionSchemaVersion", 0);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref legacyAuthoringDataEpoch,
                    "CA_authoringDataEpoch", 0);
            bool readable = CACampaignCompatibility.ShouldReadLiveState(
                "world.social-reactions", campaignSchemaVersion,
                legacyAuthoringDataEpoch);
            if (readable)
                Scribe_Collections.Look(ref reactions, "CA_socialReactions",
                    LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && readable)
            {
                CACampaignCompatibility.CompleteOwnerLoad(
                    "world.social-reactions", ref campaignSchemaVersion,
                    legacyAuthoringDataEpoch, ValidateCampaignState);
            }
            base.ExposeData();
        }

        private string ValidateCampaignState()
        {
            if (reactions == null)
                return "social-reaction owner collection is missing";
            var identities = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < reactions.Count; i++)
            {
                CASocialReactionRecord reaction = reactions[i];
                if (reaction == null)
                    return "social reaction " + i + " is null";
                if (reaction.factIdentity.NullOrEmpty() || reaction.pawnId < 0)
                    return "social reaction " + i + " has no fact or pawn";
                string key = reaction.factIdentity + "|" + reaction.pawnId;
                if (!identities.Add(key))
                    return "social reaction " + key + " is duplicated";
            }
            return null;
        }

        internal void RecordAct(CAActRecord act, Pawn pawn,
            CACulture culture, string populationIdentity,
            string organizationIdentity,
            IEnumerable<CASocialContribution> otherContributions)
        {
            if (act == null || pawn == null || !act.Knows(pawn.thingIDNumber))
                return;
            CASocialFactContext fact;
            if (!CASocialActAdapter.TryMap(act, pawn, out fact))
                return;
            RecordFact(fact, pawn, culture, populationIdentity,
                organizationIdentity, otherContributions);
        }

        // Generic ingress for any loaded source owner. The owner registers its
        // subject and supplies a known factual event; this component persists
        // only the resulting interpretation. CAActRecord is one adapter, not
        // the social-subject universe.
        public bool RecordFact(CASocialFactContext fact, Pawn pawn,
            CACulture culture, string populationIdentity,
            string organizationIdentity,
            IEnumerable<CASocialContribution> otherContributions)
        {
            using (CAModuleProfiler.Measure(
                CAModuleProfileKey.SocialInterpretation))
            {
            if (pawn == null) return false;
            CAPersistedSocialReaction response =
                CASocialReactionPersistenceKernel.Record(
                    fact, pawn.thingIDNumber.ToString(), populationIdentity,
                    organizationIdentity, InfluenceOf(pawn),
                    CACultureModel.Resolve(culture, fact?.SubjectKey,
                        populationIdentity), otherContributions,
                    reactions.Where(value => value != null).Select(value =>
                        value.factIdentity + "|" + value.pawnId));
            if (response == null)
            {
                CAModuleProfiler.Observe(
                    CAModuleProfileKey.SocialInterpretation,
                    objectsExamined: 1, workSkippedOrDeferred: 1);
                return false;
            }
            reactions.Add(new CASocialReactionRecord
            {
                subjectKey = response.SubjectKey,
                factIdentity = response.FactIdentity,
                pawnId = pawn.thingIDNumber,
                populationIdentity = response.PopulationIdentity,
                organizationIdentity = response.OrganizationIdentity,
                approval = response.Approval,
                prestige = response.Prestige,
                surprise = response.Surprise,
                salience = response.Salience,
                knowledgeSource = response.KnowledgeSource,
                tick = response.Tick,
                influenceWeight = response.InfluenceWeight,
                internalContradiction = response.InternalContradiction,
                contributions = response.Contributions
            });
            CAModuleProfiler.Observe(
                CAModuleProfileKey.SocialInterpretation,
                objectsExamined: 1, candidatesAccepted: 1);
            return true;
            }
        }

        internal List<CASocialGroupPattern> PatternsFor(
            string organizationIdentity, int eligiblePopulation, int now)
        {
            using (CAModuleProfiler.Measure(
                CAModuleProfileKey.SocialAggregation))
            {
            reactions.RemoveAll(value => value == null || (value.tick >= 0
                && now - value.tick > RetainTicks));
            var currentRuns = new List<CASocialReactionRecord>();
            foreach (IGrouping<string, CASocialReactionRecord> group in reactions
                .Where(value => value.organizationIdentity
                    == organizationIdentity)
                .GroupBy(value => value.subjectKey + "|"
                    + (value.populationIdentity ?? "all")))
            {
                List<CASocialReactionRecord> ordered = group
                    .OrderBy(value => value.tick)
                    .ThenBy(value => value.factIdentity)
                    .ThenBy(value => value.pawnId).ToList();
                int runStart = 0;
                for (int index = 1; index < ordered.Count; index++)
                    if (ordered[index].tick - ordered[index - 1].tick
                        > EvidenceRunGap) runStart = index;
                currentRuns.AddRange(ordered.Skip(runStart));
            }
            List<CASocialGroupPattern> patterns = CASocialPatternKernel
                .Aggregate(currentRuns.Select(value =>
                    value.ToInterpretation()), eligiblePopulation);
            CAModuleProfiler.Observe(
                CAModuleProfileKey.SocialAggregation,
                objectsExamined: reactions.Count,
                candidatesAccepted: patterns.Count);
            return patterns;
            }
        }

        private static int InfluenceOf(Pawn pawn)
        {
            int weight = 100;
            if (pawn?.skills?.GetSkill(SkillDefOf.Social)?.Level >= 10)
                weight += 25;
            if (pawn?.Faction?.leader == pawn) weight += 50;
            CAOrganizationWorldComponent organizations =
                CAOrganizationWorldComponent.Current;
            if (organizations != null && pawn != null)
                foreach (CAOrganization organization in organizations.Organizations)
                {
                    if (organization?.offices == null) continue;
                    CAOffice office = organization.offices.FirstOrDefault(value =>
                        value != null && value.holderId == pawn.thingIDNumber);
                    if (office != null)
                    {
                        weight += Mathf.Clamp(office.seniority / 20, 10, 60);
                        break;
                    }
                }
            return weight;
        }

        internal static CACulture CultureFor(Pawn pawn,
            out string populationIdentity, out string organizationIdentity)
        {
            populationIdentity = null;
            organizationIdentity = null;
            if (pawn == null) return null;
            Map map = pawn.MapHeld;
            if (pawn.Faction?.IsPlayer == true)
            {
                CACulture inherited = CAFactionStateWorldComponent.Current
                    ?.Find(pawn.Faction)?.culture;
                CACulture local = CACultureLongitudinalMapComponent.For(map)
                    ?.PlayerLocalCulture;
                CACulture culture = local ?? inherited;
                populationIdentity = inherited?.id ?? culture?.id ?? "player";
                // Social history belongs to the settlement that experienced
                // it. A faction-wide key would let one colony rewrite every
                // other colony's local Culture.
                organizationIdentity = local?.localityKey
                    ?? (map == null ? "player"
                        : "player-settlement:" + map.uniqueID);
                return culture;
            }

            CARegionalWorldComponent regional = CARegionalWorldComponent.Current;
            CARegionalPlan plan = regional?.FindRegionForMap(map);
            if (regional != null && plan != null)
                foreach (CARegionalSettlementRecord record in regional.ForMap(map))
                {
                    string assigned = CAPopulationProjection
                        .AssignedPopulationGroupOf(record, pawn);
                    int key;
                    if (!int.TryParse(assigned, out key)) continue;
                    CASettlementPopulationGroup group = record.populationGroups?
                        .FirstOrDefault(value => value != null
                            && value.key == key);
                    int factionKey = group?.factionKey >= 0
                        ? group.factionKey : record.factionKey;
                    populationIdentity = plan.FactionPlan(factionKey)
                        ?.culture?.id ?? record.culture?.id
                        ?? "population:" + key;
                    organizationIdentity = record.regionalId + "#" + record.slot;
                    return record.culture ?? plan.FactionPlan(factionKey)?.culture;
                }

            CACulture factionCulture = CAFactionStateWorldComponent.Current
                ?.Find(pawn.Faction)?.culture;
            populationIdentity = factionCulture?.id
                ?? "faction:" + (pawn.Faction?.loadID ?? -1);
            organizationIdentity = CAViolenceSite.OrgKeyOf(pawn)
                ?? populationIdentity;
            return factionCulture;
        }
    }

    // Act-specific mapping lives beside the act source adapter. Other source
    // owners call RecordFact with their own registered factual contract.
    internal static class CASocialActAdapter
    {
        internal static bool TryMap(CAActRecord act, Pawn knower,
            out CASocialFactContext fact)
        {
            fact = null;
            if (act == null || knower == null
                || !act.Knows(knower.thingIDNumber)) return false;
            int realization = 1;
            string subject = null;
            string key = (act.act ?? "").ToLowerInvariant();
            if (key == "compelled-work" || key.Contains("forced-work"))
                subject = CASocialSubjectRegistry.CompelledService;
            else if (key.Contains("coerc"))
                subject = CASocialSubjectRegistry.EnforcedOrder;
            else if (key.Contains("tax") || key.Contains("confisc")
                || key.Contains("requisition") || key.Contains("seiz"))
                subject = CASocialSubjectRegistry.CompulsoryTransfer;
            else if (key.Contains("custod") || key.Contains("prison")
                || key.Contains("capture") || key.Contains("execut"))
            {
                realization = key.Contains("harm") || key.Contains("execut")
                    || key.Contains("neglect") ? -1 : 1;
                subject = CASocialSubjectRegistry.HumaneCustody;
            }
            else if (key == "violence"
                && (act.circumstance == CAViolenceSite.Downed
                    || act.circumstance == CAViolenceSite.Unresisting))
            {
                realization = -1;
                subject = CASocialSubjectRegistry.QuarterGiven;
            }
            else if (key.Contains("surrender") || key.Contains("quarter"))
            {
                realization = key.Contains("refus") ? -1 : 1;
                subject = CASocialSubjectRegistry.QuarterGiven;
            }
            else if (key.Contains("provision") || key.Contains("food")
                || key.Contains("support"))
            {
                realization = key.Contains("refus") || key.Contains("withheld")
                    ? -1 : 1;
                subject = CASocialSubjectRegistry.SharedProvision;
            }
            if (subject == null || CASocialSubjectRegistry.Find(subject) == null)
                return false;
            fact = new CASocialFactContext
            {
                SubjectKey = subject,
                FactIdentity = "act:" + act.id,
                ActorIdentity = act.actorPawnId.ToString(),
                TargetIdentity = act.subjectPawnId.ToString(),
                OrganizationIdentity = act.orgKey,
                KnowledgeSource = act.HowKnown(knower.thingIDNumber).ToString(),
                Known = true,
                Realization = realization,
                Tick = act.tick
            };
            return true;
        }
    }
}
