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

    // Sparse seed: generation plants at half density on oversized maps -
    // a real generation-time saving exactly where flora is heaviest
    // (jungle worst of all), and the spawner grows the map to full
    // density over the first in-game weeks. Momentary factor override,
    // restored in finally; generation is single-threaded.
    [HarmonyPatch(typeof(GenStep_Plants), nameof(GenStep_Plants.Generate))]
    internal static class Patch_CASparseSeed
    {
        internal static float factor = 1f;

        private static void Prefix(Map map)
        {
            factor = CAFloraLoad.Oversized(map) ? 0.5f : 1f;
        }

        private static void Postfix()
        {
            factor = 1f;
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
