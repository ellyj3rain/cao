using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Draws projected regional geography. The projection kernel supplies the
    // region's continuous land/water
    // silhouette and the broad world area to which each object belongs. The
    // screen deliberately does not render a generated terrain map: no cell
    // beauty, fertility, relief, buildability, or exact object coordinate is
    // available yet. A settlement is assigned to an area; map generation
    // later finds its actual site on the realized terrain.
    //
    // The landing marker is an overlay. Moving it does not change the
    // projection or its cache key.
    [StaticConstructorOnStartup]
    internal static class CARegionalProjectionPreview
    {
        // A preview grid this wide costs a bounded amount however large the
        // real backing map is; it is still a sampled area diagram whenever
        // the backing map exceeds this bound.
        // Sized to the rect the dialog actually draws (~840px wide at the
        // 1280-class viewport), so the texture renders at ~1:1 instead of
        // being built small and stretched into mush. The kernel build is
        // cached by plan signature, so the cost is one build per candidate,
        // and every marker anchored to a constituent centroid gets the
        // finer ownership grid for free.
        internal const int TargetLongestSide = 832;

        private static long cachedSignature;
        private static CARegionalProjectionKernel cachedKernel;
        private static Texture2D cachedGround;
        private static CARegionalCandidateFacts cachedFacts;

        // Turning the shape or stepping the size builds a NEW composition,
        // and one cached slot meant every step back through orientations
        // paid the full kernel build again -- the clunk the operator
        // reported on the rotation control. A short most-recently-used
        // memory keeps the last few compositions warm, so cycling through
        // orientations costs one build each the first time and nothing
        // after.
        private sealed class CachedComposition
        {
            internal long Signature;
            internal CARegionalProjectionKernel Kernel;
            internal Texture2D Ground;
            internal CARegionalCandidateFacts Facts;
        }

        private const int CompositionCacheDepth = 8;
        private static readonly List<CachedComposition> compositions =
            new List<CachedComposition>();
        // Held per projection identity, so a measurement never outlives
        // the geography it measured.
        private static long fidelitySignature;
        private static CARegionalProjectionFidelity fidelity;

        internal static void RecordFidelity(
            CARegionalProjectionFidelity result)
        {
            fidelity = result;
            fidelitySignature = cachedSignature;
        }

        internal static CARegionalProjectionFidelity FidelityFor(
            CARegionalPlan plan)
        {
            EnsureCurrent(plan);
            return fidelity != null && fidelitySignature == cachedSignature
                && cachedKernel != null
                && fidelity.PreviewSize == cachedKernel.Size
                && Mathf.Approximately(fidelity.PreviewResolution,
                    cachedKernel.Resolution)
                ? fidelity : null;
        }

        internal static CARegionalProjectionKernel KernelFor(
            CARegionalPlan plan)
        {
            EnsureCurrent(plan);
            return cachedKernel;
        }

        internal static Texture2D GroundFor(CARegionalPlan plan)
        {
            EnsureCurrent(plan);
            return cachedGround;
        }

        internal static CARegionalCandidateFacts FactsFor(
            CARegionalPlan plan)
        {
            EnsureCurrent(plan);
            return cachedFacts;
        }

        internal static void Release()
        {
            foreach (CachedComposition held in compositions)
                if (held.Ground != null)
                    UnityEngine.Object.Destroy(held.Ground);
            compositions.Clear();
            cachedGround = null;
            cachedKernel = null;
            cachedFacts = null;
            fidelity = null;
            fidelitySignature = 0L;
            cachedSignature = 0L;
        }

        // World generation is queued on RimWorld's long-event worker. Its
        // WorldComponent constructors therefore cannot call Unity Destroy.
        // Detach every world-bound value immediately, then hand only the old
        // texture reference to the main-thread completion queue. A new world
        // may build a new cache before that callback runs without the callback
        // ever touching it.
        internal static void InvalidateForWorldBoundary()
        {
            List<Texture2D> staleGrounds = compositions
                .Where(held => held.Ground != null)
                .Select(held => held.Ground).ToList();
            compositions.Clear();
            cachedGround = null;
            cachedKernel = null;
            cachedFacts = null;
            fidelity = null;
            fidelitySignature = 0L;
            cachedSignature = 0L;
            if (staleGrounds.Count > 0)
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    foreach (Texture2D stale in staleGrounds)
                        UnityEngine.Object.Destroy(stale);
                });
        }

        // Preview geography changes with the footprint anchor, constituents,
        // and size. The landing tile does not change the projection. Sources
        // are sorted by tile ID so constituent ordering cannot change it.
        private static long Signature(CARegionalPlan plan)
        {
            ulong value = 14695981039346656037UL;
            value = SignatureStep(value, plan.bundleRootTileId);
            value = SignatureStep(value, plan.mapSize);
            IntVec3 backing = plan.BackingMapSize;
            value = SignatureStep(value, backing.x);
            value = SignatureStep(value, backing.z);
            value = SignatureStep(value, plan.memberTileIds.Count);
            // Hash the sorted identity sequence. The old sum-of-squares key
            // allowed different area sets with equal sums to reuse the wrong
            // projection, which violates composition ownership even though it
            // is rare on a particular world.
            foreach (int id in plan.memberTileIds.OrderBy(id => id))
                value = SignatureStep(value, id);
            long folded = CAFeatureShapeModel.FoldSignature(
                unchecked((long)value), plan);
            return folded;
        }

        private static ulong SignatureStep(ulong value, int item)
        {
            unchecked
            {
                value ^= (uint)item;
                return value * 1099511628211UL;
            }
        }

        private static void EnsureCurrent(CARegionalPlan plan)
        {
            if (plan == null || plan.memberTileIds == null
                || plan.memberTileIds.Count == 0)
            {
                Release();
                return;
            }
            long signature = Signature(plan);
            if (signature == cachedSignature && cachedKernel != null) return;
            CachedComposition warm = compositions.FirstOrDefault(held =>
                held.Signature == signature);
            if (warm != null)
            {
                compositions.Remove(warm);
                compositions.Insert(0, warm);
                cachedSignature = warm.Signature;
                cachedKernel = warm.Kernel;
                cachedGround = warm.Ground;
                cachedFacts = warm.Facts;
                return;
            }
            CARegionalProjectionRequest request =
                CARegionalProjectionRequest.ForPreview(plan,
                    TargetLongestSide);
            cachedSignature = signature;
            cachedKernel = CARegionalProjectionKernel.Build(request);
            if (!cachedKernel.Active && cachedKernel.Members == null)
            {
                cachedGround = null;
                cachedFacts = null;
                return;
            }
            cachedGround = Paint(cachedKernel);
            cachedFacts = new CARegionalCandidateFacts(cachedKernel,
                plan.memberTileIds);
            compositions.Insert(0, new CachedComposition
            {
                Signature = signature,
                Kernel = cachedKernel,
                Ground = cachedGround,
                Facts = cachedFacts
            });
            while (compositions.Count > CompositionCacheDepth)
            {
                CachedComposition evicted =
                    compositions[compositions.Count - 1];
                compositions.RemoveAt(compositions.Count - 1);
                if (evicted.Ground != null)
                    UnityEngine.Object.Destroy(evicted.Ground);
            }
            CARegionalGeographyComposition composition =
                CARegionalGeographyContract.Inspect(plan, cachedKernel);
            Log.Message("[CA][Regional][Preview] candidate projection "
                + cachedKernel.Size.x + "x" + cachedKernel.Size.z
                + " at resolution "
                + cachedKernel.Resolution.ToString("F3") + " of "
                + plan.BackingMapSize.x + "x" + plan.BackingMapSize.z
                + "; composition " + composition.Signature + "; "
                + (composition.IsValid ? "contract valid" : "contract FAILED: "
                    + string.Join("; ", composition.Failures)) + "; "
                + cachedKernel.PerformanceSummary);
        }

        // AREA DIAGRAM, not a generated-map preview. The screen reports only
        // the broad world-area membership and each area's world biome. It does
        // not paint per-cell relief, water depth, fertility, beauty or a
        // quality gradient: those depend on the eventual map generation and
        // cannot be promised from one sampled construction.
        private static Texture2D Paint(CARegionalProjectionKernel kernel)
        {
            if (kernel.MemberByCell == null
                || kernel.MemberByCell.Length == 0) return null;
            int width = kernel.Size.x;
            int height = kernel.Size.z;
            var pixels = new Color32[width * height];
            var biomeColors = new Dictionary<BiomeDef, Color>();
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = z * width + x;
                    pixels[index] = DiagramCellColor(kernel, index, x, z, width,
                        height, biomeColors);
                }
            }
            var texture = new Texture2D(width, height,
                TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static Color32 DiagramCellColor(
            CARegionalProjectionKernel kernel, int index, int x, int z,
            int width, int height, Dictionary<BiomeDef, Color> biomeColors)
        {
            int member = kernel.MemberByCell[index];
            if (member < 0 || kernel.Members == null
                || member >= kernel.Members.Count)
                return new Color(0.035f, 0.045f, 0.055f, 1f);
            PlanetTile area = kernel.Members[member];
            BiomeDef biome = area.Valid ? area.Tile?.PrimaryBiome : null;
            Color color = BiomeColor(biome, biomeColors);
            if (biome?.isWaterBiome == true)
                color = new Color(0.12f, 0.29f, 0.40f);
            BiomeDef realized = kernel.BiomeAtIndex(index);
            if (realized?.isWaterBiome == true)
            {
                if (kernel.BergValueByCell != null
                    && kernel.BergValueByCell[index] > 0.35f)
                    // Iceberg mass stands white on the water.
                    color = Color.Lerp(new Color(0.72f, 0.78f, 0.84f),
                        new Color(0.90f, 0.93f, 0.96f), Mathf.InverseLerp(
                            0.35f, 1f, kernel.BergValueByCell[index]));
                else
                {
                    // Water reads by its own depth: pale over shelves and
                    // sandbars, dark over open sea -- atolls, lagoons and
                    // island passages become legible instead of one flat
                    // blue.
                    float depth = kernel.WaterDepth != null
                        ? Mathf.Clamp01(kernel.WaterDepth[index] / 900f)
                        : 0.5f;
                    color = Color.Lerp(new Color(0.24f, 0.46f, 0.55f),
                        new Color(0.06f, 0.16f, 0.26f), depth);
                }
            }
            else if (kernel.LakeValueByCell != null
                && kernel.LakeValueByCell[index] > 0.25f
                && (kernel.LakeValueByCell[index] > 0.45f
                    || CARegionalProjectionKernel.IsLavaKind(
                        (CAInlandWaterKind)kernel.LakeKindByCell[index])))
            {
                // Inland water reads like the coast does: pale over the
                // shore band, dark at the basin's heart, and by its own
                // kind -- toxic water green, a dry bed as cracked pan,
                // lava glowing over volcanic rock.
                float value = kernel.LakeValueByCell[index];
                var kind = (CAInlandWaterKind)kernel.LakeKindByCell[index];
                if (CARegionalProjectionKernel.IsLavaKind(kind))
                    color = value > 0.4f
                        ? new Color(0.82f, 0.28f, 0.10f)
                        : new Color(0.26f, 0.21f, 0.19f);
                else if (kind == CAInlandWaterKind.Oasis)
                    color = value > 0.57f
                        ? new Color(0.22f, 0.44f, 0.52f)
                        : new Color(0.30f, 0.42f, 0.24f);
                else if (kind == CAInlandWaterKind.DryLake)
                    color = value > 0.5f
                        ? new Color(0.60f, 0.54f, 0.42f)
                        : new Color(0.66f, 0.61f, 0.45f);
                else if (value <= 0.5f)
                    color = new Color(0.66f, 0.61f, 0.45f);
                else
                {
                    float depth = Mathf.InverseLerp(0.5f, 0.85f, value);
                    color = kind == CAInlandWaterKind.Toxic
                        ? Color.Lerp(new Color(0.33f, 0.46f, 0.33f),
                            new Color(0.16f, 0.30f, 0.20f), depth)
                        : Color.Lerp(new Color(0.24f, 0.46f, 0.55f),
                            new Color(0.08f, 0.20f, 0.32f), depth);
                }
            }
            else if (kernel.LittoralFormationByCell != null
                && kernel.LittoralFormationByCell[index]
                    == (byte)CALittoralFormation.VanillaBeach)
                color = new Color(0.66f, 0.61f, 0.45f);
            else
            {
                color *= 0.86f;
                // Relief is real information: the same blended hill factor
                // the elevation rescale consumes shades the diagram, and
                // genuine mountain mass reads as rock instead of biome
                // green -- the map's shape stops lying about its ground.
                if (kernel.HillFactorByCell != null)
                {
                    float hill = kernel.HillFactorByCell[index];
                    if (hill > 1.35f)
                        color = Color.Lerp(color,
                            new Color(0.42f, 0.38f, 0.34f),
                            Mathf.Clamp01((hill - 1.35f) / 0.9f));
                    else
                        color *= Mathf.Lerp(0.94f, 1.10f,
                            Mathf.InverseLerp(0.80f, 1.35f, hill));
                }
            }
            // A stitched region owns its seams: a one-cell shade change
            // where area ownership turns over keeps the composition honest
            // without drawing a border wall.
            if (x + 1 < width && kernel.MemberByCell[index + 1] != member)
                color *= 0.90f;
            else if (z + 1 < height
                && kernel.MemberByCell[index + width] != member)
                color *= 0.90f;
            color.a = 1f;
            return color;
        }

        private static Color BiomeColor(BiomeDef biome,
            Dictionary<BiomeDef, Color> cache)
        {
            if (biome == null) return new Color(0.28f, 0.34f, 0.26f);
            Color found;
            if (cache.TryGetValue(biome, out found)) return found;
            Color color = new Color(0.28f, 0.36f, 0.28f);
            try
            {
                Material material = biome.DrawMaterial;
                if (material != null
                    && material.color.maxColorComponent > 0.05f)
                    color = material.color;
            }
            catch { }
            color.a = 1f;
            cache[biome] = color;
            return color;
        }
    }

    // What this candidate ALREADY IS, read once per projection.
    //
    // These are candidate facts, not generation outcomes: they are true of
    // the world tiles the footprint occupies before anything is generated,
    // which is why they belong on the pre-landing screen rather than being
    // deferred to the map that materialises them.
    internal sealed class CARegionalCandidateFacts
    {
        internal sealed class Link
        {
            internal PlanetTile From;
            internal PlanetTile To;
            internal RoadDef Road;
            internal RiverDef River;
        }

        internal sealed class Feature
        {
            internal PlanetTile Tile;
            internal Def Def;
            internal string Name;
            // Landmarks whose def is categorised "structure" are the marks
            // of people who were here before - ruins, abandoned colonies,
            // ancient works. The split is read off the def's own category
            // rather than a defName list, so modded landmarks classify
            // themselves.
            internal bool Historical;
            internal bool GeographicFeature;
        }

        internal sealed class Neighbor
        {
            internal WorldObject Object;
            internal Faction Faction;
            internal PlanetTile Tile;
            // Projected position in kernel cells. Inside the frame when
            // `InFrame`; otherwise a bearing from the frame centre.
            internal Vector2 Point;
            internal bool InFrame;
            internal float WorldDistanceTiles;
        }

        internal readonly List<Link> Roads = new List<Link>();
        internal readonly List<Link> Rivers = new List<Link>();
        internal readonly List<Feature> Features = new List<Feature>();
        internal readonly List<Neighbor> Neighbors = new List<Neighbor>();

        internal float ReliefLow = 1f;
        internal float ReliefHigh = 1f;
        internal float ReliefMean = 1f;
        internal int LandCells;

        internal IEnumerable<Feature> Landmarks
        {
            get { return Features.Where(item => !item.Historical); }
        }

        internal IEnumerable<Feature> HistoricalSites
        {
            get { return Features.Where(item => item.Historical); }
        }

        internal CARegionalCandidateFacts(
            CARegionalProjectionKernel kernel,
            IEnumerable<int> regionTileIds)
        {
            if (kernel?.Members == null) return;
            List<int> coreIds = (regionTileIds ?? Enumerable.Empty<int>())
                .Distinct().ToList();
            var core = new HashSet<int>(coreIds);
            CollectLinks(coreIds);
            CollectFeatures(coreIds);
            CollectNeighbors(kernel, coreIds);
            MeasureGround(kernel, core);
        }

        // One pass over the projected grid for both the relief spread and
        // which constituents reach the water.
        private void MeasureGround(CARegionalProjectionKernel kernel,
            HashSet<int> regionTileIds)
        {
            int[] owner = kernel.MemberByCell;
            if (owner == null || owner.Length == 0) return;
            float low = float.MaxValue;
            float high = float.MinValue;
            double sum = 0;
            var water = new bool[owner.Length];
            for (int i = 0; i < owner.Length; i++)
                water[i] = kernel.BiomeAtIndex(i)?.isWaterBiome == true;
            for (int i = 0; i < owner.Length; i++)
            {
                int member = owner[i];
                if (water[i] || member < 0 || member >= kernel.Members.Count
                    || !regionTileIds.Contains(
                        kernel.Members[member].tileId)) continue;
                LandCells++;
                float value = kernel.HillFactorByCell != null
                    ? kernel.HillFactorByCell[i] : 1f;
                if (value < low) low = value;
                if (value > high) high = value;
                sum += value;
            }
            if (LandCells > 0)
            {
                ReliefLow = low;
                ReliefHigh = high;
                ReliefMean = (float)(sum / LandCells);
            }
        }

        // MapGenTuning spans 0.80 flat to 1.10 mountainous; this is the same
        // blended factor the elevation rescale consumes at generation.
        internal string ReliefWords
        {
            get
            {
                if (LandCells == 0) return "no land";
                string band = ReliefMean < 0.87f ? "mostly flat"
                    : ReliefMean < 0.95f ? "gentle"
                    : ReliefMean < 1.03f ? "hilly" : "mountainous";
                return band + " (" + ReliefLow.ToString("F2") + "-"
                    + ReliefHigh.ToString("F2")
                    + " on the 0.80-1.10 scale)";
            }
        }

        private void CollectLinks(List<int> sourceIds)
        {
            var seenRoads = new HashSet<string>();
            var seenRivers = new HashSet<string>();
            foreach (int id in sourceIds)
            {
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(id);
                SurfaceTile surface = tile.Valid
                    ? tile.Tile as SurfaceTile : null;
                if (surface == null) continue;
                if (surface.Roads != null)
                    foreach (SurfaceTile.RoadLink road in surface.Roads)
                    {
                        if (road.road == null) continue;
                        string key = PairKey(id, road.neighbor.tileId,
                            road.road.defName);
                        if (!seenRoads.Add(key)) continue;
                        Roads.Add(new Link
                        {
                            From = tile,
                            To = road.neighbor,
                            Road = road.road
                        });
                    }
                if (surface.Rivers != null)
                    foreach (SurfaceTile.RiverLink river in surface.Rivers)
                    {
                        if (river.river == null) continue;
                        string key = PairKey(id, river.neighbor.tileId,
                            river.river.defName);
                        if (!seenRivers.Add(key)) continue;
                        Rivers.Add(new Link
                        {
                            From = tile,
                            To = river.neighbor,
                            River = river.river
                        });
                    }
            }
        }

        private static string PairKey(int left, int right, string defName)
        {
            return Math.Min(left, right) + ":" + Math.Max(left, right)
                + ":" + (defName ?? "none");
        }

        private void CollectFeatures(IEnumerable<int> regionTileIds)
        {
            foreach (PlanetTile tile in regionTileIds
                .Select(CARegionalPlanUtility.SurfaceTile)
                .Where(tile => tile.Valid))
            {
                var seen = new HashSet<string>();
                Landmark landmark = null;
                try { landmark = tile.Tile?.Landmark; }
                catch { }
                if (landmark?.def != null)
                {
                    seen.Add(landmark.def.defName ?? "landmark");
                    Features.Add(new Feature
                    {
                        Tile = tile,
                        Def = landmark.def,
                        Name = landmark.name.NullOrEmpty()
                            ? landmark.def.LabelCap.ToString() : landmark.name,
                        Historical = string.Equals(landmark.def.category,
                            "structure", StringComparison.OrdinalIgnoreCase)
                    });
                }

                // Tile mutators are the world's selected geographic features:
                // coasts, atolls, groves, stockpiles, caves, mountains and
                // their modded peers. They are not necessarily Landmark
                // objects, so reading only Tile.Landmark made exactly the
                // features that alter map generation disappear from the
                // authoring surface even while generation consumed them.
                Tile info = tile.Tile;
                if (info?.Mutators == null) continue;
                foreach (TileMutatorDef mutator in info.Mutators
                    .Where(item => item != null)
                    .OrderBy(item => item.label ?? item.defName))
                {
                    if (!seen.Add(mutator.defName ?? mutator.label ?? "feature"))
                        continue;
                    Features.Add(new Feature
                    {
                        Tile = tile,
                        Def = mutator,
                        Name = mutator.LabelCap.ToString(),
                        GeographicFeature = true
                    });
                }
            }
        }

        // The political surroundings. The footprint itself cannot hold a
        // world object - CARegionalWorldComponent.CanReserveRegion refuses
        // a member tile that is occupied - so everything here is genuinely
        // OUTSIDE the region, which is exactly what "surrounding context"
        // means.
        private void CollectNeighbors(CARegionalProjectionKernel kernel,
            List<int> sourceIds)
        {
            if (Verse.Find.WorldObjects == null) return;
            var footprint = new HashSet<int>(sourceIds);
            PlanetTile root = kernel.BundleRoot;
            if (!root.Valid) return;
            foreach (WorldObject item in Verse.Find.WorldObjects
                .AllWorldObjects)
            {
                if (item == null || item is
                    WorldObject_CARegionalMemberReservation) continue;
                // World settlements and other map-bearing holdings only.
                // Caravans and quest markers are not regional neighbors.
                if (!(item is MapParent)) continue;
                PlanetTile tile = item.Tile;
                if (!tile.Valid || footprint.Contains(tile.tileId)) continue;
                float distance;
                try
                {
                    distance = Verse.Find.WorldGrid.ApproxDistanceInTiles(
                        root, tile);
                }
                catch { continue; }
                if (distance > 14f) continue;
                Vector2 point = kernel.ProjectPoint(tile);
                bool inFrame = point.x >= 0f && point.y >= 0f
                    && point.x <= kernel.Size.x - 1
                    && point.y <= kernel.Size.z - 1;
                Neighbors.Add(new Neighbor
                {
                    Object = item,
                    Faction = item.Faction,
                    Tile = tile,
                    Point = point,
                    InFrame = inFrame,
                    WorldDistanceTiles = distance
                });
            }
            Neighbors.Sort((left, right) => left.WorldDistanceTiles
                .CompareTo(right.WorldDistanceTiles));
        }
    }

    internal enum CARegionSelectionKind
    {
        Region,
        Arrival,
        Settlement,
        Faction,
        Feature,
        Neighbor
    }

    [StaticConstructorOnStartup]
    internal static class CARegionMapWidget
    {
        // The region is the default selection, including when it has no
        // settlements.
        internal static CARegionSelectionKind selectedKind =
            CARegionSelectionKind.Region;
        internal static int selectedSlot = -1;
        internal static int selectedFactionKey = -1;
        internal static int selectedTileId = -1;
        internal static string selectedFeatureDefName;

        // Hover feedback flows both ways: point at an object's area and its
        // entry lights up; point at an entry and the whole assigned area
        // lights up. There is deliberately no settlement-coordinate marker.
        internal static int hoveredSlot = -1;

        // A faction under the cursor highlights all of its settlements. The
        // inspector sets this each frame; the next map draw consumes it.
        internal static int emphasisFactionKey = -1;

        // Armed for placement: the next click assigns this settlement to the
        // broad world area owning the clicked cell. It does not choose an
        // eventual in-map coordinate.
        internal static int awaitingSlot = -1;

        // Arrival uses the same broad-area interaction. It remains distinct
        // from settlement placement because the arrival must occupy valid,
        // unclaimed ground inside the current region.
        internal static bool awaitingArrivalArea;

        // Labels are visual projections of canonical objects, but they are
        // also real hit targets. Keeping their frame-local rectangles here
        // lets a click select the label it visibly landed on instead of
        // guessing from whichever constituent lies beneath the label.
        private enum LabelHitKind
        {
            Settlement,
            Arrival
        }

        private sealed class LabelHit
        {
            internal LabelHitKind Kind;
            internal int SettlementSlot;
            internal Rect Rect;
        }

        // Appended in paint order and resolved in reverse paint order, so the
        // label visibly on top always owns an overlapping click.
        private static readonly List<LabelHit> labelHits =
            new List<LabelHit>();

        internal static void SelectRegion()
        {
            selectedKind = CARegionSelectionKind.Region;
            selectedSlot = -1;
            selectedFactionKey = -1;
            selectedTileId = -1;
            selectedFeatureDefName = null;
            selectionChangedAt = Time.realtimeSinceStartup;
        }

        internal static void SelectSettlement(int slot)
        {
            selectedKind = CARegionSelectionKind.Settlement;
            selectedSlot = slot;
            selectionChangedAt = Time.realtimeSinceStartup;
        }

        internal static void SelectArrival(int tileId)
        {
            selectedKind = CARegionSelectionKind.Arrival;
            selectedTileId = tileId;
            selectionChangedAt = Time.realtimeSinceStartup;
        }

        internal static void SelectFaction(int factionKey)
        {
            selectedKind = CARegionSelectionKind.Faction;
            selectedFactionKey = factionKey;
            selectionChangedAt = Time.realtimeSinceStartup;
        }

        internal static void SelectFeature(int tileId, string defName = null)
        {
            selectedKind = CARegionSelectionKind.Feature;
            selectedTileId = tileId;
            selectedFeatureDefName = defName;
            selectionChangedAt = Time.realtimeSinceStartup;
        }

        internal static void SelectNeighbor(int tileId)
        {
            selectedKind = CARegionSelectionKind.Neighbor;
            selectedTileId = tileId;
            selectionChangedAt = Time.realtimeSinceStartup;
        }

        internal static void Reset()
        {
            SelectRegion();
            awaitingSlot = -1;
            awaitingArrivalArea = false;
            hoveredSlot = -1;
            labelHits.Clear();
        }

        // Ground is the base. The faction layer tints each area by its holder
        // and stripes disputed areas. The reach layer draws settlement links
        // and service reach. Layer switches use a short fade.
        internal static int overlayMode;
        internal static float overlayChangedAt;
        internal static float selectionChangedAt;
        private static Texture2D cachedOverlay;
        private static long cachedOverlaySignature;
        private static Texture2D cachedFocusOverlay;
        private static long cachedFocusSignature;

        // Called by the editor on the Unity thread when its page closes.
        internal static void Release()
        {
            Texture2D overlay = cachedOverlay;
            Texture2D focus = cachedFocusOverlay;
            cachedOverlay = null;
            cachedOverlaySignature = 0L;
            cachedFocusOverlay = null;
            cachedFocusSignature = 0L;
            if (overlay != null) UnityEngine.Object.Destroy(overlay);
            if (focus != null) UnityEngine.Object.Destroy(focus);
            overlayMode = 0;
            overlayChangedAt = 0f;
            emphasisFactionKey = -1;
            Reset();
        }

        // World construction may run off the Unity thread. Reset scalar state
        // without consulting Time, detach the old textures, and destroy only
        // those captured references after the long event returns to the main
        // thread.
        internal static void InvalidateForWorldBoundary()
        {
            Texture2D overlay = cachedOverlay;
            Texture2D focus = cachedFocusOverlay;
            cachedOverlay = null;
            cachedOverlaySignature = 0L;
            cachedFocusOverlay = null;
            cachedFocusSignature = 0L;
            selectedKind = CARegionSelectionKind.Region;
            selectedSlot = -1;
            selectedFactionKey = -1;
            selectedTileId = -1;
            selectedFeatureDefName = null;
            hoveredSlot = -1;
            emphasisFactionKey = -1;
            awaitingSlot = -1;
            awaitingArrivalArea = false;
            labelHits.Clear();
            overlayMode = 0;
            overlayChangedAt = 0f;
            selectionChangedAt = 0f;
            if (overlay != null)
                LongEventHandler.ExecuteWhenFinished(() =>
                    UnityEngine.Object.Destroy(overlay));
            if (focus != null)
                LongEventHandler.ExecuteWhenFinished(() =>
                    UnityEngine.Object.Destroy(focus));
        }

        private static float OverlayAlpha()
        {
            return Mathf.Clamp01(
                (Time.realtimeSinceStartup - overlayChangedAt) / 0.25f);
        }

        internal static void Draw(Rect rect, CARegionalPlan plan,
            Action changed, Action viewDetails = null,
            Action placeSettlement = null)
        {
            CAOpeningTheme.SurfacePanel(rect, true);
            Rect inner = rect.ContractedBy(6f);
            CARegionalProjectionKernel kernel =
                CARegionalProjectionPreview.KernelFor(plan);
            Texture2D ground = CARegionalProjectionPreview.GroundFor(plan);
            if (kernel == null || ground == null)
            {
                Widgets.Label(inner, "This region's land is still loading.");
                return;
            }

            CARegionalCandidateFacts facts =
                CARegionalProjectionPreview.FactsFor(plan);
            string contextText = ContextStripText(plan, facts);
            GameFont priorFont = Text.Font;
            Text.Font = GameFont.Tiny;
            float stripButton = ContextActionWidth(viewDetails,
                placeSettlement);
            float stripMarker = selectedKind == CARegionSelectionKind.Settlement
                    || selectedKind == CARegionSelectionKind.Faction
                ? 10f : 0f;
            float stripTextWidth = Mathf.Max(80f, inner.width - stripMarker
                - stripButton - (stripButton > 0f ? 6f : 0f));
            float minimumStripHeight = stripButton > 0f ? 30f : 22f;
            float stripHeight = Mathf.Clamp(Text.CalcHeight(contextText,
                stripTextWidth), minimumStripHeight, 66f);
            Text.Font = priorFont;
            Rect strip = new Rect(inner.x, inner.yMax - stripHeight,
                inner.width, stripHeight);
            Rect area = new Rect(inner.x, inner.y, inner.width,
                inner.height - stripHeight - 4f);
            DrawLayerButtons(ref area);

            // The map in its true proportions. A stitched region is rarely
            // square and pretending otherwise would misreport its shape.
            float aspect = kernel.Size.x / (float)kernel.Size.z;
            float w = area.width;
            float h = w / aspect;
            if (h > area.height) { h = area.height; w = h * aspect; }
            Rect map = new Rect(area.x + (area.width - w) * 0.5f,
                area.y + (area.height - h) * 0.5f, w, h);
            DrawCanvas(map, plan, kernel, ground, facts, changed, true);
            DrawContextStrip(strip, plan, contextText, viewDetails,
                placeSettlement);
        }

        // The canvas body without the widget's own chrome: ground, layer
        // overlays, geographic marks, labels and click handling, for a host
        // that owns its own framing (the opening workspace). Selection
        // state and texture caches are shared with the boxed Draw above so
        // every surface agrees about what is selected.
        internal static void DrawCanvas(Rect map, CARegionalPlan plan,
            CARegionalProjectionKernel kernel, Texture2D ground,
            CARegionalCandidateFacts facts, Action changed, bool interact)
        {
            labelHits.Clear();
            GUI.DrawTexture(map, ground);
            if (overlayMode == 1)
            {
                Texture2D overlay = FactionOverlayFor(plan, kernel);
                if (overlay != null)
                {
                    GUI.color = new Color(1f, 1f, 1f, OverlayAlpha());
                    GUI.DrawTexture(map, overlay);
                    GUI.color = Color.white;
                }
            }
            Texture2D focus = AreaFocusOverlayFor(plan, kernel);
            if (focus != null) GUI.DrawTexture(map, focus);
            CAOpeningTheme.Border(map, CAOpeningTheme.Hairline);

            DrawWaterLinks(map, kernel, facts);
            DrawRoads(map, kernel, facts);
            DrawNeighbors(map, kernel, facts);
            DrawFeatures(map, kernel, facts);
            if (overlayMode == 2) DrawReach(map, kernel, plan);
            DrawSettlementAreaLabels(map, kernel, plan);
            DrawLanding(map, kernel, plan);
            // Clicks are resolved in ONE place, in an explicit priority
            // order, rather than by whichever invisible button happens to
            // register its control first. Markers overlap constantly on a
            // dense region, and "whatever GUI saw first" is not an order
            // anyone can predict from looking at the screen.
            if (interact) HandleClicks(map, kernel, plan, facts, changed);
        }

        private static void DrawLayerButtons(ref Rect area)
        {
            string[] names = { "Land", "Factions", "Connections" };
            string[] tips =
            {
                "The region's terrain, water, roads and landmarks.",
                "Which faction holds the ground around each settlement. "
                    + "Mixed colors show overlapping presence.",
                "Links between central and dependent settlements, plus "
                    + "services that reach across the region."
            };
            // Self-describing chips; the "Map view" caption earned nothing.
            Rect row = new Rect(area.x, area.y, area.width, 26f);
            float w = (row.width - 12f) / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                var button = new Rect(row.x + i * (w + 6f), row.y, w, 24f);
                bool active = overlayMode == i;
                if (CAOpeningTheme.Chip(button, names[i], active, tips[i])
                    && !active)
                {
                    overlayMode = i;
                    overlayChangedAt = Time.realtimeSinceStartup;
                }
            }
            area = new Rect(area.x, area.y + 30f, area.width,
                area.height - 30f);
        }

        // Faction presence uses the same area assignment generation consumes.
        private static Texture2D FactionOverlayFor(CARegionalPlan plan,
            CARegionalProjectionKernel kernel)
        {
            if (kernel.MemberByCell == null || plan == null) return null;
            long signature = 31L * plan.settlements.Count;
            foreach (CARegionalSettlementPlan settlementPlan in plan.settlements)
                if (settlementPlan != null)
                    signature = signature * 31L
                        + settlementPlan.memberTileId * 7L
                        + (int)(settlementPlan.factionLinks?.ownership
                            ?? CASiteFactionReferenceKind.None) * 17L
                        + (settlementPlan.factionLinks
                            ?.ownerRegionalFactionKey ?? -1) * 23L
                        + (settlementPlan.factionLinks
                            ?.ownerWorldFactionLoadId ?? -1);
            if (signature == cachedOverlaySignature
                && cachedOverlay != null) return cachedOverlay;

            var owners = new Dictionary<int, List<CASiteFactionLinks>>();
            for (int i = 0; i < plan.settlements.Count; i++)
            {
                CARegionalSettlementPlan settlementPlan = plan.settlements[i];
                if (settlementPlan == null) continue;
                int member = kernel.Members == null ? -1
                    : kernel.Members.FindIndex(tile =>
                        tile.tileId == settlementPlan.memberTileId);
                if (member < 0) continue;
                List<CASiteFactionLinks> groups;
                if (!owners.TryGetValue(member, out groups))
                    owners[member] = groups = new List<CASiteFactionLinks>();
                CASiteFactionLinks links = settlementPlan.factionLinks
                    ?? new CASiteFactionLinks();
                if (!groups.Any(value => SameOwner(value, links)))
                    groups.Add(links);
            }

            int width = kernel.Size.x;
            int height = kernel.Size.z;
            var pixels = new Color32[width * height];
            for (int z = 0; z < height; z++)
                for (int x = 0; x < width; x++)
                {
                    int index = z * width + x;
                    int member = kernel.MemberByCell[index];
                    List<CASiteFactionLinks> groups;
                    if (member < 0 || !kernel.IsVisualLandAtIndex(index)
                        || !owners.TryGetValue(member, out groups))
                    {
                        pixels[index] = new Color32(10, 12, 14, 60);
                        continue;
                    }
                    CASiteFactionLinks pick = groups.Count == 1 ? groups[0]
                        : groups[((x + z) / 6) % groups.Count];
                    Color color = SiteOwnerColor(plan, pick);
                    pixels[index] = new Color32((byte)(color.r * 255),
                        (byte)(color.g * 255), (byte)(color.b * 255),
                        (byte)(groups.Count > 1 ? 150 : 105));
                }
            if (cachedOverlay != null)
                UnityEngine.Object.Destroy(cachedOverlay);
            cachedOverlay = new Texture2D(width, height,
                TextureFormat.RGBA32, false);
            cachedOverlay.wrapMode = TextureWrapMode.Clamp;
            cachedOverlay.filterMode = FilterMode.Bilinear;
            cachedOverlay.SetPixels32(pixels);
            cachedOverlay.Apply();
            cachedOverlaySignature = signature;
            return cachedOverlay;
        }

        private static bool SameOwner(CASiteFactionLinks left,
            CASiteFactionLinks right)
        {
            if (left == null || right == null) return left == right;
            return left.ownership == right.ownership
                && left.ownerRegionalFactionKey
                    == right.ownerRegionalFactionKey
                && left.ownerWorldFactionLoadId
                    == right.ownerWorldFactionLoadId;
        }

        private static Color SiteOwnerColor(CARegionalPlan plan,
            CASiteFactionLinks links)
        {
            if (links?.ownership
                    == CASiteFactionReferenceKind.RegionalFaction)
                return CARegionalWorldOverlay.FactionColor(
                    links.ownerRegionalFactionKey);
            if (links?.ownership == CASiteFactionReferenceKind.WorldFaction)
            {
                Faction faction = CASiteState.ResolveOwner(plan, links);
                if (faction != null) return faction.Color;
            }
            return new Color(0.62f, 0.66f, 0.68f, 1f);
        }

        // Selection is an AREA fill. A settlement does not have a generated
        // map coordinate yet, so centering a point inside its source tile
        // would manufacture precision. This mask says only what canonical
        // state says: which broad world area contains the selected object.
        private static Texture2D AreaFocusOverlayFor(CARegionalPlan plan,
            CARegionalProjectionKernel kernel)
        {
            if (plan == null || kernel?.MemberByCell == null
                || kernel.Members == null) return null;
            int emphasis = emphasisFactionKey >= 0 ? emphasisFactionKey
                : selectedKind == CARegionSelectionKind.Faction
                    ? selectedFactionKey : -1;
            var colors = new Dictionary<int, Color>();

            // The editor's projection includes a narrow geographic halo so a
            // coast, river, or landform is not clipped at the selected tiles.
            // That halo is context, not part of the authored region. When the
            // region is the selected object, tint its complete saved footprint
            // as one fill so the player can see where the region ends without
            // exposing constituent seams as the primary geography.
            if (selectedKind == CARegionSelectionKind.Region)
                foreach (int tileId in plan.memberTileIds.Distinct())
                    colors[tileId] = new Color(0.42f, 0.82f, 0.92f, 0.18f);

            if (emphasis >= 0)
            {
                Color factionColor =
                    CARegionalWorldOverlay.FactionColor(emphasis);
                factionColor.a = 0.28f;
                foreach (CARegionalSettlementPlan settlement in plan.settlements)
                    if (settlement != null
                        && settlement.OwningFactionKey == emphasis)
                        colors[settlement.memberTileId] = factionColor;
            }

            CARegionalSettlementPlan hovered = plan.settlements.FirstOrDefault(
                settlement => settlement != null
                    && settlement.slot == hoveredSlot);
            if (hovered != null)
                colors[hovered.memberTileId] =
                    new Color(0.70f, 0.90f, 1f, 0.30f);

            if (selectedKind == CARegionSelectionKind.Settlement)
            {
                CARegionalSettlementPlan selected = plan.settlements.FirstOrDefault(
                    settlement => settlement != null
                        && settlement.slot == selectedSlot);
                if (selected != null)
                    colors[selected.memberTileId] =
                        new Color(1f, 0.88f, 0.34f, 0.38f);
            }
            else if (selectedKind == CARegionSelectionKind.Arrival
                && plan.memberTileIds.Contains(plan.startTileId))
                colors[plan.startTileId] =
                    new Color(0.42f, 0.90f, 1f, 0.38f);
            else if (selectedKind == CARegionSelectionKind.Feature
                && selectedTileId >= 0)
                colors[selectedTileId] =
                    new Color(0.82f, 0.94f, 0.86f, 0.32f);

            if (awaitingSlot >= 0)
            {
                CARegionalSettlementPlan moving = plan.settlements.FirstOrDefault(
                    settlement => settlement != null
                        && settlement.slot == awaitingSlot);
                if (moving != null)
                    colors[moving.memberTileId] =
                        new Color(0.50f, 0.90f, 1f, 0.35f);
            }
            if (awaitingArrivalArea
                && plan.memberTileIds.Contains(plan.startTileId))
                colors[plan.startTileId] =
                    new Color(0.42f, 0.90f, 1f, 0.38f);
            if (colors.Count == 0) return null;

            long signature = plan.bundleRootTileId;
            signature = signature * 31L + kernel.Size.x;
            signature = signature * 31L + kernel.Size.z;
            foreach (KeyValuePair<int, Color> pair in colors
                .OrderBy(entry => entry.Key))
            {
                signature = signature * 31L + pair.Key;
                Color32 packed = pair.Value;
                signature = signature * 31L + packed.r;
                signature = signature * 31L + packed.g;
                signature = signature * 31L + packed.b;
                signature = signature * 31L + packed.a;
            }
            if (cachedFocusOverlay != null
                && cachedFocusSignature == signature)
                return cachedFocusOverlay;

            var colorByMember = new Dictionary<int, Color32>();
            for (int i = 0; i < kernel.Members.Count; i++)
            {
                Color color;
                if (colors.TryGetValue(kernel.Members[i].tileId, out color))
                    colorByMember[i] = color;
            }
            if (colorByMember.Count == 0) return null;
            var pixels = new Color32[kernel.MemberByCell.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 color;
                pixels[i] = kernel.IsVisualLandAtIndex(i)
                    && colorByMember.TryGetValue(
                        kernel.MemberByCell[i], out color)
                    ? color : new Color32(0, 0, 0, 0);
            }
            if (cachedFocusOverlay != null)
                UnityEngine.Object.Destroy(cachedFocusOverlay);
            cachedFocusOverlay = new Texture2D(kernel.Size.x, kernel.Size.z,
                TextureFormat.RGBA32, false);
            cachedFocusOverlay.wrapMode = TextureWrapMode.Clamp;
            cachedFocusOverlay.filterMode = FilterMode.Bilinear;
            cachedFocusOverlay.SetPixels32(pixels);
            cachedFocusOverlay.Apply();
            cachedFocusSignature = signature;
            return cachedFocusOverlay;
        }

        // Draws links from the regional seat to dependent settlements and a
        // ring for provisions that serve the surrounding region. Both use the
        // same realized fields as generation.
        private static void DrawReach(Rect map,
            CARegionalProjectionKernel kernel, CARegionalPlan plan)
        {
            if (Event.current.type != EventType.Repaint) return;
            float alpha = OverlayAlpha();
            CARegionalSettlementPlan center = plan.settlements.FirstOrDefault(b =>
                b != null && b.realizedRole
                    == (byte)CASettlementRole.Center);
            foreach (CARegionalSettlementPlan settlementPlan in plan.settlements)
            {
                if (settlementPlan == null) continue;
                Vector2 at = ToGui(map, kernel,
                    SettlementPoint(kernel, plan, settlementPlan));
                if (center != null && settlementPlan != center
                    && settlementPlan.realizedRole
                        == (byte)CASettlementRole.Satellite)
                {
                    Vector2 hub = ToGui(map, kernel,
                        SettlementPoint(kernel, plan, center));
                    Widgets.DrawLine(hub, at, new Color(1f, 0.95f, 0.6f,
                        0.55f * alpha), 2f);
                }
                bool regionReach = settlementPlan.provisionArrangements != null
                    && settlementPlan.provisionArrangements.Any(a => a != null
                        && a.active
                        && a.reach == CAProvisionReach.Region);
                if (regionReach)
                {
                    // A hollow circular ring - service reach around the
                    // provider, occluding nothing underneath it.
                    Color ringColor = new Color(1f, 0.95f, 0.6f,
                        0.75f * alpha);
                    const float r = 26f;
                    const int segments = 16;
                    Vector2 previous = at + new Vector2(r, 0f);
                    for (int s = 1; s <= segments; s++)
                    {
                        float angle = s * Mathf.PI * 2f / segments;
                        Vector2 next = at + new Vector2(
                            Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
                        Widgets.DrawLine(previous, next, ringColor, 2f);
                        previous = next;
                    }
                }
            }
        }

        // ---- coordinate transfer -------------------------------------------

        internal static Vector2 ToGui(Rect map,
            CARegionalProjectionKernel kernel, Vector2 cellPoint)
        {
            float nx = (cellPoint.x + 0.5f) / kernel.Size.x;
            float nz = (cellPoint.y + 0.5f) / kernel.Size.z;
            return new Vector2(map.x + nx * map.width,
                map.y + (1f - nz) * map.height);
        }

        private static float CellsToPixels(Rect map,
            CARegionalProjectionKernel kernel, float cells)
        {
            return cells / Math.Max(1, kernel.Size.x) * map.width;
        }

        // ---- overlays -------------------------------------------------------

        // Endpoints and corridor widths come from generation. The preview uses
        // straight links rather than generated road paths or river meanders,
        // and enforces a minimum visible line width.
        private static void DrawWaterLinks(Rect map,
            CARegionalProjectionKernel kernel, CARegionalCandidateFacts facts)
        {
            if (facts == null || Event.current.type != EventType.Repaint)
                return;
            foreach (CARegionalCandidateFacts.Link link in facts.Rivers)
            {
                float radius = Mathf.Clamp(link.River.widthOnMap * 0.5f,
                    1.5f, 15f) * kernel.Resolution;
                DrawLink(map, kernel, link, new Color(0.30f, 0.52f, 0.68f),
                    Math.Max(2f, CellsToPixels(map, kernel, radius * 2f)),
                    0.10f);
            }
        }

        private static void DrawRoads(Rect map,
            CARegionalProjectionKernel kernel, CARegionalCandidateFacts facts)
        {
            if (facts == null || Event.current.type != EventType.Repaint)
                return;
            foreach (CARegionalCandidateFacts.Link link in facts.Roads
                .OrderBy(item => item.Road.priority))
            {
                float radius = Mathf.Clamp(link.Road.priority / 20f, 1f, 4f)
                    * kernel.Resolution;
                Color color = link.Road.priority >= 40
                    ? new Color(0.80f, 0.74f, 0.58f)
                    : new Color(0.63f, 0.56f, 0.43f);
                DrawLink(map, kernel, link, color,
                    Math.Max(2.5f, CellsToPixels(map, kernel, radius * 2f)),
                    0.03f);
            }
        }

        private static void DrawLink(Rect map,
            CARegionalProjectionKernel kernel,
            CARegionalCandidateFacts.Link link, Color color,
            float pixelThickness, float meander = 0f)
        {
            Vector2 from = kernel.ProjectPoint(link.From);
            Vector2 to = kernel.ProjectPoint(link.To);
            if (!ClipToFrame(kernel, ref from, ref to)) return;
            // Widgets.DrawLine multiplies its width argument by three before
            // drawing, so the argument is a third of the thickness wanted.
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (meander <= 0f || length < 6f)
            {
                Widgets.DrawLine(ToGui(map, kernel, from),
                    ToGui(map, kernel, to), color, pixelThickness / 3f);
                return;
            }
            // A river is not a ruled line. The channel sways around its
            // link with a deterministic meander seeded by the tile pair --
            // the same pair always draws the same bends -- pinched to zero
            // at both ends so chained links stay continuous.
            Vector2 across = new Vector2(-delta.y, delta.x) / length;
            float phase = ((link.From.tileId * 31 + link.To.tileId * 17)
                % 628) / 100f;
            const int Steps = 8;
            Vector2 prior = from;
            for (int step = 1; step <= Steps; step++)
            {
                float t = step / (float)Steps;
                float pinch = 1f - Mathf.Abs(2f * t - 1f);
                float sway = Mathf.Sin(t * Mathf.PI * 2f + phase) * pinch
                    * length * meander;
                Vector2 point = step == Steps ? to
                    : from + delta * t + across * sway;
                point.x = Mathf.Clamp(point.x, 0f, kernel.Size.x - 1f);
                point.y = Mathf.Clamp(point.y, 0f, kernel.Size.z - 1f);
                Widgets.DrawLine(ToGui(map, kernel, prior),
                    ToGui(map, kernel, point), color, pixelThickness / 3f);
                prior = point;
            }
        }

        // Liang-Barsky against the projected frame, matching the clip the
        // generator applies before it paints a link.
        private static bool ClipToFrame(CARegionalProjectionKernel kernel,
            ref Vector2 start, ref Vector2 end)
        {
            float t0 = 0f;
            float t1 = 1f;
            Vector2 delta = end - start;
            if (!Clip(-delta.x, start.x, ref t0, ref t1)
                || !Clip(delta.x, kernel.Size.x - 1f - start.x, ref t0,
                    ref t1)
                || !Clip(-delta.y, start.y, ref t0, ref t1)
                || !Clip(delta.y, kernel.Size.z - 1f - start.y, ref t0,
                    ref t1)) return false;
            Vector2 original = start;
            start = original + delta * t0;
            end = original + delta * t1;
            return true;
        }

        private static bool Clip(float denominator, float numerator,
            ref float t0, ref float t1)
        {
            if (Mathf.Abs(denominator) < 0.0001f) return numerator >= 0f;
            float value = numerator / denominator;
            if (denominator < 0f)
            {
                if (value > t1) return false;
                if (value > t0) t0 = value;
            }
            else
            {
                if (value < t0) return false;
                if (value < t1) t1 = value;
            }
            return true;
        }

        // Feature marks identify the broad area carrying the world feature.
        // The mark is centered for legibility and makes no claim about the
        // feature's eventual in-map shape or coordinate.
        private static void DrawFeatures(Rect map,
            CARegionalProjectionKernel kernel, CARegionalCandidateFacts facts)
        {
            if (facts == null) return;
            bool roomForLabels = map.width > 520f;
            foreach (CARegionalCandidateFacts.Feature feature in
                facts.Features)
            {
                Vector2 point = FeatureScreenPoint(map, kernel, facts,
                    feature);
                // A landmark pin - a grounded tick with the feature's own
                // name, never an abstract glyph. Ochre marks an old site;
                // off-white marks a present landmark. Geographic features
                // draw as geometry elsewhere; the pin only names the ground
                // that carries them.
                Color color = feature.Historical
                    ? new Color(0.82f, 0.72f, 0.46f)
                    : new Color(0.90f, 0.91f, 0.88f);
                var halo = new Color(0.05f, 0.06f, 0.07f, 0.85f);
                var stem = new Rect(point.x - 1f, point.y - 9f, 2f, 9f);
                var foot = new Rect(point.x - 3f, point.y - 1f, 6f, 2f);
                Widgets.DrawBoxSolid(stem.ExpandedBy(1f), halo);
                Widgets.DrawBoxSolid(foot.ExpandedBy(1f), halo);
                Widgets.DrawBoxSolid(stem, color);
                Widgets.DrawBoxSolid(foot, color);
                Rect hit = new Rect(point.x - 8f, point.y - 12f, 16f, 16f);
                if (roomForLabels && !feature.Name.NullOrEmpty())
                {
                    Text.Font = GameFont.Tiny;
                    Vector2 size = Text.CalcSize(feature.Name);
                    var label = new Rect(point.x + 6f, point.y - 16f,
                        size.x + 8f, 15f);
                    label.x = Mathf.Min(label.x,
                        map.xMax - label.width - 2f);
                    label.y = Mathf.Max(label.y, map.y + 2f);
                    Widgets.DrawBoxSolid(label,
                        new Color(0.05f, 0.06f, 0.07f, 0.72f));
                    GUI.color = color;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(label, feature.Name);
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                    hit = new Rect(Mathf.Min(hit.x, label.x),
                        Mathf.Min(hit.y, label.y),
                        Mathf.Max(hit.xMax, label.xMax)
                            - Mathf.Min(hit.x, label.x),
                        Mathf.Max(hit.yMax, label.yMax)
                            - Mathf.Min(hit.y, label.y));
                }
                TooltipHandler.TipRegion(hit,
                    feature.Name + "\n" + feature.Def.LabelCap
                    + (feature.Historical
                        ? " - old site"
                        : feature.GeographicFeature
                            ? " - geographic feature" : " - landmark")
                    + "\n\nWorld area carrying this feature. Its map shape "
                    + "is generated when play begins.");
            }
        }

        private static Vector2 FeatureAnchor(
            CARegionalProjectionKernel kernel,
            CARegionalCandidateFacts.Feature feature)
        {
            return feature == null || !feature.Tile.Valid
                ? new Vector2(kernel.Center.x, kernel.Center.z)
                : AreaCentroid(kernel, feature.Tile.tileId);
        }

        private static Vector2 FeatureScreenPoint(Rect map,
            CARegionalProjectionKernel kernel, CARegionalCandidateFacts facts,
            CARegionalCandidateFacts.Feature feature)
        {
            Vector2 point = ToGui(map, kernel,
                FeatureAnchor(kernel, feature));
            if (facts == null || feature == null || !feature.Tile.Valid)
                return point;
            List<CARegionalCandidateFacts.Feature> siblings = facts.Features
                .Where(item => item != null && item.Tile.Valid
                    && item.Tile.tileId == feature.Tile.tileId)
                .OrderBy(item => item.Def?.defName ?? item.Name).ToList();
            if (siblings.Count <= 1) return point;
            int index = siblings.IndexOf(feature);
            float angle = (-90f + 360f * index / siblings.Count)
                * Mathf.Deg2Rad;
            const float radius = 12f;
            point += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            point.x = Mathf.Clamp(point.x, map.x + 7f, map.xMax - 7f);
            point.y = Mathf.Clamp(point.y, map.y + 38f, map.yMax - 7f);
            return point;
        }

        private static void DrawNeighbors(Rect map,
            CARegionalProjectionKernel kernel, CARegionalCandidateFacts facts)
        {
            if (facts == null) return;
            foreach (CARegionalCandidateFacts.Neighbor neighbor in
                facts.Neighbors)
            {
                Color color = neighbor.Faction?.Color
                    ?? new Color(0.7f, 0.7f, 0.7f);
                Vector2 point;
                float size;
                if (neighbor.InFrame)
                {
                    point = ToGui(map, kernel, neighbor.Point);
                    size = 9f;
                }
                else
                {
                    // Outside the frame it still has a direction and a
                    // distance, so it is pinned to the edge on its true
                    // bearing instead of being dropped.
                    Vector2 raw = ToGui(map, kernel, neighbor.Point);
                    point = new Vector2(
                        Mathf.Clamp(raw.x, map.x + 5f, map.xMax - 5f),
                        Mathf.Clamp(raw.y, map.y + 5f, map.yMax - 5f));
                    size = 7f;
                }
                // Settlements everywhere in CA read as squares; an outside
                // settlement is the same mark in its faction's color, dimmed
                // when it is only edge-pinned on its true bearing.
                Rect glyph = new Rect(point.x - size * 0.5f,
                    point.y - size * 0.5f, size, size);
                Widgets.DrawBoxSolid(glyph.ExpandedBy(2f),
                    new Color(0.05f, 0.06f, 0.07f,
                        neighbor.InFrame ? 0.9f : 0.6f));
                Widgets.DrawBoxSolid(glyph, neighbor.InFrame
                    ? color : new Color(color.r, color.g, color.b, 0.6f));
                TooltipHandler.TipRegion(glyph.ExpandedBy(4f),
                    (neighbor.Object.LabelCap.NullOrEmpty()
                        ? neighbor.Object.def?.LabelCap.ToString()
                        : neighbor.Object.LabelCap)
                    + (neighbor.Faction != null
                        ? "\n" + neighbor.Faction.Name : "")
                    + "\n" + neighbor.WorldDistanceTiles.ToString("F0")
                    + " tiles away, outside this region"
                    + (neighbor.InFrame
                        ? " but inside the projected frame." : "."));
            }
        }

        private static void DrawSettlementAreaLabels(Rect map,
            CARegionalProjectionKernel kernel, CARegionalPlan plan)
        {
            // Settlement names sit on the largest connected visible land mass
            // of their assigned area. They are labels, not promised map cells.
            int emphasize = emphasisFactionKey >= 0 ? emphasisFactionKey
                : selectedKind == CARegionSelectionKind.Faction
                    ? selectedFactionKey : -1;
            emphasisFactionKey = -1; // consumed; panels re-set each frame
            foreach (IGrouping<int, CARegionalSettlementPlan> group in plan.settlements
                .Where(settlement => settlement != null)
                .GroupBy(settlement => settlement.memberTileId))
            {
                List<CARegionalSettlementPlan> settlements = group
                    .OrderBy(settlement => settlement.slot).ToList();
                bool emphasized = settlements.Any(settlement => emphasize >= 0
                    && settlement.OwningFactionKey == emphasize);

                Vector2 point = ToGui(map, kernel,
                    AreaCentroid(kernel, group.Key));
                int arrivalOffset = group.Key == plan.startTileId ? 1 : 0;
                Text.Font = GameFont.Tiny;
                float maxBadgeWidth = Mathf.Max(82f,
                    Mathf.Min(320f, map.width * 0.62f));
                var badgeHeights = settlements.Select(settlement =>
                    Mathf.Max(22f, Text.CalcHeight(
                        CARegionalPlanUtility.SettlementName(plan, settlement),
                        maxBadgeWidth - 12f) + 6f)).ToList();
                float arrivalHeight = arrivalOffset > 0 ? 24f : 0f;
                float blockHeight = arrivalHeight + badgeHeights.Sum()
                    + Mathf.Max(0, badgeHeights.Count - 1) * 2f;
                float blockY = Mathf.Clamp(point.y - blockHeight * 0.5f,
                    map.y + 36f, map.yMax - blockHeight - 4f);
                float badgeY = blockY + arrivalHeight;
                for (int i = 0; i < settlements.Count; i++)
                {
                    CARegionalSettlementPlan settlement = settlements[i];
                    string label = CARegionalPlanUtility.SettlementName(plan,
                        settlement);
                    bool selected = selectedKind
                        == CARegionSelectionKind.Settlement
                        && selectedSlot == settlement.slot;
                    bool hovered = hoveredSlot == settlement.slot;
                    Text.Font = GameFont.Tiny;
                    float width = Mathf.Clamp(Text.CalcSize(label).x + 16f,
                        82f, maxBadgeWidth);
                    Rect badge = new Rect(point.x - width * 0.5f,
                        badgeY, width, badgeHeights[i]);
                    badge.x = Mathf.Clamp(badge.x, map.x + 4f,
                        map.xMax - badge.width - 4f);
                    labelHits.Add(new LabelHit
                    {
                        Kind = LabelHitKind.Settlement,
                        SettlementSlot = settlement.slot,
                        Rect = badge
                    });
                    if (Mouse.IsOver(badge))
                    {
                        hoveredSlot = settlement.slot;
                        hovered = true;
                    }
                    Widgets.DrawBoxSolid(badge, selected || hovered
                        ? new Color(0.12f, 0.16f, 0.17f, 0.96f)
                        : emphasized
                            ? new Color(0.07f, 0.10f, 0.11f, 0.94f)
                            : new Color(0.035f, 0.05f, 0.06f, 0.84f));
                    if (selected) Widgets.DrawBox(badge, 2);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(badge, label);
                    Text.Anchor = TextAnchor.UpperLeft;
                    TooltipHandler.TipRegion(badge,
                        "Click to select this settlement. Assigned area: "
                        + CARegionalPlanUtility.TileWords(settlement.memberTileId)
                        + ". Exact site is chosen from generated terrain.");
                    badgeY += badge.height + 2f;
                }
                Text.Font = GameFont.Small;
            }
        }

        // Connections need a schematic endpoint. This is explicitly an area
        // centroid, never a promised settlement coordinate.
        private static Vector2 SettlementPoint(CARegionalProjectionKernel kernel,
            CARegionalPlan plan, CARegionalSettlementPlan settlement)
        {
            return AreaCentroid(kernel, settlement.memberTileId);
        }

        internal static Vector2 AreaCentroid(
            CARegionalProjectionKernel kernel, int memberTileId)
        {
            return kernel.VisualLandAnchor(memberTileId);
        }

        // The arrival is a point-of-entry mark ON the region - a ringed
        // point with a small name - not a floating dialog chip. It marks
        // the broad area; the exact cell is chosen from generated terrain.
        private static void DrawLanding(Rect map,
            CARegionalProjectionKernel kernel, CARegionalPlan plan)
        {
            PlanetTile landing = plan.StartTile;
            if (!landing.Valid) return;
            Vector2 point = ToGui(map, kernel,
                AreaCentroid(kernel, landing.tileId));
            point.x = Mathf.Clamp(point.x, map.x + 14f, map.xMax - 14f);
            point.y = Mathf.Clamp(point.y, map.y + 40f, map.yMax - 26f);
            bool selected = selectedKind == CARegionSelectionKind.Arrival;
            Color mark = selected
                ? new Color(0.98f, 0.97f, 0.92f)
                : new Color(0.88f, 0.87f, 0.80f);
            var halo = new Color(0.05f, 0.06f, 0.07f, 0.85f);
            const float r = 8f;
            const int segments = 14;
            Vector2 previous = point + new Vector2(r, 0f);
            for (int s = 1; s <= segments; s++)
            {
                float angle = s * Mathf.PI * 2f / segments;
                Vector2 next = point + new Vector2(
                    Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
                Widgets.DrawLine(previous, next, halo, 4f);
                Widgets.DrawLine(previous, next, mark,
                    selected ? 2.4f : 1.8f);
                previous = next;
            }
            Widgets.DrawBoxSolid(
                new Rect(point.x - 2f, point.y - 2f, 4f, 4f), mark);
            Text.Font = GameFont.Tiny;
            Vector2 nameSize = Text.CalcSize("Arrival");
            var label = new Rect(point.x - (nameSize.x + 8f) * 0.5f,
                point.y + r + 3f, nameSize.x + 8f, 15f);
            label.x = Mathf.Clamp(label.x, map.x + 2f,
                map.xMax - label.width - 2f);
            Widgets.DrawBoxSolid(label, new Color(0.05f, 0.06f, 0.07f,
                selected ? 0.9f : 0.72f));
            GUI.color = mark;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(label, "Arrival");
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            var hit = new Rect(point.x - r - 4f, point.y - r - 4f,
                (r + 4f) * 2f, (r + 4f) * 2f + 18f);
            labelHits.Add(new LabelHit
            {
                Kind = LabelHitKind.Arrival,
                SettlementSlot = -1,
                Rect = hit
            });
            if (Mouse.IsOver(hit))
                TooltipHandler.TipRegion(hit, "Where the arriving party "
                    + "enters this region. Click to select; the exact "
                    + "arrival cell is chosen from generated terrain.");
        }

        // Selection and placement, resolved in one pass and in one order:
        // an armed area assignment wins outright, then explicit feature and
        // neighbor glyphs, then settlements belonging to the clicked area.
        // Open ground returns the subject to the region itself.
        private static void HandleClicks(Rect map,
            CARegionalProjectionKernel kernel, CARegionalPlan plan,
            CARegionalCandidateFacts facts, Action changed)
        {
            if (Event.current.type != EventType.MouseDown
                || Event.current.button != 0
                || !map.Contains(Event.current.mousePosition)) return;
            Vector2 mouse = Event.current.mousePosition;

            if (awaitingSlot >= 0)
            {
                // The diagram is an area-assignment surface. The click asks
                // which broad world area owns the sampled cell; it never
                // assigns an exact generated-map coordinate.
                int tileId = TileUnderMouse(map, kernel);
                CARegionalSettlementPlan moving = plan.settlements.FirstOrDefault(
                    item => item != null && item.slot == awaitingSlot);
                if (moving != null && tileId >= 0
                    && plan.memberTileIds.Contains(tileId))
                {
                    if (tileId == plan.startTileId)
                    {
                        Messages.Message("The arrival area occupies this "
                            + "ground. Move the arrival first, then place "
                            + "the settlement here.",
                            MessageTypeDefOf.RejectInput, false);
                    }
                    else if (moving.memberTileId != tileId)
                    {
                        if (!CAHabitatViability.CanPotentiallySettle(plan,
                                moving, tileId,
                                out string habitatFailure))
                            Messages.Message(habitatFailure,
                                MessageTypeDefOf.RejectInput, false);
                        else
                        {
                            ReopenForMapAuthoring(plan);
                            moving.siteClusterKey = moving.slot;
                            moving.memberTileId = tileId;
                            changed?.Invoke();
                            Messages.Message(
                                CARegionalPlanUtility.SettlementName(plan,
                                    moving) + " now belongs to "
                                    + CARegionalPlanUtility.TileWords(tileId)
                                    + ".",
                                MessageTypeDefOf.TaskCompletion, false);
                        }
                    }
                }
                else if (tileId >= 0)
                    Messages.Message("That ground belongs to "
                        + CARegionalPlanUtility.TileWords(tileId)
                        + ", which is outside this region's footprint.",
                        MessageTypeDefOf.RejectInput, false);
                else
                    Messages.Message("Choose visible land for this "
                        + "settlement's broad area.",
                        MessageTypeDefOf.RejectInput, false);
                awaitingSlot = -1;
                Event.current.Use();
                return;
            }

            if (awaitingArrivalArea)
            {
                int tileId = TileUnderMouse(map, kernel);
                if (tileId < 0 || !plan.memberTileIds.Contains(tileId))
                    Messages.Message("Choose visible land inside this "
                        + "region for the arrival area.",
                        MessageTypeDefOf.RejectInput, false);
                else if (plan.settlements.Any(item => item != null
                    && item.memberTileId == tileId))
                    Messages.Message("That ground already contains a "
                        + "settlement. Move the settlement first, then place "
                        + "the arrival here.",
                        MessageTypeDefOf.RejectInput, false);
                else
                {
                    // Arrival consumes the region; the one owner of that
                    // write is TrySetArrival. A confirmed plan reopens for
                    // authoring first because its realization was accepted
                    // against the previous arrival.
                    bool moving = plan.startTileId != tileId;
                    if (moving) ReopenForMapAuthoring(plan);
                    if (CARegionalSetupSession.TrySetArrival(plan, tileId,
                            out string arrivalRefusal))
                    {
                        if (moving) changed?.Invoke();
                        SelectArrival(tileId);
                        Messages.Message("Arrival area: "
                            + CARegionalPlanUtility.TileSummary(tileId),
                            MessageTypeDefOf.TaskCompletion, false);
                    }
                    else
                        Messages.Message(arrivalRefusal,
                            MessageTypeDefOf.RejectInput, false);
                }
                awaitingArrivalArea = false;
                Event.current.Use();
                return;
            }

            // The visible object itself wins over the broad area behind it.
            // This matters when a label is clamped away from its centroid or
            // several labels share one constituent.
            for (int i = labelHits.Count - 1; i >= 0; i--)
            {
                LabelHit hit = labelHits[i];
                if (!hit.Rect.Contains(mouse)) continue;
                if (hit.Kind == LabelHitKind.Arrival)
                {
                    if (selectedKind == CARegionSelectionKind.Arrival)
                        SelectRegion();
                    else SelectArrival(plan.startTileId);
                }
                else if (selectedKind == CARegionSelectionKind.Settlement
                    && selectedSlot == hit.SettlementSlot)
                    SelectRegion();
                else SelectSettlement(hit.SettlementSlot);
                Event.current.Use();
                return;
            }

            if (facts != null)
            {
                foreach (CARegionalCandidateFacts.Feature feature in
                    facts.Features)
                {
                    Vector2 point = FeatureScreenPoint(map, kernel, facts,
                        feature);
                    if ((point - mouse).sqrMagnitude > 81f) continue;
                    SelectFeature(feature.Tile.tileId,
                        feature.Def?.defName);
                    Event.current.Use();
                    return;
                }
                foreach (CARegionalCandidateFacts.Neighbor neighbor in
                    facts.Neighbors)
                {
                    Vector2 raw = ToGui(map, kernel, neighbor.Point);
                    Vector2 point = neighbor.InFrame ? raw
                        : new Vector2(
                            Mathf.Clamp(raw.x, map.x + 5f, map.xMax - 5f),
                            Mathf.Clamp(raw.y, map.y + 5f, map.yMax - 5f));
                    if ((point - mouse).sqrMagnitude > 64f) continue;
                    SelectNeighbor(neighbor.Tile.tileId);
                    Event.current.Use();
                    return;
                }
            }

            int clickedTileId = TileUnderMouse(map, kernel);
            List<CARegionalSettlementPlan> settlements = plan.settlements.Where(
                settlement => settlement != null
                    && settlement.memberTileId == clickedTileId)
                .OrderBy(settlement => settlement.slot).ToList();
            if (settlements.Count == 1)
            {
                CARegionalSettlementPlan settlement = settlements[0];
                if (selectedKind == CARegionSelectionKind.Settlement
                    && selectedSlot == settlement.slot) SelectRegion();
                else SelectSettlement(settlement.slot);
                Event.current.Use();
                return;
            }

            if (settlements.Count > 1)
            {
                var options = new List<FloatMenuOption>();
                foreach (CARegionalSettlementPlan item in settlements)
                {
                    CARegionalSettlementPlan settlement = item;
                    options.Add(new FloatMenuOption(
                        CARegionalPlanUtility.SettlementName(plan, settlement),
                        delegate { SelectSettlement(settlement.slot); }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
                Event.current.Use();
                return;
            }

            if (clickedTileId == plan.startTileId)
            {
                SelectArrival(plan.startTileId);
                Event.current.Use();
                return;
            }

            SelectRegion();
            Event.current.Use();
        }

        // Returning to this page after a previous confirmation reopens the
        // candidate before any causal input changes. SavePending can then
        // realize the new draft once; a confirmed plan is never mutated while
        // retaining results derived from its former settlement geography.
        private static void ReopenForMapAuthoring(CARegionalPlan plan)
        {
            if (plan == null) return;
            plan.confirmed = false;
            plan.developerExercise = false;
            plan.operatorAuthored = true;
            CARegionalSettlements.Invalidate(plan);
        }

        internal static int TileUnderMouse(Rect map,
            CARegionalProjectionKernel kernel)
        {
            Vector2 mouse = Event.current.mousePosition;
            if (!map.Contains(mouse)) return -1;
            int x = Mathf.Clamp(Mathf.FloorToInt(
                (mouse.x - map.x) / map.width * kernel.Size.x), 0,
                kernel.Size.x - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(
                (1f - (mouse.y - map.y) / map.height) * kernel.Size.z), 0,
                kernel.Size.z - 1);
            int index = kernel.Indices.CellToIndex(new IntVec3(x, 0, z));
            if (!kernel.IsVisualLandAtIndex(index)) return -1;
            PlanetTile tile = kernel.MemberTileAtIndex(index);
            return tile.Valid ? tile.tileId : -1;
        }

        // ---- the strip under the map ---------------------------------------

        private static bool ShowPlaceSettlement(Action placeSettlement)
        {
            return placeSettlement != null && awaitingSlot < 0
                && !awaitingArrivalArea
                && (selectedKind == CARegionSelectionKind.Region
                    || selectedKind == CARegionSelectionKind.Faction);
        }

        private static float ContextActionWidth(Action viewDetails,
            Action placeSettlement)
        {
            bool move = selectedKind == CARegionSelectionKind.Settlement
                || selectedKind == CARegionSelectionKind.Arrival
                || awaitingSlot >= 0 || awaitingArrivalArea;
            bool details = selectedKind != CARegionSelectionKind.Region
                && viewDetails != null;
            bool place = ShowPlaceSettlement(placeSettlement);
            int count = (move ? 1 : 0) + (details ? 1 : 0)
                + (place ? 1 : 0);
            if (count == 0) return 0f;
            return (move ? 104f : 0f) + (details ? 104f : 0f)
                + (place ? 126f : 0f) + (count - 1) * 6f;
        }

        private static void DrawContextStrip(Rect strip, CARegionalPlan plan,
            string text, Action viewDetails, Action placeSettlement)
        {
            Text.Font = GameFont.Tiny;
            bool hasFactionMarker = selectedKind
                    == CARegionSelectionKind.Settlement
                || selectedKind == CARegionSelectionKind.Faction;
            float markerWidth = hasFactionMarker ? 10f : 0f;
            if (hasFactionMarker)
            {
                int factionKey = selectedKind == CARegionSelectionKind.Faction
                    ? selectedFactionKey : plan?.settlements?.FirstOrDefault(
                            item => item != null && item.slot == selectedSlot)
                        ?.OwningFactionKey ?? -1;
                Widgets.DrawBoxSolid(new Rect(strip.x, strip.y + 3f, 4f,
                    Mathf.Max(4f, strip.height - 6f)),
                    CARegionalWorldOverlay.FactionColor(factionKey));
            }
            bool move = selectedKind == CARegionSelectionKind.Settlement
                || selectedKind == CARegionSelectionKind.Arrival
                || awaitingSlot >= 0 || awaitingArrivalArea;
            bool details = selectedKind != CARegionSelectionKind.Region
                && viewDetails != null;
            bool place = ShowPlaceSettlement(placeSettlement);
            float actionsWidth = ContextActionWidth(viewDetails,
                placeSettlement);
            GUI.color = CAOpeningTheme.TextLo;
            Rect textRect = new Rect(strip.x + markerWidth, strip.y,
                strip.width - markerWidth - actionsWidth
                    - (actionsWidth > 0f ? 6f : 0f),
                strip.height);
            Widgets.Label(textRect, text);
            GUI.color = Color.white;

            float right = strip.xMax;
            if (details)
            {
                Rect detailsRect = new Rect(right - 104f, strip.y, 104f,
                    strip.height);
                if (CAOpeningTheme.GhostButton(detailsRect, "View details"))
                    viewDetails();
                right = detailsRect.x - 6f;
            }
            if (move)
            {
                bool movingSettlement = awaitingSlot >= 0;
                bool movingArrival = awaitingArrivalArea;
                string label = movingSettlement || movingArrival
                    ? "Cancel move"
                    : selectedKind == CARegionSelectionKind.Arrival
                        ? "Move arrival" : "Move area";
                Rect moveRect = new Rect(right - 104f, strip.y, 104f,
                    strip.height);
                if (CAOpeningTheme.GhostButton(moveRect, label))
                {
                    if (movingSettlement || movingArrival)
                    {
                        awaitingSlot = -1;
                        awaitingArrivalArea = false;
                    }
                    else if (selectedKind == CARegionSelectionKind.Arrival)
                    {
                        awaitingArrivalArea = true;
                        awaitingSlot = -1;
                    }
                    else if (selectedKind
                        == CARegionSelectionKind.Settlement)
                    {
                        awaitingSlot = selectedSlot;
                        awaitingArrivalArea = false;
                    }
                }
                TooltipHandler.TipRegion(moveRect,
                    selectedKind == CARegionSelectionKind.Arrival
                        || movingArrival
                        ? "Choose another unoccupied area inside this region."
                        : "Choose the broad area this settlement belongs to. "
                            + "Its exact site is chosen from generated terrain.");
                right = moveRect.x - 6f;
            }
            if (place)
            {
                Rect placeRect = new Rect(right - 126f, strip.y, 126f,
                    strip.height);
                if (CAOpeningTheme.GhostButton(placeRect,
                        "Place settlement"))
                    placeSettlement();
                TooltipHandler.TipRegion(placeRect, "Choose an existing "
                    + "faction or a starting society, then assign the new "
                    + "settlement to a broad area on this map.");
            }
            TooltipHandler.TipRegion(textRect, text);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        internal static string ContextStripText(CARegionalPlan plan,
            CARegionalCandidateFacts facts)
        {
            string text;
            if (awaitingSlot >= 0)
                text = "Choose the broad area this settlement belongs to.";
            else if (awaitingArrivalArea)
                text = "Choose unoccupied land inside this region for arrival.";
            else if (selectedKind == CARegionSelectionKind.Arrival)
                text = "Arrival area: "
                    + CARegionalPlanUtility.TileWords(plan.startTileId)
                    + "; the exact arrival cell is chosen from generated terrain.";
            else if (selectedKind == CARegionSelectionKind.Settlement)
            {
                CARegionalSettlementPlan settlement = plan.settlements
                    .FirstOrDefault(item => item != null
                        && item.slot == selectedSlot);
                text = settlement == null ? "Settlement"
                    : "Settlement: "
                        + CARegionalPlanUtility.SettlementName(plan, settlement)
                        + " - assigned to "
                        + CARegionalPlanUtility.TileWords(settlement.memberTileId)
                        + "; its exact site is chosen from generated terrain.";
            }
            else if (selectedKind == CARegionSelectionKind.Faction)
            {
                CARegionalFactionPlan faction = plan.FactionPlan(
                    selectedFactionKey);
                int held = plan.settlements.Count(item => item != null
                    && item.OwningFactionKey == selectedFactionKey);
                text = faction == null ? "Faction"
                    : "Faction - " + CARegionalPlanUtility.FactionName(faction)
                        + " - " + held + " settlement"
                        + (held == 1 ? "" : "s");
            }
            else if (selectedKind == CARegionSelectionKind.Feature)
            {
                CARegionalCandidateFacts.Feature feature = facts?.Features
                    .FirstOrDefault(item => item != null
                        && item.Tile.tileId == selectedTileId
                        && (selectedFeatureDefName.NullOrEmpty()
                            || item.Def?.defName == selectedFeatureDefName));
                text = feature == null ? "Selected feature"
                    : feature.Name + " - "
                        + CARegionalPlanUtility.TileWords(
                            feature.Tile.tileId);
            }
            else if (selectedKind == CARegionSelectionKind.Neighbor)
            {
                CARegionalCandidateFacts.Neighbor neighbor = facts?.Neighbors
                    .FirstOrDefault(item => item != null
                        && item.Tile.tileId == selectedTileId);
                text = neighbor?.Object == null ? "Selected neighboring site"
                    : neighbor.Object.LabelCap + " - "
                        + neighbor.WorldDistanceTiles.ToString("F0")
                        + " tiles from the region";
            }
            else if (overlayMode == 2)
                text = "Connections are schematic links between assigned "
                    + "areas; they are not roads or exact travel paths.";
            else
            {
                var parts = new List<string>
                {
                    CARegionalPlanUtility.RegionName(plan),
                    plan.factions.Count + " faction"
                        + (plan.factions.Count == 1 ? "" : "s"),
                    plan.settlements.Count + " settlement"
                        + (plan.settlements.Count == 1 ? "" : "s")
                };
                if (facts != null)
                {
                    if (facts.Roads.Count > 0)
                        parts.Add(facts.Roads.Count + " road link"
                            + (facts.Roads.Count == 1 ? "" : "s"));
                    if (facts.Rivers.Count > 0)
                        parts.Add(facts.Rivers.Count + " river link"
                            + (facts.Rivers.Count == 1 ? "" : "s"));
                }
                text = string.Join(" - ", parts.ToArray());
            }
            return text;
        }

    }
}
