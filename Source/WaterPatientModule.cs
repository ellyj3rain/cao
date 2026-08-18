using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // WATER FOR SOMEONE WHO CANNOT GO AND GET IT [Bad Hygiene port,
    // step 2]. A body in a bed still loses water, and a body that
    // cannot walk cannot drink from a stream. Without this, thirst is
    // a death sentence for the wounded - the person you just carried
    // home dies of it while healing.
    //
    // This is vanilla's feed-the-patient arrangement with water in
    // place of food: the same WorkGiver_Scanner shape, the same
    // eligibility rule the engine already uses to decide who is fed in
    // bed (FeedPatientUtility.ShouldBeFed), the same doctor work type.
    // Nothing new is decided about who deserves care.
    public class WorkGiver_CAWaterPatient : WorkGiver_Scanner
    {
        // Below this a patient is thirsty enough to be worth a trip.
        private const float Thirsty = 0.45f;

        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override PathEndMode PathEndMode =>
            PathEndMode.ClosestTouch;

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t,
            bool forced = false)
        {
            Pawn patient = t as Pawn;
            if (patient == null || patient == pawn) return false;
            if (!patient.RaceProps.Humanlike) return false;
            if (!FeedPatientUtility.ShouldBeFed(patient)) return false;
            Need thirst = patient.needs?.TryGetNeed(
                CAWaterDefOf.CA_Thirst);
            if (thirst == null || thirst.CurLevel > Thirsty) return false;
            if (!pawn.CanReserve(patient, 1, -1, null, forced))
                return false;
            return FindWater(pawn, patient) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t,
            bool forced = false)
        {
            Pawn patient = t as Pawn;
            if (patient == null) return null;
            Thing water = FindWater(pawn, patient);
            if (water == null) return null;
            Job job = JobMaker.MakeJob(CAWaterDefOf.CA_WaterPatient,
                water, patient);
            job.count = 1;
            return job;
        }

        // Clean water first; untreated water is better than a dead
        // patient, and a doctor holding only that will use it.
        private Thing FindWater(Pawn carrier, Pawn patient)
        {
            Thing found = Closest(carrier, CAWaterDefOf.CA_WaterPotable);
            return found ?? Closest(carrier, CAWaterDefOf.CA_WaterContaminated);
        }

        private Thing Closest(Pawn carrier, ThingDef def)
        {
            if (def == null || carrier.Map == null) return null;
            return GenClosest.ClosestThingReachable(carrier.Position,
                carrier.Map, ThingRequest.ForDef(def),
                PathEndMode.ClosestTouch, TraverseParms.For(carrier),
                9999f,
                t => !t.IsForbidden(carrier) && carrier.CanReserve(t));
        }
    }

    public class JobDriver_CAWaterPatient : JobDriver
    {
        private Pawn Patient => (Pawn)job.targetB.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFail)
        {
            return pawn.Reserve(job.targetA, job, 1, job.count,
                       null, errorOnFail)
                && pawn.Reserve(job.targetB, job, 1, -1, null,
                       errorOnFail);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);
            this.FailOnForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A,
                PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B,
                PathEndMode.Touch);
            Toil give = Toils_General.Wait(180, TargetIndex.B);
            give.WithProgressBarToilDelay(TargetIndex.B);
            give.FailOnDespawnedNullOrForbidden(TargetIndex.B);
            yield return give;
            Toil done = ToilMaker.MakeToil("CAWaterPatientFinish");
            done.initAction = delegate
            {
                Pawn patient = Patient;
                Thing carried = pawn.carryTracker?.CarriedThing;
                if (patient == null || carried == null) return;
                // the patient drinks it: the item's own ingestion
                // outcomes apply, so untreated water carries exactly
                // the risk it carries when drunk standing up
                carried.Ingested(patient, 0f);
                if (pawn.carryTracker?.CarriedThing != null
                    && pawn.carryTracker.CarriedThing.Destroyed == false
                    && pawn.carryTracker.CarriedThing.stackCount <= 0)
                    pawn.carryTracker.CarriedThing.Destroy();
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }
}
