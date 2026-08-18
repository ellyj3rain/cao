using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ColonistAwareness.Tools;

internal static class Program
{
    private const string WorldIdentity = "alysaliu|1|Algorab Markab";
    private const string Region = "CA-RG-EB596A12";
    private const string Candidate = "613b1fe44104";
    private static readonly string[] Domains =
    {
        "medicine", "agriculture", "construction", "manufacturing",
        "metallurgy-materials", "electrical-systems", "chemistry",
        "logistics-preservation", "weapons-defense"
    };

    private static readonly Dictionary<string, int[]> Profiles =
        new(StringComparer.Ordinal)
        {
            { "subsistence", new[] { 1, 2, 1, 1, 0, 0, 0, 1, 1 } },
            { "early-modern", new[] { 2, 2, 2, 2, 2, 0, 1, 2, 2 } },
            { "electrified-industrial", new[] { 3, 3, 3, 3, 3, 3, 3, 3, 3 } }
        };

    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("usage: B15FixtureGenerator <active> "
                + "<mirror> <repo>");
            return 1;
        }
        string active = Path.GetFullPath(args[0]);
        string mirror = Path.GetFullPath(args[1]);
        string repo = Path.GetFullPath(args[2]);
        byte[] beforeActive = File.ReadAllBytes(active);
        byte[] beforeMirror = File.ReadAllBytes(mirror);
        Require(beforeActive.SequenceEqual(beforeMirror),
            "active and mirror fixtures differ before B15 conversion");
        XDocument document = Parse(beforeActive);
        XElement plan = RequireB14(document);
        string composition = CompositionSignature(plan);
        string identity = IdentitySignature(document, plan);

        string evidence = Path.Combine(repo, "Receipts", "B15", "evidence");
        Directory.CreateDirectory(evidence);
        File.WriteAllBytes(Path.Combine(evidence,
            "active-schema13-before-b15.xml"), beforeActive);

        Set(document.Root!, "authoringDataEpoch", "13");
        Set(plan, "schemaVersion", "14");
        foreach (XElement faction in Items(plan, "factions"))
        {
            string key = Value(faction, "key");
            string profile;
            string template;
            if (key == "1")
            {
                // The authored recovery fixture's existing load-id 3 faction
                // is the Medieval Trogaem Alliance recorded by the B14 runtime
                // test. This is the same one-time compatibility seed the live
                // migration would derive from its native faction template.
                profile = "early-modern";
                template = "existing Medieval faction load-id 3";
            }
            else
            {
                string defName = Value(faction, "customFactionDefName");
                Require(defName is "TribeCannibal" or "TribeRough",
                    "unexpected new-faction template " + defName);
                profile = "subsistence";
                template = defName;
            }
            string seed = "regional:" + Region + ":faction:" + key
                + ":technology";
            ReplaceTechnology(faction, profile, seed,
                "initial knowledge inferred once from engine template "
                    + template);
        }

        XElement founding = plan.Element("playerFounding")
            ?? throw new InvalidDataException("player founding plan missing");
        Require(Value(founding, "schemaVersion") == "3",
            "fixture is not the B14 founding schema");
        Set(founding, "schemaVersion", "4");
        string foundingSeed = "regional:" + Region
            + ":player-founding:technology";
        ReplaceTechnology(founding, "electrified-industrial", foundingSeed,
            "initial knowledge inferred once from engine template PlayerColony");

        Require(CompositionSignature(plan) == composition,
            "technological-knowledge conversion changed authored composition");
        Require(IdentitySignature(document, plan) != identity
                && IdentityWithoutVersions(document, plan)
                    == IdentityWithoutVersions(Parse(beforeActive),
                        Parse(beforeActive).Root!.Element("plan")!),
            "fixture identity or geography changed during B15 conversion");
        VerifyKnowledge(plan);

        string output = Serialize(document);
        B12FixturePairCommit.Commit(active, mirror, output);
        byte[] writtenActive = File.ReadAllBytes(active);
        byte[] writtenMirror = File.ReadAllBytes(mirror);
        Require(writtenActive.SequenceEqual(writtenMirror),
            "active and mirror fixtures differ after B15 conversion");
        XDocument readbackDocument = Parse(writtenActive);
        XElement readback = RequireB15(readbackDocument);
        Require(CompositionSignature(readback) == composition,
            "authored composition did not survive B15 readback");
        VerifyKnowledge(readback);

        string beforeHash = Hash(beforeActive);
        string afterHash = Hash(writtenActive);
        WriteReceipt(Path.Combine(repo, "Receipts", "B15",
            "B15_AUTHORED_FIXTURE_RECEIPT.md"), active, mirror, beforeHash,
            afterHash, composition);
        Console.WriteLine("B15 fixture conversion complete");
        Console.WriteLine("3 factions; 4 settlements; 4 faction/founding "
            + "technological-knowledge records");
        Console.WriteLine("SHA256 " + afterHash);
        return 0;
    }

    private static void ReplaceTechnology(XElement owner, string profile,
        string identity, string provenance)
    {
        Require(Profiles.TryGetValue(profile, out int[] ranks),
            "unknown fixture knowledge profile " + profile);
        owner.Element("technologicalKnowledge")?.Remove();
        var domains = new XElement("domains");
        for (int i = 0; i < Domains.Length; i++)
        {
            domains.Add(new XElement("li",
                new XElement("domainKey", Domains[i]),
                new XElement("understand", ranks[i]),
                new XElement("construct", ranks[i]),
                new XElement("operate", ranks[i]),
                new XElement("maintain", ranks[i]),
                new XElement("source", 1),
                new XElement("provenance", provenance)));
        }
        var technology = new XElement("technologicalKnowledge",
            new XElement("schemaVersion", 1),
            new XElement("id", StableId(identity)),
            domains,
            new XElement("knownResearchProjects"),
            new XElement("custody"),
            new XElement("availabilityHistory"),
            new XElement("distributionInitialized", "False"),
            new XElement("originSource", 0),
            new XElement("originOrigin", provenance),
            new XElement("revision", 1));
        XElement politics = owner.Element("politicalBeliefs");
        if (politics != null) politics.AddAfterSelf(technology);
        else owner.Add(technology);
    }

    private static XElement RequireB14(XDocument document)
    {
        XElement root = document.Root
            ?? throw new InvalidDataException("fixture root missing");
        Require(root.Name.LocalName == "caRegionalPendingPlan"
                && Value(root, "authoringDataEpoch") == "12"
                && Value(root, "worldIdentity") == WorldIdentity,
            "fixture is not the expected B14 pending plan");
        XElement plan = root.Element("plan")
            ?? throw new InvalidDataException("regional plan missing");
        Require(Value(plan, "schemaVersion") == "13",
            "fixture is not regional schema 13");
        VerifyIdentity(plan);
        Require(Items(plan, "factions").Count == 3
                && Items(plan, "settlements").Count == 4
                && Items(plan, "relations").Count == 3,
            "unexpected authored composition counts");
        return plan;
    }

    private static XElement RequireB15(XDocument document)
    {
        XElement root = document.Root
            ?? throw new InvalidDataException("fixture root missing");
        Require(Value(root, "authoringDataEpoch") == "13"
                && Value(root, "worldIdentity") == WorldIdentity,
            "fixture is not current pending-authoring epoch 13");
        XElement plan = root.Element("plan")
            ?? throw new InvalidDataException("regional plan missing");
        Require(Value(plan, "schemaVersion") == "14"
                && Value(plan.Element("playerFounding")!, "schemaVersion")
                    == "4",
            "fixture is not the B15 regional/founding schema");
        VerifyIdentity(plan);
        Require(Items(plan, "factions").Count == 3
                && Items(plan, "settlements").Count == 4
                && Items(plan, "relations").Count == 3,
            "unexpected B15 authored composition counts");
        return plan;
    }

    private static void VerifyIdentity(XElement plan)
    {
        Require(Value(plan, "regionalId") == Region
                && Value(plan, "candidateId") == Candidate
                && Value(plan, "startTileId") == "389638"
                && Value(plan, "mapSize") == "350",
            "unexpected fixture identity or geography");
    }

    private static void VerifyKnowledge(XElement plan)
    {
        XElement[] records = Items(plan, "factions").Select(item =>
                item.Element("technologicalKnowledge"))
            .Append(plan.Element("playerFounding")
                ?.Element("technologicalKnowledge"))
            .Where(item => item != null).Cast<XElement>().ToArray();
        Require(records.Length == 4,
            "expected three faction records and one founding record");
        foreach (XElement record in records)
        {
            Require(Value(record, "schemaVersion") == "1"
                    && !string.IsNullOrWhiteSpace(Value(record, "id"))
                    && Items(record, "domains").Count == 9
                    && Items(record, "custody").Count == 0
                    && Value(record, "distributionInitialized") == "False",
                "invalid staged technological-knowledge record");
            Require(Items(record, "domains").Select(item =>
                    Value(item, "domainKey")).SequenceEqual(Domains),
                "knowledge domains are incomplete or out of order");
        }
    }

    private static string CompositionSignature(XElement plan)
    {
        var parts = new List<string>();
        foreach (XElement faction in Items(plan, "factions").OrderBy(value =>
                     Value(value, "key"), StringComparer.Ordinal))
            parts.Add("F|" + Value(faction, "key") + "|"
                + Default(Value(faction, "source"), "ExistingWorldFaction")
                + "|" + Default(Value(faction, "existingFactionLoadId"), "-1")
                + "|" + Value(faction, "customFactionDefName") + "|"
                + Value(faction, "customName"));
        foreach (XElement settlement in Items(plan, "settlements")
                     .OrderBy(value => Value(value, "slot"),
                         StringComparer.Ordinal))
        {
            parts.Add("S|" + Value(settlement, "slot") + "|"
                + Value(settlement, "memberTileId") + "|"
                + Value(settlement, "factionKey") + "|"
                + Value(settlement, "customName") + "|"
                + Value(settlement, "siteClusterKey"));
            foreach (XElement population in Items(settlement,
                         "populationGroups").OrderBy(value =>
                         Value(value, "key"), StringComparer.Ordinal))
                parts.Add("P|" + Value(settlement, "slot") + "|"
                    + Value(population, "key") + "|"
                    + Value(population, "label") + "|"
                    + Value(population, "share") + "|"
                    + Value(population, "factionKey") + "|"
                    + Value(population, "ideoligionId"));
        }
        return Hash(Encoding.UTF8.GetBytes(string.Join("\n", parts)));
    }

    private static string IdentitySignature(XDocument document,
        XElement plan) => Hash(Encoding.UTF8.GetBytes(
            Value(document.Root!, "authoringDataEpoch") + "|"
            + Value(plan, "schemaVersion") + "|"
            + IdentityWithoutVersions(document, plan)));

    private static string IdentityWithoutVersions(XDocument document,
        XElement plan) => Value(document.Root!, "worldIdentity") + "|"
        + Value(plan, "regionalId") + "|" + Value(plan, "candidateId")
        + "|" + Value(plan, "startTileId") + "|" + Value(plan, "mapSize")
        + "|" + string.Join(",", Items(plan, "memberTileIds")
            .Select(item => item.Value));

    private static string StableId(string seed)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in seed ?? "technology")
            {
                hash ^= c;
                hash *= 16777619;
            }
            return "CA-TK-" + hash.ToString("X8");
        }
    }

    private static void WriteReceipt(string path, string active,
        string mirror, string beforeHash, string afterHash,
        string composition)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        string text = "# B15 Authored Fixture Receipt\n\n"
            + "Generated: " + now.ToUniversalTime().ToString(
                "yyyy-MM-dd HH:mm:ss 'UTC'") + " / "
            + now.ToString("yyyy-MM-dd HH:mm:ss zzz") + "\n\n"
            + "| Check | Result |\n|---|---|\n"
            + "| Identity and geography | PASS - world, region, candidate, arrival tile, member tiles, and map scale unchanged |\n"
            + "| Composition | PASS - 3 factions, 4 settlements, population groups, and relations unchanged |\n"
            + "| Canonical staging | PASS - each faction and the player founding plan carries schema-1 Technological Knowledge |\n"
            + "| Distribution | PASS - fixture carries no pawn custody; standard mode remains the initial availability model |\n"
            + "| Pair commit | PASS - active and mirror files are byte-identical after atomic replacement |\n"
            + "| Readback | PASS - schemas 14/4/1 and all four knowledge compositions survived XML readback |\n\n"
            + "Active: `" + active + "`\n\nMirror: `" + mirror + "`\n\n"
            + "Before SHA-256: `" + beforeHash + "`\n\n"
            + "After SHA-256: `" + afterHash + "`\n\n"
            + "Composition signature: `" + composition + "`\n\n"
            + "The conversion preserves the B14 source fixture in `Receipts/B15/evidence/active-schema13-before-b15.xml`.";
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    private static XDocument Parse(byte[] bytes) => XDocument.Parse(
        Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'),
        LoadOptions.PreserveWhitespace);
    private static List<XElement> Items(XElement owner, string name) =>
        owner?.Element(name)?.Elements("li").ToList()
            ?? new List<XElement>();
    private static string Value(XElement owner, string name) =>
        owner?.Element(name)?.Value ?? "";
    private static string Default(string value, string fallback) =>
        string.IsNullOrEmpty(value) ? fallback : value;
    private static void Set(XElement owner, string name, string value)
    {
        XElement existing = owner.Element(name);
        if (existing == null) owner.Add(new XElement(name, value));
        else existing.Value = value;
    }
    private static string Serialize(XDocument document)
    {
        using var stream = new MemoryStream();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "\t",
            NewLineChars = Environment.NewLine,
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false
        });
        document.Save(writer);
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static string Hash(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value));
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
