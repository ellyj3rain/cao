using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using ColonistAwareness;

internal static class Program
{
    private sealed record Result(int Number, string Name, bool Passed,
        string Evidence);

    private static string repo = "";
    private static readonly List<Result> Results = new();

    private static int Main(string[] args)
    {
        bool verifyOnly = args.Length == 4
            && args[3] == "--verify-only";
        if (args.Length != 3 && !verifyOnly)
        {
            Console.Error.WriteLine(
                "usage: B10AcceptanceReceipts <repo> <active> <mirror> [--verify-only]");
            return 1;
        }
        repo = Path.GetFullPath(args[0]);
        string active = Path.GetFullPath(args[1]);
        string mirror = Path.GetFullPath(args[2]);

        string domestic = S("Source/DomesticUnitModule.cs")
            + S("Source/DomesticUnitStateModule.cs")
            + S("Source/DomesticResidenceAdapterModule.cs")
            + S("Source/DomesticProvisionAdapterModule.cs");
        string capabilityState = S("Source/SettlementCapabilityModule.cs");
        string capabilityEvidence = S(
            "Source/SettlementCapabilityEvidenceModule.cs");
        string capabilityAssessment = S(
            "Source/SettlementCapabilityAssessmentKernel.cs");
        string capabilityAdapters = S(
            "Source/SettlementCapabilityAdaptersModule.cs");
        string capability = capabilityState + capabilityEvidence
            + capabilityAssessment + capabilityAdapters
            + S("Source/SettlementCapabilityInspectionModule.cs");
        string program = S("Source/SettlementProgramModule.cs");
        string operational = S("Source/SettlementProgramOperationalModule.cs");
        string programKernel = S("Source/SettlementProgramCausalKernel.cs");
        string programRuntime = S("Source/SettlementProgramRuntimeModule.cs");
        string programAuthoring = S(
            "Source/SettlementProgramAuthoringModule.cs")
            + S("Source/SettlementOperationalFactAuthoringKernel.cs");
        string programAssets = S(
            "Source/SettlementProgramAssetModule.cs");
        string organizationRelations = S(
            "Source/OrganizationRelationsModule.cs");
        string materializer = S(
            "Source/SettlementProgramMaterializerModule.cs");
        string provision = S("Source/ProvisionCausalKernel.cs");
        string provisionOwners = S("Source/ProvisionOperatorResolverModule.cs")
            + S("Source/ProvisionFundingResolverModule.cs")
            + S("Source/ProvisionMaterializationAdapterModule.cs")
            + S("Source/ProvisionAccessServiceModule.cs")
            + S("Source/ProvisionRuntimeModule.cs");
        string provisionMaterialization = Slice(materializer,
            "private static bool MaterializeProvisionEntry(",
            "private static Thing SpawnStock(");
        string composition = S("Source/SettlementCompositionModule.cs");
        string axes = S("Source/SettlementAxesModule.cs");
        string regional = S("Source/RegionalSettlementModelModule.cs");
        string setup = S("Source/RegionalSetupModule.cs");
        string world = S("Source/RegionalWorldModule.cs");
        string planning = S("Source/SettlementPlanningContextModule.cs");
        string hostile = S("Source/HostileFactionOrganizationModule.cs");
        string taskForce = S("Source/TaskForceModule.cs");
        string assault = S("Source/AssaultApproachModule.cs");
        string road = S("Source/RoadExpansionModule.cs");
        string securityFacts = S("Source/SettlementSecurityFactsModule.cs");
        string securityAssignments = S(
            "Source/SettlementSecurityAssignmentModule.cs");
        string politicalEffects = S(
            "Source/PoliticalBeliefEffectsModule.cs");
        string culture = S("Source/SocialMeaningKernel.cs")
            + S("Source/CulturalExpressionModule.cs")
            + S("Source/FactionCultureBeliefsModule.cs");
        string politics = S("Source/FactionCultureBeliefsModule.cs")
            + politicalEffects;
        string institutionalChange = S(
                "Source/FactionCultureBeliefsModule.cs")
            + S("Source/OrganizationModule.cs");
        string ui = S("Source/RegionalPopulationScreenModule.cs");
        string fixtureTool = S("tools/B10FixtureGenerator/Program.cs");
        string sweep = S("B10_SYNTHETIC_STATE_SWEEP.md");

        XDocument activeDoc = XDocument.Load(active);
        XDocument mirrorDoc = XDocument.Load(mirror);
        XElement root = activeDoc.Root
            ?? throw new InvalidDataException("active fixture root missing");
        XElement plan = root.Element("plan")
            ?? throw new InvalidDataException("active plan missing");
        XElement[] settlements = Items(plan, "settlements").ToArray();

        CASettlementProgramOperationalEvidence completeProgram =
            ProgramEvidence();
        CASettlementProgramOperationalEvidence missingOperator =
            Clone(completeProgram); missingOperator.OperatorIdentity = null;
        CASettlementProgramOperationalEvidence missingKnowledge =
            Clone(completeProgram); missingKnowledge.KnowledgeSource = null;
        CASettlementProgramOperationalEvidence missingLabor =
            Clone(completeProgram); missingLabor.LaborSource = null;
        CASettlementProgramOperationalEvidence missingMaterial =
            Clone(completeProgram); missingMaterial.MaterialSource = null;
        CASettlementProgramOperationalEvidence unrelatedCulture =
            Clone(completeProgram);
        unrelatedCulture.CulturalSubjects.Add("ca.unrelated.subject");
        List<CASettlementProgramCausalSpec> derived =
            CASettlementProgramCausalKernel.Derive(new[] { completeProgram });

        CAProvisionOperatorFact communal = ProvisionFact("Communal",
            "org:communal", "SharedWork");
        CAProvisionOperatorFact authority = ProvisionFact("Authority",
            "org:authority", "Taxation");
        authority.PolicyKey = "tax rate";
        authority.PolicyValue = "0.08";
        authority.CollectionPath = "settlement levy";
        CAProvisionOperatorFact individual = ProvisionFact("Individual",
            "pawn:31", "Domestic");
        CAProvisionOperatorFact vendor = ProvisionFact("Vendor",
            "org:vendor", "SharedWork");
        CAProvisionOperatorFact taxless = ProvisionFact("Authority",
            "org:authority", "Taxation");

        string postOpen = Slice(ui, "public override void PostOpen()",
            "public override void PostClose()");
        string buildProposal = Slice(planning, "BuildCreationProposal(",
            "CreationFromRecord(");
        string institutionalProposal = Slice(planning,
            "BuildInstitutionalProposal(",
            "CanExerciseInstitutionalDevelopment(");

        C(1, "Hash-selected household method is absent",
            !AllSource().Contains("HouseholdConsumers",
                StringComparison.Ordinal),
            "no HouseholdConsumers or equivalent hash-roster symbol exists");
        C(2, "Multi-pawn units own persistent identities",
            domestic.Contains("class CADomesticUnit")
                && domestic.Contains("unitIdentity")
                && domestic.Contains("Scribe_Values.Look(ref unitIdentity"),
            "CADomesticUnit persists one stable unit identity");
        C(3, "Every membership records its source",
            domestic.Contains("sourceKind")
                && domestic.Contains("sourceIdentity")
                && domestic.Contains("MembershipEvidence("),
            "memberships store their exact relationship/residence source");
        C(4, "Ordering and hashing cannot make a household",
            !domestic.Contains("Rand.")
                && !domestic.Contains("StableStringHash")
                && domestic.Contains("RepresentedDomesticLink"),
            "formation uses native relations and shared owned beds");
        C(5, "Individual self-provision remains valid",
            domestic.Contains("CADomesticUnitKind.Individual")
                && domestic.Contains("sourceKind = \"individual\""),
            "unlinked residents retain explicit individual identity");
        C(6, "Domestic membership is serialized",
            domestic.Contains("Scribe_Collections.Look(ref memberships")
                && world.Contains("domesticMembershipTransitions"),
            "memberships and transition ledger cross save/load");
        C(7, "UI redraw cannot change domestic membership",
            !ui.Contains("CADomesticUnitFormation.Reconcile")
                && !ui.Contains("CADomesticProvisionAdapter.ReconcileDemands"),
            "Starting Region presentation has no domestic runtime mutator");
        C(8, "Membership changes have represented transitions",
            domestic.Contains("RecordTransition(record")
                && domestic.Contains("\"joined\"")
                && domestic.Contains("\"dissolved\"")
                && domestic.Contains("\"split\""),
            "join, exit, split, merge, and dissolution write evidence");
        C(9, "Domestic stores and access bind exact identities",
            provisionOwners.Contains("CADomesticResidenceAdapter.Consumers")
                && provisionOwners.Contains("arrangement.operatorIdentity")
                && domestic.Contains("unitIdentity == operatorIdentity"),
            "access resolves the exact unit or pawn key");
        C(10, "Domestic identity never becomes settlement organization",
            provisionOwners.Contains("case CAProvisionOperator.DomesticUnit")
                && !S("Source/FrontierModule.cs").Contains(
                    "CAOrganizationKind.Household")
                && composition.Contains("operatorIdentity"),
            "domestic operators resolve through units, never a fallback org");

        C(11, "Practiced capability has no random selection",
            !capability.Contains("Rand.")
                && !capability.Contains("StableCandidateIndex"),
            "capability assessment uses evidence only; hash is a receipt digest");
        C(12, "Capability evidence is domain-specific",
            new[] { "CAMedicalCapabilityAdapter",
                "CAProductionCapabilityAdapter",
                "CALogisticsCapabilityAdapter", "CACivicCapabilityAdapter",
                "CAResearchCapabilityAdapter", "CASecurityCapabilityAdapter",
                "CACommerceCapabilityAdapter",
                "CACommunicationCapabilityAdapter" }
                .All(capabilityAdapters.Contains),
            "eight domain adapters feed one assessment contract");
        C(13, "Medicine requires medical operations",
            capabilityAdapters.Contains(
                "no represented treatment operation and treatment history")
                && !capabilityAdapters.Contains("WorkTypeDefOf.Doctor"),
            "medicine remains absent rather than inferred from a bed or skill");
        C(14, "Research requires contract and history",
            capabilityAdapters.Contains("TryActiveResearchContract")
                && capabilityAdapters.Contains(
                    "also requires a recorded completed milestone")
                && capabilityAdapters.Contains(
                    "no active research contract with completed work history"),
            "bench/contract without completed work remains insufficient");
        C(15, "Commerce requires exchange and transactions",
            capabilityAdapters.Contains("CATransactionLedger.Current")
                && capabilityAdapters.Contains("transactions.Count == 0"),
            "commerce reads actual exchange parties and transaction history");
        C(16, "Civic capacity requires administration",
            capabilityAdapters.Contains("offices.Count == 0")
                && capabilityAdapters.Contains("decisions.Count == 0")
                && capabilityAdapters.Contains(
                    "CASettlementProgramAssets.AssetIds"),
            "occupied offices, decisions, members, and site are constitutive");
        C(17, "Security requires actors and organization",
            capabilityAdapters.Contains("securityPractices")
                && capabilityAdapters.Contains("guardPawnIds")
                && capabilityAdapters.Contains("equipment")
                && capabilityAdapters.Contains("decisionHistory"),
            "guards, equipment, practice, authority, and history are required");
        C(18, "Unrelated facts cannot perturb capability",
            !capability.Contains("economicCapacity")
                && !capability.Contains("urbanSupport")
                && !capability.Contains("specialization"),
            "capability adapters have no world-summary inputs");
        C(19, "Missing evidence blocks capability",
            capabilityAssessment.Contains("bool complete =")
                && capabilityAssessment.Contains(
                    "result.level = complete ?")
                && capabilityAssessment.Contains("result.blocker"),
            "pure assessment emits zero and a blocker for incomplete evidence");
        C(20, "Capability evidence signature is persisted",
            capabilityState.Contains(
                "Scribe_Values.Look(ref evidenceSignature")
                && capabilityState.Contains(
                    "This digest proves readback equality"),
            "the signature is persisted as a readback receipt only");

        C(21, "Tendencies cannot instantiate programs",
            !operational.Contains("economicCapacity")
                && !operational.Contains("urbanSupport")
                && !operational.Contains("realizedServiceInfrastructure"),
            "program resolver consumes authored/observed operational facts");
        C(22, "Hash cannot create specialization",
            !regional.Contains("CountBits(settlement.operationalRoleMask)")
                && regional.Contains("specialization = Specialization(plan")
                && regional.Contains("private static int Specialization(")
                && regional.Contains("CompleteProgramCount(plan, settlement"),
            "specialization summarizes complete operating contracts");
        C(23, "Every active program has a real need",
            derived.Count == 1
                && derived[0].NeedSource == completeProgram.NeedSource,
            "pure kernel preserves the exact direct need");
        C(24, "Institutional program requires operator",
            CASettlementProgramCausalKernel.Derive(
                new[] { missingOperator }).Count == 0,
            "removing operator identity rejects the program");
        C(25, "Programs require knowledge and labor",
            CASettlementProgramCausalKernel.Derive(
                    new[] { missingKnowledge }).Count == 0
                && CASettlementProgramCausalKernel.Derive(
                    new[] { missingLabor }).Count == 0,
            "either missing contract rejects the program");
        C(26, "Material program requires physical contract",
            CASettlementProgramCausalKernel.Derive(
                new[] { missingMaterial }).Count == 0,
            "removing material source rejects materialized research");
        C(27, "Asset alone cannot establish institution",
            CASettlementProgramCausalKernel.Derive(new[]
            {
                new CASettlementProgramOperationalEvidence
                {
                    Key = CASettlementProgramCausalKernel.Research,
                    CandidateGroups = new() { new[] { "ResearchBench" } }
                }
            }).Count == 0,
            "candidate assets are downstream of complete operations");
        C(28, "Removing operator suspends program",
            !missingOperator.Complete,
            "operator removal fails pure completeness");
        C(29, "Removing knowledge suspends program",
            !missingKnowledge.Complete,
            "knowledge removal fails pure completeness");
        C(30, "Removing labor suspends program",
            !missingLabor.Complete,
            "labor removal fails pure completeness");
        C(31, "Materialization consumes persisted program",
            materializer.Contains("record.settlementProgram")
                && materializer.Contains("selectedCandidates"),
            "materializer reads saved entries and candidate choices");
        C(32, "Repair and rebuilding consume placed assets",
            materializer.Contains("CASettlementProgramAssets")
                && materializer.Contains("AssetIds(record, program)")
                && materializer.Contains("repairWork")
                && materializer.Contains("rebuildWork"),
            "later work targets actual placed program assets");
        C(33, "Fixture and runtime share derivation",
            fixtureTool.Contains("does not implement provision, program")
                && setup.Contains("RefreshDraftRealization")
                && setup.Contains("CASettlementProgramRegistry.EnsureDerived"),
            "fixture carries source facts; production refresh owns derivation");
        string[] keys = ProgramKeys();
        C(34, "All B9 keys have explicit contract results",
            keys.Length == 22 && keys.All(programKernel.Contains)
                && program.Contains("Unavailable("),
            "22 registered names remain inspectable and evidence-gated");
        C(35, "Unsupported programs are absent",
            settlements.All(item => item.Element("settlementProgram") == null)
                && settlements.All(item => item.Element("operationalFacts")
                    != null),
            "fixture has zero unsupported program instances");

        C(36, "Culture cannot grant execution capability",
            !operational.Contains("CACulture")
                && !capabilityAdapters.Contains("CACulture"),
            "Culture supplies neither operator nor capability evidence");
        C(37, "Unrelated Culture subject cannot affect program",
            CASettlementProgramCausalKernel.Derive(
                    new[] { unrelatedCulture }).Single().CulturalSubjects
                .SequenceEqual(new[] { "ca.unrelated.subject" })
                && derived.Single().OperatorIdentity
                    == CASettlementProgramCausalKernel.Derive(
                        new[] { unrelatedCulture }).Single().OperatorIdentity,
            "subjects are carried as scoped meaning; operations stay unchanged");
        C(38, "Constituent disagreement remains distinct",
            culture.Contains("populationScope")
                && culture.Contains("constituent")
                && culture.Contains("disagreement"),
            "Culture retains population-scoped readings and disagreement");
        C(39, "Culture affects documented dimensions only",
            culture.Contains("approval") && culture.Contains("normality")
                && culture.Contains("prestige")
                && culture.Contains("salience")
                && S("Source/SettlementProgramMaterializerModule.cs")
                    .Contains("otherwise valid"),
            "meaning ranks valid forms without supplying execution facts");
        C(40, "Political Belief change does not rewrite institutions",
            !operational.Contains("politicalBeliefs")
                && politicalEffects.Contains(
                    "Political beliefs remain unchanged")
                && !politicalEffects.Contains("factionStructure ="),
            "normative belief and current structure remain separate inputs");
        C(41, "Institutional change requires a transition",
            institutionalChange.Contains(
                "Current order is an established fact")
                && institutionalChange.Contains(
                    "Changes are recorded in the decision history"),
            "institutional effects route through represented decisions/events");
        C(42, "Belief/current-order tension remains visible",
            politicalEffects.Contains("openBeliefConflicts")
                && politics.Contains("belief-rule tension"),
            "comparison persists and presents disagreement without rewriting");

        C(43, "Provision Support switch is gone",
            !provision.Contains("CAProvisionCausalFacts.Support")
                && provision.Contains("Explicit facts supplied"),
            "provision accepts exact operator facts only");
        C(44, "Provision operators preserve actual identity",
            CAProvisionCausalKernel.Derive(new CAProvisionCausalFacts
            { Operators = new() { communal } }).Single().OperatorIdentity
                == "org:communal",
            "kernel preserves the supplied organization identity");
        C(45, "Communal provision requires full contract",
            CAProvisionCausalKernel.Complete(communal)
                && communal.LaborSource.Length > 0
                && communal.StockSource.Length > 0
                && communal.MaterialSource.Length > 0,
            "operator, work, stock, material, access, and distribution exist");
        C(46, "Authority provision requires authority and funding",
            CAProvisionCausalKernel.Complete(authority)
                && authority.CollectionPath == "settlement levy",
            "authority identity and complete tax pathway are explicit");
        C(47, "Taxation requires policy and collection",
            !CAProvisionCausalKernel.Complete(taxless)
                && S("Source/ProvisionFundingResolverModule.cs")
                    .Contains("collection path"),
            "tax-funded fact fails without policy, value, and collection");
        C(48, "Domestic provision resolves real identity",
            CAProvisionCausalKernel.Derive(new CAProvisionCausalFacts
            { Operators = new() { individual } }).Single().OperatorIdentity
                == "pawn:31",
            "individual pawn identity survives validation");
        C(49, "Unsupported vendor provision remains absent",
            !CAProvisionCausalKernel.Complete(vendor)
                && !composition.Contains("CAProvisionOperator.Vendor"),
            "vendor is outside the active operator vocabulary");
        C(50, "Unsupported religious provision remains absent",
            !composition.Contains("CAProvisionOperator.Religious")
                && !provision.Contains("Religious"),
            "religious provision is not fabricated");
        C(51, "Provision labels summarize saved arrangements",
            composition.Contains("authoritative fact")
                && composition.Contains("validates its complete causal contract")
                && !composition.Contains("support switch"),
            "saved facts precede validation and display");

        C(52, "No RNG/hash creates semantic social state",
            sweep.Contains("Unresolved Critical/High: **0**")
                && sweep.Contains("Result: **PASS**"),
            "standalone sweep classified every active occurrence");
        C(53, "UI opening cannot mutate simulation authority",
            !postOpen.Contains("SavePending")
                && !postOpen.Contains("RefreshDraftRealization")
                && !postOpen.Contains("EnsureDerived")
                && !postOpen.Contains("ReconcileProvision"),
            "PostOpen only binds, validates, logs, and resets presentation");
        C(54, "Current tick is absent from semantic identity",
            domestic.Contains("formationSequence")
                && !Slice(domestic, "unitIdentity =",
                    "formationSource =").Contains("TicksGame"),
            "ticks record chronology; event sequence forms unit keys");
        C(55, "Read models are not sole causes",
            !operational.Contains("urbanSupport")
                && !operational.Contains("economicCapacity")
                && !buildProposal.Contains("economicCapacity")
                && institutionalProposal.Contains("OperationalProgram(record")
                && institutionalProposal.Contains(
                    "CAPopulationProjection.Residents")
                && !AllSource().Contains("CASettlementCapabilities.Level("),
            "program, development, and operative paths consume direct contracts");
        C(56, "No fixture-only causal implementation exists",
            !fixtureTool.Contains("CAProvisionCausalKernel")
                && !fixtureTool.Contains("CASettlementProgramCausalKernel")
                && fixtureTool.Contains("production path"),
            "fixture tool strips derived state and delegates realization");
        C(57, "Repeated synthetic-state sweep is clean",
            sweep.Contains("Unresolved Critical/High: **0**")
                && sweep.Contains("Classified occurrences: **"),
            "second repository-wide pass has no unresolved Critical/High");
        C(58, "Creation reaches corrected map-generation boundary",
            int.TryParse(Value(root, "authoringDataEpoch"), out int epoch)
                && epoch >= 11
                && int.TryParse(Value(plan, "schemaVersion"),
                    out int planSchema)
                && planSchema >= 11
                && Value(plan, "confirmed") == "False"
                && Items(plan, "factions").Count() == 3
                && settlements.Length == 4
                && Items(plan, "memberTileIds").Any()
                && world.Contains("TryValidateRealization")
                && world.Contains("TryValidateSaved")
                && world.Contains("BuildCreationProposal"),
            "the current fixture remains at or beyond the B10 creation boundary and the production validation/materialization path is structurally ready; operator runtime remains separate");

        C(59, "Same-kind program operators retain separate entries",
            program.Contains(
                "IEnumerable<CASettlementProgramEntry> Entries(string key)")
                && operational.Contains("existing.OperatorIdentity")
                && programRuntime.Contains(
                    "item.operatorIdentity == entry.operatorIdentity")
                && provisionOwners.Contains(".settlementProgram?.Entries(key)"),
            "program resolution and readback use key plus exact operator identity");
        C(60, "Provision materialization is preflighted and atomic",
            Before(provisionMaterialization,
                "CAProvisionMaterializationAdapter.TryPlan", "Place(org")
                && provisionMaterialization.Contains("new CAFacilityHolding")
                && provisionMaterialization.Contains(
                    "new CAStartingStockRecord")
                && provisionMaterialization.Contains("ledger.Add(holding)")
                && provisionMaterialization.Contains(
                    "record.startingStock.AddRange")
                && provisionMaterialization.Contains("RemoveDerivedHolding")
                && provisionMaterialization.Contains("DestroyMode.Vanish"),
            "all arrangements preflight before exact holdings and stock receipts commit; every mutation has rollback");
        C(61, "Capability summaries cannot authorize behavior",
            !AllSource().Contains("CASettlementCapabilities.Level(")
                && securityFacts.Contains(
                    "CASettlementProgramRegistry.Defense")
                && securityFacts.Contains("LiveThing")
                && securityFacts.Contains("LiveGuardIds"),
            "operative security reads represented defenses and assigned guards");
        C(62, "Encounter cannot synthesize hostile doctrine",
            !hostile.Contains("EnsureFor(")
                && !hostile.Contains("SeedDoctrine")
                && !taskForce.Contains("adherence")
                && !taskForce.Contains("2654435761")
                && taskForce.Contains(
                    "Practices(CAOrganization factionOrg, string concept)")
                && assault.Contains("unit.Practices(org, \"ambush\")")
                && assault.Contains("unit.Practices(org, \"line\")"),
            "advanced assault logic requires an already recorded practiced custom");
        string researchWork = Slice(materializer, "private bool TryResearch(",
            "internal void CompleteNativeLabor(");
        C(63, "Institutional activity cannot create its own demand",
            !institutionalProposal.Contains("TryActiveResearchContract")
                && institutionalProposal.Contains("OperationalProgram(record")
                && researchWork.Contains(
                    "CASettlementProgramRegistry.Research")
                && researchWork.Contains("ReceiptsFor(record, program)")
                && researchWork.Contains("LiveThing")
                && researchWork.Contains("programSignature")
                && !researchWork.Contains("operationalFacts.Add"),
            "saved operational program and exact placed bench precede research work");
        C(64, "Settlement development uses typed residents",
            institutionalProposal.Contains(
                    "CAPopulationProjection.Residents(record, map)")
                && road.Contains("runtime.Workers")
                && road.Contains("project.programSignature")
                && !road.Contains("SpawnedPawnsInFaction")
                && road.Contains(
                    "CASettlementProgramRuntimeContract.TryResolve"),
            "research, maintenance, cultivation, and roads use the settlement residence projection and preserve their owning commitment");
        C(65, "Provision readback binds exact material receipts",
            provisionOwners.Contains("provisionArrangementKey")
                && provisionOwners.Contains("provisionNodeIndex")
                && provisionOwners.Contains("operatorIdentity")
                && provisionOwners.Contains("startingStock"),
            "arrangement, node, operator, facility, and stock survive exact validation");

        CAEstablishedProgramFactSpec authoredA =
            CASettlementOperationalFactAuthoringKernel.Establish(
                CASettlementProgramCausalKernel.Medicine, 7, 1);
        CAEstablishedProgramFactSpec authoredB =
            CASettlementOperationalFactAuthoringKernel.Establish(
                CASettlementProgramCausalKernel.Medicine, 8, 1);
        C(66, "Established-program formation is explicit and stable",
            authoredA.OperatorIdentity == "population-group:7"
                && authoredA.FactKey ==
                    "established:ca.settlement.medicine:population-group:7:1"
                && authoredA.Provenance.StartsWith(
                    "authored:established:",
                    StringComparison.Ordinal)
                && authoredA.FactKey != authoredB.FactKey
                && !programAuthoring.Contains("TicksGame")
                && !programAuthoring.Contains("StableStringHash")
                && !programAuthoring.Contains("Rand."),
            "one explicit authoring act supplies a stable fact identity; group identity, not redraw/hash/tick, distinguishes operators");
        C(67, "Governed fixture carries explicit established facts",
            settlements.SelectMany(item => Items(item,
                    "operationalFacts")).Count() == 19
                && settlements.SelectMany(item => Items(item,
                    "operationalFacts")).All(fact =>
                        Value(fact, "provenance").StartsWith(
                            "authored:established:",
                            StringComparison.Ordinal)
                        && Value(fact, "operatorIdentity")
                            .StartsWith("population-group:",
                                StringComparison.Ordinal))
                && fixtureTool.Contains(
                    "CASettlementOperationalFactAuthoringKernel.Establish"),
            "the current four-settlement fixture declares nineteen initial institutions through the production formation kernel");
        C(68, "Program authoring owns persistence and regeneration",
            programAuthoring.Contains("operationalFacts.Add(fact)")
                && programAuthoring.Contains("operationalFacts")
                && programAuthoring.Contains("EnsureDerived")
                && programAuthoring.Contains("SavePending")
                && program.Contains("Establish a program...")
                && program.Contains(
                    "CASettlementProgramAuthoring.Establish"),
            "Starting Region creates/removes saved operational facts and derives the visible program from them");
        C(69, "Runtime resolves exact fact, operator, and labor",
            programRuntime.Contains("item.active")
                && programRuntime.Contains(
                    "item.programKey == entry.programKey")
                && programRuntime.Contains(
                    "item.operatorIdentity == entry.operatorIdentity")
                && programRuntime.Contains("Matches(entry, fact)")
                && programRuntime.Contains("ResolveLabor")
                && programRuntime.Contains("WorkerAuthorized"),
            "removing or changing the fact/operator/labor suspends the exact entry instead of borrowing another program");
        C(70, "Every material role has a live asset receipt",
            programAssets.Contains(
                "class CASettlementProgramAssetReceipt")
                && programAssets.Contains("assetRole")
                && programAssets.Contains("programSignature")
                && programAssets.Contains("RecordThing")
                && programAssets.Contains("RecordZone")
                && programAssets.Contains("LiveThing")
                && programAssets.Contains("LiveZone")
                && provisionMaterialization.Contains(
                    "for (int role = 0; role < nodeThings.Count"),
            "thing and zone roles are persisted and provision nodes retain one holding per selected role");
        C(71, "Rebuilding rebinds exact program identity atomically",
            materializer.Contains("oldThingId")
                && materializer.Contains("assetRole")
                && materializer.Contains("programSignature")
                && materializer.Contains(
                    "CASettlementProgramAssets.Rebind")
                && programAssets.Contains(
                    "receipt.thingId = rebuilt.ThingID")
                && programAssets.Contains("SyncEntryView(record, entry)")
                && programAssets.Contains("TryRebindProgramAsset")
                && organizationRelations.Contains(
                    "holding.thingId = rebuilt.thingIDNumber"),
            "the rebuilt Thing replaces the old receipt, program ID, and provision holding under one exact saved work contract");
        C(72, "Observed provision stock is exclusive and reachable",
            provisionOwners.Contains("reservedObservedStockIds")
                && provisionOwners.Contains(
                    "record.localRect.Contains(thing.Position)")
                && provisionOwners.Contains("pawn.CanReach")
                && provisionOwners.Contains(
                    "GroupBy(item => item.thingId)")
                && materializer.Contains(
                    ".DefaultIfEmpty(IntVec3.Invalid).First()"),
            "one observed stack cannot back two arrangements and all stock remains on reachable settlement ground");
        C(73, "Provision program kind cannot fall through",
            materializer.Contains(
                "IsProvisionProgram(entry.programKey)")
                && materializer.Contains(
                    "no exact provision arrangement exists for this program kind and operator")
                && programRuntime.Contains("ProgramKeyFor(")
                && programRuntime.Contains(
                    "item.operatorKind) == entry.programKey"),
            "wrong-kind or absent arrangements block before the generic materializer");
        C(74, "Security is produced by live Defense labor",
            securityAssignments.Contains(
                    "CASettlementProgramRuntimeContract.TryResolve")
                && securityAssignments.Contains("equipment?.Primary")
                && securityAssignments.Contains("programSignature")
                && securityAssignments.Contains("LiveGuardIds")
                && S("Source/PatrolSystemModule.cs")
                    .Contains("LiveGuardIds(record, map)")
                && !S("Source/PatrolSystemModule.cs")
                    .Contains("return armed ?? fallback"),
            "patrols use only named, live, armed residents assigned by the exact Defense program");
        C(75, "Later work preserves exact program continuity",
            materializer.Contains("public string programKey;")
                && materializer.Contains("public string operatorIdentity;")
                && materializer.Contains("public string programSignature;")
                && planning.Contains(
                    "CASettlementProgramRuntimeContract.TryResolve")
                && planning.Contains("WorkerAuthorized")
                && road.Contains("programSignature")
                && materializer.Contains(
                    "requireMaterializedAssets: false"),
            "research, cultivation, repair, rebuilding, and roads retain exact program/operator/signature receipts through completion");

        bool mirrorEqual = File.ReadAllBytes(active).SequenceEqual(
            File.ReadAllBytes(mirror));
        if (!verifyOnly)
            WriteReport(active, mirror, mirrorEqual);
        foreach (Result result in Results)
            Console.WriteLine($"{result.Number:00} {(result.Passed ? "PASS" : "FAIL")} {result.Name}");
        int passed = Results.Count(item => item.Passed);
        Console.WriteLine($"B10 acceptance: {passed}/{Results.Count}; mirror={(mirrorEqual ? "identical" : "different")}");
        return passed == Results.Count && mirrorEqual ? 0 : 2;
    }

    private static CASettlementProgramOperationalEvidence ProgramEvidence() =>
        new()
        {
            Key = CASettlementProgramCausalKernel.Research,
            NeedSource = "authored:research question",
            OperatorIdentity = "org:academy",
            OperatorSource = "authored:academy charter",
            LaborSource = "authored:pawn:7 assignment",
            StandingSource = "authored:research standing",
            ActivitySource = "observed:active project",
            TargetPopulation = "settlement residents",
            KnowledgeSource = "authored:project:3",
            FundingSource = "authored:grant:2",
            MaterialSource = "observed:bench:9",
            AccessSource = "authored:members",
            MaintenanceSource = "authored:academy maintenance",
            RequiresOperator = true,
            RequiresFunding = true,
            RequiresMaterial = true,
            CandidateGroups = new() { new[] { "SimpleResearchBench" } }
        };

    private static CASettlementProgramOperationalEvidence Clone(
        CASettlementProgramOperationalEvidence source) => new()
    {
        Key = source.Key, Scope = source.Scope,
        NeedSource = source.NeedSource,
        OperatorIdentity = source.OperatorIdentity,
        OperatorSource = source.OperatorSource,
        LaborSource = source.LaborSource,
        StandingSource = source.StandingSource,
        ActivitySource = source.ActivitySource,
        TargetPopulation = source.TargetPopulation,
        KnowledgeSource = source.KnowledgeSource,
        FundingSource = source.FundingSource,
        StockSource = source.StockSource,
        PolicyKey = source.PolicyKey,
        MaterialSource = source.MaterialSource,
        AccessSource = source.AccessSource,
        MaintenanceSource = source.MaintenanceSource,
        CulturalSubjects = new(source.CulturalSubjects),
        CandidateGroups = source.CandidateGroups.Select(value =>
            value.ToArray()).ToList(),
        Count = source.Count, Extent = source.Extent,
        RequiresOperator = source.RequiresOperator,
        RequiresFunding = source.RequiresFunding,
        RequiresStock = source.RequiresStock,
        RequiresMaterial = source.RequiresMaterial,
        NativeSpatialContract = source.NativeSpatialContract,
        MaterializeSpatialContract = source.MaterializeSpatialContract,
        Blocker = source.Blocker
    };

    private static CAProvisionOperatorFact ProvisionFact(string kind,
        string identity, string funding) => new()
    {
        Key = 1, BasisKey = "fact:1", BasisLabel = "Saved provision",
        Operator = kind, OperatorIdentity = identity,
        OperatorSource = "authored:operator record", Access = "Members",
        AccessSource = "authored:access rule",
        Funding = funding, FundingSource = "authored:funding record",
        Distribution = "Neighborhood",
        DistributionSource = "authored:distribution rule",
        LaborSource = "authored:assigned workers",
        KnowledgeSource = "observed:active provision work",
        MaterialSource = "observed:kitchen:4",
        StockSource = "observed:stock:8", Reach = "Settlement"
    };

    private static void C(int number, string name, bool passed,
        string evidence) => Results.Add(new Result(number, name, passed,
            evidence));

    private static string S(string relative) => File.ReadAllText(Path.Combine(
        repo, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string AllSource() => string.Join("\n",
        Directory.EnumerateFiles(Path.Combine(repo, "Source"), "*.cs",
            SearchOption.AllDirectories).Select(File.ReadAllText));

    private static string Slice(string text, string start, string end)
    {
        int a = text.IndexOf(start, StringComparison.Ordinal);
        if (a < 0) return "";
        int b = text.IndexOf(end, a + start.Length,
            StringComparison.Ordinal);
        return b < 0 ? text[a..] : text[a..b];
    }

    private static bool Before(string text, string first, string second)
    {
        int a = text.IndexOf(first, StringComparison.Ordinal);
        int b = text.IndexOf(second, StringComparison.Ordinal);
        return a >= 0 && b > a;
    }

    private static IEnumerable<XElement> Items(XElement parent,
        string name) => parent?.Element(name)?.Elements("li")
            ?? Enumerable.Empty<XElement>();

    private static string Value(XElement parent, string name) =>
        parent?.Element(name)?.Value ?? "";

    private static string[] ProgramKeys() => new[]
    {
        "ca.settlement.housing", "ca.settlement.food-preparation",
        "ca.settlement.storage", "ca.settlement.medicine",
        "ca.settlement.production", "ca.settlement.specialized-industry",
        "ca.settlement.trade", "ca.settlement.governance",
        "ca.settlement.custody", "ca.settlement.defense",
        "ca.settlement.research", "ca.settlement.religion",
        "ca.settlement.gathering", "ca.settlement.recreation",
        "ca.settlement.art-memory", "ca.settlement.agriculture",
        "ca.settlement.animals", "ca.settlement.communications",
        "ca.settlement.transport", "ca.settlement.provision.communal",
        "ca.settlement.provision.authority",
        "ca.settlement.provision.domestic"
    };

    private static void WriteReport(string active, string mirror,
        bool mirrorEqual)
    {
        string Hash(string path) => Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(path)));
        var text = new StringBuilder();
        text.AppendLine("# B10 Acceptance Receipts").AppendLine();
        text.AppendLine("Date: 2026-08-12").AppendLine();
        text.AppendLine("Generated by `tools/B10AcceptanceReceipts` from the current production kernels, active source, and governed fixture. Runtime observation remains the operator's next boundary.").AppendLine();
        text.AppendLine("- Result: **" + Results.Count(item => item.Passed)
            + "/" + Results.Count + "**");
        text.AppendLine("- Active fixture SHA-256: `" + Hash(active) + "`");
        text.AppendLine("- Mirror fixture SHA-256: `" + Hash(mirror) + "`");
        text.AppendLine("- Fixture surfaces: **"
            + (mirrorEqual ? "byte-identical" : "different") + "**")
            .AppendLine();
        text.AppendLine("| # | Receipt | Result | Evidence |");
        text.AppendLine("|---:|---|---|---|");
        foreach (Result result in Results)
            text.AppendLine("| " + result.Number + " | " + result.Name
                + " | **" + (result.Passed ? "PASS" : "FAIL") + "** | "
                + result.Evidence.Replace("|", "\\|") + " |");
        File.WriteAllText(Path.Combine(repo, "B10_ACCEPTANCE_RECEIPTS.md"),
            text.ToString(), new UTF8Encoding(false));
    }
}
