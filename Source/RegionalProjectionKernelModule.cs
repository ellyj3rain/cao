using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace ColonistAwareness
{
    // Shared regional projection for map generation and the pre-landing
    // preview. Both consumers use this implementation so the preview and
    // generated terrain share the same geography.
    //
    // Derived from the regional plan and world:
    //   - projection origin, mean, scale and latitude correction
    //   - projected constituent centres
    //   - the world-anchored warp fields
    //   - constituent ownership classification (the warped nearest-source
    //     Voronoi, plus micro-fragment smoothing)
    //   - biome classification
    //   - coast/water classification: the coast scalar, water depth, and
    //     littoral formation
    //   - the per-cell hilliness blend
    //   - constituent frames (per-constituent centroid, area, extent)
    //   - the topological diagnostics both consumers read
    //
    // Map-bound work remains outside this kernel:
    //   - MapGenerator.Elevation / Fertility / Caves, and every write to
    //     them (ApplyTerrainProjection, ApplyProjectedMountains,
    //     ApplyProjectedCaves, ApplyProjectedCaveTerrain)
    //   - the projected Mountain field itself: its arithmetic is
    //     map-independent, but its only consumer is an elevation write, the
    //     population screen has no use for it, and moving it would have
    //     dragged the F-131 fixture receipt across a refactor boundary
    //   - map-grid arrays tied to cellIndices, thingGrid, terrainGrid,
    //     roofGrid, regionAndRoomUpdater
    //   - MapGenUtility.BeachTerrainAt, which resolves a TerrainDef against
    //     a live map's biome and mutators
    //   - native TileMutatorWorker dispatch (worker.Init(map) takes a Map)
    //   - plan lookup: which plan a map belongs to is a
    //     map-bound question, so the caller resolves it and hands the
    //     kernel the answer.
    //
    // The projection derives Map.Center directly from the supplied map size.
    internal sealed class CARegionalProjectionRequest
    {
        // The footprint's geometric anchor. Every projected coordinate is
        // expressed relative to this tile's longitude/latitude.
        internal int BundleRootTileId = -1;

        // The selected footprint's core member tiles. The geographic halo
        // (land and water neighbors that fall inside the frame) is
        // discovered from these, not supplied.
        internal List<int> MemberTileIds = new List<int>();

        // Cells per SOURCE TILE at full resolution - the local scale axis,
        // never the aggregate.
        internal int CellsPerSourceTile;

        // The full-resolution backing map, i.e. what generation will
        // actually allocate. A preview scales this down; it never redefines
        // it.
        internal IntVec3 BackingSize;

        // 1 for generation. Below 1 for the same geography
        // sampled on a coarser grid. Every length constant is computed at
        // full scale and then multiplied by this, and the projection scale
        // is multiplied by it too, so a preview cell maps to the same world
        // point as the full-resolution cell it covers. At exactly 1f every
        // multiplication is the IEEE identity and the arithmetic is
        // bit-identical to the pre-kernel generation path.
        internal float Resolution = 1f;

        // Used only where a source tile has no primary biome, which does
        // not happen in practice. Generation passes map.Biome; a preview
        // passes the bundle root's own biome.
        internal BiomeDef FallbackBiome;

        // The request has no startTileId. Moving the landing within a footprint
        // therefore cannot reroll or rebuild regional geography.

        internal PlanetTile BundleRoot
        {
            get { return CARegionalPlanUtility.SurfaceTile(BundleRootTileId); }
        }

        internal static CARegionalProjectionRequest ForGeneration(
            CARegionalPlan plan, IntVec3 backingSize, BiomeDef fallbackBiome)
        {
            return new CARegionalProjectionRequest
            {
                BundleRootTileId = plan.bundleRootTileId,
                MemberTileIds = plan.memberTileIds?.ToList()
                    ?? new List<int>(),
                CellsPerSourceTile = plan.mapSize,
                BackingSize = backingSize,
                Resolution = 1f,
                FallbackBiome = fallbackBiome
            };
        }

        // A preview of the same candidate. `targetLongestSide` is the
        // longest side, in cells, the preview grid may have; the resolution
        // follows from it so the preview costs a bounded amount however
        // large the real region is.
        internal static CARegionalProjectionRequest ForPreview(
            CARegionalPlan plan, int targetLongestSide)
        {
            IntVec3 backing = plan.BackingMapSize;
            int longest = Math.Max(1, Math.Max(backing.x, backing.z));
            float resolution = Mathf.Clamp(
                targetLongestSide / (float)longest, 0.02f, 1f);
            PlanetTile root = CARegionalPlanUtility.SurfaceTile(
                plan.bundleRootTileId);
            return new CARegionalProjectionRequest
            {
                BundleRootTileId = plan.bundleRootTileId,
                MemberTileIds = plan.memberTileIds?.ToList()
                    ?? new List<int>(),
                CellsPerSourceTile = plan.mapSize,
                BackingSize = backing,
                Resolution = resolution,
                FallbackBiome = root.Valid
                    ? root.Tile?.PrimaryBiome : null
            };
        }
    }

    internal sealed class CARegionalProjectionKernel
    {
        // Regional shape varies at terrain scale, so the expensive Perlin
        // sources are evaluated on a coarse lattice and bilinearly sampled
        // per cell. At 1415x1415 this replaces roughly twelve million
        // Perlin calls with under two hundred thousand without changing the
        // frequency or continuity of the fields.
        internal sealed class CoarseNoiseField
        {
            private readonly float originX;
            private readonly float originZ;
            private readonly float stepX;
            private readonly float stepZ;
            private readonly int width;
            private readonly int height;
            private readonly float[] values;

            internal CoarseNoiseField(IntVec3 size, float originX,
                float originZ, float stepX, float stepZ,
                Func<float, float, float> sampler)
            {
                this.originX = originX;
                this.originZ = originZ;
                this.stepX = Math.Max(0.001f, stepX);
                this.stepZ = Math.Max(0.001f, stepZ);
                width = Mathf.CeilToInt((size.x - 1f - originX)
                    / this.stepX) + 1;
                height = Mathf.CeilToInt((size.z - 1f - originZ)
                    / this.stepZ) + 1;
                values = new float[width * height];
                for (int gx = 0; gx < width; gx++)
                {
                    float x = originX + gx * this.stepX;
                    for (int gz = 0; gz < height; gz++)
                    {
                        float z = originZ + gz * this.stepZ;
                        values[gx * height + gz] = sampler(x, z);
                    }
                }
            }

            internal float Sample(int x, int z)
            {
                float gridX = (x - originX) / stepX;
                float gridZ = (z - originZ) / stepZ;
                int left = Mathf.Clamp(Mathf.FloorToInt(gridX), 0,
                    width - 1);
                int bottom = Mathf.Clamp(Mathf.FloorToInt(gridZ), 0,
                    height - 1);
                int right = Math.Min(width - 1, left + 1);
                int top = Math.Min(height - 1, bottom + 1);
                float tx = gridX - left;
                float tz = gridZ - bottom;
                float low = Mathf.Lerp(values[left * height + bottom],
                    values[right * height + bottom], tx);
                float high = Mathf.Lerp(values[left * height + top],
                    values[right * height + top], tx);
                return Mathf.Lerp(low, high, tz);
            }
        }

        private readonly CARegionalProjectionRequest request;
        private readonly PlanetTile bundleRoot;
        private readonly BiomeDef fallbackBiome;
        private readonly float resolution;

        // The one substitution for map.Center, derived exactly as
        // Verse.Map derives it.
        internal IntVec3 Size { get; private set; }
        internal IntVec3 Center { get; private set; }
        internal CellIndices Indices { get; private set; }

        internal float LocalCells { get; private set; }
        internal Vector2 ProjectionOrigin { get; private set; }
        internal Vector2 ProjectionMean { get; private set; }
        internal float ProjectionCosLatitude { get; private set; }
        internal float ProjectionScale { get; private set; }

        internal List<PlanetTile> Members { get; private set; }
        internal List<IntVec3> MemberCenters { get; private set; }
        internal List<float> MemberHillFactors { get; private set; }
        internal List<PlanetTile> GeographicLandTiles { get; private set; }
        internal List<PlanetTile> BoundaryWaterTiles { get; private set; }
        internal List<IntVec3> BoundaryWaterCenters { get; private set; }

        internal int[] MemberByCell { get; private set; }
        internal int[] NearestLandMemberByCell { get; private set; }
        internal int[] BoundaryWaterByCell { get; private set; }
        internal int[] NearestBoundaryWaterByCell { get; private set; }
        internal BiomeDef[] RawBiomeByCell { get; private set; }
        internal ushort[] WaterDepth { get; private set; }
        internal float[] CoastValueByCell { get; private set; }
        internal byte[] LittoralFormationByCell { get; private set; }
        internal float[] HillFactorByCell { get; private set; }

        internal int LittoralSandCells { get; private set; }
        internal Dictionary<BiomeDef, int> BiomeCellCounts
        {
            get; private set;
        }
        internal int CrossBiomeEdges { get; private set; }
        internal int TripleBiomeJunctionQuads { get; private set; }
        internal int WaterCellsBeforeLittoral { get; private set; }
        internal int MaximumWaterDepthBeforeLittoral { get; private set; }
        internal int ShallowWaterCellsBeforeLittoral { get; private set; }
        internal int DeepWaterCellsBeforeLittoral { get; private set; }
        internal int WaterCells { get; private set; }
        internal int ShorelineCells { get; private set; }
        internal int SelectedCoreLandCells { get; private set; }
        internal int GeographicHaloLandCells { get; private set; }
        internal int LittoralComponents { get; private set; }
        internal int LargestLittoralComponent { get; private set; }
        internal int TinyLittoralComponents { get; private set; }
        internal int BiomeSmoothingReassignments { get; private set; }
        internal int LandSourceCentersFlooded { get; private set; }
        internal int WaterSourceCentersDry { get; private set; }
        internal int LandSourceJoinsChecked { get; private set; }
        internal int SeveredLandSourceJoins { get; private set; }
        internal int AtollCarrierCount { get; private set; }
        internal int AtollLagoonCells { get; private set; }
        internal int AtollRingLandCells { get; private set; }
        internal int AtollChangedCellsBeyondCarrier { get; private set; }
        internal int CoveCarrierCount { get; private set; }
        internal int CoveWaterCells { get; private set; }
        internal int CoveChangedCellsBeyondCarrier { get; private set; }
        internal int ArchipelagoCarrierCount { get; private set; }
        internal int ArchipelagoWaterCells { get; private set; }
        internal int ArchipelagoChangedCellsBeyondCarrier
        {
            get; private set;
        }
        internal string PerformanceSummary { get; private set; }

        // Beach width differs for a CoastalAtoll (native MaxForSand=.53)
        // from the generic coast (.60). This mask keeps that feature-specific
        // terrain contract without exposing a second geography model.
        private bool[] atollFieldByCell;

        // True only when the AUTHORITATIVE selected core contains at least two
        // areas. Members also contains nearby land sampled as a geographic
        // halo; counting that context made an ordinary one-area map look
        // stitched and let regional patches suppress its native generation.
        internal bool Active
        {
            get
            {
                return MemberByCell != null && MemberByCell.Length > 0
                    && request.MemberTileIds != null
                    && request.MemberTileIds.Distinct().Take(2).Count() > 1;
            }
        }

        internal float Resolution
        {
            get { return resolution; }
        }

        internal PlanetTile BundleRoot
        {
            get { return bundleRoot; }
        }

        private CAConstituentFrameSet frames;

        internal CAConstituentFrameSet Frames
        {
            get
            {
                if (frames == null && MemberByCell != null
                    && MemberByCell.Length > 0 && Members != null)
                    frames = new CAConstituentFrameSet(Size, Indices,
                        MemberByCell, Members);
                return frames;
            }
        }

        private CARegionalProjectionKernel(
            CARegionalProjectionRequest request)
        {
            this.request = request;
            bundleRoot = request.BundleRoot;
            fallbackBiome = request.FallbackBiome;
            resolution = Mathf.Clamp(request.Resolution, 0.001f, 1f);
        }

        // The single entry point. Returns a kernel whose `Active` is false
        // when the request describes no projectable geography; callers fall
        // back to their own non-regional behaviour in that case exactly as
        // they did before.
        internal static CARegionalProjectionKernel Build(
            CARegionalProjectionRequest request)
        {
            var kernel = new CARegionalProjectionKernel(request);
            kernel.BuildInternal();
            return kernel;
        }

        private void BuildInternal()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Size = new IntVec3(
                Math.Max(1, Mathf.RoundToInt(request.BackingSize.x
                    * resolution)), 1,
                Math.Max(1, Mathf.RoundToInt(request.BackingSize.z
                    * resolution)));
            // Verse.Map.Center is exactly this expression over Map.Size.
            Center = new IntVec3(Size.x / 2, 0, Size.z / 2);
            Indices = new CellIndices(Size.x, Size.z);
            LocalCells = request.CellsPerSourceTile;

            List<PlanetTile> coreMembers = (request.MemberTileIds
                    ?? new List<int>())
                .Select(CARegionalPlanUtility.SurfaceTile)
                .Where(tile => tile.Valid).ToList();
            if (!bundleRoot.Valid || coreMembers.Count == 0)
            {
                MemberByCell = Array.Empty<int>();
                PerformanceSummary = "no projectable footprint";
                return;
            }

            InitializeProjectionFrame(coreMembers);
            List<PlanetTile> land;
            List<PlanetTile> water;
            CollectProjectionSources(coreMembers, out land, out water);
            GeographicLandTiles = land;
            BoundaryWaterTiles = water;
            // The selected bundle owns the map and its reservations, but the
            // terrain frame must include every sampled land neighbor as well
            // as every sampled water neighbor. Omitting the land halo while
            // retaining the water halo can sever a real peninsula and make
            // the preview fabricate an island.
            Members = GeographicLandTiles.Where(tile =>
                    !CARegionalGeometry.IsBlocked(tile))
                .GroupBy(tile => tile.tileId).Select(group => group.First())
                .ToList();
            if (Members.Count == 0)
                Members = coreMembers.Where(tile =>
                        !CARegionalGeometry.IsBlocked(tile))
                    .GroupBy(tile => tile.tileId)
                    .Select(group => group.First()).ToList();
            if (Members.Count == 0)
            {
                MemberByCell = Array.Empty<int>();
                PerformanceSummary = "no unblocked constituents";
                return;
            }

            MemberCenters = Members.Select(tile => ProjectCenter(tile,
                false)).ToList();
            MemberHillFactors = Members.Select(tile =>
                HillFactor(tile.Tile.hilliness)).ToList();
            BoundaryWaterCenters = BoundaryWaterTiles.Select(tile =>
                ProjectCenter(tile, false)).ToList();

            int cellCount = Indices.NumGridCells;
            MemberByCell = new int[cellCount];
            BoundaryWaterByCell = Enumerable.Repeat(-1, cellCount).ToArray();
            NearestBoundaryWaterByCell = Enumerable.Repeat(-1, cellCount)
                .ToArray();
            CoastValueByCell = Enumerable.Repeat(1f, cellCount).ToArray();
            NearestLandMemberByCell = Enumerable.Repeat(-1, cellCount)
                .ToArray();
            HillFactorByCell = new float[cellCount];

            // Every length below is the full-resolution expression times the
            // resolution, so a preview is the same geography on a coarser
            // grid rather than a differently-shaped one. At resolution 1f
            // the multiplication is the identity.
            float smoothing = Mathf.Max(24f, LocalCells * 0.30f) * resolution;
            float smoothingSquared = smoothing * smoothing;
            // Mathf.Pow rather than a self-multiply, because this is a
            // comparison threshold and the pre-kernel path used Pow: an
            // last-ulp difference here would move a cell's hilliness
            // inclusion and break bit-parity for no gain.
            float hillInfluenceSquared = Mathf.Pow(
                LocalCells * 1.5f * resolution, 2f);
            // One continuous world-anchored warp shapes all source boundaries.
            // The nearest source then owns the cell. This keeps land-biome
            // geography deterministic and local; a globally weighted biome
            // lottery can let a common biome jump across unrelated sources and
            // produces long order-sensitive streaks.
            float warpStrength = Mathf.Max(18f, LocalCells * 0.10f)
                * resolution;
            float coastDenominator = Math.Max(2f, LocalCells * 2f)
                * resolution;
            // The lattice uses world-space spacing expressed in cells, so it
            // scales with the grid. Rounding keeps a preview's lattice as
            // close to the generation lattice in world terms as its own grid
            // can express.
            int noiseSpacing = Math.Max(1, Mathf.RoundToInt(8f * resolution));

            CoarseNoiseField warpXNoise = CreateWorldAnchoredNoiseField(
                noiseSpacing, (x, z) =>
                warpStrength * ((SampleWorldNoise(x, z, 0.0028f,
                    39017) - 0.5f) * 1.55f
                    + (SampleWorldNoise(x, z, 0.009f,
                        70351) - 0.5f) * 0.28f));
            CoarseNoiseField warpZNoise = CreateWorldAnchoredNoiseField(
                noiseSpacing, (x, z) =>
                warpStrength * ((SampleWorldNoise(x, z, 0.0028f,
                    52361) - 0.5f) * 1.55f
                    + (SampleWorldNoise(x, z, 0.009f,
                        91673) - 0.5f) * 0.28f));
            CoarseNoiseField coastBoundaryDisplacement =
                CreateWorldAnchoredNoiseField(noiseSpacing, (x, z) =>
                    (SampleWorldNoise(x, z, 0.006f, 211009) * 2f - 1f)
                        * 30f * resolution
                    + (SampleWorldNoise(x, z, 0.015f, 257063) * 2f - 1f)
                        * 25f * resolution);

            for (int x = 0; x < Size.x; x++)
            {
                for (int z = 0; z < Size.z; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    float warpedX = x + warpXNoise.Sample(x, z);
                    float warpedZ = z + warpZNoise.Sample(x, z);
                    int classifiedMember = 0;
                    float bestMemberDistanceSquared = float.MaxValue;
                    float closestLandDistanceSquared = float.MaxValue;
                    int closestLandMember = -1;
                    float totalWeight = 0f;
                    float weightedHillFactor = 0f;
                    for (int i = 0; i < MemberCenters.Count; i++)
                    {
                        float dx = warpedX - MemberCenters[i].x;
                        float dz = warpedZ - MemberCenters[i].z;
                        float distanceSquared = dx * dx + dz * dz;
                        if (Members[i].Tile.PrimaryBiome?.isWaterBiome != true
                            && distanceSquared < closestLandDistanceSquared)
                        {
                            closestLandDistanceSquared = distanceSquared;
                            closestLandMember = i;
                        }
                        if (distanceSquared < bestMemberDistanceSquared)
                        {
                            bestMemberDistanceSquared = distanceSquared;
                            classifiedMember = i;
                        }

                        if (distanceSquared <= hillInfluenceSquared)
                        {
                            float weight = 1f / (distanceSquared
                                + smoothingSquared);
                            totalWeight += weight;
                            weightedHillFactor += weight
                                * MemberHillFactors[i];
                        }
                    }

                    int cellIndex = Indices.CellToIndex(cell);
                    MemberByCell[cellIndex] = classifiedMember;
                    NearestLandMemberByCell[cellIndex] = closestLandMember;
                    HillFactorByCell[cellIndex] = totalWeight > 0f
                        ? weightedHillFactor / totalWeight
                        : MemberHillFactors[classifiedMember];
                    int closestWater = -1;
                    float closestWaterDistanceSquared = float.MaxValue;
                    float closestLandDistance = Mathf.Sqrt(
                        closestLandDistanceSquared);
                    float coastDisplacement =
                        coastBoundaryDisplacement.Sample(x, z);
                    for (int i = 0; i < BoundaryWaterCenters.Count; i++)
                    {
                        float dx = warpedX - BoundaryWaterCenters[i].x;
                        float dz = warpedZ - BoundaryWaterCenters[i].z;
                        float distance = dx * dx + dz * dz;
                        if (distance >= closestWaterDistanceSquared) continue;
                        closestWaterDistanceSquared = distance;
                        closestWater = i;
                    }
                    if (closestWater >= 0)
                    {
                        BiomeDef waterBiome = BoundaryWaterTiles[closestWater]
                            .Tile.PrimaryBiome;
                        float displacedWaterDistance = Mathf.Sqrt(
                                closestWaterDistanceSquared)
                            + (waterBiome == BiomeDefOf.Ocean
                                ? coastDisplacement : 0f);
                        // This scalar is the single coast contract: <0.4 deep
                        // water, 0.4-0.5 shallow water, 0.5-0.6 native beach.
                        // A two-times-local-scale denominator makes each 0.1
                        // band approximately ten percent of one source tile.
                        float coastValue = Mathf.Clamp01(0.5f
                            + (displacedWaterDistance - closestLandDistance)
                                / coastDenominator);
                        NearestBoundaryWaterByCell[cellIndex] = closestWater;
                        CoastValueByCell[cellIndex] = coastValue;
                        bool projectedWater = coastValue < 0.5f;
                        if (projectedWater)
                        {
                            BoundaryWaterByCell[cellIndex] = closestWater;
                            HillFactorByCell[cellIndex] = 0f;
                        }
                    }
                }
            }
            long classifyMs = timer.ElapsedMilliseconds;
            SmoothMemberMicrofragments();
            ApplyContinuousGeographicFeatures();
            BuildRawBiomeCache();
            long smoothMs = timer.ElapsedMilliseconds - classifyMs;
            BuildWaterDepth();
            long waterMs = timer.ElapsedMilliseconds - classifyMs - smoothMs;
            BuildLittoralTopology();
            long littoralMs = timer.ElapsedMilliseconds - classifyMs
                - smoothMs - waterMs;
            BuildProjectionDiagnostics();
            long diagnosticMs = timer.ElapsedMilliseconds - classifyMs
                - smoothMs - waterMs - littoralMs;
            PerformanceSummary = "classification " + classifyMs
                + " ms; smoothing/cache " + smoothMs + " ms; water depth "
                + waterMs + " ms; littoral " + littoralMs
                + " ms; diagnostics " + diagnosticMs + " ms; total "
                + timer.ElapsedMilliseconds + " ms";
        }

        // ---- projection frame ---------------------------------------------

        private void InitializeProjectionFrame(List<PlanetTile> anchors)
        {
            var coordinates = new List<Vector2>();
            ProjectionOrigin = Verse.Find.WorldGrid.LongLatOf(bundleRoot);
            ProjectionCosLatitude = Mathf.Cos(ProjectionOrigin.y
                * Mathf.Deg2Rad);
            foreach (PlanetTile tile in anchors)
            {
                Vector2 value = Verse.Find.WorldGrid.LongLatOf(tile);
                float longitude = Mathf.DeltaAngle(ProjectionOrigin.x,
                    value.x) * ProjectionCosLatitude;
                coordinates.Add(new Vector2(longitude,
                    value.y - ProjectionOrigin.y));
            }
            float minX = coordinates.Min(value => value.x);
            float maxX = coordinates.Max(value => value.x);
            float minY = coordinates.Min(value => value.y);
            float maxY = coordinates.Max(value => value.y);
            ProjectionMean = new Vector2((minX + maxX) * 0.5f,
                (minY + maxY) * 0.5f);
            // Cells per degree scales with the grid, so a preview cell and
            // the full-resolution cells it covers resolve to the same world
            // point.
            ProjectionScale = CARegionalPlanUtility.ProjectionCellsPerDegree(
                bundleRoot, request.CellsPerSourceTile) * resolution;
        }

        private void CollectProjectionSources(List<PlanetTile> anchors,
            out List<PlanetTile> land, out List<PlanetTile> water)
        {
            var landById = new Dictionary<int, PlanetTile>();
            var waterById = new Dictionary<int, PlanetTile>();
            var visited = new HashSet<int>();
            var queue = new Queue<PlanetTile>();
            foreach (PlanetTile anchor in anchors)
            {
                if (!anchor.Valid || !visited.Add(anchor.tileId)) continue;
                queue.Enqueue(anchor);
            }
            float sampleMargin = LocalCells * 2f * resolution;
            float traversalMargin = sampleMargin + LocalCells * resolution;
            while (queue.Count > 0)
            {
                PlanetTile tile = queue.Dequeue();
                IntVec3 center = ProjectCenter(tile, false);
                bool inSampleRange = center.x >= -sampleMargin
                    && center.x <= Size.x - 1 + sampleMargin
                    && center.z >= -sampleMargin
                    && center.z <= Size.z - 1 + sampleMargin;
                if (inSampleRange)
                {
                    BiomeDef biome = tile.Tile.PrimaryBiome;
                    if (biome?.isWaterBiome == true)
                        waterById[tile.tileId] = tile;
                    else if (biome != null && !biome.impassable)
                        landById[tile.tileId] = tile;
                }
                bool traverse = center.x >= -traversalMargin
                    && center.x <= Size.x - 1 + traversalMargin
                    && center.z >= -traversalMargin
                    && center.z <= Size.z - 1 + traversalMargin;
                if (!traverse) continue;
                var neighbors = new List<PlanetTile>();
                tile.Layer.GetTileNeighbors(tile, neighbors);
                foreach (PlanetTile neighbor in neighbors.OrderBy(item =>
                    item.tileId))
                {
                    if (!neighbor.Valid || !visited.Add(neighbor.tileId))
                        continue;
                    queue.Enqueue(neighbor);
                }
            }
            land = landById.Values.OrderBy(tile => tile.tileId).ToList();
            water = waterById.Values.OrderBy(tile => tile.tileId).ToList();
        }

        internal Vector3 WorldNoisePoint(float x, float z)
        {
            Vector2 coordinate = ProjectionMean + new Vector2(
                (x - Center.x) / ProjectionScale,
                (z - Center.z) / ProjectionScale);
            float longitude = ProjectionOrigin.x + coordinate.x
                / Math.Max(0.0001f, ProjectionCosLatitude);
            float latitude = ProjectionOrigin.y + coordinate.y;
            float longitudeRadians = longitude * Mathf.Deg2Rad;
            float latitudeRadians = latitude * Mathf.Deg2Rad;
            float latitudeCosine = Mathf.Cos(latitudeRadians);
            float cellsPerRadian = ProjectionScale * Mathf.Rad2Deg;
            return new Vector3(
                Mathf.Sin(longitudeRadians) * latitudeCosine,
                Mathf.Sin(latitudeRadians),
                -Mathf.Cos(longitudeRadians) * latitudeCosine)
                * cellsPerRadian;
        }

        internal float SampleWorldNoise(float x, float z, float frequency,
            int salt)
        {
            return SampleWorldNoisePoint(WorldNoisePoint(x, z), frequency,
                salt);
        }

        internal static float SampleWorldNoisePoint(Vector3 point,
            float frequency, int salt)
        {
            int seed = Gen.HashCombineInt(Verse.Find.World.info.Seed, salt);
            return (float)Perlin.GetValue(point.x, point.y, point.z,
                frequency, seed, 2.0, 0.5, 1, true, false,
                QualityMode.Medium);
        }

        internal CoarseNoiseField CreateWorldAnchoredNoiseField(int spacing,
            Func<float, float, float> sampler)
        {
            float safeSpacing = Math.Max(1, spacing);
            float safeCosine = Math.Max(0.0001f, ProjectionCosLatitude);
            float absoluteLongitudeAtZero = (ProjectionOrigin.x
                + (ProjectionMean.x - Center.x / ProjectionScale)
                    / safeCosine) * ProjectionScale;
            float absoluteLatitudeAtZero = (ProjectionOrigin.y
                + ProjectionMean.y - Center.z / ProjectionScale)
                    * ProjectionScale;
            float longitudeNode = Mathf.Floor(
                absoluteLongitudeAtZero / safeSpacing) * safeSpacing;
            float latitudeNode = Mathf.Floor(
                absoluteLatitudeAtZero / safeSpacing) * safeSpacing;
            float originX = (longitudeNode / ProjectionScale
                - ProjectionOrigin.x) * safeCosine * ProjectionScale
                - ProjectionMean.x * ProjectionScale + Center.x;
            float originZ = (latitudeNode / ProjectionScale
                - ProjectionOrigin.y - ProjectionMean.y) * ProjectionScale
                + Center.z;
            return new CoarseNoiseField(Size, originX, originZ,
                safeSpacing * safeCosine, safeSpacing, sampler);
        }

        internal IntVec3 ProjectCenter(PlanetTile tile, bool clamp)
        {
            Vector2 point = ProjectPoint(tile);
            int x = Mathf.RoundToInt(point.x);
            int z = Mathf.RoundToInt(point.y);
            if (clamp)
            {
                int margin = Mathf.Clamp(
                    Mathf.RoundToInt(LocalCells * resolution) / 10, 32,
                    Math.Min(Size.x, Size.z) / 4);
                x = Mathf.Clamp(x, margin, Size.x - margin - 1);
                z = Mathf.Clamp(z, margin, Size.z - margin - 1);
            }
            return new IntVec3(x, 0, z);
        }

        internal Vector2 ProjectPoint(PlanetTile tile)
        {
            Vector2 value = Verse.Find.WorldGrid.LongLatOf(tile);
            float longitude = Mathf.DeltaAngle(ProjectionOrigin.x, value.x)
                * ProjectionCosLatitude;
            Vector2 coordinate = new Vector2(longitude,
                value.y - ProjectionOrigin.y);
            return new Vector2(Center.x
                    + (coordinate.x - ProjectionMean.x) * ProjectionScale,
                Center.z
                    + (coordinate.y - ProjectionMean.y) * ProjectionScale);
        }

        internal string ProjectionFrameSummary
        {
            get
            {
                return "origin " + ProjectionOrigin.x.ToString("F4") + ","
                    + ProjectionOrigin.y.ToString("F4") + "; center offset "
                    + ProjectionMean.x.ToString("F4") + ","
                    + ProjectionMean.y.ToString("F4") + "; scale "
                    + ProjectionScale.ToString("F3") + " cells/degree";
            }
        }

        private static float HillFactor(Hilliness hilliness)
        {
            switch (hilliness)
            {
                case Hilliness.Flat: return MapGenTuning.ElevationFactorFlat;
                case Hilliness.SmallHills:
                    return MapGenTuning.ElevationFactorSmallHills;
                case Hilliness.LargeHills:
                    return MapGenTuning.ElevationFactorLargeHills;
                case Hilliness.Mountainous:
                    return MapGenTuning.ElevationFactorMountains;
                case Hilliness.Impassable:
                    return MapGenTuning.ElevationFactorImpassableMountains;
                default: return 1f;
            }
        }

        // ---- reads ---------------------------------------------------------

        internal bool InBounds(IntVec3 cell)
        {
            return cell.x >= 0 && cell.z >= 0 && cell.x < Size.x
                && cell.z < Size.z;
        }

        internal BiomeDef BiomeAtIndex(int index)
        {
            if (RawBiomeByCell != null && index >= 0
                && index < RawBiomeByCell.Length)
                return RawBiomeByCell[index] ?? fallbackBiome;
            return fallbackBiome;
        }

        internal PlanetTile MemberTileAtIndex(int index)
        {
            if (MemberByCell == null || index < 0
                || index >= MemberByCell.Length) return PlanetTile.Invalid;
            int member = MemberByCell[index];
            return member >= 0 && member < Members.Count
                ? Members[member] : PlanetTile.Invalid;
        }

        internal bool IsVisualLandAtIndex(int index)
        {
            return MemberByCell != null && index >= 0
                && index < MemberByCell.Length && MemberByCell[index] >= 0
                && BiomeAtIndex(index)?.isWaterBiome != true
                && (BoundaryWaterByCell == null
                    || index >= BoundaryWaterByCell.Length
                    || BoundaryWaterByCell[index] < 0);
        }

        private readonly Dictionary<int, Vector2> visualLandAnchors =
            new Dictionary<int, Vector2>();

        // Typography and authoring identifiers belong to the visible land,
        // not to a Voronoi ownership polygon. Choose the largest connected
        // land component owned by the constituent, find its centroid, and then
        // snap to the nearest actual land cell so a label never lands in a bay,
        // lagoon, or detached water-owned part of the source area.
        internal Vector2 VisualLandAnchor(int memberTileId)
        {
            Vector2 cached;
            if (visualLandAnchors.TryGetValue(memberTileId, out cached))
                return cached;
            int member = Members == null ? -1 : Members.FindIndex(tile =>
                tile.tileId == memberTileId);
            if (member < 0 || MemberByCell == null)
                return new Vector2(Center.x, Center.z);

            cached = ComputeVisualLandAnchor(member);
            visualLandAnchors[memberTileId] = cached;
            return cached;
        }

        private Vector2 ComputeVisualLandAnchor(int member)
        {
            if (Members == null || member < 0 || member >= Members.Count
                || MemberByCell == null)
                return new Vector2(Center.x, Center.z);
            var visited = new bool[MemberByCell.Length];
            List<int> best = null;
            var queue = new Queue<int>();
            for (int index = 0; index < MemberByCell.Length; index++)
            {
                if (visited[index] || MemberByCell[index] != member
                    || !IsVisualLandAtIndex(index)) continue;
                var component = new List<int>();
                visited[index] = true;
                queue.Enqueue(index);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    component.Add(current);
                    int x = current % Size.x;
                    int z = current / Size.x;
                    EnqueueLand(current - 1, x > 0, member, visited, queue);
                    EnqueueLand(current + 1, x + 1 < Size.x, member, visited,
                        queue);
                    EnqueueLand(current - Size.x, z > 0, member, visited,
                        queue);
                    EnqueueLand(current + Size.x, z + 1 < Size.z, member,
                        visited, queue);
                }
                if (best == null || component.Count > best.Count)
                    best = component;
            }
            if (best == null || best.Count == 0)
            {
                PlanetTile tile = Members[member];
                return tile.Valid ? ProjectPoint(tile)
                    : new Vector2(Center.x, Center.z);
            }
            float meanX = 0f;
            float meanZ = 0f;
            foreach (int index in best)
            {
                meanX += index % Size.x;
                meanZ += index / Size.x;
            }
            meanX /= best.Count;
            meanZ /= best.Count;
            int nearest = best.OrderBy(index =>
            {
                float dx = index % Size.x - meanX;
                float dz = index / Size.x - meanZ;
                return dx * dx + dz * dz;
            }).First();
            return new Vector2(nearest % Size.x, nearest / Size.x);
        }

        private void EnqueueLand(int index, bool inBounds, int member,
            bool[] visited, Queue<int> queue)
        {
            if (!inBounds || index < 0 || index >= MemberByCell.Length
                || visited[index] || MemberByCell[index] != member
                || !IsVisualLandAtIndex(index)) return;
            visited[index] = true;
            queue.Enqueue(index);
        }

        internal IntVec3 CenterForMember(int memberTileId)
        {
            if (Members == null) return Center;
            int index = Members.FindIndex(tile =>
                tile.tileId == memberTileId);
            return index >= 0 ? MemberCenters[index] : Center;
        }

        internal IEnumerable<int> GeographicSourceTileIds
        {
            get
            {
                return (GeographicLandTiles ?? Members
                        ?? new List<PlanetTile>()).Concat(
                        BoundaryWaterTiles ?? new List<PlanetTile>())
                    .Select(tile => tile.tileId).Distinct()
                    .OrderBy(id => id);
            }
        }

        internal string DiagnosticSummary
        {
            get
            {
                string counts = BiomeCellCounts == null ? "none"
                    : string.Join(", ", BiomeCellCounts
                        .OrderByDescending(pair => pair.Value)
                        .ThenBy(pair => pair.Key.defName)
                        .Select(pair => pair.Key.defName + ":"
                            + pair.Value));
                return "biome cells [" + counts + "]; cross-biome edges "
                    + CrossBiomeEdges + "; triple-junction quads "
                    + TripleBiomeJunctionQuads + "; micro-fragment cells "
                    + "reassigned " + BiomeSmoothingReassignments
                    + "; projected water "
                    + WaterCellsBeforeLittoral + " (maximum depth "
                    + MaximumWaterDepthBeforeLittoral + ", shallow "
                    + ShallowWaterCellsBeforeLittoral + ", deep "
                    + DeepWaterCellsBeforeLittoral
                    + ", isotropic chamfer); retained water "
                    + WaterCells + " (shoreline " + ShorelineCells
                    + ", share " + (MemberByCell == null
                        || MemberByCell.Length == 0 ? "0.0"
                        : (100f * WaterCells / MemberByCell.Length)
                            .ToString("F1")) + "%); selected-core land "
                    + SelectedCoreLandCells + "; geographic-halo land "
                    + GeographicHaloLandCells
                    + "; vanilla-profile beach cells " + LittoralSandCells
                    + "; littoral components " + LittoralComponents
                    + " (largest " + LargestLittoralComponent + ", tiny "
                    + TinyLittoralComponents + "); source-center checks land "
                    + LandSourceCentersFlooded + " flooded, water "
                    + WaterSourceCentersDry + " dry; adjacent land joins "
                    + LandSourceJoinsChecked + " checked, "
                    + SeveredLandSourceJoins + " severed; coastal atolls "
                    + AtollCarrierCount
                    + " (lagoon cells " + AtollLagoonCells
                    + ", ring land " + AtollRingLandCells
                    + ", classification changes beyond carrier ownership "
                    + AtollChangedCellsBeyondCarrier + "); coves "
                    + CoveCarrierCount + " (water cells " + CoveWaterCells
                    + ", classification changes beyond carrier ownership "
                    + CoveChangedCellsBeyondCarrier + "); archipelagos "
                    + ArchipelagoCarrierCount + " (water cells "
                    + ArchipelagoWaterCells
                    + ", classification changes beyond carrier ownership "
                    + ArchipelagoChangedCellsBeyondCarrier + ")";
            }
        }

        // ---- classification post-passes ------------------------------------

        private void BuildRawBiomeCache()
        {
            RawBiomeByCell = new BiomeDef[MemberByCell.Length];
            for (int index = 0; index < RawBiomeByCell.Length; index++)
            {
                int waterIndex = BoundaryWaterByCell[index];
                if (waterIndex >= 0 && waterIndex < BoundaryWaterTiles.Count)
                {
                    RawBiomeByCell[index] = BoundaryWaterTiles[waterIndex]
                        .Tile.PrimaryBiome ?? fallbackBiome;
                    continue;
                }
                int memberIndex = MemberByCell[index];
                RawBiomeByCell[index] = memberIndex >= 0
                    && memberIndex < Members.Count
                    ? Members[memberIndex].Tile.PrimaryBiome ?? fallbackBiome
                    : fallbackBiome;
            }
        }

        private void SmoothMemberMicrofragments()
        {
            if (MemberByCell == null || Size.x < 3 || Size.z < 3) return;
            int[] source = (int[])MemberByCell.Clone();
            for (int z = 1; z < Size.z - 1; z++)
            {
                for (int x = 1; x < Size.x - 1; x++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    int index = Indices.CellToIndex(cell);
                    int current = source[index];
                    int north = source[Indices.CellToIndex(
                        cell + IntVec3.North)];
                    int south = source[Indices.CellToIndex(
                        cell + IntVec3.South)];
                    int east = source[Indices.CellToIndex(
                        cell + IntVec3.East)];
                    int west = source[Indices.CellToIndex(
                        cell + IntVec3.West)];
                    int replacement = ThreeOfFour(north, south, east, west);
                    if (replacement < 0 || replacement == current) continue;
                    MemberByCell[index] = replacement;
                    BiomeSmoothingReassignments++;
                }
            }
        }

        // A world tile is provenance, not a coastline stencil. Native
        // CoastalAtoll builds an outer sea, an island ring and an inner lagoon
        // around the map centre. Running that worker against the aggregate map
        // would stretch one feature across the whole region; suppressing it
        // without replacing its shape erases the feature. This re-expresses
        // the same three scalar operations around the carrier's visible land
        // at one-tile scale. The field is deliberately not
        // clipped to MemberByCell: ownership remains available for biome and
        // object provenance while the visible geography crosses that seam.
        private void ApplyContinuousGeographicFeatures()
        {
            if (!ModsConfig.OdysseyActive || Members == null
                || Members.Count == 0 || BoundaryWaterTiles == null
                || BoundaryWaterTiles.Count == 0) return;

            // Only the selected core owns authored/exercised features. Halo
            // land supplies continuous geographic context, but allowing its
            // mutators to become feature carriers would execute content the
            // candidate compatibility gate never inspected.
            var coreIds = new HashSet<int>(request.MemberTileIds
                ?? new List<int>());
            var atollCarriers = new List<int>();
            var coveCarriers = new List<int>();
            var archipelagoCarriers = new List<int>();
            for (int i = 0; i < Members.Count; i++)
            {
                if (!coreIds.Contains(Members[i].tileId)) continue;
                Tile info = Members[i].Valid ? Members[i].Tile : null;
                if (info == null) continue;
                foreach (TileMutatorDef mutator in info.Mutators)
                {
                    TileMutatorWorker worker = mutator?.Worker;
                    if (worker is TileMutatorWorker_CoastalAtoll)
                        atollCarriers.Add(i);
                    else if (worker is TileMutatorWorker_Cove)
                        coveCarriers.Add(i);
                    else if (worker is TileMutatorWorker_Archipelago)
                        archipelagoCarriers.Add(i);
                }
            }
            atollCarriers = atollCarriers.Distinct().ToList();
            coveCarriers = coveCarriers.Distinct().ToList();
            archipelagoCarriers = archipelagoCarriers.Distinct().ToList();
            if (atollCarriers.Count == 0 && coveCarriers.Count == 0
                && archipelagoCarriers.Count == 0) return;

            // Capture each carrier's visible land before any feature changes
            // the coast. World-tile centers describe provenance; feature
            // shapes belong to the land the player can actually see.
            Dictionary<int, Vector2> featureCenters = atollCarriers
                .Concat(coveCarriers).Concat(archipelagoCarriers)
                .Distinct().ToDictionary(index => index,
                    index => ComputeVisualLandAnchor(index));

            int spacing = Math.Max(1, Mathf.RoundToInt(8f * resolution));
            float span = Math.Max(8f, LocalCells * resolution);
            AtollCarrierCount = atollCarriers.Count;
            if (atollCarriers.Count > 0)
                atollFieldByCell = new bool[MemberByCell.Length];

            foreach (int carrierIndex in atollCarriers)
            {
                PlanetTile carrier = Members[carrierIndex];
                Vector2 center = featureCenters[carrierIndex];
                int oceanIndex = NearestOceanIndex(center);
                if (oceanIndex < 0) continue;

                int featureSalt = Gen.HashCombineInt(carrier.tileId,
                    1096044364); // "ATOL"
                CoarseNoiseField outerX = CreateWorldAnchoredNoiseField(
                    spacing, (x, z) => (SampleWorldNoise(x, z, 0.015f,
                        Gen.HashCombineInt(featureSalt, 11)) * 2f - 1f)
                            * 25f * resolution);
                CoarseNoiseField outerZ = CreateWorldAnchoredNoiseField(
                    spacing, (x, z) => (SampleWorldNoise(x, z, 0.015f,
                        Gen.HashCombineInt(featureSalt, 12)) * 2f - 1f)
                            * 25f * resolution);
                CoarseNoiseField shapeX = CreateWorldAnchoredNoiseField(
                    spacing, (x, z) =>
                        (SampleWorldNoise(x, z, 0.003f,
                            Gen.HashCombineInt(featureSalt, 21)) * 2f - 1f)
                                * 20f * resolution
                        + (SampleWorldNoise(x, z, 0.015f,
                            Gen.HashCombineInt(featureSalt, 22)) * 2f - 1f)
                                * 5f * resolution);
                CoarseNoiseField shapeZ = CreateWorldAnchoredNoiseField(
                    spacing, (x, z) =>
                        (SampleWorldNoise(x, z, 0.003f,
                            Gen.HashCombineInt(featureSalt, 23)) * 2f - 1f)
                                * 20f * resolution
                        + (SampleWorldNoise(x, z, 0.015f,
                            Gen.HashCombineInt(featureSalt, 24)) * 2f - 1f)
                                * 5f * resolution);

                float innerOffsetX = span * Mathf.Lerp(-0.08f, 0.08f,
                    DeterministicUnit(Gen.HashCombineInt(featureSalt, 31)));
                float innerOffsetZ = span * Mathf.Lerp(-0.08f, 0.08f,
                    DeterministicUnit(Gen.HashCombineInt(featureSalt, 32)));
                float angle = Verse.Find.World.CoastAngleAt(carrier,
                    BiomeDefOf.Ocean).GetValueOrDefault() * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                for (int z = 0; z < Size.z; z++)
                {
                    for (int x = 0; x < Size.x; x++)
                    {
                        int index = z * Size.x + x;
                        float localX = x - center.x;
                        float localZ = z - center.y;

                        float displacedOuterX = localX
                            + outerX.Sample(x, z);
                        float displacedOuterZ = localZ
                            + outerZ.Sample(x, z);
                        float rotatedOuterX = cos * displacedOuterX
                            + sin * displacedOuterZ;
                        float rotatedOuterZ = -sin * displacedOuterX
                            + cos * displacedOuterZ;
                        // Native scales one axis by .8 before evaluating the
                        // outer DistFromPoint, producing the atoll's broad oval.
                        float outerDistance = Mathf.Sqrt(
                            rotatedOuterX * rotatedOuterX
                            + rotatedOuterZ * 0.8f * rotatedOuterZ * 0.8f);
                        float outerNormalized = outerDistance / (span * 0.95f);
                        if (outerNormalized > 1.12f) continue;

                        float displacedShapeX = localX
                            + shapeX.Sample(x, z);
                        float displacedShapeZ = localZ
                            + shapeZ.Sample(x, z);
                        float islandDistance = Mathf.Sqrt(
                            displacedShapeX * displacedShapeX
                            + displacedShapeZ * displacedShapeZ);
                        float innerX = displacedShapeX - innerOffsetX;
                        float innerZ = displacedShapeZ - innerOffsetZ;
                        float innerDistance = Mathf.Sqrt(innerX * innerX
                            + innerZ * innerZ);

                        float outer = Mathf.Clamp(outerNormalized, 0.4f, 1f);
                        float island = 1f - islandDistance / (span * 0.65f);
                        float inner = Mathf.Clamp(innerDistance
                            / (span * 0.5f), 0.4f, 1f);
                        float original = CoastValueByCell[index];
                        float shaped = GenMath.SmoothMin(original, outer, 0.5f);
                        shaped = Mathf.Max(shaped, island);
                        shaped = Mathf.Clamp01(Mathf.Min(shaped, inner));

                        atollFieldByCell[index] = true;
                        CoastValueByCell[index] = shaped;
                        NearestBoundaryWaterByCell[index] = oceanIndex;
                        bool wasWater = BoundaryWaterByCell[index] >= 0;
                        bool isWater = shaped < 0.5f;
                        if (isWater)
                            BoundaryWaterByCell[index] = oceanIndex;
                        else
                            BoundaryWaterByCell[index] = -1;

                        // CoastalAtoll's native elevation hook zeros the sea,
                        // lagoon and its narrow sand band through .53.
                        if (shaped < 0.53f) HillFactorByCell[index] = 0f;
                        else
                        {
                            int owner = MemberByCell[index];
                            if (owner >= 0 && owner < MemberHillFactors.Count)
                                HillFactorByCell[index] =
                                    MemberHillFactors[owner];
                        }

                        if (inner < 0.5f) AtollLagoonCells++;
                        if (!isWater && island >= 0.5f && inner >= 0.5f)
                            AtollRingLandCells++;
                        if (wasWater != isWater
                            && MemberByCell[index] != carrierIndex)
                            AtollChangedCellsBeyondCarrier++;
                    }
                }
            }

            ApplyCoveFeatures(coveCarriers, spacing, span, featureCenters);
            ApplyArchipelagoFeatures(archipelagoCarriers, spacing, span,
                featureCenters);
            visualLandAnchors.Clear();
        }

        // Cove and Archipelago are authored geographic shapes, not labels on
        // a Voronoi ownership polygon. Their native workers center a noise
        // field on a one-tile map; here the same kind of field is centered on
        // the carrying area's visible land and allowed to cross the ownership
        // seam. World provenance and visible geography remain separate.
        private void ApplyCoveFeatures(List<int> carriers, int spacing,
            float span, Dictionary<int, Vector2> featureCenters)
        {
            CoveCarrierCount = carriers?.Count ?? 0;
            if (CoveCarrierCount == 0) return;
            foreach (int carrierIndex in carriers)
            {
                PlanetTile carrier = Members[carrierIndex];
                Vector2 center = featureCenters[carrierIndex];
                int oceanIndex = NearestOceanIndex(center);
                if (oceanIndex < 0) continue;

                int salt = Gen.HashCombineInt(carrier.tileId,
                    1129272914); // "COVE"
                CoarseNoiseField displacementX =
                    CreateWorldAnchoredNoiseField(spacing, (x, z) =>
                        (SampleWorldNoise(x, z, 0.003f,
                            Gen.HashCombineInt(salt, 11)) * 2f - 1f)
                                * 30f * resolution
                        + (SampleWorldNoise(x, z, 0.015f,
                            Gen.HashCombineInt(salt, 12)) * 2f - 1f)
                                * 25f * resolution);
                CoarseNoiseField displacementZ =
                    CreateWorldAnchoredNoiseField(spacing, (x, z) =>
                        (SampleWorldNoise(x, z, 0.003f,
                            Gen.HashCombineInt(salt, 13)) * 2f - 1f)
                                * 30f * resolution
                        + (SampleWorldNoise(x, z, 0.015f,
                            Gen.HashCombineInt(salt, 14)) * 2f - 1f)
                                * 25f * resolution);
                float angle = Verse.Find.World.CoastAngleAt(carrier,
                    BiomeDefOf.Ocean).GetValueOrDefault() * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float entranceBias = Mathf.Lerp(-0.10f, 0.10f,
                    DeterministicUnit(Gen.HashCombineInt(salt, 21))) * span;

                for (int z = 0; z < Size.z; z++)
                {
                    for (int x = 0; x < Size.x; x++)
                    {
                        float localX = x - center.x
                            + displacementX.Sample(x, z);
                        float localZ = z - center.y
                            + displacementZ.Sample(x, z);
                        // Native rotates both its radial offset and
                        // DistFromCone by coastAngle + 90 degrees. These are
                        // that rotation's along/across axes written directly.
                        float along = -sin * localX + cos * localZ;
                        float across = cos * localX + sin * localZ
                            - entranceBias;
                        float envelope = Mathf.Sqrt(localX * localX
                            + localZ * localZ) / (span * 0.92f);
                        if (envelope > 1.08f) continue;

                        // Native Cove smooth-mins a displaced half-map radial
                        // basin with a narrow DistFromCone entrance. The
                        // widening channel below preserves that readable
                        // basin-and-mouth topology at carrier scale.
                        float basinAlong = along + span * 0.18f;
                        float basin = Mathf.Sqrt(across * across
                            + basinAlong * basinAlong) / (span * 0.50f);
                        float mouthProgress = Mathf.InverseLerp(
                            -span * 0.18f, span * 0.72f, along);
                        float mouthHalfWidth = span * Mathf.Lerp(0.055f,
                            0.16f, mouthProgress);
                        float channel = Mathf.Abs(across)
                            / Math.Max(1f, mouthHalfWidth);
                        if (along < -span * 0.24f)
                            channel += (-span * 0.24f - along)
                                / (span * 0.12f);
                        else if (along > span * 0.78f)
                            channel += (along - span * 0.78f)
                                / (span * 0.12f);
                        float cove = GenMath.SmoothMin(basin,
                            channel * 0.25f, 0.2f);
                        cove = Mathf.Max(0.4f, cove);

                        int index = z * Size.x + x;
                        float shaped = GenMath.SmoothMin(
                            CoastValueByCell[index], cove, 0.5f);
                        float fade = Mathf.SmoothStep(0f, 1f,
                            Mathf.InverseLerp(0.78f, 1.08f, envelope));
                        shaped = Mathf.Lerp(shaped,
                            CoastValueByCell[index], fade);
                        bool wasWater = BoundaryWaterByCell[index] >= 0;
                        bool isWater = shaped < 0.5f;
                        CoastValueByCell[index] = shaped;
                        NearestBoundaryWaterByCell[index] = oceanIndex;
                        BoundaryWaterByCell[index] = isWater
                            ? oceanIndex : -1;
                        if (isWater)
                        {
                            HillFactorByCell[index] = 0f;
                            CoveWaterCells++;
                        }
                        else
                        {
                            int owner = MemberByCell[index];
                            if (wasWater && owner >= 0
                                && owner < MemberHillFactors.Count)
                                HillFactorByCell[index] =
                                    MemberHillFactors[owner];
                        }
                        if (wasWater != isWater
                            && MemberByCell[index] != carrierIndex)
                            CoveChangedCellsBeyondCarrier++;
                    }
                }
            }
        }

        private void ApplyArchipelagoFeatures(List<int> carriers, int spacing,
            float span, Dictionary<int, Vector2> featureCenters)
        {
            ArchipelagoCarrierCount = carriers?.Count ?? 0;
            if (ArchipelagoCarrierCount == 0) return;
            foreach (int carrierIndex in carriers)
            {
                PlanetTile carrier = Members[carrierIndex];
                Vector2 center = featureCenters[carrierIndex];
                int oceanIndex = NearestOceanIndex(center);
                if (oceanIndex < 0) continue;

                int salt = Gen.HashCombineInt(carrier.tileId,
                    1095781448); // "ARCH"
                CoarseNoiseField displacementX =
                    CreateWorldAnchoredNoiseField(spacing, (x, z) =>
                        (SampleWorldNoise(x, z, 0.015f,
                            Gen.HashCombineInt(salt, 11)) * 2f - 1f)
                                * 25f * resolution);
                CoarseNoiseField displacementZ =
                    CreateWorldAnchoredNoiseField(spacing, (x, z) =>
                        (SampleWorldNoise(x, z, 0.015f,
                            Gen.HashCombineInt(salt, 12)) * 2f - 1f)
                                * 25f * resolution);
                int islandSpacing = Math.Max(1,
                    Mathf.RoundToInt(4f * resolution));
                CoarseNoiseField islandNoise =
                    CreateWorldAnchoredNoiseField(islandSpacing, (x, z) =>
                    {
                        int sampleX = Mathf.RoundToInt(x);
                        int sampleZ = Mathf.RoundToInt(z);
                        return SampleWorldNoise(x + displacementX.Sample(
                                sampleX, sampleZ),
                            z + displacementZ.Sample(sampleX, sampleZ),
                            0.02f, Gen.HashCombineInt(salt, 31));
                    });

                for (int z = 0; z < Size.z; z++)
                {
                    for (int x = 0; x < Size.x; x++)
                    {
                        float localX = x - center.x;
                        float localZ = z - center.y;
                        float envelope = Mathf.Sqrt(localX * localX
                            + localZ * localZ) / (span * 0.95f);
                        if (envelope > 1.12f) continue;
                        float islands = islandNoise.Sample(x, z);
                        // Archipelago's native coast offset is fixed at .2;
                        // lowering the field slightly carries that more
                        // water-forward shoreline into the shared coast.
                        islands = Mathf.Clamp01(islands - 0.04f);

                        int index = z * Size.x + x;
                        float shaped = GenMath.SmoothMin(
                            CoastValueByCell[index], islands, 0.2f);
                        float fade = Mathf.SmoothStep(0f, 1f,
                            Mathf.InverseLerp(0.82f, 1.12f, envelope));
                        shaped = Mathf.Lerp(shaped,
                            CoastValueByCell[index], fade);
                        bool wasWater = BoundaryWaterByCell[index] >= 0;
                        bool isWater = shaped < 0.5f;
                        CoastValueByCell[index] = shaped;
                        NearestBoundaryWaterByCell[index] = oceanIndex;
                        BoundaryWaterByCell[index] = isWater
                            ? oceanIndex : -1;
                        if (isWater)
                        {
                            HillFactorByCell[index] = 0f;
                            ArchipelagoWaterCells++;
                        }
                        else
                        {
                            int owner = MemberByCell[index];
                            if (wasWater && owner >= 0
                                && owner < MemberHillFactors.Count)
                                HillFactorByCell[index] =
                                    MemberHillFactors[owner];
                        }
                        if (wasWater != isWater
                            && MemberByCell[index] != carrierIndex)
                            ArchipelagoChangedCellsBeyondCarrier++;
                    }
                }
            }
        }

        private int NearestOceanIndex(Vector2 point)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < BoundaryWaterTiles.Count; i++)
            {
                if (BoundaryWaterTiles[i].Tile.PrimaryBiome
                        != BiomeDefOf.Ocean) continue;
                Vector2 center = new Vector2(BoundaryWaterCenters[i].x,
                    BoundaryWaterCenters[i].z);
                float distance = (center - point).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        private static float DeterministicUnit(int seed)
        {
            unchecked
            {
                uint value = (uint)seed;
                value ^= value >> 16;
                value *= 0x7feb352dU;
                value ^= value >> 15;
                value *= 0x846ca68bU;
                value ^= value >> 16;
                return (value & 0x00ffffffU) / 16777215f;
            }
        }

        private static int ThreeOfFour(int north, int south, int east,
            int west)
        {
            if (north == south && (north == east || north == west))
                return north;
            if (east == west && (east == north || east == south)) return east;
            return -1;
        }

        private void BuildWaterDepth()
        {
            WaterDepth = new ushort[MemberByCell.Length];
            WaterCellsBeforeLittoral = 0;
            MaximumWaterDepthBeforeLittoral = 0;
            ShallowWaterCellsBeforeLittoral = 0;
            DeepWaterCellsBeforeLittoral = 0;
            int width = Size.x;
            int height = Size.z;
            var distance = new float[MemberByCell.Length];
            for (int i = 0; i < MemberByCell.Length; i++)
            {
                BiomeDef biome = RawBiomeByCell != null
                    ? RawBiomeByCell[i] : BiomeAtIndex(i);
                if (biome == null || biome.isWaterBiome)
                {
                    distance[i] = float.PositiveInfinity;
                    WaterCellsBeforeLittoral++;
                }
                else distance[i] = 0f;
            }

            // Eight-neighbor chamfer distance removes the diamond and
            // rectilinear bands produced by cardinal-only flood fill while
            // retaining an inexpensive exact grid pass on very large maps.
            const float diagonalDistance = 1.41421356f;
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = z * width + x;
                    if (x > 0) Improve(index, index - 1, 1f);
                    if (z > 0) Improve(index, index - width, 1f);
                    if (x > 0 && z > 0)
                        Improve(index, index - width - 1,
                            diagonalDistance);
                    if (x + 1 < width && z > 0)
                        Improve(index, index - width + 1,
                            diagonalDistance);
                }
            }
            for (int z = height - 1; z >= 0; z--)
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    int index = z * width + x;
                    if (x + 1 < width) Improve(index, index + 1, 1f);
                    if (z + 1 < height) Improve(index, index + width, 1f);
                    if (x + 1 < width && z + 1 < height)
                        Improve(index, index + width + 1,
                            diagonalDistance);
                    if (x > 0 && z + 1 < height)
                        Improve(index, index + width - 1,
                            diagonalDistance);
                }
            }
            for (int i = 0; i < WaterDepth.Length; i++)
            {
                if (RawBiomeByCell[i]?.isWaterBiome != true) continue;
                float measured = distance[i];
                ushort depth = float.IsPositiveInfinity(measured)
                    ? (ushort)(ushort.MaxValue - 1)
                    : (ushort)Mathf.Clamp(Mathf.CeilToInt(measured), 1,
                        ushort.MaxValue - 1);
                WaterDepth[i] = depth;
                MaximumWaterDepthBeforeLittoral = Math.Max(
                    MaximumWaterDepthBeforeLittoral, depth);
                if (CoastValueByCell != null
                    && CoastValueByCell[i] < 0.4f)
                    DeepWaterCellsBeforeLittoral++;
                else ShallowWaterCellsBeforeLittoral++;
            }

            void Improve(int targetIndex, int sourceIndex, float step)
            {
                float sourceDistance = distance[sourceIndex];
                if (float.IsPositiveInfinity(sourceDistance)) return;
                float candidate = sourceDistance + step;
                if (distance[targetIndex] <= candidate) return;
                distance[targetIndex] = candidate;
            }
        }

        private void BuildLittoralTopology()
        {
            LittoralFormationByCell = new byte[MemberByCell.Length];
            LittoralSandCells = 0;
            if (RawBiomeByCell == null
                || !RawBiomeByCell.Any(biome => biome == BiomeDefOf.Ocean))
                return;
            for (int index = 0; index < MemberByCell.Length; index++)
            {
                if (RawBiomeByCell[index]?.isWaterBiome == true) continue;
                int waterIndex = NearestBoundaryWaterByCell == null ? -1
                    : NearestBoundaryWaterByCell[index];
                if (waterIndex < 0 || waterIndex >= BoundaryWaterTiles.Count
                    || BoundaryWaterTiles[waterIndex].Tile.PrimaryBiome
                        != BiomeDefOf.Ocean
                    || CoastValueByCell[index] < 0.5f
                    || CoastValueByCell[index] >=
                        (atollFieldByCell != null
                            && atollFieldByCell[index] ? 0.53f : 0.6f))
                    continue;
                LittoralFormationByCell[index] =
                    (byte)CALittoralFormation.VanillaBeach;
                LittoralSandCells++;
            }
        }

        private void BuildProjectionDiagnostics()
        {
            BiomeCellCounts = new Dictionary<BiomeDef, int>();
            SelectedCoreLandCells = 0;
            GeographicHaloLandCells = 0;
            var selectedIds = new HashSet<int>(request.MemberTileIds
                ?? new List<int>());
            int width = Size.x;
            int height = Size.z;
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = z * width + x;
                    BiomeDef biome = BiomeAtIndex(index);
                    if (biome != null)
                    {
                        if (!BiomeCellCounts.ContainsKey(biome))
                            BiomeCellCounts[biome] = 0;
                        BiomeCellCounts[biome]++;
                        if (biome.isWaterBiome)
                        {
                            WaterCells++;
                            bool shore = (x > 0 && BiomeAtIndex(index - 1)
                                    ?.isWaterBiome != true)
                                || (x + 1 < width && BiomeAtIndex(index + 1)
                                    ?.isWaterBiome != true)
                                || (z > 0 && BiomeAtIndex(index - width)
                                    ?.isWaterBiome != true)
                                || (z + 1 < height && BiomeAtIndex(
                                    index + width)
                                    ?.isWaterBiome != true);
                            if (shore) ShorelineCells++;
                        }
                        else
                        {
                            int memberIndex = MemberByCell[index];
                            int tileId = memberIndex >= 0
                                    && memberIndex < Members.Count
                                ? Members[memberIndex].tileId : -1;
                            if (selectedIds.Contains(tileId))
                                SelectedCoreLandCells++;
                            else GeographicHaloLandCells++;
                        }
                    }
                    if (x + 1 < width && BiomeAtIndex(index + 1) != biome)
                        CrossBiomeEdges++;
                    if (z + 1 < height
                        && BiomeAtIndex(index + width) != biome)
                        CrossBiomeEdges++;
                    if (x + 1 >= width || z + 1 >= height) continue;
                    BiomeDef eastBiome = BiomeAtIndex(index + 1);
                    BiomeDef northBiome = BiomeAtIndex(index + width);
                    BiomeDef northEastBiome = BiomeAtIndex(
                        index + width + 1);
                    int distinct = DistinctBiomes(biome, eastBiome,
                        northBiome, northEastBiome);
                    if (distinct >= 3) TripleBiomeJunctionQuads++;
                }
            }

            ValidateProjectedSourceConnectivity();

            if (LittoralFormationByCell == null) return;
            var visited = new bool[LittoralFormationByCell.Length];
            var queue = new Queue<int>();
            for (int index = 0; index < LittoralFormationByCell.Length;
                index++)
            {
                if (visited[index] || LittoralFormationByCell[index]
                    == (byte)CALittoralFormation.None) continue;
                visited[index] = true;
                queue.Enqueue(index);
                int componentSize = 0;
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    componentSize++;
                    IntVec3 cell = Indices.IndexToCell(current);
                    foreach (IntVec3 direction in GenAdj.CardinalDirections)
                    {
                        IntVec3 adjacent = cell + direction;
                        if (!InBounds(adjacent)) continue;
                        int adjacentIndex = Indices.CellToIndex(adjacent);
                        if (visited[adjacentIndex]
                            || LittoralFormationByCell[adjacentIndex]
                                == (byte)CALittoralFormation.None) continue;
                        visited[adjacentIndex] = true;
                        queue.Enqueue(adjacentIndex);
                    }
                }
                LittoralComponents++;
                LargestLittoralComponent = Math.Max(
                    LargestLittoralComponent, componentSize);
                if (componentSize < 6) TinyLittoralComponents++;
            }
        }

        private void ValidateProjectedSourceConnectivity()
        {
            for (int i = 0; i < Members.Count; i++)
            {
                IntVec3 center = MemberCenters[i];
                if (InBounds(center)
                    && BiomeAtIndex(Indices.CellToIndex(center))
                        ?.isWaterBiome == true)
                    LandSourceCentersFlooded++;

                var neighbors = new List<PlanetTile>();
                Members[i].Layer.GetTileNeighbors(Members[i], neighbors);
                var neighborIds = new HashSet<int>(neighbors.Select(tile =>
                    tile.tileId));
                for (int j = i + 1; j < Members.Count; j++)
                {
                    if (!neighborIds.Contains(Members[j].tileId)) continue;
                    IntVec3 other = MemberCenters[j];
                    if (!InBounds(center) || !InBounds(other)) continue;
                    LandSourceJoinsChecked++;
                    int samples = Math.Max(Math.Abs(other.x - center.x),
                        Math.Abs(other.z - center.z));
                    bool severed = false;
                    for (int sample = 0; sample <= samples; sample++)
                    {
                        float progress = samples == 0 ? 0f
                            : sample / (float)samples;
                        IntVec3 cell = new IntVec3(Mathf.RoundToInt(Mathf.Lerp(
                                center.x, other.x, progress)), 0,
                            Mathf.RoundToInt(Mathf.Lerp(center.z, other.z,
                                progress)));
                        if (!InBounds(cell)
                            || BiomeAtIndex(Indices.CellToIndex(cell))
                                ?.isWaterBiome != true) continue;
                        severed = true;
                        break;
                    }
                    if (severed) SeveredLandSourceJoins++;
                }
            }

            for (int i = 0; i < BoundaryWaterCenters.Count; i++)
            {
                IntVec3 center = BoundaryWaterCenters[i];
                if (InBounds(center)
                    && BiomeAtIndex(Indices.CellToIndex(center))
                        ?.isWaterBiome != true)
                    WaterSourceCentersDry++;
            }
        }

        private static int DistinctBiomes(BiomeDef first, BiomeDef second,
            BiomeDef third, BiomeDef fourth)
        {
            int count = first == null ? 0 : 1;
            if (second != null && second != first) count++;
            if (third != null && third != first && third != second) count++;
            if (fourth != null && fourth != first && fourth != second
                && fourth != third) count++;
            return count;
        }
    }
}
