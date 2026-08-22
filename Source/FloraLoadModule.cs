using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // JUNGLE CARD, always-real-compatible: rate is not reality. On
    // oversized maps the wild plant spawner's continuous density
    // maintenance runs on a reduced cadence (identical eventual density,
    // slower ecological convergence, every plant fully real), and initial
    // flora seeds sparse so generation is cheaper and the world grows
    // into its density naturally - the young world matures. Standard maps
    // are untouched.
    internal static class CAFloraLoad
    {
        internal const int OversizeCells = 1000000;

        internal static bool Oversized(Map map)
        {
            return map != null
                && map.Size.x * map.Size.z > OversizeCells;
        }

        // An expanded region initially samples the same number of cells that
        // its authored local map scale would have seeded. The random-order
        // sample is spread over the complete backing map, while RimWorld's
        // normal spawner retains the full eventual density target.
        internal static bool TryRegionalInitialSeedBudget(Map map,
            out int attempts)
        {
            attempts = 0;
            if (!Oversized(map)) return false;
            CARegionalPlan region = CARegionalWorldComponent.Current
                ?.FindRegionForMap(map);
            if (region == null || region.mapSize <= 0) return false;
            long localArea = (long)region.mapSize * region.mapSize;
            attempts = (int)System.Math.Min(map.Area,
                System.Math.Max(1L, localArea));
            return attempts < map.Area;
        }
    }

    // Regrowth cadence: 1-in-4 ticks on oversized maps. The sampling
    // budget drops fourfold; the target density does not change at all.
    [HarmonyPatch(typeof(WildPlantSpawner),
        nameof(WildPlantSpawner.WildPlantSpawnerTick))]
    internal static class Patch_CAWildPlantCadence
    {
        private static bool Prefix(Map ___map)
        {
            if (!CAFloraLoad.Oversized(___map)) return true;
            return (Find.TickManager.TicksGame + ___map.uniqueID) % 4 == 0;
        }
    }

    // Non-regional oversized maps retain the earlier half-density fallback.
    // Expanded regional maps use the bounded native seed pass below instead,
    // because lowering the density factor while still traversing every cell
    // does not bound their generation work.
    [HarmonyPatch(typeof(GenStep_Plants), nameof(GenStep_Plants.Generate))]
    internal static class Patch_CASparseSeed
    {
        internal static float factor = 1f;

        private static void Prefix(Map map)
        {
            factor = CAFloraLoad.Oversized(map)
                && !CAFloraLoad.TryRegionalInitialSeedBudget(map, out _)
                ? 0.5f : 1f;
        }

        private static void Postfix()
        {
            factor = 1f;
        }

        [HarmonyFinalizer]
        private static System.Exception Finalizer(
            System.Exception __exception)
        {
            factor = 1f;
            return __exception;
        }
    }

    // Regional backing maps preserve native plant eligibility, biome lookup,
    // density, definition selection, and random order. Only the number of
    // initial cells examined is bounded. Normal regrowth then advances the
    // same canonical ecology toward its full map-wide target.
    [HarmonyPatch(typeof(GenStep_Plants), nameof(GenStep_Plants.Generate))]
    internal static class Patch_CABoundedRegionalPlantSeed
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(Map map)
        {
            if (!CAFloraLoad.TryRegionalInitialSeedBudget(map,
                    out int budget)
                || CARegionalPlantClusterIndexLifecyclePatch.Active == null)
                return true;

            var timer = System.Diagnostics.Stopwatch.StartNew();
            float density = map.wildPlantSpawner
                .CurrentPlantDensityFactor;
            float desired = map.wildPlantSpawner
                .CurrentWholeMapNumDesiredPlants;
            int attempts = 0;
            int spawned = 0;
            foreach (IntVec3 cell in map.cellsInRandomOrder.GetAll())
            {
                if (attempts >= budget) break;
                attempts++;
                if (!Rand.Chance(0.001f)
                    && map.wildPlantSpawner.CheckSpawnWildPlantAt(cell,
                        density, desired, setRandomGrowth: true))
                    spawned++;
            }
            timer.Stop();
            Log.Message("[CA][Regional][Timing][Plants] bounded initial "
                + "native seed examined " + attempts + " of " + map.Area
                + " cells and spawned " + spawned + " plants in "
                + timer.ElapsedMilliseconds + " ms for " + map.Size.x + "x"
                + map.Size.z + " map " + map.uniqueID + "; full ecological "
                + "target " + desired.ToString("F1") + ".");
            return false;
        }
    }

    [HarmonyPatch(typeof(WildPlantSpawner), "CurrentPlantDensityFactor",
        MethodType.Getter)]
    internal static class Patch_CASparseSeedFactor
    {
        private static void Postfix(ref float __result)
        {
            if (Patch_CASparseSeed.factor < 1f)
                __result *= Patch_CASparseSeed.factor;
        }
    }
}
