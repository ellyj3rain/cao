using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace ColonistAwareness
{
    // Resolve the durable organization already recorded for a faction. An
    // encounter does not create doctrine, support, membership, or history.
    internal static class CAHostileFactionOrganization
    {
        internal static string KeyFor(Faction faction)
        {
            return faction == null ? null
                : "faction:" + faction.loadID;
        }

        // Recognize an existing faction. Encounter alone supplies no doctrine,
        // history, public support, or practiced capability.
        internal static CAOrganization For(Faction faction)
        {
            if (faction == null) return null;
            var comp = CAOrganizationWorldComponent.Current;
            if (comp == null) return null;
            if (faction.IsPlayer) return comp.EnsureColony();
            return comp.ByKey(KeyFor(faction));
        }
    }

    // WHAT AN ATTACKING FORCE ACTUALLY KNOWS: the settlement graph is
    // public structure, but knowing it requires eyes on it. This reads
    // the same CASettlementLayout the envoys read, and returns only
    // the parts this force has genuinely established - by seeing them
    // now, or by having been here before and written it down.
    internal sealed class CAApproachIntelligence
    {
        internal readonly List<IntVec3> knownGates = new List<IntVec3>();
        internal bool sawDefences;
        internal int rememberedLossesHere;
        internal bool hasDoctrineForPreparedGround;
        internal bool canSpread;

        internal static CAApproachIntelligence Gather(Map map,
            Faction attacker, CARegionalSettlementRecord target)
        {
            var intel = new CAApproachIntelligence();
            try
            {
                CAOrganization org = CAHostileFactionOrganization.For(attacker);
                if (org != null)
                {
                    intel.hasDoctrineForPreparedGround =
                        org.HasCustom("ambush");
                    intel.canSpread = org.HasCustom("line");
                    intel.rememberedLossesHere = CountRememberedLosses(
                        org, target);
                }
                if (target?.layout == null) return intel;

                // eyes: a gate is known when one of this force's people
                // can actually see that cell from where they stand
                var eyes = map.mapPawns.SpawnedPawnsInFaction(attacker)
                    .Where(p => p != null && !p.Downed && p.Awake())
                    .ToList();
                foreach (IntVec3 gate in target.layout.gates)
                {
                    foreach (Pawn p in eyes)
                    {
                        if (!p.Position.InHorDistOf(gate, 60f)) continue;
                        if (!GenSight.LineOfSight(p.Position, gate, map,
                            true)) continue;
                        intel.knownGates.Add(gate);
                        break;
                    }
                }
                // defences are read the same way: seen, not given
                foreach (Pawn p in eyes)
                {
                    foreach (Thing t in map.listerBuildings
                        .allBuildingsColonist)
                    {
                        if (!p.Position.InHorDistOf(t.Position, 45f))
                            continue;
                        if (!GenSight.LineOfSight(p.Position, t.Position,
                            map, true)) continue;
                        if (t.def.building != null
                            && (t.def.building.IsTurret
                                || t.def.building.ai_chillDestination))
                        { intel.sawDefences = true; break; }
                    }
                    if (intel.sawDefences) break;
                }
            }
            catch { }
            return intel;
        }

        private static int CountRememberedLosses(CAOrganization org,
            CARegionalSettlementRecord target)
        {
            try
            {
                string mark = target?.name ?? "this settlement";
                return org.decisionHistory == null ? 0
                    : org.decisionHistory.Count(d => d != null
                        && d.text != null && d.text.Contains(mark)
                        && d.text.Contains("lost"));
            }
            catch { return 0; }
        }
    }
}
