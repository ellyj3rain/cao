using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using ColonistAwareness;

// Translates the governed B8 authored fixture into the current B9 schema.
// Recovery data supplies identity and authored facts. The same pure causal
// kernel used by runtime supplies program applicability, scopes, counts,
// candidate order, and signatures.
internal static class Program
{
    private const string ExistingFaction3Def = "TribeRoughNeanderthal";

    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine(
                "usage: B9FixtureGenerator <input> <active> <mirror>");
            return 1;
        }
        XDocument doc = XDocument.Load(Path.GetFullPath(args[0]),
            LoadOptions.PreserveWhitespace);
        XElement root = doc.Root ?? throw new InvalidDataException("root missing");
        XElement plan = root.Element("plan")
            ?? throw new InvalidDataException("plan missing");
        if (Value(plan, "regionalId") != "CA-RG-EB596A12"
            || Value(plan, "candidateId") != "613b1fe44104")
            throw new InvalidDataException("unexpected governed fixture identity");

        Set(plan, "schemaVersion", "8");
        XElement[] settlements = Items(plan, "settlements").ToArray();
        if (settlements.Length != 4)
            throw new InvalidDataException("expected four settlements");
        foreach (XElement settlement in settlements)
        {
            settlement.Element("startingFacilityMask")?.Remove();
            settlement.Element("facilityExceptionMask")?.Remove();
            settlement.Element("facilityExceptionValues")?.Remove();
            settlement.Element("startingProvisions")?.Remove();
            settlement.Element("provisionArrangements")?.Remove();
            settlement.Element("settlementProgram")?.Remove();
            RefreshDerivedSettlementFacts(plan, settlement);
            // Route facts were not serialized by B8. Preserve the absence of
            // exact tile-route evidence instead of inferring a route from a
            // broad access score or the region's visual coastline.
            Set(settlement, "hasRoadAccess", "False");
            Set(settlement, "hasRiverAccess", "False");
            Set(settlement, "hasCoastalAccess", "False");
            XElement provisions = Provisions(plan, settlement);
            XElement programs = Programs(plan, settlement, provisions);
            XElement population = settlement.Element("populationGroups")
                ?? throw new InvalidDataException("population groups missing");
            population.AddAfterSelf(provisions, programs);
        }
        CARegionalFixtureContracts.StampRealizationSourceHash(plan);
        Validate(plan);
        string text = Serialize(doc);
        string active = Path.GetFullPath(args[1]);
        string mirror = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(Path.GetDirectoryName(active)!);
        Directory.CreateDirectory(Path.GetDirectoryName(mirror)!);
        File.WriteAllText(active, text, new System.Text.UTF8Encoding(false));
        File.WriteAllText(mirror, text, new System.Text.UTF8Encoding(false));
        Console.WriteLine("B9 fixture regenerated through shared causal authority");
        foreach (XElement settlement in settlements.OrderBy(item =>
            Int(item, "slot")))
            Console.WriteLine(Value(settlement, "customName") + ": "
                + Items(settlement.Element("settlementProgram"), "entries")
                    .Count() + " programs; "
                + Items(settlement, "provisionArrangements").Count()
                    + " provision arrangements");
        return 0;
    }

    private static void RefreshDerivedSettlementFacts(XElement plan,
        XElement settlement)
    {
        int population = Int(settlement, "residentPopulation");
        int land = Int(settlement, "landCapacity");
        int access = Int(settlement, "realizedAccessInfrastructure");
        int services = Int(settlement, "realizedServiceInfrastructure");
        int civic = Int(settlement, "realizedCivicInfrastructure");
        int history = Int(settlement, "historicalDevelopment");
        int economy = CAWorldTendencyCausalKernel.EconomicCapacity(population,
            civic, civic >= 1 && history >= 1, access >= 1 || services >= 1);
        int support = CAWorldTendencyCausalKernel.UrbanSupport(population,
            land, access, services, civic, economy,
            Int(settlement, "tradeConnectivity"),
            Int(settlement, "specialization"),
            Int(settlement, "realizedRole") == 1, history);
        float tendency = float.TryParse(Value(plan.Element("worldPolicy"),
            "urbanGrowthPropensity"), NumberStyles.Float,
            CultureInfo.InvariantCulture, out float parsed) ? parsed : 0.35f;
        Set(settlement, "economicCapacity", economy.ToString(
            CultureInfo.InvariantCulture));
        Set(settlement, "urbanSupport", support.ToString(
            CultureInfo.InvariantCulture));
        Set(settlement, "realizedScale",
            CAWorldTendencyCausalKernel.SettlementScale(population, support,
                tendency).ToString(CultureInfo.InvariantCulture));
    }

    private static XElement Provisions(XElement plan, XElement settlement)
    {
        XElement faction = Faction(plan, settlement);
        string AxisOf(XElement owner, string key) =>
            Axis(owner?.Element("factionStructure"), key)
                ?? Axis(owner?.Element("politicalBeliefs")
                    ?.Element("positions"), key);
        int held = Items(plan, "settlements").Count(item =>
            Int(item, "factionKey") == Int(faction, "key"));
        int authority;
        if (Bool(faction, "settlementAuthorityExplicit"))
            authority = Int(faction, "settlementAuthority");
        else if (held <= 1) authority = 0;
        else
        {
            string leadership = AxisOf(faction, "leadership");
            string participation = AxisOf(faction, "participation");
            authority = leadership switch
            {
                "none" or "whole" => 4,
                "federated" => 3,
                "council" => participation == "universal" ? 2 : 1,
                "single" => participation == "universal" && held >= 3
                    ? 1 : 0,
                _ => held >= 3 ? 1 : 0
            };
        }
        var facts = new CAProvisionCausalFacts
        {
            Support = Axis(faction.Element("factionStructure"), "support"),
            Ownership = AxisOf(faction, "ownership"),
            Work = AxisOf(faction, "work"),
            Pattern = Int(plan, "settlementPattern"),
            Authority = authority,
            Role = Int(settlement, "realizedRole"),
            Scale = Int(settlement, "realizedScale"),
            Access = Int(settlement, "realizedAccessInfrastructure"),
            Services = Int(settlement, "realizedServiceInfrastructure"),
            Civic = Int(settlement, "realizedCivicInfrastructure")
        };
        foreach (XElement group in Items(settlement, "populationGroups")
            .Where(item => Value(item, "kind") == "OtherFaction"))
        {
            int factionKey = Int(group, "politicalBeliefsFactionKey", -1);
            if (factionKey < 0) factionKey = Int(group, "factionKey", -1);
            XElement source = Items(plan, "factions").FirstOrDefault(item =>
                Int(item, "key") == factionKey);
            facts.Population.Add(new CAProvisionPopulationFact
            {
                Key = Int(group, "key"), OtherFaction = true,
                Ownership = AxisOf(source, "ownership")
            });
        }
        return new XElement("provisionArrangements",
            CAProvisionCausalKernel.Derive(facts).Select(spec =>
                new XElement("li", new XElement("schemaVersion", 2),
                    new XElement("key", spec.Key),
                    new XElement("basisKey", spec.BasisKey),
                    new XElement("basisLabel", spec.BasisLabel),
                    new XElement("operatorKind", spec.Operator),
                    spec.PopulationGroupKey >= 0
                        ? new XElement("populationGroupKey",
                            spec.PopulationGroupKey) : null,
                    new XElement("access", spec.Access),
                    new XElement("funding", spec.Funding),
                    new XElement("distribution", spec.Distribution),
                    new XElement("active", true),
                    new XElement("waterSecured", true),
                    new XElement("nodes", spec.Nodes),
                    new XElement("reach", spec.Reach))));
    }

    private static XElement Programs(XElement plan, XElement settlement,
        XElement provisions)
    {
        CASettlementProgramFacts facts = ProgramFacts(plan, settlement,
            provisions);
        string source = CASettlementProgramCausalKernel.SourceSignature(facts);
        var entries = new XElement("entries");
        foreach (CASettlementProgramCausalSpec spec in
            CASettlementProgramCausalKernel.Derive(facts))
        {
            var selected = new List<string>();
            for (int group = 0; group < spec.CandidateGroups.Count; group++)
            {
                string[] candidates = spec.CandidateGroups[group];
                int index = CASettlementProgramCausalKernel
                    .StableCandidateIndex(facts.CandidateId, facts.Slot,
                        spec.Key, group, candidates.Length);
                selected.Add(candidates[index]);
            }
            string signature = CASettlementProgramCausalKernel.EntrySignature(
                source, spec.Key, spec.Scope, spec.Count, spec.Extent,
                selected, null);
            entries.Add(new XElement("li", new XElement("schemaVersion", 1),
                new XElement("programKey", spec.Key),
                new XElement("scope", spec.Scope),
                new XElement("sourceReceipt", "shared causal kernel ["
                    + source + "]"),
                new XElement("selectedCandidates", selected.Select(value =>
                    new XElement("li", value))),
                new XElement("count", spec.Count),
                new XElement("extent", spec.Extent),
                new XElement("materializationState",
                    spec.NativeSpatialContract
                        && !spec.MaterializeSpatialContract
                            ? "present in saved geography" : "pending"),
                new XElement("placedThingIds"),
                new XElement("fallback", Fallback(spec.Key)),
                new XElement("signature", signature)));
        }
        return new XElement("settlementProgram",
            new XElement("schemaVersion", 1),
            new XElement("sourceSignature", source), entries);
    }

    private static CASettlementProgramFacts ProgramFacts(XElement plan,
        XElement settlement, XElement provisions)
    {
        XElement faction = Faction(plan, settlement);
        string defName = Value(faction, "customFactionDefName");
        if (string.IsNullOrEmpty(defName)
            && Int(faction, "existingFactionLoadId", -1) == 3)
            defName = ExistingFaction3Def;
        string technology = Technology(defName);
        int factionKey = Int(settlement, "factionKey");
        string axes = AxisSignature(faction.Element("factionStructure"));
        string beliefs = AxisSignature(
            faction.Element("politicalBeliefs")?.Element("positions"));
        string provisionFacts = string.Join(",", Items(provisions, null)
            .OrderBy(item => Value(item, "basisKey"))
            .Select(item => Value(item, "basisKey") + ":"
                + Value(item, "operatorKind") + ":" + Value(item, "access")
                + ":" + Value(item, "funding") + ":"
                + Value(item, "distribution") + ":" + Value(item, "nodes")
                + ":" + Value(item, "reach")));
        string relations = string.Join(",", Items(plan, "relations")
            .Where(item => Int(item, "leftFactionKey") == factionKey
                || Int(item, "rightFactionKey") == factionKey)
            .OrderBy(item => Int(item, "leftFactionKey"))
            .ThenBy(item => Int(item, "rightFactionKey"))
            .Select(item => Value(item, "leftFactionKey") + "-"
                + Value(item, "rightFactionKey") + "="
                + RelationValue(item)));
        var facts = new CASettlementProgramFacts
        {
            CandidateId = Value(plan, "candidateId"),
            Slot = Int(settlement, "slot"),
            TileId = Int(settlement, "memberTileId"),
            FactionKey = factionKey,
            PopulationOrigin = Value(settlement, "populationOrigin"),
            Population = Int(settlement, "residentPopulation"),
            Land = Int(settlement, "landCapacity"),
            Access = Int(settlement, "realizedAccessInfrastructure"),
            Services = Int(settlement, "realizedServiceInfrastructure"),
            Civic = Int(settlement, "realizedCivicInfrastructure"),
            Economy = Int(settlement, "economicCapacity"),
            Trade = Int(settlement, "tradeConnectivity"),
            Specialization = Int(settlement, "specialization"),
            History = Int(settlement, "historicalDevelopment"),
            Role = Int(settlement, "realizedRole"),
            Scale = Int(settlement, "realizedScale"),
            Operations = Int(settlement, "operationalRoleMask"),
            TechnologyTier = technology == "Industrial" ? 2
                : technology == "Medieval" ? 1 : 0,
            Technology = technology,
            Axes = axes,
            Beliefs = beliefs,
            Culture = CultureSignature(settlement.Element("localCulture")),
            IdeoligionPlanned = true,
            Provisions = provisionFacts,
            Relations = relations,
            DefenseRule = Axis(faction.Element("factionStructure"), "defense")
                ?? Axis(faction.Element("politicalBeliefs")
                    ?.Element("positions"), "defense"),
            LocalOrderRule = Axis(faction.Element("factionStructure"),
                    "localOrder")
                ?? Axis(faction.Element("politicalBeliefs")
                    ?.Element("positions"), "localOrder"),
            HostileRelation = Items(plan, "relations").Any(item =>
                (Int(item, "leftFactionKey") == factionKey
                    || Int(item, "rightFactionKey") == factionKey)
                && RelationValue(item) == "Hostile"),
            PublicGatheringMeaning = CultureHasPublicGathering(
                settlement.Element("localCulture")),
            HasRoad = Bool(settlement, "hasRoadAccess"),
            HasRiver = Bool(settlement, "hasRiverAccess"),
            HasCoast = Bool(settlement, "hasCoastalAccess")
        };
        foreach (XElement item in Items(provisions, null))
            facts.ProvisionFacts.Add(new CASettlementProgramProvisionFact
            {
                OperatorKind = Value(item, "operatorKind"),
                Nodes = Int(item, "nodes", 1)
            });
        return facts;
    }

    private static string Fallback(string key) => key switch
    {
        "ca.settlement.housing" => "bedroll or bed in the same program",
        "ca.settlement.food-preparation" => "campfire within the same program",
        "ca.settlement.storage" => "shelf in the same program",
        "ca.settlement.medicine" => "bed or bedroll in the same program",
        "ca.settlement.production" => "crafting spot in the same program",
        "ca.settlement.specialized-industry" => "another work table in the same program",
        "ca.settlement.custody" => "bedroll in the same program",
        "ca.settlement.defense" => "barricade or sandbags",
        "ca.settlement.research" => "simple research bench in the same program",
        "ca.settlement.religion" => "ritual spot in the same program",
        "ca.settlement.governance" or "ca.settlement.gathering" => "table and stools in the same program",
        "ca.settlement.recreation" => "horseshoes pin in the same program",
        "ca.settlement.art-memory" => "small sculpture in the same program",
        "ca.settlement.animals" => "animal sleeping box in the same program",
        var value when value.StartsWith("ca.settlement.provision.") =>
            "same-program stove, shelf, table, or seat",
        _ => "none"
    };

    private static void Validate(XElement plan)
    {
        if (Value(plan, "schemaVersion") != "8")
            throw new InvalidDataException("plan schema not 8");
        XElement[] settlements = Items(plan, "settlements").ToArray();
        if (Items(plan, "factions").Count() != 3 || settlements.Length != 4
            || settlements.Sum(item => Items(item, "populationGroups").Count())
                != 9)
            throw new InvalidDataException("authored composition changed");
        foreach (XElement settlement in settlements)
        {
            if (settlement.Elements().Any(item => item.Name.LocalName is
                "startingFacilityMask" or "facilityExceptionMask"
                or "facilityExceptionValues" or "startingProvisions"))
                throw new InvalidDataException("obsolete composition survived");
            XElement program = settlement.Element("settlementProgram");
            if (Value(program, "schemaVersion") != "1"
                || string.IsNullOrEmpty(Value(program, "sourceSignature"))
                || Items(program, "entries").Count() < 10)
                throw new InvalidDataException("program is incomplete");
            string expected = CASettlementProgramCausalKernel.SourceSignature(
                ProgramFacts(plan, settlement,
                    settlement.Element("provisionArrangements")));
            if (Value(program, "sourceSignature") != expected)
                throw new InvalidDataException("program source drifted");
        }
        XElement seat = settlements.First(item =>
            Int(item, "realizedRole") == 1);
        string[] keys = Items(seat.Element("settlementProgram"), "entries")
            .Select(item => Value(item, "programKey")).ToArray();
        string[] required = { "ca.settlement.production",
            "ca.settlement.governance", "ca.settlement.gathering",
            "ca.settlement.art-memory" };
        if (!required.All(keys.Contains))
            throw new InvalidDataException("regional seat is not substantive");
    }

    private static XElement Faction(XElement plan, XElement settlement) =>
        Items(plan, "factions").First(item =>
            Int(item, "key") == Int(settlement, "factionKey"));
    private static string Technology(string defName) => defName switch
    {
        "TribeCannibal" or "TribeRough" or ExistingFaction3Def => "Neolithic",
        _ => "Industrial"
    };
    private static bool CultureHasPublicGathering(XElement culture) =>
        culture != null && culture.Elements().Where(item =>
            item.Name.LocalName is "inheritedMeanings" or "localMeanings")
            .SelectMany(item => item.Elements("li")).Any(item =>
                Value(item, "subjectKey") == "ca.space.public_gathering"
                && Int(item, "salience") > 0);
    private static string AxisSignature(XElement axes) => string.Join(",",
        Items(axes, null).OrderBy(item => Value(item, "axis"))
            .Select(item => Value(item, "axis") + "="
                + Value(item, "option")));
    private static string CultureSignature(XElement culture)
    {
        if (culture == null) return "unrecorded";
        string meanings = string.Join("|", culture.Elements().Where(item =>
            item.Name.LocalName is "inheritedMeanings" or "localMeanings")
            .SelectMany(item => item.Elements("li"))
            .OrderBy(item => Value(item, "subjectKey"))
            .ThenBy(item => Value(item, "populationScope"))
            .Select(item => string.Join(":", new[] { Value(item, "subjectKey"),
                string.IsNullOrWhiteSpace(Value(item, "populationScope"))
                    ? "all" : Value(item, "populationScope"),
                Value(item, "approval"), Value(item, "normality"),
                Value(item, "prestige"), Value(item, "salience"),
                string.IsNullOrWhiteSpace(Value(item, "provenance"))
                    ? "recorded" : Value(item, "provenance"),
                string.IsNullOrWhiteSpace(Value(item, "sourceIdentity"))
                    ? "unrecorded" : Value(item, "sourceIdentity") })));
        string practices = string.Join("|", culture.Elements().Where(item =>
            item.Name.LocalName is "inheritedPractices" or "practices")
            .SelectMany(item => item.Elements("li"))
            .OrderBy(item => Value(item, "subjectKey"))
            .Select(item => Value(item, "subjectKey") + ":"
                + Value(item, "strength") + ":"
                + (string.IsNullOrWhiteSpace(Value(item, "sourceSignature"))
                    ? "authored" : Value(item, "sourceSignature"))));
        return CulturalHash(Value(culture, "id") + "|"
            + Value(culture, "parentId") + "|" + Value(culture, "localityKey")
            + "|" + meanings + "|" + practices);
    }
    private static string Serialize(XDocument doc)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(false), Indent = true,
            IndentChars = "\t", NewLineChars = Environment.NewLine,
            OmitXmlDeclaration = false
        };
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using (XmlWriter xml = XmlWriter.Create(writer, settings)) doc.Save(xml);
        return writer.ToString().Replace("encoding=\"utf-16\"",
            "encoding=\"utf-8\"");
    }
    private static string CulturalHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char c in value ?? "") { hash ^= c; hash *= 16777619u; }
            return hash.ToString("X8");
        }
    }
    private static string Axis(XElement axes, string key) =>
        Items(axes, null).Where(item => Value(item, "axis") == key)
            .Select(item => Value(item, "option")).FirstOrDefault();
    private static IEnumerable<XElement> Items(XElement parent, string name) =>
        name == null ? parent?.Elements("li") ?? Enumerable.Empty<XElement>()
            : parent?.Element(name)?.Elements("li")
                ?? Enumerable.Empty<XElement>();
    private static string Value(XElement parent, string name) =>
        (string)parent?.Element(name) ?? "";
    private static int Int(XElement parent, string name, int fallback = 0) =>
        int.TryParse(Value(parent, name), out int value) ? value : fallback;
    private static bool Bool(XElement parent, string name) =>
        bool.TryParse(Value(parent, name), out bool value) && value;
    private static string RelationValue(XElement relation)
    {
        string value = Value(relation, "relation");
        return string.IsNullOrEmpty(value) ? "Neutral" : value;
    }
    private static void Set(XElement parent, string name, string value)
    {
        XElement node = parent.Element(name);
        if (node == null) parent.AddFirst(new XElement(name, value));
        else node.Value = value;
    }
}
