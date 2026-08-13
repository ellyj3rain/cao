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

        public CAAttitudeMaterializationInput(float sampledPrivatePosition,
            float populationMean, float descriptiveNormPrior,
            float normStrength,
            float divergenceTolerance, float salience,
            float sourceConfidence, float visibility,
            float doctrinePressure,
            float agreeableness, float groupIdentification,
            float reactance, float epistemicVigilance,
            float psychologicalUncertainty)
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
        }
    }

    public readonly struct CAAttitudeMaterializationResult
    {
        public readonly float PrivatePosition;
        public readonly float MoralConviction;
        public readonly float IdentityCentrality;
        public readonly float KnowledgeConfidence;
        public readonly float DescriptiveNorm;
        public readonly float InjunctiveNorm;
        public readonly float ExpectedEnforcement;
        public readonly float PublicExpression;
        public readonly float Uncertainty;

        public CAAttitudeMaterializationResult(float privatePosition,
            float moralConviction, float identityCentrality,
            float knowledgeConfidence, float descriptiveNorm,
            float injunctiveNorm, float expectedEnforcement,
            float publicExpression, float uncertainty)
        {
            PrivatePosition = privatePosition;
            MoralConviction = moralConviction;
            IdentityCentrality = identityCentrality;
            KnowledgeConfidence = knowledgeConfidence;
            DescriptiveNorm = descriptiveNorm;
            InjunctiveNorm = injunctiveNorm;
            ExpectedEnforcement = expectedEnforcement;
            PublicExpression = publicExpression;
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

        public CAKnowledgeAcceptanceInput(float sourceReliability,
            float relationshipTrust, float expertise, float prestige,
            float authority, float motiveIntegrity, float corroboration,
            float plausibility, float priorCongruence, float methodQuality,
            float observedPayoff, float conflictFreedom,
            float epistemicVigilance, float noveltyAcceptance)
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
            float privatePosition = Clamp(input.SampledPrivatePosition,
                -1f, 1f);
            float injunctive = Clamp(input.PopulationMean * 0.75f
                + input.DoctrinePressure * 0.25f, -1f, 1f);
            float enforcement = Clamp01(input.NormStrength
                * (1f - input.DivergenceTolerance));
            float conformity = Clamp01((input.Agreeableness
                + input.GroupIdentification) * 0.5f);
            float pressure = conformity * enforcement;
            float reactance = Clamp01(input.Reactance) * enforcement;
            float expression = Lerp(privatePosition, injunctive,
                pressure * 0.65f);
            expression += Math.Sign(privatePosition - injunctive)
                * reactance * 0.15f;
            // Visibility governs how much of the chosen expression becomes
            // socially observable. It never changes the private position.
            expression = Lerp(0f, expression, Clamp01(input.Visibility));
            return new CAAttitudeMaterializationResult(
                privatePosition,
                Clamp01(input.Salience
                    * (0.45f + input.NormStrength * 0.55f)),
                Clamp01(input.Salience * input.GroupIdentification),
                Clamp01(input.SourceConfidence
                    * (0.55f + input.EpistemicVigilance * 0.45f)),
                Clamp(input.DescriptiveNormPrior, -1f, 1f), injunctive,
                enforcement, Clamp(expression, -1f, 1f),
                Clamp01(1f - input.SourceConfidence
                    * (1f - input.PsychologicalUncertainty * 0.5f)));
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
                        Clamp01(expectedEnforcement) * 0.45f)
                        * Clamp01(visibility));
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
                    Clamp01(expectedEnforcement) * 0.45f)
                    * Clamp01(visibility));
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
            float source = Clamp01(input.SourceReliability) * 0.16f
                + Clamp01(input.RelationshipTrust) * 0.10f
                + Clamp01(input.Expertise) * 0.13f
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
