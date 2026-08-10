using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // The execution contract a mutator has under regional projection.
    //
    // DEFAULT IS Unsupported, deliberately. Before this registry existed the
    // default was to run the ROOT tile's copy across the whole region and
    // silently discard every member's - which is how one mountainous root came
    // to fill 19-24% of a region with rock (F-126) and how cave dispatch both
    // lost and spread (F-129). An unclassified mutator must be visibly refused,
    // never invisibly misapplied.
    // HOW a mutator would execute per constituent. Mechanism is a description
    // of approach; it says nothing about whether that approach works yet.
    internal enum CAExecutionMechanism
    {
        Unsupported = 0,
        ProjectedScalar,
        FrameAwareAnalytic,
        MaskedPlacement,
        ConnectedRegional,
        IsolatedStaging,
        HistoricalSiteMaterialization,

        // Supplies behaviour through def.Worker at RUNTIME with no generation
        // hook at all - MixedBiome.SecondaryBiome, AnimalCommonalityFactorFor,
        // PlantCommonalityFactorFor, AdditionalWildPlants,
        // MutateWeatherCommonalityFor, OnAddedToTile. Absence of a generation
        // hook is NOT evidence of scalar semantics: these have per-tile runtime
        // effects, and CA's generation-local private worker instances do not
        // update the singleton those consumers read.
        RuntimeWorkerBehaviour,
    }

    // WHETHER it works, tracked independently of mechanism. CA routing a
    // mutator is not the same as CA supporting it: Mountain and Caves were both
    // routed through CA and both were wrong when their bodies were read.
    internal enum CAReadiness
    {
        Unsupported = 0,
        ImplementedUnverified,
        ProductionSupported,
    }

    // ONE obligation a def places on the engine, and whether CA satisfies it
    // per constituent. Readiness is a property of the OBLIGATION, never of the
    // def: a def can have several, satisfied to different degrees.
    internal sealed class CAContractFacet
    {
        internal readonly string Obligation;
        internal readonly CAExecutionMechanism Mechanism;
        internal readonly CAReadiness Readiness;
        internal readonly string Reason;

        internal CAContractFacet(string obligation,
            CAExecutionMechanism mechanism, CAReadiness readiness,
            string reason)
        {
            Obligation = obligation;
            Mechanism = mechanism;
            Readiness = readiness;
            Reason = reason;
        }
    }

    internal sealed class CACompatibilityEntry
    {
        internal int SourceTileId;
        internal string DefName;
        internal readonly List<CAContractFacet> Facets =
            new List<CAContractFacet>();

        // A def is only as ready as its WEAKEST obligation. AnimalHabitat is
        // the case that forced this: its per-cell spawning factor and
        // incident-time lookup are separate contracts, so readiness for one
        // must never carry the other along behind a promotion.
        internal CAReadiness Readiness => Facets.Count == 0
            ? CAReadiness.Unsupported
            : (CAReadiness)Facets.Min(facet => (int)facet.Readiness);

        // The facet that DECIDES readiness - what has to be closed to raise
        // it, rather than a summary of the def as a whole.
        internal CAContractFacet Governing => Facets
            .OrderBy(facet => (int)facet.Readiness).FirstOrDefault();

        internal CAExecutionMechanism Mechanism =>
            Governing?.Mechanism ?? CAExecutionMechanism.Unsupported;

        internal IEnumerable<CAContractFacet> Unresolved => Facets
            .Where(facet => facet.Readiness != CAReadiness.ProductionSupported)
            .OrderBy(facet => (int)facet.Readiness);

        // Every facet, weakest first, so a partially-implemented def reports
        // what works alongside what does not instead of collapsing to either.
        internal string Reason => Facets.Count == 0
            ? "no obligation classified"
            : string.Join("; ", Facets
                .OrderBy(facet => (int)facet.Readiness)
                .Select(facet => facet.Obligation + " [" + facet.Readiness
                    + "] " + facet.Reason));
    }

    internal sealed class CACompatibilityReport
    {
        internal readonly List<CACompatibilityEntry> Entries =
            new List<CACompatibilityEntry>();

        // The durable confirmation gate passes ONLY production-supported
        // content. Implemented-but-unverified is a development state, never a
        // reason to let a durable world be created.
        internal IEnumerable<CACompatibilityEntry> Blocking =>
            Entries.Where(e => e.Readiness != CAReadiness.ProductionSupported);

        internal IEnumerable<CACompatibilityEntry> Unsupported => Blocking;

        internal bool IsValid => !Blocking.Any();

        // Reported at BOTH levels on purpose. Counting only defs hides that
        // one def can be mostly implemented and still blocked; counting only
        // obligations hides how many defs are actually affected.
        internal string Summary()
        {
            var facets = Entries.SelectMany(e => e.Facets).ToList();
            var byMechanism = facets.GroupBy(f => f.Mechanism)
                .OrderBy(g => g.Key.ToString())
                .Select(g => g.Key + "=" + g.Count());
            var byDefReadiness = Entries.GroupBy(e => e.Readiness)
                .OrderBy(g => g.Key.ToString())
                .Select(g => g.Key + "=" + g.Count());
            var byFacetReadiness = facets.GroupBy(f => f.Readiness)
                .OrderBy(g => g.Key.ToString())
                .Select(g => g.Key + "=" + g.Count());
            return Entries.Count + " attachments across "
                + Entries.Select(e => e.SourceTileId).Distinct().Count()
                + " source tiles; " + facets.Count + " obligations; mechanism ["
                + string.Join(", ", byMechanism) + "]; def readiness ["
                + string.Join(", ", byDefReadiness) + "]; obligation readiness ["
                + string.Join(", ", byFacetReadiness) + "]";
        }

        // Actionable rather than diagnostic: names the def, how many tiles
        // carry it, and WHICH of its obligations is unresolved, so the failure
        // can be acted on at configuration time instead of discovered after a
        // world exists. Naming the facet matters - "AnimalHabitat is not
        // supported" is not actionable, "AnimalHabitat's incident-time
        // aggressor lookup is not supported" is.
        internal string ActionableFailure()
        {
            var grouped = Blocking
                .GroupBy(e => e.DefName)
                .OrderByDescending(g => g.Count())
                .Select(g =>
                {
                    CACompatibilityEntry sample = g.First();
                    string blockers = string.Join(", ", sample.Unresolved
                        .Select(f => f.Obligation + " (" + f.Readiness + ": "
                            + f.Reason + ")"));
                    return g.Key + " on " + g.Count() + " tile"
                        + (g.Count() == 1 ? "" : "s") + " - overall "
                        + sample.Readiness + "; unresolved: " + blockers;
                });
            return "This region contains content CA cannot yet generate "
                + "correctly: " + string.Join("; ", grouped)
                + ". Choose a different region, or remove the landmarks "
                + "carrying it, before continuing.";
        }
    }

    internal static class CARegionalContentCompatibility
    {
        // DEVELOPER ONLY. Lets a disposable regional generation exercise
        // contracts that are ImplementedUnverified, which is the only way to
        // obtain the runtime evidence that would promote them to
        // ProductionSupported. It never admits Unsupported content, and a plan
        // confirmed under it is stamped developerExercise so the durable start
        // can refuse it. The durable confirmation gate itself is unchanged and
        // still passes ProductionSupported alone.
        // Initialised from the environment so a boot-time test does not depend
        // on reaching a menu, and toggleable in dev mode so it can be turned
        // off without relaunching. One capability, one entry: the debug action
        // reports the state it lands in rather than adding a second switch.
        internal static bool DeveloperExerciseUnverified =
            string.Equals(Environment.GetEnvironmentVariable(
                "CA_REGIONAL_DEV_EXERCISE"), "1", StringComparison.Ordinal);

        [DebugAction("Colonist Awareness",
            "Toggle regional developer exercise",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Entry
                | AllowedGameStates.Playing)]
        private static void ToggleDeveloperExercise()
        {
            DeveloperExerciseUnverified = !DeveloperExerciseUnverified;
            Log.Warning("[CA][Regional] developer exercise "
                + (DeveloperExerciseUnverified ? "ENABLED" : "disabled")
                + ". While enabled, confirmation admits ImplementedUnverified "
                + "content and stamps the plan developerExercise, which a "
                + "durable start refuses. It never admits Unsupported "
                + "content.");
        }

        internal static bool AllowsDeveloperExercise(
            CACompatibilityReport report)
        {
            return DeveloperExerciseUnverified
                && report != null
                && !report.Entries.Any(entry =>
                    entry.Readiness == CAReadiness.Unsupported);
        }

        // EVERY obligation a def places on the engine, each classified on its
        // own. The previous shape returned one verdict per def and stopped at
        // the first match, which produced two distinct partial-status defects:
        // a def with several runtime hooks was described by whichever one was
        // tested first, and a worker-less def carrying several fields was
        // described by whichever field was checked first. Both let an
        // unresolved obligation ride along under a resolved one's status.
        //
        // Readiness stays TRUTHFUL, not aspirational: nothing has been run in
        // a regional generation, so nothing is production-supported except
        // obligations regional projection provably cannot affect.
        internal static List<CAContractFacet> FacetsFor(TileMutatorDef def)
        {
            var facets = new List<CAContractFacet>();
            if (def == null)
            {
                facets.Add(new CAContractFacet("def",
                    CAExecutionMechanism.Unsupported, CAReadiness.Unsupported,
                    "null def"));
                return facets;
            }
            TileMutatorWorker worker = def.Worker;
            AddDefFieldFacets(def, worker, facets);
            AddGenerationFacet(worker, facets);
            AddLifecycleFacets(worker, facets);
            AddRuntimeFacets(worker, facets);
            if (facets.Count == 0)
                facets.Add(new CAContractFacet("none classified",
                    CAExecutionMechanism.Unsupported, CAReadiness.Unsupported,
                    "no traced obligation; refused by default rather than "
                    + "assumed inert"));
            return facets;
        }

        // Def fields are read by native code whether or not the def has a
        // worker, so these are NOT gated on worker == null, and every group
        // present gets its own facet. The seven fields have five different
        // execution semantics traced to their native consumers; averaging any
        // of the local ones into one regional value would let one
        // constituent's ecology, junk, geysers or ponds reach unrelated ones.
        private static void AddDefFieldFacets(TileMutatorDef def,
            TileMutatorWorker worker, List<CAContractFacet> facets)
        {
            if (def.overrideCoastalBeachTerrain != null
                || def.overrideLakeBeachTerrain != null
                || def.overrideMudTerrain != null
                || def.overrideRiverbankTerrain != null)
            {
                // MapGenUtility.BeachTerrainAt / LakeshoreTerrainAt /
                // MudTerrainAt / RiverbankTerrainAt each take the cell and fall
                // back to map.BiomeAt(cell); the per-cell redirect makes them
                // read the owning constituent's overrides.
                bool redirectHealthy = CARegionalTerrainFromOwnerPatch
                    .RedirectHealthy;
                facets.Add(new CAContractFacet("terrain overrides",
                    CAExecutionMechanism.ProjectedScalar,
                    redirectHealthy ? CAReadiness.ImplementedUnverified
                        : CAReadiness.Unsupported,
                    redirectHealthy
                        ? "per-cell terrain override redirect installed"
                        : "per-cell terrain override redirect is unavailable: "
                            + CARegionalTerrainFromOwnerPatch
                                .RedirectHealthSummary));
            }

            if (def.terrainPatchMakers != null
                && def.terrainPatchMakers.Count > 0)
            {
                // TileMutatorDef.terrainPatchMakers has exactly one reader,
                // TileMutatorWorker_Patches, and it reads its OWN def's list.
                bool reachable = worker is TileMutatorWorker_Patches;
                facets.Add(new CAContractFacet("terrainPatchMakers",
                    CAExecutionMechanism.FrameAwareAnalytic,
                    CAReadiness.Unsupported,
                    reachable
                        ? "consumed per cell by MapGenUtility.TerrainFrom with "
                            + "its own noise geometry; not projected per "
                            + "constituent"
                        : "no reachable consumer on a def whose worker is not "
                            + "TileMutatorWorker_Patches; recorded, not "
                            + "projected"));
            }

            if (Math.Abs(def.junkDensityFactor - 1f) > 0.0001f
                || Math.Abs(def.geyserCountFactor - 1f) > 0.0001f)
                // GenStep_Scatterer.GetPlacementFactor and
                // GenStep_ScatterGeysers.CalculateFinalCount multiply these
                // into a MAP-WIDE count from the root tile's mutators alone.
                // One Junkyard constituent carries x15. An area-weighted mean
                // is NOT acceptable: it still raises junk on constituents that
                // carry none.
                facets.Add(new CAContractFacet("junk/geyser counts",
                    CAExecutionMechanism.MaskedPlacement,
                    CAReadiness.ImplementedUnverified,
                    "per-constituent count allocation and masked placement "
                    + "installed; junk spacing (minSpacing/placementFactor) is "
                    + "still native"));

            if (Math.Abs(def.fishPopulationFactor - 1f) > 0.0001f)
                // Tile.FishPopulationFactor is a per-tile property. A region
                // holds several water bodies owned by different constituents.
                facets.Add(new CAContractFacet("fishPopulationFactor",
                    CAExecutionMechanism.Unsupported, CAReadiness.Unsupported,
                    "per-water-body effect; requires water-body ownership, "
                    + "which constituent cell ownership does not supply"));

            if (Math.Abs(def.plantDensityFactor - 1f) > 0.0001f)
                facets.Add(new CAContractFacet("plantDensityFactor",
                    CAExecutionMechanism.ProjectedScalar,
                    CAReadiness.ImplementedUnverified,
                    "per-cell substitution installed at "
                    + "WildPlantSpawner.CheckSpawnWildPlantAt; the map-wide "
                    + "plant budget is still root-derived"));

            if (Math.Abs(def.animalDensityFactor - 1f) > 0.0001f)
                // Tile.AnimalDensity feeds a whole-map population target and
                // animals roam across constituent boundaries, so a single
                // area-weighted regional value is defensible here where it is
                // not for junk or ponds. Flagged as a decision, not assumed.
                facets.Add(new CAContractFacet("animalDensityFactor",
                    CAExecutionMechanism.ProjectedScalar,
                    CAReadiness.Unsupported,
                    "whole-map roaming population; area-weighted mean is "
                    + "defensible but undecided, and not implemented"));

            if (def.preventsPondGeneration || def.preventPatches)
            {
                bool redirectHealthy = CARegionalTerrainFromOwnerPatch
                    .RedirectHealthy;
                facets.Add(new CAContractFacet("suppression flags",
                    CAExecutionMechanism.ProjectedScalar,
                    redirectHealthy ? CAReadiness.ImplementedUnverified
                        : CAReadiness.Unsupported,
                    redirectHealthy
                        ? "per-cell suppression installed: TerrainFrom now "
                            + "resolves the constituent owning the cell"
                        : "per-cell suppression redirect is unavailable: "
                            + CARegionalTerrainFromOwnerPatch
                                .RedirectHealthSummary));
            }

            // This is not one of TerrainFrom's per-cell reads. Native
            // GenStep_ElevationFertility consumes it once, before any cell is
            // available, and suppresses the base elevation field for the
            // entire map. A TerrainFrom redirect cannot recover that missing
            // field, and admitting (for example) one Cavern carrier would let
            // the anchored area's flag flatten every other area.
            if (def.preventNaturalElevation)
                facets.Add(new CAContractFacet("natural elevation",
                    CAExecutionMechanism.Unsupported,
                    CAReadiness.Unsupported,
                    "GenStep_ElevationFertility reads this flag map-wide; no "
                    + "per-area elevation suppression adapter is installed"));
        }

        // Exactly one facet, and only when the worker actually overrides a
        // generation hook. A worker with none places no generation obligation
        // at all, and its runtime facets carry the def instead.
        private static void AddGenerationFacet(TileMutatorWorker worker,
            List<CAContractFacet> facets)
        {
            if (worker == null) return;
            if (worker is TileMutatorWorker_CoastalAtoll)
                facets.Add(new CAContractFacet("generation",
                    CAExecutionMechanism.ConnectedRegional,
                    CAReadiness.ImplementedUnverified,
                    "outer sea, island ring, and lagoon are centered on the "
                    + "carrying area's visible land without clipping to "
                    + "ownership"));
            else if (worker is TileMutatorWorker_Cove)
                facets.Add(new CAContractFacet("generation",
                    CAExecutionMechanism.ConnectedRegional,
                    CAReadiness.ImplementedUnverified,
                    "basin and widening sea entrance are centered on the "
                    + "carrying area's visible land and cross ownership "
                    + "seams at one-source-tile scale"));
            else if (worker is TileMutatorWorker_Archipelago)
                facets.Add(new CAContractFacet("generation",
                    CAExecutionMechanism.ConnectedRegional,
                    CAReadiness.ImplementedUnverified,
                    "world-anchored island field is centered on the carrying "
                    + "area's visible land and blended across ownership seams "
                    + "at one-source-tile scale"));
            else if (worker is TileMutatorWorker_ArcheanTrees)
                facets.Add(new CAContractFacet("generation",
                    CAExecutionMechanism.MaskedPlacement,
                    CAReadiness.ImplementedUnverified,
                    "critical-structure adapter spawns the native mature "
                    + "tree target and terraformer soil only on the carrying "
                    + "area's visible land"));
            else if (worker is TileMutatorWorker_Stockpile)
                facets.Add(new CAContractFacet("generation",
                    CAExecutionMechanism.MaskedPlacement,
                    CAReadiness.ImplementedUnverified,
                    "post-fog AncientHatch adapter places the full occupied "
                    + "rect inside the carrying area's ownership"));
            else if (worker is TileMutatorWorker_Mountain)
                facets.Add(new CAContractFacet("generation",
                    CAExecutionMechanism.FrameAwareAnalytic,
                    CAReadiness.ImplementedUnverified,
                    "frame-aware adapter built; never run in a regional "
                    + "generation"));
            else if (worker is TileMutatorWorker_Caves)
                facets.Add(new CAContractFacet("generation",
                    CAExecutionMechanism.MaskedPlacement,
                    CAReadiness.ImplementedUnverified,
                    "ownership-masked dispatch built; cave continuity across "
                    + "seams unverified"));
            else if (worker is TileMutatorWorker_River)
                facets.Add(new CAContractFacet("generation",
                    CAExecutionMechanism.ConnectedRegional,
                    CAReadiness.ImplementedUnverified,
                    "CA owns the topology, but River reads the shared "
                    + "waterInfo.lakeCenter singleton and that hydrology "
                    + "coupling is unresolved"));
            else if (worker is TileMutatorWorker_Coast)
                facets.Add(new CAContractFacet("generation",
                    CAExecutionMechanism.ConnectedRegional,
                    CAReadiness.ImplementedUnverified,
                    "CA owns the topology; connected seam reconciliation "
                    + "unresolved"));
            else if (OverridesAnyGenerationHook(worker))
                facets.Add(new CAContractFacet("generation",
                    CAExecutionMechanism.Unsupported, CAReadiness.Unsupported,
                    "no regional execution contract; would run against the "
                    + "aggregate map"));
        }

        // Init and Tick were missing from the obligation set entirely. Both
        // iterate the ROOT tile's mutators and act on the per-def SINGLETON:
        // MapGenerator.cs:141-144 for Init, Map.cs:1136-1139 for Tick. A
        // mutator carried only by a member constituent is therefore never
        // initialised and never ticked, and one carried by the root is
        // initialised once, from map.Tile and map.Size, for the whole region.
        private static void AddLifecycleFacets(TileMutatorWorker worker,
            List<CAContractFacet> facets)
        {
            if (worker == null) return;

            if (Overrides(worker, nameof(TileMutatorWorker.Init),
                typeof(Map)))
            {
                if (worker is TileMutatorWorker_Coast)
                    facets.Add(new CAContractFacet("Init",
                        CAExecutionMechanism.ConnectedRegional,
                        CAReadiness.ImplementedUnverified,
                        "native aggregate-map noise is replaced by a shared "
                        + "feature-centered, one-source-tile projection-kernel "
                        + "field"));
                else if (worker is TileMutatorWorker_Caves)
                    facets.Add(new CAContractFacet("Init",
                        CAExecutionMechanism.MaskedPlacement,
                        CAReadiness.ImplementedUnverified,
                        "CA calls Init on a generation-local instance per "
                        + "carried def, so member-carried caves are "
                        + "initialised"));
                else if (worker is TileMutatorWorker_MixedBiome)
                    // Corrects an earlier note of mine. CA's Map.Biomes patch
                    // supplies the biome SET; it does not touch this grid.
                    facets.Add(new CAContractFacet("Init",
                        CAExecutionMechanism.RuntimeWorkerBehaviour,
                        CAReadiness.Unsupported,
                        "writes MixedBiomeMapComponent.biomeGrid - ONE "
                        + "map-wide per-cell grid whose axis comes from "
                        + "map.Tile's neighbors and whose extent is map.Size; "
                        + "the Map.Biomes patch supplies the biome set, not "
                        + "this grid"));
                else
                    facets.Add(new CAContractFacet("Init",
                        CAExecutionMechanism.Unsupported,
                        CAReadiness.Unsupported,
                        "MapGenerator.cs:141-144 inits ROOT mutators only, on "
                        + "the def singleton; a member-carried copy is never "
                        + "initialised and CA supplies no per-constituent "
                        + "Init"));
            }

            if (Overrides(worker, nameof(TileMutatorWorker.Tick),
                typeof(Map)))
                facets.Add(new CAContractFacet("Tick",
                    CAExecutionMechanism.RuntimeWorkerBehaviour,
                    CAReadiness.Unsupported,
                    "Map.cs:1136-1139 ticks the ROOT tile's mutators on the "
                    + "def singleton; a member-carried copy never ticks"));
        }

        // Behaviour supplied through def.Worker at runtime. What separates
        // these is whether the native consumer holds a CELL to substitute
        // against - not, as an earlier note of mine claimed, whether CA's
        // private generation instances leave the singleton stale. CA never
        // creates a private instance for a worker it does not drive.
        private static void AddRuntimeFacets(TileMutatorWorker worker,
            List<CAContractFacet> facets)
        {
            if (worker == null) return;

            if (Overrides(worker,
                nameof(TileMutatorWorker.AnimalCommonalityFactorFor),
                typeof(PawnKindDef), typeof(PlanetTile)))
                facets.Add(new CAContractFacet("AnimalCommonalityFactorFor",
                    CAExecutionMechanism.RuntimeWorkerBehaviour,
                    CAReadiness.ImplementedUnverified,
                    "WildAnimalSpawner.CommonalityOfAnimalNow holds the cell; "
                    + "CA recomputes the whole value from the owning "
                    + "constituent's tile"));

            if (Overrides(worker,
                nameof(TileMutatorWorker.PlantCommonalityFactorFor),
                typeof(ThingDef), typeof(PlanetTile)))
                facets.Add(new CAContractFacet("PlantCommonalityFactorFor",
                    CAExecutionMechanism.RuntimeWorkerBehaviour,
                    worker is TileMutatorWorker_PlantGrove
                        ? CAReadiness.ImplementedUnverified
                        : CAReadiness.Unsupported,
                    worker is TileMutatorWorker_PlantGrove
                        ? "PlantChoiceWeight supplies the cell; CA removes the "
                            + "cached anchor Grove factor and applies the "
                            + "owning area's factor at final choice"
                        : "read at WildPlantSpawner.cs:393 inside the map-wide "
                            + "cachedPlantCommonalities cache; no traced "
                            + "per-cell adapter for this worker"));

            if (Overrides(worker,
                nameof(TileMutatorWorker.AdditionalWildPlants),
                typeof(PlanetTile)))
                facets.Add(new CAContractFacet("AdditionalWildPlants",
                    CAExecutionMechanism.RuntimeWorkerBehaviour,
                    CAReadiness.ImplementedUnverified,
                    "selected member additions are unioned into the map-wide "
                    + "candidate/commonality caches, then PlantChoiceWeight "
                    + "uses its cell to retain mutator-only species on the "
                    + "carrying area's visible land"));

            if (Overrides(worker,
                nameof(TileMutatorWorker.MutateWeatherCommonalityFor),
                typeof(WeatherDef), typeof(PlanetTile),
                typeof(float).MakeByRefType()))
                facets.Add(new CAContractFacet("MutateWeatherCommonalityFor",
                    CAExecutionMechanism.RuntimeWorkerBehaviour,
                    CAReadiness.Unsupported,
                    "WeatherDecider.cs:203 iterates the ROOT tile's mutators, "
                    + "and weather is a single map-wide state, so a "
                    + "per-constituent value has no representation"));

            if (Overrides(worker, nameof(TileMutatorWorker.OnAddedToTile),
                typeof(PlanetTile)))
                facets.Add(new CAContractFacet("OnAddedToTile",
                    CAExecutionMechanism.RuntimeWorkerBehaviour,
                    CAReadiness.ProductionSupported,
                    "Tile.AddMutator calls it per tile at WORLD generation, "
                    + "before any map exists; regional projection cannot "
                    + "affect it"));

            if (worker is TileMutatorWorker_AnimalHabitat)
                // The facet that motivated aggregation. AnimalHabitat's
                // incident has no cell of its own, so it needs an explicit
                // region-level resolution contract separate from spawning.
                facets.Add(new CAContractFacet("GetAnimalKind (incident)",
                    CAExecutionMechanism.RuntimeWorkerBehaviour,
                    CAReadiness.ImplementedUnverified,
                    "the incident has no target cell, so selected habitat "
                    + "carriers are explicitly aggregated by resolved visible "
                    + "land area before the native 50% habitat roll"));

            if (worker is TileMutatorWorker_MixedBiome)
                facets.Add(new CAContractFacet("SecondaryBiome",
                    CAExecutionMechanism.RuntimeWorkerBehaviour,
                    CAReadiness.ImplementedUnverified,
                    "Tile.Biomes resolves it per tile and CA already replaces "
                    + "Map.Biomes with projection.AllBiomes; the biome SET is "
                    + "covered - the per-cell grid is not, see the Init "
                    + "facet"));
        }

        private static readonly string[] GenerationHooks =
        {
            nameof(TileMutatorWorker.GeneratePostElevationFertility),
            nameof(TileMutatorWorker.GeneratePostTerrain),
            nameof(TileMutatorWorker.GenerateCriticalStructures),
            nameof(TileMutatorWorker.GenerateNonCriticalStructures),
            nameof(TileMutatorWorker.GeneratePostFog),
        };

        // Signature-matched, so an overload never counts as an override.
        // Reflection walks the inheritance chain, so a def inheriting an
        // override from an ancestor is caught - an own-file regex is not
        // enough, which is how Pond, Oasis, ToxicLake, AbandonedColony and the
        // vents once looked hook-free.
        private static bool Overrides(TileMutatorWorker worker, string hook,
            params Type[] parameters)
        {
            if (worker == null) return false;
            var method = worker.GetType().GetMethod(hook,
                System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public,
                null, parameters, null);
            return method != null
                && method.DeclaringType != typeof(TileMutatorWorker);
        }

        private static bool OverridesAnyGenerationHook(TileMutatorWorker worker)
        {
            return GenerationHooks.Any(hook =>
                Overrides(worker, hook, typeof(Map)));
        }

        // Enumerates what generation will ACTUALLY execute: the mutators each
        // member tile carries by the time a plan exists. Landmark attachment
        // has already happened at world generation (WorldGenStep_Landmarks runs
        // after WorldGenStep_Mutators and calls Tile.AddMutator), so required
        // and selected-optional attachments are both already on the tile - the
        // set here is the realised one, not a probability.
        internal static CACompatibilityReport Evaluate(CARegionalPlan plan)
        {
            var report = new CACompatibilityReport();
            if (plan?.memberTileIds == null) return report;
            foreach (int tileId in plan.memberTileIds.Distinct())
            {
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(tileId);
                if (!tile.Valid) continue;
                Tile info = tile.Tile;
                if (info == null) continue;
                foreach (TileMutatorDef mutator in info.Mutators)
                {
                    if (mutator == null) continue;
                    var entry = new CACompatibilityEntry
                    {
                        SourceTileId = tileId,
                        DefName = mutator.defName,
                    };
                    entry.Facets.AddRange(FacetsFor(mutator));
                    report.Entries.Add(entry);
                }
            }
            return report;
        }
    }
}
