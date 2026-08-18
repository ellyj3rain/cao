using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Module 16: children of militant creeds train. War games at the defensive line and
    // practice hunts in the near wilds - native learning activities granting real combat
    // skill XP, years before the engine lets them fight.
    public static class TrainingUtility
    {
        public static bool MilitantChild(Pawn p)
        {
            if (p == null || p.DevelopmentalStage != DevelopmentalStage.Child) return false;
            try
            {
                var ideo = p.Ideo;
                if (ideo == null) return false;
                foreach (var m in ideo.memes)
                {
                    var d = m.defName;
                    if (d == "Raider" || d == "Supremacist" || d == "PainIsVirtue") return true;
                }
            }
            catch { }
            return false;
        }

        public static bool TryFindDrillSpot(Pawn p, out IntVec3 dest)
        {
            dest = IntVec3.Invalid;
            var map = p.Map;
            if (map == null) return false;
            float best = float.MaxValue;
            var buildings = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b.Faction != Faction.OfPlayer) continue;
                if (b.def.fillPercent < 0.3f || b.def.fillPercent > 0.8f) continue;
                var cells = GenAdj.CellsAdjacentCardinal(b);
                foreach (var c in cells)
                {
                    if (!c.InBounds(map) || !c.Standable(map)) continue;
                    if (!p.CanReach(c, PathEndMode.OnCell, Danger.None)) continue;
                    float d = p.Position.DistanceToSquared(c);
                    if (d < best) { best = d; dest = c; }
                }
            }
            return dest.IsValid;
        }
    }

    public class LearningGiver_WarGames : LearningGiver
    {
        public override bool CanDo(Pawn pawn)
        {
            if (!base.CanDo(pawn)) return false;
            if (!TrainingUtility.MilitantChild(pawn)) return false;
            IntVec3 c;
            return TrainingUtility.TryFindDrillSpot(pawn, out c);
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            IntVec3 c;
            if (!TrainingUtility.TryFindDrillSpot(pawn, out c)) return null;
            return JobMaker.MakeJob(def.jobDef, c);
        }
    }

    public class LearningGiver_PracticeHunt : LearningGiver
    {
        public override bool CanDo(Pawn pawn)
        {
            if (!base.CanDo(pawn)) return false;
            if (!TrainingUtility.MilitantChild(pawn)) return false;
            IntVec3 c;
            return RCellFinder.TryFindRandomSpotJustOutsideColony(pawn, out c);
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            IntVec3 c;
            if (!RCellFinder.TryFindRandomSpotJustOutsideColony(pawn, out c)) return null;
            return JobMaker.MakeJob(def.jobDef, c);
        }
    }

    public class JobDriver_PracticeDrill : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            Toil drill = Toils_General.Wait(2500);
            drill.socialMode = RandomSocialMode.Normal;
            drill.tickAction = delegate
            {
                bool hunt = job.def.defName == "CA_PracticeHuntJob";
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Shooting, hunt ? 0.10f : 0.12f);
                    pawn.skills.Learn(hunt ? SkillDefOf.Animals : SkillDefOf.Melee, 0.08f);
                }
                if (pawn.needs != null && pawn.needs.learning != null)
                    pawn.needs.learning.Learn(1.2E-05f);
            };
            yield return drill;
        }
    }
}
