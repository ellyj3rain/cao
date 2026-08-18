using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Module: line interpretation. A painted line is a designator of goals
    // and constraints, not geometry to occupy. The interpreter turns (line
    // kind x terrain x threat picture) into POSITION SOLUTIONS that serve the
    // line's meaning - conventional register: fight the frontage from cover
    // on the friendly side with fields of fire ACROSS the line. Warfare
    // character is contextual, never a mode flag; this solver simply reads
    // the ground (cover in built terrain IS the urban read). Slots painted
    // on the line itself remain only the last-resort fallback for featureless
    // ground.
    public struct CALinePosition
    {
        public IntVec3 Cell;
        public IntVec3 Watch;
        public bool FromCover;
    }

    public static class CALineInterpreter
    {
        private const float FriendlySearchRadius = 8.9f;
        private const float FireProbeDistance = 10f;

        // A fighting position inside harmful gas is not a position. Shared by
        // every CA position solver (interpreter, support taskings, hold
        // displacement) so nobody holds overwatch in a tox cloud.
        public static bool HazardousCell(Map map, IntVec3 cell)
        {
            try
            {
                var gas = map.gasGrid;
                if (gas == null) return false;
                return gas.DensityAt(cell, GasType.ToxGas) > 12
                    || gas.DensityAt(cell, GasType.DeadlifeDust) > 12;
            }
            catch { return false; }
        }

        // Solve COUNT fighting positions serving the painted line. threatHint
        // orients the enemy side (invalid -> away from the home area).
        public static List<CALinePosition> Solve(Map map, CAOverlayKind kind,
            int count, IntVec3 threatHint)
        {
            var result = new List<CALinePosition>();
            var overlay = CAOverlayMapComponent.For(map);
            if (overlay == null || count <= 0) return result;
            List<IntVec3> stations = overlay.SlotsAlong(kind, count);
            if (stations.Count == 0) return result;
            IntVec3 enemyRef = EnemyReference(map, overlay, kind, threatHint);
            var taken = new List<IntVec3>();
            for (int s = 0; s < stations.Count; s++)
            {
                IntVec3 station = stations[s];
                Vector3 toEnemy = (enemyRef - station).ToVector3();
                if (toEnemy.sqrMagnitude < 1f) toEnemy = Vector3.forward;
                toEnemy.Normalize();
                IntVec3 probe = station
                    + IntVec3.FromVector3(toEnemy * FireProbeDistance);
                if (!probe.InBounds(map)) probe = enemyRef;
                CALinePosition best = default(CALinePosition);
                float bestScore = float.MinValue;
                int limit = GenRadial.NumCellsInRadius(FriendlySearchRadius);
                for (int r = 0; r < limit; r++)
                {
                    IntVec3 c = station + GenRadial.RadialPattern[r];
                    if (!c.InBounds(map) || !c.Standable(map)) continue;
                    if (HazardousCell(map, c)) continue;
                    // Friendly side only: never solve a position past the
                    // line toward the enemy.
                    Vector3 offset = (c - station).ToVector3();
                    if (Vector3.Dot(offset, toEnemy) > 1.5f) continue;
                    bool clash = false;
                    for (int t = 0; t < taken.Count; t++)
                        if (c.InHorDistOf(taken[t], 1.9f)) { clash = true; break; }
                    if (clash) continue;
                    if (!GenSight.LineOfSight(c, probe, map, true)) continue;
                    float cover = CoverScore(map, c, probe);
                    float score = cover * 3f
                        - c.DistanceTo(station) * 0.15f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = new CALinePosition
                        {
                            Cell = c,
                            Watch = probe,
                            FromCover = cover > 0.2f
                        };
                    }
                }
                if (bestScore == float.MinValue)
                {
                    // Featureless or blind ground: the station itself is the
                    // ratified fallback.
                    best = new CALinePosition
                    { Cell = station, Watch = probe, FromCover = false };
                }
                taken.Add(best.Cell);
                result.Add(best);
            }
            return result;
        }

        // Cover the position enjoys against fire from the probe direction -
        // vanilla's own leaning/cover arithmetic reads walls, rocks, and
        // sandbags alike, which is what makes built ground solve differently
        // from open steppe without any mode flag.
        private static float CoverScore(Map map, IntVec3 position, IntVec3 from)
        {
            try
            {
                return CoverUtility.CalculateOverallBlockChance(
                    position, from, map);
            }
            catch { return 0f; }
        }

        private static IntVec3 EnemyReference(Map map,
            CAOverlayMapComponent overlay, CAOverlayKind kind,
            IntVec3 threatHint)
        {
            if (threatHint.IsValid) return threatHint;
            // No known threats: the enemy side is away from home ground.
            var lineCells = overlay.CellsOf(kind);
            IntVec3 lineCentroid = Centroid(lineCells);
            var home = map.areaManager?.Home;
            if (home != null && home.TrueCount > 0)
            {
                var homeCells = new List<IntVec3>(home.ActiveCells);
                IntVec3 homeCentroid = Centroid(homeCells);
                IntVec3 away = lineCentroid + (lineCentroid - homeCentroid);
                if (away.InBounds(map) && away != lineCentroid) return away;
                Vector3 dir = (lineCentroid - homeCentroid).ToVector3();
                if (dir.sqrMagnitude >= 1f)
                {
                    dir.Normalize();
                    IntVec3 stepped = lineCentroid
                        + IntVec3.FromVector3(dir * 20f);
                    if (stepped.InBounds(map)) return stepped;
                }
            }
            IntVec3 center = map.Center;
            return center != lineCentroid ? center
                : lineCentroid + IntVec3.North;
        }

        private static IntVec3 Centroid(List<IntVec3> cells)
        {
            if (cells == null || cells.Count == 0) return IntVec3.Invalid;
            long x = 0, z = 0;
            for (int i = 0; i < cells.Count; i++)
            { x += cells[i].x; z += cells[i].z; }
            return new IntVec3((int)(x / cells.Count), 0,
                (int)(z / cells.Count));
        }
    }
}
