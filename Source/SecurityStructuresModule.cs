using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Materialize tents, barricades, and lights required by outpost variants
    // after the base settlement layout has been placed. Missing defs or blocked
    // cells are skipped.
    public static class CASecurityStructures
    {
        // techTier follows the settlement program technology tier - 0 tribal,
        // 1 medieval, 2+ industrial: the same boundaries the
        // adapter's material palette cuts on.
        public static void MaterializeVariantExtras(Map map,
            CAMorphResult plan, CellRect rect, Faction faction,
            int techTier)
        {
            try
            {
                if (map == null || plan?.cells == null) return;
                if (plan.outpostVariant < 0) return;
                // The adapter centers its capped plan grid inside
                // the rect; reproduce that mapping from the plan's
                // own dimensions so every cell index lands exactly
                // where the adapter put the walls.
                int offX = rect.minX + (rect.Width - plan.w) / 2;
                int offZ = rect.minZ + (rect.Height - plan.h) / 2;
                var variant = (CAOutpostVariant)plan.outpostVariant;
                if (variant == CAOutpostVariant.TentCamp)
                    PitchTents(map, plan, offX, offZ, faction);
                else if (variant == CAOutpostVariant.Watchpost)
                    RaiseWatchPlatform(map, plan, offX, offZ,
                        faction, techTier);
            }
            catch (Exception e)
            {
                Log.Warning("[CA] security structures failed over "
                    + rect + ": " + e.Message);
            }
        }

        // ---- TENTCAMP: shelter on every footprint ----
        // One packed tent bag per footprint, assembled through the
        // ported machinery's own API (cover and poles packed with
        // PackPart, Ready verified, wrapped in its MinifiedThing).
        // If assembly cannot reach Ready nothing false is spawned:
        // the footprint gets loose tent parts instead - a camp
        // still reads as a camp from its kit on the ground.
        private static void PitchTents(Map map, CAMorphResult plan,
            int offX, int offZ, Faction faction)
        {
            if (plan.tentSpots == null) return;
            for (int i = 0; i < plan.tentSpots.Count; i++)
            {
                IntVec3 cell = CellAt(plan, plan.tentSpots[i],
                    offX, offZ);
                if (!cell.InBounds(map)) continue;
                try
                {
                    if (!TrySpawnPackedTent(map, cell, faction))
                        SpawnLooseTentKit(map, plan, i, offX, offZ, faction);
                }
                catch { }
            }
        }

        private static bool TrySpawnPackedTent(Map map, IntVec3 cell,
            Faction faction)
        {
            try
            {
                ThingDef bagDef = DefDatabase<ThingDef>
                    .GetNamedSilentFail("NCS_TentBag");
                ThingDef miniDef = DefDatabase<ThingDef>
                    .GetNamedSilentFail("NCS_MiniTentBag");
                ThingDef coverDef = DefDatabase<ThingDef>
                    .GetNamedSilentFail("NCS_TentPart_Cover_Small");
                ThingDef poleDef = DefDatabase<ThingDef>
                    .GetNamedSilentFail("NCS_TentPart_Pole");
                ThingDef floorDef = DefDatabase<ThingDef>
                    .GetNamedSilentFail("NCS_TentPart_Floor");
                if (bagDef == null || miniDef == null
                    || coverDef == null || poleDef == null)
                    return false;
                if (!KnownConstruction(faction, bagDef)
                    || !KnownConstruction(faction, miniDef)
                    || !KnownConstruction(faction, coverDef)
                    || !KnownConstruction(faction, poleDef)
                    || (floorDef != null
                        && !KnownConstruction(faction, floorDef)))
                    return false;

                Camping_Stuff.NCS_Tent tent =
                    ThingMaker.MakeThing(bagDef)
                    as Camping_Stuff.NCS_Tent;
                if (tent == null) return false;
                Thing cover = coverDef.MadeFromStuff
                    ? ThingMaker.MakeThing(coverDef,
                        GenStuff.DefaultStuffFor(coverDef))
                    : ThingMaker.MakeThing(coverDef);
                tent.PackPart(cover);
                int needPoles = 2;
                try
                {
                    var props = cover.TryGetComp
                        <Camping_Stuff.TentCoverComp>()?.Props;
                    if (props != null) needPoles = props.numPoles;
                }
                catch { }
                Thing poles = poleDef.MadeFromStuff
                    ? ThingMaker.MakeThing(poleDef,
                        GenStuff.DefaultStuffFor(poleDef))
                    : ThingMaker.MakeThing(poleDef);
                poles.stackCount = needPoles;
                tent.PackPart(poles);
                if (floorDef != null)
                {
                    Thing floor = floorDef.MadeFromStuff
                        ? ThingMaker.MakeThing(floorDef,
                            GenStuff.DefaultStuffFor(floorDef))
                        : ThingMaker.MakeThing(floorDef);
                    tent.PackPart(floor);
                }
                if (!tent.Ready) return false;

                Camping_Stuff.NCS_MiniTent mini =
                    ThingMaker.MakeThing(miniDef)
                    as Camping_Stuff.NCS_MiniTent;
                if (mini == null) return false;
                mini.Bag = tent;
                GenSpawn.Spawn(mini, cell, map);
                mini.SetForbidden(true, false);
                return true;
            }
            catch (Exception) { return false; }
        }

        // The parts path: pole, cover, mat laid out on the same
        // footprint's own cells - never a heap on one square.
        private static void SpawnLooseTentKit(Map map,
            CAMorphResult plan, int footprint, int offX, int offZ,
            Faction faction)
        {
            if (plan.lots == null || footprint >= plan.lots.Count)
                return;
            List<int> cells = plan.lots[footprint]?.cells;
            if (cells == null || cells.Count == 0) return;
            string[] partDefs =
            {
                "NCS_TentPart_Cover_Small", "NCS_TentPart_Pole",
                "NCS_TentPart_Floor"
            };
            for (int i = 0; i < partDefs.Length; i++)
            {
                ThingDef pd = DefDatabase<ThingDef>
                    .GetNamedSilentFail(partDefs[i]);
                if (pd == null || !KnownConstruction(faction, pd)) continue;
                IntVec3 pc = CellAt(plan,
                    cells[(i * 2) % cells.Count], offX, offZ);
                if (!pc.InBounds(map)) continue;
                try
                {
                    Thing part = pd.MadeFromStuff
                        ? ThingMaker.MakeThing(pd,
                            GenStuff.DefaultStuffFor(pd))
                        : ThingMaker.MakeThing(pd);
                    GenSpawn.Spawn(part, pc, map);
                    part.SetForbidden(true, false);
                }
                catch { }
            }
        }

        // ---- WATCHPOST: the platform's ring and light ----
        // The plan floored the pad and recorded its rim (already
        // gapped toward the camp) and the watch point. Low cover
        // rings the rim - barricades of the tier's material, or
        // sandbags once the tier is industrial - and the light
        // marks the point: campfire, torch, floodlight by the same
        // tier boundaries the adapter's palette cuts on.
        private static void RaiseWatchPlatform(Map map,
            CAMorphResult plan, int offX, int offZ, Faction faction,
            int techTier)
        {
            ThingDef cover = techTier >= 2
                ? DefDatabase<ThingDef>.GetNamedSilentFail("Sandbags")
                    ?? DefDatabase<ThingDef>
                        .GetNamedSilentFail("Barricade")
                : DefDatabase<ThingDef>.GetNamedSilentFail("Barricade")
                    ?? DefDatabase<ThingDef>
                        .GetNamedSilentFail("Sandbags");
            ThingDef coverStuff = techTier == 1
                ? DefDatabase<ThingDef>
                    .GetNamedSilentFail("BlocksGranite")
                    ?? DefDatabase<ThingDef>
                        .GetNamedSilentFail("WoodLog")
                : DefDatabase<ThingDef>.GetNamedSilentFail("WoodLog");
            if (cover != null && plan.watchRing != null)
                for (int i = 0; i < plan.watchRing.Count; i++)
                    SpawnSecurityThing(map, cover, coverStuff,
                        CellAt(plan, plan.watchRing[i], offX, offZ),
                        faction);

            // FloodLight verified in Core 1.6 defs; StandingLamp
            // stays behind it for a modlist that removed it.
            ThingDef light = techTier <= 0
                ? DefDatabase<ThingDef>.GetNamedSilentFail("Campfire")
                : techTier == 1
                    ? DefDatabase<ThingDef>
                        .GetNamedSilentFail("TorchLamp")
                    : DefDatabase<ThingDef>
                        .GetNamedSilentFail("FloodLight")
                        ?? DefDatabase<ThingDef>
                            .GetNamedSilentFail("StandingLamp");
            if (light != null && plan.watchPoint >= 0)
                SpawnSecurityThing(map, light, null,
                    CellAt(plan, plan.watchPoint, offX, offZ),
                    faction);
        }

        // ---- shared guarded spawn, the adapter's manner ----
        private static void SpawnSecurityThing(Map map, ThingDef def,
            ThingDef stuff, IntVec3 cell, Faction faction)
        {
            try
            {
                if (def == null || !cell.InBounds(map)) return;
                if (!KnownConstruction(faction, def)) return;
                if (cell.GetEdifice(map) != null) return;
                List<Thing> present = cell.GetThingList(map);
                for (int i = 0; i < present.Count; i++)
                    if (present[i] is Pawn) return;
                Thing thing = def.MadeFromStuff
                    ? ThingMaker.MakeThing(def, stuff
                        ?? GenStuff.DefaultStuffFor(def))
                    : ThingMaker.MakeThing(def);
                if (faction != null && def.CanHaveFaction)
                    thing.SetFaction(faction);
                GenSpawn.Spawn(thing, cell, map);
                var fuel = thing.TryGetComp<CompRefuelable>();
                if (fuel != null) fuel.Refuel(20f);
            }
            catch { }
        }

        private static bool KnownConstruction(Faction faction,
            BuildableDef definition)
        {
            return CATechnologicalKnowledgeRuntime.CanConstructCanonical(
                faction, definition, out _);
        }

        private static IntVec3 CellAt(CAMorphResult plan, int index,
            int offX, int offZ)
        {
            if (index < 0 || plan.w <= 0)
                return IntVec3.Invalid;
            return new IntVec3(offX + index % plan.w, 0,
                offZ + index / plan.w);
        }
    }
}
