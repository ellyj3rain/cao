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
            if (world?.Regions == null || world.Regions.Count == 0) return;

            foreach (CARegionalPlan region in world.Regions.Where(item =>
                         item != null && item.settlements != null
                         && item.settlements.Count > 0)
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
            foreach (IGrouping<int, CARegionalSettlementPlan> cluster in region.settlements
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
                DrawCluster(tile, settlements.Count, factions,
                    Tooltip(region, settlements, matching));
            }
        }

        private static bool VisibleInWorld(CARegionalPlan region,
            CARegionalSettlementPlan settlement)
        {
            return region.FactionPlan(settlement.factionKey)
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
                    ?? region.FactionPlan(settlement.factionKey)
                        ?.resolvedFaction;
                if (faction != null && !factions.Contains(faction))
                    factions.Add(faction);
            }
            return factions;
        }

        private static void DrawCluster(PlanetTile tile, int settlementCount,
            List<Faction> factions, string tooltip)
        {
            Vector2 screen = GenWorldUI.WorldToUIPosition(
                Find.WorldGrid.GetTileCenter(tile));
            float size = Mathf.Clamp(11f + Mathf.Sqrt(Mathf.Max(0,
                settlementCount - 1)) * 4f, 11f, 21f);
            Rect bounds = new Rect(screen.x - size * 0.5f,
                screen.y - size * 0.5f, size, size);
            if (!new Rect(-size, -size, UI.screenWidth + size * 2f,
                    UI.screenHeight + size * 2f).Overlaps(bounds))
                return;

            if (Event.current.type == EventType.Repaint)
            {
                Color prior = GUI.color;
                GUI.color = MarkerFrame;
                Widgets.DrawTextureRotated(bounds, BaseContent.WhiteTex, 45f);
                Rect inner = bounds.ContractedBy(2f);
                GUI.color = factions.Count == 1 ? factions[0].Color
                    : UnknownFaction;
                Widgets.DrawTextureRotated(inner, BaseContent.WhiteTex, 45f);

                if (factions.Count > 1)
                {
                    float radius = Mathf.Max(2.5f, size * 0.22f);
                    float pipSize = Mathf.Clamp(size * 0.30f, 4f, 6f);
                    int count = Math.Min(6, factions.Count);
                    for (int i = 0; i < count; i++)
                    {
                        float angle = -90f + 360f * i / count;
                        Vector2 center = bounds.center + new Vector2(
                            Mathf.Cos(angle * Mathf.Deg2Rad),
                            Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
                        Rect pip = new Rect(center.x - pipSize * 0.5f,
                            center.y - pipSize * 0.5f, pipSize, pipSize);
                        GUI.color = factions[i].Color;
                        Widgets.DrawTextureRotated(pip, BaseContent.WhiteTex,
                            45f);
                    }
                }
                GUI.color = prior;
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
                    settlement.factionKey);
                Faction faction = record?.faction ?? group?.resolvedFaction;
                string name = record?.name ?? "Authored settlement "
                    + (settlement.slot + 1);
                string detail = faction?.Name ?? group?.Summary
                    ?? "unresolved faction";
                if (record != null)
                    detail += " · " + record.FactionEraLabel
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
