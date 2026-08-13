using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonistAwareness
{
    public enum CACulturalBehaviorResponse : byte
    {
        Comply,
        Volunteer,
        Abstain,
        Conceal,
        Dissent,
        Object,
        Organize,
        Reform,
        Exit,
        Violate,
        Retaliate
    }

    public readonly struct CACulturalResponseInput
    {
        public readonly float Support;
        public readonly float ExpectedEnforcement;
        public readonly float PublicExpression;
        public readonly float PrivatePosition;
        public readonly float Reactance;
        public readonly float IdentityCentrality;
        public readonly float Legitimacy;
        public readonly float DynamicCapacity;
        public readonly float Anger;

        public CACulturalResponseInput(float support,
            float expectedEnforcement, float publicExpression,
            float privatePosition, float reactance,
            float identityCentrality, float legitimacy,
            float dynamicCapacity, float anger)
        {
            Support = support;
            ExpectedEnforcement = expectedEnforcement;
            PublicExpression = publicExpression;
            PrivatePosition = privatePosition;
            Reactance = reactance;
            IdentityCentrality = identityCentrality;
            Legitimacy = legitimacy;
            DynamicCapacity = dynamicCapacity;
            Anger = anger;
        }
    }

    public readonly struct CAKnowledgeRetentionCandidate
    {
        public readonly string Identity;
        public readonly string Topic;
        public readonly float Confidence;
        public readonly int LastConfirmedTick;
        public readonly bool Referenced;

        public CAKnowledgeRetentionCandidate(string identity, string topic,
            float confidence, int lastConfirmedTick, bool referenced)
        {
            Identity = identity;
            Topic = topic;
            Confidence = confidence;
            LastConfirmedTick = lastConfirmedTick;
            Referenced = referenced;
        }
    }

    public readonly struct CAAttitudeMaterializationInput
    {
        public readonly float SampledPrivatePosition;
        public readonly float PopulationMean;
        public readonly float DescriptiveNormPrior;
        public readonly float NormStrength;
        public readonly float DivergenceTolerance;
        public readonly float Salience;
        public readonly float SourceConfidence;
        public readonly float Visibility;
        public readonly float DoctrinePressure;
        public readonly float Agreeableness;
        public readonly float GroupIdentification;
        public readonly float Reactance;
        public readonly float EpistemicVigilance;
        public readonly float PsychologicalUncertainty;
        public readonly float PsychologicalPositionShift;
        public readonly float DirectEvidenceConfidence;
        public readonly float RepresentedEnforcement;
        public readonly float MoralExperience;

        public CAAttitudeMaterializationInput(float sampledPrivatePosition,
            float populationMean, float descriptiveNormPrior,
            float normStrength,
            float divergenceTolerance, float salience,
            float sourceConfidence, float visibility,
            float doctrinePressure,
            float agreeableness, float groupIdentification,
            float reactance, float epistemicVigilance,
            float psychologicalUncertainty,
            float psychologicalPositionShift = 0f,
            float directEvidenceConfidence = 0.35f,
            float representedEnforcement = 0f,
            float moralExperience = 0f)
        {
            SampledPrivatePosition = sampledPrivatePosition;
            PopulationMean = populationMean;
            DescriptiveNormPrior = descriptiveNormPrior;
            NormStrength = normStrength;
            DivergenceTolerance = divergenceTolerance;
            Salience = salience;
            SourceConfidence = sourceConfidence;
            Visibility = visibility;
            DoctrinePressure = doctrinePressure;
            Agreeableness = agreeableness;
            GroupIdentification = groupIdentification;
            Reactance = reactance;
            EpistemicVigilance = epistemicVigilance;
            PsychologicalUncertainty = psychologicalUncertainty;
            PsychologicalPositionShift = psychologicalPositionShift;
            DirectEvidenceConfidence = directEvidenceConfidence;
            RepresentedEnforcement = representedEnforcement;
            MoralExperience = moralExperience;
        }
    }

    public readonly struct CARepresentedSourceAppraisal
    {
        public readonly float Weight;
        public readonly float Trust;
        public readonly float Prestige;
        public readonly float PayoffVisibility;

        public CARepresentedSourceAppraisal(float weight, float trust,
            float prestige, float payoffVisibility)
        {
            Weight = weight;
            Trust = trust;
            Prestige = prestige;
            PayoffVisibility = payoffVisibility;
        }
    }

    public readonly struct CARepresentedMoralExperience
    {
        public readonly float Salience;
        public readonly float ApprovalMagnitude;
        public readonly float KnowledgeConfidence;

        public CARepresentedMoralExperience(float salience,
            float approvalMagnitude, float knowledgeConfidence)
        {
            Salience = salience;
            ApprovalMagnitude = approvalMagnitude;
            KnowledgeConfidence = knowledgeConfidence;
        }
    }

    public readonly struct CAAttitudeMaterializationResult
    {
        public readonly float PrivatePosition;
        public readonly float Attention;
        public readonly float MoralConviction;
        public readonly float IdentityCentrality;
        public readonly float InheritedPriorStrength;
        public readonly float KnowledgeConfidence;
        public readonly float DescriptiveNorm;
        public readonly float InjunctiveNorm;
        public readonly float PerceivedSocialPressure;
        public readonly float ExpectedEnforcement;
        public readonly float PublicExpression;
        public readonly float ObservationLikelihood;
        public readonly float Uncertainty;

        public CAAttitudeMaterializationResult(float privatePosition,
            float attention, float moralConviction, float identityCentrality,
            float inheritedPriorStrength, float knowledgeConfidence,
            float descriptiveNorm, float injunctiveNorm,
            float perceivedSocialPressure, float expectedEnforcement,
            float publicExpression, float observationLikelihood,
            float uncertainty)
        {
            PrivatePosition = privatePosition;
            Attention = attention;
            MoralConviction = moralConviction;
            IdentityCentrality = identityCentrality;
            InheritedPriorStrength = inheritedPriorStrength;
            KnowledgeConfidence = knowledgeConfidence;
            DescriptiveNorm = descriptiveNorm;
            InjunctiveNorm = injunctiveNorm;
            PerceivedSocialPressure = perceivedSocialPressure;
            ExpectedEnforcement = expectedEnforcement;
            PublicExpression = publicExpression;
            ObservationLikelihood = observationLikelihood;
            Uncertainty = uncertainty;
        }
    }

    public readonly struct CACulturalInfluenceSample
    {
        public readonly float PublicExpression;
        public readonly float Weight;
        public readonly float Trust;
        public readonly float Prestige;
        public readonly float Conformity;
        public readonly float PayoffVisibility;

        public CACulturalInfluenceSample(float publicExpression,
            float weight, float trust, float prestige, float conformity,
            float payoffVisibility)
        {
            PublicExpression = publicExpression;
            Weight = weight;
            Trust = trust;
            Prestige = prestige;
            Conformity = conformity;
            PayoffVisibility = payoffVisibility;
        }
    }

    public readonly struct CACulturalInfluenceResult
    {
        public readonly float PrivatePosition;
        public readonly float DescriptiveNorm;
        public readonly float InjunctiveNorm;
        public readonly float PublicExpression;

        public CACulturalInfluenceResult(float privatePosition,
            float descriptiveNorm, float injunctiveNorm,
            float publicExpression)
        {
            PrivatePosition = privatePosition;
            DescriptiveNorm = descriptiveNorm;
            InjunctiveNorm = injunctiveNorm;
            PublicExpression = publicExpression;
        }
    }

    public readonly struct CAPoliticalFormationInput
    {
        public readonly float CultureSupport;
        public readonly float PsychologicalSupport;
        public readonly float IdeoligionPressure;
        public readonly float MaterialInterest;
        public readonly float InstitutionalExperience;
        public readonly float ThreatPressure;
        public readonly float PriorBeliefSupport;
        public readonly float KnowledgeSupport;
        public readonly float NetworkSupport;
        public readonly float KnowledgeConfidence;
        public readonly float Salience;
        public readonly float IdentityCentrality;
        public readonly float PerceivedMajority;
        public readonly float ExpectedEnforcement;
        public readonly float Reactance;

        public CAPoliticalFormationInput(float cultureSupport,
            float psychologicalSupport, float ideoligionPressure,
            float materialInterest, float institutionalExperience,
            float threatPressure, float priorBeliefSupport,
            float knowledgeSupport, float networkSupport,
            float knowledgeConfidence,
            float salience, float identityCentrality,
            float perceivedMajority, float expectedEnforcement,
            float reactance)
        {
            CultureSupport = cultureSupport;
            PsychologicalSupport = psychologicalSupport;
            IdeoligionPressure = ideoligionPressure;
            MaterialInterest = materialInterest;
            InstitutionalExperience = institutionalExperience;
            ThreatPressure = threatPressure;
            PriorBeliefSupport = priorBeliefSupport;
            KnowledgeSupport = knowledgeSupport;
            NetworkSupport = networkSupport;
            KnowledgeConfidence = knowledgeConfidence;
            Salience = salience;
            IdentityCentrality = identityCentrality;
            PerceivedMajority = perceivedMajority;
            ExpectedEnforcement = expectedEnforcement;
            Reactance = reactance;
        }
    }

    public readonly struct CAPoliticalFormationResult
    {
        public readonly float Support;
        public readonly float Confidence;
        public readonly float MoralConviction;
        public readonly float PublicExpression;

        public CAPoliticalFormationResult(float support, float confidence,
            float moralConviction, float publicExpression)
        {
            Support = support;
            Confidence = confidence;
            MoralConviction = moralConviction;
            PublicExpression = publicExpression;
        }
    }

    public readonly struct CAInstitutionLegitimacyInput
    {
        public readonly float ProceduralFairness;
        public readonly float OutcomePerformance;
        public readonly float LawAndCustomFit;
        public readonly float IdentityRepresentation;
        public readonly float Competence;
        public readonly float Corruption;
        public readonly float Coercion;
        public readonly float CulturalFit;
        public readonly float IdeoligionFit;
        public readonly float PoliticalFit;
        public readonly float PersonalTreatment;
        public readonly float Trust;
        public readonly float PublicSupport;

        public CAInstitutionLegitimacyInput(float proceduralFairness,
            float outcomePerformance, float lawAndCustomFit,
            float identityRepresentation, float competence,
            float corruption, float coercion, float culturalFit,
            float ideoligionFit, float politicalFit,
            float personalTreatment, float trust, float publicSupport)
        {
            ProceduralFairness = proceduralFairness;
            OutcomePerformance = outcomePerformance;
            LawAndCustomFit = lawAndCustomFit;
            IdentityRepresentation = identityRepresentation;
            Competence = competence;
            Corruption = corruption;
            Coercion = coercion;
            CulturalFit = culturalFit;
            IdeoligionFit = ideoligionFit;
            PoliticalFit = politicalFit;
            PersonalTreatment = personalTreatment;
            Trust = trust;
            PublicSupport = publicSupport;
        }
    }

    public readonly struct CAInstitutionLegitimacyEvidence
    {
        public readonly CAInstitutionLegitimacyInput Input;
        public readonly float EvidenceConfidence;
        public readonly string SourceSignature;

        public CAInstitutionLegitimacyEvidence(
            CAInstitutionLegitimacyInput input, float evidenceConfidence,
            string sourceSignature)
        {
            Input = input;
            EvidenceConfidence = evidenceConfidence;
            SourceSignature = sourceSignature;
        }
    }

    public readonly struct CASanctionResponseResult
    {
        public readonly float Deterrence;
        public readonly float NormReinforcement;
        public readonly float Reactance;
        public readonly float VoluntaryCooperation;

        public CASanctionResponseResult(float deterrence,
            float normReinforcement, float reactance,
            float voluntaryCooperation)
        {
            Deterrence = deterrence;
            NormReinforcement = normReinforcement;
            Reactance = reactance;
            VoluntaryCooperation = voluntaryCooperation;
        }
    }

    public readonly struct CAKnowledgeAcceptanceInput
    {
        public readonly float SourceReliability;
        public readonly float RelationshipTrust;
        public readonly float Expertise;
        public readonly float Prestige;
        public readonly float Authority;
        public readonly float MotiveIntegrity;
        public readonly float Corroboration;
        public readonly float Plausibility;
        public readonly float PriorCongruence;
        public readonly float MethodQuality;
        public readonly float ObservedPayoff;
        public readonly float ConflictFreedom;
        public readonly float EpistemicVigilance;
        public readonly float NoveltyAcceptance;
        public readonly float ExpertiseDeference;

        public CAKnowledgeAcceptanceInput(float sourceReliability,
            float relationshipTrust, float expertise, float prestige,
            float authority, float motiveIntegrity, float corroboration,
            float plausibility, float priorCongruence, float methodQuality,
            float observedPayoff, float conflictFreedom,
            float epistemicVigilance, float noveltyAcceptance,
            float expertiseDeference = 0.5f)
        {
            SourceReliability = sourceReliability;
            RelationshipTrust = relationshipTrust;
            Expertise = expertise;
            Prestige = prestige;
            Authority = authority;
            MotiveIntegrity = motiveIntegrity;
            Corroboration = corroboration;
            Plausibility = plausibility;
            PriorCongruence = priorCongruence;
            MethodQuality = methodQuality;
            ObservedPayoff = observedPayoff;
            ConflictFreedom = conflictFreedom;
            EpistemicVigilance = epistemicVigilance;
            NoveltyAcceptance = noveltyAcceptance;
            ExpertiseDeference = expertiseDeference;
        }
    }

    public readonly struct CAKnowledgeAcceptanceResult
    {
        public readonly float Confidence;
        public readonly float Attention;
        public readonly float Transmissibility;

        public CAKnowledgeAcceptanceResult(float confidence,
            float attention, float transmissibility)
        {
            Confidence = confidence;
            Attention = attention;
            Transmissibility = transmissibility;
        }
    }

    // Pure kernels expose every causal equation to fixed-seed receipts. The
    // runtime wrapper supplies represented state and persists the result.
    public static class CACulturalCognitionPureKernel
    {
        public const int MaxKnowledgePropositions = 4096;
        public const int MaxKnowledgePerTopic = 64;
        public const int MaxResearchPrograms = 1024;
        public const int MaxKnowledgeEvidence = 32;
        public const int MaxKnowledgeProvenance = 32;
        public const int MaxKnowledgeContradictions = 64;
        public const int MaxPoliticalEvidenceHistory = 24;
        public const int MaxResearchPriorKnowledge = 8;
        public const int MaxResearchEvidence = 32;
        public const int MaxResearchSkilledPawns = 64;
        public const int MaxInfluenceEdgesPerPawn = 8;
        public const int MaxPoliticalIssueLinksPerFaction = 96;
        public const int MinPoliticalIssueLinkObservations = 16;
        public const float MinPoliticalIssueCorrelation = 0.70f;
        public const double MaxPoliticalIssueFamilyWiseError = 0.01d;
        public const int MaxInstitutionLegitimacyAppraisals = 256;
        public const int MaxInstitutionSanctionAppraisals = 256;
        public const string ExpertiseSocial = "Social";
        public const string ExpertiseIntellectual = "Intellectual";
        public const string ExpertiseMedicine = "Medicine";
        public const string ExpertiseCooking = "Cooking";
        public const string ExpertisePlants = "Plants";
        public const string ExpertiseAnimals = "Animals";
        public const string ExpertiseCrafting = "Crafting";
        public const string ExpertiseConstruction = "Construction";
        public const string ExpertiseShooting = "Shooting";
        public const string ExpertiseMelee = "Melee";
        public const string ExpertiseArtistic = "Artistic";

        // A claim source earns deference from demonstrated, subject-relevant
        // skill. Every built-in social subject has an explicit domain; an
        // unregistered integration subject retains the neutral 0.5 prior.
        private static readonly IReadOnlyDictionary<string, string[]>
            ExpertiseDomains = new Dictionary<string, string[]>(
                StringComparer.Ordinal)
            {
                [CASocialSubjectRegistry.PublicGathering] =
                    new[] { ExpertiseSocial },
                [CASocialSubjectRegistry.OutsiderContact] =
                    new[] { ExpertiseSocial },
                [CASocialSubjectRegistry.DefendedBoundary] =
                    new[] { ExpertiseConstruction, ExpertiseShooting,
                        ExpertiseMelee },
                [CASocialSubjectRegistry.ResearchWork] =
                    new[] { ExpertiseIntellectual },
                [CASocialSubjectRegistry.PublicVoice] =
                    new[] { ExpertiseSocial },
                [CASocialSubjectRegistry.CompelledService] =
                    new[] { ExpertiseSocial },
                [CASocialSubjectRegistry.EnforcedOrder] =
                    new[] { ExpertiseSocial, ExpertiseShooting,
                        ExpertiseMelee },
                [CASocialSubjectRegistry.CompulsoryTransfer] =
                    new[] { ExpertiseSocial, ExpertiseIntellectual },
                [CASocialSubjectRegistry.SharedProvision] =
                    new[] { ExpertiseSocial, ExpertiseIntellectual },
                [CASocialSubjectRegistry.InheritedRank] =
                    new[] { ExpertiseSocial },
                [CASocialSubjectRegistry.HumaneCustody] =
                    new[] { ExpertiseMedicine, ExpertiseSocial },
                [CASocialSubjectRegistry.QuarterGiven] =
                    new[] { ExpertiseShooting, ExpertiseMelee,
                        ExpertiseSocial },
                [CASocialSubjectRegistry.MedicalCare] =
                    new[] { ExpertiseMedicine },
                [CASocialSubjectRegistry.CustodyPunishment] =
                    new[] { ExpertiseShooting, ExpertiseMelee,
                        ExpertiseSocial },
                [CASocialSubjectRegistry.VoluntaryTrade] =
                    new[] { ExpertiseSocial },
                [CASocialSubjectRegistry.MealPreparation] =
                    new[] { ExpertiseCooking },
                [CASocialSubjectRegistry.KnowledgeTransmission] =
                    new[] { ExpertiseIntellectual, ExpertiseSocial },
                [CASocialSubjectRegistry.LongRangeCommunication] =
                    new[] { ExpertiseIntellectual },
                [CASocialSubjectRegistry.FactionMembership] =
                    new[] { ExpertiseSocial },
                [CASocialSubjectRegistry.HouseholdMembership] =
                    new[] { ExpertiseSocial },
                [CASocialSubjectRegistry.ArtAndRemembrance] =
                    new[] { ExpertiseArtistic },
                [CASocialSubjectRegistry.DelegatedAuthority] =
                    new[] { ExpertiseSocial },
                [CASocialSubjectRegistry.OfficeGovernance] =
                    new[] { ExpertiseSocial, ExpertiseIntellectual },
                [CASocialSubjectRegistry.AnimalTending] =
                    new[] { ExpertiseAnimals },
                [CASocialSubjectRegistry.Cultivation] =
                    new[] { ExpertisePlants },
                [CASocialSubjectRegistry.GeneralCraft] =
                    new[] { ExpertiseCrafting, ExpertiseConstruction },
                [CASocialSubjectRegistry.RepairAndRebuilding] =
                    new[] { ExpertiseCrafting, ExpertiseConstruction },
                [CASocialSubjectRegistry.SpecializedCraft] =
                    new[] { ExpertiseCrafting },
                [CASocialSubjectRegistry.CommonOwnership] =
                    new[] { ExpertiseSocial, ExpertiseIntellectual },
                [CASocialSubjectRegistry.Confiscation] =
                    new[] { ExpertiseSocial, ExpertiseIntellectual },
                [CASocialSubjectRegistry.PrivateOwnership] =
                    new[] { ExpertiseSocial, ExpertiseIntellectual },
                [CASocialSubjectRegistry.Taxation] =
                    new[] { ExpertiseSocial, ExpertiseIntellectual },
                [CASocialSubjectRegistry.SharedRecreation] =
                    new[] { ExpertiseSocial },
                [CASocialSubjectRegistry.VoluntaryAgreement] =
                    new[] { ExpertiseSocial },
                [CASocialSubjectRegistry.ReligiousObservance] =
                    new[] { ExpertiseSocial },
                [CASocialSubjectRegistry.SecurityService] =
                    new[] { ExpertiseShooting, ExpertiseMelee },
                [CASocialSubjectRegistry.MaintainedHousing] =
                    new[] { ExpertiseConstruction },
                [CASocialSubjectRegistry.PublicWorks] =
                    new[] { ExpertiseConstruction },
                [CASocialSubjectRegistry.KinSuccession] =
                    new[] { ExpertiseSocial },
                [CASocialSubjectRegistry.OfficeHolding] =
                    new[] { ExpertiseSocial },
                [CASocialSubjectRegistry.StoredReserves] =
                    new[] { ExpertiseSocial, ExpertiseIntellectual },
                [CASocialSubjectRegistry.AuthorityProvision] =
                    new[] { ExpertiseSocial, ExpertiseIntellectual },
                [CASocialSubjectRegistry.HouseholdProvision] =
                    new[] { ExpertiseSocial, ExpertiseIntellectual },
                [CASocialSubjectRegistry.RouteUse] =
                    new[] { ExpertiseConstruction },
                [CASocialSubjectRegistry.CombatViolence] =
                    new[] { ExpertiseShooting, ExpertiseMelee }
            };

        // The durable ledger is bounded by topic and globally. Current
        // research references have first retention priority, followed by
        // recency and confidence; identity provides deterministic tie-breaks.
        public static HashSet<string> RetainedKnowledgeIdentities(
            IEnumerable<CAKnowledgeRetentionCandidate> candidates,
            int maxTotal = MaxKnowledgePropositions,
            int maxPerTopic = MaxKnowledgePerTopic)
        {
            if (maxTotal < 1 || maxPerTopic < 1)
                return new HashSet<string>(StringComparer.Ordinal);
            IEnumerable<CAKnowledgeRetentionCandidate> ranked =
                (candidates ?? Enumerable.Empty<
                    CAKnowledgeRetentionCandidate>())
                .Where(value => !string.IsNullOrWhiteSpace(value.Identity)
                    && !string.IsNullOrWhiteSpace(value.Topic))
                .GroupBy(value => value.Topic, StringComparer.Ordinal)
                .SelectMany(group => RankKnowledge(group).Take(maxPerTopic));
            return new HashSet<string>(RankKnowledge(ranked)
                .Take(maxTotal).Select(value => value.Identity),
                StringComparer.Ordinal);
        }

        private static IOrderedEnumerable<CAKnowledgeRetentionCandidate>
            RankKnowledge(IEnumerable<CAKnowledgeRetentionCandidate> values)
        {
            return values.OrderByDescending(value => value.Referenced)
                .ThenByDescending(value => value.LastConfirmedTick)
                .ThenByDescending(value => value.Confidence)
                .ThenBy(value => value.Identity, StringComparer.Ordinal);
        }

        public static float NeutralPsychologicalPrior() => 0.5f;

        public static CACulturalBehaviorResponse SelectCulturalResponse(
            CACulturalResponseInput input)
        {
            if (input.Support >= 0.55f)
                return CACulturalBehaviorResponse.Volunteer;
            if (input.Support >= 0.05f)
                return CACulturalBehaviorResponse.Comply;
            if (input.Support <= -0.70f && input.Legitimacy <= 0.30f
                && input.Anger >= 0.65f)
                return CACulturalBehaviorResponse.Retaliate;
            if (input.Support <= -0.60f && input.Reactance >= 0.70f
                && input.Legitimacy <= 0.40f)
                return CACulturalBehaviorResponse.Violate;
            if (input.Support <= -0.45f && input.DynamicCapacity <= 0.30f)
                return CACulturalBehaviorResponse.Exit;
            if (input.ExpectedEnforcement >= 0.65f)
                return input.PublicExpression * input.PrivatePosition < 0f
                    ? CACulturalBehaviorResponse.Conceal
                    : CACulturalBehaviorResponse.Abstain;
            if (input.Reactance >= 0.70f)
                return CACulturalBehaviorResponse.Object;
            if (input.IdentityCentrality >= 0.70f
                && input.Legitimacy < 0.50f)
                return CACulturalBehaviorResponse.Reform;
            if (input.IdentityCentrality >= 0.60f)
                return CACulturalBehaviorResponse.Organize;
            return CACulturalBehaviorResponse.Dissent;
        }

        public static CAAttitudeMaterializationResult Materialize(
            CAAttitudeMaterializationInput input)
        {
            float privatePosition = Clamp(input.SampledPrivatePosition
                + input.PsychologicalPositionShift, -1f, 1f);
            float injunctive = Clamp(input.PopulationMean * 0.75f
                + input.DoctrinePressure * 0.25f, -1f, 1f);
            // Norm pressure is a population property. Expected enforcement is
            // learned from represented rules and sanction history; one never
            // aliases the other.
            float socialPressure = Clamp01(input.NormStrength
                * (1f - input.DivergenceTolerance));
            float enforcement = Clamp01(input.RepresentedEnforcement);
            float expression = PublicExpression(privatePosition, injunctive,
                socialPressure, enforcement, input.Agreeableness,
                input.GroupIdentification, input.Reactance);
            float attention = Clamp01(input.Salience
                * (0.65f + Math.Abs(privatePosition) * 0.35f));
            // Group identification and position extremity establish identity
            // centrality. Attention must not turn salience into conviction by
            // an indirect path through identity.
            float identity = Clamp01(input.GroupIdentification
                * (0.55f + Math.Abs(privatePosition) * 0.45f));
            // Conviction needs position extremity, doctrine, identity, or
            // lived moral evidence. Salience changes attention, not conviction
            // by itself.
            float conviction = MoralConviction(privatePosition,
                input.DoctrinePressure, identity, input.MoralExperience);
            float evidence = Clamp01(input.DirectEvidenceConfidence);
            float knowledgeConfidence = KnowledgeConfidence(evidence,
                input.PsychologicalUncertainty,
                input.EpistemicVigilance);
            float uncertainty = KnowledgeUncertainty(evidence,
                input.PsychologicalUncertainty,
                input.EpistemicVigilance);
            return new CAAttitudeMaterializationResult(
                privatePosition, attention, conviction, identity,
                Clamp01(input.SourceConfidence), knowledgeConfidence,
                Clamp(input.DescriptiveNormPrior, -1f, 1f), injunctive,
                socialPressure, enforcement, Clamp(expression, -1f, 1f),
                Clamp01(input.Visibility), uncertainty);
        }

        public static float PublicExpression(float privatePosition,
            float perceivedInjunctiveNorm, float perceivedSocialPressure,
            float expectedEnforcement, float agreeableness,
            float groupIdentification, float reactance)
        {
            float conformity = Clamp01((Clamp01(agreeableness)
                + Clamp01(groupIdentification)) * 0.5f);
            float pressure = conformity * Clamp01(
                Clamp01(perceivedSocialPressure) * 0.65f
                + Clamp01(expectedEnforcement) * 0.35f);
            float backlash = Clamp01(reactance)
                * Clamp01(expectedEnforcement);
            float expression = Lerp(Clamp(privatePosition, -1f, 1f),
                Clamp(perceivedInjunctiveNorm, -1f, 1f),
                pressure * 0.65f);
            expression += Math.Sign(privatePosition
                    - perceivedInjunctiveNorm)
                * backlash * 0.15f;
            return Clamp(expression, -1f, 1f);
        }

        public static float MoralConviction(float privatePosition,
            float doctrinePressure, float identityCentrality,
            float moralExperience)
        {
            return Clamp01(Math.Abs(Clamp(privatePosition, -1f, 1f))
                * 0.34f
                + Math.Abs(Clamp(doctrinePressure, -1f, 1f)) * 0.22f
                + Clamp01(identityCentrality) * 0.20f
                + Clamp01(moralExperience) * 0.24f);
        }

        public static float KnowledgeConfidence(float directEvidence,
            float psychologicalUncertainty, float epistemicVigilance)
        {
            return Clamp01(Clamp01(directEvidence) * 0.55f
                + (1f - Clamp01(psychologicalUncertainty)) * 0.25f
                + Clamp01(epistemicVigilance) * 0.20f);
        }

        public static float KnowledgeUncertainty(float directEvidence,
            float psychologicalUncertainty, float epistemicVigilance)
        {
            return Clamp01(1f - (Clamp01(directEvidence) * 0.55f
                + (1f - Clamp01(psychologicalUncertainty)) * 0.30f
                + Clamp01(epistemicVigilance) * 0.15f));
        }

        // Source appraisal is built only from represented, question-specific
        // observations. An empty history retains a conservative uncertainty
        // floor; it is never interpreted as negative evidence.
        public static float RepresentedEvidenceConfidence(
            IEnumerable<CARepresentedSourceAppraisal> sources)
        {
            CARepresentedSourceAppraisal[] represented = (sources
                    ?? Enumerable.Empty<CARepresentedSourceAppraisal>())
                .Take(6).ToArray();
            if (represented.Length == 0) return 0.30f;
            float appraisal = represented.Average(value =>
                Clamp01(value.Trust) * 0.35f
                + Clamp01(value.Prestige) * 0.25f
                + Clamp01(value.PayoffVisibility) * 0.20f
                + Clamp01(value.Weight) * 0.20f);
            return Clamp01(0.30f + represented.Length * 0.05f
                + appraisal * 0.35f);
        }

        // Conviction consumes the magnitude of represented lived moral
        // experience. Direction remains in the private position and the
        // event appraisal; a missing event contributes nothing.
        public static float RepresentedMoralExperience(
            IEnumerable<CARepresentedMoralExperience> experiences)
        {
            CARepresentedMoralExperience[] represented = (experiences
                    ?? Enumerable.Empty<CARepresentedMoralExperience>())
                .Take(12).ToArray();
            if (represented.Length == 0) return 0f;
            return Clamp01(represented.Average(value =>
                Clamp01(value.Salience)
                * (0.45f + Clamp01(value.ApprovalMagnitude) * 0.35f
                    + Clamp01(value.KnowledgeConfidence) * 0.20f)));
        }

        public static IReadOnlyList<string>
            ExpertiseDomainsForSocialSubject(string subjectKey)
        {
            return !string.IsNullOrWhiteSpace(subjectKey)
                && ExpertiseDomains.TryGetValue(subjectKey,
                    out string[] domains)
                ? domains : Array.Empty<string>();
        }

        public static float DemonstratedExpertise(string subjectKey,
            Func<string, float> normalizedSkillForDomain)
        {
            IReadOnlyList<string> domains =
                ExpertiseDomainsForSocialSubject(subjectKey);
            if (domains.Count == 0 || normalizedSkillForDomain == null)
                return 0.5f;
            return Clamp01(domains.Max(domain =>
                normalizedSkillForDomain(domain)));
        }

        // Population evidence is available only to the represented share of
        // the population. The stable sample avoids a fresh reroll on load.
        public static bool ReceivesRepresentedQuestionEvidence(int pawnId,
            string evidenceSignature, float participation)
        {
            float represented = Clamp01(participation);
            if (represented <= 0f) return false;
            if (represented >= 1f) return true;
            uint sample = StableHash(pawnId + "|"
                + (evidenceSignature ?? "unrecorded question evidence"));
            return (sample & 0x00FFFFFFu) / 16777215f < represented;
        }

        // Direct historical evidence appraises a represented population fact,
        // not a synthetic speaker. Agreement and disagreement remain in the
        // measured position; reliability follows participation, consistency,
        // and repeated observation.
        public static CARepresentedSourceAppraisal
            RepresentedQuestionSourceAppraisal(float dispersion,
                float participation, int observationCount)
        {
            float represented = Clamp01(participation);
            float consistency = 1f - Clamp01(dispersion);
            float continuity = Clamp01(Math.Max(0, observationCount) / 6f);
            float trust = Clamp01(consistency * 0.55f
                + continuity * 0.25f + represented * 0.20f);
            return new CARepresentedSourceAppraisal(represented, trust,
                0.5f, represented * (0.5f + consistency * 0.5f));
        }

        public static CARepresentedMoralExperience
            RepresentedQuestionMoralExperience(float position,
                float dispersion, float participation, int observationCount)
        {
            CARepresentedSourceAppraisal source =
                RepresentedQuestionSourceAppraisal(dispersion,
                    participation, observationCount);
            return new CARepresentedMoralExperience(
                Clamp01(participation),
                Math.Abs(Clamp(position, -1f, 1f)), source.Trust);
        }

        // The registered scale runs from open work (-1) to rigid division
        // (+1). Assignment difference therefore increases, rather than
        // reverses, the recorded position.
        public static float HistoricalGenderedWorkPosition(
            float assignmentDifference)
        {
            return Clamp(Clamp01(assignmentDifference) * 2f - 1f,
                -1f, 1f);
        }

        // Mixed represented officeholders are positive evidence of broad
        // access. A single-gender holder set is composition, not proof that
        // anyone else was ineligible, so it produces no access sample.
        public static bool TryHistoricalOfficeAccessPosition(
            int maleOfficeholders, int femaleOfficeholders,
            out float position)
        {
            bool represented = maleOfficeholders > 0
                && femaleOfficeholders > 0;
            position = represented ? 0.70f : 0f;
            return represented;
        }

        public static CACulturalInfluenceResult Influence(
            float targetPrivate, float descriptiveNorm,
            float injunctiveNorm, float expectedEnforcement,
            float visibility, float openness,
            IEnumerable<CACulturalInfluenceSample> samples)
        {
            List<CACulturalInfluenceSample> accepted = (samples
                    ?? Enumerable.Empty<CACulturalInfluenceSample>())
                .Where(value => Math.Abs(value.PublicExpression
                    - targetPrivate) <= 0.70f).ToList();
            if (accepted.Count == 0)
                return new CACulturalInfluenceResult(targetPrivate,
                    descriptiveNorm, injunctiveNorm,
                    Lerp(targetPrivate, injunctiveNorm,
                        Clamp01(expectedEnforcement) * 0.45f));
            float total = accepted.Sum(InfluenceWeight);
            float observed = accepted.Sum(value => value.PublicExpression
                * InfluenceWeight(value)) / total;
            // Repeated independent exposure strengthens adoption without
            // turning one prestigious referent into an artificial majority.
            float reinforcement = Clamp01((accepted.Count - 1) / 3f);
            float susceptibility = (0.03f + Clamp01(openness) * 0.07f)
                * (0.75f + reinforcement * 0.50f);
            float nextPrivate = Lerp(targetPrivate, observed,
                susceptibility);
            float nextDescriptive = Lerp(descriptiveNorm, observed, 0.28f);
            float conformityTotal = Math.Max(0.01f,
                accepted.Sum(value => value.Conformity));
            float nextInjunctive = Lerp(injunctiveNorm,
                accepted.Sum(value => value.PublicExpression
                    * value.Conformity) / conformityTotal, 0.16f);
            return new CACulturalInfluenceResult(nextPrivate,
                nextDescriptive, nextInjunctive,
                Lerp(nextPrivate, nextInjunctive,
                    Clamp01(expectedEnforcement) * 0.45f));
        }

        private static float InfluenceWeight(CACulturalInfluenceSample value)
        {
            float visiblePayoff = 0.20f
                + Clamp01(value.PayoffVisibility) * 0.80f;
            return Math.Max(0.01f, value.Weight * visiblePayoff
                * (0.35f + value.Trust * 0.35f
                    + value.Prestige * 0.30f));
        }

        public static float RelationshipApproachFactor(float publicExpression,
            float knowledgeConfidence)
        {
            float expressed = (Clamp(publicExpression, -1f, 1f) + 1f)
                * 0.5f;
            return Lerp(0.70f, 1.15f,
                Lerp(0.5f, expressed, Clamp01(knowledgeConfidence)));
        }

        public static float InstitutionFit(float desired, float realized)
        {
            return Clamp01(1f - Math.Abs(Clamp(desired, -1f, 1f)
                - Clamp(realized, -1f, 1f)) * 0.5f);
        }

        public static float KnowledgeAccessThreshold(float publicAccess)
        {
            float openness = (Clamp(publicAccess, -1f, 1f) + 1f) * 0.5f;
            return Lerp(0.90f, 0.15f, openness);
        }

        public static bool KnowledgeAccessEligible(float publicAccess,
            float representedStanding, bool directObservation)
        {
            return directObservation || Clamp01(representedStanding)
                >= KnowledgeAccessThreshold(publicAccess);
        }

        public static float KnowledgeTransmission(float publicAccess,
            float publicNovelty)
        {
            float access = (Clamp(publicAccess, -1f, 1f) + 1f) * 0.5f;
            float novelty = (Clamp(publicNovelty, -1f, 1f) + 1f) * 0.5f;
            return Lerp(0.08f, 0.95f,
                Clamp01(access * 0.60f + novelty * 0.40f));
        }

        public static float KnowledgeTransmission(float publicNovelty)
        {
            return KnowledgeTransmission(0f, publicNovelty);
        }

        public static CAPoliticalFormationResult FormPoliticalAttitude(
            CAPoliticalFormationInput input)
        {
            float support = Clamp(input.CultureSupport * 0.22f
                + input.PsychologicalSupport * 0.14f
                + input.IdeoligionPressure * 0.10f
                + input.MaterialInterest * 0.12f
                + input.InstitutionalExperience * 0.08f
                + input.ThreatPressure * 0.06f
                + input.PriorBeliefSupport * 0.14f
                + input.KnowledgeSupport * 0.08f
                + input.NetworkSupport * 0.06f, -1f, 1f);
            float confidence = Clamp01(input.KnowledgeConfidence);
            float conviction = Clamp01(input.Salience
                * (0.45f + Math.Abs(support) * 0.55f));
            float socialPressure = Clamp01(input.ExpectedEnforcement)
                * (1f - Clamp01(input.Reactance)) * 0.45f;
            float publicExpression = Lerp(support,
                Clamp(input.PerceivedMajority, -1f, 1f), socialPressure);
            publicExpression = Lerp(publicExpression, support,
                Clamp01(input.IdentityCentrality) * 0.35f);
            return new CAPoliticalFormationResult(support, confidence,
                conviction, Clamp(publicExpression, -1f, 1f));
        }

        public static float PearsonCorrelation(IReadOnlyList<float> left,
            IReadOnlyList<float> right)
        {
            int count = Math.Min(left?.Count ?? 0, right?.Count ?? 0);
            if (count < 3) return 0f;
            double leftMean = 0d;
            double rightMean = 0d;
            for (int i = 0; i < count; i++)
            {
                leftMean += left[i];
                rightMean += right[i];
            }
            leftMean /= count;
            rightMean /= count;
            double covariance = 0d;
            double leftVariance = 0d;
            double rightVariance = 0d;
            for (int i = 0; i < count; i++)
            {
                double l = left[i] - leftMean;
                double r = right[i] - rightMean;
                covariance += l * r;
                leftVariance += l * l;
                rightVariance += r * r;
            }
            double denominator = Math.Sqrt(leftVariance * rightVariance);
            return denominator <= 0.000001d ? 0f
                : Clamp((float)(covariance / denominator), -1f, 1f);
        }

        public static bool EligiblePoliticalIssueLink(float correlation,
            int observations, int testedHypotheses = 1)
        {
            if (observations < MinPoliticalIssueLinkObservations
                || Math.Abs(correlation) < MinPoliticalIssueCorrelation)
                return false;
            double absolute = Math.Min(0.999999d,
                Math.Abs((double)correlation));
            double fisher = 0.5d * Math.Log((1d + absolute)
                / (1d - absolute));
            double z = fisher * Math.Sqrt(Math.Max(1d, observations - 3d));
            // Conservative two-tailed Fisher-z bound with a Bonferroni family
            // correction. A faction evaluates the entire cross-axis option
            // family together; no link is selected as an isolated test.
            double tailBound = Math.Min(1d, 2d * Math.Exp(-0.5d * z * z));
            double perTest = MaxPoliticalIssueFamilyWiseError
                / Math.Max(1, testedHypotheses);
            return tailBound <= perTest;
        }

        public static float ShrunkPoliticalIssueCorrelation(
            float correlation, int observations, int testedHypotheses = 1)
        {
            if (!EligiblePoliticalIssueLink(correlation, observations,
                    testedHypotheses))
                return 0f;
            float reliability = Clamp01((observations - 3f)
                / (observations + 1f));
            return Clamp(correlation * reliability, -1f, 1f);
        }

        public static float PoliticalIssueEvidenceWeight(int observations)
        {
            if (observations < MinPoliticalIssueLinkObservations) return 0f;
            return Clamp01((observations - 8f) / 24f);
        }

        public static float RecordIndependentKnowledgeSource(
            List<string> sources, string sourceIdentity,
            int limit = MaxKnowledgeProvenance)
        {
            if (sources == null || string.IsNullOrWhiteSpace(sourceIdentity)
                || limit < 1) return KnowledgeCorroboration(0);
            sources.Remove(sourceIdentity);
            sources.Add(sourceIdentity);
            if (sources.Count > limit)
                sources.RemoveRange(0, sources.Count - limit);
            return KnowledgeCorroboration(sources.Distinct(
                StringComparer.Ordinal).Count());
        }

        public static bool IsDistinctReportRoute(int holderPawnId,
            int reporterPawnId)
        {
            return holderPawnId >= 0 && reporterPawnId >= 0
                && holderPawnId != reporterPawnId;
        }

        public static string ActKnowledgeChannel(byte representedHow)
        {
            switch (representedHow)
            {
                case 0: return "Firsthand";
                case 1: return "Witnessed";
                default: return "Reported";
            }
        }

        public static float PoliticalNetworkSupport(
            IEnumerable<(float OtherSupport, float Correlation,
                float Constraint)> links)
        {
            var represented = (links ?? Enumerable.Empty<(float, float,
                    float)>())
                .Where(value => Math.Abs(value.Correlation) > 0.001f
                    && value.Constraint > 0f).ToList();
            if (represented.Count == 0) return 0f;
            float total = represented.Sum(value =>
                Math.Max(0.01f, Clamp01(value.Constraint)));
            // A single weakly evidenced link remains weak. Normalizing by
            // its own constraint would cancel the sample-size attenuation.
            return Clamp(represented.Sum(value => value.OtherSupport
                    * value.Correlation * Clamp01(value.Constraint))
                / Math.Max(1f, total), -1f, 1f);
        }

        public static float InstitutionLegitimacy(
            CAInstitutionLegitimacyInput input)
        {
            return Clamp01(
                Clamp01(input.ProceduralFairness) * 0.15f
                + Clamp01(input.OutcomePerformance) * 0.12f
                + Clamp01(input.LawAndCustomFit) * 0.06f
                + Clamp01(input.IdentityRepresentation) * 0.07f
                + Clamp01(input.Competence) * 0.07f
                + (1f - Clamp01(input.Corruption)) * 0.06f
                + (1f - Clamp01(input.Coercion)) * 0.06f
                + Clamp01(input.CulturalFit) * 0.11f
                + Clamp01(input.IdeoligionFit) * 0.05f
                + Clamp01(input.PoliticalFit) * 0.07f
                + Clamp01(input.PersonalTreatment) * 0.05f
                + Clamp01(input.Trust) * 0.04f
                + Clamp01(input.PublicSupport) * 0.09f);
        }

        public static CASanctionResponseResult SanctionResponse(
            float legitimacy, float proportionality, float visibility,
            float consistency, float procedure, float socialSupport,
            float intrinsicMotivation, float psychologicalReactance)
        {
            float fair = (Clamp01(legitimacy)
                + Clamp01(proportionality) + Clamp01(consistency)
                + Clamp01(procedure)) * 0.25f;
            float deterrence = Clamp01(visibility * 0.30f
                + consistency * 0.30f + fair * 0.40f);
            float backlash = Clamp01((1f - fair) * 0.55f
                + Clamp01(psychologicalReactance) * 0.45f);
            float reinforcement = Clamp01(fair * 0.65f
                + Clamp01(socialSupport) * 0.35f - backlash * 0.30f);
            float voluntary = Clamp01(Clamp01(intrinsicMotivation)
                * (0.70f + reinforcement * 0.30f)
                - backlash * 0.45f);
            return new CASanctionResponseResult(deterrence, reinforcement,
                backlash, voluntary);
        }

        public static CAKnowledgeAcceptanceResult EvaluateKnowledge(
            CAKnowledgeAcceptanceInput input)
        {
            float expertiseWeight = 0.60f
                + Clamp01(input.ExpertiseDeference) * 0.80f;
            float source = Clamp01(input.SourceReliability) * 0.16f
                + Clamp01(input.RelationshipTrust) * 0.10f
                + Clamp01(input.Expertise * expertiseWeight) * 0.13f
                + Clamp01(input.Prestige) * 0.05f
                + Clamp01(input.Authority) * 0.04f
                + Clamp01(input.MotiveIntegrity) * 0.08f;
            float claim = Clamp01(input.Corroboration) * 0.12f
                + Clamp01(input.Plausibility) * 0.08f
                + Clamp01(input.MethodQuality) * 0.10f
                + Clamp01(input.ObservedPayoff) * 0.06f
                + Clamp01(input.ConflictFreedom) * 0.08f;
            float congruence = Clamp01(input.PriorCongruence);
            float vigilance = Clamp01(input.EpistemicVigilance);
            float confidence = Clamp01(source + claim
                + congruence * (0.08f - vigilance * 0.04f));
            float novelty = (Clamp(input.NoveltyAcceptance, -1f, 1f) + 1f)
                * 0.5f;
            float attention = Clamp01(0.20f + novelty * 0.55f
                + vigilance * 0.25f);
            float transmission = Clamp01(confidence
                * (0.35f + attention * 0.65f));
            return new CAKnowledgeAcceptanceResult(confidence, attention,
                transmission);
        }

        public static float KnowledgeCorroboration(int independentSources)
        {
            return Clamp01(0.25f
                + Math.Max(0, independentSources - 1) * 0.20f);
        }

        public static float KnowledgeConflictFreedom(float uncontested,
            int explicitContradictions)
        {
            return Math.Min(Clamp01(uncontested),
                Clamp01(1f - Math.Max(0, explicitContradictions) / 8f));
        }

        public static float PrivatePublicGap(float privatePosition,
            float publicExpression)
        {
            return Math.Abs(Clamp(privatePosition, -1f, 1f)
                - Clamp(publicExpression, -1f, 1f));
        }

        public static float PerceivedActualNormGap(float perceivedNorm,
            float actualExpressionMean)
        {
            return Math.Abs(Clamp(perceivedNorm, -1f, 1f)
                - Clamp(actualExpressionMean, -1f, 1f));
        }

        private static float Lerp(float left, float right, float amount)
        {
            return left + (right - left) * Clamp01(amount);
        }

        private static float Clamp01(float value) => Clamp(value, 0f, 1f);

        private static float Clamp(float value, float low, float high)
        {
            return value < low ? low : value > high ? high : value;
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char character in value ?? "")
                {
                    hash ^= character;
                    hash *= 16777619u;
                }
                return hash;
            }
        }
    }
}
