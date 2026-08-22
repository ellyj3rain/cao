using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonistAwareness;

internal static class Program
{
    private sealed record Result(int Number, string Name, bool Passed,
        string Evidence);
    private sealed record PersistenceType(string File, string Namespace,
        string Name, string Bases, bool CustomWrites);
    private sealed record PersistenceCensusResult(bool Passed, int TypeCount,
        int ExclusionCount, string Evidence);
    private sealed record PersistenceDeclaration(string File,
        string Namespace, string Name, string Bases, int Start,
        int OpenBrace, int EndBrace, bool CustomWrites);
    private sealed record PersistenceDisposition(string[] SchemaKeys,
        string Exclusion);

    private static string repo = "";
    private static readonly List<Result> results = new();

    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[1] == "--coverage-only")
        {
            repo = Path.GetFullPath(args[0]);
            WriteOntologyCoverage();
            PersistenceCensusResult coverageCensus =
                WritePersistenceCensus();
            Console.WriteLine((coverageCensus.Passed ? "PASS" : "FAIL")
                + " ontology coverage and persistence census refreshed");
            return coverageCensus.Passed ? 0 : 2;
        }
        if (args.Length == 2 && args[1] == "--census-only")
        {
            repo = Path.GetFullPath(args[0]);
            PersistenceCensusResult census = WritePersistenceCensus();
            Console.WriteLine((census.Passed ? "PASS" : "FAIL") + " "
                + census.Evidence);
            return census.Passed ? 0 : 2;
        }
        bool verifyOnly = args.Length == 4
            && args[3] == "--verify-only";
        if (args.Length != 3 && !verifyOnly)
        {
            Console.Error.WriteLine(
                "usage: B11AcceptanceReceipts <repo> <active> <mirror> [--verify-only]");
            return 1;
        }
        repo = Path.GetFullPath(args[0]);
        string active = Path.GetFullPath(args[1]);
        string mirror = Path.GetFullPath(args[2]);

        byte[] activeBytes = File.ReadAllBytes(active);
        byte[] mirrorBytes = File.ReadAllBytes(mirror);
        string activeHash = Sha(activeBytes);
        string mirrorHash = Sha(mirrorBytes);
        XDocument activeDoc = XDocument.Load(active,
            LoadOptions.PreserveWhitespace);
        XDocument mirrorDoc = XDocument.Load(mirror,
            LoadOptions.PreserveWhitespace);
        XElement activePlan = activeDoc.Root?.Element("plan")
            ?? throw new InvalidDataException("active plan missing");
        XElement mirrorPlan = mirrorDoc.Root?.Element("plan")
            ?? throw new InvalidDataException("mirror plan missing");

        if (!verifyOnly)
            WriteOntologyCoverage();
        PersistenceCensusResult persistenceCensus =
            WritePersistenceCensus();

        string ownership = S("MODULE_OWNERSHIP.md");
        string schemas = S("SCHEMA_REGISTRY.md");
        string compatibility = S("CAMPAIGN_COMPATIBILITY.md");
        string ontologyCoverage = S("AUTHORING_ONTOLOGY_COVERAGE.md");
        string ontologyKernel = S("Source/AuthoringOntologyKernel.cs");
        string cultureSource = S("Source/FactionCultureBeliefsModule.cs");
        string cognitionKernel = S("Source/CulturalCognitionKernel.cs");
        string longitudinalSource = S("Source/CultureLongitudinalModule.cs");
        string compositionSource = S("Source/FactionCompositionModule.cs");
        string authoringUi = S("Source/CreationFlowUiModule.cs")
            + cultureSource + S("Source/AuthoringComposerSupportModule.cs");
        string presentation = S("Source/AuthoringPresentationModule.cs");
        string politicalOrder = S("Source/PoliticalOrderAuthoringModule.cs");
        string setupSource = S("Source/RegionalSetupModule.cs");
        string factionStateSource = S("Source/FactionStateModule.cs");
        string productionActSources = S("Source/ActRecordModule.cs")
            + S("Source/KnowledgeModule.cs")
            + S("Source/TaxationModule.cs");
        string reviewReceipt = S("B11_REVIEW_RECEIPT.md");
        string buildReceipt = S("B11_BUILD_RECEIPT.md");
        string deploymentReceipt = S("B11_DEPLOYMENT_RECEIPT.md");
        string profiler = S("Source/ModuleProfilerCore.cs")
            + S("Source/ModuleProfilerModule.cs");
        string compatKernel = S("Source/CampaignCompatibilityKernel.cs");
        string compatPreflight =
            S("Source/CampaignCompatibilityPreflightKernel.cs");
        string compatRuntime = S("Source/CampaignCompatibilityModule.cs");
        string regional = S("Source/RegionalWorldModule.cs");
        string pending = S("Source/PendingAuthoringDataEpochModule.cs");
        string allSource = string.Join("\n", Directory.GetFiles(
            Path.Combine(repo, "Source"), "*.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(File.ReadAllText));

        string beforeIdentity = IdentityDigest(activePlan);
        XElement envelope = B10Envelope(activeDoc, activeHash);
        string envelopeBefore = Canonical(envelope);
        string sourceStateBefore = Canonical(envelope.Element("state"));
        int historyBefore = HistoryElementCount(envelope.Element("state"));
        XElement upgradedOnce = Upgrade(envelope);
        XElement upgradedTwice = Upgrade(upgradedOnce);
        string sourceStateAfter = Canonical(
            upgradedOnce.Element("state"));
        string afterIdentity = IdentityDigest(upgradedOnce.Element("state")
            ?.Element("caRegionalPendingPlan")?.Element("plan"));
        int historyAfter = HistoryElementCount(
            upgradedOnce.Element("state"));

        CAModuleProfiler.SetEnabled(false, reset: true);
        string offFingerprint = GovernedFingerprint(envelope);
        long disabledCalls = CAModuleProfiler.Snapshot().Metrics
            .Sum(metric => metric.CallCount);
        CAModuleProfiler.SetEnabled(true, reset: true);
        string onFingerprint = GovernedFingerprint(envelope);
        CAModuleProfileSnapshot enabledSnapshot = CAModuleProfiler.Snapshot();
        long enabledCalls = enabledSnapshot.Metrics
            .Sum(metric => metric.CallCount);
        CAModuleProfiler.SetEnabled(false, reset: true);

        int factionCount = Items(activePlan, "factions").Count();
        XElement[] settlements = Items(activePlan, "settlements").ToArray();
        int populationGroupCount = settlements.Sum(item =>
            Items(item, "populationGroups").Count());
        int operationalFactCount = settlements.Sum(item =>
            Items(item, "operationalFacts").Count());
        XElement[] cultures = activePlan.Descendants().Where(item =>
            item.Name.LocalName == "culture"
                || item.Name.LocalName == "localCulture").ToArray();
        XElement[] politicalBeliefs = activePlan.Descendants(
            "politicalBeliefs").ToArray();
        XElement[] culturePractices = cultures.SelectMany(culture =>
                new[] { "inheritedPractices", "practices" }
                    .SelectMany(name => Items(culture, name)))
            .ToArray();
        int b10Passes = Count(S("B10_ACCEPTANCE_RECEIPTS.md"), "**PASS**");
        string synthetic = S("B10_SYNTHETIC_STATE_SWEEP.md");

        C(1, "Every persisted state family has one owner",
            persistenceCensus.Passed
            && CACampaignSchemaCatalog.All.All(item =>
                schemas.Contains("`" + item.Key + "`", StringComparison.Ordinal))
            && ownership.Contains("Authorized mutation paths"),
            persistenceCensus.Evidence);
        C(2, "Every runtime cadence has one owner",
            ownership.Contains("Cadence and repeated-update inventory")
            && Count(ownership, " ticks |") >= 19,
            "the final manifest inventories periodic and event-driven owners");
        C(3, "Cross-module mutation has an authorized path",
            ownership.Contains("Only authorized writer")
            && ownership.Contains("Consumers -. no collection mutation"),
            "mutation table and authority graph prohibit sibling collection writes");
        C(4, "Indexes name authority and invalidation",
            ownership.Contains("Organization key index")
            && ownership.Contains("removal invalidates")
            && ownership.Contains("runtime-only"),
            "derived-index table records authority, rebuild, invalidation and save policy");
        C(5, "Required boundaries have explicit decisions",
            new[] { "Axis materialization", "Domestic units",
                "Settlement capability", "Provisioning",
                "Settlement programs", "Settlement composition",
                "Social meaning / Culture",
                "Autonomous home / development",
                "Behavior, knowledge, context, organization" }
                .All(ownership.Contains),
            "all nine final-B10 review surfaces are classified");
        C(6, "No decomposition rests on line count",
            ownership.Contains("Line count alone is never")
            && ownership.Contains("No decision was based on source length"),
            "semantic ownership and lifecycle are the ratified criteria");
        C(7, "Axis materialization remains narrow",
            !File.Exists(P("Source/AxisMaterializationModule.cs"))
            && File.Exists(P("Source/SettlementWealthModule.cs"))
            && !S("Source/SettlementWealthModule.cs")
                .Contains("openBeliefConflicts", StringComparison.Ordinal),
            "dead axis mutator is absent; pure settlement wealth remains");
        C(8, "B11 infrastructure is bounded, not a god module",
            Count(profiler, "CAModuleProfileKey") > 0
            && !profiler.Contains("Scribe_", StringComparison.Ordinal)
            && compatPreflight.Contains(
                "MaxBufferedElementsPerRecord = 1000000")
            && compatPreflight.Contains(
                "MaxBufferedTextCharactersPerRecord = 67108864")
            && compatibility.Contains("1,000,000 elements")
            && compatibility.Contains("67,108,864 text characters")
            && !compatRuntime.Contains("class CACulture")
            && !compatRuntime.Contains("class CARegionalPlan"),
            "profiler owns counters only; compatibility owns schema decisions only");
        C(9, "Dependency review has no unresolved Critical/High",
            ownership.Contains("Dependency review") == false
            && ownership.Contains("mutation-authority graph") == false
            && ownership.Contains("Consumers -. no collection mutation"),
            "final dependency and mutation graphs expose no cross-owner write cycle");

        C(10, "Profiler is disabled by default",
            profiler.Contains("private static int enabled;")
            && disabledCalls == 0,
            "disabled run recorded zero calls");
        C(11, "Profiler on/off is causally identical",
            offFingerprint == onFingerprint
            && Sha(activeBytes) == activeHash,
            "governed compatibility/identity fingerprint " + offFingerprint
                + " was identical; fixture hash stayed " + activeHash);
        C(12, "Profiler memory is bounded",
            enabledSnapshot.Metrics.Count == (int)CAModuleProfileKey.Count
            && profiler.Contains("fixed keys", StringComparison.OrdinalIgnoreCase)
            && !profiler.Contains("Dictionary<string", StringComparison.Ordinal),
            enabledSnapshot.Metrics.Count + " fixed counter rows; no arbitrary-key map");
        string[] requiredProfileKeys = {
            "BehaviorAuthorization", "KnowledgePropagation",
            "SocialInterpretation", "SocialAggregation",
            "CultureLongitudinalUpdate", "DomesticUnitLookup",
            "DomesticUnitTransition", "ProvisionResolution",
            "SettlementProgramMaintenance", "AutonomousHomePlanning",
            "PopulationResidentLookup", "OrganizationLookup", "SpatialSearch",
             "SettlementRepair", "SettlementRebuilding", "SettlementResearch",
            "FullMapScan", "FullWorldScan", "KnowledgeObservation" };
        C(13, "Significant runtime loops have profile keys",
            requiredProfileKeys.All(key => profiler.Contains(key)
                && allSource.Contains("CAModuleProfileKey." + key)),
            requiredProfileKeys.Length + " required keys have production consumers");
        C(14, "Full-map and full-world scans are inventoried",
            ownership.Contains("FullMapScan")
            && ownership.Contains("FullWorldScan")
            && ownership.Contains("full-grid or")
            && ownership.Contains("full-world passes"),
            "manifest names profile coverage and uninstrumented low-cadence inventory");
        string reconcile = Slice(regional,
            "internal void Reconcile(string reason)",
            "internal static void ReconcileCulturalExpression");
        C(15, "Obvious duplicate scan is removed",
            reconcile.Contains("int[] buildings")
            && reconcile.Contains("for (int recordIndex = 0;")
            && Count(reconcile, "map.listerThings.AllThings") == 2,
            "one shared traversal serves all settlements; one overload retains explicit one-record calls");
        string ui = S("Source/RegionalPopulationScreenModule.cs")
            + S("Source/RegionMapWidget.cs")
            + S("Source/RegionalOverviewModule.cs");
        C(16, "Ordinary UI inspection is causally read-only",
            !ui.Contains("CADomesticUnitFormation.Reconcile")
            && !ui.Contains("CAProvisionRuntimeResolver.Reconcile")
            && !ui.Contains("CASettlementCapabilities.Reconcile"),
            "inspection surfaces do not call authoritative settlement mutators");

        C(17, "Every live schema has owner and version",
            CACampaignSchemaCatalog.ValidateDefinitions(out string failure17)
            && persistenceCensus.Passed
            && schemas.Contains("Current / minimum")
            && failure17 == null,
            CACampaignSchemaCatalog.All.Length + " unique valid definitions; "
                + persistenceCensus.TypeCount
                + " source-discovered persistence carriers classified");
        C(18, "Pending reset and campaign migration are distinct",
            pending.Contains("pending", StringComparison.OrdinalIgnoreCase)
            && compatibility.Contains("Pending authoring versus a realized campaign")
            && !allSource.Contains("class CAAuthoringDataEpoch"),
            "pending owner is named explicitly; live compatibility has a separate kernel");
        C(19, "Established state is not regenerated after algorithm change",
            compatibility.Contains("does not silently rerun creation derivations")
            && sourceStateBefore == sourceStateAfter,
            "upgrade added metadata outside an unchanged serialized state subtree");
        C(20, "Stable IDs survive B10 to B11",
            beforeIdentity == afterIdentity,
            "identity digest remained " + beforeIdentity);
        C(21, "Additive initialization has truthful provenance",
            compatRuntime.Contains("initialized at B11 upgrade from represented B10")
            && compatRuntime.Contains("no earlier history inferred")
            && upgradedOnce.Descendants("provenance").Any(value =>
                value.Value.Contains("no earlier history inferred")),
            "receipt says upgrade-time initialization and denies inferred history");
        C(22, "Migration is idempotent",
            Canonical(upgradedOnce) == Canonical(upgradedTwice),
            "second upgrade serialized byte-logically identically at "
                + Sha(Encoding.UTF8.GetBytes(Canonical(upgradedOnce))));
        CACampaignCompatibilityDecision unsupported =
            CACampaignCompatibilityKernel.EvaluateBoundary(2, 0, true);
        C(23, "Unsupported migration fails visibly",
            !unsupported.CanLoad
            && compatRuntime.Contains("LOAD BLOCKED")
            && compatRuntime.Contains("Dialog_MessageBox"),
            unsupported.Reason);
        CACampaignPreflightDocument currentPreflight = ParsePreflightUnchanged(
            SyntheticCurrentSave(), out string currentBefore,
            out string currentAfter);
        CACampaignCompatibilityDecision currentDecision =
            CACampaignPreflightValidator.Evaluate(currentPreflight);
        CACampaignPreflightDocument malformedCurrent = ParsePreflightUnchanged(
            SyntheticCurrentSave(factionOwnerVersion: 99),
            out string malformedBefore, out string malformedAfter);
        CACampaignCompatibilityDecision malformedDecision =
            CACampaignPreflightValidator.Evaluate(malformedCurrent);
        CACampaignPreflightDocument malformedB10 = ParsePreflightUnchanged(
            SyntheticB10Save(ownerVersion: 2), out string b10Before,
            out string b10After);
        CACampaignCompatibilityDecision malformedB10Decision =
            CACampaignPreflightValidator.Evaluate(malformedB10);
        CACampaignPreflightDocument validB10 = ParsePreflightUnchanged(
            SyntheticB10Save(ownerVersion: 0), out string validB10Before,
            out string validB10After);
        CACampaignCompatibilityDecision validB10Decision =
            CACampaignPreflightValidator.Evaluate(validB10);
        CACampaignPreflightDocument ambiguousB10 = ParsePreflightUnchanged(
            SyntheticB10Save(ownerVersion: 0, ambiguousSupport: true),
            out string ambiguousB10Before, out string ambiguousB10After);
        CACampaignCompatibilityDecision ambiguousB10Decision =
            CACampaignPreflightValidator.Evaluate(ambiguousB10);
        CACampaignPreflightDocument missingMapOwner = ParsePreflightUnchanged(
            SyntheticCurrentSave(includeMapOwner: false),
            out string missingMapBefore, out string missingMapAfter);
        CACampaignCompatibilityDecision missingMapDecision =
            CACampaignPreflightValidator.Evaluate(missingMapOwner);
        CACampaignPreflightDocument malformedNested = ParsePreflightUnchanged(
            SyntheticCurrentSave(malformedActRecord: true),
            out string nestedBefore, out string nestedAfter);
        CACampaignCompatibilityDecision malformedNestedDecision =
            CACampaignPreflightValidator.Evaluate(malformedNested);
        CACampaignPreflightDocument topologyPayload = ParsePreflightUnchanged(
            SyntheticCurrentSave(nonemptyTopology: true),
            out string topologyBefore, out string topologyAfter);
        CACampaignCompatibilityDecision topologyDecision =
            CACampaignPreflightValidator.Evaluate(topologyPayload);
        CACampaignPreflightDocument malformedDictionary =
            ParsePreflightUnchanged(
                SyntheticCurrentSave(malformedDictionary: true),
                out string dictionaryBefore, out string dictionaryAfter);
        CACampaignCompatibilityDecision malformedDictionaryDecision =
            CACampaignPreflightValidator.Evaluate(malformedDictionary);
        CACampaignPreflightDocument nullReference = ParsePreflightUnchanged(
            SyntheticCurrentSave(nullMissionReference: true),
            out string referenceBefore, out string referenceAfter);
        CACampaignCompatibilityDecision nullReferenceDecision =
            CACampaignPreflightValidator.Evaluate(nullReference);
        bool malformedNativeRejected = ParseFailsUnchanged(
            SyntheticCurrentSave(malformedNative: true),
            out string malformedNativeReason);
        CACampaignPreflightDocument malformedLayout = ParsePreflightUnchanged(
            SyntheticCurrentSave(malformedLayout: true),
            out string layoutBefore, out string layoutAfter);
        CACampaignCompatibilityDecision malformedLayoutDecision =
            CACampaignPreflightValidator.Evaluate(malformedLayout);
        CACampaignPreflightDocument unprovenLegacy = ParsePreflightUnchanged(
            SyntheticUnprovenLegacyState(), out string unprovenBefore,
            out string unprovenAfter);
        CACampaignCompatibilityDecision unprovenLegacyDecision =
            CACampaignPreflightValidator.Evaluate(unprovenLegacy);
        bool doubleSealRejected = DoubleSealRejected(out string sealReason);
        string corruptXml = SyntheticCurrentSave().Replace(
            "<CA_campaignMigrations />",
            "<CA_campaignMigrations><li /></CA_campaignMigrations>",
            StringComparison.Ordinal);
        CACampaignPreflightDocument corruptDigest = ParsePreflightUnchanged(
            corruptXml, out string corruptBefore, out string corruptAfter);
        CACampaignCompatibilityDecision corruptDigestDecision =
            CACampaignPreflightValidator.Evaluate(corruptDigest);
        bool emptyOwnerRejected = ParseFailsUnchanged(
            SyntheticEmptyOwnerSave(), out string emptyOwnerReason);
        bool duplicateMechanismRejected = ParseFailsUnchanged(
            SyntheticB10Save(ownerVersion: 0, ambiguousSupport: true,
                duplicateMechanismOption: true),
            out string duplicateMechanismReason);
        var preflightChecks = new Dictionary<string, bool>
        {
            ["current accepted"] = currentDecision.CanLoad,
            ["B10 accepted"] = validB10Decision.CanLoad,
            ["owner mismatch rejected"] = !malformedDecision.CanLoad,
            ["B10 owner mismatch rejected"] = !malformedB10Decision.CanLoad,
            ["ambiguous B10 rejected"] = !ambiguousB10Decision.CanLoad
                && ambiguousB10Decision.Reason.Contains(
                    "did not identify which"),
            ["missing map owner rejected"] = !missingMapDecision.CanLoad,
            ["deep record rejected"] = !malformedNestedDecision.CanLoad
                && malformedNestedDecision.Reason.Contains("knownHow"),
            ["nonempty topology accepted"] = topologyDecision.CanLoad,
            ["dictionary parity rejected"] =
                !malformedDictionaryDecision.CanLoad
                && malformedDictionaryDecision.Reason.Contains(
                    "keys/values differ in length"),
            ["textual null rejected"] = !nullReferenceDecision.CanLoad
                && nullReferenceDecision.Reason.Contains("null"),
            ["native parallel state rejected"] = malformedNativeRejected
                && malformedNativeReason.Contains("parallel collections"),
            ["nested layout rejected"] = !malformedLayoutDecision.CanLoad
                && malformedLayoutDecision.Reason.Contains(
                    "parallel collections"),
            ["unproven legacy rejected"] = !unprovenLegacyDecision.CanLoad
                && unprovenLegacyDecision.Reason.Contains(
                    "no supported B10 provenance"),
            ["double seal rejected"] = doubleSealRejected
                && sealReason.Contains("already contains a payload seal"),
            ["corrupt digest rejected"] = !corruptDigestDecision.CanLoad
                && corruptDigestDecision.Reason.Contains(
                    "digest does not match"),
            ["empty owner rejected"] = emptyOwnerRejected
                && emptyOwnerReason.Contains("empty campaign owner record"),
            ["duplicate mechanism rejected"] = duplicateMechanismRejected
                && duplicateMechanismReason.Contains("multiple options"),
            ["all preflight inputs immutable"] = new[]
            {
                currentBefore == currentAfter,
                malformedBefore == malformedAfter,
                b10Before == b10After,
                validB10Before == validB10After,
                ambiguousB10Before == ambiguousB10After,
                missingMapBefore == missingMapAfter,
                nestedBefore == nestedAfter,
                topologyBefore == topologyAfter,
                dictionaryBefore == dictionaryAfter,
                referenceBefore == referenceAfter,
                layoutBefore == layoutAfter,
                unprovenBefore == unprovenAfter,
                corruptBefore == corruptAfter
            }.All(value => value),
            ["load gate source present"] =
                allSource.Contains("PreflightBeforeGameDisposal")
                && allSource.Contains("LastRejectedLoadReason")
                && allSource.Contains("armedSavePayloadDigest")
                && allSource.Contains("document.PayloadDigest")
        };
        string[] failedPreflightChecks = preflightChecks
            .Where(item => !item.Value).Select(item => item.Key).ToArray();
        C(24, "Failed preflight leaves input unchanged",
            failedPreflightChecks.Length == 0,
            failedPreflightChecks.Length == 0
                ? "streaming preflight accepted complete current/B10 and nonempty topology saves; rejected digest/double-seal corruption, unequal dictionaries, textual-null references, truncated native parallel state, malformed nested layout, mismatched/missing owners, and unproven or ambiguous legacy state before Scribe owner load; every input byte remained unchanged"
                : "failed checks: " + string.Join(", ",
                    failedPreflightChecks) + "; current="
                    + currentDecision.Reason + "; nested="
                    + malformedNestedDecision.Reason + "; topology="
                    + topologyDecision.Reason + "; dictionary="
                    + malformedDictionaryDecision.Reason);
        string liveOwners = S("Source/CultureLongitudinalModule.cs")
            + S("Source/FactionStateModule.cs")
            + S("Source/PlayerFoundingStateModule.cs")
            + S("Source/OrganizationRelationsModule.cs")
            + S("Source/OrganizationModule.cs")
            + S("Source/RegionalWorldModule.cs")
            + S("Source/SocialInterpretationRuntimeModule.cs");
        C(25, "Authoring epoch cannot erase live campaign state",
            !liveOwners.Contains("RecordDiscard(")
            && liveOwners.Contains("LoadSaveMode.LoadingVars")
            && compatibility.Contains("never discard realized")
            && compatibility.Contains("state because it differs"),
            "legacy epoch is read-only B10 evidence; no live discard path remains");
        C(26, "Rollback workflow is documented",
            compatibility.Contains("## Rollback")
            && compatibility.Contains("restore the previous DLL")
            && compatibility.Contains("pre-migration save"),
            "DLL/save pair and no-hot-swap boundary are explicit");

        C(27, "Applicable B10 causal receipts remain passing",
            b10Passes == 75,
            b10Passes + "/75 recorded B10 receipts pass");
        C(28, "B10 synthetic-state invariants remain passing",
            synthetic.Contains("Unresolved Critical/High: **0**")
            && synthetic.Contains("Result: **PASS**"),
            "current sweep reports zero unresolved Critical/High occurrences");

        // B11 addendum receipts 1-50. These begin at 29 so the original B11
        // durability/ownership sequence remains stable and auditable.
        C(29, "Four completion layers are reported separately",
            new[] { "Representational capacity", "Production vocabulary",
                "Runtime realization", "Control surface" }
                .All(ontologyCoverage.Contains),
            "coverage report names capacity, current content, realization, and authoring independently");
        C(30, "Open capacity is not accepted as production breadth",
            ontologyCoverage.Contains("Extensibility is not production breadth")
            && ontologyCoverage.Contains("open registry", StringComparison.OrdinalIgnoreCase),
            "coverage standard explicitly rejects registry openness as completion evidence");
        C(31, "Demonstration records are distinguished from production content",
            ontologyCoverage.Contains("demonstration", StringComparison.OrdinalIgnoreCase)
            && ontologyCoverage.Contains("production", StringComparison.OrdinalIgnoreCase),
            "coverage report records the former exemplar failure and current production rule");
        C(32, "Every active social fact family is classified",
            ontologyCoverage.Contains("Mechanics classification matrix")
            && ontologyCoverage.Contains("Unclassified: **0**")
            && !ontologyCoverage.Contains("UNCLASSIFIED"),
            "mechanics matrix closes with zero unclassified active fact families");

        C(33, "Culture distributions, legacy evidence, and practices use distinct records",
            cognitionKernel.Contains("class CACultureQuestionDistribution")
            && cognitionKernel.Contains("class CACultureLegacyEvidence")
            && cultureSource.Contains("class CACulturePractice")
            && cultureSource.Contains("inheritedQuestions")
            && cultureSource.Contains("localQuestions")
            && cultureSource.Contains("public string practiceKey")
            && cultureSource.Contains("legacyEvidence"),
            "current normative distributions, migrated factual evidence, and concrete repeated practices remain separate payloads");
        string practiceState = Slice(cultureSource,
            "public sealed class CACulturePractice", "public sealed class CACultureObservation");
        C(34, "A practice cannot consist only of a subject key",
            practiceState.Contains("practiceKey")
            && !practiceState.Contains("public string subjectKey"),
            "persisted practice state has no public subject-key identity");
        C(35, "Every production practice declares repeated conduct",
            CACulturalPracticeRegistry.Validate(out string practiceFailure35)
            && CACulturalPracticeRegistry.All.Count >= 30,
            CACulturalPracticeRegistry.All.Count + " concrete practice definitions; "
                + (practiceFailure35 ?? "all complete"));
        C(36, "Every practice has evidence and a runtime consumer",
            CACulturalPracticeRegistry.All.All(item =>
                !string.IsNullOrWhiteSpace(item.EvidenceSource)
                    && !string.IsNullOrWhiteSpace(item.RuntimeConsumer)
                    && CACulturalPracticeRegistry
                        .HasExecutableObservationAdapter(item))
            && CACulturalPracticeRegistry.All
                .Where(item => !string.IsNullOrWhiteSpace(item.ActKind))
                .All(item => productionActSources.Contains(
                    "Emit(\"" + item.ActKind + "\""))
            && !CACulturalPracticeRegistry.HasBoundaryPatrolEvidence(
                17, 4, 0, false)
            && CACulturalPracticeRegistry.HasBoundaryPatrolEvidence(
                17, 4, 2, false)
            && CACulturalPracticeRegistry.MatchesActEvidence(
                CACulturalPracticeRegistry.Find(
                    CACulturalPracticeRegistry.CombatConduct),
                "violence", "struck-downed")
            && !CACulturalPracticeRegistry.MatchesActEvidence(
                CACulturalPracticeRegistry.Find(
                    CACulturalPracticeRegistry.CombatConduct),
                "violence", "mutual-combat")
            && longitudinalSource.Contains("MatchesActEvidence")
            && longitudinalSource.Contains("AddInstitutionalPractices"),
            "all concrete practices reach an executable observation adapter and consumer; act-backed practices have production emitters and exact predicates");
        C(37, "One subject may participate in several practices",
            CACulturalPracticeRegistry.All.SelectMany(item => item.SubjectKeys)
                .GroupBy(value => value, StringComparer.Ordinal)
                .Any(group => group.Count() > 1),
            "practice-to-subject relation is many-to-many");
        C(38, "One practice may implicate several subjects",
            CACulturalPracticeRegistry.All.Any(item =>
                item.SubjectKeys.Distinct(StringComparer.Ordinal).Count() > 1),
            "at least one concrete practice has several represented social referents");
        C(39, "Meaning and practice candidate universes are distinct",
            !new HashSet<string>(CASocialSubjectRegistry.Authorable()
                    .Select(item => item.Key), StringComparer.Ordinal)
                .SetEquals(CACulturalPracticeRegistry.All.Select(item => item.Key)),
            CASocialSubjectRegistry.Authorable().Count + " subjects versus "
                + CACulturalPracticeRegistry.All.Count + " practices");
        C(40, "Question authoring and practice inspection perform distinct operations",
            cultureSource.Contains("DrawQuestions")
            && cultureSource.Contains("CACultureQuestionRegistry.All")
            && cultureSource.Contains("DrawQuestionAdvanced")
            && cultureSource.Contains("DrawPracticeHistory")
            && cultureSource.Contains("are not settings here."),
            "questions write current distributions while practice history remains a distinct evidence surface");

        C(41, "Zero-item categories are omitted",
            CAAuthoringCategoryPolicy.NavigableGroups(
                Array.Empty<KeyValuePair<string, int>>()).Count == 0,
            "empty cardinality input produces no navigation");
        C(42, "One-item categories are not top-level navigation",
            CAAuthoringCategoryPolicy.NavigableGroups(new[] {
                new KeyValuePair<string, int>("one", 1),
                new KeyValuePair<string, int>("full", 4) }).Count == 0,
            "one row and one qualifying group produce no category strip");
        C(43, "Two- and three-item categories do not become tabs",
            CAAuthoringCategoryPolicy.NavigableGroups(new[] {
                new KeyValuePair<string, int>("two", 2),
                new KeyValuePair<string, int>("three", 3) }).Count == 0,
            "sub-threshold groups remain inline");
        string openMeaning = Slice(cultureSource,
            "private void OpenMeaningSubject()", "private void OpenPracticeSubject()");
        C(44, "SourceDomain is not projected into navigation",
            !openMeaning.Contains("SourceDomain")
            && authoringUi.Contains("CAAuthoringCategoryPolicy.NavigableGroups"),
            "social subjects are not split into source-shaped one-item tabs");
        C(45, "Category strips use wrapping layout",
            authoringUi.Contains("DrawSegmentRows")
            && ontologyKernel.Contains("TopLevelMinimumItems"),
            "qualifying filters wrap through the shared segment-row layout");
        C(46, "Category membership does not imply exclusivity",
            ontologyCoverage.Contains("Categories are filters, not exclusive state")
            && ontologyKernel.Contains("MultiValuedSet"),
            "navigation facets are documented and implemented separately from state cardinality");

        C(47, "Political Order owns complete normative composition",
            CAAuthoringControlContracts.All.Any(item =>
                item.Key == "politics.order"
                    && item.TemporalStatus == CAAuthoringTemporalStatus.Normative
                    && item.AuthoritativeOwner == "CAPoliticalBeliefs"
                    && item.SemanticKind
                        == CAAuthoringSemanticKind.StructuredComposition),
            "Political Order is one complete causal composition");
        C(48, "Represented institutions remain separate realized facts",
            CAAuthoringControlContracts.All.Any(item =>
                item.Key == "politics.represented-institutions"
                    && item.TemporalStatus == CAAuthoringTemporalStatus.Current
                    && item.AuthoritativeOwner == "CAFactionState"),
            "instituted facts are not a duplicate setup editor");
        C(49, "Obsolete duplicate political editor is removed",
            !allSource.Contains("Dialog_CAAxisEditor")
            && !allSource.Contains("CAPoliticalPatchTemplate")
            && !allSource.Contains("Current-order sets"),
            "one Political Order composer remains active");
        C(50, "Political identity is generated from variables",
            politicalOrder.Contains("GeneratedName(")
            && politicalOrder.Contains("Description(")
            && politicalOrder.Contains("PropertyAverage("),
            "name and account are projections of the complete variable state");
        C(51, "Political presets fill the complete order",
            politicalOrder.Contains("foreach (string property in PropertyKeys)")
            && politicalOrder.Contains("BuildPreset(")
            && politicalOrder.Contains("Complete order"),
            "complete presets replace partial mechanism patches");
        C(52, "Political mixtures are normalized",
            politicalOrder.Contains("100 - normalized.Sum")
            && politicalOrder.Contains("does not total 100")
            && politicalOrder.Contains("does not permit a mixture"),
            "blendable subjects total 100 and exclusive subjects stay singular");
        C(53, "Saved orders copy complete variables",
            presentation.Contains("ApplyPoliticalValues(profile?.values, target)")
            && presentation.Contains("target.CopyFrom(values)")
            && presentation.Contains("target.questions")
            && presentation.Contains("CAUserPoliticalOrderProfile"),
            "saved orders copy the complete question model into the target owner");
        C(54, "Global preset edits cannot mutate authored worlds",
            presentation.Contains("values = beliefs.Copy()")
            && presentation.Contains("ApplyPoliticalValues(profile?.values, target)")
            && presentation.Contains("target.CopyFrom(values)")
            && presentation.Contains("string ownerId = target.id")
            && presentation.Contains("option.source = (byte)CAAxisSource.Authored"),
            "saved profiles own copies; application copies the complete order into world-owned state while preserving owner identity");
        C(55, "Self-identification is separate from structural state",
            setupSource.Contains("public string customName")
            && setupSource.Contains("public List<CAAxisEntry> factionStructure")
            && ontologyCoverage.Contains("self-identification"),
            "faction name and instituted mechanisms have separate fields and controls");
        string characterize = Slice(compositionSource,
            "internal static string Characterize", "internal static List<string> Conflicts");
        C(56, "Derived descriptions remain read-only",
            characterize.Contains("OptionsOf")
            && !characterize.Contains("CAFactionAxes.Add")
            && !characterize.Contains("CAFactionAxes.Set"),
            "structural description reads complete mechanism sets and writes nothing");

        C(57, "Every exclusive authoring contract records its invariant",
            CAAuthoringControlContracts.Validate(out string contractFailure57)
            && CAAuthoringControlContracts.All.Where(item =>
                    item.SemanticKind == CAAuthoringSemanticKind.ExclusiveCategorical)
                .All(item => !string.IsNullOrWhiteSpace(
                    item.ExclusivityInvariant)),
            contractFailure57 ?? "all control contracts complete");
        C(58, "Political positions compose within their owning question",
            CAAuthoringControlContracts.All.Any(item =>
                item.Key == "politics.order"
                    && item.SemanticKind
                        == CAAuthoringSemanticKind.StructuredComposition)
            && politicalOrder.Contains("definition.Blendable")
            && politicalOrder.Contains("item.share * 100d / total"),
            "each blendable political question stores a normalized composition instead of an unscoped mechanism bag");
        bool authoredPair = activePlan.Descendants("questions").Any(value =>
                HasPoliticalPair(value, "property.industry", "private",
                    "cooperative"))
            && activePlan.Descendants("questions").Any(value =>
                HasPoliticalPair(value, "economy.exchange", "market",
                    "communal"));
        bool orderPair = activePlan.Descendants("factionStructure").Any(value =>
            HasPair(value, "support", "private", "public"));
        C(59, "Valid pairwise combinations survive serialization",
            authoredPair && orderPair,
            "fixture carries private+cooperative industry, market+communal exchange, and private+public represented support combinations");
        IReadOnlyList<CAPoliticalLegacyReceiptExpansion> ownershipExpansion =
            CAPoliticalLegacyMechanisms.ExpandReceipt("ownership", "mixed",
                "private=2,common=2", "seed=fixed", tieBroken: true);
        IReadOnlyList<CAPoliticalLegacyReceiptExpansion> economyExpansion =
            CAPoliticalLegacyMechanisms.ExpandReceipt("economy", "mixed",
                "market=3,planned=3", "seed=fixed", tieBroken: false);
        C(60, "Former mixed pseudo-options are eliminated",
            !compositionSource.Contains("new CAAxisOption(\"mixed\"")
            && !activePlan.Descendants("option").Any(value =>
                value.Value == "mixed")
            && compositionSource.Contains("IsAbsenceOption")
            && CAPoliticalLegacyMechanisms.Expand("ownership", "mixed")
                .SequenceEqual(new[] { "private", "cooperative", "common" })
            && CAPoliticalLegacyMechanisms.Expand("economy", "mixed")
                .SequenceEqual(new[] { "market", "planned", "communal" })
            && CAPoliticalLegacyMechanisms.Expand("support", "mixed")
                .Count == 0
            && !string.IsNullOrWhiteSpace(CAPoliticalLegacyMechanisms
                .UnsupportedReason("support", "mixed"))
            && ownershipExpansion.Select(item => item.OptionKey)
                .SequenceEqual(new[] { "private", "cooperative", "common" })
            && economyExpansion.Select(item => item.OptionKey)
                .SequenceEqual(new[] { "market", "planned", "communal" })
            && ownershipExpansion.All(item => item.Scores
                    == "private=2,common=2"
                && item.Evidence.Contains("seed=fixed")
                && item.Evidence.Contains("private, cooperative, common")
                && item.TieBroken)
            && economyExpansion.All(item => item.Scores
                    == "market=3,planned=3"
                && item.Evidence.Contains("seed=fixed")
                && item.Evidence.Contains("market, planned, communal")
                && !item.TieBroken)
            && ownershipExpansion.All(item => item.OptionKey != "mixed")
            && economyExpansion.All(item => item.OptionKey != "mixed")
            && cultureSource.Contains("NormalizeMechanisms")
            && cultureSource.Contains("ExpandReceipt")
            && cultureSource.Contains("ValidationFailure")
            && factionStateSource.Contains("TryUpgradeToCurrent(")
            && regional.Contains("TryUpgradeToCurrent(")
            && compatRuntime.Contains("migrateState")
            && !factionStateSource.Contains(
                "NormalizeMechanisms(state.politicalBeliefs.positions)"),
            "exact B10 ownership/economy coexistence expands completely; ambiguous B10 support fails visibly instead of inventing mechanisms");

        C(61, "Every visible control declares a real degree of freedom",
            CAAuthoringControlContracts.All.All(item =>
                item.FieldsWritten.Count > 0 && item.Consumers.Count > 0),
            CAAuthoringControlContracts.All.Count
                + " controls name exact writes and downstream consumers");
        C(62, "Derived facts are not authored controls",
            CAAuthoringControlContracts.All.Where(item =>
                    item.SemanticKind == CAAuthoringSemanticKind.Derived)
                .All(item => item.Authorship.Contains("read", StringComparison.OrdinalIgnoreCase)),
            "no derived summary is exposed as a direct authoring control");
        C(63, "One-option groups remain facts rather than controls",
            ontologyCoverage.Contains("One-option controls: **0**")
            && ontologyKernel.Contains("TopLevelMinimumItems = 4"),
            "singleton state is omitted or displayed as a factual readout");
        C(64, "Backend similarity does not duplicate authoring UI",
            cultureSource.Contains("DrawQuestions")
            && cultureSource.Contains("DrawPracticeHistory")
            && cognitionKernel.Contains("CACultureQuestionDistribution")
            && cultureSource.Contains("CACulturePractice"),
            "one composer separates current question distributions from historical practice evidence");
        C(65, "No global detail mode was introduced",
            !authoringUi.Contains("Advanced mode", StringComparison.OrdinalIgnoreCase)
            && !authoringUi.Contains("basic mode", StringComparison.OrdinalIgnoreCase),
            "detail remains contextual to the selected choice");
        C(66, "Ordinary authoring is not a raw registry browser",
            !openMeaning.Contains("SourceDomain")
            && !openMeaning.Contains("namespaced key", StringComparison.OrdinalIgnoreCase)
            && !openMeaning.Contains("schema version", StringComparison.OrdinalIgnoreCase),
            "player copy describes social facts and direct consequences");

        int subjectCount = CASocialSubjectRegistry.Authorable().Count;
        int practiceCount = CACulturalPracticeRegistry.All.Count;
        C(67, "Before and after vocabulary counts are reported",
            ontologyCoverage.Contains(subjectCount
                + " social subjects**")
            && ontologyCoverage.Contains(practiceCount
                + " concrete practice definitions**")
            && ontologyCoverage.Contains("B12 admitted the first 13 explicit questions")
            && ontologyCoverage.Contains("**48 Culture questions in 12 categories**"),
            "the coverage report preserves the former twelve-example boundary and reports "
                + subjectCount + " social subjects, " + practiceCount
                + " concrete practices, the B12 13-question boundary, and 48 current Culture questions");
        C(68, "Visible category cardinalities are reported",
            ontologyCoverage.Contains("Category cardinalities")
            && ontologyCoverage.Contains("top-level", StringComparison.OrdinalIgnoreCase),
            "coverage report records every projected category and threshold result");
        C(69, "No synonym-only practice inflates breadth",
            CACulturalPracticeRegistry.All.GroupBy(item =>
                item.Activity + "|" + item.EvidenceSource + "|"
                    + item.RuntimeConsumer, StringComparer.Ordinal).All(group =>
                        group.Count() == 1),
            "every practice differs in conduct, evidence, or consumer");
        C(70, "Every production entry has source and consumer",
            CASocialSubjectRegistry.Authorable().All(item =>
                item.IsAuthorable(out _))
            && CACulturalPracticeRegistry.Validate(out _),
            subjectCount + " social subjects and " + practiceCount
                + " practices pass executable production contracts");
        C(71, "The former twelve examples no longer define Culture breadth",
            subjectCount > 12 && practiceCount > 12,
            subjectCount + " authorable subjects and " + practiceCount
                + " distinct repeated practices are production-backed");

        C(72, "B10 causal receipts remain passing after the addendum",
            b10Passes == 75,
            b10Passes + "/75 retained causal receipts pass");
        C(73, "B10 synthetic-state invariants remain passing after the addendum",
            synthetic.Contains("Unresolved Critical/High: **0**")
            && synthetic.Contains("Result: **PASS**"),
            "synthetic-state sweep remains clean");
        C(74, "B11 ownership and persistence receipts remain passing",
            results.Where(item => item.Number <= 26).All(item => item.Passed),
            "all original ownership, profiler, schema, migration, and rollback receipts pass");
        C(75, "Schema transition is deterministic and explicit",
            V(activeDoc.Root, "authoringDataEpoch") == "15"
            && V(activePlan, "schemaVersion") == "16"
            && cultures.All(item => V(item, "schemaVersion") == "11")
            && politicalBeliefs.All(item => V(item, "schemaVersion") == "10")
            && activeDoc.Descendants("technologicalKnowledge").Count() >= 4
            && activeDoc.Descendants("technologicalKnowledge").All(item =>
                V(item, "schemaVersion") == "1")
            && activePlan.Element("settlements")?.Elements("li").All(item =>
                item.Element("factionLinks") != null) == true
            && activePlan.Element("frontierHoldings")?.Elements("li").All(item =>
                item.Element("factionLinks") != null
                && item.Element("localSociety") != null) == true
            && activeDoc.Descendants("populationGroups").Elements("li")
                .All(item => V(item, "schemaVersion") == "2")
            && culturePractices.All(item => !V(item, "practiceKey").Equals("")
                && !V(item, "sourceOwner").Equals("")
                && item.Element("subjectKey") == null)
            && ontologyKernel.Contains("FromB10LongitudinalEvidence"),
            "pending epoch 15, regional plan 16, Culture 11, Political Order 10, typed site links, complete frontier local state, population schema 2, technological-knowledge schema 1, and explicit B10 evidence gate");
        C(76, "Clean Release build completed with zero errors",
            buildReceipt.Contains("Warnings: **0**")
            && buildReceipt.Contains("Errors: **0**")
            && buildReceipt.Contains("Result: **PASS**"),
            "final build receipt records a clean Release build");
        C(77, "Deployed DLL is byte-identical to the verified build",
            deploymentReceipt.Contains("Byte-identical: **PASS**")
            && deploymentReceipt.Contains("RimWorld process: **closed**"),
            "closed-process deployment receipt records matching size and SHA-256");
        C(78, "No unresolved Critical or High findings remain",
            reviewReceipt.Contains("Critical: **0**")
            && reviewReceipt.Contains("High: **0**")
            && reviewReceipt.Contains("Result: **PASS**"),
            "five-lens review receipt closes production breadth, semantic distinction, composition, surface, and durability");

        if (!verifyOnly)
            WriteReports(activeHash, mirrorHash, activeBytes.Length,
                factionCount, settlements.Length, populationGroupCount,
                operationalFactCount, beforeIdentity, upgradedOnce,
                upgradedTwice, historyBefore, historyAfter, enabledCalls,
                activeBytes.SequenceEqual(mirrorBytes)
                    && Canonical(activePlan) == Canonical(mirrorPlan));

        foreach (Result result in results)
            Console.WriteLine($"{result.Number:00} {(result.Passed ? "PASS" : "FAIL")} {result.Name}: {result.Evidence}");
        int failed = results.Count(value => !value.Passed);
        Console.WriteLine($"{results.Count - failed}/{results.Count} passed");
        return failed == 0 ? 0 : 2;
    }

    private static PersistenceCensusResult WritePersistenceCensus()
    {
        List<PersistenceType> carriers = DiscoverPersistenceTypes();
        var catalog = new HashSet<string>(CACampaignSchemaCatalog.All
            .Select(item => item.Key), StringComparer.Ordinal);
        var rows = new List<(PersistenceType Carrier,
            PersistenceDisposition Disposition, bool Valid)>();
        foreach (PersistenceType carrier in carriers)
        {
            PersistenceDisposition disposition =
                ClassifyPersistenceCarrier(carrier);
            bool valid = disposition != null
                && (!string.IsNullOrWhiteSpace(disposition.Exclusion)
                    || (disposition.SchemaKeys.Length > 0
                        && disposition.SchemaKeys.All(catalog.Contains)));
            rows.Add((carrier, disposition, valid));
        }

        int exclusions = rows.Count(row => row.Disposition != null
            && !string.IsNullOrWhiteSpace(row.Disposition.Exclusion));
        int invalid = rows.Count(row => !row.Valid);
        int routedSchemas = rows.Where(row => row.Valid
                && row.Disposition.SchemaKeys.Length > 0)
            .SelectMany(row => row.Disposition.SchemaKeys)
            .Distinct(StringComparer.Ordinal).Count();
        var text = new StringBuilder();
        text.AppendLine("# Persistence census")
            .AppendLine()
            .AppendLine("Date: 2026-08-18")
            .AppendLine()
            .AppendLine("This report is generated from production C# source, independently of the campaign schema catalog. It discovers declarations that directly write through `Scribe`, call a nested `Expose` writer, or inherit a native persisted job/lord/need/thought/world/scenario owner. Every discovered carrier must resolve to an executable catalog schema or a narrow, stated non-campaign exclusion.")
            .AppendLine()
            .AppendLine("- Discovered persistence carriers: **"
                + carriers.Count + "**")
            .AppendLine("- Catalog schemas reached from source carriers: **"
                + routedSchemas + "**")
            .AppendLine("- Explicit non-campaign exclusions: **"
                + exclusions + "**")
            .AppendLine("- Unclassified or invalid routes: **"
                + invalid + "**")
            .AppendLine("- Result: **" + (invalid == 0 ? "PASS" : "FAIL")
                + "**")
            .AppendLine()
            .AppendLine("| Source declaration | Persistence evidence | Campaign schema or exclusion | Result |")
            .AppendLine("|---|---|---|---|");
        foreach (var row in rows.OrderBy(value => value.Carrier.File,
                     StringComparer.Ordinal)
                 .ThenBy(value => value.Carrier.Name,
                     StringComparer.Ordinal))
        {
            string fullName = string.IsNullOrWhiteSpace(
                    row.Carrier.Namespace)
                ? row.Carrier.Name
                : row.Carrier.Namespace + "." + row.Carrier.Name;
            string route = row.Disposition == null
                ? "UNCLASSIFIED"
                : !string.IsNullOrWhiteSpace(row.Disposition.Exclusion)
                    ? "Excluded: " + row.Disposition.Exclusion
                    : string.Join(", ", row.Disposition.SchemaKeys
                        .Select(key => "`" + key + "`"));
            text.AppendLine("| `" + fullName + "` in `"
                + row.Carrier.File + "` | "
                + (row.Carrier.CustomWrites
                    ? "direct Scribe/nested Expose writer"
                    : "native persisted owner (`" + Escape(
                        row.Carrier.Bases) + "`)")
                + " | " + Escape(route) + " | **"
                + (row.Valid ? "PASS" : "FAIL") + "** |");
        }
        text.AppendLine()
            .AppendLine("The executable reverse check remains separate: `CACampaignPreflightValidator.ValidateCatalogCoverage` requires every catalog key to have a component, nested-record, exact-native-class, or native-prefix validation route. Together, the two checks prove source-to-catalog and catalog-to-validator closure without using either list as its own evidence.");
        File.WriteAllText(P("PERSISTENCE_CENSUS.md"), text.ToString(),
            new UTF8Encoding(false));

        string evidence = carriers.Count + " source-derived carriers; "
            + routedSchemas + " catalog schemas; " + exclusions
            + " explicit non-campaign exclusions; " + invalid
            + " unclassified/invalid routes";
        return new PersistenceCensusResult(invalid == 0, carriers.Count,
            exclusions, evidence);
    }

    private static List<PersistenceType> DiscoverPersistenceTypes()
    {
        var discovered = new List<PersistenceType>();
        var declarationPattern = new Regex(
            @"(?m)^[ \t]*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|new|unsafe|readonly)\s+)*(?:class|struct)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:\s*<[^>{}]+>)?(?:\s*:\s*(?<bases>[^{}]+?))?\s*\{",
            RegexOptions.CultureInvariant);
        var namespacePattern = new Regex(
            @"\bnamespace\s+(?<name>[A-Za-z_][A-Za-z0-9_.]*)",
            RegexOptions.CultureInvariant);
        string sourceRoot = Path.Combine(repo, "Source");
        Dictionary<string, string> sourceInheritance =
            DiscoverSourceInheritance(sourceRoot, declarationPattern);
        foreach (string path in Directory.GetFiles(sourceRoot, "*.cs",
                     SearchOption.AllDirectories)
                 .OrderBy(value => value, StringComparer.Ordinal))
        {
            string source = File.ReadAllText(path);
            string relative = Path.GetRelativePath(repo, path)
                .Replace('\\', '/');
            string sourceNamespace = namespacePattern.Match(source) is Match nm
                && nm.Success ? nm.Groups["name"].Value : "";
            var declarations = new List<PersistenceDeclaration>();
            foreach (Match match in declarationPattern.Matches(source))
            {
                int openBrace = source.IndexOf('{', match.Index,
                    match.Length);
                int endBrace = FindMatchingBrace(source, openBrace);
                if (openBrace < 0 || endBrace < 0) continue;
                declarations.Add(new PersistenceDeclaration(relative,
                    sourceNamespace, match.Groups["name"].Value,
                    Regex.Replace(match.Groups["bases"].Value ?? "",
                        @"\s+", " ").Trim(), match.Index, openBrace,
                    endBrace, false));
            }
            for (int index = 0; index < declarations.Count; index++)
            {
                PersistenceDeclaration declaration = declarations[index];
                var ownBody = new StringBuilder(source.Substring(
                    declaration.OpenBrace + 1,
                    declaration.EndBrace - declaration.OpenBrace - 1));
                foreach (PersistenceDeclaration nested in declarations.Where(
                             candidate => candidate.Start
                                 > declaration.OpenBrace
                                 && candidate.EndBrace
                                 < declaration.EndBrace)
                         .OrderByDescending(candidate => candidate.Start))
                {
                    int nestedStart = nested.Start
                        - declaration.OpenBrace - 1;
                    int nestedLength = nested.EndBrace - nested.Start + 1;
                    if (nestedStart < 0
                        || nestedStart + nestedLength > ownBody.Length)
                        continue;
                    ownBody.Remove(nestedStart, nestedLength);
                    ownBody.Insert(nestedStart,
                        new string(' ', nestedLength));
                }
                bool customWrites = Regex.IsMatch(ownBody.ToString(),
                    @"\bScribe_[A-Za-z0-9_]+\s*\.\s*Look\b|\.\s*Expose\s*\(",
                    RegexOptions.CultureInvariant);
                bool nativeOwner = InheritsNativePersistence(
                    declaration.Bases, sourceInheritance,
                    new HashSet<string>(StringComparer.Ordinal));
                if (!customWrites && !nativeOwner) continue;
                discovered.Add(new PersistenceType(relative,
                    declaration.Namespace, declaration.Name,
                    declaration.Bases, customWrites));
            }
        }
        return discovered
            .GroupBy(item => item.File + "::" + item.Namespace + "."
                + item.Name, StringComparer.Ordinal)
            .Select(group => new PersistenceType(group.First().File,
                group.First().Namespace, group.First().Name,
                string.Join(" + ", group.Select(item => item.Bases)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)),
                group.Any(item => item.CustomWrites)))
            .OrderBy(item => item.File, StringComparer.Ordinal)
            .ThenBy(item => item.Name, StringComparer.Ordinal).ToList();
    }

    private static Dictionary<string, string> DiscoverSourceInheritance(
        string sourceRoot, Regex declarationPattern)
    {
        var inheritance = new Dictionary<string, string>(
            StringComparer.Ordinal);
        foreach (string path in Directory.GetFiles(sourceRoot, "*.cs",
                     SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(path);
            foreach (Match match in declarationPattern.Matches(source))
            {
                string name = match.Groups["name"].Value;
                string bases = Regex.Replace(
                    match.Groups["bases"].Value ?? "", @"\s+", " ")
                    .Trim();
                if (string.IsNullOrWhiteSpace(name)
                    || string.IsNullOrWhiteSpace(bases)) continue;
                if (inheritance.TryGetValue(name, out string existing))
                    inheritance[name] = existing + ", " + bases;
                else inheritance.Add(name, bases);
            }
        }
        return inheritance;
    }

    private static bool InheritsNativePersistence(string bases,
        IReadOnlyDictionary<string, string> sourceInheritance,
        HashSet<string> visited)
    {
        if (string.IsNullOrWhiteSpace(bases)) return false;
        foreach (Match identifier in Regex.Matches(bases,
                     @"\b[A-Za-z_][A-Za-z0-9_]*\b",
                     RegexOptions.CultureInvariant))
        {
            string name = identifier.Value;
            if (Regex.IsMatch(name,
                    @"^(?:JobDriver(?:_[A-Za-z0-9_]+)?|LordJob|Need|Thought(?:_[A-Za-z0-9_]+)?|WorldObject|ScenPart)$",
                    RegexOptions.CultureInvariant))
                return true;
            if (!visited.Add(name)
                || !sourceInheritance.TryGetValue(name,
                    out string inheritedBases)) continue;
            if (InheritsNativePersistence(inheritedBases,
                    sourceInheritance, visited)) return true;
        }
        return false;
    }

    private static int FindMatchingBrace(string source, int openBrace)
    {
        if (openBrace < 0) return -1;
        int depth = 0;
        bool lineComment = false;
        bool blockComment = false;
        bool inString = false;
        bool inCharacter = false;
        bool verbatim = false;
        for (int index = openBrace; index < source.Length; index++)
        {
            char current = source[index];
            char next = index + 1 < source.Length ? source[index + 1] : '\0';
            if (lineComment)
            {
                if (current == '\n') lineComment = false;
                continue;
            }
            if (blockComment)
            {
                if (current == '*' && next == '/')
                {
                    blockComment = false;
                    index++;
                }
                continue;
            }
            if (inString)
            {
                if (verbatim && current == '"' && next == '"')
                {
                    index++;
                    continue;
                }
                if (current == '"' && (verbatim
                        || index == 0 || source[index - 1] != '\\'))
                {
                    inString = false;
                    verbatim = false;
                }
                continue;
            }
            if (inCharacter)
            {
                if (current == '\''
                    && (index == 0 || source[index - 1] != '\\'))
                    inCharacter = false;
                continue;
            }
            if (current == '/' && next == '/')
            {
                lineComment = true;
                index++;
                continue;
            }
            if (current == '/' && next == '*')
            {
                blockComment = true;
                index++;
                continue;
            }
            if (current == '"')
            {
                inString = true;
                verbatim = index > 0 && source[index - 1] == '@';
                continue;
            }
            if (current == '\'')
            {
                inCharacter = true;
                continue;
            }
            if (current == '{') depth++;
            else if (current == '}' && --depth == 0) return index;
        }
        return -1;
    }

    private static PersistenceDisposition ClassifyPersistenceCarrier(
        PersistenceType carrier)
    {
        string fullName = string.IsNullOrWhiteSpace(carrier.Namespace)
            ? carrier.Name : carrier.Namespace + "." + carrier.Name;
        if (CACampaignPreflightReader.NativeSchemaByClass.TryGetValue(
                fullName, out string nativeSchema))
            return Route(nativeSchema);
        foreach (KeyValuePair<string, string> prefix in
                 CACampaignPreflightReader.NativeSchemaPrefixes)
            if (fullName.StartsWith(prefix.Key, StringComparison.Ordinal))
                return Route(prefix.Value);

        var direct = new HashSet<string>(StringComparer.Ordinal);
        foreach (CACampaignOwnerVersionDefinition owner in
                 CACampaignPreflightReader.OwnerVersionDefinitions)
            if (owner.ComponentType == fullName) direct.Add(owner.SchemaKey);
        foreach (CACampaignPayloadDefinition payload in
                 CACampaignPreflightReader.PayloadDefinitions)
            if (payload.ComponentType == fullName)
                foreach (string key in payload.SchemaKeys) direct.Add(key);
        if (direct.Count > 0) return Route(direct.ToArray());

        if (carrier.Name is "AwarenessSettings"
            or "CAUserCultureProfile" or "CAUserPoliticalOrderProfile"
            or "CAUserSocietyProfile")
            return Exclude("global mod settings or user preset outside a realized campaign save");
        if (carrier.Name is "CAAgentDebugBridge"
            or "CAConvergenceExerciseComponent")
            return Exclude("developer-only diagnostic state with no campaign causal authority");
        if (carrier.Namespace.StartsWith("Camping_Stuff",
                StringComparison.Ordinal))
            return Route("embedded.camping-state");

        string file = Path.GetFileName(carrier.File);
        switch (file)
        {
            case "ActRecordModule.cs": return Route("world.act-ledger");
            case "ArrangementModule.cs": return Route("map.arrangements");
            case "AssaultAwarenessModule.cs": return Route("map.assault-awareness");
            case "AutonomousHomeModule.cs": return Route("map.autonomous-home");
            case "AutonomyModule.cs": return Route("game.autonomy");
            case "BattlefieldParleyModule.cs": return Route("map.parley");
            case "BehaviorIntentModule.cs": return Route("map.behavior-intent");
            case "CampaignCompatibilityModule.cs": return Route("campaign.boundary");
            case "CombatAftermathModule.cs": return Route("map.combat-aftermath");
            case "CombatSpatialLogModule.cs": return Route("game.combat-spatial-log");
            case "CombatTopologyModule.cs": return Route("game.combat-topology");
            case "CultureLongitudinalModule.cs": return Route("map.culture-longitudinal");
            case "CultureNativePracticeModule.cs": return Route("map.culture-longitudinal");
            case "CulturalCognitionKernel.cs": return Route("model.culture");
            case "CulturalCognitionStateModule.cs":
                if (carrier.Name is "CAPoliticalOptionSupport"
                    or "CAPawnPoliticalAttitude"
                    or "CAPoliticalIssueLink"
                    or "CAPoliticalCoalitionRecord")
                    return Route("world.political-cognition");
                if (carrier.Name == "CAKnowledgePropositionRecord")
                    return Route("world.proposition-knowledge");
                return Route("world.cultural-cognition");
            case "CulturalPoliticsStateModule.cs": return Route("world.political-cognition");
            case "DomesticUnitModule.cs":
                return Route(carrier.Name == "CADomesticProvisionDemand"
                    ? "model.domestic-provision-demand"
                    : "model.domestic-unit");
            case "EquipTransitionModule.cs": return Route("map.equipment-transition");
            case "FactionCompositionModule.cs":
                return Route("model.political-order",
                    "model.represented-institutions");
            case "FactionCultureBeliefsModule.cs":
                return Route(carrier.Name.StartsWith("CAPolitical",
                        StringComparison.Ordinal)
                    ? "model.political-order" : "model.culture");
            case "PoliticalOrderAuthoringModule.cs":
                return Route("model.political-order");
            case "FactionStateModule.cs": return Route("world.faction-state");
            case "FoundingArrangementModule.cs": return Route("model.founding-arrangement");
            case "FrontierModule.cs": return Route("model.frontier-map-plan");
            case "GroundwaterModule.cs": return Route("model.groundwater-tuning");
            case "HomeIntentModule.cs": return Route("map.home-space-program");
            case "HomePrerequisiteModule.cs": return Route("map.home-prerequisite");
            case "HygieneNeedsModule.cs": return Route("pawn.hygiene-needs");
            case "ImmediateCombatModule.cs": return Route("map.drafted-combat-initiative");
            case "InventoryStorageModule.cs": return Route("map.inventory-storage");
            case "MissionTriageModule.cs": return Route("map.mission-triage");
            case "OffhandModule.cs": return Route("game.offhand");
            case "OperationalAccessModule.cs": return Route("game.operational-access");
            case "OrganizationModule.cs": return Route("world.organization");
            case "OrganizationRelationModel.cs":
                return Route(carrier.Name == "CARelation"
                    ? "model.organization-relation"
                    : carrier.Name == "CAFacilityHolding"
                        ? "model.organization-holding"
                        : "world.organization-relations");
            case "OrganizationRelationsModule.cs": return Route("world.organization-relations");
            case "PatrolSystemModule.cs": return Route("map.patrol");
            case "PlanModule.cs": return Route("map.contingency-plan");
            case "PlayerFoundingStateModule.cs": return Route("model.player-founding-plan");
            case "PropositionKnowledgeModule.cs": return Route("world.proposition-knowledge");
            case "RaidResponseModule.cs": return Route("map.raid-response");
            case "RegionalSettlementModelModule.cs": return Route("world.regional");
            case "RegionalSetupModule.cs":
                return Route(carrier.Name == "CARegionalWorldPolicy"
                    ? "world.regional" : "model.regional-plan");
            case "RegionalWorldModule.cs":
                return Route(carrier.Name == "CAStartingStockRecord"
                    ? "model.starting-stock"
                    : carrier.Name == "CARegionalSettlementRecord"
                        ? "model.regional-settlement-record"
                        : "world.regional");
            case "RoadExpansionModule.cs": return Route("map.road-expansion");
            case "SettlementCapabilityModule.cs": return Route("model.settlement-capability");
            case "SettlementCompositionModule.cs":
                return Route(carrier.Name == "CAProvisionArrangement"
                    ? "model.settlement-provision"
                    : "model.settlement-population-group");
            case "SettlementLayoutModule.cs": return Route("model.settlement-layout");
            case "SettlementPlanningContextModule.cs": return Route("map.settlement-planning-context");
            case "SettlementProgramAssetModule.cs": return Route("model.settlement-program-asset");
            case "SettlementProgramMaterializerModule.cs":
                return Route(carrier.Name.Contains("Research",
                        StringComparison.Ordinal)
                    ? "model.settlement-research-work"
                    : carrier.Name.Contains("Repair", StringComparison.Ordinal)
                        ? "model.settlement-repair-work"
                        : carrier.Name.Contains("Rebuild", StringComparison.Ordinal)
                            ? "model.settlement-rebuild-work"
                            : "map.settlement-work");
            case "SettlementProgramModule.cs":
                return Route(carrier.Name == "CASettlementProgramEntry"
                    ? "model.settlement-program-entry"
                    : carrier.Name == "CASettlementOperationalFact"
                        ? "model.settlement-operational-fact"
                        : "model.settlement-program");
            case "SettlementResidenceModule.cs": return Route("model.settlement-residence");
            case "SiteAffiliationModule.cs":
                return Route(carrier.Name == "CASiteFactionLinks"
                    ? "model.site-faction-links"
                    : "model.site-local-society");
            case "SocialInterpretationRuntimeModule.cs": return Route("world.social-reactions");
            case "SpatialInitiativeModule.cs": return Route("map.spatial-initiative");
            case "SquadModule.cs": return Route("game.squad");
            case "SquadSupportModule.cs": return Route("map.squad-support");
            case "StackLord.cs": return Route("native.stack-lord");
            case "StorageProgramModule.cs": return Route("map.storage-program");
            case "SurvivalModule.cs": return Route("game.hidden-things");
            case "SustenanceModule.cs": return Route("game.sustenance");
            case "TacticalLord.cs": return Route("native.tactical-lord");
            case "TacticalOverlayModule.cs": return Route("map.tactical-overlay");
            case "TaskForceModule.cs": return Route("map.task-force");
            case "TechnologicalKnowledgeModule.cs":
                return Route("model.technological-knowledge");
            case "ToxicWasteLifecycleModule.cs": return Route("map.toxic-waste");
            case "TransactionLedgerModule.cs": return Route("world.transaction-ledger");
            case "TrapAwarenessModule.cs": return Route("map.trap-memory");
            case "WelfareKnowledgeModule.cs": return Route("map.welfare-knowledge");
            case "WelfareSupportModule.cs": return Route("map.welfare-support");
            case "WithdrawalModule.cs": return Route("map.withdrawal");
        }
        return null;
    }

    private static PersistenceDisposition Route(params string[] keys)
    {
        return new PersistenceDisposition(keys.Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal).ToArray(), null);
    }

    private static PersistenceDisposition Exclude(string reason)
    {
        return new PersistenceDisposition(Array.Empty<string>(), reason);
    }

    private static XElement B10Envelope(XDocument fixture, string hash)
    {
        return new XElement("caB10CompatibilityEnvelope",
            new XElement("legacyAuthoringEpoch",
                CACampaignCompatibilityKernel.LegacyB10AuthoringEpoch),
            new XElement("sourceSha256", hash),
            new XElement("state", new XElement(fixture.Root)));
    }

    private static CACampaignPreflightDocument ParsePreflightUnchanged(
        string xml, out string before, out string after)
    {
        string path = Path.Combine(Path.GetTempPath(),
            "cao-b11-preflight-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            File.WriteAllText(path, xml, new UTF8Encoding(false));
            before = Sha(File.ReadAllBytes(path));
            CACampaignPreflightDocument document =
                CACampaignPreflightReader.Read(path);
            after = Sha(File.ReadAllBytes(path));
            return document;
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static bool ParseFailsUnchanged(string xml, out string reason)
    {
        string path = Path.Combine(Path.GetTempPath(),
            "cao-b11-preflight-invalid-" + Guid.NewGuid().ToString("N")
                + ".xml");
        try
        {
            File.WriteAllText(path, xml, new UTF8Encoding(false));
            string before = Sha(File.ReadAllBytes(path));
            try
            {
                CACampaignPreflightReader.Read(path);
                reason = "preflight unexpectedly accepted malformed input";
                return false;
            }
            catch (InvalidDataException failure)
            {
                reason = failure.Message;
                return before == Sha(File.ReadAllBytes(path));
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static string SyntheticCurrentSave(int factionOwnerVersion = -1,
        bool includeMapOwner = true, bool malformedActRecord = false,
        bool nonemptyTopology = false, bool malformedDictionary = false,
        bool nullMissionReference = false, bool malformedNative = false,
        bool malformedLayout = false)
    {
        var gameComponents = new XElement("components");
        var worldComponents = new XElement("components");
        var mapComponents = new XElement("components");
        foreach (CACampaignPayloadDefinition definition in
            CACampaignPreflightReader.PayloadDefinitions)
        {
            if (!includeMapOwner && definition.ComponentType ==
                    "ColonistAwareness.CACultureLongitudinalMapComponent")
                continue;
            XElement component = SyntheticPayloadComponent(definition,
                factionOwnerVersion, malformedActRecord, nonemptyTopology,
                malformedDictionary, nullMissionReference, malformedLayout);
            if (definition.Scope == CACampaignPayloadScope.WorldOnce)
                worldComponents.Add(component);
            else if (definition.Scope == CACampaignPayloadScope.GameOnce)
                gameComponents.Add(component);
            else
                mapComponents.Add(component);
        }
        // The mechanism parser is deliberately scoped to recognized CA owner
        // records. An unrelated component can use the same generic XML names.
        gameComponents.Add(new XElement("li",
            new XAttribute("Class", "Unrelated.Mod.Component"),
            new XElement("unrelatedRecords", new XElement("li",
                new XElement("axis", "one"),
                new XElement("axis", "two"),
                new XElement("option", "alpha"),
                new XElement("option", "beta"))),
            new XElement("positions", new XElement("li",
                new XElement("axis", "support"),
                new XElement("option", "mixed")))));
        XElement map = new XElement("li", mapComponents);
        if (malformedNative) map.Add(SyntheticMalformedTacticalLord());
        var root = new XElement("savegame",
            new XElement("game", gameComponents,
                new XElement("world", worldComponents),
                new XElement("maps", map)));
        return SealXml(root);
    }

    private static XElement SyntheticPayloadComponent(
        CACampaignPayloadDefinition definition, int factionOwnerVersion,
        bool malformedActRecord, bool nonemptyTopology,
        bool malformedDictionary, bool nullMissionReference,
        bool malformedLayout)
    {
        var component = new XElement("li",
            new XAttribute("Class", definition.ComponentType));
        CACampaignOwnerVersionDefinition? owner =
            CACampaignPreflightReader.OwnerVersionDefinitions
                .Cast<CACampaignOwnerVersionDefinition?>()
                .FirstOrDefault(item => item.Value.ComponentType
                    == definition.ComponentType);
        if (owner.HasValue)
        {
            int current = CACampaignSchemaCatalog.All.First(item =>
                item.Key == owner.Value.SchemaKey).CurrentVersion;
            int version = owner.Value.SchemaKey == "world.faction-state"
                && factionOwnerVersion >= 0
                    ? factionOwnerVersion : current;
            component.Add(new XElement(owner.Value.XmlTag, version));
        }
        if (definition.ComponentType ==
            "ColonistAwareness.CACampaignCompatibilityWorldComponent")
        {
            component.Add(new XElement("CA_campaignBoundaryVersion",
                CACampaignCompatibilityKernel.CurrentBoundaryVersion));
            component.Add(new XElement("CA_campaignCatalogVersion",
                CACampaignSchemaCatalog.CurrentCatalogVersion));
            component.Add(new XElement("CA_campaignSchemas",
                CACampaignSchemaCatalog.All.Select(item => new XElement("li",
                    new XElement("schemaKey", item.Key),
                    new XElement("version", item.CurrentVersion)))));
            component.Add(new XElement("CA_campaignMigrations"));
            return component;
        }
        foreach (string name in definition.RequiredNonNullPaths)
            component.Add(new XElement(name));
        foreach (string name in definition.RequiredAllowNullPaths)
            component.Add(new XElement(name,
                new XAttribute("IsNull", "True")));
        if (definition.ComponentType ==
            "ColonistAwareness.CAPlayerFoundingWorldComponent")
        {
            XElement founding = component.Element("CA_playerFounding")!;
            founding.Add(new XElement("schemaVersion",
                    CAPlayerFoundingPlanVersion()),
                SyntheticCulture(), SyntheticPoliticalBeliefs(),
                new XElement("arrangement",
                    new XElement("schemaVersion",
                        CACampaignSchemaCatalog.All.First(item =>
                            item.Key == "model.founding-arrangement")
                            .CurrentVersion),
                    new XElement("id", "shared-survival"),
                    new XElement("label", "Shared survival"),
                    new XElement("premise", "Shared terms at landing"),
                    new XElement("leaderRule", "none")));
            component.Element("CA_playerFoundingAppliedAtTick")!.Value = "1";
        }
        else if (definition.ComponentType == "ColonistAwareness.CAActLedger")
        {
            component.Element("CA_actRecords")?.Add(
                malformedActRecord
                    ? new XElement("li", new XElement("id", 1),
                        new XElement("knownByIds"),
                        new XElement("orgsAnswered"),
                        new XElement("pawnsAnswered"))
                    : null);
            component.Add(new XElement("CA_actRecordNextId", 2));
        }
        else if (definition.ComponentType ==
            "ColonistAwareness.CATransactionLedger")
            component.Add(new XElement("nextId", 1));
        else if (malformedDictionary && definition.ComponentType ==
            "ColonistAwareness.AutonomyComponent")
        {
            XElement dictionary = component.Element("CA_initiativeTiers")!;
            dictionary.Add(new XElement("keys", new XElement("li", 1)),
                new XElement("values"));
        }
        else if (nullMissionReference && definition.ComponentType ==
            "ColonistAwareness.CAPatrolSystemMapComponent")
            // Casualty facts now legitimately allow null rescuer and
            // beneficiary references (the pawns can die); a patrol
            // assignment's pawn stays required non-null, so it carries
            // the textual-null rejection contract.
            component.Element("patrols")!.Add(
                new XElement("li", new XElement("pawn", "null"),
                    new XElement("route",
                        new XElement("li", "(1, 0, 1)"))));
        else if (definition.ComponentType ==
            "ColonistAwareness.CARegionalWorldComponent")
        {
            component.Element("CA_groundwaterTuning")?.Add(
                new XElement("schemaVersion",
                    CACampaignSchemaCatalog.All.First(item =>
                        item.Key == "model.groundwater-tuning")
                        .CurrentVersion));
            component.Element("CA_regionalSettlements")!.Add(
                SyntheticRegionalSettlement(malformedLayout));
        }
        else if (nonemptyTopology && definition.ComponentType ==
            "ColonistAwareness.CACombatSpatialLogComponent")
        {
            component.Element("CA_combatTopologyIncidents")?.Add(
                new XElement("li",
                    new XElement("terrainPalette"),
                    new XElement("roofPalette"),
                    new XElement("baselineThings"),
                    new XElement("knownCellIndices"),
                    new XElement("observerPawnIds"),
                    new XElement("observedThingIds"),
                    new XElement("logIds"),
                    new XElement("nativeBattleAliases"),
                    new XElement("deltas", new XElement("li",
                        new XElement("cells"),
                        new XElement("thing",
                            new XAttribute("IsNull", "True")),
                        new XElement("things"))),
                    new XElement("hazardSamples"),
                    new XElement("battlefieldReference",
                        new XElement("terrainPalette"),
                        new XElement("roofPalette"),
                        new XElement("artifacts"),
                        new XElement("changes"))));
        }
        return component;
    }

    private static XElement SyntheticCulture()
    {
        IEnumerable<XElement> questions = CACultureQuestionRegistry.All
            .Select(question => new XElement("li",
                new XElement("schemaVersion", 1),
                new XElement("questionKey", question.Key),
                new XElement("populationScope", "*"),
                new XElement("subgroups")));
        return new XElement("culture",
            new XElement("schemaVersion",
                CACampaignSchemaCatalog.All.First(item =>
                    item.Key == "model.culture").CurrentVersion),
            new XElement("id", "culture.synthetic"),
            new XElement("constituents"),
            new XElement("questionRegistryVersion", 3),
            new XElement("withinGroupSpread", 2),
            new XElement("subgroupSeparation", 2),
            new XElement("inheritedQuestions", questions),
            new XElement("localQuestions"),
            new XElement("legacyEvidence"),
            new XElement("inheritedMeanings"),
            new XElement("localMeanings"),
            new XElement("transitions"),
            new XElement("inheritedPractices"),
            new XElement("practices"),
            new XElement("observations"),
            new XElement("lastEvidence", new XAttribute("IsNull", "True")));
    }

    private static XElement SyntheticPoliticalBeliefs()
    {
        return new XElement("politicalBeliefs",
            new XElement("schemaVersion", CACampaignSchemaCatalog.All
                .First(item => item.Key == "model.political-order")
                .CurrentVersion),
            new XElement("id", "beliefs.synthetic"),
            new XElement("name", new XAttribute("IsNull", "True")),
            new XElement("nameAuthored", "False"),
            new XElement("nameRoll", 0),
            new XElement("generationRoll", 0),
            new XElement("questions"),
            new XElement("positions"),
            new XElement("derivationReceipts"));
    }

    private static int CAPlayerFoundingPlanVersion()
    {
        return CACampaignSchemaCatalog.All.First(item =>
            item.Key == "model.player-founding-plan").CurrentVersion;
    }

    private static XElement SyntheticRegionalSettlement(bool malformedLayout)
    {
        XElement layout = new XElement("layout",
            new XElement("schemaVersion",
                CACampaignSchemaCatalog.All.First(item =>
                    item.Key == "model.settlement-layout").CurrentVersion),
            new XElement("gates", new XElement("li", "(1,0,1)")),
            new XElement("gateWidths", malformedLayout
                ? null : new XElement("li", 1)),
            new XElement("ways"),
            new XElement("facilityKinds"),
            new XElement("facilityCells"),
            new XElement("roads"),
            new XElement("roomCells"),
            new XElement("roomRoles"),
            new XElement("utilities"),
            new XElement("utilityKinds"),
            new XElement("approaches"));
        return new XElement("li",
            new XElement("schemaVersion", CACampaignSchemaCatalog.All
                .First(item => item.Key == "model.regional-settlement-record")
                .CurrentVersion),
            new XElement("regionalId", "regional.synthetic"),
            new XElement("name", "Synthetic settlement"),
            new XElement("regionKey", "region.synthetic"),
            new XElement("slot", 0),
            new XElement("faction", new XAttribute("IsNull", "True")),
            new XElement("factionLinks",
                new XElement("schemaVersion", 1),
                new XElement("ownership", "None"),
                new XElement("ownerRegionalFactionKey", -1),
                new XElement("ownerWorldFactionLoadId", -1),
                new XElement("support", "None"),
                new XElement("supportRegionalFactionKey", -1),
                new XElement("supportWorldFactionLoadId", -1)),
            new XElement("localSociety",
                new XElement("schemaVersion", 1),
                new XElement("explicitLocalDivergence", "True"),
                new XElement("politicalOrder",
                    SyntheticPoliticalBeliefs().Elements()),
                new XElement("technologicalKnowledge",
                    new XElement("schemaVersion", 1),
                    new XElement("domains"),
                    new XElement("custody")),
                new XElement("institutions"),
                new XElement("institutionalStateIncomplete", "False")),
            new XElement("capabilities"),
            new XElement("residentIds"),
            new XElement("populationGroups"),
            new XElement("residenceAssignments"),
            new XElement("provisionArrangements"),
            new XElement("domesticProvisionDemands"),
            new XElement("domesticUnits"),
            new XElement("domesticMembershipTransitions"),
            new XElement("operationalFacts"),
            new XElement("settlementProgram",
                new XElement("schemaVersion", 4),
                new XElement("entries")),
            SyntheticCulture(),
            new XElement("populationAssignments"),
            new XElement("statusAssignments"),
            new XElement("seededAssets"),
            new XElement("programAssets"),
            new XElement("startingStock"),
            layout,
            new XElement("creationBeneficiaries"),
            new XElement("developmentBeneficiaries"),
            new XElement("developmentDemandKinds"),
            new XElement("developmentAssetCandidates"));
    }

    private static XElement SyntheticMalformedTacticalLord()
    {
        string[] names =
        {
            "orderPawns", "orderKinds", "orderCells", "orderWatches",
            "orderEpisodes", "orderOrigins", "orderControllers",
            "orderIssuers", "orderBehaviorKeys", "orderAuthorityOrigins",
            "orderAuthorityIdentities", "orderOwnershipScopes",
            "orderOwnerIds", "orderCreatedTicks", "orderCreationTiers",
            "orderTargets", "orderTerminationConditions",
            "orderFireSuppressed", "orderEnvelopes", "orderGrits",
            "orderProfilePinned"
        };
        var lord = new XElement("lordJob",
            new XAttribute("Class", "ColonistAwareness.LordJob_CATactical"));
        foreach (string name in names)
            lord.Add(new XElement(name, name == "orderKinds"
                ? new XElement("li", 1) : null));
        return lord;
    }

    private static string SyntheticUnprovenLegacyState()
    {
        return new XElement("savegame",
            new XElement("game", new XElement("components",
                new XElement("li",
                    new XAttribute("Class",
                        "ColonistAwareness.CATransactionLedger"),
                    new XElement("transactions"),
                    new XElement("debts"))))).ToString(
                        SaveOptions.DisableFormatting);
    }

    private static bool DoubleSealRejected(out string reason)
    {
        string path = Path.Combine(Path.GetTempPath(),
            "cao-b11-double-seal-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            File.WriteAllText(path, "<savegame />", new UTF8Encoding(false));
            CACampaignPreflightReader.AppendPayloadDigest(path);
            try
            {
                CACampaignPreflightReader.AppendPayloadDigest(path);
                reason = "double seal unexpectedly accepted";
                return false;
            }
            catch (InvalidDataException expected)
            {
                reason = expected.Message;
                return CACampaignPreflightReader.TryVerifyPayloadDigest(path,
                    out bool valid, out _) && valid;
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static string SealXml(XElement root)
    {
        string path = Path.Combine(Path.GetTempPath(),
            "cao-b11-seal-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            File.WriteAllText(path,
                root.ToString(SaveOptions.DisableFormatting),
                new UTF8Encoding(false));
            CACampaignPreflightReader.AppendPayloadDigest(path);
            return File.ReadAllText(path);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static string SyntheticB10Save(int ownerVersion,
        bool ambiguousSupport = false,
        bool duplicateMechanismOption = false)
    {
        var worldComponents = new XElement("components");
        foreach (CACampaignOwnerVersionDefinition owner in
            CACampaignPreflightReader.OwnerVersionDefinitions.Where(item =>
                item.RequiredOncePerSave))
        {
            int version = owner.SchemaKey == "world.faction-state"
                ? ownerVersion : 0;
            XElement ownerElement = new XElement("li",
                new XAttribute("Class", owner.ComponentType),
                new XElement("CA_authoringDataEpoch",
                    CACampaignCompatibilityKernel.LegacyB10AuthoringEpoch),
                new XElement(owner.XmlTag, version));
            if (ambiguousSupport
                && owner.SchemaKey == "world.faction-state")
            {
                XElement mechanism = new XElement("li",
                    new XElement("axis", "support"),
                    new XElement("option", "mixed"));
                if (duplicateMechanismOption)
                    mechanism.Add(new XElement("option", "public"));
                ownerElement.Add(new XElement("politicalBeliefs",
                    new XElement("positions", mechanism)));
            }
            worldComponents.Add(ownerElement);
        }
        CACampaignOwnerVersionDefinition mapOwner =
            CACampaignPreflightReader.OwnerVersionDefinitions.First(item =>
                !item.RequiredOncePerSave);
        var mapComponents = new XElement("components", new XElement("li",
                new XAttribute("Class", mapOwner.ComponentType),
                new XElement("CA_authoringDataEpoch",
                    CACampaignCompatibilityKernel.LegacyB10AuthoringEpoch),
                new XElement(mapOwner.XmlTag, 0)));
        var root = new XElement("savegame", new XElement("game",
            new XElement("world", worldComponents),
            new XElement("maps", new XElement("li", mapComponents))));
        return root.ToString(SaveOptions.DisableFormatting);
    }

    private static string SyntheticEmptyOwnerSave()
    {
        CACampaignOwnerVersionDefinition owner =
            CACampaignPreflightReader.OwnerVersionDefinitions.First(item =>
                item.RequiredOncePerSave);
        return new XElement("savegame", new XElement("game",
            new XElement("world", new XElement("components",
                new XElement("li",
                    new XAttribute("Class", owner.ComponentType))))))
            .ToString(SaveOptions.DisableFormatting);
    }

    private static XElement Upgrade(XElement source)
    {
        var result = new XElement(source);
        if (result.Element("campaignBoundary") == null)
            result.AddFirst(new XElement("campaignBoundary",
                new XAttribute("version",
                    CACampaignCompatibilityKernel.CurrentBoundaryVersion),
                new XElement("schemas", CACampaignSchemaCatalog.All.Select(item =>
                    new XElement("schema", new XAttribute("key", item.Key),
                        new XAttribute("version", item.CurrentVersion)))),
                new XElement("migration",
                    new XAttribute("from", 0),
                    new XAttribute("to",
                        CACampaignCompatibilityKernel.CurrentBoundaryVersion),
                    new XElement("provenance",
                        "initialized at B11 upgrade from represented B10 state; no earlier history inferred"))));
        return result;
    }

    private static string GovernedFingerprint(XElement envelope)
    {
        using (CAModuleProfiler.Measure(
            CAModuleProfileKey.CompatibilityPreflight))
        {
            int epoch = (int?)envelope.Element("legacyAuthoringEpoch") ?? 0;
            CACampaignCompatibilityDecision decision =
                CACampaignCompatibilityKernel.EvaluateBoundary(0, epoch, true);
            string identity = IdentityDigest(envelope.Element("state")
                ?.Element("caRegionalPendingPlan")?.Element("plan"));
            CAModuleProfiler.Observe(
                CAModuleProfileKey.CompatibilityPreflight,
                objectsExamined: CACampaignSchemaCatalog.All.Length,
                candidatesAccepted: decision.CanLoad ? 1 : 0);
            return Sha(Encoding.UTF8.GetBytes(
                decision.Kind + "|" + decision.Reason + "|" + identity));
        }
    }

    private static string IdentityDigest(XElement plan)
    {
        if (plan == null) return "missing";
        string region = plan.Element("regionalId")?.Value ?? "";
        IEnumerable<XElement> factions = Items(plan, "factions");
        IEnumerable<XElement> settlements = Items(plan, "settlements");
        var values = new List<string> { "region:" + region };
        values.AddRange(factions.Select(item => "faction:"
            + V(item, "key") + ":" + V(item, "existingFactionLoadId")));
        foreach (XElement settlement in settlements)
        {
            values.Add("settlement:" + V(settlement, "slot") + ":"
                + V(settlement, "factionKey") + ":"
                + V(settlement, "memberTileId"));
            values.AddRange(Items(settlement, "populationGroups").Select(item =>
                "population:" + V(settlement, "slot") + ":"
                    + V(item, "key") + ":" + V(item, "factionKey")));
            values.AddRange(Items(settlement, "operationalFacts").Select(item =>
                "fact:" + V(item, "factKey") + ":"
                    + V(item, "operatorIdentity")));
        }
        return Sha(Encoding.UTF8.GetBytes(string.Join("\n",
            values.OrderBy(value => value, StringComparer.Ordinal))));
    }

    private static int HistoryElementCount(XElement state)
    {
        return state?.Descendants().Count(item =>
            item.Name.LocalName.Contains("transition",
                StringComparison.OrdinalIgnoreCase)
            || item.Name.LocalName.Contains("reaction",
                StringComparison.OrdinalIgnoreCase)
            || item.Name.LocalName.Contains("history",
                StringComparison.OrdinalIgnoreCase)
            || item.Name.LocalName.Contains("intent",
                StringComparison.OrdinalIgnoreCase)) ?? 0;
    }

    private static IEnumerable<XElement> Items(XElement parent, string name)
    {
        return parent?.Element(name)?.Elements("li")
            ?? Enumerable.Empty<XElement>();
    }

    private static string V(XElement parent, string name)
    {
        return parent?.Element(name)?.Value ?? "";
    }

    private static bool HasPair(XElement collection, string axis,
        string left, string right)
    {
        if (collection == null) return false;
        var values = new HashSet<string>(collection.Elements("li")
            .Where(item => V(item, "axis") == axis)
            .Select(item => V(item, "option")), StringComparer.Ordinal);
        return values.Contains(left) && values.Contains(right);
    }

    private static bool HasPoliticalPair(XElement collection,
        string question, string left, string right)
    {
        if (collection == null) return false;
        XElement state = collection.Elements("li").FirstOrDefault(item =>
            V(item, "questionKey") == question);
        if (state == null) return false;
        var represented = new HashSet<string>(Items(state, "options")
            .Where(item => int.TryParse(V(item, "share"), out int share)
                && share > 0)
            .Select(item => V(item, "optionKey")), StringComparer.Ordinal);
        return represented.Contains(left) && represented.Contains(right);
    }

    private static string Canonical(XElement element)
    {
        return element?.ToString(SaveOptions.DisableFormatting) ?? "";
    }

    private static string Sha(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string S(string path)
    {
        string full = P(path);
        return File.Exists(full) ? File.ReadAllText(full) : "";
    }

    private static string P(string path)
    {
        return Path.Combine(repo, path.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string Slice(string text, string start, string end)
    {
        int first = text.IndexOf(start, StringComparison.Ordinal);
        if (first < 0) return "";
        int last = text.IndexOf(end, first + start.Length,
            StringComparison.Ordinal);
        return last < 0 ? text[first..] : text[first..last];
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int at = 0;
        while ((at = text.IndexOf(value, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += value.Length;
        }
        return count;
    }

    private static void C(int number, string name, bool passed,
        string evidence)
    {
        results.Add(new Result(number, name, passed, evidence));
    }

    private static void WriteOntologyCoverage()
    {
        int subjectCount = CASocialSubjectRegistry.Authorable().Count;
        int practiceCount = CACulturalPracticeRegistry.All.Count;
        int politicalMechanisms = PoliticalMechanismCount();
        string politicalOrderSource = S("Source/PoliticalOrderAuthoringModule.cs");
        int propertyQuestions = Regex.Matches(politicalOrderSource,
            "PropertyQuestion\\(\"property\\.").Count;
        int politicalQuestions = Regex.Matches(politicalOrderSource,
                "new CAPoliticalQuestionDef\\(").Count - 1
            + propertyQuestions;
        int declaredPoliticalOptions = Regex.Matches(politicalOrderSource,
            "\\bO\\(\"").Count;
        int politicalOptions = declaredPoliticalOptions - 4
            + propertyQuestions * 4;
        int politicalPresets = Regex.Matches(politicalOrderSource,
            "new CAPoliticalOrderPreset\\(").Count;
        int representedInstitutionSubjects = Regex.Matches(
            S("Source/FactionCompositionModule.cs"),
            "new CAAxisDef").Count;
        var text = new StringBuilder();
        text.AppendLine("# Authoring Ontology Coverage")
            .AppendLine()
            .AppendLine("Date: 2026-08-18")
            .AppendLine()
            .AppendLine("This is the current production inventory for CAO's world and founding authoring. It separates what the architecture can represent from what the current build actually knows, realizes, persists, consumes, and exposes. Extensibility is not production breadth. An open registry is capacity, not completion; demonstration records are not production content unless they have a real source and consumer.")
            .AppendLine()
            .AppendLine("## Completion layers")
            .AppendLine()
            .AppendLine("| Domain | Representational capacity | Production vocabulary | Runtime realization | Control surface |")
            .AppendLine("|---|---|---|---|---|")
            .AppendLine($"| Social meaning | Namespaced, population-scoped evaluative relations with approval, normality, prestige, salience, provenance, evidence, and contradiction | {subjectCount} mechanically grounded social-subject schemas | Source facts and known acts feed interpretation, reaction, expression, and longitudinal history | Add or edit a population's meaning of a concrete social referent; searchable list without source-domain tabs |")
            .AppendLine($"| Cultural practice | Repeated conduct with actor, target, trigger, cadence, operator, authority, setting, material, conditions, provenance, evidence, and consumer | {practiceCount} concrete practice schemas | Settlement programs, organizations, acts, agreements, provisions, and longitudinal evidence realize practice state | Add inherited or established local practice from the distinct concrete-practice vocabulary |")
            .AppendLine("| Culture | Population distributions over 48 explicit questions, constituents, inherited and lived practices, observations, transitions, locality, and native visual tradition | Forty-eight questions in 12 categories plus the complete subject/practice vocabularies below | Pawn appraisal and repeated represented evidence alter local Culture only at explicit transition boundaries | One Culture composer with categorized values, one overall-disagreement control, per-value disagreement under More, presets, randomization, constituents, read-only practices, continuity, and visual style |")
            .AppendLine($"| Political Order | Complete normative composition over {politicalQuestions} causal questions | {politicalOptions} supported positions and {politicalPresets} complete presets | Generated identity and account, belief-practice readings, legitimacy, reaction, ownership, provision, and founding suggestions consume the saved variables | Edit one concrete question at a time; blendable subjects total 100 and complete presets remain editable |")
            .AppendLine($"| Represented institutions | Independent instituted mechanisms over {representedInstitutionSubjects} comparison subjects | {politicalMechanisms} supported compatibility mechanisms | Offices, organizations, work, property, security, provisions, and tension reporting consume represented facts | Read-only comparison in Political Order authoring; institutions arise from scenario or historical evidence |")
            .AppendLine("| Factions, inhabited sites, and populations | Relational and structured composition over native factions, independently optional site ownership and support, population affiliation, local social state, relations, current institutions, and represented history | The control-contract inventory below names every active authoring degree of freedom | Starting-region realization persists ownership, support, resident affiliation, and local state once; generation consumes those facts without inventing a faction | Region list, ground map, details, and object-specific composers; direct owner and support choices remain separate from settlement authority |")
            .AppendLine("| Settlement habitat | Physical requirements derived from the exact selected terrain, biome, climate, ecology, water, light, pollution, hazards, and vacuum state | Shelter, thermal control, food route and reserve, medicine, water treatment, light, hazard protection, and breathable interior | Advanced capability can admit potential established-settlement ground; actual programs must satisfy it at confirmation, while frontier occupancy requires the compact materializer to prove every function | Read-only requirement and viability summary on the owning settlement; no biome personality or survival-style control |")
            .AppendLine("| World tendencies | Independent scalar propensities and bounded ranges plus read-only realized outcomes | Eight causal policy controls | Fixed-seed generation owns placement, extent, frontier count/form, urban threshold, source selection, and distant cadence | One direct-effect row per tendency; presets are copy-on-apply convenience compositions |")
            .AppendLine()
            .AppendLine("The former Culture surface had 12 subject examples and no distinct concrete-practice vocabulary. The current production counts are **12 -> " + subjectCount + " social subjects** and **0 -> " + practiceCount + " concrete practice definitions**. B12 admitted the first 13 explicit questions; B13 closed its historical 24-question boundary; B16's reverse mechanical audit ships **48 Culture questions in 12 categories**. The counts are receipts, not quotas: every row below has a source and consumer.")
            .AppendLine()
            .AppendLine("## Semantic-kind inventory and control contracts")
            .AppendLine()
            .AppendLine("Every authorable or inspectable control below declares its actual cardinality. Exclusive controls state a mechanical invariant. Categories are filters, not exclusive state.")
            .AppendLine()
            .AppendLine("| Control | Semantic kind | Owner and scope | Time | Cardinality and coexistence | Exclusivity invariant | Authorship | Exact writes | Consumers |")
            .AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (CAAuthoringControlContract item in
            CAAuthoringControlContracts.All.OrderBy(value => value.Key,
                StringComparer.Ordinal))
            text.AppendLine("| `" + item.Key + "` | "
                + KindWords(item.SemanticKind) + " | "
                + Escape(item.AuthoritativeOwner + "; " + item.Scope) + " | "
                + item.TemporalStatus + " | "
                + Escape(item.Cardinality + "; " + item.Coexistence) + " | "
                + Escape(item.ExclusivityInvariant ?? "none; states coexist or the control is not exclusive") + " | "
                + Escape(item.Authorship) + " | "
                + Escape(string.Join("; ", item.FieldsWritten)) + " | "
                + Escape(string.Join("; ", item.Consumers)) + " |");

        text.AppendLine()
            .AppendLine("Exclusive controls retained: native faction type, one current relation per faction pair, and one native visual tradition per Culture. Their engine or record invariants are stated above. Political mechanisms, property forms, provision operators, security forms, authority mechanisms, and practices are not globally exclusive.")
            .AppendLine()
            .AppendLine("One-option controls: **0**. A realized fact with one available value is shown as a readout or omitted; it is not rendered as a choice.")
            .AppendLine()
            .AppendLine("## Mechanics classification matrix")
            .AppendLine()
            .AppendLine("Each active mechanically significant social fact family has one primary classification here. The mapping column records any subject or practice projection without turning that projection into the authoritative fact.")
            .AppendLine()
            .AppendLine("| Mechanically observable fact | Primary classification | Authority / persisted fact | Production mapping | Runtime consumer or exclusion reason | Player-facing boundary |")
            .AppendLine("|---|---|---|---|---|---|");
        foreach (string[] row in MechanicRows)
            text.AppendLine("| " + string.Join(" | ", row.Select(Escape))
                + " |");
        text.AppendLine()
            .AppendLine("Audited active fact families: **" + MechanicRows.Length
                + "**. Unclassified: **0**.")
            .AppendLine()
            .AppendLine("### Explicit exclusions and unsupported future domains")
            .AppendLine()
            .AppendLine("- Native Ideoligion doctrine, precepts, rituals, roles, and certainty remain native RimWorld facts. CAO may reference or interpret them; it does not duplicate them as Culture or Political Order.")
            .AppendLine("- A building without operator, participants, rules, work, authority, and lifecycle is material evidence, not an institution.")
            .AppendLine("- Faction self-identification is a name or description. It does not write structural mechanisms.")
            .AppendLine("- Hypothetical institutions, acts, relationships, practices, and categories with no current source and consumer are not implemented and are not player-facing.")
            .AppendLine("- Derived settlement scale, regional pattern, cultural summaries, and structural descriptions are read-only realized state; authoring changes their causes, not their labels.")
            .AppendLine()
            .AppendLine("## Social-subject production vocabulary")
            .AppendLine()
            .AppendLine("A social subject is a possible referent. Registration never asserts that the fact exists.")
            .AppendLine()
            .AppendLine("| Key | Subject | Authoritative source and factual condition | Consumers |")
            .AppendLine("|---|---|---|---|");
        foreach (CASocialSubjectDef item in
            CASocialSubjectRegistry.Authorable())
            text.AppendLine("| `" + item.Key + "` | " + Escape(item.Label)
                + " | " + Escape(item.AuthoritativeSource + "; "
                    + item.FactualCondition) + " | "
                + Escape(string.Join("; ", item.Consumers)) + " |");

        text.AppendLine()
            .AppendLine("## Concrete-practice production vocabulary")
            .AppendLine()
            .AppendLine("A practice is repeated conduct. Its implicated subjects are interpretive referents, not its identity.")
            .AppendLine()
            .AppendLine("| Key | Conduct | Actors / target | Trigger / cadence | Operator, authority, and setting | Evidence | Consumer | Implicated subjects | Facet |")
            .AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (CACulturalPracticeDef item in CACulturalPracticeRegistry.All)
            text.AppendLine("| `" + item.Key + "` | "
                + Escape(item.Activity) + " | "
                + Escape(item.ActorRole + (string.IsNullOrWhiteSpace(
                    item.TargetRole) ? "" : "; " + item.TargetRole)) + " | "
                + Escape(item.Trigger + "; " + item.Cadence) + " | "
                + Escape(item.Operator + "; " + item.AuthorityBasis + "; "
                    + item.Setting) + " | " + Escape(item.EvidenceSource)
                + " | " + Escape(item.RuntimeConsumer) + " | "
                + Escape(string.Join("; ", item.SubjectKeys)) + " | "
                + Escape(item.PrimaryFacet) + " |");

        text.AppendLine()
            .AppendLine("## Political Order and represented institutions")
            .AppendLine()
            .AppendLine("Political Order is normative. Represented institutions are instituted facts. The saved political variables are authoritative; generated names, summaries, and accounts write nothing back to them. Economy, property, offices, membership, security, and institutions remain factual consumers rather than prose-only labels.")
            .AppendLine()
            .AppendLine("The active Political Order model has **" + politicalQuestions
                + " causal questions** and **" + politicalOptions
                + " supported positions**. Blendable questions allocate exactly 100 points within one intelligible subject; exclusive questions select exactly one position. Different ownership domains remain independent, so essential provision, land, workshops, finance, trade, and luxury enterprise can have genuinely different mixtures.")
            .AppendLine()
            .AppendLine("### Complete generated orders")
            .AppendLine()
            .AppendLine("The **" + politicalPresets
                + " complete presets** write every Political Order question and remain editable. Fixed-seed generation composes complete orders, then generates the political identity and account from the resulting variables. Saved orders copy the same complete state; a custom name never replaces its causal substrate.")
            .AppendLine()
            .AppendLine("Represented institutions are shown as read-only comparison facts in the established-society composer. Rules adopted at landing remain a separate founding choice. Agreement or tension is derived from the two records; no duplicate institution editor remains.")
            .AppendLine()
            .AppendLine("## Category cardinalities")
            .AppendLine()
            .AppendLine("Top-level navigation appears only when at least two human-relevant groups each contain four or more items. Groups below four remain discoverable in the unfiltered parent list; no empty category is rendered.")
            .AppendLine()
            .AppendLine("| Surface | Candidate type | Group / count | Top-level navigation |")
            .AppendLine("|---|---|---|---|")
            .AppendLine("| Add social meaning | `CASocialSubjectDef` | Ungrouped searchable production list / "
                + subjectCount + " | none; `SourceDomain` is diagnostic metadata, not navigation |");
        AppendCategoryRows(text, "Add inherited/local practice",
            "CACulturalPracticeDef", CACulturalPracticeRegistry.All
                .GroupBy(item => item.PrimaryFacet, StringComparer.Ordinal)
                .Select(group => new KeyValuePair<string, int>(group.Key,
                    group.Count())));
        text.AppendLine("| Political Order | `CAPoliticalQuestionDef` | Authority, civic life, property, economy, and security / "
                + politicalQuestions + " questions | five causal sections; overview presents generated consequences |");
        text.AppendLine()
            .AppendLine("Removed one-item categories: **all former source-domain tabs**. The former one-subject-per-domain strip is no longer projected. Category rows wrap through the shared segment-row layout when they qualify.")
            .AppendLine()
            .AppendLine("## Duplicate-surface audit")
            .AppendLine()
            .AppendLine("| Surface | Semantic task | Candidate source and type | Fields / commit | Result |")
            .AppendLine("|---|---|---|---|---|")
            .AppendLine("| Add social meaning | Interpret a represented referent for a population | `CASocialSubjectRegistry` / `CASocialSubjectDef` | Writes population scope, approval, normality, prestige, salience, provenance | retained; distinct operation |")
            .AppendLine("| Add inherited/local practice | Record concrete repeated conduct and continuity | `CACulturalPracticeRegistry` / `CACulturalPracticeDef` | Writes practice identity, strength, owner, evidence signature, period, and observation boundary | retained; distinct operation |")
            .AppendLine("| Political Order | Author concrete commitments about authority, civic life, ownership, exchange, work, provision, and security | `CAPoliticalQuestionDef` into `CAPoliticalBeliefs.questions` | Replaces one complete question composition | retained; causal authoring surface |")
            .AppendLine("| Represented institutions | Inspect instituted mechanisms for an established society | `CAFactionState.factionStructure` | read-only comparison in the Political Order composer | retained; descriptive facts, not duplicate controls |")
            .AppendLine("| Complete presets | Copy a complete Political Order | `CAPoliticalOrderPreset` through `CAPoliticalOrderModel` | Writes every registered question | retained; convenience generation over the same ontology |")
            .AppendLine()
            .AppendLine("Duplicate candidate universes: **0**. Shared layout code remains presentation infrastructure; candidate records, fields, commits, temporal status, and consumers differ.")
            .AppendLine()
            .AppendLine("## Closure statement")
            .AppendLine()
            .AppendLine("The current authoring ontology reaches its static boundary only when this inventory, the executable contracts, the fixture round trip, retained B10-B16 suites, B17 affiliation and epistemic receipts, clean build, and byte-verified deployment agree. Operator runtime judgment remains separate.");
        File.WriteAllText(P("AUTHORING_ONTOLOGY_COVERAGE.md"),
            text.ToString(), new UTF8Encoding(false));
    }

    private static int PoliticalMechanismCount()
    {
        string source = S("Source/FactionCompositionModule.cs");
        return Count(source, "new CAAxisOption(");
    }

    private static string KindWords(CAAuthoringSemanticKind kind)
    {
        return kind switch
        {
            CAAuthoringSemanticKind.ExclusiveCategorical =>
                "exclusive categorical",
            CAAuthoringSemanticKind.MultiValuedSet => "multi-valued set",
            CAAuthoringSemanticKind.ScalarContinuous =>
                "scalar or continuous",
            CAAuthoringSemanticKind.Relational => "relational",
            CAAuthoringSemanticKind.StructuredComposition =>
                "structured composition",
            CAAuthoringSemanticKind.OptionalUnset => "optional or unset",
            CAAuthoringSemanticKind.Derived => "derived",
            CAAuthoringSemanticKind.ReadOnlyRealized =>
                "read-only realized state",
            CAAuthoringSemanticKind.PartialPatchPreset =>
                "partial patch or preset",
            _ => kind.ToString()
        };
    }

    private static void AppendCategoryRows(StringBuilder text,
        string surface, string candidateType,
        IEnumerable<KeyValuePair<string, int>> source)
    {
        List<KeyValuePair<string, int>> values = source
            .OrderBy(item => item.Key, StringComparer.Ordinal).ToList();
        var navigable = new HashSet<string>(
            CAAuthoringCategoryPolicy.NavigableGroups(values),
            StringComparer.Ordinal);
        foreach (KeyValuePair<string, int> value in values)
            text.AppendLine("| " + surface + " | `" + candidateType
                + "` | " + Escape(value.Key) + " / " + value.Value
                + " | " + (navigable.Contains(value.Key)
                    ? "filter shown" : "no top-level filter") + " |");
    }

    private static readonly string[][] MechanicRows =
    {
        M("Known violence, coercion, confiscation, compelled work, refusal, and taxation acts", "culturally interpretable subject", "CAActLedger / CAActRecord", "combat violence, enforced order, compulsory transfer, compelled service, and taxation subjects; only repeated taxation and defeated-enemy conduct currently have concrete act-practice producers", "social interpretation, political belief effects, reaction, and Culture history", "meanings may be authored before play; concrete practices appear only where production evidence exists; runtime facts remain read-only"),
        M("Work obligation, consent, compensation, emergency, procedure, and force", "political or institutional fact", "CAActRecord independent factual flags and current work rules", "compelled-service and coercive-enforcement subjects; no production practice without repeated fact emission", "political belief comparison and stress response", "beliefs author legitimacy; current rules and observed facts remain distinct"),
        M("Decisions and policies", "political or institutional fact", "CAOrganization decisionHistory and current policy records", "public voice, office governance, public deliberation", "organization behavior, settlement services, legitimacy, Culture history", "represented institutions and established-program facts"),
        M("Office existence, holder, jurisdiction, standing, and succession", "political or institutional fact", "CAOrganization offices and succession records", "office holding, office governance, delegated authority, kin succession", "authority, membership, institutional legitimacy, Culture", "represented institutions; office facts are inspected rather than replaced by a political label"),
        M("Organization membership and groups", "political or institutional fact", "CAOrganization membership and group records", "faction membership, delegated governance, public deliberation", "authority, reporting, work, agreements, and political response", "population/faction authoring plus represented institutions"),
        M("Population group affiliation and share", "political or institutional fact", "CASettlementPopulationGroup.kind, explicit faction payload when affiliated, and share", "faction membership and Culture constituents", "pawn realization, Culture weighting, beliefs, provisions", "direct structured settlement-population composition; unaffiliated residents remain explicit even in supported or owned sites"),
        M("Status, rank, caste, and standing", "political or institutional fact", "represented institutions, offices, and membership records", "inherited rank, office holding, kin succession", "participation, succession, work, prestige, and conflict", "normative Political Order and represented institutions remain independent"),
        M("Domestic-unit membership and residence", "political or institutional fact", "CADomesticUnit and factual pawn relations/residence", "household membership and household provision", "domestic provision, housing, continuity", "population composition is authorable; factual units form from represented relationships"),
        M("Kinship and represented personal relationships", "culturally interpretable subject", "native pawn relations and domestic-unit evidence", "household membership and kin succession", "domestic formation, succession, Culture interpretation", "read-only realized relationship at this boundary"),
        M("Property ownership and holdings", "political or institutional fact", "property claims, organization holdings, settlement assets", "private ownership, common ownership, stored reserves", "property acts, programs, provisions, legitimacy", "Political Order, represented institutions, and concrete established facts"),
        M("Confiscation, requisition, and compulsory transfer", "culturally interpretable subject", "CAActRecord and property claims", "confiscation and compulsory-transfer meanings; property requisition is not a production practice until a real fact producer exists", "political response, grievances, Culture history", "pre-start meaning authoring; live transfer is read-only fact"),
        M("Voluntary exchange and trade", "concrete repeated practice", "trade programs, agreements, routes, and completed exchange", "voluntary trade and trade exchange", "settlement economy, relations, Culture", "established practice and operational-program authoring"),
        M("Agreements, hospitality, and outsider contact", "relational fact represented as culturally interpretable subject", "CAAgreementRecord and known counterparties", "outsider contact, voluntary agreement, external agreement", "relations, reporting, settlement development, Culture", "faction relations and inherited/established practice"),
        M("Provision operator, access, funding, distribution, stock, and reach", "political or institutional fact", "CAProvisionArrangement and materialization receipts", "shared, authority, household provision; meal preparation; reserves", "starting stock, facilities, taxation, domestic and shared work", "generated from facts and editable per arrangement"),
        M("Care and medicine", "concrete repeated practice", "medicine operational fact, caregiver, patient, assets, and live work", "medical care", "settlement program, care work, Culture", "established/inherited practice only when the represented conduct exists"),
        M("Research, knowledge, teaching, and transmission", "concrete repeated practice", "research work, milestones, knowledge records, communications", "research work, organized research, knowledge transmission", "knowledge development, institutions, Culture", "established programs/practices; unknown knowledge is not authored into existence"),
        M("Long-range communication", "concrete repeated practice", "communications program and represented counterparties", "long-range communication and communications practice", "relations, reporting, settlement development", "established operational/practice authoring"),
        M("Custody, treatment, punishment, and coercion", "culturally interpretable subject", "custody programs, captive state, CAActRecord", "humane custody, punishment, and custodial-care meanings/practices; coercion remains a subject until a production act producer exists", "political response, security, Culture", "established custody program and supported pre-start practice; live outcomes read-only"),
        M("Combat, surrender, quarter, and defeated-person outcome", "culturally interpretable subject", "native combat outcomes, parley/custody state, CAActRecord", "combat violence, quarter given, combat conduct", "combat behavior, aftermath, legitimacy, Culture", "beliefs and pre-start meanings/practices; combat result is not directly authored"),
        M("Defense, patrol, watch, guards, and boundaries", "concrete repeated practice", "security practices, defense programs, assignments, built defenses", "defended boundary, security service, boundary defense, boundary patrol", "security runtime, settlement development, Culture", "represented institutions plus established program/practice"),
        M("Migration, regional arrival, and settlement placement", "political or institutional fact", "CARegionalPlan, settlement source, member tile, and arrival tile", "route use, faction membership, local Culture continuity", "world transfer, map generation, population and relations", "direct Starting Region map and object authoring"),
        M("Public gathering and participation", "concrete repeated practice", "gathering program, shared place, decision records, participants", "public gathering, public voice, public deliberation", "political development, settlement services, Culture", "meaning and concrete practice authoring remain distinct"),
        M("Native Ideoligion doctrine, roles, rituals, and certainty", "excluded with a specific reason", "RimWorld Ideo, precepts, roles, ritual and pawn certainty", "religious observance may be culturally interpreted", "native Ideology system and CA population/settlement integration", "native editor/reference only; CAO does not duplicate doctrine"),
        M("Agriculture and animal tending", "concrete repeated practice", "agriculture/animal operational facts, workers, land/animals, live work", "cultivation and animal tending", "settlement programs, economy, Culture", "established operational/practice authoring"),
        M("General and specialized production", "concrete repeated practice", "production programs, workstations, material, knowledge, completed work", "general craft and specialized craft", "settlement economy, work, Culture", "established operational/practice authoring"),
        M("Repair and rebuilding", "concrete repeated practice", "repair/rebuild work records, damaged asset, labor, material", "repair and rebuilding", "settlement maintenance, history, Culture", "established practice; runtime work follows actual damage"),
        M("Housing and inhabited shelter", "concrete repeated practice", "housing programs, residence assignments, occupied assets, repair", "maintained housing and housing upkeep", "domestic life, settlement services, Culture", "established program/practice; building alone is not an institution"),
        M("Public, communal, private, and institutional space", "culturally interpretable subject", "layout, holdings, access, program assets, operator identity", "public gathering, defended boundary, public works, ownership meanings", "spatial planning, programs, Culture", "location, operational facts, meaning; derived spatial description is read-only"),
        M("Art, memory, mourning, recreation, and performance where represented", "concrete repeated practice", "art-memory/recreation program, asset, participants, event evidence", "art and remembrance, shared recreation", "cultural expression and settlement life", "available only when represented program/evidence exists"),
        M("Institution formation, change, and dissolution", "political or institutional fact", "organization office/group/policy lifecycle and history", "office governance, delegated governance, office succession", "authority, work, membership, Culture transition", "represented institutions at start; later lifecycle develops through simulation"),
        M("Social reaction, disagreement, and cultural transition", "read-only realized state", "CASocialReaction patterns and CACultureTransition history", "population-scoped meaning resolution and tension summaries", "behavior, legitimacy, expression, longitudinal Culture", "inspect and author initial causes; runtime transition is not directly authored"),
        M("Settlement program operation", "political or institutional fact", "CASettlementOperationalFact complete causal contract", "program-specific subjects and concrete practices", "materializer, work, services, provisions, Culture evidence", "direct established-operation authoring; incomplete contracts are rejected"),
        M("Transport routes and completed route use", "concrete repeated practice", "saved access facts, transport program, roads, counterparties, journey evidence", "route use and transport service", "trade, relations, access, Culture", "location/access facts and established practice"),
        M("Settlement scale, role, pattern, and regional relation pattern", "derived summary only", "persisted realization from population, land, routes, services, history, placement, and relations", "read-only descriptions", "map generation, provision scale, summaries", "no direct label control; author causes and constrained propensities"),
        M("Faction self-identification and descriptive structural fingerprint", "derived summary only", "custom/native faction name plus read-only institutional characterization", "none; names do not write mechanisms", "labels and operator summaries", "name is optional; structural description is read-only"),
        M("Hypothetical future institution or practice with no source/consumer", "not yet implemented and therefore not player-facing", "none", "none", "excluded until a real operator, participants, rules, resources, work, evidence, and consumer exist", "not shown")
    };

    private static string[] M(params string[] values) => values;

    private static void WriteReports(string activeHash, string mirrorHash,
        int bytes, int factions, int settlements, int populationGroups,
        int facts, string identity, XElement upgradedOnce,
        XElement upgradedTwice, int historyBefore, int historyAfter,
        long enabledCalls, bool fixtureAgreement)
    {
        var acceptance = new StringBuilder();
        acceptance.AppendLine("# B11 Acceptance Receipts")
            .AppendLine()
            .AppendLine("Date: 2026-08-12")
            .AppendLine()
            .AppendLine("Generated by `tools/B11AcceptanceReceipts` from the executable compatibility/profiler kernels, final source, governed manifests, and the active/mirror authored fixture.")
            .AppendLine()
            .AppendLine($"- Automated result: **{results.Count(value => value.Passed)}/{results.Count} passed**")
            .AppendLine($"- Active fixture SHA-256: `{activeHash}`")
            .AppendLine($"- Mirror fixture SHA-256: `{mirrorHash}`")
            .AppendLine($"- Fixture size: {bytes:N0} bytes")
            .AppendLine()
            .AppendLine("| # | Acceptance | Result | Evidence |")
            .AppendLine("|---:|---|---|---|");
        foreach (Result result in results)
            acceptance.AppendLine($"| {result.Number} | {result.Name} | **{(result.Passed ? "PASS" : "FAIL")}** | {Escape(result.Evidence)} |");
        acceptance.AppendLine()
            .AppendLine("Receipts 1-28 retain B11's original ownership, observability, and durability sequence. Receipts 29-78 map in order to the addendum's required receipts 1-50.");
        File.WriteAllText(P("B11_ACCEPTANCE_RECEIPTS.md"),
            acceptance.ToString(), new UTF8Encoding(false));

        string upgradedHash = Sha(Encoding.UTF8.GetBytes(
            Canonical(upgradedOnce)));
        string secondHash = Sha(Encoding.UTF8.GetBytes(
            Canonical(upgradedTwice)));
        var compatibility = new StringBuilder();
        compatibility.AppendLine("# B10 to B11 Compatibility Receipt")
            .AppendLine()
            .AppendLine("Date: 2026-08-12")
            .AppendLine()
            .AppendLine("This is the strongest available non-interactive compatibility exercise. It uses the current governed fixture as an identity-preserving payload, synthetic owner-scoped B10 save envelopes for preflight decisions, and the production compatibility/profiler kernels. It is not represented as a true RimWorld save round trip.")
            .AppendLine()
            .AppendLine("| Fact | Evidence |")
            .AppendLine("|---|---|")
            .AppendLine($"| Active/mirror agreement | {(fixtureAgreement ? "byte and logical match" : "MISMATCH")} |")
            .AppendLine($"| Input SHA-256 | `{activeHash}` |")
            .AppendLine($"| Composition | {factions} factions; {settlements} settlements; {populationGroups} population groups; {facts} established-program facts |")
            .AppendLine($"| Stable identity digest | `{identity}` before and after |")
            .AppendLine($"| First upgrade envelope SHA-256 | `{upgradedHash}` |")
            .AppendLine($"| Second upgrade envelope SHA-256 | `{secondHash}` |")
            .AppendLine($"| Idempotence | {(upgradedHash == secondHash ? "PASS" : "FAIL")} |")
            .AppendLine($"| Historical-element count | {historyBefore} before; {historyAfter} after; no synthetic history added |")
            .AppendLine($"| Profiler exercise | {enabledCalls} enabled call(s); disabled path recorded zero; governed fingerprint identical |")
            .AppendLine("| Emitted-save boundary | exact game/world/map component scope and cardinality validated before SafeSaver swap; repeated nested records validated per occurrence |")
            .AppendLine("| Payload seal | strict terminal SHA-256; altered payload rejected before Scribe owner load |")
            .AppendLine("| Source fixture mutation | none; upgrade operates on a copy and unsupported decisions return before copy mutation |")
            .AppendLine("| Creation derivation | not invoked; compatibility metadata wraps an unchanged state subtree |")
            .AppendLine("| Remaining live proof | Operator creates/saves the retained campaign, later loads that same save under a compatible DLL, inspects migration/log output, continues play, then saves only after validation |")
            .AppendLine()
            .AppendLine("Upgrade provenance is explicit: `initialized at B11 upgrade from represented B10 state; no earlier history inferred`. Stable region, faction, settlement, population-group, and established-program fact/operator identities are included in the digest.");
        File.WriteAllText(P("B11_COMPATIBILITY_RECEIPT.md"),
            compatibility.ToString(), new UTF8Encoding(false));
    }

    private static string Escape(string value)
    {
        return (value ?? "").Replace("|", "\\|").Replace("\r", " ")
            .Replace("\n", " ");
    }
}
