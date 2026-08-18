using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    internal readonly struct CASpatialCombatMemoryDiagnosticState
    {
        public readonly int KnownCellCount;
        public readonly int OldestObservationTick;
        public readonly int NewestObservationTick;

        public CASpatialCombatMemoryDiagnosticState(int knownCellCount,
            int oldestObservationTick, int newestObservationTick)
        {
            KnownCellCount = knownCellCount;
            OldestObservationTick = oldestObservationTick;
            NewestObservationTick = newestObservationTick;
        }
    }

    // Session-local memory of terrain and structure a pawn has physically occupied
    // or recently seen. Combat consumers may use the current map to execute a path,
    // but a route around a wall is eligible only when the actor has actually learned
    // those cells. This keeps stable geometry distinct from live hidden-pawn state.
    public class CASpatialCombatMemoryMapComponent : MapComponent
    {
        private readonly Dictionary<int, Dictionary<int, int>> observedCells =
            new Dictionary<int, Dictionary<int, int>>();

        private const float ObservationRadius = 18f;
        private const int MemoryTicks = 60000;
        private const int MaxCellsPerPawn = 4096;

        public CASpatialCombatMemoryMapComponent(Map map) : base(map) { }

        internal static CASpatialCombatMemoryMapComponent For(Map map)
        {
            return map?.GetComponent<CASpatialCombatMemoryMapComponent>();
        }

        internal static bool KnowsCell(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || pawn.Map == null || !pawn.Spawned
                || !cell.IsValid || !cell.InBounds(pawn.Map)) return false;
            if (cell == pawn.Position || pawn.Position.InHorDistOf(cell,
                    ObservationRadius)
                && GenSight.LineOfSight(pawn.Position, cell, pawn.Map, true))
                return true;
            CASpatialCombatMemoryMapComponent memory = For(pawn.Map);
            return memory != null && memory.WasRecentlyObserved(pawn, cell);
        }

        internal void ObserveNow(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map != map
                || pawn.Dead) return;
            int now = Find.TickManager.TicksGame;
            Dictionary<int, int> cells;
            if (!observedCells.TryGetValue(pawn.thingIDNumber, out cells))
            {
                cells = new Dictionary<int, int>();
                observedCells[pawn.thingIDNumber] = cells;
            }
            cells[map.cellIndices.CellToIndex(pawn.Position)] = now;
            int limit = GenRadial.NumCellsInRadius(ObservationRadius);
            for (int i = 1; i < limit; i++)
            {
                IntVec3 cell = pawn.Position + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map)
                    || !GenSight.LineOfSight(pawn.Position, cell, map, true))
                    continue;
                cells[map.cellIndices.CellToIndex(cell)] = now;
            }
            if (cells.Count > MaxCellsPerPawn) PrunePawn(cells, now, true);
        }

        internal bool TryGetDiagnosticState(Pawn pawn,
            out CASpatialCombatMemoryDiagnosticState state)
        {
            state = default(CASpatialCombatMemoryDiagnosticState);
            Dictionary<int, int> cells;
            if (pawn == null || !observedCells.TryGetValue(
                    pawn.thingIDNumber, out cells) || cells.Count == 0)
                return false;
            int oldest = int.MaxValue;
            int newest = int.MinValue;
            foreach (int tick in cells.Values)
            {
                if (tick < oldest) oldest = tick;
                if (tick > newest) newest = tick;
            }
            state = new CASpatialCombatMemoryDiagnosticState(cells.Count,
                oldest, newest);
            return true;
        }

        private bool WasRecentlyObserved(Pawn pawn, IntVec3 cell)
        {
            Dictionary<int, int> cells;
            int tick;
            return observedCells.TryGetValue(pawn.thingIDNumber, out cells)
                && cells.TryGetValue(map.cellIndices.CellToIndex(cell), out tick)
                && Find.TickManager.TicksGame - tick <= MemoryTicks;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            int now = Find.TickManager.TicksGame;
            if (now % 60 == 0)
            {
                IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
                KnowledgeMapComponent knowledge = KnowledgeMapComponent.For(map);
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn pawn = pawns[i];
                    if (pawn?.RaceProps == null || !pawn.RaceProps.Humanlike
                        || !pawn.Awake() || pawn.InMentalState
                        || !pawn.Drafted && (knowledge == null
                            || !knowledge.KnowsAnyThreat(pawn))) continue;
                    ObserveNow(pawn);
                }
            }
            if (now % 600 != 0) return;

            var live = new HashSet<int>();
            IReadOnlyList<Pawn> spawned = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null && !spawned[i].Dead)
                    live.Add(spawned[i].thingIDNumber);
            var owners = new List<int>(observedCells.Keys);
            for (int i = 0; i < owners.Count; i++)
            {
                Dictionary<int, int> cells;
                if (!live.Contains(owners[i]))
                {
                    observedCells.Remove(owners[i]);
                    continue;
                }
                if (observedCells.TryGetValue(owners[i], out cells))
                    PrunePawn(cells, now, false);
            }
        }

        private static void PrunePawn(Dictionary<int, int> cells, int now,
            bool enforceCapacity)
        {
            if (cells == null || cells.Count == 0) return;
            var stale = new List<int>();
            foreach (KeyValuePair<int, int> entry in cells)
                if (now - entry.Value > MemoryTicks) stale.Add(entry.Key);
            for (int i = 0; i < stale.Count; i++) cells.Remove(stale[i]);
            if (!enforceCapacity || cells.Count <= MaxCellsPerPawn) return;

            var ordered = new List<KeyValuePair<int, int>>(cells);
            ordered.Sort((first, second) => first.Value.CompareTo(second.Value));
            int remove = cells.Count - MaxCellsPerPawn;
            for (int i = 0; i < remove; i++) cells.Remove(ordered[i].Key);
        }
    }
}
