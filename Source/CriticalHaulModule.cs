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
            // Criticality is part of the survival floor and still runs at Standard.
            return base.ShouldSkip(pawn, forced);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.IsForbidden(pawn) || !IsCritical(t)) return false;
            if (base.HasJobOnThing(pawn, t, forced)) return true;
            IntVec3 fallback;
            return TryFindFallbackCell(pawn, t, forced, out fallback);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!IsCritical(t)) return null;
            Job storage = base.JobOnThing(pawn, t, forced);
            Job job = storage;
            if (job == null)
            {
                IntVec3 cell;
                if (!TryFindFallbackCell(pawn, t, forced, out cell))
                    return null;
                job = JobMaker.MakeJob(JobDefOf.HaulToCell, t, cell);
                job.count = t.stackCount;
                job.haulMode = HaulMode.ToCellNonStorage;
            }
            CAAuthorityOrigin origin = forced
                ? CAAuthorityOrigin.OperatorDirect
                : CAAuthorityOrigin.NativeDuty;
            var context = CABehaviorContext.ForPawn(pawn, origin,
                authoritySatisfied: true,
                capabilitySatisfied: pawn != null && job != null,
                materialSatisfied: job != null,
                directPlayerOwnership: !forced && pawn?.CurJob != null
                    && pawn.CurJob.playerForced,
                authorityBasis: forced ? "direct hauling order"
                    : "enabled native hauling work",
                owner: "native hauling work");
            CABehaviorDecision decision;
            CAIntentContext intent;
            return CABehaviorJobOrigin.TryRegisterNativeExecution(pawn, job,
                "logistics.critical_haul", CAIntentController.Unknown, context,
                out decision, out intent, t.LabelShort,
                "native hauling work", 2500) ? job : null;
        }

        private static bool TryFindFallbackCell(Pawn pawn, Thing t,
            bool forced, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (pawn?.Map == null || t == null || t.Map != pawn.Map
                || t.Position.Roofed(t.Map)
                || !HaulAIUtility.PawnCanAutomaticallyHaulFast(
                    pawn, t, forced)) return false;
            Map map = pawn.Map;
            IntVec3 root = t.Position;
            int limit = GenRadial.NumCellsInRadius(30f);
            for (int i = 0; i < limit; i++)
            {
                IntVec3 candidate = root + GenRadial.RadialPattern[i];
                if (!candidate.InBounds(map) || !candidate.Roofed(map)
                    || !candidate.Standable(map)
                    || candidate.GetDoor(map) != null) continue;
                Room room = candidate.GetRoom(map);
                if (room == null || room.PsychologicallyOutdoors
                    || room.IsDoorway || candidate.IsForbidden(pawn)
                    || candidate.GetFirstItem(map) != null
                    || !pawn.CanReserve(candidate)) continue;
                cell = candidate;
                return true;
            }
            return false;
        }
    }
}
