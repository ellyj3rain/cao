using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace ColonistAwareness
{
    public sealed class CACampaignPreflightDocument
    {
        internal CACampaignPreflightDocument(int boundaryVersion,
            int catalogVersion, int mapCount,
            int legacyAuthoringEpoch, bool containsCAState,
            Dictionary<string, int> manifest,
            List<CACampaignOwnerVersionRecord> ownerVersions,
            List<CACampaignPayloadRecord> payloads,
            List<string> unsupportedLegacyState,
            bool hasPayloadDigest, bool payloadDigestValid,
            string payloadDigest)
        {
            BoundaryVersion = boundaryVersion;
            CatalogVersion = catalogVersion;
            MapCount = mapCount;
            LegacyAuthoringEpoch = legacyAuthoringEpoch;
            ContainsCAState = containsCAState;
            Manifest = manifest;
            OwnerVersions = ownerVersions;
            Payloads = payloads;
            UnsupportedLegacyState = unsupportedLegacyState;
            HasPayloadDigest = hasPayloadDigest;
            PayloadDigestValid = payloadDigestValid;
            PayloadDigest = payloadDigest;
        }

        public int BoundaryVersion { get; }
        public int CatalogVersion { get; }
        public int MapCount { get; }
        public int LegacyAuthoringEpoch { get; }
        public bool ContainsCAState { get; }
        public IReadOnlyDictionary<string, int> Manifest { get; }
        public IReadOnlyList<CACampaignOwnerVersionRecord> OwnerVersions
        {
            get;
        }
        public IReadOnlyList<CACampaignPayloadRecord> Payloads { get; }
        public IReadOnlyList<string> UnsupportedLegacyState { get; }
        public bool HasPayloadDigest { get; }
        public bool PayloadDigestValid { get; }
        public string PayloadDigest { get; }
    }

    public sealed class CACampaignPayloadRecord
    {
        internal CACampaignPayloadRecord(string componentType,
            string container, int mapOrdinal,
            IReadOnlyCollection<string> validationFailures)
        {
            ComponentType = componentType;
            Container = container;
            MapOrdinal = mapOrdinal;
            ValidationFailures = validationFailures;
        }

        public string ComponentType { get; }
        public string Container { get; }
        public int MapOrdinal { get; }
        public IReadOnlyCollection<string> ValidationFailures { get; }
    }

    public sealed class CACampaignPayloadRuleDefinition
    {
        internal CACampaignPayloadRuleDefinition(string componentType,
            string parentPath, string[] requiredNonNullChildren,
            string[] requiredAllowNullChildren,
            string[] equalLengthChildren, bool skipWhenParentNull)
        {
            ComponentType = componentType;
            ParentPath = parentPath;
            RequiredNonNullChildren = requiredNonNullChildren;
            RequiredAllowNullChildren = requiredAllowNullChildren;
            EqualLengthChildren = equalLengthChildren;
            SkipWhenParentNull = skipWhenParentNull;
        }

        public string ComponentType { get; }
        public string ParentPath { get; }
        public IReadOnlyList<string> RequiredNonNullChildren { get; }
        public IReadOnlyList<string> RequiredAllowNullChildren { get; }
        public IReadOnlyList<string> EqualLengthChildren { get; }
        public bool SkipWhenParentNull { get; }
    }

    public enum CACampaignPayloadScope : byte
    {
        WorldOnce = 0,
        GameOnce = 1,
        PerMap = 2
    }

    public sealed class CACampaignPayloadDefinition
    {
        internal CACampaignPayloadDefinition(CACampaignPayloadScope scope,
            string componentType, string[] schemaKeys,
            string[] requiredNonNullPaths,
            string[] requiredAllowNullPaths = null)
        {
            Scope = scope;
            ComponentType = componentType;
            SchemaKeys = schemaKeys;
            RequiredNonNullPaths = requiredNonNullPaths;
            RequiredAllowNullPaths = requiredAllowNullPaths
                ?? Array.Empty<string>();
        }

        public CACampaignPayloadScope Scope { get; }
        public string ComponentType { get; }
        public IReadOnlyList<string> SchemaKeys { get; }
        public IReadOnlyList<string> RequiredNonNullPaths { get; }
        public IReadOnlyList<string> RequiredAllowNullPaths { get; }
    }

    public sealed class CACampaignOwnerVersionRecord
    {
        internal CACampaignOwnerVersionRecord(string schemaKey,
            string componentType, string xmlTag, int? version,
            int? legacyAuthoringEpoch, string container, int mapOrdinal)
        {
            SchemaKey = schemaKey;
            ComponentType = componentType;
            XmlTag = xmlTag;
            Version = version;
            LegacyAuthoringEpoch = legacyAuthoringEpoch;
            Container = container;
            MapOrdinal = mapOrdinal;
        }

        public string SchemaKey { get; }
        public string ComponentType { get; }
        public string XmlTag { get; }
        public int? Version { get; }
        public int? LegacyAuthoringEpoch { get; }
        public string Container { get; }
        public int MapOrdinal { get; }
    }

    public readonly struct CACampaignOwnerVersionDefinition
    {
        public CACampaignOwnerVersionDefinition(string schemaKey,
            string componentType, string xmlTag, bool requiredOncePerSave)
        {
            SchemaKey = schemaKey;
            ComponentType = componentType;
            XmlTag = xmlTag;
            RequiredOncePerSave = requiredOncePerSave;
        }

        public string SchemaKey { get; }
        public string ComponentType { get; }
        public string XmlTag { get; }
        public bool RequiredOncePerSave { get; }
    }

    // This is intentionally free of Verse and Harmony dependencies so the
    // exact streaming preflight used before a save is loaded can be exercised
    // by compatibility receipts.
    public static class CACampaignPreflightReader
    {
        public const string PayloadDigestPrefix =
            "<!-- CA_CAMPAIGN_PAYLOAD_SHA256:";
        public const string PayloadDigestSuffix = " -->";
        public const int MaxBufferedElementsPerRecord = 1000000;
        public const long MaxBufferedTextCharactersPerRecord = 67108864;

        private sealed class MechanismRecord
        {
            public int Depth;
            public string Axis;
            public string Option;
        }

        private sealed class PayloadElementFrame
        {
            public int Depth;
            public string Name;
            public string Path;
            public bool IsNull;
            public readonly StringBuilder Text = new StringBuilder();
            public readonly List<PayloadElementFrame> Children =
                new List<PayloadElementFrame>();

            public int ItemCount => Children.Count(item => item.Name == "li");
        }

        private sealed class NativePayloadRecord
        {
            public string ClassName;
            public int Depth;
            public PayloadElementFrame Root;
            public int BufferedElements;
            public long BufferedTextCharacters;
            public readonly Stack<PayloadElementFrame> Frames =
                new Stack<PayloadElementFrame>();
            public readonly List<string> Failures = new List<string>();
        }

        public sealed class NestedSchemaBinding
        {
            internal NestedSchemaBinding(string schemaKey,
                string componentType, string parentPath, bool allowNull)
            {
                SchemaKey = schemaKey;
                ComponentType = componentType;
                ParentPath = parentPath;
                AllowNull = allowNull;
            }
            public string SchemaKey { get; }
            public string ComponentType { get; }
            public string ParentPath { get; }
            public bool AllowNull { get; }
        }

        public static readonly NestedSchemaBinding[] NestedSchemaBindings =
        {
            N("model.culture", "ColonistAwareness.CAFactionStateWorldComponent",
                "CA_factionStates/li/culture"),
            N("model.culture", "ColonistAwareness.CAPlayerFoundingWorldComponent",
                "CA_playerFounding/culture"),
            N("model.culture",
                "ColonistAwareness.CACultureLongitudinalMapComponent",
                "CA_playerLocalCulture", true),
            N("model.culture", "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalPlans/li/factions/li/culture"),
            N("model.culture", "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalPlans/li/settlements/li/localCulture"),
            N("model.culture", "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalSettlements/li/culture"),
            N("model.culture", "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalPlans/li/playerFounding/culture"),
            N("model.political-beliefs",
                "ColonistAwareness.CAFactionStateWorldComponent",
                "CA_factionStates/li/politicalBeliefs"),
            N("model.political-beliefs",
                "ColonistAwareness.CAPlayerFoundingWorldComponent",
                "CA_playerFounding/politicalBeliefs"),
            N("model.political-beliefs",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalPlans/li/factions/li/politicalBeliefs"),
            N("model.political-beliefs",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalPlans/li/playerFounding/politicalBeliefs"),
            N("model.current-order",
                "ColonistAwareness.CAFactionStateWorldComponent",
                "CA_factionStates/li/factionStructure"),
            N("model.current-order", "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalPlans/li/factions/li/factionStructure"),
            N("model.founding-arrangement",
                "ColonistAwareness.CAPlayerFoundingWorldComponent",
                "CA_playerFounding/arrangement"),
            N("model.founding-arrangement",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalPlans/li/playerFounding/arrangement"),
            N("model.player-founding-plan",
                "ColonistAwareness.CAPlayerFoundingWorldComponent",
                "CA_playerFounding"),
            N("model.player-founding-plan",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalPlans/li/playerFounding"),
            N("model.regional-plan", "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalPlans/li"),
            N("model.regional-settlement-record",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalSettlements/li"),
            N("model.settlement-population-group",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalSettlements/li/populationGroups/li"),
            N("model.domestic-unit", "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalSettlements/li/domesticUnits/li"),
            N("model.domestic-provision-demand",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalSettlements/li/domesticProvisionDemands/li"),
            N("model.frontier-map-plan",
                "ColonistAwareness.CAOrganizationWorldComponent",
                "CA_frontierMapPlans/li"),
            N("model.groundwater-tuning",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_groundwaterTuning"),
            N("model.settlement-residence",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalSettlements/li/residenceAssignments/li"),
            N("model.settlement-capability",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalSettlements/li/capabilities/li"),
            N("model.settlement-program",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalSettlements/li/settlementProgram"),
            N("model.settlement-program-entry",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalSettlements/li/settlementProgram/entries/li"),
            N("model.settlement-operational-fact",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalSettlements/li/operationalFacts/li"),
            N("model.settlement-program-asset",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalSettlements/li/programAssets/li"),
            N("model.settlement-provision",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalSettlements/li/provisionArrangements/li"),
            N("model.starting-stock", "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalSettlements/li/startingStock/li"),
            N("model.organization-relation",
                "ColonistAwareness.CAOrganizationRelationsWorldComponent",
                "CA_relations/li"),
            N("model.organization-holding",
                "ColonistAwareness.CAOrganizationRelationsWorldComponent",
                "CA_holdings/li"),
            N("model.settlement-layout",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalSettlements/li/layout", true),
            N("model.settlement-research-work",
                "ColonistAwareness.CASettlementWorksMapComponent",
                "CA_settlementResearchWork/li"),
            N("model.settlement-repair-work",
                "ColonistAwareness.CASettlementWorksMapComponent",
                "CA_settlementRepairWork/li"),
            N("model.settlement-rebuild-work",
                "ColonistAwareness.CASettlementWorksMapComponent",
                "CA_settlementRebuildWork/li")
        };

        public static readonly IReadOnlyDictionary<string, string>
            NativeSchemaByClass = new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                ["ColonistAwareness.JobDriver_CASearchKnownContact"] =
                    "map.assault-awareness",
                ["ColonistAwareness.LordJob_CATactical"] =
                    "native.tactical-lord",
                ["ColonistAwareness.LordJob_CAStackBreach"] =
                    "native.stack-lord",
                ["ColonistAwareness.LordJob_CARegionalSettlement"] =
                    "native.regional-settlement-lord",
                ["ColonistAwareness.Thought_CAPoliticalBelief"] =
                    "pawn.political-belief-memory",
                ["ColonistAwareness.CANeed_Thirst"] = "pawn.hygiene-needs",
                ["ColonistAwareness.CANeed_Hygiene"] = "pawn.hygiene-needs",
                ["ColonistAwareness.CANeed_Bladder"] = "pawn.hygiene-needs",
                ["ColonistAwareness.CompWell"] = "thing.water-state",
                ["ColonistAwareness.CompRainCatch"] = "thing.water-state",
                ["ColonistAwareness.CompWaterVessel"] = "thing.water-state",
                ["ColonistAwareness.ScenPart_CAEstablishedPlayerSettlement"] =
                    "scenario.established-player-settlement",
                ["ColonistAwareness.WorldObject_CARegionalMemberReservation"] =
                    "world.regional-reservation",
                ["ColonistAwareness.WorldObject_CARegionalSettlement"] =
                    "world.regional",
                ["Camping_Stuff.NCS_Tent"] = "embedded.camping-state",
                ["Camping_Stuff.TentSpawnedComp"] = "embedded.camping-state",
                ["Camping_Stuff.CompTentPartWithCellsDamage"] =
                    "embedded.camping-state",
                ["Camping_Stuff.TentSpec"] = "embedded.camping-state",
                ["Camping_Stuff.SketchRoof"] = "embedded.camping-state"
            };

        public static readonly KeyValuePair<string, string>[]
            NativeSchemaPrefixes =
            {
                new KeyValuePair<string, string>(
                    "ColonistAwareness.JobDriver_", "native.ca-job-drivers"),
                new KeyValuePair<string, string>(
                    "Camping_Stuff.JobDriver_", "embedded.camping-state"),
                new KeyValuePair<string, string>(
                    "Camping_Stuff.NCS_", "embedded.camping-state"),
                new KeyValuePair<string, string>(
                    "Camping_Stuff.CompTent", "embedded.camping-state")
            };

        // These records live inside native RimWorld owners rather than a CAO
        // component container. They are optional, but every occurrence is a
        // durable CAO payload and must validate if it is present.
        private static readonly Dictionary<string, string[]> NativeRequired =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["ColonistAwareness.LordJob_CATactical"] = K(
                    "orderPawns", "orderKinds", "orderCells", "orderWatches",
                    "orderEpisodes", "orderOrigins", "orderControllers",
                    "orderIssuers", "orderBehaviorKeys",
                    "orderAuthorityOrigins", "orderAuthorityIdentities",
                    "orderOwnershipScopes", "orderOwnerIds",
                    "orderCreatedTicks", "orderCreationTiers", "orderTargets",
                    "orderTerminationConditions", "orderFireSuppressed",
                    "orderEnvelopes", "orderGrits", "orderProfilePinned"),
                ["ColonistAwareness.LordJob_CAStackBreach"] = K(
                    "door", "slotPawns", "slotCells", "clearPawns",
                    "clearCells"),
                ["ColonistAwareness.LordJob_CARegionalSettlement"] = K(
                    "faction", "settlementCenter"),
                ["ColonistAwareness.JobDriver_CASearchKnownContact"] = K(
                    "hostileId", "sourceTick"),
                ["ColonistAwareness.Thought_CAPoliticalBelief"] = K(
                    "CA_politicalBelief", "CA_eventIdentity")
            };

        private static readonly Dictionary<string, string[]> NativeParallel =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["ColonistAwareness.LordJob_CATactical"] = NativeRequired[
                    "ColonistAwareness.LordJob_CATactical"],
                ["ColonistAwareness.LordJob_CAStackBreach"] = K(
                    "slotPawns", "slotCells"),
                ["ColonistAwareness.LordJob_CAStackBreach#clear"] = K(
                    "clearPawns", "clearCells")
            };

        public static readonly CACampaignOwnerVersionDefinition[]
            OwnerVersionDefinitions =
        {
            O("map.culture-longitudinal",
                "ColonistAwareness.CACultureLongitudinalMapComponent",
                "CA_cultureHistorySchemaVersion", false),
            O("world.faction-state",
                "ColonistAwareness.CAFactionStateWorldComponent",
                "CA_factionStateSchemaVersion", true),
            O("world.player-founding",
                "ColonistAwareness.CAPlayerFoundingWorldComponent",
                "CA_playerFoundingSchemaVersion", true),
            O("world.organization-relations",
                "ColonistAwareness.CAOrganizationRelationsWorldComponent",
                "CA_organizationRelationsSchemaVersion", true),
            O("world.organization",
                "ColonistAwareness.CAOrganizationWorldComponent",
                "CA_organizationSchemaVersion", true),
            O("world.regional",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalSchemaVersion", true),
            O("world.social-reactions",
                "ColonistAwareness.CASocialReactionWorldComponent",
                "CA_socialReactionSchemaVersion", true)
        };

        // Component-level durable owners not already covered by an inline
        // owner version. Required paths are direct children of the exact
        // component record. Empty collections are valid; IsNull=True is not.
        public static readonly CACampaignPayloadDefinition[]
            PayloadDefinitions =
        {
            P(CACampaignPayloadScope.WorldOnce,
                "ColonistAwareness.CACampaignCompatibilityWorldComponent",
                K("campaign.boundary"), "CA_campaignSchemas",
                "CA_campaignMigrations"),
            P(CACampaignPayloadScope.WorldOnce,
                "ColonistAwareness.CAFactionStateWorldComponent",
                K(), "CA_factionStates"),
            P(CACampaignPayloadScope.WorldOnce,
                "ColonistAwareness.CAPlayerFoundingWorldComponent",
                K(), "CA_playerFounding", "CA_playerFoundingAppliedAtTick"),
            P(CACampaignPayloadScope.WorldOnce,
                "ColonistAwareness.CAOrganizationRelationsWorldComponent",
                K(), "CA_relations", "CA_holdings", "CA_removedRecords"),
            P(CACampaignPayloadScope.WorldOnce,
                "ColonistAwareness.CAOrganizationWorldComponent",
                K(), "CA_organizations", "CA_frontierMapPlans",
                "CA_agreements", "CA_breachCases", "CA_hostileActs",
                "CA_offers", "CA_pendingGatherings"),
            P(CACampaignPayloadScope.WorldOnce,
                "ColonistAwareness.CARegionalWorldComponent",
                K(), "CA_regionalSettlements", "CA_regionalPlans",
                "CA_regionalWorldPolicy", "CA_groundwaterTuning"),
            P(CACampaignPayloadScope.WorldOnce,
                "ColonistAwareness.CASocialReactionWorldComponent",
                K(), "CA_socialReactions"),
            PA(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CACultureLongitudinalMapComponent",
                K(), K("CA_playerLocalCulture")),
            P(CACampaignPayloadScope.WorldOnce,
                "ColonistAwareness.CAActLedger",
                K("world.act-ledger"), "CA_actRecords"),
            P(CACampaignPayloadScope.WorldOnce,
                "ColonistAwareness.CATransactionLedger",
                K("world.transaction-ledger"), "transactions", "debts"),
            P(CACampaignPayloadScope.GameOnce,
                "ColonistAwareness.AutonomyComponent",
                K("game.autonomy"), "CA_initiativeTiers"),
            P(CACampaignPayloadScope.GameOnce,
                "ColonistAwareness.CACombatSpatialLogComponent",
                K("game.combat-spatial-log", "game.combat-topology"),
                "CA_combatSpatialLogRecords", "CA_combatTopologyIncidents"),
            P(CACampaignPayloadScope.GameOnce,
                "ColonistAwareness.HiddenThingsComponent",
                K("game.hidden-things"), "CA_stashes", "CA_stashQuality"),
            P(CACampaignPayloadScope.GameOnce,
                "ColonistAwareness.OffhandComponent",
                K("game.offhand"), "CA_offhandOf"),
            P(CACampaignPayloadScope.GameOnce,
                "ColonistAwareness.OperationalAccessComponent",
                K("game.operational-access"), "CA_operationalApparelNext",
                "CA_operationalPlayerForbidden"),
            P(CACampaignPayloadScope.GameOnce,
                "ColonistAwareness.SquadComponent",
                K("game.squad"), "CA_squadOf", "CA_leaderOf",
                "CA_fireteamOf", "CA_ftLeaderOf",
                "CA_propagatedDraftLeader", "CA_propagatedDraftEpisode"),
            P(CACampaignPayloadScope.GameOnce,
                "ColonistAwareness.SustenanceComponent",
                K("game.sustenance"), "CA_weight", "CA_fullness",
                "CA_originalBody"),
            P(CACampaignPayloadScope.GameOnce,
                "Camping_Stuff.LayoutCache",
                K("embedded.camping-state"), "layoutUsage"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CAArrangementMapComponent",
                K("map.arrangements"), "caArrangements"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.AutonomousHomeMapComponent",
                K("map.autonomous-home"), "CA_homeSuppressedKinds",
                "CA_homeCompletedAutoBuildings"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CAParleyMapComponent",
                K("map.parley"), "CA_parleyMissions", "CA_envoyMissions"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CABehaviorIntentMapComponent",
                K("map.behavior-intent"), "CA_ownedJobIntents"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CAAftermathAccountabilityMapComponent",
                K("map.combat-aftermath"), "CA_executionAccountability"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.EquipTransitionMapComponent",
                K("map.equipment-transition"), "CA_stowedPrimary",
                "CA_transitionWeapon", "CA_transitionEpisodes",
                "CA_transitionAuthorityOrigins", "CA_automaticOffhand",
                "CA_automaticOffhandEpisodes", "CA_calmScans",
                "CA_offhandCalm"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.KnowledgeMapComponent",
                K("map.welfare-knowledge"), "CA_accountability"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.PlannedUseMapComponent",
                K("map.home-space-program"), "CA_spacePrograms",
                "CA_residentRosterIntents"),
            PA(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CAHomePrerequisiteMapComponent",
                K("map.home-prerequisite"), K("CA_homeMaterialDemand"),
                "CA_homeOwnedTreeSources", "CA_homePlayerVetoedTrees"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.MissionCasualtyKnowledgeMapComponent",
                K("map.mission-triage"), "CA_missionCasualtyFacts"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CAPatrolSystemMapComponent",
                K("map.patrol"), "circuits", "patrols"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.RaidResponseMapComponent",
                K("map.raid-response"), "CA_shelterCells",
                "CA_shelterEpisodes", "CA_shelterBehaviorKeys"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CAPlanMapComponent",
                K("map.contingency-plan"), "caPlans"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CARoadExpansionMapComponent",
                K("map.road-expansion"), "CA_roadProjects"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CASettlementPlanningContextMapComponent",
                K("map.settlement-planning-context"),
                "CA_settlementContextIdeoligions"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CASettlementWorksMapComponent",
                K("map.settlement-work"), "CA_settlementResearchWork",
                "CA_settlementRepairWork", "CA_settlementRebuildWork"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CASpatialInitiativeMapComponent",
                K("map.spatial-initiative"), "CA_spatialInitiatives",
                "CA_spatialSuppressedZones", "CA_spatialSuppressedPrograms",
                "CA_spatialCompletedStorage", "CA_spatialCompletedRoom",
                "CA_spatialPendingRoomFocusThingIds"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.SquadSupportMapComponent",
                K("map.squad-support"), "ownedSupportIntents"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CAStorageProgramMapComponent",
                K("map.storage-program", "map.inventory-storage"),
                "CA_storageProjections", "CA_storageObjectiveVetoes",
                "CA_inventoryStorageProjections",
                "CA_inventoryStorageVetoes"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CATaskForceMapComponent",
                K("map.task-force"), "CA_taskForces"),
            PA(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CAToxicWasteLifecycleMapComponent",
                K("map.toxic-waste"),
                K("CA_toxicWasteLifecycleObjective")),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.TrapMemoryMapComponent",
                K("map.trap-memory"), "CA_trapSprings",
                "CA_trapSpringTicks"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CAWelfareThresholdSupportMapComponent",
                K("map.welfare-support"), "CA_welfareThresholdSupport"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.WithdrawalMapComponent",
                K("map.withdrawal"), "CA_withdrawalPlans"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CACombatRecoveryMapComponent",
                K("map.immediate-combat-recovery"),
                "CA_combatRecoveryEpisodes", "CA_combatRecoveryNextMove",
                "CA_combatRecoveryOperatorOverrideHarmTicks",
                "CA_combatRecoveryOperatorOverrideUntilTicks",
                "CA_combatRecoveryOperatorOverrideCombatPower",
                "CA_combatRecoveryOperatorOverrideBloodLoss",
                "CA_combatRecoveryOperatorOverrideBleedRate",
                "CA_combatRecoveryOperatorOverrideDeathTicks"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CADraftedCombatInitiativeMapComponent",
                K("map.drafted-combat-initiative"),
                "CA_draftedResponseJobIds", "CA_autonomousRangedJobIds",
                "CA_supportTargetIds", "CA_supportSourceTicks",
                "CA_supportCooldownUntilTicks", "CA_supportAnchors",
                "CA_supportTargetCells", "CA_supportAttemptJobIds",
                "CA_supportAttemptOrigins", "CA_supportEvaluations"),
            P(CACampaignPayloadScope.PerMap,
                "ColonistAwareness.CAOverlayMapComponent",
                K("map.tactical-overlay"), "caOverlayObjective",
                "caOverlayPrimaryObjective", "caOverlayDefensiveLine",
                "caOverlayFallbackLine", "caOverlayOffensiveLine")
        };

        // Repeated deep records are validated at the end of every matching
        // parent occurrence. This prevents one complete record from masking a
        // null or truncated sibling. Dictionary values use the emitted
        // `values/li` path; list records use `li` directly.
        public static readonly CACampaignPayloadRuleDefinition[] PayloadRules =
        {
            R("ColonistAwareness.CAFactionStateWorldComponent",
                "CA_factionStates/li",
                K("culture", "politicalBeliefs", "factionStructure",
                    "originOrigin"),
                K(), K()),
            R("ColonistAwareness.CAPlayerFoundingWorldComponent",
                "CA_playerFounding",
                K("culture", "politicalBeliefs", "arrangement"), K(), K()),
            R("ColonistAwareness.CAOrganizationRelationsWorldComponent",
                "CA_relations/li",
                K("delegatedResponsibilities", "retainedResponsibilities",
                    "originOrigin", "termsOriginOrigin"), K(), K()),
            R("ColonistAwareness.CAOrganizationRelationsWorldComponent",
                "CA_holdings/li", K("beneficiaries", "originOrigin"), K(), K()),
            R("ColonistAwareness.CAOrganizationWorldComponent",
                "CA_organizations/li",
                K("offices", "groups", "customs", "securityPractices",
                    "claims", "policies", "decisionHistory", "memberPawnIds",
                    "openBeliefConflicts", "beliefConflictStartTicks"), K(),
                K("openBeliefConflicts", "beliefConflictStartTicks")),
            R("ColonistAwareness.CAActLedger", "CA_actRecords/li",
                K("knownByIds", "knownHow", "orgsAnswered", "pawnsAnswered"),
                K(), K("knownByIds", "knownHow")),
            R("ColonistAwareness.CATransactionLedger", "transactions/li",
                K("buyer", "seller", "owner", "runBy", "debtor", "creditor"),
                K("terms"), K()),
            R("ColonistAwareness.CATransactionLedger", "debts/li",
                K("debtor", "creditor"), K(), K()),
            R("ColonistAwareness.CACombatSpatialLogComponent",
                "CA_combatSpatialLogRecords/values/li", K("concerns"), K(), K()),
            R("ColonistAwareness.CACombatSpatialLogComponent",
                "CA_combatTopologyIncidents/li",
                K("terrainPalette", "roofPalette", "baselineThings",
                    "knownCellIndices", "observerPawnIds", "observedThingIds",
                    "logIds", "nativeBattleAliases", "deltas", "hazardSamples"),
                K("battlefieldReference"), K()),
            R("ColonistAwareness.CACombatSpatialLogComponent",
                "CA_combatTopologyIncidents/li/deltas/li",
                K("cells", "things"), K("thing"), K()),
            R("ColonistAwareness.CACombatSpatialLogComponent",
                "CA_combatTopologyIncidents/li/battlefieldReference",
                K("terrainPalette", "roofPalette", "artifacts", "changes"),
                K(), K(), true),
            R("ColonistAwareness.CACombatSpatialLogComponent",
                "CA_combatTopologyIncidents/li/hazardSamples/li",
                K("centerCellIndices", "nonzeroCells"), K(), K()),
            R("ColonistAwareness.CAArrangementMapComponent",
                "caArrangements/li", K("cells", "memberIds"), K(), K()),
            R("ColonistAwareness.PlannedUseMapComponent",
                "CA_spacePrograms/li", K("cells", "residents"), K(), K()),
            R("ColonistAwareness.CAHomePrerequisiteMapComponent",
                "CA_homeMaterialDemand", K("requirements"), K(), K(), true),
            R("ColonistAwareness.MissionCasualtyKnowledgeMapComponent",
                "CA_missionCasualtyFacts/li", K("rescuer", "beneficiary"),
                K(), K()),
            R("ColonistAwareness.CAPatrolSystemMapComponent",
                "circuits/li", K("nodes"), K(), K()),
            R("ColonistAwareness.CAPatrolSystemMapComponent",
                "patrols/li", K("pawn", "route"), K(), K()),
            R("ColonistAwareness.CAPlanMapComponent", "caPlans/li",
                K("legs"), K(), K()),
            R("ColonistAwareness.CARoadExpansionMapComponent",
                "CA_roadProjects/li", K("pending"), K(), K()),
            R("ColonistAwareness.CASettlementPlanningContextMapComponent",
                "CA_settlementContextIdeoligions/li", K("memeDefNames"), K(), K()),
            R("ColonistAwareness.CASpatialInitiativeMapComponent",
                "CA_spatialCompletedRoom/li", K("expectedFocusThingIds"), K(), K()),
            R("ColonistAwareness.CAStorageProgramMapComponent",
                "CA_storageProjections/li", K("cells"), K(), K()),
            R("ColonistAwareness.CAStorageProgramMapComponent",
                "CA_inventoryStorageProjections/li",
                K("allowedDefNames", "cells"), K(), K()),
            R("ColonistAwareness.CATaskForceMapComponent",
                "CA_taskForces/li", K("knownEntrances", "knownDefences"), K(), K()),
            R("ColonistAwareness.CAToxicWasteLifecycleMapComponent",
                "CA_toxicWasteLifecycleObjective",
                K("inventory", "capacity", "freezing", "relocation", "transport",
                    "destination", "consequence"), K("nativeResponse"), K(), true),
            R("ColonistAwareness.CAToxicWasteLifecycleMapComponent",
                "CA_toxicWasteLifecycleObjective/inventory", K("lots"), K(), K()),
            R("ColonistAwareness.CAToxicWasteLifecycleMapComponent",
                "CA_toxicWasteLifecycleObjective/capacity",
                K("authoredFootprint", "reachableContainmentCells",
                    "containmentHaulValidCells", "readyCells", "haulValidCells"),
                K(), K()),
            R("ColonistAwareness.CAToxicWasteLifecycleMapComponent",
                "CA_toxicWasteLifecycleObjective/relocation", K("candidates"), K(), K()),
            R("ColonistAwareness.CAToxicWasteLifecycleMapComponent",
                "CA_toxicWasteLifecycleObjective/transport",
                K("pendingThingIds", "succeededThingIds", "authorizedSources"), K(), K()),
            R("ColonistAwareness.CAToxicWasteLifecycleMapComponent",
                "CA_toxicWasteLifecycleObjective/destination", K("cells"), K(), K()),
            R("ColonistAwareness.CAToxicWasteLifecycleMapComponent",
                "CA_toxicWasteLifecycleObjective/consequence",
                K("knowledgeRecordIds", "grievanceRecordIds", "conflictRecordIds"),
                K(), K()),
            R("ColonistAwareness.TrapMemoryMapComponent", "",
                K(), K(), K("CA_trapSprings", "CA_trapSpringTicks"))
        };

        public static CACampaignPreflightDocument Read(string path)
        {
            bool hasPayloadDigest = TryVerifyPayloadDigest(path,
                out bool payloadDigestValid, out long payloadLength,
                out string payloadDigest);
            int? boundary = null;
            int? catalogVersion = null;
            var legacyEpochs = new HashSet<int>();
            var manifest = new Dictionary<string, int>(StringComparer.Ordinal);
            var ownerVersions = new List<CACampaignOwnerVersionRecord>();
            var payloads = new List<CACampaignPayloadRecord>();
            var unsupportedLegacyState = new List<string>();
            var mechanismContainerDepths = new Stack<int>();
            var mechanismRecords = new Stack<MechanismRecord>();
            var nativePayloads = new Stack<NativePayloadRecord>();
            bool containsCAState = false;
            int manifestDepth = -1;
            int mapsDepth = -1;
            int mapCount = 0;
            int currentMapOrdinal = -1;
            int currentMapDepth = -1;
            int worldDepth = -1;
            var containerDepths = new Stack<Tuple<string, int, int>>();
            int recordDepth = -1;
            string manifestKey = null;
            int? manifestVersion = null;
            CACampaignOwnerVersionDefinition? owner = null;
            int ownerDepth = -1;
            int? ownerVersion = null;
            int? ownerLegacyEpoch = null;
            string ownerContainer = null;
            int ownerMapOrdinal = -1;
            string payloadComponentType = null;
            int payloadDepth = -1;
            string payloadContainer = null;
            int payloadMapOrdinal = -1;
            PayloadElementFrame payloadRoot = null;
            var payloadFrames = new Stack<PayloadElementFrame>();
            var payloadFailures = new List<string>();
            int payloadBufferedElements = 0;
            long payloadBufferedTextCharacters = 0;
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreWhitespace = true,
                XmlResolver = null
            };

            using (var payloadStream = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.Read))
            using (XmlReader reader = XmlReader.Create(payloadStream, settings))
            {
                // ReadElementContentAs* positions the reader on the following
                // node. Re-process that node before advancing so adjacent
                // schemaKey/version elements cannot be skipped.
                bool advance = true;
                while (advance ? reader.Read() : !reader.EOF)
                {
                    advance = true;
                    if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        if (nativePayloads.Count > 0)
                        {
                            NativePayloadRecord native = nativePayloads.Peek();
                            while (native.Frames.Count > 0
                                && native.Frames.Peek().Depth >= reader.Depth)
                                ValidateAndPopNativeFrame(native);
                            if (reader.Depth == native.Depth)
                            {
                                ValidateNativePayload(native);
                                if (native.Failures.Count > 0)
                                    throw new InvalidDataException(native.ClassName
                                        + " " + string.Join("; ",
                                            native.Failures));
                                nativePayloads.Pop();
                            }
                        }
                        while (payloadFrames.Count > 0
                            && payloadFrames.Peek().Depth >= reader.Depth)
                            ValidateAndPopPayloadFrame(payloadComponentType,
                                payloadFrames, payloadFailures);
                        if (payloadDepth >= 0 && reader.Depth == payloadDepth
                            && reader.Name == "li")
                        {
                            ValidatePayloadFrame(payloadComponentType,
                                payloadRoot, payloadFailures);
                            ValidatePayloadSemantics(payloadComponentType,
                                payloadRoot, payloadFailures);
                            payloads.Add(new CACampaignPayloadRecord(
                                payloadComponentType, payloadContainer,
                                payloadMapOrdinal,
                                payloadFailures.ToArray()));
                            payloadComponentType = null;
                            payloadDepth = -1;
                            payloadContainer = null;
                            payloadMapOrdinal = -1;
                            payloadRoot = null;
                            payloadFrames.Clear();
                            payloadFailures.Clear();
                            payloadBufferedElements = 0;
                            payloadBufferedTextCharacters = 0;
                        }
                        if (currentMapDepth == reader.Depth
                            && reader.Name == "li")
                            currentMapDepth = -1;
                        if (worldDepth == reader.Depth && reader.Name == "world")
                            worldDepth = -1;
                        if (containerDepths.Count > 0
                            && containerDepths.Peek().Item2 == reader.Depth
                            && reader.Name == "components")
                            containerDepths.Pop();
                        if (reader.Name == "li" && mechanismRecords.Count > 0
                            && mechanismRecords.Peek().Depth == reader.Depth)
                        {
                            MechanismRecord mechanism =
                                mechanismRecords.Pop();
                            string reason = CAPoliticalLegacyMechanisms
                                .UnsupportedReason(mechanism.Axis,
                                    mechanism.Option);
                            if (!string.IsNullOrWhiteSpace(reason))
                                unsupportedLegacyState.Add(reason);
                        }
                        if ((reader.Name == "positions"
                                || reader.Name == "factionStructure")
                            && mechanismContainerDepths.Count > 0
                            && mechanismContainerDepths.Peek() == reader.Depth)
                            mechanismContainerDepths.Pop();
                        if (ownerDepth >= 0 && reader.Depth == ownerDepth
                            && reader.Name == "li")
                        {
                            CACampaignOwnerVersionDefinition definition =
                                owner.Value;
                            ownerVersions.Add(new CACampaignOwnerVersionRecord(
                                definition.SchemaKey, definition.ComponentType,
                                definition.XmlTag, ownerVersion,
                                ownerLegacyEpoch, ownerContainer,
                                ownerMapOrdinal));
                            owner = null;
                            ownerDepth = -1;
                            ownerVersion = null;
                            ownerLegacyEpoch = null;
                            ownerContainer = null;
                            ownerMapOrdinal = -1;
                        }
                        if (recordDepth >= 0 && reader.Depth == recordDepth
                            && reader.Name == "li")
                        {
                            AddManifestRecord(manifest, manifestKey,
                                manifestVersion);
                            recordDepth = -1;
                            manifestKey = null;
                            manifestVersion = null;
                        }
                        else if (manifestDepth >= 0
                            && reader.Depth == manifestDepth
                            && reader.Name == "CA_campaignSchemas")
                        {
                            manifestDepth = -1;
                        }
                        if (mapsDepth >= 0 && reader.Depth == mapsDepth
                            && reader.Name == "maps")
                            mapsDepth = -1;
                        continue;
                    }
                    if (reader.NodeType == XmlNodeType.Text
                        || reader.NodeType == XmlNodeType.CDATA)
                    {
                        if (payloadFrames.Count > 0)
                        {
                            AccountBufferedText(payloadComponentType,
                                reader.Value.Length,
                                ref payloadBufferedTextCharacters);
                            payloadFrames.Peek().Text.Append(reader.Value);
                        }
                        if (nativePayloads.Count > 0
                            && nativePayloads.Peek().Frames.Count > 0)
                        {
                            NativePayloadRecord native =
                                nativePayloads.Peek();
                            AccountBufferedText(native.ClassName,
                                reader.Value.Length,
                                ref native.BufferedTextCharacters);
                            nativePayloads.Peek().Frames.Peek().Text
                                .Append(reader.Value);
                        }
                        continue;
                    }
                    if (reader.NodeType != XmlNodeType.Element) continue;

                    string name = reader.Name;
                    if (nativePayloads.Count > 0
                        && reader.Depth > nativePayloads.Peek().Depth)
                        AddNativeFrame(nativePayloads.Peek(), reader);
                    if (payloadDepth >= 0 && reader.Depth > payloadDepth)
                    {
                        while (payloadFrames.Count > 0
                            && payloadFrames.Peek().Depth >= reader.Depth)
                            ValidateAndPopPayloadFrame(payloadComponentType,
                                payloadFrames, payloadFailures);
                        PayloadElementFrame parent = payloadFrames.Count > 0
                            ? payloadFrames.Peek() : payloadRoot;
                        AccountBufferedElement(payloadComponentType,
                            ref payloadBufferedElements);
                        var frame = new PayloadElementFrame
                        {
                            Depth = reader.Depth,
                            Name = name,
                            Path = string.IsNullOrEmpty(parent.Path)
                                ? name : parent.Path + "/" + name,
                            IsNull = string.Equals(reader.GetAttribute("IsNull"),
                                "True", StringComparison.OrdinalIgnoreCase)
                        };
                        parent.Children.Add(frame);
                        if (reader.IsEmptyElement)
                            ValidatePayloadFrame(payloadComponentType, frame,
                                payloadFailures);
                        else
                            payloadFrames.Push(frame);
                    }
                    if (owner.HasValue
                        && (name == "positions"
                            || name == "factionStructure")
                        && !reader.IsEmptyElement)
                        mechanismContainerDepths.Push(reader.Depth);
                    if (name == "li" && !reader.IsEmptyElement
                        && mechanismContainerDepths.Count > 0
                        && reader.Depth
                            == mechanismContainerDepths.Peek() + 1)
                        mechanismRecords.Push(new MechanismRecord
                        {
                            Depth = reader.Depth
                        });
                    bool directComponentRecord = name == "li"
                        && containerDepths.Count > 0
                        && reader.Depth == containerDepths.Peek().Item2 + 1;
                    if (directComponentRecord && ownerDepth < 0
                        && TryFindOwner(reader.GetAttribute("Class"),
                            out CACampaignOwnerVersionDefinition foundOwner))
                    {
                        if (reader.IsEmptyElement)
                            throw new InvalidDataException(
                                foundOwner.ComponentType
                                + " is an empty campaign owner record");
                        owner = foundOwner;
                        ownerDepth = reader.Depth;
                        ownerVersion = null;
                        ownerLegacyEpoch = null;
                        Tuple<string, int, int> context =
                            containerDepths.Peek();
                        ownerContainer = context.Item1;
                        ownerMapOrdinal = context.Item3;
                    }
                    if (directComponentRecord && payloadDepth < 0)
                    {
                        string candidate = reader.GetAttribute("Class");
                        if (TryFindPayload(candidate, out _))
                        {
                            if (reader.IsEmptyElement)
                                throw new InvalidDataException(candidate
                                    + " is an empty durable payload record");
                            payloadComponentType = candidate;
                            payloadDepth = reader.Depth;
                            Tuple<string, int, int> context =
                                containerDepths.Count > 0
                                    ? containerDepths.Peek() : null;
                            payloadContainer = context?.Item1;
                            payloadMapOrdinal = context?.Item3 ?? -1;
                            payloadRoot = new PayloadElementFrame
                            {
                                Depth = payloadDepth,
                                Name = "li",
                                Path = string.Empty,
                                IsNull = false
                            };
                            payloadBufferedElements = 1;
                            payloadBufferedTextCharacters = 0;
                            payloadFrames.Clear();
                            payloadFailures.Clear();
                        }
                    }
                    string className = reader.GetAttribute("Class");
                    if (IsRecognizedNativeClass(className))
                    {
                        containsCAState = true;
                        if (reader.IsEmptyElement
                            && NativeRequired.ContainsKey(className))
                            throw new InvalidDataException(className
                                + " is an empty native durability record");
                        if (!reader.IsEmptyElement)
                        {
                            var native = new NativePayloadRecord
                            {
                                ClassName = className,
                                Depth = reader.Depth,
                                Root = new PayloadElementFrame
                                {
                                    Depth = reader.Depth,
                                    Name = name,
                                    Path = string.Empty
                                },
                                BufferedElements = 1,
                                BufferedTextCharacters = 0
                            };
                            nativePayloads.Push(native);
                        }
                    }
                    if (directComponentRecord
                        && (ownerDepth >= 0 || payloadDepth >= 0))
                        containsCAState = true;
                    bool outsideComponent = payloadDepth < 0
                        || reader.Depth <= payloadDepth;
                    if (outsideComponent && name == "maps")
                    {
                        if (!reader.IsEmptyElement) mapsDepth = reader.Depth;
                        continue;
                    }
                    if (outsideComponent && mapsDepth >= 0 && name == "li"
                        && reader.Depth == mapsDepth + 1)
                    {
                        mapCount++;
                        currentMapOrdinal = mapCount - 1;
                        currentMapDepth = reader.IsEmptyElement
                            ? -1 : reader.Depth;
                    }
                    if (outsideComponent && name == "components"
                        && !reader.IsEmptyElement)
                    {
                        string container = currentMapDepth >= 0
                            && reader.Depth > currentMapDepth
                            ? "map" : worldDepth >= 0
                                && reader.Depth > worldDepth
                                ? "world" : "game";
                        containerDepths.Push(Tuple.Create(container,
                            reader.Depth, container == "map"
                                ? currentMapOrdinal : -1));
                    }
                    else if (outsideComponent && name == "world"
                        && !reader.IsEmptyElement)
                    {
                        worldDepth = reader.Depth;
                    }
                    if (name == "CA_campaignSchemas"
                        && payloadComponentType ==
                            "ColonistAwareness.CACampaignCompatibilityWorldComponent"
                        && reader.Depth == payloadDepth + 1)
                    {
                        manifestDepth = reader.Depth;
                        continue;
                    }
                    if (name == "CA_campaignBoundaryVersion"
                        && payloadComponentType ==
                            "ColonistAwareness.CACampaignCompatibilityWorldComponent"
                        && reader.Depth == payloadDepth + 1)
                    {
                        int value = reader.ReadElementContentAsInt();
                        CompletePayloadScalar(payloadComponentType,
                            payloadFrames, payloadFailures, name,
                            reader.Depth, value.ToString(),
                            ref payloadBufferedTextCharacters);
                        if (boundary.HasValue && boundary.Value != value)
                            throw new InvalidDataException(
                                "conflicting campaign boundary versions: "
                                + boundary.Value + "," + value);
                        boundary = value;
                        advance = false;
                        continue;
                    }
                    if (name == "CA_campaignCatalogVersion"
                        && payloadComponentType ==
                            "ColonistAwareness.CACampaignCompatibilityWorldComponent"
                        && reader.Depth == payloadDepth + 1)
                    {
                        int value = reader.ReadElementContentAsInt();
                        CompletePayloadScalar(payloadComponentType,
                            payloadFrames, payloadFailures, name,
                            reader.Depth, value.ToString(),
                            ref payloadBufferedTextCharacters);
                        if (catalogVersion.HasValue
                            && catalogVersion.Value != value)
                            throw new InvalidDataException(
                                "conflicting campaign catalog versions: "
                                + catalogVersion.Value + "," + value);
                        catalogVersion = value;
                        advance = false;
                        continue;
                    }
                    if (name == "CA_authoringDataEpoch"
                        && owner.HasValue && reader.Depth == ownerDepth + 1)
                    {
                        int value = reader.ReadElementContentAsInt();
                        CompletePayloadScalar(payloadComponentType,
                            payloadFrames, payloadFailures, name,
                            reader.Depth, value.ToString(),
                            ref payloadBufferedTextCharacters);
                        if (owner.HasValue)
                        {
                            if (ownerLegacyEpoch.HasValue)
                                throw new InvalidDataException(
                                    owner.Value.ComponentType
                                    + " has multiple legacy authoring epochs");
                            ownerLegacyEpoch = value;
                        }
                        else legacyEpochs.Add(value);
                        advance = false;
                        continue;
                    }
                    if (mechanismRecords.Count > 0
                        && reader.Depth > mechanismRecords.Peek().Depth
                        && (name == "axis" || name == "option"))
                    {
                        string value = reader.ReadElementContentAsString();
                        CompletePayloadScalar(payloadComponentType,
                            payloadFrames, payloadFailures, name,
                            reader.Depth, value,
                            ref payloadBufferedTextCharacters);
                        if (name == "axis")
                        {
                            if (mechanismRecords.Peek().Axis != null)
                                throw new InvalidDataException(
                                    "political mechanism record has multiple axes");
                            mechanismRecords.Peek().Axis = value;
                        }
                        else
                        {
                            if (mechanismRecords.Peek().Option != null)
                                throw new InvalidDataException(
                                    "political mechanism record has multiple options");
                            mechanismRecords.Peek().Option = value;
                        }
                        advance = false;
                        continue;
                    }
                    if (manifestDepth < 0 || reader.Depth <= manifestDepth)
                    {
                        if (owner.HasValue && reader.Depth == ownerDepth + 1
                            && name == owner.Value.XmlTag)
                        {
                            if (ownerVersion.HasValue)
                                throw new InvalidDataException(
                                    owner.Value.ComponentType
                                    + " has multiple owner schema versions");
                            ownerVersion = reader.ReadElementContentAsInt();
                            CompletePayloadScalar(payloadComponentType,
                                payloadFrames, payloadFailures, name,
                                reader.Depth, ownerVersion.Value.ToString(),
                                ref payloadBufferedTextCharacters);
                            advance = false;
                        }
                        continue;
                    }
                    if (name == "li" && reader.Depth == manifestDepth + 1)
                    {
                        if (recordDepth >= 0)
                            throw new InvalidDataException(
                                "nested campaign schema records are invalid");
                        if (reader.IsEmptyElement)
                            throw new InvalidDataException(
                                "empty campaign schema record");
                        recordDepth = reader.Depth;
                        continue;
                    }
                    if (recordDepth < 0 || reader.Depth <= recordDepth)
                        continue;
                    if (name == "schemaKey")
                    {
                        if (manifestKey != null)
                            throw new InvalidDataException(
                                "campaign schema record has multiple keys");
                        manifestKey = reader.ReadElementContentAsString();
                        CompletePayloadScalar(payloadComponentType,
                            payloadFrames, payloadFailures, name,
                            reader.Depth, manifestKey,
                            ref payloadBufferedTextCharacters);
                        advance = false;
                    }
                    else if (name == "version")
                    {
                        if (manifestVersion.HasValue)
                            throw new InvalidDataException(
                                "campaign schema record has multiple versions");
                        manifestVersion = reader.ReadElementContentAsInt();
                        CompletePayloadScalar(payloadComponentType,
                            payloadFrames, payloadFailures, name,
                            reader.Depth, manifestVersion.Value.ToString(),
                            ref payloadBufferedTextCharacters);
                        advance = false;
                    }
                }
            }

            if (recordDepth >= 0 || manifestDepth >= 0 || ownerDepth >= 0
                || payloadDepth >= 0
                || mechanismContainerDepths.Count > 0
                || mechanismRecords.Count > 0 || nativePayloads.Count > 0)
                throw new InvalidDataException(
                    "campaign schema manifest ended unexpectedly");
            foreach (int epoch in ownerVersions.Where(item =>
                    item.LegacyAuthoringEpoch.HasValue)
                .Select(item => item.LegacyAuthoringEpoch.Value))
                legacyEpochs.Add(epoch);
            if (legacyEpochs.Count > 1)
                throw new InvalidDataException(
                    "conflicting legacy CA authoring epochs: "
                    + string.Join(",", legacyEpochs.OrderBy(value => value)));
            int legacyEpoch = legacyEpochs.Count == 1
                ? legacyEpochs.First()
                : 0;
            return new CACampaignPreflightDocument(boundary ?? 0,
                catalogVersion ?? 0, mapCount, legacyEpoch, containsCAState, manifest,
                ownerVersions, payloads, unsupportedLegacyState.Distinct(
                    StringComparer.Ordinal).ToList(), hasPayloadDigest,
                payloadDigestValid, payloadDigest);
        }

        private static void CompletePayloadScalar(string componentType,
            Stack<PayloadElementFrame> frames, List<string> failures,
            string name, int ignoredReaderDepth, string value,
            ref long bufferedTextCharacters)
        {
            if (frames.Count == 0 || frames.Peek().Name != name) return;
            AccountBufferedText(componentType, value?.Length ?? 0,
                ref bufferedTextCharacters);
            frames.Peek().Text.Append(value ?? string.Empty);
            ValidateAndPopPayloadFrame(componentType, frames, failures);
        }

        private static void AccountBufferedElement(string owner,
            ref int bufferedElements)
        {
            bufferedElements++;
            if (bufferedElements > MaxBufferedElementsPerRecord)
                throw new InvalidDataException((owner ?? "campaign payload")
                    + " exceeds the " + MaxBufferedElementsPerRecord
                    + "-element streaming preflight record limit");
        }

        private static void AccountBufferedText(string owner, int characters,
            ref long bufferedTextCharacters)
        {
            bufferedTextCharacters += characters;
            if (bufferedTextCharacters > MaxBufferedTextCharactersPerRecord)
                throw new InvalidDataException((owner ?? "campaign payload")
                    + " exceeds the " + MaxBufferedTextCharactersPerRecord
                    + "-character streaming preflight record limit");
        }

        private static void ValidateAndPopPayloadFrame(string componentType,
            Stack<PayloadElementFrame> frames, List<string> failures)
        {
            PayloadElementFrame frame = frames.Pop();
            ValidatePayloadFrame(componentType, frame, failures);
        }

        private static void ValidatePayloadFrame(string componentType,
            PayloadElementFrame frame, List<string> failures)
        {
            if (frame == null) return;
            if (frame.Name == "li" && IsNullValue(frame))
                failures.Add(frame.Path + " contains a null record/reference");

            ValidateDictionaryShape(frame, failures);

            if (frame.Path.Length == 0
                && TryFindPayload(componentType,
                    out CACampaignPayloadDefinition definition))
                ValidateRequiredChildren(frame,
                    definition.RequiredNonNullPaths,
                    definition.RequiredAllowNullPaths, Array.Empty<string>(),
                    failures);

            for (int index = 0; index < PayloadRules.Length; index++)
            {
                CACampaignPayloadRuleDefinition rule = PayloadRules[index];
                if (!string.Equals(rule.ComponentType, componentType,
                        StringComparison.Ordinal)
                    || !string.Equals(rule.ParentPath, frame.Path,
                        StringComparison.Ordinal)) continue;
                if (IsNullValue(frame) && rule.SkipWhenParentNull) continue;
                if (IsNullValue(frame))
                {
                    failures.Add(frame.Path + " is a null durable record");
                    continue;
                }
                ValidateRequiredChildren(frame,
                    rule.RequiredNonNullChildren,
                    rule.RequiredAllowNullChildren,
                    rule.EqualLengthChildren, failures);
            }
        }

        private static void ValidateRequiredChildren(PayloadElementFrame frame,
            IReadOnlyList<string> requiredNonNull,
            IReadOnlyList<string> requiredAllowNull,
            IReadOnlyList<string> equalLength, List<string> failures)
        {
            for (int index = 0; index < requiredNonNull.Count; index++)
            {
                string name = requiredNonNull[index];
                List<PayloadElementFrame> matches = frame.Children
                    .Where(child => child.Name == name).ToList();
                if (matches.Count != 1)
                {
                    failures.Add(DisplayPath(frame, name)
                        + " must occur exactly once; found " + matches.Count);
                    continue;
                }
                if (IsNullValue(matches[0]))
                    failures.Add(DisplayPath(frame, name) + " is null");
            }
            for (int index = 0; index < requiredAllowNull.Count; index++)
            {
                string name = requiredAllowNull[index];
                int count = frame.Children.Count(child => child.Name == name);
                if (count != 1)
                    failures.Add(DisplayPath(frame, name)
                        + " must occur exactly once; found " + count);
            }
            if (equalLength.Count <= 1) return;
            int? expected = null;
            for (int index = 0; index < equalLength.Count; index++)
            {
                PayloadElementFrame collection = frame.Children
                    .FirstOrDefault(child => child.Name == equalLength[index]);
                if (collection == null) continue;
                if (!expected.HasValue) expected = collection.ItemCount;
                else if (expected.Value != collection.ItemCount)
                    failures.Add(frame.Path + " parallel collections "
                        + string.Join(", ", equalLength) + " differ in length");
            }
        }

        private static bool IsNullValue(PayloadElementFrame frame)
        {
            return frame != null && (frame.IsNull || (frame.Children.Count == 0
                && string.Equals(frame.Text.ToString().Trim(), "null",
                    StringComparison.OrdinalIgnoreCase)));
        }

        private static void ValidateDictionaryShape(PayloadElementFrame frame,
            List<string> failures)
        {
            List<PayloadElementFrame> keys = frame.Children
                .Where(item => item.Name == "keys").ToList();
            List<PayloadElementFrame> values = frame.Children
                .Where(item => item.Name == "values").ToList();
            if (keys.Count == 0 && values.Count == 0) return;
            if (keys.Count != 1 || values.Count != 1)
            {
                failures.Add((frame.Path.Length == 0 ? "payload" : frame.Path)
                    + " dictionary must contain one keys and one values list");
                return;
            }
            if (IsNullValue(keys[0]) || IsNullValue(values[0]))
                failures.Add(frame.Path + " dictionary keys/values are null");
            if (keys[0].ItemCount != values[0].ItemCount)
                failures.Add(frame.Path + " dictionary keys/values differ in length");
        }

        private static bool IsRecognizedNativeClass(string className)
        {
            if (string.IsNullOrWhiteSpace(className)) return false;
            if (NativeSchemaByClass.ContainsKey(className)) return true;
            for (int index = 0; index < NativeSchemaPrefixes.Length; index++)
                if (className.StartsWith(
                        NativeSchemaPrefixes[index].Key,
                        StringComparison.Ordinal)) return true;
            return false;
        }

        private static void AddNativeFrame(NativePayloadRecord native,
            XmlReader reader)
        {
            while (native.Frames.Count > 0
                && native.Frames.Peek().Depth >= reader.Depth)
                ValidateAndPopNativeFrame(native);
            PayloadElementFrame parent = native.Frames.Count > 0
                ? native.Frames.Peek() : native.Root;
            AccountBufferedElement(native.ClassName,
                ref native.BufferedElements);
            var frame = new PayloadElementFrame
            {
                Depth = reader.Depth,
                Name = reader.Name,
                Path = string.IsNullOrEmpty(parent.Path)
                    ? reader.Name : parent.Path + "/" + reader.Name,
                IsNull = string.Equals(reader.GetAttribute("IsNull"), "True",
                    StringComparison.OrdinalIgnoreCase)
            };
            parent.Children.Add(frame);
            if (reader.IsEmptyElement)
                ValidateNativeFrame(frame, native.Failures);
            else native.Frames.Push(frame);
        }

        private static void ValidateAndPopNativeFrame(
            NativePayloadRecord native)
        {
            PayloadElementFrame frame = native.Frames.Pop();
            ValidateNativeFrame(frame, native.Failures);
        }

        private static void ValidateNativeFrame(PayloadElementFrame frame,
            List<string> failures)
        {
            if (frame.Name == "li" && IsNullValue(frame))
                failures.Add(frame.Path + " contains a null native record/reference");
            ValidateDictionaryShape(frame, failures);
        }

        private static void ValidateNativePayload(NativePayloadRecord native)
        {
            if (!NativeRequired.TryGetValue(native.ClassName,
                    out string[] required))
                return; // CA JobDrivers persist native job state unless listed.
            ValidateRequiredChildren(native.Root, required, K(), K(),
                native.Failures);
            if (NativeParallel.TryGetValue(native.ClassName,
                    out string[] parallel))
                ValidateRequiredChildren(native.Root, K(), K(), parallel,
                    native.Failures);
            if (native.ClassName ==
                "ColonistAwareness.LordJob_CAStackBreach"
                && NativeParallel.TryGetValue(native.ClassName + "#clear",
                    out string[] clearParallel))
                ValidateRequiredChildren(native.Root, K(), K(), clearParallel,
                    native.Failures);
        }

        private static string DisplayPath(PayloadElementFrame frame,
            string child)
        {
            return string.IsNullOrEmpty(frame.Path)
                ? child : frame.Path + "/" + child;
        }

        private static void ValidatePayloadSemantics(string componentType,
            PayloadElementFrame root, List<string> failures)
        {
            if (root == null) return;
            ValidateNestedSchemaBindings(componentType, root, failures);
            if (componentType == "ColonistAwareness.CAActLedger")
                ValidateIdContinuity(root, "CA_actRecords/li", "id",
                    "CA_actRecordNextId", failures);
            else if (componentType == "ColonistAwareness.CATransactionLedger")
                ValidateIdContinuity(root, "transactions/li", "id",
                    "nextId", failures);
            else if (componentType == "ColonistAwareness.KnowledgeMapComponent")
            {
                foreach (PayloadElementFrame record in FindFrames(root,
                    "CA_accountability/li"))
                {
                    RequirePositive(record, "ownerId", failures);
                    RequirePositive(record, "subjectId", failures);
                }
            }
            else if (componentType ==
                "ColonistAwareness.CARegionalWorldComponent")
                ValidateRegionalNestedPayload(root, failures);
        }

        private static void ValidateNestedSchemaBindings(string componentType,
            PayloadElementFrame root, List<string> failures)
        {
            for (int index = 0; index < NestedSchemaBindings.Length; index++)
            {
                NestedSchemaBinding binding = NestedSchemaBindings[index];
                if (!string.Equals(binding.ComponentType, componentType,
                        StringComparison.Ordinal)) continue;
                foreach (PayloadElementFrame frame in FindFrames(root,
                    binding.ParentPath))
                {
                    if (binding.AllowNull && IsNullValue(frame)) continue;
                    if (IsNullValue(frame))
                    {
                        failures.Add(binding.ParentPath + " is a null "
                            + binding.SchemaKey + " record");
                        continue;
                    }
                    CACampaignSchemaDefinition schema =
                        CACampaignSchemaCatalog.All.First(item =>
                            item.Key == binding.SchemaKey);
                    PayloadElementFrame version = frame.Children
                        .FirstOrDefault(item => item.Name == "schemaVersion");
                    if (version != null
                        && (!int.TryParse(version.Text.ToString(),
                                out int savedVersion)
                            || savedVersion != schema.CurrentVersion))
                        failures.Add(binding.ParentPath + " "
                            + binding.SchemaKey + " schemaVersion is "
                            + version.Text + ", expected "
                            + schema.CurrentVersion);
                    if (binding.SchemaKey == "model.culture")
                        ValidateRequiredChildren(frame,
                            K("constituents", "inheritedMeanings",
                                "localMeanings", "transitions",
                                "inheritedPractices", "practices",
                                "observations"), K("lastEvidence"), K(),
                            failures);
                    else if (binding.SchemaKey ==
                        "model.political-beliefs")
                        ValidateRequiredChildren(frame,
                            K("positions", "derivationReceipts"), K(), K(),
                            failures);
                }
            }
        }

        private static void ValidateRegionalNestedPayload(
            PayloadElementFrame root, List<string> failures)
        {
            foreach (PayloadElementFrame region in FindFrames(root,
                "CA_regionalPlans/li"))
                ValidateRequiredChildren(region,
                    K("memberTileIds", "footprintTileIds", "factions",
                        "settlements", "relations", "frontierHoldings",
                        "worldPolicy", "groundwater", "playerFounding"),
                    K(), K(), failures);
            foreach (PayloadElementFrame faction in FindFrames(root,
                "CA_regionalPlans/li/factions/li"))
                ValidateRequiredChildren(faction,
                    K("factionStructure", "culture", "politicalBeliefs"),
                    K("resolvedFaction"), K(), failures);
            foreach (PayloadElementFrame settlement in FindFrames(root,
                "CA_regionalPlans/li/settlements/li"))
                ValidateRequiredChildren(settlement,
                    K("populationGroups", "provisionArrangements",
                        "domesticProvisionDemands", "operationalFacts",
                        "settlementProgram", "localCulture"),
                    K(), K(), failures);
            foreach (PayloadElementFrame holding in FindFrames(root,
                "CA_regionalPlans/li/frontierHoldings/li"))
                ValidateRequiredChildren(holding, K("residentPawnIds"),
                    K(), K(), failures);
            foreach (PayloadElementFrame record in FindFrames(root,
                "CA_regionalSettlements/li"))
                ValidateRequiredChildren(record,
                    K("capabilities", "residentIds", "populationGroups",
                        "residenceAssignments", "provisionArrangements",
                        "domesticProvisionDemands", "domesticUnits",
                        "domesticMembershipTransitions", "operationalFacts",
                        "settlementProgram", "culture",
                        "populationAssignments", "statusAssignments",
                        "seededAssets", "programAssets", "startingStock",
                        "creationBeneficiaries", "developmentBeneficiaries",
                        "developmentDemandKinds",
                        "developmentAssetCandidates"),
                    K("faction", "layout"), K(), failures);
            foreach (PayloadElementFrame capability in FindFrames(root,
                "CA_regionalSettlements/li/capabilities/li"))
                ValidateRequiredChildren(capability,
                    K("actorIdentities", "organizationIdentities",
                        "operationIdentities", "knowledgeEvidence",
                        "materialEvidence", "historicalEvidence"),
                    K(), K(), failures);
            foreach (PayloadElementFrame unit in FindFrames(root,
                "CA_regionalSettlements/li/domesticUnits/li"))
                ValidateRequiredChildren(unit,
                    K("sharedProvisionNodeKeys", "memberships"), K(), K(),
                    failures);
            foreach (PayloadElementFrame program in FindFrames(root,
                "CA_regionalSettlements/li/settlementProgram"))
                ValidateRequiredChildren(program, K("entries"), K(), K(),
                    failures);
            foreach (PayloadElementFrame layout in FindFrames(root,
                "CA_regionalSettlements/li/layout"))
            {
                if (IsNullValue(layout)) continue;
                ValidateRequiredChildren(layout,
                    K("gates", "gateWidths", "ways", "facilityKinds",
                        "facilityCells", "roads", "roomCells", "roomRoles",
                        "utilities", "utilityKinds", "approaches"), K(),
                    K("gates", "gateWidths"), failures);
                ValidateRequiredChildren(layout, K(), K(),
                    K("facilityKinds", "facilityCells"), failures);
                ValidateRequiredChildren(layout, K(), K(),
                    K("roomCells", "roomRoles"), failures);
                ValidateRequiredChildren(layout, K(), K(),
                    K("utilities", "utilityKinds"), failures);
            }
        }

        private static void ValidateIdContinuity(PayloadElementFrame root,
            string recordPath, string idName, string nextName,
            List<string> failures)
        {
            int maximum = 0;
            PayloadElementFrame[] records = FindFrames(root, recordPath)
                .ToArray();
            foreach (PayloadElementFrame record in records)
            {
                int id = RequirePositive(record, idName, failures);
                if (id > maximum) maximum = id;
            }
            PayloadElementFrame next = root.Children.FirstOrDefault(item =>
                item.Name == nextName);
            if (next == null)
            {
                if (records.Length == 0) return; // Scribe omits default 1.
                failures.Add(nextName + " is missing");
                return;
            }
            if (!int.TryParse(next.Text.ToString(), out int value)
                || value <= maximum)
                failures.Add(nextName + " must be greater than every saved "
                    + idName + "; maximum " + maximum + ", next "
                    + next.Text);
        }

        private static int RequirePositive(PayloadElementFrame parent,
            string name, List<string> failures)
        {
            PayloadElementFrame field = parent.Children.FirstOrDefault(item =>
                item.Name == name);
            if (field == null || field.IsNull
                || !int.TryParse(field.Text.ToString(), out int value)
                || value <= 0)
            {
                failures.Add(DisplayPath(parent, name)
                    + " must be a positive integer");
                return 0;
            }
            return value;
        }

        private static IEnumerable<PayloadElementFrame> FindFrames(
            PayloadElementFrame root, string path)
        {
            foreach (PayloadElementFrame child in root.Children)
            {
                if (child.Path == path) yield return child;
                foreach (PayloadElementFrame nested in FindFrames(child, path))
                    yield return nested;
            }
        }

        private static bool TryFindOwner(string componentType,
            out CACampaignOwnerVersionDefinition definition)
        {
            for (int i = 0; i < OwnerVersionDefinitions.Length; i++)
            {
                if (!string.Equals(OwnerVersionDefinitions[i].ComponentType,
                        componentType, StringComparison.Ordinal)) continue;
                definition = OwnerVersionDefinitions[i];
                return true;
            }
            definition = default(CACampaignOwnerVersionDefinition);
            return false;
        }

        private static bool TryFindPayload(string componentType,
            out CACampaignPayloadDefinition definition)
        {
            for (int i = 0; i < PayloadDefinitions.Length; i++)
            {
                if (!string.Equals(PayloadDefinitions[i].ComponentType,
                        componentType, StringComparison.Ordinal)) continue;
                definition = PayloadDefinitions[i];
                return true;
            }
            definition = null;
            return false;
        }

        public static void AppendPayloadDigest(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));
            if (ContainsPayloadDigestMarker(path))
                throw new InvalidDataException(
                    "campaign save candidate already contains a payload seal");
            using (var stream = new FileStream(path, FileMode.Open,
                FileAccess.ReadWrite, FileShare.None))
            {
                long payloadLength = stream.Length;
                string trailer = PayloadDigestPrefix
                    + PayloadDigest(stream, payloadLength)
                    + PayloadDigestSuffix;
                byte[] trailerBytes = Encoding.ASCII.GetBytes(trailer);
                stream.Position = payloadLength;
                stream.Write(trailerBytes, 0, trailerBytes.Length);
                stream.Flush(true);
            }
        }

        public static bool TryVerifyPayloadDigest(string path,
            out bool valid, out long payloadLength)
        {
            return TryVerifyPayloadDigest(path, out valid,
                out payloadLength, out _);
        }

        public static bool TryVerifyPayloadDigest(string path,
            out bool valid, out long payloadLength, out string storedDigest)
        {
            valid = false;
            payloadLength = 0;
            storedDigest = null;
            byte[] prefix = Encoding.ASCII.GetBytes(PayloadDigestPrefix);
            byte[] suffix = Encoding.ASCII.GetBytes(PayloadDigestSuffix);
            int trailerLength = prefix.Length + 64 + suffix.Length;
            using (var stream = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.Read))
            {
                payloadLength = stream.Length;
                if (stream.Length < trailerLength) return false;
                long at = stream.Length - trailerLength;
                stream.Position = at;
                var trailer = new byte[trailerLength];
                int offset = 0;
                while (offset < trailer.Length)
                {
                    int read = stream.Read(trailer, offset,
                        trailer.Length - offset);
                    if (read <= 0) return false;
                    offset += read;
                }
                for (int i = 0; i < prefix.Length; i++)
                    if (trailer[i] != prefix[i]) return false;
                for (int i = 0; i < suffix.Length; i++)
                    if (trailer[prefix.Length + 64 + i] != suffix[i])
                        return false;
                string stored = Encoding.ASCII.GetString(trailer,
                    prefix.Length, 64);
                if (stored.Any(value => !Uri.IsHexDigit(value))) return false;
                storedDigest = stored.ToUpperInvariant();
                payloadLength = at;
                if (ContainsPayloadDigestMarker(stream, at)) return false;
                valid = string.Equals(stored, PayloadDigest(stream, at),
                    StringComparison.OrdinalIgnoreCase);
                return true;
            }
        }

        private static bool ContainsPayloadDigestMarker(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.Read))
                return ContainsPayloadDigestMarker(stream, stream.Length);
        }

        private static bool ContainsPayloadDigestMarker(Stream stream,
            long count)
        {
            byte[] marker = Encoding.ASCII.GetBytes(PayloadDigestPrefix);
            var buffer = new byte[64 * 1024];
            int matched = 0;
            long remaining = count;
            stream.Position = 0;
            while (remaining > 0)
            {
                int read = stream.Read(buffer, 0,
                    (int)Math.Min(buffer.Length, remaining));
                if (read <= 0) break;
                remaining -= read;
                for (int index = 0; index < read; index++)
                {
                    byte value = buffer[index];
                    if (value == marker[matched])
                    {
                        matched++;
                        if (matched == marker.Length) return true;
                    }
                    else matched = value == marker[0] ? 1 : 0;
                }
            }
            return false;
        }

        private static string PayloadDigest(Stream stream, long count)
        {
            stream.Position = 0;
            using (SHA256 sha = SHA256.Create())
            {
                var buffer = new byte[64 * 1024];
                long remaining = count;
                while (remaining > 0)
                {
                    int requested = (int)Math.Min(buffer.Length, remaining);
                    int read = stream.Read(buffer, 0, requested);
                    if (read <= 0)
                        throw new EndOfStreamException(
                            "campaign payload ended during digest calculation");
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    remaining -= read;
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(sha.Hash)
                    .Replace("-", string.Empty);
            }
        }

        private static CACampaignOwnerVersionDefinition O(string schemaKey,
            string componentType, string xmlTag, bool requiredOncePerSave)
        {
            return new CACampaignOwnerVersionDefinition(schemaKey,
                componentType, xmlTag, requiredOncePerSave);
        }

        private static CACampaignPayloadDefinition P(
            CACampaignPayloadScope scope, string componentType,
            string[] schemaKeys, params string[] requiredNonNullPaths)
        {
            return new CACampaignPayloadDefinition(scope, componentType,
                schemaKeys, requiredNonNullPaths);
        }

        private static CACampaignPayloadDefinition PA(
            CACampaignPayloadScope scope, string componentType,
            string[] schemaKeys, string[] allowNull,
            params string[] requiredNonNullPaths)
        {
            return new CACampaignPayloadDefinition(scope, componentType,
                schemaKeys, requiredNonNullPaths, allowNull);
        }

        private static CACampaignPayloadRuleDefinition R(string componentType,
            string parentPath, string[] requiredNonNull,
            string[] requiredAllowNull, string[] equalLength,
            bool skipWhenParentNull = false)
        {
            return new CACampaignPayloadRuleDefinition(componentType,
                parentPath, requiredNonNull, requiredAllowNull, equalLength,
                skipWhenParentNull);
        }

        private static string[] K(params string[] keys) => keys;

        private static NestedSchemaBinding N(string schemaKey,
            string componentType, string parentPath, bool allowNull = false)
        {
            return new NestedSchemaBinding(schemaKey, componentType,
                parentPath, allowNull);
        }

        private static void AddManifestRecord(
            Dictionary<string, int> manifest, string key, int? version)
        {
            if (string.IsNullOrWhiteSpace(key) || !version.HasValue)
                throw new InvalidDataException(
                    "campaign schema record is incomplete");
            if (manifest.ContainsKey(key))
                throw new InvalidDataException(
                    "duplicate campaign schema " + key);
            manifest.Add(key, version.Value);
        }
    }

    public static class CACampaignPreflightValidator
    {
        public static CACampaignCompatibilityDecision Evaluate(
            CACampaignPreflightDocument document)
        {
            return Evaluate(document, requirePayloadDigest: true);
        }

        public static CACampaignCompatibilityDecision EvaluateEmittedCurrent(
            CACampaignPreflightDocument document)
        {
            return Evaluate(document, requirePayloadDigest: false);
        }

        private static CACampaignCompatibilityDecision Evaluate(
            CACampaignPreflightDocument document, bool requirePayloadDigest)
        {
            if (document == null)
                return Unsupported("campaign preflight document is missing");
            if (document.UnsupportedLegacyState.Count > 0)
                return Unsupported(string.Join("; ",
                    document.UnsupportedLegacyState)
                    + ". The source save was not changed; select the actual "
                    + "mechanisms in a supported authoring build before load.");
            CACampaignCompatibilityDecision boundaryDecision =
                CACampaignCompatibilityKernel.EvaluateBoundary(
                    document.BoundaryVersion,
                    document.LegacyAuthoringEpoch,
                    document.ContainsCAState);
            if (!boundaryDecision.CanLoad)
                return boundaryDecision;
            if (!requirePayloadDigest && document.BoundaryVersion
                    != CACampaignCompatibilityKernel.CurrentBoundaryVersion)
                return Unsupported("an emitted save must use current campaign "
                    + "boundary "
                    + CACampaignCompatibilityKernel.CurrentBoundaryVersion);
            if (document.BoundaryVersion == 0)
            {
                for (int i = 0; i < document.OwnerVersions.Count; i++)
                {
                    CACampaignOwnerVersionRecord owner =
                        document.OwnerVersions[i];
                    if (owner.Version.HasValue && owner.Version.Value != 0)
                        return Unsupported(owner.ComponentType
                            + " is unversioned at the campaign boundary but "
                            + "declares owner schema " + owner.Version.Value);
                    if (boundaryDecision.Kind
                            == CACampaignCompatibilityKind.MigrateB10
                        && owner.LegacyAuthoringEpoch
                            != CACampaignCompatibilityKernel
                                .LegacyB10AuthoringEpoch)
                        return Unsupported(owner.ComponentType
                            + " has legacy authoring epoch "
                            + (owner.LegacyAuthoringEpoch?.ToString()
                                ?? "missing") + "; expected "
                            + CACampaignCompatibilityKernel
                                .LegacyB10AuthoringEpoch);
                }
                CACampaignCompatibilityDecision cardinality =
                    ValidateOwnerCardinality(document,
                        boundaryDecision.Kind
                            == CACampaignCompatibilityKind.MigrateB10);
                if (!cardinality.CanLoad) return cardinality;
                return boundaryDecision;
            }
            if (requirePayloadDigest && !document.HasPayloadDigest)
                return Unsupported("current campaign payload digest is missing");
            if (requirePayloadDigest && !document.PayloadDigestValid)
                return Unsupported("current campaign payload digest does not "
                    + "match the saved bytes");
            if (document.CatalogVersion < 1 || document.CatalogVersion
                > CACampaignSchemaCatalog.CurrentCatalogVersion)
                return Unsupported("campaign catalog version "
                    + document.CatalogVersion + " is unsupported; current is "
                    + CACampaignSchemaCatalog.CurrentCatalogVersion);

            foreach (KeyValuePair<string, int> entry in document.Manifest)
            {
                CACampaignCompatibilityDecision schemaDecision =
                    CACampaignCompatibilityKernel.EvaluateSchema(
                        entry.Key, entry.Value);
                if (!schemaDecision.CanLoad) return schemaDecision;
            }
            for (int i = 0; i < CACampaignSchemaCatalog.All.Length; i++)
            {
                CACampaignSchemaDefinition definition =
                    CACampaignSchemaCatalog.All[i];
                if (definition.IntroducedCatalogVersion
                    > document.CatalogVersion) continue;
                if (!document.Manifest.TryGetValue(definition.Key,
                        out int savedVersion))
                    return Unsupported("campaign catalog "
                        + document.CatalogVersion + " is missing required "
                        + "schema " + definition.Key);
                CACampaignCompatibilityDecision schemaDecision =
                    CACampaignCompatibilityKernel.EvaluateSchema(
                        definition.Key, savedVersion);
                if (!schemaDecision.CanLoad) return schemaDecision;
            }
            for (int i = 0; i < document.OwnerVersions.Count; i++)
            {
                CACampaignOwnerVersionRecord owner =
                    document.OwnerVersions[i];
                if (!owner.Version.HasValue)
                    return Unsupported(owner.ComponentType + " is missing "
                        + owner.XmlTag);
                if (!document.Manifest.TryGetValue(owner.SchemaKey,
                        out int manifestVersion))
                    return Unsupported(owner.ComponentType
                        + " has no manifest schema " + owner.SchemaKey);
                if (owner.Version.Value != manifestVersion)
                    return Unsupported(owner.ComponentType + " owner schema "
                        + owner.Version.Value
                        + " disagrees with manifest schema "
                    + manifestVersion);
            }
            for (int i = 0;
                i < CACampaignPreflightReader.OwnerVersionDefinitions.Length;
                i++)
            {
                CACampaignOwnerVersionDefinition required =
                    CACampaignPreflightReader.OwnerVersionDefinitions[i];
                int count = document.OwnerVersions.Count(item =>
                    item.ComponentType == required.ComponentType);
                if (required.RequiredOncePerSave && count != 1)
                    return Unsupported(required.ComponentType
                        + " must occur exactly once; found " + count);
                if (!required.RequiredOncePerSave
                    && document.OwnerVersions.Any(item =>
                        item.ComponentType == required.ComponentType
                        && item.Version.HasValue == false))
                    return Unsupported(required.ComponentType
                        + " is missing " + required.XmlTag);
            }
            CACampaignCompatibilityDecision currentCardinality =
                ValidateOwnerCardinality(document, expectOwners: true);
            if (!currentCardinality.CanLoad) return currentCardinality;
            CACampaignCompatibilityDecision payloadDecision =
                ValidatePayloads(document);
            if (!payloadDecision.CanLoad) return payloadDecision;
            CACampaignCompatibilityDecision coverageDecision =
                ValidateCatalogCoverage();
            if (!coverageDecision.CanLoad) return coverageDecision;
            return new CACampaignCompatibilityDecision(
                CACampaignCompatibilityKind.Current,
                "current durable campaign boundary, catalog, manifest, and "
                + "complete digested owner payload are compatible");
        }

        public static CACampaignCompatibilityDecision ValidatePayloads(
            CACampaignPreflightDocument document)
        {
            for (int i = 0;
                i < CACampaignPreflightReader.PayloadDefinitions.Length; i++)
            {
                CACampaignPayloadDefinition definition =
                    CACampaignPreflightReader.PayloadDefinitions[i];
                List<CACampaignPayloadRecord> records = document.Payloads
                    .Where(item => string.Equals(item.ComponentType,
                        definition.ComponentType, StringComparison.Ordinal))
                    .ToList();
                int expected = definition.Scope == CACampaignPayloadScope.PerMap
                    ? document.MapCount : 1;
                if (records.Count != expected)
                    return Unsupported(definition.ComponentType
                        + " must occur " + expected + " time(s); found "
                        + records.Count);
                string expectedContainer = definition.Scope
                    == CACampaignPayloadScope.WorldOnce ? "world"
                    : definition.Scope == CACampaignPayloadScope.GameOnce
                        ? "game" : "map";
                for (int recordIndex = 0; recordIndex < records.Count;
                    recordIndex++)
                {
                    CACampaignPayloadRecord record = records[recordIndex];
                    if (record.Container != expectedContainer)
                        return Unsupported(definition.ComponentType
                            + " belongs in " + expectedContainer
                            + "/components, not "
                            + (record.Container ?? "an unknown container"));
                    if (record.ValidationFailures.Count > 0)
                        return Unsupported(definition.ComponentType + " "
                            + string.Join("; ", record.ValidationFailures));
                }
                if (definition.Scope == CACampaignPayloadScope.PerMap)
                {
                    int[] ordinals = records.Select(item => item.MapOrdinal)
                        .OrderBy(value => value).ToArray();
                    if (!ordinals.SequenceEqual(Enumerable.Range(0,
                            document.MapCount)))
                        return Unsupported(definition.ComponentType
                            + " must occur once in every saved map component "
                            + "container; ordinals were "
                            + string.Join(",", ordinals));
                }
                else if (records.Any(item => item.MapOrdinal != -1))
                {
                    return Unsupported(definition.ComponentType
                        + " has an unexpected map ordinal");
                }
            }
            return new CACampaignCompatibilityDecision(
                CACampaignCompatibilityKind.Current,
                "durable payload component cardinality and roots match");
        }

        public static CACampaignCompatibilityDecision ValidateCatalogCoverage()
        {
            var routes = new Dictionary<string, HashSet<string>>(
                StringComparer.Ordinal);
            Action<string, string> add = (key, route) =>
            {
                if (!routes.TryGetValue(key, out HashSet<string> values))
                {
                    values = new HashSet<string>(StringComparer.Ordinal);
                    routes.Add(key, values);
                }
                values.Add(route);
            };
            foreach (CACampaignOwnerVersionDefinition owner in
                CACampaignPreflightReader.OwnerVersionDefinitions)
                add(owner.SchemaKey, "inline-owner:" + owner.ComponentType);
            foreach (CACampaignPayloadDefinition payload in
                CACampaignPreflightReader.PayloadDefinitions)
            foreach (string key in payload.SchemaKeys)
                add(key, "component:" + payload.ComponentType);
            foreach (CACampaignPreflightReader.NestedSchemaBinding binding in
                CACampaignPreflightReader.NestedSchemaBindings)
            {
                if (!CACampaignPreflightReader.PayloadDefinitions.Any(item =>
                        item.ComponentType == binding.ComponentType))
                    return Unsupported("nested schema " + binding.SchemaKey
                        + " is bound to unknown payload "
                        + binding.ComponentType);
                add(binding.SchemaKey, "nested:" + binding.ComponentType
                    + "/" + binding.ParentPath);
            }
            foreach (KeyValuePair<string, string> binding in
                CACampaignPreflightReader.NativeSchemaByClass)
                add(binding.Value, "native-class:" + binding.Key);
            foreach (KeyValuePair<string, string> binding in
                CACampaignPreflightReader.NativeSchemaPrefixes)
                add(binding.Value, "native-prefix:" + binding.Key);
            var catalog = new HashSet<string>(CACampaignSchemaCatalog.All
                .Select(item => item.Key), StringComparer.Ordinal);
            var covered = new HashSet<string>(routes.Keys,
                StringComparer.Ordinal);
            if (!catalog.SetEquals(covered))
            {
                string missing = string.Join(",", catalog.Except(covered)
                    .OrderBy(item => item, StringComparer.Ordinal));
                string unknown = string.Join(",", covered.Except(catalog)
                    .OrderBy(item => item, StringComparer.Ordinal));
                return Unsupported("campaign catalog executable persistence "
                    + "routes are incomplete: missing=[" + missing
                    + "], unknown=[" + unknown + "]");
            }
            return new CACampaignCompatibilityDecision(
                CACampaignCompatibilityKind.Current,
                covered.Count + " catalog schemas have executable component, "
                + "nested-record, or native-class persistence routes");
        }

        private static CACampaignCompatibilityDecision
            ValidateOwnerCardinality(CACampaignPreflightDocument document,
                bool expectOwners)
        {
            if (!expectOwners)
            {
                if (document.OwnerVersions.Count != 0)
                    return Unsupported("a campaign without prior CA state has "
                        + document.OwnerVersions.Count
                        + " persisted CA compatibility owners");
                return CompatibleCardinality();
            }
            for (int i = 0;
                i < CACampaignPreflightReader.OwnerVersionDefinitions.Length;
                i++)
            {
                CACampaignOwnerVersionDefinition definition =
                    CACampaignPreflightReader.OwnerVersionDefinitions[i];
                int actual = document.OwnerVersions.Count(item =>
                    item.ComponentType == definition.ComponentType);
                int expected = definition.RequiredOncePerSave
                    ? 1 : document.MapCount;
                if (actual != expected)
                    return Unsupported(definition.ComponentType
                        + " must occur " + expected + " time(s) for "
                        + document.MapCount + " saved map(s); found " + actual);
                List<CACampaignOwnerVersionRecord> records =
                    document.OwnerVersions.Where(item =>
                        item.ComponentType == definition.ComponentType)
                    .ToList();
                string expectedContainer = definition.RequiredOncePerSave
                    ? "world" : "map";
                if (records.Any(item => item.Container != expectedContainer))
                    return Unsupported(definition.ComponentType
                        + " belongs in " + expectedContainer
                        + "/components");
                if (!definition.RequiredOncePerSave)
                {
                    int[] ordinals = records.Select(item => item.MapOrdinal)
                        .OrderBy(value => value).ToArray();
                    if (!ordinals.SequenceEqual(Enumerable.Range(0,
                            document.MapCount)))
                        return Unsupported(definition.ComponentType
                            + " must have one owner in every saved map");
                }
                else if (records.Any(item => item.MapOrdinal != -1))
                    return Unsupported(definition.ComponentType
                        + " has an unexpected map ordinal");
            }
            return CompatibleCardinality();
        }

        private static CACampaignCompatibilityDecision CompatibleCardinality()
        {
            return new CACampaignCompatibilityDecision(
                CACampaignCompatibilityKind.Current,
                "compatibility owner cardinality matches the saved world");
        }

        private static CACampaignCompatibilityDecision Unsupported(
            string reason)
        {
            return new CACampaignCompatibilityDecision(
                CACampaignCompatibilityKind.Unsupported, reason);
        }
    }
}
