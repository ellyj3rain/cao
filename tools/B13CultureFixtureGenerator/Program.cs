using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ColonistAwareness;
using ColonistAwareness.Tools;

internal static class Program
{
    private const string Region = "CA-RG-EB596A12";
    private const string Candidate = "613b1fe44104";

    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "usage: B13CultureFixtureGenerator <active> <mirror>");
            return 1;
        }

        string active = Path.GetFullPath(args[0]);
        string mirror = Path.GetFullPath(args[1]);
        byte[] activeBytes = File.ReadAllBytes(active);
        byte[] mirrorBytes = File.ReadAllBytes(mirror);
        Require(activeBytes.SequenceEqual(mirrorBytes),
            "active and mirror fixtures differ before B13 conversion");

        XDocument document = XDocument.Parse(
            Encoding.UTF8.GetString(activeBytes),
            LoadOptions.PreserveWhitespace);
        XElement root = document.Root
            ?? throw new InvalidDataException("fixture root missing");
        Require(Value(root, "authoringDataEpoch") == "12",
            "unexpected pending-authoring epoch");
        XElement plan = root.Element("plan")
            ?? throw new InvalidDataException("regional plan missing");
        Require(Value(plan, "schemaVersion") == "11",
            "unexpected regional-plan schema");
        Require(Value(plan, "regionalId") == Region,
            "unexpected region identity");
        Require(Value(plan, "candidateId") == Candidate,
            "unexpected candidate identity");
        Require(Value(plan, "startTileId") == "389638"
                && Value(plan, "bundleRootTileId") == "389638",
            "unexpected arrival tile");
        Require(Value(plan, "mapSize") == "350",
            "unexpected map scale");

        string compositionBefore = CompositionSignature(plan);
        XElement[] cultures = document.Descendants().Where(element =>
                element.Name.LocalName == "culture"
                || element.Name.LocalName == "localCulture")
            .ToArray();
        Require(cultures.Length == 8,
            "fixture must preserve eight Culture records");
        int retained = 0;
        int added = 0;
        foreach (XElement culture in cultures)
        {
            (int Retained, int Added) result = CompleteCulture(culture);
            retained += result.Retained;
            added += result.Added;
        }

        Require(CompositionSignature(plan) == compositionBefore,
            "B13 Culture completion changed authored composition");
        VerifyPlan(plan, cultures);
        string output = Serialize(document);
        Directory.CreateDirectory(Path.GetDirectoryName(active)!);
        Directory.CreateDirectory(Path.GetDirectoryName(mirror)!);
        B12FixturePairCommit.Commit(active, mirror, output);

        byte[] writtenActive = File.ReadAllBytes(active);
        byte[] writtenMirror = File.ReadAllBytes(mirror);
        Require(writtenActive.SequenceEqual(writtenMirror),
            "active and mirror fixtures differ after B13 conversion");
        XDocument readback = XDocument.Load(active);
        XElement readbackPlan = readback.Root?.Element("plan")
            ?? throw new InvalidDataException("readback plan missing");
        XElement[] readbackCultures = readback.Descendants().Where(element =>
                element.Name.LocalName == "culture"
                || element.Name.LocalName == "localCulture")
            .ToArray();
        VerifyPlan(readbackPlan, readbackCultures);
        Require(CompositionSignature(readbackPlan) == compositionBefore,
            "authored composition did not survive serialization");

        Console.WriteLine("B13 Culture fixture conversion complete");
        Console.WriteLine($"{cultures.Length} Culture records; "
            + $"{retained} authored distributions retained; {added} missing root distributions added");
        Console.WriteLine("3 factions; 4 settlements; 9 population groups; 19 established program facts");
        Console.WriteLine("SHA256 "
            + Convert.ToHexString(SHA256.HashData(writtenActive)));
        return 0;
    }

    private static (int Retained, int Added) CompleteCulture(
        XElement culture)
    {
        Require(Value(culture, "schemaVersion") == "10",
            "Culture schema is not current: " + Value(culture, "id"));
        XElement registry = culture.Element("questionRegistryVersion")
            ?? throw new InvalidDataException("Culture registry missing: "
                + Value(culture, "id"));
        Require(registry.Value == "1" || registry.Value == "2",
            "Culture registry has no B13 conversion: " + registry.Value);
        XElement inherited = culture.Element("inheritedQuestions")
            ?? throw new InvalidDataException("inherited questions missing: "
                + Value(culture, "id"));
        XElement local = culture.Element("localQuestions")
            ?? throw new InvalidDataException("local questions missing: "
                + Value(culture, "id"));
        List<XElement> existing = inherited.Elements("li")
            .Concat(local.Elements("li")).ToList();
        foreach (XElement question in existing)
            VerifyDistribution(question);
        var rootKeys = new HashSet<string>(existing.Where(value =>
                Scope(value) == "*").Select(value =>
                Value(value, "questionKey")), StringComparer.Ordinal);
        Require(rootKeys.Count == existing.Count(value => Scope(value) == "*"),
            "duplicate root Culture question: " + Value(culture, "id"));

        string cultureId = Value(culture, "id");
        int diversity = Int(culture, "withinGroupSpread");
        Require(diversity >= 0 && diversity <= 4,
            "Culture diversity is outside range: " + cultureId);
        uint state = Hash(cultureId + ":b13-culture-completion");
        int added = 0;
        foreach (CACultureQuestionDef definition in
            CACultureQuestionRegistry.All)
        {
            uint rowState = Next(ref state) ^ Hash(definition.Key);
            if (rootKeys.Contains(definition.Key)) continue;
            inherited.Add(NewDistribution(definition, ref rowState,
                diversity, cultureId));
            rootKeys.Add(definition.Key);
            added++;
        }
        registry.Value = CACultureQuestionRegistry.CurrentVersion
            .ToString(CultureInfo.InvariantCulture);
        Require(rootKeys.SetEquals(CACultureQuestionRegistry.All.Select(
                value => value.Key)),
            "Culture completion did not cover the current registry: "
                + cultureId);
        return (existing.Count, added);
    }

    private static XElement NewDistribution(CACultureQuestionDef definition,
        ref uint state, int diversity, string cultureId)
    {
        float mean = Range(ref state, -0.78f, 0.78f);
        float descriptive = Clamp(mean + Range(ref state, -0.18f, 0.18f),
            -1f, 1f);
        float spread = new[] { 0.10f, 0.18f, 0.28f, 0.42f, 0.60f }[
            diversity];
        float salience = Range(ref state, 0.36f, 0.86f);
        float norm = Range(ref state, 0.28f, 0.78f);
        float tolerance = Range(ref state, 0.24f, 0.78f);
        float visibility = Range(ref state, 0.35f, 0.90f);
        float sourceConfidence = Range(ref state, 0.46f, 0.84f);
        float prestige = Range(ref state, -0.20f, 0.20f);
        string source = cultureId + ":b13-culture-completion";
        return new XElement("li",
            new XElement("schemaVersion", "1"),
            new XElement("questionKey", definition.Key),
            new XElement("populationScope", "*"),
            new XElement("mean", F(mean)),
            new XElement("hasDescriptiveNormPrior", "True"),
            new XElement("descriptiveNormPrior", F(descriptive)),
            new XElement("prestigeSignal", F(prestige)),
            new XElement("spread", F(spread)),
            new XElement("spreadOverride", "False"),
            new XElement("salience", F(salience)),
            new XElement("normStrength", F(norm)),
            new XElement("visibility", F(visibility)),
            new XElement("sourceConfidence", F(sourceConfidence)),
            new XElement("toleranceForDivergence", F(tolerance)),
            new XElement("subgroups"),
            new XElement("provenance",
                "generated missing Culture question"),
            new XElement("sourceIdentity", source),
            new XElement("evidenceSignature", StableHash(source + "|"
                + definition.Key + "|" + mean.ToString("0.000",
                    CultureInfo.InvariantCulture))),
            new XElement("firstRecordedTick", "-1"),
            new XElement("lastChangedTick", "-1"));
    }

    private static void VerifyPlan(XElement plan, XElement[] cultures)
    {
        Require(Items(plan, "factions").Count() == 3,
            "fixture must preserve three factions");
        XElement[] settlements = Items(plan, "settlements").ToArray();
        Require(settlements.Length == 4,
            "fixture must preserve four settlements");
        Require(settlements.SelectMany(value => Items(value,
                "populationGroups")).Count() == 9,
            "fixture must preserve nine population groups");
        Require(settlements.SelectMany(value => Items(value,
                "operationalFacts")).Count() == 19,
            "fixture must preserve nineteen established program facts");
        Require(cultures.Length == 8,
            "fixture must preserve eight Culture records");
        foreach (XElement culture in cultures)
        {
            Require(Value(culture, "questionRegistryVersion") == "2",
                "Culture registry is not current: " + Value(culture, "id"));
            List<XElement> roots = Items(culture, "inheritedQuestions")
                .Concat(Items(culture, "localQuestions"))
                .Where(value => Scope(value) == "*").ToList();
            Require(roots.Count == CACultureQuestionRegistry
                    .FixedQuestionCount,
                "Culture is not complete: " + Value(culture, "id"));
            Require(roots.Select(value => Value(value, "questionKey"))
                    .ToHashSet(StringComparer.Ordinal).SetEquals(
                        CACultureQuestionRegistry.All.Select(value =>
                            value.Key)),
                "Culture keys differ from the registry: "
                    + Value(culture, "id"));
            foreach (XElement question in roots) VerifyDistribution(question);
        }
    }

    private static void VerifyDistribution(XElement question)
    {
        Require(Value(question, "schemaVersion") == "1",
            "question distribution schema is not current");
        Require(CACultureQuestionRegistry.Find(Value(question,
                "questionKey")) != null,
            "question key is not registered: "
                + Value(question, "questionKey"));
        Require(Float(question, "mean", 0f) >= -1f
                && Float(question, "mean", 0f) <= 1f,
            "question mean is outside range");
        Require(Float(question, "spread", 0.28f) >= 0.06f
                && Float(question, "spread", 0.28f) <= 1f,
            "question spread is outside range");
    }

    private static string CompositionSignature(XElement plan)
    {
        IEnumerable<string> facts = Items(plan, "factions").Select(value =>
                "f:" + Value(value, "key") + ":" + Value(value, "name"))
            .Concat(Items(plan, "settlements").SelectMany(settlement =>
                new[]
                {
                    "s:" + Value(settlement, "slot") + ":"
                        + Value(settlement, "name") + ":"
                        + Value(settlement, "factionKey") + ":"
                        + Value(settlement, "tileId"),
                    "p:" + Value(settlement, "slot") + ":"
                        + string.Join(",", Items(settlement,
                            "populationGroups").Select(group =>
                                Value(group, "key") + ":"
                                + Value(group, "factionKey") + ":"
                                + Value(group, "cultureId") + ":"
                                + Value(group, "share")))
                }))
            .Concat(new[]
            {
                "plan:" + Value(plan, "regionalId") + ":"
                    + Value(plan, "candidateId") + ":"
                    + Value(plan, "bundleRootTileId") + ":"
                    + Value(plan, "startTileId") + ":"
                    + Value(plan, "mapSize")
            });
        return StableHash(string.Join("|", facts));
    }

    private static string Scope(XElement element)
    {
        string value = Value(element, "populationScope");
        return value.Length == 0 ? "*" : value;
    }

    private static float Range(ref uint state, float minimum, float maximum)
    {
        float unit = (Next(ref state) & 0x00FFFFFFu) / 16777215f;
        return minimum + (maximum - minimum) * unit;
    }

    private static uint Next(ref uint state)
    {
        if (state == 0u) state = 0x9E3779B9u;
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
    }

    private static uint Hash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char character in value ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619u;
            }
            return hash;
        }
    }

    private static string StableHash(string value) => Hash(value)
        .ToString("X8", CultureInfo.InvariantCulture);

    private static float Clamp(float value, float minimum, float maximum) =>
        Math.Max(minimum, Math.Min(maximum, value));

    private static string F(float value) => value.ToString("0.#######",
        CultureInfo.InvariantCulture);

    private static IEnumerable<XElement> Items(XElement parent, string name) =>
        parent?.Element(name)?.Elements("li") ?? Enumerable.Empty<XElement>();

    private static string Value(XElement parent, string name) =>
        parent?.Element(name)?.Value ?? string.Empty;

    private static int Int(XElement parent, string name) => int.Parse(
        Value(parent, name), CultureInfo.InvariantCulture);

    private static float Float(XElement parent, string name,
        float fallback)
    {
        string value = Value(parent, name);
        return value.Length == 0 ? fallback : float.Parse(value,
            CultureInfo.InvariantCulture);
    }

    private static string Serialize(XDocument document)
    {
        document.Declaration = null;
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            OmitXmlDeclaration = true,
            NewLineHandling = NewLineHandling.None
        };
        var builder = new StringBuilder();
        using (XmlWriter writer = XmlWriter.Create(builder, settings))
            (document.Root ?? throw new InvalidDataException(
                "fixture root missing during serialization")).Save(writer);
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + builder;
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidDataException(failure);
    }
}
