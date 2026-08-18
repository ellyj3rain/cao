using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

internal static class Program
{
    private const BindingFlags All = BindingFlags.Public
        | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private sealed record Row(string SocietyKey, string SocietyName,
        string Group, string Region, string Period, string CultureKey,
        string CultureName, string PoliticalBase, string StateHash);

    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("usage: B14SocietyPresetReceipts "
                + "<repo> <rimworld-managed-directory>");
            return 1;
        }

        string repo = Path.GetFullPath(args[0]);
        string managed = Path.GetFullPath(args[1]);
        foreach (string file in new[]
        {
            "netstandard.dll", "UnityEngine.CoreModule.dll",
            "UnityEngine.IMGUIModule.dll", "UnityEngine.TextRenderingModule.dll",
            "Assembly-CSharp-firstpass.dll", "Assembly-CSharp.dll"
        })
            Assembly.LoadFrom(Path.Combine(managed, file));

        string dll = Path.Combine(repo, "Assemblies", "ColonistAwareness.dll");
        Assembly assembly = Assembly.LoadFrom(dll);
        Type societyLibrary = RequiredType(assembly,
            "ColonistAwareness.CASocietyPresetLibrary");
        Type cultureLibrary = RequiredType(assembly,
            "ColonistAwareness.CACulturePresetLibrary");
        Type politicalModel = RequiredType(assembly,
            "ColonistAwareness.CAPoliticalOrderModel");
        Type cultureType = RequiredType(assembly, "ColonistAwareness.CACulture");
        Type politicalType = RequiredType(assembly,
            "ColonistAwareness.CAPoliticalBeliefs");
        Type userProfileType = RequiredType(assembly,
            "ColonistAwareness.CAUserSocietyProfile");
        Type userAdapterType = RequiredType(assembly,
            "ColonistAwareness.CAUserSocietyPresetAdapter");

        MethodInfo validate = RequiredMethod(societyLibrary,
            "ValidationFailure");
        MethodInfo tryApply = RequiredMethod(societyLibrary, "TryApply");
        MethodInfo match = RequiredMethod(societyLibrary, "Match");
        object validation = validate.Invoke(null, Array.Empty<object>());
        Require(validation == null, "society catalog: " + validation);

        IList presets = ((IEnumerable)RequiredProperty(societyLibrary, "All")
            .GetValue(null)).Cast<object>().ToList();
        IList culturePresets = ((IEnumerable)RequiredProperty(cultureLibrary,
            "All").GetValue(null)).Cast<object>().ToList();
        var cultureKeys = new HashSet<string>(culturePresets.Cast<object>()
            .Select(item => StringField(item, "Key")), StringComparer.Ordinal);
        Require(presets.Count > 0, "built-in society catalog is empty");
        Require(presets.Cast<object>().All(item => !cultureKeys.Contains(
                StringProperty(item, "Key"))),
            "a Society preset reuses a Culture preset key");

        var rows = new List<Row>();
        for (int index = 0; index < presets.Count; index++)
        {
            object preset = presets[index];
            object culture = Activator.CreateInstance(cultureType)!;
            object political = Activator.CreateInstance(politicalType)!;
            Apply(tryApply, preset, culture, political, "receipt:" + index);

            string key = StringProperty(preset, "Key");
            string cultureName = StringField(culture, "name");
            Require(!string.IsNullOrWhiteSpace(cultureName),
                key + " did not create a Culture name");
            Require(ListCount(culture, "inheritedQuestions") == 24,
                key + " did not set 24 Culture values");
            Require(ListCount(political, "questions") == 26,
                key + " did not set 26 Political Order questions");
            object ownedPolitical = RequiredField(preset.GetType(),
                "PoliticalOrderValues").GetValue(preset)!;
            Require(ListCount(ownedPolitical, "questions") == 26,
                key + " does not own a complete Political Order snapshot");
            Require(StringProperty(match.Invoke(null,
                    new[] { culture, political }), "Key") == key,
                key + " did not match after application");

            object cultureCopy = RequiredMethod(cultureType, "Copy")
                .Invoke(culture, Array.Empty<object>())!;
            object politicalCopy = RequiredMethod(politicalType, "Copy")
                .Invoke(political, Array.Empty<object>())!;
            Require(StringProperty(match.Invoke(null,
                    new[] { cultureCopy, politicalCopy }), "Key") == key,
                key + " did not match after deep copy");
            rows.Add(new Row(key, StringProperty(preset, "Label"),
                StringProperty(preset, "CatalogGroup"),
                StringProperty(preset, "ReferenceRegion"),
                StringProperty(preset, "ApproximatePeriod"),
                StringField(preset, "CulturePresetKey"), cultureName,
                StringField(preset, "PoliticalOrderPresetKey"),
                PairHash(culture, political)));
        }

        // The type itself must permit multiple independently named Society
        // presets to reference one Culture. This is the executable proof that
        // the old one-to-one catalog contract is gone.
        object alternate = NewBuiltIn(presets[0],
            "receipt-independent-society", "Independent society identity",
            StringField(presets[0], "CulturePresetKey"));
        Require(RequiredMethod(alternate.GetType(), "ValidationFailure")
                .Invoke(alternate, Array.Empty<object>()) == null,
            "a second Society identity could not reuse a Culture component");
        object alternateCulture = Activator.CreateInstance(cultureType)!;
        object alternatePolitical = Activator.CreateInstance(politicalType)!;
        Apply(tryApply, alternate, alternateCulture, alternatePolitical,
            "receipt:independent");

        // Matching must cover every causal inherited-Culture field. Changing
        // a less prominent field must end the derived preset match.
        object matchCulture = Activator.CreateInstance(cultureType)!;
        object matchPolitical = Activator.CreateInstance(politicalType)!;
        Apply(tryApply, presets[0], matchCulture, matchPolitical,
            "receipt:match-negative");
        IList inheritedQuestions = (IList)RequiredField(cultureType,
            "inheritedQuestions").GetValue(matchCulture)!;
        object firstQuestion = inheritedQuestions[0]!;
        FieldInfo confidence = RequiredField(firstQuestion.GetType(),
            "sourceConfidence");
        confidence.SetValue(firstQuestion,
            (float)confidence.GetValue(firstQuestion)! - 0.01f);
        object afterCultureMutation = match.Invoke(null,
            new[] { matchCulture, matchPolitical });
        Require(afterCultureMutation == null
                || StringProperty(afterCultureMutation, "Key")
                    != StringProperty(presets[0], "Key"),
            "a changed causal Culture field retained a false Society match");

        object retainedCulture = Activator.CreateInstance(cultureType)!;
        object retainedPolitical = Activator.CreateInstance(politicalType)!;
        Apply(tryApply, presets[0], retainedCulture, retainedPolitical,
            "receipt:retained");
        string beforeReject = PairHash(retainedCulture, retainedPolitical);
        object invalid = NewBuiltIn(presets[0], "receipt-invalid",
            "Invalid society", "missing-culture-preset");
        object[] rejected = { invalid, retainedCulture, retainedPolitical,
            "receipt:invalid", null };
        bool accepted = (bool)tryApply.Invoke(null, rejected)!;
        Require(!accepted && !string.IsNullOrWhiteSpace(rejected[4] as string),
            "invalid preset was not visibly rejected");
        Require(PairHash(retainedCulture, retainedPolitical) == beforeReject,
            "rejected preset changed one or both target models");

        Apply(tryApply, presets[1], retainedCulture, retainedPolitical,
            "receipt:replacement");
        Require(StringProperty(match.Invoke(null,
                new[] { retainedCulture, retainedPolitical }), "Key")
            == StringProperty(presets[1], "Key"),
            "a second preset did not replace both prior components");

        // Culture-only and Political-Order-only substitutions must leave the
        // sibling component byte-for-byte equivalent at the data level.
        Apply(tryApply, presets[0], retainedCulture, retainedPolitical,
            "receipt:isolation");
        string politicalBeforeCulture = ObjectHash(retainedPolitical);
        string cultureBeforeCulture = ObjectHash(retainedCulture);
        MethodInfo applyCulture = RequiredMethod(cultureLibrary, "Apply");
        applyCulture.Invoke(null, new[] { retainedCulture, culturePresets[1],
            "receipt:culture-only" });
        Require(ObjectHash(retainedPolitical) == politicalBeforeCulture,
            "Culture substitution changed Political Order");
        Require(ObjectHash(retainedCulture) != cultureBeforeCulture,
            "Culture substitution did not change Culture");

        Apply(tryApply, presets[0], retainedCulture, retainedPolitical,
            "receipt:isolation-reset");
        string cultureBeforePolitics = ObjectHash(retainedCulture);
        string politicalBeforePolitics = ObjectHash(retainedPolitical);
        IList politicalPresets = ((IEnumerable)RequiredField(politicalModel,
            "Presets").GetValue(null)!).Cast<object>().ToList();
        string replacementPoliticalKey = politicalPresets.Cast<object>()
            .Select(item => StringField(item, "Key"))
            .First(key => key != StringField(presets[0],
                "PoliticalOrderPresetKey"));
        Type axisSource = RequiredType(assembly, "ColonistAwareness.CAAxisSource");
        RequiredMethod(politicalModel, "ApplyPreset").Invoke(null,
            new[] { retainedPolitical, replacementPoliticalKey,
                Enum.Parse(axisSource, "Authored") });
        Require(ObjectHash(retainedCulture) == cultureBeforePolitics,
            "Political Order substitution changed Culture");
        Require(ObjectHash(retainedPolitical) != politicalBeforePolitics,
            "Political Order substitution did not change Political Order");

        // A saved Society is a deep two-component snapshot. Applying an
        // independently copied representation reaches the same atomic path.
        Apply(tryApply, presets[2], retainedCulture, retainedPolitical,
            "receipt:saved-source");
        object userProfile = Activator.CreateInstance(userProfileType)!;
        RequiredField(userProfileType, "key").SetValue(userProfile,
            "user-society:receipt");
        RequiredField(userProfileType, "displayName").SetValue(userProfile,
            "Receipt society");
        object cultureTemplate = RequiredMethod(cultureType,
            "CopyAsInheritedTemplate").Invoke(retainedCulture,
            Array.Empty<object>())!;
        object politicalTemplate = RequiredMethod(politicalType, "Copy")
            .Invoke(retainedPolitical, Array.Empty<object>())!;
        RequiredField(politicalType, "id").SetValue(politicalTemplate, null);
        RequiredField(userProfileType, "cultureValues").SetValue(userProfile,
            cultureTemplate);
        RequiredField(userProfileType, "politicalOrderValues").SetValue(
            userProfile, politicalTemplate);
        object copiedProfile = RequiredMethod(userProfileType, "CopyAs")
            .Invoke(userProfile, new object[] { "user-society:readback",
                "Receipt society readback" })!;
        Assembly gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .Single(item => item.GetName().Name == "Assembly-CSharp");
        object readProfile = ScribeRoundTrip(gameAssembly, userProfileType,
            copiedProfile);
        object adapter = Activator.CreateInstance(userAdapterType, All, null,
            new[] { readProfile }, CultureInfo.InvariantCulture)!;
        object savedCulture = Activator.CreateInstance(cultureType)!;
        object savedPolitical = Activator.CreateInstance(politicalType)!;
        Apply(tryApply, adapter, savedCulture, savedPolitical,
            "receipt:saved-readback");
        Require((bool)RequiredMethod(userAdapterType, "Matches").Invoke(adapter,
                new[] { savedCulture, savedPolitical })!,
            "saved Society snapshot did not match after Scribe readback");

        Require(!cultureType.GetFields(All).Any(field => field.Name
                .Contains("societyPreset", StringComparison.OrdinalIgnoreCase))
            && !politicalType.GetFields(All).Any(field => field.Name
                .Contains("societyPreset", StringComparison.OrdinalIgnoreCase)),
            "canonical world state retains Society preset ownership");

        string receipt = Path.Combine(repo, "Receipts", "B14",
            "B14_SOCIETY_PRESET_EXECUTION_RECEIPT.md");
        Directory.CreateDirectory(Path.GetDirectoryName(receipt)!);
        File.WriteAllText(receipt, Render(rows, rejected[4] as string, dll),
            new UTF8Encoding(false));
        Console.WriteLine("PASS " + rows.Count + "/" + presets.Count
            + " built-in Society presets applied and matched after deep copy");
        Console.WriteLine("PASS every built-in Society owns a complete Political Order snapshot");
        Console.WriteLine("PASS causal Culture mutation ends the derived Society match");
        Console.WriteLine("PASS independent Society identity can reuse a Culture reference");
        Console.WriteLine("PASS invalid application rolled back both component states");
        Console.WriteLine("PASS Culture-only and Political-Order-only substitutions are isolated");
        Console.WriteLine("PASS saved Society snapshot applied after Scribe serialization and readback");
        Console.WriteLine(receipt);
        return 0;
    }

    private static object NewBuiltIn(object source, string key, string label,
        string cultureKey)
    {
        Type type = source.GetType();
        ConstructorInfo constructor = type.GetConstructors(All).Single();
        return constructor.Invoke(new object[]
        {
            key, label, "Social forms", null, null,
            "Receipt-only Society composition.", cultureKey,
            StringField(source, "PoliticalOrderPresetKey"),
            RequiredField(type, "PoliticalOverrides").GetValue(source)
        });
    }

    private static object ScribeRoundTrip(Assembly gameAssembly,
        Type valueType, object value)
    {
        Type scribe = gameAssembly.GetType("Verse.Scribe", true)!;
        Type log = gameAssembly.GetType("Verse.Log", true)!;
        FieldInfo logDisablers = RequiredField(log, "logDisablers");
        int priorLogDisablers = (int)logDisablers.GetValue(null)!;
        logDisablers.SetValue(null, priorLogDisablers + 1);
        object saver = RequiredField(scribe, "saver").GetValue(null)!;
        object loader = RequiredField(scribe, "loader").GetValue(null)!;
        MethodInfo expose = RequiredMethod(valueType, "ExposeData");
        string path = Path.Combine(Path.GetTempPath(), "cao-b14-society-"
            + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            RequiredMethod(saver.GetType(), "InitSaving").Invoke(saver,
                new object[] { path, "caSocietyProfileReceipt" });
            try
            {
                expose.Invoke(value, Array.Empty<object>());
            }
            finally
            {
                RequiredMethod(saver.GetType(), "FinalizeSaving").Invoke(
                    saver, Array.Empty<object>());
            }

            RequiredMethod(loader.GetType(), "InitLoading").Invoke(loader,
                new object[] { path });
            object restored = Activator.CreateInstance(valueType)!;
            try
            {
                expose.Invoke(restored, Array.Empty<object>());
            }
            finally
            {
                // These settings snapshots contain no cross references or
                // post-load initializers. ForceStop closes the isolated
                // LoadingVars pass without requiring a live Unity Prefs root.
                RequiredMethod(loader.GetType(), "ForceStop").Invoke(
                    loader, Array.Empty<object>());
            }
            return restored;
        }
        finally
        {
            logDisablers.SetValue(null, priorLogDisablers);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void Apply(MethodInfo method, object preset, object culture,
        object political, string source)
    {
        object[] values = { preset, culture, political, source, null };
        bool applied = (bool)method.Invoke(null, values)!;
        Require(applied, StringProperty(preset, "Key") + ": " + values[4]);
    }

    private static string PairHash(object culture, object political) =>
        ObjectHash(culture) + ":" + ObjectHash(political);

    private static string ObjectHash(object value)
    {
        string canonical = Canonical(value, 0);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            canonical))).Substring(0, 16);
    }

    private static string Canonical(object value, int depth)
    {
        if (value == null) return "null";
        if (depth > 10) return "<depth>";
        Type type = value.GetType();
        if (type.IsEnum || type.IsPrimitive || value is decimal)
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        if (value is string text) return "\"" + text + "\"";
        if (value is IEnumerable sequence)
            return "[" + string.Join(",", sequence.Cast<object>()
                .Select(item => Canonical(item, depth + 1))) + "]";
        return "{" + string.Join(",", type.GetFields(All)
            .Where(field => !field.IsStatic)
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .Select(field => field.Name + "=" + Canonical(
                field.GetValue(value), depth + 1))) + "}";
    }

    private static string Render(IReadOnlyList<Row> rows, string rejected,
        string dll)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        var output = new StringBuilder()
            .AppendLine("# B14 Society Preset Execution Receipt")
            .AppendLine()
            .AppendLine("Generated: " + now.ToUniversalTime().ToString(
                "yyyy-MM-dd HH:mm:ss 'UTC'") + " / "
                + now.ToString("yyyy-MM-dd HH:mm:ss zzz"))
            .AppendLine()
            .AppendLine("Assembly: `" + dll + "`")
            .AppendLine()
            .AppendLine("This executable reflection receipt loads the built assembly "
                + "against RimWorld's managed references. It does not claim operator "
                + "visual or gameplay acceptance.")
            .AppendLine()
            .AppendLine("| Check | Result |")
            .AppendLine("|---|---|")
            .AppendLine("| Catalog validation | PASS |")
            .AppendLine("| Independent catalog identity | PASS - Society keys are distinct from Culture keys; a second Society identity can reuse one Culture reference |")
            .AppendLine("| Complete owned composition | PASS - " + rows.Count
                + " presets each own and apply 24 Culture values and a frozen 26-question Political Order snapshot |")
            .AppendLine("| Deep-copy match | PASS - every independently copied pair matches its applied preset |")
            .AppendLine("| Mutation-negative match | PASS - changing a causal inherited-Culture field ends the derived Society match |")
            .AppendLine("| Saved Society snapshot | PASS - the schema-1 profile survives Scribe serialization/readback and its copied Culture and Political Order apply together through the same atomic path |")
            .AppendLine("| Component isolation | PASS - Culture-only and Political-Order-only substitutions leave the sibling component unchanged |")
            .AppendLine("| Atomic rejection | PASS - `" + rejected
                + "`; before/after state fingerprints agree |")
            .AppendLine("| Replacement | PASS - applying a second Society preset replaced both components |")
            .AppendLine("| Runtime ownership | PASS - canonical Culture and Political Order carry no Society-preset ownership field |")
            .AppendLine()
            .AppendLine("## Built-in catalog crosswalk")
            .AppendLine()
            .AppendLine("| Society key | Society name | Type | Region | Period | Culture reference | Applied Culture name | Political base | State hash |")
            .AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (Row row in rows)
            output.AppendLine("| `" + row.SocietyKey + "` | " + row.SocietyName
                + " | " + row.Group + " | " + (row.Region ?? "") + " | "
                + (row.Period ?? "") + " | `" + row.CultureKey + "` | "
                + row.CultureName + " | `" + row.PoliticalBase + "` | `"
                + row.StateHash + "` |");
        return output.ToString();
    }

    private static Type RequiredType(Assembly assembly, string name) =>
        assembly.GetType(name, true)!;
    private static MethodInfo RequiredMethod(Type type, string name) =>
        type.GetMethods(All).Single(method => method.Name == name);
    private static PropertyInfo RequiredProperty(Type type, string name) =>
        type.GetProperty(name, All)
        ?? throw new MissingMemberException(type.FullName, name);
    private static FieldInfo RequiredField(Type type, string name) =>
        type.GetField(name, All)
        ?? throw new MissingMemberException(type.FullName, name);
    private static string StringProperty(object value, string name) =>
        value == null ? "" : RequiredProperty(value.GetType(), name)
            .GetValue(value) as string ?? "";
    private static string StringField(object value, string name) =>
        RequiredField(value.GetType(), name).GetValue(value) as string ?? "";
    private static int ListCount(object value, string name) =>
        ((ICollection)RequiredField(value.GetType(), name).GetValue(value)!).Count;
    private static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }
}
