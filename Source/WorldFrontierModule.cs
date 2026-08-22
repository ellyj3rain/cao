using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // THE FRONTIER EXISTS EVERYWHERE, NOT ONLY WHERE THE PLAYER STANDS.
    // The frontier tendencies are authored about the world - how much of
    // each region's suitable empty land carries a holding - but holdings
    // were only ever built inside a regional plan, and plans only exist
    // for regions the player has entered or looked at. Every other region
    // in the world carried none, so an open-frontier world and a
    // heartlands world were identical everywhere the player had not been,
    // which is almost all of the map.
    //
    // A holding is a deterministic consequence of facts the world already
    // holds: the region's members, their land, and the authored
    // tendencies. So it needs no new persisted state - it is derived on
    // demand from the same kernel calls the plan-side realization uses,
    // and cached per region so the globe is not recomputing it per frame.
    // When a region does materialize, its plan builds the real holdings
    // and this derivation stands down for that ground.
    //
    // The two are close but NOT identical, and the difference is stated
    // rather than hidden: the plan additionally screens each candidate
    // through habitat viability and a regional supporter, which needs
    // full environment facts per tile. Paying that across every region
    // in the world - on every daily revision - to decorate a globe is
    // not worth it, so this screens on land capacity alone. A region may
    // therefore show a holding here that its realization declines to
    // build on that exact tile. Counts and character hold; the specific
    // ground is settled when the region is actually realized.
    internal readonly struct CAWorldHolding
    {
        internal readonly int TileId;
        internal readonly int Residents;
        internal readonly int MaterialLevel;
        internal readonly int Form;

        internal CAWorldHolding(int tileId, int residents,
            int materialLevel, int form)
        {
            TileId = tileId;
            Residents = residents;
            MaterialLevel = materialLevel;
            Form = form;
        }
    }

    internal static class CAWorldFrontier
    {
        private static readonly Dictionary<string, List<CAWorldHolding>>
            Cache = new Dictionary<string, List<CAWorldHolding>>();
        private static int cacheRevision = -1;
        private static int cacheSeed;

        internal static void Invalidate()
        {
            Cache.Clear();
            cacheRevision = -1;
            visible = null;
            visibleRevision = -1;
        }

        // The holdings a partition region carries, derived from its own
        // ground. Empty for single-area regions, which are addresses
        // rather than country.
        internal static List<CAWorldHolding> For(
            CARegionalTopologyRecord record)
        {
            var empty = new List<CAWorldHolding>();
            CARegionalWorldComponent world =
                CARegionalWorldComponent.Current;
            if (world == null || record?.memberTileIds == null
                || record.memberTileIds.Count < 2) return empty;

            int seed = Verse.Find.World?.info?.Seed ?? 0;
            if (cacheRevision != world.WorldStateRevision
                || cacheSeed != seed)
            {
                Cache.Clear();
                cacheRevision = world.WorldStateRevision;
                cacheSeed = seed;
            }
            if (Cache.TryGetValue(record.regionId,
                    out List<CAWorldHolding> cached))
                return cached;

            var built = new List<CAWorldHolding>();
            try
            {
                CARegionalWorldPolicy policy = world.WorldPolicy;
                // A settlement's ground is its own; the frontier fills
                // what is left, exactly as the plan-side realization
                // treats occupied members.
                var occupied = new HashSet<int>(record.memberTileIds
                    .Where(id => world.WorldSettlementStateAt(id) != null));
                List<int> suitable = record.memberTileIds
                    .Where(id => !occupied.Contains(id)
                        && LandCapacityOf(id) > 0)
                    .OrderBy(id => CAWorldTendencyCausalKernel.Unit(seed,
                        id, 2718283))
                    .ThenBy(id => id).ToList();
                int count = CAWorldTendencyCausalKernel
                    .FrontierHoldingCount(suitable.Count,
                        policy.frontierHoldingFrequency);
                for (int i = 0; i < count && i < suitable.Count; i++)
                {
                    int land = LandCapacityOf(suitable[i]);
                    int residents = CAWorldTendencyCausalKernel
                        .FrontierResidentCount(land,
                            policy.frontierHoldingSize);
                    int material = CAWorldTendencyCausalKernel
                        .FrontierMaterialLevel(land,
                            policy.frontierHoldingSize);
                    built.Add(new CAWorldHolding(suitable[i], residents,
                        material,
                        CAWorldTendencyCausalKernel.FrontierForm(
                            residents, material)));
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[CA][WorldFrontier] could not derive "
                    + "holdings for " + record.regionId + ": "
                    + ex.Message);
                built.Clear();
            }
            Cache[record.regionId] = built;
            return built;
        }

        // Every unrealized region's frontier, flattened once per world
        // change. The overlay runs each frame; walking the whole
        // partition there would pay for the entire world every frame to
        // draw a handful of marks.
        private static List<(CAWorldHolding Holding, string Region)>
            visible;
        private static int visibleRevision = -1;

        internal static IReadOnlyList<(CAWorldHolding Holding,
            string Region)> Visible()
        {
            CARegionalWorldComponent world =
                CARegionalWorldComponent.Current;
            if (world?.Topology == null)
                return new List<(CAWorldHolding, string)>();
            if (visible != null
                && visibleRevision == world.WorldStateRevision)
                return visible;
            visibleRevision = world.WorldStateRevision;
            visible = new List<(CAWorldHolding, string)>();
            IReadOnlyList<CARegionalTopologyRecord> topology =
                world.Topology;
            for (int i = 0; i < topology.Count; i++)
            {
                CARegionalTopologyRecord record = topology[i];
                if (record?.memberTileIds == null
                    || record.memberTileIds.Count < 2) continue;
                // Realized ground draws its own real holdings.
                PlanetTile root = CARegionalPlanUtility.SurfaceTile(
                    record.rootTileId);
                if (root.Valid
                    && CARegionalGeography.RegionAt(root) != null)
                    continue;
                string name = CARegionalGeography.TopologyName(record);
                foreach (CAWorldHolding holding in For(record))
                    visible.Add((holding, name));
            }
            return visible;
        }

        private static int LandCapacityOf(int tileId)
        {
            return CARegionalWorldSettlements.LandCapacityOf(
                CARegionalPlanUtility.SurfaceTile(tileId));
        }
    }
}
