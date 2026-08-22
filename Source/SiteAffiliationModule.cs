using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // A site may be owned or supported by a faction, or by neither. These are
    // independent relationships. Keys and load ids are payloads selected by
    // the typed relationship; they are never themselves the relationship.
    public sealed class CASiteFactionLinks : IExposable
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public CASiteFactionReferenceKind ownership =
            CASiteFactionReferenceKind.None;
        public int ownerRegionalFactionKey = -1;
        public int ownerWorldFactionLoadId = -1;
        public CASiteFactionReferenceKind support =
            CASiteFactionReferenceKind.None;
        public int supportRegionalFactionKey = -1;
        public int supportWorldFactionLoadId = -1;

        public void ExposeData()
        {
            // Zero is never a written version, so the stamp always
            // serializes; a default equal to the live value would omit it
            // and fail the preflight's schema binding.
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref ownership, "ownership",
                CASiteFactionReferenceKind.None);
            Scribe_Values.Look(ref ownerRegionalFactionKey,
                "ownerRegionalFactionKey", -1);
            Scribe_Values.Look(ref ownerWorldFactionLoadId,
                "ownerWorldFactionLoadId", -1);
            Scribe_Values.Look(ref support, "support",
                CASiteFactionReferenceKind.None);
            Scribe_Values.Look(ref supportRegionalFactionKey,
                "supportRegionalFactionKey", -1);
            Scribe_Values.Look(ref supportWorldFactionLoadId,
                "supportWorldFactionLoadId", -1);
        }

        internal CASiteFactionLinks Copy()
        {
            return new CASiteFactionLinks
            {
                schemaVersion = schemaVersion,
                ownership = ownership,
                ownerRegionalFactionKey = ownerRegionalFactionKey,
                ownerWorldFactionLoadId = ownerWorldFactionLoadId,
                support = support,
                supportRegionalFactionKey = supportRegionalFactionKey,
                supportWorldFactionLoadId = supportWorldFactionLoadId
            };
        }

        internal void SetNoOwner()
        {
            ownership = CASiteFactionReferenceKind.None;
            ownerRegionalFactionKey = -1;
            ownerWorldFactionLoadId = -1;
        }

        internal void SetRegionalOwner(int key)
        {
            ownership = CASiteFactionReferenceKind.RegionalFaction;
            ownerRegionalFactionKey = key;
            ownerWorldFactionLoadId = -1;
        }

        internal void SetWorldOwner(int loadId)
        {
            ownership = CASiteFactionReferenceKind.WorldFaction;
            ownerWorldFactionLoadId = loadId;
            ownerRegionalFactionKey = -1;
        }

        internal void SetNoSupport()
        {
            support = CASiteFactionReferenceKind.None;
            supportRegionalFactionKey = -1;
            supportWorldFactionLoadId = -1;
        }

        internal void SetRegionalSupport(int key)
        {
            support = CASiteFactionReferenceKind.RegionalFaction;
            supportRegionalFactionKey = key;
            supportWorldFactionLoadId = -1;
        }

        internal void SetWorldSupport(int loadId)
        {
            support = CASiteFactionReferenceKind.WorldFaction;
            supportWorldFactionLoadId = loadId;
            supportRegionalFactionKey = -1;
        }

        internal string ValidationFailure(CARegionalPlan region = null)
        {
            string owner = ValidateReference(ownership,
                ownerRegionalFactionKey, ownerWorldFactionLoadId,
                region, "owner");
            if (!owner.NullOrEmpty()) return owner;
            return ValidateReference(support, supportRegionalFactionKey,
                supportWorldFactionLoadId, region, "supporter");
        }

        private static string ValidateReference(
            CASiteFactionReferenceKind kind, int regionalKey,
            int worldLoadId, CARegionalPlan region, string role)
        {
            switch (kind)
            {
                case CASiteFactionReferenceKind.None:
                    return regionalKey == -1 && worldLoadId == -1 ? null
                        : "site has a " + role
                            + " payload without that relationship";
                case CASiteFactionReferenceKind.RegionalFaction:
                    if (regionalKey < 0 || worldLoadId != -1)
                        return "site has an invalid regional " + role;
                    return region == null || region.FactionPlan(regionalKey)
                        != null ? null : "site references a missing regional "
                            + role;
                case CASiteFactionReferenceKind.WorldFaction:
                    return worldLoadId >= 0 && regionalKey == -1 ? null
                        : "site has an invalid world " + role;
                default:
                    return "site has an unknown " + role
                        + " relationship";
            }
        }
    }

    // When a site has no owning faction, this is its canonical local state.
    // On a faction-owned site it exists only when the player explicitly
    // represents a local divergence; otherwise consumers resolve the owning
    // faction's canonical state directly.
    public sealed class CASiteLocalSocietyState : IExposable
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public bool explicitLocalDivergence;
        public CAPoliticalBeliefs politicalOrder;
        public CATechnologicalKnowledge technologicalKnowledge;
        public List<CAAxisEntry> institutions;
        public bool institutionalStateIncomplete;

        public void ExposeData()
        {
            // Zero is never a written version; see the ownership links
            // stamp above for the omission rationale.
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref explicitLocalDivergence,
                "explicitLocalDivergence", false);
            Scribe_Deep.Look(ref politicalOrder, "politicalOrder");
            Scribe_Deep.Look(ref technologicalKnowledge,
                "technologicalKnowledge");
            Scribe_Collections.Look(ref institutions, "institutions",
                LookMode.Deep);
            Scribe_Values.Look(ref institutionalStateIncomplete,
                "institutionalStateIncomplete", false);
        }

        internal CASiteLocalSocietyState Copy()
        {
            return new CASiteLocalSocietyState
            {
                schemaVersion = schemaVersion,
                explicitLocalDivergence = explicitLocalDivergence,
                politicalOrder = politicalOrder?.Copy(),
                technologicalKnowledge = technologicalKnowledge?.Copy(),
                institutions = CAFactionStartingState.CopyAxes(institutions),
                institutionalStateIncomplete = institutionalStateIncomplete
            };
        }

        internal void CopyFrom(CARegionalFactionPlan faction)
        {
            if (faction == null) return;
            politicalOrder = faction.politicalBeliefs?.Copy();
            technologicalKnowledge = faction.technologicalKnowledge?.Copy();
            institutions = CAFactionStartingState.CopyAxes(
                faction.factionStructure);
            institutionalStateIncomplete =
                faction.institutionalStateIncomplete;
        }
    }

    internal static class CASiteState
    {
        internal static bool HasOwner(CASiteFactionLinks links)
        {
            return links != null && links.ownership
                != CASiteFactionReferenceKind.None;
        }

        internal static CARegionalFactionPlan OwnerPlan(CARegionalPlan plan,
            CASiteFactionLinks links)
        {
            return links?.ownership
                    == CASiteFactionReferenceKind.RegionalFaction
                ? plan?.FactionPlan(links.ownerRegionalFactionKey) : null;
        }

        internal static Faction ResolveOwner(CARegionalPlan plan,
            CASiteFactionLinks links)
        {
            if (links == null) return null;
            if (links.ownership == CASiteFactionReferenceKind.WorldFaction)
                return CARegionalPlanUtility.FactionByLoadId(
                    links.ownerWorldFactionLoadId);
            CARegionalFactionPlan group = OwnerPlan(plan, links);
            return group?.resolvedFaction
                ?? CARegionalPlanUtility.FactionByLoadId(
                    group?.resolvedFactionLoadId ?? -1)
                ?? CARegionalPlanUtility.FactionByLoadId(
                    group?.existingFactionLoadId ?? -1);
        }

        internal static Faction ResolveSupport(CARegionalPlan plan,
            CASiteFactionLinks links)
        {
            if (links == null) return null;
            if (links.support == CASiteFactionReferenceKind.WorldFaction)
                return CARegionalPlanUtility.FactionByLoadId(
                    links.supportWorldFactionLoadId);
            if (links.support != CASiteFactionReferenceKind.RegionalFaction)
                return null;
            CARegionalFactionPlan group = plan?.FactionPlan(
                links.supportRegionalFactionKey);
            return group?.resolvedFaction
                ?? CARegionalPlanUtility.FactionByLoadId(
                    group?.resolvedFactionLoadId ?? -1)
                ?? CARegionalPlanUtility.FactionByLoadId(
                    group?.existingFactionLoadId ?? -1);
        }

        internal static CAPoliticalBeliefs PoliticalOrder(
            CARegionalPlan plan, CARegionalSettlementPlan settlement)
        {
            if (settlement == null) return null;
            CARegionalFactionPlan owner = OwnerPlan(plan,
                settlement.factionLinks);
            if (!HasOwner(settlement.factionLinks)
                || settlement.localSociety?.explicitLocalDivergence == true)
                return settlement.localSociety?.politicalOrder;
            return owner?.politicalBeliefs
                ?? CAFactionStateWorldComponent.Current
                    ?.Find(ResolveOwner(plan, settlement.factionLinks))
                    ?.politicalBeliefs;
        }

        internal static CATechnologicalKnowledge Knowledge(
            CARegionalPlan plan, CARegionalSettlementPlan settlement)
        {
            if (settlement == null) return null;
            CARegionalFactionPlan owner = OwnerPlan(plan,
                settlement.factionLinks);
            if (!HasOwner(settlement.factionLinks)
                || settlement.localSociety?.explicitLocalDivergence == true)
                return settlement.localSociety?.technologicalKnowledge;
            return owner?.technologicalKnowledge
                ?? CATechnologicalKnowledgeRuntime.ForFaction(
                    ResolveOwner(plan, settlement.factionLinks),
                    ensure: false);
        }

        internal static List<CAAxisEntry> Institutions(CARegionalPlan plan,
            CARegionalSettlementPlan settlement)
        {
            if (settlement == null) return null;
            CARegionalFactionPlan owner = OwnerPlan(plan,
                settlement.factionLinks);
            if (!HasOwner(settlement.factionLinks)
                || settlement.localSociety?.explicitLocalDivergence == true)
                return settlement.localSociety?.institutions;
            return owner?.factionStructure
                ?? CAFactionStateWorldComponent.Current
                    ?.Find(ResolveOwner(plan, settlement.factionLinks))
                    ?.factionStructure;
        }

        internal static void EnsureIndependentState(CARegionalPlan plan,
            CARegionalSettlementPlan settlement,
            CARegionalFactionPlan copyFrom = null)
        {
            if (settlement == null) return;
            bool mustSnapshot = settlement.localSociety == null
                || !settlement.localSociety.explicitLocalDivergence;
            if (settlement.factionLinks == null)
                settlement.factionLinks = new CASiteFactionLinks();
            settlement.factionLinks.SetNoOwner();
            if (settlement.localSociety == null)
                settlement.localSociety = new CASiteLocalSocietyState();
            // Detaching an owned site snapshots the owner's current state
            // exactly once. Dormant local fields from an earlier owner may
            // never silently reappear as the new independent site's state.
            if (mustSnapshot && copyFrom != null)
                settlement.localSociety.CopyFrom(copyFrom);
            string seed = (plan?.candidateId ?? "ca-region") + ":site:"
                + settlement.slot;
            if (settlement.localSociety.politicalOrder == null)
                settlement.localSociety.politicalOrder =
                    new CAPoliticalBeliefs();
            CAPoliticalBeliefsModel.Ensure(
                settlement.localSociety.politicalOrder, seed + ":politics");
            if (settlement.localSociety.technologicalKnowledge == null)
                settlement.localSociety.technologicalKnowledge =
                    new CATechnologicalKnowledge();
            CATechnologicalKnowledgeModel.Ensure(
                settlement.localSociety.technologicalKnowledge,
                seed + ":technology");
            if (settlement.localSociety.institutions == null)
                settlement.localSociety.institutions =
                    new List<CAAxisEntry>();
            settlement.localSociety.explicitLocalDivergence = true;
        }
    }

    // Native faction hostility is only one way that a pawn can threaten an
    // inhabited site. Factionless sites are evaluated against their actual
    // typed residents, preserving mental-state, predator and
    // hostile-to-factionless behavior without inventing a faction owner.
    internal static class CASiteThreats
    {
        internal static bool Threatens(CARegionalSettlementRecord record,
            Map map, Pawn candidate)
        {
            if (record == null || map == null || candidate == null
                || candidate.Dead || candidate.Downed || !candidate.Spawned
                || candidate.Map != map)
                return false;
            try
            {
                if (record.faction != null)
                    return candidate.Faction != record.faction
                        && candidate.HostileTo(record.faction);
                List<Pawn> residents = CAPopulationProjection.Residents(
                    record, map);
                for (int i = 0; i < residents.Count; i++)
                {
                    Pawn resident = residents[i];
                    if (resident == null || resident == candidate
                        || resident.Dead) continue;
                    if (candidate.HostileTo(resident)) return true;
                }
            }
            catch { }
            return false;
        }

        internal static List<Pawn> Within(
            CARegionalSettlementRecord record, Map map, CellRect area)
        {
            var result = new List<Pawn>();
            if (record == null || map?.mapPawns == null) return result;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                if (pawn != null && area.Contains(pawn.Position)
                    && Threatens(record, map, pawn))
                    result.Add(pawn);
            return result;
        }

        internal static bool LordServes(CARegionalSettlementRecord record,
            Map map, Lord lord)
        {
            if (record == null || map == null || lord == null
                || lord.faction != record.faction)
                return false;
            if (record.faction != null) return true;
            var residentIds = new HashSet<int>(CAPopulationProjection
                .Residents(record, map).Select(pawn => pawn.thingIDNumber));
            return lord.ownedPawns != null && lord.ownedPawns.Any(pawn =>
                pawn != null && residentIds.Contains(pawn.thingIDNumber));
        }
    }

    // Converts the one supported predecessor into the explicit site contract
    // without mutating the source graph until every site has a valid
    // candidate. The legacy integer fields are read only by the owning
    // IExposable adapters; this migration turns those parsed references into
    // complete current state and then retires the predecessor schema.
    internal static class CASiteAffiliationMigration
    {
        internal static bool TryPrepareRegionalPlan(CARegionalPlan plan,
            int expectedSourceSchema, bool requireCompleteSites,
            out List<Action> commits, out string failure)
        {
            commits = new List<Action>();
            failure = null;
            if (plan == null || plan.schemaVersion != expectedSourceSchema)
            {
                failure = "regional-plan source schema is not the expected "
                    + expectedSourceSchema;
                return false;
            }
            if (plan.settlements == null || plan.frontierHoldings == null)
            {
                failure = "regional site collections are missing";
                return false;
            }

            for (int i = 0; i < plan.settlements.Count; i++)
            {
                CARegionalSettlementPlan settlement = plan.settlements[i];
                if (settlement == null)
                {
                    failure = "regional plan contains a null settlement";
                    return false;
                }
                if (!TryPrepareSettlement(plan, settlement,
                        requireCompleteSites, out Action settlementCommit,
                        out failure))
                    return false;
                commits.Add(settlementCommit);
            }

            for (int i = 0; i < plan.frontierHoldings.Count; i++)
            {
                int index = i;
                CAFrontierHoldingPlan source = plan.frontierHoldings[i];
                if (!TryPrepareFrontier(plan, source,
                        out CAFrontierHoldingPlan candidate, out failure))
                    return false;
                commits.Add(() => plan.frontierHoldings[index] = candidate);
            }
            return true;
        }

        internal static bool TryPrepareRegionalRecord(
            CARegionalSettlementRecord record, CARegionalPlan region,
            out Action commit, out string failure)
        {
            commit = null;
            failure = null;
            if (record == null)
            {
                failure = "regional settlement record is missing";
                return false;
            }
            CASiteFactionLinks links = record.factionLinks;
            string linkFailure = ValidateLinks(links, region);
            if (!linkFailure.NullOrEmpty())
            {
                failure = "regional settlement " + record.regionalId + "#"
                    + record.slot + ": " + linkFailure;
                return false;
            }
            CARegionalFactionPlan owner = CASiteState.OwnerPlan(region,
                links);
            if (CASiteState.HasOwner(links) && owner == null
                && links.ownership
                    == CASiteFactionReferenceKind.RegionalFaction)
            {
                failure = "regional settlement " + record.regionalId + "#"
                    + record.slot + " references a missing owner";
                return false;
            }
            if (!CASiteState.HasOwner(links))
            {
                string localFailure = ValidateLocalSociety(
                    record.localSociety, requireComplete: true);
                if (!localFailure.NullOrEmpty())
                {
                    failure = "independent regional settlement "
                        + record.regionalId + "#" + record.slot + ": "
                        + localFailure;
                    return false;
                }
            }
            else if (record.localSociety != null)
            {
                string localFailure = ValidateLocalSociety(
                    record.localSociety,
                    record.localSociety.explicitLocalDivergence);
                if (!localFailure.NullOrEmpty())
                {
                    failure = "regional settlement local divergence: "
                        + localFailure;
                    return false;
                }
            }
            if (!TryPreparePopulationGroups(record.populationGroups,
                    allowEmpty: false, out Action populationsCommit,
                    out failure))
                return false;
            string generationDef = record.generationFactionDefName;
            if (generationDef.NullOrEmpty())
                generationDef = owner?.ResolvedFactionDef?.defName
                    ?? record.faction?.def?.defName;
            string finalGenerationDef = generationDef;
            commit = () =>
            {
                populationsCommit();
                record.generationFactionDefName = finalGenerationDef;
                record.schemaVersion =
                    CARegionalSettlementRecord.CurrentSchemaVersion;
            };
            return true;
        }

        internal static string ValidateCurrentRegionalPlan(
            CARegionalPlan plan, bool requireCompleteSites)
        {
            if (plan == null
                || plan.schemaVersion != CARegionalPlan.CurrentSchemaVersion)
                return "regional plan schema is not current";
            if (plan.settlements == null || plan.frontierHoldings == null)
                return "regional site collections are missing";
            foreach (CARegionalSettlementPlan settlement in plan.settlements)
            {
                if (settlement == null)
                    return "regional plan contains a null settlement";
                string linkFailure = ValidateLinks(settlement.factionLinks,
                    plan);
                if (!linkFailure.NullOrEmpty()) return linkFailure;
                if (!CASiteState.HasOwner(settlement.factionLinks))
                {
                    string localFailure = ValidateLocalSociety(
                        settlement.localSociety, requireCompleteSites);
                    if (!localFailure.NullOrEmpty()) return localFailure;
                }
                else if (settlement.localSociety != null)
                {
                    string localFailure = ValidateLocalSociety(
                        settlement.localSociety,
                        settlement.localSociety.explicitLocalDivergence);
                    if (!localFailure.NullOrEmpty()) return localFailure;
                }
                string populationFailure = ValidatePopulationGroups(
                    settlement.populationGroups, allowEmpty: true);
                if (!populationFailure.NullOrEmpty())
                    return populationFailure;
            }
            foreach (CAFrontierHoldingPlan holding in plan.frontierHoldings)
            {
                if (holding == null || holding.schemaVersion
                        != CAFrontierHoldingPlan.CurrentSchemaVersion)
                    return "regional frontier-holding schema is not current";
                string linkFailure = ValidateLinks(holding.factionLinks,
                    plan);
                if (!linkFailure.NullOrEmpty()) return linkFailure;
                string localFailure = ValidateLocalSociety(
                    holding.localSociety, requireComplete: true);
                if (!localFailure.NullOrEmpty()) return localFailure;
                string populationFailure = ValidatePopulationGroups(
                    holding.populationGroups, allowEmpty: false);
                if (!populationFailure.NullOrEmpty())
                    return populationFailure;
                string residenceFailure = ValidateFrontierResidence(holding);
                if (!residenceFailure.NullOrEmpty())
                    return residenceFailure;
            }
            return null;
        }

        internal static string ValidateCurrentRegionalRecord(
            CARegionalSettlementRecord record, CARegionalPlan region)
        {
            if (record == null || record.schemaVersion
                    != CARegionalSettlementRecord.CurrentSchemaVersion)
                return "regional settlement-record schema is not current";
            string linkFailure = ValidateLinks(record.factionLinks, region);
            if (!linkFailure.NullOrEmpty()) return linkFailure;
            if (!CASiteState.HasOwner(record.factionLinks))
            {
                if (record.faction != null)
                    return "independent settlement retains a native faction "
                        + "owner";
                string localFailure = ValidateLocalSociety(
                    record.localSociety, requireComplete: true);
                if (!localFailure.NullOrEmpty()) return localFailure;
            }
            else
            {
                Faction expectedOwner = CASiteState.ResolveOwner(region,
                    record.factionLinks);
                if (expectedOwner == null)
                    return "owned settlement cannot resolve its native "
                        + "faction owner";
                if (record.faction != expectedOwner)
                    return "settlement native faction does not match its "
                        + "canonical owner";
                if (record.localSociety != null)
                {
                    string localFailure = ValidateLocalSociety(
                        record.localSociety,
                        record.localSociety.explicitLocalDivergence);
                    if (!localFailure.NullOrEmpty()) return localFailure;
                }
            }
            string populationFailure = ValidatePopulationGroups(
                record.populationGroups, allowEmpty: false);
            if (!populationFailure.NullOrEmpty()) return populationFailure;
            return null;
        }

        internal static bool TryPrepareFrontierMapPlan(
            CAFrontierMapPlan source, out CAFrontierMapPlan candidate,
            out string failure)
        {
            candidate = null;
            failure = null;
            if (source == null || source.schemaVersion != 2
                || source.holdings == null)
            {
                failure = "frontier map-plan predecessor is invalid";
                return false;
            }
            candidate = new CAFrontierMapPlan
            {
                schemaVersion = CAFrontierMapPlan.CurrentSchemaVersion,
                mapId = source.mapId,
                mapTileId = source.mapTileId,
                mapWidth = source.mapWidth,
                mapHeight = source.mapHeight,
                realizationSourceHash = source.realizationSourceHash,
                holdings = new List<CAFrontierHoldingPlan>()
            };
            foreach (CAFrontierHoldingPlan holding in source.holdings)
            {
                if (!TryPrepareFrontier(null, holding,
                        out CAFrontierHoldingPlan current, out failure))
                    return false;
                candidate.holdings.Add(current);
            }
            return true;
        }

        internal static string ValidateCurrentFrontierMapPlan(
            CAFrontierMapPlan plan)
        {
            if (plan == null || plan.schemaVersion
                    != CAFrontierMapPlan.CurrentSchemaVersion)
                return "frontier map-plan schema is not current";
            if (plan.holdings == null)
                return "frontier map-plan holdings are missing";
            foreach (CAFrontierHoldingPlan holding in plan.holdings)
            {
                if (holding == null || holding.schemaVersion
                        != CAFrontierHoldingPlan.CurrentSchemaVersion)
                    return "frontier-holding schema is not current";
                string linkFailure = ValidateLinks(holding.factionLinks,
                    null);
                if (!linkFailure.NullOrEmpty()) return linkFailure;
                string localFailure = ValidateLocalSociety(
                    holding.localSociety, requireComplete: true);
                if (!localFailure.NullOrEmpty()) return localFailure;
                string populationFailure = ValidatePopulationGroups(
                    holding.populationGroups, allowEmpty: false);
                if (!populationFailure.NullOrEmpty())
                    return populationFailure;
                string residenceFailure = ValidateFrontierResidence(holding);
                if (!residenceFailure.NullOrEmpty())
                    return residenceFailure;
            }
            return null;
        }

        private static bool TryPrepareSettlement(CARegionalPlan plan,
            CARegionalSettlementPlan settlement, bool requireComplete,
            out Action commit, out string failure)
        {
            commit = null;
            failure = null;
            string linkFailure = ValidateLinks(settlement.factionLinks,
                plan);
            if (!linkFailure.NullOrEmpty())
            {
                failure = "settlement " + settlement.slot + ": "
                    + linkFailure;
                return false;
            }
            CARegionalFactionPlan owner = CASiteState.OwnerPlan(plan,
                settlement.factionLinks);
            if (CASiteState.HasOwner(settlement.factionLinks)
                && owner == null && settlement.factionLinks.ownership
                    == CASiteFactionReferenceKind.RegionalFaction)
            {
                failure = "settlement " + settlement.slot
                    + " references a missing owner";
                return false;
            }
            if (!CASiteState.HasOwner(settlement.factionLinks))
            {
                string localFailure = ValidateLocalSociety(
                    settlement.localSociety, requireComplete);
                if (!localFailure.NullOrEmpty())
                {
                    failure = "independent settlement " + settlement.slot
                        + ": " + localFailure;
                    return false;
                }
            }
            else if (settlement.localSociety != null)
            {
                string localFailure = ValidateLocalSociety(
                    settlement.localSociety,
                    settlement.localSociety.explicitLocalDivergence);
                if (!localFailure.NullOrEmpty())
                {
                    failure = "settlement " + settlement.slot
                        + " local divergence: " + localFailure;
                    return false;
                }
            }
            if (!TryPreparePopulationGroups(settlement.populationGroups,
                    allowEmpty: !requireComplete,
                    out Action populationsCommit, out failure))
                return false;
            string generationDef = settlement.generationFactionDefName;
            if (generationDef.NullOrEmpty())
                generationDef = owner?.ResolvedFactionDef?.defName;
            string finalGenerationDef = generationDef;
            commit = () =>
            {
                populationsCommit();
                settlement.generationFactionDefName = finalGenerationDef;
            };
            return true;
        }

        private static bool TryPrepareFrontier(CARegionalPlan plan,
            CAFrontierHoldingPlan source,
            out CAFrontierHoldingPlan candidate, out string failure)
        {
            candidate = null;
            failure = null;
            if (source == null)
            {
                failure = "regional plan contains a null frontier holding";
                return false;
            }
            if (source.schemaVersion ==
                CAFrontierHoldingPlan.CurrentSchemaVersion)
            {
                candidate = source.Copy();
                string currentFailure = ValidateLinks(
                    candidate.factionLinks, plan);
                if (!currentFailure.NullOrEmpty())
                {
                    failure = currentFailure;
                    return false;
                }
                currentFailure = ValidateLocalSociety(
                    candidate.localSociety, requireComplete: true);
                if (!currentFailure.NullOrEmpty())
                {
                    failure = currentFailure;
                    return false;
                }
                currentFailure = ValidatePopulationGroups(
                    candidate.populationGroups, allowEmpty: false);
                if (!currentFailure.NullOrEmpty())
                {
                    failure = currentFailure;
                    return false;
                }
                currentFailure = ValidateFrontierResidence(candidate);
                if (!currentFailure.NullOrEmpty())
                {
                    failure = currentFailure;
                    return false;
                }
                return true;
            }
            if (source.schemaVersion != 1)
            {
                failure = "frontier holding schema " + source.schemaVersion
                    + " has no supported migration";
                return false;
            }
            candidate = source.Copy();
            candidate.schemaVersion =
                CAFrontierHoldingPlan.CurrentSchemaVersion;
            if (candidate.factionLinks == null)
                candidate.factionLinks = new CASiteFactionLinks();
            candidate.factionLinks.schemaVersion =
                CASiteFactionLinks.CurrentSchemaVersion;
            string linkFailure = candidate.factionLinks.ValidationFailure(
                plan);
            if (!linkFailure.NullOrEmpty())
            {
                failure = linkFailure;
                return false;
            }
            CARegionalFactionPlan regionalSupport = candidate.factionLinks
                    .support == CASiteFactionReferenceKind.RegionalFaction
                ? plan?.FactionPlan(candidate.factionLinks
                    .supportRegionalFactionKey) : null;
            Faction worldSupport = candidate.factionLinks.support
                    == CASiteFactionReferenceKind.WorldFaction
                ? CARegionalPlanUtility.FactionByLoadId(candidate
                    .factionLinks.supportWorldFactionLoadId) : null;
            CATechnologicalKnowledge initialKnowledge = regionalSupport
                    ?.technologicalKnowledge
                ?? (worldSupport == null ? null
                    : CATechnologicalKnowledgeRuntime.ForFaction(
                        worldSupport));
            int legacyTier = source.capabilityTier;
            CASettlementEnvironmentFacts environment =
                CASettlementEnvironment.ForTile(candidate.memberTileId);
            if (environment == null || !environment.Valid)
            {
                failure = "frontier holding " + source.key
                    + " has no current world-tile evidence";
                return false;
            }
            CAHabitatViability.ApplyFrontier(candidate, environment,
                candidate.SupportingFactionKey,
                candidate.SupportingFactionLoadId, initialKnowledge);
            if (candidate.capabilityTier != legacyTier)
            {
                failure = "frontier holding " + source.key
                    + " support no longer reproduces its saved capability "
                    + legacyTier;
                return false;
            }
            if (candidate.generationFactionDefName.NullOrEmpty())
                candidate.generationFactionDefName = regionalSupport
                    ?.ResolvedFactionDef?.defName ?? worldSupport?.def?.defName;
            if (candidate.materialized
                && candidate.firstMaterializationTick < 0)
                candidate.firstMaterializationTick = 0;
            if (candidate.residenceAssignments == null)
                candidate.residenceAssignments =
                    new List<CASettlementResidenceAssignment>();
            if (candidate.materialized
                && candidate.residenceAssignments.Count == 0
                && candidate.residentPawnIds?.Count > 0)
            {
                int primaryKey = candidate.populationGroups.FirstOrDefault(
                    value => value?.isPrimary == true)?.key ?? -1;
                if (primaryKey < 0)
                {
                    failure = "materialized frontier holding " + source.key
                        + " has no primary population for residence migration";
                    return false;
                }
                int sequence = 1;
                foreach (int pawnId in candidate.residentPawnIds.Distinct())
                    candidate.residenceAssignments.Add(
                        new CASettlementResidenceAssignment
                        {
                            sequence = sequence++,
                            pawnId = pawnId,
                            populationGroupKey = primaryKey,
                            entryKind = "frontier affiliation migration",
                            entryEvidence = "existing frontier resident was "
                                + "assigned to the represented primary population",
                            enteredTick = candidate.firstMaterializationTick
                        });
            }
            string localFailure = ValidateLocalSociety(
                candidate.localSociety, requireComplete: true);
            if (!localFailure.NullOrEmpty())
            {
                failure = localFailure;
                return false;
            }
            string populationFailure = ValidatePopulationGroups(
                candidate.populationGroups, allowEmpty: false);
            if (!populationFailure.NullOrEmpty())
            {
                failure = populationFailure;
                return false;
            }
            string residenceFailure = ValidateFrontierResidence(candidate);
            if (!residenceFailure.NullOrEmpty())
            {
                failure = residenceFailure;
                return false;
            }
            return true;
        }

        private static string ValidateFrontierResidence(
            CAFrontierHoldingPlan holding)
        {
            if (holding == null) return "frontier holding is missing";
            if (holding.residentPawnIds == null
                || holding.residenceAssignments == null)
                return "frontier residence state is missing";
            var residentIds = new HashSet<int>(holding.residentPawnIds);
            if (residentIds.Count != holding.residentPawnIds.Count
                || residentIds.Any(value => value < 0))
                return "frontier resident identities are invalid";
            var active = holding.residenceAssignments.Where(value =>
                value?.active == true).ToList();
            if (active.Any(value => value.schemaVersion
                        != CASettlementResidenceAssignment.CurrentSchemaVersion
                    || value.pawnId < 0
                    || holding.populationGroups?.Any(group => group != null
                        && group.key == value.populationGroupKey) != true)
                || active.GroupBy(value => value.pawnId)
                    .Any(group => group.Count() != 1))
                return "frontier residence assignments are invalid";
            if (holding.materialized
                && !residentIds.SetEquals(active.Select(value => value.pawnId)))
                return "materialized frontier residents and population "
                    + "assignments disagree";
            if (!holding.materialized && (residentIds.Count > 0
                    || active.Count > 0))
                return "unmaterialized frontier holding retains residents";
            return null;
        }

        private static bool TryPreparePopulationGroups(
            List<CASettlementPopulationGroup> groups, bool allowEmpty,
            out Action commit, out string failure)
        {
            commit = null;
            failure = null;
            string currentFailure = ValidatePopulationGroups(groups,
                allowEmpty, acceptPredecessor: true);
            if (!currentFailure.NullOrEmpty())
            {
                failure = currentFailure;
                return false;
            }
            List<CASettlementPopulationGroup> values = groups
                ?? new List<CASettlementPopulationGroup>();
            CASettlementPopulationGroup inferredPrimary = null;
            if (values.Count > 0 && values.All(value => !value.isPrimary))
                inferredPrimary = values.Single(value => value.kind
                    == CAPopulationGroupKind.Main);
            commit = () =>
            {
                foreach (CASettlementPopulationGroup group in values)
                {
                    group.schemaVersion =
                        CASettlementPopulationGroup.CurrentSchemaVersion;
                    if (ReferenceEquals(group, inferredPrimary))
                        group.isPrimary = true;
                }
            };
            return true;
        }

        private static string ValidatePopulationGroups(
            List<CASettlementPopulationGroup> groups, bool allowEmpty,
            bool acceptPredecessor = false)
        {
            if (groups == null) return "population groups are missing";
            if (groups.Count == 0)
                return allowEmpty ? null : "population groups are empty";
            if (groups.Any(value => value == null))
                return "population groups contain a null record";
            if (groups.Any(value => value.schemaVersion
                    != CASettlementPopulationGroup.CurrentSchemaVersion
                    && (!acceptPredecessor || value.schemaVersion != 1)))
                return "population-group schema is unsupported";
            int primaryCount = groups.Count(value => value.isPrimary);
            if (primaryCount > 1)
                return "population groups have more than one primary group";
            if (primaryCount == 0 && groups.Count(value => value.kind
                    == CAPopulationGroupKind.Main) != 1)
                return "population groups cannot identify one legacy primary";
            return null;
        }

        private static string ValidateLinks(CASiteFactionLinks links,
            CARegionalPlan region)
        {
            if (links == null) return "site faction relationships are missing";
            if (links.schemaVersion
                    != CASiteFactionLinks.CurrentSchemaVersion)
                return "site faction-relationship schema is unsupported";
            return links.ValidationFailure(region);
        }

        private static string ValidateLocalSociety(
            CASiteLocalSocietyState local, bool requireComplete)
        {
            if (local == null)
                return requireComplete ? "local social state is missing"
                    : null;
            if (local.schemaVersion
                    != CASiteLocalSocietyState.CurrentSchemaVersion)
                return "local social-state schema is unsupported";
            if (!requireComplete) return null;
            if (!local.explicitLocalDivergence)
                return "local social state is not declared as local";
            string politicalFailure = CAPoliticalBeliefsModel
                .ValidationFailure(local.politicalOrder,
                    allowExactLegacy: false);
            if (!politicalFailure.NullOrEmpty())
                return "local Political Order: " + politicalFailure;
            string knowledgeFailure = CATechnologicalKnowledgeModel
                .ValidationFailure(local.technologicalKnowledge);
            if (!knowledgeFailure.NullOrEmpty())
                return "local Technological Knowledge: " + knowledgeFailure;
            string institutionFailure = CAPoliticalBeliefsModel
                .ValidationFailure(local.institutions);
            return institutionFailure.NullOrEmpty() ? null
                : "local institutions: " + institutionFailure;
        }
    }
}
