using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class Program
{
    private const BindingFlags All = BindingFlags.Public
        | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private const string GovernedOrphanQuestionIdentity =
        "culture:settlement:CA-RG-EB596A12:2|localQuestions|"
        + "culture:ridge-covenant|voice.inclusionExpectation";
    private const string GovernedOrphanEvidenceSignature = "952E24D9";

    private sealed record Check(string Name, bool Passed, string Evidence);
    private sealed record NativeDef(string PackageId, string PackageLabel,
        string Kind, string DefName, string Issue);
    private sealed record FamilyAdjudication(string PackageId, string Family,
        int DefinitionCount, int AdaptedDefinitionCount,
        string[] CultureQuestions, string Disposition, string Rationale);
    private sealed record NativeEmissionRoute(string EventDefName,
        string Root, string RelativePath, string MethodName, string RouteKind,
        string[] RequiredTokens, int ExpectedTokenOccurrences = 1,
        string SecondaryRelativePath = null,
        string SecondaryMethodName = null,
        string[] SecondaryRequiredTokens = null,
        int ExpectedSecondaryTokenOccurrences = 0);

    private static readonly List<Check> Checks = new();

    private static int Main(string[] args)
    {
        if (args.Length != 9)
        {
            Console.Error.WriteLine("usage: B16IdeoligionCultureReceipts "
                + "<repo> <managed> <rimworld-data> <assembly> <active> "
                + "<mirror> <more-precepts-root> <more-precepts-commit> "
                + "<decompiled-source-root>");
            return 1;
        }

        string repo = Path.GetFullPath(args[0]);
        string managed = Path.GetFullPath(args[1]);
        string data = Path.GetFullPath(args[2]);
        string dll = Path.GetFullPath(args[3]);
        string active = Path.GetFullPath(args[4]);
        string mirror = Path.GetFullPath(args[5]);
        string morePrecepts = Path.GetFullPath(args[6]);
        string moreCommit = args[7];
        string decompiled = Path.GetFullPath(args[8]);

        LoadManagedAssemblies(managed);
        Assembly gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .Single(item => item.GetName().Name == "Assembly-CSharp");
        Assembly assembly = Assembly.LoadFrom(dll);
        string hash = Convert.ToHexString(SHA256.HashData(
            File.ReadAllBytes(dll)));

        Type questionRegistry = RequiredType(assembly,
            "ColonistAwareness.CACultureQuestionRegistry");
        IList questions = Values(questionRegistry, "All");
        string[] questionKeys = questions.Cast<object>()
            .Select(value => StringField(value, "Key")).ToArray();
        Type executionRoutes = RequiredType(assembly,
            "ColonistAwareness.CACultureQuestionExecutionRoutes");
        MethodInfo allRoutesFor = Method(executionRoutes, "For", 1);
        MethodInfo evidenceInputsFor = Method(executionRoutes,
            "EvidenceInputsFor", 1);
        MethodInfo consumerRoutesFor = Method(executionRoutes,
            "ConsumerRoutesFor", 1);
        Dictionary<string, string[]> evidenceInputs = questionKeys.ToDictionary(
            key => key,
            key => ((IEnumerable)evidenceInputsFor.Invoke(null,
                    new object[] { key })!)
                .Cast<object>().Select(value => value.ToString()!).ToArray(),
            StringComparer.Ordinal);
        Dictionary<string, string[]> consumerRoutes = questionKeys.ToDictionary(
            key => key,
            key => ((IEnumerable)consumerRoutesFor.Invoke(null,
                    new object[] { key })!)
                .Cast<object>().Select(value => value.ToString()!).ToArray(),
            StringComparer.Ordinal);
        Dictionary<string, string[]> allRoutes = questionKeys.ToDictionary(
            key => key,
            key => ((IEnumerable)allRoutesFor.Invoke(null,
                    new object[] { key })!)
                .Cast<object>().Select(value => value.ToString()!).ToArray(),
            StringComparer.Ordinal);
        CheckIt("Culture question registry", questions.Count == 48
                && questionKeys.Distinct(StringComparer.Ordinal).Count() == 48
                && (int)RequiredField(questionRegistry, "CurrentVersion")
                    .GetRawConstantValue()! == 3,
            questions.Count + " unique registry-3 questions");
        CheckIt("Culture decision categories", questions.Cast<object>()
                .Select(value => RequiredField(value.GetType(), "Layer")
                    .GetValue(value)!.ToString()).Distinct().Count() == 12,
            "all 12 player decision categories represented");
        CheckIt("Question contracts", questions.Cast<object>().All(value =>
                ((Array)RequiredField(value.GetType(), "Anchors")
                    .GetValue(value)!).Length == 5
                && allRoutes[StringField(value, "Key")].Length > 0
                && ((Array)RequiredField(value.GetType(),
                    "HistoricalSources").GetValue(value)!).Length > 0
                && ((Array)RequiredField(value.GetType(),
                    "ResearchProvenance").GetValue(value)!).Length > 0),
            "48/48 have five anchors, an executable evidence, feedback, or behavior route, history, and research provenance");
        var b16Research = new Dictionary<string,
            (string ResearchToken, string CorpusKey)>(StringComparer.Ordinal)
        {
            ["relationships.sexualConduct"] =
                ("World Values Survey wave 7 questionnaire", "WVS7"),
            ["relationships.marriageNaming"] =
                ("MarriageName", "MarriageName"),
            ["relationships.childhoodProtection"] =
                ("ChildLabor", "ChildLabor"),
            ["status.ageStanding"] = ("AgeNorms", "AgeNorms"),
            ["groups.doctrinalPluralism"] =
                ("World Values Survey wave 7 questionnaire", "WVS7"),
            ["groups.xenotypeHierarchy"] =
                ("SocialDominance", "SocialDominance"),
            ["labor.workExpectation"] =
                ("World Values Survey wave 7 questionnaire", "WVS7"),
            ["property.predatoryAcquisition"] =
                ("descriptive and injunctive norm distinction", "Norms"),
            ["war.violenceAcceptance"] =
                ("World Values Survey wave 7 questionnaire", "WVS7"),
            ["body.maleExposure"] = ("Modesty", "Modesty"),
            ["body.femaleExposure"] = ("Modesty", "Modesty"),
            ["body.alteration"] =
                ("BodyModification", "BodyModification"),
            ["body.integrity"] =
                ("BodilyIntegrity", "BodilyIntegrity"),
            ["body.painMeaning"] = ("RitualPain", "RitualPain"),
            ["death.humanRemainsTreatment"] = ("Mortuary", "Mortuary"),
            ["food.humanFleshAcceptance"] =
                ("FoodDisgust", "FoodDisgust"),
            ["food.animalFoodAcceptance"] =
                ("MoralExpansiveness", "MoralExpansiveness"),
            ["food.adaptability"] = ("FoodDisgust", "FoodDisgust"),
            ["substances.recreationalUse"] =
                ("World Values Survey wave 7 questionnaire", "WVS7"),
            ["animals.moralStanding"] =
                ("MoralExpansiveness", "MoralExpansiveness"),
            ["environment.resourceStewardship"] =
                ("EnvironmentalAttitudes", "EnvironmentalAttitudes"),
            ["settlement.permanence"] =
                ("MobilitySedentism", "MobilitySedentism"),
            ["daily.comfortExpectation"] =
                ("World Values Survey wave 7 questionnaire", "WVS7"),
            ["technology.machineDelegation"] =
                ("RobotAcceptance", "RobotAcceptance")
        };
        string researchCorpusText = File.ReadAllText(Path.Combine(repo,
            "CULTURE_RESEARCH_CORPUS.md"));
        Dictionary<string, object> questionByKey = questions.Cast<object>()
            .ToDictionary(value => StringField(value, "Key"),
                StringComparer.Ordinal);
        string[] researchFailures = b16Research.Where(pair =>
            !questionByKey.TryGetValue(pair.Key, out object definition)
            || !((IEnumerable)RequiredField(definition.GetType(),
                    "ResearchProvenance").GetValue(definition)!)
                .Cast<object>().Select(value => value.ToString())
                .Contains(pair.Value.ResearchToken, StringComparer.Ordinal)
            || !researchCorpusText.Contains("`" + pair.Key + "`",
                StringComparison.Ordinal)
            || !researchCorpusText.Contains("`" + pair.Value.CorpusKey + "`",
                StringComparison.Ordinal)).Select(pair => pair.Key).ToArray();
        CheckIt("B16 research and mechanic decomposition",
            b16Research.Count == 24 && researchFailures.Length == 0,
            researchFailures.Length == 0
                ? "24/24 added questions bind a named source family, a playable mechanical seam, an ownership decision, and a downstream consumer in the governed corpus"
                : string.Join(", ", researchFailures));
        CheckIt("Executable question routes",
            allRoutes.All(pair => pair.Value.Length > 0),
            string.Join("; ", allRoutes.GroupBy(pair => string.Join("+",
                    pair.Value.OrderBy(value => value, StringComparer.Ordinal)))
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.Key + "=" + group.Count())));
        int behaviorConsumers = consumerRoutes.Count(pair =>
            pair.Value.Contains("autonomous-behavior", StringComparer.Ordinal));
        CheckIt("Behavior consumer coverage",
            behaviorConsumers > 0 && consumerRoutes.Values.SelectMany(
                value => value).All(value => value == "autonomous-behavior"),
            behaviorConsumers + "/48 questions currently affect autonomous behavior; remaining questions are observable appraisal or feedback state, not falsely counted as behavior consumers");
        CheckIt("Doctrine is evidence, not a consumer",
            evidenceInputs.Values.SelectMany(value => value).Contains(
                "native-doctrine", StringComparer.Ordinal)
            && consumerRoutes.Values.SelectMany(value => value).All(value =>
                value != "native-doctrine"),
            evidenceInputs.Count(pair => pair.Value.Contains(
                "native-doctrine", StringComparer.Ordinal))
                + " questions accept exact doctrine pressure without counting it as downstream execution");

        Type distributionKernel = RequiredType(assembly,
            "ColonistAwareness.CACultureDistributionKernel");
        MethodInfo newQuestion = Method(distributionKernel, "NewQuestion", 2);
        MethodInfo materializeDistribution = Method(distributionKernel,
            "Materialize", 6);
        string[] perturbationFailures = questions.Cast<object>().Select(
            definition =>
            {
                object low = newQuestion.Invoke(null,
                    new[] { definition, "*" })!;
                object high = newQuestion.Invoke(null,
                    new[] { definition, "*" })!;
                RequiredField(low.GetType(), "mean").SetValue(low, -0.80f);
                RequiredField(high.GetType(), "mean").SetValue(high, 0.80f);
                object[] seed = { "b16-world", "b16-culture", "all",
                    "pawn-1", 1 };
                float lowValue = (float)materializeDistribution.Invoke(null,
                    new object[] { low, seed[0], seed[1], seed[2], seed[3],
                        seed[4] })!;
                float highValue = (float)materializeDistribution.Invoke(null,
                    new object[] { high, seed[0], seed[1], seed[2], seed[3],
                        seed[4] })!;
                return highValue > lowValue + 0.20f ? null
                    : StringField(definition, "Key") + "="
                        + lowValue.ToString("0.000", CultureInfo.InvariantCulture)
                        + "/" + highValue.ToString("0.000",
                            CultureInfo.InvariantCulture);
            }).Where(value => value != null).ToArray()!;
        CheckIt("Question perturbation uses production materialization",
            perturbationFailures.Length == 0,
            perturbationFailures.Length == 0
                ? "48/48 fixed-seed question positions change through CACultureDistributionKernel"
                : string.Join(", ", perturbationFailures));

        Type culturePresets = RequiredType(assembly,
            "ColonistAwareness.CACulturePresetLibrary");
        IList presets = Values(culturePresets, "All");
        object presetFailure = Method(culturePresets, "ValidationFailure", 0)
            .Invoke(null, Array.Empty<object>());
        bool substantiveB16Presets = presets.Cast<object>().All(value =>
        {
            IList rows = (IList)RequiredField(value.GetType(), "Values")
                .GetValue(value)!;
            return rows.Cast<object>().Skip(24).Any(row => Math.Abs(
                (float)RequiredField(row.GetType(), "Mean").GetValue(row)!)
                    >= 0.10f);
        });
        bool sourcedPresets = presets.Cast<object>().All(value =>
            !string.IsNullOrWhiteSpace(StringField(value, "Sources"))
            && (!StringField(value, "CatalogGroup").Equals(
                    "Historical cultures", StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(StringField(value,
                        "ReferenceRegion"))
                    && !string.IsNullOrWhiteSpace(StringField(value,
                        "ApproximatePeriod")))));
        bool honestValueEvidence = presets.Cast<object>().All(value =>
            ((IEnumerable)RequiredField(value.GetType(), "Values")
                .GetValue(value)!).Cast<object>().All(row =>
                    !string.IsNullOrWhiteSpace(StringField(row, "Evidence"))
                    && ((float)RequiredField(row.GetType(),
                        "SourceConfidence").GetValue(row)!) <= 0.35f));
        object Preset(string key) => presets.Cast<object>().Single(value =>
            StringField(value, "Key") == key);
        float Mean(object preset, string question) => ((IList)RequiredField(
                preset.GetType(), "Values").GetValue(preset)!).Cast<object>()
            .Where(value => StringField(value, "QuestionKey") == question)
            .Select(value => (float)RequiredField(value.GetType(), "Mean")
                .GetValue(value)!).Single();
        object weimar = Preset("germany-weimar-republic");
        object nazi = Preset("germany-national-socialist-dictatorship");
        object confederate = Preset(
            "confederate-slaveholding-dominant-culture");
        object freedpeople = Preset("freedpeople-emancipation-communities");
        object tokugawa = Preset("japan-late-tokugawa");
        object meiji = Preset("japan-meiji-transformation");
        object postwar = Preset("united-states-postwar-mid-century");
        object contemporary = Preset("united-states-contemporary");
        bool intendedContrasts =
            Mean(nazi, "groups.doctrinalPluralism")
                < Mean(weimar, "groups.doctrinalPluralism")
            && Mean(nazi, "groups.xenotypeHierarchy")
                > Mean(weimar, "groups.xenotypeHierarchy")
            && Mean(confederate, "groups.xenotypeHierarchy")
                > Mean(freedpeople, "groups.xenotypeHierarchy")
            && Mean(confederate, "property.predatoryAcquisition")
                > Mean(freedpeople, "property.predatoryAcquisition")
            && Mean(meiji, "technology.machineDelegation")
                > Mean(tokugawa, "technology.machineDelegation")
            && Mean(contemporary, "relationships.sameSexAcceptance")
                > Mean(postwar, "relationships.sameSexAcceptance");
        CheckIt("Culture preset completion", presets.Count == 22
                && presetFailure == null && presets.Cast<object>().All(value =>
                    ((ICollection)RequiredField(value.GetType(), "Values")
                        .GetValue(value)!).Count == 48)
                && substantiveB16Presets
                && sourcedPresets && honestValueEvidence
                && intendedContrasts,
            presetFailure == null && substantiveB16Presets && sourcedPresets
                    && honestValueEvidence && intendedContrasts
                ? "22 complete in-range presets carry contextual sources, explicitly low-confidence per-question estimates, and selected intended contrasts; no fitted historical measurement is claimed"
                : "validation=" + (presetFailure ?? "none")
                    + "; substantive=" + substantiveB16Presets
                    + "; sources=" + sourcedPresets + "; honest evidence="
                    + honestValueEvidence + "; contrasts=" + intendedContrasts);

        VerifyDoctrinePressure(assembly);
        VerifySubjectMappings(assembly);

        Type cultureType = RequiredType(assembly,
            "ColonistAwareness.CACulture");
        object legacyCulture = Activator.CreateInstance(cultureType)!;
        RequiredField(cultureType, "id").SetValue(legacyCulture,
            "culture.b16-registry-migration-receipt");
        Method(culturePresets, "Apply", 3).Invoke(null, new[]
        {
            legacyCulture, presets[0], "b16-registry-migration-receipt"
        });
        IList legacyRows = (IList)RequiredField(cultureType,
            "inheritedQuestions").GetValue(legacyCulture)!;
        while (legacyRows.Count > 24) legacyRows.RemoveAt(legacyRows.Count - 1);
        object nonNormalizedLegacyRow = legacyRows[0]!;
        RequiredField(nonNormalizedLegacyRow.GetType(), "spreadOverride")
            .SetValue(nonNormalizedLegacyRow, false);
        RequiredField(nonNormalizedLegacyRow.GetType(), "spread")
            .SetValue(nonNormalizedLegacyRow, 0.47f);
        RequiredField(cultureType, "schemaVersion").SetValue(legacyCulture, 10);
        RequiredField(cultureType, "questionRegistryVersion").SetValue(
            legacyCulture, 2);
        string[] legacySignatures = legacyRows.Cast<object>()
            .Select(QuestionSignature).ToArray();
        Type cultureModel = RequiredType(assembly,
            "ColonistAwareness.CACultureModel");
        object[] migrationCall = { legacyCulture, null, null };
        bool registryMigrated = (bool)Method(cultureModel,
            "TryUpgradeQuestionRegistry", 3).Invoke(null, migrationCall)!;
        object migratedCulture = migrationCall[1];
        IList migratedRows = migratedCulture == null ? null
            : (IList)RequiredField(cultureType, "inheritedQuestions")
                .GetValue(migratedCulture)!;
        bool legacyRowsPreserved = migratedRows != null
            && legacySignatures.SequenceEqual(migratedRows.Cast<object>()
                .Take(24).Select(QuestionSignature));
        CheckIt("Registry-2 Culture migration", registryMigrated
                && migrationCall[2] == null && migratedRows?.Count == 48
                && (int)RequiredField(cultureType,
                    "questionRegistryVersion").GetValue(migratedCulture)! == 3
                && (int)RequiredField(cultureType,
                    "schemaVersion").GetValue(migratedCulture)! == 11
                && legacyRowsPreserved,
            registryMigrated && migrationCall[2] == null
                ? "rows=" + (migratedRows?.Count ?? -1)
                    + "; registry=" + (migratedCulture == null ? -1
                        : (int)RequiredField(cultureType,
                            "questionRegistryVersion").GetValue(
                                migratedCulture)!)
                    + "; legacy rows exact=" + legacyRowsPreserved
                : "migration rejected: " + (migrationCall[2]
                    ?? "no failure supplied"));

        object incompleteCulture = Method(cultureType, "Copy", 0)
            .Invoke(legacyCulture, Array.Empty<object>())!;
        RequiredField(cultureType, "schemaVersion").SetValue(
            incompleteCulture, 10);
        RequiredField(cultureType, "questionRegistryVersion").SetValue(
            incompleteCulture, 2);
        IList incompleteRows = (IList)RequiredField(cultureType,
            "inheritedQuestions").GetValue(incompleteCulture)!;
        incompleteRows.RemoveAt(incompleteRows.Count - 1);
        object[] incompleteCall = { incompleteCulture, null, null };
        bool incompleteMigrated = (bool)Method(cultureModel,
            "TryUpgradeQuestionRegistry", 3).Invoke(null, incompleteCall)!;
        CheckIt("Incomplete registry-2 Culture rejection",
            !incompleteMigrated && incompleteCall[1] == null
                && incompleteCall[2] is string incompleteFailure
                && incompleteFailure.Contains("missing legacy questions",
                    StringComparison.Ordinal),
            "a predecessor scope must contain every registry-2 question; migration appends only the 24 B16 questions and never neutral-fills missing historical state");

        VerifySavedProfileMigration(assembly, gameAssembly, legacyCulture,
            cultureType);
        VerifyLegacyCultureMigration(assembly, gameAssembly, cultureType,
            cultureModel);
        VerifyDescriptiveEvidenceBoundary(assembly, culturePresets, presets,
            cultureType);

        Type campaignCatalog = RequiredType(assembly,
            "ColonistAwareness.CACampaignSchemaCatalog");
        IList campaignSchemas = (IList)RequiredField(campaignCatalog, "All")
            .GetValue(null)!;
        var schemaVersions = campaignSchemas.Cast<object>().ToDictionary(
            value => StringProperty(value, "Key"),
            value => (int)RequiredProperty(value.GetType(), "CurrentVersion")
                .GetValue(value)!, StringComparer.Ordinal);
        CheckIt("Adjacent campaign schema declarations",
            (int)RequiredField(campaignCatalog, "CurrentCatalogVersion")
                .GetRawConstantValue()! == 5
            && schemaVersions["world.faction-state"] == 4
            && schemaVersions["world.player-founding"] == 4
            && schemaVersions["world.regional"] == 4
            && schemaVersions["map.culture-longitudinal"] == 3
            && schemaVersions["model.regional-plan"] == 15,
            "catalog 5 declares every live Culture owner and regional plan schema 15");
        Type compatibilityKernel = RequiredType(assembly,
            "ColonistAwareness.CACampaignCompatibilityKernel");
        MethodInfo evaluateCatalogSchema = Method(compatibilityKernel,
            "EvaluateCatalogSchema", 3);
        bool CanLoadCatalog(int catalog, string key, int version) =>
            (bool)RequiredProperty(evaluateCatalogSchema.Invoke(null,
                new object[] { catalog, key, version })!.GetType(), "CanLoad")
                .GetValue(evaluateCatalogSchema.Invoke(null,
                    new object[] { catalog, key, version }))!;
        CheckIt("Catalog generation owns schema generation",
            CanLoadCatalog(5, "world.faction-state", 4)
                && CanLoadCatalog(5, "model.culture", 11)
                && !CanLoadCatalog(5, "world.faction-state", 3)
                && !CanLoadCatalog(5, "model.culture", 10)
                && CanLoadCatalog(4, "world.faction-state", 3)
                && CanLoadCatalog(4, "model.culture", 10),
            "catalog 5 accepts only owner-4/Culture-11 state; catalog 4 remains the supported owner-3/Culture-10 migration input");
        VerifyStreamingPreflight(assembly, questionKeys);

        Dictionary<string, string> packageRoots = new(
            StringComparer.OrdinalIgnoreCase)
        {
            ["ludeon.rimworld"] = Path.Combine(data, "Core"),
            ["ludeon.rimworld.royalty"] = Path.Combine(data, "Royalty"),
            ["ludeon.rimworld.ideology"] = Path.Combine(data, "Ideology"),
            ["ludeon.rimworld.biotech"] = Path.Combine(data, "Biotech"),
            ["ludeon.rimworld.odyssey"] = Path.Combine(data, "Odyssey"),
            ["llunak.MorePrecepts"] = Path.Combine(morePrecepts, "Defs")
        };
        var corpus = new List<NativeDef>();
        foreach ((string package, string root) in packageRoots)
            corpus.AddRange(ReadDefs(root, package,
                package == "llunak.MorePrecepts" ? "More Precepts" :
                package.Split('.').Last()));

        string packageId = ReadPackageId(morePrecepts);
        string actualMoreCommit = GitHead(morePrecepts);
        CheckIt("More Precepts support identity",
            packageId.Equals("llunak.MorePrecepts",
                    StringComparison.OrdinalIgnoreCase)
                && actualMoreCommit.Equals(moreCommit,
                    StringComparison.OrdinalIgnoreCase),
            "package=" + packageId + "; source commit=" + actualMoreCommit);

        Type semanticRegistry = RequiredType(assembly,
            "ColonistAwareness.CAIdeoligionSemanticAdapterRegistry");
        IList semantic = Values(semanticRegistry, "All");
        object semanticFailure = Method(semanticRegistry,
            "ValidationFailure", 0).Invoke(null, Array.Empty<object>());
        string[] semanticIdentities = semantic.Cast<object>().Select(value =>
            SemanticMappingIdentity(value)).ToArray();
        CheckIt("Exact Ideoligion semantic registry", semanticFailure == null
                && semantic.Count > 100
                && semanticIdentities.Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count() == semantic.Count,
            semantic.Count + " explicit package, kind, def, and Culture mappings");
        var mappedDoctrineQuestions = semantic.Cast<object>().Select(value =>
                StringField(value, "QuestionKey"))
            .ToHashSet(StringComparer.Ordinal);
        var advertisedDoctrineQuestions = questions.Cast<object>()
            .Where(value => ((string[])RequiredField(value.GetType(),
                    "IdeoligionAdapters").GetValue(value)!).Length > 0)
            .Select(value => StringField(value, "Key"))
            .ToHashSet(StringComparer.Ordinal);
        CheckIt("Question doctrine metadata matches the exact registry",
            advertisedDoctrineQuestions.SetEquals(mappedDoctrineQuestions),
            advertisedDoctrineQuestions.SetEquals(mappedDoctrineQuestions)
                ? mappedDoctrineQuestions.Count
                    + " Culture questions advertise exact doctrine mappings"
                : "metadata-only=" + string.Join(",",
                    advertisedDoctrineQuestions.Except(
                        mappedDoctrineQuestions, StringComparer.Ordinal))
                    + "; registry-only=" + string.Join(",",
                        mappedDoctrineQuestions.Except(
                            advertisedDoctrineQuestions,
                            StringComparer.Ordinal)));
        string[] missingSemantic = semantic.Cast<object>().Where(value =>
        {
            string package = StringField(value, "PackageId");
            string kind = RequiredField(value.GetType(), "Kind").GetValue(value)!
                .ToString();
            string defName = StringField(value, "DefName");
            return !corpus.Any(def => def.PackageId.Equals(package,
                    StringComparison.OrdinalIgnoreCase)
                && def.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase)
                && def.DefName == defName);
        }).Select(SemanticIdentity).ToArray();
        CheckIt("Semantic definitions exist", missingSemantic.Length == 0,
            missingSemantic.Length == 0
                ? "every supported adapter resolves in its authoritative Def corpus"
                : string.Join(", ", missingSemantic));

        Type kindType = RequiredType(assembly,
            "ColonistAwareness.CAIdeoligionSemanticDefKind");
        MethodInfo semanticFind = Method(semanticRegistry, "Find", 3);
        object unknown = semanticFind.Invoke(null, new[]
        {
            "unknown.mod", Enum.Parse(kindType, "Precept"),
            "Cannibalism_Preferred"
        });
        CheckIt("Unknown mod doctrine fails closed",
            unknown is ICollection collection && collection.Count == 0,
            "an unknown package cannot inherit a known defName's semantics");
        string[] targetSpecificNativeOnly =
        {
            "ludeon.rimworld.ideology|Precept|AnimalVenerated",
            "ludeon.rimworld.odyssey|Precept|Fishing_Disapproved",
            "ludeon.rimworld.odyssey|Precept|Fishing_Prohibited",
            "ludeon.rimworld.odyssey|Precept|Fishing_Sacred"
        };
        CheckIt("Target-specific doctrine remains native-only",
            !semantic.Cast<object>().Any(value => targetSpecificNativeOnly
                .Contains(SemanticIdentity(value),
                    StringComparer.OrdinalIgnoreCase)),
            "animal-veneration and fishing prescriptions do not masquerade as a global animal moral-standing appraisal");
        VerifySourceLabelResolution(semanticRegistry, gameAssembly);

        string ideoligionSource = File.ReadAllText(Path.Combine(repo, "Source",
            "CulturalCognitionIdeoligionModule.cs"));
        CheckIt("No semantic name guessing",
            !ideoligionSource.Contains("DefName.StartsWith",
                StringComparison.Ordinal)
            && !ideoligionSource.Contains("def.label",
                StringComparison.OrdinalIgnoreCase)
            && ideoligionSource.Contains("exact package-plus-def mappings",
                StringComparison.OrdinalIgnoreCase),
            "production matches exact package and Def identity only");

        Type eventRegistry = RequiredType(assembly,
            "ColonistAwareness.CANativeCultureEventAdapterRegistry");
        IList events = Values(eventRegistry, "All");
        object eventFailure = Method(eventRegistry, "ValidationFailure", 0)
            .Invoke(null, Array.Empty<object>());
        string[] missingEvents = events.Cast<object>().Where(value =>
        {
            string package = StringField(value, "PackageId");
            string name = StringField(value, "EventDefName");
            return !corpus.Any(def => def.PackageId.Equals(package,
                    StringComparison.OrdinalIgnoreCase)
                && def.Kind == "HistoryEvent" && def.DefName == name);
        }).Select(value => StringField(value, "PackageId") + "|"
            + StringField(value, "EventDefName")).ToArray();
        CheckIt("Exact native-practice registry", eventFailure == null
                && events.Count > 0 && missingEvents.Length == 0,
            missingEvents.Length == 0
                ? events.Count + " exact native events resolve to represented practices"
                : string.Join(", ", missingEvents));
        MethodInfo eventFind = eventRegistry.GetMethods(All).Single(method =>
            method.Name == "Find" && method.GetParameters().Length == 2);
        CheckIt("Unknown native event fails closed",
            eventFind.Invoke(null, new object[] { "unknown.mod", "AteHumanMeat" })
                == null, "unknown events stay native and uninterpreted");

        Type practices = RequiredType(assembly,
            "ColonistAwareness.CACulturalPracticeRegistry");
        IList practiceDefs = Values(practices, "All");
        string[] productionFailures = practiceDefs.Cast<object>().Select(value =>
        {
            object[] call = { null };
            bool okay = (bool)Method(value.GetType(), "IsProduction", 1)
                .Invoke(value, call)!;
            return okay ? null : StringField(value, "Key") + ": " + call[0];
        }).Where(value => value != null).ToArray()!;
        CheckIt("Practice contracts", productionFailures.Length == 0,
            productionFailures.Length == 0
                ? practiceDefs.Count + " production practice definitions validate"
                : string.Join("; ", productionFailures));

        string humanFlesh = "food.humanFleshAcceptance";
        string[] cannibalDoctrine = semantic.Cast<object>().Where(value =>
            StringField(value, "QuestionKey") == humanFlesh).Select(value =>
            StringField(value, "DefName")).ToArray();
        string[] cannibalPractice = events.Cast<object>().Where(value =>
            StringField(value, "PracticeKey") is
                "ca.practice.human_flesh_consumption" or
                "ca.practice.human_butchery").Select(value =>
            StringField(value, "EventDefName")).ToArray();
        CheckIt("Cannibalism ownership separation",
            cannibalDoctrine.Contains("Cannibalism_Preferred")
                && cannibalPractice.Contains("AteHumanMeat")
                && cannibalPractice.Contains("ButcheredHuman")
                && questionKeys.Contains(humanFlesh),
            "native doctrine, Culture appraisal, and actual practice remain three facts");

        string eventSource = File.ReadAllText(Path.Combine(repo, "Source",
            "CultureNativePracticeModule.cs"));
        string historyPatch = eventSource.Substring(eventSource.IndexOf(
            "class CANativeCultureHistoryEventPatch", StringComparison.Ordinal));
        historyPatch = historyPatch.Substring(0, historyPatch.IndexOf(
            "class CANativeCultureRaidActPatch", StringComparison.Ordinal));
        CheckIt("Native executor remains authoritative",
            historyPatch.Contains("[HarmonyPostfix]", StringComparison.Ordinal)
                && !historyPatch.Contains("[HarmonyPrefix]",
                    StringComparison.Ordinal),
            "CA observes supported HistoryEvents after RimWorld executes them; non-blocking prefixes only scope audited native acts");
        VerifyNativeSourceRoutes(events, repo, decompiled, morePrecepts,
            data);

        VerifyNativeOccurrences(assembly, gameAssembly);
        VerifyFixture(active, mirror, repo, questionKeys, assembly,
            gameAssembly);
        VerifyLiveOwnerMigrations(repo, assembly, gameAssembly);
        VerifyOwnershipDocs(repo);

        List<FamilyAdjudication> familyAudit = AuditFamilies(corpus, semantic);
        FamilyAdjudication[] unadjudicated = familyAudit.Where(value =>
            value.Disposition == "Unadjudicated").ToArray();
        Dictionary<string, NativeFamilyDisposition> exactNativeDefinitions =
            NativeDefinitionAdjudications();
        var corpusIdentities = new HashSet<string>(corpus.Where(value =>
                value.Kind is "Precept" or "Meme").Select(
                NativeDefinitionIdentity), StringComparer.OrdinalIgnoreCase);
        string[] unusedExactDispositions = exactNativeDefinitions.Keys.Where(
                value => !corpusIdentities.Contains(value))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        CheckIt("Playable ontology definition adjudication",
            unadjudicated.Length == 0 && unusedExactDispositions.Length == 0,
            unadjudicated.Length == 0 && unusedExactDispositions.Length == 0
                ? familyAudit.Count
                    + " doctrine families contain no unresolved exact definitions; "
                    + exactNativeDefinitions.Count
                    + " native-only definitions are explicitly adjudicated"
                : "unadjudicated=" + string.Join(", ",
                    unadjudicated.Select(value => value.PackageId + "|"
                        + value.Family)) + "; unused exact dispositions="
                    + string.Join(", ", unusedExactDispositions));

        Dictionary<string, NativeFamilyDisposition> eventAdjudications =
            NativeEventAdjudications();
        var corpusEventIdentities = new HashSet<string>(corpus.Where(value =>
                value.Kind == "HistoryEvent").Select(NativeDefinitionIdentity),
            StringComparer.OrdinalIgnoreCase);
        var mappedEventIdentities = new HashSet<string>(events.Cast<object>()
            .Select(EventIdentity), StringComparer.OrdinalIgnoreCase);
        string[] unresolvedEvents = corpusEventIdentities.Where(value =>
                !mappedEventIdentities.Contains(value)
                && !eventAdjudications.ContainsKey(value))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string[] staleEventAdjudications = eventAdjudications.Keys.Where(
                value => !corpusEventIdentities.Contains(value)
                    || mappedEventIdentities.Contains(value))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        CheckIt("Playable native-event adjudication",
            unresolvedEvents.Length == 0
                && staleEventAdjudications.Length == 0,
            unresolvedEvents.Length == 0
                && staleEventAdjudications.Length == 0
                ? corpusEventIdentities.Count + " loaded native events: "
                    + mappedEventIdentities.Count
                    + " exact Culture-practice adapters and "
                    + eventAdjudications.Count
                    + " explicit native-only dispositions"
                : "unresolved=" + string.Join(", ", unresolvedEvents)
                    + "; stale-or-mapped dispositions="
                    + string.Join(", ", staleEventAdjudications));

        string receipts = Path.Combine(repo, "Receipts", "B16");
        Directory.CreateDirectory(receipts);
        WriteAcceptance(Path.Combine(receipts,
            "B16_ACCEPTANCE_RECEIPT.md"), dll, hash, moreCommit);
        WriteCensus(Path.Combine(receipts,
            "B16_PLAYABLE_ONTOLOGY_CENSUS.md"), corpus, semantic, events,
            familyAudit, eventAdjudications, moreCommit);

        int passed = Checks.Count(value => value.Passed);
        Console.WriteLine("B16 acceptance: " + passed + "/" + Checks.Count);
        foreach (Check check in Checks.Where(value => !value.Passed))
            Console.Error.WriteLine("FAIL " + check.Name + ": "
                + check.Evidence);
        return passed == Checks.Count ? 0 : 2;
    }

    private static void VerifyDoctrinePressure(Assembly assembly)
    {
        Type kernel = RequiredType(assembly,
            "ColonistAwareness.CAIdeoligionPressureKernel");
        object opposed = Method(kernel, "Aggregate", 1).Invoke(null,
            new object[] { new[] { -0.8f, 0.8f } })!;
        object aligned = Method(kernel, "Aggregate", 1).Invoke(null,
            new object[] { new[] { 0.7f, 0.9f } })!;
        float center = FloatField(opposed, "Center");
        float intensity = FloatField(opposed, "Intensity");
        float conflict = FloatField(opposed, "Conflict");
        bool profile = Math.Abs(center) < 0.001f
            && Math.Abs(intensity - 0.8f) < 0.001f
            && Math.Abs(conflict - 0.8f) < 0.001f
            && FloatField(aligned, "Center") > 0.79f
            && FloatField(aligned, "Conflict") < 0.11f;

        Type registry = RequiredType(assembly,
            "ColonistAwareness.CAIdeoligionSemanticAdapterRegistry");
        Type pairKind = RequiredType(assembly,
            "ColonistAwareness.CAIdeoligionPairAggregation");
        MethodInfo combine = Method(registry, "CombinePair", 3);
        float plurality = (float)combine.Invoke(null, new object[]
        {
            Enum.Parse(pairKind, "Average"), 0.72f, -0.70f
        })!;
        float gender = (float)combine.Invoke(null, new object[]
        {
            Enum.Parse(pairKind, "FemaleMinusMale"), 0.70f, 0f
        })!;
        CheckIt("Doctrine pressure preserves disagreement", profile
                && Math.Abs(plurality - 0.01f) < 0.001f
                && Math.Abs(gender + 0.70f) < 0.001f,
            "center, intensity, conflict, spouse plurality, and sex-asymmetric spouse pressure remain distinct");
    }

    private static void VerifySubjectMappings(Assembly assembly)
    {
        Type registry = RequiredType(assembly,
            "ColonistAwareness.CACultureQuestionRegistry");
        MethodInfo adaptersFor = Method(registry,
            "AdaptersForSocialSubject", 1);
        Dictionary<string, (string Question, int Direction)[]> Expected(
            string subject) => new()
            {
                [subject] = ((IEnumerable)adaptersFor.Invoke(null,
                        new object[] { subject })!).Cast<object>()
                    .Select(value => (StringField(value, "QuestionKey"),
                        (int)RequiredField(value.GetType(), "Direction")
                            .GetValue(value)!)).ToArray()
            };
        (string Question, int Direction)[] execution = Expected(
            "ca.practice.execution")["ca.practice.execution"];
        (string Question, int Direction)[] slaughter = Expected(
            "ca.practice.animal_slaughter")
            ["ca.practice.animal_slaughter"];
        (string Question, int Direction)[] research = Expected(
            "ca.practice.organized_research")
            ["ca.practice.organized_research"];
        int unknown = ((IEnumerable)adaptersFor.Invoke(null,
            new object[] { "ca.practice.unknown" })!).Cast<object>().Count();
        bool Exact((string Question, int Direction)[] actual,
            params (string Question, int Direction)[] expected) => actual
            .OrderBy(value => value.Question, StringComparer.Ordinal)
            .ThenBy(value => value.Direction)
            .SequenceEqual(expected.OrderBy(value => value.Question,
                    StringComparer.Ordinal).ThenBy(value => value.Direction));
        CheckIt("Many-to-many signed subject mappings",
            Exact(execution,
                ("war.captiveProtection", -1),
                ("war.punishmentSeverity", 1))
                && Exact(slaughter,
                    ("animals.moralStanding", -1),
                    ("food.animalFoodAcceptance", 1))
                && Exact(research,
                    ("knowledge.access", 1),
                    ("knowledge.noveltyAcceptance", 1),
                    ("labor.workExpectation", 1))
                && unknown == 0,
            "execution, slaughter, and research reach every declared question; unknown subjects reach none");
    }

    private static void VerifySavedProfileMigration(Assembly assembly,
        Assembly gameAssembly, object legacyCulture, Type cultureType)
    {
        Type settingsType = RequiredType(assembly,
            "ColonistAwareness.AwarenessSettings");
        Type cultureProfileType = RequiredType(assembly,
            "ColonistAwareness.CAUserCultureProfile");
        Type societyProfileType = RequiredType(assembly,
            "ColonistAwareness.CAUserSocietyProfile");
        object settings = Activator.CreateInstance(settingsType)!;
        object cultureProfile = Activator.CreateInstance(cultureProfileType)!;
        object societyProfile = Activator.CreateInstance(societyProfileType)!;
        RequiredField(cultureProfileType, "key").SetValue(cultureProfile,
            "b16-culture-profile");
        RequiredField(cultureProfileType, "displayName").SetValue(
            cultureProfile, "B16 Culture Profile");
        RequiredField(cultureProfileType, "values").SetValue(cultureProfile,
            legacyCulture);
        RequiredField(societyProfileType, "key").SetValue(societyProfile,
            "b16-society-profile");
        RequiredField(societyProfileType, "displayName").SetValue(
            societyProfile, "B16 Society Profile");
        RequiredField(societyProfileType, "cultureValues").SetValue(
            societyProfile, legacyCulture);
        IList cultureProfiles = (IList)RequiredField(settingsType,
            "cultureProfiles").GetValue(settings)!;
        IList societyProfiles = (IList)RequiredField(settingsType,
            "societyProfiles").GetValue(settings)!;
        cultureProfiles.Add(cultureProfile);
        societyProfiles.Add(societyProfile);
        Type library = RequiredType(assembly,
            "ColonistAwareness.CAAuthoringProfileLibrary");
        Method(library, "Normalize", 1).Invoke(null, new[] { settings });
        object readback = ScribeRoundTrip(gameAssembly, settingsType, settings,
            "caB16SettingsReceipt");
        cultureProfiles = (IList)RequiredField(settingsType,
            "cultureProfiles").GetValue(readback)!;
        societyProfiles = (IList)RequiredField(settingsType,
            "societyProfiles").GetValue(readback)!;

        bool Current(object value)
        {
            IList rows = (IList)RequiredField(cultureType,
                "inheritedQuestions").GetValue(value)!;
            return (int)RequiredField(cultureType, "schemaVersion")
                    .GetValue(value)! == 11
                && (int)RequiredField(cultureType, "questionRegistryVersion")
                    .GetValue(value)! == 3
                && rows.Count == 48;
        }
        object savedCulture = cultureProfiles.Count == 1
            ? RequiredField(cultureProfileType, "values")
                .GetValue(cultureProfiles[0]) : null;
        object savedSocietyCulture = societyProfiles.Count == 1
            ? RequiredField(societyProfileType, "cultureValues")
                .GetValue(societyProfiles[0]) : null;
        CheckIt("Saved profile Culture migration",
            savedCulture != null && savedSocietyCulture != null
                && StringField(cultureProfiles[0], "key")
                    == "b16-culture-profile"
                && StringField(societyProfiles[0], "key")
                    == "b16-society-profile"
                && Current(savedCulture) && Current(savedSocietyCulture),
            "Scribe readback retains both profile identities and their schema-11/registry-3 nested Culture");
    }

    private static void VerifyLegacyCultureMigration(Assembly assembly,
        Assembly gameAssembly, Type cultureType, Type cultureModel)
    {
        object legacy = Activator.CreateInstance(cultureType)!;
        RequiredField(cultureType, "id").SetValue(legacy,
            "culture.b16-schema9-receipt");
        RequiredField(cultureType, "name").SetValue(legacy,
            "Schema 9 receipt Culture");
        RequiredField(cultureType, "schemaVersion").SetValue(legacy, 9);
        RequiredField(cultureType, "questionRegistryVersion").SetValue(
            legacy, 0);
        Type meaningType = RequiredType(assembly,
            "ColonistAwareness.CACulturalMeaning");
        object meaning = Activator.CreateInstance(meaningType)!;
        RequiredField(meaningType, "subjectKey").SetValue(meaning,
            "ca.property.private_ownership");
        RequiredField(meaningType, "populationScope").SetValue(meaning, "*");
        RequiredField(meaningType, "approval").SetValue(meaning, 60);
        RequiredField(meaningType, "normality").SetValue(meaning, 70);
        RequiredField(meaningType, "prestige").SetValue(meaning, 30);
        RequiredField(meaningType, "salience").SetValue(meaning, 80);
        RequiredField(meaningType, "provenance").SetValue(meaning,
            "schema-9 receipt");
        RequiredField(meaningType, "sourceIdentity").SetValue(meaning,
            "schema-9 source");
        RequiredField(meaningType, "evidenceSignature").SetValue(meaning,
            "schema-9-evidence");
        ((IList)RequiredField(cultureType, "inheritedMeanings")
            .GetValue(legacy)!).Add(meaning);

        object[] call = { legacy, null, null };
        bool migrated = (bool)Method(cultureModel,
            "TryUpgradeToCurrent", 3).Invoke(null, call)!;
        object current = call[1];
        object readback = current == null ? null : ScribeRoundTrip(
            gameAssembly, cultureType, current, "caSchema9CultureReceipt");
        bool Complete(object culture)
        {
            if (culture == null) return false;
            IList inherited = (IList)RequiredField(cultureType,
                "inheritedQuestions").GetValue(culture)!;
            IList local = (IList)RequiredField(cultureType,
                "localQuestions").GetValue(culture)!;
            object[] rows = inherited.Cast<object>().Concat(
                local.Cast<object>()).ToArray();
            return (int)RequiredField(cultureType, "schemaVersion")
                    .GetValue(culture)! == 11
                && (int)RequiredField(cultureType,
                    "questionRegistryVersion").GetValue(culture)! == 3
                && rows.Select(value => StringField(value, "questionKey"))
                    .Distinct(StringComparer.Ordinal).Count() == 48
                && rows.Any(value => StringField(value, "questionKey")
                    == "property.control" && FloatField(value, "mean") < 0f)
                && ((IList)RequiredField(cultureType, "legacyEvidence")
                    .GetValue(culture)!).Count == 1;
        }
        CheckIt("Schema-9 Culture migration",
            migrated && call[2] == null && Complete(current)
                && Complete(readback),
            migrated && call[2] == null
                ? "the exact legacy meaning maps to property control, all 48 registry-3 questions are present, and the full evidence row survives Scribe readback"
                : "migration rejected: " + (call[2]
                    ?? "no failure supplied"));
    }

    private static void VerifyDescriptiveEvidenceBoundary(Assembly assembly,
        Type culturePresets, IList presets, Type cultureType)
    {
        object culture = Activator.CreateInstance(cultureType)!;
        Method(culturePresets, "Apply", 3).Invoke(null,
            new[] { culture, presets[0], "b16-descriptive-boundary" });
        IList inherited = (IList)RequiredField(cultureType,
            "inheritedQuestions").GetValue(culture)!;
        object source = inherited.Cast<object>().Single(value =>
            StringField(value, "questionKey")
                == "relationships.sameSexAcceptance");
        RequiredField(source.GetType(), "mean").SetValue(source, -0.60f);

        Type patternType = RequiredType(assembly,
            "ColonistAwareness.CASocialGroupPattern");
        object Pattern(bool appraisal, string signature, int start, int end)
        {
            object pattern = Activator.CreateInstance(patternType)!;
            RequiredField(patternType, "QuestionKey").SetValue(pattern,
                "relationships.sameSexAcceptance");
            RequiredField(patternType, "PopulationIdentity").SetValue(
                pattern, "*");
            RequiredField(patternType, "WeightedPosition").SetValue(
                pattern, 80);
            RequiredField(patternType, "AppraisalEvidence").SetValue(
                pattern, appraisal);
            RequiredField(patternType, "Dispersion").SetValue(pattern, 0.2f);
            RequiredField(patternType, "Participation").SetValue(pattern, 1f);
            RequiredField(patternType, "GroupAlignment").SetValue(pattern,
                0.8f);
            RequiredField(patternType, "EvidenceCount").SetValue(pattern, 2);
            RequiredField(patternType, "ObservedPawnCount").SetValue(pattern,
                4);
            RequiredField(patternType, "EligiblePopulation").SetValue(pattern,
                4);
            RequiredField(patternType, "EvidenceStartTick").SetValue(pattern,
                start);
            RequiredField(patternType, "LastEvidenceTick").SetValue(pattern,
                end);
            RequiredField(patternType, "EvidenceSignature").SetValue(pattern,
                signature);
            return pattern;
        }
        Array direct = Array.CreateInstance(patternType, 1);
        direct.SetValue(Pattern(false, "represented-union", 0, 60000), 0);
        Type history = RequiredType(assembly,
            "ColonistAwareness.CACultureHistory");
        MethodInfo evaluate = Method(history, "EvaluateMeaningTransition", 4);
        bool descriptiveChanged = (bool)evaluate.Invoke(null,
            new object[] { culture, direct, "receipt population", 60000 })!;
        IList local = (IList)RequiredField(cultureType, "localQuestions")
            .GetValue(culture)!;
        object descriptive = local.Cast<object>().Single(value =>
            StringField(value, "questionKey")
                == "relationships.sameSexAcceptance");
        float directMean = FloatField(descriptive, "mean");
        float descriptiveNorm = FloatField(descriptive,
            "descriptiveNormPrior");

        Array appraisal = Array.CreateInstance(patternType, 1);
        appraisal.SetValue(Pattern(true, "expressed-approval", 60000,
            120000), 0);
        bool appraisalChanged = (bool)evaluate.Invoke(null,
            new object[] { culture, appraisal, "receipt population",
                120000 })!;
        object appraised = ((IList)RequiredField(cultureType,
                "localQuestions").GetValue(culture)!).Cast<object>()
            .Single(value => StringField(value, "questionKey")
                == "relationships.sameSexAcceptance");
        float appraisalMean = FloatField(appraised, "mean");
        CheckIt("Represented state does not manufacture appraisal",
            descriptiveChanged && Math.Abs(directMean + 0.60f) < 0.001f
                && descriptiveNorm > directMean + 0.10f
                && appraisalChanged && appraisalMean > directMean + 0.10f,
            "sustained represented state changes only the descriptive norm; an explicit social appraisal is required before the evaluative mean moves");
    }

    private static void VerifyNativeOccurrences(Assembly assembly,
        Assembly gameAssembly)
    {
        Type kernel = RequiredType(assembly,
            "ColonistAwareness.CANativeCultureOccurrenceKernel");
        Type recordType = RequiredType(assembly,
            "ColonistAwareness.CANativeCultureEventRecord");
        Type scopeType = RequiredType(assembly,
            "ColonistAwareness.CANativeCultureOccurrenceScope");
        object actorScope = Enum.Parse(scopeType, "Actor");
        object actScope = Enum.Parse(scopeType, "SharedAct");
        object targetScope = Enum.Parse(scopeType, "SharedTargetAtTick");
        MethodInfo build = Method(kernel, "BuildOccurrenceKey", 7);
        MethodInfo pair = Method(kernel, "BuildPairedActIdentity", 4);
        string actorOne = (string)build.Invoke(null,
            new[] { (object)3, 11, "ca.practice.raiding", 900, actorScope,
                null, null })!;
        string actorTwo = (string)build.Invoke(null,
            new[] { (object)3, 12, "ca.practice.raiding", 900, actorScope,
                null, null })!;

        Type context = RequiredType(assembly,
            "ColonistAwareness.CANativeCultureSharedActContext");
        MethodInfo begin = Method(context, "Begin", 2);
        MethodInfo end = Method(context, "End", 1);
        MethodInfo composeActIdentity = Method(context, "BuildIdentity", 4);
        string beforeReload = (string)composeActIdentity.Invoke(null,
            new object[] { "raid", 900, "session-before", 1L })!;
        string afterReload = (string)composeActIdentity.Invoke(null,
            new object[] { "raid", 900, "session-after", 1L })!;
        string firstAct = (string)begin.Invoke(null,
            new object[] { "raid", 900 })!;
        string sharedOne;
        string sharedTwo;
        try
        {
            string current = (string)RequiredProperty(context,
                "CurrentIdentity").GetValue(null)!;
            sharedOne = (string)build.Invoke(null,
                new[] { (object)3, 11, "ca.practice.raiding", 900, actScope,
                    current, null })!;
            sharedTwo = (string)build.Invoke(null,
                new[] { (object)3, 12, "ca.practice.raiding", 900, actScope,
                    current, null })!;
        }
        finally { end.Invoke(null, new object[] { firstAct }); }
        string secondAct = (string)begin.Invoke(null,
            new object[] { "raid", 900 })!;
        string sameTickSecondAct;
        try
        {
            sameTickSecondAct = (string)build.Invoke(null,
                new[] { (object)3, 11, "ca.practice.raiding", 900, actScope,
                    secondAct, null })!;
        }
        finally { end.Invoke(null, new object[] { secondAct }); }
        object sharedWithoutAct = build.Invoke(null,
            new[] { (object)3, 11, "ca.practice.raiding", 900, actScope,
                null, null });
        string sameCorpseObserverOne = (string)build.Invoke(null,
            new[] { (object)3, 11, "ca.practice.corpse_exposure", 900,
                targetScope, null, "thing:44" })!;
        string sameCorpseObserverTwo = (string)build.Invoke(null,
            new[] { (object)3, 12, "ca.practice.corpse_exposure", 900,
                targetScope, null, "thing:44" })!;
        string secondCorpse = (string)build.Invoke(null,
            new[] { (object)3, 12, "ca.practice.corpse_exposure", 900,
                targetScope, null, "thing:45" })!;
        object targetWithoutIdentity = build.Invoke(null,
            new[] { (object)3, 12, "ca.practice.corpse_exposure", 900,
                targetScope, null, null });
        string lovinPairOne = (string)pair.Invoke(null,
            new object[] { 11, 101, 12, 102 })!;
        string lovinPairOtherParticipant = (string)pair.Invoke(null,
            new object[] { 12, 102, 11, 101 })!;
        string lovinPairSecondAct = (string)pair.Invoke(null,
            new object[] { 11, 201, 12, 202 })!;
        string lovinOccurrenceOne = (string)build.Invoke(null,
            new[] { (object)3, 11, "ca.practice.non_spousal_intimacy", 900,
                actScope, lovinPairOne, "thing:12" })!;
        string lovinOccurrenceOtherParticipant = (string)build.Invoke(null,
            new[] { (object)3, 12, "ca.practice.non_spousal_intimacy", 901,
                actScope, lovinPairOtherParticipant, "thing:11" })!;
        string lovinOccurrenceSecondAct = (string)build.Invoke(null,
            new[] { (object)3, 11, "ca.practice.non_spousal_intimacy", 902,
                actScope, lovinPairSecondAct, "thing:12" })!;

        object Record(string key, int x, int z, int tick,
            int faction = 7, string practice = "ca.practice.raiding",
            int map = 3, string locality = "settlement:a")
        {
            object value = Activator.CreateInstance(recordType)!;
            RequiredField(recordType, "packageId").SetValue(value,
                "ludeon.rimworld");
            RequiredField(recordType, "eventDefName").SetValue(value,
                "Mined");
            RequiredField(recordType, "practiceKey").SetValue(value,
                practice);
            RequiredField(recordType, "occurrenceKey").SetValue(value, key);
            RequiredField(recordType, "targetIdentity").SetValue(value,
                "thing:91");
            RequiredField(recordType, "pawnId").SetValue(value, 11);
            RequiredField(recordType, "factionLoadId").SetValue(value,
                faction);
            RequiredField(recordType, "mapId").SetValue(value, map);
            RequiredField(recordType, "localityKey").SetValue(value,
                locality);
            RequiredField(recordType, "cellX").SetValue(value, x);
            RequiredField(recordType, "cellZ").SetValue(value, z);
            RequiredField(recordType, "tick").SetValue(value, tick);
            return value;
        }
        object first = Record(sharedOne, 4, 4, 900);
        object duplicate = Record(sharedTwo, 5, 5, 900);
        object distinct = Record(sameTickSecondAct, 40, 40, 900);
        Array same = Array.CreateInstance(recordType, 2);
        same.SetValue(first, 0);
        same.SetValue(duplicate, 1);
        Array different = Array.CreateInstance(recordType, 2);
        different.SetValue(first, 0);
        different.SetValue(distinct, 1);
        int sameCount = (int)Method(kernel, "DistinctCount", 1)
            .Invoke(null, new object[] { same })!;
        int differentCount = (int)Method(kernel, "DistinctCount", 1)
            .Invoke(null, new object[] { different })!;
        MethodInfo bounds = Method(kernel, "MatchesBounds", 9);
        bool inside = (bool)bounds.Invoke(null, new object[]
            { first, 7, false, 0, 0, 10, 10, 800, 1000 })!;
        bool outside = (bool)bounds.Invoke(null, new object[]
            { distinct, 7, false, 0, 0, 10, 10, 800, 1000 })!;
        bool mapWide = (bool)bounds.Invoke(null, new object[]
            { distinct, 7, true, 0, 0, 10, 10, 800, 1000 })!;
        CheckIt("Native occurrence identity and locality",
            actorOne != actorTwo && sharedOne == sharedTwo
                && sharedOne != sameTickSecondAct && sharedWithoutAct == null
                && sameCorpseObserverOne == sameCorpseObserverTwo
                && sameCorpseObserverOne != secondCorpse
                && targetWithoutIdentity == null
                && beforeReload != afterReload
                && sameCount == 1 && differentCount == 2
                && inside && !outside && mapWide,
            "participants collapse within one act, two same-tick acts remain distinct, a process reload at the same tick retains a different session identity, observers collapse per target, and missing shared identity fails closed");
        object gotLovin = Method(RequiredType(assembly,
                "ColonistAwareness.CANativeCultureEventAdapterRegistry"),
                "Find", 2).Invoke(null,
            new object[] { "ludeon.rimworld", "GotLovin_NonSpouse" });
        CheckIt("Intimacy occurrence identity",
            lovinPairOne == lovinPairOtherParticipant
                && lovinPairOne != lovinPairSecondAct
                && lovinOccurrenceOne == lovinOccurrenceOtherParticipant
                && lovinOccurrenceOne != lovinOccurrenceSecondAct
                && gotLovin != null && RequiredField(gotLovin.GetType(),
                    "OccurrenceScope").GetValue(gotLovin)!.ToString()
                    == "SharedAct",
            "two participant emissions from one encounter remain one occurrence; a later encounter with new native jobs becomes a second occurrence");

        Type eventRegistry = RequiredType(assembly,
            "ColonistAwareness.CANativeCultureEventAdapterRegistry");
        MethodInfo find = eventRegistry.GetMethods(All).Single(method =>
            method.Name == "Find" && method.GetParameters().Length == 2);
        object soldSlave = find.Invoke(null,
            new object[] { "ludeon.rimworld", "SoldSlave" });
        CheckIt("Proxy Doer is not attributed",
            soldSlave != null && RequiredField(soldSlave.GetType(),
                "OccurrenceScope").GetValue(soldSlave)!.ToString()
                    == "SharedAct"
            && RequiredField(soldSlave.GetType(), "Provenance")
                .GetValue(soldSlave)!.ToString() == "Participant"
            && sharedWithoutAct == null,
            "SoldSlave is accepted only inside the audited local trade scope; the remote random-colonist emitter has no act identity and is ignored");

        object gotBlinded = find.Invoke(null,
            new object[] { "ludeon.rimworld.ideology", "GotBlinded" });
        object gotScarified = find.Invoke(null,
            new object[] { "ludeon.rimworld.ideology", "GotScarified" });
        CheckIt("Ritual injury records the recipient",
            new[] { gotBlinded, gotScarified }.All(value => value != null
                && RequiredField(value.GetType(), "Provenance")
                    .GetValue(value)!.ToString() == "Recipient"
                && RequiredField(value.GetType(), "OccurrenceScope")
                    .GetValue(value)!.ToString() == "Actor"),
            "GotBlinded and GotScarified keep per-pawn occurrence identity but do not mislabel the altered pawn as the actor");

        Type retention = RequiredType(assembly,
            "ColonistAwareness.CANativeCultureEventRetentionKernel");
        Type listType = typeof(List<>).MakeGenericType(recordType);
        IList ledger = (IList)Activator.CreateInstance(listType)!;
        MethodInfo append = Method(retention, "AppendBounded", 2);
        append.Invoke(null, new[] { ledger,
            Record("rare:1", 1, 1, 1, 70, "ca.practice.rare") });
        append.Invoke(null, new[] { ledger,
            Record("rare:2", 1, 1, 2, 70, "ca.practice.rare") });
        for (int index = 0; index < 1000; index++)
            append.Invoke(null, new[] { ledger,
                Record("common:" + index, 2, 2, 100 + index, 71,
                    "ca.practice.common") });
        append.Invoke(null, new[] { ledger,
            Record("common:999", 3, 3, 1100, 71,
                "ca.practice.common") });
        append.Invoke(null, new[] { ledger,
            Record("other-settlement:1", 80, 80, 2000, 71,
                "ca.practice.common", 3, "settlement:b") });
        append.Invoke(null, new[] { ledger,
            Record("other-settlement:2", 80, 80, 2001, 71,
                "ca.practice.common", 3, "settlement:b") });
        int rareCount = ledger.Cast<object>().Count(value =>
            StringField(value, "practiceKey") == "ca.practice.rare");
        int commonCount = ledger.Cast<object>().Count(value =>
            StringField(value, "practiceKey") == "ca.practice.common"
                && (int)RequiredField(recordType, "mapId").GetValue(value)!
                    == 3
                && StringField(value, "localityKey") == "settlement:a");
        int commonOccurrences = ledger.Cast<object>().Where(value =>
                StringField(value, "practiceKey") == "ca.practice.common"
                && StringField(value, "localityKey") == "settlement:a")
            .Select(value => StringField(value, "occurrenceKey"))
            .Distinct(StringComparer.Ordinal).Count();
        int otherSettlementCount = ledger.Cast<object>().Count(value =>
            StringField(value, "practiceKey") == "ca.practice.common"
                && StringField(value, "localityKey") == "settlement:b");
        object retentionFailure = Method(retention, "ValidationFailure", 1)
            .Invoke(null, new object[] { ledger });
        CheckIt("Native event retention is per settlement practice",
            rareCount == 2 && commonCount == 65 && commonOccurrences == 64
                && otherSettlementCount == 2
                && retentionFailure == null,
            "rare evidence and another same-map, same-faction settlement's same-practice evidence survive a 1000-occurrence flood; duplicate participant emissions remain with their occurrence while only 64 distinct occurrences are retained");

        object sourceRecord = Record("3:pawn:11:ca.practice.resource:1200",
            14, 19, 1200, 72, "ca.practice.resource_extraction");
        object readRecord = ScribeRoundTrip(gameAssembly, recordType,
            sourceRecord, "caNativeCultureEventReceipt");
        CheckIt("Native event Scribe round-trip",
            QuestionSignature(sourceRecord) == QuestionSignature(readRecord),
            "package, event, practice, occurrence, target, tick, pawn, faction, and cell provenance survive serialization readback");
    }

    private static void VerifyStreamingPreflight(Assembly assembly,
        string[] questionKeys)
    {
        string validEvent = NativeEventRow("Mined",
            "ca.practice.resource_extraction", 1);
        IReadOnlyCollection<string> validEventFailures = PreflightFailures(
            assembly, CultureLongitudinalPayloadXml(null, validEvent));
        IReadOnlyCollection<string> unknownEventFailures = PreflightFailures(
            assembly, CultureLongitudinalPayloadXml(null,
                NativeEventRow("UnknownEvent",
                    "ca.practice.resource_extraction", 1)));
        string overflowRows = string.Concat(Enumerable.Range(1, 65).Select(
            index => NativeEventRow("Mined",
                "ca.practice.resource_extraction", index)));
        IReadOnlyCollection<string> overflowFailures = PreflightFailures(
            assembly, CultureLongitudinalPayloadXml(null, overflowRows));
        string duplicateRows = string.Concat(Enumerable.Range(1, 65).Select(
            _ => NativeEventRow("Mined",
                "ca.practice.resource_extraction", 1)));
        IReadOnlyCollection<string> duplicateFailures = PreflightFailures(
            assembly, CultureLongitudinalPayloadXml(null, duplicateRows));
        CheckIt("Native event streaming preflight",
            validEventFailures.Count == 0
                && unknownEventFailures.Any(value => value.Contains(
                    "no exact supported native Culture adapter",
                    StringComparison.Ordinal))
                && overflowFailures.Any(value => value.Contains(
                    "exceeds 64 distinct occurrences",
                    StringComparison.Ordinal))
                && duplicateFailures.Count == 0,
            "a valid exact event and repeated emissions of one occurrence pass; an unknown event and 65 distinct occurrences in one map/faction/locality/practice bucket fail before Scribe owner load");

        IReadOnlyCollection<string> completeCultureFailures =
            PreflightFailures(assembly, CultureLongitudinalPayloadXml(
                CultureXml(questionKeys), ""));
        IReadOnlyCollection<string> incompleteCultureFailures =
            PreflightFailures(assembly, CultureLongitudinalPayloadXml(
                CultureXml(questionKeys.Take(questionKeys.Length - 1)), ""));
        CheckIt("Culture scope completeness preflight",
            completeCultureFailures.Count == 0
                && incompleteCultureFailures.Any(value => value.Contains(
                    "is missing 1 Culture questions", StringComparison.Ordinal)),
            completeCultureFailures.Count == 0
                    && incompleteCultureFailures.Any(value => value.Contains(
                        "is missing 1 Culture questions",
                        StringComparison.Ordinal))
                ? "schema-11 registry-3 Culture requires all 48 questions in every represented population scope before Scribe owner load"
                : "complete=" + string.Join("; ", completeCultureFailures)
                    + "; incomplete=" + string.Join("; ",
                        incompleteCultureFailures));

        string legacyCulture = CultureXml(questionKeys.Take(24), 10);
        IReadOnlyCollection<string> predecessorFailures = PreflightFailures(
            assembly, CultureLongitudinalPayloadXml(legacyCulture, "", 2,
                false));
        IReadOnlyCollection<string> missingCurrentLedgerFailures =
            PreflightFailures(assembly, CultureLongitudinalPayloadXml(
                CultureXml(questionKeys), "", 3, false));
        IReadOnlyCollection<string> mismatchedCurrentFailures =
            PreflightFailures(assembly, CultureLongitudinalPayloadXml(
                legacyCulture, "", 3));
        CheckIt("Owner generation binds nested Culture preflight",
            predecessorFailures.Count == 0
                && missingCurrentLedgerFailures.Any(value => value.Contains(
                    "CA_nativeCultureEvents must occur exactly once",
                    StringComparison.Ordinal))
                && mismatchedCurrentFailures.Any(value => value.Contains(
                    "does not match owner generation; expected 11",
                    StringComparison.Ordinal)),
            predecessorFailures.Count == 0
                ? "owner schema 2 accepts Culture-10/registry-2 without the not-yet-introduced native-event ledger; current owner schema 3 requires that ledger and rejects legacy Culture before Scribe"
                : "predecessor=" + string.Join("; ", predecessorFailures)
                    + "; missing-current-ledger=" + string.Join("; ",
                        missingCurrentLedgerFailures)
                    + "; current=" + string.Join("; ",
                        mismatchedCurrentFailures));
    }

    private static void VerifySourceLabelResolution(Type semanticRegistry,
        Assembly gameAssembly)
    {
        const string defName = "Cannibalism_Preferred";
        const string packageId = "ludeon.rimworld.ideology";
        const string unknown = "a related Ideoligion rule";
        Type preceptType = RequiredType(gameAssembly, "RimWorld.PreceptDef");
        Type packType = RequiredType(gameAssembly, "Verse.ModContentPack");
        Type databaseType = RequiredType(gameAssembly,
            "Verse.DefDatabase`1").MakeGenericType(preceptType);
        MethodInfo getNamed = databaseType.GetMethods(All).Single(method =>
            method.Name == "GetNamedSilentFail"
                && method.GetParameters().Length == 1);
        MethodInfo add = databaseType.GetMethods(All).Single(method =>
            method.Name == "Add" && method.GetParameters().Length == 1
                && method.GetParameters()[0].ParameterType == preceptType);
        MethodInfo clear = Method(databaseType, "Clear", 0);
        IList original = ((IEnumerable)RequiredProperty(databaseType,
                "AllDefsListForReading").GetValue(null)!).Cast<object>()
            .ToList();
        bool inserted = false;
        try
        {
            object definition = getNamed.Invoke(null,
                new object[] { defName });
            if (definition == null)
            {
                object pack = RuntimeHelpers.GetUninitializedObject(packType);
                RequiredField(packType, "packageIdInt").SetValue(pack,
                    packageId);
                definition = RuntimeHelpers.GetUninitializedObject(
                    preceptType);
                RequiredField(preceptType, "defName").SetValue(definition,
                    defName);
                RequiredField(preceptType, "label").SetValue(definition,
                    "receipt cannibalism rule");
                RequiredField(preceptType, "modContentPack").SetValue(
                    definition, pack);
                add.Invoke(null, new[] { definition });
                inserted = true;
            }

            MethodInfo sourceLabel = Method(semanticRegistry, "SourceLabel",
                1);
            string exact = (string)sourceLabel.Invoke(null, new object[]
                { packageId + "|Precept|" + defName })!;
            string wrongPackage = (string)sourceLabel.Invoke(null,
                new object[] { "unknown.mod|Precept|" + defName })!;
            string shorthand = (string)sourceLabel.Invoke(null,
                new object[] { "precept:" + defName })!;
            CheckIt("Exact Ideoligion source-label resolution",
                !string.IsNullOrWhiteSpace(exact) && exact != unknown
                    && wrongPackage == unknown && shorthand == unknown,
                "a registered package|kind|def token resolves its native label; a package mismatch and retired shorthand both fail closed");
        }
        finally
        {
            if (inserted)
            {
                clear.Invoke(null, Array.Empty<object>());
                foreach (object definition in original)
                    add.Invoke(null, new[] { definition });
            }
        }
    }

    private static IReadOnlyCollection<string> PreflightFailures(
        Assembly assembly, string xml)
    {
        string path = Path.Combine(Path.GetTempPath(), "cao-b16-preflight-"
            + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            File.WriteAllText(path, xml, new UTF8Encoding(false));
            Type reader = RequiredType(assembly,
                "ColonistAwareness.CACampaignPreflightReader");
            object document = Method(reader, "Read", 1).Invoke(null,
                new object[] { path })!;
            IEnumerable payloads = (IEnumerable)RequiredProperty(
                document.GetType(), "Payloads").GetValue(document)!;
            object payload = payloads.Cast<object>().Single(value =>
                StringProperty(value, "ComponentType")
                    == "ColonistAwareness.CACultureLongitudinalMapComponent");
            return ((IEnumerable)RequiredProperty(payload.GetType(),
                    "ValidationFailures").GetValue(payload)!).Cast<object>()
                .Select(value => value.ToString()!).ToArray();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static string CultureLongitudinalPayloadXml(string culture,
        string eventRows, int ownerSchema = 3,
        bool includeEventCollection = true)
    {
        string cultureNode = culture ??
            "<CA_playerLocalCulture IsNull=\"True\" />";
        string eventCollection = includeEventCollection
            ? "<CA_nativeCultureEvents>" + eventRows
                + "</CA_nativeCultureEvents>"
            : "";
        return "<savegame><game><maps><li><components>"
            + "<li Class=\"ColonistAwareness.CACultureLongitudinalMapComponent\">"
            + "<CA_cultureHistorySchemaVersion>" + ownerSchema
            + "</CA_cultureHistorySchemaVersion>"
            + cultureNode + eventCollection
            + "</li></components></li></maps>"
            + "</game></savegame>";
    }

    private static string NativeEventRow(string eventDefName,
        string practiceKey, int occurrence)
    {
        return "<li><schemaVersion>2</schemaVersion>"
            + "<packageId>ludeon.rimworld</packageId><eventDefName>"
            + eventDefName + "</eventDefName><practiceKey>" + practiceKey
            + "</practiceKey><occurrenceKey>receipt:" + occurrence
            + "</occurrenceKey><tick>" + occurrence + "</tick>"
            + "<pawnId>1</pawnId><factionLoadId>2</factionLoadId>"
            + "<mapId>3</mapId><localityKey>settlement:receipt#1</localityKey>"
            + "<cellX>4</cellX><cellZ>5</cellZ></li>";
    }

    private static string CultureXml(IEnumerable<string> questionKeys,
        int schemaVersion = 11)
    {
        string rows = string.Concat(questionKeys.Select(key =>
            "<li><schemaVersion>1</schemaVersion><questionKey>" + key
            + "</questionKey><populationScope>*</populationScope>"
            + "<subgroups /></li>"));
        int registryVersion = schemaVersion == 10 ? 2 : 3;
        return "<CA_playerLocalCulture><schemaVersion>" + schemaVersion
            + "</schemaVersion><constituents /><questionRegistryVersion>"
            + registryVersion + "</questionRegistryVersion>"
            + "<withinGroupSpread>2</withinGroupSpread>"
            + "<subgroupSeparation>2</subgroupSeparation>"
            + "<inheritedQuestions>" + rows + "</inheritedQuestions>"
            + "<localQuestions /><legacyEvidence /><transitions />"
            + "<inheritedPractices /><practices /><observations />"
            + "<lastEvidence IsNull=\"True\" />"
            + "</CA_playerLocalCulture>";
    }

    private static void VerifyFixture(string active, string mirror,
        string repo, IReadOnlyCollection<string> questionKeys,
        Assembly assembly, Assembly gameAssembly)
    {
        byte[] activeBytes = File.ReadAllBytes(active);
        byte[] mirrorBytes = File.ReadAllBytes(mirror);
        XDocument current = XDocument.Load(new MemoryStream(activeBytes));
        XElement plan = current.Root?.Element("plan")!;
        XElement[] cultures = CultureNodes(current);
        IEnumerable<XElement> Rows(XElement culture) => culture
            .Element("inheritedQuestions")!.Elements("li")
            .Concat(culture.Element("localQuestions")!.Elements("li"));
        bool ScopeComplete(IGrouping<string, XElement> scope)
        {
            var exact = new HashSet<string>(scope.Select(row =>
                Value(row, "questionKey")), StringComparer.Ordinal);
            return exact.Count == questionKeys.Count
                && questionKeys.All(exact.Contains);
        }
        string[] unknownKeys = cultures.SelectMany(Rows)
            .Select(row => Value(row, "questionKey"))
            .Where(key => !questionKeys.Contains(key))
            .Distinct(StringComparer.Ordinal).ToArray();
        string[] badScopes = cultures.SelectMany(culture => Rows(culture)
                .GroupBy(Scope, StringComparer.Ordinal)
                .Where(scope => !ScopeComplete(scope))
                .Select(scope => Value(culture, "id") + ":" + scope.Key
                    + "=" + scope.Count())).ToArray();
        bool coverage = cultures.Length == 8 && cultures.All(culture =>
            Value(culture, "questionRegistryVersion") == "3"
            && Rows(culture)
                .GroupBy(Scope, StringComparer.Ordinal).All(scope =>
                    ScopeComplete(scope)
                    && scope.All(row => questionKeys.Contains(
                        Value(row, "questionKey")))));
        CheckIt("Current fixture pair", activeBytes.SequenceEqual(mirrorBytes)
                && Value(current.Root!, "authoringDataEpoch") == "14"
                && Value(plan, "schemaVersion") == "15"
                && coverage && Value(plan, "regionalId") == "CA-RG-EB596A12"
                && Value(plan, "candidateId") == "613b1fe44104"
                && plan.Element("factions")!.Elements("li").Count() == 3
                && plan.Element("settlements")!.Elements("li").Count() == 4
                && plan.Element("relations")!.Elements("li").Count() == 3,
            unknownKeys.Length > 0 ? "unknown keys: "
                + string.Join(", ", unknownKeys)
                : badScopes.Length > 0 ? "bad scopes: "
                    + string.Join(", ", badScopes)
                : "pair=" + activeBytes.SequenceEqual(mirrorBytes)
                    + "; epoch=" + Value(current.Root!,
                        "authoringDataEpoch")
                    + "; schema=" + Value(plan, "schemaVersion")
                    + "; cultures=" + cultures.Length
                    + "; region=" + Value(plan, "regionalId")
                    + "; candidate=" + Value(plan, "candidateId")
                    + "; factions=" + plan.Element("factions")!
                        .Elements("li").Count()
                    + "; settlements=" + plan.Element("settlements")!
                        .Elements("li").Count());

        string baselinePath = Path.Combine(repo, "Receipts", "B16",
            "evidence", "active-registry2-before-b16.xml");
        XDocument baseline = XDocument.Load(baselinePath);
        Dictionary<string, string> before = CultureRows(baseline);
        Dictionary<string, string> after = CultureRows(current);
        bool orphanWasPresent = before.Remove(
            GovernedOrphanQuestionIdentity);
        bool orphanPreservedAsEvidence = current.Descendants("legacyEvidence")
            .Elements("li").Any(value =>
                Value(value, "sourceLayer") == "B16 governed fixture repair"
                && Value(value, "evidenceSignature")
                    == GovernedOrphanEvidenceSignature
                && Value(value, "disposition").Contains(
                    "not a constituent", StringComparison.Ordinal));
        CheckIt("Authored Culture preservation", orphanWasPresent
                && orphanPreservedAsEvidence && before.All(pair =>
                    after.TryGetValue(pair.Key, out string xml)
                    && xml == pair.Value),
            before.Count + " live pre-B16 Culture rows survive exact serialization readback; one orphaned scoped row remains durable legacy evidence rather than live state");

        Type planType = RequiredType(assembly,
            "ColonistAwareness.CARegionalPlan");
        Type cultureType = RequiredType(assembly,
            "ColonistAwareness.CACulture");
        object loaded = LoadDeepRoot(gameAssembly, planType, active, "plan");
        IList loadedFactions = (IList)RequiredField(planType, "factions")
            .GetValue(loaded)!;
        IList loadedSettlements = (IList)RequiredField(planType,
            "settlements").GetValue(loaded)!;
        IList loadedRelations = (IList)RequiredField(planType, "relations")
            .GetValue(loaded)!;
        object loadedFounding = RequiredField(planType, "playerFounding")
            .GetValue(loaded)!;
        var loadedCultures = loadedFactions.Cast<object>().Select(value =>
                RequiredField(value.GetType(), "culture").GetValue(value))
            .Concat(loadedSettlements.Cast<object>().Select(value =>
                RequiredField(value.GetType(), "localCulture")
                    .GetValue(value)))
            .Append(RequiredField(loadedFounding.GetType(), "culture")
                .GetValue(loadedFounding)).Where(value => value != null)
            .ToArray();
        bool CurrentCulture(object culture)
        {
            Type type = culture.GetType();
            if ((int)RequiredField(type, "schemaVersion").GetValue(culture)!
                    != 11
                || (int)RequiredField(type, "questionRegistryVersion")
                    .GetValue(culture)! != 3)
                return false;
            IEnumerable<object> rows = ((IList)RequiredField(type,
                    "inheritedQuestions").GetValue(culture)!).Cast<object>()
                .Concat(((IList)RequiredField(type, "localQuestions")
                    .GetValue(culture)!).Cast<object>());
            return rows.GroupBy(value => StringField(value,
                    "populationScope") is string scope
                        && !string.IsNullOrWhiteSpace(scope) ? scope : "*",
                    StringComparer.Ordinal).All(group => group.Select(value =>
                        StringField(value, "questionKey")).Distinct(
                            StringComparer.Ordinal).Count() == 48);
        }
        CheckIt("Current fixture runtime Scribe load",
            (int)RequiredField(planType, "schemaVersion").GetValue(loaded)!
                    == 15
                && loadedFactions.Count == 3
                && loadedSettlements.Count == 4
                && loadedRelations.Count == 3
                && loadedCultures.Length == 8
                && loadedCultures.All(CurrentCulture),
            loadedCultures.Length == 8 && loadedCultures.All(CurrentCulture)
                ? "the runtime serializer loads schema 15 with 3 factions, 4 settlements, 3 relations, and eight registry-3 Culture owners without dropping their 48-question scopes"
                : "schema=" + RequiredField(planType, "schemaVersion")
                    .GetValue(loaded) + "; factions=" + loadedFactions.Count
                    + "; settlements=" + loadedSettlements.Count
                    + "; relations=" + loadedRelations.Count
                    + "; cultures=" + loadedCultures.Length + "; invalid="
                    + string.Join(", ", loadedCultures.Select((value, index) =>
                        CurrentCulture(value) ? null : index.ToString())
                        .Where(value => value != null)));

        string compatiblePath = Path.Combine(repo, "Receipts", "B16",
            "evidence", "active-registry2-compatible-migration.xml");
        object adjacent = LoadDeepRoot(gameAssembly, planType, compatiblePath,
            "plan");
        string adjacentRegion = StringField(adjacent, "regionalId");
        string adjacentCandidate = StringField(adjacent, "candidateId");
        int adjacentTile = (int)RequiredField(planType, "startTileId")
            .GetValue(adjacent)!;
        int adjacentMapSize = (int)RequiredField(planType, "mapSize")
            .GetValue(adjacent)!;
        Type epoch = RequiredType(assembly,
            "ColonistAwareness.CAPendingAuthoringDataEpoch");
        MethodInfo upgradePlan = Method(epoch, "TryUpgradeRegionalPlan", 3);
        object[] upgradeCall = { 13, adjacent, null };
        bool planMigrated = (bool)upgradePlan.Invoke(null, upgradeCall)!;
        object adjacentReadback = planMigrated ? ScribeRoundTrip(gameAssembly,
            planType, adjacent, "caRegionalPlanMigrationReceipt") : null;

        object[] OwnedCultures(object owner)
        {
            IList factions = (IList)RequiredField(planType, "factions")
                .GetValue(owner)!;
            IList settlements = (IList)RequiredField(planType, "settlements")
                .GetValue(owner)!;
            object founding = RequiredField(planType, "playerFounding")
                .GetValue(owner)!;
            return factions.Cast<object>().Select(value => RequiredField(
                    value.GetType(), "culture").GetValue(value))
                .Concat(settlements.Cast<object>().Select(value =>
                    RequiredField(value.GetType(), "localCulture")
                        .GetValue(value)))
                .Append(RequiredField(founding.GetType(), "culture")
                    .GetValue(founding)).Where(value => value != null)
                .ToArray();
        }
        bool PlanCurrent(object owner) => owner != null
            && (int)RequiredField(planType, "schemaVersion")
                .GetValue(owner)! == 15
            && ((IList)RequiredField(planType, "factions")
                .GetValue(owner)!).Count == 3
            && ((IList)RequiredField(planType, "settlements")
                .GetValue(owner)!).Count == 4
            && ((IList)RequiredField(planType, "relations")
                .GetValue(owner)!).Count == 3
            && OwnedCultures(owner).Length == 8
            && OwnedCultures(owner).All(CurrentCulture);
        CheckIt("Adjacent pending-plan migration",
            planMigrated && upgradeCall[2] == null && PlanCurrent(adjacent)
                && PlanCurrent(adjacentReadback)
                && StringField(adjacent, "regionalId") == adjacentRegion
                && StringField(adjacent, "candidateId") == adjacentCandidate
                && (int)RequiredField(planType, "startTileId")
                    .GetValue(adjacent)! == adjacentTile
                && (int)RequiredField(planType, "mapSize")
                    .GetValue(adjacent)! == adjacentMapSize,
            planMigrated && upgradeCall[2] == null
                ? "epoch-13/schema-14 evidence upgrades all eight nested Culture owners atomically to schema 15 and survives Scribe readback with identity, geography, and 3/4/3 composition intact"
                : "migration rejected: " + (upgradeCall[2]
                    ?? "no failure supplied"));

        object invalid = LoadDeepRoot(gameAssembly, planType, compatiblePath,
            "plan");
        IList invalidFactions = (IList)RequiredField(planType, "factions")
            .GetValue(invalid)!;
        object untouchedCulture = RequiredField(
            invalidFactions[0]!.GetType(), "culture")
            .GetValue(invalidFactions[0])!;
        object invalidCulture = RequiredField(
            invalidFactions[1]!.GetType(), "culture")
            .GetValue(invalidFactions[1])!;
        RequiredField(cultureType, "schemaVersion").SetValue(invalidCulture, 7);
        object[] failedCall = { 13, invalid, null };
        bool rejected = !(bool)upgradePlan.Invoke(null, failedCall)!;
        CheckIt("Pending-plan migration rollback",
            rejected && failedCall[2] != null
                && (int)RequiredField(planType, "schemaVersion")
                    .GetValue(invalid)! == 14
                && (int)RequiredField(cultureType, "schemaVersion")
                    .GetValue(untouchedCulture)! == 10,
            "one invalid nested Culture rejects the conversion before any owner or outer schema stamp changes");
    }

    private static void VerifyLiveOwnerMigrations(string repo,
        Assembly assembly, Assembly gameAssembly)
    {
        string baseline = Path.Combine(repo, "Receipts", "B16", "evidence",
            "active-registry2-compatible-migration.xml");
        Type planType = RequiredType(assembly,
            "ColonistAwareness.CARegionalPlan");
        Type cultureType = RequiredType(assembly,
            "ColonistAwareness.CACulture");
        Type cultureModel = RequiredType(assembly,
            "ColonistAwareness.CACultureModel");
        MethodInfo cultureValidation = Method(cultureModel,
            "ValidationFailure", 2);

        object LoadPlan() => LoadDeepRoot(gameAssembly, planType, baseline,
            "plan");
        bool CurrentCulture(object culture) => culture != null
            && (int)RequiredField(cultureType, "schemaVersion")
                .GetValue(culture)! == 11
            && (int)RequiredField(cultureType, "questionRegistryVersion")
                .GetValue(culture)! == 3
            && cultureValidation.Invoke(null, new[] { culture, (object)true })
                == null;
        IList NewList(Type itemType, params object[] values)
        {
            IList list = (IList)Activator.CreateInstance(typeof(List<>)
                .MakeGenericType(itemType))!;
            foreach (object value in values) list.Add(value);
            return list;
        }
        void ConfirmFounding(object plan)
        {
            object founding = RequiredField(planType, "playerFounding")
                .GetValue(plan)!;
            RequiredField(founding.GetType(), "confirmed").SetValue(founding,
                true);
        }

        Type factionStateType = RequiredType(assembly,
            "ColonistAwareness.CAFactionState");
        Type factionOwnerType = RequiredType(assembly,
            "ColonistAwareness.CAFactionStateWorldComponent");
        IList FactionStatesFor(object plan)
        {
            IList plans = (IList)RequiredField(planType, "factions")
                .GetValue(plan)!;
            IList states = NewList(factionStateType);
            for (int index = 0; index < plans.Count; index++)
            {
                object source = plans[index]!;
                object state = Activator.CreateInstance(factionStateType)!;
                RequiredField(factionStateType, "factionLoadId").SetValue(
                    state, 76000 + index);
                RequiredField(factionStateType, "factionName").SetValue(state,
                    StringField(source, "customName") is string name
                        && !string.IsNullOrWhiteSpace(name)
                            ? name : "B16 migration faction " + index);
                RequiredField(factionStateType, "engineTemplateDefName")
                    .SetValue(state, StringField(source,
                        "customFactionDefName"));
                foreach (string field in new[] { "culture",
                             "politicalBeliefs", "technologicalKnowledge",
                             "factionStructure" })
                    RequiredField(factionStateType, field).SetValue(state,
                        RequiredField(source.GetType(), field)
                            .GetValue(source));
                RequiredField(factionStateType,
                    "institutionalStateIncomplete").SetValue(state,
                    RequiredField(source.GetType(),
                        "institutionalStateIncomplete").GetValue(source));
                states.Add(state);
            }
            return states;
        }
        object FactionOwner(IList states)
        {
            object owner = RuntimeHelpers.GetUninitializedObject(
                factionOwnerType);
            RequiredField(factionOwnerType, "campaignSchemaVersion")
                .SetValue(owner, 3);
            RequiredField(factionOwnerType, "factionStates")
                .SetValue(owner, states);
            return owner;
        }

        object factionPlan = LoadPlan();
        IList factionStates = FactionStatesFor(factionPlan);
        object factionOwner = FactionOwner(factionStates);
        string factionFailure = (string)Method(factionOwnerType,
            "MigrateSupportedState", 0).Invoke(factionOwner,
                Array.Empty<object>());
        IList migratedFactionStates = (IList)RequiredField(factionOwnerType,
            "factionStates").GetValue(factionOwner)!;
        bool factionValid = factionFailure == null
            && !ReferenceEquals(factionStates, migratedFactionStates)
            && migratedFactionStates.Cast<object>().All(value =>
                CurrentCulture(RequiredField(factionStateType, "culture")
                    .GetValue(value)))
            && Method(factionOwnerType, "ValidateCampaignState", 0)
                .Invoke(factionOwner, Array.Empty<object>()) == null;
        RequiredField(factionOwnerType, "campaignSchemaVersion")
            .SetValue(factionOwner, 4);
        object factionReadback = factionValid ? ScribeRoundTrip(gameAssembly,
            factionOwnerType, factionOwner, "factionOwner") : null;
        IList readbackFactionStates = factionReadback == null ? null
            : (IList)RequiredField(factionOwnerType, "factionStates")
                .GetValue(factionReadback)!;

        object invalidFactionPlan = LoadPlan();
        IList invalidFactionPlans = (IList)RequiredField(planType, "factions")
            .GetValue(invalidFactionPlan)!;
        object invalidFactionCulture = RequiredField(
            invalidFactionPlans[1]!.GetType(), "culture")
            .GetValue(invalidFactionPlans[1])!;
        RequiredField(cultureType, "schemaVersion").SetValue(
            invalidFactionCulture, 7);
        IList rollbackFactionStates = FactionStatesFor(invalidFactionPlan);
        object firstFactionCulture = RequiredField(factionStateType,
            "culture").GetValue(rollbackFactionStates[0])!;
        object rollbackFactionOwner = FactionOwner(rollbackFactionStates);
        string rollbackFactionFailure = (string)Method(factionOwnerType,
            "MigrateSupportedState", 0).Invoke(rollbackFactionOwner,
                Array.Empty<object>());
        bool factionRollback = rollbackFactionFailure != null
            && ReferenceEquals(rollbackFactionStates,
                RequiredField(factionOwnerType, "factionStates")
                    .GetValue(rollbackFactionOwner))
            && (int)RequiredField(cultureType, "schemaVersion")
                .GetValue(firstFactionCulture)! == 10;
        CheckIt("Faction owner catalog-4 migration",
            factionValid && readbackFactionStates?.Count
                == migratedFactionStates.Count
                && readbackFactionStates.Cast<object>().All(value =>
                    CurrentCulture(RequiredField(factionStateType, "culture")
                        .GetValue(value))) && factionRollback,
            "schema-3 faction state migrates to exact registry-3 Culture, "
                + "survives owner Scribe readback, and an invalid sibling "
                + "leaves the original owner graph unchanged");

        Type foundingOwnerType = RequiredType(assembly,
            "ColonistAwareness.CAPlayerFoundingWorldComponent");
        object FoundingOwner(object plan)
        {
            ConfirmFounding(plan);
            object owner = RuntimeHelpers.GetUninitializedObject(
                foundingOwnerType);
            RequiredField(foundingOwnerType, "campaignSchemaVersion")
                .SetValue(owner, 3);
            RequiredField(foundingOwnerType, "founding").SetValue(owner,
                RequiredField(planType, "playerFounding").GetValue(plan));
            RequiredField(foundingOwnerType, "appliedAtTick")
                .SetValue(owner, 1);
            return owner;
        }
        object foundingOwner = FoundingOwner(LoadPlan());
        string foundingFailure = (string)Method(foundingOwnerType,
            "MigrateSupportedState", 0).Invoke(foundingOwner,
                Array.Empty<object>());
        object migratedFounding = RequiredField(foundingOwnerType, "founding")
            .GetValue(foundingOwner)!;
        bool foundingValid = foundingFailure == null
            && CurrentCulture(RequiredField(migratedFounding.GetType(),
                "culture").GetValue(migratedFounding))
            && Method(foundingOwnerType, "ValidateCampaignState", 0)
                .Invoke(foundingOwner, Array.Empty<object>()) == null;
        RequiredField(foundingOwnerType, "campaignSchemaVersion")
            .SetValue(foundingOwner, 4);
        object foundingReadback = foundingValid ? ScribeRoundTrip(
            gameAssembly, foundingOwnerType, foundingOwner, "foundingOwner")
            : null;
        object readbackFounding = foundingReadback == null ? null
            : RequiredField(foundingOwnerType, "founding")
                .GetValue(foundingReadback);

        object invalidFoundingPlan = LoadPlan();
        object rollbackFoundingOwner = FoundingOwner(invalidFoundingPlan);
        object untouchedFounding = RequiredField(foundingOwnerType, "founding")
            .GetValue(rollbackFoundingOwner)!;
        object badFoundingCulture = RequiredField(untouchedFounding.GetType(),
            "culture").GetValue(untouchedFounding)!;
        RequiredField(cultureType, "schemaVersion").SetValue(
            badFoundingCulture, 7);
        string rollbackFoundingFailure = (string)Method(foundingOwnerType,
            "MigrateSupportedState", 0).Invoke(rollbackFoundingOwner,
                Array.Empty<object>());
        bool foundingRollback = rollbackFoundingFailure != null
            && ReferenceEquals(untouchedFounding,
                RequiredField(foundingOwnerType, "founding")
                    .GetValue(rollbackFoundingOwner));
        CheckIt("Player-founding owner catalog-4 migration",
            foundingValid && readbackFounding != null
                && CurrentCulture(RequiredField(readbackFounding.GetType(),
                    "culture").GetValue(readbackFounding))
                && foundingRollback,
            "schema-3 founding state migrates to exact registry-3 Culture, "
                + "survives owner Scribe readback, and failed validation "
                + "does not replace the founding object");

        Type recordType = RequiredType(assembly,
            "ColonistAwareness.CARegionalSettlementRecord");
        Type regionalOwnerType = RequiredType(assembly,
            "ColonistAwareness.CARegionalWorldComponent");
        object RecordFor(object plan)
        {
            IList settlements = (IList)RequiredField(planType, "settlements")
                .GetValue(plan)!;
            object settlement = settlements[0]!;
            object record = Activator.CreateInstance(recordType)!;
            RequiredField(recordType, "schemaVersion").SetValue(record, 9);
            RequiredField(recordType, "regionalId").SetValue(record,
                StringField(plan, "regionalId"));
            RequiredField(recordType, "regionKey").SetValue(record,
                StringField(plan, "regionalId"));
            RequiredField(recordType, "slot").SetValue(record,
                RequiredField(settlement.GetType(), "slot")
                    .GetValue(settlement));
            RequiredField(recordType, "factionKey").SetValue(record,
                RequiredField(settlement.GetType(), "factionKey")
                    .GetValue(settlement));
            RequiredField(recordType, "culture").SetValue(record,
                RequiredField(settlement.GetType(), "localCulture")
                    .GetValue(settlement));
            RequiredField(recordType, "factionKnowledgeId").SetValue(record,
                "catalog-4-receipt");
            RequiredField(recordType, "factionKnowledgeRevision")
                .SetValue(record, 0);
            RequiredField(recordType, "factionKnowledgeTier")
                .SetValue(record, 0);
            return record;
        }
        object RegionalOwner(object plan, object record)
        {
            ConfirmFounding(plan);
            object owner = RuntimeHelpers.GetUninitializedObject(
                regionalOwnerType);
            RequiredField(regionalOwnerType, "campaignSchemaVersion")
                .SetValue(owner, 3);
            RequiredField(regionalOwnerType, "regions").SetValue(owner,
                NewList(planType, plan));
            RequiredField(regionalOwnerType, "records").SetValue(owner,
                NewList(recordType, record));
            RequiredField(regionalOwnerType, "worldPolicy").SetValue(owner,
                RequiredField(planType, "worldPolicy").GetValue(plan));
            RequiredField(regionalOwnerType, "groundwater").SetValue(owner,
                RequiredField(planType, "groundwater").GetValue(plan));
            return owner;
        }
        object regionalPlan = LoadPlan();
        object regionalRecord = RecordFor(regionalPlan);
        object regionalOwner = RegionalOwner(regionalPlan, regionalRecord);
        string regionalFailure = (string)Method(regionalOwnerType,
            "MigrateSupportedState", 0).Invoke(regionalOwner,
                Array.Empty<object>());
        bool regionalValid = regionalFailure == null
            && (int)RequiredField(planType, "schemaVersion")
                .GetValue(regionalPlan)! == 15
            && CurrentCulture(RequiredField(recordType, "culture")
                .GetValue(regionalRecord))
            && Method(regionalOwnerType, "ValidateCampaignState", 0)
                .Invoke(regionalOwner, Array.Empty<object>()) == null;
        RequiredField(regionalOwnerType, "campaignSchemaVersion")
            .SetValue(regionalOwner, 4);
        object regionalReadback = regionalValid ? ScribeRoundTrip(
            gameAssembly, regionalOwnerType, regionalOwner, "regionalOwner")
            : null;
        IList readbackRegions = regionalReadback == null ? null
            : (IList)RequiredField(regionalOwnerType, "regions")
                .GetValue(regionalReadback)!;
        IList readbackRecords = regionalReadback == null ? null
            : (IList)RequiredField(regionalOwnerType, "records")
                .GetValue(regionalReadback)!;

        object invalidRegionalPlan = LoadPlan();
        IList invalidRegionalFactions = (IList)RequiredField(planType,
            "factions").GetValue(invalidRegionalPlan)!;
        object firstRegionalCulture = RequiredField(
            invalidRegionalFactions[0]!.GetType(), "culture")
            .GetValue(invalidRegionalFactions[0])!;
        object badRegionalCulture = RequiredField(
            invalidRegionalFactions[1]!.GetType(), "culture")
            .GetValue(invalidRegionalFactions[1])!;
        RequiredField(cultureType, "schemaVersion").SetValue(
            badRegionalCulture, 7);
        object rollbackRegionalRecord = RecordFor(invalidRegionalPlan);
        object rollbackRegionalOwner = RegionalOwner(invalidRegionalPlan,
            rollbackRegionalRecord);
        string rollbackRegionalFailure = (string)Method(regionalOwnerType,
            "MigrateSupportedState", 0).Invoke(rollbackRegionalOwner,
                Array.Empty<object>());
        bool regionalRollback = rollbackRegionalFailure != null
            && (int)RequiredField(planType, "schemaVersion")
                .GetValue(invalidRegionalPlan)! == 14
            && (int)RequiredField(cultureType, "schemaVersion")
                .GetValue(firstRegionalCulture)! == 10;
        CheckIt("Regional owner catalog-4 migration",
            regionalValid && readbackRegions?.Count == 1
                && readbackRecords?.Count == 1
                && (int)RequiredField(planType, "schemaVersion")
                    .GetValue(readbackRegions[0])! == 15
                && CurrentCulture(RequiredField(recordType, "culture")
                    .GetValue(readbackRecords[0])) && regionalRollback,
            "schema-3 regional plans, settlements, founding copy, and record "
                + "migrate together, survive owner Scribe readback, and an "
                + "invalid nested Culture commits none of the queued changes");

        Type mapOwnerType = RequiredType(assembly,
            "ColonistAwareness.CACultureLongitudinalMapComponent");
        Type eventType = RequiredType(assembly,
            "ColonistAwareness.CANativeCultureEventRecord");
        Type mapType = RequiredType(gameAssembly, "Verse.Map");
        object MapOwner(object culture, IList events, int ownerSchema = 2,
            int ownerMapId = 3)
        {
            object owner = RuntimeHelpers.GetUninitializedObject(mapOwnerType);
            RequiredField(mapOwnerType, "campaignSchemaVersion")
                .SetValue(owner, ownerSchema);
            RequiredField(mapOwnerType, "nextEvaluationTick")
                .SetValue(owner, 5000);
            RequiredField(mapOwnerType, "playerLocalCulture")
                .SetValue(owner, culture);
            RequiredField(mapOwnerType, "nativeEvents").SetValue(owner,
                events);
            object ownerMap = RuntimeHelpers.GetUninitializedObject(mapType);
            RequiredField(mapType, "uniqueID").SetValue(ownerMap,
                ownerMapId);
            RequiredField(mapOwnerType, "map").SetValue(owner, ownerMap);
            return owner;
        }
        object NativeEvent(int mapId)
        {
            object value = Activator.CreateInstance(eventType)!;
            RequiredField(eventType, "packageId").SetValue(value,
                "ludeon.rimworld");
            RequiredField(eventType, "eventDefName").SetValue(value,
                "Mined");
            RequiredField(eventType, "practiceKey").SetValue(value,
                "ca.practice.resource_extraction");
            RequiredField(eventType, "occurrenceKey").SetValue(value,
                mapId + ":receipt:mined");
            RequiredField(eventType, "tick").SetValue(value, 1000);
            RequiredField(eventType, "pawnId").SetValue(value, 1);
            RequiredField(eventType, "factionLoadId").SetValue(value, 2);
            RequiredField(eventType, "mapId").SetValue(value, mapId);
            RequiredField(eventType, "localityKey").SetValue(value,
                "settlement:receipt#1");
            RequiredField(eventType, "cellX").SetValue(value, 4);
            RequiredField(eventType, "cellZ").SetValue(value, 5);
            return value;
        }
        object mapPlan = LoadPlan();
        object mapCulture = RequiredField(((IList)RequiredField(planType,
                "factions").GetValue(mapPlan)!)[0]!.GetType(), "culture")
            .GetValue(((IList)RequiredField(planType, "factions")
                .GetValue(mapPlan)!)[0])!;
        object mapOwner = MapOwner(mapCulture, NewList(eventType));
        string mapFailure = (string)Method(mapOwnerType, "MigrateState", 0)
            .Invoke(mapOwner, Array.Empty<object>());
        bool mapValid = mapFailure == null
            && CurrentCulture(RequiredField(mapOwnerType,
                "playerLocalCulture").GetValue(mapOwner))
            && Method(mapOwnerType, "ValidateCampaignState", 0)
                .Invoke(mapOwner, Array.Empty<object>()) == null;
        RequiredField(mapOwnerType, "campaignSchemaVersion")
            .SetValue(mapOwner, 3);
        object mapReadback = mapValid ? ScribeRoundTrip(gameAssembly,
            mapOwnerType, mapOwner, "cultureHistoryOwner") : null;

        object invalidMapPlan = LoadPlan();
        object invalidMapCulture = RequiredField(((IList)RequiredField(
                planType, "factions").GetValue(invalidMapPlan)!)[0]!
            .GetType(), "culture").GetValue(((IList)RequiredField(planType,
                "factions").GetValue(invalidMapPlan)!)[0])!;
        RequiredField(cultureType, "schemaVersion").SetValue(
            invalidMapCulture, 7);
        object rollbackMapOwner = MapOwner(invalidMapCulture, null);
        string rollbackMapFailure = (string)Method(mapOwnerType,
            "MigrateState", 0).Invoke(rollbackMapOwner,
                Array.Empty<object>());
        bool mapRollback = rollbackMapFailure != null
            && RequiredField(mapOwnerType, "nativeEvents")
                .GetValue(rollbackMapOwner) == null
            && ReferenceEquals(invalidMapCulture,
                RequiredField(mapOwnerType, "playerLocalCulture")
                    .GetValue(rollbackMapOwner));
        IList wrongMapEvents = NewList(eventType);
        wrongMapEvents.Add(NativeEvent(4));
        object currentMapCulture = RequiredField(mapOwnerType,
            "playerLocalCulture").GetValue(mapOwner)!;
        object wrongCurrentMapOwner = MapOwner(currentMapCulture,
            wrongMapEvents, 3, 3);
        string wrongCurrentMapFailure = (string)Method(mapOwnerType,
            "ValidateCampaignState", 0).Invoke(wrongCurrentMapOwner,
                Array.Empty<object>());
        object wrongPredecessorMapOwner = MapOwner(mapCulture,
            wrongMapEvents, 2, 3);
        string wrongPredecessorMapFailure = (string)Method(mapOwnerType,
            "MigrateState", 0).Invoke(wrongPredecessorMapOwner,
                Array.Empty<object>());
        bool wrongMapRejected = wrongCurrentMapFailure?.Contains(
                "does not match owner map", StringComparison.Ordinal) == true
            && wrongPredecessorMapFailure?.Contains(
                "does not match owner map", StringComparison.Ordinal) == true
            && ReferenceEquals(mapCulture, RequiredField(mapOwnerType,
                "playerLocalCulture").GetValue(wrongPredecessorMapOwner))
            && ReferenceEquals(wrongMapEvents, RequiredField(mapOwnerType,
                "nativeEvents").GetValue(wrongPredecessorMapOwner));
        CheckIt("Culture-history owner catalog-4 migration",
            mapValid && mapReadback != null
                && CurrentCulture(RequiredField(mapOwnerType,
                    "playerLocalCulture").GetValue(mapReadback))
                && mapRollback && wrongMapRejected,
            "schema-2 map Culture upgrades through the same registry path, "
                + "survives owner Scribe readback, and failed validation "
                + "leaves both Culture and the event ledger untouched; current and predecessor owners reject occurrence provenance belonging to another map");
    }

    private static void VerifyOwnershipDocs(string repo)
    {
        string audit = File.ReadAllText(Path.Combine(repo,
            "B16_PLAYABLE_SOCIAL_ONTOLOGY_AUDIT.md"));
        CheckIt("Canonical ownership contract",
            new[] { "Ideoligion", "Culture", "Political Order",
                    "Institutions", "Practice" }.All(value => audit.Contains(
                    value, StringComparison.Ordinal))
                && audit.Contains("native mechanic remains the executor",
                    StringComparison.OrdinalIgnoreCase),
            "five owners and native execution boundary are documented");
    }

    private static void VerifyNativeSourceRoutes(IList events, string repo,
        string decompiled, string morePrecepts, string gameData)
    {
        string[] registered = events.Cast<object>().Select(value =>
                StringField(value, "EventDefName"))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        List<NativeEmissionRoute> routes = NativeEmissionRoutes();
        string[] routed = routes.Select(value => value.EventDefName)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string[] duplicateRoutes = routes.GroupBy(value => value.EventDefName,
                StringComparer.Ordinal).Where(group => group.Count() != 1)
            .Select(group => group.Key).ToArray();
        bool exactManifest = duplicateRoutes.Length == 0
            && registered.SequenceEqual(routed, StringComparer.Ordinal);
        CheckIt("Native event emitter manifest",
            exactManifest,
            exactManifest
                ? routes.Count + " registered events each name exactly one audited emitter contract"
                : "duplicates=" + string.Join(",", duplicateRoutes)
                    + "; missing=" + string.Join(",", registered.Except(
                        routed, StringComparer.Ordinal))
                    + "; extras=" + string.Join(",", routed.Except(
                        registered, StringComparer.Ordinal)));

        var failures = new List<string>();
        foreach (NativeEmissionRoute route in routes)
        {
            string root = route.Root == "decompiled"
                ? decompiled : morePrecepts;
            string primary = Path.Combine(root, route.RelativePath);
            if (!File.Exists(primary))
            {
                failures.Add(route.EventDefName + ": missing " + primary);
                continue;
            }
            string source = File.ReadAllText(primary);
            string primaryFailure = CallableContractFailure(source,
                route.MethodName, route.RequiredTokens,
                route.ExpectedTokenOccurrences);
            if (primaryFailure != null)
                failures.Add(route.EventDefName + ": " + route.RelativePath
                    + "::" + route.MethodName + " " + primaryFailure);

            if (!string.IsNullOrWhiteSpace(route.SecondaryRelativePath))
            {
                string secondary = Path.Combine(root,
                    route.SecondaryRelativePath);
                if (!File.Exists(secondary))
                    failures.Add(route.EventDefName + ": missing "
                        + secondary);
                else
                {
                    string secondarySource = File.ReadAllText(secondary);
                    string secondaryFailure = CallableContractFailure(
                        secondarySource, route.SecondaryMethodName,
                        route.SecondaryRequiredTokens
                            ?? Array.Empty<string>(),
                        route.ExpectedSecondaryTokenOccurrences);
                    if (secondaryFailure != null)
                        failures.Add(route.EventDefName + ": "
                            + route.SecondaryRelativePath + "::"
                            + route.SecondaryMethodName + " "
                            + secondaryFailure);
                }
            }
        }

        var ateEvents = new HashSet<string>(StringComparer.Ordinal);
        foreach (string root in new[] { gameData, morePrecepts })
        {
            if (!Directory.Exists(root)) continue;
            foreach (string file in Directory.EnumerateFiles(root, "*.xml",
                         SearchOption.AllDirectories))
            {
                XDocument document;
                try { document = XDocument.Load(file); }
                catch { continue; }
                foreach (XElement value in document.Descendants().Where(
                             value => value.Name.LocalName == "ateEvent"))
                    if (!string.IsNullOrWhiteSpace(value.Value))
                        ateEvents.Add(value.Value.Trim());
            }
        }
        if (!ateEvents.Contains("AteNutrientPaste"))
            failures.Add("AteNutrientPaste: no exact ingestible ateEvent XML");

        string caSource = File.ReadAllText(Path.Combine(repo, "Source",
            "CultureNativePracticeModule.cs"));
        if (!caSource.Contains("HarmonyPatch(typeof(Tradeable_Pawn)",
                StringComparison.Ordinal)
            || !caSource.Contains("nameof(Tradeable_Pawn.ResolveTrade)",
                StringComparison.Ordinal)
            || !caSource.Contains("Begin(\"slave-sale\"",
                StringComparison.Ordinal))
            failures.Add("SoldSlave: CA lacks exact local-sale act scope");
        if (!caSource.Contains("class CANativeCultureMarriageActPatch",
                StringComparison.Ordinal)
            || !caSource.Contains("Begin(\"marriage\"",
                StringComparison.Ordinal))
            failures.Add("marriage events: CA lacks one shared native-marriage act scope");
        if (!caSource.Contains(
                "class CANativeCultureSettlementAbandonmentActPatch",
                StringComparison.Ordinal)
            || !caSource.Contains("Begin(\"settlement-abandonment\"",
                StringComparison.Ordinal))
            failures.Add("Nomadism_AbandonedSettlement: CA lacks one shared abandonment act scope");
        failures.AddRange(CallableContractGuardrailFailures().Select(value =>
            "verifier guardrail: " + value));

        CheckIt("Native event emission source evidence",
            failures.Count == 0,
            failures.Count == 0
                ? routes.GroupBy(value => value.RouteKind).OrderBy(group =>
                        group.Key, StringComparer.Ordinal)
                    .Select(group => group.Key + "=" + group.Count())
                    .Aggregate((left, right) => left + "; " + right)
                    + "; every route binds a declaring method, exact event-token count, and one containing-call or identifier-correlated local-dataflow contract; 16 positive and negative mutation guardrails reject code-hidden, prefixed, neighboring, misplaced, unrelated, and unbound evidence"
                : string.Join("; ", failures));
    }

    private static IEnumerable<string> CallableContractGuardrailFailures()
    {
        var failures = new List<string>();
        string[] direct =
        {
            "HistoryEventDefOf.Target", "RecordEvent",
            "HistoryEventArgsNames.Doer"
        };
        void Expect(string name, string source, string[] tokens,
            bool shouldPass)
        {
            bool passed = CallableContractFailure(source, "Emit", tokens, 1)
                == null;
            if (passed != shouldPass)
                failures.Add(name + " " + (shouldPass
                    ? "was rejected" : "was falsely accepted"));
        }

        Expect("valid inline call",
            "public void Emit() { RecordEvent(new HistoryEvent(HistoryEventDefOf.Target, pawn.Named(HistoryEventArgsNames.Doer))); }",
            direct, true);
        Expect("neighbor argument borrowing",
            "public void Emit() { RecordEvent(new HistoryEvent(HistoryEventDefOf.Target)); RecordEvent(new HistoryEvent(HistoryEventDefOf.Other, pawn.Named(HistoryEventArgsNames.Doer))); }",
            direct, false);
        Expect("left-prefix selector impostor",
            "public void Emit() { RecordEvent(new HistoryEvent(NotHistoryEventDefOf.Target, pawn.Named(HistoryEventArgsNames.Doer))); }",
            direct, false);
        Expect("left-prefix argument impostor",
            "public void Emit() { RecordEvent(new HistoryEvent(HistoryEventDefOf.Target, pawn.Named(NotHistoryEventArgsNames.Doer))); }",
            direct, false);
        Expect("valid selected-def inline flow",
            "public void Emit() { HistoryEventDef def = HistoryEventDefOf.Target; RecordEvent(new HistoryEvent(def, pawn.Named(HistoryEventArgsNames.Doer))); }",
            direct, true);
        Expect("wrong selected-def consumer",
            "public void Emit() { HistoryEventDef def = HistoryEventDefOf.Target; RecordEvent(new HistoryEvent(otherDef, pawn.Named(HistoryEventArgsNames.Doer))); }",
            direct, false);
        Expect("selected def used outside event-def position",
            "public void Emit() { HistoryEventDef def = HistoryEventDefOf.Target; RecordEvent(new HistoryEvent(otherDef, def.Named(HistoryEventArgsNames.Doer))); }",
            direct, false);
        Expect("valid event-local flow",
            "public void Emit() { HistoryEvent historyEvent = new HistoryEvent(HistoryEventDefOf.Target, pawn.Named(HistoryEventArgsNames.Doer)); RecordEvent(historyEvent); }",
            direct, true);
        Expect("wrong event-local consumer",
            "public void Emit() { HistoryEvent historyEvent = new HistoryEvent(HistoryEventDefOf.Target, pawn.Named(HistoryEventArgsNames.Doer)); RecordEvent(otherHistoryEvent); }",
            direct, false);
        Expect("event local used outside first argument",
            "public void Emit() { HistoryEvent historyEvent = new HistoryEvent(HistoryEventDefOf.Target, pawn.Named(HistoryEventArgsNames.Doer)); RecordEvent(otherHistoryEvent, historyEvent); }",
            direct, false);
        Expect("prefixed RecordEvent impostor",
            "public void Emit() { HistoryEvent historyEvent = new HistoryEvent(HistoryEventDefOf.Target, pawn.Named(HistoryEventArgsNames.Doer)); NotRecordEvent(historyEvent); }",
            direct, false);
        Expect("commented-out emitter",
            "public void Emit() { // RecordEvent(new HistoryEvent(HistoryEventDefOf.Target, pawn.Named(HistoryEventArgsNames.Doer)));\n }",
            direct, false);
        Expect("string-literal emitter",
            "public void Emit() { string text = \"RecordEvent(new HistoryEvent(HistoryEventDefOf.Target, pawn.Named(HistoryEventArgsNames.Doer)))\"; }",
            direct, false);
        string[] control =
        {
            "HistoryEventDefOf.SoldSlave", "TryRandomElement",
            "HistoryEventArgsNames.Doer", "RecordEvent"
        };
        Expect("valid control producer",
            "public void Emit() { if (pawns.TryRandomElement(out result)) { RecordEvent(new HistoryEvent(HistoryEventDefOf.SoldSlave, result.Named(HistoryEventArgsNames.Doer))); } }",
            control, true);
        Expect("unrelated control producer",
            "public void Emit() { if (pawns.TryRandomElement(out result)) { RecordEvent(new HistoryEvent(HistoryEventDefOf.SoldSlave, other.Named(HistoryEventArgsNames.Doer))); } }",
            control, false);
        Expect("prefixed control producer impostor",
            "public void Emit() { if (pawns.NotTryRandomElement(out result)) { RecordEvent(new HistoryEvent(HistoryEventDefOf.SoldSlave, result.Named(HistoryEventArgsNames.Doer))); } }",
            control, false);
        return failures;
    }

    private static List<NativeEmissionRoute> NativeEmissionRoutes()
    {
        var result = new List<NativeEmissionRoute>();
        void Direct(string root, string path, string method,
            params string[] names)
        {
            foreach (string name in names)
                result.Add(new NativeEmissionRoute(name, root, path,
                    method, "direct", new[]
                    {
                        "HistoryEventDefOf." + name,
                        "RecordEvent",
                        "HistoryEventArgsNames.Doer"
                    }, name == "AteVeneratedAnimalMeat" ? 2 : 1));
        }

        Direct("decompiled", "Verse/Thing.cs", "Ingested",
            "AteHumanMeat", "AteHumanMeatAsIngredient",
            "AteHumanMeatDirect", "AteInsectMeatAsIngredient",
            "AteFungus", "AteFungusAsIngredient", "AteMeat",
            "AteVeneratedAnimalMeat");
        result.Add(new NativeEmissionRoute("ButcheredHuman", "decompiled",
            "Verse/Corpse.cs", "ButcherProducts", "direct",
            new[] { "HistoryEventDefOf.ButcheredHuman", "RecordEvent",
                "HistoryEventArgsNames.Doer", "HistoryEventArgsNames.Victim" }));
        foreach (string name in new[] { "ObservedLayingCorpse",
                     "ObservedLayingRottingCorpse" })
            result.Add(new NativeEmissionRoute(name, "decompiled",
                "Verse/Corpse.cs", "GiveObservedHistoryEvent",
                "observed target", new[] { "HistoryEventDefOf." + name }, 1,
                "RimWorld/PawnObserver.cs",
                "TryCreateObservedHistoryEvent",
                new[] { "observedThoughtGiver.GiveObservedHistoryEvent",
                    "HistoryEventArgsNames.Doer",
                    "HistoryEventArgsNames.Subject", "RecordEvent" }, 1));
        Direct("decompiled", "RimWorld/ThoughtUtility.cs",
            "GiveThoughtsForPawnOrganHarvested",
            "HarvestedOrganFromColonist", "HarvestedOrganFromGuest",
            "HarvestedOrgan");
        Direct("decompiled", "RimWorld/ThoughtUtility.cs",
            "GiveThoughtsForPawnExecuted", "ExecutedColonist", "ExecutedGuest",
            "ExecutedPrisoner", "ExecutedPrisonerGuilty",
            "ExecutedPrisonerInnocent");
        Direct("decompiled", "RimWorld/Recipe_InstallArtificialBodyPart.cs",
            "ApplyOnPawn",
            "InstalledProsthetic");
        Direct("decompiled", "RimWorld/JobDriver_Lovin.cs", "MakeNewToils",
            "GotLovin_NonSpouse");
        foreach (string name in new[]
                 {
                     "GotMarried_SpouseCount_OneOrFewer",
                     "GotMarried_SpouseCount_Two",
                     "GotMarried_SpouseCount_Three",
                     "GotMarried_SpouseCount_Four",
                     "GotMarried_SpouseCount_FiveOrMore"
                 })
            result.Add(new NativeEmissionRoute(name, "decompiled",
                "RimWorld/SpouseRelationUtility.cs",
                "GetHistoryEventForSpouseCount", "selected native event",
                new[] { "HistoryEventDefOf." + name }, 1,
                "RimWorld/MarriageCeremonyUtility.cs", "Married",
                new[] { "GetHistoryEventForSpouseCount", "RecordEvent",
                    "HistoryEventArgsNames.Doer" }, 2));
        Direct("decompiled", "RimWorld/CompDrug.cs", "PostIngested",
            "IngestedRecreationalDrug");
        Direct("decompiled", "RimWorld/Recipe_AdministerIngestible.cs",
            "ApplyOnPawn",
            "AdministeredRecreationalDrug");
        result.Add(new NativeEmissionRoute("AteNutrientPaste", "decompiled",
            "Verse/Thing.cs", "Ingested", "ingestible definition",
            new[] { "new HistoryEvent(def.ingestible.ateEvent", "RecordEvent",
                "HistoryEventArgsNames.Doer" }));
        Direct("decompiled", "RimWorld/Plant.cs", "PlantCollected", "CutTree");
        Direct("decompiled", "RimWorld/Mineable.cs", "DestroyMined", "Mined");
        result.Add(new NativeEmissionRoute("SowedPlant", "decompiled",
            "RimWorld/CompPlantable.cs", "DoPlant", "direct",
            new[] { "HistoryEventDefOf.SowedPlant", "RecordEvent",
                "HistoryEventArgsNames.Doer" }, 1,
            "RimWorld/JobDriver_PlantSow.cs", "MakeNewToils",
            new[] { "HistoryEventDefOf.SowedPlant", "RecordEvent",
                "HistoryEventArgsNames.Doer" }, 1));
        result.Add(new NativeEmissionRoute("SoldSlave", "decompiled",
            "RimWorld/Tradeable_Pawn.cs", "ResolveTrade",
            "locally scoped direct",
            new[] { "HistoryEventDefOf.SoldSlave", "TradeSession.playerNegotiator",
                "HistoryEventArgsNames.Doer", "RecordEvent" }, 1,
            "RimWorld/Planet/TransportersArrivalAction_GiveGift.cs",
            "Arrived",
            new[] { "HistoryEventDefOf.SoldSlave", "TryRandomElement",
                "HistoryEventArgsNames.Doer", "RecordEvent" }, 1));
        Direct("decompiled", "RimWorld/JobDriver_Blind.cs",
            "CreateHistoryEventDef", "GotBlinded");
        Direct("decompiled", "RimWorld/JobDriver_Scarify.cs",
            "CreateHistoryEventDef", "GotScarified");
        Direct("decompiled", "RimWorld/CompBiosculpterPod.cs",
            "CycleCompleted",
            "UsedBiosculpterPod");
        Direct("decompiled", "RimWorld/Recipe_InstallNaturalBodyPart.cs",
            "ApplyOnPawn",
            "InstalledOrgan");
        Direct("decompiled", "RimWorld/TradeDeal.cs", "TryExecute", "TradedOrgan",
            "SoldOrgan");
        Direct("decompiled", "RimWorld/Pawn_IdeoTracker.cs", "SetIdeo",
            "ChangedIdeo");
        Direct("decompiled", "RimWorld/Pawn_IdeoTracker.cs",
            "IdeoConversionAttempt", "ConvertedNewMember");
        Direct("decompiled", "RimWorld/LordJob_Ritual.cs",
            "AddParticipantThoughts",
            "ParticipatedInOthersRitual");
        Direct("decompiled", "RimWorld/ExecutionUtility.cs", "ExecutionInt",
            "SlaughteredAnimal");
        result.Add(new NativeEmissionRoute("KilledInnocentAnimal", "decompiled",
            "Verse/Pawn.cs", "DoKillSideEffects", "direct",
            new[] { "HistoryEventDefOf.KilledInnocentAnimal", "RecordEvent",
                "HistoryEventArgsNames.Doer", "HistoryEventArgsNames.Victim" }));
        Direct("decompiled", "RimWorld/IdeoUtility.cs",
            "Notify_PlayerRaidedSomeone", "Raided");
        Direct("decompiled", "RimWorld/GenGuest.cs", "TryEnslavePrisoner",
            "EnslavedPrisoner",
            "EnslavedPrisonerNotPreviouslyEnslaved");
        Direct("decompiled", "RimWorld/JobDriver_Fish.cs",
            "CompleteFishingToil",
            "SlaughteredFish");
        Direct("more-precepts", "Source/Alcohol.cs", "PostIngested_Hook",
            "IngestedAlcohol");
        result.Add(new NativeEmissionRoute("AdministeredAlcohol",
            "more-precepts", "Source/Alcohol.cs", "ApplyOnPawn", "direct",
            new[] { "HistoryEventDefOf.AdministeredAlcohol", "RecordEvent",
                "HistoryEventArgsNames.Doer", "HistoryEventArgsNames.Victim" }));
        result.Add(new NativeEmissionRoute("Nomadism_AbandonedSettlement",
            "more-precepts", "Source/Nomadism.cs", "Abandon",
            "participant event in one settlement abandonment",
            new[] { "HistoryEventDefOf.Nomadism_AbandonedSettlement",
                "RecordEvent", "HistoryEventArgsNames.Doer" }));
        result.Add(new NativeEmissionRoute(
            "TakingFromDowned_DownedStripped", "more-precepts",
            "Source/TakingFromDowned.cs", "MakeNewToils_Hook", "direct",
            new[] { "HistoryEventDefOf.TakingFromDowned_DownedStripped",
                "RecordEvent", "HistoryEventArgsNames.Doer",
                "HistoryEventArgsNames.Victim" }));
        foreach (string name in new[] { "Violence_AttackedPerson",
                     "Violence_AttackedNonHostilePerson" })
            result.Add(new NativeEmissionRoute(name, "more-precepts",
                "Source/Violence.cs", "PreApplyDamage", "direct",
                new[] { "HistoryEventDefOf." + name, "RecordEvent",
                    "HistoryEventArgsNames.Doer",
                    "HistoryEventArgsNames.Victim" }));
        return result;
    }

    private static string CallableContractFailure(string source,
        string methodName, string[] requiredTokens,
        int expectedTokenOccurrences)
    {
        if (string.IsNullOrWhiteSpace(methodName))
            return "has no declaring method";
        if (requiredTokens == null || requiredTokens.Length == 0)
            return "has no argument contract";
        List<string> bodies = CallableBodies(source, methodName).ToList();
        if (bodies.Count == 0)
            return "declaring method was not found";
        foreach (string body in bodies)
        {
            int occurrenceCount = CountToken(body, requiredTokens[0]);
            if (occurrenceCount != expectedTokenOccurrences) continue;
            if (!requiredTokens.Contains("RecordEvent",
                    StringComparer.Ordinal))
            {
                if (requiredTokens.All(token => body.Contains(token,
                        StringComparison.Ordinal)))
                    return null;
                continue;
            }

            int[] selectors = TokenPositions(body, requiredTokens[0])
                .ToArray();
            bool allBound = selectors.Length == expectedTokenOccurrences
                && selectors.All(selector => EventLocalEmissionWindow(
                    body, selector, requiredTokens) != null);
            if (allBound) return null;
        }
        return "does not contain one coherent event-local contract for `"
            + string.Join("` + `", requiredTokens) + "` with "
            + expectedTokenOccurrences + " exact selector occurrence(s), "
            + "each bound to its containing RecordEvent call or one unambiguous local dataflow into RecordEvent";
    }

    private static IEnumerable<int> TokenPositions(string source,
        string token)
    {
        string code = CodeMask(source);
        int start = 0;
        while (start <= code.Length - token.Length)
        {
            int found = code.IndexOf(token, start,
                StringComparison.Ordinal);
            if (found < 0) yield break;
            int after = found + token.Length;
            bool leftBoundary = found == 0
                || !(char.IsLetterOrDigit(code[found - 1])
                    || code[found - 1] == '_');
            if (leftBoundary && (after >= code.Length
                || !(char.IsLetterOrDigit(code[after])
                    || code[after] == '_')))
                yield return found;
            start = found + token.Length;
        }
    }

    private static string EventLocalEmissionWindow(string body,
        int selector, IReadOnlyList<string> requiredTokens)
    {
        // Most native emitters construct the HistoryEvent inline, so the
        // selector follows the RecordEvent token. Bind it to the invocation
        // which actually contains it instead of borrowing a later call in the
        // same method.
        foreach (int record in TokenPositions(body, "RecordEvent"))
        {
            int open = body.IndexOf('(', record + "RecordEvent".Length);
            int close = MatchingParenthesis(body, open);
            if (open < 0 || close < open || selector < record
                || selector > close)
                continue;
            string direct = body.Substring(record, close - record + 1);
            if (ValidEventLocalWindow(direct, requiredTokens))
                return direct;

            // An argument may be sourced by the immediately enclosing control
            // expression (for example, TryRandomElement(out result) followed
            // by result.Named(Doer)). Keep that evidence local to this branch;
            // the exact-identity check below rejects a neighboring emitter.
            string control = ImmediateControlWindow(body, record, close);
            if (control != null
                && ValidEventLocalWindow(control, requiredTokens)
                && ControlProducerFeedsEventArgument(control, direct))
                return control;
            return null;
        }

        // A small number of native routes construct a HistoryEvent in a local
        // variable before submitting it. Permit that ordered dataflow only to
        // the first following RecordEvent, and reject any intervening event
        // identity so one emitter cannot satisfy another emitter's contract.
        int following = -1;
        foreach (int candidate in TokenPositions(body, "RecordEvent"))
        {
            if (candidate <= selector) continue;
            following = candidate;
            break;
        }
        if (following < 0) return null;
        int followingOpen = body.IndexOf('(',
            following + "RecordEvent".Length);
        int followingClose = MatchingParenthesis(body, followingOpen);
        if (followingOpen < 0 || followingClose < followingOpen) return null;
        return BoundLocalDataflowWindow(body, selector, following,
            followingOpen, followingClose, requiredTokens);
    }

    private static bool ControlProducerFeedsEventArgument(string control,
        string eventCall)
    {
        Match producer = Regex.Match(CodeMask(control),
            @"(?<![A-Za-z0-9_])TryRandomElement\s*\(\s*out\s+(?:var\s+)?"
            + @"(?<value>[A-Za-z_][A-Za-z0-9_]*)\s*\)");
        if (!producer.Success) return false;
        string value = producer.Groups["value"].Value;
        return Regex.IsMatch(eventCall,
            @"(?<![A-Za-z0-9_])" + Regex.Escape(value)
            + @"\.Named\s*\(\s*HistoryEventArgsNames\.Doer\s*\)");
    }

    private static string BoundLocalDataflowWindow(string body, int selector,
        int record, int recordOpen, int recordClose,
        IReadOnlyList<string> requiredTokens)
    {
        string code = CodeMask(body);
        int producerEnd = code.IndexOf(';', selector);
        if (producerEnd < 0 || producerEnd >= record) return null;
        int producerStart = StatementStart(body, selector);
        string producerStatement = body.Substring(producerStart,
            producerEnd - producerStart + 1);
        string producer = AssignedIdentifier(producerStatement);
        if (producer == null) return null;

        string recordArguments = body.Substring(recordOpen + 1,
            recordClose - recordOpen - 1);
        IEnumerable<string> constructorTokens = requiredTokens.Where(token =>
            token != requiredTokens[0]
            && !token.Contains("RecordEvent", StringComparison.Ordinal));

        // One-step flow: the selector is inside a HistoryEvent assigned to a
        // local which is submitted by this exact RecordEvent invocation.
        if (producerStatement.Contains("new HistoryEvent",
                StringComparison.Ordinal)
            && constructorTokens.All(token => HasExactToken(
                producerStatement, token))
            && FirstArgumentIsIdentifier(recordArguments, producer))
        {
            string oneStep = body.Substring(selector,
                recordClose - selector + 1);
            return ValidEventLocalWindow(oneStep, requiredTokens)
                ? oneStep : null;
        }

        // Two-step flow: a selected HistoryEventDef is consumed by one local
        // or inline HistoryEvent, and that same event is submitted. This proves
        // both producer-to-constructor and constructor-to-RecordEvent identity.
        int construction = -1;
        foreach (int candidate in TokenPositions(body, "new HistoryEvent"))
        {
            if (candidate <= producerEnd) continue;
            construction = candidate;
            break;
        }
        if (construction < 0 || construction >= recordClose) return null;
        int constructionOpen = body.IndexOf('(',
            construction + "new HistoryEvent".Length);
        int constructionClose = MatchingParenthesis(body, constructionOpen);
        if (constructionOpen < 0 || constructionClose < constructionOpen
            || constructionClose > recordClose)
            return null;
        string constructor = body.Substring(construction,
            constructionClose - construction + 1);
        string constructorArguments = body.Substring(constructionOpen + 1,
            constructionClose - constructionOpen - 1);
        if (!FirstArgumentIsIdentifier(constructorArguments, producer)
            || !constructorTokens.All(token => HasExactToken(constructor,
                token)))
            return null;

        string twoStep = body.Substring(selector,
            recordClose - selector + 1);
        if (construction >= recordOpen)
            return ValidEventLocalWindow(twoStep, requiredTokens)
                ? twoStep : null;

        int eventStatementStart = StatementStart(body, construction);
        int eventStatementEnd = code.IndexOf(';', constructionClose);
        if (eventStatementEnd < 0 || eventStatementEnd >= record) return null;
        string eventStatement = body.Substring(eventStatementStart,
            eventStatementEnd - eventStatementStart + 1);
        string eventLocal = AssignedIdentifier(eventStatement);
        if (eventLocal == null
            || !FirstArgumentIsIdentifier(recordArguments, eventLocal))
            return null;

        return ValidEventLocalWindow(twoStep, requiredTokens)
            ? twoStep : null;
    }

    private static int StatementStart(string source, int position)
    {
        string code = CodeMask(source);
        int semicolon = code.LastIndexOf(';', position);
        int open = code.LastIndexOf('{', position);
        int close = code.LastIndexOf('}', position);
        return Math.Max(semicolon, Math.Max(open, close)) + 1;
    }

    private static string AssignedIdentifier(string statement)
    {
        Match assignment = Regex.Match(CodeMask(statement),
            @"(?:^|[\s{])(?:var|[A-Za-z_][A-Za-z0-9_<>,.?\[\]]*)\s+"
            + @"(?<value>[A-Za-z_][A-Za-z0-9_]*)\s*=");
        return assignment.Success ? assignment.Groups["value"].Value : null;
    }

    private static bool FirstArgumentIsIdentifier(string arguments,
        string identifier)
    {
        return Regex.IsMatch(CodeMask(arguments),
            @"^\s*" + Regex.Escape(identifier)
            + @"\s*(?:,|$)");
    }

    private static bool HasExactToken(string source, string token)
    {
        return CountToken(source, token) > 0;
    }

    private static string ImmediateControlWindow(string body, int record,
        int callClose)
    {
        string code = CodeMask(body);
        int blockOpen = code.LastIndexOf('{', record);
        if (blockOpen < 0) return null;
        int start = body.LastIndexOf('\n', blockOpen);
        start = start < 0 ? 0 : start + 1;
        if (string.IsNullOrWhiteSpace(body.Substring(start,
                blockOpen - start)))
        {
            int previous = start - 2;
            while (previous >= 0
                && (body[previous] == '\r' || body[previous] == '\n'))
                previous--;
            if (previous >= 0)
            {
                int previousLine = body.LastIndexOf('\n', previous);
                start = previousLine < 0 ? 0 : previousLine + 1;
            }
        }
        return callClose >= start
            ? body.Substring(start, callClose - start + 1)
            : null;
    }

    private static bool ValidEventLocalWindow(string window,
        IReadOnlyList<string> requiredTokens)
    {
        if (!requiredTokens.All(token => HasExactToken(window, token)))
            return false;

        // A removed emitter must not be allowed to borrow a later, unrelated
        // RecordEvent call in a method which emits several native events.
        // Literal HistoryEventDef selectors therefore admit no competing exact
        // event identity inside their call or ordered local-dataflow window.
        string selectorToken = requiredTokens[0];
        if (selectorToken.StartsWith("HistoryEventDefOf.",
                StringComparison.Ordinal))
        {
            Match[] identities = Regex.Matches(CodeMask(window),
                    @"HistoryEventDefOf\.[A-Za-z0-9_]+")
                .Cast<Match>().ToArray();
            if (identities.Length != 1
                || identities[0].Value != selectorToken)
                return false;
        }
        return true;
    }

    private static int MatchingParenthesis(string source, int open)
    {
        if (open < 0 || open >= source.Length || source[open] != '(')
            return -1;
        string code = CodeMask(source);
        int depth = 0;
        for (int index = open; index < code.Length; index++)
        {
            char current = code[index];
            if (current == '(') depth++;
            else if (current == ')' && --depth == 0) return index;
        }
        return -1;
    }

    private static IEnumerable<string> CallableBodies(string source,
        string methodName)
    {
        string code = CodeMask(source);
        string pattern = @"(?m)^[\t ]*(?:(?:public|private|protected|internal|"
            + @"static|virtual|override|sealed|async|extern|unsafe|partial|new|"
            + @"abstract)\s+)*(?:[A-Za-z_][A-Za-z0-9_<>\[\],.?]*\s+)+"
            + Regex.Escape(methodName)
            + @"\s*\([^;{}]*\)\s*(?:where[^\r\n{]+)?\s*\{";
        foreach (Match match in Regex.Matches(code, pattern))
        {
            int open = code.IndexOf('{', match.Index);
            int close = MatchingBrace(source, open);
            if (open >= 0 && close > open)
                yield return source.Substring(open, close - open + 1);
        }
    }

    private static int MatchingBrace(string source, int open)
    {
        if (open < 0 || open >= source.Length || source[open] != '{')
            return -1;
        int depth = 0;
        bool lineComment = false;
        bool blockComment = false;
        bool text = false;
        bool character = false;
        bool verbatim = false;
        bool escaped = false;
        for (int index = open; index < source.Length; index++)
        {
            char current = source[index];
            char next = index + 1 < source.Length ? source[index + 1] : '\0';
            if (lineComment)
            {
                if (current == '\n') lineComment = false;
                continue;
            }
            if (blockComment)
            {
                if (current == '*' && next == '/')
                {
                    blockComment = false;
                    index++;
                }
                continue;
            }
            if (text)
            {
                if (verbatim && current == '"' && next == '"')
                {
                    index++;
                    continue;
                }
                if (current == '"' && (verbatim || !escaped))
                {
                    text = false;
                    verbatim = false;
                }
                escaped = !verbatim && current == '\\' && !escaped;
                if (current != '\\') escaped = false;
                continue;
            }
            if (character)
            {
                if (current == '\'' && !escaped) character = false;
                escaped = current == '\\' && !escaped;
                if (current != '\\') escaped = false;
                continue;
            }
            if (current == '/' && next == '/')
            {
                lineComment = true;
                index++;
                continue;
            }
            if (current == '/' && next == '*')
            {
                blockComment = true;
                index++;
                continue;
            }
            if (current == '"')
            {
                text = true;
                verbatim = index > 0 && source[index - 1] == '@';
                continue;
            }
            if (current == '\'')
            {
                character = true;
                continue;
            }
            if (current == '{') depth++;
            else if (current == '}' && --depth == 0) return index;
        }
        return -1;
    }

    private static int CountToken(string source, string token)
    {
        return Regex.Matches(CodeMask(source),
            @"(?<![A-Za-z0-9_])" + Regex.Escape(token)
            + @"(?![A-Za-z0-9_])").Count;
    }

    private static string CodeMask(string source)
    {
        char[] masked = source.ToCharArray();
        bool lineComment = false;
        bool blockComment = false;
        bool text = false;
        bool character = false;
        bool verbatim = false;
        bool escaped = false;
        for (int index = 0; index < source.Length; index++)
        {
            char current = source[index];
            char next = index + 1 < source.Length ? source[index + 1] : '\0';
            if (lineComment)
            {
                if (current == '\r' || current == '\n')
                    lineComment = false;
                else
                    masked[index] = ' ';
                continue;
            }
            if (blockComment)
            {
                if (current != '\r' && current != '\n') masked[index] = ' ';
                if (current == '*' && next == '/')
                {
                    if (index + 1 < masked.Length) masked[index + 1] = ' ';
                    blockComment = false;
                    index++;
                }
                continue;
            }
            if (text)
            {
                if (current != '\r' && current != '\n') masked[index] = ' ';
                if (verbatim && current == '"' && next == '"')
                {
                    masked[index + 1] = ' ';
                    index++;
                    continue;
                }
                if (current == '"' && (verbatim || !escaped))
                {
                    text = false;
                    verbatim = false;
                }
                escaped = !verbatim && current == '\\' && !escaped;
                if (current != '\\') escaped = false;
                continue;
            }
            if (character)
            {
                if (current != '\r' && current != '\n') masked[index] = ' ';
                if (current == '\'' && !escaped) character = false;
                escaped = current == '\\' && !escaped;
                if (current != '\\') escaped = false;
                continue;
            }
            if (current == '/' && next == '/')
            {
                masked[index] = masked[index + 1] = ' ';
                lineComment = true;
                index++;
                continue;
            }
            if (current == '/' && next == '*')
            {
                masked[index] = masked[index + 1] = ' ';
                blockComment = true;
                index++;
                continue;
            }
            if (current == '@' && next == '"')
            {
                masked[index] = masked[index + 1] = ' ';
                text = true;
                verbatim = true;
                index++;
                continue;
            }
            if (current == '"')
            {
                masked[index] = ' ';
                text = true;
                escaped = false;
                continue;
            }
            if (current == '\'')
            {
                masked[index] = ' ';
                character = true;
                escaped = false;
            }
        }
        return new string(masked);
    }

    private static string ReadPackageId(string modRoot)
    {
        string about = Path.Combine(modRoot, "About", "About.xml");
        if (!File.Exists(about)) return "missing";
        return XDocument.Load(about).Descendants().FirstOrDefault(value =>
            value.Name.LocalName == "packageId")?.Value?.Trim() ?? "missing";
    }

    private static string GitHead(string root)
    {
        var start = new ProcessStartInfo("git",
            "-C \"" + root + "\" rev-parse HEAD")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using Process process = Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return process.ExitCode == 0 ? output : "unresolved";
    }

    private static List<NativeDef> ReadDefs(string root, string package,
        string label)
    {
        var result = new List<NativeDef>();
        if (!Directory.Exists(root)) return result;
        foreach (string file in Directory.EnumerateFiles(root, "*.xml",
                     SearchOption.AllDirectories))
        {
            XDocument document;
            try { document = XDocument.Load(file); }
            catch { continue; }
            foreach (XElement definition in document.Descendants().Where(value =>
                value.Name.LocalName is "PreceptDef" or "MemeDef"
                    or "HistoryEventDef"))
            {
                string name = Value(definition, "defName");
                if (name.Length == 0) continue;
                string kind = definition.Name.LocalName switch
                {
                    "PreceptDef" => "Precept",
                    "MemeDef" => "Meme",
                    _ => "HistoryEvent"
                };
                result.Add(new NativeDef(package, label, kind, name,
                    Value(definition, "issue")));
            }
        }
        return result.GroupBy(value => value.PackageId + "|" + value.Kind
                + "|" + value.DefName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()).ToList();
    }

    private static void WriteAcceptance(string path, string dll, string hash,
        string moreCommit)
    {
        var text = Header("B16 Ideoligion and Culture Acceptance Receipt",
            dll, hash).AppendLine("More Precepts source commit: `" + moreCommit
                + "`").AppendLine().AppendLine("| Check | Result | Evidence |")
            .AppendLine("|---|---|---|");
        foreach (Check check in Checks)
            text.AppendLine("| " + Escape(check.Name) + " | **"
                + (check.Passed ? "PASS" : "FAIL") + "** | "
                + Escape(check.Evidence) + " |");
        text.AppendLine().AppendLine("This is executable/static evidence. "
            + "Operator visual and gameplay acceptance remains the runtime boundary.");
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private sealed record NativeFamilyDisposition(string Disposition,
        string Rationale);

    private static List<FamilyAdjudication> AuditFamilies(
        List<NativeDef> corpus, IList semantic)
    {
        var mapped = semantic.Cast<object>().GroupBy(SemanticIdentity,
                StringComparer.OrdinalIgnoreCase).ToDictionary(
                group => group.Key,
                group => group.Select(value => StringField(value,
                        "QuestionKey")).Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        Dictionary<string, NativeFamilyDisposition> native =
            NativeFamilyAdjudications();
        Dictionary<string, NativeFamilyDisposition> exactNative =
            NativeDefinitionAdjudications();
        var result = new List<FamilyAdjudication>();
        foreach (IGrouping<string, NativeDef> group in corpus.Where(value =>
                     value.Kind is "Precept" or "Meme").GroupBy(value =>
                     FamilyIdentity(value), StringComparer.Ordinal)
                 .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            NativeDef first = group.First();
            string family = FamilyName(first);
            NativeDef[] definitions = group.ToArray();
            NativeDef[] adapted = definitions.Where(value =>
                mapped.ContainsKey(NativeDefinitionIdentity(value))).ToArray();
            string[] questions = adapted.SelectMany(value => mapped[
                    NativeDefinitionIdentity(value)])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            NativeDef[] unresolved = definitions.Where(value =>
                !mapped.ContainsKey(NativeDefinitionIdentity(value))
                && !exactNative.ContainsKey(NativeDefinitionIdentity(value))
                && !native.ContainsKey(group.Key)).ToArray();
            if (unresolved.Length > 0)
            {
                result.Add(new FamilyAdjudication(first.PackageId, family,
                    definitions.Length, adapted.Length, questions,
                    "Unadjudicated",
                    "No exact Culture adapter or explicit native-owner decision exists for "
                    + string.Join(", ", unresolved.Select(
                        NativeDefinitionIdentity)) + "."));
                continue;
            }

            NativeFamilyDisposition[] nativeRows = definitions.Where(value =>
                    !mapped.ContainsKey(NativeDefinitionIdentity(value)))
                .Select(value => exactNative.TryGetValue(
                        NativeDefinitionIdentity(value), out
                        NativeFamilyDisposition exactRow)
                    ? exactRow : native[group.Key]).ToArray();
            if (adapted.Length > 0)
            {
                result.Add(new FamilyAdjudication(first.PackageId, family,
                    definitions.Length, adapted.Length, questions,
                    nativeRows.Length == 0 ? "Culture appraisal"
                        : "Culture appraisal + Ideoligion executor",
                    "Exact package, kind, and definition adapters project "
                    + string.Join(", ", questions)
                    + "; native Ideoligion remains the executor."
                    + (nativeRows.Length == 0 ? "" : " "
                        + string.Join(" ", nativeRows.Select(value =>
                                value.Rationale)
                            .Distinct(StringComparer.Ordinal)))));
                continue;
            }

            string[] dispositions = nativeRows.Select(value =>
                    value.Disposition).Distinct(StringComparer.Ordinal)
                .ToArray();
            result.Add(new FamilyAdjudication(first.PackageId, family,
                definitions.Length, 0, Array.Empty<string>(),
                dispositions.Length == 1 ? dispositions[0]
                    : "Explicit native ownership",
                string.Join(" ", nativeRows.Select(value => value.Rationale)
                    .Distinct(StringComparer.Ordinal))));
        }
        return result;
    }

    private static Dictionary<string, NativeFamilyDisposition>
        NativeDefinitionAdjudications()
    {
        var result = new Dictionary<string, NativeFamilyDisposition>(
            StringComparer.OrdinalIgnoreCase);
        AddDisposition(result, "Ideoligion executor",
            "AnimalVenerated refers to a selected animal target; it cannot establish the population's global moral standing for every animal without losing scope.",
            "ludeon.rimworld.ideology|Precept|AnimalVenerated");
        AddDisposition(result, "Ideoligion executor",
            "Fishing doctrine prescribes or condemns the specific conduct of fishing; it does not state general moral protection owed to animals, so native doctrine remains authoritative.",
            "ludeon.rimworld.odyssey|Precept|Fishing_Disapproved",
            "ludeon.rimworld.odyssey|Precept|Fishing_Prohibited",
            "ludeon.rimworld.odyssey|Precept|Fishing_Sacred");
        return result;
    }

    private static Dictionary<string, NativeFamilyDisposition>
        NativeFamilyAdjudications()
    {
        var result = new Dictionary<string, NativeFamilyDisposition>(
            StringComparer.Ordinal);
        AddDisposition(result, "Political or institutional rule",
            "This is an explicit possession and trade rule, not evidence of personal drug use; Ideoligion records the prescription until an institution enacts it.",
            "llunak.MorePrecepts|DrugPossession");
        AddDisposition(result, "Ideoligion executor",
            "This specifies who must not be left incapacitated to die. It does not establish treatment of captives generally; an exact care-for-incapacitated Culture construct does not yet exist.",
            "llunak.MorePrecepts|Compassion");
        AddDisposition(result, "Ideoligion executor",
            "This specifies preference for shared or private sleeping space. It does not establish material comfort generally; an exact housing-privacy Culture construct does not yet exist.",
            "llunak.MorePrecepts|Barracks");
        AddDisposition(result, "Ideoligion executor",
            "This condemns human-animal bonding, not animal welfare or moral protection generally; an exact bonding Culture construct does not yet exist.",
            "ludeon.rimworld.ideology|Bonding");
        AddDisposition(result, "Ideoligion executor",
            "This reveres or reviles bloodfeeders as a particular inherited group. The current broad xenotype-hierarchy scalar cannot preserve which group is ranked or in which direction.",
            "ludeon.rimworld.biotech|Bloodfeeders");
        AddDisposition(result, "Ideoligion executor",
            "This definition is a specific ritual, sacred object, role, symbolic structure, or prescribed observance. RimWorld executes it; a second Culture scalar would duplicate doctrine.",
            "llunak.MorePrecepts|Ritual",
            "llunak.MorePrecepts|standalone: FuneralPyre",
            "llunak.MorePrecepts|standalone: FuneralPyreNoCorpse",
            "llunak.MorePrecepts|standalone: MP_DrinkingParty",
            "llunak.MorePrecepts|standalone: MP_Feast",
            "ludeon.rimworld.biotech|Ritual",
            "ludeon.rimworld.ideology|IdeoBuilding",
            "ludeon.rimworld.ideology|IdeoRelic",
            "ludeon.rimworld.ideology|IdeoRitualSeat",
            "ludeon.rimworld.ideology|Ritual",
            "ludeon.rimworld.ideology|standalone: Classic_DanceParty",
            "ludeon.rimworld.ideology|standalone: Classic_DrumParty",
            "ludeon.rimworld.ideology|standalone: DateRitualConsumable",
            "ludeon.rimworld.ideology|standalone: Festival",
            "ludeon.rimworld.ideology|standalone: Funeral",
            "ludeon.rimworld.ideology|standalone: FuneralNoCorpse",
            "ludeon.rimworld.ideology|standalone: IdeoRole_AnimalsSpecialist",
            "ludeon.rimworld.ideology|standalone: IdeoRole_Leader",
            "ludeon.rimworld.ideology|standalone: IdeoRole_MedicalSpecialist",
            "ludeon.rimworld.ideology|standalone: IdeoRole_MeleeSpecialist",
            "ludeon.rimworld.ideology|standalone: IdeoRole_MiningSpecialist",
            "ludeon.rimworld.ideology|standalone: IdeoRole_Moralist",
            "ludeon.rimworld.ideology|standalone: IdeoRole_PlantSpecialist",
            "ludeon.rimworld.ideology|standalone: IdeoRole_ProductionSpecialist",
            "ludeon.rimworld.ideology|standalone: IdeoRole_ResearchSpecialist",
            "ludeon.rimworld.ideology|standalone: IdeoRole_ShootingSpecialist",
            "ludeon.rimworld.ideology|standalone: LeaderSpeech",
            "ludeon.rimworld.ideology|standalone: Structure_Animist",
            "ludeon.rimworld.ideology|standalone: Structure_Archist",
            "ludeon.rimworld.ideology|standalone: Structure_Ideological",
            "ludeon.rimworld.ideology|standalone: Structure_OriginBuddhist",
            "ludeon.rimworld.ideology|standalone: Structure_OriginChristian",
            "ludeon.rimworld.ideology|standalone: Structure_OriginHindu",
            "ludeon.rimworld.ideology|standalone: Structure_OriginIslamic",
            "ludeon.rimworld.ideology|standalone: Structure_TheistAbstract",
            "ludeon.rimworld.ideology|standalone: Structure_TheistEmbodied",
            "ludeon.rimworld.ideology|standalone: Trial",
            "ludeon.rimworld.ideology|standalone: TrialMentalState",
            "ludeon.rimworld.ideology|standalone: TrialPrisoner",
            "ludeon.rimworld.odyssey|Ritual",
            "ludeon.rimworld.royalty|Ritual",
            "ludeon.rimworld.royalty|standalone: ThroneSpeech");
        AddDisposition(result, "Ideoligion executor",
            "This is a concrete doctrinal preference for dress, habitat, environment, weapon, or omen interpretation. No current Culture question states the same fact exactly, so native doctrine remains authoritative instead of using a proxy axis.",
            "llunak.MorePrecepts|Superstition",
            "llunak.MorePrecepts|Traps",
            "ludeon.rimworld.ideology|ApparelDesire",
            "ludeon.rimworld.ideology|AutonomousWeapons",
            "ludeon.rimworld.ideology|Eclipse",
            "ludeon.rimworld.ideology|Indoors",
            "ludeon.rimworld.ideology|Lighting",
            "ludeon.rimworld.ideology|Temperature",
            "ludeon.rimworld.ideology|Trees",
            "ludeon.rimworld.ideology|Weapons",
            "ludeon.rimworld.odyssey|SpaceHabitat",
            "ludeon.rimworld.biotech|GrowthVat");
        AddDisposition(result, "Native capability",
            "This definition changes a native ability, rate, yield, or utility. It is not a population appraisal; the native mechanic remains the capability authority.",
            "ludeon.rimworld.ideology|Biosculpting",
            "ludeon.rimworld.ideology|BlindPsysense",
            "ludeon.rimworld.ideology|DarknessCombat",
            "ludeon.rimworld.ideology|GauranlenConnection",
            "ludeon.rimworld.ideology|MiningYield",
            "ludeon.rimworld.ideology|NeuralSupercharge",
            "ludeon.rimworld.ideology|Research",
            "ludeon.rimworld.ideology|SleepAccelerator",
            "ludeon.rimworld.ideology|WorkDrive");
        AddDisposition(result, "Doctrine bundle",
            "This meme composes or weights native precepts. Adapting both the meme and its resulting precepts would count the same doctrine twice.",
            "llunak.MorePrecepts|standalone: Nomadism",
            "llunak.MorePrecepts|standalone: Pacifism",
            "ludeon.rimworld.biotech|standalone: Bloodfeeding",
            "ludeon.rimworld.ideology|standalone: AnimalPersonhood",
            "ludeon.rimworld.ideology|standalone: Blindsight",
            "ludeon.rimworld.ideology|standalone: Cannibal",
            "ludeon.rimworld.ideology|standalone: Collectivist",
            "ludeon.rimworld.ideology|standalone: Darkness",
            "ludeon.rimworld.ideology|standalone: FleshPurity",
            "ludeon.rimworld.ideology|standalone: Guilty",
            "ludeon.rimworld.ideology|standalone: HighLife",
            "ludeon.rimworld.ideology|standalone: HumanPrimacy",
            "ludeon.rimworld.ideology|standalone: Individualist",
            "ludeon.rimworld.ideology|standalone: Loyalist",
            "ludeon.rimworld.ideology|standalone: NaturePrimacy",
            "ludeon.rimworld.ideology|standalone: Nudism",
            "ludeon.rimworld.ideology|standalone: PainIsVirtue",
            "ludeon.rimworld.ideology|standalone: Proselytizer",
            "ludeon.rimworld.ideology|standalone: Proselytizing_Frequently",
            "ludeon.rimworld.ideology|standalone: Proselytizing_Occasionally",
            "ludeon.rimworld.ideology|standalone: Proselytizing_Sometimes",
            "ludeon.rimworld.ideology|standalone: Raider",
            "ludeon.rimworld.ideology|standalone: Rancher",
            "ludeon.rimworld.ideology|standalone: Supremacist",
            "ludeon.rimworld.ideology|standalone: Transhumanist",
            "ludeon.rimworld.ideology|standalone: TreeConnection",
            "ludeon.rimworld.ideology|standalone: Tunneler",
            "ludeon.rimworld.odyssey|standalone: Shipborn");
        return result;
    }

    private static Dictionary<string, NativeFamilyDisposition>
        NativeEventAdjudications()
    {
        var result = new Dictionary<string, NativeFamilyDisposition>(
            StringComparer.OrdinalIgnoreCase);

        AddEventDisposition(result, "ludeon.rimworld",
            "Native permission or doctrine trigger",
            "The definition is used to ask whether an act is allowed or to apply native doctrine, not as one attributable completed-practice record; Culture does not turn a permission check into history.",
            "AteInsectMeatDirect", "BuildSpecificDef",
            "GotMarried_KeptName", "GotMarried_TookMansName",
            "GotMarried_TookWomansName", "InitiatedLovin", "Researching",
            "UsedForbiddenThing", "UsedHarmfulAbility", "UsedHarmfulItem");
        AddEventDisposition(result, "ludeon.rimworld",
            "Context too broad for the Culture construct",
            "These native drug events combine medical, compulsory, and recreational contexts. Culture observes the exact recreational and alcohol events instead of guessing intent from the broad event.",
            "AdministeredDrug", "AdministeredHardDrug", "IngestedDrug",
            "IngestedHardDrug");
        AddEventDisposition(result, "ludeon.rimworld",
            "Overlapping native evidence",
            "The same completed act emits a more general event already adapted by Culture; retaining this sibling as a second practice would double-count one occurrence.",
            "DestroyedMineable", "SowedHumanFoodPlant");
        AddEventDisposition(result, "ludeon.rimworld",
            "Relationship fact outside the exact Culture adapter",
            "The event is broader than consensual sex outside marriage, records ordinary spousal conduct, or records bed sharing rather than sex. Native relationship mechanics remain authoritative.",
            "GotLovin", "GotLovin_Spouse", "SharedBed",
            "SharedBed_NonSpouse", "SharedBed_Spouse");
        AddEventDisposition(result, "ludeon.rimworld",
            "No exact appraisal or accountable practice actor",
            "The event is socially relevant, but its native seam does not state the exact population appraisal or accountable local actor required by the current Culture history contract.",
            "AcceptedDeserter", "Bonded", "CharityRefused_ThreatReward_Joiner",
            "FriendlyExitedMapTended", "GaveGift", "PerformedHarmfulSurgery");
        AddEventDisposition(result, "ludeon.rimworld",
            "Faction, quest, or world outcome",
            "This is a faction-level reason, quest result, casualty state, diplomatic result, or world-object outcome rather than a completed pawn practice with exact actor and map provenance.",
            "AttackedBuilding", "AttackedCaravan", "AttackedMember",
            "AttackedSettlement", "DestroyedEnemyBase", "GuiltyPrisonerDied",
            "InnocentPrisonerDied", "PrisonerDied", "MemberCaptured",
            "MemberCrushed", "MemberExitedMapHealthy", "MemberKilled",
            "MemberNeutrallyDied", "MemberSold", "MemberStripped",
            "QuestPawnArrested", "QuestPawnLost", "QuestPrisonerEnslaved",
            "QuestPrisonerRecruited", "FailedToKeepLodgersHappy",
            "LaborersMissedShuttle", "MemberMissedShuttle",
            "MonumentConstructionExpired", "MonumentConstructionMapRemoved",
            "MonumentDestroyed", "MinifiedTreeDied", "PeaceTalksBackfire", "PeaceTalksDisaster",
            "PeaceTalksSuccess", "PeaceTalksTriumph", "QuestGoodwillReward",
            "ReachNaturalGoodwill", "RequestedMilitaryAid",
            "RequestedOrbitalTrader", "RequestedTrader", "SettlementProximity",
            "ShuttleCommanderMissedShuttle", "ShuttleDestroyed",
            "ShuttleGuardsMissedShuttle");
        AddEventDisposition(result, "ludeon.rimworld",
            "Native Ideoligion execution",
            "Ritual completion is already a native Ideoligion fact. Culture separately observes exact cross-Ideoligion participation and does not duplicate every ritual as a Culture value.",
            "RitualDone");
        AddEventDisposition(result, "ludeon.rimworld",
            "Institutional or exchange outcome",
            "The generic trade event does not identify the kind of exchange, ownership rule, or stable population appraisal; Political Order and institutions retain those distinctions.",
            "Traded");
        AddEventDisposition(result, "ludeon.rimworld",
            "Native body and xenotype state",
            "Xenogerm absorption changes a specific pawn and xenotype state. It does not by itself establish the population's appraisal of bodily alteration or inherited groups.",
            "XenogermAbsorbed");
        AddEventDisposition(result, "ludeon.rimworld",
            "Diagnostic-only event",
            "This debug goodwill reason is not playable social evidence.",
            "DebugGoodwill");

        AddEventDisposition(result, "ludeon.rimworld.ideology",
            "Dietary complement event",
            "These high-frequency complement events record ordinary alternatives to a doctrine target. Treating their absence-of-target semantics as strong Culture evidence would make availability look like appraisal.",
            "AteNonCannibalFood", "AteNonFungusMealWithPlants",
            "AteNonFungusPlant", "AteNonMeat");
        AddEventDisposition(result, "ludeon.rimworld.ideology",
            "Native permission or target-specific doctrine",
            "The definition gates a specific automated weapon or named weapon category. It is not an attributable general practice or a population-wide machine or violence appraisal.",
            "BuiltAutomatedTurret", "KillWithDespisedWeapon",
            "KillWithNobleWeapon", "UsedDespisedWeapon", "UsedNobleWeapon");
        AddEventDisposition(result, "ludeon.rimworld.ideology",
            "Quest charity outcome without exact actor provenance",
            "The event is a quest-specific charity result. It lacks one accountable local actor and cannot be projected onto mutual provision or outsider inclusion without preserving beneficiary and circumstance.",
            "CharityFulfilled_Beggars",
            "CharityFulfilled_HospitalityRefugees",
            "CharityFulfilled_IntroWimp",
            "CharityFulfilled_RefugeePodCrash",
            "CharityFulfilled_ShuttleCrashRescue",
            "CharityFulfilled_ThreatReward_Joiner",
            "CharityFulfilled_WandererJoins", "CharityRefused_Beggars",
            "CharityRefused_Beggars_Betrayed",
            "CharityRefused_HospitalityRefugees",
            "CharityRefused_IntroWimp", "CharityRefused_Pilgrims",
            "CharityRefused_Pilgrims_Betrayed",
            "CharityRefused_RefugeePodCrash",
            "CharityRefused_ShuttleCrashRescue",
            "CharityRefused_WandererJoins");
        AddEventDisposition(result, "ludeon.rimworld.ideology",
            "Target-specific animal fact",
            "The event concerns a venerated species or a particular hospitality animal. It cannot establish the population's general moral standing for all animals without losing its target.",
            "HospitalitySuccess_Animals", "HuntedVeneratedAnimal",
            "SlaughteredVeneratedAnimal", "TameVeneratedAnimalDied");
        AddEventDisposition(result, "ludeon.rimworld.ideology",
            "Native symbolic practice",
            "Planting the Gauranlen tree is a specific native symbolic and doctrinal act, not general evidence about cultivation or resource stewardship.",
            "PlantedGauranlenTree");
        AddEventDisposition(result, "ludeon.rimworld.ideology",
            "Overlapping raid evidence",
            "The exact general raid event is already adapted. These scenario and target variants remain native so one raid is not counted again as a second cultural practice.",
            "PlayerRaidedSomeone", "Raided_BanditCamp", "Raided_WorkSite");
        AddEventDisposition(result, "ludeon.rimworld.ideology",
            "Native relic or pilgrimage outcome",
            "Relic and pilgrimage outcomes belong to native Ideoligion history; they do not independently establish a broader population appraisal.",
            "RelicDestroyed", "RelicHuntSuccess", "RelicLost",
            "ReliquaryPilgrimsSuccess");

        AddEventDisposition(result, "ludeon.rimworld.biotech",
            "Target-specific body, gene, or bloodfeeder state",
            "The event changes or reports a specific xenotype, gene, hemogen, or bloodfeeder state. Culture requires a separate population appraisal and does not infer one from the biological event.",
            "AbsorbedXenogerm", "BecomeNonPreferredXenotype",
            "BloodfeederDied", "ExtractedHemogenPack",
            "PropagateBloodfeederGene");
        AddEventDisposition(result, "ludeon.rimworld.biotech",
            "Actorless pollution and goodwill outcome",
            "The native event is selected as a world-tile goodwill reason and carries no exact pawn Doer or map-local practice occurrence. It remains native until a faction-level environmental-event adapter owns that provenance.",
            "PollutedBase", "PollutedNearbySite", "ToxicWasteDumping");

        AddEventDisposition(result, "ludeon.rimworld.odyssey",
            "World or combat outcome",
            "This is a world-object destruction or kidnapping outcome rather than a completed local practice with exact actor provenance.",
            "DestroyedMechhive", "PawnKidnappedOnGravShip");
        AddEventDisposition(result, "ludeon.rimworld.odyssey",
            "Actorless pollution and goodwill outcome",
            "Orbital pollution is selected as a world-layer goodwill reason and carries no exact pawn Doer or map-local practice occurrence.",
            "OrbitalPollution");

        AddEventDisposition(result, "llunak.MorePrecepts",
            "Native permission or doctrine trigger",
            "The trap event is used by the mod's exact native doctrine check; it is not one audited completed-practice emitter for Culture history.",
            "BuiltTrap");
        AddEventDisposition(result, "llunak.MorePrecepts",
            "Victim condition without accountable omitted-care actor",
            "These events record the incapacitated victim and severity, not who withheld care or which institution was responsible. Culture cannot invent an actor for the omission.",
            "Compassion_IncapacitatedPawnLeftToDie_All",
            "Compassion_IncapacitatedPawnLeftToDie_Allies",
            "Compassion_IncapacitatedPawnLeftToDie_NonGuiltyEnemies",
            "Compassion_IncapacitatedPawnLeftToDie_NonHostile");
        AddEventDisposition(result, "llunak.MorePrecepts",
            "Political or institutional possession rule",
            "These events implement drug-possession and trade prescriptions. They do not establish personal drug use; Political Order and institutions retain the rule while native Ideoligion executes it.",
            "DrugPossession_TradedDrug", "DrugPossession_TradedHardDrug",
            "DrugPossession_TradedNonAlcoholDrug");
        AddEventDisposition(result, "llunak.MorePrecepts",
            "Target-specific superstition signal",
            "The event conveys the mod's signed omen and thought semantics. No exact Culture construct currently represents those omen categories, so the native mechanic remains authoritative.",
            "Superstition_Superstitious_Generic",
            "Superstition_Superstitious_Mild_Minus",
            "Superstition_Superstitious_Mild_Plus",
            "Superstition_Superstitious_Strong_Minus",
            "Superstition_Superstitious_Strong_Plus");

        return result;
    }

    private static void AddEventDisposition(
        Dictionary<string, NativeFamilyDisposition> target,
        string packageId, string disposition, string rationale,
        params string[] eventDefNames)
    {
        foreach (string eventDefName in eventDefNames)
            target.Add(packageId + "|HistoryEvent|" + eventDefName,
                new NativeFamilyDisposition(disposition, rationale));
    }

    private static void AddDisposition(
        Dictionary<string, NativeFamilyDisposition> target,
        string disposition, string rationale, params string[] identities)
    {
        foreach (string identity in identities)
            target.Add(identity, new NativeFamilyDisposition(disposition,
                rationale));
    }

    private static string FamilyName(NativeDef value) =>
        value.Issue.Length == 0 ? "standalone: " + value.DefName : value.Issue;

    private static string FamilyIdentity(NativeDef value) =>
        value.PackageId + "|" + FamilyName(value);

    private static string NativeDefinitionIdentity(NativeDef value) =>
        value.PackageId + "|" + value.Kind + "|" + value.DefName;

    private static string EventIdentity(object value) =>
        StringField(value, "PackageId") + "|HistoryEvent|"
        + StringField(value, "EventDefName");

    private static void WriteCensus(string path, List<NativeDef> corpus,
        IList semantic, IList events, List<FamilyAdjudication> familyAudit,
        Dictionary<string, NativeFamilyDisposition> eventAdjudications,
        string moreCommit)
    {
        var mapped = new HashSet<string>(semantic.Cast<object>().Select(
            SemanticIdentity), StringComparer.OrdinalIgnoreCase);
        var text = new StringBuilder("# B16 Playable Social Ontology Census\n\n")
            .AppendLine("More Precepts source commit: `" + moreCommit + "`")
            .AppendLine().AppendLine("| Package | Precepts | Memes | Native events | Culture adapter rows | Practice event rows |")
            .AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach (IGrouping<string, NativeDef> package in corpus.GroupBy(value =>
                     value.PackageId).OrderBy(value => value.Key))
        {
            int semanticRows = semantic.Cast<object>().Count(value =>
                StringField(value, "PackageId").Equals(package.Key,
                    StringComparison.OrdinalIgnoreCase));
            int eventRows = events.Cast<object>().Count(value =>
                StringField(value, "PackageId").Equals(package.Key,
                    StringComparison.OrdinalIgnoreCase));
            text.AppendLine("| `" + package.Key + "` | "
                + package.Count(value => value.Kind == "Precept") + " | "
                + package.Count(value => value.Kind == "Meme") + " | "
                + package.Count(value => value.Kind == "HistoryEvent") + " | "
                + semanticRows + " | " + eventRows + " |");
        }
        text.AppendLine().AppendLine("## Native doctrine families")
            .AppendLine().AppendLine("Every loaded definition below has an exact Culture adapter, an exact native-only definition disposition, or an explicit family-wide native-owner disposition. Adapted siblings cannot hide an unresolved definition. Native doctrine always remains the executor; absence never triggers label or name inference.")
            .AppendLine().AppendLine("| Package | Issue or standalone definition | Definitions | Adapted | Disposition | Culture questions | Rationale |")
            .AppendLine("|---|---|---:|---:|---|---|---|");
        foreach (FamilyAdjudication row in familyAudit)
            text.AppendLine("| `" + row.PackageId + "` | "
                + Escape(row.Family) + " | " + row.DefinitionCount + " | "
                + row.AdaptedDefinitionCount + " | "
                + Escape(row.Disposition) + " | "
                + Escape(row.CultureQuestions.Length == 0 ? "-"
                    : string.Join(", ", row.CultureQuestions)) + " | "
                + Escape(row.Rationale) + " |");
        text.AppendLine().AppendLine("## Exact native-only definition dispositions")
            .AppendLine().AppendLine("These definitions remain native-only for an explicit semantic reason rather than being projected onto a broader Culture construct that would lose their target or scope.")
            .AppendLine().AppendLine("| Definition identity | Disposition | Rationale |")
            .AppendLine("|---|---|---|");
        foreach (KeyValuePair<string, NativeFamilyDisposition> row in
                 NativeDefinitionAdjudications().OrderBy(value => value.Key,
                     StringComparer.Ordinal))
            text.AppendLine("| `" + row.Key + "` | "
                + Escape(row.Value.Disposition) + " | "
                + Escape(row.Value.Rationale) + " |");
        var mappedEvents = events.Cast<object>().ToDictionary(EventIdentity,
            value => StringField(value, "PracticeKey"),
            StringComparer.OrdinalIgnoreCase);
        text.AppendLine().AppendLine("## Exact native-event dispositions")
            .AppendLine()
            .AppendLine("Every loaded HistoryEventDef is either an exact Culture-practice adapter or has an explicit native-only disposition. A definition name alone never proves that the event is recorded, attributable, culturally scoped, or independent of another event.")
            .AppendLine()
            .AppendLine("| Event identity | Disposition | Culture practice | Rationale |")
            .AppendLine("|---|---|---|---|");
        foreach (NativeDef value in corpus.Where(value =>
                     value.Kind == "HistoryEvent").OrderBy(
                     NativeDefinitionIdentity, StringComparer.Ordinal))
        {
            string identity = NativeDefinitionIdentity(value);
            if (mappedEvents.TryGetValue(identity, out string practiceKey))
                text.AppendLine("| `" + identity
                    + "` | Exact Culture-practice adapter | `"
                    + practiceKey
                    + "` | Native execution remains authoritative; CA records the exact completed event through its audited emitter and provenance contract. |");
            else
            {
                if (eventAdjudications.TryGetValue(identity,
                        out NativeFamilyDisposition disposition))
                    text.AppendLine("| `" + identity + "` | "
                        + Escape(disposition.Disposition) + " | - | "
                        + Escape(disposition.Rationale) + " |");
                else
                    text.AppendLine("| `" + identity
                        + "` | **Unresolved** | - | No exact adapter or explicit disposition exists. |");
            }
        }
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static Dictionary<string, string> CultureRows(XDocument document)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (XElement culture in CultureNodes(document))
        foreach (string collection in new[] { "inheritedQuestions",
                     "localQuestions" })
        foreach (XElement row in culture.Element(collection)?.Elements("li")
                     ?? Enumerable.Empty<XElement>())
        {
            string identity = Value(culture, "id") + "|" + collection + "|"
                + Scope(row) + "|" + Value(row, "questionKey");
            result[identity] = row.ToString(SaveOptions.DisableFormatting);
        }
        return result;
    }

    private static XElement[] CultureNodes(XDocument document) => document
        .Descendants().Where(value => value.Element("questionRegistryVersion")
            != null && value.Element("inheritedQuestions") != null
            && value.Element("localQuestions") != null).ToArray();

    private static string Scope(XElement row) =>
        row.Element("populationScope")?.Value ?? "*";
    private static string Value(XElement owner, string name) =>
        owner?.Element(name)?.Value ?? "";

    private static string SemanticIdentity(object value) =>
        StringField(value, "PackageId") + "|"
        + RequiredField(value.GetType(), "Kind").GetValue(value) + "|"
        + StringField(value, "DefName");

    private static string SemanticMappingIdentity(object value) =>
        SemanticIdentity(value) + "|"
        + StringField(value, "QuestionKey") + "|"
        + StringField(value, "PairSide");

    private static void CheckIt(string name, bool passed, string evidence) =>
        Checks.Add(new Check(name, passed, evidence));

    private static object ScribeRoundTrip(Assembly gameAssembly,
        Type valueType, object value, string root)
    {
        Type scribe = gameAssembly.GetType("Verse.Scribe", true)!;
        Type log = gameAssembly.GetType("Verse.Log", true)!;
        FieldInfo logDisablers = RequiredField(log, "logDisablers");
        int prior = (int)logDisablers.GetValue(null)!;
        logDisablers.SetValue(null, prior + 1);
        object saver = RequiredField(scribe, "saver").GetValue(null)!;
        object loader = RequiredField(scribe, "loader").GetValue(null)!;
        MethodInfo expose = Method(valueType, "ExposeData", 0);
        string path = Path.Combine(Path.GetTempPath(), "cao-b16-"
            + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            Method(saver.GetType(), "InitSaving", 2).Invoke(saver,
                new object[] { path, root });
            try { expose.Invoke(value, Array.Empty<object>()); }
            finally
            {
                Method(saver.GetType(), "FinalizeSaving", 0).Invoke(saver,
                    Array.Empty<object>());
            }
            Method(loader.GetType(), "InitLoading", 1).Invoke(loader,
                new object[] { path });
            object restored;
            try { restored = Activator.CreateInstance(valueType)!; }
            catch (MissingMethodException)
            {
                restored = RuntimeHelpers.GetUninitializedObject(valueType);
            }
            Type mapComponentType = gameAssembly.GetType(
                "Verse.MapComponent", true)!;
            if (mapComponentType.IsAssignableFrom(valueType))
                RequiredField(valueType, "map").SetValue(restored,
                    RequiredField(valueType, "map").GetValue(value));
            try { expose.Invoke(restored, Array.Empty<object>()); }
            finally
            {
                Method(loader.GetType(), "ForceStop", 0).Invoke(loader,
                    Array.Empty<object>());
            }
            return restored;
        }
        finally
        {
            logDisablers.SetValue(null, prior);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static object LoadDeepRoot(Assembly gameAssembly, Type valueType,
        string path, string root)
    {
        Type scribe = gameAssembly.GetType("Verse.Scribe", true)!;
        Type log = gameAssembly.GetType("Verse.Log", true)!;
        Type prefs = gameAssembly.GetType("Verse.Prefs", true)!;
        Type deep = gameAssembly.GetType("Verse.Scribe_Deep", true)!;
        FieldInfo logDisablers = RequiredField(log, "logDisablers");
        FieldInfo prefsData = RequiredField(prefs, "data");
        if (prefsData.GetValue(null) == null)
            prefsData.SetValue(null, Activator.CreateInstance(
                prefsData.FieldType));
        int prior = (int)logDisablers.GetValue(null)!;
        object loader = RequiredField(scribe, "loader").GetValue(null)!;
        MethodInfo look = deep.GetMethods(All).Single(method =>
                method.Name == "Look" && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 3
                && method.GetParameters()[1].ParameterType == typeof(string))
            .MakeGenericMethod(valueType);
        object loaded = null;
        logDisablers.SetValue(null, prior + 1);
        try
        {
            Method(loader.GetType(), "InitLoading", 1).Invoke(loader,
                new object[] { path });
            object[] values = { loaded, root, Array.Empty<object>() };
            try
            {
                look.Invoke(null, values);
                loaded = values[0];
            }
            finally
            {
                Method(loader.GetType(), "FinalizeLoading", 0).Invoke(loader,
                    Array.Empty<object>());
            }
        }
        finally { logDisablers.SetValue(null, prior); }
        return loaded ?? throw new InvalidDataException(path
            + " did not load root " + root);
    }

    private static StringBuilder Header(string title, string dll, string hash)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        return new StringBuilder().AppendLine("# " + title).AppendLine()
            .AppendLine("Generated: " + now.ToUniversalTime().ToString(
                "yyyy-MM-dd HH:mm:ss 'UTC'") + " / "
                + now.ToString("yyyy-MM-dd HH:mm:ss zzz"))
            .AppendLine().AppendLine("Assembly: `"
                + PortableAssemblyPath(dll) + "`")
            .AppendLine("SHA-256: `" + hash + "`").AppendLine();
    }

    private static string Escape(string value) => (value ?? "")
        .Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    private static string PortableAssemblyPath(string dll) =>
        string.Equals(Path.GetFileName(dll), "ColonistAwareness.dll",
            StringComparison.OrdinalIgnoreCase)
            ? "Assemblies/ColonistAwareness.dll"
            : Path.GetFileName(dll);

    private static void LoadManagedAssemblies(string managed)
    {
        foreach (string file in new[]
        {
            "netstandard.dll", "UnityEngine.CoreModule.dll",
            "UnityEngine.IMGUIModule.dll", "UnityEngine.TextRenderingModule.dll",
            "Assembly-CSharp-firstpass.dll", "Assembly-CSharp.dll"
        }) Assembly.LoadFrom(Path.Combine(managed, file));
    }

    private static Type RequiredType(Assembly assembly, string name) =>
        assembly.GetType(name, true)!;

    private static IList Values(Type type, string property) =>
        ((IEnumerable)RequiredProperty(type, property).GetValue(null)!)
        .Cast<object>().ToList();

    private static MethodInfo Method(Type type, string name,
        int parameterCount) => type.GetMethods(All).Single(method =>
        method.Name == name && method.GetParameters().Length == parameterCount);

    private static PropertyInfo RequiredProperty(Type type, string name) =>
        type.GetProperty(name, All)
        ?? throw new MissingMemberException(type.FullName, name);

    private static string StringProperty(object value, string name) =>
        RequiredProperty(value.GetType(), name).GetValue(value) as string ?? "";

    private static string QuestionSignature(object value)
    {
        return string.Join("|", value.GetType().GetFields(All)
            .Where(field => !field.IsStatic)
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .Select(field => field.Name + "="
                + Canonical(field.GetValue(value))));
    }

    private static string Canonical(object value)
    {
        if (value == null) return "null";
        if (value is string text) return text;
        if (value is IEnumerable sequence)
            return "[" + string.Join(",", sequence.Cast<object>()
                .Select(Canonical)) + "]";
        return value is IFormattable formatted
            ? formatted.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString();
    }

    private static FieldInfo RequiredField(Type type, string name)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            FieldInfo field = current.GetField(name, All);
            if (field != null) return field;
        }
        throw new MissingMemberException(type.FullName, name);
    }

    private static string StringField(object value, string name) =>
        RequiredField(value.GetType(), name).GetValue(value) as string ?? "";

    private static float FloatField(object value, string name) =>
        (float)RequiredField(value.GetType(), name).GetValue(value)!;
}
