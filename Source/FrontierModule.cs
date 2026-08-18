using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonistAwareness
{
    // A standard map saves its frontier realization the first time that map is
    // processed. Regional maps use the holding rows saved on their region plan.
    public sealed class CAFrontierMapPlan : IExposable
    {
        public const int CurrentSchemaVersion = 2;
        public int schemaVersion = CurrentSchemaVersion;
        public int mapId = -1;
        public int mapTileId = -1;
        public int mapWidth;
        public int mapHeight;
        public int realizationSourceHash;
        public List<CAFrontierHoldingPlan> holdings =
            new List<CAFrontierHoldingPlan>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion",
                CurrentSchemaVersion);
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref mapTileId, "mapTileId", -1);
            Scribe_Values.Look(ref mapWidth, "mapWidth", 0);
            Scribe_Values.Look(ref mapHeight, "mapHeight", 0);
            Scribe_Values.Look(ref realizationSourceHash,
                "realizationSourceHash", 0);
            Scribe_Collections.Look(ref holdings, "holdings", LookMode.Deep);
            if (holdings == null)
                holdings = new List<CAFrontierHoldingPlan>();
        }
    }

    public static class CAFrontier
    {
        private static int HoldingsFor(CAOrganizationWorldComponent comp,
            Map map)
        {
            CARegionalPlan plan = CARegionalWorldComponent.Current
                ?.FindRegionForMap(map);
            if (plan?.frontierHoldings != null)
                return Mathf.Clamp(plan.frontierHoldings.Count, 0, 8);
            CAFrontierMapPlan mapPlan = comp?.EnsureFrontierMapPlan(map);
            return Mathf.Clamp(mapPlan?.holdings?.Count ?? 0, 0, 8);
        }

        private static CAFrontierHoldingPlan HoldingPlanFor(
            CAOrganizationWorldComponent comp, Map map, int index)
        {
            CARegionalPlan plan = CARegionalWorldComponent.Current
                ?.FindRegionForMap(map);
            if (plan?.frontierHoldings != null
                && index >= 0 && index < plan.frontierHoldings.Count)
                return plan.frontierHoldings[index];
            CAFrontierMapPlan mapPlan = comp?.EnsureFrontierMapPlan(map);
            return mapPlan?.holdings != null && index >= 0
                && index < mapPlan.holdings.Count
                    ? mapPlan.holdings[index] : null;
        }

        public static void EnsureHoldings(CAOrganizationWorldComponent comp)
        {
            if (comp == null || Current.Game == null) return;
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                if (!map.IsPlayerHome) continue;
                int existing = 0;
                for (int index = 0; index < HoldingsFor(comp, map); index++)
                {
                    CAFrontierHoldingPlan holding = HoldingPlanFor(comp, map,
                        index);
                    if (holding?.materialized == true
                        && holding.materializedMapId == map.uniqueID)
                        existing++;
                }
                int want = HoldingsFor(comp, map);
                // New maps seed saved holdings promptly. Older maps add one
                // saved holding per pulse after the initial delay.
                if (existing < want && Find.TickManager.TicksGame
                    - map.generationTick < 20000)
                {
                    // [perf] Frontier holdings are still generated with the map,
                    // but two holdings per pulse instead of eight in
                    // one tick: each site costs a reachability flood
                    // across the whole map, and on a two-million-cell
                    // region eight of those in a single frame is a
                    // visible freeze. Four pulses inside the window
                    // seat the full complement.
                    var taken = new List<IntVec3>();
                    int spawned = 0;
                    for (int index = 0; index < want && spawned < 2;
                        index++)
                    {
                        CAFrontierHoldingPlan holding = HoldingPlanFor(comp,
                            map, index);
                        if (holding == null || holding.materialized) continue;
                        if (!TrySeedHolding(map, holding, taken)) break;
                        spawned++;
                    }
                }
                else if (existing < want
                    && Find.TickManager.TicksGame > 120000)
                {
                    for (int index = 0; index < want; index++)
                    {
                        CAFrontierHoldingPlan holding = HoldingPlanFor(comp,
                            map, index);
                        if (holding == null || holding.materialized) continue;
                        TrySeedHolding(map, holding);
                        break;
                    }
                }
            }
        }

        private static bool TrySeedHolding(Map map,
            CAFrontierHoldingPlan holding, List<IntVec3> taken = null)
        {
            if (map == null || holding == null) return false;
            CARegionalPlan region = CARegionalWorldComponent.Current
                ?.FindRegionForMap(map);
            CASettlementEnvironmentFacts environment =
                CASettlementEnvironment.ForTile(holding.memberTileId);
            if (!CAHabitatViability.ValidateFrontier(holding, environment,
                    out string habitatFailure, region))
                return BlockMaterialization(holding, habitatFailure);

            bool supportDeclared = holding.supportingFactionKey >= 0
                || holding.supportingFactionLoadId >= 0;
            Faction flag = CAHabitatViability.ResolveSupporter(region,
                holding);
            if (supportDeclared && flag == null)
                return BlockMaterialization(holding,
                    "the saved supporting faction is unavailable");
            int resolvedTier = flag == null ? 0
                : CAHabitatViability.KnowledgeCompatibilityTier(flag);
            if (resolvedTier != holding.capabilityTier)
                return BlockMaterialization(holding,
                    "the saved supporting capability no longer matches its faction");

            IntVec3 site;
            IntVec3 preferred = IntVec3.Invalid;
            if (holding.memberTileId >= 0)
                preferred = map.GetComponent<CARegionalProjectionMapComponent>()
                    ?.CenterForMember(holding.memberTileId)
                    ?? IntVec3.Invalid;
            int siteSeed = Gen.HashCombineInt(map.uniqueID,
                holding.key, holding.memberTileId,
                509203);
            Rand.PushState(siteSeed);
            try
            {
                if (!TryFindSite(map, holding, environment, out site,
                        taken, preferred))
                    return BlockMaterialization(holding,
                        "no site can hold the required compact habitat");
            }
            finally { Rand.PopState(); }

            if (!TryMaterializeHabitat(map, site, flag, holding,
                    environment, out List<Thing> habitat,
                    out List<IntVec3> roofs, out habitatFailure))
                return BlockMaterialization(holding, habitatFailure);

            var folk = new List<Pawn>();
            int count = Mathf.Clamp(holding.residentCount, 1, 6);
            PawnKindDef kind = PawnKindDefOf.Villager;
            if (flag != null)
                try
                {
                    PawnKindDef fk = flag.RandomPawnKind();
                    if (fk != null && fk.RaceProps != null
                        && fk.RaceProps.Humanlike) kind = fk;
                }
                catch { }
            for (int i = 0; i < count; i++)
            {
                try
                {
                    Pawn p = PawnGenerator.GeneratePawn(kind, flag);
                    IntVec3 spot;
                    if (!CellFinder.TryFindRandomCellNear(site, map, 4,
                        c => c.Standable(map) && !c.Fogged(map),
                        out spot)) spot = site;
                    GenSpawn.Spawn(p, spot, map);
                    folk.Add(p);
                }
                catch { }
            }
            if (folk.Count == 0)
            {
                RollBackHabitat(map, habitat, roofs);
                return BlockMaterialization(holding,
                    "no resident could be materialized for the saved habitat");
            }
            taken?.Add(site);

            // The compact habitat above is the viability proof. Morphology
            // expands it into the saved cabin or homestead form, but cannot
            // turn a failed shelter, food, water, or medical path into a
            // successful holding.
            try
            {
                CAMorphologyAdapter.Materialize(map,
                    CellRect.CenteredOn(site, 26, 26).ClipInsideMap(map),
                    holding.form == 1
                        ? CAMorphForm.FrontierHomestead
                        : CAMorphForm.Cabin,
                    siteSeed, flag);
            }
            catch { }

            var residentIds = new List<int>();
            for (int i = 0; i < folk.Count; i++)
                residentIds.Add(folk[i].thingIDNumber);
            holding.materialized = true;
            holding.materializedMapId = map.uniqueID;
            holding.site = site;
            holding.residentPawnIds = residentIds;
            holding.materializationFailure = null;

            Messages.Message((flag == null
                ? "An unaffiliated frontier site has been established nearby"
                : "A frontier site affiliated with " + flag.Name
                    + " has been established nearby")
                + ": " + folk.Count + (folk.Count == 1
                    ? " resident." : " residents."),
                new LookTargets(site, map),
                MessageTypeDefOf.NeutralEvent, false);
            return true;
        }

        private static bool BlockMaterialization(
            CAFrontierHoldingPlan holding, string failure)
        {
            if (holding != null)
                holding.materializationFailure = failure.NullOrEmpty()
                    ? "frontier habitat materialization failed" : failure;
            return false;
        }

        // Battlefield parley consumes this faction-side reading of an
        // unarmed approach. It does not create frontier social state.
        internal static float ReadUnarmed(Faction aggressor, out string note)
        {
            note = null;
            if (aggressor?.def == null) return 0f;
            try
            {
                if (aggressor.def.permanentEnemy
                    || !aggressor.def.humanlikeFaction)
                {
                    note = "they read an unarmed figure as prey -0.10";
                    return -0.1f;
                }
                if (aggressor.def.naturalEnemy)
                {
                    note = "they read it as weakness -0.05";
                    return -0.05f;
                }
                Ideo ideo = aggressor.ideos?.PrimaryIdeo;
                if (ideo != null)
                    for (int i = 0; i < ideo.memes.Count; i++)
                    {
                        string defName = ideo.memes[i].defName;
                        if (defName == "Raider" || defName == "Supremacist")
                        {
                            note = "their creed reads it as weakness -0.10";
                            return -0.1f;
                        }
                    }
                note = "they read it as good faith +0.10";
                return 0.1f;
            }
            catch
            {
                note = null;
                return 0f;
            }
        }

        private static bool TryFindSite(Map map,
            CAFrontierHoldingPlan holding,
            CASettlementEnvironmentFacts environment, out IntVec3 site,
            List<IntVec3> taken = null,
            IntVec3 preferredCenter = default(IntVec3))
        {
            site = IntVec3.Invalid;
            IntVec3 home = map.Center;
            try
            {
                var cols = map.mapPawns.FreeColonistsSpawned;
                if (cols.Count > 0) home = cols[0].Position;
            }
            catch { }
            CARegionalWorldComponent regional =
                CARegionalWorldComponent.Current;
            // Reachability floods are the expensive check on a giant map:
            // fewer attempts there, and every cheap filter runs first.
            int maxTries = map.Size.x * map.Size.z > 1000000 ? 80 : 220;
            bool found = false;
            CAAutonomousBuildingPatternEvidence best =
                default(CAAutonomousBuildingPatternEvidence);
            for (int tries = 0; tries < maxTries; tries++)
            {
                IntVec3 c = IntVec3.Invalid;
                if (preferredCenter.IsValid
                    && !CellFinder.TryFindRandomCellNear(preferredCenter,
                        map, Math.Max(35,
                            Math.Min(map.Size.x, map.Size.z) / 4),
                        cell => cell.InBounds(map), out c))
                    c = CellFinder.RandomCell(map);
                else if (!preferredCenter.IsValid)
                    c = CellFinder.RandomCell(map);
                if (!c.Standable(map) || c.Fogged(map)) continue;
                if (c.Roofed(map)) continue;
                if (c.DistanceTo(home) < 60f) continue;
                if (!CanHoldCompactHabitat(map, c, holding?.form ?? 0))
                    continue;
                // Birth batches pick several sites in one pass: keep
                // sibling homesteads off each other's ground.
                bool crowded = false;
                if (taken != null)
                    for (int i = 0; i < taken.Count; i++)
                        if (c.InHorDistOf(taken[i], 40f))
                        { crowded = true; break; }
                if (crowded) continue;
                bool nearSettlement = false;
                if (regional != null)
                    for (int i = 0; i < regional.Records.Count; i++)
                    {
                        CARegionalSettlementRecord r = regional.Records[i];
                        if (r.lastMapId != map.uniqueID
                            || r.localRect == CellRect.Empty) continue;
                        if (r.localRect.ExpandedBy(35).Contains(c))
                        { nearSettlement = true; break; }
                    }
                if (nearSettlement) continue;
                if (!map.reachability.CanReachMapEdge(c,
                    TraverseParms.For(TraverseMode.PassDoors))) continue;
                CAAutonomousBuildingPatternEvidence evidence =
                    FrontierPatternEvidence(map, c, holding, environment,
                        tries);
                if (!found || CAAutonomousBuildingPatternKernel
                    .PreferFrontier(evidence, best))
                {
                    found = true;
                    site = c;
                    best = evidence;
                }
            }
            if (found)
                Log.Message("[CA] frontier autonomous siting chose " + site
                    + ": " + best.Receipt());
            return found;
        }

        private static bool CanHoldCompactHabitat(Map map, IntVec3 center,
            int form)
        {
            int radius = form == 1 ? 5 : 4;
            CellRect rect = CellRect.CenteredOn(center,
                radius * 2 + 1, radius * 2 + 1);
            if (rect != rect.ClipInsideMap(map)) return false;
            foreach (IntVec3 cell in rect)
            {
                if (!cell.InBounds(map) || cell.Fogged(map)
                    || cell.Roofed(map) || !cell.Walkable(map))
                    return false;
                TerrainDef terrain = cell.GetTerrain(map);
                if (terrain == null || terrain.IsWater) return false;
                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                    if (things[i] is Pawn
                        || things[i].def.category == ThingCategory.Building
                        || things[i].def.category == ThingCategory.Item)
                        return false;
            }
            return true;
        }

        private static CAAutonomousBuildingPatternEvidence
            FrontierPatternEvidence(Map map, IntVec3 center,
                CAFrontierHoldingPlan holding,
                CASettlementEnvironmentFacts environment, int stableOrder)
        {
            int fit = 0, circulation = 0, expansion = 0;
            int fertile = 0, naturalCover = 0, obstacles = 0;
            int northEast = 0, northWest = 0;
            int southEast = 0, southWest = 0;
            for (int dz = -12; dz <= 12; dz++)
                for (int dx = -12; dx <= 12; dx++)
                {
                    IntVec3 cell = center + new IntVec3(dx, 0, dz);
                    if (!cell.InBounds(map)) continue;
                    int distance = Math.Max(Math.Abs(dx), Math.Abs(dz));
                    TerrainDef terrain = cell.GetTerrain(map);
                    bool open = cell.Walkable(map) && !cell.Fogged(map)
                        && terrain != null && !terrain.IsWater
                        && cell.GetEdifice(map) == null;
                    if (distance <= 5 && open) fit++;
                    if (distance <= 7 && open) circulation++;
                    if (distance >= 7 && distance <= 12 && open)
                        expansion++;
                    if (distance <= 10 && open
                        && map.fertilityGrid.FertilityAt(cell) >= 0.70f)
                        fertile++;
                    Building edifice = cell.GetEdifice(map);
                    if (distance >= 5 && distance <= 10
                        && edifice?.def?.building?.isNaturalRock == true)
                        naturalCover++;
                    if (distance <= 5 && !open) obstacles++;
                    if (distance <= 7 && open)
                    {
                        if (dx >= 0 && dz >= 0) northEast++;
                        else if (dx < 0 && dz >= 0) northWest++;
                        else if (dx >= 0) southEast++;
                        else southWest++;
                    }
                }
            CAHabitatFoodRoute route = holding == null
                ? environment?.FoodRoute
                    ?? CAHabitatFoodRoute.OutdoorCultivation
                : (CAHabitatFoodRoute)holding.habitatFoodRoute;
            int adjacency = route == CAHabitatFoodRoute.OutdoorCultivation
                ? fertile : route == CAHabitatFoodRoute.ForageAndHunt
                    ? Mathf.RoundToInt((environment?.FoodSupport ?? 0f)
                        * 100f) : 100;
            int cardinalOpen = 0;
            for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
            {
                IntVec3 direction = GenAdj.CardinalDirections[i];
                bool lane = true;
                for (int step = 1; step <= 4; step++)
                {
                    IntVec3 cell = center + direction * step;
                    if (!cell.InBounds(map) || !cell.Walkable(map)
                        || cell.GetEdifice(map) != null)
                    { lane = false; break; }
                }
                if (lane) cardinalOpen++;
            }
            return new CAAutonomousBuildingPatternEvidence
            {
                ValidGround = CanHoldCompactHabitat(map, center,
                    holding?.form ?? 0),
                UnmetRequirements = holding == null ? 0
                    : CountBits(holding.missingHabitatRequirementMask),
                TerrainFit = fit,
                FunctionalAdjacency = adjacency,
                Throughput = cardinalOpen,
                Circulation = circulation,
                Expansion = expansion,
                EnvironmentalBuffer = naturalCover,
                DefensiveSeparation = naturalCover,
                VisualOrder = CAAutonomousBuildingPatternKernel.Balance(
                    northEast, northWest, southEast, southWest),
                MaterialCost = obstacles,
                StableOrder = stableOrder
            };
        }

        private static int CountBits(int value)
        {
            int count = 0;
            uint bits = unchecked((uint)value);
            while (bits != 0)
            {
                count += (int)(bits & 1u);
                bits >>= 1;
            }
            return count;
        }

        private static bool TryMaterializeHabitat(Map map, IntVec3 site,
            Faction faction, CAFrontierHoldingPlan holding,
            CASettlementEnvironmentFacts environment,
            out List<Thing> spawned, out List<IntVec3> roofs,
            out string failure)
        {
            spawned = new List<Thing>();
            roofs = new List<IntVec3>();
            failure = null;
            int requirements = holding.habitatRequirementMask;
            if (Requires(requirements,
                    CAHabitatRequirement.BreathableInterior))
            {
                failure = "no autonomous frontier builder can yet prove a sealed breathable interior";
                return false;
            }
            if (!TryBuildCompactShell(map, site, faction, holding.form,
                    spawned, roofs))
            {
                RollBackHabitat(map, spawned, roofs);
                failure = "the selected ground could not hold an enclosed, roofed habitat";
                return false;
            }

            ThingDef wood = DefDatabase<ThingDef>.GetNamedSilentFail(
                "WoodLog");
            ThingDef cloth = DefDatabase<ThingDef>.GetNamedSilentFail(
                "Cloth");
            int residents = Mathf.Clamp(holding.residentCount, 1, 6);
            for (int i = 0; i < residents; i++)
                if (!TrySpawnNearbyTracked(map, site, "Bedroll", faction,
                        cloth, 0f, 3 + i * 11, 5f, spawned))
                {
                    RollBackHabitat(map, spawned, roofs);
                    failure = "the habitat could not provide one sheltered bed per resident";
                    return false;
                }

            if (!TrySpawnNearbyTracked(map, site, "Campfire", faction,
                    null, 35f, 2, 4f, spawned)
                || !TrySpawnNearbyTracked(map, site, "Table1x2c", faction,
                    wood, 0f, 19, 5f, spawned)
                || !TrySpawnNearbyTracked(map, site, "Stool", faction,
                    wood, 0f, 29, 5f, spawned))
            {
                RollBackHabitat(map, spawned, roofs);
                failure = "the habitat could not provide food preparation and ordinary living space";
                return false;
            }

            bool reserveRequired = Requires(requirements,
                CAHabitatRequirement.FoodReserve)
                || (CAHabitatFoodRoute)holding.habitatFoodRoute
                    == CAHabitatFoodRoute.StoredAndSupported;
            if (reserveRequired
                && !TrySpawnNearbyTracked(map, site, "Shelf", faction,
                    wood, 0f, 37, 5f, spawned))
            {
                RollBackHabitat(map, spawned, roofs);
                failure = "the habitat could not provide protected stores";
                return false;
            }

            // This is the realization of an already-established saved
            // holding. Its initial material form is authorized by canonical
            // faction knowledge; once residents exist, ordinary work queries
            // local distributed availability.
            int logistics = CATechnologicalKnowledgeRuntime.CanonicalRank(
                faction, CATechnologyDomains.Logistics,
                CATechnologyCompetencies.Maintain);
            string foodDef = logistics >= 3
                ? "MealSurvivalPack" : "Pemmican";
            int foodUnits = Math.Max(30, residents * (reserveRequired
                ? 35 : 20));
            if (!TrySpawnStock(map, site, foodDef, foodUnits, spawned))
            {
                RollBackHabitat(map, spawned, roofs);
                failure = "the habitat could not materialize its saved food reserve";
                return false;
            }

            CAHabitatFoodRoute route = (CAHabitatFoodRoute)
                holding.habitatFoodRoute;
            if (route == CAHabitatFoodRoute.OutdoorCultivation
                && !TrySowFoodPatch(map, site, Math.Max(12,
                    residents * 4), faction, spawned))
            {
                RollBackHabitat(map, spawned, roofs);
                failure = "the outdoor-cultivation route has no cultivable ground at the selected site";
                return false;
            }
            if ((route == CAHabitatFoodRoute.StoredAndSupported
                    || Requires(requirements,
                        CAHabitatRequirement.SecuredFoodSupply))
                && faction == null)
            {
                RollBackHabitat(map, spawned, roofs);
                failure = "the saved outside-supply route has no supporting faction";
                return false;
            }

            if (Requires(requirements, CAHabitatRequirement.MedicalCare)
                && (!TrySpawnStock(map, site, "MedicineHerbal",
                        Math.Max(8, residents * 3), spawned)
                    || !TrySpawnMedicalBed(map, site, faction, cloth,
                        spawned)))
            {
                RollBackHabitat(map, spawned, roofs);
                failure = "the habitat could not provide its required medical stock and treatment bed";
                return false;
            }

            if (Requires(requirements, CAHabitatRequirement.ThermalControl))
            {
                bool heatReady = environment.MinimumTemperature >= 0f
                    || spawned.Any(thing => thing?.def?.defName
                        == "Campfire");
                bool coolReady = environment.MaximumTemperature <= 35f
                    || TrySpawnNearbyTracked(map, site, "PassiveCooler",
                        faction, wood, 0f, 43, 5f, spawned);
                if (!heatReady || !coolReady)
                {
                    RollBackHabitat(map, spawned, roofs);
                    failure = "the habitat could not provide the required seasonal heating and cooling";
                    return false;
                }
            }

            if (Requires(requirements,
                    CAHabitatRequirement.ArtificialLight)
                && !TrySpawnNearbyTracked(map, site, "TorchLamp", faction,
                    wood, 30f, 47, 5f, spawned))
            {
                RollBackHabitat(map, spawned, roofs);
                failure = "the habitat could not provide light through permanent darkness";
                return false;
            }

            if (Requires(requirements, CAHabitatRequirement.WaterTreatment))
            {
                bool well = TrySpawnNearbyTracked(map, site, "CA_WellDug",
                    faction, wood, 0f, 53, 8f, spawned)
                    || TrySpawnNearbyTracked(map, site, "CA_WellBored",
                        faction, null, 0f, 53, 8f, spawned);
                bool raw = TrySpawnStock(map, site, "CA_WaterBrackish",
                    20, spawned)
                    || TrySpawnStock(map, site, "CA_WaterSaline", 20,
                        spawned);
                bool potable = TrySpawnStock(map, site,
                    "CA_WaterPotable", Math.Max(12, residents * 4),
                    spawned);
                if (!well || !raw || !potable)
                {
                    RollBackHabitat(map, spawned, roofs);
                    failure = "the habitat could not materialize a well, distillation input, and potable reserve";
                    return false;
                }
            }

            int roofMinimum = holding.form == 1 ? 49 : 25;
            int roofed = roofs.Count(cell => cell.Roofed(map));
            if (roofed < roofMinimum)
            {
                RollBackHabitat(map, spawned, roofs);
                failure = "the completed habitat does not retain enough enclosed roofed space";
                return false;
            }
            Log.Message("[CA] frontier autonomous habitat closed "
                + CAHabitatViabilityCausalKernel.FoodRouteWords(route)
                + " at " + site + ": " + residents + " beds, "
                + foodUnits + " food units, " + roofed
                + " roofed cells; requirements " + requirements);
            return true;
        }

        private static bool TrySpawnMedicalBed(Map map, IntVec3 site,
            Faction faction, ThingDef cloth, List<Thing> spawned)
        {
            int before = spawned.Count;
            if (!TrySpawnNearbyTracked(map, site, "Bedroll", faction,
                    cloth, 0f, 59, 5f, spawned)
                || spawned.Count <= before)
                return false;
            Building_Bed bed = spawned[spawned.Count - 1] as Building_Bed;
            if (bed == null) return false;
            bed.Medical = true;
            return true;
        }

        private static bool TryBuildCompactShell(Map map, IntVec3 site,
            Faction faction, int form, List<Thing> spawned,
            List<IntVec3> roofs)
        {
            ThingDef wall = DefDatabase<ThingDef>.GetNamedSilentFail("Wall");
            ThingDef door = DefDatabase<ThingDef>.GetNamedSilentFail("Door");
            ThingDef wood = DefDatabase<ThingDef>.GetNamedSilentFail(
                "WoodLog");
            if (wall == null || door == null || wood == null
                || RoofDefOf.RoofConstructed == null)
                return false;
            int radius = form == 1 ? 5 : 4;
            for (int dz = -radius; dz <= radius; dz++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) != radius
                        && Math.Abs(dz) != radius) continue;
                    bool entrance = dx == 0 && dz == -radius;
                    if (!TrySpawnDefTracked(map,
                            site + new IntVec3(dx, 0, dz),
                            entrance ? door : wall, faction, wood, 0f,
                            Rot4.North, spawned))
                        return false;
                }
            for (int dz = -radius + 1; dz <= radius - 1; dz++)
                for (int dx = -radius + 1; dx <= radius - 1; dx++)
                {
                    IntVec3 cell = site + new IntVec3(dx, 0, dz);
                    if (!cell.InBounds(map) || cell.Roofed(map)) continue;
                    map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
                    roofs.Add(cell);
                }
            return true;
        }

        private static bool Requires(int mask,
            CAHabitatRequirement requirement)
        {
            return (mask & (int)requirement) != 0;
        }

        private static bool TrySpawnNearbyTracked(Map map, IntVec3 center,
            string defName, Faction faction, ThingDef stuff, float fuel,
            int start, float radius, List<Thing> spawned)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null) return false;
            int cells = GenRadial.NumCellsInRadius(radius);
            for (int offset = 0; offset < cells; offset++)
            {
                int index = 1 + (start + offset) % Math.Max(1, cells - 1);
                if (TrySpawnDefTracked(map,
                        center + GenRadial.RadialPattern[index], def,
                        faction, stuff, fuel, Rot4.North, spawned))
                    return true;
            }
            return false;
        }

        private static bool TrySpawnDefTracked(Map map, IntVec3 c,
            ThingDef def, Faction faction, ThingDef stuff, float fuel,
            Rot4 rotation, List<Thing> spawned)
        {
            try
            {
                if (def == null || !c.InBounds(map)) return false;
                if (!CATechnologicalKnowledgeRuntime.CanConstructCanonical(
                        faction, def, out _)) return false;
                CellRect occupied = GenAdj.OccupiedRect(c, rotation,
                    def.Size);
                foreach (IntVec3 cell in occupied)
                {
                    if (!cell.InBounds(map) || !cell.Standable(map)
                        || cell.GetEdifice(map) != null) return false;
                    List<Thing> things = cell.GetThingList(map);
                    for (int i = 0; i < things.Count; i++)
                        if (things[i] is Pawn
                            || things[i].def.category
                                == ThingCategory.Item)
                            return false;
                }
                foreach (IntVec3 cell in occupied)
                    ClearNaturalCover(cell, map);
                Thing thing = def.MadeFromStuff
                    ? ThingMaker.MakeThing(def, stuff
                        ?? GenStuff.DefaultStuffFor(def))
                    : ThingMaker.MakeThing(def);
                thing.Rotation = rotation;
                if (faction != null && def.CanHaveFaction)
                    thing.SetFaction(faction);
                GenSpawn.Spawn(thing, c, map, rotation);
                if (fuel > 0f)
                {
                    CompRefuelable refuelable = thing
                        .TryGetComp<CompRefuelable>();
                    if (refuelable != null) refuelable.Refuel(fuel);
                }
                spawned.Add(thing);
                return true;
            }
            catch { return false; }
        }

        private static bool TrySpawnStock(Map map, IntVec3 center,
            string defName, int count, List<Thing> spawned)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null || count <= 0) return false;
            int remaining = count;
            int cells = GenRadial.NumCellsInRadius(6f);
            for (int i = 1; i < cells && remaining > 0; i++)
            {
                IntVec3 cell = center + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map) || !cell.Standable(map)
                    || cell.GetEdifice(map) != null
                    || cell.GetThingList(map).Any(thing => thing is Pawn))
                    continue;
                try
                {
                    Thing stock = ThingMaker.MakeThing(def);
                    stock.stackCount = Math.Min(remaining,
                        Math.Max(1, def.stackLimit));
                    GenSpawn.Spawn(stock, cell, map);
                    spawned.Add(stock);
                    remaining -= stock.stackCount;
                }
                catch { }
            }
            return remaining == 0;
        }

        private static bool TrySowFoodPatch(Map map, IntVec3 center,
            int wanted, Faction faction, List<Thing> spawned)
        {
            ThingDef crop = DefDatabase<ThingDef>.GetNamedSilentFail(
                "Plant_Potato");
            if (crop == null
                || !CATechnologicalKnowledgeRuntime.CanGrowCanonical(
                    faction, crop, out _)) return false;
            int planted = 0;
            int cells = GenRadial.NumCellsInRadius(13f);
            for (int i = 1; i < cells && planted < wanted; i++)
            {
                IntVec3 cell = center + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map) || cell.Roofed(map)
                    || cell.GetEdifice(map) != null
                    || !crop.CanEverPlantAt(cell, map)) continue;
                try
                {
                    ClearNaturalCover(cell, map);
                    Plant plant = ThingMaker.MakeThing(crop) as Plant;
                    if (plant == null) continue;
                    plant.Growth = 0.55f;
                    plant.sown = true;
                    GenSpawn.Spawn(plant, cell, map);
                    spawned.Add(plant);
                    planted++;
                }
                catch { }
            }
            return planted >= wanted;
        }

        private static void ClearNaturalCover(IntVec3 cell, Map map)
        {
            List<Thing> things = cell.GetThingList(map).ToList();
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing == null || thing.Destroyed) continue;
                if (thing.def.category == ThingCategory.Plant
                    || thing.def.category == ThingCategory.Filth)
                    thing.Destroy(DestroyMode.Vanish);
            }
        }

        private static void RollBackHabitat(Map map, List<Thing> spawned,
            List<IntVec3> roofs)
        {
            for (int i = (spawned?.Count ?? 0) - 1; i >= 0; i--)
            {
                Thing thing = spawned[i];
                if (thing != null && !thing.Destroyed)
                    thing.Destroy(DestroyMode.Vanish);
            }
            for (int i = 0; i < (roofs?.Count ?? 0); i++)
                if (roofs[i].InBounds(map)
                    && map.roofGrid.RoofAt(roofs[i])
                        == RoofDefOf.RoofConstructed)
                    map.roofGrid.SetRoof(roofs[i], null);
        }
    }
}
