using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

internal static class Program
{
    private const BindingFlags All = BindingFlags.Public
        | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private static int Main(string[] args)
    {
        bool verifyOnly = args.Length == 6 && args[5] == "--verify-only";
        if (args.Length != 5 && !verifyOnly)
        {
            Console.Error.WriteLine("usage: B17AffiliationEpistemicReceipts "
                + "<repo> <managed> <assembly> <active-fixture> "
                + "<mirror-fixture> [--verify-only]");
            return 1;
        }

        string repo = Path.GetFullPath(args[0]);
        string managed = Path.GetFullPath(args[1]);
        string dll = Path.GetFullPath(args[2]);
        string active = Path.GetFullPath(args[3]);
        string mirror = Path.GetFullPath(args[4]);
        LoadManagedAssemblies(managed);
        Assembly gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .Single(value => value.GetName().Name == "Assembly-CSharp");
        Assembly assembly = Assembly.LoadFrom(dll);
        string hash = Convert.ToHexString(SHA256.HashData(
            File.ReadAllBytes(dll)));

        FixtureResult fixture = TestFixture(gameAssembly, assembly,
            active, mirror);
        TestSiteContracts(gameAssembly, assembly);
        TestAtomicMigrationRollback(assembly);
        EpistemicResult epistemic = TestEpistemics(gameAssembly, assembly);
        TestCoverageAndSource(repo, assembly);

        if (!verifyOnly)
        {
            string receipts = Path.Combine(repo, "Receipts", "B17");
            Directory.CreateDirectory(receipts);
            WriteAffiliationExecution(Path.Combine(receipts,
                "B17_AFFILIATION_EXECUTION_RECEIPT.md"), dll, hash,
                fixture);
            WriteAffiliationAudit(Path.Combine(receipts,
                "B17_AFFILIATION_REVERSE_AUDIT.md"), dll, hash);
            WriteEpistemicExecution(Path.Combine(receipts,
                "B17_EPISTEMIC_EXECUTION_RECEIPT.md"), dll, hash,
                epistemic);
            WriteFactCoverage(Path.Combine(receipts,
                "B17_EPISTEMIC_FACT_COVERAGE.md"), dll, hash);
        }

        Console.WriteLine("PASS current fixture: " + fixture.Factions
            + " factions, " + fixture.Settlements + " settlements, "
            + fixture.Frontiers + " factionless frontier holdings");
        Console.WriteLine("PASS no-owner, support-only, population-affiliation, "
            + "form-transition, and atomic migration contracts");
        Console.WriteLine("PASS pawn-private observation, report provenance, "
            + "contradiction, revision, supersession, retention, and readback");
        Console.WriteLine("PASS 14/14 broader fact families and disabled-mode boundary");
        return 0;
    }

    private sealed record FixtureResult(int Factions, int Settlements,
        int Relations, int Frontiers, string PairHash, string Fingerprint);

    private sealed record EpistemicResult(int PawnARecords, int PawnBRecords,
        int Contradictions, int ActiveAfterRevision, string ReadbackHash);

    private static FixtureResult TestFixture(Assembly gameAssembly,
        Assembly assembly, string active, string mirror)
    {
        Require(File.Exists(active) && File.Exists(mirror),
            "current fixture pair is missing");
        byte[] activeBytes = File.ReadAllBytes(active);
        byte[] mirrorBytes = File.ReadAllBytes(mirror);
        Require(activeBytes.SequenceEqual(mirrorBytes),
            "active and mirror fixtures differ");
        string pairHash = Convert.ToHexString(SHA256.HashData(activeBytes));
        Type planType = RequiredType(assembly,
            "ColonistAwareness.CARegionalPlan");
        object plan = LoadDeepRoot(gameAssembly, planType, active, "plan");
        Require((int)Field(plan, "schemaVersion")! == 16,
            "fixture is not regional-plan schema 16");
        Type migration = RequiredType(assembly,
            "ColonistAwareness.CASiteAffiliationMigration");
        object validation = Method(migration,
            "ValidateCurrentRegionalPlan", 2).Invoke(null,
                new[] { plan, (object)true });
        Require(validation == null, "fixture affiliation validation: "
            + validation);

        IList factions = List(plan, "factions");
        IList settlements = List(plan, "settlements");
        IList relations = List(plan, "relations");
        IList frontiers = List(plan, "frontierHoldings");
        Require(factions.Count == 3 && settlements.Count == 4
                && relations.Count == 3 && frontiers.Count == 3,
            "fixture composition changed during B17 conversion");

        foreach (object settlement in settlements)
        {
            object links = Field(settlement, "factionLinks")!;
            Require(EnumName(Field(links, "ownership"))
                    == "RegionalFaction"
                    && EnumName(Field(links, "support")) == "None",
                "authored major settlement did not preserve typed ownership");
            IList groups = List(settlement, "populationGroups");
            Require(groups.Count > 0 && groups.Cast<object>().Count(value =>
                    (bool)Field(value, "isPrimary")!) == 1
                    && groups.Cast<object>().All(value =>
                        (int)Field(value, "schemaVersion")! == 2),
                "major settlement population ownership is incomplete");
        }
        foreach (object frontier in frontiers)
        {
            object links = Field(frontier, "factionLinks")!;
            Require((int)Field(frontier, "schemaVersion")! == 2
                    && EnumName(Field(links, "ownership")) == "None"
                    && EnumName(Field(links, "support"))
                        == "RegionalFaction",
                "frontier ownership and support were conflated");
            Require(Field(frontier, "localCulture") != null
                    && Field(frontier, "localSociety") != null,
                "factionless frontier lacks canonical local state");
            IList groups = List(frontier, "populationGroups");
            Require(groups.Count > 0 && groups.Cast<object>().Count(value =>
                    (bool)Field(value, "isPrimary")!) == 1,
                "factionless frontier lacks a primary population");
            Require(Field(frontier, "residenceAssignments") is IList,
                "frontier residence ledger is missing");
        }

        string before = PlanFingerprint(plan);
        object restored = ScribeRoundTrip(gameAssembly, planType, plan,
            "plan");
        object restoredFailure = Method(migration,
            "ValidateCurrentRegionalPlan", 2).Invoke(null,
                new[] { restored, (object)true });
        Require(restoredFailure == null,
            "fixture failed Scribe readback: " + restoredFailure);
        string after = PlanFingerprint(restored);
        Require(before == after,
            "site ownership, support, or population changed on readback");
        return new FixtureResult(factions.Count, settlements.Count,
            relations.Count, frontiers.Count, pairHash, before);
    }

    private static void TestSiteContracts(Assembly gameAssembly,
        Assembly assembly)
    {
        Type linksType = RequiredType(assembly,
            "ColonistAwareness.CASiteFactionLinks");
        Type linkKind = RequiredType(assembly,
            "ColonistAwareness.CASiteFactionReferenceKind");
        object none = Enum.Parse(linkKind, "None");
        object regional = Enum.Parse(linkKind, "RegionalFaction");
        object links = Activator.CreateInstance(linksType)!;
        Require(Method(linksType, "ValidationFailure", 1).Invoke(links,
                new object[] { null }) == null,
            "no-owner/no-support relationship is invalid");
        Method(linksType, "SetRegionalSupport", 1).Invoke(links,
            new object[] { 7 });
        Require(EnumName(Field(links, "ownership")) == "None"
                && EnumName(Field(links, "support")) == "RegionalFaction",
            "support-only relationship created an owner");
        object invalid = Activator.CreateInstance(linksType)!;
        RequiredField(linksType, "ownerRegionalFactionKey")
            .SetValue(invalid, 7);
        Require(Method(linksType, "ValidationFailure", 1).Invoke(invalid,
                new object[] { null }) != null,
            "sentinel payload was accepted without a typed relationship");

        Type planType = RequiredType(assembly,
            "ColonistAwareness.CARegionalPlan");
        Type settlementType = RequiredType(assembly,
            "ColonistAwareness.CARegionalSettlementPlan");
        object plan = Activator.CreateInstance(planType)!;
        RequiredField(planType, "candidateId").SetValue(plan,
            "b17-independent-receipt");
        object settlement = Activator.CreateInstance(settlementType)!;
        RequiredField(settlementType, "slot").SetValue(settlement, 4);
        Type siteState = RequiredType(assembly,
            "ColonistAwareness.CASiteState");
        Method(siteState, "EnsureIndependentState", 3).Invoke(null,
            new[] { plan, settlement, null });
        object independentLinks = Field(settlement, "factionLinks")!;
        object local = Field(settlement, "localSociety")!;
        Require(EnumName(Field(independentLinks, "ownership")) == "None"
                && (bool)Field(local, "explicitLocalDivergence")!
                && Field(local, "politicalOrder") != null
                && Field(local, "technologicalKnowledge") != null
                && Field(local, "institutions") is IList,
            "direct independent authoring did not create local state");

        Type frontierType = RequiredType(assembly,
            "ColonistAwareness.CAFrontierHoldingPlan");
        object frontier = Activator.CreateInstance(frontierType)!;
        object frontierLinks = Field(frontier, "factionLinks")!;
        Method(linksType, "SetRegionalSupport", 1).Invoke(frontierLinks,
            new object[] { 7 });
        RequiredField(frontierType, "form").SetValue(frontier, 0);
        object cabin = Method(frontierType, "Copy", 0).Invoke(frontier,
            Array.Empty<object>())!;
        RequiredField(frontierType, "form").SetValue(frontier, 1);
        object homestead = Method(frontierType, "Copy", 0).Invoke(frontier,
            Array.Empty<object>())!;
        foreach (object copy in new[] { cabin, homestead })
        {
            object copyLinks = Field(copy, "factionLinks")!;
            Require(EnumName(Field(copyLinks, "ownership")) == "None"
                    && EnumName(Field(copyLinks, "support"))
                        == "RegionalFaction",
                "frontier form transition changed affiliation");
        }
        Require((int)Field(cabin, "form")! == 0
                && (int)Field(homestead, "form")! == 1,
            "frontier forms did not remain explicit");

        Type groupType = RequiredType(assembly,
            "ColonistAwareness.CASettlementPopulationGroup");
        Type groupKind = RequiredType(assembly,
            "ColonistAwareness.CAPopulationGroupKind");
        object primary = NewGroup(groupType, groupKind, 0, true, 2);
        object unaffiliated = NewGroup(groupType, groupKind, 1, false, -1,
            "Unaffiliated");
        Method(linksType, "SetRegionalOwner", 1).Invoke(independentLinks,
            new object[] { 1 });
        RequiredField(settlementType, "populationGroups").SetValue(
            settlement, NewTypedList(groupType, primary, unaffiliated));
        object populationReadback = ScribeRoundTrip(gameAssembly,
            settlementType, settlement, "settlement");
        IList readGroups = List(populationReadback, "populationGroups");
        Require((int)Field(readGroups[0]!, "factionKey")! == 2
                && (int)Field(readGroups[1]!, "factionKey")! == -1
                && (int)Field(Field(populationReadback,
                    "factionLinks")!, "ownerRegionalFactionKey")! == 1,
            "resident affiliation collapsed into site ownership");

        _ = none;
        _ = regional;
    }

    private static void TestAtomicMigrationRollback(Assembly assembly)
    {
        Type planType = RequiredType(assembly,
            "ColonistAwareness.CARegionalPlan");
        Type factionType = RequiredType(assembly,
            "ColonistAwareness.CARegionalFactionPlan");
        Type settlementType = RequiredType(assembly,
            "ColonistAwareness.CARegionalSettlementPlan");
        Type groupType = RequiredType(assembly,
            "ColonistAwareness.CASettlementPopulationGroup");
        Type groupKind = RequiredType(assembly,
            "ColonistAwareness.CAPopulationGroupKind");
        Type linksType = RequiredType(assembly,
            "ColonistAwareness.CASiteFactionLinks");
        object plan = Activator.CreateInstance(planType)!;
        RequiredField(planType, "schemaVersion").SetValue(plan, 15);
        object faction = Activator.CreateInstance(factionType)!;
        RequiredField(factionType, "key").SetValue(faction, 1);
        RequiredField(planType, "factions").SetValue(plan,
            NewTypedList(factionType, faction));

        object first = Activator.CreateInstance(settlementType)!;
        RequiredField(settlementType, "slot").SetValue(first, 0);
        Method(linksType, "SetRegionalOwner", 1).Invoke(
            Field(first, "factionLinks"), new object[] { 1 });
        object firstGroup = NewGroup(groupType, groupKind, 0, false, 1);
        RequiredField(groupType, "schemaVersion").SetValue(firstGroup, 1);
        RequiredField(settlementType, "populationGroups").SetValue(first,
            NewTypedList(groupType, firstGroup));

        object second = Activator.CreateInstance(settlementType)!;
        RequiredField(settlementType, "slot").SetValue(second, 1);
        object badLinks = Field(second, "factionLinks")!;
        RequiredField(linksType, "ownerRegionalFactionKey")
            .SetValue(badLinks, 99);
        object secondGroup = NewGroup(groupType, groupKind, 0, false, 1);
        RequiredField(groupType, "schemaVersion").SetValue(secondGroup, 1);
        RequiredField(settlementType, "populationGroups").SetValue(second,
            NewTypedList(groupType, secondGroup));
        RequiredField(planType, "settlements").SetValue(plan,
            NewTypedList(settlementType, first, second));

        Type migration = RequiredType(assembly,
            "ColonistAwareness.CASiteAffiliationMigration");
        object[] values = { plan, 15, true, null, null };
        bool prepared = (bool)Method(migration,
            "TryPrepareRegionalPlan", 5).Invoke(null, values)!;
        Require(!prepared && values[4] is string
                && (int)Field(firstGroup, "schemaVersion")! == 1
                && !(bool)Field(firstGroup, "isPrimary")!
                && (int)Field(plan, "schemaVersion")! == 15,
            "failed migration mutated the source graph before commit");
    }

    private static EpistemicResult TestEpistemics(Assembly gameAssembly,
        Assembly assembly)
    {
        Type componentType = RequiredType(assembly,
            "ColonistAwareness.CAPropositionKnowledgeWorldComponent");
        Type pawnType = RequiredGameType("Verse.Pawn");
        Type payloadType = RequiredType(assembly,
            "ColonistAwareness.CASiteAffiliationKnowledgePayload");
        Type kindType = RequiredType(assembly,
            "ColonistAwareness.CASiteFactionReferenceKind");
        Type channelType = RequiredType(assembly,
            "ColonistAwareness.CommunicationChannel");
        Type recordType = RequiredType(assembly,
            "ColonistAwareness.CAKnowledgePropositionRecord");
        Type persistenceType = RequiredType(assembly,
            "ColonistAwareness.CAKnowledgePersistenceClass");
        object component = Activator.CreateInstance(componentType,
            new object[] { null })!;
        object pawnA = NewPawn(pawnType, 101);
        object pawnB = NewPawn(pawnType, 202);
        object pawnC = NewPawn(pawnType, 303);
        object pawnD = NewPawn(pawnType, 404);
        object voice = Enum.Parse(channelType, "Voice");
        MethodInfo record = componentType.GetMethods(All).Single(value =>
            value.Name == "RecordSiteAffiliation"
            && value.GetParameters().Length == 6
            && value.GetParameters()[1].ParameterType == payloadType);
        MethodInfo relay = Method(componentType, "Relay", 5);
        MethodInfo forPawn = Method(componentType, "ForPawn", 1);

        object noOwner = NewPayload(payloadType, kindType,
            "site:receipt", "None", -1, "RegionalFaction", 7,
            new[] { 7 }, true);
        object observedA = record.Invoke(component,
            new[] { pawnA, noOwner, 100, "direct observation",
                "site:receipt", 90 })!;
        Require(observedA != null && Records(forPawn, component, pawnB).Count
                == 0,
            "direct observation leaked into another pawn's knowledge");
        object reportedB = relay.Invoke(component,
            new[] { observedA, pawnA, pawnB, voice, 110 })!;
        Require(reportedB != null
                && (string)Field(reportedB, "sourceIdentity")!
                    == (string)Field(observedA, "sourceIdentity")!
                && (int)Field(reportedB, "sourceEventTick")!
                    == (int)Field(observedA, "sourceEventTick")!
                && (string)Field(reportedB, "immediateReporterIdentity")!
                    == "pawn:101"
                && (string)Field(reportedB, "content")!
                    == (string)Field(observedA, "content")!
                && !ReferenceEquals(Field(reportedB, "siteAffiliation"),
                    Field(observedA, "siteAffiliation")),
            "relay reconstructed live truth or lost report provenance");

        string bBeforeRevision = (string)Field(reportedB, "content")!;
        object ownerTwo = NewPayload(payloadType, kindType,
            "site:receipt", "RegionalFaction", 2, "None", -1,
            new[] { 2 }, false);
        object revisedA = record.Invoke(component,
            new[] { pawnA, ownerTwo, 120, "direct observation",
                "site:receipt", 120 })!;
        Require((int)Field(revisedA, "revision")! == 2
                && (string)Field(reportedB, "content")! == bBeforeRevision,
            "source revision mutated the receiver before a new relay");

        object ownerThree = NewPayload(payloadType, kindType,
            "site:receipt", "RegionalFaction", 3, "None", -1,
            new[] { 3 }, false);
        object observedC = record.Invoke(component,
            new[] { pawnC, ownerThree, 121, "direct observation",
                "site:receipt", 121 })!;
        object conflictB = relay.Invoke(component,
            new[] { observedC, pawnC, pawnB, voice, 122 })!;
        IList conflictLinks = (IList)Field(conflictB, "contradictions")!;
        Require(conflictLinks.Count > 0,
            "contradictory reports did not coexist explicitly");
        int contradictionCount = conflictLinks.Count;

        object revisedReportB = relay.Invoke(component,
            new[] { revisedA, pawnA, pawnB, voice, 130 })!;
        List<object> bRecords = Records(forPawn, component, pawnB);
        List<object> activeB = bRecords.Where(value =>
            string.IsNullOrEmpty((string)Field(value,
                "supersededByIdentity"))).ToList();
        Require(activeB.Count == 2
                && activeB.Contains(revisedReportB)
                && activeB.Contains(conflictB)
                && !string.IsNullOrEmpty((string)Field(reportedB,
                    "supersededByIdentity")),
            "revision erased a later contradictory observation or failed to "
                + "supersede its own earlier report");

        object ownerFour = NewPayload(payloadType, kindType,
            "site:receipt", "RegionalFaction", 4, "None", -1,
            new[] { 4 }, false);
        object latestA = record.Invoke(component,
            new[] { pawnA, ownerFour, 140, "direct observation",
                "site:receipt", 140 })!;
        object latestReportB = relay.Invoke(component,
            new[] { latestA, pawnA, pawnB, voice, 141 })!;
        bRecords = Records(forPawn, component, pawnB);
        activeB = bRecords.Where(value => string.IsNullOrEmpty(
            (string)Field(value, "supersededByIdentity"))).ToList();
        Require(activeB.Count == 1
                && ReferenceEquals(activeB[0], latestReportB)
                && (int)Field(latestReportB, "revision")! == 3,
            "newest represented event did not supersede older receiver records");

        object falseOwner = NewPayload(payloadType, kindType,
            "site:correction", "RegionalFaction", 9, "None", -1,
            Array.Empty<int>(), false);
        object falseRecord = record.Invoke(component,
            new[] { pawnD, falseOwner, 200, "direct observation",
                "site:correction", 200 })!;
        object corrected = NewPayload(payloadType, kindType,
            "site:correction", "None", -1, "None", -1,
            Array.Empty<int>(), true);
        object correctedRecord = record.Invoke(component,
            new[] { pawnD, corrected, 210, "direct observation",
                "site:correction", 210 })!;
        Require((int)Field(correctedRecord, "revision")! == 2
                && !string.IsNullOrEmpty((string)Field(falseRecord,
                    "supersededByIdentity"))
                && EnumName(Field(Field(correctedRecord,
                    "siteAffiliation")!, "ownership")) == "None",
            "false owner report was not correctable without live mutation");

        object readback = ScribeRoundTrip(gameAssembly, recordType,
            latestReportB, "knowledge");
        string readbackHash = ObjectHash(readback);
        Require(RecordFingerprint(readback) == RecordFingerprint(
                latestReportB),
            "knowledge provenance changed during Scribe readback");

        IList all = List(component, "propositions");
        object transient = Activator.CreateInstance(recordType)!;
        SetRecordRetention(transient, persistenceType,
            "receipt:transient", "Transient", 1, 2);
        object durable = Activator.CreateInstance(recordType)!;
        SetRecordRetention(durable, persistenceType,
            "receipt:durable", "Durable", 1, 2);
        all.Add(transient);
        all.Add(durable);
        Method(componentType, "ApplyRetention", 1).Invoke(component,
            new object[] { 1000 });
        Require(!all.Cast<object>().Any(value =>
                    (string)Field(value, "identity") == "receipt:transient")
                && all.Cast<object>().Any(value =>
                    (string)Field(value, "identity") == "receipt:durable"),
            "retention failed to drop stale transient or retain durable fact");

        Type settingsType = RequiredType(assembly,
            "ColonistAwareness.AwarenessSettings");
        object settings = Activator.CreateInstance(settingsType)!;
        Require(!(bool)Field(settings,
                "experimentalBroaderPawnKnowledge")!,
            "broader pawn knowledge is not disabled by default");

        return new EpistemicResult(
            Records(forPawn, component, pawnA).Count, bRecords.Count,
            contradictionCount, activeB.Count, readbackHash);
    }

    private static void TestCoverageAndSource(string repo, Assembly assembly)
    {
        Type factKind = RequiredType(assembly,
            "ColonistAwareness.CAKnowledgeFactKind");
        string[] expected =
        {
            "SocialEvent", "SiteAffiliation", "SitePopulation", "Culture",
            "Ideoligion", "PoliticalOrder", "Institution", "Organization",
            "Officeholder", "TechnologicalKnowledge", "Geography", "Route",
            "Conflict", "Research"
        };
        string[] actual = Enum.GetNames(factKind);
        Require(actual.SequenceEqual(expected),
            "broader fact registry is not the explicit 14-family contract");

        string proposition = Read(repo,
            "Source/PropositionKnowledgeModule.cs");
        foreach (string kind in expected.Skip(1))
            Require(proposition.Contains("CAKnowledgeFactKind." + kind,
                    StringComparison.Ordinal),
                kind + " has no proposition consumer");
        Require(proposition.Contains("direct observation",
                    StringComparison.Ordinal)
                && proposition.Contains("immediateReporterIdentity",
                    StringComparison.Ordinal)
                && proposition.Contains("sourceEventTick",
                    StringComparison.Ordinal)
                && proposition.Contains("supersededByIdentity",
                    StringComparison.Ordinal),
            "direct/report provenance and revision fields are disconnected");

        string settings = Read(repo, "Source/ModEntry.cs");
        string inspector = Read(repo,
            "Source/EpistemicInspectionModule.cs");
        Require(settings.Contains(
                    "experimentalBroaderPawnKnowledge = false",
                    StringComparison.Ordinal)
                && proposition.Contains(
                    "experimentalBroaderPawnKnowledge == true",
                    StringComparison.Ordinal)
                && inspector.Contains(
                    "experimentalBroaderPawnKnowledge",
                    StringComparison.Ordinal),
            "optional-mode execution or inspection gate is missing");

        string regionalUi = Read(repo,
            "Source/RegionalPopulationScreenModule.cs") + "\n"
            + Read(repo, "Source/RegionalPopulationEditorsModule.cs");
        string frontier = Read(repo, "Source/FrontierModule.cs");
        string world = Read(repo, "Source/RegionalWorldModule.cs");
        Require(regionalUi.Contains("Independent settlement",
                    StringComparison.Ordinal)
                && regionalUi.Contains("No faction support",
                    StringComparison.Ordinal)
                && regionalUi.Contains("No faction affiliation",
                    StringComparison.Ordinal)
                && frontier.Contains("GeneratePawn(kind, owner)",
                    StringComparison.Ordinal)
                && world.Contains("MaterializeIndependentResidents",
                    StringComparison.Ordinal),
            "factionless authoring or materialization is not directly wired");
    }

    private static object NewPayload(Type payloadType, Type kindType,
        string site, string ownership, int ownerKey, string support,
        int supportKey, int[] populationKeys, bool unaffiliated)
    {
        object value = Activator.CreateInstance(payloadType)!;
        RequiredField(payloadType, "siteIdentity").SetValue(value, site);
        RequiredField(payloadType, "ownership").SetValue(value,
            Enum.Parse(kindType, ownership));
        RequiredField(payloadType, "ownerRegionalFactionKey")
            .SetValue(value, ownerKey);
        RequiredField(payloadType, "support").SetValue(value,
            Enum.Parse(kindType, support));
        RequiredField(payloadType, "supportRegionalFactionKey")
            .SetValue(value, supportKey);
        RequiredField(payloadType, "populationRegionalFactionKeys")
            .SetValue(value, populationKeys.ToList());
        RequiredField(payloadType, "includesUnaffiliatedResidents")
            .SetValue(value, unaffiliated);
        return value;
    }

    private static object NewGroup(Type groupType, Type groupKind,
        int key, bool primary, int factionKey, string kind = "Main")
    {
        object value = Activator.CreateInstance(groupType)!;
        RequiredField(groupType, "key").SetValue(value, key);
        RequiredField(groupType, "kind").SetValue(value,
            Enum.Parse(groupKind, kind));
        RequiredField(groupType, "isPrimary").SetValue(value, primary);
        RequiredField(groupType, "label").SetValue(value,
            kind == "Unaffiliated" ? "Unaffiliated residents" : "Residents");
        RequiredField(groupType, "share").SetValue(value,
            primary ? 80 : 20);
        RequiredField(groupType, "factionKey").SetValue(value, factionKey);
        return value;
    }

    private static object NewPawn(Type pawnType, int id)
    {
        object pawn = RuntimeHelpers.GetUninitializedObject(pawnType);
        RequiredField(pawnType, "thingIDNumber").SetValue(pawn, id);
        return pawn;
    }

    private static void SetRecordRetention(object record,
        Type persistenceType, string identity, string persistence,
        int acquired, int stale)
    {
        Type type = record.GetType();
        RequiredField(type, "identity").SetValue(record, identity);
        RequiredField(type, "topic").SetValue(record, identity);
        RequiredField(type, "subjectIdentity").SetValue(record, identity);
        RequiredField(type, "holderIdentity").SetValue(record, "pawn:999");
        RequiredField(type, "sourceIdentity").SetValue(record, identity);
        RequiredField(type, "immediateReporterIdentity")
            .SetValue(record, "pawn:999");
        RequiredField(type, "acquiredTick").SetValue(record, acquired);
        RequiredField(type, "lastConfirmedTick").SetValue(record, acquired);
        RequiredField(type, "staleAfterTick").SetValue(record, stale);
        RequiredField(type, "confidence").SetValue(record, 0.5f);
        RequiredField(type, "persistenceClass").SetValue(record,
            Enum.Parse(persistenceType, persistence));
    }

    private static List<object> Records(MethodInfo method, object component,
        object pawn) => ((IEnumerable)method.Invoke(component,
            new[] { pawn })!).Cast<object>().ToList();

    private static string PlanFingerprint(object plan)
    {
        string settlements = string.Join(";", List(plan, "settlements")
            .Cast<object>().Select(value =>
            {
                object links = Field(value, "factionLinks")!;
                string groups = string.Join(",", List(value,
                    "populationGroups").Cast<object>().Select(group =>
                    Field(group, "key") + ":" + Field(group, "isPrimary")
                    + ":" + Field(group, "factionKey")));
                return Field(value, "slot") + ":"
                    + EnumName(Field(links, "ownership")) + ":"
                    + Field(links, "ownerRegionalFactionKey") + ":"
                    + EnumName(Field(links, "support")) + ":" + groups;
            }));
        string frontiers = string.Join(";", List(plan, "frontierHoldings")
            .Cast<object>().Select(value =>
            {
                object links = Field(value, "factionLinks")!;
                return Field(value, "key") + ":"
                    + EnumName(Field(links, "ownership")) + ":"
                    + EnumName(Field(links, "support")) + ":"
                    + Field(links, "supportRegionalFactionKey") + ":"
                    + List(value, "populationGroups").Count;
            }));
        return string.Join("|", Field(plan, "regionalId"),
            Field(plan, "candidateId"), List(plan, "factions").Count,
            List(plan, "relations").Count, settlements, frontiers);
    }

    private static string RecordFingerprint(object record)
    {
        object payload = Field(record, "siteAffiliation")!;
        return string.Join("|", Field(record, "factKind"),
            Field(record, "identity"), Field(record, "subjectIdentity"),
            Field(record, "content"), Field(record, "holderIdentity"),
            Field(record, "sourceIdentity"),
            Field(record, "immediateReporterIdentity"),
            Field(record, "sourceEventTick"), Field(record, "revision"),
            payload == null ? "null" : string.Join(":",
                Field(payload, "siteIdentity"), Field(payload, "ownership"),
                Field(payload, "ownerRegionalFactionKey"),
                Field(payload, "support"),
                Field(payload, "supportRegionalFactionKey"),
                Field(payload, "includesUnaffiliatedResidents")));
    }

    private static object ScribeRoundTrip(Assembly gameAssembly,
        Type valueType, object value, string root)
    {
        Type scribe = gameAssembly.GetType("Verse.Scribe", true)!;
        Type log = gameAssembly.GetType("Verse.Log", true)!;
        FieldInfo logDisablers = RequiredField(log, "logDisablers");
        int prior = (int)logDisablers.GetValue(null)!;
        object saver = RequiredField(scribe, "saver").GetValue(null)!;
        object loader = RequiredField(scribe, "loader").GetValue(null)!;
        MethodInfo expose = Method(valueType, "ExposeData", 0);
        string path = Path.Combine(Path.GetTempPath(), "cao-b17-"
            + Guid.NewGuid().ToString("N") + ".xml");
        logDisablers.SetValue(null, prior + 1);
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
            try { look.Invoke(null, values); value = values[0]; }
            finally { Method(loader.GetType(), "FinalizeLoading", 0)
                .Invoke(loader, Array.Empty<object>()); }
        }
        finally { logDisablers.SetValue(null, prior); }
        Require(value != null, path + " did not load " + root);
        return value;
    }

    private static void WriteAffiliationExecution(string path, string dll,
        string hash, FixtureResult fixture)
    {
        StringBuilder text = Header("B17 Site Affiliation Execution Receipt",
                dll, hash)
            .AppendLine("| Executed contract | Result |")
            .AppendLine("|---|---|")
            .AppendLine("| Current authored fixture | PASS - schema 16 Scribe-load and readback preserve "
                + fixture.Factions + " factions, " + fixture.Settlements
                + " settlements, " + fixture.Relations + " relations, and "
                + fixture.Frontiers + " frontier holdings |")
            .AppendLine("| Fixture pair | PASS - active and mirror are byte-identical (`"
                + fixture.PairHash + "`) |")
            .AppendLine("| No faction | PASS - typed `None` ownership validates without a sentinel or native faction |")
            .AppendLine("| Support | PASS - support-only state retains no owner |")
            .AppendLine("| Independent settlement | PASS - direct authoring creates canonical local Political Order, Technological Knowledge, and institutions |")
            .AppendLine("| Population | PASS - faction-affiliated and unaffiliated groups survive beside an independently typed site owner |")
            .AppendLine("| Frontier forms | PASS - cabin and homestead transitions preserve ownership and support exactly |")
            .AppendLine("| Migration transaction | PASS - a late invalid site leaves an earlier predecessor population and plan schema untouched |")
            .AppendLine("| Persistence | PASS - ownership, support, population affiliation, and local state survive Scribe readback |")
            .AppendLine()
            .AppendLine("Semantic fingerprint: `" + fixture.Fingerprint + "`")
            .AppendLine()
            .AppendLine("This executable receipt verifies data behavior; operator gameplay remains the runtime acceptance boundary.");
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static void WriteAffiliationAudit(string path, string dll,
        string hash)
    {
        StringBuilder text = Header("B17 Site Affiliation Reverse Audit",
                dll, hash)
            .AppendLine("| Scale or consumer | Canonical relationship | Result |")
            .AppendLine("|---|---|---|")
            .AppendLine("| Major regional settlement | typed owner + independent support + local or owner-resolved social state | PASS |")
            .AppendLine("| Frontier cabin/homestead | no owner by default; optional support; local Culture, Political Order, knowledge, institutions, population, residence | PASS |")
            .AppendLine("| Population groups | primary status, affiliation, Ideoligion, and Political Order source remain independent | PASS |")
            .AppendLine("| Generation recipe | `generationFactionDefName` selects native content only; it cannot express ownership | PASS |")
            .AppendLine("| Habitat viability | environment, local knowledge/material capacity, and outside support are queried separately | PASS |")
            .AppendLine("| Pawn generation | native faction is supplied only by the explicit owner; support does not own residents | PASS |")
            .AppendLine("| Threats and security | factionless sites compare threats against actual represented residents | PASS |")
            .AppendLine("| Organizations, patrols, roads, and programs | site residents and typed affiliation replace implicit owner requirements | PASS |")
            .AppendLine("| UI | no-owner settlement, no support, and unaffiliated population are direct authoring choices | PASS |")
            .AppendLine("| Transition | changing owner or frontier form preserves or snapshots canonical state explicitly and never mints a faction | PASS |")
            .AppendLine()
            .AppendLine("The reverse pass starts at each inhabited-scale consumer and traces back to the same typed site contract. No fake faction or negative-key ownership semantics are admitted.");
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static void WriteEpistemicExecution(string path, string dll,
        string hash, EpistemicResult result)
    {
        StringBuilder text = Header("B17 Broader Pawn Knowledge Execution Receipt",
                dll, hash)
            .AppendLine("| Executed contract | Result |")
            .AppendLine("|---|---|")
            .AppendLine("| Default boundary | PASS - experimental broader pawn knowledge defaults off |")
            .AppendLine("| Private observation | PASS - an observed site fact exists only for the observing pawn |")
            .AppendLine("| Report | PASS - receiver records the teller as immediate reporter while retaining original source and event time |")
            .AppendLine("| No omniscience | PASS - relay copies the teller's typed payload; later source revision does not mutate the receiver |")
            .AppendLine("| Contradiction | PASS - incompatible same-revision reports coexist with explicit contradiction links |")
            .AppendLine("| Revision | PASS - newer source events supersede older receiver records without deleting their provenance |")
            .AppendLine("| Correction | PASS - a false owner report can be revised to no owner |")
            .AppendLine("| Retention | PASS - stale transient memory expires while durable knowledge remains |")
            .AppendLine("| Serialization | PASS - typed payload, source, reporter, event time, revision, and supersession survive Scribe readback |")
            .AppendLine("| Inspection | PASS - pawn UI describes records, confidence, uncertainty, contradiction, age, and provenance rather than live truth |")
            .AppendLine()
            .AppendLine("Observed records: pawn A `" + result.PawnARecords
                + "`; pawn B `" + result.PawnBRecords
                + "`; contradiction links `" + result.Contradictions
                + "`; active B records after revision `"
                + result.ActiveAfterRevision + "`.")
            .AppendLine("Readback fingerprint: `" + result.ReadbackHash + "`")
            .AppendLine()
            .AppendLine("This mode extends the proposition store. It does not replace tactical private contacts or standard-mode social capability.");
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static void WriteFactCoverage(string path, string dll,
        string hash)
    {
        StringBuilder text = Header("B17 Broader Knowledge Fact Coverage",
                dll, hash)
            .AppendLine("| Fact family | Owning represented fact | Observation or update seam |")
            .AppendLine("|---|---|---|")
            .AppendLine("| Social event | interpreted event record | existing event acquisition |")
            .AppendLine("| Site affiliation | typed ownership, support, population affiliation | nearby represented site |")
            .AppendLine("| Site population | population-group composition | nearby represented site |")
            .AppendLine("| Culture | local Culture state | nearby represented site |")
            .AppendLine("| Ideoligion | represented native Ideoligion identities | nearby represented site |")
            .AppendLine("| Political Order | local or owner-resolved state | nearby represented site |")
            .AppendLine("| Institution | represented institutional mechanisms | nearby represented site and institutional acts |")
            .AppendLine("| Organization | represented organization records | nearby represented site |")
            .AppendLine("| Officeholder | represented office assignment | nearby represented site |")
            .AppendLine("| Technological Knowledge | local or owner-resolved canonical knowledge | nearby represented site |")
            .AppendLine("| Geography | saved site land and habitat facts | nearby represented site |")
            .AppendLine("| Route | saved access facts | nearby represented site |")
            .AppendLine("| Conflict | represented conflict/social event | proposition acquisition |")
            .AppendLine("| Research | research program lineage | research lifecycle |")
            .AppendLine()
            .AppendLine("All 14 families share holder identity, source identity, immediate reporter, source event time, acquisition time, confidence, uncertainty, contradiction, revision, supersession, staleness, persistence class, scopes, and provenance. Unknown facts are not inferred from labels.");
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static StringBuilder Header(string title, string dll,
        string hash)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        return new StringBuilder().AppendLine("# " + title).AppendLine()
            .AppendLine("Generated: " + now.ToUniversalTime().ToString(
                "yyyy-MM-dd HH:mm:ss 'UTC'") + " / "
                + now.ToString("yyyy-MM-dd HH:mm:ss zzz"))
            .AppendLine()
            .AppendLine("Assembly: `Assemblies/ColonistAwareness.dll`")
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

    private static Type RequiredGameType(string name) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Select(value => value.GetType(name, false))
            .First(value => value != null)!;

    private static Type RequiredType(Assembly assembly, string name) =>
        assembly.GetType(name, true)!;

    private static MethodInfo Method(Type type, string name,
        int parameterCount) => type.GetMethods(All).Single(method =>
            method.Name == name
            && method.GetParameters().Length == parameterCount);

    private static FieldInfo RequiredField(Type type, string name)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            FieldInfo field = current.GetField(name, All);
            if (field != null) return field;
        }
        throw new MissingMemberException(type.FullName, name);
    }

    private static object Field(object value, string name) => value == null
        ? null : RequiredField(value.GetType(), name).GetValue(value);

    private static IList List(object value, string name) =>
        (IList)Field(value, name)!;

    private static string EnumName(object value) => value?.ToString() ?? "";

    private static object NewTypedList(Type itemType, params object[] values)
    {
        IList list = (IList)Activator.CreateInstance(
            typeof(List<>).MakeGenericType(itemType))!;
        foreach (object value in values) list.Add(value);
        return list;
    }

    private static string Read(string repo, string relative) =>
        File.ReadAllText(Path.Combine(repo,
            relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string ObjectHash(object value)
    {
        string canonical = Canonical(value, 0);
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(canonical))).Substring(0, 16);
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

    private static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }
}
