using System.Text;
using System.Xml;
using System.Xml.Linq;
using ColonistAwareness;

// The B10 fixture contains intentional authoring facts only. It deliberately
// does not implement provision, program, capability, domestic-unit, or world-
// tendency derivation. On load, CARegionalPendingPlan.RefreshDraftRealization
// sends this draft through the same production path as an operator-authored
// draft. That is the fixture/runtime causal-equivalence contract.
internal static class Program
{
    // The former B9 facility masks survive as historical evidence for this
    // one governed test composition. B10 converts that evidence into explicit
    // established-program authoring facts; runtime never reads the masks.
    private static readonly IReadOnlyDictionary<int, string[]> ProgramsBySlot =
        new Dictionary<int, string[]>
        {
            [0] = new[]
            {
                "ca.settlement.food-preparation",
                "ca.settlement.storage", "ca.settlement.medicine",
                "ca.settlement.production", "ca.settlement.gathering"
            },
            [1] = new[]
            {
                "ca.settlement.food-preparation",
                "ca.settlement.storage", "ca.settlement.medicine",
                "ca.settlement.production", "ca.settlement.custody",
                "ca.settlement.gathering"
            },
            [2] = new[]
            {
                "ca.settlement.food-preparation",
                "ca.settlement.storage", "ca.settlement.medicine",
                "ca.settlement.gathering"
            },
            [3] = new[]
            {
                "ca.settlement.food-preparation",
                "ca.settlement.storage", "ca.settlement.medicine",
                "ca.settlement.gathering"
            }
        };
    private static readonly string[] DerivedSettlementFields =
    {
        "residentPopulation", "landCapacity", "economicCapacity",
        "tradeConnectivity", "specialization", "historicalDevelopment",
        "urbanSupport", "realizedRole", "realizedScale",
        "realizedAccessInfrastructure", "realizedServiceInfrastructure",
        "realizedCivicInfrastructure", "hasRoadAccess", "hasRiverAccess",
        "hasCoastalAccess", "domesticProvisionDemands",
        "provisionArrangements", "settlementProgram"
    };

    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine(
                "usage: B10FixtureGenerator <input> <active> <mirror>");
            return 1;
        }

        XDocument document = XDocument.Load(Path.GetFullPath(args[0]),
            LoadOptions.PreserveWhitespace);
        XElement root = document.Root
            ?? throw new InvalidDataException("fixture root missing");
        XElement plan = root.Element("plan")
            ?? throw new InvalidDataException("regional plan missing");
        Require(Value(plan, "regionalId") == "CA-RG-EB596A12",
            "unexpected region identity");
        Require(Value(plan, "candidateId") == "613b1fe44104",
            "unexpected candidate identity");

        Set(root, "authoringDataEpoch", "10");
        Set(plan, "schemaVersion", "10");
        Set(plan, "confirmed", "False");

        XElement policy = plan.Element("worldPolicy");
        Remove(policy, "unaffiliatedPopulationShare");
        Remove(policy, "newLocalFactionChance");
        Remove(policy, "localFactionChance");
        Remove(policy, "regionalConflictChance");

        XElement[] settlements = Items(plan, "settlements").ToArray();
        Require(Items(plan, "factions").Count() == 3,
            "fixture must preserve three factions");
        Require(settlements.Length == 4,
            "fixture must preserve four settlements");
        Require(settlements.SelectMany(item => Items(item,
            "populationGroups")).Count() == 9,
            "fixture must preserve nine population groups");

        foreach (XElement settlement in settlements)
        {
            foreach (string field in DerivedSettlementFields)
                Remove(settlement, field);
            foreach (XElement population in Items(settlement,
                "populationGroups"))
            {
                XElement old = population.Element(
                    "independentIdeoligionKey");
                if (old != null)
                    throw new InvalidDataException("legacy independent "
                        + "Ideoligion key has no valid native Ideoligion ID "
                        + "equivalent; preserve the source and adjudicate it "
                        + "instead of converting the value");
            }
            int slot = Int(settlement, "slot");
            XElement facts = settlement.Element("operationalFacts");
            facts?.Remove();
            facts = new XElement("operationalFacts");
            settlement.Add(facts);
            int mainGroup = Items(settlement, "populationGroups")
                .Where(group => Value(group, "kind") == "Main"
                    || Value(group, "kind").Length == 0)
                .Select(group => Int(group, "key")).Single();
            foreach (string programKey in ProgramsBySlot[slot])
            {
                CAEstablishedProgramFactSpec spec =
                    CASettlementOperationalFactAuthoringKernel.Establish(
                        programKey, mainGroup, 1);
                facts.Add(FactElement(spec));
            }
        }

        Remove(plan, "frontierHoldings");
        Remove(plan, "settlementPattern");
        Remove(plan, "settlementScale");
        Remove(plan, "regionalRelationPattern");
        Remove(plan, "settlementRealizationComplete");
        Remove(plan, "settlementRealizationSourceHash");
        ValidateIdentityGraph(plan, settlements);
        Require(settlements.SelectMany(item => Items(item,
            "operationalFacts")).Count() == 19,
            "fixture must carry nineteen explicit established programs");
        string output = Serialize(document);
        string active = Path.GetFullPath(args[1]);
        string mirror = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(Path.GetDirectoryName(active)!);
        Directory.CreateDirectory(Path.GetDirectoryName(mirror)!);
        File.WriteAllText(active, output, new UTF8Encoding(false));
        File.WriteAllText(mirror, output, new UTF8Encoding(false));

        Console.WriteLine("B10 intentional-source fixture regenerated");
        Console.WriteLine("3 factions; 4 settlements; 9 population groups");
        Console.WriteLine("19 established program facts; derived state deferred to production runtime");
        return 0;
    }

    private static XElement FactElement(CAEstablishedProgramFactSpec spec)
    {
        return new XElement("li",
            new XElement("schemaVersion", "3"),
            new XElement("factKey", spec.FactKey),
            new XElement("programKey", spec.ProgramKey),
            new XElement("needSource", spec.NeedSource),
            new XElement("operatorIdentity", spec.OperatorIdentity),
            new XElement("operatorSource", spec.OperatorSource),
            new XElement("laborSource", spec.LaborSource),
            new XElement("standingSource", spec.StandingSource),
            new XElement("activitySource", spec.ActivitySource),
            new XElement("targetPopulation", spec.TargetPopulation),
            new XElement("knowledgeSource", spec.KnowledgeSource),
            new XElement("fundingSource", spec.FundingSource),
            new XElement("stockSource", spec.StockSource),
            new XElement("policyKey", spec.PolicyKey),
            new XElement("materialSource", spec.MaterialSource),
            new XElement("accessSource", spec.AccessSource),
            new XElement("maintenanceSource", spec.MaintenanceSource),
            new XElement("culturalSubjects"),
            new XElement("active", "True"),
            new XElement("provenance", spec.Provenance));
    }

    private static void ValidateIdentityGraph(XElement plan,
        IEnumerable<XElement> settlements)
    {
        HashSet<int> factionKeys = Items(plan, "factions")
            .Select(item => Int(item, "key")).ToHashSet();
        HashSet<int> members = Items(plan, "memberTileIds")
            .Select(item => int.Parse(item.Value)).ToHashSet();
        HashSet<int> slots = new();
        foreach (XElement settlement in settlements)
        {
            Require(slots.Add(Int(settlement, "slot")),
                "duplicate settlement slot");
            Require(factionKeys.Contains(Int(settlement, "factionKey")),
                "settlement faction is missing");
            Require(members.Contains(Int(settlement, "memberTileId")),
                "settlement tile is outside the region");
            XElement[] groups = Items(settlement,
                "populationGroups").ToArray();
            Require(groups.Sum(item => Int(item, "share")) == 100,
                "population shares do not total 100");
            Require(groups.Count(item => Value(item, "kind") == "Main"
                    || Value(item, "kind").Length == 0) == 1,
                "settlement must have one main population");
        }
    }

    private static IEnumerable<XElement> Items(XElement parent,
        string container)
    {
        XElement owner = container == null ? parent
            : parent?.Element(container);
        return owner?.Elements("li") ?? Enumerable.Empty<XElement>();
    }

    private static string Value(XElement parent, string name) =>
        parent?.Element(name)?.Value ?? "";

    private static int Int(XElement parent, string name) =>
        int.TryParse(Value(parent, name), out int value) ? value : -1;

    private static void Set(XElement parent, string name, string value)
    {
        XElement element = parent.Element(name);
        if (element == null) parent.Add(new XElement(name, value));
        else element.Value = value;
    }

    private static void Remove(XElement parent, string name) =>
        parent?.Element(name)?.Remove();

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    private static string Serialize(XDocument document)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "\t",
            NewLineChars = "\r\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false,
            Encoding = new UTF8Encoding(false)
        };
        using var writer = new Utf8StringWriter();
        using (XmlWriter xml = XmlWriter.Create(writer, settings))
            document.Save(xml);
        return writer.ToString();
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => new UTF8Encoding(false);
    }
}
