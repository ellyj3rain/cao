using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    public sealed class CAKnowledgePropositionRecord : IExposable
    {
        public string identity;
        public string topic;
        public string claim;
        public string content;
        public string holderIdentity;
        public string sourceIdentity;
        public string sourceType;
        public string acquisitionChannel;
        public string provenance;
        public List<string> provenanceChain = new List<string>();
        public float confidence;
        public List<string> evidence = new List<string>();
        public List<string> contradictions = new List<string>();
        public List<string> corroboratingSources = new List<string>();
        public string access;
        public float transmissibility;
        public float sourceReliability = 0.5f;
        public float sourceTrust = 0.5f;
        public float expertise = 0.5f;
        public float prestige = 0.5f;
        public float authority = 0.5f;
        public float motiveIntegrity = 0.5f;
        public float corroboration = 0.5f;
        public float plausibility = 0.5f;
        public float priorCongruence = 0.5f;
        public float methodQuality = 0.5f;
        public float observedPayoff = 0.5f;
        public float uncontestedConflictFreedom = 1f;
        public float conflictFreedom = 0.5f;
        public float epistemicVigilance = 0.5f;
        public float noveltyAcceptance;
        public string politicalAxisKey;
        public string politicalOptionKey;
        public float politicalSupport;
        public int acquiredTick = -1;
        public int lastConfirmedTick = -1;
        public int lastDecayTick = -1;
        public float decayRate;
        public string custodianIdentity;

        public void ExposeData()
        {
            Scribe_Values.Look(ref identity, "identity");
            Scribe_Values.Look(ref topic, "topic");
            Scribe_Values.Look(ref claim, "claim");
            Scribe_Values.Look(ref content, "content");
            Scribe_Values.Look(ref holderIdentity, "holderIdentity");
            Scribe_Values.Look(ref sourceIdentity, "sourceIdentity");
            Scribe_Values.Look(ref sourceType, "sourceType");
            Scribe_Values.Look(ref acquisitionChannel,
                "acquisitionChannel");
            Scribe_Values.Look(ref provenance, "provenance");
            Scribe_Collections.Look(ref provenanceChain,
                "provenanceChain", LookMode.Value);
            Scribe_Values.Look(ref confidence, "confidence", 0f);
            Scribe_Collections.Look(ref evidence, "evidence", LookMode.Value);
            Scribe_Collections.Look(ref contradictions, "contradictions",
                LookMode.Value);
            Scribe_Collections.Look(ref corroboratingSources,
                "corroboratingSources", LookMode.Value);
            Scribe_Values.Look(ref access, "access");
            Scribe_Values.Look(ref transmissibility,
                "transmissibility", 0f);
            Scribe_Values.Look(ref sourceReliability,
                "sourceReliability", 0.5f);
            Scribe_Values.Look(ref sourceTrust, "sourceTrust", 0.5f);
            Scribe_Values.Look(ref expertise, "expertise", 0.5f);
            Scribe_Values.Look(ref prestige, "prestige", 0.5f);
            Scribe_Values.Look(ref authority, "authority", 0.5f);
            Scribe_Values.Look(ref motiveIntegrity,
                "motiveIntegrity", 0.5f);
            Scribe_Values.Look(ref corroboration,
                "corroboration", 0.5f);
            Scribe_Values.Look(ref plausibility, "plausibility", 0.5f);
            Scribe_Values.Look(ref priorCongruence,
                "priorCongruence", 0.5f);
            Scribe_Values.Look(ref methodQuality,
                "methodQuality", 0.5f);
            Scribe_Values.Look(ref observedPayoff,
                "observedPayoff", 0.5f);
            Scribe_Values.Look(ref uncontestedConflictFreedom,
                "uncontestedConflictFreedom", 1f);
            Scribe_Values.Look(ref conflictFreedom,
                "conflictFreedom", 0.5f);
            Scribe_Values.Look(ref epistemicVigilance,
                "epistemicVigilance", 0.5f);
            Scribe_Values.Look(ref noveltyAcceptance,
                "noveltyAcceptance", 0f);
            Scribe_Values.Look(ref politicalAxisKey,
                "politicalAxisKey");
            Scribe_Values.Look(ref politicalOptionKey,
                "politicalOptionKey");
            Scribe_Values.Look(ref politicalSupport,
                "politicalSupport", 0f);
            Scribe_Values.Look(ref acquiredTick, "acquiredTick", -1);
            Scribe_Values.Look(ref lastConfirmedTick,
                "lastConfirmedTick", -1);
            Scribe_Values.Look(ref lastDecayTick, "lastDecayTick", -1);
            Scribe_Values.Look(ref decayRate, "decayRate", 0f);
            Scribe_Values.Look(ref custodianIdentity,
                "custodianIdentity");
        }
    }

    public sealed class CAResearchProgramReceipt : IExposable
    {
        public string identity;
        public string projectKey;
        public List<string> priorKnowledgeKeys = new List<string>();
        public List<int> skilledPawnIds = new List<int>();
        public string institutionIdentity;
        public string authorityBasis;
        public string method;
        public string facilityIdentity;
        public string materialBasis;
        public int startedTick = -1;
        public int completedTick = -1;
        public int collaborationCount;
        public List<string> evidenceKeys = new List<string>();
        public string evaluation;
        public string preservation;
        public string dissemination;
        public string behaviorKey;

        public void ExposeData()
        {
            Scribe_Values.Look(ref identity, "identity");
            Scribe_Values.Look(ref projectKey, "projectKey");
            Scribe_Collections.Look(ref priorKnowledgeKeys,
                "priorKnowledgeKeys", LookMode.Value);
            Scribe_Collections.Look(ref skilledPawnIds,
                "skilledPawnIds", LookMode.Value);
            Scribe_Values.Look(ref institutionIdentity,
                "institutionIdentity");
            Scribe_Values.Look(ref authorityBasis, "authorityBasis");
            Scribe_Values.Look(ref method, "method");
            Scribe_Values.Look(ref facilityIdentity, "facilityIdentity");
            Scribe_Values.Look(ref materialBasis, "materialBasis");
            Scribe_Values.Look(ref startedTick, "startedTick", -1);
            Scribe_Values.Look(ref completedTick, "completedTick", -1);
            Scribe_Values.Look(ref collaborationCount,
                "collaborationCount", 0);
            Scribe_Collections.Look(ref evidenceKeys, "evidenceKeys",
                LookMode.Value);
            Scribe_Values.Look(ref evaluation, "evaluation");
            Scribe_Values.Look(ref preservation, "preservation");
            Scribe_Values.Look(ref dissemination, "dissemination");
            Scribe_Values.Look(ref behaviorKey, "behaviorKey");
        }
    }

    // Proposition knowledge is not Culture and is not the actor-private
    // tactical contact store. It owns durable claims, provenance, access,
    // contradiction, institutional custody, and represented research lineage.
    public sealed class CAPropositionKnowledgeWorldComponent : WorldComponent
    {
        public const int CurrentSchemaVersion = 1;
        private int campaignSchemaVersion =
            CACampaignCompatibilityKernel.CurrentBoundaryVersion;
        private int schemaVersion = CurrentSchemaVersion;
        private int nextKnowledgeTick = 60000;
        private int nextLongTick = 600000;
        private List<CAKnowledgePropositionRecord> propositions =
            new List<CAKnowledgePropositionRecord>();
        private List<CAResearchProgramReceipt> researchPrograms =
            new List<CAResearchProgramReceipt>();
        private Dictionary<string, CAKnowledgePropositionRecord>
            propositionByIdentity;
        private Dictionary<string, List<CAKnowledgePropositionRecord>>
            propositionsByTopic;
        private Dictionary<string, List<CAKnowledgePropositionRecord>>
            politicalPropositionsByHolderAxis;
        private Dictionary<string, CAResearchProgramReceipt>
            researchByIdentity;

        public CAPropositionKnowledgeWorldComponent(World world) : base(world)
        { }

        internal static CAPropositionKnowledgeWorldComponent Current =>
            Find.World?.GetComponent<CAPropositionKnowledgeWorldComponent>();

        internal IReadOnlyList<CAKnowledgePropositionRecord> Propositions =>
            propositions;
        internal IReadOnlyList<CAResearchProgramReceipt> ResearchPrograms =>
            researchPrograms;

        internal IReadOnlyList<CAKnowledgePropositionRecord>
            PoliticalPropositions(string holderIdentity, string axisKey)
        {
            if (holderIdentity.NullOrEmpty() || axisKey.NullOrEmpty())
                return Array.Empty<CAKnowledgePropositionRecord>();
            EnsureIndexes();
            return politicalPropositionsByHolderAxis.TryGetValue(
                holderIdentity + "\0" + axisKey,
                out List<CAKnowledgePropositionRecord> values)
                ? values : Array.Empty<CAKnowledgePropositionRecord>();
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref campaignSchemaVersion,
                "CA_propositionKnowledgeOwnerVersion",
                0, forceSave: true);
            bool readable = CACampaignCompatibility.ShouldReadLiveState(
                "world.proposition-knowledge", campaignSchemaVersion, 0);
            if (readable)
            {
                Scribe_Values.Look(ref schemaVersion,
                    "CA_propositionKnowledgeSchemaVersion",
                    CurrentSchemaVersion);
                Scribe_Values.Look(ref nextKnowledgeTick,
                    "CA_propositionKnowledgeNextTick", 60000);
                Scribe_Values.Look(ref nextLongTick,
                    "CA_propositionKnowledgeNextLongTick", 600000);
                Scribe_Collections.Look(ref propositions,
                    "CA_knowledgePropositions", LookMode.Deep);
                Scribe_Collections.Look(ref researchPrograms,
                    "CA_researchProgramReceipts", LookMode.Deep);
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && readable)
            {
                CACampaignCompatibility.CompleteOwnerLoad(
                    "world.proposition-knowledge",
                    ref campaignSchemaVersion, 0, ValidateCampaignState,
                    BootstrapMissingState);
                RebuildIndexes();
            }
            base.ExposeData();
        }

        private string BootstrapMissingState()
        {
            if (schemaVersion == 0) schemaVersion = CurrentSchemaVersion;
            if (propositions == null)
                propositions = new List<CAKnowledgePropositionRecord>();
            if (researchPrograms == null)
                researchPrograms = new List<CAResearchProgramReceipt>();
            return ValidateCampaignState();
        }

        private string ValidateCampaignState()
        {
            if (schemaVersion != CurrentSchemaVersion)
                return "proposition-knowledge schema is " + schemaVersion
                    + ", expected " + CurrentSchemaVersion;
            if (propositions == null || researchPrograms == null)
                return "proposition-knowledge collection is missing";
            if (propositions.Count
                    > CACulturalCognitionPureKernel.MaxKnowledgePropositions
                || propositions.GroupBy(value => value?.topic,
                        StringComparer.Ordinal).Any(group => group.Count()
                    > CACulturalCognitionPureKernel.MaxKnowledgePerTopic))
                return "knowledge proposition retention bound is exceeded";
            if (researchPrograms.Count
                > CACulturalCognitionPureKernel.MaxResearchPrograms)
                return "research program retention bound is exceeded";
            if (propositions.Any(value => value == null
                    || value.identity.NullOrEmpty()
                    || value.topic.NullOrEmpty() || value.claim.NullOrEmpty()
                    || value.holderIdentity.NullOrEmpty()
                    || value.sourceIdentity.NullOrEmpty()
                    || value.sourceType.NullOrEmpty()
                    || value.acquisitionChannel.NullOrEmpty()
                    || value.provenanceChain == null
                    || value.evidence == null || value.contradictions == null
                    || value.corroboratingSources == null
                    || value.provenanceChain.Count
                        > CACulturalCognitionPureKernel.MaxKnowledgeProvenance
                    || value.evidence.Count
                        > CACulturalCognitionPureKernel.MaxKnowledgeEvidence
                    || value.contradictions.Count
                        > CACulturalCognitionPureKernel
                            .MaxKnowledgeContradictions
                    || value.corroboratingSources.Count
                        > CACulturalCognitionPureKernel.MaxKnowledgeProvenance
                    || value.provenanceChain.Any(item => item == null)
                    || value.evidence.Any(item => item == null)
                    || value.contradictions.Any(item => item == null)
                    || value.corroboratingSources.Any(item => item == null)
                    || !Unit(value.confidence)
                    || !Unit(value.transmissibility)
                    || !Unit(value.sourceReliability)
                    || !Unit(value.sourceTrust) || !Unit(value.expertise)
                    || !Unit(value.prestige) || !Unit(value.authority)
                    || !Unit(value.motiveIntegrity)
                    || !Unit(value.corroboration)
                    || !Unit(value.plausibility)
                    || !Unit(value.priorCongruence)
                    || !Unit(value.methodQuality)
                    || !Unit(value.observedPayoff)
                    || !Unit(value.uncontestedConflictFreedom)
                    || !Unit(value.conflictFreedom)
                    || !Unit(value.epistemicVigilance)
                    || !Signed(value.noveltyAcceptance)
                    || !ValidPoliticalBinding(value)
                    || float.IsNaN(value.decayRate)
                    || float.IsInfinity(value.decayRate)
                    || value.decayRate < 0f))
                return "a knowledge proposition is incomplete";
            if (propositions.GroupBy(value => value.identity,
                    StringComparer.Ordinal).Any(group => group.Count() > 1))
                return "a knowledge proposition identity is duplicated";
            if (researchPrograms.Any(value => value == null
                    || value.identity.NullOrEmpty()
                    || value.projectKey.NullOrEmpty()
                    || value.priorKnowledgeKeys == null
                    || value.skilledPawnIds == null
                    || value.evidenceKeys == null
                    || value.priorKnowledgeKeys.Count
                        > CACulturalCognitionPureKernel
                            .MaxResearchPriorKnowledge
                    || value.evidenceKeys.Count
                        > CACulturalCognitionPureKernel.MaxResearchEvidence
                    || value.skilledPawnIds.Count
                        > CACulturalCognitionPureKernel.MaxResearchSkilledPawns
                    || value.institutionIdentity.NullOrEmpty()
                    || value.authorityBasis.NullOrEmpty()
                    || value.method.NullOrEmpty()
                    || value.facilityIdentity.NullOrEmpty()
                    || value.materialBasis.NullOrEmpty()
                    || value.preservation.NullOrEmpty()
                    || value.dissemination.NullOrEmpty()
                    || value.collaborationCount < 0
                    || value.skilledPawnIds.Any(id => id < 0)))
                return "a research program receipt is incomplete";
            if (researchPrograms.GroupBy(value => value.identity,
                    StringComparer.Ordinal).Any(group => group.Count() > 1))
                return "a research program identity is duplicated";
            return null;
        }

        private static bool ValidPoliticalBinding(
            CAKnowledgePropositionRecord value)
        {
            bool empty = value.politicalAxisKey.NullOrEmpty()
                && value.politicalOptionKey.NullOrEmpty()
                && Math.Abs(value.politicalSupport) < 0.0001f;
            if (empty) return true;
            CAAxisDef axis = CAFactionAxes.AxisDef(value.politicalAxisKey);
            return axis?.Options?.Any(option => option.Key
                    == value.politicalOptionKey) == true
                && value.politicalSupport >= -1f
                && value.politicalSupport <= 1f
                && !float.IsNaN(value.politicalSupport)
                && !float.IsInfinity(value.politicalSupport);
        }

        public override void WorldComponentTick()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now >= nextKnowledgeTick)
            {
                nextKnowledgeTick = now + 60000;
                KnowledgeTick(now);
            }
            if (now >= nextLongTick)
            {
                nextLongTick = now + 10 * 60000;
                LongTick(now);
            }
        }

        private void KnowledgeTick(int now)
        {
            using (CAModuleProfiler.Measure(
                CAModuleProfileKey.PropositionKnowledge))
            {
                SyncFinishedResearch(now);
                SyncInstitutionalRecords(now);
                ApplyRetention(now);
                CAModuleProfiler.Observe(
                    CAModuleProfileKey.PropositionKnowledge,
                    objectsExamined: propositions.Count,
                    candidatesAccepted: researchPrograms.Count);
            }
        }

        private void LongTick(int now)
        {
            foreach (CAKnowledgePropositionRecord proposition in propositions
                .Where(value => value != null && value.decayRate > 0f
                    && value.lastConfirmedTick >= 0))
            {
                int since = Math.Max(proposition.lastConfirmedTick,
                    proposition.lastDecayTick);
                float elapsedDays = Math.Max(0, now - since) / 60000f;
                proposition.confidence = Mathf.Clamp01(
                    proposition.confidence * Mathf.Exp(
                        -proposition.decayRate * elapsedDays));
                proposition.lastDecayTick = now;
            }
            ApplyRetention(now);
        }

        internal CAKnowledgePropositionRecord RecordObservedFact(
            CASocialFactContext fact, Pawn holder, string sourceType,
            string acquisitionChannel, string sourceIdentity, int tick)
        {
            if (fact == null || holder == null
                || fact.FactIdentity.NullOrEmpty()
                || fact.SubjectKey.NullOrEmpty()) return null;
            float accessPosition = CACulturalCognitionWorldComponent.Current
                ?.AttitudeFor(holder, CACultureQuestionRegistry.KnowledgeAccess)
                ?.publicExpression ?? 0f;
            bool directObservation = acquisitionChannel
                == "direct observation";
            string representedSource = fact.EpistemicSourceIdentity
                ?? sourceIdentity;
            if (representedSource.NullOrEmpty()) return null;
            Pawn sourcePawn = int.TryParse(representedSource,
                    out int sourcePawnId)
                ? PawnById(sourcePawnId) : null;
            float representedStanding = Mathf.Clamp01(0.25f
                + (holder.skills?.GetSkill(SkillDefOf.Intellectual)?.Level
                    ?? 0) / 20f * 0.75f);
            if (!CACulturalCognitionPureKernel.KnowledgeAccessEligible(
                    accessPosition, representedStanding, directObservation))
                return null;
            float relationshipTrust = sourcePawn == null ? 0.5f
                : sourcePawn == holder ? 1f : Mathf.Clamp01(
                    (holder.relations?.OpinionOf(sourcePawn) ?? 0) / 200f
                        + 0.5f);
            float expertise = ExpertiseFor(sourcePawn, fact.SubjectKey);
            float prestige = sourcePawn?.Faction?.leader == sourcePawn ? 1f
                : sourcePawn == null ? 0.5f : Mathf.Clamp01(
                    (sourcePawn.skills?.GetSkill(SkillDefOf.Social)?.Level
                        ?? 0) / 20f);
            float authority = SourceAuthority(sourcePawn);
            string questionKey = CACultureQuestionRegistry
                .QuestionForSocialSubject(fact.SubjectKey);
            CAPawnCulturalAttitude prior = questionKey.NullOrEmpty() ? null
                : CACulturalCognitionWorldComponent.Current?.AttitudeFor(
                    holder, questionKey);
            int direction = CACultureQuestionRegistry
                .DirectionForSocialSubject(fact.SubjectKey);
            float priorCongruence = prior == null ? 0.5f : Mathf.Clamp01(
                (prior.privateAttitude * direction
                    * (fact.Realization < 0 ? -1f : 1f) + 1f) * 0.5f);
            var input = new CAKnowledgeAcceptanceInput(
                sourceReliability: directObservation ? 0.90f : 0.55f,
                relationshipTrust: relationshipTrust,
                expertise: expertise,
                prestige: prestige,
                authority: authority,
                motiveIntegrity: directObservation ? 0.75f : 0.50f,
                corroboration: CACulturalCognitionPureKernel
                    .KnowledgeCorroboration(1),
                plausibility: 0.50f,
                priorCongruence: priorCongruence,
                methodQuality: acquisitionChannel == "direct observation"
                    ? 0.90f : 0.45f,
                observedPayoff: 0.50f,
                conflictFreedom: 1f,
                epistemicVigilance: CACulturalCognitionWorldComponent.Current
                    ?.ProfileFor(holder)?.epistemicVigilance ?? 0.5f,
                noveltyAcceptance: CACulturalCognitionWorldComponent.Current
                    ?.AttitudeFor(holder,
                        CACultureQuestionRegistry.NoveltyAcceptance)
                        ?.privateAttitude ?? 0f,
                expertiseDeference: Mathf.InverseLerp(-1f, 1f,
                    CACulturalCognitionWorldComponent.Current?.AttitudeFor(
                        holder, CACultureQuestionRegistry.ExpertiseDeference)
                        ?.privateAttitude ?? 0f));
            CAPoliticalEvidenceMap.TryFor(fact, out string politicalAxis,
                out string politicalOption, out float politicalSupport);
            CAKnowledgePropositionRecord record = Acquire("fact:"
                    + fact.FactIdentity + ":holder:"
                    + holder.thingIDNumber,
                fact.SubjectKey,
                "The represented fact " + fact.FactIdentity + " occurred.",
                fact.SubjectKey + " / " + fact.FactIdentity,
                "pawn:" + holder.thingIDNumber,
                representedSource ?? "represented source",
                sourceType ?? "represented event",
                acquisitionChannel ?? "observation",
                input, "social fact owner", fact.FactIdentity,
                tick, decayRate: 0.02f,
                politicalAxisKey: politicalAxis,
                politicalOptionKey: politicalOption,
                politicalSupport: politicalSupport);
            if (record != null)
            {
                record.access = KnowledgeAccessRule(holder);
                float novelty = CACulturalCognitionWorldComponent.Current
                    ?.AttitudeFor(holder,
                        CACultureQuestionRegistry.NoveltyAcceptance)
                    ?.publicExpression ?? 0f;
                record.transmissibility *=
                    CACulturalCognitionPureKernel.KnowledgeTransmission(
                        accessPosition, novelty);
            }
            return record;
        }

        internal CAKnowledgePropositionRecord Acquire(string identity,
            string topic, string claim, string content, string holderIdentity,
            string sourceIdentity, string sourceType,
            string acquisitionChannel, CAKnowledgeAcceptanceInput appraisal,
            string provenance, string evidenceKey, int tick,
            float decayRate = 0f, string custodianIdentity = null,
            string politicalAxisKey = null,
            string politicalOptionKey = null,
            float politicalSupport = 0f,
            string contradictsIdentity = null)
        {
            if (identity.NullOrEmpty() || topic.NullOrEmpty()
                || claim.NullOrEmpty() || holderIdentity.NullOrEmpty()
                || sourceIdentity.NullOrEmpty()) return null;
            EnsureIndexes();
            propositionByIdentity.TryGetValue(identity,
                out CAKnowledgePropositionRecord record);
            if (record == null)
            {
                record = new CAKnowledgePropositionRecord
                {
                    identity = identity,
                    topic = topic,
                    claim = claim,
                    content = content,
                    holderIdentity = holderIdentity,
                    sourceIdentity = sourceIdentity,
                    sourceType = sourceType ?? "represented source",
                    acquisitionChannel = acquisitionChannel
                        ?? "represented acquisition",
                    provenance = provenance,
                    provenanceChain = new List<string>(),
                    evidence = new List<string>(),
                    contradictions = new List<string>(),
                    corroboratingSources = new List<string>(),
                    acquiredTick = tick,
                    decayRate = Math.Max(0f, decayRate),
                    custodianIdentity = custodianIdentity ?? holderIdentity
                };
                propositions.Add(record);
                propositionByIdentity[identity] = record;
                if (!propositionsByTopic.TryGetValue(topic,
                        out List<CAKnowledgePropositionRecord> topicRecords))
                {
                    topicRecords = new List<CAKnowledgePropositionRecord>();
                    propositionsByTopic[topic] = topicRecords;
                }
                topicRecords.Add(record);
            }
            if (record.corroboratingSources == null)
                record.corroboratingSources = new List<string>();
            float independentCorroboration = CACulturalCognitionPureKernel
                .RecordIndependentKnowledgeSource(
                    record.corroboratingSources, sourceIdentity);
            record.sourceIdentity = sourceIdentity;
            record.sourceType = sourceType ?? "represented source";
            record.acquisitionChannel = acquisitionChannel
                ?? "represented acquisition";
            record.provenance = provenance;
            record.sourceReliability = Clamp(appraisal.SourceReliability);
            record.sourceTrust = Clamp(appraisal.RelationshipTrust);
            record.expertise = Clamp(appraisal.Expertise);
            record.prestige = Clamp(appraisal.Prestige);
            record.authority = Clamp(appraisal.Authority);
            record.motiveIntegrity = Clamp(appraisal.MotiveIntegrity);
            record.corroboration = Mathf.Max(Clamp(appraisal.Corroboration),
                independentCorroboration);
            record.plausibility = Clamp(appraisal.Plausibility);
            record.priorCongruence = Clamp(appraisal.PriorCongruence);
            record.methodQuality = Clamp(appraisal.MethodQuality);
            record.observedPayoff = Clamp(appraisal.ObservedPayoff);
            record.uncontestedConflictFreedom = Clamp(
                appraisal.ConflictFreedom);
            record.epistemicVigilance = Clamp(
                appraisal.EpistemicVigilance);
            record.noveltyAcceptance = Mathf.Clamp(
                appraisal.NoveltyAcceptance, -1f, 1f);
            record.politicalAxisKey = politicalAxisKey;
            record.politicalOptionKey = politicalOptionKey;
            record.politicalSupport = Mathf.Clamp(politicalSupport,
                -1f, 1f);
            record.lastConfirmedTick = tick;
            string link = (sourceType ?? "source") + ":"
                + sourceIdentity + " via "
                + (acquisitionChannel ?? "acquisition");
            AddBounded(record.provenanceChain, link,
                CACulturalCognitionPureKernel.MaxKnowledgeProvenance);
            if (!evidenceKey.NullOrEmpty()
                ) AddBounded(record.evidence, evidenceKey,
                    CACulturalCognitionPureKernel.MaxKnowledgeEvidence);
            ReevaluateKnowledge(record, appraisal.ExpertiseDeference);
            if (!contradictsIdentity.NullOrEmpty())
                LinkExplicitContradiction(record.identity,
                    contradictsIdentity);
            ApplyRetention(tick);
            return propositionByIdentity.TryGetValue(identity,
                out CAKnowledgePropositionRecord retained) ? retained : null;
        }

        internal bool LinkExplicitContradiction(string leftIdentity,
            string rightIdentity)
        {
            if (leftIdentity.NullOrEmpty() || rightIdentity.NullOrEmpty()
                || leftIdentity == rightIdentity) return false;
            EnsureIndexes();
            if (!propositionByIdentity.TryGetValue(leftIdentity,
                    out CAKnowledgePropositionRecord left)
                || !propositionByIdentity.TryGetValue(rightIdentity,
                    out CAKnowledgePropositionRecord right)) return false;
            AddBounded(left.contradictions, right.identity,
                CACulturalCognitionPureKernel.MaxKnowledgeContradictions);
            AddBounded(right.contradictions, left.identity,
                CACulturalCognitionPureKernel.MaxKnowledgeContradictions);
            ReevaluateKnowledge(left);
            ReevaluateKnowledge(right);
            return true;
        }

        private void ReevaluateKnowledge(
            CAKnowledgePropositionRecord record,
            float? expertiseDeference = null)
        {
            if (record == null) return;
            record.conflictFreedom = CACulturalCognitionPureKernel
                .KnowledgeConflictFreedom(
                    record.uncontestedConflictFreedom,
                    record.contradictions?.Count ?? 0);
            CAKnowledgeAcceptanceResult accepted =
                CACulturalCognitionPureKernel.EvaluateKnowledge(
                    new CAKnowledgeAcceptanceInput(
                        record.sourceReliability, record.sourceTrust,
                        record.expertise, record.prestige, record.authority,
                        record.motiveIntegrity, record.corroboration,
                        record.plausibility, record.priorCongruence,
                        record.methodQuality, record.observedPayoff,
                        record.conflictFreedom,
                        record.epistemicVigilance,
                        record.noveltyAcceptance,
                        expertiseDeference
                            ?? ExpertiseDeferenceFor(record)));
            record.confidence = accepted.Confidence;
            record.transmissibility = accepted.Transmissibility;
        }

        private static float ExpertiseDeferenceFor(
            CAKnowledgePropositionRecord record)
        {
            if (record?.holderIdentity.NullOrEmpty() != false)
                return 0.5f;
            string identity = record.holderIdentity.StartsWith("pawn:",
                    StringComparison.Ordinal)
                ? record.holderIdentity.Substring(5)
                : record.holderIdentity;
            if (!int.TryParse(identity, out int pawnId)) return 0.5f;
            Pawn holder = PawnById(pawnId);
            float position = CACulturalCognitionWorldComponent.Current
                ?.AttitudeFor(holder,
                    CACultureQuestionRegistry.ExpertiseDeference)
                ?.privateAttitude ?? 0f;
            return Mathf.InverseLerp(-1f, 1f, position);
        }

        internal void RecordSettlementResearch(
            CASettlementResearchWork work, Pawn worker,
            CARegionalSettlementRecord settlement, bool succeeded, int tick)
        {
            if (work == null || worker == null || settlement == null) return;
            string identity = "research-program:" + work.settlementKey + ":"
                + work.episodeId;
            EnsureIndexes();
            researchByIdentity.TryGetValue(identity,
                out CAResearchProgramReceipt receipt);
            if (receipt == null)
            {
                receipt = new CAResearchProgramReceipt
                {
                    identity = identity,
                    projectKey = work.programKey,
                    institutionIdentity = work.operatorIdentity,
                    authorityBasis = work.authorityIdentity,
                    method = "native study job under represented program",
                    facilityIdentity = work.bench.ToString(),
                    materialBasis = work.programSignature,
                    startedTick = Math.Max(0, tick - 2500),
                    collaborationCount = 1,
                    behaviorKey = work.behaviorKey,
                    preservation = "settlement research program and "
                        + "proposition ledger",
                    dissemination = KnowledgeAccessRule(worker)
                };
                receipt.skilledPawnIds.Add(worker.thingIDNumber);
                receipt.priorKnowledgeKeys.AddRange(propositions
                    .Where(value => value != null
                        && value.holderIdentity == work.operatorIdentity)
                    .Select(value => value.identity).Take(8));
                researchPrograms.Add(receipt);
                researchByIdentity[identity] = receipt;
            }
            receipt.completedTick = succeeded ? tick : -1;
            receipt.evaluation = succeeded
                ? "native job completed and completion authority revalidated"
                : "native job ended without validated completion";
            if (!succeeded)
            {
                ApplyRetention(tick);
                return;
            }
            string evidence = "research-work:" + work.episodeId;
            AddBounded(receipt.evidenceKeys, evidence,
                CACulturalCognitionPureKernel.MaxResearchEvidence);
            CAKnowledgePropositionRecord milestone = Acquire(
                "research-milestone:" + work.settlementKey + ":"
                    + work.episodeId,
                work.programKey,
                "A represented research milestone was completed.",
                work.programSignature,
                work.operatorIdentity,
                "pawn:" + worker.thingIDNumber,
                "technical work",
                "experiment and institutional record",
                new CAKnowledgeAcceptanceInput(0.90f, 0.80f,
                    Mathf.Clamp01(worker.skills.GetSkill(
                        SkillDefOf.Intellectual).Level / 20f),
                    0.40f, 0.65f, 0.85f, 0.80f, 0.80f, 0.50f,
                    0.85f, 0.70f, 0.90f,
                    CACulturalCognitionWorldComponent.Current
                        ?.ProfileFor(worker)?.epistemicVigilance ?? 0.5f,
                    CACulturalCognitionWorldComponent.Current
                        ?.AttitudeFor(worker,
                            CACultureQuestionRegistry.NoveltyAcceptance)
                        ?.privateAttitude ?? 0f),
                "settlement research completion", evidence, tick,
                custodianIdentity: work.operatorIdentity);
            if (milestone != null)
            {
                milestone.access = KnowledgeAccessRule(worker);
                milestone.transmissibility *= NoveltyTransmission(worker);
            }
            ApplyRetention(tick);
        }

        private void SyncFinishedResearch(int now)
        {
            if (Find.ResearchManager == null) return;
            foreach (ResearchProjectDef project in
                DefDatabase<ResearchProjectDef>.AllDefsListForReading
                    .Where(value => value != null && value.IsFinished))
            {
                string identity = "research:" + project.defName;
                EnsureIndexes();
                propositionByIdentity.TryGetValue(identity,
                    out CAKnowledgePropositionRecord existing);
                if (existing != null)
                {
                    existing.lastConfirmedTick = now;
                    existing.access = KnowledgeAccessRule(null);
                    existing.transmissibility = NoveltyTransmission(null);
                    continue;
                }
                Acquire(identity, project.label,
                    "The methods and results of " + project.label
                        + " are established.",
                    project.description,
                    "player research program", project.defName,
                    "native research result", "technical work",
                    new CAKnowledgeAcceptanceInput(1f, 0.75f, 1f, 0.5f,
                        0.65f, 0.9f, 1f, 0.9f, 0.5f, 1f, 0.8f, 0.95f,
                        0.65f, MeanPlayerQuestion(
                            CACultureQuestionRegistry.NoveltyAcceptance)),
                    "native ResearchManager finished project",
                    "ResearchProjectDef.IsFinished", now,
                    custodianIdentity: "player research program");
                propositionByIdentity.TryGetValue(identity,
                    out CAKnowledgePropositionRecord record);
                if (record == null) continue;
                record.access = KnowledgeAccessRule(null);
                record.transmissibility = NoveltyTransmission(null);
            }
        }

        private void SyncInstitutionalRecords(int now)
        {
            foreach (CAOrganization organization in
                CAOrganizationWorldComponent.Current?.Organizations
                    ?? Array.Empty<CAOrganization>())
            {
                foreach (CADecisionEntry decision in
                    (organization.decisionHistory
                        ?? new List<CADecisionEntry>())
                    .Where(value => value != null && value.tick >= 0)
                    .OrderByDescending(value => value.tick).Take(8))
                {
                    string identity = "institutional-record:"
                        + organization.organizationKey + ":" + decision.tick
                        + ":" + CASocialPatternKernel.StableHash(
                            (decision.kind ?? "note") + "|"
                            + (decision.text ?? ""));
                CAKnowledgePropositionRecord record = Acquire(identity,
                        "institutional." + (decision.kind ?? "record"),
                        decision.text ?? "An institutional act was recorded.",
                        decision.text,
                        organization.organizationKey,
                        organization.organizationKey,
                        "institutional record", "record custody",
                        new CAKnowledgeAcceptanceInput(0.85f, 0.70f, 0.65f,
                            0.45f, 0.80f, 0.70f, 0.65f, 0.75f, 0.50f,
                            0.70f, 0.50f, 0.70f, 0.60f,
                            MeanOrganizationQuestion(organization,
                                CACultureQuestionRegistry
                                    .NoveltyAcceptance)),
                        "organization decision history",
                        "decision:" + decision.tick, now,
                        decayRate: 0.005f,
                        custodianIdentity: organization.organizationKey);
                    if (record != null)
                    {
                        float access = MeanOrganizationQuestion(organization,
                            CACultureQuestionRegistry.KnowledgeAccess);
                        float novelty = MeanOrganizationQuestion(organization,
                            CACultureQuestionRegistry.NoveltyAcceptance);
                        record.access = KnowledgeAccessRule(access);
                        record.transmissibility *=
                            CACulturalCognitionPureKernel
                                .KnowledgeTransmission(access, novelty);
                    }
                }
            }
        }

        private static string KnowledgeAccessRule(Pawn pawn)
        {
            float mean = pawn == null
                ? MeanPlayerQuestion(CACultureQuestionRegistry
                    .KnowledgeAccess)
                : CACulturalCognitionWorldComponent.Current?.AttitudeFor(
                    pawn, CACultureQuestionRegistry.KnowledgeAccess)
                    ?.publicExpression ?? 0f;
            return KnowledgeAccessRule(mean);
        }

        private static string KnowledgeAccessRule(float mean)
        {
            if (mean <= -0.65f) return "esoteric custodians";
            if (mean <= -0.20f) return "restricted access";
            if (mean < 0.20f) return "credentialed access";
            if (mean < 0.65f) return "broad access";
            return "open access";
        }

        private static float NoveltyTransmission(Pawn pawn)
        {
            float novelty = pawn == null
                ? MeanPlayerQuestion(CACultureQuestionRegistry
                    .NoveltyAcceptance)
                : CACulturalCognitionWorldComponent.Current?.AttitudeFor(
                    pawn, CACultureQuestionRegistry.NoveltyAcceptance)
                    ?.publicExpression ?? 0f;
            float access = pawn == null
                ? MeanPlayerQuestion(CACultureQuestionRegistry.KnowledgeAccess)
                : CACulturalCognitionWorldComponent.Current?.AttitudeFor(
                    pawn, CACultureQuestionRegistry.KnowledgeAccess)
                    ?.publicExpression ?? 0f;
            return CACulturalCognitionPureKernel.KnowledgeTransmission(
                access, novelty);
        }

        private static float MeanPlayerQuestion(string questionKey)
        {
            List<CAPawnCulturalAttitude> values =
                PawnsFinder.AllMapsWorldAndTemporary_Alive
                    .Where(value => value != null
                        && value.Faction == Faction.OfPlayer)
                    .Select(value => CACulturalCognitionWorldComponent.Current
                        ?.AttitudeFor(value, questionKey))
                    .Where(value => value != null).ToList();
            return values.Count == 0 ? 0f
                : values.Average(value => value.publicExpression);
        }

        private static float MeanOrganizationQuestion(
            CAOrganization organization, string questionKey)
        {
            if (organization?.memberPawnIds == null) return 0f;
            List<CAPawnCulturalAttitude> values = organization.memberPawnIds
                .Select(PawnById).Where(value => value != null)
                .Select(value => CACulturalCognitionWorldComponent.Current
                    ?.AttitudeFor(value, questionKey))
                .Where(value => value != null).ToList();
            return values.Count == 0 ? 0f
                : values.Average(value => value.publicExpression);
        }

        private void EnsureIndexes()
        {
            if (propositionByIdentity == null || propositionsByTopic == null
                || politicalPropositionsByHolderAxis == null
                || researchByIdentity == null)
                RebuildIndexes();
        }

        private void RebuildIndexes()
        {
            propositionByIdentity = (propositions
                    ?? new List<CAKnowledgePropositionRecord>())
                .Where(value => value != null
                    && !value.identity.NullOrEmpty())
                .GroupBy(value => value.identity, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(),
                    StringComparer.Ordinal);
            propositionsByTopic = (propositions
                    ?? new List<CAKnowledgePropositionRecord>())
                .Where(value => value != null && !value.topic.NullOrEmpty())
                .GroupBy(value => value.topic, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(),
                    StringComparer.Ordinal);
            politicalPropositionsByHolderAxis = (propositions
                    ?? new List<CAKnowledgePropositionRecord>())
                .Where(value => value != null
                    && !value.holderIdentity.NullOrEmpty()
                    && !value.politicalAxisKey.NullOrEmpty())
                .GroupBy(value => value.holderIdentity + "\0"
                    + value.politicalAxisKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(),
                    StringComparer.Ordinal);
            researchByIdentity = (researchPrograms
                    ?? new List<CAResearchProgramReceipt>())
                .Where(value => value != null
                    && !value.identity.NullOrEmpty())
                .GroupBy(value => value.identity, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(),
                    StringComparer.Ordinal);
        }

        private void ApplyRetention(int now)
        {
            if (researchPrograms.Count
                > CACulturalCognitionPureKernel.MaxResearchPrograms)
                researchPrograms = researchPrograms.Where(value =>
                        value != null)
                    .OrderByDescending(value => value.completedTick < 0)
                    .ThenByDescending(value => Math.Max(value.completedTick,
                        value.startedTick))
                    .ThenBy(value => value.identity, StringComparer.Ordinal)
                    .Take(CACulturalCognitionPureKernel.MaxResearchPrograms)
                    .ToList();
            var referenced = new HashSet<string>(researchPrograms
                .Where(value => value?.priorKnowledgeKeys != null)
                .SelectMany(value => value.priorKnowledgeKeys)
                .Where(value => !value.NullOrEmpty()), StringComparer.Ordinal);
            HashSet<string> retained = CACulturalCognitionPureKernel
                .RetainedKnowledgeIdentities(propositions.Where(value =>
                        value != null).Select(value =>
                    new CAKnowledgeRetentionCandidate(value.identity,
                        value.topic, value.confidence,
                        Math.Max(value.lastConfirmedTick,
                            value.acquiredTick),
                        referenced.Contains(value.identity))));
            if (retained.Count != propositions.Count)
                propositions.RemoveAll(value => value == null
                    || !retained.Contains(value.identity));
            foreach (CAKnowledgePropositionRecord proposition in propositions)
            {
                int removed = proposition.contradictions?.RemoveAll(value =>
                    !retained.Contains(value)) ?? 0;
                if (removed > 0) ReevaluateKnowledge(proposition);
            }
            foreach (CAResearchProgramReceipt program in researchPrograms
                .Where(value => value != null))
                program.priorKnowledgeKeys?.RemoveAll(value =>
                    !retained.Contains(value));
            RebuildIndexes();
        }

        private static void AddBounded(List<string> values, string value,
            int limit)
        {
            if (values == null || value.NullOrEmpty() || limit < 1) return;
            values.Remove(value);
            values.Add(value);
            if (values.Count > limit)
                values.RemoveRange(0, values.Count - limit);
        }

        private static Pawn PawnById(int id)
        {
            return PawnsFinder.AllMapsWorldAndTemporary_Alive
                .FirstOrDefault(value => value?.thingIDNumber == id);
        }

        private static float ExpertiseFor(Pawn pawn, string subjectKey)
        {
            if (pawn?.skills == null || subjectKey.NullOrEmpty()) return 0.5f;
            return CACulturalCognitionPureKernel.DemonstratedExpertise(
                subjectKey, domain =>
                {
                    SkillDef skill = ExpertiseSkill(domain);
                    return skill == null ? 0f : Mathf.Clamp01(
                        (pawn.skills.GetSkill(skill)?.Level ?? 0) / 20f);
                });
        }

        private static SkillDef ExpertiseSkill(string domain)
        {
            switch (domain)
            {
                case CACulturalCognitionPureKernel.ExpertiseSocial:
                    return SkillDefOf.Social;
                case CACulturalCognitionPureKernel.ExpertiseIntellectual:
                    return SkillDefOf.Intellectual;
                case CACulturalCognitionPureKernel.ExpertiseMedicine:
                    return SkillDefOf.Medicine;
                case CACulturalCognitionPureKernel.ExpertiseCooking:
                    return SkillDefOf.Cooking;
                case CACulturalCognitionPureKernel.ExpertisePlants:
                    return SkillDefOf.Plants;
                case CACulturalCognitionPureKernel.ExpertiseAnimals:
                    return SkillDefOf.Animals;
                case CACulturalCognitionPureKernel.ExpertiseCrafting:
                    return SkillDefOf.Crafting;
                case CACulturalCognitionPureKernel.ExpertiseConstruction:
                    return SkillDefOf.Construction;
                case CACulturalCognitionPureKernel.ExpertiseShooting:
                    return SkillDefOf.Shooting;
                case CACulturalCognitionPureKernel.ExpertiseMelee:
                    return SkillDefOf.Melee;
                case CACulturalCognitionPureKernel.ExpertiseArtistic:
                    return SkillDefOf.Artistic;
                default: return null;
            }
        }

        private static float SourceAuthority(Pawn pawn)
        {
            if (pawn == null) return 0.5f;
            bool holdsOffice = CAOrganizationWorldComponent.Current
                ?.Organizations.Any(organization => organization?.offices
                    ?.Any(office => office != null
                        && office.holderId == pawn.thingIDNumber) == true)
                == true;
            return holdsOffice ? 1f : 0.25f;
        }

        private static float Clamp(float value)
        {
            return Mathf.Clamp01(value);
        }

        private static bool Unit(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value)
                && value >= 0f && value <= 1f;
        }

        private static bool Signed(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value)
                && value >= -1f && value <= 1f;
        }
    }
}
