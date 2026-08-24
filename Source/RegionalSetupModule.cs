using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;

namespace ColonistAwareness
{
    [HarmonyPatch(typeof(Dialog_AdvancedGameConfig), nameof(
        Dialog_AdvancedGameConfig.DoWindowContents))]
    internal static class CARegionalMapSizeHeadingPatch
    {
        // Keeps the static MapSizes field on CA's catalog for other consumers.
        // The draw path uses DrawnSourceScales directly.
        [HarmonyPrefix]
        private static void Prefix()
        {
            CARegionalCompatibility.ReassertMapSizes();
        }

        // F-147 showed background preview generation replacing MapSizes while
        // the vanilla window was drawing. Reasserting it in a prefix was too
        // early, so this transpiler rewrites
        // the `ldsfld MapSizes` that feeds the size loop into a call to
        // CA's own source-scale array, so the drawn list is CA's
        // catalog no matter what any thread does to the field. Verified
        // in the live alysaliu flow: the dialog now draws 200..500 with
        // zero off-catalog rows while the field-writer is still active.
        internal static int[] DrawnSourceScales()
        {
            return (int[])CAExpandedLandmassProfile
                .SupportedLocalMapSizes.Clone();
        }

        // The main catalog carries every scale; the test-sizes read
        // routes here so a stray Prefs.TestMapSizes can never concat a
        // foreign size onto CA's list.
        internal static int[] NoExtraScales()
        {
            return Array.Empty<int>();
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo catalog = AccessTools.Method(
                typeof(CARegionalMapSizeHeadingPatch),
                nameof(DrawnSourceScales));
            MethodInfo none = AccessTools.Method(
                typeof(CARegionalMapSizeHeadingPatch),
                nameof(NoExtraScales));
            int redirected = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldstr
                    && instruction.operand as string == "MapSizeExtreme")
                    instruction.operand = "CA_MapSizeExtremeSourceScale";
                // Redirect the field reads that feed the size loop to
                // CA's own arrays. Match by field identity (name +
                // declaring type), robust across reflection instances
                // and IL shape. MapSizes -> CA catalog; TestMapSizes ->
                // empty, so the drawn list is exactly CA's catalog no
                // matter what any thread writes to either field.
                var field = instruction.operand as FieldInfo;
                if ((instruction.opcode == OpCodes.Ldsfld
                        || instruction.opcode == OpCodes.Ldsflda)
                    && field != null
                    && field.DeclaringType
                        == typeof(Dialog_AdvancedGameConfig)
                    && (field.Name == "MapSizes"
                        || field.Name == "TestMapSizes"))
                {
                    redirected++;
                    var call = new CodeInstruction(OpCodes.Call,
                        field.Name == "MapSizes" ? catalog : none);
                    call.labels = instruction.labels;
                    call.blocks = instruction.blocks;
                    yield return call;
                    continue;
                }
                yield return instruction;
            }
            Log.Message("[CA][MapSize] transpiler: redirected "
                + redirected + " MapSizes/TestMapSizes field-read(s) in "
                + "the size loop to CA's source-scale catalog; the drawn "
                + "list no longer trusts the mutable field");
        }
    }

    public enum CARegionalFactionSource
    {
        ExistingWorldFaction,
        NewWorldFaction
    }

    public sealed class CARegionalWorldPolicy : IExposable
    {
        // The partition kernel is now geography-aware (barrier costs
        // from elevation, hilliness, and biome transitions stop regions
        // from growing across mountain ridges and major ecotones). These
        // scalars remain as authored targets: the frequency band sets how
        // much land the player wants in joined regions, and the size
        // band caps how large a joined region can grow. Geography now
        // modulates the realized result — a mountainous world realizes
        // less joined land than a plains world at the same authored share
        // — but the targets remain meaningful authoring controls.
        public float stitchedRegionFrequencyMin = 0.25f;
        public float stitchedRegionFrequencyMax = 0.55f;
        // Rolled once per world from the world seed and then persisted.
        // Displayed as a readout, never edited: a realized frequency the
        // player could re-roll would not be a realized frequency.
        public float realizedStitchedRegionFrequency = -1f;

        public int stitchedRegionSizeMin = 3;
        public int stitchedRegionSizeMax = 5;

        // Controls generated major-settlement placement. Low values spread
        // authorized settlements across the region; high values favor placing
        // them near settlements already assigned. Pattern classification reads
        // the resulting placement and never consults this tendency.
        public float settlementConcentration = 0.50f;

        // Frontier holdings are separate from major settlements. Frequency
        // applies only to suitable land.
        public float frontierHoldingFrequency = 0.45f;

        // Frontier size is applied after a site exists. It controls the
        // factual resident count and material form, not a household identity.
        public float frontierHoldingSize = 0.50f;

        // Selects how many distinct actual world-faction sources appear in
        // automatically reallocated settlements. It never creates a faction.
        public float reallocationSourceVariety = 0.50f;

        // Every settlement retains its structural state. This controls how
        // often distant factions found new settlements while the player
        // plays. Political-belief pulse cadence is now a derived property
        // of institutional capacity, not an authored tendency.
        public float distantFoundingRate = 0.5f;
        // How developed the world's settlements are at the start. A real
        // cause that replaces the hidden per-tile hash: the authored
        // baseline is modulated by each settlement's position in the
        // settlement network (regional centers are more developed than
        // isolated outposts).
        public float worldDevelopment = 0.6f;
        // Coherent countertypical prevalence and extremity: how many
        // settlements diverge from their faction\u2019s norm (tech level,
        // political character) and how far that divergence goes. Low
        // variability preserves the dominant world character; high
        // variability produces outlier societies while the dominant
        // character still holds.
        public float worldVariability = 0.3f;
        // Longitudinal stability: how slowly endogenous state transitions
        // (culture drift, political shifts, institutional change, distant
        // founding) proceed. High stability slows change; low stability
        // accelerates it. This modulates legitimate transition cadence,
        // not randomness; it is distinct from the Storyteller\u2019s threat
        // pacing and from variability\u2019s spatial divergence.
        public float worldStability = 0.5f;
        // Preset provenance: the last bundle applied, so a diverged state
        // can say which preset it came from. Purely descriptive; the
        // eleven values above are the only causes.
        public string lastAppliedPresetKey;

        public void ExposeData()
        {
            Scribe_Values.Look(ref stitchedRegionFrequencyMin,
                "stitchedRegionFrequencyMin", 0.25f);
            Scribe_Values.Look(ref stitchedRegionFrequencyMax,
                "stitchedRegionFrequencyMax",
                0.55f);
            Scribe_Values.Look(ref realizedStitchedRegionFrequency,
                "realizedStitchedRegionFrequency", -1f);
            Scribe_Values.Look(ref stitchedRegionSizeMin,
                "stitchedRegionSizeMin", 3);
            Scribe_Values.Look(ref stitchedRegionSizeMax,
                "stitchedRegionSizeMax", 5);
            Scribe_Values.Look(ref settlementConcentration,
                "settlementConcentration",
                0.50f);
            Scribe_Values.Look(ref frontierHoldingFrequency,
                "frontierHoldingFrequency",
                0.45f);
            Scribe_Values.Look(ref frontierHoldingSize,
                "frontierHoldingSize", 0.50f);
            Scribe_Values.Look(ref reallocationSourceVariety,
                "reallocationSourceVariety", 0.50f);
            Scribe_Values.Look(ref distantFoundingRate,
                "distantFoundingRate", 0.5f);
            Scribe_Values.Look(ref worldDevelopment,
                "worldDevelopment", 0.6f);
            Scribe_Values.Look(ref worldVariability,
                "worldVariability", 0.3f);
            Scribe_Values.Look(ref worldStability,
                "worldStability", 0.5f);
            Scribe_Values.Look(ref lastAppliedPresetKey,
                "lastAppliedPresetKey");
        }

        // Rolled once per world and then fixed.
        internal float ResolveStitchedRegionFrequency()
        {
            if (realizedStitchedRegionFrequency >= 0f)
                return realizedStitchedRegionFrequency;
            float low = Mathf.Min(stitchedRegionFrequencyMin,
                stitchedRegionFrequencyMax);
            float high = Mathf.Max(stitchedRegionFrequencyMin,
                stitchedRegionFrequencyMax);
            int seed = 0;
            try { seed = Verse.Find.World.info.Seed; }
            catch { }
            realizedStitchedRegionFrequency =
                CAWorldTendencyCausalKernel.ResolveRange(seed, 0,
                    826351197, low, high);
            // Runtime evidence for the tendencies dialog readback: the
            // authored band, the realized value, and who consumes it.
            Log.Message("[CA][Tendencies] realized connected-region share "
                + realizedStitchedRegionFrequency.ToString("F2")
                + " from authored band " + low.ToString("F2") + ".."
                + high.ToString("F2") + " (seed " + seed + "); consumed "
                + "by region reallocation when generated settlements "
                + "join adjacent world tiles");
            return realizedStitchedRegionFrequency;
        }

        // Resolve the global tendency for one generated region. The realized
        // world share is rolled once; membership and stitched-region size are
        // keyed to its world tile, so a range
        // produces genuine geographic variation rather than one size repeated
        // everywhere.
        internal int ResolveRequestedExtent(PlanetTile root,
            int availableTiles)
        {
            float stitchedRegionFrequency = ResolveStitchedRegionFrequency();
            int seed = 0;
            try { seed = Verse.Find.World.info.Seed; }
            catch { }
            return CAWorldTendencyCausalKernel.RequestedExtent(seed,
                root.Valid ? root.tileId : 0, stitchedRegionFrequency,
                stitchedRegionSizeMin, stitchedRegionSizeMax,
                availableTiles);
        }

        internal CARegionalWorldPolicy Copy()
        {
            return new CARegionalWorldPolicy
            {
                stitchedRegionFrequencyMin = stitchedRegionFrequencyMin,
                stitchedRegionFrequencyMax = stitchedRegionFrequencyMax,
                realizedStitchedRegionFrequency = realizedStitchedRegionFrequency,
                stitchedRegionSizeMin = stitchedRegionSizeMin,
                stitchedRegionSizeMax = stitchedRegionSizeMax,
                settlementConcentration = settlementConcentration,
                frontierHoldingFrequency = frontierHoldingFrequency,
                frontierHoldingSize = frontierHoldingSize,
                reallocationSourceVariety = reallocationSourceVariety,
                distantFoundingRate = distantFoundingRate,
                worldDevelopment = worldDevelopment,
                worldVariability = worldVariability,
                worldStability = worldStability,
                lastAppliedPresetKey = lastAppliedPresetKey
            };
        }

        internal void PopulateDerived(CARegionalPlan plan,
            CAExpandedLandmassProfile profile, Faction parentFaction)
        {
            if (plan == null) return;
            plan.settlements.Clear();
            plan.factions.Clear();
            plan.relations.Clear();
            plan.frontierHoldings.Clear();
            // Seed population from the footprint anchor, not the landing tile,
            // so moving the landing does not reroll factions or relations.
            int seed = Gen.HashCombineInt(Verse.Find.World.info.Seed,
                plan.bundleRootTileId, profile.Size,
                plan.RegionTileCount ^ 197731);

            // Major-settlement count comes only from RimWorld settlements
            // already authorized by world population. Starting-region rows
            // with ScenarioOverride are the separate explicit exception.
            var footprint = new HashSet<int>(plan.ReservedTileIds);
            // SETTLEMENTS STANDING ON THE REGION'S OWN GROUND ARE THE
            // REGION'S SETTLEMENTS. The persistent partition assigns
            // membership before world settlements scatter, so a region's
            // members may already carry them; they are mandatory sources,
            // realized at their actual tiles, before any nearby-pool
            // reallocation is considered.
            var residents = new List<Settlement>();
            var pool = new List<Settlement>();
            foreach (Settlement settlement in Verse.Find.WorldObjects
                ?.Settlements ?? new List<Settlement>())
            {
                if (settlement == null || settlement.Faction == null
                    || settlement.Faction.IsPlayer
                    || !CARegionalPlanUtility.IsEligibleExistingFaction(
                        settlement.Faction)) continue;
                bool isArrivalAnchor = settlement.Tile.tileId
                    == plan.startTileId;
                // All settlements on the region own member tiles are
                // authoritative anchors: they stay on the world map,
                // keep their vanilla layout, and get CAO composition.
                // Only pool settlements from outside the region are
                // reallocated and consumed.
                bool sameFaction = settlement.Faction == parentFaction;
                if (footprint.Contains(settlement.Tile.tileId))
                {
                    // ARRIVAL MUST NOT CHANGE COMPOSITION. This skipped a
                    // settlement standing on the arrival tile, and the
                    // arrival tile defaults to whichever member the
                    // region was entered through - so the same region
                    // absorbed a different set of settlements depending
                    // on where the player came in, and that difference
                    // was frozen into the scribed plan at first
                    // materialization. A region is one map spanning its
                    // members, so a settlement on the arrival member is
                    // simply a settlement inside the region like any
                    // other, and it is absorbed as one rather than
                    // silently dropped out of the world.
                    residents.Add(settlement);
                    continue;
                }
                float distance;
                try
                {
                    distance = Verse.Find.WorldGrid.ApproxDistanceInTiles(
                        plan.BundleRoot, settlement.Tile);
                }
                catch { continue; }
                if (distance <= 14f) pool.Add(settlement);
            }
            residents = residents.OrderBy(item => item.Tile.tileId)
                .ToList();
            pool = pool.OrderBy(item =>
            {
                try
                {
                    return Verse.Find.WorldGrid.ApproxDistanceInTiles(
                        plan.BundleRoot, item.Tile);
                }
                catch { return float.MaxValue; }
            }).ThenBy(item => item.Tile.tileId).ToList();

            List<int> destinations = plan.memberTileIds.Where(id =>
                id != plan.startTileId && PlacementLandCapacity(id) > 0)
                .ToList();
            int physicalCapacity = destinations.Sum(id =>
                Math.Max(1, PlacementLandCapacity(id) - 1));
            int count = Math.Min(8, Math.Min(pool.Count, physicalCapacity));
            List<Settlement> sources = SelectSettlementSources(pool, count,
                reallocationSourceVariety);
            var usedByTile = new Dictionary<int, int>();
            var assignedTiles = new List<int>();
            int slot = 0;

            CARegionalFactionPlan GroupFor(Settlement source)
            {
                CARegionalFactionPlan group = plan.factions.FirstOrDefault(
                    item => item.source
                            == CARegionalFactionSource.ExistingWorldFaction
                        && item.existingFactionLoadId
                            == source.Faction.loadID);
                if (group != null) return group;
                int key = CARegionalPlanUtility.LowestFreeFactionKey(plan);
                group = new CARegionalFactionPlan
                {
                    key = key,
                    source = CARegionalFactionSource.ExistingWorldFaction,
                    existingFactionLoadId = source.Faction.loadID,
                    playerRelation = source.Faction.PlayerRelationKind,
                    authorPlayerRelation = false,
                    visibleInWorld = true
                };
                plan.factions.Add(group);
                foreach (CARegionalFactionPlan other in plan.factions
                    .Where(item => item != group))
                {
                    Faction left = source.Faction;
                    Faction right = CARegionalPlanUtility.FactionByLoadId(
                        other.existingFactionLoadId);
                    if (left == null || right == null) continue;
                    FactionRelationKind relation =
                        left.RelationKindWith(right);
                    // Automatic relations preserve an actual RimWorld
                    // relation. Starting Region overrides remain separate.
                    plan.SetRelation(group.key, other.key, relation,
                        CARegionalRelationSource.NativeExisting);
                }
                return group;
            }

            // The world's own record for a standing settlement, so an
            // absorbed place keeps the population it already had.
            int WorldPopulationAt(int tileId)
            {
                CARegionalWorldSettlementState state =
                    CARegionalWorldComponent.Current
                        ?.WorldSettlementStateAt(tileId);
                return state == null ? -1 : state.population;
            }

            // Residents first: their ground is inside the map about to
            // exist, so they are absorbed where they stand, outside the
            // reallocation count and concentration scoring.
            foreach (Settlement resident in residents)
            {
                CARegionalFactionPlan group = GroupFor(resident);
                int destination = resident.Tile.tileId;
                bool anchor = true;
                usedByTile[destination] = usedByTile.TryGetValue(destination,
                    out int prior) ? prior + 1 : 1;
                assignedTiles.Add(destination);
                plan.settlements.Add(new CARegionalSettlementPlan
                {
                    slot = slot,
                    memberTileId = destination,
                    OwningFactionKey = group.key,
                    // Every settlement on the region own ground is an
                    // anchor. It keeps its vanilla layout and gets CAO
                    // composition. Pool settlements from outside the
                    // region are still reallocated and consumed.
                    populationOrigin = anchor
                        ? CASettlementOrigin.Unset
                        : CASettlementOrigin.ReallocatedFromWorldPool,
                    reallocatedFromTileId = anchor ? -1 : destination,
                    siteClusterKey = destination,
                    absorbedWorldPopulation = WorldPopulationAt(destination),
                    persistent = true
                });
                slot++;
            }

            for (int i = 0; i < sources.Count; i++)
            {
                Settlement source = sources[i];
                CARegionalFactionPlan group = GroupFor(source);

                int destination = SelectDestination(destinations, usedByTile,
                    assignedTiles, plan.bundleRootTileId, seed, slot,
                    settlementConcentration);
                if (destination < 0) break;
                usedByTile[destination] = usedByTile.TryGetValue(destination,
                    out int prior) ? prior + 1 : 1;
                assignedTiles.Add(destination);
                plan.settlements.Add(new CARegionalSettlementPlan
                {
                    slot = slot,
                    memberTileId = destination,
                    OwningFactionKey = group.key,
                    populationOrigin = CASettlementOrigin
                        .ReallocatedFromWorldPool,
                    reallocatedFromTileId = source.Tile.tileId,
                    siteClusterKey = destination,
                    // Reallocated sources move, but the people move with
                    // them: the population is the source settlement's.
                    absorbedWorldPopulation =
                        WorldPopulationAt(source.Tile.tileId),
                    persistent = true
                });
                slot++;
            }

            CARegionalPlanUtility.EnsureRelationRows(plan);
            // Represented institutions are inputs to local services and settlement
            // programs. Resolve it before settlement state so automatic
            // regions follow the same dependency order as Starting Region.
            foreach (CARegionalFactionPlan group in plan.factions
                .Where(item => item != null))
            {
                group.EnsureCultureAndPolitics(plan);
                CAFactionAxes.Derive(plan, group);
            }
            CARegionalSettlements.DeriveSettlementPattern(plan);
            plan.creationSummary += "; " + plan.settlements.Count
                + " major settlement"
                + (plan.settlements.Count == 1 ? "" : "s")
                + " reallocated from RimWorld's nearby settlement pool; "
                + "placement and frontier holdings realized once; faction "
                + "relations preserved from the reallocated world sources";
        }

        private static List<Settlement> SelectSettlementSources(
            List<Settlement> pool, int count, float variety)
        {
            var selected = new List<Settlement>();
            var remaining = new List<Settlement>(pool);
            int distinctAvailable = remaining.Select(item => item.Faction)
                .Where(item => item != null).Distinct().Count();
            int targetDistinct = CAWorldTendencyCausalKernel
                .SourceVarietyTargetDistinct(count, distinctAvailable,
                    variety);
            while (selected.Count < count && remaining.Count > 0)
            {
                int represented = selected.Select(item => item.Faction)
                    .Where(item => item != null).Distinct().Count();
                bool different = selected.Count > 0
                    && represented < targetDistinct;
                List<Settlement> preferred = different
                    ? remaining.Where(item => selected.All(chosen =>
                        chosen.Faction != item.Faction)).ToList()
                    : selected.Count == 0 ? remaining
                    : remaining.Where(item => item.Faction
                        == selected[0].Faction).ToList();
                if (preferred.Count == 0) preferred = remaining;
                Settlement next = preferred[0];
                selected.Add(next);
                remaining.Remove(next);
            }
            return selected;
        }

        private static int SelectDestination(List<int> destinations,
            Dictionary<int, int> usedByTile, List<int> assignedTiles,
            int anchorTileId, int seed, int slot, float concentration)
        {
            int best = -1;
            double bestScore = double.MinValue;
            foreach (int tileId in destinations)
            {
                int land = PlacementLandCapacity(tileId);
                int used = usedByTile.TryGetValue(tileId, out int count)
                    ? count : 0;
                int capacity = Math.Max(1, land - 1);
                if (used >= capacity) continue;
                int access = (CARegionalPlanUtility.ConstituentHasRoad(tileId)
                        ? 1 : 0)
                    + (CARegionalPlanUtility.ConstituentHasRiver(tileId)
                        || CARegionalPlanUtility.ConstituentIsCoastal(tileId)
                        ? 1 : 0);
                float anchorDistance = TileDistance(anchorTileId, tileId);
                float nearest = assignedTiles.Count == 0 ? -1f
                    : assignedTiles.Min(other => TileDistance(other, tileId));
                bool route = CARegionalPlanUtility.ConstituentHasRoad(tileId)
                    || CARegionalPlanUtility.ConstituentHasRiver(tileId);
                double score = CAWorldTendencyCausalKernel.PlacementScore(
                    seed, tileId ^ slot * 7919, land, access, route,
                    anchorDistance, nearest, concentration);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = tileId;
                }
            }
            return best;
        }

        private static int PlacementLandCapacity(int tileId)
        {
            return CASettlementEnvironment.ForTile(tileId).LandCapacity;
        }

        private static float TileDistance(int leftId, int rightId)
        {
            try
            {
                return Verse.Find.WorldGrid.ApproxDistanceInTiles(
                    CARegionalPlanUtility.SurfaceTile(leftId),
                    CARegionalPlanUtility.SurfaceTile(rightId));
            }
            catch { return 2f; }
        }
    }

    public sealed class CARegionalFactionPlan : IExposable
    {
        public int key;
        public CARegionalFactionSource source;
        public int existingFactionLoadId = -1;
        public string customFactionDefName;
        public string customName;
        public FactionRelationKind playerRelation = FactionRelationKind.Neutral;
        public bool authorPlayerRelation = true;
        public bool visibleInWorld = true;
        public Faction resolvedFaction;
        public int resolvedFactionLoadId = -1;

        // True when the player explicitly added this faction. An explicit
        // faction remains in the draft even when it has no settlement.
        public bool authored;

        // Current authority shared between this faction's settlements.
        // Generated from represented institutions unless explicitly set.
        public byte settlementAuthority; // CASettlementAuthority
        public bool settlementAuthorityExplicit;

        // Represented institutions: instituted state, never normative Political Order.
        public List<CAAxisEntry> factionStructure = new List<CAAxisEntry>();
        // Explicit evidence-only state for a faction whose institutions are
        // intentionally unknown. Established generated factions otherwise
        // realize every institutional axis at initialization.
        public bool institutionalStateIncomplete;

        // Faction-owned Culture, Political Order, and Technological Knowledge.
        // Ideoligion remains
        // native faction state. Population groups may carry different
        // Ideoligions, beliefs, and faction affiliation.
        public CACulture culture = new CACulture();
        public CAPoliticalBeliefs politicalBeliefs =
            new CAPoliticalBeliefs();
        public CATechnologicalKnowledge technologicalKnowledge =
            new CATechnologicalKnowledge();

        // Independent factions with the same key belong to one federation.
        public int federationKey = -1;
        // Federation kind selects the responsibilities its members share.
        public string federationKind;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key", 0);
            Scribe_Values.Look(ref source, "source",
                CARegionalFactionSource.ExistingWorldFaction);
            Scribe_Values.Look(ref existingFactionLoadId,
                "existingFactionLoadId", -1);
            Scribe_Values.Look(ref customFactionDefName,
                "customFactionDefName");
            Scribe_Values.Look(ref customName, "customName");
            Scribe_Values.Look(ref playerRelation, "playerRelation",
                FactionRelationKind.Neutral);
            Scribe_Values.Look(ref authorPlayerRelation,
                "authorPlayerRelation", true);
            Scribe_Values.Look(ref visibleInWorld, "visibleInWorld", true);
            Scribe_References.Look(ref resolvedFaction, "resolvedFaction");
            Scribe_Values.Look(ref resolvedFactionLoadId,
                "resolvedFactionLoadId", -1);
            Scribe_Values.Look(ref authored, "authored", false);
            Scribe_Values.Look(ref settlementAuthority,
                "settlementAuthority", (byte)0);
            Scribe_Values.Look(ref settlementAuthorityExplicit,
                "settlementAuthorityExplicit", false);
            Scribe_Collections.Look(ref factionStructure, "factionStructure",
                LookMode.Deep);
            Scribe_Values.Look(ref institutionalStateIncomplete,
                "institutionalStateIncomplete", false);
            Scribe_Deep.Look(ref culture, "culture");
            Scribe_Deep.Look(ref politicalBeliefs,
                "politicalBeliefs");
            Scribe_Deep.Look(ref technologicalKnowledge,
                "technologicalKnowledge");
            Scribe_Values.Look(ref federationKey, "federationKey", -1);
            Scribe_Values.Look(ref federationKind, "federationKind");
        }

        // The Ideoligion currently held by an existing faction.
        internal Ideo LivingIdeo
        {
            get
            {
                Faction faction = resolvedFaction;
                if (faction == null
                    && source == CARegionalFactionSource
                        .ExistingWorldFaction)
                    faction = CARegionalPlanUtility.FactionByLoadId(
                        existingFactionLoadId);
                return faction?.ideos?.PrimaryIdeo;
            }
        }

        internal void EnsureCultureAndPolitics(CARegionalPlan plan)
        {
            CAFactionStartingState.EnsureFaction(plan, this);
        }

        internal string Summary
        {
            get
            {
                if (source == CARegionalFactionSource.ExistingWorldFaction)
                {
                    Faction faction = CARegionalPlanUtility.FactionByLoadId(
                        existingFactionLoadId);
                    return !CARegionalPlanUtility.IsEligibleExistingFaction(
                            faction) ? "Choose existing faction"
                        : faction.Name + " · " + TechnologySummary;
                }
                FactionDef def = CARegionalPlanUtility.FactionDefByName(
                    customFactionDefName);
                if (!CARegionalPlanUtility.CanMaterializeSettlement(def))
                    return "Choose new faction template";
                string name = customName.NullOrEmpty()
                    ? "New " + def.LabelCap.ToString()
                    : customName;
                return name + " · " + TechnologySummary;
            }
        }

        // The FactionDef this group will actually produce, whichever
        // way it was chosen. Name generation needs it to draw from the
        // same name-maker the world itself would have used.
        internal FactionDef ResolvedFactionDef
        {
            get
            {
                if (source == CARegionalFactionSource.ExistingWorldFaction)
                {
                    Faction faction = CARegionalPlanUtility.FactionByLoadId(
                        existingFactionLoadId);
                    return CARegionalPlanUtility.IsEligibleExistingFaction(
                        faction) ? faction.def : null;
                }
                FactionDef def = CARegionalPlanUtility.FactionDefByName(
                    customFactionDefName);
                return CARegionalPlanUtility.CanMaterializeSettlement(def)
                    ? def : null;
            }
        }

        internal string TechnologySummary
        {
            get
            {
                if (technologicalKnowledge?.domains?.Count > 0)
                    return CATechnologicalKnowledgeModel.Summary(
                        technologicalKnowledge);
                return ResolvedFactionDef == null
                    ? "choose faction" : "knowledge not set";
            }
        }
    }

    // Source of a major settlement's population. Vanilla OverallPopulation
    // limits the world settlement pool. A regional settlement either
    // reallocates one of those settlements or uses an explicit scenario
    // override. Frontier holdings use their own generation rules.
    public enum CASettlementOrigin
    {
        Unset,
        // Reuses a real world settlement named by `reallocatedFromTileId`.
        // Materialization removes that source before creating the regional
        // settlement so population is not duplicated.
        ReallocatedFromWorldPool,
        // The starting scenario deliberately exceeds the authorized
        // pool. Legitimate, and required to be explicit.
        ScenarioOverride
    }

    public sealed class CARegionalSettlementPlan : IExposable
    {
        public int slot;
        public int memberTileId = -1;
        public CASiteFactionLinks factionLinks = new CASiteFactionLinks();
        // Culture is always local to the settlement. Political Order,
        // Technological Knowledge, and institutions resolve through the owner
        // unless this site has no owner or an explicit local divergence.
        public CASiteLocalSocietyState localSociety;
        // Native faction definitions remain useful generation recipes. This
        // value never represents ownership, support, or population membership.
        public string generationFactionDefName;
        public CASettlementOrigin populationOrigin = CASettlementOrigin.Unset;
        public int reallocatedFromTileId = -1;
        public int siteClusterKey = -1;
        public int operationalRoleMask;
        // Realized settlement summaries. These are written once from saved
        // population, ground, complete operating contracts, routes, and
        // explicit history. They do not create the facts they summarize.
        public int residentPopulation = -1;
        public int landCapacity = -1;
        // Saved habitat contract. The environment declares requirements;
        // the established settlement's actual programs and technical means
        // declare capabilities. Generation consumes this result instead of
        // re-reading or re-rolling the biome.
        public int environmentSourceHash;
        public int habitatRequirementMask;
        public int requiredCapabilityTier;
        public int habitatCapabilityMask;
        public int missingHabitatRequirementMask;
        public byte habitatFoodRoute; // CAHabitatFoodRoute
        public bool habitatViable;
        public string habitatBlocker;
        public int realizedAccessInfrastructure = -1;
        // Saved route facts. Programs and confirmed generation consume these
        // values instead of consulting mutable world tiles again.
        public bool hasRoadAccess;
        public bool hasRiverAccess;
        public bool hasCoastalAccess;
        public int realizedServiceInfrastructure = -1;
        public int realizedCivicInfrastructure = -1;
        public int economicCapacity = -1;
        public int tradeConnectivity = -1;
        public int specialization = -1;
        public int historicalDevelopment = -1;
        public int urbanSupport = -1;
        public byte realizedScale; // CASettlementScale
        // Settlement populations, provision arrangements, and the realized
        // open settlement program are copied to the runtime record.
        public List<CASettlementPopulationGroup> populationGroups =
            new List<CASettlementPopulationGroup>();
        public List<CAProvisionArrangement> provisionArrangements =
            new List<CAProvisionArrangement>();
        // Population-level domestic demand is factual before pawns exist.
        // Unit identities are deliberately unresolved until native pawn
        // relations and owned beds can establish them.
        public List<CADomesticProvisionDemand> domesticProvisionDemands =
            new List<CADomesticProvisionDemand>();
        public List<CASettlementOperationalFact> operationalFacts =
            new List<CASettlementOperationalFact>();
        public CASettlementProgram settlementProgram =
            new CASettlementProgram();
        // The settlement's own persistent culture. It begins from its
        // populations' inherited cultures and changes only through recorded
        // historical transitions.
        public CACulture localCulture;
        // This settlement's realized regional role. Persisted so generation
        // cannot choose a different center.
        public byte realizedRole; // CASettlementRole
        public bool persistent = true;
        // Settlement form may be set directly or generated from its faction.
        public int authoredForm = -1;           // what it looks like
        // Authored causes. Like authoredForm, these are saved composition
        // facts the realization consumes and never writes: an explicit
        // population, ground grade, or established history wins over the
        // fact-derived default, and the summaries above realize from
        // whichever source applies. -1 means unauthored.
        public int authoredPopulation = -1;
        public int authoredLandCapacity = -1;
        public int authoredHistoricalDevelopment = -1;
        // The population this place already had on the world map before
        // the region materialized around it. A settlement the player
        // inspected on the globe is the same settlement after they
        // enter its region; without carrying its established population
        // the regional derivation invents a new one and the place can
        // change standing between the two views. Operator authoring
        // still wins over it. -1 means this settlement was not absorbed
        // from a standing world settlement.
        public int absorbedWorldPopulation = -1;
        // Empty uses the owning faction's name maker. The generated name is
        // shown during setup and may be rerolled.
        public string customName;

        public void ExposeData()
        {
            Scribe_Values.Look(ref slot, "slot", -1);
            Scribe_Values.Look(ref memberTileId, "memberTileId", -1);
            int legacyFactionKey = -1;
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref legacyFactionKey, "factionKey", -1);
            Scribe_Deep.Look(ref factionLinks, "factionLinks");
            Scribe_Deep.Look(ref localSociety, "localSociety");
            Scribe_Values.Look(ref generationFactionDefName,
                "generationFactionDefName");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (factionLinks == null)
                    factionLinks = new CASiteFactionLinks();
                if (legacyFactionKey >= 0
                    && factionLinks.ownership
                        == CASiteFactionReferenceKind.None)
                    factionLinks.SetRegionalOwner(legacyFactionKey);
            }
            Scribe_Values.Look(ref populationOrigin, "populationOrigin",
                CASettlementOrigin.Unset);
            Scribe_Values.Look(ref reallocatedFromTileId,
                "reallocatedFromTileId", -1);
            Scribe_Values.Look(ref siteClusterKey, "siteClusterKey", -1);
            Scribe_Values.Look(ref operationalRoleMask,
                "operationalRoleMask", 0);
            Scribe_Values.Look(ref residentPopulation,
                "residentPopulation", -1);
            Scribe_Values.Look(ref landCapacity, "landCapacity", -1);
            Scribe_Values.Look(ref environmentSourceHash,
                "environmentSourceHash", 0);
            Scribe_Values.Look(ref habitatRequirementMask,
                "habitatRequirementMask", 0);
            Scribe_Values.Look(ref requiredCapabilityTier,
                "requiredCapabilityTier", 0);
            Scribe_Values.Look(ref habitatCapabilityMask,
                "habitatCapabilityMask", 0);
            Scribe_Values.Look(ref missingHabitatRequirementMask,
                "missingHabitatRequirementMask", 0);
            Scribe_Values.Look(ref habitatFoodRoute,
                "habitatFoodRoute", (byte)0);
            Scribe_Values.Look(ref habitatViable, "habitatViable", false);
            Scribe_Values.Look(ref habitatBlocker, "habitatBlocker");
            Scribe_Values.Look(ref realizedAccessInfrastructure,
                "realizedAccessInfrastructure", -1);
            Scribe_Values.Look(ref hasRoadAccess, "hasRoadAccess", false);
            Scribe_Values.Look(ref hasRiverAccess, "hasRiverAccess", false);
            Scribe_Values.Look(ref hasCoastalAccess,
                "hasCoastalAccess", false);
            Scribe_Values.Look(ref realizedServiceInfrastructure,
                "realizedServiceInfrastructure", -1);
            Scribe_Values.Look(ref realizedCivicInfrastructure,
                "realizedCivicInfrastructure", -1);
            Scribe_Values.Look(ref economicCapacity,
                "economicCapacity", -1);
            Scribe_Values.Look(ref tradeConnectivity,
                "tradeConnectivity", -1);
            Scribe_Values.Look(ref specialization, "specialization", -1);
            Scribe_Values.Look(ref historicalDevelopment,
                "historicalDevelopment", -1);
            Scribe_Values.Look(ref urbanSupport, "urbanSupport", -1);
            Scribe_Values.Look(ref realizedScale,
                "realizedScale", (byte)0);
            Scribe_Collections.Look(ref populationGroups, "populationGroups", LookMode.Deep);
            Scribe_Collections.Look(ref provisionArrangements,
                "provisionArrangements",
                LookMode.Deep);
            Scribe_Collections.Look(ref domesticProvisionDemands,
                "domesticProvisionDemands", LookMode.Deep);
            Scribe_Collections.Look(ref operationalFacts,
                "operationalFacts", LookMode.Deep);
            Scribe_Deep.Look(ref settlementProgram, "settlementProgram");
            Scribe_Deep.Look(ref localCulture, "localCulture");
            Scribe_Values.Look(ref realizedRole, "realizedRole", (byte)0);
            Scribe_Values.Look(ref persistent, "persistent", true);
            Scribe_Values.Look(ref customName, "customName");
            Scribe_Values.Look(ref authoredForm, "authoredForm", -1);
            Scribe_Values.Look(ref authoredPopulation,
                "authoredPopulation", -1);
            Scribe_Values.Look(ref absorbedWorldPopulation,
                "absorbedWorldPopulation", -1);
            Scribe_Values.Look(ref authoredLandCapacity,
                "authoredLandCapacity", -1);
            Scribe_Values.Look(ref authoredHistoricalDevelopment,
                "authoredHistoricalDevelopment", -1);
        }

        internal int PhysicalClusterKey
        {
            get { return siteClusterKey >= 0 ? siteClusterKey : slot; }
        }

        internal int OwningFactionKey
        {
            get => factionLinks?.ownership
                    == CASiteFactionReferenceKind.RegionalFaction
                ? factionLinks.ownerRegionalFactionKey : -1;
            set
            {
                if (factionLinks == null)
                    factionLinks = new CASiteFactionLinks();
                if (value < 0) factionLinks.SetNoOwner();
                else factionLinks.SetRegionalOwner(value);
            }
        }

        internal bool HasFactionOwner => CASiteState.HasOwner(factionLinks);
    }

    // One persisted frontier holding. Frequency owns how many rows exist;
    // size owns resident count and material form after site eligibility.
    // Domestic identity is established later from actual pawn relations.
    public sealed class CAFrontierHoldingPlan : IExposable
    {
        public const int CurrentSchemaVersion = 2;
        public int schemaVersion = CurrentSchemaVersion;
        public int key;
        public string siteName;
        public int memberTileId = -1;
        public int residentCount = 1;
        public int landCapacity;
        public int materialLevel;
        public int form; // 0 cabin, 1 established homestead
        // Ownership and material support remain separate facts. A frontier
        // holding may have either, both, or neither.
        public CASiteFactionLinks factionLinks = new CASiteFactionLinks();
        public CACulture localCulture;
        public CASiteLocalSocietyState localSociety;
        public List<CASettlementPopulationGroup> populationGroups =
            new List<CASettlementPopulationGroup>();
        public string generationFactionDefName;
        public int environmentSourceHash;
        public int habitatRequirementMask;
        public int requiredCapabilityTier;
        public int capabilityTier;
        public int habitatCapabilityMask;
        public int missingHabitatRequirementMask;
        public byte habitatFoodRoute; // CAHabitatFoodRoute
        public string materializationFailure;
        public bool materialized;
        public int materializedMapId = -1;
        public int firstMaterializationTick = -1;
        public IntVec3 site = IntVec3.Invalid;
        public List<int> residentPawnIds = new List<int>();
        public List<CASettlementResidenceAssignment> residenceAssignments =
            new List<CASettlementResidenceAssignment>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion",
                1);
            Scribe_Values.Look(ref key, "key", 0);
            Scribe_Values.Look(ref siteName, "siteName");
            Scribe_Values.Look(ref memberTileId, "memberTileId", -1);
            Scribe_Values.Look(ref residentCount, "residentCount", 1);
            Scribe_Values.Look(ref landCapacity, "landCapacity", 0);
            Scribe_Values.Look(ref materialLevel, "materialLevel", 0);
            Scribe_Values.Look(ref form, "form", 0);
            int legacySupportingFactionKey = -1;
            int legacySupportingFactionLoadId = -1;
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Values.Look(ref legacySupportingFactionKey,
                    "supportingFactionKey", -1);
                Scribe_Values.Look(ref legacySupportingFactionLoadId,
                    "supportingFactionLoadId", -1);
            }
            Scribe_Deep.Look(ref factionLinks, "factionLinks");
            Scribe_Deep.Look(ref localCulture, "localCulture");
            Scribe_Deep.Look(ref localSociety, "localSociety");
            Scribe_Collections.Look(ref populationGroups,
                "populationGroups", LookMode.Deep);
            Scribe_Values.Look(ref generationFactionDefName,
                "generationFactionDefName");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (factionLinks == null)
                    factionLinks = new CASiteFactionLinks();
                if (factionLinks.support
                        == CASiteFactionReferenceKind.None
                    && legacySupportingFactionKey >= 0)
                    factionLinks.SetRegionalSupport(
                        legacySupportingFactionKey);
                else if (factionLinks.support
                        == CASiteFactionReferenceKind.None
                    && legacySupportingFactionLoadId >= 0)
                    factionLinks.SetWorldSupport(
                        legacySupportingFactionLoadId);
            }
            Scribe_Values.Look(ref environmentSourceHash,
                "environmentSourceHash", 0);
            Scribe_Values.Look(ref habitatRequirementMask,
                "habitatRequirementMask", 0);
            Scribe_Values.Look(ref requiredCapabilityTier,
                "requiredCapabilityTier", 0);
            Scribe_Values.Look(ref capabilityTier, "capabilityTier", 0);
            Scribe_Values.Look(ref habitatCapabilityMask,
                "habitatCapabilityMask", 0);
            Scribe_Values.Look(ref missingHabitatRequirementMask,
                "missingHabitatRequirementMask", 0);
            Scribe_Values.Look(ref habitatFoodRoute,
                "habitatFoodRoute", (byte)0);
            Scribe_Values.Look(ref materializationFailure,
                "materializationFailure");
            Scribe_Values.Look(ref materialized, "materialized", false);
            Scribe_Values.Look(ref materializedMapId,
                "materializedMapId", -1);
            Scribe_Values.Look(ref firstMaterializationTick,
                "firstMaterializationTick", -1);
            Scribe_Values.Look(ref site, "site", IntVec3.Invalid);
            Scribe_Collections.Look(ref residentPawnIds,
                "residentPawnIds", LookMode.Value);
            Scribe_Collections.Look(ref residenceAssignments,
                "residenceAssignments", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (siteName.NullOrEmpty())
                    siteName = (form == 1 ? "Frontier homestead "
                        : "Frontier cabin ") + (key + 1);
                if (factionLinks == null)
                    factionLinks = new CASiteFactionLinks();
                if (populationGroups == null)
                    populationGroups = new List<CASettlementPopulationGroup>();
                if (residentPawnIds == null)
                    residentPawnIds = new List<int>();
                if (residenceAssignments == null)
                    residenceAssignments =
                        new List<CASettlementResidenceAssignment>();
            }
        }

        internal CAFrontierHoldingPlan Copy()
        {
            return new CAFrontierHoldingPlan
            {
                schemaVersion = schemaVersion,
                key = key,
                siteName = siteName,
                memberTileId = memberTileId,
                residentCount = residentCount,
                landCapacity = landCapacity,
                materialLevel = materialLevel,
                form = form,
                factionLinks = factionLinks?.Copy(),
                localCulture = localCulture?.Copy(),
                localSociety = localSociety?.Copy(),
                populationGroups = (populationGroups
                    ?? new List<CASettlementPopulationGroup>())
                    .Where(value => value != null)
                    .Select(value => value.Copy()).ToList(),
                generationFactionDefName = generationFactionDefName,
                environmentSourceHash = environmentSourceHash,
                habitatRequirementMask = habitatRequirementMask,
                requiredCapabilityTier = requiredCapabilityTier,
                capabilityTier = capabilityTier,
                habitatCapabilityMask = habitatCapabilityMask,
                missingHabitatRequirementMask =
                    missingHabitatRequirementMask,
                habitatFoodRoute = habitatFoodRoute,
                materializationFailure = materializationFailure,
                materialized = materialized,
                materializedMapId = materializedMapId,
                firstMaterializationTick = firstMaterializationTick,
                site = site,
                residentPawnIds = new List<int>(residentPawnIds
                    ?? new List<int>()),
                residenceAssignments = (residenceAssignments
                    ?? new List<CASettlementResidenceAssignment>())
                    .Where(value => value != null).Select(value =>
                        new CASettlementResidenceAssignment
                        {
                            schemaVersion = value.schemaVersion,
                            sequence = value.sequence,
                            pawnId = value.pawnId,
                            populationGroupKey = value.populationGroupKey,
                            active = value.active,
                            entryKind = value.entryKind,
                            entryEvidence = value.entryEvidence,
                            enteredTick = value.enteredTick,
                            exitKind = value.exitKind,
                            exitEvidence = value.exitEvidence,
                            exitedTick = value.exitedTick
                        }).ToList()
            };
        }

        internal int SupportingFactionKey => factionLinks?.support
                == CASiteFactionReferenceKind.RegionalFaction
            ? factionLinks.supportRegionalFactionKey : -1;

        internal int SupportingFactionLoadId => factionLinks?.support
                == CASiteFactionReferenceKind.WorldFaction
            ? factionLinks.supportWorldFactionLoadId : -1;
    }

    public enum CARegionalRelationSource : byte
    {
        Unset = 0,
        NativeExisting = 1,
        NativeInitial = 2,
        Authored = 3
    }

    public sealed class CARegionalRelationPlan : IExposable
    {
        public int leftFactionKey;
        public int rightFactionKey;
        public FactionRelationKind relation = FactionRelationKind.Neutral;
        public CARegionalRelationSource source = CARegionalRelationSource.Unset;

        public void ExposeData()
        {
            Scribe_Values.Look(ref leftFactionKey, "leftFactionKey", 0);
            Scribe_Values.Look(ref rightFactionKey, "rightFactionKey", 0);
            Scribe_Values.Look(ref relation, "relation",
                FactionRelationKind.Neutral);
            Scribe_Values.Look(ref source, "source",
                CARegionalRelationSource.Unset);
        }
    }

    public sealed class CARegionalPlan : IExposable
    {
        internal const int CurrentSchemaVersion = 16;

        // Schema 16 gives every inhabited site explicit, typed ownership and
        // support relationships. A site with no owner carries its own social
        // state; population affiliation remains independent. Schema 15 at
        // authoring epoch 14 is the one supported adjacent pending-draft
        // source and is upgraded atomically by CAPendingAuthoringDataEpoch.
        // Earlier development schemas are deliberately unsupported.
        public int schemaVersion = CurrentSchemaVersion;
        public string regionalId;
        // The player's geographic name for this realized region. This lives on
        // the same saved plan that owns the footprint, settlements and
        // materialization state; the world-map label is never a UI-only alias.
        public string regionName;
        // A moved region carries a name the player wrote. A geographic fallback
        // is recomputed from the new land instead of becoming stale geography.
        public bool regionNameAuthored;
        // mapSize is the local cell scale of one source world tile. Aggregate
        // backing dimensions are separate so region extent never masquerades as
        // local resolution.
        public int mapSize;
        // The extent the player asked the connected-land builder to seek.
        // regionTileCount below is the realized number of member areas. They
        // differ when a coast, impassable land, or occupied neighbor ends the
        // connected region before the requested extent can be reached.
        public int requestedRegionTileCount;
        public int regionTileCount;
        public int backingMapWidth;
        public int backingMapHeight;
        public int footprintRotation;
        public int bundleRootTileId = -1;
        public int startTileId = -1;
        public List<int> memberTileIds = new List<int>();
        public List<int> footprintTileIds = new List<int>();
        public List<CARegionalFactionPlan> factions =
            new List<CARegionalFactionPlan>();
        public List<CARegionalSettlementPlan> settlements =
            new List<CARegionalSettlementPlan>();
        public List<CARegionalRelationPlan> relations =
            new List<CARegionalRelationPlan>();
        public List<CAFrontierHoldingPlan> frontierHoldings =
            new List<CAFrontierHoldingPlan>();
        public CARegionalWorldPolicy worldPolicy;
        // Groundwater settings carried from the draft into generation.
        public CAGroundwaterTuning groundwater =
            new CAGroundwaterTuning();
        // Canonical authored feature shapes: per carried mutator, the
        // player's chosen degrees of freedom over CA's per-carrier
        // re-expression. Absent records mean the deterministic
        // identity-seeded realization.
        public List<CAAuthoredFeatureShape> featureShapes =
            new List<CAAuthoredFeatureShape>();
        // What the player faction brings and establishes at the founding
        // moment. Native Ideoligion remains on Faction.OfPlayer; this draft
        // persists the independent CA state and its native-Ideo receipt.
        public CAPlayerFoundingPlan playerFounding =
            new CAPlayerFoundingPlan();
        public bool operatorAuthored;

        // Candidate-plan lifecycle (P2):
        //   candidate -> preview/edit/reroll -> confirm -> authoritative
        //
        // candidateId is this candidate's generation identity. It is minted
        // when a candidate is created and RE-minted on reroll, so a rerolled
        // region is a different candidate rather than the same one mutated.
        // Feature-instance identity and every derived RNG seed hang off it,
        // which is what makes a preview reproducible: the same candidate always
        // yields the same features, and confirming changes nothing because the
        // id is carried through unchanged.
        //
        // confirmed marks the transition to the authoritative plan. Generation
        // consumes only a confirmed plan; inspecting, editing or rerolling a
        // candidate must never write durable world state.
        public string candidateId;
        public bool confirmed;

        // Stamped when a candidate was confirmed only because the
        // developer-exercise route admitted ImplementedUnverified content. Such
        // a plan is for disposable regional generation that produces runtime
        // evidence; a durable start must refuse it.
        public bool developerExercise;
        public string creationSummary;
        // Realized settlement pattern and scale, derived once from the
        // candidate's settlements and then persisted.
        public byte settlementPattern;   // CASettlementPattern
        public byte settlementScale;  // CASettlementScale
        public byte regionalRelationPattern; // CARegionalRelationPattern
        public bool settlementRealizationComplete;
        public int settlementRealizationSourceHash;
        // Reallocation receipts:
        // "tileId|factionLoadId|name" for every world settlement this
        // plan absorbed when map generation began. The receipt is the proof the
        // transfer happened once and cannot silently happen twice.
        public List<string> consumedSources = new List<string>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref regionalId, "regionalId");
            Scribe_Values.Look(ref regionName, "regionName");
            Scribe_Values.Look(ref regionNameAuthored,
                "regionNameAuthored", false);
            Scribe_Values.Look(ref mapSize, "mapSize", 0);
            Scribe_Values.Look(ref requestedRegionTileCount,
                "requestedRegionTileCount", 0);
            Scribe_Values.Look(ref regionTileCount, "regionTileCount", 0);
            Scribe_Values.Look(ref backingMapWidth, "backingMapWidth", 0);
            Scribe_Values.Look(ref backingMapHeight, "backingMapHeight", 0);
            Scribe_Values.Look(ref footprintRotation,
                "footprintRotation", 0);
            Scribe_Values.Look(ref bundleRootTileId,
                "bundleRootTileId", -1);
            Scribe_Values.Look(ref startTileId, "startTileId", -1);
            Scribe_Collections.Look(ref memberTileIds, "memberTileIds",
                LookMode.Value);
            Scribe_Collections.Look(ref footprintTileIds, "footprintTileIds",
                LookMode.Value);
            Scribe_Collections.Look(ref factions, "factions",
                LookMode.Deep);
            Scribe_Collections.Look(ref settlements, "settlements",
                LookMode.Deep);
            Scribe_Collections.Look(ref relations, "relations",
                LookMode.Deep);
            Scribe_Collections.Look(ref frontierHoldings,
                "frontierHoldings", LookMode.Deep);
            Scribe_Deep.Look(ref worldPolicy, "worldPolicy");
            Scribe_Deep.Look(ref groundwater, "groundwater");
            Scribe_Collections.Look(ref featureShapes, "featureShapes",
                LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit
                && featureShapes == null)
                featureShapes = new List<CAAuthoredFeatureShape>();
            Scribe_Deep.Look(ref playerFounding, "playerFounding");
            Scribe_Values.Look(ref operatorAuthored, "operatorAuthored", false);
            Scribe_Values.Look(ref candidateId, "candidateId");
            Scribe_Values.Look(ref confirmed, "confirmed", false);
            Scribe_Values.Look(ref developerExercise, "developerExercise",
                false);
            Scribe_Values.Look(ref creationSummary, "creationSummary");
            Scribe_Values.Look(ref settlementPattern, "settlementPattern",
                (byte)0);
            Scribe_Values.Look(ref settlementScale, "settlementScale",
                (byte)0);
            Scribe_Values.Look(ref regionalRelationPattern,
                "regionalRelationPattern", (byte)0);
            Scribe_Values.Look(ref settlementRealizationComplete,
                "settlementRealizationComplete", false);
            Scribe_Values.Look(ref settlementRealizationSourceHash,
                "settlementRealizationSourceHash", 0);
            Scribe_Collections.Look(ref consumedSources,
                "consumedSources", LookMode.Value);
        }

        internal PlanetTile BundleRoot
        {
            get { return CARegionalPlanUtility.SurfaceTile(bundleRootTileId); }
        }

        internal PlanetTile StartTile
        {
            get { return CARegionalPlanUtility.SurfaceTile(startTileId); }
        }

        internal int RegionTileCount
        {
            get
            {
                return regionTileCount > 0 ? regionTileCount
                    : Math.Max(1, memberTileIds?.Count ?? 0);
            }
        }

        internal int RequestedRegionTileCount
        {
            get
            {
                return requestedRegionTileCount > 0
                    ? requestedRegionTileCount : RegionTileCount;
            }
        }

        internal int FootprintRotation
        {
            get
            {
                int normalized = footprintRotation % 6;
                return normalized < 0 ? normalized + 6 : normalized;
            }
        }

        internal IntVec3 BackingMapSize
        {
            get
            {
                int width = backingMapWidth > 0 ? backingMapWidth : mapSize;
                int height = backingMapHeight > 0 ? backingMapHeight : mapSize;
                return new IntVec3(width, 1, height);
            }
        }

        internal IReadOnlyList<int> ReservedTileIds
        {
            get
            {
                return footprintTileIds != null && footprintTileIds.Count > 0
                    ? footprintTileIds : memberTileIds;
            }
        }

        internal CARegionalFactionPlan FactionPlan(int key)
        {
            return factions.FirstOrDefault(group => group != null
                && group.key == key);
        }

        internal FactionRelationKind RelationBetween(int left, int right)
        {
            if (left == right) return FactionRelationKind.Ally;
            CARegionalRelationPlan pair = RelationPlanBetween(left, right);
            return pair?.relation ?? FactionRelationKind.Neutral;
        }

        internal CARegionalRelationPlan RelationPlanBetween(int left,
            int right)
        {
            if (left == right) return null;
            return relations.FirstOrDefault(item =>
                item != null && ((item.leftFactionKey == left
                    && item.rightFactionKey == right)
                    || (item.leftFactionKey == right
                        && item.rightFactionKey == left)));
        }

        internal void SetRelation(int left, int right,
            FactionRelationKind relation, CARegionalRelationSource source)
        {
            if (left == right) return;
            CARegionalRelationPlan pair = RelationPlanBetween(left, right);
            if (pair == null)
            {
                pair = new CARegionalRelationPlan
                {
                    leftFactionKey = Math.Min(left, right),
                    rightFactionKey = Math.Max(left, right)
                };
                relations.Add(pair);
            }
            pair.relation = relation;
            pair.source = source;
        }

        internal bool ValidFor(CAExpandedLandmassProfile profile)
        {
            if (memberTileIds == null) return false;
            IntVec3 expectedBacking = CARegionalPlanUtility.BackingMapSize(
                profile, BundleRoot, memberTileIds.Select(
                    CARegionalPlanUtility.SurfaceTile)
                    .Where(tile => tile.Valid).ToList());
            if (mapSize != profile.Size
                || memberTileIds.Count != RegionTileCount
                || memberTileIds.Distinct().Count() != memberTileIds.Count
                || footprintTileIds == null
                || memberTileIds.Any(id => !footprintTileIds.Contains(id))
                || backingMapWidth != expectedBacking.x
                || backingMapHeight != expectedBacking.z
                || !memberTileIds.Contains(startTileId)
                || !memberTileIds.Contains(bundleRootTileId)) return false;
            if (!StartTile.Valid || !BundleRoot.Valid) return false;
            string identityFailure;
            if (!CARegionalPlanUtility.TryValidateStableIdentities(this,
                    out identityFailure)) return false;
            for (int i = 0; i < settlements.Count; i++)
            {
                CARegionalSettlementPlan settlement = settlements[i];
                if (!memberTileIds.Contains(settlement.memberTileId))
                    return false;
            }
            return true;
        }
    }

    internal static class CARegionalPlanUtility
    {
        internal static Settlement SettlementAtTile(int tileId)
        {
            if (tileId < 0) return null;
            List<Settlement> settlements =
                Verse.Find.WorldObjects?.Settlements;
            if (settlements == null) return null;
            for (int i = 0; i < settlements.Count; i++)
                if (settlements[i] != null
                    && settlements[i].Tile.tileId == tileId)
                    return settlements[i];
            return null;
        }

        // Runs when map generation commits the plan. Each reallocated source is
        // removed from the world and recorded so population is not duplicated.
        internal static void ConsumeReallocatedSources(CARegionalPlan plan)
        {
            if (plan?.settlements == null) return;
            if (plan.consumedSources == null)
                plan.consumedSources = new List<string>();
            foreach (CARegionalSettlementPlan place in plan.settlements)
            {
                if (place == null || place.populationOrigin
                    != CASettlementOrigin.ReallocatedFromWorldPool)
                    continue;
                string receiptPrefix = place.reallocatedFromTileId + "|";
                if (plan.consumedSources.Any(entry => entry != null
                        && entry.StartsWith(receiptPrefix)))
                    continue; // already absorbed (re-entrant commit)
                Settlement source = SettlementAtTile(
                    place.reallocatedFromTileId);
                if (source == null)
                {
                    Log.Warning("[CA][Regional] reallocation source at "
                        + "tile " + place.reallocatedFromTileId
                        + " vanished between validation and commit; "
                        + "the settlement keeps its origin but no receipt "
                        + "exists.");
                    continue;
                }
                string name = source.LabelCap;
                int factionLoadId = source.Faction?.loadID ?? -1;
                Verse.Find.WorldObjects.Remove(source);
                plan.consumedSources.Add(place.reallocatedFromTileId
                    + "|" + factionLoadId + "|" + name);
                Log.Message("[CA][Regional] reallocation consumed: "
                    + name + " (tile " + place.reallocatedFromTileId
                    + ") absorbed into the region - its authorization "
                    + "now stands as settlement slot " + place.slot);
            }
        }

        internal static PlanetTile SurfaceTile(int id)
        {
            PlanetLayer surface = Verse.Find.WorldGrid?.Surface;
            if (surface == null || id < 0 || id >= surface.TilesCount)
                return PlanetTile.Invalid;
            return new PlanetTile(id, surface);
        }

        internal static float ProjectionCellsPerDegree(PlanetTile root,
            int localTileCells)
        {
            if (!root.Valid) return Math.Max(1, localTileCells);
            Vector3 center = Verse.Find.WorldGrid.GetTileCenter(root);
            float radius = Math.Max(0.0001f, center.magnitude);
            // One world layer owns one canonical geographic scale. Per-tile
            // neighbor spacing varies slightly around pentagons and would make
            // the same terrain noise and river bend change when an adjacent
            // tile becomes the preview root.
            float chord = Math.Max(0.0001f, root.Layer.AverageTileSize);
            float spacingRadians = 2f * Mathf.Asin(Mathf.Clamp(
                chord / (radius * 2f), 0f, 0.9999f));
            float spacingDegrees = Math.Max(0.0001f,
                spacingRadians * Mathf.Rad2Deg);
            return Math.Max(1, localTileCells) / spacingDegrees;
        }

        internal static IntVec3 BackingMapSize(
            CAExpandedLandmassProfile profile, PlanetTile root,
            IReadOnlyList<PlanetTile> members)
        {
            IntVec3 nominal = profile.BackingMapSize(
                Math.Max(1, members?.Count ?? 0));
            if (!root.Valid || members == null || members.Count == 0)
                return nominal;
            Vector2 origin = Verse.Find.WorldGrid.LongLatOf(root);
            float cosine = Mathf.Cos(origin.y * Mathf.Deg2Rad);
            float scale = ProjectionCellsPerDegree(root, profile.Size);
            float minCenterX = float.MaxValue;
            float maxCenterX = float.MinValue;
            float minCenterY = float.MaxValue;
            float maxCenterY = float.MinValue;
            foreach (PlanetTile tile in members)
            {
                Vector2 coordinate = Verse.Find.WorldGrid.LongLatOf(tile);
                float x = Mathf.DeltaAngle(origin.x, coordinate.x) * cosine;
                float y = coordinate.y - origin.y;
                minCenterX = Mathf.Min(minCenterX, x);
                maxCenterX = Mathf.Max(maxCenterX, x);
                minCenterY = Mathf.Min(minCenterY, y);
                maxCenterY = Mathf.Max(maxCenterY, y);
            }
            float frameCenterX = (minCenterX + maxCenterX) * 0.5f;
            float frameCenterY = (minCenterY + maxCenterY) * 0.5f;
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            foreach (PlanetTile tile in members)
            {
                var vertices = new List<Vector3>();
                Verse.Find.WorldGrid.GetTileVertices(tile, vertices);
                foreach (Vector3 vertex in vertices)
                {
                    float longitude = Mathf.Atan2(vertex.x, -vertex.z)
                        * Mathf.Rad2Deg;
                    float latitude = Mathf.Asin(vertex.y
                        / Math.Max(0.0001f, vertex.magnitude))
                        * Mathf.Rad2Deg;
                    float x = Mathf.DeltaAngle(origin.x, longitude) * cosine;
                    float y = latitude - origin.y;
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            float margin = Mathf.Max(32f, profile.Size * 0.08f);
            float halfWidth = Mathf.Max(Mathf.Abs(minX - frameCenterX),
                Mathf.Abs(maxX - frameCenterX));
            float halfHeight = Mathf.Max(Mathf.Abs(minY - frameCenterY),
                Mathf.Abs(maxY - frameCenterY));
            int width = Math.Max(nominal.x, Mathf.CeilToInt(
                halfWidth * scale * 2f + margin * 2f));
            int height = Math.Max(nominal.z, Mathf.CeilToInt(
                halfHeight * scale * 2f + margin * 2f));
            return new IntVec3(width, 1, height);
        }

        // Deleted faction numbers return to the pool.
        internal static int LowestFreeFactionKey(CARegionalPlan plan)
        {
            int key = 1;
            while (plan.factions.Any(item => item != null
                && item.key == key)) key++;
            return key;
        }

        internal static int LowestFreeSlot(CARegionalPlan plan)
        {
            int slot = 0;
            while (plan.settlements.Any(item => item != null
                && item.slot == slot)) slot++;
            return slot;
        }

        internal static Faction FactionByLoadId(int loadId)
        {
            FactionManager manager = Verse.Find.World?.factionManager;
            if (loadId < 0 || manager == null) return null;
            return manager.AllFactionsListForReading
                .FirstOrDefault(faction => faction != null
                    && faction.loadID == loadId);
        }

        internal static FactionDef FactionDefByName(string defName)
        {
            return string.IsNullOrEmpty(defName) ? null
                : DefDatabase<FactionDef>.GetNamedSilentFail(defName);
        }

        internal static bool TryValidateStableIdentities(CARegionalPlan plan,
            out string failure)
        {
            return TryValidateStableIdentities(plan,
                CARegionalPlan.CurrentSchemaVersion, out failure);
        }

        internal static bool TryValidateStableIdentities(CARegionalPlan plan,
            int expectedSchemaVersion, out string failure)
        {
            failure = null;
            if (plan == null)
            {
                failure = "regional plan is unavailable";
                return false;
            }
            if (plan.schemaVersion != expectedSchemaVersion)
            {
                failure = "regional plan uses an unsupported development schema";
                return false;
            }
            if (plan.factions == null
                || plan.factions.Any(group => group == null
                    || group.key <= 0)
                || plan.factions.Select(group => group.key).Distinct()
                    .Count() != plan.factions.Count)
            {
                failure = "factions need distinct stable identities";
                return false;
            }
            if (plan.settlements == null)
            {
                failure = "regional settlement records are unavailable";
                return false;
            }
            var slots = new HashSet<int>();
            foreach (CARegionalSettlementPlan settlement in plan.settlements)
            {
                if (settlement == null || settlement.slot < 0
                    || !slots.Add(settlement.slot))
                {
                    failure = "regional settlements need distinct stable identities";
                    return false;
                }
                if (settlement.factionLinks == null)
                {
                    failure = "settlement " + (settlement.slot + 1)
                        + " has no affiliation record";
                    return false;
                }
                string linksFailure = settlement.factionLinks
                    .ValidationFailure(plan);
                if (!linksFailure.NullOrEmpty())
                {
                    failure = "settlement " + (settlement.slot + 1)
                        + " has invalid faction relationships: "
                        + linksFailure;
                    return false;
                }
            }
            return true;
        }

        private static bool TryValidatePopulationGroups(CARegionalPlan plan,
            CARegionalSettlementPlan settlement, out string failure)
        {
            failure = null;
            List<CASettlementPopulationGroup> populationGroups =
                settlement.populationGroups;
            if (populationGroups == null || populationGroups.Count == 0)
            {
                failure = "settlement " + (settlement.slot + 1)
                    + " needs a population composition";
                return false;
            }
            if (populationGroups.Any(item => item == null || item.key <= 0)
                || populationGroups.Select(item => item.key).Distinct().Count()
                    != populationGroups.Count)
            {
                failure = "settlement " + (settlement.slot + 1)
                    + " needs distinct population-group identities";
                return false;
            }
            if (populationGroups.Count(item => item.isPrimary) != 1
                || populationGroups.Any(item => item.share <= 0
                    || item.share > 100)
                || populationGroups.Sum(item => item.share) != 100)
            {
                failure = "settlement " + (settlement.slot + 1)
                    + " population groups need one primary group and shares "
                    + "totaling 100%";
                return false;
            }
            foreach (CASettlementPopulationGroup populationGroup in
                populationGroups)
            {
                if (populationGroup.kind != CAPopulationGroupKind.Unaffiliated
                    && plan.FactionPlan(populationGroup.factionKey) == null)
                {
                    failure = "settlement " + (settlement.slot + 1)
                        + " population group " + populationGroup.key
                        + " references a missing faction";
                    return false;
                }
                if (populationGroup.politicalBeliefsFactionKey >= 0
                    && plan.FactionPlan(
                        populationGroup.politicalBeliefsFactionKey) == null
                    || populationGroup.ideoligionFactionKey >= 0
                    && plan.FactionPlan(
                        populationGroup.ideoligionFactionKey) == null)
                {
                    failure = "settlement " + (settlement.slot + 1)
                        + " population group " + populationGroup.key
                        + " references missing beliefs or Ideoligion";
                    return false;
                }
            }
            foreach (CAProvisionArrangement provision in
                settlement.provisionArrangements
                    ?? new List<CAProvisionArrangement>())
                if (provision != null && provision.populationGroupKey >= 0
                    && !populationGroups.Any(item => item.key
                        == provision.populationGroupKey))
                {
                    failure = "settlement " + (settlement.slot + 1)
                        + " has provisions for a missing population group";
                    return false;
                }
            return true;
        }

        internal static List<Faction> EligibleExistingFactions()
        {
            if (Verse.Find.FactionManager == null) return new List<Faction>();
            return Verse.Find.FactionManager.AllFactionsVisible
                .Where(IsEligibleExistingFaction)
                .OrderBy(faction => faction.Name).ToList();
        }

        internal static List<FactionDef> EligibleNewFactionDefs()
        {
            return FactionGenerator.ConfigurableFactions
                .Where(def => def != null && !def.isPlayer
                    && CanMaterializeSettlement(def))
                .OrderBy(def => def.label)
                .ToList();
        }

        internal static bool CanMaterializeSettlement(FactionDef def)
        {
            return def != null && !def.isPlayer && def.humanlikeFaction
                && def.pawnGroupMakers != null
                && def.pawnGroupMakers.Any(maker => maker != null
                    && maker.kindDef == PawnGroupKindDefOf.Settlement);
        }

        internal static bool CanUseSettlementFaction(Faction faction,
            bool requireVisible)
        {
            return faction != null && !faction.IsPlayer && !faction.defeated
                && !faction.temporary && !faction.deactivated
                && (!requireVisible || !faction.Hidden)
                && CanMaterializeSettlement(faction.def);
        }

        internal static bool IsEligibleExistingFaction(Faction faction)
        {
            return CanUseSettlementFaction(faction, true);
        }

        internal static bool MatchesFaction(Faction faction,
            CARegionalFactionPlan group)
        {
            if (group == null) return false;
            bool existing = group.source
                == CARegionalFactionSource.ExistingWorldFaction;
            if (!CanUseSettlementFaction(faction, existing)) return false;
            return existing
                ? faction.loadID == group.existingFactionLoadId
                : !string.IsNullOrEmpty(group.customFactionDefName)
                    && faction.def.defName == group.customFactionDefName;
        }

        // One authored entry per native faction. This is shared by setup and
        // the durable world-state boundary so alternate entry paths cannot
        // commit sequential, conflicting drafts to the same faction.
        internal static bool TryValidateDistinctFactionClaims(
            CARegionalPlan plan, out string failure)
        {
            failure = null;
            if (plan?.factions == null) return true;
            var factionClaims = new HashSet<int>();
            foreach (CARegionalFactionPlan group in plan.factions)
            {
                if (group == null) continue;
                int factionId = group.source
                    == CARegionalFactionSource.ExistingWorldFaction
                        ? group.existingFactionLoadId
                        : group.resolvedFactionLoadId >= 0
                            ? group.resolvedFactionLoadId
                            : group.resolvedFaction?.loadID ?? -1;
                if (factionId < 0 || factionClaims.Add(factionId)) continue;
                Faction faction = FactionByLoadId(factionId);
                failure = (faction?.Name ?? "The same faction")
                    + " is selected more than once. Use one faction entry "
                    + "for all of its settlements.";
                return false;
            }
            return true;
        }

        internal static bool TryValidateStartingSettlements(CARegionalPlan plan,
            out string failure)
        {
            failure = null;
            if (plan == null)
            {
                failure = "The starting-region plan is unavailable.";
                return false;
            }

            string identityFailure;
            if (!TryValidateStableIdentities(plan, out identityFailure))
            {
                failure = identityFailure.CapitalizeFirst() + ".";
                return false;
            }

            if (!TryValidateDistinctFactionClaims(plan, out failure))
                return false;

            foreach (CARegionalFactionPlan group in plan.factions)
            {
                if (group == null) continue;
                string cultureFailure = CACultureModel.SubstantiveFailure(
                    group.culture);
                if (!cultureFailure.NullOrEmpty())
                {
                    failure = "Culture for " + FactionName(group)
                        + " cannot be used: " + cultureFailure;
                    return false;
                }
                string beliefFailure = CAPoliticalBeliefsModel
                    .ValidationFailure(group.politicalBeliefs,
                        allowExactLegacy: false);
                if (!beliefFailure.NullOrEmpty())
                {
                    failure = "Political Order for " + FactionName(group)
                        + " cannot be used: " + beliefFailure;
                    return false;
                }
                string technologyFailure = CATechnologicalKnowledgeModel
                    .ValidationFailure(group.technologicalKnowledge);
                if (!technologyFailure.NullOrEmpty())
                {
                    failure = "Technological Knowledge for "
                        + FactionName(group) + " cannot be used: "
                        + technologyFailure;
                    return false;
                }
                string orderFailure = CAPoliticalBeliefsModel
                    .ValidationFailure(group.factionStructure);
                if (!orderFailure.NullOrEmpty())
                {
                    failure = "Represented institutions for " + FactionName(group)
                        + " cannot be used: " + orderFailure;
                    return false;
                }
            }

            if (plan.playerFounding?.ArrangementChosen == true)
            {
                CAFoundingArrangement founding = plan.playerFounding.arrangement;
                if (founding == null)
                {
                    failure = "The authored founding terms are missing.";
                    return false;
                }
                if (founding.leaderRule != "none"
                    && founding.leaderRule != "chosen")
                {
                    failure = "The founding terms have an unknown "
                        + "leadership rule.";
                    return false;
                }
                if (founding.leaderRule == "none"
                    && !founding.foundersDecide)
                {
                    failure = "The founding terms need either a leader "
                        + "or shared decisions.";
                    return false;
                }
            }

            foreach (CARegionalSettlementPlan settlement in plan.settlements)
            {
                int displaySlot = settlement.slot + 1;
                CARegionalFactionPlan group = CASiteState.OwnerPlan(plan,
                    settlement.factionLinks);
                if (!settlement.HasFactionOwner)
                {
                    if (settlement.localCulture == null
                        || settlement.localSociety?.politicalOrder == null
                        || settlement.localSociety
                            .technologicalKnowledge == null
                        || settlement.localSociety.institutions == null)
                    {
                        failure = "Settlement " + displaySlot
                            + " has no owning faction and needs its own "
                            + "Culture, Political Order, Technological "
                            + "Knowledge, and institutions.";
                        return false;
                    }
                    string localCultureFailure = CACultureModel
                        .ValidationFailure(settlement.localCulture,
                            requireSubstantive: true);
                    if (!localCultureFailure.NullOrEmpty())
                    {
                        failure = "Settlement " + displaySlot
                            + " has invalid local Culture: "
                            + localCultureFailure;
                        return false;
                    }
                    string localPoliticalFailure = CAPoliticalBeliefsModel
                        .ValidationFailure(
                            settlement.localSociety.politicalOrder,
                            allowExactLegacy: false);
                    if (!localPoliticalFailure.NullOrEmpty())
                    {
                        failure = "Settlement " + displaySlot
                            + " has invalid local Political Order: "
                            + localPoliticalFailure;
                        return false;
                    }
                    string localTechnologyFailure =
                        CATechnologicalKnowledgeModel.ValidationFailure(
                            settlement.localSociety
                                .technologicalKnowledge);
                    if (!localTechnologyFailure.NullOrEmpty())
                    {
                        failure = "Settlement " + displaySlot
                            + " has invalid local Technological Knowledge: "
                            + localTechnologyFailure;
                        return false;
                    }
                    string localInstitutionFailure = CAPoliticalBeliefsModel
                        .ValidationFailure(
                            settlement.localSociety.institutions);
                    if (!localInstitutionFailure.NullOrEmpty())
                    {
                        failure = "Settlement " + displaySlot
                            + " has invalid local institutions: "
                            + localInstitutionFailure;
                        return false;
                    }
                }
                if (!TryValidatePopulationGroups(plan, settlement,
                        out failure))
                    return false;
                if (!CASettlementComposition.TryValidateProvisionArrangements(
                        plan, settlement, out string provisionFailure))
                {
                    failure = "Settlement " + displaySlot
                        + " has invalid provision arrangements: "
                        + provisionFailure + ".";
                    return false;
                }
                if (!CASettlementProgramRegistry.TryValidateSaved(plan,
                        settlement, out string programFailure))
                {
                    failure = "Settlement " + displaySlot
                        + " has an invalid settlement composition: "
                        + programFailure + ".";
                    return false;
                }
                if (!plan.memberTileIds.Contains(settlement.memberTileId))
                {
                    failure = "Settlement " + displaySlot
                        + " needs a source tile inside this region.";
                    return false;
                }
                // A settlement keeps its authored location. A landing tile
                // already occupied by that settlement is rejected.
                if (settlement.memberTileId == plan.startTileId)
                {
                    failure = "Your arrival tile is already occupied by "
                        + (settlement.customName.NullOrEmpty()
                            ? "settlement " + displaySlot
                            : settlement.customName)
                        + ". The arrival is invalid - choose a different "
                        + "landing tile. The settlement stays where it is.";
                    return false;
                }

                // Every major settlement needs a population source.
                if (settlement.populationOrigin == CASettlementOrigin.Unset)
                {
                    failure = "Settlement " + displaySlot + " has not said "
                        + "where its population comes from. A major "
                        + "settlement either reallocates an authorized one "
                        + "from the world pool or is an explicit scenario "
                        + "override; region size does not authorize it.";
                    return false;
                }
                // Validate the named source now. Map generation removes it and
                // records the transfer when the plan is committed.
                if (settlement.populationOrigin
                    == CASettlementOrigin.ReallocatedFromWorldPool)
                {
                    if (settlement.reallocatedFromTileId < 0)
                    {
                        failure = "Settlement " + displaySlot
                            + " is marked reallocated but names no "
                            + "source settlement to concentrate.";
                        return false;
                    }
                    Settlement source = SettlementAtTile(
                        settlement.reallocatedFromTileId);
                    if (source == null)
                    {
                        failure = "Settlement " + displaySlot
                            + " would concentrate a settlement that no "
                            + "longer stands. Choose another source.";
                        return false;
                    }
                    if (source.Faction != null
                        && source.Faction.IsPlayer)
                    {
                        failure = "Settlement " + displaySlot
                            + " cannot concentrate a player settlement.";
                        return false;
                    }
                    foreach (CARegionalSettlementPlan other in plan.settlements)
                        if (other != null && other != settlement
                            && other.populationOrigin == CASettlementOrigin
                                .ReallocatedFromWorldPool
                            && other.reallocatedFromTileId
                                == settlement.reallocatedFromTileId)
                        {
                            failure = "Two settlements both reallocate "
                                + source.LabelCap
                                + ". A world settlement can be reallocated "
                                + "only once.";
                            return false;
                        }
                }
                if (group != null && group.source
                    == CARegionalFactionSource.ExistingWorldFaction)
                {
                    Faction faction = FactionByLoadId(
                        group.existingFactionLoadId);
                    if (!IsEligibleExistingFaction(faction))
                    {
                        failure = "Faction " + group.key
                            + " needs an existing faction capable of "
                            + "generating a settlement.";
                        return false;
                    }
                }
                else if (group != null)
                {
                    FactionDef def = FactionDefByName(
                        group.customFactionDefName);
                    if (!CanMaterializeSettlement(def))
                    {
                        failure = "Faction " + group.key
                            + " needs a settlement-capable faction template.";
                        return false;
                    }
                }
                else
                {
                    FactionDef template = FactionDefByName(
                        settlement.generationFactionDefName);
                    if (!CanMaterializeSettlement(template))
                    {
                        failure = "Settlement " + displaySlot
                            + " has no owning faction and needs a "
                            + "settlement-capable generation template.";
                        return false;
                    }
                }
            }

            // Explicit factions without settlements are still validated and
            // materialized as world factions.
            foreach (CARegionalFactionPlan group in plan.factions)
            {
                if (group == null) continue;
                if (plan.settlements.Any(item => item != null
                        && item.OwningFactionKey == group.key)) continue;
                string name = group.customName.NullOrEmpty()
                    ? "Faction " + group.key : group.customName;
                if (group.source
                    == CARegionalFactionSource.ExistingWorldFaction)
                {
                    if (!IsEligibleExistingFaction(FactionByLoadId(
                            group.existingFactionLoadId)))
                    {
                        failure = name + " holds no settlement and names "
                            + "no real existing faction. Choose one, or "
                            + "remove the faction.";
                        return false;
                    }
                }
                else if (!CanMaterializeSettlement(FactionDefByName(
                             group.customFactionDefName)))
                {
                    failure = name + " holds no settlement and its engine "
                        + "template cannot generate. Choose a real "
                        + "template, or remove the faction.";
                    return false;
                }
            }
            return true;
        }

        internal static string TileSummary(int id)
        {
            PlanetTile tile = SurfaceTile(id);
            if (!tile.Valid) return "invalid tile";
            Tile info = tile.Tile;
            return "tile " + id + " · "
                + (info.PrimaryBiome?.LabelCap.ToString() ?? "unknown biome")
                + " · " + info.hilliness;
        }

        // Road and river access come from the settlement's world tile. The
        // setup screen and generation use these same checks.
        internal static bool ConstituentHasRoad(int tileId)
        {
            PlanetTile tile = SurfaceTile(tileId);
            SurfaceTile surface = tile.Valid ? tile.Tile as SurfaceTile
                : null;
            return surface?.Roads != null && surface.Roads.Count > 0;
        }

        internal static bool ConstituentHasRiver(int tileId)
        {
            PlanetTile tile = SurfaceTile(tileId);
            SurfaceTile surface = tile.Valid ? tile.Tile as SurfaceTile
                : null;
            return surface?.Rivers != null && surface.Rivers.Count > 0;
        }

        internal static bool ConstituentsShareRoute(int leftTileId,
            int rightTileId)
        {
            PlanetTile left = SurfaceTile(leftTileId);
            SurfaceTile surface = left.Valid ? left.Tile as SurfaceTile
                : null;
            if (surface == null) return false;
            bool road = surface.Roads != null && surface.Roads.Any(link =>
                link.neighbor.Valid && link.neighbor.tileId == rightTileId);
            bool river = surface.Rivers != null && surface.Rivers.Any(link =>
                link.neighbor.Valid && link.neighbor.tileId == rightTileId);
            return road || river;
        }

        internal static bool ConstituentIsCoastal(int tileId)
        {
            PlanetTile tile = SurfaceTile(tileId);
            if (!tile.Valid) return false;
            if (tile.Tile?.PrimaryBiome?.isWaterBiome == true) return true;
            var neighbors = new List<PlanetTile>();
            try { tile.Layer.GetTileNeighbors(tile, neighbors); }
            catch { return false; }
            foreach (PlanetTile neighbor in neighbors)
                if (neighbor.Valid
                    && neighbor.Tile?.PrimaryBiome?.isWaterBiome == true)
                    return true;
            return false;
        }

        // A constituent is its land, not its number. Shared by the map
        // canvas and the population screen so a settlement reads the same
        // wherever it is named.
        internal static string TileWords(int tileId)
        {
            PlanetTile tile = SurfaceTile(tileId);
            if (!tile.Valid || tile.Tile == null) return "unknown ground";
            string biome = tile.Tile.PrimaryBiome?.label ?? "unknown";
            string hill = tile.Tile.hilliness.ToString().ToLower()
                .Replace("largehills", "hilly")
                .Replace("smallhills", "gentle hills")
                .Replace("mountainous", "mountains");
            string words = biome.NullOrEmpty() ? "unknown ground"
                : char.ToUpper(biome[0]) + biome.Substring(1);
            if (hill != "flat" && hill != "undefined")
                words += ", " + hill;
            return words;
        }

        internal static string RegionName(CARegionalPlan plan)
        {
            if (plan == null) return "Region";
            if (!plan.regionName.NullOrEmpty()) return plan.regionName;
            return DerivedRegionName(plan);
        }

        internal static string DerivedRegionName(CARegionalPlan plan)
        {
            if (plan == null) return "Region";
            PlanetTile root = plan.BundleRoot;
            string feature = root.Valid ? root.Tile?.feature?.name : null;
            if (!feature.NullOrEmpty()) return feature + " region";
            string biome = root.Valid ? root.Tile?.PrimaryBiome?.label : null;
            return biome.NullOrEmpty() ? "Starting region"
                : biome.CapitalizeFirst() + " region";
        }

        // The name a settlement will carry: an
        // authored name if there is one, otherwise a placeholder that says
        // plainly that its owner's own name-maker supplies it on arrival.
        internal static string SettlementName(CARegionalPlan plan,
            CARegionalSettlementPlan place)
        {
            if (place == null) return "";
            if (!place.customName.NullOrEmpty()) return place.customName;
            return "Settlement " + (place.slot + 1) + " (named on arrival)";
        }

        // The faction that owns a settlement. Research, technology, relations,
        // Political Order and represented institutions are resolved on this object.
        internal static string FactionName(CARegionalFactionPlan group)
        {
            if (group == null) return "no owner";
            if (group.source
                == CARegionalFactionSource.ExistingWorldFaction)
            {
                Faction faction = FactionByLoadId(
                    group.existingFactionLoadId);
                return IsEligibleExistingFaction(faction) ? faction.Name
                    : "Choose an existing faction";
            }
            if (!group.customName.NullOrEmpty()) return group.customName;
            FactionDef def = FactionDefByName(group.customFactionDefName);
            return CanMaterializeSettlement(def)
                ? "New " + def.LabelCap : "Choose a faction type";
        }

        internal static string SelectionTopologySummary(CARegionalPlan plan)
        {
            if (plan == null) return "no plan";
            string sources = string.Join(" | ", plan.memberTileIds.Select(id =>
            {
                PlanetTile tile = SurfaceTile(id);
                SurfaceTile surface = tile.Valid
                    ? tile.Tile as SurfaceTile : null;
                string roads = surface?.Roads.NullOrEmpty() != false ? "-"
                    : surface.Roads.Select(link => link.road.defName)
                        .Distinct().ToCommaList();
                string rivers = surface?.Rivers.NullOrEmpty() != false ? "-"
                    : surface.Rivers.Select(link => link.river.defName)
                        .Distinct().ToCommaList();
                string mutators = tile.Valid
                        && tile.Tile.Mutators.NullOrEmpty() == false
                    ? tile.Tile.Mutators.Select(mutator => mutator.defName)
                        .Distinct().ToCommaList() : "-";
                string rocks = tile.Valid ? Verse.Find.World
                    .NaturalRockTypesIn(tile).Select(rock => rock.defName)
                    .Distinct().OrderBy(name => name).ToCommaList() : "-";
                return tile.Valid ? id + ":"
                    + (tile.Tile.PrimaryBiome?.defName ?? "unknown") + ":"
                    + tile.Tile.hilliness + ":roads=" + roads
                    + ":rivers=" + rivers + ":mutators=" + mutators
                    + ":rocks=" + rocks
                    : id + ":invalid";
            }));

            var reserved = new HashSet<int>(plan.ReservedTileIds);
            var blockedPerimeter = new Dictionary<int, PlanetTile>();
            foreach (int id in reserved)
            {
                PlanetTile tile = SurfaceTile(id);
                if (!tile.Valid) continue;
                var neighbors = new List<PlanetTile>();
                tile.Layer.GetTileNeighbors(tile, neighbors);
                foreach (PlanetTile neighbor in neighbors)
                {
                    if (!neighbor.Valid || reserved.Contains(neighbor.tileId)
                        || !CARegionalGeometry.IsBlocked(neighbor)) continue;
                    blockedPerimeter[neighbor.tileId] = neighbor;
                }
            }
            string blocked = blockedPerimeter.Count == 0 ? "none"
                : string.Join(",", blockedPerimeter.Values
                    .OrderBy(tile => tile.tileId).Select(tile => tile.tileId
                        + ":" + (tile.Tile.PrimaryBiome?.defName
                            ?? "unknown") + ":" + tile.Tile.hilliness));
            CARegionalEnvelopeGeometry envelope = CARegionalGeometry
                .BuildEnvelope(plan, true);
            return "orientation " + (plan.FootprintRotation + 1)
                + "/6; sources [" + sources + "]; footprint ["
                + string.Join(",", reserved.OrderBy(id => id))
                + "]; blocked perimeter [" + blocked
                + "]; envelope loops " + envelope.loops.Count
                + "; vertices [" + string.Join(",", envelope.loops.Select(
                    loop => loop.Count)) + "]; internal seams "
                + envelope.internalEdges.Count + "; union "
                + (envelope.usedConvexFallback ? "convex fallback"
                    : "exact shared-edge boundary");
        }

        // Rerolling creates a new candidate identity. Preview and generation
        // retain that saved identity and derive feature seeds from it.
        internal static string MintCandidateId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        internal static CARegionalPlan Create(
            CAExpandedLandmassProfile profile, PlanetTile root,
            bool operatorAuthored, int regionTileCount = 0,
            int footprintRotation = 0)
        {
            int count = regionTileCount > 0 ? regionTileCount
                : profile.DefaultRegionTileCount;
            int normalizedRotation = ((footprintRotation % 6) + 6) % 6;
            List<PlanetTile> members = CARegionalBundleBuilder.Build(root,
                count, normalizedRotation);
            IntVec3 backing = BackingMapSize(profile, root, members);
            int seed = Gen.HashCombineInt(Gen.HashCombineInt(
                    Verse.Find.World.info.Seed, root.tileId, profile.Size,
                    count), normalizedRotation);
            int automaticCandidateSeed = Gen.HashCombineInt(seed,
                members.Count, backing.x, backing.z);
            var plan = new CARegionalPlan
            {
                mapSize = profile.Size,
                requestedRegionTileCount = count,
                regionTileCount = members.Count,
                backingMapWidth = backing.x,
                backingMapHeight = backing.z,
                footprintRotation = normalizedRotation,
                bundleRootTileId = root.tileId,
                startTileId = root.tileId,
                candidateId = operatorAuthored ? MintCandidateId()
                    : "auto" + unchecked((uint)automaticCandidateSeed)
                        .ToString("X8"),
                memberTileIds = members.Select(tile => tile.tileId).ToList(),
                operatorAuthored = operatorAuthored,
                worldPolicy = operatorAuthored
                    ? CAWorldTendenciesSession.Policy.Copy() : null,
                creationSummary = operatorAuthored
                    ? "operator-selected connected world-tile bundle"
                    : "campaign-scale connected bundle derived at first map visit"
            };
            plan.regionalId = "CA-RG-"
                + unchecked((uint)seed).ToString("X8");
            TryAdoptPartitionIdentity(plan);
            plan.regionName = RegionName(plan);
            plan.footprintTileIds = CARegionalGeometry.ClaimedTiles(plan);
            return plan;
        }

        // ONE LAND, ONE IDENTITY. A composition that matches a persistent
        // partition region's member set exactly IS that region: the same
        // land keeps the same identity whether the player arrived by
        // selection, by extent and orientation, or tile by tile. The
        // partition is geographic evidence, never a boundary the selection
        // must reproduce; exact coincidence is the only path by which a
        // candidate inherits a partition identity.
        internal static void TryAdoptPartitionIdentity(CARegionalPlan plan)
        {
            if (plan?.memberTileIds == null || plan.memberTileIds.Count == 0)
                return;
            CARegionalTopologyRecord match = CARegionalWorldComponent
                .Current?.TopologyRecordAt(plan.bundleRootTileId);
            if (match?.memberTileIds != null
                && match.memberTileIds.Count == plan.memberTileIds.Count
                && new HashSet<int>(match.memberTileIds).SetEquals(
                    plan.memberTileIds))
                plan.regionalId = match.regionId;
        }

        // SELECTION IS AUTHORING. New ground selects by the established
        // extent and orientation rules over connected usable land, with
        // geographic barrier evidence biasing candidate growth - the same
        // shared measure the world stitching is built from. The partition
        // is not the selection, and configuration (extent, orientation)
        // shapes the result. Ground the connected-land builder cannot
        // claim at all (for example land already inside a realized
        // region) keeps the partition record as the only remaining
        // candidate source, preserving prior coverage there.
        internal static CARegionalPlan CreateAuthoredAt(
            CAExpandedLandmassProfile profile, PlanetTile clicked,
            int fallbackCount, int fallbackRotation)
        {
            CARegionalPlan plan = Create(profile, clicked, true,
                fallbackCount, fallbackRotation);
            if (plan.memberTileIds != null && plan.memberTileIds.Count > 0)
                return plan;
            CARegionalTopologyRecord record = CARegionalWorldComponent
                .Current?.TopologyRecordAt(clicked);
            if (record == null) return plan;
            plan = CreateExplicit(profile, SurfaceTile(record.rootTileId),
                record.memberTileIds.ToList(), 0);
            plan.regionalId = record.regionId;
            plan.startTileId = record.memberTileIds.Contains(clicked.tileId)
                ? clicked.tileId : record.rootTileId;
            plan.creationSummary = "selected from the world's persistent "
                + "regional partition after connected land could not be"
                + " claimed";
            return plan;
        }

        // A TOPOLOGY REGION MATERIALIZES AS ITSELF. The plan realizes the
        // persistent partition record: its identity IS the record's, its
        // members ARE the record's, and its realization seed derives from
        // the record's root -- so the same region materializes identically
        // whichever member tile the engine entered through. The entry tile
        // is retained as arrival/anchor context only.
        internal static CARegionalPlan CreateFromTopology(
            CAExpandedLandmassProfile profile,
            CARegionalTopologyRecord record, PlanetTile entryTile)
        {
            List<PlanetTile> members = record.memberTileIds
                .Select(SurfaceTile).Where(tile => tile.Valid).ToList();
            PlanetTile root = SurfaceTile(record.rootTileId);
            IntVec3 backing = BackingMapSize(profile, root, members);
            int seed = Gen.HashCombineInt(Gen.HashCombineInt(
                Verse.Find.World.info.Seed, record.rootTileId,
                profile.Size, members.Count), 0);
            var plan = new CARegionalPlan
            {
                mapSize = profile.Size,
                requestedRegionTileCount = members.Count,
                regionTileCount = members.Count,
                backingMapWidth = backing.x,
                backingMapHeight = backing.z,
                footprintRotation = 0,
                bundleRootTileId = record.rootTileId,
                startTileId = entryTile.Valid
                    && record.memberTileIds.Contains(entryTile.tileId)
                    ? entryTile.tileId : record.rootTileId,
                candidateId = "topo" + unchecked((uint)Gen.HashCombineInt(
                    seed, members.Count, backing.x, backing.z))
                    .ToString("X8"),
                memberTileIds = record.memberTileIds.ToList(),
                operatorAuthored = false,
                worldPolicy = null,
                creationSummary = "materialized from the world's "
                    + "persistent regional partition"
            };
            plan.regionalId = record.regionId;
            plan.regionName = RegionName(plan);
            plan.footprintTileIds = CARegionalGeometry.ClaimedTiles(plan);
            return plan;
        }

        // AN AUTHORED MEMBER SET IS CANONICAL GEOMETRY. Where Create derives
        // its members from the bundle builder's stock shapes, this builds a
        // plan around exactly the connected areas the operator composed --
        // the presets remain conveniences over the same representation. The
        // regional identity hashes the member set itself, so the same
        // composition is the same region.
        internal static CARegionalPlan CreateExplicit(
            CAExpandedLandmassProfile profile, PlanetTile root,
            List<int> memberIds, int footprintRotation)
        {
            int normalizedRotation = ((footprintRotation % 6) + 6) % 6;
            List<PlanetTile> members = memberIds
                .Select(CARegionalPlanUtility.SurfaceTile)
                .Where(tile => tile.Valid).ToList();
            IntVec3 backing = BackingMapSize(profile, root, members);
            ulong fold = 14695981039346656037UL;
            void step(int item)
            {
                unchecked
                {
                    fold ^= (uint)item;
                    fold *= 1099511628211UL;
                }
            }
            step(Verse.Find.World.info.Seed);
            step(profile.Size);
            foreach (int id in memberIds.OrderBy(id => id)) step(id);
            int seed = unchecked((int)fold);
            var plan = new CARegionalPlan
            {
                mapSize = profile.Size,
                requestedRegionTileCount = members.Count,
                regionTileCount = members.Count,
                backingMapWidth = backing.x,
                backingMapHeight = backing.z,
                footprintRotation = normalizedRotation,
                bundleRootTileId = root.tileId,
                startTileId = root.tileId,
                candidateId = MintCandidateId(),
                memberTileIds = members.Select(tile => tile.tileId).ToList(),
                operatorAuthored = true,
                worldPolicy = CAWorldTendenciesSession.Policy.Copy(),
                creationSummary =
                    "operator-composed connected world-tile set"
            };
            plan.regionalId = "CA-RG-"
                + unchecked((uint)seed).ToString("X8");
            // One land, one identity: a composition matching a partition
            // region exactly inherits that region identity.
            TryAdoptPartitionIdentity(plan);
            plan.regionName = RegionName(plan);
            plan.footprintTileIds = CARegionalGeometry.ClaimedTiles(plan);
            return plan;
        }

        internal static void EnsureRelationRows(CARegionalPlan plan)
        {
            if (plan == null) return;
            List<int> keys = plan.factions.Where(group => group != null)
                .Select(group => group.key).Distinct().OrderBy(key => key)
                .ToList();
            plan.relations.RemoveAll(pair => pair == null
                || !keys.Contains(pair.leftFactionKey)
                || !keys.Contains(pair.rightFactionKey)
                || pair.leftFactionKey == pair.rightFactionKey);
            for (int i = 0; i < keys.Count; i++)
                for (int j = i + 1; j < keys.Count; j++)
                {
                    int left = keys[i];
                    int right = keys[j];
                    CARegionalRelationPlan pair = plan.relations.FirstOrDefault(
                        item => item != null && item.leftFactionKey == left
                            && item.rightFactionKey == right);
                    if (pair == null)
                    {
                        pair = new CARegionalRelationPlan
                        {
                            leftFactionKey = left,
                            rightFactionKey = right
                        };
                        plan.relations.Add(pair);
                    }
                    if (!plan.confirmed
                        && pair.source != CARegionalRelationSource.Authored)
                    {
                        if (TryDetermineNativeRelation(plan.FactionPlan(left),
                                plan.FactionPlan(right), out FactionRelationKind
                                relation, out CARegionalRelationSource source,
                                out _))
                        {
                            pair.relation = relation;
                            pair.source = source;
                        }
                        else
                        {
                            pair.relation = FactionRelationKind.Neutral;
                            pair.source = CARegionalRelationSource.Unset;
                        }
                    }
                }
        }

        internal static bool TryDetermineNativeRelation(
            CARegionalFactionPlan left, CARegionalFactionPlan right,
            out FactionRelationKind relation,
            out CARegionalRelationSource source, out string failure)
        {
            relation = FactionRelationKind.Neutral;
            source = CARegionalRelationSource.Unset;
            failure = null;
            if (left == null || right == null)
            {
                failure = "a faction is missing";
                return false;
            }
            if (left.source == CARegionalFactionSource.ExistingWorldFaction
                && right.source
                    == CARegionalFactionSource.ExistingWorldFaction)
            {
                Faction leftFaction = FactionByLoadId(
                    left.existingFactionLoadId);
                Faction rightFaction = FactionByLoadId(
                    right.existingFactionLoadId);
                if (!IsEligibleExistingFaction(leftFaction)
                    || !IsEligibleExistingFaction(rightFaction)
                    || leftFaction == rightFaction)
                {
                    failure = "the existing faction relation is unavailable";
                    return false;
                }
                relation = leftFaction.RelationKindWith(rightFaction);
                source = CARegionalRelationSource.NativeExisting;
                return true;
            }

            FactionDef leftDef = left.ResolvedFactionDef;
            FactionDef rightDef = right.ResolvedFactionDef;
            if (leftDef == null || rightDef == null)
            {
                failure = "the selected faction types do not define a native "
                    + "initial relation";
                return false;
            }
            int goodwill = Math.Min(InitialGoodwill(leftDef, rightDef),
                InitialGoodwill(rightDef, leftDef));
            relation = goodwill <= -10 ? FactionRelationKind.Hostile
                : goodwill >= 75 ? FactionRelationKind.Ally
                : FactionRelationKind.Neutral;
            source = CARegionalRelationSource.NativeInitial;
            return true;
        }

        internal static bool TryValidateRelationSource(CARegionalPlan plan,
            CARegionalRelationPlan relation, out string failure)
        {
            failure = null;
            if (relation == null)
            {
                failure = "relation row is missing";
                return false;
            }
            CARegionalFactionPlan left = plan?.FactionPlan(
                relation.leftFactionKey);
            CARegionalFactionPlan right = plan?.FactionPlan(
                relation.rightFactionKey);
            if (left == null || right == null)
            {
                failure = "relation owner is missing";
                return false;
            }
            if (relation.source == CARegionalRelationSource.Authored)
                return true;
            if (relation.source == CARegionalRelationSource.NativeExisting)
            {
                if (left.source == CARegionalFactionSource.ExistingWorldFaction
                    && right.source
                        == CARegionalFactionSource.ExistingWorldFaction)
                    return true;
                failure = "existing-world provenance was assigned to a new "
                    + "faction";
                return false;
            }
            if (relation.source == CARegionalRelationSource.NativeInitial)
            {
                if (left.source == CARegionalFactionSource.ExistingWorldFaction
                    && right.source
                        == CARegionalFactionSource.ExistingWorldFaction)
                {
                    failure = "native-initial provenance was assigned to two "
                        + "existing factions";
                    return false;
                }
                if (!TryDetermineNativeRelation(left, right,
                        out FactionRelationKind expected,
                        out CARegionalRelationSource expectedSource,
                        out failure))
                    return false;
                if (expectedSource != CARegionalRelationSource.NativeInitial
                    || expected != relation.relation)
                {
                    failure = "saved native-initial relation does not match "
                        + "the selected faction types";
                    return false;
                }
                return true;
            }
            failure = "no represented relation source exists";
            return false;
        }

        private static int InitialGoodwill(FactionDef faction,
            FactionDef other)
        {
            if (faction.permanentEnemy) return -100;
            if (faction.permanentEnemyToEveryoneExceptPlayer
                && !other.isPlayer) return -100;
            if (faction.permanentEnemyToEveryoneExcept != null
                && !faction.permanentEnemyToEveryoneExcept.Contains(other))
                return -100;
            return faction.naturalEnemy ? -80 : 0;
        }
    }

    internal static class CARegionalBundleBuilder
    {
        internal static List<PlanetTile> Build(PlanetTile root, int count,
            int rotation = 0)
        {
            var result = new List<PlanetTile>();
            if (!root.Valid || count <= 0) return result;
            int sequenceLength = Math.Max(8, count);
            CARegionalPlan existing = CARegionalWorldComponent.Current
                ?.FindRegionContaining(root);
            if (existing != null) return result;
            result.Add(root);
            var chosen = new HashSet<int> { root.tileId };
            var frontier = new Dictionary<int, PlanetTile>();
            int normalizedRotation = ((rotation % 6) + 6) % 6;
            float desiredHeading = normalizedRotation * 60f;
            AddFrontier(root);
            while (result.Count < sequenceLength && frontier.Count > 0)
            {
                IOrderedEnumerable<PlanetTile> ordered = frontier.Values
                    .OrderByDescending(tile => SelectedNeighborCount(tile,
                        chosen))
                    // Geographic barrier evidence: among equally cohesive
                    // candidates, prefer the attachment crossing the
                    // weakest geographic barrier - the same shared measure
                    // the world partition is built from. Evidence, never
                    // a mandate: cohesion still orders first.
                    .ThenBy(tile => AttachmentBarrier(tile, chosen));
                // Orientation zero is the pre-rotation canonical ordering, so
                // adding the control does not lose an already useful specimen.
                // Other orientations bias the same nested growth sequence
                // toward one of six headings while cohesion remains primary.
                ordered = normalizedRotation == 0
                    ? ordered.ThenBy(tile => CompactSpan(root, result, tile))
                        .ThenBy(tile => Verse.Find.WorldGrid.GetHeadingFromTo(
                            root, tile))
                    : ordered.ThenBy(tile => HeadingDistance(root, tile,
                            desiredHeading))
                        .ThenBy(tile => CompactSpan(root, result, tile));
                PlanetTile next = ordered.ThenBy(tile => tile.tileId).First();
                frontier.Remove(next.tileId);
                if (!chosen.Add(next.tileId)) continue;
                result.Add(next);
                AddFrontier(next);
            }
            return result.Take(count).ToList();

            void AddFrontier(PlanetTile from)
            {
                var neighbors = new List<PlanetTile>();
                from.Layer.GetTileNeighbors(from, neighbors);
                foreach (PlanetTile tile in neighbors)
                {
                    if (chosen.Contains(tile.tileId)
                        || frontier.ContainsKey(tile.tileId)
                        || !IsAvailable(tile)) continue;
                    frontier[tile.tileId] = tile;
                }
            }
        }

        // The cheapest edge by which a frontier candidate could attach
        // to the already-chosen set, by shared geographic barrier cost.
        private static float AttachmentBarrier(PlanetTile candidate,
            HashSet<int> chosen)
        {
            var neighbors = new List<PlanetTile>();
            candidate.Layer.GetTileNeighbors(candidate, neighbors);
            float cost = 1f;
            foreach (PlanetTile neighbor in neighbors)
                if (chosen.Contains(neighbor.tileId))
                    cost = Math.Min(cost,
                        CARegionalGeometry.GeographicBarrierCost(
                            neighbor.tileId, candidate.tileId));
            return cost;
        }

        private static float HeadingDistance(PlanetTile root,
            PlanetTile candidate, float desiredHeading)
        {
            float heading = Verse.Find.WorldGrid.GetHeadingFromTo(root,
                candidate);
            return Mathf.Abs(Mathf.DeltaAngle(desiredHeading, heading));
        }

        private static int SelectedNeighborCount(PlanetTile tile,
            HashSet<int> selected)
        {
            var neighbors = new List<PlanetTile>();
            tile.Layer.GetTileNeighbors(tile, neighbors);
            return neighbors.Count(neighbor => selected.Contains(
                neighbor.tileId));
        }

        private static float CompactSpan(PlanetTile root,
            List<PlanetTile> selected, PlanetTile candidate)
        {
            Vector3 origin = Verse.Find.WorldGrid.GetTileCenter(root)
                .normalized;
            Vector3 axisX = Vector3.Cross(Vector3.up, origin);
            if (axisX.sqrMagnitude < 0.0001f)
                axisX = Vector3.Cross(Vector3.right, origin);
            axisX.Normalize();
            Vector3 axisY = Vector3.Cross(origin, axisX).normalized;
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            foreach (PlanetTile tile in selected.Concat(new[] { candidate }))
            {
                Vector3 center = Verse.Find.WorldGrid.GetTileCenter(tile);
                float x = Vector3.Dot(center, axisX);
                float y = Vector3.Dot(center, axisY);
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
            float width = maxX - minX;
            float height = maxY - minY;
            return Mathf.Max(width, height) * 1000f + width * height;
        }

        private static bool IsAvailable(PlanetTile tile)
        {
            if (!tile.Valid || tile.Layer != Verse.Find.WorldGrid.Surface)
                return false;
            if (CARegionalGeometry.IsBlocked(tile)) return false;
            if (CARegionalWorldComponent.Current?.FindRegionContaining(tile)
                    != null) return false;
            return Verse.Find.WorldObjects == null
                || !Verse.Find.WorldObjects.AnyWorldObjectAt(tile);
        }
    }

    internal static class CARegionalSetupSession
    {
        internal static CARegionalPlan Pending;

        // The plan governing the current game for consumers that run after
        // generation. The founding arrangement is applied at StartedNewGame.
        // The committed plan lives on the world
        // component by then; Pending only bridges the gap while the
        // start is still being configured.
        internal static CARegionalPlan CurrentPlanOrNull()
        {
            try
            {
                Map map = Verse.Find.CurrentMap;
                CARegionalPlan committed = map != null
                    ? CARegionalWorldComponent.Current
                        ?.FindRegionForMap(map)
                    : null;
                if (committed != null) return committed;
                return PendingForCurrentWorld;
            }
            catch { return null; }
        }
        private static CARegionalPlan previewDerived;
        private static CARegionalPlan boundPreviewPlan;
        internal static bool ChoosingLandingAnchor;
        // [setup pass] The dialog-open guard that keeps outside clicks from
        // replacing the plan being edited.
        //
        // Settlement placement happens on the candidate projection, where the
        // ground a settlement will stand on is visible.
        internal static bool EditingDialogOpen;
        private static string pendingIdentity;
        private static bool committing;
        private static int lastObservedSelection = -1;
        private static bool relocationConfirmationOpen;

        // Chosen extent and orientation, held outside the plan so they survive
        // plan replacement.
        //
        // ObserveSelection rebuilds Pending wholesale whenever the player
        // clicks a candidate that is not already a member of the footprint,
        // and that rebuild passed neither value to Create - so both fell back
        // to their defaults (profile.DefaultRegionTileCount, a hard-coded 4,
        // and rotation 0). The plan object was the only place the choice was
        // recorded, so browsing candidates silently discarded it and the
        // operator had to resize at every candidate. Zero means "never chosen"
        // and Create keeps its own default.
        internal static int StickyRegionTileCount;
        internal static int StickyFootprintRotation;

        // The world the sticky pair belongs to. Keying this on world identity
        // means page navigation and an immediate pending-plan save cannot erase
        // the selected size or orientation, while creating another world still
        // starts from its own defaults.
        private static string stickyIdentity;

        // [setup pass] The plan is no longer ephemeral. It survives page
        // navigation in memory and survives the process on disk, keyed to
        // the world it belongs to; the file also records the world seed.
        internal static void Begin(Page_SelectStartingSite page)
        {
            ChoosingLandingAnchor = false;
            EditingDialogOpen = false;
            committing = false;
            relocationConfirmationOpen = false;
            // Map Preview bindings belong to the window interaction that
            // produced them. Re-entering this page starts a fresh interaction
            // and must not let a previously browsed candidate outrank the
            // current world's pending plan.
            boundPreviewPlan = null;
            lastObservedSelection = -1;
            InstallStartingRegionPage(page);
            string identity = WorldIdentity();
            // Keep setup preferences through Back/Next and reset them when the
            // world identity changes.
            if (stickyIdentity != identity)
            {
                StickyRegionTileCount = 0;
                StickyFootprintRotation = 0;
                stickyIdentity = identity;
            }
            if (Pending != null && pendingIdentity == identity)
            {
                // A Back round-trip through world parameters may have changed
                // the tendency policy. Only a draft can adopt those changes;
                // a confirmed plan owns the snapshot that produced its saved
                // realized facts.
                if (!Pending.confirmed
                    && CAWorldTendenciesSession.OperatorEdited)
                    ApplyWorldPolicyToDraft(Pending,
                        CAWorldTendenciesSession.Policy);
                else
                    CAWorldTendenciesSession.Adopt(Pending.worldPolicy);
                Verse.Find.WorldInterface.SelectedTile = Pending.StartTile;
                lastObservedSelection = Pending.startTileId;
                SavePending();
                ObserveSelection();
                return;
            }
            Pending = null;
            previewDerived = null;
            boundPreviewPlan = null;
            pendingIdentity = null;
            TryRestoreFromDisk(identity);
            // A restored footprint is this world's authoritative choice, so
            // adopt its values: the sticky pair must never disagree with the
            // plan the session starts from.
            if (Pending != null)
            {
                StickyRegionTileCount = Pending.RequestedRegionTileCount;
                StickyFootprintRotation = Pending.footprintRotation;
                CAWorldTendenciesSession.Adopt(Pending.worldPolicy);
            }
            ObserveSelection();
        }

        private static void ApplyWorldPolicyToDraft(CARegionalPlan plan,
            CARegionalWorldPolicy policy)
        {
            if (plan == null || plan.confirmed) return;
            plan.worldPolicy = policy?.Copy() ?? new CARegionalWorldPolicy();
            CASettlementComposition.ClearPolicyGeneratedPopulation(plan);
            CARegionalSettlements.Invalidate(plan);
            CARegionalSettlements.EnsureSettlementPattern(plan);
            foreach (CARegionalSettlementPlan settlement in plan.settlements
                .Where(item => item != null))
                CASettlementComposition.EnsureDerived(plan, settlement);
            // The draft has now consumed this world policy. Re-adopt the saved
            // copy so later page openings do not treat the same edit as new.
            CAWorldTendenciesSession.Adopt(plan.worldPolicy);
        }

        private static void InstallStartingRegionPage(
            Page_SelectStartingSite landingPage)
        {
            if (landingPage == null) return;
            Page_CAStartingRegion existing = landingPage.next
                as Page_CAStartingRegion;
            if (existing != null)
            {
                existing.prev = landingPage;
                // Scenario chains put InitGameStart on their terminal page.
                // Move it with the inserted page so landing selection cannot
                // start generation before regional review and confirmation.
                if (landingPage.nextAct != null)
                {
                    existing.nextAct = landingPage.nextAct;
                    landingPage.nextAct = null;
                }
                return;
            }
            var startingRegion = new Page_CAStartingRegion(landingPage,
                landingPage.next);
            startingRegion.nextAct = landingPage.nextAct;
            landingPage.nextAct = null;
            landingPage.next = startingRegion;
        }

        private static string WorldIdentity(World world = null)
        {
            WorldInfo info = (world ?? Verse.Find.World)?.info;
            if (info == null) return null;
            return info.seedString + "|" + info.planetCoverage + "|"
                + info.name;
        }

        private static bool IdentityMatchesWorld(string savedIdentity,
            string currentIdentity)
        {
            if (savedIdentity.NullOrEmpty() || currentIdentity.NullOrEmpty())
                return false;
            return savedIdentity == currentIdentity;
        }

        // Static setup state can outlive the World object it described. This
        // is especially visible in ForcedMap scenarios, which never open the
        // landing page (and therefore never call Begin). A newly constructed
        // world is an unconditional session boundary: the disk-backed draft is
        // left untouched for Begin to restore by identity, while no in-memory
        // plan or preview from the previous world can govern this one.
        internal static void ResetForNewWorld(World world)
        {
            CARegionalProjectionPreview.InvalidateForWorldBoundary();
            CARegionMapWidget.InvalidateForWorldBoundary();
            Pending = null;
            previewDerived = null;
            boundPreviewPlan = null;
            pendingIdentity = null;
            ChoosingLandingAnchor = false;
            EditingDialogOpen = false;
            committing = false;
            relocationConfirmationOpen = false;
            lastObservedSelection = -1;

            string identity = WorldIdentity(world);
            if (stickyIdentity != identity)
            {
                StickyRegionTileCount = 0;
                StickyFootprintRotation = 0;
                stickyIdentity = identity;
            }
        }

        // Non-page authoring paths (the deterministic convergence exercise)
        // must establish the same world ownership that Begin/restore does.
        internal static void AdoptPendingForCurrentWorld(CARegionalPlan plan)
        {
            Pending = plan;
            pendingIdentity = plan == null ? null : WorldIdentity();
            previewDerived = null;
            boundPreviewPlan = null;
        }

        internal static void ClearPending()
        {
            Pending = null;
            pendingIdentity = null;
            previewDerived = null;
            boundPreviewPlan = null;
        }

        internal static CARegionalPlan PendingForCurrentWorld
        {
            get
            {
                if (Pending == null) return null;
                string identity = WorldIdentity();
                return identity != null && pendingIdentity == identity
                    ? Pending : null;
            }
        }

        // Read-only authority probe for hot generation seams. Unlike
        // PreviewPlanFor this does not bind, log, or change a candidate. It is
        // used only to decide whether an unexpected projection lookup must
        // fail neutral (regional intent) or preserve native behavior (an
        // ordinary map).
        internal static bool IntendsRegionalMap(Map map)
        {
            if (map == null) return false;
            try
            {
                CARegionalPlan committed = CARegionalWorldComponent.Current
                    ?.FindRegionForMap(map);
                if (committed != null)
                    return committed.memberTileIds != null
                        && committed.memberTileIds.Count > 1;
                CARegionalPlan plan = PendingForCurrentWorld ?? previewDerived;
                if (plan == null || plan.memberTileIds == null
                    || plan.memberTileIds.Count <= 1
                    || plan.BackingMapSize != map.Size
                    || !map.Tile.Valid
                    || !plan.memberTileIds.Contains(map.Tile.tileId))
                    return false;
                return plan.confirmed
                    || CARegionalCompatibility.IsMapPreviewGenerating();
            }
            catch { return false; }
        }

        private static string PendingDirectoryPath()
        {
            return System.IO.Path.Combine(GenFilePaths.ConfigFolderPath,
                "CARegionalPendingPlans");
        }

        private static string PendingFilePath(string identity)
        {
            if (identity.NullOrEmpty()) return null;
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(identity);
            byte[] digest;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                digest = sha.ComputeHash(bytes);
            string key = string.Concat(digest.Take(16).Select(value =>
                value.ToString("x2")));
            return System.IO.Path.Combine(PendingDirectoryPath(),
                "regional-plan-" + key + ".xml");
        }

        internal static void SavePending()
        {
            if (Pending == null) return;
            if (!Pending.operatorAuthored && Pending.settlements.Count == 0)
                return;
            try
            {
                RefreshDraftRealization(Pending);
                foreach (CARegionalFactionPlan group in
                    Pending.factions)
                    if (group != null) group.resolvedFaction = null;
                string identity = WorldIdentity();
                string keyedPath = PendingFilePath(identity);
                System.IO.Directory.CreateDirectory(PendingDirectoryPath());
                string temporaryPath = keyedPath + ".writing";
                Scribe.saver.InitSaving(temporaryPath,
                    "caRegionalPendingPlan");
                try
                {
                    CARegionalPlan plan = Pending;
                    int authoringDataEpoch =
                        CAPendingAuthoringDataEpoch.Current;
                    Scribe_Values.Look(ref authoringDataEpoch,
                        "authoringDataEpoch", 0);
                    Scribe_Values.Look(ref identity, "worldIdentity");
                    Scribe_Deep.Look(ref plan, "plan");
                }
                finally
                {
                    Scribe.saver.FinalizeSaving();
                }
                System.IO.File.Copy(temporaryPath, keyedPath, true);
                System.IO.File.Delete(temporaryPath);
                pendingIdentity = identity;
            }
            catch (Exception e)
            {
                Log.Warning("[CA][Regional] pending-plan save failed: "
                    + e);
            }
        }

        private static void TryRestoreFromDisk(string identity)
        {
            if (identity == null) return;
            string path = PendingFilePath(identity);
            if (!System.IO.File.Exists(path)) return;
            try
            {
                int authoringDataEpoch = 0;
                string fileIdentity = null;
                CARegionalPlan plan = null;
                Scribe.loader.InitLoading(path);
                try
                {
                    Scribe_Values.Look(ref authoringDataEpoch,
                        "authoringDataEpoch", 0);
                    if (CAPendingAuthoringDataEpoch.CanRead(
                            authoringDataEpoch))
                        Scribe_Values.Look(ref fileIdentity, "worldIdentity");
                    if (CAPendingAuthoringDataEpoch.CanRead(
                            authoringDataEpoch)
                        && IdentityMatchesWorld(fileIdentity, identity))
                        Scribe_Deep.Look(ref plan, "plan");
                }
                finally
                {
                    Scribe.loader.FinalizeLoading();
                }
                if (!CAPendingAuthoringDataEpoch.CanRead(
                        authoringDataEpoch))
                {
                    CAPendingAuthoringDataEpoch.RecordDiscard(
                        "pending starting region");
                    try
                    {
                        // The exact identity-keyed pre-release draft is owned
                        // by CA and cannot become current. Remove it once so it
                        // is not retried on every session.
                        System.IO.File.Delete(path);
                    }
                    catch (Exception deleteFailure)
                    {
                        Log.Warning("[CA][Regional] could not remove discarded "
                            + "pending plan " + path + ": " + deleteFailure);
                    }
                    return;
                }
                if (plan == null) return;
                if (!CAPendingAuthoringDataEpoch.TryUpgradeRegionalPlan(
                        authoringDataEpoch, plan, out string upgradeFailure))
                {
                    Log.Warning("[CA][Regional] refused pending plan from "
                        + path + ": " + upgradeFailure + ".");
                    return;
                }

                string validationFailure;
                if (!TryValidateRestoredPlan(plan, out validationFailure))
                {
                    Log.Warning("[CA][Regional] refused pending plan from "
                        + path + ": " + validationFailure + ".");
                    return;
                }

                string restoredScale = RestoreSavedMapScale(plan);
                // A confirmed plan already owns one persisted realization.
                // Replacing its policy snapshot would describe causes that did
                // not produce the saved facts. Current session tendencies can
                // replace defaults only while a draft is still unconfirmed;
                // its authored positions and relation overrides remain inputs
                // when that draft is realized again.
                bool currentTendenciesWin =
                    CAWorldTendenciesSession.OperatorEdited
                    && !plan.confirmed;
                if (currentTendenciesWin)
                    ApplyWorldPolicyToDraft(plan,
                        CAWorldTendenciesSession.Policy);
                else
                    CAWorldTendenciesSession.Adopt(plan.worldPolicy);
                // Loading is an explicit reconstruction boundary. The page
                // that displays this draft remains read-only until the
                // operator invokes a control.
                RefreshDraftRealization(plan);
                Pending = plan;
                pendingIdentity = identity;
                Verse.Find.WorldInterface.SelectedTile = plan.StartTile;
                lastObservedSelection = plan.startTileId;
                Messages.Message("Restored starting region: "
                    + plan.RegionTileCount + " connected areas, "
                    + plan.settlements.Count + " settlement"
                    + (plan.settlements.Count == 1 ? "" : "s") + ", arrival at "
                    + CARegionalPlanUtility.TileWords(plan.startTileId)
                    + (currentTendenciesWin
                        ? ". Current world tendencies kept."
                        : ". Saved world tendencies restored.")
                    + (restoredScale.NullOrEmpty() ? ""
                        : " " + restoredScale),
                    MessageTypeDefOf.PositiveEvent, false);
                Log.Message("[CA][Regional] pending plan restored from "
                    + path + "; landing " + plan.startTileId + "; members "
                    + string.Join(",", plan.memberTileIds)
                    + "; factions " + plan.factions.Count
                    + "; settlements " + plan.settlements.Count);
                // Persist a successfully upgraded adjacent draft immediately,
                // so the disk owner and the in-memory owner cannot diverge.
                if (authoringDataEpoch != CAPendingAuthoringDataEpoch.Current)
                    SavePending();
            }
            catch (Exception e)
            {
                Log.Warning("[CA][Regional] pending-plan restore failed from "
                    + path + ": " + e);
            }
        }

        // Every persisted draft and every confirmation preview uses the same
        // deterministic realization path. Cause edits therefore cannot leave
            // stale routes, programs, provisions, or settlement axes on screen.
        internal static void RefreshDraftRealization(CARegionalPlan plan)
        {
            if (plan == null || plan.confirmed) return;
            CARegionalSettlements.Invalidate(plan);
            CARegionalSettlements.RealizeForConfirmation(plan);
            foreach (CARegionalSettlementPlan settlement in plan.settlements
                .Where(item => item != null))
            {
                CASettlementComposition.EnsureDerived(plan, settlement);
                CASettlementProgramRegistry.EnsureDerived(plan, settlement);
            }
        }

        private static bool TryValidateRestoredPlan(CARegionalPlan plan,
            out string failure)
        {
            failure = null;
            string identityFailure;
            if (!CARegionalPlanUtility.TryValidateStableIdentities(plan,
                    out identityFailure))
            {
                failure = identityFailure;
                return false;
            }

            CAExpandedLandmassProfile profile;
            if (!CAExpandedLandmassProfile.TryFor(plan.mapSize, out profile)
                || !plan.ValidFor(profile))
            {
                failure = "regional footprint is incomplete or inconsistent";
                return false;
            }

            if (plan.confirmed
                && !CARegionalPlanUtility.TryValidateStartingSettlements(plan,
                    out failure))
                return false;
            if (plan.confirmed
                && !CARegionalSettlements.TryValidateRealization(plan,
                    out failure))
            {
                failure = "confirmed plan has invalid realized settlement "
                    + "state: " + failure;
                return false;
            }
            return true;
        }

        private static string RestoreSavedMapScale(CARegionalPlan plan)
        {
            if (plan == null || plan.mapSize <= 0
                || Verse.Find.GameInitData == null
                || Verse.Find.GameInitData.mapSize == plan.mapSize)
                return null;
            CAExpandedLandmassProfile savedProfile;
            if (!CAExpandedLandmassProfile.TryFor(plan.mapSize,
                    out savedProfile))
                return null;
            int replaced = Verse.Find.GameInitData.mapSize;
            Verse.Find.GameInitData.mapSize = savedProfile.Size;
            return "Its saved local area scale " + savedProfile.Size
                + " was restored instead of the reset value " + replaced + ".";
        }

        internal static void ObserveSelection()
        {
            CAExpandedLandmassProfile profile;
            if (!CAExpandedLandmassProfile.TryFor(
                    Verse.Find.GameInitData.mapSize, out profile))
            {
                Pending = null;
                previewDerived = null;
                boundPreviewPlan = null;
                ChoosingLandingAnchor = false;
                return;
            }
            if (Pending != null && Pending.mapSize != profile.Size)
                ApplyLocalScaleChange(profile, Pending);
            PlanetTile selected = Verse.Find.WorldInterface.SelectedTile;
            if (!selected.Valid || selected.Layer != Verse.Find.WorldGrid.Surface)
                return;
            if (committing)
            {
                if (Pending != null && selected.tileId == Pending.startTileId)
                    return;
                committing = false;
            }
            if (ChoosingLandingAnchor && Pending != null)
            {
                // Armed is not action: only a NEW selection is a choice.
                // Without this gate, arming instantly consumed the already-
                // pinned start tile, set the arrival to itself, and
                // disarmed - the player's real click never got a chance.
                // That is why "choose arrival" never worked, in every
                // prior UI that armed this flow.
                if (selected.tileId == lastObservedSelection
                    || selected.tileId == Pending.startTileId)
                    return;
                if (TrySetArrival(Pending, selected.tileId,
                        out string arrivalRefusal))
                    Messages.Message("Arrival area: "
                        + CARegionalPlanUtility.TileSummary(selected.tileId),
                        MessageTypeDefOf.TaskCompletion, false);
                else
                    Messages.Message(arrivalRefusal,
                        MessageTypeDefOf.RejectInput, false);
                ChoosingLandingAnchor = false;
                Verse.Find.WorldInterface.SelectedTile = Pending.StartTile;
                lastObservedSelection = Pending.startTileId;
                return;
            }

            // GRANULAR COMPOSITION: a shift-click authors the extent tile
            // by tile -- add the clicked area, or remove it -- through the
            // same replacement funnel the size and turn presets use, so
            // settlements, factions, and decisions are kept identically.
            if (Pending != null && Pending.mapSize == profile.Size
                && Pending.memberTileIds != null
                && (UnityEngine.Input.GetKey(KeyCode.LeftShift)
                    || UnityEngine.Input.GetKey(KeyCode.RightShift))
                && selected.tileId != lastObservedSelection)
            {
                CARegionalPlan composing = Pending;
                ToggleRegionArea(profile, composing, selected);
                Verse.Find.WorldInterface.SelectedTile = Pending.StartTile;
                lastObservedSelection = Pending.startTileId;
                return;
            }

            // [setup pass] While the setup dialog is open, the plan is
            // the object being edited: an outside click must not silently
            // replace the whole footprint.
            if (EditingDialogOpen && Pending != null
                && !Pending.memberTileIds.Contains(selected.tileId))
            {
                if (selected.tileId != lastObservedSelection)
                    Messages.Message("Close the Starting Region page to move "
                        + "the whole region elsewhere.",
                        MessageTypeDefOf.RejectInput, false);
                Verse.Find.WorldInterface.SelectedTile = Pending.StartTile;
                lastObservedSelection = Pending.startTileId;
                return;
            }

            // A world-map selection is not permission to erase an authored
            // region. Vanilla's random-site action uses the same selection
            // route as a click, so a designed region needs confirmation before
            // its location changes.
            if (Pending != null && Pending.mapSize == profile.Size
                && Pending.memberTileIds != null
                && !Pending.memberTileIds.Contains(selected.tileId)
                && HasDesignedRegion(Pending))
            {
                CARegionalPlan current = Pending;
                PlanetTile destination = selected;
                Verse.Find.WorldInterface.SelectedTile = current.StartTile;
                lastObservedSelection = current.startTileId;
                if (!relocationConfirmationOpen)
                {
                    relocationConfirmationOpen = true;
                    Verse.Find.WindowStack.Add(Dialog_MessageBox
                        .CreateConfirmation(
                            "Move your starting region to "
                                + CARegionalPlanUtility.TileWords(
                                    destination.tileId) + "?\n\n"
                                + "The land and its geographic features will "
                                + "change. Your factions, settlements, "
                                + "population groups, beliefs, starting "
                                + "conditions, "
                                + "relationships and world tendencies will "
                                + "all stay. Each settlement keeps the same "
                                + "relative area in the new region.",
                            delegate
                            {
                                relocationConfirmationOpen = false;
                                if (Pending == current)
                                    RelocateDesignedRegion(profile, current,
                                        destination);
                            }, delegate
                            {
                                relocationConfirmationOpen = false;
                            }, false, "Move starting region"));
                }
                return;
            }

            if (selected.tileId == lastObservedSelection) return;
            lastObservedSelection = selected.tileId;

            // Selecting another member of the currently authored footprint is
            // an inspection/preview action, not a request to replace the
            // region. Map Preview separately canonicalizes its generation seed
            // to the saved landing tile. Preserve every pending axis here so a
            // member click cannot discard extent, orientation, settlements,
            // physical complexes, roles, factions, or world policy before the
            // preview hook runs.
            if (Pending != null && Pending.mapSize == profile.Size
                && Pending.memberTileIds != null
                && Pending.memberTileIds.Contains(selected.tileId))
            {
                Log.Message("[CA][Regional][Selection] retained pending "
                    + "footprint " + Pending.regionalId + "; inspected member "
                    + selected.tileId + "; canonical landing "
                    + Pending.startTileId + "; members "
                    + string.Join(",", Pending.memberTileIds) + "; backing "
                    + Pending.BackingMapSize.x + "x"
                    + Pending.BackingMapSize.z + "; authored settlements "
                    + (Pending.settlements?.Count ?? 0));
                return;
            }

            if (Pending == null || Pending.mapSize != profile.Size
                || Pending.bundleRootTileId != selected.tileId)
            {
                // Land is already regional: new ground selects the
                // persistent partition region containing it, and composing
                // edits from that base. The sticky extent and orientation
                // remain the fallback for land outside the partition.
                Pending = CARegionalPlanUtility.CreateAuthoredAt(profile,
                    selected, StickyRegionTileCount,
                    StickyFootprintRotation);
                pendingIdentity = WorldIdentity();
                previewDerived = null;
                boundPreviewPlan = null;
                Log.Message("[CA][Regional] provisional starting footprint "
                    + Pending.regionalId + "; local source scale "
                    + profile.Size + "x" + profile.Size + "; extent actual "
                    + Pending.RegionTileCount + ", requested "
                    + Pending.RequestedRegionTileCount + " tiles; aggregate "
                    + Pending.BackingMapSize.x + "x"
                    + Pending.BackingMapSize.z + "; root "
                    + Pending.bundleRootTileId + "; members "
                    + string.Join(",", Pending.memberTileIds));
                Log.Message("[CA][Regional] selection topology "
                    + CARegionalPlanUtility.SelectionTopologySummary(Pending));
                CARegionalCompatibility.NotifyPreviewChanged();
            }
        }

        // Going Back to world parameters and choosing another local area scale
        // changes the resolution of this candidate, not the identity or social
        // contents of its geographic region. Reframe the same saved land in
        // place; never route a scale change through the blank-plan constructor.
        private static void ApplyLocalScaleChange(
            CAExpandedLandmassProfile profile, CARegionalPlan plan)
        {
            if (plan == null || plan.mapSize == profile.Size) return;
            int oldScale = plan.mapSize;
            List<PlanetTile> members = (plan.memberTileIds
                    ?? new List<int>()).Select(
                        CARegionalPlanUtility.SurfaceTile)
                .Where(tile => tile.Valid).ToList();
            plan.mapSize = profile.Size;
            IntVec3 backing = CARegionalPlanUtility.BackingMapSize(profile,
                plan.BundleRoot, members);
            plan.backingMapWidth = backing.x;
            plan.backingMapHeight = backing.z;
            plan.candidateId = CARegionalPlanUtility.MintCandidateId();
            plan.confirmed = false;
            plan.developerExercise = false;
            plan.creationSummary = (plan.creationSummary
                    ?? "operator-selected connected world-tile bundle")
                + "; local area scale changed from " + oldScale + " to "
                + profile.Size + " with authored region intact";
            pendingIdentity = WorldIdentity();
            previewDerived = null;
            boundPreviewPlan = null;
            SavePending();
            CARegionalCompatibility.NotifyPreviewChanged();
            Messages.Message("Local area scale changed from " + oldScale
                    + " to " + profile.Size + "; "
                    + (plan.factions?.Count ?? 0) + " faction"
                    + ((plan.factions?.Count ?? 0) == 1 ? "" : "s")
                    + " and " + (plan.settlements?.Count ?? 0) + " settlement"
                    + ((plan.settlements?.Count ?? 0) == 1 ? "" : "s")
                    + " were kept.",
                MessageTypeDefOf.TaskCompletion, false);
            Log.Message("[CA][Regional][Selection] local area scale "
                + oldScale + " -> " + profile.Size + "; region "
                + plan.regionalId + "; factions "
                + (plan.factions?.Count ?? 0) + "; settlements "
                + (plan.settlements?.Count ?? 0) + "; members "
                + string.Join(",", plan.memberTileIds ?? new List<int>()));
        }

        internal static CARegionalPlan ActivePreviewPlan
        {
            get
            {
                // Only the candidate bound by the current external preview
                // request may supply size or generation authority. Pending
                // and cached derived plans are lookup inputs, not proof that
                // an unrelated world-tile preview belongs to the region.
                return boundPreviewPlan;
            }
        }

        internal static void BindPreviewPlan(CARegionalPlan plan)
        {
            if (plan == null) return;
            boundPreviewPlan = plan;
            if (plan != PendingForCurrentWorld) previewDerived = plan;
        }

        internal static void ClearPreviewBinding()
        {
            boundPreviewPlan = null;
        }

        internal static CARegionalPlan EnsurePreviewPlan(PlanetTile mapTile,
            CAExpandedLandmassProfile profile, int regionTileCount)
        {
            if (!mapTile.Valid) return null;
            CARegionalPlan pending = PendingForCurrentWorld;
            if (pending != null && pending.mapSize == profile.Size
                && pending.memberTileIds.Contains(mapTile.tileId))
                return pending;
            CARegionalPlan existing = CARegionalWorldComponent.Current
                ?.FindRegionContaining(mapTile);
            if (existing != null && existing.mapSize == profile.Size)
            {
                previewDerived = existing;
                return existing;
            }
            // THE PREVIEW IS THE REGION, NOT A BUNDLE AROUND A TILE. The
            // world already has a partition, and materialization realizes
            // that record through CreateFromTopology. This path ignored
            // it and built a stock hex bundle around whatever tile was
            // clicked under a freshly minted identity, so on every fresh
            // world the previewed member set, identity and backing size
            // contradicted what would actually be created - the exact
            // "a tile becomes its own competing region" failure the
            // partition exists to prevent. The record answers first;
            // Create remains the fallback for ground the partition does
            // not cover.
            CARegionalTopologyRecord record = CARegionalWorldComponent
                .Current?.TopologyRecordAt(mapTile);
            if (record != null && record.memberTileIds != null
                && record.memberTileIds.Count > 0)
            {
                if (previewDerived != null
                    && previewDerived.mapSize == profile.Size
                    && previewDerived.regionalId == record.regionId)
                    return previewDerived;
                previewDerived = CARegionalPlanUtility.CreateFromTopology(
                    profile, record, mapTile);
                return previewDerived;
            }
            if (previewDerived != null
                && previewDerived.mapSize == profile.Size
                && previewDerived.RequestedRegionTileCount == regionTileCount
                && previewDerived.bundleRootTileId == mapTile.tileId)
                return previewDerived;
            previewDerived = CARegionalPlanUtility.Create(profile, mapTile,
                false, regionTileCount);
            return previewDerived;
        }

        internal static CARegionalPlan PreviewPlanFor(PlanetTile mapTile,
            IntVec3 backingSize)
        {
            CARegionalPlan plan = ActivePreviewPlan;
            if (plan == null || !mapTile.Valid
                || plan.memberTileIds == null
                || !plan.memberTileIds.Contains(mapTile.tileId)) return null;
            // A preview generates at reduced resolution, so its map size
            // rarely equals the full backing frame. Demanding equality kept
            // the projection plan unresolvable on every scaled preview --
            // which is why early mutator consumers failed neutral and no
            // mutator content ever reached the preview terrain. Exact size
            // remains required outside preview generation.
            if (plan.BackingMapSize != backingSize
                && !CARegionalCompatibility.IsMapPreviewGenerating())
                return null;
            // Preview rendering may use an unconfirmed candidate so projected
            // geography and tile mutators draw the same regional map the
            // player is inspecting. Played maps still require confirmation.
            if (!plan.confirmed
                && !CARegionalCompatibility.IsMapPreviewGenerating())
            {
                Log.Error("[CA][Regional] refusing generation authority to "
                    + "unconfirmed candidate "
                    + (plan.candidateId ?? "(no id)") + " for tile "
                    + mapTile.tileId + ". A candidate must be confirmed "
                    + "through the setup flow before it can generate; this "
                    + "map will generate natively.");
                return null;
            }
            return plan;
        }

        internal static bool PrepareNext(bool commit,
            bool permitTransientTest = false)
        {
            CAExpandedLandmassProfile profile;
            if (!CAExpandedLandmassProfile.TryFor(
                    Verse.Find.GameInitData.mapSize, out profile)) return true;
            if (!committing) ObserveSelection();
            CARegionalPlan plan = PendingForCurrentWorld;
            if (plan == null)
            {
                Messages.Message("Choose a starting region before continuing.",
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
            RefreshDraftRealization(plan);
            if (!plan.ValidFor(profile))
            {
                Messages.Message(InvalidPlanMessage(plan, profile),
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
            CARegionalProjectionKernel geographyKernel =
                CARegionalProjectionPreview.KernelFor(plan);
            string geographyFailure;
            if (!CARegionalGeographyContract.TryValidate(plan,
                    geographyKernel, out geographyFailure))
            {
                Messages.Message("This region cannot generate correctly: "
                        + geographyFailure,
                    MessageTypeDefOf.RejectInput, false);
                Log.Warning("[CA][Regional] composition validation refused "
                    + "candidate " + (plan.candidateId ?? "unknown") + ": "
                    + geographyFailure);
                return false;
            }
            string settlementFailure;
            if (!CARegionalPlanUtility.TryValidateStartingSettlements(plan,
                    out settlementFailure))
            {
                Messages.Message(settlementFailure,
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
            PlanetTile start = plan.StartTile;
            var reason = new System.Text.StringBuilder();
            if (!TileFinder.IsValidTileForNewSettlement(start, reason))
            {
                Messages.Message(reason.ToString(),
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
            CARegionalWorldComponent world = CARegionalWorldComponent.Current;
            string reservationFailure = null;
            if (world == null || !world.CanReserveRegion(plan,
                    out reservationFailure))
            {
                Messages.Message("The regional footprint cannot be reserved: "
                    + (reservationFailure ?? "world state is unavailable"),
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
            // The pre-generation compatibility gate. Confirmation is where an
            // incompatible region is stopped, because it is the last point at
            // which the player can still choose differently and no durable
            // world exists yet. Generation-side enforcement is only a backstop.
            CACompatibilityReport compatibility =
                CARegionalContentCompatibility.Evaluate(plan);
            bool developerExercise = false;
            if (commit && !compatibility.IsValid)
            {
                // A person who explicitly accepts the final developer-mode
                // warning may run the exact composition as a transient test.
                // This is not a relaxation of durable materialization: the
                // stamp routes the plan into the runtime-only world slot and
                // disables every save path. It is the evidence-producing path
                // for Unsupported as well as ImplementedUnverified content;
                // without it the very contracts blocking promotion could never
                // be observed on the selected region.
                bool explicitTransientTest = permitTransientTest
                    && Prefs.DevMode;
                if (explicitTransientTest)
                {
                    developerExercise = true;
                    Log.Warning("[CA][Regional] DEVELOPER EXERCISE: confirming "
                        + "a candidate carrying non-production content. "
                        + compatibility.Summary() + ". This plan is stamped "
                        + "developerExercise and is NOT valid for a durable "
                        + "start; saving is disabled and it exists to produce "
                        + "runtime evidence. Explicit transient acceptance="
                        + explicitTransientTest + ".");
                }
                else
                {
                    Messages.Message(compatibility.ActionableFailure(),
                        MessageTypeDefOf.RejectInput, false);
                    Log.Warning("[CA][Regional] confirmation refused - "
                        + compatibility.Summary() + "; blocking: "
                        + string.Join(", ", compatibility.Blocking
                            .Select(entry => entry.DefName).Distinct()));
                    return false;
                }
            }
            if (commit)
            {
                CABehaviorDecision authoringDecision =
                    CABehaviorGate.EvaluateForSelection(
                        "spatial.creation_authoring",
                        new CABehaviorContext(actor: null,
                            actorContext: CAActorContext.CreationAuthor,
                            initiative: CAInitiativeTier.Standard,
                            authorityOrigin:
                                CAAuthorityOrigin.WorldAuthoring,
                            authoritySatisfied: true,
                            knowledgeSatisfied: true,
                            knowledgeFresh: true, liveValidated: true,
                            capabilitySatisfied: world != null,
                            materialSatisfied: compatibility.IsValid
                                || developerExercise,
                            currentIntentCompatible: true,
                            directPlayerOwnership: false,
                            authorityBasis:
                                "explicit starting-region confirmation",
                            knowledgeBasis:
                                "validated current authored candidate",
                            owner: "creation author"));
                if (!authoringDecision.SelectionApproved)
                {
                    Messages.Message("The starting region cannot be confirmed: "
                        + authoringDecision.PrimaryReason + ".",
                        MessageTypeDefOf.RejectInput, false);
                    return false;
                }
                committing = true;
                // The candidate becomes authoritative here and nowhere else.
                // Its candidateId is carried through unchanged, which
                // establishes the stable identity and seeding that preview
                // parity requires. It does not by itself establish parity:
                // preview and generation must also consume the same execution
                // contracts, and that equivalence is unverified.
                if (plan.candidateId.NullOrEmpty())
                    plan.candidateId =
                        CARegionalPlanUtility.MintCandidateId();
                CARegionalSettlements.RealizeForConfirmation(plan);
                foreach (CARegionalSettlementPlan settlement in
                    plan.settlements.Where(item => item != null))
                {
                    CASettlementComposition.EnsureDerived(plan, settlement);
                    CASettlementProgramRegistry.EnsureDerived(plan,
                        settlement);
                }
                plan.confirmed = true;
                plan.developerExercise = developerExercise;
                // Confirmation changes only canonical authored state. External
                // world objects remain untouched until actual map generation;
                // this keeps Back/Forward navigation and later setup pages
                // fully reversible.
                Verse.Find.WorldInterface.SelectedTile = start;
                Verse.Find.GameInitData.startingTile = start;
                lastObservedSelection = start.tileId;
                previewDerived = null;
                boundPreviewPlan = null;
                SavePending();
                Log.Message("[CA][Regional] candidate " + plan.candidateId
                    + " confirmed as the authoritative regional plan; root "
                    + plan.startTileId + ", " + plan.memberTileIds.Count
                    + " members");
            }
            return true;
        }

        // Vanilla validates and captures WorldInterface.SelectedTile in both
        // CanDoNext and DoNext. The visible selection may be another member the
        // player was inspecting, while the canonical arrival is startTileId.
        // Align only at transition time so goodwill checks and GameInitData use
        // the authored arrival rather than the bundle geometry anchor.
        // ARRIVAL IS NOT GEOGRAPHY. Where the arriving party enters an
        // already-defined region is one field of authored state. Setting it
        // consumes the region and changes nothing about it: not membership,
        // not topology, not the projection or its identity, not the extent.
        // Every arrival change in the product funnels through here so that
        // stays structurally true.
        internal static bool TrySetArrival(CARegionalPlan plan,
            int memberTileId, out string refusal)
        {
            refusal = ArrivalRefusalFor(plan, memberTileId);
            if (refusal != null) return false;
            plan.startTileId = memberTileId;
            plan.operatorAuthored = true;
            SavePending();
            // The world's selection ring follows the arrival - it is the
            // one native mark players read as "this tile is chosen", and
            // the preview dedupe makes this realignment free: same
            // composition, no regeneration.
            Verse.Find.WorldInterface.SelectedTile = plan.StartTile;
            lastObservedSelection = plan.startTileId;
            return true;
        }

        // The query half of TrySetArrival: null means this member can host
        // the arrival right now; otherwise the stated reason it cannot.
        // The workspace derives its valid-location overlay from this, so
        // what the overlay shows and what the click accepts cannot drift.
        internal static string ArrivalRefusalFor(CARegionalPlan plan,
            int memberTileId)
        {
            if (plan == null || plan.memberTileIds == null
                || !plan.memberTileIds.Contains(memberTileId))
                return "Choose an arrival area inside the region.";
            if (plan.settlements.Any(item => item != null
                && item.memberTileId == memberTileId))
                return "That area already contains one of this region's "
                    + "settlements. Choose another arrival area, or move "
                    + "that settlement first.";
            PlanetTile tile = CARegionalPlanUtility.SurfaceTile(memberTileId);
            var reason = new System.Text.StringBuilder();
            if (!tile.Valid
                || !TileFinder.IsValidTileForNewSettlement(tile, reason))
                return reason.Length > 0 ? reason.ToString()
                    : "That area cannot host the arrival.";
            return null;
        }

        internal static void AlignVanillaLandingSelection()
        {
            PlanetTile start = PendingForCurrentWorld?.StartTile
                ?? PlanetTile.Invalid;
            if (!start.Valid) return;
            Verse.Find.WorldInterface.SelectedTile = start;
            lastObservedSelection = start.tileId;
        }

        private static string InvalidPlanMessage(CARegionalPlan plan,
            CAExpandedLandmassProfile profile)
        {
            if (plan?.memberTileIds == null || plan.memberTileIds.Count == 0)
                return "This starting region contains no connected land. "
                    + "Choose another region.";
            if (plan.memberTileIds.Count != plan.RegionTileCount)
                return "This saved region records " + plan.RegionTileCount
                    + " areas but contains " + plan.memberTileIds.Count
                    + ". Reload it so the saved geography can be repaired.";
            if (!plan.memberTileIds.Contains(plan.startTileId))
                return "Choose an arrival area inside this region before "
                    + "continuing.";
            CARegionalSettlementPlan outside = plan.settlements?.FirstOrDefault(place =>
                place != null
                && !plan.memberTileIds.Contains(place.memberTileId));
            if (outside != null)
                return CARegionalPlanUtility.SettlementName(plan, outside)
                    + " lies outside this region. Move the settlement or "
                    + "restore the region shape that contained it.";
            IntVec3 expected = CARegionalPlanUtility.BackingMapSize(profile,
                plan.BundleRoot, plan.memberTileIds.Select(
                    CARegionalPlanUtility.SurfaceTile)
                    .Where(tile => tile.Valid).ToList());
            if (plan.BackingMapSize != expected)
                return "This region's generated-map dimensions no longer "
                    + "match its connected land. Turn or move the region to "
                    + "rebuild them before continuing.";
            return "This saved starting region could not be validated. Its "
                + "arrival area and settlements were kept; turn or move the "
                + "region to rebuild its geography.";
        }

        // One gathering of the landing screen's stated facts, shared by the
        // CAO workspace and the fallback panel so both always say the same
        // things about the same land.
        internal sealed class CALandingFacts
        {
            internal bool reservable;
            internal string reservationFailure;
            internal string biomes;
            internal string relief;
            internal string geology;
            internal string features;
            internal CARegionalProjectionKernel geographyKernel;
            internal CARegionalGeographyComposition geography;
            internal int actualAreas;
            internal int requestedAreas;
            internal int claimedSea;
            internal int claimedImpassable;
            internal string surveyText;
            internal string noteText;
        }

        internal static CALandingFacts GatherLandingFacts(CARegionalPlan plan)
        {
            long key = LandingFactsKey(plan);
            CALandingFacts landing = landingFactsCache;
            if (landing == null || landingFactsKey != key)
            {
                landing = DeriveLandingFacts(plan);
                landingFactsCache = landing;
                landingFactsKey = key;
            }
            // Reservation validity depends on world state outside the plan
            // (other regions' reservations, settlements near the start
            // tile), so it can change while every keyed input stays the
            // same; it is re-asked every pass.
            landing.reservable = CARegionalWorldComponent.Current != null
                && CARegionalWorldComponent.Current.CanReserveRegion(plan,
                    out landing.reservationFailure);
            var startReason = new System.Text.StringBuilder();
            if (landing.reservable && !TileFinder.IsValidTileForNewSettlement(
                    plan.StartTile, startReason))
            {
                landing.reservable = false;
                landing.reservationFailure = startReason.ToString();
            }
            return landing;
        }

        private static CALandingFacts landingFactsCache;
        private static long landingFactsKey;

        // Every plan input the derived facts read, folded into one value so
        // the per-frame member sweeps only rerun when something they state
        // has actually changed.
        private static long LandingFactsKey(CARegionalPlan plan)
        {
            unchecked
            {
                long key = 17L;
                key = key * 31L + (Verse.Find.World?.info?.Seed ?? 0);
                key = key * 31L + plan.startTileId;
                key = key * 31L + (plan.settlements?.Count ?? 0);
                key = key * 31L + plan.RequestedRegionTileCount;
                List<int> members = plan.memberTileIds;
                key = key * 31L + (members?.Count ?? 0);
                if (members != null)
                    for (int i = 0; i < members.Count; i++)
                        key = key * 31L + members[i];
                IReadOnlyList<int> reserved = plan.ReservedTileIds;
                key = key * 31L + (reserved?.Count ?? 0);
                if (reserved != null)
                    for (int i = 0; i < reserved.Count; i++)
                        key = key * 31L + reserved[i];
                CAGroundwaterTuning tuning = plan.groundwater;
                if (tuning != null)
                {
                    key = key * 31L + tuning.saltIntrusion;
                    key = key * 31L + tuning.brackishReach;
                    key = key * 31L + tuning.highTableReach;
                    key = key * 31L + tuning.wetShallow.GetHashCode();
                    key = key * 31L + tuning.wetMedium.GetHashCode();
                    key = key * 31L + tuning.wetDeep.GetHashCode();
                    key = key * 31L + tuning.catchmentEfficiency.GetHashCode();
                }
                key = CAFeatureShapeModel.FoldSignature(key, plan);
                return key;
            }
        }

        private static CALandingFacts DeriveLandingFacts(CARegionalPlan plan)
        {
            var landing = new CALandingFacts();
            landing.biomes = string.Join(", ", plan.memberTileIds
                .Select(CARegionalPlanUtility.SurfaceTile)
                .Where(tile => tile.Valid)
                .GroupBy(tile => tile.Tile.PrimaryBiome)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key?.defName)
                .Select(group => group.Count() + " "
                    + (group.Key?.label ?? "unknown")));
            landing.relief = string.Join(", ", plan.memberTileIds
                .Select(CARegionalPlanUtility.SurfaceTile)
                .Where(tile => tile.Valid)
                .GroupBy(tile => tile.Tile.hilliness)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Select(group => group.Count() + " "
                    + group.Key.ToString().ToLower()
                        .Replace("largehills", "hilly")
                        .Replace("smallhills", "gentle hills")
                        .Replace("mountainous", "mountains")
                        .Replace("flat", "flat")
                        .Replace("impassable", "impassable")));
            landing.geology = string.Join(", ", plan.memberTileIds
                .Select(CARegionalPlanUtility.SurfaceTile)
                .Where(tile => tile.Valid)
                .SelectMany(tile => Verse.Find.World.NaturalRockTypesIn(tile))
                .Distinct().OrderBy(rock => rock.label)
                .Select(rock => rock.LabelCap.ToString()));
            // The special features the selected land actually carries -- the
            // same vanilla-assigned tile mutators the generated map will
            // realize. CA never rolls its own; this states incidence so a
            // person can see what their composition collected.
            landing.features = string.Join(", ", plan.memberTileIds
                .Select(CARegionalPlanUtility.SurfaceTile)
                .Where(tile => tile.Valid)
                .SelectMany(tile => tile.Tile.Mutators
                    ?? Enumerable.Empty<TileMutatorDef>())
                .Where(def => def != null)
                .GroupBy(def => def.label ?? def.defName)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.Key + (group.Count() > 1
                    ? " (" + group.Count() + ")" : "")));
            landing.geographyKernel =
                CARegionalProjectionPreview.KernelFor(plan);
            landing.geography =
                CARegionalGeographyContract.Inspect(plan,
                    landing.geographyKernel);
            landing.actualAreas = plan.RegionTileCount;
            landing.requestedAreas = plan.RequestedRegionTileCount;
            // The outline can claim ground nobody can use -- ocean bays and
            // impassable massifs enclosed by the selected areas. They still
            // occupy part of the generated map, so the person choosing this
            // land is told, instead of discovering a third of their map is
            // sea or sheer rock.
            foreach (int claimedId in plan.ReservedTileIds
                ?? (IReadOnlyList<int>)new List<int>())
            {
                if (plan.memberTileIds.Contains(claimedId)) continue;
                PlanetTile claimedTile =
                    CARegionalPlanUtility.SurfaceTile(claimedId);
                if (!claimedTile.Valid
                    || !CARegionalGeometry.IsBlocked(claimedTile)) continue;
                if (claimedTile.Tile?.WaterCovered == true)
                    landing.claimedSea++;
                else landing.claimedImpassable++;
            }
            landing.surveyText = CAGroundwater.SurveyTile(plan.startTileId,
                plan.groundwater);
            landing.noteText = (plan.settlements.Count == 0
                    ? "No neighbors live here yet."
                    : plan.settlements.Count + " neighbor settlement"
                        + (plan.settlements.Count == 1 ? "" : "s")
                        + " will already be living here.")
                + " These areas become one continuous map when you start.";
            return landing;
        }

        private static bool HasDesignedRegion(CARegionalPlan plan)
        {
            return plan != null && ((plan.settlements?.Count ?? 0) > 0
                || (plan.factions?.Count ?? 0) > 0
                || (plan.relations?.Count ?? 0) > 0
                || plan.playerFounding?.arrangement != null
                || plan.playerFounding?.confirmed == true
                || plan.regionNameAuthored);
        }

        private static void RelocateDesignedRegion(
            CAExpandedLandmassProfile profile, CARegionalPlan current,
            PlanetTile destination)
        {
            if (current == null || !destination.Valid) return;
            CARegionalPlan replacement = CARegionalPlanUtility
                .CreateAuthoredAt(profile, destination,
                    current.RequestedRegionTileCount,
                    current.FootprintRotation);
            if (replacement.memberTileIds == null
                || replacement.memberTileIds.Count == 0) return;

            var oldOrder = new Dictionary<int, int>();
            for (int i = 0; i < current.memberTileIds.Count; i++)
                oldOrder[current.memberTileIds[i]] = i;
            Func<int, int> correspondingArea = oldTileId =>
            {
                int index;
                if (!oldOrder.TryGetValue(oldTileId, out index)) index = 0;
                index = Mathf.Clamp(index, 0,
                    replacement.memberTileIds.Count - 1);
                return replacement.memberTileIds[index];
            };

            replacement.startTileId = correspondingArea(current.startTileId);
            replacement.factions = current.factions
                ?.Where(item => item != null).ToList()
                ?? new List<CARegionalFactionPlan>();
            replacement.settlements = current.settlements
                ?.Where(item => item != null).ToList()
                ?? new List<CARegionalSettlementPlan>();
            foreach (CARegionalSettlementPlan place in replacement.settlements)
                place.memberTileId = correspondingArea(place.memberTileId);
            replacement.relations = current.relations
                ?.Where(item => item != null).ToList()
                ?? new List<CARegionalRelationPlan>();
            replacement.worldPolicy = current.worldPolicy?.Copy()
                ?? replacement.worldPolicy;
            if (current.regionNameAuthored)
            {
                replacement.regionName = current.regionName;
                replacement.regionNameAuthored = true;
            }
            replacement.groundwater = current.groundwater
                ?? new CAGroundwaterTuning();
            // Authored feature shapes follow their areas to the new land:
            // each record moves to the corresponding member and survives
            // only where the destination actually carries the same feature.
            replacement.featureShapes = new List<CAAuthoredFeatureShape>();
            foreach (CAAuthoredFeatureShape shape in current.featureShapes
                ?? new List<CAAuthoredFeatureShape>())
            {
                if (shape == null || shape.keys == null
                    || shape.keys.Count == 0) continue;
                int shapeDestination = correspondingArea(shape.tileId);
                PlanetTile shapeTile = CARegionalPlanUtility.SurfaceTile(
                    shapeDestination);
                if (shapeTile.Valid && shapeTile.Tile?.Mutators?.Any(
                        def => def?.defName == shape.feature) == true)
                    replacement.featureShapes.Add(new CAAuthoredFeatureShape
                    {
                        tileId = shapeDestination,
                        feature = shape.feature,
                        keys = shape.keys.ToList(),
                        values = shape.values.ToList()
                    });
            }
            replacement.playerFounding = current.playerFounding?.Copy()
                ?? new CAPlayerFoundingPlan();
            replacement.operatorAuthored = true;
            replacement.consumedSources = current.consumedSources
                ?.ToList() ?? new List<string>();
            replacement.confirmed = false;
            replacement.developerExercise = false;
            replacement.creationSummary = (current.creationSummary
                    ?? "operator-selected connected world-tile bundle")
                + "; moved with authored region intact";
            CARegionalPlanUtility.EnsureRelationRows(replacement);
            CARegionalSettlements.Invalidate(replacement);
            CARegionalSettlements.DeriveSettlementPattern(replacement);

            StickyRegionTileCount = replacement.RequestedRegionTileCount;
            StickyFootprintRotation = replacement.FootprintRotation;
            Pending = replacement;
            previewDerived = null;
            boundPreviewPlan = null;
            ChoosingLandingAnchor = false;
            Verse.Find.WorldInterface.SelectedTile = replacement.StartTile;
            lastObservedSelection = replacement.startTileId;
            SavePending();
            CARegionalCompatibility.NotifyPreviewChanged();
            Messages.Message("Starting region moved; "
                    + replacement.settlements.Count + " settlement"
                    + (replacement.settlements.Count == 1 ? "" : "s")
                    + " and " + replacement.factions.Count + " faction"
                    + (replacement.factions.Count == 1 ? "" : "s")
                    + " kept.", MessageTypeDefOf.TaskCompletion, false);
            Log.Message("[CA][Regional][Selection] moved designed region "
                + current.regionalId + " -> " + replacement.regionalId
                + "; settlements kept " + replacement.settlements.Count
                + "; factions kept " + replacement.factions.Count
                + "; candidate " + replacement.candidateId + "; members "
                + string.Join(",", replacement.memberTileIds));
        }

        // [extent] The preset sizes are direct targets, not only a cycle:
        // more tiles of land means real geography - coast, ridge, differing
        // biome - instead of one large square, and the panel prints the
        // resulting composition so the cost of the choice is visible.
        internal static void ChangeRegionExtentTo(
            CAExpandedLandmassProfile profile, CARegionalPlan current,
            int next)
        {
            if (next == current.RequestedRegionTileCount) return;
            CARegionalPlan replacement = CARegionalPlanUtility.Create(profile,
                current.BundleRoot, true, next, current.FootprintRotation);
            var retainedIds = new HashSet<int>(replacement.memberTileIds);
            bool dropsLanding = !retainedIds.Contains(current.startTileId);
            List<CARegionalSettlementPlan> droppedSettlements = current.settlements.Where(item =>
                    item != null && !retainedIds.Contains(item.memberTileId))
                .ToList();
            if (dropsLanding || droppedSettlements.Count > 0)
            {
                var survivingFactionKeys = new HashSet<int>(current.settlements.Where(
                        item => item != null
                            && retainedIds.Contains(item.memberTileId))
                    .Select(item => item.OwningFactionKey));
                var retainedFactionKeys = new HashSet<int>(survivingFactionKeys);
                foreach (CARegionalFactionPlan group in
                    current.factions.Where(item => item?.authored == true))
                    retainedFactionKeys.Add(group.key);
                int droppedGroups = current.factions.Count(item =>
                    item != null && !item.authored
                        && !survivingFactionKeys.Contains(item.key));
                int droppedRelations = current.relations.Count(item =>
                    item != null && (!retainedFactionKeys.Contains(
                        item.leftFactionKey) || !retainedFactionKeys.Contains(
                        item.rightFactionKey)));
                string consequences = (dropsLanding
                        ? "The current arrival area is outside the smaller "
                            + "region and will move to "
                            + CARegionalPlanUtility.TileWords(
                                replacement.bundleRootTileId) + ". "
                        : "")
                    + (droppedSettlements.Count > 0
                        ? droppedSettlements.Count + " settlement"
                            + (droppedSettlements.Count == 1 ? "" : "s")
                            + " outside the smaller region will be removed."
                        : "")
                    + (droppedGroups > 0 ? " " + droppedGroups + " "
                        + (droppedGroups == 1 ? "faction" : "factions")
                        + " left with no settlement will also be removed."
                        : "")
                    + (droppedRelations > 0 ? " " + droppedRelations
                        + " relationship"
                        + (droppedRelations == 1 ? "" : "s")
                        + " involving those factions will be removed."
                        : "");
                Verse.Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Shrink this region from "
                    + current.RequestedRegionTileCount + " to " + next
                    + " requested areas?\n\n"
                    + consequences,
                    () => ApplyPlanReplacement(profile, current,
                        replacement, "requested size changed to " + next
                            + " areas", true), null, true,
                    "Shrink region"));
                return;
            }
            ApplyPlanReplacement(profile, current, replacement,
                "requested size changed to " + next + " areas");
        }

        // SHAPE AND EXTENT ARE PLAYER-AUTHORED STATE. A shift-click adds
        // the clicked area to the region or removes it, under the same
        // contract validation enforces: connected, unblocked, inside the
        // canonical 2..16 range. Size and Turn stay as presets over the
        // same geometry; a valley, island chain, or coastal strip is
        // composed here, not forced into a stock footprint.
        internal static void ToggleRegionArea(
            CAExpandedLandmassProfile profile, CARegionalPlan current,
            PlanetTile tile)
        {
            bool isMember = current.memberTileIds.Contains(tile.tileId);
            if (!isMember)
            {
                if (CARegionalGeometry.IsBlocked(tile))
                {
                    Messages.Message("Open ocean and impassable mountains "
                        + "cannot join a region.",
                        MessageTypeDefOf.RejectInput, false);
                    return;
                }
                if (current.memberTileIds.Count
                    >= CARegionalGeographyComposition.MaxExtent)
                {
                    Messages.Message("A region holds at most "
                        + CARegionalGeographyComposition.MaxExtent
                        + " areas.", MessageTypeDefOf.RejectInput, false);
                    return;
                }
                var neighbors = new List<PlanetTile>();
                tile.Layer.GetTileNeighbors(tile, neighbors);
                if (!neighbors.Any(neighbor => neighbor.Valid
                    && current.memberTileIds.Contains(neighbor.tileId)))
                {
                    Messages.Message("New areas must touch the region.",
                        MessageTypeDefOf.RejectInput, false);
                    return;
                }
            }
            else
            {
                if (current.memberTileIds.Count
                    <= CARegionalGeographyComposition.MinExtent)
                {
                    Messages.Message("A region keeps at least "
                        + CARegionalGeographyComposition.MinExtent
                        + " areas.", MessageTypeDefOf.RejectInput, false);
                    return;
                }
                List<int> remaining = current.memberTileIds
                    .Where(id => id != tile.tileId).ToList();
                if (!AreasConnected(remaining))
                {
                    Messages.Message("Removing that area would split the "
                        + "region in two.", MessageTypeDefOf.RejectInput,
                        false);
                    return;
                }
            }
            List<int> members = isMember
                ? current.memberTileIds.Where(id => id != tile.tileId)
                    .ToList()
                : current.memberTileIds.Concat(new[] { tile.tileId })
                    .ToList();
            int rootId = members.Contains(current.bundleRootTileId)
                ? current.bundleRootTileId : members[0];
            CARegionalPlan replacement = CARegionalPlanUtility
                .CreateExplicit(profile,
                    CARegionalPlanUtility.SurfaceTile(rootId), members,
                    current.footprintRotation);
            ApplyPlanReplacement(profile, current, replacement,
                (isMember ? "area removed at " : "area added at ")
                    + CARegionalPlanUtility.TileWords(tile.tileId)
                    + "; the region is now " + members.Count + " areas",
                isMember);
        }

        private static bool AreasConnected(List<int> ids)
        {
            if (ids.Count == 0) return false;
            var remaining = new HashSet<int>(ids);
            var reached = new HashSet<int>();
            var queue = new Queue<PlanetTile>();
            PlanetTile first = CARegionalPlanUtility.SurfaceTile(ids[0]);
            if (!first.Valid) return false;
            reached.Add(first.tileId);
            queue.Enqueue(first);
            var neighbors = new List<PlanetTile>();
            while (queue.Count > 0)
            {
                PlanetTile at = queue.Dequeue();
                neighbors.Clear();
                at.Layer.GetTileNeighbors(at, neighbors);
                foreach (PlanetTile neighbor in neighbors)
                    if (neighbor.Valid && remaining.Contains(neighbor.tileId)
                        && reached.Add(neighbor.tileId))
                        queue.Enqueue(neighbor);
            }
            return reached.Count == remaining.Count;
        }

        internal static void RotateRegionFootprint(
            CAExpandedLandmassProfile profile, CARegionalPlan current)
        {
            int next = (current.FootprintRotation + 1) % 6;
            CARegionalPlan replacement = CARegionalPlanUtility.Create(profile,
                current.BundleRoot, true,
                current.RequestedRegionTileCount, next);
            var retainedIds = new HashSet<int>(replacement.memberTileIds);
            bool dropsLanding = !retainedIds.Contains(current.startTileId);
            List<CARegionalSettlementPlan> droppedSettlements = current.settlements.Where(item =>
                    item != null && !retainedIds.Contains(item.memberTileId))
                .ToList();
            string change = "region shape turned to orientation "
                + (next + 1) + "/6";
            if (dropsLanding || droppedSettlements.Count > 0)
            {
                string consequences = (dropsLanding
                        ? "The arrival area will move to "
                            + CARegionalPlanUtility.TileWords(
                                replacement.bundleRootTileId) + ". " : "")
                    + (droppedSettlements.Count > 0
                        ? droppedSettlements.Count + " settlement"
                            + (droppedSettlements.Count == 1 ? "" : "s")
                            + " outside the turned region will be removed."
                        : "");
                Verse.Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Turn this region to orientation "
                    + (next + 1) + "/6?\n\n" + consequences,
                    () => ApplyPlanReplacement(profile, current, replacement,
                        change, true), null, true, "Turn region"));
                return;
            }
            ApplyPlanReplacement(profile, current, replacement, change);
        }

        private static void ApplyPlanReplacement(
            CAExpandedLandmassProfile profile, CARegionalPlan current,
            CARegionalPlan replacement, string changeSummary,
            bool pruneDroppedAuthoring = false)
        {
            int previousSettlements = current.settlements.Count(item => item != null);
            int previousFactions = current.factions.Count(item =>
                item != null);
            int previousRelations = current.relations.Count(item =>
                item != null);
            var retainedIds = new HashSet<int>(replacement.memberTileIds);
            replacement.startTileId = retainedIds.Contains(current.startTileId)
                ? current.startTileId : replacement.bundleRootTileId;
            replacement.factions = current.factions
                .Where(item => item != null).ToList();
            replacement.settlements = current.settlements.Where(item => item != null
                    && retainedIds.Contains(item.memberTileId)).ToList();
            replacement.relations = current.relations
                .Where(item => item != null).ToList();
            if (pruneDroppedAuthoring)
            {
                var usedFactionKeys = new HashSet<int>(replacement.settlements.Select(
                    item => item.OwningFactionKey));
                // Explicitly added factions survive a footprint change even
                // when all of their settlements are removed.
                replacement.factions.RemoveAll(item => item == null
                    || (!item.authored && !usedFactionKeys.Contains(item.key)));
                foreach (CARegionalFactionPlan kept in
                    replacement.factions)
                    if (kept != null) usedFactionKeys.Add(kept.key);
                replacement.relations.RemoveAll(item => item == null
                    || !usedFactionKeys.Contains(item.leftFactionKey)
                    || !usedFactionKeys.Contains(item.rightFactionKey));
                CARegionalPlanUtility.EnsureRelationRows(replacement);
            }
            replacement.worldPolicy = current.worldPolicy?.Copy()
                ?? replacement.worldPolicy;
            if (current.regionNameAuthored)
            {
                replacement.regionName = current.regionName;
                replacement.regionNameAuthored = true;
            }
            replacement.groundwater = current.groundwater
                ?? new CAGroundwaterTuning();
            // Same land, changed footprint: authored feature shapes carry
            // where their area is still a member; a released area takes its
            // authored shape with it.
            var retainedMembers = new HashSet<int>(
                replacement.memberTileIds ?? new List<int>());
            replacement.featureShapes = (current.featureShapes
                    ?? new List<CAAuthoredFeatureShape>())
                .Where(shape => shape != null && shape.keys != null
                    && shape.keys.Count > 0
                    && retainedMembers.Contains(shape.tileId))
                .Select(shape => new CAAuthoredFeatureShape
                {
                    tileId = shape.tileId,
                    feature = shape.feature,
                    keys = shape.keys.ToList(),
                    values = shape.values.ToList()
                }).ToList();
            replacement.playerFounding = current.playerFounding?.Copy()
                ?? new CAPlayerFoundingPlan();
            replacement.consumedSources = current.consumedSources?.ToList()
                ?? new List<string>();
            // A changed physical region is a new candidate. Its design is
            // retained, but it must be confirmed against the new geography.
            replacement.confirmed = false;
            replacement.developerExercise = false;
            replacement.operatorAuthored = current.operatorAuthored;
            replacement.creationSummary = (current.creationSummary
                ?? "operator-selected connected world-tile bundle")
                + "; " + changeSummary;
            CARegionalSettlements.Invalidate(replacement);
            CARegionalSettlements.DeriveSettlementPattern(replacement);
            // Every operator-driven change to the footprint funnels through
            // here, so this is the one place that records extent and
            // orientation somewhere a later plan replacement cannot erase.
            StickyRegionTileCount = replacement.RequestedRegionTileCount;
            StickyFootprintRotation = replacement.footprintRotation;
            Pending = replacement;
            // Size and orientation create a new geographic candidate. Any
            // Map Preview binding from the previous shape must yield to this
            // replacement before the refresh asks for its frame or gensteps.
            previewDerived = null;
            boundPreviewPlan = null;
            ChoosingLandingAnchor = false;
            PlanetTile selectedAfterReplacement =
                Verse.Find.WorldInterface.SelectedTile;
            bool selectionReanchored = !selectedAfterReplacement.Valid
                || selectedAfterReplacement.Layer != Verse.Find.WorldGrid.Surface
                || !retainedIds.Contains(selectedAfterReplacement.tileId);
            if (selectionReanchored)
            {
                Verse.Find.WorldInterface.SelectedTile = replacement.StartTile;
                selectedAfterReplacement = replacement.StartTile;
            }
            lastObservedSelection = selectedAfterReplacement.tileId;
            SavePending();
            Log.Message("[CA][Regional][Selection] applied " + changeSummary
                + "; retained inspected member "
                + selectedAfterReplacement.tileId + "; selection reanchored "
                + selectionReanchored + "; footprint "
                + replacement.regionalId + "; members "
                + string.Join(",", replacement.memberTileIds));
            CARegionalCompatibility.NotifyPreviewChanged();
            int removedSettlements = previousSettlements
                - replacement.settlements.Count(item => item != null);
            int removedFactions = previousFactions
                - replacement.factions.Count(item => item != null);
            int removedRelations = previousRelations
                - replacement.relations.Count(item => item != null);
            string outcome;
            if (removedSettlements <= 0 && removedFactions <= 0
                && removedRelations <= 0)
                outcome = "your factions, settlements, and other decisions "
                    + "were kept.";
            else
            {
                var removed = new List<string>();
                if (removedSettlements > 0)
                    removed.Add(removedSettlements + " settlement"
                        + (removedSettlements == 1 ? "" : "s"));
                if (removedFactions > 0)
                    removed.Add(removedFactions + " faction"
                        + (removedFactions == 1 ? "" : "s"));
                if (removedRelations > 0)
                    removed.Add(removedRelations + " relationship"
                        + (removedRelations == 1 ? "" : "s"));
                outcome = "removed " + string.Join(", ", removed)
                    + " as confirmed; the remaining composition was kept.";
            }
            Messages.Message(changeSummary.CapitalizeFirst() + "; " + outcome,
                MessageTypeDefOf.TaskCompletion, false);
            Log.Message("[CA][Regional] selection topology "
                + CARegionalPlanUtility.SelectionTopologySummary(replacement));
        }
    }

    internal static class CARegionalWorldOverlay
    {
        internal static Color FactionColor(int key)
        {
            if (key < 0)
                return new Color(0.66f, 0.68f, 0.70f);
            return Color.HSVToRGB((key * 0.6180339887f) % 1f, 0.62f,
                0.95f);
        }
    }

    // Map Preview owns its windows; Starting Region owns where those windows
    // fit while this page is open. Keep the details panel stable and dock the
    // preview plus its toolbar into the available map lane. Their saved global
    // positions are restored when the page closes, so CA does not rewrite the
    // other mod's ordinary layout preference.
    internal static class CARegionalPreviewDock
    {
        private static bool captured;
        private static bool toolbarCaptured;
        private static bool dragCaptured;
        private static bool originalDraggable;
        private static Window capturedPreview;
        private static Window capturedToolbar;
        private static Vector2 previewPosition;
        private static Vector2 toolbarPosition;
        private static Vector2 preferredPreviewSize;
        private static Vector2 preferredToolbarSize;
        private static Rect lastDetailsPanel;
        private static bool hasDetailsPanel;

        internal static void Arrange(Rect detailsPanel)
        {
            lastDetailsPanel = detailsPanel;
            hasDetailsPanel = true;
            WindowStack stack = Verse.Find.WindowStack;
            if (stack == null) return;
            Window preview = stack.Windows.FirstOrDefault(window =>
                window?.GetType().FullName == "MapPreview.MapPreviewWindow");
            if (preview == null) return;
            Window toolbar = stack.Windows.FirstOrDefault(window =>
                window?.GetType().FullName == "MapPreview.MapPreviewToolbar");
            if (!captured || !ReferenceEquals(capturedPreview, preview))
            {
                previewPosition = preview.windowRect.position;
                preferredPreviewSize = preview.windowRect.size;
                capturedPreview = preview;
                captured = true;
            }
            // The toolbar may be added one frame after the preview window.
            // Capture it when it actually exists rather than freezing a
            // zero-sized placeholder into the page layout.
            if (toolbar != null && (!toolbarCaptured
                    || !ReferenceEquals(capturedToolbar, toolbar)))
            {
                toolbarPosition = toolbar.windowRect.position;
                preferredToolbarSize = toolbar.windowRect.size;
                capturedToolbar = toolbar;
                toolbarCaptured = true;
            }

            const float margin = 12f;
            const float gap = 10f;
            float leftBound = margin;
            Window inspect = stack.Windows.FirstOrDefault(window =>
                window?.GetType().Name == "WorldInspectPane");
            if (inspect != null)
                leftBound = Mathf.Max(leftBound,
                    inspect.windowRect.xMax + gap);

            // THE PREVIEW IS A FIRST-CLASS VIEW, not a corner thumbnail:
            // fill the lane at the window's region aspect (the window is
            // already aspect-framed to the exact composed backing). The
            // texture carries backing-resolution pixels, so upscaling the
            // window shows more of what was actually generated, and CAO's
            // zoom/pan presentation makes the rest inspectable.
            float rightBound = detailsPanel.x - gap;
            float laneWidth = Mathf.Max(1f, rightBound - leftBound);
            float previewY = toolbar == null ? 64f
                : Mathf.Max(64f, toolbar.windowRect.height + 55f);
            float bottom = UI.screenHeight - 158f;
            float laneHeight = Mathf.Max(1f, bottom - previewY);
            float scale = Mathf.Min(3f, Mathf.Min(laneWidth
                    / Mathf.Max(1f, preferredPreviewSize.x),
                laneHeight / Mathf.Max(1f, preferredPreviewSize.y)));
            Vector2 previewSize = preferredPreviewSize * scale;
            // Docked means CAO owns the geometry; dragging would fight the
            // dock every frame. The original preference is restored when
            // the window leaves the dock.
            if (!dragCaptured)
            {
                originalDraggable = preview.draggable;
                dragCaptured = true;
            }
            preview.draggable = false;
            float x = rightBound - previewSize.x;
            if (x < leftBound)
                x = leftBound;
            x = Mathf.Clamp(x, margin,
                Mathf.Max(margin,
                    UI.screenWidth - previewSize.x - margin));
            if (previewY + previewSize.y > bottom)
                previewY = Mathf.Max(64f,
                    bottom - previewSize.y);
            preview.windowRect = new Rect(x, previewY,
                previewSize.x, previewSize.y);

            if (toolbar == null || !toolbarCaptured) return;
            float toolbarWidth = Mathf.Min(preferredToolbarSize.x, laneWidth);
            float toolbarX = Mathf.Clamp(x, margin,
                Mathf.Max(margin,
                    UI.screenWidth - toolbarWidth - margin));
            float toolbarY = Mathf.Max(8f,
                preview.windowRect.y - toolbar.windowRect.height - 5f);
            toolbar.windowRect = new Rect(toolbarX, toolbarY,
                toolbarWidth, preferredToolbarSize.y);
        }

        internal static void BeforeExternalSelection(Window preview)
        {
            if (captured && preview != null
                && ReferenceEquals(capturedPreview, preview))
            {
                preview.windowRect.position = previewPosition;
                preview.windowRect.size = preferredPreviewSize;
            }
        }

        internal static void AfterExternalSelection()
        {
            if (!captured || !hasDetailsPanel) return;
            WindowStack stack = Verse.Find.WindowStack;
            Window preview = stack?.Windows.FirstOrDefault(window =>
                window?.GetType().FullName == "MapPreview.MapPreviewWindow");
            if (preview != null && ReferenceEquals(capturedPreview, preview))
                preferredPreviewSize = preview.windowRect.size;
            Arrange(lastDetailsPanel);
        }

        // Map Preview persists window positions from PreClose. If either
        // docked window is closed or recreated while this page is still open,
        // put its own undocked geometry back first so CA's temporary layout
        // never becomes that mod's global preference.
        internal static void BeforeExternalPreviewClose(Window preview)
        {
            if (!captured || preview == null
                || !ReferenceEquals(capturedPreview, preview)) return;
            preview.windowRect.position = previewPosition;
            preview.windowRect.size = preferredPreviewSize;
            if (dragCaptured)
            {
                preview.draggable = originalDraggable;
                dragCaptured = false;
            }
            Window toolbar = Verse.Find.WindowStack?.Windows.FirstOrDefault(
                window => window?.GetType().FullName
                    == "MapPreview.MapPreviewToolbar");
            BeforeExternalToolbarClose(toolbar);
            captured = false;
            capturedPreview = null;
        }

        internal static void BeforeExternalToolbarClose(Window toolbar)
        {
            if (!toolbarCaptured || toolbar == null
                || !ReferenceEquals(capturedToolbar, toolbar)) return;
            toolbar.windowRect.position = toolbarPosition;
            toolbar.windowRect.size = preferredToolbarSize;
            toolbarCaptured = false;
            capturedToolbar = null;
        }

        internal static void Release()
        {
            WindowStack stack = Verse.Find.WindowStack;
            Window preview = stack?.Windows.FirstOrDefault(window =>
                window?.GetType().FullName == "MapPreview.MapPreviewWindow");
            Window toolbar = stack?.Windows.FirstOrDefault(window =>
                window?.GetType().FullName == "MapPreview.MapPreviewToolbar");
            if (preview != null && ReferenceEquals(capturedPreview, preview))
            {
                preview.windowRect.position = previewPosition;
                preview.windowRect.size = preferredPreviewSize;
            }
            if (toolbar != null && toolbarCaptured
                && ReferenceEquals(capturedToolbar, toolbar))
            {
                toolbar.windowRect.position = toolbarPosition;
                toolbar.windowRect.size = preferredToolbarSize;
            }
            captured = false;
            toolbarCaptured = false;
            capturedPreview = null;
            capturedToolbar = null;
            hasDetailsPanel = false;
        }
    }

    [HarmonyPatch(typeof(WorldDrawLayer_SelectedTile), "Tile",
        MethodType.Getter)]
    internal static class CARegionalNativeSelectedTilePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref PlanetTile __result)
        {
            if (CARegionalSetupSession.PendingForCurrentWorld == null
                || Verse.Find.WindowStack == null
                || !Verse.Find.WindowStack.IsOpen<Page_SelectStartingSite>())
                return;
            CAExpandedLandmassProfile profile;
            if (CAExpandedLandmassProfile.TryFor(
                    Verse.Find.GameInitData.mapSize, out profile))
                __result = PlanetTile.Invalid;
        }
    }

    internal static class CARegionalPlanResolver
    {
        internal static void Resolve(CARegionalPlan plan)
        {
            if (plan == null || Verse.Find.FactionManager == null) return;
            string identityFailure;
            if (!CARegionalPlanUtility.TryValidateStableIdentities(plan,
                    out identityFailure))
            {
                Log.Error("[CA][Regional] refused faction resolution for "
                    + (plan.regionalId ?? "unknown") + ": "
                    + identityFailure);
                return;
            }
            string factionFailure;
            if (!CARegionalPlanUtility.TryValidateDistinctFactionClaims(plan,
                    out factionFailure))
            {
                Log.Error("[CA][Regional] refused faction resolution for "
                    + (plan.regionalId ?? "unknown") + ": "
                    + factionFailure);
                return;
            }
            foreach (CARegionalFactionPlan group in plan.factions)
            {
                if (group == null) continue;
                Faction faction = group.resolvedFaction;
                if (group.source
                    == CARegionalFactionSource.ExistingWorldFaction)
                {
                    faction = CARegionalPlanUtility.FactionByLoadId(
                        group.existingFactionLoadId);
                    if (!CARegionalPlanUtility.MatchesFaction(faction,
                            group)) faction = null;
                    group.resolvedFactionLoadId = -1;
                }
                else if (!CARegionalPlanUtility.MatchesFaction(faction,
                             group))
                {
                    int priorIdentity = group.resolvedFactionLoadId >= 0
                        ? group.resolvedFactionLoadId
                        : faction?.loadID ?? -1;
                    if (priorIdentity >= 0)
                    {
                        group.resolvedFactionLoadId = priorIdentity;
                        faction = CARegionalPlanUtility.FactionByLoadId(
                            priorIdentity);
                        if (!CARegionalPlanUtility.MatchesFaction(faction,
                                group))
                        {
                            Log.Warning("[CA][Regional] authored faction "
                                + group.key + " retained generated identity "
                                + priorIdentity + " but that faction is no "
                                + "longer eligible; refusing to replace it");
                            faction = null;
                        }
                    }
                    else
                        faction = CreateFaction(group);
                }
                group.resolvedFaction = faction;
                if (group.source == CARegionalFactionSource.NewWorldFaction
                    && faction != null)
                    group.resolvedFactionLoadId = faction.loadID;
                if (group.authorPlayerRelation)
                    ApplyRelation(faction, Faction.OfPlayer,
                        group.playerRelation);
            }
            if (!CARegionalPlanUtility.TryValidateDistinctFactionClaims(plan,
                    out factionFailure))
            {
                Log.Error("[CA][Regional] stopped conflicting faction drafts "
                    + "before durable adoption: " + factionFailure);
                return;
            }

            for (int i = 0; i < plan.factions.Count; i++)
                for (int j = i + 1; j < plan.factions.Count; j++)
                {
                    CARegionalFactionPlan left = plan.factions[i];
                    CARegionalFactionPlan right = plan.factions[j];
                    if (left?.resolvedFaction == null
                        || right?.resolvedFaction == null
                        || left.resolvedFaction == right.resolvedFaction)
                        continue;
                    CARegionalRelationPlan pair = plan.relations
                        .FirstOrDefault(item => item != null
                            && ((item.leftFactionKey == left.key
                                && item.rightFactionKey == right.key)
                                || (item.leftFactionKey == right.key
                                    && item.rightFactionKey == left.key)));
                    if (pair == null) continue;
                    // The validated persisted row is authoritative. Its source
                    // records whether this fact came from existing world state,
                    // native faction initialization, or Starting Region.
                    ApplyRelation(left.resolvedFaction,
                        right.resolvedFaction,
                        pair.relation);
                }

            // Confirmed Culture, Political Order, Technological Knowledge,
            // and represented institutions become durable faction state.
            // Confirmation owns completion; runtime resolution is a
            // projection and may not rewrite the canonical regional plan.
            // Native Ideo remains separate.
            foreach (CARegionalFactionPlan group in plan.factions)
            {
                if (group?.resolvedFaction == null) continue;
                CAFactionStartingState.ApplyPlan(group.resolvedFaction,
                    group.culture, group.politicalBeliefs,
                    group.technologicalKnowledge,
                    group.factionStructure,
                    group.institutionalStateIncomplete);
            }

            // Fill missing state for established humanlike factions. Native
            // Ideoligion remains unchanged. The player's carried state is
            // staged by the founding page and applied at the one-shot game
            // start boundary; regional resolution does not own or replay it.
            Log.Message(CAFactionStateGenerator.RunWorldPass(
                "region " + (plan.regionalId ?? "unknown")));
        }

        private static Faction CreateFaction(CARegionalFactionPlan group)
        {
            FactionDef def = CARegionalPlanUtility.FactionDefByName(
                group.customFactionDefName);
            if (!CARegionalPlanUtility.CanMaterializeSettlement(def))
            {
                Log.Warning("[CA][Regional] authored faction "
                    + group.key + " has no settlement-capable faction "
                    + "template");
                return null;
            }
            // Build Ideo generation parameters from the FactionDef so fixed
            // Ideoligions, memes, styles, and descriptions survive.
            var parms = new FactionGeneratorParms(def,
                new IdeoGenerationParms(def), true);
            Faction faction = FactionGenerator.NewGeneratedFaction(
                Verse.Find.WorldGrid.Surface, parms);
            faction.hidden = !group.visibleInWorld;
            if (!group.customName.NullOrEmpty()) faction.Name = group.customName;
            Verse.Find.FactionManager.Add(faction);
            Log.Message("[CA][Regional] created authored world faction "
                + faction.Name + " from " + def.defName + "; visible "
                + group.visibleInWorld);
            return faction;
        }

        private static void ApplyRelation(Faction left, Faction right,
            FactionRelationKind kind)
        {
            if (left == null || right == null || left == right) return;
            int goodwill = kind == FactionRelationKind.Hostile ? -100
                : kind == FactionRelationKind.Ally ? 100 : 0;
            left.SetRelation(new FactionRelation(right, kind)
            {
                baseGoodwill = goodwill
            });
            FactionRelation mirror = right.RelationWith(left);
            if (mirror != null)
            {
                mirror.kind = kind;
                mirror.baseGoodwill = goodwill;
            }
        }
    }

    internal enum CALittoralFormation : byte
    {
        None,
        VanillaBeach
    }

    // The radial lake family the regional projection re-expresses per
    // carrier. Lava basins are mirrored radially without the native
    // largest-island flood-fill cleanup, whose touches-map-edge test has no
    // meaning at carrier scale -- small islands inside a regional lava lake
    // survive where the native pass would have drowned them. Oasis's
    // native anywhere-on-the-map Rand center becomes the same
    // deterministic wander the rest of the family uses.
    internal enum CAInlandWaterKind : byte
    {
        None,
        Lake,
        Toxic,
        Pond,
        DryLake,
        LakeWithIsland,
        LavaLake,
        LavaCrater,
        // Basin's water: a .3 basin exactly at the carrier's anchor (its
        // native center picker is the map center, never offset). The
        // raised rim and entrance corridors are a separate elevation pass.
        Basin,
        LakeWithIslands,
        Oasis
    }

    // Map-generation consumer of the shared projection kernel. Ownership,
    // biome, coast, water depth, littoral formation, hill blend, and
    // constituent frames come from the same kernel as the setup preview.
    // This component retains work that requires an instantiated Map:
    // MapGenerator.Elevation/Fertility/Caves writes, the projected Mountain
    // field and its fixture receipt, native cave-worker dispatch,
    // MapGenUtility.BeachTerrainAt, and the rock-type cache keyed off the
    // world's natural rock lookup.
    public sealed class CARegionalProjectionMapComponent : MapComponent
    {
        private CARegionalProjectionKernel kernel;
        private CARegionalPlan region;
        // Aliases of the kernel's arrays, assigned once in EnsureBuilt. They
        // exist so that every map-bound consumer below reads exactly the
        // expression it read before the kernel was extracted.
        private int[] memberByCell;
        private int[] nearestLandMemberByCell;
        private int[] boundaryWaterByCell;
        private BiomeDef[] rawBiomeByCell;
        private ushort[] waterDepth;
        private float[] coastValueByCell;
        private byte[] littoralFormationByCell;
        private float[] hillFactorByCell;
        private float[] lakeValueByCell;
        private byte[] lakeKindByCell;
        private float[] bergValueByCell;
        private List<PlanetTile> members;
        private List<float> memberHillFactors;
        private List<PlanetTile> boundaryWaterTiles;
        // Map-bound: the projected Mountain field writes MapGenerator
        // elevation and is measured against the F-131 fixture, so it stays
        // with the consumer that owns those.
        private float[] mountainAddByCell;
        private List<CAMountainCarrierMeasure> mountainCarriers;
        private int mountainRockInsideOwnership;
        private int mountainRockOutsideOwnership;
        // Natural geology is fixed per world tile. RockDefAt is called once
        // per generated cell, so rebuilding its list (and then a HashSet) at
        // every call created millions of avoidable allocations on regional
        // maps. Cached lists are treated as read-only by the two local
        // consumers below.
        private static readonly List<ThingDef> EmptyRockTypes =
            new List<ThingDef>(0);
        private readonly Dictionary<int, List<ThingDef>> rockTypesByTileId =
            new Dictionary<int, List<ThingDef>>();

        public CARegionalProjectionMapComponent(Map map) : base(map) { }

        // Native ticks only the anchor tile's mutator workers (Map.MapTick
        // iterates TileInfo.Mutators). Member-carried mutators tick here,
        // once per def like the native loop, so a member's lava flow
        // schedules its eruptions. The only ticking worker in the catalog
        // (LavaFlow) reads no Init-time state, so an un-Inited member def
        // is safe; the guard keeps a future modded ticker from breaking
        // the map tick.
        private List<TileMutatorDef> memberOnlyTickers;

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!Active) return;
            if (memberOnlyTickers == null)
            {
                memberOnlyTickers = new List<TileMutatorDef>();
                var anchorDefs = new HashSet<TileMutatorDef>(
                    map.TileInfo.Mutators);
                for (int i = 0; i < members.Count; i++)
                {
                    if (!IsSelectedCore(members[i])) continue;
                    Tile info = members[i].Valid ? members[i].Tile : null;
                    if (info == null) continue;
                    foreach (TileMutatorDef mutator in info.Mutators)
                        if (mutator?.Worker != null
                            && !anchorDefs.Contains(mutator)
                            && !memberOnlyTickers.Contains(mutator))
                            memberOnlyTickers.Add(mutator);
                }
            }
            for (int i = 0; i < memberOnlyTickers.Count; i++)
            {
                try { memberOnlyTickers[i].Worker?.Tick(map); }
                catch (Exception failure)
                {
                    Log.ErrorOnce("[CA][Regional] member mutator tick "
                        + memberOnlyTickers[i].defName + ": " + failure,
                        Gen.HashCombineInt(
                            memberOnlyTickers[i].shortHash, 0x4D544B54));
                }
            }
        }

        internal bool Active
        {
            get
            {
                EnsureBuilt();
                return region?.memberTileIds != null
                    && region.memberTileIds.Count > 1
                    && memberByCell != null && memberByCell.Length > 0;
            }
        }

        internal CARegionalPlan Region
        {
            get { EnsureBuilt(); return region; }
        }

        // Region-level consumers (incidents have no target cell) still need a
        // principled way to aggregate selected spatial features. Visible land
        // area is the representation: it weights a carrier by the geography
        // that actually resolved on this map, not by an arbitrary tile-list
        // order or by the bookkeeping anchor.
        internal int ProjectedAreaFor(PlanetTile tile)
        {
            EnsureBuilt();
            if (!tile.Valid || kernel?.Members == null) return 0;
            int index = kernel.Members.FindIndex(member =>
                member.tileId == tile.tileId);
            if (index < 0 || memberByCell == null) return 0;
            int visibleLand = 0;
            for (int cellIndex = 0; cellIndex < memberByCell.Length;
                cellIndex++)
                if (memberByCell[cellIndex] == index
                    && kernel.IsVisualLandAtIndex(cellIndex))
                    visibleLand++;
            return visibleLand;
        }

        internal BiomeDef BiomeAt(IntVec3 cell)
        {
            EnsureBuilt();
            if (region == null || !cell.InBounds(map)) return map.Biome;
            return BiomeAtIndex(map.cellIndices.CellToIndex(cell));
        }

        private BiomeDef BiomeAtIndex(int index)
        {
            if (rawBiomeByCell != null && index < rawBiomeByCell.Length)
                return rawBiomeByCell[index] ?? map.Biome;
            return map.Biome;
        }

        internal PlanetTile MemberTileAt(IntVec3 cell)
        {
            EnsureBuilt();
            if (region == null || !cell.InBounds(map)) return PlanetTile.Invalid;
            int index = memberByCell[map.cellIndices.CellToIndex(cell)];
            return index >= 0 && index < members.Count
                ? members[index] : PlanetTile.Invalid;
        }

        // The projection kernel includes a narrow geographic halo so coast,
        // roads and rivers meet the selected land naturally. That halo is not
        // authored feature authority: compatibility evaluates the selected
        // footprint, and every mutator consumer uses this same boundary.
        internal bool IsSelectedCore(PlanetTile tile)
        {
            EnsureBuilt();
            return tile.Valid && region?.memberTileIds != null
                && region.memberTileIds.Contains(tile.tileId);
        }

        internal IntVec3 CenterForMember(int memberTileId)
        {
            EnsureBuilt();
            if (region == null || kernel == null) return map.Center;
            return kernel.CenterForMember(memberTileId);
        }

        internal IntVec3 ProjectTile(PlanetTile tile)
        {
            EnsureBuilt();
            return region == null || kernel == null || !tile.Valid
                ? map.Center : kernel.ProjectCenter(tile, false);
        }

        internal Vector2 ProjectTilePoint(PlanetTile tile)
        {
            EnsureBuilt();
            return region == null || kernel == null || !tile.Valid
                ? new Vector2(map.Center.x, map.Center.z)
                : kernel.ProjectPoint(tile);
        }

        internal IEnumerable<int> GeographicSourceTileIds
        {
            get
            {
                EnsureBuilt();
                return kernel == null ? Enumerable.Empty<int>()
                    : kernel.GeographicSourceTileIds;
            }
        }

        internal string ProjectionFrameSummary
        {
            get
            {
                EnsureBuilt();
                return kernel == null ? "no projection frame"
                    : kernel.ProjectionFrameSummary;
            }
        }

        internal float WorldNoiseAt(IntVec3 cell, float frequency, int salt)
        {
            EnsureBuilt();
            return region == null || kernel == null ? 0.5f
                : kernel.SampleWorldNoise(cell.x, cell.z, frequency, salt);
        }

        internal float WorldLinkNoiseAt(PlanetTile from, PlanetTile to,
            float progress, float frequency, int salt)
        {
            EnsureBuilt();
            if (region == null || kernel == null || !from.Valid
                || !to.Valid) return 0.5f;
            Vector3 fromDirection = Verse.Find.WorldGrid.GetTileCenter(from)
                .normalized;
            Vector3 toDirection = Verse.Find.WorldGrid.GetTileCenter(to)
                .normalized;
            float cellsPerRadian = kernel.ProjectionScale * Mathf.Rad2Deg;
            Vector3 point = Vector3.Slerp(fromDirection, toDirection,
                Mathf.Clamp01(progress)) * cellsPerRadian;
            return CARegionalProjectionKernel.SampleWorldNoisePoint(point,
                frequency, salt);
        }

        internal ushort WaterDepthAt(IntVec3 cell)
        {
            EnsureBuilt();
            if (waterDepth == null || !cell.InBounds(map)) return 0;
            return waterDepth[map.cellIndices.CellToIndex(cell)];
        }

        internal float CoastValueAt(IntVec3 cell)
        {
            EnsureBuilt();
            if (coastValueByCell == null || !cell.InBounds(map)) return 1f;
            return coastValueByCell[map.cellIndices.CellToIndex(cell)];
        }

        internal CALittoralFormation LittoralFormationAt(IntVec3 cell)
        {
            EnsureBuilt();
            if (littoralFormationByCell == null || !cell.InBounds(map))
                return CALittoralFormation.None;
            return (CALittoralFormation)littoralFormationByCell[
                map.cellIndices.CellToIndex(cell)];
        }

        internal TerrainDef LittoralTerrainAt(IntVec3 cell)
        {
            CALittoralFormation formation = LittoralFormationAt(cell);
            if (formation == CALittoralFormation.None) return null;
            // This is RimWorld's own terrain-selection seam. It respects a
            // local biome's coastalBeachTerrain and any active mutator
            // override instead of manufacturing a CA-only sand or rock type.
            // A shore whose water is a lake takes the native lakeshore
            // treatment, not ocean beach -- the same distinction the
            // water-terrain painter already draws for the water itself.
            if (kernel?.NearestBoundaryWaterByCell != null
                && kernel.BoundaryWaterTiles != null)
            {
                int index = map.cellIndices.CellToIndex(cell);
                int nearest = index >= 0 && index
                    < kernel.NearestBoundaryWaterByCell.Length
                    ? kernel.NearestBoundaryWaterByCell[index] : -1;
                if (nearest >= 0
                    && nearest < kernel.BoundaryWaterTiles.Count
                    && kernel.BoundaryWaterTiles[nearest].Tile
                        ?.PrimaryBiome != BiomeDefOf.Ocean)
                    return MapGenUtility.LakeshoreTerrainAt(cell, map);
            }
            return MapGenUtility.BeachTerrainAt(cell, map);
        }

        internal int LittoralSandCells
        {
            get { EnsureBuilt(); return kernel?.LittoralSandCells ?? 0; }
        }

        internal int NativeShallowWaterDepth
        {
            get
            {
                EnsureBuilt();
                return region == null ? 1 : Math.Max(1,
                    Mathf.CeilToInt(region.mapSize * 0.10f));
            }
        }

        internal int ProjectedSourceCount
        {
            get
            {
                EnsureBuilt();
                return kernel?.GeographicLandTiles?.Count
                    ?? members?.Count ?? 0;
            }
        }

        internal string DiagnosticSummary
        {
            get
            {
                EnsureBuilt();
                return kernel == null ? "no projection"
                    : kernel.DiagnosticSummary;
            }
        }

        internal string PerformanceSummary
        {
            get
            {
                EnsureBuilt();
                return kernel?.PerformanceSummary ?? "unmeasured";
            }
        }

        internal float HillFactorAt(IntVec3 cell)
        {
            EnsureBuilt();
            if (hillFactorByCell == null || !cell.InBounds(map)) return 1f;
            return hillFactorByCell[map.cellIndices.CellToIndex(cell)];
        }

        internal void ApplyTerrainProjection(float rootFactor)
        {
            EnsureBuilt();
            if (region == null || memberByCell == null) return;
            float safeRootFactor = Math.Max(0.01f, rootFactor);
            for (int index = 0; index < memberByCell.Length; index++)
            {
                int memberIndex = memberByCell[index];
                if (memberIndex < 0 || memberIndex >= members.Count) continue;
                IntVec3 cell = map.cellIndices.IndexToCell(index);
                if (BiomeAtIndex(index)?.isWaterBiome == true)
                {
                    MapGenerator.Elevation[cell] = 0f;
                    MapGenerator.Fertility[cell] = 0f;
                    MapGenerator.Caves[cell] = 0f;
                }
                else
                {
                    float ratio = hillFactorByCell[index] / safeRootFactor;
                    MapGenerator.Elevation[cell] = Mathf.Clamp01(
                        MapGenerator.Elevation[cell] * ratio);
                }
            }
        }

        // Adds the projected mountain field. Runs where the native mutator
        // would have run (GenStep_MutatorPostElevationFertility, order 20), so
        // the regional hill-factor rescale at order 190 still scales it and
        // RocksFromGrid at 200 still reads the result. Native caps the sum only
        // on a water-covered tile; regional water cells are zeroed by
        // ApplyTerrainProjection, so no cap is applied here.
        internal CAConstituentFrameSet Frames
        {
            get { EnsureBuilt(); return kernel?.Frames; }
        }

        // Each mountain carrier evaluates the native field in its own frame,
        // anchored on the actual centroid of its cells and oriented by that
        // area's coast angle. Its support follows geographic ownership and may
        // cross a boundary only as a bounded seam:
        //
        //   support = 1                       on cells the carrier owns
        //           = smoothstep 1 -> 0       across seamWidth cells outside
        //           = 0                       beyond that, exactly
        //
        // Carriers combine with max, so adjacent carriers form one continuous
        // range without adding into a new plateau. Support decays continuously
        // past the border and reaches exactly zero at the far bound.
        //
        // The ramp width is the carrier's span along its own rotated
        // Mountain axis, measured over the cells it owns. Seam width follows
        // the carrier's geometry and is bounded by the per-area source scale.
        private void EnsureMountainField()
        {
            if (mountainAddByCell != null) return;
            CAConstituentFrameSet constituentFrames = Frames;
            mountainAddByCell = new float[map.cellIndices.NumGridCells];
            mountainCarriers = new List<CAMountainCarrierMeasure>();
            if (constituentFrames == null || members == null
                || memberByCell == null || kernel == null) return;

            var carriers = new List<int>();
            for (int i = 0; i < members.Count; i++)
                if (IsSelectedCore(members[i])
                    && CarriesMountain(members[i])) carriers.Add(i);
            if (carriers.Count == 0) return;

            const int noiseSpacing = 8;
            var displaceX = kernel.CreateWorldAnchoredNoiseField(noiseSpacing,
                (x, z) => (kernel.SampleWorldNoise(x, z, 0.003f, 143263)
                    * 2f - 1f) * 20f);
            var displaceZ = kernel.CreateWorldAnchoredNoiseField(noiseSpacing,
                (x, z) => (kernel.SampleWorldNoise(x, z, 0.003f, 143273)
                    * 2f - 1f) * 20f);

            var distance = new int[map.cellIndices.NumGridCells];
            foreach (int i in carriers)
            {
                CAConstituentFrame frame = constituentFrames[i];
                var instance = new CAFeatureInstance(region.candidateId, i,
                    members[i].tileId, "Mountain", 0);
                float degrees = MountainAxisDegrees(members[i], instance);
                float cos = Mathf.Cos(degrees * Mathf.Deg2Rad);
                float sin = Mathf.Sin(degrees * Mathf.Deg2Rad);

                // The carrier's true reach along its own Mountain axis.
                float axisMin = float.MaxValue;
                float axisMax = float.MinValue;
                for (int index = 0; index < memberByCell.Length; index++)
                {
                    if (memberByCell[index] != i) continue;
                    IntVec3 owned = map.cellIndices.IndexToCell(index);
                    float projected = frame.RotatedLocalX(owned.x, owned.z,
                        cos, sin);
                    if (projected < axisMin) axisMin = projected;
                    if (projected > axisMax) axisMax = projected;
                }
                float axisSpan = axisMax > axisMin ? axisMax - axisMin : 0f;
                float width = axisSpan > 1f ? axisSpan
                    : Math.Max(1f, frame.CharacteristicWidth);
                float ramp = Math.Max(1f, width * 0.30f);
                float offset = width * 0.20f;

                // Constituent scale, never the aggregate frame. The carrier's
                // own equal-area diameter sets it; the per-tile source scale
                // caps it, so a large stitched region cannot widen the seam.
                float seamWidth = Mathf.Clamp(
                    frame.CharacteristicWidth * 0.10f, 8f,
                    region.mapSize * 0.25f);
                int seamCells = Mathf.Max(1, Mathf.CeilToInt(seamWidth));

                List<int> band = BoundedDistanceFromOwnership(i, seamCells,
                    distance);
                int insideCells = 0;
                int outsideCells = 0;
                float peak = 0f;
                foreach (int index in band)
                {
                    IntVec3 cell = map.cellIndices.IndexToCell(index);
                    float px = cell.x + displaceX.Sample(cell.x, cell.z);
                    float pz = cell.z + displaceZ.Sample(cell.x, cell.z);
                    float xRot = frame.RotatedLocalX(px, pz, cos, sin);
                    float value = Mathf.Clamp01(
                        0.5f + (xRot - offset) / ramp);
                    if (value <= 0f) continue;
                    int steps = distance[index];
                    if (steps > 0)
                    {
                        // Smoothstep across the seam band, zero beyond it.
                        float t = Mathf.Clamp01(1f - (float)steps / seamCells);
                        value *= t * t * (3f - 2f * t);
                    }
                    if (value <= 0.0001f) continue;
                    if (steps == 0) insideCells++; else outsideCells++;
                    if (value > peak) peak = value;
                    // MAX, never a sum: adjacent carriers form one continuous
                    // range and can never compound into a higher plateau than
                    // the strongest single carrier produces on its own.
                    if (value > mountainAddByCell[index])
                        mountainAddByCell[index] = value;
                }

                mountainCarriers.Add(new CAMountainCarrierMeasure
                {
                    FrameIndex = i,
                    SourceTileId = members[i].tileId,
                    OwnedCells = frame.Area,
                    AxisDegrees = degrees,
                    AxisSpan = axisSpan,
                    CharacteristicWidth = frame.CharacteristicWidth,
                    SeamWidth = seamCells,
                    AddCellsInside = insideCells,
                    AddCellsOutside = outsideCells,
                    PeakAdd = peak
                });
            }
        }

        // Multi-source BFS outward from one carrier's owned cells, stopping at
        // `maxSteps`. Returns every cell within the band, with `distance`
        // holding the step count (0 = owned by the carrier). This is what makes
        // "zero contribution beyond the seam" exact rather than asymptotic: a
        // cell outside the band is never visited at all.
        private List<int> BoundedDistanceFromOwnership(int frameIndex,
            int maxSteps, int[] distance)
        {
            var visited = new List<int>();
            var frontier = new List<int>();
            for (int index = 0; index < memberByCell.Length; index++)
            {
                distance[index] = int.MaxValue;
                if (memberByCell[index] != frameIndex) continue;
                distance[index] = 0;
                frontier.Add(index);
                visited.Add(index);
            }
            for (int step = 1; step <= maxSteps && frontier.Count > 0; step++)
            {
                var next = new List<int>();
                foreach (int index in frontier)
                {
                    IntVec3 cell = map.cellIndices.IndexToCell(index);
                    for (int direction = 0; direction < 8; direction++)
                    {
                        IntVec3 neighbor = cell + GenAdj.AdjacentCells[
                            direction];
                        if (!neighbor.InBounds(map)) continue;
                        int neighborIndex = map.cellIndices.CellToIndex(
                            neighbor);
                        if (distance[neighborIndex] <= step) continue;
                        distance[neighborIndex] = step;
                        next.Add(neighborIndex);
                        visited.Add(neighborIndex);
                    }
                }
                frontier = next;
            }
            return visited;
        }

        internal int ApplyProjectedMountains()
        {
            EnsureBuilt();
            EnsureMountainField();
            if (region == null || mountainAddByCell == null) return 0;
            MapGenFloatGrid elevation = MapGenerator.Elevation;
            var carrierOwned = new HashSet<int>();
            if (mountainCarriers != null)
                foreach (CAMountainCarrierMeasure measure in mountainCarriers)
                    carrierOwned.Add(measure.FrameIndex);
            int raised = 0;
            mountainRockInsideOwnership = 0;
            mountainRockOutsideOwnership = 0;
            for (int index = 0; index < mountainAddByCell.Length; index++)
            {
                float add = mountainAddByCell[index];
                if (add <= 0.0001f) continue;
                IntVec3 cell = map.cellIndices.IndexToCell(index);
                // Counted here and nowhere else: this is the only point at
                // which the elevation before the mountain add is still
                // available, so "pushed above the rock threshold" is measured
                // rather than inferred from the add value.
                float before = elevation[cell];
                float after = before + add;
                elevation[cell] = after;
                raised++;
                if (before <= RockThreshold && after > RockThreshold)
                {
                    if (memberByCell != null
                        && carrierOwned.Contains(memberByCell[index]))
                        mountainRockInsideOwnership++;
                    else mountainRockOutsideOwnership++;
                }
            }
            return raised;
        }

        // MapGenCavesUtility and GenStep_ElevationFertility both treat rock as
        // elevation above 0.7; the fixture receipt reports against the same
        // number rather than a private one.
        private const float RockThreshold = 0.7f;

        // The measurement the Mountain fixture is accepted or rejected on.
        // Every quantity is counted against actual carrier ownership, because
        // the defect was precisely that contribution escaped ownership - a
        // receipt that only reported totals would have shown nothing wrong.
        internal string MountainFixtureReceipt()
        {
            EnsureBuilt();
            EnsureMountainField();
            var text = new System.Text.StringBuilder();
            int total = map.cellIndices.NumGridCells;
            text.AppendLine("[CA][Regional][Mountain] fixture receipt - region "
                + (region?.regionalId ?? "none") + ", candidate "
                + (region?.candidateId ?? "none") + ", anchor "
                + (region?.bundleRootTileId ?? -1) + ", landing "
                + (region?.startTileId ?? -1));
            text.AppendLine("  map " + map.Size.x + "x" + map.Size.z + " = "
                + total + " cells; " + (members?.Count ?? 0)
                + " constituents");
            if (mountainCarriers == null || mountainCarriers.Count == 0)
            {
                text.AppendLine("  NO Mountain carriers on this footprint. The "
                    + "Mountain adapter contributes nothing here, so any rock "
                    + "present comes from base elevation - the per-cell "
                    + "hilliness factor - and not from this path (see F-131).");
                text.AppendLine(HillFactorSpreadLine());
                return text.ToString();
            }

            int ownedByCarriers = 0;
            int insideAdd = 0;
            int outsideAdd = 0;
            foreach (CAMountainCarrierMeasure measure in mountainCarriers)
            {
                ownedByCarriers += measure.OwnedCells;
                insideAdd += measure.AddCellsInside;
                outsideAdd += measure.AddCellsOutside;
                text.AppendLine("  carrier tile " + measure.SourceTileId
                    + " (frame " + measure.FrameIndex + "): owns "
                    + measure.OwnedCells + " cells; axis "
                    + measure.AxisDegrees.ToString("F1") + " deg; rotated axis "
                    + "span " + measure.AxisSpan.ToString("F0")
                    + " cells (equal-area surrogate "
                    + measure.CharacteristicWidth.ToString("F0")
                    + ", overstates by "
                    + (measure.AxisSpan > 0.5f
                        ? (100f * (measure.CharacteristicWidth
                            - measure.AxisSpan) / measure.AxisSpan)
                            .ToString("F0") + "%"
                        : "n/a")
                    + "); seam band " + measure.SeamWidth
                    + " cells; add inside ownership " + measure.AddCellsInside
                    + ", outside " + measure.AddCellsOutside + "; peak add "
                    + measure.PeakAdd.ToString("F3"));
            }

            int maxOutside = 0;
            int landCells = 0;
            int rockCells = 0;
            var carrierOwned = new HashSet<int>();
            foreach (CAMountainCarrierMeasure measure in mountainCarriers)
                carrierOwned.Add(measure.FrameIndex);
            for (int index = 0; index < total; index++)
            {
                if (BiomeAtIndex(index)?.isWaterBiome != true) landCells++;
                if (mountainAddByCell[index] >= RockThreshold) rockCells++;
            }
            foreach (CAMountainCarrierMeasure measure in mountainCarriers)
                if (measure.SeamWidth > maxOutside)
                    maxOutside = measure.SeamWidth;

            text.AppendLine("  cells owned by Mountain carriers "
                + ownedByCarriers + " (" + (100f * ownedByCarriers
                    / Math.Max(1, total)).ToString("F1") + "% of map)");
            text.AppendLine("  Mountain-add cells INSIDE carrier ownership "
                + insideAdd + "; OUTSIDE " + outsideAdd
                + " (outside/inside " + (insideAdd > 0
                    ? (100f * outsideAdd / insideAdd).ToString("F1") + "%"
                    : "n/a") + ")");
            text.AppendLine("  maximum distance outside ownership receiving "
                + "contribution " + maxOutside + " cells (hard bound; a cell "
                + "beyond the seam band is never evaluated)");
            text.AppendLine("  pushed above rock threshold " + RockThreshold
                .ToString("F2") + ": inside ownership "
                + mountainRockInsideOwnership + ", outside "
                + mountainRockOutsideOwnership
                + (mountainRockInsideOwnership == 0
                    && mountainRockOutsideOwnership == 0
                    ? "  (zero means ApplyProjectedMountains has not run yet "
                        + "on this map - regenerate to populate)" : ""));
            text.AppendLine("  Mountain field at or above rock threshold "
                + rockCells + " cells = " + (100f * rockCells
                    / Math.Max(1, landCells)).ToString("F1")
                + "% of the island's " + landCells + " land cells");
            text.AppendLine(HillFactorSpreadLine());
            return text.ToString();
        }

        // F-131 also recorded this bounded leak. It is reported separately so
        // the decision to treat it
        // the same way can be made from data. MapGenTuning spans 0.8 (Flat) to
        // 1.1 (Mountains), so any spread here is capped at a 1.375x elevation
        // lift and cannot by itself produce solid rock.
        private string HillFactorSpreadLine()
        {
            if (hillFactorByCell == null || memberByCell == null
                || memberHillFactors == null)
                return "  hill-factor spread: unavailable";
            float worst = 0f;
            int lifted = 0;
            for (int index = 0; index < hillFactorByCell.Length; index++)
            {
                int owner = memberByCell[index];
                if (owner < 0 || owner >= memberHillFactors.Count) continue;
                float own = memberHillFactors[owner];
                float blended = hillFactorByCell[index];
                float excess = blended - own;
                if (excess > 0.0001f) lifted++;
                if (excess > worst) worst = excess;
            }
            return "  hill-factor blend (F-131, separate and NOT fixed here): "
                + lifted + " cells read a hilliness factor above their OWN "
                + "constituent's, worst excess " + worst.ToString("F3")
                + " on a 0.80-1.10 scale";
        }

        // Caves carry no anchoring defect: MapGenCavesUtility.GenerateCaves
        // walks map.AllCells, flood-fills each connected rock group through the
        // isRock predicate, and digs tunnels inside it. The predicate is
        // elevation > 0.7, so caves already follow rock wherever rock is. The
        // regional defect is only about whether the pass runs. Natively both
        // Worker.Init (MapGenerator.cs:141-143) and the genstep iterate the
        // root tile's mutators alone, so a cave-bearing member generates
        // nothing, and a cave-bearing root floods every rock group on the map
        // including members that carry no cave def.
        //
        // Fix: run the native worker once per carried cave def, with the
        // elevation grid temporarily masked so rock owned by members without
        // that def reads as non-rock. The flood fill then stops at the
        // boundary on its own. A rock ridge spanning a cave tile and a
        // non-cave tile is deliberately split - the def is a per-tile fact the
        // player can read off the world map, so it stays one.
        internal string ApplyProjectedCaves()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null)
                return "inactive";
            var carriers = new Dictionary<TileMutatorDef, HashSet<int>>();
            for (int i = 0; i < members.Count; i++)
            {
                if (!IsSelectedCore(members[i])) continue;
                Tile info = members[i].Valid ? members[i].Tile : null;
                if (info == null) continue;
                foreach (TileMutatorDef mutator in info.Mutators)
                {
                    if (!(mutator?.Worker is TileMutatorWorker_Caves)) continue;
                    HashSet<int> owners;
                    if (!carriers.TryGetValue(mutator, out owners))
                    {
                        owners = new HashSet<int>();
                        carriers[mutator] = owners;
                    }
                    owners.Add(i);
                }
            }
            if (carriers.Count == 0) return "no cave-bearing source tiles";

            MapGenFloatGrid elevation = MapGenerator.Elevation;
            var receipts = new List<string>();
            var maskedIndices = new List<int>();
            var maskedValues = new List<float>();
            foreach (KeyValuePair<TileMutatorDef, HashSet<int>> carried in
                carriers.OrderBy(entry => entry.Key.defName))
            {
                maskedIndices.Clear();
                maskedValues.Clear();
                for (int index = 0; index < memberByCell.Length; index++)
                {
                    int owner = memberByCell[index];
                    if (owner >= 0 && carried.Value.Contains(owner)) continue;
                    IntVec3 cell = map.cellIndices.IndexToCell(index);
                    float value = elevation[cell];
                    if (value <= 0.7f) continue;
                    maskedIndices.Add(index);
                    maskedValues.Add(value);
                    elevation[cell] = 0f;
                }
                // A generation-local worker instance per carried def, so the
                // per-Def singleton's fields cannot be clobbered by another
                // constituent driving the same def. Init is what creates
                // directionNoise; natively it runs for root mutators only, so
                // a member-only cave def would otherwise arrive with a null
                // field or one left from an earlier map.
                // One execution per carried def across all its carrying
                // constituents, so the instance is keyed to the region's root
                // tile, not to whichever carrier happens to sort first - that
                // would move the identity if member ordering ever changed.
                // Identity comes from the carrier set, not the landing tile.
                // Min() of the carrying members is stable under both a landing
                // change and an engine-root change.
                int firstOwner = carried.Value.Min();
                int firstOwnerTileId = members[firstOwner].tileId;
                var instance = new CAFeatureInstance(region.candidateId,
                    firstOwner, firstOwnerTileId, carried.Key.defName, 0);
                TileMutatorWorker worker =
                    CAPrivateWorkers.For(carried.Key, instance);
                CAFeatureRandom.Push(instance, 0x43415645);
                worker.Init(map);
                worker.GeneratePostElevationFertility(map);
                CAFeatureRandom.Pop();
                for (int i = 0; i < maskedIndices.Count; i++)
                    elevation[map.cellIndices.IndexToCell(maskedIndices[i])] =
                        maskedValues[i];
                receipts.Add(carried.Key.defName + " on "
                    + carried.Value.Count + " source tiles, "
                    + maskedIndices.Count + " foreign rock cells masked");
            }
            int caveCells = 0;
            MapGenFloatGrid caves = MapGenerator.Caves;
            foreach (IntVec3 cell in map.AllCells)
                if (caves[cell] > 0f) caveCells++;
            return string.Join(" | ", receipts) + "; " + caveCells
                + " cave cells carved";
        }

        // Cave texturing (shallow fresh water and gravel) self-masks on
        // MapGenerator.Caves being positive, so it needs no ownership mask -
        // only to run once per carried cave def instead of the root's alone.
        internal string ApplyProjectedCaveTerrain()
        {
            EnsureBuilt();
            if (region == null || members == null) return null;
            var defs = new HashSet<TileMutatorDef>();
            foreach (PlanetTile tile in members)
            {
                if (!IsSelectedCore(tile)) continue;
                Tile info = tile.Valid ? tile.Tile : null;
                if (info == null) continue;
                foreach (TileMutatorDef mutator in info.Mutators)
                    if (mutator?.Worker is TileMutatorWorker_Caves)
                        defs.Add(mutator);
            }
            if (defs.Count == 0) return null;
            foreach (TileMutatorDef mutator in defs.OrderBy(
                entry => entry.defName))
                mutator.Worker.GeneratePostTerrain(map);
            return defs.Select(entry => entry.defName).OrderBy(name => name)
                .ToCommaList() + " textured";
        }

        // Native lakes flatten their basin at MutatorPostElevationFertility
        // so RocksFromGrid never stands rock in the bowl. The projected
        // field does the same for every carried basin, and the map's water
        // body anchor points at the first basin the way a native lake's
        // Init would have set it.
        internal string ApplyProjectedLakeElevation()
        {
            EnsureBuilt();
            if (lakeValueByCell == null) return null;
            MapGenFloatGrid elevation = MapGenerator.Elevation;
            int flattened = 0;
            for (int index = 0; index < lakeValueByCell.Length; index++)
            {
                float value = lakeValueByCell[index];
                var kind = (CAInlandWaterKind)lakeKindByCell[index];
                bool lava = CARegionalProjectionKernel.IsLavaKind(kind);
                if (value <= (lava ? 0.16f : 0.45f)) continue;
                IntVec3 cell = map.cellIndices.IndexToCell(index);
                if (!lava || value > 0.25f)
                {
                    elevation[cell] = 0f;
                    flattened++;
                }
                else if (kind == CAInlandWaterKind.LavaCrater)
                    // The native crater raises a full-rock rim around its
                    // basin before the lake flattens the middle.
                    elevation[cell] = 1f;
            }
            if (kernel != null && kernel.HasLakeCenter)
                map.waterInfo.lakeCenter = new IntVec3(
                    Mathf.Clamp(Mathf.RoundToInt(kernel.FirstLakeCenter.x),
                        0, map.Size.x - 1), 0,
                    Mathf.Clamp(Mathf.RoundToInt(kernel.FirstLakeCenter.y),
                        0, map.Size.z - 1));
            return (kernel?.LakeCarrierCount ?? 0) + " carrier areas, "
                + flattened + " basin cells flattened, "
                + (kernel?.LakeWaterCells ?? 0) + " water cells";
        }

        internal string ApplyProjectedLakeTerrain()
        {
            EnsureBuilt();
            if (lakeValueByCell == null) return null;
            int water = 0;
            int shore = 0;
            for (int index = 0; index < lakeValueByCell.Length; index++)
            {
                float value = lakeValueByCell[index];
                var kind = (CAInlandWaterKind)lakeKindByCell[index];
                if (value <= (CARegionalProjectionKernel.IsLavaKind(kind)
                        ? 0.25f : kind == CAInlandWaterKind.Oasis
                        ? 0.3f : 0.45f)) continue;
                IntVec3 cell = map.cellIndices.IndexToCell(index);
                switch (kind == CAInlandWaterKind.Basin
                    ? CAInlandWaterKind.Lake : kind)
                {
                    case CAInlandWaterKind.LavaLake:
                    case CAInlandWaterKind.LavaCrater:
                        if (value > 0.4f)
                        {
                            map.terrainGrid.SetTerrain(cell,
                                TerrainDefOf.LavaDeep);
                            water++;
                        }
                        else
                        {
                            map.terrainGrid.SetTerrain(cell,
                                TerrainDefOf.VolcanicRock);
                            shore++;
                        }
                        break;
                    case CAInlandWaterKind.Lake:
                    case CAInlandWaterKind.Toxic:
                        if (value > 0.75f)
                        {
                            map.terrainGrid.SetTerrain(cell,
                                kind == CAInlandWaterKind.Toxic
                                    ? TerrainDefOf.ToxicWaterDeep
                                    : MapGenUtility.DeepFreshWaterTerrainAt(
                                        cell, map));
                            water++;
                        }
                        else if (value > 0.5f)
                        {
                            map.terrainGrid.SetTerrain(cell,
                                kind == CAInlandWaterKind.Toxic
                                    ? TerrainDefOf.ToxicWaterShallow
                                    : MapGenUtility
                                        .ShallowFreshWaterTerrainAt(cell,
                                            map));
                            water++;
                        }
                        else if (MapGenUtility.ShouldGenerateBeachSand(cell,
                            map))
                        {
                            map.terrainGrid.SetTerrain(cell,
                                MapGenUtility.LakeshoreTerrainAt(cell, map));
                            shore++;
                        }
                        break;
                    case CAInlandWaterKind.Pond:
                    case CAInlandWaterKind.LakeWithIsland:
                    case CAInlandWaterKind.LakeWithIslands:
                        if (value > 0.5f)
                        {
                            map.terrainGrid.SetTerrain(cell, MapGenUtility
                                .ShallowFreshWaterTerrainAt(cell, map));
                            water++;
                        }
                        else if (MapGenUtility.ShouldGenerateBeachSand(cell,
                            map))
                        {
                            map.terrainGrid.SetTerrain(cell,
                                MapGenUtility.LakeshoreTerrainAt(cell, map));
                            shore++;
                        }
                        break;
                    case CAInlandWaterKind.Oasis:
                        // Native thresholds: deep .75, water .57, rich
                        // soil .45, soil .3 -- the fertile ring is the
                        // oasis's whole point.
                        if (value > 0.75f)
                        {
                            map.terrainGrid.SetTerrain(cell,
                                MapGenUtility.DeepFreshWaterTerrainAt(cell,
                                    map));
                            water++;
                        }
                        else if (value > 0.57f)
                        {
                            map.terrainGrid.SetTerrain(cell, MapGenUtility
                                .ShallowFreshWaterTerrainAt(cell, map));
                            water++;
                        }
                        else if (!cell.GetTerrain(map).IsWater)
                        {
                            map.terrainGrid.SetTerrain(cell, value > 0.45f
                                ? TerrainDefOf.SoilRich : TerrainDefOf.Soil);
                            shore++;
                        }
                        break;
                    case CAInlandWaterKind.DryLake:
                        map.terrainGrid.SetTerrain(cell, value > 0.5f
                            ? TerrainDefOf.DryLakeBed : TerrainDefOf.Sand);
                        shore++;
                        break;
                }
            }
            return water + " water cells, " + shore + " shore cells";
        }

        // Wetland is a per-cell ridged-noise texture, not a shape: native
        // paints the whole one-tile map from a Rand-seeded field. Here each
        // carrying area is painted from its own deterministically seeded
        // field of the same native character, masked to the cells it owns --
        // the ownership boundary is already noise-warped, so the texture
        // ends where the carrier's ground ends.
        internal string ApplyProjectedWetlands()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null)
                return null;
            var carrierIndexes = new List<int>();
            for (int i = 0; i < members.Count; i++)
            {
                if (!IsSelectedCore(members[i])) continue;
                Tile info = members[i].Valid ? members[i].Tile : null;
                if (info == null) continue;
                foreach (TileMutatorDef mutator in info.Mutators)
                    if (mutator?.Worker is TileMutatorWorker_Wetland)
                    {
                        carrierIndexes.Add(i);
                        break;
                    }
            }
            if (carrierIndexes.Count == 0) return null;

            int painted = 0;
            foreach (int carrierIndex in carrierIndexes)
            {
                int wetTileId = members[carrierIndex].tileId;
                string wetlandDef = CarrierDefOf(carrierIndex, worker =>
                    worker is TileMutatorWorker_Wetland);
                float coverage = ShapeOf(wetTileId, wetlandDef, "coverage",
                    0f);
                var noise = new Verse.Noise.RidgedMultifractal(0.03, 2.0, 2,
                    ShapeSaltOf(Gen.HashCombineInt(wetTileId,
                        0x57455442), wetTileId, wetlandDef),
                    Verse.Noise.QualityMode.High); // "WETB"
                for (int index = 0; index < memberByCell.Length; index++)
                {
                    if (memberByCell[index] != carrierIndex) continue;
                    IntVec3 cell = map.cellIndices.IndexToCell(index);
                    if (cell.GetEdifice(map) != null) continue;
                    TerrainDef terrain = cell.GetTerrain(map);
                    if (terrain.IsWater) continue;
                    float value = Mathf.Clamp01((float)noise.GetValue(
                        cell.x, 0.0, cell.z));
                    if (value > 0.35f - coverage)
                    {
                        map.terrainGrid.SetTerrain(cell, terrain.IsRock
                            ? MapGenUtility.MudTerrainAt(cell, map)
                            : MapGenUtility.ShallowFreshWaterTerrainAt(cell,
                                map));
                        painted++;
                    }
                    else if (value > 0.015f
                        && MapGenUtility.ShouldGenerateBeachSand(cell, map))
                    {
                        map.terrainGrid.SetTerrain(cell,
                            MapGenUtility.MudTerrainAt(cell, map));
                        painted++;
                    }
                }
            }
            return carrierIndexes.Count + " carrier areas, " + painted
                + " cells wetted";
        }

        private float ShapeOf(int tileId, string featureDef, string key,
            float fallback)
        {
            return CAFeatureShapeModel.Value(region?.featureShapes, tileId,
                featureDef, key, fallback);
        }

        private int ShapeSaltOf(int salt, int tileId, string featureDef)
        {
            return CAFeatureShapeModel.SaltWithVariant(salt,
                region?.featureShapes, tileId, featureDef);
        }

        private string CarrierDefOf(int carrierIndex,
            Func<TileMutatorWorker, bool> test)
        {
            Tile info = members[carrierIndex].Valid
                ? members[carrierIndex].Tile : null;
            if (info == null) return null;
            foreach (TileMutatorDef mutator in info.Mutators)
                if (mutator?.Worker != null && test(mutator.Worker))
                    return mutator.defName;
            return null;
        }

        private List<int> CarriersOf(Func<TileMutatorWorker, bool> test)
        {
            var carriers = new List<int>();
            for (int i = 0; i < members.Count; i++)
            {
                if (!IsSelectedCore(members[i])) continue;
                Tile info = members[i].Valid ? members[i].Tile : null;
                if (info == null) continue;
                foreach (TileMutatorDef mutator in info.Mutators)
                    if (mutator?.Worker != null && test(mutator.Worker))
                    {
                        carriers.Add(i);
                        break;
                    }
            }
            return carriers;
        }

        private static float CarrierUnit(int salt, int index)
        {
            return (Gen.HashCombineInt(salt, index) & 0xFFFF) / 65535f;
        }

        private float? CarrierCoastAngle(PlanetTile carrier)
        {
            try
            {
                return Verse.Find.World.CoastAngleAt(carrier,
                        BiomeDefOf.Ocean)
                    ?? Verse.Find.World.CoastAngleAt(carrier,
                        BiomeDefOf.Lake);
            }
            catch { return null; }
        }

        // The native seaward falloff (FalloffAtAngle) rebuilt at carrier
        // scale: a signed ramp low on the ocean side, one inland, so raised
        // shapes never wall off the carrier's own coast.
        private static Verse.Noise.ModuleBase CarrierCoastFalloff(
            float angle, float offsetPct, float span, Vector2 center)
        {
            Verse.Noise.ModuleBase input =
                new Verse.Noise.DistFromAxis_Directional(span / 2f);
            input = new Verse.Noise.ScaleBias(0.5, 0.5, input);
            input = new Verse.Noise.Translate(span * (0.5f - offsetPct),
                0.0, 0.0, input);
            input = new Verse.Noise.Rotate(0.0, angle, 0.0, input);
            return new Verse.Noise.Translate(-center.x, 0.0, -center.y,
                input);
        }

        // Native Plateau raises a broad flat-topped massif (falloff radius
        // to the .2 power) shifted toward the coast when there is one. The
        // exact native module graph is evaluated per carrying area with a
        // deterministic seed and carrier-scaled radii, blended into the
        // carrier's own cells.
        internal string ApplyProjectedPlateaus()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null
                || kernel == null) return null;
            List<int> carriers = CarriersOf(worker =>
                worker is TileMutatorWorker_Plateau);
            if (carriers.Count == 0) return null;
            MapGenFloatGrid elevation = MapGenerator.Elevation;
            float span = Math.Max(8f, kernel.LocalCells);
            int raised = 0;
            foreach (int carrierIndex in carriers)
            {
                PlanetTile carrier = members[carrierIndex];
                string plateauDef = CarrierDefOf(carrierIndex, worker =>
                    worker is TileMutatorWorker_Plateau);
                int salt = ShapeSaltOf(Gen.HashCombineInt(carrier.tileId,
                    0x504C4154), carrier.tileId, plateauDef); // "PLAT"
                Vector2 anchor = kernel.VisualLandAnchor(carrier.tileId);
                float? coast = CarrierCoastAngle(carrier);
                Vector2 offset = coast.HasValue
                    ? anchor + Vector2Utility.FromAngle(coast.Value)
                        * span * 0.2f
                    : anchor + new Vector2(
                        (CarrierUnit(salt, 1) * 2f - 1f) * 0.2f * span,
                        (CarrierUnit(salt, 2) * 2f - 1f) * 0.2f * span);
                float plateauWanderX = ShapeOf(carrier.tileId, plateauDef,
                    "wanderX", float.NaN);
                float plateauWanderZ = ShapeOf(carrier.tileId, plateauDef,
                    "wanderZ", float.NaN);
                if (!float.IsNaN(plateauWanderX))
                    offset.x = anchor.x + plateauWanderX * span;
                if (!float.IsNaN(plateauWanderZ))
                    offset.y = anchor.y + plateauWanderZ * span;
                Verse.Noise.ModuleBase noise =
                    MapNoiseUtility.CreateFalloffRadius(span * 0.4f
                        * ShapeOf(carrier.tileId, plateauDef, "span", 1f),
                        offset, 0.2f);
                noise = MapNoiseUtility.AddDisplacementNoise(noise, 0.015f,
                    35f, 4, Gen.HashCombineInt(salt, 11));
                raised += BlendElevationShape(elevation, carrierIndex,
                    anchor, span, noise, 0f);
            }
            return carriers.Count + " carrier areas, " + raised
                + " cells raised";
        }

        // Native Cliffs raises a squashed coastal shelf behind the shore
        // (threshold .75), smoothed low toward the sea. Exact module graph,
        // carrier-scaled.
        internal string ApplyProjectedCliffs()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null
                || kernel == null) return null;
            List<int> carriers = CarriersOf(worker =>
                worker is TileMutatorWorker_Cliffs);
            if (carriers.Count == 0) return null;
            MapGenFloatGrid elevation = MapGenerator.Elevation;
            float span = Math.Max(8f, kernel.LocalCells);
            int raised = 0;
            foreach (int carrierIndex in carriers)
            {
                PlanetTile carrier = members[carrierIndex];
                string cliffDef = CarrierDefOf(carrierIndex, worker =>
                    worker is TileMutatorWorker_Cliffs);
                int salt = ShapeSaltOf(Gen.HashCombineInt(carrier.tileId,
                    0x434C4946), carrier.tileId, cliffDef); // "CLIF"
                Vector2 anchor = kernel.VisualLandAnchor(carrier.tileId);
                float? coast = CarrierCoastAngle(carrier);
                float angle = ShapeOf(carrier.tileId, cliffDef,
                    "orientation", coast ?? CarrierUnit(salt, 1) * 360f);
                Verse.Noise.ModuleBase noise =
                    new Verse.Noise.DistFromPoint(span * 0.5f
                        * ShapeOf(carrier.tileId, cliffDef, "span", 1f));
                noise = new Verse.Noise.Scale(1.0, 1.0, 0.5, noise);
                noise = new Verse.Noise.Translate(0.0, 0.0, -span * 0.5f,
                    noise);
                noise = new Verse.Noise.Rotate(0.0, angle + 90f, 0.0,
                    noise);
                noise = new Verse.Noise.Translate(-anchor.x, 0.0,
                    -anchor.y, noise);
                if (coast.HasValue)
                    noise = new Verse.Noise.SmoothMin(noise,
                        CarrierCoastFalloff(coast.Value, 0.1f, span,
                            anchor), 0.1);
                noise = MapNoiseUtility.AddDisplacementNoise(noise, 0.015f,
                    40f, 4, Gen.HashCombineInt(salt, 11));
                raised += BlendElevationShape(elevation, carrierIndex,
                    anchor, span, noise, 0.75f);
            }
            return carriers.Count + " carrier areas, " + raised
                + " cells raised";
        }

        // Shared consumer for native shapes that SET elevation above a
        // threshold: owner-masked, faded at the carrier's rim, kept out of
        // projected water so a raised shelf never stands in the sea.
        private int BlendElevationShape(MapGenFloatGrid elevation,
            int carrierIndex, Vector2 anchor, float span,
            Verse.Noise.ModuleBase noise, float threshold)
        {
            int changed = 0;
            int minX = Math.Max(0, Mathf.FloorToInt(anchor.x - span * 1.2f));
            int maxX = Math.Min(map.Size.x - 1,
                Mathf.CeilToInt(anchor.x + span * 1.2f));
            int minZ = Math.Max(0, Mathf.FloorToInt(anchor.y - span * 1.2f));
            int maxZ = Math.Min(map.Size.z - 1,
                Mathf.CeilToInt(anchor.y + span * 1.2f));
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int index = z * map.Size.x + x;
                    if (memberByCell[index] != carrierIndex) continue;
                    if (boundaryWaterByCell != null
                        && boundaryWaterByCell[index] >= 0) continue;
                    if (lakeValueByCell != null
                        && lakeValueByCell[index] > 0.5f
                        && lakeKindByCell[index]
                            != (byte)CAInlandWaterKind.DryLake) continue;
                    float localX = x - anchor.x;
                    float localZ = z - anchor.y;
                    float envelope = Mathf.Sqrt(localX * localX
                        + localZ * localZ) / (span * 0.95f);
                    if (envelope > 1.15f) continue;
                    float value = (float)noise.GetValue(x, 0.0, z);
                    if (value <= threshold) continue;
                    float fade = Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(0.80f, 1.15f, envelope));
                    IntVec3 cell = map.cellIndices.IndexToCell(index);
                    float blended = Mathf.Lerp(value, elevation[cell], fade);
                    if (Mathf.Abs(blended - elevation[cell]) < 0.01f)
                        continue;
                    elevation[cell] = blended;
                    changed++;
                }
            }
            return changed;
        }

        // Obsidian lumps at the native density, confined to the carrying
        // area: the native worker delegates to the lump scatterer over the
        // whole map, so on a regional map the anchor's deposits spread
        // region-wide and a member's never appeared.
        private sealed class CACarrierLumpScatterer
            : GenStep_ScatterLumpsMineable
        {
            internal Func<IntVec3, bool> allowedCell;
            protected override bool CanScatterAt(IntVec3 c, Map map)
            {
                return (allowedCell == null || allowedCell(c))
                    && base.CanScatterAt(c, map);
            }
        }

        internal string ApplyProjectedObsidianDeposits()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null)
                return null;
            List<int> carriers = CarriersOf(worker =>
                worker is TileMutatorWorker_ObsidianDeposits);
            if (carriers.Count == 0) return null;
            // Mineable lumps are things; the terrain-only preview pipeline
            // discards them, so seeding is real-generation work.
            if (CARegionalCompatibility.IsMapPreviewGenerating())
                return null;
            float per10k = GenStep_RocksFromGrid
                .GetResourceBlotchesPer10KCellsForMap(map) * 0.1f;
            int lumps = 0;
            foreach (int carrierIndex in carriers)
            {
                int carrierCells = 0;
                for (int index = 0; index < memberByCell.Length; index++)
                    if (memberByCell[index] == carrierIndex) carrierCells++;
                if (carrierCells == 0) continue;
                // The scatterer computes count from whole-map area; scale
                // the density so the carrier receives exactly its own
                // one-tile share, then confine placement to its cells.
                float share = per10k * carrierCells
                    / (float)memberByCell.Length;
                int local = carrierIndex;
                var scatter = new CACarrierLumpScatterer
                {
                    maxValue = float.MaxValue,
                    countPer10kCellsRange = new FloatRange(share, share),
                    forcedDefToScatter = ThingDefOf.MineableObsidian,
                    allowedCell = cell => memberByCell[
                        map.cellIndices.CellToIndex(cell)] == local
                };
                scatter.Generate(map, default(GenStepParams));
                lumps++;
            }
            return carriers.Count + " carrier areas seeded";
        }

        // Native Harbor plants a faction dock outpost near the one-tile
        // map's center and runs its dock seaward by map-center geometry.
        // Per carrier: the outpost seeks clear ground near the carrier's
        // own land, its owner prefers the settlement that actually holds
        // the area, and the dock runs toward the carrier's real nearest
        // boundary water. The native perpendicular center-alignment leg
        // has no carrier-scale meaning and is dropped -- the dock walks
        // straight to open water (named deviation).
        internal string ApplyProjectedHarbors()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null
                || kernel == null) return null;
            List<int> carriers = CarriersOf(worker =>
                worker is TileMutatorWorker_Harbor);
            if (carriers.Count == 0) return null;
            var receipts = new List<string>();
            foreach (int carrierIndex in carriers)
            {
                PlanetTile carrier = members[carrierIndex];
                Vector2 anchor = kernel.VisualLandAnchor(carrier.tileId);
                var anchorCell = new IntVec3(
                    Mathf.Clamp(Mathf.RoundToInt(anchor.x), 0,
                        map.Size.x - 1), 0,
                    Mathf.Clamp(Mathf.RoundToInt(anchor.y), 0,
                        map.Size.z - 1));
                List<CellRect> usedRects = MapGenerator
                    .GetOrGenerateVar<List<CellRect>>("UsedRects");
                CellRect rect;
                if (!MapGenUtility.TryGetClosestClearRectTo(out rect,
                    new IntVec2(16, 16), anchorCell,
                    r => !r.Cells.Any(c =>
                            c.GetTerrain(map).IsWater)
                        && !usedRects.Any(ur => ur.Overlaps(r)
                            && ur.CenterCell.InHorDistOf(anchorCell,
                                75f))))
                {
                    IntVec3 fallback;
                    if (!CellFinder.TryFindRandomCellNear(anchorCell, map,
                        75, c => !c.GetTerrain(map).IsWater
                            && c.GetEdifice(map) == null, out fallback))
                    {
                        receipts.Add(carrier.tileId + ":no ground");
                        continue;
                    }
                    rect = CellRect.CenteredOn(fallback, 8);
                }
                Faction faction = OwnerFactionFor(carrier.tileId)
                    ?? map.ParentFaction;
                if (faction == null || faction == Faction.OfPlayer)
                    Find.FactionManager.GetFactions().TryRandomElement(
                        out faction);
                if (faction == null)
                {
                    receipts.Add(carrier.tileId + ":no faction");
                    continue;
                }
                var outpost = new GenStep_Outpost
                {
                    size = 16,
                    forcedRect = rect,
                    overrideFaction = faction,
                    postProcessSettlementParams =
                        new MapGenUtility.PostProcessSettlementParams
                        {
                            clearBuildingFaction = true,
                            faction = faction,
                            damageBuildings = true,
                            canDamageWalls = false,
                            noFuel = true,
                            ageCorpses = true
                        },
                    settlementDontGeneratePawns = true,
                    generateLoot = false
                };
                outpost.Generate(map, default(GenStepParams));

                IntVec3 center = rect.CenterCell;
                int nearest = kernel.NearestBoundaryWaterByCell[
                    map.cellIndices.CellToIndex(center)];
                if (nearest < 0
                    || nearest >= kernel.BoundaryWaterTiles.Count)
                {
                    receipts.Add(carrier.tileId + ":outpost, no water");
                    continue;
                }
                Vector2 waterPoint = kernel.ProjectPoint(
                    kernel.BoundaryWaterTiles[nearest]);
                Rot4 waterDir = Rot4.FromAngleFlat((new IntVec3(
                    Mathf.RoundToInt(waterPoint.x), 0,
                    Mathf.RoundToInt(waterPoint.y)) - center).AngleFlat);
                var path = new List<IntVec3>();
                IntVec3 walk = center;
                int guard = 199;
                while (walk.InBounds(map)
                    && !walk.GetTerrain(map).IsOcean && guard-- > 0)
                {
                    path.Add(walk);
                    walk += waterDir.AsIntVec3;
                }
                if (!walk.InBounds(map) || guard <= 0)
                {
                    receipts.Add(carrier.tileId
                        + ":outpost, dock found no open water");
                    continue;
                }
                DockExtend(walk, path, Rand.RangeInclusive(19, 27),
                    waterDir, 1f, true, waterDir);
                path = DockStartFrom(path, rect);
                foreach (IntVec3 item in path)
                    DockAround(item);
                receipts.Add(carrier.tileId + ":dock " + path.Count
                    + " cells");
            }
            return string.Join(" | ", receipts);
        }

        // ---- ancient-structure family, carrier-local ---------------------
        // Native anchors these to the one-tile map (its center, its edges,
        // or anywhere on it). Per carrier: searches confine to the cells
        // the carrier owns, counts stay the native per-tile counts, and
        // member-carried structures exist at all. Ownership containment
        // replaces the native map-edge margins -- named deviation.

        private List<IntVec3> OwnedCells(int carrierIndex)
        {
            var cells = new List<IntVec3>();
            for (int index = 0; index < memberByCell.Length; index++)
                if (memberByCell[index] == carrierIndex)
                    cells.Add(map.cellIndices.IndexToCell(index));
            return cells;
        }

        private bool CarrierOwnsRect(int carrierIndex, CellRect rect)
        {
            foreach (IntVec3 cell in rect)
            {
                if (!cell.InBounds(map)) return false;
                if (memberByCell[map.cellIndices.CellToIndex(cell)]
                    != carrierIndex) return false;
            }
            return true;
        }

        private List<KeyValuePair<int, TileMutatorDef>> CarrierDefsOf(
            Func<TileMutatorWorker, bool> test)
        {
            var entries = new List<KeyValuePair<int, TileMutatorDef>>();
            for (int i = 0; i < members.Count; i++)
            {
                if (!IsSelectedCore(members[i])) continue;
                Tile info = members[i].Valid ? members[i].Tile : null;
                if (info == null) continue;
                foreach (TileMutatorDef mutator in info.Mutators)
                    if (mutator?.Worker != null && test(mutator.Worker))
                        entries.Add(new KeyValuePair<int, TileMutatorDef>(
                            i, mutator));
            }
            return entries;
        }

        internal string ApplyProjectedAncientVents()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null)
                return null;
            List<KeyValuePair<int, TileMutatorDef>> entries = CarrierDefsOf(
                worker => worker is TileMutatorWorker_AncientVent);
            if (entries.Count == 0) return null;
            List<CellRect> usedRects = MapGenerator
                .GetOrGenerateVar<List<CellRect>>("UsedRects");
            int vents = 0;
            foreach (KeyValuePair<int, TileMutatorDef> entry in entries)
            {
                var ventDef = AccessTools.Property(
                        entry.Value.Worker.GetType(), "AncientVentDef")
                    ?.GetValue(entry.Value.Worker) as ThingDef;
                if (ventDef == null) continue;
                List<IntVec3> owned = OwnedCells(entry.Key);
                if (owned.Count == 0) continue;
                int target = Rand.RangeInclusive(5, 9);
                var positions = new List<IntVec3>();
                int attempts = 400;
                while (positions.Count < target && attempts-- > 0)
                {
                    IntVec3 candidate = owned.RandomElement();
                    if (!GenSpawn.CanSpawnAt(ventDef, candidate, map, null,
                        canWipeEdifices: false)) continue;
                    CellRect occupied = GenAdj.OccupiedRect(candidate,
                        Rot4.North, ventDef.size);
                    if (!CarrierOwnsRect(entry.Key, occupied)) continue;
                    if (usedRects.Any(rect => rect.Overlaps(occupied)))
                        continue;
                    if (positions.Any(placed =>
                        placed.InHorDistOf(candidate, 25f))) continue;
                    positions.Add(candidate);
                }
                positions.RemoveAll(position => GenRadial.RadialCellsAround(
                        position, 0f, 8.9f)
                    .Any(cell => cell.InBounds(map)
                        && map.terrainGrid.TerrainAt(cell).IsWater));
                foreach (IntVec3 position in positions)
                    foreach (IntVec3 cell in GenRadial.RadialCellsAround(
                        position, 0f, 8.9f))
                        if (cell.InBounds(map)
                            && cell.GetEdifice(map) == null)
                            map.terrainGrid.SetTerrain(cell,
                                TerrainDefOf.AncientMegastructure);
                foreach (IntVec3 position in positions)
                {
                    Thing thing = ThingMaker.MakeThing(ventDef);
                    GenSpawn.Spawn(thing, position, map);
                    usedRects.Add(thing.OccupiedRect());
                    vents++;
                }
            }
            return entries.Count + " carrier entries, " + vents
                + " vents placed";
        }

        // Native ruins fill .3-.5 of the ONE-TILE map through a map-wide
        // generator whose internal placement cannot be masked. The fill is
        // scaled to the carrying areas' share of the aggregate so the
        // QUANTITY is the native per-tile quantity; positions remain
        // map-wide -- named deviation, recorded until the native generator
        // grows a maskable seam.
        internal string ApplyProjectedAncientRuins()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null)
                return null;
            List<int> carriers = CarriersOf(worker =>
                worker is TileMutatorWorker_AncientRuins);
            if (carriers.Count == 0) return null;
            int carrierCells = 0;
            for (int index = 0; index < memberByCell.Length; index++)
                if (carriers.Contains(memberByCell[index])) carrierCells++;
            if (carrierCells == 0) return null;
            float share = carrierCells / (float)memberByCell.Length;
            try
            {
                var genStep = (GenStep_AncientRuins)GenStepDefOf
                    .AncientRuins_Special.genStep;
                genStep.GenerateRuins(map, default(GenStepParams),
                    new FloatRange(0.3f * share, 0.5f * share));
            }
            catch (Exception failure)
            {
                Log.Error("[CA][Regional] ancient ruins generation: "
                    + failure);
            }
            return carriers.Count + " carrier areas, fill share "
                + share.ToStringPercent();
        }

        private readonly Dictionary<string, CellRect>
            ancientStructureRects = new Dictionary<string, CellRect>();

        internal string ApplyProjectedAncientStructures()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null
                || kernel == null) return null;
            List<KeyValuePair<int, TileMutatorDef>> entries = CarrierDefsOf(
                worker => worker is TileMutatorWorker_AncientStructure);
            if (entries.Count == 0) return null;
            List<CellRect> usedRects = MapGenerator
                .GetOrGenerateVar<List<CellRect>>("UsedRects");
            int placed = 0;
            foreach (KeyValuePair<int, TileMutatorDef> entry in entries)
            {
                var parms = entry.Value.structureGenParms;
                if (parms?.structureLayoutDef == null) continue;
                Vector2 anchor = kernel.VisualLandAnchor(
                    members[entry.Key].tileId);
                var anchorCell = new IntVec3(
                    Mathf.Clamp(Mathf.RoundToInt(anchor.x), 0,
                        map.Size.x - 1), 0,
                    Mathf.Clamp(Mathf.RoundToInt(anchor.y), 0,
                        map.Size.z - 1));
                int width = parms.structureSizeRange.RandomInRange
                    + parms.perimeterExpandBy * 2;
                int height = parms.structureSizeRange.RandomInRange
                    + parms.perimeterExpandBy * 2;
                int carrier = entry.Key;
                CellRect perimeter;
                if (!MapGenUtility.TryGetClosestClearRectTo(out perimeter,
                    new IntVec2(width, height), anchorCell,
                    rect => CarrierOwnsRect(carrier, rect)
                        && !usedRects.Any(used => used.Overlaps(rect))))
                    perimeter = anchorCell.RectAbout(width, height)
                        .ClipInsideMap(map);
                usedRects.Add(perimeter);
                CellRect inner = perimeter.ContractedBy(
                    parms.perimeterExpandBy);
                MapGenerator.SetVar("SpawnRect", inner);
                var sketchParms = new StructureGenParams
                {
                    size = inner.Size
                };
                LayoutWorker layoutWorker =
                    parms.structureLayoutDef.Worker;
                LayoutStructureSketch sketch =
                    layoutWorker.GenerateStructureSketch(sketchParms);
                map.layoutStructureSketches.Add(sketch);
                layoutWorker.Spawn(sketch, map, inner.Min);
                TileMutatorWorker privateWorker = CAPrivateWorkers.For(
                    entry.Value, new CAFeatureInstance(region.candidateId,
                        carrier, members[carrier].tileId,
                        entry.Value.defName, 1));
                AccessTools.Method(
                        typeof(TileMutatorWorker_AncientStructure),
                        "GeneratePerimeter")
                    ?.Invoke(privateWorker, new object[] { sketch, map });
                ancientStructureRects[carrier + ":"
                    + entry.Value.defName] = perimeter;
                placed++;
            }
            return entries.Count + " carrier entries, " + placed
                + " structures placed";
        }

        internal string ApplyProjectedAncientStructureScatter()
        {
            if (ancientStructureRects.Count == 0) return null;
            EnsureBuilt();
            List<KeyValuePair<int, TileMutatorDef>> entries = CarrierDefsOf(
                worker => worker is TileMutatorWorker_AncientStructure);
            int scattered = 0;
            foreach (KeyValuePair<int, TileMutatorDef> entry in entries)
            {
                CellRect rect;
                if (!ancientStructureRects.TryGetValue(entry.Key + ":"
                    + entry.Value.defName, out rect)) continue;
                MapGenUtility.SpawnScatteredGroupPrefabs(map, rect,
                    entry.Value.structureGenParms.scatteredPrefabs);
                scattered++;
            }
            return scattered + " perimeters scattered";
        }

        internal string ApplyProjectedAncientQuarries()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null)
                return null;
            List<int> carriers = CarriersOf(worker =>
                worker is TileMutatorWorker_AncientQuarry);
            if (carriers.Count == 0) return null;
            int carved = 0;
            foreach (int carrierIndex in carriers)
            {
                List<IntVec3> owned = OwnedCells(carrierIndex);
                if (owned.Count == 0) continue;
                int areas = Rand.RangeInclusive(10, 15);
                for (int i = 0; i < areas; i++)
                {
                    IntVec3 seat = IntVec3.Invalid;
                    for (int attempt = 0; attempt < 80; attempt++)
                    {
                        IntVec3 candidate = owned.RandomElement();
                        if (candidate.Fogged(map)) continue;
                        Building edifice = candidate.GetEdifice(map);
                        if (edifice == null
                            || !edifice.def.IsNonResourceNaturalRock)
                            continue;
                        bool exposed = false;
                        foreach (IntVec3 direction in
                            GenAdj.CardinalDirections)
                        {
                            IntVec3 side = candidate + direction;
                            if (side.InBounds(map)
                                && side.GetEdifice(map) == null)
                                exposed = true;
                        }
                        if (!exposed) continue;
                        int size = 0;
                        map.floodFiller.FloodFill(candidate,
                            cell => cell.GetEdifice(map)?.def.mineable
                                ?? false,
                            (Action<IntVec3>)delegate { size++; },
                            int.MaxValue, false, null);
                        if (size < 1000) continue;
                        seat = candidate;
                        break;
                    }
                    if (!seat.IsValid) continue;
                    int lumpSize = Rand.RangeInclusive(100, 500);
                    List<IntVec3> lump = GridShapeMaker.IrregularLump(seat,
                        map, lumpSize);
                    bool allRoofed = lump.All(cell => cell.Roofed(map));
                    foreach (IntVec3 cell in lump)
                    {
                        Building edifice = cell.GetEdifice(map);
                        if (edifice != null)
                        {
                            if (!edifice.def.mineable) continue;
                            edifice.Destroy();
                            map.fogGrid.FloodUnfogAdjacent(cell, false);
                            if (Rand.Chance(0.8f))
                                GenSpawn.Spawn(ThingDefOf.Filth_RubbleRock,
                                    cell, map);
                            if (Rand.Chance(0.1f) && edifice.def.building
                                .mineableThing != null)
                            {
                                Thing yield = GenSpawn.Spawn(
                                    edifice.def.building.mineableThing,
                                    cell, map);
                                yield.stackCount = Rand.Range(1, Mathf.Max(
                                    1, edifice.def.building
                                        .EffectiveMineableYield));
                                if (yield.def.EverHaulable
                                    && !yield.def.designateHaulable)
                                    yield.SetForbidden(true, false);
                            }
                        }
                        if (!allRoofed) map.roofGrid.SetRoof(cell, null);
                    }
                    if (allRoofed)
                        foreach (IntVec3 cell in lump)
                        {
                            if (RoofCollapseUtility.WithinRangeOfRoofHolder(
                                cell, map)) continue;
                            ThingDef rock = DeepDrillUtility.RockForTerrain(
                                cell.GetTerrain(map));
                            if (rock == null) continue;
                            ThingDef stuff = DefDatabase<ThingDef>.AllDefs
                                .FirstOrDefault(def => def.IsStuff
                                    && def.stuffProps.SourceNaturalRock
                                        == rock);
                            if (stuff == null
                                || !stuff.stuffProps.CanMake(
                                    ThingDefOf.Column))
                                stuff = GenStuff.RandomStuffByCommonalityFor(
                                    ThingDefOf.Column);
                            GenSpawn.Spawn(ThingMaker.MakeThing(
                                ThingDefOf.Column, stuff), cell, map);
                        }
                    carved++;
                }
            }
            RoofCollapseCellsFinder.CheckAndRemoveCollpsingRoofs(map);
            return carriers.Count + " carrier areas, " + carved
                + " quarried lumps";
        }

        internal string ApplyProjectedAncientUplinks()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null)
                return null;
            List<int> carriers = CarriersOf(worker =>
                worker is TileMutatorWorker_AncientUplink);
            if (carriers.Count == 0) return null;
            List<CellRect> usedRects = MapGenerator
                .GetOrGenerateVar<List<CellRect>>("UsedRects");
            PrefabDef prefab = PrefabDefOf.AncientUplink;
            int placed = 0;
            foreach (int carrierIndex in carriers)
            {
                List<IntVec3> owned = OwnedCells(carrierIndex);
                if (owned.Count == 0) continue;
                CellRect rect = CellRect.Empty;
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    IntVec3 candidate = owned.RandomElement();
                    CellRect probe = CellRect.CenteredOn(candidate,
                        prefab.size.x, prefab.size.z);
                    if (!probe.InBounds(map)) continue;
                    if (!CarrierOwnsRect(carrierIndex, probe)) continue;
                    if (probe.Cells.Any(cell => cell.Fogged(map)))
                        continue;
                    if (!PrefabUtility.CanSpawnPrefab(prefab, map,
                        UplinkRoot(probe), Rot4.North,
                        canWipeEdifices: false)) continue;
                    if (usedRects.Any(used => used.Overlaps(probe)))
                        continue;
                    rect = probe;
                    break;
                }
                if (rect == CellRect.Empty) continue;
                foreach (IntVec3 cell in rect)
                    map.terrainGrid.SetTerrain(cell,
                        TerrainDefOf.AncientTile);
                PrefabUtility.SpawnPrefab(prefab, map, UplinkRoot(rect),
                    Rot4.North);
                usedRects.Add(rect);
                placed++;
            }
            return carriers.Count + " carrier areas, " + placed
                + " uplinks placed";
        }

        private static IntVec3 UplinkRoot(CellRect rect)
        {
            IntVec3 center = rect.CenterCell;
            if (rect.Width % 2 == 0) center.x--;
            if (rect.Height % 2 == 0) center.z--;
            return center;
        }

        private sealed class CACarrierSettlementScatterer
            : GenStep_Settlement
        {
            internal Func<IntVec3, bool> allowedCell;
            protected override bool CanScatterAt(IntVec3 c, Map map)
            {
                return (allowedCell == null || allowedCell(c))
                    && base.CanScatterAt(c, map);
            }
        }

        internal string ApplyProjectedAbandonedColonies()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null)
                return null;
            List<KeyValuePair<int, TileMutatorDef>> entries = CarrierDefsOf(
                worker => worker is TileMutatorWorker_AbandonedColony);
            if (entries.Count == 0) return null;
            int placed = 0;
            foreach (KeyValuePair<int, TileMutatorDef> entry in entries)
            {
                var faction = AccessTools.Method(
                        entry.Value.Worker.GetType(), "GetFaction")
                    ?.Invoke(entry.Value.Worker, null) as Faction;
                if (faction == null) continue;
                int carrier = entry.Key;
                var scatter = new CACarrierSettlementScatterer
                {
                    generatePawns = false,
                    overrideFaction = faction,
                    count = 1,
                    lootThingSetMaker = ThingSetMakerDefOf
                        .MapGen_AbandonedColonyStockpile,
                    lootMarketValue = 500f,
                    postProcessSettlementParams =
                        new MapGenUtility.PostProcessSettlementParams
                        {
                            clearBuildingFaction = true,
                            faction = faction,
                            damageBuildings = true,
                            canDamageWalls = false,
                            noFuel = true,
                            ageCorpses = true
                        },
                    allowedCell = cell => memberByCell[
                        map.cellIndices.CellToIndex(cell)] == carrier
                };
                scatter.Generate(map, default(GenStepParams));
                CellRect rect = MapGenerator.GetVar<CellRect>(
                    "SettlementRect");
                MapGenUtility.ScatterCorpses(rect, map, faction,
                    new IntRange(0, 3), new FloatRange(20f, 60f));
                MapGenUtility.DestroyTurrets(map);
                MapGenUtility.DestroyProcessedFood(map);
                MapGenUtility.ForbidAllItems(map);
                MapGenerator.GetOrGenerateVar<List<CellRect>>("UsedRects")
                    .Add(rect);
                placed++;
            }
            return entries.Count + " carrier entries, " + placed
                + " abandoned colonies";
        }

        private Faction OwnerFactionFor(int tileId)
        {
            CARegionalSettlementPlan place = region?.settlements
                ?.FirstOrDefault(item => item != null
                    && item.memberTileId == tileId);
            int loadId = place?.factionLinks?.ownerWorldFactionLoadId
                ?? -1;
            if (loadId < 0) return null;
            return Find.FactionManager.AllFactions.FirstOrDefault(
                faction => faction.loadID == loadId);
        }

        // The native dock builders, re-expressed verbatim with the water
        // direction threaded as a parameter instead of worker state.
        private bool DockCollision(IntVec3 checkCell, Rot4 dir,
            List<IntVec3> path)
        {
            if (!DockValidateCell(checkCell, dir, path)) return true;
            if (!DockValidateCell(checkCell,
                    dir.Rotated(RotationDirection.Clockwise), path)
                || !DockValidateCell(checkCell,
                    dir.Rotated(RotationDirection.Counterclockwise), path))
                return true;
            return false;
        }

        private bool DockValidateCell(IntVec3 cell, Rot4 dir,
            List<IntVec3> path)
        {
            for (int i = 0; i < 4; i++)
            {
                IntVec3 probe = cell + dir.AsIntVec3 * i;
                if (!probe.InBounds(map)
                    || !probe.GetTerrain(map).IsOcean
                    || path.Contains(probe))
                    return false;
            }
            return true;
        }

        private void DockExtend(IntVec3 startCell, List<IntVec3> path,
            int length, Rot4 direction, float branchChance,
            bool mainBranch, Rot4 waterDir)
        {
            int untilBranch = Rand.RangeInclusive(9, 11);
            for (int i = 0; i < length; i++)
            {
                IntVec3 cell = startCell + direction.AsIntVec3 * i;
                if (!cell.InBounds(map)) continue;
                if (DockCollision(cell, direction, path))
                {
                    if (mainBranch && i > 10) break;
                    if (!mainBranch && i > 0)
                    {
                        for (int back = i; back > 1; back--)
                            path.Pop();
                        break;
                    }
                }
                path.Add(cell);
                untilBranch--;
                if (untilBranch != 0) continue;
                untilBranch = Rand.RangeInclusive(9, 11);
                if (Rand.Chance(branchChance))
                {
                    Rot4 branch;
                    do
                    {
                        branch = direction.Rotated(Rand.Bool
                            ? RotationDirection.Clockwise
                            : RotationDirection.Counterclockwise);
                    }
                    while (branch == waterDir.Opposite);
                    DockExtend(cell, path, Rand.RangeInclusive(8, 11),
                        branch, branchChance - 0.7f, false, waterDir);
                    if (mainBranch && Rand.Chance(0.75f))
                        DockExtend(cell, path, Rand.RangeInclusive(8, 11),
                            branch.Opposite, branchChance - 0.7f, false,
                            waterDir);
                    else if (Rand.Bool)
                    {
                        path.Add(cell + branch.Opposite.AsIntVec3);
                        path.Add(cell + branch.Opposite.AsIntVec3 * 2);
                    }
                }
            }
        }

        private static List<IntVec3> DockStartFrom(List<IntVec3> path,
            CellRect outpostRect)
        {
            path.Reverse();
            for (int i = 0; i < path.Count; i++)
            {
                IntVec3 cell = path[i];
                foreach (IntVec3 adjacent in GenAdj.AdjacentCellsAndInside)
                {
                    if (!outpostRect.Contains(cell + adjacent)) continue;
                    path = path.GetRange(0, i);
                    path.Reverse();
                    return path;
                }
            }
            path.Reverse();
            return path;
        }

        private void DockAround(IntVec3 cell)
        {
            foreach (IntVec3 adjacent in GenAdj.AdjacentCellsAndInside)
            {
                IntVec3 c = cell + adjacent;
                if (!c.InBounds(map)) continue;
                TerrainDef terrain = c.GetTerrain(map);
                if (terrain.IsFloor) continue;
                Building edifice = c.GetEdifice(map);
                if (edifice != null)
                {
                    if (edifice.def.building.isNaturalRock)
                    {
                        edifice.Destroy();
                        map.roofGrid.SetRoof(c, null);
                    }
                    else if (edifice.def.fillPercent >= 0.99f) continue;
                }
                map.terrainGrid.SetTerrain(c, terrain.IsWater
                    ? TerrainDefOf.Bridge : TerrainDefOf.PackedDirt);
            }
        }

        // Deterministic entrance angles with a minimum separation,
        // replacing the native Rand retry loop.
        private static List<float> CarrierEntranceAngles(int salt,
            int count, float minSeparation, float? first)
        {
            var angles = new List<float>();
            if (first.HasValue) angles.Add(first.Value);
            int target = count + (first.HasValue ? 1 : 0);
            int attempt = 0;
            while (angles.Count < target && attempt < 300)
            {
                float candidate = CarrierUnit(salt, 100 + attempt) * 360f;
                attempt++;
                if (angles.Any(existing => Mathf.Abs(Mathf.DeltaAngle(
                        existing, candidate)) < minSeparation)) continue;
                angles.Add(candidate);
            }
            return angles;
        }

        // Basin's raised rim and entrance corridors (its water is kind
        // Basin in the shared inland-water field): a rock ring around the
        // anchor, cut by a wide entrance facing away from the coast and
        // one or two narrow extra corridors.
        internal string ApplyProjectedBasinRims()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null
                || kernel == null) return null;
            List<int> carriers = CarriersOf(worker =>
                worker is TileMutatorWorker_Basin);
            if (carriers.Count == 0) return null;
            MapGenFloatGrid elevation = MapGenerator.Elevation;
            float span = Math.Max(8f, kernel.LocalCells);
            int raised = 0;
            foreach (int carrierIndex in carriers)
            {
                PlanetTile carrier = members[carrierIndex];
                string basinDef = CarrierDefOf(carrierIndex, worker =>
                    worker is TileMutatorWorker_Basin);
                int salt = ShapeSaltOf(Gen.HashCombineInt(carrier.tileId,
                    0x4241534E), carrier.tileId, basinDef); // "BASN"
                float basinSpan = ShapeOf(carrier.tileId, basinDef, "span",
                    1f);
                Vector2 anchor = kernel.VisualLandAnchor(carrier.tileId);
                float? coast = CarrierCoastAngle(carrier);
                float mainAngle = ShapeOf(carrier.tileId, basinDef,
                    "orientation", (coast.HasValue ? coast.Value + 180f
                        : CarrierUnit(salt, 1) * 360f)
                    + (CarrierUnit(salt, 2) * 60f - 30f));
                Verse.Noise.ModuleBase ring =
                    MapNoiseUtility.CreateFalloffRadius(
                        span * 0.35f * basinSpan, anchor, 1f,
                        invert: false);
                Verse.Noise.ModuleBase cone =
                    new Verse.Noise.DistFromCone(5f, span * 0.05f);
                cone = new Verse.Noise.Rotate(0.0, 360f - mainAngle, 0.0,
                    cone);
                cone = new Verse.Noise.Translate(-anchor.x, 0.0, -anchor.y,
                    cone);
                ring = new Verse.Noise.SmoothMin(ring, cone, 0.2);
                int extras = Mathf.Clamp(Mathf.RoundToInt(ShapeOf(
                    carrier.tileId, basinDef, "corridors",
                    1 + (CarrierUnit(salt, 3) > 0.5f ? 1 : 0))), 1, 2);
                foreach (float angle in CarrierEntranceAngles(
                    Gen.HashCombineInt(salt, 4), extras, 70f, mainAngle))
                {
                    if (Mathf.Approximately(angle, mainAngle)) continue;
                    cone = new Verse.Noise.DistFromCone(5f, span * 0.02f);
                    cone = new Verse.Noise.Rotate(0.0, angle, 0.0, cone);
                    cone = new Verse.Noise.Translate(-anchor.x, 0.0,
                        -anchor.y, cone);
                    ring = new Verse.Noise.SmoothMin(ring, cone, 0.2);
                }
                if (coast.HasValue)
                    ring = new Verse.Noise.SmoothMin(ring,
                        CarrierCoastFalloff(coast.Value, 0.1f, span,
                            anchor), 0.1);
                ring = MapNoiseUtility.AddDisplacementNoise(ring, 0.015f,
                    35f, 4, Gen.HashCombineInt(salt, 5));
                raised += BlendElevationShape(elevation, carrierIndex,
                    anchor, span, ring, 0.5f);
            }
            return carriers.Count + " carrier areas, " + raised
                + " rim cells raised";
        }

        // Hollow: a raised bowl behind the coast opened by two cone
        // corridors at fixed bearings. All native constants, exact graph,
        // carrier-scaled.
        internal string ApplyProjectedHollows()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null
                || kernel == null) return null;
            List<int> carriers = CarriersOf(worker =>
                worker is TileMutatorWorker_Hollow);
            if (carriers.Count == 0) return null;
            MapGenFloatGrid elevation = MapGenerator.Elevation;
            float span = Math.Max(8f, kernel.LocalCells);
            int raised = 0;
            foreach (int carrierIndex in carriers)
            {
                PlanetTile carrier = members[carrierIndex];
                string hollowDef = CarrierDefOf(carrierIndex, worker =>
                    worker is TileMutatorWorker_Hollow);
                int salt = ShapeSaltOf(Gen.HashCombineInt(carrier.tileId,
                    0x484F4C4C), carrier.tileId, hollowDef); // "HOLL"
                Vector2 anchor = kernel.VisualLandAnchor(carrier.tileId);
                float? coast = CarrierCoastAngle(carrier);
                float angle = ShapeOf(carrier.tileId, hollowDef,
                    "orientation", coast ?? CarrierUnit(salt, 1) * 360f);
                Verse.Noise.ModuleBase bowl =
                    MapNoiseUtility.CreateFalloffRadius(span * 0.25f
                        * ShapeOf(carrier.tileId, hollowDef, "span", 1f),
                        Vector2.zero, 1f, invert: false);
                bowl = new Verse.Noise.Translate(0.0, 0.0, span * 0.175f,
                    bowl);
                bowl = new Verse.Noise.Rotate(0.0, angle + 90f, 0.0, bowl);
                bowl = new Verse.Noise.Translate(-anchor.x, 0.0, -anchor.y,
                    bowl);
                Verse.Noise.ModuleBase cone =
                    new Verse.Noise.DistFromCone(0.5f, span * 0.25f);
                cone = new Verse.Noise.Translate(0.0, 0.0, -span * 0.025f,
                    cone);
                cone = new Verse.Noise.Rotate(0.0, angle + 90f, 0.0, cone);
                cone = new Verse.Noise.Translate(-anchor.x, 0.0, -anchor.y,
                    cone);
                bowl = new Verse.Noise.SmoothMin(bowl, cone, 0.5);
                cone = new Verse.Noise.DistFromCone(3f, span * 0.25f);
                cone = new Verse.Noise.Translate(0.0, 0.0, span * 0.375f,
                    cone);
                cone = new Verse.Noise.Rotate(0.0, angle + 90f, 0.0, cone);
                cone = new Verse.Noise.Translate(-anchor.x, 0.0, -anchor.y,
                    cone);
                bowl = new Verse.Noise.SmoothMin(bowl, cone, 0.5);
                if (coast.HasValue)
                    bowl = new Verse.Noise.SmoothMin(bowl,
                        CarrierCoastFalloff(coast.Value, 0.1f, span,
                            anchor), 0.1);
                bowl = MapNoiseUtility.AddDisplacementNoise(bowl, 0.015f,
                    40f, 4, Gen.HashCombineInt(salt, 2));
                raised += BlendElevationShape(elevation, carrierIndex,
                    anchor, span, bowl, 0.5f);
            }
            return carriers.Count + " carrier areas, " + raised
                + " bowl cells raised";
        }

        // Chasm: a rock enclosure around an open squashed pit, cut by
        // three to six narrow entrance corridors at separated bearings.
        internal string ApplyProjectedChasms()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null
                || kernel == null) return null;
            List<int> carriers = CarriersOf(worker =>
                worker is TileMutatorWorker_Chasm);
            if (carriers.Count == 0) return null;
            MapGenFloatGrid elevation = MapGenerator.Elevation;
            float span = Math.Max(8f, kernel.LocalCells);
            int raised = 0;
            foreach (int carrierIndex in carriers)
            {
                PlanetTile carrier = members[carrierIndex];
                string chasmDef = CarrierDefOf(carrierIndex, worker =>
                    worker is TileMutatorWorker_Chasm);
                int salt = ShapeSaltOf(Gen.HashCombineInt(carrier.tileId,
                    0x4348534D), carrier.tileId, chasmDef); // "CHSM"
                Vector2 anchor = kernel.VisualLandAnchor(carrier.tileId);
                float? coast = CarrierCoastAngle(carrier);
                Verse.Noise.ModuleBase pit =
                    new Verse.Noise.DistFromPoint(span * 0.4f
                        * ShapeOf(carrier.tileId, chasmDef, "span", 1f));
                pit = new Verse.Noise.Scale(ShapeOf(carrier.tileId,
                    chasmDef, "stretch", Mathf.Lerp(1f, 1.3f,
                        CarrierUnit(salt, 1))), 1.0, 1.0, pit);
                pit = new Verse.Noise.Rotate(0.0, ShapeOf(carrier.tileId,
                    chasmDef, "orientation", CarrierUnit(salt, 2) * 360f),
                    0.0, pit);
                pit = new Verse.Noise.Translate(-anchor.x, 0.0, -anchor.y,
                    pit);
                if (coast.HasValue)
                    pit = new Verse.Noise.SmoothMin(pit,
                        CarrierCoastFalloff(coast.Value, 0.1f, span,
                            anchor), 0.2);
                pit = MapNoiseUtility.AddDisplacementNoise(pit, 0.015f, 40f,
                    4, Gen.HashCombineInt(salt, 3));
                Verse.Noise.ModuleBase corridors =
                    new Verse.Noise.Const(1.0);
                int count = Mathf.Clamp(Mathf.RoundToInt(ShapeOf(
                    carrier.tileId, chasmDef, "corridors",
                    3 + Mathf.RoundToInt(CarrierUnit(salt, 4) * 3f))),
                    3, 6);
                foreach (float angle in CarrierEntranceAngles(
                    Gen.HashCombineInt(salt, 5), count, 40f, null))
                {
                    Verse.Noise.ModuleBase corridor =
                        new Verse.Noise.DistFromAxis(span * 0.06f);
                    corridor = new Verse.Noise.ScaleBias(-1.0, 1.0,
                        corridor);
                    corridor = new Verse.Noise.Multiply(corridor,
                        new Verse.Noise.CutOff(invert: false,
                            zAxis: true));
                    corridor = new Verse.Noise.ScaleBias(-1.0, 1.0,
                        corridor);
                    corridor = new Verse.Noise.Rotate(0.0, angle, 0.0,
                        corridor);
                    corridor = new Verse.Noise.Translate(-anchor.x, 0.0,
                        -anchor.y, corridor);
                    corridors = new Verse.Noise.SmoothMin(corridors,
                        corridor, 0.2);
                }
                corridors = MapNoiseUtility.AddDisplacementNoise(corridors,
                    0.007f, 50f, 6, Gen.HashCombineInt(salt, 6));
                pit = new Verse.Noise.SmoothMin(pit, corridors, 0.2);
                raised += BlendElevationShape(elevation, carrierIndex,
                    anchor, span, pit, 0.5f);
            }
            return carriers.Count + " carrier areas, " + raised
                + " enclosure cells raised";
        }

        // Crevasse: an ice canyon along a random axis; everything outside
        // the open band becomes solid ice under rock roof. The exact native
        // graph is evaluated per carrying area with a deterministic seed
        // and carrier-scaled span, masked to owned cells.
        private Verse.Noise.ModuleBase CrevasseField(int carrierIndex,
            float span, Vector2 anchor)
        {
            int tileId = members[carrierIndex].tileId;
            string crevasseDef = CarrierDefOf(carrierIndex, worker =>
                worker is TileMutatorWorker_Crevasse);
            int salt = ShapeSaltOf(Gen.HashCombineInt(tileId,
                0x43524556), tileId, crevasseDef); // "CREV"
            Verse.Noise.ModuleBase field =
                new Verse.Noise.DistFromAxis(span * 0.15f
                    * ShapeOf(tileId, crevasseDef, "span", 1f));
            field = new Verse.Noise.Rotate(0.0, ShapeOf(tileId,
                crevasseDef, "orientation",
                CarrierUnit(salt, 1) * 180f), 0.0, field);
            field = new Verse.Noise.Translate(-anchor.x, 0.0, -anchor.y,
                field);
            return MapNoiseUtility.AddDisplacementNoise(field, 0.015f, 20f,
                4, Gen.HashCombineInt(salt, 2));
        }

        internal string ApplyProjectedCrevasseElevation()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null
                || kernel == null) return null;
            List<int> carriers = CarriersOf(worker =>
                worker is TileMutatorWorker_Crevasse);
            if (carriers.Count == 0) return null;
            MapGenFloatGrid elevation = MapGenerator.Elevation;
            float span = Math.Max(8f, kernel.LocalCells);
            int opened = 0;
            foreach (int carrierIndex in carriers)
            {
                Vector2 anchor = kernel.VisualLandAnchor(
                    members[carrierIndex].tileId);
                Verse.Noise.ModuleBase field = CrevasseField(carrierIndex,
                    span, anchor);
                for (int index = 0; index < memberByCell.Length; index++)
                {
                    if (memberByCell[index] != carrierIndex) continue;
                    IntVec3 cell = map.cellIndices.IndexToCell(index);
                    if (field.GetValue(cell.x, 0.0, cell.z) <= 0.5)
                    {
                        elevation[cell] = 0f;
                        opened++;
                    }
                }
            }
            return carriers.Count + " carrier areas, " + opened
                + " canyon cells opened";
        }

        internal string ApplyProjectedCrevasseTerrain()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null
                || kernel == null) return null;
            List<int> carriers = CarriersOf(worker =>
                worker is TileMutatorWorker_Crevasse);
            if (carriers.Count == 0) return null;
            // Preview keeps the ice-terrain statement of the mass; roofs
            // and solid-ice things are discarded by the terrain-only
            // pipeline and wait for real generation.
            bool preview = CARegionalCompatibility.IsMapPreviewGenerating();
            float span = Math.Max(8f, kernel.LocalCells);
            int iced = 0;
            foreach (int carrierIndex in carriers)
            {
                Vector2 anchor = kernel.VisualLandAnchor(
                    members[carrierIndex].tileId);
                Verse.Noise.ModuleBase field = CrevasseField(carrierIndex,
                    span, anchor);
                for (int index = 0; index < memberByCell.Length; index++)
                {
                    if (memberByCell[index] != carrierIndex) continue;
                    IntVec3 cell = map.cellIndices.IndexToCell(index);
                    float value = (float)field.GetValue(cell.x, 0.0,
                        cell.z);
                    if (value <= 0.5f) continue;
                    if (!preview)
                    {
                        if (value > 0.798f)
                            map.roofGrid.SetRoof(cell,
                                RoofDefOf.RoofRockThick);
                        else if (value > 0.728f)
                            map.roofGrid.SetRoof(cell,
                                RoofDefOf.RoofRockThin);
                    }
                    if (cell.GetEdifice(map) == null)
                    {
                        if (!preview)
                            GenSpawn.Spawn(ThingDefOf.SolidIce, cell, map);
                        map.terrainGrid.SetTerrain(cell, TerrainDefOf.Ice);
                        iced++;
                    }
                }
            }
            return carriers.Count + " carrier areas, " + iced
                + " ice-mass cells";
        }

        // Hot springs: perlin pools inside a soft radial area. Exact native
        // graph per carrying area, carrier-anchored.
        private Verse.Noise.ModuleBase HotSpringField(int carrierIndex,
            float span, Vector2 anchor)
        {
            int tileId = members[carrierIndex].tileId;
            string springDef = CarrierDefOf(carrierIndex, worker =>
                worker is TileMutatorWorker_HotSprings);
            int salt = ShapeSaltOf(Gen.HashCombineInt(tileId,
                0x53505247), tileId, springDef); // "SPRG"
            float springWanderX = ShapeOf(tileId, springDef, "wanderX",
                float.NaN);
            float springWanderZ = ShapeOf(tileId, springDef, "wanderZ",
                float.NaN);
            if (!float.IsNaN(springWanderX))
                anchor.x += springWanderX * span;
            if (!float.IsNaN(springWanderZ))
                anchor.y += springWanderZ * span;
            Verse.Noise.ModuleBase pools = new Verse.Noise.Perlin(0.05,
                1.5, 0.5, 4, Gen.HashCombineInt(salt, 1),
                Verse.Noise.QualityMode.Medium);
            pools = new Verse.Noise.ScaleBias(0.5, 0.5, pools);
            Verse.Noise.ModuleBase area =
                MapNoiseUtility.CreateFalloffRadius(span * 0.35f
                    * ShapeOf(tileId, springDef, "span", 1f), anchor,
                    0.05f);
            area = MapNoiseUtility.AddDisplacementNoise(area, 0.015f, 25f,
                1, Gen.HashCombineInt(salt, 2));
            return new Verse.Noise.Multiply(pools, area);
        }

        internal string ApplyProjectedHotSpringElevation()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null
                || kernel == null) return null;
            List<int> carriers = CarriersOf(worker =>
                worker is TileMutatorWorker_HotSprings);
            if (carriers.Count == 0) return null;
            MapGenFloatGrid elevation = MapGenerator.Elevation;
            float span = Math.Max(8f, kernel.LocalCells);
            int flattened = 0;
            foreach (int carrierIndex in carriers)
            {
                Vector2 anchor = kernel.VisualLandAnchor(
                    members[carrierIndex].tileId);
                Verse.Noise.ModuleBase field = HotSpringField(carrierIndex,
                    span, anchor);
                float pools = ShapeOf(members[carrierIndex].tileId,
                    CarrierDefOf(carrierIndex, worker =>
                        worker is TileMutatorWorker_HotSprings),
                    "pools", 0f);
                for (int index = 0; index < memberByCell.Length; index++)
                {
                    if (memberByCell[index] != carrierIndex) continue;
                    IntVec3 cell = map.cellIndices.IndexToCell(index);
                    if (field.GetValue(cell.x, 0.0, cell.z)
                        > 0.65f - pools)
                    {
                        elevation[cell] = 0f;
                        flattened++;
                    }
                }
            }
            return carriers.Count + " carrier areas, " + flattened
                + " cells flattened";
        }

        internal string ApplyProjectedHotSpringTerrain()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null
                || kernel == null) return null;
            List<int> carriers = CarriersOf(worker =>
                worker is TileMutatorWorker_HotSprings);
            if (carriers.Count == 0) return null;
            float span = Math.Max(8f, kernel.LocalCells);
            int springs = 0;
            foreach (int carrierIndex in carriers)
            {
                Vector2 anchor = kernel.VisualLandAnchor(
                    members[carrierIndex].tileId);
                Verse.Noise.ModuleBase field = HotSpringField(carrierIndex,
                    span, anchor);
                float pools = ShapeOf(members[carrierIndex].tileId,
                    CarrierDefOf(carrierIndex, worker =>
                        worker is TileMutatorWorker_HotSprings),
                    "pools", 0f);
                for (int index = 0; index < memberByCell.Length; index++)
                {
                    if (memberByCell[index] != carrierIndex) continue;
                    IntVec3 cell = map.cellIndices.IndexToCell(index);
                    float value = (float)field.GetValue(cell.x, 0.0,
                        cell.z);
                    if (value > 0.85f - pools)
                    {
                        map.terrainGrid.SetTerrain(cell,
                            TerrainDefOf.HotSpring);
                        springs++;
                    }
                    else if (value > 0.65f - pools)
                        map.terrainGrid.SetTerrain(cell,
                            GenStep_RocksFromGrid.RockDefAt(cell)
                                .building.naturalTerrain);
                }
            }
            return carriers.Count + " carrier areas, " + springs
                + " spring cells";
        }

        // Terraforming scar: native replaces the whole one-tile elevation
        // with a doubly-warped alien field. The same graph, deterministic
        // parameters drawn from the native ranges, replaces the carrying
        // area's own elevation with a faded rim.
        internal string ApplyProjectedTerraformingScars()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null
                || kernel == null) return null;
            List<int> carriers = CarriersOf(worker =>
                worker is TileMutatorWorker_TerraformingScar);
            if (carriers.Count == 0) return null;
            MapGenFloatGrid elevation = MapGenerator.Elevation;
            float span = Math.Max(8f, kernel.LocalCells);
            int rewritten = 0;
            foreach (int carrierIndex in carriers)
            {
                string scarDef = CarrierDefOf(carrierIndex, worker =>
                    worker is TileMutatorWorker_TerraformingScar);
                int salt = ShapeSaltOf(Gen.HashCombineInt(
                    members[carrierIndex].tileId, 0x53434152),
                    members[carrierIndex].tileId, scarDef); // "SCAR"
                Vector2 anchor = kernel.VisualLandAnchor(
                    members[carrierIndex].tileId);
                Verse.Noise.ModuleBase field = new Verse.Noise.Perlin(
                    Mathf.Lerp(GenStep_ElevationFertility.ElevationFreqRange
                            .min, GenStep_ElevationFertility
                            .ElevationFreqRange.max,
                        CarrierUnit(salt, 1)), 2.0, 0.5, 3,
                    Gen.HashCombineInt(salt, 2),
                    Verse.Noise.QualityMode.High);
                field = MapNoiseUtility.AddDisplacementNoise(field,
                    Mathf.Lerp(GenStep_ElevationFertility.DetailFreqRange
                        .min, GenStep_ElevationFertility.DetailFreqRange
                        .max, CarrierUnit(salt, 3)),
                    Mathf.Lerp(GenStep_ElevationFertility
                        .DetailStrengthRange.min, GenStep_ElevationFertility
                        .DetailStrengthRange.max, CarrierUnit(salt, 4)),
                    GenStep_ElevationFertility.DetailOctavesRange.min,
                    Gen.HashCombineInt(salt, 5));
                field = new Verse.Noise.Scale(2.0, 1.0, 1.0, field);
                field = new Verse.Noise.Rotate(0.0,
                    CarrierUnit(salt, 6) * 180f, 0.0, field);
                field = MapNoiseUtility.AddDisplacementNoise(field, 0.005f,
                    75f, 4, Gen.HashCombineInt(salt, 7));
                field = new Verse.Noise.Rotate(0.0, ShapeOf(
                    members[carrierIndex].tileId, scarDef, "orientation",
                    CarrierUnit(salt, 8) * 180f), 0.0, field);
                field = new Verse.Noise.ScaleBias(0.5, 0.5, field);
                field = new Verse.Noise.Multiply(field,
                    new Verse.Noise.Const(
                        MapGenTuning.ElevationFactorMountains));
                for (int index = 0; index < memberByCell.Length; index++)
                {
                    if (memberByCell[index] != carrierIndex) continue;
                    if (boundaryWaterByCell != null
                        && boundaryWaterByCell[index] >= 0) continue;
                    float localX = map.cellIndices.IndexToCell(index).x
                        - anchor.x;
                    float localZ = map.cellIndices.IndexToCell(index).z
                        - anchor.y;
                    float envelope = Mathf.Sqrt(localX * localX
                        + localZ * localZ) / (span * 0.95f);
                    if (envelope > 1.15f) continue;
                    IntVec3 cell = map.cellIndices.IndexToCell(index);
                    float value = (float)field.GetValue(cell.x, 0.0,
                        cell.z);
                    float fade = Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(0.80f, 1.15f, envelope));
                    elevation[cell] = Mathf.Lerp(value, elevation[cell],
                        fade);
                    rewritten++;
                }
            }
            return carriers.Count + " carrier areas, " + rewritten
                + " cells rewritten";
        }

        // Dune fields are stretched voronoi ridge patterns, scale-free like
        // wetland: native builds one Rand-seeded field per one-tile map.
        // Each carrying area gets its own deterministically seeded exact
        // native graph, masked to owned cells -- soft sand between sand
        // ridges, or solid ice spurs between ice ridges.
        internal string ApplyProjectedDunes()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null)
                return null;
            List<int> sandCarriers = CarriersOf(worker =>
                worker is TileMutatorWorker_Dunes);
            List<int> iceCarriers = CarriersOf(worker =>
                worker is TileMutatorWorker_IceDunes);
            // Ice dunes only spawn things, which the terrain-only preview
            // pipeline discards -- native ice dunes never showed in the
            // preview either.
            if (CARegionalCompatibility.IsMapPreviewGenerating())
                iceCarriers = new List<int>();
            if (sandCarriers.Count == 0 && iceCarriers.Count == 0)
                return null;
            int softened = 0;
            int iced = 0;
            foreach (int carrierIndex in sandCarriers)
            {
                int duneTileId = members[carrierIndex].tileId;
                string duneDef = CarrierDefOf(carrierIndex, worker =>
                    worker is TileMutatorWorker_Dunes);
                int salt = ShapeSaltOf(Gen.HashCombineInt(duneTileId,
                    0x44554E45), duneTileId, duneDef); // "DUNE"
                Verse.Noise.ModuleBase noise = new Verse.Noise.Voronoi2D(
                    0.0025, Gen.HashCombineInt(salt, 1), 0.15f, 0.5f,
                    staggered: true);
                noise = MapNoiseUtility.AddDisplacementNoise(noise, 0.015f,
                    20f, 2, Gen.HashCombineInt(salt, 2));
                noise = new Verse.Noise.Scale(1.0, 1.0, 15.0, noise);
                Verse.Noise.ModuleBase ripple = new Verse.Noise.Perlin(
                    0.02, 2.0, 0.5, 2, Gen.HashCombineInt(salt, 3),
                    Verse.Noise.QualityMode.Medium);
                ripple = new Verse.Noise.Multiply(ripple,
                    new Verse.Noise.Const(7.5));
                noise = new Verse.Noise.Displace(noise,
                    new Verse.Noise.Const(0.0), new Verse.Noise.Const(0.0),
                    ripple);
                noise = new Verse.Noise.Rotate(0.0, ShapeOf(duneTileId,
                    duneDef, "orientation", CarrierUnit(salt, 4) * 180f),
                    0.0, noise);
                for (int index = 0; index < memberByCell.Length; index++)
                {
                    if (memberByCell[index] != carrierIndex) continue;
                    IntVec3 cell = map.cellIndices.IndexToCell(index);
                    if (!(noise.GetValue(cell.x, 0.0, cell.z) > 0.2)
                        && cell.GetTerrain(map) == TerrainDefOf.Sand)
                    {
                        map.terrainGrid.SetTerrain(cell,
                            TerrainDefOf.SoftSand);
                        softened++;
                    }
                }
            }
            foreach (int carrierIndex in iceCarriers)
            {
                int iceTileId = members[carrierIndex].tileId;
                string iceDef = CarrierDefOf(carrierIndex, worker =>
                    worker is TileMutatorWorker_IceDunes);
                int salt = ShapeSaltOf(Gen.HashCombineInt(iceTileId,
                    0x49445545), iceTileId, iceDef); // "IDUE"
                Verse.Noise.ModuleBase noise = new Verse.Noise.Voronoi2D(
                    0.007, Gen.HashCombineInt(salt, 1), 0.5f,
                    staggered: true);
                noise = MapNoiseUtility.AddDisplacementNoise(noise, 0.03f,
                    10f, 3, Gen.HashCombineInt(salt, 2));
                noise = new Verse.Noise.Scale(3.0, 1.0, 1.0, noise);
                Verse.Noise.ModuleBase ripple = new Verse.Noise.Perlin(
                    0.02, 2.0, 0.5, 2, Gen.HashCombineInt(salt, 3),
                    Verse.Noise.QualityMode.Medium);
                ripple = new Verse.Noise.Multiply(ripple,
                    new Verse.Noise.Const(6.5));
                noise = new Verse.Noise.Displace(noise, ripple,
                    new Verse.Noise.Const(0.0), new Verse.Noise.Const(0.0));
                noise = new Verse.Noise.Rotate(0.0, ShapeOf(iceTileId,
                    iceDef, "orientation", CarrierUnit(salt, 4) * 180f),
                    0.0, noise);
                for (int index = 0; index < memberByCell.Length; index++)
                {
                    if (memberByCell[index] != carrierIndex) continue;
                    IntVec3 cell = map.cellIndices.IndexToCell(index);
                    // The edifice guard is a deviation from native: on a
                    // one-tile map nothing else spawns solid ice, but at
                    // regional scale an iceberg's seam-crossing walls can
                    // reach a neighboring ice-dunes carrier's cells.
                    if (!(noise.GetValue(cell.x, 0.0, cell.z) > 0.2)
                        && !cell.GetTerrain(map).IsWater
                        && cell.GetEdifice(map) == null)
                    {
                        GenSpawn.Spawn(ThingDefOf.SolidIce, cell, map);
                        iced++;
                    }
                }
            }
            return (sandCarriers.Count + iceCarriers.Count)
                + " carrier areas, " + softened + " cells softened, "
                + iced + " ice spurs";
        }

        // Native LavaFlow flattens everything but narrow rock veins and
        // cools the ground to lava rock. The same inverted ridged field is
        // applied per carrying area with a deterministic seed, masked to
        // owned cells. The worker's Tick hook (eruption incidents) stays
        // native and root-only: a member-carried flow field is projected
        // but does not schedule eruptions.
        private List<int> LavaFlowCarriers()
        {
            var carriers = new List<int>();
            for (int i = 0; i < members.Count; i++)
            {
                if (!IsSelectedCore(members[i])) continue;
                Tile info = members[i].Valid ? members[i].Tile : null;
                if (info == null) continue;
                foreach (TileMutatorDef mutator in info.Mutators)
                    if (mutator?.Worker is TileMutatorWorker_LavaFlow)
                    {
                        carriers.Add(i);
                        break;
                    }
            }
            return carriers;
        }

        private Verse.Noise.ModuleBase LavaFlowField(int carrierIndex)
        {
            int tileId = members[carrierIndex].tileId;
            Verse.Noise.ModuleBase field = new Verse.Noise.RidgedMultifractal(
                0.03, 2.0, 3, ShapeSaltOf(Gen.HashCombineInt(tileId,
                    0x464C4F57), tileId, CarrierDefOf(carrierIndex,
                    worker => worker is TileMutatorWorker_LavaFlow)),
                Verse.Noise.QualityMode.High); // "FLOW"
            field = new Verse.Noise.Clamp(0.0, 1.0, field);
            field = new Verse.Noise.Invert(field);
            return new Verse.Noise.ScaleBias(1.0, 1.0, field);
        }

        private float LavaFlowVeinThreshold(int carrierIndex)
        {
            return 0.85f - ShapeOf(members[carrierIndex].tileId,
                CarrierDefOf(carrierIndex, worker =>
                    worker is TileMutatorWorker_LavaFlow), "veins", 0f);
        }

        internal string ApplyProjectedLavaFlowElevation()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null)
                return null;
            List<int> carriers = LavaFlowCarriers();
            if (carriers.Count == 0) return null;
            MapGenFloatGrid elevation = MapGenerator.Elevation;
            int flattened = 0;
            foreach (int carrierIndex in carriers)
            {
                Verse.Noise.ModuleBase field = LavaFlowField(carrierIndex);
                float veinThreshold = LavaFlowVeinThreshold(carrierIndex);
                for (int index = 0; index < memberByCell.Length; index++)
                {
                    if (memberByCell[index] != carrierIndex) continue;
                    IntVec3 cell = map.cellIndices.IndexToCell(index);
                    if (field.GetValue(cell.x, 0.0, cell.z)
                        > veinThreshold) continue;
                    elevation[cell] = 0f;
                    flattened++;
                }
            }
            return carriers.Count + " carrier areas, " + flattened
                + " cells flattened";
        }

        internal string ApplyProjectedLavaFlowTerrain()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null)
                return null;
            List<int> carriers = LavaFlowCarriers();
            if (carriers.Count == 0) return null;
            TerrainPatchMaker patchMaker = map.Biome.terrainPatchMakers
                .FirstOrDefault(item => item.isPond);
            int cooled = 0;
            foreach (int carrierIndex in carriers)
            {
                Verse.Noise.ModuleBase field = LavaFlowField(carrierIndex);
                for (int index = 0; index < memberByCell.Length; index++)
                {
                    if (memberByCell[index] != carrierIndex) continue;
                    IntVec3 cell = map.cellIndices.IndexToCell(index);
                    TerrainDef terrain = cell.GetTerrain(map);
                    if (terrain.IsWater) continue;
                    if (terrain == TerrainDefOf.VolcanicRock)
                        map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
                    TerrainDef patch = patchMaker?.TerrainAt(cell, map,
                        cell.GetFertility(map));
                    if (patch != null)
                    {
                        map.terrainGrid.SetTerrain(cell,
                            patch == TerrainDefOf.VolcanicRock
                                ? TerrainDefOf.CooledLava : patch);
                    }
                    else if (!(field.GetValue(cell.x, 0.0, cell.z)
                            > LavaFlowVeinThreshold(carrierIndex))
                        && terrain != TerrainDefOf.LavaDeep)
                    {
                        map.terrainGrid.SetTerrain(cell,
                            TerrainDefOf.CooledLava);
                        cooled++;
                    }
                }
            }
            patchMaker?.Cleanup();
            return carriers.Count + " carrier areas, " + cooled
                + " cells cooled";
        }

        // Native Valley adds a trough-and-flanks field oriented by the
        // coast: carved toward -1 along its axis, walls rising toward +1,
        // the seaward side smoothed low so the valley opens to the water.
        // The same field is added here per carrying area, masked to the
        // cells it owns, faded at its rim, and kept out of projected water.
        internal string ApplyProjectedValleys()
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null
                || kernel == null) return null;
            var carriers = new List<int>();
            for (int i = 0; i < members.Count; i++)
            {
                if (!IsSelectedCore(members[i])) continue;
                Tile info = members[i].Valid ? members[i].Tile : null;
                if (info == null) continue;
                foreach (TileMutatorDef mutator in info.Mutators)
                    if (mutator?.Worker is TileMutatorWorker_Valley)
                    {
                        carriers.Add(i);
                        break;
                    }
            }
            if (carriers.Count == 0) return null;

            MapGenFloatGrid elevation = MapGenerator.Elevation;
            float span = Math.Max(8f, kernel.LocalCells);
            int carved = 0;
            foreach (int carrierIndex in carriers)
            {
                PlanetTile carrier = members[carrierIndex];
                string valleyDef = CarrierDefOf(carrierIndex, worker =>
                    worker is TileMutatorWorker_Valley);
                int salt = ShapeSaltOf(Gen.HashCombineInt(carrier.tileId,
                    0x56414C4C), carrier.tileId, valleyDef); // "VALL"
                float widthScale = ShapeOf(carrier.tileId, valleyDef,
                    "span", 1f);
                Vector2 center = kernel.VisualLandAnchor(carrier.tileId);
                float? coast = null;
                try
                {
                    coast = Verse.Find.World.CoastAngleAt(carrier,
                            BiomeDefOf.Ocean)
                        ?? Verse.Find.World.CoastAngleAt(carrier,
                            BiomeDefOf.Lake);
                }
                catch { }
                float angleDegrees = ShapeOf(carrier.tileId, valleyDef,
                    "orientation", coast ?? (Gen.HashCombineInt(salt, 31)
                        & 0xFFFF) / 65535f * 360f);
                float angle = angleDegrees * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                int minX = Math.Max(0,
                    Mathf.FloorToInt(center.x - span * 1.2f));
                int maxX = Math.Min(map.Size.x - 1,
                    Mathf.CeilToInt(center.x + span * 1.2f));
                int minZ = Math.Max(0,
                    Mathf.FloorToInt(center.y - span * 1.2f));
                int maxZ = Math.Min(map.Size.z - 1,
                    Mathf.CeilToInt(center.y + span * 1.2f));
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        int index = z * map.Size.x + x;
                        if (memberByCell[index] != carrierIndex) continue;
                        if (boundaryWaterByCell != null
                            && boundaryWaterByCell[index] >= 0) continue;
                        if (lakeValueByCell != null
                            && lakeValueByCell[index] > 0.5f
                            && lakeKindByCell[index]
                                != (byte)CAInlandWaterKind.DryLake) continue;
                        float localX = x - center.x
                            + (kernel.SampleWorldNoise(x, z, 0.015f,
                                Gen.HashCombineInt(salt, 11)) * 2f - 1f)
                                * 30f;
                        float localZ = z - center.y
                            + (kernel.SampleWorldNoise(x, z, 0.015f,
                                Gen.HashCombineInt(salt, 12)) * 2f - 1f)
                                * 30f;
                        float envelope = Mathf.Sqrt(localX * localX
                            + localZ * localZ) / (span * 0.95f);
                        if (envelope > 1.15f) continue;
                        float along = -sin * localX + cos * localZ;
                        float across = cos * localX + sin * localZ;
                        float value = 2f * Mathf.Pow(Mathf.Clamp01(
                            Mathf.Abs(across)
                            / (0.3f * span * widthScale)), 0.5f) - 1f;
                        if (coast.HasValue)
                            value = GenMath.SmoothMin(value,
                                Mathf.Clamp01(0.7f - along / span), 0.1f);
                        float fade = Mathf.SmoothStep(0f, 1f,
                            Mathf.InverseLerp(0.80f, 1.15f, envelope));
                        float add = value * (1f - fade);
                        if (Mathf.Abs(add) < 0.01f) continue;
                        IntVec3 cell = map.cellIndices.IndexToCell(index);
                        elevation[cell] += add;
                        if (add < -0.05f) carved++;
                    }
                }
            }
            return carriers.Count + " carrier areas, " + carved
                + " cells carved";
        }

        // Iceberg surface: ice past .35, solid ice walls past .55, and the
        // shallow ring where the berg field is barely positive -- the same
        // thresholds the native worker paints from its own field.
        internal string ApplyProjectedIcebergs()
        {
            EnsureBuilt();
            if (bergValueByCell == null) return null;
            // The preview pipeline renders terrain only; things it spawns
            // are dead work discarded with the preview map. Terrain writes
            // stay so the berg shows; walls wait for real generation.
            bool preview = CARegionalCompatibility.IsMapPreviewGenerating();
            int ice = 0;
            int walls = 0;
            for (int index = 0; index < bergValueByCell.Length; index++)
            {
                float value = bergValueByCell[index];
                if (value <= 0f) continue;
                IntVec3 cell = map.cellIndices.IndexToCell(index);
                if (value > 0.35f)
                {
                    map.terrainGrid.SetTerrain(cell, TerrainDefOf.Ice);
                    ice++;
                }
                else
                    map.terrainGrid.SetTerrain(cell,
                        MapGenUtility.ShallowOceanWaterTerrainAt(cell, map));
                if (!preview && value > 0.55f)
                {
                    GenSpawn.Spawn(ThingDefOf.SolidIce, cell, map);
                    walls++;
                }
            }
            return (kernel?.IcebergCarrierCount ?? 0) + " carrier areas, "
                + ice + " ice cells, " + walls + " ice walls";
        }

        internal string MountainFieldSummary()
        {
            EnsureBuilt();
            if (mountainAddByCell == null) return "no field";
            int carriers = members == null ? 0
                : members.Count(tile => IsSelectedCore(tile)
                    && CarriesMountain(tile));
            int raised = 0;
            int rock = 0;
            float peak = 0f;
            for (int index = 0; index < mountainAddByCell.Length; index++)
            {
                float add = mountainAddByCell[index];
                if (add <= 0.0001f) continue;
                raised++;
                if (add >= 0.7f) rock++;
                if (add > peak) peak = add;
            }
            return carriers + " mountainous source tiles; " + raised
                + " cells raised (" + (100f * raised
                    / Math.Max(1, mountainAddByCell.Length)).ToString("F1")
                + "% of map), " + rock + " at or above 0.70; peak add "
                + peak.ToString("F3");
        }

        private static bool CarriesMountain(PlanetTile tile)
        {
            if (!tile.Valid) return false;
            Tile info = tile.Tile;
            if (info == null || info.WaterCovered) return false;
            foreach (TileMutatorDef mutator in info.Mutators)
                if (mutator?.Worker is TileMutatorWorker_Mountain) return true;
            return false;
        }

        // Native reads the root tile's coast angle and falls back to
        // Rand.Range(0, 360) at genstep time. Per tile here, and the fallback
        // is anchored to the tile id so a preview and the generated map cannot
        // disagree about which way a mountain faces.
        private static float MountainAxisDegrees(PlanetTile tile,
            CAFeatureInstance instance)
        {
            float? coast = Verse.Find.World.CoastAngleAt(tile,
                    BiomeDefOf.Ocean)
                ?? Verse.Find.World.CoastAngleAt(tile, BiomeDefOf.Lake);
            if (coast.HasValue) return coast.Value;
            // Native falls back to Rand.Range(0, 360) on the ambient,
            // order-dependent generation stream. Anchored to the feature
            // instance instead, so a preview and the generated map cannot
            // disagree about which way a mountain faces.
            CAFeatureRandom.Push(instance, 1298752843);
            float degrees = Rand.Range(0, 360);
            CAFeatureRandom.Pop();
            return degrees;
        }

        internal List<ThingDef> RockTypesAt(IntVec3 cell)
        {
            EnsureBuilt();
            if (region == null || members == null || memberByCell == null
                || nearestLandMemberByCell == null
                || !cell.InBounds(map)) return EmptyRockTypes;
            int cellIndex = map.cellIndices.CellToIndex(cell);
            if ((uint)cellIndex >= (uint)memberByCell.Length
                || (uint)cellIndex >= (uint)nearestLandMemberByCell.Length)
                return EmptyRockTypes;
            int memberIndex = BiomeAtIndex(cellIndex)?.isWaterBiome == true
                ? nearestLandMemberByCell[cellIndex]
                : memberByCell[cellIndex];
            if (memberIndex < 0 || memberIndex >= members.Count)
                return EmptyRockTypes;
            PlanetTile tile = members[memberIndex];
            List<ThingDef> cached;
            if (rockTypesByTileId.TryGetValue(tile.tileId, out cached))
                return cached;
            cached = Verse.Find.World.NaturalRockTypesIn(tile).ToList();
            rockTypesByTileId.Add(tile.tileId, cached);
            return cached;
        }

        internal IEnumerable<BiomeDef> AllBiomes
        {
            get
            {
                EnsureBuilt();
                return members == null ? Enumerable.Empty<BiomeDef>()
                    : members.Concat(boundaryWaterTiles
                            ?? Enumerable.Empty<PlanetTile>())
                        .Select(tile => tile.Tile.PrimaryBiome)
                        .Where(biome => biome != null).Distinct();
            }
        }

        internal IEnumerable<int> BoundaryWaterTileIds
        {
            get
            {
                EnsureBuilt();
                return boundaryWaterTiles == null
                    ? Enumerable.Empty<int>()
                    : boundaryWaterTiles.Select(tile => tile.tileId);
            }
        }

        internal IEnumerable<ThingDef> SelectedRockTypes
        {
            get
            {
                EnsureBuilt();
                return region == null ? Enumerable.Empty<ThingDef>()
                    : region.ReservedTileIds.Select(
                            CARegionalPlanUtility.SurfaceTile)
                        .Where(tile => tile.Valid)
                        .SelectMany(tile => Verse.Find.World
                            .NaturalRockTypesIn(tile))
                        .Distinct().OrderBy(rock => rock.defName);
            }
        }

        // Resolves the map's regional plan, then aliases the shared kernel at
        // full resolution. Plan ownership remains map-bound.
        private void EnsureBuilt()
        {
            if (memberByCell != null) return;
            region = CARegionalWorldComponent.Current?.FindRegionForMap(map)
                ?? CARegionalSetupSession.PreviewPlanFor(map.Tile, map.Size);
            List<PlanetTile> coreMembers = region?.memberTileIds
                .Select(CARegionalPlanUtility.SurfaceTile)
                .Where(tile => tile.Valid).ToList()
                ?? new List<PlanetTile>();
            // A one-area realized region is canonical world state, but it is
            // not a stitched map. It must retain vanilla map generation and
            // must not pay to build the surrounding geographic halo merely
            // because a regional patch asked whether it was active.
            if (region == null || coreMembers.Count <= 1)
            {
                memberByCell = Array.Empty<int>();
                return;
            }

            kernel = CARegionalProjectionKernel.Build(
                CARegionalProjectionRequest.ForGeneration(region, map.Size,
                    map.Biome));
            CARegionalGeographyComposition composition =
                CARegionalGeographyContract.Inspect(region, kernel);
            if (!composition.IsValid)
            {
                // A prospective preview may carry compositions the durable
                // catalog would refuse (auto-derived extents above all).
                // The preview still projects what it can and states the
                // failure; only a durable generation aborts on it.
                if (CARegionalCompatibility.IsMapPreviewGenerating())
                    Log.Warning("[CA][Regional] preview composition "
                        + composition.Signature + " is outside the durable "
                        + "catalog: "
                        + string.Join("; ", composition.Failures));
                else
                    throw new InvalidOperationException("[CA][Regional] "
                        + "confirmed composition " + composition.Signature
                        + " failed at generation: "
                        + string.Join("; ", composition.Failures));
            }
            memberByCell = kernel.MemberByCell;
            if (memberByCell.Length == 0) return;
            nearestLandMemberByCell = kernel.NearestLandMemberByCell;
            boundaryWaterByCell = kernel.BoundaryWaterByCell;
            rawBiomeByCell = kernel.RawBiomeByCell;
            waterDepth = kernel.WaterDepth;
            coastValueByCell = kernel.CoastValueByCell;
            littoralFormationByCell = kernel.LittoralFormationByCell;
            hillFactorByCell = kernel.HillFactorByCell;
            lakeValueByCell = kernel.LakeValueByCell;
            lakeKindByCell = kernel.LakeKindByCell;
            bergValueByCell = kernel.BergValueByCell;
            members = kernel.Members;
            memberHillFactors = kernel.MemberHillFactors;
            boundaryWaterTiles = kernel.BoundaryWaterTiles;
            Log.Message("[CA][Regional][Timing] projection " + map.Size.x
                + "x" + map.Size.z + " map " + map.uniqueID + ": "
                + "composition " + composition.Signature + "; "
                + kernel.PerformanceSummary);
        }

    }

    public sealed class GenStep_CARegionalProjection : GenStep
    {
        public override int SeedPart => 1442069301;

        public override void Generate(Map map, GenStepParams parms)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            CARegionalProjectionMapComponent projection = map.GetComponent<
                CARegionalProjectionMapComponent>();
            if (projection?.Active != true) return;
            long buildMs = timer.ElapsedMilliseconds;
            // Preserve the vanilla hilliness multiplier cancellation.
            // GenStep_ElevationFertility multiplies the elevation noise by this
            // exact factor and writes Mathf.Min(value, float.MaxValue) on
            // non-water without an upper clamp, so CA's divide at ApplyTerrainProjection
            // inverts it and the root term vanishes. That only holds while the
            // divisor must match the value vanilla multiplied by, so it is
            // read through footprint-anchored map.TileInfo and
            // HillinessForElevationGen, including mutator overrides.
            float rootFactor = HillFactor(
                map.TileInfo.HillinessForElevationGen);
            projection.ApplyTerrainProjection(rootFactor);
            int riverReliefCells = GenStep_CARegionalWorldLinks
                .CarveRiverReliefCorridors(map, projection,
                    projection.Region);
            int sampledWaterSources = projection.BoundaryWaterTileIds.Count();
            CARegionalPlan generating = projection.Region;
            // A preview exists precisely to show an unconfirmed prospective
            // candidate; demanding confirmation here aborted every world-map
            // preview of a prospective region halfway through its own
            // projection and left a broken partial texture behind a red
            // error. Durable map generation still demands confirmation.
            bool previewGeneration =
                CARegionalCompatibility.IsMapPreviewGenerating();
            if (generating == null || generating.candidateId.NullOrEmpty()
                || (!generating.confirmed && !previewGeneration))
                throw new InvalidOperationException(
                    "[CA][Regional] generation requires a confirmed candidate "
                    + "with an identity. Return to regional setup and confirm "
                    + "the composition before starting the map.");
            // Backstop. The gate runs at confirmation; anything reaching here
            // incompatible slipped past it, so it is a defect either way.
            // Every durable start must abort and surface the failure rather
            // than quietly generate unsupported geography. Only a plan that
            // carries the explicit transient-test stamp may continue.
            CACompatibilityReport atGeneration =
                CARegionalContentCompatibility.Evaluate(generating);
            if (!atGeneration.IsValid)
            {
                string failure = "[CA][Regional] compatibility gate failed at "
                    + "GENERATION for candidate "
                    + (generating?.candidateId ?? "(no id)") + " - "
                    + atGeneration.ActionableFailure() + " ("
                    + atGeneration.Summary() + ")";
                bool transientTest = generating?.developerExercise == true
                    || previewGeneration;
                if (!transientTest)
                    throw new InvalidOperationException(failure
                        + " Generation aborted: durable regional generation "
                        + "requires ProductionSupported content.");
                // This is the outcome the preceding confirmation explicitly
                // authorised. Logging it as an error would auto-open RimWorld's
                // dev log over the map and make the successful test route look
                // like another failed Start.
                Log.Warning(failure + " Continuing as the explicitly "
                    + "accepted transient test; saving is disabled and this "
                    + "result is evidence, not a durable-world compatibility "
                    + "claim.");
            }
            Log.Message("[CA][Regional] compatibility: "
                + atGeneration.Summary());
            Log.Message("[CA][Regional] candidate "
                + generating.candidateId + " confirmed"
                + " is generating; feature identities and seeds derive from it. "
                + "Preview-to-generation equivalence is NOT established by this "
                + "- it requires both to consume the same execution contracts "
                + "and remains unverified.");
            Log.Message("[CA][Regional] continuously projected "
                + (projection.ProjectedSourceCount + sampledWaterSources)
                + " geographic sampling tiles (land "
                + projection.ProjectedSourceCount + ", water "
                + sampledWaterSources + "; "
                + projection.Region.memberTileIds.Count + " selected core) at "
                + projection.Region.mapSize
                + " local cells each onto aggregate " + map.Size.x + "x"
                + map.Size.z + " map " + map.uniqueID + "; landing member "
                + projection.Region.startTileId + "; sampling tile ids "
                + projection.GeographicSourceTileIds.Select(id =>
                    id.ToString()).ToCommaList() + "; frame "
                + projection.ProjectionFrameSummary + "; biomes "
                + projection.AllBiomes.Select(biome => biome.defName)
                    .ToCommaList() + "; perimeter water neighbors "
                + projection.BoundaryWaterTileIds.Select(id => id.ToString())
                    .ToCommaList() + "; vanilla-profile beach cells "
                + projection.LittoralSandCells + "; shallow-water depth "
                + projection.NativeShallowWaterDepth + "; "
                + projection.DiagnosticSummary + "; timing "
                + projection.PerformanceSummary + "; elevation application "
                + (timer.ElapsedMilliseconds - buildMs) + " ms; genstep total "
                + timer.ElapsedMilliseconds + " ms; river-valley relief cells "
                + riverReliefCells);
        }

        private static float HillFactor(Hilliness hilliness)
        {
            switch (hilliness)
            {
                case Hilliness.Flat: return MapGenTuning.ElevationFactorFlat;
                case Hilliness.SmallHills:
                    return MapGenTuning.ElevationFactorSmallHills;
                case Hilliness.LargeHills:
                    return MapGenTuning.ElevationFactorLargeHills;
                case Hilliness.Mountainous:
                    return MapGenTuning.ElevationFactorMountains;
                case Hilliness.Impassable:
                    return MapGenTuning.ElevationFactorImpassableMountains;
                default: return 1f;
            }
        }
    }

    public sealed class GenStep_CARegionalPlayerStart : GenStep
    {
        public override int SeedPart => 1779223461;

        public override void Generate(Map map, GenStepParams parms)
        {
            CARegionalProjectionMapComponent projection = map.GetComponent<
                CARegionalProjectionMapComponent>();
            CARegionalPlan region = projection?.Region;
            if (region == null || !region.memberTileIds.Contains(
                    region.startTileId)) return;
            IntVec3 desired = projection.CenterForMember(region.startTileId);
            IntVec3 best = IntVec3.Invalid;
            float bestDistance = float.MaxValue;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(desired,
                90f, true))
            {
                if (!cell.InBounds(map) || cell.Roofed(map)
                    || cell.GetEdifice(map) != null
                    || !cell.GetAffordances(map)
                        .Contains(TerrainAffordanceDefOf.Heavy)
                    || MapGenerator.UsedRects.Any(rect => rect.Contains(cell)))
                    continue;
                float distance = cell.DistanceToSquared(desired);
                if (distance >= bestDistance) continue;
                best = cell;
                bestDistance = distance;
            }
            if (best.IsValid)
            {
                MapGenerator.PlayerStartSpot = best;
                Log.Message("[CA][Regional] landing anchor tile "
                    + region.startTileId + " projected to cell " + best);
            }
            else
                Log.Warning("[CA][Regional] no valid landing cell near member "
                    + "tile " + region.startTileId + "; native start search will "
                    + "choose a fallback");
        }
    }

    public sealed class GenStep_CARegionalWorldLinks : GenStep
    {
        private sealed class Link
        {
            internal PlanetTile From;
            internal PlanetTile To;
            internal RoadDef Road;
            internal RiverDef River;
        }

        public override int SeedPart => 1940613879;

        internal static int CarveRiverReliefCorridors(Map map,
            CARegionalProjectionMapComponent projection, CARegionalPlan region)
        {
            if (map == null || projection == null || region == null)
                return 0;
            List<Link> links = CollectLinks(
                projection.GeographicSourceTileIds);
            if (links.Count == 0) return 0;

            var changed = new bool[map.cellIndices.NumGridCells];
            int changedCount = 0;
            foreach (Link link in links.Where(item => item.River != null))
            {
                if (!Endpoints(map, projection, region, link,
                        out IntVec3 start, out IntVec3 end)) continue;
                float riverRadius = Mathf.Clamp(
                    link.River.widthOnMap * 0.5f, 1.5f, 15f);
                int linkSeed = RiverLinkSeed(link);
                List<IntVec3> spine = BuildRiverCenterline(map, projection,
                    link, start, end, riverRadius, linkSeed);
                if (spine.Count < Math.Max(8,
                        Mathf.CeilToInt(riverRadius * 2f))) continue;

                float valleyRadius = Mathf.Clamp(riverRadius * 2.35f + 5f,
                    9f, 42f);
                int reliefStride = Mathf.Max(2,
                    Mathf.RoundToInt(riverRadius * 0.35f));
                for (int i = 0; i < spine.Count; i += reliefStride)
                {
                    IntVec3 point = spine[i];
                    if (!point.InBounds(map)) continue;
                    if (projection.BiomeAt(point)?.isWaterBiome == true)
                        break;
                    foreach (IntVec3 cell in GenRadial.RadialCellsAround(point,
                        valleyRadius, true))
                    {
                        if (!cell.InBounds(map)
                            || projection.BiomeAt(cell)?.isWaterBiome == true)
                            continue;
                        float normalized = Mathf.Clamp01(
                            cell.DistanceTo(point) / valleyRadius);
                        float centerWeight = 1f - Mathf.SmoothStep(0f, 1f,
                            normalized);
                        float elevationCeiling = Mathf.Lerp(0.64f, 0.44f,
                            centerWeight * centerWeight);
                        if (MapGenerator.Elevation[cell]
                                <= elevationCeiling) continue;
                        MapGenerator.Elevation[cell] = elevationCeiling;
                        MapGenerator.Caves[cell] = 0f;
                        int cellIndex = map.cellIndices.CellToIndex(cell);
                        if (!changed[cellIndex])
                        {
                            changed[cellIndex] = true;
                            changedCount++;
                        }
                    }
                }
            }
            return changedCount;
        }

        public override void Generate(Map map, GenStepParams parms)
        {
            CARegionalProjectionMapComponent projection = map.GetComponent<
                CARegionalProjectionMapComponent>();
            CARegionalPlan region = projection?.Region;
            if (region == null || region.memberTileIds.Count < 2) return;
            List<Link> links = CollectLinks(projection.GeographicSourceTileIds);
            if (links.Count == 0) return;

            int rivers = 0;
            int discoveredRiverLinks = 0;
            var projectedIds = new HashSet<int>(region.ReservedTileIds);
            foreach (Link link in links.Where(item => item.River != null))
            {
                discoveredRiverLinks++;
                bool painted = DrawRiver(map, projection, region, link);
                if (painted) rivers++;
                int selectedEndpoints = (projectedIds.Contains(
                        link.From.tileId) ? 1 : 0)
                    + (projectedIds.Contains(link.To.tileId) ? 1 : 0);
                Log.Message("[CA][Regional] river link "
                    + link.River.defName + " " + link.From.tileId + " -> "
                    + link.To.tileId + "; " + (selectedEndpoints == 2
                        ? "inside footprint" : selectedEndpoints == 1
                            ? "crosses footprint perimeter"
                            : "outside-footprint geographic sample")
                    + "; " + (painted ? "painted" : "no land channel painted"));
            }

            int roads = 0;
            if (links.Any(item => item.Road != null))
            {
                map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
                foreach (Link link in links.Where(item => item.Road != null))
                {
                    if (DrawRoad(map, projection, region, link)) roads++;
                }
            }
            Log.Message("[CA][Regional] projected world links for region "
                + region.regionalId + ": roads " + roads + ", rivers "
                + rivers + "/" + discoveredRiverLinks + " painted");
        }

        public override void PostMapInitialized(Map map, GenStepParams parms)
        {
            base.PostMapInitialized(map, parms);
            CARegionalProjectionMapComponent projection = map?.GetComponent<
                CARegionalProjectionMapComponent>();
            if (projection?.Active != true
                || map.waterInfo.riverGraph.NullOrEmpty()) return;

            int valueCount = map.Size.x * map.Size.z * 2;
            map.waterInfo.riverFlowMap = Enumerable.Repeat(0f, valueCount)
                .ToList();
            int riverCells = 0;
            foreach (IntVec3 cell in map.AllCells)
            {
                if (cell.GetTerrain(map)?.IsRiver != true) continue;
                RiverNode closest = null;
                float closestDistance = float.MaxValue;
                Vector2 point = new Vector2(cell.x + 0.5f, cell.z + 0.5f);
                foreach (RiverNode node in map.waterInfo.riverGraph)
                {
                    Vector2 start = new Vector2(node.start.x, node.start.z);
                    Vector2 end = new Vector2(node.end.x, node.end.z);
                    Vector2 segment = end - start;
                    float denominator = Vector2.Dot(segment, segment);
                    if (denominator <= 0.001f) continue;
                    float t = Mathf.Clamp01(Vector2.Dot(point - start,
                        segment) / denominator);
                    float distance = (point - (start + segment * t))
                        .sqrMagnitude;
                    if (distance >= closestDistance) continue;
                    closestDistance = distance;
                    closest = node;
                }
                if (closest == null) continue;
                Vector2 direction = new Vector2(
                    closest.end.x - closest.start.x,
                    closest.end.z - closest.start.z).normalized;
                int index = (cell.x * map.Size.z + cell.z) * 2;
                map.waterInfo.riverFlowMap[index] = direction.x;
                map.waterInfo.riverFlowMap[index + 1] = direction.y;
                riverCells++;
            }
            Log.Message("[CA][Regional] rebuilt projected river flow for "
                + riverCells + " cells on region "
                + projection.Region.regionalId);
        }

        private static List<Link> CollectLinks(IEnumerable<int> sourceTileIds)
        {
            var links = new List<Link>();
            var seenRoads = new HashSet<string>();
            var seenRivers = new HashSet<string>();
            foreach (int id in (sourceTileIds ?? Enumerable.Empty<int>())
                .Distinct().OrderBy(value => value))
            {
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(id);
                SurfaceTile surface = tile.Valid ? tile.Tile as SurfaceTile
                    : null;
                if (surface == null) continue;
                if (surface.Roads != null)
                {
                    foreach (SurfaceTile.RoadLink road in surface.Roads)
                    {
                        string key = PairKey(id, road.neighbor.tileId,
                            road.road?.defName);
                        if (road.road == null || !seenRoads.Add(key)) continue;
                        PlanetTile first = tile.tileId <= road.neighbor.tileId
                            ? tile : road.neighbor;
                        PlanetTile second = tile.tileId <= road.neighbor.tileId
                            ? road.neighbor : tile;
                        links.Add(new Link
                        {
                            From = first,
                            To = second,
                            Road = road.road
                        });
                    }
                }
                if (surface.Rivers != null)
                {
                    foreach (SurfaceTile.RiverLink river in surface.Rivers)
                    {
                        string key = PairKey(id, river.neighbor.tileId,
                            river.river?.defName);
                        if (river.river == null || !seenRivers.Add(key)) continue;
                        SurfaceTile neighborSurface = river.neighbor.Valid
                            ? river.neighbor.Tile as SurfaceTile : null;
                        bool tileWater = surface.PrimaryBiome?.isWaterBiome
                            == true;
                        bool neighborWater = neighborSurface?.PrimaryBiome
                            ?.isWaterBiome == true;
                        bool tileUpstream = neighborSurface == null
                            || surface.riverDist > neighborSurface.riverDist
                            || (surface.riverDist == neighborSurface.riverDist
                                && ((!tileWater && neighborWater)
                                    || (tileWater == neighborWater
                                        && tile.tileId
                                            < river.neighbor.tileId)));
                        PlanetTile first = tileUpstream
                            ? tile : river.neighbor;
                        PlanetTile second = tileUpstream
                            ? river.neighbor : tile;
                        links.Add(new Link
                        {
                            From = first,
                            To = second,
                            River = river.river
                        });
                    }
                }
            }
            return links;
        }

        private static string PairKey(int left, int right, string defName)
        {
            return Math.Min(left, right) + ":" + Math.Max(left, right)
                + ":" + (defName ?? "none");
        }

        private static bool Endpoints(Map map,
            CARegionalProjectionMapComponent projection, CARegionalPlan region,
            Link link, out IntVec3 start, out IntVec3 end)
        {
            PlanetTile first = link.From;
            PlanetTile second = link.To;
            bool firstWater = first.Tile.PrimaryBiome?.isWaterBiome == true;
            bool secondWater = second.Tile.PrimaryBiome?.isWaterBiome == true;
            var footprint = new HashSet<int>(region.ReservedTileIds);
            bool firstInside = footprint.Contains(first.tileId);
            bool secondInside = footprint.Contains(second.tileId);
            if (link.River == null && ((firstWater && !secondWater)
                || (firstInside != secondInside && secondInside)))
            {
                PlanetTile swap = first;
                first = second;
                second = swap;
            }
            Vector2 from = projection.ProjectTilePoint(first);
            Vector2 to = projection.ProjectTilePoint(second);
            if (!ClipSegmentToMap(map, ref from, ref to))
            {
                start = IntVec3.Invalid;
                end = IntVec3.Invalid;
                return false;
            }
            start = new IntVec3(Mathf.Clamp(Mathf.RoundToInt(from.x), 0,
                map.Size.x - 1), 0, Mathf.Clamp(Mathf.RoundToInt(from.y), 0,
                map.Size.z - 1));
            end = new IntVec3(Mathf.Clamp(Mathf.RoundToInt(to.x), 0,
                map.Size.x - 1), 0, Mathf.Clamp(Mathf.RoundToInt(to.y), 0,
                map.Size.z - 1));
            return start != end;
        }

        private static bool ClipSegmentToMap(Map map, ref Vector2 start,
            ref Vector2 end)
        {
            float t0 = 0f;
            float t1 = 1f;
            Vector2 delta = end - start;
            if (!Clip(-delta.x, start.x, ref t0, ref t1)
                || !Clip(delta.x, map.Size.x - 1f - start.x,
                    ref t0, ref t1)
                || !Clip(-delta.y, start.y, ref t0, ref t1)
                || !Clip(delta.y, map.Size.z - 1f - start.y,
                    ref t0, ref t1)) return false;
            Vector2 original = start;
            start = original + delta * t0;
            end = original + delta * t1;
            return true;
        }

        private static bool Clip(float denominator, float numerator,
            ref float t0, ref float t1)
        {
            if (Mathf.Abs(denominator) < 0.0001f) return numerator >= 0f;
            float value = numerator / denominator;
            if (denominator < 0f)
            {
                if (value > t1) return false;
                if (value > t0) t0 = value;
            }
            else
            {
                if (value < t0) return false;
                if (value < t1) t1 = value;
            }
            return true;
        }

        private static bool DrawRiver(Map map,
            CARegionalProjectionMapComponent projection, CARegionalPlan region,
            Link link)
        {
            if (!Endpoints(map, projection, region, link, out IntVec3 start,
                    out IntVec3 end)) return false;
            float radius = Mathf.Clamp(link.River.widthOnMap * 0.5f,
                1.5f, 15f);
            int linkSeed = RiverLinkSeed(link);
            List<IntVec3> spine = BuildRiverCenterline(map, projection, link,
                start, end, radius, linkSeed);
            if (spine.Count < 2) return false;

            // A world river may point at an ocean or lake neighbor. Its map
            // channel ends at the first projected shoreline; continuing the
            // moving-water terrain through shallow and deep marine water makes
            // the world link visible as an artificial stripe across the sea.
            int firstMarineIndex = -1;
            for (int i = 0; i < spine.Count; i++)
            {
                IntVec3 cell = spine[i];
                if (!cell.InBounds(map)
                    || projection.BiomeAt(cell)?.isWaterBiome != true)
                    continue;
                firstMarineIndex = i;
                break;
            }
            int lastChannelIndex = firstMarineIndex >= 0
                ? firstMarineIndex - 1 : spine.Count - 1;
            // A painted river needs at least one directed segment. A lone
            // shoreline cell cannot own flow and would otherwise borrow the
            // nearest unrelated river node during post-initialization.
            int minimumVisibleChannel = Mathf.Max(8,
                Mathf.CeilToInt(radius * 2f));
            if (lastChannelIndex + 1 < minimumVisibleChannel)
            {
                Log.Warning("[CA][Regional] rejected river "
                    + link.River.defName + " " + link.From.tileId + " -> "
                    + link.To.tileId + "; clipped channel contained only "
                    + (lastChannelIndex + 1) + " land cells (minimum visible "
                    + minimumVisibleChannel + ")");
                return false;
            }
            IntVec3 channelEnd = spine[lastChannelIndex];
            AddRiverGraphSegments(map, spine, lastChannelIndex,
                link.River.widthOnMap, linkSeed);
            int estuaryLength = Mathf.Clamp(
                Mathf.RoundToInt(radius * 3f), 8, 36);
            int estuaryStart = Math.Max(0,
                lastChannelIndex - estuaryLength);
            int marineCellsPreserved = 0;
            for (int i = 0; i <= lastChannelIndex; i++)
            {
                IntVec3 point = spine[i];

                // Rivers broaden on their landward approach to the mouth but
                // never replace the projected ocean/lake terrain itself.
                float mouthProgress = firstMarineIndex < 0
                    || i < estuaryStart ? 0f
                    : Mathf.InverseLerp(estuaryStart,
                        Math.Max(estuaryStart + 1, lastChannelIndex), i);
                float localRadius = radius * Mathf.Lerp(1f, 1.55f,
                    mouthProgress * mouthProgress);
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(point,
                    localRadius, true))
                {
                    if (!cell.InBounds(map)) continue;
                    if (projection.BiomeAt(cell)?.isWaterBiome == true)
                    {
                        marineCellsPreserved++;
                        continue;
                    }
                    float distance = cell.DistanceTo(point);
                    TerrainDef terrain = distance < localRadius * 0.42f
                        ? TerrainDefOf.WaterMovingChestDeep
                        : TerrainDefOf.WaterMovingShallow;
                    Building edifice = cell.GetEdifice(map);
                    if (edifice != null && edifice.def.IsNonResourceNaturalRock)
                        edifice.Destroy();
                    map.roofGrid.SetRoof(cell, null);
                    map.terrainGrid.SetTerrain(cell, terrain);
                }
            }
            if (firstMarineIndex >= 0)
                Log.Message("[CA][Regional] river " + link.River.defName
                    + " reached projected shoreline at " + channelEnd
                    + "; preserved " + marineCellsPreserved
                    + " marine terrain samples beyond its widening mouth");
            Log.Message("[CA][Regional] river geometry "
                + link.River.defName + " " + link.From.tileId + " -> "
                + link.To.tileId + "; clipped cells " + start + " -> "
                + end + "; spine " + spine.Count + " cells; land channel "
                + (lastChannelIndex + 1) + " cells ending " + channelEnd
                + "; seed " + linkSeed);
            return true;
        }

        private static int RiverLinkSeed(Link link)
        {
            return Gen.HashCombineInt(
                Gen.HashCombineInt(Verse.Find.World.info.Seed,
                    Gen.HashCombineInt(Math.Min(link.From.tileId,
                            link.To.tileId),
                        Math.Max(link.From.tileId, link.To.tileId))),
                GenText.StableStringHash(link.River.defName));
        }

        private static List<IntVec3> BuildRiverCenterline(Map map,
            CARegionalProjectionMapComponent projection, Link link,
            IntVec3 start, IntVec3 end, float radius, int linkSeed)
        {
            // GenSight owns one shared temporary list. Copy it before the
            // nested bridge calls below, or the first bridge clears the line
            // being iterated and silently collapses a river to two cells.
            List<IntVec3> straight = GenSight.BresenhamCellsBetween(start, end)
                .ToList();
            var result = new List<IntVec3>(straight.Count * 2);
            Vector2 canonicalStart = projection.ProjectTilePoint(link.From);
            Vector2 canonicalEnd = projection.ProjectTilePoint(link.To);
            Vector2 canonicalDelta = canonicalEnd - canonicalStart;
            Vector2 perpendicular = new Vector2(-canonicalDelta.y,
                canonicalDelta.x).normalized;
            float canonicalLengthSquared = Mathf.Max(0.001f,
                canonicalDelta.sqrMagnitude);
            IntVec3 previous = IntVec3.Invalid;
            for (int sourceIndex = 0; sourceIndex < straight.Count;
                sourceIndex++)
            {
                IntVec3 source = straight[sourceIndex];
                Vector2 sourcePoint = new Vector2(source.x, source.z);
                float linkProgress = Mathf.Clamp01(Vector2.Dot(sourcePoint
                    - canonicalStart, canonicalDelta)
                    / canonicalLengthSquared);
                float macroWobble = projection.WorldLinkNoiseAt(link.From,
                    link.To, linkProgress, 0.0065f,
                    Gen.HashCombineInt(linkSeed, 82171)) * 2f - 1f;
                float detailWobble = projection.WorldLinkNoiseAt(link.From,
                    link.To, linkProgress, 0.018f,
                    Gen.HashCombineInt(linkSeed, 99277)) * 2f - 1f;
                float wobble = macroWobble * 0.76f
                    + detailWobble * 0.24f;
                float endpointFade = Mathf.Sin(Mathf.PI * linkProgress);
                float meanderReach = Mathf.Clamp(straight.Count * 0.035f
                    + radius * 0.5f, 6f, 28f);
                float lateralOffset = wobble * endpointFade
                    * meanderReach;
                IntVec3 displaced = new IntVec3(Mathf.Clamp(
                    Mathf.RoundToInt(source.x
                        + perpendicular.x * lateralOffset), 0,
                    map.Size.x - 1), 0, Mathf.Clamp(Mathf.RoundToInt(
                        source.z + perpendicular.y * lateralOffset), 0,
                    map.Size.z - 1));
                if (!previous.IsValid)
                {
                    result.Add(displaced);
                    previous = displaced;
                    continue;
                }
                List<IntVec3> bridge = GenSight.BresenhamCellsBetween(
                    previous, displaced).ToList();
                for (int i = 1; i < bridge.Count; i++)
                {
                    IntVec3 cell = bridge[i];
                    if (result.Count == 0 || result[result.Count - 1] != cell)
                        result.Add(cell);
                }
                previous = displaced;
            }
            return result;
        }

        private static void AddRiverGraphSegments(Map map,
            List<IntVec3> spine, int lastChannelIndex, float width,
            int linkSeed)
        {
            const int segmentLength = 8;
            int startIndex = 0;
            int segment = 0;
            while (startIndex < lastChannelIndex)
            {
                int endIndex = Math.Min(lastChannelIndex,
                    startIndex + segmentLength);
                int seed = Gen.HashCombineInt(linkSeed, segment++);
                RiverNode node;
                Rand.PushState(seed);
                try
                {
                    node = new RiverNode
                    {
                        start = spine[startIndex].ToVector3Shifted(),
                        end = spine[endIndex].ToVector3Shifted(),
                        width = width,
                        seed = seed
                    };
                }
                finally
                {
                    Rand.PopState();
                }
                map.waterInfo.riverGraph.Add(node);
                startIndex = endIndex;
            }
        }

        private static bool DrawRoad(Map map,
            CARegionalProjectionMapComponent projection, CARegionalPlan region,
            Link link)
        {
            if (!Endpoints(map, projection, region, link, out IntVec3 start,
                    out IntVec3 end)) return false;
            List<IntVec3> cells;
            PawnPath path = map.pathFinder?.FindPathNow(start, end,
                TraverseParms.For(TraverseMode.PassAllDestroyableThings),
                PathFinderCostTuning.DefaultTuning, PathEndMode.OnCell);
            if (path == null)
            {
                cells = GenSight.BresenhamCellsBetween(start, end);
            }
            else
            {
                if (!path.Found)
                {
                    path.ReleaseToPool();
                    return false;
                }
                cells = path.NodesReversed.ToList();
                path.ReleaseToPool();
            }
            TerrainDef roadTerrain = CoreRoadTerrain(link.Road, map,
                link.From);
            if (roadTerrain == null) return false;
            float radius = Mathf.Clamp(link.Road.priority / 20f, 1f, 4f);
            foreach (IntVec3 point in cells)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(point,
                    radius, true))
                {
                    if (!cell.InBounds(map) || cell.GetEdifice(map) != null)
                        continue;
                    TerrainDef existing = cell.GetTerrain(map);
                    if (existing?.IsWater == true)
                    {
                        if (existing == TerrainDefOf.WaterDeep)
                            map.terrainGrid.SetTerrain(cell,
                                TerrainDefOf.WaterShallow);
                        else if (existing == TerrainDefOf.WaterOceanDeep)
                            map.terrainGrid.SetTerrain(cell,
                                TerrainDefOf.WaterOceanShallow);
                        if (GenConstruct.CanBuildOnTerrain(TerrainDefOf.Bridge,
                                cell, map, Rot4.North))
                            map.terrainGrid.SetTerrain(cell,
                                TerrainDefOf.Bridge);
                    }
                    else if (GenConstruct.CanBuildOnTerrain(roadTerrain,
                        cell, map, Rot4.North))
                        map.terrainGrid.SetTerrain(cell, roadTerrain);
                    if (map.roadInfo != null && cell.OnEdge(map)
                        && !map.roadInfo.roadEdgeTiles.Contains(cell))
                        map.roadInfo.roadEdgeTiles.Add(cell);
                }
            }
            return true;
        }

        private static TerrainDef CoreRoadTerrain(RoadDef road, Map map,
            PlanetTile tile)
        {
            RoadDefGenStep_Place best = road?.roadGenSteps
                ?.OfType<RoadDefGenStep_Place>()
                .Where(step => step.place is TerrainDef terrain
                    && terrain != TerrainDefOf.Bridge)
                .OrderByDescending(step => step.chancePerPositionCurve
                    ?.Evaluate(0f) ?? 0f).FirstOrDefault();
            TerrainDef terrainDef = best?.place as TerrainDef;
            if (terrainDef == TerrainDefOf.FlagstoneSandstone)
                terrainDef = BaseGenUtility.RegionalRockTerrainDef(tile,
                    false);
            return terrainDef ?? TerrainDefOf.PackedDirt;
        }
    }

    [HarmonyPatch(typeof(Page_SelectStartingSite), nameof(
        Page_SelectStartingSite.PostOpen))]
    internal static class CARegionalLandingPageOpenPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Page_SelectStartingSite __instance)
        {
            CARegionalSetupSession.Begin(__instance);
        }
    }

    [HarmonyPatch(typeof(Page_SelectStartingSite), nameof(
        Page_SelectStartingSite.PostClose))]
    internal static class CARegionalLandingPageClosePatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            CARegionalPreviewDock.Release();
        }
    }

    [HarmonyPatch(typeof(Page_SelectStartingSite), nameof(
        Page_SelectStartingSite.DoWindowContents))]
    internal static class CARegionalLandingPageSelectionPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            CARegionalSetupSession.ObserveSelection();
        }
    }

    [HarmonyPatch(typeof(Page_SelectStartingSite), nameof(
        Page_SelectStartingSite.ExtraOnGUI))]
    internal static class CARegionalLandingPageGuiPatch
    {
        // The live globe stays whole and vivid - it IS the authoring
        // surface. What goes: the stock page chrome (title, beveled
        // bottom row, tutorial arrow), replaced by progression inside
        // CA's own column, drawn by the postfix along with the direct
        // composition and arrival interactions on the world itself.
        [HarmonyPrefix]
        private static bool Prefix(Page_SelectStartingSite __instance)
        {
            return !CAOpeningAugments.BeginPageFrame(__instance);
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            CAOpeningAugments.DrawLanding();
        }
    }

    [HarmonyPatch(typeof(Page_SelectStartingSite), "CanDoNext")]
    internal static class CARegionalLandingPageValidationPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Page_SelectStartingSite __instance)
        {
            if (__instance.next is Page_CAStartingRegion)
                CARegionalSetupSession.AlignVanillaLandingSelection();
        }

        [HarmonyPostfix]
        private static void Postfix(Page_SelectStartingSite __instance,
            ref bool __result)
        {
            if (!__result) return;
            // The full Starting Region page owns settlement and faction
            // validation. Leaving the globe only requires a valid footprint;
            // otherwise a half-edited faction could lock the player out of the
            // very page where it is repaired.
            if (__instance.next is Page_CAStartingRegion)
                __result = CARegionalSetupSession.PendingForCurrentWorld
                    != null;
            else
                __result = CARegionalSetupSession.PrepareNext(false);
        }
    }

    [HarmonyPatch(typeof(Page_SelectStartingSite), "DoNext")]
    internal static class CARegionalLandingPageNextPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Page_SelectStartingSite __instance)
        {
            if (__instance.next is Page_CAStartingRegion)
            {
                CARegionalSetupSession.AlignVanillaLandingSelection();
                return true;
            }
            return CARegionalSetupSession.PrepareNext(true);
        }
    }

    [HarmonyPatch(typeof(MapGenUtility), nameof(MapGenUtility.BiomeAt))]
    internal static class CARegionalBiomeAtPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Map map, IntVec3 c,
            ref BiomeDef __result)
        {
            CARegionalPlantSpatialIndex ecology =
                CARegionalPlantClusterIndexLifecyclePatch.Active;
            CARegionalProjectionMapComponent projection = ecology != null
                    && ecology.Owns(map)
                ? ecology.Projection
                : map?.GetComponent<CARegionalProjectionMapComponent>();
            if (projection?.Active != true) return true;
            __result = projection.BiomeAt(c);
            return false;
        }
    }

    [HarmonyPatch(typeof(MapGenUtility), nameof(MapGenUtility.IsMixedBiome))]
    internal static class CARegionalMixedBiomePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Map map, ref bool __result)
        {
            if (map?.GetComponent<CARegionalProjectionMapComponent>()
                    ?.Active == true) __result = true;
        }
    }

    [HarmonyPatch(typeof(Map), nameof(Map.Biomes), MethodType.Getter)]
    internal static class CARegionalMapBiomesPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Map __instance,
            ref IEnumerable<BiomeDef> __result)
        {
            CARegionalProjectionMapComponent projection = __instance
                ?.GetComponent<CARegionalProjectionMapComponent>();
            if (projection?.Active == true) __result = projection.AllBiomes;
        }
    }

    [HarmonyPatch(typeof(MapGenUtility), nameof(MapGenUtility.TerrainFrom))]
    internal static class CARegionalWaterTerrainPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(IntVec3 c, Map map,
            ref TerrainDef __result)
        {
            CARegionalProjectionMapComponent projection = map?.GetComponent<
                CARegionalProjectionMapComponent>();
            if (projection?.Active != true) return true;
            BiomeDef biome = projection.BiomeAt(c);
            if (biome?.isWaterBiome != true) return true;
            bool ocean = biome == BiomeDefOf.Ocean;
            bool deep = projection.CoastValueAt(c) < 0.4f;
            __result = ocean
                ? (deep ? MapGenUtility.DeepOceanWaterTerrainAt(c, map)
                    : MapGenUtility.ShallowOceanWaterTerrainAt(c, map))
                : (deep ? MapGenUtility.DeepFreshWaterTerrainAt(c, map)
                    : MapGenUtility.ShallowFreshWaterTerrainAt(c, map));
            return false;
        }

        [HarmonyPostfix]
        private static void Postfix(IntVec3 c, Map map,
            ref TerrainDef __result)
        {
            CARegionalProjectionMapComponent projection = map?.GetComponent<
                CARegionalProjectionMapComponent>();
            if (projection?.Active != true || __result == null
                || __result.IsWater || __result.IsIce
                || __result.categoryType
                    == TerrainDef.TerrainCategoryType.Stone
                || projection.LittoralFormationAt(c)
                    != CALittoralFormation.VanillaBeach) return;
            __result = projection.LittoralTerrainAt(c);
        }
    }

    [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateMap))]
    internal static class CARegionalMapGenerationTimingPatch
    {
        [HarmonyPrefix]
        private static void Prefix(IntVec3 mapSize,
            out System.Diagnostics.Stopwatch __state)
        {
            __state = System.Diagnostics.Stopwatch.StartNew();
        }

        [HarmonyPostfix]
        private static void Postfix(Map __result,
            System.Diagnostics.Stopwatch __state)
        {
            CARegionalProjectionMapComponent projection = __result
                ?.GetComponent<CARegionalProjectionMapComponent>();
            if (projection?.Active != true) return;
            Log.Message("[CA][Regional][Timing] complete map generation "
                + __result.Size.x + "x" + __result.Size.z + " map "
                + __result.uniqueID + " took " + __state.ElapsedMilliseconds
                + " ms; regional projection "
                + projection.PerformanceSummary);
        }
    }

    [HarmonyPatch(typeof(MapGenerator),
        nameof(MapGenerator.GenerateContentsIntoMap))]
    internal static class CARegionalMapContentsTimingPatch
    {
        [HarmonyPrefix]
        private static void Prefix(out System.Diagnostics.Stopwatch __state)
        {
            __state = System.Diagnostics.Stopwatch.StartNew();
        }

        [HarmonyPostfix]
        private static void Postfix(Map map,
            System.Diagnostics.Stopwatch __state)
        {
            CARegionalProjectionMapComponent projection = map?.GetComponent<
                CARegionalProjectionMapComponent>();
            if (projection?.Active != true) return;
            Log.Message("[CA][Regional][Timing] ordered map gensteps for "
                + map.Size.x + "x" + map.Size.z + " map " + map.uniqueID
                + " took " + __state.ElapsedMilliseconds + " ms");
        }
    }

    [HarmonyPatch(typeof(Map), nameof(Map.FinalizeInit))]
    internal static class CARegionalMapFinalizeTimingPatch
    {
        [ThreadStatic] private static Stack<Map> finalizingRegionalMaps;

        internal static bool IsFinalizing(Map map)
        {
            return map != null && finalizingRegionalMaps != null
                && finalizingRegionalMaps.Count > 0
                && ReferenceEquals(finalizingRegionalMaps.Peek(), map);
        }

        [HarmonyPrefix]
        private static void Prefix(Map __instance,
            out System.Diagnostics.Stopwatch __state)
        {
            __state = System.Diagnostics.Stopwatch.StartNew();
            if (finalizingRegionalMaps == null)
                finalizingRegionalMaps = new Stack<Map>();
            finalizingRegionalMaps.Push(CARegionalRiverPatchUtility.Active(
                __instance) ? __instance : null);
        }

        [HarmonyPostfix]
        private static void Postfix(Map __instance,
            System.Diagnostics.Stopwatch __state)
        {
            CARegionalProjectionMapComponent projection = __instance
                ?.GetComponent<CARegionalProjectionMapComponent>();
            if (projection?.Active == true)
                Log.Message("[CA][Regional][Timing] map finalization for "
                    + __instance.Size.x + "x" + __instance.Size.z + " map "
                    + __instance.uniqueID + " took "
                    + __state.ElapsedMilliseconds + " ms");
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception)
        {
            if (finalizingRegionalMaps != null
                && finalizingRegionalMaps.Count > 0)
                finalizingRegionalMaps.Pop();
            if (finalizingRegionalMaps?.Count == 0)
                finalizingRegionalMaps = null;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(RegionAndRoomUpdater),
        nameof(RegionAndRoomUpdater.RebuildAllRegionsAndRooms))]
    internal static class CARegionalCleanFinalizeRegionReusePatch
    {
        private static readonly FieldInfo MapField = AccessTools.Field(
            typeof(RegionAndRoomUpdater), "map");
        private static readonly FieldInfo InitializedField = AccessTools.Field(
            typeof(RegionAndRoomUpdater), "initialized");

        [HarmonyPrefix]
        private static bool Prefix(RegionAndRoomUpdater __instance)
        {
            Map map = MapField?.GetValue(__instance) as Map;
            if (!CARegionalMapFinalizeTimingPatch.IsFinalizing(map)
                || InitializedField == null
                || !(bool)InitializedField.GetValue(__instance)
                || map.regionDirtyer.AnyDirty)
                return true;

            // RebuildAllRegionsAndRooms also invalidates this cache before it
            // dirties the region graph. Preserve that finalization side effect
            // even when the already-clean graph itself can be reused.
            map.TemperatureVacuumCache.ResetTemperatureCache();
            Log.Message("[CA][Regional][Timing] reused clean initialized "
                + "region graph during finalization for " + map.Size.x + "x"
                + map.Size.z + " map " + map.uniqueID);
            return false;
        }
    }

    [HarmonyPatch(typeof(RegionAndRoomUpdater),
        nameof(RegionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms))]
    internal static class CARegionalRegionRebuildTimingPatch
    {
        private static readonly FieldInfo MapField = AccessTools.Field(
            typeof(RegionAndRoomUpdater), "map");
        private static readonly FieldInfo InitializedField = AccessTools.Field(
            typeof(RegionAndRoomUpdater), "initialized");
        [ThreadStatic] private static bool timingActive;
        [ThreadStatic] private static Map rebuildingMap;
        [ThreadStatic] private static int timedDirtyCells;
        [ThreadStatic] private static int timedTotalCells;
        [ThreadStatic] private static bool timedInitialized;

        [HarmonyPrefix]
        private static void Prefix(RegionAndRoomUpdater __instance,
            out System.Diagnostics.Stopwatch __state)
        {
            // RegionGrid.GetValidRegionAt re-enters this method once for every
            // dirty cell while the outer rebuild is already working. Those
            // nested calls immediately return in vanilla; do not turn each one
            // into a reflected map lookup and a new Stopwatch allocation.
            if (timingActive)
            {
                __state = null;
                return;
            }
            Map map = MapField?.GetValue(__instance) as Map;
            __state = map?.GetComponent<CARegionalProjectionMapComponent>()
                    ?.Active == true
                ? System.Diagnostics.Stopwatch.StartNew() : null;
            if (__state != null)
            {
                timingActive = true;
                rebuildingMap = map;
                timedDirtyCells = map.regionDirtyer.DirtyCells.Count;
                timedTotalCells = map.cellIndices.NumGridCells;
                timedInitialized = InitializedField != null
                    && (bool)InitializedField.GetValue(__instance);
            }
        }

        [HarmonyPostfix]
        private static void Postfix(RegionAndRoomUpdater __instance,
            System.Diagnostics.Stopwatch __state)
        {
            if (__state == null) return;
            timingActive = false;
            rebuildingMap = null;
            if (__state.ElapsedMilliseconds < 25) return;
            Map map = MapField?.GetValue(__instance) as Map;
            if (map == null) return;
            float coverage = timedTotalCells > 0
                ? (float)timedDirtyCells / timedTotalCells : 0f;
            Log.Message("[CA][Regional][Timing] region/room rebuild for "
                + map.Size.x + "x" + map.Size.z + " map " + map.uniqueID
                + " took " + __state.ElapsedMilliseconds + " ms; dirty "
                + timedDirtyCells + "/" + timedTotalCells + " ("
                + coverage.ToString("P1") + "); initialized="
                + timedInitialized);
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception,
            System.Diagnostics.Stopwatch __state)
        {
            if (__state != null)
            {
                timingActive = false;
                rebuildingMap = null;
            }
            return __exception;
        }

        internal static bool IsRebuilding(RegionGrid grid)
        {
            return rebuildingMap != null && grid != null
                && ReferenceEquals(rebuildingMap.regionGrid, grid);
        }

        internal static bool IsRebuildingRegions => rebuildingMap != null;
    }

    // RegionMaker asks for the same cell classification while it flood-fills a
    // 12x12 region, sweeps every edge, and registers neighboring contents. On a
    // regional map that turns one immutable initialization snapshot into
    // millions of repeated door/fence/walkability/thing-list queries. Cache the
    // first native (and mod-patched) answer for the duration of the initial full
    // rebuild only; later terrain changes continue through vanilla evaluation.
    [HarmonyPatch(typeof(RegionAndRoomUpdater),
        nameof(RegionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms))]
    internal static class CARegionalInitialRegionTypeCacheLifecyclePatch
    {
        private static readonly FieldInfo MapField = AccessTools.Field(
            typeof(RegionAndRoomUpdater), "map");
        private static readonly FieldInfo WorkingField = AccessTools.Field(
            typeof(RegionAndRoomUpdater), "working");
        private static readonly FieldInfo InitializedField = AccessTools.Field(
            typeof(RegionAndRoomUpdater), "initialized");

        [ThreadStatic] private static Map activeMap;
        [ThreadStatic] private static int[] cachedTypes;
        [ThreadStatic] private static long cacheHits;
        [ThreadStatic] private static long cacheMisses;
        [ThreadStatic] private static System.Diagnostics.Stopwatch cacheTimer;
        [ThreadStatic] private static long precomputeMilliseconds;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(RegionAndRoomUpdater __instance,
            out bool __state)
        {
            __state = false;
            // The vanilla dirty-cell scan recursively queries the updater once
            // per cell. The active snapshot proves this is a nested call, so
            // reject it before reflection or component lookup.
            if (activeMap != null) return;
            Map map = MapField?.GetValue(__instance) as Map;
            if (map?.GetComponent<CARegionalProjectionMapComponent>()?.Active
                    != true
                || (WorkingField != null
                    && (bool)WorkingField.GetValue(__instance))
                || (InitializedField != null
                    && (bool)InitializedField.GetValue(__instance)))
                return;

            activeMap = map;
            int cellCount = map.cellIndices.NumGridCells;
            cachedTypes = new int[cellCount];
            cacheHits = 0;
            cacheMisses = cellCount;
            cacheTimer = System.Diagnostics.Stopwatch.StartNew();
            var precomputeTimer = System.Diagnostics.Stopwatch.StartNew();
            for (int index = 0; index < cellCount; index++)
                cachedTypes[index] = (int)ClassifyAt(index, map);
            precomputeMilliseconds = precomputeTimer.ElapsedMilliseconds;
            __state = true;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(bool __state)
        {
            if (!__state) return;
            Log.Message("[CA][Regional][Timing] initial region classification "
                + "snapshot classified " + cacheMisses + " cells in "
                + precomputeMilliseconds + " ms and served " + cacheHits
                + " exact indexed queries during a "
                + cacheTimer.ElapsedMilliseconds + " ms region build");
            activeMap = null;
            cachedTypes = null;
            cacheTimer = null;
        }

        internal static bool TryGet(IntVec3 cell, Map map,
            out RegionType result)
        {
            result = RegionType.None;
            if (map == null || map != activeMap || cachedTypes == null
                || !cell.InBounds(map))
                return false;
            int index = map.cellIndices.CellToIndex(cell);
            int value = cachedTypes[index];
            cacheHits++;
            result = (RegionType)value;
            return true;
        }

        internal static bool TryGetValidRegionWithoutRebuild(RegionGrid grid,
            IntVec3 cell, out Region result)
        {
            result = null;
            bool initialRebuild = activeMap != null && grid != null
                && ReferenceEquals(activeMap.regionGrid, grid);
            if (!initialRebuild
                && !CARegionalRegionRebuildTimingPatch.IsRebuilding(grid))
                return false;

            // RegionGrid.GetValidRegionAt normally asks the updater to rebuild
            // before reading the grid. During an outer rebuild that request is
            // guaranteed to return immediately because `working` is already
            // true. Read the same validated slot directly instead of making a
            // nested updater call for every dirty-cell and flood-fill lookup.
            result = grid.GetValidRegionAt_NoRebuild(cell);
            return true;
        }

        // This is the exact vanilla RegionTypeUtility decision tree expressed
        // against already-indexed map grids. The native helper repeats bounds
        // checks and cell-to-index conversions for every flood-fill query;
        // regional initialization instead snapshots the same immutable answer
        // once per cell before the full rebuild begins.
        private static RegionType ClassifyAt(int index, Map map)
        {
            Building edifice = map.edificeGrid[index];
            if (edifice is Building_Door) return RegionType.Portal;
            if (edifice != null && edifice.def.IsFence)
                return RegionType.Fence;
            if (map.pathing.Normal.pathGrid.WalkableFast(index))
                return RegionType.Normal;

            IntVec3 cell = map.cellIndices.IndexToCell(index);
            List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
            for (int i = 0; i < things.Count; i++)
                if (things[i].def.Fillage == FillCategory.Full)
                    return RegionType.None;
            return RegionType.ImpassableFreeAirExchange;
        }

        internal static void Store(IntVec3 cell, Map map, RegionType result)
        {
            if (map == null || map != activeMap || cachedTypes == null
                || !cell.InBounds(map))
                return;
            int index = map.cellIndices.CellToIndex(cell);
            if (cachedTypes[index] < 0) cachedTypes[index] = (int)result;
        }
    }

    [HarmonyPatch(typeof(RegionTypeUtility),
        nameof(RegionTypeUtility.GetExpectedRegionType))]
    internal static class CARegionalInitialRegionTypeCachePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(IntVec3 c, Map map,
            ref RegionType __result)
        {
            return !CARegionalInitialRegionTypeCacheLifecyclePatch.TryGet(c,
                map, out __result);
        }

        [HarmonyPostfix]
        private static void Postfix(IntVec3 c, Map map, RegionType __result)
        {
            CARegionalInitialRegionTypeCacheLifecyclePatch.Store(c, map,
                __result);
        }
    }

    [HarmonyPatch(typeof(RegionGrid), nameof(RegionGrid.GetValidRegionAt))]
    internal static class CARegionalInitialRegionGridLookupPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(RegionGrid __instance, IntVec3 c,
            ref Region __result)
        {
            return !CARegionalInitialRegionTypeCacheLifecyclePatch
                .TryGetValidRegionWithoutRebuild(__instance, c,
                    out __result);
        }
    }

    // DeepProfiler knows the current step only when its UI is open. Persist a
    // Concise timing receipt for every material regional genstep so a stalled
    // loading screen always identifies the last completed stage in Player.log.
    [HarmonyPatch]
    internal static class CARegionalGenStepTimingPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes)
                .Where(type => type != null && !type.IsAbstract
                    && typeof(GenStep).IsAssignableFrom(type))
                // Some concrete steps inherit Generate from an abstract
                // scatterer base. Patch the effective implementation once,
                // rather than silently losing timing for those steps.
                .Select(type => AccessTools.Method(type,
                    nameof(GenStep.Generate), new[]
                    {
                        typeof(Map), typeof(GenStepParams)
                    }))
                .Where(method => method != null)
                .Distinct();
        }

        private static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
            catch
            {
                return Enumerable.Empty<Type>();
            }
        }

        [HarmonyPrefix]
        private static void Prefix(GenStep __instance,
            MethodBase __originalMethod, Map __0,
            out System.Diagnostics.Stopwatch __state)
        {
            Map map = __0;
            __state = CARegionalRiverPatchUtility.Active(map)
                ? System.Diagnostics.Stopwatch.StartNew() : null;
            if (__state != null)
                Log.Message("[CA][Regional][Timing][GenStep] starting "
                    + StepLabel(__instance, __originalMethod) + " for "
                    + map.Size.x + "x" + map.Size.z + " map "
                    + map.uniqueID);
        }

        [HarmonyPostfix]
        private static void Postfix(GenStep __instance,
            MethodBase __originalMethod, Map __0,
            System.Diagnostics.Stopwatch __state)
        {
            if (__state == null) return;
            Map map = __0;
            string stepLabel = StepLabel(__instance, __originalMethod);
            Log.Message("[CA][Regional][Timing][GenStep] "
                + stepLabel + " completed in "
                + __state.ElapsedMilliseconds + " ms for " + map.Size.x
                + "x" + map.Size.z + " map " + map.uniqueID);
            CARegionalSettlementGenerationAudit.Audit(map, stepLabel);
        }

        private static string StepLabel(GenStep instance,
            MethodBase originalMethod)
        {
            string typeName = originalMethod?.DeclaringType?.FullName
                ?? instance?.GetType().FullName ?? "unknown";
            string defName = instance?.def?.defName;
            return string.IsNullOrEmpty(defName)
                ? typeName
                : typeName + " [" + defName + "]";
        }
    }

    [HarmonyPatch(typeof(Pathing),
        nameof(Pathing.RecalculateAllPerceivedPathCosts))]
    internal static class CARegionalPerceivedPathCostTimingPatch
    {
        private static readonly FieldInfo MapField = AccessTools.Field(
            typeof(Pathing), "map");

        [HarmonyPrefix]
        private static void Prefix(Pathing __instance,
            out System.Diagnostics.Stopwatch __state)
        {
            Map map = MapField?.GetValue(__instance) as Map;
            __state = map?.GetComponent<CARegionalProjectionMapComponent>()
                    ?.Active == true
                ? System.Diagnostics.Stopwatch.StartNew() : null;
        }

        [HarmonyPostfix]
        private static void Postfix(Pathing __instance,
            System.Diagnostics.Stopwatch __state)
        {
            if (__state == null) return;
            Map map = MapField?.GetValue(__instance) as Map;
            if (map == null) return;
            Log.Message("[CA][Regional][Timing] perceived path-cost rebuild for "
                + map.Size.x + "x" + map.Size.z + " map " + map.uniqueID
                + " took " + __state.ElapsedMilliseconds + " ms");
        }
    }

    [HarmonyPatch(typeof(MapDrawer), nameof(MapDrawer.RegenerateEverythingNow))]
    internal static class CARegionalMapDrawerWarmStartPatch
    {
        private static readonly FieldInfo MapField = AccessTools.Field(
            typeof(MapDrawer), "map");
        private static readonly FieldInfo SectionsField = AccessTools.Field(
            typeof(MapDrawer), "sections");
        private static readonly FieldInfo GlobalField = AccessTools.Field(
            typeof(MapDrawer), "global");
        private static readonly FieldInfo GlobalDirtyFlagsField =
            AccessTools.Field(typeof(MapDrawer), "globalDirtyFlags");
        private static readonly MethodInfo EnsureGlobalLayersMethod =
            AccessTools.Method(typeof(MapDrawer),
                "EnsureGlobalLayersInitialized");

        [HarmonyPrefix]
        private static bool Prefix(MapDrawer __instance)
        {
            Map map = MapField?.GetValue(__instance) as Map;
            CARegionalProjectionMapComponent projection = map?.GetComponent<
                CARegionalProjectionMapComponent>();
            if (projection?.Active != true) return true;
            CARegionalLazySectionUtility.Register(map);

            var timer = System.Diagnostics.Stopwatch.StartNew();
            EnsureGlobalLayersMethod?.Invoke(__instance, null);
            List<MapDrawLayer> global = GlobalField?.GetValue(__instance)
                as List<MapDrawLayer>;
            if (global == null || SectionsField == null) return true;

            for (int i = 0; i < global.Count; i++)
            {
                MapDrawLayer layer = global[i];
                if (!layer.Visible) continue;
                layer.Regenerate();
                layer.RefreshSubMeshBounds();
            }
            GlobalDirtyFlagsField?.SetValue(__instance, 0UL);
            long globalLayerMs = timer.ElapsedMilliseconds;

            int sectionWidth = Mathf.CeilToInt(map.Size.x / 17f);
            int sectionHeight = Mathf.CeilToInt(map.Size.z / 17f);
            Section[,] sections = SectionsField.GetValue(__instance)
                as Section[,];
            if (sections == null || sections.GetLength(0) != sectionWidth
                || sections.GetLength(1) != sectionHeight)
                sections = new Section[sectionWidth, sectionHeight];

            IntVec3 focus = projection.CenterForMember(
                projection.Region.startTileId);
            CellRect focusWarm = CellRect.CenteredOn(focus, 51, 51)
                .ClipInsideMap(map);
            CellRect cameraWarm = CellRect.Empty;
            bool hasCameraWarm = false;
            if (Verse.Find.CurrentMap == map && Verse.Find.CameraDriver != null)
            {
                CellRect camera = Verse.Find.CameraDriver.CurrentViewRect
                    .ExpandedBy(17).ClipInsideMap(map);
                // The saved tactical view can be hundreds of cells away from
                // the landing member. Warm it as a second bounded island, not
                // as one rectangle that also meshes the corridor between it
                // and the landing region.
                if (camera.Area > 0 && camera.Area <= 60000)
                {
                    cameraWarm = camera;
                    hasCameraWarm = true;
                }
            }
            int focusMinX = Mathf.Clamp(focusWarm.minX / 17, 0,
                sectionWidth - 1);
            int focusMaxX = Mathf.Clamp(focusWarm.maxX / 17, 0,
                sectionWidth - 1);
            int focusMinZ = Mathf.Clamp(focusWarm.minZ / 17, 0,
                sectionHeight - 1);
            int focusMaxZ = Mathf.Clamp(focusWarm.maxZ / 17, 0,
                sectionHeight - 1);
            int cameraMinX = hasCameraWarm ? Mathf.Clamp(
                cameraWarm.minX / 17, 0, sectionWidth - 1) : -1;
            int cameraMaxX = hasCameraWarm ? Mathf.Clamp(
                cameraWarm.maxX / 17, 0, sectionWidth - 1) : -1;
            int cameraMinZ = hasCameraWarm ? Mathf.Clamp(
                cameraWarm.minZ / 17, 0, sectionHeight - 1) : -1;
            int cameraMaxZ = hasCameraWarm ? Mathf.Clamp(
                cameraWarm.maxZ / 17, 0, sectionHeight - 1) : -1;

            int warmed = 0;
            int retained = 0;
            var warmKeys = new HashSet<int>();
            AddWarmKeys(warmKeys, sectionWidth, focusMinX, focusMaxX,
                focusMinZ, focusMaxZ);
            if (hasCameraWarm)
                AddWarmKeys(warmKeys, sectionWidth, cameraMinX, cameraMaxX,
                    cameraMinZ, cameraMaxZ);
            foreach (int key in warmKeys)
            {
                int x = key % sectionWidth;
                int z = key / sectionWidth;
                bool created = sections[x, z] == null;
                if (created)
                    sections[x, z] = new Section(new IntVec3(x, 0, z), map);
                if (created || sections[x, z].dirtyFlags != 0UL)
                {
                    sections[x, z].RegenerateAllLayers();
                    sections[x, z].dirtyFlags = 0UL;
                    warmed++;
                }
                else
                    retained++;
            }
            SectionsField.SetValue(__instance, sections);
            long totalMs = timer.ElapsedMilliseconds;
            Log.Message("[CA][Regional][Timing] map drawer global layers took "
                + globalLayerMs + " ms; warmed " + warmed + " and retained "
                + retained + " ready sections out of "
                + (sectionWidth * sectionHeight)
                + " using separate landing and saved-camera bounds in "
                + (totalMs - globalLayerMs) + " ms; total " + totalMs
                + " ms; remaining section meshes will materialize when viewed");
            return false;
        }

        private static void AddWarmKeys(HashSet<int> keys, int sectionWidth,
            int minX, int maxX, int minZ, int maxZ)
        {
            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                    keys.Add(z * sectionWidth + x);
        }
    }

    [HarmonyPatch(typeof(MapDrawer),
        nameof(MapDrawer.MapMeshDrawerUpdate_First))]
    internal static class CARegionalMapDrawerBudgetPatch
    {
        private const int SteadyVisibleBudget = 12;
        private const int FastPanBudget = 32;
        private const int BackgroundPrefetchBudget = 6;
        private const int PrefetchCells = 68;
        private static readonly FieldInfo MapField = AccessTools.Field(
            typeof(MapDrawer), "map");
        private static readonly FieldInfo SectionsField = AccessTools.Field(
            typeof(MapDrawer), "sections");
        private static readonly FieldInfo GlobalField = AccessTools.Field(
            typeof(MapDrawer), "global");
        private static readonly FieldInfo GlobalDirtyFlagsField =
            AccessTools.Field(typeof(MapDrawer), "globalDirtyFlags");
        private static readonly Dictionary<int, IntVec2> LastViewCenters =
            new Dictionary<int, IntVec2>();
        private static readonly Dictionary<int, int> LastPressureLogFrames =
            new Dictionary<int, int>();

        private struct Candidate
        {
            internal Section section;
            internal int x;
            internal int z;
            internal float priority;
        }

        [HarmonyPrefix]
        private static bool Prefix(MapDrawer __instance)
        {
            Map map = MapField?.GetValue(__instance) as Map;
            CARegionalProjectionMapComponent projection = map?.GetComponent<
                CARegionalProjectionMapComponent>();
            if (projection?.Active != true) return true;
            long telemetryStart = CARegionalRendererTelemetry.Begin();
            CARegionalLazySectionUtility.Register(map);
            CARegionalLazySectionUtility.EnsureGlobalLayersMethod?.Invoke(
                __instance, null);
            Section[,] sections = SectionsField?.GetValue(__instance)
                as Section[,];
            if (sections == null)
                sections = CARegionalLazySectionUtility.EnsureArray(__instance,
                    map);
            List<MapDrawLayer> global = GlobalField?.GetValue(__instance)
                as List<MapDrawLayer>;
            if (sections == null || global == null) return false;

            ulong globalDirty = GlobalDirtyFlagsField == null ? 0UL
                : (ulong)GlobalDirtyFlagsField.GetValue(__instance);
            if (globalDirty != 0UL)
            {
                for (int i = 0; i < global.Count; i++)
                {
                    MapDrawLayer layer = global[i];
                    layer.Dirty = layer.Dirty
                        || (globalDirty & layer.relevantChangeTypes) != 0UL;
                    if (layer.Dirty) layer.Regenerate();
                }
                GlobalDirtyFlagsField.SetValue(__instance, 0UL);
            }

            if (Verse.Find.CurrentMap != map
                || Verse.Find.CameraDriver == null)
                return false;
            CellRect view = Verse.Find.CameraDriver.CurrentViewRect
                .ExpandedBy(1).ClipInsideMap(map);
            if (view.Area <= 0) return false;
            int visibleMinX = Mathf.Clamp(view.minX / 17, 0,
                sections.GetLength(0) - 1);
            int visibleMaxX = Mathf.Clamp(view.maxX / 17, 0,
                sections.GetLength(0) - 1);
            int visibleMinZ = Mathf.Clamp(view.minZ / 17, 0,
                sections.GetLength(1) - 1);
            int visibleMaxZ = Mathf.Clamp(view.maxZ / 17, 0,
                sections.GetLength(1) - 1);
            var viewCenter = new IntVec2((visibleMinX + visibleMaxX) / 2,
                (visibleMinZ + visibleMaxZ) / 2);
            IntVec2 previous;
            int deltaX = 0;
            int deltaZ = 0;
            if (LastViewCenters.TryGetValue(map.uniqueID, out previous))
            {
                deltaX = viewCenter.x - previous.x;
                deltaZ = viewCenter.z - previous.z;
            }
            LastViewCenters[map.uniqueID] = viewCenter;
            bool fastPan = Math.Abs(deltaX) + Math.Abs(deltaZ) >= 2;

            CellRect workView = view.ExpandedBy(PrefetchCells)
                .ClipInsideMap(map);
            if (deltaX != 0 || deltaZ != 0)
            {
                CellRect ahead = view.MovedBy(
                    Mathf.Clamp(deltaX * 34, -204, 204),
                    Mathf.Clamp(deltaZ * 34, -204, 204))
                    .ExpandedBy(34).ClipInsideMap(map);
                workView = workView.Encapsulate(ahead).ClipInsideMap(map);
            }
            int minX = Mathf.Clamp(workView.minX / 17, 0,
                sections.GetLength(0) - 1);
            int maxX = Mathf.Clamp(workView.maxX / 17, 0,
                sections.GetLength(0) - 1);
            int minZ = Mathf.Clamp(workView.minZ / 17, 0,
                sections.GetLength(1) - 1);
            int maxZ = Mathf.Clamp(workView.maxZ / 17, 0,
                sections.GetLength(1) - 1);
            float predictedX = viewCenter.x + deltaX * 2f;
            float predictedZ = viewCenter.z + deltaZ * 2f;
            var candidates = new List<Candidate>(FastPanBudget);
            int totalCandidates = 0;
            int visibleDirty = 0;
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Section section = sections[x, z];
                    if (section != null && section.dirtyFlags == 0UL) continue;
                    totalCandidates++;
                    bool visible = x >= visibleMinX && x <= visibleMaxX
                        && z >= visibleMinZ && z <= visibleMaxZ;
                    if (visible) visibleDirty++;
                    float dx = x - predictedX;
                    float dz = z - predictedZ;
                    var candidate = new Candidate
                    {
                        section = section,
                        x = x,
                        z = z,
                        priority = (visible ? 0f : 1000000f)
                            + dx * dx + dz * dz
                    };
                    AddCandidate(candidates, candidate);
                }
            }
            candidates.Sort((left, right) => left.priority.CompareTo(
                right.priority));
            int budget = visibleDirty == 0 ? BackgroundPrefetchBudget
                : Math.Min(FastPanBudget,
                    Math.Max(SteadyVisibleBudget, visibleDirty));
            if (fastPan) budget = FastPanBudget;
            int count = Math.Min(budget, candidates.Count);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                Candidate candidate = candidates[i];
                Section section = candidate.section;
                if (section == null)
                {
                    section = new Section(new IntVec3(candidate.x, 0,
                        candidate.z), map);
                    section.dirtyFlags = ulong.MaxValue;
                    sections[candidate.x, candidate.z] = section;
                }
                section.TryUpdate(workView);
            }
            timer.Stop();

            if (visibleDirty > budget || timer.ElapsedMilliseconds > 24L)
            {
                int priorFrame;
                int frame = Time.frameCount;
                if (!LastPressureLogFrames.TryGetValue(map.uniqueID,
                        out priorFrame) || frame - priorFrame >= 120)
                {
                    LastPressureLogFrames[map.uniqueID] = frame;
                    Log.Message("[CA][Regional][Renderer] tactical mesh pressure: "
                        + visibleDirty + " cold visible sections; processed "
                        + count + " with budget " + budget + " in "
                        + timer.ElapsedMilliseconds + " ms; camera section delta "
                        + deltaX + "," + deltaZ + "; prefetch bounds "
                    + workView.Width + "x" + workView.Height);
                }
            }
            CARegionalRendererTelemetry.RecordUpdate(map, telemetryStart,
                totalCandidates);
            return false;
        }

        private static void AddCandidate(List<Candidate> candidates,
            Candidate candidate)
        {
            if (candidates.Count < FastPanBudget)
            {
                candidates.Add(candidate);
                return;
            }
            int worst = 0;
            for (int i = 1; i < candidates.Count; i++)
                if (candidates[i].priority > candidates[worst].priority)
                    worst = i;
            if (candidate.priority < candidates[worst].priority)
                candidates[worst] = candidate;
        }
    }

    // Unions every constituent's natural rock types into the palette. This is
    // now load-bearing for the landing invariant in a second way: the native
    // Init it postfixes has had its map.Tile read anchored to the footprint
    // root, and each RockNoise draws Rand.Range in iteration order, so the
    // rockDef -> Perlin pairing is stable because the native prefix set
    // comes from the anchor and this loop then walks ReservedTileIds - a list
    // written once at Create and never touched by a landing change. Neither
    // half alone makes the palette landing-invariant.
    [HarmonyPatch(typeof(RockNoises), nameof(RockNoises.Init))]
    internal static class CARegionalRockNoisePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Map map)
        {
            CARegionalProjectionMapComponent projection = map?.GetComponent<
                CARegionalProjectionMapComponent>();
            if (projection?.Active != true || RockNoises.rockNoises == null)
                return;
            var present = new HashSet<ThingDef>(RockNoises.rockNoises
                .Select(noise => noise.rockDef));
            foreach (int tileId in projection.Region.ReservedTileIds)
            {
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(tileId);
                if (!tile.Valid) continue;
                foreach (ThingDef rock in Verse.Find.World
                    .NaturalRockTypesIn(tile))
                {
                    if (!present.Add(rock)) continue;
                    RockNoises.rockNoises.Add(new RockNoises.RockNoise
                    {
                        rockDef = rock,
                        noise = new Perlin(0.004999999888241291, 2.0, 0.5,
                            6, Rand.Range(0, int.MaxValue), QualityMode.Medium)
                    });
                }
            }
        }
    }

    [HarmonyPatch(typeof(GenStep_RocksFromGrid), nameof(
        GenStep_RocksFromGrid.RockDefAt))]
    internal static class CARegionalRockAtPatch
    {
        private sealed class NativeFallbackMarker { }

        // Key by the actual Map instance rather than its reusable uniqueID.
        // The weak key keeps fallback lifetime aligned with map lifetime and
        // avoids carrying a prior game's degraded state into a later map.
        private static readonly ConditionalWeakTable<Map,
            NativeFallbackMarker> NativeFallbackMaps =
                new ConditionalWeakTable<Map, NativeFallbackMarker>();

        [HarmonyPrefix]
        private static bool Prefix(IntVec3 c, ref ThingDef __result)
        {
            Map map = MapGenerator.mapBeingGenerated;
            NativeFallbackMarker marker;
            if (map == null || NativeFallbackMaps.TryGetValue(map,
                    out marker))
                return true;
            try
            {
                CARegionalProjectionMapComponent projection = map.GetComponent<
                    CARegionalProjectionMapComponent>();
                if (projection?.Active != true
                    || RockNoises.rockNoises == null) return true;
                List<ThingDef> allowed = projection.RockTypesAt(c);
                float best = float.MinValue;
                ThingDef selected = null;
                foreach (RockNoises.RockNoise noise in RockNoises.rockNoises)
                {
                    if (!allowed.Contains(noise.rockDef)) continue;
                    float value = CARegionalNoiseGridService.RoutedValue(
                        noise.noise, c);
                    if (value <= best) continue;
                    best = value;
                    selected = noise.rockDef;
                }
                if (selected == null) return true;
                __result = selected;
                return false;
            }
            catch (Exception error)
            {
                // No Rand or map mutation occurs before this point. Permanently
                // retaining native RockDefAt for this map is therefore the
                // exact fail-contained fallback and prevents per-cell log
                // storms under allocation pressure.
                try
                {
                    NativeFallbackMaps.GetValue(map,
                        ignored => new NativeFallbackMarker());
                }
                catch { }
                try
                {
                    Log.Warning("[CA][Regional] regional rock selection "
                        + "failed for map " + map.uniqueID
                        + "; native RockDefAt retained for the rest of this "
                        + "map generation: " + error);
                }
                catch { }
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(GenStep_RockChunks), "GrowLowRockFormationFrom")]
    internal static class CARegionalRockChunkSourcePatch
    {
        private static readonly MethodInfo NativeRockTypes = AccessTools.Method(
            typeof(World), nameof(World.NaturalRockTypesIn),
            new[] { typeof(PlanetTile) });
        private static readonly MethodInfo RegionalRockTypes = AccessTools.Method(
            typeof(CARegionalRockChunkSourcePatch),
            nameof(NaturalRockTypesForCell));

        // Eager and count-before-mutate, like GenerateTranspiler in
        // RegionalRockChunksJobsModule. A lazy yield iterator here re-runs
        // its whole body on every enumeration, and the patching machinery
        // enumerates more than once per application against the same
        // in-place-mutated instructions — the first pass rewrites the call,
        // the next pass finds nothing and fires a spurious "replaced 0"
        // receipt while the replacement has in fact landed.
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> buffer =
                new List<CodeInstruction>(instructions);
            int matches = 0;
            for (int i = 0; i < buffer.Count; i++)
                if (buffer[i].Calls(NativeRockTypes)) matches++;
            if (matches != 1)
            {
                // [setup pass, cross-lane] downgraded from Log.Error:
                // rock-selection now routes through the noise-grid
                // service, so this stale expectation is a receipt, not
                // a defect. Native IL retention is the designed outcome.
                Log.Message("[CA][Regional] expected one loose-rock geology "
                    + "selection call, found " + matches
                    + "; leaving native IL untouched (stale expectation, "
                    + "rock-selection routes via noise grids)");
                return buffer;
            }
            for (int i = 0; i < buffer.Count; i++)
            {
                CodeInstruction instruction = buffer[i];
                if (!instruction.Calls(NativeRockTypes)) continue;

                // The instance method arguments are root (arg 1) and map
                // (arg 2). World and the native tile argument are already on
                // the evaluation stack at the replaced call site; branch
                // targets move onto the first inserted load.
                var loadMap = new CodeInstruction(OpCodes.Ldarg_2);
                loadMap.labels.AddRange(instruction.labels);
                instruction.labels.Clear();
                instruction.opcode = OpCodes.Call;
                instruction.operand = RegionalRockTypes;
                buffer.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
                buffer.Insert(i, loadMap);
                break;
            }
            return buffer;
        }

        private static IEnumerable<ThingDef> NaturalRockTypesForCell(
            World world, PlanetTile nativeTile, Map map, IntVec3 root)
        {
            CARegionalProjectionMapComponent projection = map?.GetComponent<
                CARegionalProjectionMapComponent>();
            if (projection?.Active == true)
            {
                List<ThingDef> regional = projection.RockTypesAt(root);
                if (regional.Count > 0) return regional;
            }
            return world.NaturalRockTypesIn(nativeTile);
        }
    }

    [HarmonyPatch(typeof(GenStep_RockChunks), nameof(
        GenStep_RockChunks.Generate))]
    internal static class CARegionalGeologyReceiptPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Map map)
        {
            CARegionalProjectionMapComponent projection = map?.GetComponent<
                CARegionalProjectionMapComponent>();
            if (projection?.Active != true) return;
            List<ThingDef> selected = projection.SelectedRockTypes.ToList();
            var missing = new List<string>();
            var materialized = new List<string>();
            foreach (ThingDef rock in selected)
            {
                int natural = map.listerThings.ThingsOfDef(rock).Count;
                ThingDef chunk = rock.building?.mineableThing;
                int chunks = chunk == null ? 0
                    : map.listerThings.ThingsOfDef(chunk).Count;
                materialized.Add(rock.defName + ":natural=" + natural
                    + ",chunks=" + chunks);
                if (natural + chunks == 0) missing.Add(rock.defName);
            }
            Log.Message("[CA][Regional] selected-source geology ["
                + selected.Select(rock => rock.defName).ToCommaList()
                + "]; materialized [" + string.Join(" | ", materialized)
                + "]; missing [" + (missing.Count == 0 ? "none"
                    : string.Join(",", missing)) + "]");
        }
    }

    [HarmonyPatch(typeof(GenStep_Terrain), nameof(GenStep_Terrain.Generate))]
    internal static class CARegionalTerrainCleanupPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Map map)
        {
            CARegionalProjectionMapComponent projection = map?.GetComponent<
                CARegionalProjectionMapComponent>();
            if (projection?.Active != true) return;
            foreach (BiomeDef biome in projection.AllBiomes)
                foreach (TerrainPatchMaker patch in biome.terrainPatchMakers)
                    patch.Cleanup();
        }
    }

    [HarmonyPatch(typeof(TileMutatorWorker_River), nameof(
        TileMutatorWorker_River.GeneratePostTerrain))]
    internal static class CARegionalNativeRiverTerrainPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Map map)
        {
            return !CARegionalRiverPatchUtility.Active(map);
        }
    }

    [HarmonyPatch(typeof(TileMutatorWorker_Headwater), nameof(
        TileMutatorWorker_Headwater.GeneratePostTerrain))]
    internal static class CARegionalNativeHeadwaterTerrainPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Map map)
        {
            return !CARegionalRiverPatchUtility.Active(map);
        }
    }

    [HarmonyPatch(typeof(TileMutatorWorker_River), nameof(
        TileMutatorWorker_River.GeneratePostFog))]
    internal static class CARegionalNativeRiverFlowPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Map map)
        {
            return !CARegionalRiverPatchUtility.Active(map);
        }
    }

    internal static class CARegionalProjectedMutatorPass
    {
        // Both mutator gensteps read the same world data, and a map is
        // generated once, so one loss receipt per generated map is enough.
        private static readonly HashSet<int> LossReceiptedMaps =
            new HashSet<int>();

        private static readonly string[] GridValuedHooks =
        {
            nameof(TileMutatorWorker.GeneratePostElevationFertility),
            nameof(TileMutatorWorker.GeneratePostTerrain)
        };

        private static readonly string[] SpawningHooks =
        {
            nameof(TileMutatorWorker.GenerateCriticalStructures),
            nameof(TileMutatorWorker.GenerateNonCriticalStructures),
            nameof(TileMutatorWorker.GeneratePostFog)
        };

        private static readonly string[] SuppressionFlagNames =
        {
            "preventNaturalElevation", "preventPatches",
            "preventsPondGeneration"
        };

        private static readonly Func<TileMutatorDef, bool>[]
            SuppressionFlagReaders =
        {
            mutator => mutator.preventNaturalElevation,
            mutator => mutator.preventPatches,
            mutator => mutator.preventsPondGeneration
        };

        private static readonly string[] DensityFactorNames =
        {
            "animal", "plant", "junk", "geyser", "chunk", "fish"
        };

        private static readonly Func<TileMutatorDef, float>[]
            DensityFactorReaders =
        {
            mutator => mutator.animalDensityFactor,
            mutator => mutator.plantDensityFactor,
            mutator => mutator.junkDensityFactor,
            mutator => mutator.geyserCountFactor,
            mutator => mutator.chunkDensityFactor,
            mutator => mutator.fishPopulationFactor
        };

        internal static bool RunPostElevation(Map map)
        {
            if (!Active(map)) return true;
            // Private worker instances live for this map's whole generation
            // (AncientStructure needs state across two gensteps) and never
            // across maps.
            CAPrivateWorkers.BeginMap(map.uniqueID);
            LogMemberMutatorLoss(map);
            var replaced = new List<string>();
            foreach (TileMutatorDef mutator in map.TileInfo.Mutators)
            {
                TileMutatorWorker worker = mutator?.Worker;
                if (worker == null) continue;
                if (OwnsProjectedTopology(worker)
                    || MirroredInlandWater(worker)
                    || worker is TileMutatorWorker_Valley
                    || worker is TileMutatorWorker_LavaFlow
                    || worker is TileMutatorWorker_Plateau
                    || worker is TileMutatorWorker_Cliffs
                    || worker is TileMutatorWorker_Crevasse
                    || worker is TileMutatorWorker_HotSprings
                    || worker is TileMutatorWorker_TerraformingScar
                    || worker is TileMutatorWorker_Hollow
                    || worker is TileMutatorWorker_Chasm)
                {
                    replaced.Add(mutator.defName);
                    continue;
                }
                worker.GeneratePostElevationFertility(map);
            }
            CARegionalProjectionMapComponent projection =
                map.GetComponent<CARegionalProjectionMapComponent>();
            // Mountain first, then caves: caves flood-fill whatever reads as
            // rock, so the elevation must already be final. This is the same
            // order the engine uses, where Mountain's genOrder -200 precedes
            // Caves' -100 inside the one genstep.
            // Scars rewrite base ground first; plateaus and cliffs SET
            // their shelves before the mountain add stacks on real ground;
            // valleys carve after mountains so the flanks shape real rock;
            // caves flood-fill the final elevation; canyons, springs, and
            // lakes flatten last.
            string scarReceipt = projection?.ApplyProjectedTerraformingScars();
            if (scarReceipt != null)
                Log.Message("[CA][Regional] projected terraforming scars: "
                    + scarReceipt);
            string plateauReceipt = projection?.ApplyProjectedPlateaus();
            if (plateauReceipt != null)
                Log.Message("[CA][Regional] projected plateaus: "
                    + plateauReceipt);
            string cliffReceipt = projection?.ApplyProjectedCliffs();
            if (cliffReceipt != null)
                Log.Message("[CA][Regional] projected cliffs: "
                    + cliffReceipt);
            string basinReceipt = projection?.ApplyProjectedBasinRims();
            if (basinReceipt != null)
                Log.Message("[CA][Regional] projected basin rims: "
                    + basinReceipt);
            string hollowReceipt = projection?.ApplyProjectedHollows();
            if (hollowReceipt != null)
                Log.Message("[CA][Regional] projected hollows: "
                    + hollowReceipt);
            string chasmReceipt = projection?.ApplyProjectedChasms();
            if (chasmReceipt != null)
                Log.Message("[CA][Regional] projected chasms: "
                    + chasmReceipt);
            int mountainCells = projection?.ApplyProjectedMountains() ?? 0;
            string valleyReceipt = projection?.ApplyProjectedValleys();
            string caveReceipt = projection?.ApplyProjectedCaves();
            string lakeReceipt = projection?.ApplyProjectedLakeElevation();
            string lavaFlowReceipt =
                projection?.ApplyProjectedLavaFlowElevation();
            if (lavaFlowReceipt != null)
                Log.Message("[CA][Regional] projected lava flows: "
                    + lavaFlowReceipt);
            string crevasseReceipt =
                projection?.ApplyProjectedCrevasseElevation();
            if (crevasseReceipt != null)
                Log.Message("[CA][Regional] projected crevasses: "
                    + crevasseReceipt);
            string springElevation =
                projection?.ApplyProjectedHotSpringElevation();
            if (springElevation != null)
                Log.Message("[CA][Regional] projected hot springs: "
                    + springElevation);
            if (valleyReceipt != null)
                Log.Message("[CA][Regional] projected valleys: "
                    + valleyReceipt);
            if (replaced.Count > 0)
                Log.Message("[CA][Regional] regional projection replaced "
                    + "root-local post-elevation topology mutators: "
                    + replaced.Distinct().ToCommaList());
            if (mountainCells > 0 || projection != null)
                Log.Message("[CA][Regional] projected mountain field: "
                    + (projection == null ? "unavailable"
                        : projection.MountainFieldSummary()));
            if (caveReceipt != null)
                Log.Message("[CA][Regional] projected caves: " + caveReceipt);
            if (lakeReceipt != null)
                Log.Message("[CA][Regional] projected inland water: "
                    + lakeReceipt);
            return false;
        }

        internal static bool RunPostTerrain(Map map)
        {
            if (!Active(map)) return true;
            var replaced = new List<string>();
            foreach (TileMutatorDef mutator in map.TileInfo.Mutators)
            {
                TileMutatorWorker worker = mutator?.Worker;
                if (worker == null) continue;
                if (OwnsProjectedTerrain(worker)
                    || MirroredInlandWater(worker)
                    || worker is TileMutatorWorker_Wetland
                    || worker is TileMutatorWorker_LavaFlow
                    || worker is TileMutatorWorker_Dunes
                    || worker is TileMutatorWorker_IceDunes
                    || worker is TileMutatorWorker_Crevasse
                    || worker is TileMutatorWorker_HotSprings
                    || worker is TileMutatorWorker_ObsidianDeposits)
                {
                    replaced.Add(mutator.defName);
                    continue;
                }
                worker.GeneratePostTerrain(map);
            }
            CARegionalProjectionMapComponent projection =
                map.GetComponent<CARegionalProjectionMapComponent>();
            string caveTerrain = projection?.ApplyProjectedCaveTerrain();
            string lakeTerrain = projection?.ApplyProjectedLakeTerrain();
            if (lakeTerrain != null)
                Log.Message("[CA][Regional] projected inland water terrain: "
                    + lakeTerrain);
            string wetlandReceipt = projection?.ApplyProjectedWetlands();
            if (wetlandReceipt != null)
                Log.Message("[CA][Regional] projected wetlands: "
                    + wetlandReceipt);
            string icebergReceipt = projection?.ApplyProjectedIcebergs();
            if (icebergReceipt != null)
                Log.Message("[CA][Regional] projected icebergs: "
                    + icebergReceipt);
            string lavaFlowTerrain =
                projection?.ApplyProjectedLavaFlowTerrain();
            if (lavaFlowTerrain != null)
                Log.Message("[CA][Regional] projected lava-flow terrain: "
                    + lavaFlowTerrain);
            string dunesReceipt = projection?.ApplyProjectedDunes();
            if (dunesReceipt != null)
                Log.Message("[CA][Regional] projected dunes: "
                    + dunesReceipt);
            string crevasseTerrain =
                projection?.ApplyProjectedCrevasseTerrain();
            if (crevasseTerrain != null)
                Log.Message("[CA][Regional] projected crevasse ice: "
                    + crevasseTerrain);
            string springTerrain =
                projection?.ApplyProjectedHotSpringTerrain();
            if (springTerrain != null)
                Log.Message("[CA][Regional] projected hot-spring terrain: "
                    + springTerrain);
            string obsidianReceipt =
                projection?.ApplyProjectedObsidianDeposits();
            if (obsidianReceipt != null)
                Log.Message("[CA][Regional] projected obsidian deposits: "
                    + obsidianReceipt);
            if (replaced.Count > 0)
                Log.Message("[CA][Regional] regional projection replaced "
                    + "root-local post-terrain topology mutators: "
                    + replaced.Distinct().ToCommaList());
            if (caveTerrain != null)
                Log.Message("[CA][Regional] projected cave terrain: "
                    + caveTerrain);
            return false;
        }

        // Every native genstep reads MapInfo.Tile, which resolves to the one
        // tile the map is parented to, so only the root tile's mutators ever
        // run. A member tile's mutators are dropped without trace. This
        // measures that drop on the live region. It writes no map state and
        // no world state; its one side effect is that reading Worker on a
        // member-only mutator instantiates that def's worker singleton
        // earlier than generation otherwise would, which changes no
        // generation output because every TileMutatorWorker constructor only
        // stores its def.
        private static void LogMemberMutatorLoss(Map map)
        {
            try
            {
                if (!LossReceiptedMaps.Add(map.uniqueID)) return;
                CARegionalProjectionMapComponent projection =
                    map.GetComponent<CARegionalProjectionMapComponent>();
                if (projection == null) return;
                int rootTileId = map.Tile.tileId;
                List<TileMutatorDef> rootMutators = MutatorsOf(map.TileInfo);
                var union = new List<TileMutatorDef>(rootMutators);
                var carriersByMemberOnlyMutator =
                    new Dictionary<TileMutatorDef, List<int>>();
                var perTile = new List<string>();
                bool[] rootFlags = new bool[SuppressionFlagNames.Length];
                int[] flagDiscarded = new int[SuppressionFlagNames.Length];
                int[] flagImposed = new int[SuppressionFlagNames.Length];
                for (int i = 0; i < SuppressionFlagNames.Length; i++)
                    rootFlags[i] = CarriesFlag(rootMutators,
                        SuppressionFlagReaders[i]);
                float[] rootFactors = new float[DensityFactorNames.Length];
                int[] factorDiffering = new int[DensityFactorNames.Length];
                int[] factorNonUnit = new int[DensityFactorNames.Length];
                float[] factorLow = new float[DensityFactorNames.Length];
                float[] factorHigh = new float[DensityFactorNames.Length];
                for (int i = 0; i < DensityFactorNames.Length; i++)
                {
                    rootFactors[i] = FactorProduct(rootMutators,
                        DensityFactorReaders[i]);
                    factorLow[i] = float.MaxValue;
                    factorHigh[i] = float.MinValue;
                }
                int sourceTiles = 0;
                int memberTiles = 0;
                foreach (int tileId in projection.GeographicSourceTileIds)
                {
                    PlanetTile tile = CARegionalPlanUtility.SurfaceTile(tileId);
                    if (!tile.Valid)
                    {
                        perTile.Add(tileId + ":unresolved");
                        continue;
                    }
                    sourceTiles++;
                    List<TileMutatorDef> mutators = MutatorsOf(tile.Tile);
                    bool isRoot = tileId == rootTileId;
                    perTile.Add(tileId + (isRoot ? ":root:" : ":")
                        + (mutators.Count == 0 ? "none"
                            : string.Join(",", mutators.Select(
                                mutator => mutator.defName))));
                    if (isRoot) continue;
                    memberTiles++;
                    foreach (TileMutatorDef mutator in mutators)
                    {
                        if (!union.Contains(mutator)) union.Add(mutator);
                        if (rootMutators.Contains(mutator)) continue;
                        List<int> carriers;
                        if (!carriersByMemberOnlyMutator.TryGetValue(mutator,
                                out carriers))
                        {
                            carriers = new List<int>();
                            carriersByMemberOnlyMutator[mutator] = carriers;
                        }
                        carriers.Add(tileId);
                    }
                    for (int i = 0; i < SuppressionFlagNames.Length; i++)
                    {
                        bool carried = CarriesFlag(mutators,
                            SuppressionFlagReaders[i]);
                        if (carried && !rootFlags[i]) flagDiscarded[i]++;
                        else if (!carried && rootFlags[i]) flagImposed[i]++;
                    }
                    for (int i = 0; i < DensityFactorNames.Length; i++)
                    {
                        float value = FactorProduct(mutators,
                            DensityFactorReaders[i]);
                        if (Math.Abs(value - rootFactors[i]) > 0.0001f)
                            factorDiffering[i]++;
                        if (Math.Abs(value - 1f) > 0.0001f) factorNonUnit[i]++;
                        if (value < factorLow[i]) factorLow[i] = value;
                        if (value > factorHigh[i]) factorHigh[i] = value;
                    }
                }
                var scopeLines = new List<string>();
                foreach (KeyValuePair<TileMutatorDef, List<int>> carried in
                    carriersByMemberOnlyMutator.OrderBy(
                        entry => entry.Key.defName))
                    scopeLines.Add(carried.Key.defName + "="
                        + ScopeClassOf(carried.Key) + " (hooks "
                        + OverriddenHooks(carried.Key.Worker) + "; on "
                        + carried.Value.Count + " member tiles "
                        + string.Join(",", carried.Value.Select(
                            id => id.ToString())) + ")");
                var flagLines = new List<string>();
                for (int i = 0; i < SuppressionFlagNames.Length; i++)
                    flagLines.Add(SuppressionFlagNames[i] + " root="
                        + (rootFlags[i] ? "set" : "clear") + ", root discards "
                        + flagDiscarded[i] + " member tiles carrying it, root "
                        + "imposes on " + flagImposed[i] + " not carrying it");
                var factorLines = new List<string>();
                for (int i = 0; i < DensityFactorNames.Length; i++)
                    factorLines.Add(DensityFactorNames[i] + " root="
                        + rootFactors[i].ToString("F2") + ", "
                        + factorDiffering[i] + " member tiles differ, "
                        + factorNonUnit[i] + " non-unit, member range "
                        + (memberTiles == 0 ? "n/a"
                            : factorLow[i].ToString("F2") + "-"
                                + factorHigh[i].ToString("F2")));
                Log.Message("[CA][Regional] member-mutator loss receipt for map "
                    + map.uniqueID + "; root tile " + rootTileId + " of "
                    + sourceTiles + " geographic source tiles (" + memberTiles
                    + " non-root); root mutators ["
                    + (rootMutators.Count == 0 ? "none"
                        : string.Join(",", rootMutators.Select(
                            mutator => mutator.defName)))
                    + "]; per-source [" + string.Join(" | ", perTile)
                    + "]; union [" + (union.Count == 0 ? "none"
                        : string.Join(",", union.Select(
                            mutator => mutator.defName)))
                    + "]; member-only discarded ["
                    + (carriersByMemberOnlyMutator.Count == 0 ? "none"
                        : string.Join(",", carriersByMemberOnlyMutator.Keys
                            .Select(mutator => mutator.defName).OrderBy(
                                defName => defName)))
                    + "]; provisional hook-shape classes, not verified scope ["
                    + (scopeLines.Count == 0 ? "none"
                        : string.Join(" | ", scopeLines))
                    + "]; suppression flags [" + string.Join(" | ", flagLines)
                    + "]; density factors [" + string.Join(" | ", factorLines)
                    + "]");
            }
            catch (Exception exception)
            {
                Log.Warning("[CA][Regional] member-mutator loss receipt failed: "
                    + exception);
            }
        }

        private static List<TileMutatorDef> MutatorsOf(Tile tile)
        {
            var mutators = new List<TileMutatorDef>();
            if (tile == null) return mutators;
            foreach (TileMutatorDef mutator in tile.Mutators)
                if (mutator != null) mutators.Add(mutator);
            return mutators;
        }

        private static bool CarriesFlag(List<TileMutatorDef> mutators,
            Func<TileMutatorDef, bool> reader)
        {
            foreach (TileMutatorDef mutator in mutators)
                if (reader(mutator)) return true;
            return false;
        }

        private static float FactorProduct(List<TileMutatorDef> mutators,
            Func<TileMutatorDef, float> reader)
        {
            float product = 1f;
            foreach (TileMutatorDef mutator in mutators)
                product *= reader(mutator);
            return product;
        }

        // Provisional, and only ever provisional. This reads the shape of a
        // worker's declared hooks, not what the worker does inside them: the
        // cave/coast/river families are edge-to-edge or whole-map by
        // construction; the spawning hooks register Things into the map's
        // listers and used rects; the grid hooks write index-addressable
        // arrays; no generation hook at all means def-level scalars only.
        // Hook shape cannot establish tile-locality. A grid-hook worker is
        // free to read map.Size, map.Center, map.Tile or the map edges and
        // write every cell. TileMutatorWorker_Mountain proved it: overriding
        // only GeneratePostElevationFertility, it classified here as
        // tile-local while actually laying a map-wide half-plane, which is why
        // CA now owns it and it reports as connected. Nothing may promote a
        // verdict from this method into a staging or adapter registry without
        // that worker's body having been read.
        private static string ScopeClassOf(TileMutatorDef mutator)
        {
            TileMutatorWorker worker = mutator.Worker;
            if (worker == null) return "region-wide";
            if (mutator.IsCave
                || worker is TileMutatorWorker_Caves
                || OwnsProjectedTopology(worker)
                || MirroredInlandWater(worker)
                || worker is TileMutatorWorker_Wetland
                || worker is TileMutatorWorker_Valley
                || worker is TileMutatorWorker_LavaFlow
                || worker is TileMutatorWorker_Plateau
                || worker is TileMutatorWorker_Cliffs
                || worker is TileMutatorWorker_Dunes
                || worker is TileMutatorWorker_IceDunes
                || worker is TileMutatorWorker_Crevasse
                || worker is TileMutatorWorker_HotSprings
                || worker is TileMutatorWorker_TerraformingScar
                || worker is TileMutatorWorker_Hollow
                || worker is TileMutatorWorker_Chasm
                || worker is TileMutatorWorker_ObsidianDeposits
                || worker is TileMutatorWorker_Harbor
                || worker is TileMutatorWorker_AncientVent
                || worker is TileMutatorWorker_AncientRuins
                || worker is TileMutatorWorker_AncientStructure
                || worker is TileMutatorWorker_AbandonedColony
                || worker is TileMutatorWorker_AncientQuarry
                || worker is TileMutatorWorker_AncientUplink
                // Adapted by the earlier substrate arc: per-cell fields,
                // selected-feature resolution, engine-root and frame
                // adapters, and the headwater terrain patch. Named here so
                // the member-mutator loss receipt reports them truthfully.
                || worker is TileMutatorWorker_PlantGrove
                || worker is TileMutatorWorker_AnimalHabitat
                || worker is TileMutatorWorker_MineralRich
                || worker is TileMutatorWorker_MixedBiome
                || worker is TileMutatorWorker_Patches
                || worker is TileMutatorWorker_Headwater)
                return "connected";
            if (OverridesAnyHook(worker, SpawningHooks)) return "incompatible";
            if (OverridesAnyHook(worker, GridValuedHooks)) return "tile-local";
            return "region-wide";
        }

        private static string OverriddenHooks(TileMutatorWorker worker)
        {
            if (worker == null) return "none";
            var hooks = new List<string>();
            foreach (string hook in GridValuedHooks.Concat(SpawningHooks))
                if (OverridesHook(worker, hook))
                    hooks.Add(hook.Replace("Generate", string.Empty));
            return hooks.Count == 0 ? "none" : string.Join("+", hooks);
        }

        private static bool OverridesAnyHook(TileMutatorWorker worker,
            string[] hooks)
        {
            foreach (string hook in hooks)
                if (OverridesHook(worker, hook)) return true;
            return false;
        }

        private static bool OverridesHook(TileMutatorWorker worker, string hook)
        {
            MethodInfo method = worker.GetType().GetMethod(hook,
                BindingFlags.Instance | BindingFlags.Public, null,
                new[] { typeof(Map) }, null);
            return method != null
                && method.DeclaringType != typeof(TileMutatorWorker);
        }

        private static bool Active(Map map)
        {
            return map?.GetComponent<CARegionalProjectionMapComponent>()
                ?.Active == true;
        }

        private static bool OwnsProjectedTopology(TileMutatorWorker worker)
        {
            return worker is TileMutatorWorker_Coast
                || worker is TileMutatorWorker_River
                || worker is TileMutatorWorker_Mountain
                || worker is TileMutatorWorker_Caves;
        }

        // The radial lake family the kernel re-expresses per carrier. The
        // native root instance must not also flood the aggregate at
        // region-proportionate scale.
        private static bool MirroredInlandWater(TileMutatorWorker worker)
        {
            return CARegionalProjectionKernel.InlandWaterKindOf(worker)
                != CAInlandWaterKind.None;
        }

        // The terrain phase is a narrower set. A cave worker's GeneratePostTerrain
        // only touches cells where MapGenerator.Caves is already positive, so it
        // is self-masking and safe to run over the whole map - but it still has
        // to run once per carried cave def rather than for the root's alone,
        // which is what CA drives.
        private static bool OwnsProjectedTerrain(TileMutatorWorker worker)
        {
            return worker is TileMutatorWorker_Coast
                || worker is TileMutatorWorker_River
                || worker is TileMutatorWorker_Caves;
        }
    }

    [HarmonyPatch(typeof(GenStep_MutatorPostElevationFertility), nameof(
        GenStep_MutatorPostElevationFertility.Generate))]
    internal static class CARegionalNativeProjectedElevationMutatorPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Map map)
        {
            return CARegionalProjectedMutatorPass.RunPostElevation(map);
        }
    }

    [HarmonyPatch(typeof(GenStep_MutatorPostTerrain), nameof(
        GenStep_MutatorPostTerrain.Generate))]
    internal static class CARegionalNativeProjectedTerrainMutatorPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Map map)
        {
            return CARegionalProjectedMutatorPass.RunPostTerrain(map);
        }
    }

    internal static class CARegionalRiverPatchUtility
    {
        internal static bool Active(Map map)
        {
            return map?.GetComponent<CARegionalProjectionMapComponent>()
                ?.Active == true;
        }
    }
}
