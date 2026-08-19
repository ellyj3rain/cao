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

    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("usage: B17FixtureGenerator <active> "
                + "<mirror> <repo>");
            return 1;
        }
        string active = Path.GetFullPath(args[0]);
        string mirror = Path.GetFullPath(args[1]);
        string repo = Path.GetFullPath(args[2]);
        byte[] beforeActive = File.ReadAllBytes(active);
        byte[] beforeMirror = File.ReadAllBytes(mirror);
        Require(beforeActive.SequenceEqual(beforeMirror),
            "active and mirror fixtures differ before B17 conversion");
        XDocument document = Parse(beforeActive);
        XElement plan = RequireB16(document);
        string invariant = AuthoredInvariant(document, plan);

        string evidence = Path.Combine(repo, "Receipts", "B17", "evidence");
        Directory.CreateDirectory(evidence);
        string evidencePath = Path.Combine(evidence,
            "active-schema15-before-b17.xml");
        if (!File.Exists(evidencePath))
            File.WriteAllBytes(evidencePath, beforeActive);
        Require(File.ReadAllBytes(evidencePath).SequenceEqual(beforeActive),
            "preserved B17 source evidence differs from the live B16 pair");

        Set(document.Root!, "authoringDataEpoch", "15");
        Set(plan, "schemaVersion", "16");
        Dictionary<int, XElement> factions = Items(plan, "factions")
            .ToDictionary(item => Integer(item, "key"));

        foreach (XElement settlement in Items(plan, "settlements"))
        {
            int factionKey = Integer(settlement, "factionKey");
            Require(factions.ContainsKey(factionKey),
                "settlement owner does not identify an authored faction");
            settlement.Element("factionKey")!.Remove();
            InsertAfter(settlement, "memberTileId",
                FactionLinks("RegionalFaction", factionKey, -1,
                    "None", -1, -1));
            UpgradePopulationGroups(settlement, requireOne: true);
        }

        int frontierIndex = 0;
        foreach (XElement holding in Items(plan, "frontierHoldings"))
        {
            int supportingKey = Integer(holding, "supportingFactionKey");
            Require(factions.TryGetValue(supportingKey,
                    out XElement supportingFaction),
                "frontier support does not identify an authored faction");
            holding.Element("supportingFactionKey")?.Remove();
            holding.Element("supportingFactionLoadId")?.Remove();
            Set(holding, "schemaVersion", "2", first: true);
            InsertAfter(holding, "form",
                FactionLinks("None", -1, -1, "RegionalFaction",
                    supportingKey, -1));

            XElement sourceCulture = Required(supportingFaction, "culture");
            XElement localCulture = new(sourceCulture);
            string parentCultureId = Value(localCulture, "id");
            Set(localCulture, "id", "culture:frontier:" + Region + ":"
                + frontierIndex);
            Set(localCulture, "parentId", parentCultureId);
            Set(localCulture, "localityKey", "frontier:" + Region + ":"
                + frontierIndex);
            Set(localCulture, "temporalBasis",
                "Frontier population at scenario start");
            localCulture.Name = "localCulture";

            XElement political = new(Required(supportingFaction,
                "politicalBeliefs"));
            political.Name = "politicalOrder";
            XElement knowledge = new(Required(supportingFaction,
                "technologicalKnowledge"));
            knowledge.Name = "technologicalKnowledge";
            XElement institutions = new(Required(supportingFaction,
                "factionStructure"));
            institutions.Name = "institutions";
            XElement localSociety = new("localSociety",
                new XElement("schemaVersion", 1),
                new XElement("explicitLocalDivergence", "True"),
                political, knowledge, institutions);

            string populationLabel = Value(sourceCulture, "name");
            XElement groups = new("populationGroups",
                new XElement("li",
                    new XElement("schemaVersion", 2),
                    new XElement("isPrimary", "True"),
                    new XElement("label", populationLabel),
                    new XElement("share", 100),
                    new XElement("factionKey", supportingKey)));
            XElement links = holding.Element("factionLinks")!;
            links.AddAfterSelf(localCulture, localSociety, groups);
            if (holding.Element("residentPawnIds") == null)
                holding.Add(new XElement("residentPawnIds"));
            holding.Element("residenceAssignments")?.Remove();
            holding.Element("residentPawnIds")!.AddAfterSelf(
                new XElement("residenceAssignments"));
            frontierIndex++;
        }

        Require(AuthoredInvariant(document, plan) == invariant,
            "B17 conversion changed plan identity, geography, or authored "
                + "faction/settlement composition");
        VerifyCurrent(plan);

        string output = Serialize(document);
        B12FixturePairCommit.Commit(active, mirror, output);
        byte[] afterActive = File.ReadAllBytes(active);
        byte[] afterMirror = File.ReadAllBytes(mirror);
        Require(afterActive.SequenceEqual(afterMirror),
            "active and mirror fixtures differ after B17 conversion");
        XDocument readback = Parse(afterActive);
        XElement readbackPlan = RequireB17(readback);
        Require(AuthoredInvariant(readback, readbackPlan) == invariant,
            "authored plan changed during B17 serialization/readback");
        VerifyCurrent(readbackPlan);

        string beforeHash = Hash(beforeActive);
        string afterHash = Hash(afterActive);
        WriteReceipt(Path.Combine(repo, "Receipts", "B17",
            "B17_AUTHORED_FIXTURE_RECEIPT.md"), active, mirror,
            beforeHash, afterHash, invariant, frontierIndex);
        Console.WriteLine("B17 fixture conversion complete");
        Console.WriteLine("3 factions; 4 settlements; " + frontierIndex
            + " frontier holdings; explicit ownership, support, population, "
            + "and local frontier state");
        Console.WriteLine("SHA256 " + afterHash);
        return 0;
    }

    private static XElement RequireB16(XDocument document)
    {
        XElement root = document.Root
            ?? throw new InvalidDataException("fixture root missing");
        Require(root.Name.LocalName == "caRegionalPendingPlan"
                && Value(root, "authoringDataEpoch") == "14"
                && Value(root, "worldIdentity") == WorldIdentity,
            "fixture is not the expected B16 pending plan");
        XElement plan = Required(root, "plan");
        Require(Value(plan, "schemaVersion") == "15",
            "fixture is not regional schema 15");
        VerifyIdentity(plan);
        return plan;
    }

    private static XElement RequireB17(XDocument document)
    {
        XElement root = document.Root
            ?? throw new InvalidDataException("fixture root missing");
        Require(root.Name.LocalName == "caRegionalPendingPlan"
                && Value(root, "authoringDataEpoch") == "15"
                && Value(root, "worldIdentity") == WorldIdentity,
            "fixture is not the expected B17 pending plan");
        XElement plan = Required(root, "plan");
        Require(Value(plan, "schemaVersion") == "16",
            "fixture is not regional schema 16");
        VerifyIdentity(plan);
        return plan;
    }

    private static void VerifyIdentity(XElement plan)
    {
        Require(Value(plan, "regionalId") == Region
                && Value(plan, "candidateId") == Candidate
                && Value(plan, "startTileId") == "389638"
                && Value(plan, "mapSize") == "350",
            "fixture identity, candidate, arrival, or map scale changed");
        Require(Items(plan, "factions").Count == 3
                && Items(plan, "settlements").Count == 4
                && Items(plan, "relations").Count == 3,
            "unexpected authored composition counts");
    }

    private static void VerifyCurrent(XElement plan)
    {
        foreach (XElement settlement in Items(plan, "settlements"))
        {
            Require(settlement.Element("factionKey") == null,
                "settlement retains the predecessor owner field");
            VerifyLinks(Required(settlement, "factionLinks"),
                "RegionalFaction", "None");
            UpgradePopulationGroups(settlement, requireOne: true,
                verifyOnly: true);
        }
        foreach (XElement holding in Items(plan, "frontierHoldings"))
        {
            Require(Value(holding, "schemaVersion") == "2"
                    && holding.Element("supportingFactionKey") == null
                    && holding.Element("supportingFactionLoadId") == null,
                "frontier holding retains predecessor affiliation state");
            VerifyLinks(Required(holding, "factionLinks"), "None",
                "RegionalFaction");
            Require(holding.Element("localCulture") != null
                    && holding.Element("localSociety") != null
                    && holding.Element("populationGroups") != null
                    && holding.Element("residentPawnIds") != null
                    && holding.Element("residenceAssignments") != null,
                "frontier holding lacks current local or residence state");
            UpgradePopulationGroups(holding, requireOne: true,
                verifyOnly: true);
        }
    }

    private static XElement FactionLinks(string ownership,
        int ownerRegional, int ownerWorld, string support,
        int supportRegional, int supportWorld) => new("factionLinks",
            new XElement("schemaVersion", 1),
            new XElement("ownership", ownership),
            new XElement("ownerRegionalFactionKey", ownerRegional),
            new XElement("ownerWorldFactionLoadId", ownerWorld),
            new XElement("support", support),
            new XElement("supportRegionalFactionKey", supportRegional),
            new XElement("supportWorldFactionLoadId", supportWorld));

    private static void VerifyLinks(XElement links, string ownership,
        string support)
    {
        Require(Value(links, "schemaVersion") == "1"
                && Value(links, "ownership") == ownership
                && Value(links, "support") == support,
            "typed site relationship is invalid");
    }

    private static void UpgradePopulationGroups(XElement owner,
        bool requireOne, bool verifyOnly = false)
    {
        List<XElement> groups = Items(owner, "populationGroups");
        Require(!requireOne || groups.Count > 0,
            "site has no represented population group");
        int primary = 0;
        for (int index = 0; index < groups.Count; index++)
        {
            XElement group = groups[index];
            if (!verifyOnly)
            {
                Set(group, "schemaVersion", "2", first: true);
                if (group.Element("isPrimary") == null)
                    InsertAfter(group, "kind",
                        new XElement("isPrimary",
                            index == 0 ? "True" : "False"));
            }
            Require(Value(group, "schemaVersion") == "2",
                "population group was not advanced to schema 2");
            if (string.Equals(Value(group, "isPrimary"), "True",
                    StringComparison.OrdinalIgnoreCase)) primary++;
        }
        Require(groups.Count == 0 || primary == 1,
            "site does not have exactly one primary population");
    }

    private static string AuthoredInvariant(XDocument document,
        XElement plan)
    {
        XElement root = document.Root!;
        return string.Join("|",
            Value(root, "worldIdentity"), Value(plan, "regionalId"),
            Value(plan, "candidateId"), Value(plan, "startTileId"),
            Value(plan, "mapSize"), Value(plan, "regionName"),
            string.Join(",", Items(plan, "memberTileIds")
                .Select(item => item.Value)),
            string.Join(",", Items(plan, "factions")
                .Select(item => Value(item, "key") + ":"
                    + Value(item, "customName") + ":"
                    + Value(item, "existingFactionLoadId") + ":"
                    + Value(item, "customFactionDefName"))),
            string.Join(",", Items(plan, "settlements")
                .Select(item => Value(item, "slot") + ":"
                    + Value(item, "memberTileId") + ":"
                    + (Value(item, "factionKey").Length > 0
                        ? Value(item, "factionKey")
                        : Value(Required(item, "factionLinks"),
                            "ownerRegionalFactionKey")) + ":"
                    + Value(item, "customName") + ":"
                    + Value(item, "residentPopulation"))),
            string.Join(",", Items(plan, "relations")
                .Select(item => Value(item, "leftFactionKey") + ":"
                    + Value(item, "rightFactionKey") + ":"
                    + Value(item, "relation"))),
            string.Join(",", Items(plan, "frontierHoldings")
                .Select(item => Value(item, "memberTileId") + ":"
                    + Value(item, "residentCount") + ":"
                    + Value(item, "form"))));
    }

    private static void InsertAfter(XElement parent, string after,
        params object[] values)
    {
        XElement anchor = parent.Element(after);
        if (anchor != null) anchor.AddAfterSelf(values);
        else parent.AddFirst(values);
    }

    private static void Set(XElement owner, string name, string value,
        bool first = false)
    {
        XElement field = owner.Element(name);
        if (field == null)
        {
            field = new XElement(name, value);
            if (first) owner.AddFirst(field); else owner.Add(field);
        }
        else field.Value = value;
    }

    private static XElement Required(XElement owner, string name) =>
        owner.Element(name)
        ?? throw new InvalidDataException(name + " is missing");

    private static List<XElement> Items(XElement owner, string name) =>
        owner.Element(name)?.Elements("li").ToList()
        ?? new List<XElement>();

    private static string Value(XElement owner, string name) =>
        owner.Element(name)?.Value?.Trim() ?? "";

    private static int Integer(XElement owner, string name) =>
        int.TryParse(Value(owner, name), out int value) ? value
        : throw new InvalidDataException(name + " is not an integer");

    private static XDocument Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static string Serialize(XDocument document)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            IndentChars = "\t",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false
        };
        using var stream = new MemoryStream();
        using (XmlWriter writer = XmlWriter.Create(stream, settings))
            document.Save(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static void WriteReceipt(string path, string active,
        string mirror, string beforeHash, string afterHash,
        string invariant, int frontiers)
    {
        string text = "# B17 Authored Fixture Receipt\n\n"
            + "Generated: " + DateTimeOffset.UtcNow.ToString(
                "yyyy-MM-dd HH:mm:ss 'UTC'") + "\n\n"
            + "| Check | Result |\n|---|---|\n"
            + "| Source evidence | PASS - exact schema-15 source retained at `Receipts/B17/evidence/active-schema15-before-b17.xml`; SHA-256 `"
            + beforeHash + "` |\n"
            + "| Pair transaction | PASS - active and mirror files were committed atomically and are byte-identical |\n"
            + "| Current schema | PASS - authoring epoch 15; regional plan 16; settlement populations 2; frontier holdings 2 |\n"
            + "| Authored composition | PASS - world, region, candidate, arrival, map scale, 3 factions, 4 settlements, 3 relations, and settlement ownership are unchanged |\n"
            + "| Frontier conversion | PASS - " + frontiers
            + " holdings now carry no faction owner, explicit material support, primary resident affiliation, local Culture, local Political Order, local Technological Knowledge, institutions, and residence ledgers |\n"
            + "| Readback | PASS - output reparses and preserves invariant `"
            + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                invariant))).Substring(0, 16) + "` |\n"
            + "| Current pair SHA-256 | `" + afterHash + "` |\n\n"
            + "Active: `" + active.Replace('\\', '/') + "`\n\n"
            + "Mirror: `" + mirror.Replace('\\', '/') + "`\n\n"
            + "This structural receipt does not claim operator visual or gameplay acceptance.\n";
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }
}
