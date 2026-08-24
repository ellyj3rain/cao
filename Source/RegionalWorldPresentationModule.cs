using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    [HarmonyPatch(typeof(WorldInterface), nameof(
        WorldInterface.WorldInterfaceOnGUI))]
    internal static class CARegionalWorldPoliticalOverlayPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (Current.ProgramState != ProgramState.Playing
                || !WorldRendererUtility.WorldSelected
                || Find.WorldCamera?.gameObject?.activeInHierarchy != true)
                return;
            CARegionalWorldPoliticalOverlay.Draw();
        }
    }

    internal static class CARegionalWorldPoliticalOverlay
    {
        private static readonly Color MarkerFrame =
            new Color(0.025f, 0.035f, 0.045f, 0.96f);
        private static readonly Color UnknownFaction =
            new Color(0.94f, 0.72f, 0.28f, 1f);

        internal static void Draw()
        {
            CARegionalWorldComponent world = CARegionalWorldComponent.Current;
            if (world == null) return;
            DrawWorldStandings(world);
            if (world.Regions == null || world.Regions.Count == 0) return;

            // A region worked only by frontier holdings has no
            // settlements and still has something on the ground to show.
            foreach (CARegionalPlan region in world.Regions.Where(item =>
                         item != null
                         && ((item.settlements != null
                                 && item.settlements.Count > 0)
                             || (item.frontierHoldings != null
                                 && item.frontierHoldings.Count > 0)))
                     .OrderBy(item => item.regionalId)
                     .ThenBy(item => item.startTileId))
            {
                DrawRegion(world, region);
            }
        }

        private static void DrawRegion(CARegionalWorldComponent world,
            CARegionalPlan region)
        {
            // The footprint's identity, not the landing tile's - the world map
            // must draw the same settlements regardless of where the player lands.
            string regionKey = CARegionalWorldComponent.RegionKeyFor(region,
                null);
            List<CARegionalSettlementRecord> records = world.Records
                .Where(item => item != null && item.regionKey == regionKey
                    && item.mapSize == region.mapSize).ToList();
            foreach (IGrouping<int, CARegionalSettlementPlan> cluster in
                     (region.settlements
                         ?? new List<CARegionalSettlementPlan>())
                         .Where(item => item != null
                             && VisibleInWorld(region, item))
                         .GroupBy(item => item.memberTileId)
                         .OrderBy(item => item.Key))
            {
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(
                    cluster.Key);
                if (!tile.Valid) continue;
                List<CARegionalSettlementPlan> settlements = cluster
                    .OrderBy(item => item.slot).ToList();
                List<CARegionalSettlementRecord> matching = settlements.Select(
                        settlement => records.FirstOrDefault(record =>
                            record.slot == settlement.slot))
                    .Where(record => record != null).ToList();
                List<Faction> factions = DistinctFactions(region, settlements,
                    matching);
                int standing = settlements.Max(item =>
                    (int)item.realizedScale);
                DrawCluster(tile, settlements.Count, standing, factions,
                    Tooltip(region, settlements, matching));
            }

            // Frontier holdings are persisted regional facts that no
            // world-map surface consumed: they existed only inside the
            // viewport, so a region worked by homesteads looked
            // identical on the globe to an empty one. They draw where
            // they stand, smaller than a settlement, with their real
            // resident and material state.
            foreach (IGrouping<int, CAFrontierHoldingPlan> group in
                (region.frontierHoldings
                    ?? new List<CAFrontierHoldingPlan>())
                .Where(item => item != null && item.memberTileId >= 0)
                .GroupBy(item => item.memberTileId)
                .OrderBy(item => item.Key))
            {
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(
                    group.Key);
                if (!tile.Valid) continue;
                DrawHoldings(tile, group.OrderBy(item => item.siteName
                    ?? string.Empty).ToList());
            }
        }

        // THE WORLD'S SIGNIFICANT CENTRES, AT GLOBE SCALE. Every
        // generated settlement carries a persisted standing derived from
        // the ground it stands on against the authored urban tendency,
        // and that standing reached exactly one place: the inspect line
        // of a settlement the player had already decided to click. The
        // globe drew every settlement identically, so a world of towns
        // and a world of hamlets were indistinguishable without
        // inspecting them one at a time. Only town-and-larger draw a
        // place mark - the vanilla faction icon still says who, this
        // says how big - so the marks stay few and mean something.
        private static void DrawWorldStandings(
            CARegionalWorldComponent world)
        {
            // The world's own frontier. Holdings were built only inside
            // regional plans, and plans exist only where the player has
            // been, so the frontier tendencies showed nothing across the
            // rest of the map. These derive from the same kernel the
            // plan-side realization uses, flattened once per world change
            // rather than walking the whole partition every frame.
            foreach ((CAWorldHolding holding, string region) in
                CAWorldFrontier.Visible(CARegionalGeography
                    .PartitionContextIds()))
            {
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(
                    holding.TileId);
                if (!tile.Valid) continue;
                Vector2 screen = GenWorldUI.WorldToUIPosition(
                    Find.WorldGrid.GetTileCenter(tile));
                var mark = new Rect(screen.x - 8f, screen.y - 2f, 16f, 13f);
                if (!new Rect(-18f, -18f, UI.screenWidth + 36f,
                        UI.screenHeight + 36f).Overlaps(mark))
                    continue;
                if (Event.current.type == EventType.Repaint)
                    CAPlaceGlyphs.DrawHolding(
                        new Vector2(screen.x, screen.y + 5f), 2.4f,
                        holding.MaterialLevel);
                TooltipHandler.TipRegion(mark,
                    (holding.Form == 1 ? "Homestead" : "Cabin")
                    + "\n" + holding.Residents + " resident"
                    + (holding.Residents == 1 ? "" : "s")
                    + " on the frontier of " + region);
            }

            IReadOnlyList<CARegionalWorldSettlementState> states =
                world.WorldSettlementStates;
            if (states == null || states.Count == 0) return;
            for (int i = 0; i < states.Count; i++)
            {
                CARegionalWorldSettlementState state = states[i];
                if (state == null || state.urbanClass < 4) continue;
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(
                    state.tileId);
                if (!tile.Valid) continue;
                Vector2 screen = GenWorldUI.WorldToUIPosition(
                    Find.WorldGrid.GetTileCenter(tile));
                var bounds = new Rect(screen.x - 12f, screen.y - 4f,
                    24f, 18f);
                if (!new Rect(-24f, -24f, UI.screenWidth + 48f,
                        UI.screenHeight + 48f).Overlaps(bounds))
                    continue;
                if (Event.current.type != EventType.Repaint) continue;
                Faction owner = CARegionalPlanUtility.FactionByLoadId(
                    state.factionLoadId);
                CAPlaceGlyphs.DrawSettlement(
                    new Vector2(screen.x, screen.y + 7f),
                    state.urbanClass >= 5 ? 3.2f : 2.8f,
                    state.urbanClass, owner?.Color ?? UnknownFaction);
            }
        }

        private static void DrawHoldings(PlanetTile tile,
            List<CAFrontierHoldingPlan> holdings)
        {
            Vector2 screen = GenWorldUI.WorldToUIPosition(
                Find.WorldGrid.GetTileCenter(tile));
            var bounds = new Rect(screen.x - 9f, screen.y - 2f, 18f, 14f);
            if (!new Rect(-20f, -20f, UI.screenWidth + 40f,
                    UI.screenHeight + 40f).Overlaps(bounds)) return;
            if (Event.current.type == EventType.Repaint)
            {
                int best = holdings.Max(item => item.materialLevel);
                CAPlaceGlyphs.DrawHolding(
                    new Vector2(screen.x, screen.y + 6f), 2.6f, best);
            }
            string label = holdings.Count == 1
                ? holdings[0].siteName ?? (holdings[0].form == 1
                    ? "Homestead" : "Cabin")
                : holdings.Count + " frontier holdings";
            TooltipHandler.TipRegion(bounds, label + "\n"
                + holdings.Sum(item => Math.Max(0, item.residentCount))
                + " resident" + (holdings.Sum(item =>
                    Math.Max(0, item.residentCount)) == 1 ? "" : "s"));
        }

        private static bool VisibleInWorld(CARegionalPlan region,
            CARegionalSettlementPlan settlement)
        {
            return region.FactionPlan(settlement.OwningFactionKey)
                ?.visibleInWorld != false;
        }

        private static List<Faction> DistinctFactions(CARegionalPlan region,
            List<CARegionalSettlementPlan> settlements,
            List<CARegionalSettlementRecord> records)
        {
            var factions = new List<Faction>();
            foreach (CARegionalSettlementPlan settlement in settlements)
            {
                Faction faction = records.FirstOrDefault(record =>
                        record.slot == settlement.slot)?.faction
                    ?? region.FactionPlan(settlement.OwningFactionKey)
                        ?.resolvedFaction;
                if (faction != null && !factions.Contains(faction))
                    factions.Add(faction);
            }
            return factions;
        }

        private static void DrawCluster(PlanetTile tile, int settlementCount,
            int standing, List<Faction> factions, string tooltip)
        {
            Vector2 screen = GenWorldUI.WorldToUIPosition(
                Find.WorldGrid.GetTileCenter(tile));
            float size = Mathf.Clamp(15f + standing * 3f
                + Mathf.Sqrt(Mathf.Max(0, settlementCount - 1)) * 4f,
                15f, 34f);
            Rect bounds = new Rect(screen.x - size * 0.5f,
                screen.y - size * 0.5f, size, size);
            if (!new Rect(-size, -size, UI.screenWidth + size * 2f,
                    UI.screenHeight + size * 2f).Overlaps(bounds))
                return;

            if (Event.current.type == EventType.Repaint)
            {
                // The place itself: a roofed cluster that grows with its
                // standing, in its holder's color. Several holders stand
                // as their own smaller neighbors around the main place.
                Color roof = factions.Count >= 1 ? factions[0].Color
                    : UnknownFaction;
                CAPlaceGlyphs.DrawSettlement(bounds.center,
                    Mathf.Clamp(3.4f + standing * 0.35f, 3.4f, 5.4f),
                    standing, roof);
                for (int i = 1; i < Math.Min(4, factions.Count); i++)
                {
                    float angle = -50f + 95f * i;
                    Vector2 satellite = bounds.center + new Vector2(
                        Mathf.Cos(angle * Mathf.Deg2Rad),
                        Mathf.Sin(angle * Mathf.Deg2Rad))
                        * (size * 0.52f);
                    CAPlaceGlyphs.DrawSettlement(satellite, 2.8f, 1,
                        factions[i].Color);
                }
            }
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(bounds,
                tooltip);
        }

        private static string Tooltip(CARegionalPlan region,
            List<CARegionalSettlementPlan> settlements,
            List<CARegionalSettlementRecord> records)
        {
            int population = records.Sum(record =>
                Math.Max(0, record.populationCurrent));
            int physicalComplexes = settlements.Select(item =>
                item.PhysicalClusterKey).Distinct().Count();
            var lines = new List<string>
            {
                settlements.Count + (settlements.Count == 1
                    ? " regional settlement" : " regional settlements")
                    + " · " + physicalComplexes
                    + (physicalComplexes == 1 ? " physical complex"
                        : " physical complexes")
                    + (records.Count > 0 ? " · population " + population
                        : "")
            };
            int aggregateRoles = settlements.Aggregate(0, (mask, item) => mask
                | item.operationalRoleMask);
            if ((aggregateRoles & CARegionalOperationalRoles.KnownMask) != 0)
                lines.Add("Declared roles: "
                    + CARegionalOperationalRoles.Summary(aggregateRoles));
            foreach (CARegionalSettlementPlan settlement in settlements)
            {
                CARegionalSettlementRecord record = records.FirstOrDefault(
                    item => item.slot == settlement.slot);
                CARegionalFactionPlan group = region.FactionPlan(
                    settlement.OwningFactionKey);
                Faction faction = record?.faction ?? group?.resolvedFaction;
                string name = record?.name ?? "Authored settlement "
                    + (settlement.slot + 1);
                // Each settlement's own standing. The glyph collapses a
                // shared tile to its largest place, so without this a
                // hamlet beside an urban centre was unreadable.
                string detail = CARegionalSettlements.ScaleWords(
                        (CASettlementScale)settlement.realizedScale)
                    + " · " + (!settlement.HasFactionOwner
                        ? "no faction"
                        : faction?.Name ?? group?.Summary
                            ?? "unresolved faction");
                if (record != null)
                    detail += " · " + record.TechnologicalKnowledgeLabel
                        + " · population " + record.populationCurrent;
                if ((settlement.operationalRoleMask
                        & CARegionalOperationalRoles.KnownMask) != 0)
                    detail += " · roles " + CARegionalOperationalRoles
                        .Summary(settlement.operationalRoleMask);
                lines.Add(name + " — " + detail);
            }
            return string.Join("\n", lines);
        }
    }
}
