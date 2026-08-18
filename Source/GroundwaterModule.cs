using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // GROUNDWATER [Bad Hygiene port, step 2 - the boot gate].
    //
    // A well is not a clean-water dispenser. It is a hole reaching an
    // aquifer that was already there, and what comes up is whatever
    // that ground holds. The geography decides, not the building: dig
    // beside the sea and you reach salt; dig in a marsh and you reach
    // water full of what makes marshes; dig in a desert and you may
    // reach nothing at all without going very deep.
    //
    // This derives the aquifer under a cell from conditions the map
    // already has - distance to the sea, distance to fresh surface
    // water, terrain fertility, and the tile's own rainfall - so a
    // coastal settlement genuinely faces the brackish problem the
    // region implied, and a river valley genuinely does not.
    internal struct CAAquifer
    {
        public bool present;
        public CAWaterSalinity salinity;
        public bool contaminated;
        // How much comes up per drawing, before treatment.
        public int yield;
        // How far down it is. Depth requires work and, past a point,
        // machinery - a hand-dug well cannot reach a deep aquifer.
        public int depth;
        public string reason;

        public bool HandDiggable => present && depth <= 5;
    }

    // THESE ARE BALANCE ASSUMPTIONS, NOT GEOLOGY. How far salt pushes
    // inland through the ground, how deep water lies under dry
    // country, how much a well brings up - none of that is a universal
    // law, and burying it in consts would turn one person's guess into
    // permanent, invisible geography. They live here, in one place,
    // readable, and Survey() reports what they imply for any cell so a
    // regional preview can show a player what the ground under a
    // candidate site actually holds BEFORE they commit to it.
    //
    // THEY ARE SNAPSHOTTED PER WORLD. A world keeps the values it was
    // made with, because these decide what is under ground that
    // already has a settlement standing on it. If a later build
    // changed them globally, a well that reached fresh water last
    // session could come up salt this one, and nothing in the world
    // would have moved - which is not a balance change, it is the
    // ground lying.
    public sealed class CAGroundwaterTuning : IExposable
    {
        // How far the sea reaches through the ground. Inside this, a
        // well draws salt; out to brackishReach it draws brackish.
        // This is why a well is not the answer to a salt coast.
        public int saltIntrusion = 18;
        public int brackishReach = 34;
        // Within this of fresh surface water the table stands high.
        public int highTableReach = 30;
        // Wetness thresholds deciding how deep the water lies.
        public float wetShallow = 0.9f;
        public float wetMedium = 0.45f;
        public float wetDeep = 0.2f;
        // How much of what falls on a catchment is kept.
        public float catchmentEfficiency = 0.02f;

        // The values this world was made with, or the defaults when
        // asked before a world exists.
        public static CAGroundwaterTuning Current
        {
            get
            {
                try
                {
                    CAGroundwaterTuning fromWorld =
                        CARegionalWorldComponent.Current?.Groundwater;
                    if (fromWorld != null) return fromWorld;
                }
                catch { }
                return Defaults;
            }
        }

        public static readonly CAGroundwaterTuning Defaults =
            new CAGroundwaterTuning();

        public CAGroundwaterTuning Copy()
        {
            return new CAGroundwaterTuning
            {
                saltIntrusion = saltIntrusion,
                brackishReach = brackishReach,
                highTableReach = highTableReach,
                wetShallow = wetShallow,
                wetMedium = wetMedium,
                wetDeep = wetDeep,
                catchmentEfficiency = catchmentEfficiency
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref saltIntrusion, "saltIntrusion", 18);
            Scribe_Values.Look(ref brackishReach, "brackishReach", 34);
            Scribe_Values.Look(ref highTableReach, "highTableReach", 30);
            Scribe_Values.Look(ref wetShallow, "wetShallow", 0.9f);
            Scribe_Values.Look(ref wetMedium, "wetMedium", 0.45f);
            Scribe_Values.Look(ref wetDeep, "wetDeep", 0.2f);
            Scribe_Values.Look(ref catchmentEfficiency,
                "catchmentEfficiency", 0.02f);
        }

        public string Describe()
        {
            return "salt intrusion " + saltIntrusion
                + "c, brackish reach " + brackishReach
                + "c, high table " + highTableReach + "c";
        }
    }

    internal static class CAGroundwater
    {
        // What the ground under a cell holds, in words - for a preview
        // that has to tell a player whether a site can be lived on.
        public static string Survey(IntVec3 cell, Map map)
        {
            return Report(Under(cell, map), RainfallOf(map));
        }

        // THE SAME QUESTION, ASKED BEFORE THE MAP EXISTS. Setup has no
        // cells to measure, so it passes what the world tile implies:
        // a coastal tile puts salt within reach of the ground, a tile
        // with a river or lake stands the table high, and rainfall and
        // swampiness carry over directly. Coarser inputs, identical
        // decision - so what the preview promises is what the well
        // finds.
        public static string SurveyTile(int tileId,
            CAGroundwaterTuning tuning = null)
        {
            try
            {
                var tile = Find.WorldGrid?[tileId]
                    as RimWorld.Planet.SurfaceTile;
                if (tile == null) return "No survey available.";
                bool coastal = Find.World.CoastDirectionAt(tileId)
                    != Rot4.Invalid;
                bool hasFresh = tile.Rivers != null
                    && tile.Rivers.Count > 0;
                CAGroundwaterTuning t = tuning ?? CAGroundwaterTuning.Current;
                // a coastal tile is salt-reached ground by definition;
                // one with a river stands its table high
                int toSalt = coastal ? t.saltIntrusion : -1;
                int toFresh = hasFresh ? 1 : -1;
                // swampiness is the tile's own standing-water measure,
                // which is what fertility stands in for at cell scale
                CAAquifer a = Decide(toSalt, toFresh, tile.rainfall,
                    0.3f + tile.swampiness * 0.7f, t);
                return Report(a, tile.rainfall);
            }
            catch { return "No survey available."; }
        }

        // What a player needs to know before committing to ground.
        private static string Report(CAAquifer a, float rainfall)
        {
            var s = new System.Text.StringBuilder();
            if (!a.present)
                s.Append("No groundwater: ").Append(a.reason).Append(".");
            else
            {
                ThingDef yields = YieldDef(a);
                s.Append(yields != null
                    ? yields.LabelCap.ToString() : "Water");
                s.Append(" at depth ").Append(a.depth);
                s.Append(", about ").Append(a.yield)
                    .Append("L a drawing. ");
                s.Append(a.HandDiggable
                    ? "A dug well reaches it."
                    : "Needs a bored well.");
                if (a.salinity != CAWaterSalinity.Fresh)
                    s.Append(" Must be distilled before drinking.");
                else if (a.contaminated)
                    s.Append(" Must be boiled before drinking.");
                if (a.yield <= 4) s.Append(" Yield is poor.");
            }

            // rain catchment, on the same page, because the answer to
            // "can anyone live here" is the two of them together
            s.Append("\nRain catch: ");
            if (rainfall >= 900f) s.Append("reliable year round.");
            else if (rainfall >= 500f)
                s.Append("workable; a cistern carries the dry spells.");
            else if (rainfall >= 200f)
                s.Append("seasonal - a cistern is storage, not supply.");
            else s.Append("negligible.");

            bool freshGround = a.present
                && a.salinity == CAWaterSalinity.Fresh;
            if (!freshGround && rainfall < 200f)
                s.Append("\nWARNING: no dependable fresh source here."
                    + " Distillation or imports only.");
            return s.ToString();
        }

        internal static CAAquifer Under(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map))
                return Decide(-1, -1, 500f, 0.5f);
            TerrainDef here = cell.GetTerrain(map);
            return Decide(
                NearestWater(cell, map, true),
                NearestWater(cell, map, false),
                RainfallOf(map),
                here?.fertility ?? 0.5f);
        }

        // THE DECISION ITSELF, taking only the four facts it needs.
        //
        // Both callers come through here: a well standing on a map
        // measures the distances exactly, and a setup preview - which
        // runs BEFORE any map exists - passes what the world tile
        // implies instead. That is the shared path. If the preview
        // computed its own answer it would drift from the ground, and
        // the HandDiggable slip already showed how quietly that
        // happens: the reach moved to five and the survey was still
        // judging by three, so the preview would have told a player to
        // build a bored well they did not need.
        internal static CAAquifer Decide(int toSalt, int toFresh,
            float rain, float fertility,
            CAGroundwaterTuning tuning = null)
        {
            CAGroundwaterTuning t = tuning ?? CAGroundwaterTuning.Current;
            var a = new CAAquifer
            {
                present = false,
                salinity = CAWaterSalinity.Fresh,
                contaminated = true,
                yield = 0,
                depth = 1,
                reason = "no water in this ground"
            };

            // Salt reaches inland through the ground much further than
            // it does across the surface.
            if (toSalt >= 0 && toSalt <= t.saltIntrusion)
            {
                a.present = true;
                a.salinity = CAWaterSalinity.Saline;
                a.contaminated = false;
                a.depth = 2;
                a.yield = 8;
                a.reason = "the sea reaches this ground; the well draws"
                    + " salt";
                return a;
            }
            if (toSalt >= 0 && toSalt <= t.brackishReach)
            {
                a.present = true;
                a.salinity = CAWaterSalinity.Brackish;
                a.contaminated = false;
                a.depth = 3;
                a.yield = 7;
                a.reason = "close enough to the sea that the water"
                    + " tastes of it";
                return a;
            }

            // Fresh surface water nearby means the water table is high
            // and reachable by hand - and full of what the surface has.
            if (toFresh >= 0 && toFresh <= t.highTableReach)
            {
                a.present = true;
                a.salinity = CAWaterSalinity.Fresh;
                a.contaminated = true;
                a.depth = 2;
                a.yield = 10;
                a.reason = "the water table stands high beside the"
                    + " surface water";
                return a;
            }

            // Otherwise the rain and the ground decide. Wet, fertile
            // ground holds water shallowly; dry ground holds it deep
            // or not at all.
            float wetness = rain / 1000f + fertility * 0.4f;
            if (wetness >= t.wetShallow)
            {
                a.present = true;
                a.depth = 3;
                a.yield = 9;
                a.contaminated = true;
                a.reason = "wet ground; water sits close beneath it";
            }
            else if (wetness >= t.wetMedium)
            {
                a.present = true;
                a.depth = 5;
                a.yield = 6;
                a.contaminated = true;
                a.reason = "water lies deep under this ground";
            }
            else if (wetness >= t.wetDeep)
            {
                a.present = true;
                a.depth = 8;
                a.yield = 4;
                a.contaminated = false;
                a.reason = "dry ground; only a deep bore reaches water,"
                    + " and little of it";
            }
            else
            {
                a.present = false;
                a.reason = "this ground is dry all the way down";
            }
            return a;
        }

        // Chebyshev distance to the nearest water of the wanted kind,
        // bounded so a big map does not pay for a full scan. -1 when
        // none is within reach.
        private static int NearestWater(IntVec3 cell, Map map, bool salt)
        {
            const int Limit = 40;
            for (int r = 1; r <= Limit; r++)
            {
                foreach (IntVec3 c in GenRadial.RadialCellsAround(cell,
                    r, false))
                {
                    if (!c.InBounds(map)) continue;
                    if (!CAWaterGround.IsWater(c, map)) continue;
                    bool isSalt = CAWaterGround.IsSalt(c, map);
                    if (isSalt == salt) return r;
                }
            }
            return -1;
        }

        private static float RainfallOf(Map map)
        {
            try { return map.TileInfo?.rainfall ?? 500f; }
            catch { return 500f; }
        }

        internal static ThingDef YieldDef(CAAquifer a)
        {
            if (!a.present) return null;
            if (a.salinity == CAWaterSalinity.Saline)
                return CAWaterDefOf.CA_WaterSaline;
            if (a.salinity == CAWaterSalinity.Brackish)
                return CAWaterDefOf.CA_WaterBrackish;
            return a.contaminated
                ? CAWaterDefOf.CA_WaterContaminated
                : CAWaterDefOf.CA_WaterPotable;
        }
    }

    // THE WELL ITSELF. It reports what it reached, and what it draws
    // is what is down there - the def is decided by the ground, once,
    // when it is built.
    public class CompProperties_Well : CompProperties
    {
        // How deep this construction can reach. A hand-dug shaft
        // cannot follow a water table down a desert.
        public int reach = 3;
        public bool needsPower;

        public CompProperties_Well()
        {
            compClass = typeof(CompWell);
        }
    }

    public class CompWell : ThingComp
    {
        private bool surveyed;
        private bool dry = true;
        private CAWaterSalinity salinity = CAWaterSalinity.Fresh;
        private bool contaminated = true;
        private int yield;
        private string reason;

        public CompProperties_Well Props =>
            (CompProperties_Well)props;

        public bool Dry => dry;
        public int Yield => yield;
        public CAWaterSalinity Salinity => salinity;
        public bool Contaminated => contaminated;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref surveyed, "CA_surveyed", false);
            Scribe_Values.Look(ref dry, "CA_dry", true);
            Scribe_Values.Look(ref salinity, "CA_salinity",
                CAWaterSalinity.Fresh);
            Scribe_Values.Look(ref contaminated, "CA_contaminated",
                true);
            Scribe_Values.Look(ref yield, "CA_yield", 0);
            Scribe_Values.Look(ref reason, "CA_reason");
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (surveyed) return;
            surveyed = true;
            CAAquifer a = CAGroundwater.Under(parent.Position,
                parent.Map);
            reason = a.reason;
            if (!a.present || a.depth > Props.reach)
            {
                dry = true;
                yield = 0;
                if (a.present)
                    reason = "the water here lies deeper than this"
                        + " well can reach";
                Messages.Message("[CA] " + parent.LabelCap + ": "
                    + reason, parent, MessageTypeDefOf.NegativeEvent,
                    false);
                return;
            }
            dry = false;
            salinity = a.salinity;
            contaminated = a.contaminated;
            yield = a.yield;
            Messages.Message("[CA] " + parent.LabelCap + ": " + reason,
                parent, MessageTypeDefOf.NeutralEvent, false);
        }

        public ThingDef Draws()
        {
            if (dry) return null;
            if (salinity == CAWaterSalinity.Saline)
                return CAWaterDefOf.CA_WaterSaline;
            if (salinity == CAWaterSalinity.Brackish)
                return CAWaterDefOf.CA_WaterBrackish;
            return contaminated
                ? CAWaterDefOf.CA_WaterContaminated
                : CAWaterDefOf.CA_WaterPotable;
        }

        public override string CompInspectStringExtra()
        {
            if (dry) return "Dry: " + reason;
            ThingDef d = Draws();
            return "Draws " + yield + "L of "
                + (d != null ? d.label : "water") + " a trip"
                + (string.IsNullOrEmpty(reason) ? "" : "\n" + reason);
        }
    }
}
