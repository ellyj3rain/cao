using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class Program
{
    private const BindingFlags All = BindingFlags.Public
        | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private sealed record ResearchRow(string DefName, string Domains);
    private sealed record NativeTechRead(string File, int Line,
        string Text, string Classification);

    private static int Main(string[] args)
    {
        bool verifyOnly = args.Length == 7 && args[6] == "--verify-only";
        if (args.Length != 6 && !verifyOnly)
        {
            Console.Error.WriteLine("usage: B15TechnologicalKnowledgeReceipts "
                + "<repo> <rimworld-managed-directory> <rimworld-data-directory> "
                + "<colonist-awareness-assembly> <current-regional-fixture> "
                + "<rimworld-decompiled-directory>");
            return 1;
        }

        string repo = Path.GetFullPath(args[0]);
        string managed = Path.GetFullPath(args[1]);
        string data = Path.GetFullPath(args[2]);
        string dll = Path.GetFullPath(args[3]);
        string fixture = Path.GetFullPath(args[4]);
        string decompiled = Path.GetFullPath(args[5]);
        LoadManagedAssemblies(managed);
        Assembly assembly = Assembly.LoadFrom(dll);

        Type societyLibrary = RequiredType(assembly,
            "ColonistAwareness.CASocietyPresetLibrary");
        Type cultureType = RequiredType(assembly, "ColonistAwareness.CACulture");
        Type politicalType = RequiredType(assembly,
            "ColonistAwareness.CAPoliticalBeliefs");
        Type knowledgeType = RequiredType(assembly,
            "ColonistAwareness.CATechnologicalKnowledge");
        Type technologyModel = RequiredType(assembly,
            "ColonistAwareness.CATechnologicalKnowledgeModel");
        Type availability = RequiredType(assembly,
            "ColonistAwareness.CATechnologicalKnowledgeAvailability");
        Type requirementResolver = RequiredType(assembly,
            "ColonistAwareness.CATechnologyRequirementResolver");
        Type custodyType = RequiredType(assembly,
            "ColonistAwareness.CATechnologyCustodyRecord");
        Type custodyKind = RequiredType(assembly,
            "ColonistAwareness.CATechnologyCustodyKind");
        Type axisSource = RequiredType(assembly, "ColonistAwareness.CAAxisSource");

        IList presets = ((IEnumerable)RequiredProperty(societyLibrary, "All")
            .GetValue(null)!).Cast<object>().ToList();
        Require(presets.Count > 0, "built-in Society catalog is empty");
        object validation = Method(societyLibrary, "ValidationFailure", 0)
            .Invoke(null, Array.Empty<object>());
        Require(validation == null, "Society catalog: " + validation);

        MethodInfo tryApply = Method(societyLibrary, "TryApply", 6);
        MethodInfo match = Method(societyLibrary, "Match", 3);
        var societyHashes = new List<string>();
        for (int index = 0; index < presets.Count; index++)
        {
            object culture = Activator.CreateInstance(cultureType)!;
            object political = Activator.CreateInstance(politicalType)!;
            object knowledge = Activator.CreateInstance(knowledgeType)!;
            ApplySociety(tryApply, presets[index], culture, political,
                knowledge, "b15-receipt:" + index);
            Require(ListCount(culture, "inheritedQuestions") == 48,
                "Society preset did not apply 48 Culture values");
            Require(ListCount(political, "questions") == 26,
                "Society preset did not apply 26 Political Order values");
            Require(ListCount(knowledge, "domains") == 9,
                "Society preset did not apply nine knowledge domains");
            Require(RequiredField(knowledgeType, "custody").GetValue(knowledge)
                    is ICollection custody && custody.Count == 0,
                "Society preset copied distributed custody");
            object matched = match.Invoke(null,
                new[] { culture, political, knowledge });
            Require(matched != null && StringProperty(matched, "Key")
                    == StringProperty(presets[index], "Key"),
                "three-component Society match failed");
            societyHashes.Add(TripleHash(culture, political, knowledge));
        }

        TestAtomicRejection(presets[0], tryApply, cultureType, politicalType,
            knowledgeType);
        TestSavedSocietyRoundTrip(assembly, presets[1], tryApply, cultureType,
            politicalType, knowledgeType);
        TestRuntimeBoundarySource(repo);

        object distributed = NewProfiledKnowledge(knowledgeType,
            technologyModel, axisSource, "industrial", "distributed-receipt");
        MethodInfo effectiveRank = Method(availability, "EffectiveRank", 5);
        string construction = "construction";
        string construct = "construct";
        int standard = Effective(effectiveRank, distributed, construction,
            construct, false, _ => false);
        Require(standard == 3,
            "standard mode did not read canonical faction knowledge");
        int noCarrier = Effective(effectiveRank, distributed, construction,
            construct, true, _ => false);
        Require(noCarrier == 0,
            "distributed mode invented availability without custody");

        IList custodyRecords = (IList)RequiredField(knowledgeType, "custody")
            .GetValue(distributed)!;
        custodyRecords.Add(NewCustody(custodyType, custodyKind,
            construction, construct, 3, "Pawn", 101, "pawn:101"));
        custodyRecords.Add(NewCustody(custodyType, custodyKind,
            construction, construct, 3, "Pawn", 202, "pawn:202"));
        RequiredField(knowledgeType, "distributionInitialized")
            .SetValue(distributed, true);
        var availablePawns = new HashSet<int> { 101, 202 };
        Func<int, bool> pawnAvailable = id => availablePawns.Contains(id);
        Require(Effective(effectiveRank, distributed, construction, construct,
                true, pawnAvailable) == 3,
            "two carriers did not expose faction knowledge");
        availablePawns.Remove(101);
        Require(Effective(effectiveRank, distributed, construction, construct,
                true, pawnAvailable) == 3,
            "redundant carrier loss removed available knowledge");
        availablePawns.Remove(202);
        Require(Effective(effectiveRank, distributed, construction, construct,
                true, pawnAvailable) == 0,
            "isolated carrier loss did not remove availability");
        Require(Effective(effectiveRank, distributed, construction, construct,
                true, pawnAvailable) == 0,
            "lost carriers were regenerated by a later query");
        availablePawns.Add(202);
        Require(Effective(effectiveRank, distributed, construction, construct,
                true, pawnAvailable) == 3,
            "carrier recovery did not restore availability");
        Require(Effective(effectiveRank, distributed, construction, construct,
                true, id => id == 101) == 3
                && Effective(effectiveRank, distributed, construction,
                    construct, true, id => id == 303) == 0,
            "distributed availability did not respect the supplied local "
                + "carrier boundary");

        object retained = NewProfiledKnowledge(knowledgeType, technologyModel,
            axisSource, "industrial", "retention-receipt");
        IList retainedCustody = (IList)RequiredField(knowledgeType, "custody")
            .GetValue(retained)!;
        retainedCustody.Add(NewCustody(custodyType, custodyKind,
            construction, construct, 3, "Institution", -1,
            "institution:builders-guild", 17, 701));
        retainedCustody.Add(NewCustody(custodyType, custodyKind,
            "medicine", "operate", 2, "Record", -1,
            "record:medical-manual", 17, 701));
        Require(Effective(effectiveRank, retained, construction, construct,
                true, _ => false) == 3,
            "institutional custody did not retain knowledge");
        Require(Effective(effectiveRank, retained, "medicine", "operate",
                true, _ => false) == 2,
            "recorded custody did not retain knowledge");
        MethodInfo scopedEffectiveRank = Method(availability,
            "EffectiveRank", 6);
        Delegate localStore = CustodyPredicate(custodyType, 17);
        Delegate remoteStore = CustodyPredicate(custodyType, 99);
        Require(ScopedEffective(scopedEffectiveRank, retained, construction,
                construct, true, _ => false, localStore) == 3,
            "local institutional custody was unavailable at its map");
        Require(ScopedEffective(scopedEffectiveRank, retained, construction,
                construct, true, _ => false, remoteStore) == 0,
            "institutional custody leaked to another map");
        string retainedHash = ObjectHash(retained);
        Assembly gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .Single(item => item.GetName().Name == "Assembly-CSharp");
        object retainedReadback = ScribeRoundTrip(gameAssembly, knowledgeType,
            retained, "caTechnologicalKnowledgeReceipt");
        Require(ObjectHash(retainedReadback) == retainedHash,
            "knowledge distribution changed during Scribe readback");

        MethodInfo forResearch = Method(requirementResolver, "ForResearch", 1);
        List<ResearchRow> researchRows = AuditResearchDefinitions(data,
            forResearch, assembly);
        TestExactResearchMappings(forResearch);
        TestUnknownResearchFailsClosed(forResearch, assembly);
        TestConsumerTranslations(requirementResolver, availability, assembly,
            retained, technologyModel, axisSource);
        TestSettlementPotentialBoundary(assembly);
        Console.WriteLine("PASS settlement placement tests authored knowledge "
            + "potential while confirmation still requires realized functions");
        AuditCampaignSchema(assembly);
        TestSupportedB14Migrations(gameAssembly, assembly, repo);
        TestCurrentRegionalFixtureLoad(gameAssembly, assembly, fixture);
        Console.WriteLine("PASS current regional fixture Scribe-loads without "
            + "dropping its 3 factions, 4 settlements, or knowledge state");
        AuditOwnershipAndConsumerSource(repo);
        IReadOnlyList<NativeTechRead> nativeReads =
            AuditNativeTechAuthority(repo, decompiled);

        string receiptDirectory = Path.Combine(repo, "Receipts", "B15");
        Directory.CreateDirectory(receiptDirectory);
        string assemblyHash = Convert.ToHexString(SHA256.HashData(
            File.ReadAllBytes(dll)));
        if (!verifyOnly)
        {
            WriteExecutionReceipt(Path.Combine(receiptDirectory,
                "B15_TECHNOLOGICAL_KNOWLEDGE_EXECUTION_RECEIPT.md"), dll,
                assemblyHash, presets.Count, societyHashes, standard);
            WriteMappingReceipt(Path.Combine(receiptDirectory,
                "B15_TECHNOLOGICAL_KNOWLEDGE_MAPPING_RECEIPT.md"), dll,
                assemblyHash, researchRows);
            WriteStaticReceipt(Path.Combine(receiptDirectory,
                "B15_TECHNOLOGICAL_KNOWLEDGE_STATIC_RECEIPT.md"), repo, dll,
                assemblyHash);
            WriteConsumerMatrixReceipt(Path.Combine(receiptDirectory,
                "B15_TECHNOLOGICAL_KNOWLEDGE_CONSUMER_MATRIX.md"), dll,
                assemblyHash);
            WriteNativeAuthorityReceipt(Path.Combine(receiptDirectory,
                "B15_NATIVE_TECH_AUTHORITY_CENSUS.md"), dll, assemblyHash,
                nativeReads);
        }

        Console.WriteLine("PASS faction-owned three-component Society apply: "
            + presets.Count + "/" + presets.Count);
        Console.WriteLine("PASS failed third-component validation rolls back all three");
        Console.WriteLine("PASS saved Society snapshot survives Scribe readback");
        Console.WriteLine("PASS exact research evidence participates in Society matching");
        Console.WriteLine("PASS standard and distributed availability semantics");
        Console.WriteLine("PASS standard mode and unsupported factions preserve native boundaries");
        Console.WriteLine("PASS redundancy, isolated loss, recovery, institution, and record custody");
        Console.WriteLine("PASS " + researchRows.Count
            + " installed native/DLC research definitions explicitly mapped");
        Console.WriteLine("PASS synthetic unknown research fails closed");
        Console.WriteLine("PASS ownership, persistence, consumer, and schema convergence");
        return 0;
    }

    private static void TestAtomicRejection(object source,
        MethodInfo tryApply, Type cultureType, Type politicalType,
        Type knowledgeType)
    {
        object culture = Activator.CreateInstance(cultureType)!;
        object political = Activator.CreateInstance(politicalType)!;
        object knowledge = Activator.CreateInstance(knowledgeType)!;
        ApplySociety(tryApply, source, culture, political, knowledge,
            "b15-receipt:rollback-baseline");
        string before = TripleHash(culture, political, knowledge);
        Type presetType = source.GetType();
        ConstructorInfo constructor = presetType.GetConstructors(All).Single();
        object invalid = constructor.Invoke(new object[]
        {
            "b15-invalid-technology", "Invalid technology receipt",
            "Social forms", null, null, "Receipt-only invalid preset.",
            StringField(source, "CulturePresetKey"),
            StringField(source, "PoliticalOrderPresetKey"),
            RequiredField(presetType, "PoliticalOverrides").GetValue(source)
        });
        object technology = RequiredField(presetType,
            "TechnologicalKnowledgeValues").GetValue(invalid)!;
        IList domains = (IList)RequiredField(knowledgeType, "domains")
            .GetValue(technology)!;
        domains.RemoveAt(domains.Count - 1);
        object[] args = { invalid, culture, political, knowledge,
            "b15-receipt:rollback", null };
        bool accepted = (bool)tryApply.Invoke(null, args)!;
        Require(!accepted && !string.IsNullOrWhiteSpace(args[5] as string),
            "invalid third component was accepted");
        Require(TripleHash(culture, political, knowledge) == before,
            "failed third-component validation changed canonical state");
    }

    private static void TestSavedSocietyRoundTrip(Assembly assembly,
        object source, MethodInfo tryApply, Type cultureType,
        Type politicalType, Type knowledgeType)
    {
        object culture = Activator.CreateInstance(cultureType)!;
        object political = Activator.CreateInstance(politicalType)!;
        object knowledge = Activator.CreateInstance(knowledgeType)!;
        ApplySociety(tryApply, source, culture, political, knowledge,
            "b15-receipt:saved-source");
        IList sourceProjects = (IList)RequiredField(knowledgeType,
            "knownResearchProjects").GetValue(knowledge)!;
        sourceProjects.Add("Electricity");
        sourceProjects.Add("MicroelectronicsBasics");
        Type profileType = RequiredType(assembly,
            "ColonistAwareness.CAUserSocietyProfile");
        object profile = Activator.CreateInstance(profileType)!;
        RequiredField(profileType, "key").SetValue(profile,
            "user-society:b15-receipt");
        RequiredField(profileType, "displayName").SetValue(profile,
            "B15 receipt society");
        object cultureCopy = Method(cultureType, "CopyAsInheritedTemplate", 0)
            .Invoke(culture, Array.Empty<object>())!;
        object politicalCopy = Method(politicalType, "Copy", 0)
            .Invoke(political, Array.Empty<object>())!;
        object knowledgeCopy = Method(knowledgeType, "CopyAsPreset", 0)
            .Invoke(knowledge, Array.Empty<object>())!;
        RequiredField(profileType, "cultureValues").SetValue(profile,
            cultureCopy);
        RequiredField(profileType, "politicalOrderValues").SetValue(profile,
            politicalCopy);
        RequiredField(profileType, "technologicalKnowledgeValues")
            .SetValue(profile, knowledgeCopy);
        Assembly gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .Single(item => item.GetName().Name == "Assembly-CSharp");
        object restored = ScribeRoundTrip(gameAssembly, profileType, profile,
            "caSocietyProfileReceipt");
        Type adapterType = RequiredType(assembly,
            "ColonistAwareness.CAUserSocietyPresetAdapter");
        object adapter = Activator.CreateInstance(adapterType, All, null,
            new[] { restored }, CultureInfo.InvariantCulture)!;
        object targetCulture = Activator.CreateInstance(cultureType)!;
        object targetPolitical = Activator.CreateInstance(politicalType)!;
        object targetKnowledge = Activator.CreateInstance(knowledgeType)!;
        ApplySociety(tryApply, adapter, targetCulture, targetPolitical,
            targetKnowledge, "b15-receipt:saved-readback");
        Require((bool)Method(adapterType, "Matches", 3).Invoke(adapter,
                new[] { targetCulture, targetPolitical, targetKnowledge })!,
            "saved three-component Society did not match after readback");
        IList restoredProjects = (IList)RequiredField(knowledgeType,
            "knownResearchProjects").GetValue(targetKnowledge)!;
        Require(restoredProjects.Cast<string>().OrderBy(item => item,
                StringComparer.Ordinal).SequenceEqual(new[]
                {
                    "Electricity", "MicroelectronicsBasics"
                }, StringComparer.Ordinal),
            "saved Society lost exact research-project evidence");
        restoredProjects.Remove("MicroelectronicsBasics");
        Require(!(bool)Method(adapterType, "Matches", 3).Invoke(adapter,
                new[] { targetCulture, targetPolitical, targetKnowledge })!,
            "Society match ignored divergent research-project evidence");
        Require(ListCount(targetKnowledge, "custody") == 0,
            "saved Society persisted pawn custody");
    }

    private static void TestRuntimeBoundarySource(string repo)
    {
        string source = File.ReadAllText(Path.Combine(repo, "Source",
            "TechnologicalKnowledgeModule.cs"));
        foreach (string patch in new[]
        {
            "CATechnologicalKnowledgeConstructionPatch",
            "CATechnologicalKnowledgeProductionPatch",
            "CATechnologicalKnowledgeAgriculturePatch",
            "CATechnologicalKnowledgeResearchCostPatch",
            "CATechnologicalKnowledgeResearchProgressPatch"
        })
        {
            int start = source.IndexOf("class " + patch,
                StringComparison.Ordinal);
            int end = source.IndexOf("\n    [HarmonyPatch", start + 1,
                StringComparison.Ordinal);
            string block = start < 0 ? string.Empty : source.Substring(start,
                (end < 0 ? source.Length : end) - start);
            Require(block.Contains(
                    "!CAFactionStateGenerator.UsesFactionState(player)",
                    StringComparison.Ordinal),
                patch + " does not preserve unsupported native behavior");
        }
        int transfer = source.IndexOf("internal static void TransferPawn",
            StringComparison.Ordinal);
        int transferEnd = source.IndexOf(
            "\n        internal static void RecordCarrierLoss", transfer + 1,
            StringComparison.Ordinal);
        string transferBlock = transfer < 0 ? string.Empty
            : source.Substring(transfer, transferEnd - transfer);
        Require(transferBlock.Contains("if (!DistributedEnabled",
                StringComparison.Ordinal),
            "standard mode still transfers pawn custody");
        int carrierLoss = source.IndexOf(
            "internal static void RecordCarrierLoss",
            StringComparison.Ordinal);
        int carrierLossEnd = source.IndexOf(
            "\n        internal static bool PawnAvailable", carrierLoss + 1,
            StringComparison.Ordinal);
        string carrierLossBlock = carrierLoss < 0 ? string.Empty
            : source.Substring(carrierLoss, carrierLossEnd - carrierLoss);
        Require(carrierLossBlock.Contains("bool changed = false;",
                StringComparison.Ordinal)
            && carrierLossBlock.Contains(
                "if (changed) knowledge.revision++;",
                StringComparison.Ordinal),
            "non-carrier loss still records a knowledge revision");
        string regional = File.ReadAllText(Path.Combine(repo, "Source",
            "RegionalWorldModule.cs"));
        Require(regional.Contains("CanonicalCompatibilityTier(faction)",
                StringComparison.Ordinal)
            && regional.Contains("CanonicalBuildTechLevel(",
                StringComparison.Ordinal),
            "initial regional settlement facts use live carrier availability");
        string apertures = File.ReadAllText(Path.Combine(repo, "Source",
            "ApertureModule.cs"));
        Require(apertures.Contains("CanonicalTechTier(record.faction)",
                StringComparison.Ordinal),
            "initial apertures use live carrier availability");
    }

    private static object NewProfiledKnowledge(Type knowledgeType,
        Type technologyModel, Type axisSource, string profile, string identity)
    {
        object value = Activator.CreateInstance(knowledgeType)!;
        object source = Enum.Parse(axisSource, "Authored");
        Method(technologyModel, "ApplyProfile", 4).Invoke(null,
            new[] { value, profile, source, identity });
        Method(technologyModel, "Ensure", 3).Invoke(null,
            new object[] { value, identity, "subsistence" });
        return value;
    }

    private static object NewCustody(Type recordType, Type kindType,
        string domain, string competency, int rank, string kind, int pawnId,
        string custodian, int mapId = -1, int tileId = -1)
    {
        object record = Activator.CreateInstance(recordType)!;
        RequiredField(recordType, "domainKey").SetValue(record, domain);
        RequiredField(recordType, "competencyKey").SetValue(record, competency);
        RequiredField(recordType, "rank").SetValue(record, rank);
        RequiredField(recordType, "kind").SetValue(record,
            Enum.Parse(kindType, kind));
        RequiredField(recordType, "pawnThingId").SetValue(record, pawnId);
        RequiredField(recordType, "custodianKey").SetValue(record, custodian);
        RequiredField(recordType, "mapId").SetValue(record, mapId);
        RequiredField(recordType, "tileId").SetValue(record, tileId);
        RequiredField(recordType, "accessible").SetValue(record, true);
        RequiredField(recordType, "unavailableAtTick").SetValue(record, -1);
        RequiredField(recordType, "provenance").SetValue(record,
            "B15 executable receipt");
        return record;
    }

    private static int Effective(MethodInfo method, object knowledge,
        string domain, string competency, bool distributed,
        Func<int, bool> pawnAvailable)
    {
        return (int)method.Invoke(null, new object[]
            { knowledge, domain, competency, distributed, pawnAvailable })!;
    }

    private static int ScopedEffective(MethodInfo method, object knowledge,
        string domain, string competency, bool distributed,
        Func<int, bool> pawnAvailable, Delegate retainedAvailable)
    {
        return (int)method.Invoke(null, new object[] { knowledge, domain,
            competency, distributed, pawnAvailable, retainedAvailable })!;
    }

    private static Delegate CustodyPredicate(Type custodyType, int mapId)
    {
        Type delegateType = typeof(Func<,>).MakeGenericType(custodyType,
            typeof(bool));
        ParameterExpression record = Expression.Parameter(custodyType,
            "record");
        BinaryExpression body = Expression.Equal(Expression.Field(record,
            "mapId"), Expression.Constant(mapId));
        return Expression.Lambda(delegateType, body, record).Compile();
    }

    private static List<ResearchRow> AuditResearchDefinitions(string data,
        MethodInfo forResearch, Assembly assembly)
    {
        Type projectType = forResearch.GetParameters()[0].ParameterType;
        Type techLevel = RequiredField(projectType, "techLevel").FieldType;
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(data, "*.xml",
                     SearchOption.AllDirectories))
        {
            XDocument document;
            try { document = XDocument.Load(file, LoadOptions.None); }
            catch { continue; }
            foreach (XElement node in document.Descendants().Where(item =>
                         item.Name.LocalName == "ResearchProjectDef"))
            {
                string name = node.Elements().FirstOrDefault(item =>
                    item.Name.LocalName == "defName")?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
            }
        }
        Require(names.Count > 0, "installed research census is empty");
        var rows = new List<ResearchRow>();
        foreach (string name in names)
        {
            object project = RuntimeHelpers.GetUninitializedObject(projectType);
            RequiredField(projectType, "defName").SetValue(project, name);
            RequiredField(projectType, "techLevel").SetValue(project,
                Enum.Parse(techLevel, "Industrial"));
            IList requirements = ((IEnumerable)forResearch.Invoke(null,
                new[] { project })!).Cast<object>().ToList();
            Require(requirements.Count > 0,
                name + " produced no knowledge mapping");
            Require(requirements.Cast<object>().All(item =>
                    (bool)RequiredField(item.GetType(), "ExplicitlyMapped")
                        .GetValue(item)!
                    && !string.IsNullOrWhiteSpace(StringField(item,
                        "DomainKey"))),
                name + " is not explicitly mapped");
            rows.Add(new ResearchRow(name, string.Join(", ", requirements
                .Cast<object>().Select(item => StringField(item, "DomainKey"))
                .Distinct(StringComparer.Ordinal).OrderBy(item => item,
                    StringComparer.Ordinal))));
        }
        return rows;
    }

    private static void TestUnknownResearchFailsClosed(MethodInfo forResearch,
        Assembly assembly)
    {
        Type projectType = forResearch.GetParameters()[0].ParameterType;
        Type techLevel = RequiredField(projectType, "techLevel").FieldType;
        object project = RuntimeHelpers.GetUninitializedObject(projectType);
        RequiredField(projectType, "defName").SetValue(project,
            "GunPlantShipReceiptUnknown");
        RequiredField(projectType, "techLevel").SetValue(project,
            Enum.Parse(techLevel, "Industrial"));
        IList requirements = ((IEnumerable)forResearch.Invoke(null,
            new[] { project })!).Cast<object>().ToList();
        Require(requirements.Count == 1
                && !(bool)RequiredField(requirements[0].GetType(),
                    "ExplicitlyMapped").GetValue(requirements[0])!,
            "synthetic unknown research did not fail closed");
    }

    private static void TestExactResearchMappings(MethodInfo forResearch)
    {
        RequireResearchDomains(forResearch, "Electricity",
            "electrical-systems");
        RequireResearchDomains(forResearch, "MedicineProduction",
            "chemistry", "manufacturing", "medicine");
        RequireResearchDomains(forResearch, "ShipReactor",
            "electrical-systems", "logistics-preservation");
        RequireResearchDomains(forResearch, "Gunsmithing",
            "metallurgy-materials", "weapons-defense");
    }

    private static void RequireResearchDomains(MethodInfo forResearch,
        string defName, params string[] expected)
    {
        Type projectType = forResearch.GetParameters()[0].ParameterType;
        Type techLevel = RequiredField(projectType, "techLevel").FieldType;
        object project = RuntimeHelpers.GetUninitializedObject(projectType);
        RequiredField(projectType, "defName").SetValue(project, defName);
        RequiredField(projectType, "techLevel").SetValue(project,
            Enum.Parse(techLevel, "Industrial"));
        string[] actual = ((IEnumerable)forResearch.Invoke(null,
                new[] { project })!).Cast<object>()
            .Select(item => StringField(item, "DomainKey"))
            .OrderBy(item => item, StringComparer.Ordinal).ToArray();
        string[] orderedExpected = expected.OrderBy(item => item,
            StringComparer.Ordinal).ToArray();
        Require(actual.SequenceEqual(orderedExpected, StringComparer.Ordinal),
            defName + " mapped to [" + string.Join(", ", actual)
                + "] rather than [" + string.Join(", ", orderedExpected)
                + "]");
    }

    private static void TestConsumerTranslations(Type resolver,
        Type availability, Assembly assembly, object knowledge,
        Type technologyModel, Type axisSource)
    {
        MethodInfo forResearch = Method(resolver, "ForResearch", 1);
        Type projectType = forResearch.GetParameters()[0].ParameterType;
        Type techLevel = RequiredField(projectType, "techLevel").FieldType;
        object project = RuntimeHelpers.GetUninitializedObject(projectType);
        RequiredField(projectType, "defName").SetValue(project, "Electricity");
        RequiredField(projectType, "techLevel").SetValue(project,
            Enum.Parse(techLevel, "Industrial"));

        Type thingDef = RequiredGameType("Verse.ThingDef");
        object buildable = RuntimeHelpers.GetUninitializedObject(thingDef);
        RequiredField(thingDef, "defName").SetValue(buildable,
            "ReceiptPoweredBuilding");
        RequiredField(thingDef, "constructionSkillPrerequisite")
            .SetValue(buildable, 16);
        RequiredField(thingDef, "researchPrerequisites").SetValue(buildable,
            NewTypedList(projectType, project));
        IList buildRequirements = Requirements(resolver, "ForBuildable",
            buildable);
        Require(HasRequirement(buildRequirements, "electrical-systems",
                "construct"),
            "buildable translation diverged from research mapping");
        object practicalConstruction = buildRequirements.Cast<object>()
            .Single(item => StringField(item, "DomainKey") == "construction"
                && StringField(item, "CompetencyKey") == "construct");
        Require((int)RequiredField(practicalConstruction.GetType(), "Rank")
                    .GetValue(practicalConstruction)! == 4,
            "buildable translation ignored its practical skill prerequisite");

        IList maintenanceRequirements = Requirements(resolver,
            "ForMaintenance", buildable);
        Require(HasRequirement(maintenanceRequirements, "construction",
                "maintain")
                && HasRequirement(maintenanceRequirements,
                    "electrical-systems", "maintain"),
            "maintenance translation diverged from construction knowledge");

        Type recipeDef = Method(resolver, "ForRecipe", 1)
            .GetParameters()[0].ParameterType;
        object recipe = RuntimeHelpers.GetUninitializedObject(recipeDef);
        RequiredField(recipeDef, "researchPrerequisite").SetValue(recipe,
            project);
        IList recipeRequirements = Requirements(resolver, "ForRecipe", recipe);
        Require(HasRequirement(recipeRequirements, "electrical-systems",
                "operate"),
            "recipe translation diverged from research mapping");

        object plant = RuntimeHelpers.GetUninitializedObject(thingDef);
        Type plantProperties = RequiredField(thingDef, "plant").FieldType;
        object properties = RuntimeHelpers.GetUninitializedObject(
            plantProperties);
        RequiredField(plantProperties, "sowResearchPrerequisites")
            .SetValue(properties, NewTypedList(projectType, project));
        RequiredField(thingDef, "plant").SetValue(plant, properties);
        IList plantRequirements = Requirements(resolver, "ForPlant", plant);
        Require(HasRequirement(plantRequirements, "electrical-systems",
                "operate"),
            "plant translation diverged from research mapping");

        object weapon = RuntimeHelpers.GetUninitializedObject(thingDef);
        RequiredField(thingDef, "defName").SetValue(weapon,
            "ReceiptPoweredWeapon");
        Type thingCategory = RequiredField(thingDef, "category").FieldType;
        RequiredField(thingDef, "category").SetValue(weapon,
            Enum.Parse(thingCategory, "Item"));
        Type toolType = RequiredField(thingDef, "tools").FieldType
            .GetGenericArguments()[0];
        RequiredField(thingDef, "tools").SetValue(weapon,
            NewTypedList(toolType,
                RuntimeHelpers.GetUninitializedObject(toolType)));
        RequiredField(thingDef, "techLevel").SetValue(weapon,
            Enum.Parse(techLevel, "Industrial"));
        RequiredField(thingDef, "researchPrerequisites").SetValue(weapon,
            NewTypedList(projectType, project));
        IList weaponRequirements = Requirements(resolver, "ForWeapon", weapon);
        Require(HasRequirement(weaponRequirements, "weapons-defense",
                    "operate")
                && HasRequirement(weaponRequirements, "electrical-systems",
                    "operate"),
            "weapon operation did not use the common knowledge translation");

        IList manufacturedWeaponRequirements = Requirements(resolver,
            "ForManufacturedThing", weapon);
        Require(HasRequirement(manufacturedWeaponRequirements,
                    "weapons-defense", "construct")
                && HasRequirement(manufacturedWeaponRequirements,
                    "electrical-systems", "construct"),
            "manufactured weapon did not use the common exact definition "
                + "translation");

        IList medicalRequirements = Requirements(resolver,
            "ForMedicalCare", null);
        Require(HasRequirement(medicalRequirements, "medicine", "operate"),
            "ordinary medical care has no knowledge translation");

        Type habitatRequirement = RequiredType(assembly,
            "ColonistAwareness.CAHabitatRequirement");
        object medical = Enum.Parse(habitatRequirement, "MedicalCare");
        IList habitatRequirements = Requirements(resolver, "ForHabitat",
            medical);
        Require(HasRequirement(habitatRequirements, "medicine", "operate"),
            "habitat translation does not use the knowledge ontology");

        MethodInfo satisfies = Method(availability, "Satisfies", 5);
        object typedHabitatRequirements = NewTypedList(
            habitatRequirements[0]!.GetType(),
            habitatRequirements.Cast<object>().ToArray());
        object[] call = { knowledge, typedHabitatRequirements, false,
            new Func<int, bool>(_ => false), null };
        Require((bool)satisfies.Invoke(null, call)!,
            "authored faction knowledge did not satisfy habitat knowledge");

        object rankZero = NewProfiledKnowledge(knowledge.GetType(),
            technologyModel, axisSource, "subsistence",
            "rank-zero-consumer-receipt");
        MethodInfo setRank = Method(technologyModel, "SetRank", 6);
        object generated = Enum.Parse(axisSource, "Generated");
        foreach ((string domain, string competency) in new[]
                 {
                     ("construction", "construct"),
                     ("construction", "maintain"),
                     ("manufacturing", "operate"),
                     ("agriculture", "operate"),
                     ("medicine", "operate"),
                     ("weapons-defense", "construct"),
                     ("weapons-defense", "operate")
                 })
            setRank.Invoke(null, new object[]
                { rankZero, domain, competency, 0, generated, "receipt" });

        object baseBuildable = RuntimeHelpers.GetUninitializedObject(thingDef);
        RequiredField(thingDef, "defName").SetValue(baseBuildable,
            "ReceiptBaseShelter");
        IList baseBuildRequirements = Requirements(resolver, "ForBuildable",
            baseBuildable);
        Require(!Satisfies(satisfies, rankZero, baseBuildRequirements,
                out object missingBuild)
                && StringField(missingBuild, "DomainKey") == "construction",
            "rank-zero knowledge could construct a base buildable");

        object baseRecipe = RuntimeHelpers.GetUninitializedObject(recipeDef);
        RequiredField(recipeDef, "defName").SetValue(baseRecipe,
            "ReceiptBaseRecipe");
        IList baseRecipeRequirements = Requirements(resolver, "ForRecipe",
            baseRecipe);
        Require(!Satisfies(satisfies, rankZero, baseRecipeRequirements,
                out object missingRecipe)
                && StringField(missingRecipe, "DomainKey") == "manufacturing",
            "rank-zero knowledge could operate a base recipe");

        object basePlant = RuntimeHelpers.GetUninitializedObject(thingDef);
        object basePlantProperties = RuntimeHelpers.GetUninitializedObject(
            plantProperties);
        RequiredField(thingDef, "defName").SetValue(basePlant,
            "ReceiptBasePlant");
        RequiredField(thingDef, "plant").SetValue(basePlant,
            basePlantProperties);
        IList basePlantRequirements = Requirements(resolver, "ForPlant",
            basePlant);
        Require(!Satisfies(satisfies, rankZero, basePlantRequirements,
                out object missingPlant)
                && StringField(missingPlant, "DomainKey") == "agriculture",
            "rank-zero knowledge could grow a base plant");

        IList baseMaintenanceRequirements = Requirements(resolver,
            "ForMaintenance", baseBuildable);
        Require(!Satisfies(satisfies, rankZero,
                baseMaintenanceRequirements, out object missingMaintenance)
                && StringField(missingMaintenance, "DomainKey")
                    == "construction",
            "rank-zero knowledge could maintain a base buildable");

        object baseWeapon = RuntimeHelpers.GetUninitializedObject(thingDef);
        RequiredField(thingDef, "defName").SetValue(baseWeapon,
            "ReceiptBaseWeapon");
        RequiredField(thingDef, "category").SetValue(baseWeapon,
            Enum.Parse(thingCategory, "Item"));
        RequiredField(thingDef, "tools").SetValue(baseWeapon,
            NewTypedList(toolType,
                RuntimeHelpers.GetUninitializedObject(toolType)));
        IList baseWeaponRequirements = Requirements(resolver, "ForWeapon",
            baseWeapon);
        Require(!Satisfies(satisfies, rankZero, baseWeaponRequirements,
                out object missingWeapon)
                && StringField(missingWeapon, "DomainKey")
                    == "weapons-defense",
            "rank-zero knowledge could operate a base weapon");

        IList baseManufacturedWeaponRequirements = Requirements(resolver,
            "ForManufacturedThing", baseWeapon);
        Require(!Satisfies(satisfies, rankZero,
                baseManufacturedWeaponRequirements,
                out object missingManufacturedWeapon)
                && StringField(missingManufacturedWeapon, "DomainKey")
                    == "weapons-defense"
                && StringField(missingManufacturedWeapon, "CompetencyKey")
                    == "construct",
            "rank-zero knowledge could manufacture a base weapon");

        Require(!Satisfies(satisfies, rankZero, medicalRequirements,
                out object missingMedical)
                && StringField(missingMedical, "DomainKey") == "medicine",
            "rank-zero knowledge could provide ordinary medical care");

        foreach ((string domain, string competency) in new[]
                 {
                     ("construction", "construct"),
                     ("construction", "maintain"),
                     ("manufacturing", "operate"),
                     ("agriculture", "operate"),
                     ("medicine", "operate"),
                     ("weapons-defense", "construct"),
                     ("weapons-defense", "operate")
                 })
            setRank.Invoke(null, new object[]
                { rankZero, domain, competency, 1, generated, "receipt" });
        Require(Satisfies(satisfies, rankZero, baseBuildRequirements,
                    out _)
                && Satisfies(satisfies, rankZero, baseRecipeRequirements,
                    out _)
                && Satisfies(satisfies, rankZero, basePlantRequirements,
                    out _)
                && Satisfies(satisfies, rankZero,
                    baseMaintenanceRequirements, out _)
                && Satisfies(satisfies, rankZero,
                    baseManufacturedWeaponRequirements, out _)
                && Satisfies(satisfies, rankZero, baseWeaponRequirements,
                    out _)
                && Satisfies(satisfies, rankZero, medicalRequirements,
                    out _),
            "matching rank-one knowledge did not unlock base consumers");
    }

    private static void AuditCampaignSchema(Assembly assembly)
    {
        Type catalog = RequiredType(assembly,
            "ColonistAwareness.CACampaignSchemaCatalog");
        Require((int)RequiredField(catalog, "CurrentCatalogVersion")
                .GetRawConstantValue()! == 5,
            "campaign schema catalog is not current version 5");
        IList definitions = (IList)RequiredField(catalog, "All").GetValue(null)!;
        var versions = definitions.Cast<object>().ToDictionary(
            item => StringProperty(item, "Key"),
            item => (int)RequiredProperty(item.GetType(), "CurrentVersion")
                .GetValue(item)!);
        Require(versions["world.faction-state"] == 4
                && versions["world.player-founding"] == 4
                && versions["world.regional"] == 4
                && versions["model.technological-knowledge"] == 1
                && versions["model.player-founding-plan"] == 4
                && versions["model.regional-plan"] == 15
                && versions["model.regional-settlement-record"] == 9,
            "current campaign schema versions do not retain B15 ownership");
    }

    private static void TestSupportedB14Migrations(Assembly gameAssembly,
        Assembly assembly, string repo)
    {
        string evidence = Path.Combine(repo, "Receipts", "B15", "evidence",
            "active-schema13-compatible-migration.xml");
        Require(File.Exists(evidence),
            "derived compatible B14 migration fixture is missing");
        Type planType = RequiredType(assembly,
            "ColonistAwareness.CARegionalPlan");

        object factionSourcePlan = LoadDeepRoot(gameAssembly, planType,
            evidence, "plan");
        IList factionPlans = (IList)RequiredField(planType, "factions")
            .GetValue(factionSourcePlan)!;
        Require(factionPlans.Count > 0,
            "B14 migration fixture has no faction source");
        object factionPlan = factionPlans[0]!;
        Type factionStateType = RequiredType(assembly,
            "ColonistAwareness.CAFactionState");
        object factionState = Activator.CreateInstance(factionStateType)!;
        RequiredField(factionStateType, "factionLoadId").SetValue(factionState,
            74001);
        RequiredField(factionStateType, "factionName").SetValue(factionState,
            "B15 migration faction");
        foreach (string field in new[]
                 { "culture", "politicalBeliefs", "factionStructure" })
            RequiredField(factionStateType, field).SetValue(factionState,
                RequiredField(factionPlan.GetType(), field)
                    .GetValue(factionPlan));
        RequiredField(factionStateType, "technologicalKnowledge")
            .SetValue(factionState, null);
        Type factionOwnerType = RequiredType(assembly,
            "ColonistAwareness.CAFactionStateWorldComponent");
        object factionOwner = RuntimeHelpers.GetUninitializedObject(
            factionOwnerType);
        RequiredField(factionOwnerType, "campaignSchemaVersion")
            .SetValue(factionOwner, 2);
        RequiredField(factionOwnerType, "factionStates").SetValue(factionOwner,
            NewTypedList(factionStateType, factionState));
        object factionFailure = Method(factionOwnerType,
            "MigrateSupportedState", 0).Invoke(factionOwner,
                Array.Empty<object>());
        Require(factionFailure == null,
            "faction owner migration failed: " + factionFailure);
        IList migratedFactions = (IList)RequiredField(factionOwnerType,
            "factionStates").GetValue(factionOwner)!;
        object migratedFactionKnowledge = RequiredField(factionStateType,
            "technologicalKnowledge").GetValue(migratedFactions[0])!;
        Require(migratedFactionKnowledge != null
                && ListCount(migratedFactionKnowledge, "domains") == 9
                && Method(factionOwnerType, "ValidateCampaignState", 0)
                    .Invoke(factionOwner, Array.Empty<object>()) == null,
            "faction owner migration did not produce valid knowledge");

        object foundingSourcePlan = LoadDeepRoot(gameAssembly, planType,
            evidence, "plan");
        object founding = RequiredField(planType, "playerFounding")
            .GetValue(foundingSourcePlan)!;
        Require((int)RequiredField(founding.GetType(), "schemaVersion")
                .GetValue(founding)! == 3,
            "B14 founding migration source is not schema 3");
        // The preserved regional fixture is an unfinished authoring plan. The
        // world-owner migration contract applies only after confirmation, so
        // exercise it as the valid live B14 state it would have become.
        RequiredField(founding.GetType(), "confirmed").SetValue(founding,
            true);
        Type foundingOwnerType = RequiredType(assembly,
            "ColonistAwareness.CAPlayerFoundingWorldComponent");
        object foundingOwner = RuntimeHelpers.GetUninitializedObject(
            foundingOwnerType);
        RequiredField(foundingOwnerType, "campaignSchemaVersion")
            .SetValue(foundingOwner, 2);
        RequiredField(foundingOwnerType, "founding").SetValue(foundingOwner,
            founding);
        object foundingFailure = Method(foundingOwnerType,
            "MigrateSupportedState", 0).Invoke(foundingOwner,
                Array.Empty<object>());
        Require(foundingFailure == null,
            "founding owner migration failed: " + foundingFailure);
        object migratedFounding = RequiredField(foundingOwnerType, "founding")
            .GetValue(foundingOwner)!;
        Require((int)RequiredField(migratedFounding.GetType(), "schemaVersion")
                    .GetValue(migratedFounding)! == 4
                && ListCount(RequiredField(migratedFounding.GetType(),
                    "technologicalKnowledge").GetValue(migratedFounding)!,
                    "domains") == 9
                && Method(foundingOwnerType, "ValidateCampaignState", 0)
                    .Invoke(foundingOwner, Array.Empty<object>()) == null,
            "founding owner migration did not produce valid schema 4 state");

        object regionalPlan = LoadDeepRoot(gameAssembly, planType, evidence,
            "plan");
        Require((int)RequiredField(planType, "schemaVersion")
                .GetValue(regionalPlan)! == 13,
            "B14 regional migration source is not schema 13");
        IList settlements = (IList)RequiredField(planType, "settlements")
            .GetValue(regionalPlan)!;
        Require(settlements.Count > 0,
            "B14 regional migration fixture has no settlement");
        object regionalFounding = RequiredField(planType, "playerFounding")
            .GetValue(regionalPlan)!;
        RequiredField(regionalFounding.GetType(), "confirmed").SetValue(
            regionalFounding, true);
        object settlementPlan = settlements[0]!;
        Type recordType = RequiredType(assembly,
            "ColonistAwareness.CARegionalSettlementRecord");
        object record = Activator.CreateInstance(recordType)!;
        RequiredField(recordType, "schemaVersion").SetValue(record, 8);
        RequiredField(recordType, "regionalId").SetValue(record,
            StringField(regionalPlan, "regionalId"));
        RequiredField(recordType, "slot").SetValue(record,
            (int)RequiredField(settlementPlan.GetType(), "slot")
                .GetValue(settlementPlan)!);
        RequiredField(recordType, "factionKey").SetValue(record,
            (int)RequiredField(settlementPlan.GetType(), "factionKey")
                .GetValue(settlementPlan)!);
        RequiredField(recordType, "culture").SetValue(record,
            RequiredField(settlementPlan.GetType(), "localCulture")
                .GetValue(settlementPlan));

        Type regionalOwnerType = RequiredType(assembly,
            "ColonistAwareness.CARegionalWorldComponent");
        object regionalOwner = RuntimeHelpers.GetUninitializedObject(
            regionalOwnerType);
        RequiredField(regionalOwnerType, "campaignSchemaVersion")
            .SetValue(regionalOwner, 2);
        RequiredField(regionalOwnerType, "regions").SetValue(regionalOwner,
            NewTypedList(planType, regionalPlan));
        RequiredField(regionalOwnerType, "records").SetValue(regionalOwner,
            NewTypedList(recordType, record));
        RequiredField(regionalOwnerType, "worldPolicy").SetValue(regionalOwner,
            RequiredField(planType, "worldPolicy").GetValue(regionalPlan));
        RequiredField(regionalOwnerType, "groundwater").SetValue(regionalOwner,
            RequiredField(planType, "groundwater").GetValue(regionalPlan));
        object regionalFailure = Method(regionalOwnerType,
            "MigrateSupportedState", 0).Invoke(regionalOwner,
                Array.Empty<object>());
        Require(regionalFailure == null,
            "regional owner migration failed: " + regionalFailure);
        Require((int)RequiredField(planType, "schemaVersion")
                    .GetValue(regionalPlan)! == 15
                && (int)RequiredField(recordType, "schemaVersion")
                    .GetValue(record)! == 9
                && !string.IsNullOrEmpty(StringField(record,
                    "factionKnowledgeId"))
                && Method(regionalOwnerType, "ValidateCampaignState", 0)
                    .Invoke(regionalOwner, Array.Empty<object>()) == null,
            "regional owner migration did not produce valid current plan, "
                + "founding, and settlement-record state");
    }

    private static void TestSettlementPotentialBoundary(Assembly assembly)
    {
        Type environmentType = RequiredType(assembly,
            "ColonistAwareness.CAHabitatEnvironmentInput");
        Type capabilityType = RequiredType(assembly,
            "ColonistAwareness.CAHabitatCapabilityInput");
        Type kernel = RequiredType(assembly,
            "ColonistAwareness.CAHabitatViabilityCausalKernel");
        MethodInfo requirements = Method(kernel, "Requirements", 1);
        MethodInfo settlementPotential = Method(kernel,
            "SettlementPotentialCapability", 1);
        MethodInfo evaluate = Method(kernel, "Evaluate", 2);

        object ordinary = Activator.CreateInstance(environmentType)!;
        RequiredField(environmentType, "GrowingTwelfths").SetValue(ordinary, 8);
        RequiredField(environmentType, "FoodSupport").SetValue(ordinary, .75f);
        RequiredField(environmentType, "DiseasePerYear").SetValue(ordinary,
            1.2f);
        object ordinaryRequirements = requirements.Invoke(null,
            new[] { ordinary })!;
        object ordinaryPotential = evaluate.Invoke(null, new[]
        {
            ordinaryRequirements,
            settlementPotential.Invoke(null, new object[] { 0 })
        })!;
        object unrealized = Activator.CreateInstance(capabilityType)!;
        RequiredField(capabilityType, "KnowledgeCompatibilityTier")
            .SetValue(unrealized, 0);
        foreach (string field in new[]
                 {
                     "EnclosedShelter", "ReliableFood", "FoodReserve",
                     "ArtificialLight"
                 })
            RequiredField(capabilityType, field).SetValue(unrealized, true);
        object ordinaryConfirmation = evaluate.Invoke(null,
            new[] { ordinaryRequirements, unrealized })!;
        Require((bool)RequiredField(ordinaryPotential.GetType(), "Viable")
                    .GetValue(ordinaryPotential)!
                && !(bool)RequiredField(ordinaryConfirmation.GetType(),
                    "Viable").GetValue(ordinaryConfirmation)!,
            "placement potential was conflated with realized facilities");

        object severe = Activator.CreateInstance(environmentType)!;
        RequiredField(environmentType, "GrowingTwelfths").SetValue(severe, 8);
        RequiredField(environmentType, "FoodSupport").SetValue(severe, .75f);
        RequiredField(environmentType, "DiseasePerYear").SetValue(severe,
            2.1f);
        object severeRequirements = requirements.Invoke(null,
            new[] { severe })!;
        object primitive = evaluate.Invoke(null, new[]
        {
            severeRequirements,
            settlementPotential.Invoke(null, new object[] { 0 })
        })!;
        object capable = evaluate.Invoke(null, new[]
        {
            severeRequirements,
            settlementPotential.Invoke(null, new object[] { 1 })
        })!;
        Require(!(bool)RequiredField(primitive.GetType(), "Viable")
                    .GetValue(primitive)!
                && (bool)RequiredField(capable.GetType(), "Viable")
                    .GetValue(capable)!,
            "environmental knowledge threshold was lost at placement");
    }

    private static void TestCurrentRegionalFixtureLoad(Assembly gameAssembly,
        Assembly assembly, string fixture)
    {
        Require(File.Exists(fixture), "current regional fixture is missing");
        Type scribe = gameAssembly.GetType("Verse.Scribe", true)!;
        Type log = gameAssembly.GetType("Verse.Log", true)!;
        Type deep = gameAssembly.GetType("Verse.Scribe_Deep", true)!;
        Type prefs = gameAssembly.GetType("Verse.Prefs", true)!;
        Type planType = RequiredType(assembly,
            "ColonistAwareness.CARegionalPlan");
        FieldInfo logDisablers = RequiredField(log, "logDisablers");
        FieldInfo prefsData = RequiredField(prefs, "data");
        if (prefsData.GetValue(null) == null)
            prefsData.SetValue(null, Activator.CreateInstance(
                prefsData.FieldType));
        int prior = (int)logDisablers.GetValue(null)!;
        object loader = RequiredField(scribe, "loader").GetValue(null)!;
        MethodInfo look = deep.GetMethods(All).Single(method =>
                method.Name == "Look" && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 3
                && method.GetParameters()[1].ParameterType == typeof(string))
            .MakeGenericMethod(planType);
        object plan = null;
        logDisablers.SetValue(null, prior + 1);
        try
        {
            Method(loader.GetType(), "InitLoading", 1).Invoke(loader,
                new object[] { fixture });
            object[] values = { plan, "plan", Array.Empty<object>() };
            try
            {
                look.Invoke(null, values);
                plan = values[0];
            }
            finally
            {
                Method(loader.GetType(), "FinalizeLoading", 0).Invoke(loader,
                    Array.Empty<object>());
            }
        }
        finally
        {
            logDisablers.SetValue(null, prior);
        }

        Require(plan != null, "current regional fixture did not Scribe-load");
        Require((int)RequiredField(planType, "schemaVersion").GetValue(plan)!
                == 15,
            "current regional fixture did not load as schema 15");
        IList factions = (IList)RequiredField(planType, "factions")
            .GetValue(plan)!;
        IList settlements = (IList)RequiredField(planType, "settlements")
            .GetValue(plan)!;
        IList relations = (IList)RequiredField(planType, "relations")
            .GetValue(plan)!;
        Require(factions.Count == 3 && settlements.Count == 4
                && relations.Count == 3,
            "current regional fixture dropped authored composition on load");
        foreach (object faction in factions)
        {
            object knowledge = RequiredField(faction.GetType(),
                "technologicalKnowledge").GetValue(faction)!;
            Require(knowledge != null && ListCount(knowledge, "domains") == 9,
                "faction knowledge did not survive current-schema load");
        }
        object founding = RequiredField(planType, "playerFounding")
            .GetValue(plan)!;
        object foundingKnowledge = RequiredField(founding.GetType(),
            "technologicalKnowledge").GetValue(founding)!;
        Require((int)RequiredField(founding.GetType(), "schemaVersion")
                    .GetValue(founding)! == 4
                && foundingKnowledge != null
                && ListCount(foundingKnowledge, "domains") == 9,
            "founding knowledge did not survive current-schema load");
    }

    private static void AuditOwnershipAndConsumerSource(string repo)
    {
        string faction = Read(repo, "Source/FactionStateModule.cs");
        string society = Read(repo, "Source/SocietyPresetModule.cs");
        string technology = Read(repo, "Source/TechnologicalKnowledgeModule.cs");
        string habitat = Read(repo, "Source/HabitatViabilityModule.cs");
        string autonomous = Read(repo, "Source/AutonomousHomeModule.cs");
        string furnishing = Read(repo, "Source/SpatialFurnishingModule.cs");
        string regional = Read(repo, "Source/RegionalSetupModule.cs");
        string founding = Read(repo, "Source/PlayerFoundingStateModule.cs");
        string settlement = Read(repo, "Source/RegionalWorldModule.cs");
        string settings = Read(repo, "Source/ModEntry.cs");
        string materializer = Read(repo,
            "Source/SettlementProgramMaterializerModule.cs");
        string frontier = Read(repo, "Source/FrontierModule.cs");
        string morphology = Read(repo, "Source/MorphologyAdapterModule.cs");
        string security = Read(repo, "Source/SecurityStructuresModule.cs");
        Require(faction.Contains("CATechnologicalKnowledge technologicalKnowledge")
                && society.Contains("faction-owned components"),
            "faction ownership is not explicit in source");
        Require(regional.Contains("CATechnologicalKnowledge technologicalKnowledge")
                && founding.Contains("CATechnologicalKnowledge technologicalKnowledge"),
            "regional/founding staging does not carry faction knowledge");
        Require(settlement.Contains("factionKnowledgeId")
                && !settlement.Contains("public CATechnologicalKnowledge technologicalKnowledge"),
            "settlement materialization duplicated faction-owned knowledge");
        Require(technology.Contains("Pawn.SetFaction")
                && technology.Contains("Pawn.SpawnSetup")
                && technology.Contains("Pawn.Kill")
                && technology.Contains("ResearchManager")
                && technology.Contains("GenConstruct.CanConstruct")
                && technology.Contains("JobDriver_ConstructFinishFrame")
                && technology.Contains("WorkGiver_DoBill")
                && technology.Contains("JobDriver_DoBill")
                && technology.Contains("Command_SetPlantToGrow")
                && technology.Contains("WorkGiver_GrowerSow")
                && technology.Contains("JobDriver_PlantSow")
                && technology.Contains("WorkGiver_Tend")
                && technology.Contains("JobDriver_TendPatient")
                && technology.Contains("WorkGiver_Repair")
                && technology.Contains("JobDriver_Repair")
                && technology.Contains("WorkGiver_FixBrokenDownBuilding")
                && technology.Contains("JobDriver_FixBrokenDownBuilding")
                && technology.Contains("Verb.Available"),
            "knowledge lifecycle or native consumers are not integrated");
        Require(technology.Contains(
                    "Dictionary<string, string[]> ResearchDomains")
                && !technology.Contains(
                    "HarmonyPatch(typeof(ResearchProjectDef), \"get_IsFinished\")"),
            "research mapping is lexical or CA replaces native completion truth");
        Require(habitat.Contains("CATechnologicalKnowledgeAvailability.Satisfies")
                && autonomous.Contains("CATechnologicalKnowledgeRuntime.CanConstruct")
                && furnishing.Contains("CATechnologicalKnowledgeRuntime.CanConstruct"),
            "habitat/autonomous consumers bypass canonical knowledge");
        Require(frontier.Contains("CanConstructCanonical")
                && morphology.Contains("CanConstructCanonical")
                && security.Contains("CanConstructCanonical")
                && materializer.Contains("CanConstructCanonical")
                && materializer.Contains("CanManufacture")
                && materializer.Contains("CanUseWeapon")
                && materializer.Contains("CanMaintain"),
            "initial settlement generation or milestone materialization "
                + "bypasses exact knowledge requirements");
        Require(settings.Contains("experimentalDistributedKnowledge = false"),
            "standard faction-level availability is not the default");
        string[] sourceFiles = Directory.GetFiles(Path.Combine(repo, "Source"),
            "*.cs", SearchOption.TopDirectoryOnly);
        foreach (string file in sourceFiles)
        {
            string text = File.ReadAllText(file);
            if (!file.EndsWith("TechnologicalKnowledgeModule.cs",
                    StringComparison.OrdinalIgnoreCase))
                Require(!text.Contains(".techLevel"),
                    Path.GetFileName(file) + " retains hidden FactionDef authority");
            Require(!text.Contains("factionEra")
                    && !text.Contains("FactionEraLabel")
                    && !text.Contains("TemplateEraPrior"),
                Path.GetFileName(file) + " retains obsolete faction-era state");
        }
    }

    private static IReadOnlyList<NativeTechRead> AuditNativeTechAuthority(
        string repo, string decompiled)
    {
        Require(Directory.Exists(decompiled),
            "RimWorld decompiled source directory is missing");
        var directFactionTech = new Regex(
            @"(?:FactionDefOf\.PlayerColony|\.def|FactionDef|factionDef|faction)\.techLevel"
            + @"|FactionDefOf\.PlayerColony\.techLevel",
            RegexOptions.CultureInvariant);
        var reads = new List<NativeTechRead>();
        foreach (string file in Directory.GetFiles(decompiled, "*.cs",
                     SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(decompiled, file)
                .Replace('\\', '/');
            string[] lines = File.ReadAllLines(file);
            for (int index = 0; index < lines.Length; index++)
            {
                string source = lines[index].Trim();
                if (!directFactionTech.IsMatch(source)) continue;
                reads.Add(new NativeTechRead(relative, index + 1, source,
                    ClassifyNativeTechRead(relative)));
            }
        }

        reads.Sort((left, right) =>
        {
            int file = StringComparer.Ordinal.Compare(left.File, right.File);
            return file != 0 ? file : left.Line.CompareTo(right.Line);
        });
        Require(reads.Count >= 60,
            "native FactionDef tech authority census is unexpectedly incomplete");
        Require(reads.Count(item => item.File.EndsWith(
                "Designator_Build.cs", StringComparison.Ordinal)) == 2,
            "native build-visibility authority boundary changed");
        Require(reads.Count(item => item.File.EndsWith(
                    "ResearchProjectDef.cs", StringComparison.Ordinal)
                || item.File.EndsWith("ResearchManager.cs",
                    StringComparison.Ordinal)
                || item.File.EndsWith("MainTabWindow_Research.cs",
                    StringComparison.Ordinal)) == 6,
            "native player research authority boundary changed");
        Require(reads.Count(item => item.File.Contains("/BaseGen/",
                StringComparison.Ordinal)) >= 20,
            "native BaseGen tech-level boundary changed");

        string technology = Read(repo,
            "Source/TechnologicalKnowledgeModule.cs");
        string regional = Read(repo, "Source/RegionalWorldModule.cs");
        Require(technology.Contains(
                    "HarmonyPatch(typeof(Designator_Build), \"get_Visible\")")
                && technology.Contains(
                    "HarmonyPatch(typeof(ResearchProjectDef), \"get_CostApparent\")")
                && technology.Contains(
                    "HarmonyPatch(typeof(ResearchManager)")
                && technology.Contains(
                    "HarmonyPatch(typeof(MainTabWindow_Research)")
                && technology.Contains(
                    "HarmonyPatch(typeof(Verb), nameof(Verb.Available))"),
            "CA player research, build visibility, or weapon-use replacement is missing");
        Require(regional.Contains("BaseGen.symbolStack.Push(\"pawnGroup\"")
                && !regional.Contains(
                    "BaseGen.symbolStack.Push(\"settlement\""),
            "CA settlement realization began delegating structure authority to native BaseGen");
        Console.WriteLine("PASS decompiled native tech census: "
            + reads.Count + " direct FactionDef reads classified");
        return reads;
    }

    private static string ClassifyNativeTechRead(string relative)
    {
        string file = Path.GetFileName(relative);
        if (file == "Designator_Build.cs")
            return "CA replacement for authored player factions: build "
                + "visibility and execution query faction knowledge; native "
                + "metadata remains fallback for unsupported factions.";
        if (file is "ResearchProjectDef.cs" or "ResearchManager.cs"
            or "MainTabWindow_Research.cs")
            return "CA replacement for authored player factions: research "
                + "cost, progress, speed, and presentation query faction "
                + "knowledge; native behavior remains for unsupported factions.";
        if (relative.Contains("/BaseGen/", StringComparison.Ordinal))
            return "Retained native template/content selection. CA-authored "
                + "regional structures use CA realization with exact knowledge "
                + "gates; CA invokes BaseGen only for pawn-group materialization.";
        if (file.Contains("PawnInventory", StringComparison.Ordinal)
            || file.Contains("PawnApparel", StringComparison.Ordinal)
            || file.Contains("PawnAddiction", StringComparison.Ordinal)
            || file is "GenStuff.cs" or "MapGenUtility.cs"
            or "LordToil_Siege.cs" or "GenStep_Turrets.cs")
            return "Retained native pawn/loadout or encounter template "
                + "selection. Equipped weapon operation for authored factions "
                + "is independently gated by faction knowledge.";
        if (file is "IdeoUtility.cs" or "RitualPatternDef.cs")
            return "Retained native Ideoligion compatibility metadata; native "
                + "Ideoligion remains the executor of its own mechanics.";
        return "Retained native world, quest, pawn, dialogue, debug, or template "
            + "compatibility outside CA-authored technological capability.";
    }

    private static IList Requirements(Type resolver, string method,
        object input)
    {
        int parameterCount = input == null ? 0 : 1;
        object[] arguments = parameterCount == 0
            ? Array.Empty<object>() : new[] { input };
        return ((IEnumerable)Method(resolver, method, parameterCount)
            .Invoke(null, arguments)!).Cast<object>().ToList();
    }

    private static bool Satisfies(MethodInfo method, object knowledge,
        IList requirements, out object missing)
    {
        Type requirementType = requirements.Count > 0
            ? requirements[0]!.GetType()
            : method.GetParameters()[1].ParameterType.GetGenericArguments()[0];
        object typed = NewTypedList(requirementType,
            requirements.Cast<object>().ToArray());
        object[] args = { knowledge, typed, false,
            new Func<int, bool>(_ => false), null };
        bool result = (bool)method.Invoke(null, args)!;
        missing = args[4];
        return result;
    }

    private static bool HasRequirement(IList values, string domain,
        string competency)
    {
        return values.Cast<object>().Any(item =>
            StringField(item, "DomainKey") == domain
            && StringField(item, "CompetencyKey") == competency
            && (bool)RequiredField(item.GetType(), "ExplicitlyMapped")
                .GetValue(item)!);
    }

    private static object NewTypedList(Type itemType, params object[] items)
    {
        Type listType = typeof(List<>).MakeGenericType(itemType);
        IList result = (IList)Activator.CreateInstance(listType)!;
        foreach (object item in items) result.Add(item);
        return result;
    }

    private static object ScribeRoundTrip(Assembly gameAssembly,
        Type valueType, object value, string root)
    {
        Type scribe = gameAssembly.GetType("Verse.Scribe", true)!;
        Type log = gameAssembly.GetType("Verse.Log", true)!;
        FieldInfo logDisablers = RequiredField(log, "logDisablers");
        int prior = (int)logDisablers.GetValue(null)!;
        logDisablers.SetValue(null, prior + 1);
        object saver = RequiredField(scribe, "saver").GetValue(null)!;
        object loader = RequiredField(scribe, "loader").GetValue(null)!;
        MethodInfo expose = Method(valueType, "ExposeData", 0);
        string path = Path.Combine(Path.GetTempPath(), "cao-b15-"
            + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            Method(saver.GetType(), "InitSaving", 2).Invoke(saver,
                new object[] { path, root });
            try { expose.Invoke(value, Array.Empty<object>()); }
            finally { Method(saver.GetType(), "FinalizeSaving", 0)
                .Invoke(saver, Array.Empty<object>()); }
            Method(loader.GetType(), "InitLoading", 1).Invoke(loader,
                new object[] { path });
            object restored = Activator.CreateInstance(valueType)!;
            try { expose.Invoke(restored, Array.Empty<object>()); }
            finally { Method(loader.GetType(), "ForceStop", 0)
                .Invoke(loader, Array.Empty<object>()); }
            return restored;
        }
        finally
        {
            logDisablers.SetValue(null, prior);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static object LoadDeepRoot(Assembly gameAssembly, Type valueType,
        string path, string root)
    {
        Type scribe = gameAssembly.GetType("Verse.Scribe", true)!;
        Type log = gameAssembly.GetType("Verse.Log", true)!;
        Type prefs = gameAssembly.GetType("Verse.Prefs", true)!;
        Type deep = gameAssembly.GetType("Verse.Scribe_Deep", true)!;
        FieldInfo logDisablers = RequiredField(log, "logDisablers");
        FieldInfo prefsData = RequiredField(prefs, "data");
        if (prefsData.GetValue(null) == null)
            prefsData.SetValue(null, Activator.CreateInstance(
                prefsData.FieldType));
        int prior = (int)logDisablers.GetValue(null)!;
        object loader = RequiredField(scribe, "loader").GetValue(null)!;
        MethodInfo look = deep.GetMethods(All).Single(method =>
                method.Name == "Look" && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 3
                && method.GetParameters()[1].ParameterType == typeof(string))
            .MakeGenericMethod(valueType);
        object value = null;
        logDisablers.SetValue(null, prior + 1);
        try
        {
            Method(loader.GetType(), "InitLoading", 1).Invoke(loader,
                new object[] { path });
            object[] values = { value, root, Array.Empty<object>() };
            try
            {
                look.Invoke(null, values);
                value = values[0];
            }
            finally
            {
                Method(loader.GetType(), "FinalizeLoading", 0).Invoke(loader,
                    Array.Empty<object>());
            }
        }
        finally { logDisablers.SetValue(null, prior); }
        Require(value != null, path + " did not load " + root);
        return value;
    }

    private static void ApplySociety(MethodInfo method, object preset,
        object culture, object political, object knowledge, string source)
    {
        object[] values = { preset, culture, political, knowledge, source, null };
        bool applied = (bool)method.Invoke(null, values)!;
        Require(applied, StringProperty(preset, "Key") + ": " + values[5]);
    }

    private static void WriteExecutionReceipt(string path, string dll,
        string hash, int presetCount, IReadOnlyList<string> presetHashes,
        int standardRank)
    {
        var text = Header("B15 Technological Knowledge Execution Receipt",
                dll, hash)
            .AppendLine("| Check | Result |")
            .AppendLine("|---|---|")
            .AppendLine("| Canonical owner | PASS - `CAFactionState` owns Technological Knowledge beside Culture and Political Order |")
            .AppendLine("| Society initialization | PASS - " + presetCount + "/" + presetCount + " built-in Society presets atomically apply and match all three faction components |")
            .AppendLine("| Failed validation | PASS - an invalid technological component leaves all three targets byte-equivalent at the data level |")
            .AppendLine("| Saved Society | PASS - Culture, Political Order, Technological Knowledge ranks, and exact research evidence survive Scribe serialization/readback and participate in matching; pawn custody is not copied |")
            .AppendLine("| Standard mode | PASS - canonical faction rank " + standardRank + " remains effective independently of pawn custody |")
            .AppendLine("| Mode boundary | PASS - standard mode does not transfer canonical ranks through dormant pawn custody |")
            .AppendLine("| Non-carrier loss | PASS - losing a pawn with no active custody records does not create a false knowledge revision |")
            .AppendLine("| Initial realization | PASS - settlement form, wealth, construction era, morphology, and apertures use canonical faction knowledge before residents spawn |")
            .AppendLine("| Distributed mode | PASS - availability is projected from the same canonical domains through living pawn, institution, and record custody |")
            .AppendLine("| Local availability | PASS - a carrier contributes on its supplied map/settlement boundary and does not unlock another map |")
            .AppendLine("| Redundancy | PASS - two carriers survive one loss |")
            .AppendLine("| Isolated carrier | PASS - loss removes availability and later queries do not regenerate custody |")
            .AppendLine("| Incapacity/recovery | PASS - the carrier-availability predicate removes and restores the same knowledge without rewriting it |")
            .AppendLine("| Persistent retention | PASS - institutional and recorded custody survive Scribe readback |")
            .AppendLine("| Placement versus realization | PASS - settlement placement checks whether the faction can establish required functions; confirmation still rejects absent programs and material functions |")
            .AppendLine("| Base consumers | PASS - rank-zero knowledge cannot build, maintain, manufacture or use weapons, operate recipes, sow plants, or provide medical care; matching rank-one knowledge can |")
            .AppendLine("| Supported migration | PASS - B14 faction owner, founding owner, regional plan, nested founding plan, and settlement receipt execute their B15 migration and validate |")
            .AppendLine("| Current authored fixture | PASS - current regional/founding schemas Scribe-load with 3 factions, 4 settlements, all relations, and all four staged knowledge compositions intact |")
            .AppendLine("| Settlement realization | PASS - settlements persist a faction knowledge identity/revision/tier receipt rather than a second knowledge owner |")
            .AppendLine()
            .AppendLine("Three-component fingerprints: `"
                + string.Join("`, `", presetHashes) + "`")
            .AppendLine()
            .AppendLine("This executable receipt verifies causal data behavior. It does not claim operator visual or gameplay acceptance.");
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static void WriteMappingReceipt(string path, string dll,
        string hash, IReadOnlyList<ResearchRow> rows)
    {
        var text = Header("B15 Technological Knowledge Mapping Receipt", dll,
                hash)
            .AppendLine("Installed native/DLC research definitions: "
                + rows.Count + "/" + rows.Count + " explicitly mapped.")
            .AppendLine()
            .AppendLine("The mapping is keyed by exact `ResearchProjectDef.defName`. Representative multi-domain assertions cover `MedicineProduction`, `ShipReactor`, and `Gunsmithing`; `Electricity` is asserted as electrical only. A synthetic project containing the words Gun, Plant, and Ship remains deliberately unmapped and fails closed.")
            .AppendLine()
            .AppendLine("| Research project | Knowledge domain(s) |")
            .AppendLine("|---|---|");
        foreach (ResearchRow row in rows)
            text.AppendLine("| `" + row.DefName + "` | " + row.Domains + " |");
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static void WriteStaticReceipt(string path, string repo,
        string dll, string hash)
    {
        var text = Header("B15 Technological Knowledge Static Receipt", dll,
                hash)
            .AppendLine("| Contract | Result |")
            .AppendLine("|---|---|")
            .AppendLine("| Ownership | PASS - faction is the sole realized owner; regional/founding plans stage values; Society is the joint editor; presets are snapshots |")
            .AppendLine("| Hidden authority | PASS - no CA source outside the single translator reads `.techLevel`; no faction-era serialized field remains |")
            .AppendLine("| Native translation | PASS - research, buildables, manufactured items, recipes, plants, habitat requirements, and native build visibility converge through one knowledge module |")
            .AppendLine("| Native pass-through | PASS - temporary and non-humanlike player factions keep native research, build, recipe, and plant results |")
            .AppendLine("| Lifecycle | PASS - recruitment/departure, death, incapacity queries, research, custody, and redundancy use the same domain/competency keys |")
            .AppendLine("| Habitat | PASS - environment requirements remain separate from knowledge and material/program satisfaction |")
            .AppendLine("| Autonomous consumers | PASS - autonomous homes and spatial furnishing call the canonical construction query |")
            .AppendLine("| Campaign schemas | PASS - catalog 5 retains knowledge schema 1, supports B14-to-B15 migration, and advances adjacent social owners for Culture registry 3 |")
            .AppendLine("| Pending authoring | PASS - epoch 13 discards obsolete two-component drafts rather than inventing authored knowledge |")
            .AppendLine()
            .AppendLine("Repository: repository root")
            .AppendLine()
            .AppendLine("This static receipt verifies source topology and compiled contracts. It does not claim operator visual or gameplay acceptance.");
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static void WriteConsumerMatrixReceipt(string path, string dll,
        string hash)
    {
        var text = Header("B15 Technological Knowledge Consumer Matrix", dll,
                hash)
            .AppendLine("This matrix records the execution paths verified in "
                + "the current B15 source. Domain/competency cells without a "
                + "listed consumer remain inspectable model capacity; they "
                + "are not claimed as shipped behavioral breadth.")
            .AppendLine()
            .AppendLine("| Authored capability | Direct consumer | Execution boundary |")
            .AppendLine("|---|---|---|")
            .AppendLine("| Understand exact research | Research availability, cost, progress, speed, and research presentation | Native completion remains factual; CA replaces authored-faction capability projection |")
            .AppendLine("| Construct buildables | Build visibility, construction work selection, and active construction jobs | `ForBuildable` at selection and execution |")
            .AppendLine("| Operate recipes | Recipe availability, bill work selection, and active bill jobs | `ForRecipe` at selection and execution |")
            .AppendLine("| Operate agriculture | Plant selection, sowing work selection, and active sowing jobs | `ForPlant` at selection and execution |")
            .AppendLine("| Operate medicine | Tending work selection and active tending jobs | `ForMedicalCare` at selection and execution |")
            .AppendLine("| Maintain buildables | Repair and breakdown work selection and active maintenance jobs | `ForMaintenance` at selection and execution |")
            .AppendLine("| Operate weapons | Availability of an equipped weapon verb | `ForWeapon` during weapon use |")
            .AppendLine("| Establish habitat functions | Regional placement potential and confirmation of represented programs, labor, materials, and access | `ForHabitat`; environment remains a separate requirement |")
            .AppendLine("| Realize starting structures | Frontier, morphology, settlement-program, and security construction | `ForBuildable` through canonical faction knowledge before spawn |")
            .AppendLine("| Realize milestone items | Exact weapon manufacture/use, medical-supply manufacture/use, and light construction/maintenance | Exact candidate definitions pass the common translators before spawn |")
            .AppendLine("| Autonomous spatial work | Home and furnishing plan selection | Exact `ForBuildable` requirements before designation |")
            .AppendLine()
            .AppendLine("The nine-domain by four-competency schema is broader "
                + "than this table by design. Future consumers must enter "
                + "through the same translator and receive their own receipt; "
                + "an unused cell is not implementation evidence.")
            .AppendLine()
            .AppendLine("This source and executable matrix does not claim "
                + "operator visual or gameplay acceptance.");
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static void WriteNativeAuthorityReceipt(string path, string dll,
        string hash, IReadOnlyList<NativeTechRead> reads)
    {
        var text = Header("B15 Native Technology Authority Census", dll,
                hash)
            .AppendLine("This census records every direct faction-template "
                + "`techLevel` read matched in the decompiled RimWorld source. "
                + "It distinguishes CA-owned runtime capability from native "
                + "template and world compatibility; it does not claim that "
                + "CA replaces every native use of `FactionDef.techLevel`.")
            .AppendLine()
            .AppendLine("Direct reads classified: " + reads.Count)
            .AppendLine()
            .AppendLine("| Decompiled source | Line | Authority boundary | Native expression |")
            .AppendLine("|---|---:|---|---|");
        foreach (NativeTechRead read in reads)
        {
            string source = read.Text.Replace("|", "\\|")
                .Replace("`", "'");
            string classification = read.Classification.Replace("|", "\\|");
            text.AppendLine("| `" + read.File + "` | " + read.Line + " | "
                + classification + " | `" + source + "` |");
        }
        text.AppendLine()
            .AppendLine("CA-authored settlement structures are realized by "
                + "the frontier, morphology, settlement-program, and security "
                + "modules through exact canonical knowledge requirements. "
                + "The only CA call into native BaseGen in the regional world "
                + "path is `pawnGroup`, which materializes population rather "
                + "than choosing the settlement's structural capability.")
            .AppendLine()
            .AppendLine("This static/decompiled census does not claim operator "
                + "visual or gameplay acceptance.");
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static StringBuilder Header(string title, string dll, string hash)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        return new StringBuilder().AppendLine("# " + title).AppendLine()
            .AppendLine("Generated: " + now.ToUniversalTime().ToString(
                "yyyy-MM-dd HH:mm:ss 'UTC'") + " / "
                + now.ToString("yyyy-MM-dd HH:mm:ss zzz"))
            .AppendLine()
            .AppendLine("Assembly: `" + PortableAssemblyPath(dll) + "`")
            .AppendLine("SHA-256: `" + hash + "`")
            .AppendLine();
    }

    private static void LoadManagedAssemblies(string managed)
    {
        foreach (string file in new[]
        {
            "netstandard.dll", "UnityEngine.CoreModule.dll",
            "UnityEngine.IMGUIModule.dll", "UnityEngine.TextRenderingModule.dll",
            "Assembly-CSharp-firstpass.dll", "Assembly-CSharp.dll"
        }) Assembly.LoadFrom(Path.Combine(managed, file));
    }

    private static Type RequiredGameType(string name)
    {
        Type exact = AppDomain.CurrentDomain.GetAssemblies()
            .Select(item => item.GetType(name, false)).FirstOrDefault(item =>
                item != null);
        if (exact != null) return exact;
        string simpleName = name.Contains('.')
            ? name[(name.LastIndexOf('.') + 1)..] : name;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type match = assembly.GetTypes().FirstOrDefault(item =>
                    item.Name == simpleName);
                if (match != null) return match;
            }
            catch (ReflectionTypeLoadException exception)
            {
                Type match = exception.Types.Where(item => item != null)
                    .FirstOrDefault(item => item!.Name == simpleName);
                if (match != null) return match;
            }
        }
        throw new TypeLoadException(name);
    }

    private static Type RequiredType(Assembly assembly, string name) =>
        assembly.GetType(name, true)!;

    private static MethodInfo Method(Type type, string name, int parameterCount)
    {
        return type.GetMethods(All).Single(method => method.Name == name
            && method.GetParameters().Length == parameterCount);
    }

    private static PropertyInfo RequiredProperty(Type type, string name) =>
        type.GetProperty(name, All)
        ?? throw new MissingMemberException(type.FullName, name);

    private static FieldInfo RequiredField(Type type, string name)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            FieldInfo found = current.GetField(name, All);
            if (found != null) return found;
        }
        throw new MissingMemberException(type.FullName, name);
    }

    private static string StringProperty(object value, string name) =>
        value == null ? "" : RequiredProperty(value.GetType(), name)
            .GetValue(value) as string ?? "";

    private static string StringField(object value, string name) =>
        RequiredField(value.GetType(), name).GetValue(value) as string ?? "";

    private static int ListCount(object value, string name) =>
        ((ICollection)RequiredField(value.GetType(), name)
            .GetValue(value)!).Count;

    private static string TripleHash(object culture, object political,
        object knowledge) => ObjectHash(culture) + ":" + ObjectHash(political)
        + ":" + ObjectHash(knowledge);

    private static string ObjectHash(object value)
    {
        string canonical = Canonical(value, 0);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            canonical))).Substring(0, 16);
    }

    private static string Canonical(object value, int depth)
    {
        if (value == null) return "null";
        if (depth > 12) return "<depth>";
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

    private static string Read(string repo, string relative) =>
        File.ReadAllText(Path.Combine(repo,
            relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string PortableAssemblyPath(string dll) =>
        string.Equals(Path.GetFileName(dll), "ColonistAwareness.dll",
            StringComparison.OrdinalIgnoreCase)
            ? "Assemblies/ColonistAwareness.dll"
            : Path.GetFileName(dll);

    private static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }
}
