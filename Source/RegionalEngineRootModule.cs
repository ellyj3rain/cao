using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // ENGINE ROOT vs LANDING CHOICE.
    //
    // CA stitches several adjacent world tiles into one backing Map. RimWorld
    // parents that Map to exactly ONE tile - Map.Tile is not stored on the Map,
    // it reads through to the MapParent world object (Verse/MapInfo.cs:16,
    // Verse/Map.cs:384). CA sets Find.GameInitData.startingTile to the LANDING
    // tile (RegionalSetupModule.cs:1760), so map.Tile == plan.startTileId and
    // every engine socket keyed on the parented tile became landing-bound.
    //
    // The hard invariant this file exists to satisfy: the SAME candidate with
    // the SAME footprint and a DIFFERENT landing member must produce identical
    // regional geography and feature state. Only the landing marker, the start
    // position, and genuinely entry-specific state may differ.
    //
    // WHAT THIS IS NOT. It does not reassign startingTile. The landing tile
    // stays the landing tile exactly as Page_SelectStartingSite.cs:218 defines
    // it; ScenPart_PlayerFaction.cs:62 still places the player settlement where
    // the player clicked; Game.cs still copies that into GameInfo.startingTile,
    // so Map.IsStartingMap (Verse/Map.cs:404) stays true and every
    // onlyOnStartingMap scatterer, the nomadic-mineables multiplier and the
    // shrine suppression behave exactly as they do in vanilla. Nothing here
    // relocates a world object, so no pod, caravan, gravship or camera target
    // moves.
    //
    // What it does instead is remove CA's SEMANTIC DEPENDENCE on the parented
    // tile, one read at a time, gated on "is this map regional" so that
    // non-regional generation is untouched by construction.
    //
    // The residual is a POPULATION of direct map.Tile readers, not one seed.
    // Intercepting only MapGenerator's master seed plus the Map.TileInfo getter
    // was tried and REFUTED: RockNoises.cs:22 (stone palette),
    // MapCellsInRandomOrder.cs:40 (plant layout), GenStep_Pollution.cs:17,
    // GenStep_Snow.cs:20 and several TileMutatorWorkers read map.Tile DIRECTLY
    // and never go through TileInfo, so the palette, the plant layout, snow and
    // pollution all still moved with the landing pin.
    internal static class CARegionalEngineRoot
    {
        private sealed class Resolution
        {
            internal bool regional;
            internal PlanetTile anchor;
            internal Tile anchorInfo;
            internal string regionalId;
        }

        // Weak keys: a resolution lives exactly as long as its Map, and a later
        // game can never inherit an earlier one's answer.
        private static readonly ConditionalWeakTable<Map, Resolution> ByMap =
            new ConditionalWeakTable<Map, Resolution>();

        // The Map.TileInfo substitution is installed on a property that some of
        // the resolution path may itself touch through a mod or a future engine
        // change. One flag turns any such reentry into the native answer rather
        // than a stack overflow.
        [ThreadStatic] private static bool resolving;

        // One line per map per subject. These sit on hot paths - the TileInfo
        // getter is read tens of thousands of times per map - so nothing here
        // may log per call.
        private static readonly HashSet<string> Announced =
            new HashSet<string>();

        internal static void Announce(Map map, string subject, string detail)
        {
            string key = (map?.uniqueID ?? -1) + "#" + subject;
            lock (Announced)
            {
                if (!Announced.Add(key)) return;
            }
            Log.Message("[CA][Regional][Root] " + subject + ": " + detail);
        }

        internal static void ForgetAnnouncements()
        {
            lock (Announced) { Announced.Clear(); }
        }

        // Resolution reads plan state and map.Tile / map.Size only. It must
        // never read Map.TileInfo, Map.Biome or the projection component:
        // TileInfo is patched below, and the projection component's Active
        // getter builds the whole projection, which reads map.Biome.
        internal static CARegionalPlan PlanForTile(PlanetTile mapTile,
            IntVec3 backingSize)
        {
            if (!mapTile.Valid) return null;
            CARegionalPlan plan = null;
            try
            {
                plan = CARegionalWorldComponent.Current
                    ?.FindRegionForTile(mapTile, backingSize);
                if (plan == null)
                    plan = CARegionalSetupSession.PreviewPlanFor(mapTile,
                        backingSize);
            }
            catch { return null; }
            if (plan == null || plan.memberTileIds == null
                || plan.memberTileIds.Count <= 1) return null;
            return plan;
        }

        private static Resolution Resolve(Map map)
        {
            if (map == null) return null;
            Resolution cached;
            if (ByMap.TryGetValue(map, out cached)) return cached;
            if (resolving) return null;
            resolving = true;
            try
            {
                if (map.IsPocketMap) return Remember(map, null, true);
                PlanetTile landing = map.Tile;
                if (!landing.Valid) return null;
                CARegionalPlan plan = PlanForTile(landing, map.Size);
                // Only a resolution taken against a live world is durable. A
                // negative answer reached before the world component exists is
                // an artefact of ordering, not a fact about the map.
                bool durable = Verse.Find.World != null
                    && CARegionalWorldComponent.Current != null;
                return Remember(map, plan, durable);
            }
            catch { return null; }
            finally { resolving = false; }
        }

        private static Resolution Remember(Map map, CARegionalPlan plan,
            bool durable)
        {
            var resolution = new Resolution();
            if (plan != null)
            {
                PlanetTile anchor = plan.BundleRoot;
                if (anchor.Valid)
                {
                    resolution.regional = true;
                    resolution.anchor = anchor;
                    resolution.regionalId = plan.regionalId;
                    try { resolution.anchorInfo = Verse.Find.WorldGrid[anchor]; }
                    catch { resolution.anchorInfo = null; }
                    if (resolution.anchorInfo == null)
                        resolution.regional = false;
                }
            }
            if (!durable && !resolution.regional) return resolution;
            try { ByMap.Add(map, resolution); }
            catch { }
            if (resolution.regional)
                Announce(map, "engine root", "map " + map.uniqueID
                    + " parented to landing tile " + map.Tile.tileId
                    + "; footprint anchor " + resolution.anchor.tileId
                    + " (" + resolution.regionalId + ") now answers every "
                    + "intercepted root read");
            return resolution;
        }

        // ---- substitution entry points -------------------------------------
        // Every one of these is shaped so a transpiler can drop it in place of
        // the native read with an identical evaluation stack.

        internal static bool IsRegional(Map map)
        {
            Resolution resolution = Resolve(map);
            return resolution != null && resolution.regional;
        }

        internal static PlanetTile AnchorTile(Map map)
        {
            if (map == null) return PlanetTile.Invalid;
            Resolution resolution = Resolve(map);
            return resolution != null && resolution.regional
                ? resolution.anchor : map.Tile;
        }

        internal static int AnchorTileId(Map map)
        {
            PlanetTile tile = AnchorTile(map);
            return tile.Valid ? tile.tileId : -1;
        }

        // Returns null - meaning "leave the native result alone" - for every
        // map that is not regional.
        internal static Tile AnchorTileInfo(Map map)
        {
            Resolution resolution = Resolve(map);
            return resolution != null && resolution.regional
                ? resolution.anchorInfo : null;
        }

        // MapGenerator.GenerateMap computes its master seed from parent.Tile
        // before the Map exists, so this takes the parent and the backing size
        // the CA generation prefix has already written into the argument slot.
        internal static PlanetTile AnchorTileForParent(MapParent parent,
            IntVec3 backingSize)
        {
            if (parent == null) return PlanetTile.Invalid;
            PlanetTile landing = parent.Tile;
            CARegionalPlan plan = PlanForTile(landing, backingSize);
            if (plan == null) return landing;
            PlanetTile anchor = plan.BundleRoot;
            if (!anchor.Valid) return landing;
            if (anchor.tileId != landing.tileId)
                Announce(null, "master seed " + plan.regionalId,
                    "MapGenerator seeded from "
                    + "footprint anchor " + anchor.tileId + " instead of "
                    + "landing tile " + landing.tileId + " for "
                    + plan.regionalId);
            return anchor;
        }

        // PollutionGrid.PollutionTick is the ONLY write in the whole root
        // trace: it stamps the map's average pollution onto the parented world
        // tile. Re-anchoring that write is exactly as wrong as leaving it -
        // the bundle's average is not the anchor's pollution any more than it
        // is the landing tile's - so for a regional map the stamp is
        // SUPPRESSED. Returning an invalid tile is how: the native guard is
        // `if (map.Tile.Valid)`, so the whole write-back block is skipped while
        // the dirty flag is still cleared.
        //
        // KNOWN LIMIT, stated rather than hidden: the world-tile pollution
        // readout for a regional footprint therefore stops tracking the map.
        // Splitting the stamp per carrying tile needs a per-member pollution
        // count that does not exist; it is not synthesised here.
        internal static PlanetTile SuppressedTileForPollutionWriteBack(Map map)
        {
            if (map == null) return PlanetTile.Invalid;
            if (!IsRegional(map)) return map.Tile;
            Announce(map, "pollution write-back", "map " + map.uniqueID
                + " is regional; the world-tile pollution stamp is suppressed "
                + "rather than written to one member of the footprint");
            return PlanetTile.Invalid;
        }
    }

    // The reusable root substitution. Every target below reads map.Tile
    // DIRECTLY for a decision that is regional geography, so each read is
    // replaced with the footprint anchor.
    //
    // FAIL CLOSED, and count BEFORE mutating. A lazily-yielded transpiler that
    // silently matches nothing reports success and leaves the method unpatched;
    // that idiom already cost this repository one false "replaced 0" alarm. The
    // expected read count is recorded per method, so a body change refuses the
    // rewrite loudly instead of redirecting something never analysed.
    [HarmonyPatch]
    internal static class CARegionalRootTileReadPatch
    {
        private sealed class Target
        {
            internal Type type;
            internal string method;
            internal Type[] parameters;
            internal MethodType kind = MethodType.Normal;
            internal int expected;
            internal bool suppress;
            internal string why;
        }

        private static readonly List<Target> Targets = new List<Target>
        {
            new Target
            {
                type = typeof(RockNoises), method = nameof(RockNoises.Init),
                parameters = new[] { typeof(Map) }, expected = 1,
                why = "stone palette - NaturalRockTypesIn(map.Tile) fixes both "
                    + "the native rock set and, because each RockNoise draws "
                    + "Rand.Range in iteration order, the rockDef->Perlin "
                    + "pairing that CA's union postfix extends"
            },
            new Target
            {
                type = typeof(MapCellsInRandomOrder),
                method = "CreateListIfShould",
                parameters = Type.EmptyTypes, expected = 1,
                why = "initial plant layout - GenStep_Plants walks this order "
                    + "and its shuffle is seeded worldSeed ^ map.Tile hash"
            },
            new Target
            {
                type = typeof(GenStep_Pollution),
                method = nameof(GenStep_Pollution.Generate),
                parameters = new[] { typeof(Map), typeof(GenStepParams) },
                expected = 1,
                why = "map-wide pollution percentage is read off the parented "
                    + "tile's pollution value"
            },
            new Target
            {
                type = typeof(GenStep_Snow),
                method = nameof(GenStep_Snow.Generate),
                parameters = new[] { typeof(Map), typeof(GenStepParams) },
                expected = 1,
                why = "snow depth counts twelfths below freezing at map.Tile"
            },
            new Target
            {
                type = typeof(MapTemperature),
                method = nameof(MapTemperature.SeasonalTemp),
                kind = MethodType.Getter, expected = 1,
                why = "seasonal temperature is climate, and one backing map has "
                    + "one climate; it also feeds GenStep_Snow's depth"
            },
            new Target
            {
                type = typeof(MapTemperature),
                method = nameof(MapTemperature.OutdoorTemp),
                kind = MethodType.Getter, expected = 1,
                why = "outdoor temperature, same reason as SeasonalTemp"
            },
            new Target
            {
                type = typeof(GenLocalDate),
                method = nameof(GenLocalDate.Twelfth),
                parameters = new[] { typeof(Map) }, expected = 1,
                why = "the twelfth is a climate/season reading, and GenStep_"
                    + "Snow takes it from the map"
            },
            new Target
            {
                type = typeof(GenLocalDate),
                method = nameof(GenLocalDate.Season),
                parameters = new[] { typeof(Map) }, expected = 1,
                why = "season, same reason as Twelfth"
            },
            new Target
            {
                type = typeof(TileMutatorWorker_MineralRich),
                method = nameof(TileMutatorWorker_MineralRich
                    .GeneratePostTerrain),
                parameters = new[] { typeof(Map) }, expected = 1,
                why = "which mineral the landmark yields is feature identity"
            },
            new Target
            {
                type = typeof(TileMutatorWorker_Stockpile),
                method = nameof(TileMutatorWorker_Stockpile.GeneratePostFog),
                parameters = new[] { typeof(Map) }, expected = 1,
                why = "which stockpile type the hatch carries is feature "
                    + "identity"
            },
            new Target
            {
                type = typeof(PollutionGrid), method = "PollutionTick",
                parameters = Type.EmptyTypes, expected = 3, suppress = true,
                why = "the only WRITE in the root trace; suppressed for "
                    + "regional maps rather than re-anchored"
            },
        };

        private static readonly Dictionary<MethodBase, Target> Resolved =
            new Dictionary<MethodBase, Target>();

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (Target target in Targets)
            {
                MethodBase method = null;
                try
                {
                    method = target.kind == MethodType.Getter
                        ? AccessTools.PropertyGetter(target.type,
                            target.method)
                        : AccessTools.Method(target.type, target.method,
                            target.parameters);
                }
                catch { method = null; }
                if (method == null)
                {
                    Log.Error("[CA][Regional][Root] "
                        + target.type.Name + "." + target.method
                        + " not found; its root substitution is NOT installed. "
                        + target.why);
                    continue;
                }
                Resolved[method] = target;
                yield return method;
            }
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpile(
            IEnumerable<CodeInstruction> instructions,
            MethodBase __originalMethod)
        {
            // Eager. Materialise and count before touching anything, so a
            // refusal is reported instead of a lazily-unpatched method.
            var code = new List<CodeInstruction>(instructions);
            Target target;
            if (__originalMethod == null
                || !Resolved.TryGetValue(__originalMethod, out target))
            {
                Log.Error("[CA][Regional][Root] root substitution REFUSED for "
                    + (__originalMethod?.Name ?? "(unknown)")
                    + ": no expected-read record. The method stays "
                    + "landing-bound.");
                return code;
            }

            MethodInfo tileGetter = AccessTools.PropertyGetter(typeof(Map),
                nameof(Map.Tile));
            MethodInfo replacement = AccessTools.Method(
                typeof(CARegionalEngineRoot), target.suppress
                    ? nameof(CARegionalEngineRoot
                        .SuppressedTileForPollutionWriteBack)
                    : nameof(CARegionalEngineRoot.AnchorTile));
            int matches = tileGetter == null ? 0
                : code.Count(instruction => instruction.Calls(tileGetter));
            if (tileGetter == null || replacement == null
                || matches != target.expected)
            {
                Log.Error("[CA][Regional][Root] root substitution REFUSED for "
                    + __originalMethod.DeclaringType?.Name + "."
                    + __originalMethod.Name + ": matched " + matches
                    + " map.Tile read(s), expected " + target.expected
                    + ". That method stays landing-bound - "
                    + target.why + ". Fail-closed refusal, not a partial "
                    + "rewrite.");
                return code;
            }

            var output = new List<CodeInstruction>(code.Count);
            int replaced = 0;
            foreach (CodeInstruction instruction in code)
            {
                if (instruction.Calls(tileGetter))
                {
                    // The Map is already on the stack and the replacement
                    // returns a PlanetTile, so the evaluation stack is
                    // unchanged either side of the substitution.
                    output.Add(new CodeInstruction(OpCodes.Call, replacement)
                        .MoveLabelsFrom(instruction)
                        .MoveBlocksFrom(instruction));
                    replaced++;
                    continue;
                }
                output.Add(instruction);
            }
            Log.Message("[CA][Regional][Root] "
                + __originalMethod.DeclaringType?.Name + "."
                + __originalMethod.Name + ": replaced " + replaced
                + " map.Tile read(s) with the footprint anchor"
                + (target.suppress ? " sentinel (write suppressed)" : "")
                + " - " + target.why);
            return output;
        }
    }

    // MapGenerator.cs:90 - the master seed.
    //
    //   int seed = Gen.HashCombineInt(Find.World.info.Seed,
    //                                 parent?.Tile.GetHashCode() ?? 0);
    //
    // PlanetTile.GetHashCode is (tileId * 397) ^ layerId, so the seed varies
    // directly with the parented tileId, and MapGenerator.cs:328 derives EVERY
    // genstep's Rand.Seed from it. Substituting parent.Tile with the footprint
    // anchor is one read and it re-anchors the entire generation stream.
    //
    // CA's own generation prefix (RegionalWorldModule.cs:1732) runs before this
    // body, registers the region and rewrites the mapSize argument to the
    // backing size, so both values this substitution needs are already correct
    // in their argument slots when it executes.
    [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateMap))]
    internal static class CARegionalMasterSeedPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpile(
            IEnumerable<CodeInstruction> instructions,
            MethodBase __originalMethod)
        {
            var code = new List<CodeInstruction>(instructions);
            MethodInfo tileGetter = AccessTools.PropertyGetter(
                typeof(WorldObject), nameof(WorldObject.Tile));
            MethodInfo replacement = AccessTools.Method(
                typeof(CARegionalEngineRoot),
                nameof(CARegionalEngineRoot.AnchorTileForParent));
            int matches = tileGetter == null ? 0
                : code.Count(instruction => instruction.Calls(tileGetter));

            // The rewrite pushes argument 0 as the backing size and relies on
            // argument 1 already being the MapParent whose Tile is on the
            // stack, so it is only valid while GenerateMap keeps that
            // signature. Anything else means the body changed.
            ParameterInfo[] parameters = __originalMethod?.GetParameters();
            bool shapeOk = __originalMethod != null
                && __originalMethod.IsStatic
                && parameters != null && parameters.Length >= 2
                && parameters[0].ParameterType == typeof(IntVec3)
                && parameters[1].ParameterType == typeof(MapParent);
            if (tileGetter == null || replacement == null || !shapeOk
                || matches != 1)
            {
                Log.Error("[CA][Regional][Root] master-seed substitution "
                    + "REFUSED: matched " + matches + " parent.Tile read(s), "
                    + "expected 1; signature shape "
                    + (shapeOk ? "ok" : "UNEXPECTED (needs a static "
                        + "GenerateMap whose first two parameters are the "
                        + "IntVec3 map size and the MapParent)")
                    + ". MapGenerator's master seed stays landing-keyed, which "
                    + "means every genstep downstream of it does too. This is "
                    + "the worst half-state available, so it is an error and "
                    + "not a warning.");
                return code;
            }

            var output = new List<CodeInstruction>(code.Count + 1);
            int replaced = 0;
            foreach (CodeInstruction instruction in code)
            {
                if (instruction.Calls(tileGetter))
                {
                    // Stack holds the MapParent; push the backing map size and
                    // call the anchor resolver, which returns a PlanetTile -
                    // the same value shape parent.Tile produced.
                    output.Add(new CodeInstruction(OpCodes.Ldarg_0)
                        .MoveLabelsFrom(instruction)
                        .MoveBlocksFrom(instruction));
                    output.Add(new CodeInstruction(OpCodes.Call, replacement));
                    replaced++;
                    continue;
                }
                output.Add(instruction);
            }
            Log.Message("[CA][Regional][Root] MapGenerator.GenerateMap: "
                + "replaced " + replaced + " parent.Tile read(s); the map "
                + "generation master seed is now footprint-anchored.");
            return output;
        }
    }

    // Map.TileInfo (Verse/Map.cs:392) is `Find.WorldGrid[Tile]`. Roughly ninety
    // engine sites read the parented tile's record through it - Map.Biome,
    // Map.NextGenSeed, the mutator list MapGenerator assembles its genstep set
    // from, both of GenStep_ElevationFertility's all-or-nothing gates
    // (preventNaturalElevation and WaterCovered, either of which lets ONE
    // landing member suppress or zero the entire elevation field),
    // MapGenUtility, WeatherDecider, WildAnimalSpawner, WildPlantSpawner,
    // PlantUtility and MapTemperature. Anchoring the getter carries all of them
    // at once.
    //
    // Map.Tile itself is deliberately NOT touched. IsStartingMap compares
    // GameInfo.startingTile against Map.Tile, so leaving Map.Tile alone is what
    // keeps the highest-risk coupling in the entire trace coherent.
    [HarmonyPatch(typeof(Map), nameof(Map.TileInfo), MethodType.Getter)]
    internal static class CARegionalTileInfoAnchorPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Map __instance, ref Tile __result)
        {
            if (__instance == null || __instance.IsPocketMap) return;
            Tile anchored = CARegionalEngineRoot.AnchorTileInfo(__instance);
            if (anchored != null) __result = anchored;
        }
    }

    // GenTicks.cs:58 reads Find.GameInitData.startingTile BEFORE any map
    // exists, so no map-side redirect can reach it. It decides the starting
    // twelfth (per-tile temperature) and the time zone (per-tile longitude),
    // which then reach snow depth, plant and animal state through
    // GenLocalDate.
    //
    // The substitution swaps the field for the duration of the getter rather
    // than reimplementing its arithmetic, so vanilla's own body - including its
    // memo - stays authoritative. That also disposes of the staleness hazard:
    // ConfiguredTicksAbsAtGameStartCache keys on the value present during the
    // computation, so a landing change is a cache MISS that recomputes to the
    // same anchored answer, never a stale hit.
    //
    // Restoration is a finalizer, not a postfix, so a throw inside the getter
    // cannot leave startingTile pointing at the anchor.
    [HarmonyPatch(typeof(GenTicks),
        nameof(GenTicks.ConfiguredTicksAbsAtGameStart), MethodType.Getter)]
    internal static class CARegionalStartTicksAnchorPatch
    {
        [HarmonyPrefix]
        private static void Prefix(out PlanetTile __state)
        {
            __state = PlanetTile.Invalid;
            try
            {
                GameInitData data = Verse.Find.GameInitData;
                if (data == null || !data.startingTile.Valid) return;
                CARegionalPlan plan =
                    CARegionalSetupSession.PendingForCurrentWorld;
                if (plan == null || plan.memberTileIds == null
                    || !plan.memberTileIds.Contains(data.startingTile.tileId))
                    return;
                PlanetTile anchor = plan.BundleRoot;
                if (!anchor.Valid || anchor.tileId == data.startingTile.tileId)
                    return;
                __state = data.startingTile;
                data.startingTile = anchor;
                CARegionalEngineRoot.Announce(null,
                    "start ticks " + plan.regionalId,
                    "starting twelfth and time zone taken from footprint "
                    + "anchor " + anchor.tileId + " instead of landing tile "
                    + __state.tileId + " for " + plan.regionalId);
            }
            catch { __state = PlanetTile.Invalid; }
        }

        [HarmonyFinalizer]
        private static void Finalizer(PlanetTile __state)
        {
            if (!__state.Valid) return;
            try
            {
                GameInitData data = Verse.Find.GameInitData;
                if (data != null) data.startingTile = __state;
            }
            catch { }
        }
    }
}
