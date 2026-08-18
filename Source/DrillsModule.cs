using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // Retained as an empty shell so saves that recorded this component keep loading.
    // The drill state machine now lives in LordJob_CAStackBreach - the game's own
    // group-AI substrate - and this class only forwards the old query surface.
    public class DrillsMapComponent : MapComponent
    {
        public DrillsMapComponent(Map map) : base(map) { }

        public bool IsStacked(Building_Door door, List<Pawn> team)
        {
            var lord = Drills.StackLordFor(map, door);
            if (lord == null) return false;
            if (!((LordJob_CAStackBreach)lord.LordJob).InStackPhase) return false;
            for (int i = 0; i < team.Count; i++)
                if (lord.ownedPawns.Contains(team[i])) return true;
            return false;
        }

        public int StackCount()
        {
            return Drills.CountStackedLords(map);
        }

        public void BreachEverything()
        {
            Drills.BreachAllStacks(map);
        }
    }

    public static class Drills
    {
        // The door knows its own orientation - the wall runs perpendicular to the
        // passage. Slots walk OUTWARD along the wall on the team's side, skipping
        // unstandable cells by continuing along the wall - never scattering radially.
        public static void StackOnDoor(List<Pawn> team, Building_Door door, Map map)
        {
            IntVec3 d = door.Position;
            IntVec3 wallAxis, perp;
            ComputeAxes(door, map, out wallAxis, out perp);

            // One drill per door. A stack still forming or holding is replaced by the
            // new order; a team already through the door is NOT silently yanked
            // mid-assault - stand them down deliberately if that's what you want.
            var existing = StackLordFor(map, door);
            if (existing != null)
            {
                var existingJob = (LordJob_CAStackBreach)existing.LordJob;
                if (existingJob.InStackPhase)
                {
                    map.lordManager.RemoveLord(existing); // synchronous: frees the pawns now
                }
                else
                {
                    Messages.Message("A team is already clearing through that door - stand them down first.",
                        new LookTargets(door), MessageTypeDefOf.RejectInput, false);
                    return;
                }
            }

            var centroid = Vector3.zero;
            var usable = new List<Pawn>();
            for (int i = 0; i < team.Count; i++)
            {
                var p = team[i];
                var currentLord = p.GetLord();
                if (currentLord != null)
                {
                    var drill = currentLord.LordJob as LordJob_CAStackBreach;
                    if (drill != null && drill.InStackPhase)
                    {
                        // A new stack order re-tasks them from a drill still forming.
                        currentLord.Notify_PawnLost(p, PawnLostCondition.ForcedByPlayerAction);
                        if (p.CurJob != null && p.CurJob.lord == currentLord)
                            p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    }
                    else if (currentLord.LordJob is LordJob_CATactical)
                    {
                        // Standing hold/ambush superseded by the stack order.
                        var holdComp = map.GetComponent<HoldMapComponent>();
                        if (holdComp != null) holdComp.Release(p);
                        HiddenRegistry.Reveal(p);
                        CATactical.Release(p);
                    }
                    else
                    {
                        // Mid-assault, ritual, caravan - genuinely spoken for.
                        Messages.Message(p.LabelShort + " is committed elsewhere and sits this one out.",
                            p, MessageTypeDefOf.RejectInput, false);
                        continue;
                    }
                }
                usable.Add(p);
                centroid += p.Position.ToVector3();
            }
            if (usable.Count == 0)
            {
                Messages.Message("No one available to stack.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            centroid /= usable.Count;
            float side = Vector3.Dot(centroid - d.ToVector3(), perp.ToVector3());
            var near = side >= 0f ? perp : perp * -1;
            var far = near * -1;

            var sorted = new List<Pawn>(usable);
            sorted.Sort(delegate (Pawn a, Pawn b) { return MeleeOf(b).CompareTo(MeleeOf(a)); });

            int lanePlus = 1, laneMinus = 1;
            var stackers = new List<Pawn>();
            var slots = new List<IntVec3>();
            for (int i = 0; i < sorted.Count; i++)
            {
                // Preferred side by parity; overflow to the other side when this
                // side's wall runs out rather than shedding the pawn.
                IntVec3 slot = NextSlot(d, wallAxis, near, map, i % 2 == 0, ref lanePlus, ref laneMinus);
                if (!slot.IsValid)
                    slot = NextSlot(d, wallAxis, near, map, i % 2 != 0, ref lanePlus, ref laneMinus);
                if (!slot.IsValid)
                {
                    Messages.Message(sorted[i].LabelShort + ": no room on the wall - sits this one out.",
                        sorted[i], MessageTypeDefOf.RejectInput, false);
                    continue;
                }
                stackers.Add(sorted[i]);
                slots.Add(slot);
            }
            if (stackers.Count == 0)
            {
                Messages.Message("No room to stack on that wall.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            bool autoGo = true;
            for (int i = 0; i < stackers.Count; i++)
                if (!CABehaviorGate.StableProfileAllows(stackers[i],
                        "support.stack_auto_breach"))
                { autoGo = false; break; }

            var job = new LordJob_CAStackBreach(door, stackers, slots, wallAxis, far, autoGo);
            LordMaker.MakeNewLord(Faction.OfPlayer, job, map, stackers);
            CATrace.Log("stack lord created at " + d + " (" + stackers.Count + ")");
            Messages.Message("Stacking on the door (" + stackers.Count + ")"
                + (autoGo ? " - they will breach when set." : " - breach on your order."),
                new LookTargets(door), MessageTypeDefOf.SilentInput, false);
        }

        // Immediate assault: give the standing stack the go, or form one and send it.
        public static void BreachAndClear(List<Pawn> team, Building_Door door, Map map)
        {
            var lord = StackLordFor(map, door);
            if (lord != null && !((LordJob_CAStackBreach)lord.LordJob).InStackPhase)
            {
                Messages.Message("That team is already through the door.",
                    new LookTargets(door), MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (lord == null)
            {
                StackOnDoor(team, door, map);
                lord = StackLordFor(map, door);
            }
            if (lord != null) lord.ReceiveMemo(LordJob_CAStackBreach.MemoGo);
        }

        public static void BreachAllStacks(Map map)
        {
            var lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                var job = lords[i].LordJob as LordJob_CAStackBreach;
                if (job != null && job.InStackPhase)
                    lords[i].ReceiveMemo(LordJob_CAStackBreach.MemoGo);
            }
        }

        public static Lord StackLordFor(Map map, Building_Door door)
        {
            if (door == null) return null;
            var lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                // A stood-down lord lingers in End until the manager's next tick
                // (which never comes while paused) - it is a corpse, not a drill.
                if (lords[i].CurLordToil is LordToil_End) continue;
                var job = lords[i].LordJob as LordJob_CAStackBreach;
                if (job != null && job.DoorCell == door.Position) return lords[i];
            }
            return null;
        }

        // One slot attempt on one side of the door, advancing that side's lane counter.
        private static IntVec3 NextSlot(IntVec3 d, IntVec3 wallAxis, IntVec3 near, Map map,
            bool plusSide, ref int lanePlus, ref int laneMinus)
        {
            for (int tries = 0; tries < 7; tries++)
            {
                int lane = plusSide ? lanePlus : laneMinus;
                if (lane > 9) break; // off the wall entirely
                var c = d + (plusSide ? wallAxis : wallAxis * -1) * lane + near;
                if (plusSide) lanePlus++; else laneMinus++;
                if (!c.InBounds(map) || !c.Standable(map)) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        public static int CountStackedLords(Map map)
        {
            int n = 0;
            var lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                var job = lords[i].LordJob as LordJob_CAStackBreach;
                if (job != null && job.InStackPhase) n++;
            }
            return n;
        }

        // Points of domination inside the room: hooks along the inside wall first,
        // depth after, minimum 2-cell spacing.
        public static List<IntVec3> ComputeClearPoints(IntVec3 entry, IntVec3 doorCell,
            IntVec3 wallAxis, IntVec3 far, int count, Map map)
        {
            var result = new List<IntVec3>();
            if (!entry.InBounds(map)) return result;

            var room = entry.GetRoom(map);
            var candidates = new List<IntVec3>();
            if (room != null && !room.TouchesMapEdge)
            {
                foreach (var c in room.Cells)
                    if (c.InHorDistOf(entry, 11.9f) && c.Standable(map)) candidates.Add(c);
            }
            else
            {
                int n2 = GenRadial.NumCellsInRadius(6.9f);
                for (int i = 0; i < n2; i++)
                {
                    var c = entry + GenRadial.RadialPattern[i];
                    if (c.InBounds(map) && c.Standable(map)
                        && Vector3.Dot((c - doorCell).ToVector3().normalized, far.ToVector3()) > 0.2f)
                        candidates.Add(c);
                }
            }
            if (candidates.Count == 0) { result.Add(entry); return result; }

            candidates.Sort(delegate (IntVec3 a, IntVec3 b)
            {
                return ClearScore(a, entry, wallAxis, far).CompareTo(ClearScore(b, entry, wallAxis, far));
            });

            for (int i = 0; i < candidates.Count && result.Count < count; i++)
            {
                var c = candidates[i];
                bool tooClose = false;
                for (int j = 0; j < result.Count; j++)
                    if (c.InHorDistOf(result[j], 1.9f)) { tooClose = true; break; }
                if (!tooClose) result.Add(c);
            }
            if (result.Count == 0) result.Add(entry);
            return result;
        }

        public static void ComputeAxes(Building_Door door, Map map, out IntVec3 wallAxis, out IntVec3 perp)
        {
            IntVec3 d = door.Position;
            if (door.Rotation == Rot4.North || door.Rotation == Rot4.South)
            { wallAxis = IntVec3.East; perp = IntVec3.North; }
            else
            { wallAxis = IntVec3.North; perp = IntVec3.East; }
            // sanity: if the door def disagrees with the edifice line, trust the walls
            if (!IsWallish(map, d + wallAxis) && !IsWallish(map, d + wallAxis * -1)
                && (IsWallish(map, d + perp) || IsWallish(map, d + perp * -1)))
            { var t = wallAxis; wallAxis = perp; perp = t; }
        }

        // The leader relays: fire team stacks on the pointed door, gated on command contact.
        public static List<Pawn> FireteamOf(Pawn leader, int team, Map map)
        {
            var result = new List<Pawn>();
            int sq = SquadComponent.SquadOf(leader);
            if (sq <= 0) return result;
            var colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                var m = colonists[i];
                if (m == leader || m.Dead || m.Downed) continue;
                if (SquadComponent.SquadOf(m) != sq || SquadComponent.FireteamOf(m) != team) continue;
                if (m.WorkTagIsDisabled(WorkTags.Violent)) continue;
                if (!CommsModule.CanCommand(leader, m)) continue;
                result.Add(m);
            }
            return result;
        }

        private static float ClearScore(IntVec3 c, IntVec3 entry, IntVec3 wallAxis, IntVec3 far)
        {
            var delta = c - entry;
            float lateral = Mathf.Abs(Vector3.Dot(delta.ToVector3(), wallAxis.ToVector3()));
            float depth = Mathf.Abs(Vector3.Dot(delta.ToVector3(), far.ToVector3()));
            return depth * 2f - lateral + c.DistanceTo(entry) * 0.5f;
        }

        private static bool IsWallish(Map map, IntVec3 c)
        {
            if (!c.InBounds(map)) return false;
            var ed = c.GetEdifice(map);
            return ed != null && ed.def.passability == Traversability.Impassable;
        }

        public static int MeleeOf(Pawn p)
        {
            return p.skills != null ? p.skills.GetSkill(SkillDefOf.Melee).Level : 0;
        }
    }
}
