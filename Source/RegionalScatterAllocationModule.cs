using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Per-constituent count allocation for map-wide scatterers.
    //
    // Native computes ONE count for the whole map from the ROOT tile. On a
    // region that lets one constituent's factor scale content everywhere -
    // Junkyard carries junkDensityFactor 15. An area-weighted regional mean is
    // NOT the fix: it still raises junk on constituents carrying none. Each
    // constituent is allocated a count from its OWN area, biome and mutators,
    // and placement is constrained to the constituent that count belongs to.
    //
    // The counts are RECOMPUTED from the native pre-factor inputs
    // (countPer10kCellsRange / count / GetPlacementFactor / CountFromPer10kCells)
    // rather than derived by dividing an already-rounded native total. Dividing
    // the total leaves the root's biome and mutator factors baked into every
    // constituent and never substitutes the constituent's own biome.
    //
    // The two consumers do NOT share semantics and are computed separately:
    //   geysers - base count * map.Biome.geyserCountFactor * mutator
    //             geyserCountFactors. Count only.
    //   junk    - GetPlacementFactor feeds the count AND
    //             CalculateFinalMinSpacing (minSpacing / placementFactor), so
    //             junk also changes spacing. Spacing stays native here; only
    //             count and placement are per-constituent.
    internal sealed class CAScatterAllocation
    {
        // Remaining quota keyed by PERSISTED source tile id, never by frame
        // index. Frame index is enumeration-derived (P1) and is not guaranteed
        // stable between a candidate preview and generation; the source tile id
        // is scribed on the plan and is.
        internal readonly Dictionary<int, int> RemainingByTile =
            new Dictionary<int, int>();

        internal int TotalCount;
        internal int Placed;
        internal int Surrendered;

        internal bool HasQuota(int sourceTileId)
        {
            int remaining;
            return RemainingByTile.TryGetValue(sourceTileId, out remaining)
                && remaining > 0;
        }

        internal void Consume(int sourceTileId)
        {
            int remaining;
            if (!RemainingByTile.TryGetValue(sourceTileId, out remaining)
                || remaining <= 0) return;
            RemainingByTile[sourceTileId] = remaining - 1;
            Placed++;
        }

        internal int Outstanding =>
            RemainingByTile.Values.Where(v => v > 0).Sum();

        // Surrender RECORDS and CLEARS together. Leaving quotas outstanding
        // after declaring them surrendered would let a later placement satisfy
        // an allocation that was already reported as abandoned.
        internal int SurrenderOutstanding()
        {
            int outstanding = Outstanding;
            if (outstanding <= 0) return 0;
            foreach (int tileId in RemainingByTile.Keys.ToList())
                RemainingByTile[tileId] = 0;
            Surrendered += outstanding;
            return outstanding;
        }
    }

    internal static class CARegionalScatterAllocator
    {
        private static readonly Dictionary<object, CAScatterAllocation> Active =
            new Dictionary<object, CAScatterAllocation>();
        private static int activeForMap = -1;

        internal static void BeginMap(int mapUniqueId)
        {
            if (activeForMap == mapUniqueId) return;
            activeForMap = mapUniqueId;
            Active.Clear();
        }

        internal static CAScatterAllocation For(object genStep)
        {
            CAScatterAllocation allocation;
            return Active.TryGetValue(genStep, out allocation)
                ? allocation : null;
        }

        internal static CARegionalProjectionMapComponent ProjectionOf(Map map)
        {
            var projection =
                map?.GetComponent<CARegionalProjectionMapComponent>();
            return projection?.Active == true ? projection : null;
        }

        // Native base count for ONE constituent, from the pre-factor inputs.
        // CountFromPer10kCells squares a linear mapSize, so a constituent of
        // area A contributes A directly - the same quantity, without inventing
        // a linear dimension for an irregular region.
        internal static int BaseCountForArea(GenStep_Scatterer scatterer,
            float density, int area, int totalArea)
        {
            if (scatterer.countPer10kCellsRange != FloatRange.Zero)
            {
                int per = Mathf.RoundToInt(10000f / Mathf.Max(0.0001f, density));
                if (per <= 0) return 0;
                return Mathf.RoundToInt(area / (float)per);
            }
            if (scatterer.count <= 0 || totalArea <= 0) return 0;
            // A flat `count` is a whole-map quantity; distribute it by area.
            return Mathf.RoundToInt(scatterer.count
                * (area / (float)totalArea));
        }

        // One density draw for the whole allocation, seeded from the region so
        // it is deterministic and does not consume the ambient stream once per
        // constituent.
        internal static float DrawDensity(GenStep_Scatterer scatterer,
            CARegionalProjectionMapComponent projection, string label)
        {
            if (scatterer.countPer10kCellsRange == FloatRange.Zero) return 0f;
            CARegionalPlan plan = projection.Region;
            // bundleRootTileId, NOT startTileId: scatter density is a regional
            // fact, and startTileId is the landing choice. Seeding off it made
            // moving the landing tile reroll junk and geyser density.
            var instance = new CAFeatureInstance(plan?.candidateId,
                0, plan?.bundleRootTileId ?? 0, label, 0);
            CAFeatureRandom.Push(instance, 0x53434154);
            float density = scatterer.countPer10kCellsRange.RandomInRange;
            CAFeatureRandom.Pop();
            return density;
        }

        internal static CAScatterAllocation Allocate(GenStep_Scatterer scatterer,
            Map map, CARegionalProjectionMapComponent projection,
            Func<CAConstituentFrame, Tile, float> factorFor, string label)
        {
            var allocation = new CAScatterAllocation();
            CAConstituentFrameSet frames = projection.Frames;
            if (frames == null) return allocation;

            int totalArea = 0;
            for (int i = 0; i < frames.Count; i++) totalArea += frames[i].Area;
            if (totalArea <= 0) return allocation;

            // No generic native placement factor is applied here. For junk,
            // GetPlacementFactor(map) IS the root junkDensityFactor product,
            // so multiplying it in and then applying the constituent's own
            // factor would count junk twice and reintroduce the root. Each
            // consumer supplies its COMPLETE constituent-local multiplier
            // derived from the native pre-factor semantics instead.
            float density = DrawDensity(scatterer, projection, label);
            var counts = new List<int>(frames.Count);
            var receipts = new List<string>();
            for (int i = 0; i < frames.Count; i++)
            {
                CAConstituentFrame frame = frames[i];
                PlanetTile owner =
                    CARegionalPlanUtility.SurfaceTile(frame.SourceTileId);
                Tile info = owner.Valid ? owner.Tile : null;
                if (frame.Area <= 0 || info == null) { counts.Add(0); continue; }
                int baseCount = BaseCountForArea(scatterer, density,
                    frame.Area, totalArea);
                float factor = factorFor(frame, info);
                int count = Mathf.Max(0, Mathf.RoundToInt(baseCount * factor));
                counts.Add(count);
                if (count > 0)
                    receipts.Add(frame.SourceTileId + ":" + count
                        + (Math.Abs(factor - 1f) > 0.0001f
                            ? "(x" + factor.ToString("F2") + ")" : ""));
            }
            for (int i = 0; i < frames.Count && i < counts.Count; i++)
            {
                if (counts[i] <= 0) continue;
                int tileId = frames[i].SourceTileId;
                int existing;
                allocation.RemainingByTile[tileId] =
                    (allocation.RemainingByTile.TryGetValue(tileId,
                        out existing) ? existing : 0) + counts[i];
                allocation.TotalCount += counts[i];
            }
            Active[scatterer] = allocation;
            Log.Message("[CA][Regional] " + label + " recomputed per "
                + "constituent from native inputs: total "
                + allocation.TotalCount + " [" + string.Join(", ", receipts)
                + "]");
            return allocation;
        }
    }

    // Geysers: recomputed, not rebased. Each constituent uses its OWN biome
    // geyserCountFactor and its OWN mutator factors, so neither the root biome
    // nor the root mutators survive into any constituent's count.
    [HarmonyPatch(typeof(GenStep_ScatterGeysers), "CalculateFinalCount")]
    internal static class CARegionalGeyserCountPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(GenStep_ScatterGeysers __instance, Map map,
            ref int __result)
        {
            try
            {
                var projection = CARegionalScatterAllocator.ProjectionOf(map);
                if (projection == null) return true;
                CARegionalScatterAllocator.BeginMap(map.uniqueID);
                CAScatterAllocation allocation =
                    CARegionalScatterAllocator.Allocate(__instance, map,
                        projection,
                        (frame, info) =>
                        {
                            BiomeDef biome =
                                projection.BiomeAt(frame.Centroid)
                                ?? info.PrimaryBiome;
                            float product = biome?.geyserCountFactor ?? 1f;
                            PlanetTile owner = CARegionalPlanUtility
                                .SurfaceTile(frame.SourceTileId);
                            if (projection.IsSelectedCore(owner))
                                foreach (TileMutatorDef mutator in info.Mutators)
                                    if (mutator != null)
                                        product *= mutator.geyserCountFactor;
                            return product;
                        },
                        "geysers");
                __result = allocation.TotalCount;
                return false;
            }
            catch (Exception exception)
            {
                Log.Warning("[CA][Regional] geyser recompute failed, falling "
                    + "back to the native count: " + exception.Message);
                return true;
            }
        }
    }

    // Junk: same recompute, its own factor. The root junk factor is never
    // applied because the native count is never used.
    [HarmonyPatch(typeof(GenStep_Scatterer), "CalculateFinalCount")]
    internal static class CARegionalJunkCountPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(GenStep_Scatterer __instance, Map map,
            ref int __result)
        {
            try
            {
                if (__instance is GenStep_ScatterGeysers) return true;
                if (!Traverse.Create(__instance).Field("isJunk")
                        .GetValue<bool>()) return true;
                var projection = CARegionalScatterAllocator.ProjectionOf(map);
                if (projection == null) return true;
                CARegionalScatterAllocator.BeginMap(map.uniqueID);
                CAScatterAllocation allocation =
                    CARegionalScatterAllocator.Allocate(__instance, map,
                        projection,
                        (frame, info) =>
                        {
                            float product = 1f;
                            PlanetTile owner = CARegionalPlanUtility
                                .SurfaceTile(frame.SourceTileId);
                            if (projection.IsSelectedCore(owner))
                                foreach (TileMutatorDef mutator in info.Mutators)
                                    if (mutator != null)
                                        product *= mutator.junkDensityFactor;
                            return product;
                        },
                        "junk(" + __instance.GetType().Name + ")");
                __result = allocation.TotalCount;
                return false;
            }
            catch (Exception exception)
            {
                Log.Warning("[CA][Regional] junk recompute failed, falling "
                    + "back to the native count: " + exception.Message);
                return true;
            }
        }
    }

    // Placement isolation.
    //
    // INVARIANT: an allocation either produces a cell owned by a constituent
    // that holds quota for it, or it is explicitly surrendered. A
    // cross-constituent item is never produced.
    //
    // Ownership is composed INTO the native validator rather than repaired
    // afterwards. Every native search path - nearMapCenter, nearPlayerStart and
    // the default TryFindRandomNotEdgeCellWith - consults CanScatterAt and
    // nothing else, and CanScatterAt is where NearUsedSpot/minSpacing, the edge
    // distances, standable, fogged, roofed and the fallback validators all
    // live. Adding ownership there therefore keeps every native rule intact
    // while making an unowned cell simply not a candidate.
    [HarmonyPatch(typeof(GenStep_Scatterer), "CanScatterAt")]
    internal static class CARegionalScatterOwnershipPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GenStep_Scatterer __instance, IntVec3 loc,
            Map map, ref bool __result)
        {
            if (!__result) return;
            CAScatterAllocation allocation = null;
            try
            {
                allocation = CARegionalScatterAllocator.For(__instance);
                if (allocation == null) return;
                var projection = CARegionalScatterAllocator.ProjectionOf(map);
                if (projection == null) return;
                int owner = projection.Frames.OwnerAtIndex(
                    map.cellIndices.CellToIndex(loc));
                if (owner < 0)
                {
                    __result = false;
                    return;
                }
                if (!allocation.HasQuota(
                        projection.Frames[owner].SourceTileId))
                    __result = false;
            }
            catch (Exception exception)
            {
                // FAIL CLOSED. With an active regional allocation, an
                // unevaluated ownership test must refuse the cell; swallowing
                // it would let a cross-constituent item through and break the
                // invariant this filter exists to hold.
                if (allocation != null)
                {
                    __result = false;
                    Log.Error("[CA][Regional] scatter ownership filter threw; "
                        + "refusing the cell to preserve constituent "
                        + "isolation: " + exception);
                }
            }
        }
    }

    // Consumes the quota of whichever constituent actually owns the accepted
    // cell. The ownership filter guarantees that constituent held quota, so the
    // per-constituent totals are exact.
    [HarmonyPatch(typeof(GenStep_Scatterer), "TryFindScatterCell")]
    internal static class CARegionalScatterConsumePatch
    {
        [HarmonyPostfix]
        private static void Postfix(GenStep_Scatterer __instance, Map map,
            IntVec3 result, ref bool __result)
        {
            try
            {
                CAScatterAllocation allocation =
                    CARegionalScatterAllocator.For(__instance);
                if (allocation == null) return;
                var projection = CARegionalScatterAllocator.ProjectionOf(map);
                if (projection == null) return;

                if (!__result)
                {
                    // No owned cell remained. Every outstanding allocation is
                    // surrendered explicitly rather than being satisfied on the
                    // wrong constituent.
                    int surrendered = allocation.SurrenderOutstanding();
                    if (surrendered > 0)
                    {
                        Log.Message("[CA][Regional] scatter surrendered "
                            + surrendered + " of "
                            + allocation.TotalCount + " allocated placements: "
                            + "no cell owned by a constituent still holding "
                            + "quota satisfied the native validators. No "
                            + "cross-constituent item was produced.");
                    }
                    return;
                }

                int owner = projection.Frames.OwnerAtIndex(
                    map.cellIndices.CellToIndex(result));
                if (owner < 0)
                {
                    // Should be unreachable: the ownership filter runs inside
                    // every native search path. Refuse rather than emit a
                    // cross-constituent item, which is the stated invariant.
                    __result = false;
                    Log.Warning("[CA][Regional] scatter refused an unowned "
                        + "cell that passed validation; surrendering the "
                        + "remaining allocation to preserve constituent "
                        + "isolation.");
                    return;
                }
                allocation.Consume(projection.Frames[owner].SourceTileId);
            }
            catch (Exception exception)
            {
                // An unaccounted placement is not acceptable: if quota could
                // not be consumed, the item must not stand.
                __result = false;
                Log.Error("[CA][Regional] scatter quota consumption threw; "
                    + "refusing the placement rather than leaving it "
                    + "unaccounted: " + exception);
            }
        }
    }
}
