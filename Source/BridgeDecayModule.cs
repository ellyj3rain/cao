using Verse;

namespace ColonistAwareness
{
    // Makeshift bridges rot: each cell has a small chance per check to collapse,
    // averaging out to roughly a season and a half of useful life.
    public class BridgeDecayMapComponent : MapComponent
    {
        private const int IntervalTicks = 2500;
        private const float CollapseChancePerCheck = 0.0028f;
        private TerrainDef makeshift;
        private bool resolved;

        public BridgeDecayMapComponent(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % IntervalTicks != 0) return;
            try
            {
                if (!resolved)
                {
                    makeshift = DefDatabase<TerrainDef>.GetNamedSilentFail("CA_MakeshiftBridge");
                    resolved = true;
                }
                if (makeshift == null) return;

                var grid = map.terrainGrid;
                int cells = map.cellIndices.NumGridCells;
                for (int i = 0; i < cells; i++)
                {
                    if (grid.TerrainAt(i) != makeshift) continue;
                    if (Rand.Value >= CollapseChancePerCheck) continue;
                    IntVec3 c = map.cellIndices.IndexToCell(i);
                    grid.RemoveTopLayer(c, false);
                }
            }
            catch { }
        }
    }
}
