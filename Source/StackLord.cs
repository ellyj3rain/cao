using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // The door stack as the game itself models group behavior: a Lord that OWNS the
    // team, knows every slot and role, and walks the whole unit through
    //   Form -> Set -> Breach/Clear -> Reconvene -> stand down
    // on real transitions. Duties drive the individuals; drafting or a direct player
    // order on a member simply shadows their duty and it resumes when the player's
    // hand lifts. The state machine scribes with the save.
    public class LordJob_CAStackBreach : LordJob
    {
        public const string MemoAllSet = "CA_AllSet";
        public const string MemoGo = "CA_Go";
        public const string MemoRoomClear = "CA_RoomClear";
        public const string MemoStandDown = "CA_StandDown";

        private Building_Door door;
        private List<Pawn> slotPawns = new List<Pawn>();
        private List<IntVec3> slotCells = new List<IntVec3>();
        private List<Pawn> clearPawns = new List<Pawn>();
        private List<IntVec3> clearCells = new List<IntVec3>();
        private IntVec3 doorCell = IntVec3.Invalid;
        private IntVec3 wallAxis = IntVec3.Invalid;
        private IntVec3 farSide = IntVec3.Invalid;   // into the room being cleared
        // This is an auto-release request, not saved authorization. The full
        // behavior contract is re-evaluated at the release boundary.
        private bool autoGo;
        private int autoGoReadyTick = -1;

        private LordToil_CAForm formToil;
        private LordToil_CASet setToil;
        private LordToil_CAClear clearToil;
        private LordToil_CAReconvene reconveneToil;

        public override bool CanAutoAddPawns => false;

        public IntVec3 DoorCell => doorCell;
        public IntVec3 FarSide => farSide;
        public IntVec3 WallAxis => wallAxis;
        public IntVec3 Entry => doorCell + farSide;
        public IntVec3 RallyPoint => doorCell - farSide;
        public bool InStackPhase
        {
            get { return lord != null && (lord.CurLordToil == formToil || lord.CurLordToil == setToil); }
        }

        public LordJob_CAStackBreach() { }

        public LordJob_CAStackBreach(Building_Door door, List<Pawn> team, List<IntVec3> slots,
            IntVec3 wallAxis, IntVec3 farSide, bool autoGo)
        {
            this.door = door;
            doorCell = door.Position;
            slotPawns = new List<Pawn>(team);
            slotCells = new List<IntVec3>(slots);
            this.wallAxis = wallAxis;
            this.farSide = farSide;
            this.autoGo = autoGo;
        }

        public IntVec3 SlotFor(Pawn p)
        {
            int i = slotPawns.IndexOf(p);
            return i >= 0 && i < slotCells.Count ? slotCells[i] : IntVec3.Invalid;
        }

        public IntVec3 ClearPointFor(Pawn p)
        {
            int i = clearPawns.IndexOf(p);
            return i >= 0 && i < clearCells.Count ? clearCells[i] : Entry;
        }

        // Point-of-domination assignment for the current members, hooks first.
        public void ComputeClearAssignments()
        {
            clearPawns.Clear();
            clearCells.Clear();
            var points = Drills.ComputeClearPoints(Entry, doorCell, wallAxis, farSide,
                lord.ownedPawns.Count, base.Map);
            var sorted = new List<Pawn>(lord.ownedPawns);
            sorted.Sort((a, b) => Drills.MeleeOf(b).CompareTo(Drills.MeleeOf(a)));
            for (int i = 0; i < sorted.Count && i < points.Count; i++)
            {
                clearPawns.Add(sorted[i]);
                clearCells.Add(points[i]);
            }
        }

        public bool HostileNearEntry(float radius)
        {
            var all = base.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                var h = all[i];
                if (h.Dead || h.Downed) continue;
                // Mental-state-aware: manhunters and berserkers count as contact too.
                if (!GenHostility.HostileTo(h, Faction.OfPlayer)) continue;
                if (h.Position.InHorDistOf(Entry, radius)) return true;
            }
            return false;
        }

        public static Rot4 FacingFromTo(IntVec3 from, IntVec3 to)
        {
            return FacingFromTo(from, to, IntVec3.Invalid);
        }

        // On an exact diagonal, prefer the cardinal along preferAxis - a stacker on the
        // first lane sights ALONG the wall toward the door, never into the wall itself.
        public static Rot4 FacingFromTo(IntVec3 from, IntVec3 to, IntVec3 preferAxis)
        {
            IntVec3 d = to - from;
            int ax = Mathf.Abs(d.x), az = Mathf.Abs(d.z);
            bool useX = ax > az || (ax == az && (preferAxis.IsValid ? preferAxis.x != 0 : true));
            if (useX && ax > 0) return d.x >= 0 ? Rot4.East : Rot4.West;
            if (az > 0) return d.z >= 0 ? Rot4.North : Rot4.South;
            return Rot4.North;
        }

        // Apply a freshly assigned duty now - unless the player's hand is on the pawn.
        // A drafted pawn, a player-forced current job, or queued player orders all mean
        // the duty waits its turn; it starts naturally when the player's hand lifts.
        // Never clears prioritized work or the job queue - the duty shadows, it does
        // not destroy.
        public static void PushDuty(Pawn p)
        {
            if (p.Drafted) return;
            if (p.CurJob != null && p.CurJob.playerForced) return;
            if (p.jobs == null) return;
            if (p.jobs.jobQueue != null && p.jobs.jobQueue.AnyPlayerForced) return;
            p.jobs.CheckForJobOverride();
        }

        public override StateGraph CreateGraph()
        {
            var g = new StateGraph();
            formToil = new LordToil_CAForm();
            g.AddToil(formToil);
            setToil = new LordToil_CASet();
            g.AddToil(setToil);
            clearToil = new LordToil_CAClear();
            g.AddToil(clearToil);
            reconveneToil = new LordToil_CAReconvene();
            g.AddToil(reconveneToil);
            var end = new LordToil_End();
            g.AddToil(end);

            var toSet = new Transition(formToil, setToil);
            toSet.AddTrigger(new Trigger_Memo(MemoAllSet));
            toSet.AddPreAction(new TransitionAction_Custom((Action)delegate
            {
                Messages.Message("Stack set (" + lord.ownedPawns.Count + ")"
                    + (autoGo ? " - breaching." : " - breach on your order."),
                    new LookTargets(doorCell, base.Map), MessageTypeDefOf.SilentInput, false);
            }));
            g.AddTransition(toSet);

            var goEarly = new Transition(formToil, clearToil);
            goEarly.AddTrigger(new Trigger_Memo(MemoGo));
            g.AddTransition(goEarly);

            var go = new Transition(setToil, clearToil);
            go.AddTrigger(new Trigger_Memo(MemoGo));
            g.AddTransition(go);

            var roomClear = new Transition(clearToil, reconveneToil);
            roomClear.AddTrigger(new Trigger_Memo(MemoRoomClear));
            roomClear.AddPreAction(new TransitionAction_Custom((Action)delegate
            {
                Messages.Message("Room clear - reconvening.",
                    new LookTargets(RallyPoint, base.Map), MessageTypeDefOf.SilentInput, false);
            }));
            g.AddTransition(roomClear);

            var done = new Transition(reconveneToil, end);
            done.AddTrigger(new Trigger_TicksPassed(400));
            done.AddPreAction(new TransitionAction_Custom((Action)delegate
            {
                Messages.Message("Drill complete - team stands down.",
                    new LookTargets(RallyPoint, base.Map), MessageTypeDefOf.SilentInput, false);
            }));
            g.AddTransition(done);

            var standDown = new Transition(formToil, end);
            standDown.AddSources(new List<LordToil> { setToil, clearToil, reconveneToil });
            standDown.AddTrigger(new Trigger_Memo(MemoStandDown));
            g.AddTransition(standDown);

            var neverFormed = new Transition(formToil, end);
            neverFormed.AddTrigger(new Trigger_TicksPassed(3000));
            neverFormed.AddPreAction(new TransitionAction_Custom((Action)delegate
            {
                Log.Warning("[Colonist Awareness] UNPLANNED: stack never formed at "
                    + doorCell + " - dropping the drill");
                Messages.Message("Stack never formed - drill dropped.",
                    new LookTargets(doorCell, base.Map), MessageTypeDefOf.CautionInput, false);
            }));
            g.AddTransition(neverFormed);

            return g;
        }

        public override void LordJobTick()
        {
            if (!autoGo) return;

            // A saved request never survives its permission being disabled.
            // The player-authored stack itself remains available for an
            // explicit Breach or Stand down order.
            AwarenessSettings settings = AwarenessMod.Settings;
            if (settings == null || !settings.battleDrills)
            {
                autoGo = false;
                autoGoReadyTick = -1;
                return;
            }

            if (lord.CurLordToil != setToil)
            {
                autoGoReadyTick = -1;
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (autoGoReadyTick < 0)
            {
                autoGoReadyTick = now;
                return;
            }
            if (now - autoGoReadyTick < 180) return;

            CABehaviorDecision denied;
            if (!CanAutoRelease(out denied))
            {
                // Authorization is consumed exactly once. A refused automatic
                // release does not keep polling until circumstances happen to
                // permit an action the player did not renew.
                autoGo = false;
                autoGoReadyTick = -1;
                return;
            }

            autoGo = false;
            autoGoReadyTick = -1;
            lord.ReceiveMemo(MemoGo);
        }

        private bool CanAutoRelease(out CABehaviorDecision denied)
        {
            denied = default(CABehaviorDecision);
            Map map = base.Map;
            bool doorValid = door != null && door.Spawned && door.Map == map
                && door.Position == doorCell && Entry.IsValid
                && Entry.InBounds(map);
            if (!doorValid) return false;

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                bool capable = pawn != null && !pawn.Dead && !pawn.Downed
                    && pawn.Spawned && pawn.Map == map && pawn.jobs != null;
                bool owned = capable && pawn.GetLord() == lord
                    && !CATactical.HasForeignPlayerForcedJob(pawn);
                bool set = owned && SlotFor(pawn).IsValid
                    && pawn.Position.InHorDistOf(SlotFor(pawn), 1.5f);
                var context = CABehaviorContext.ForPawn(pawn,
                    CAAuthorityOrigin.PlayerDelegated,
                    authoritySatisfied: owned,
                    knowledgeSatisfied: set && doorValid,
                    knowledgeFresh: set && doorValid,
                    liveValidated: set && doorValid,
                    capabilitySatisfied: capable,
                    materialSatisfied: doorValid,
                    currentIntentCompatible: owned,
                    authorityBasis: "active player-authored stack episode",
                    knowledgeBasis: "all assigned stack members set at "
                        + doorCell,
                    owner: "stack lord at " + doorCell);
                CABehaviorDecision decision = CABehaviorGate.Evaluate(
                    "support.stack_auto_breach", context);
                if (!decision.Allowed)
                {
                    denied = decision;
                    return false;
                }
            }
            return lord.ownedPawns.Count > 0;
        }

        public override IEnumerable<Gizmo> GetPawnGizmos(Pawn p)
        {
            if (InStackPhase)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Breach",
                    defaultDesc = "Give the go - the stack breaches and clears now.",
                    icon = TexCommand.Attack,
                    action = delegate { lord.ReceiveMemo(MemoGo); }
                };
            }
            yield return new Command_Action
            {
                defaultLabel = "Stand down",
                defaultDesc = "Dissolve the stack - everyone returns to their own work.",
                icon = TexCommand.ClearPrioritizedWork,
                action = delegate { lord.ReceiveMemo(MemoStandDown); }
            };
        }

        public override string GetReport(Pawn pawn)
        {
            if (lord.CurLordToil == formToil) return "Stacking up";
            if (lord.CurLordToil == setToil) return "Stacked and set";
            if (lord.CurLordToil == clearToil) return "Clearing";
            if (lord.CurLordToil == reconveneToil) return "Reconvening";
            return null;
        }

        // An in-flight direct player order survives the lord's teardown.
        public override bool EndPawnJobOnCleanup(Pawn p)
        {
            return p.CurJob == null || !p.CurJob.playerForced;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref door, "door");
            Scribe_Collections.Look(ref slotPawns, "slotPawns", LookMode.Reference);
            Scribe_Collections.Look(ref slotCells, "slotCells", LookMode.Value);
            Scribe_Collections.Look(ref clearPawns, "clearPawns", LookMode.Reference);
            Scribe_Collections.Look(ref clearCells, "clearCells", LookMode.Value);
            Scribe_Values.Look(ref doorCell, "doorCell", IntVec3.Invalid);
            Scribe_Values.Look(ref wallAxis, "wallAxis", IntVec3.Invalid);
            Scribe_Values.Look(ref farSide, "farSide", IntVec3.Invalid);
            Scribe_Values.Look(ref autoGo, "autoGo", defaultValue: false);
            Scribe_Values.Look(ref autoGoReadyTick, "autoGoReadyTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (slotPawns == null) slotPawns = new List<Pawn>();
                if (slotCells == null) slotCells = new List<IntVec3>();
                if (clearPawns == null) clearPawns = new List<Pawn>();
                if (clearCells == null) clearCells = new List<IntVec3>();
                if (AwarenessMod.Settings == null
                    || !AwarenessMod.Settings.battleDrills)
                {
                    autoGo = false;
                    autoGoReadyTick = -1;
                }
            }
        }
    }

    // Form: everyone moves to their slot on the wall, muzzle toward the door.
    public class LordToil_CAForm : LordToil
    {
        protected LordJob_CAStackBreach Job => (LordJob_CAStackBreach)lord.LordJob;

        public override bool AllowSatisfyLongNeeds => false;

        public override void UpdateAllDuties()
        {
            foreach (Pawn p in lord.ownedPawns)
            {
                if (p.mindState == null) continue;
                IntVec3 slot = Job.SlotFor(p);
                if (!slot.IsValid) slot = Job.RallyPoint;
                var duty = new PawnDuty(CA_Defs.StackPosition, slot);
                duty.locomotion = LocomotionUrgency.Jog;
                duty.overrideFacing = LordJob_CAStackBreach.FacingFromTo(slot, Job.DoorCell, Job.WallAxis);
                p.mindState.duty = duty;
                CATrace.Pawn(p, "stack duty -> " + slot);
                LordJob_CAStackBreach.PushDuty(p);
            }
        }

        public override void LordToilTick()
        {
            if (lord.ticksInToil % 30 != 0) return;
            bool anyPlaced = false;
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn p = lord.ownedPawns[i];
                if (p.Drafted) return; // the player is borrowing this one - not set
                IntVec3 slot = Job.SlotFor(p);
                if (!slot.IsValid) continue;
                if (p.Position != slot) return;
                anyPlaced = true;
            }
            if (anyPlaced) lord.ReceiveMemo(LordJob_CAStackBreach.MemoAllSet);
        }
    }

    // Set: holding the stack, waiting on the go-ahead. Same posture, no arrival poll.
    public class LordToil_CASet : LordToil_CAForm
    {
        public override void LordToilTick() { }
    }

    // Scribed clear-phase state: the quiet-clock baseline survives save/load.
    public class LordToilData_CAClear : LordToilData
    {
        public int lastContactTick = -1;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref lastContactTick, "lastContactTick", -1);
        }
    }

    // Breach and clear: flow through the door to points of domination, hooks first,
    // engage on contact. Quiet for 600 ticks ends the phase.
    public class LordToil_CAClear : LordToil
    {
        public LordToil_CAClear()
        {
            data = new LordToilData_CAClear();
        }

        private LordToilData_CAClear ClearData => (LordToilData_CAClear)data;

        protected LordJob_CAStackBreach Job => (LordJob_CAStackBreach)lord.LordJob;

        public override bool AllowSatisfyLongNeeds => false;

        public override void Init()
        {
            base.Init();
            Job.ComputeClearAssignments();
            foreach (Pawn p in lord.ownedPawns)
                HiddenRegistry.GrantFirstStrike(p);
            CATrace.Log("breach GO at " + Job.DoorCell);
        }

        public override void UpdateAllDuties()
        {
            if (lord.ownedPawns.Count == 0) return;
            foreach (Pawn p in lord.ownedPawns)
            {
                if (p.mindState == null) continue;
                IntVec3 point = Job.ClearPointFor(p);
                var duty = new PawnDuty(CA_Defs.ClearRoom, point);
                duty.radius = 12f;
                duty.locomotion = LocomotionUrgency.Sprint;
                duty.overrideFacing = LordJob_CAStackBreach.FacingFromTo(Job.DoorCell, point, Job.FarSide);
                p.mindState.duty = duty;
                CATrace.Pawn(p, "clear duty -> " + point);
                LordJob_CAStackBreach.PushDuty(p);
            }
        }

        public override void LordToilTick()
        {
            if (lord.ticksInToil % 30 != 0) return;
            if (ClearData.lastContactTick < 0) ClearData.lastContactTick = lord.ticksInToil;
            if (Job.HostileNearEntry(12.9f)) ClearData.lastContactTick = lord.ticksInToil;
            else if (lord.ticksInToil - ClearData.lastContactTick > 600)
                lord.ReceiveMemo(LordJob_CAStackBreach.MemoRoomClear);
        }
    }

    // Reconvene: rally outside the entry with outward-facing security, then the
    // graph stands the team down and the lord dissolves.
    public class LordToil_CAReconvene : LordToil
    {
        protected LordJob_CAStackBreach Job => (LordJob_CAStackBreach)lord.LordJob;

        public override void UpdateAllDuties()
        {
            IntVec3 rally = Job.RallyPoint;
            int slot = 0;
            foreach (Pawn p in lord.ownedPawns)
            {
                if (p.mindState == null) continue;
                IntVec3 rough = rally + GenRadial.RadialPattern[slot + 1];
                IntVec3 cell = CellFinder.StandableCellNear(rough, base.Map, 3f, null);
                if (!cell.IsValid) cell = rally;
                var duty = new PawnDuty(CA_Defs.StackPosition, cell);
                duty.locomotion = LocomotionUrgency.Jog;
                // 360 security: face away from the rally centroid
                duty.overrideFacing = LordJob_CAStackBreach.FacingFromTo(rally, cell.Equals(rally) ? cell + IntVec3.North : cell);
                p.mindState.duty = duty;
                slot++;
                LordJob_CAStackBreach.PushDuty(p);
            }
        }
    }
}
