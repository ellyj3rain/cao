using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    public sealed class CASettlementResearchWork : IExposable
    {
        public string settlementKey;
        public string programKey;
        public string operatorIdentity;
        public string programSignature;
        public int pawnId = -1;
        public int jobId = -1;
        public IntVec3 bench = IntVec3.Invalid;
        public string behaviorKey;
        public int episodeId;
        public int authorityOrigin;
        public string authorityIdentity;
        public string owner;
        public int createdTick;
        public string terminationCondition;

        public void ExposeData()
        {
            Scribe_Values.Look(ref settlementKey, "settlementKey");
            Scribe_Values.Look(ref programKey, "programKey");
            Scribe_Values.Look(ref operatorIdentity, "operatorIdentity");
            Scribe_Values.Look(ref programSignature, "programSignature");
            Scribe_Values.Look(ref pawnId, "pawnId", -1);
            Scribe_Values.Look(ref jobId, "jobId", -1);
            Scribe_Values.Look(ref bench, "bench", IntVec3.Invalid);
            Scribe_Values.Look(ref behaviorKey, "behaviorKey");
            Scribe_Values.Look(ref episodeId, "episodeId", 0);
            Scribe_Values.Look(ref authorityOrigin, "authorityOrigin", 0);
            Scribe_Values.Look(ref authorityIdentity, "authorityIdentity");
            Scribe_Values.Look(ref owner, "owner");
            Scribe_Values.Look(ref createdTick, "createdTick", 0);
            Scribe_Values.Look(ref terminationCondition,
                "terminationCondition");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                CACombatIntent.ObserveEpisode(episodeId);
        }
    }

    public sealed class CASettlementRepairWork : IExposable
    {
        public string settlementKey;
        public string programKey;
        public string operatorIdentity;
        public string programSignature;
        public int pawnId = -1;
        public int jobId = -1;
        public int targetThingId = -1;
        public string targetLabel;
        public string behaviorKey;
        public int episodeId;
        public int authorityOrigin;
        public string authorityIdentity;
        public string owner;
        public int createdTick;
        public string terminationCondition;

        public void ExposeData()
        {
            Scribe_Values.Look(ref settlementKey, "settlementKey");
            Scribe_Values.Look(ref programKey, "programKey");
            Scribe_Values.Look(ref operatorIdentity, "operatorIdentity");
            Scribe_Values.Look(ref programSignature, "programSignature");
            Scribe_Values.Look(ref pawnId, "pawnId", -1);
            Scribe_Values.Look(ref jobId, "jobId", -1);
            Scribe_Values.Look(ref targetThingId, "targetThingId", -1);
            Scribe_Values.Look(ref targetLabel, "targetLabel");
            Scribe_Values.Look(ref behaviorKey, "behaviorKey");
            Scribe_Values.Look(ref episodeId, "episodeId", 0);
            Scribe_Values.Look(ref authorityOrigin, "authorityOrigin", 0);
            Scribe_Values.Look(ref authorityIdentity, "authorityIdentity");
            Scribe_Values.Look(ref owner, "owner");
            Scribe_Values.Look(ref createdTick, "createdTick", 0);
            Scribe_Values.Look(ref terminationCondition,
                "terminationCondition");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                CACombatIntent.ObserveEpisode(episodeId);
        }
    }

    public sealed class CASettlementRebuildWork : IExposable
    {
        public string settlementKey;
        public string programKey;
        public string operatorIdentity;
        public string programSignature;
        public string assetRole;
        public string oldThingId;
        public int provisionArrangementKey;
        public int provisionNodeIndex = -1;
        public string defName;
        public string stuffDefName;
        public IntVec3 cell = IntVec3.Invalid;
        public string blueprintThingId;
        public string frameThingId;
        public int factionLoadId = -1;
        public int createdTick = -1;
        public string behaviorKey;
        public int episodeId;
        public int authorityOrigin;
        public string authorityIdentity;
        public string owner;
        public string terminationCondition;

        public void ExposeData()
        {
            Scribe_Values.Look(ref settlementKey, "settlementKey");
            Scribe_Values.Look(ref programKey, "programKey");
            Scribe_Values.Look(ref operatorIdentity, "operatorIdentity");
            Scribe_Values.Look(ref programSignature, "programSignature");
            Scribe_Values.Look(ref assetRole, "assetRole");
            Scribe_Values.Look(ref oldThingId, "oldThingId");
            Scribe_Values.Look(ref provisionArrangementKey,
                "provisionArrangementKey", 0);
            Scribe_Values.Look(ref provisionNodeIndex,
                "provisionNodeIndex", -1);
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref stuffDefName, "stuffDefName");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref blueprintThingId, "blueprintThingId");
            Scribe_Values.Look(ref frameThingId, "frameThingId");
            Scribe_Values.Look(ref factionLoadId, "factionLoadId", -1);
            Scribe_Values.Look(ref createdTick, "createdTick", -1);
            Scribe_Values.Look(ref behaviorKey, "behaviorKey");
            Scribe_Values.Look(ref episodeId, "episodeId", 0);
            Scribe_Values.Look(ref authorityOrigin, "authorityOrigin", 0);
            Scribe_Values.Look(ref authorityIdentity, "authorityIdentity");
            Scribe_Values.Look(ref owner, "owner");
            Scribe_Values.Look(ref terminationCondition,
                "terminationCondition");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                CACombatIntent.ObserveEpisode(episodeId);
        }
    }

    // Materializes the saved open settlement program and registers its placed
    // assets for repair, rebuilding, and supported research work.
    internal static class CASettlementProgramMaterializer
    {
        internal static int TechTier(Faction faction, Map map = null)
        {
            int construction = Math.Min(
                CATechnologicalKnowledgeRuntime.EffectiveRank(faction,
                    CATechnologyDomains.Construction,
                    CATechnologyCompetencies.Construct, map),
                CATechnologicalKnowledgeRuntime.EffectiveRank(faction,
                    CATechnologyDomains.Construction,
                    CATechnologyCompetencies.Maintain, map));
            int materials = Math.Min(
                CATechnologicalKnowledgeRuntime.EffectiveRank(faction,
                    CATechnologyDomains.Metallurgy,
                    CATechnologyCompetencies.Construct, map),
                CATechnologicalKnowledgeRuntime.EffectiveRank(faction,
                    CATechnologyDomains.Metallurgy,
                    CATechnologyCompetencies.Maintain, map));
            if (construction >= 3 && materials >= 3) return 2;
            return construction >= 2 ? 1 : 0;
        }

        internal static int CanonicalTechTier(Faction faction)
        {
            int construction = Math.Min(
                CATechnologicalKnowledgeRuntime.CanonicalRank(faction,
                    CATechnologyDomains.Construction,
                    CATechnologyCompetencies.Construct),
                CATechnologicalKnowledgeRuntime.CanonicalRank(faction,
                    CATechnologyDomains.Construction,
                    CATechnologyCompetencies.Maintain));
            int materials = Math.Min(
                CATechnologicalKnowledgeRuntime.CanonicalRank(faction,
                    CATechnologyDomains.Metallurgy,
                    CATechnologyCompetencies.Construct),
                CATechnologicalKnowledgeRuntime.CanonicalRank(faction,
                    CATechnologyDomains.Metallurgy,
                    CATechnologyCompetencies.Maintain));
            if (construction >= 3 && materials >= 3) return 2;
            return construction >= 2 ? 1 : 0;
        }

        internal static int TechTier(CARegionalSettlementRecord record,
            Map map = null)
        {
            if (record == null) return 0;
            // Faction-owned sites query their owner's live availability.
            // Independent sites use their own canonical knowledge receipt;
            // faction support never substitutes for either authority.
            return record.faction != null ? TechTier(record.faction, map)
                : Mathf.Clamp(record.technologicalKnowledgeTier, 0, 3);
        }

        internal static int CanonicalTechTier(
            CARegionalSettlementRecord record)
        {
            if (record == null) return 0;
            if (record.faction != null)
                return CanonicalTechTier(record.faction);
            CATechnologicalKnowledge knowledge = record.localSociety
                ?.technologicalKnowledge;
            if (knowledge == null)
                return Mathf.Clamp(record.technologicalKnowledgeTier, 0, 3);
            int construction = Math.Min(
                CATechnologicalKnowledgeModel.Rank(knowledge,
                    CATechnologyDomains.Construction,
                    CATechnologyCompetencies.Construct),
                CATechnologicalKnowledgeModel.Rank(knowledge,
                    CATechnologyDomains.Construction,
                    CATechnologyCompetencies.Maintain));
            int materials = Math.Min(
                CATechnologicalKnowledgeModel.Rank(knowledge,
                    CATechnologyDomains.Metallurgy,
                    CATechnologyCompetencies.Construct),
                CATechnologicalKnowledgeModel.Rank(knowledge,
                    CATechnologyDomains.Metallurgy,
                    CATechnologyCompetencies.Maintain));
            if (construction >= 3 && materials >= 3) return 2;
            return construction >= 2 ? 1 : 0;
        }

        internal static void Materialize(CAOrganization org,
            CARegionalSettlementRecord record, Map map)
        {
            try
            {
                if (map == null || record == null
                    || record.localRect == CellRect.Empty) return;
                if (!record.creationAuthorized
                    || !record.creationExecutable
                    || !record.creationMaterialFeasible
                    || !record.creationSitingFeasible) return;
                if (record.seededAssets == null)
                    record.seededAssets = new List<string>();

                var rooms = new List<Room>();
                foreach (IntVec3 c in record.localRect)
                {
                    if (!c.InBounds(map) || !c.Roofed(map)) continue;
                    Room room = c.GetRoom(map);
                    if (room == null || room.PsychologicallyOutdoors
                        || room.IsDoorway || rooms.Contains(room)) continue;
                    if (room.CellCount < 6) continue;
                    rooms.Add(room);
                }
                // Retained Culture changes which otherwise valid rooms are
                // preferred. Shared public life pulls common programs
                // toward the settlement core. It never creates a room or
                // bypasses program feasibility.
                CACulturalMeaningResolution shared = CACultureModel.Resolve(
                    record.culture, CASocialSubjectRegistry.PublicGathering);
                IntVec3 culturalCore = record.layout?.core
                    ?? record.localRect.CenterCell;
                rooms = shared.Approval + shared.Salience > 0
                    ? rooms.OrderBy(room => RoomDistance(room, culturalCore))
                        .ThenByDescending(room => room.CellCount).ToList()
                    : rooms.OrderByDescending(room => room.CellCount)
                        .ToList();
                int cursor = 0;
                Room Next()
                {
                    if (rooms.Count == 0) return null;
                    return rooms[cursor++ % rooms.Count];
                }
                // Rooms from the FAR end of the list - spatially distinct
                // from the front-cursor rooms. Population-scoped provision can
                // therefore retain distinct program rooms without treating an
                // Ideoligion-protection flag as residential assignment.
                int farCursor = 0;
                Room NextFar()
                {
                    if (rooms.Count == 0) return null;
                    return rooms[rooms.Count - 1
                        - (farCursor++ % rooms.Count)];
                }

                foreach (CASettlementProgramEntry entry in record
                    .settlementProgram?.entries
                    ?? new List<CASettlementProgramEntry>())
                {
                    if (entry == null || !entry.blocker.NullOrEmpty()) continue;
                    if (entry.materializationState == "materialized") continue;
                    CASettlementProgramDef definition =
                        CASettlementProgramRegistry.Find(entry.programKey);
                    if (definition == null)
                    {
                        entry.materializationState = "blocked";
                        entry.blocker = "the saved program is not registered";
                        continue;
                    }
                    if (!CASettlementProgramRuntimeContract
                        .TryResolveOperator(record, map, entry,
                            out string operatorFailure))
                    {
                        entry.materializationState = "blocked";
                        entry.blocker = operatorFailure;
                        continue;
                    }
                    if (definition.MaterializeSpatialContract)
                    {
                        bool materialized = definition.SpatialMaterializer
                            ?.Invoke(record, map, entry) == true;
                        if (!materialized && entry.blocker.NullOrEmpty())
                            entry.blocker = "the native spatial contract could "
                                + "not be materialized on settlement ground";
                        continue;
                    }
                    if (entry.materializationState
                        == "present in saved geography")
                    {
                        continue;
                    }
                    List<CAProvisionArrangement> provisionArrangements =
                        ProvisionArrangements(record, entry);
                    if (CASettlementProgramOperationalResolver
                            .IsProvisionProgram(entry.programKey)
                        && provisionArrangements.Count == 0)
                    {
                        entry.materializationState = "blocked";
                        entry.blocker = "no exact provision arrangement exists for this program kind and operator";
                        continue;
                    }
                    if (provisionArrangements.Count > 0)
                    {
                        MaterializeProvisionEntry(org, record, map, entry,
                            definition, provisionArrangements, Next, NextFar);
                        continue;
                    }
                    Room room = entry.scope == "saved provision nodes"
                        && entry.programKey.Contains("provision")
                        ? NextFar() : Next();
                    int laid = 0;
                    int required = 0;
                    IntVec3 powerAnchor = IntVec3.Invalid;
                    int repetitions = Math.Max(1,
                        Math.Max(entry.count, entry.extent));
                    for (int repetition = 0; repetition < repetitions;
                        repetition++)
                    {
                        // Multi-role contracts belong together. Count repeats
                        // that whole contract at another site; extent repeats
                        // assets within the same site so powered programs can
                        // form one native network.
                        Room target = entry.count > 1 && repetition > 0
                            ? Next() : room;
                        for (int i = 0; i < entry.selectedCandidates.Count;
                            i++)
                        {
                            string candidate = entry.selectedCandidates[i];
                            required++;
                            int placed = Place(org, record, map, target,
                                candidate, null,
                                forPrisoners: entry.programKey
                                    == CASettlementProgramRegistry.Custody,
                                providerKey: ProviderKey(record, entry),
                                scope: entry.scope,
                                assetRole: "site:" + repetition
                                    + ":role:" + i,
                                programEntry: entry, reportFailure: false,
                                medical: entry.programKey
                                    == CASettlementProgramRegistry.Medicine,
                                near: ProgramRequiresPower(entry)
                                    ? powerAnchor : IntVec3.Invalid,
                                placedAt: cell =>
                                {
                                    ThingDef placedDef = DefDatabase<ThingDef>
                                        .GetNamedSilentFail(candidate);
                                    if (placedDef?.EverTransmitsPower == true)
                                        powerAnchor = cell;
                                });
                            laid += placed;
                        }
                    }
                    bool completeProgram = required > 0 && laid == required;
                    if (completeProgram && ProgramRequiresPower(entry))
                        completeProgram = HasWorkingPowerContract(record, map,
                            entry);
                    entry.materializationState = completeProgram
                        ? "materialized" : "blocked";
                    if (!completeProgram && entry.blocker.NullOrEmpty())
                        entry.blocker = laid != required
                            ? "only " + laid + " of " + required
                                + " required assets could be placed inside the "
                                + "settlement's realized ground"
                            : "the required powered assets do not share an "
                                + "active power source";
                    if (completeProgram)
                        org?.Record("settlement program", definition.Label
                            + " materialized - " + laid + " placed asset"
                            + (laid == 1 ? "" : "s"));
                }
                CAProvisionRuntimeResolver.Reconcile(record, map);
                Log.Message("[CA] " + (record.name ?? "settlement")
                    + " settlement program: "
                    + (record.settlementProgram?.entries?.Count ?? 0)
                    + " entries, " + record.seededAssets.Count
                    + " placed assets");
            }
            catch (Exception e)
            {
                Log.Warning("[CA] settlement program materialization failed for "
                    + (record?.name ?? "?") + ": " + e.Message);
            }
        }

        private static int Place(CAOrganization org,
            CARegionalSettlementRecord record, Map map, Room room,
            string defName, string stuffName, bool forPrisoners = false,
            string providerKey = null,
            CASettlementProgramEntry programEntry = null,
            bool reportFailure = false, bool medical = false,
            string scope = "settlement", IntVec3? near = null,
            string assetRole = null,
            int provisionArrangementKey = 0,
            int provisionNodeIndex = -1,
            Action<IntVec3> placedAt = null,
            Action<Thing> placedThing = null)
        {
            try
            {
                ThingDef def = CASettlementAssetRegistry.Resolve(
                    CASettlementAssetRegistry.DemandForProgram(
                        programEntry?.programKey), defName);
                if (def != null && (def.category != ThingCategory.Building
                    || !def.BuildableByPlayer || def.blueprintDef == null))
                    def = null;
                if (def == null)
                {
                    string blocker = "explicit starting asset " + defName
                        + " is not a valid loaded buildable definition";
                    record.creationBlocker = blocker;
                    org?.Record("works", "starting asset blocked - " + blocker);
                    if (reportFailure)
                        throw new InvalidOperationException(blocker);
                    return 0;
                }
                if (!CATechnologicalKnowledgeRuntime.CanConstructCanonical(
                        record.faction, def,
                        out CATechnologyRequirement missingKnowledge))
                {
                    string blocker = "explicit starting asset " + defName
                        + " exceeds the faction's authored technological "
                        + "knowledge: " + CAHabitatViability
                            .MissingKnowledgeWords(missingKnowledge);
                    record.creationBlocker = blocker;
                    org?.Record("works", "starting asset blocked - "
                        + blocker);
                    if (reportFailure)
                        throw new InvalidOperationException(blocker);
                    return 0;
                }
                ThingDef stuff = def.MadeFromStuff && stuffName != null
                    ? DefDatabase<ThingDef>.GetNamedSilentFail(stuffName)
                    : null;
                if (def.MadeFromStuff && stuff == null)
                    stuff = GenStuff.DefaultStuffFor(def);
                IntVec3 cell = FreeCell(map, room, record, def, stuff,
                    scope, near);
                if (!cell.IsValid)
                {
                    string blocker = "explicit starting asset " + defName
                        + " has no valid native placement in its authored area";
                    record.creationBlocker = blocker;
                    org?.Record("works", "starting asset blocked - " + blocker);
                    if (reportFailure)
                        throw new InvalidOperationException(blocker);
                    return 0;
                }
                AcceptanceReport placement = CASettlementSitingConstraints
                    .CanPlaceNativeBlueprint(map, def, cell, Rot4.South,
                        stuff);
                if (!placement.Accepted)
                {
                    string blocker = "explicit starting asset " + defName
                        + " has no valid native placement: "
                        + placement.Reason;
                    record.creationBlocker = blocker;
                    org?.Record("works", "starting asset blocked - " + blocker);
                    if (reportFailure)
                        throw new InvalidOperationException(blocker);
                    return 0;
                }
                Thing t = ThingMaker.MakeThing(def, stuff);
                ThingStyleDef culturalStyle =
                    CAVisualTraditionStyle.StyleFor(
                        CAVisualTraditionStyle.CultureFor(record), def);
                if (culturalStyle != null)
                    t.SetStyleDef(culturalStyle);
                GenSpawn.Spawn(t, cell, map, Rot4.South);
                if (record.faction != null) t.SetFaction(record.faction);
                CompRefuelable fuel = t.TryGetComp<CompRefuelable>();
                if (fuel != null && !fuel.HasFuel)
                    fuel.Refuel(fuel.Props.fuelCapacity);
                var bed = t as Building_Bed;
                if (bed != null && medical)
                {
                    try { bed.Medical = true; } catch { }
                }
                if (bed != null && forPrisoners)
                {
                    try { bed.ForPrisoners = true; } catch { }
                }
                record.seededAssets.Add(defName + "|" + cell.x + "|"
                    + cell.z + "|" + (stuff?.defName ?? "") + "|"
                    + (providerKey ?? ""));
                if (programEntry != null && !assetRole.NullOrEmpty())
                    CASettlementProgramAssets.RecordThing(record, map,
                        programEntry, assetRole, t,
                        provisionArrangementKey,
                        provisionNodeIndex);
                placedAt?.Invoke(cell);
                placedThing?.Invoke(t);
                return 1;
            }
            catch (Exception exception)
            {
                if (reportFailure)
                    throw new InvalidOperationException("could not place "
                        + defName, exception);
                return 0;
            }
        }

        private static bool MaterializeProvisionEntry(CAOrganization org,
            CARegionalSettlementRecord record, Map map,
            CASettlementProgramEntry entry,
            CASettlementProgramDef definition,
            List<CAProvisionArrangement> arrangements,
            Func<Room> next, Func<Room> nextFar)
        {
            var plans = new List<CAProvisionMaterializationPlan>();
            var reservedObservedStockIds = new HashSet<int>((record
                    .startingStock ?? new List<CAStartingStockRecord>())
                .Where(item => item != null && item.thingId >= 0)
                .Select(item => item.thingId));
            foreach (CAProvisionArrangement arrangement in arrangements)
            {
                if (!CAProvisionMaterializationAdapter.TryPlan(record, map,
                        arrangement, reservedObservedStockIds,
                        out CAProvisionMaterializationPlan plan,
                        out string failure))
                {
                    entry.materializationState = "blocked";
                    entry.blocker = failure;
                    return false;
                }
                if (plan.ObservedStock != null)
                    reservedObservedStockIds.Add(
                        plan.ObservedStock.thingIDNumber);
                plans.Add(plan);
            }
            if (entry.selectedCandidates == null
                || entry.selectedCandidates.Count == 0)
            {
                entry.materializationState = "blocked";
                entry.blocker = "the provision contract has no selected material asset types";
                return false;
            }
            CAOrganizationRelationsWorldComponent ledger =
                CAOrganizationRelationsWorldComponent.Current;
            if (ledger == null)
            {
                entry.materializationState = "blocked";
                entry.blocker = "the facility holding ledger is unavailable";
                return false;
            }
            if (record.startingStock == null)
                record.startingStock = new List<CAStartingStockRecord>();
            int seededStart = record.seededAssets.Count;
            int receiptStart = record.programAssets?.Count ?? 0;
            int stockStart = record.startingStock.Count;
            var spawned = new List<Thing>();
            var pendingHoldings = new List<CAFacilityHolding>();
            var addedHoldings = new List<CAFacilityHolding>();
            var pendingStock = new List<CAStartingStockRecord>();
            try
            {
                int assetCount = 0;
                foreach (CAProvisionMaterializationPlan plan in plans)
                {
                    for (int node = 0; node < plan.Nodes; node++)
                    {
                        Room room = plan.Arrangement.populationGroupKey >= 0
                            ? nextFar() : next();
                        var nodeThings = new List<Thing>();
                        for (int role = 0;
                            role < entry.selectedCandidates.Count; role++)
                        {
                            string candidate = entry.selectedCandidates[role];
                            string assetRole = "arrangement:"
                                + plan.Arrangement.key + ":node:" + node
                                + ":role:" + role;
                            int placed = Place(org, record, map, room,
                                candidate, null,
                                providerKey: CAProvisionArrangements.NodeKey(
                                    record, plan.Arrangement, node),
                                scope: entry.scope, programEntry: entry,
                                assetRole: assetRole,
                                provisionArrangementKey:
                                    plan.Arrangement.key,
                                provisionNodeIndex: node,
                                reportFailure: true,
                                placedThing: thing =>
                                {
                                    spawned.Add(thing);
                                    nodeThings.Add(thing);
                                });
                            if (placed != 1)
                                throw new InvalidOperationException(
                                    "a required provision asset could not be placed");
                            assetCount += placed;
                        }
                        Thing facility = nodeThings.FirstOrDefault();
                        if (facility == null)
                            throw new InvalidOperationException(
                                "a provision node has no material facility");
                        Thing stock = plan.ObservedStock;
                        if (stock == null)
                        {
                            stock = SpawnStock(record, map,
                                plan.AuthoredStockDef, plan.StockCount,
                                facility.Position);
                            spawned.Add(stock);
                        }
                        pendingStock.Add(new CAStartingStockRecord
                        {
                            thingId = stock.thingIDNumber,
                            thingDefName = stock.def.defName,
                            providerIdentity =
                                plan.Arrangement.operatorIdentity,
                            provisionArrangementKey = plan.Arrangement.key,
                            provisionNodeIndex = node,
                            count = plan.StockCount
                        });
                        for (int role = 0; role < nodeThings.Count; role++)
                        {
                            Thing nodeThing = nodeThings[role];
                            pendingHoldings.Add(new CAFacilityHolding
                            {
                                thingId = nodeThing.thingIDNumber,
                                mapId = map.uniqueID,
                                cell = nodeThing.Position,
                                facilityKind = entry.programKey,
                                programKey = entry.programKey,
                                programSignature = entry.signature,
                                assetRole = "arrangement:"
                                    + plan.Arrangement.key + ":node:" + node
                                    + ":role:" + role,
                                ownerIdentity =
                                    plan.Arrangement.operatorIdentity,
                                operatorIdentity =
                                    plan.Arrangement.operatorIdentity,
                                ownerOrgKey = plan.Operator.Organization
                                    ?.organizationKey,
                                operatorOrgKey = plan.Operator.Organization
                                    ?.organizationKey,
                                ownerPawnId = plan.Operator.Individual
                                    ?.thingIDNumber ?? -1,
                                operatorPawnId = plan.Operator.Individual
                                    ?.thingIDNumber ?? -1,
                                provisionArrangementKey =
                                    plan.Arrangement.key,
                                provisionNodeIndex = node,
                                capitalSource =
                                    CAProvisionMaterializationAdapter
                                    .CapitalSource(
                                        plan.Arrangement.funding),
                                allocationRule =
                                    CAProvisionMaterializationAdapter
                                    .AllocationRule(
                                        plan.Arrangement.access),
                                oversight = plan.Arrangement.funding
                                        == CAProvisionFunding.Taxation
                                    ? CAOversightKinds.Law
                                    : CAOversightKinds.None,
                                liability = plan.Arrangement.funding
                                        == CAProvisionFunding.Taxation
                                    ? CALiabilityKinds.Treasury
                                    : CALiabilityKinds.Owner,
                                beneficiaries = plan.Operator.Consumers
                                    .Select(pawn => "pawn:"
                                        + pawn.thingIDNumber)
                                    .OrderBy(value => value,
                                        StringComparer.Ordinal).ToList(),
                                origin = CAOrigin.Derived("provision:"
                                    + plan.Arrangement.key + ":" + node
                                    + ":" + role)
                            });
                        }
                    }
                }
                foreach (CAFacilityHolding holding in pendingHoldings)
                {
                    if (!ledger.Add(holding))
                        throw new InvalidOperationException(
                            "the provision holding identity already exists");
                    addedHoldings.Add(holding);
                }
                record.startingStock.AddRange(pendingStock);
                entry.materializationState = "materialized";
                entry.blocker = null;
                org?.Record("settlement program", definition.Label
                    + " materialized - " + assetCount + " placed asset"
                    + (assetCount == 1 ? "" : "s") + ", "
                    + pendingStock.Count + " stock receipt"
                    + (pendingStock.Count == 1 ? "" : "s"));
                return true;
            }
            catch (Exception exception)
            {
                foreach (CAFacilityHolding holding in addedHoldings)
                    ledger.RemoveDerivedHolding(holding);
                if (record.startingStock.Count > stockStart)
                    record.startingStock.RemoveRange(stockStart,
                        record.startingStock.Count - stockStart);
                if (record.seededAssets.Count > seededStart)
                    record.seededAssets.RemoveRange(seededStart,
                        record.seededAssets.Count - seededStart);
                CASettlementProgramAssets.RollbackToCount(record,
                    receiptStart);
                foreach (Thing thing in spawned.Where(thing => thing != null
                    && !thing.Destroyed).Reverse())
                    thing.Destroy(DestroyMode.Vanish);
                entry.materializationState = "blocked";
                entry.blocker = exception.Message;
                return false;
            }
        }

        private static Thing SpawnStock(CARegionalSettlementRecord record,
            Map map, ThingDef def, int count, IntVec3 near)
        {
            if (def == null || count <= 0)
                throw new InvalidOperationException(
                    "authored provision stock is invalid");
            IntVec3 cell = record.localRect.Cells.Where(candidate =>
                    candidate.InBounds(map) && candidate.Standable(map)
                    && !candidate.GetThingList(map).Any(thing =>
                        thing.def.category == ThingCategory.Building))
                .OrderBy(candidate => candidate.DistanceToSquared(near))
                .ThenBy(candidate => candidate.x)
                .ThenBy(candidate => candidate.z)
                .DefaultIfEmpty(IntVec3.Invalid).First();
            if (!cell.IsValid)
                throw new InvalidOperationException(
                    "authored provision stock has no valid placement cell");
            Thing stock = ThingMaker.MakeThing(def);
            stock.stackCount = count;
            GenSpawn.Spawn(stock, cell, map);
            return stock;
        }

        private static bool ProgramRequiresPower(
            CASettlementProgramEntry entry)
        {
            return entry?.programKey == CASettlementProgramRegistry.Trade
                || entry?.programKey
                    == CASettlementProgramRegistry.Communications;
        }

        private static bool HasWorkingPowerContract(
            CARegionalSettlementRecord record, Map map,
            CASettlementProgramEntry entry)
        {
            if (map == null || entry == null) return false;
            map.powerNetManager.UpdatePowerNetsAndConnections_First();
            List<Thing> placed = CASettlementProgramAssets
                .AssetIds(record, entry)
                .Select(id => map.listerThings.AllThings.FirstOrDefault(
                    thing => thing != null && thing.ThingID == id))
                .Where(thing => thing != null).ToList();
            List<CompPowerTrader> consumers = placed
                .Select(thing => thing.TryGetComp<CompPowerTrader>())
                .Where(power => power != null
                    && power.Props.PowerConsumption > 0f).ToList();
            return consumers.Count > 0 && consumers.All(power =>
                power.PowerNet != null
                && power.PowerNet.HasActivePowerSource);
        }

        private static float RoomDistance(Room room, IntVec3 anchor)
        {
            if (room == null || !anchor.IsValid) return float.MaxValue;
            IntVec3 nearest = room.Cells.OrderBy(cell =>
                cell.DistanceToSquared(anchor)).FirstOrDefault();
            return nearest.IsValid ? nearest.DistanceTo(anchor)
                : float.MaxValue;
        }

        private static string ProviderKey(CARegionalSettlementRecord record,
            CASettlementProgramEntry entry)
        {
            if (record == null || entry == null
                || !entry.programKey.Contains(".provision.")) return null;
            CAProvisionOperator? kind = entry.programKey
                == CASettlementProgramRegistry.DomesticProvision
                    ? CAProvisionOperator.DomesticUnit
                : entry.programKey == CASettlementProgramRegistry.CommunalProvision
                    ? CAProvisionOperator.Communal
                : entry.programKey
                    == CASettlementProgramRegistry.AuthorityProvision
                    ? CAProvisionOperator.Authority
                    : (CAProvisionOperator?)null;
            if (!kind.HasValue) return null;
            CAProvisionArrangement arrangement = (record
                    .provisionArrangements
                    ?? new List<CAProvisionArrangement>())
                .FirstOrDefault(item => item != null && item.active
                    && item.operatorIdentity == entry.operatorIdentity
                    && (item.operatorKind == kind.Value
                        || kind.Value == CAProvisionOperator.DomesticUnit
                            && item.operatorKind
                                == CAProvisionOperator.Individual));
            return CAProvisionArrangements.ProviderKey(record, arrangement);
        }

        private static List<CAProvisionArrangement> ProvisionArrangements(
            CARegionalSettlementRecord record,
            CASettlementProgramEntry entry)
        {
            if (record == null || entry == null) return
                new List<CAProvisionArrangement>();
            CAProvisionOperator? kind = entry.programKey
                == CASettlementProgramRegistry.DomesticProvision
                    ? CAProvisionOperator.DomesticUnit
                : entry.programKey == CASettlementProgramRegistry.CommunalProvision
                    ? CAProvisionOperator.Communal
                : entry.programKey
                    == CASettlementProgramRegistry.AuthorityProvision
                        ? CAProvisionOperator.Authority
                        : (CAProvisionOperator?)null;
            return !kind.HasValue
                ? new List<CAProvisionArrangement>()
                : (record.provisionArrangements
                        ?? new List<CAProvisionArrangement>())
                    .Where(item => item != null && item.active
                        && item.operatorIdentity == entry.operatorIdentity
                        && (item.operatorKind == kind.Value
                            || kind.Value == CAProvisionOperator.DomesticUnit
                                && item.operatorKind
                                    == CAProvisionOperator.Individual))
                    .OrderBy(item => item.key).ToList();
        }

        // Cultivation is a native spatial contract rather than a decorative
        // grower object. The saved program's extent becomes one real growing
        // zone, and later sowing/harvesting uses RimWorld's normal work.
        internal static bool MaterializeCultivation(
            CARegionalSettlementRecord record, Map map,
            CASettlementProgramEntry entry)
        {
            if (record == null || map == null || entry == null) return false;
            int wanted = Mathf.Clamp(Math.Max(entry.count, entry.extent)
                * 6, 6, 30);
            List<IntVec3> cells = record.localRect.Cells
                .Where(cell => cell.InBounds(map)
                    && cell.Standable(map) && !cell.Roofed(map)
                    && map.zoneManager.ZoneAt(cell) == null
                    && cell.GetFertility(map) >= 0.7f
                    && !cell.GetThingList(map).Any(thing =>
                        thing.def.category == ThingCategory.Building))
                .OrderBy(cell => cell.DistanceToSquared(
                    record.layout?.core ?? record.localRect.CenterCell))
                .Take(wanted).ToList();
            if (cells.Count < 6)
            {
                entry.materializationState = "blocked";
                entry.blocker = "no suitable unroofed growing ground exists "
                    + "inside the settlement's realized area";
                return false;
            }
            var zone = new Zone_Growing(map.zoneManager)
            {
                label = "Settlement cultivation"
            };
            map.zoneManager.RegisterZone(zone);
            foreach (IntVec3 cell in cells) zone.AddCell(cell);
            CASettlementProgramAssets.RecordZone(record, map, entry,
                "zone:0", zone, cells[0]);
            entry.materializationState = "materialized";
            record.seededAssets.Add("zone:growing|" + zone.ID + "|"
                + cells.Count + "||");
            return true;
        }

        private static IntVec3 FreeCell(Map map, Room room,
            CARegionalSettlementRecord record, ThingDef def, ThingDef stuff,
            string scope = "settlement", IntVec3? near = null)
        {
            IEnumerable<IntVec3> source;
            if (scope == "settlement perimeter")
                source = record.localRect.Cells.Where(c => c.InBounds(map)
                    && (c.x <= record.localRect.minX + 2
                        || c.x >= record.localRect.maxX - 2
                        || c.z <= record.localRect.minZ + 2
                        || c.z >= record.localRect.maxZ - 2));
            else if (scope == "usable settlement ground"
                || scope == "workable settlement ground")
                source = record.localRect.Cells.Where(c => c.InBounds(map)
                    && !c.Roofed(map));
            else
                source = room != null ? room.Cells
                    : record.localRect.Cells.Where(c => c.InBounds(map));
            if (near.HasValue && near.Value.IsValid)
                source = source.Where(cell =>
                        cell.DistanceToSquared(near.Value) <= 36)
                    .OrderBy(cell => cell.DistanceToSquared(near.Value));
            foreach (IntVec3 c in source)
            {
                if (def.category == ThingCategory.Building)
                {
                    AcceptanceReport placement = CASettlementSitingConstraints
                        .CanPlaceNativeBlueprint(map, def, c, Rot4.South,
                            stuff);
                    if (!placement.Accepted) continue;
                }
                else if (!CASettlementSitingConstraints.HasMaterialFootprint(
                        map, def, c, Rot4.South)) continue;
                if (c.GetEdifice(map) != null) continue;
                if (c.GetThingList(map).Any(t =>
                    t.def.category == ThingCategory.Building
                    || t.def.category == ThingCategory.Item)) continue;
                bool fits = true;
                foreach (IntVec3 o in GenAdj.OccupiedRect(c, Rot4.South,
                    def.size))
                    if (o.GetEdifice(map) != null)
                    { fits = false; break; }
                if (fits) return c;
            }
            return IntVec3.Invalid;
        }
    }

    // Settlement workers repair
    // their own damage, rebuild their destroyed program assets through
    // real blueprints and the vanilla Build duty (the siege-builder
    // machinery), and staff a saved research program - research accrues only while a
    // pawn actually stands at the bench, and lands only as material
    // things.
    public class CASettlementWorksMapComponent : MapComponent
    {
        private int nextTick;
        private List<CASettlementResearchWork> researchWork =
            new List<CASettlementResearchWork>();
        private List<CASettlementRepairWork> repairWork =
            new List<CASettlementRepairWork>();
        private List<CASettlementRebuildWork> rebuildWork =
            new List<CASettlementRebuildWork>();
        private bool needsRestoreValidation;

        public CASettlementWorksMapComponent(Map map) : base(map) { }

        internal bool HasActiveResearchContract(string settlementKey)
        {
            return !settlementKey.NullOrEmpty() && researchWork.Any(work =>
                work != null && work.settlementKey == settlementKey
                && !work.programKey.NullOrEmpty()
                && !work.operatorIdentity.NullOrEmpty()
                && !work.programSignature.NullOrEmpty()
                && work.pawnId >= 0 && work.jobId >= 0
                && work.bench.IsValid && !work.behaviorKey.NullOrEmpty()
                && work.episodeId > 0
                && !work.authorityIdentity.NullOrEmpty());
        }

        internal bool TryActiveResearchContract(string settlementKey,
            out CASettlementResearchWork contract)
        {
            contract = researchWork.FirstOrDefault(work => work != null
                && work.settlementKey == settlementKey
                && !work.programKey.NullOrEmpty()
                && !work.operatorIdentity.NullOrEmpty()
                && !work.programSignature.NullOrEmpty()
                && work.pawnId >= 0 && work.jobId >= 0
                && work.bench.IsValid && !work.behaviorKey.NullOrEmpty()
                && work.episodeId > 0
                && !work.authorityIdentity.NullOrEmpty());
            return contract != null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref researchWork,
                "CA_settlementResearchWork", LookMode.Deep);
            Scribe_Collections.Look(ref repairWork,
                "CA_settlementRepairWork", LookMode.Deep);
            Scribe_Collections.Look(ref rebuildWork,
                "CA_settlementRebuildWork", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (researchWork == null)
                    researchWork = new List<CASettlementResearchWork>();
                if (repairWork == null)
                    repairWork = new List<CASettlementRepairWork>();
                if (rebuildWork == null)
                    rebuildWork = new List<CASettlementRebuildWork>();
                needsRestoreValidation = true;
            }
        }

        public override void MapComponentTick()
        {
            if (needsRestoreValidation)
            {
                needsRestoreValidation = false;
                RevalidateRestoredWork();
            }
            if (Find.TickManager.TicksGame < nextTick) return;
            nextTick = Find.TickManager.TicksGame + 2000;
            try { Pulse(); }
            catch (Exception e)
            {
                Log.Warning("[CA] settlement works pulse failed: "
                    + e.Message);
            }
        }

        private void Pulse()
        {
            var world = CARegionalWorldComponent.Current;
            var comp = CAOrganizationWorldComponent.Current;
            if (world == null || comp == null) return;
            foreach (CARegionalSettlementRecord record in world.ForMap(map))
            {
                if (record == null || record.faction?.IsPlayer == true
                    || record.localRect == CellRect.Empty) continue;
                CAOrganization org = comp.ByKey(record.regionalId + "#"
                    + record.slot);
                if (org == null) continue;
                // Each pulse re-derives later development from the institution,
                // residents, structures, and ground that exist now. Starting
                // initial program materialization is historical evidence,
                // not this gate.
                CAOrganizationInheritance.RefreshDevelopmentAuthority(org,
                    record, map);
                if (!record.developmentExecutable) continue;
                if (TryCultivation(record, org)) continue;
                if (TryRepair(record, org)) continue;
                if (TryRebuild(record, org)) continue;
                // Research remains a represented program and work contract.
                // Culture may affect a worker's appraisal after eligibility;
                // an old activity label never creates priority or capacity.
                TryResearch(record, org);
            }
        }

        private Pawn FindWorker(CARegionalSettlementRecord record,
            CASettlementProgramEntry program,
            bool requireMaterializedAssets)
        {
            if (!CASettlementProgramRuntimeContract.TryResolve(record, map,
                    program, requireMaterializedAssets,
                    out CASettlementProgramRuntimeResolution runtime))
                return null;
            List<Pawn> pawns = runtime.Workers;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null || p.Downed || p.Dead || !p.Awake()
                    || p.IsPrisoner || !p.RaceProps.Humanlike) continue;
                if (p.InMentalState || p.InAggroMentalState) continue;
                if (CASiteThreats.Within(record, map,
                        CellRect.CenteredOn(p.Position, 40)).Count > 0)
                    continue;
                if (!p.Position.InHorDistOf(
                    record.localRect.CenterCell, 90f)) continue;
                Job cur = p.CurJob;
                if (cur != null && cur.def != JobDefOf.Wait
                    && cur.def != JobDefOf.Wait_Wander
                    && cur.def != JobDefOf.GotoWander
                    && cur.def != JobDefOf.Goto) continue;
                return p;
            }
            return null;
        }

        private bool TryCultivation(CARegionalSettlementRecord record,
            CAOrganization org)
        {
            foreach (CASettlementProgramEntry program in record
                ?.settlementProgram?.Entries(
                    CASettlementProgramRegistry.Agriculture)
                    ?? Enumerable.Empty<CASettlementProgramEntry>())
            {
            Pawn worker = FindWorker(record, program,
                requireMaterializedAssets: true);
            if (worker == null) continue;
            var zoneIds = new HashSet<int>(CASettlementProgramAssets
                .AssetIds(record, program).Where(value => value != null
                        && value.StartsWith("zone:",
                            StringComparison.Ordinal))
                .Select(value => int.TryParse(value.Substring(5),
                    out int id) ? id : -1).Where(id => id >= 0));
            if (zoneIds.Count == 0) return false;
            WorkGiverDef harvestDef = DefDatabase<WorkGiverDef>
                .GetNamedSilentFail("GrowerHarvest");
            WorkGiverDef sowDef = DefDatabase<WorkGiverDef>
                .GetNamedSilentFail("GrowerSow");
            foreach (WorkGiverDef work in new[] { harvestDef, sowDef })
            {
                var scanner = work?.Worker as WorkGiver_Scanner;
                if (scanner == null || scanner.ShouldSkip(worker)) continue;
                foreach (IntVec3 cell in record.localRect)
                {
                    if (!cell.InBounds(map)
                        || !(map.zoneManager.ZoneAt(cell) is Zone_Growing zone)
                        || !zoneIds.Contains(zone.ID)
                        || !scanner.HasJobOnCell(worker, cell)) continue;
                    Job job = scanner.JobOnCell(worker, cell);
                    if (job == null) continue;
                    if (!CASettlementInstitutionalAuthorization.TryAuthorizeJob(
                            record, program, worker, job,
                            CASettlementDemandKind.Cultivation,
                            "cultivation at " + cell,
                            requireMaterializedAssets: true,
                            out CABehaviorDecision _,
                            out CAIntentContext _)) continue;
                    worker.jobs.StartJob(job,
                        JobCondition.InterruptForced);
                    return worker.CurJob == job;
                }
            }
            }
            return false;
        }

        private bool TryRepair(CARegionalSettlementRecord record,
            CAOrganization org)
        {
            using (CAModuleProfiler.Measure(
                CAModuleProfileKey.SettlementRepair))
            {
            string settlementKey = record.regionalId + "#" + record.slot;
            foreach (CASettlementProgramEntry program in record
                .settlementProgram?.entries
                    ?? new List<CASettlementProgramEntry>())
            {
                Pawn worker = FindWorker(record, program,
                    requireMaterializedAssets: true);
                if (worker == null) continue;
                foreach (CASettlementProgramAssetReceipt receipt in
                    CASettlementProgramAssets.ReceiptsFor(record, program))
                {
                Building b = CASettlementProgramAssets.LiveThing(record,
                    map, receipt) as Building;
                if (b == null || b.Faction != record.faction) continue;
                if (b.HitPoints >= b.MaxHitPoints
                    || !b.def.useHitPoints) continue;
                if (repairWork.Any(work => work != null
                        && work.settlementKey == settlementKey
                        && work.targetThingId == b.thingIDNumber))
                    continue;
                if (!worker.CanReserveAndReach(b, PathEndMode.Touch,
                    Danger.Some)) continue;
                Job job = JobMaker.MakeJob(JobDefOf.Repair, b);
                if (!CASettlementInstitutionalAuthorization.TryAuthorizeJob(
                        record, program, worker, job,
                        CASettlementDemandKind.Maintenance,
                        "repair " + b.LabelShort,
                        requireMaterializedAssets: true,
                        out CABehaviorDecision _,
                        out CAIntentContext intent))
                    continue;
                worker.jobs.StartJob(job, JobCondition.InterruptForced);
                if (worker.CurJob != job)
                {
                    CABehaviorIntentMapComponent.For(map)?.Unregister(worker,
                        job);
                    continue;
                }
                repairWork.Add(new CASettlementRepairWork
                {
                    settlementKey = settlementKey,
                    programKey = program.programKey,
                    operatorIdentity = program.operatorIdentity,
                    programSignature = program.signature,
                    pawnId = worker.thingIDNumber,
                    jobId = job.loadID,
                    targetThingId = b.thingIDNumber,
                    targetLabel = b.LabelShort,
                    behaviorKey = intent.BehaviorKey,
                    episodeId = intent.EpisodeId,
                    authorityOrigin = (int)intent.AuthorityOrigin,
                    authorityIdentity = intent.AuthorityIdentity,
                    owner = intent.OwnershipScope,
                    createdTick = intent.CreatedTick,
                    terminationCondition = intent.TerminationCondition
                });
                return true;
                }
            }
            return false;
            }
        }

        private bool TryRebuild(CARegionalSettlementRecord record,
            CAOrganization org)
        {
            using (CAModuleProfiler.Measure(
                CAModuleProfileKey.SettlementRebuilding))
            {
            if (record.programAssets == null) return false;
            string settlementKey = record.regionalId + "#" + record.slot;
            foreach (CASettlementProgramEntry program in record
                .settlementProgram?.entries
                    ?? new List<CASettlementProgramEntry>())
            {
                Pawn worker = FindWorker(record, program,
                    requireMaterializedAssets: false);
                if (worker == null) continue;
                foreach (CASettlementProgramAssetReceipt receipt in
                    CASettlementProgramAssets.ReceiptsFor(record, program)
                        .Where(item => item.assetKind == "thing"))
                {
                if (CASettlementProgramAssets.LiveThing(record, map,
                        receipt) != null) continue;
                ThingDef def = DefDatabase<ThingDef>
                    .GetNamedSilentFail(receipt.defName);
                if (def == null) continue;
                IntVec3 cell = receipt.cell;
                if (!cell.InBounds(map)) continue;
                CASettlementRebuildWork pending = rebuildWork
                    .FirstOrDefault(work => work != null
                        && work.settlementKey == settlementKey
                        && work.programSignature == program.signature
                        && work.assetRole == receipt.assetRole);
                bool standing = cell.GetThingList(map).Any(t =>
                    t.def == def
                    || (t is Blueprint_Build bp && bp.def.entityDefToBuild
                        == def)
                    || (t is Frame f && f.def.entityDefToBuild == def));
                if (standing) continue;
                if (pending != null)
                {
                    int age = pending.createdTick < 0 ? 0
                        : Find.TickManager.TicksGame - pending.createdTick;
                    if (pending.createdTick < 0 || age < 60000) continue;
                    rebuildWork.Remove(pending);
                }
                if (!cell.Standable(map)) continue;
                ThingDef stuff = !receipt.stuffDefName.NullOrEmpty()
                    ? DefDatabase<ThingDef>.GetNamedSilentFail(
                        receipt.stuffDefName)
                    : (def.MadeFromStuff
                        ? GenStuff.DefaultStuffFor(def) : null);
                Job approach = null;
                try
                {
                    AcceptanceReport placement = CASettlementSitingConstraints
                        .CanPlaceNativeBlueprint(map, def, cell, Rot4.South,
                            stuff);
                    if (!placement.Accepted) continue;
                    approach = JobMaker.MakeJob(JobDefOf.Goto, cell);
                    if (!CASettlementInstitutionalAuthorization
                            .TryAuthorizeJob(record, program, worker, approach,
                                CASettlementDemandKind.Maintenance,
                                "rebuild " + def.defName + " at " + cell,
                                requireMaterializedAssets: false,
                                out CABehaviorDecision _,
                                out CAIntentContext intent))
                        continue;
                    Blueprint_Build blueprint = GenConstruct
                        .PlaceBlueprintForBuild(def, cell, map,
                            Rot4.South, record.faction, stuff);
                    if (blueprint == null)
                    {
                        CABehaviorIntentMapComponent.For(map)?.Unregister(
                            worker, approach);
                        continue;
                    }
                    rebuildWork.Add(new CASettlementRebuildWork
                    {
                        settlementKey = settlementKey,
                        programKey = program.programKey,
                        operatorIdentity = program.operatorIdentity,
                        programSignature = program.signature,
                        assetRole = receipt.assetRole,
                        oldThingId = receipt.thingId,
                        provisionArrangementKey =
                            receipt.provisionArrangementKey,
                        provisionNodeIndex = receipt.provisionNodeIndex,
                        defName = def.defName,
                        stuffDefName = stuff?.defName,
                        cell = cell,
                        blueprintThingId = blueprint.ThingID,
                        factionLoadId = record.faction?.loadID ?? -1,
                        createdTick = Find.TickManager.TicksGame,
                        behaviorKey = intent.BehaviorKey,
                        episodeId = intent.EpisodeId,
                        authorityOrigin = (int)intent.AuthorityOrigin,
                        authorityIdentity = intent.AuthorityIdentity,
                        owner = intent.OwnershipScope,
                        terminationCondition = intent.TerminationCondition
                    });
                    worker.mindState.duty = new PawnDuty(DutyDefOf.Build,
                        cell) { radius = 12f };
                    worker.jobs.StartJob(approach,
                        JobCondition.InterruptForced);
                    if (worker.CurJob != approach)
                        CABehaviorIntentMapComponent.For(map)?.Unregister(
                            worker, approach);
                    return true;
                }
                catch
                {
                    CAModuleProfiler.RecordFailure(
                        CAModuleProfileKey.SettlementRebuilding);
                    if (approach != null)
                        CABehaviorIntentMapComponent.For(map)?.Unregister(
                            worker, approach);
                }
                }
            }
            return false;
            }
        }

        private bool TryResearch(CARegionalSettlementRecord record,
            CAOrganization org)
        {
            using (CAModuleProfiler.Measure(
                CAModuleProfileKey.SettlementResearch))
            {
            if (org == null) return false;
            string settlementKey = record.regionalId + "#" + record.slot;
            foreach (CASettlementProgramEntry program in record
                ?.settlementProgram?.Entries(
                    CASettlementProgramRegistry.Research)
                    ?? Enumerable.Empty<CASettlementProgramEntry>())
            {
            Pawn worker = FindWorker(record, program,
                requireMaterializedAssets: true);
            if (worker?.skills?.GetSkill(
                    SkillDefOf.Intellectual)?.Level <= 0) continue;
            if (researchWork.Any(work => work != null
                    && work.settlementKey == settlementKey
                    && work.programSignature == program.signature))
                continue;
            Building benchThing = CASettlementProgramAssets
                .ReceiptsFor(record, program)
                .Select(receipt => CASettlementProgramAssets.LiveThing(
                    record, map, receipt) as Building)
                .FirstOrDefault(building => building != null
                    && building.Faction == record.faction
                    && building.def?.defName?.Contains(
                        "ResearchBench") == true);
            IntVec3 bench = benchThing?.Position ?? IntVec3.Invalid;
            if (!bench.IsValid || !bench.InBounds(map)) continue;

            bool attended = worker.Position.InHorDistOf(bench, 5f);
            if (!attended)
            {
                if (worker.CanReach(bench, PathEndMode.Touch, Danger.Some))
                {
                    Job approach = JobMaker.MakeJob(JobDefOf.Goto, bench);
                    if (CASettlementInstitutionalAuthorization.TryAuthorizeJob(
                            record, program, worker, approach,
                            CASettlementDemandKind.Research,
                            "staff research bench at " + bench,
                            requireMaterializedAssets: true,
                            out CABehaviorDecision _, out CAIntentContext _))
                    {
                        worker.jobs.StartJob(approach,
                            JobCondition.InterruptForced);
                        if (worker.CurJob != approach)
                            CABehaviorIntentMapComponent.For(map)?.Unregister(
                                worker, approach);
                        else return true;
                    }
                }
                continue;
            }
            Job study = JobMaker.MakeJob(JobDefOf.Wait, bench);
            study.expiryInterval = 300;
            if (!CASettlementInstitutionalAuthorization.TryAuthorizeJob(record,
                    program, worker, study, CASettlementDemandKind.Research,
                    "research at " + bench,
                    requireMaterializedAssets: true,
                    out CABehaviorDecision _,
                    out CAIntentContext intent)) continue;
            worker.jobs.StartJob(study, JobCondition.InterruptForced);
            if (worker.CurJob != study)
            {
                CABehaviorIntentMapComponent.For(map)?.Unregister(worker,
                    study);
                continue;
            }
            researchWork.Add(new CASettlementResearchWork
            {
                settlementKey = settlementKey,
                programKey = program.programKey,
                operatorIdentity = program.operatorIdentity,
                programSignature = program.signature,
                pawnId = worker.thingIDNumber,
                jobId = study.loadID,
                bench = bench,
                behaviorKey = intent.BehaviorKey,
                episodeId = intent.EpisodeId,
                authorityOrigin = (int)intent.AuthorityOrigin,
                authorityIdentity = intent.AuthorityIdentity,
                owner = intent.OwnershipScope,
                createdTick = intent.CreatedTick,
                terminationCondition = intent.TerminationCondition
            });
            return true;
            }
            return false;
            }
        }

        // The native job tracker owns repair and research completion truth.
        // Durable effects require both the exact saved work receipt and a
        // Succeeded native job; merely originating the job records no history.
        internal void CompleteNativeLabor(Pawn worker, Job job,
            JobCondition condition)
        {
            if (worker == null || job == null) return;

            int repairIndex = repairWork.FindIndex(work => work != null
                && work.pawnId == worker.thingIDNumber
                && work.jobId == job.loadID);
            if (repairIndex >= 0)
            {
                CASettlementRepairWork completed = repairWork[repairIndex];
                repairWork.RemoveAt(repairIndex);
                CARegionalSettlementRecord record = RecordFor(
                    completed.settlementKey);
                CABehaviorDecision completionDecision;
                bool authorized = condition == JobCondition.Succeeded
                    && CASettlementInstitutionalAuthorization
                        .TryReauthorizeCompletion(record, map, worker, job,
                            completed.programKey,
                            completed.operatorIdentity,
                            completed.programSignature,
                            requireMaterializedAssets: true,
                            completed.behaviorKey, completed.episodeId,
                            (CAAuthorityOrigin)completed.authorityOrigin,
                            completed.authorityIdentity, completed.owner,
                            "repair " + completed.targetLabel,
                            out completionDecision);
                CABehaviorIntentMapComponent.For(map)?.Unregister(worker, job);
                if (authorized)
                {
                    Building repaired = map.listerThings.AllThings
                        .OfType<Building>().FirstOrDefault(building =>
                            building.thingIDNumber
                                == completed.targetThingId);
                    CAOrganization org = CAOrganizationWorldComponent.Current
                        ?.ByKey(completed.settlementKey);
                    if (record != null && repaired != null && org != null
                        && repaired.Faction == record.faction
                        && repaired.def.useHitPoints
                        && repaired.HitPoints >= repaired.MaxHitPoints)
                        org.Record("works", "repair completed - "
                            + (completed.targetLabel
                                ?? repaired.LabelShort)
                            + " restored by settlement residents");
                }
            }

            int researchIndex = researchWork.FindIndex(work => work != null
                && work.pawnId == worker.thingIDNumber
                && work.jobId == job.loadID);
            if (researchIndex < 0) return;
            CASettlementResearchWork research = researchWork[researchIndex];
            researchWork.RemoveAt(researchIndex);
            CARegionalSettlementRecord researchRecord = RecordFor(
                research.settlementKey);
            CABehaviorDecision researchDecision;
            bool researchAuthorized = condition == JobCondition.Succeeded
                && research.bench.InBounds(map)
                && CASettlementInstitutionalAuthorization
                    .TryReauthorizeCompletion(researchRecord, map, worker, job,
                        research.programKey, research.operatorIdentity,
                        research.programSignature,
                        requireMaterializedAssets: true,
                        research.behaviorKey, research.episodeId,
                        (CAAuthorityOrigin)research.authorityOrigin,
                        research.authorityIdentity, research.owner,
                        "research at " + research.bench,
                        out researchDecision);
            CABehaviorIntentMapComponent.For(map)?.Unregister(worker, job);
            int researchTick = Find.TickManager?.TicksGame ?? -1;
            if (!researchAuthorized)
            {
                CAPropositionKnowledgeWorldComponent.Current
                    ?.RecordSettlementResearch(research, worker,
                        researchRecord, false, researchTick);
                return;
            }
            CAOrganization researchOrg = CAOrganizationWorldComponent.Current
                ?.ByKey(research.settlementKey);
            CASettlementProgramEntry researchProgram = researchRecord
                ?.settlementProgram?.Entries(research.programKey)
                .FirstOrDefault(entry => entry.operatorIdentity
                    == research.operatorIdentity
                    && entry.signature == research.programSignature);
            bool currentBench = researchProgram != null
                && CASettlementProgramAssets.ReceiptsFor(researchRecord,
                    researchProgram).Any(receipt =>
                        CASettlementProgramAssets.LiveThing(researchRecord,
                            map, receipt) is Building building
                        && building.Position == research.bench
                        && building.def?.defName?.Contains(
                            "ResearchBench") == true);
            if (!currentBench || researchOrg == null)
            {
                CAPropositionKnowledgeWorldComponent.Current
                    ?.RecordSettlementResearch(research, worker,
                        researchRecord, false, researchTick);
                return;
            }
            researchRecord.researchStock++;
            researchRecord.lastResearchActivityTick =
                researchTick;
            CAPropositionKnowledgeWorldComponent.Current
                ?.RecordSettlementResearch(research, worker, researchRecord,
                    true, researchTick);
            if (researchRecord.researchStock == 25
                || researchRecord.researchStock == 75
                || researchRecord.researchStock == 150)
            {
                researchRecord.researchMilestones++;
                MaterializeMilestone(researchRecord, researchOrg);
            }
        }

        private void RevalidateRestoredWork()
        {
            for (int i = repairWork.Count - 1; i >= 0; i--)
            {
                CASettlementRepairWork work = repairWork[i];
                Pawn pawn = PawnFor(work?.pawnId ?? -1);
                Job job = pawn?.CurJob;
                CARegionalSettlementRecord record = RecordFor(
                    work?.settlementKey);
                CABehaviorDecision decision;
                bool valid = work != null && job != null
                    && job.loadID == work.jobId
                    && CASettlementInstitutionalAuthorization
                        .TryReauthorizeCompletion(record, map, pawn, job,
                            work.programKey, work.operatorIdentity,
                            work.programSignature,
                            requireMaterializedAssets: true,
                            work.behaviorKey, work.episodeId,
                            (CAAuthorityOrigin)work.authorityOrigin,
                            work.authorityIdentity, work.owner,
                            "repair " + work.targetLabel, out decision);
                if (valid) continue;
                repairWork.RemoveAt(i);
                CancelRestoredJob(pawn, job, work?.jobId ?? -1);
            }
            for (int i = researchWork.Count - 1; i >= 0; i--)
            {
                CASettlementResearchWork work = researchWork[i];
                Pawn pawn = PawnFor(work?.pawnId ?? -1);
                Job job = pawn?.CurJob;
                CARegionalSettlementRecord record = RecordFor(
                    work?.settlementKey);
                CABehaviorDecision decision;
                bool valid = work != null && job != null
                    && job.loadID == work.jobId
                    && CASettlementInstitutionalAuthorization
                        .TryReauthorizeCompletion(record, map, pawn, job,
                            work.programKey, work.operatorIdentity,
                            work.programSignature,
                            requireMaterializedAssets: true,
                            work.behaviorKey, work.episodeId,
                            (CAAuthorityOrigin)work.authorityOrigin,
                            work.authorityIdentity, work.owner,
                            "research at " + work.bench, out decision);
                if (valid) continue;
                researchWork.RemoveAt(i);
                CancelRestoredJob(pawn, job, work?.jobId ?? -1);
            }
            for (int i = rebuildWork.Count - 1; i >= 0; i--)
            {
                CASettlementRebuildWork work = rebuildWork[i];
                CARegionalSettlementRecord record = RecordFor(
                    work?.settlementKey);
                CABehaviorDecision decision;
                bool valid = work != null
                    && RebuildTargetOwnedBy(record, work)
                    && CASettlementInstitutionalAuthorization
                        .TryReauthorizeCompletion(record, map, null, null,
                            work.programKey, work.operatorIdentity,
                            work.programSignature,
                            requireMaterializedAssets: false,
                            work.behaviorKey, work.episodeId,
                            (CAAuthorityOrigin)work.authorityOrigin,
                            work.authorityIdentity, work.owner,
                            "rebuild " + work.defName + " at " + work.cell,
                            out decision);
                if (valid) continue;
                rebuildWork.RemoveAt(i);
                CancelRestoredRebuild(record, work);
            }
        }

        private void CancelRestoredJob(Pawn pawn, Job job, int expectedJobId)
        {
            if (pawn == null || job == null || job.loadID != expectedJobId)
                return;
            CABehaviorIntentMapComponent.For(map)?.Unregister(pawn, job);
            if (pawn.CurJob == job && !job.playerForced)
                pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
        }

        private void CancelRestoredRebuild(CARegionalSettlementRecord record,
            CASettlementRebuildWork work)
        {
            if (work == null) return;
            Thing blueprint = map.listerThings
                .ThingsInGroup(ThingRequestGroup.Blueprint)
                .FirstOrDefault(thing => thing.ThingID
                    == work.blueprintThingId);
            if (blueprint != null)
            {
                blueprint.Destroy(DestroyMode.Cancel);
                return;
            }
            if (!work.cell.InBounds(map)) return;
            if (work.frameThingId.NullOrEmpty()) return;
            Frame frame = work.cell.GetThingList(map).OfType<Frame>()
                .FirstOrDefault(candidate => FrameMatchesWork(candidate,
                    work, requireBoundIdentity: true));
            frame?.Destroy(DestroyMode.Cancel);
        }

        // Blueprint.TryReplaceWithSolidThing binds the CA-created blueprint to
        // the exact native Frame after RimWorld has spawned and factioned it.
        // The saved frame identity keeps a stale settlement receipt from ever
        // matching foreign construction.
        internal void BindNativeRebuildFrame(string blueprintThingId,
            Frame frame)
        {
            if (blueprintThingId.NullOrEmpty() || frame == null) return;
            CASettlementRebuildWork work = rebuildWork.FirstOrDefault(
                candidate => candidate != null
                    && candidate.blueprintThingId == blueprintThingId);
            if (work == null || !FrameMatchesWork(frame, work,
                    requireBoundIdentity: false))
                return;
            work.frameThingId = frame.ThingID;
        }

        private bool RebuildTargetOwnedBy(CARegionalSettlementRecord record,
            CASettlementRebuildWork work)
        {
            if (record == null || work == null
                || !work.cell.InBounds(map) || work.defName.NullOrEmpty())
                return false;
            int expectedFaction = record.faction?.loadID ?? -1;
            if (work.factionLoadId != expectedFaction) return false;

            Blueprint_Build blueprint = map.listerThings
                .ThingsInGroup(ThingRequestGroup.Blueprint)
                .OfType<Blueprint_Build>()
                .FirstOrDefault(candidate => candidate.ThingID
                    == work.blueprintThingId);
            if (blueprint != null)
                return BlueprintMatchesWork(blueprint, work);

            if (work.frameThingId.NullOrEmpty()) return false;
            Frame frame = work.cell.GetThingList(map).OfType<Frame>()
                .FirstOrDefault(candidate => FrameMatchesWork(candidate,
                    work, requireBoundIdentity: true));
            return frame != null;
        }

        private static bool BlueprintMatchesWork(Blueprint_Build blueprint,
            CASettlementRebuildWork work)
        {
            return blueprint != null && work != null
                && blueprint.Position == work.cell
                && blueprint.BuildDef?.defName == work.defName
                && (work.stuffDefName.NullOrEmpty()
                    || blueprint.Stuff?.defName == work.stuffDefName)
                && FactionMatches(blueprint.Faction, work.factionLoadId);
        }

        private static bool FrameMatchesWork(Frame frame,
            CASettlementRebuildWork work, bool requireBoundIdentity)
        {
            return frame != null && work != null
                && frame.Position == work.cell
                && frame.BuildDef?.defName == work.defName
                && (work.stuffDefName.NullOrEmpty()
                    || frame.Stuff?.defName == work.stuffDefName)
                && FactionMatches(frame.Faction, work.factionLoadId)
                && (!requireBoundIdentity
                    || frame.ThingID == work.frameThingId);
        }

        private static bool FactionMatches(Faction faction, int loadId)
        {
            return loadId < 0 ? faction == null
                : faction != null && faction.loadID == loadId;
        }

        // Frame.CompleteConstruction is the native rebuild completion seam.
        // A matching faction building and the saved institutional ownership
        // receipt must both exist before the settlement gains durable history.
        internal bool AuthorizeNativeRebuildCompletion(Frame frame,
            out int episodeId)
        {
            episodeId = 0;
            if (frame == null) return true;
            int index = rebuildWork.FindIndex(work => work != null
                && FrameMatchesWork(frame, work,
                    requireBoundIdentity: true));
            if (index < 0) return true;
            CASettlementRebuildWork work = rebuildWork[index];
            episodeId = work.episodeId;
            CARegionalSettlementRecord record = RecordFor(work.settlementKey);
            CABehaviorDecision decision;
            bool allowed = RebuildTargetOwnedBy(record, work)
                && CASettlementInstitutionalAuthorization
                .TryReauthorizeCompletion(record, map, null, null,
                    work.programKey, work.operatorIdentity,
                    work.programSignature,
                    requireMaterializedAssets: false,
                    work.behaviorKey, work.episodeId,
                    (CAAuthorityOrigin)work.authorityOrigin,
                    work.authorityIdentity, work.owner,
                    "rebuild " + work.defName + " at " + work.cell,
                    out decision);
            if (!allowed)
            {
                rebuildWork.RemoveAt(index);
                episodeId = 0;
            }
            return allowed;
        }

        internal void CompleteNativeRebuild(int episodeId, IntVec3 cell,
            string defName, string stuffDefName)
        {
            int index = rebuildWork.FindIndex(work => work != null
                && work.episodeId == episodeId
                && work.cell == cell && work.defName == defName
                && (work.stuffDefName.NullOrEmpty()
                    || work.stuffDefName == stuffDefName));
            if (index < 0) return;
            CASettlementRebuildWork completed = rebuildWork[index];
            rebuildWork.RemoveAt(index);
            CARegionalSettlementRecord record = RecordFor(
                completed.settlementKey);
            Building rebuilt = cell.InBounds(map)
                ? cell.GetThingList(map).OfType<Building>()
                    .FirstOrDefault(building => building.def.defName == defName
                        && (completed.stuffDefName.NullOrEmpty()
                            || building.Stuff?.defName
                                == completed.stuffDefName))
                : null;
            if (rebuilt == null || record == null
                || rebuilt.Faction != record.faction) return;
            CAOrganization org = CAOrganizationWorldComponent.Current
                ?.ByKey(completed.settlementKey);
            CABehaviorDecision completionDecision;
            bool authorized = org != null
                && CASettlementInstitutionalAuthorization
                    .TryReauthorizeCompletion(record, map, null, null,
                        completed.programKey,
                        completed.operatorIdentity,
                        completed.programSignature,
                        requireMaterializedAssets: false,
                        completed.behaviorKey, completed.episodeId,
                        (CAAuthorityOrigin)completed.authorityOrigin,
                        completed.authorityIdentity, completed.owner,
                        "rebuild " + defName + " at " + cell,
                        out completionDecision);
            if (!authorized) return;
            CASettlementProgramEntry program = record.settlementProgram
                ?.Entries(completed.programKey).FirstOrDefault(entry =>
                    entry.operatorIdentity == completed.operatorIdentity
                    && entry.signature == completed.programSignature);
            CASettlementProgramAssetReceipt receipt = record.programAssets
                ?.FirstOrDefault(item => item != null
                    && item.programSignature == completed.programSignature
                    && item.assetRole == completed.assetRole
                    && item.thingId == completed.oldThingId);
            if (program == null || receipt == null) return;
            CASettlementProgramAssets.Rebind(record, map, program, receipt,
                rebuilt);
            org.Record("works", "rebuild completed - " + rebuilt.LabelShort
                + " restored by settlement residents");
            CAOrganizationInheritance.RefreshDevelopmentAuthority(org,
                record, map);
        }

        private CARegionalSettlementRecord RecordFor(string settlementKey)
        {
            return CARegionalWorldComponent.Current?.ForMap(map)
                .FirstOrDefault(candidate => candidate != null
                    && candidate.regionalId + "#" + candidate.slot
                        == settlementKey);
        }

        private Pawn PawnFor(int pawnId)
        {
            return map.mapPawns.AllPawnsSpawned
                .FirstOrDefault(pawn => pawn.thingIDNumber == pawnId);
        }

        // Research access is represented by placed objects, not a number alone.
        private void MaterializeMilestone(CARegionalSettlementRecord record,
            CAOrganization org)
        {
            ThingDef def;
            string story;
            switch (record.researchMilestones)
            {
                case 1:
                    def = FirstKnownManufacturedWeapon(record.faction,
                        "Gun_BoltActionRifle", "MeleeWeapon_LongSword");
                    if (def == null) return;
                    story = "their study bears arms - finer weapons join"
                        + " the armory";
                    break;
                case 2:
                    def = FirstKnownMedicalSupply(record.faction,
                        "MedicineIndustrial", "MedicineHerbal");
                    if (def == null) return;
                    story = "their study bears healing - the infirmary"
                        + " stocks deeper";
                    break;
                default:
                    def = FirstKnownMaintainableBuilding(record.faction,
                        "StandingLamp", "TorchLamp");
                    if (def == null) return;
                    story = "their study bears light - the halls glow"
                        + " longer into the night";
                    break;
            }
            IntVec3 cell = record.localRect.CenterCell;
            if (!cell.InBounds(map) || !cell.Standable(map))
                cell = record.localRect.Cells.FirstOrDefault(c =>
                    c.InBounds(map) && c.Standable(map));
            if (!cell.IsValid) return;
            try
            {
                Thing t = def.MadeFromStuff
                    ? ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def))
                    : ThingMaker.MakeThing(def);
                if (t.def.stackLimit > 1) t.stackCount = 8;
                GenSpawn.Spawn(t, cell, map);
                if (t.def.category == ThingCategory.Building
                    && record.faction != null)
                    t.SetFaction(record.faction);
                else t.SetForbidden(true, false);
                org.Record("research", story);
                Log.Message("[CA] " + (record.name ?? "settlement")
                    + " research milestone " + record.researchMilestones
                    + ": " + story);
            }
            catch { }
        }

        private ThingDef FirstKnownManufacturedWeapon(Faction faction,
            params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                ThingDef def = DefDatabase<ThingDef>
                    .GetNamedSilentFail(candidate);
                if (def?.IsWeapon != true) continue;
                if (!CATechnologicalKnowledgeRuntime.CanManufacture(
                        faction, def, out _, map)) continue;
                if (CATechnologicalKnowledgeRuntime.CanUseWeapon(
                        faction, def, out _, map)) return def;
            }
            return null;
        }

        private ThingDef FirstKnownMedicalSupply(Faction faction,
            params string[] candidates)
        {
            if (!CATechnologicalKnowledgeRuntime.CanProvideMedicalCare(
                    faction, out _, map)) return null;
            foreach (string candidate in candidates)
            {
                ThingDef def = DefDatabase<ThingDef>
                    .GetNamedSilentFail(candidate);
                if (def?.IsMedicine != true) continue;
                if (CATechnologicalKnowledgeRuntime.CanManufacture(
                        faction, def, out _, map)) return def;
            }
            return null;
        }

        private ThingDef FirstKnownMaintainableBuilding(Faction faction,
            params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                ThingDef def = DefDatabase<ThingDef>
                    .GetNamedSilentFail(candidate);
                if (def?.category != ThingCategory.Building) continue;
                if (!CATechnologicalKnowledgeRuntime.CanConstruct(
                        faction, def, out _, map)) continue;
                if (CATechnologicalKnowledgeRuntime.CanMaintain(
                        faction, def, out _, map)) return def;
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
    internal static class Patch_CASettlementNativeRebuildCompletion
    {
        private struct CompletionState
        {
            public Map map;
            public IntVec3 cell;
            public string defName;
            public string stuffDefName;
            public int episodeId;
        }

        private static bool Prefix(Frame __instance,
            out CompletionState __state)
        {
            __state = new CompletionState
            {
                map = __instance?.Map,
                cell = __instance?.Position ?? IntVec3.Invalid,
                defName = __instance?.BuildDef?.defName,
                stuffDefName = __instance?.Stuff?.defName
            };
            if (__state.map == null || !__state.cell.IsValid
                || __state.defName.NullOrEmpty()) return true;
            CASettlementWorksMapComponent component = __state.map
                .GetComponent<CASettlementWorksMapComponent>();
            if (component == null) return true;
            if (component.AuthorizeNativeRebuildCompletion(__instance,
                    out __state.episodeId))
                return true;
            __instance.Destroy(DestroyMode.Cancel);
            __state.map = null;
            return false;
        }

        private static void Postfix(CompletionState __state)
        {
            if (__state.map == null || !__state.cell.IsValid
                || __state.defName.NullOrEmpty()
                || __state.episodeId <= 0) return;
            __state.map.GetComponent<CASettlementWorksMapComponent>()
                ?.CompleteNativeRebuild(__state.episodeId, __state.cell,
                    __state.defName, __state.stuffDefName);
        }
    }

    [HarmonyPatch(typeof(Blueprint), "TryReplaceWithSolidThing")]
    internal static class Patch_CASettlementNativeRebuildFrame
    {
        private struct BlueprintState
        {
            public Map map;
            public string blueprintThingId;
        }

        private static void Prefix(Blueprint __instance,
            out BlueprintState __state)
        {
            Blueprint_Build build = __instance as Blueprint_Build;
            __state = new BlueprintState
            {
                map = build?.Map,
                blueprintThingId = build?.ThingID
            };
        }

        private static void Postfix(bool __result, Thing createdThing,
            BlueprintState __state)
        {
            if (!__result || __state.map == null
                || createdThing is not Frame frame
                || __state.blueprintThingId.NullOrEmpty()) return;
            __state.map.GetComponent<CASettlementWorksMapComponent>()
                ?.BindNativeRebuildFrame(__state.blueprintThingId, frame);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker),
        nameof(Pawn_JobTracker.EndCurrentJob))]
    internal static class Patch_CASettlementNativeLaborCompletion
    {
        private static readonly System.Reflection.FieldInfo PawnField =
            AccessTools.Field(typeof(Pawn_JobTracker), "pawn");

        private static void Prefix(Pawn_JobTracker __instance,
            JobCondition condition)
        {
            Job job = __instance?.curJob;
            Pawn pawn = PawnField?.GetValue(__instance) as Pawn;
            Map map = pawn?.Map;
            if (map == null || job == null) return;
            map.GetComponent<CARoadExpansionMapComponent>()
                ?.CompleteNativeLabor(pawn, job, condition);
            map.GetComponent<CASettlementWorksMapComponent>()
                ?.CompleteNativeLabor(pawn, job, condition);
        }
    }
}
