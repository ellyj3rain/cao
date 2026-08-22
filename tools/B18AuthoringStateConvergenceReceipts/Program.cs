using System.Text;
using System.Xml.Linq;

internal static class Program
{
    private sealed record Check(string Name, bool Passed, string Evidence);

    private static int Main(string[] args)
    {
        bool verifyOnly = args.Length == 3 && args[2] == "--verify-only";
        if (args.Length != 2 && !verifyOnly)
        {
            Console.Error.WriteLine("usage: B18AuthoringStateConvergenceReceipts "
                + "<repo> <active-fixture> [--verify-only]");
            return 1;
        }

        string repo = Path.GetFullPath(args[0]);
        string fixturePath = Path.GetFullPath(args[1]);
        string political = Read(repo,
            "Source/PoliticalOrderAuthoringModule.cs");
        string population = Read(repo,
            "Source/RegionalPopulationScreenModule.cs");
        string settlement = Read(repo,
            "Source/RegionalSettlementModelModule.cs");
        string setup = Read(repo, "Source/RegionalSetupModule.cs");
        string world = Read(repo, "Source/RegionalWorldModule.cs");
        string flora = Read(repo, "Source/FloraLoadModule.cs");

        string complete = Slice(political,
            "private static void CompleteFromPreset",
            "private static void BuildPreset");
        string generatedName = Slice(political,
            "private static string GeneratedName",
            "private static double PropertyAverage");
        string overview = Slice(political,
            "private void DrawOverview", "private void DrawFoundingRelation");
        string confirmation = Slice(settlement,
            "internal static void RealizeForConfirmation",
            "internal static void Invalidate");
        string resolver = Slice(setup,
            "internal static class CARegionalPlanResolver",
            "private static Faction CreateFaction");
        string transient = Slice(world,
            "internal CARegionalPlan RegisterTransientDeveloperExercise",
            "internal CARegionalPlan EnsureDerivedRegion");
        string mapGeneration = Slice(world,
            "internal static class CARegionalMapGenerationPatch",
            "private static void AddOnce");
        string boundedSeed = Slice(flora,
            "internal static class Patch_CABoundedRegionalPlantSeed",
            "internal static class Patch_CASparseSeedFactor");

        XDocument fixture = XDocument.Load(fixturePath);
        XElement root = fixture.Root
            ?? throw new InvalidDataException("fixture root missing");
        XElement plan = root.Element("plan")
            ?? throw new InvalidDataException("fixture plan missing");
        int factions = plan.Element("factions")?.Elements("li").Count() ?? 0;
        int settlements = plan.Element("settlements")?.Elements("li").Count()
            ?? 0;

        var checks = new List<Check>();
        void Add(string name, bool passed, string evidence) => checks.Add(
            new Check(name, passed, evidence));

        Add("Political completion uses the selected finished preset",
            complete.Contains("var complete = new CAPoliticalBeliefs()")
                && complete.Contains("BuildPreset(complete, key, source, false)")
                && complete.Contains("beliefs.questions.Add(state.Copy())"),
            "missing fields copy from a complete selected preset");
        Add("Political editor opens on decisions, not generated prose",
            political.Contains("private string group = "
                + "CAPoliticalQuestionRegistry.Authority")
                && !overview.Contains("CAPoliticalOrderModel.Description")
                && !overview.Contains("Text.CalcHeight"),
            "Authority is initial; Overview renders only the short summary");
        Add("Generated political names are concise classifications",
            !generatedName.Contains("free-enterprise market-socialist")
                && !generatedName.Contains("civic-libertarian")
                && generatedName.Contains("Civic market republic")
                && generatedName.Contains("Cooperative commonwealth"),
            "generated identity no longer serializes every configured axis");
        Add("Regional replacement evaluates the post-replacement pool",
            population.Contains("ReallocatableSettlementsForReplacement")
                && population.Contains("int sources = "
                    + "ReallocatableSettlementsForReplacement().Count"),
            "currently reserved sources remain eligible when composition is replaced");
        Add("Confirmation freezes faction causes before settlement results",
            confirmation.IndexOf("EnsureCultureAndPolitics(plan)",
                    StringComparison.Ordinal)
                < confirmation.IndexOf("DeriveSettlementPattern(plan)",
                    StringComparison.Ordinal)
                && confirmation.Contains("CAFactionAxes.Derive(plan, faction)"),
            "Culture, Political Order, knowledge, and institutions precede the realization hash");
        Add("Runtime faction resolution is projection-only",
            !resolver.Contains("EnsureCultureAndPolitics(plan)")
                && !resolver.Contains("CAFactionAxes.Derive(plan"),
            "resolver applies confirmed faction state without completing the plan");
        Add("Every confirmed start validates composition before registration",
            transient.Contains("ValidateConfirmedComposition(region")
                && transient.Contains("TryValidateRealization(region")
                && transient.Contains("TryValidateStartingSettlements(region")
                && transient.Contains("TryValidateSaved(region"),
            "developer exercises and durable starts share realization, identity, and program gates");
        Add("Projection is revalidated before map dimensions and gensteps",
            mapGeneration.IndexOf("CARegionalPlanResolver.Resolve(region)",
                    StringComparison.Ordinal)
                < mapGeneration.IndexOf(
                    "CARegionalWorldComponent.ValidateConfirmedComposition",
                    StringComparison.Ordinal)
                && mapGeneration.IndexOf(
                    "CARegionalWorldComponent.ValidateConfirmedComposition",
                    StringComparison.Ordinal)
                < mapGeneration.IndexOf("mapSize = region.BackingMapSize",
                    StringComparison.Ordinal),
            "a runtime mutation fails before backing-map allocation");
        Add("Oversized regional flora has a causal bounded seed budget",
            flora.Contains("long localArea = (long)region.mapSize * "
                    + "region.mapSize")
                && boundedSeed.Contains("HarmonyPriority(Priority.Last)")
                && boundedSeed.Contains("attempts >= budget")
                && boundedSeed.Contains("CheckSpawnWildPlantAt")
                && boundedSeed.Contains("return false"),
            "one authored source-tile area is sampled through RimWorld's native plant check");
        Add("Current operator fixture retains the failed-run composition",
            factions == 3 && settlements == 3
                && Value(plan, "schemaVersion") == "16"
                && Bool(plan, "settlementRealizationComplete")
                && Bool(plan, "confirmed") && Bool(plan, "developerExercise"),
            $"schema {Value(plan, "schemaVersion")}; {factions} factions; "
                + $"{settlements} settlements; region {Value(plan, "regionalId")}");

        int passed = checks.Count(item => item.Passed);
        foreach (Check check in checks)
            Console.WriteLine((check.Passed ? "PASS " : "FAIL ")
                + check.Name + ": " + check.Evidence);
        Console.WriteLine($"B18 authoring-state convergence: {passed}/"
            + $"{checks.Count} PASS");

        if (!verifyOnly)
            WriteReceipt(repo, fixturePath, checks);
        return passed == checks.Count ? 0 : 2;
    }

    private static void WriteReceipt(string repo, string fixturePath,
        IReadOnlyList<Check> checks)
    {
        string directory = Path.Combine(repo, "Receipts", "B18");
        Directory.CreateDirectory(directory);
        var text = new StringBuilder()
            .AppendLine("# B18 Authoring State Convergence Static Receipt")
            .AppendLine()
            .AppendLine("Generated from current source and the active operator fixture. Static evidence does not establish successful gameplay entry.")
            .AppendLine()
            .AppendLine("| Contract | Result | Evidence |")
            .AppendLine("|---|---:|---|");
        foreach (Check check in checks)
            text.Append("| ").Append(check.Name).Append(" | ")
                .Append(check.Passed ? "PASS" : "FAIL").Append(" | ")
                .Append(check.Evidence.Replace("|", "\\|")).AppendLine(" |");
        text.AppendLine().Append("Fixture: `")
            .Append(Path.GetFileName(fixturePath)).AppendLine("`");
        File.WriteAllText(Path.Combine(directory,
            "B18_AUTHORING_STATE_STATIC_RECEIPT.md"), text.ToString());
    }

    private static string Read(string repo, string relative) =>
        File.ReadAllText(Path.Combine(repo,
            relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string Slice(string source, string start, string end)
    {
        int first = source.IndexOf(start, StringComparison.Ordinal);
        int last = source.IndexOf(end, first + start.Length,
            StringComparison.Ordinal);
        if (first < 0 || last < 0)
            throw new InvalidDataException("source boundary missing: "
                + start + " -> " + end);
        return source[first..last];
    }

    private static string Value(XElement parent, string name) =>
        parent.Element(name)?.Value ?? "";

    private static bool Bool(XElement parent, string name) =>
        bool.TryParse(Value(parent, name), out bool value) && value;
}
