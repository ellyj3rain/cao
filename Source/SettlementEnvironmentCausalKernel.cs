using System;

namespace ColonistAwareness
{
    // Pure, deterministic environmental derivation. Runtime adapters provide
    // RimWorld facts; this kernel decides only the resulting physical support.
    public static class CASettlementEnvironmentCausalKernel
    {
        public static float FoodSupport(int growingTwelfths,
            float plantDensity, float forageability)
        {
            return Clamp01(Clamp(growingTwelfths, 0, 12) / 12f * 0.45f
                + Clamp01(plantDensity) * 0.35f
                + Clamp01(forageability) * 0.20f);
        }

        public static float SeasonalThermalPressure(float minimum,
            float maximum)
        {
            float cold = minimum < 0f
                ? Clamp01((0f - minimum) / 50f) : 0f;
            float heat = maximum > 35f
                ? Clamp01((maximum - 35f) / 25f) : 0f;
            return Math.Max(cold, heat);
        }

        public static int Capacity(int terrainCapacity,
            int growingTwelfths, float plantDensity, float forageability,
            float foodSupport, float thermalPressure, bool extremeBiome)
        {
            if (terrainCapacity <= 0) return 0;
            int environmentalCapacity;
            bool surfaceFoodAbsent = growingTwelfths == 0
                && plantDensity < 0.03f && forageability < 0.05f;
            if (surfaceFoodAbsent || foodSupport < 0.32f)
                environmentalCapacity = 1;
            else if (foodSupport < 0.55f)
                environmentalCapacity = 2;
            else
                environmentalCapacity = 3;
            if (extremeBiome && environmentalCapacity > 1)
                environmentalCapacity--;
            if (thermalPressure >= 0.85f)
                environmentalCapacity = Math.Min(environmentalCapacity, 1);
            else if (thermalPressure >= 0.55f)
                environmentalCapacity = Math.Min(environmentalCapacity, 2);
            return Clamp(Math.Min(terrainCapacity, environmentalCapacity),
                1, 3);
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return value < minimum ? minimum
                : value > maximum ? maximum : value;
        }
    }
}
