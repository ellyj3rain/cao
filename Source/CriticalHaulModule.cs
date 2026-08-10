using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Module 2: items that are actively deteriorating and worth caring about get hauled
    // ahead of general hauling. If no storage accepts the item, it is carried to the
    // nearest roofed cell instead of being left to rot in the open.
    public class WorkGiver_HaulCritical : WorkGiver_HaulGeneral
    {
        public override bool Prioritized { get { return true; } }

        public static float AreaDutyBonus(Pawn pawn, IntVec3 cell)
        {
            var s = AwarenessMod.Settings;
            if (s == null || !s.areaDuty) return 0f;
            if (pawn.playerSettings == null) return 0f;
            var area = pawn.playerSettings.AreaRestrictionInPawnCurrentMap;
            if (area == null) return 0f;
            return area[cell] ? 1000f : 0f;
        }

        public override float GetPriority(Pawn pawn, TargetInfo t)
        {
            var thing = t.Thing;
            float crit = thing == null ? 0f : thing.MarketValue * thing.stackCount;
            return crit + AreaDutyBonus(pawn, t.Cell);
        }

        public static bool IsCritical(Thing t)
        {
            if (t == null || t.Destroyed || !t.Spawned) return false;
            if (!t.def.EverHaulable) return false;
            if (SteadyEnvironmentEffects.FinalDeteriorationRate(t) <= 0.1f) return false;
            float total = t.MarketValue * t.stackCount;
            return total >= 75f || t.MarketValue >= 20f;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            var s = AwarenessMod.Settings;
            if (s == null || !s.criticalHauling) yield break;
            var all = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                if (IsCritical(t) && !t.IsForbidden(pawn)) yield return t;
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            var s = AwarenessMod.Settings;
            if (s == null || !s.criticalHauling) return true;
            // Criticality is part of the survival floor and still runs at Directed.
            return base.ShouldSkip(pawn, forced);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.IsForbidden(pawn)) return false;
            // WorkGiver_Scanner requires the scan predicate and materialized job to
            // agree. Pickup eligibility alone does not prove a destination exists.
            return JobOnThing(pawn, t, forced) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!IsCritical(t)) return null;
            Job storage = base.JobOnThing(pawn, t, forced);
            if (storage != null) return storage;
            if (t.Position.Roofed(t.Map)) return null;
            if (!HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, t, forced)) return null;

            Map map = pawn.Map;
            IntVec3 root = t.Position;
            int limit = GenRadial.NumCellsInRadius(30f);
            for (int i = 0; i < limit; i++)
            {
                IntVec3 c = root + GenRadial.RadialPattern[i];
                if (!c.InBounds(map)) continue;
                if (!c.Roofed(map)) continue;
                if (!c.Standable(map)) continue;
                // "Indoors" means INSIDE, not the threshold: a doorway cell is
                // roofed and standable and is exactly where valuables get
                // stolen from. Require a real interior cell.
                if (c.GetDoor(map) != null) continue;
                Room room = c.GetRoom(map);
                if (room == null || room.PsychologicallyOutdoors
                    || room.IsDoorway) continue;
                if (c.IsForbidden(pawn)) continue;
                if (c.GetFirstItem(map) != null) continue;
                if (!pawn.CanReserve(c)) continue;
                Job job = JobMaker.MakeJob(JobDefOf.HaulToCell, t, c);
                job.count = t.stackCount;
                job.haulMode = HaulMode.ToCellNonStorage;
                return job;
            }
            return null;
        }
    }
}
