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
        private static readonly HashSet<string> CulturalQuestionKeys =
            new HashSet<string>(new[]
            {
                "relationships.sameSexAcceptance",
                "relationships.pluralityAcceptance",
                "relationships.kinObligation",
                "authority.genderDistribution",
                "authority.genderedWork",
                "authority.officeAccess",
                "status.hereditaryLegitimacy",
                "status.rankDifferentiation",
                "status.mobility",
                "groups.outsiderInclusion",
                "groups.integrationPreference",
                "groups.membershipAccess",
                "labor.coercionLegitimacy",
                "property.control",
                "voice.inclusionExpectation",
                "voice.dissentTolerance",
                "authority.enforcementLegitimacy",
                "war.captiveProtection",
                "war.punishmentSeverity",
                "war.retaliatoryViolence",
                "provision.mutualObligation",
                "knowledge.access",
                "knowledge.noveltyAcceptance",
                "knowledge.expertiseDeference"
            }, StringComparer.Ordinal);
        private static readonly HashSet<string> PoliticalAxisKeys =
            new HashSet<string>(new[]
            {
                "leadership", "decisions", "participation", "dissent",
                "ownership", "economy", "work", "support", "membership",
                "status", "localOrder", "defense", "warConduct"
            }, StringComparer.Ordinal);
        private static readonly HashSet<string> PsychologyConstructKeys =
            new HashSet<string>(new[]
            {
                "honesty-humility", "emotionality", "extraversion",
                "agreeableness", "conscientiousness", "openness",
                "psychological reactance", "need for closure",
                "empathic concern", "personal distress",
                "epistemic vigilance", "status seeking",
                "dangerous-world belief", "competitive-world belief",
                "domain risk tolerance", "source trust",
                "group identification"
            }, StringComparer.Ordinal);
        private static readonly IReadOnlyDictionary<string, string[]>
            PoliticalAxisOptions = new Dictionary<string, string[]>(
                StringComparer.Ordinal)
            {
                ["leadership"] = new[] { "single", "council", "whole",
                    "federated", "none" },
                ["decisions"] = new[] { "decree", "majority", "consensus",
                    "custom" },
                ["participation"] = new[] { "universal", "members",
                    "standing", "heads" },
                ["dissent"] = new[] { "plural", "majoritarian",
                    "orthodoxy", "customary" },
                ["ownership"] = new[] { "private", "cooperative", "common",
                    "state" },
                ["economy"] = new[] { "market", "planned", "communal" },
                ["work"] = new[] { "contract", "organized", "duty",
                    "household" },
                ["support"] = new[] { "private", "public", "communal",
                    "charitable" },
                ["membership"] = new[] { "open", "vetted", "hereditary",
                    "closed" },
                ["status"] = new[] { "equal", "earned", "hereditary",
                    "castes" },
                ["localOrder"] = new[] { "none", "watch", "constabulary",
                    "rulers" },
                ["defense"] = new[] { "none", "levy", "militia",
                    "professional", "caste" },
                ["warConduct"] = new[] { "quarter", "strength",
                    "combatants" }
            };

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
            N("model.political-order",
                "ColonistAwareness.CAFactionStateWorldComponent",
                "CA_factionStates/li/politicalBeliefs"),
            N("model.political-order",
                "ColonistAwareness.CAPlayerFoundingWorldComponent",
                "CA_playerFounding/politicalBeliefs"),
            N("model.political-order",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalPlans/li/factions/li/politicalBeliefs"),
            N("model.political-order",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalPlans/li/playerFounding/politicalBeliefs"),
            N("model.technological-knowledge",
                "ColonistAwareness.CAFactionStateWorldComponent",
                "CA_factionStates/li/technologicalKnowledge"),
            N("model.technological-knowledge",
                "ColonistAwareness.CAPlayerFoundingWorldComponent",
                "CA_playerFounding/technologicalKnowledge"),
            N("model.technological-knowledge",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalPlans/li/factions/li/technologicalKnowledge"),
            N("model.technological-knowledge",
                "ColonistAwareness.CARegionalWorldComponent",
                "CA_regionalPlans/li/playerFounding/technologicalKnowledge"),
            N("model.represented-institutions",
                "ColonistAwareness.CAFactionStateWorldComponent",
                "CA_factionStates/li/factionStructure"),
            N("model.represented-institutions", "ColonistAwareness.CARegionalWorldComponent",
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
            O("world.cultural-cognition",
                "ColonistAwareness.CACulturalCognitionWorldComponent",
                "CA_culturalCognitionOwnerVersion", true),
            O("world.political-cognition",
                "ColonistAwareness.CAPoliticalCognitionWorldComponent",
                "CA_politicalCognitionOwnerVersion", true),
            O("world.proposition-knowledge",
                "ColonistAwareness.CAPropositionKnowledgeWorldComponent",
                "CA_propositionKnowledgeOwnerVersion", true),
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
                "ColonistAwareness.CACulturalCognitionWorldComponent",
                K(), "CA_psychologicalProfiles", "CA_culturalAttitudes",
                "CA_socialInfluenceEdges"),
            P(CACampaignPayloadScope.WorldOnce,
                "ColonistAwareness.CAPoliticalCognitionWorldComponent",
                K(), "CA_pawnPoliticalAttitudes", "CA_politicalIssueLinks",
                "CA_politicalCoalitions"),
            P(CACampaignPayloadScope.WorldOnce,
                "ColonistAwareness.CAPropositionKnowledgeWorldComponent",
                K(), "CA_knowledgePropositions",
                "CA_researchProgramReceipts"),
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
            {
                ValidateIdContinuity(root, "CA_actRecords/li", "id",
                    "CA_actRecordNextId", failures);
                foreach (PayloadElementFrame record in FindFrames(root,
                             "CA_actRecords/li"))
                {
                    ValidateOptionalParallel(record, "knownByIds",
                        "knownSourceIds", failures);
                    ValidateOptionalParallel(record, "knownByIds",
                        "knownHow", failures);
                    ValidateOptionalParallel(record, "knownSourceHolderIds",
                        "knownSourcePawnIds", failures);
                    ValidateOptionalParallel(record, "knownSourcePawnIds",
                        "knownSourceHow", failures);
                    ValidateOptionalParallel(record,
                        "answeredSourceHolderIds", "answeredSourcePawnIds",
                        failures);
                }
            }
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
            else if (componentType ==
                "ColonistAwareness.CACulturalCognitionWorldComponent")
                ValidateCulturalCognitionNestedPayload(root, failures);
            else if (componentType ==
                "ColonistAwareness.CAPoliticalCognitionWorldComponent")
                ValidatePoliticalCognitionNestedPayload(root, failures);
            else if (componentType ==
                "ColonistAwareness.CAPropositionKnowledgeWorldComponent")
                ValidatePropositionKnowledgeNestedPayload(root, failures);
            else if (componentType ==
                "ColonistAwareness.CAOrganizationWorldComponent")
                ValidateOrganizationLegitimacyPayload(root, failures);
        }

        private static void ValidateOptionalParallel(PayloadElementFrame parent,
            string leftName, string rightName, List<string> failures)
        {
            PayloadElementFrame left = parent.Children.FirstOrDefault(value =>
                value.Name == leftName);
            PayloadElementFrame right = parent.Children.FirstOrDefault(value =>
                value.Name == rightName);
            if (left == null && right == null) return;
            if (left == null || right == null || IsNullValue(left)
                || IsNullValue(right))
            {
                failures.Add(parent.Path + " optional parallel collections "
                    + leftName + ", " + rightName
                    + " must occur together and be non-null");
                return;
            }
            if (left.ItemCount != right.ItemCount)
                failures.Add(parent.Path + " optional parallel collections "
                    + leftName + ", " + rightName + " differ in length");
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
                    int savedVersion = -1;
                    if (version == null || !int.TryParse(
                            version.Text.ToString(), out savedVersion))
                        failures.Add(binding.ParentPath + " "
                            + binding.SchemaKey
                            + " schemaVersion is missing or invalid");
                    else
                    {
                        CACampaignCompatibilityDecision decision =
                            CACampaignCompatibilityKernel.EvaluateSchema(
                                binding.SchemaKey, savedVersion);
                        if (!decision.CanLoad)
                            failures.Add(binding.ParentPath + " "
                                + decision.Reason);
                    }
                    if (binding.SchemaKey == "model.culture")
                    {
                        if (savedVersion >= 10)
                        {
                            ValidateRequiredChildren(frame,
                                K("constituents", "questionRegistryVersion",
                                    "withinGroupSpread",
                                    "subgroupSeparation",
                                    "inheritedQuestions", "localQuestions",
                                    "legacyEvidence", "transitions",
                                    "inheritedPractices", "practices",
                                    "observations"), K("lastEvidence"), K(),
                                failures);
                            ValidateCultureQuestionPayload(frame, failures);
                        }
                        else
                            ValidateRequiredChildren(frame,
                                K("constituents", "inheritedMeanings",
                                    "localMeanings", "transitions",
                                    "inheritedPractices", "practices",
                                    "observations"), K("lastEvidence"), K(),
                                failures);
                    }
                    else if (binding.SchemaKey ==
                        "model.political-order")
                        ValidateRequiredChildren(frame,
                            K("positions", "derivationReceipts", "questions"),
                            K("name", "nameAuthored", "nameRoll",
                                "generationRoll"), K(),
                            failures);
                }
            }
        }

        private static void ValidateCultureQuestionPayload(
            PayloadElementFrame culture, List<string> failures)
        {
            RequireInteger(culture, "questionRegistryVersion", 2, 2,
                failures);
            foreach (string collectionName in new[]
                { "inheritedQuestions", "localQuestions" })
            {
                PayloadElementFrame collection = culture.Children
                    .FirstOrDefault(value => value.Name == collectionName);
                if (collection == null || IsNullValue(collection)) continue;
                var identities = new HashSet<string>(StringComparer.Ordinal);
                foreach (PayloadElementFrame question in collection.Children
                    .Where(value => value.Name == "li"))
                {
                    ValidateRequiredChildren(question,
                        K("schemaVersion", "questionKey", "subgroups"), K(),
                        K(), failures);
                    RequireInteger(question, "schemaVersion", 1, 1,
                        failures);
                    string key = RequireText(question, "questionKey",
                        failures);
                    if (!string.IsNullOrEmpty(key)
                        && !CulturalQuestionKeys.Contains(key))
                        failures.Add(DisplayPath(question, "questionKey")
                            + " is not registered");
                    string scope = OptionalText(question, "populationScope")
                        ?? "*";
                    if (!identities.Add((key ?? "") + "\0" + scope))
                        failures.Add(question.Path
                            + " duplicates a question and population scope");
                    RequireFloat(question, "mean", -1f, 1f, 0f, failures);
                    RequireFloat(question, "descriptiveNormPrior", -1f, 1f,
                        0f, failures);
                    RequireFloat(question, "prestigeSignal", -1f, 1f, 0f,
                        failures);
                    RequireFloat(question, "spread", 0.06f, 1f, 0.28f,
                        failures);
                    RequireFloat(question, "salience", 0f, 1f, 0.55f,
                        failures);
                    RequireFloat(question, "normStrength", 0f, 1f, 0.50f,
                        failures);
                    RequireFloat(question, "visibility", 0f, 1f, 0.65f,
                        failures);
                    RequireFloat(question, "sourceConfidence", 0f, 1f,
                        0.60f, failures);
                    RequireFloat(question, "toleranceForDivergence", 0f, 1f,
                        0.50f, failures);
                    ValidateSubgroupMixture(question, failures);
                }
            }
        }

        private static void ValidateSubgroupMixture(
            PayloadElementFrame question, List<string> failures)
        {
            PayloadElementFrame subgroups = question.Children.FirstOrDefault(
                value => value.Name == "subgroups");
            if (subgroups == null || IsNullValue(subgroups)) return;
            int total = 0;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (PayloadElementFrame subgroup in subgroups.Children
                .Where(value => value.Name == "li"))
            {
                string key = RequireText(subgroup, "subgroupKey", failures);
                if (!string.IsNullOrEmpty(key) && !keys.Add(key))
                    failures.Add(subgroup.Path + " duplicates subgroup " + key);
                int share = RequireInteger(subgroup, "share", 0, 100,
                    failures);
                total += Math.Max(0, share);
                RequireFloat(subgroup, "meanOffset", -1f, 1f, 0f,
                    failures);
                RequireFloat(subgroup, "spreadMultiplier", 0.1f, 3f, 1f,
                    failures);
            }
            if (subgroups.ItemCount > 0 && total != 100)
                failures.Add(subgroups.Path + " shares total " + total
                    + ", expected 100");
        }

        private static void ValidateCulturalCognitionNestedPayload(
            PayloadElementFrame root, List<string> failures)
        {
            var profileIds = new HashSet<int>();
            foreach (PayloadElementFrame profile in FindFrames(root,
                "CA_psychologicalProfiles/li"))
            {
                ValidateRequiredChildren(profile,
                    K("schemaVersion", "pawnId", "mappingVersion",
                        "evidence", "constructUncertainties",
                        "dynamicState"),
                    K(), K(), failures);
                RequireInteger(profile, "schemaVersion", 1, 1, failures);
                RequireInteger(profile, "mappingVersion", 1, 1, failures);
                int pawnId = RequireInteger(profile, "pawnId", 1,
                    int.MaxValue, failures);
                if (pawnId > 0 && !profileIds.Add(pawnId))
                    failures.Add(profile.Path + " duplicates pawn " + pawnId);
                foreach (string field in new[]
                    {
                        "honestyHumility", "emotionality", "extraversion",
                        "agreeableness", "conscientiousness", "openness",
                        "reactance", "needForClosure", "empathicConcern",
                        "personalDistress", "epistemicVigilance",
                        "statusSeeking", "dangerousWorldBelief",
                        "competitiveWorldBelief", "domainRiskTolerance",
                        "sourceTrust", "groupIdentification"
                    })
                    RequireFloat(profile, field, 0f, 1f, 0.5f, failures);
                RequireFloat(profile, "uncertainty", 0f, 1f, 0.35f,
                    failures);
                PayloadElementFrame constructUncertainties = profile.Children
                    .FirstOrDefault(value => value.Name
                        == "constructUncertainties");
                if (constructUncertainties == null
                    || IsNullValue(constructUncertainties))
                    failures.Add(DisplayPath(profile,
                        "constructUncertainties") + " is missing or null");
                else
                {
                    var constructs = new HashSet<string>(
                        StringComparer.Ordinal);
                    foreach (PayloadElementFrame construct in
                        constructUncertainties.Children.Where(value =>
                            value.Name == "li"))
                    {
                        string key = RequireText(construct, "construct",
                            failures);
                        if (!string.IsNullOrEmpty(key)
                            && !PsychologyConstructKeys.Contains(key))
                            failures.Add(construct.Path
                                + " names unknown construct " + key);
                        else if (!string.IsNullOrEmpty(key)
                            && !constructs.Add(key))
                            failures.Add(construct.Path
                                + " duplicates construct " + key);
                        RequireFloat(construct, "uncertainty", 0f, 1f,
                            0.35f, failures);
                    }
                    if (!constructs.SetEquals(PsychologyConstructKeys))
                        failures.Add(constructUncertainties.Path
                            + " does not contain the complete construct set");
                }
                PayloadElementFrame evidence = profile.Children
                    .FirstOrDefault(value => value.Name == "evidence");
                if (evidence == null || IsNullValue(evidence)
                    || evidence.ItemCount == 0)
                    failures.Add(DisplayPath(profile, "evidence")
                        + " must contain at least one mapping record");
                else
                    foreach (PayloadElementFrame record in evidence.Children
                        .Where(value => value.Name == "li"))
                    {
                        RequireText(record, "sourceFeature", failures);
                        RequireText(record, "construct", failures);
                        RequireText(record, "scope", failures);
                        RequireText(record, "provenance", failures);
                        RequireFinite(record, "meanShift", 0f, failures);
                        RequireFinite(record, "uncertaintyChange", 0f,
                            failures);
                    }
                PayloadElementFrame dynamic = profile.Children
                    .FirstOrDefault(value => value.Name == "dynamicState");
                if (dynamic != null && !IsNullValue(dynamic))
                    foreach (string field in new[]
                        {
                            "mood", "pain", "fatigue", "fear", "anger",
                            "grief", "scarcityPerception", "personalThreat",
                            "groupThreat", "recentSuccess", "humiliation",
                            "cognitiveLoad"
                        })
                        RequireFloat(dynamic, field, 0f, 1f,
                            field == "mood" ? 0.5f : 0f, failures);
            }

            var attitudeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PayloadElementFrame attitude in FindFrames(root,
                "CA_culturalAttitudes/li"))
            {
                ValidateRequiredChildren(attitude,
                    K("schemaVersion", "pawnId", "cultureId", "subgroupId",
                        "questionKey", "privateAttitude", "attention",
                        "moralConviction", "identityCentrality",
                        "inheritedPriorStrength", "knowledgeConfidence",
                        "perceivedDescriptiveNorm",
                        "perceivedInjunctiveNorm",
                        "perceivedSocialPressure", "expectedEnforcement",
                        "publicExpression", "observationLikelihood",
                        "prestigeSignal", "uncertainty"), K(), K(), failures);
                RequireInteger(attitude, "schemaVersion", 2, 2, failures);
                int pawnId = RequireInteger(attitude, "pawnId", 1,
                    int.MaxValue, failures);
                string culture = RequireText(attitude, "cultureId", failures);
                string subgroup = RequireText(attitude, "subgroupId", failures);
                string question = RequireText(attitude, "questionKey",
                    failures);
                if (!string.IsNullOrEmpty(question)
                    && !CulturalQuestionKeys.Contains(question))
                    failures.Add(DisplayPath(attitude, "questionKey")
                        + " is not registered");
                string identity = pawnId + "\0" + culture + "\0" + subgroup
                    + "\0" + question;
                if (!attitudeIds.Add(identity))
                    failures.Add(attitude.Path + " duplicates attitude "
                        + identity.Replace('\0', '/'));
                foreach (string field in new[]
                    {
                        "privateAttitude", "perceivedDescriptiveNorm",
                        "perceivedInjunctiveNorm", "publicExpression",
                        "prestigeSignal"
                    })
                    RequireFloat(attitude, field, -1f, 1f, 0f, failures);
                foreach (string field in new[]
                    {
                        "attention", "moralConviction",
                        "identityCentrality", "inheritedPriorStrength",
                        "knowledgeConfidence", "perceivedSocialPressure",
                        "expectedEnforcement", "observationLikelihood",
                        "uncertainty"
                    })
                    RequireFloat(attitude, field, 0f, 1f, 0f, failures);
            }

            var edgeIds = new HashSet<string>(StringComparer.Ordinal);
            var inboundEdges = new Dictionary<int, int>();
            foreach (PayloadElementFrame edge in FindFrames(root,
                "CA_socialInfluenceEdges/li"))
            {
                int source = RequireInteger(edge, "sourcePawnId", 1,
                    int.MaxValue, failures);
                int target = RequireInteger(edge, "targetPawnId", 1,
                    int.MaxValue, failures);
                if (target > 0)
                {
                    inboundEdges.TryGetValue(target, out int inbound);
                    inboundEdges[target] = inbound + 1;
                }
                if (source == target && source > 0)
                    failures.Add(edge.Path + " has a self influence edge");
                RequireText(edge, "edgeType", failures);
                ValidateRequiredChildren(edge, K("exposures",
                    "lastContactTick"),
                    K(), K(), failures);
                RequireInteger(edge, "lastContactTick", -1,
                    int.MaxValue, failures);
                PayloadElementFrame exposures = edge.Children.FirstOrDefault(
                    value => value.Name == "exposures");
                if (exposures != null && !IsNullValue(exposures))
                {
                    var observedSubjects = new HashSet<string>(
                        StringComparer.Ordinal);
                    PayloadElementFrame[] items = exposures.Children
                        .Where(value => value.Name == "li")
                        .ToArray();
                    if (items.Length > CACultureQuestionRegistry.All.Count
                            + PoliticalAxisOptions.Count)
                        failures.Add(exposures.Path
                            + " exceeds the exposure-subject bound");
                    foreach (PayloadElementFrame item in items)
                    {
                        ValidateRequiredChildren(item,
                            K("subjectKey", "lastObservedTick"), K(), K(),
                            failures);
                        string key = RequireText(item, "subjectKey",
                            failures);
                        RequireInteger(item, "lastObservedTick", -1,
                            int.MaxValue, failures);
                        bool political = key?.StartsWith("politics:",
                                StringComparison.Ordinal) == true
                            && PoliticalAxisOptions.ContainsKey(key.Substring(
                                "politics:".Length));
                        if (CACultureQuestionRegistry.Find(key) == null
                            && !political)
                            failures.Add(item.Path
                                + " is not a registered influence subject");
                        else if (!observedSubjects.Add(key))
                            failures.Add(item.Path
                                + " duplicates observed subject " + key);
                    }
                }
                if (!edgeIds.Add(source + "\0" + target))
                    failures.Add(edge.Path + " duplicates influence edge "
                        + source + "/" + target);
                foreach (string field in new[]
                    { "weight", "trust", "prestige", "conformity",
                        "payoffVisibility" })
                    RequireFloat(edge, field, 0f, 1f, 0f, failures);
            }
            foreach (KeyValuePair<int, int> inbound in inboundEdges.Where(
                value => value.Value > CACulturalCognitionPureKernel
                    .MaxInfluenceEdgesPerPawn))
                failures.Add("pawn " + inbound.Key + " has "
                    + inbound.Value + " inbound influence edges; maximum is "
                    + CACulturalCognitionPureKernel
                        .MaxInfluenceEdgesPerPawn);

        }

        private static void ValidatePoliticalCognitionNestedPayload(
            PayloadElementFrame root, List<string> failures)
        {
            var attitudeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PayloadElementFrame attitude in FindFrames(root,
                "CA_pawnPoliticalAttitudes/li"))
            {
                int pawnId = RequireInteger(attitude, "pawnId", 1,
                    int.MaxValue, failures);
                string axisKey = RequireText(attitude, "axisKey", failures);
                if (!string.IsNullOrEmpty(axisKey)
                    && !PoliticalAxisKeys.Contains(axisKey))
                    failures.Add(DisplayPath(attitude, "axisKey")
                        + " is not registered");
                PayloadElementFrame options = attitude.Children
                    .FirstOrDefault(value => value.Name == "options");
                if (options == null || IsNullValue(options))
                    failures.Add(DisplayPath(attitude, "options") + " is null");
                else
                {
                    var optionKeys = new HashSet<string>(
                        StringComparer.Ordinal);
                    foreach (PayloadElementFrame option in options.Children
                        .Where(value => value.Name == "li"))
                    {
                    string optionKey = RequireText(option, "optionKey",
                        failures);
                    if (!string.IsNullOrEmpty(optionKey)
                        && !optionKeys.Add(optionKey))
                        failures.Add(option.Path + " duplicates option "
                            + optionKey);
                    RequireFloat(option, "support", -1f, 1f, 0f, failures);
                    }
                    if (PoliticalAxisOptions.TryGetValue(axisKey,
                            out string[] expected)
                        && !optionKeys.SetEquals(expected))
                        failures.Add(options.Path
                            + " does not contain the canonical options for "
                            + axisKey);
                }
                ValidateRequiredChildren(attitude, K("evidenceHistory"),
                    K(), K(), failures);
                PayloadElementFrame history = attitude.Children
                    .FirstOrDefault(value => value.Name == "evidenceHistory");
                if (history != null && history.ItemCount
                    > CACulturalCognitionPureKernel
                        .MaxPoliticalEvidenceHistory)
                    failures.Add(history.Path + " contains "
                        + history.ItemCount + " records; maximum is "
                        + CACulturalCognitionPureKernel
                            .MaxPoliticalEvidenceHistory);
                ValidateNonNullValues(attitude, "evidenceHistory", failures);
                foreach (string field in new[]
                    {
                        "salience", "confidence", "moralConviction",
                        "identityCentrality"
                    })
                    RequireFloat(attitude, field, 0f, 1f, 0f, failures);
                foreach (string field in new[]
                    {
                        "perceivedMajority", "publicExpression",
                        "materialInterest"
                    })
                    RequireFloat(attitude, field, -1f, 1f, 0f, failures);
                string identity = pawnId + "\0" + axisKey;
                if (!attitudeIds.Add(identity))
                    failures.Add(attitude.Path
                        + " duplicates political attitude "
                        + identity.Replace('\0', '/'));
            }

            var issueLinkIds = new HashSet<string>(StringComparer.Ordinal);
            var issueLinksPerFaction = new Dictionary<string, int>(
                StringComparer.Ordinal);
            foreach (PayloadElementFrame link in FindFrames(root,
                "CA_politicalIssueLinks/li"))
            {
                string faction = RequireText(link, "factionBoundary",
                    failures);
                if (!string.IsNullOrEmpty(faction))
                {
                    issueLinksPerFaction.TryGetValue(faction,
                        out int linkCount);
                    issueLinksPerFaction[faction] = linkCount + 1;
                }
                string left = RequireText(link, "leftIssueKey", failures);
                string right = RequireText(link, "rightIssueKey", failures);
                if (!string.IsNullOrEmpty(left)
                    && !CanonicalPoliticalIssueKey(left))
                    failures.Add(DisplayPath(link, "leftIssueKey")
                        + " is not a canonical political issue");
                if (!string.IsNullOrEmpty(right)
                    && !CanonicalPoliticalIssueKey(right))
                    failures.Add(DisplayPath(link, "rightIssueKey")
                        + " is not a canonical political issue");
                if (!string.IsNullOrEmpty(left)
                    && !string.IsNullOrEmpty(right)
                    && string.CompareOrdinal(left, right) >= 0)
                    failures.Add(link.Path
                        + " has a noncanonical issue-key order");
                RequireFloat(link, "learnedCorrelation", -1f, 1f, 0f,
                    failures);
                RequireFloat(link, "identityAttachment", 0f, 1f, 0f,
                    failures);
                RequireFloat(link, "constraint", 0f, 1f, 0f, failures);
                RequireInteger(link, "observations",
                    CACulturalCognitionPureKernel
                        .MinPoliticalIssueLinkObservations,
                    int.MaxValue, failures);
                RequireText(link, "evidenceSignature", failures);
                string identity = faction + "\0" + left + "\0" + right;
                if (!issueLinkIds.Add(identity))
                    failures.Add(link.Path + " duplicates political issue link "
                        + identity.Replace('\0', '/'));
            }
            foreach (KeyValuePair<string, int> count in issueLinksPerFaction
                .Where(value => value.Value
                    > CACulturalCognitionPureKernel
                        .MaxPoliticalIssueLinksPerFaction))
                failures.Add("political issue links for " + count.Key
                    + " contain " + count.Value + " records; maximum is "
                    + CACulturalCognitionPureKernel
                        .MaxPoliticalIssueLinksPerFaction);

            var coalitionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PayloadElementFrame coalition in FindFrames(root,
                "CA_politicalCoalitions/li"))
            {
                string identity = RequireText(coalition, "identity", failures);
                RequireText(coalition, "factionBoundary", failures);
                ValidateRequiredChildren(coalition,
                    K("memberPawnIds", "issueKeys", "grievanceKeys",
                        "institutionalGoals"), K(), K(), failures);
                ValidateIntegerValues(coalition, "memberPawnIds", 0,
                    int.MaxValue, unique: true, failures);
                ValidateNonemptyValues(coalition, "issueKeys", failures);
                ValidatePoliticalIssueValues(coalition, "issueKeys",
                    failures);
                if (!string.IsNullOrEmpty(identity)
                    && !coalitionIds.Add(identity))
                    failures.Add(coalition.Path + " duplicates coalition "
                        + identity);
                RequireInteger(coalition, "leaderPawnId", -1, int.MaxValue,
                    failures);
                foreach (string field in new[]
                    { "perceivedEfficacy", "cohesion", "publicSupport" })
                    RequireFloat(coalition, field, 0f, 1f, 0f, failures);
            }
        }

        private static void ValidatePropositionKnowledgeNestedPayload(
            PayloadElementFrame root, List<string> failures)
        {
            var propositionIds = new HashSet<string>(StringComparer.Ordinal);
            PayloadElementFrame[] propositions = FindFrames(root,
                "CA_knowledgePropositions/li").ToArray();
            if (propositions.Length
                > CACulturalCognitionPureKernel.MaxKnowledgePropositions)
                failures.Add("CA_knowledgePropositions contains "
                    + propositions.Length + " records; maximum is "
                    + CACulturalCognitionPureKernel.MaxKnowledgePropositions);
            var topicCounts = new Dictionary<string, int>(
                StringComparer.Ordinal);
            foreach (PayloadElementFrame proposition in propositions)
            {
                string identity = RequireText(proposition, "identity",
                    failures);
                string topic = RequireText(proposition, "topic", failures);
                if (!string.IsNullOrEmpty(topic))
                {
                    topicCounts.TryGetValue(topic, out int topicCount);
                    topicCounts[topic] = topicCount + 1;
                }
                RequireText(proposition, "claim", failures);
                RequireText(proposition, "holderIdentity", failures);
                RequireText(proposition, "sourceIdentity", failures);
                RequireText(proposition, "sourceType", failures);
                RequireText(proposition, "acquisitionChannel", failures);
                ValidateRequiredChildren(proposition,
                    K("provenanceChain", "evidence", "contradictions",
                        "corroboratingSources"),
                    K(), K(), failures);
                ValidateNonNullValues(proposition, "provenanceChain",
                    failures);
                ValidateNonNullValues(proposition, "evidence", failures);
                ValidateNonNullValues(proposition, "contradictions",
                    failures);
                ValidateNonemptyValues(proposition, "corroboratingSources",
                    failures);
                ValidateCollectionMaximum(proposition, "provenanceChain",
                    CACulturalCognitionPureKernel.MaxKnowledgeProvenance,
                    failures);
                ValidateCollectionMaximum(proposition, "evidence",
                    CACulturalCognitionPureKernel.MaxKnowledgeEvidence,
                    failures);
                ValidateCollectionMaximum(proposition, "contradictions",
                    CACulturalCognitionPureKernel.MaxKnowledgeContradictions,
                    failures);
                ValidateCollectionMaximum(proposition,
                    "corroboratingSources",
                    CACulturalCognitionPureKernel.MaxKnowledgeProvenance,
                    failures);
                if (!string.IsNullOrEmpty(identity)
                    && !propositionIds.Add(identity))
                    failures.Add(proposition.Path
                        + " duplicates knowledge proposition " + identity);
                foreach (string field in new[]
                    {
                        "confidence", "transmissibility",
                        "sourceReliability", "sourceTrust", "expertise",
                        "prestige", "authority",
                        "motiveIntegrity", "corroboration", "plausibility",
                        "priorCongruence", "methodQuality",
                        "observedPayoff", "uncontestedConflictFreedom",
                        "conflictFreedom", "epistemicVigilance"
                    })
                    RequireFloat(proposition, field, 0f, 1f,
                        field == "confidence" || field == "transmissibility"
                            ? 0f : 0.5f, failures);
                RequireFloat(proposition, "noveltyAcceptance", -1f, 1f,
                    0f, failures);
                string politicalAxis = OptionalText(proposition,
                    "politicalAxisKey");
                string politicalOption = OptionalText(proposition,
                    "politicalOptionKey");
                float politicalSupport = RequireFloat(proposition,
                    "politicalSupport", -1f, 1f, 0f, failures);
                bool noPoliticalBinding = string.IsNullOrEmpty(politicalAxis)
                    && string.IsNullOrEmpty(politicalOption)
                    && Math.Abs(politicalSupport) < 0.0001f;
                if (!noPoliticalBinding
                    && (!PoliticalAxisOptions.TryGetValue(politicalAxis,
                            out string[] politicalOptions)
                        || !politicalOptions.Contains(politicalOption,
                            StringComparer.Ordinal)))
                    failures.Add(proposition.Path
                        + " has an incomplete or noncanonical political binding");
                RequireFloat(proposition, "decayRate", 0f, float.MaxValue,
                    0f, failures);
            }
            foreach (KeyValuePair<string, int> topic in topicCounts.Where(
                value => value.Value
                    > CACulturalCognitionPureKernel.MaxKnowledgePerTopic))
                failures.Add("CA_knowledgePropositions topic " + topic.Key
                    + " contains " + topic.Value + " records; maximum is "
                    + CACulturalCognitionPureKernel.MaxKnowledgePerTopic);

            var researchIds = new HashSet<string>(StringComparer.Ordinal);
            PayloadElementFrame[] research = FindFrames(root,
                "CA_researchProgramReceipts/li").ToArray();
            if (research.Length
                > CACulturalCognitionPureKernel.MaxResearchPrograms)
                failures.Add("CA_researchProgramReceipts contains "
                    + research.Length + " records; maximum is "
                    + CACulturalCognitionPureKernel.MaxResearchPrograms);
            foreach (PayloadElementFrame receipt in research)
            {
                string identity = RequireText(receipt, "identity", failures);
                RequireText(receipt, "projectKey", failures);
                RequireText(receipt, "institutionIdentity", failures);
                RequireText(receipt, "authorityBasis", failures);
                RequireText(receipt, "method", failures);
                RequireText(receipt, "facilityIdentity", failures);
                RequireText(receipt, "materialBasis", failures);
                RequireText(receipt, "preservation", failures);
                RequireText(receipt, "dissemination", failures);
                ValidateRequiredChildren(receipt,
                    K("priorKnowledgeKeys", "skilledPawnIds", "evidenceKeys"),
                    K(), K(), failures);
                ValidateNonNullValues(receipt, "priorKnowledgeKeys",
                    failures);
                ValidateIntegerValues(receipt, "skilledPawnIds", 0,
                    int.MaxValue, unique: false, failures);
                ValidateNonNullValues(receipt, "evidenceKeys", failures);
                ValidateCollectionMaximum(receipt, "priorKnowledgeKeys",
                    CACulturalCognitionPureKernel.MaxResearchPriorKnowledge,
                    failures);
                ValidateCollectionMaximum(receipt, "skilledPawnIds",
                    CACulturalCognitionPureKernel.MaxResearchSkilledPawns,
                    failures);
                ValidateCollectionMaximum(receipt, "evidenceKeys",
                    CACulturalCognitionPureKernel.MaxResearchEvidence,
                    failures);
                RequireInteger(receipt, "collaborationCount", 0,
                    int.MaxValue, failures);
                if (!string.IsNullOrEmpty(identity)
                    && !researchIds.Add(identity))
                    failures.Add(receipt.Path
                        + " duplicates research receipt " + identity);
            }
        }

        private static void ValidateOrganizationLegitimacyPayload(
            PayloadElementFrame root, List<string> failures)
        {
            PayloadElementFrame ownerVersion = root.Children.FirstOrDefault(
                value => value.Name == "CA_organizationSchemaVersion");
            int version = ownerVersion != null && int.TryParse(
                ownerVersion.Text.ToString(), out int parsed) ? parsed : 0;
            foreach (PayloadElementFrame organization in FindFrames(root,
                "CA_organizations/li"))
            {
                PayloadElementFrame appraisals = organization.Children
                    .FirstOrDefault(value => value.Name
                        == "legitimacyAppraisals");
                if (version >= 2 && (appraisals == null
                    || IsNullValue(appraisals)))
                {
                    failures.Add(DisplayPath(organization,
                        "legitimacyAppraisals") + " is missing or null");
                    continue;
                }
                if (appraisals == null || IsNullValue(appraisals)) continue;
                ValidateCollectionMaximum(organization,
                    "legitimacyAppraisals",
                    CACulturalCognitionPureKernel
                        .MaxInstitutionLegitimacyAppraisals, failures);
                var scopes = new HashSet<string>(StringComparer.Ordinal);
                foreach (PayloadElementFrame appraisal in appraisals.Children
                    .Where(value => value.Name == "li"))
                {
                    string scope = RequireText(appraisal, "populationScope",
                        failures);
                    if (!string.IsNullOrEmpty(scope) && !scopes.Add(scope))
                        failures.Add(appraisal.Path
                            + " duplicates legitimacy scope " + scope);
                    foreach (string field in new[]
                        {
                            "proceduralFairness", "outcomePerformance",
                            "lawAndCustomFit", "identityRepresentation",
                            "competence", "corruption", "coercion",
                            "culturalFit", "ideoligionFit", "politicalFit",
                            "personalTreatment", "trust",
                            "evidenceConfidence", "reportedPublicSupport",
                            "outstandingSupportLoss", "legitimacy"
                        })
                        RequireFloat(appraisal, field, 0f, 1f,
                            field == "reportedPublicSupport"
                                || field == "outstandingSupportLoss"
                                || field == "legitimacy"
                                || field == "evidenceConfidence"
                                    ? 0f : 0.5f, failures);
                }

                PayloadElementFrame sanctions = organization.Children
                    .FirstOrDefault(value => value.Name
                        == "sanctionAppraisals");
                if (version >= 2 && (sanctions == null
                    || IsNullValue(sanctions)))
                {
                    failures.Add(DisplayPath(organization,
                        "sanctionAppraisals") + " is missing or null");
                    continue;
                }
                if (sanctions == null || IsNullValue(sanctions)) continue;
                ValidateCollectionMaximum(organization,
                    "sanctionAppraisals",
                    CACulturalCognitionPureKernel
                        .MaxInstitutionSanctionAppraisals, failures);
                var sanctionIds = new HashSet<string>(
                    StringComparer.Ordinal);
                foreach (PayloadElementFrame sanction in sanctions.Children
                    .Where(value => value.Name == "li"))
                {
                    string fact = RequireText(sanction, "factIdentity",
                        failures);
                    RequireText(sanction, "subjectKey", failures);
                    int pawnId = RequireInteger(sanction, "pawnId", 1,
                        int.MaxValue, failures);
                    RequireText(sanction, "populationScope", failures);
                    string identity = fact + "\0" + pawnId;
                    if (!sanctionIds.Add(identity))
                        failures.Add(sanction.Path
                            + " duplicates sanction appraisal "
                            + identity.Replace('\0', '/'));
                    foreach (string field in new[]
                        {
                            "legitimacy", "proportionality", "visibility",
                            "consistency", "procedure", "socialSupport",
                            "intrinsicMotivation", "psychologicalReactance",
                            "deterrence", "normReinforcement", "reactance",
                            "voluntaryCooperation"
                        })
                        RequireFloat(sanction, field, 0f, 1f, 0f,
                            failures);
                }
            }
        }

        private static string OptionalText(PayloadElementFrame parent,
            string name)
        {
            PayloadElementFrame field = parent.Children.FirstOrDefault(value =>
                value.Name == name);
            return field == null || IsNullValue(field)
                ? null : field.Text.ToString().Trim();
        }

        private static string RequireText(PayloadElementFrame parent,
            string name, List<string> failures)
        {
            string value = OptionalText(parent, name);
            if (!string.IsNullOrWhiteSpace(value)) return value;
            failures.Add(DisplayPath(parent, name) + " is missing or empty");
            return null;
        }

        private static int RequireInteger(PayloadElementFrame parent,
            string name, int minimum, int maximum, List<string> failures)
        {
            string text = OptionalText(parent, name);
            if (text != null && int.TryParse(text, out int value)
                && value >= minimum && value <= maximum) return value;
            failures.Add(DisplayPath(parent, name) + " must be between "
                + minimum + " and " + maximum);
            return 0;
        }

        private static float RequireFloat(PayloadElementFrame parent,
            string name, float minimum, float maximum, float defaultValue,
            List<string> failures)
        {
            string text = OptionalText(parent, name);
            if (text == null) return defaultValue;
            if (float.TryParse(text,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float value) && !float.IsNaN(value)
                && !float.IsInfinity(value) && value >= minimum
                && value <= maximum) return value;
            failures.Add(DisplayPath(parent, name) + " must be between "
                + minimum + " and " + maximum);
            return defaultValue;
        }

        private static float RequireFinite(PayloadElementFrame parent,
            string name, float defaultValue, List<string> failures)
        {
            string text = OptionalText(parent, name);
            if (text == null) return defaultValue;
            if (float.TryParse(text,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float value) && !float.IsNaN(value)
                && !float.IsInfinity(value)) return value;
            failures.Add(DisplayPath(parent, name)
                + " must be a finite number");
            return defaultValue;
        }

        private static void ValidateNonNullValues(PayloadElementFrame parent,
            string collectionName, List<string> failures)
        {
            PayloadElementFrame collection = parent.Children.FirstOrDefault(
                value => value.Name == collectionName);
            if (collection == null || IsNullValue(collection)) return;
            foreach (PayloadElementFrame item in collection.Children.Where(
                value => value.Name == "li"))
                if (IsNullValue(item)) failures.Add(item.Path
                    + " contains a null value");
        }

        private static void ValidateCollectionMaximum(
            PayloadElementFrame parent, string collectionName, int maximum,
            List<string> failures)
        {
            PayloadElementFrame collection = parent.Children.FirstOrDefault(
                value => value.Name == collectionName);
            if (collection == null || IsNullValue(collection)) return;
            if (collection.ItemCount > maximum)
                failures.Add(collection.Path + " contains "
                    + collection.ItemCount + " records; maximum is "
                    + maximum);
        }

        private static bool CanonicalPoliticalIssueKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            int split = value.IndexOf(':');
            if (split <= 0 || split >= value.Length - 1) return false;
            string axis = value.Substring(0, split);
            string option = value.Substring(split + 1);
            return PoliticalAxisOptions.TryGetValue(axis,
                    out string[] expected)
                && expected.Contains(option, StringComparer.Ordinal);
        }

        private static void ValidatePoliticalIssueValues(
            PayloadElementFrame parent, string collectionName,
            List<string> failures)
        {
            PayloadElementFrame collection = parent.Children.FirstOrDefault(
                value => value.Name == collectionName);
            if (collection == null || IsNullValue(collection)) return;
            foreach (PayloadElementFrame item in collection.Children.Where(
                value => value.Name == "li"))
            {
                string issue = item.Text.ToString().Trim();
                if (!CanonicalPoliticalIssueKey(issue))
                    failures.Add(item.Path
                        + " is not a canonical political issue");
            }
        }

        private static void ValidateNonemptyValues(
            PayloadElementFrame parent, string collectionName,
            List<string> failures)
        {
            PayloadElementFrame collection = parent.Children.FirstOrDefault(
                value => value.Name == collectionName);
            if (collection == null || IsNullValue(collection)) return;
            foreach (PayloadElementFrame item in collection.Children.Where(
                value => value.Name == "li"))
                if (IsNullValue(item)
                    || string.IsNullOrWhiteSpace(item.Text.ToString()))
                    failures.Add(item.Path + " contains an empty value");
        }

        private static void ValidateIntegerValues(PayloadElementFrame parent,
            string collectionName, int minimum, int maximum, bool unique,
            List<string> failures)
        {
            PayloadElementFrame collection = parent.Children.FirstOrDefault(
                value => value.Name == collectionName);
            if (collection == null || IsNullValue(collection)) return;
            var seen = new HashSet<int>();
            foreach (PayloadElementFrame item in collection.Children.Where(
                value => value.Name == "li"))
            {
                string text = IsNullValue(item)
                    ? null : item.Text.ToString().Trim();
                if (text == null || !int.TryParse(text, out int value)
                    || value < minimum || value > maximum)
                {
                    failures.Add(item.Path + " must be between " + minimum
                        + " and " + maximum);
                    continue;
                }
                if (unique && !seen.Add(value))
                    failures.Add(item.Path + " duplicates value " + value);
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
                if (ComponentIntroducedAfter(required.ComponentType,
                        document.CatalogVersion)) continue;
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
            bool catalogUpgrade = document.CatalogVersion
                < CACampaignSchemaCatalog.CurrentCatalogVersion;
            return new CACampaignCompatibilityDecision(
                catalogUpgrade
                    ? CACampaignCompatibilityKind.AdditiveBootstrap
                    : CACampaignCompatibilityKind.Current,
                catalogUpgrade
                    ? "compatible earlier campaign catalog; initialize newly "
                        + "introduced owners from current represented facts"
                    : "current durable campaign boundary, catalog, manifest, "
                        + "and complete digested owner payload are compatible");
        }

        public static CACampaignCompatibilityDecision ValidatePayloads(
            CACampaignPreflightDocument document)
        {
            for (int i = 0;
                i < CACampaignPreflightReader.PayloadDefinitions.Length; i++)
            {
                CACampaignPayloadDefinition definition =
                    CACampaignPreflightReader.PayloadDefinitions[i];
                if (ComponentIntroducedAfter(definition.ComponentType,
                        document.CatalogVersion)) continue;
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
                if (ComponentIntroducedAfter(definition.ComponentType,
                        document.CatalogVersion)) continue;
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

        private static bool ComponentIntroducedAfter(string componentType,
            int savedCatalogVersion)
        {
            CACampaignOwnerVersionDefinition owner =
                CACampaignPreflightReader.OwnerVersionDefinitions
                    .FirstOrDefault(item => string.Equals(item.ComponentType,
                        componentType, StringComparison.Ordinal));
            if (owner.ComponentType == null
                || !CACampaignSchemaCatalog.TryFind(owner.SchemaKey,
                    out CACampaignSchemaDefinition schema))
                return false;
            return schema.IntroducedCatalogVersion
                > Math.Max(1, savedCatalogVersion);
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
