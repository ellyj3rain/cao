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

    internal sealed class CACultureRuntimeContext
    {
        internal CACulture Culture;
        internal string PopulationIdentity;
        internal string ReactionScopeIdentity;
        internal string InstitutionalOrganizationIdentity;
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
        private Dictionary<int, List<CASocialReactionRecord>> reactionsByPawn;
        private bool reactionIndexDirty = true;

        public CASocialReactionWorldComponent(World world) : base(world) { }

        internal static CASocialReactionWorldComponent Current =>
            Find.World?.GetComponent<CASocialReactionWorldComponent>();

        internal IReadOnlyList<CASocialReactionRecord> ReactionsForPawn(
            int pawnId)
        {
            EnsureReactionIndex();
            return reactionsByPawn.TryGetValue(pawnId,
                out List<CASocialReactionRecord> indexed)
                ? indexed : (IReadOnlyList<CASocialReactionRecord>)
                    System.Array.Empty<CASocialReactionRecord>();
        }

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
                reactionIndexDirty = true;
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
            CACultureRuntimeContext context,
            IEnumerable<CASocialContribution> otherContributions)
        {
            if (act == null || pawn == null || !act.Knows(pawn.thingIDNumber))
                return;
            int[] representedSources = act.UnansweredSourcePawnIdsFor(
                    pawn.thingIDNumber).ToArray();
            foreach (int sourcePawnId in representedSources)
            {
                CASocialFactContext fact;
                if (!CASocialActAdapter.TryMap(act, pawn, out fact))
                    return;
                fact.KnowledgeSource = act.KnowledgeSourceFor(
                    pawn.thingIDNumber, sourcePawnId);
                if (sourcePawnId >= 0)
                    fact.EpistemicSourceIdentity = sourcePawnId.ToString();
                RecordFact(fact, pawn, context, otherContributions);
                act.MarkKnowledgeSourceAnswered(pawn.thingIDNumber,
                    sourcePawnId);
            }
        }

        // Generic ingress for any loaded source owner. The owner registers its
        // subject and supplies a known factual event; this component persists
        // only the resulting interpretation. CAActRecord is one adapter, not
        // the social-subject universe.
        internal bool RecordFact(CASocialFactContext fact, Pawn pawn,
            CACultureRuntimeContext context,
            IEnumerable<CASocialContribution> otherContributions)
        {
            using (CAModuleProfiler.Measure(
                CAModuleProfileKey.SocialInterpretation))
            {
            if (pawn == null) return false;
            CACulture culture = context?.Culture;
            string populationIdentity = context?.PopulationIdentity;
            string reactionScopeIdentity = context?.ReactionScopeIdentity;
            string institutionalOrganizationIdentity =
                context?.InstitutionalOrganizationIdentity;
            string sourceWords = fact?.KnowledgeSource ?? "";
            bool directKnowledge = sourceWords.IndexOf("direct",
                    System.StringComparison.OrdinalIgnoreCase) >= 0
                || sourceWords.Equals("Firsthand",
                    System.StringComparison.OrdinalIgnoreCase)
                || sourceWords.Equals("Witnessed",
                    System.StringComparison.OrdinalIgnoreCase);
            string acquisitionChannel = directKnowledge
                ? "direct observation" : "testimony or report";
            CAPropositionKnowledgeWorldComponent.Current?.RecordObservedFact(
                fact, pawn,
                fact == null || fact.KnowledgeSource.NullOrEmpty()
                    ? "represented social source" : fact.KnowledgeSource,
                acquisitionChannel,
                fact?.EpistemicSourceIdentity,
                fact?.Tick ?? -1);
            CACulturalCognitionWorldComponent cognition =
                CACulturalCognitionWorldComponent.Current;
            CACulturalMeaningResolution culturalMeaning = cognition == null
                ? CACultureModel.Resolve(culture, fact?.SubjectKey,
                    populationIdentity)
                : cognition.ResolveFor(pawn, culture, fact?.SubjectKey,
                    populationIdentity, institutionalOrganizationIdentity);
            string reactionIdentity = fact?.FactIdentity;
            bool alreadyReacted = reactions.Any(value => value != null
                && value.factIdentity == reactionIdentity
                && value.pawnId == pawn.thingIDNumber);
            CAPersistedSocialReaction response =
                CASocialReactionPersistenceKernel.Record(
                    fact, pawn.thingIDNumber.ToString(), populationIdentity,
                    reactionScopeIdentity, InfluenceOf(pawn),
                    culturalMeaning, otherContributions,
                    reactions.Where(value => value != null).Select(value =>
                        value.factIdentity + "|" + value.pawnId));
            if (response == null && alreadyReacted)
            {
                CAModuleProfiler.Observe(CAModuleProfileKey.SocialInterpretation,
                    objectsExamined: 1, candidatesAccepted: 1);
                return true;
            }
            if (response == null) return false;
            var recorded = new CASocialReactionRecord
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
            };
            reactions.Add(recorded);
            IndexReaction(recorded);
            string exposedQuestion = CACultureQuestionRegistry
                .QuestionForSocialSubject(fact.SubjectKey);
            if (!exposedQuestion.NullOrEmpty()
                && int.TryParse(fact.ActorIdentity, out int sourcePawnId))
            {
                cognition?.RecordQuestionExposure(pawn, sourcePawnId,
                    exposedQuestion, fact.Tick);
                if (CAPoliticalEvidenceMap.TryFor(fact, out string axis,
                        out _, out _))
                    cognition?.RecordPoliticalExposure(pawn, sourcePawnId,
                        axis, fact.Tick);
            }
            CAInstitutionSanctionRuntime.Observe(fact, pawn,
                populationIdentity, institutionalOrganizationIdentity,
                response);
            if (!exposedQuestion.NullOrEmpty())
                cognition?.RefreshRepresentedEvidence(pawn,
                    exposedQuestion, fact.Tick, null,
                    institutionalOrganizationIdentity);
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
            int removed = reactions.RemoveAll(value => value == null
                || (value.tick >= 0 && now - value.tick > RetainTicks));
            if (removed > 0) reactionIndexDirty = true;
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

        private void EnsureReactionIndex()
        {
            if (!reactionIndexDirty && reactionsByPawn != null) return;
            reactionsByPawn = (reactions ?? new List<CASocialReactionRecord>())
                .Where(value => value != null && value.pawnId >= 0)
                .GroupBy(value => value.pawnId)
                .ToDictionary(group => group.Key, group => group
                    .OrderByDescending(value => value.tick).ToList());
            reactionIndexDirty = false;
        }

        private void IndexReaction(CASocialReactionRecord reaction)
        {
            if (reaction == null || reactionIndexDirty
                || reactionsByPawn == null) return;
            if (!reactionsByPawn.TryGetValue(reaction.pawnId,
                    out List<CASocialReactionRecord> indexed))
            {
                indexed = new List<CASocialReactionRecord>();
                reactionsByPawn.Add(reaction.pawnId, indexed);
            }
            int insert = indexed.FindIndex(value => value.tick < reaction.tick);
            if (insert < 0) indexed.Add(reaction);
            else indexed.Insert(insert, reaction);
        }

        internal static CACultureRuntimeContext ContextFor(Pawn pawn)
        {
            if (pawn == null) return null;
            Map map = pawn.MapHeld;
            if (pawn.Faction?.IsPlayer == true)
            {
                CACulture inherited = CAFactionStateWorldComponent.Current
                    ?.Find(pawn.Faction)?.culture;
                CACulture local = CACultureLongitudinalMapComponent.For(map)
                    ?.PlayerLocalCulture;
                CACulture culture = local ?? inherited;
                string populationIdentity = inherited?.id ?? culture?.id
                    ?? "player";
                // Social history belongs to the settlement that experienced
                // it. A faction-wide key would let one colony rewrite every
                // other colony's local Culture.
                string reactionScopeIdentity = local?.localityKey
                    ?? (map == null ? "player"
                        : "player-settlement:" + map.uniqueID);
                return new CACultureRuntimeContext
                {
                    Culture = culture,
                    PopulationIdentity = populationIdentity,
                    ReactionScopeIdentity = reactionScopeIdentity,
                    InstitutionalOrganizationIdentity = "player"
                };
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
                    string populationIdentity = plan.FactionPlan(factionKey)
                        ?.culture?.id ?? record.culture?.id
                        ?? "population:" + key;
                    string organizationIdentity = record.regionalId + "#"
                        + record.slot;
                    return new CACultureRuntimeContext
                    {
                        Culture = record.culture
                            ?? plan.FactionPlan(factionKey)?.culture,
                        PopulationIdentity = populationIdentity,
                        ReactionScopeIdentity = organizationIdentity,
                        InstitutionalOrganizationIdentity = organizationIdentity
                    };
                }

            CACulture factionCulture = CAFactionStateWorldComponent.Current
                ?.Find(pawn.Faction)?.culture;
            string fallbackPopulationIdentity = factionCulture?.id
                ?? "faction:" + (pawn.Faction?.loadID ?? -1);
            string fallbackOrganizationIdentity = CAViolenceSite.OrgKeyOf(pawn)
                ?? fallbackPopulationIdentity;
            return new CACultureRuntimeContext
            {
                Culture = factionCulture,
                PopulationIdentity = fallbackPopulationIdentity,
                ReactionScopeIdentity = fallbackOrganizationIdentity,
                InstitutionalOrganizationIdentity = fallbackOrganizationIdentity
            };
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
                EpistemicSourceIdentity = act.SourceIdentityFor(
                    knower.thingIDNumber),
                Known = true,
                Realization = realization,
                Tick = act.tick
            };
            return true;
        }
    }
}
