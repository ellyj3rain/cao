using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // A standard map saves its frontier realization the first time that map is
    // processed. Regional maps use the holding rows saved on their region plan.
    public sealed class CAFrontierMapPlan : IExposable
    {
        public int mapId = -1;
        public int mapTileId = -1;
        public int mapWidth;
        public int mapHeight;
        public int realizationSourceHash;
        public List<CAFrontierHoldingPlan> holdings =
            new List<CAFrontierHoldingPlan>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref mapTileId, "mapTileId", -1);
            Scribe_Values.Look(ref mapWidth, "mapWidth", 0);
            Scribe_Values.Look(ref mapHeight, "mapHeight", 0);
            Scribe_Values.Look(ref realizationSourceHash,
                "realizationSourceHash", 0);
            Scribe_Collections.Look(ref holdings, "holdings", LookMode.Deep);
            if (holdings == null)
                holdings = new List<CAFrontierHoldingPlan>();
        }
    }

    public static class CAFrontier
    {
        private static int HoldingsFor(CAOrganizationWorldComponent comp,
            Map map)
        {
            CARegionalPlan plan = CARegionalWorldComponent.Current
                ?.FindRegionForMap(map);
            if (plan?.frontierHoldings != null)
                return Mathf.Clamp(plan.frontierHoldings.Count, 0, 8);
            CAFrontierMapPlan mapPlan = comp?.EnsureFrontierMapPlan(map);
            return Mathf.Clamp(mapPlan?.holdings?.Count ?? 0, 0, 8);
        }

        private static CAFrontierHoldingPlan HoldingPlanFor(
            CAOrganizationWorldComponent comp, Map map, int index)
        {
            CARegionalPlan plan = CARegionalWorldComponent.Current
                ?.FindRegionForMap(map);
            if (plan?.frontierHoldings != null
                && index >= 0 && index < plan.frontierHoldings.Count)
                return plan.frontierHoldings[index];
            CAFrontierMapPlan mapPlan = comp?.EnsureFrontierMapPlan(map);
            return mapPlan?.holdings != null && index >= 0
                && index < mapPlan.holdings.Count
                    ? mapPlan.holdings[index] : null;
        }

        public static void EnsureHoldings(CAOrganizationWorldComponent comp)
        {
            if (comp == null || Current.Game == null) return;
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                if (!map.IsPlayerHome) continue;
                int existing = 0;
                for (int index = 0; index < HoldingsFor(comp, map); index++)
                {
                    CAFrontierHoldingPlan holding = HoldingPlanFor(comp, map,
                        index);
                    if (holding?.materialized == true
                        && holding.materializedMapId == map.uniqueID)
                        existing++;
                }
                int want = HoldingsFor(comp, map);
                // New maps seed saved holdings promptly. Older maps add one
                // saved holding per pulse after the initial delay.
                if (existing < want && Find.TickManager.TicksGame
                    - map.generationTick < 20000)
                {
                    // [perf] Frontier holdings are still generated with the map,
                    // but two holdings per pulse instead of eight in
                    // one tick: each site costs a reachability flood
                    // across the whole map, and on a two-million-cell
                    // region eight of those in a single frame is a
                    // visible freeze. Four pulses inside the window
                    // seat the full complement.
                    var taken = new List<IntVec3>();
                    int spawned = 0;
                    for (int index = 0; index < want && spawned < 2;
                        index++)
                    {
                        CAFrontierHoldingPlan holding = HoldingPlanFor(comp,
                            map, index);
                        if (holding == null || holding.materialized) continue;
                        if (!TrySeedHolding(map, holding, taken)) break;
                        spawned++;
                    }
                }
                else if (existing < want
                    && Find.TickManager.TicksGame > 120000)
                {
                    for (int index = 0; index < want; index++)
                    {
                        CAFrontierHoldingPlan holding = HoldingPlanFor(comp,
                            map, index);
                        if (holding == null || holding.materialized) continue;
                        TrySeedHolding(map, holding);
                        break;
                    }
                }
            }
        }

        private static bool TrySeedHolding(Map map,
            CAFrontierHoldingPlan holding, List<IntVec3> taken = null)
        {
            if (holding == null) return false;
            bool factionless = holding.factionless;
            Faction flag = factionless ? null : PickFlag();
            if (!factionless && flag == null) factionless = true;
            IntVec3 site;
            IntVec3 preferred = IntVec3.Invalid;
            if (holding != null && holding.memberTileId >= 0)
                preferred = map.GetComponent<CARegionalProjectionMapComponent>()
                    ?.CenterForMember(holding.memberTileId)
                    ?? IntVec3.Invalid;
            int siteSeed = Gen.HashCombineInt(map.uniqueID,
                holding.key, holding.memberTileId,
                509203);
            Rand.PushState(siteSeed);
            try
            {
                if (!TryFindSite(map, out site, taken, preferred)) return false;
            }
            finally { Rand.PopState(); }

            var folk = new List<Pawn>();
            int count = Mathf.Clamp(holding.residentCount, 1, 6);
            PawnKindDef kind = PawnKindDefOf.Villager;
            if (flag != null)
                try
                {
                    PawnKindDef fk = flag.RandomPawnKind();
                    if (fk != null && fk.RaceProps != null
                        && fk.RaceProps.Humanlike) kind = fk;
                }
                catch { }
            for (int i = 0; i < count; i++)
            {
                try
                {
                    Pawn p = PawnGenerator.GeneratePawn(kind, flag);
                    IntVec3 spot;
                    if (!CellFinder.TryFindRandomCellNear(site, map, 4,
                        c => c.Standable(map) && !c.Fogged(map),
                        out spot)) spot = site;
                    GenSpawn.Spawn(p, spot, map);
                    folk.Add(p);
                }
                catch { }
            }
            if (folk.Count == 0) return false;
            taken?.Add(site);

            // Form selects the physical layout. Material level adds saved
            // furnishings and storage independently of faction ownership.
            bool materialized = false;
            if (flag != null)
                try
                {
                    CAMorphologyAdapter.Materialize(map,
                        CellRect.CenteredOn(site, 26, 26)
                            .ClipInsideMap(map),
                        holding.form == 1
                            ? CAMorphForm.FrontierHomestead
                            : CAMorphForm.Cabin,
                        site.GetHashCode(), flag);
                    materialized = true;
                }
                catch { }
            if (materialized)
                SpawnMaterialDetails(map, site, flag, holding.materialLevel);
            else
                SpawnHomestead(map, site, flag, folk.Count,
                    holding.materialLevel, holding.form);

            var residentIds = new List<int>();
            for (int i = 0; i < folk.Count; i++)
                residentIds.Add(folk[i].thingIDNumber);
            holding.materialized = true;
            holding.materializedMapId = map.uniqueID;
            holding.site = site;
            holding.residentPawnIds = residentIds;

            Messages.Message((factionless
                ? "An unaffiliated frontier site has been established nearby"
                : "A frontier site affiliated with " + flag.Name
                    + " has been established nearby")
                + ": " + folk.Count + (folk.Count == 1
                    ? " resident." : " residents."),
                new LookTargets(site, map),
                MessageTypeDefOf.NeutralEvent, false);
            return true;
        }

        // Battlefield parley consumes this faction-side reading of an
        // unarmed approach. It does not create frontier social state.
        internal static float ReadUnarmed(Faction aggressor, out string note)
        {
            note = null;
            if (aggressor?.def == null) return 0f;
            try
            {
                if (aggressor.def.permanentEnemy
                    || !aggressor.def.humanlikeFaction)
                {
                    note = "they read an unarmed figure as prey -0.10";
                    return -0.1f;
                }
                if (aggressor.def.naturalEnemy)
                {
                    note = "they read it as weakness -0.05";
                    return -0.05f;
                }
                Ideo ideo = aggressor.ideos?.PrimaryIdeo;
                if (ideo != null)
                    for (int i = 0; i < ideo.memes.Count; i++)
                    {
                        string defName = ideo.memes[i].defName;
                        if (defName == "Raider" || defName == "Supremacist")
                        {
                            note = "their creed reads it as weakness -0.10";
                            return -0.1f;
                        }
                    }
                note = "they read it as good faith +0.10";
                return 0.1f;
            }
            catch
            {
                note = null;
                return 0f;
            }
        }

        private static Faction PickFlag()
        {
            List<Faction> all = Find.FactionManager
                .AllFactionsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                Faction f = all[i];
                if (f.IsPlayer || f.defeated || f.Hidden
                    || f.temporary) continue;
                if (f.def == null || !f.def.humanlikeFaction) continue;
                try
                {
                    if (f.PlayerRelationKind
                        == FactionRelationKind.Hostile) continue;
                }
                catch { continue; }
                return f;
            }
            return null;
        }

        private static bool TryFindSite(Map map, out IntVec3 site,
            List<IntVec3> taken = null,
            IntVec3 preferredCenter = default(IntVec3))
        {
            site = IntVec3.Invalid;
            IntVec3 home = map.Center;
            try
            {
                var cols = map.mapPawns.FreeColonistsSpawned;
                if (cols.Count > 0) home = cols[0].Position;
            }
            catch { }
            CARegionalWorldComponent regional =
                CARegionalWorldComponent.Current;
            // Reachability floods are the expensive check on a giant map:
            // fewer attempts there, and every cheap filter runs first.
            int maxTries = map.Size.x * map.Size.z > 1000000 ? 80 : 220;
            for (int tries = 0; tries < maxTries; tries++)
            {
                IntVec3 c = IntVec3.Invalid;
                if (preferredCenter.IsValid
                    && !CellFinder.TryFindRandomCellNear(preferredCenter,
                        map, Math.Max(35,
                            Math.Min(map.Size.x, map.Size.z) / 4),
                        cell => cell.InBounds(map), out c))
                    c = CellFinder.RandomCell(map);
                else if (!preferredCenter.IsValid)
                    c = CellFinder.RandomCell(map);
                if (!c.Standable(map) || c.Fogged(map)) continue;
                if (c.Roofed(map)) continue;
                if (c.DistanceTo(home) < 60f) continue;
                // Birth batches pick several sites in one pass: keep
                // sibling homesteads off each other's ground.
                bool crowded = false;
                if (taken != null)
                    for (int i = 0; i < taken.Count; i++)
                        if (c.InHorDistOf(taken[i], 40f))
                        { crowded = true; break; }
                if (crowded) continue;
                bool nearSettlement = false;
                if (regional != null)
                    for (int i = 0; i < regional.Records.Count; i++)
                    {
                        CARegionalSettlementRecord r = regional.Records[i];
                        if (r.lastMapId != map.uniqueID
                            || r.localRect == CellRect.Empty) continue;
                        if (r.localRect.ExpandedBy(35).Contains(c))
                        { nearSettlement = true; break; }
                    }
                if (nearSettlement) continue;
                if (!map.reachability.CanReachMapEdge(c,
                    TraverseParms.For(TraverseMode.PassDoors))) continue;
                site = c;
                return true;
            }
            return false;
        }

        private static void SpawnHomestead(Map map, IntVec3 site,
            Faction flag, int residentCount, int materialLevel, int form)
        {
            if (form == 1) SpawnEstablishedShell(map, site, flag);
            TrySpawn(map, site, "Campfire", flag, null, 20f);
            ThingDef bedroll = DefDatabase<ThingDef>.GetNamedSilentFail(
                "Bedroll");
            ThingDef cloth = DefDatabase<ThingDef>.GetNamedSilentFail(
                "Cloth");
            for (int i = 1; i <= Math.Max(1, residentCount); i++)
            {
                int radial = Math.Min(GenRadial.NumCellsInRadius(4f) - 1,
                    i * 2);
                IntVec3 c = site + GenRadial.RadialPattern[radial];
                if (bedroll != null) TrySpawnDef(map, c, bedroll, flag,
                    cloth, 0f);
            }
            SpawnMaterialDetails(map, site, flag, materialLevel);
            ThingDef mini = DefDatabase<ThingDef>.GetNamedSilentFail(
                "NCS_TentBag");
            if (mini != null)
            {
                ThingDef pole = DefDatabase<ThingDef>.GetNamedSilentFail(
                    "NCS_TentPart_Pole");
                ThingDef cover = DefDatabase<ThingDef>.GetNamedSilentFail(
                    "NCS_TentPart_Cover_Small");
                IntVec3 c = site + GenRadial.RadialPattern[7];
                if (pole != null) TrySpawnDef(map, c, pole, null, null, 0f);
                if (cover != null) TrySpawnDef(map,
                    site + GenRadial.RadialPattern[8], cover, null, null,
                    0f);
            }
        }

        private static void SpawnEstablishedShell(Map map, IntVec3 site,
            Faction flag)
        {
            ThingDef wall = DefDatabase<ThingDef>.GetNamedSilentFail("Wall");
            ThingDef door = DefDatabase<ThingDef>.GetNamedSilentFail("Door");
            ThingDef wood = DefDatabase<ThingDef>.GetNamedSilentFail("WoodLog");
            if (wall == null) return;
            for (int dx = -3; dx <= 3; dx++)
                for (int dz = -3; dz <= 3; dz++)
                {
                    if (Math.Abs(dx) != 3 && Math.Abs(dz) != 3) continue;
                    IntVec3 cell = site + new IntVec3(dx, 0, dz);
                    if (dx == 0 && dz == -3 && door != null)
                        TrySpawnDef(map, cell, door, flag, wood, 0f);
                    else
                        TrySpawnDef(map, cell, wall, flag, wood, 0f);
                }
        }

        private static void SpawnMaterialDetails(Map map, IntVec3 site,
            Faction flag, int materialLevel)
        {
            ThingDef wood = DefDatabase<ThingDef>.GetNamedSilentFail(
                "WoodLog");
            if (materialLevel >= 1)
                TrySpawnNearby(map, site, "Stool", flag, wood, 0f, 5);
            if (materialLevel >= 2)
                TrySpawnNearby(map, site, "Table1x2c", flag, wood, 0f, 11);
            if (materialLevel >= 3)
            {
                TrySpawnNearby(map, site, "Shelf", flag, wood, 0f, 17);
                TrySpawnNearby(map, site, "TorchLamp", flag, wood, 20f, 23);
            }
        }

        private static bool TrySpawn(Map map, IntVec3 c, string defName,
            Faction faction, ThingDef stuff, float fuel)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(
                defName);
            return def != null
                && TrySpawnDef(map, c, def, faction, stuff, fuel);
        }

        private static void TrySpawnNearby(Map map, IntVec3 center,
            string defName, Faction faction, ThingDef stuff, float fuel,
            int start)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null) return;
            int cells = GenRadial.NumCellsInRadius(7f);
            for (int offset = 0; offset < cells; offset++)
            {
                int index = 1 + (start + offset) % Math.Max(1, cells - 1);
                if (TrySpawnDef(map, center + GenRadial.RadialPattern[index],
                        def, faction, stuff, fuel))
                    return;
            }
        }

        private static bool TrySpawnDef(Map map, IntVec3 c, ThingDef def,
            Faction faction, ThingDef stuff, float fuel)
        {
            try
            {
                if (!c.InBounds(map) || !c.Standable(map)) return false;
                if (c.GetEdifice(map) != null) return false;
                Thing t = def.MadeFromStuff
                    ? ThingMaker.MakeThing(def, stuff
                        ?? GenStuff.DefaultStuffFor(def))
                    : ThingMaker.MakeThing(def);
                if (faction != null && def.CanHaveFaction)
                    t.SetFaction(faction);
                GenSpawn.Spawn(t, c, map);
                if (fuel > 0f)
                {
                    var comp = t.TryGetComp<CompRefuelable>();
                    if (comp != null) comp.Refuel(fuel);
                }
                return true;
            }
            catch { return false; }
        }
    }
}
