using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Per-cell substitution of def-level mutator fields whose native consumers
    // already resolve a cell but read the ROOT tile's mutators.
    //
    // Traced consumers, not inferred:
    //   MapGenUtility.TerrainFrom(IntVec3 c, Map map, ...) reads
    //     map.TileInfo.Mutators for preventsPondGeneration and preventPatches
    //     while already holding c and already calling map.BiomeAt(c).
    //   WildPlantSpawner.CheckSpawnWildPlantAt(IntVec3 c, float
    //     plantDensityFactor, ...) takes the cell and the factor as arguments.
    //
    // Both are genuinely per-cell, so the owning constituent's value can be
    // substituted without allocating counts or redistributing placement. That
    // is what separates them from junk, geysers and fish, which remain
    // unresolved.
    // Per-map receipt for the two substitutions whose patches are otherwise
    // SILENT. Both run per cell per candidate - far too hot to log per call -
    // so this accumulates and emits exactly one line per subject per map.
    //
    // The line reports what actually matters: how many calls resolved to a
    // constituent OTHER than the root tile, and, of a bounded sample, how many
    // produced a value DIFFERENT from what the root tile would have produced.
    // "Substituted" alone would prove only that the patch ran; "differed" is
    // what proves the substitution changed the outcome.
    internal static class CAPerCellReceipt
    {
        private const int EmitAfterCalls = 4096;
        private const int SampleLimit = 256;

        private sealed class Tally
        {
            internal int Calls;
            internal int NonRoot;
            internal int Sampled;
            internal int Differed;
            internal string Example;
            internal bool Emitted;
        }

        private static readonly Dictionary<string, Tally> tallies =
            new Dictionary<string, Tally>();

        // True while the sample budget is unspent, so a caller can skip
        // computing the root-tile comparison value once sampling is done.
        internal static bool WantsSample(string subject, Map map)
        {
            if (map == null) return false;
            lock (tallies)
            {
                Tally tally;
                return tallies.TryGetValue(Key(subject, map), out tally)
                    ? tally.Sampled < SampleLimit
                    : true;
            }
        }

        internal static void Record(string subject, Map map, bool nonRoot,
            bool sampled, bool differed, string example)
        {
            if (map == null) return;
            string line = null;
            lock (tallies)
            {
                string key = Key(subject, map);
                Tally tally;
                if (!tallies.TryGetValue(key, out tally))
                    tallies[key] = tally = new Tally();
                tally.Calls++;
                if (nonRoot) tally.NonRoot++;
                if (sampled)
                {
                    tally.Sampled++;
                    if (differed)
                    {
                        tally.Differed++;
                        if (tally.Example == null) tally.Example = example;
                    }
                }
                if (tally.Emitted || tally.Calls < EmitAfterCalls) return;
                tally.Emitted = true;
                line = "[CA][Regional] " + subject + " receipt for map "
                    + map.uniqueID + ": " + tally.Calls + " calls, "
                    + tally.NonRoot + " resolved to a non-root constituent, "
                    + tally.Differed + " of " + tally.Sampled
                    + " sampled differed from the root-tile value"
                    + (tally.Example == null ? "" : "; e.g. " + tally.Example);
            }
            Log.Message(line);
        }

        private static string Key(string subject, Map map)
        {
            return subject + "#" + map.uniqueID;
        }
    }

    internal static class CARegionalPerCellFields
    {
        private static readonly IList<TileMutatorDef> NoMutators =
            Array.Empty<TileMutatorDef>();

        // The mutators authorized for one cell. Core land reads its owning
        // selected area's features. The narrow geographic halo retains biome,
        // coast and link context but has no authored mutator authority, so it
        // returns the neutral empty list rather than inheriting either the
        // halo tile's unevaluated features or the selected anchor's features.
        // Non-regional generation receives the native list unchanged.
        internal static IList<TileMutatorDef> MutatorsAt(Map map, IntVec3 cell)
        {
            bool regionalAuthority = CARegionalSetupSession
                .IntendsRegionalMap(map);
            try
            {
                CARegionalProjectionMapComponent projection =
                    map?.GetComponent<CARegionalProjectionMapComponent>();
                if (projection?.Active != true)
                    return regionalAuthority
                        ? FailClosedMutators(map,
                            "projection component was not active")
                        : NativeMutators(map);
                regionalAuthority = true;
                PlanetTile owner = projection.MemberTileAt(cell);
                if (!projection.IsSelectedCore(owner)) return NoMutators;
                Tile info = owner.Valid ? owner.Tile : null;
                return info?.Mutators ?? NoMutators;
            }
            catch (Exception exception)
            {
                return regionalAuthority
                    ? FailClosedMutators(map, exception.GetType().Name)
                    : NativeMutators(map);
            }
        }

        private static IList<TileMutatorDef> NativeMutators(Map map)
        {
            try { return map?.TileInfo?.Mutators ?? NoMutators; }
            catch { return NoMutators; }
        }

        private static IList<TileMutatorDef> FailClosedMutators(Map map,
            string reason)
        {
            int mapId = map?.uniqueID ?? -1;
            Log.ErrorOnce("[CA][Regional] authorized per-cell mutator lookup "
                + "failed neutral on regional map " + mapId + " (" + reason
                + "); anchor mutators were NOT allowed to spread across the "
                + "region.", 947113 ^ mapId);
            return NoMutators;
        }

        // Product of the owning constituent's plantDensityFactor. Native
        // computes this from Tile.PlantDensityFactor on the root tile.
        internal static float PlantDensityFactorAt(Map map, IntVec3 cell,
            float nativeFactor)
        {
            try
            {
                CARegionalProjectionMapComponent projection =
                    map?.GetComponent<CARegionalProjectionMapComponent>();
                if (projection?.Active != true) return nativeFactor;
                PlanetTile owner = projection.MemberTileAt(cell);
                if (!owner.Valid) return nativeFactor;
                // Halo cells keep their projected biome/coast geography, but
                // neither the anchor's selected mutators nor an unevaluated
                // halo mutator owns them. Neutralize the root-derived factor.
                if (!projection.IsSelectedCore(owner)) return 1f;
                Tile info = owner.Tile;
                if (info == null) return nativeFactor;
                float product = 1f;
                foreach (TileMutatorDef mutator in info.Mutators)
                    if (mutator != null) product *= mutator.plantDensityFactor;

                // The root-derived value arrives as an argument, so the
                // comparison costs nothing and every call can be sampled.
                bool differed = Math.Abs(product - nativeFactor) > 0.0001f;
                // Against the ANCHOR, not map.Tile. The root the native code
                // actually consumed is the footprint anchor now that the
                // Map.TileInfo getter answers with it; comparing the owner
                // against the landing tile would make this receipt report
                // "off-root" for the anchor's own cells and "on-root" for the
                // landing tile's - exactly inverted.
                CAPerCellReceipt.Record("plant density", map,
                    owner.tileId != CARegionalEngineRoot.AnchorTileId(map),
                    true, differed,
                    differed
                        ? "cell " + cell + " owned by tile " + owner.tileId
                            + ": root " + nativeFactor.ToString("0.###")
                            + " -> constituent " + product.ToString("0.###")
                        : null);
                return product;
            }
            catch { return nativeFactor; }
        }

        // WildPlantSpawner caches commonality map-wide, but its final choice
        // still has the candidate cell. Undo only the selected root
        // PlantGrove factor and apply the owning area's factor at that seam.
        // This preserves every other term in PlantChoiceWeight (global balance,
        // clusters, local distribution and fertility) while making the grove
        // a spatial world feature instead of a root-tile accident.
        internal static float PlantGroveChoiceMultiplier(Map map,
            IntVec3 cell, ThingDef plant)
        {
            try
            {
                CARegionalProjectionMapComponent projection = map
                    ?.GetComponent<CARegionalProjectionMapComponent>();
                if (projection?.Active != true || plant == null) return 1f;
                PlanetTile ownerTile = projection.MemberTileAt(cell);
                if (!ownerTile.Valid || ownerTile.Tile == null) return 1f;

                // Halo land is visual/geographic context, not selected feature
                // authority. Remove the anchor's cached grove there, but never
                // execute an unselected halo grove that compatibility did not
                // inspect.
                bool selectedOwner = projection.IsSelectedCore(ownerTile);
                float ownerFactor = selectedOwner
                    ? GroveFactor(ownerTile.Tile, plant, ownerTile) : 1f;
                // Reproduce the factor native actually cached so the quotient
                // removes it exactly: mutators come from TileInfo (the regional
                // anchor), while the native call passes map.Tile.
                float rootFactor = GroveFactor(map.TileInfo, plant, map.Tile);
                if (Math.Abs(ownerFactor - 1f) < 0.0001f
                    && Math.Abs(rootFactor - 1f) < 0.0001f) return 1f;
                if (rootFactor <= 0f) return 1f;

                // Native does not merely raise the factor to the biome count.
                // Each pass first adds another 1/N share of the unmodified
                // commonality and then multiplies, so for factor F its exact
                // normalized cache shape is:
                //   F^N + (F + F^2 + ... + F^N) / N
                // Use the quotient of those shapes to remove the cached root
                // grove and install the owning area's grove without disturbing
                // the rest of PlantChoiceWeight.
                int biomePasses = Math.Max(1, map.Biomes.Count());
                float rootShape = GroveCacheShape(rootFactor, biomePasses);
                if (rootShape <= 0f) return 1f;
                float multiplier = GroveCacheShape(ownerFactor, biomePasses)
                    / rootShape;
                bool differed = Math.Abs(multiplier - 1f) > 0.0001f;
                CAPerCellReceipt.Record("plant grove commonality", map,
                    ownerTile.tileId != CARegionalEngineRoot.AnchorTileId(map),
                    true, differed, differed
                        ? plant.defName + " at " + cell + " owned by tile "
                            + ownerTile.tileId + ": root factor "
                            + rootFactor.ToString("0.###") + " -> area factor "
                            + ownerFactor.ToString("0.###") + " (choice x"
                            + multiplier.ToString("0.###") + ")"
                        : null);
                return multiplier;
            }
            catch { return 1f; }
        }

        private static float GroveFactor(Tile info, ThingDef plant,
            PlanetTile tileArgument)
        {
            if (info == null || !tileArgument.Valid) return 1f;
            float factor = 1f;
            foreach (TileMutatorDef mutator in info.Mutators)
                if (mutator?.Worker is TileMutatorWorker_PlantGrove)
                    factor *= mutator.Worker.PlantCommonalityFactorFor(plant,
                        tileArgument);
            return factor;
        }

        private static float GroveCacheShape(float factor, int biomePasses)
        {
            int passes = Math.Max(1, biomePasses);
            float power = factor;
            float sum = power;
            for (int i = 1; i < passes; i++)
            {
                power *= factor;
                sum += power;
            }
            return power + sum / passes;
        }
    }

    // Replaces the exact map.TileInfo.Mutators read inside each analysed
    // MapGenUtility method with the authorized mutator list for the cell. This
    // changes no other TileInfo field and lets halo geography receive neutral
    // suppression/override decisions without manufacturing a fake Tile.
    [HarmonyPatch]
    internal static class CARegionalTerrainFromOwnerPatch
    {
        // Every target has the same shape: static, IntVec3 cell as argument 0,
        // Map as argument 1, reading map.TileInfo.Mutators for a per-cell
        // decision it then falls back to map.BiomeAt(cell) for - and CA already
        // overrides BiomeAt per constituent. The expected read count is
        // recorded per method so a body change fails closed rather than
        // silently redirecting something never analysed.
        //
        //   TerrainFrom            2  preventsPondGeneration, preventPatches
        //   BeachTerrainAt         1  overrideCoastalBeachTerrain
        //   LakeshoreTerrainAt     1  overrideLakeBeachTerrain
        //   MudTerrainAt           1  overrideMudTerrain
        //   RiverbankTerrainAt     1  overrideRiverbankTerrain
        private static readonly Dictionary<string, int> ExpectedReads =
            new Dictionary<string, int>
            {
                { nameof(MapGenUtility.TerrainFrom), 2 },
                { nameof(MapGenUtility.BeachTerrainAt), 1 },
                { nameof(MapGenUtility.LakeshoreTerrainAt), 1 },
                { nameof(MapGenUtility.MudTerrainAt), 1 },
                { nameof(MapGenUtility.RiverbankTerrainAt), 1 },
            };
        private static readonly HashSet<string> InstalledTargets =
            new HashSet<string>();
        private static bool refused;

        internal static bool RedirectHealthy
        {
            get
            {
                lock (InstalledTargets)
                    return !refused
                        && InstalledTargets.Count == ExpectedReads.Count;
            }
        }

        internal static string RedirectHealthSummary
        {
            get
            {
                lock (InstalledTargets)
                    return InstalledTargets.Count + "/"
                        + ExpectedReads.Count + " target(s) installed"
                        + (refused ? "; at least one target refused" : "");
            }
        }

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in ExpectedReads.Keys)
            {
                MethodBase method = AccessTools.Method(typeof(MapGenUtility),
                    name);
                if (method == null)
                {
                    lock (InstalledTargets) refused = true;
                    Log.Error("[CA][Regional] MapGenUtility." + name
                        + " not found; its per-cell redirect is NOT installed.");
                }
                else yield return method;
            }
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpile(
            IEnumerable<CodeInstruction> instructions,
            MethodBase __originalMethod)
        {
            int expected;
            if (__originalMethod == null
                || !ExpectedReads.TryGetValue(__originalMethod.Name,
                    out expected))
                expected = -1;
            MethodInfo tileInfoGetter =
                AccessTools.PropertyGetter(typeof(Map), nameof(Map.TileInfo));
            MethodInfo mutatorsGetter = AccessTools.PropertyGetter(typeof(Tile),
                nameof(Tile.Mutators));
            MethodInfo replacement = AccessTools.Method(
                typeof(CARegionalPerCellFields),
                nameof(CARegionalPerCellFields.MutatorsAt));

            // Eager: materialise and count BEFORE mutating, so a failed rewrite
            // is reported rather than silently producing an unpatched method
            // through a lazy iterator.
            var code = new List<CodeInstruction>(instructions);
            int tileReads = tileInfoGetter == null ? 0
                : code.Count(instruction => instruction.Calls(tileInfoGetter));
            int matches = 0;
            if (tileInfoGetter != null && mutatorsGetter != null)
                for (int i = 0; i + 1 < code.Count; i++)
                    if (code[i].Calls(tileInfoGetter)
                        && code[i + 1].Calls(mutatorsGetter))
                        matches++;

            // FAIL CLOSED on anything unexpected, not only on zero. The rewrite
            // pushes argument 0 as the cell, so it is only valid while
            // TerrainFrom stays static with IntVec3 as its first parameter; and
            // a match count other than the expected one means the body changed
            // and some read would be redirected that we never analysed.
            ParameterInfo[] parameters = __originalMethod?.GetParameters();
            bool shapeOk = __originalMethod != null
                && __originalMethod.IsStatic
                && parameters != null && parameters.Length > 0
                && parameters[0].ParameterType == typeof(IntVec3);
            if (tileInfoGetter == null || mutatorsGetter == null
                || replacement == null || !shapeOk || expected < 0
                || matches != expected || tileReads != expected)
            {
                lock (InstalledTargets) refused = true;
                Log.Error("[CA][Regional] per-cell redirect REFUSED for "
                    + (__originalMethod?.Name ?? "(unknown)") + ": matched "
                    + matches + " complete map.TileInfo.Mutators read(s) and "
                    + tileReads + " TileInfo read(s), expected " + expected
                    + "; signature shape "
                    + (shapeOk ? "ok" : "UNEXPECTED (needs a static method "
                        + "whose first parameter is the IntVec3 cell)")
                    + ". That method stays root-only. Fail-closed refusal, not "
                    + "a partial rewrite.");
                return code;
            }

            var output = new List<CodeInstruction>(code.Count);
            for (int i = 0; i < code.Count; i++)
            {
                CodeInstruction instruction = code[i];
                if (instruction.Calls(tileInfoGetter))
                {
                    // Stack holds the Map. Push the cell and replace the whole
                    // TileInfo.Mutators pair with the authorized-list lookup.
                    output.Add(new CodeInstruction(OpCodes.Ldarg_0)
                        .MoveLabelsFrom(instruction)
                        .MoveBlocksFrom(instruction));
                    CodeInstruction call = new CodeInstruction(OpCodes.Call,
                        replacement).MoveLabelsFrom(code[i + 1])
                        .MoveBlocksFrom(code[i + 1]);
                    output.Add(call);
                    i++;
                    continue;
                }
                output.Add(instruction);
            }
            Log.Message("[CA][Regional] " + __originalMethod.Name
                + ": redirected " + matches
                + " map.TileInfo.Mutators read(s) to the selected area owning "
                + "the cell; geographic halo reads are neutral.");
            lock (InstalledTargets)
                InstalledTargets.Add(__originalMethod.Name);
            return output;
        }
    }

    // Substitutes the owning constituent's plant density for the cell being
    // considered. The factor arrives as a parameter, so no transpiler is
    // needed.
    //
    // KNOWN LIMITATION, deliberately not hidden: the caller also passes
    // wholeMapNumDesiredPlants, a map-wide target derived from the root tile.
    // Substituting the per-cell factor without also re-deriving that target
    // makes local density correct relative to its constituent while the map's
    // total plant budget is still root-derived. That is strictly better than
    // root-only everywhere and strictly not finished, which is why this
    // contract is ImplementedUnverified.
    [HarmonyPatch(typeof(WildPlantSpawner), nameof(
        WildPlantSpawner.CheckSpawnWildPlantAt))]
    internal static class CARegionalPlantDensityPerCellPatch
    {
        [HarmonyPrefix]
        private static void Prefix(WildPlantSpawner __instance, IntVec3 c,
            ref float plantDensityFactor)
        {
            try
            {
                Map map = Traverse.Create(__instance).Field("map")
                    .GetValue<Map>();
                if (map == null) return;
                plantDensityFactor = CARegionalPerCellFields
                    .PlantDensityFactorAt(map, c, plantDensityFactor);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(WildPlantSpawner), "PlantChoiceWeight")]
    internal static class CARegionalPlantGroveChoicePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Map ___map, ThingDef plantDef, IntVec3 c,
            ref float __result)
        {
            __result *= CARegionalPerCellFields.PlantGroveChoiceMultiplier(
                ___map, c, plantDef);
        }
    }

    // RuntimeWorkerBehaviour: the defs that act through def.Worker with no
    // generation hook. Tracing their consumers corrected the framing recorded
    // in P1. The problem is NOT mainly that CA's private generation instances
    // leave the singleton stale - CA never creates a private instance for these
    // defs at all, because it only does so for workers it drives, and these
    // have no generation hook to drive. The defect is the same root-tile read
    // as everywhere else:
    //
    //   WildAnimalSpawner.CommonalityOfAnimalNow(def, loc) iterates
    //     map.TileInfo.Mutators and passes map.Tile - root, though it holds loc.
    //   Map.Biomes => TileInfo.Biomes would be the ROOT tile's biome set,
    //     which is where MixedBiome's secondary biome enters - but CA already
    //     replaces it with projection.AllBiomes in
    //     CARegionalMapBiomesPatch (RegionalSetupModule). Tile.Biomes itself is
    //     already per-tile, so nothing further is needed there.
    //   WildPlantSpawner's commonality loop reads map.TileInfo.Mutators inside
    //     a MAP-WIDE cache (cachedPlantCommonalities), so it has no cell to
    //     substitute against and is left unresolved.
    //   AggressiveAnimalIncidentUtility casts TileMutatorDefOf.AnimalHabitat
    //     .Worker at incident time with no cell; also left unresolved.
    [HarmonyPatch(typeof(WildAnimalSpawner), "CommonalityOfAnimalNow")]
    internal static class CARegionalAnimalCommonalityPatch
    {
        private static bool loggedFailure;

        // RECOMPUTED from the native pre-root inputs, not repaired after the
        // fact. The postfix this replaces was unsound three ways, and only the
        // first was a matter of ordering:
        //
        //   1. It returned early on __result <= 0f, so a root factor of zero
        //      was unrecoverable - and its own divide-by-zero guard made that
        //      outcome MORE likely, since a zero root factor was skipped
        //      rather than undone, silently leaving the zero in place.
        //   2. The coastal term is ADDITIVE (num += CommonalityOfCoastalAnimal)
        //      and gated on the ROOT tile's IsCoastal. No multiplicative
        //      repair of a final value can add or remove an additive term, so
        //      that error was not reachable by any algebraic correction.
        //   3. The pollution branch rolls against the ROOT tile's pollution,
        //      choosing between two different biome commonality tables. That
        //      is a branch, not a factor, and equally unreachable.
        //
        // Native reference, RimWorld 1.6.4871 WildAnimalSpawner.cs:163-182.
        // Rand.Value is consumed under exactly the same short-circuit
        // condition as native (Biotech active only), so the random stream
        // stays aligned with an unpatched game.
        [HarmonyPrefix]
        private static bool Prefix(Map ___map, PawnKindDef def, IntVec3 loc,
            ref float __result)
        {
            CARegionalProjectionMapComponent projection;
            PlanetTile ownerTile;
            Tile owner;
            try
            {
                projection = ___map
                    ?.GetComponent<CARegionalProjectionMapComponent>();
                if (projection?.Active != true) return true;
                ownerTile = projection.MemberTileAt(loc);
                if (!ownerTile.Valid) return true;
                owner = ownerTile.Tile;
                if (owner == null) return true;
            }
            catch { return true; }

            try
            {
                if (def.RaceProps.waterSeeker
                    && !___map.terrainGrid.AnyWaterCells)
                {
                    __result = 0f;
                    return false;
                }

                // BiomeAt is already per-cell and CA already projects it, so
                // the biome side needs no substitution - only the tile-derived
                // inputs do.
                BiomeDef biome = ___map.BiomeAt(loc);
                bool polluted = ModsConfig.BiotechActive
                    && Rand.Value < WildAnimalSpawner
                        .PollutionAnimalSpawnChanceFromPollutionCurve
                        .Evaluate(owner.pollution);
                float num = polluted
                    ? biome.CommonalityOfPollutionAnimal(def)
                    : biome.CommonalityOfAnimal(def);
                if (owner.IsCoastal)
                    num += biome.CommonalityOfCoastalAnimal(def);
                if (projection.IsSelectedCore(ownerTile))
                    foreach (TileMutatorDef mutator in owner.Mutators)
                        if (mutator?.Worker != null)
                            num *= mutator.Worker
                                .AnimalCommonalityFactorFor(def, ownerTile);
                __result = num / def.wildGroupSize.Average;

                // Receipt. The root-tile comparison is recomputed only while
                // the sample budget is unspent, and deliberately reuses the
                // SAME polluted branch: re-rolling would consume a second
                // Rand.Value. So this isolates the coastal flag and the
                // mutator factors - the tile-derived terms - and does not
                // claim to cover a differing pollution roll.
                bool sample = CAPerCellReceipt.WantsSample(
                    "animal commonality", ___map);
                bool differed = false;
                string example = null;
                if (sample)
                {
                    Tile root = ___map.TileInfo;
                    float rootNum = polluted
                        ? biome.CommonalityOfPollutionAnimal(def)
                        : biome.CommonalityOfAnimal(def);
                    if (root != null)
                    {
                        if (root.IsCoastal)
                            rootNum += biome.CommonalityOfCoastalAnimal(def);
                        foreach (TileMutatorDef mutator in root.Mutators)
                            if (mutator?.Worker != null)
                                rootNum *= mutator.Worker
                                    .AnimalCommonalityFactorFor(def,
                                        ___map.Tile);
                    }
                    rootNum /= def.wildGroupSize.Average;
                    differed = Math.Abs(rootNum - __result) > 0.0001f;
                    if (differed)
                        example = def.defName + " at " + loc + " owned by tile "
                            + ownerTile.tileId + ": root "
                            + rootNum.ToString("0.####") + " -> constituent "
                            + __result.ToString("0.####");
                }
                CAPerCellReceipt.Record("animal commonality", ___map,
                    ownerTile.tileId != ___map.Tile.tileId, sample, differed,
                    example);
                return false;
            }
            catch (Exception e)
            {
                // Fall through to native root-tile semantics rather than
                // returning a half-computed value. Logged once: this runs per
                // animal kind per spawn attempt.
                if (!loggedFailure)
                {
                    loggedFailure = true;
                    Log.Error("[CA][Regional] per-cell animal commonality "
                        + "failed; falling back to ROOT-tile semantics for the "
                        + "rest of this session. " + e);
                }
                return true;
            }
        }
    }
}
