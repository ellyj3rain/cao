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
    // Env-gated screenshot probe for the production creator surface.
    // It creates a small world and authors factions and settlements through
    // the same state and generation paths used by the new-game flow.
    [HarmonyPatch(typeof(Root_Entry), "Update")]
    internal static class CACreatorShotsProbePatch
    {
        private static int stage;
        private static int stageFrame = -1;
        private static bool active = true;
        private static string outDir;
        private static CARegionalPlan plan;
        private static CAExpandedLandmassProfile profile;

        private static void Shot(string name)
        {
            // the dev-mode debug log auto-opens over the surface; a
            // clean capture dismisses it first
            Window log = Verse.Find.WindowStack.Windows
                .FirstOrDefault(w => w != null
                    && w.GetType().Name == "EditWindow_Log");
            if (log != null) Verse.Find.WindowStack.TryRemove(log, false);
            string path = outDir + "/" + name + ".png";
            ScreenCapture.CaptureScreenshot(path, 1);
            Log.Message("[CA][Shots] captured " + path);
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!active) return;
            try
            {
                outDir = Environment.GetEnvironmentVariable(
                    "CA_CREATOR_SHOTS");
                if (outDir.NullOrEmpty()) { active = false; return; }
                int frame = Time.frameCount;
                switch (stage)
                {
                    case 0:
                        if (frame < 60
                            || Current.ProgramState != ProgramState.Entry
                            || LongEventHandler.AnyEventNowOrWaiting
                            || DefDatabase<ThingDef>.DefCount == 0)
                            return;
                        Step0_WorldAndPlan();
                        stage = 1;
                        stageFrame = frame;
                        return;
                    case 1: // region panel with faction cards
                        if (frame - stageFrame < 60) return;
                        Shot("1-region-factions");
                        stage = 2;
                        stageFrame = frame;
                        return;
                    case 2: // faction editor and map emphasis
                        if (frame - stageFrame < 15) return;
                        CARegionMapWidget.SelectFaction(
                            plan.factions[0].key);
                        stage = 3;
                        stageFrame = frame;
                        return;
                    case 3:
                        if (frame - stageFrame < 30) return;
                        Shot("2-faction-editor");
                        stage = 4;
                        stageFrame = frame;
                        return;
                    case 4: // full composition expanded
                        if (frame - stageFrame < 15) return;
                        ExpandFullComposition();
                        stage = 5;
                        stageFrame = frame;
                        return;
                    case 5:
                        if (frame - stageFrame < 30) return;
                        Shot("3-full-composition");
                        stage = 6;
                        stageFrame = frame;
                        return;
                    case 6: // settlement object
                        if (frame - stageFrame < 15) return;
                        CARegionMapWidget.SelectSettlement(
                            plan.settlements[0].slot);
                        stage = 7;
                        stageFrame = frame;
                        return;
                    case 7:
                        if (frame - stageFrame < 30) return;
                        Shot("4-settlement");
                        stage = 8;
                        stageFrame = frame;
                        return;
                    case 8: // world tendencies, the demoted layer
                        if (frame - stageFrame < 15) return;
                        Verse.Find.WindowStack.Add(
                            new Dialog_CAWorldGeneration(plan.worldPolicy));
                        stage = 9;
                        stageFrame = frame;
                        return;
                    case 9:
                        if (frame - stageFrame < 30) return;
                        Shot("5-world-tendencies");
                        stage = 10;
                        stageFrame = frame;
                        return;
                    case 10:
                        if (frame - stageFrame < 20) return;
                        active = false;
                        Log.Message("[CA][Shots] complete, quitting");
                        Root.Shutdown();
                        return;
                }
            }
            catch (Exception e)
            {
                active = false;
                Log.Warning("[CA][Shots] probe failed at stage " + stage
                    + ": " + e);
            }
        }

        private static void Step0_WorldAndPlan()
        {
            Game.ClearCaches();
            Current.Game = new Game();
            Current.Game.InitData = new GameInitData();
            ScenarioDef scenDef = ScenarioDefOf.Crashlanded
                ?? DefDatabase<ScenarioDef>.AllDefs.FirstOrDefault();
            if (scenDef != null)
            {
                Current.Game.Scenario = scenDef.scenario;
                Current.Game.Scenario.PreConfigure();
            }
            Find.GameInitData.startedFromEntry = true;
            Find.GameInitData.mapSize = 325;

            Log.Message("[CA][Shots] generating a small real world "
                + "(seed alysaliu)...");
            Current.Game.World = WorldGenerator.GenerateWorld(0.05f,
                "alysaliu", OverallRainfall.Normal,
                OverallTemperature.Normal, OverallPopulation.Normal,
                LandmarkDensity.Normal);
            Current.Game.World.FinalizeInit(false);

            PlanetTile root = PlanetTile.Invalid;
            PlanetLayer surface = Verse.Find.WorldGrid.Surface;
            for (int i = 0; i < surface.TilesCount; i++)
            {
                PlanetTile tile = new PlanetTile(i, surface);
                var reason = new System.Text.StringBuilder();
                if (TileFinder.IsValidTileForNewSettlement(tile, reason))
                { root = tile; break; }
            }
            if (!root.Valid)
                throw new Exception("no settleable tile found");

            CAExpandedLandmassProfile.TryFor(325, out profile);
            plan = CARegionalPlanUtility.Create(profile, root, true);

            // Two factions and three settlements, written through the
            // same plan state the UI writes.
            var groupA = new CARegionalFactionPlan
            {
                key = 1,
                source = CARegionalFactionSource.NewWorldFaction,
                customFactionDefName =
                    (DefDatabase<FactionDef>.GetNamedSilentFail(
                            "OutlanderCivil")
                        ?? DefDatabase<FactionDef>.AllDefs
                            .FirstOrDefault(def => !def.isPlayer
                                && def.humanlikeFaction
                                && !def.hidden))?.defName,
                customName = "The Reed Compact",
                authored = true,
                visibleInWorld = true
            };
            var groupB = new CARegionalFactionPlan
            {
                key = 2,
                source = CARegionalFactionSource.NewWorldFaction,
                customFactionDefName = groupA.customFactionDefName,
                customName = "Kingdom of the Vale",
                authored = true,
                visibleInWorld = true
            };
            plan.factions.Add(groupA);
            plan.factions.Add(groupB);
            var members = plan.memberTileIds;
            for (int i = 0; i < 3 && i < members.Count; i++)
                plan.settlements.Add(new CARegionalSettlementPlan
                {
                    slot = i,
                    memberTileId = members[i],
                    factionKey = i < 2 ? 1 : 2,
                    siteClusterKey = i,
                    persistent = true,
                    populationOrigin =
                        CASettlementOrigin.ScenarioOverride
                });
            CARegionalPlanUtility.EnsureRelationRows(plan);

            // Fill everything open through the same entry points the
            // Generate button calls.
            foreach (CARegionalFactionPlan group in
                plan.factions)
            {
                group.EnsureCultureAndPolitics(plan);
                CAFactionAxes.Derive(plan, group);
            }
            foreach (CARegionalSettlementPlan place in plan.settlements)
                CASettlementComposition.EnsureDerived(plan, place);
            CARegionalSettlements.EnsureSettlementPattern(plan);

            Verse.Find.WindowStack.Add(
                new Page_CAStartingRegion(plan, profile));
            Log.Message("[CA][Shots] creator open: "
                + plan.factions.Count + " factions, "
                + plan.settlements.Count + " settlements");
        }

        private static void ExpandFullComposition()
        {
            Window window = Verse.Find.WindowStack.Windows
                .FirstOrDefault(item =>
                    item is Page_CAStartingRegion);
            if (window == null) return;
            AccessTools.Field(typeof(Page_CAStartingRegion),
                "authorEveryDimension")?.SetValue(window, true);
            // scroll the panel down so the expanded composition is in
            // frame for the capture
            AccessTools.Field(typeof(Page_CAStartingRegion), "scroll")
                ?.SetValue(window, new Vector2(0f, 1600f));
        }
    }
}
