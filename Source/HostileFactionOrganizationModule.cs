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
    // Give a visiting hostile faction a durable organization record for its
    // doctrine, memory, and support. It reads the same settlement layout as
    // other actors, limited to facts its members have observed.
    internal static class CAHostileFactionOrganization
    {
        internal static string KeyFor(Faction faction)
        {
            return faction == null ? null
                : "faction:" + faction.loadID;
        }

        // Build the faction's organization record from current faction facts.
        internal static CAOrganization For(Faction faction)
        {
            if (faction == null) return null;
            var comp = CAOrganizationWorldComponent.Current;
            if (comp == null) return null;
            if (faction.IsPlayer) return comp.EnsureColony();

            string key = KeyFor(faction);
            CAOrganization existing = comp.ByKey(key);
            if (existing != null) return existing;

            CAOrganization org = comp.EnsureFor(key, faction.Name,
                "a people without a seat on this ground",
                CAOrganizationKind.Faction);
            SeedDoctrine(org, faction);
            org.Record("organization", faction.Name
                + " is recognized on this ground");
            return org;
        }

        // Doctrine on the same terms settlements earn it: practiced
        // war-making grants formation, holding ground grants the line,
        // and only a people who make war their trade understand
        // prepared ground well enough to look for it.
        private static void SeedDoctrine(CAOrganization org,
            Faction faction)
        {
            try
            {
                int tech = (int)(faction.def?.techLevel
                    ?? TechLevel.Neolithic);
                bool warlike = faction.def != null
                    && (faction.def.permanentEnemy
                        || faction.def.naturalEnemy
                        || faction.HostileTo(Faction.OfPlayer));
                int settled = CountSettlements(faction);

                if (tech >= 3 || warlike)
                    Seed(org, "formation");
                if (settled > 0 || tech >= 4)
                    Seed(org, "line");
                // Prepared ground is a professional's idea: a people
                // who raid for a living, or who field a technological
                // army, have met it and learned to look for it.
                if (warlike && tech >= 3)
                    Seed(org, "ambush");
                if (tech >= 4)
                    Seed(org, "status reporting");
                org.publicSupport = Mathf.Clamp01(0.4f + tech * 0.08f);
            }
            catch { }
        }

        private static int CountSettlements(Faction faction)
        {
            try
            {
                return Find.WorldObjects?.Settlements?
                    .Count(s => s.Faction == faction) ?? 0;
            }
            catch { return 0; }
        }

        private static void Seed(CAOrganization org, string key)
        {
            if (org.HasCustom(key)) return;
            org.customs.Add(new CAOrganizationCustom
            {
                key = key,
                source = "faction starting state",
                adoptedTick = Find.TickManager.TicksGame
            });
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
