using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Module: drag. Distinct from hauling and from rescue-to-bed: grab a downed body and
    // pull it a short distance FAST - out of the fire lane, behind cover, to where the
    // medic works, toward the extraction point. Rougher than a carry: unskilled hands
    // can bruise the casualty. Buddy carry assists stack on top.
    public class JobDriver_DragTo : JobDriver
    {
        private const TargetIndex Casualty = TargetIndex.A;
        private const TargetIndex Dest = TargetIndex.B;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(Casualty), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(Casualty);
            this.FailOnAggroMentalState(Casualty);
            AddFinishAction(delegate
            {
                var def = DefDatabase<HediffDef>.GetNamedSilentFail("CA_Dragging");
                if (def == null || pawn.health == null) return;
                var h = pawn.health.hediffSet.GetFirstHediffOfDef(def);
                if (h != null) pawn.health.RemoveHediff(h);
            });

            var approach = Toils_Goto.GotoThing(Casualty, PathEndMode.Touch);
            approach.FailOn((System.Func<bool>)delegate
            {
                var t = job.GetTarget(Casualty).Thing as Pawn;
                return t == null || t.Dead || !t.Downed;
            });
            yield return approach;

            var grab = Toils_Haul.StartCarryThing(Casualty);
            grab.AddFinishAction(delegate
            {
                // dragging is quick - the whole point
                var h = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("CA_Dragging"), pawn);
                pawn.health.AddHediff(h);
            });
            yield return grab;

            var haul = Toils_Haul.CarryHauledThingToCell(Dest);
            yield return haul;

            var drop = new Toil();
            drop.initAction = delegate
            {
                Thing outThing;
                pawn.carryTracker.TryDropCarriedThing(job.GetTarget(Dest).Cell, ThingPlaceMode.Near, out outThing);
                // rough handling: unpracticed hands bruise the casualty
                var victim = outThing as Pawn;
                int med = pawn.skills != null ? pawn.skills.GetSkill(SkillDefOf.Medicine).Level : 0;
                if (victim != null && !victim.Dead && med < 4 && Rand.Chance(0.25f))
                    victim.TakeDamage(new DamageInfo(DamageDefOf.Blunt, Rand.RangeInclusive(1, 3)));
            };
            drop.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return drop;
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }

        public override string GetReport()
        {
            var t = job.GetTarget(Casualty).Thing;
            return "dragging " + (t != null ? t.LabelShort : "casualty") + " clear.";
        }
    }

    public static class DragOrders
    {
        // The choose-where half: the targeter, capped at short range - a drag is a pull
        // to cover, not a transport job.
        public static void BeginDragTargeting(Pawn actor, Pawn casualty, Map map)
        {
            var tp = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false
            };
            Find.Targeter.BeginTargeting(tp,
                delegate (LocalTargetInfo targ)
                {
                    IntVec3 c = targ.Cell;
                    if (!c.InBounds(map) || !c.Standable(map)
                        || !c.InHorDistOf(casualty.Position, 15.9f))
                    {
                        Messages.Message("Too far to drag - pulls are short.",
                            MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("CA_DragTo"), casualty, c);
                    job.count = 1;
                    job.locomotionUrgency = LocomotionUrgency.Sprint;
                    actor.jobs.TryTakeOrderedJob(job);
                },
                delegate (LocalTargetInfo targ)
                {
                    bool ok = targ.Cell.InBounds(map) && targ.Cell.Standable(map)
                        && targ.Cell.InHorDistOf(casualty.Position, 15.9f);
                    if (!ok)
                        Widgets.MouseAttachedLabel("too far to drag", 0f, 0f,
                            new UnityEngine.Color(1f, 0.35f, 0.35f));
                });
        }
    }
}
