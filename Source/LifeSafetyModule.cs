using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    internal static class CALifeSafety
    {
        internal static bool InTemperatureDanger(Pawn victim)
        {
            if (victim == null || !victim.Spawned) return false;
            float here = victim.Position.GetTemperature(victim.Map);
            FloatRange safe = victim.SafeTemperatureRange();
            if (safe.Includes(here))
            {
                HediffSet hediffs = victim.health?.hediffSet;
                if (hediffs == null) return false;
                Hediff hypo = hediffs.GetFirstHediffOfDef(
                    HediffDefOf.Hypothermia);
                if (hypo != null && hypo.Severity > 0.12f) return true;
                Hediff heat = hediffs.GetFirstHediffOfDef(
                    HediffDefOf.Heatstroke);
                return heat != null && heat.Severity > 0.12f;
            }
            return true;
        }
    }

    // Module 3: pawns notice humans in temperatures that are hurting them - babies in the
    // sun, downed pawns freezing in the open - and move them somewhere safe without being
    // told. Colonists and prisoners always covered; downed outsiders behind a setting.
    public class WorkGiver_RescueFromTemperature : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode { get { return PathEndMode.OnCell; } }

        public override bool Prioritized { get { return true; } }

        public override ThingRequest PotentialWorkThingRequest
        {
            get { return ThingRequest.ForGroup(ThingRequestGroup.Pawn); }
        }

        private static bool IsEligibleVictim(Pawn victim, Pawn rescuer)
        {
            var s = AwarenessMod.Settings;
            if (victim == null || victim == rescuer || !victim.RaceProps.Humanlike) return false;
            if (victim.Dead || !victim.Spawned) return false;
            bool helpless = victim.Downed || victim.DevelopmentalStage == DevelopmentalStage.Baby;
            if (!helpless) return false;
            if (victim.InBed()) return false;
            bool ours = victim.IsColonist || victim.IsPrisonerOfColony || victim.IsSlaveOfColony
                || (victim.Faction != null && rescuer.Faction != null && victim.Faction == rescuer.Faction);
            CAEffectiveBehaviorProfile profile =
                CAEffectiveBehaviorProfileCache.Of(rescuer);
            string behaviorKey = ours ? "welfare.local_rescue"
                : "welfare.outsider_rescue";
            if (profile == null || !profile.Includes(behaviorKey)) return false;
            if (!ours && victim.HostileTo(rescuer.Faction) && !victim.Downed) return false;
            return CALifeSafety.InTemperatureDanger(victim);
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            var result = new List<Thing>();
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(pawn?.Map);
            if (knowledge == null) return result;
            var facts = new List<WelfareFactSnapshot>();
            knowledge.CopyFreshWelfare(pawn, facts);
            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < facts.Count; i++)
            {
                WelfareFactSnapshot fact = facts[i];
                if (!fact.Actionable || !fact.ObservedTemperatureDanger) continue;
                for (int j = 0; j < pawns.Count; j++)
                {
                    Pawn subject = pawns[j];
                    if (subject.thingIDNumber != fact.SubjectId
                        || !knowledge.CanActOnCurrentWelfare(pawn, subject))
                        continue;
                    result.Add(subject);
                    break;
                }
            }
            return result;
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            var s = AwarenessMod.Settings;
            CAEffectiveBehaviorProfile profile =
                CAEffectiveBehaviorProfileCache.Of(pawn);
            if (s == null || profile == null
                || !profile.Includes("welfare.local_rescue")
                    && !profile.Includes("welfare.outsider_rescue"))
                return true;
            // Colony life safety is part of the survival floor and still runs at Standard.
            // The Proactive threshold for automatic outsider rescue remains in eligibility.
            KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(pawn.Map);
            if (knowledge == null) return true;
            var facts = new List<WelfareFactSnapshot>();
            knowledge.CopyFreshWelfare(pawn, facts);
            for (int i = 0; i < facts.Count; i++)
                if (facts[i].Actionable
                    && facts[i].ObservedTemperatureDanger) return false;
            return true;
        }

        public override float GetPriority(Pawn pawn, TargetInfo t)
        {
            // Lives always outrank goods; in-area lives come first among them.
            return 100000f + WorkGiver_HaulCritical.AreaDutyBonus(pawn, t.Cell);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var victim = t as Pawn;
            if (!IsEligibleVictim(victim, pawn)) return false;
            if (!forced)
            {
                KnowledgeMapComponent knowledge =
                    KnowledgeMapComponent.For(pawn.Map);
                if (knowledge == null
                    || !knowledge.CanActOnCurrentWelfare(pawn, victim))
                    return false;
            }
            if (!pawn.CanReserveAndReach(victim, PathEndMode.OnCell, Danger.Deadly, 1, -1, null, forced)) return false;
            return FindSaferBed(pawn, victim) != null;
        }

        private static Building_Bed FindSaferBed(Pawn rescuer, Pawn victim)
        {
            Building_Bed bed = RestUtility.FindBedFor(victim, rescuer, checkSocialProperness: false, ignoreOtherReservations: false, guestStatus: null);
            if (bed == null) return null;
            float here = victim.Position.GetTemperature(victim.Map);
            float there = bed.Position.GetTemperature(bed.Map);
            FloatRange safe = victim.SafeTemperatureRange();
            if (safe.Includes(there)) return bed;
            // Destination must at least be meaningfully less dangerous.
            float comfort = (safe.min + safe.max) * 0.5f;
            if (System.Math.Abs(there - comfort) + 5f < System.Math.Abs(here - comfort)) return bed;
            return null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var victim = t as Pawn;
            if (!IsEligibleVictim(victim, pawn)) return null;
            Building_Bed bed = FindSaferBed(pawn, victim);
            if (bed == null) return null;
            KnowledgeMapComponent knowledge =
                KnowledgeMapComponent.For(pawn.Map);
            WelfareFactSnapshot fact = default(WelfareFactSnapshot);
            bool hasFact = knowledge != null && knowledge.TryGetFreshWelfare(
                pawn, victim.thingIDNumber, out fact);
            bool ours = victim.IsColonist || victim.IsPrisonerOfColony
                || victim.IsSlaveOfColony
                || victim.Faction != null && pawn.Faction != null
                    && victim.Faction == pawn.Faction;
            string behaviorKey = ours ? "welfare.local_rescue"
                : "welfare.outsider_rescue";
            Job job = JobMaker.MakeJob(JobDefOf.Rescue, victim, bed);
            job.count = 1;
            int now = Find.TickManager.TicksGame;
            CABehaviorContext context = CABehaviorContext.ForPawn(pawn,
                forced ? CAAuthorityOrigin.OperatorDirect
                    : CAAuthorityOrigin.PlayerDelegated,
                authoritySatisfied: forced || ours
                    || AwarenessMod.Settings?.rescueOutsiders == true,
                knowledgeSatisfied: hasFact && fact.Actionable
                    && fact.ObservedTemperatureDanger,
                knowledgeFresh: hasFact,
                liveValidated: victim.Spawned
                    && CALifeSafety.InTemperatureDanger(victim)
                    && bed.Spawned,
                knowledgeRelayed: hasFact && !fact.Evidence.IsDirect,
                knowledgeAgeTicks: hasFact
                    ? System.Math.Max(0, now - fact.SourceTick) : int.MaxValue,
                capabilitySatisfied: pawn.CanReserveAndReach(victim,
                    PathEndMode.OnCell, Danger.Deadly, 1, -1, null, forced),
                materialSatisfied: bed.Spawned,
                currentIntentCompatible: forced
                    || pawn.CurJob == null || !pawn.CurJob.playerForced,
                directPlayerOwnership: !forced && pawn.CurJob != null
                    && pawn.CurJob.playerForced,
                authorityBasis: forced ? "direct operator rescue"
                    : ours ? "colony care responsibility"
                    : "enabled outsider-rescue permission",
                knowledgeBasis: hasFact
                    ? (fact.Evidence.IsDirect ? "direct welfare observation"
                        : "physically relayed welfare observation")
                    : "no current welfare fact",
                owner: nameof(WorkGiver_RescueFromTemperature));
            CABehaviorDecision decision;
            CAIntentContext intent;
            if (!CABehaviorJobOrigin.TryAuthorizeAndRegister(pawn, job,
                behaviorKey, CAIntentController.Welfare, context,
                out decision, out intent,
                targetOrDemand: victim.LabelShort,
                ownershipScope: "bounded life-safety rescue",
                lifetimeTicks: 7500))
                return null;
            return job;
        }
    }
}
