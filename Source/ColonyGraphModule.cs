using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // The player colony uses the same physical layout as regional settlements.
    // Rebuilding follows construction changes so attackers read current gates,
    // paths, and defenses.
    public class CAColonyGraphMapComponent : MapComponent
    {
        private CASettlementLayout layout;
        private int builtTick = -99999;
        private readonly List<IntVec3> defences = new List<IntVec3>();
        private bool dirty = true;

        public CAColonyGraphMapComponent(Map map) : base(map) { }

        // The graph is ground truth for the simulation and is derived
        // from what the player has actually built. Building,
        // demolishing, blocking or repurposing marks it stale at once;
        // the slow tick is only a backstop.
        public CASettlementLayout Layout
        {
            get
            {
                if (dirty || layout == null
                    || Find.TickManager.TicksGame - builtTick > 15000)
                    Rebuild();
                return layout;
            }
        }

        public void MarkDirty()
        {
            dirty = true;
        }

        public IReadOnlyList<IntVec3> SeenDefences => defences;

        private void Rebuild()
        {
            builtTick = Find.TickManager.TicksGame;
            dirty = false;
            var next = new CASettlementLayout
            {
                builtTick = builtTick
            };
            defences.Clear();
            try
            {
                CellRect home = HomeBounds();
                if (home == CellRect.Empty) { layout = next; return; }

                foreach (Building b in map.listerBuildings
                    .allBuildingsColonist)
                {
                    if (b?.def == null) continue;
                    if (b.def.building != null
                        && b.def.building.IsTurret)
                        defences.Add(b.Position);
                }
                // Center the colony on its buildings, not the painted home area.
                next.core = ColonistCentre() ??
                    (home.CenterCell.InBounds(map)
                        ? home.CenterCell : map.Center);
                // Entrances and streets use the shared settlement derivation.
                CABuiltMass mass =
                    CAGraphDerivation.Fill(next, map, home, true);
                if (mass != null && mass.centroid.IsValid
                    && mass.centroid.InBounds(map))
                    next.core = mass.centroid;
            }
            catch (Exception e)
            {
                Log.Warning("[CA] colony graph failed: " + e.Message);
            }
            layout = next;
        }

        private CellRect HomeBounds()
        {
            try
            {
                var cells = map.areaManager?.Home?.ActiveCells;
                if (cells == null) return CellRect.Empty;
                int minX = int.MaxValue, maxX = int.MinValue;
                int minZ = int.MaxValue, maxZ = int.MinValue;
                bool any = false;
                foreach (IntVec3 c in cells)
                {
                    any = true;
                    if (c.x < minX) minX = c.x;
                    if (c.x > maxX) maxX = c.x;
                    if (c.z < minZ) minZ = c.z;
                    if (c.z > maxZ) maxZ = c.z;
                }
                return any ? CellRect.FromLimits(minX, minZ, maxX, maxZ)
                    : CellRect.Empty;
            }
            catch { return CellRect.Empty; }
        }

        // The middle of the colonists' own structures. Null when they
        // have built nothing at all - a landing party on day one.
        private IntVec3? ColonistCentre()
        {
            try
            {
                var list = map.listerBuildings.allBuildingsColonist;
                if (list == null || list.Count == 0) return null;
                long sx = 0, sz = 0;
                int n = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    Building b = list[i];
                    if (b == null || !b.Spawned) continue;
                    sx += b.Position.x; sz += b.Position.z; n++;
                }
                if (n == 0) return null;
                return new IntVec3((int)(sx / n), 0, (int)(sz / n));
            }
            catch { return null; }
        }
    }
}
