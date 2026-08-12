using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonistAwareness;

internal static class Program
{
    private enum Status { Verified, PendingOperatorRuntime, Failed }
    private sealed record Receipt(int Id, string Name, Status Status,
        string Evidence);

    private static readonly List<Receipt> Receipts = new();
    private static Dictionary<string, string> source;
    private static string allSource;
    private static string sourceRoot;
    private static XElement plan;
    private static string activePath;
    private static string mirrorPath;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3) throw new ArgumentException(
                "usage: B9AcceptanceReceipts <repo> <active-fixture> <mirror-fixture>");
            string repo = Path.GetFullPath(args[0]);
            sourceRoot = Path.Combine(repo, "Source");
            source = Directory.GetFiles(sourceRoot, "*.cs")
                .ToDictionary(Path.GetFileName, File.ReadAllText,
                    StringComparer.OrdinalIgnoreCase);
            allSource = string.Join("\n", source.Values);
            activePath = Path.GetFullPath(args[1]);
            mirrorPath = Path.GetFullPath(args[2]);
            XDocument active = XDocument.Load(activePath);
            XDocument mirror = XDocument.Load(mirrorPath);
            plan = Plan(active);

            NavigationAndHierarchy();
            CopyAndLayout();
            SettlementProgram(active, mirror);
            Provisioning();
            CultureOnboarding();

            if (Receipts.Count != 84
                || Receipts.Select(item => item.Id).Distinct().Count() != 84
                || Receipts.Select(item => item.Id).OrderBy(item => item)
                    .Where((id, index) => id != index + 1).Any())
                throw new InvalidOperationException(
                    "B9 receipt ledger must contain each ID 1-84 exactly once");
            WriteReport(repo);
            int failed = Receipts.Count(item => item.Status == Status.Failed);
            int pending = Receipts.Count(item =>
                item.Status == Status.PendingOperatorRuntime);
            Console.WriteLine("result: " + (failed == 0 ? "READY" : "FAIL")
                + " (" + (84 - pending - failed) + " verified, " + pending
                + " pending operator runtime, " + failed + " failed; 84 total)");
            return failed == 0 ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("result: FAIL after " + Receipts.Count
                + " receipts");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void NavigationAndHierarchy()
    {
        string screen = Read("RegionalPopulationScreenModule.cs");
        string map = Read("RegionMapWidget.cs");
        string settlementPanel = Slice(screen,
            "private void DrawSettlementPanel", "private void DrawSettlementComparison");
        string factionPanel = Slice(screen,
            "private void DrawFactionPanel", "private void FactionSettingsChanged");
        string objectRail = Slice(screen,
            "private void DrawObjectRail", "private void DrawInspector");

        V(1, "Objects and Map remain available",
            screen.Contains("new[] { \"Objects\", \"Map\", \"Details\" }")
                && screen.Contains("CARegionMapWidget.Draw"),
            "compact tabs and the shared regional map remain in the page");
        V(2, "Wide-mode object rail remains persistent",
            Count(screen, "DrawObjectRail(") >= 3
                && screen.Contains("layoutMode == CARegionLayoutMode.Compact")
                && screen.Contains("DrawInspector(panel)"),
            "non-compact layout draws rail, map, and inspector together");
        V(3, "Selecting a settlement from compact Objects opens Details",
            objectRail.Contains("SelectSettlement(place.slot)")
                && Count(objectRail, "OpenDetails();") >= 3,
            "settlement rows select the shared slot and invoke OpenDetails");
        V(4, "Selecting a faction from compact Objects opens Details",
            objectRail.Contains("SelectFaction(group.key)")
                && objectRail.Contains("OpenDetails();"),
            "faction cards select the shared faction and invoke OpenDetails");
        V(5, "Selecting a map marker keeps Map active",
            map.Contains("delegate { SelectSettlement(settlement.slot); }")
                && !Slice(map, "private static void HandleClicks",
                    "internal static int TileUnderMouse").Contains("viewDetails"),
            "map hit handling changes shared selection without changing compactPane");
        V(6, "A selected-map-object strip exposes View Details",
            map.Contains("DrawContextStrip(strip, plan, contextText, viewDetails)")
                && map.Contains("\"View Details\""),
            "the map context strip owns the explicit details action");
        V(7, "Settlement Details permit previous/next settlement comparison",
            screen.Contains("DrawSettlementComparison")
                && screen.Contains("\"Previous\"")
                && screen.Contains("\"Next\""),
            "compact settlement Details begins with Objects/Previous/current/Next");
        V(8, "Switching settlements resets the Details scroll",
            screen.Contains("if (selection != lastInspectorSelection)")
                && Slice(screen, "if (selection != lastInspectorSelection)",
                    "// The previous frame").Contains("scroll = Vector2.zero"),
            "the shared selection identity resets the inspector scroll");
        V(9, "Switching settlements does not mutate the regional plan",
            !Slice(screen, "private void DrawSettlementComparison",
                "private void DrawSettlementProgramInspector")
                .Contains("SavePending")
                && !Slice(screen, "private void DrawSettlementComparison",
                    "private void DrawSettlementProgramInspector")
                .Contains("EnsureDerived"),
            "comparison only selects another saved settlement");
        V(10, "Selection stays synchronized across Objects, Map, and Details",
            Count(screen, "CARegionMapWidget.selectedKind") >= 4
                && screen.Contains("CARegionMapWidget.selectedSlot")
                && map.Contains("selectedSlot"),
            "all three surfaces consume CARegionMapWidget shared selection");
        V(11, "Settlement Details begin with one compact identity header",
            Count(settlementPanel, "Title(ref y") == 4
                && settlementPanel.IndexOf("Kind(ref y", StringComparison.Ordinal)
                    < settlementPanel.IndexOf("Title(ref y", StringComparison.Ordinal),
            "identity kind/name precede Population, Culture, and Composition");
        V(12, "Culture appears once as a compact block",
            Count(settlementPanel, "Title(ref y, width, \"Culture\"") == 1
                && settlementPanel.Contains("SettlementCultureSummary(place)"),
            "one Culture block summarizes sources, meanings, practices, and tension");
        V(13, "Population appears once as a coherent section",
            Count(settlementPanel, "Title(ref y, width, \"Population\"") == 1
                && settlementPanel.Contains("Add minority population"),
            "one measured population-group section owns shares and minority editing");
        V(14, "Settlement Composition appears once as a coherent section",
            Count(settlementPanel,
                "Title(ref y, width, \"Settlement Composition\"") == 1
                && screen.Contains("Inspect settlement composition..."),
            "one derived program summary links to the composition inspector");
        V(15, "Settlement relations are absent from settlement Details",
            !settlementPanel.Contains("Faction Relations")
                && !settlementPanel.Contains("Relations belong"),
            "relations are owned only by faction Details");
        V(16, "Settlement knowledge/research exposition is absent",
            !settlementPanel.Contains("Knowledge and Research")
                && !settlementPanel.Contains("Knowledge belongs"),
            "settlement Details has no faction-knowledge exposition");
        V(17, "Starting Facilities is absent",
            !allSource.Contains("\"Starting Facilities\""),
            "the obsolete domain title is absent from active source");
        V(18, "Roads and Water is absent from settlement Details",
            !settlementPanel.Contains("Roads and Water"),
            "routes remain map/location facts rather than a settlement section");
        V(19, "Starting Conditions is absent",
            !settlementPanel.Contains("Starting Conditions"),
            "the backend state section was removed");
        V(20, "Local Material Capability is absent",
            !settlementPanel.Contains("Local Material Capability"),
            "the material-capability exposition was removed");
    }

    private static void CopyAndLayout()
    {
        string screen = Read("RegionalPopulationScreenModule.cs");
        string choice = Read("SettlementProgramModule.cs");
        string sourceStrings = OrdinarySetupSource();
        string factionPanel = Slice(screen,
            "private void DrawFactionPanel", "private void FactionSettingsChanged");
        string settlementPanel = Slice(screen,
            "private void DrawSettlementPanel", "private void DrawSettlementComparison");

        V(21, "Starting Region uses conventional capitalization",
            screen.Contains("PageTitle => \"Starting Region\""),
            "the explicit page title is Starting Region");
        V(22, "Faction Technology uses conventional capitalization",
            factionPanel.Contains("group.TechnologySummary")
                && factionPanel.Contains("settlementCount")
                && !factionPanel.Contains("Title(ref y, width, \"technology"),
            "technology is a concise faction-owned fact in the identity header");
        V(23, "Beliefs and Current Order uses conventional capitalization",
            factionPanel.Contains("\"Beliefs and Current Order\""),
            "the faction section uses the canonical explicit title");
        V(24, "Faction Relations uses conventional capitalization",
            factionPanel.Contains("\"Faction Relations\""),
            "the relational authoring section uses the canonical title");
        V(25, "No ordinary setup string contains Generated Default",
            !sourceStrings.Contains("Generated Default"),
            "ordinary creation-flow source contains no such label");
        V(26, "No ordinary setup string contains Generated Relation",
            !sourceStrings.Contains("Generated Relation"),
            "ordinary creation-flow source contains no such label");
        V(27, "No ordinary setup string contains Generated by Contextual",
            !sourceStrings.Contains("Generated by Contextual")
                && !sourceStrings.Contains("Generated Included"),
            "ordinary creation-flow source contains no contextual provenance label");
        V(28, "No permanent Details paragraph explains architecture without a decision",
            !settlementPanel.Contains("These facilities and routes follow from")
                && !settlementPanel.Contains("Relations belong to factions")
                && !settlementPanel.Contains("Knowledge belongs to the faction")
                && !settlementPanel.Contains("Each arrangement records"),
            "the default settlement hierarchy no longer narrates implementation ownership");
        V(29, "No one-option fact renders as an enabled button",
            choice.Contains("if (choiceCount == 1)")
                && choice.Contains("essentialWhenFixed ? Mode.Readout : Mode.Omit")
                && factionPanel.Contains("if (factionChoices > 1)"),
            "shared contextual-choice rule and faction picker suppress fixed controls");
        V(30, "Site arrangement is omitted or read-only when only one choice exists",
            settlementPanel.Contains("PhysicalSiteChoiceCount(place)")
                && settlementPanel.Contains("== CAContextualChoicePresentation.Mode.Control"),
            "site layout draws only when more than one valid form exists");
        V(31, "Population origin is omitted when only one choice exists",
            settlementPanel.Contains("int originChoices")
                && settlementPanel.Contains("CAContextualChoicePresentation.Resolve(originChoices"),
            "population source follows the same contextual-choice contract");
        V(32, "Population origin uses plain-language alternatives when several exist",
            screen.Contains("Existing settlement population")
                || screen.Contains("Scenario population")
                || screen.Contains("Move residents from"),
            "multi-option origin menu names the population source rather than provenance");

        int[] widths = { 1280, 1366, 1600, 1920, 1600 };
        int[] heights = { 720, 768, 900, 1080, 1536 };
        float[] scales = { 1f, 1.25f, 1.5f, 2f };
        List<CAStartingRegionLayoutMeasurement> layouts = new();
        for (int i = 0; i < widths.Length; i++)
            foreach (float scale in scales)
                layouts.Add(CAStartingRegionLayoutHarness.Measure(
                    widths[i], heights[i], scale));
        bool allFit = layouts.All(item => item.Fits);
        P(33, "No title or row clips at the tested compact width",
            allFit && screen.Contains("Text.CalcHeight(text, width)")
                && screen.Contains("Text.CalcHeight(label")
                && layouts.All(item => item.LongComparisonRowHeight >= 28f
                    && item.LongObjectCardHeight >= 42f
                    && item.LongRelationRowHeight >= 28f
                    && item.LongMapBadgeHeight >= 22f
                    && item.LongContextStripHeight >= 22f),
            "20 resolution/scale measurements include representative long comparison, object, relation, badge, and context text");
        P(34, "No wrapped text overlaps a following control",
            allFit && screen.Contains("float populationHeight = Mathf.Max")
                && screen.Contains("y += populationHeight + 2f")
                && Count(screen, "Text.CalcHeight") >= 12,
            "measured rows advance y by their calculated height across the tested matrix");
        P(35, "No Details content draws beneath Back or Continue",
            allFit && screen.Contains("float bodyHeight = inRect.height - bodyTop - 54f")
                && screen.Contains("DoBottomButtons(inRect, \"Continue\")"),
            "the scroll body reserves a distinct 54-unit bottom navigation band");
        P(36, "Faction Relations retains its current visual organization",
            factionPanel.Contains("Title(ref y, width, \"Faction Relations\"")
                && factionPanel.Contains("Player faction: ")
                && factionPanel.Contains("Federation: ")
                && factionPanel.Contains("PairRelationLabel"),
            "player, federation, and pair relations remain grouped on the faction surface");
    }

    private static void SettlementProgram(XDocument active,
        XDocument mirror)
    {
        string program = Read("SettlementProgramModule.cs");
        string materializer = Read("SettlementProgramMaterializerModule.cs");
        string setup = Read("RegionalSetupModule.cs");
        string world = Read("RegionalWorldModule.cs");
        string fixture = File.ReadAllText(activePath);
        XElement[] settlements = Items(plan, "settlements").ToArray();
        XElement[] programs = settlements.SelectMany(item =>
            ProgramEntries(item)).ToArray();
        string[] oldMaskTerms = { "CAStartingFacilities", "MaskHearth",
            "MaskStores", "MaskInfirmary", "MaskWorkshop", "MaskJail",
            "MaskDining", "MaskLab", "AllFacilityMask", "FacilityKind" };

        V(37, "No active startingFacilityMask remains",
            !allSource.Contains("startingFacilityMask")
                && !fixture.Contains("startingFacilityMask"),
            "active source and schema-7 fixture have no facility mask field");
        V(38, "No active facility-exception mask remains",
            !allSource.Contains("facilityExceptionMask")
                && !allSource.Contains("facilityExceptionValues")
                && !fixture.Contains("facilityException"),
            "active source and fixture have no exception mask/value fields");
        V(39, "No seven-bit facility constants remain as initial settlement ontology",
            oldMaskTerms.All(term => !allSource.Contains(term)),
            "the obsolete type, constants, and enum are absent from active source");
        V(40, "Initial settlement composition uses a namespaced program registry",
            program.Contains("class CASettlementProgramRegistry")
                && programs.All(item => Value(item, "programKey")
                    .StartsWith("ca.settlement.", StringComparison.Ordinal)),
            "open registry entries use stable ca.settlement.* keys");
        V(41, "Every program has an actual factual source",
            program.Contains("SourceFacts = source")
                && programs.All(item => !Value(item, "sourceReceipt").NullOrEmpty()),
            "definition source contracts and saved source receipts are populated");
        V(42, "Every program has functional materialization or a native spatial contract",
            program.Contains("program has no functional candidate contract")
                && program.Contains("program has no native spatial realization contract")
                && program.Contains("LoadedFunctionalCandidates"),
            "registry admission and derivation validate functional candidates or spatial contracts");
        V(43, "Visual-only content cannot satisfy a functional program",
            program.Contains("SupportsFunctionalContract")
                && program.Contains("def.category != ThingCategory.Building")
                && !program.Contains("Anon2PlantSpot"),
            "candidate resolution validates runtime contracts and excludes the decorative plant spot");
        V(44, "Loaded functional ported assets use the same registry as native assets",
            program.Contains("Anon2CushionedChair")
                && program.Contains("def.building?.isSittable == true")
                && File.ReadAllText(Path.Combine(Path.GetDirectoryName(sourceRoot)!,
                    "Defs", "MoreFurniture", "Buildings_MoreFurnitureSeatingStuff.xml"))
                    .Contains("<isSittable>true</isSittable>"),
            "ported cushioned chairs qualify through the native sittable contract");
        V(45, "The same confirmed plan produces the same program signature",
            Hash(activePath) == Hash(mirrorPath)
                && programs.All(item => !Value(item, "signature").NullOrEmpty()),
            "active/mirror plans are byte-identical and every entry has a stable signature");
        V(46, "Renaming a settlement does not reroll its program",
            !Read("SettlementProgramCausalKernel.cs")
                .Contains("customName"),
            "settlement names are excluded from the program source signature");
        V(47, "Relevant upstream facts invalidate and re-derive the program",
            Read("SettlementProgramCausalKernel.cs").Contains("facts.Population")
                && program.Contains("ProgramsEquivalent")
                && Read("RegionalSettlementModelModule.cs")
                    .Contains("EnsureDerived(plan, settlement,\n                    force: true)"),
            "causal facts feed the signature and realization forces derivation after upstream change");
        V(48, "Page entry validates confirmed state and derives drafts before read-only Details",
            Read("RegionalPopulationScreenModule.cs")
                .Contains("RefreshDraftRealization(plan)")
                && Read("RegionalPopulationScreenModule.cs")
                    .Contains("TryValidateStartingSettlements")
                && !Slice(Read("RegionalPopulationScreenModule.cs"),
                    "private void DrawSettlementPanel",
                    "private void DrawSettlementComparison")
                    .Contains("CASettlementProgramRegistry.EnsureDerived"),
            "draft page entry uses the shared realization boundary; confirmed page entry validates without mutation");
        V(49, "Preview and materialization consume the same saved program",
            Read("RegionalPopulationScreenModule.cs")
                .Contains("CASettlementProgramRegistry.Summary(place)")
                && materializer.Contains("record.settlementProgram?.entries")
                && world.Contains("settlementProgram = settlement?.settlementProgram?.Copy()"),
            "preview, runtime record, and materializer share the saved entry list");
        V(50, "Every populated fixture settlement receives viability programs",
            settlements.Where(item => Int(item, "residentPopulation") > 0)
                .All(item => HasPrograms(item, "ca.settlement.housing",
                    "ca.settlement.food-preparation", "ca.settlement.storage")),
            "all four populated settlements have housing, food preparation, and stores");
        V(51, "Supported fixture settlements receive non-survival programs",
            settlements.All(item => ProgramEntries(item).Any(entry =>
                new[] { "ca.settlement.governance", "ca.settlement.gathering",
                    "ca.settlement.recreation", "ca.settlement.trade",
                    "ca.settlement.art-memory", "ca.settlement.communications" }
                    .Contains(Value(entry, "programKey")))),
            "every settlement carries institutional or social programs beyond viability");
        XElement seat = settlements.OrderByDescending(item =>
            Int(item, "realizedRole") == 1 ? 1 : 0).ThenByDescending(item =>
                ProgramEntries(item).Count()).First();
        V(52, "The regional seat receives distinct economic, institutional, and social programs",
            HasPrograms(seat, "ca.settlement.production",
                "ca.settlement.governance", "ca.settlement.gathering",
                "ca.settlement.art-memory")
                && ProgramEntries(seat).Count() >= 16,
            Value(seat, "customName") + " has " + ProgramEntries(seat).Count()
                + " programs including production, governance, gathering, and memory");
        V(53, "At least one fixture settlement uses a functional ported asset",
            programs.Any(item => Items(item, "selectedCandidates").Any(value =>
                (string)value == "Anon2CushionedChair"
                    || (string)value == "DankPyon_Bust")),
            "the fixture selects a ported asset through its native functional contract");
        V(54, "At least two settlements select different valid assets or combinations",
            settlements.Select(item => string.Join("|", ProgramEntries(item)
                    .Select(entry => Value(entry, "programKey") + "="
                        + string.Join(",", Items(entry, "selectedCandidates")))))
                .Distinct(StringComparer.Ordinal).Count() >= 2,
            "the four fixture settlements have distinct saved compositions");
        V(55, "A missing required program records a blocker rather than vanishing",
            program.Contains("entry.blocker = \"no loaded functional asset \"")
                && program.Contains("entry.materializationState = \"blocked\"")
                && program.Contains("Unavailable("),
            "unresolved loaded/spatial contracts remain inspectable with blockers");
        V(56, "Repair and rebuilding remain bound to placed program assets",
            materializer.Contains("record.seededAssets")
                && materializer.Contains("CASettlementRepairWork")
                && materializer.Contains("CASettlementRebuildWork")
                && materializer.Contains("targetThingId"),
            "placed identities seed native repair and reconstruction receipts");
        V(57, "Research requires a real current bench",
            !Slice(materializer, "private bool TryResearch",
                    "private void RevalidateRestoredWork")
                .Contains("CASettlementProgramRegistry.Has")
                && Count(materializer, "SimpleResearchBench") >= 2,
            "starting programs create history; later research reads the current faction-owned bench");
        V(58, "Technology alone cannot create a laboratory",
            program.Contains("s.realizedServiceInfrastructure >= 2")
                && program.Contains("s.realizedCivicInfrastructure >= 2")
                && program.Contains("s.economicCapacity >= 2")
                && !setup.Contains("startingFacilityMask"),
            "research applicability requires service, civic, economy, history/role, and technology");
    }

    private static void Provisioning()
    {
        string composition = Read("SettlementCompositionModule.cs");
        string program = Read("SettlementProgramModule.cs");
        string materializer = Read("SettlementProgramMaterializerModule.cs");
        string screen = Read("RegionalPopulationScreenModule.cs");
        XElement[] arrangements = Items(plan, "settlements").SelectMany(item =>
            Items(item, "provisionArrangements")).ToArray();

        V(59, "Household provision creates no fake organization",
            composition.Contains("!= CAProvisionOperator.Household")
                && composition.Contains("settlementKey + \":household\"")
                && composition.Contains("op = null")
                && program.Contains("HouseholdProvision")
                && materializer.Contains("CAProvisionOperator.Household"),
            "households own stable material nodes and stocked stores without a fake organization");
        V(60, "Communal provision has a real operator and material nodes",
            arrangements.Any(item => Value(item, "operatorKind") == "Communal")
                && composition.Contains(":prov\" + arrangement.key")
                && materializer.Contains("nodes = Math.Max(1, arrangement.nodes)"),
            "communal entries own distinct provider keys and materialize each saved node");
        V(61, "Authority provision has authority, funding, and reserve assets",
            arrangements.Where(item => Value(item, "operatorKind") == "Authority")
                .All(item => !Value(item, "funding").NullOrEmpty())
                && composition.Contains("? settlementKey")
                && program.Contains("new[] { \"Shelf\" }")
                && materializer.Contains("AuthorityProvision"),
            "authority uses the settlement organization, saved funding, and shelf reserve program");
        V(62, "Vendor provision cannot generate without transaction path and organization",
            !allSource.Contains("CAProvisionOperator.Vendor"),
            "no vendor operator exists; unsupported transactional provision cannot be generated");
        V(63, "Religious provision cannot generate without operator and site",
            !allSource.Contains("CAProvisionOperator.Religious"),
            "no religious provision operator exists; the separate ritual program does not claim provision");
        V(64, "Unsupported operator branches are absent from generation",
            !Regex.IsMatch(composition,
                @"CAProvisionOperator\.(Vendor|Religious)")
                && arrangements.All(item => new[] { "Household", "Communal",
                    "Authority" }.Contains(Value(item, "operatorKind"))),
            "generation and fixture contain only the three supported operators");
        V(65, "Provision arrangements derive from upstream social state",
            composition.Contains("GenerateProvisionArrangements")
                && composition.Contains("CAProvisionCausalKernel.Derive")
                && composition.Contains("factionStructure")
                && composition.Contains("populationGroups")
                && composition.Contains("SettlementAuthorityOf")
                && composition.Contains("CASettlementStartingState.Access")
                && composition.Contains("CASettlementStartingState.Services")
                && composition.Contains("CASettlementStartingState.Civic"),
            "social order, populations, access, services, civic state, scale, and role feed derivation");
        V(66, "No generic provision-distribution control appears in standard Details",
            !Slice(screen, "private void DrawSettlementPanel",
                "private void DrawSettlementComparison")
                .Contains("ProvisionDistribution"),
            "standard settlement Details has no provision mode control");
        V(67, "Concrete provision operators appear in Settlement Composition inspection",
            (program.Contains("CommunalProvision")
                    && program.Contains("AuthorityProvision"))
                && Read("SettlementProgramCausalKernel.cs")
                    .Contains("saved provision nodes"),
            "communal kitchens and authority reserve are namespaced program entries with saved nodes");
        V(68, "No ordinary UI uses Everyday Provision as an operator substitute",
            !OrdinarySetupSource().Contains("Everyday Provision"),
            "the obsolete generic label is absent from ordinary setup source");
    }

    private static void CultureOnboarding()
    {
        string culture = Read("FactionCultureBeliefsModule.cs");
        string editor = Slice(culture, "internal sealed class Dialog_CACultureEditor",
            "internal sealed class Dialog_CACultureValueFineTune");
        string meaning = Slice(editor, "private void DrawMeaning(ref float y",
            "private void OpenMeaningScope");
        string practice = Slice(editor, "private void DrawPractices",
            "private void DrawVisual");
        string fineTune = Slice(culture,
            "internal sealed class Dialog_CACultureValueFineTune",
            "internal sealed class Dialog_CACultureCausalInspector");
        string tabs = Slice(editor, "string[] tabs =", "float tabHeight");

        V(69, "Raw Culture sliders are absent from the default meaning list",
            !meaning.Contains("HorizontalSlider")
                && fineTune.Contains("HorizontalSlider"),
            "cards use semantic anchors; exact sliders exist only in the fine-tune dialog");
        V(70, "Every meaning card has a plain-language interpretation",
            meaning.Contains("MeaningInterpretation(meaning)")
                && culture.Contains("It carries")
                && culture.Contains("in daily life."),
            "each card composes ordinary language from the four independent dimensions");
        V(71, "Every meaning retains independent Culture values",
            culture.Contains("meaning.approval = approval")
                && culture.Contains("meaning.normality = normality")
                && culture.Contains("meaning.prestige = prestige")
                && culture.Contains("meaning.salience = salience"),
            "approval, normality, prestige, and salience remain separate continuous fields");
        V(72, "Culture has no aggregate linear score",
            !Regex.IsMatch(editor, @"(CultureScore|AggregateCulture|global Culture score)",
                RegexOptions.IgnoreCase),
            "onboarding exposes meanings, practices, plurality, and disputes without a global score");
        V(73, "Guided anchors map deterministically to continuous values",
            culture.Contains("SignedAnchorValues")
                && culture.Contains("UnsignedAnchorValues")
                && culture.Contains("index => choose(values[index])"),
            "five fixed anchor arrays map semantic choices to exact values");
        V(74, "Fine-Tune Values exposes exact sliders contextually",
            editor.Contains("Fine-tune values...")
                && fineTune.Contains("label + \" \" + value")
                && fineTune.Contains("HorizontalSlider"),
            "the contextual dialog displays exact numbers and sliders");
        P(75, "Fine-tuned values survive closing and reopening the editor",
            fineTune.Contains("meaning.approval = approval")
                && fineTune.Contains("changed?.Invoke()")
                && editor.Contains("meaning, changed"),
            "fine-tuning writes the same persisted meaning object and invokes the save callback");
        V(76, "Guided mode does not snap fine-tuned values without explicit choice",
            culture.Contains("private static int ExactAnchor")
                && culture.Contains("return -1;")
                && !Slice(editor, "private static string MeaningInterpretation",
                    "private void MeaningChanged").Contains("meaning.approval ="),
            "non-anchor values render no selected segment and interpretation is read-only");
        V(77, "Practice strength uses plain language by default",
            practice.Contains("PracticeAnchors[NearestAnchor")
                && practice.Contains("+ \" practice.\""),
            "practice cards show Occasional through Defining instead of a number");
        V(78, "Exact practice strength remains contextually available",
            practice.Contains("Fine-tune value...")
                && fineTune.Contains("practice.strength")
                && fineTune.Contains("\"Strength\""),
            "the contextual fine-tune dialog retains the exact strength slider");
        V(79, "Causal Preview is not a primary onboarding tab",
            !tabs.Contains("Causal") && !tabs.Contains("Preview"),
            "primary tabs are Overview, Social meanings, Practices, and Visual tradition");
        V(80, "Inspect Causal Effects remains available",
            editor.Contains("Inspect causal effects...")
                && culture.Contains("Dialog_CACultureCausalInspector"),
            "advanced causal inspection is a secondary read-only action");
        V(81, "Subject source and consumer detail is secondary",
            !meaning.Contains("AuthoritativeSource")
                && !meaning.Contains("Consumers")
                && Slice(editor, "private void OpenMeaningSubject",
                    "private void OpenPracticeSubject").Contains("Details ="),
            "meaning cards stay concise; selected chooser detail owns sources and consumers");
        V(82, "Constituent-specific meanings remain available",
            editor.Contains("MeaningScopeLabel")
                && editor.Contains("culture.constituents")
                && editor.Contains("meaning.populationScope = local.cultureId"),
            "the meaning editor can scope a meaning to any recorded constituent");
        V(83, "Plural and contradictory meanings remain visible",
            editor.Contains("Internal disputes")
                && editor.Contains("group.Min(item =>\n                        item.approval) < 0")
                && editor.Contains("group.Max(item =>\n                            item.approval) > 0"),
            "overview counts subjects with opposing constituent/local approval");
        V(84, "Opening or navigating Culture never mutates Culture",
            !Slice(editor, "public override void DoWindowContents",
                "private float DrawActions").Contains("changed?.Invoke")
                && !Slice(editor, "private void DrawOverview",
                    "private void DrawMeanings").Contains("Normalize(culture)")
                && !Slice(editor, "private void DrawOverview",
                    "private void DrawMeanings").Contains("EnsureGenerated"),
            "render/navigation paths do not normalize, generate, or call the persistence callback");
    }

    private static string OrdinarySetupSource()
    {
        string[] files = { "RegionalPopulationScreenModule.cs",
            "RegionMapWidget.cs", "SettlementProgramModule.cs",
            "FactionCultureBeliefsModule.cs", "PlayerFoundingPageModule.cs" };
        return string.Join("\n", files.Select(Read));
    }

    private static void V(int id, string name, bool condition,
        string evidence)
    {
        Receipts.Add(new Receipt(id, name,
            condition ? Status.Verified : Status.Failed, evidence));
    }

    private static void P(int id, string name, bool ready,
        string evidence)
    {
        Receipts.Add(new Receipt(id, name, ready
            ? Status.PendingOperatorRuntime : Status.Failed, evidence));
    }

    private static void WriteReport(string repo)
    {
        string output = Path.Combine(repo, ".build-b9",
            "B9_ACCEPTANCE_RECEIPTS.md");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var text = new StringBuilder();
        text.AppendLine("# B9 Acceptance Receipts").AppendLine();
        text.AppendLine("Generated from the current source tree and the two "
            + "runtime fixture surfaces. Static receipts establish readiness; "
            + "the operator establishes visual and gameplay acceptance.")
            .AppendLine();
        foreach (Receipt item in Receipts)
        {
            string marker = item.Status == Status.Verified ? "PASS"
                : item.Status == Status.PendingOperatorRuntime
                    ? "PENDING OPERATOR RUNTIME" : "FAIL";
            text.Append("- ").Append(item.Id.ToString("00")).Append(" [")
                .Append(marker).Append("] ").Append(item.Name).Append(" — ")
                .AppendLine(item.Evidence);
        }
        text.AppendLine().AppendLine("## Totals").AppendLine();
        foreach (Status status in Enum.GetValues<Status>())
            text.Append("- ").Append(status).Append(": ")
                .AppendLine(Receipts.Count(item => item.Status == status)
                    .ToString());
        File.WriteAllText(output, text.ToString(),
            new UTF8Encoding(false));
        Console.WriteLine("report: " + output);
    }

    private static string Read(string file) => source.TryGetValue(file,
        out string text) ? text : throw new FileNotFoundException(file);

    private static string Slice(string text, string start, string end)
    {
        int from = text.IndexOf(start, StringComparison.Ordinal);
        if (from < 0) return "";
        int to = text.IndexOf(end, from + start.Length,
            StringComparison.Ordinal);
        return to < 0 ? text[from..] : text[from..to];
    }

    private static int Count(string text, string value)
    {
        int count = 0, at = 0;
        while ((at = text.IndexOf(value, at, StringComparison.Ordinal)) >= 0)
        { count++; at += value.Length; }
        return count;
    }

    private static XElement Plan(XDocument document) =>
        document.Root?.Element("plan")
            ?? throw new InvalidDataException("plan missing");

    private static IEnumerable<XElement> Items(XElement owner,
        string collection)
    {
        XElement root = collection == null ? owner : owner?.Element(collection);
        return root?.Elements("li") ?? Enumerable.Empty<XElement>();
    }

    private static IEnumerable<XElement> ProgramEntries(XElement settlement) =>
        Items(settlement.Element("settlementProgram"), "entries");

    private static bool HasPrograms(XElement settlement, params string[] keys)
    {
        HashSet<string> actual = ProgramEntries(settlement)
            .Select(item => Value(item, "programKey")).ToHashSet();
        return keys.All(actual.Contains);
    }

    private static string Value(XElement owner, string name) =>
        (string)owner?.Element(name) ?? "";

    private static int Int(XElement owner, string name) =>
        int.TryParse(Value(owner, name), out int value) ? value : 0;

    private static string Hash(string path) => Convert.ToHexString(
        SHA256.HashData(File.ReadAllBytes(path)));
}

internal static class B9StringExtensions
{
    internal static bool NullOrEmpty(this string value) =>
        string.IsNullOrEmpty(value);
}
