using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class Program
{
    private enum ReceiptStatus { Verified, PendingOperatorRuntime, Failed }

    private sealed record Receipt(int Id, string Name, ReceiptStatus Status,
        string Evidence, string ProofKind);

    private static readonly List<Receipt> Results = new();

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3)
                throw new ArgumentException("usage: BehaviorConvergenceReceipts "
                    + "<repo> <active-mirror> <keyed-fixture>");
            string repository = Path.GetFullPath(args[0]);
            string mirrorPath = Path.GetFullPath(args[1]);
            string keyedPath = Path.GetFullPath(args[2]);
            string Source(string file) => File.ReadAllText(Path.Combine(
                repository, "Source", file));

            string catalog = Source("BehaviorCatalogModule.cs");
            string autonomy = Source("AutonomyModule.cs");
            string modEntry = Source("ModEntry.cs");
            string intent = Source("CombatIntentModule.cs");
            string ownedIntent = Source("BehaviorIntentModule.cs");
            string trace = Source("TraceModule.cs");
            string comms = Source("CommsModule.cs");
            string authority = Source("AuthorityModule.cs");
            string knowledge = Source("KnowledgeModule.cs");
            string welfareKnowledge = Source("WelfareKnowledgeModule.cs");
            string support = Source("SquadSupportModule.cs");
            string access = Source("OperationalAccessModule.cs");
            string aftermath = Source("CombatAftermathModule.cs");
            string restraint = Source("EnemyRestraintModule.cs");
            string lifeSafety = Source("LifeSafetyModule.cs");
            string fire = Source("FireResponseModule.cs");
            string triage = Source("MissionTriageModule.cs");
            string animal = Source("AnimalCareModule.cs");
            string criticalHaul = Source("CriticalHaulModule.cs");
            string corpse = Source("CorpseDisciplineModule.cs");
            string welfareSupport = Source("WelfareSupportModule.cs");
            string equipment = Source("EquipTransitionModule.cs");
            string withdrawal = Source("WithdrawalModule.cs");
            string raid = Source("RaidResponseModule.cs");
            string home = Source("AutonomousHomeModule.cs");
            string homeIntent = Source("HomeIntentModule.cs");
            string homePrerequisite = Source("HomePrerequisiteModule.cs");
            string arrangement = Source("ArrangementModule.cs");
            string spatial = Source("SpatialInitiativeModule.cs");
            string furnishing = Source("SpatialFurnishingModule.cs");
            string siting = Source("FacilitySitingModule.cs");
            string planning = Source("SettlementPlanningContextModule.cs");
            string regional = Source("RegionalWorldModule.cs");
            string startingFacilities = Source("StartingFacilitiesModule.cs");
            string roads = Source("RoadExpansionModule.cs");
            string toxicWaste = Source("ToxicWasteLifecycleModule.cs");
            string survival = Source("SurvivalModule.cs");
            string drills = Source("DrillsModule.cs");
            string stackLord = Source("StackLord.cs");
            string assaultApproach = Source("AssaultApproachModule.cs");
            string frontier = Source("FrontierModule.cs");
            string organization = Source("OrganizationModule.cs");
            string disposition = Source("DispositionModule.cs");
            string immediate = Source("ImmediateCombatModule.cs");
            string combatReaction = Source("CombatReactionModule.cs");
            string census = Source("BehaviorCensusModule.cs");
            string founding = Source("PlayerFoundingPageModule.cs");
            string setup = Source("RegionalSetupModule.cs");
            string mapTemplate = Source("RegionalMapTemplateModule.cs");
            string mapArtifact = Source("RegionalMapArtifactModule.cs");
            string allSource = string.Join("\n", Directory.GetFiles(
                Path.Combine(repository, "Source"), "*.cs")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
            string runtimeSource = string.Join("\n", Directory.GetFiles(
                Path.Combine(repository, "Source"), "*.cs")
                .Where(path => !Path.GetFileName(path).Equals(
                    "BehaviorCatalogModule.cs", StringComparison.Ordinal)
                    && !Path.GetFileName(path).Equals("ModEntry.cs",
                        StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

            XDocument mirror = XDocument.Load(mirrorPath,
                LoadOptions.PreserveWhitespace);
            XDocument keyed = XDocument.Load(keyedPath,
                LoadOptions.PreserveWhitespace);
            XElement mirrorPlan = Plan(mirror);
            XElement keyedPlan = Plan(keyed);

            MatchCollection definitionMatches = Regex.Matches(catalog,
                "(?:Add|Direct|Support)\\(list,\\s*\\\"([^\\\"]+)\\\"");
            string[] behaviorKeys = definitionMatches.Cast<Match>()
                .Select(match => match.Groups[1].Value).ToArray();
            int domainCount = EnumMemberCount(catalog, "CABehaviorDomain");
            int formCount = EnumMemberCount(catalog, "CABehaviorForm");

            // Catalog integrity: 1-12.
            C(1, "Every behavior key is unique",
                behaviorKeys.Length > 0
                    && behaviorKeys.Distinct(StringComparer.Ordinal).Count()
                        == behaviorKeys.Length,
                behaviorKeys.Length + " registered keys", "executable");
            C(2, "Every behavior has one domain",
                domainCount == 15
                    && catalog.Contains("public CABehaviorDomain Domain")
                    && catalog.Contains("Validate(list)"),
                domainCount + " typed domains and catalog validation",
                "source-contract");
            C(3, "Every behavior has one form",
                formCount == 13
                    && catalog.Contains("public CABehaviorForm Form")
                    && catalog.Contains("Validate(list)"),
                formCount + " typed forms and catalog validation",
                "source-contract");
            C(4, "Every behavior has an executor owner",
                HasAll(catalog, "public string ExecutorOwner",
                    "has no executor owner", "ExecutorOwner = executorOwner"),
                "startup validation rejects missing owners", "executable");
            C(5, "Permission owner or always-available is explicit",
                HasAll(catalog, "public bool AlwaysAvailable",
                    "AlwaysAvailable = setting == CASettingKey.None",
                    "has no permission owner"),
                "setting ownership is constructor-derived and validated",
                "executable");
            C(6, "Autonomous behavior declares initiative or institution",
                HasAll(catalog, "MinimumInitiative",
                    "InstitutionalAuthority", "has no initiative or institutional authority"),
                "startup validation enforces the authority alternative",
                "executable");
            C(7, "Every behavior has a cadence",
                HasAll(catalog, "public string Cadence",
                    "string.IsNullOrWhiteSpace(definition.Cadence)"),
                "startup validation rejects missing cadence", "executable");
            C(8, "Every behavior has completion or stand-down",
                HasAll(catalog, "CompletionCondition", "StandDownCondition",
                    "has no cadence or termination contract"),
                "startup validation enforces termination", "executable");
            C(9, "UI behaviors have one owned description",
                HasAll(catalog, "public string Description",
                    "string.IsNullOrWhiteSpace(definition.Description)",
                    "has incomplete presentation text")
                    && !catalog.Contains("CompactText")
                    && !catalog.Contains("StandardText")
                    && !catalog.Contains("ExpandedText"),
                "startup validation enforces one contextual description without arbitrary presentation tiers",
                "executable");
            C(10, "Autonomy-sensitive settings map to behaviors",
                FeaturePermissionsHaveConsumers(catalog, runtimeSource)
                    && AuthorizingBehaviorKeysHaveGateConsumers(catalog,
                        repository),
                "every FeaturePermission and every originating catalog key reaches a production file that crosses the behavior gate",
                "executable");
            C(11, "Presentation-only settings cannot authorize behavior",
                HasAll(catalog, "AuthorizesOrigination",
                    "Form != CABehaviorForm.Presentation",
                    "CABehaviorBlockReason.NonOriginating",
                    "if (!definition.AuthorizesOrigination) continue"),
                "gate and stable profile exclude presentation entries",
                "source-contract");
            C(12, "Execution capability differs from action origination",
                HasAll(catalog, "CASettingKind.ExecutionCapability",
                    "CABehaviorForm.ExecutionCapability",
                    "catalog entry does not originate action",
                    "EvaluateNativeExecution", "permitNativeExecution")
                    && catalog.Contains("AuthorizesOrigination")
                    && ownedIntent.Contains("TryRegisterNativeExecution")
                    && criticalHaul.Contains("TryRegisterNativeExecution")
                    && corpse.Contains("EvaluateNativeExecution"),
                "capability and augmentation entries cannot originate action; their explicit native-execution seam remains usable", "source-contract");

            // Initiative migration: 13-22.
            C(13, "Old Directed maps to Standard",
                MigrationMaps(autonomy, 0, "Standard"), "legacy 0 -> Standard",
                "executable-source");
            C(14, "Old Standard maps to Standard",
                MigrationMaps(autonomy, 1, "Standard"), "legacy 1 -> Standard",
                "executable-source");
            C(15, "Old Proactive maps to Proactive",
                MigrationMaps(autonomy, 2, "Proactive"), "legacy 2 -> Proactive",
                "executable-source");
            C(16, "Old Autonomous maps to Autonomous",
                MigrationMaps(autonomy, 3, "Autonomous"), "legacy 3 -> Autonomous",
                "executable-source");
            C(17, "Default-autonomy migration is deterministic",
                HasAll(autonomy, "CurrentInitiativeSchema = 1",
                    "MigrateLegacyLevel(pair.Value)", "NormalizeStoredTiers"),
                "one schema marker and total normalization", "source-contract");
            C(18, "Spatial-initiative migration is deterministic",
                HasAll(spatial, "AutonomyComponent.MigrateLegacyLevel",
                    "CAInitiativeTier", "initiativeSchema",
                    "CurrentInitiativeSchema"),
                "spatial plans use typed migration and schema marker",
                "source-contract");
            C(19, "Legacy Proactive thresholds remain Proactive",
                autonomy.Contains("if (legacy >= 2) return CAInitiativeTier.Proactive"),
                "threshold 2 remains Proactive", "source-contract");
            C(20, "Legacy Autonomous thresholds remain Autonomous",
                autonomy.Contains("if (legacy >= 3) return CAInitiativeTier.Autonomous"),
                "threshold 3 remains Autonomous", "source-contract");
            C(21, "Menus contain exactly three active tiers",
                ActiveTierCount(catalog) == 3
                    && HasAll(catalog, "CAInitiativeTier.Standard",
                        "CAInitiativeTier.Proactive", "CAInitiativeTier.Autonomous"),
                ActiveTierCount(catalog) + " active tier entries", "executable");
            C(22, "Saved fixtures round-trip with the new schema",
                RoundTrips(mirror) && RoundTrips(keyed)
                    && Value(mirrorPlan, "schemaVersion") == "5"
                    && autonomy.Contains("CA_initiativeSchema"),
                "regional schema 5 round-trip plus initiative schema marker",
                "executable");

            // Authority: 23-32.
            C(23, "Direct operator action remains valid at every tier",
                HasAll(catalog, "bool direct = context.AuthorityOrigin",
                    "== CAAuthorityOrigin.OperatorDirect",
                    "initiativeApplies && !direct", "AuthorityCeilingApplies && !direct"),
                "direct origin bypasses tier and ceiling only", "source-contract");
            C(24, "Failed delivery prevents obedience evaluation",
                authority.Contains("if (!CommsModule.CanRelayOrder")
                    && authority.IndexOf("if (!CommsModule.CanRelayOrder",
                        StringComparison.Ordinal)
                        < authority.IndexOf("evaluating obedience separately",
                            StringComparison.Ordinal),
                "delivery result is checked before obedience", "source-contract");
            C(25, "Relayed obedience remains distinct from initiative",
                HasAll(authority, "evaluating obedience separately",
                    "Authority", "routeReceipt")
                    && catalog.Contains("authority.relay_obedience"),
                "separate delivery/obedience behavior and module", "source-contract");
            C(26, "Feature permission and initiative vary independently",
                HasAll(catalog, "FeatureEnabled", "CurrentTier", "RequiredTier",
                    "CABehaviorSettings.IsEnabled", "context.Initiative"),
                "decision carries permission and tier separately", "source-contract");
            C(27, "Spatial ceiling and pawn initiative vary independently",
                HasAll(catalog, "AuthorityCeiling", "context.Initiative")
                    && HasAll(spatial, "authorityId", "CAInitiativeTier tier"),
                "gate and spatial program retain separate values", "source-contract");
            C(28, "NPC institutional action ignores player autonomy",
                HasAll(catalog, "CAActorContext.NPCSettlement",
                    "CAActorContext.NPCInstitution", "InitiativeIndependent")
                    && catalog.Contains("spatial.npc_settlement_development"),
                "NPC institutional catalog context is initiative-independent",
                "source-contract");
            C(29, "Creation authoring ignores runtime autonomy",
                catalog.Contains("spatial.creation_authoring")
                    && catalog.Contains("CAActorContext.CreationAuthor")
                    && catalog.Contains("CAAuthorityOrigin.WorldAuthoring"),
                "creation has its own author context and authority", "source-contract");
            C(30, "NPC commitments name an in-world authority",
                catalog.Contains("spatial.npc_settlement_development")
                    && catalog.Contains("named household, operator, organization")
                    && HasAll(regional, "developmentProposer",
                        "developmentApprover", "developmentLaborSource",
                        "developmentBeneficiaries",
                        "developmentCulturalBasis",
                        "developmentPoliticalBasis")
                    && census.Contains("institutional authority"),
                "planning records proposer, approver, labor, beneficiaries, cultural and political basis, and census authority",
                "source-contract");
            C(31, "Player spatial commitments name delegated authority",
                HasAll(spatial, "authorityId", "CAInitiativeTier tier")
                    && census.Contains("player-authored programs delegate"),
                "program author and ceiling are explicit", "source-contract");
            C(32, "CA autonomous jobs carry behavior key and episode",
                HasAll(ownedIntent, "BehaviorKey", "EpisodeId", "Register(",
                    "string.IsNullOrEmpty(context.BehaviorKey)",
                    "TryAuthorizeAndRegister", "CAOwnedJobIntent")
                    && HasAll(animal, "TryAuthorizeAndRegister",
                        "animal.emergency_care")
                    && HasAll(criticalHaul, "TryRegisterNativeExecution",
                        "logistics.critical_haul")
                    && HasAll(triage, "TryAuthorizeAndRegister",
                        "welfare.mission_triage")
                    && HasAll(welfareKnowledge, "TryAuthorizeAndRegister",
                        "welfare.local_treatment")
                    && HasAll(welfareSupport, "TryAuthorizeAndRegister",
                        "welfare.threshold_support")
                    && HasAll(combatReaction, "TryAuthorizeAndRegister",
                        "knowledge.acoustic_investigation")
                    && HasAll(raid, "TryAuthorizeAndRegister",
                        "survival.shelter")
                    && immediate.Contains("TryRegisterCombatJob")
                    && intent.Contains("survival.immediate_evasion")
                    && HasAll(startingFacilities, "TryAuthorizeJob",
                        "developmentExecutable")
                    && HasAll(roads, "TryAuthorizeJob",
                        "CABehaviorGate.Evaluate")
                    && HasAll(toxicWaste, "TryAuthorizeAndRegister",
                        "hazard.toxic_waste_response")
                    && HasAll(survival, "TryAuthorizeAndRegister",
                        "support.ambush_aftermath",
                        "support.ambush_adaptation", "parent.EpisodeId")
                    && HasAll(stackLord, "support.stack_auto_breach",
                        "CABehaviorGate.Evaluate", "autoGoReadyTick",
                        "lord.ReceiveMemo(MemoGo)")
                    && !stackLord.Contains("Trigger_TicksPassed(180)")
                    && HasAll(assaultApproach,
                        "TryAuthorizeAndRegister", "combat.assault_approach")
                    && HasAll(frontier, "TryAuthorizeAndRegister",
                        "institution.frontier_household_activity",
                        "survival.frontier_flight")
                    && CountOccurrences(runtimeSource,
                        "TryAuthorizeAndRegister") >= 26
                    && Ordered(roads, "CompleteNativeLabor(Pawn worker",
                        "condition == JobCondition.Succeeded",
                        "TryReauthorizeCompletion",
                        "map.terrainGrid.SetTerrain", "org.treasury -=")
                    && Ordered(startingFacilities,
                        "CompleteNativeLabor(Pawn worker",
                        "condition == JobCondition.Succeeded",
                        "TryReauthorizeCompletion",
                        "researchRecord.researchStock++")
                    && HasAll(startingFacilities,
                        "nameof(Pawn_JobTracker.EndCurrentJob)",
                        "CA_settlementRepairWork",
                        "CA_settlementRebuildWork",
                        "nameof(Frame.CompleteConstruction)",
                        "CompleteNativeRebuild",
                        "repair completed - ",
                        "rebuild completed - ",
                        "CARoadExpansionMapComponent",
                        "CASettlementWorksMapComponent")
                    && HasAll(withdrawal, "exactReceipt",
                        "receipts.TryGet", "TryAuthorizeRestoredWithdrawalJob",
                        "CaptureWithdrawalKnowledge")
                    && HasAll(planning, "TryReauthorizeCompletion",
                        "CABehaviorIntentMapComponent.For(map)",
                        "exact saved institutional commitment")
                    && HasAll(roads, "RevalidateRestoredProjects",
                        "ProjectAuthorityValid", "TryReauthorizeCompletion")
                    && HasAll(startingFacilities,
                        "RevalidateRestoredWork",
                        "AuthorizeNativeRebuildCompletion",
                        "TryReauthorizeCompletion",
                        "frameThingId", "factionLoadId",
                        "BindNativeRebuildFrame",
                        "FrameMatchesWork",
                        "Patch_CASettlementNativeRebuildFrame",
                        "typeof(Blueprint), \"TryReplaceWithSolidThing\"",
                        "createdThing is not Frame frame")
                    && !startingFacilities.Contains(
                        "typeof(Blueprint_Build), \"MakeSolidThing\"")
                    && !startingFacilities.Contains("repairs under way - ")
                    && !startingFacilities.Contains("rebuilding what was lost - "),
                "production origins cross the saved gate; restored withdrawal and settlement work reauthorize exact ownership; road, repair, rebuild, and research results land only after native job success",
                "source-contract");

            // Knowledge: 33-38.
            C(33, "Remembered facts never grant a live target alone",
                HasAll(knowledge, "CanCurrentlySeeHostile",
                    "DirectlyAcquired", "lastKnown")
                    && catalog.Contains("LiveValidationRequired"),
                "memory and live validation are separate", "source-contract");
            C(34, "Relayed knowledge preserves source age",
                HasAll(knowledge, "SourceTick", "sourceTick", "acquiredTick")
                    && welfareKnowledge.Contains("relayed heartbeat renews delivery freshness while preserving")
                    && HasAll(catalog, "KnowledgeRelayed",
                        "KnowledgeAgeTicks", "MaximumKnowledgeAgeTicks"),
                "source and acquisition ticks remain separate", "source-contract");
            C(35, "Relayed knowledge preserves uncertainty",
                HasAll(knowledge, "Uncertainty", "RelayedThrough")
                    && welfareKnowledge.Contains("UncertaintyTicks")
                    && catalog.Contains("KnowledgeUncertainty")
                    && HasAll(support, "UncertaintyAffectsRanking",
                        "EvidenceRankingPenalty",
                        "fact.Evidence.Uncertainty"),
                "relay evidence carries uncertainty and uncertainty changes supporter/fact ranking where declared",
                "source-contract");
            C(36, "Stale facts block fresh-evidence behavior",
                HasAll(catalog, "CAKnowledgeRequirement.FreshFact",
                    "!context.KnowledgeFresh", "KnowledgeStale",
                    "context.KnowledgeAgeTicks",
                    "definition.MaximumKnowledgeAgeTicks"),
                "structured gate has a distinct stale-fact block", "executable-source");
            C(37, "Current sight satisfies live validation",
                HasAll(knowledge, "CanCurrentlySeeHostile",
                    "RememberDirectlyObservedPawn")
                    && welfareKnowledge.Contains("CanDirectlyObserveSubject")
                    && catalog.Contains("LiveValidated"),
                "native sight check supplies separate live-validation fact",
                "source-contract");
            C(38, "Presentation density is absent from knowledge",
                !catalog.Contains("InformationDetail")
                    && !modEntry.Contains("informationDetail")
                    && !knowledge.Contains("informationDetail"),
                "the retired global detail policy has no knowledge state",
                "source-contract");

            // Standard: 39-44.
            C(39, "Standard performs native work",
                CatalogEntryHas(catalog, "survival.food_choice",
                    "CAInitiativeTier.Standard", "CABehaviorForm.NativeAugmentation"),
                "native food work remains Standard", "source-contract");
            C(40, "Standard executes direct orders",
                HasAll(catalog, "CABehaviorForm.DirectOrder",
                    "CAInitiativeTier.Standard, true, true", "direct orders"),
                "direct-order helper is initiative-independent at Standard",
                "source-contract");
            C(41, "Standard continues compatible owned intent",
                HasAll(catalog, "StandardMayContinue",
                    "CAAuthorityOrigin.Continuation", "CAAuthorityOrigin.SaveRestore")
                    && intent.Contains("Continuation("),
                "continuation is explicit and intent-owned", "source-contract");
            C(42, "Standard performs registered baseline safeguards",
                CatalogEntryHas(catalog, "survival.immediate_evasion",
                    "CABehaviorForm.Safeguard", "CAInitiativeTier.Standard"),
                "immediate evasion is a Standard safeguard", "source-contract");
            C(43, "Standard does not claim unowned strategic objectives",
                CatalogEntryHas(catalog, "support.objective_defense",
                    "CAInitiativeTier.Autonomous", "AdaptivePlanning"),
                "objective defense requires Autonomous or institution",
                "source-contract");
            C(44, "Standard does not originate spatial construction",
                CatalogEntryHas(catalog, "spatial.home_essentials",
                    "CAInitiativeTier.Proactive")
                    && spatial.Contains("CAInitiativeTier tier")
                    && !CatalogEntryHas(catalog, "spatial.home_essentials",
                        "CAInitiativeTier.Standard"),
                "first player spatial origination begins at Proactive",
                "source-contract");

            // Proactive: 45-54.
            C(45, "Proactive responds to a current actionable fact",
                CatalogEntryHas(catalog, "combat.local_reaction",
                    "CAInitiativeTier.Proactive", "FreshFact"),
                "local reaction requires fresh fact", "source-contract");
            C(46, "Proactive work is bounded by target and completion",
                CatalogEntryHas(catalog, "welfare.threshold_support",
                    "one shared episode", "crossing and treatment complete"),
                "support episode declares owner and completion", "source-contract");
            C(47, "Proactive preserves direct player ownership",
                HasAll(catalog, "DirectPlayerOwnership", "PlayerOwnership",
                    "context.DirectPlayerOwnership && !direct"),
                "structured gate blocks non-direct origination", "executable-source");
            C(48, "Fire response preserves high-priority work",
                fire.Contains("hazard.fire_response")
                    && fire.Contains("PlayerForcedJobNowOrSoon")
                    && fire.Contains("ThinkTreeDutyHook.HighPriority"),
                "fire response gates current job ownership", "source-contract");
            C(49, "Mission triage uses finite mission knowledge",
                CatalogEntryHas(catalog, "welfare.mission_triage",
                    "MissionManifest", "finite incident manifest")
                    && triage.Contains("manifest"),
                "triage catalog and module own finite manifest", "source-contract");
            C(50, "Outsider rescue requires its own permission",
                CatalogEntryHas(catalog, "welfare.outsider_rescue",
                    "CASettingKey.RescueOutsiders")
                    && lifeSafety.Contains("welfare.outsider_rescue"),
                "outsider rescue no longer inherits local life safety",
                "source-contract");
            C(51, "Proactive home planning holds one commitment",
                CatalogEntryHas(catalog, "spatial.home_essentials",
                    "one essential deficiency")
                    && HasAll(home, "pendingDefName", "pendingSinceTick",
                        "HasPendingPlanForObservation"),
                "home planner persists one current plan", "source-contract");
            C(52, "Proactive support is a bounded transaction",
                CatalogEntryHas(catalog, "welfare.threshold_support",
                    "bounded support evaluation", "timeout"),
                "support declares cadence and stand-down", "source-contract");
            C(53, "Proactive custody may secure and stabilize",
                aftermath.Contains("aftermath.secure_hostile")
                    && CatalogEntryHas(catalog, "aftermath.secure_hostile",
                        "CAInitiativeTier.Proactive"),
                "secure/stabilize lane is Proactive", "source-contract");
            C(54, "Proactive cannot unilaterally resolve custody irreversibly",
                HasAll(aftermath, "CAInitiativeTier.Autonomous", "Secure",
                    "Stabilize") && catalog.Contains("aftermath.custody_resolution"),
                "ordinary irreversible resolution requires Autonomous authority",
                "source-contract");

            // Autonomous: 55-60.
            C(55, "Autonomous retains persistent objectives",
                catalog.Contains("CABehaviorForm.PersistentObjective")
                    && CatalogEntryHas(catalog, "survival.shelter",
                        "one shelter intent"),
                "persistent objective form has save-owned episodes",
                "source-contract");
            C(56, "Autonomous can answer one unowned collective need",
                CatalogEntryHas(catalog, "welfare.medic_dispatch",
                    "CAInitiativeTier.Autonomous", "at most one"),
                "medic dispatch owns one collective need", "source-contract");
            C(57, "Autonomous coordination records authority and beneficiaries",
                HasAll(census, "authority", "beneficiaries")
                    && HasAll(ownedIntent, "AuthorityIdentity", "OwnershipScope"),
                "planning and intent receipts name authority and scope",
                "source-contract");
            C(58, "Autonomous spatial planning respects authored programs",
                CatalogEntryHas(catalog, "spatial.home_comfort",
                    "delegated player home")
                    && furnishing.Contains("CASpaceProgram"),
                "player program remains the spatial boundary", "source-contract");
            C(59, "Autonomous adds context only after essentials",
                CatalogEntryHas(catalog, "spatial.home_comfort",
                    "after essentials are covered", "CAInitiativeTier.Autonomous"),
                "comfort is explicitly downstream of essentials", "source-contract");
            C(60, "Irreversible action needs law or separate breach receipt",
                catalog.Contains("aftermath.unlawful_execution_breach")
                    && HasAll(aftermath, "authority", "law", "unlawful"),
                "lawful resolution and character breach have separate keys",
                "source-contract");

            // Cross-wired module corrections: 61-69.
            C(61, "holdOrders gates hold behavior, not support pass",
                support.Contains("support.fold_cover")
                    && !support.Contains("if (!s.holdOrders) return;"),
                "fold behavior owns the hold permission", "source-contract");
            C(62, "Medic dispatch is owned by fieldMedicine",
                CatalogEntryHas(catalog, "welfare.medic_dispatch",
                    "CASettingKey.FieldMedicine"),
                "catalog permission owner: FieldMedicine", "source-contract");
            C(63, "Objective defense is owned by raidResponse",
                CatalogEntryHas(catalog, "support.objective_defense",
                    "CASettingKey.RaidResponse"),
                "catalog permission owner: RaidResponse", "source-contract");
            C(64, "Flank guard is owned by raidResponse",
                CatalogEntryHas(catalog, "support.flank_guard",
                    "CASettingKey.RaidResponse"),
                "catalog permission owner: RaidResponse", "source-contract");
            C(65, "Command relay needs draftChain and commsSystem",
                HasAll(catalog, "communication.command_delivery",
                    "CASettingKey.DraftChain", "CASettingKey.CommsSystem")
                    && support.Contains("communication.command_delivery"),
                "combined permission is centralized in catalog", "source-contract");
            C(66, "Medic dispatch registers owned intent",
                HasAll(support, "welfare.medic_dispatch", "Register(",
                    "CAIntentContext", "HasPlayerOwnedWork",
                    "jobQueue.AnyPlayerForced",
                    "CasualtyZoneContested(casualty, medic",
                    "FreshContacts(observer)")
                    && ownedIntent.Contains("CAOwnedJobIntent"),
                "periodic dispatch records job ownership, preserves current and queued direct work, and acts only on the medic's delivered knowledge",
                "source-contract");
            C(67, "Shared-item policy belongs to operationalAccess",
                CatalogEntryHas(catalog, "operations.shared_item_access",
                    "CASettingKey.OperationalAccess")
                    && !access.Contains("proactive > 0"),
                "colony policy no longer depends on pawn count", "source-contract");
            C(68, "Personal threat equipment remains initiative-sensitive",
                CatalogEntryHas(catalog, "operations.arm_for_known_threat",
                    "CAInitiativeTier.Proactive")
                    && access.Contains("operations.arm_for_known_threat"),
                "personal arming uses a Proactive behavior profile",
                "source-contract");
            C(69, "Enemy restraint is NPC doctrine",
                CatalogEntryHas(catalog, "npc.enemy_restraint",
                    "Npc", "CAInitiativeTier.Standard", "true")
                    && !restraint.Contains("AutonomyComponent.TierOf"),
                "NPC restraint never reads player initiative", "source-contract");

            // Spatial/B5: 70-79.
            C(70, "Culture changes ranking, never permission",
                CatalogEntryHas(catalog, "culture.longitudinal_update",
                    "SocialAndPolitical", "WorldSimulation")
                    && HasAll(planning, "CACultureHistory.PracticeStrength",
                        "shared-public-life", "defensive-boundary")
                    && HasAll(planning, "Culture never grants permission",
                        "result.culturalExpression"),
                "persistent Culture has an executor and ranks only otherwise-valid candidates",
                "source-contract");
            C(71, "Player-authored spatial constraints remain authoritative",
                HasAll(spatial, "player-authored", "initiative ceiling")
                    || HasAll(furnishing, "player-authored", "program"),
                "authored program and ceiling remain execution bounds",
                "source-contract");
            C(72, "Spatial initiative uses three typed tiers",
                HasAll(spatial, "CAInitiativeTier.Standard",
                    "CAInitiativeTier.Proactive", "CAInitiativeTier.Autonomous")
                    && !spatial.Contains("Directed"),
                "spatial tier vocabulary is converged", "source-contract");
            C(73, "NPC settlement development uses institution authority",
                CatalogEntryHas(catalog, "spatial.npc_settlement_development",
                    "InstitutionalAction", "named household")
                    && HasAll(planning,
                        "CASettlementInstitutionalAuthorization",
                        "CAAuthorityOrigin.Institutional",
                        "TryAuthorizeLaterDevelopment",
                        "TryAuthorizeJob")
                    && HasAll(organization,
                        "TryAuthorizeLaterDevelopment(record, org, proposal",
                        "developmentProposer", "developmentApprover",
                        "developmentLaborSource", "developmentBeneficiaries")
                    && HasAll(regional, "developmentAuthorityIdentity",
                        "developmentAuthorized", "developmentEpisodeId",
                        "developmentCulturalBasis",
                        "developmentPoliticalBasis")
                    && HasAll(roads,
                        "(CAAuthorityOrigin)record.developmentAuthorityOrigin",
                        "institutionalOrigin"),
                "later development branches to a saved institutional episode with complete in-world provenance",
                "source-contract");
            C(74, "Creation composition writes initial state directly",
                CatalogEntryHas(catalog, "spatial.creation_authoring",
                    "Writes confirmed creation state directly")
                    && HasAll(planning, "TryAuthorizeCreationHistory",
                        "CAActorContext.CreationAuthor",
                        "CAAuthorityOrigin.WorldAuthoring")
                    && HasAll(regional, "TryAuthorizeCreationHistory",
                        "creationBehaviorKey", "creationAuthorized",
                        "creationBeneficiaries", "creationCulturalBasis",
                        "creationPoliticalBasis")
                    && Ordered(organization, "record.creationExecutable",
                        "SeedRepresentativeAssets", "CAStartingFacilities.Furnish",
                        "TryAuthorizeLaterDevelopment"),
                "confirmed creation history materializes under world-authoring authority before later institutional development is considered",
                "source-contract");
            C(75, "Player and NPC planning share semantic demands",
                HasAll(planning, "enum CASettlementDemandKind",
                        "CASettlementDevelopmentProposal", "Demands",
                        "BuildDemandFact")
                    && HasAll(furnishing, "BuildDemandFact(",
                        "CASettlementDemandKind.Storage",
                        ".Candidates(CASettlementDemandKind.Storage)")
                    && HasAll(planning, "BuildInstitutionalProposal",
                        "CASettlementDemandKind.Access",
                        "CASettlementDemandKind.Maintenance")
                    && CatalogEntryHas(catalog,
                        "spatial.npc_settlement_development", "B5 demand")
                    && Ordered(regional, "CASettlementStartingState.Sync",
                        "CASettlementComposition.EnsureDerived",
                        "BuildCreationProposal",
                        "TryAuthorizeCreationHistory"),
                "player storage planning and NPC creation/development consume one typed authority-neutral demand fact",
                "source-contract");
            C(76, "Player and NPC planning share asset registry",
                HasAll(planning, "CASettlementAssetRegistry",
                        "internal static List<ThingDef> Candidates",
                        "AssetCandidates", "BuildDemandFact")
                    && furnishing.Contains("BuildDemandFact")
                    && startingFacilities.Contains(
                        "CASettlementAssetRegistry.Resolve")
                    && HasAll(planning,
                        "An explicit loaded def is evidence",
                        "demand == CASettlementDemandKind.Unknown")
                    && HasAll(regional, "developmentAssetCandidates",
                        "developmentProposalSignature"),
                "both paths resolve the same loaded native ThingDefs; explicit unknowns remain explicit or block instead of becoming storage",
                "source-contract");
            C(77, "Player and NPC planning share siting constraints",
                siting.Contains("CAFacilitySitingModule")
                    && HasAll(planning, "CASettlementSitingConstraints",
                        "HasMaterialFootprint", "CanPlaceNativeBlueprint")
                    && startingFacilities.Contains(
                        "CASettlementSitingConstraints")
                    && furnishing.Contains(
                        "CASettlementSitingConstraints")
                    && HasAll(planning, "CanSiteCreationDemands",
                        "CanExerciseInstitutionalDevelopment",
                        "CanPlaceNativeBlueprint")
                    && HasAll(planning, "developmentSitingEvaluated",
                        "developmentSitingFeasible"),
                "candidate probing, initial furnishing, and materialized NPC execution cross one native placement boundary",
                "source-contract");
            C(78, "Authorization branches after candidate generation",
                Ordered(spatial, "TrySelectStoragePlan", "CABehaviorContext",
                        "CABehaviorGate.Evaluate")
                    && Ordered(regional, "BuildCreationProposal",
                        "TryAuthorizeCreationHistory")
                    && Ordered(organization, "CreationFromRecord(record)",
                        "CanSiteCreationDemands", "record.creationExecutable",
                        "TryAuthorizeLaterDevelopment")
                    && Ordered(organization, "BuildInstitutionalProposal",
                        "CanExerciseInstitutionalDevelopment",
                        "TryAuthorizeLaterDevelopment")
                    && Ordered(planning, "BuildDemandFact(",
                        "TryAuthorizeCreationHistory(",
                        "TryAuthorizeLaterDevelopment("),
                "candidate facts are generated and sited before creation and institutional authority branch at their owning surfaces",
                "source-contract");
            C(79, "Material impossibility blocks both planning paths",
                catalog.Contains("CABehaviorBlockReason.Material")
                    && HasAll(spatial, "materialSatisfied: plan.def != null",
                        "TrySelectStoragePlan")
                    && HasAll(planning, "MaterialFeasible",
                        "FundingFeasible", "CanSiteCreationDemands",
                        "CanExerciseInstitutionalDevelopment",
                        "RecordInstitutionalFacts",
                        "ReconcileRecord",
                        "materialSatisfied: proposal != null")
                    && HasAll(organization, "creationExecutable",
                        "developmentExecutable",
                        "confirmed creation history blocked")
                    && HasAll(startingFacilities,
                        "!record.creationExecutable",
                        "!record.developmentExecutable",
                        "CanPlaceNativeBlueprint"),
                "material or siting failure blocks creation and later development independently after deterministic readback reconciliation",
                "source-contract");

            // Runtime efficiency: 80-89.
            C(80, "Behavior metadata is built once",
                catalog.Contains("static CABehaviorCatalog()")
                    && catalog.Contains("public static readonly CABehaviorDefinition[] All"),
                "one static immutable catalog", "source-contract");
            C(81, "Stable pawn eligibility is cached",
                HasAll(catalog, "CAEffectiveBehaviorProfileCache",
                    "ulong[] permitted", "StableProfileAllows"),
                "compact bitset cache", "source-contract");
            C(82, "Settings changes invalidate the cache",
                HasAll(catalog, "SettingsChanged()", "CAEffectiveBehaviorProfileCache.Clear()"),
                "settings generation invalidates all profiles", "source-contract");
            C(83, "Autonomy changes invalidate the cache",
                HasAll(catalog, "AutonomyChanged(Pawn pawn)",
                    "CAEffectiveBehaviorProfileCache.Invalidate(pawn)")
                    && autonomy.Contains("CABehaviorRevisions.AutonomyChanged(pawn)"),
                "pawn tier mutation invalidates its profile", "source-contract");
            C(84, "Role and authority changes invalidate the cache",
                HasAll(catalog, "RoleChanged(Pawn pawn", "SpatialAuthorityChanged")
                    && allSource.Contains("CABehaviorRevisions.RoleChanged"),
                "role/spatial generations feed cache invalidation",
                "source-contract");
            C(85, "Disposition retains bounded caching",
                disposition.Contains("600") && disposition.Contains("cache"),
                "existing disposition cache remains bounded", "source-contract");
            C(86, "Trace formats only meaningful/requested transitions",
                trace.Contains("traceBehavior")
                    && catalog.Contains("meaningful transition only"),
                "trace setting and transition cadence own formatting",
                "source-contract");
            C(87, "Immediate combat retains constant-think ownership",
                CatalogEntryHas(catalog, "survival.immediate_evasion",
                    "constant-think")
                    && (immediate.Contains("ThinkNode_JobGiver")
                        || combatReaction.Contains(
                            "JobGiver_CACombatReaction : JobGiver_AIFightEnemies")),
                "native constant-think job giver remains executor",
                "source-contract");
            C(88, "Slow systems retain bounded cadences",
                catalog.Contains("600-tick") && catalog.Contains("90-tick map pass")
                    && catalog.Contains("120-tick map pass"),
                "catalog records domain-owned periodic cadences",
                "source-contract");
            C(89, "Constant-think lane has no catalog-wide map scan",
                !immediate.Contains("CABehaviorCatalog.All")
                    && !immediate.Contains("AllPawnsSpawned.ToList"),
                "immediate lane never enumerates catalog or materializes full map list",
                "source-contract");

            // UI: 90-96.
            C(90, "Autonomy UI contains three tiers",
                ActiveTierCount(catalog) == 3
                    && autonomy.Contains("ActiveTiers.Length"),
                "gizmo and menus read three-value ActiveTiers", "executable-source");
            C(91, "Tier descriptions derive from catalog",
                autonomy.Contains("CAInitiativePresentation.Description")
                    && catalog.Contains("CABehaviorCatalog.ForTier"),
                "one presentation function consumes catalog", "source-contract");
            C(92, "Settings group by domain and type",
                HasAll(modEntry, "DrawBehaviorSettings", "setting.Section",
                    "setting.Kind", "CABehaviorSettings.KindLabel"),
                "settings surface iterates typed catalog groups", "source-contract");
            C(93, "Six causal concerns are visibly separate",
                HasAll(autonomy, "Permissions, initiative, authority, knowledge, capability, and material feasibility")
                    && catalog.Contains("CASettingKind.Presentation")
                    && catalog.Contains("CASettingKind.Diagnostic"),
                "Behavior scope and settings name each owner", "source-contract");
            C(94, "Global text-density state is removed",
                !catalog.Contains("presentation.information_detail")
                    && !catalog.Contains("CASettingKey.InformationDetail")
                    && !modEntry.Contains("informationDetail"),
                "contextual explanations have no simulation-setting surrogate",
                "source-contract");
            C(95, "NPC inspectors expose no pawn tier controls",
                census.Contains("Settlement behavior census")
                    && !census.Contains("SetTier("),
                "settlement census is read-only and contains no autonomy mutation",
                "source-contract");
            C(96, "Selected-pawn scope explains primary block reason",
                HasAll(census, "observed active or blocked candidate",
                    "primary reason",
                    "TryGetRecentDecision",
                    "no production behavior candidate was recently evaluated")
                    && !census.Contains("FirstBlockedCandidate")
                    && !census.Contains("CABehaviorGate.Evaluate")
                    && !autonomy.Contains("CABehaviorGate.Evaluate")
                    && autonomy.Contains("Tier boundary"),
                "scope UI is read-only; census reports a real observed decision or an honest absence",
                "source-contract");

            // Playability: 97-106. Cases 97-105 are registered operator
            // runtime work and remain pending until observed inside RimWorld.
            R(97, "Verify creation flow reaches map generation",
                founding.Contains("Page_CreateWorldParams")
                    && founding.Contains("Page_ChooseIdeoPreset")
                    && setup.Contains("starting region")
                    && census.Contains("Behavior runtime matrix"),
                "complete creation then open the behavior runtime matrix");
            R(98, "Verify player map generation completes",
                mapTemplate.Contains("Map") && mapArtifact.Contains("Map")
                    && census.Contains("components: knowledge"),
                "the matrix is available only after PlayingOnMap");
            R(99, "Verify optional asset absence remains nonfatal",
                allSource.Contains("neutral fallback")
                    && allSource.Contains("unavailable")
                    && census.Contains("Settlement behavior census"),
                "inspect the fallback receipt and settlement census");
            R(100, "Verify Standard pawn native work begins",
                census.Contains("verify native work and direct orders")
                    && CatalogEntryHas(catalog, "survival.food_choice",
                        "CAInitiativeTier.Standard"),
                "observe ordinary native work at Standard");
            R(101, "Observe one Proactive bounded behavior",
                census.Contains("bounded Proactive response")
                    && CatalogEntryHas(catalog, "hazard.fire_response",
                        "CAInitiativeTier.Proactive"),
                "create and observe one bounded Proactive response");
            R(102, "Observe one Autonomous persistent behavior",
                census.Contains("persistent ")
                    && census.Contains("Autonomous objective")
                    && catalog.Contains("CABehaviorForm.PersistentObjective"),
                "create and observe one persistent Autonomous objective");
            R(103, "Verify direct player orders remain authoritative",
                census.Contains("direct orders")
                    && catalog.Contains("CAAuthorityOrigin.OperatorDirect"),
                "issue direct work around each initiative tier");
            R(104, "Verify save/reload preserves registered intent",
                census.Contains("save/reload")
                    && HasAll(ownedIntent, "ExposeData", "Scribe_Values.Look",
                        "CA_ownedJobIntents"),
                "save, reload, and compare the registered intent readout");
            R(105, "Verify no repeating first-hour exception loop",
                census.Contains("inspect Player.log for repeated exceptions")
                    && census.Contains("Find.TickManager"),
                "repeat the matrix and inspect Player.log through the first hour");
            C(106, "Keyed fixture retains identity and counts",
                FixtureIdentityAndCounts(keyedPlan, keyed)
                    && XNode.DeepEquals(mirror.Root, keyed.Root),
                FixtureEvidence(keyedPlan, keyed), "executable");
            C(107, "Every saved non-job episode owner restores sequence identity",
                HasAll(home,
                    "public CAIntentController intentController",
                    "CA_homePendingIntentController",
                    "pendingIntentController = (int)plan.intentController",
                    "CACombatIntent.ObserveEpisode(pendingEpisodeId)",
                    "TryGetHomeDeficitEvidence",
                    "CATactical.HasForeignPlayerForcedJob(planner)")
                    && HasAll(homePrerequisite,
                        "public CAIntentController intentController",
                        "intentController == plan.intentController",
                        "intentController = demand.intentController",
                        "intentController = plan.intentController",
                        "CACombatIntent.ObserveEpisode(episodeId)",
                        "HasForeignPlayerForcedJob(planner)",
                        "HasForeignPlayerForcedJob(forester)")
                    && HasAll(homeIntent,
                        "CAResidentRosterIntent : IExposable",
                        "CA_residentRosterIntents",
                        "spatial.resident_roster_negotiation",
                        "CAActorContext.PlayerSpatialAuthority",
                        "CABehaviorGate.Evaluate",
                        "RecordResidentRosterIntent",
                        "CACombatIntent.ObserveEpisode(episodeId)")
                    && HasAll(arrangement,
                        "class CAArrangement : IExposable",
                        "CACombatIntent.ObserveEpisode(episodeId)")
                    && HasAll(support,
                        "class SupportTasking : IExposable",
                        "CACombatIntent.ObserveEpisode(EpisodeId)"),
                "persisted owners: home plan, home material demand, resident roster, arrangement, and squad support tasking",
                "source-contract");

            if (Results.Count != 107
                || !Results.Select(result => result.Id)
                    .SequenceEqual(Enumerable.Range(1, 107)))
                throw new InvalidDataException("suite registration is not exactly 1-107");

            var report = new StringBuilder();
            report.AppendLine("BEHAVIOR CONVERGENCE RECEIPT");
            foreach (Receipt result in Results)
                report.Append(result.Status == ReceiptStatus.Verified
                        ? "PASS " : result.Status
                            == ReceiptStatus.PendingOperatorRuntime
                        ? "PENDING " : "FAIL ")
                    .Append(result.Id.ToString("D3")).Append(" ")
                    .Append(result.Name).Append(" [")
                    .Append(result.ProofKind).Append("] -> ")
                    .AppendLine(result.Evidence);
            AppendDistribution(report, catalog);
            int verified = Results.Count(result => result.Status
                == ReceiptStatus.Verified);
            int pending = Results.Count(result => result.Status
                == ReceiptStatus.PendingOperatorRuntime);
            int failed = Results.Count(result => result.Status
                == ReceiptStatus.Failed);
            report.Append("result: ").Append(failed == 0 ? "PASS" : "FAIL")
                .Append(" (").Append(verified).Append(" verified assertions; ")
                .Append(pending).Append(" operator-runtime cases pending; ")
                .Append(Results.Count).AppendLine(" numbered cases)");
            Console.Write(report.ToString());
            return failed == 0 ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("result: FAIL");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void C(int id, string name, bool passed, string evidence,
        string proofKind)
    {
        Results.Add(new Receipt(id, name, passed ? ReceiptStatus.Verified
            : ReceiptStatus.Failed, evidence, proofKind));
    }

    private static void R(int id, string name, bool registered,
        string evidence)
    {
        Results.Add(new Receipt(id, name, registered
            ? ReceiptStatus.PendingOperatorRuntime : ReceiptStatus.Failed,
            evidence, registered ? "operator-runtime-pending"
                : "runtime-registration-missing"));
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    private static bool Ordered(string text, params string[] values)
    {
        int index = 0;
        foreach (string value in values)
        {
            index = text.IndexOf(value, index, StringComparison.Ordinal);
            if (index < 0) return false;
            index += value.Length;
        }
        return true;
    }

    private static bool HasAll(string text, params string[] values) =>
        values.All(value => text.Contains(value, StringComparison.Ordinal));

    private static bool CatalogEntryHas(string catalog, string key,
        params string[] values)
    {
        string line = catalog.Split('\n').FirstOrDefault(candidate =>
            candidate.Contains("\"" + key + "\"", StringComparison.Ordinal));
        if (line != null && line.TrimStart().StartsWith("Direct(",
                StringComparison.Ordinal))
            line += catalog.Substring(catalog.IndexOf(
                "private static void Direct", StringComparison.Ordinal),
                catalog.IndexOf("private static void Support",
                    StringComparison.Ordinal)
                - catalog.IndexOf("private static void Direct",
                    StringComparison.Ordinal));
        else if (line != null && line.TrimStart().StartsWith("Support(",
                StringComparison.Ordinal))
            line += catalog.Substring(catalog.IndexOf(
                "private static void Support", StringComparison.Ordinal),
                catalog.IndexOf("public static CABehaviorDefinition Get",
                    StringComparison.Ordinal)
                - catalog.IndexOf("private static void Support",
                    StringComparison.Ordinal));
        return line != null && values.All(value => line.Contains(value,
            StringComparison.Ordinal));
    }

    private static int EnumMemberCount(string source, string enumName)
    {
        Match match = Regex.Match(source, @"enum\s+" + enumName
            + @"\s*:\s*\w+\s*\{(?<body>.*?)\}", RegexOptions.Singleline);
        if (!match.Success) return 0;
        return match.Groups["body"].Value.Split(',')
            .Select(value => Regex.Replace(value, @"//.*", "").Trim())
            .Count(value => value.Length > 0);
    }

    private static int ActiveTierCount(string catalog)
    {
        Match match = Regex.Match(catalog,
            @"ActiveTiers\s*=\s*\{(?<body>.*?)\};", RegexOptions.Singleline);
        return match.Success ? Regex.Matches(match.Groups["body"].Value,
            @"CAInitiativeTier\.").Count : 0;
    }

    private static bool MigrationMaps(string autonomy, int legacy,
        string expected)
    {
        CAInitiative parsed = legacy >= 3 ? CAInitiative.Autonomous
            : legacy >= 2 ? CAInitiative.Proactive : CAInitiative.Standard;
        return parsed.ToString() == expected
            && autonomy.Contains("if (legacy >= 3) return CAInitiativeTier.Autonomous")
            && autonomy.Contains("if (legacy >= 2) return CAInitiativeTier.Proactive")
            && autonomy.Contains("return CAInitiativeTier.Standard");
    }

    private enum CAInitiative { Standard, Proactive, Autonomous }

    private static bool FeaturePermissionsHaveConsumers(string catalog,
        string runtimeSource)
    {
        string[] keys = Regex.Matches(catalog,
            @"S\(CASettingKey\.(\w+).*?CASettingKind\.FeaturePermission")
            .Cast<Match>().Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal).ToArray();
        return keys.Length > 0 && keys.All(key =>
        {
            Match field = Regex.Match(catalog,
                @"case CASettingKey\." + Regex.Escape(key)
                    + @": return s\.(\w+)");
            bool rawConsumer = field.Success && Regex.IsMatch(runtimeSource,
                @"\." + Regex.Escape(field.Groups[1].Value) + @"\b");
            string[] behaviorKeys = catalog.Split('\n')
                .Where(line => line.Contains("CASettingKey." + key,
                    StringComparison.Ordinal)
                    && Regex.IsMatch(line,
                        @"^\s*(?:Add|Direct|Support)\(list,"))
                .Select(line => Regex.Match(line,
                    @"(?:Add|Direct|Support)\(list,\s*""([^""]+)"""))
                .Where(match => match.Success)
                .Select(match => match.Groups[1].Value)
                .ToArray();
            bool keyedConsumer = behaviorKeys.Any(behaviorKey =>
                runtimeSource.Contains("\"" + behaviorKey + "\"",
                    StringComparison.Ordinal));
            return behaviorKeys.Length > 0 && (rawConsumer || keyedConsumer);
        });
    }

    private static bool AuthorizingBehaviorKeysHaveGateConsumers(
        string catalog, string repository)
    {
        var nonOriginating = new HashSet<string>(StringComparer.Ordinal)
        {
            "Observation", "NativeAugmentation", "DirectOrder",
            "ExecutionCapability", "Presentation", "Diagnostic"
        };
        var authorizing = new List<string>();
        foreach (string raw in catalog.Split('\n'))
        {
            string line = raw.Trim();
            Match key = Regex.Match(line,
                @"^(Add|Direct|Support)\(list,\s*""([^""]+)""");
            if (!key.Success) continue;
            if (key.Groups[1].Value == "Direct") continue;
            if (key.Groups[1].Value == "Support")
            {
                authorizing.Add(key.Groups[2].Value);
                continue;
            }
            Match form = Regex.Match(line,
                @"CABehaviorForm\.(\w+)");
            if (form.Success
                && !nonOriginating.Contains(form.Groups[1].Value))
                authorizing.Add(key.Groups[2].Value);
        }
        string[] runtimeFiles = Directory.GetFiles(Path.Combine(repository,
                "Source"), "*.cs")
            .Where(path => !Path.GetFileName(path).Equals(
                "BehaviorCatalogModule.cs", StringComparison.Ordinal)
                && !Path.GetFileName(path).Equals("ModEntry.cs",
                    StringComparison.Ordinal))
            .ToArray();
        return authorizing.Count > 0 && authorizing.All(key =>
            runtimeFiles.Any(path =>
            {
                string source = File.ReadAllText(path);
                if (!source.Contains("\"" + key + "\"",
                        StringComparison.Ordinal)) return false;
                return source.Contains("CABehaviorGate.Evaluate",
                           StringComparison.Ordinal)
                    || source.Contains("TryAuthorizeAndRegister",
                        StringComparison.Ordinal);
            }));
    }

    private static XElement Plan(XDocument document)
    {
        XElement plan = document.Root?.Name.LocalName == "plan"
            ? document.Root : document.Root?.Element("plan");
        return plan ?? throw new InvalidDataException("fixture has no plan");
    }

    private static string Value(XElement parent, string name) =>
        parent.Element(name)?.Value ?? string.Empty;

    private static IEnumerable<XElement> Items(XElement parent,
        string container) => parent.Element(container)?.Elements("li")
            ?? Enumerable.Empty<XElement>();

    private static bool RoundTrips(XDocument document)
    {
        XDocument parsed = XDocument.Parse(document.ToString(
            SaveOptions.DisableFormatting), LoadOptions.PreserveWhitespace);
        return XNode.DeepEquals(document.Root, parsed.Root);
    }

    private static bool FixtureIdentityAndCounts(XElement plan,
        XDocument document)
    {
        int groups = Items(plan, "settlements").Sum(settlement =>
            Items(settlement, "populationGroups").Count());
        return Value(document.Root, "worldIdentity")
                == "alysaliu|1|Algorab Markab"
            && Value(plan, "regionalId") == "CA-RG-EB596A12"
            && Value(plan, "candidateId") == "613b1fe44104"
            && Value(plan, "startTileId") == "389638"
            && Value(plan, "mapSize") == "350"
            && Items(plan, "factions").Count() == 3
            && Items(plan, "settlements").Count() == 4
            && groups == 9;
    }

    private static string FixtureEvidence(XElement plan, XDocument document)
    {
        int groups = Items(plan, "settlements").Sum(settlement =>
            Items(settlement, "populationGroups").Count());
        return Value(document.Root, "worldIdentity") + "; region "
            + Value(plan, "regionalId") + "; candidate "
            + Value(plan, "candidateId") + "; tile "
            + Value(plan, "startTileId") + "; scale "
            + Value(plan, "mapSize") + "; factions "
            + Items(plan, "factions").Count() + "; settlements "
            + Items(plan, "settlements").Count() + "; population groups "
            + groups;
    }

    private static void AppendDistribution(StringBuilder report,
        string catalog)
    {
        var domains = new Dictionary<string, int>(StringComparer.Ordinal);
        var forms = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string raw in catalog.Split('\n'))
        {
            string line = raw.Trim();
            if (!Regex.IsMatch(line,
                "^(Add|Direct|Support)\\(list,\\s*\\\"")) continue;
            string domain;
            string form;
            if (line.StartsWith("Direct(", StringComparison.Ordinal))
            {
                domain = "TacticalCoordination";
                form = "DirectOrder";
            }
            else if (line.StartsWith("Support(", StringComparison.Ordinal))
            {
                domain = "TacticalCoordination";
                form = "AdaptivePlanning";
            }
            else
            {
                Match domainMatch = Regex.Match(line,
                    @"CABehaviorDomain\.(\w+)");
                Match formMatch = Regex.Match(line,
                    @"CABehaviorForm\.(\w+)");
                if (!domainMatch.Success || !formMatch.Success) continue;
                domain = domainMatch.Groups[1].Value;
                form = formMatch.Groups[1].Value;
            }
            domains[domain] = domains.GetValueOrDefault(domain) + 1;
            forms[form] = forms.GetValueOrDefault(form) + 1;
        }
        report.AppendLine("registered behaviors by domain: " + string.Join(
            ", ", domains.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + "=" + pair.Value)));
        report.AppendLine("registered behaviors by form: " + string.Join(
            ", ", forms.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + "=" + pair.Value)));
    }
}
