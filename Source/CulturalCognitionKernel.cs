using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonistAwareness
{
    public enum CACultureQuestionLayer : byte
    {
        RelationshipsFamilySexuality,
        GenderSocialAuthority,
        StatusHierarchy,
        MembershipOutsiders,
        PublicAuthoritySocialOrder,
        PropertyLaborProvision,
        ViolenceCaptivityPunishment,
        KnowledgeTradition,
        BodyHealthDeath,
        FoodSubstances,
        AnimalsEnvironment,
        DailyLifeTechnology
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
        // Named represented facts or events capable of changing this
        // distribution over lived history. These are evidence routes, not
        // claims that the fact already exists.
        public string[] HistoricalSources;
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
            if (HistoricalSources == null || HistoricalSources.Length == 0
                || HistoricalSources.Any(string.IsNullOrWhiteSpace))
                return "historical evidence route is missing";
            if (ResearchProvenance == null || ResearchProvenance.Length == 0)
                return "research provenance is missing";
            return null;
        }
    }

    public sealed class CACultureQuestionSubjectAdapterDef
    {
        public string SubjectKey;
        public string QuestionKey;
        public int Direction;
        public string SourceKind;

        public CACultureQuestionSubjectAdapterDef(string subjectKey,
            string questionKey, int direction, string sourceKind)
        {
            SubjectKey = subjectKey;
            QuestionKey = questionKey;
            Direction = direction;
            SourceKind = sourceKind;
        }

        internal string ValidationFailure()
        {
            if (string.IsNullOrWhiteSpace(SubjectKey)
                || string.IsNullOrWhiteSpace(QuestionKey))
                return "subject and question identities are required";
            if (Direction != -1 && Direction != 1)
                return "adapter direction must be -1 or 1";
            if (SourceKind != "social fact" && SourceKind != "practice")
                return "adapter source kind is invalid";
            return null;
        }
    }

    // Culture owns population distributions over intelligible questions.
    // Actions, institutions, political rules, Ideoligion and knowledge remain
    // separate owners and appear here only through named evidence adapters.
    public static class CACultureQuestionRegistry
    {
        public const int CurrentVersion = 3;
        public const int LegacyQuestionCount = 24;
        public const int FixedQuestionCount = 48;
        public const string SameSexAcceptance =
            "relationships.sameSexAcceptance";
        public const string PluralityAcceptance =
            "relationships.pluralityAcceptance";
        public const string KinObligation =
            "relationships.kinObligation";
        public const string GenderDistribution =
            "authority.genderDistribution";
        public const string GenderedWork =
            "authority.genderedWork";
        public const string GenderOfficeAccess =
            "authority.officeAccess";
        public const string HereditaryLegitimacy =
            "status.hereditaryLegitimacy";
        public const string RankDifferentiation =
            "status.rankDifferentiation";
        public const string StatusMobility =
            "status.mobility";
        public const string OutsiderInclusion =
            "groups.outsiderInclusion";
        public const string IntegrationPreference =
            "groups.integrationPreference";
        public const string MembershipAccess =
            "groups.membershipAccess";
        public const string CoercionLegitimacy =
            "labor.coercionLegitimacy";
        public const string PropertyControl =
            "property.control";
        public const string VoiceInclusion =
            "voice.inclusionExpectation";
        public const string DissentTolerance =
            "voice.dissentTolerance";
        public const string EnforcementLegitimacy =
            "authority.enforcementLegitimacy";
        public const string CaptiveProtection =
            "war.captiveProtection";
        public const string PunishmentSeverity =
            "war.punishmentSeverity";
        public const string RetaliatoryViolence =
            "war.retaliatoryViolence";
        public const string MutualProvision =
            "provision.mutualObligation";
        public const string KnowledgeAccess = "knowledge.access";
        public const string NoveltyAcceptance =
            "knowledge.noveltyAcceptance";
        public const string ExpertiseDeference =
            "knowledge.expertiseDeference";
        public const string SexualConduct =
            "relationships.sexualConduct";
        public const string MarriageNaming =
            "relationships.marriageNaming";
        public const string ChildhoodProtection =
            "relationships.childhoodProtection";
        public const string AgeStanding = "status.ageStanding";
        public const string DoctrinalPluralism =
            "groups.doctrinalPluralism";
        public const string XenotypeHierarchy =
            "groups.xenotypeHierarchy";
        public const string WorkExpectation = "labor.workExpectation";
        public const string PredatoryAcquisition =
            "property.predatoryAcquisition";
        public const string ViolenceAcceptance =
            "war.violenceAcceptance";
        public const string MaleBodyExposure = "body.maleExposure";
        public const string FemaleBodyExposure = "body.femaleExposure";
        public const string BodilyAlteration = "body.alteration";
        public const string BodilyIntegrity = "body.integrity";
        public const string PainMeaning = "body.painMeaning";
        public const string HumanRemainsTreatment =
            "death.humanRemainsTreatment";
        public const string HumanFleshAcceptance =
            "food.humanFleshAcceptance";
        public const string AnimalFoodAcceptance =
            "food.animalFoodAcceptance";
        public const string FoodAdaptability = "food.adaptability";
        public const string RecreationalDrugUse =
            "substances.recreationalUse";
        public const string AnimalMoralStanding =
            "animals.moralStanding";
        public const string ResourceStewardship =
            "environment.resourceStewardship";
        public const string SettlementPermanence =
            "settlement.permanence";
        public const string ComfortExpectation =
            "daily.comfortExpectation";
        public const string MachineDelegation =
            "technology.machineDelegation";

        private static readonly double[] Centers =
            { -0.90d, -0.45d, 0d, 0.45d, 0.90d };
        private static readonly CACultureQuestionDef[] Definitions = Build();
        private static readonly Dictionary<string, CACultureQuestionDef> ByKey =
            Definitions.ToDictionary(value => value.Key, value => value,
                StringComparer.Ordinal);
        private static readonly CACultureQuestionSubjectAdapterDef[]
            SubjectAdapters = BuildSubjectAdapters();
        private static readonly Dictionary<string,
            CACultureQuestionSubjectAdapterDef[]> SubjectAdaptersByKey =
                SubjectAdapters.GroupBy(value => value.SubjectKey,
                        StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.ToArray(),
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
            if (Definitions.Length != FixedQuestionCount)
                return "the Culture question registry is incomplete";
            CACultureQuestionLayer[] layers = (CACultureQuestionLayer[])
                Enum.GetValues(typeof(CACultureQuestionLayer));
            if (Definitions.Select(value => value.Layer).Distinct().Count()
                    != layers.Length
                || layers.Any(layer => Definitions.All(value =>
                    value.Layer != layer)))
                return "one or more Culture categories are incomplete";
            if (Definitions.Select(value => value.Key).Distinct(
                    StringComparer.Ordinal).Count() != Definitions.Length)
                return "Culture question keys are duplicated";
            foreach (CACultureQuestionDef definition in Definitions)
            {
                string failure = definition.ValidationFailure();
                if (!string.IsNullOrWhiteSpace(failure))
                    return definition.Key + ": " + failure;
                if (CACultureQuestionExecutionRoutes.For(
                        definition.Key).Count == 0)
                    return definition.Key
                        + ": no executable evidence, feedback, or behavior route";
                foreach (string subject in (definition.SocialSubjectAdapters
                    ?? Array.Empty<string>()).Concat(
                        definition.PracticeEvidenceAdapters
                            ?? Array.Empty<string>()))
                    if (!AdaptersForSocialSubject(subject).Any(value =>
                            value.QuestionKey == definition.Key))
                        return definition.Key + ": " + subject
                            + " has no explicit signed subject adapter";
            }
            foreach (CACultureQuestionSubjectAdapterDef adapter in
                SubjectAdapters)
            {
                string failure = adapter.ValidationFailure();
                if (!string.IsNullOrWhiteSpace(failure))
                    return adapter.SubjectKey + ": " + failure;
                if (Find(adapter.QuestionKey) == null)
                    return adapter.SubjectKey + ": unknown question "
                        + adapter.QuestionKey;
                if (adapter.SourceKind == "practice"
                    && CACulturalPracticeRegistry.Find(adapter.SubjectKey)
                        == null)
                    return adapter.SubjectKey
                        + ": practice adapter has no practice definition";
                CACultureQuestionDef owner = Find(adapter.QuestionKey);
                string[] declared = adapter.SourceKind == "practice"
                    ? owner?.PracticeEvidenceAdapters
                    : owner?.SocialSubjectAdapters;
                if (declared == null || !declared.Contains(
                        adapter.SubjectKey, StringComparer.Ordinal))
                    return adapter.SubjectKey + ": signed adapter is not "
                        + "declared by " + adapter.QuestionKey;
            }
            if (SubjectAdapters.GroupBy(value => value.SubjectKey + "\0"
                        + value.QuestionKey, StringComparer.Ordinal)
                    .Any(group => group.Count() != 1))
                return "Culture subject adapters are duplicated";
            return null;
        }

        public static IReadOnlyList<CACultureQuestionSubjectAdapterDef>
            AdaptersForSocialSubject(string subjectKey)
        {
            if (subjectKey == null) return Array.Empty<
                CACultureQuestionSubjectAdapterDef>();
            return SubjectAdaptersByKey.TryGetValue(subjectKey,
                out CACultureQuestionSubjectAdapterDef[] values)
                ? values : Array.Empty<CACultureQuestionSubjectAdapterDef>();
        }

        public static string QuestionForSocialSubject(string subjectKey)
        {
            IReadOnlyList<CACultureQuestionSubjectAdapterDef> values =
                AdaptersForSocialSubject(subjectKey);
            return values.Count == 1 ? values[0].QuestionKey : null;
        }

        // Adapter polarity belongs beside the adapter itself. Private
        // ownership points away from common control; every other registered
        // subject points in the ordered direction of its question.
        public static int DirectionForSocialSubject(string subjectKey)
        {
            IReadOnlyList<CACultureQuestionSubjectAdapterDef> values =
                AdaptersForSocialSubject(subjectKey);
            return values.Count == 1 ? values[0].Direction : 0;
        }

        private static CACultureQuestionSubjectAdapterDef[]
            BuildSubjectAdapters()
        {
            const string Fact = "social fact";
            const string Practice = "practice";
            return new[]
            {
                S(CASocialSubjectRegistry.HouseholdMembership, KinObligation, 1, Fact),
                S(CASocialSubjectRegistry.HouseholdProvision, KinObligation, 1, Fact),
                S(CASocialSubjectRegistry.InheritedRank, HereditaryLegitimacy, 1, Fact),
                S(CASocialSubjectRegistry.KinSuccession, HereditaryLegitimacy, 1, Fact),
                S(CASocialSubjectRegistry.OfficeGovernance, RankDifferentiation, 1, Fact),
                S(CASocialSubjectRegistry.OfficeHolding, StatusMobility, 1, Fact),
                S(CASocialSubjectRegistry.OutsiderContact, OutsiderInclusion, 1, Fact),
                S(CASocialSubjectRegistry.FactionMembership, MembershipAccess, 1, Fact),
                S(CASocialSubjectRegistry.PublicVoice, VoiceInclusion, 1, Fact),
                S(CASocialSubjectRegistry.PublicGathering, VoiceInclusion, 1, Fact),
                S(CASocialSubjectRegistry.DelegatedAuthority, VoiceInclusion, 1, Fact),
                S(CASocialSubjectRegistry.EnforcedOrder, EnforcementLegitimacy, 1, Fact),
                S(CASocialSubjectRegistry.CompelledService, CoercionLegitimacy, 1, Fact),
                S(CASocialSubjectRegistry.SharedProvision, MutualProvision, 1, Fact),
                S(CASocialSubjectRegistry.MedicalCare, MutualProvision, 1, Fact),
                S(CASocialSubjectRegistry.StoredReserves, MutualProvision, 1, Fact),
                S(CASocialSubjectRegistry.AuthorityProvision, MutualProvision, 1, Fact),
                S(CASocialSubjectRegistry.CommonOwnership, PropertyControl, 1, Fact),
                S(CASocialSubjectRegistry.PrivateOwnership, PropertyControl, -1, Fact),
                S(CASocialSubjectRegistry.HumaneCustody, CaptiveProtection, 1, Fact),
                S(CASocialSubjectRegistry.QuarterGiven, CaptiveProtection, 1, Fact),
                S(CACulturalPracticeRegistry.Execution, CaptiveProtection, -1, Practice),
                S(CACulturalPracticeRegistry.Enslavement, CaptiveProtection, -1, Practice),
                S(CASocialSubjectRegistry.CustodyPunishment, PunishmentSeverity, 1, Fact),
                S(CACulturalPracticeRegistry.Execution, PunishmentSeverity, 1, Practice),
                S(CASocialSubjectRegistry.CombatViolence, RetaliatoryViolence, 1, Fact),
                S(CASocialSubjectRegistry.KnowledgeTransmission, KnowledgeAccess, 1, Fact),
                S(CASocialSubjectRegistry.LongRangeCommunication, KnowledgeAccess, 1, Fact),
                S(CACulturalPracticeRegistry.OrganizedResearch, KnowledgeAccess, 1, Practice),
                S(CASocialSubjectRegistry.ResearchWork, NoveltyAcceptance, 1, Fact),
                S(CACulturalPracticeRegistry.OrganizedResearch, NoveltyAcceptance, 1, Practice),
                S(CASocialSubjectRegistry.SpecializedCraft, ExpertiseDeference, 1, Fact),
                S(CACulturalPracticeRegistry.HousingUpkeep, WorkExpectation, 1, Practice),
                S(CACulturalPracticeRegistry.MealPreparation, WorkExpectation, 1, Practice),
                S(CACulturalPracticeRegistry.ReserveStorage, WorkExpectation, 1, Practice),
                S(CACulturalPracticeRegistry.MedicalCare, WorkExpectation, 1, Practice),
                S(CACulturalPracticeRegistry.GeneralCraft, WorkExpectation, 1, Practice),
                S(CACulturalPracticeRegistry.SpecializedCraft, WorkExpectation, 1, Practice),
                S(CACulturalPracticeRegistry.OrganizedResearch, WorkExpectation, 1, Practice),
                S(CACulturalPracticeRegistry.Cultivation, WorkExpectation, 1, Practice),
                S(CACulturalPracticeRegistry.AnimalTending, WorkExpectation, 1, Practice),
                S(CACulturalPracticeRegistry.TransportService, WorkExpectation, 1, Practice),
                S(CACulturalPracticeRegistry.RepairAndRebuilding, WorkExpectation, 1, Practice),
                S(CACulturalPracticeRegistry.NonSpousalIntimacy, SexualConduct, 1, Practice),
                S(CACulturalPracticeRegistry.ExclusiveMarriage, PluralityAcceptance, -1, Practice),
                S(CACulturalPracticeRegistry.PluralMarriage, PluralityAcceptance, 1, Practice),
                S(CACulturalPracticeRegistry.DoctrinalChange, DoctrinalPluralism, 1, Practice),
                S(CACulturalPracticeRegistry.CrossIdeoligionObservance, DoctrinalPluralism, 1, Practice),
                S(CACulturalPracticeRegistry.Raiding, PredatoryAcquisition, 1, Practice),
                S(CACulturalPracticeRegistry.DownedPersonStripping, PredatoryAcquisition, 1, Practice),
                S(CACulturalPracticeRegistry.CombatConduct, ViolenceAcceptance, 1, Practice),
                S(CACulturalPracticeRegistry.InterpersonalViolence, ViolenceAcceptance, 1, Practice),
                S(CACulturalPracticeRegistry.BodyModification, BodilyAlteration, 1, Practice),
                S(CACulturalPracticeRegistry.RitualInjury, BodilyAlteration, 1, Practice),
                S(CACulturalPracticeRegistry.OrganExtraction, BodilyIntegrity, -1, Practice),
                S(CACulturalPracticeRegistry.OrganTrade, BodilyIntegrity, -1, Practice),
                S(CACulturalPracticeRegistry.RitualInjury, PainMeaning, 1, Practice),
                S(CACulturalPracticeRegistry.HumanButchery, HumanRemainsTreatment, -1, Practice),
                S(CACulturalPracticeRegistry.CorpseExposure, HumanRemainsTreatment, -1, Practice),
                S(CACulturalPracticeRegistry.HumanFleshConsumption, HumanFleshAcceptance, 1, Practice),
                S(CACulturalPracticeRegistry.AnimalFoodConsumption, AnimalFoodAcceptance, 1, Practice),
                S(CACulturalPracticeRegistry.AnimalSlaughter, AnimalFoodAcceptance, 1, Practice),
                S(CACulturalPracticeRegistry.UnfamiliarFoodConsumption, FoodAdaptability, 1, Practice),
                S(CACulturalPracticeRegistry.DrugUse, RecreationalDrugUse, 1, Practice),
                S(CACulturalPracticeRegistry.DrugAdministration, RecreationalDrugUse, 1, Practice),
                S(CACulturalPracticeRegistry.AnimalSlaughter, AnimalMoralStanding, -1, Practice),
                S(CACulturalPracticeRegistry.InnocentAnimalKilling, AnimalMoralStanding, -1, Practice),
                S(CACulturalPracticeRegistry.ResourceExtraction, ResourceStewardship, -1, Practice),
                S(CACulturalPracticeRegistry.SettlementAbandonment, SettlementPermanence, -1, Practice)
            };
        }

        private static CACultureQuestionSubjectAdapterDef S(string subject,
            string question, int direction, string sourceKind) =>
            new CACultureQuestionSubjectAdapterDef(subject, question,
                direction, sourceKind);

        private static CACultureQuestionDef[] Build()
        {
            const string Schwartz = "Schwartz value theory and cross-cultural value structure";
            const string Norms = "descriptive and injunctive norm distinction";
            const string Ideology = "RimWorld Ideoligion precept adapter evidence";
            const string Institutions = "institutional legitimacy and represented-practice evidence";
            const string Wvs = "World Values Survey wave 7 questionnaire";
            const string Issp = "ISSP Family and Changing Gender Roles V questionnaire";
            const string Ess = "European Social Survey rotating modules";
            const string Gss = "General Social Survey social-change series";
            // B16 specialist source-family keys. The governed Culture corpus
            // resolves each key to a named publication and states the narrow
            // decomposition decision it supports. These are provenance, not
            // substitute mechanics or empirical calibration.
            const string MarriageName = "MarriageName";
            const string ChildLabor = "ChildLabor";
            const string AgeNorms = "AgeNorms";
            const string SocialDominance = "SocialDominance";
            const string Modesty = "Modesty";
            const string BodyModification = "BodyModification";
            const string BodilyIntegrityResearch = "BodilyIntegrity";
            const string RitualPain = "RitualPain";
            const string Mortuary = "Mortuary";
            const string FoodDisgust = "FoodDisgust";
            const string MoralExpansiveness = "MoralExpansiveness";
            const string EnvironmentalAttitudes = "EnvironmentalAttitudes";
            const string MobilitySedentism = "MobilitySedentism";
            const string RobotAcceptance = "RobotAcceptance";
            return new[]
            {
                Q(SameSexAcceptance, "Same-sex relationship acceptance",
                    "How are same-sex relationships regarded?",
                    "approval of same-sex romantic relationships",
                    "condemnation to affirmation", CACultureQuestionLayer.RelationshipsFamilySexuality,
                    A("Strongly condemned", "Disapproved", "Tolerated", "Accepted", "Affirmed"),
                    Array.Empty<string>(),
                    Array.Empty<string>(), Array.Empty<string>(),
                    A("romance appraisal", "public expression"),
                    A("membership conflict"), A("relationship legitimacy"),
                    Array.Empty<string>(), A("same-sex romance outcomes and known unions"),
                    A(Schwartz, Norms, Wvs, Gss)),
                Q(PluralityAcceptance, "Relationship plurality acceptance",
                    "How are plural unions regarded?",
                    "approval of sex-neutral relationship plurality",
                    "exclusive unions to preferred plural unions",
                    CACultureQuestionLayer.RelationshipsFamilySexuality,
                    A("Exclusive only", "Exclusivity preferred", "Plural unions tolerated", "Plural unions accepted", "Plural unions preferred"),
                    new[] { "spouse-count precepts" }, Array.Empty<string>(),
                    A(CACulturalPracticeRegistry.ExclusiveMarriage,
                        CACulturalPracticeRegistry.PluralMarriage),
                    A("household formation", "jealousy appraisal"),
                    Array.Empty<string>(), A("relationship rules"),
                    Array.Empty<string>(), A("plural relationship outcomes and household composition"),
                    A(Schwartz, Norms, Ideology, Wvs)),
                Q(KinObligation, "Obligation to kin",
                    "How far do duties to kin extend?",
                    "expected material and personal obligation to kin",
                    "individual discretion to binding extended-kin duty",
                    CACultureQuestionLayer.RelationshipsFamilySexuality,
                    A("Individual discretion", "Immediate household", "Close kin", "Extended kin", "Binding kin duty"),
                    Array.Empty<string>(),
                    A(CASocialSubjectRegistry.HouseholdMembership,
                        CASocialSubjectRegistry.HouseholdProvision),
                    Array.Empty<string>(), A("family aid", "care obligation"),
                    A("kin-support politics"), A("household support rules"),
                    Array.Empty<string>(), A("household membership and kin provision records"),
                    A(Schwartz, Norms, Wvs, Issp)),
                Q(GenderDistribution, "Gender distribution of authority",
                    "How should authority be distributed between women and men?",
                    "legitimacy of gendered authority distribution",
                    "male dominance through symmetry to female dominance",
                    CACultureQuestionLayer.GenderSocialAuthority,
                    A("Strongly male-dominant", "Male-leaning", "Symmetric", "Female-leaning", "Strongly female-dominant"),
                    new[] { "gender-supremacy precepts" }, Array.Empty<string>(),
                    Array.Empty<string>(), A("command legitimacy"),
                    A("office support", "participation conflict"),
                    A("office selection"), Array.Empty<string>(),
                    A("gender of represented officeholders and binding decision-makers"),
                    A(Schwartz, Norms, Ideology, Institutions, Wvs, Issp)),
                Q(GenderedWork, "Gendered work expectations",
                    "How strongly should work be divided by gender?",
                    "approval of gender-specific work obligations",
                    "open work to rigid gender division",
                    CACultureQuestionLayer.GenderSocialAuthority,
                    A("Open to all", "Mostly open", "Some customary division", "Strongly divided", "Rigidly divided"),
                    Array.Empty<string>(), Array.Empty<string>(),
                    Array.Empty<string>(), A("work assignment appraisal"),
                    A("labor participation conflict"), A("work eligibility rules"),
                    Array.Empty<string>(), A("represented work assignments by gender and role"),
                    A(Norms, Wvs, Issp, Gss)),
                Q(GenderOfficeAccess, "Gender access to office",
                    "Who may hold public office?",
                    "gender breadth of eligibility for represented office",
                    "gender-restricted to unrestricted office access",
                    CACultureQuestionLayer.GenderSocialAuthority,
                    A("One gender only", "Strong preference", "Conditional access", "Broad access", "Equal access"),
                    Array.Empty<string>(), Array.Empty<string>(),
                    Array.Empty<string>(), A("office appointment appraisal"),
                    A("office-access support"), A("office eligibility"),
                    Array.Empty<string>(), A("appointments, elections, and office tenure by gender"),
                    A(Norms, Institutions, Wvs, Issp, Gss)),
                Q(HereditaryLegitimacy, "Hereditary status legitimacy",
                    "How legitimate is inherited status?",
                    "legitimacy attributed to inherited status and succession",
                    "illegitimate to naturalized", CACultureQuestionLayer.StatusHierarchy,
                    A("Illegitimate", "Disfavored", "Permitted", "Respected", "Naturalized"),
                    Array.Empty<string>(),
                    A(CASocialSubjectRegistry.InheritedRank,
                        CASocialSubjectRegistry.KinSuccession),
                    Array.Empty<string>(), A("succession appraisal"),
                    A("status support"), A("office legitimacy"),
                    Array.Empty<string>(), A("represented inherited rank and kin succession"),
                    A(Schwartz, Norms, Institutions)),
                Q(RankDifferentiation, "Social rank differentiation",
                    "How much durable social rank is proper?",
                    "approval of durable rank differentiation",
                    "rank rejection to entrenched rank", CACultureQuestionLayer.StatusHierarchy,
                    A("Rank rejected", "Rank minimized", "Mixed", "Rank accepted", "Rank entrenched"),
                    Array.Empty<string>(),
                    A(CASocialSubjectRegistry.OfficeGovernance),
                    Array.Empty<string>(), A("deference appraisal"),
                    A("status and resource legitimacy"),
                    A("office privilege"), Array.Empty<string>(), A("office privilege and status assignments"),
                    A(Schwartz, Norms, Institutions)),
                Q(StatusMobility, "Movement between social ranks",
                    "How open should movement between ranks be?",
                    "expected permeability of durable social status",
                    "fixed rank to open mobility",
                    CACultureQuestionLayer.StatusHierarchy,
                    A("Fixed at birth", "Rare movement", "Limited movement", "Mobility expected", "Open mobility"),
                    Array.Empty<string>(), A(CASocialSubjectRegistry.OfficeHolding),
                    Array.Empty<string>(), A("promotion and demotion appraisal"),
                    A("status-mobility support"), A("appointment and standing rules"),
                    Array.Empty<string>(), A("represented changes in office, standing, and rank"),
                    A(Schwartz, Norms, Institutions, Wvs)),
                Q(OutsiderInclusion, "Outsider social inclusion",
                    "How readily are outsiders included in social life?",
                    "social inclusion of people treated as outsiders",
                    "exclusionary to integrative", CACultureQuestionLayer.MembershipOutsiders,
                    A("Exclusionary", "Guarded", "Selective", "Receptive", "Integrative"),
                    new[] { "newcomer-attitude precepts" },
                    A(CASocialSubjectRegistry.OutsiderContact),
                    Array.Empty<string>(), A("hospitality", "recruitment", "intermarriage"),
                    A("membership conflict"), A("access and membership"),
                    Array.Empty<string>(), A("hospitality, rescue, recruitment, and outsider contact"),
                    A(Schwartz, Norms, Wvs, Ess)),
                Q(IntegrationPreference, "Intergroup integration",
                    "How much intergroup integration is expected?",
                    "preference for separation or integration between groups",
                    "required separation to expected integration",
                    CACultureQuestionLayer.MembershipOutsiders,
                    A("Separation required", "Separation preferred", "Context-dependent", "Integration preferred", "Integration expected"),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(), A("mixed-group interaction"),
                    A("integration politics"),
                    A("residence and access policy"), Array.Empty<string>(),
                    A("mixed-population residence, work, and social ties"),
                    A(Schwartz, Norms, Institutions, Ess)),
                Q(MembershipAccess, "Access to group membership",
                    "How readily may outsiders become full members?",
                    "permeability of formal group membership",
                    "closed descent to open membership",
                    CACultureQuestionLayer.MembershipOutsiders,
                    A("Closed by descent", "Rare admission", "Conditional admission", "Accessible", "Open membership"),
                    Array.Empty<string>(), A(CASocialSubjectRegistry.FactionMembership),
                    Array.Empty<string>(), A("recruitment and naturalization"),
                    A("membership-access politics"), A("membership procedure"),
                    Array.Empty<string>(), A("represented admissions, exclusions, and membership changes"),
                    A(Norms, Institutions, Wvs, Ess)),

                Q(VoiceInclusion, "Inclusion in public voice",
                    "Who is expected to have a public voice?",
                    "expected breadth of public political voice",
                    "reserved voice to universal voice",
                    CACultureQuestionLayer.PublicAuthoritySocialOrder,
                    A("Voice reserved", "Voice restricted", "Voice conditional", "Voice broadly expected", "Voice universally expected"),
                    Array.Empty<string>(),
                    A(CASocialSubjectRegistry.PublicVoice,
                        CASocialSubjectRegistry.PublicGathering,
                        CASocialSubjectRegistry.DelegatedAuthority),
                    Array.Empty<string>(), A("meeting participation", "public decisions"),
                    A("participation conflict"), A("eligibility and decision procedure"),
                    Array.Empty<string>(), A("represented participation in binding decisions"),
                    A(Schwartz, Norms, Institutions, Wvs, Ess)),
                Q(DissentTolerance, "Tolerance of public dissent",
                    "How much open disagreement should public order permit?",
                    "legitimacy of expressing disagreement with binding rules",
                    "suppressed dissent to protected dissent",
                    CACultureQuestionLayer.PublicAuthoritySocialOrder,
                    A("Suppressed", "Strongly discouraged", "Conditionally allowed", "Tolerated", "Protected"),
                    Array.Empty<string>(), Array.Empty<string>(),
                    Array.Empty<string>(), A("objection", "protest", "public disagreement"),
                    A("dissent conflict"), A("speech and meeting rules"),
                    Array.Empty<string>(), A("represented objections, protests, sanctions, and tolerated criticism"),
                    A(Norms, Institutions, Wvs, Ess, Gss)),
                Q(EnforcementLegitimacy, "Legitimacy of enforced order",
                    "When is force used to uphold public order legitimate?",
                    "legitimacy attributed to institutional enforcement",
                    "force rejected to routine enforcement",
                    CACultureQuestionLayer.PublicAuthoritySocialOrder,
                    A("Force rejected", "Emergency only", "Narrowly authorized", "Broadly authorized", "Routine enforcement"),
                    Array.Empty<string>(), A(CASocialSubjectRegistry.EnforcedOrder),
                    Array.Empty<string>(), A("order enforcement appraisal"),
                    A("public-order support"), A("sanction and enforcement rules"),
                    Array.Empty<string>(), A("represented orders, resistance, enforcement, and sanctions"),
                    A(Norms, Institutions, Wvs, Ess)),

                Q(CoercionLegitimacy, "Coercive labor legitimacy",
                    "When is compelled labor legitimate?",
                    "legitimacy attributed to compelled labor",
                    "never legitimate to institutionally expected",
                    CACultureQuestionLayer.PropertyLaborProvision,
                    A("Never legitimate", "Emergency only", "Conditionally tolerated", "Broadly accepted", "Institutionally expected"),
                    new[] { "slavery and work precepts" },
                    A(CASocialSubjectRegistry.CompelledService),
                    Array.Empty<string>(),
                    A("work refusal", "enforcement response"),
                    A("labor conflict"), A("work rules"),
                    Array.Empty<string>(), A("compelled work, refusal, and actual enforcement"),
                    A(Schwartz, Norms, Ideology, Institutions, Wvs)),
                Q(MutualProvision, "Mutual provision obligation",
                    "Who is expected to provide during hardship?",
                    "breadth of obligation to provide food, shelter and care",
                    "household responsibility to collective guarantee",
                    CACultureQuestionLayer.PropertyLaborProvision,
                    A("Household only", "Voluntary aid", "Mixed responsibility", "Shared responsibility", "Collective guarantee"),
                    new[] { "charity and communal precepts" },
                    A(CASocialSubjectRegistry.SharedProvision,
                        CASocialSubjectRegistry.MedicalCare,
                        CASocialSubjectRegistry.StoredReserves,
                        CASocialSubjectRegistry.AuthorityProvision),
                    Array.Empty<string>(), A("aid", "food, shelter, and medical provision"),
                    A("support politics"), A("provision systems"),
                    Array.Empty<string>(), A("represented provision, reserves, unmet need, and care"),
                    A(Schwartz, Norms, Ideology, Institutions, Wvs)),
                Q(PropertyControl, "Control of property",
                    "How broadly should control of productive property be shared?",
                    "legitimacy of concentrated or shared control over productive property",
                    "concentrated private control to common control",
                    CACultureQuestionLayer.PropertyLaborProvision,
                    A("Concentrated private control", "Private control favored", "Mixed control", "Shared control favored", "Common control"),
                    Array.Empty<string>(), A(CASocialSubjectRegistry.CommonOwnership,
                        CASocialSubjectRegistry.PrivateOwnership),
                    Array.Empty<string>(), A("ownership dispute", "resource allocation"),
                    A("property-control politics"), A("ownership and transfer rules"),
                    Array.Empty<string>(), A("represented ownership, common stores, transfers, and disputes"),
                    A(Schwartz, Norms, Institutions, Wvs)),

                Q(CaptiveProtection, "Protection owed to defeated people",
                    "What protection is owed to defeated people?",
                    "duty of restraint and care toward defeated people",
                    "no restraint to strong duty of care",
                    CACultureQuestionLayer.ViolenceCaptivityPunishment,
                    A("No expected restraint", "Minimal restraint", "Conditional protection", "Protection expected", "Strong duty of care"),
                    new[] { "execution, slavery and war-conduct precepts" },
                    A(CASocialSubjectRegistry.HumaneCustody,
                        CASocialSubjectRegistry.QuarterGiven),
                    A(
                        CACulturalPracticeRegistry.Execution,
                        CACulturalPracticeRegistry.Enslavement),
                    A("surrender", "custody", "execution", "medical care"),
                    A("war legitimacy"), A("custody rules"),
                    Array.Empty<string>(), A("surrender, custody, execution, rescue, and medical care outcomes"),
                    A(Schwartz, Norms, Ideology, Institutions)),
                Q(PunishmentSeverity, "Severity of punishment",
                    "How severe should punishment be after wrongdoing?",
                    "approval of punitive severity in represented sanctions",
                    "restorative restraint to severe punishment",
                    CACultureQuestionLayer.ViolenceCaptivityPunishment,
                    A("Restorative restraint", "Mild penalties", "Proportional punishment", "Harsh punishment", "Exemplary severity"),
                    new[] { "execution and punishment precepts" },
                    A(CASocialSubjectRegistry.CustodyPunishment),
                    A(CACulturalPracticeRegistry.Execution),
                    A("punishment and clemency appraisal"),
                    A("punishment politics"), A("sanction schedule"),
                    Array.Empty<string>(), A("represented sanctions, punishment, clemency, and recidivism"),
                    A(Norms, Ideology, Institutions, Gss)),
                Q(RetaliatoryViolence, "Retaliatory violence",
                    "When is retaliatory violence expected?",
                    "approval of violence undertaken to repay prior harm",
                    "restraint to obligatory retaliation",
                    CACultureQuestionLayer.ViolenceCaptivityPunishment,
                    A("Retaliation rejected", "Defense only", "Proportional reply", "Retaliation approved", "Retaliation required"),
                    Array.Empty<string>(), A(CASocialSubjectRegistry.CombatViolence),
                    Array.Empty<string>(), A("revenge and reprisal appraisal"),
                    A("war and feud support"), A("reprisal restraint"),
                    Array.Empty<string>(), A("represented attacks, prior harm, reprisals, and peace agreements"),
                    A(Schwartz, Norms, Institutions, Wvs)),

                Q(KnowledgeAccess, "Access to established knowledge",
                    "Who should have access to established knowledge?",
                    "breadth of access to represented established knowledge",
                    "esoteric to open", CACultureQuestionLayer.KnowledgeTradition,
                    A("Esoteric", "Restricted", "Credentialed", "Broad", "Open"),
                    Array.Empty<string>(),
                    A(CASocialSubjectRegistry.KnowledgeTransmission,
                        CASocialSubjectRegistry.LongRangeCommunication),
                    A("ca.practice.organized_research"),
                    A("teaching", "publication"), A("knowledge-access politics"),
                    A("archive and school access"),
                    A("proposition access", "transmission"), A("represented teaching, publication, archives, and proposition access"),
                    A(Schwartz, Norms, Institutions, Wvs)),
                Q(NoveltyAcceptance, "Acceptance of novel claims",
                    "How readily are novel claims accepted for testing and use?",
                    "openness to evaluating and adopting novel claims",
                    "tradition-bound to experimental",
                    CACultureQuestionLayer.KnowledgeTradition,
                    A("Tradition-bound", "Suspicious", "Selective", "Receptive", "Experimental"),
                    Array.Empty<string>(),
                    A(CASocialSubjectRegistry.ResearchWork),
                    A("ca.practice.organized_research"),
                    A("research adoption", "foreign knowledge"),
                    A("innovation politics"), A("method rules"),
                    A("claim evaluation", "research adoption"), A("represented research attempts, corroboration, adoption, and observed payoff"),
                    A(Schwartz, Norms, Ideology, Institutions, Wvs)),
                Q(ExpertiseDeference, "Deference to demonstrated expertise",
                    "How much weight should demonstrated expertise carry?",
                    "legitimacy of domain expertise as a source of judgment",
                    "status-indifferent to expert-led judgment",
                    CACultureQuestionLayer.KnowledgeTradition,
                    A("Status-indifferent", "Limited weight", "One consideration", "Expert weight", "Expert-led"),
                    Array.Empty<string>(), A(CASocialSubjectRegistry.SpecializedCraft),
                    Array.Empty<string>(), A("advice and skilled-work appraisal"),
                    A("expert-role support"), A("credential and office rules"),
                    A("source weighting", "expert testimony"), A("represented advice, skill, source accuracy, and task outcomes"),
                    A(Schwartz, Norms, Institutions, Wvs, Ess)),

                Q(SexualConduct, "Sexual conduct",
                    "How is consensual sex outside marriage regarded?",
                    "approval of consensual sexual conduct outside marriage",
                    "prohibited to freely accepted",
                    CACultureQuestionLayer.RelationshipsFamilySexuality,
                    A("Prohibited", "Strongly disapproved", "Private tolerance", "Accepted", "Freely accepted"),
                    A("lovin precepts"), Array.Empty<string>(),
                    A(CACulturalPracticeRegistry.NonSpousalIntimacy),
                    A("relationship appraisal", "social response"),
                    Array.Empty<string>(), A("marriage and conduct rules"),
                    Array.Empty<string>(),
                    A("represented intimate relationships and public responses"),
                    A(Norms, Ideology, Wvs, Gss)),
                Q(MarriageNaming, "Marriage naming custom",
                    "Whose name is normally taken at marriage?",
                    "expected surname continuity after marriage",
                    "husband's line through separate names to wife's line",
                    CACultureQuestionLayer.RelationshipsFamilySexuality,
                    A("Husband's name expected", "Husband's name preferred", "Names kept or chosen", "Wife's name preferred", "Wife's name expected"),
                    A("marriage-name precepts"), Array.Empty<string>(),
                    Array.Empty<string>(),
                    A("marriage-name appraisal"), Array.Empty<string>(),
                    A("marriage registration custom"), Array.Empty<string>(),
                    A("represented marriages and name changes"),
                    A(Norms, Ideology, Issp, Gss, MarriageName)),
                Q(ChildhoodProtection, "Protection of childhood",
                    "How strongly should children be protected from adult work and risk?",
                    "expected protection of children from adult obligations",
                    "adult obligations to protected childhood",
                    CACultureQuestionLayer.RelationshipsFamilySexuality,
                    A("Adult duties expected", "Early work accepted", "Mixed expectations", "Childhood protected", "Strongly protected"),
                    A("child-labor precepts"), Array.Empty<string>(),
                    Array.Empty<string>(), A("child work and care appraisal"),
                    A("child-welfare politics"), A("work eligibility and care rules"),
                    Array.Empty<string>(),
                    A("represented child work, education, care, and harm"),
                    A(Norms, Institutions, Wvs, Issp, ChildLabor)),
                Q(AgeStanding, "Standing by age",
                    "How much standing should age carry?",
                    "social standing attributed to age",
                    "youth preference to elder authority",
                    CACultureQuestionLayer.StatusHierarchy,
                    A("Youth favored", "Youth-leaning", "Age-neutral", "Elders respected", "Elders authoritative"),
                    A("age-standing precepts"), Array.Empty<string>(),
                    Array.Empty<string>(), A("age-based deference appraisal"),
                    A("age and office support"), A("age qualifications"),
                    Array.Empty<string>(),
                    A("represented office, care, work, and deference by age"),
                    A(Schwartz, Norms, Institutions, Wvs, AgeNorms)),
                Q(DoctrinalPluralism, "Religious and doctrinal pluralism",
                    "How are different Ideoligions regarded?",
                    "acceptance of doctrinal difference within a population",
                    "one required doctrine to active pluralism",
                    CACultureQuestionLayer.MembershipOutsiders,
                    A("One doctrine required", "Difference disapproved", "Difference tolerated", "Difference accepted", "Pluralism valued"),
                    A("Ideoligion-diversity precepts"), Array.Empty<string>(),
                    A(CACulturalPracticeRegistry.DoctrinalChange,
                        CACulturalPracticeRegistry.CrossIdeoligionObservance),
                    A("conversion and doctrinal-difference appraisal"),
                    A("membership conflict"), A("religious access and membership rules"),
                    Array.Empty<string>(),
                    A("represented conversions, mixed Ideoligions, and public responses"),
                    A(Schwartz, Norms, Ideology, Wvs, Ess)),
                Q(XenotypeHierarchy, "Xenotype status",
                    "How much social standing should xenotype determine?",
                    "legitimacy of xenotype-based social hierarchy",
                    "xenotype-neutral to hereditary hierarchy",
                    CACultureQuestionLayer.MembershipOutsiders,
                    A("Xenotype-neutral", "Differences minimized", "Differences recognized", "Preferred types favored", "Hereditary hierarchy"),
                    A("preferred-xenotype precepts"), Array.Empty<string>(),
                    Array.Empty<string>(), A("xenotype appraisal and recruitment"),
                    A("membership and status conflict"), A("xenotype eligibility rules"),
                    Array.Empty<string>(),
                    A("represented xenotype composition, status, and treatment"),
                    A(Norms, Institutions, Wvs, SocialDominance)),
                Q(WorkExpectation, "Expected contribution to work",
                    "How strongly is able participation in work expected?",
                    "normative expectation of contribution to shared work",
                    "personal discretion to universal duty",
                    CACultureQuestionLayer.PropertyLaborProvision,
                    A("Personal discretion", "Contribution encouraged", "Contribution expected", "Strong work duty", "Universal work duty"),
                    Array.Empty<string>(), Array.Empty<string>(),
                    A(CACulturalPracticeRegistry.HousingUpkeep,
                        CACulturalPracticeRegistry.MealPreparation,
                        CACulturalPracticeRegistry.ReserveStorage,
                        CACulturalPracticeRegistry.MedicalCare,
                        CACulturalPracticeRegistry.GeneralCraft,
                        CACulturalPracticeRegistry.SpecializedCraft,
                        CACulturalPracticeRegistry.OrganizedResearch,
                        CACulturalPracticeRegistry.Cultivation,
                        CACulturalPracticeRegistry.AnimalTending,
                        CACulturalPracticeRegistry.TransportService,
                        CACulturalPracticeRegistry.RepairAndRebuilding),
                    A("work participation appraisal"),
                    A("labor-duty support"), A("work obligation rules"),
                    Array.Empty<string>(),
                    A("represented work participation, refusal, incapacity, and support"),
                    A(Schwartz, Norms, Institutions, Wvs)),
                Q(PredatoryAcquisition, "Taking by force",
                    "When is taking resources by force acceptable?",
                    "approval of acquiring property through coercion or raid",
                    "never acceptable to expected",
                    CACultureQuestionLayer.PropertyLaborProvision,
                    A("Never acceptable", "Strongly disapproved", "Emergency only", "Accepted against outsiders", "Expected"),
                    A("raiding and taking-from-downed precepts"), Array.Empty<string>(),
                    A(CACulturalPracticeRegistry.Raiding,
                        CACulturalPracticeRegistry.DownedPersonStripping),
                    A("raiding and seizure appraisal"), A("war and property support"),
                    A("seizure and salvage rules"), Array.Empty<string>(),
                    A("represented raids, seizures, stripping, and restitution"),
                    A(Norms, Ideology, Institutions)),
                Q(ViolenceAcceptance, "Use of violence",
                    "When is interpersonal violence acceptable?",
                    "approval of violence beyond immediate necessity",
                    "pacifism to celebrated violence",
                    CACultureQuestionLayer.ViolenceCaptivityPunishment,
                    A("Pacifist", "Violence strongly disapproved", "Defense accepted", "Violence broadly accepted", "Violence celebrated"),
                    A("violence precepts"), Array.Empty<string>(),
                    A(CACulturalPracticeRegistry.CombatConduct,
                        CACulturalPracticeRegistry.InterpersonalViolence),
                    A("violent conduct appraisal"), A("security and war support"),
                    A("use-of-force rules"), Array.Empty<string>(),
                    A("represented assaults, battles, defense, and social response"),
                    A(Schwartz, Norms, Ideology, Institutions, Wvs)),

                Q(MaleBodyExposure, "Men's body exposure",
                    "How much of a man's body is expected to be covered?",
                    "modesty expectations applied to male bodies",
                    "required covering to accepted nudity",
                    CACultureQuestionLayer.BodyHealthDeath,
                    A("Full covering expected", "Most covering expected", "Groin covered", "Exposure accepted", "Nudity accepted"),
                    A("male nudity precepts"), Array.Empty<string>(),
                    Array.Empty<string>(), A("apparel and exposure appraisal"),
                    Array.Empty<string>(), A("dress rules"), Array.Empty<string>(),
                    A("represented apparel, exposure, and social response"),
                    A(Norms, Ideology, Wvs, Issp, Modesty)),
                Q(FemaleBodyExposure, "Women's body exposure",
                    "How much of a woman's body is expected to be covered?",
                    "modesty expectations applied to female bodies",
                    "required covering to accepted nudity",
                    CACultureQuestionLayer.BodyHealthDeath,
                    A("Full covering expected", "Most covering expected", "Groin covered", "Exposure accepted", "Nudity accepted"),
                    A("female nudity precepts"), Array.Empty<string>(),
                    Array.Empty<string>(), A("apparel and exposure appraisal"),
                    Array.Empty<string>(), A("dress rules"), Array.Empty<string>(),
                    A("represented apparel, exposure, and social response"),
                    A(Norms, Ideology, Wvs, Issp, Modesty)),
                Q(BodilyAlteration, "Bodily alteration",
                    "How is deliberate alteration of the body regarded?",
                    "approval of deliberate bodily alteration",
                    "body preservation to celebrated alteration",
                    CACultureQuestionLayer.BodyHealthDeath,
                    A("Strongly opposed", "Disapproved", "Accepted when useful", "Approved", "Celebrated"),
                    A("body-modification, biosculpting, scarification, and blindness precepts"),
                    Array.Empty<string>(), A(CACulturalPracticeRegistry.BodyModification,
                        CACulturalPracticeRegistry.RitualInjury),
                    A("body modification appraisal"), Array.Empty<string>(),
                    A("medical and ritual authorization"), A("medical capability"),
                    A("represented implants, biosculpting, scarification, and blinding"),
                    A(Norms, Ideology, Institutions, BodyModification)),
                Q(BodilyIntegrity, "Bodily integrity",
                    "How strongly should a person's body be protected from non-consensual use?",
                    "duty to protect bodily integrity in medicine and extraction",
                    "instrumental use to strong protection",
                    CACultureQuestionLayer.BodyHealthDeath,
                    A("Instrumental use accepted", "Weak protection", "Consent-dependent", "Protection expected", "Strong protection"),
                    A("organ-use precepts"), Array.Empty<string>(),
                    A(CACulturalPracticeRegistry.OrganExtraction,
                        CACulturalPracticeRegistry.OrganTrade),
                    A("organ use and medical appraisal"), A("medical-rights support"),
                    A("medical consent and extraction rules"), A("medical capability"),
                    A("represented extraction, implantation, sale, and consent"),
                    A(Norms, Ideology, Institutions, Wvs,
                        BodilyIntegrityResearch)),
                Q(PainMeaning, "Meaning of pain",
                    "How is deliberate suffering regarded?",
                    "social meaning attributed to endured or imposed pain",
                    "harm to avoid through accepted ordeal to virtue",
                    CACultureQuestionLayer.BodyHealthDeath,
                    A("Pain should be prevented", "Pain disfavored", "Pain has no special meaning", "Ordeal respected", "Pain treated as virtue"),
                    A("pain and ritual-injury precepts"), Array.Empty<string>(),
                    A(CACulturalPracticeRegistry.RitualInjury),
                    A("pain and ordeal appraisal"), Array.Empty<string>(),
                    A("medical and ritual rules"), Array.Empty<string>(),
                    A("represented pain, treatment, ordeal, and social response"),
                    A(Norms, Ideology, Institutions, RitualPain)),
                Q(HumanRemainsTreatment, "Treatment of human remains",
                    "How should human remains be treated?",
                    "care and symbolic regard expected toward human remains",
                    "instrumental use to protected remembrance",
                    CACultureQuestionLayer.BodyHealthDeath,
                    A("Instrumental use accepted", "Little regard", "Context-dependent", "Respect expected", "Protected remembrance"),
                    A("corpse and skull-display precepts"), Array.Empty<string>(),
                    A(CACulturalPracticeRegistry.HumanButchery,
                        CACulturalPracticeRegistry.CorpseExposure),
                    A("corpse treatment appraisal"), Array.Empty<string>(),
                    A("corpse-use and exposure rules"), Array.Empty<string>(),
                    A("represented human butchery and corpse exposure"),
                    A(Norms, Ideology, Institutions, Mortuary)),

                Q(HumanFleshAcceptance, "Eating human flesh",
                    "How is eating human flesh regarded?",
                    "social acceptability and status of consuming human flesh",
                    "abhorrent to prestigious",
                    CACultureQuestionLayer.FoodSubstances,
                    A("Abhorrent", "Strongly disapproved", "Tolerated in extremity", "Accepted", "Prestigious"),
                    A("cannibalism precepts"), Array.Empty<string>(),
                    A(CACulturalPracticeRegistry.HumanFleshConsumption),
                    A("meal appraisal and social response"), Array.Empty<string>(),
                    A("food and corpse-use rules"), Array.Empty<string>(),
                    A("represented consumption, ingredients, butchery, and public response"),
                    A(Norms, Ideology, Institutions, FoodDisgust)),
                Q(AnimalFoodAcceptance, "Animal food",
                    "How is eating animals regarded?",
                    "acceptability of animal-derived food",
                    "prohibited to central",
                    CACultureQuestionLayer.FoodSubstances,
                    A("Prohibited", "Strongly disapproved", "Accepted", "Preferred", "Central"),
                    A("meat-eating and ranching precepts"), Array.Empty<string>(),
                    A(CACulturalPracticeRegistry.AnimalFoodConsumption,
                        CACulturalPracticeRegistry.AnimalSlaughter),
                    A("meal and slaughter appraisal"), Array.Empty<string>(),
                    A("food and animal-use rules"), Array.Empty<string>(),
                    A("represented slaughter, meat use, non-meat meals, and response"),
                    A(Norms, Ideology, Institutions, FoodDisgust,
                        MoralExpansiveness)),
                Q(FoodAdaptability, "Acceptance of unfamiliar foods",
                    "How readily are unusual or processed foods accepted?",
                    "willingness to treat unfamiliar, insect, fungal, or processed foods as edible",
                    "strong taboo to broad adaptability",
                    CACultureQuestionLayer.FoodSubstances,
                    A("Strong taboo", "Disapproved", "Necessity only", "Accepted", "Broadly adaptable"),
                    A("fungus, insect-meat, and nutrient-paste precepts"),
                    Array.Empty<string>(),
                    A(CACulturalPracticeRegistry.UnfamiliarFoodConsumption),
                    A("food-choice appraisal"), Array.Empty<string>(),
                    A("food provision rules"), Array.Empty<string>(),
                    A("represented meals, scarcity, ingredients, and social response"),
                    A(Norms, Ideology, Institutions, FoodDisgust)),
                Q(RecreationalDrugUse, "Recreational drug use",
                    "How is recreational drug use regarded?",
                    "acceptability of non-medical intoxicant use",
                    "prohibited to expected",
                    CACultureQuestionLayer.FoodSubstances,
                    A("Prohibited", "Strongly disapproved", "Restricted", "Accepted", "Expected"),
                    A("drug-use and alcohol precepts"), Array.Empty<string>(),
                    A(CACulturalPracticeRegistry.DrugUse,
                        CACulturalPracticeRegistry.DrugAdministration),
                    A("drug-use appraisal"), Array.Empty<string>(),
                    A("medical and recreational drug rules"), Array.Empty<string>(),
                    A("represented ingestion, administration, and social response"),
                    A(Norms, Ideology, Institutions, Wvs)),

                Q(AnimalMoralStanding, "Moral standing of animals",
                    "How much moral protection is owed to animals?",
                    "moral regard and protection attributed to animals",
                    "purely instrumental to sacred protection",
                    CACultureQuestionLayer.AnimalsEnvironment,
                    A("Purely instrumental", "Limited regard", "Welfare considered", "Protection expected", "Sacred protection"),
                    A("animal-veneration and slaughter precepts"), Array.Empty<string>(),
                    A(CACulturalPracticeRegistry.AnimalSlaughter,
                        CACulturalPracticeRegistry.InnocentAnimalKilling),
                    A("animal treatment appraisal"), Array.Empty<string>(),
                    A("slaughter and animal-harm rules"), Array.Empty<string>(),
                    A("represented slaughter, innocent animal killing, and response"),
                    A(Norms, Ideology, Institutions, Wvs,
                        MoralExpansiveness)),
                Q(ResourceStewardship, "Land and resource stewardship",
                    "How strongly should extraction protect the surrounding land?",
                    "expected restraint in extracting living and mineral resources",
                    "unrestricted extraction to strict stewardship",
                    CACultureQuestionLayer.AnimalsEnvironment,
                    A("Unrestricted extraction", "Extraction favored", "Balanced use", "Stewardship expected", "Strict protection"),
                    A("tree-cutting and mining precepts"), Array.Empty<string>(),
                    A(CACulturalPracticeRegistry.ResourceExtraction),
                    A("extraction appraisal"), Array.Empty<string>(),
                    A("land-use and extraction rules"), Array.Empty<string>(),
                    A("represented tree cutting and mineral extraction"),
                    A(Norms, Ideology, Institutions, Wvs,
                        EnvironmentalAttitudes)),
                Q(SettlementPermanence, "Permanent settlement",
                    "How strongly is permanent settlement preferred?",
                    "preference for mobile life or enduring settlement",
                    "mobile to permanent",
                    CACultureQuestionLayer.AnimalsEnvironment,
                    A("Mobile life expected", "Mobility preferred", "Context-dependent", "Settlement preferred", "Permanent roots expected"),
                    A("nomadism precepts"), Array.Empty<string>(),
                    A(CACulturalPracticeRegistry.SettlementAbandonment),
                    A("departure and settlement appraisal"), Array.Empty<string>(),
                    A("land tenure and settlement rules"), Array.Empty<string>(),
                    A("represented migration, abandonment, rebuilding, and tenure"),
                    A(Norms, Ideology, Institutions, MobilitySedentism)),
                Q(ComfortExpectation, "Expected material comfort",
                    "How much material comfort is considered proper?",
                    "social expectation of material comfort in daily life",
                    "austere to comfort-centered",
                    CACultureQuestionLayer.DailyLifeTechnology,
                    A("Austere", "Comfort disfavored", "Practical comfort", "Comfort expected", "Comfort-centered"),
                    A("comfort and rough-living precepts"), Array.Empty<string>(),
                    Array.Empty<string>(), A("comfort and housing appraisal"),
                    A("provision expectations"), A("housing and furnishing standards"),
                    Array.Empty<string>(),
                    A("represented housing, furniture, deprivation, and social response"),
                    A(Schwartz, Norms, Ideology, Institutions, Wvs)),
                Q(MachineDelegation, "Delegating work to machines",
                    "How readily should human work be delegated to machines?",
                    "acceptance of autonomous machines as workers and defenders",
                    "human-only work to broad machine delegation",
                    CACultureQuestionLayer.DailyLifeTechnology,
                    A("Human work only", "Machines strongly limited", "Practical use", "Machine work accepted", "Broad delegation"),
                    A("autonomous-weapon and mechanoid-labor precepts"),
                    Array.Empty<string>(), Array.Empty<string>(),
                    A("machine work and defense appraisal"), A("labor and security support"),
                    A("machine access and deployment rules"), A("machine operation knowledge"),
                    A("represented machine labor, autonomous defense, maintenance, and response"),
                    A(Norms, Ideology, Institutions, RobotAcceptance))
            };
        }

        private static CACultureQuestionDef Q(string key, string label,
            string question, string construct, string direction,
            CACultureQuestionLayer layer, string[] anchors,
            string[] ideoligion, string[] subjects, string[] practices,
            string[] behaviors, string[] politics, string[] institutions,
            string[] knowledge, string[] historicalSources,
            string[] provenance)
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
                HistoricalSources = historicalSources,
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

    // This is a projection over production routes, not a second declaration
    // catalog. Each result is derived from a registry or sampler that runtime
    // code actually calls.
    internal static class CACultureQuestionExecutionRoutes
    {
        internal const string NativeDoctrine = "native-doctrine";
        internal const string SocialFact = "social-fact";
        internal const string RepeatedPractice = "repeated-practice";
        internal const string RepresentedState = "represented-state";
        internal const string AutonomousBehavior = "autonomous-behavior";

        internal static IReadOnlyList<string> For(string questionKey)
        {
            return EvidenceInputsFor(questionKey)
                .Concat(ConsumerRoutesFor(questionKey))
                .Distinct(StringComparer.Ordinal).ToArray();
        }

        internal static IReadOnlyList<string> EvidenceInputsFor(
            string questionKey)
        {
            CACultureQuestionDef definition = CACultureQuestionRegistry.Find(
                questionKey);
            if (definition == null)
                return Array.Empty<string>();
            var result = new List<string>();
            if (CAIdeoligionSemanticAdapterRegistry.All.Any(value =>
                    value.QuestionKey == questionKey))
                result.Add(NativeDoctrine);
            if ((definition.SocialSubjectAdapters
                    ?? Array.Empty<string>()).Any(subject =>
                        CASocialSubjectRegistry.Find(subject) != null))
                result.Add(SocialFact);
            if ((definition.PracticeEvidenceAdapters
                    ?? Array.Empty<string>()).Any(practice =>
                {
                    CACulturalPracticeDef represented =
                        CACulturalPracticeRegistry.Find(practice);
                    return represented != null
                        && CACulturalPracticeRegistry
                            .HasExecutableObservationAdapter(represented);
                }))
                result.Add(RepeatedPractice);
            if (CACultureLongitudinalMapComponent.HasDirectQuestionRoute(
                    questionKey))
                result.Add(RepresentedState);
            return result.Distinct(StringComparer.Ordinal).ToArray();
        }

        internal static IReadOnlyList<string> ConsumerRoutesFor(
            string questionKey)
        {
            CACultureQuestionDef definition = CACultureQuestionRegistry.Find(
                questionKey);
            if (definition == null) return Array.Empty<string>();
            var result = new List<string>();
            if (CAQuestionConsumerMap.HasQuestion(questionKey))
                result.Add(AutonomousBehavior);
            return result.Distinct(StringComparer.Ordinal).ToArray();
        }

        internal static string PlayerSummary(string questionKey)
        {
            IReadOnlyList<string> routes = For(questionKey);
            var parts = new List<string>();
            if (routes.Contains(NativeDoctrine))
                parts.Add("Ideoligion doctrine");
            if (routes.Contains(SocialFact))
                parts.Add("known conduct and social response");
            if (routes.Contains(RepeatedPractice))
                parts.Add("repeated local practice");
            if (routes.Contains(RepresentedState))
                parts.Add("represented settlement life");
            if (routes.Contains(AutonomousBehavior))
                parts.Add("relevant autonomous decisions");
            return string.Join(", ", parts);
        }
    }
}
