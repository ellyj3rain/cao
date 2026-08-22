using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonistAwareness
{
    // A CONFIRMED REGION IS ONE CONTINUOUS PLACE. Its member settlements are
    // locations INSIDE the loaded regional map, yet selecting one on the
    // world offered "Send caravan" -- the affordance for travelling to a
    // separate map -- and no way to simply look at the place. While the
    // region's own map is the current map, a member selection now carries a
    // jump to its actual ground, and the caravan affordance is disabled with
    // the reason stated rather than silently removed, so travel FROM other
    // maps in a larger campaign remains representable.
    [HarmonyPatch(typeof(Settlement), nameof(Settlement.GetGizmos))]
    internal static class CARegionalMemberSelectionPatch
    {
        [HarmonyPostfix]
        private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos,
            Settlement __instance)
        {
            CARegionalPlan region = null;
            CARegionalSettlementRecord record = null;
            Map regionalMap = null;
            try
            {
                var world = CARegionalWorldComponent.Current;
                region = world?.FindRegionContaining(__instance.Tile);
                if (region != null)
                {
                    foreach (Map map in Find.Maps
                        ?? new List<Map>())
                    {
                        if (world.FindRegionForMap(map) != region) continue;
                        regionalMap = map;
                        record = world.ForMap(map)?.FirstOrDefault(item =>
                            item != null && item.memberTileId
                                == __instance.Tile.tileId
                            && item.localRect != CellRect.Empty);
                        break;
                    }
                }
            }
            catch (Exception) { }

            bool member = region != null && regionalMap != null;
            bool onOwnMap = member && Find.CurrentMap == regionalMap;

            foreach (Gizmo gizmo in gizmos ?? Enumerable.Empty<Gizmo>())
            {
                if (onOwnMap && gizmo is Command command
                    && IsCaravanCommand(command))
                {
                    command.Disable(
                        "CA_PartOfLoadedRegion".Translate(region.regionalId
                            ?? "region"));
                }
                yield return gizmo;
            }

            if (member && record != null)
            {
                CellRect rect = record.localRect;
                yield return new Command_Action
                {
                    defaultLabel = "CA_JumpToSettlement".Translate(),
                    defaultDesc = "CA_JumpToSettlementDesc".Translate(),
                    icon = TexButton.ShowZones,
                    action = () => CameraJumper.TryJump(
                        rect.CenterCell, regionalMap)
                };
            }
        }

        private static bool IsCaravanCommand(Command command)
        {
            try
            {
                string label = command.defaultLabel;
                if (label.NullOrEmpty()) return false;
                return label == "CommandSendCaravan".Translate()
                    || label == "CommandFormCaravan".Translate();
            }
            catch (Exception) { return false; }
        }
    }

    // ONE CONFIRMED REGION PRESENTS AS ONE WORLD ENTITY. Materialization
    // keeps a vanilla Settlement world object per member -- factions,
    // trade, and quests hang off them -- but presenting each of them as an
    // independently drawn, independently selectable vanilla destination is
    // exactly the fragmentation the region exists to end. While a member's
    // region is realized, the member's own world presentation is
    // suppressed: it does not draw, does not expand an icon, does not take
    // selection; the region's canonical arrival object carries the world
    // identity, and the members are read inside the regional views.
    internal static class CARegionalMemberPresentation
    {
        private static readonly HashSet<int> probedTiles =
            new HashSet<int>();

        internal static bool Suppressed(WorldObject worldObject)
        {
            if (!(worldObject is Settlement settlement)) return false;
            if (Current.ProgramState != ProgramState.Playing) return false;
            try
            {
                var world = CARegionalWorldComponent.Current;
                CARegionalPlan region =
                    world?.FindRegionContaining(settlement.Tile);
                if (probedTiles.Count < 40
                    && probedTiles.Add(settlement.Tile.tileId))
                {
                    Log.Message("[CA][Regional][Presentation] probe: "
                        + (settlement.Name ?? "settlement") + " tile "
                        + settlement.Tile.tileId + "; world component "
                        + (world == null ? "MISSING" : "present")
                        + "; region "
                        + (region == null ? "NOT FOUND"
                            : (region.regionalId ?? "?") + " start "
                                + region.startTileId)
                        + " -> "
                        + (region == null ? "presented (no region)"
                            : settlement.Tile.tileId != region.startTileId
                                ? "suppressed member" : "canonical"));
                }
                if (region == null) return false;
                // the canonical arrival object keeps its presentation
                return settlement.Tile.tileId != region.startTileId;
            }
            catch (Exception) { return false; }
        }

        internal static WorldObject Canonical(WorldObject worldObject)
        {
            try
            {
                var world = CARegionalWorldComponent.Current;
                CARegionalPlan region =
                    world?.FindRegionContaining(worldObject.Tile);
                if (region == null) return worldObject;
                MapParent anchor = Find.WorldObjects?.MapParentAt(
                    region.StartTile);
                return anchor ?? worldObject;
            }
            catch (Exception) { return worldObject; }
        }
    }

    [HarmonyPatch(typeof(WorldObjectSelectionUtility),
        nameof(WorldObjectSelectionUtility.HiddenBehindTerrainNow))]
    internal static class CARegionalMemberHiddenPatch
    {
        [HarmonyPostfix]
        private static void Postfix(WorldObject o, ref bool __result)
        {
            if (!__result && CARegionalMemberPresentation.Suppressed(o))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.Print))]
    internal static class CARegionalMemberPrintPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(WorldObject __instance)
        {
            return !CARegionalMemberPresentation.Suppressed(__instance);
        }
    }

    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.Draw))]
    internal static class CARegionalMemberDrawPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(WorldObject __instance)
        {
            return !CARegionalMemberPresentation.Suppressed(__instance);
        }
    }

    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.SelectableNow),
        MethodType.Getter)]
    internal static class CARegionalMemberSelectablePatch
    {
        [HarmonyPostfix]
        private static void Postfix(WorldObject __instance,
            ref bool __result)
        {
            if (__result
                && CARegionalMemberPresentation.Suppressed(__instance))
                __result = false;
        }
    }

    // Selection by ANY path -- mouse, camera jump, search, quest links --
    // resolves a suppressed member to the region's canonical object.
    [HarmonyPatch(typeof(WorldSelector), nameof(WorldSelector.Select))]
    internal static class CARegionalMemberSelectPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref WorldObject obj)
        {
            if (obj != null
                && CARegionalMemberPresentation.Suppressed(obj))
                obj = CARegionalMemberPresentation.Canonical(obj);
        }
    }

    // Clicking anywhere on a member's ground selects the region's
    // canonical object -- focus changes, geography never does.
    [HarmonyPatch(typeof(GenWorldUI),
        nameof(GenWorldUI.WorldObjectsUnderMouse))]
    internal static class CARegionalMemberUnderMousePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref List<WorldObject> __result)
        {
            if (__result == null) return;
            for (int i = __result.Count - 1; i >= 0; i--)
            {
                WorldObject worldObject = __result[i];
                if (worldObject == null
                    || !CARegionalMemberPresentation.Suppressed(worldObject))
                    continue;
                WorldObject canonical =
                    CARegionalMemberPresentation.Canonical(worldObject);
                if (canonical != worldObject
                    && !__result.Contains(canonical))
                    __result[i] = canonical;
                else
                    __result.RemoveAt(i);
            }
        }
    }

    // A MEMBER TILE'S MAP IS THE REGIONAL MAP. Every vanilla travel and
    // visit action -- caravan arrival, attack, trade visit, quest map
    // opening -- funnels through GetOrGenerateMap, and for a member tile the
    // vanilla body cannot find the region's map (its parent sits at the
    // anchor tile), so it generated a fresh disconnected vanilla settlement
    // map. That is precisely the "members behave as independent vanilla
    // destinations" acceptance failure. For a tile inside a confirmed
    // region: the loaded regional map is the answer; if the regional map is
    // not loaded, generation is redirected to the region's canonical
    // arrival tile so one region only ever generates its one map.
    [HarmonyPatch(typeof(GetOrGenerateMapUtility),
        nameof(GetOrGenerateMapUtility.GetOrGenerateMap),
        new[] { typeof(PlanetTile), typeof(IntVec3), typeof(WorldObjectDef),
            typeof(IEnumerable<GenStepWithParams>), typeof(bool) })]
    internal static class CARegionalMemberMapOpenPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref PlanetTile tile, ref Map __result)
        {
            try
            {
                var world = CARegionalWorldComponent.Current;
                CARegionalPlan region = world?.FindRegionContaining(tile);
                if (region == null) return true;
                // Transient world content standing on regional ground --
                // quest sites, camps -- owns its own encounter map. Only
                // requests for the region's own ground funnel to the
                // regional map.
                bool ownMapParentAtTile = Find.WorldObjects?.ObjectsAt(tile)
                    ?.Any(item => item is MapParent
                        && !(item is WorldObject_CARegionalMemberReservation)
                        && item.Tile.tileId != region.startTileId) == true;
                if (ownMapParentAtTile) return true;
                foreach (Map map in Find.Maps ?? new List<Map>())
                {
                    if (world.FindRegionForMap(map) != region) continue;
                    Log.Message("[CA][Regional][MapOpen] tile " + tile.tileId
                        + " resolved to the loaded regional map of "
                        + (region.regionalId ?? "region"));
                    __result = map;
                    return false;
                }
                PlanetTile canonical = region.StartTile;
                if (canonical.Valid && canonical.tileId != tile.tileId)
                {
                    Log.Message("[CA][Regional][MapOpen] tile " + tile.tileId
                        + " redirected to regional anchor "
                        + canonical.tileId + " for one-map generation");
                    tile = canonical;
                }
                return true;
            }
            catch (Exception) { return true; }
        }
    }
}
