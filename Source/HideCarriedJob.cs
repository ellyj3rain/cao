using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Carry a thing out of the pawn's own pack to the chosen spot and hide it there.
    public class JobDriver_HideCarried : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var place = new Toil();
            place.initAction = delegate
            {
                var t = job.targetA.Thing;
                if (t == null || pawn.inventory == null) return;
                Thing dropped;
                if (pawn.inventory.innerContainer.TryDrop(t, job.targetB.Cell, pawn.Map,
                    ThingPlaceMode.Direct, out dropped) && dropped != null)
                {
                    HiddenThingsComponent.Stash(dropped, dropped.Position,
                        HiddenRegistry.ConcealmentQualityAt(pawn.Map, dropped.Position));
                }
            };
            place.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return place;
        }
    }
}
