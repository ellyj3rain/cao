# Persistence census

Date: 2026-08-18

This report is generated from production C# source, independently of the campaign schema catalog. It discovers declarations that directly write through `Scribe`, call a nested `Expose` writer, or inherit a native persisted job/lord/need/thought/world/scenario owner. Every discovered carrier must resolve to an executable catalog schema or a narrow, stated non-campaign exclusion.

- Discovered persistence carriers: **258**
- Catalog schemas reached from source carriers: **87**
- Explicit non-campaign exclusions: **5**
- Unclassified or invalid routes: **0**
- Result: **PASS**

| Source declaration | Persistence evidence | Campaign schema or exclusion | Result |
|---|---|---|---|
| `ColonistAwareness.CAActLedger` in `Source/ActRecordModule.cs` | direct Scribe/nested Expose writer | `world.act-ledger` | **PASS** |
| `ColonistAwareness.CAActRecord` in `Source/ActRecordModule.cs` | direct Scribe/nested Expose writer | `world.act-ledger` | **PASS** |
| `ColonistAwareness.Thought_CAPoliticalBelief` in `Source/ActRecordModule.cs` | direct Scribe/nested Expose writer | `pawn.political-belief-memory` | **PASS** |
| `ColonistAwareness.JobDriver_CAFeedDownedAnimal` in `Source/AnimalCareModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.JobDriver_CAAnimalGunfireResponse` in `Source/AnimalReactionModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.CAArrangement` in `Source/ArrangementModule.cs` | direct Scribe/nested Expose writer | `map.arrangements` | **PASS** |
| `ColonistAwareness.CAArrangementMapComponent` in `Source/ArrangementModule.cs` | direct Scribe/nested Expose writer | `map.arrangements` | **PASS** |
| `ColonistAwareness.JobDriver_CASafeIgnite` in `Source/AssaultAwarenessModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.JobDriver_CASearchKnownContact` in `Source/AssaultAwarenessModule.cs` | direct Scribe/nested Expose writer | `map.assault-awareness` | **PASS** |
| `ColonistAwareness.JobDriver_CAInvestigateGunfire` in `Source/AudibleCueModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.CAUserCultureProfile` in `Source/AuthoringPresentationModule.cs` | direct Scribe/nested Expose writer | Excluded: global mod settings or user preset outside a realized campaign save | **PASS** |
| `ColonistAwareness.CAUserPoliticalOrderProfile` in `Source/AuthoringPresentationModule.cs` | direct Scribe/nested Expose writer | Excluded: global mod settings or user preset outside a realized campaign save | **PASS** |
| `ColonistAwareness.CAUserSocietyProfile` in `Source/AuthoringPresentationModule.cs` | direct Scribe/nested Expose writer | Excluded: global mod settings or user preset outside a realized campaign save | **PASS** |
| `ColonistAwareness.AutonomousHomeMapComponent` in `Source/AutonomousHomeModule.cs` | direct Scribe/nested Expose writer | `map.autonomous-home` | **PASS** |
| `ColonistAwareness.CAHomeBuiltRecord` in `Source/AutonomousHomeModule.cs` | direct Scribe/nested Expose writer | `map.autonomous-home` | **PASS** |
| `ColonistAwareness.AutonomyComponent` in `Source/AutonomyModule.cs` | direct Scribe/nested Expose writer | `game.autonomy` | **PASS** |
| `ColonistAwareness.CAEnvoyMission` in `Source/BattlefieldParleyModule.cs` | direct Scribe/nested Expose writer | `map.parley` | **PASS** |
| `ColonistAwareness.CAParleyMapComponent` in `Source/BattlefieldParleyModule.cs` | direct Scribe/nested Expose writer | `map.parley` | **PASS** |
| `ColonistAwareness.CAParleyMission` in `Source/BattlefieldParleyModule.cs` | direct Scribe/nested Expose writer | `map.parley` | **PASS** |
| `ColonistAwareness.CABehaviorIntentMapComponent` in `Source/BehaviorIntentModule.cs` | direct Scribe/nested Expose writer | `map.behavior-intent` | **PASS** |
| `ColonistAwareness.CAOwnedJobIntent` in `Source/BehaviorIntentModule.cs` | direct Scribe/nested Expose writer | `map.behavior-intent` | **PASS** |
| `ColonistAwareness.CACampaignCompatibilityWorldComponent` in `Source/CampaignCompatibilityModule.cs` | direct Scribe/nested Expose writer | `campaign.boundary` | **PASS** |
| `ColonistAwareness.CACampaignMigrationReceipt` in `Source/CampaignCompatibilityModule.cs` | direct Scribe/nested Expose writer | `campaign.boundary` | **PASS** |
| `ColonistAwareness.CACampaignSchemaRecord` in `Source/CampaignCompatibilityModule.cs` | direct Scribe/nested Expose writer | `campaign.boundary` | **PASS** |
| `Camping_Stuff.CompTentPartWithCellsDamage` in `Source/CampingStuff/Comps/CompTentPartWithCellsDamage.cs` | direct Scribe/nested Expose writer | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.TentSpawnedComp` in `Source/CampingStuff/Comps/TentSpawnedComp.cs` | direct Scribe/nested Expose writer | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.JobDriver_Fetch` in `Source/CampingStuff/Jobs/JobDriver_Fetch.cs` | native persisted owner (`JobDriver // JobDriver`) | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.JobDriver_PackBag` in `Source/CampingStuff/Jobs/JobDriver_PackBag.cs` | native persisted owner (`JobDriver_Fetch // JobDriver`) | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.JobDriver_RepairPart` in `Source/CampingStuff/Jobs/JobDriver_RepairPart.cs` | native persisted owner (`JobDriver_Fetch`) | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.JobDriver_RepairCoverInBag` in `Source/CampingStuff/Jobs/JobDriver_RepairPartInBag.cs` | native persisted owner (`JobDriver_RepairPart`) | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.JobDriver_RepairFloorInBag` in `Source/CampingStuff/Jobs/JobDriver_RepairPartInBag.cs` | native persisted owner (`JobDriver_RepairPart`) | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.JobDriver_UnpackBag` in `Source/CampingStuff/Jobs/JobDriver_UnPackBag.cs` | native persisted owner (`JobDriver`) | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.JobDriver_UnpackBagAll` in `Source/CampingStuff/Jobs/JobDriver_UnPackBag.cs` | native persisted owner (`JobDriver_UnpackBag`) | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.JobDriver_UnpackBagAllPoles` in `Source/CampingStuff/Jobs/JobDriver_UnPackBag.cs` | native persisted owner (`JobDriver_UnpackBag`) | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.JobDriver_UnpackBagCover` in `Source/CampingStuff/Jobs/JobDriver_UnPackBag.cs` | native persisted owner (`JobDriver_UnpackBag`) | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.JobDriver_UnpackBagFloor` in `Source/CampingStuff/Jobs/JobDriver_UnPackBag.cs` | native persisted owner (`JobDriver_UnpackBag`) | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.JobDriver_UnpackBagPole` in `Source/CampingStuff/Jobs/JobDriver_UnPackBag.cs` | native persisted owner (`JobDriver_UnpackBag`) | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.LayoutCache` in `Source/CampingStuff/LayoutCache.cs` | direct Scribe/nested Expose writer | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.LayoutUsage` in `Source/CampingStuff/LayoutCache.cs` | direct Scribe/nested Expose writer | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.SketchRoof` in `Source/CampingStuff/SketchRoof.cs` | direct Scribe/nested Expose writer | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.TentSpec` in `Source/CampingStuff/TentSpec.cs` | direct Scribe/nested Expose writer | `embedded.camping-state` | **PASS** |
| `Camping_Stuff.NCS_Tent` in `Source/CampingStuff/Things/NCS_Tent.cs` | direct Scribe/nested Expose writer | `embedded.camping-state` | **PASS** |
| `ColonistAwareness.CAAftermathAccountabilityMapComponent` in `Source/CombatAftermathModule.cs` | direct Scribe/nested Expose writer | `map.combat-aftermath` | **PASS** |
| `ColonistAwareness.CAExecutionAccountabilityRecord` in `Source/CombatAftermathModule.cs` | direct Scribe/nested Expose writer | `map.combat-aftermath` | **PASS** |
| `ColonistAwareness.JobDriver_CASecureEPW` in `Source/CombatAftermathModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.JobDriver_CACombatPosture` in `Source/CombatReactionModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.CACombatSpatialConcernSnapshot` in `Source/CombatSpatialLogModule.cs` | direct Scribe/nested Expose writer | `game.combat-spatial-log` | **PASS** |
| `ColonistAwareness.CACombatSpatialLogComponent` in `Source/CombatSpatialLogModule.cs` | direct Scribe/nested Expose writer | `game.combat-spatial-log`, `game.combat-topology` | **PASS** |
| `ColonistAwareness.CACombatSpatialLogRecord` in `Source/CombatSpatialLogModule.cs` | direct Scribe/nested Expose writer | `game.combat-spatial-log` | **PASS** |
| `ColonistAwareness.CACombatSpatialLogComponent` in `Source/CombatTopologyModule.cs` | direct Scribe/nested Expose writer | `game.combat-spatial-log`, `game.combat-topology` | **PASS** |
| `ColonistAwareness.CACombatTopologyBattlefieldReference` in `Source/CombatTopologyModule.cs` | direct Scribe/nested Expose writer | `game.combat-topology` | **PASS** |
| `ColonistAwareness.CACombatTopologyCellState` in `Source/CombatTopologyModule.cs` | direct Scribe/nested Expose writer | `game.combat-topology` | **PASS** |
| `ColonistAwareness.CACombatTopologyDelta` in `Source/CombatTopologyModule.cs` | direct Scribe/nested Expose writer | `game.combat-topology` | **PASS** |
| `ColonistAwareness.CACombatTopologyHazardCell` in `Source/CombatTopologyModule.cs` | direct Scribe/nested Expose writer | `game.combat-topology` | **PASS** |
| `ColonistAwareness.CACombatTopologyHazardSample` in `Source/CombatTopologyModule.cs` | direct Scribe/nested Expose writer | `game.combat-topology` | **PASS** |
| `ColonistAwareness.CACombatTopologyIncident` in `Source/CombatTopologyModule.cs` | direct Scribe/nested Expose writer | `game.combat-topology` | **PASS** |
| `ColonistAwareness.CACombatTopologyReferenceArtifactState` in `Source/CombatTopologyModule.cs` | direct Scribe/nested Expose writer | `game.combat-topology` | **PASS** |
| `ColonistAwareness.CACombatTopologyReferenceCellState` in `Source/CombatTopologyModule.cs` | direct Scribe/nested Expose writer | `game.combat-topology` | **PASS** |
| `ColonistAwareness.CACombatTopologyReferenceChange` in `Source/CombatTopologyModule.cs` | direct Scribe/nested Expose writer | `game.combat-topology` | **PASS** |
| `ColonistAwareness.CACombatTopologyThingState` in `Source/CombatTopologyModule.cs` | direct Scribe/nested Expose writer | `game.combat-topology` | **PASS** |
| `ColonistAwareness.CACultureLegacyEvidence` in `Source/CulturalCognitionKernel.cs` | direct Scribe/nested Expose writer | `model.culture` | **PASS** |
| `ColonistAwareness.CACultureQuestionDistribution` in `Source/CulturalCognitionKernel.cs` | direct Scribe/nested Expose writer | `model.culture` | **PASS** |
| `ColonistAwareness.CACultureSubgroupDistribution` in `Source/CulturalCognitionKernel.cs` | direct Scribe/nested Expose writer | `model.culture` | **PASS** |
| `ColonistAwareness.CACulturalCognitionWorldComponent` in `Source/CulturalCognitionStateModule.cs` | direct Scribe/nested Expose writer | `world.cultural-cognition` | **PASS** |
| `ColonistAwareness.CAInfluenceExposureRecord` in `Source/CulturalCognitionStateModule.cs` | direct Scribe/nested Expose writer | `world.cultural-cognition` | **PASS** |
| `ColonistAwareness.CAPawnCulturalAttitude` in `Source/CulturalCognitionStateModule.cs` | direct Scribe/nested Expose writer | `world.cultural-cognition` | **PASS** |
| `ColonistAwareness.CAPsychologicalDynamicState` in `Source/CulturalCognitionStateModule.cs` | direct Scribe/nested Expose writer | `world.cultural-cognition` | **PASS** |
| `ColonistAwareness.CAPsychologicalProfile` in `Source/CulturalCognitionStateModule.cs` | direct Scribe/nested Expose writer | `world.cultural-cognition` | **PASS** |
| `ColonistAwareness.CAPsychologyConstructUncertainty` in `Source/CulturalCognitionStateModule.cs` | direct Scribe/nested Expose writer | `world.cultural-cognition` | **PASS** |
| `ColonistAwareness.CAPsychologyEvidenceRecord` in `Source/CulturalCognitionStateModule.cs` | direct Scribe/nested Expose writer | `world.cultural-cognition` | **PASS** |
| `ColonistAwareness.CASocialInfluenceEdge` in `Source/CulturalCognitionStateModule.cs` | direct Scribe/nested Expose writer | `world.cultural-cognition` | **PASS** |
| `ColonistAwareness.CAPawnPoliticalAttitude` in `Source/CulturalPoliticsStateModule.cs` | direct Scribe/nested Expose writer | `world.political-cognition` | **PASS** |
| `ColonistAwareness.CAPoliticalCoalitionRecord` in `Source/CulturalPoliticsStateModule.cs` | direct Scribe/nested Expose writer | `world.political-cognition` | **PASS** |
| `ColonistAwareness.CAPoliticalCognitionWorldComponent` in `Source/CulturalPoliticsStateModule.cs` | direct Scribe/nested Expose writer | `world.political-cognition` | **PASS** |
| `ColonistAwareness.CAPoliticalIssueLink` in `Source/CulturalPoliticsStateModule.cs` | direct Scribe/nested Expose writer | `world.political-cognition` | **PASS** |
| `ColonistAwareness.CAPoliticalOptionSupport` in `Source/CulturalPoliticsStateModule.cs` | direct Scribe/nested Expose writer | `world.political-cognition` | **PASS** |
| `ColonistAwareness.CACultureLongitudinalMapComponent` in `Source/CultureLongitudinalModule.cs` | direct Scribe/nested Expose writer | `map.culture-longitudinal` | **PASS** |
| `ColonistAwareness.CANativeCultureEventRecord` in `Source/CultureNativePracticeModule.cs` | direct Scribe/nested Expose writer | `map.culture-longitudinal` | **PASS** |
| `ColonistAwareness.CAAgentDebugBridge` in `Source/DevTestModule.cs` | direct Scribe/nested Expose writer | Excluded: developer-only diagnostic state with no campaign causal authority | **PASS** |
| `ColonistAwareness.CADomesticMembership` in `Source/DomesticUnitModule.cs` | direct Scribe/nested Expose writer | `model.domestic-unit` | **PASS** |
| `ColonistAwareness.CADomesticMembershipTransition` in `Source/DomesticUnitModule.cs` | direct Scribe/nested Expose writer | `model.domestic-unit` | **PASS** |
| `ColonistAwareness.CADomesticProvisionDemand` in `Source/DomesticUnitModule.cs` | direct Scribe/nested Expose writer | `model.domestic-provision-demand` | **PASS** |
| `ColonistAwareness.CADomesticUnit` in `Source/DomesticUnitModule.cs` | direct Scribe/nested Expose writer | `model.domestic-unit` | **PASS** |
| `ColonistAwareness.JobDriver_DragTo` in `Source/DragModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.EquipTransitionMapComponent` in `Source/EquipTransitionModule.cs` | direct Scribe/nested Expose writer | `map.equipment-transition` | **PASS** |
| `ColonistAwareness.CAAxisEntry` in `Source/FactionCompositionModule.cs` | direct Scribe/nested Expose writer | `model.political-order`, `model.represented-institutions` | **PASS** |
| `ColonistAwareness.CACulturalMeaning` in `Source/FactionCultureBeliefsModule.cs` | direct Scribe/nested Expose writer | `model.culture` | **PASS** |
| `ColonistAwareness.CACulture` in `Source/FactionCultureBeliefsModule.cs` | direct Scribe/nested Expose writer | `model.culture` | **PASS** |
| `ColonistAwareness.CACultureConstituent` in `Source/FactionCultureBeliefsModule.cs` | direct Scribe/nested Expose writer | `model.culture` | **PASS** |
| `ColonistAwareness.CACultureEvidenceSnapshot` in `Source/FactionCultureBeliefsModule.cs` | direct Scribe/nested Expose writer | `model.culture` | **PASS** |
| `ColonistAwareness.CACultureObservation` in `Source/FactionCultureBeliefsModule.cs` | direct Scribe/nested Expose writer | `model.culture` | **PASS** |
| `ColonistAwareness.CACulturePractice` in `Source/FactionCultureBeliefsModule.cs` | direct Scribe/nested Expose writer | `model.culture` | **PASS** |
| `ColonistAwareness.CACultureTransition` in `Source/FactionCultureBeliefsModule.cs` | direct Scribe/nested Expose writer | `model.culture` | **PASS** |
| `ColonistAwareness.CAPoliticalBeliefs` in `Source/FactionCultureBeliefsModule.cs` | direct Scribe/nested Expose writer | `model.political-order` | **PASS** |
| `ColonistAwareness.CAPoliticalDerivationReceipt` in `Source/FactionCultureBeliefsModule.cs` | direct Scribe/nested Expose writer | `model.political-order` | **PASS** |
| `ColonistAwareness.CAFactionState` in `Source/FactionStateModule.cs` | direct Scribe/nested Expose writer | `world.faction-state` | **PASS** |
| `ColonistAwareness.CAFactionStateWorldComponent` in `Source/FactionStateModule.cs` | direct Scribe/nested Expose writer | `world.faction-state` | **PASS** |
| `ColonistAwareness.CAFoundingArrangement` in `Source/FoundingArrangementModule.cs` | direct Scribe/nested Expose writer | `model.founding-arrangement` | **PASS** |
| `ColonistAwareness.CAFrontierMapPlan` in `Source/FrontierModule.cs` | direct Scribe/nested Expose writer | `model.frontier-map-plan` | **PASS** |
| `ColonistAwareness.CAGroundwaterTuning` in `Source/GroundwaterModule.cs` | direct Scribe/nested Expose writer | `model.groundwater-tuning` | **PASS** |
| `ColonistAwareness.CompWell` in `Source/GroundwaterModule.cs` | direct Scribe/nested Expose writer | `thing.water-state` | **PASS** |
| `ColonistAwareness.JobDriver_HideCarried` in `Source/HideCarriedJob.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.CAResidentRosterIntent` in `Source/HomeIntentModule.cs` | direct Scribe/nested Expose writer | `map.home-space-program` | **PASS** |
| `ColonistAwareness.CASpaceProgram` in `Source/HomeIntentModule.cs` | direct Scribe/nested Expose writer | `map.home-space-program` | **PASS** |
| `ColonistAwareness.PlannedUseMapComponent` in `Source/HomeIntentModule.cs` | direct Scribe/nested Expose writer | `map.home-space-program` | **PASS** |
| `ColonistAwareness.CAHomeMaterialDemand` in `Source/HomePrerequisiteModule.cs` | direct Scribe/nested Expose writer | `map.home-prerequisite` | **PASS** |
| `ColonistAwareness.CAHomeMaterialRequirement` in `Source/HomePrerequisiteModule.cs` | direct Scribe/nested Expose writer | `map.home-prerequisite` | **PASS** |
| `ColonistAwareness.CAHomePrerequisiteMapComponent` in `Source/HomePrerequisiteModule.cs` | direct Scribe/nested Expose writer | `map.home-prerequisite` | **PASS** |
| `ColonistAwareness.CANeed_Bladder` in `Source/HygieneNeedsModule.cs` | direct Scribe/nested Expose writer | `pawn.hygiene-needs` | **PASS** |
| `ColonistAwareness.CANeed_Body` in `Source/HygieneNeedsModule.cs` | direct Scribe/nested Expose writer | `pawn.hygiene-needs` | **PASS** |
| `ColonistAwareness.CANeed_Hygiene` in `Source/HygieneNeedsModule.cs` | native persisted owner (`CANeed_Body`) | `pawn.hygiene-needs` | **PASS** |
| `ColonistAwareness.CANeed_Thirst` in `Source/HygieneNeedsModule.cs` | native persisted owner (`CANeed_Body`) | `pawn.hygiene-needs` | **PASS** |
| `ColonistAwareness.CACombatRecoveryMapComponent` in `Source/ImmediateCombatModule.cs` | direct Scribe/nested Expose writer | `map.immediate-combat-recovery` | **PASS** |
| `ColonistAwareness.CADraftedCombatInitiativeMapComponent` in `Source/ImmediateCombatModule.cs` | direct Scribe/nested Expose writer | `map.drafted-combat-initiative` | **PASS** |
| `ColonistAwareness.CAReportedSupportEvaluation` in `Source/ImmediateCombatModule.cs` | direct Scribe/nested Expose writer | `map.drafted-combat-initiative` | **PASS** |
| `ColonistAwareness.JobDriver_CACombatMove` in `Source/ImmediateCombatModule.cs` | direct Scribe/nested Expose writer | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.JobDriver_CACombatRecovery` in `Source/ImmediateCombatModule.cs` | native persisted owner (`JobDriver_Wait`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.CAInventoryStorageProjection` in `Source/InventoryStorageModule.cs` | direct Scribe/nested Expose writer | `map.inventory-storage` | **PASS** |
| `ColonistAwareness.CAStorageProgramMapComponent` in `Source/InventoryStorageModule.cs` | direct Scribe/nested Expose writer | `map.inventory-storage`, `map.storage-program` | **PASS** |
| `ColonistAwareness.JobDriver_CAAssessCasualty` in `Source/MissionTriageModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.JobDriver_CAEmergencySelfTend` in `Source/MissionTriageModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.MissionCasualtyFact` in `Source/MissionTriageModule.cs` | direct Scribe/nested Expose writer | `map.mission-triage` | **PASS** |
| `ColonistAwareness.MissionCasualtyKnowledgeMapComponent` in `Source/MissionTriageModule.cs` | direct Scribe/nested Expose writer | `map.mission-triage` | **PASS** |
| `ColonistAwareness.AwarenessSettings` in `Source/ModEntry.cs` | direct Scribe/nested Expose writer | Excluded: global mod settings or user preset outside a realized campaign save | **PASS** |
| `ColonistAwareness.JobDriver_EquipOffhand` in `Source/OffhandModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.OffhandComponent` in `Source/OffhandModule.cs` | direct Scribe/nested Expose writer | `game.offhand` | **PASS** |
| `ColonistAwareness.OperationalAccessComponent` in `Source/OperationalAccessModule.cs` | direct Scribe/nested Expose writer | `game.operational-access` | **PASS** |
| `ColonistAwareness.CAAgreement` in `Source/OrganizationModule.cs` | direct Scribe/nested Expose writer | `world.organization` | **PASS** |
| `ColonistAwareness.CAAgreementOffer` in `Source/OrganizationModule.cs` | direct Scribe/nested Expose writer | `world.organization` | **PASS** |
| `ColonistAwareness.CABreachCase` in `Source/OrganizationModule.cs` | direct Scribe/nested Expose writer | `world.organization` | **PASS** |
| `ColonistAwareness.CAClaim` in `Source/OrganizationModule.cs` | direct Scribe/nested Expose writer | `world.organization` | **PASS** |
| `ColonistAwareness.CADecisionEntry` in `Source/OrganizationModule.cs` | direct Scribe/nested Expose writer | `world.organization` | **PASS** |
| `ColonistAwareness.CAHostileActRecord` in `Source/OrganizationModule.cs` | direct Scribe/nested Expose writer | `world.organization` | **PASS** |
| `ColonistAwareness.CAInstitutionLegitimacyAppraisal` in `Source/OrganizationModule.cs` | direct Scribe/nested Expose writer | `world.organization` | **PASS** |
| `ColonistAwareness.CAInstitutionSanctionAppraisal` in `Source/OrganizationModule.cs` | direct Scribe/nested Expose writer | `world.organization` | **PASS** |
| `ColonistAwareness.CAOffice` in `Source/OrganizationModule.cs` | direct Scribe/nested Expose writer | `world.organization` | **PASS** |
| `ColonistAwareness.CAOrganization` in `Source/OrganizationModule.cs` | direct Scribe/nested Expose writer | `world.organization` | **PASS** |
| `ColonistAwareness.CAOrganizationCustom` in `Source/OrganizationModule.cs` | direct Scribe/nested Expose writer | `world.organization` | **PASS** |
| `ColonistAwareness.CAOrganizationGroup` in `Source/OrganizationModule.cs` | direct Scribe/nested Expose writer | `world.organization` | **PASS** |
| `ColonistAwareness.CAOrganizationWorldComponent` in `Source/OrganizationModule.cs` | direct Scribe/nested Expose writer | `world.organization` | **PASS** |
| `ColonistAwareness.CAPendingGathering` in `Source/OrganizationModule.cs` | direct Scribe/nested Expose writer | `world.organization` | **PASS** |
| `ColonistAwareness.CAPolicyRecord` in `Source/OrganizationModule.cs` | direct Scribe/nested Expose writer | `world.organization` | **PASS** |
| `ColonistAwareness.CASecurityPractice` in `Source/OrganizationModule.cs` | direct Scribe/nested Expose writer | `world.organization` | **PASS** |
| `ColonistAwareness.CAFacilityHolding` in `Source/OrganizationRelationModel.cs` | direct Scribe/nested Expose writer | `model.organization-holding` | **PASS** |
| `ColonistAwareness.CAOrigin` in `Source/OrganizationRelationModel.cs` | direct Scribe/nested Expose writer | `world.organization-relations` | **PASS** |
| `ColonistAwareness.CARelation` in `Source/OrganizationRelationModel.cs` | direct Scribe/nested Expose writer | `model.organization-relation` | **PASS** |
| `ColonistAwareness.CARemovedRecord` in `Source/OrganizationRelationModel.cs` | direct Scribe/nested Expose writer | `world.organization-relations` | **PASS** |
| `ColonistAwareness.CAOrganizationRelationsWorldComponent` in `Source/OrganizationRelationsModule.cs` | direct Scribe/nested Expose writer | `world.organization-relations` | **PASS** |
| `ColonistAwareness.CAPatrolAssignment` in `Source/PatrolSystemModule.cs` | direct Scribe/nested Expose writer | `map.patrol` | **PASS** |
| `ColonistAwareness.CAPatrolCircuit` in `Source/PatrolSystemModule.cs` | direct Scribe/nested Expose writer | `map.patrol` | **PASS** |
| `ColonistAwareness.CAPatrolSystemMapComponent` in `Source/PatrolSystemModule.cs` | direct Scribe/nested Expose writer | `map.patrol` | **PASS** |
| `ColonistAwareness.CAContingencyPlan` in `Source/PlanModule.cs` | direct Scribe/nested Expose writer | `map.contingency-plan` | **PASS** |
| `ColonistAwareness.CAPlanMapComponent` in `Source/PlanModule.cs` | direct Scribe/nested Expose writer | `map.contingency-plan` | **PASS** |
| `ColonistAwareness.CAPlayerFoundingPlan` in `Source/PlayerFoundingStateModule.cs` | direct Scribe/nested Expose writer | `model.player-founding-plan` | **PASS** |
| `ColonistAwareness.CAPlayerFoundingWorldComponent` in `Source/PlayerFoundingStateModule.cs` | direct Scribe/nested Expose writer | `world.player-founding` | **PASS** |
| `ColonistAwareness.ScenPart_CAEstablishedPlayerSettlement` in `Source/PlayerFoundingStateModule.cs` | direct Scribe/nested Expose writer | `scenario.established-player-settlement` | **PASS** |
| `ColonistAwareness.CAPoliticalOptionShare` in `Source/PoliticalOrderAuthoringModule.cs` | direct Scribe/nested Expose writer | `model.political-order` | **PASS** |
| `ColonistAwareness.CAPoliticalQuestionState` in `Source/PoliticalOrderAuthoringModule.cs` | direct Scribe/nested Expose writer | `model.political-order` | **PASS** |
| `ColonistAwareness.CAKnowledgePropositionRecord` in `Source/PropositionKnowledgeModule.cs` | direct Scribe/nested Expose writer | `world.proposition-knowledge` | **PASS** |
| `ColonistAwareness.CAPropositionKnowledgeWorldComponent` in `Source/PropositionKnowledgeModule.cs` | direct Scribe/nested Expose writer | `world.proposition-knowledge` | **PASS** |
| `ColonistAwareness.CAResearchProgramReceipt` in `Source/PropositionKnowledgeModule.cs` | direct Scribe/nested Expose writer | `world.proposition-knowledge` | **PASS** |
| `ColonistAwareness.JobDriver_CAShelter` in `Source/RaidResponseModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.RaidResponseMapComponent` in `Source/RaidResponseModule.cs` | direct Scribe/nested Expose writer | `map.raid-response` | **PASS** |
| `ColonistAwareness.CompRainCatch` in `Source/RainCatchModule.cs` | direct Scribe/nested Expose writer | `thing.water-state` | **PASS** |
| `ColonistAwareness.JobDriver_CADrawFromCistern` in `Source/RainCatchModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.WorldObject_CARegionalSettlement` in `Source/RegionalSettlementModelModule.cs` | direct Scribe/nested Expose writer | `world.regional` | **PASS** |
| `ColonistAwareness.CAFrontierHoldingPlan` in `Source/RegionalSetupModule.cs` | direct Scribe/nested Expose writer | `model.regional-plan` | **PASS** |
| `ColonistAwareness.CARegionalFactionPlan` in `Source/RegionalSetupModule.cs` | direct Scribe/nested Expose writer | `model.regional-plan` | **PASS** |
| `ColonistAwareness.CARegionalPlan` in `Source/RegionalSetupModule.cs` | direct Scribe/nested Expose writer | `model.regional-plan` | **PASS** |
| `ColonistAwareness.CARegionalRelationPlan` in `Source/RegionalSetupModule.cs` | direct Scribe/nested Expose writer | `model.regional-plan` | **PASS** |
| `ColonistAwareness.CARegionalSettlementPlan` in `Source/RegionalSetupModule.cs` | direct Scribe/nested Expose writer | `model.regional-plan` | **PASS** |
| `ColonistAwareness.CARegionalSetupSession` in `Source/RegionalSetupModule.cs` | direct Scribe/nested Expose writer | `model.regional-plan` | **PASS** |
| `ColonistAwareness.CARegionalWorldPolicy` in `Source/RegionalSetupModule.cs` | direct Scribe/nested Expose writer | `world.regional` | **PASS** |
| `ColonistAwareness.CARegionalSettlementRecord` in `Source/RegionalWorldModule.cs` | direct Scribe/nested Expose writer | `model.regional-settlement-record` | **PASS** |
| `ColonistAwareness.CARegionalWorldComponent` in `Source/RegionalWorldModule.cs` | direct Scribe/nested Expose writer | `world.regional` | **PASS** |
| `ColonistAwareness.CAStartingStockRecord` in `Source/RegionalWorldModule.cs` | direct Scribe/nested Expose writer | `model.starting-stock` | **PASS** |
| `ColonistAwareness.LordJob_CARegionalSettlement` in `Source/RegionalWorldModule.cs` | direct Scribe/nested Expose writer | `native.regional-settlement-lord` | **PASS** |
| `ColonistAwareness.WorldObject_CARegionalMemberReservation` in `Source/RegionalWorldModule.cs` | direct Scribe/nested Expose writer | `world.regional-reservation` | **PASS** |
| `ColonistAwareness.CARoadExpansionMapComponent` in `Source/RoadExpansionModule.cs` | direct Scribe/nested Expose writer | `map.road-expansion` | **PASS** |
| `ColonistAwareness.CARoadProject` in `Source/RoadExpansionModule.cs` | direct Scribe/nested Expose writer | `map.road-expansion` | **PASS** |
| `ColonistAwareness.CASettlementCapabilityAssessment` in `Source/SettlementCapabilityModule.cs` | direct Scribe/nested Expose writer | `model.settlement-capability` | **PASS** |
| `ColonistAwareness.CAProvisionArrangement` in `Source/SettlementCompositionModule.cs` | direct Scribe/nested Expose writer | `model.settlement-provision` | **PASS** |
| `ColonistAwareness.CASettlementPopulationGroup` in `Source/SettlementCompositionModule.cs` | direct Scribe/nested Expose writer | `model.settlement-population-group` | **PASS** |
| `ColonistAwareness.CASettlementLayout` in `Source/SettlementLayoutModule.cs` | direct Scribe/nested Expose writer | `model.settlement-layout` | **PASS** |
| `ColonistAwareness.CASettlementIdeoligionEvidence` in `Source/SettlementPlanningContextModule.cs` | direct Scribe/nested Expose writer | `map.settlement-planning-context` | **PASS** |
| `ColonistAwareness.CASettlementPlanningContextMapComponent` in `Source/SettlementPlanningContextModule.cs` | direct Scribe/nested Expose writer | `map.settlement-planning-context` | **PASS** |
| `ColonistAwareness.CASettlementProgramAssetReceipt` in `Source/SettlementProgramAssetModule.cs` | direct Scribe/nested Expose writer | `model.settlement-program-asset` | **PASS** |
| `ColonistAwareness.CASettlementRebuildWork` in `Source/SettlementProgramMaterializerModule.cs` | direct Scribe/nested Expose writer | `model.settlement-rebuild-work` | **PASS** |
| `ColonistAwareness.CASettlementRepairWork` in `Source/SettlementProgramMaterializerModule.cs` | direct Scribe/nested Expose writer | `model.settlement-repair-work` | **PASS** |
| `ColonistAwareness.CASettlementResearchWork` in `Source/SettlementProgramMaterializerModule.cs` | direct Scribe/nested Expose writer | `model.settlement-research-work` | **PASS** |
| `ColonistAwareness.CASettlementWorksMapComponent` in `Source/SettlementProgramMaterializerModule.cs` | direct Scribe/nested Expose writer | `map.settlement-work` | **PASS** |
| `ColonistAwareness.CASettlementOperationalFact` in `Source/SettlementProgramModule.cs` | direct Scribe/nested Expose writer | `model.settlement-operational-fact` | **PASS** |
| `ColonistAwareness.CASettlementProgram` in `Source/SettlementProgramModule.cs` | direct Scribe/nested Expose writer | `model.settlement-program` | **PASS** |
| `ColonistAwareness.CASettlementProgramEntry` in `Source/SettlementProgramModule.cs` | direct Scribe/nested Expose writer | `model.settlement-program-entry` | **PASS** |
| `ColonistAwareness.CASettlementResidenceAssignment` in `Source/SettlementResidenceModule.cs` | direct Scribe/nested Expose writer | `model.settlement-residence` | **PASS** |
| `ColonistAwareness.CASocialReactionRecord` in `Source/SocialInterpretationRuntimeModule.cs` | direct Scribe/nested Expose writer | `world.social-reactions` | **PASS** |
| `ColonistAwareness.CASocialReactionWorldComponent` in `Source/SocialInterpretationRuntimeModule.cs` | direct Scribe/nested Expose writer | `world.social-reactions` | **PASS** |
| `ColonistAwareness.CASpatialBuiltRoomRecord` in `Source/SpatialInitiativeModule.cs` | direct Scribe/nested Expose writer | `map.spatial-initiative` | **PASS** |
| `ColonistAwareness.CASpatialBuiltStorageRecord` in `Source/SpatialInitiativeModule.cs` | direct Scribe/nested Expose writer | `map.spatial-initiative` | **PASS** |
| `ColonistAwareness.CASpatialInitiativeMapComponent` in `Source/SpatialInitiativeModule.cs` | direct Scribe/nested Expose writer | `map.spatial-initiative` | **PASS** |
| `ColonistAwareness.CASpatialInitiativeRecord` in `Source/SpatialInitiativeModule.cs` | direct Scribe/nested Expose writer | `map.spatial-initiative` | **PASS** |
| `ColonistAwareness.SquadComponent` in `Source/SquadModule.cs` | direct Scribe/nested Expose writer | `game.squad` | **PASS** |
| `ColonistAwareness.SquadSupportMapComponent` in `Source/SquadSupportModule.cs` | direct Scribe/nested Expose writer | `map.squad-support` | **PASS** |
| `ColonistAwareness.SupportTasking` in `Source/SquadSupportModule.cs` | direct Scribe/nested Expose writer | `map.squad-support` | **PASS** |
| `ColonistAwareness.JobDriver_CABoundedMeleeDefense` in `Source/StackJobs.cs` | native persisted owner (`JobDriver_AttackMelee`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.JobDriver_CABoundedRangedDefense` in `Source/StackJobs.cs` | native persisted owner (`JobDriver_AttackStatic`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.JobDriver_CAStackPosture` in `Source/StackJobs.cs` | direct Scribe/nested Expose writer | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.LordJob_CAStackBreach` in `Source/StackLord.cs` | direct Scribe/nested Expose writer | `native.stack-lord` | **PASS** |
| `ColonistAwareness.LordToilData_CAClear` in `Source/StackLord.cs` | direct Scribe/nested Expose writer | `native.stack-lord` | **PASS** |
| `ColonistAwareness.CAStorageProgramMapComponent` in `Source/StorageProgramModule.cs` | direct Scribe/nested Expose writer | `map.inventory-storage`, `map.storage-program` | **PASS** |
| `ColonistAwareness.CAStorageProjection` in `Source/StorageProgramModule.cs` | direct Scribe/nested Expose writer | `map.storage-program` | **PASS** |
| `ColonistAwareness.HiddenThingsComponent` in `Source/SurvivalModule.cs` | direct Scribe/nested Expose writer | `game.hidden-things` | **PASS** |
| `ColonistAwareness.SustenanceComponent` in `Source/SustenanceModule.cs` | direct Scribe/nested Expose writer | `game.sustenance` | **PASS** |
| `ColonistAwareness.LordJob_CATactical` in `Source/TacticalLord.cs` | direct Scribe/nested Expose writer | `native.tactical-lord` | **PASS** |
| `ColonistAwareness.CAOverlayMapComponent` in `Source/TacticalOverlayModule.cs` | direct Scribe/nested Expose writer | `map.tactical-overlay` | **PASS** |
| `ColonistAwareness.CATaskForce` in `Source/TaskForceModule.cs` | direct Scribe/nested Expose writer | `map.task-force` | **PASS** |
| `ColonistAwareness.CATaskForceMapComponent` in `Source/TaskForceModule.cs` | direct Scribe/nested Expose writer | `map.task-force` | **PASS** |
| `ColonistAwareness.CATechnologicalKnowledge` in `Source/TechnologicalKnowledgeModule.cs` | direct Scribe/nested Expose writer | `model.technological-knowledge` | **PASS** |
| `ColonistAwareness.CATechnologyAvailabilityReceipt` in `Source/TechnologicalKnowledgeModule.cs` | direct Scribe/nested Expose writer | `model.technological-knowledge` | **PASS** |
| `ColonistAwareness.CATechnologyCustodyRecord` in `Source/TechnologicalKnowledgeModule.cs` | direct Scribe/nested Expose writer | `model.technological-knowledge` | **PASS** |
| `ColonistAwareness.CATechnologyDomainKnowledge` in `Source/TechnologicalKnowledgeModule.cs` | direct Scribe/nested Expose writer | `model.technological-knowledge` | **PASS** |
| `ColonistAwareness.CAToxicWasteAuthorizedSourceRecord` in `Source/ToxicWasteLifecycleModule.cs` | direct Scribe/nested Expose writer | `map.toxic-waste` | **PASS** |
| `ColonistAwareness.CAToxicWasteCapacityRecord` in `Source/ToxicWasteLifecycleModule.cs` | direct Scribe/nested Expose writer | `map.toxic-waste` | **PASS** |
| `ColonistAwareness.CAToxicWasteConsequenceRecord` in `Source/ToxicWasteLifecycleModule.cs` | direct Scribe/nested Expose writer | `map.toxic-waste` | **PASS** |
| `ColonistAwareness.CAToxicWasteDestinationRecord` in `Source/ToxicWasteLifecycleModule.cs` | direct Scribe/nested Expose writer | `map.toxic-waste` | **PASS** |
| `ColonistAwareness.CAToxicWasteFreezingRecord` in `Source/ToxicWasteLifecycleModule.cs` | direct Scribe/nested Expose writer | `map.toxic-waste` | **PASS** |
| `ColonistAwareness.CAToxicWasteInventoryLotRecord` in `Source/ToxicWasteLifecycleModule.cs` | direct Scribe/nested Expose writer | `map.toxic-waste` | **PASS** |
| `ColonistAwareness.CAToxicWasteInventoryRecord` in `Source/ToxicWasteLifecycleModule.cs` | direct Scribe/nested Expose writer | `map.toxic-waste` | **PASS** |
| `ColonistAwareness.CAToxicWasteLifecycleMapComponent` in `Source/ToxicWasteLifecycleModule.cs` | direct Scribe/nested Expose writer | `map.toxic-waste` | **PASS** |
| `ColonistAwareness.CAToxicWasteLifecycleObjective` in `Source/ToxicWasteLifecycleModule.cs` | direct Scribe/nested Expose writer | `map.toxic-waste` | **PASS** |
| `ColonistAwareness.CAToxicWasteNativeResponseRecord` in `Source/ToxicWasteLifecycleModule.cs` | direct Scribe/nested Expose writer | `map.toxic-waste` | **PASS** |
| `ColonistAwareness.CAToxicWasteRelocationRecord` in `Source/ToxicWasteLifecycleModule.cs` | direct Scribe/nested Expose writer | `map.toxic-waste` | **PASS** |
| `ColonistAwareness.CAToxicWasteStagingCandidateRecord` in `Source/ToxicWasteLifecycleModule.cs` | direct Scribe/nested Expose writer | `map.toxic-waste` | **PASS** |
| `ColonistAwareness.CAToxicWasteTransportRecord` in `Source/ToxicWasteLifecycleModule.cs` | direct Scribe/nested Expose writer | `map.toxic-waste` | **PASS** |
| `ColonistAwareness.JobDriver_PracticeDrill` in `Source/TrainingModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.CACreditTerms` in `Source/TransactionLedgerModule.cs` | direct Scribe/nested Expose writer | `world.transaction-ledger` | **PASS** |
| `ColonistAwareness.CADebt` in `Source/TransactionLedgerModule.cs` | direct Scribe/nested Expose writer | `world.transaction-ledger` | **PASS** |
| `ColonistAwareness.CAParty` in `Source/TransactionLedgerModule.cs` | direct Scribe/nested Expose writer | `world.transaction-ledger` | **PASS** |
| `ColonistAwareness.CATransaction` in `Source/TransactionLedgerModule.cs` | direct Scribe/nested Expose writer | `world.transaction-ledger` | **PASS** |
| `ColonistAwareness.CATransactionLedger` in `Source/TransactionLedgerModule.cs` | direct Scribe/nested Expose writer | `world.transaction-ledger` | **PASS** |
| `ColonistAwareness.TrapMemoryMapComponent` in `Source/TrapAwarenessModule.cs` | direct Scribe/nested Expose writer | `map.trap-memory` | **PASS** |
| `ColonistAwareness.JobDriver_CADrinkFromSource` in `Source/WaterActsModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.JobDriver_CARelieveOutdoors` in `Source/WaterActsModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.JobDriver_CAWashAtWater` in `Source/WaterActsModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.JobDriver_CADrawFromWell` in `Source/WaterDrawModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.JobDriver_CADrawWater` in `Source/WaterDrawModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.JobDriver_CADrinkFromVessel` in `Source/WaterDrawModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.JobDriver_CAWaterPatient` in `Source/WaterPatientModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.CompWaterVessel` in `Source/WaterVesselModule.cs` | direct Scribe/nested Expose writer | `thing.water-state` | **PASS** |
| `ColonistAwareness.CAAccountabilityRecord` in `Source/WelfareKnowledgeModule.cs` | direct Scribe/nested Expose writer | `map.welfare-knowledge` | **PASS** |
| `ColonistAwareness.JobDriver_CACheckWelfare` in `Source/WelfareKnowledgeModule.cs` | direct Scribe/nested Expose writer | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.KnowledgeMapComponent` in `Source/WelfareKnowledgeModule.cs` | direct Scribe/nested Expose writer | `map.welfare-knowledge` | **PASS** |
| `ColonistAwareness.CAWelfareSupportRequest` in `Source/WelfareSupportModule.cs` | direct Scribe/nested Expose writer | `map.welfare-support` | **PASS** |
| `ColonistAwareness.CAWelfareThresholdSupportMapComponent` in `Source/WelfareSupportModule.cs` | direct Scribe/nested Expose writer | `map.welfare-support` | **PASS** |
| `ColonistAwareness.JobDriver_CAWelfareSupportWait` in `Source/WelfareSupportModule.cs` | native persisted owner (`JobDriver_Wait`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.JobDriver_CAWithdrawal` in `Source/WithdrawalModule.cs` | native persisted owner (`JobDriver`) | `native.ca-job-drivers` | **PASS** |
| `ColonistAwareness.Plan` in `Source/WithdrawalModule.cs` | direct Scribe/nested Expose writer | `map.withdrawal` | **PASS** |
| `ColonistAwareness.WithdrawalMapComponent` in `Source/WithdrawalModule.cs` | direct Scribe/nested Expose writer | `map.withdrawal` | **PASS** |

The executable reverse check remains separate: `CACampaignPreflightValidator.ValidateCatalogCoverage` requires every catalog key to have a component, nested-record, exact-native-class, or native-prefix validation route. Together, the two checks prove source-to-catalog and catalog-to-validator closure without using either list as its own evidence.
