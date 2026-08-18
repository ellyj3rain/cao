using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // GETTING WATER OUT OF THE GROUND AND INTO SOMETHING [Bad Hygiene
    // port, step 2]. Two different acts that look the same and are not:
    //
    //   a thirsty body at a stream drinks, and tops up what it carries
    //   while it is there. That is not logistics, it is a person at a
    //   stream.
    //
    //   drawing water to STORE is work, and it happens because the
    //   place has somewhere to put it. Storage settings are the demand
    //   signal - the same one that decides whether anything else in
    //   this game gets hauled anywhere. Nothing new decides what a
    //   colony wants.
    internal static class CAWaterDraw
    {
        // What comes out of open ground: fresh, and never safe.
        internal const int UnitsPerTrip = 5;

        internal static bool WantsStored(Map map, ThingDef def)
        {
            if (map?.haulDestinationManager == null || def == null)
                return false;
            List<SlotGroup> groups =
                map.haulDestinationManager.AllGroupsListForReading;
            for (int i = 0; i < groups.Count; i++)
            {
                SlotGroup g = groups[i];
                if (g?.Settings == null) continue;
                if (!g.Settings.AllowedToAccept(def)) continue;
                foreach (IntVec3 c in g.CellsList)
                    if (c.GetFirstItem(map) == null) return true;
            }
            return false;
        }

        // Fill everything a pawn is carrying that will take this water.
        internal static int TopUp(Pawn pawn, CAWaterSalinity kind,
            bool foul)
        {
            int filled = 0;
            foreach (CompWaterVessel v in CAVessels.Carried(pawn))
                filled += v.Fill(kind, foul, v.Space);
            return filled;
        }
    }

    // ---- drawing water to store ------------------------------------

    // A WELL IS A PLACE TO DRAW FROM, like the river is. It differs in
    // what it yields, which the ground under it already decided - so
    // nothing here needs to know about wells beyond asking one what it
    // brings up.
    public class WorkGiver_CADrawFromWell : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override bool HasJobOnThing(Pawn pawn, Thing t,
            bool forced = false)
        {
            CompWell well = (t as ThingWithComps)?.GetComp<CompWell>();
            if (well == null || well.Dry) return false;
            ThingDef yields = well.Draws();
            if (yields == null) return false;
            if (!CAWaterDraw.WantsStored(t.Map, yields)) return false;
            if (t.IsForbidden(pawn)) return false;
            return pawn.CanReserve(t, 1, -1, null, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t,
            bool forced = false)
        {
            return JobMaker.MakeJob(CAWaterDefOf.CA_DrawFromWell, t);
        }
    }

    public class JobDriver_CADrawFromWell : JobDriver
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
            Toil draw = Toils_General.Wait(260, TargetIndex.A);
            draw.WithProgressBarToilDelay(TargetIndex.A);
            draw.defaultCompleteMode = ToilCompleteMode.Delay;
            yield return draw;
            Toil made = ToilMaker.MakeToil("CAWellDrawMake");
            made.initAction = delegate
            {
                Pawn p = made.actor;
                CompWell well = (job.targetA.Thing as ThingWithComps)
                    ?.GetComp<CompWell>();
                ThingDef def = well?.Draws();
                if (def == null || well.Dry) return;
                Thing water = ThingMaker.MakeThing(def);
                water.stackCount = well.Yield;
                if (!GenPlace.TryPlaceThing(water, p.Position, p.Map,
                    ThingPlaceMode.Near))
                    water.Destroy();
            };
            made.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return made;
        }
    }

    public class WorkGiver_CADrawWater : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.Nothing);

        // What a given piece of water yields. The ground decides, not
        // the drawer: a bucket lowered into the sea comes up full of
        // sea water however badly it is wanted otherwise.
        internal static ThingDef Yield(IntVec3 c, Map map)
        {
            if (CAWaterGround.IsSalt(c, map))
                return CAWaterDefOf.CA_WaterSaline;
            if (CAWaterGround.IsWater(c, map))
                return CAWaterDefOf.CA_WaterContaminated;
            return null;
        }

        public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(
            Pawn pawn)
        {
            Map map = pawn.Map;
            if (map == null) yield break;
            bool wantFresh = CAWaterDraw.WantsStored(map,
                CAWaterDefOf.CA_WaterContaminated);
            bool wantSalt = CAWaterDraw.WantsStored(map,
                CAWaterDefOf.CA_WaterSaline);
            if (!wantFresh && !wantSalt) yield break;
            foreach (IntVec3 c in map.AllCells)
            {
                if (!CAWaterGround.IsWater(c, map)) continue;
                bool salt = CAWaterGround.IsSalt(c, map);
                if (salt ? wantSalt : wantFresh) yield return c;
            }
        }

        public override bool HasJobOnCell(Pawn pawn, IntVec3 c,
            bool forced = false)
        {
            Map map = pawn.Map;
            if (map == null || !CAWaterGround.IsWater(c, map))
                return false;
            if (c.Fogged(map)) return false;
            ThingDef yields = Yield(c, map);
            if (yields == null) return false;
            if (!CAWaterDraw.WantsStored(map, yields)) return false;
            return pawn.CanReach(c, PathEndMode.Touch, Danger.Some);
        }

        public override Job JobOnCell(Pawn pawn, IntVec3 c,
            bool forced = false)
        {
            return JobMaker.MakeJob(CAWaterDefOf.CA_DrawWater, c);
        }
    }

    public class JobDriver_CADrawWater : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFail)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A,
                PathEndMode.Touch);
            Toil draw = Toils_General.Wait(320);
            draw.WithProgressBarToilDelay(TargetIndex.A);
            draw.defaultCompleteMode = ToilCompleteMode.Delay;
            yield return draw;
            Toil made = ToilMaker.MakeToil("CADrawWaterMake");
            made.initAction = delegate
            {
                Pawn p = made.actor;
                ThingDef def = CAWaterDefOf.CA_WaterContaminated;
                if (def == null) return;
                Thing water = ThingMaker.MakeThing(def);
                water.stackCount = CAWaterDraw.UnitsPerTrip;
                if (!GenPlace.TryPlaceThing(water, p.Position, p.Map,
                    ThingPlaceMode.Near))
                    water.Destroy();
            };
            made.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return made;
        }
    }

    // ---- drinking from what you carry ------------------------------

    public class JobDriver_CADrinkFromVessel : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFail)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil drink = Toils_General.Wait(120);
            drink.defaultCompleteMode = ToilCompleteMode.Delay;
            yield return drink;
            Toil done = ToilMaker.MakeToil("CADrinkVesselFinish");
            done.initAction = delegate
            {
                Pawn p = done.actor;
                CompWaterVessel v = CAVessels.FirstDrinkable(p);
                if (v == null) return;
                bool foul = v.Contaminated;
                if (v.Draw(1) <= 0) return;
                Need thirst = p.needs?.TryGetNeed(
                    CAWaterDefOf.CA_Thirst);
                if (thirst != null) thirst.CurLevel += 0.35f;
                if (!foul) return;
                if (Rand.Value < 0.12f)
                    JobDriver_CADrinkFromSource.Sicken(p,
                        "CA_Dysentery", 0.15f);
                if (Rand.Value < 0.02f)
                    JobDriver_CADrinkFromSource.Sicken(p,
                        "CA_Cholera", 0.10f);
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }
}
