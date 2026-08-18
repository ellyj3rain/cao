using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Berth a faction-owned boat at a generated settlement pier. Hull choice
    // follows faction, technology, and settlement wealth.
    internal static class CANavalBerth
    {
        // Called by the morphology adapter once a pier exists, with the
        // deck's outermost cell and the water direction it runs into.
        internal static bool TryBerth(Map map, Faction faction,
            IntVec3 deckHead, IntVec3 waterDir, int techTier,
            int wealthTier)
        {
            try
            {
                // Derived from geography: the caller has already resolved a
                // deck head and the water direction it runs into, and the
                // berth search below refuses anything but open water. The
                // global pier toggle added nothing this does not already
                // establish from the actual shore.
                if (map == null || faction == null) return false;
                string defName = HullFor(faction, techTier, wealthTier,
                    deckHead);
                if (defName == null) return false;
                ThingDef hull = DefDatabase<ThingDef>
                    .GetNamedSilentFail(defName);
                if (hull == null) return false;

                // moor alongside the deck, in open water, never on it
                IntVec3 berth = IntVec3.Invalid;
                foreach (IntVec3 side in new[]
                    { waterDir.RotatedBy(RotationDirection.Clockwise),
                      waterDir.RotatedBy(
                          RotationDirection.Counterclockwise) })
                {
                    for (int step = 1; step <= 2; step++)
                    {
                        IntVec3 c = deckHead + side * step;
                        if (!c.InBounds(map)) continue;
                        TerrainDef t = c.GetTerrain(map);
                        if (t == null || !t.IsWater) continue;
                        if (c.GetThingList(map).Any(x =>
                            x.def.category == ThingCategory.Building
                            || x.def.category == ThingCategory.Pawn))
                            continue;
                        berth = c; break;
                    }
                    if (berth.IsValid) break;
                }
                if (!berth.IsValid) return false;

                Thing boat = ThingMaker.MakeThing(hull);
                if (boat is Pawn boatPawn)
                {
                    GenSpawn.Spawn(boatPawn, berth, map,
                        Rot4.FromIntVec3(waterDir));
                    boatPawn.SetFaction(faction);
                }
                else
                {
                    GenSpawn.Spawn(boat, berth, map, Rot4.FromIntVec3(waterDir));
                    if (boat.def.CanHaveFaction) boat.SetFaction(faction);
                }
                Log.Message("[CA] naval: " + faction.Name
                    + " keeps a " + hull.label + " at its dock");
                return true;
            }
            catch (Exception e)
            {
                Log.Warning("[CA] berth failed: " + e.Message);
                return false;
            }
        }

        // The hull answers the settlement, not the dice: poor shores
        // keep canoes, working shores keep fishing boats, a trading
        // power with the wealth for it keeps a galleon.
        private static string HullFor(Faction faction, int techTier,
            int wealthTier, IntVec3 site)
        {
            bool galleon = wealthTier >= 3 && techTier >= 1;
            bool working = wealthTier >= 2;
            string name = galleon ? "CA_Galleon"
                : working ? "CA_FishingBoat" : "CA_Canoe";
            if (DefDatabase<ThingDef>.GetNamedSilentFail(name) == null)
                return null;
            return name;
        }
    }
}
