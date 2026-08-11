using System;

namespace ColonistAwareness
{
    // Pure causal kernel shared by runtime generation and the fixed-seed
    // receipt runner. It contains no RimWorld state: adapters gather facts,
    // persist the result, and downstream systems consume that saved result.
    public static class CAWorldTendencyCausalKernel
    {
        public static int StableStringHash(string value)
        {
            if (value == null) return 0;
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
                return hash;
            }
        }

        public static int HashCombineInt(int seed, int value)
        {
            unchecked
            {
                return (int)((uint)seed ^ ((uint)value + 2654435769u
                    + ((uint)seed << 6) + ((uint)seed >> 2)));
            }
        }

        public static int HashCombineInt(int first, int second, int third,
            int fourth)
        {
            unchecked
            {
                int left = 352654597;
                int right = left;
                left = ((left << 5) + left + (left >> 27)) ^ first;
                right = ((right << 5) + right + (right >> 27)) ^ second;
                left = ((left << 5) + left + (left >> 27)) ^ third;
                right = ((right << 5) + right + (right >> 27)) ^ fourth;
                return left + right * 1566083941;
            }
        }

        public static float Unit(int seed, int subject, int salt)
        {
            unchecked
            {
                uint value = (uint)seed;
                value ^= (uint)subject + 0x9E3779B9u
                    + (value << 6) + (value >> 2);
                value ^= (uint)salt + 0x85EBCA6Bu
                    + (value << 6) + (value >> 2);
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return (value & 0x00FFFFFFu) / 16777216f;
            }
        }

        public static float ResolveRange(int seed, int subject, int salt,
            float low, float high)
        {
            float minimum = Math.Min(low, high);
            float maximum = Math.Max(low, high);
            return minimum + (maximum - minimum) * Unit(seed, subject, salt);
        }

        public static int RequestedExtent(int seed, int rootTileId,
            float stitchedFrequency, int sizeMin, int sizeMax,
            int availableTiles)
        {
            int available = Math.Max(1, availableTiles);
            if (Unit(seed, rootTileId, 1195726031)
                    >= Clamp01(stitchedFrequency))
                return 1;
            int low = Math.Max(1, Math.Min(sizeMin, sizeMax));
            int high = Math.Max(low, Math.Max(sizeMin, sizeMax));
            int span = high - low + 1;
            int requested = low + Math.Min(span - 1,
                (int)(Unit(seed, rootTileId, 5119037) * span));
            return Math.Min(available, requested);
        }

        // Concentration affects placement, never classification. Ground and
        // access remain independent inputs. At low concentration distance from
        // existing placements is rewarded; at high concentration proximity is.
        public static double PlacementScore(int seed, int subject,
            int landCapacity, int access, bool routeLinked,
            float distanceFromAnchor, float distanceFromNearestSettlement,
            float concentration)
        {
            float tendency = Clamp01(concentration);
            float distance = distanceFromNearestSettlement < 0f
                ? distanceFromAnchor : distanceFromNearestSettlement;
            double ground = Clamp(landCapacity, 0, 3) * 24d
                + Clamp(access, 0, 3) * 11d
                + (routeLinked ? 12d : 0d);
            double spread = Math.Min(12f, Math.Max(0f, distance)) * 14d;
            double cluster = Math.Max(0f, 12f - distance) * 14d;
            double spatial = spread * (1d - tendency) + cluster * tendency;
            return ground + spatial + Unit(seed, subject, 9137) * 0.01d;
        }

        // One suitable constituent can support at most one generated holding.
        // Size never changes this count.
        public static int FrontierHoldingCount(int seed, int suitableSites,
            float frequency)
        {
            int count = 0;
            int sites = Math.Max(0, Math.Min(8, suitableSites));
            for (int i = 0; i < sites; i++)
                if (Unit(seed, i, 2718281) < Clamp01(frequency)) count++;
            return count;
        }

        // Size owns household and material form after a site exists. Land is a
        // hard capacity constraint and frequency is deliberately absent.
        public static int FrontierHouseholdSize(int seed, int holdingIndex,
            int landCapacity, float size)
        {
            int cap = Math.Max(1, Math.Min(6, 2 + Clamp(landCapacity, 0, 3)));
            int preferred = 1 + (int)Math.Round(Clamp01(size) * (cap - 1));
            int jitter = Unit(seed, holdingIndex, 314159) < 0.35f ? -1
                : Unit(seed, holdingIndex, 314159) > 0.82f ? 1 : 0;
            return Clamp(preferred + jitter, 1, cap);
        }

        public static int FrontierMaterialLevel(int seed, int holdingIndex,
            int landCapacity, float size)
        {
            int preferred = (int)Math.Round(Clamp01(size) * 3f);
            if (Unit(seed, holdingIndex, 1618033) > 0.88f) preferred++;
            return Clamp(preferred, 0, Clamp(landCapacity, 0, 3));
        }

        // 0 = cabin, 1 = established homestead.
        public static int FrontierForm(int householdSize, int materialLevel)
        {
            return householdSize >= 3 && materialLevel >= 2 ? 1 : 0;
        }

        public static int UnaffiliatedPercent(float share, int variation)
        {
            return Clamp(3 + (int)(Clamp01(share) * 12f)
                + Clamp(variation, 0, 3), 0, 45);
        }

        public static bool UseDifferentSettlementOwner(int seed, int slot,
            float variety)
        {
            return Unit(seed, slot, 777013) < Clamp01(variety);
        }

        public static bool CreateLocalFaction(int seed, int slot,
            float propensity)
        {
            return Unit(seed, slot, 777019) < Clamp01(propensity);
        }

        // 0 neutral, 1 hostile. The saved relation is the only downstream
        // political input; consumers never call this method again.
        public static int GeneratedRelation(int seed, int leftKey,
            int rightKey, float conflictPropensity)
        {
            int left = Math.Min(leftKey, rightKey);
            int right = Math.Max(leftKey, rightKey);
            int pair = unchecked(left * 397 ^ right * 7919);
            return Unit(seed, pair, 777023) < Clamp01(conflictPropensity)
                ? 1 : 0;
        }

        public static int GeneratedPopulation(int seed, int slot,
            int landCapacity, int historicalDevelopment, int techTier,
            bool scenarioOverride)
        {
            int ground = Clamp(landCapacity, 0, 3);
            int history = Clamp(historicalDevelopment, 0, 3);
            int tier = Clamp(techTier, 0, 3);
            int basePopulation = 45 + ground * 55 + history * 70
                + tier * 85 + (scenarioOverride ? 70 : 0);
            int variation = (int)(Unit(seed, slot, 777029) * 91f) - 45;
            return Math.Max(18, basePopulation + variation);
        }

        public static int EconomicCapacity(int population, int civic,
            bool hasWorkshop, bool hasStores)
        {
            int value = population >= 700 ? 2 : population >= 140 ? 1 : 0;
            if (hasWorkshop) value++;
            if (hasStores) value++;
            if (civic >= 2) value++;
            return Clamp(value, 0, 3);
        }

        public static int UrbanSupport(int population, int landCapacity,
            int access, int services, int civic, int economicCapacity,
            int tradeConnectivity, int specialization, bool regionalCenter,
            int historicalDevelopment)
        {
            int populationSupport = population >= 1200 ? 20
                : population >= 700 ? 16 : population >= 400 ? 12
                : population >= 220 ? 8 : population >= 100 ? 4 : 0;
            return populationSupport
                + Clamp(landCapacity, 0, 3) * 4
                + Clamp(access, 0, 3) * 4
                + Clamp(services, 0, 3) * 6
                + Clamp(civic, 0, 3) * 6
                + Clamp(economicCapacity, 0, 3) * 5
                + Clamp(tradeConnectivity, 0, 3) * 5
                + Clamp(specialization, 0, 3) * 3
                + (regionalCenter ? 6 : 0)
                + Clamp(historicalDevelopment, 0, 3) * 4;
        }

        // The tendency changes only this threshold. It cannot supply missing
        // population, land, access, services, civic development, economic
        // capacity, trade links, specialization, regional role, or history.
        public static int UrbanThreshold(float propensity)
        {
            return 72 - (int)Math.Round(Clamp01(propensity) * 20f);
        }

        // Values match CASettlementScale.
        public static int SettlementScale(int population, int urbanSupport,
            float urbanGrowthPropensity)
        {
            int threshold = UrbanThreshold(urbanGrowthPropensity);
            if (population >= 1200 && urbanSupport >= threshold + 12) return 5;
            if (population >= 500 && urbanSupport >= threshold) return 4;
            if (population >= 280) return 3;
            if (population >= 140) return 2;
            if (population >= 60) return 1;
            return 0;
        }

        // 0 peaceful, 1 rival, 2 contested.
        public static int RelationPattern(int factionCount, int hostilePairs,
            int neutralPairs)
        {
            if (hostilePairs > 0) return 2;
            if (factionCount > 1 && neutralPairs > 0) return 1;
            return 0;
        }

        // Values match CASettlementPattern. All inputs are realized facts;
        // no tendency appears here.
        public static int SettlementPattern(int settlementCount,
            int occupiedTileCount, int largestTileGroup,
            int routeEdges, float averageDistance, bool dominantCenter,
            int factionCount, int relationPattern, int frontierHoldingCount)
        {
            if (settlementCount <= 0) return 0;
            if (settlementCount == 1) return 1;
            if (relationPattern == 2) return 8;
            if (settlementCount <= 2
                && frontierHoldingCount >= settlementCount * 2
                && frontierHoldingCount >= 4) return 7;
            if (largestTileGroup >= Math.Max(2, settlementCount - 1))
                return dominantCenter ? 5 : 4;
            if (occupiedTileCount >= 3 && routeEdges >= occupiedTileCount - 1)
                return 3;
            if (factionCount > 1 && !dominantCenter
                && occupiedTileCount >= 2 && averageDistance > 1.6f)
                return 6;
            if (largestTileGroup > 1)
                return dominantCenter ? 5 : 6;
            if (dominantCenter) return 5;
            return averageDistance <= 1.6f ? 4 : 2;
        }

        public static int OffMapActivityBudget(int subjectCount, float rate)
        {
            if (subjectCount <= 0 || rate <= 0f) return 0;
            return Math.Max(1, Math.Min(subjectCount,
                (int)Math.Ceiling(subjectCount * Clamp01(rate))));
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }

        private static int Clamp(int value, int low, int high)
        {
            return value < low ? low : value > high ? high : value;
        }
    }
}
