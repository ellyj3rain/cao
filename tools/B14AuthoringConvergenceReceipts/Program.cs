using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class Program
{
    private sealed record Check(int Number, string Name, bool Passed,
        string Evidence);

    private static int Main(string[] args)
    {
        bool verifyOnly = args.Length == 4 && args[3] == "--verify-only";
        if (args.Length != 3 && !verifyOnly)
        {
            Console.Error.WriteLine("usage: B14AuthoringConvergenceReceipts "
                + "<repo> <active-fixture> <mirror-fixture>");
            return 1;
        }
        string repo = Path.GetFullPath(args[0]);
        string activePath = Path.GetFullPath(args[1]);
        string mirrorPath = Path.GetFullPath(args[2]);
        string political = Read(repo, "Source/PoliticalOrderAuthoringModule.cs");
        string culture = Read(repo, "Source/FactionCultureBeliefsModule.cs");
        string cultureAuthoring = Read(repo,
            "Source/CultureAuthoringModule.cs");
        string authoringChoices = Read(repo,
            "Source/AuthoringComposerSupportModule.cs");
        string creationFlow = Read(repo,
            "Source/CreationFlowUiModule.cs");
        string societyPresets = Read(repo,
            "Source/SocietyPresetModule.cs");
        string playerFounding = Read(repo,
            "Source/PlayerFoundingPageModule.cs");
        string regional = Read(repo, "Source/RegionalSetupModule.cs");
        string regionalScreen = Read(repo,
            "Source/RegionalPopulationScreenModule.cs");
        string regionMap = Read(repo, "Source/RegionMapWidget.cs");
        string regionalEditors = Read(repo,
            "Source/RegionalPopulationEditorsModule.cs");
        string culturalExpression = Read(repo,
            "Source/CulturalExpressionModule.cs");
        string ontology = Read(repo, "Source/AuthoringOntologyKernel.cs");
        string presentation = Read(repo,
            "Source/AuthoringPresentationModule.cs");
        string settings = Read(repo, "Source/ModEntry.cs");
        string compatibility = Read(repo,
            "Source/CampaignCompatibilityKernel.cs");
        string governance = Read(repo, "GOVERNANCE.md");
        string allSource = string.Join("\n", Directory.GetFiles(
                Path.Combine(repo, "Source"), "*.cs")
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(File.ReadAllText));

        byte[] activeBytes = File.ReadAllBytes(activePath);
        byte[] mirrorBytes = File.ReadAllBytes(mirrorPath);
        XDocument active = XDocument.Parse(Encoding.UTF8.GetString(
            activeBytes).TrimStart('\uFEFF'));
        XElement plan = active.Root?.Element("plan")
            ?? throw new InvalidDataException("active plan missing");
        XElement[] orders = plan.Descendants("politicalBeliefs").ToArray();
        int propertyQuestions = Regex.Matches(political,
            "PropertyQuestion\\(\"property\\.").Count;
        int questionCount = Regex.Matches(political,
                "new CAPoliticalQuestionDef\\(").Count - 1
            + propertyQuestions;
        int declaredOptions = Regex.Matches(political, "\\bO\\(\"").Count;
        int optionCount = declaredOptions - 4 + propertyQuestions * 4;
        int presetCount = Regex.Matches(political,
            "new CAPoliticalOrderPreset\\(").Count;

        var checks = new List<Check>();
        void C(string name, bool passed, string evidence) => checks.Add(
            new Check(checks.Count + 1, name, passed, evidence));

        C("Orphan regional markers are removed",
            !allSource.Contains("CARegionalPreviewOverlay")
                && !allSource.Contains("CARegionalWorldOverlay.Draw"),
            "no preview painter remains for the former A or diamond glyphs");
        C("Culture identity references follow their owner",
            culture.Contains("SynchronizeOwnIdentityLabel")
                && culture.Contains("item.cultureId == culture.id")
                && culture.Contains("followsInheritedName")
                && culture.Contains("existing.name = LocalName(inheritedNow?.name)"),
            "self-constituent labels and generated local names follow the owning Culture");
        C("Explicit local Culture names remain independent",
            culture.Contains("bool followsInheritedName = !existing.Authored(")
                && culture.Contains("CACulture.NameField)")
                && culture.Contains("if (followsInheritedName)"),
            "parent propagation is conditional on the local name remaining generated");
        string authoringUi = string.Join("\n", culture, political,
            playerFounding, regionalScreen, regionalEditors,
            culturalExpression, authoringChoices);
        C("Player copy does not expose model-working vocabulary",
            !authoringUi.Contains("\"Most salient\"")
                && !authoringUi.Contains("\"Inherited source\"")
                && !authoringUi.Contains("\"Constituent cultures\"")
                && !authoringUi.Contains("\"Practice evidence\"")
                && !authoringUi.Contains("\"Causal effects\"")
                && !authoringUi.Contains("\"Generated political account\"")
                && !authoringUi.Contains(
                    "\"represented belief-practice tension")
                && !authoringUi.Contains("\"Set composition...\"")
                && authoringUi.IndexOf("inherited view",
                    StringComparison.OrdinalIgnoreCase) < 0
                && authoringUi.IndexOf("recorded as:",
                    StringComparison.OrdinalIgnoreCase) < 0
                && authoringUi.IndexOf("authored for this population",
                    StringComparison.OrdinalIgnoreCase) < 0
                && culture.Contains("\"Overall disagreement\"")
                && culture.Contains("\"Gameplay effects...\"")
                && political.Contains("\"Institutions in use\"")
                && political.Contains("\"Set mix...\"")
                && !regionalScreen.Contains("beliefs · structure")
                && !regionalScreen.Contains("Beliefs, culture, structure")
                && governance.Contains("**Concepts are not copy.**"),
            "chat and model terminology remain design input; Culture and Political Order screens state choices and effects in direct player language");
        C("Political Order has complete causal breadth",
            questionCount == 26 && optionCount == 110
                && propertyQuestions == 12 && presetCount == 8,
            questionCount + " questions; " + propertyQuestions
                + " ownership domains; " + optionCount + " positions; "
                + presetCount + " complete presets");
        C("Mixtures and exclusive choices have explicit invariants",
            political.Contains("does not total 100")
                && political.Contains("does not permit a mixture")
                && political.Contains("item.share * 100d / total"),
            "blendable questions normalize to 100; exclusive questions retain one answer");
        C("Generated political identity follows variables",
            political.Contains("GeneratedName(beliefs")
                && political.Contains("PropertyAverage(beliefs")
                && political.Contains("CompositionWords(definition, selected)")
                && political.Contains(
                    "internal static string Description(CAPoliticalBeliefs"),
            "name, short summary, and natural account project the configured order");
        C("Custom political prose remains secondary",
            political.Contains("beliefs.nameAuthored = true")
                && political.Contains(
                    "if (!beliefs.nameAuthored || beliefs.name.NullOrEmpty())")
                && political.Contains("Reroll generated name")
                && political.Contains("Custom name..."),
            "optional display naming does not replace the generated causal account");
        C("The progressive mixed order is representable",
            HasText(political,
                "authority.leadership", "executive", "50", "council", "30",
                "assembly", "20")
                && HasText(political, "property.food", "public", "45",
                    "cooperative", "30", "private", "25")
                && HasText(political, "property.industry", "private", "45",
                    "cooperative", "40", "public", "15")
                && HasText(political, "property.luxury", "private", "55",
                    "cooperative", "35", "public", "10")
                && HasText(political, "economy.rent", "noExtraction", "100"),
            "executive/civic authority, public essentials, mixed industry and luxury enterprise, and anti-rent policy coexist");
        C("One question edit owns one direct variable",
            political.Contains("beliefs.questions.RemoveAll(item => item != null")
                && political.Contains("item.questionKey == questionKey")
                && political.Contains("beliefs.questions.Add(state)"),
            "SetQuestion replaces only the selected question before deterministic normalization");
        C("Duplicate institution authoring is absent",
            !allSource.Contains("Dialog_CAAxisEditor")
                && !allSource.Contains("CAPoliticalPatchTemplate")
                && !allSource.Contains("Current-order sets"),
            "one Political Order composer remains; represented institutions are comparison facts");
        C("Saved Political Orders copy complete state",
            presentation.Contains("values = beliefs.Copy()")
                && presentation.Contains("ApplyPoliticalValues(profile?.values")
                && presentation.Contains("target.CopyFrom(values)")
                && presentation.Contains("target.questions"),
            "profiles copy all questions into the destination without shared mutable state");
        C("Persistence and compatibility name the current model",
            culture.Contains("Scribe_Collections.Look(ref questions, \"questions\"")
                && compatibility.Contains("D(\"model.political-order\", 10)")
                && compatibility.Contains(
                    "D(\"model.represented-institutions\", 1)")
                && ontology.Contains("Control(\"politics.order\"")
                && ontology.Contains(
                    "Control(\"politics.represented-institutions\""),
            "schema 10 Political Order and schema 1 represented institutions have separate owners");
        C("Active and mirror fixtures are byte-identical",
            activeBytes.SequenceEqual(mirrorBytes),
            activeBytes.Length + " bytes; SHA-256 " + Sha(activeBytes));
        C("Authored composition survives the schema conversion",
            Items(plan, "factions").Count == 3
                && Items(plan, "settlements").Count == 4,
            Items(plan, "factions").Count + " factions; "
                + Items(plan, "settlements").Count + " settlements");
        C("Every saved Political Order is complete",
            orders.Length == 4 && orders.All(order =>
                Value(order, "schemaVersion") == "10"
                && Items(order, "questions").Count == questionCount
                && Items(order, "questions").All(state =>
                    Items(state, "options").Count > 0
                    && Items(state, "options").Sum(option =>
                        int.Parse(Value(option, "share"))) == 100)
                && Items(order, "questions").Where(state =>
                        Value(state, "questionKey") is "civic.liberty"
                            or "economy.rent")
                    .All(state => Items(state, "options").Count == 1)),
            orders.Length + " records each retain " + questionCount
                + " normalized questions after readback");
        C("Recovered mixed relationships survive serialization",
            orders.Any(order => HasPair(order, "property.industry", "private",
                    "cooperative")
                && HasPair(order, "economy.exchange", "market", "communal"))
                && plan.Descendants("factionStructure").Any(structure =>
                    HasInstitutionPair(structure, "support", "private",
                        "public")),
            "normative mixtures and independent represented support institutions coexist");
        string[] requestedCulturePresets =
        {
            "english-north-america-early-colonial",
            "france-napoleonic-empire",
            "france-second-empire",
            "united-states-civil-war-union",
            "confederate-slaveholding-dominant-culture",
            "freedpeople-emancipation-communities",
            "germany-weimar-republic",
            "germany-national-socialist-dictatorship",
            "japan-late-tokugawa",
            "japan-meiji-transformation",
            "qing-china-late-imperial",
            "ottoman-empire-tanzimat",
            "mughal-india-akbar"
        };
        string[] requestedCultureLabels =
        {
            "Early English colonial culture",
            "Civil War Union culture",
            "Confederate slaveholding culture",
            "Freedpeople emancipation culture",
            "Napoleonic French culture",
            "Second-Empire French culture",
            "Weimar German culture",
            "Nazi regime culture",
            "Late Tokugawa Japanese culture",
            "Meiji Japanese culture",
            "Late Qing Chinese culture",
            "Tanzimat Ottoman culture",
            "Akbar-era Mughal culture"
        };
        C("Historical Culture presets share one complete authoring path",
            Regex.Matches(cultureAuthoring,
                "HistoricalPreset\\(\"").Count == 16
                && Regex.Matches(cultureAuthoring,
                    "Preset\\(\"").Count >= 22
                && requestedCulturePresets.All(cultureAuthoring.Contains)
                && requestedCultureLabels.All(label =>
                    cultureAuthoring.Contains("\"" + label + "\""))
                && cultureAuthoring.Contains("Definitions.Length != 22")
                && cultureAuthoring.Contains("Historical cultures")
                && cultureAuthoring.Contains("ReferenceContext")
                && cultureAuthoring.Contains("ReferenceRegion")
                && authoringChoices.Contains("CulturePresets(")
                && authoringChoices.Contains("CatalogGroup")
                && authoringChoices.Contains(
                    "OrderBy(item => CultureCatalogOrder(item.CatalogGroup))")
                && authoringChoices.Contains(
                    "ThenBy(item => item.ApproximatePeriod")
                && culture.Contains("CAAuthoringChoices.CulturePresets(")
                && playerFounding.Contains(
                    "CAAuthoringChoices.CulturePresets("),
            "22 complete Culture presets have component-specific names and one social-or-historical catalog axis; period and region remain metadata");
        C("Society presets own an independent coordinated catalog",
            Regex.Matches(societyPresets, "\\bS\\(\"").Count == 22
                && societyPresets.Contains("abstract class CASocietyPreset")
                && societyPresets.Contains("PresetKey")
                && societyPresets.Contains("CulturePresetKey")
                && societyPresets.Contains("PoliticalOrderValues")
                && societyPresets.Contains("TechnologicalKnowledgeValues")
                && societyPresets.Contains("a society preset reuses a Culture preset key")
                && !societyPresets.Contains(
                    "Definitions.Length != CACulturePresetLibrary.All.Count")
                && !societyPresets.Contains(
                    "society presets and Culture presets do not agree")
                && societyPresets.Contains("internal static bool TryApply(")
                && societyPresets.Contains(
                    "CACulture cultureCandidate = culture.Copy()")
                && societyPresets.Contains(
                    "CAPoliticalBeliefs politicalCandidate = politicalOrder.Copy()")
                && societyPresets.Contains(
                    "CATechnologicalKnowledge technologyCandidate")
                && societyPresets.Contains("preset.ApplyComponents(")
                && societyPresets.Contains("CAPoliticalOrderModel.ApplyPreset(")
                && societyPresets.Contains(
                    "politicalOrder.CopyFrom(PoliticalOrderValues)")
                && societyPresets.Contains(
                    "CACultureModel.ValidationFailure(cultureCandidate")
                && societyPresets.Contains(
                    "CAPoliticalOrderModel.ValidationFailure(")
                && societyPresets.Contains(
                    "CATechnologicalKnowledgeModel.ValidationFailure(")
                && societyPresets.Contains("preset.Matches(")
                && societyPresets.Contains("PoliticalOverrides")
                && societyPresets.Contains("ApplyPoliticalOrder(")
                && societyPresets.Contains("MatchesPoliticalOrder(")
                && societyPresets.Contains("culture.CopyFrom(cultureCandidate)")
                && societyPresets.Contains(
                    "politicalOrder.CopyFrom(politicalCandidate)")
                && societyPresets.Contains(
                    "technologicalKnowledge.CopyFrom(technologyCandidate")
                && societyPresets.Contains(
                    "culture.CopyFrom(cultureBefore)")
                && societyPresets.Contains(
                    "politicalOrder.CopyFrom(politicalBefore)")
                && societyPresets.Contains(
                    "technologicalKnowledge.CopyFrom(technologyBefore)")
                && culture.Contains("MatchesInheritedTemplate(")
                && cultureAuthoring.Contains("MatchesInheritedTemplate(")
                && political.Contains("O(\"coerced\", \"coerced labor\"")
                && societyPresets.Contains("\"restricted\", 100")
                && authoringChoices.Contains("SocietyPresets(")
                && authoringChoices.Contains(
                    "CASocietyPresetLibrary.TryApply(")
                && authoringChoices.Contains("TryChoose = choose")
                && creationFlow.Contains("internal Func<bool> TryChoose")
                && creationFlow.Contains("if (accepted) Close()")
                && playerFounding.Contains("DrawSocietyCard(")
                && playerFounding.Contains("OpenSocietyPresets(")
                && !Regex.IsMatch(playerFounding,
                    "CAAuthoringChoices\\.SocietyPresets[\\s\\S]{0,300}"
                        + "RefreshSuggestedArrangement")
                && regionalScreen.Contains("OpenSocietyPresets(group)"),
            "22 independently identified Society recipes atomically copy complete faction-owned Culture, Political Order, and Technological Knowledge state; no one-to-one catalog validator remains");
        C("Saved Society presets use the same existing interaction path",
            presentation.Contains("class CAUserSocietyProfile")
                && presentation.Contains("cultureValues")
                && presentation.Contains("politicalOrderValues")
                && presentation.Contains("technologicalKnowledgeValues")
                && presentation.Contains("CAUserSocietyPresetAdapter")
                && presentation.Contains("SaveSociety(")
                && settings.Contains("societyProfiles")
                && settings.Contains("\"societyProfiles\", LookMode.Deep")
                && societyPresets.Contains("CASocietyPresetLibrary.Societies") == false
                && societyPresets.Contains("CAAuthoringProfileLibrary.Societies")
                && authoringChoices.Contains("CASocietyPresetLibrary.Available")
                && playerFounding.Contains("Save society preset...")
                && regionalScreen.Contains("Save society preset...")
                && !allSource.Contains("Page_CASociety")
                && !allSource.Contains("Dialog_CASociety"),
            "ModSettings stores deep three-component snapshots; founding and faction Society sections apply and save them without another page or persistent world ownership");
        C("Culture and Political Order remain independent substitutions",
            authoringChoices.Contains("CulturePresets(")
                && authoringChoices.Contains("CultureProfiles(")
                && political.Contains("OpenPresets()")
                && political.Contains("OpenSaved()")
                && presentation.Contains("ApplyCultureValues(")
                && presentation.Contains("ApplyPoliticalValues(")
                && cultureAuthoring.Contains("culture.name = preset.Label")
                && !regional.Contains("societyPreset")
                && !playerFounding.Contains("societyPresetKey"),
            "component loaders write only their owning state; preset identity is not serialized into faction, settlement, or founding records");
        C("Settlement placement reuses the society catalog and map assignment",
            authoringChoices.Contains("SocietyPlacementPresets(")
                && regionalScreen.Contains("OpenSettlementPlacement()")
                && regionalScreen.Contains(
                    "AddSettlementFor(local.key, scenarioPopulation: true)")
                && regionalScreen.Contains(
                    "populationOrigin = scenarioPopulation")
                && regionalScreen.Contains(
                    "? CASettlementOrigin.ScenarioOverride")
                && regionalScreen.Contains(
                    "CARegionMapWidget.awaitingSlot = slot")
                && regionMap.Contains("Action placeSettlement = null")
                && regionMap.Contains("ShowPlaceSettlement(")
                && regionMap.Contains("\"Place settlement\"")
                && !regional.Contains("settlementPreset")
                && !regionalScreen.Contains("new[] { \"Objects\""),
            "one copy-on-apply society catalog creates ordinary scenario settlement state and arms the existing broad-area map assignment; no settlement-preset mode is persisted");
        C("Settlement Culture follows faction-owned state unless locally changed",
            regionalScreen.Contains("FactionSocietyChanged(group)")
                && regionalScreen.Contains("FactionCultureChanged(group)")
                && regionalScreen.Contains("refreshInheritedState: true")
                && culture.Contains("local.parentId = inherited?.id")
                && culture.Contains("cultureId = inheritedId")
                && culture.Contains("bool followsInheritedName")
                && culture.Contains("SyncConstituentQuestionBaselines(plan")
                && culture.Contains("A later explicit rename sets NameField"),
            "the faction owns applied Culture; each settlement's explicit local-history record references that parent and refreshes inherited baselines until a local field is authored");
        C("Region authoring remains the current schema",
            Value(plan, "schemaVersion") == "15"
                && regional.Contains("CurrentSchemaVersion = 15"),
            "regional plan schema 15 retains the existing geography and composition while carrying the current faction-owned authored state");

        string receiptPath = Path.Combine(repo, "Receipts", "B14",
            "B14_AUTHORING_CONVERGENCE_STATIC_RECEIPT.md");
        Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
        var output = new StringBuilder()
            .AppendLine("# B14 Authoring Convergence Static Receipt")
            .AppendLine()
            .AppendLine("Timestamp: " + Timestamp())
            .AppendLine()
            .AppendLine("This receipt verifies the current source and governed pending fixture. It does not claim operator visual or gameplay acceptance.")
            .AppendLine()
            .AppendLine("| # | Check | Result | Evidence |")
            .AppendLine("|---:|---|---|---|");
        foreach (Check check in checks)
            output.AppendLine("| " + check.Number + " | " + check.Name
                + " | **" + (check.Passed ? "PASS" : "FAIL") + "** | "
                + check.Evidence.Replace("|", "\\|") + " |");
        int failures = checks.Count(check => !check.Passed);
        output.AppendLine()
            .AppendLine("Result: **" + (failures == 0 ? "PASS" : "FAIL")
                + "** - " + (checks.Count - failures) + "/" + checks.Count
                + " checks passed.");
        if (!verifyOnly) File.WriteAllText(receiptPath, output.ToString(),
            new UTF8Encoding(false));
        foreach (Check check in checks)
            Console.WriteLine($"{check.Number:00} "
                + (check.Passed ? "PASS " : "FAIL ") + check.Name + ": "
                + check.Evidence);
        Console.WriteLine((checks.Count - failures) + "/" + checks.Count
            + " passed");
        return failures == 0 ? 0 : 2;
    }

    private static string Timestamp()
    {
        DateTimeOffset utc = DateTimeOffset.UtcNow;
        TimeZoneInfo pacific = TimeZoneInfo.FindSystemTimeZoneById(
            "Pacific Standard Time");
        DateTimeOffset local = TimeZoneInfo.ConvertTime(utc, pacific);
        return utc.ToString("yyyy-MM-dd HH:mm") + " UTC / "
            + local.ToString("HH:mm") + " PST";
    }

    private static bool HasText(string source, params string[] values)
    {
        int index = 0;
        foreach (string value in values)
        {
            index = source.IndexOf(value, index, StringComparison.Ordinal);
            if (index < 0) return false;
            index += value.Length;
        }
        return true;
    }

    private static bool HasPair(XElement beliefs, string question,
        string left, string right)
    {
        XElement state = Items(beliefs, "questions").FirstOrDefault(item =>
            Value(item, "questionKey") == question);
        if (state == null) return false;
        var options = new HashSet<string>(Items(state, "options")
            .Where(item => int.Parse(Value(item, "share")) > 0)
            .Select(item => Value(item, "optionKey")), StringComparer.Ordinal);
        return options.Contains(left) && options.Contains(right);
    }

    private static bool HasInstitutionPair(XElement structure, string axis,
        string left, string right)
    {
        var options = new HashSet<string>(structure.Elements("li")
            .Where(item => Value(item, "axis") == axis)
            .Select(item => Value(item, "option")), StringComparer.Ordinal);
        return options.Contains(left) && options.Contains(right);
    }

    private static List<XElement> Items(XElement owner, string name) =>
        owner?.Element(name)?.Elements("li").ToList()
            ?? new List<XElement>();

    private static string Value(XElement owner, string name) =>
        owner?.Element(name)?.Value ?? "";

    private static string Read(string repo, string relative) =>
        File.ReadAllText(Path.Combine(repo,
            relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string Sha(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));
}
