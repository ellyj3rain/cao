using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // Choose an assault entrance from the force's mission, doctrine, observed
    // defenses, and prior losses. Forces without relevant knowledge use the
    // nearest entrance.
    public class CAAssaultApproachMapComponent : MapComponent
    {
        private int nextTick;
        private readonly Dictionary<int, IntVec3> chosen =
            new Dictionary<int, IntVec3>();

        public CAAssaultApproachMapComponent(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame < nextTick) return;
            nextTick = Find.TickManager.TicksGame + 600;
            try { Pulse(); }
            catch (Exception e)
            {
                Log.Warning("[CA] assault approach failed: " + e.Message);
            }
        }

        private void Pulse()
        {
            if (!map.IsPlayerHome) return;
            var graph = map.GetComponent<CAColonyGraphMapComponent>();
            if (graph == null) return;
            CASettlementLayout layout = graph.Layout;
            if (layout == null || layout.gates.Count == 0) return;

            foreach (Faction faction in map.mapPawns.AllPawnsSpawned
                .Where(p => p.HostileTo(Faction.OfPlayer)
                    && p.Faction != null && p.RaceProps.Humanlike)
                .Select(p => p.Faction).Distinct().ToList())
            {
                Advise(faction, layout, graph);
            }
        }

        private void Advise(Faction faction, CASettlementLayout layout,
            CAColonyGraphMapComponent graph)
        {
            List<Pawn> force = map.mapPawns
                .SpawnedPawnsInFaction(faction)
                .Where(p => p != null && !p.Downed && !p.Dead
                    && p.RaceProps.Humanlike && p.Awake()).ToList();
            if (force.Count == 0) { chosen.Remove(faction.loadID); return; }

            CAOrganization org = CAHostileFactionOrganization.For(faction);
            if (org == null) return;
            // The force, not the faction, decides: its people supply
            // the doctrine, this party supplies the discipline to use
            // it. A rabble carrying a proud tradition still charges.
            var registry = map.GetComponent<CATaskForceMapComponent>();
            CATaskForce unit = registry?.ForFaction(faction);
            if (unit == null) return;
            bool readsPreparedGround = unit.CanAct(org, "ambush", 11);
            bool holdsLine = unit.CanAct(org, "line", 22);

            if (!readsPreparedGround && !holdsLine) return;  // walk in

            // only advise while still outside and not yet fighting
            List<Pawn> approaching = force.Where(p =>
                !InContact(p) && !NearColony(p, layout)).ToList();
            if (approaching.Count == 0) return;

            IntVec3 aim;
            if (!chosen.TryGetValue(faction.loadID, out aim)
                || !aim.IsValid)
            {
                aim = Decide(faction, force, unit, org, layout,
                    readsPreparedGround, holdsLine);
                if (!aim.IsValid) return;
                chosen[faction.loadID] = aim;
            }

            foreach (Pawn p in approaching)
            {
                if (p.Position.InHorDistOf(aim, 12f)) continue;
                Job cur = p.CurJob;
                if (cur != null && cur.def == JobDefOf.Goto
                    && cur.targetA.Cell.InHorDistOf(aim, 12f)) continue;
                if (!p.CanReach(aim, PathEndMode.OnCell, Danger.Deadly))
                    continue;
                p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, aim),
                    JobCondition.InterruptForced);
            }
        }

        // The decision itself, made ONLY from what this force has
        // established for itself. The map's truth is never consulted
        // here - that would be privileged knowledge wearing a
        // different coat.
        private IntVec3 Decide(Faction faction, List<Pawn> force,
            CATaskForce unit, CAOrganization org,
            CASettlementLayout layout,
            bool readsPreparedGround, bool holdsLine)
        {
            IntVec3 from = force[0].Position;
            List<IntVec3> seenGuns = unit.knownDefences;
            List<IntVec3> knownWaysIn = unit.knownEntrances;
            // A force that has found no way in yet has nothing to
            // decide between: it advances and looks.
            if (knownWaysIn.Count == 0) return IntVec3.Invalid;

            IntVec3 nearestGate = knownWaysIn
                .OrderBy(g => g.DistanceTo(from)).First();
            if (!readsPreparedGround || seenGuns.Count == 0)
            {
                // no understanding of prepared ground, or nothing seen
                // to understand: the line still spreads, but the way in
                // is the obvious one.
                return holdsLine ? Spread(nearestGate, from) : IntVec3.Invalid;
            }

            // covered gates are refused where an alternative exists.
            // How thoroughly a way in is covered depends on how WIDE
            // it is: two guns seal a doorway, and the same two guns
            // barely reach across the open side of a camp. A force
            // that reads prepared ground reads that too - it is
            // looking at the gap.
            IntVec3 best = IntVec3.Invalid;
            float bestScore = float.MinValue;
            foreach (IntVec3 gate in knownWaysIn)
            {
                float cover = Cover(gate, seenGuns, layout);
                float walk = gate.DistanceTo(from) / 40f;
                float score = -cover * 3f - walk;
                if (score > bestScore) { bestScore = score; best = gate; }
            }
            if (!best.IsValid) return IntVec3.Invalid;

            float coverOnBest = Cover(best, seenGuns, layout);
            if (unit.lossesTakenHere > 0 && best == nearestGate
                && knownWaysIn.Count > 1)
            {
                // they have bled here before: the near way is the one
                // they remember, so they try another
                IntVec3 other = knownWaysIn
                    .Where(g => g != nearestGate)
                    .OrderBy(g => g.DistanceTo(from)).First();
                org.Record("war", "we lost people at this entrance"
                    + " before; we try another way");
                return other;
            }
            if (coverOnBest > 0f)
            {
                // every way in is covered: their mission still stands,
                // so they take the least bad one and say so.
                org.Record("war", "every way into the colony is covered;"
                    + " we go in anyway");
                return best;
            }
            if (best != nearestGate)
                org.Record("war", "the near gate is covered by guns;"
                    + " we come around");
            return best;
        }
        // How covered a way in is, per cell of frontage. A gun that can
        // see the crossing counts; the wider the crossing, the less of
        // it any one gun holds.
        private float Cover(IntVec3 gate, List<IntVec3> seenGuns,
            CASettlementLayout layout)
        {
            float guns = seenGuns.Count(g => g.InHorDistOf(gate, 22f)
                && GenSight.LineOfSight(g, gate, map, true));
            if (guns <= 0f) return 0f;
            int width = layout != null ? layout.WidthOfGate(gate) : 1;
            // a gun covers about four cells of frontage well
            float spans = Mathf.Max(1f, width / 4f);
            return guns / spans;
        }

        // Holding the line means arriving on a frontage, not in a file.
        private IntVec3 Spread(IntVec3 gate, IntVec3 from)
        {
            IntVec3 away = gate - from;
            IntVec3 side = new IntVec3(-away.z, 0, away.x);
            if (side.LengthHorizontalSquared < 1) return gate;
            float len = Mathf.Sqrt(side.LengthHorizontalSquared);
            IntVec3 offset = new IntVec3(
                (int)(side.x / len * 14f), 0, (int)(side.z / len * 14f));
            IntVec3 target = gate + offset;
            return target.InBounds(map) && target.Standable(map)
                ? target : gate;
        }

        private bool InContact(Pawn p)
        {
            return p.mindState?.enemyTarget != null
                || (p.CurJob != null && p.CurJob.def == JobDefOf.AttackMelee)
                || p.stances?.curStance is Stance_Busy;
        }

        private bool NearColony(Pawn p, CASettlementLayout layout)
        {
            // proximity to the built-up area is a physical fact the
            // pawn can feel, not privileged structure
            return layout.core.IsValid
                && p.Position.InHorDistOf(layout.core, 30f);
        }
    }
}
