using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using ColonistAwareness;

internal static class Program
{
    private static int acceptanceChecks;
    private static int cultureChecks;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length < 1 || args.Length > 3)
                throw new ArgumentException("usage: AuthoringConvergenceReceipts "
                    + "<repo> [active-fixture] [mirror-fixture]");

            string repository = Path.GetFullPath(args[0]);
            string sourceRoot = Path.Combine(repository, "Source");
            var source = Directory.GetFiles(sourceRoot, "*.cs",
                    SearchOption.TopDirectoryOnly)
                .ToDictionary(Path.GetFileName,
                    File.ReadAllText, StringComparer.OrdinalIgnoreCase);
            string allSource = string.Join("\n", source.Values);
            string Read(string name) => source.TryGetValue(name, out string value)
                ? value : throw new InvalidDataException(
                    "source file missing: " + name);

            XDocument active = args.Length >= 2
                ? XDocument.Load(Path.GetFullPath(args[1]),
                    LoadOptions.PreserveWhitespace) : null;
            XDocument mirror = args.Length >= 3
                ? XDocument.Load(Path.GetFullPath(args[2]),
                    LoadOptions.PreserveWhitespace) : null;

            var report = new StringBuilder();
            report.AppendLine("B7 AUTHORING AND CREATION ACCEPTANCE");
            RunAcceptance(Read, allSource, active, mirror, report);
            if (acceptanceChecks != 50)
                throw new InvalidOperationException("expected 50 B7 acceptance "
                    + "cases, observed " + acceptanceChecks);

            report.AppendLine();
            report.AppendLine("B7 CULTURE CAUSAL RECEIPTS");
            RunCultureReceipts(Read, active, report);
            if (cultureChecks != 15)
                throw new InvalidOperationException("expected 15 Culture causal "
                    + "receipts, observed " + cultureChecks);

            report.AppendLine();
            report.AppendLine("result: PASS (50 acceptance cases; 15 Culture "
                + "causal receipts)");
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

    private static void RunAcceptance(Func<string, string> read,
        string allSource, XDocument active, XDocument mirror,
        StringBuilder report)
    {
        string presentation = read("AuthoringPresentationModule.cs");
        string settings = read("ModEntry.cs");
        string region = read("RegionalPopulationScreenModule.cs");
        string editors = read("RegionalPopulationEditorsModule.cs");
        string setup = read("RegionalSetupModule.cs");
        string mapWidget = read("RegionMapWidget.cs");
        string settlement = read("RegionalSettlementModelModule.cs");
        string facilities = read("StartingFacilitiesModule.cs");
        string settlementAxes = read("SettlementAxesModule.cs");
        string culture = read("FactionCultureBeliefsModule.cs");
        string expression = read("CulturalExpressionModule.cs");
        string history = read("CultureLongitudinalModule.cs");
        string spatial = read("SettlementPlanningContextModule.cs");
        string organization = read("OrganizationModule.cs");
        string politicalEffects = read("PoliticalBeliefEffectsModule.cs");
        string roads = read("RoadExpansionModule.cs");
        string founding = read("PlayerFoundingPageModule.cs");
        string foundingState = read("PlayerFoundingStateModule.cs");
        string politicalPractice = read("PoliticalBeliefPracticeModule.cs");
        string worldTendencies = read("WorldGenerationDialogModule.cs");
        string world = read("RegionalWorldModule.cs");
        string diagnostics = read("B7CreationDiagnosticsModule.cs");

        Case(1, "one information architecture",
            !allSource.Contains("CAInformationDetail")
                && !allSource.Contains("CAInformationPresentation")
                && !settings.Contains("informationDetail"),
            "global display-density policy was removed", report);
        Case(2, "no cosmetic detail toggle",
            !allSource.Contains("Use normal detail")
                && !allSource.Contains("Show full details")
                && !presentation.Contains("DrawLocalExpansion"),
            "no control changes only the amount of prose", report);
        Case(3, "read models are contextual",
            expression.Contains("Continuity and change")
                && expression.Contains("Lived practices")
                && expression.Contains("Present setting"),
            "Culture explanation is organized around saved facts", report);
        Case(4, "editable controls own saved facts",
            setup.Contains("memberTileIds")
                && setup.Contains("startTileId")
                && region.Contains("populationGroups"),
            "region actions edit location, ownership, or population", report);
        Case(5, "saved facts have downstream consumers",
            world.Contains("populationGroups")
                && setup.Contains("settlementRealizationSourceHash")
                && facilities.Contains("startingFacilityMask"),
            "authored regional facts enter materialization", report);
        Case(6, "derived summaries stay read-only",
            expression.Contains("ForMaterializedSettlement")
                && !expression.Contains("Scribe_")
                && !expression.Contains("Widgets.Button"),
            "the expression read model neither saves nor edits", report);
        Case(7, "retired abstractions are load-only",
            setup.Contains("retired development profile")
                && !region.Contains("Development profile")
                && !editors.Contains("Development profile"),
            "old development values migrate without returning to the UI", report);

        Case(8, "Starting Region map is authoritative",
            region.Contains("CARegionMapWidget.Draw")
                && mapWidget.Contains("{ \"Land\", \"Factions\", \"Connections\" }")
                && mapWidget.Contains("DrawRoads")
                && mapWidget.Contains("DrawLink"),
            "the same map presents land, objects, and connections", report);
        Case(9, "map objects are directly selectable",
            region.Contains("CARegionMapWidget.SelectFaction")
                && region.Contains("CARegionMapWidget.SelectSettlement"),
            "factions and settlements select their own inspectors", report);
        Case(10, "population composition is directly editable",
            region.Contains("Generate population")
                && region.Contains("Add minority population")
                && setup.Contains("Scribe_Collections.Look(ref populationGroups"),
            "population groups are saved settlement objects", report);
        Case(11, "ordinal development controls are absent",
            !region.Contains("Local services")
                && !region.Contains("Public works")
                && !region.Contains("Transport access")
                && !editors.Contains("Development profile"),
            "the Starting Region page exposes concrete results", report);
        Case(12, "facility profile modal is absent",
            !allSource.Contains("Dialog_CAStartingFacilities")
                && !region.Contains("Starting facilities...")
                && !editors.Contains("Generated: included"),
            "the rejected per-row include/omit feature is gone", report);
        Case(13, "facilities are concrete realized facts",
            region.Contains("FacilityNames(facilities)")
                && region.Contains("These facilities and routes follow")
                && settlementAxes.Contains("ResolveFacilityMask"),
            "facility readout and materialization share one resolved mask", report);
        Case(14, "region continuation does not require inspectors",
            region.Contains("CARegionalSetupSession.PrepareNext(false)")
                && !Slice(region, "protected override bool CanDoNext()",
                    "protected override void DoNext()").Contains("selected"),
            "Continue validates the composition itself", report);

        Case(15, "Culture is independent saved state",
            culture.Contains("public sealed class CACulture : IExposable")
                && culture.Contains("public string parentId")
                && culture.Contains("public string localityKey")
                && culture.Contains("public List<CACultureTransition> transitions"),
            "identity, inheritance, locality, and history persist", report);
        Case(16, "Culture changes through lived evidence",
            history.Contains("CACultureHistory.EvaluateTransition")
                && history.Contains("MapComponentTick")
                && culture.Contains("CACultureLongitudinalKernel.Evaluate"),
            "a periodic runtime executor records Culture transitions", report);
        Case(17, "Culture has spatial consumers",
            spatial.Contains("shared-public-life")
                && spatial.Contains("defensive-boundary"),
            "retained practices alter placement scoring", report);
        Case(18, "Culture has social institutional political consumers",
            organization.Contains("shared-public-life")
                && facilities.Contains("research-tradition")
                && politicalEffects.Contains("CulturalStrength")
                && politicalEffects.Contains("CACultureConsumerKernel")
                && politicalEffects.Contains("PoliticalHabituationTicks"),
            "three distinct simulation systems consume Culture history", report);
        Case(19, "Culture does not own material definitions",
            !Slice(culture, "public sealed class CACulture : IExposable",
                    "public sealed class CAPoliticalBeliefs")
                .Contains("ThingDef")
                && !expression.Contains("ThingMaker")
                && !expression.Contains("GenSpawn"),
            "Culture records continuity rather than construction recipes", report);
        Case(20, "settlements have local Culture identities",
            active != null && LocalCultures(active).Count == 4
                && LocalCultures(active).Select(item => Value(item, "id"))
                    .Distinct(StringComparer.Ordinal).Count() == 4
                && LocalCultures(active).All(item =>
                    !string.IsNullOrWhiteSpace(Value(item, "parentId"))),
            "the fixture has four distinct inherited local Cultures", report);
        Case(21, "plural populations remain explicit evidence",
            active != null && Settlements(active).SelectMany(item =>
                    Items(item, "populationGroups")).Count() == 9
                && Settlements(active).SelectMany(item =>
                    Items(item, "populationGroups"))
                    .Any(item => BoolValue(item, "quarter")),
            "nine groups and a separate quarter survive", report);
        Case(22, "Culture inspection cannot mutate",
            !expression.Contains("EvaluateTransition")
                && !expression.Contains("EnsureSettlementCulture")
                && !expression.Contains("Scribe_")
                && !expression.Contains("Rand."),
            "inspection only derives a reading", report);
        Case(23, "drawing does not generate Culture",
            !Slice(region, "private void DrawSettlementPanel",
                    "private void DrawFactionPanel")
                .Contains("CACultureHistory.EvaluateTransition")
                && !Slice(region, "private void DrawSettlementPanel",
                    "private void DrawFactionPanel")
                .Contains("EnsureSettlementCulture")
                && !Slice(region, "private void DrawSettlementPanel",
                    "private void DrawFactionPanel")
                .Contains("EnsurePlayerLocalCulture"),
            "the authoring page renders saved Culture without advancing it", report);
        Case(24, "Culture explanations cite history",
            expression.Contains("ContinuitySummary")
                && expression.Contains("PracticeSummary")
                && expression.Contains("CultureFacts")
                && expression.Contains("Readings.Select(item => item.Family"),
            "the readout distinguishes inheritance, practices, and change", report);

        Case(25, "player political beliefs are authored",
            founding.Contains("DrawPoliticalCard")
                && founding.Contains("new CAFoundingAction(\"Profiles...\"")
                && founding.Contains("Dialog_CAAxisEditor"),
            "the founding flow exposes beliefs and editing", report);
        Case(26, "existing factions share political machinery",
            culture.Contains("DrawPoliticalOverview")
                && culture.Contains("CAAuthoringChoices.PoliticalProfiles")
                && culture.Contains("Dialog_CAAxisEditor"),
            "player and NPC surfaces use one political model", report);
        Case(27, "belief and structure remain separate",
            setup.Contains("CAPoliticalBeliefs politicalBeliefs")
                && setup.Contains("List<CAAxisEntry> factionStructure")
                && politicalPractice.Contains("ReadAgainstPoliticalBeliefs"),
            "professed rules and instituted rules are distinct state", report);
        Case(28, "founding arrangement is its own object",
            foundingState.Contains("CAFoundingArrangement arrangement")
                && founding.Contains("DrawArrangementCard")
                && founding.Contains("Dialog_CAFoundingArrangementEditor"),
            "landing terms are chosen separately from beliefs", report);
        Case(29, "belief-practice tension is visible and causal",
            founding.Contains("belief-term tension")
                && diagnostics.Contains("belief-practice tensions")
                && politicalEffects.Contains("openBeliefConflicts")
                && politicalEffects.Contains("CulturalStrength"),
            "agreement and disagreement are inspectable state", report);

        Case(30, "native Ideoligion chooser is retained",
            founding.Contains("current is Page_ChooseIdeoPreset")
                && founding.Contains("Page following = current.next")
                && founding.Contains("current.next = inserted"),
            "CA follows RimWorld's native Ideology surface", report);
        Case(31, "founding presents all four concepts",
            founding.Contains("DrawCultureCard")
                && founding.Contains("DrawIdeoCard")
                && founding.Contains("DrawPoliticalCard")
                && founding.Contains("DrawArrangementCard"),
            "Culture, Ideoligion, beliefs, and adopted terms are co-present", report);
        Case(32, "native Ideoligion state round-trips",
            foundingState.Contains("nativeIdeoId")
                && foundingState.Contains("nativeIdeoSignature")
                && foundingState.Contains("nativeIdeoNotified")
                && founding.Contains("CaptureNativeIdeo"),
            "native edits are captured with identity and content", report);
        Case(33, "Ideoligion and Culture remain separate models",
            foundingState.Contains("public CACulture culture")
                && foundingState.Contains("public int nativeIdeoId")
                && culture.Contains("Inherited Culture is separate from Ideoligion"),
            "visual/belief substrate is not collapsed into Culture history", report);

        Case(34, "player fixture begins at a new landing",
            active != null && !BoolValue(Plan(active).Element("playerFounding"),
                    "establishedStart")
                && Value(Plan(active).Element("playerFounding"),
                    "temporalBasis").Contains("new landing",
                        StringComparison.OrdinalIgnoreCase),
            "the authored fixture does not invent prior player history", report);
        Case(35, "existing settlements begin established",
            active != null && LocalCultures(active).All(item =>
                    Value(item, "maturity") == "Established")
                && LocalCultures(active).All(item =>
                    !string.IsNullOrWhiteSpace(Value(item, "temporalBasis")))
                && !LocalCultures(active).SelectMany(item =>
                        Items(item, "transitions"))
                    .Any(item => Value(item, "cause")
                        == "history before game start"),
            "pre-game continuity is a temporal basis, not an invented event", report);
        Case(36, "established player starts require explicit scenarios",
            foundingState.Contains("ICAEstablishedPlayerStart")
                && foundingState.Contains(
                    "ICAEstablishedPlayerStart established = part")
                && foundingState.Contains(
                    "as ICAEstablishedPlayerStart")
                && !Slice(foundingState,
                    "private static void DetermineTemporalBoundary",
                    "internal static bool ArrivedViolently")
                    .Contains("GetType().Name"),
            "typed scenario metadata owns the exception", report);
        Case(37, "technology and pawn count do not imply history",
            !Slice(foundingState, "private static void DetermineTemporalBoundary",
                    "internal static bool ArrivedViolently")
                .Contains("techLevel")
                && !Slice(foundingState, "private static void DetermineTemporalBoundary",
                    "internal static bool ArrivedViolently")
                .Contains("StartingPawnCount"),
            "player temporal meaning is independent of capability", report);

        Case(38, "legacy Culture migration is deterministic",
            LegacyMigrationStable(),
            "identical obsolete input yields identical schema-5 output", report);
        Case(39, "fixture contains only current Culture schema",
            active != null && AllCultures(active).All(item =>
                    Value(item, "schemaVersion") == "5")
                && !AllCultures(active).Any(item =>
                    item.Element("presetName") != null),
            "obsolete visual-profile state is absent", report);
        Case(40, "sparse facility exceptions remain bit-stable",
            CACulturalExpressionCausalKernel.ResolveFacilityMask(
                    0b1010010, 0b0011100, 0b0001000, 0b1111111)
                == 0b1001010,
            "explicit exceptions replace only their owned bits", report);
        Case(41, "fixture is stable across XML readback",
            active != null && StableXml(active),
            "current founding and regional state survives serialization", report);
        Case(42, "active and mirror fixtures agree",
            active != null && mirror != null
                && CanonicalXml(active) == CanonicalXml(mirror)
                && FixtureIdentityValid(active),
            "both runtime surfaces contain the same 3/4/9 composition", report);

        Case(43, "World tendencies has an explicit confirmation surface",
            worldTendencies.Contains("Dialog_CAWorldGeneration")
                && worldTendencies.Contains("Starting Region choices replace")
                && worldTendencies.Contains("CAWorldTendenciesSession.Policy"),
            "world defaults remain editable before generation", report);
        Case(44, "landing has a guarded confirmation path",
            setup.Contains("CARegionalLandingPageValidationPatch")
                && setup.Contains("CARegionalLandingPageNextPatch")
                && setup.Contains("AlignVanillaLandingSelection"),
            "the selected footprint is validated before leaving the globe", report);
        Case(45, "Starting Region confirms the final composition",
            region.Contains("Continue with this region?")
                && region.Contains("PrepareNext(true")
                && region.Contains("SavePending"),
            "Continue saves and validates what is shown", report);
        Case(46, "founding validates and commits once",
            founding.Contains("CAPlayerFoundingModel.TryValidate")
                && founding.Contains("CAPlayerFoundingSession.Confirm")
                && founding.Contains("ApplyCarriedState"),
            "the page has a complete authoring-to-runtime boundary", report);
        Case(47, "native Ideology notification is guarded",
            founding.Contains("!draft.nativeIdeoNotified")
                && founding.Contains("PostIdeoChosen")
                && founding.Contains("draft.nativeIdeoNotified = true"),
            "scenario initialization cannot fire twice", report);
        Case(48, "regional generation consumes the saved plan",
            world.Contains("PendingForCurrentWorld")
                && world.Contains("RegisterTransientDeveloperExercise")
                && world.Contains("if (region.developerExercise)")
                && world.Contains("CARegionalPlanResolver.Resolve(region)")
                && setup.Contains("bool transientTest = generating?.developerExercise == true")
                && setup.Contains("Continuing as the explicitly")
                && setup.Contains("settlementRealizationSourceHash")
                && setup.Contains("CARegionalBiomeAtPatch"),
            "durable plans validate normally while an explicitly stamped developer exercise reaches transient materialization",
            report);
        Case(49, "Culture advances during play, not setup rendering",
            history.Contains("MapComponentTick")
                && history.Contains("TickManager?.TicksGame")
                && history.Contains("regional.ForMap(map)")
                && history.Contains("EvaluatePlayer"),
            "runtime cadence covers player and NPC settlements", report);
        Case(50, "operator diagnostics expose the test boundary",
            diagnostics.Contains("B7: founding-state census")
                && diagnostics.Contains("B7: Culture-history census")
                && diagnostics.Contains("allowedGameStates")
                && diagnostics.Contains("predecessor=")
                && diagnostics.Contains("CurrentOrNull"),
            "read-only debug receipts are available for runtime testing", report);
    }

    private static void RunCultureReceipts(Func<string, string> read,
        XDocument active, StringBuilder report)
    {
        CACulturalHistoryEvidence initial = Evidence(0, "population:a",
            "spatial:a", "social:a", "institutional:a", "political:a",
            "material:a", Practice("shared-public-life", 80, "period:a"));
        CACulturalHistoryEvidence changed = Evidence(60000, "population:a",
            "spatial:a", "social:b", "institutional:a", "political:a",
            "material:a", Practice("shared-public-life", 40, "period:b"));
        var prior = new List<CACulturalPracticeState>
        {
            new CACulturalPracticeState
            {
                Key = "shared-public-life", Summary = "shared public life",
                Strength = 80, SourceSignature = "period:a"
            }
        };
        CACulturalTransitionEvaluation baseline =
            CACultureLongitudinalKernel.Evaluate(null, initial, prior,
                "culture:a");
        CACulturalTransitionEvaluation transition =
            CACultureLongitudinalKernel.Evaluate(initial, changed, prior,
                "culture:a");
        CACulturalTransitionEvaluation repeat =
            CACultureLongitudinalKernel.Evaluate(initial, changed, prior,
                "culture:a");
        CACulturalHistoryEvidence sameTick = Evidence(0, "population:a",
            "spatial:a", "social:a", "institutional:a", "political:a",
            "material:a", Practice("shared-public-life", 80, "period:a"));
        CACulturalTransitionEvaluation inert =
            CACultureLongitudinalKernel.Evaluate(initial, sameTick, prior,
                "culture:a");
        CACulturalHistoryEvidence materialOnly = Evidence(60000,
            "population:a", "spatial:a", "social:a", "institutional:a",
            "political:a", "material:b",
            Practice("shared-public-life", 80, "period:a"));
        CACulturalTransitionEvaluation unrelated =
            CACultureLongitudinalKernel.Evaluate(initial, materialOnly, prior,
                "culture:a");
        CACulturalHistoryEvidence absenceDayOne = Evidence(60000,
            "population:a", "spatial:a", "social:a", "institutional:a",
            "political:a", "material:a");
        CACulturalTransitionEvaluation firstDecay =
            CACultureLongitudinalKernel.Evaluate(initial, absenceDayOne, prior,
                "culture:a");
        CACulturalHistoryEvidence absenceDayTwo = Evidence(120000,
            "population:a", "spatial:a", "social:a", "institutional:a",
            "political:a", "material:a");
        CACulturalTransitionEvaluation secondDecay =
            CACultureLongitudinalKernel.Evaluate(absenceDayOne, absenceDayTwo,
                firstDecay.Practices, "culture:a");

        CultureCase("C01", "prior Culture persists",
            baseline.Practices.Single().Strength == 80 && !baseline.Changed,
            "the first evidence snapshot does not rewrite inherited state", report);
        CultureCase("C02", "changed evidence changes Culture deterministically",
            transition.Changed
                && transition.ChangedDomains.SequenceEqual(new[]
                    { "lived practice" })
                && transition.Practices.Single().Strength == 70
                && transition.TransitionSignature == repeat.TransitionSignature,
            "one elapsed period yields one repeatable historical transition",
            report);
        CultureCase("C03", "practice change follows time, not unrelated edges",
            !inert.Changed
                && !unrelated.Changed
                && unrelated.Practices.Single().Strength == 80
                && unrelated.ChangedDomains.Count == 0
                && firstDecay.Practices.Single().Strength == 70
                && secondDecay.Practices.Single().Strength == 60
                && read("FactionCultureBeliefsModule.cs")
                    .Contains("absent.observationCount = 0")
                && read("CultureLongitudinalModule.cs")
                    .Contains("RecentPracticeTicks")
                && read("CultureLongitudinalModule.cs")
                    .Contains("CountKind(\"policy\", recentStart)")
                && read("CultureLongitudinalModule.cs")
                    .Contains("lastResearchActivityTick >= recentStart"),
            "same-tick inspection is inert; elapsed absence decays while an "
                + "unrelated material change does not alter an observed practice",
            report);
        CultureCase("C04", "transition records provenance",
            transition.PriorEvidenceSignature == initial.StableSignature()
                && transition.EvidenceSignature == changed.StableSignature()
                && !string.IsNullOrWhiteSpace(transition.TransitionSignature),
            "predecessor evidence, new evidence, and receipt are retained", report);

        CACulturalHistoryEvidence other = Evidence(60000, "population:a",
            "spatial:b", "social:a", "institutional:a", "political:a",
            "material:a", Practice("defensive-boundary", 60, "period:c"));
        CACulturalTransitionEvaluation divergent =
            CACultureLongitudinalKernel.Evaluate(initial, other, prior,
                "culture:a");
        CultureCase("C05", "different histories diverge",
            divergent.TransitionSignature != transition.TransitionSignature
                && divergent.ChangedDomains.SequenceEqual(new[]
                    { "lived practice" }),
            "two settlements with different evidence do not converge by label", report);

        var pluralInput = new CACulturalExpressionCausalInput
        {
            Scope = "settlement:plural",
            CultureName = "Local Culture",
            CultureMaturity = "Established",
            CultureConstituents = new List<CACulturalConstituentState>
            {
                new CACulturalConstituentState
                {
                    CultureId = "culture:a", Label = "A", Share = 65,
                    Inherited = true
                },
                new CACulturalConstituentState
                {
                    CultureId = "culture:b", Label = "B", Share = 35,
                    Inherited = true, SeparateQuarter = true
                }
            },
            Populations = new List<CACulturalPopulationCause>
            {
                new CACulturalPopulationCause
                {
                    Key = "group:a", Share = 65,
                    IdeoligionSource = "ideo:a", BeliefSource = "belief:a"
                },
                new CACulturalPopulationCause
                {
                    Key = "group:b", Share = 35,
                    IdeoligionSource = "ideo:b", BeliefSource = "belief:b",
                    SeparateQuarter = true
                }
            }
        };
        CACulturalExpressionCausalResult plural =
            CACulturalExpressionCausalKernel.Evaluate(pluralInput);
        CultureCase("C06", "plural constituent Cultures remain distinct",
            plural.Status == 3
                && plural.Facts.Any(item => item.Contains("culture:a:65"))
                && plural.Facts.Any(item => item.Contains("culture:b:35"))
                && CACulturalExpressionCausalKernel.NormalizeConstituents(
                    pluralInput.CultureConstituents).Count == 2,
            "two population groups retain both inherited Culture identities",
            report);

        string spatialSource = read("SettlementPlanningContextModule.cs")
            + read("RoadExpansionModule.cs");
        CultureCase("C07", "spatial consumer is live",
            CACultureConsumerKernel.SpatialPreference(2f, 3f, 80, 0)
                    > CACultureConsumerKernel.SpatialPreference(2f, 3f, 0, 0)
                && CACultureConsumerKernel.PreferExchangeRoad(true, true,
                    70, 30)
                && !CACultureConsumerKernel.PreferExchangeRoad(true, true,
                    30, 70)
                && spatialSource.Contains("CACultureConsumerKernel")
                && spatialSource.Contains("SpatialPreference")
                && spatialSource.Contains("PreferExchangeRoad"),
            "the production placement and road paths call the varied shared kernel",
            report);
        string socialSource = read("OrganizationModule.cs");
        CultureCase("C08", "social consumer is live",
            CACultureConsumerKernel.GatheringScore(5, 10f, 80)
                    > CACultureConsumerKernel.GatheringScore(5, 10f, 0)
                && socialSource.Contains("CACultureConsumerKernel")
                && socialSource.Contains("GatheringScore"),
            "the production gathering path calls the varied shared kernel",
            report);
        string institutionalSource = read("StartingFacilitiesModule.cs");
        CultureCase("C09", "institutional consumer is live",
            !CACultureConsumerKernel.PrioritizeResearch(39)
                && CACultureConsumerKernel.PrioritizeResearch(40)
                && institutionalSource.Contains("CACultureConsumerKernel")
                && institutionalSource.Contains("PrioritizeResearch"),
            "the production institutional path calls the varied shared kernel",
            report);
        string politicalSource = read("PoliticalBeliefEffectsModule.cs");
        CultureCase("C10", "political contradiction persists",
            CACultureConsumerKernel.PoliticalHabituationTicks(1000, 100)
                    > CACultureConsumerKernel.PoliticalHabituationTicks(
                        1000, 0)
                && politicalSource.Contains("CACultureConsumerKernel")
                && politicalSource.Contains("PoliticalHabituationTicks")
                && !politicalSource.Contains("politicalBeliefs ="),
            "public-gathering Culture keeps voice conflicts salient without "
                + "overwriting belief", report);

        string longitudinal = read("CultureLongitudinalModule.cs");
        CultureCase("C11", "Culture grants no authority or material objects",
            !longitudinal.Contains("ThingMaker.MakeThing")
                && !longitudinal.Contains("GenSpawn.Spawn")
                && !longitudinal.Contains("SetAuthority")
                && !longitudinal.Contains("GrantAuthority"),
            "Culture observes and influences; it does not mint power or things", report);

        CACulturalPersistedStateInput visualA = PersistedState("Rustican");
        CACulturalPersistedStateInput visualB = PersistedState("Sophian");
        CultureCase("C12", "visual tradition is independent",
            visualA.HistoricalSignature() == visualB.HistoricalSignature()
                && visualA.VisualExpressionSignature()
                    != visualB.VisualExpressionSignature()
                && read("FactionCultureBeliefsModule.cs").Contains(
                    "}.HistoricalSignature()"),
            "different visual inheritance changes its expression receipt without rewriting lived history",
            report);

        CACulturalPersistedStateInput readback = RoundTripState(visualA);
        CultureCase("C13", "transition state survives save/readback",
            readback.HistoricalSignature() == visualA.HistoricalSignature()
                && readback.Practices.Single().Key == "shared-public-life"
                && readback.Practices.Single().SourceSignature == "period:a"
                && readback.Transitions.Single().Tick == 60000
                && readback.Transitions.Single().Cause
                    == "lived history evaluated"
                && readback.Transitions.Single()
                    .PredecessorCultureSignature == "culture:prior"
                && readback.Transitions.Single().EvidenceSignature
                    == "evidence:period-a"
                && readback.Transitions.Single().ChangedDomains
                    == "social, lived practice"
                && read("FactionCultureBeliefsModule.cs").Contains(
                    "Scribe_Collections.Look(ref transitions")
                && read("FactionCultureBeliefsModule.cs").Contains(
                    "Scribe_Collections.Look(ref practices"),
            "a populated Culture snapshot serializes and reads back with transition and practice provenance",
            report);
        CultureCase("C14", "inspector is non-mutating",
            !read("CulturalExpressionModule.cs").Contains("EvaluateTransition")
                && !read("CulturalExpressionModule.cs").Contains("Scribe_")
                && !read("CulturalExpressionModule.cs").Contains("Rand.")
                && read("B7CreationDiagnosticsModule.cs")
                    .Contains("CurrentOrNull"),
            "opening Culture explanation cannot change history", report);
        CultureCase("C15", "identical input is byte-stable",
            TransitionFingerprint(transition) == TransitionFingerprint(repeat)
                && TransitionFingerprint(secondDecay)
                    == TransitionFingerprint(CACultureLongitudinalKernel
                        .Evaluate(absenceDayOne, absenceDayTwo,
                            firstDecay.Practices, "culture:a")),
            "changed and repeated elapsed-history receipts are deterministic",
            report);
    }

    private static CACulturalHistoryEvidence Evidence(int tick,
        string population,
        string spatial, string social, string institutional, string political,
        string material, params CACulturalPracticeEvidence[] practices)
    {
        return new CACulturalHistoryEvidence
        {
            Tick = tick,
            Population = population,
            Spatial = spatial,
            Social = social,
            Institutional = institutional,
            Political = political,
            Material = material,
            Practices = practices.ToList()
        };
    }

    private static CACulturalPracticeEvidence Practice(string key,
        int strength, string source)
    {
        return new CACulturalPracticeEvidence
        {
            Key = key,
            Summary = key.Replace('-', ' '),
            Strength = strength,
            SourceOwner = "settlement:test",
            SourceDomain = "test",
            EvidenceStartTick = 0,
            SourceSignature = source
        };
    }

    private static CACulturalPersistedStateInput PersistedState(
        string visualTradition)
    {
        return new CACulturalPersistedStateInput
        {
            Identity = "culture:test",
            ParentIdentity = "culture:inherited",
            Locality = "settlement:test",
            Maturity = "Established",
            Revision = 2,
            CompositionSignature = "two-groups",
            VisualTradition = visualTradition,
            Practices = new List<CACulturalPracticeState>
            {
                new CACulturalPracticeState
                {
                    Key = "shared-public-life",
                    Summary = "Public gatherings remain common.",
                    Strength = 70,
                    SourceSignature = "period:a"
                }
            },
            Transitions = new List<CACulturalTransitionState>
            {
                new CACulturalTransitionState
                {
                    Sequence = 2,
                    Tick = 60000,
                    Cause = "lived history evaluated",
                    Summary = "Recorded change in social practice.",
                    SuccessorSignature = "culture:successor",
                    PredecessorCultureSignature = "culture:prior",
                    EvidenceSignature = "evidence:period-a",
                    ChangedDomains = "social, lived practice"
                }
            }
        };
    }

    private static CACulturalPersistedStateInput RoundTripState(
        CACulturalPersistedStateInput input)
    {
        var root = new XElement("culture",
            new XElement("id", input.Identity ?? ""),
            new XElement("parentId", input.ParentIdentity ?? ""),
            new XElement("localityKey", input.Locality ?? ""),
            new XElement("maturity", input.Maturity ?? ""),
            new XElement("revision", input.Revision),
            new XElement("compositionSignature",
                input.CompositionSignature ?? ""),
            new XElement("sourceCultureDefName",
                input.VisualTradition ?? ""),
            new XElement("practices", input.Practices.Select(item =>
                new XElement("li",
                    new XElement("key", item.Key ?? ""),
                    new XElement("summary", item.Summary ?? ""),
                    new XElement("strength", item.Strength),
                    new XElement("sourceSignature",
                        item.SourceSignature ?? "")))),
            new XElement("transitions", input.Transitions.Select(item =>
                new XElement("li",
                    new XElement("sequence", item.Sequence),
                    new XElement("tick", item.Tick),
                    new XElement("cause", item.Cause ?? ""),
                    new XElement("summary", item.Summary ?? ""),
                    new XElement("sourceSignature",
                        item.SuccessorSignature ?? ""),
                    new XElement("predecessorCultureSignature",
                        item.PredecessorCultureSignature ?? ""),
                    new XElement("evidenceSignature",
                        item.EvidenceSignature ?? ""),
                    new XElement("changedDomains",
                        item.ChangedDomains ?? "")))));
        XElement loaded = XElement.Parse(root.ToString(
            SaveOptions.DisableFormatting));
        return new CACulturalPersistedStateInput
        {
            Identity = (string)loaded.Element("id"),
            ParentIdentity = (string)loaded.Element("parentId"),
            Locality = (string)loaded.Element("localityKey"),
            Maturity = (string)loaded.Element("maturity"),
            Revision = (int?)loaded.Element("revision") ?? 0,
            CompositionSignature = (string)loaded.Element(
                "compositionSignature"),
            VisualTradition = (string)loaded.Element(
                "sourceCultureDefName"),
            Practices = loaded.Element("practices")?.Elements("li")
                .Select(item => new CACulturalPracticeState
                {
                    Key = (string)item.Element("key"),
                    Summary = (string)item.Element("summary"),
                    Strength = (int?)item.Element("strength") ?? 0,
                    SourceSignature = (string)item.Element(
                        "sourceSignature")
                }).ToList() ?? new List<CACulturalPracticeState>(),
            Transitions = loaded.Element("transitions")?.Elements("li")
                .Select(item => new CACulturalTransitionState
                {
                    Sequence = (int?)item.Element("sequence") ?? 0,
                    Tick = (int?)item.Element("tick") ?? -1,
                    Cause = (string)item.Element("cause"),
                    Summary = (string)item.Element("summary"),
                    SuccessorSignature = (string)item.Element(
                        "sourceSignature"),
                    PredecessorCultureSignature = (string)item.Element(
                        "predecessorCultureSignature"),
                    EvidenceSignature = (string)item.Element(
                        "evidenceSignature"),
                    ChangedDomains = (string)item.Element("changedDomains")
                }).ToList() ?? new List<CACulturalTransitionState>()
        };
    }

    private static bool LegacyMigrationStable()
    {
        var input = new CACultureLegacyMigrationInput
        {
            SchemaVersion = 3,
            PresetName = "Hearth customs",
            Name = "Hearth customs",
            NameAuthored = false
        };
        CACultureLegacyMigrationResult first =
            CACultureLegacyMigrationKernel.Migrate(input);
        CACultureLegacyMigrationResult second =
            CACultureLegacyMigrationKernel.Migrate(input);
        return first.SchemaVersion == 5
            && first.PresetName == null
            && first.ClearLegacyPractices
            && first.Name == second.Name
            && first.SourceCultureDefName == second.SourceCultureDefName;
    }

    private static bool FixtureIdentityValid(XDocument document)
    {
        XElement plan = Plan(document);
        return Value(document.Root, "worldIdentity")
                == "alysaliu|1|Algorab Markab"
            && Value(plan, "schemaVersion") == "5"
            && Value(plan, "regionalId") == "CA-RG-EB596A12"
            && Value(plan, "candidateId") == "613b1fe44104"
            && Value(plan, "bundleRootTileId") == "389638"
            && Value(plan, "startTileId") == "389638"
            && Value(plan, "mapSize") == "350"
            && Value(plan, "settlementRealizationSourceHash")
                == "-2009058919"
            && Factions(document).Count == 3
            && Settlements(document).Count == 4
            && Settlements(document).SelectMany(item =>
                Items(item, "populationGroups")).Count() == 9;
    }

    private static List<XElement> AllCultures(XDocument document)
    {
        XElement plan = Plan(document);
        return Factions(document).Select(item => item.Element("culture"))
            .Concat(LocalCultures(document))
            .Concat(new[] { plan.Element("playerFounding")?.Element("culture") })
            .Where(item => item != null).ToList();
    }

    private static List<XElement> LocalCultures(XDocument document)
    {
        return Settlements(document).Select(item => item.Element("localCulture"))
            .Where(item => item != null).ToList();
    }

    private static List<XElement> Factions(XDocument document)
    {
        return Items(Plan(document), "factions");
    }

    private static List<XElement> Settlements(XDocument document)
    {
        return Items(Plan(document), "settlements");
    }

    private static XElement Plan(XDocument document)
    {
        return document?.Root?.Element("plan")
            ?? throw new InvalidDataException("fixture has no plan element");
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

    private static bool BoolValue(XElement parent, string name)
    {
        return bool.TryParse(Value(parent, name), out bool value) && value;
    }

    private static bool StableXml(XDocument document)
    {
        string first = CanonicalXml(document);
        string second = CanonicalXml(XDocument.Parse(first,
            LoadOptions.PreserveWhitespace));
        return Hash(first) == Hash(second);
    }

    private static string CanonicalXml(XDocument document)
    {
        return document.Root?.ToString(SaveOptions.DisableFormatting) ?? "";
    }

    private static string TransitionFingerprint(
        CACulturalTransitionEvaluation value)
    {
        return Hash(string.Join("|", new[]
        {
            value.Changed.ToString(),
            value.PriorEvidenceSignature ?? "",
            value.EvidenceSignature ?? "",
            value.TransitionSignature ?? "",
            string.Join(",", value.ChangedDomains),
            string.Join(",", value.Practices.Select(item => item.Key + ":"
                + item.Strength + ":" + item.SourceSignature))
        }));
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(value)));
    }

    private static string Slice(string source, string start, string end)
    {
        int from = source.IndexOf(start, StringComparison.Ordinal);
        int to = from < 0 ? -1 : source.IndexOf(end,
            from + start.Length, StringComparison.Ordinal);
        if (from < 0 || to <= from)
            throw new InvalidOperationException("source contract slice is "
                + "missing: " + start);
        return source.Substring(from, to - from);
    }

    private static void Case(int number, string label, bool condition,
        string evidence, StringBuilder report)
    {
        acceptanceChecks++;
        if (!condition)
            throw new InvalidOperationException("B7-"
                + number.ToString("D2") + " failed: " + label);
        report.Append("PASS B7-").Append(number.ToString("D2"))
            .Append(" ").Append(label).Append(" -> ")
            .AppendLine(evidence);
    }

    private static void CultureCase(string code, string label,
        bool condition, string evidence, StringBuilder report)
    {
        cultureChecks++;
        if (!condition)
            throw new InvalidOperationException(code + " failed: " + label);
        report.Append("PASS ").Append(code).Append(" ").Append(label)
            .Append(" -> ").AppendLine(evidence);
    }
}
