using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // A task force carries a commander, mission, equipment, and observations.
    public sealed class CATaskForce : IExposable
    {
        public int factionLoadId = -1;
        public int commanderId = -1;
        public string mission = "assault";
        public int arrivedTick;
        // WHAT THIS FORCE HAS ESTABLISHED ITSELF. Never the map's
        // truth - only what these eyes saw, or what they were told,
        // or what they remember from the last time they came.
        public List<IntVec3> knownEntrances = new List<IntVec3>();
        public List<IntVec3> knownDefences = new List<IntVec3>();
        public int lossesTakenHere;
        public bool briefedByPriorVisit;

        public void ExposeData()
        {
            Scribe_Values.Look(ref factionLoadId, "factionLoadId", -1);
            Scribe_Values.Look(ref commanderId, "commanderId", -1);
            Scribe_Values.Look(ref mission, "mission");
            Scribe_Values.Look(ref arrivedTick, "arrivedTick", 0);
            Scribe_Collections.Look(ref knownEntrances, "knownEntrances",
                LookMode.Value);
            Scribe_Collections.Look(ref knownDefences, "knownDefences",
                LookMode.Value);
            Scribe_Values.Look(ref lossesTakenHere, "lossesTakenHere", 0);
            Scribe_Values.Look(ref briefedByPriorVisit,
                "briefedByPriorVisit", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (knownEntrances == null)
                    knownEntrances = new List<IntVec3>();
                if (knownDefences == null)
                    knownDefences = new List<IntVec3>();
            }
        }

        // The force may use only doctrine its organization actually adopted or
        // practiced. Arrival order and deterministic variation do not grant it.
        public bool Practices(CAOrganization factionOrg, string concept)
        {
            return factionOrg?.HasCustom(concept) == true;
        }
    }

    // The forces present on this map, and what each has learned.
    public class CATaskForceMapComponent : MapComponent
    {
        private List<CATaskForce> forces = new List<CATaskForce>();
        private int nextTick;

        public CATaskForceMapComponent(Map map) : base(map) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref forces, "CA_taskForces",
                LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit
                && forces == null) forces = new List<CATaskForce>();
        }

        public CATaskForce ForFaction(Faction faction)
        {
            if (faction == null) return null;
            return forces.FirstOrDefault(f => f != null
                && f.factionLoadId == faction.loadID);
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame < nextTick) return;
            nextTick = Find.TickManager.TicksGame + 250;
            try { Pulse(); }
            catch (Exception e)
            {
                Log.Warning("[CA] task force pulse: " + e.Message);
            }
        }

        private void Pulse()
        {
            var present = map.mapPawns.AllPawnsSpawned
                .Where(p => p?.Faction != null && p.RaceProps.Humanlike
                    && p.HostileTo(Faction.OfPlayer) && !p.Dead)
                .GroupBy(p => p.Faction).ToList();

            // forces that have left are forgotten as formations; what
            // they learned goes home with their organization
            forces.RemoveAll(f => f != null
                && !present.Any(g => g.Key.loadID == f.factionLoadId));

            foreach (var group in present)
            {
                CATaskForce force = ForFaction(group.Key);
                if (force == null)
                {
                    force = Form(group.Key, group.ToList());
                    forces.Add(force);
                }
                Observe(force, group.ToList());
            }
        }

        // Forming up: who leads, how disciplined, and what they were
        // told before they came.
        private CATaskForce Form(Faction faction, List<Pawn> pawns)
        {
            Pawn commander = pawns
                .OrderByDescending(p => p.skills?.GetSkill(
                    SkillDefOf.Melee)?.Level ?? 0)
                .ThenByDescending(p => p.kindDef?.combatPower ?? 0f)
                .FirstOrDefault();
            CAOrganization org = CAHostileFactionOrganization.For(faction);

            var force = new CATaskForce
            {
                factionLoadId = faction.loadID,
                commanderId = commander?.thingIDNumber ?? -1,
                arrivedTick = Find.TickManager.TicksGame,
                mission = faction.HostileTo(Faction.OfPlayer)
                    ? "assault" : "visit"
            };

            // briefed by their organization's memory of this place: not
            // the map's truth, only what was written down before
            if (org?.decisionHistory != null)
            {
                int priors = org.decisionHistory.Count(d => d != null
                    && d.text != null && d.text.Contains("colony"));
                if (priors > 0)
                {
                    force.briefedByPriorVisit = true;
                    force.lossesTakenHere = org.decisionHistory
                        .Count(d => d?.text != null
                            && d.text.Contains("lost"));
                }
            }
            org?.Record("war", "a force of " + pawns.Count
                + " forms up against the colony"
                + (force.briefedByPriorVisit
                    ? ", carrying what we learned last time" : ""));
            return force;
        }

        // Learning: only what these people can actually see from where
        // they stand, accumulated as they advance.
        private void Observe(CATaskForce force, List<Pawn> pawns)
        {
            var graph = map.GetComponent<CAColonyGraphMapComponent>();
            if (graph == null) return;
            CASettlementLayout truth = graph.Layout;
            if (truth == null) return;

            foreach (Pawn p in pawns)
            {
                if (p.Downed || !p.Awake()) continue;
                float sight = 40f
                    + (p.health?.capacities?.GetLevel(
                        PawnCapacityDefOf.Sight) ?? 1f) * 20f;
                foreach (IntVec3 gate in truth.gates)
                {
                    if (force.knownEntrances.Contains(gate)) continue;
                    if (!p.Position.InHorDistOf(gate, sight)) continue;
                    if (!GenSight.LineOfSight(p.Position, gate, map,
                        true)) continue;
                    force.knownEntrances.Add(gate);
                }
                foreach (IntVec3 gun in graph.SeenDefences)
                {
                    if (force.knownDefences.Contains(gun)) continue;
                    if (!p.Position.InHorDistOf(gun, sight)) continue;
                    if (!GenSight.LineOfSight(p.Position, gun, map,
                        true)) continue;
                    force.knownDefences.Add(gun);
                }
            }
        }

        // Losses are remembered by the organization, not the corpse.
        public void NoteLoss(Faction faction)
        {
            CATaskForce force = ForFaction(faction);
            if (force != null) force.lossesTakenHere++;
        }
    }
}
