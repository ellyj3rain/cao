using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ColonistAwareness.Tools;

internal static class Program
{
    private const BindingFlags All = BindingFlags.Public
        | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private const string WorldIdentity = "alysaliu|1|Algorab Markab";
    private const string Region = "CA-RG-EB596A12";
    private const string Candidate = "613b1fe44104";

    private static Type cultureType;
    private static FieldInfo cultureId;
    private static MethodInfo newRegistryQuestion;
    private static object[] questionDefinitions;
    private static string[] allQuestions;
    private static object[] addedQuestionDefinitions;
    private static string[] addedQuestions;
    private static int legacyQuestionCount;

    private sealed record QuarantinedQuestion(string CultureId,
        string Collection, string Scope, string QuestionKey,
        string SourceIdentity, string EvidenceSignature, string Summary,
        int FirstRecordedTick, int LastChangedTick);

    private static int Main(string[] args)
    {
        if (args.Length != 5)
        {
            Console.Error.WriteLine("usage: B16FixtureGenerator <active> "
                + "<mirror> <repo> <managed> <assembly>");
            return 1;
        }

        string active = Path.GetFullPath(args[0]);
        string mirror = Path.GetFullPath(args[1]);
        string repo = Path.GetFullPath(args[2]);
        LoadManagedAssemblies(Path.GetFullPath(args[3]));
        Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(args[4]));
        InitializeProductionProjection(assembly);
        byte[] beforeActive = File.ReadAllBytes(active);
        byte[] beforeMirror = File.ReadAllBytes(mirror);
        Require(beforeActive.SequenceEqual(beforeMirror),
            "active and mirror fixtures differ before B16 conversion");

        string evidence = Path.Combine(repo, "Receipts", "B16", "evidence");
        Directory.CreateDirectory(evidence);
        string evidencePath = Path.Combine(evidence,
            "active-registry2-before-b16.xml");
        if (!File.Exists(evidencePath)) File.WriteAllBytes(evidencePath,
            beforeActive);
        byte[] baselineBytes = File.ReadAllBytes(evidencePath);
        XDocument baseline = Parse(baselineBytes);
        XElement[] baselineCultures = CultureNodes(baseline);
        Require(baselineCultures.Length == 8,
            "expected eight authored Culture records in B16 evidence");
        Require(baselineCultures.All(culture =>
                Value(culture, "schemaVersion") == "10"
                && Value(culture, "questionRegistryVersion") == "2"),
            "governed B16 evidence is not schema-10 registry-2 input");
        QuarantinedQuestion[] quarantined = OrphanedSparseRows(baseline);
        Require(quarantined.Length == 1,
            "expected one governed orphaned local Culture row");
        Dictionary<string, string> priorRows = ExistingQuestionRows(baseline);
        foreach (QuarantinedQuestion item in quarantined)
            priorRows.Remove(QuestionIdentity(item));

        XDocument compatibleMigration = new(baseline);
        Quarantine(compatibleMigration, quarantined);
        string compatiblePath = Path.Combine(evidence,
            "active-registry2-compatible-migration.xml");
        File.WriteAllText(compatiblePath, Serialize(compatibleMigration),
            new UTF8Encoding(false));

        // B15's preserved schema-13 source is historical evidence and remains
        // byte-for-byte untouched. Its regional-owner migration receipt needs
        // the same governed fixture correction, so derive a separate input
        // which removes only the proven non-constituent sparse row and retains
        // that row in legacy evidence.
        string b15Evidence = Path.Combine(repo, "Receipts", "B15",
            "evidence", "active-schema13-before-b15.xml");
        Require(File.Exists(b15Evidence),
            "preserved B15 migration fixture is missing");
        XDocument b15CompatibleMigration = Parse(File.ReadAllBytes(
            b15Evidence));
        QuarantinedQuestion[] b15Quarantined = OrphanedSparseRows(
            b15CompatibleMigration);
        Require(b15Quarantined.Length == 1
                && QuestionIdentity(b15Quarantined[0])
                    == QuestionIdentity(quarantined[0]),
            "B15 and B16 evidence do not identify the same governed orphan");
        Quarantine(b15CompatibleMigration, b15Quarantined);
        string b15CompatiblePath = Path.Combine(repo, "Receipts", "B15",
            "evidence", "active-schema13-compatible-migration.xml");
        File.WriteAllText(b15CompatiblePath, Serialize(b15CompatibleMigration),
            new UTF8Encoding(false));

        // The live pair may already contain an earlier B16 projection. Repair
        // that current state in place; the registry-2 evidence proves which
        // rows predate B16 and must survive byte-for-byte.
        XDocument document = Parse(beforeActive);
        XElement plan = RequireCompatiblePlan(document);
        string invariantBefore = InvariantSignature(document);
        XElement[] cultures = CultureNodes(document);
        Require(cultures.Length == 8,
            "expected eight authored Culture records");

        Quarantine(document, quarantined);
        foreach (XElement culture in cultures) Upgrade(culture);
        document.Root!.Element("authoringDataEpoch")!.Value = "14";
        plan.Element("schemaVersion")!.Value = "15";

        Require(InvariantSignature(document) == invariantBefore,
            "Culture registry conversion changed non-Culture authored state");
        VerifyPriorRows(document, priorRows);
        VerifyQuarantine(document, quarantined);
        VerifyCultures(document);

        string output = Serialize(document);
        B12FixturePairCommit.Commit(active, mirror, output);
        byte[] writtenActive = File.ReadAllBytes(active);
        byte[] writtenMirror = File.ReadAllBytes(mirror);
        Require(writtenActive.SequenceEqual(writtenMirror),
            "active and mirror fixtures differ after B16 conversion");

        XDocument readback = Parse(writtenActive);
        Require(Value(readback.Root!, "authoringDataEpoch") == "14"
                && Value(readback.Root!.Element("plan")!, "schemaVersion")
                    == "15",
            "pending owner was not advanced to epoch 14 / schema 15");
        Require(InvariantSignature(readback) == invariantBefore,
            "non-Culture authored state changed during B16 readback");
        VerifyPriorRows(readback, priorRows);
        VerifyQuarantine(readback, quarantined);
        VerifyCultures(readback);

        WriteReceipt(Path.Combine(repo, "Receipts", "B16",
            "B16_AUTHORED_FIXTURE_RECEIPT.md"), active, mirror,
            Hash(baselineBytes), Hash(writtenActive), priorRows.Count,
            CultureNodes(readback).Sum(CoverageRows), quarantined.Length);
        Console.WriteLine("B16 fixture conversion complete");
        Console.WriteLine("8 Culture records; 48 questions per represented "
            + "population scope; one stale scoped row retained as evidence");
        Console.WriteLine("SHA256 " + Hash(writtenActive));
        return 0;
    }

    private static void InitializeProductionProjection(Assembly assembly)
    {
        cultureType = RequiredType(assembly, "ColonistAwareness.CACulture");
        cultureId = RequiredField(cultureType, "id");
        Type registry = RequiredType(assembly,
            "ColonistAwareness.CACultureQuestionRegistry");
        IList definitions = (IList)RequiredProperty(registry, "All")
            .GetValue(null)!;
        legacyQuestionCount = (int)RequiredField(registry,
            "LegacyQuestionCount").GetRawConstantValue()!;
        questionDefinitions = definitions.Cast<object>().ToArray();
        allQuestions = questionDefinitions.Select(definition =>
            (string)RequiredField(definition.GetType(), "Key")
                .GetValue(definition)!).ToArray();
        addedQuestionDefinitions = questionDefinitions.Skip(
                legacyQuestionCount)
            .ToArray();
        addedQuestions = addedQuestionDefinitions.Select(definition =>
            (string)RequiredField(definition.GetType(), "Key")
                .GetValue(definition)!).ToArray();
        Require(addedQuestions.Length == 24
                && addedQuestions.Distinct(StringComparer.Ordinal).Count()
                    == 24,
            "production Culture registry does not expose 24 B16 questions");
        Require(allQuestions.Length == 48
                && allQuestions.Distinct(StringComparer.Ordinal).Count() == 48,
            "production Culture registry does not expose 48 unique questions");
        Type model = RequiredType(assembly,
            "ColonistAwareness.CACultureModel");
        newRegistryQuestion = model.GetMethods(All).Single(method =>
            method.Name == "NewRegistryUpgradeQuestion"
                && method.GetParameters().Length == 3);
    }

    private static void Upgrade(XElement culture)
    {
        XElement schema = culture.Element("schemaVersion")
            ?? throw new InvalidDataException("Culture schema version missing");
        XElement registry = culture.Element("questionRegistryVersion")
            ?? throw new InvalidDataException("Culture registry version missing");
        Require((schema.Value == "10" && registry.Value == "2")
                || (schema.Value == "11" && registry.Value == "3"),
            "expected Culture schema 10/registry 2 or schema 11/registry 3 input");
        XElement inherited = culture.Element("inheritedQuestions")
            ?? throw new InvalidDataException("inherited questions missing");
        XElement local = culture.Element("localQuestions")
            ?? throw new InvalidDataException("local questions missing");
        // Earlier B16 converter output is development projection, not authored
        // state. Its exact question set and provenance make it safe to replace
        // while every other row remains untouched.
        inherited.Elements("li").Where(IsSupersededB16Row).Remove();
        local.Elements("li").Where(IsSupersededB16Row).Remove();
        XElement[] rows = inherited.Elements("li")
            .Concat(local.Elements("li")).ToArray();
        string[] scopes = rows.Select(Scope).Distinct(StringComparer.Ordinal)
            .ToArray();
        Require(scopes.Length > 0, "Culture has no represented population scope");

        foreach (string scope in scopes)
        {
            var present = new HashSet<string>(rows.Where(row =>
                    Scope(row) == scope).Select(row =>
                    Value(row, "questionKey")), StringComparer.Ordinal);
            object productionCulture = Activator.CreateInstance(cultureType)!;
            cultureId.SetValue(productionCulture, Value(culture, "id"));
            foreach (object definition in questionDefinitions)
            {
                string question = (string)RequiredField(
                    definition.GetType(), "Key").GetValue(definition)!;
                if (present.Contains(question)) continue;
                object projected = newRegistryQuestion.Invoke(null,
                    new[] { productionCulture, scope, definition })!;
                inherited.Add(SerializeDistribution(projected));
            }
        }
        schema.Value = "11";
        registry.Value = "3";
    }

    private static XElement SerializeDistribution(object value)
    {
        Type type = value.GetType();
        var row = new XElement("li",
            Element("schemaVersion", IntField(value, "schemaVersion")),
            Element("questionKey", StringField(value, "questionKey")));
        string scope = StringField(value, "populationScope");
        if (scope != "*") row.Add(Element("populationScope", scope));
        row.Add(Element("mean", FloatField(value, "mean")));
        AddWhen(row, value, "hasDescriptiveNormPrior", false);
        AddWhen(row, value, "descriptiveNormPrior", 0f);
        AddWhen(row, value, "prestigeSignal", 0f);
        AddWhen(row, value, "spread", 0.28f);
        AddWhen(row, value, "spreadOverride", false);
        AddWhen(row, value, "salience", 0.55f);
        AddWhen(row, value, "normStrength", 0.50f);
        AddWhen(row, value, "visibility", 0.65f);
        AddWhen(row, value, "sourceConfidence", 0.60f);
        AddWhen(row, value, "toleranceForDivergence", 0.50f);

        var subgroups = new XElement("subgroups");
        foreach (object subgroup in ((IEnumerable)RequiredField(type,
                     "subgroups").GetValue(value)!).Cast<object>())
            subgroups.Add(SerializeSubgroup(subgroup));
        row.Add(subgroups);
        AddString(row, value, "provenance");
        AddString(row, value, "sourceIdentity");
        AddString(row, value, "evidenceSignature");
        AddWhen(row, value, "firstRecordedTick", -1);
        AddWhen(row, value, "lastChangedTick", -1);
        return row;
    }

    private static XElement SerializeSubgroup(object value)
    {
        var row = new XElement("li");
        AddString(row, value, "subgroupKey");
        AddString(row, value, "label");
        AddWhen(row, value, "share", 0);
        AddWhen(row, value, "meanOffset", 0f);
        AddWhen(row, value, "spreadMultiplier", 1f);
        AddWhen(row, value, "inherited", true);
        return row;
    }

    private static void AddString(XElement owner, object value, string name)
    {
        string text = StringField(value, name);
        if (text.Length > 0) owner.Add(Element(name, text));
    }

    private static void AddWhen(XElement owner, object value, string name,
        bool defaultValue)
    {
        bool current = (bool)RequiredField(value.GetType(), name)
            .GetValue(value)!;
        if (current != defaultValue) owner.Add(Element(name, current));
    }

    private static void AddWhen(XElement owner, object value, string name,
        int defaultValue)
    {
        int current = IntField(value, name);
        if (current != defaultValue) owner.Add(Element(name, current));
    }

    private static void AddWhen(XElement owner, object value, string name,
        float defaultValue)
    {
        float current = FloatField(value, name);
        if (current != defaultValue) owner.Add(Element(name, current));
    }

    private static XElement Element(string name, object value) =>
        new(name, value switch
        {
            float number => number.ToString("R", CultureInfo.InvariantCulture),
            bool flag => flag.ToString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        });

    private static void VerifyCultures(XDocument document)
    {
        foreach (XElement culture in CultureNodes(document))
        {
            Require(Value(culture, "schemaVersion") == "11"
                    && Value(culture, "questionRegistryVersion") == "3",
                Value(culture, "id")
                    + " is not Culture schema 11 / registry 3");
            XElement[] rows = culture.Element("inheritedQuestions")!
                .Elements("li").Concat(culture.Element("localQuestions")!
                    .Elements("li")).ToArray();
            foreach (IGrouping<string, XElement> scope in rows.GroupBy(Scope,
                StringComparer.Ordinal))
            {
                var keys = new HashSet<string>(scope.Select(row =>
                    Value(row, "questionKey")), StringComparer.Ordinal);
                Require(keys.Count == allQuestions.Length
                        && allQuestions.All(keys.Contains),
                    Value(culture, "id") + " scope " + scope.Key
                        + " does not contain all 48 registry-3 questions");
            }
        }
    }

    private static int CoverageRows(XElement culture) => culture
        .Element("inheritedQuestions")!.Elements("li").Count()
        + culture.Element("localQuestions")!.Elements("li").Count();

    private static Dictionary<string, string> ExistingQuestionRows(
        XDocument document)
    {
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (XElement culture in CultureNodes(document))
        foreach (string collection in new[] { "inheritedQuestions",
                     "localQuestions" })
        foreach (XElement row in culture.Element(collection)?.Elements("li")
                     ?? Enumerable.Empty<XElement>())
        {
            if (IsSupersededB16Row(row)) continue;
            string identity = Value(culture, "id") + "|" + collection + "|"
                + Scope(row) + "|" + Value(row, "questionKey");
            Require(!rows.ContainsKey(identity),
                "question identity is duplicated before conversion: "
                    + identity);
            rows.Add(identity, row.ToString(SaveOptions.DisableFormatting));
        }
        return rows;
    }

    private static bool IsSupersededB16Row(XElement row)
    {
        string key = Value(row, "questionKey");
        string provenance = Value(row, "provenance");
        return allQuestions.Contains(key, StringComparer.Ordinal)
            && provenance.StartsWith("B16 playable-ontology coverage; neutral "
                + "because no authored or historical evidence establishes ",
                StringComparison.Ordinal);
    }

    private static QuarantinedQuestion[] OrphanedSparseRows(
        XDocument document)
    {
        var result = new List<QuarantinedQuestion>();
        foreach (XElement culture in CultureNodes(document))
        {
            var constituents = new HashSet<string>((culture
                    .Element("constituents")?.Elements("li")
                    ?? Enumerable.Empty<XElement>())
                .Select(value => Value(value, "cultureId"))
                .Where(value => value.Length > 0), StringComparer.Ordinal);
            XElement[] rows = culture.Element("inheritedQuestions")!
                .Elements("li").Concat(culture.Element("localQuestions")!
                    .Elements("li")).ToArray();
            foreach (IGrouping<string, XElement> scope in rows
                         .Where(row => Scope(row) != "*")
                         .GroupBy(Scope, StringComparer.Ordinal))
            {
                int unique = scope.Select(row => Value(row, "questionKey"))
                    .Distinct(StringComparer.Ordinal).Count();
                if (constituents.Contains(scope.Key)
                    || unique >= legacyQuestionCount)
                    continue;
                foreach (XElement row in scope)
                {
                    string collection = row.Parent?.Name.LocalName ?? "";
                    result.Add(new QuarantinedQuestion(
                        Value(culture, "id"), collection, scope.Key,
                        Value(row, "questionKey"),
                        Value(row, "sourceIdentity"),
                        Value(row, "evidenceSignature"),
                        "mean=" + Value(row, "mean") + "; salience="
                            + Value(row, "salience") + "; confidence="
                            + Value(row, "sourceConfidence")
                            + "; provenance=" + Value(row, "provenance"),
                        IntValue(row, "firstRecordedTick", -1),
                        IntValue(row, "lastChangedTick", -1)));
                }
            }
        }
        return result.ToArray();
    }

    private static void Quarantine(XDocument document,
        IEnumerable<QuarantinedQuestion> records)
    {
        foreach (QuarantinedQuestion record in records)
        {
            XElement culture = CultureNodes(document).Single(value =>
                Value(value, "id") == record.CultureId);
            XElement collection = culture.Element(record.Collection)
                ?? throw new InvalidDataException(record.Collection
                    + " is missing from " + record.CultureId);
            collection.Elements("li").Where(row =>
                    Scope(row) == record.Scope
                    && Value(row, "questionKey") == record.QuestionKey)
                .Remove();
            XElement evidence = culture.Element("legacyEvidence")
                ?? throw new InvalidDataException("legacy evidence is missing");
            evidence.Elements("li").Where(row =>
                    Value(row, "sourceLayer")
                        == "B16 governed fixture repair"
                    && Value(row, "evidenceSignature")
                        == record.EvidenceSignature)
                .Remove();
            var item = new XElement("li",
                Element("sourceKey", record.QuestionKey),
                Element("sourceLayer", "B16 governed fixture repair"),
                Element("populationScope", record.Scope),
                Element("disposition", "preserved as evidence; the recorded "
                    + "population scope is not a constituent of this "
                    + "settlement's current population"),
                Element("summary", record.Summary),
                Element("sourceIdentity", record.SourceIdentity),
                Element("evidenceSignature", record.EvidenceSignature));
            if (record.FirstRecordedTick != -1)
                item.Add(Element("firstRecordedTick",
                    record.FirstRecordedTick));
            if (record.LastChangedTick != -1)
                item.Add(Element("lastChangedTick", record.LastChangedTick));
            evidence.Add(item);
        }
    }

    private static string QuestionIdentity(QuarantinedQuestion item) =>
        item.CultureId + "|" + item.Collection + "|" + item.Scope + "|"
        + item.QuestionKey;

    private static void VerifyQuarantine(XDocument document,
        IEnumerable<QuarantinedQuestion> records)
    {
        foreach (QuarantinedQuestion record in records)
        {
            XElement culture = CultureNodes(document).Single(value =>
                Value(value, "id") == record.CultureId);
            Require(!culture.Element(record.Collection)!.Elements("li").Any(
                    row => Scope(row) == record.Scope
                        && Value(row, "questionKey") == record.QuestionKey),
                "orphaned Culture row remains live: "
                    + QuestionIdentity(record));
            Require(culture.Element("legacyEvidence")!.Elements("li").Any(
                    row => Value(row, "sourceLayer")
                            == "B16 governed fixture repair"
                        && Value(row, "sourceKey") == record.QuestionKey
                        && Value(row, "populationScope") == record.Scope
                        && Value(row, "evidenceSignature")
                            == record.EvidenceSignature),
                "orphaned Culture row was not retained as evidence: "
                    + QuestionIdentity(record));
        }
    }

    private static void VerifyPriorRows(XDocument document,
        IReadOnlyDictionary<string, string> priorRows)
    {
        Dictionary<string, string> current = ExistingQuestionRows(document);
        foreach ((string identity, string xml) in priorRows)
            Require(current.TryGetValue(identity, out string readback)
                    && readback == xml,
                "prior authored Culture row changed: " + identity);
    }

    private static string InvariantSignature(XDocument document)
    {
        XDocument clone = new(document);
        clone.Root?.Element("authoringDataEpoch")?.Remove();
        clone.Root?.Element("plan")?.Element("schemaVersion")?.Remove();
        foreach (XElement culture in CultureNodes(clone))
        {
            culture.Element("schemaVersion")?.Remove();
            culture.Element("questionRegistryVersion")?.Remove();
            culture.Element("inheritedQuestions")?.Remove();
            culture.Element("localQuestions")?.Remove();
            culture.Element("legacyEvidence")?.Remove();
        }
        return Hash(Encoding.UTF8.GetBytes(clone.ToString(
            SaveOptions.DisableFormatting)));
    }

    private static XElement[] CultureNodes(XDocument document) => document
        .Descendants().Where(value => value.Element("questionRegistryVersion")
            != null && value.Element("inheritedQuestions") != null
            && value.Element("localQuestions") != null).ToArray();

    private static XElement RequireCompatiblePlan(XDocument document)
    {
        XElement root = document.Root
            ?? throw new InvalidDataException("fixture root missing");
        Require(root.Name.LocalName == "caRegionalPendingPlan"
                && (Value(root, "authoringDataEpoch") == "13"
                    || Value(root, "authoringDataEpoch") == "14")
                && Value(root, "worldIdentity") == WorldIdentity,
            "fixture is not the supported adjacent or current pending plan");
        XElement plan = root.Element("plan")
            ?? throw new InvalidDataException("regional plan missing");
        Require((Value(plan, "schemaVersion") == "14"
                    || Value(plan, "schemaVersion") == "15")
                && Value(plan.Element("playerFounding")!, "schemaVersion")
                    == "4", "fixture is not regional/founding schema 14-or-15/4");
        Require(Value(plan, "regionalId") == Region
                && Value(plan, "candidateId") == Candidate
                && Value(plan, "startTileId") == "389638"
                && Value(plan, "mapSize") == "350",
            "fixture identity or geography changed");
        Require(Items(plan, "factions").Count == 3
                && Items(plan, "settlements").Count == 4
                && Items(plan, "relations").Count == 3,
            "unexpected authored composition counts");
        return plan;
    }

    private static List<XElement> Items(XElement owner, string name) => owner
        .Element(name)?.Elements("li").ToList() ?? new List<XElement>();

    private static string Scope(XElement row) =>
        row.Element("populationScope")?.Value ?? "*";

    private static string Value(XElement owner, string name) =>
        owner?.Element(name)?.Value ?? "";

    private static int IntValue(XElement owner, string name,
        int defaultValue) => int.TryParse(Value(owner, name),
        NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value : defaultValue;

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
            Indent = false,
            OmitXmlDeclaration = document.Declaration == null,
            NewLineHandling = NewLineHandling.None
        };
        using var writer = new StringWriterWithEncoding(settings.Encoding);
        using (XmlWriter xml = XmlWriter.Create(writer, settings))
            document.Save(xml);
        return writer.ToString();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(
        SHA256.HashData(bytes));

    private static void LoadManagedAssemblies(string managed)
    {
        foreach (string file in new[]
        {
            "netstandard.dll", "UnityEngine.CoreModule.dll",
            "UnityEngine.IMGUIModule.dll", "UnityEngine.TextRenderingModule.dll",
            "Assembly-CSharp-firstpass.dll", "Assembly-CSharp.dll"
        }) Assembly.LoadFrom(Path.Combine(managed, file));
    }

    private static Type RequiredType(Assembly assembly, string name) =>
        assembly.GetType(name, true)!;

    private static PropertyInfo RequiredProperty(Type type, string name) =>
        type.GetProperty(name, All)
        ?? throw new MissingMemberException(type.FullName, name);

    private static FieldInfo RequiredField(Type type, string name)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            FieldInfo field = current.GetField(name, All);
            if (field != null) return field;
        }
        throw new MissingMemberException(type.FullName, name);
    }

    private static string StringField(object value, string name) =>
        RequiredField(value.GetType(), name).GetValue(value) as string ?? "";

    private static int IntField(object value, string name) =>
        (int)RequiredField(value.GetType(), name).GetValue(value)!;

    private static float FloatField(object value, string name) =>
        (float)RequiredField(value.GetType(), name).GetValue(value)!;

    private static void WriteReceipt(string path, string active,
        string mirror, string beforeHash, string afterHash, int preserved,
        int coverageRows, int quarantined)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "# B16 Authored Fixture Receipt\n\n"
            + "| Check | Result |\n|---|---|\n"
            + "| Active and mirror | Byte-identical after atomic pair write |\n"
            + "| Fixture identity | Alysa Liu / Algorab Markab / " + Region
                + " / " + Candidate + " / tile 389638 / map 350 preserved |\n"
            + "| Composition | 3 factions / 4 settlements / 3 relations preserved |\n"
            + "| Pending owner | Authoring epoch 14 / regional plan schema 15 |\n"
            + "| Existing Culture rows | " + preserved
                + " serialized rows preserved exactly |\n"
            + "| Superseded scoped rows | " + quarantined
                + " row preserved in legacy evidence because its population "
                + "scope is absent from the settlement composition |\n"
            + "| Current Culture coverage | " + coverageRows
                + " combined inherited/local rows; all 48 questions resolve per population scope |\n"
            + "| Added state | Neutral, low-confidence coverage only; no missing "
                + "historical position invented |\n"
            + "| Before SHA-256 | `" + beforeHash + "` |\n"
            + "| After SHA-256 | `" + afterHash + "` |\n"
            + "| Active | `" + active.Replace("\\", "/") + "` |\n"
            + "| Mirror | `" + mirror.Replace("\\", "/") + "` |\n",
            new UTF8Encoding(false));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    private sealed class StringWriterWithEncoding : StringWriter
    {
        private readonly Encoding encoding;
        internal StringWriterWithEncoding(Encoding encoding) =>
            this.encoding = encoding;
        public override Encoding Encoding => encoding;
    }
}
