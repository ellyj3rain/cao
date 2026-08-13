using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    public sealed class CAPsychologyEvidenceRecord : IExposable
    {
        public string sourceFeature;
        public string construct;
        public float meanShift;
        public float uncertaintyChange;
        public string scope;
        public string provenance;

        public void ExposeData()
        {
            Scribe_Values.Look(ref sourceFeature, "sourceFeature");
            Scribe_Values.Look(ref construct, "construct");
            Scribe_Values.Look(ref meanShift, "meanShift", 0f);
            Scribe_Values.Look(ref uncertaintyChange,
                "uncertaintyChange", 0f);
            Scribe_Values.Look(ref scope, "scope");
            Scribe_Values.Look(ref provenance, "provenance");
        }
    }

    public sealed class CAPsychologyConstructUncertainty : IExposable
    {
        public string construct;
        public float uncertainty = 0.35f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref construct, "construct");
            Scribe_Values.Look(ref uncertainty, "uncertainty", 0.35f);
        }
    }

    public sealed class CAPsychologicalDynamicState : IExposable
    {
        public float mood = 0.5f;
        public float pain;
        public float fatigue;
        public float fear;
        public float anger;
        public float grief;
        public float scarcityPerception;
        public float personalThreat;
        public float groupThreat;
        public float recentSuccess;
        public float humiliation;
        public float cognitiveLoad;
        public int observedTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref mood, "mood", 0.5f);
            Scribe_Values.Look(ref pain, "pain", 0f);
            Scribe_Values.Look(ref fatigue, "fatigue", 0f);
            Scribe_Values.Look(ref fear, "fear", 0f);
            Scribe_Values.Look(ref anger, "anger", 0f);
            Scribe_Values.Look(ref grief, "grief", 0f);
            Scribe_Values.Look(ref scarcityPerception,
                "scarcityPerception", 0f);
            Scribe_Values.Look(ref personalThreat, "personalThreat", 0f);
            Scribe_Values.Look(ref groupThreat, "groupThreat", 0f);
            Scribe_Values.Look(ref recentSuccess, "recentSuccess", 0f);
            Scribe_Values.Look(ref humiliation, "humiliation", 0f);
            Scribe_Values.Look(ref cognitiveLoad, "cognitiveLoad", 0f);
            Scribe_Values.Look(ref observedTick, "observedTick", -1);
        }
    }

    public sealed class CAPsychologicalProfile : IExposable
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public int pawnId = -1;
        public float honestyHumility = 0.5f;
        public float emotionality = 0.5f;
        public float extraversion = 0.5f;
        public float agreeableness = 0.5f;
        public float conscientiousness = 0.5f;
        public float openness = 0.5f;
        public float reactance = 0.5f;
        public float needForClosure = 0.5f;
        public float empathicConcern = 0.5f;
        public float personalDistress = 0.5f;
        public float epistemicVigilance = 0.5f;
        public float statusSeeking = 0.5f;
        public float dangerousWorldBelief = 0.5f;
        public float competitiveWorldBelief = 0.5f;
        public float domainRiskTolerance = 0.5f;
        public float sourceTrust = 0.5f;
        public float groupIdentification = 0.5f;
        public float uncertainty = 0.35f;
        public List<CAPsychologyConstructUncertainty>
            constructUncertainties =
                new List<CAPsychologyConstructUncertainty>();
        public int mappingVersion = 1;
        public int establishedTick = -1;
        public List<CAPsychologyEvidenceRecord> evidence =
            new List<CAPsychologyEvidenceRecord>();
        public CAPsychologicalDynamicState dynamicState =
            new CAPsychologicalDynamicState();

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref pawnId, "pawnId", -1);
            Scribe_Values.Look(ref honestyHumility, "honestyHumility", 0.5f);
            Scribe_Values.Look(ref emotionality, "emotionality", 0.5f);
            Scribe_Values.Look(ref extraversion, "extraversion", 0.5f);
            Scribe_Values.Look(ref agreeableness, "agreeableness", 0.5f);
            Scribe_Values.Look(ref conscientiousness,
                "conscientiousness", 0.5f);
            Scribe_Values.Look(ref openness, "openness", 0.5f);
            Scribe_Values.Look(ref reactance, "reactance", 0.5f);
            Scribe_Values.Look(ref needForClosure, "needForClosure", 0.5f);
            Scribe_Values.Look(ref empathicConcern,
                "empathicConcern", 0.5f);
            Scribe_Values.Look(ref personalDistress,
                "personalDistress", 0.5f);
            Scribe_Values.Look(ref epistemicVigilance,
                "epistemicVigilance", 0.5f);
            Scribe_Values.Look(ref statusSeeking, "statusSeeking", 0.5f);
            Scribe_Values.Look(ref dangerousWorldBelief,
                "dangerousWorldBelief", 0.5f);
            Scribe_Values.Look(ref competitiveWorldBelief,
                "competitiveWorldBelief", 0.5f);
            Scribe_Values.Look(ref domainRiskTolerance,
                "domainRiskTolerance", 0.5f);
            Scribe_Values.Look(ref sourceTrust, "sourceTrust", 0.5f);
            Scribe_Values.Look(ref groupIdentification,
                "groupIdentification", 0.5f);
            Scribe_Values.Look(ref uncertainty, "uncertainty", 0.35f);
            Scribe_Collections.Look(ref constructUncertainties,
                "constructUncertainties", LookMode.Deep);
            Scribe_Values.Look(ref mappingVersion, "mappingVersion", 1);
            Scribe_Values.Look(ref establishedTick, "establishedTick", -1);
            Scribe_Collections.Look(ref evidence, "evidence", LookMode.Deep);
            Scribe_Deep.Look(ref dynamicState, "dynamicState");
        }
    }

    public sealed class CAPawnCulturalAttitude : IExposable
    {
        public const int CurrentSchemaVersion = 2;
        public int schemaVersion = CurrentSchemaVersion;
        public int pawnId = -1;
        public string cultureId;
        public string subgroupId;
        public string questionKey;
        public float privateAttitude;
        public float attention;
        public float moralConviction;
        public float identityCentrality;
        public float inheritedPriorStrength;
        public float knowledgeConfidence;
        public float perceivedDescriptiveNorm;
        public float perceivedInjunctiveNorm;
        public float perceivedSocialPressure;
        public float expectedEnforcement;
        public float publicExpression;
        public float observationLikelihood = 0.65f;
        // Status/prestige attached to the represented cultural position. It
        // modifies social referent weight, never the private position itself.
        public float prestigeSignal;
        public float uncertainty;
        public string provenance;
        public string sourceDistributionSignature;
        public string sourceIdeoligionSignature;
        public int materializationEpoch;
        public int lastUpdatedTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0,
                forceSave: true);
            Scribe_Values.Look(ref pawnId, "pawnId", -1);
            Scribe_Values.Look(ref cultureId, "cultureId");
            Scribe_Values.Look(ref subgroupId, "subgroupId");
            Scribe_Values.Look(ref questionKey, "questionKey");
            Scribe_Values.Look(ref privateAttitude, "privateAttitude", 0f,
                forceSave: true);
            Scribe_Values.Look(ref attention, "attention", 0f,
                forceSave: true);
            Scribe_Values.Look(ref moralConviction, "moralConviction", 0f,
                forceSave: true);
            Scribe_Values.Look(ref identityCentrality,
                "identityCentrality", 0f, forceSave: true);
            Scribe_Values.Look(ref inheritedPriorStrength,
                "inheritedPriorStrength", 0f, forceSave: true);
            Scribe_Values.Look(ref knowledgeConfidence,
                "knowledgeConfidence", 0f, forceSave: true);
            Scribe_Values.Look(ref perceivedDescriptiveNorm,
                "perceivedDescriptiveNorm", 0f, forceSave: true);
            Scribe_Values.Look(ref perceivedInjunctiveNorm,
                "perceivedInjunctiveNorm", 0f, forceSave: true);
            Scribe_Values.Look(ref perceivedSocialPressure,
                "perceivedSocialPressure", 0f, forceSave: true);
            Scribe_Values.Look(ref expectedEnforcement,
                "expectedEnforcement", 0f, forceSave: true);
            Scribe_Values.Look(ref publicExpression,
                "publicExpression", 0f, forceSave: true);
            Scribe_Values.Look(ref observationLikelihood,
                "observationLikelihood", 0.65f, forceSave: true);
            Scribe_Values.Look(ref prestigeSignal, "prestigeSignal", 0f,
                forceSave: true);
            Scribe_Values.Look(ref uncertainty, "uncertainty", 0f,
                forceSave: true);
            Scribe_Values.Look(ref provenance, "provenance");
            Scribe_Values.Look(ref sourceDistributionSignature,
                "sourceDistributionSignature");
            Scribe_Values.Look(ref sourceIdeoligionSignature,
                "sourceIdeoligionSignature");
            Scribe_Values.Look(ref materializationEpoch,
                "materializationEpoch", 0);
            Scribe_Values.Look(ref lastUpdatedTick, "lastUpdatedTick", -1);
        }
    }

    public sealed class CAInfluenceExposureRecord : IExposable
    {
        public string subjectKey;
        public int lastObservedTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref subjectKey, "subjectKey");
            Scribe_Values.Look(ref lastObservedTick,
                "lastObservedTick", -1);
        }
    }

    public sealed class CASocialInfluenceEdge : IExposable
    {
        public int sourcePawnId = -1;
        public int targetPawnId = -1;
        public string edgeType;
        public float weight;
        public float trust;
        public float prestige;
        public float conformity;
        public float payoffVisibility;
        public List<CAInfluenceExposureRecord> exposures =
            new List<CAInfluenceExposureRecord>();
        public int lastContactTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref sourcePawnId, "sourcePawnId", -1);
            Scribe_Values.Look(ref targetPawnId, "targetPawnId", -1);
            Scribe_Values.Look(ref edgeType, "edgeType");
            Scribe_Values.Look(ref weight, "weight", 0f);
            Scribe_Values.Look(ref trust, "trust", 0f);
            Scribe_Values.Look(ref prestige, "prestige", 0f);
            Scribe_Values.Look(ref conformity, "conformity", 0f);
            Scribe_Values.Look(ref payoffVisibility,
                "payoffVisibility", 0f);
            Scribe_Collections.Look(ref exposures,
                "exposures", LookMode.Deep);
            Scribe_Values.Look(ref lastContactTick,
                "lastContactTick", -1);
        }
    }

    public sealed class CACulturalBehaviorAppraisal
    {
        public int pawnId = -1;
        public string behaviorKey;
        public string questionKey;
        public CACulturalBehaviorResponse response;
        public float support;
        public float privatePosition;
        public float perceivedNorm;
        public float knowledgeConfidence;
        public float institutionalLegitimacy = 0.5f;
        public float dynamicCapacity = 0.5f;
        public string cause;
    }

    public static class CACulturalAttitudeKernel
    {
        public static CAPawnCulturalAttitude Materialize(int pawnId,
            string worldIdentity, CACulture culture, string subgroupId,
            CACultureQuestionDistribution distribution,
            CAPsychologicalProfile psychology, float doctrinePressure,
            string doctrineSource, string organizationIdentity, int epoch,
            int tick)
        {
            float privateValue = CACultureDistributionKernel.Materialize(
                distribution, worldIdentity, culture?.id, subgroupId,
                pawnId.ToString(), epoch);
            psychology = psychology ?? new CAPsychologicalProfile();
            CAAttitudeMaterializationResult result =
                CACulturalCognitionPureKernel.Materialize(
                    new CAAttitudeMaterializationInput(privateValue,
                        distribution.mean,
                        distribution.hasDescriptiveNormPrior
                            ? distribution.descriptiveNormPrior
                            : distribution.mean,
                        distribution.normStrength,
                        distribution.toleranceForDivergence,
                        distribution.salience,
                        distribution.sourceConfidence,
                        distribution.visibility, doctrinePressure,
                        psychology.agreeableness,
                        psychology.groupIdentification,
                        psychology.reactance,
                        psychology.epistemicVigilance,
                        CAPsychologyRuntime.UncertaintyForQuestion(
                            psychology, distribution.questionKey),
                        CAPsychologyRuntime.PositionShiftForQuestion(
                            psychology, distribution.questionKey),
                        EvidenceConfidenceFor(pawnId,
                            distribution.questionKey, culture),
                        RepresentedEnforcementFor(distribution.questionKey,
                            organizationIdentity),
                        MoralExperienceFor(pawnId,
                            distribution.questionKey, culture)));
            return new CAPawnCulturalAttitude
            {
                pawnId = pawnId,
                cultureId = culture?.id,
                subgroupId = subgroupId,
                questionKey = distribution.questionKey,
                privateAttitude = result.PrivatePosition,
                attention = result.Attention,
                moralConviction = result.MoralConviction,
                identityCentrality = result.IdentityCentrality,
                inheritedPriorStrength = result.InheritedPriorStrength,
                knowledgeConfidence = result.KnowledgeConfidence,
                perceivedDescriptiveNorm = result.DescriptiveNorm,
                perceivedInjunctiveNorm = result.InjunctiveNorm,
                perceivedSocialPressure = result.PerceivedSocialPressure,
                expectedEnforcement = result.ExpectedEnforcement,
                publicExpression = result.PublicExpression,
                observationLikelihood = result.ObservationLikelihood,
                prestigeSignal = distribution.prestigeSignal,
                uncertainty = result.Uncertainty,
                provenance = distribution.provenance
                    + (doctrineSource == null ? ""
                        : "; doctrine=" + doctrineSource),
                sourceDistributionSignature =
                    CACultureDistributionKernel.Fingerprint(distribution),
                sourceIdeoligionSignature = doctrineSource + ":"
                    + doctrinePressure.ToString("0.0000",
                        System.Globalization.CultureInfo.InvariantCulture),
                materializationEpoch = epoch,
                lastUpdatedTick = tick
            };
        }

        public static void Influence(CAPawnCulturalAttitude target,
            IEnumerable<(CAPawnCulturalAttitude attitude,
                CASocialInfluenceEdge edge)> neighbors, float openness,
            int tick)
        {
            if (target == null) return;
            IEnumerable<CACulturalInfluenceSample> samples =
                (neighbors ?? Enumerable.Empty<(
                    CAPawnCulturalAttitude, CASocialInfluenceEdge)>())
                .Where(value => value.attitude != null && value.edge != null)
                .Where(value => Observed(value.attitude, target,
                    value.edge, tick))
                .Select(value => new CACulturalInfluenceSample(
                    value.attitude.publicExpression, value.edge.weight,
                    value.edge.trust, Mathf.Clamp01(
                        value.edge.prestige * 0.65f
                        + (value.attitude.prestigeSignal + 1f) * 0.175f),
                    value.edge.conformity, value.edge.payoffVisibility));
            // Friedkin-Johnsen persistence: private position moves slowly;
            // represented local evidence updates perceived norms faster.
            CACulturalInfluenceResult result =
                CACulturalCognitionPureKernel.Influence(
                    target.privateAttitude,
                    target.perceivedDescriptiveNorm,
                    target.perceivedInjunctiveNorm,
                    target.expectedEnforcement,
                    target.observationLikelihood,
                    openness, samples);
            target.privateAttitude = result.PrivatePosition;
            target.perceivedDescriptiveNorm = result.DescriptiveNorm;
            target.perceivedInjunctiveNorm = result.InjunctiveNorm;
            target.publicExpression = result.PublicExpression;
            target.lastUpdatedTick = tick;
        }

        private static bool Observed(CAPawnCulturalAttitude source,
            CAPawnCulturalAttitude target, CASocialInfluenceEdge edge,
            int tick)
        {
            float likelihood = Mathf.Clamp01(source.observationLikelihood);
            if (likelihood <= 0f) return false;
            if (likelihood >= 1f) return true;
            string hash = CASocialPatternKernel.StableHash(
                source.pawnId + "|" + target.pawnId + "|"
                    + source.questionKey + "|" + edge.edgeType + "|"
                    + tick / 2500);
            uint sample = Convert.ToUInt32(hash, 16);
            return (sample & 0x00FFFFFFu) / 16777215f <= likelihood;
        }

        internal static float EvidenceConfidenceFor(int pawnId,
            string questionKey, CACulture culture = null)
        {
            IEnumerable<CARepresentedSourceAppraisal> direct =
                DirectQuestionEvidenceFor(pawnId, questionKey, culture)
                .Select(observation => CACulturalCognitionPureKernel
                    .RepresentedQuestionSourceAppraisal(
                        observation.questionDispersion,
                        QuestionParticipation(observation),
                        observation.observationCount));
            IEnumerable<CARepresentedSourceAppraisal> social =
                (CACulturalCognitionWorldComponent.Current
                    ?.InfluenceEdgesForTarget(pawnId)
                    ?? Array.Empty<CASocialInfluenceEdge>())
                .Where(edge => edge?.exposures?.Any(value => value != null
                    && value.subjectKey == questionKey) == true)
                .OrderByDescending(edge => edge.lastContactTick)
                .Select(edge => new CARepresentedSourceAppraisal(
                    edge.weight, edge.trust, edge.prestige,
                    edge.payoffVisibility));
            return CACulturalCognitionPureKernel
                .RepresentedEvidenceConfidence(direct.Concat(social));
        }

        internal static float MoralExperienceFor(int pawnId,
            string questionKey, CACulture culture = null)
        {
            CACultureQuestionDef definition =
                CACultureQuestionRegistry.Find(questionKey);
            var subjects = new HashSet<string>(
                definition?.SocialSubjectAdapters ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            IEnumerable<CARepresentedMoralExperience> direct =
                DirectQuestionEvidenceFor(pawnId, questionKey, culture)
                .Select(observation => CACulturalCognitionPureKernel
                    .RepresentedQuestionMoralExperience(
                        observation.questionPosition,
                        observation.questionDispersion,
                        QuestionParticipation(observation),
                        observation.observationCount));
            IEnumerable<CARepresentedMoralExperience> social =
                (CASocialReactionWorldComponent.Current
                    ?.ReactionsForPawn(pawnId)
                    ?? Array.Empty<CASocialReactionRecord>())
                .Where(value => value != null
                    && subjects.Contains(value.subjectKey))
                .OrderByDescending(value => value.tick).Take(12)
                .Select(value => new CARepresentedMoralExperience(
                    Mathf.Clamp01(value.salience / 100f),
                    Mathf.Clamp01(Math.Abs(value.approval) / 100f),
                    KnowledgeSourceConfidence(value.knowledgeSource)));
            return CACulturalCognitionPureKernel
                .RepresentedMoralExperience(direct.Concat(social));
        }

        private static IEnumerable<CACultureObservation>
            DirectQuestionEvidenceFor(int pawnId, string questionKey,
                CACulture culture)
        {
            string key = "question:" + (questionKey ?? "");
            return (culture?.observations
                    ?? new List<CACultureObservation>())
                .Where(value => value != null
                    && value.sourceDomain == "Culture question evidence"
                    && value.key == key && value.hasQuestionEvidence
                    && value.observationCount > 0
                    && CACulturalCognitionPureKernel
                        .ReceivesRepresentedQuestionEvidence(pawnId,
                            value.sourceSignature,
                            QuestionParticipation(value)));
        }

        private static float QuestionParticipation(
            CACultureObservation observation)
        {
            if (observation == null || observation.eligiblePopulation < 1)
                return 0f;
            return Mathf.Clamp01(observation.observedPawnCount
                / (float)observation.eligiblePopulation);
        }

        private static float KnowledgeSourceConfidence(string source)
        {
            if (source.NullOrEmpty()) return 0.35f;
            if (source.IndexOf("direct", StringComparison.OrdinalIgnoreCase)
                    >= 0
                || source.Equals("Firsthand",
                    StringComparison.OrdinalIgnoreCase)
                || source.Equals("Witnessed",
                    StringComparison.OrdinalIgnoreCase))
                return 0.90f;
            return 0.55f;
        }

        internal static float RepresentedEnforcementFor(string questionKey,
            string organizationIdentity)
        {
            CACultureQuestionDef definition =
                CACultureQuestionRegistry.Find(questionKey);
            if (definition?.SocialSubjectAdapters == null
                || definition.SocialSubjectAdapters.Length == 0)
                return 0f;
            var subjects = new HashSet<string>(
                definition.SocialSubjectAdapters, StringComparer.Ordinal);
            CAOrganization organization = organizationIdentity.NullOrEmpty()
                ? null : CAOrganizationWorldComponent.Current
                    ?.ByKey(organizationIdentity);
            List<CAInstitutionSanctionAppraisal> evidence = organization
                ?.sanctionAppraisals?.Where(value => value != null
                    && subjects.Contains(value.subjectKey))
                .OrderByDescending(value => value.tick).Take(12).ToList();
            if (evidence == null || evidence.Count == 0) return 0f;
            return Mathf.Clamp01(evidence.Average(value =>
                value.visibility * 0.45f + value.consistency * 0.35f
                    + value.deterrence * 0.20f));
        }
    }

    public sealed class CACulturalCognitionWorldComponent : WorldComponent
    {
        public const int CurrentSchemaVersion = 2;
        private const int InfluenceLifetimeTicks = 10 * 60000;
        // The manifest stores this owner's schema version, not the campaign
        // boundary version. They happened to share value 1 in B12; B13's
        // schema-2 owner must emit 2 or its own manifest fails preflight.
        private int campaignSchemaVersion = CurrentSchemaVersion;
        private int schemaVersion = CurrentSchemaVersion;
        private int nextSocialTick = 2500;
        private int nextInfluenceCleanupTick = 60000;
        private int nextLongTick = 600000;
        private int pawnCursor;
        private List<CAPsychologicalProfile> psychologicalProfiles =
            new List<CAPsychologicalProfile>();
        private List<CAPawnCulturalAttitude> culturalAttitudes =
            new List<CAPawnCulturalAttitude>();
        private List<CASocialInfluenceEdge> influenceEdges =
            new List<CASocialInfluenceEdge>();
        private Dictionary<int, CAPsychologicalProfile> profileByPawn;
        private Dictionary<string, CAPawnCulturalAttitude> attitudeByIdentity;
        private Dictionary<int, List<CASocialInfluenceEdge>> edgesByTarget;
        private List<Pawn> livePawnSnapshot;
        private Dictionary<int, Pawn> pawnById;

        public CACulturalCognitionWorldComponent(World world) : base(world) { }

        internal static CACulturalCognitionWorldComponent Current =>
            Find.World?.GetComponent<CACulturalCognitionWorldComponent>();

        internal IReadOnlyList<CAPsychologicalProfile> PsychologicalProfiles
            => psychologicalProfiles;
        internal IReadOnlyList<CAPawnCulturalAttitude> CulturalAttitudes
            => culturalAttitudes;
        internal IReadOnlyList<CASocialInfluenceEdge> InfluenceEdges
            => influenceEdges;

        internal IReadOnlyList<CASocialInfluenceEdge>
            InfluenceEdgesForTarget(int pawnId) => EdgesForTarget(pawnId);

        internal IReadOnlyList<Pawn> CognitionPawns()
        {
            EnsureRuntimeIndexes();
            return livePawnSnapshot;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref campaignSchemaVersion,
                "CA_culturalCognitionOwnerVersion",
                0, forceSave: true);
            bool readable = CACampaignCompatibility.ShouldReadLiveState(
                "world.cultural-cognition", campaignSchemaVersion, 0);
            if (readable)
            {
                Scribe_Values.Look(ref schemaVersion,
                    "CA_culturalCognitionSchemaVersion",
                    CurrentSchemaVersion);
                Scribe_Values.Look(ref nextSocialTick,
                    "CA_culturalCognitionNextSocialTick", 2500);
                Scribe_Values.Look(ref nextInfluenceCleanupTick,
                    "CA_culturalCognitionNextInfluenceCleanupTick", 60000);
                Scribe_Values.Look(ref nextLongTick,
                    "CA_culturalCognitionNextLongTick", 600000);
                Scribe_Values.Look(ref pawnCursor,
                    "CA_culturalCognitionPawnCursor", 0);
                Scribe_Collections.Look(ref psychologicalProfiles,
                    "CA_psychologicalProfiles", LookMode.Deep);
                Scribe_Collections.Look(ref culturalAttitudes,
                    "CA_culturalAttitudes", LookMode.Deep);
                Scribe_Collections.Look(ref influenceEdges,
                    "CA_socialInfluenceEdges", LookMode.Deep);
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && readable)
            {
                CACampaignCompatibility.CompleteOwnerLoad(
                    "world.cultural-cognition", ref campaignSchemaVersion, 0,
                    ValidateCampaignState, BootstrapMissingState);
                RebuildRuntimeIndexes();
            }
            base.ExposeData();
        }

        private string BootstrapMissingState()
        {
            if (schemaVersion == 0)
                schemaVersion = CurrentSchemaVersion;
            if (psychologicalProfiles == null)
                psychologicalProfiles = new List<CAPsychologicalProfile>();
            if (culturalAttitudes == null)
                culturalAttitudes = new List<CAPawnCulturalAttitude>();
            if (influenceEdges == null)
                influenceEdges = new List<CASocialInfluenceEdge>();
            RebuildRuntimeIndexes();
            return ValidateCampaignState();
        }

        private string ValidateCampaignState()
        {
            if (schemaVersion != CurrentSchemaVersion)
                return "cultural-cognition schema is " + schemaVersion
                    + ", expected " + CurrentSchemaVersion;
            if (psychologicalProfiles == null || culturalAttitudes == null
                || influenceEdges == null)
                return "cultural-cognition collection is missing";
            if (psychologicalProfiles.Any(value => value == null
                    || value.schemaVersion
                        != CAPsychologicalProfile.CurrentSchemaVersion
                    || value.mappingVersion != 1
                    || value.pawnId < 0 || value.evidence == null
                    || value.evidence.Count == 0
                    || value.constructUncertainties == null
                    || value.dynamicState == null
                    || !ValidProfile(value)))
                return "a psychological profile is incomplete";
            if (psychologicalProfiles.GroupBy(value => value.pawnId)
                    .Any(group => group.Count() > 1))
                return "a psychological profile identity is duplicated";
            if (culturalAttitudes.Any(value => value == null
                    || value.schemaVersion
                        != CAPawnCulturalAttitude.CurrentSchemaVersion
                    || value.pawnId < 0
                    || CACultureQuestionRegistry.Find(value.questionKey)
                        == null || value.cultureId.NullOrEmpty()
                    || value.subgroupId.NullOrEmpty()
                    || !ValidAttitude(value)))
                return "a pawn cultural attitude is incomplete";
            if (culturalAttitudes.GroupBy(value => value.pawnId + "\0"
                        + value.questionKey + "\0" + (value.cultureId ?? "")
                        + "\0" + (value.subgroupId ?? "*"),
                        StringComparer.Ordinal)
                    .Any(group => group.Count() > 1))
                return "a pawn cultural attitude is duplicated";
            if (influenceEdges.Any(value => value == null
                    || value.sourcePawnId < 0 || value.targetPawnId < 0
                    || value.sourcePawnId == value.targetPawnId
                    || value.edgeType.NullOrEmpty()
                    || value.exposures == null
                    || value.exposures.Count > CACultureQuestionRegistry
                        .All.Count + CAFactionAxes.Axes.Length
                    || value.exposures.Any(exposure =>
                        exposure == null
                        || !ValidExposureSubject(exposure.subjectKey)
                        || exposure.lastObservedTick < -1)
                    || value.exposures.GroupBy(exposure =>
                            exposure.subjectKey, StringComparer.Ordinal)
                        .Any(group => group.Count() > 1)
                    || value.lastContactTick < -1
                    || !Unit(value.weight) || !Unit(value.trust)
                    || !Unit(value.prestige) || !Unit(value.conformity)
                    || !Unit(value.payoffVisibility)))
                return "a social influence edge is incomplete";
            if (influenceEdges.GroupBy(value => value.sourcePawnId + "\0"
                        + value.targetPawnId, StringComparer.Ordinal)
                    .Any(group => group.Count() > 1))
                return "a social influence edge is duplicated";
            if (influenceEdges.GroupBy(value => value.targetPawnId)
                    .Any(group => group.Count()
                        > CACulturalCognitionPureKernel
                            .MaxInfluenceEdgesPerPawn))
                return "a pawn has too many social influence edges";
            return null;
        }

        private static bool ValidProfile(CAPsychologicalProfile value)
        {
            return new[]
                {
                    value.honestyHumility, value.emotionality,
                    value.extraversion, value.agreeableness,
                    value.conscientiousness, value.openness, value.reactance,
                    value.needForClosure, value.empathicConcern,
                    value.personalDistress, value.epistemicVigilance,
                    value.statusSeeking, value.dangerousWorldBelief,
                    value.competitiveWorldBelief, value.domainRiskTolerance,
                    value.sourceTrust, value.groupIdentification,
                    value.uncertainty, value.dynamicState.mood,
                    value.dynamicState.pain, value.dynamicState.fatigue,
                    value.dynamicState.fear, value.dynamicState.anger,
                    value.dynamicState.grief,
                    value.dynamicState.scarcityPerception,
                    value.dynamicState.personalThreat,
                    value.dynamicState.groupThreat,
                    value.dynamicState.recentSuccess,
                    value.dynamicState.humiliation,
                    value.dynamicState.cognitiveLoad
                }.All(Unit)
                && value.mappingVersion == 1
                && value.evidence != null && value.evidence.Count > 0
                && value.constructUncertainties != null
                && value.constructUncertainties.Count
                    == CAPsychologyRuntime.ConstructKeys.Length
                && value.constructUncertainties.All(item => item != null
                    && CAPsychologyRuntime.ConstructKeys.Contains(
                        item.construct, StringComparer.Ordinal)
                    && Unit(item.uncertainty))
                && value.constructUncertainties.GroupBy(item =>
                        item.construct, StringComparer.Ordinal)
                    .All(group => group.Count() == 1)
                && value.evidence.All(item => item != null
                    && !item.sourceFeature.NullOrEmpty()
                    && !item.construct.NullOrEmpty()
                    && !item.scope.NullOrEmpty()
                    && !item.provenance.NullOrEmpty()
                    && Finite(item.meanShift)
                    && Finite(item.uncertaintyChange));
        }

        private static bool ValidAttitude(CAPawnCulturalAttitude value)
        {
            return new[]
                {
                    value.privateAttitude, value.perceivedDescriptiveNorm,
                    value.perceivedInjunctiveNorm, value.publicExpression,
                    value.prestigeSignal
                }.All(Signed)
                && new[]
                {
                    value.moralConviction, value.identityCentrality,
                    value.attention, value.inheritedPriorStrength,
                    value.knowledgeConfidence,
                    value.perceivedSocialPressure,
                    value.expectedEnforcement,
                    value.observationLikelihood, value.uncertainty
                }.All(Unit);
        }

        private static bool Unit(float value)
        {
            return Finite(value) && value >= 0f && value <= 1f;
        }

        private static bool Signed(float value)
        {
            return Finite(value) && value >= -1f && value <= 1f;
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool ValidExposureSubject(string subjectKey)
        {
            if (CACultureQuestionRegistry.Find(subjectKey) != null)
                return true;
            const string prefix = "politics:";
            return subjectKey?.StartsWith(prefix,
                    StringComparison.Ordinal) == true
                && CAFactionAxes.AxisDef(subjectKey.Substring(
                    prefix.Length)) != null;
        }

        public override void WorldComponentTick()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now >= nextSocialTick)
            {
                nextSocialTick = now + 2500;
                SocialTick(now);
            }
            if (now >= nextInfluenceCleanupTick)
            {
                nextInfluenceCleanupTick = now + 60000;
                PruneExpiredInfluence(now);
            }
            if (now >= nextLongTick)
            {
                nextLongTick = now + 10 * 60000;
                LongTick(now);
            }
        }

        internal CAPsychologicalProfile ProfileFor(Pawn pawn)
        {
            if (pawn == null) return null;
            EnsureRuntimeIndexes();
            TrackPawn(pawn);
            profileByPawn.TryGetValue(pawn.thingIDNumber,
                out CAPsychologicalProfile profile);
            if (profile == null)
            {
                profile = CAPsychologyRuntime.Build(pawn,
                    CAPlayerFoundingSession.WorldIdentity());
                psychologicalProfiles.Add(profile);
                profileByPawn[pawn.thingIDNumber] = profile;
            }
            CAPsychologyRuntime.UpdateDynamic(profile, pawn,
                Find.TickManager?.TicksGame ?? 0);
            return profile;
        }

        internal CAPawnCulturalAttitude AttitudeFor(Pawn pawn,
            string questionKey)
        {
            if (pawn == null || questionKey.NullOrEmpty()) return null;
            CACultureRuntimeContext context = CASocialReactionWorldComponent
                .ContextFor(pawn);
            return AttitudeFor(pawn, questionKey, context?.Culture,
                context?.PopulationIdentity,
                context?.InstitutionalOrganizationIdentity);
        }

        private CAPawnCulturalAttitude AttitudeFor(Pawn pawn,
            string questionKey, CACulture culture, string subgroup,
            string organizationIdentity)
        {
            if (pawn == null || questionKey.NullOrEmpty() || culture == null)
                return null;
            string cultureId = culture.id ?? "unrecorded culture";
            string subgroupId = subgroup ?? "*";
            CACultureQuestionDistribution distribution = CACultureModel
                .DistributionFor(culture, questionKey, subgroup);
            if (distribution == null) return null;
            float doctrine = CACultureIdeoligionAdapter.Pressure(pawn.Ideo,
                questionKey, out string doctrineSource);
            string distributionSignature = CACultureDistributionKernel
                .Fingerprint(distribution);
            string doctrineSignature = doctrineSource + ":"
                + doctrine.ToString("0.0000",
                    System.Globalization.CultureInfo.InvariantCulture);
            EnsureRuntimeIndexes();
            string attitudeIdentity = AttitudeIdentity(pawn.thingIDNumber,
                questionKey, cultureId, subgroupId);
            attitudeByIdentity.TryGetValue(attitudeIdentity,
                out CAPawnCulturalAttitude existing);
            if (existing != null
                && existing.sourceDistributionSignature
                    == distributionSignature
                && existing.sourceIdeoligionSignature == doctrineSignature)
                return existing;
            if (existing != null)
            {
                RefreshAttitude(existing, culture, distribution,
                    ProfileFor(pawn), doctrine, doctrineSource,
                    distributionSignature,
                    doctrineSignature, organizationIdentity,
                    Find.TickManager?.TicksGame ?? 0);
                return existing;
            }
            existing = CACulturalAttitudeKernel.Materialize(
                pawn.thingIDNumber,
                CAPlayerFoundingSession.WorldIdentity(), culture, subgroup,
                distribution, ProfileFor(pawn), doctrine, doctrineSource,
                organizationIdentity,
                epoch: CAPendingAuthoringDataEpoch.Current,
                Find.TickManager?.TicksGame ?? 0);
            culturalAttitudes.Add(existing);
            attitudeByIdentity[attitudeIdentity] = existing;
            return existing;
        }

        private static string AttitudeIdentity(int pawnId,
            string questionKey, string cultureId, string subgroupId) =>
            pawnId + "\0" + (questionKey ?? "") + "\0"
                + (cultureId ?? "unrecorded culture") + "\0"
                + (subgroupId ?? "*");

        private void EnsureRuntimeIndexes()
        {
            if (profileByPawn == null || attitudeByIdentity == null
                || edgesByTarget == null || livePawnSnapshot == null
                || pawnById == null)
                RebuildRuntimeIndexes();
        }

        private void RebuildRuntimeIndexes()
        {
            profileByPawn = (psychologicalProfiles
                    ?? new List<CAPsychologicalProfile>())
                .Where(value => value != null && value.pawnId >= 0)
                .GroupBy(value => value.pawnId)
                .ToDictionary(group => group.Key, group => group.First());
            attitudeByIdentity = (culturalAttitudes
                    ?? new List<CAPawnCulturalAttitude>())
                .Where(value => value != null).GroupBy(value =>
                    AttitudeIdentity(value.pawnId, value.questionKey,
                        value.cultureId, value.subgroupId),
                    StringComparer.Ordinal).ToDictionary(group => group.Key,
                    group => group.First(), StringComparer.Ordinal);
            edgesByTarget = (influenceEdges
                    ?? new List<CASocialInfluenceEdge>())
                .Where(value => value != null && value.targetPawnId >= 0)
                .GroupBy(value => value.targetPawnId)
                .ToDictionary(group => group.Key, group => group.ToList());
            livePawnSnapshot = PawnsFinder.AllMapsWorldAndTemporary_Alive
                .Where(value => value != null && value.RaceProps.Humanlike)
                .OrderBy(value => value.thingIDNumber).ToList();
            pawnById = livePawnSnapshot.GroupBy(value => value.thingIDNumber)
                .ToDictionary(group => group.Key, group => group.First());
        }

        private void TrackPawn(Pawn pawn)
        {
            if (pawn == null || !pawn.RaceProps.Humanlike) return;
            if (pawnById.TryGetValue(pawn.thingIDNumber,
                    out Pawn existing) && existing == pawn) return;
            pawnById[pawn.thingIDNumber] = pawn;
            livePawnSnapshot.RemoveAll(value => value == null
                || value.thingIDNumber == pawn.thingIDNumber);
            int insert = livePawnSnapshot.BinarySearch(pawn,
                Comparer<Pawn>.Create((left, right) =>
                    left.thingIDNumber.CompareTo(right.thingIDNumber)));
            livePawnSnapshot.Insert(insert < 0 ? ~insert : insert, pawn);
        }

        internal void NotifyPawnAvailable(Pawn pawn)
        {
            EnsureRuntimeIndexes();
            TrackPawn(pawn);
        }

        internal void NotifyPawnDestroyed(Pawn pawn)
        {
            if (pawn == null) return;
            EnsureRuntimeIndexes();
            int pawnId = pawn.thingIDNumber;
            livePawnSnapshot.RemoveAll(value => value == null
                || value.thingIDNumber == pawnId);
            pawnById.Remove(pawnId);
            profileByPawn.Remove(pawnId);
            psychologicalProfiles.RemoveAll(value => value == null
                || value.pawnId == pawnId);
            culturalAttitudes.RemoveAll(value => value == null
                || value.pawnId == pawnId);
            attitudeByIdentity = null;
            influenceEdges.RemoveAll(value => value == null
                || value.sourcePawnId == pawnId || value.targetPawnId == pawnId);
            edgesByTarget = null;
        }

        private Pawn PawnByIdIndexed(int pawnId)
        {
            EnsureRuntimeIndexes();
            pawnById.TryGetValue(pawnId, out Pawn pawn);
            return pawn;
        }

        private List<CASocialInfluenceEdge> EdgesForTarget(int pawnId)
        {
            EnsureRuntimeIndexes();
            if (!edgesByTarget.TryGetValue(pawnId,
                    out List<CASocialInfluenceEdge> edges))
            {
                edges = new List<CASocialInfluenceEdge>();
                edgesByTarget[pawnId] = edges;
            }
            return edges;
        }

        private static void RefreshAttitude(CAPawnCulturalAttitude target,
            CACulture culture, CACultureQuestionDistribution distribution,
            CAPsychologicalProfile psychology, float doctrine,
            string doctrineSource, string distributionSignature,
            string doctrineSignature, string organizationIdentity, int tick)
        {
            psychology = psychology ?? new CAPsychologicalProfile();
            CAAttitudeMaterializationResult result =
                CACulturalCognitionPureKernel.Materialize(
                    new CAAttitudeMaterializationInput(
                        target.privateAttitude, distribution.mean,
                        distribution.hasDescriptiveNormPrior
                            ? distribution.descriptiveNormPrior
                            : distribution.mean,
                        distribution.normStrength,
                        distribution.toleranceForDivergence,
                        distribution.salience,
                        distribution.sourceConfidence,
                        distribution.visibility, doctrine,
                        psychology.agreeableness,
                        psychology.groupIdentification,
                        psychology.reactance,
                        psychology.epistemicVigilance,
                        CAPsychologyRuntime.UncertaintyForQuestion(
                            psychology, distribution.questionKey), 0f,
                        CACulturalAttitudeKernel.EvidenceConfidenceFor(
                            target.pawnId, distribution.questionKey,
                            culture),
                        CACulturalAttitudeKernel.RepresentedEnforcementFor(
                            distribution.questionKey,
                            organizationIdentity),
                        CACulturalAttitudeKernel.MoralExperienceFor(
                            target.pawnId, distribution.questionKey,
                            culture)));
            target.attention = result.Attention;
            target.moralConviction = result.MoralConviction;
            target.identityCentrality = result.IdentityCentrality;
            target.inheritedPriorStrength = result.InheritedPriorStrength;
            target.knowledgeConfidence = result.KnowledgeConfidence;
            target.perceivedDescriptiveNorm = result.DescriptiveNorm;
            target.perceivedInjunctiveNorm = result.InjunctiveNorm;
            target.perceivedSocialPressure = result.PerceivedSocialPressure;
            target.expectedEnforcement = result.ExpectedEnforcement;
            target.publicExpression = result.PublicExpression;
            target.observationLikelihood = result.ObservationLikelihood;
            target.prestigeSignal = distribution.prestigeSignal;
            target.uncertainty = result.Uncertainty;
            target.provenance = distribution.provenance
                + (doctrineSource == null ? ""
                    : "; doctrine=" + doctrineSource);
            target.sourceDistributionSignature = distributionSignature;
            target.sourceIdeoligionSignature = doctrineSignature;
            target.lastUpdatedTick = tick;
        }

        internal CACulturalMeaningResolution ResolveFor(Pawn pawn,
            CACulture culture, string subjectKey, string populationScope,
            string institutionalOrganizationIdentity)
        {
            string questionKey = CACultureQuestionRegistry
                .QuestionForSocialSubject(subjectKey);
            CAPawnCulturalAttitude attitude = AttitudeFor(pawn, questionKey,
                culture, populationScope,
                institutionalOrganizationIdentity);
            if (attitude == null)
                return CACultureModel.Resolve(culture, subjectKey,
                    populationScope);
            int direction = CACultureQuestionRegistry
                .DirectionForSocialSubject(subjectKey);
            var result = new CACulturalMeaningResolution
            {
                SubjectKey = subjectKey,
                Approval = Mathf.RoundToInt(attitude.privateAttitude
                    * direction * 100f),
                Normality = Mathf.RoundToInt((attitude
                    .perceivedDescriptiveNorm * direction + 1f) * 50f),
                Prestige = Mathf.RoundToInt(attitude.prestigeSignal
                    * direction * 100f),
                Salience = Mathf.RoundToInt(attitude.moralConviction * 100f),
                Dissonance = Mathf.Clamp01(Mathf.Abs(
                    attitude.privateAttitude - attitude.publicExpression)),
                Confidence = attitude.knowledgeConfidence
            };
            result.Provenance.Add(attitude.provenance ?? "pawn attitude");
            result.Contributions.Add(new CACulturalMeaningContribution
            {
                PopulationScope = populationScope,
                Provenance = "pawn attitude",
                SourceIdentity = pawn.thingIDNumber.ToString(),
                Weight = Mathf.Max(1,
                    Mathf.RoundToInt(attitude.knowledgeConfidence * 100f)),
                Approval = result.Approval,
                Normality = result.Normality,
                Prestige = result.Prestige,
                Salience = result.Salience
            });
            return result;
        }

        internal DispositionProfile DispositionFor(Pawn pawn)
        {
            return CAPsychologyRuntime.Project(ProfileFor(pawn), pawn);
        }

        internal CACulturalBehaviorAppraisal AppraiseBehavior(Pawn pawn,
            string behaviorKey, CABehaviorContext context)
        {
            string questionKey = CAQuestionConsumerMap.ForBehavior(
                behaviorKey);
            CACultureRuntimeContext culturalContext =
                CASocialReactionWorldComponent.ContextFor(pawn);
            CAPawnCulturalAttitude attitude = AttitudeFor(pawn, questionKey,
                culturalContext?.Culture, culturalContext?.PopulationIdentity,
                culturalContext?.InstitutionalOrganizationIdentity);
            if (attitude == null) return null;
            CAPsychologicalProfile profile = ProfileFor(pawn);
            float perceivedNorm = Mathf.Clamp(
                attitude.perceivedDescriptiveNorm * 0.45f
                    + attitude.perceivedInjunctiveNorm * 0.55f, -1f, 1f);
            float legitimacy = InstitutionLegitimacyFor(
                culturalContext?.InstitutionalOrganizationIdentity);
            float knowledgeConfidence = Mathf.Clamp01(
                attitude.knowledgeConfidence * 0.55f
                    + context.KnowledgeConfidence * 0.45f);
            float dynamicCapacity = DynamicCapacity(profile?.dynamicState);
            float convictionWeight = 0.35f
                + attitude.moralConviction * 0.35f
                + attitude.identityCentrality * 0.15f;
            float support = Mathf.Clamp(
                attitude.privateAttitude * convictionWeight
                    + perceivedNorm * 0.20f
                    + (legitimacy * 2f - 1f) * 0.10f
                    + (knowledgeConfidence * 2f - 1f) * 0.05f,
                -1f, 1f);
            return new CACulturalBehaviorAppraisal
            {
                pawnId = pawn.thingIDNumber,
                behaviorKey = behaviorKey,
                questionKey = questionKey,
                response = CACulturalCognitionPureKernel
                    .SelectCulturalResponse(new CACulturalResponseInput(
                        support, attitude.expectedEnforcement,
                        attitude.publicExpression,
                        attitude.privateAttitude,
                        profile?.reactance ?? 0.5f,
                        attitude.identityCentrality, legitimacy,
                        dynamicCapacity,
                        profile?.dynamicState?.anger ?? 0f)),
                support = support,
                privatePosition = attitude.privateAttitude,
                perceivedNorm = perceivedNorm,
                knowledgeConfidence = knowledgeConfidence,
                institutionalLegitimacy = legitimacy,
                dynamicCapacity = dynamicCapacity,
                cause = "private position, perceived norm, conviction, "
                    + "knowledge confidence, institution legitimacy, and "
                    + "current psychological state"
            };
        }

        private static float InstitutionLegitimacyFor(
            string organizationIdentity)
        {
            CAOrganization organization = organizationIdentity.NullOrEmpty()
                ? null : CAOrganizationWorldComponent.Current
                    ?.ByKey(organizationIdentity);
            CAInstitutionLegitimacyAppraisal appraisal = organization
                ?.legitimacyAppraisals?.Where(value => value != null)
                .OrderByDescending(value => value.lastUpdatedTick)
                .FirstOrDefault();
            return Mathf.Clamp01(appraisal?.legitimacy
                ?? organization?.publicSupport ?? 0.5f);
        }

        private static float DynamicCapacity(
            CAPsychologicalDynamicState state)
        {
            if (state == null) return 0.5f;
            float burden = (state.pain + state.fatigue + state.fear
                + state.grief + state.cognitiveLoad) / 5f;
            return Mathf.Clamp01(state.mood * 0.40f
                + state.recentSuccess * 0.25f
                + (1f - burden) * 0.35f);
        }

        private void SocialTick(int now)
        {
            using (CAModuleProfiler.Measure(
                CAModuleProfileKey.CulturalCognition))
            {
            EnsureRuntimeIndexes();
            List<Pawn> pawns = livePawnSnapshot;
            if (pawns.Count == 0) return;
            int count = Math.Min(12, pawns.Count);
            int objectsExamined = count;
            for (int offset = 0; offset < count; offset++)
            {
                Pawn pawn = pawns[(pawnCursor + offset) % pawns.Count];
                if (pawn == null || pawn.DestroyedOrNull()
                    || !pawn.RaceProps.Humanlike) continue;
                ProfileFor(pawn);
                EnsureEdges(pawn, now);
                foreach (CACultureQuestionDef definition in
                    CACultureQuestionRegistry.All)
                {
                    CAPawnCulturalAttitude attitude = AttitudeFor(pawn,
                        definition.Key);
                    if (attitude == null) continue;
                    List<CASocialInfluenceEdge> inbound = EdgesForTarget(
                        pawn.thingIDNumber);
                    objectsExamined += inbound.Count;
                    IEnumerable<(CAPawnCulturalAttitude,
                        CASocialInfluenceEdge)> neighbors = inbound
                        .Where(edge => edge != null
                            && HasRecentExposure(edge, definition.Key, now))
                        .Select(edge =>
                        {
                            Pawn source = PawnByIdIndexed(edge.sourcePawnId);
                            return (AttitudeFor(source, definition.Key), edge);
                        })
                        .Where(value => value.Item1 != null);
                    CACulturalAttitudeKernel.Influence(attitude, neighbors,
                        ProfileFor(pawn).openness, now);
                }
            }
            pawnCursor = (pawnCursor + count) % pawns.Count;
            CAModuleProfiler.Observe(CAModuleProfileKey.CulturalCognition,
                objectsExamined: objectsExamined,
                candidatesAccepted: count);
            }
        }

        private void LongTick(int now)
        {
            // Long cadence maintains only already represented state. It does
            // not infer institutions, doctrines, or historical events.
            List<Pawn> livePawns = PawnsFinder
                .AllMapsWorldAndTemporary_Alive.Where(value => value != null
                    && value.RaceProps.Humanlike)
                .OrderBy(value => value.thingIDNumber).ToList();
            var livePawnIds = new HashSet<int>(livePawns.Select(value =>
                value.thingIDNumber));
            psychologicalProfiles.RemoveAll(value => value == null
                || !livePawnIds.Contains(value.pawnId));
            culturalAttitudes.RemoveAll(value => value == null
                || !livePawnIds.Contains(value.pawnId));
            influenceEdges.RemoveAll(value => value == null
                || !livePawnIds.Contains(value.sourcePawnId)
                || !livePawnIds.Contains(value.targetPawnId));
            PruneExpiredInfluence(now);
            var currentIdentities = new HashSet<string>(StringComparer.Ordinal);
            foreach (Pawn pawn in livePawns)
            {
                CACultureRuntimeContext context = CASocialReactionWorldComponent
                    .ContextFor(pawn);
                CACulture culture = context?.Culture;
                string subgroup = context?.PopulationIdentity;
                if (culture == null) continue;
                foreach (CACultureQuestionDef question in
                    CACultureQuestionRegistry.All)
                    currentIdentities.Add(AttitudeIdentity(
                        pawn.thingIDNumber, question.Key,
                        culture.id ?? "unrecorded culture", subgroup ?? "*"));
            }
            culturalAttitudes.RemoveAll(value => value != null
                && !currentIdentities.Contains(AttitudeIdentity(value.pawnId,
                    value.questionKey, value.cultureId, value.subgroupId)));
            RebuildRuntimeIndexes();
        }

        private void EnsureEdges(Pawn pawn, int now)
        {
            var desired = new Dictionary<int, (string type, float weight)>();
            foreach (CASocialInfluenceEdge observed in EdgesForTarget(
                pawn.thingIDNumber).Where(
                value => value != null
                    && HasAnyRecentExposure(value, now)))
                desired[observed.sourcePawnId] =
                    (observed.edgeType ?? "observed conduct",
                        Math.Max(0.60f, observed.weight));
            foreach (DirectPawnRelation relation in pawn.relations
                ?.DirectRelations ?? new List<DirectPawnRelation>())
                if (relation?.otherPawn != null
                    && relation.otherPawn.RaceProps.Humanlike)
                    desired[relation.otherPawn.thingIDNumber] =
                        ("relation:" + relation.def.defName, 1f);
            if (pawn.MapHeld != null)
            {
                // Verse's radial thing-grid query bounds examined cells by the
                // represented contact radius instead of by map population.
                List<Pawn> localPawns = GenRadial.RadialDistinctThingsAround(
                        pawn.Position, pawn.MapHeld, 12f, true)
                    .OfType<Pawn>().Where(value => value != pawn
                        && value.MapHeld == pawn.MapHeld
                        && value.RaceProps.Humanlike)
                    .OrderBy(value => value.Position.DistanceToSquared(
                        pawn.Position)).ToList();
                foreach (Pawn peer in localPawns.Where(value =>
                        value.Faction == pawn.Faction).Take(4))
                    if (!desired.ContainsKey(peer.thingIDNumber))
                        desired[peer.thingIDNumber] = ("local contact", 0.35f);
                CAPawnCulturalAttitude integration = AttitudeFor(pawn,
                    CACultureQuestionRegistry.IntegrationPreference);
                float integrationWeight = integration == null ? 0f
                    : Mathf.InverseLerp(-1f, 1f,
                        integration.publicExpression);
                if (integrationWeight > 0.10f)
                    foreach (Pawn peer in localPawns.Where(value =>
                            value.Faction != pawn.Faction
                            && (pawn.Faction == null || value.Faction == null
                                || !pawn.Faction.HostileTo(value.Faction)))
                        .Take(2))
                        if (!desired.ContainsKey(peer.thingIDNumber))
                            desired[peer.thingIDNumber] =
                                ("intergroup contact",
                                    0.10f + integrationWeight * 0.35f);
            }
            HashSet<int> retained = desired
                .OrderByDescending(value => value.Value.weight)
                .Take(8).Select(value => value.Key).ToHashSet();
            List<CASocialInfluenceEdge> targetEdges = EdgesForTarget(
                pawn.thingIDNumber);
            CASocialInfluenceEdge[] removed = targetEdges.Where(value =>
                value == null || !retained.Contains(value.sourcePawnId))
                .ToArray();
            // Global list compaction is owned by the 60,000-tick cleanup.
            // Until then, removed edges remain valid persisted records but
            // have no current target-bucket consumer; new edges reuse their
            // exact slots so a save cannot contain duplicate or ninth edges.
            var reusable = new Queue<CASocialInfluenceEdge>(removed.Where(
                value => value != null));
            foreach (KeyValuePair<int, (string type, float weight)> pair in
                desired.Where(value => retained.Contains(value.Key)))
            {
                CASocialInfluenceEdge edge = targetEdges.FirstOrDefault(
                    value => value != null
                        && value.sourcePawnId == pair.Key);
                Pawn source = PawnByIdIndexed(pair.Key);
                if (edge == null)
                {
                    bool reused = reusable.Count > 0;
                    edge = reused ? reusable.Dequeue()
                        : new CASocialInfluenceEdge();
                    edge.sourcePawnId = pair.Key;
                    edge.targetPawnId = pawn.thingIDNumber;
                    edge.exposures = new List<CAInfluenceExposureRecord>();
                    edge.lastContactTick = -1;
                    if (!reused)
                    {
                        influenceEdges.Add(edge);
                        targetEdges.Add(edge);
                    }
                }
                edge.edgeType = pair.Value.type;
                edge.weight = pair.Value.weight;
                edge.trust = source == null ? 0.5f : Mathf.Clamp01(
                    (pawn.relations?.OpinionOf(source) ?? 0) / 200f + 0.5f);
                edge.prestige = source?.Faction?.leader == source ? 1f
                    : source?.skills?.GetSkill(SkillDefOf.Social)?.Level
                        / 20f ?? 0.35f;
                edge.conformity = ProfileFor(pawn).agreeableness;
                edge.payoffVisibility = source?.MapHeld == pawn.MapHeld
                    ? 0.8f : 0.25f;
                edge.lastContactTick = now;
            }
            foreach (CASocialInfluenceEdge inactive in reusable)
            {
                inactive.edgeType = "inactive represented contact";
                inactive.weight = 0f;
                inactive.exposures?.Clear();
                inactive.lastContactTick = -1;
            }
        }

        internal void RecordQuestionExposure(Pawn observer, int sourcePawnId,
            string questionKey, int tick)
        {
            if (CACultureQuestionRegistry.Find(questionKey) == null) return;
            RecordExposure(observer, sourcePawnId, questionKey, tick);
        }

        // A represented event updates only the causal fields owned by that
        // evidence. It does not resample the pawn or reset socially learned
        // descriptive and injunctive norms.
        internal void RefreshRepresentedEvidence(Pawn pawn,
            string questionKey, int tick, CACulture culture = null,
            string institutionalOrganizationIdentity = null)
        {
            if (pawn == null
                || CACultureQuestionRegistry.Find(questionKey) == null)
                return;
            CACultureRuntimeContext context = CASocialReactionWorldComponent
                .ContextFor(pawn);
            culture = culture ?? context?.Culture;
            string subgroup = context?.PopulationIdentity;
            institutionalOrganizationIdentity =
                institutionalOrganizationIdentity
                ?? context?.InstitutionalOrganizationIdentity;
            CAPawnCulturalAttitude attitude = AttitudeFor(pawn, questionKey,
                culture, subgroup, institutionalOrganizationIdentity);
            if (attitude == null) return;
            CAPsychologicalProfile psychology = ProfileFor(pawn)
                ?? new CAPsychologicalProfile();
            float evidence = CACulturalAttitudeKernel.EvidenceConfidenceFor(
                pawn.thingIDNumber, questionKey, culture);
            float moralExperience = CACulturalAttitudeKernel
                .MoralExperienceFor(pawn.thingIDNumber, questionKey,
                    culture);
            float enforcement = CACulturalAttitudeKernel
                .RepresentedEnforcementFor(questionKey,
                    institutionalOrganizationIdentity);
            float doctrine = CACultureIdeoligionAdapter.Pressure(pawn.Ideo,
                questionKey, out _);
            float psychologicalUncertainty = CAPsychologyRuntime
                .UncertaintyForQuestion(psychology, questionKey);
            attitude.knowledgeConfidence = CACulturalCognitionPureKernel
                .KnowledgeConfidence(evidence, psychologicalUncertainty,
                    psychology.epistemicVigilance);
            attitude.uncertainty = CACulturalCognitionPureKernel
                .KnowledgeUncertainty(evidence, psychologicalUncertainty,
                    psychology.epistemicVigilance);
            attitude.moralConviction = CACulturalCognitionPureKernel
                .MoralConviction(attitude.privateAttitude, doctrine,
                    attitude.identityCentrality, moralExperience);
            attitude.expectedEnforcement = enforcement;
            attitude.publicExpression = CACulturalCognitionPureKernel
                .PublicExpression(attitude.privateAttitude,
                    attitude.perceivedInjunctiveNorm,
                    attitude.perceivedSocialPressure, enforcement,
                    psychology.agreeableness,
                    psychology.groupIdentification, psychology.reactance);
            attitude.lastUpdatedTick = tick >= 0 ? tick
                : Find.TickManager?.TicksGame ?? 0;
        }

        internal void RecordPoliticalExposure(Pawn observer,
            int sourcePawnId, string axisKey, int tick)
        {
            if (CAFactionAxes.AxisDef(axisKey) == null) return;
            RecordExposure(observer, sourcePawnId,
                PoliticalExposureKey(axisKey), tick);
        }

        internal bool HasRecentPoliticalExposure(CASocialInfluenceEdge edge,
            string axisKey, int now)
        {
            return HasRecentExposure(edge, PoliticalExposureKey(axisKey),
                now);
        }

        private static string PoliticalExposureKey(string axisKey) =>
            "politics:" + (axisKey ?? "");

        private void RecordExposure(Pawn observer, int sourcePawnId,
            string subjectKey, int tick)
        {
            if (observer == null || sourcePawnId < 0
                || sourcePawnId == observer.thingIDNumber
                || !ValidExposureSubject(subjectKey)) return;
            EnsureRuntimeIndexes();
            TrackPawn(observer);
            CASocialInfluenceEdge edge = EdgesForTarget(
                observer.thingIDNumber).FirstOrDefault(value =>
                value != null && value.sourcePawnId == sourcePawnId
                    && value.targetPawnId == observer.thingIDNumber);
            Pawn source = PawnByIdIndexed(sourcePawnId);
            if (edge == null)
            {
                edge = new CASocialInfluenceEdge
                {
                    sourcePawnId = sourcePawnId,
                    targetPawnId = observer.thingIDNumber,
                    edgeType = "observed conduct",
                    weight = 0.60f,
                    trust = source == null ? 0.5f : Mathf.Clamp01(
                        (observer.relations?.OpinionOf(source) ?? 0) / 200f
                            + 0.5f),
                    prestige = source?.Faction?.leader == source ? 1f
                        : source?.skills?.GetSkill(SkillDefOf.Social)?.Level
                            / 20f ?? 0.35f,
                    conformity = ProfileFor(observer).agreeableness,
                    payoffVisibility = source?.MapHeld == observer.MapHeld
                        ? 0.90f : 0.45f
                };
                influenceEdges.Add(edge);
                EdgesForTarget(observer.thingIDNumber).Add(edge);
            }
            int observedAt = tick >= 0
                ? tick : Find.TickManager?.TicksGame ?? 0;
            if (edge.exposures == null)
                edge.exposures = new List<CAInfluenceExposureRecord>();
            CAInfluenceExposureRecord exposure = edge.exposures
                .FirstOrDefault(value => value != null
                    && value.subjectKey == subjectKey);
            if (exposure == null)
            {
                exposure = new CAInfluenceExposureRecord
                    { subjectKey = subjectKey };
                edge.exposures.Add(exposure);
            }
            exposure.lastObservedTick = observedAt;
            edge.lastContactTick = observedAt;
            TrimInfluenceEdges(observer.thingIDNumber);
        }

        private void TrimInfluenceEdges(int targetPawnId)
        {
            CASocialInfluenceEdge[] overflow = influenceEdges.Where(value =>
                    value != null && value.targetPawnId == targetPawnId)
                .OrderByDescending(value => value.exposures?.Count
                    ?? 0)
                .ThenByDescending(value => value.weight)
                .ThenByDescending(LatestExposureTick)
                .ThenByDescending(value => value.lastContactTick)
                .Skip(CACulturalCognitionPureKernel
                    .MaxInfluenceEdgesPerPawn).ToArray();
            if (overflow.Length > 0)
            {
                influenceEdges.RemoveAll(overflow.Contains);
                EdgesForTarget(targetPawnId).RemoveAll(overflow.Contains);
            }
        }

        private static bool HasRecentExposure(CASocialInfluenceEdge edge,
            string questionKey, int now)
        {
            if (edge?.exposures == null
                || questionKey.NullOrEmpty()) return false;
            CAInfluenceExposureRecord exposure = edge.exposures
                .FirstOrDefault(value => value != null
                    && value.subjectKey == questionKey);
            return exposure != null && exposure.lastObservedTick >= 0
                && now - exposure.lastObservedTick <= InfluenceLifetimeTicks;
        }

        private static bool HasAnyRecentExposure(CASocialInfluenceEdge edge,
            int now)
        {
            return edge?.exposures?.Any(value => value != null
                && value.lastObservedTick >= 0
                && now - value.lastObservedTick
                    <= InfluenceLifetimeTicks) == true;
        }

        private static int LatestExposureTick(CASocialInfluenceEdge edge)
        {
            return edge?.exposures?.Where(value => value != null)
                .Select(value => value.lastObservedTick)
                .DefaultIfEmpty(-1).Max() ?? -1;
        }

        private static void PruneExposureRecords(CASocialInfluenceEdge edge,
            int now)
        {
            edge?.exposures?.RemoveAll(value => value == null
                || value.lastObservedTick < 0
                || now - value.lastObservedTick > InfluenceLifetimeTicks);
        }

        private void PruneExpiredInfluence(int now)
        {
            if (influenceEdges == null) return;
            foreach (CASocialInfluenceEdge edge in influenceEdges)
                PruneExposureRecords(edge, now);
            CASocialInfluenceEdge[] expired = influenceEdges.Where(value =>
                value == null || (value.lastContactTick >= 0
                    && now - Math.Max(value.lastContactTick,
                        LatestExposureTick(value)) > InfluenceLifetimeTicks)
                || (value.lastContactTick < 0
                    && LatestExposureTick(value) >= 0
                    && now - LatestExposureTick(value)
                        > InfluenceLifetimeTicks)
                || (value.lastContactTick < 0
                    && LatestExposureTick(value) < 0)).ToArray();
            if (expired.Length == 0) return;
            influenceEdges.RemoveAll(expired.Contains);
            foreach (List<CASocialInfluenceEdge> bucket in
                edgesByTarget?.Values ??
                    Enumerable.Empty<List<CASocialInfluenceEdge>>())
                bucket.RemoveAll(expired.Contains);
        }

        internal float InstitutionCulturalFitFor(
            CAOrganization organization,
            out string sourceSignature)
        {
            List<int> members = organization.memberPawnIds
                ?? new List<int>();
            if (members.Count == 0)
            {
                sourceSignature = "no represented member population";
                return 0.5f;
            }
            string[] keys =
            {
                CACultureQuestionRegistry.GenderDistribution,
                CACultureQuestionRegistry.HereditaryLegitimacy,
                CACultureQuestionRegistry.RankDifferentiation
            };
            var relevant = new List<CAPawnCulturalAttitude>();
            foreach (int member in members)
            {
                Pawn pawn = PawnByIdIndexed(member);
                if (pawn == null) continue;
                foreach (string key in keys)
                {
                    CAPawnCulturalAttitude attitude = AttitudeFor(pawn, key);
                    if (attitude != null) relevant.Add(attitude);
                }
            }
            float cultural = 0.5f;
            var sources = new List<string>();
            if (relevant.Count > 0)
            {
                var fits = new List<float>();
                AddFit(fits, sources, relevant,
                    CACultureQuestionRegistry.HereditaryLegitimacy,
                    organization.offices.Any(value => value != null
                        && (value.successionRule == "hereditary"
                            || value.kinSuccessions > 0)) ? 1f : -1f);
                AddFit(fits, sources, relevant,
                    CACultureQuestionRegistry.RankDifferentiation,
                    organization.offices.Any(value => value != null
                        && value.seniority > 0) ? 1f : -1f);
                List<Pawn> holders = organization.offices.Where(value =>
                        value != null && value.holderId >= 0)
                    .Select(value => PawnByIdIndexed(value.holderId))
                    .Where(value => value != null
                        && value.gender != Gender.None).ToList();
                if (holders.Count > 0)
                    AddFit(fits, sources, relevant,
                        CACultureQuestionRegistry.GenderDistribution,
                        holders.Average(value => value.gender == Gender.Female
                            ? 1f : -1f));
                if (fits.Count > 0) cultural = fits.Average();
            }
            sourceSignature = sources.Count == 0
                ? "no represented member attitudes for realized office facts"
                : string.Join("|", sources.OrderBy(value => value,
                    StringComparer.Ordinal));
            return cultural;
        }

        private static void AddFit(List<float> fits, List<string> sources,
            List<CAPawnCulturalAttitude> attitudes, string questionKey,
            float realized)
        {
            List<CAPawnCulturalAttitude> relevant = attitudes.Where(value =>
                value.questionKey == questionKey).ToList();
            if (relevant.Count == 0) return;
            float desired = relevant.Average(value =>
                value.publicExpression);
            fits.Add(CACulturalCognitionPureKernel.InstitutionFit(desired,
                realized));
            sources.Add(questionKey + ":desired="
                + desired.ToString("0.000") + ":realized="
                + realized.ToString("0.000") + ":n=" + relevant.Count);
        }

    }

    internal static class CAPsychologyRuntime
    {
        internal static readonly string[] ConstructKeys =
        {
            "honesty-humility", "emotionality", "extraversion",
            "agreeableness", "conscientiousness", "openness",
            "psychological reactance", "need for closure",
            "empathic concern", "personal distress",
            "epistemic vigilance", "status seeking",
            "dangerous-world belief", "competitive-world belief",
            "domain risk tolerance", "source trust",
            "group identification"
        };

        internal static CAPsychologicalProfile Build(Pawn pawn,
            string worldIdentity)
        {
            var profile = new CAPsychologicalProfile
            {
                pawnId = pawn.thingIDNumber,
                establishedTick = Find.TickManager?.TicksGame ?? 0
            };
            InitializeNeutral(profile);
            profile.evidence.Add(new CAPsychologyEvidenceRecord
            {
                sourceFeature = "neutral-prior",
                construct = "unobserved stable constructs",
                meanShift = 0f,
                uncertaintyChange = 0f,
                scope = "stable profile",
                provenance = "neutral prior; no represented evidence"
            });
            TraitSet traits = pawn.story?.traits;
            Evidence(profile, traits, "Kind", "agreeableness", 0.18f,
                value => value.agreeableness += 0.18f);
            Evidence(profile, traits, "Kind", "empathic concern", 0.18f,
                value => value.empathicConcern += 0.18f);
            Evidence(profile, traits, "Psychopath", "honesty-humility",
                -0.20f, value => value.honestyHumility -= 0.20f);
            Evidence(profile, traits, "Psychopath", "empathic concern",
                -0.35f, value => value.empathicConcern -= 0.35f);
            Evidence(profile, traits, "Bloodlust", "agreeableness", -0.22f,
                value => value.agreeableness -= 0.22f);
            Evidence(profile, traits, "Bloodlust", "competitive-world belief",
                0.25f, value => value.competitiveWorldBelief += 0.25f);
            Evidence(profile, traits, "Abrasive", "agreeableness", -0.16f,
                value => value.agreeableness -= 0.16f);
            Evidence(profile, traits, "Abrasive",
                "psychological reactance", 0.16f,
                value => value.reactance += 0.16f);
            Evidence(profile, traits, "TooSmart", "openness", 0.20f,
                value => value.openness += 0.20f);
            Evidence(profile, traits, "TooSmart", "epistemic vigilance",
                0.14f, value => value.epistemicVigilance += 0.14f);
            Evidence(profile, traits, "TooSmart",
                "psychological reactance", 0.10f,
                value => value.reactance += 0.10f);
            Evidence(profile, traits, "Wimp", "personal distress", 0.22f,
                value => value.personalDistress += 0.22f);
            Evidence(profile, traits, "Tough", "personal distress", -0.14f,
                value => value.personalDistress -= 0.14f);
            DegreeEvidence(profile, traits, "Industriousness",
                "conscientiousness", 0.09f,
                (value, degree) => value.conscientiousness += 0.09f * degree);
            DegreeEvidence(profile, traits, "Nerves", "emotionality",
                -0.09f,
                (value, degree) => value.emotionality -= 0.09f * degree);
            DegreeEvidence(profile, traits, "NaturalMood", "emotionality",
                -0.04f,
                (value, degree) => value.emotionality -= 0.04f * degree);
            if (pawn.Faction != null)
                ContextEvidence(profile, "faction:" + pawn.Faction.loadID,
                    "group identification", 0.08f,
                    value => value.groupIdentification += 0.08f,
                    "represented faction membership");
            int directTies = pawn.relations?.DirectRelations?.Count(value =>
                value?.otherPawn != null && value.otherPawn.RaceProps.Humanlike)
                ?? 0;
            if (directTies >= 2)
            {
                float tieShift = Math.Min(0.15f, directTies * 0.03f);
                ContextEvidence(profile, "direct-relations:" + directTies,
                    "group identification", tieShift,
                    value => value.groupIdentification += tieShift,
                    "represented direct social ties");
            }
            if (pawn.story?.Childhood != null)
            {
                profile.evidence.Add(new CAPsychologyEvidenceRecord
                {
                    sourceFeature = "backstory:"
                        + pawn.story.Childhood.identifier,
                    construct = "developmental-history evidence",
                    meanShift = 0f,
                    uncertaintyChange = 0f,
                    scope = "stable profile",
                    provenance = "native BackstoryDef retained as evidence; "
                        + "no unsupported generic personality inference"
                });
            }
            if (ModsConfig.BiotechActive && pawn.genes != null)
                foreach (Gene gene in pawn.genes.GenesListForReading.Where(
                    value => value?.def != null))
                    profile.evidence.Add(new CAPsychologyEvidenceRecord
                    {
                        sourceFeature = "gene:" + gene.def.defName,
                        construct = "gene evidence",
                        meanShift = 0f,
                        uncertaintyChange = 0f,
                        scope = "stable profile",
                        provenance = "native gene retained without invented "
                            + "cross-construct mapping"
                    });
            Clamp(profile);
            UpdateDynamic(profile, pawn, profile.establishedTick);
            return profile;
        }

        internal static void UpdateDynamic(CAPsychologicalProfile profile,
            Pawn pawn, int now)
        {
            if (profile == null || pawn == null) return;
            CAPsychologicalDynamicState state = profile.dynamicState
                ?? (profile.dynamicState = new CAPsychologicalDynamicState());
            state.mood = pawn.needs?.mood?.CurLevelPercentage ?? 0.5f;
            state.pain = pawn.health?.hediffSet?.PainTotal ?? 0f;
            state.fatigue = pawn.needs?.rest == null ? 0f
                : 1f - pawn.needs.rest.CurLevelPercentage;
            state.personalThreat = pawn.mindState?.enemyTarget != null
                ? 1f : 0f;
            state.groupThreat = pawn.MapHeld?.attackTargetsCache?.TargetsHostileToFaction(
                    pawn.Faction)?.Any() == true ? 0.75f : 0f;
            state.fear = Mathf.Clamp01(state.personalThreat * 0.6f
                + state.pain * 0.4f);
            state.anger = Mathf.Clamp01(state.personalThreat * 0.35f
                + state.groupThreat * 0.25f
                + (1f - state.mood) * 0.25f + state.pain * 0.15f);
            state.cognitiveLoad = Mathf.Clamp01(state.fatigue * 0.45f
                + state.pain * 0.35f + state.personalThreat * 0.45f);
            state.observedTick = now;
        }

        internal static DispositionProfile Project(
            CAPsychologicalProfile profile, Pawn pawn)
        {
            if (profile == null) return new DispositionProfile
            {
                courage = 0.5f, discipline = 0.5f, aggression = 0.5f,
                empathy = 0.5f, conformity = 0.5f, initiative = 0.5f,
                skepticism = 0.5f
            };
            CAPsychologicalDynamicState state = profile.dynamicState
                ?? new CAPsychologicalDynamicState();
            float selfEfficacy = 0.5f;
            if (pawn?.skills != null)
                selfEfficacy = Mathf.Clamp01((pawn.skills.GetSkill(
                        SkillDefOf.Melee).Level
                    + pawn.skills.GetSkill(SkillDefOf.Shooting).Level
                    + pawn.skills.GetSkill(SkillDefOf.Intellectual).Level)
                    / 60f);
            return new DispositionProfile
            {
                courage = Mathf.Clamp01(0.45f
                    + (1f - profile.emotionality) * 0.25f
                    + profile.domainRiskTolerance * 0.20f
                    + selfEfficacy * 0.15f - state.fear * 0.35f),
                discipline = Mathf.Clamp01(profile.conscientiousness * 0.70f
                    + (1f - state.cognitiveLoad) * 0.30f),
                aggression = Mathf.Clamp01(
                    profile.competitiveWorldBelief * 0.40f
                    + profile.statusSeeking * 0.25f
                    + profile.domainRiskTolerance * 0.20f
                    + state.anger * 0.20f),
                empathy = Mathf.Clamp01(profile.empathicConcern * 0.65f
                    + profile.agreeableness * 0.35f
                    - profile.personalDistress * 0.10f),
                conformity = Mathf.Clamp01(profile.groupIdentification * 0.45f
                    + profile.agreeableness * 0.35f
                    + profile.needForClosure * 0.20f
                    - profile.reactance * 0.35f),
                initiative = Mathf.Clamp01(profile.extraversion * 0.35f
                    + profile.conscientiousness * 0.30f
                    + profile.openness * 0.20f
                    + selfEfficacy * 0.25f
                    - state.cognitiveLoad * 0.30f),
                skepticism = Mathf.Clamp01(profile.epistemicVigilance * 0.55f
                    + profile.openness * 0.25f
                    + (1f - profile.sourceTrust) * 0.30f)
            };
        }

        private static void InitializeNeutral(CAPsychologicalProfile profile)
        {
            float prior = CACulturalCognitionPureKernel
                .NeutralPsychologicalPrior();
            profile.honestyHumility = prior;
            profile.emotionality = prior;
            profile.extraversion = prior;
            profile.agreeableness = prior;
            profile.conscientiousness = prior;
            profile.openness = prior;
            profile.reactance = prior;
            profile.needForClosure = prior;
            profile.empathicConcern = prior;
            profile.personalDistress = prior;
            profile.epistemicVigilance = prior;
            profile.statusSeeking = prior;
            profile.dangerousWorldBelief = prior;
            profile.competitiveWorldBelief = prior;
            profile.domainRiskTolerance = prior;
            profile.sourceTrust = prior;
            profile.groupIdentification = prior;
            profile.constructUncertainties = ConstructKeys.Select(key =>
                new CAPsychologyConstructUncertainty
                {
                    construct = key,
                    uncertainty = 0.35f
                }).ToList();
            profile.uncertainty = 0.35f;
        }

        internal static float UncertaintyForQuestion(
            CAPsychologicalProfile profile, string questionKey)
        {
            string[] constructs = questionKey switch
            {
                "relationships.sameSexAcceptance" =>
                    new[] { "openness", "agreeableness" },
                "relationships.pluralityAcceptance" =>
                    new[] { "openness", "agreeableness" },
                "relationships.kinObligation" =>
                    new[] { "group identification", "empathic concern" },
                "authority.genderDistribution" =>
                    new[] { "status seeking", "agreeableness" },
                "authority.genderedWork" =>
                    new[] { "need for closure", "openness" },
                "authority.officeAccess" =>
                    new[] { "openness", "agreeableness" },
                "status.hereditaryLegitimacy" => new[]
                    { "status seeking", "need for closure" },
                "status.rankDifferentiation" => new[]
                    { "status seeking", "competitive-world belief" },
                "status.mobility" => new[]
                    { "openness", "need for closure" },
                "groups.outsiderInclusion" => new[]
                    { "openness", "dangerous-world belief" },
                "groups.integrationPreference" => new[]
                    { "openness", "group identification" },
                "groups.membershipAccess" => new[]
                    { "openness", "dangerous-world belief" },
                "labor.coercionLegitimacy" => new[]
                    { "psychological reactance", "need for closure" },
                "voice.inclusionExpectation" => new[]
                    { "extraversion", "psychological reactance" },
                "voice.dissentTolerance" => new[]
                    { "openness", "psychological reactance" },
                "authority.enforcementLegitimacy" => new[]
                    { "need for closure", "dangerous-world belief" },
                "war.captiveProtection" => new[]
                    { "empathic concern", "honesty-humility" },
                "war.punishmentSeverity" => new[]
                    { "dangerous-world belief", "empathic concern" },
                "war.retaliatoryViolence" => new[]
                    { "competitive-world belief", "empathic concern" },
                "provision.mutualObligation" => new[]
                    { "empathic concern", "agreeableness" },
                "property.control" => new[]
                    { "competitive-world belief", "empathic concern" },
                "knowledge.access" => new[]
                    { "source trust", "group identification" },
                "knowledge.noveltyAcceptance" => new[]
                    { "openness", "epistemic vigilance" },
                "knowledge.expertiseDeference" => new[]
                    { "epistemic vigilance", "conscientiousness" },
                _ => Array.Empty<string>()
            };
            return UncertaintyFor(profile, constructs);
        }

        // Versioned, conservative question loadings. Culture supplies the
        // population distribution; these broad factors create bounded
        // individual deviation rather than cloning the population center.
        internal static float PositionShiftForQuestion(
            CAPsychologicalProfile profile, string questionKey)
        {
            if (profile == null) return 0f;
            float openness = Center(profile.openness);
            float agree = Center(profile.agreeableness);
            float empathy = Center(profile.empathicConcern);
            float honesty = Center(profile.honestyHumility);
            float group = Center(profile.groupIdentification);
            float closure = Center(profile.needForClosure);
            float reactance = Center(profile.reactance);
            float status = Center(profile.statusSeeking);
            float danger = Center(profile.dangerousWorldBelief);
            float competition = Center(profile.competitiveWorldBelief);
            float vigilance = Center(profile.epistemicVigilance);
            float conscientious = Center(profile.conscientiousness);
            float shift = questionKey switch
            {
                "relationships.sameSexAcceptance" =>
                    openness * 0.16f + agree * 0.08f,
                "relationships.pluralityAcceptance" =>
                    openness * 0.14f + reactance * 0.06f,
                "relationships.kinObligation" =>
                    group * 0.12f + empathy * 0.10f,
                "authority.genderDistribution" => 0f,
                "authority.genderedWork" =>
                    closure * 0.12f - openness * 0.12f,
                "authority.officeAccess" =>
                    openness * 0.12f + agree * 0.08f,
                "status.hereditaryLegitimacy" =>
                    closure * 0.10f + status * 0.08f,
                "status.rankDifferentiation" =>
                    status * 0.12f + competition * 0.08f,
                "status.mobility" =>
                    openness * 0.12f - closure * 0.08f,
                "groups.outsiderInclusion" =>
                    openness * 0.12f + agree * 0.08f - danger * 0.10f,
                "groups.integrationPreference" =>
                    openness * 0.12f + agree * 0.06f - danger * 0.08f,
                "groups.membershipAccess" =>
                    openness * 0.10f - danger * 0.10f
                        - group * 0.04f,
                "voice.inclusionExpectation" =>
                    openness * 0.10f + reactance * 0.08f,
                "voice.dissentTolerance" =>
                    openness * 0.12f + reactance * 0.10f
                        - closure * 0.08f,
                "authority.enforcementLegitimacy" =>
                    closure * 0.12f + danger * 0.08f
                        - reactance * 0.10f,
                "labor.coercionLegitimacy" =>
                    closure * 0.10f - reactance * 0.12f
                        - empathy * 0.06f,
                "provision.mutualObligation" =>
                    empathy * 0.14f + agree * 0.08f,
                "property.control" => empathy * 0.08f
                    - competition * 0.10f,
                "war.captiveProtection" =>
                    empathy * 0.14f + honesty * 0.08f
                        - competition * 0.08f,
                "war.punishmentSeverity" =>
                    danger * 0.10f + closure * 0.08f
                        - empathy * 0.12f,
                "war.retaliatoryViolence" =>
                    competition * 0.12f - empathy * 0.10f
                        - honesty * 0.06f,
                "knowledge.access" =>
                    openness * 0.10f + agree * 0.05f,
                "knowledge.noveltyAcceptance" =>
                    openness * 0.16f - closure * 0.10f,
                "knowledge.expertiseDeference" =>
                    vigilance * 0.08f + conscientious * 0.08f
                        - reactance * 0.05f,
                _ => 0f
            };
            return Mathf.Clamp(shift, -0.20f, 0.20f);
        }

        private static float Center(float value)
        {
            return Mathf.Clamp01(value) - 0.5f;
        }

        internal static float UncertaintyForPoliticalAxis(
            CAPsychologicalProfile profile, string axis)
        {
            string[] constructs = axis switch
            {
                "leadership" => new[] { "status seeking",
                    "need for closure", "group identification" },
                "decisions" => new[] { "need for closure", "openness",
                    "agreeableness" },
                "participation" => new[] { "extraversion", "openness" },
                "dissent" => new[] { "psychological reactance",
                    "need for closure" },
                "ownership" => new[] { "competitive-world belief",
                    "agreeableness" },
                "economy" => new[] { "competitive-world belief",
                    "openness" },
                "work" => new[] { "conscientiousness",
                    "psychological reactance" },
                "support" => new[] { "empathic concern",
                    "agreeableness" },
                "membership" => new[] { "openness",
                    "dangerous-world belief" },
                "status" => new[] { "status seeking",
                    "competitive-world belief" },
                "localOrder" => new[] { "need for closure",
                    "psychological reactance" },
                "defense" => new[] { "dangerous-world belief",
                    "group identification" },
                "warConduct" => new[] { "empathic concern",
                    "competitive-world belief" },
                _ => Array.Empty<string>()
            };
            return UncertaintyFor(profile, constructs);
        }

        private static float UncertaintyFor(CAPsychologicalProfile profile,
            IEnumerable<string> constructs)
        {
            if (profile?.constructUncertainties == null)
                return profile?.uncertainty ?? 0.35f;
            HashSet<string> requested = new HashSet<string>(constructs
                ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<float> values = profile.constructUncertainties
                .Where(value => value != null
                    && requested.Contains(value.construct))
                .Select(value => value.uncertainty).ToList();
            return values.Count == 0 ? profile.uncertainty
                : Mathf.Clamp01(values.Average());
        }

        private static void ReduceUncertainty(CAPsychologicalProfile profile,
            string construct, float amount)
        {
            CAPsychologyConstructUncertainty target = profile
                .constructUncertainties?.FirstOrDefault(value =>
                    value != null && value.construct == construct);
            if (target != null)
                target.uncertainty = Mathf.Clamp01(target.uncertainty
                    - Math.Max(0f, amount));
            profile.uncertainty = profile.constructUncertainties == null
                || profile.constructUncertainties.Count == 0 ? 0.35f
                : Mathf.Clamp01(profile.constructUncertainties.Average(
                    value => value.uncertainty));
        }

        private static void Evidence(CAPsychologicalProfile profile,
            TraitSet traits, string traitName, string construct, float shift,
            Action<CAPsychologicalProfile> apply)
        {
            TraitDef definition = DefDatabase<TraitDef>.GetNamedSilentFail(
                traitName);
            if (definition == null || traits?.HasTrait(definition) != true)
                return;
            apply(profile);
            ReduceUncertainty(profile, construct, 0.04f);
            profile.evidence.Add(new CAPsychologyEvidenceRecord
            {
                sourceFeature = "trait:" + traitName,
                construct = construct,
                meanShift = shift,
                uncertaintyChange = -0.04f,
                scope = "stable profile",
                provenance = "versioned B12 trait evidence mapping"
            });
        }

        private static void DegreeEvidence(CAPsychologicalProfile profile,
            TraitSet traits, string traitName, string construct, float shift,
            Action<CAPsychologicalProfile, int> apply)
        {
            TraitDef definition = DefDatabase<TraitDef>.GetNamedSilentFail(
                traitName);
            Trait trait = definition == null || traits == null ? null
                : traits.GetTrait(definition);
            if (trait == null) return;
            apply(profile, trait.Degree);
            ReduceUncertainty(profile, construct, 0.04f);
            profile.evidence.Add(new CAPsychologyEvidenceRecord
            {
                sourceFeature = "trait:" + traitName + ":"
                    + trait.Degree,
                construct = construct,
                meanShift = shift * trait.Degree,
                uncertaintyChange = -0.04f,
                scope = "stable profile",
                provenance = "versioned B12 trait-degree evidence mapping"
            });
        }

        private static void ContextEvidence(CAPsychologicalProfile profile,
            string source, string construct, float shift,
            Action<CAPsychologicalProfile> apply, string provenance)
        {
            apply(profile);
            ReduceUncertainty(profile, construct, 0.03f);
            profile.evidence.Add(new CAPsychologyEvidenceRecord
            {
                sourceFeature = source,
                construct = construct,
                meanShift = shift,
                uncertaintyChange = -0.03f,
                scope = "stable profile",
                provenance = provenance
            });
        }

        private static void Clamp(CAPsychologicalProfile value)
        {
            value.honestyHumility = Mathf.Clamp01(value.honestyHumility);
            value.emotionality = Mathf.Clamp01(value.emotionality);
            value.extraversion = Mathf.Clamp01(value.extraversion);
            value.agreeableness = Mathf.Clamp01(value.agreeableness);
            value.conscientiousness = Mathf.Clamp01(value.conscientiousness);
            value.openness = Mathf.Clamp01(value.openness);
            value.reactance = Mathf.Clamp01(value.reactance);
            value.needForClosure = Mathf.Clamp01(value.needForClosure);
            value.empathicConcern = Mathf.Clamp01(value.empathicConcern);
            value.personalDistress = Mathf.Clamp01(value.personalDistress);
            value.epistemicVigilance = Mathf.Clamp01(
                value.epistemicVigilance);
            value.statusSeeking = Mathf.Clamp01(value.statusSeeking);
            value.dangerousWorldBelief = Mathf.Clamp01(
                value.dangerousWorldBelief);
            value.competitiveWorldBelief = Mathf.Clamp01(
                value.competitiveWorldBelief);
            value.domainRiskTolerance = Mathf.Clamp01(
                value.domainRiskTolerance);
            value.sourceTrust = Mathf.Clamp01(value.sourceTrust);
            value.groupIdentification = Mathf.Clamp01(
                value.groupIdentification);
            value.uncertainty = Mathf.Clamp01(value.uncertainty);
        }
    }

    internal static class CAQuestionConsumerMap
    {
        internal static string ForBehavior(string behaviorKey)
        {
            if (behaviorKey.NullOrEmpty()) return null;
            switch (behaviorKey)
            {
                case "welfare.outsider_rescue":
                    return CACultureQuestionRegistry.OutsiderInclusion;
                case "welfare.local_rescue":
                case "welfare.local_treatment":
                case "welfare.mission_triage":
                case "welfare.accountability_check":
                case "welfare.threshold_support":
                case "welfare.buddy_carry":
                case "welfare.medic_dispatch":
                    return CACultureQuestionRegistry.MutualProvision;
                case "communication.knowledge_relay":
                    return CACultureQuestionRegistry.KnowledgeAccess;
                case "communication.status_report":
                    return CACultureQuestionRegistry.VoiceInclusion;
                case "authority.relay_obedience":
                    return CACultureQuestionRegistry.CoercionLegitimacy;
                case "aftermath.secure_hostile":
                case "aftermath.custody_resolution":
                case "npc.enemy_restraint":
                case "npc.captive_stabilization":
                    return CACultureQuestionRegistry.CaptiveProtection;
                case "culture.longitudinal_update":
                    return null;
            }
            if (behaviorKey.StartsWith("research.",
                    StringComparison.Ordinal)
                || behaviorKey == "spatial.npc_settlement_development")
                return CACultureQuestionRegistry.NoveltyAcceptance;
            if (behaviorKey.StartsWith("organization.",
                    StringComparison.Ordinal))
                return CACultureQuestionRegistry.VoiceInclusion;
            if (behaviorKey.StartsWith("provision.",
                    StringComparison.Ordinal))
                return CACultureQuestionRegistry.MutualProvision;
            return null;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup))]
    internal static class CACulturalCognitionPawnSpawnPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Thing __instance)
        {
            if (__instance is Pawn pawn && pawn.RaceProps.Humanlike)
                CACulturalCognitionWorldComponent.Current
                    ?.NotifyPawnAvailable(pawn);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
    internal static class CACulturalCognitionPawnDestroyPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Thing __instance)
        {
            if (__instance is Pawn pawn && pawn.RaceProps.Humanlike)
                CACulturalCognitionWorldComponent.Current
                    ?.NotifyPawnDestroyed(pawn);
        }
    }
}
