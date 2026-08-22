using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // THE LAND A SETTLEMENT LIVES FROM IS PART OF THE SETTLEMENT. An
    // established society that operates agriculture has standing fields;
    // one that forages does not, and legitimately fades into wilderness.
    // This is simultaneously the self-sufficiency substrate (the causal
    // explanation for how the population persisted to the starting date)
    // and the end of the artificial settlement boundary: fabric terminates
    // differently in different places because the land uses that occupy
    // the edge differ -- worked fields on a farming side, harbor works on
    // a trading shore, nothing at all where nothing is operated.
    //
    // Every plot is traced to the represented operator of the agriculture
    // fact that causes it (the program entry's operatorIdentity), never to
    // anonymous settlement labor. Weak capability stays weak: fewer, more
    // ragged plots at low agricultural knowledge, and a settlement with no
    // food operation at all is stated as the deficit it is.
    internal static class CASettlementSubsistence
    {
        internal static void Materialize(Map map, CellRect rect,
            CARegionalSettlementRecord record, int seed)
        {
            if (map == null || record == null) return;
            try
            {
                Rand.PushState(seed ^ 0x50B515);
                try { Run(map, rect, record); }
                finally { Rand.PopState(); }
            }
            catch (Exception e)
            {
                Log.Error("[CA][Settlement][Subsistence] " + record.name
                    + " skipped: " + e);
            }
        }

        private static void Run(Map map, CellRect rect,
            CARegionalSettlementRecord record)
        {
            List<CASettlementProgramEntry> agriculture =
                (record.settlementProgram?.entries
                    ?? new List<CASettlementProgramEntry>())
                .Where(entry => entry != null && entry.programKey
                    == CASettlementProgramCausalKernel.Agriculture)
                .ToList();
            bool forages = (record.settlementProgram?.entries
                    ?? new List<CASettlementProgramEntry>())
                .Any(entry => entry != null && entry.programKey
                    == CASettlementProgramCausalKernel.Gathering);
            if (agriculture.Count == 0)
            {
                Log.Message("[CA][Settlement][Subsistence] " + record.name
                    + ": no agriculture operation; "
                    + (forages
                        ? "the settlement forages and its edge stays wild"
                        : "no represented food production on this ground -- "
                            + "a stated deficit, not an omission"));
                return;
            }

            int rank = CATechnologicalKnowledgeRuntime.CanonicalRank(
                record.faction, CATechnologyDomains.Agriculture,
                CATechnologyCompetencies.Operate);
            ThingDef crop = CropFor(map, rank);
            if (crop == null)
            {
                Log.Message("[CA][Settlement][Subsistence] " + record.name
                    + ": no viable crop for this ground/season; fields "
                    + "deferred to the works pulse");
                return;
            }

            // One worked plot per agriculture operation, sized by the
            // operation's own extent and worked more confidently at higher
            // knowledge. Plots sit on fertile open ground adjacent to the
            // built fabric -- the fields ring the town where the ground
            // allows, not where a mask says.
            int plots = 0, sown = 0;
            var operators = new List<string>();
            foreach (CASettlementProgramEntry entry in agriculture)
            {
                int count = Math.Max(1, entry.count);
                for (int i = 0; i < count && plots < 6; i++)
                {
                    int want = Mathf.Clamp(
                        30 + Math.Max(1, entry.extent) * 25
                        + rank * 10, 30, 140);
                    int grown = GrowPlot(map, rect, crop, want,
                        rank >= 3 ? 0.9f : 0.75f);
                    if (grown <= 0) continue;
                    plots++;
                    sown += grown;
                    operators.Add((entry.operatorIdentity
                        ?? "unattributed") + ":" + grown);
                }
            }
            Log.Message("[CA][Settlement][Subsistence] " + record.name
                + ": " + plots + " field plot(s), " + sown + " cells sown ("
                + crop.defName + ", agri rank " + rank + "); operators ["
                + string.Join(", ", operators) + "]"
                + (plots == 0 ? "; no arable open ground beside the fabric "
                    + "-- a real siting deficit" : ""));
        }

        // A worked field is an irregular contiguous patch grown from a
        // fertile anchor near the settlement edge, never a stamped square.
        private static int GrowPlot(Map map, CellRect rect, ThingDef crop,
            int want, float density)
        {
            IntVec3 anchor = IntVec3.Invalid;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                IntVec3 c = rect.ExpandedBy(6).ClipInsideMap(map)
                    .RandomCell;
                if (Plantable(map, c, crop)) { anchor = c; break; }
            }
            if (!anchor.IsValid) return 0;

            var frontier = new List<IntVec3> { anchor };
            var chosen = new HashSet<IntVec3>();
            while (frontier.Count > 0 && chosen.Count < want)
            {
                IntVec3 at = frontier[Rand.Range(0, frontier.Count)];
                frontier.Remove(at);
                if (chosen.Contains(at) || !Plantable(map, at, crop))
                    continue;
                chosen.Add(at);
                foreach (IntVec3 step in GenAdj.CardinalDirections)
                {
                    IntVec3 next = at + step;
                    // ragged edges: growth occasionally declines a side
                    if (Rand.Chance(0.12f)) continue;
                    if (!chosen.Contains(next)) frontier.Add(next);
                }
            }
            if (chosen.Count < 12) return 0;

            int grown = 0;
            foreach (IntVec3 c in chosen)
            {
                if (!Rand.Chance(density)) continue;
                foreach (Thing standing in c.GetThingList(map).ToList())
                    if (standing is Plant)
                    { try { standing.Destroy(DestroyMode.Vanish); }
                        catch { } }
                try
                {
                    var plant = (Plant)GenSpawn.Spawn(crop, c, map);
                    // established fields are mid-cycle, not freshly sown
                    plant.Growth = Rand.Range(0.25f, 0.85f);
                    grown++;
                }
                catch { }
            }
            return grown;
        }

        private static bool Plantable(Map map, IntVec3 c, ThingDef crop)
        {
            if (!c.InBounds(map) || c.Fogged(map)) return false;
            TerrainDef terrain = c.GetTerrain(map);
            if (terrain == null || terrain.IsWater
                || terrain.fertility < 0.5f) return false;
            if (map.terrainGrid.FoundationAt(c) != null) return false;
            if (c.GetEdifice(map) != null) return false;
            if (c.Roofed(map)) return false;
            foreach (Thing thing in c.GetThingList(map))
                if (thing.def.category == ThingCategory.Building
                    || thing.def.category == ThingCategory.Item)
                    return false;
            return true;
        }

        // The crop is what this society can actually grow here: knowledge
        // widens the roster, the map's own outdoor temperature decides
        // whether anything stands in the ground at the starting date.
        private static ThingDef CropFor(Map map, int rank)
        {
            if (map.mapTemperature.OutdoorTemp < -8f) return null;
            ThingDef corn = DefDatabase<ThingDef>
                .GetNamedSilentFail("Plant_Corn");
            ThingDef potato = DefDatabase<ThingDef>
                .GetNamedSilentFail("Plant_Potato");
            ThingDef rice = DefDatabase<ThingDef>
                .GetNamedSilentFail("Plant_Rice");
            if (rank >= 3 && corn != null && Rand.Chance(0.5f)) return corn;
            if (rank >= 2 && rice != null && Rand.Chance(0.35f)) return rice;
            return potato ?? rice ?? corn;
        }
    }
}
