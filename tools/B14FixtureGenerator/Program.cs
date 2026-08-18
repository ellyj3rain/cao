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
    private static readonly string[] PropertyQuestions =
    {
        "property.land", "property.housing", "property.food",
        "property.care", "property.industry", "property.trade",
        "property.utilities", "property.infrastructure",
        "property.finance", "property.luxury", "property.security",
        "property.knowledge"
    };
    private static readonly string[] QuestionOrder =
    {
        "authority.leadership", "authority.decisions",
        "civic.participation", "civic.liberty", "civic.status",
        "civic.membership", "property.land", "property.housing",
        "property.food", "property.care", "property.industry",
        "property.trade", "property.utilities",
        "property.infrastructure", "property.finance",
        "property.luxury", "property.security", "property.knowledge",
        "economy.exchange", "economy.credit", "economy.rent",
        "economy.work", "economy.provision", "security.local",
        "security.defense", "security.conflict"
    };
    private static readonly Dictionary<string, string> FixturePresets =
        new(StringComparer.Ordinal)
        {
            { "politics:b8-faction-1", "progressive-civic-mix" },
            { "politics:b8-faction-2", "cooperative-commonwealth" },
            { "politics:b8-faction-3", "central-party" },
            { "politics:b8-founders-sea-commons", "communal-assembly" }
        };

    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("usage: B14FixtureGenerator <active> "
                + "<mirror> <evidence-directory>");
            return 1;
        }
        string active = Path.GetFullPath(args[0]);
        string mirror = Path.GetFullPath(args[1]);
        string evidence = Path.GetFullPath(args[2]);
        byte[] activeBytes = File.ReadAllBytes(active);
        byte[] mirrorBytes = File.ReadAllBytes(mirror);
        XDocument activeDocument = Parse(activeBytes);
        XDocument mirrorDocument = Parse(mirrorBytes);
        XElement activeCandidate = activeDocument.Root?.Element("plan")
            ?? throw new InvalidDataException("active regional plan missing");
        bool convertingRegion = Value(activeCandidate, "schemaVersion") == "11";
        XElement activePlan = convertingRegion
            ? RequireCurrentB13(activeDocument, false)
            : RequireCurrentB14(activeDocument, allowSchema12: true);
        XElement mirrorPlan = convertingRegion
            ? RequireCurrentB13(mirrorDocument, true)
            : RequireCurrentB14Identity(mirrorDocument,
                allowSchema12: true);
        bool convertingPolitics = activePlan.Descendants("politicalBeliefs")
            .Any(value => Value(value, "schemaVersion") == "9"
                || !Items(value, "questions").Any());

        string composition = CompositionSignature(activePlan);
        Require(CoreCompositionSignature(mirrorPlan)
                == CoreCompositionSignature(activePlan),
            "active and mirror faction/settlement identities differ before "
            + "B14 fixture preparation");

        if (convertingRegion || convertingPolitics)
        {
            Directory.CreateDirectory(evidence);
            File.WriteAllBytes(Path.Combine(evidence, convertingRegion
                ? "active-schema11-before-b14.xml"
                : "active-before-political-order-schema10.xml"), activeBytes);
            if (convertingRegion)
            {
                File.WriteAllBytes(Path.Combine(evidence,
                    "keyed-schema11-after-failed-runtime.xml"), mirrorBytes);
                File.WriteAllText(Path.Combine(evidence,
                    "fixture-divergence.txt"),
                    "The failed B13 runtime mirror retained the same three factions "
                    + "and four settlements but collapsed every settlement to its "
                    + "100% main population. The active authored plan retains the "
                    + "minority and unaffiliated population groups and is the B14 "
                    + "conversion authority." + Environment.NewLine,
                    new UTF8Encoding(false));
            }
        }

        Set(activePlan, "schemaVersion", "13");
        Set(activePlan, "confirmed", "False");
        Set(activePlan, "developerExercise", "False");
        Set(activePlan, "settlementRealizationComplete", "False");
        Set(activePlan, "settlementRealizationSourceHash", "0");
        if (convertingRegion) ConvertRelations(activePlan);
        ConvertPoliticalOrders(activePlan);
        Require(CompositionSignature(activePlan) == composition,
            "B14 relation conversion changed authored composition");
        VerifyPoliticalOrders(activePlan);

        string output = Serialize(activeDocument);
        B12FixturePairCommit.Commit(active, mirror, output);
        byte[] writtenActive = File.ReadAllBytes(active);
        byte[] writtenMirror = File.ReadAllBytes(mirror);
        Require(writtenActive.SequenceEqual(writtenMirror),
            "active and mirror fixtures differ after B14 conversion");
        XElement readback = RequireCurrentB14(Parse(writtenActive));
        Require(CompositionSignature(readback) == composition,
            "authored composition did not survive B14 serialization");

        Console.WriteLine(convertingRegion || convertingPolitics
            ? "B14 fixture conversion complete"
            : "B14 fixture reset for production rerun");
        Console.WriteLine("3 factions; 4 settlements; relation sources: "
            + "1-2 NativeInitial Hostile, 1-3 Authored Hostile, "
            + "2-3 NativeInitial Hostile");
        Console.WriteLine("SHA256 "
            + Convert.ToHexString(SHA256.HashData(writtenActive)));
        return 0;
    }

    private static void ConvertPoliticalOrders(XElement plan)
    {
        XElement[] records = plan.Descendants("politicalBeliefs").ToArray();
        Require(records.Length == 4,
            "expected three faction Political Orders and one founding order");
        foreach (XElement beliefs in records)
        {
            string id = Value(beliefs, "id");
            Require(FixturePresets.TryGetValue(id, out string preset),
                "no evidence-backed Political Order preset for " + id);
            EnsurePoliticalIdentityFields(beliefs);
            if (Value(beliefs, "schemaVersion") == "10"
                && Items(beliefs, "questions").Count == QuestionOrder.Length)
                continue;
            Require(Value(beliefs, "schemaVersion") == "9",
                "unsupported fixture Political Order schema for " + id);

            Dictionary<string, Dictionary<string, int>> state =
                BuildPreset(preset);
            var sources = QuestionOrder.ToDictionary(key => key, _ => 1,
                StringComparer.Ordinal);
            ApplyLegacyEvidence(beliefs, state, sources);

            var questions = new XElement("questions");
            foreach (string question in QuestionOrder)
            {
                Require(state.TryGetValue(question,
                        out Dictionary<string, int> options),
                    "preset did not fill " + question + " for " + id);
                questions.Add(new XElement("li",
                    new XElement("questionKey", question),
                    new XElement("options", options.Select(option =>
                        new XElement("li",
                            new XElement("optionKey", option.Key),
                            new XElement("share", option.Value),
                            new XElement("source", sources[question]))))));
            }
            beliefs.Element("questions")?.Remove();
            beliefs.Add(questions);
            Set(beliefs, "schemaVersion", "10");
        }
    }

    private static void EnsurePoliticalIdentityFields(XElement beliefs)
    {
        if (beliefs.Element("name") == null)
            beliefs.Add(new XElement("name",
                new XAttribute("IsNull", "True")));
        if (beliefs.Element("nameAuthored") == null)
            Set(beliefs, "nameAuthored", "False");
        if (beliefs.Element("nameRoll") == null)
            Set(beliefs, "nameRoll", "0");
        if (beliefs.Element("generationRoll") == null)
            Set(beliefs, "generationRoll", "0");
    }

    private static Dictionary<string, Dictionary<string, int>> BuildPreset(
        string key)
    {
        var state = new Dictionary<string, Dictionary<string, int>>(
            StringComparer.Ordinal);
        Put(state, "authority.leadership", ("council", 55),
            ("executive", 25), ("assembly", 20));
        Put(state, "authority.decisions", ("majority", 60),
            ("review", 25), ("consensus", 15));
        Put(state, "civic.participation", ("residents", 75),
            ("citizens", 25));
        Put(state, "civic.liberty", ("protected", 100));
        Put(state, "civic.status", ("equal", 70), ("merit", 30));
        Put(state, "civic.membership", ("open", 65), ("vetted", 35));
        PropertyPattern(state, 40, 25, 25, 10);
        Put(state, "property.land", ("private", 55),
            ("cooperative", 15), ("common", 20), ("public", 10));
        Put(state, "property.utilities", ("public", 55),
            ("cooperative", 25), ("private", 15), ("common", 5));
        Put(state, "property.infrastructure", ("public", 70),
            ("cooperative", 15), ("private", 10), ("common", 5));
        Put(state, "property.finance", ("private", 35),
            ("cooperative", 30), ("public", 35));
        Put(state, "economy.exchange", ("market", 35),
            ("regulated", 50), ("planned", 10), ("communal", 5));
        Put(state, "economy.credit", ("private", 35), ("public", 35),
            ("cooperative", 30));
        Put(state, "economy.rent", ("regulated", 100));
        Put(state, "economy.work", ("contract", 45),
            ("cooperative", 35), ("public", 15), ("household", 5));
        Put(state, "economy.provision", ("public", 45),
            ("common", 25), ("household", 20), ("voluntary", 10));
        Put(state, "security.local", ("watch", 45),
            ("professional", 40), ("adhoc", 15));
        Put(state, "security.defense", ("militia", 45),
            ("professional", 35), ("levy", 10), ("adhoc", 10));
        Put(state, "security.conflict", ("quarter", 50),
            ("combatants", 50));

        if (key == "progressive-civic-mix")
        {
            Put(state, "authority.leadership", ("executive", 50),
                ("council", 30), ("assembly", 20));
            Put(state, "authority.decisions", ("directive", 25),
                ("majority", 45), ("review", 30));
            Put(state, "property.food", ("public", 45),
                ("cooperative", 30), ("private", 25));
            Put(state, "property.care", ("public", 55),
                ("cooperative", 25), ("private", 20));
            Put(state, "property.industry", ("private", 45),
                ("cooperative", 40), ("public", 15));
            Put(state, "property.luxury", ("private", 55),
                ("cooperative", 35), ("public", 10));
            Put(state, "economy.rent", ("noExtraction", 100));
        }
        else if (key == "cooperative-commonwealth")
        {
            Put(state, "authority.leadership", ("council", 60),
                ("federal", 25), ("assembly", 15));
            PropertyPattern(state, 10, 55, 25, 10);
            Put(state, "economy.exchange", ("communal", 45),
                ("regulated", 35), ("market", 20));
            Put(state, "economy.credit", ("cooperative", 70),
                ("public", 30));
            Put(state, "economy.work", ("cooperative", 70),
                ("household", 20), ("public", 10));
            Put(state, "economy.provision", ("common", 60),
                ("public", 30), ("household", 10));
        }
        else if (key == "communal-assembly")
        {
            Put(state, "authority.leadership", ("assembly", 65),
                ("council", 25), ("federal", 10));
            Put(state, "authority.decisions", ("consensus", 55),
                ("majority", 45));
            PropertyPattern(state, 5, 25, 15, 55);
            Put(state, "economy.exchange", ("communal", 70),
                ("regulated", 20), ("market", 10));
            Put(state, "economy.provision", ("common", 75),
                ("public", 15), ("household", 10));
            Put(state, "security.local", ("watch", 65), ("adhoc", 35));
            Put(state, "security.defense", ("militia", 70),
                ("levy", 20), ("adhoc", 10));
        }
        else if (key == "central-party")
        {
            Put(state, "authority.leadership", ("executive", 65),
                ("council", 35));
            Put(state, "authority.decisions", ("directive", 70),
                ("custom", 30));
            Put(state, "civic.participation", ("citizens", 65),
                ("workers", 35));
            Put(state, "civic.liberty", ("orthodox", 100));
            PropertyPattern(state, 5, 10, 80, 5);
            Put(state, "economy.exchange", ("planned", 75),
                ("regulated", 20), ("communal", 5));
            Put(state, "economy.credit", ("public", 80),
                ("restricted", 20));
            Put(state, "economy.work", ("public", 60), ("duty", 30),
                ("cooperative", 10));
            Put(state, "security.local", ("executive", 55),
                ("professional", 45));
            Put(state, "security.defense", ("professional", 70),
                ("levy", 30));
        }
        else throw new InvalidDataException("unsupported fixture preset " + key);
        return state;
    }

    private static void PropertyPattern(
        Dictionary<string, Dictionary<string, int>> state,
        int privateShare, int cooperative, int publicShare, int common)
    {
        foreach (string question in PropertyQuestions)
            Put(state, question, ("private", privateShare),
                ("cooperative", cooperative), ("public", publicShare),
                ("common", common));
    }

    private static void Put(
        Dictionary<string, Dictionary<string, int>> state,
        string question, params (string Key, int Share)[] options)
    {
        state[question] = options.ToDictionary(option => option.Key,
            option => option.Share, StringComparer.Ordinal);
    }

    private static void ApplyLegacyEvidence(XElement beliefs,
        Dictionary<string, Dictionary<string, int>> state,
        Dictionary<string, int> sources)
    {
        var crosswalk = new Dictionary<string,
            (string Question, Dictionary<string, string> Options)>(
                StringComparer.Ordinal)
        {
            { "leadership", ("authority.leadership", Map(("single", "executive"), ("council", "council"), ("whole", "assembly"), ("federated", "federal"), ("none", "assembly"))) },
            { "decisions", ("authority.decisions", Map(("decree", "directive"), ("majority", "majority"), ("consensus", "consensus"), ("custom", "custom"))) },
            { "participation", ("civic.participation", Map(("universal", "residents"), ("members", "citizens"), ("standing", "standing"), ("heads", "households"))) },
            { "dissent", ("civic.liberty", Map(("plural", "protected"), ("majoritarian", "majoritarian"), ("orthodoxy", "orthodox"), ("customary", "customary"))) },
            { "status", ("civic.status", Map(("equal", "equal"), ("earned", "merit"), ("hereditary", "hereditary"), ("castes", "caste"))) },
            { "membership", ("civic.membership", Map(("open", "open"), ("vetted", "vetted"), ("hereditary", "descent"), ("closed", "closed"))) },
            { "ownership", ("property.industry", Map(("private", "private"), ("cooperative", "cooperative"), ("common", "common"), ("state", "public"))) },
            { "economy", ("economy.exchange", Map(("market", "market"), ("planned", "planned"), ("communal", "communal"))) },
            { "work", ("economy.work", Map(("contract", "contract"), ("organized", "cooperative"), ("duty", "duty"), ("household", "household"))) },
            { "support", ("economy.provision", Map(("private", "household"), ("public", "public"), ("communal", "common"), ("charitable", "voluntary"))) },
            { "localOrder", ("security.local", Map(("none", "adhoc"), ("watch", "watch"), ("constabulary", "professional"), ("rulers", "executive"))) },
            { "defense", ("security.defense", Map(("none", "adhoc"), ("levy", "levy"), ("militia", "militia"), ("professional", "professional"), ("caste", "hereditary"))) },
            { "warConduct", ("security.conflict", Map(("quarter", "quarter"), ("combatants", "combatants"), ("strength", "unrestricted"))) }
        };
        foreach (IGrouping<string, XElement> group in Items(beliefs,
                     "positions").GroupBy(item => Value(item, "axis"),
                     StringComparer.Ordinal))
        {
            if (!crosswalk.TryGetValue(group.Key, out var mapping)) continue;
            string[] options = group.Select(item => Value(item, "option"))
                .Where(mapping.Options.ContainsKey).Select(item =>
                    mapping.Options[item]).Distinct(StringComparer.Ordinal)
                .ToArray();
            if (options.Length == 0) continue;
            int floor = 100 / options.Length;
            int remainder = 100 - floor * options.Length;
            var shares = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < options.Length; i++)
                shares[options[i]] = floor + (i < remainder ? 1 : 0);
            state[mapping.Question] = shares;
            sources[mapping.Question] = 2;
        }
    }

    private static Dictionary<string, string> Map(
        params (string Old, string Current)[] values) => values.ToDictionary(
            value => value.Old, value => value.Current, StringComparer.Ordinal);

    private static void VerifyPoliticalOrders(XElement plan)
    {
        XElement[] records = plan.Descendants("politicalBeliefs").ToArray();
        Require(records.Length == 4,
            "Political Order record count changed during conversion");
        foreach (XElement beliefs in records)
        {
            Require(Value(beliefs, "schemaVersion") == "10",
                "Political Order schema did not advance for "
                    + Value(beliefs, "id"));
            XElement[] states = Items(beliefs, "questions").ToArray();
            Require(states.Length == QuestionOrder.Length
                    && states.Select(item => Value(item, "questionKey"))
                        .SequenceEqual(QuestionOrder),
                "Political Order questions are incomplete or out of order for "
                    + Value(beliefs, "id"));
            foreach (XElement state in states)
            {
                XElement[] options = Items(state, "options").ToArray();
                Require(options.Length > 0 && options.All(option =>
                        int.TryParse(Value(option, "share"), out int share)
                            && share > 0)
                    && options.Sum(option => int.Parse(Value(option,
                        "share"))) == 100,
                    Value(beliefs, "id") + ":" + Value(state,
                        "questionKey") + " is not a complete composition");
                if (Value(state, "questionKey") is "civic.liberty"
                    or "economy.rent")
                    Require(options.Length == 1,
                        Value(state, "questionKey")
                            + " must remain an exclusive choice");
            }
        }
    }

    private static XElement RequireCurrentB13(XDocument document,
        bool confirmed)
    {
        XElement root = document.Root
            ?? throw new InvalidDataException("fixture root missing");
        Require(Value(root, "authoringDataEpoch") == "12",
            "unexpected pending-authoring epoch");
        Require(Value(root, "worldIdentity") == WorldIdentity,
            "unexpected world identity");
        XElement plan = root.Element("plan")
            ?? throw new InvalidDataException("regional plan missing");
        Require(Value(plan, "schemaVersion") == "11",
            "fixture is not the B13 regional schema");
        Require(Value(plan, "regionalId") == Region
                && Value(plan, "candidateId") == Candidate
                && Value(plan, "startTileId") == "389638"
                && Value(plan, "mapSize") == "350",
            "unexpected fixture identity or geography");
        Require(Value(plan, "confirmed") == confirmed.ToString(),
            "unexpected fixture confirmation state");
        Require(Items(plan, "factions").Count == 3
                && Items(plan, "settlements").Count == 4
                && Items(plan, "relations").Count == 3,
            "unexpected authored composition counts");
        return plan;
    }

    private static XElement RequireCurrentB14(XDocument document,
        bool allowSchema12 = false)
    {
        XElement plan = RequireCurrentB14Identity(document,
            allowSchema12);
        Require(Value(plan, "confirmed") == "False"
                && Value(plan, "settlementRealizationComplete") == "False"
                && Value(plan, "settlementRealizationSourceHash") == "0",
            "converted fixture must re-realize through current production code");
        VerifyRelation(plan, 1, 2, "Hostile", "NativeInitial");
        VerifyRelation(plan, 1, 3, "Hostile", "Authored");
        VerifyRelation(plan, 2, 3, "Hostile", "NativeInitial");
        Require(!plan.Descendants("authorRelation").Any(),
            "obsolete relation boolean survived conversion");
        return plan;
    }

    private static XElement RequireCurrentB14Identity(XDocument document,
        bool allowSchema12 = false)
    {
        XElement root = document.Root
            ?? throw new InvalidDataException("fixture root missing");
        Require(Value(root, "authoringDataEpoch") == "12",
            "unexpected pending-authoring epoch");
        Require(Value(root, "worldIdentity") == WorldIdentity,
            "unexpected world identity");
        XElement plan = root.Element("plan")
            ?? throw new InvalidDataException("regional plan missing");
        string schema = Value(plan, "schemaVersion");
        Require(schema == "13" || allowSchema12 && schema == "12",
            "fixture is not the B14 regional schema");
        Require(Value(plan, "regionalId") == Region
                && Value(plan, "candidateId") == Candidate
                && Value(plan, "startTileId") == "389638"
                && Value(plan, "mapSize") == "350",
            "unexpected fixture identity or geography");
        Require(Items(plan, "factions").Count == 3
                && Items(plan, "settlements").Count == 4
                && Items(plan, "relations").Count == 3,
            "unexpected authored composition counts");
        return plan;
    }

    private static void ConvertRelations(XElement plan)
    {
        XElement[] relations = Items(plan, "relations").ToArray();
        foreach (XElement relation in relations)
        {
            int left = int.Parse(Value(relation, "leftFactionKey"));
            int right = int.Parse(Value(relation, "rightFactionKey"));
            bool authored = relation.Element("authorRelation") == null
                || bool.Parse(Value(relation, "authorRelation"));
            relation.Element("authorRelation")?.Remove();
            if (authored)
            {
                Require(left == 1 && right == 3
                        && Value(relation, "relation") == "Hostile",
                    "unexpected authored relation in current fixture");
                Set(relation, "source", "Authored");
                continue;
            }
            Require((left == 1 && right == 2)
                    || (left == 2 && right == 3),
                "unexpected unrepresented B13 relation pair");
            // Both pairs contain TribeCannibal. RimWorld's native initial
            // relation rule makes a permanentEnemy FactionDef hostile in both
            // directions; the conversion is deliberately fixture-specific.
            Set(relation, "relation", "Hostile");
            Set(relation, "source", "NativeInitial");
        }
    }

    private static void VerifyRelation(XElement plan, int left, int right,
        string relation, string source)
    {
        XElement row = Items(plan, "relations").Single(value =>
            Value(value, "leftFactionKey") == left.ToString()
            && Value(value, "rightFactionKey") == right.ToString());
        Require(Value(row, "relation") == relation
                && Value(row, "source") == source,
            $"relation {left}-{right} did not survive readback");
    }

    private static string CompositionSignature(XElement plan)
    {
        var parts = new List<string>();
        foreach (XElement faction in Items(plan, "factions").OrderBy(value =>
                     Value(value, "key")))
            parts.Add("F|" + Value(faction, "key") + "|"
                + Default(Value(faction, "source"), "ExistingWorldFaction")
                + "|" + Default(Value(faction, "existingFactionLoadId"), "-1")
                + "|" + Value(faction, "customFactionDefName") + "|"
                + Value(faction, "customName"));
        foreach (XElement settlement in Items(plan, "settlements")
                     .OrderBy(value => Value(value, "slot")))
        {
            parts.Add("S|" + Value(settlement, "slot") + "|"
                + Value(settlement, "memberTileId") + "|"
                + Value(settlement, "factionKey") + "|"
                + Value(settlement, "customName") + "|"
                + Value(settlement, "siteClusterKey"));
            foreach (XElement population in Items(settlement,
                         "populationGroups").OrderBy(value =>
                         Value(value, "key")))
                parts.Add("P|" + Value(settlement, "slot") + "|"
                    + Value(population, "key") + "|"
                    + Value(population, "label") + "|"
                    + Value(population, "share") + "|"
                    + Value(population, "factionKey") + "|"
                    + Value(population, "ideoligionId"));
        }
        string value = string.Join("\n", parts);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static string CoreCompositionSignature(XElement plan)
    {
        var parts = new List<string>();
        foreach (XElement faction in Items(plan, "factions").OrderBy(value =>
                     Value(value, "key")))
            parts.Add("F|" + Value(faction, "key") + "|"
                + Default(Value(faction, "source"), "ExistingWorldFaction")
                + "|" + Default(Value(faction, "existingFactionLoadId"), "-1")
                + "|" + Value(faction, "customFactionDefName") + "|"
                + Value(faction, "customName"));
        foreach (XElement settlement in Items(plan, "settlements")
                     .OrderBy(value => Value(value, "slot")))
            parts.Add("S|" + Value(settlement, "slot") + "|"
                + Value(settlement, "memberTileId") + "|"
                + Value(settlement, "factionKey") + "|"
                + Value(settlement, "customName") + "|"
                + Value(settlement, "siteClusterKey"));
        string value = string.Join("\n", parts);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static string Default(string value, string fallback) =>
        string.IsNullOrEmpty(value) ? fallback : value;

    private static XDocument Parse(byte[] bytes) => XDocument.Parse(
        Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'),
        LoadOptions.PreserveWhitespace);

    private static List<XElement> Items(XElement owner, string name) =>
        owner.Element(name)?.Elements("li").ToList() ?? new List<XElement>();

    private static string Value(XElement owner, string name) =>
        owner.Element(name)?.Value ?? "";

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

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
