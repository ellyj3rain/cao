using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Typed payload for the affiliation fact family. Ownership, support, and
    // resident affiliation remain independently reportable even when a pawn's
    // version is incomplete or wrong. A relay copies this payload; it never
    // reconstructs it from the live site.
    public sealed class CASiteAffiliationKnowledgePayload : IExposable
    {
        public string siteIdentity;
        public CASiteFactionReferenceKind ownership;
        public int ownerRegionalFactionKey = -1;
        public int ownerWorldFactionLoadId = -1;
        public CASiteFactionReferenceKind support;
        public int supportRegionalFactionKey = -1;
        public int supportWorldFactionLoadId = -1;
        public List<int> populationRegionalFactionKeys = new List<int>();
        public bool includesUnaffiliatedResidents;

        public void ExposeData()
        {
            Scribe_Values.Look(ref siteIdentity, "siteIdentity");
            Scribe_Values.Look(ref ownership, "ownership",
                CASiteFactionReferenceKind.None);
            Scribe_Values.Look(ref ownerRegionalFactionKey,
                "ownerRegionalFactionKey", -1);
            Scribe_Values.Look(ref ownerWorldFactionLoadId,
                "ownerWorldFactionLoadId", -1);
            Scribe_Values.Look(ref support, "support",
                CASiteFactionReferenceKind.None);
            Scribe_Values.Look(ref supportRegionalFactionKey,
                "supportRegionalFactionKey", -1);
            Scribe_Values.Look(ref supportWorldFactionLoadId,
                "supportWorldFactionLoadId", -1);
            Scribe_Collections.Look(ref populationRegionalFactionKeys,
                "populationRegionalFactionKeys", LookMode.Value);
            Scribe_Values.Look(ref includesUnaffiliatedResidents,
                "includesUnaffiliatedResidents", false);
        }

        internal CASiteAffiliationKnowledgePayload Copy()
        {
            return new CASiteAffiliationKnowledgePayload
            {
                siteIdentity = siteIdentity,
                ownership = ownership,
                ownerRegionalFactionKey = ownerRegionalFactionKey,
                ownerWorldFactionLoadId = ownerWorldFactionLoadId,
                support = support,
                supportRegionalFactionKey = supportRegionalFactionKey,
                supportWorldFactionLoadId = supportWorldFactionLoadId,
                populationRegionalFactionKeys =
                    new List<int>(populationRegionalFactionKeys
                        ?? new List<int>()),
                includesUnaffiliatedResidents =
                    includesUnaffiliatedResidents
            };
        }

        internal string ValidationFailure()
        {
            if (siteIdentity.NullOrEmpty()) return "site identity is missing";
            var links = new CASiteFactionLinks
            {
                ownership = ownership,
                ownerRegionalFactionKey = ownerRegionalFactionKey,
                ownerWorldFactionLoadId = ownerWorldFactionLoadId,
                support = support,
                supportRegionalFactionKey = supportRegionalFactionKey,
                supportWorldFactionLoadId = supportWorldFactionLoadId
            };
            string linkFailure = links.ValidationFailure();
            if (!linkFailure.NullOrEmpty()) return linkFailure;
            if (populationRegionalFactionKeys == null
                || populationRegionalFactionKeys.Any(key => key < 0))
                return "population affiliation is invalid";
            return null;
        }
    }

    public sealed class CAKnowledgePropositionRecord : IExposable
    {
        public CAKnowledgeFactKind factKind =
            CAKnowledgeFactKind.SocialEvent;
        public CAKnowledgePersistenceClass persistenceClass =
            CAKnowledgePersistenceClass.Working;
        public string subjectIdentity;
        public string identity;
        public string topic;
        public string claim;
        public string content;
        public string holderIdentity;
        public string sourceIdentity;
        public string immediateReporterIdentity;
        public string sourceType;
        public string acquisitionChannel;
        public string provenance;
        public List<string> provenanceChain = new List<string>();
        public float confidence;
        public float uncertainty = 0.5f;
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
        public int sourceEventTick = -1;
        public int lastReportedTick = -1;
        public int lastConfirmedTick = -1;
        public int lastDecayTick = -1;
        public float decayRate;
        public string custodianIdentity;
        public int staleAfterTick = -1;
        public int revision = 1;
        public string supersedesIdentity;
        public string supersededByIdentity;
        public string spatialScope;
        public string socialScope;
        public CASiteAffiliationKnowledgePayload siteAffiliation;

        public void ExposeData()
        {
            Scribe_Values.Look(ref factKind, "factKind",
                CAKnowledgeFactKind.SocialEvent);
            Scribe_Values.Look(ref persistenceClass, "persistenceClass",
                CAKnowledgePersistenceClass.Working);
            Scribe_Values.Look(ref subjectIdentity, "subjectIdentity");
            Scribe_Values.Look(ref identity, "identity");
            Scribe_Values.Look(ref topic, "topic");
            Scribe_Values.Look(ref claim, "claim");
            Scribe_Values.Look(ref content, "content");
            Scribe_Values.Look(ref holderIdentity, "holderIdentity");
            Scribe_Values.Look(ref sourceIdentity, "sourceIdentity");
            Scribe_Values.Look(ref immediateReporterIdentity,
                "immediateReporterIdentity");
            Scribe_Values.Look(ref sourceType, "sourceType");
            Scribe_Values.Look(ref acquisitionChannel,
                "acquisitionChannel");
            Scribe_Values.Look(ref provenance, "provenance");
            Scribe_Collections.Look(ref provenanceChain,
                "provenanceChain", LookMode.Value);
            Scribe_Values.Look(ref confidence, "confidence", 0f);
            Scribe_Values.Look(ref uncertainty, "uncertainty", 0.5f);
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
            Scribe_Values.Look(ref sourceEventTick, "sourceEventTick", -1);
            Scribe_Values.Look(ref lastReportedTick, "lastReportedTick", -1);
            Scribe_Values.Look(ref lastConfirmedTick,
                "lastConfirmedTick", -1);
            Scribe_Values.Look(ref lastDecayTick, "lastDecayTick", -1);
            Scribe_Values.Look(ref decayRate, "decayRate", 0f);
            Scribe_Values.Look(ref custodianIdentity,
                "custodianIdentity");
            Scribe_Values.Look(ref staleAfterTick, "staleAfterTick", -1);
            Scribe_Values.Look(ref revision, "revision", 1);
            Scribe_Values.Look(ref supersedesIdentity,
                "supersedesIdentity");
            Scribe_Values.Look(ref supersededByIdentity,
                "supersededByIdentity");
            Scribe_Values.Look(ref spatialScope, "spatialScope");
            Scribe_Values.Look(ref socialScope, "socialScope");
            Scribe_Deep.Look(ref siteAffiliation, "siteAffiliation");
        }

        internal bool IsStale(int tick)
        {
            return staleAfterTick >= 0 && tick >= staleAfterTick;
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
        public const int CurrentOwnerSchemaVersion = 2;
        public const int CurrentSchemaVersion = 2;
        private int campaignSchemaVersion =
            CurrentOwnerSchemaVersion;
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
                if (schemaVersion == 1)
                {
                    foreach (CAKnowledgePropositionRecord record in
                        propositions ?? new List<CAKnowledgePropositionRecord>())
                        NormalizeCurrentRecord(record);
                    schemaVersion = CurrentSchemaVersion;
                }
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
            foreach (CAKnowledgePropositionRecord record in propositions)
                NormalizeCurrentRecord(record);
            return ValidateCampaignState();
        }

        private static void NormalizeCurrentRecord(
            CAKnowledgePropositionRecord record)
        {
            if (record == null) return;
            if (record.subjectIdentity.NullOrEmpty())
                record.subjectIdentity = record.topic;
            if (record.immediateReporterIdentity.NullOrEmpty())
                record.immediateReporterIdentity = record.sourceIdentity;
            if (record.sourceEventTick < 0)
                record.sourceEventTick = record.acquiredTick;
            if (record.lastReportedTick < 0)
                record.lastReportedTick = record.acquiredTick;
            record.revision = Math.Max(1, record.revision);
            record.uncertainty = Mathf.Clamp01(record.uncertainty);
            if (record.persistenceClass
                    == CAKnowledgePersistenceClass.Transient
                && record.staleAfterTick < 0 && record.acquiredTick >= 0)
                record.staleAfterTick = record.acquiredTick + 2500;
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
                    || value.subjectIdentity.NullOrEmpty()
                    || value.holderIdentity.NullOrEmpty()
                    || value.sourceIdentity.NullOrEmpty()
                    || value.immediateReporterIdentity.NullOrEmpty()
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
                    || !Unit(value.uncertainty)
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
            if (propositions.Any(value => value.revision < 1
                    || (value.factKind == CAKnowledgeFactKind.SiteAffiliation
                        && (value.siteAffiliation == null
                            || !value.siteAffiliation.ValidationFailure()
                                .NullOrEmpty()))))
                return "a typed knowledge proposition is incomplete";
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
                if (AwarenessMod.Settings
                        ?.experimentalBroaderPawnKnowledge == true)
                {
                    ObserveNearbySettlementAffiliations(now);
                    RelayBroaderPawnKnowledge(now);
                }
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
            float[] priorSignals = CACultureQuestionRegistry
                .AdaptersForSocialSubject(fact.SubjectKey)
                .Select(adapter => new
                {
                    Adapter = adapter,
                    Prior = CACulturalCognitionWorldComponent.Current?
                        .AttitudeFor(holder, adapter.QuestionKey)
                }).Where(value => value.Prior != null)
                .Select(value => Mathf.Clamp01((value.Prior.privateAttitude
                    * value.Adapter.Direction
                    * (fact.Realization < 0 ? -1f : 1f) + 1f) * 0.5f))
                .ToArray();
            float priorCongruence = priorSignals.Length == 0 ? 0.5f
                : priorSignals.Average();
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
            CASocialSubjectDef representedSubject =
                CASocialSubjectRegistry.Find(fact.SubjectKey);
            CAKnowledgeFactKind representedKind = representedSubject
                    ?.Consumers?.Any(value => string.Equals(value,
                        "political conflict", StringComparison.Ordinal))
                    == true
                ? CAKnowledgeFactKind.Conflict
                : CAKnowledgeFactKind.SocialEvent;
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
                politicalSupport: politicalSupport,
                factKind: representedKind,
                persistenceClass: CAKnowledgePersistenceClass.Working,
                subjectIdentity: fact.FactIdentity,
                immediateReporterIdentity: "pawn:"
                    + holder.thingIDNumber,
                sourceEventTick: tick,
                staleAfterTick: tick + 10 * 60000,
                spatialScope: holder.Map == null ? null
                    : "map:" + holder.Map.uniqueID,
                socialScope: "pawn:" + holder.thingIDNumber);
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
            string contradictsIdentity = null,
            CAKnowledgeFactKind factKind = CAKnowledgeFactKind.SocialEvent,
            CAKnowledgePersistenceClass persistenceClass =
                CAKnowledgePersistenceClass.Working,
            string subjectIdentity = null,
            string immediateReporterIdentity = null,
            int sourceEventTick = -1,
            int staleAfterTick = -1,
            string spatialScope = null,
            string socialScope = null,
            CASiteAffiliationKnowledgePayload siteAffiliation = null)
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
                    factKind = factKind,
                    persistenceClass = persistenceClass,
                    subjectIdentity = subjectIdentity ?? topic,
                    identity = identity,
                    topic = topic,
                    claim = claim,
                    content = content,
                    holderIdentity = holderIdentity,
                    sourceIdentity = sourceIdentity,
                    immediateReporterIdentity = immediateReporterIdentity
                        ?? sourceIdentity,
                    sourceType = sourceType ?? "represented source",
                    acquisitionChannel = acquisitionChannel
                        ?? "represented acquisition",
                    provenance = provenance,
                    provenanceChain = new List<string>(),
                    evidence = new List<string>(),
                    contradictions = new List<string>(),
                    corroboratingSources = new List<string>(),
                    acquiredTick = tick,
                    sourceEventTick = sourceEventTick >= 0
                        ? sourceEventTick : tick,
                    lastReportedTick = tick,
                    decayRate = Math.Max(0f, decayRate),
                    custodianIdentity = custodianIdentity ?? holderIdentity,
                    staleAfterTick = staleAfterTick,
                    revision = 1,
                    spatialScope = spatialScope,
                    socialScope = socialScope,
                    siteAffiliation = siteAffiliation?.Copy()
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
            record.factKind = factKind;
            record.persistenceClass = persistenceClass;
            record.subjectIdentity = subjectIdentity ?? record.subjectIdentity
                ?? topic;
            record.immediateReporterIdentity = immediateReporterIdentity
                ?? record.immediateReporterIdentity ?? sourceIdentity;
            if (sourceEventTick >= 0)
                record.sourceEventTick = sourceEventTick;
            record.staleAfterTick = staleAfterTick;
            record.spatialScope = spatialScope ?? record.spatialScope;
            record.socialScope = socialScope ?? record.socialScope;
            if (factKind != CAKnowledgeFactKind.SiteAffiliation)
                record.siteAffiliation = null;
            else if (siteAffiliation != null)
                record.siteAffiliation = siteAffiliation.Copy();
            float independentCorroboration = CACulturalCognitionPureKernel
                .RecordIndependentKnowledgeSource(
                    record.corroboratingSources, sourceIdentity);
            record.sourceIdentity = sourceIdentity;
            record.immediateReporterIdentity = immediateReporterIdentity
                ?? sourceIdentity;
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
            record.lastReportedTick = tick;
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

        internal IReadOnlyList<CAKnowledgePropositionRecord> ForPawn(
            Pawn pawn)
        {
            if (pawn == null) return Array.Empty<
                CAKnowledgePropositionRecord>();
            string holder = "pawn:" + pawn.thingIDNumber;
            return propositions.Where(value => value != null
                    && value.holderIdentity == holder)
                .OrderByDescending(value => value.sourceEventTick)
                .ThenByDescending(value => value.acquiredTick)
                .ThenBy(value => value.topic, StringComparer.Ordinal)
                .ToList();
        }

        internal CAKnowledgePropositionRecord RecordSiteAffiliation(
            Pawn holder, CARegionalSettlementRecord site, int tick,
            string acquisitionChannel = "direct observation",
            string sourceIdentity = null, int sourceEventTick = -1)
        {
            if (holder == null || site == null) return null;
            string siteIdentity = site.regionalId + "#" + site.slot;
            var payload = new CASiteAffiliationKnowledgePayload
            {
                siteIdentity = siteIdentity,
                ownership = site.factionLinks?.ownership
                    ?? CASiteFactionReferenceKind.None,
                ownerRegionalFactionKey = site.factionLinks
                    ?.ownerRegionalFactionKey ?? -1,
                ownerWorldFactionLoadId = site.factionLinks
                    ?.ownerWorldFactionLoadId ?? -1,
                support = site.factionLinks?.support
                    ?? CASiteFactionReferenceKind.None,
                supportRegionalFactionKey = site.factionLinks
                    ?.supportRegionalFactionKey ?? -1,
                supportWorldFactionLoadId = site.factionLinks
                    ?.supportWorldFactionLoadId ?? -1,
                populationRegionalFactionKeys = (site.populationGroups
                        ?? new List<CASettlementPopulationGroup>())
                    .Where(value => value?.factionKey >= 0)
                    .Select(value => value.factionKey).Distinct()
                    .OrderBy(value => value).ToList(),
                includesUnaffiliatedResidents = (site.populationGroups
                    ?? new List<CASettlementPopulationGroup>()).Any(value =>
                        value != null && (value.factionKey < 0
                            || value.kind
                                == CAPopulationGroupKind.Unaffiliated))
            };
            return RecordSiteAffiliation(holder, payload, tick,
                acquisitionChannel, sourceIdentity ?? siteIdentity,
                sourceEventTick >= 0 ? sourceEventTick
                    : Math.Max(site.firstMaterializationTick,
                        site.lastReconciliationTick) >= 0
                        ? Math.Max(site.firstMaterializationTick,
                            site.lastReconciliationTick) : tick);
        }

        internal CAKnowledgePropositionRecord RecordSiteAffiliation(
            Pawn holder, CASiteAffiliationKnowledgePayload payload, int tick,
            string acquisitionChannel, string sourceIdentity,
            int sourceEventTick)
        {
            if (holder == null || payload == null
                || !payload.ValidationFailure().NullOrEmpty()) return null;
            string holderIdentity = "pawn:" + holder.thingIDNumber;
            string signature = SiteAffiliationSignature(payload);
            List<CAKnowledgePropositionRecord> active = ForPawn(holder)
                .Where(value => value.factKind
                        == CAKnowledgeFactKind.SiteAffiliation
                    && value.subjectIdentity == payload.siteIdentity
                    && value.supersededByIdentity.NullOrEmpty())
                .OrderByDescending(value => value.revision)
                .ThenByDescending(value => value.acquiredTick)
                .ToList();
            CAKnowledgePropositionRecord current = active.FirstOrDefault();
            if (current != null && current.content == signature)
            {
                current.lastConfirmedTick = tick;
                return current;
            }
            int revision = (current?.revision ?? 0) + 1;
            string identity = "site-affiliation:" + payload.siteIdentity
                + ":holder:" + holder.thingIDNumber + ":revision:"
                + revision;
            bool direct = acquisitionChannel == "direct observation";
            var appraisal = new CAKnowledgeAcceptanceInput(
                direct ? 0.95f : 0.60f, direct ? 1f : 0.65f,
                0.55f, 0.35f, 0.35f, 0.75f, 0.50f, 0.70f,
                0.50f, direct ? 0.95f : 0.55f, 0.50f, 1f,
                CACulturalCognitionWorldComponent.Current
                    ?.ProfileFor(holder)?.epistemicVigilance ?? 0.5f,
                CACulturalCognitionWorldComponent.Current?.AttitudeFor(
                    holder, CACultureQuestionRegistry.NoveltyAcceptance)
                    ?.privateAttitude ?? 0f);
            CAKnowledgePropositionRecord record = Acquire(identity,
                "settlement affiliation", SiteAffiliationClaim(payload),
                signature, holderIdentity, sourceIdentity,
                direct ? "observed settlement" : "reported settlement",
                acquisitionChannel, appraisal,
                direct ? "visible inhabited site"
                    : "reported site affiliation",
                "site:" + payload.siteIdentity + ":event:"
                    + sourceEventTick, tick, decayRate: 0.0025f,
                custodianIdentity: holderIdentity,
                factKind: CAKnowledgeFactKind.SiteAffiliation,
                persistenceClass: CAKnowledgePersistenceClass.Durable,
                subjectIdentity: payload.siteIdentity,
                immediateReporterIdentity: direct ? holderIdentity
                    : sourceIdentity,
                sourceEventTick: sourceEventTick,
                staleAfterTick: tick + 60 * 60000,
                spatialScope: payload.siteIdentity,
                socialScope: holderIdentity,
                siteAffiliation: payload);
            if (record == null) return null;
            record.revision = revision;
            if (current != null)
            {
                record.supersedesIdentity = current.identity;
                if (current.content != record.content)
                    LinkExplicitContradiction(record.identity,
                        current.identity);
            }
            foreach (CAKnowledgePropositionRecord prior in active)
            {
                prior.supersededByIdentity = record.identity;
                prior.lastConfirmedTick = Math.Min(
                    prior.lastConfirmedTick, sourceEventTick);
            }
            return record;
        }

        private CAKnowledgePropositionRecord RecordRepresentedSiteFact(
            Pawn holder, CAKnowledgeFactKind factKind, string siteIdentity,
            string topic, string claim, string content, int sourceEventTick,
            int tick, CAKnowledgePersistenceClass persistenceClass =
                CAKnowledgePersistenceClass.Durable)
        {
            if (holder == null || siteIdentity.NullOrEmpty()
                || topic.NullOrEmpty() || claim.NullOrEmpty()) return null;
            string normalizedContent = content ?? "not recorded";
            List<CAKnowledgePropositionRecord> active = ForPawn(holder)
                .Where(value => value.factKind == factKind
                    && value.subjectIdentity == siteIdentity
                    && value.supersededByIdentity.NullOrEmpty())
                .OrderByDescending(value => value.revision)
                .ThenByDescending(value => value.acquiredTick).ToList();
            CAKnowledgePropositionRecord current = active.FirstOrDefault();
            if (current != null && current.content == normalizedContent)
            {
                current.lastConfirmedTick = tick;
                return current;
            }
            int revision = (current?.revision ?? 0) + 1;
            string identity = "site-fact:" + factKind + ":"
                + siteIdentity + ":holder:" + holder.thingIDNumber
                + ":revision:" + revision;
            string holderIdentity = "pawn:" + holder.thingIDNumber;
            var appraisal = new CAKnowledgeAcceptanceInput(
                0.92f, 1f, 0.55f, 0.35f, 0.35f, 0.80f, 0.55f,
                0.75f, 0.50f, 0.92f, 0.50f, 1f,
                CACulturalCognitionWorldComponent.Current
                    ?.ProfileFor(holder)?.epistemicVigilance ?? 0.5f,
                CACulturalCognitionWorldComponent.Current?.AttitudeFor(
                    holder, CACultureQuestionRegistry.NoveltyAcceptance)
                    ?.privateAttitude ?? 0f);
            CAKnowledgePropositionRecord record = Acquire(identity,
                topic, claim, normalizedContent, holderIdentity,
                siteIdentity, "observed settlement", "direct observation",
                appraisal, "visible represented site",
                "site:" + siteIdentity + ":event:" + sourceEventTick,
                tick, decayRate: 0.0025f,
                custodianIdentity: holderIdentity, factKind: factKind,
                persistenceClass: persistenceClass,
                subjectIdentity: siteIdentity,
                immediateReporterIdentity: holderIdentity,
                sourceEventTick: sourceEventTick,
                staleAfterTick: tick + 60 * 60000,
                spatialScope: siteIdentity, socialScope: holderIdentity);
            if (record == null) return null;
            record.revision = revision;
            if (current != null)
            {
                record.supersedesIdentity = current.identity;
                if (current.content != record.content)
                    LinkExplicitContradiction(record.identity,
                        current.identity);
            }
            foreach (CAKnowledgePropositionRecord prior in active)
                prior.supersededByIdentity = record.identity;
            return record;
        }

        internal CAKnowledgePropositionRecord Relay(
            CAKnowledgePropositionRecord source, Pawn teller, Pawn receiver,
            CommunicationChannel channel, int tick)
        {
            if (source == null || teller == null || receiver == null
                || source.holderIdentity != "pawn:" + teller.thingIDNumber
                || channel == CommunicationChannel.None) return null;
            string receiverIdentity = "pawn:" + receiver.thingIDNumber;
            List<CAKnowledgePropositionRecord> priorRecords =
                ForPawn(receiver).Where(value => value != null
                    && value.factKind == source.factKind
                    && value.subjectIdentity == source.subjectIdentity
                    && value.supersededByIdentity.NullOrEmpty()).ToList();
            string relayIdentity = "relay:" + source.identity
                + ":holder:" + receiver.thingIDNumber;
            float trust = Mathf.Clamp01((receiver.relations
                    ?.OpinionOf(teller) ?? 0) / 200f + 0.5f);
            var appraisal = new CAKnowledgeAcceptanceInput(
                source.confidence, trust, source.expertise,
                source.prestige, source.authority,
                source.motiveIntegrity, source.corroboration,
                source.plausibility, source.priorCongruence,
                channel == CommunicationChannel.Voice ? 0.70f : 0.80f,
                source.observedPayoff, source.conflictFreedom,
                CACulturalCognitionWorldComponent.Current
                    ?.ProfileFor(receiver)?.epistemicVigilance ?? 0.5f,
                CACulturalCognitionWorldComponent.Current?.AttitudeFor(
                    receiver, CACultureQuestionRegistry.NoveltyAcceptance)
                    ?.privateAttitude ?? 0f);
            CAKnowledgePropositionRecord received = Acquire(relayIdentity,
                source.topic, source.claim, source.content,
                receiverIdentity, source.sourceIdentity,
                "report of " + source.sourceType,
                channel.ToString().ToLowerInvariant(), appraisal,
                source.provenance, source.identity, tick,
                decayRate: source.decayRate,
                custodianIdentity: receiverIdentity,
                politicalAxisKey: source.politicalAxisKey,
                politicalOptionKey: source.politicalOptionKey,
                politicalSupport: source.politicalSupport,
                factKind: source.factKind,
                persistenceClass: source.persistenceClass,
                subjectIdentity: source.subjectIdentity,
                immediateReporterIdentity: "pawn:"
                    + teller.thingIDNumber,
                sourceEventTick: source.sourceEventTick,
                staleAfterTick: source.staleAfterTick,
                spatialScope: source.spatialScope,
                socialScope: receiverIdentity,
                siteAffiliation: source.siteAffiliation);
            if (received == null) return null;
            received.revision = source.revision;
            received.confidence = Math.Min(received.confidence,
                source.confidence * 0.95f);
            received.uncertainty = Math.Max(received.uncertainty,
                source.uncertainty);
            received.provenanceChain = new List<string>(
                source.provenanceChain ?? new List<string>());
            AddBounded(received.provenanceChain,
                "pawn:" + teller.thingIDNumber + " via "
                    + channel.ToString().ToLowerInvariant(),
                CACulturalCognitionPureKernel.MaxKnowledgeProvenance);
            foreach (CAKnowledgePropositionRecord other in priorRecords)
            {
                bool newerRevision = received.revision > other.revision
                    && received.sourceEventTick >= other.sourceEventTick;
                if (newerRevision)
                {
                    other.supersededByIdentity = received.identity;
                    if (received.supersedesIdentity.NullOrEmpty())
                        received.supersedesIdentity = other.identity;
                }
                else if (other.claim != received.claim
                    || other.content != received.content)
                    LinkExplicitContradiction(received.identity,
                        other.identity);
            }
            source.lastReportedTick = tick;
            return received;
        }

        private static string SiteAffiliationClaim(
            CASiteAffiliationKnowledgePayload payload)
        {
            string ownership = payload.ownership
                    == CASiteFactionReferenceKind.None
                ? "has no faction owner"
                : payload.ownership
                    == CASiteFactionReferenceKind.RegionalFaction
                    ? "is owned by regional faction "
                        + payload.ownerRegionalFactionKey
                    : "is owned by world faction "
                        + payload.ownerWorldFactionLoadId;
            return payload.siteIdentity + " " + ownership + ".";
        }

        private static string SiteAffiliationSignature(
            CASiteAffiliationKnowledgePayload payload)
        {
            return string.Join("|", payload.siteIdentity,
                payload.ownership, payload.ownerRegionalFactionKey,
                payload.ownerWorldFactionLoadId, payload.support,
                payload.supportRegionalFactionKey,
                payload.supportWorldFactionLoadId,
                string.Join(",", payload.populationRegionalFactionKeys
                    ?? new List<int>()),
                payload.includesUnaffiliatedResidents);
        }

        private sealed class CAKnowledgeRelayDelivery
        {
            internal CAKnowledgePropositionRecord source;
            internal Pawn teller;
            internal Pawn receiver;
            internal CommunicationChannel channel;
        }

        private void ObserveNearbySettlementAffiliations(int now)
        {
            CARegionalWorldComponent regional =
                CARegionalWorldComponent.Current;
            foreach (Map map in Find.Maps)
            {
                List<CARegionalSettlementRecord> sites = regional?.ForMap(map)
                    .Where(value => value != null
                        && value.localRect != CellRect.Empty).ToList()
                    ?? new List<CARegionalSettlementRecord>();
                List<CAFrontierHoldingPlan> holdings = FrontierHoldingsFor(
                    regional, map);
                if (sites.Count == 0 && holdings.Count == 0) continue;
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.Where(
                    value => value != null && !value.Dead && !value.Downed
                        && value.Awake() && value.RaceProps?.Humanlike == true))
                {
                    foreach (CARegionalSettlementRecord site in sites)
                        if (site.localRect.ExpandedBy(12)
                            .Contains(pawn.Position))
                        {
                            RecordSiteAffiliation(pawn, site, now);
                            ObserveRepresentedSiteFacts(pawn, site, now);
                        }
                    foreach (CAFrontierHoldingPlan holding in holdings)
                        if (holding.residentPawnIds?.Contains(
                                pawn.thingIDNumber) == true
                            || holding.site.IsValid
                                && holding.site.InHorDistOf(
                                    pawn.Position, 24f))
                            ObserveFrontierSiteFacts(pawn, holding, map, now);
                }
            }
        }

        private static List<CAFrontierHoldingPlan> FrontierHoldingsFor(
            CARegionalWorldComponent regional, Map map)
        {
            var result = new List<CAFrontierHoldingPlan>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            CARegionalPlan plan = regional?.FindRegionForMap(map);
            foreach (CAFrontierHoldingPlan holding in plan?.frontierHoldings
                ?? new List<CAFrontierHoldingPlan>())
            {
                if (holding?.materialized != true
                    || holding.materializedMapId != map.uniqueID) continue;
                string identity = "frontier:" + map.uniqueID + ":"
                    + holding.key;
                if (seen.Add(identity)) result.Add(holding);
            }
            foreach (CAFrontierHoldingPlan holding in
                CAOrganizationWorldComponent.Current?.FrontierHoldings
                    ?? Enumerable.Empty<CAFrontierHoldingPlan>())
            {
                if (holding?.materialized != true
                    || holding.materializedMapId != map.uniqueID) continue;
                string identity = "frontier:" + map.uniqueID + ":"
                    + holding.key;
                if (seen.Add(identity)) result.Add(holding);
            }
            return result;
        }

        private void ObserveFrontierSiteFacts(Pawn pawn,
            CAFrontierHoldingPlan holding, Map map, int now)
        {
            string siteIdentity = "frontier:" + map.uniqueID + ":"
                + holding.key;
            string name = holding.siteName.NullOrEmpty()
                ? holding.form == 1 ? "Frontier homestead"
                    : "Frontier cabin"
                : holding.siteName;
            List<CASettlementPopulationGroup> groups = holding
                .populationGroups ?? new List<CASettlementPopulationGroup>();
            var affiliation = new CASiteAffiliationKnowledgePayload
            {
                siteIdentity = siteIdentity,
                ownership = holding.factionLinks?.ownership
                    ?? CASiteFactionReferenceKind.None,
                ownerRegionalFactionKey = holding.factionLinks
                    ?.ownerRegionalFactionKey ?? -1,
                ownerWorldFactionLoadId = holding.factionLinks
                    ?.ownerWorldFactionLoadId ?? -1,
                support = holding.factionLinks?.support
                    ?? CASiteFactionReferenceKind.None,
                supportRegionalFactionKey = holding.factionLinks
                    ?.supportRegionalFactionKey ?? -1,
                supportWorldFactionLoadId = holding.factionLinks
                    ?.supportWorldFactionLoadId ?? -1,
                populationRegionalFactionKeys = groups.Where(value =>
                        value?.factionKey >= 0)
                    .Select(value => value.factionKey).Distinct()
                    .OrderBy(value => value).ToList(),
                includesUnaffiliatedResidents = groups.Any(value =>
                    value != null && (value.factionKey < 0
                        || value.kind == CAPopulationGroupKind.Unaffiliated))
            };
            int sourceTick = holding.firstMaterializationTick >= 0
                ? holding.firstMaterializationTick : now;
            RecordSiteAffiliation(pawn, affiliation, now,
                "direct observation", siteIdentity, sourceTick);
            RecordRepresentedSiteFact(pawn,
                CAKnowledgeFactKind.Geography, siteIdentity,
                "frontier location", name + " exists at the observed place.",
                "map " + map.uniqueID + "; tile " + holding.memberTileId
                    + "; cell " + holding.site.x + "," + holding.site.z,
                sourceTick, now);

            string population = string.Join("; ", groups
                .Where(value => value != null).OrderBy(value => value.key)
                .Select(value => value.share + "% "
                    + (value.label ?? value.kind.ToString())
                    + " [affiliation " + (value.factionKey < 0
                        ? "none" : value.factionKey.ToString()) + "]"));
            RecordRepresentedSiteFact(pawn,
                CAKnowledgeFactKind.SitePopulation, siteIdentity,
                "frontier population", name + " has the observed residents.",
                population, sourceTick, now);
            RecordRepresentedSiteFact(pawn, CAKnowledgeFactKind.Culture,
                siteIdentity, "frontier culture",
                name + " has the observed local culture.",
                CACultureModel.Summary(holding.localCulture), sourceTick,
                now);
            string ideoligions = string.Join("; ", groups
                .Where(value => value != null).OrderBy(value => value.key)
                .Select(value => value.nativeIdeoligionId < 0
                    ? "not recorded (" + value.share + "%)"
                    : "Ideoligion " + value.nativeIdeoligionId + " ("
                        + value.share + "%)"));
            RecordRepresentedSiteFact(pawn,
                CAKnowledgeFactKind.Ideoligion, siteIdentity,
                "frontier ideoligions",
                name + " has the observed Ideoligions.", ideoligions,
                sourceTick, now);
            RecordRepresentedSiteFact(pawn,
                CAKnowledgeFactKind.PoliticalOrder, siteIdentity,
                "frontier political order",
                name + " has the observed political order.",
                CAPoliticalBeliefsModel.Summary(
                    holding.localSociety?.politicalOrder), sourceTick, now);
            RecordRepresentedSiteFact(pawn,
                CAKnowledgeFactKind.TechnologicalKnowledge, siteIdentity,
                "frontier technological knowledge",
                name + " has the observed technological knowledge.",
                CATechnologicalKnowledgeModel.Summary(
                    holding.localSociety?.technologicalKnowledge),
                sourceTick, now,
                CAKnowledgePersistenceClass.Institutional);
            RecordRepresentedSiteFact(pawn,
                CAKnowledgeFactKind.Institution, siteIdentity,
                "frontier institutions",
                name + " has the observed institutions.",
                CAFactionStructureModel.Summary(
                    holding.localSociety?.institutions), sourceTick, now,
                CAKnowledgePersistenceClass.Institutional);
            ObserveOrganizationFacts(pawn, siteIdentity, name, sourceTick,
                now);
        }

        private void ObserveRepresentedSiteFacts(Pawn pawn,
            CARegionalSettlementRecord site, int now)
        {
            string siteIdentity = site.regionalId + "#" + site.slot;
            int sourceTick = Math.Max(site.firstMaterializationTick,
                site.lastReconciliationTick);
            if (sourceTick < 0) sourceTick = now;
            RecordRepresentedSiteFact(pawn,
                CAKnowledgeFactKind.Geography, siteIdentity,
                "settlement location",
                site.name + " exists at the observed place.",
                "map " + (pawn.Map?.uniqueID ?? -1) + "; tile "
                    + site.memberTileId + "; center "
                    + site.localRect.CenterCell.x + ","
                    + site.localRect.CenterCell.z,
                sourceTick, now);
            if ((site.layout?.roads?.Count ?? 0) > 0)
                RecordRepresentedSiteFact(pawn,
                    CAKnowledgeFactKind.Route, siteIdentity,
                    "settlement routes",
                    site.name + " has observed internal routes.",
                    site.layout.roads.Count + " represented road cells; "
                        + (site.layout.gates?.Count ?? 0) + " gates",
                    sourceTick, now);

            string population = string.Join("; ", (site.populationGroups
                    ?? new List<CASettlementPopulationGroup>())
                .Where(value => value != null)
                .OrderBy(value => value.key).Select(value =>
                    value.share + "% " + (value.label ?? value.kind.ToString())
                    + " [affiliation " + (value.factionKey < 0
                        ? "none" : value.factionKey.ToString()) + "]"));
            RecordRepresentedSiteFact(pawn,
                CAKnowledgeFactKind.SitePopulation, siteIdentity,
                "settlement population",
                site.name + " has the observed resident groups.",
                population, sourceTick, now);

            RecordRepresentedSiteFact(pawn, CAKnowledgeFactKind.Culture,
                siteIdentity, "settlement culture",
                site.name + " has the observed local culture.",
                CACultureModel.Summary(site.culture), sourceTick, now);

            string ideoligions = string.Join("; ", (site.populationGroups
                    ?? new List<CASettlementPopulationGroup>())
                .Where(value => value != null)
                .OrderBy(value => value.key).Select(value =>
                {
                    Ideo ideo = value.nativeIdeoligionId < 0 ? null
                        : Find.IdeoManager?.IdeosListForReading
                            ?.FirstOrDefault(item => item != null
                                && item.id == value.nativeIdeoligionId);
                    return (ideo?.name ?? (value.nativeIdeoligionId < 0
                            ? "not recorded" : "Ideoligion "
                                + value.nativeIdeoligionId))
                        + " (" + value.share + "%)";
                }));
            RecordRepresentedSiteFact(pawn,
                CAKnowledgeFactKind.Ideoligion, siteIdentity,
                "settlement ideoligions",
                site.name + " has the observed Ideoligions.",
                ideoligions, sourceTick, now);

            CAFactionState factionState = CAFactionStateWorldComponent
                .Current?.Find(site.faction);
            bool local = !CASiteState.HasOwner(site.factionLinks)
                || site.localSociety?.explicitLocalDivergence == true;
            CAPoliticalBeliefs political = local
                ? site.localSociety?.politicalOrder
                : factionState?.politicalBeliefs;
            CATechnologicalKnowledge knowledge = local
                ? site.localSociety?.technologicalKnowledge
                : factionState?.technologicalKnowledge;
            List<CAAxisEntry> institutions = local
                ? site.localSociety?.institutions
                : factionState?.factionStructure;
            RecordRepresentedSiteFact(pawn,
                CAKnowledgeFactKind.PoliticalOrder, siteIdentity,
                "settlement political order",
                site.name + " has the observed political order.",
                CAPoliticalBeliefsModel.Summary(political), sourceTick, now);
            RecordRepresentedSiteFact(pawn,
                CAKnowledgeFactKind.TechnologicalKnowledge, siteIdentity,
                "settlement technological knowledge",
                site.name + " has the observed technological knowledge.",
                CATechnologicalKnowledgeModel.Summary(knowledge),
                sourceTick, now,
                CAKnowledgePersistenceClass.Institutional);
            RecordRepresentedSiteFact(pawn,
                CAKnowledgeFactKind.Institution, siteIdentity,
                "settlement institutions",
                site.name + " has the observed institutions.",
                CAFactionStructureModel.Summary(institutions),
                sourceTick, now,
                CAKnowledgePersistenceClass.Institutional);

            ObserveOrganizationFacts(pawn, siteIdentity, site.name,
                sourceTick, now);
        }

        private void ObserveOrganizationFacts(Pawn pawn,
            string siteIdentity, string siteName, int sourceTick, int now)
        {
            CAOrganization organization = CAOrganizationWorldComponent
                .Current?.ByKey(siteIdentity);
            if (organization == null) return;
            RecordRepresentedSiteFact(pawn,
                CAKnowledgeFactKind.Organization, siteIdentity,
                "settlement organization",
                siteName + " has the observed local organization.",
                (organization.name ?? siteName) + "; support "
                    + organization.publicSupport.ToString("0.00"),
                sourceTick, now,
                CAKnowledgePersistenceClass.Institutional);
            string offices = string.Join("; ", (organization.offices
                    ?? new List<CAOffice>()).Where(value => value != null)
                .OrderBy(value => value.seniority)
                .ThenBy(value => value.sourceKey, StringComparer.Ordinal)
                .Select(value => (value.name ?? "office") + ": "
                    + (value.holderLabel ?? (value.holderId < 0
                        ? "vacant" : "pawn " + value.holderId))));
            RecordRepresentedSiteFact(pawn,
                CAKnowledgeFactKind.Officeholder, siteIdentity,
                "settlement officeholders",
                siteName + " has the observed officeholders.", offices,
                sourceTick, now,
                CAKnowledgePersistenceClass.Institutional);
        }

        private void RelayBroaderPawnKnowledge(int now)
        {
            var deliveries = new List<CAKnowledgeRelayDelivery>();
            var scheduledReceivers = new HashSet<int>();
            foreach (Map map in Find.Maps)
            {
                List<Pawn> pawns = map.mapPawns.AllPawnsSpawned.Where(value =>
                        value != null && !value.Dead && !value.Downed
                        && value.Awake() && value.RaceProps?.Humanlike == true)
                    .ToList();
                foreach (Pawn teller in pawns)
                {
                    CAKnowledgePropositionRecord source = ForPawn(teller)
                        .Where(value => value != null
                            && !value.IsStale(now)
                            && value.supersededByIdentity.NullOrEmpty()
                            && value.transmissibility >= 0.25f
                            && value.lastReportedTick < now)
                        .OrderByDescending(value => value.confidence
                            * value.transmissibility)
                        .ThenBy(value => value.identity,
                            StringComparer.Ordinal).FirstOrDefault();
                    if (source == null) continue;
                    foreach (Pawn receiver in pawns)
                    {
                        if (receiver == teller
                            || scheduledReceivers.Contains(
                                receiver.thingIDNumber)) continue;
                        if (!CommsModule.TryGetBroaderKnowledgeChannel(teller,
                                receiver, 18f,
                                out CommunicationChannel channel))
                            continue;
                        deliveries.Add(new CAKnowledgeRelayDelivery
                        {
                            source = source,
                            teller = teller,
                            receiver = receiver,
                            channel = channel
                        });
                        scheduledReceivers.Add(receiver.thingIDNumber);
                        break;
                    }
                }
            }
            // Sample every teller first. A report crosses one represented edge
            // per cadence and cannot become a same-tick truth broadcast.
            foreach (CAKnowledgeRelayDelivery delivery in deliveries)
                Relay(delivery.source, delivery.teller, delivery.receiver,
                    delivery.channel, now);
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
            record.uncertainty = Mathf.Clamp01(1f - accepted.Confidence
                + (1f - record.conflictFreedom) * 0.25f);
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
                custodianIdentity: work.operatorIdentity,
                factKind: CAKnowledgeFactKind.Research,
                persistenceClass: CAKnowledgePersistenceClass.Institutional,
                subjectIdentity: work.programKey,
                immediateReporterIdentity: "pawn:" + worker.thingIDNumber,
                sourceEventTick: tick,
                spatialScope: work.settlementKey,
                socialScope: work.operatorIdentity);
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
                    custodianIdentity: "player research program",
                    factKind: CAKnowledgeFactKind.Research,
                    persistenceClass:
                        CAKnowledgePersistenceClass.Institutional,
                    subjectIdentity: project.defName,
                    immediateReporterIdentity: "player research program",
                    sourceEventTick: now,
                    socialScope: "player research program");
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
                        custodianIdentity: organization.organizationKey,
                        factKind: CAKnowledgeFactKind.Institution,
                        persistenceClass:
                            CAKnowledgePersistenceClass.Institutional,
                        subjectIdentity: organization.organizationKey,
                        immediateReporterIdentity:
                            organization.organizationKey,
                        sourceEventTick: decision.tick,
                        socialScope: organization.organizationKey);
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
            foreach (CAKnowledgePropositionRecord value in propositions
                .Where(value => value != null))
            {
                if (!value.supersedesIdentity.NullOrEmpty())
                    referenced.Add(value.supersedesIdentity);
                if (!value.supersededByIdentity.NullOrEmpty())
                    referenced.Add(value.supersededByIdentity);
                if (value.persistenceClass
                    >= CAKnowledgePersistenceClass.Durable)
                    referenced.Add(value.identity);
            }
            propositions.RemoveAll(value => value == null
                || (value.persistenceClass
                        == CAKnowledgePersistenceClass.Transient
                    && value.IsStale(now)
                    && !referenced.Contains(value.identity)));
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
            if (id < 0) return null;
            try
            {
                return PawnsFinder.AllMapsWorldAndTemporary_Alive
                    .FirstOrDefault(value => value?.thingIDNumber == id);
            }
            catch
            {
                // Saved proposition validation and contradiction repair can
                // run while no live game/world pawn registry exists. The
                // record's stored appraisal remains authoritative until a
                // represented pawn is actually available again.
                return null;
            }
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
