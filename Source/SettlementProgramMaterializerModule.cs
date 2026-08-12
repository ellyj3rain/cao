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
        internal static int TechTier(Faction faction)
        {
            return CASettlementAxes.Tier(
                faction?.def?.techLevel ?? TechLevel.Neolithic);
        }

        internal static int TechTier(CARegionalSettlementRecord record)
        {
            if (record == null) return 0;
            if (record.factionEra > (int)TechLevel.Undefined)
                return CASettlementAxes.Tier(
                    (TechLevel)record.factionEra);
            return 0;
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

                int tier = TechTier(record);

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
                // from the front-cursor rooms - so a quartered population
                // group has separate program rooms.
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
                    if (provisionArrangements.Count > 0)
                    {
                        int provisionAssets = 0;
                        int provisionRoles = 0;
                        bool complete = true;
                        foreach (CAProvisionArrangement arrangement in
                            provisionArrangements)
                        {
                            int nodes = Math.Max(1, arrangement.nodes);
                            for (int node = 0; node < nodes; node++)
                            {
                                string nodeKey = CAProvisionArrangements
                                    .NodeKey(record, arrangement, node);
                                Room nodeRoom = arrangement.populationGroupKey
                                        >= 0 ? NextFar() : Next();
                                foreach (string candidate in entry
                                    .selectedCandidates)
                                {
                                    provisionRoles++;
                                    int placed = Place(org, record, map,
                                        nodeRoom, candidate, null,
                                        providerKey: nodeKey,
                                        scope: entry.scope,
                                        programEntry: entry,
                                        reportFailure: false);
                                    provisionAssets += placed;
                                    if (placed != 1) complete = false;
                                }
                            }
                        }
                        complete = complete && provisionRoles > 0
                            && provisionAssets == provisionRoles;
                        entry.materializationState = complete
                            ? "assets placed" : "blocked";
                        if (!complete && entry.blocker.NullOrEmpty())
                            entry.blocker = "only " + provisionAssets + " of "
                                + provisionRoles + " required provision assets "
                                + "could be placed inside the settlement's "
                                + "realized ground";
                        if (complete)
                            org?.Record("settlement program",
                                definition.Label + " materialized - "
                                + provisionAssets + " placed asset"
                                + (provisionAssets == 1 ? "" : "s"));
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
                    if (entry.programKey == CASettlementProgramRegistry.Storage)
                        Stock(org, record, map, room, "Pemmican", 75,
                            ProviderKey(record, entry));
                    if (entry.programKey == CASettlementProgramRegistry.Medicine)
                        Stock(org, record, map, room, tier >= 2
                            ? "MedicineIndustrial" : "MedicineHerbal",
                            tier >= 2 ? 10 : 12, org?.organizationKey);
                    bool completeProgram = required > 0 && laid == required;
                    if (completeProgram && ProgramRequiresPower(entry))
                        completeProgram = HasWorkingPowerContract(map, entry);
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
                CAProvisionArrangements.RealizeOperatorsAndStock(record, map,
                    (rm, def, count, providerKey) =>
                        Stock(org, record, map, rm, def, count, providerKey));
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
            Action<IntVec3> placedAt = null)
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
                programEntry?.placedThingIds.Add(t.ThingID);
                placedAt?.Invoke(cell);
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

        private static bool ProgramRequiresPower(
            CASettlementProgramEntry entry)
        {
            return entry?.programKey == CASettlementProgramRegistry.Trade
                || entry?.programKey
                    == CASettlementProgramRegistry.Communications;
        }

        private static bool HasWorkingPowerContract(Map map,
            CASettlementProgramEntry entry)
        {
            if (map == null || entry?.placedThingIds == null) return false;
            map.powerNetManager.UpdatePowerNetsAndConnections_First();
            List<Thing> placed = entry.placedThingIds
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
                == CASettlementProgramRegistry.HouseholdProvision
                    ? CAProvisionOperator.Household
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
                    && item.operatorKind == kind.Value);
            return CAProvisionArrangements.ProviderKey(record, arrangement);
        }

        private static List<CAProvisionArrangement> ProvisionArrangements(
            CARegionalSettlementRecord record,
            CASettlementProgramEntry entry)
        {
            if (record == null || entry == null) return
                new List<CAProvisionArrangement>();
            CAProvisionOperator? kind = entry.programKey
                == CASettlementProgramRegistry.HouseholdProvision
                    ? CAProvisionOperator.Household
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
                        && item.operatorKind == kind.Value)
                    .OrderBy(item => item.key).ToList();
        }

        private static int Stock(CAOrganization org,
            CARegionalSettlementRecord record, Map map, Room room,
            string defName, int count, string providerOrgKey)
        {
            try
            {
                ThingDef def = DefDatabase<ThingDef>
                    .GetNamedSilentFail(defName);
                if (def == null) return 0;
                IntVec3 cell = FreeCell(map, room, record, def, null);
                if (!cell.IsValid) return 0;
                Thing t = ThingMaker.MakeThing(def);
                t.stackCount = Math.Min(count, def.stackLimit);
                Thing spawned = GenSpawn.Spawn(t, cell, map);
                spawned.SetForbidden(true, false);
                if (record.startingStock == null)
                    record.startingStock = new List<CAStartingStockRecord>();
                record.startingStock.Add(new CAStartingStockRecord
                {
                    thingId = spawned.thingIDNumber,
                    thingDefName = spawned.def.defName,
                    providerOrgKey = providerOrgKey.NullOrEmpty()
                        ? org?.organizationKey : providerOrgKey
                });
                return 1;
            }
            catch { return 0; }
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
            entry.placedThingIds.Add("zone:" + zone.ID);
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
                if (record?.faction == null || record.faction.IsPlayer
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
                Pawn worker = FindWorker(record);
                if (worker == null) continue;

                CACulturalMeaningResolution research = CACultureModel.Resolve(
                    record.culture, CASocialSubjectRegistry.ResearchWork);
                if (research.Approval + research.Prestige
                        + research.Salience >= 80
                    && TryResearch(record, worker, org)) continue;
                if (TryCultivation(record, worker, org)) continue;
                if (TryRepair(record, worker, org)) continue;
                if (TryRebuild(record, worker, org)) continue;
                TryResearch(record, worker, org);
            }
        }

        private Pawn FindWorker(CARegionalSettlementRecord record)
        {
            var pawns = map.mapPawns.SpawnedPawnsInFaction(record.faction);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null || p.Downed || p.Dead || !p.Awake()
                    || p.IsPrisoner || !p.RaceProps.Humanlike) continue;
                if (p.InMentalState || p.InAggroMentalState) continue;
                if (map.attackTargetsCache
                        .TargetsHostileToFaction(record.faction)
                        .Any(t => t.Thing is Pawn tp && !tp.Downed
                            && tp.Position.InHorDistOf(p.Position, 40f)))
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
            Pawn worker, CAOrganization org)
        {
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
                        || !(map.zoneManager.ZoneAt(cell) is Zone_Growing)
                        || !scanner.HasJobOnCell(worker, cell)) continue;
                    Job job = scanner.JobOnCell(worker, cell);
                    if (job == null) continue;
                    if (!CASettlementInstitutionalAuthorization.TryAuthorizeJob(
                            record, worker, job,
                            "cultivation at " + cell,
                            out CABehaviorDecision _,
                            out CAIntentContext _)) continue;
                    worker.jobs.StartJob(job,
                        JobCondition.InterruptForced);
                    return worker.CurJob == job;
                }
            }
            return false;
        }

        private bool TryRepair(CARegionalSettlementRecord record,
            Pawn worker, CAOrganization org)
        {
            string settlementKey = record.regionalId + "#" + record.slot;
            foreach (IntVec3 c in record.localRect)
            {
                if (!c.InBounds(map)) continue;
                Building b = c.GetEdifice(map);
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
                        record, worker, job, "repair " + b.LabelShort,
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
            return false;
        }

        private bool TryRebuild(CARegionalSettlementRecord record,
            Pawn worker, CAOrganization org)
        {
            if (record.seededAssets == null) return false;
            string settlementKey = record.regionalId + "#" + record.slot;
            for (int i = 0; i < record.seededAssets.Count; i++)
            {
                string[] parts = record.seededAssets[i].Split('|');
                if (parts.Length < 4) continue;
                ThingDef def = DefDatabase<ThingDef>
                    .GetNamedSilentFail(parts[0]);
                int x, z;
                if (def == null || !int.TryParse(parts[1], out x)
                    || !int.TryParse(parts[2], out z)) continue;
                IntVec3 cell = new IntVec3(x, 0, z);
                if (!cell.InBounds(map)) continue;
                CASettlementRebuildWork pending = rebuildWork
                    .FirstOrDefault(work => work != null
                        && work.settlementKey == settlementKey
                        && work.defName == def.defName
                        && work.cell == cell);
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
                ThingDef stuff = parts[3].Length > 0
                    ? DefDatabase<ThingDef>.GetNamedSilentFail(parts[3])
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
                            .TryAuthorizeJob(record, worker, approach,
                                "rebuild " + def.defName + " at " + cell,
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
                    if (approach != null)
                        CABehaviorIntentMapComponent.For(map)?.Unregister(
                            worker, approach);
                }
            }
            return false;
        }

        private bool TryResearch(CARegionalSettlementRecord record,
            Pawn worker, CAOrganization org)
        {
            if (org == null) return false;
            string settlementKey = record.regionalId + "#" + record.slot;
            if (researchWork.Any(work => work != null
                    && work.settlementKey == settlementKey)) return false;
            IntVec3 bench = IntVec3.Invalid;
            foreach (IntVec3 cell in record.localRect)
            {
                if (!cell.InBounds(map)) continue;
                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Building building = things[i] as Building;
                    if (building?.def?.defName != "SimpleResearchBench"
                        || building.Faction != record.faction) continue;
                    bench = building.Position;
                    break;
                }
                if (bench.IsValid) break;
            }
            if (!bench.IsValid || !bench.InBounds(map)) return false;

            bool attended = worker.Position.InHorDistOf(bench, 5f);
            if (!attended)
            {
                if (worker.CanReach(bench, PathEndMode.Touch, Danger.Some))
                {
                    Job approach = JobMaker.MakeJob(JobDefOf.Goto, bench);
                    if (CASettlementInstitutionalAuthorization.TryAuthorizeJob(
                            record, worker, approach,
                            "staff research bench at " + bench,
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
                return false;
            }
            Job study = JobMaker.MakeJob(JobDefOf.Wait, bench);
            study.expiryInterval = 300;
            if (!CASettlementInstitutionalAuthorization.TryAuthorizeJob(record,
                    worker, study, "research at " + bench,
                    out CABehaviorDecision _,
                    out CAIntentContext intent)) return false;
            worker.jobs.StartJob(study, JobCondition.InterruptForced);
            if (worker.CurJob != study)
            {
                CABehaviorIntentMapComponent.For(map)?.Unregister(worker,
                    study);
                return false;
            }
            researchWork.Add(new CASettlementResearchWork
            {
                settlementKey = settlementKey,
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
                        research.behaviorKey, research.episodeId,
                        (CAAuthorityOrigin)research.authorityOrigin,
                        research.authorityIdentity, research.owner,
                        "research at " + research.bench,
                        out researchDecision);
            CABehaviorIntentMapComponent.For(map)?.Unregister(worker, job);
            if (!researchAuthorized) return;
            CAOrganization researchOrg = CAOrganizationWorldComponent.Current
                ?.ByKey(research.settlementKey);
            bool currentBench = researchRecord != null
                && research.bench.GetThingList(map).Any(thing =>
                    thing is Building
                    && thing.def.defName == "SimpleResearchBench"
                    && thing.Faction == researchRecord.faction);
            if (!currentBench || researchOrg == null) return;
            researchRecord.researchStock++;
            researchRecord.lastResearchActivityTick =
                Find.TickManager?.TicksGame ?? -1;
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
            if (record?.faction == null || work == null
                || !work.cell.InBounds(map) || work.defName.NullOrEmpty())
                return false;
            if (work.factionLoadId < 0)
                work.factionLoadId = record.faction.loadID;
            if (record.faction.loadID != work.factionLoadId) return false;

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
                && blueprint.Faction?.loadID == work.factionLoadId;
        }

        private static bool FrameMatchesWork(Frame frame,
            CASettlementRebuildWork work, bool requireBoundIdentity)
        {
            return frame != null && work != null
                && frame.Position == work.cell
                && frame.BuildDef?.defName == work.defName
                && (work.stuffDefName.NullOrEmpty()
                    || frame.Stuff?.defName == work.stuffDefName)
                && frame.Faction?.loadID == work.factionLoadId
                && (!requireBoundIdentity
                    || frame.ThingID == work.frameThingId);
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
                        completed.behaviorKey, completed.episodeId,
                        (CAAuthorityOrigin)completed.authorityOrigin,
                        completed.authorityIdentity, completed.owner,
                        "rebuild " + defName + " at " + cell,
                        out completionDecision);
            if (!authorized) return;
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
            int tier = CASettlementProgramMaterializer.TechTier(record);
            string thing;
            string story;
            switch (record.researchMilestones)
            {
                case 1:
                    thing = tier >= 2 ? "Gun_BoltActionRifle"
                        : "MeleeWeapon_LongSword";
                    story = "their study bears arms - finer weapons join"
                        + " the armory";
                    break;
                case 2:
                    thing = tier >= 2 ? "MedicineIndustrial"
                        : "MedicineHerbal";
                    story = "their study bears healing - the infirmary"
                        + " stocks deeper";
                    break;
                default:
                    thing = tier >= 2 ? "StandingLamp" : "TorchLamp";
                    story = "their study bears light - the halls glow"
                        + " longer into the night";
                    break;
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(thing);
            if (def == null) return;
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
