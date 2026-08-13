using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonistAwareness
{
    public enum CACultureQuestionLayer : byte
    {
        Relationship,
        Authority,
        Status,
        Intergroup,
        Labor,
        Voice,
        War,
        Provision,
        Knowledge
    }

    public sealed class CACultureQuestionDef
    {
        public int SchemaVersion = 1;
        public string Key;
        public string Label;
        public string Question;
        public string Construct;
        public string OrderedDirection;
        public CACultureQuestionLayer Layer;
        public string[] Anchors;
        public double[] AnchorCenters;
        public string Applicability;
        public float DefaultSalience;
        public float DefaultSpread;
        public float DefaultNormStrength;
        public string[] IdeoligionAdapters;
        public string[] SocialSubjectAdapters;
        public string[] PracticeEvidenceAdapters;
        public string[] BehaviorConsumers;
        public string[] PoliticalConsumers;
        public string[] InstitutionConsumers;
        public string[] KnowledgeConsumers;
        public string[] ResearchProvenance;

        public string ValidationFailure()
        {
            if (SchemaVersion != 1) return "unsupported definition schema";
            if (string.IsNullOrWhiteSpace(Key) || !Key.Contains("."))
                return "stable key is missing or unnamespaced";
            if (string.IsNullOrWhiteSpace(Label)
                || string.IsNullOrWhiteSpace(Question)
                || string.IsNullOrWhiteSpace(Construct)
                || string.IsNullOrWhiteSpace(OrderedDirection))
                return "player-facing construct contract is incomplete";
            if (Anchors == null || Anchors.Length != 5
                || Anchors.Any(string.IsNullOrWhiteSpace))
                return "exactly five named anchors are required";
            if (AnchorCenters == null || AnchorCenters.Length != 5)
                return "exactly five anchor centers are required";
            for (int i = 1; i < AnchorCenters.Length; i++)
                if (AnchorCenters[i] <= AnchorCenters[i - 1])
                    return "anchor centers are not monotonic";
            if (DefaultSpread <= 0f || DefaultSpread > 1f
                || DefaultSalience < 0f || DefaultSalience > 1f
                || DefaultNormStrength < 0f || DefaultNormStrength > 1f)
                return "default distribution values are outside 0..1";
            int consumers = (BehaviorConsumers?.Length ?? 0)
                + (PoliticalConsumers?.Length ?? 0)
                + (InstitutionConsumers?.Length ?? 0)
                + (KnowledgeConsumers?.Length ?? 0);
            if (consumers == 0) return "no represented consumer is registered";
            if (ResearchProvenance == null || ResearchProvenance.Length == 0)
                return "research provenance is missing";
            return null;
        }
    }

    // Culture owns population distributions over intelligible questions.
    // Actions, institutions, political rules, Ideoligion and knowledge remain
    // separate owners and appear here only through named evidence adapters.
    public static class CACultureQuestionRegistry
    {
        public const int CurrentVersion = 1;
        public const string SameSexAcceptance =
            "relationships.sameSexAcceptance";
        public const string PluralityAcceptance =
            "relationships.pluralityAcceptance";
        public const string GenderDistribution =
            "authority.genderDistribution";
        public const string HereditaryLegitimacy =
            "status.hereditaryLegitimacy";
        public const string RankDifferentiation =
            "status.rankDifferentiation";
        public const string OutsiderInclusion =
            "groups.outsiderInclusion";
        public const string IntegrationPreference =
            "groups.integrationPreference";
        public const string CoercionLegitimacy =
            "labor.coercionLegitimacy";
        public const string VoiceInclusion =
            "voice.inclusionExpectation";
        public const string CaptiveProtection =
            "war.captiveProtection";
        public const string MutualProvision =
            "provision.mutualObligation";
        public const string KnowledgeAccess = "knowledge.access";
        public const string NoveltyAcceptance =
            "knowledge.noveltyAcceptance";

        private static readonly double[] Centers =
            { -0.90d, -0.45d, 0d, 0.45d, 0.90d };
        private static readonly CACultureQuestionDef[] Definitions = Build();
        private static readonly Dictionary<string, CACultureQuestionDef> ByKey =
            Definitions.ToDictionary(value => value.Key, value => value,
                StringComparer.Ordinal);

        public static IReadOnlyList<CACultureQuestionDef> All => Definitions;

        public static CACultureQuestionDef Find(string key)
        {
            if (key == null) return null;
            ByKey.TryGetValue(key, out CACultureQuestionDef value);
            return value;
        }

        public static string ValidationFailure()
        {
            if (Definitions.Length != 13)
                return "the initial Culture question registry is incomplete";
            if (Definitions.Select(value => value.Key).Distinct(
                    StringComparer.Ordinal).Count() != Definitions.Length)
                return "Culture question keys are duplicated";
            foreach (CACultureQuestionDef definition in Definitions)
            {
                string failure = definition.ValidationFailure();
                if (!string.IsNullOrWhiteSpace(failure))
                    return definition.Key + ": " + failure;
            }
            return null;
        }

        public static string QuestionForSocialSubject(string subjectKey)
        {
            return Definitions.FirstOrDefault(value =>
                value.SocialSubjectAdapters?.Contains(subjectKey,
                    StringComparer.Ordinal) == true)?.Key;
        }

        // Adapter polarity belongs beside the adapter itself. Most legacy
        // subjects point in the same direction as their current question;
        // approval of punishment points away from protection owed to captives.
        public static int DirectionForSocialSubject(string subjectKey)
        {
            return subjectKey == CASocialSubjectRegistry.CustodyPunishment
                ? -1 : 1;
        }

        private static CACultureQuestionDef[] Build()
        {
            const string Schwartz = "Schwartz value theory and cross-cultural value structure";
            const string Norms = "descriptive and injunctive norm distinction";
            const string Ideology = "RimWorld Ideoligion precept adapter evidence";
            const string Institutions = "institutional legitimacy and represented-practice evidence";
            return new[]
            {
                Q(SameSexAcceptance, "Same-sex relationship acceptance",
                    "How are same-sex relationships regarded?",
                    "approval of same-sex romantic relationships",
                    "condemnation to affirmation", CACultureQuestionLayer.Relationship,
                    A("Strongly condemned", "Disapproved", "Tolerated", "Accepted", "Affirmed"),
                    Array.Empty<string>(),
                    Array.Empty<string>(), Array.Empty<string>(),
                    A("romance appraisal", "public expression"),
                    A("membership conflict"), A("relationship legitimacy"),
                    Array.Empty<string>(), A(Schwartz, Norms)),
                Q(PluralityAcceptance, "Relationship plurality acceptance",
                    "How are plural unions regarded?",
                    "approval of sex-neutral relationship plurality",
                    "exclusive unions to preferred plural unions",
                    CACultureQuestionLayer.Relationship,
                    A("Exclusive only", "Exclusivity preferred", "Plural unions tolerated", "Plural unions accepted", "Plural unions preferred"),
                    new[] { "spouse-count precepts" }, Array.Empty<string>(),
                    Array.Empty<string>(), A("household formation", "jealousy appraisal"),
                    Array.Empty<string>(), A("relationship rules"),
                    Array.Empty<string>(), A(Schwartz, Norms, Ideology)),
                Q(GenderDistribution, "Gender distribution of authority",
                    "How should authority be distributed between women and men?",
                    "legitimacy of gendered authority distribution",
                    "male dominance through symmetry to female dominance",
                    CACultureQuestionLayer.Authority,
                    A("Strongly male-dominant", "Male-leaning", "Symmetric", "Female-leaning", "Strongly female-dominant"),
                    new[] { "gender-supremacy precepts" }, Array.Empty<string>(),
                    Array.Empty<string>(), A("command legitimacy"),
                    A("office support", "participation conflict"),
                    A("office selection"), Array.Empty<string>(),
                    A(Schwartz, Norms, Ideology, Institutions)),
                Q(HereditaryLegitimacy, "Hereditary status legitimacy",
                    "How legitimate is inherited status?",
                    "legitimacy attributed to inherited status and succession",
                    "illegitimate to naturalized", CACultureQuestionLayer.Status,
                    A("Illegitimate", "Disfavored", "Permitted", "Respected", "Naturalized"),
                    Array.Empty<string>(),
                    A(CASocialSubjectRegistry.InheritedRank,
                        CASocialSubjectRegistry.KinSuccession),
                    Array.Empty<string>(), A("succession appraisal"),
                    A("status support"), A("office legitimacy"),
                    Array.Empty<string>(), A(Schwartz, Norms, Institutions)),
                Q(RankDifferentiation, "Social rank differentiation",
                    "How much durable social rank is proper?",
                    "approval of durable rank differentiation",
                    "rank rejection to entrenched rank", CACultureQuestionLayer.Status,
                    A("Rank rejected", "Rank minimized", "Mixed", "Rank accepted", "Rank entrenched"),
                    Array.Empty<string>(),
                    A(CASocialSubjectRegistry.OfficeHolding),
                    Array.Empty<string>(), A("deference appraisal"),
                    A("status and resource legitimacy"),
                    A("office privilege"), Array.Empty<string>(),
                    A(Schwartz, Norms, Institutions)),
                Q(OutsiderInclusion, "Outsider social inclusion",
                    "How readily are outsiders included in social life?",
                    "social inclusion of people treated as outsiders",
                    "exclusionary to integrative", CACultureQuestionLayer.Intergroup,
                    A("Exclusionary", "Guarded", "Selective", "Receptive", "Integrative"),
                    Array.Empty<string>(),
                    A(CASocialSubjectRegistry.OutsiderContact,
                        CASocialSubjectRegistry.FactionMembership),
                    Array.Empty<string>(), A("hospitality", "recruitment", "intermarriage"),
                    A("membership conflict"), A("access and membership"),
                    Array.Empty<string>(), A(Schwartz, Norms)),
                Q(IntegrationPreference, "Intergroup integration",
                    "How much intergroup integration is expected?",
                    "preference for separation or integration between groups",
                    "required separation to expected integration",
                    CACultureQuestionLayer.Intergroup,
                    A("Separation required", "Separation preferred", "Context-dependent", "Integration preferred", "Integration expected"),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(), A("mixed-group interaction"),
                    A("integration politics"),
                    A("residence and access policy"), Array.Empty<string>(),
                    A(Schwartz, Norms, Institutions)),
                Q(CoercionLegitimacy, "Coercive labor legitimacy",
                    "When is compelled labor legitimate?",
                    "legitimacy attributed to compelled labor",
                    "never legitimate to institutionally expected",
                    CACultureQuestionLayer.Labor,
                    A("Never legitimate", "Emergency only", "Conditionally tolerated", "Broadly accepted", "Institutionally expected"),
                    new[] { "slavery and work precepts" },
                    A(CASocialSubjectRegistry.CompelledService,
                        CASocialSubjectRegistry.EnforcedOrder),
                    A("ca.practice.compelled_service"),
                    A("work refusal", "enforcement response"),
                    A("labor conflict"), A("work rules"),
                    Array.Empty<string>(), A(Schwartz, Norms, Ideology, Institutions)),
                Q(VoiceInclusion, "Inclusion in public voice",
                    "Who is expected to have a public voice?",
                    "expected breadth of public political voice",
                    "reserved voice to universal voice",
                    CACultureQuestionLayer.Voice,
                    A("Voice reserved", "Voice restricted", "Voice conditional", "Voice broadly expected", "Voice universally expected"),
                    Array.Empty<string>(),
                    A(CASocialSubjectRegistry.PublicVoice,
                        CASocialSubjectRegistry.PublicGathering,
                        CASocialSubjectRegistry.DelegatedAuthority,
                        CASocialSubjectRegistry.OfficeGovernance),
                    Array.Empty<string>(), A("meeting participation", "dissent response"),
                    A("participation conflict"),
                    A("eligibility and decision procedure"), Array.Empty<string>(),
                    A(Schwartz, Norms, Institutions)),
                Q(CaptiveProtection, "Protection owed to defeated people",
                    "What protection is owed to defeated people?",
                    "duty of restraint and care toward defeated people",
                    "no restraint to strong duty of care",
                    CACultureQuestionLayer.War,
                    A("No expected restraint", "Minimal restraint", "Conditional protection", "Protection expected", "Strong duty of care"),
                    new[] { "execution, slavery and war-conduct precepts" },
                    A(CASocialSubjectRegistry.HumaneCustody,
                        CASocialSubjectRegistry.QuarterGiven,
                        CASocialSubjectRegistry.CustodyPunishment),
                    A("ca.practice.custody_care"),
                    A("surrender", "custody", "execution", "medical care"),
                    A("war legitimacy"), A("custody rules"),
                    Array.Empty<string>(), A(Schwartz, Norms, Ideology, Institutions)),
                Q(MutualProvision, "Mutual provision obligation",
                    "Who is expected to provide during hardship?",
                    "breadth of obligation to provide food, shelter and care",
                    "household responsibility to collective guarantee",
                    CACultureQuestionLayer.Provision,
                    A("Household only", "Voluntary aid", "Mixed responsibility", "Shared responsibility", "Collective guarantee"),
                    new[] { "charity and communal precepts" },
                    A(CASocialSubjectRegistry.SharedProvision,
                        CASocialSubjectRegistry.MedicalCare,
                        CASocialSubjectRegistry.StoredReserves,
                        CASocialSubjectRegistry.AuthorityProvision,
                        CASocialSubjectRegistry.HouseholdProvision),
                    A("ca.practice.shared_provision"),
                    A("aid", "food and shelter provision"),
                    A("support politics"), A("provision systems"),
                    Array.Empty<string>(), A(Schwartz, Norms, Ideology, Institutions)),
                Q(KnowledgeAccess, "Access to established knowledge",
                    "Who should have access to established knowledge?",
                    "breadth of access to represented established knowledge",
                    "esoteric to open", CACultureQuestionLayer.Knowledge,
                    A("Esoteric", "Restricted", "Credentialed", "Broad", "Open"),
                    Array.Empty<string>(),
                    A(CASocialSubjectRegistry.KnowledgeTransmission,
                        CASocialSubjectRegistry.LongRangeCommunication),
                    A("ca.practice.organized_research"),
                    A("teaching", "publication"), A("knowledge-access politics"),
                    A("archive and school access"),
                    A("proposition access", "transmission"),
                    A(Schwartz, Norms, Institutions)),
                Q(NoveltyAcceptance, "Acceptance of novel claims",
                    "How readily are novel claims accepted for testing and use?",
                    "openness to evaluating and adopting novel claims",
                    "tradition-bound to experimental",
                    CACultureQuestionLayer.Knowledge,
                    A("Tradition-bound", "Suspicious", "Selective", "Receptive", "Experimental"),
                    new[] { "research-speed precepts" },
                    Array.Empty<string>(),
                    A("ca.practice.organized_research"),
                    A("research adoption", "foreign knowledge"),
                    A("innovation politics"), A("method rules"),
                    A("claim evaluation", "research adoption"),
                    A(Schwartz, Norms, Ideology, Institutions))
            };
        }

        private static CACultureQuestionDef Q(string key, string label,
            string question, string construct, string direction,
            CACultureQuestionLayer layer, string[] anchors,
            string[] ideoligion, string[] subjects, string[] practices,
            string[] behaviors, string[] politics, string[] institutions,
            string[] knowledge, string[] provenance)
        {
            return new CACultureQuestionDef
            {
                Key = key,
                Label = label,
                Question = question,
                Construct = construct,
                OrderedDirection = direction,
                Layer = layer,
                Anchors = anchors,
                AnchorCenters = (double[])Centers.Clone(),
                Applicability = "represented populations and subgroups",
                DefaultSalience = 0.55f,
                DefaultSpread = 0.28f,
                DefaultNormStrength = 0.50f,
                IdeoligionAdapters = ideoligion,
                SocialSubjectAdapters = subjects,
                PracticeEvidenceAdapters = practices,
                BehaviorConsumers = behaviors,
                PoliticalConsumers = politics,
                InstitutionConsumers = institutions,
                KnowledgeConsumers = knowledge,
                ResearchProvenance = provenance
            };
        }

        private static string[] A(params string[] values) => values;
    }

    public sealed class CACultureSubgroupDistribution : IExposable
    {
        public string subgroupKey;
        public string label;
        public int share;
        public float meanOffset;
        public float spreadMultiplier = 1f;
        public bool inherited = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref subgroupKey, "subgroupKey");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref share, "share", 0);
            Scribe_Values.Look(ref meanOffset, "meanOffset", 0f);
            Scribe_Values.Look(ref spreadMultiplier,
                "spreadMultiplier", 1f);
            Scribe_Values.Look(ref inherited, "inherited", true);
        }

        public CACultureSubgroupDistribution Copy()
        {
            return (CACultureSubgroupDistribution)MemberwiseClone();
        }
    }

    public sealed class CACultureQuestionDistribution : IExposable
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string questionKey;
        public string populationScope = "*";
        public float mean;
        public bool hasDescriptiveNormPrior;
        public float descriptiveNormPrior;
        public float prestigeSignal;
        public float spread = 0.28f;
        public bool spreadOverride;
        public float salience = 0.55f;
        public float normStrength = 0.50f;
        public float visibility = 0.65f;
        public float sourceConfidence = 0.60f;
        public float toleranceForDivergence = 0.50f;
        public List<CACultureSubgroupDistribution> subgroups =
            new List<CACultureSubgroupDistribution>();
        public string provenance;
        public string sourceIdentity;
        public string evidenceSignature;
        public int firstRecordedTick = -1;
        public int lastChangedTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref questionKey, "questionKey");
            Scribe_Values.Look(ref populationScope, "populationScope", "*");
            Scribe_Values.Look(ref mean, "mean", 0f);
            Scribe_Values.Look(ref hasDescriptiveNormPrior,
                "hasDescriptiveNormPrior", false);
            Scribe_Values.Look(ref descriptiveNormPrior,
                "descriptiveNormPrior", 0f);
            Scribe_Values.Look(ref prestigeSignal, "prestigeSignal", 0f);
            Scribe_Values.Look(ref spread, "spread", 0.28f);
            Scribe_Values.Look(ref spreadOverride, "spreadOverride", false);
            Scribe_Values.Look(ref salience, "salience", 0.55f);
            Scribe_Values.Look(ref normStrength, "normStrength", 0.50f);
            Scribe_Values.Look(ref visibility, "visibility", 0.65f);
            Scribe_Values.Look(ref sourceConfidence,
                "sourceConfidence", 0.60f);
            Scribe_Values.Look(ref toleranceForDivergence,
                "toleranceForDivergence", 0.50f);
            Scribe_Collections.Look(ref subgroups, "subgroups", LookMode.Deep);
            Scribe_Values.Look(ref provenance, "provenance");
            Scribe_Values.Look(ref sourceIdentity, "sourceIdentity");
            Scribe_Values.Look(ref evidenceSignature, "evidenceSignature");
            Scribe_Values.Look(ref firstRecordedTick,
                "firstRecordedTick", -1);
            Scribe_Values.Look(ref lastChangedTick, "lastChangedTick", -1);
        }

        public CACultureQuestionDistribution Copy()
        {
            var result = (CACultureQuestionDistribution)MemberwiseClone();
            result.subgroups = (subgroups
                    ?? new List<CACultureSubgroupDistribution>())
                .Where(value => value != null).Select(value => value.Copy())
                .ToList();
            return result;
        }
    }

    public sealed class CACultureLegacyEvidence : IExposable
    {
        public string sourceKey;
        public string sourceLayer;
        public string populationScope = "*";
        public string disposition;
        public string summary;
        public string sourceIdentity;
        public string evidenceSignature;
        public int firstRecordedTick = -1;
        public int lastChangedTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref sourceKey, "sourceKey");
            Scribe_Values.Look(ref sourceLayer, "sourceLayer");
            Scribe_Values.Look(ref populationScope, "populationScope", "*");
            Scribe_Values.Look(ref disposition, "disposition");
            Scribe_Values.Look(ref summary, "summary");
            Scribe_Values.Look(ref sourceIdentity, "sourceIdentity");
            Scribe_Values.Look(ref evidenceSignature, "evidenceSignature");
            Scribe_Values.Look(ref firstRecordedTick,
                "firstRecordedTick", -1);
            Scribe_Values.Look(ref lastChangedTick, "lastChangedTick", -1);
        }

        public CACultureLegacyEvidence Copy()
        {
            return (CACultureLegacyEvidence)MemberwiseClone();
        }
    }

    public sealed class CACultureDistributionSummary
    {
        public float Mean;
        public float Median;
        public float Spread;
        public float Polarization;
        public float Salience;
        public float Tolerance;
        public string Anchor;
        public string Signature;
    }

    public readonly struct CALegacyCultureMeaningAdapterInput
    {
        public readonly int Approval;
        public readonly int Salience;
        public readonly int Weight;
        public readonly int Direction;

        public CALegacyCultureMeaningAdapterInput(int approval, int salience,
            int weight, int direction)
        {
            Approval = approval;
            Salience = salience;
            Weight = Math.Max(1, weight);
            Direction = direction < 0 ? -1 : 1;
        }
    }

    public readonly struct CALegacyCultureQuestionAdapterResult
    {
        public readonly float Mean;
        public readonly float Salience;

        public CALegacyCultureQuestionAdapterResult(float mean,
            float salience)
        {
            Mean = mean;
            Salience = salience;
        }
    }

    // The one schema-9 -> schema-10 semantic adapter shared by production,
    // fixture conversion, and receipts. Only weighted approval and salience
    // have exact destinations. Normality, prestige, weight, and source detail
    // remain migration evidence and never become live B12 state by analogy.
    public static class CALegacyCultureQuestionAdapter
    {
        public static CALegacyCultureQuestionAdapterResult Adapt(
            IEnumerable<CALegacyCultureMeaningAdapterInput> source)
        {
            CALegacyCultureMeaningAdapterInput[] values = (source
                    ?? Enumerable.Empty<CALegacyCultureMeaningAdapterInput>())
                .ToArray();
            int total = values.Sum(value => value.Weight);
            if (total <= 0)
                return new CALegacyCultureQuestionAdapterResult(0f, 0.55f);
            float mean = values.Sum(value => value.Approval
                    * value.Direction * value.Weight)
                / (100f * total);
            float salience = values.Sum(value => value.Salience
                    * value.Weight) / (100f * total);
            return new CALegacyCultureQuestionAdapterResult(
                Math.Max(-1f, Math.Min(1f, mean)),
                Math.Max(0f, Math.Min(1f, salience)));
        }
    }

    public static class CACultureDistributionKernel
    {
        public const float VarianceFloor = 0.06f;
        private static readonly double[] NormalCalibrationNodes =
        {
            -1.9817, -1.4652, -1.1806, -0.9674, -0.7916,
            -0.6375, -0.4972, -0.3661, -0.2410, -0.1196,
             0.0000,
             0.1196,  0.2410,  0.3661,  0.4972,  0.6375,
             0.7916,  0.9674,  1.1806,  1.4652,  1.9817
        };

        public static string ValidationFailure(
            CACultureQuestionDistribution value)
        {
            if (value == null) return "question distribution is null";
            if (value.schemaVersion
                != CACultureQuestionDistribution.CurrentSchemaVersion)
                return "question distribution schema is unsupported";
            if (CACultureQuestionRegistry.Find(value.questionKey) == null)
                return "question key is not registered";
            if (value.populationScope.NullOrEmpty())
                return "population scope is missing";
            if (!In(value.mean, -1f, 1f)
                || !In(value.descriptiveNormPrior, -1f, 1f)
                || !In(value.prestigeSignal, -1f, 1f)
                || !In(value.spread, VarianceFloor, 1f)
                || !In(value.salience, 0f, 1f)
                || !In(value.normStrength, 0f, 1f)
                || !In(value.visibility, 0f, 1f)
                || !In(value.sourceConfidence, 0f, 1f)
                || !In(value.toleranceForDivergence, 0f, 1f))
                return "question distribution parameter is out of range";
            if (value.subgroups == null)
                return "subgroup mixture is missing";
            if (value.subgroups.Any(group => group == null
                    || group.subgroupKey.NullOrEmpty() || group.share < 0
                    || group.share > 100 || !In(group.meanOffset, -1f, 1f)
                    || !In(group.spreadMultiplier, 0.1f, 3f)))
                return "subgroup mixture is invalid";
            if (value.subgroups.GroupBy(group => group.subgroupKey,
                    StringComparer.Ordinal).Any(group => group.Count() > 1))
                return "subgroup identity is duplicated";
            if (value.subgroups.Count > 0
                && value.subgroups.Sum(group => group.share) != 100)
                return "subgroup shares do not total 100";
            return null;
        }

        public static CACultureQuestionDistribution NewQuestion(
            CACultureQuestionDef definition, string populationScope = "*")
        {
            if (definition == null) return null;
            return new CACultureQuestionDistribution
            {
                questionKey = definition.Key,
                populationScope = populationScope ?? "*",
                mean = 0f,
                hasDescriptiveNormPrior = false,
                descriptiveNormPrior = 0f,
                prestigeSignal = 0f,
                spread = Math.Max(VarianceFloor, definition.DefaultSpread),
                salience = 0.55f,
                normStrength = 0.50f,
                visibility = 0.65f,
                sourceConfidence = 0.55f,
                toleranceForDivergence = 0.50f,
                provenance = "new authored question defaults"
            };
        }

        public static float Materialize(
            CACultureQuestionDistribution distribution,
            string worldIdentity, string cultureIdentity,
            string subgroupIdentity, string pawnIdentity, int epoch)
        {
            if (distribution == null) return 0f;
            CACultureSubgroupDistribution subgroup = distribution.subgroups?
                .FirstOrDefault(value => value != null
                    && value.subgroupKey == subgroupIdentity);
            float center = Math.Max(-0.995f, Math.Min(0.995f,
                distribution.mean + (subgroup?.meanOffset ?? 0f)));
            float sigma = Math.Max(VarianceFloor, distribution.spread
                * (subgroup?.spreadMultiplier ?? 1f));
            string seed = (worldIdentity ?? "world") + "|"
                + (cultureIdentity ?? "culture") + "|"
                + (subgroupIdentity ?? "all") + "|"
                + (pawnIdentity ?? "pawn") + "|"
                + (distribution.questionKey ?? "question") + "|" + epoch;
            double normal = StandardNormal(StableHash(seed),
                StableHash(seed + "|second"));
            double latentCenter = CalibratedLatentCenter(center, sigma);
            return (float)Math.Tanh(latentCenter + sigma * normal);
        }

        // Authoring stores the bounded population center displayed by the
        // five anchors. The latent normal location is solved so changing
        // spread changes dispersion without silently moving that center.
        // Equal-probability normal nodes make the calibration deterministic
        // and independent from any pawn materialization seed.
        public static double CalibratedLatentCenter(float boundedCenter,
            float sigma)
        {
            double target = Math.Max(-0.995d, Math.Min(0.995d,
                boundedCenter));
            double low = -7d;
            double high = 7d;
            double spread = Math.Max(VarianceFloor, sigma);
            for (int iteration = 0; iteration < 28; iteration++)
            {
                double middle = (low + high) * 0.5d;
                double expected = 0d;
                for (int i = 0; i < NormalCalibrationNodes.Length; i++)
                    expected += Math.Tanh(middle
                        + spread * NormalCalibrationNodes[i]);
                expected /= NormalCalibrationNodes.Length;
                if (expected < target) low = middle;
                else high = middle;
            }
            return (low + high) * 0.5d;
        }

        public static CACultureDistributionSummary Summarize(
            CACultureQuestionDistribution distribution, string seed)
        {
            if (distribution == null) return new CACultureDistributionSummary();
            var values = new List<float>();
            List<CACultureSubgroupDistribution> mixture = (distribution
                    .subgroups ?? new List<CACultureSubgroupDistribution>())
                .Where(value => value != null && value.share > 0).ToList();
            for (int i = 0; i < 101; i++)
            {
                string subgroup = null;
                if (mixture.Count > 0)
                {
                    int draw = i % 100;
                    int cumulative = 0;
                    foreach (CACultureSubgroupDistribution candidate in mixture)
                    {
                        cumulative += candidate.share;
                        if (draw >= cumulative) continue;
                        subgroup = candidate.subgroupKey;
                        break;
                    }
                }
                values.Add(Materialize(distribution, seed, seed, subgroup,
                    i.ToString(), 0));
            }
            values.Sort();
            float mean = values.Average();
            float variance = values.Average(value =>
            {
                float delta = value - mean;
                return delta * delta;
            });
            int positive = values.Count(value => value >= 0.45f);
            int negative = values.Count(value => value <= -0.45f);
            CACultureQuestionDef definition = CACultureQuestionRegistry.Find(
                distribution.questionKey);
            return new CACultureDistributionSummary
            {
                Mean = mean,
                Median = values[values.Count / 2],
                Spread = (float)Math.Sqrt(variance),
                Polarization = Math.Min(positive, negative)
                    / (float)values.Count,
                Salience = distribution.salience,
                Tolerance = distribution.toleranceForDivergence,
                Anchor = definition?.Anchors[NearestAnchor(
                    distribution.mean)] ?? "Unregistered",
                Signature = Fingerprint(distribution)
            };
        }

        public static int NearestAnchor(float mean)
        {
            double[] centers = { -0.90d, -0.45d, 0d, 0.45d, 0.90d };
            int best = 0;
            double distance = double.MaxValue;
            for (int i = 0; i < centers.Length; i++)
            {
                double candidate = Math.Abs(mean - centers[i]);
                if (candidate >= distance) continue;
                best = i;
                distance = candidate;
            }
            return best;
        }

        public static string Fingerprint(
            CACultureQuestionDistribution value)
        {
            if (value == null) return "unrecorded";
            string groups = string.Join(",", (value.subgroups
                    ?? new List<CACultureSubgroupDistribution>())
                .Where(group => group != null)
                .OrderBy(group => group.subgroupKey, StringComparer.Ordinal)
                .Select(group => group.subgroupKey + ":" + group.share + ":"
                    + F(group.meanOffset) + ":" + F(group.spreadMultiplier)));
            return HashText((value.questionKey ?? "question") + "|"
                + (value.populationScope ?? "*") + "|" + F(value.mean)
                + "|" + value.hasDescriptiveNormPrior + ":"
                + F(value.descriptiveNormPrior) + "|"
                + F(value.prestigeSignal)
                + "|" + F(value.spread) + ":" + value.spreadOverride
                + "|" + F(value.salience) + "|"
                + F(value.normStrength) + "|" + F(value.visibility) + "|"
                + F(value.sourceConfidence) + "|"
                + F(value.toleranceForDivergence) + "|" + groups);
        }

        public static string Fingerprint(
            IEnumerable<CACultureQuestionDistribution> values)
        {
            return HashText(string.Join("|", (values
                    ?? Enumerable.Empty<CACultureQuestionDistribution>())
                .Where(value => value != null)
                .OrderBy(value => value.questionKey, StringComparer.Ordinal)
                .ThenBy(value => value.populationScope,
                    StringComparer.Ordinal).Select(Fingerprint)));
        }

        private static string HashText(string value)
        {
            return StableHash(value).ToString("X8");
        }

        private static string F(float value)
        {
            return value.ToString("0.0000",
                System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool In(float value, float low, float high)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value)
                && value >= low && value <= high;
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char c in value ?? "")
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        private static float Unit(uint value)
        {
            return (value & 0x00FFFFFFu) / 16777215f;
        }

        private static double StandardNormal(uint first, uint second)
        {
            double u1 = Math.Max(0.000001d, Unit(first));
            double u2 = Math.Max(0.000001d, Unit(second));
            return Math.Sqrt(-2d * Math.Log(u1))
                * Math.Cos(2d * Math.PI * u2);
        }
    }
}
