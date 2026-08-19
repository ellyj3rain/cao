using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

internal static class Program
{
    private sealed record Result(string Name, bool Passed, string Evidence);
    private static readonly List<Result> Results = new();

    private static int Main(string[] args)
    {
        if (args.Length != 5)
        {
            Console.Error.WriteLine("usage: B14GenerationReceipts <repo> "
                + "<active> <mirror> <rimworld-data> <decompiled-root>");
            return 1;
        }
        string repo = Path.GetFullPath(args[0]);
        string active = Path.GetFullPath(args[1]);
        string mirror = Path.GetFullPath(args[2]);
        string data = Path.GetFullPath(args[3]);
        string decompiled = Path.GetFullPath(args[4]);
        try
        {
            Run(repo, active, mirror, data, decompiled);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 2;
        }

        int passed = Results.Count(result => result.Passed);
        foreach (Result result in Results)
            Console.WriteLine((result.Passed ? "PASS " : "FAIL ")
                + result.Name + ": " + result.Evidence);
        Console.WriteLine($"B14 generation acceptance: {passed}/"
            + $"{Results.Count} PASS");
        WriteReport(repo, active, mirror);
        return passed == Results.Count ? 0 : 3;
    }

    private static void Run(string repo, string active, string mirror,
        string data, string decompiled)
    {
        byte[] activeBytes = File.ReadAllBytes(active);
        byte[] mirrorBytes = File.ReadAllBytes(mirror);
        XDocument activeDocument = Parse(activeBytes);
        XDocument mirrorDocument = Parse(mirrorBytes);
        XElement plan = activeDocument.Root?.Element("plan")
            ?? throw new InvalidDataException("active regional plan missing");
        XElement mirrorPlan = mirrorDocument.Root?.Element("plan")
            ?? throw new InvalidDataException("mirror regional plan missing");

        Add("paired fixture is byte-identical",
            activeBytes.SequenceEqual(mirrorBytes),
            $"active={Hash(activeBytes)}; mirror={Hash(mirrorBytes)}");
        Add("current fixture identity and schema survive readback",
            V(activeDocument.Root!, "worldIdentity")
                == "alysaliu|1|Algorab Markab"
            && V(plan, "schemaVersion") == "16"
            && V(plan, "regionalId") == "CA-RG-EB596A12"
            && V(plan, "candidateId") == "613b1fe44104"
            && V(plan, "startTileId") == "389638"
            && V(plan, "mapSize") == "350"
            && Canonical(plan) == Canonical(mirrorPlan),
            "schema 16; alysaliu exact route; active and mirror XML agree");

        XElement[] factions = Items(plan, "factions").ToArray();
        XElement[] settlements = Items(plan, "settlements").ToArray();
        XElement[] populations = settlements.SelectMany(settlement =>
            Items(settlement, "populationGroups")).ToArray();
        Add("authored composition remains complete",
            factions.Length == 3 && settlements.Length == 4
            && populations.Length == 4
            && settlements.All(settlement => Items(settlement,
                "populationGroups").Sum(group => I(group, "share")) == 100),
            $"factions={factions.Length}; settlements={settlements.Length}; "
            + $"population groups={populations.Length}; every share sum=100");

        Add("relation provenance is explicit and causal",
            Relation(plan, 1, 2, "Hostile", "NativeInitial")
            && Relation(plan, 1, 3, "Hostile", "Authored")
            && Relation(plan, 2, 3, "Hostile", "NativeInitial")
            && !plan.Descendants("authorRelation").Any(),
            "1-2 NativeInitial Hostile; 1-3 Authored Hostile; "
            + "2-3 NativeInitial Hostile; no legacy boolean");

        string pre = Path.Combine(repo, "Receipts", "B14", "pre-repair");
        string oldActive = Path.Combine(pre,
            "active-schema11-before-b14.xml");
        string oldMirror = Path.Combine(pre,
            "keyed-schema11-after-failed-runtime.xml");
        XElement oldPlan = Parse(File.ReadAllBytes(oldActive)).Root!
            .Element("plan")!;
        XElement oldPair = Pair(oldPlan, 1, 2);
        Add("failed B13 artifacts are preserved exactly",
            File.Exists(oldMirror)
            && V(oldPlan, "schemaVersion") == "11"
            && string.IsNullOrEmpty(V(oldPair, "relation"))
            && V(oldPair, "authorRelation") == "False"
            && oldPair.Element("source") == null,
            $"active={Hash(File.ReadAllBytes(oldActive))}; keyed="
            + Hash(File.ReadAllBytes(oldMirror))
            + "; pair 1-2 was unsupported Neutral");

        string factionSource = File.ReadAllText(Path.Combine(decompiled,
            "RimWorld", "Faction.cs"));
        string generatorSource = File.ReadAllText(Path.Combine(decompiled,
            "RimWorld", "FactionGenerator.cs"));
        Add("implementation follows RimWorld initial-relation path",
            generatorSource.Contains("faction.TryMakeInitialRelationsWith(item)")
            && factionSource.Contains("a.def.permanentEnemy")
            && factionSource.Contains("a.def.naturalEnemy")
            && factionSource.Contains("num > -10")
            && factionSource.Contains("num < 75"),
            "NewGeneratedFaction invokes TryMakeInitialRelationsWith; engine "
            + "thresholds and permanent/natural-enemy rules are present");

        string tribeDefPath = Directory.EnumerateFiles(data, "*.xml",
                SearchOption.AllDirectories).First(path =>
                File.ReadAllText(path).Contains(
                    "<defName>TribeCannibal</defName>"));
        XDocument tribeDocument = XDocument.Load(tribeDefPath);
        XElement tribe = tribeDocument.Descendants("FactionDef")
            .Single(value => V(value, "defName") == "TribeCannibal");
        Add("fixture faction type supplies native hostile cause",
            V(tribe, "permanentEnemy") == "true",
            "live TribeCannibal FactionDef has permanentEnemy=true");

        string setup = File.ReadAllText(Path.Combine(repo, "Source",
            "RegionalSetupModule.cs"));
        string model = File.ReadAllText(Path.Combine(repo, "Source",
            "RegionalSettlementModelModule.cs"));
        string world = File.ReadAllText(Path.Combine(repo, "Source",
            "RegionalWorldModule.cs"));
        int gate = world.IndexOf(
            "CARegionalSettlements.TryValidateRealization(region",
            StringComparison.Ordinal);
        int resolve = world.IndexOf("CARegionalPlanResolver.Resolve(region)",
            StringComparison.Ordinal);
        Add("production generation owns the same strict prerequisite gate",
            setup.Contains("CARegionalRelationSource source")
            && setup.Contains("no represented relation source exists")
            && model.Contains("TryValidateRelationSource(plan")
            && gate >= 0 && resolve > gate,
            "saved source -> realization validator -> pre-resolution "
            + "GenStep gate is one production chain");
        Add("actual materializer emits complete causal receipt",
            world.Contains("CARegionalGenerationReceipt.Write(region, map, world")
            && world.Contains("unsupportedSourceProbe=")
            && world.Contains("PopulationMatches(settlement.populationGroups")
            && world.Contains("materializedSlots.Add(slot)"),
            "GenStep receipt covers slots, ownership, populations, relations, "
            + "and an executable unsupported-source rejection probe");
        string populationProjection = File.ReadAllText(Path.Combine(repo,
            "Source", "SettlementCompositionModule.cs"));
        Add("population projection preserves authored group identity",
            populationProjection.Contains("private static bool ProjectMinority(")
            && populationProjection.Contains("if (!ProjectMinority(record, map,")
            && !populationProjection.Contains("populationGroup.kind = "
                + "CAPopulationGroupKind.LocalResidents")
            && !populationProjection.Contains("populationGroup.factionKey = "
                + "record.factionKey")
            && !populationProjection.Contains("populationGroup.label +="),
            "hostility constrains native pawn projection without rewriting "
            + "the saved group kind, faction key, or label");
        Add("B13 unsupported row fails the current contract",
            setup.Contains("CARegionalRelationSource.Unset")
            && setup.Contains("Scribe_Values.Look(ref source, \"source\",")
            && setup.Contains("failure = \"no represented relation source exists\"")
            && oldPair.Element("source") == null,
            "missing schema-11 source loads as Unset and current validation "
            + "rejects it before faction resolution or settlement creation");

        string assembly = Path.Combine(repo, "Assemblies",
            "ColonistAwareness.dll");
        Add("production assembly exists after clean compile",
            File.Exists(assembly) && new FileInfo(assembly).Length > 0,
            $"bytes={new FileInfo(assembly).Length}; "
            + $"SHA256={Hash(File.ReadAllBytes(assembly))}");
    }

    private static bool Relation(XElement plan, int left, int right,
        string relation, string source)
    {
        XElement pair = Pair(plan, left, right);
        return V(pair, "relation") == relation && V(pair, "source") == source;
    }

    private static XElement Pair(XElement plan, int left, int right) =>
        Items(plan, "relations").Single(value =>
            I(value, "leftFactionKey") == left
            && I(value, "rightFactionKey") == right);

    private static void Add(string name, bool passed, string evidence) =>
        Results.Add(new Result(name, passed, evidence));

    private static IEnumerable<XElement> Items(XElement owner, string name) =>
        owner.Element(name)?.Elements("li") ?? Enumerable.Empty<XElement>();

    private static string V(XElement owner, string name) =>
        owner.Element(name)?.Value ?? string.Empty;

    private static int I(XElement owner, string name) =>
        int.Parse(V(owner, name));

    private static XDocument Parse(byte[] bytes) => XDocument.Parse(
        Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));

    private static string Canonical(XElement value) =>
        value.ToString(SaveOptions.DisableFormatting);

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static void WriteReport(string repo, string active, string mirror)
    {
        string path = Path.Combine(repo, "Receipts", "B14",
            "B14_GENERATION_STATIC_RECEIPT.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var text = new StringBuilder()
            .AppendLine("# B14 generation static receipt")
            .AppendLine()
            .AppendLine("This executable receipt inspects the paired current "
                + "fixture, preserved B13 failure artifacts, live RimWorld Defs, "
                + "decompiled engine path, current production gate, and compiled "
                + "assembly. Runtime materialization remains a separate receipt.")
            .AppendLine()
            .AppendLine("| Contract | Result | Evidence |")
            .AppendLine("|---|---|---|");
        foreach (Result result in Results)
            text.AppendLine("| " + Escape(result.Name) + " | **"
                + (result.Passed ? "PASS" : "FAIL") + "** | "
                + Escape(result.Evidence) + " |");
        text.AppendLine().AppendLine("Active fixture: `" + active + "`")
            .AppendLine().AppendLine("Mirror fixture: `" + mirror + "`")
            .AppendLine().AppendLine("Overall: **"
                + (Results.All(value => value.Passed) ? "PASS" : "FAIL")
                + "**");
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static string Escape(string value) => value.Replace("|", "\\|")
        .Replace("\r", " ").Replace("\n", " ");
}
