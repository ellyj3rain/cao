using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.SketchGen;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Critical-structure workers normally run only from map.TileInfo, which is
    // the bookkeeping anchor on a regional map. ArcheanTrees is adapted once
    // per selected carrier and every converted cell is constrained to that
    // carrier's visible land. Other anchor workers retain their native pass.
    [HarmonyPatch(typeof(GenStep_MutatorCriticalStructures),
        nameof(GenStep_MutatorCriticalStructures.Generate))]
    internal static class CARegionalCriticalMutatorPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Map map)
        {
            CARegionalProjectionMapComponent projection = map
                ?.GetComponent<CARegionalProjectionMapComponent>();
            if (projection?.Active != true) return true;

            foreach (TileMutatorDef mutator in map.TileInfo.Mutators)
            {
                TileMutatorWorker worker = mutator?.Worker;
                if (worker == null
                    || worker is TileMutatorWorker_ArcheanTrees
                    || worker is TileMutatorWorker_Harbor
                    || worker is TileMutatorWorker_AncientStructure
                    || worker is TileMutatorWorker_AbandonedColony)
                    continue;
                worker.GenerateCriticalStructures(map);
            }

            string receipt = CARegionalArcheanTreeAdapter.Generate(map,
                projection);
            Log.Message("[CA][Regional] selected-area Archean trees: "
                + receipt);
            string harborReceipt = projection.ApplyProjectedHarbors();
            if (harborReceipt != null)
                Log.Message("[CA][Regional] projected harbors: "
                    + harborReceipt);
            string structureReceipt =
                projection.ApplyProjectedAncientStructures();
            if (structureReceipt != null)
                Log.Message("[CA][Regional] projected ancient structures: "
                    + structureReceipt);
            string colonyReceipt =
                projection.ApplyProjectedAbandonedColonies();
            if (colonyReceipt != null)
                Log.Message("[CA][Regional] projected abandoned colonies: "
                    + colonyReceipt);
            return false;
        }
    }

    internal static class CARegionalArcheanTreeAdapter
    {
        private static readonly IntRange TreeRange = new IntRange(10, 15);

        internal static string Generate(Map map,
            CARegionalProjectionMapComponent projection)
        {
            CARegionalPlan region = projection?.Region;
            if (!ModsConfig.OdysseyActive || map == null
                || region?.memberTileIds == null)
                return "inactive";

            var receipts = new List<string>();
            int carriers = 0;
            foreach (int tileId in region.memberTileIds.Distinct()
                .OrderBy(id => id))
            {
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(tileId);
                Tile info = tile.Valid ? tile.Tile : null;
                if (info == null) continue;
                int ordinal = 0;
                foreach (TileMutatorDef mutator in info.Mutators.Where(def =>
                    def?.Worker is TileMutatorWorker_ArcheanTrees)
                    .OrderBy(def => def.defName))
                {
                    carriers++;
                    var instance = new CAFeatureInstance(region.candidateId,
                        -1, tileId, mutator.defName, ordinal++);
                    receipts.Add(GenerateOne(map, projection, tile,
                        instance));
                }
            }
            return carriers == 0
                ? "no selected area carries ArcheanTrees"
                : carriers + " carrier" + (carriers == 1 ? "" : "s")
                    + "; " + string.Join(" | ", receipts);
        }

        private static string GenerateOne(Map map,
            CARegionalProjectionMapComponent projection, PlanetTile carrier,
            CAFeatureInstance instance)
        {
            CAFeatureRandom.Push(instance, 0x41524348); // "ARCH"
            try
            {
                CompProperties_Terraformer props = ThingDefOf
                    .Plant_TreeArchean
                    .GetCompProperties<CompProperties_Terraformer>();
                float radius = props?.radius ?? 0f;
                if (radius <= 0f)
                    return "tile " + carrier.tileId
                        + " FAILED: Archean tree has no terraformer radius";
                int target = TreeRange.RandomInRange;
                int spawned = 0;
                int richSoil = 0;
                foreach (IntVec3 cell in map.AllCells.InRandomOrder())
                {
                    if (!OwnedBy(projection, carrier.tileId, cell)
                        || !ValidPosition(cell, map, projection,
                            carrier.tileId, radius))
                        continue;
                    Plant tree = WildPlantSpawner.SpawnPlant(
                        ThingDefOf.Plant_TreeArchean, map, cell,
                        setRandomGrowth: false);
                    if (tree == null) continue;
                    tree.Growth = 1f;
                    int radialCells = GenRadial.NumCellsInRadius(radius
                        + 1.9f);
                    for (int i = 0; i < radialCells; i++)
                    {
                        IntVec3 nearby = cell + GenRadial.RadialPattern[i];
                        if (!nearby.InBounds(map)
                            || !OwnedBy(projection, carrier.tileId, nearby)
                            || !CompTerraformer.CanEverConvertCell(nearby,
                                map))
                            continue;
                        float distance = (nearby - cell).Magnitude;
                        if (distance > radius && !Rand.Chance(0.33f))
                            continue;
                        map.terrainGrid.SetTerrain(nearby,
                            TerrainDefOf.SoilRich);
                        richSoil++;
                    }
                    MapGenerator.UsedRects.Add(cell.RectAbout(
                        (int)radius / 2, (int)radius / 2));
                    spawned++;
                    if (spawned >= target) break;
                }
                return "tile " + carrier.tileId + " -> " + spawned + "/"
                    + target + " mature trees, " + richSoil
                    + " owned rich-soil writes";
            }
            catch (Exception exception)
            {
                return "tile " + carrier.tileId + " FAILED: "
                    + exception.GetType().Name + " " + exception.Message;
            }
            finally
            {
                CAFeatureRandom.Pop();
            }
        }

        private static bool ValidPosition(IntVec3 center, Map map,
            CARegionalProjectionMapComponent projection, int carrierTileId,
            float radius)
        {
            if (!CompTerraformer.CanEverConvertCell(center, map)) return false;
            int count = 0;
            // The spawned tree keeps its native CompTerraformer, which runs on
            // later rare ticks. Its entire possible conversion footprint must
            // therefore belong to this carrier; masking only the initial soil
            // writes would allow later native ticks to cross the seam.
            int radialCells = GenRadial.NumCellsInRadius(radius + 1.9f);
            for (int i = 0; i < radialCells; i++)
            {
                IntVec3 nearby = center + GenRadial.RadialPattern[i];
                if (!nearby.InBounds(map)
                    || !OwnedBy(projection, carrierTileId, nearby))
                    return false;
                if (CompTerraformer.CanEverConvertCell(nearby, map)) count++;
            }
            return count >= 5;
        }

        private static bool OwnedBy(
            CARegionalProjectionMapComponent projection, int tileId,
            IntVec3 cell)
        {
            PlanetTile owner = projection.MemberTileAt(cell);
            return owner.Valid && owner.tileId == tileId;
        }
    }

    // Non-critical structure workers normally run only from the anchor
    // tile. Vents, ruins, and the ancient structure's perimeter scatter
    // are driven per carrying area instead; other anchor workers keep
    // their native pass.
    [HarmonyPatch(typeof(GenStep_MutatorNonCriticalStructures),
        nameof(GenStep_MutatorNonCriticalStructures.Generate))]
    internal static class CARegionalNonCriticalMutatorPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Map map)
        {
            CARegionalProjectionMapComponent projection = map
                ?.GetComponent<CARegionalProjectionMapComponent>();
            if (projection?.Active != true) return true;

            foreach (TileMutatorDef mutator in map.TileInfo.Mutators)
            {
                TileMutatorWorker worker = mutator?.Worker;
                if (worker == null
                    || worker is TileMutatorWorker_AncientVent
                    || worker is TileMutatorWorker_AncientRuins
                    || worker is TileMutatorWorker_AncientStructure)
                    continue;
                worker.GenerateNonCriticalStructures(map);
            }

            string ventReceipt = projection.ApplyProjectedAncientVents();
            if (ventReceipt != null)
                Log.Message("[CA][Regional] projected ancient vents: "
                    + ventReceipt);
            string ruinReceipt = projection.ApplyProjectedAncientRuins();
            if (ruinReceipt != null)
                Log.Message("[CA][Regional] projected ancient ruins: "
                    + ruinReceipt);
            string scatterReceipt =
                projection.ApplyProjectedAncientStructureScatter();
            if (scatterReceipt != null)
                Log.Message("[CA][Regional] projected structure "
                    + "perimeters: " + scatterReceipt);
            return false;
        }
    }

    // GenStep_MutatorFinal normally sees only map.TileInfo.Mutators. On a
    // regional map TileInfo is deliberately anchored to one bookkeeping tile,
    // so a stockpile carried by any other selected area silently disappears.
    // Stockpile is adapted explicitly rather than running every member's
    // post-fog workers against the aggregate map: its native placement contract
    // is understood, bounded, and can be verified cell-for-cell.
    [HarmonyPatch(typeof(GenStep_MutatorFinal),
        nameof(GenStep_MutatorFinal.Generate))]
    internal static class CARegionalFinalMutatorPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Map map)
        {
            CARegionalProjectionMapComponent projection = map
                ?.GetComponent<CARegionalProjectionMapComponent>();
            if (projection?.Active != true) return true;

            // Preserve native final workers on the regional anchor except
            // for Stockpile, quarries, and uplinks, which are driven per
            // actual carrier below.
            foreach (TileMutatorDef mutator in map.TileInfo.Mutators)
            {
                TileMutatorWorker worker = mutator?.Worker;
                if (worker == null
                    || worker is TileMutatorWorker_Stockpile
                    || worker is TileMutatorWorker_AncientQuarry
                    || worker is TileMutatorWorker_AncientUplink) continue;
                worker.GeneratePostFog(map);
            }

            string receipt = CARegionalStockpileAdapter.Generate(map,
                projection);
            Log.Message("[CA][Regional] selected-area stockpiles: " + receipt);
            string quarryReceipt = projection.ApplyProjectedAncientQuarries();
            if (quarryReceipt != null)
                Log.Message("[CA][Regional] projected ancient quarries: "
                    + quarryReceipt);
            string uplinkReceipt = projection.ApplyProjectedAncientUplinks();
            if (uplinkReceipt != null)
                Log.Message("[CA][Regional] projected ancient uplinks: "
                    + uplinkReceipt);
            return false;
        }
    }

    internal static class CARegionalStockpileAdapter
    {
        internal static string Generate(Map map,
            CARegionalProjectionMapComponent projection)
        {
            CARegionalPlan region = projection?.Region;
            if (!ModsConfig.OdysseyActive || map == null
                || region?.memberTileIds == null)
                return "inactive";

            var receipts = new List<string>();
            int carriers = 0;
            foreach (int tileId in region.memberTileIds.Distinct()
                .OrderBy(id => id))
            {
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(tileId);
                Tile info = tile.Valid ? tile.Tile : null;
                if (info == null) continue;
                int ordinal = 0;
                foreach (TileMutatorDef mutator in info.Mutators.Where(def =>
                    def?.Worker is TileMutatorWorker_Stockpile)
                    .OrderBy(def => def.defName))
                {
                    carriers++;
                    var instance = new CAFeatureInstance(region.candidateId,
                        -1, tileId, mutator.defName, ordinal++);
                    receipts.Add(GenerateOne(map, projection, tile, instance));
                }
            }
            return carriers == 0 ? "no selected area carries Stockpile"
                : carriers + " carrier" + (carriers == 1 ? "" : "s")
                    + "; " + string.Join(" | ", receipts);
        }

        private static string GenerateOne(Map map,
            CARegionalProjectionMapComponent projection, PlanetTile carrier,
            CAFeatureInstance instance)
        {
            CAFeatureRandom.Push(instance, 0x53544F43); // "STOC"
            try
            {
                List<CellRect> usedRects = MapGenerator
                    .GetOrGenerateVar<List<CellRect>>("UsedRects");
                SketchResolveParams parms = default(SketchResolveParams);
                parms.sketch = new Sketch();
                Sketch sketch = RimWorld.SketchGen.SketchGen.Generate(
                    SketchResolverDefOf.AncientHatch, parms);

                CellRect rect;
                bool relaxedFog = false;
                if (!TryFindRect(map, projection, carrier.tileId, sketch,
                        usedRects, true, out rect))
                {
                    relaxedFog = true;
                    if (!TryFindRect(map, projection, carrier.tileId, sketch,
                            usedRects, false, out rect))
                        return "tile " + carrier.tileId
                            + " FAILED: no clear owned rect for "
                            + sketch.OccupiedSize;
                }

                var spawned = new List<Thing>();
                sketch.Spawn(map, rect.Min, null,
                    Sketch.SpawnPosType.Unchanged, Sketch.SpawnMode.Normal,
                    wipeIfCollides: true, forceTerrainAffordance: false,
                    clearEdificeWhereFloor: true, spawned);
                usedRects.Add(rect);
                AncientHatch hatch = spawned.OfType<AncientHatch>()
                    .FirstOrDefault();
                if (hatch == null)
                    return "tile " + carrier.tileId + " FAILED: sketch at "
                        + rect + " spawned no AncientHatch";
                hatch.stockpileType = StockpileTypeFor(carrier);

                bool ownershipVerified = rect.Cells.All(cell =>
                    projection.MemberTileAt(cell).tileId == carrier.tileId);
                return "tile " + carrier.tileId + " -> "
                    + hatch.stockpileType + " hatch at " + hatch.Position
                    + ", occupied rect " + rect + ", ownership "
                    + (ownershipVerified ? "verified" : "FAILED")
                    + (relaxedFog ? ", fog constraint relaxed after native "
                        + "search found no site" : ", native visibility kept");
            }
            catch (Exception exception)
            {
                return "tile " + carrier.tileId + " FAILED: "
                    + exception.GetType().Name + " " + exception.Message;
            }
            finally
            {
                CAFeatureRandom.Pop();
            }
        }

        private static bool TryFindRect(Map map,
            CARegionalProjectionMapComponent projection, int carrierTileId,
            Sketch sketch, List<CellRect> usedRects, bool requireUnfogged,
            out CellRect rect)
        {
            Predicate<CellRect> validator = candidate =>
            {
                if (!candidate.InBounds(map)) return false;
                foreach (IntVec3 cell in candidate.Cells)
                {
                    PlanetTile owner = projection.MemberTileAt(cell);
                    if (!owner.Valid || owner.tileId != carrierTileId)
                        return false;
                    if (requireUnfogged && cell.Fogged(map)) return false;
                }
                if (usedRects.Any(used => used.Overlaps(candidate)))
                    return false;
                return !sketch.IsSpawningBlocked(map, candidate.Min, null);
            };

            if (MapGenUtility.TryGetRandomClearRect(sketch.OccupiedSize.x,
                    sketch.OccupiedSize.z, out rect, -1, -1, validator))
                return true;
            IntVec3 center;
            if (CellFinder.TryFindRandomCell(map, cell => validator(
                    CellRect.CenteredOn(cell, sketch.OccupiedSize)), out center))
            {
                rect = CellRect.CenteredOn(center, sketch.OccupiedSize);
                return true;
            }
            rect = default(CellRect);
            return false;
        }

        // Exact native identity rule: the list and tile-seeded random choice
        // are copied from TileMutatorWorker_Stockpile.GetStockpileType.
        private static TileMutatorWorker_Stockpile.StockpileType
            StockpileTypeFor(PlanetTile tile)
        {
            Rand.PushState(tile.tileId);
            try
            {
                return TileMutatorWorker_Stockpile
                    .GeneratableStockpileTypes.RandomElement();
            }
            finally
            {
                Rand.PopState();
            }
        }
    }

    // WildPlantSpawner builds both its candidate list and commonality cache
    // from the bookkeeping anchor. The catalog unions selected member-carried
    // additions into those caches, then the final choice seam uses its actual
    // cell to keep each added species on its carrying area's visible ground.
    internal static class CARegionalAdditionalWildPlants
    {
        private sealed class Catalog
        {
            internal readonly Dictionary<int, HashSet<ThingDef>> ByTile =
                new Dictionary<int, HashSet<ThingDef>>();
            internal readonly Dictionary<ThingDef, float> Union =
                new Dictionary<ThingDef, float>();
        }

        private static readonly ConditionalWeakTable<Map, Catalog> Catalogs =
            new ConditionalWeakTable<Map, Catalog>();

        internal static void AddToCommonalities(Map map,
            Dictionary<ThingDef, float> commonalities)
        {
            if (commonalities == null) return;
            Catalog catalog = For(map);
            foreach (KeyValuePair<ThingDef, float> pair in catalog.Union)
            {
                float existing;
                if (commonalities.TryGetValue(pair.Key, out existing))
                    commonalities[pair.Key] = Mathf.Max(existing, pair.Value);
                else commonalities.Add(pair.Key, pair.Value);
            }
        }

        internal static void AddToCandidateList(Map map,
            List<ThingDef> plants)
        {
            if (plants == null) return;
            foreach (ThingDef plant in For(map).Union.Keys)
                if (!plants.Contains(plant)) plants.Add(plant);
        }

        internal static float ChoiceMultiplier(Map map, IntVec3 cell,
            ThingDef plant)
        {
            if (map == null || plant == null) return 1f;
            Catalog catalog = For(map);
            if (!catalog.Union.ContainsKey(plant)) return 1f;

            // A species native to the projected biome remains native there;
            // the carrier filter applies only to the additional-mutator route.
            BiomeDef biome = map.BiomeAt(cell);
            if (biome?.AllWildPlants?.Contains(plant) == true) return 1f;

            CARegionalProjectionMapComponent projection = map
                .GetComponent<CARegionalProjectionMapComponent>();
            if (projection?.Active != true) return 1f;
            PlanetTile owner = projection.MemberTileAt(cell);
            HashSet<ThingDef> owned;
            bool represented = owner.Valid
                && catalog.ByTile.TryGetValue(owner.tileId, out owned)
                && owned.Contains(plant);
            return represented ? 1f : 0f;
        }

        private static Catalog For(Map map)
        {
            if (map == null) return new Catalog();
            return Catalogs.GetValue(map, Build);
        }

        private static Catalog Build(Map map)
        {
            var catalog = new Catalog();
            CARegionalProjectionMapComponent projection = map
                ?.GetComponent<CARegionalProjectionMapComponent>();
            CARegionalPlan region = projection?.Active == true
                ? projection.Region : null;
            if (region?.memberTileIds == null) return catalog;

            foreach (int tileId in region.memberTileIds.Distinct())
            {
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(tileId);
                Tile info = tile.Valid ? tile.Tile : null;
                if (info == null) continue;
                var local = new HashSet<ThingDef>();
                foreach (TileMutatorDef mutator in info.Mutators)
                {
                    IEnumerable<BiomePlantRecord> records = mutator?.Worker
                        ?.AdditionalWildPlants(tile)
                        ?? Enumerable.Empty<BiomePlantRecord>();
                    foreach (BiomePlantRecord record in records)
                    {
                        if (record?.plant == null) continue;
                        local.Add(record.plant);
                        float existing;
                        if (catalog.Union.TryGetValue(record.plant,
                                out existing))
                            catalog.Union[record.plant] = Mathf.Max(existing,
                                record.commonality);
                        else catalog.Union.Add(record.plant,
                            record.commonality);
                    }
                }
                if (local.Count > 0) catalog.ByTile[tileId] = local;
            }
            return catalog;
        }
    }

    [HarmonyPatch(typeof(WildPlantSpawner),
        "CachePlantCommonalitiesIfShould")]
    internal static class CARegionalAdditionalPlantCommonalityPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Map ___map,
            Dictionary<ThingDef, float> ___cachedPlantCommonalities)
        {
            CARegionalAdditionalWildPlants.AddToCommonalities(___map,
                ___cachedPlantCommonalities);
        }
    }

    [HarmonyPatch(typeof(WildPlantSpawner),
        nameof(WildPlantSpawner.MutatorWildPlants), MethodType.Getter)]
    internal static class CARegionalAdditionalPlantCandidatesPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Map ___map, List<ThingDef> __result)
        {
            CARegionalAdditionalWildPlants.AddToCandidateList(___map,
                __result);
        }
    }

    [HarmonyPatch(typeof(WildPlantSpawner), "PlantChoiceWeight")]
    internal static class CARegionalAdditionalPlantChoicePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Map ___map, ThingDef plantDef, IntVec3 c,
            ref float __result)
        {
            __result *= CARegionalAdditionalWildPlants.ChoiceMultiplier(
                ___map, c, plantDef);
        }
    }

    // AnimalHabitat's incident hook has no cell. On a regional map the honest
    // representation is therefore region-level: selected habitat carriers are
    // aggregated by their resolved visible land area, and one carrier supplies
    // the native 50% habitat roll. This replaces root-tile accident with a
    // defined spatial policy without pretending an incident has a location it
    // does not carry.
    [HarmonyPatch(typeof(AggressiveAnimalIncidentUtility),
        "TryGetHabitatAnimal")]
    internal static class CARegionalHabitatIncidentPatch
    {
        private sealed class Candidate
        {
            internal PlanetTile Tile;
            internal PawnKindDef Animal;
            internal float Weight;
        }

        [HarmonyPrefix]
        private static bool Prefix(PlanetTile tile,
            ref PawnKindDef animalKind, ref bool __result)
        {
            if (!ModsConfig.OdysseyActive || !tile.Valid) return true;
            Map map = Find.Maps.FirstOrDefault(candidate =>
            {
                if (candidate.Tile != tile) return false;
                try
                {
                    return candidate
                        .GetComponent<CARegionalProjectionMapComponent>()
                        ?.Active == true;
                }
                catch { return false; }
            });
            if (map == null) return true;

            CARegionalProjectionMapComponent projection = map
                .GetComponent<CARegionalProjectionMapComponent>();
            CARegionalPlan region = projection.Region;
            var candidates = new List<Candidate>();
            foreach (int tileId in region.memberTileIds.Distinct())
            {
                PlanetTile carrier = CARegionalPlanUtility.SurfaceTile(tileId);
                Tile info = carrier.Valid ? carrier.Tile : null;
                if (info == null) continue;
                foreach (TileMutatorDef mutator in info.Mutators)
                {
                    var worker = mutator?.Worker
                        as TileMutatorWorker_AnimalHabitat;
                    if (worker == null) continue;
                    PawnKindDef animal = worker.GetAnimalKind(carrier);
                    if (animal?.RaceProps?.Animal != true
                        || !animal.canArriveManhunter
                        || !animal.RaceProps.CanPassFences) continue;
                    candidates.Add(new Candidate
                    {
                        Tile = carrier,
                        Animal = animal,
                        Weight = Mathf.Max(1f,
                            projection.ProjectedAreaFor(carrier))
                    });
                }
            }
            if (candidates.Count == 0) return true;

            Candidate selected;
            if (!candidates.TryRandomElementByWeight(item => item.Weight,
                    out selected))
                selected = candidates[0];
            animalKind = selected.Animal;
            __result = Rand.Chance(0.5f);
            if (__result)
                Log.Message("[CA][Regional] AnimalHabitat incident selected "
                    + animalKind.defName + " from tile "
                    + selected.Tile.tileId + " by projected-land-area weight");
            else animalKind = null;
            return false;
        }
    }
}
