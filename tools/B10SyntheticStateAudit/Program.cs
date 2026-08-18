using System.Text;
using System.Text.RegularExpressions;

internal static class Program
{
    private sealed record Classification(string Category, string Basis);
    private sealed record Occurrence(string File, int Line, string Text,
        Classification Classification);
    private sealed record Finding(string Severity, string Surface,
        string Evidence);

    private static readonly Regex RandomOrHash = new(
        @"\bRand\.|RangeInclusive|RandomInRange|RandomElement|TryRandom|"
        + @"StableStringHash|CAStableHash|HashParts|HashCombine|ValueSeeded|"
        + @"ValueAsync", RegexOptions.Compiled);

    private static readonly Dictionary<string, Classification> FileClasses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["AnimalCareModule.cs"] = Event("native health event chance"),
            ["AnimalReactionModule.cs"] = Event("bounded reaction chance after a real cue"),
            ["ApertureModule.cs"] = Form("equivalent aperture and material-form selection"),
            ["ArrangementModule.cs"] = Form("presentation offset"),
            ["AudibleCueModule.cs"] = Technical("transient audible-event key and per-hearer evaluation"),
            ["BattlefieldPerceptionModule.cs"] = Technical("transient perception key"),
            ["BridgeDecayModule.cs"] = Event("represented material decay event"),
            ["ClaimOverlayModule.cs"] = Form("overlay presentation"),
            ["CombatTopologyModule.cs"] = Technical("native battle-log replay and diagnostics"),
            ["DragModule.cs"] = Event("represented injury chance during an actual drag"),
            ["EnemyRestraintModule.cs"] = Event("bounded actor response after eligibility"),
            ["FrontierModule.cs"] = Form("site placement after factual holding eligibility"),
            ["GossipModule.cs"] = Event("bounded transmission attempt between actual actors"),
            ["ImmediateCombatModule.cs"] = Schedule("bounded operator override window"),
            ["OrganizationModule.cs"] = Form("frontier physical-plan receipt after eligibility"),
            ["PatrolSystemModule.cs"] = Schedule("patrol staggering and equivalent route geometry"),
            ["PlayerFoundingPageModule.cs"] = Form("native Ideoligion detail generation after explicit preset choice"),
            ["RaidResponseModule.cs"] = Event("bounded response chance for an eligible actor"),
            ["RegionalConstituentFrameModule.cs"] = Form("regional projection frame geometry"),
            ["RegionalEngineRootModule.cs"] = Technical("commentary about native generation ordering"),
            ["RegionalMapTemplateModule.cs"] = Technical("commentary about deterministic native cell order"),
            ["RegionalPerCellFieldsModule.cs"] = Form("native terrain and wildlife material generation"),
            ["RegionalPlantMaskJobsModule.cs"] = Technical("commentary about native plant eligibility"),
            ["RegionalProjectionKernelModule.cs"] = Form("equivalent geographic-shape variation"),
            ["RegionalRockChunksJobsModule.cs"] = Technical("commentary about native rock generation"),
            ["RegionalScatterAllocationModule.cs"] = Form("native material scatter allocation"),
            ["RegionalSelectedFeatureResolutionModule.cs"] = Form("equivalent feature placement or material choice after authored eligibility"),
            ["RegionalSettlementModelModule.cs"] = Form("eligible settlement/frontier placement and readback signatures"),
            ["RegionalSetupModule.cs"] = Form("map geometry, river geometry, native pawn generation, or technical draft key"),
            ["RegionalWorldModule.cs"] = Form("equivalent spatial placement after saved settlement eligibility"),
            ["SettlementCapabilityAssessmentKernel.cs"] = Receipt("capability evidence signature"),
            ["SettlementEnvironmentModule.cs"] = Receipt("environment fact and aggregate provenance signature"),
            ["SettlementCompositionModule.cs"] = Form("native pawn generation for an authored population group"),
            ["SettlementProgramCausalKernel.cs"] = Receipt("program evidence signature or equivalent functional-asset selection"),
            ["StackJobs.cs"] = Schedule("native combat-job recovery interval"),
            ["SurvivalModule.cs"] = Event("bounded survival response after factual threat eligibility"),
            ["TacticalOverlayModule.cs"] = Form("overlay presentation"),
            ["WaterActsModule.cs"] = Event("represented illness chance after contaminated-water exposure"),
            ["WaterDrawModule.cs"] = Event("represented illness chance after contaminated-water exposure"),
            ["WelfareKnowledgeModule.cs"] = Schedule("bounded accountability follow-up interval"),
            ["WorldTendencyCausalKernel.cs"] = Receipt("pure placement pressure and source-signature helper")
        };

    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("usage: B10SyntheticStateAudit <repo>");
            return 1;
        }
        string repo = Path.GetFullPath(args[0]);
        string sourceRoot = Path.Combine(repo, "Source");
        var occurrences = new List<Occurrence>();
        var findings = new List<Finding>();

        foreach (string path in Directory.EnumerateFiles(sourceRoot, "*.cs",
            SearchOption.AllDirectories).OrderBy(value => value,
                StringComparer.OrdinalIgnoreCase))
        {
            string file = Path.GetFileName(path);
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!RandomOrHash.IsMatch(lines[i])) continue;
                if (!FileClasses.TryGetValue(file, out Classification kind))
                {
                    findings.Add(new Finding("High", file + ":" + (i + 1),
                        "random/hash occurrence has no approved causal classification: "
                            + lines[i].Trim()));
                    kind = new Classification("UNCLASSIFIED",
                        "requires causal adjudication");
                }
                occurrences.Add(new Occurrence(Relative(repo, path), i + 1,
                    lines[i].Trim(), kind));
            }
        }

        string all = string.Join("\n", Directory.EnumerateFiles(sourceRoot,
                "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));
        string domestic = Read(repo, "Source/DomesticUnitModule.cs")
            + Read(repo, "Source/DomesticUnitStateModule.cs")
            + Read(repo, "Source/DomesticResidenceAdapterModule.cs")
            + Read(repo, "Source/DomesticProvisionAdapterModule.cs");
        string capability = Read(repo,
                "Source/SettlementCapabilityAssessmentKernel.cs")
            + Read(repo, "Source/SettlementCapabilityAdaptersModule.cs")
            + Read(repo, "Source/SettlementCapabilityEvidenceModule.cs")
            + Read(repo, "Source/SettlementCapabilityModule.cs");
        string programModel = Read(repo,
            "Source/SettlementProgramModule.cs");
        string programResolver = Read(repo,
            "Source/SettlementProgramOperationalModule.cs");
        string programRuntime = Read(repo,
            "Source/SettlementProgramRuntimeModule.cs");
        string programAuthoring = Read(repo,
            "Source/SettlementProgramAuthoringModule.cs")
            + Read(repo,
                "Source/SettlementOperationalFactAuthoringKernel.cs");
        string programAssets = Read(repo,
            "Source/SettlementProgramAssetModule.cs");
        string materializer = Read(repo,
            "Source/SettlementProgramMaterializerModule.cs");
        string provision = Read(repo, "Source/ProvisionCausalKernel.cs")
            + Read(repo, "Source/ProvisionOperatorResolverModule.cs")
            + Read(repo, "Source/ProvisionFundingResolverModule.cs")
            + Read(repo, "Source/ProvisionAccessServiceModule.cs")
            + Read(repo, "Source/ProvisionMaterializationAdapterModule.cs")
            + Read(repo, "Source/ProvisionRuntimeModule.cs");
        string provisionMaterialization = Slice(materializer,
            "private static bool MaterializeProvisionEntry(",
            "private static Thing SpawnStock(");
        string frontier = Read(repo, "Source/FrontierModule.cs");
        string axes = Read(repo, "Source/SettlementAxesModule.cs");
        string regional = Read(repo, "Source/RegionalSettlementModelModule.cs");
        string regionalSetup = Read(repo, "Source/RegionalSetupModule.cs");
        string planning = Read(repo,
            "Source/SettlementPlanningContextModule.cs");
        string animal = Read(repo, "Source/AnimalDispositionModule.cs");
        string organization = Read(repo, "Source/OrganizationModule.cs");
        string hostile = Read(repo,
            "Source/HostileFactionOrganizationModule.cs");
        string taskForce = Read(repo, "Source/TaskForceModule.cs");
        string assault = Read(repo, "Source/AssaultApproachModule.cs");
        string road = Read(repo, "Source/RoadExpansionModule.cs");
        string patrol = Read(repo, "Source/PatrolSystemModule.cs");
        string culturalExpression = Read(repo,
            "Source/CulturalExpressionModule.cs");
        string securityFacts = Read(repo,
            "Source/SettlementSecurityFactsModule.cs");
        string securityAssignments = Read(repo,
            "Source/SettlementSecurityAssignmentModule.cs");
        string ui = Read(repo, "Source/RegionalPopulationScreenModule.cs");
        string fixture = Read(repo, "tools/B10FixtureGenerator/Program.cs");

        Critical(findings, !all.Contains("HouseholdConsumers",
            StringComparison.Ordinal), "domestic identity",
            "hash-selected HouseholdConsumers path remains");
        Critical(findings, !all.Contains("persistentRandomValue",
            StringComparison.Ordinal), "persistent identity",
            "persistent random value remains in an identity path");
        Critical(findings, !domestic.Contains("Rand.")
            && !domestic.Contains("StableStringHash"), "domestic identity",
            "domestic formation still uses random/hash selection");
        Critical(findings, !capability.Contains("Rand.")
            && !capability.Contains("StableCandidateIndex"),
            "practiced capability",
            "capability discovery still uses random selection");
        Critical(findings, !programResolver.Contains("politicalBeliefs")
            && !programResolver.Contains("CACulture")
            && !programResolver.Contains("economicCapacity")
            && !programResolver.Contains("urbanSupport"),
            "settlement programs",
            "program operations are still created by beliefs, Culture, or read models");
        Critical(findings, !provision.Contains("CAProvisionCausalFacts.Support")
            && !provision.Contains("fallback organization"), "provisioning",
            "provision operator still comes from a category or fallback");
        Critical(findings, !frontier.Contains("new CAOrganization")
            && !frontier.Contains("CAOrganizationKind.Household")
            && !frontier.Contains("new CAOffice")
            && !frontier.Contains("new CAOrganizationGroup"),
            "frontier holdings",
            "frontier materialization still fabricates social organization");
        Critical(findings, !animal.Contains("StableVariation")
            && !animal.Contains("Rand.")
            && !animal.Contains("HashCombine"), "animal disposition",
            "animal personality still contains hidden seeded variation");
        Critical(findings, !organization.Contains("bestCombat >=")
            && !organization.Contains("known by a skilled fighter"),
            "organizational customs",
            "combat skill still creates a practiced organizational custom");
        Critical(findings, !regional.Contains(
            "CountBits(settlement.operationalRoleMask)")
            && regional.Contains("CompleteProgramCount(plan, settlement"),
            "specialization", "saved role bits still create specialization");
        Critical(findings, !Slice(axes, "private static int DerivedServices",
                "private static int DerivedCivic").Contains(
                    "residentPopulation")
            && !Slice(axes, "private static int DerivedCivic",
                "private static int CountComplete").Contains(
                    "residentPopulation"), "services and civic summaries",
            "population still manufactures service or civic infrastructure");
        Critical(findings, !Slice(planning,
                "BuildCreationProposal(", "CreationFromRecord(")
            .Contains("economicCapacity"), "creation funding",
            "broad economic capacity still proves creation funding");
        string institutionalProposal = Slice(planning,
            "BuildInstitutionalProposal(",
            "CanExerciseInstitutionalDevelopment(");
        Critical(findings, !institutionalProposal.Contains(
                "TryActiveResearchContract")
            && !institutionalProposal.Contains("SpawnedPawnsInFaction")
            && institutionalProposal.Contains("OperationalProgram(record")
            && institutionalProposal.Contains(
                "CAPopulationProjection.Residents")
            && institutionalProposal.Contains(
                "CASettlementDemandKind.Research"),
            "institutional development",
            "later development can still create research demand from its own activity or untyped faction presence");
        string postOpen = Slice(ui, "public override void PostOpen()",
            "public override void PostClose()");
        Critical(findings, !postOpen.Contains("SavePending")
            && !postOpen.Contains("RefreshDraftRealization")
            && !postOpen.Contains("EnsureDerived")
            && !postOpen.Contains("ReconcileProvision"), "Starting Region UI",
            "opening the page mutates authoritative draft state");
        Critical(findings, !Regex.IsMatch(all,
            @"TicksGame[^;\n]{0,160}(unitIdentity|organizationKey|operatorIdentity|culture\.id)",
            RegexOptions.IgnoreCase), "semantic identity",
            "current tick participates in a persistent semantic identity");
        Critical(findings, !Regex.IsMatch(regionalSetup,
            @"if\s*\(candidateId\.NullOrEmpty\(\)\)\s*"
                + @"candidateId\s*=\s*operatorAuthored"),
            "semantic identity",
            "post-load compatibility code still mints candidate identity");
        Critical(findings, !fixture.Contains(
            "Set(population, \"nativeIdeoligionId\", old.Value)"),
            "fixture schema", "legacy Ideoligion key is reinterpreted as a native ID");

        Critical(findings, !all.Contains("CASettlementCapabilities.Level(",
            StringComparison.Ordinal), "capability ownership",
            "a derived capability rating still feeds operative state");
        Critical(findings, securityFacts.Contains(
                "CASettlementProgramRegistry.Defense")
            && securityFacts.Contains("CASettlementProgramAssets.LiveThing")
            && securityFacts.Contains(
                "CASettlementSecurityAssignments.LiveGuardIds")
            && organization.Contains("CASettlementSecurityFacts.Read")
            && patrol.Contains("LiveGuardIds(record, map)")
            && !patrol.Contains("GuardIdsFor(key)")
            && !patrol.Contains("return armed ?? fallback")
            && !patrol.Contains("CASettlementCapabilities"),
            "security and patrol",
            "operative security still reads a capability summary instead of represented defenses and assigned guards");
        Critical(findings, !culturalExpression.Contains("Fortification")
            && !culturalExpression.Contains("Organization"),
            "cultural interpretation",
            "cultural expression still receives derived execution capability as an input");

        Critical(findings, !hostile.Contains("EnsureFor(")
            && !hostile.Contains("SeedDoctrine")
            && !hostile.Contains("techLevel")
            && !hostile.Contains("publicSupport"),
            "hostile faction organization",
            "encounter still creates an organization, doctrine, capability, or support");
        Critical(findings, !taskForce.Contains("adherence")
            && !taskForce.Contains("2654435761")
            && !taskForce.Contains("StableStringHash")
            && taskForce.Contains("Practices(CAOrganization factionOrg"),
            "hostile task force",
            "hash/adherence state still grants task-force doctrine");
        Critical(findings, assault.Contains(
                "unit.Practices(org, \"ambush\")")
            && assault.Contains("unit.Practices(org, \"line\")")
            && !assault.Contains("adherence")
            && !assault.Contains("StableStringHash"),
            "assault approach",
            "advanced assault behavior is not gated by practiced doctrine");

        Critical(findings, programModel.Contains(
                "IEnumerable<CASettlementProgramEntry> Entries(string key)")
            && programResolver.Contains("existing.OperatorIdentity")
            && programRuntime.Contains("operatorIdentity")
            && provision.Contains(".Entries(key)"),
            "settlement program cardinality",
            "same-kind programs are still collapsed or reconciled without exact operator identity");
        Critical(findings, Before(provisionMaterialization,
                "CAProvisionMaterializationAdapter.TryPlan", "Place(org")
            && provisionMaterialization.Contains("new CAFacilityHolding")
            && provisionMaterialization.Contains("new CAStartingStockRecord")
            && provisionMaterialization.Contains("ledger.Add(holding)")
            && provisionMaterialization.Contains(
                "record.startingStock.AddRange")
            && provisionMaterialization.Contains("RemoveDerivedHolding")
            && provisionMaterialization.Contains("DestroyMode.Vanish"),
            "provision materialization",
            "provision nodes lack preflight, exact receipts, or atomic rollback");
        Critical(findings, provision.Contains("provisionArrangementKey")
            && provision.Contains("provisionNodeIndex")
            && provision.Contains("operatorIdentity")
            && provision.Contains("startingStock"),
            "provision readback",
            "runtime provision validation does not bind exact arrangement, node, operator, and stock receipts");

        string researchWork = Slice(materializer, "private bool TryResearch(",
            "internal void CompleteNativeLabor(");
        Critical(findings, researchWork.Contains(
                "CASettlementProgramRegistry.Research")
            && researchWork.Contains("ReceiptsFor(record, program)")
            && researchWork.Contains(
                "CASettlementProgramAssets.LiveThing")
            && researchWork.Contains("programSignature")
            && researchWork.Contains("CASettlementDemandKind.Research")
            && !researchWork.Contains("operationalFacts.Add"),
            "research work",
            "research work still creates its own program premise or ignores exact placed assets");
        Critical(findings, road.Contains(
                "CASettlementProgramRuntimeContract.TryResolve")
            && road.Contains("runtime.Workers")
            && road.Contains("project.programSignature")
            && road.Contains("CASettlementDemandKind.Access")
            && road.Contains("CASettlementProgramRegistry.Transport")
            && !road.Contains("SpawnedPawnsInFaction"),
            "road development",
            "road work can use untyped faction pawns or outlive its exact transport commitment");

        Critical(findings, programAuthoring.Contains(
                "CASettlementOperationalFactAuthoringKernel.Establish")
            && programAuthoring.Contains("operationalFacts.Add(fact)")
            && programAuthoring.Contains(
                "CASettlementProgramRegistry.EnsureDerived")
            && programModel.Contains("Establish a program...")
            && programModel.Contains(
                "CASettlementProgramAuthoring.Establish"),
            "program formation lifecycle",
            "established programs have no explicit authoring producer shared by UI and persistence");
        Critical(findings, fixture.Contains(
                "CASettlementOperationalFactAuthoringKernel.Establish")
            && fixture.Contains("19 established program facts")
            && !fixture.Contains("operationalRoleMask"),
            "governed fixture",
            "fixture does not use the production program-fact formation kernel or still derives programs from a legacy mask");
        Critical(findings, programAssets.Contains(
                "class CASettlementProgramAssetReceipt")
            && programAssets.Contains("programSignature")
            && programAssets.Contains("assetRole")
            && programAssets.Contains("RecordThing(")
            && programAssets.Contains("RecordZone(")
            && programAssets.Contains("LiveThing(")
            && programAssets.Contains("Complete(")
            && programAssets.Contains("Rebind(")
            && materializer.Contains(
                "string assetRole = \"arrangement:")
            && materializer.Contains("for (int role = 0; role < nodeThings.Count")
            && materializer.Contains(
                "CASettlementProgramAssets.RollbackToCount(")
            && programAssets.Contains("record.programAssets.RemoveRange"),
            "program material-node lifecycle",
            "program asset roles lack persistent exact receipts, live validation, all-role provision holdings, or rollback/rebind");
        Critical(findings, provision.Contains(
                "reservedObservedStockIds")
            && provision.Contains("record.localRect.Contains(thing.Position)")
            && provision.Contains("pawn.CanReach")
            && provision.Contains("GroupBy(item => item.thingId)")
            && materializer.Contains(
                ".DefaultIfEmpty(IntVec3.Invalid).First()")
            && materializer.Contains(
                "no exact provision arrangement exists for this program kind and operator"),
            "provision stock and program identity",
            "observed stock can be shared, remote, unreachable, default-spawned, or a wrong-kind provision can fall through");
        Critical(findings, securityAssignments.Contains(
                "CASettlementProgramRuntimeContract.TryResolve")
            && securityAssignments.Contains("equipment?.Primary")
            && securityAssignments.Contains("programSignature")
            && securityAssignments.Contains("LiveGuardIds")
            && patrol.Contains("LiveGuardIds(record, map)")
            && !patrol.Contains("return armed ?? fallback"),
            "security assignment lifecycle",
            "guards are not produced and revalidated by exact Defense-program labor or patrol still falls back to generic residents");
        Critical(findings, materializer.Contains("public string programKey;")
            && materializer.Contains("public string operatorIdentity;")
            && materializer.Contains("public string programSignature;")
            && planning.Contains(
                "CASettlementProgramRuntimeContract.TryResolve")
            && planning.Contains("WorkerAuthorized")
            && road.Contains("programSignature")
            && materializer.Contains("requireMaterializedAssets: false")
            && materializer.Contains(
                "CASettlementProgramAssets.Rebind"),
            "exact program work continuity",
            "later work does not retain and reauthorize the exact program, operator, worker, asset role, and rebuilt identity");

        WriteReport(repo, occurrences, findings);
        int unresolved = findings.Count(item => item.Severity is "Critical" or "High");
        Console.WriteLine("B10 synthetic-state occurrences classified: "
            + occurrences.Count);
        Console.WriteLine("B10 unresolved Critical/High: " + unresolved);
        return unresolved == 0 ? 0 : 2;
    }

    private static Classification Form(string basis) =>
        new("physical, spatial, or presentation variation", basis);
    private static Classification Event(string basis) =>
        new("bounded represented-event uncertainty", basis);
    private static Classification Schedule(string basis) =>
        new("bounded scheduling or equivalent route ordering", basis);
    private static Classification Receipt(string basis) =>
        new("readback signature or post-eligibility choice", basis);
    private static Classification Technical(string basis) =>
        new("transient technical identity or documentation", basis);

    private static void Critical(List<Finding> findings, bool condition,
        string surface, string evidence)
    {
        if (!condition) findings.Add(new Finding("Critical", surface,
            evidence));
    }

    private static string Read(string repo, string relative) =>
        File.ReadAllText(Path.Combine(repo,
            relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string Slice(string value, string start, string end)
    {
        int a = value.IndexOf(start, StringComparison.Ordinal);
        if (a < 0) return "";
        int b = value.IndexOf(end, a + start.Length,
            StringComparison.Ordinal);
        return b < 0 ? value[a..] : value[a..b];
    }

    private static bool Before(string value, string first, string second)
    {
        int a = value.IndexOf(first, StringComparison.Ordinal);
        int b = value.IndexOf(second, StringComparison.Ordinal);
        return a >= 0 && b > a;
    }

    private static string Relative(string repo, string path) =>
        Path.GetRelativePath(repo, path).Replace('\\', '/');

    private static void WriteReport(string repo,
        List<Occurrence> occurrences, List<Finding> findings)
    {
        int unresolved = findings.Count(item => item.Severity is "Critical"
            or "High");
        var text = new StringBuilder();
        text.AppendLine("# B10 Synthetic-State Sweep").AppendLine();
        text.AppendLine("Date: 2026-08-12").AppendLine();
        text.AppendLine("This report is generated by `tools/B10SyntheticStateAudit`. "
            + "Every active C# RNG/hash occurrence is classified. Executable "
            + "owner-path assertions additionally cover domestic identity, "
            + "capability feedback, hostile doctrine, program cardinality, "
            + "provision receipts, institutional development, and UI authority. "
            + "Any failed invariant is a Critical finding.").AppendLine();
        text.AppendLine("- Classified occurrences: **" + occurrences.Count + "**");
        text.AppendLine("- Unresolved Critical/High: **" + unresolved + "**");
        text.AppendLine("- Result: **" + (unresolved == 0 ? "PASS" : "FAIL")
            + "**").AppendLine();
        text.AppendLine("## Critical and High findings").AppendLine();
        if (findings.Count == 0) text.AppendLine("None.").AppendLine();
        else
        {
            text.AppendLine("| Severity | Surface | Evidence |");
            text.AppendLine("|---|---|---|");
            foreach (Finding finding in findings)
                text.AppendLine("| " + finding.Severity + " | "
                    + Escape(finding.Surface) + " | "
                    + Escape(finding.Evidence) + " |");
            text.AppendLine();
        }
        text.AppendLine("## Classified RNG and hash occurrences").AppendLine();
        text.AppendLine("| Source | Category | Causal basis | Occurrence |");
        text.AppendLine("|---|---|---|---|");
        foreach (Occurrence item in occurrences)
            text.AppendLine("| `" + item.File + ":" + item.Line + "` | "
                + Escape(item.Classification.Category) + " | "
                + Escape(item.Classification.Basis) + " | `"
                + item.Text.Replace("`", "'").Replace("|", "\\|") + "` |");
        File.WriteAllText(Path.Combine(repo,
            "B10_SYNTHETIC_STATE_SWEEP.md"), text.ToString(),
            new UTF8Encoding(false));
    }

    private static string Escape(string value) =>
        (value ?? "").Replace("|", "\\|").Replace("\r", " ")
            .Replace("\n", " ");
}
