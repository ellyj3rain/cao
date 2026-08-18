using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // One factual environmental projection used by settlement realization and
    // live planning. Biome is evidence, not an authored social category: it
    // constrains capacity and ranks already-authorized action without creating
    // a culture, institution, program, or player permission.
    internal sealed class CASettlementEnvironmentFacts
    {
        internal bool Valid;
        internal string Biomes = "unknown";
        internal int SourceTiles;
        internal int GrowingTwelfths;
        internal float AverageTemperature;
        internal float MinimumTemperature;
        internal float MaximumTemperature;
        internal float Rainfall;
        internal float Forageability;
        internal float PlantDensity;
        internal float AnimalDensity;
        internal float DiseasePerYear;
        internal float FoodSupport;
        internal float SeasonalThermalPressure;
        internal float PollutionPressure;
        internal float ScariaPressure;
        internal bool PermanentDarkness;
        internal bool ToxicWater;
        internal bool FarmingCampsAllowed = true;
        internal bool Vacuum;
        internal int TerrainCapacity;
        internal int NaturalCapacity;
        internal int LandCapacity;
        internal int HabitatRequirementMask;
        internal int RequiredCapabilityTier;
        internal CAHabitatFoodRoute FoodRoute;
        internal int SourceHash;

        internal bool SeasonalExposureMatters => Valid
            && (MinimumTemperature < 0f || MaximumTemperature > 35f);

        internal string ShortSummary()
        {
            if (!Valid) return "Environmental conditions unavailable";
            return Biomes + " · outdoor growing "
                + (GrowingTwelfths * 5) + "/60 days · rain "
                + Rainfall.ToString("F0", CultureInfo.InvariantCulture)
                + " mm · ground " + TerrainCapacity + "/3"
                + " · natural support " + NaturalCapacity + "/3"
                + " · " + CAHabitatViabilityCausalKernel
                    .FoodRouteWords(FoodRoute);
        }

        internal string RequirementSummary()
        {
            if (!Valid) return "Habitat requirements unavailable";
            string requirements = string.Join(", ",
                CAHabitatViabilityCausalKernel
                    .Enumerate(HabitatRequirementMask)
                    .Select(CAHabitatViabilityCausalKernel.RequirementWords)
                    .ToArray());
            return "Requires " + (requirements.NullOrEmpty()
                    ? "no represented habitat functions" : requirements)
                + " · minimum capability tier " + RequiredCapabilityTier;
        }
    }

    internal static class CASettlementEnvironment
    {
        private const float CropMinimumTemperature = 6f;
        private const float CropMaximumTemperature = 42f;

        internal static CASettlementEnvironmentFacts ForTile(int tileId)
        {
            return ForTile(CARegionalPlanUtility.SurfaceTile(tileId));
        }

        internal static CASettlementEnvironmentFacts ForTile(
            PlanetTile tile)
        {
            if (!tile.Valid || tile.Tile == null)
                return new CASettlementEnvironmentFacts();

            Tile surface = tile.Tile;
            List<BiomeDef> biomes = surface.Biomes
                .Where(biome => biome != null).Distinct()
                .OrderBy(biome => biome.defName, StringComparer.Ordinal)
                .ToList();
            if (biomes.Count == 0 && surface.PrimaryBiome != null)
                biomes.Add(surface.PrimaryBiome);
            if (biomes.Count == 0)
                return new CASettlementEnvironmentFacts();

            int terrainCapacity = TerrainCapacity(surface);
            int growingTwelfths = 0;
            try
            {
                growingTwelfths = GenTemperature
                    .TwelfthsInAverageTemperatureRange(tile,
                        CropMinimumTemperature, CropMaximumTemperature).Count;
            }
            catch
            {
                // A valid saved tile still has useful biome facts if a
                // third-party world layer cannot supply a seasonal curve.
            }

            float plantDensity = biomes.Average(biome => biome.plantDensity)
                * surface.PlantDensityFactor;
            float forageability = biomes.Average(biome =>
                biome.foragedFood == null ? 0f : biome.forageability);
            float animalDensity = biomes.Average(biome =>
                biome.animalDensity);
            float diseasePerYear = biomes.Average(biome =>
                biome.diseaseMtbDays > 0f
                    ? 60f / biome.diseaseMtbDays : 0f);
            float pollutionPressure = Mathf.Max(surface.pollution,
                biomes.Max(biome => Mathf.Max(0f, biome.pollutionOffset)));
            float scariaPressure = biomes.Max(biome =>
                Mathf.Max(0f, biome.wildAnimalScariaChance));
            bool permanentDarkness = biomes.Any(biome =>
                (biome.biomeMapConditions ?? new List<GameConditionDef>())
                    .Any(condition => condition?.conditionClass != null
                        && typeof(GameCondition_NoSunlight).IsAssignableFrom(
                            condition.conditionClass)));
            bool toxicWater = biomes.Any(HasToxicWater);
            bool farmingCampsAllowed = biomes.All(biome =>
                biome.allowFarmingCamps);
            bool vacuum = biomes.Any(biome => biome.inVacuum);
            float foodSupport = CASettlementEnvironmentCausalKernel
                .FoodSupport(growingTwelfths, plantDensity, forageability);
            float minimum = surface.MinTemperature;
            float maximum = surface.MaxTemperature;
            float thermalPressure = CASettlementEnvironmentCausalKernel
                .SeasonalThermalPressure(minimum, maximum);
            bool extreme = biomes.Any(biome => biome.isExtremeBiome);
            int landCapacity = CASettlementEnvironmentCausalKernel.Capacity(
                terrainCapacity,
                growingTwelfths, plantDensity, forageability, foodSupport,
                thermalPressure, extreme);
            CAHabitatRequirementProfile habitat =
                CAHabitatViabilityCausalKernel.Requirements(
                    new CAHabitatEnvironmentInput
                    {
                        GrowingTwelfths = growingTwelfths,
                        FoodSupport = foodSupport,
                        ThermalPressure = thermalPressure,
                        DiseasePerYear = diseasePerYear,
                        PollutionPressure = pollutionPressure,
                        ScariaPressure = scariaPressure,
                        PermanentDarkness = permanentDarkness,
                        ToxicWater = toxicWater,
                        FarmingCampsAllowed = farmingCampsAllowed,
                        Vacuum = vacuum
                    });

            var facts = new CASettlementEnvironmentFacts
            {
                Valid = terrainCapacity > 0,
                Biomes = string.Join(" + ", biomes.Select(biome =>
                    biome.label.NullOrEmpty() ? biome.defName : biome.label)
                    .ToArray()),
                SourceTiles = 1,
                GrowingTwelfths = growingTwelfths,
                AverageTemperature = surface.temperature,
                MinimumTemperature = minimum,
                MaximumTemperature = maximum,
                Rainfall = surface.rainfall,
                Forageability = forageability,
                PlantDensity = plantDensity,
                AnimalDensity = animalDensity,
                DiseasePerYear = diseasePerYear,
                FoodSupport = foodSupport,
                SeasonalThermalPressure = thermalPressure,
                PollutionPressure = pollutionPressure,
                ScariaPressure = scariaPressure,
                PermanentDarkness = permanentDarkness,
                ToxicWater = toxicWater,
                FarmingCampsAllowed = farmingCampsAllowed,
                Vacuum = vacuum,
                TerrainCapacity = terrainCapacity,
                NaturalCapacity = landCapacity,
                // Physical capacity and natural support are separate. The
                // site's realized capability decides whether this ground is
                // usable; environment alone cannot create that capability.
                LandCapacity = terrainCapacity,
                HabitatRequirementMask = habitat.RequirementMask,
                RequiredCapabilityTier = habitat.RequiredCapabilityTier,
                FoodRoute = habitat.FoodRoute
            };
            facts.SourceHash = SourceHash(facts, tile.tileId,
                surface.Mutators.Select(mutator => mutator?.defName));
            return facts;
        }

        // Regional maps preserve constituent provenance per cell. Aggregate
        // the exact represented tiles rather than treating the bookkeeping
        // anchor or Map.Biome as the whole settlement environment.
        internal static CASettlementEnvironmentFacts ForMap(Map map,
            IEnumerable<IntVec3> cells)
        {
            if (map == null) return new CASettlementEnvironmentFacts();
            var weights = new Dictionary<int, int>();
            CARegionalProjectionMapComponent projection = map
                .GetComponent<CARegionalProjectionMapComponent>();
            foreach (IntVec3 cell in cells ?? Enumerable.Empty<IntVec3>())
            {
                if (!cell.InBounds(map)) continue;
                PlanetTile source = projection?.MemberTileAt(cell)
                    ?? map.Tile;
                if (!source.Valid) source = map.Tile;
                if (!source.Valid) continue;
                weights[source.tileId] = weights.TryGetValue(source.tileId,
                    out int count) ? count + 1 : 1;
            }
            if (weights.Count == 0 && map.Tile.Valid)
                weights[map.Tile.tileId] = 1;
            List<(CASettlementEnvironmentFacts facts, int weight)> sources =
                weights.OrderBy(pair => pair.Key)
                    .Select(pair => (ForTile(pair.Key), pair.Value))
                    .Where(pair => pair.Item1.Valid).ToList();
            if (sources.Count == 0)
                return new CASettlementEnvironmentFacts();

            int totalWeight = sources.Sum(source => source.weight);
            float Weighted(Func<CASettlementEnvironmentFacts, float> value)
                => sources.Sum(source => value(source.facts) * source.weight)
                    / Math.Max(1, totalWeight);
            int WeightedInt(Func<CASettlementEnvironmentFacts, int> value)
                => Mathf.RoundToInt(Weighted(facts => value(facts)));
            string biomes = string.Join(" + ", sources.SelectMany(source =>
                    source.facts.Biomes.Split(new[] { " + " },
                        StringSplitOptions.RemoveEmptyEntries))
                .Distinct().OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
            var aggregate = new CASettlementEnvironmentFacts
            {
                Valid = true,
                Biomes = biomes.NullOrEmpty() ? "unknown" : biomes,
                SourceTiles = sources.Count,
                GrowingTwelfths = Mathf.Clamp(WeightedInt(facts =>
                    facts.GrowingTwelfths), 0, 12),
                AverageTemperature = Weighted(facts =>
                    facts.AverageTemperature),
                MinimumTemperature = sources.Min(source =>
                    source.facts.MinimumTemperature),
                MaximumTemperature = sources.Max(source =>
                    source.facts.MaximumTemperature),
                Rainfall = Weighted(facts => facts.Rainfall),
                Forageability = Weighted(facts => facts.Forageability),
                PlantDensity = Weighted(facts => facts.PlantDensity),
                AnimalDensity = Weighted(facts => facts.AnimalDensity),
                DiseasePerYear = Weighted(facts => facts.DiseasePerYear),
                FoodSupport = Weighted(facts => facts.FoodSupport),
                SeasonalThermalPressure = sources.Max(source =>
                    source.facts.SeasonalThermalPressure),
                PollutionPressure = sources.Max(source =>
                    source.facts.PollutionPressure),
                ScariaPressure = sources.Max(source =>
                    source.facts.ScariaPressure),
                PermanentDarkness = sources.Any(source =>
                    source.facts.PermanentDarkness),
                ToxicWater = sources.Any(source =>
                    source.facts.ToxicWater),
                FarmingCampsAllowed = sources.All(source =>
                    source.facts.FarmingCampsAllowed),
                Vacuum = sources.Any(source => source.facts.Vacuum),
                TerrainCapacity = Mathf.Clamp(WeightedInt(facts =>
                    facts.TerrainCapacity), 0, 3),
                NaturalCapacity = Mathf.Clamp(WeightedInt(facts =>
                    facts.NaturalCapacity), 0, 3),
                LandCapacity = Mathf.Clamp(WeightedInt(facts =>
                    facts.LandCapacity), 0, 3)
            };
            CAHabitatRequirementProfile aggregateHabitat =
                CAHabitatViabilityCausalKernel.Requirements(
                    new CAHabitatEnvironmentInput
                    {
                        GrowingTwelfths = aggregate.GrowingTwelfths,
                        FoodSupport = aggregate.FoodSupport,
                        ThermalPressure = aggregate.SeasonalThermalPressure,
                        DiseasePerYear = aggregate.DiseasePerYear,
                        PollutionPressure = aggregate.PollutionPressure,
                        ScariaPressure = aggregate.ScariaPressure,
                        PermanentDarkness = aggregate.PermanentDarkness,
                        ToxicWater = aggregate.ToxicWater,
                        FarmingCampsAllowed =
                            aggregate.FarmingCampsAllowed,
                        Vacuum = aggregate.Vacuum
                    });
            aggregate.HabitatRequirementMask =
                aggregateHabitat.RequirementMask;
            aggregate.RequiredCapabilityTier =
                aggregateHabitat.RequiredCapabilityTier;
            aggregate.FoodRoute = aggregateHabitat.FoodRoute;
            int aggregateHash = CAWorldTendencyCausalKernel.StableStringHash(
                aggregate.Biomes);
            foreach (var source in sources)
                aggregateHash = CAWorldTendencyCausalKernel.HashCombineInt(
                    aggregateHash, source.facts.SourceHash, source.weight, 0);
            aggregate.SourceHash = aggregateHash;
            return aggregate;
        }

        private static int TerrainCapacity(Tile tile)
        {
            if (tile == null || tile.WaterCovered
                || tile.PrimaryBiome == null
                || tile.PrimaryBiome.isWaterBiome
                || tile.PrimaryBiome.impassable
                || !tile.PrimaryBiome.canBuildBase)
                return 0;
            switch (tile.hilliness)
            {
                case Hilliness.Impassable: return 0;
                case Hilliness.Mountainous: return 1;
                case Hilliness.LargeHills: return 2;
                default: return 3;
            }
        }

        private static int SourceHash(CASettlementEnvironmentFacts facts,
            int tileId, IEnumerable<string> mutators)
        {
            int hash = CAWorldTendencyCausalKernel.StableStringHash(
                facts.Biomes ?? "unknown");
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash, tileId,
                facts.GrowingTwelfths, facts.LandCapacity);
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                Mathf.RoundToInt(facts.AverageTemperature * 100f),
                Mathf.RoundToInt(facts.MinimumTemperature * 100f),
                Mathf.RoundToInt(facts.MaximumTemperature * 100f));
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                Mathf.RoundToInt(facts.Rainfall),
                Mathf.RoundToInt(facts.PlantDensity * 10000f),
                Mathf.RoundToInt(facts.Forageability * 10000f));
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                Mathf.RoundToInt(facts.AnimalDensity * 10000f),
                Mathf.RoundToInt(facts.DiseasePerYear * 10000f),
                Mathf.RoundToInt(facts.FoodSupport * 10000f));
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                Mathf.RoundToInt(facts.PollutionPressure * 10000f),
                Mathf.RoundToInt(facts.ScariaPressure * 10000f),
                facts.HabitatRequirementMask);
            hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                facts.RequiredCapabilityTier, (int)facts.FoodRoute,
                facts.NaturalCapacity);
            foreach (string mutator in (mutators ?? Enumerable.Empty<string>())
                .Where(name => !name.NullOrEmpty()).OrderBy(name => name,
                    StringComparer.Ordinal))
                hash = CAWorldTendencyCausalKernel.HashCombineInt(hash,
                    CAWorldTendencyCausalKernel.StableStringHash(mutator));
            return hash;
        }

        private static bool HasToxicWater(BiomeDef biome)
        {
            if (biome == null) return false;
            return WaterTerrains(biome).Any(terrain =>
                terrain != null && terrain.toxicBuildupFactor > 0f);
        }

        private static IEnumerable<TerrainDef> WaterTerrains(BiomeDef biome)
        {
            yield return biome.waterShallowTerrain;
            yield return biome.waterDeepTerrain;
            yield return biome.oceanShallowTerrain;
            yield return biome.oceanDeepTerrain;
            yield return biome.waterMovingShallowTerrain;
            yield return biome.waterMovingChestDeepTerrain;
        }
    }
}
