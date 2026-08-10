using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // Module: fighting withdrawals. Retreating under pursuit is bounds, not a footrace -
    // move a stretch, halt, put fire on the pursuer, move again.
    // - Solo suppressive retreat: one pawn alternates bounds and bursts back to the point.
    // - Covered bounded retreat: two elements alternate - one holds fire on the pursuer
    //   while the other bounds, roles swap, until both arrive.
    // One persistent native job owns each participating pawn. The map component scribes
    // only the shared plan and arbitrates the two drivers; it never supervises them by
    // repeatedly replacing jobs.
    public class WithdrawalMapComponent : MapComponent
    {
        private class Plan : IExposable
        {
            public int aId = -1;     // solo pawn, or element A in a pair
            public int bId = -1;     // element B, -1 = solo
            public IntVec3 dest = IntVec3.Invalid;
            public bool aDrafted;
            public bool bDrafted;
            public int aJobId = -1;
            public int bJobId = -1;
            public bool aMoving = true;
            public int fireScans;    // solo: 30-tick fire scans remaining before next bound

            // Driver-owned physical phase state. These are shared so a covered pair
            // resumes the exact mover/coverer relationship after save/load.
            public IntVec3 aGoal = IntVec3.Invalid;
            public IntVec3 bGoal = IntVec3.Invalid;
            public bool aBounding;
            public bool bBounding;
            public bool aFinalRun;
            public bool bFinalRun;
            public int nextFireScanTick = -1;
            public int nextBoundRetryTick = -1;

            // Causal provenance for this whole bounded-movement episode. A and B
            // can differ in origin: the selected pawn is operator-direct while an
            // accepted named partner is operator-relayed. An automatic shared-Hold
            // deviation instead preserves its autonomous continuation origin.
            public int episodeId;
            public int controller;
            public int issuerId = -1;
            public int aOrigin;
            public int bOrigin;

            // Derived contact cache. It is intentionally not scribed: knowledge is
            // authoritative and the drivers recompute it on their first post-load tick.
            public int threatScanTick = -1;
            public bool anyThreatKnown;

            public void ExposeData()
            {
                Scribe_Values.Look(ref aId, "aId", -1);
                Scribe_Values.Look(ref bId, "bId", -1);
                Scribe_Values.Look(ref dest, "dest", IntVec3.Invalid);
                Scribe_Values.Look(ref aDrafted, "aDrafted", false);
                Scribe_Values.Look(ref bDrafted, "bDrafted", false);
                Scribe_Values.Look(ref aJobId, "aJobId", -1);
                Scribe_Values.Look(ref bJobId, "bJobId", -1);
                Scribe_Values.Look(ref aMoving, "aMoving", true);
                Scribe_Values.Look(ref fireScans, "fireScans", 0);
                Scribe_Values.Look(ref aGoal, "aGoal", IntVec3.Invalid);
                Scribe_Values.Look(ref bGoal, "bGoal", IntVec3.Invalid);
                Scribe_Values.Look(ref aBounding, "aBounding", false);
                Scribe_Values.Look(ref bBounding, "bBounding", false);
                Scribe_Values.Look(ref aFinalRun, "aFinalRun", false);
                Scribe_Values.Look(ref bFinalRun, "bFinalRun", false);
                Scribe_Values.Look(ref nextFireScanTick, "nextFireScanTick", -1);
                Scribe_Values.Look(ref nextBoundRetryTick,
                    "nextBoundRetryTick", -1);
                Scribe_Values.Look(ref episodeId, "intentEpisodeId", 0);
                Scribe_Values.Look(ref controller, "intentController", 0);
                Scribe_Values.Look(ref issuerId, "intentIssuerId", -1);
                Scribe_Values.Look(ref aOrigin, "aIntentOrigin", 0);
                Scribe_Values.Look(ref bOrigin, "bIntentOrigin", 0);
            }
        }

        private readonly struct PerceivedPursuer
        {
            public readonly bool Known;
            public readonly Pawn VisiblePawn;
            public readonly IntVec3 Cell;

            public PerceivedPursuer(bool known, Pawn visiblePawn, IntVec3 cell)
            {
                Known = known;
                VisiblePawn = visiblePawn;
                Cell = cell;
            }
        }

        private readonly struct PlanMemberState
        {
            public readonly Pawn Pawn;
            public readonly int JobId;

            public PlanMemberState(Pawn pawn, int jobId)
            {
                Pawn = pawn;
                JobId = jobId;
            }
        }

        private List<Plan> plans = new List<Plan>();
        private int cooldown;

        public WithdrawalMapComponent(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref plans, "CA_withdrawalPlans", LookMode.Deep);
            if (plans == null) plans = new List<Plan>();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            var s = AwarenessMod.Settings;
            if (s == null || !s.withdrawals)
            {
                DisableWithdrawals();
                return;
            }

            // A pre-a2-20 save may contain a component-owned Goto or AttackStatic.
            // Adopt it only when the exact saved loadID still owns the pawn; any newer
            // job means the player or engine has already taken control.
            ValidatePlans(allowLegacyMigration: true);
        }

        public override void MapComponentTick()
        {
            var s = AwarenessMod.Settings;
            if (s == null || !s.withdrawals)
            {
                if (plans.Count > 0) DisableWithdrawals();
                return;
            }
            if (--cooldown > 0) return;
            cooldown = 30;
            if (plans.Count == 0) return;
            try
            {
                // Integrity repair only. Physical phases live inside the persistent
                // JobDrivers and this component never issues replacement jobs here.
                ValidatePlans(allowLegacyMigration: false);
            }
            catch { }
        }

        public bool CanOfferSolo(Pawn p, IntVec3 dest)
        {
            return p != null && p.Map == map && ValidDestination(dest)
                && CanStartSelected(p)
                && p.CanReach(dest, PathEndMode.OnCell, Danger.Some);
        }

        public string CoveredPartnerExclusion(Pawn actor, Pawn buddy,
            IntVec3 dest)
        {
            if (!CanOfferSolo(actor, dest))
                return "the selected fighter cannot start that movement";
            if (buddy == null || buddy == actor) return "not a distinct partner";
            if (buddy.Map != map || !buddy.Spawned) return "not on this map";
            if (buddy.Dead) return "dead";
            if (buddy.Downed) return "downed";
            if (buddy.InMentalState) return "in a mental state";
            if (buddy.jobs == null) return "has no available job tracker";
            if (InPlan(buddy)) return "already owns another withdrawal";
            if (HasCurrentForcedJob(buddy))
                return "currently follows another direct player order";
            if (HasQueuedForcedJob(buddy))
                return "has a queued direct player order";
            if (CACombatDutySafety.MustYield(buddy, buddy.CurJob))
                return "must yield to immediate health or duty safety";
            if (CATactical.HasExplicitOrder(buddy))
                return "already owns another explicit tactical order";
            if (HiddenRegistry.IsHiddenOrOrdered(buddy))
                return "is concealed under another order";
            if (IsStackActor(buddy)) return "is committed to a breach stack";
            if (!HasRangedWeapon(buddy))
                return "has no ranged weapon for the covering role";
            return null;
        }

        public void OrderSolo(Pawn p, IntVec3 dest, CAIntentContext context)
        {
            var s = AwarenessMod.Settings;
            if (s == null || !s.withdrawals || !ValidDestination(dest)
                || !CanOfferSolo(p, dest)) return;
            Cancel(p);
            CATrace.Pawn(p,
                "tactical ownership TRANSITION -> solo withdrawal",
                destination: dest, anchor: p.Position, intent: context);
            ReleaseSelectedTacticalOwnership(p);

            var plan = new Plan
            {
                aId = p.thingIDNumber,
                dest = dest,
                aDrafted = p.Drafted,
                episodeId = context.EpisodeId,
                controller = (int)context.Controller,
                issuerId = context.IssuerId,
                aOrigin = (int)context.Origin
            };
            NormalizeIntent(plan, p);
            Job job = MakeWithdrawalJob(dest);
            plan.aJobId = job.loadID;
            plans.Add(plan);
            MovingFire.Set(p, true);
            if (!StartWithdrawalJob(p, job, allowCurrentForced: true))
            {
                MovingFire.Set(p, false);
                plans.Remove(plan);
                return;
            }
            CATrace.Pawn(p, "starts solo suppressive withdrawal",
                destination: dest, anchor: p.Position, intent: Intent(plan, true));
        }

        public void OrderCovered(Pawn p, Pawn buddy, IntVec3 dest,
            CAIntentContext context)
        {
            OrderCoveredInternal(p, buddy, dest, context,
                allowSharedHoldRelease: false);
        }

        // A Proactive holder who genuinely loses combat viability may take a
        // co-holder with them instead of independently manufacturing two solo
        // retreats. Both actors already share player-authored tactical ownership;
        // this continuation releases that shared Hold explicitly and preserves one
        // paired withdrawal episode.
        public void OrderCoveredFromHold(Pawn p, Pawn buddy, IntVec3 dest,
            CAIntentContext context)
        {
            OrderCoveredInternal(p, buddy, dest, context,
                allowSharedHoldRelease: true);
        }

        private void OrderCoveredInternal(Pawn p, Pawn buddy, IntVec3 dest,
            CAIntentContext context, bool allowSharedHoldRelease)
        {
            var s = AwarenessMod.Settings;
            if (s == null || !s.withdrawals || !ValidDestination(dest)
                || !CanOfferSolo(p, dest)) return;
            bool buddyEligible = buddy != null && !InPlan(buddy)
                && HasRangedWeapon(buddy)
                && (CanJoinAsBuddy(buddy)
                    || allowSharedHoldRelease
                        && CanJoinSharedHoldBuddy(p, buddy));
            if (!buddyEligible || buddy.Map != map)
            {
                if (allowSharedHoldRelease) OrderSolo(p, dest, context);
                else
                {
                    string reason = CoveredPartnerExclusion(p, buddy, dest)
                        ?? "partner eligibility changed before execution";
                    CATrace.Skip(p, "covered bounded movement",
                        reason + "; no solo fallback was substituted",
                        destination: dest, anchor: p.Position,
                        intent: context);
                    Messages.Message("Covered movement was not issued: "
                        + reason + ".", new LookTargets(dest, map),
                        MessageTypeDefOf.RejectInput, false);
                }
                return;
            }

            Cancel(p);
            Cancel(buddy);
            CATrace.Pawn(p,
                "tactical ownership TRANSITION -> covered withdrawal",
                destination: dest, anchor: p.Position, intent: context);
            ReleaseSelectedTacticalOwnership(p);
            if (allowSharedHoldRelease && CATactical.IsHold(buddy))
                ReleaseSelectedTacticalOwnership(buddy);
            else CATactical.ReleaseAutomaticDefense(buddy);

            var plan = new Plan
            {
                aId = p.thingIDNumber,
                bId = buddy.thingIDNumber,
                dest = dest,
                aDrafted = p.Drafted,
                bDrafted = buddy.Drafted,
                episodeId = context.EpisodeId,
                controller = (int)context.Controller,
                issuerId = context.IssuerId,
                aOrigin = (int)context.Origin,
                bOrigin = context.Origin == CAIntentOrigin.OperatorDirect
                    || context.Origin == CAIntentOrigin.OperatorRelay
                        ? (int)CAIntentOrigin.OperatorRelay
                        : (int)context.Origin
            };
            NormalizeIntent(plan, p);
            Job aJob = MakeWithdrawalJob(dest);
            Job bJob = MakeWithdrawalJob(dest);
            plan.aJobId = aJob.loadID;
            plan.bJobId = bJob.loadID;
            plans.Add(plan);
            MovingFire.Set(p, true);
            MovingFire.Set(buddy, true);

            bool aStarted = StartWithdrawalJob(p, aJob, allowCurrentForced: true);
            bool bStarted = StartWithdrawalJob(buddy, bJob,
                allowCurrentForced: allowSharedHoldRelease);
            if (!aStarted) RemoveMember(plan, p.thingIDNumber);
            if (!bStarted) RemoveMember(plan, buddy.thingIDNumber);
            if (aStarted && plans.Contains(plan))
                CATrace.Pawn(p, "starts covered bounded withdrawal",
                    destination: dest, anchor: p.Position, intent: Intent(plan, true));
            if (bStarted && plans.Contains(plan))
                CATrace.Pawn(buddy, "takes covering role for bounded withdrawal",
                    destination: dest, anchor: buddy.Position,
                    intent: Intent(plan,
                        plan.aId == buddy.thingIDNumber));
        }

        public void Cancel(Pawn p)
        {
            if (p == null) return;
            int ownedJobId = -1;
            for (int i = plans.Count - 1; i >= 0; i--)
            {
                Plan plan = plans[i];
                if (plan.aId == p.thingIDNumber)
                {
                    ownedJobId = plan.aJobId;
                    if (plan.bId >= 0)
                        MakeSolo(plan, plan.bId, plan.bDrafted);
                    else plans.RemoveAt(i);
                }
                else if (plan.bId == p.thingIDNumber)
                {
                    ownedJobId = plan.bJobId;
                    MakeSolo(plan, plan.aId, plan.aDrafted);
                }
            }
            MovingFire.Set(p, false);
            EndPlanJobIfSafe(p, ownedJobId);
        }

        public bool InPlan(Pawn p)
        {
            if (p == null) return false;
            for (int i = 0; i < plans.Count; i++)
                if (plans[i].aId == p.thingIDNumber
                    || plans[i].bId == p.thingIDNumber) return true;
            return false;
        }

        // Automatic paired extraction values social stakes, but it does not treat
        // pregnancy or degraded combat power as positive covering capability.
        public Pawn FindBuddyForHoldDeviation(Pawn p)
        {
            return FindBuddyInternal(p, allowSharedHold: true);
        }

        private Pawn FindBuddyInternal(Pawn p, bool allowSharedHold)
        {
            int squad = SquadComponent.SquadOf(p);
            var colonists = map.mapPawns.FreeColonistsSpawned;
            Pawn best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < colonists.Count; i++)
            {
                var c = colonists[i];
                if (c == p || InPlan(c)) continue;
                if (!CanJoinAsBuddy(c)
                    && !(allowSharedHold && CanJoinSharedHoldBuddy(p, c)))
                    continue;
                if (!(AutonomyComponent.LevelOf(c) >= 2 || c.Drafted)) continue;
                if (c.equipment == null || c.equipment.Primary == null
                    || !c.equipment.Primary.def.IsRangedWeapon) continue;
                float d = p.Position.DistanceTo(c.Position);
                bool spouse = AreSpouses(p, c);
                if (d > (spouse ? 24f : 16f)) continue;
                CACombatConditionSnapshot condition =
                    CACombatConditionSnapshot.Capture(c);
                float score = -d
                    + (squad > 0 && SquadComponent.SquadOf(c) == squad ? 25f : 0f)
                    + (spouse ? 100f : 0f)
                    - (IsPregnant(c) ? 24f : 0f)
                    - (condition.RequiresCombatRecovery(true) ? 40f : 0f);
                if (score > bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        private static bool AreSpouses(Pawn first, Pawn second)
        {
            return first?.relations != null && second != null
                && first.relations.DirectRelationExists(
                    PawnRelationDefOf.Spouse, second);
        }

        private static bool IsPregnant(Pawn pawn)
        {
            return HediffDefOf.PregnantHuman != null
                && pawn?.health?.hediffSet != null
                && pawn.health.hediffSet.HasHediff(HediffDefOf.PregnantHuman);
        }

        private static bool CanJoinSharedHoldBuddy(Pawn actor, Pawn candidate)
        {
            CAIntentContext actorContext;
            CAIntentContext candidateContext;
            if (!Able(candidate) || candidate.jobs == null
                || actor == null || candidate.Map != actor.Map
                || !CATactical.IsHold(actor) || !CATactical.IsHold(candidate)
                || !CATactical.TryGetContext(actor, out actorContext)
                || !CATactical.TryGetContext(candidate,
                    out candidateContext)
                || actorContext.EpisodeId != candidateContext.EpisodeId
                || CATactical.HasForeignPlayerForcedJob(candidate)
                || HiddenRegistry.IsHiddenOrOrdered(candidate)
                || IsStackActor(candidate)) return false;
            return candidate.jobs.jobQueue == null
                || !candidate.jobs.jobQueue.AnyPlayerForced;
        }

        internal JobCondition DriverCondition(Pawn pawn, Job job)
        {
            Plan plan;
            bool isA;
            if (!TryGetOwnedPlan(pawn, job, out plan, out isA))
                return JobCondition.Succeeded;
            var s = AwarenessMod.Settings;
            if (s == null || !s.withdrawals
                || !CanRemainInPlan(pawn, isA ? plan.aDrafted : plan.bDrafted))
                return JobCondition.InterruptForced;
            return JobCondition.Ongoing;
        }

        internal JobCondition TickDriver(Pawn pawn, Job job)
        {
            JobCondition condition = DriverCondition(pawn, job);
            if (condition != JobCondition.Ongoing) return condition;

            Plan plan;
            bool isA;
            if (!TryGetOwnedPlan(pawn, job, out plan, out isA))
                return JobCondition.Succeeded;

            Pawn a = Colonist(plan.aId);
            Pawn b = plan.bId >= 0 ? Colonist(plan.bId) : null;
            if (!Able(a))
            {
                RemoveMember(plan, plan.aId);
                return isA ? JobCondition.InterruptForced : JobCondition.Ongoing;
            }
            if (plan.bId >= 0 && !Able(b))
            {
                int removed = plan.bId;
                RemoveMember(plan, removed);
                if (pawn.thingIDNumber == removed) return JobCondition.InterruptForced;
                b = null;
                isA = true;
            }

            bool aThere = a.Position.InHorDistOf(plan.dest, 4.5f);
            bool bThere = b == null || b.Position.InHorDistOf(plan.dest, 4.5f);
            if (aThere && bThere)
            {
                CompletePlan(plan);
                return JobCondition.Succeeded;
            }

            bool threatKnown = AnyThreatKnown(plan, a, b);
            if (!threatKnown)
            {
                if (!(isA ? aThere : bThere)) EnsureFinalPath(plan, pawn, job, isA);
                else SetHolding(plan, pawn, job, isA);
                return JobCondition.Ongoing;
            }

            PerceivedPursuer pursuer = NearestThreat(pawn, 26f);
            // A plan may begin before contact reaches either pawn, in which case
            // EnsureFinalPath starts a direct run.  Once shared knowledge becomes
            // actionable, convert that inherited final run into a holding state so
            // the ordinary solo/pair phase logic selects a bounded, cover-aware
            // destination immediately.  The persistent job remains the owner.
            if (IsMoving(plan, isA) && IsFinalRun(plan, isA))
                SetHolding(plan, pawn, job, isA);
            if (b == null)
                TickSolo(plan, pawn, job, pursuer);
            else
                TickPair(plan, pawn, job, isA, pursuer, aThere, bThere);
            return JobCondition.Ongoing;
        }

        internal void NotifyDriverFinished(Pawn pawn, int jobId)
        {
            if (pawn == null) return;
            MovingFire.Set(pawn, false);

            // Cleanup runs while CurJob still points at the ending job. Exact loadID
            // matching makes this callback idempotent and prevents a stale driver from
            // removing a replacement plan. Never end or start another job from here.
            for (int i = plans.Count - 1; i >= 0; i--)
            {
                Plan plan = plans[i];
                if (plan.aId == pawn.thingIDNumber && plan.aJobId == jobId)
                {
                    if (plan.bId >= 0) MakeSolo(plan, plan.bId, plan.bDrafted);
                    else plans.RemoveAt(i);
                    return;
                }
                if (plan.bId == pawn.thingIDNumber && plan.bJobId == jobId)
                {
                    MakeSolo(plan, plan.aId, plan.aDrafted);
                    return;
                }
            }
        }

        private void TickSolo(Plan plan, Pawn pawn, Job job,
            PerceivedPursuer pursuer)
        {
            if (IsMoving(plan, true))
            {
                if (pawn.pather != null && pawn.pather.Moving) return;
                IntVec3 goal = Goal(plan, true);
                if (goal.IsValid && !pawn.Position.InHorDistOf(goal, 0.9f))
                {
                    StartPath(plan, pawn, job, true, goal, IsFinalRun(plan, true));
                    return;
                }

                SetHolding(plan, pawn, job, true);
                plan.fireScans = 3;
                plan.nextFireScanTick = Find.TickManager.TicksGame + 30;
                if (!TryFire(pawn, pursuer)) plan.fireScans = 0;
            }

            if (plan.fireScans > 0)
            {
                if (!TryFire(pawn, pursuer)) plan.fireScans = 0;
                int now = Find.TickManager.TicksGame;
                if (plan.fireScans > 0 && now >= plan.nextFireScanTick)
                {
                    plan.fireScans--;
                    plan.nextFireScanTick = now + 30;
                }
                if (plan.fireScans > 0) return;
            }

            StartBound(plan, pawn, job, true, pursuer);
        }

        private void TickPair(Plan plan, Pawn pawn, Job job, bool isA,
            PerceivedPursuer pursuer, bool aThere, bool bThere)
        {
            bool mover = plan.aMoving == isA;
            if (!mover)
            {
                SetHolding(plan, pawn, job, isA);
                TryFire(pawn, pursuer);
                return;
            }

            bool arrived = isA ? aThere : bThere;
            bool partnerArrived = isA ? bThere : aThere;
            if (arrived)
            {
                SetHolding(plan, pawn, job, isA);
                if (!partnerArrived) plan.aMoving = !plan.aMoving;
                return;
            }

            if (IsMoving(plan, isA))
            {
                if (pawn.pather != null && pawn.pather.Moving) return;
                IntVec3 goal = Goal(plan, isA);
                if (goal.IsValid && !pawn.Position.InHorDistOf(goal, 0.9f))
                {
                    StartPath(plan, pawn, job, isA, goal, IsFinalRun(plan, isA));
                    return;
                }

                SetHolding(plan, pawn, job, isA);
                TryFire(pawn, pursuer);
                plan.aMoving = !plan.aMoving;
                return;
            }

            StartBound(plan, pawn, job, isA, pursuer);
        }

        private void StartBound(Plan plan, Pawn pawn, Job job, bool isA,
            PerceivedPursuer pursuer)
        {
            int now = Find.TickManager.TicksGame;
            if (plan.nextBoundRetryTick > now)
            {
                SetHolding(plan, pawn, job, isA);
                TryFire(pawn, pursuer);
                return;
            }

            IntVec3 cell = BoundCell(pawn, plan.dest, 7f, pursuer);
            if (!cell.IsValid || cell == pawn.Position)
            {
                SetHolding(plan, pawn, job, isA);
                TryFire(pawn, pursuer);
                plan.nextBoundRetryTick = now + 30;
                return;
            }
            plan.nextBoundRetryTick = -1;
            StartPath(plan, pawn, job, isA, cell, finalRun: false);
        }

        private void EnsureFinalPath(Plan plan, Pawn pawn, Job job, bool isA)
        {
            if (IsMoving(plan, isA) && IsFinalRun(plan, isA)
                && Goal(plan, isA) == plan.dest && pawn.pather != null
                && pawn.pather.Moving) return;
            StartPath(plan, pawn, job, isA, plan.dest, finalRun: true);
        }

        private void StartPath(Plan plan, Pawn pawn, Job job, bool isA,
            IntVec3 goal, bool finalRun)
        {
            if (pawn == null || pawn.Map != map || pawn.pather == null
                || pawn.jobs == null || pawn.CurJob != job
                || HasQueuedForcedJob(pawn)) return;
            if (!goal.IsValid || !goal.InBounds(map) || !goal.Standable(map))
                goal = plan.dest;

            bool sameActivePath = IsMoving(plan, isA)
                && Goal(plan, isA) == goal && IsFinalRun(plan, isA) == finalRun
                && pawn.pather.Moving && pawn.pather.Destination.Cell == goal;
            if (sameActivePath) return;

            SetGoal(plan, isA, goal);
            SetMoving(plan, isA, true);
            SetFinalRun(plan, isA, finalRun);
            job.targetB = goal;
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            map.pawnDestinationReservationManager.Reserve(pawn, job, goal);
            pawn.pather.StartPath(goal, PathEndMode.OnCell);
            CAIntentContext context = Intent(plan, isA);
            CACombatIntent.RecordMovement(pawn, context, pawn.Position, goal);
            CATrace.Pawn(pawn, finalRun
                    ? "withdrawal makes final movement"
                    : "withdrawal bounds to new position",
                destination: goal, anchor: pawn.Position, intent: context);
        }

        private void SetHolding(Plan plan, Pawn pawn, Job job, bool isA)
        {
            if (pawn == null || pawn.pather == null) return;
            bool changed = IsMoving(plan, isA) || IsFinalRun(plan, isA)
                || pawn.pather.Moving || Goal(plan, isA) != pawn.Position;
            if (!changed) return;

            pawn.pather.StopDead();
            SetGoal(plan, isA, pawn.Position);
            SetMoving(plan, isA, false);
            SetFinalRun(plan, isA, false);
            job.targetB = pawn.Position;
            map.pawnDestinationReservationManager.Reserve(pawn, job, pawn.Position);
            CATrace.Pawn(pawn, "withdrawal holds for fire or partner movement",
                destination: pawn.Position, anchor: plan.dest,
                intent: Intent(plan, isA));
        }

        // Stand and fire, but never chase. Remembered cells can sustain the decision
        // to bound; only a currently visible pawn can become a shot target.
        private static bool TryFire(Pawn pawn, PerceivedPursuer pursuer)
        {
            Pawn visible = pursuer.VisiblePawn;
            if (!pursuer.Known || visible == null || pawn == null
                || pawn.stances == null) return false;
            if (pawn.stances.curStance is Stance_Busy) return true;
            Verb verb = pawn.TryGetAttackVerb(visible, !pawn.IsColonist);
            if (verb == null || verb.verbProps.IsMeleeAttack
                || !verb.CanHitTargetFrom(pawn.Position, visible)) return false;
            return pawn.TryStartAttack(visible);
        }

        private bool AnyThreatKnown(Plan plan, Pawn a, Pawn b)
        {
            int now = Find.TickManager.TicksGame;
            if (plan.threatScanTick == now) return plan.anyThreatKnown;
            plan.threatScanTick = now;
            plan.anyThreatKnown = NearestThreat(a, 26f).Known
                || (b != null && NearestThreat(b, 26f).Known);
            return plan.anyThreatKnown;
        }

        private IntVec3 BoundCell(Pawn p, IntVec3 dest, float stride,
            PerceivedPursuer pursuer)
        {
            var delta = (dest - p.Position).ToVector3();
            float len = delta.magnitude;
            if (len <= stride) return dest;
            IntVec3 nominal = p.Position
                + IntVec3.FromVector3(delta * (stride / len));
            int currentDistance = p.Position.DistanceToSquared(dest);
            IntVec3 fallback = CellFinder.StandableCellNear(
                nominal, map, 4f,
                c => ValidBoundCell(p, c, dest, stride, currentDistance));
            if (!fallback.IsValid)
                fallback = CellFinder.StandableCellNear(
                    p.Position, map, stride,
                    c => ValidBoundCell(p, c, dest, stride, currentDistance));

            Pawn visible = pursuer.VisiblePawn;
            if (!pursuer.Known || visible == null)
                return fallback.IsValid ? fallback : p.Position;
            Verb verb = p.TryGetAttackVerb(visible, !p.IsColonist);
            if (verb == null || verb.verbProps.IsMeleeAttack)
                return fallback.IsValid ? fallback : p.Position;

            // RimWorld 1.6 clips maxRangeFromLocus around the target rather than
            // the locus. Leave that field unset and constrain the local bound in
            // the validator while retaining the finder's native firing-position
            // scoring and safety gates.
            var request = new CastPositionRequest
            {
                caster = p,
                target = visible,
                verb = verb,
                maxRangeFromCaster = stride,
                maxRangeFromTarget = verb.EffectiveRange,
                wantCoverFromTarget = verb.EffectiveRange > 7f,
                validator = c => c.InHorDistOf(nominal, 4f)
                    && c.InHorDistOf(p.Position, stride)
                    && c.DistanceToSquared(dest) < currentDistance
            };
            IntVec3 coverCell;
            if (CastPositionFinder.TryFindCastPosition(request, out coverCell)
                && coverCell.IsValid && coverCell != p.Position
                && coverCell.InBounds(map) && coverCell.Standable(map))
                return coverCell;
            return fallback.IsValid ? fallback : p.Position;
        }

        private bool ValidBoundCell(Pawn pawn, IntVec3 cell, IntVec3 dest,
            float stride, int currentDistance)
        {
            return cell.IsValid && cell.InBounds(map)
                && cell.InHorDistOf(pawn.Position, stride)
                && cell.DistanceToSquared(dest) < currentDistance
                && cell.WalkableBy(map, pawn) && cell.InAllowedArea(pawn)
                && map.pawnDestinationReservationManager.CanReserve(cell, pawn)
                && !PawnUtility.KnownDangerAt(cell, map, pawn)
                && map.reachability.CanReach(pawn.Position, cell,
                    PathEndMode.OnCell, TraverseParms.For(pawn, Danger.Some));
        }

        private static Job MakeWithdrawalJob(IntVec3 dest)
        {
            Job job = JobMaker.MakeJob(CA_Defs.FightingWithdrawal, dest);
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            return job;
        }

        private static bool StartWithdrawalJob(Pawn pawn, Job job,
            bool allowCurrentForced)
        {
            if (pawn == null || pawn.jobs == null || job == null) return false;
            Job previous = pawn.CurJob;
            if (previous != null && previous.playerForced)
            {
                if (!allowCurrentForced) return false;
                // Ordered AttackStatic cleanup otherwise may enqueue a follow-up target.
                previous.playerInterruptedForced = true;
            }
            else if (previous != null && previous.def == JobDefOf.AttackStatic)
                previous.playerInterruptedForced = true;

            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            return pawn.CurJob == job;
        }

        private bool TryGetOwnedPlan(Pawn pawn, Job job, out Plan plan,
            out bool isA)
        {
            plan = null;
            isA = false;
            if (pawn == null || job == null) return false;
            int pawnId = pawn.thingIDNumber;
            for (int i = 0; i < plans.Count; i++)
            {
                Plan candidate = plans[i];
                if (candidate.aId == pawnId && candidate.aJobId == job.loadID)
                {
                    plan = candidate;
                    isA = true;
                    return true;
                }
                if (candidate.bId == pawnId && candidate.bJobId == job.loadID)
                {
                    plan = candidate;
                    return true;
                }
            }
            return false;
        }

        private static bool IsMoving(Plan plan, bool isA)
        {
            return isA ? plan.aBounding : plan.bBounding;
        }

        private static void SetMoving(Plan plan, bool isA, bool value)
        {
            if (isA) plan.aBounding = value;
            else plan.bBounding = value;
        }

        private static bool IsFinalRun(Plan plan, bool isA)
        {
            return isA ? plan.aFinalRun : plan.bFinalRun;
        }

        private static void SetFinalRun(Plan plan, bool isA, bool value)
        {
            if (isA) plan.aFinalRun = value;
            else plan.bFinalRun = value;
        }

        private static IntVec3 Goal(Plan plan, bool isA)
        {
            return isA ? plan.aGoal : plan.bGoal;
        }

        private static void SetGoal(Plan plan, bool isA, IntVec3 value)
        {
            if (isA) plan.aGoal = value;
            else plan.bGoal = value;
        }

        private static bool Able(Pawn p)
        {
            return p != null && p.Spawned && !p.Dead && !p.Downed
                && !p.InMentalState;
        }

        private static bool HasCurrentForcedJob(Pawn p)
        {
            return p != null && p.CurJob != null && p.CurJob.playerForced;
        }

        private static bool HasQueuedForcedJob(Pawn p)
        {
            return p != null && p.jobs != null && p.jobs.jobQueue != null
                && p.jobs.jobQueue.AnyPlayerForced;
        }

        private static bool CanDrive(Pawn p)
        {
            return Able(p) && p.jobs != null && !HasCurrentForcedJob(p)
                && !HasQueuedForcedJob(p);
        }

        private static bool CanStartSelected(Pawn p)
        {
            // Choosing a withdrawal is itself a new player order, so it may replace
            // the selected pawn's current forced job. A queued forced order remains
            // future player intent and is never discarded or jumped.
            return Able(p) && p.jobs != null && !HasQueuedForcedJob(p);
        }

        private static bool CanJoinAsBuddy(Pawn p)
        {
            // B joins a paired action rather than being the selected direct actor;
            // preserve unrelated direct work and standing tactical ownership.
            return CanDrive(p) && !CACombatDutySafety.MustYield(p, p.CurJob)
                && !CATactical.HasExplicitOrder(p)
                && !HiddenRegistry.IsHiddenOrOrdered(p) && !IsStackActor(p);
        }

        private static bool IsStackActor(Pawn pawn)
        {
            Lord lord = pawn != null ? pawn.GetLord() : null;
            return lord != null && lord.LordJob is LordJob_CAStackBreach;
        }

        private static bool HasRangedWeapon(Pawn pawn)
        {
            return pawn?.equipment?.Primary != null
                && pawn.equipment.Primary.def.IsRangedWeapon;
        }

        private static void ReleaseSelectedTacticalOwnership(Pawn pawn)
        {
            if (pawn == null) return;
            if (HiddenRegistry.IsHiddenOrOrdered(pawn))
                HiddenRegistry.CancelForReplacement(pawn,
                    "replaced by a withdrawal order");
            HoldMapComponent hold = pawn.Map != null
                ? pawn.Map.GetComponent<HoldMapComponent>() : null;
            if (hold != null && hold.IsHolding(pawn))
                hold.Release(pawn, startNewJob: false);
            else CATactical.Release(pawn, startNewJob: false);

            Lord lord = pawn.GetLord();
            if (lord == null || !(lord.LordJob is LordJob_CAStackBreach)) return;
            lord.Notify_PawnLost(pawn, PawnLostCondition.ForcedByPlayerAction);
            if (pawn.mindState != null) pawn.mindState.duty = null;
            if (pawn.CurJob != null && pawn.CurJob.lord == lord)
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        private static bool CanRemainInPlan(Pawn p, bool draftedAtOrder)
        {
            return CanDrive(p) && p.Drafted == draftedAtOrder
                && (!CACombatDutySafety.MustYield(p, p.CurJob)
                    || p.CurJob != null
                        && p.CurJob.def == CA_Defs.FightingWithdrawal)
                && !CATactical.HasExplicitOrder(p)
                && !HiddenRegistry.IsHiddenOrOrdered(p) && !IsStackActor(p);
        }

        private bool ValidDestination(IntVec3 dest)
        {
            return dest.IsValid && dest.InBounds(map) && dest.Standable(map);
        }

        private static void NormalizeIntent(Plan plan, Pawn actor)
        {
            if (plan == null) return;
            if (plan.episodeId <= 0)
            {
                CAIntentContext restored = CACombatIntent.Restored(actor,
                    CAIntentController.Withdrawal, 0);
                plan.episodeId = restored.EpisodeId;
                plan.controller = (int)restored.Controller;
                plan.issuerId = restored.IssuerId;
                plan.aOrigin = (int)restored.Origin;
                if (plan.bId >= 0) plan.bOrigin = (int)restored.Origin;
            }
            else CACombatIntent.ObserveEpisode(plan.episodeId);
            if (plan.controller == 0)
                plan.controller = (int)CAIntentController.Withdrawal;
            if (plan.aOrigin == 0) plan.aOrigin = (int)CAIntentOrigin.SaveRestore;
            if (plan.bId >= 0 && plan.bOrigin == 0)
                plan.bOrigin = (int)CAIntentOrigin.SaveRestore;
        }

        private static CAIntentContext Intent(Plan plan, bool isA)
        {
            return new CAIntentContext(plan.episodeId,
                (CAIntentOrigin)(isA ? plan.aOrigin : plan.bOrigin),
                (CAIntentController)plan.controller, plan.issuerId);
        }

        private static void MakeSolo(Plan plan, int pawnId, bool draftedAtOrder)
        {
            bool fromA = plan.aId == pawnId;
            int jobId = fromA ? plan.aJobId
                : (plan.bId == pawnId ? plan.bJobId : -1);
            IntVec3 goal = fromA ? plan.aGoal : plan.bGoal;
            bool moving = fromA ? plan.aBounding : plan.bBounding;
            bool finalRun = fromA ? plan.aFinalRun : plan.bFinalRun;
            plan.aId = pawnId;
            plan.bId = -1;
            plan.aDrafted = draftedAtOrder;
            plan.bDrafted = false;
            plan.aJobId = jobId;
            plan.bJobId = -1;
            if (!fromA) plan.aOrigin = plan.bOrigin;
            plan.bOrigin = 0;
            plan.aMoving = true;
            plan.aGoal = goal;
            plan.bGoal = IntVec3.Invalid;
            plan.aBounding = moving;
            plan.bBounding = false;
            plan.aFinalRun = finalRun;
            plan.bFinalRun = false;
            plan.fireScans = 0;
            plan.nextFireScanTick = -1;
            plan.nextBoundRetryTick = -1;
            plan.threatScanTick = -1;
        }

        private void RemoveMember(Plan plan, int pawnId)
        {
            if (plan == null || !plans.Contains(plan)) return;
            Pawn departing = PawnOnMap(pawnId);
            if (departing != null) MovingFire.Set(departing, false);
            if (plan.aId == pawnId)
            {
                if (plan.bId >= 0) MakeSolo(plan, plan.bId, plan.bDrafted);
                else plans.Remove(plan);
            }
            else if (plan.bId == pawnId)
                MakeSolo(plan, plan.aId, plan.aDrafted);
        }

        private void CompletePlan(Plan plan)
        {
            if (plan == null || !plans.Remove(plan)) return;
            Pawn a = PawnOnMap(plan.aId);
            Pawn b = plan.bId >= 0 ? PawnOnMap(plan.bId) : null;
            if (a != null)
                CATrace.Pawn(a, "withdrawal completes",
                    destination: plan.dest, anchor: a.Position,
                    intent: Intent(plan, true));
            if (b != null)
                CATrace.Pawn(b, "withdrawal completes",
                    destination: plan.dest, anchor: b.Position,
                    intent: Intent(plan, false));
            MovingFire.Set(a, false);
            if (b != null) MovingFire.Set(b, false);
        }

        // Validate persisted/runtime ownership. This pass may migrate exact legacy
        // jobs once during FinalizeInit; ordinary component ticks only remove stale
        // claims and never issue work.
        private void ValidatePlans(bool allowLegacyMigration)
        {
            var claimed = new HashSet<int>();
            for (int i = 0; i < plans.Count;)
            {
                Plan plan = plans[i];
                if (plan == null || !ValidDestination(plan.dest))
                {
                    ClearUnclaimedFlags(plan, claimed);
                    plans.RemoveAt(i);
                    continue;
                }

                NormalizeIntent(plan, PawnOnMap(plan.aId));

                Pawn a = Colonist(plan.aId);
                Pawn b = plan.bId >= 0 ? Colonist(plan.bId) : null;
                bool aValid = TryValidateMember(plan, a, true, claimed,
                    allowLegacyMigration);
                bool bValid = plan.bId >= 0 && plan.bId != plan.aId
                    && TryValidateMember(plan, b, false, claimed,
                        allowLegacyMigration);

                if (!aValid)
                {
                    ClearUnclaimedFlag(a, claimed);
                    if (!bValid)
                    {
                        ClearUnclaimedFlag(b, claimed);
                        plans.RemoveAt(i);
                        continue;
                    }
                    MakeSolo(plan, plan.bId, plan.bDrafted);
                    a = b;
                    b = null;
                }
                else if (plan.bId >= 0 && !bValid)
                {
                    ClearUnclaimedFlag(b, claimed);
                    MakeSolo(plan, plan.aId, plan.aDrafted);
                    b = null;
                }

                if (plan.fireScans < 0) plan.fireScans = 0;
                claimed.Add(plan.aId);
                MovingFire.Set(a, true);
                if (plan.bId >= 0)
                {
                    claimed.Add(plan.bId);
                    MovingFire.Set(b, true);
                }
                i++;
            }
        }

        private bool TryValidateMember(Plan plan, Pawn pawn, bool isA,
            HashSet<int> claimed, bool allowLegacyMigration)
        {
            if (pawn == null || claimed.Contains(pawn.thingIDNumber)
                || !CanRemainInPlan(pawn,
                    isA ? plan.aDrafted : plan.bDrafted)) return false;

            int jobId = isA ? plan.aJobId : plan.bJobId;
            Job current = pawn.CurJob;
            if (current != null && current.def == CA_Defs.FightingWithdrawal
                && current.loadID == jobId) return true;
            if (!allowLegacyMigration || current == null
                || current.loadID != jobId || current.playerForced
                || (current.def != JobDefOf.Goto
                    && current.def != JobDefOf.AttackStatic)) return false;

            SeedLegacyMotion(plan, pawn, current, isA);
            Job replacement = MakeWithdrawalJob(plan.dest);
            if (isA) plan.aJobId = replacement.loadID;
            else plan.bJobId = replacement.loadID;
            current.playerInterruptedForced = true;
            if (StartWithdrawalJob(pawn, replacement, allowCurrentForced: false))
                return true;
            return false;
        }

        private static void SeedLegacyMotion(Plan plan, Pawn pawn, Job legacy,
            bool isA)
        {
            if (legacy.def == JobDefOf.Goto && legacy.targetA.Cell.IsValid)
            {
                SetGoal(plan, isA, legacy.targetA.Cell);
                SetMoving(plan, isA, true);
                SetFinalRun(plan, isA, legacy.targetA.Cell == plan.dest);
                return;
            }

            SetGoal(plan, isA, pawn.Position);
            SetMoving(plan, isA, false);
            SetFinalRun(plan, isA, false);
        }

        private void ClearUnclaimedFlags(Plan plan, HashSet<int> claimed)
        {
            if (plan == null) return;
            ClearUnclaimedFlag(PawnOnMap(plan.aId), claimed);
            if (plan.bId >= 0 && plan.bId != plan.aId)
                ClearUnclaimedFlag(PawnOnMap(plan.bId), claimed);
        }

        private static void ClearUnclaimedFlag(Pawn p, HashSet<int> claimed)
        {
            if (p != null && !claimed.Contains(p.thingIDNumber)) MovingFire.Set(p, false);
        }

        private void DisableWithdrawals()
        {
            var members = new List<PlanMemberState>();
            var seen = new HashSet<int>();
            for (int i = 0; i < plans.Count; i++)
            {
                Plan plan = plans[i];
                if (plan == null) continue;
                AddPlanMember(plan.aId, plan.aJobId, seen, members);
                if (plan.bId >= 0)
                    AddPlanMember(plan.bId, plan.bJobId, seen, members);
            }
            plans.Clear();

            for (int i = 0; i < members.Count; i++)
            {
                Pawn p = members[i].Pawn;
                MovingFire.Set(p, false);
                EndPlanJobIfSafe(p, members[i].JobId);
            }
        }

        private void AddPlanMember(int id, int jobId, HashSet<int> seen,
            List<PlanMemberState> members)
        {
            if (id < 0 || !seen.Add(id)) return;
            Pawn p = PawnOnMap(id);
            if (p != null) members.Add(new PlanMemberState(p, jobId));
        }

        private static void EndPlanJobIfSafe(Pawn p, int planJobId)
        {
            if (planJobId < 0 || !Able(p) || p.jobs == null) return;
            Job cur = p.CurJob;
            if (cur == null || cur.loadID != planJobId || cur.playerForced) return;
            if (cur.def == CA_Defs.FightingWithdrawal
                || cur.def == JobDefOf.Goto || cur.def == JobDefOf.AttackStatic)
                p.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        private Pawn Colonist(int id)
        {
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
                if (colonists[i].thingIDNumber == id) return colonists[i];
            return null;
        }

        private Pawn PawnOnMap(int id)
        {
            var pawns = map.mapPawns.AllPawns;
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].thingIDNumber == id) return pawns[i];
            return null;
        }

        private PerceivedPursuer NearestThreat(Pawn p, float radius)
        {
            if (p == null) return default;
            var settings = AwarenessMod.Settings;
            var know = KnowledgeMapComponent.For(map);
            float bestDist = radius;
            if (settings != null && settings.knowledgeContacts)
            {
                if (know == null) return default;
                ThreatContactSnapshot best = default;
                bool found = false;
                var contacts = know.FreshContacts(p);
                for (int i = 0; i < contacts.Count; i++)
                {
                    float distance = p.Position.DistanceTo(contacts[i].Cell);
                    if (distance >= bestDist) continue;
                    bestDist = distance;
                    best = contacts[i];
                    found = true;
                }
                if (!found) return default;
                Pawn visible = VisiblePawnById(p, best.HostileId, radius);
                return new PerceivedPursuer(true, visible,
                    visible != null ? visible.Position : best.Cell);
            }

            Pawn nearest = null;
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn hostile = pawns[i];
                if (!KnowledgeMapComponent.CanCurrentlySeeHostile(
                    p, hostile, radius)) continue;
                float distance = p.Position.DistanceTo(hostile.Position);
                if (distance >= bestDist) continue;
                bestDist = distance;
                nearest = hostile;
            }
            return nearest != null
                ? new PerceivedPursuer(true, nearest, nearest.Position) : default;
        }

        private Pawn VisiblePawnById(Pawn observer, int id, float radius)
        {
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn candidate = pawns[i];
                if (candidate.thingIDNumber != id
                    || !KnowledgeMapComponent.CanCurrentlySeeHostile(
                        observer, candidate, radius)) continue;
                return candidate;
            }
            return null;
        }
    }

    public class JobDriver_CAWithdrawal : JobDriver
    {
        private WithdrawalMapComponent Component
        {
            get
            {
                return pawn != null && pawn.Map != null
                    ? pawn.Map.GetComponent<WithdrawalMapComponent>() : null;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // The actual bound cell changes throughout the job. Each native phase
            // reserves its destination immediately before pathing.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddEndCondition(delegate
            {
                WithdrawalMapComponent component = Component;
                return component != null
                    ? component.DriverCondition(pawn, job)
                    : JobCondition.InterruptForced;
            });
            AddFinishAction(delegate
            {
                // A Never toil has no native movement cleanup. Stop only this
                // driver's path; an incoming player job starts after cleanup and
                // keeps its independently acquired reservation.
                pawn.pather?.StopDead();
                WithdrawalMapComponent component = Component;
                if (component != null)
                    component.NotifyDriverFinished(pawn, job.loadID);
                else MovingFire.Set(pawn, false);
            });

            Toil execute = ToilMaker.MakeToil("CAFightingWithdrawal");
            execute.initAction = delegate
            {
                // Starting a job runs init synchronously. Stop inherited movement here,
                // but defer plan mutation to DriverTick so a covered pair and a legacy
                // load can install both drivers before either one advances the phase.
                pawn.pather?.StopDead();
            };
            execute.tickAction = delegate
            {
                if (pawn.IsHashIntervalTick(4)) TickPlan();
            };
            execute.defaultCompleteMode = ToilCompleteMode.Never;
            execute.socialMode = RandomSocialMode.Off;
            yield return execute;
        }

        public override void Notify_PatherArrived()
        {
            TickPlan();
        }

        private void TickPlan()
        {
            WithdrawalMapComponent component = Component;
            JobCondition condition = component != null
                ? component.TickDriver(pawn, job)
                : JobCondition.InterruptForced;
            if (condition != JobCondition.Ongoing) EndJobWith(condition);
        }
    }
}
