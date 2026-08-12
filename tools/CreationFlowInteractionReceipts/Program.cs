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
            string repository = args.Length > 0
                ? Path.GetFullPath(args[0]) : FindRepository();
            string Read(string file) => File.ReadAllText(
                Path.Combine(repository, "Source", file));

            string shared = Read("CreationFlowUiModule.cs");
            string world = Read("WorldGenerationDialogModule.cs");
            string setup = Read("RegionalSetupModule.cs");
            string region = Read("RegionalPopulationScreenModule.cs");
            string regionEditors = Read("RegionalPopulationEditorsModule.cs");
            string founding = Read("PlayerFoundingPageModule.cs");
            string foundingState = Read("PlayerFoundingStateModule.cs");
            string culture = Read("FactionCultureBeliefsModule.cs");
            string authoring = Read("AuthoringComposerSupportModule.cs");
            string presentation = Read("AuthoringPresentationModule.cs");

            var report = new StringBuilder();
            Require(shared.Contains("Dialog_CACreationChoices")
                && shared.Contains("DrawGrid")
                && shared.Contains("DrawDetails")
                && shared.Contains("ConfirmLabel")
                && shared.Contains("bool focused")
                && shared.Contains("bool applied")
                && shared.Contains("Current selection"),
                "shared choice surface lacks selection, detail, or confirmation state");
            report.AppendLine("PASS shared graphical choice surface");

            Require(shared.Contains("DrawFlow")
                && world.Contains("DrawFlow(")
                && region.Contains("DrawFlow(")
                && founding.Contains("DrawFlow("),
                "creation-flow continuity is not visible across custom surfaces");
            Require(setup.Contains("InstallStartingRegionPage")
                && founding.Contains("CAPlayerFoundingPageChainPatch")
                && founding.Contains("editor.next = this")
                && founding.Contains("current is Page_ChooseIdeoPreset")
                && founding.Contains("current.next = inserted"),
                "page chain or native Ideoligion return path is missing");
            report.AppendLine("PASS continuous world-to-founding page chain");

            Require(world.Contains("WorldProfile[] Profiles")
                && world.Contains("NeutralProfile")
                && world.Contains("CACreationUI.DrawSegment")
                && !world.Contains("new FloatMenu"),
                "World tendencies are not stable independent bands with presets");
            Require(setup.Contains("localFactionChance = 0.45f")
                && setup.Contains("regionalConflictChance = 0.4f"),
                "neutral world policy does not select the middle faction bands");
            string[] retiredWorldLabels =
            {
                "\"Generated land\"", "\"Settlement concentration\"",
                "\"Urban growth propensity\"",
                "\"Reallocation source variety\"",
                "\"Local faction formation\"", "\"Regional conflict\"",
                "\"Off-map activity rate\"", "\"Distant-world activity\""
            };
            foreach (string label in retiredWorldLabels)
                Require(!world.Contains(label),
                    "retired mechanism-facing world label remains: " + label);
            report.AppendLine("PASS neutral causal World tendencies interaction");

            Require(founding.Contains("\"Rules at landing\"")
                && !founding.Contains("\"Founding arrangement\"")
                && founding.Contains("ReadAgainstPoliticalBeliefs")
                && founding.Contains("ComparisonHeight")
                && !founding.Contains("Comparable("),
                "founding terms do not use the exact belief/practice comparison");
            Require(!founding.Contains("new FloatMenu")
                && !culture.Contains("new FloatMenu"),
                "founding culture, Ideoligion, or political choices still use text-wall menus");
            Require(culture.Contains("CACreationUI.SourceWords")
                && culture.Contains("ProfileDetails")
                && culture.Contains("ApplyProfile")
                && authoring.Contains("CAPoliticalBeliefsModel.ApplyProfile")
                && culture.Contains("Dialog_CAAxisEditor")
                && authoring.Contains("CultureProfiles(")
                && authoring.Contains("PoliticalProfiles("),
                "shared political/culture editors do not expose source and preset detail");
            Require(!presentation.Contains("CAInformationDetail")
                && !presentation.Contains("DrawLocalExpansion")
                && !shared.Contains("Show full details")
                && !shared.Contains("Use normal detail")
                && shared.Contains("rowHeights")
                && !shared.Contains("132f"),
                "global information detail remains or choice cards are rigid");
            report.AppendLine("PASS founding belief-versus-practice interaction");

            Require(!region.Contains("new FloatMenu")
                && region.Contains("\"Replace composition...\"")
                && region.Contains("Dialog_CAPopulationGroupEditor")
                && region.Contains("Dialog_CASettlementProgram")
                && region.Contains("Settlement Composition")
                && region.Contains("Inspect settlement composition..."),
                "Starting Region still exposes an unbounded authoring menu");
            Require(regionEditors.Contains("Faction affiliation")
                && regionEditors.Contains("Ideoligion")
                && regionEditors.Contains("Political beliefs")
                && regionEditors.Contains("Set the group's size")
                && !regionEditors.Contains("SetFacilityOverride")
                && !regionEditors.Contains("\"Generated: included\"")
                && !regionEditors.Contains("\"Include\", \"Omit\""),
                "population editing is incomplete or the rejected facility tuner remains");
            Require(region.Contains("Rect actionArea")
                && region.Contains("\"New faction\"")
                && region.Contains("\"New settlement\""),
                "Starting Region object creation is not pinned to the object rail");
            report.AppendLine("PASS Starting Region objects, population, and realized-state interaction");

            CheckInteractionContracts(report, region, regionEditors,
                foundingState, culture);

            CheckPortedAssets(repository, report,
                shared, world, region, regionEditors, founding, culture);

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

    private static void CheckInteractionContracts(StringBuilder report,
        string region, string regionEditors, string founding, string culture)
    {
        int clamped = CACreationFlowContracts.ClampMinorityShare(35, 60);
        int main = CACreationFlowContracts.MainPopulationShare(60 + clamped);
        Require(clamped == 20 && main == 20
            && 60 + clamped + main == 100,
            "minority editing can exceed the 100% population invariant");
        for (int others = 0; others <= 100; others += 5)
        {
            int share = CACreationFlowContracts.ClampMinorityShare(35, others);
            int totalMinority = Math.Min(80, others + share);
            Require(share <= CACreationFlowContracts.MaximumMinorityShare(others)
                && CACreationFlowContracts.MainPopulationShare(totalMinority)
                    >= CACreationFlowContracts.MinimumMainPopulationShare,
                "population-share cause violates its main-population constraint");
        }
        Require(region.Contains("ClampMinorityShare")
            && regionEditors.Contains("MaximumMinorityShare"),
            "the tested population-share contract is not consumed by both editors");

        int inheritedBefore = CACreationFlowContracts.EffectiveSourceKey(-1, 3);
        int inheritedAfter = CACreationFlowContracts.EffectiveSourceKey(-1, 7);
        int independentAfter = CACreationFlowContracts.EffectiveSourceKey(3, 7);
        Require(inheritedBefore == 3 && inheritedAfter == 7
            && independentAfter == 3,
            "affiliation-linked and independent belief sources are conflated");
        Require(regionEditors.Contains("Follows affiliation")
            && regionEditors.Contains("EffectiveSourceKey"),
            "belief-source binding is not visible or consumed by the editor");

        var serialized = new XElement("interaction",
            new XAttribute("minority", clamped),
            new XAttribute("main", main),
            new XAttribute("beliefSource", independentAfter));
        XElement readback = XElement.Parse(serialized.ToString(
            SaveOptions.DisableFormatting));
        Require((int)readback.Attribute("minority") == clamped
            && (int)readback.Attribute("main") == main
            && (int)readback.Attribute("beliefSource") == independentAfter,
            "realized interaction state did not survive serialization readback");

        Require(founding.Contains("custom founding terms")
            && culture.Contains("CAPoliticalBeliefsModel.Release")
            && region.Contains("item != current"),
            "edited identity or current-choice continuity remains stale");
        report.AppendLine("PASS causal interaction transitions and readback");
    }

    private static void CheckPortedAssets(string repository,
        StringBuilder report, params string[] sources)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pattern = new Regex("Rimshare/WorldMapIcons/[A-Za-z0-9 _-]+",
            RegexOptions.CultureInvariant);
        foreach (string source in sources)
            foreach (Match match in pattern.Matches(source))
                paths.Add(match.Value);
        foreach (string path in paths)
        {
            string physical = Path.Combine(repository, "Textures",
                path.Replace('/', Path.DirectorySeparatorChar) + ".png");
            Require(File.Exists(physical),
                "referenced ported asset is missing: " + path);
        }
        report.AppendLine("PASS " + paths.Count
            + " semantically referenced ported assets resolve");
    }

    private static void Require(bool condition, string failure)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(failure);
    }

    private static string FindRepository()
    {
        DirectoryInfo cursor = new DirectoryInfo(Environment.CurrentDirectory);
        while (cursor != null)
        {
            if (Directory.Exists(Path.Combine(cursor.FullName, "Source"))
                && Directory.Exists(Path.Combine(cursor.FullName, "Textures")))
                return cursor.FullName;
            cursor = cursor.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
