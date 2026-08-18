using System;
using System.Collections.Generic;

namespace ColonistAwareness
{
    public enum CACampaignCompatibilityKind : byte
    {
        Current = 0,
        MigrateB10 = 1,
        AdditiveBootstrap = 2,
        Unsupported = 3
    }

    public readonly struct CACampaignCompatibilityDecision
    {
        public CACampaignCompatibilityDecision(
            CACampaignCompatibilityKind kind, string reason)
        {
            Kind = kind;
            Reason = reason;
        }

        public CACampaignCompatibilityKind Kind { get; }
        public string Reason { get; }
        public bool CanLoad => Kind != CACampaignCompatibilityKind.Unsupported;
    }

    public readonly struct CACampaignSchemaDefinition
    {
        public CACampaignSchemaDefinition(string key, int currentVersion,
            int minimumCompatibleVersion, int introducedCatalogVersion)
        {
            Key = key;
            CurrentVersion = currentVersion;
            MinimumCompatibleVersion = minimumCompatibleVersion;
            IntroducedCatalogVersion = introducedCatalogVersion;
        }

        public string Key { get; }
        public int CurrentVersion { get; }
        public int MinimumCompatibleVersion { get; }
        public int IntroducedCatalogVersion { get; }
    }

    // This catalog versions semantic state families rather than C# files. A
    // source reorganization does not change a schema; a persisted contract does.
    public static class CACampaignSchemaCatalog
    {
        public const int CurrentCatalogVersion = 3;

        public static readonly CACampaignSchemaDefinition[] All =
        {
            D("campaign.boundary", 1),
            D("world.act-ledger", 2, 1, 1),
            D("map.arrangements", 1),
            D("map.assault-awareness", 1),
            D("map.autonomous-home", 1),
            D("game.autonomy", 1),
            D("map.parley", 1),
            D("map.behavior-intent", 1),
            D("map.combat-aftermath", 1),
            D("game.combat-spatial-log", 1),
            D("game.combat-topology", 1),
            D("map.culture-longitudinal", 2, 1, 1),
            D("map.equipment-transition", 1),
            D("game.hidden-things", 1),
            D("world.faction-state", 2, 1, 1),
            D("world.player-founding", 2, 1, 1),
            D("world.organization", 2, 1, 1),
            D("world.organization-relations", 1),
            D("world.regional", 2, 1, 1),
            D("world.cultural-cognition", 2, 2, 2),
            D("world.political-cognition", 1, 1, 2),
            D("world.proposition-knowledge", 1, 1, 2),
            D("world.social-reactions", 1),
            D("world.transaction-ledger", 1),
            D("map.home-space-program", 1),
            D("map.home-prerequisite", 1),
            D("map.mission-triage", 1),
            D("game.offhand", 1),
            D("game.operational-access", 1),
            D("map.patrol", 1),
            D("map.raid-response", 1),
            D("map.contingency-plan", 1),
            D("map.road-expansion", 1),
            D("map.settlement-planning-context", 3, 2, 1),
            D("map.settlement-work", 1),
            D("map.spatial-initiative", 1),
            D("game.squad", 1),
            D("map.squad-support", 1),
            D("map.storage-program", 1),
            D("map.inventory-storage", 1),
            D("game.sustenance", 1),
            D("map.task-force", 1),
            D("map.toxic-waste", 1),
            D("map.trap-memory", 1),
            D("map.welfare-support", 1),
            D("map.welfare-knowledge", 1),
            D("map.withdrawal", 1),
            D("map.immediate-combat-recovery", 1),
            D("map.drafted-combat-initiative", 1),
            D("map.tactical-overlay", 1),
            D("pawn.political-belief-memory", 1),
            D("pawn.hygiene-needs", 1),
            D("thing.water-state", 1),
            D("scenario.established-player-settlement", 1),
            D("embedded.camping-state", 1),
            D("native.tactical-lord", 3),
            D("native.stack-lord", 1),
            D("native.regional-settlement-lord", 1),
            D("native.ca-job-drivers", 1),
            D("world.regional-reservation", 1),
            D("model.culture", 10, 9, 1),
            D("model.political-order", 10),
            D("model.represented-institutions", 1),
            D("model.founding-arrangement", 1),
            D("model.player-founding-plan", 3),
            D("model.regional-plan", 13),
            D("model.regional-settlement-record", 8),
            D("model.settlement-population-group", 1),
            D("model.domestic-unit", 1),
            D("model.domestic-provision-demand", 1),
            D("model.frontier-map-plan", 2),
            D("model.groundwater-tuning", 1),
            D("model.settlement-residence", 1),
            D("model.settlement-capability", 2),
            D("model.settlement-program", 4),
            D("model.settlement-program-entry", 4),
            D("model.settlement-operational-fact", 3),
            D("model.settlement-program-asset", 1),
            D("model.settlement-provision", 5),
            D("model.starting-stock", 1),
            D("model.organization-relation", 1),
            D("model.organization-holding", 1),
            D("model.settlement-layout", 1),
            D("model.settlement-research-work", 1),
            D("model.settlement-repair-work", 1),
            D("model.settlement-rebuild-work", 1)
        };

        public static bool TryFind(string key,
            out CACampaignSchemaDefinition definition)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (!string.Equals(All[i].Key, key,
                        StringComparison.Ordinal)) continue;
                definition = All[i];
                return true;
            }
            definition = default(CACampaignSchemaDefinition);
            return false;
        }

        public static bool ValidateDefinitions(out string failure)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < All.Length; i++)
            {
                CACampaignSchemaDefinition item = All[i];
                if (string.IsNullOrWhiteSpace(item.Key)
                    || item.CurrentVersion < 1
                    || item.MinimumCompatibleVersion < 1
                    || item.MinimumCompatibleVersion > item.CurrentVersion
                    || item.IntroducedCatalogVersion < 1
                    || item.IntroducedCatalogVersion > CurrentCatalogVersion)
                {
                    failure = "invalid campaign schema definition at index "
                        + i;
                    return false;
                }
                if (!keys.Add(item.Key))
                {
                    failure = "duplicate campaign schema key " + item.Key;
                    return false;
                }
            }
            failure = null;
            return true;
        }

        private static CACampaignSchemaDefinition D(string key, int version)
        {
            return new CACampaignSchemaDefinition(key, version, version, 1);
        }

        private static CACampaignSchemaDefinition D(string key,
            int currentVersion, int minimumCompatibleVersion,
            int introducedCatalogVersion)
        {
            return new CACampaignSchemaDefinition(key, currentVersion,
                minimumCompatibleVersion, introducedCatalogVersion);
        }
    }

    public static class CACampaignCompatibilityKernel
    {
        public const int CurrentBoundaryVersion = 1;
        public const int LegacyB10AuthoringEpoch = 10;

        public static CACampaignCompatibilityDecision EvaluateBoundary(
            int savedBoundaryVersion, int legacyAuthoringEpoch,
            bool containsCAState)
        {
            if (savedBoundaryVersion == CurrentBoundaryVersion)
                return new CACampaignCompatibilityDecision(
                    CACampaignCompatibilityKind.Current,
                    "current durable campaign boundary");
            if (savedBoundaryVersion != 0)
                return Unsupported("campaign boundary "
                    + savedBoundaryVersion + " is not supported by "
                    + CurrentBoundaryVersion);
            if (legacyAuthoringEpoch == LegacyB10AuthoringEpoch)
                return new CACampaignCompatibilityDecision(
                    CACampaignCompatibilityKind.MigrateB10,
                    "unversioned B10 state is eligible for one additive "
                    + "durable-boundary migration");
            if (!containsCAState && legacyAuthoringEpoch == 0)
                return new CACampaignCompatibilityDecision(
                    CACampaignCompatibilityKind.AdditiveBootstrap,
                    "no prior CA campaign state; initialize from current "
                    + "represented facts without prior history");
            return Unsupported("unversioned CA campaign state has no supported "
                + "B10 provenance");
        }

        public static CACampaignCompatibilityDecision EvaluateSchema(
            string key, int savedVersion)
        {
            if (!CACampaignSchemaCatalog.TryFind(key, out
                    CACampaignSchemaDefinition definition))
                return Unsupported("unknown campaign schema key " + key);
            if (savedVersion < definition.MinimumCompatibleVersion
                || savedVersion > definition.CurrentVersion)
                return Unsupported(key + " schema " + savedVersion
                    + " is outside supported range "
                    + definition.MinimumCompatibleVersion + "-"
                    + definition.CurrentVersion);
            return new CACampaignCompatibilityDecision(
                CACampaignCompatibilityKind.Current,
                key + " schema " + savedVersion + " is compatible");
        }

        private static CACampaignCompatibilityDecision Unsupported(
            string reason)
        {
            return new CACampaignCompatibilityDecision(
                CACampaignCompatibilityKind.Unsupported, reason);
        }
    }
}
