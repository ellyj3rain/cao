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
        public static int FrontierHoldingCount(int suitableSites,
            float frequency)
        {
            int sites = Math.Max(0, Math.Min(8, suitableSites));
            return Clamp((int)Math.Round(sites * Clamp01(frequency)),
                0, sites);
        }

        // Size owns resident count and material form after a site exists. Land
        // is a hard capacity constraint and frequency is deliberately absent.
        public static int FrontierResidentCount(int landCapacity, float size)
        {
            int cap = Math.Max(1, Math.Min(6, 2 + Clamp(landCapacity, 0, 3)));
            int preferred = 1 + (int)Math.Round(Clamp01(size) * (cap - 1));
            return Clamp(preferred, 1, cap);
        }

        public static int FrontierMaterialLevel(int landCapacity, float size)
        {
            int preferred = (int)Math.Round(Clamp01(size) * 3f);
            return Clamp(preferred, 0, Clamp(landCapacity, 0, 3));
        }

        // 0 = cabin, 1 = established homestead.
        public static int FrontierForm(int residentCount, int materialLevel)
        {
            return residentCount >= 3 && materialLevel >= 2 ? 1 : 0;
        }

        public static int PopulationFromFacts(
            int landCapacity, int historicalDevelopment, int techTier,
            bool scenarioOverride)
        {
            int ground = Clamp(landCapacity, 0, 3);
            int history = Clamp(historicalDevelopment, 0, 3);
            int tier = Clamp(techTier, 0, 3);
            int basePopulation = 45 + ground * 55 + history * 70
                + tier * 85 + (scenarioOverride ? 70 : 0);
            return Math.Max(18, basePopulation);
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

        // FIXED CLASSIFICATION THRESHOLD. The town/city bar is a game
        // constant, not authored world state (DR-114: settlement scale is
        // derived; DR-115: generic intensity controls are not authoring
        // primitives). A settlement with support >= 62 counts as a town;
        // >= 74 as a city. The support facts (population, land, access,
        // services, civic, economic, trade, specialization, regional role,
        // history) determine the scale; the threshold does not.
        public const int TownThreshold = 62;
        public const int CityThreshold = 74;

        public static int SettlementScale(int population, int urbanSupport)
        {
            int threshold = TownThreshold;
            if (population >= 1200 && urbanSupport >= threshold + 12) return 5;
            if (population >= 500 && urbanSupport >= threshold) return 4;
            if (population >= 280) return 3;
            if (population >= 140) return 2;
            if (population >= 60) return 1;
            return 0;
        }

        // HOW MANY QUARTERS A SETTLEMENT BUILDS, from what it actually
        // has. A larger place is more internally complex than a small
        // one, and co-sited settlements of different size should not
        // raise identical internal structure.
        //
        // Deliberately NOT keyed to realized scale. Scale is population
        // and support measured against a fixed classification threshold, which the
        // tendency moves - so keying quarters to scale let a naming
        // preference change how much of a settlement physically got
        // built. The tendency decides where the bar for calling a place
        // a town or a city sits; it cannot supply a place with support
        // it lacks, and it must not supply it with buildings either.
        //
        // The support constants are the unmoved threshold, so the same
        // settlement raises the same structure whatever the player calls
        // it.
        public static int SettlementQuarters(int population,
            int urbanSupport)
        {
            const int Unmoved = 72;
            if (population >= 1200 && urbanSupport >= Unmoved + 12)
                return 4;
            if (population >= 500 && urbanSupport >= Unmoved) return 3;
            if (population >= 280) return 2;
            return 1;
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

        // The variety tendency's whole effect: how many distinct owning
        // factions a source selection of a given size aims to represent.
        // Selection then prefers unrepresented owners until this target is
        // met, and the pool's actual composition caps the result.
        public static int SourceVarietyTargetDistinct(int count,
            int distinctAvailable, float variety)
        {
            if (count <= 0) return 0;
            return Math.Min(count, 1 + (int)Math.Round(
                Clamp01(variety) * Math.Max(0, distinctAvailable - 1)));
        }

        // World-scale settlement placement. Habitat preference (the biome
        // and temperature weight vanilla already computes) stays primary;
        // concentration only bends the spatial term. At 0.5 the term is a
        // constant 7.5 for every distance, so the middle of the tendency
        // reproduces vanilla's indifference to existing settlements.
        public static double WorldPlacementScore(int seed, int tileId,
            float habitatWeight, float distanceToNearestSettlement,
            float concentration)
        {
            if (habitatWeight <= 0f) return 0d;
            float tendency = Clamp01(concentration);
            float distance = distanceToNearestSettlement < 0f ? 15f
                : Math.Min(15f, Math.Max(0f, distanceToNearestSettlement));
            double spread = distance;
            double cluster = 15f - distance;
            double spatial = spread * (1d - tendency) + cluster * tendency;
            return habitatWeight * (1d + spatial * 0.15d)
                + Unit(seed, tileId, 6577) * 0.001d;
        }

        // World-scale settlement facts, derived deterministically from the
        // ground the settlement stands on. These are coarse world-map
        // classifications; a materialized regional settlement derives its
        // richer record from the full composition instead. The development
        // level is a real cause supplied by the caller (the authored world
        // baseline plus position-in-network variation); it replaces the
        // hidden per-tile hash that previously stood for history.
        public static int WorldSettlementPopulation(int seed, int tileId,
            int landCapacity, int techTier, int developmentLevel,
            float variability = 0f)
        {
            // Countertypical tech tier: with probability proportional to
            // variability, a settlement diverges from its faction\u2019s tech
            // level. The divergence is deterministic (seeded by tile id),
            // not random, and its extremity scales with variability.
            int effectiveTechTier = techTier;
            if (variability > 0f && Unit(seed, tileId, 3721) < variability)
            {
                int maxDivergence = 1 + (int)(variability * 2f);
                int divergence = 1 + (int)(Unit(seed, tileId, 3723)
                    * maxDivergence);
                int direction = Unit(seed, tileId, 3727) < 0.5f ? -1 : 1;
                effectiveTechTier = Clamp(
                    techTier + direction * divergence, 0, 3);
            }
            return PopulationFromFacts(landCapacity,
                Clamp(developmentLevel, 0, 3),
                Clamp(effectiveTechTier, 0, 3), false);
        }

        // Stability modulates endogenous transition cadence. The base
        // cadence is one in-game day (60000 ticks); stability scales it
        // from 0.5x (volatile) to 5x (very stable). This modulates
        // legitimate transition rate, not randomness.
        public static int StabilityCadence(int baseCadence, float stability)
        {
            float scale = 1f + Clamp01(stability) * 4f;
            return Math.Max(2500, (int)(baseCadence * scale));
        }

        public static int WorldSettlementSupport(int population,
            int landCapacity, int access, bool regionalCenter,
            int developmentLevel)
        {
            int services = population >= 400 ? 2
                : population >= 140 ? 1 : 0;
            int civic = population >= 700 ? 2
                : population >= 220 ? 1 : 0;
            int economic = EconomicCapacity(population, civic,
                hasWorkshop: population >= 280, hasStores: population >= 140);
            // Development is a real supplied cause, not a population proxy.
            // A young world can have large settlements that are still
            // thinly developed; an old world can have small ones that are
            // richly developed for their size.
            int history = Clamp(developmentLevel, 0, 3);
            return UrbanSupport(population, landCapacity, access, services,
                civic, economic, Clamp(access, 0, 3), 0, regionalCenter,
                history);
        }

        // The distant-founding gate: one deterministic roll per period.
        // The player authors how often new settlements appear in the
        // far-off world; this is the only thing the rate controls.
        public static bool DistantFoundingRoll(int seed, int period,
            float rate)
        {
            if (rate <= 0f) return false;
            return Unit(seed, period, 442771) < Clamp01(rate);
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
