using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // One selected region is a composition, not a named map type. This contract
    // records every independent geographic input that the selection flow can
    // combine, gives that exact composition a stable identity, and validates
    // the shared projection before either preview or generation may claim it.
    // Nothing here is serialized: the world tiles and the saved plan remain the
    // authority, and this object is derived from them on every boundary.
    internal sealed class CARegionalGeographyComposition
    {
        internal const int Schema = 1;
        // PRESETS, NOT THE ONTOLOGY. These are quick sizes, tested
        // defaults, regression fixtures, and snapping targets for
        // auto-derived prospects. A valid region is any connected selected
        // world geography within the supported range below -- a long
        // valley, an island chain, a coastal strip, an irregular frontier
        // -- and no consumer may treat this preset list as the definition
        // of a valid extent.
        internal static readonly int[] SupportedExtents = { 4, 6, 8, 10, 12 };

        // The canonical extent range. The floor keeps a region from being
        // one lone tile pretending to be a region; the ceiling bounds the
        // aggregate backing map the engine must hold.
        internal const int MinExtent = 2;
        internal const int MaxExtent = 16;

        internal string Signature;
        internal string Canonical;
        internal int RequestedExtent;
        internal int ActualExtent;
        internal int Orientation;
        internal int ArrivalTileId;
        internal IntVec3 BackingSize;
        internal readonly List<string> Biomes = new List<string>();
        internal readonly List<string> Relief = new List<string>();
        internal readonly List<string> BoundaryWater = new List<string>();
        internal readonly List<string> Roads = new List<string>();
        internal readonly List<string> Rivers = new List<string>();
        internal readonly List<string> Rocks = new List<string>();
        internal readonly List<string> Features = new List<string>();
        internal readonly List<string> Mutators = new List<string>();
        internal readonly List<string> Failures = new List<string>();
        internal CACompatibilityReport Compatibility;

        internal bool IsValid => Failures.Count == 0;

        internal string PlayerSummary()
        {
            var parts = new List<string>();
            if (BoundaryWater.Count > 0)
                parts.Add(string.Join("/", BoundaryWater.Select(LabelWords)
                    .Distinct().OrderBy(value => value,
                        StringComparer.Ordinal)));
            if (Roads.Count > 0)
            {
                int count = Roads.Distinct().Count();
                parts.Add(count + " road link" + (count == 1 ? "" : "s"));
            }
            if (Rivers.Count > 0)
            {
                int count = Rivers.Distinct().Count();
                parts.Add(count + " river link" + (count == 1 ? "" : "s"));
            }
            if (Features.Count > 0)
                parts.Add(Features.Count + " feature" + (Features.Count == 1 ? "" : "s"));
            if (parts.Count == 0) parts.Add("no coast, route, or feature");
            return string.Join(" · ", parts);
        }

        internal string GenerationWords()
        {
            if (!IsValid)
                return "cannot generate: " + Failures.First();
            if (Compatibility?.IsValid == false)
            {
                int defs = Compatibility.Blocking.Select(entry => entry.DefName)
                    .Distinct().Count();
                int obligations = Compatibility.Blocking
                    .SelectMany(entry => entry.Unresolved).Count();
                return "blocked: " + defs + " feature"
                    + (defs == 1 ? "" : "s") + " have " + obligations
                    + " unresolved generation obligation"
                    + (obligations == 1 ? "" : "s");
            }
            return "ready";
        }

        internal string Tooltip()
        {
            var lines = new List<string>
            {
                "Composition " + Signature,
                "Areas " + ActualExtent + " of " + RequestedExtent
                    + "; orientation " + (Orientation + 1) + "/6"
                    + "; arrival tile " + ArrivalTileId,
                "Biomes: " + WordsOrNone(Biomes),
                "Relief: " + WordsOrNone(Relief),
                "Water context: " + WordsOrNone(BoundaryWater),
                "Roads: " + WordsOrNone(Roads),
                "Rivers: " + WordsOrNone(Rivers),
                "Stone: " + WordsOrNone(Rocks),
                "Features: " + WordsOrNone(Features),
                "Generation: " + GenerationWords()
            };
            return string.Join("\n", lines);
        }

        private static string WordsOrNone(IEnumerable<string> values)
        {
            List<string> list = values.Where(value => !value.NullOrEmpty())
                .Distinct().OrderBy(value => value, StringComparer.Ordinal)
                .Select(LabelWords).ToList();
            return list.Count == 0 ? "none" : string.Join(", ", list);
        }

        private static string LabelWords(string value)
        {
            return value.NullOrEmpty() ? "none"
                : value.Replace("_", " ").CapitalizeFirst();
        }
    }

    internal static class CARegionalGeographyContract
    {
        internal static CARegionalGeographyComposition Inspect(
            CARegionalPlan plan, CARegionalProjectionKernel kernel = null)
        {
            var result = new CARegionalGeographyComposition();
            if (plan == null)
            {
                result.Failures.Add("the regional plan is missing");
                result.Canonical = "schema=1|missing-plan";
                result.Signature = Digest(result.Canonical);
                return result;
            }

            result.RequestedExtent = plan.RequestedRegionTileCount;
            result.ActualExtent = plan.memberTileIds?.Count ?? 0;
            result.Orientation = plan.FootprintRotation;
            result.ArrivalTileId = plan.startTileId;
            result.BackingSize = plan.BackingMapSize;
            var canonical = new List<string>
            {
                "schema=" + CARegionalGeographyComposition.Schema,
                "scale=" + plan.mapSize,
                "extent=" + result.ActualExtent + "/" + result.RequestedExtent,
                "orientation=" + result.Orientation,
                "root=" + plan.bundleRootTileId,
                // Arrival is deliberately NOT part of this identity: the
                // composition describes the region's geography, and where
                // the arriving party enters consumes that geography without
                // changing it. Folding arrival in here made the preview
                // treat an arrival change as a new region. ArrivalTileId
                // stays available as descriptive data on the result.
                "backing=" + result.BackingSize.x + "x" + result.BackingSize.z
            };

            ValidateSelection(plan, result);
            foreach (int tileId in (plan.memberTileIds ?? new List<int>())
                .Distinct().OrderBy(value => value))
            {
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(tileId);
                if (!tile.Valid || tile.Tile == null)
                {
                    canonical.Add("member=" + tileId + ":invalid");
                    continue;
                }
                Tile info = tile.Tile;
                SurfaceTile surface = info as SurfaceTile;
                string biome = info.PrimaryBiome?.defName ?? "unknown";
                string relief = info.hilliness.ToString();
                result.Biomes.Add(biome);
                result.Relief.Add(relief);

                List<string> mutators = (info.Mutators
                        ?? Enumerable.Empty<TileMutatorDef>())
                    .Where(def => def != null).Select(def => def.defName)
                    .Distinct().OrderBy(value => value, StringComparer.Ordinal)
                    .ToList();
                result.Mutators.AddRange(mutators);
                result.Features.AddRange(mutators);

                Landmark landmark = null;
                try { landmark = info.Landmark; }
                catch { }
                string landmarkName = landmark?.def?.defName;
                if (!landmarkName.NullOrEmpty())
                    result.Features.Add(landmarkName);
                WorldFeature feature = info.feature;
                string worldFeature = feature?.name;
                string worldFeatureKey = feature == null ? null
                    : (feature.def?.defName ?? "unknown") + "#"
                        + feature.uniqueID;
                if (!worldFeature.NullOrEmpty())
                    result.Features.Add(worldFeature);
                else if (feature?.def != null)
                    result.Features.Add(feature.def.defName);

                List<string> rocks = Verse.Find.World
                    .NaturalRockTypesIn(tile).Where(def => def != null)
                    .Select(def => def.defName).Distinct()
                    .OrderBy(value => value, StringComparer.Ordinal).ToList();
                result.Rocks.AddRange(rocks);
                List<string> roads = surface?.Roads == null
                    ? new List<string>() : surface.Roads
                        .Where(link => link.road != null)
                        .Select(link => LinkKey(tileId, link.neighbor.tileId,
                            link.road.defName)).Distinct()
                        .OrderBy(value => value, StringComparer.Ordinal).ToList();
                List<string> rivers = surface?.Rivers == null
                    ? new List<string>() : surface.Rivers
                        .Where(link => link.river != null)
                        .Select(link => LinkKey(tileId, link.neighbor.tileId,
                            link.river.defName)).Distinct()
                        .OrderBy(value => value, StringComparer.Ordinal).ToList();
                result.Roads.AddRange(roads);
                result.Rivers.AddRange(rivers);

                canonical.Add("member=" + tileId + ":biome=" + biome
                    + ":relief=" + relief
                    + ":rocks=" + string.Join(",", rocks)
                    + ":roads=" + string.Join(",", roads)
                    + ":rivers=" + string.Join(",", rivers)
                    + ":mutators=" + string.Join(",", mutators)
                    + ":landmark=" + (landmarkName ?? "-")
                    + ":feature=" + (worldFeatureKey ?? "-"));
            }

            result.Biomes.Sort(StringComparer.Ordinal);
            result.Relief.Sort(StringComparer.Ordinal);
            result.Rocks.Sort(StringComparer.Ordinal);
            result.Roads.Sort(StringComparer.Ordinal);
            result.Rivers.Sort(StringComparer.Ordinal);
            result.Features.Sort(StringComparer.Ordinal);
            result.Mutators.Sort(StringComparer.Ordinal);
            result.Compatibility = CARegionalContentCompatibility.Evaluate(plan);

            // Boundary water is a selected-world fact, not an observation of
            // whichever resolution happened to build the projection. Derive
            // it once here so selection, preview, and generation produce the
            // same composition identity even when only some callers already
            // hold a kernel.
            List<PlanetTile> selectedBoundaryWater = BoundaryWaterFor(plan);
            foreach (PlanetTile water in selectedBoundaryWater)
            {
                string biome = water.Tile?.PrimaryBiome?.defName
                    ?? "unknown-water";
                result.BoundaryWater.Add(biome);
                canonical.Add("water=" + water.tileId + ":" + biome);
            }

            if (kernel != null)
                ValidateProjection(plan, kernel, selectedBoundaryWater,
                    result);

            result.BoundaryWater.Sort(StringComparer.Ordinal);
            // Authored feature shapes change the generated geography, so
            // they are part of the composition's identity everywhere
            // identity is compared (diagram cache, preview dedupe,
            // generation validation).
            foreach (string segment in
                CAFeatureShapeModel.CanonicalSegments(plan))
                canonical.Add(segment);
            result.Canonical = string.Join("|", canonical);
            result.Signature = Digest(result.Canonical);
            return result;
        }

        internal static bool TryValidate(CARegionalPlan plan,
            CARegionalProjectionKernel kernel, out string failure)
        {
            CARegionalGeographyComposition composition = Inspect(plan, kernel);
            failure = composition.IsValid ? null
                : string.Join("; ", composition.Failures.Distinct());
            return composition.IsValid;
        }

        private static void ValidateSelection(CARegionalPlan plan,
            CARegionalGeographyComposition result)
        {
            List<int> ids = plan.memberTileIds ?? new List<int>();
            if (ids.Count == 0)
            {
                result.Failures.Add("the selected region contains no land");
                return;
            }
            if (ids.Distinct().Count() != ids.Count)
                result.Failures.Add("the selected region repeats an area");
            if (plan.RequestedRegionTileCount
                    < CARegionalGeographyComposition.MinExtent
                || plan.RequestedRegionTileCount
                    > CARegionalGeographyComposition.MaxExtent)
                result.Failures.Add("the requested area count is outside "
                    + "the supported "
                    + CARegionalGeographyComposition.MinExtent + ".."
                    + CARegionalGeographyComposition.MaxExtent
                    + " area range");
            if (ids.Count > plan.RequestedRegionTileCount)
                result.Failures.Add("the realized region exceeds its "
                    + "requested area count");
            if (plan.footprintRotation < 0 || plan.footprintRotation > 5)
                result.Failures.Add("the orientation is outside the six "
                    + "world-grid headings");
            if (!ids.Contains(plan.bundleRootTileId))
                result.Failures.Add("the shape anchor is outside the region");
            if (!ids.Contains(plan.startTileId))
                result.Failures.Add("the arrival area is outside the region");
            if (plan.BackingMapSize.x <= 0 || plan.BackingMapSize.z <= 0)
                result.Failures.Add("the generated-map frame has no size");

            var valid = new HashSet<int>();
            foreach (int id in ids.Distinct())
            {
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(id);
                if (!tile.Valid || tile.Layer != Verse.Find.WorldGrid.Surface)
                    result.Failures.Add("area " + id
                        + " is not valid surface land");
                else if (CARegionalGeometry.IsBlocked(tile))
                    result.Failures.Add("area " + id
                        + " is water or impassable land");
                else valid.Add(id);
            }
            if (valid.Count != ids.Distinct().Count()) return;

            var reached = new HashSet<int>();
            var queue = new Queue<PlanetTile>();
            PlanetTile root = CARegionalPlanUtility.SurfaceTile(ids[0]);
            reached.Add(root.tileId);
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                PlanetTile current = queue.Dequeue();
                var neighbors = new List<PlanetTile>();
                current.Layer.GetTileNeighbors(current, neighbors);
                foreach (PlanetTile neighbor in neighbors)
                    if (neighbor.Valid && valid.Contains(neighbor.tileId)
                        && reached.Add(neighbor.tileId))
                        queue.Enqueue(neighbor);
            }
            if (reached.Count != valid.Count)
                result.Failures.Add("the selected areas are not one connected "
                    + "region");
        }

        private static void ValidateProjection(CARegionalPlan plan,
            CARegionalProjectionKernel kernel,
            IEnumerable<PlanetTile> selectedBoundaryWater,
            CARegionalGeographyComposition result)
        {
            if (kernel.MemberByCell == null
                || kernel.MemberByCell.Length == 0)
            {
                result.Failures.Add("the shared geographic projection is empty");
                return;
            }
            if ((plan.memberTileIds?.Count ?? 0) > 1 && !kernel.Active)
                result.Failures.Add("the shared geographic projection is not "
                    + "active for this multi-area region");

            var projectedWater = new HashSet<int>((kernel.BoundaryWaterTiles
                    ?? new List<PlanetTile>()).Where(tile => tile.Valid)
                .Select(tile => tile.tileId));
            foreach (PlanetTile water in selectedBoundaryWater
                ?? Enumerable.Empty<PlanetTile>())
                if (!projectedWater.Contains(water.tileId))
                    result.Failures.Add("boundary water area " + water.tileId
                        + " is absent from the shared projection");

            var visible = new Dictionary<int, int>();
            var core = new HashSet<int>(plan.memberTileIds
                ?? new List<int>());
            for (int index = 0; index < kernel.MemberByCell.Length; index++)
            {
                if (!kernel.IsVisualLandAtIndex(index)) continue;
                PlanetTile owner = kernel.MemberTileAtIndex(index);
                if (!owner.Valid || !core.Contains(owner.tileId)) continue;
                int count;
                visible.TryGetValue(owner.tileId, out count);
                visible[owner.tileId] = count + 1;
            }
            foreach (int tileId in core.OrderBy(value => value))
                if (!visible.ContainsKey(tileId))
                    result.Failures.Add("area " + tileId
                        + " has no visible land in the shared projection");

            int atolls = 0;
            int coves = 0;
            int archipelagos = 0;
            int coastalAreas = 0;
            foreach (int tileId in core)
            {
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(tileId);
                if (!tile.Valid) continue;
                bool coastal = false;
                var neighbors = new List<PlanetTile>();
                tile.Layer.GetTileNeighbors(tile, neighbors);
                coastal = neighbors.Any(neighbor => neighbor.Valid
                    && neighbor.Tile?.PrimaryBiome?.isWaterBiome == true);
                if (coastal) coastalAreas++;
                foreach (TileMutatorDef mutator in tile.Tile.Mutators
                    ?? Enumerable.Empty<TileMutatorDef>())
                {
                    if (mutator?.Worker is TileMutatorWorker_CoastalAtoll)
                        atolls++;
                    else if (mutator?.Worker is TileMutatorWorker_Cove)
                        coves++;
                    else if (mutator?.Worker is TileMutatorWorker_Archipelago)
                        archipelagos++;
                }
            }
            if (atolls != kernel.AtollCarrierCount)
                result.Failures.Add("atoll carrier count changed between the "
                    + "selected areas and their projection");
            if (coves != kernel.CoveCarrierCount)
                result.Failures.Add("cove carrier count changed between the "
                    + "selected areas and their projection");
            if (archipelagos != kernel.ArchipelagoCarrierCount)
                result.Failures.Add("archipelago carrier count changed between "
                    + "the selected areas and their projection");
            if (atolls > 0 && (kernel.AtollLagoonCells <= 0
                    || kernel.AtollRingLandCells <= 0))
                result.Failures.Add("an atoll lost its lagoon or ring land");
            if (coves > 0 && kernel.CoveWaterCells <= 0)
                result.Failures.Add("a cove produced no water");
            if (archipelagos > 0 && kernel.ArchipelagoWaterCells <= 0)
                result.Failures.Add("an archipelago produced no water");
            if (coastalAreas > 0 && kernel.WaterCells <= 0)
                result.Failures.Add("coastal source land produced no visible "
                    + "water context");
        }

        private static string LinkKey(int left, int right, string defName)
        {
            return Math.Min(left, right) + "-" + Math.Max(left, right)
                + ":" + (defName ?? "unknown");
        }

        private static List<PlanetTile> BoundaryWaterFor(CARegionalPlan plan)
        {
            var water = new Dictionary<int, PlanetTile>();
            foreach (int tileId in (plan?.memberTileIds ?? new List<int>())
                .Distinct())
            {
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(tileId);
                if (!tile.Valid) continue;
                var neighbors = new List<PlanetTile>();
                tile.Layer.GetTileNeighbors(tile, neighbors);
                foreach (PlanetTile neighbor in neighbors)
                    if (neighbor.Valid && neighbor.Tile?.PrimaryBiome
                            ?.isWaterBiome == true)
                        water[neighbor.tileId] = neighbor;
            }
            return water.Values.OrderBy(tile => tile.tileId).ToList();
        }

        // Stable FNV-1a over the canonical UTF-16 code units. This is an
        // identity receipt, not a random seed or a security primitive.
        private static string Digest(string canonical)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (char value in canonical ?? string.Empty)
                {
                    hash ^= value;
                    hash *= 1099511628211UL;
                }
                return hash.ToString("X16");
            }
        }
    }
}
