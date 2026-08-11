using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
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

    // Builds the settlement's selected facilities and registers them for
    // repair, rebuilding, and research improvements.
    internal static class CAStartingFacilities
    {
        internal const int MaskHearth = 1;
        internal const int MaskStores = 2;
        internal const int MaskInfirmary = 4;
        internal const int MaskWorkshop = 8;
        internal const int MaskJail = 16;
        internal const int MaskDining = 32;
        internal const int MaskLab = 64;

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

        internal static int DerivedMask(CARegionalSettlementRecord record)
        {
            int tier = TechTier(record);
            int mask = MaskHearth | MaskDining;
            if (record.logistics >= 1) mask |= MaskStores;
            if (record.medicine >= 1) mask |= MaskInfirmary;
            if (record.production >= 1 && tier >= 1) mask |= MaskWorkshop;
            if (record.organization >= 2 && tier >= 1) mask |= MaskJail;
            if (record.production >= 2 && tier >= 1) mask |= MaskLab;
            return mask;
        }

        internal static void Furnish(CAOrganization org,
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
                if (record.seededAssets.Count > 0) return;

                int mask = record.startingFacilityMask >= 0
                    ? record.startingFacilityMask : DerivedMask(record);
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
                rooms = rooms.OrderByDescending(r => r.CellCount).ToList();
                int cursor = 0;
                Room Next()
                {
                    if (rooms.Count == 0) return null;
                    return rooms[cursor++ % rooms.Count];
                }
                // Rooms from the FAR end of the list - spatially distinct
                // from the front-cursor rooms - so a quartered population
                // group has separate facilities.
                int farCursor = 0;
                Room NextFar()
                {
                    if (rooms.Count == 0) return null;
                    return rooms[rooms.Count - 1
                        - (farCursor++ % rooms.Count)];
                }

                if ((mask & MaskStores) != 0)
                {
                    Room r = Next();
                    int laid = 0;
                    laid += Place(org, record, map, r, "Shelf", "WoodLog");
                    laid += Place(org, record, map, r, "Shelf", "WoodLog");
                    if (tier >= 2 && DefDatabase<ThingDef>
                            .GetNamedSilentFail("Anon2FileCabinet") != null)
                        laid += Place(org, record, map, r,
                            "Anon2FileCabinet", "WoodLog");
                    laid += Stock(org, record, map, r, "Pemmican", 75,
                        org?.organizationKey);
                    if (laid > 0)
                        org.Record("provisions", "starting stores"
                            + " - shelving and preserved food");
                }
                // Each organized provision may receive its own kitchen,
                // table, operator organization, water access, and funding.
                // Household provisioning uses the settlement hearth.
                bool arranged = record.startingProvisions != null
                    && record.startingProvisions.Any(item => item != null
                        && item.operatorKind
                            != CAProvisionOperator.Household);
                bool householdProvision = record.startingProvisions != null
                    && record.startingProvisions.Any(item => item != null
                        && item.operatorKind
                            == CAProvisionOperator.Household);
                if (arranged)
                    CAStartingProvisions.Furnish(record, map, tier, Next,
                        NextFar,
                        (rm, def, stuff, providerKey) => Place(org, record,
                            map, rm, def, stuff, providerKey: providerKey),
                        (rm, def, count, providerKey) => Stock(org, record,
                            map, rm, def, count, providerKey));
                if ((!arranged || householdProvision)
                    && (mask & MaskHearth) != 0)
                {
                    Room r = Next();
                    int laid = tier == 0
                        ? Place(org, record, map, r, "Campfire", null)
                        : Place(org, record, map, r, "FueledStove", null)
                          + Place(org, record, map, r, "TableButcher",
                              "WoodLog");
                    if (laid > 0)
                        org.Record("provisions", tier == 0
                            ? "starting hearth built"
                            : "starting kitchen built - stove and butcher table");
                }
                if ((mask & MaskInfirmary) != 0)
                {
                    Room r = Next();
                    string bedDef = tier == 0 ? "Bedroll"
                        : tier == 1 ? "Bed" : "HospitalBed";
                    string medDef = tier >= 2 ? "MedicineIndustrial"
                        : "MedicineHerbal";
                    int laid = Place(org, record, map, r, bedDef, "WoodLog")
                        + Place(org, record, map, r, bedDef, "WoodLog")
                        + Stock(org, record, map, r, medDef,
                            tier >= 2 ? 10 : 12, org?.organizationKey);
                    if (tier >= 2 && DefDatabase<ThingDef>
                            .GetNamedSilentFail("Anon2EndTable") != null)
                        laid += Place(org, record, map, r,
                            "Anon2EndTable", "WoodLog");
                    if (laid > 0)
                        org.Record("care", "starting infirmary - sickbeds"
                            + " and medicine by their own craft");
                }
                if ((!arranged || householdProvision)
                    && (mask & MaskDining) != 0)
                {
                    Room r = Next();
                    // [furniture port] industrial tiers seat the ported
                    // cushioned chairs when present; def-guarded so the
                    // fallback is always vanilla.
                    string seatDef = tier >= 2
                        && DefDatabase<ThingDef>.GetNamedSilentFail(
                            "Anon2CushionedChair") != null
                        ? "Anon2CushionedChair"
                        : tier >= 2 ? "DiningChair" : "Stool";
                    int laid = Place(org, record, map, r, "Table2x2c",
                            "WoodLog")
                        + Place(org, record, map, r, seatDef, "WoodLog")
                        + Place(org, record, map, r, seatDef, "WoodLog");
                    if (laid > 0)
                        org.Record("provisions",
                            "a common table where the people eat");
                }
                if ((mask & MaskWorkshop) != 0)
                {
                    Room r = Next();
                    int laid = tier == 0
                        ? Place(org, record, map, r, "CraftingSpot", null)
                        : Place(org, record, map, r, "FueledSmithy", null);
                    if (laid > 0)
                        org.Record("industry", tier == 0
                            ? "a crafting ground worked by hand"
                            : "smithy stands hot - their own steel");
                }
                if ((mask & MaskJail) != 0 && rooms.Count > 0)
                {
                    Room r = rooms[rooms.Count - 1]; // smallest
                    int laid = Place(org, record, map, r,
                        tier == 0 ? "Bedroll" : "Bed", "WoodLog",
                        forPrisoners: true);
                    if (laid > 0)
                        org.Record("security",
                            "a holding cell kept under law");
                }
                if ((mask & MaskLab) != 0)
                {
                    Room r = Next();
                    int laid = Place(org, record, map, r,
                        "SimpleResearchBench", "WoodLog");
                    if (laid > 0)
                        org.Record("research", "a study bench raised - "
                            + "this settlement develops its own arts");
                }
                Log.Message("[CA] " + (record.name ?? "settlement")
                    + " starting facilities: mask " + mask + ", tier "
                    + tier + ", " + record.seededAssets.Count + " assets");
            }
            catch (Exception e)
            {
                Log.Warning("[CA] starting facilities failed for "
                    + (record?.name ?? "?") + ": " + e.Message);
            }
        }

        private static int Place(CAOrganization org,
            CARegionalSettlementRecord record, Map map, Room room,
            string defName, string stuffName, bool forPrisoners = false,
            string providerKey = null, bool reportFailure = false)
        {
            try
            {
                CASettlementDemandKind demand =
                    CASettlementAssetRegistry.DemandFor(defName);
                ThingDef def = CASettlementAssetRegistry.Resolve(demand,
                    defName);
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
                IntVec3 cell = FreeCell(map, room, record, def, stuff);
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
                var bed = t as Building_Bed;
                if (bed != null && forPrisoners)
                {
                    try { bed.ForPrisoners = true; } catch { }
                }
                record.seededAssets.Add(defName + "|" + cell.x + "|"
                    + cell.z + "|" + (stuff?.defName ?? "") + "|"
                    + (providerKey ?? ""));
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

        private static IntVec3 FreeCell(Map map, Room room,
            CARegionalSettlementRecord record, ThingDef def, ThingDef stuff)
        {
            IEnumerable<IntVec3> source = room != null
                ? room.Cells : record.localRect.Cells
                    .Where(c => c.InBounds(map));
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
    // their own damage, rebuild their destroyed facilities through
    // real blueprints and the vanilla Build duty (the siege-builder
    // machinery), and staff their lab - research accrues only while a
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
                // facility creation is historical evidence, not this gate.
                CAOrganizationInheritance.RefreshDevelopmentAuthority(org,
                    record, map);
                if (!record.developmentExecutable) continue;
                Pawn worker = FindWorker(record);
                if (worker == null) continue;

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

        private void TryResearch(CARegionalSettlementRecord record,
            Pawn worker, CAOrganization org)
        {
            if (org == null) return;
            string settlementKey = record.regionalId + "#" + record.slot;
            if (researchWork.Any(work => work != null
                    && work.settlementKey == settlementKey)) return;
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
            if (!bench.IsValid || !bench.InBounds(map)) return;

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
                    }
                }
                return;
            }
            Job study = JobMaker.MakeJob(JobDefOf.Wait, bench);
            study.expiryInterval = 300;
            if (!CASettlementInstitutionalAuthorization.TryAuthorizeJob(record,
                    worker, study, "research at " + bench,
                    out CABehaviorDecision _,
                    out CAIntentContext intent)) return;
            worker.jobs.StartJob(study, JobCondition.InterruptForced);
            if (worker.CurJob != study)
            {
                CABehaviorIntentMapComponent.For(map)?.Unregister(worker,
                    study);
                return;
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
            int tier = CAStartingFacilities.TechTier(record);
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
