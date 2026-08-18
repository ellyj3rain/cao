using System.Globalization;
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
        if (args.Length != 4)
        {
            Console.Error.WriteLine(
                "usage: B12FixtureGenerator <current-input> <B11-evidence> <active> <mirror>");
            return 1;
        }

        XDocument document = ReadUtf8Xml(Path.GetFullPath(args[0]));
        XDocument evidenceDocument = ReadUtf8Xml(
            Path.GetFullPath(args[1]));
        Dictionary<string, XElement> evidenceCultures = evidenceDocument
            .Descendants().Where(element => element.Name.LocalName == "culture"
                || element.Name.LocalName == "localCulture")
            .ToDictionary(element => Value(element, "id"),
                element => element, StringComparer.Ordinal);
        XElement root = document.Root
            ?? throw new InvalidDataException("fixture root missing");
        Set(root, "authoringDataEpoch", "12");
        XElement plan = root.Element("plan")
            ?? throw new InvalidDataException("regional plan missing");
        Require(Value(plan, "regionalId") == Region,
            "unexpected region identity");
        Require(Value(plan, "candidateId") == Candidate,
            "unexpected candidate identity");
        Require(Value(plan, "startTileId") == "389638"
            && Value(plan, "bundleRootTileId") == "389638",
            "unexpected arrival tile");
        Require(Value(plan, "mapSize") == "350",
            "unexpected map scale");

        int cultures = 0;
        int questions = 0;
        int evidence = 0;
        foreach (XElement culture in document.Descendants().Where(element =>
            element.Name.LocalName == "culture"
                || element.Name.LocalName == "localCulture"))
        {
            string cultureId = Value(culture, "id");
            Require(evidenceCultures.TryGetValue(cultureId,
                    out XElement sourceEvidence),
                "B11 evidence is missing Culture " + cultureId);
            (int questionCount, int evidenceCount) = UpgradeCulture(culture,
                sourceEvidence!);
            cultures++;
            questions += questionCount;
            evidence += evidenceCount;
        }

        XElement[] settlements = Items(plan, "settlements").ToArray();
        Require(Items(plan, "factions").Count() == 3,
            "fixture must preserve three factions");
        Require(settlements.Length == 4,
            "fixture must preserve four settlements");
        Require(settlements.SelectMany(item => Items(item,
            "populationGroups")).Count() == 9,
            "fixture must preserve nine population groups");
        Require(settlements.SelectMany(item => Items(item,
            "operationalFacts")).Count() == 19,
            "fixture must preserve nineteen established program facts");
        Require(cultures == 8, "fixture must preserve eight Culture records");
        Require(!document.Descendants("inheritedMeanings").Any()
            && !document.Descendants("localMeanings").Any(),
            "current fixture must not serialize B11 meanings");

        string output = Serialize(document);
        string active = Path.GetFullPath(args[2]);
        string mirror = Path.GetFullPath(args[3]);
        Directory.CreateDirectory(Path.GetDirectoryName(active)!);
        Directory.CreateDirectory(Path.GetDirectoryName(mirror)!);
        B12FixturePairCommit.Commit(active, mirror, output);

        Console.WriteLine("B12 Culture fixture migration complete");
        Console.WriteLine($"{cultures} Culture records; {questions} exact question distributions; {evidence} preserved source records");
        Console.WriteLine("3 factions; 4 settlements; 9 population groups; 19 established program facts");
        return 0;
    }

    private static (int Questions, int Evidence) UpgradeCulture(
        XElement culture, XElement sourceEvidence)
    {
        int schema = Int(culture, "schemaVersion");
        Require(schema == 9 || schema == 10,
            "Culture schema has no B12 fixture migration: " + schema);
        bool containsSupersededProjection = schema == 10
            && Items(culture, "inheritedQuestions")
                .Concat(Items(culture, "localQuestions"))
                .Any(value => Value(value, "provenance").Contains(
                    "approval, normality, prestige, and salience",
                    StringComparison.Ordinal));
        if (schema == 10 && !containsSupersededProjection)
        {
            Require(!culture.Elements("inheritedMeanings").Any()
                    && !culture.Elements("localMeanings").Any(),
                "schema-10 Culture still contains B11 meaning state: "
                    + Value(culture, "id"));
            return (Items(culture, "inheritedQuestions").Count()
                    + Items(culture, "localQuestions").Count(),
                Items(culture, "legacyEvidence").Count());
        }
        int sourceSchema = Int(sourceEvidence, "schemaVersion");
        Require(sourceSchema == 8 || sourceSchema == 9,
            "fixture evidence is not a B11 Culture record");

        string cultureId = Value(culture, "id");
        string localIdentity = Value(culture, "localityKey");
        if (localIdentity.Length == 0) localIdentity = cultureId;
        var evidence = new List<XElement>();
        List<XElement> inherited = MigrateCollection(sourceEvidence,
            "inheritedMeanings", "inherited", cultureId, evidence);
        List<XElement> local = MigrateCollection(sourceEvidence,
            "localMeanings", "local", localIdentity, evidence);
        HashSet<string> localKeys = local.Select(QuestionIdentity)
            .ToHashSet(StringComparer.Ordinal);
        inherited.RemoveAll(value => localKeys.Contains(
            QuestionIdentity(value)));

        culture.Element("inheritedMeanings")?.Remove();
        culture.Element("localMeanings")?.Remove();
        culture.Element("questionRegistryVersion")?.Remove();
        culture.Element("withinGroupSpread")?.Remove();
        culture.Element("subgroupSeparation")?.Remove();
        culture.Element("inheritedQuestions")?.Remove();
        culture.Element("localQuestions")?.Remove();
        culture.Element("legacyEvidence")?.Remove();

        XElement predecessor = culture.Element("constituents")
            ?? throw new InvalidDataException(
                "Culture constituents missing: " + cultureId);
        XElement questionRegistry = new("questionRegistryVersion", "1");
        XElement within = new("withinGroupSpread", "2");
        XElement separation = new("subgroupSeparation", "2");
        XElement inheritedQuestions = Collection("inheritedQuestions",
            inherited);
        XElement localQuestions = Collection("localQuestions", local);
        XElement legacyEvidence = Collection("legacyEvidence", evidence);
        predecessor.AddAfterSelf(questionRegistry);
        questionRegistry.AddAfterSelf(within);
        within.AddAfterSelf(separation);
        separation.AddAfterSelf(inheritedQuestions);
        inheritedQuestions.AddAfterSelf(localQuestions);
        localQuestions.AddAfterSelf(legacyEvidence);
        Set(culture, "schemaVersion", "10");
        return (inherited.Count + local.Count, evidence.Count);
    }

    private static List<XElement> MigrateCollection(XElement culture,
        string collectionName, string layer, string fallbackIdentity,
        List<XElement> evidence)
    {
        XElement collection = culture.Element(collectionName);
        XElement[] meanings = collection?.Elements("li").ToArray()
            ?? Array.Empty<XElement>();
        var result = new List<XElement>();
        foreach (IGrouping<string, XElement> group in meanings.GroupBy(
            meaning => (CACultureQuestionRegistry.QuestionForSocialSubject(
                    Value(meaning, "subjectKey")) ?? "")
                + "\0" + Scope(meaning), StringComparer.Ordinal))
        {
            string[] identity = group.Key.Split('\0');
            string questionKey = identity[0];
            foreach (XElement meaning in group)
            {
                string sourceIdentity = Value(meaning, "sourceIdentity");
                if (sourceIdentity.Length == 0)
                    sourceIdentity = fallbackIdentity.Length == 0
                        ? "B11 Culture" : fallbackIdentity;
                evidence.Add(new XElement("li",
                    new XElement("sourceKey", Value(meaning, "subjectKey")),
                    new XElement("sourceLayer",
                        "B11 " + layer + " social meaning"),
                    new XElement("populationScope", Scope(meaning)),
                    new XElement("disposition", questionKey.Length == 0
                        ? "preserved as evidence; no exact B12 question"
                        : "approval and salience mapped through exact subject adapter; other dimensions preserved as evidence"),
                    new XElement("summary", "approval="
                        + Int(meaning, "approval") + "; normality="
                        + Int(meaning, "normality") + "; prestige="
                        + Int(meaning, "prestige") + "; salience="
                        + Int(meaning, "salience") + "; weight="
                        + Weight(meaning) + "; firstRecordedTick="
                        + OptionalInt(meaning, "firstRecordedTick", -1)
                        + "; lastChangedTick="
                        + OptionalInt(meaning, "lastChangedTick", -1)),
                    new XElement("sourceIdentity", sourceIdentity),
                    new XElement("evidenceSignature",
                        Value(meaning, "evidenceSignature")),
                    new XElement("firstRecordedTick", OptionalInt(meaning,
                        "firstRecordedTick", -1)),
                    new XElement("lastChangedTick", OptionalInt(meaning,
                        "lastChangedTick", -1))));
            }
            if (questionKey.Length == 0) continue;
            CALegacyCultureQuestionAdapterResult adapted =
                CALegacyCultureQuestionAdapter.Adapt(group.Select(value =>
                    new CALegacyCultureMeaningAdapterInput(
                        Int(value, "approval"), Int(value, "salience"),
                        Weight(value), CACultureQuestionRegistry
                            .DirectionForSocialSubject(
                                Value(value, "subjectKey")))));
            string sourceIdentityForQuestion = fallbackIdentity.Length == 0
                ? "B11 Culture" : fallbackIdentity;
            string signatureSource = string.Join("|", group.Select(value =>
                Value(value, "subjectKey") + ":"
                + Int(value, "approval") + ":"
                + Int(value, "normality") + ":"
                + Int(value, "prestige") + ":"
                + Int(value, "salience") + ":" + Weight(value)));
            result.Add(new XElement("li",
                new XElement("schemaVersion", "1"),
                new XElement("questionKey", questionKey),
                new XElement("populationScope", identity.Length > 1
                    ? identity[1] : "*"),
                new XElement("mean", F(adapted.Mean)),
                new XElement("hasDescriptiveNormPrior", "False"),
                new XElement("descriptiveNormPrior", "0"),
                new XElement("prestigeSignal", "0"),
                new XElement("spread", "0.28"),
                new XElement("spreadOverride", "False"),
                new XElement("salience", F(adapted.Salience)),
                new XElement("normStrength", "0.5"),
                new XElement("visibility", "0.65"),
                new XElement("sourceConfidence", "0.55"),
                new XElement("toleranceForDivergence", "0.5"),
                new XElement("subgroups"),
                new XElement("provenance",
                    "B12 exact adapter from B11 " + layer
                        + " approval and salience; other dimensions retained as evidence"),
                new XElement("sourceIdentity", sourceIdentityForQuestion),
                new XElement("evidenceSignature", StableHash(signatureSource)),
                new XElement("firstRecordedTick", group.Where(value =>
                        OptionalInt(value, "firstRecordedTick", -1) >= 0)
                    .Select(value => OptionalInt(value,
                        "firstRecordedTick", -1)).DefaultIfEmpty(-1).Min()),
                new XElement("lastChangedTick", group.Where(value =>
                        OptionalInt(value, "lastChangedTick", -1) >= 0)
                    .Select(value => OptionalInt(value,
                        "lastChangedTick", -1)).DefaultIfEmpty(-1).Max())));
        }
        return result;
    }

    private static XElement Collection(string name,
        IEnumerable<XElement> values)
    {
        var result = new XElement(name);
        foreach (XElement value in values) result.Add(value);
        return result;
    }

    private static string QuestionIdentity(XElement question) =>
        Value(question, "questionKey") + "\0" + Scope(question);

    private static string Scope(XElement element)
    {
        string value = Value(element, "populationScope");
        return value.Length == 0 ? "*" : value;
    }

    private static int Weight(XElement meaning)
    {
        string value = Value(meaning, "weight");
        return value.Length == 0 ? 100 : int.Parse(value,
            CultureInfo.InvariantCulture);
    }

    private static string StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char character in value ?? "")
            {
                hash ^= character;
                hash *= 16777619u;
            }
            return hash.ToString("X8", CultureInfo.InvariantCulture);
        }
    }

    private static float Clamp(float value, float low, float high) =>
        value < low ? low : value > high ? high : value;

    private static string F(float value) => value.ToString("0.#######",
        CultureInfo.InvariantCulture);

    private static IEnumerable<XElement> Items(XElement parent, string name) =>
        parent?.Element(name)?.Elements("li") ?? Enumerable.Empty<XElement>();

    private static string Value(XElement parent, string name) =>
        parent?.Element(name)?.Value ?? "";

    private static int Int(XElement parent, string name) => int.Parse(
        Value(parent, name), CultureInfo.InvariantCulture);

    private static int OptionalInt(XElement parent, string name,
        int fallback)
    {
        string value = Value(parent, name);
        return value.Length == 0 ? fallback : int.Parse(value,
            CultureInfo.InvariantCulture);
    }

    private static void Set(XElement parent, string name, string value)
    {
        XElement element = parent.Element(name);
        if (element == null) parent.AddFirst(new XElement(name, value));
        else element.Value = value;
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
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
            + builder;
    }

    private static XDocument ReadUtf8Xml(string path)
    {
        return XDocument.Parse(File.ReadAllText(path, Encoding.UTF8),
            LoadOptions.PreserveWhitespace);
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidDataException(failure);
    }
}
