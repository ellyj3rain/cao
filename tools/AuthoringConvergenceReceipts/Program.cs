using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

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

            var report = new StringBuilder();
            report.AppendLine("AUTHORING CONVERGENCE RECEIPT");
            CheckInformationDetail(repository, presentation, creation, region,
                settings, report);
            CheckCulture(culture, support, facilities, report);
            CheckPolitics(axes, culture, support, report);
            CheckProfiles(presentation, support, culture, report);
            CheckNativeFlow(founding, foundingState, report);
            CheckResponsiveLayout(creation, region, founding, support,
                regionEditors, setup, culture, report);
            CheckEstablishedState(setup, culture, report);
            CheckTechnologyOwnership(region, report);
            CheckSharedAuthoring(founding, culture, support, report);
            CheckCopyIsolation(presentation, culture, report);
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

    private static void CheckCulture(string culture, string support,
        string facilities, StringBuilder report)
    {
        Require(culture.Contains("CACulture.CurrentSchemaVersion = 2")
            || culture.Contains("const int CurrentSchemaVersion = 2"),
            "culture schema 2 is absent");
        foreach (string field in new[]
        {
            "hospitalityKey", "mealsKey", "remembranceKey", "profileKey"
        })
            Require(culture.Contains("Scribe_Values.Look(ref " + field),
                "culture field is not serialized: " + field);
        foreach (string domain in new[]
        {
            "Key = \"gathering\"", "Key = \"hospitality\"",
            "Key = \"meals\"", "Key = \"remembrance\""
        })
            Require(culture.Contains(domain),
                "culture domain is absent: " + domain);
        foreach (string consumer in new[]
        {
            "Campfire", "Bedroll", "Table2x2c", "Sarcophagus", "PartySpot"
        })
            Require(culture.Contains("\"" + consumer + "\""),
                "culture domain has no verified furnishing contract: " + consumer);
        Require(culture.Contains("foreach (CACultureDomainDef domain in "
                + "CACultureModel.Domains)")
            && culture.Contains("option.ThingDefName")
            && culture.Contains("option.StuffDefName"),
            "culture materialization does not consume every shipped domain");
        Require(facilities.Contains(
                "CACultureMaterialization.Furnish(record, Next")
            && facilities.Contains("CACultureMaterialization.StyleFor("),
            "culture materialization has no settlement-furnishing caller");
        string furnish = Slice(culture,
            "internal static void Furnish(CARegionalSettlementRecord",
            "internal static class CAFactionStartingState");
        Require(!furnish.Contains("EnsureGenerated")
            && furnish.Contains("CompatibilityFailure")
            && furnish.Contains("catch (Exception exception)"),
            "settlement furnishing mutates culture or lacks per-domain containment");
        var expectedThings = new Dictionary<string, string>
        {
            ["hearth"] = "Campfire", ["feast"] = "Table3x3c",
            ["waymeet"] = "HorseshoesPin",
            ["memorial"] = "SculptureSmall",
            ["guest_bedding"] = "Bedroll",
            ["open_seating"] = "Stool",
            ["hosted_seating"] = "DiningChair",
            ["private_receiving"] = "Armchair",
            ["household_tables"] = "Table2x2c",
            ["common_table"] = "Table3x3c",
            ["fireside_meals"] = "Campfire",
            ["carved_memory"] = "SculptureSmall",
            ["burial_memory"] = "Sarcophagus",
            ["assembly_marker"] = "PartySpot"
        };
        MatchCollection optionContracts = Regex.Matches(culture,
            "OptionDef\\(\\\"(?<key>[^\\\"]+)\\\",\\s*\\\"[^\\\"]+\\\","
                + "\\s*\\\"[^\\\"]+\\\",\\s*\\\"[^\\\"]+\\\","
                + "\\s*\\\"(?<thing>[^\\\"]+)\\\"",
            RegexOptions.Singleline);
        var realized = optionContracts.Cast<Match>().ToDictionary(
            match => match.Groups["key"].Value,
            match => match.Groups["thing"].Value);
        foreach (KeyValuePair<string, string> expected in expectedThings)
            Require(realized.TryGetValue(expected.Key, out string thing)
                && thing == expected.Value,
                "culture practice has the wrong placed output: "
                    + expected.Key);

        string[] stableKeys =
        {
            "hearth_common", "road_exchange", "memorial_households",
            "festival_market"
        };
        string[] oldLabels =
        {
            "Hearth customs", "Traveling customs", "Memorial customs",
            "Festival customs"
        };
        foreach (string value in stableKeys.Concat(oldLabels))
            Require(culture.Contains("\"" + value + "\""),
                "culture profile identity or alias missing: " + value);
        foreach (string style in new[]
        {
            "Rustican", "Corunan", "Sophian", "Astropolitan"
        })
            Require(culture.Contains("ProfilePositions(\"" + style + "\""),
                "culture profile lacks a distinct visual source: " + style);

        string migration = Slice(culture, "internal static void Migrate(CACulture",
            "internal static void EnsureGenerated");
        Require(migration.Contains("culture.presetName = preset.Key")
            && migration.Contains("if (!culture.Value(pair.Key).NullOrEmpty()) continue")
            && !migration.Contains("authoredMask = 0"),
            "culture migration does not preserve existing authored values");
        Require(culture.Contains("culture.schemaVersion = "
                + "CACulture.CurrentSchemaVersion")
            && culture.Contains("Roadside meeting place")
            && culture.Contains("CompatibilityFailure(CACulture culture)"),
            "culture migration or corrected display casing is incomplete");
        Require(support.Contains("Visual style source:")
            && support.Contains("CultureDetails")
            && support.Contains("CultureIdentity"),
            "culture profile overview omits its substantive identity");
        report.AppendLine("PASS culture -> schema 2, aliases, four materialized "
            + "practice domains, distinct visual profiles");
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
        string culture, StringBuilder report)
    {
        string cultureCopy = Slice(culture, "internal CACulture Copy()",
            "internal void CopyFrom(CACulture source)");
        foreach (string field in new[] { "hospitalityKey", "mealsKey",
            "remembranceKey", "authoredMask", "presetMask" })
            Require(cultureCopy.Contains(field + " = " + field),
                "culture clone omits " + field);
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

        XElement state = new XElement("state",
            new XElement("culture", "road_exchange"),
            new XElement("politics", "worker_federation"));
        string before = state.ToString(SaveOptions.DisableFormatting);
        foreach (string detail in new[] { "Compact", "Standard", "Expanded" })
            _ = detail + ":" + state.Element("culture")?.Value;
        Require(before == state.ToString(SaveOptions.DisableFormatting),
            "presentation-detail test changed authored state");
        report.AppendLine("PASS deterministic invariants -> profile edits are "
            + "copy-isolated; detail selection does not alter state");
    }

    private static void CheckFixture(string path, StringBuilder report)
    {
        XDocument document = XDocument.Load(path,
            LoadOptions.PreserveWhitespace);
        XElement plan = document.Root?.Element("plan")
            ?? throw new InvalidDataException("fixture has no plan element");
        List<XElement> factions = Items(plan, "factions");
        List<XElement> settlements = Items(plan, "settlements");
        int groups = settlements.Sum(item =>
            Items(item, "populationGroups").Count);
        Require(factions.Count == 3 && settlements.Count == 4 && groups == 9,
            "fixture composition changed from 3 factions, 4 settlements, 9 groups");

        var cultures = factions.Select(item => item.Element("culture"))
            .Concat(new[] { plan.Element("playerFounding")?.Element("culture") })
            .Where(item => item != null).ToList();
        Require(cultures.Count == 4,
            "fixture does not contain three faction cultures and one founding culture");
        var allowedCultureKeys = new Dictionary<string, HashSet<string>>
        {
            ["gatheringKey"] = new HashSet<string>
                { "hearth", "feast", "waymeet", "memorial" },
            ["hospitalityKey"] = new HashSet<string>
                { "guest_bedding", "open_seating", "hosted_seating",
                    "private_receiving" },
            ["mealsKey"] = new HashSet<string>
                { "household_tables", "common_table", "fireside_meals" },
            ["remembranceKey"] = new HashSet<string>
                { "carved_memory", "burial_memory", "assembly_marker" }
        };
        foreach (XElement culture in cultures)
        {
            Require(Value(culture, "schemaVersion") == "2"
                && !string.IsNullOrEmpty(Value(culture, "gatheringKey"))
                && !string.IsNullOrEmpty(Value(culture, "hospitalityKey"))
                && !string.IsNullOrEmpty(Value(culture, "mealsKey"))
                && !string.IsNullOrEmpty(Value(culture, "remembranceKey")),
                "fixture culture is not converged to schema 2");
            foreach (KeyValuePair<string, HashSet<string>> domain in
                allowedCultureKeys)
                Require(domain.Value.Contains(Value(culture, domain.Key)),
                    "fixture culture has an unavailable " + domain.Key);
        }

        var politics = factions.Select(item => item.Element("politicalBeliefs"))
            .Concat(new[]
            {
                plan.Element("playerFounding")?.Element("politicalBeliefs")
            }).Where(item => item != null).ToList();
        Require(politics.Count == 4
            && politics.All(item => Value(item, "schemaVersion") == "2"),
            "fixture political beliefs are not converged to schema 2");

        string serialized = document.ToString(SaveOptions.DisableFormatting);
        string before = Hash(serialized);
        string after = Hash(XDocument.Parse(serialized,
                LoadOptions.PreserveWhitespace)
            .ToString(SaveOptions.DisableFormatting));
        Require(before == after, "fixture changed across XML readback");
        report.AppendLine("PASS keyed fixture -> schema 2 authoring state; "
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
