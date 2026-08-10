using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Module: moving fire. Vanilla locks shooting to standing still - the stance system
    // pauses the pather for the whole aim-and-burst cycle. Flagged shooters retain an
    // existing path across native attack cycles; warmup and an active burst advance
    // only while the next step preserves the shot, while completed cooldown releases
    // movement. Accuracy pays a skill-scaled penalty through the game's own stat.
    // Withdrawals flag pawns automatically; drafted shooters get a toggle gizmo.
    public static class MovingFire
    {
        private sealed class FireState
        {
            public int jobId = -1;
            public Thing target;
            public int unavailableSince = -1;
            public int nextAcquireTick;
        }

        private static readonly HashSet<int> on = new HashSet<int>();
        private static readonly Dictionary<int, FireState> states =
            new Dictionary<int, FireState>();

        public static bool IsOn(Pawn p)
        {
            return p != null && on.Contains(p.thingIDNumber);
        }

        internal static void ClearTransient()
        {
            on.Clear();
            states.Clear();
        }

        // The path/job may be player-authored, but Moving Fire selects and owns
        // this target itself. Expose that exact state so friendly-fire protection
        // never has to infer authorship from the surrounding movement job.
        internal static bool OwnsCurrentAutonomousShot(Pawn pawn, Thing target,
            int jobId)
        {
            if (pawn == null || target == null || !IsOn(pawn)) return false;
            FireState state;
            return states.TryGetValue(pawn.thingIDNumber, out state)
                && state != null && state.jobId == jobId
                && state.target == target;
        }

        public static void Set(Pawn p, bool value)
        {
            if (p == null) return;
            if (value) on.Add(p.thingIDNumber);
            else
            {
                on.Remove(p.thingIDNumber);
                states.Remove(p.thingIDNumber);
                RemovePenalty(p);
            }
        }

        // This is execution inside an already-running movement job. It never creates
        // movement or replaces the job. Target acquisition is actor-local and a chosen
        // contact remains owned for the movement job instead of being globally
        // re-ranked every pulse.
        public static void TickMovingPawn(Pawn p)
        {
            if (p == null) return;
            if (!IsOn(p)) return;
            var settings = AwarenessMod.Settings;
            if (settings == null || !settings.movingFire)
            {
                Set(p, false);
                return;
            }
            if (p.Dead || p.Downed)
            {
                Set(p, false);
                return;
            }
            if (p.pather == null || !p.pather.Moving || p.CurJob == null)
            {
                if (states.Remove(p.thingIDNumber)) RemovePenalty(p);
                return;
            }

            FireState state;
            if (!states.TryGetValue(p.thingIDNumber, out state)
                || state.jobId != p.CurJob.loadID)
            {
                state = new FireState { jobId = p.CurJob.loadID };
                states[p.thingIDNumber] = state;
            }
            Stance_Busy busy = p.stances != null
                ? p.stances.curStance as Stance_Busy : null;
            if (busy != null)
            {
                if (CanAdvanceThroughCycle(p, busy)) EnsurePenalty(p);
                else RemovePenalty(p);
                return;
            }
            EnsurePenalty(p);
            if (p.drafter != null && !p.drafter.FireAtWill) return;
            ThingWithComps primary = p.equipment != null
                ? p.equipment.Primary : null;
            if (primary == null || !primary.def.IsRangedWeapon) return;

            int now = Find.TickManager.TicksGame;
            if (!TargetRelevant(p, state.target))
            {
                state.target = null;
                state.unavailableSince = -1;
            }
            else
            {
                Verb stableVerb = p.TryGetAttackVerb(state.target,
                    allowManualCastWeapons: false);
                if (stableVerb != null && !stableVerb.IsMeleeAttack
                    && stableVerb.CanHitTarget(state.target))
                {
                    state.unavailableSince = -1;
                    p.TryStartAttack(state.target);
                    return;
                }

                // Do not turn one obstructed step into target churn. A sustained loss
                // of the lane releases the contact, after the current native cycle has
                // already finished or invalidated itself.
                if (state.unavailableSince < 0) state.unavailableSince = now;
                if (now - state.unavailableSince < 60) return;
                state.target = null;
                state.unavailableSince = -1;
            }

            if (now < state.nextAcquireTick) return;
            state.nextAcquireTick = now + 60;
            Verb verb = p.CurrentEffectiveVerb;
            if (verb == null || verb.IsMeleeAttack) return;
            TargetScanFlags flags = TargetScanFlags.NeedLOSToAll
                | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
            if (verb.IsIncendiary_Ranged()) flags |= TargetScanFlags.NeedNonBurning;
            state.target = (Thing)AttackTargetFinder
                .BestShootTargetFromCurrentPosition(p, flags);
            if (state.target != null) p.TryStartAttack(state.target);
        }

        internal static bool CanAdvanceThroughCycle(Pawn pawn,
            Stance_Busy busy)
        {
            if (pawn == null || busy == null || busy.verb == null
                || busy.verb.IsMeleeAttack
                || (!(busy is Stance_Warmup) && !(busy is Stance_Cooldown))
                || pawn.stances == null
                || pawn.stances.stunner != null
                    && pawn.stances.stunner.Stunned
                || pawn.pather == null || !pawn.pather.Moving) return false;
            if (busy is Stance_Cooldown && !busy.verb.Bursting) return true;
            IntVec3 next = pawn.pather.nextCell;
            return next.IsValid && next.InBounds(pawn.Map)
                && busy.focusTarg.IsValid
                && busy.verb.CanHitTargetFrom(next, busy.focusTarg);
        }

        private static bool TargetRelevant(Pawn pawn, Thing target)
        {
            if (pawn == null || target == null || target.Destroyed
                || !target.Spawned || target.Map != pawn.Map
                || !pawn.HostileTo(target)) return false;
            Pawn targetPawn = target as Pawn;
            if (targetPawn != null && (targetPawn.Dead || targetPawn.Downed
                || targetPawn.IsPsychologicallyInvisible())) return false;
            IAttackTarget attackTarget = target as IAttackTarget;
            return attackTarget == null || !attackTarget.ThreatDisabled(pawn);
        }

        public static void EnsurePenalty(Pawn p)
        {
            try
            {
                var def = DefDatabase<HediffDef>.GetNamedSilentFail("CA_MovingFire");
                if (def == null || p.health == null) return;
                var existing = p.health.hediffSet.GetFirstHediffOfDef(def);
                if (existing != null)
                {
                    // Keep the self-expiry wound up while the behavior is live.
                    var comp = existing.TryGetComp<HediffComp_Disappears>();
                    if (comp != null) comp.ticksToDisappear = 240;
                    return;
                }
                int shoot = p.skills != null ? p.skills.GetSkill(SkillDefOf.Shooting).Level : 0;
                var h = HediffMaker.MakeHediff(def, p);
                h.Severity = UnityEngine.Mathf.Clamp(1f - 0.045f * shoot, 0.15f, 1f);
                p.health.AddHediff(h);
            }
            catch { }
        }

        public static void RemovePenalty(Pawn p)
        {
            try
            {
                var def = DefDatabase<HediffDef>.GetNamedSilentFail("CA_MovingFire");
                if (def == null || p == null || p.health == null) return;
                var h = p.health.hediffSet.GetFirstHediffOfDef(def);
                if (h != null) p.health.RemoveHediff(h);
            }
            catch { }
        }
    }

    // Vanilla AttackStatic stops the pather. Moving fire instead retains the existing
    // movement job and uses its ranged verb directly. The pather may advance during
    // warmup or an unfinished burst only while the next step preserves the same shot;
    // otherwise the native firing cycle plants the pawn until it completes. Post-burst
    // cooldown always releases the legs.
    [HarmonyPatch(typeof(Pawn_StanceTracker), "FullBodyBusy", MethodType.Getter)]
    public static class Patch_MovingFireStance
    {
        public static bool Prefix(Pawn_StanceTracker __instance, ref bool __result)
        {
            // A stun is unconditionally full-body-busy in vanilla; moving fire frees
            // the legs from the AIM cycle, never from a stun.
            if (__instance.stunner != null && __instance.stunner.Stunned) return true;
            var busy = __instance.curStance as Stance_Busy;
            if (busy == null || busy.verb == null || busy.verb.IsMeleeAttack) return true;
            if (!(busy is Stance_Warmup) && !(busy is Stance_Cooldown)) return true;
            if (!MovingFire.IsOn(__instance.pawn)) return true;
            Pawn pawn = __instance.pawn;
            if (!MovingFire.CanAdvanceThroughCycle(pawn, busy))
            {
                MovingFire.RemovePenalty(pawn);
                return true;
            }
            MovingFire.EnsurePenalty(pawn);
            __result = false;
            return false;
        }
    }

    // Retained for save compatibility and the setting kill switch. Firing itself is
    // driven by each pawn's existing pather tick, not by a map-wide polling loop.
    public class MovingFireMapComponent : MapComponent
    {
        private int cooldown;

        public MovingFireMapComponent(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (--cooldown > 0) return;
            cooldown = 120;
            var s = AwarenessMod.Settings;
            if (s == null || !s.movingFire)
            {
                // The kill switch actually kills: strip flags AND the accuracy hediff,
                // never leave a debuff running under a disabled feature.
                try { Sweep(); } catch { }
            }
            else
            {
                // The moving-fire flag is intentionally session-local, while its
                // accuracy hediff scribes. Clear a loaded orphan without restoring
                // the former map-wide target scan.
                try { SweepOrphanedPenalties(); } catch { }
            }
        }

        private void Sweep()
        {
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                if (MovingFire.IsOn(colonists[i])) MovingFire.Set(colonists[i], false);
                else MovingFire.RemovePenalty(colonists[i]); // orphaned hediff (e.g. loaded save)
            }
        }

        private void SweepOrphanedPenalties()
        {
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
                if (!MovingFire.IsOn(colonists[i]))
                    MovingFire.RemovePenalty(colonists[i]);
        }

    }

    // Actor-local execution seam. This observes locomotion; it never originates it.
    [HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.PatherTick))]
    public static class Patch_MovingFirePather
    {
        public static void Postfix(Pawn ___pawn)
        {
            try { MovingFire.TickMovingPawn(___pawn); } catch { }
        }
    }

    // Drafted shooters get the switch; withdrawals throw it automatically.
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_MovingFireGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn __instance)
        {
            foreach (var g in gizmos) yield return g;
            var p = __instance;
            var s = AwarenessMod.Settings;
            if (s == null || !s.movingFire) yield break;
            if (p == null || !p.IsColonistPlayerControlled || !p.Drafted) yield break;
            var primary = p.equipment != null ? p.equipment.Primary : null;
            if (primary == null || !primary.def.IsRangedWeapon) yield break;
            yield return new Command_Toggle
            {
                defaultLabel = "Moving fire",
                defaultDesc = "Fire during an already-running move when the next step preserves the shot. This switch never starts or chooses movement; combat positioning and survival moves remain separate. The pawn plants when the native aim or burst cycle needs stability, and aim suffers during the maneuver - less for skilled shooters.",
                icon = TexCommand.Attack,
                isActive = delegate { return MovingFire.IsOn(p); },
                toggleAction = delegate { MovingFire.Set(p, !MovingFire.IsOn(p)); }
            };
        }
    }
}
