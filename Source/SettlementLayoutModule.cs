using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Machine-readable layout of a materialized settlement. Gates, paths,
    // core, and facility locations are derived from the generated map and
    // saved on the settlement record. Envoys, gatherings, trade, patrols,
    // and missions read this same layout.
    public sealed class CASettlementLayout : IExposable
    {
        public int schemaVersion = 1;
        public List<IntVec3> gates = new List<IntVec3>();
        // How WIDE each way in is, in cells, parallel to gates. A door
        // is one; a lane between two huts is three; the open side of a
        // camp is a frontage. A force arriving at a doorway files
        // through it; a force arriving at a frontage is already spread.
        public List<int> gateWidths = new List<int>();
        public IntVec3 core = IntVec3.Invalid;
        public List<IntVec3> ways = new List<IntVec3>();
        // facility kind -> where it stands ("hearth", "stores",
        // "infirmary", "workshop", "jail", "dining", "lab", "armory")
        public List<string> facilityKinds = new List<string>();
        public List<IntVec3> facilityCells = new List<IntVec3>();
        // Roads, rooms, utilities, and open approaches used by settlement
        // behavior.
        public List<IntVec3> roads = new List<IntVec3>();
        public List<IntVec3> roomCells = new List<IntVec3>();
        public List<string> roomRoles = new List<string>();
        public List<IntVec3> utilities = new List<IntVec3>();
        public List<string> utilityKinds = new List<string>();
        public List<IntVec3> approaches = new List<IntVec3>();
        public int builtTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Collections.Look(ref gates, "gates", LookMode.Value);
            Scribe_Collections.Look(ref gateWidths, "gateWidths",
                LookMode.Value);
            Scribe_Values.Look(ref core, "core", IntVec3.Invalid);
            Scribe_Collections.Look(ref ways, "ways", LookMode.Value);
            Scribe_Collections.Look(ref facilityKinds,
                "facilityKinds", LookMode.Value);
            Scribe_Collections.Look(ref facilityCells,
                "facilityCells", LookMode.Value);
            Scribe_Collections.Look(ref roads, "roads", LookMode.Value);
            Scribe_Collections.Look(ref roomCells, "roomCells",
                LookMode.Value);
            Scribe_Collections.Look(ref roomRoles, "roomRoles",
                LookMode.Value);
            Scribe_Collections.Look(ref utilities, "utilities",
                LookMode.Value);
            Scribe_Collections.Look(ref utilityKinds, "utilityKinds",
                LookMode.Value);
            Scribe_Collections.Look(ref approaches, "approaches",
                LookMode.Value);
            Scribe_Values.Look(ref builtTick, "builtTick", -1);
        }

        public IntVec3 FacilityCell(string kind)
        {
            for (int i = 0; i < facilityKinds.Count
                && i < facilityCells.Count; i++)
                if (facilityKinds[i] == kind)
                    return facilityCells[i];
            return IntVec3.Invalid;
        }

        // The cell an OUTSIDER approaches first: nearest gate to them,
        // else the core - negotiation happens at the door, not in the
        // bedroom.
        public IntVec3 ApproachCell(IntVec3 from)
        {
            IntVec3 best = core;
            float bestD = float.MaxValue;
            for (int i = 0; i < gates.Count; i++)
            {
                float d = (gates[i] - from).LengthHorizontalSquared;
                if (d < bestD) { bestD = d; best = gates[i]; }
            }
            return best;
        }

        // How wide the way in at this index is. Unknown reads as one -
        // the narrowest thing a way in can be.
        public int GateWidth(int index)
        {
            if (index < 0 || index >= gateWidths.Count) return 1;
            int width = gateWidths[index];
            return width < 1 ? 1 : width;
        }

        public int WidthOfGate(IntVec3 gate)
        {
            for (int i = 0; i < gates.Count; i++)
                if (gates[i] == gate) return GateWidth(i);
            return 1;
        }

        public IntVec3 RoomOfRole(string role)
        {
            for (int i = 0; i < roomRoles.Count && i < roomCells.Count;
                i++)
                if (roomRoles[i] == role) return roomCells[i];
            return IntVec3.Invalid;
        }

        public IntVec3 UtilityOfKind(string kind)
        {
            for (int i = 0; i < utilityKinds.Count
                && i < utilities.Count; i++)
                if (utilityKinds[i] == kind) return utilities[i];
            return IntVec3.Invalid;
        }

        // The cell a RECEIVED guest is brought to: the core - hearth
        // or dining ground beats an empty center.
        public IntVec3 ReceptionCell()
        {
            IntVec3 hearth = FacilityCell("hearth");
            if (hearth.IsValid) return hearth;
            IntVec3 dining = FacilityCell("dining");
            if (dining.IsValid) return dining;
            return core;
        }
    }

    // Rebuilds a settlement layout after construction or damage changes it.
    // Work is staggered so a busy map does not rebuild every layout at once.
    public class CASettlementGraphMapComponent : MapComponent
    {
        private readonly HashSet<string> stale = new HashSet<string>();
        private int nextTick;

        public CASettlementGraphMapComponent(Map map) : base(map) { }

        // Something was built, destroyed or laid at this cell: whoever
        // owns that ground now has an out-of-date description.
        public void MarkDirtyAt(IntVec3 cell)
        {
            try
            {
                var world = CARegionalWorldComponent.Current;
                if (world == null) return;
                foreach (CARegionalSettlementRecord r in
                    world.ForMap(map))
                {
                    if (r?.localRect == null
                        || r.localRect == CellRect.Empty) continue;
                    if (r.localRect.ExpandedBy(6).Contains(cell))
                        stale.Add(r.regionalId + "#" + r.slot);
                }
            }
            catch { }
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame < nextTick) return;
            nextTick = Find.TickManager.TicksGame + 500;
            if (stale.Count == 0) return;
            try
            {
                var world = CARegionalWorldComponent.Current;
                if (world == null) { stale.Clear(); return; }
                // Refresh one settlement per pulse.
                string key = null;
                foreach (string k in stale) { key = k; break; }
                if (key == null) return;
                stale.Remove(key);
                foreach (CARegionalSettlementRecord r in
                    world.ForMap(map))
                {
                    if (r == null) continue;
                    if (r.regionalId + "#" + r.slot != key) continue;
                    if (r.localRect == CellRect.Empty) break;
                    r.layout = CASettlementLayoutBuilder.Build(r, map);
                    break;
                }
            }
            catch (Exception e)
            {
                Log.Warning("[CA] settlement graph refresh: "
                    + e.Message);
            }
        }
    }

    // The connected built area around a settlement center. Nearby structures
    // are joined across streets and yards; disconnected ruins or neighboring
    // buildings are excluded. RimWorld's building listers omit natural rock.
    internal sealed class CABuiltMass
    {
        // Maximum street or yard gap joined into one built area.
        private const int Bridge = 2;

        internal Map map;
        internal readonly HashSet<int> solid = new HashSet<int>();
        internal readonly HashSet<int> cells = new HashSet<int>();
        internal IntVec3 centroid = IntVec3.Invalid;

        internal int Count => cells.Count;
        internal bool Any => cells.Count > 0;

        internal bool At(IntVec3 c)
        {
            return c.InBounds(map)
                && cells.Contains(map.cellIndices.CellToIndex(c));
        }

        internal bool IsStructure(IntVec3 c)
        {
            return c.InBounds(map)
                && solid.Contains(map.cellIndices.CellToIndex(c));
        }

        // The seed selects the connected built area belonging to this
        // settlement.
        internal static CABuiltMass Derive(Map map, CellRect area,
            IntVec3 seed)
        {
            var mass = new CABuiltMass { map = map };
            if (map == null || area == CellRect.Empty) return mass;
            CellRect window = area.ExpandedBy(10).ClipInsideMap(map);
            CellIndices idx = map.cellIndices;

            Gather(mass, map.listerBuildings.allBuildingsColonist,
                window, idx);
            Gather(mass, map.listerBuildings.allBuildingsNonColonist,
                window, idx);
            if (mass.solid.Count == 0) return mass;

            // Join structures across short streets and yards.
            var grown = new HashSet<int>();
            foreach (int i in mass.solid)
            {
                IntVec3 c = idx.IndexToCell(i);
                for (int dx = -Bridge; dx <= Bridge; dx++)
                    for (int dz = -Bridge; dz <= Bridge; dz++)
                    {
                        var n = new IntVec3(c.x + dx, 0, c.z + dz);
                        if (window.Contains(n))
                            grown.Add(idx.CellToIndex(n));
                    }
            }

            PieceAt(mass, grown, idx, seed);
            mass.SetCentroid(idx);
            return mass;
        }

        private static void Gather(CABuiltMass mass, List<Building> list,
            CellRect window, CellIndices idx)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                Building b = list[i];
                if (b?.def == null || !b.Spawned) continue;
                if (!window.Contains(b.Position)) continue;
                foreach (IntVec3 c in b.OccupiedRect())
                    if (window.Contains(c))
                        mass.solid.Add(idx.CellToIndex(c));
            }
        }

        // Prefer the component containing the settlement center. If the center
        // is open ground, choose the nearest component and break ties by size.
        private static void PieceAt(CABuiltMass mass,
            HashSet<int> grown, CellIndices idx, IntVec3 seed)
        {
            var unvisited = new HashSet<int>(grown);
            HashSet<int> best = null;
            float bestScore = float.MinValue;
            var queue = new List<int>();
            while (unvisited.Count > 0)
            {
                int start = 0;
                foreach (int i in unvisited) { start = i; break; }
                var piece = new HashSet<int>();
                queue.Clear();
                queue.Add(start);
                unvisited.Remove(start);
                piece.Add(start);
                for (int q = 0; q < queue.Count; q++)
                {
                    IntVec3 c = idx.IndexToCell(queue[q]);
                    for (int d = 0; d < 4; d++)
                    {
                        IntVec3 n = c + GenAdj.CardinalDirections[d];
                        if (!n.InBounds(mass.map)) continue;
                        int ni = idx.CellToIndex(n);
                        if (!unvisited.Remove(ni)) continue;
                        piece.Add(ni);
                        queue.Add(ni);
                    }
                }
                // holding the heart settles it outright; otherwise the
                // nearest lump wins, and size breaks a tie
                bool holdsSeed = seed.IsValid
                    && piece.Contains(idx.CellToIndex(seed));
                float score;
                if (holdsSeed) score = float.MaxValue;
                else
                {
                    float near = float.MaxValue;
                    foreach (int pi in piece)
                    {
                        IntVec3 pc = idx.IndexToCell(pi);
                        float d = (pc - seed).LengthHorizontalSquared;
                        if (d < near) near = d;
                    }
                    score = -near + piece.Count * 0.001f;
                }
                if (score > bestScore) { bestScore = score; best = piece; }
                if (holdsSeed) break;
            }
            if (best != null)
                foreach (int i in best) mass.cells.Add(i);
        }

        private void SetCentroid(CellIndices idx)
        {
            if (cells.Count == 0) return;
            long sx = 0, sz = 0;
            foreach (int i in cells)
            {
                IntVec3 c = idx.IndexToCell(i);
                sx += c.x; sz += c.z;
            }
            centroid = new IntVec3((int)(sx / cells.Count), 0,
                (int)(sz / cells.Count));
        }
    }

    // Settlements and the player colony use the same layout derivation.
    internal static class CAGraphDerivation
    {
        // Returns the built area so callers without a hearth can use its
        // centroid instead of the planning rectangle.
        internal static CABuiltMass Fill(CASettlementLayout layout,
            Map map, CellRect area, bool colonistOwned)
        {
            CABuiltMass mass = null;
            try
            {
                mass = CABuiltMass.Derive(map, area,
                    layout.core.IsValid ? layout.core : area.CenterCell);
                FillWaysIn(layout, map, mass);
                FillWays(layout, map, mass);
                FillRoads(layout, map, area);
                FillRooms(layout, map, area);
                FillOpenAreas(layout, map, mass);
                FillUtilities(layout, map, area, colonistOwned);
                FillApproaches(layout, map, area, mass);
            }
            catch (Exception e)
            {
                Log.Warning("[CA] graph fill: " + e.Message);
            }
            return mass;
        }

        // Derive entrances from walkable crossings into the built area. Doors
        // are one case; unwalled camps also have entrances. Adjacent crossings
        // are grouped by facing, then ranked by width and approaching roads.
        private static void FillWaysIn(CASettlementLayout layout, Map map,
            CABuiltMass mass)
        {
            if (!mass.Any || !mass.centroid.IsValid) return;
            CellIndices idx = map.cellIndices;

            var edge = new HashSet<int>();
            foreach (int i in mass.cells)
            {
                IntVec3 inner = idx.IndexToCell(i);
                for (int d = 0; d < 4; d++)
                {
                    IntVec3 outer = inner + GenAdj.CardinalDirections[d];
                    if (!outer.InBounds(map) || mass.At(outer)) continue;
                    if (!outer.WalkableByNormal(map)) continue;
                    edge.Add(idx.CellToIndex(outer));
                }
            }
            if (edge.Count == 0) return;

            var crossings = new HashSet<int>();
            foreach (int i in edge)
                if (WalksIn(idx.IndexToCell(i), mass, map))
                    crossings.Add(i);
            AddDoorways(crossings, edge, mass, map, idx);
            if (crossings.Count == 0) return;

            var openings = new List<Opening>();
            var unvisited = new HashSet<int>(crossings);
            var queue = new List<int>();
            while (unvisited.Count > 0)
            {
                int start = 0;
                foreach (int i in unvisited) { start = i; break; }
                unvisited.Remove(start);
                queue.Clear();
                queue.Add(start);
                var run = new List<IntVec3> { idx.IndexToCell(start) };
                for (int q = 0; q < queue.Count; q++)
                {
                    IntVec3 c = idx.IndexToCell(queue[q]);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            var n = new IntVec3(c.x + dx, 0, c.z + dz);
                            if (!n.InBounds(map)) continue;
                            int ni = idx.CellToIndex(n);
                            if (!unvisited.Remove(ni)) continue;
                            run.Add(n);
                            queue.Add(ni);
                        }
                }
                SplitByFacing(run, mass.centroid, openings);
            }
            Merge(openings);
            if (openings.Count == 0) return;

            foreach (Opening o in openings) o.score = Score(o, map, mass);
            openings.Sort((a, b) => b.score.CompareTo(a.score));
            for (int i = 0; i < openings.Count && layout.gates.Count < 8;
                i++)
            {
                Opening o = openings[i];
                if (!o.cell.IsValid) continue;
                layout.gates.Add(o.cell);
                layout.gateWidths.Add(o.width);
            }
        }

        private sealed class Opening
        {
            internal IntVec3 cell;
            internal int width;
            internal float score;
        }

        // Required penetration and maximum test distance for an entrance.
        private const int EnterDepth = 6;
        private const int WalkLimit = 30;

        // Test the direct path and two nearby bearings toward the centroid.
        private static bool WalksIn(IntVec3 from, CABuiltMass mass,
            Map map)
        {
            IntVec3 to = mass.centroid;
            IntVec3 side = new IntVec3(-(to.z - from.z), 0,
                to.x - from.x);
            float len = Mathf.Sqrt(
                Mathf.Max(1, side.LengthHorizontalSquared));
            var nudge = new IntVec3((int)Mathf.Round(side.x / len * 2f),
                0, (int)Mathf.Round(side.z / len * 2f));
            return WalksTo(from, to, mass, map)
                || WalksTo(from, to + nudge, mass, map)
                || WalksTo(from, to - nudge, mass, map);
        }

        private static bool WalksTo(IntVec3 from, IntVec3 to,
            CABuiltMass mass, Map map)
        {
            int dx = to.x - from.x, dz = to.z - from.z;
            int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz));
            if (steps <= 0) return true;
            if (steps > WalkLimit) steps = WalkLimit;
            int inside = 0;
            for (int s = 1; s <= steps; s++)
            {
                var c = new IntVec3(
                    from.x + (int)Mathf.Round(dx * (float)s / steps), 0,
                    from.z + (int)Mathf.Round(dz * (float)s / steps));
                if (!c.InBounds(map)) return false;
                if (!c.WalkableByNormal(map)) return false;
                if (mass.At(c) && ++inside >= EnterDepth) return true;
            }
            // the middle was reached without being stopped
            return inside > 0;
        }

        // A doorway is an entrance only when walking outward clears the full
        // built area; interior room doors do not qualify.
        private static void AddDoorways(HashSet<int> crossings,
            HashSet<int> edge, CABuiltMass mass, Map map, CellIndices idx)
        {
            foreach (int i in mass.cells)
            {
                IntVec3 c = idx.IndexToCell(i);
                if (!(c.GetEdifice(map) is Building_Door)) continue;
                IntVec3 out1 = c - mass.centroid;
                int steps = Mathf.Max(Mathf.Abs(out1.x),
                    Mathf.Abs(out1.z));
                if (steps <= 0) continue;
                float len = Mathf.Sqrt(out1.LengthHorizontalSquared);
                if (len < 1f) continue;
                for (int s = 1; s <= WalkLimit; s++)
                {
                    var n = new IntVec3(
                        c.x + (int)Mathf.Round(out1.x / len * s), 0,
                        c.z + (int)Mathf.Round(out1.z / len * s));
                    if (!n.InBounds(map)) break;
                    if (!n.WalkableByNormal(map)) break;
                    if (mass.At(n)) continue;
                    // The door opens out of the built area.
                    int ni = idx.CellToIndex(n);
                    if (edge.Contains(ni)) crossings.Add(ni);
                    break;
                }
            }
        }

        // Split broad camp edges by facing. Narrow openings stay whole.
        private const int WrapWidth = 12;

        private static void SplitByFacing(List<IntVec3> run,
            IntVec3 centre, List<Opening> into)
        {
            if (run.Count < WrapWidth)
            {
                into.Add(Single(run));
                return;
            }
            var byFacing = new Dictionary<int, List<IntVec3>>();
            foreach (IntVec3 c in run)
            {
                int face = 0;
                if (centre.IsValid)
                {
                    float a = Mathf.Atan2(c.z - centre.z, c.x - centre.x);
                    if (a < 0f) a += Mathf.PI * 2f;
                    face = (int)(a / (Mathf.PI * 2f) * 8f) % 8;
                }
                if (!byFacing.TryGetValue(face, out List<IntVec3> part))
                {
                    part = new List<IntVec3>();
                    byFacing[face] = part;
                }
                part.Add(c);
            }
            foreach (List<IntVec3> part in byFacing.Values)
                if (part.Count > 0) into.Add(Single(part));
        }

        private static Opening Single(List<IntVec3> part)
        {
            long sx = 0, sz = 0;
            foreach (IntVec3 c in part) { sx += c.x; sz += c.z; }
            var mid = new IntVec3((int)(sx / part.Count), 0,
                (int)(sz / part.Count));
            IntVec3 best = part[0];
            float bestD = float.MaxValue;
            foreach (IntVec3 c in part)
            {
                float d = (c - mid).LengthHorizontalSquared;
                if (d < bestD) { bestD = d; best = c; }
            }
            return new Opening { cell = best, width = part.Count };
        }

        // Merge nearby samples of the same physical entrance.
        private static void Merge(List<Opening> openings)
        {
            for (int i = 0; i < openings.Count; i++)
                for (int j = openings.Count - 1; j > i; j--)
                {
                    if (!openings[i].cell.InHorDistOf(openings[j].cell,
                        4f)) continue;
                    openings[i].width += openings[j].width;
                    openings.RemoveAt(j);
                }
        }

        // Rank entrances by approaching made ground and opening width.
        private static float Score(Opening opening, Map map,
            CABuiltMass mass)
        {
            float use = 0f;
            for (int dx = -4; dx <= 4; dx++)
                for (int dz = -4; dz <= 4; dz++)
                {
                    var c = new IntVec3(opening.cell.x + dx, 0,
                        opening.cell.z + dz);
                    if (!c.InBounds(map) || mass.At(c)) continue;
                    if (IsMadeGround(c.GetTerrain(map))) use += 1f;
                }
            return use * 3f + Mathf.Min(opening.width, 12);
        }

        // Streets, paving, roads, and bridges. Interior floors are excluded.
        internal static bool IsMadeGround(TerrainDef t)
        {
            if (t == null) return false;
            string n = t.defName;
            return n == "PackedDirt" || n == "PavedTile"
                || n == "Concrete" || n.StartsWith("Flagstone")
                || n.Contains("Road") || n.Contains("Bridge");
        }

        // Sample made ground inside the built area for patrol and visitor paths.
        private static void FillWays(CASettlementLayout layout, Map map,
            CABuiltMass mass)
        {
            if (!mass.Any) return;
            CellIndices idx = map.cellIndices;
            int stride = 0;
            foreach (int i in mass.cells)
            {
                if (layout.ways.Count >= 120) break;
                if (++stride % 3 != 0) continue;
                IntVec3 c = idx.IndexToCell(i);
                if (!c.InBounds(map) || !c.Standable(map)) continue;
                if (IsMadeGround(c.GetTerrain(map))) layout.ways.Add(c);
            }
        }

        // Made ground inside the settlement and extending from it.
        private static void FillRoads(CASettlementLayout layout, Map map,
            CellRect area)
        {
            int stride = 0;
            foreach (IntVec3 c in area.ExpandedBy(20))
            {
                if (!c.InBounds(map)) continue;
                if (++stride % 2 != 0) continue;
                if (!IsMadeGround(c.GetTerrain(map))) continue;
                if (layout.roads.Count < 200) layout.roads.Add(c);
            }
        }

        // Rooms and what each is for - the engine already names them.
        private static void FillRooms(CASettlementLayout layout, Map map,
            CellRect area)
        {
            var seen = new HashSet<int>();
            foreach (IntVec3 c in area)
            {
                if (!c.InBounds(map)) continue;
                Room room = c.GetRoom(map);
                if (room == null || room.PsychologicallyOutdoors
                    || room.IsDoorway) continue;
                if (!seen.Add(room.ID)) continue;
                if (room.CellCount < 4) continue;
                string role = "room";
                try
                {
                    RoomRoleDef r = room.Role;
                    if (r != null && r.defName != "None")
                        role = r.label ?? r.defName;
                }
                catch { }
                if (layout.roomCells.Count >= 40) break;
                layout.roomCells.Add(room.Cells.FirstOrDefault());
                layout.roomRoles.Add(role);
            }
        }

        // Group outdoor hearths, beds, tables, and workbenches into functional
        // areas so unwalled camps expose the same room roles as built towns.
        private static void FillOpenAreas(CASettlementLayout layout,
            Map map, CABuiltMass mass)
        {
            if (!mass.Any) return;
            var loose = new List<Thing>();
            Collect(loose, map.listerBuildings.allBuildingsColonist, mass,
                map);
            Collect(loose, map.listerBuildings.allBuildingsNonColonist,
                mass, map);
            if (loose.Count == 0) return;

            // Things of one purpose standing near each other are one
            // area. Purpose comes FIRST, so a fire with bedrolls around
            // it is a hearth circle AND a sleeping cluster on the same
            // ground - which is what it is. Lumping them by proximity
            // alone would have named the ground once and lost the other.
            var byPurpose = new Dictionary<string, List<Thing>>();
            foreach (Thing t in loose)
            {
                string p = Purpose(t.def);
                if (p == null) continue;
                if (!byPurpose.TryGetValue(p, out List<Thing> bucket))
                {
                    bucket = new List<Thing>();
                    byPurpose[p] = bucket;
                }
                bucket.Add(t);
            }

            var queue = new List<int>();
            foreach (KeyValuePair<string, List<Thing>> kv in byPurpose)
            {
                List<Thing> bucket = kv.Value;
                var taken = new bool[bucket.Count];
                for (int s = 0; s < bucket.Count; s++)
                {
                    if (taken[s]) continue;
                    if (layout.roomCells.Count >= 40) return;
                    taken[s] = true;
                    queue.Clear();
                    queue.Add(s);
                    var group = new List<Thing> { bucket[s] };
                    for (int q = 0; q < queue.Count; q++)
                    {
                        IntVec3 at = bucket[queue[q]].Position;
                        for (int o = 0; o < bucket.Count; o++)
                        {
                            if (taken[o]) continue;
                            if (!bucket[o].Position.InHorDistOf(at, 6f))
                                continue;
                            taken[o] = true;
                            group.Add(bucket[o]);
                            queue.Add(o);
                        }
                    }
                    IntVec3 cell = Middle(group, map);
                    if (!cell.IsValid) continue;
                    layout.roomCells.Add(cell);
                    layout.roomRoles.Add(kv.Key);
                }
            }
        }

        private static void Collect(List<Thing> into, List<Building> list,
            CABuiltMass mass, Map map)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                Building b = list[i];
                if (b?.def == null || !b.Spawned) continue;
                if (!mass.At(b.Position)) continue;
                if (Purpose(b.def) == null) continue;
                // anything already standing in a room the engine names
                // is described there, not here
                Room room = b.Position.GetRoom(map);
                if (room != null && !room.PsychologicallyOutdoors
                    && !room.IsDoorway && room.ProperRoom) continue;
                into.Add(b);
            }
        }

        // What one piece of furniture is FOR, by the same tests the
        // engine uses to name an enclosed room: bed_countsForBedroom-
        // OrBarracks for sleeping, SurfaceType.Eat for eating,
        // workTableRoomRole for a table that declares its own trade,
        // Building_Storage for stores, and a gather spot for a fire
        // people stand around.
        private static string Purpose(ThingDef def)
        {
            try
            {
                if (def.building != null)
                {
                    if (def.building.bed_humanlike
                        && def.building.bed_countsForBedroomOrBarracks)
                        return "bedroom";
                    if (def.building.workTableRoomRole != null
                        && def.building.workTableRoomRole.defName
                            != "None")
                        return def.building.workTableRoomRole.label
                            ?? def.building.workTableRoomRole.defName;
                }
                if (def.surfaceType == SurfaceType.Eat)
                    return "dining room";
                if (typeof(Building_Storage).IsAssignableFrom(
                    def.thingClass)) return "storeroom";
                if (def.HasComp(typeof(CompGatherSpot)))
                    return "hearth";
            }
            catch { }
            return null;
        }

        private static IntVec3 Middle(List<Thing> group, Map map)
        {
            long sx = 0, sz = 0;
            foreach (Thing t in group)
            {
                sx += t.Position.x; sz += t.Position.z;
            }
            var mid = new IntVec3((int)(sx / group.Count), 0,
                (int)(sz / group.Count));
            if (mid.InBounds(map) && mid.Standable(map)) return mid;
            foreach (Thing t in group)
            {
                foreach (IntVec3 n in GenAdj.CardinalDirections
                    .Select(d => t.Position + d))
                    if (n.InBounds(map) && n.Standable(map)) return n;
            }
            return IntVec3.Invalid;
        }

        // Settlement power, cooling, communications, and defenses.
        private static void FillUtilities(CASettlementLayout layout,
            Map map, CellRect area, bool colonistOwned)
        {
            var list = colonistOwned
                ? map.listerBuildings.allBuildingsColonist
                    .Cast<Building>().ToList()
                : map.listerBuildings.allBuildingsNonColonist
                    .Cast<Building>().ToList();
            foreach (Building b in list)
            {
                if (b?.def == null || !area.ExpandedBy(4)
                    .Contains(b.Position)) continue;
                string kind = UtilityKind(b);
                if (kind == null) continue;
                if (layout.utilities.Count >= 40) break;
                layout.utilities.Add(b.Position);
                layout.utilityKinds.Add(kind);
            }
        }

        private static string UtilityKind(Building b)
        {
            try
            {
                if (b.def.building != null && b.def.building.IsTurret)
                    return "defence";
                if (b.TryGetComp<CompPowerPlant>() != null)
                    return "power";
                if (b.TryGetComp<CompPowerBattery>() != null)
                    return "battery";
                if (b.def.defName.Contains("CommsConsole"))
                    return "comms";
                if (b.def.building != null
                    && b.def.building.isEdifice
                    && b.def.defName.Contains("Vent"))
                    return "vent";
                if (b.def.defName.Contains("Wall")
                    && b.def.defName.Contains("Cooler"))
                    return "cooling";
            }
            catch { }
            return null;
        }

        // Sample open approach ground outward from each entrance, using the
        // built-area centroid rather than the planning rectangle.
        private static void FillApproaches(CASettlementLayout layout,
            Map map, CellRect area, CABuiltMass mass)
        {
            IntVec3 centre = mass != null && mass.centroid.IsValid
                ? mass.centroid : area.CenterCell;
            foreach (IntVec3 gate in layout.gates)
            {
                IntVec3 outward = gate - centre;
                if (outward.LengthHorizontalSquared < 1) continue;
                float len = Mathf.Sqrt(outward.LengthHorizontalSquared);
                for (int step = 6; step <= 24; step += 6)
                {
                    IntVec3 c = gate + new IntVec3(
                        (int)(outward.x / len * step), 0,
                        (int)(outward.z / len * step));
                    if (!c.InBounds(map) || !c.Standable(map)) continue;
                    if (layout.approaches.Count < 60)
                        layout.approaches.Add(c);
                }
            }
        }
    }

    internal static class CASettlementLayoutBuilder
    {
        // Derive the model from the REAL map - works over grown
        // morphology AND vanilla cores alike, so every settlement is
        // legible regardless of which generator raised which part.
        internal static CASettlementLayout Build(
            CARegionalSettlementRecord record, Map map)
        {
            var layout = new CASettlementLayout
            {
                builtTick = Find.TickManager.TicksGame
            };
            try
            {
                CellRect rect = record.localRect;
                if (rect == CellRect.Empty || map == null) return layout;

                // Entrances and paths are derived from the built area below.

                // PROGRAM ASSETS: the saved settlement program is the source
                // of truth for where each materialized requirement stands.
                if (record.seededAssets != null)
                    foreach (string entry in record.seededAssets)
                    {
                        string[] parts = entry.Split(new[] { '|' },
                            StringSplitOptions.None);
                        if (parts.Length < 3) continue;
                        int x, z;
                        if (!int.TryParse(parts[1], out x)
                            || !int.TryParse(parts[2], out z)) continue;
                        string kind = KindOf(parts[0]);
                        if (kind == null) continue;
                        if (layout.facilityKinds.Contains(kind))
                            continue;
                        layout.facilityKinds.Add(kind);
                        layout.facilityCells.Add(new IntVec3(x, 0, z));
                    }

                // Use the hearth to select this settlement's connected area.
                IntVec3 hearth = layout.FacilityCell("hearth");
                if (hearth.IsValid) layout.core = hearth;

                CABuiltMass mass = CAGraphDerivation.Fill(layout, map,
                    rect, false);

                // Without a hearth, use the built-area centroid.
                if (!layout.core.IsValid)
                    layout.core = Heart(mass, rect, map);
            }
            catch (Exception e)
            {
                Log.Warning("[CA] layout derivation failed for "
                    + (record?.name ?? "?") + ": " + e.Message);
            }
            return layout;
        }

        // Settlement center without a hearth: a standable cell near the
        // built-area centroid, then the planning rectangle as fallback.
        internal static IntVec3 Heart(CABuiltMass mass, CellRect rect,
            Map map)
        {
            if (mass != null && mass.centroid.IsValid)
            {
                if (mass.centroid.InBounds(map)
                    && mass.centroid.Standable(map)) return mass.centroid;
                for (int r = 1; r <= 6; r++)
                    foreach (IntVec3 c in GenRadial
                        .RadialCellsAround(mass.centroid, r, false))
                        if (c.InBounds(map) && c.Standable(map)
                            && mass.At(c)) return c;
            }
            if (rect.CenterCell.InBounds(map)
                && rect.CenterCell.Standable(map)) return rect.CenterCell;
            return rect.Cells.FirstOrDefault(c => c.InBounds(map)
                && c.Standable(map));
        }

        private static string KindOf(string defName)
        {
            switch (defName)
            {
                case "Campfire":
                case "FueledStove": return "hearth";
                case "Shelf": return "stores";
                case "Bedroll":
                case "Bed":
                case "HospitalBed": return "infirmary";
                case "CraftingSpot":
                case "FueledSmithy": return "workshop";
                case "Table2x2c": return "dining";
                case "SimpleResearchBench": return "lab";
                default: return null;
            }
        }
    }
}
