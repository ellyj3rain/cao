using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonistAwareness;

internal static class Program
{
    private static int checks;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length < 1 || args.Length > 2)
                throw new ArgumentException(
                    "usage: AuthoringConvergenceReceipts <repo> [keyed-fixture]");
            string repository = Path.GetFullPath(args[0]);
            string Read(string name) => File.ReadAllText(Path.Combine(
                repository, "Source", name));

            string presentation = Read("AuthoringPresentationModule.cs");
            string support = Read("AuthoringComposerSupportModule.cs");
            string creation = Read("CreationFlowUiModule.cs");
            string culture = Read("FactionCultureBeliefsModule.cs");
            string axes = Read("FactionCompositionModule.cs");
            string founding = Read("PlayerFoundingPageModule.cs");
            string foundingState = Read("PlayerFoundingStateModule.cs");
            string region = Read("RegionalPopulationScreenModule.cs");
            string regionEditors = Read("RegionalPopulationEditorsModule.cs");
            string setup = Read("RegionalSetupModule.cs");
            string settings = Read("ModEntry.cs");
            string facilities = Read("StartingFacilitiesModule.cs");
            string expression = Read("CulturalExpressionModule.cs");
            string causal = Read("CulturalExpressionCausalKernel.cs");
            string settlementAxes = Read("SettlementAxesModule.cs");
            string settlementModel = Read("RegionalSettlementModelModule.cs");
            string world = Read("RegionalWorldModule.cs");

            var report = new StringBuilder();
            report.AppendLine("AUTHORING CONVERGENCE RECEIPT");
            CheckInformationDetail(repository, presentation, creation, region,
                settings, report);
            CheckCulture(repository, culture, support, facilities, expression,
                causal, founding, foundingState, region, regionEditors, setup,
                settlementAxes, settlementModel, world, report);
            CheckPolitics(axes, culture, support, report);
            CheckProfiles(presentation, support, culture, report);
            CheckNativeFlow(founding, foundingState, report);
            CheckResponsiveLayout(creation, region, founding, support,
                regionEditors, setup, culture, report);
            CheckEstablishedState(setup, culture, report);
            CheckTechnologyOwnership(region, report);
            CheckSharedAuthoring(founding, culture, support, report);
            CheckCopyIsolation(presentation, culture, support, report);
            if (args.Length == 2)
                CheckFixture(Path.GetFullPath(args[1]), report);

            report.AppendLine("result: PASS (" + checks + " assertions)");
            Console.Write(report.ToString());
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("result: FAIL");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void CheckInformationDetail(string repository,
        string presentation, string creation, string region, string settings,
        StringBuilder report)
    {
        Require(presentation.Contains("enum CAInformationDetail")
            && presentation.Contains("Compact = 0")
            && presentation.Contains("Standard = 1")
            && presentation.Contains("Expanded = 2"),
            "neutral information-detail levels are incomplete");
        Require(settings.Contains("CAInformationDetail.Standard")
            && settings.Contains("Scribe_Values.Look(ref informationDetail")
            && settings.Contains("Creation-flow information")
            && settings.Contains("CAInformationPresentation.Description"),
            "information detail is not persisted and editable in ModSettings");
        Require(presentation.Contains("internal static string Select")
            && presentation.Contains("DrawLocalExpansion")
            && presentation.Contains("Show full details")
            && presentation.Contains(
                "Current >= CAInformationDetail.Expanded"),
            "central detail selection or local expansion is absent");
        Require(creation.Contains("CAInformationPresentation.Shows")
            && region.Contains("CAInformationPresentation.Select"),
            "creation cards or region inspector do not consume the policy");

        var directReads = Directory.GetFiles(Path.Combine(repository, "Source"),
                "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("informationDetail"))
            .Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value).ToArray();
        Require(directReads.SequenceEqual(new[]
            {
                "AuthoringPresentationModule.cs", "ModEntry.cs"
            }, StringComparer.OrdinalIgnoreCase),
            "information detail leaked outside the presentation owner: "
                + string.Join(", ", directReads));
        report.AppendLine("PASS information detail -> persisted, centralized, "
            + "locally expandable, presentation-only");
    }

    private static void CheckCulture(string repository, string culture,
        string support, string facilities, string expression, string causal,
        string founding, string foundingState, string region, string regionEditors,
        string setup, string settlementAxes, string settlementModel,
        string world, StringBuilder report)
    {
        string activeEnvelope = Slice(culture,
            "public sealed class CACulture",
            "public sealed class CAPoliticalBeliefs");
        Require(culture.Contains("const int CurrentSchemaVersion = 3")
            && !culture.Contains("class CACultureDomainDef")
            && !culture.Contains("class CACultureOptionDef")
            && !activeEnvelope.Contains("ThingDefName")
            && !activeEnvelope.Contains("StuffDefName"),
            "B5-01 active Culture still owns physical definitions");

        Require(!culture.Contains("class CACultureMaterialization")
            && !facilities.Contains("CACultureMaterialization.Furnish")
            && !facilities.Contains("CACultureMaterialization.StyleFor")
            && facilities.Contains("CAVisualTraditionStyle.StyleFor"),
            "B5-02 settlement materialization still calls cultural furnishing");

        string compatibility = Slice(culture,
            "internal static string CompatibilityFailure(CACulture culture)",
            "internal static void Migrate(CACulture culture)");
        Require(compatibility.Contains("return null")
            && !compatibility.Contains("ThingDef")
            && !compatibility.Contains("Texture")
            && !compatibility.Contains("DefDatabase")
            && culture.Contains("BaseContent.BadTex")
            && culture.Contains(
                "\"Rimshare/WorldMapIcons/feather\", false"),
            "B5-03 Culture validity still depends on visual or construction assets");

        string visualStyle = Slice(culture,
            "internal static class CAVisualTraditionStyle",
            "internal static class CAFactionStartingState");
        Require(visualStyle.Contains(
                "if (native?.thingStyleCategories == null) return null")
            && visualStyle.Contains("return null")
            && culture.Contains("neutral visual fallback")
            && culture.Contains(
                "!culture.Authored(CACulture.SourceCultureField)")
            && culture.Contains(
                "culture.Choose(CACulture.SourceCultureField, null)"),
            "B5-04 missing native style does not have a neutral fallback");

        CACulturalExpressionCausalResult constrained =
            CACulturalExpressionCausalKernel.Evaluate(
                ExpressionCause(0, 0, 0));
        CACulturalExpressionCausalResult developed =
            CACulturalExpressionCausalKernel.Evaluate(
                ExpressionCause(3, 3, 3));
        Require(expression.Contains(
                "CACulturalExpressionCausalKernel.Evaluate")
            && expression.Contains("Access = input.Access")
            && expression.Contains("Services = input.Services")
            && expression.Contains("Civic = input.Civic")
            && constrained.Status == 2 && developed.Status == 0
            && constrained.Summary != developed.Summary
            && constrained.Signature != developed.Signature,
            "B5-05 material conditions do not alter a fixed-belief reading");

        CACulturalExpressionCausalInput councilCause = ExpressionCause(2, 2, 2);
        CACulturalExpressionCausalInput rulerCause = ExpressionCause(2, 2, 2);
        rulerCause.PoliticalBeliefs[0].Option = "single_leader";
        CACulturalExpressionCausalResult council =
            CACulturalExpressionCausalKernel.Evaluate(councilCause);
        CACulturalExpressionCausalResult ruler =
            CACulturalExpressionCausalKernel.Evaluate(rulerCause);
        Require(expression.Contains(
                "PoliticalBeliefs = CausalAxes(input.Beliefs)")
            && !string.IsNullOrWhiteSpace(council.Summary)
            && !string.IsNullOrWhiteSpace(ruler.Summary)
            && council.Signature != ruler.Signature,
            "B5-06 political beliefs do not alter a fixed-settlement reading");

        CACulturalExpressionCausalInput uniformCause = ExpressionCause(2, 2, 2);
        CACulturalExpressionCausalInput pluralCause = ExpressionCause(2, 2, 2);
        pluralCause.Populations[0].Share = 60;
        pluralCause.Populations.Add(new CACulturalPopulationCause
        {
            Key = "minority",
            Share = 40,
            IdeoligionSource = "founders",
            BeliefSource = "single_leader",
            SeparateQuarter = true
        });
        pluralCause.WeightedBeliefs[0].Share = 60;
        pluralCause.WeightedBeliefs.Add(new CACulturalWeightedBeliefCause
        {
            Identity = "minority-beliefs",
            Share = 40,
            Positions = new List<CACulturalAxisCause>
            {
                new CACulturalAxisCause
                {
                    Axis = "leadership",
                    Option = "single_leader"
                }
            }
        });
        CACulturalExpressionCausalResult uniform =
            CACulturalExpressionCausalKernel.Evaluate(uniformCause);
        CACulturalExpressionCausalResult plural =
            CACulturalExpressionCausalKernel.Evaluate(pluralCause);
        Require(expression.Contains(
                "WeightedBeliefs = input.PopulationBeliefs.Select")
            && expression.Contains("BeliefSource(item)")
            && expression.Contains("PopulationBeliefs(")
            && expression.Contains("PreferredAxis(")
            && uniform.Status == 0 && plural.Status == 3
            && uniform.Summary.Contains("one resident community")
            && plural.Summary.Contains("several resident groups")
            && uniform.Signature != plural.Signature,
            "B5-07 weighted political plurality is not causal");

        Require(expression.Contains("retain separate quarters")
            && expression.Contains("IdeoligionSource")
            && causal.Contains("ideologySources")
            && !expression.Contains("ThingDef")
            && !expression.Contains("ThingMaker")
            && !expression.Contains("GenSpawn"),
            "B5-08 quarters or Ideoligion plurality force a physical object");

        string settlementDraw = Slice(region,
            "private void DrawSettlementPanel",
            "private void DrawFactionPanel");
        Require(expression.Contains("CopyAxes(")
            && expression.Contains("CopyPopulations(")
            && !expression.Contains("Rand.")
            && !expression.Contains("Scribe_")
            && !expression.Contains(".Destroy(")
            && !expression.Contains(".DeSpawn(")
            && !settlementDraw.Contains("EnsureSettlementPattern")
            && !settlementDraw.Contains("CASettlementStartingState.Sync")
            && Count(settlementDraw,
                "CASettlementComposition.EnsureDerived") == 1
            && settlementDraw.Contains("\"Generate population\"")
            && settlementDraw.IndexOf(
                "CASettlementComposition.EnsureDerived",
                StringComparison.Ordinal) > settlementDraw.IndexOf(
                    "\"Generate population\"", StringComparison.Ordinal),
            "B5-09 opening or drawing cultural expression can mutate state");

        string applyCarried = Slice(foundingState,
            "internal static void ApplyCarriedState",
            "internal static string ArrangementSourceWords");
        Require(founding.Contains("Their local culture is still developing")
            && expression.Contains("ForFounders(")
            && causal.Contains("culture is")
            && causal.Contains("developing from")
            && applyCarried.Contains("draft.culture?.Copy()")
            && applyCarried.Contains("draft.politicalBeliefs?.Copy()")
            && !applyCarried.Contains("startingFacility")
            && !applyCarried.Contains("gatheringKey"),
            "B5-10 founding pre-realizes mature local culture");

        string materialized = Slice(expression,
            "internal static CACulturalExpression ForMaterializedSettlement",
            "internal static CACulturalExpression ForFounders");
        Require(materialized.Contains("populationGroups")
            && materialized.Contains("factionStructure")
            && materialized.Contains("accessInfrastructure")
            && materialized.Contains("economicCapacity")
            && materialized.Contains("tradeConnectivity")
            && materialized.Contains("historicalDevelopment")
            && materialized.Contains("realizedRole")
            && materialized.Contains("realizedScale")
            && materialized.Contains("settlementForm")
            && materialized.Contains("fortification")
            && materialized.Contains("organization")
            && materialized.Contains("startingProvisions")
            && materialized.Contains("relationAtMaterialization")
            && materialized.Contains("ConstituentHasRoad")
            && materialized.Contains("ConstituentHasRiver")
            && materialized.Contains("ConstituentIsCoastal")
            && materialized.Contains("CountRelations")
            && expression.Contains("item.funding")
            && expression.Contains("item.populationGroupKey")
            && expression.Contains("item.waterSecured")
            && expression.Contains("item.nodes")
            && expression.Contains("item.reach")
            && expression.Contains("CAFactionAxes.WarConduct"),
            "B5-11 established settlement readings omit saved causal state");

        Require(expression.Contains("IdeoligionCommitmentKeys")
            && expression.Contains("IdeoligionCommitmentLabels")
            && expression.Contains("PopulationIdeoligions")
            && expression.Contains("PreceptsListForReading")
            && causal.Contains("Ideoligion commitments=")
            && causal.Contains("political beliefs=")
            && region.Contains("CACulturalExpressionModel.ForFaction")
            && region.Contains("expression.Summary")
            && settlementModel.Contains("culturalExpressionSummary")
            && settlementModel.Contains("cultural expression: ")
            && expression.Contains("MaterializedIdeoligions")
            && expression.Contains("CAPopulationProjection.Residents")
            && world.Contains("ReconcileCulturalExpression(record, map)")
            && world.Contains("CARegionalSettlementMarkers.Ensure(region"),
            "B5-11a Ideoligion commitments or established/runtime cultural "
                + "surfaces are disconnected");

        Require(expression.Contains(
                "CACulturalExpressionCausalKernel.Evaluate")
            && expression.Contains("result.SourceSignature = causal.Signature")
            && causal.Contains("Signature(facts)")
            && causal.Contains("Status(input.Tension, plural, input.Founding")
            && CACulturalExpressionCausalKernel.Status(false, false, false,
                2, 2, 0, 0) == 0
            && CACulturalExpressionCausalKernel.Status(false, true, false,
                2, 2, 0, 0) == 3
            && CACulturalExpressionCausalKernel.Status(true, false, false,
                2, 2, 0, 0) == 4,
            "B5-11b production cultural causality is not exercised by the "
                + "receipt kernel");

        string presented = Slice(expression,
            "internal string Presented(bool locallyExpanded = false)",
            "internal static string StatusWords");
        string presentationState = "belief=council|population=60/40|infra=2/1/3";
        string presentationHash = Hash(presentationState);
        foreach (string detail in new[] { "Compact", "Standard", "Expanded" })
            _ = detail + ":" + presentationState;
        Require(presented.Contains("CAInformationPresentation.Select")
            && !presented.Contains("Signature:")
            && !presented.Contains("scope=")
            && presentationHash == Hash(presentationState)
            && region.Contains("expression.Presented(inspectorExpanded)"),
            "B5-12 presentation detail changes simulation state");

        bool boundedProfiles = true;
        for (int basis = 0; basis <= 3; basis++)
            boundedProfiles &= ResolveDevelopment(-1, basis, -1)
                    == Math.Max(0, basis - 1)
                && ResolveDevelopment(-1, basis, 0) == basis
                && ResolveDevelopment(-1, basis, 1)
                    == Math.Min(3, basis + 1);
        Require(boundedProfiles
            && settlementAxes.Contains(
                "CACulturalExpressionCausalKernel.ApplyDevelopmentProfile"),
            "B5-13 relative development offsets are not bounded");

        Require(ResolveDevelopment(2, 0, -1) == 2
            && ResolveDevelopment(2, 3, 1) == 2
            && settlementAxes.Contains(
                "CACulturalExpressionCausalKernel.ResolveDevelopment"),
            "B5-14 explicit infrastructure does not survive profile changes");

        string profileSetter = Slice(regionEditors,
            "int profile = (int)settlement.developmentProfile + 1;",
            "TooltipHandler.TipRegion");
        int facilityAuthoredMask = 37;
        int facilityValues = 5;
        int generatedA = CACulturalExpressionCausalKernel.ResolveFacilityMask(
            3, facilityAuthoredMask, facilityValues, 127);
        int generatedB = CACulturalExpressionCausalKernel.ResolveFacilityMask(
            67, facilityAuthoredMask, facilityValues, 127);
        Require(facilityAuthoredMask == 37 && facilityValues == 5
            && (generatedA & facilityAuthoredMask) == facilityValues
            && (generatedB & facilityAuthoredMask) == facilityValues
            && !profileSetter.Contains("startingFacilityAuthoredMask")
            && !profileSetter.Contains("startingFacilityValues")
            && regionEditors.Contains("\"Generated: included\"")
            && regionEditors.Contains("\"Generated: omitted\"")
            && regionEditors.Contains("\"Include\", \"Omit\""),
            "B5-15 facility Include/Omit is replaced by profile changes");

        string startingChange = Slice(region,
            "private void StartingSettingsChanged",
            "private void FactionSettingsChanged");
        Require(startingChange.Contains("plan.confirmed = false;")
            && startingChange.IndexOf("plan.confirmed = false;",
                    StringComparison.Ordinal) < startingChange.IndexOf(
                    "CARegionalSettlements.Invalidate(plan);",
                    StringComparison.Ordinal),
            "B5-15a confirmed Back-to-authoring edits cannot recompute their "
                + "preview");

        string finalRepresentation = Slice(region,
            "private string FinalRepresentation",
            "private static int CountFacilityOverrides");
        Require(finalRepresentation.Contains("Settlement development:")
            && finalRepresentation.Contains("place.developmentProfile")
            && finalRepresentation.Contains("place.accessInfrastructure")
            && finalRepresentation.Contains("place.serviceInfrastructure")
            && finalRepresentation.Contains("place.civicInfrastructure")
            && finalRepresentation.Contains(
                "place.startingFacilityAuthoredMask"),
            "B5-15b final representation omits settlement development or "
                + "exact facility overrides");

        string profileEnum = Slice(setup,
            "public enum CASettlementDevelopmentProfile",
            "public sealed class CARegionalSettlementPlan");
        Require(profileEnum.Contains("Minimal = -1")
            && profileEnum.Contains("Contextual = 0")
            && profileEnum.Contains("Extensive = 1")
            && !profileEnum.Contains("Mask")
            && !regionEditors.Contains("Sparse settlement")
            && !regionEditors.Contains("Established settlement")
            && !regionEditors.Contains("All starting facilities")
            && !regionEditors.Contains("OpenPresets"),
            "B5-16 development profiles still encode fixed facility masks");

        string realizationHash = Slice(settlementModel,
            "private static int RealizationSourceHash",
            "private static void RealizeRelations");
        Require(realizationHash.Contains(
                "(int)settlement.developmentProfile")
            && realizationHash.Contains(
                "faction.institutionalStateIncomplete ? 1 : 0")
            && realizationHash.Contains("faction.politicalBeliefs")
            && setup.Contains("Scribe_Values.Look(ref developmentProfile")
            && world.Contains("Scribe_Values.Look(ref developmentProfile"),
            "B5-17 realization hashing or persistence omits the profile");

        string migration = Slice(culture,
            "internal static void Migrate(CACulture culture)",
            "internal static void EnsureGenerated");
        var legacyCases = new Dictionary<string, string>
        {
            ["hearth_common"] = "Rustican",
            ["road_exchange"] = "Corunan",
            ["memorial_households"] = "Sophian",
            ["festival_market"] = "Astropolitan"
        };
        bool legacyCasesPass = legacyCases.All(item =>
        {
            CACultureLegacyMigrationResult migrated =
                CACultureLegacyMigrationKernel.Migrate(
                    new CACultureLegacyMigrationInput
                    {
                        SchemaVersion = 2,
                        PresetName = item.Key,
                        Name = item.Key.Replace('_', ' ') + " customs",
                        NameAuthored = false
                    });
            return migrated.SchemaVersion == 3
                && migrated.PresetName == null
                && migrated.SourceCultureDefName == item.Value
                && migrated.Name == item.Value + " background"
                && migrated.ClearLegacyPractices;
        });
        Require(migration.Contains("CACultureLegacyMigrationKernel.Migrate")
            && migration.Contains("culture.schemaVersion = migrated.SchemaVersion")
            && migration.Contains("culture.name = migrated.Name")
            && migration.Contains(
                "culture.sourceCultureDefName = migrated.SourceCultureDefName")
            && migration.Contains("culture.ClearLegacyPractices()")
            && legacyCasesPass,
            "B5-18 schema-2 Culture migration is not deterministic");

        Require(!migration.Contains(".Destroy(")
            && !migration.Contains(".DeSpawn(")
            && !migration.Contains("Map")
            && !migration.Contains("Thing")
            && !migration.Contains("Furnish")
            && !Directory.GetFiles(Path.Combine(repository, "Source"), "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path).Contains("Migration",
                    StringComparison.OrdinalIgnoreCase))
                .Any(path => File.ReadAllText(path).Contains(
                    "CACulture") && (File.ReadAllText(path).Contains(
                        ".Destroy(") || File.ReadAllText(path).Contains(
                        ".DeSpawn("))),
            "B5-19 Culture migration can delete materialized objects");

        report.AppendLine("PASS B5 culture and settlement ownership -> "
            + "19 causal receipts; fixture receipt follows");
    }

    private static void CheckPolitics(string axes, string culture,
        string support, StringBuilder report)
    {
        string[] axisKeys =
        {
            "leadership", "decisions", "participation", "dissent",
            "ownership", "economy", "work", "support", "membership",
            "status", "localOrder", "defense", "warConduct"
        };
        foreach (string key in axisKeys)
            Require(axes.Contains("\"" + key + "\""),
                "political axis key changed or disappeared: " + key);
        Require(Regex.Matches(axes, "Key = \"[a-z_]+\"").Count >= 14,
            "political profiles do not have stable machine keys");
        foreach (string parent in new[]
        {
            "civic_council", "common_ownership", "stateless_pluralism",
            "hereditary_rule"
        })
            Require(axes.Contains("Parent = \"" + parent + "\""),
                "political inheritance still depends on a display label: " + parent);

        string migration = Slice(culture,
            "internal static void Migrate(CAPoliticalBeliefs",
            "internal static void Ensure(CAPoliticalBeliefs");
        Require(migration.Contains("beliefs.presetName = preset.Key")
            && !migration.Contains("positions.Clear")
            && !migration.Contains("GenerateUnset"),
            "political migration overwrites saved axis positions");
        string generate = Slice(culture, "internal static int GenerateUnset",
            "internal static void ApplyPreset");
        Require(generate.Contains("!= CAAxisSource.Unset")
            && !generate.Contains("positions.Clear"),
            "generate-missing can replace authored positions");
        foreach (string group in new[]
        {
            "Governance", "Civic participation", "Property and economy",
            "Membership and order", "Security and conflict"
        })
            Require(support.Contains("\"" + group + "\""),
                "political composer group missing: " + group);
        Require(culture.Contains("Preferred position")
            && culture.Contains("Current institution")
            && culture.Contains("In tension:")
            && culture.Contains("CAFactionStructureModel.Tensions"),
            "belief-versus-practice comparison is not visible in one composer");
        report.AppendLine("PASS politics -> stable identity, preserved axes, "
            + "grouped composition, norm-versus-practice comparison");
    }

    private static void CheckProfiles(string presentation, string support,
        string culture, StringBuilder report)
    {
        Require(Regex.Matches(presentation,
                "Scribe_Deep.Look\\(ref values").Count == 2,
            "profile values are not deep-serialized independently");
        Require(presentation.Contains("profile.values.id = null")
            && presentation.Contains("profile.values.presetName = null")
            && presentation.Contains("string worldIdentity = target.id")
            && presentation.Contains("target.id = worldIdentity"),
            "profile storage or apply semantics retain world identity");
        foreach (string operation in new[]
        {
            "SaveCulture", "SavePolitics", "Duplicate", "Rename", "Delete"
        })
            Require(presentation.Contains(operation),
                "global profile operation missing: " + operation);
        Require(support.Contains("Dialog_CAProfileManager")
            && support.Contains("compactRows")
            && support.Contains("Saved profiles are global")
            && culture.Contains("Manage saved..."),
            "profile manager or responsive CRUD surface is incomplete");
        report.AppendLine("PASS profiles -> global ModSettings library, "
            + "copy-on-apply, CRUD, world-identity isolation");
    }

    private static void CheckNativeFlow(string founding, string state,
        StringBuilder report)
    {
        Require(founding.Contains("current is Page_ChooseIdeoPreset")
            && founding.Contains("Page following = current.next")
            && founding.Contains("current.next = inserted")
            && founding.Contains("current.nextAct = null"),
            "native Ideoligion chooser is not retained before CA authoring");
        Require(!founding.Contains("OpenIdeoPresets")
            && founding.Contains("Dialog_IdeoList_Load")
            && founding.Contains("Page_CAConfigureFoundingIdeo")
            && founding.Contains("Page_CAConfigureFoundingFluidIdeo"),
            "generic CA Ideoligion browsing remains or native edit/load paths are absent");
        Require(founding.Contains("nativeIdeoChooser")
            && founding.Contains("prev is Page_ConfigureIdeo")
            && founding.Contains("nativeFlowJustAccepted")
            && founding.Contains("awaitingNativeReturn")
            && founding.Contains("nativeChooserJustAccepted") == false,
            "native preset/custom/back notification paths are not distinguished");
        Require(founding.Contains("!draft.nativeIdeoNotified")
            && founding.Contains("draft.nativeIdeoNotified = true")
            && state.Contains("nativeIdeoSignature")
            && state.Contains("nativeIdeoNotified"),
            "native notification and content receipts are incomplete");
        Require(!founding.Contains("EnsureEstablishedIdeos"),
            "opening the founding page can still mutate established factions");
        report.AppendLine("PASS native Ideoligion -> native chooser retained, "
            + "fixed/fluid/load/edit paths preserve draft and notification state");
    }

    private static void CheckResponsiveLayout(string creation, string region,
        string founding, string support, string regionEditors, string setup,
        string culture, StringBuilder report)
    {
        Require(region.Contains("enum CARegionLayoutMode")
            && region.Contains("Wide") && region.Contains("Medium")
            && region.Contains("Compact")
            && region.Contains("inRect.width >= 1600f")
            && region.Contains("inRect.width >= 1180f"),
            "Starting Region layout modes or thresholds are incomplete");
        Require(region.Contains("new[] { \"Objects\", \"Map\", \"Details\" }")
            && region.Contains("CARegionMapWidget")
            && region.Contains("Text.CalcHeight"),
            "compact region navigation, shared selection, or measured rows are absent");
        Require(creation.Contains("outRect.width >= 590f")
            && creation.Contains("rowHeights")
            && creation.Contains("Mathf.Max(112f")
            && !creation.Contains("132f")
            && !creation.Contains("Mathf.Clamp(24f"),
            "shared choice cards still use a clipping fixed-height contract");
        Require(creation.Contains("choices.Count < 8")
            && creation.Contains("DrawGroups")
            && creation.Contains("DrawLocalExpansion"),
            "choice filtering, categories, or local detail expansion is absent");
        Require(founding.Contains("bool twoColumns = gridWidth >= 900f")
            && !founding.Contains("752f")
            && founding.Contains("MeasureArrangementCard")
            && !founding.Contains("arrangementScroll")
            && founding.Contains("StartingContextSummary")
            && founding.Contains("StartingContextTitle"),
            "founding page retains its rigid grid or universal society claim");
        Require(support.Contains("width < 560f")
            && support.Contains("buttonWidth = (width - gap * 3f) / 4f"),
            "saved-profile actions can still escape a narrow panel");
        Require(creation.Contains("DrawSegmentRows")
            && culture.Contains("DrawSegmentRows")
            && culture.Contains("Mathf.Min(580f, UI.screenHeight - 48f)"),
            "long category labels or the culture modal are not height-responsive");
        string populationDialog = Slice(regionEditors,
            "internal sealed class Dialog_CAPopulationGroupEditor",
            "internal sealed class Dialog_CAStartingFacilities");
        string facilityDialog = Slice(regionEditors,
            "internal sealed class Dialog_CAStartingFacilities",
            "\n}");
        string arrangementDialog = Slice(founding,
            "internal sealed class Dialog_CAFoundingArrangementEditor",
            "\n}");
        Require(populationDialog.Contains("Widgets.BeginScrollView")
            && facilityDialog.Contains("Widgets.BeginScrollView")
            && arrangementDialog.Contains("Widgets.BeginScrollView"),
            "short-screen authoring dialogs do not own a bounded body scroll");
        Require(setup.Contains("landingPanelScroll")
            && setup.Contains("float cappedHeight")
            && Slice(setup, "internal static void DrawAndInteract()",
                "private static bool HasDesignedRegion")
                .Contains("Widgets.BeginScrollView"),
            "landing overlay is not capped and scrollable at short heights");

        var expected = new Dictionary<(int width, decimal scale), string>
        {
            [(1280, 1.0m)] = "Medium", [(1280, 1.25m)] = "Compact",
            [(1280, 1.5m)] = "Compact", [(1600, 1.0m)] = "Wide",
            [(1600, 1.25m)] = "Medium", [(1600, 1.5m)] = "Compact",
            [(1920, 1.0m)] = "Wide", [(1920, 1.25m)] = "Medium",
            [(1920, 1.5m)] = "Medium"
        };
        foreach (var item in expected)
        {
            decimal effective = item.Key.width / item.Key.scale;
            string actual = effective >= 1600m ? "Wide"
                : effective >= 1180m ? "Medium" : "Compact";
            Require(actual == item.Value,
                $"layout matrix failed at {item.Key.width}px/{item.Key.scale}");
        }
        report.AppendLine("PASS responsive layout -> 9 width/scale cases, "
            + "measured cards and rows, deliberate compact navigation");
    }

    private static void CheckEstablishedState(string setup, string culture,
        StringBuilder report)
    {
        Require(setup.Contains("institutionalStateIncomplete")
            && setup.Contains("Scribe_Values.Look(ref institutionalStateIncomplete"),
            "intentional incomplete institutional state is not persisted");
        Require(culture.Contains("if (!group.institutionalStateIncomplete)")
            && culture.Contains("CAPoliticalBeliefsModel.GenerateUnset")
            && culture.Contains("CAFactionStructureModel.GenerateUnset"),
            "established factions are not realized at an initialization boundary");
        report.AppendLine("PASS established factions -> generated institutions "
            + "with an explicit incomplete-state exception");
    }

    private static void CheckTechnologyOwnership(string region,
        StringBuilder report)
    {
        Require(region.Contains("Faction technology")
            && region.Contains("Local material capability")
            && region.Contains("Knowledge access and research")
            && region.Contains("Faction baseline:")
            && region.Contains("Local research capability:")
            && region.Contains("cannot practice everything its faction"),
            "faction knowledge and settlement capability remain conflated");
        Require(!region.Contains("Settlement laboratories, workshops, roads, "
                + "and coastal access determine local capability"),
            "old mixed-ownership technology sentence remains");
        report.AppendLine("PASS technology ownership -> faction baseline and "
            + "settlement-local implementation separated");
    }

    private static void CheckSharedAuthoring(string founding, string culture,
        string support, StringBuilder report)
    {
        Require(Count(founding, "CAAuthoringChoices.CultureProfiles") == 1
            && Count(culture, "CAAuthoringChoices.CultureProfiles") == 1,
            "founding and faction culture do not use one profile builder");
        Require(Count(founding, "CAAuthoringChoices.PoliticalProfiles") == 1
            && Count(culture, "CAAuthoringChoices.PoliticalProfiles") == 1,
            "founding and faction politics do not use one profile builder");
        Require(support.Contains("CultureProfiles(")
            && support.Contains("PoliticalProfiles(")
            && culture.Contains("Dialog_CACultureEditor")
            && culture.Contains("Dialog_CAAxisEditor"),
            "shared composers or shared choice controllers are missing");
        report.AppendLine("PASS shared authoring -> one culture builder, one "
            + "political builder, common composers at both temporal scopes");
    }

    private static void CheckCopyIsolation(string presentation,
        string culture, string support, StringBuilder report)
    {
        string cultureCopy = Slice(culture, "internal CACulture Copy()",
            "internal void CopyFrom(CACulture source)");
        Require(cultureCopy.Contains(
                "schemaVersion = CurrentSchemaVersion")
            && cultureCopy.Contains("sourceCultureDefName = sourceCultureDefName")
            && cultureCopy.Contains("profileKey = profileKey")
            && cultureCopy.Contains(
                "authoredMask = authoredMask & (NameField | SourceCultureField)")
            && cultureCopy.Contains("presetMask = 0")
            && !cultureCopy.Contains("gatheringKey")
            && !cultureCopy.Contains("hospitalityKey")
            && !cultureCopy.Contains("mealsKey")
            && !cultureCopy.Contains("remembranceKey"),
            "background clone retains recipes or omits active provenance");
        string politicalCopy = Slice(culture,
            "internal CAPoliticalBeliefs Copy()",
            "internal void CopyFrom(CAPoliticalBeliefs source)");
        Require(politicalCopy.Contains(
                "CAFactionStartingState.CopyAxes(positions)")
            && presentation.Contains("target.CopyFrom(profile.values)")
            && presentation.Contains("target.id = worldIdentity"),
            "production profile apply is not a deep copy with world identity");
        var library = new Dictionary<string, string>
        {
            ["leadership"] = "council", ["ownership"] = "cooperative"
        };
        var applied = new Dictionary<string, string>(library);
        applied["leadership"] = "single";
        Require(library["leadership"] == "council"
            && applied["leadership"] == "single",
            "profile copy test did not isolate later world edits");
        string cultureIdentity = Slice(support,
            "internal static string CultureIdentity",
            "internal static List<CACreationChoice> PoliticalProfiles");
        Require(cultureIdentity.Contains("return culture.name")
            && !cultureIdentity.Contains("CAAuthoringProfileLibrary")
            && !cultureIdentity.Contains("displayName"),
            "saved-profile rename can change already-applied background "
                + "identity");

        XElement state = new XElement("state",
            new XElement("background", "Corunan"),
            new XElement("politics", "worker_federation"));
        string before = state.ToString(SaveOptions.DisableFormatting);
        foreach (string detail in new[] { "Compact", "Standard", "Expanded" })
            _ = detail + ":" + state.Element("background")?.Value;
        Require(before == state.ToString(SaveOptions.DisableFormatting),
            "presentation-detail test changed authored state");
        report.AppendLine("PASS deterministic invariants -> background profile edits are "
            + "copy-isolated; detail selection does not alter state");
    }

    private static void CheckFixture(string path, StringBuilder report)
    {
        XDocument document = XDocument.Load(path,
            LoadOptions.PreserveWhitespace);
        XElement root = document.Root
            ?? throw new InvalidDataException("fixture has no root element");
        XElement plan = root.Element("plan")
            ?? throw new InvalidDataException("fixture has no plan element");
        List<XElement> factions = Items(plan, "factions");
        List<XElement> settlements = Items(plan, "settlements");
        int groups = settlements.Sum(item =>
            Items(item, "populationGroups").Count);
        Require(factions.Count == 3 && settlements.Count == 4 && groups == 9,
            "B5-20 fixture composition changed from 3 factions, 4 settlements, 9 groups");

        Require(Value(plan, "schemaVersion") == "4"
            && Value(root, "worldIdentity")
                == "alysaliu|1|Algorab Markab"
            && Value(plan, "regionalId") == "CA-RG-EB596A12"
            && Value(plan, "candidateId") == "613b1fe44104"
            && Value(plan, "bundleRootTileId") == "389638"
            && Value(plan, "startTileId") == "389638"
            && Value(plan, "mapSize") == "350",
            "fixture identity or schema changed during B5 migration");

        var cultures = factions.Select(item => item.Element("culture"))
            .Concat(new[] { plan.Element("playerFounding")?.Element("culture") })
            .Where(item => item != null).ToList();
        Require(cultures.Count == 4
            && cultures.All(item => Value(item, "schemaVersion") == "3")
            && cultures.All(item => !string.IsNullOrEmpty(Value(item, "id")))
            && cultures.All(item => item.Element("gatheringKey") == null
                && item.Element("hospitalityKey") == null
                && item.Element("mealsKey") == null
                && item.Element("remembranceKey") == null),
            "fixture culture did not converge to schema 3 carried state");

        Require(settlements.All(item =>
                Value(item, "developmentProfile") == "Contextual")
            && settlements.All(item =>
                !string.IsNullOrEmpty(Value(item, "startingFacilityMask"))),
            "fixture settlements did not acquire contextual development provenance");

        var politics = factions.Select(item => item.Element("politicalBeliefs"))
            .Concat(new[]
            {
                plan.Element("playerFounding")?.Element("politicalBeliefs")
            }).Where(item => item != null).ToList();
        Require(politics.Count == 4
            && politics.All(item => Value(item, "schemaVersion") == "2"),
            "fixture political beliefs changed during Culture migration");

        Require(Items(plan, "relations").Count >= 3
            && settlements.All(item => Items(item, "populationGroups").Count > 0)
            && plan.Element("playerFounding")?.Element("arrangement") != null,
            "fixture relationships, populations, or founding arrangement were dropped");

        string serialized = document.ToString(SaveOptions.DisableFormatting);
        string before = Hash(serialized);
        string after = Hash(XDocument.Parse(serialized,
                LoadOptions.PreserveWhitespace)
            .ToString(SaveOptions.DisableFormatting));
        Require(before == after, "fixture changed across XML readback");
        report.AppendLine("PASS B5-20 keyed fixture -> schema 4; Culture 3; "
            + "3 factions; 4 settlements; 9 groups; stable XML readback");
    }

    private static List<XElement> Items(XElement parent, string name)
    {
        return parent?.Element(name)?.Elements("li").ToList()
            ?? new List<XElement>();
    }

    private static string Value(XElement parent, string name)
    {
        return (string)parent?.Element(name) ?? "";
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(value)));
    }

    private static int ResolveDevelopment(int authored, int derived,
        int profile)
    {
        return CACulturalExpressionCausalKernel.ResolveDevelopment(authored,
            derived, profile);
    }

    private static CACulturalExpressionCausalInput ExpressionCause(
        int access, int services, int civic)
    {
        return new CACulturalExpressionCausalInput
        {
            Scope = "settlement:test",
            Background = "Rustican background",
            Ideoligion = "Founders' Ideoligion",
            IdeoligionCommitments = new List<string> { "collectivist" },
            PoliticalBeliefs = new List<CACulturalAxisCause>
            {
                new CACulturalAxisCause
                {
                    Axis = "leadership",
                    Option = "council"
                }
            },
            InstitutionalPractice = new List<CACulturalAxisCause>
            {
                new CACulturalAxisCause
                {
                    Axis = "leadership",
                    Option = "council"
                }
            },
            Populations = new List<CACulturalPopulationCause>
            {
                new CACulturalPopulationCause
                {
                    Key = "founders",
                    Share = 100,
                    IdeoligionSource = "founders",
                    BeliefSource = "council"
                }
            },
            WeightedBeliefs = new List<CACulturalWeightedBeliefCause>
            {
                new CACulturalWeightedBeliefCause
                {
                    Identity = "founder-beliefs",
                    Share = 100,
                    Positions = new List<CACulturalAxisCause>
                    {
                        new CACulturalAxisCause
                        {
                            Axis = "leadership",
                            Option = "council"
                        }
                    }
                }
            },
            PopulationIdeoligionIdentities = new List<string> { "founders" },
            Residents = 60,
            Land = 2,
            Access = access,
            Services = services,
            Civic = civic,
            Economy = 2,
            Trade = 2,
            Specialization = 1,
            History = 1,
            Facilities = 3,
            Role = 1,
            Scale = 1,
            Form = 1,
            Fortification = 1,
            Organization = 1,
            ProvisionFingerprint = "food=shared",
            InstitutionSummary = "Current institutions follow council rule",
            MaterialSummary = access <= 1 && services <= 1 && civic <= 1
                ? "material conditions are limited"
                : "material conditions are developed"
        };
    }

    private static int Count(string value, string needle)
    {
        int count = 0;
        for (int index = 0; (index = value.IndexOf(needle, index,
            StringComparison.Ordinal)) >= 0; index += needle.Length) count++;
        return count;
    }

    private static string Slice(string source, string start, string end)
    {
        int from = source.IndexOf(start, StringComparison.Ordinal);
        int to = from < 0 ? -1 : source.IndexOf(end,
            from + start.Length, StringComparison.Ordinal);
        Require(from >= 0 && to > from,
            "source contract slice is missing: " + start);
        return source.Substring(from, to - from);
    }

    private static void Require(bool condition, string failure)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(failure);
    }
}
