using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // Cached lookups for our own defs. Resolved once, after the DefDatabase loads.
    public static class CA_Defs
    {
        private static JobDef stackPosture;
        private static JobDef combatPosture;
        private static JobDef combatMove;
        private static JobDef emergencySelfTend;
        private static JobDef assessCasualty;
        private static JobDef checkWelfare;
        private static JobDef fightingWithdrawal;
        private static JobDef feedDownedAnimal;
        private static JobDef boundedMeleeDefense;
        private static JobDef boundedRangedDefense;
        private static JobDef shelter;
        private static JobDef combatRecovery;
        private static JobDef waitForWelfareSupport;
        private static JobDef secureEPW;
        private static DutyDef stackPosition;
        private static DutyDef clearRoom;
        private static DutyDef holdPosition;
        private static DutyDef defensivePosture;

        public static JobDef StackPosture
        {
            get { return stackPosture ?? (stackPosture = DefDatabase<JobDef>.GetNamed("CA_StackPosture")); }
        }
        public static JobDef CombatPosture
        {
            get { return combatPosture ?? (combatPosture = DefDatabase<JobDef>.GetNamed("CA_CombatPosture")); }
        }
        public static JobDef CombatMove
        {
            get { return combatMove ?? (combatMove = DefDatabase<JobDef>.GetNamed("CA_CombatMove")); }
        }
        public static JobDef EmergencySelfTend
        {
            get { return emergencySelfTend ?? (emergencySelfTend = DefDatabase<JobDef>.GetNamed("CA_EmergencySelfTend")); }
        }

        public static JobDef AssessCasualty
        {
            get { return assessCasualty ?? (assessCasualty = DefDatabase<JobDef>.GetNamed("CA_AssessCasualty")); }
        }
        public static JobDef CheckWelfare
        {
            get { return checkWelfare ?? (checkWelfare = DefDatabase<JobDef>.GetNamed("CA_CheckWelfare")); }
        }
        public static JobDef FightingWithdrawal
        {
            get { return fightingWithdrawal ?? (fightingWithdrawal = DefDatabase<JobDef>.GetNamed("CA_FightingWithdrawal")); }
        }
        public static JobDef FeedDownedAnimal
        {
            get { return feedDownedAnimal ?? (feedDownedAnimal = DefDatabase<JobDef>.GetNamed("CA_FeedDownedAnimal")); }
        }
        public static JobDef BoundedMeleeDefense
        {
            get { return boundedMeleeDefense ?? (boundedMeleeDefense =
                DefDatabase<JobDef>.GetNamed("CA_BoundedMeleeDefense")); }
        }
        public static JobDef BoundedRangedDefense
        {
            get { return boundedRangedDefense ?? (boundedRangedDefense =
                DefDatabase<JobDef>.GetNamed("CA_BoundedRangedDefense")); }
        }
        public static JobDef Shelter
        {
            get { return shelter ?? (shelter =
                DefDatabase<JobDef>.GetNamed("CA_Shelter")); }
        }
        public static JobDef CombatRecovery
        {
            get { return combatRecovery ?? (combatRecovery =
                DefDatabase<JobDef>.GetNamed("CA_CombatRecovery")); }
        }
        public static JobDef WaitForWelfareSupport
        {
            get { return waitForWelfareSupport ?? (waitForWelfareSupport =
                DefDatabase<JobDef>.GetNamed("CA_WaitForWelfareSupport")); }
        }
        public static JobDef SecureEPW
        {
            get { return secureEPW ?? (secureEPW =
                DefDatabase<JobDef>.GetNamed("CA_SecureEPW")); }
        }
        public static DutyDef StackPosition
        {
            get { return stackPosition ?? (stackPosition = DefDatabase<DutyDef>.GetNamed("CA_StackPosition")); }
        }
        public static DutyDef ClearRoom
        {
            get { return clearRoom ?? (clearRoom = DefDatabase<DutyDef>.GetNamed("CA_ClearRoom")); }
        }
        public static DutyDef HoldPosition
        {
            get { return holdPosition ?? (holdPosition = DefDatabase<DutyDef>.GetNamed("CA_HoldPosition")); }
        }
        public static DutyDef DefensivePosture
        {
            get { return defensivePosture ?? (defensivePosture = DefDatabase<DutyDef>.GetNamed("CA_DefensivePosture")); }
        }
    }

    internal static class CACombatDutySafety
    {
        internal static bool MustYield(Pawn pawn, Job job)
        {
            if (job == null) return false;
            // CA_CombatMove is a bounded, expiring runtime ownership token in its
            // own right. Protecting the def (rather than only a transient context
            // dictionary) survives save/load and covers fire-position, recovery,
            // self-preservation, and explosive-evasion routes uniformly. Native flee
            // jobs likewise remain stronger than a standing combat duty.
            return job.def == CA_Defs.CombatMove
                || job.def == JobDefOf.Flee
                || job.def == JobDefOf.FleeAndCower
                || job.def == JobDefOf.FleeAndCowerShort
                || job.def == JobDefOf.TendPatient
                || job.def == JobDefOf.TendEntity
                || job.def == JobDefOf.Rescue
                || job.def == CA_Defs.AssessCasualty
                || job.def == CA_Defs.CheckWelfare
                || job.def == CA_Defs.WaitForWelfareSupport
                || job.def == CA_Defs.EmergencySelfTend
                || job.def == CA_Defs.FightingWithdrawal
                || job.def == CA_Defs.Shelter
                || job.def == CA_Defs.CombatRecovery
                || job.def == CA_Defs.SecureEPW
                    && !CACombatThreat.PerceivesActiveThreat(pawn)
                || job.def == JobDefOf.BeatFire
                || job.def == JobDefOf.ExtinguishSelf
                || job.def == JobDefOf.ExtinguishFiresNearby;
        }
    }

    // While a pawn is stacked or clearing under a CA drill lord, the drill's own state
    // machine owns withdrawal - the civilian hostility-response setting (default: flee)
    // must not scatter the team at breach contact. The constant think tree runs that
    // response every 30 ticks over the top of any duty job, so this is the one seam the
    // game leaves us; vanilla carves out its own psychic-ritual lords in the same giver.
    public static class StackDisciplinePatch
    {
        // Instant A/B switch for the gap-case deferral while its live
        // evidence accumulates: CA_COMBAT_GAPDEFER=0 restores the native
        // civilian response for between-jobs fighters without touching the
        // ordered-state or recovery-ownership discipline.
        internal static readonly bool GapDeferralEnabled = !string.Equals(
            Environment.GetEnvironmentVariable("CA_COMBAT_GAPDEFER"), "0",
            StringComparison.Ordinal);

        public static void TryInstall(HarmonyLib.Harmony harmony)
        {
            try
            {
                var target = HarmonyLib.AccessTools.Method(
                    typeof(JobGiver_ConfigurableHostilityResponse), "TryGiveJob");
                var prefix = HarmonyLib.AccessTools.Method(
                    typeof(StackDisciplinePatch), nameof(SuppressWhileStacked));
                harmony.Patch(target, prefix: new HarmonyLib.HarmonyMethod(prefix));
            }
            catch (Exception e)
            {
                Log.Warning("[Colonist Awareness] stack discipline patch stood down: " + e.Message);
            }
        }

        public static bool SuppressWhileStacked(Pawn pawn, ref Job __result)
        {
            Lord lord = pawn.GetLord();
            if (lord != null && (lord.LordJob is LordJob_CAStackBreach
                || lord.LordJob is LordJob_CATactical))
            {
                __result = null;
                return false;
            }
            // The same discipline covers explicit holds and hides: an ordered position
            // does not dissolve into flee-and-cower because a hostile got within 8 cells
            // - that proximity is often the entire point of the order.
            if (HiddenRegistry.IsHiddenOrOrdered(pawn))
            {
                __result = null;
                return false;
            }
            var withdrawal = pawn.Map != null
                ? pawn.Map.GetComponent<WithdrawalMapComponent>() : null;
            if (withdrawal != null && withdrawal.InPlan(pawn))
            {
                __result = null;
                return false;
            }
            var hold = pawn.Map != null ? pawn.Map.GetComponent<HoldMapComponent>() : null;
            if (hold != null && hold.IsHolding(pawn))
            {
                __result = null;
                return false;
            }
            // The same discipline covers CA-owned recovery and casualty work.
            // A pawn mid-treatment, mid-rescue, or assessing a casualty is
            // already under a lane that weighs health, threat, skill, and
            // disposition; the flat civilian flee-and-cower config must not
            // yank ownership mid-act (the a2-172 specimen lost Dolly from an
            // owned self-tend and Luc from a rescue exactly here). Fear stays
            // real: the CA survival and recovery lanes may still CHOOSE to
            // break contact on their own evidence-weighed terms.
            Job current = pawn.CurJob;
            if (current != null && (current.def == CA_Defs.CombatRecovery
                || current.def == CA_Defs.EmergencySelfTend
                || current.def == CA_Defs.AssessCasualty
                || current.def == CA_Defs.CheckWelfare
                || current.def == CA_Defs.WaitForWelfareSupport
                || current.def == CA_Defs.FightingWithdrawal
                || current.def == JobDefOf.Rescue))
            {
                CATrace.Pawn(pawn, "hostility response deferred: "
                    + current.def.defName + " owns this pawn; CA recovery "
                    + "lanes arbitrate their own break-contact");
                __result = null;
                return false;
            }
            // The gap case: no CA job owns the pawn this instant (a carry
            // just broke, a duty just released), and the civilian
            // flee-and-cower config would decide the reaction of a person
            // CA models in full. A threat-aware, violence-capable
            // A colonist eligible for registered local reaction defers to CA's
            // own constant-think lanes -
            // reaction, survival response, recovery - which weigh health,
            // skill, disposition, and nearby allies and may still choose to
            // break contact on those terms. Non-fighters, lower autonomy,
            // and pawns who do not perceive the threat keep the native
            // response: vanilla psychology remains real where CA has no
            // richer answer.
            if (GapDeferralEnabled && pawn.IsColonist && !pawn.Drafted
                && CABehaviorGate.StableProfileAllows(pawn,
                    "combat.local_reaction")
                && !pawn.WorkTagIsDisabled(WorkTags.Violent)
                && !SquadComponent.CombatLiability(pawn)
                && CACombatThreat.PerceivesActiveThreat(pawn))
            {
                CATrace.Pawn(pawn, "hostility response deferred: "
                    + "threat-aware Proactive fighter; reaction belongs to "
                    + "CA lanes, not civilian flee config");
                __result = null;
                return false;
            }
            return true;
        }
    }

    // The duty's job giver: walk to the duty focus cell, then hold the posture there.
    // The vanilla sapper/breacher pattern - far from the slot issues an expiring Goto,
    // at the slot reports arrival to the lord and stands. The duty persists across job
    // churn, so the posture self-renews without any external re-issue loop.
    public class JobGiver_CAStackSlot : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (CACombatDutySafety.MustYield(pawn, pawn?.CurJob)) return null;
            if (CATactical.HasForeignPlayerForcedJob(pawn)) return null;
            if (CATactical.HasCurrentOwnedPlayerForcedJob(pawn)
                || CATactical.HasPendingOwnedPlayerForcedJob(pawn)) return null;
            PawnDuty duty = pawn.mindState != null ? pawn.mindState.duty : null;
            if (duty == null || !duty.focus.IsValid) return null;
            IntVec3 slot = duty.focus.Cell;

            if (pawn.Position == slot)
            {
                Lord lord = pawn.GetLord();
                if (lord != null) lord.Notify_ReachedDutyLocation(pawn);
                if (pawn.CurJobDef != CA_Defs.StackPosture)
                    CATrace.Pawn(pawn, "at slot " + slot);
                Job stand = JobMaker.MakeJob(CA_Defs.StackPosture);
                stand.expiryInterval = 120;
                stand.checkOverrideOnExpire = true;
                stand.overrideFacing = duty.overrideFacing;
                return stand;
            }

            if (!pawn.CanReach(slot, PathEndMode.OnCell, Danger.Deadly))
            {
                CATrace.Skip(pawn, "stack slot", "cannot reach " + slot);
                return null; // fall through to the LordDuty wander fallback
            }
            Job go = JobMaker.MakeJob(JobDefOf.Goto, slot);
            go.expiryInterval = 500;
            go.checkOverrideOnExpire = true;
            go.locomotionUrgency = PawnUtility.ResolveLocomotion(pawn, LocomotionUrgency.Jog);
            return go;
        }
    }

    // A point-defense giver that works for colonists regardless of their civilian
    // hostility-response setting. Vanilla's JobGiver_AIFightEnemy refuses to engage
    // for any colonist not set to Attack (it carves out only ritual duelists); a
    // clearing team's discipline overrides that setting, so we lift the gate for the
    // duration of the check and restore it immediately.
    public class JobGiver_CAEngagePoint : JobGiver_AIDefendPoint
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            // Constant-duty thinking may interrupt the current job. The player's
            // current or queued hand remains stronger than the standing posture.
            if (pawn == null || pawn.jobs == null
                || CACombatDutySafety.MustYield(pawn, pawn.CurJob)
                || CATactical.HasForeignPlayerForcedJob(pawn)
                || CATactical.HasCurrentOwnedPlayerForcedJob(pawn)
                || CATactical.HasPendingOwnedPlayerForcedJob(pawn)) return null;
            // A target-specific defense job owns a short firing commitment. Let its
            // driver finish or invalidate that target before the 30-tick duty scan
            // considers the whole hostile field again.
            if (pawn.CurJobDef == CA_Defs.BoundedRangedDefense
                || pawn.CurJobDef == CA_Defs.BoundedMeleeDefense)
                return pawn.CurJob;
            var settings = pawn.playerSettings;
            Job job;
            if (settings == null) job = base.TryGiveJob(pawn);
            else
            {
                var civilian = settings.hostilityResponse;
                settings.hostilityResponse = HostilityResponseMode.Attack;
                try { job = base.TryGiveJob(pawn); }
                finally { settings.hostilityResponse = civilian; }
            }

            // Fire at will remains the player's immediate drafted authority. A Hold
            // may still recover its local geometry, but its constant-duty scan may not
            // turn that toggle back into autonomous ranged fire.
            if (pawn.Drafted && pawn.drafter != null && !pawn.drafter.FireAtWill
                && job != null && IsRangedEngagement(pawn, job)) return null;

            // Vanilla can choose Wait_Combat from the pawn's current firing cell
            // before AIDefendPoint consults its locus-bounded cast-position search.
            // A holder temporarily outside the authored envelope must recover into it,
            // not establish an indefinite firing posture where they happen to be.
            if (job != null && job.def == JobDefOf.Wait_Combat
                && !CellInsideDutyEnvelope(pawn, pawn.Position, 1.5f))
            {
                IntVec3 boundedPosition;
                if (TryFindShootingPosition(pawn, out boundedPosition)
                    && boundedPosition.IsValid
                    && CellInsideDutyEnvelope(pawn, boundedPosition, 0f)
                    && boundedPosition != pawn.Position)
                {
                    Job recover = JobMaker.MakeJob(JobDefOf.Goto, boundedPosition);
                    recover.expiryInterval = ExpiryInterval_ShooterSucceeded.RandomInRange;
                    recover.checkOverrideOnExpire = true;
                    return recover;
                }
                return null;
            }

            // Wait_Combat selects a fresh global best target every four ticks. That
            // behavior visibly turns a formation back and forth without completing
            // fire. Bind the already-selected enemy to two deliberate attacks, then
            // return to the normal threat ranking for a fresh decision.
            if (job != null && job.def == JobDefOf.Wait_Combat)
            {
                Thing target = pawn.mindState != null
                    ? pawn.mindState.enemyTarget : null;
                Verb verb = target != null
                    ? pawn.TryGetAttackVerb(target, false) : null;
                if (target == null || verb == null || verb.verbProps.IsMeleeAttack)
                    return null;
                Job committed = JobMaker.MakeJob(
                    CA_Defs.BoundedRangedDefense, target);
                committed.expiryInterval = 600;
                committed.checkOverrideOnExpire = false;
                committed.endIfCantShootTargetFromCurPos = true;
                committed.maxNumStaticAttacks = 2;
                PawnDuty duty = pawn.mindState != null
                    ? pawn.mindState.duty : null;
                CAIntentContext context;
                CATrace.Pawn(pawn, "commits to point-defense target "
                    + target.LabelShort + " for two native attack cycles",
                    target: target,
                    anchor: duty != null && duty.focus.IsValid
                        ? (IntVec3?)duty.focus.Cell : null,
                    intent: CATactical.TryGetContext(pawn, out context)
                        ? (CAIntentContext?)context : null);
                return committed;
            }
            return job;
        }

        private static bool IsRangedEngagement(Pawn pawn, Job job)
        {
            if (job.def == JobDefOf.AttackStatic
                || job.def == CA_Defs.BoundedRangedDefense
                || job.def == JobDefOf.Wait_Combat) return true;
            if (job.def != JobDefOf.AttackMelee) return false;
            Thing target = job.targetA.HasThing ? job.targetA.Thing : null;
            Verb verb = target != null ? pawn.TryGetAttackVerb(target, false) : null;
            return verb != null && !verb.verbProps.IsMeleeAttack;
        }

        private static bool CellInsideDutyEnvelope(Pawn pawn, IntVec3 cell,
            float tolerance)
        {
            PawnDuty duty = pawn != null && pawn.mindState != null
                ? pawn.mindState.duty : null;
            return duty != null && duty.focus.IsValid && cell.IsValid
                && cell.InHorDistOf(duty.focus.Cell,
                    UnityEngine.Mathf.Max(1f, duty.radius) + tolerance);
        }

        protected override bool ExtraTargetValidator(Pawn pawn, Thing target)
        {
            if (!base.ExtraTargetValidator(pawn, target)) return false;
            Verb verb = pawn.TryGetAttackVerb(target, false);
            if (verb == null || !verb.verbProps.IsMeleeAttack) return true;
            PawnDuty duty = pawn.mindState != null ? pawn.mindState.duty : null;
            return duty != null && duty.focus.IsValid && target.Spawned
                && target.Position.InHorDistOf(duty.focus.Cell,
                    UnityEngine.Mathf.Max(1f, duty.radius));
        }

        // A melee holder may intercept inside the authored local position. A dedicated
        // driver checks both target and pawn against the duty radius continuously, so
        // this never degrades into the unbounded pursuit of vanilla AttackMelee.
        protected override Job MeleeAttackJob(Pawn pawn, Thing enemyTarget)
        {
            PawnDuty duty = pawn.mindState != null ? pawn.mindState.duty : null;
            if (enemyTarget == null || duty == null || !duty.focus.IsValid
                || !enemyTarget.Position.InHorDistOf(duty.focus.Cell,
                    UnityEngine.Mathf.Max(1f, duty.radius))
                || !pawn.Position.InHorDistOf(duty.focus.Cell,
                    UnityEngine.Mathf.Max(1f, duty.radius) + 1.5f)
                || !JobGiver_CACombatReaction.PathStaysInsideMeleeEnvelope(
                    pawn, enemyTarget, duty.focus.Cell,
                    UnityEngine.Mathf.Max(1f, duty.radius))) return null;
            Job job = JobMaker.MakeJob(CA_Defs.BoundedMeleeDefense, enemyTarget);
            job.expiryInterval = 300;
            job.checkOverrideOnExpire = false;
            job.expireRequiresEnemiesNearby = true;
            job.maxNumMeleeAttacks = 1;
            CAIntentContext context;
            CATrace.Pawn(pawn, "intercepts " + enemyTarget.LabelShort
                + " inside the point-defense envelope", target: enemyTarget,
                destination: enemyTarget.Position, anchor: duty.focus.Cell,
                intent: CATactical.TryGetContext(pawn, out context)
                    ? (CAIntentContext?)context : null);
            return job;
        }
    }

    // Native melee execution with one additional invariant: a point defender may
    // intercept inside the duty envelope but neither the target nor the pawn may lead
    // the job outside it. The persistent duty returns the pawn to the anchor afterward.
    public class JobDriver_CABoundedMeleeDefense : JobDriver_AttackMelee
    {
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(OutsideDutyEnvelope);
            foreach (Toil toil in base.MakeNewToils()) yield return toil;
        }

        private bool OutsideDutyEnvelope()
        {
            PawnDuty duty = pawn != null && pawn.mindState != null
                ? pawn.mindState.duty : null;
            Thing target = job != null && job.targetA.HasThing
                ? job.targetA.Thing : null;
            if (target == null || !target.Spawned) return true;
            IntVec3 anchor;
            float radius;
            // A free reaction carries its own scribed origin/radius and must not be
            // reinterpreted through an unrelated standing duty. Duty geometry is the
            // fallback only for duty-issued intercepts, which have no target C/count.
            if (job != null && job.targetC.IsValid && job.count > 0)
            {
                anchor = job.targetC.Cell;
                radius = job.count;
            }
            else if (duty != null && duty.focus.IsValid)
            {
                anchor = duty.focus.Cell;
                radius = UnityEngine.Mathf.Max(1f, duty.radius);
            }
            else return true;
            return !target.Position.InHorDistOf(anchor, radius)
                || !pawn.Position.InHorDistOf(anchor, radius + 1.5f);
        }
    }

    // Target-specific point-defense fire. AttackStatic supplies the weapon's native
    // warmup, burst, cooldown, accuracy, and ammunition/mod seams; this driver adds
    // only the authored spatial invariant.
    public class JobDriver_CABoundedRangedDefense : JobDriver_AttackStatic
    {
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(OutsideDutyEnvelope);
            foreach (Toil toil in base.MakeNewToils()) yield return toil;
        }

        private bool OutsideDutyEnvelope()
        {
            PawnDuty duty = pawn != null && pawn.mindState != null
                ? pawn.mindState.duty : null;
            if (duty == null || !duty.focus.IsValid) return true;
            return !pawn.Position.InHorDistOf(duty.focus.Cell,
                UnityEngine.Mathf.Max(1f, duty.radius) + 1.5f);
        }
    }

    // The stack posture itself: hold the cell, keep the weapon oriented on the assigned
    // sector, engage what enters it. A real posture with its own scan cadence and
    // engagement rules - not a vanilla idle-guard job pressed into service.
    public class JobDriver_CAStackPosture : JobDriver
    {
        private Thing committedTarget;
        private int targetLostTick = -1;
        private int committedAttacks;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref committedTarget, "committedTarget");
            Scribe_Values.Look(ref targetLostTick, "targetLostTick", -1);
            Scribe_Values.Look(ref committedAttacks, "committedAttacks", 0);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil stand = ToilMaker.MakeToil("CAStackPosture");
            stand.initAction = delegate
            {
                pawn.Map.pawnDestinationReservationManager.Reserve(pawn, job, pawn.Position);
                if (pawn.pather != null) pawn.pather.StopDead();
                CheckEngage();
            };
            stand.tickAction = delegate
            {
                // Shooting owns the body: while a combat stance has a focus, face it.
                var busy = pawn.stances.curStance as Stance_Busy;
                if (busy != null && busy.focusTarg.IsValid)
                    pawn.rotationTracker.FaceTarget(busy.focusTarg);
                else if (job.overrideFacing != Rot4.Invalid)
                    pawn.rotationTracker.FaceTarget(pawn.Position + job.overrideFacing.FacingCell);
                else if (pawn.mindState != null && pawn.mindState.duty != null
                    && pawn.mindState.duty.focusSecond.IsValid)
                    pawn.rotationTracker.FaceTarget(pawn.mindState.duty.focusSecond);
                if (pawn.IsHashIntervalTick(4)) CheckEngage();
            };
            stand.handlingFacing = true;
            stand.socialMode = RandomSocialMode.Off;
            stand.defaultCompleteMode = ToilCompleteMode.Never;
            yield return stand;
        }

        // Scan and engage: melee anything adjacent, shoot what the sector offers.
        private void CheckEngage()
        {
            if (pawn.Downed || pawn.stances.FullBodyBusy) return;
            if (pawn.WorkTagIsDisabled(WorkTags.Violent)) return;

            // Adjacent threat: meet it with melee regardless of the held weapon.
            if (pawn.kindDef.canMeleeAttack)
            {
                for (int i = 0; i < 9; i++)
                {
                    IntVec3 c = pawn.Position + GenAdj.AdjacentCellsAndInside[i];
                    if (!c.InBounds(pawn.Map)) continue;
                    List<Thing> things = pawn.Map.thingGrid.ThingsListAtFast(c);
                    for (int j = 0; j < things.Count; j++)
                    {
                        Pawn other = things[j] as Pawn;
                        if (other == null || other.Downed || other.Dead) continue;
                        if (!other.HostileTo(pawn)) continue;
                        if (other.ThreatDisabled(pawn)) continue;
                        committedTarget = other;
                        targetLostTick = -1;
                        committedAttacks = 0;
                        pawn.meleeVerbs.TryMeleeAttack(other);
                        return;
                    }
                }
            }

            // A HIDDEN pawn holds ranged fire - concealment discipline; the spring
            // machinery decides when the hide breaks. Melee-defense above still applies
            // (a cornered hider fights what steps on them).
            if (HiddenRegistry.IsHidden(pawn)) return;

            // Ranged: fire on the best visible target from the held position.
            if (pawn.Drafted && pawn.drafter != null
                && !pawn.drafter.FireAtWill) return;
            Verb verb = pawn.CurrentEffectiveVerb;
            if (verb == null || verb.verbProps.IsMeleeAttack) return;

            // Once this posture opens on a target, finish a meaningful firing
            // opportunity instead of re-running a global best-target contest every
            // four ticks. A brief loss-of-shot grace tolerates movement through cover;
            // destruction, incapacitation, or a sustained lost lane releases it.
            if (CommittedTargetStillRelevant())
            {
                if (verb.CanHitTarget(committedTarget))
                {
                    targetLostTick = -1;
                    if (pawn.TryStartAttack(committedTarget))
                    {
                        committedAttacks++;
                        if (committedAttacks >= 2)
                        {
                            committedTarget = null;
                            committedAttacks = 0;
                        }
                    }
                    return;
                }
                if (targetLostTick < 0) targetLostTick = Find.TickManager.TicksGame;
                if (Find.TickManager.TicksGame - targetLostTick <= 60) return;
            }
            committedTarget = null;
            targetLostTick = -1;
            committedAttacks = 0;

            TargetScanFlags flags = TargetScanFlags.NeedLOSToAll
                | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
            if (verb.IsIncendiary_Ranged()) flags |= TargetScanFlags.NeedNonBurning;
            Thing target = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(pawn, flags);
            if (target != null)
            {
                committedTarget = target;
                committedAttacks = pawn.TryStartAttack(target) ? 1 : 0;
                PawnDuty duty = pawn.mindState != null
                    ? pawn.mindState.duty : null;
                CAIntentContext context;
                CATrace.Pawn(pawn, "opens a bounded firing commitment on "
                    + target.LabelShort, target: target,
                    anchor: duty != null && duty.focus.IsValid
                        ? (IntVec3?)duty.focus.Cell : null,
                    intent: CATactical.TryGetContext(pawn, out context)
                        ? (CAIntentContext?)context : null);
            }
        }

        private bool CommittedTargetStillRelevant()
        {
            if (committedTarget == null || committedTarget.Destroyed
                || !committedTarget.Spawned || committedTarget.Map != pawn.Map
                || !pawn.HostileTo(committedTarget)) return false;
            Pawn targetPawn = committedTarget as Pawn;
            if (targetPawn != null && (targetPawn.Dead || targetPawn.Downed
                || targetPawn.IsPsychologicallyInvisible())) return false;
            IAttackTarget attackTarget = committedTarget as IAttackTarget;
            return attackTarget == null || !attackTarget.ThreatDisabled(pawn);
        }
    }
}
