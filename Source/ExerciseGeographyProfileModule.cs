using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // The benchmark fixture chose its landing tile with
    // ChooseRandomStartingTile under a fixed seed. That is deterministic but
    // geographically arbitrary, and the tile it lands on is inland arid
    // shrubland and grassland: no ocean neighbour, no river, no relief. Every
    // run therefore exercised one field -- flat inland -- while the projection
    // kernel's coast, littoral, water-depth, atoll, cove, archipelago and
    // river-corridor paths were never entered, and never appeared in any
    // performance figure either.
    //
    // Geography is a fixture AXIS, not a property of the world seed. This
    // selects a landing tile that actually carries the requested geography, so
    // a peninsula run generates a peninsula and a river run carries a river.
    // Selection is deterministic: tiles are scanned in a fixed seeded order
    // and the first qualifying tile wins, so a named profile always reproduces
    // the same landing.
    internal static class CAExerciseGeography
    {
        // Requested profile, parsed from the arming value's @suffix.
        internal static string Requested;

        // What the selection actually found, for the receipt. A run must state
        // the geography it was taken under or its numbers cannot be compared
        // against a run taken under different geography.
        internal static string Resolved = "unselected";

        internal const string Profiles =
            "coast, peninsula, island, river, mountain, inland";

        // A region spans RegionTiles source tiles, so the geography has to
        // hold across the neighbourhood rather than at a single tile, or the
        // projection samples a feature the region does not actually contain.
        internal static bool TrySelect(int regionTiles, out PlanetTile chosen)
        {
            chosen = PlanetTile.Invalid;
            if (string.IsNullOrEmpty(Requested)) return false;
            WorldGrid grid = Find.WorldGrid;
            if (grid == null) return false;

            int count = grid.TilesCount;
            var neighbours = new List<PlanetTile>();
            // Fixed stride scan: deterministic, and spreads sampling across the
            // planet instead of clustering at tile 0.
            int stride = Math.Max(1, count / 4096);
            PlanetTile best = PlanetTile.Invalid;
            int bestScore = int.MinValue;

            for (int step = 0; step < count; step++)
            {
                int id = (step * stride + step) % count;
                PlanetTile tile = new PlanetTile(id, grid.Surface);
                if (!tile.Valid) continue;
                Tile info = grid[tile];
                if (info == null || info.WaterCovered) continue;
                if (!TileFinder.IsValidTileForNewSettlement(tile, null))
                    continue;

                grid.GetTileNeighbors(tile, neighbours);
                int water = 0;
                for (int i = 0; i < neighbours.Count; i++)
                {
                    Tile n = grid[neighbours[i]];
                    if (n != null && n.WaterCovered) water++;
                }
                var surface = info as SurfaceTile;
                bool river = surface?.Rivers != null && surface.Rivers.Count > 0;
                Hilliness hill = info.hilliness;

                int score = Score(Requested, water, neighbours.Count, river,
                    hill);
                if (score <= bestScore) continue;
                bestScore = score;
                best = tile;
                // A perfect match ends the scan; anything less keeps looking
                // for a better one rather than settling for the first hit.
                if (score >= 1000) break;
            }

            if (bestScore <= 0) return false;
            chosen = best;
            Tile picked = grid[chosen];
            grid.GetTileNeighbors(chosen, neighbours);
            int pickedWater = neighbours.Count(n =>
                grid[n] != null && grid[n].WaterCovered);
            var pickedSurface = picked as SurfaceTile;
            Resolved = Requested + " @tile " + chosen.tileId
                + " (biome " + picked.PrimaryBiome?.defName
                + "; hilliness " + picked.hilliness
                + "; water neighbours " + pickedWater + "/" + neighbours.Count
                + "; rivers " + (pickedSurface?.Rivers?.Count ?? 0)
                + "; elevation " + picked.elevation.ToString("F0") + "m)";
            return true;
        }

        // Scores are deliberately coarse. 1000+ is an unambiguous match and
        // ends the scan; a positive score below that is an acceptable
        // fallback; zero or less is a rejection.
        private static int Score(string profile, int water, int neighbours,
            bool river, Hilliness hill)
        {
            switch (profile)
            {
                case "coast":
                    // One or two water neighbours: land that meets the sea
                    // along an edge rather than being surrounded by it.
                    if (water == 0) return 0;
                    return water <= 2 ? 1000 + water : 500;
                case "peninsula":
                    // Water on most sides but still joined to the mainland.
                    if (water < 3 || water >= neighbours) return 0;
                    return 1000 + water;
                case "island":
                    // Joined to nothing: every neighbour is water.
                    return water >= neighbours && neighbours > 0 ? 1000 : 0;
                case "river":
                    if (!river) return 0;
                    // A river reaching the sea is the richer case: it exercises
                    // the corridor carve AND the littoral path together.
                    return water > 0 ? 1000 : 900;
                case "mountain":
                    if (hill == Hilliness.Impassable) return 900;
                    if (hill == Hilliness.Mountainous) return 1000;
                    if (hill == Hilliness.LargeHills) return 400;
                    return 0;
                case "inland":
                    // The control: the field every previous run was taken on.
                    return water == 0 && !river
                        && (hill == Hilliness.Flat || hill == Hilliness.SmallHills)
                        ? 1000 : 0;
                default:
                    return 0;
            }
        }
    }
}
