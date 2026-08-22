using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // ONE PERSISTENT TOPOLOGY RECORD PER REGION. The record is the durable
    // identity layer beneath realization: minted once when the world's
    // partition is built, scribed with the world, and never re-derived from
    // an algorithm at load. A CARegionalPlan that materializes a topology
    // region inherits the record's identity and member set; the record
    // remains the world-facing membership truth for land no map has touched.
    public sealed class CARegionalTopologyRecord : IExposable
    {
        public string regionId;
        public int rootTileId = -1;
        public List<int> memberTileIds = new List<int>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref regionId, "regionId");
            Scribe_Values.Look(ref rootTileId, "rootTileId", -1);
            Scribe_Collections.Look(ref memberTileIds, "memberTileIds",
                LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit
                && memberTileIds == null)
                memberTileIds = new List<int>();
        }

        internal bool Multi
        {
            get { return memberTileIds != null && memberTileIds.Count > 1; }
        }
    }

    internal static class CARegionalTopologyBuilder
    {
        // The identity is unique by construction: no two regions share a
        // root tile, and the id embeds the root. The hash suffix folds the
        // world seed so ids differ between worlds sharing tile numbering.
        internal static string MintRegionId(int worldSeed, int rootTileId)
        {
            unchecked
            {
                uint state = 2166136261u;
                state = (state ^ (uint)worldSeed) * 16777619u;
                state = (state ^ (uint)rootTileId) * 16777619u;
                return "CA-RT-" + rootTileId + "-"
                    + state.ToString("X8");
            }
        }

        // Build the complete surface partition. preAssigned carries every
        // tile already owned by a registered regional plan so a
        // mid-campaign migration cannot double-claim realized ground.
        internal static List<CARegionalTopologyRecord> Build(World world,
            CARegionalWorldPolicy policy, ISet<int> preAssigned,
            out string receipt)
        {
            PlanetLayer surface = world.grid.Surface;
            int tileCount = surface.TilesCount;
            var eligible = new List<int>(tileCount / 2);
            for (int id = 0; id < tileCount; id++)
            {
                var tile = new PlanetTile(id, surface);
                if (!CARegionalGeometry.IsBlocked(tile)) eligible.Add(id);
            }

            // Eligible-only adjacency, cached once: the kernel walks these
            // lists many times during growth.
            var eligibleSet = new HashSet<int>(eligible);
            var adjacency = new Dictionary<int, int[]>(eligible.Count);
            var scratch = new List<PlanetTile>(8);
            foreach (int id in eligible)
            {
                scratch.Clear();
                surface.GetTileNeighbors(new PlanetTile(id, surface),
                    scratch);
                adjacency[id] = scratch
                    .Where(tile => tile.Valid
                        && eligibleSet.Contains(tile.tileId))
                    .Select(tile => tile.tileId).OrderBy(value => value)
                    .ToArray();
            }
            IReadOnlyList<int> NeighborsOf(int id)
            {
                return adjacency.TryGetValue(id, out int[] found)
                    ? found : Array.Empty<int>();
            }
            (float x, float y, float z) PositionOf(int id)
            {
                UnityEngine.Vector3 center = surface.GetTileCenter(
                    new PlanetTile(id, surface));
                return (center.x, center.y, center.z);
            }

            int seed = world.info.Seed;
            float share = policy.ResolveStitchedRegionFrequency();
            List<CARegionalTopologyKernel.RegionSeed> partition =
                CARegionalTopologyKernel.Partition(seed, eligible,
                    NeighborsOf, PositionOf, share,
                    policy.stitchedRegionSizeMin,
                    policy.stitchedRegionSizeMax, preAssigned);

            var records = partition.Select(region =>
                new CARegionalTopologyRecord
                {
                    regionId = MintRegionId(seed, region.RootTileId),
                    rootTileId = region.RootTileId,
                    memberTileIds = region.MemberTileIds
                }).ToList();

            long multiLand = records.Where(record => record.Multi)
                .Sum(record => (long)record.memberTileIds.Count);
            long land = records.Sum(record =>
                (long)record.memberTileIds.Count);
            receipt = "[CA][Topology] partitioned " + land
                + " eligible surface tiles (of " + tileCount
                + ") into " + records.Count + " regions; "
                + records.Count(record => record.Multi)
                + " joined regions carry " + multiLand
                + " tiles (realized land share "
                + (land == 0 ? 0f : (float)multiLand / land).ToString("F2")
                + " against rolled " + share.ToString("F2")
                + "); span band " + policy.stitchedRegionSizeMin + ".."
                + policy.stitchedRegionSizeMax
                + (preAssigned != null && preAssigned.Count > 0
                    ? "; " + preAssigned.Count
                        + " tiles pre-owned by registered regions"
                    : "");
            return records;
        }
    }

    // The partition is built by world generation itself, in the native
    // world-gen step sequence: after geography exists (Terrain 0, Tiles 5,
    // Lakes 150, Rivers 200) and before Factions (500), so settlement
    // placement can consume regional structure. Loading an existing save
    // takes the component migration path instead; this step only serves
    // fresh generation.
    public sealed class WorldGenStep_CARegionalTopology : WorldGenStep
    {
        public override int SeedPart
        {
            get { return 1187302547; }
        }

        public override void GenerateFresh(string seed, PlanetLayer layer)
        {
            if (layer != Verse.Find.WorldGrid.Surface) return;
            CARegionalWorldComponent component =
                Verse.Find.World?.GetComponent<CARegionalWorldComponent>();
            if (component == null)
            {
                Log.Warning("[CA][Topology] world component unavailable "
                    + "during world generation; the partition will be "
                    + "built by the post-load migration instead");
                return;
            }
            // A WorldGenStep that throws aborts the player's world
            // generation. Nothing in CAO is worth that: without a
            // partition the mod degrades to plain RimWorld, which is a
            // recoverable Sunday, whereas a failed generation is not.
            // Logged as an error rather than a warning because a world
            // that reached here and got no partition is a defect, and
            // the log is the only place the first run can say so.
            try
            {
                component.EnsureTopology("world generation");
            }
            catch (Exception ex)
            {
                Log.Error("[CA][Topology] the partition could not be "
                    + "built during world generation; this world has "
                    + "none and CAO's regional layer will be inert on "
                    + "it: " + ex);
            }
        }

        public override void GenerateWithoutWorldData(string seed,
            PlanetLayer layer)
        {
            // Loaded worlds carry their persisted partition; the component
            // migration path covers pre-topology saves.
        }
    }
}
