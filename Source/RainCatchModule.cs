using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // RAIN CATCHMENT [Bad Hygiene port, step 2 - the last closure].
    //
    // A cistern is not a source. It is STORAGE with a lid off, and
    // what it holds is what fell into it. That distinction is the
    // whole design: in wet country it is a convenience, and in dry
    // country it is the difference between planning and dying - you
    // fill it in the wet season and you live out of it in the dry one.
    //
    // It cannot be a free substitute for groundwater because:
    //   it collects only while it is actually raining, at a rate set
    //   by the weather and by how much sky it is open to;
    //   it holds a finite amount and then overflows;
    //   what it catches is open water, so it is contaminated, and
    //   standing in an open vessel it goes bad on its own - the
    //   spoilage the vessel model already applies to buckets;
    //   a roof over it collects nothing, which is not a rule but a
    //   fact about roofs.
    public class CompProperties_RainCatch : CompProperties
    {
        // How much of what falls on its footprint it actually keeps.
        // A cistern with a wider apron catches more than its own
        // square, which is what a catchment IS.
        public float catchmentFactor = 0.02f;
        // Extra roof area feeding it, in cells, beyond its footprint.
        public int apronCells;

        public CompProperties_RainCatch()
        {
            compClass = typeof(CompRainCatch);
        }
    }

    public class CompRainCatch : ThingComp
    {
        private const int Interval = 250;
        private float pending;
        private int lastRainTick = -99999;

        public CompProperties_RainCatch Props =>
            (CompProperties_RainCatch)props;

        private CompWaterVessel Vessel =>
            parent.TryGetComp<CompWaterVessel>();

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref pending, "CA_pending", 0f);
            Scribe_Values.Look(ref lastRainTick, "CA_lastRain", -99999);
        }

        public int Cells
        {
            get
            {
                int foot = parent.def.size.x * parent.def.size.z;
                return foot + Props.apronCells;
            }
        }

        public override void CompTick()
        {
            if (!parent.IsHashIntervalTick(Interval)) return;
            CompWaterVessel v = Vessel;
            Map map = parent.Map;
            if (v == null || map == null) return;
            // a roof over it catches nothing. Not a rule - a roof.
            if (parent.Position.Roofed(map)) return;

            float rain = 0f;
            try { rain = map.weatherManager.RainRate; }
            catch { rain = 0f; }
            if (rain <= 0.05f) return;
            lastRainTick = Find.TickManager.TicksGame;

            pending += Cells * rain * Props.catchmentFactor;
            if (pending < 1f) return;
            int litres = Mathf.FloorToInt(pending);
            pending -= litres;
            // Rain off an open apron is fresh and filthy. It keeps a
            // body alive and it wants boiling, same as a stream.
            v.Fill(CAWaterSalinity.Fresh, true, litres);
        }

        public override string CompInspectStringExtra()
        {
            int dry = Find.TickManager != null
                ? (Find.TickManager.TicksGame - lastRainTick) / 60000
                : 0;
            string s = "Catchment: " + Cells + " cells";
            if (parent.Spawned && parent.Position.Roofed(parent.Map))
                return s + "\nRoofed - catching nothing";
            if (lastRainTick < 0) return s + "\nNo rain caught yet";
            if (dry >= 1) s += "\nDry for " + dry + " days";
            return s;
        }
    }

    // Taking water OUT of a cistern, into the bulk the recipes and the
    // stockpiles use. A cistern that nobody empties simply fills and
    // sits there, which is the correct behaviour for a barrel.
    public class WorkGiver_CADrawFromCistern : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override bool HasJobOnThing(Pawn pawn, Thing t,
            bool forced = false)
        {
            CompWaterVessel v =
                (t as ThingWithComps)?.GetComp<CompWaterVessel>();
            if (v == null || v.Empty) return false;
            if ((t as ThingWithComps)?.GetComp<CompRainCatch>() == null)
                return false;
            ThingDef def = CAWaterBulk.DefFor(v.Salinity,
                v.Contaminated);
            if (def == null) return false;
            if (!CAWaterDraw.WantsStored(t.Map, def)) return false;
            if (t.IsForbidden(pawn)) return false;
            return pawn.CanReserve(t, 1, -1, null, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t,
            bool forced = false)
        {
            return JobMaker.MakeJob(CAWaterDefOf.CA_DrawFromCistern, t);
        }
    }

    public class JobDriver_CADrawFromCistern : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFail)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null,
                errorOnFail);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A,
                PathEndMode.Touch);
            Toil draw = Toils_General.Wait(200, TargetIndex.A);
            draw.WithProgressBarToilDelay(TargetIndex.A);
            draw.defaultCompleteMode = ToilCompleteMode.Delay;
            yield return draw;
            Toil made = ToilMaker.MakeToil("CACisternDraw");
            made.initAction = delegate
            {
                Pawn p = made.actor;
                CompWaterVessel v = (job.targetA.Thing as ThingWithComps)
                    ?.GetComp<CompWaterVessel>();
                if (v == null || v.Empty) return;
                ThingDef def = CAWaterBulk.DefFor(v.Salinity,
                    v.Contaminated);
                if (def == null) return;
                int took = v.Draw(Mathf.Min(25, v.Units));
                if (took <= 0) return;
                Thing water = ThingMaker.MakeThing(def);
                water.stackCount = took;
                if (!GenPlace.TryPlaceThing(water, p.Position, p.Map,
                    ThingPlaceMode.Near))
                    water.Destroy();
            };
            made.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return made;
        }
    }

    // One place that maps the two water axes onto the bulk item, so
    // wells, cisterns and streams never disagree about what a given
    // state of water is called.
    internal static class CAWaterBulk
    {
        internal static ThingDef DefFor(CAWaterSalinity salinity,
            bool contaminated)
        {
            switch (salinity)
            {
                case CAWaterSalinity.Saline:
                    return CAWaterDefOf.CA_WaterSaline;
                case CAWaterSalinity.Brackish:
                    return CAWaterDefOf.CA_WaterBrackish;
                default:
                    return contaminated
                        ? CAWaterDefOf.CA_WaterContaminated
                        : CAWaterDefOf.CA_WaterPotable;
            }
        }
    }
}
