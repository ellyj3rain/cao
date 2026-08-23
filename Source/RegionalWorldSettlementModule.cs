using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // PERSISTENT WORLD-SETTLEMENT STATE. One record per world settlement in
    // a regional world: who founded it, the coarse population and urban
    // class its ground supports, and when it was founded. The records are
    // the world-facing consumers of settlement concentration, source
    // variety, and urban growth: placement consumes concentration and
    // variety at world generation; the urban class consumes the propensity
    // threshold against derived support facts; the distant-world founding
    // cadence keeps extending the same state while the campaign runs.
    public sealed class CARegionalWorldSettlementState : IExposable
    {
        public int tileId = -1;
        public int factionLoadId = -1;
        public int population;
        public byte urbanClass;
        public int foundedAbsTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tileId, "tileId", -1);
            Scribe_Values.Look(ref factionLoadId, "factionLoadId", -1);
            Scribe_Values.Look(ref population, "population", 0);
            Scribe_Values.Look(ref urbanClass, "urbanClass", (byte)0);
            Scribe_Values.Look(ref foundedAbsTick, "foundedAbsTick", 0);
        }

        internal string UrbanWord
        {
            get
            {
                switch (urbanClass)
                {
                    case 5: return "city";
                    case 4: return "town";
                    case 3: return "large village";
                    case 2: return "village";
                    case 1: return "hamlet";
                    default: return "outpost";
                }
            }
        }
    }

    internal static class CARegionalWorldSettlements
    {
        // Coarse land capacity for a world tile, mirroring the 0..3 scale
        // regional composition uses.
        internal static int LandCapacityOf(PlanetTile tile)
        {
            Tile info = tile.Valid ? tile.Tile : null;
            if (info?.PrimaryBiome == null) return 0;
            int value = 3;
            switch (info.hilliness)
            {
                case Hilliness.LargeHills: value = 2; break;
                case Hilliness.Mountainous: value = 1; break;
                case Hilliness.Impassable: value = 0; break;
            }
            if (info.PrimaryBiome.settlementSelectionWeight < 0.5f)
                value = Math.Max(0, value - 1);
            return value;
        }

        internal static int AccessOf(PlanetTile tile)
        {
            SurfaceTile surface = tile.Valid
                ? tile.Tile as SurfaceTile : null;
            if (surface == null) return 0;
            int access = 0;
            if (surface.Roads.NullOrEmpty() == false) access++;
            if (surface.Rivers.NullOrEmpty() == false) access++;
            if (CARegionalPlanUtility.ConstituentIsCoastal(tile.tileId))
                access++;
            return Math.Min(3, access);
        }

        internal static int TechTierOf(Faction faction)
        {
            TechLevel level = faction?.def?.techLevel ?? TechLevel.Neolithic;
            if (level >= TechLevel.Spacer) return 3;
            if (level >= TechLevel.Industrial) return 2;
            if (level >= TechLevel.Medieval) return 1;
            return 0;
        }

        internal static CARegionalWorldSettlementState BuildState(int seed,
            Settlement settlement, int foundedTick)
        {
            PlanetTile tile = settlement.Tile;
            int land = LandCapacityOf(tile);
            int access = AccessOf(tile);
            var world = CARegionalWorldComponent.Current;
            float propensity = world?.WorldPolicy?.urbanGrowthPropensity
                ?? 0.45f;
            // A settlement sharing a joined region with others is that
            // land's candidate center; lone outposts are not.
            CARegionalTopologyRecord record =
                world?.TopologyRecordAt(tile);
            bool regionalCenter = record != null && record.Multi
                && CARegionalGeography.TopologySettlementCount(record) > 1;
            // Prior development is a real cause: the authored world
            // baseline modulated by each settlement's position in the
            // settlement network. A regional center — a settlement
            // sharing joined land with others — is one step more
            // developed than the world baseline; an isolated outpost
            // is one step less. This replaces the hidden per-tile hash.
            int worldDev = (int)Math.Round(
                (world?.WorldPolicy?.worldDevelopment ?? 0.6f) * 3f);
            int development = worldDev + (regionalCenter ? 1 : -1);
            development = Math.Max(0, Math.Min(3, development));
            float variability = world?.WorldPolicy?.worldVariability ?? 0f;
            int population = CAWorldTendencyCausalKernel
                .WorldSettlementPopulation(seed, tile.tileId, land,
                    TechTierOf(settlement.Faction), development,
                    variability);
            int support = CAWorldTendencyCausalKernel.WorldSettlementSupport(
                population, land, access, regionalCenter, development);
            int scale = CAWorldTendencyCausalKernel.SettlementScale(
                population, support, propensity);
            return new CARegionalWorldSettlementState
            {
                tileId = tile.tileId,
                factionLoadId = settlement.Faction?.loadID ?? -1,
                population = population,
                urbanClass = (byte)scale,
                foundedAbsTick = foundedTick
            };
        }

        // Deterministic-under-seed placement: candidates drawn from the
        // step's seeded Rand stream, scored by habitat preference bent by
        // the concentration tendency against the actual distance to the
        // nearest standing settlement.
        // The scoring band is expressed in a reference spacing rather
        // than raw tiles. The kernel discriminates over 0..15; real
        // worlds place settlements tens of tiles apart, so passing raw
        // distance saturated every candidate at the cap and the
        // tendency stopped separating spread from clustered. Scaling
        // the measured distance into the band by the world's own mean
        // spacing restores that separation at any planet size.
        internal static float ReferenceSpacing(PlanetLayer layer,
            int settlementTarget)
        {
            int tiles = Math.Max(1, layer?.TilesCount ?? 1);
            int places = Math.Max(1, settlementTarget);
            return UnityEngine.Mathf.Clamp(
                UnityEngine.Mathf.Sqrt((float)tiles / places), 4f, 400f);
        }

        internal static PlanetTile PlaceSettlementTile(PlanetLayer layer,
            Faction faction, float concentration,
            List<PlanetTile> standing, float referenceSpacing = 15f)
        {
            int seed = Verse.Find.World?.info?.Seed ?? 0;
            float toBand = 15f
                / UnityEngine.Mathf.Max(1f, referenceSpacing);
            for (int round = 0; round < 8; round++)
            {
                PlanetTile best = PlanetTile.Invalid;
                double bestScore = 0d;
                for (int i = 0; i < 80; i++)
                {
                    int id = Rand.Range(0, layer.TilesCount);
                    Tile info = layer[id];
                    if (info?.PrimaryBiome == null
                        || !info.PrimaryBiome.canBuildBase
                        || !info.PrimaryBiome.implemented
                        || info.hilliness == Hilliness.Impassable)
                        continue;
                    float habitat =
                        info.PrimaryBiome.settlementSelectionWeight;
                    if (faction?.def?.minSettlementTemperatureChanceCurve
                        != null)
                        habitat *= faction.def
                            .minSettlementTemperatureChanceCurve.Evaluate(
                                GenTemperature.MinTemperatureAtTile(
                                    info.tile));
                    if (habitat <= 0f) continue;
                    float nearest = -1f;
                    for (int s = 0; s < standing.Count; s++)
                    {
                        float distance = Verse.Find.WorldGrid
                            .ApproxDistanceInTiles(standing[s], info.tile);
                        if (nearest < 0f || distance < nearest)
                            nearest = distance;
                    }
                    double score = CAWorldTendencyCausalKernel
                        .WorldPlacementScore(seed, id, habitat,
                            nearest < 0f ? -1f : nearest * toBand,
                            concentration);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = info.tile;
                    }
                }
                if (best.Valid
                    && TileFinder.IsValidTileForNewSettlement(best))
                    return best;
            }
            // The scored search found no valid ground; vanilla's uniform
            // fallback still answers.
            return TileFinder.RandomSettlementTileFor(layer, faction);
        }

        // Source-variety-aware faction choice: prefer factions not yet
        // represented among placed settlements until the variety target is
        // met, weighted as vanilla weights them throughout.
        internal static Faction ChooseFaction(List<Faction> eligible,
            HashSet<Faction> represented, int targetDistinct)
        {
            List<Faction> preferred = represented.Count < targetDistinct
                ? eligible.Where(faction =>
                    !represented.Contains(faction)).ToList()
                : eligible;
            if (preferred.Count == 0) preferred = eligible;
            return preferred.RandomElementByWeight(faction =>
                faction.def.settlementGenerationWeight);
        }
    }

    // THE WORLD'S SETTLEMENTS CONSUME THE WORLD'S TENDENCIES. In a regional
    // world (the partition exists), settlement scatter is CA-governed:
    // vanilla's faction initialization, counts, naming, and validity all
    // stand, while the faction choice consumes source variety and the tile
    // choice consumes settlement concentration. Everything else falls
    // through to vanilla untouched.
    [HarmonyPatch(typeof(FactionGenerator),
        nameof(FactionGenerator.GenerateFactionsIntoWorldLayer))]
    internal static class CARegionalWorldSettlementPlacementPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(PlanetLayer layer,
            List<FactionDef> factions)
        {
            CARegionalWorldComponent world =
                CARegionalWorldComponent.Current;
            if (world == null || world.Topology.Count == 0
                || layer != Verse.Find.WorldGrid.Surface)
                return true;
            try
            {
                AccessTools.Method(typeof(FactionGenerator),
                        "InitializeFactions")
                    .Invoke(null, new object[] { layer, factions });
            }
            catch (Exception ex)
            {
                Log.Warning("[CA][WorldSettlements] faction initialization "
                    + "reflection failed (" + ex.GetType().Name
                    + "); vanilla placement runs instead");
                return true;
            }

            List<Faction> eligible = Verse.Find.World.factionManager
                .AllFactionsListForReading.Where(faction =>
                    !faction.def.isPlayer && !faction.Hidden
                    && !faction.temporary
                    && CanExistOnLayer(layer, faction.def)).ToList();
            // This prefix suppresses vanilla placement entirely, so a
            // throw anywhere below would hand the player a world with
            // no settlements at all. Any failure falls back to vanilla
            // for the settlements not yet placed.
            int placed = 0;
            int intended = 0;
            try
            {
            if (eligible.Count > 0)
            {
                CARegionalWorldPolicy policy = world.WorldPolicy;
                float viewFactor = layer.Def.viewAngleSettlementsFactorCurve
                    .Evaluate(UnityEngine.Mathf.Clamp01(
                        layer.ViewAngle / 180f));
                float per100k = layer.Def.settlementsPer100kTiles
                    .RandomInRange;
                float populationFactor = Verse.Find.World.info
                    .overallPopulation.GetScaleFactor();
                int target = GenMath.RoundRandom((float)layer.TilesCount
                    / 100000f * per100k * populationFactor * viewFactor);
                target -= Verse.Find.WorldObjects
                    .AllSettlementsOnLayer(layer).Count;
                int distinctAvailable = eligible.Count;
                int targetDistinct = CAWorldTendencyCausalKernel
                    .SourceVarietyTargetDistinct(Math.Max(0, target),
                        distinctAvailable,
                        policy.reallocationSourceVariety);
                var represented = new HashSet<Faction>();
                var standing = Verse.Find.WorldObjects
                    .AllSettlementsOnLayer(layer)
                    .Select(item => item.Tile).ToList();
                intended = Math.Max(0, target);
                float spacing = CARegionalWorldSettlements
                    .ReferenceSpacing(layer, intended);
                for (int i = 0; i < target; i++)
                {
                    Faction faction = CARegionalWorldSettlements
                        .ChooseFaction(eligible, represented,
                            targetDistinct);
                    WorldObject worldObject = WorldObjectMaker
                        .MakeWorldObject(layer.Def.SettlementWorldObjectDef);
                    worldObject.SetFaction(faction);
                    worldObject.Tile = CARegionalWorldSettlements
                        .PlaceSettlementTile(layer, faction,
                            policy.settlementConcentration, standing,
                            spacing);
                    if (worldObject is INameableWorldObject nameable)
                        nameable.Name = SettlementNameGenerator
                            .GenerateSettlementName(worldObject);
                    Verse.Find.WorldObjects.Add(worldObject);
                    represented.Add(faction);
                    standing.Add(worldObject.Tile);
                    placed++;
                }
                Log.Message("[CA][WorldSettlements] placed " + placed
                    + " world settlements; "
                    + represented.Count + " distinct sources against a "
                    + "variety target of " + targetDistinct
                    + "; concentration "
                    + policy.settlementConcentration.ToString("F2")
                    + "; reference spacing "
                    + spacing.ToString("F1") + " tiles");
            }
            }
            catch (Exception ex)
            {
                Log.Error("[CA][WorldSettlements] regional placement "
                    + "failed after " + placed + " of " + intended
                    + " settlements (" + ex + "); vanilla placement "
                    + "completes the remainder so the world is never "
                    + "left unsettled.");
                try
                {
                    for (int i = placed; i < intended; i++)
                    {
                        Faction faction = Verse.Find.World.factionManager
                            .AllFactionsListForReading
                            .Where(item => !item.def.isPlayer
                                && !item.Hidden && !item.temporary
                                && item.def.settlementGenerationWeight > 0f)
                            .RandomElementByWeightWithFallback(item =>
                                item.def.settlementGenerationWeight);
                        if (faction == null) break;
                        WorldObject fallback = WorldObjectMaker
                            .MakeWorldObject(
                                layer.Def.SettlementWorldObjectDef);
                        fallback.SetFaction(faction);
                        fallback.Tile = TileFinder
                            .RandomSettlementTileFor(layer, faction);
                        if (fallback is INameableWorldObject named)
                            named.Name = SettlementNameGenerator
                                .GenerateSettlementName(fallback);
                        Verse.Find.WorldObjects.Add(fallback);
                    }
                }
                catch (Exception inner)
                {
                    Log.Error("[CA][WorldSettlements] vanilla fallback "
                        + "placement also failed: " + inner);
                }
            }
            Verse.Find.IdeoManager.SortIdeos();
            return false;
        }

        private static bool CanExistOnLayer(PlanetLayer layer, FactionDef def)
        {
            try
            {
                return (bool)AccessTools.Method(typeof(FactionGenerator),
                        "CanExistOnLayer")
                    .Invoke(null, new object[] { layer, def });
            }
            catch (Exception) { return true; }
        }
    }

    // The world settlement's persistent standing, where the player already
    // reads settlements: its urban class from the ground's actual support
    // facts against the authored propensity, and its founding date when it
    // was founded after the world began.
    [HarmonyPatch(typeof(Settlement),
        nameof(Settlement.GetInspectString))]
    internal static class CARegionalWorldSettlementInspectPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Settlement __instance,
            ref string __result)
        {
            CARegionalWorldComponent component =
                CARegionalWorldComponent.Current;
            CARegionalWorldSettlementState state =
                component?.WorldSettlementStateAt(
                    __instance.Tile.tileId);
            if (state != null)
            {
                string line = "Standing: " + state.UrbanWord + " (~"
                    + state.population + " people)";
                if (state.foundedAbsTick > 0)
                    line += ", founded "
                        + GenDate.DateReadoutStringAt(
                            GenDate.TickGameToAbs(state.foundedAbsTick),
                            Verse.Find.WorldGrid.LongLatOf(
                                __instance.Tile));
                __result = __result.NullOrEmpty() ? line
                    : __result + "\n" + line;
            }

            // The settlement's regional membership, with the region's
            // remembered political state - so a place inside joined
            // land reads as part of that land, not as a lone icon.
            CARegionalTopologyRecord record =
                component?.TopologyRecordAt(__instance.Tile.tileId);
            if (record?.memberTileIds == null
                || record.memberTileIds.Count < 2) return;
            CARegionalPoliticalRecord political =
                component.PoliticalRecordFor(record.regionId);
            string regionLine = "Region: "
                + CARegionalGeography.TopologyName(record) + " ("
                + record.memberTileIds.Count + " areas) â€” "
                + (political?.StatusLine
                    ?? CARegionalGeography.PoliticalSummary(record));
            CARegionalPoliticalEvent latest =
                political?.events?.LastOrDefault();
            if (latest != null)
                regionLine += "\n  "
                    + CARegionalPoliticalLedger.EventLine(latest);
            __result = __result.NullOrEmpty() ? regionLine
                : __result + "\n" + regionLine;
        }
    }

    // World settlement state is derived after roads exist (Roads 600), so
    // access facts are real.
    public sealed class WorldGenStep_CARegionalWorldSettlementState
        : WorldGenStep
    {
        public override int SeedPart
        {
            get { return 902114177; }
        }

        public override void GenerateFresh(string seed, PlanetLayer layer)
        {
            if (layer != Verse.Find.WorldGrid.Surface) return;
            CARegionalWorldComponent world =
                CARegionalWorldComponent.Current;
            if (world == null || world.Topology.Count == 0) return;
            // Same reasoning as the partition step: a throw here would
            // take down the whole world generation, and a world without
            // CAO's settlement facts is still a world.
            try
            {
                world.RebuildWorldSettlementStates("world generation");
            }
            catch (Exception ex)
            {
                Log.Error("[CA][WorldSettlements] settlement facts "
                    + "could not be derived during world generation; "
                    + "this world has none: " + ex);
                return;
            }
            ReportWorldCharacter(world);
        }

        // THE WORLD THAT WAS ACTUALLY MADE, IN ONE LINE. Generation
        // already logs its steps separately, but answering "is this
        // world different from that one" meant reading several
        // receipts across a long log and doing arithmetic. This states
        // the realized outcome of every authored tendency together, so
        // two worlds can be compared directly. It reports what was
        // built; it is not evidence that the world plays differently.
        private static void ReportWorldCharacter(
            CARegionalWorldComponent world)
        {
            try
            {
                IReadOnlyList<CARegionalTopologyRecord> topology =
                    world.Topology;
                long land = 0;
                long joinedLand = 0;
                int joined = 0;
                int widest = 0;
                for (int i = 0; i < topology.Count; i++)
                {
                    CARegionalTopologyRecord record = topology[i];
                    if (record?.memberTileIds == null) continue;
                    int size = record.memberTileIds.Count;
                    land += size;
                    if (size < 2) continue;
                    joined++;
                    joinedLand += size;
                    if (size > widest) widest = size;
                }
                IReadOnlyList<CARegionalWorldSettlementState> states =
                    world.WorldSettlementStates;
                int towns = 0;
                var owners = new HashSet<int>();
                for (int i = 0; i < (states?.Count ?? 0); i++)
                {
                    if (states[i] == null) continue;
                    if (states[i].urbanClass >= 4) towns++;
                    owners.Add(states[i].factionLoadId);
                }
                CARegionalWorldPolicy policy = world.WorldPolicy;
                Log.Message("[CA][WorldCharacter] realized: "
                    + (land == 0 ? 0f : (float)joinedLand / land)
                        .ToString("F2")
                    + " of land in " + joined + " joined regions (up to "
                    + widest + " areas each, band "
                    + policy.stitchedRegionSizeMin + ".."
                    + policy.stitchedRegionSizeMax + "); "
                    + (states?.Count ?? 0) + " settlements held by "
                    + owners.Count + " peoples, " + towns
                    + " of them town-or-larger; authored gathering "
                    + policy.settlementConcentration.ToString("F2")
                    + ", urban " + policy.urbanGrowthPropensity
                        .ToString("F2")
                    + ", frontier " + policy.frontierHoldingFrequency
                        .ToString("F2")
                    + ", origins " + policy.reallocationSourceVariety
                        .ToString("F2")
                    + ", founding " + policy.distantFoundingRate
                        .ToString("F2")
                    + ", development " + policy.worldDevelopment
                        .ToString("F2")
                    + ", variability " + policy.worldVariability
                        .ToString("F2")
                    + ", stability " + policy.worldStability
                        .ToString("F2"));
            }
            catch (Exception ex)
            {
                Log.Warning("[CA][WorldCharacter] could not summarize "
                    + "the generated world: " + ex.Message);
            }
        }

        public override void GenerateWithoutWorldData(string seed,
            PlanetLayer layer)
        {
        }
    }
}
