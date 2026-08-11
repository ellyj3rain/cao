using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
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
                ThingDef def = DefDatabase<ThingDef>
                    .GetNamedSilentFail(defName);
                if (def == null) return 0;
                IntVec3 cell = FreeCell(map, room, record, def);
                if (!cell.IsValid) return 0;
                ThingDef stuff = def.MadeFromStuff && stuffName != null
                    ? DefDatabase<ThingDef>.GetNamedSilentFail(stuffName)
                    : null;
                if (def.MadeFromStuff && stuff == null)
                    stuff = GenStuff.DefaultStuffFor(def);
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
                IntVec3 cell = FreeCell(map, room, record, def);
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
            CARegionalSettlementRecord record, ThingDef def)
        {
            IEnumerable<IntVec3> source = room != null
                ? room.Cells : record.localRect.Cells
                    .Where(c => c.InBounds(map));
            foreach (IntVec3 c in source)
            {
                if (!c.Standable(map)) continue;
                if (c.GetEdifice(map) != null) continue;
                if (c.GetThingList(map).Any(t =>
                    t.def.category == ThingCategory.Building
                    || t.def.category == ThingCategory.Item)) continue;
                bool fits = true;
                foreach (IntVec3 o in GenAdj.OccupiedRect(c, Rot4.South,
                    def.size))
                    if (!o.InBounds(map) || !o.Standable(map)
                        || o.GetEdifice(map) != null)
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

        public CASettlementWorksMapComponent(Map map) : base(map) { }

        public override void MapComponentTick()
        {
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
            foreach (IntVec3 c in record.localRect)
            {
                if (!c.InBounds(map)) continue;
                Building b = c.GetEdifice(map);
                if (b == null || b.Faction != record.faction) continue;
                if (b.HitPoints >= b.MaxHitPoints
                    || !b.def.useHitPoints) continue;
                if (!worker.CanReserveAndReach(b, PathEndMode.Touch,
                    Danger.Some)) continue;
                Job job = JobMaker.MakeJob(JobDefOf.Repair, b);
                worker.jobs.StartJob(job, JobCondition.InterruptForced);
                org?.Record("works", "repairs under way - "
                    + b.LabelShort + " repaired by settlement residents");
                return true;
            }
            return false;
        }

        private bool TryRebuild(CARegionalSettlementRecord record,
            Pawn worker, CAOrganization org)
        {
            if (record.seededAssets == null) return false;
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
                bool standing = cell.GetThingList(map).Any(t =>
                    t.def == def
                    || (t is Blueprint_Build bp && bp.def.entityDefToBuild
                        == def)
                    || (t is Frame f && f.def.entityDefToBuild == def));
                if (standing) continue;
                if (!cell.Standable(map)) continue;
                ThingDef stuff = parts[3].Length > 0
                    ? DefDatabase<ThingDef>.GetNamedSilentFail(parts[3])
                    : (def.MadeFromStuff
                        ? GenStuff.DefaultStuffFor(def) : null);
                try
                {
                    GenConstruct.PlaceBlueprintForBuild(def, cell, map,
                        Rot4.South, record.faction, stuff);
                    worker.mindState.duty = new PawnDuty(DutyDefOf.Build,
                        cell) { radius = 12f };
                    worker.jobs?.CheckForJobOverride();
                    org?.Record("works", "rebuilding what was lost - "
                        + def.label + " rises again");
                    return true;
                }
                catch { }
            }
            return false;
        }

        private void TryResearch(CARegionalSettlementRecord record,
            Pawn worker, CAOrganization org)
        {
            if (record.seededAssets == null || org == null) return;
            IntVec3 bench = IntVec3.Invalid;
            for (int i = 0; i < record.seededAssets.Count; i++)
                if (record.seededAssets[i].StartsWith("SimpleResearchBench"))
                {
                    string[] parts = record.seededAssets[i].Split('|');
                    int x, z;
                    if (int.TryParse(parts[1], out x)
                        && int.TryParse(parts[2], out z))
                        bench = new IntVec3(x, 0, z);
                    break;
                }
            if (!bench.IsValid || !bench.InBounds(map)) return;
            bool benchAlive = bench.GetThingList(map)
                .Any(t => t.def.defName == "SimpleResearchBench");
            if (!benchAlive) return;

            var pawns = map.mapPawns.SpawnedPawnsInFaction(record.faction);
            bool attended = pawns.Any(p => p != null && !p.Downed
                && p.Position.InHorDistOf(bench, 5f));
            if (!attended)
            {
                if (worker.CanReach(bench, PathEndMode.Touch, Danger.Some))
                    worker.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto,
                        bench), JobCondition.InterruptForced);
                return;
            }
            record.researchStock++;
            if (record.researchStock == 25
                || record.researchStock == 75
                || record.researchStock == 150)
            {
                record.researchMilestones++;
                MaterializeMilestone(record, org);
            }
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
}
