using System.Xml;
using System.Xml.Linq;

internal static class Program
{
    private static readonly string[] Axes =
    {
        "leadership", "decisions", "participation", "dissent", "ownership",
        "economy", "work", "support", "membership", "status", "localOrder",
        "defense", "warConduct"
    };

    private static readonly string[][] PoliticalVectors =
    {
        new[] { "council", "majority", "universal", "plural", "mixed",
            "mixed", "organized", "public", "open", "earned", "watch",
            "militia", "quarter" },
        new[] { "federated", "consensus", "members", "plural", "cooperative",
            "communal", "organized", "communal", "vetted", "equal", "watch",
            "levy", "combatants" },
        new[] { "single", "decree", "standing", "orthodoxy", "state",
            "planned", "duty", "public", "closed", "earned", "constabulary",
            "professional", "strength" },
        new[] { "whole", "consensus", "universal", "plural", "common",
            "communal", "organized", "communal", "open", "equal", "none",
            "militia", "quarter" }
    };

    private sealed record CultureSpec(string Id, string Name, string Visual,
        (string Key, int Approval, int Normality, int Prestige, int Salience)[] Meanings,
        (string Key, string Summary, int Strength)[] Practices);

    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("usage: B8FixtureGenerator <input> <active> <mirror>");
            return 1;
        }

        string input = Path.GetFullPath(args[0]);
        string active = Path.GetFullPath(args[1]);
        string mirror = Path.GetFullPath(args[2]);
        XDocument document = XDocument.Load(input, LoadOptions.PreserveWhitespace);
        XElement root = document.Root ?? throw new InvalidDataException("missing root");
        XElement plan = root.Element("plan") ?? throw new InvalidDataException("missing plan");

        Set(root, "authoringDataEpoch", "8", before: "worldIdentity");
        Set(plan, "schemaVersion", "6");

        CultureSpec[] factions =
        {
            new("culture:ridge-covenant", "Ridge Covenant", "Corunan",
                new[]
                {
                    ("ca.politics.public_voice", 62, 72, 28, 78),
                    ("ca.support.shared_provision", 48, 66, 35, 58),
                    ("ca.exchange.outsider_contact", 18, 48, 12, 42)
                }, new[] { ("ca.space.public_gathering", "Council gatherings at shared halls", 64) }),
            new("culture:bramble-hearths", "Bramble Hearths", "Astropolitan",
                new[]
                {
                    ("ca.status.inherited_rank", -46, 28, -38, 63),
                    ("ca.war.quarter_given", 54, 76, 36, 70),
                    ("ca.custody.humane_treatment", 44, 59, 26, 52)
                }, new[] { ("ca.support.shared_provision", "Hearth stores opened during hardship", 71) }),
            new("culture:delta-compact", "Delta Compact", "Astropolitan",
                new[]
                {
                    ("ca.exchange.outsider_contact", 68, 74, 41, 72),
                    ("ca.property.compulsory_transfer", -58, 34, -43, 69),
                    ("ca.war.quarter_given", 39, 55, 27, 48)
                }, new[] { ("ca.exchange.outsider_contact", "Guest-right at market landings", 59) })
        };

        XElement[] factionNodes = Items(plan, "factions").ToArray();
        if (factionNodes.Length != 3) throw new InvalidDataException("expected 3 factions");
        for (int i = 0; i < factionNodes.Length; i++)
        {
            Replace(factionNodes[i], "culture", Culture(factions[i], null,
                "Inherited before scenario start", "Inherited"));
            Replace(factionNodes[i], "politicalBeliefs", Politics(
                "politics:b8-faction-" + (i + 1), PoliticalVectors[i]));
        }

        XElement[] settlements = Items(plan, "settlements").ToArray();
        if (settlements.Length != 4) throw new InvalidDataException("expected 4 settlements");
        for (int i = 0; i < settlements.Length; i++)
        {
            int owner = int.Parse(Value(settlements[i], "factionKey"));
            CultureSpec inherited = factions[Math.Clamp(owner - 1, 0, 2)];
            var local = inherited with
            {
                Id = "culture:settlement:CA-RG-EB596A12:" + i,
                Name = i switch
                {
                    0 => "Megaeth Commons",
                    1 => "Red Cervexa Ward",
                    2 => "Bramble Confluence",
                    _ => "Black Delta Quays"
                }
            };
            XElement culture = Culture(local, inherited.Id,
                "Established population before scenario start", "Established");
            XElement groups = settlements[i].Element("populationGroups");
            XElement constituents = culture.Element("constituents");
            constituents.RemoveNodes();
            var sources = new Dictionary<string, (string Id, string Label,
                int Share, bool Quarter)>();
            foreach (XElement group in groups?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                int factionKey = int.TryParse(Value(group, "factionKey"),
                    out int parsedFaction) ? parsedFaction : -1;
                string sourceId = factionKey > 0 && factionKey <= factions.Length
                    ? factions[factionKey - 1].Id : null;
                string sourceLabel = factionKey > 0 && factionKey <= factions.Length
                    ? factions[factionKey - 1].Name : "Culture not recorded";
                string mergeKey = sourceId ?? "unrecorded";
                int share = int.TryParse(Value(group, "share"), out int parsedShare)
                    ? parsedShare : 0;
                bool quarter = Value(group, "quarter").Equals("True",
                    StringComparison.OrdinalIgnoreCase);
                if (sources.TryGetValue(mergeKey, out var existing))
                    sources[mergeKey] = (existing.Id, existing.Label,
                        existing.Share + share, existing.Quarter || quarter);
                else
                    sources.Add(mergeKey, (sourceId, sourceLabel, share, quarter));
            }
            foreach (var source in sources.Values.OrderByDescending(item => item.Share)
                .ThenBy(item => item.Label, StringComparer.Ordinal))
                constituents.Add(Constituent(source.Id, source.Label,
                    source.Share, source.Quarter));
            if (i == 2)
            {
                // Preserve explicit plurality: the minority interprets public
                // voice differently from the owning population within the
                // same local Culture. Both constituent scopes are direct T0
                // facts; neither depends on dereferencing another object.
                culture.Element("localMeanings")!.Add(Meaning(
                    "ca.politics.public_voice", inherited.Id, 26, 61, 14,
                    58, "authored initial local plurality", local.Id));
                culture.Element("localMeanings")!.Add(Meaning(
                    "ca.politics.public_voice", factions[0].Id, -35, 42, -20,
                    64, "authored initial local plurality", local.Id));
            }
            Replace(settlements[i], "localCulture", culture);
        }

        XElement founding = plan.Element("playerFounding")
            ?? throw new InvalidDataException("player founding missing");
        Set(founding, "schemaVersion", "3");
        var playerCulture = new CultureSpec("culture:founders-sea-commons",
            "Sea Commons", "Astropolitan",
            new[]
            {
                ("ca.support.shared_provision", 72, 68, 44, 81),
                ("ca.politics.public_voice", 57, 62, 31, 73),
                ("ca.exchange.outsider_contact", 41, 53, 24, 49)
            }, new[] { ("ca.exchange.outsider_contact", "Open exchange at landfall", 56) });
        Replace(founding, "culture", Culture(playerCulture, null,
            "Inherited by the founders at a new landing", "Inherited"));
        Replace(founding, "politicalBeliefs", Politics(
            "politics:b8-founders-sea-commons", PoliticalVectors[3]));
        Set(founding, "confirmed", "False");

        foreach (XElement culture in plan.Descendants("culture")
            .Concat(plan.Descendants("localCulture")))
        {
            culture.Element("migrationEvidence")?.Remove();
            culture.Element("legacyPresetName")?.Remove();
            culture.Element("presetName")?.Remove();
        }
        CARegionalFixtureContracts.StampRealizationSourceHash(plan);

        var settings = new XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(false),
            Indent = true,
            IndentChars = "\t",
            NewLineChars = Environment.NewLine,
            OmitXmlDeclaration = false
        };
        string serialized;
        using (var writer = new StringWriter())
        {
            using (XmlWriter xml = XmlWriter.Create(writer, settings)) document.Save(xml);
            serialized = writer.ToString().Replace("encoding=\"utf-16\"", "encoding=\"utf-8\"");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(active)!);
        Directory.CreateDirectory(Path.GetDirectoryName(mirror)!);
        File.WriteAllText(active, serialized, new System.Text.UTF8Encoding(false));
        File.WriteAllText(mirror, serialized, new System.Text.UTF8Encoding(false));
        Console.WriteLine("B8 fixture generated directly from intentional region inputs");
        Console.WriteLine("factions=3 settlements=4 cultures=8 epoch=8 planSchema=6");
        return 0;
    }

    private static XElement Culture(CultureSpec spec, string parent,
        string temporalBasis, string maturity)
    {
        string inheritedSource = parent ?? spec.Id;
        var culture = new XElement("culture",
            new XElement("schemaVersion", 8),
            new XElement("id", spec.Id),
            new XElement("name", spec.Name),
            new XElement("sourceCultureDefName", spec.Visual),
            parent == null ? null : new XElement("parentId", parent),
            new XElement("temporalBasis", temporalBasis),
            new XElement("maturity", maturity),
            new XElement("constituents", Constituent(spec.Id, spec.Name, 100, false)),
            new XElement("inheritedMeanings", spec.Meanings.Select(value => Meaning(
                value.Key, "*", value.Approval, value.Normality, value.Prestige,
                value.Salience, "inherited", inheritedSource))),
            new XElement("localMeanings"),
            new XElement("transitions"),
            new XElement("inheritedPractices", spec.Practices.Select(value =>
                new XElement("li", new XElement("subjectKey", value.Key),
                    new XElement("summary", value.Summary),
                    new XElement("strength", value.Strength),
                    new XElement("firstRecordedTick", -1),
                    new XElement("lastObservedTick", -1),
                    new XElement("sourceSignature", "b8-fixture:"
                        + inheritedSource + ":" + value.Key),
                    new XElement("sourcePeriod", "before scenario start")))),
            new XElement("practices"), new XElement("observations"),
            new XElement("authoredMask", 3));
        if (maturity == "Established") culture.Add(new XElement("formedTick", -1));
        return culture;
    }

    private static XElement Meaning(string key, string scope, int approval,
        int normality, int prestige, int salience, string provenance, string source)
        => new("li", new XElement("subjectKey", key),
            new XElement("populationScope", scope), new XElement("approval", approval),
            new XElement("normality", normality), new XElement("prestige", prestige),
            new XElement("salience", salience), new XElement("provenance", provenance),
            new XElement("sourceIdentity", source),
            new XElement("evidenceSignature", "B8-T0-" + key.Replace('.', '-')),
            new XElement("firstRecordedTick", -1), new XElement("lastChangedTick", -1),
            new XElement("weight", 100));

    private static XElement Constituent(string id, string label, int share, bool quarter)
        => new("li", id == null ? null : new XElement("cultureId", id),
            new XElement("label", label),
            new XElement("share", share), new XElement("inherited", true),
            quarter ? new XElement("separateQuarter", true) : null);

    private static XElement Politics(string id, string[] vector)
    {
        if (vector == null || vector.Length != Axes.Length)
            throw new InvalidDataException("B8 political vector must answer 13 axes");
        return new XElement("politicalBeliefs",
            new XElement("schemaVersion", 8), new XElement("id", id),
            new XElement("positions", Axes.Select((axis, index) =>
                new XElement("li", new XElement("axis", axis),
                    new XElement("option", vector[index]),
                    new XElement("source", 2)))),
            new XElement("derivationReceipts"));
    }

    private static IEnumerable<XElement> Items(XElement parent, string name)
        => parent.Element(name)?.Elements("li") ?? Enumerable.Empty<XElement>();
    private static string Value(XElement parent, string name)
        => (string)parent.Element(name) ?? "";
    private static void Replace(XElement parent, string name, XElement value)
    {
        XElement current = parent.Element(name);
        value.Name = name;
        if (current == null) parent.Add(value); else current.ReplaceWith(value);
    }
    private static void Set(XElement parent, string name, string value, string before = null)
    {
        XElement current = parent.Element(name);
        if (current != null) current.Value = value;
        else if (before != null && parent.Element(before) is XElement anchor)
            anchor.AddBeforeSelf(new XElement(name, value));
        else parent.Add(new XElement(name, value));
    }
}
