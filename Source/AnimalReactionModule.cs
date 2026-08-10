using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // Knowledge triggers -> animal Disposition decides -> a bounded native job
    // executes. Gunfire remains an approximate sound: this giver never creates an
    // enemy target, resolves a shooter, or repeats a considered pulse.
    public class JobGiver_CAAnimalGunfireReaction : ThinkNode_JobGiver
    {
        private static JobDef responseJob;

        protected override Job TryGiveJob(Pawn pawn)
        {
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || !settings.knowledgeContacts
                || pawn == null || pawn.RaceProps == null
                || !pawn.RaceProps.Animal || pawn.Map == null)
                return null;

            AudibleCueMapComponent cues = AudibleCueMapComponent.For(pawn.Map);
            AudibleCueSnapshot cue;
            if (cues == null || !cues.TryGetPendingCue(pawn, out cue)) return null;

            if (cue.Provenance != CueProvenance.DirectHearing)
            {
                cues.MarkEventConsidered(pawn, cue.EventId);
                return null;
            }

            JobDef def = ResponseJob;
            if (pawn.CurJobDef == def) return null;
            if (pawn.CurJobDef == JobDefOf.Flee
                || pawn.CurJobDef == JobDefOf.FleeAndCower)
            {
                cues.MarkEventConsidered(pawn, cue.EventId);
                return null;
            }
            if (MustPreserveCurrentControl(pawn)) return null;

            // An animal already in real combat has context for the sound. It does
            // not abandon that combat to react to an unidentified report.
            if (AnimalThreatRelevance.IsActiveThreat(pawn))
            {
                cues.MarkEventConsidered(pawn, cue.EventId);
                return null;
            }

            AnimalDispositionProfile disposition = AnimalDisposition.Of(pawn);
            float repetition = Mathf.Min(3, Mathf.Max(0, cue.PulseCount - 1));
            float acoustic = cue.AcousticClass == CueAcousticClass.Heavy ? 0.12f
                : cue.AcousticClass == CueAcousticClass.Quiet ? -0.08f : 0f;
            float reactionChance = Mathf.Clamp01(0.30f
                + disposition.vigilance * 0.45f
                + cue.Confidence * 0.25f
                + repetition * 0.12f
                + acoustic);
            int seed = Gen.HashCombineInt(pawn.thingIDNumber, cue.EventId,
                cue.PulseCount, 358031);

            if (!Rand.ChanceSeeded(reactionChance, seed))
            {
                cues.MarkConsidered(pawn, cue.EventId);
                CATrace.Pawn(pawn, "heard gunfire but did not react (vigilance "
                    + disposition.vigilance.ToString("F2") + ", chance "
                    + reactionChance.ToString("F2") + ")");
                return null;
            }

            IntVec3 destination = pawn.Position;
            string response = "holds alert";
            LocomotionUrgency urgency = LocomotionUrgency.Jog;
            Pawn anchor = AnimalDisposition.AnchorFor(pawn);

            if (anchor != null && disposition.attachment >= 0.55f
                && !pawn.Position.InHorDistOf(anchor.Position, 5f)
                && TryFindRallyCell(pawn, anchor, cue.ApproximateCell,
                    out destination))
            {
                response = "seeks " + anchor.LabelShort;
            }
            else
            {
                float intensity = 0.30f + cue.Confidence * 0.28f
                    + repetition * 0.10f + Mathf.Max(0f, acoustic);
                float flightPressure = intensity
                    + (1f - disposition.nerve) * 0.48f
                    - disposition.defensiveDrive * 0.28f;
                if (flightPressure >= 0.63f
                    && TryFindSafetyCell(pawn, cue.ApproximateCell,
                        out destination))
                {
                    response = "seeks safety";
                    urgency = LocomotionUrgency.Sprint;
                }
            }

            if (destination != pawn.Position
                && !pawn.CanReach(destination, PathEndMode.OnCell, Danger.Deadly))
            {
                destination = pawn.Position;
                response = "holds alert";
                urgency = LocomotionUrgency.Jog;
            }

            Job job = JobMaker.MakeJob(def, destination, cue.ApproximateCell);
            job.count = cue.EventId;
            job.locomotionUrgency = urgency;
            job.expiryInterval = 450;
            job.checkOverrideOnExpire = true;
            job.reportStringOverride = response == "holds alert"
                ? "alert to unexplained gunfire."
                : response + " after unexplained gunfire.";
            CATrace.Pawn(pawn, response + " after direct gunfire (vigilance "
                + disposition.vigilance.ToString("F2") + ", nerve "
                + disposition.nerve.ToString("F2") + ", attachment "
                + disposition.attachment.ToString("F2") + ", defense "
                + disposition.defensiveDrive.ToString("F2") + ")");
            return job;
        }

        private static JobDef ResponseJob
        {
            get
            {
                return responseJob ?? (responseJob =
                    DefDatabase<JobDef>.GetNamed("CA_AnimalGunfireResponse"));
            }
        }

        private static bool MustPreserveCurrentControl(Pawn pawn)
        {
            if (pawn.Downed || pawn.Dead || pawn.InMentalState || pawn.IsBurning()
                || pawn.GetLord() != null || pawn.roping?.IsRoped == true)
                return true;
            if (PawnUtility.PlayerForcedJobNowOrSoon(pawn)) return true;
            if (pawn.mindState != null && pawn.mindState.forcedGotoPosition.IsValid)
                return true;
            if (ThinkNode_ConditionalShouldFollowMaster.ShouldFollowMaster(pawn))
                return true;
            if (pawn.needs != null && pawn.needs.food != null
                && pawn.needs.food.Starving)
                return true;
            if (pawn.jobs != null && pawn.jobs.jobQueue != null
                && pawn.jobs.jobQueue.AnyPlayerForced)
                return true;
            Job current = pawn.CurJob;
            return current != null && current.def.neverFleeFromEnemies;
        }

        private static bool TryFindSafetyCell(Pawn animal, IntVec3 cueCell,
            out IntVec3 destination)
        {
            int currentDistance = animal.Position.DistanceToSquared(cueCell);
            float fleeRadius = Mathf.Sqrt(currentDistance) + 12f;
            if (RCellFinder.TryFindDirectFleeDestination(cueCell, fleeRadius,
                    animal, out destination)
                && destination.DistanceToSquared(cueCell) > currentDistance
                && animal.CanReach(destination, PathEndMode.OnCell,
                    Danger.Deadly))
                return true;

            destination = animal.Position;
            return false;
        }

        private static bool TryFindRallyCell(Pawn animal, Pawn anchor,
            IntVec3 cueCell, out IntVec3 destination)
        {
            int currentDistance = animal.Position.DistanceToSquared(cueCell);
            return CellFinder.TryRandomClosewalkCellNear(anchor.Position,
                animal.Map, 3, out destination, delegate(IntVec3 cell)
                {
                    // Attachment does not smuggle knowledge of a safe shooter:
                    // never run materially closer to the approximate sound merely
                    // because the trusted pawn happens to be there.
                    return (cell == animal.Position
                            || cell.GetFirstPawn(animal.Map) == null)
                        && cell.DistanceToSquared(cueCell) + 4 >= currentDistance
                        && animal.CanReach(cell, PathEndMode.OnCell, Danger.Deadly);
                });
        }
    }

    public class JobDriver_CAAnimalGunfireResponse : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            pawn.Map.pawnDestinationReservationManager.Reserve(
                pawn, job, job.targetA.Cell);
            AudibleCueMapComponent cues = AudibleCueMapComponent.For(pawn.Map);
            if (cues != null) cues.MarkEventConsidered(pawn, job.count);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (pawn.Position != job.targetA.Cell)
                yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            Toil alert = ToilMaker.MakeToil("CAAnimalGunfireAlert");
            alert.initAction = delegate { pawn.pather?.StopDead(); };
            alert.tickAction = delegate
            {
                if (job.targetB.Cell.IsValid)
                    pawn.rotationTracker.FaceCell(job.targetB.Cell);
            };
            alert.handlingFacing = true;
            alert.socialMode = RandomSocialMode.Off;
            alert.defaultDuration = 240;
            alert.defaultCompleteMode = ToilCompleteMode.Delay;
            yield return alert;
        }
    }
}
