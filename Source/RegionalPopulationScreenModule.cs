using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Starting-region editor. The map and inspector edit the same persistent
    // region, faction, settlement, and population objects used by generation.
    // Factions own settlements, relations, knowledge, political beliefs, and
    // structure. Settlements own local population, facilities, infrastructure,
    // and provisions. Cultural background and Ideoligion remain population
    // facts; cultural expression is derived from local life.
    internal sealed class Page_CAStartingRegion : Page
    {
        private enum CARegionLayoutMode
        {
            Wide,
            Medium,
            Compact
        }

        private CARegionalPlan plan;
        private CAExpandedLandmassProfile profile;
        private bool profileReady;
        // A page installed in vanilla's setup chain must follow the session's
        // current plan object. Going Back can move, turn, or resize the region,
        // and each of those operations intentionally replaces Pending. Keeping
        // the first object this page saw would show and edit a discarded plan
        // when the player returned. The explicit-plan constructor below is used
        // by isolated tooling and remains pinned to the supplied object.
        private readonly bool followsSetupSession;
        private Vector2 scroll;
        private Vector2 railScroll;
        private bool showTechnicalGeography;
        private bool inspectorExpanded;
        private int compactPane = 1;
        private CARegionLayoutMode layoutMode;

        private const float Row = 28f;
        private const float Gap = 6f;
        private const float LabelWidth = 118f;
        private const int MaxAuthoredSettlements = 32;

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(Mathf.Min(1880f, UI.screenWidth - 16f),
                    Mathf.Min(1040f, UI.screenHeight - 16f));
            }
        }

        public override string PageTitle => "Starting region";

        internal Page_CAStartingRegion(Page previous, Page following)
        {
            followsSetupSession = true;
            prev = previous;
            next = following;
            if (following != null) following.prev = this;
            ConfigureWindow();
        }

        internal Page_CAStartingRegion(CARegionalPlan plan,
            CAExpandedLandmassProfile profile)
        {
            this.plan = plan;
            this.profile = profile;
            profileReady = true;
            ConfigureWindow();
        }

        private void ConfigureWindow()
        {
            doCloseX = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            draggable = false;
        }

        public override void PostOpen()
        {
            base.PostOpen();
            foreach (Window preview in Verse.Find.WindowStack.Windows
                .Where(window => window?.GetType().FullName
                    == "MapPreview.MapPreviewWindow").ToList())
                preview.Close();
            if (followsSetupSession)
                plan = CARegionalSetupSession.PendingForCurrentWorld;
            else if (plan == null)
                plan = CARegionalSetupSession.PendingForCurrentWorld;
            if (!profileReady)
                profileReady = CAExpandedLandmassProfile.TryFor(
                    Verse.Find.GameInitData.mapSize, out profile);
            if (plan != null)
            {
                if (plan.worldPolicy == null)
                    plan.worldPolicy = CAWorldTendenciesSession.Policy.Copy();
                if (plan.regionName.NullOrEmpty())
                    plan.regionName = CARegionalPlanUtility.RegionName(plan);
                CARegionalPlanUtility.EnsureRelationRows(plan);
                // Page entry is the explicit draft-initialization boundary.
                // Drawing and inspection below only read this state.
                foreach (CARegionalFactionPlan group in plan.factions
                    .Where(item => item != null))
                    group.EnsureCultureAndPolitics(plan);
                CARegionalSettlements.EnsureSettlementPattern(plan);
                foreach (CARegionalSettlementPlan settlement in
                    plan.settlements.Where(item => item != null))
                    CASettlementComposition.EnsureDerived(plan, settlement);
                CACompatibilityReport compatibility =
                    CARegionalContentCompatibility.Evaluate(plan);
                Log.Message("[CA][Regional][StartingRegion] content check: "
                    + compatibility.Summary()
                    + (compatibility.IsValid ? "; ready"
                        : "; blocking: "
                            + compatibility.ActionableFailure()));
            }
            CARegionalSetupSession.EditingDialogOpen = true;
            CARegionMapWidget.Reset();
        }

        public override void PostClose()
        {
            base.PostClose();
            CARegionalSetupSession.EditingDialogOpen = false;
            CARegionMapWidget.Release();
            CARegionalProjectionPreview.Release();
            CARegionalSetupSession.SavePending();
        }

        public override void DoWindowContents(Rect inRect)
        {
            DrawPageTitle(inRect);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 32f, inRect.width, 24f),
                CAInformationPresentation.Select(
                    "Set factions, settlements, and populations on the region map.",
                    "Set the starting region: factions, settlements, and "
                        + "populations on their actual ground.",
                    "The object list, spatial map, and selected-object inspector "
                        + "edit one persisted regional plan.", inspectorExpanded));

            if (plan == null || !profileReady)
            {
                Widgets.Label(new Rect(0f, 72f, inRect.width, 60f),
                    "No starting region is available. Go back and choose "
                    + "a region on the world map.");
                DoBottomButtons(inRect, "Continue", showNext: false);
                return;
            }

            CACreationUI.DrawFlow(new Rect(0f, 60f, inRect.width, 24f), 2);
            float bodyTop = 92f;
            float bodyHeight = inRect.height - bodyTop - 54f;
            const float paneGap = 10f;
            CARegionMapWidget.hoveredSlot = -1;
            layoutMode = inRect.width >= 1600f ? CARegionLayoutMode.Wide
                : inRect.width >= 1180f ? CARegionLayoutMode.Medium
                    : CARegionLayoutMode.Compact;
            if (layoutMode == CARegionLayoutMode.Compact)
            {
                Rect tabs = new Rect(0f, bodyTop, inRect.width, 30f);
                CACreationUI.DrawSegment(tabs,
                    new[] { "Objects", "Map", "Details" }, compactPane,
                    value => compactPane = value);
                Rect body = new Rect(0f, bodyTop + 38f, inRect.width,
                    bodyHeight - 38f);
                if (compactPane == 0) DrawObjectRail(body);
                else if (compactPane == 1)
                    CARegionMapWidget.Draw(body, plan,
                        CARegionalSetupSession.SavePending);
                else DrawInspector(body);
            }
            else
            {
                float railWidth = layoutMode == CARegionLayoutMode.Wide
                    ? Mathf.Clamp(inRect.width * 0.17f, 240f, 280f)
                    : Mathf.Clamp(inRect.width * 0.17f, 190f, 220f);
                float panelWidth = layoutMode == CARegionLayoutMode.Wide
                    ? Mathf.Clamp(inRect.width * 0.29f, 470f, 540f)
                    : Mathf.Clamp(inRect.width * 0.36f, 430f, 460f);
                Rect rail = new Rect(0f, bodyTop, railWidth, bodyHeight);
                Rect panel = new Rect(inRect.width - panelWidth, bodyTop,
                    panelWidth, bodyHeight);
                Rect mapRect = new Rect(rail.xMax + paneGap, bodyTop,
                    Mathf.Max(440f, panel.x - rail.xMax - paneGap * 2f),
                    bodyHeight);
                DrawObjectRail(rail);
                CARegionMapWidget.Draw(mapRect, plan,
                    CARegionalSetupSession.SavePending);
                DrawInspector(panel);
            }
            DoBottomButtons(inRect, "Continue");
        }

        // Continue validates the final regional composition shown here.
        protected override bool CanDoNext()
        {
            return base.CanDoNext()
                && CARegionalSetupSession.PrepareNext(false);
        }

        protected override void DoNext()
        {
            string summary = FinalRepresentation(null);
            CACompatibilityReport compatibility =
                CARegionalContentCompatibility.Evaluate(plan);
            if (!compatibility.IsValid && !Prefs.DevMode)
            {
                Verse.Find.WindowStack.Add(new Dialog_MessageBox(summary
                    + "\n\n" + compatibility.ActionableFailure(),
                    "Keep editing", null, null, null,
                    "Starting region", false, null, null));
                return;
            }
            bool transientTest = Prefs.DevMode && !compatibility.IsValid;
            if (transientTest)
                summary += "\n\nThis composition includes "
                    + Counted(compatibility.Blocking.Count(),
                        "selected geographic attachment")
                    + " whose regional "
                    + "behaviour is not production-ready. Because developer "
                    + "mode is active, Continue will generate a disposable "
                    + "test map from this exact composition instead of "
                    + "refusing at Start. Saving is disabled for that run, "
                    + "and unsupported attachments may be absent or visibly "
                    + "incorrect; the map exists so those results can be "
                    + "tested and reported.";
            Verse.Find.WindowStack.Add(Dialog_MessageBox
                .CreateConfirmation(summary
                    + "\n\nContinue with this region?", delegate
                {
                    plan.operatorAuthored = true;
                    CARegionalPlanUtility.EnsureRelationRows(plan);
                    CARegionalSetupSession.SavePending();
                    if (CARegionalSetupSession.PrepareNext(true,
                            transientTest))
                        base.DoNext();
                }, false, "Starting region"));
        }

        protected override void DoBack()
        {
            CARegionalSetupSession.SavePending();
            base.DoBack();
        }

        // Object navigation: region, factions, then owned settlements.
        private void DrawObjectRail(Rect rail)
        {
            Widgets.DrawMenuSection(rail);
            Rect outRect = rail.ContractedBy(6f);
            Rect actionArea = new Rect(outRect.x, outRect.yMax - 68f,
                outRect.width, 68f);
            Rect navigation = new Rect(outRect.x, outRect.y,
                outRect.width, outRect.height - 76f);
            float contentHeight = 150f
                + plan.factions.Count * 96f
                + plan.settlements.Count * 36f;
            Rect view = new Rect(0f, 0f, navigation.width - 18f,
                Mathf.Max(navigation.height, contentHeight));
            Widgets.BeginScrollView(navigation, ref railScroll, view);
            try
            {
                float y = 0f;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.68f, 0.73f, 0.79f);
                Widgets.Label(new Rect(6f, y, view.width - 12f, 22f),
                    "Region");
                GUI.color = Color.white;
                y += 22f;

                Rect regionCard = new Rect(0f, y, view.width, 50f);
                if (CARegionMapWidget.selectedKind
                    == CARegionSelectionKind.Region)
                    Widgets.DrawHighlightSelected(regionCard);
                else if (Mouse.IsOver(regionCard))
                    Widgets.DrawHighlight(regionCard);
                Widgets.DrawBox(regionCard, 1);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(10f, y + 5f, view.width - 20f, 24f),
                    CARegionalPlanUtility.RegionName(plan));
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.72f, 0.76f, 0.81f);
                Widgets.Label(new Rect(10f, y + 28f, view.width - 20f, 18f),
                    plan.factions.Count + " faction"
                    + (plan.factions.Count == 1 ? "" : "s")
                    + " - " + plan.settlements.Count + " settlement"
                    + (plan.settlements.Count == 1 ? "" : "s"));
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(regionCard))
                {
                    CARegionMapWidget.SelectRegion();
                    if (layoutMode == CARegionLayoutMode.Compact)
                        compactPane = 1;
                }
                y += 60f;

                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.68f, 0.73f, 0.79f);
                Widgets.Label(new Rect(6f, y, view.width - 12f, 22f),
                    "Factions and settlements");
                GUI.color = Color.white;
                y += 22f;

                foreach (CARegionalFactionPlan group in
                    plan.factions.Where(item => item != null)
                        .OrderBy(item => item.key).ToList())
                {
                    Color color = CARegionalWorldOverlay.FactionColor(group.key);
                    int held = plan.settlements.Count(item => item != null
                        && item.factionKey == group.key);
                    string groupSubtitle = held + " settlement"
                        + (held == 1 ? "" : "s") + " - "
                        + CAFactionAxes.Characterize(plan, group);
                    Text.Font = GameFont.Tiny;
                    float subtitleHeight = Text.CalcHeight(groupSubtitle,
                        view.width - 24f);
                    float groupHeight = Mathf.Max(42f,
                        27f + subtitleHeight + 4f);
                    Rect groupCard = new Rect(0f, y, view.width, groupHeight);
                    if (CARegionMapWidget.selectedKind
                            == CARegionSelectionKind.Faction
                        && CARegionMapWidget.selectedFactionKey == group.key)
                        Widgets.DrawHighlightSelected(groupCard);
                    else if (Mouse.IsOver(groupCard))
                        Widgets.DrawHighlight(groupCard);
                    Widgets.DrawBox(groupCard, 1);
                    Widgets.DrawBoxSolid(new Rect(groupCard.x, groupCard.y,
                        4f, groupCard.height), color);
                    Text.Font = GameFont.Small;
                    Widgets.Label(new Rect(12f, y + 3f, view.width - 20f, 22f),
                        CARegionalPlanUtility.FactionName(group));
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.72f, 0.76f, 0.81f);
                    Widgets.Label(new Rect(12f, y + 23f,
                        view.width - 20f, subtitleHeight), groupSubtitle);
                    GUI.color = Color.white;
                    if (Mouse.IsOver(groupCard))
                        CARegionMapWidget.emphasisFactionKey = group.key;
                    if (Widgets.ButtonInvisible(groupCard))
                    {
                        CARegionMapWidget.SelectFaction(group.key);
                        if (layoutMode == CARegionLayoutMode.Compact)
                            compactPane = 1;
                    }
                    y += groupHeight + 4f;

                    foreach (CARegionalSettlementPlan place in plan.settlements.Where(
                            item => item != null
                                && item.factionKey == group.key)
                        .OrderBy(item => item.slot).ToList())
                    {
                        Rect placeRow = new Rect(14f, y,
                            view.width - 14f, 30f);
                        if (CARegionMapWidget.selectedKind
                                == CARegionSelectionKind.Settlement
                            && CARegionMapWidget.selectedSlot == place.slot)
                            Widgets.DrawHighlightSelected(placeRow);
                        else if (Mouse.IsOver(placeRow))
                            Widgets.DrawHighlight(placeRow);
                        Widgets.DrawBoxSolid(new Rect(placeRow.x + 3f,
                            placeRow.center.y - 3f, 6f, 6f), color);
                        Text.Font = GameFont.Tiny;
                        Widgets.Label(new Rect(placeRow.x + 14f,
                            placeRow.y + 5f, placeRow.width - 18f, 20f),
                            CARegionalPlanUtility.SettlementName(plan, place));
                        if (Mouse.IsOver(placeRow))
                            CARegionMapWidget.hoveredSlot = place.slot;
                        if (Widgets.ButtonInvisible(placeRow))
                        {
                            CARegionMapWidget.SelectSettlement(place.slot);
                            if (layoutMode == CARegionLayoutMode.Compact)
                                compactPane = 1;
                        }
                        y += 32f;
                    }
                    y += 6f;
                }

                foreach (CARegionalSettlementPlan place in plan.settlements.Where(item =>
                        item != null && plan.FactionPlan(item.factionKey) == null)
                    .OrderBy(item => item.slot).ToList())
                {
                    Rect placeRow = new Rect(0f, y, view.width, 30f);
                    Widgets.DrawBox(placeRow, 1);
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(10f, y + 5f,
                        view.width - 20f, 20f),
                        CARegionalPlanUtility.SettlementName(plan, place)
                        + " - choose a faction");
                    if (Widgets.ButtonInvisible(placeRow))
                    {
                        CARegionMapWidget.SelectSettlement(place.slot);
                        if (layoutMode == CARegionLayoutMode.Compact)
                            compactPane = 1;
                    }
                    y += 34f;
                }
            }
            finally
            {
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                Widgets.EndScrollView();
            }
            bool room = plan.settlements.Count < MaxAuthoredSettlements;
            if (Widgets.ButtonText(new Rect(actionArea.x, actionArea.y,
                    actionArea.width, 30f), "New faction"))
                NewFaction();
            if (Widgets.ButtonText(new Rect(actionArea.x,
                    actionArea.y + 36f, actionArea.width, 30f),
                    "New settlement", true, true, room) && room)
                AddSettlement();
        }

        // ---- the inspector -------------------------------------------------

        private void DrawInspector(Rect panel)
        {
            Widgets.DrawMenuSection(panel);
            Rect outRect = panel.ContractedBy(8f);
            Rect detailToggle = new Rect(outRect.xMax - 156f, outRect.y,
                156f, 26f);
            CAInformationPresentation.DrawLocalExpansion(detailToggle,
                ref inspectorExpanded);
            outRect.yMin += 34f;
            // The previous frame's measured content height prevents clipping
            // and empty trailing space.
            Rect viewRect = new Rect(0f, 0f, outRect.width - 18f,
                Math.Max(outRect.height, measuredHeight));
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            try
            {
                // A selection change uses a short panel fade.
                float fade = Mathf.Clamp01((Time.realtimeSinceStartup
                    - CARegionMapWidget.selectionChangedAt) / 0.18f);
                GUI.color = new Color(1f, 1f, 1f, 0.25f + 0.75f * fade);
                float y = 0f;
                switch (CARegionMapWidget.selectedKind)
                {
                    case CARegionSelectionKind.Settlement:
                        DrawSettlementPanel(ref y, viewRect.width);
                        break;
                    case CARegionSelectionKind.Faction:
                        DrawFactionPanel(ref y, viewRect.width);
                        break;
                    case CARegionSelectionKind.Feature:
                        DrawFeaturePanel(ref y, viewRect.width);
                        break;
                    case CARegionSelectionKind.Neighbor:
                        DrawNeighborPanel(ref y, viewRect.width);
                        break;
                    default:
                        DrawRegionOverview(ref y, viewRect.width);
                        break;
                }
                measuredHeight = y + 8f;
            }
            finally
            {
                GUI.color = Color.white;
                Widgets.EndScrollView();
            }
        }

        private float measuredHeight = 600f;

        private CARegionalSettlementPlan Selected()
        {
            return CARegionMapWidget.selectedKind
                    != CARegionSelectionKind.Settlement ? null
                : plan.settlements.FirstOrDefault(item => item != null
                    && item.slot == CARegionMapWidget.selectedSlot);
        }

        // ---- the region itself ---------------------------------------------

        private void DrawRegionOverview(ref float y, float width)
        {
            CARegionalProjectionKernel kernel =
                CARegionalProjectionPreview.KernelFor(plan);
            CARegionalCandidateFacts facts =
                CARegionalProjectionPreview.FactsFor(plan);

            Kind(ref y, width, "Region",
                new Color(0.46f, 0.86f, 0.96f));
            Title(ref y, width, CARegionalPlanUtility.RegionName(plan));

            Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row), "Name");
            string oldName = plan.regionName ?? "";
            string newName = Widgets.TextField(new Rect(LabelWidth, y + 1f,
                width - LabelWidth, 26f), oldName);
            if (newName != oldName)
            {
                plan.regionName = newName;
                plan.regionNameAuthored = true;
                CARegionalSetupSession.SavePending();
            }
            y += Row + Gap;

            Body(ref y, width, "This region remains on the world map after "
                + "arrival, with its factions, settlements, and populations.");
            Readout(ref y, width, "Arrival",
                CARegionalPlanUtility.TileWords(plan.startTileId)
                + " - the blue marker on the map");

            Rule(ref y, width);
            Title(ref y, width, "Factions and settlements");
            Readout(ref y, width, "Factions",
                plan.factions.Count == 0 ? "none yet"
                    : plan.factions.Count + " faction"
                        + (plan.factions.Count == 1 ? "" : "s"));
            Readout(ref y, width, "Settlements",
                plan.settlements.Count == 0 ? "none yet"
                    : plan.settlements.Count + " settlement"
                        + (plan.settlements.Count == 1 ? "" : "s"));
            string population = RegionPopulationWords();
            if (population != null)
                Readout(ref y, width, "Population and ideoligions", population);
            if (plan.factions.Count == 0 && plan.settlements.Count == 0)
                Note(ref y, width, "Uninhabited region. Add a faction or "
                    + "settlement to place a population here.");

            float half = (width - Gap) * 0.5f;
            Rect fillRect = new Rect(0f, y, half, 30f);
            if (Widgets.ButtonText(fillRect, "Replace composition..."))
                OpenFillMenu();
            Rect completeRect = new Rect(fillRect.xMax + Gap, y, half, 30f);
            if (Widgets.ButtonText(completeRect, "Complete missing fields"))
                GenerateUnspecified();
            TooltipHandler.TipRegion(fillRect, "Choose a complete regional "
                + "composition template. Applying one replaces the current "
                + "factions, settlements, population sources, and relations; "
                + "geography remains unchanged. Current: " + FillSummary()
                + ".");
            TooltipHandler.TipRegion(completeRect, "Generates fields that are "
                + "not set. Existing choices remain unchanged.");
            y += 38f;

            int nearby = ReallocatableSettlements().Count;
            if (nearby > 0)
                Note(ref y, width, nearby + " nearby world settlement"
                    + (nearby == 1 ? " can" : "s can")
                    + " supply the population for a settlement placed here. "
                    + "Reallocation moves existing world population; it does "
                    + "not create additional population.");

            Rule(ref y, width);
            Title(ref y, width, "Land and access");
            if (kernel != null && kernel.BiomeCellCounts != null)
            {
                string landscapes = string.Join(", ", kernel.BiomeCellCounts
                    .Where(item => item.Key != null && item.Value > 0)
                    .OrderByDescending(item => item.Value).Take(3)
                    .Select(item => item.Key.LabelCap.ToString())
                    .ToArray());
                Readout(ref y, width, "Landscape",
                    landscapes.NullOrEmpty() ? "unknown" : landscapes);
                Readout(ref y, width, "Relief",
                    facts?.ReliefWords ?? "unmeasured");
                Readout(ref y, width, "Coast",
                    kernel.ShorelineCells > 0
                        ? (kernel.LittoralSandCells > 0
                            ? "coast and beaches visible in the diagram sample"
                            : "coast visible in the diagram sample")
                        : "no coast visible in the diagram sample");
            }
            if (facts != null)
            {
                if (facts.Roads.Count > 0)
                    Readout(ref y, width, "Roads", string.Join(", ",
                        facts.Roads.Select(item => item.Road.LabelCap.ToString())
                            .Distinct().ToArray()));
                if (facts.Rivers.Count > 0)
                    Readout(ref y, width, "Rivers", string.Join(", ",
                        facts.Rivers.Select(item => item.River.LabelCap.ToString())
                            .Distinct().ToArray()));
            }

            if (facts != null && facts.Features.Any())
            {
                Title(ref y, width, "Features and old sites");
                foreach (CARegionalCandidateFacts.Feature feature in
                    facts.Features.OrderByDescending(item => item.Historical))
                {
                    if (Entry(ref y, width, feature.Name,
                            FeatureKindWords(feature) + " - "
                            + CARegionalPlanUtility.TileWords(
                                feature.Tile.tileId),
                            feature.Historical
                                ? new Color(0.86f, 0.74f, 0.42f)
                                : new Color(0.72f, 0.86f, 0.78f)))
                        CARegionMapWidget.SelectFeature(feature.Tile.tileId,
                            feature.Def?.defName);
                }
            }

            if (plan.settlements.Count > 0)
            {
                Rule(ref y, width);
                Title(ref y, width, "Regional pattern");
                Readout(ref y, width, "Settlement pattern",
                    CARegionalSettlements.SettlementProfile(plan) + " - "
                    + plan.settlements.Count + " settlement"
                    + (plan.settlements.Count == 1 ? "" : "s"));
                Readout(ref y, width, "Regional relations",
                    CARegionalSettlements.RelationPatternWords(
                        (CARegionalRelationPattern)
                            plan.regionalRelationPattern));
                Readout(ref y, width, "Starting provisions",
                    StartingProvisionSummary());
                Readout(ref y, width, "Frontier",
                    (plan.frontierHoldings?.Count ?? 0) > 0
                        ? plan.frontierHoldings.Count
                            + " holdings between settlements"
                        : plan.settlementRealizationComplete
                            ? "no holdings between settlements"
                            : "not decided yet");
                foreach (CARegionalFactionPlan group in
                    plan.factions.Where(item => item != null)
                        .OrderBy(item => item.key))
                {
                    int held = plan.settlements.Count(item => item != null
                        && item.factionKey == group.key);
                    Readout(ref y, width,
                        CARegionalPlanUtility.FactionName(group),
                        CAFactionAxes.Characterize(plan, group) + " - "
                        + held + " settlement" + (held == 1 ? "" : "s"));
                }
            }

            if (facts != null && facts.Neighbors.Count > 0)
            {
                Rule(ref y, width);
                Title(ref y, width, "Around this region");
                foreach (CARegionalCandidateFacts.Neighbor neighbor in
                    facts.Neighbors.Take(6))
                {
                    string label = neighbor.Object.LabelCap.NullOrEmpty()
                        ? neighbor.Object.def?.LabelCap.ToString() ?? "unnamed"
                        : neighbor.Object.LabelCap;
                    if (Entry(ref y, width, label,
                            (neighbor.Faction?.Name ?? "unaligned") + " - "
                            + neighbor.WorldDistanceTiles.ToString("F0")
                            + " tiles " + Bearing(neighbor),
                            neighbor.Faction?.Color
                                ?? new Color(0.7f, 0.7f, 0.7f)))
                        CARegionMapWidget.SelectNeighbor(
                            neighbor.Tile.tileId);
                }
            }

            Rule(ref y, width);
            Rect technical = new Rect(0f, y, width, 28f);
            if (Widgets.ButtonText(technical, showTechnicalGeography
                    ? "Hide technical map details"
                    : "Technical map details..."))
                showTechnicalGeography = !showTechnicalGeography;
            y += 34f;
            if (showTechnicalGeography)
            {
                Readout(ref y, width, "Regional backing",
                    plan.BackingMapSize.x + " x " + plan.BackingMapSize.z
                    + " cells");
                Readout(ref y, width, "World-tile sources",
                    plan.RegionTileCount + " tiles at " + profile.Size
                    + " local cells each");
                if (kernel != null)
                {
                    Readout(ref y, width, "Diagram shoreline sample",
                        kernel.ShorelineCells.ToString("N0")
                        + " sample cells");
                    CARegionalProjectionFidelity measured =
                        CARegionalProjectionPreview.FidelityFor(plan);
                    Readout(ref y, width, "Preview sampling",
                        Mathf.RoundToInt(kernel.Resolution * 100f) + "%"
                        + (measured != null
                            ? " - " + measured.ScreenPhrase() : ""));
                }
            }
        }

        private string Bearing(CARegionalCandidateFacts.Neighbor neighbor)
        {
            CARegionalProjectionKernel kernel =
                CARegionalProjectionPreview.KernelFor(plan);
            if (kernel == null) return "away";
            float dx = neighbor.Point.x - kernel.Center.x;
            float dz = neighbor.Point.y - kernel.Center.z;
            if (Mathf.Abs(dx) < 1f && Mathf.Abs(dz) < 1f) return "away";
            string vertical = dz > Mathf.Abs(dx) * 0.4f ? "north"
                : -dz > Mathf.Abs(dx) * 0.4f ? "south" : "";
            string horizontal = dx > Mathf.Abs(dz) * 0.4f ? "east"
                : -dx > Mathf.Abs(dz) * 0.4f ? "west" : "";
            string words = vertical + horizontal;
            return words.NullOrEmpty() ? "away" : words;
        }

        // ---- settlement ----------------------------------------------------

        private void DrawSettlementPanel(ref float y, float width)
        {
            CARegionalSettlementPlan place = Selected();
            if (place == null) {
                CARegionMapWidget.SelectRegion();
                DrawRegionOverview(ref y, width);
                return;
            }
            CARegionalFactionPlan owner = plan.FactionPlan(
                place.factionKey);
            FactionDef def = owner?.ResolvedFactionDef;
            CARegionMapWidget.hoveredSlot = place.slot;

            Back(ref y, width, "All of this region");
            // A settlement belongs to a faction. The overline opens that
            // faction without copying its controls onto this screen.
            if (Kind(ref y, width,
                    "Settlement · "
                    + CARegionalPlanUtility.FactionName(owner)
                    + (plan.settlementPattern
                        != (byte)CASettlementPattern.Unsettled
                        ? " · " + CARegionalSettlements.RoleWords(
                            (CASettlementRole)place.realizedRole)
                        : ""),
                    CARegionalWorldOverlay.FactionColor(
                        place.factionKey)) && owner != null)
                CARegionMapWidget.SelectFaction(place.factionKey);
            Title(ref y, width,
                CARegionalPlanUtility.SettlementName(plan, place));
            if (plan.settlementRealizationComplete)
            {
                Readout(ref y, width, "Realized scale",
                    CARegionalSettlements.ScaleWords(
                        (CASettlementScale)place.realizedScale)
                    + " · " + place.residentPopulation + " residents");
                Readout(ref y, width, "Development",
                    "land " + place.landCapacity + "/3 · access "
                    + place.realizedAccessInfrastructure + "/3 · services "
                    + place.realizedServiceInfrastructure + "/3 · civic "
                    + place.realizedCivicInfrastructure + "/3 · economy "
                    + place.economicCapacity + "/3 · trade "
                    + place.tradeConnectivity + "/3 · history "
                    + place.historicalDevelopment + "/3");
            }

            Rule(ref y, width);
            Title(ref y, width, "Cultural expression");
            CACulturalExpression expression =
                CACulturalExpressionModel.ForSettlement(plan, place);
            Note(ref y, width, expression.Presented(inspectorExpanded));

            Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row), "Name");
            place.customName = Widgets.TextField(
                new Rect(LabelWidth, y + 1f, width - LabelWidth - 152f, 26f),
                place.customName ?? "");
            if (Widgets.ButtonText(new Rect(width - 148f, y, 72f, 28f),
                    "Reroll"))
            {
                place.customName = RollSettlementName(place);
                CARegionalSetupSession.SavePending();
            }
            if (Widgets.ButtonText(new Rect(width - 72f, y, 72f, 28f),
                    "Clear"))
            {
                place.customName = null;
                CARegionalSetupSession.SavePending();
            }
            y += Row + Gap;

            // Ownership links to the faction screen.
            Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row),
                "Faction");
            Rect ownerRect = new Rect(LabelWidth, y,
                width - LabelWidth - 84f, 28f);
            if (Widgets.ButtonText(ownerRect,
                    CARegionalPlanUtility.FactionName(owner)))
                CARegionMapWidget.SelectFaction(place.factionKey);
            TooltipHandler.TipRegion(ownerRect, "Owning faction. Open it to "
                + "edit its identity, knowledge, relations, political beliefs, "
                + "and structure.");
            if (Widgets.ButtonText(new Rect(width - 80f, y, 80f, 28f),
                    "Change")) OpenOwnerMenu(place);
            y += Row + Gap;

            Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row), "Location");
            Rect groundRect = new Rect(LabelWidth, y, width - LabelWidth,
                28f);
            bool placing = CARegionMapWidget.awaitingSlot == place.slot;
            if (Widgets.ButtonText(groundRect, placing
                    ? "Choose an area on the diagram..."
                    : CARegionalPlanUtility.TileWords(place.memberTileId)
                        + " - change area"))
                CARegionMapWidget.awaitingSlot = placing ? -1 : place.slot;
            TooltipHandler.TipRegion(groundRect, "Choose the broad world "
                + "area this settlement belongs to. Its exact position is "
                + "chosen against the real generated terrain when the map "
                + "exists; this diagram does not promise a coordinate.");
            y += Row + Gap;

            Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row), "Site layout");
            Rect formRect = new Rect(LabelWidth, y, width - LabelWidth, 28f);
            if (Widgets.ButtonText(formRect, PhysicalSiteSummary(place)))
                OpenPhysicalSiteMenu(place);
            TooltipHandler.TipRegion(formRect, "An independent site is its "
                + "own settlement. Settlements sharing one physical complex "
                + "generate as neighboring districts; ownership and "
                + "government stay separate.");
            y += Row + Gap;

            Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row),
                "Population source");
            Rect originRect = new Rect(LabelWidth, y, width - LabelWidth,
                28f);
            if (place.populationOrigin == CASettlementOrigin.Unset)
                GUI.color = new Color(1f, 0.72f, 0.5f);
            if (Widgets.ButtonText(originRect, OriginSummary(place)))
                OpenOriginMenu(place);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(originRect, "Vanilla world population "
                + "decides how many major settlements exist. A settlement "
                + "here either reallocates one of them - concentrating the "
                + "world's population into this region - or is an explicit "
                + "addition for this scenario. When an existing settlement is "
                + "used, that world settlement is absorbed when the map begins "
                + "so the population is moved rather than duplicated.");
            y += Row + Gap;

            Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row),
                "Starting facilities");
            Rect facilitiesRect = new Rect(LabelWidth, y,
                width - LabelWidth, 28f);
            if (Widgets.ButtonText(facilitiesRect,
                    StartingFacilitiesSummary(place)))
                OpenStartingFacilitiesMenu(place);
            TooltipHandler.TipRegion(facilitiesRect,
                "Rooms and facilities present when play begins. Defaults use "
                + "the population, settlement role, infrastructure, faction "
                + "structure, and faction knowledge. Facility choices change "
                + "the generated starting provisions.");
            y += Row + Gap;

            Readout(ref y, width, "Development profile",
                CASettlementStartingState.ProfileWords(
                    place.developmentProfile) + " - "
                + CASettlementStartingState.ProfileEffect(
                    place.developmentProfile));

            InfrastructureRow(ref y, width, place, "Transport access",
                place.accessInfrastructure,
                CASettlementStartingState.Access(plan, place), 0,
                "Road and coastal access. Affects supply reach, wealth, and "
                + "which starting facilities can operate.");
            InfrastructureRow(ref y, width, place, "Local services",
                place.serviceInfrastructure,
                CASettlementStartingState.Services(plan, place), 1,
                "Local support for kitchens, stores, workshops, and other "
                + "starting facilities.");
            InfrastructureRow(ref y, width, place, "Public works",
                place.civicInfrastructure,
                CASettlementStartingState.Civic(plan, place), 2,
                "Durable settlement works. Affects wealth, provisions, and "
                + "starting facilities independently of faction knowledge.");

            // Each row is a share of this settlement's population.
            Rule(ref y, width);
            Title(ref y, width, "Population");
            if (place.populationGroups == null || place.populationGroups.Count == 0)
            {
                Note(ref y, width, "No population set. Add a population or "
                    + "generate one from the faction and regional settings.");
                Rect generatePopulation = new Rect(0f, y, width, 26f);
                if (Widgets.ButtonText(generatePopulation,
                        "Generate population"))
                {
                    CASettlementComposition.EnsureDerived(plan, place);
                    CARegionalSetupSession.SavePending();
                }
                y += Row + Gap;
            }
            foreach (CASettlementPopulationGroup populationGroup in place.populationGroups.ToList())
            {
                if (populationGroup == null) continue;
                Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row),
                    populationGroup.share + "%");
                Rect populationGroupRect = new Rect(LabelWidth, y,
                    width - LabelWidth, 26f);
                if (populationGroup.authored)
                    GUI.color = new Color(1f, 0.92f, 0.70f);
                if (Widgets.ButtonText(populationGroupRect, populationGroup.label + " · "
                        + PopulationGroupKindWords(populationGroup) + " · "
                        + populationGroup.CertaintyWords
                        + (populationGroup.quarter ? " · quartered" : "")))
                    OpenPopulationGroupMenu(place, populationGroup);
                GUI.color = Color.white;
                TooltipHandler.TipRegion(populationGroupRect, PopulationGroupTooltip(place,
                    populationGroup));
                y += Row + 2f;
            }
            bool unrepresentedFaction = plan.factions.Any(faction => faction != null
                && faction.key != place.factionKey
                && !place.populationGroups.Any(group => group != null
                    && group.factionKey == faction.key));
            bool canAddUnaffiliated = !place.populationGroups.Any(group =>
                group != null && group.kind == CAPopulationGroupKind.Unaffiliated);
            if (MinorityCapacity(place) >= 5
                && (unrepresentedFaction || canAddUnaffiliated))
            {
                Rect addPopulationGroup = new Rect(0f, y, width, 26f);
                if (Widgets.ButtonText(addPopulationGroup, "Add minority population"))
                    OpenAddPopulationGroupMenu(place);
                y += Row + 2f;
            }
            y += Gap;

            // Starting provisions are regenerated from settlement parameters.
            // Each generated provision may keep its own distribution override.
            Rule(ref y, width);
            Title(ref y, width, "Starting provisions");
            Note(ref y, width, "Generated from population, faction structure, "
                + "starting facilities, infrastructure, and settlement role. "
                 + "Gold marks a distribution override. Other fields update when their "
                + "inputs change.");
            foreach (CAStartingProvision arrangement in
                place.startingProvisions.Where(item => item != null
                    && item.active).ToList())
            {
                if (arrangement == null) continue;
                Rect provRect = new Rect(0f, y, width, 26f);
                if (arrangement.distributionAuthored)
                    GUI.color = new Color(1f, 0.92f, 0.70f);
                string basis = arrangement.basisLabel.NullOrEmpty()
                    ? "Provision" : arrangement.basisLabel;
                string savedPreference = arrangement.distributionAuthored
                        && arrangement.PreferredDistribution
                            != arrangement.distribution
                    ? " · saved " + arrangement.PreferredDistribution
                        .ToString().ToLower() + " preference"
                    : "";
                if (Widgets.ButtonText(provRect,
                        basis + ": " + arrangement.Summary + savedPreference))
                    OpenArrangementMenu(place, arrangement);
                GUI.color = Color.white;
                TooltipHandler.TipRegion(provRect,
                    arrangement.operatorKind == CAProvisionOperator.Household
                        ? "This resolves as household provision. It does not "
                            + "create a separate operator organization or "
                            + "separate kitchen. "
                            + (arrangement.authoredDistribution >= 0
                                ? "The saved distribution preference returns "
                                    + "if facilities support it."
                                : "Its generated form is recalculated if "
                                    + "facilities change.")
                        : "When play begins this is a real operator organization "
                            + "with its own kitchen"
                            + (arrangement.distribution
                                == CAProvisionDistribution.Neighborhood
                                ? "s, one per neighborhood node" : "")
                            + ", stocked stores and a dining table, owned by "
                            + "the settlement. Tax-funded provision sets "
                            + "a tax rate that collects from its treasury. "
                            + "Water is checked against the actual map.");
                y += Row + 2f;
            }
            foreach (CAStartingProvision arrangement in
                place.startingProvisions.Where(item => item != null
                    && !item.active && item.distributionAuthored).ToList())
            {
                Rect inactiveRect = new Rect(0f, y, width, 26f);
                GUI.color = new Color(1f, 0.92f, 0.70f);
                string basis = arrangement.basisLabel.NullOrEmpty()
                    ? "Provision" : arrangement.basisLabel;
                bool savedDistribution = arrangement.distributionAuthored;
                if (Widgets.ButtonText(inactiveRect, "Saved inactive · "
                        + basis + (savedDistribution
                            ? " · " + arrangement.PreferredDistribution
                                .ToString().ToLower() + " distribution"
                            : " · ")))
                    OpenArrangementMenu(place, arrangement);
                GUI.color = Color.white;
                TooltipHandler.TipRegion(inactiveRect,
                    "This basis is not generated because "
                    + (arrangement.inactiveReason ?? "its cause is absent")
                    + ". Its distribution preference is saved"
                    + " and will return if the basis becomes available again.");
                y += Row + 2f;
            }
            y += Gap;

            Rule(ref y, width);
            Title(ref y, width, "Starting conditions");
            Body(ref y, width, MaterialState(place, owner, def));

            Rule(ref y, width);
            Title(ref y, width, "Local material capability");
            Body(ref y, width, MaterialProspect(place, owner, def));

            Rule(ref y, width);
            Title(ref y, width, "Knowledge access and research");
            Body(ref y, width, ResearchAccess(place, owner, def));

            Rule(ref y, width);
            Title(ref y, width, "Relations");
            if (owner == null)
                Note(ref y, width, "Choose an owner and its relationships "
                    + "become this settlement's relationships.");
            else
            {
                Readout(ref y, width, "Player faction",
                    PlayerRelationLabel(owner));
                foreach (CARegionalFactionPlan other in
                    plan.factions.Where(item => item != null
                        && item.key != owner.key).OrderBy(item => item.key))
                    Readout(ref y, width,
                        CARegionalPlanUtility.FactionName(other),
                        PairRelationLabel(owner, other,
                            plan.RelationPlanBetween(owner.key, other.key)));
                Note(ref y, width, "Relations belong to factions, not "
                    + "individual settlements.");
            }

            Rule(ref y, width);
            if (Widgets.ButtonText(new Rect(0f, y, width, 30f),
                    "Remove settlement"))
            {
                plan.settlements.Remove(place);
                RemoveUnusedFactions();
                CARegionMapWidget.SelectRegion();
                CARegionalSetupSession.SavePending();
            }
            y += 36f;
        }

        // Generated starting state from settlement fields and faction knowledge.
        private string MaterialState(CARegionalSettlementPlan place,
            CARegionalFactionPlan owner, FactionDef def)
        {
            if (def == null)
                return "Choose an owning faction to determine available "
                    + "building methods, facilities, and knowledge.";
            CAMorphForm form = CASettlementAxes.Form(place.authoredForm,
                CASettlementAxes.TemplateEraPrior(def));
            int districts = plan.settlements.Count(item => item != null
                && item.memberTileId == place.memberTileId
                && item.PhysicalClusterKey == place.PhysicalClusterKey);
            int facilities = CASettlementStartingState.ResolveFacilityMask(plan,
                place, def);
            string startingFacilities = FacilityNames(facilities).ToLower();
            bool generated = place.startingFacilityAuthoredMask == 0;
            return "Built in " + form.ToString().ToLower() + " form, which "
                + "follows from what "
                + CARegionalPlanUtility.FactionName(owner) + " knows. "
                + (districts > 1
                    ? "One of " + districts + " districts sharing a single "
                        + "physical complex. "
                    : "Standing on its own. ")
                + "On " + CARegionalPlanUtility.TileWords(place.memberTileId)
                    .ToLower() + ". Begins with " + startingFacilities + " ("
                    + (generated ? "generated from its parameters"
                        : "with per-facility choices applied")
                     + "). Transport access is "
                    + CASettlementStartingState.InfrastructureWords(
                        CASettlementStartingState.Access(plan, place))
                    + ", local services are "
                    + CASettlementStartingState.InfrastructureWords(
                        CASettlementStartingState.Services(plan, place))
                    + ", and public works are "
                    + CASettlementStartingState.InfrastructureWords(
                        CASettlementStartingState.Civic(plan, place)) + ".";
        }

        // Faction knowledge is shared; local facilities and access determine
        // what this settlement can practice.
        private string ResearchAccess(CARegionalSettlementPlan place,
            CARegionalFactionPlan owner, FactionDef def)
        {
            if (def == null)
                return "Choose an owning faction to determine available "
                    + "knowledge.";
            // Era limits available facilities; it does not grant knowledge.
            TechLevel knowledge = CASettlementAxes.TemplateEraPrior(def);
            int mask = CASettlementStartingState.ResolveFacilityMask(plan, place,
                def);
            bool laboratory = (mask
                & CASettlementAxes.FacilityLaboratory) != 0;
            bool workshop = (mask
                & CASettlementAxes.FacilityWorkshop) != 0;
            bool road = CARegionalPlanUtility.ConstituentHasRoad(
                place.memberTileId);
            bool coastal = CARegionalPlanUtility.ConstituentIsCoastal(
                place.memberTileId);
            int access = CASettlementStartingState.Access(plan, place);
            int services = CASettlementStartingState.Services(plan, place);
            int civic = CASettlementStartingState.Civic(plan, place);
            int ceiling = CASettlementAxes.LocalPracticeCeiling(mask, access,
                services, civic, knowledge);
            int basis = CASettlementAxes.CapabilityBasis(knowledge);
            var carriers = new List<string>();
            if (laboratory) carriers.Add("a laboratory stands here");
            if (workshop) carriers.Add("it has a workshop");
            if (road) carriers.Add("a road ties it to the rest of the "
                + "faction");
            if (coastal) carriers.Add("it has a shore, so it can be reached "
                + "by water");
            carriers.Add("access capacity is "
                + CASettlementStartingState.InfrastructureWords(access));
            carriers.Add("service capacity is "
                + CASettlementStartingState.InfrastructureWords(services));
            carriers.Add("civic capacity is "
                + CASettlementStartingState.InfrastructureWords(civic));
            string reach = carriers.Count == 0
                ? "Nothing here carries that knowledge in or works on it: "
                    + "no laboratory, no workshop, no road, no shore."
                : "Local work depends on " + carriers.ToCommaList(true)
                    + ".";
            string gap = ceiling < basis
                ? " This settlement cannot practice everything its faction "
                    + "knows; "
                    + "the missing means are local, not a loss of knowledge."
                : " Local capacity supports all current faction knowledge.";
            string compact = "Local research capability: "
                + CASettlementWealth.TierWords(ceiling)
                + ". Faction baseline: " + knowledge.ToString().ToLower()
                + ".";
            string standard = compact + " " + reach;
            string expanded = "Knowledge belongs to "
                + CARegionalPlanUtility.FactionName(owner)
                + ", not to this settlement. The faction baseline is "
                + knowledge.ToString().ToLower() + ". " + reach + gap;
            return CAInformationPresentation.Select(compact, standard,
                expanded, inspectorExpanded);
        }

        // ---- one faction ---------------------------------------------------

        private void DrawFactionPanel(ref float y, float width)
        {
            CARegionalFactionPlan group = plan.FactionPlan(
                CARegionMapWidget.selectedFactionKey);
            if (group == null) {
                CARegionMapWidget.SelectRegion();
                DrawRegionOverview(ref y, width);
                return;
            }
            Back(ref y, width, "All of this region");
            Kind(ref y, width, "Faction",
                CARegionalWorldOverlay.FactionColor(group.key));
            Title(ref y, width, CARegionalPlanUtility.FactionName(group));
            Note(ref y, width, CAFactionAxes.Characterize(plan, group)
                + " · " + CARegionalSettlements.PresenceWords(plan, group));

            Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row), "Origin");
            Rect sourceRect = new Rect(LabelWidth, y, width - LabelWidth,
                28f);
            if (Widgets.ButtonText(sourceRect, group.source
                    == CARegionalFactionSource.ExistingWorldFaction
                    ? "Existing world faction"
                    : "New faction"))
                OpenSourceMenu(group);
            y += Row + Gap;

            Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row),
                group.source == CARegionalFactionSource.ExistingWorldFaction
                    ? "Existing faction" : "Faction type");
            Rect factionRect = new Rect(LabelWidth, y, width - LabelWidth,
                28f);
            if (Widgets.ButtonText(factionRect, group.Summary))
                OpenFactionMenu(group);
            TooltipHandler.TipRegion(factionRect, group.source
                == CARegionalFactionSource.ExistingWorldFaction
                ? "Existing knowledge, ideoligion, and settlement rules "
                    + "remain in force."
                : "The faction type supplies world integration, knowledge, "
                    + "and settlement rules. Cultural background, Ideoligion, political "
                    + "beliefs, and faction structure "
                    + "remain separate.");
            y += Row + Gap;

            if (group.source == CARegionalFactionSource.NewWorldFaction)
            {
                Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row), "Name");
                group.customName = Widgets.TextField(
                    new Rect(LabelWidth, y + 1f,
                        width - LabelWidth - 80f, 26f),
                    group.customName ?? "");
                if (Widgets.ButtonText(new Rect(width - 76f, y, 76f, 28f),
                        "Reroll"))
                {
                    group.customName = RollFactionName(group);
                    CARegionalSetupSession.SavePending();
                }
                y += Row + Gap;
                bool visible = group.visibleInWorld;
                Widgets.CheckboxLabeled(new Rect(0f, y, width, 28f),
                    "Appears on the world map", ref visible);
                group.visibleInWorld = visible;
                y += Row + Gap;
            }

            Rule(ref y, width);
            Title(ref y, width, "Population");
            Readout(ref y, width, "Carried background",
                group.culture?.name ?? "Generated background");
            Rect cultureEdit = new Rect(0f, y, width, 28f);
            if (Widgets.ButtonText(cultureEdit,
                    "Edit background: "
                    + CACultureModel.Summary(group.culture)))
                Verse.Find.WindowStack.Add(new Dialog_CACultureEditor(
                    group.culture, group.Summary,
                    CARegionalSetupSession.SavePending));
            y += Row + Gap;
            Ideo livingIdeoligion = group.LivingIdeo;
            Readout(ref y, width, "Ideoligion", livingIdeoligion != null
                ? livingIdeoligion.name
                : ModsConfig.IdeologyActive
                    ? "Generated when the world starts"
                    : "Inactive");

            Rule(ref y, width);
            Title(ref y, width, "Beliefs and current order");
            List<string> institutionalTensions =
                CAFactionStructureModel.Tensions(group.politicalBeliefs,
                    group.factionStructure);
            Note(ref y, width, CAPoliticalBeliefsModel.Summary(
                group.politicalBeliefs) + ". "
                + (institutionalTensions.Count == 0
                    ? "Current institutions align with every set belief."
                    : institutionalTensions.Count
                        + " preferred positions differ from current practice."));
            Rect beliefsEdit = new Rect(0f, y, width, 28f);
            if (Widgets.ButtonText(beliefsEdit,
                    "Compare and edit beliefs and practice..."))
                Verse.Find.WindowStack.Add(Dialog_CAAxisEditor.ForStructure(
                    group.factionStructure, group.politicalBeliefs,
                    (plan.candidateId ?? "ca") + ":faction:" + group.key,
                    () => FactionSettingsChanged(group)));
            y += Row + Gap;

            // Authority between settlements is independent of political
            // beliefs and the spatial settlement pattern.
            Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row),
                "Settlement authority");
            Rect authorityRect = new Rect(LabelWidth, y,
                width - LabelWidth, 28f);
            CASettlementAuthority authority =
                CARegionalSettlements.SettlementAuthorityOf(plan, group);
            if (group.settlementAuthorityExplicit)
                GUI.color = new Color(1f, 0.92f, 0.70f);
            if (Widgets.ButtonText(authorityRect,
                    CARegionalSettlements.SettlementAuthorityWords(authority)
                        .CapitalizeFirst() + " · "
                    + CARegionalSettlements.PresenceWords(plan, group)))
                OpenSettlementAuthorityMenu(group);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(authorityRect, "Faction-wide "
                + "responsibility for decisions, taxation, justice, and "
                + "defense. This changes settlement-member relations; "
                + "faction structure and geography remain separate."
                + "\n\nCurrent setting: "
                + CARegionalSettlements.Characterize(plan, group));
            y += Row + Gap;

            Rule(ref y, width);
            Title(ref y, width, "Faction technology");
            Body(ref y, width, CAInformationPresentation.Select(
                group.TechnologySummary + " technological baseline.",
                "The faction shares a " + group.TechnologySummary.ToLower()
                    + " technological baseline: the designs and methods its "
                    + "population can carry between settlements.",
                "This baseline does not imply that every settlement can build "
                    + "or research at that level. Local facilities, services, "
                    + "and access are shown only on each settlement.",
                inspectorExpanded));

            Rule(ref y, width);
            Title(ref y, width, "Relations");
            Rect playerRect = new Rect(0f, y, width, 28f);
            if (Widgets.ButtonText(playerRect,
                    "Player faction: " + PlayerRelationLabel(group)))
                OpenPlayerRelationMenu(group);
            TooltipHandler.TipRegion(playerRect,
                PlayerRelationTooltip(group));
            y += Row + Gap;
            string federationKind = group.federationKey < 0 ? null
                : plan.factions.Where(item => item != null
                        && item.federationKey == group.federationKey)
                    .Select(item => item.federationKind)
                    .FirstOrDefault(kind => !kind.NullOrEmpty()) ?? "defense";
            Rect federationRect = new Rect(0f, y, width, 28f);
            if (Widgets.ButtonText(federationRect, "Federation: "
                    + (federationKind.NullOrEmpty() ? "None"
                        : CASettlementAuthorityWriter
                            .FederationKindName(federationKind)
                            .CapitalizeFirst())))
                OpenFederationMenu(group);
            TooltipHandler.TipRegion(federationRect,
                "Links independent factions and selects the responsibilities "
                + "they share.");
            y += Row + Gap;
            foreach (CARegionalFactionPlan other in plan.factions
                .Where(item => item != null && item.key != group.key)
                .OrderBy(item => item.key).ToList())
            {
                CARegionalRelationPlan pair = plan.RelationPlanBetween(
                    group.key, other.key);
                Rect pairRect = new Rect(0f, y, width, 28f);
                string label = CARegionalPlanUtility.FactionName(other) + ": "
                    + PairRelationLabel(group, other, pair);
                if (FactionsShareWorldFaction(group, other))
                    Widgets.Label(pairRect, label);
                else if (Widgets.ButtonText(pairRect, label))
                    OpenPairRelationMenu(group, other, pair);
                TooltipHandler.TipRegion(pairRect,
                    PairRelationTooltip(group, other, pair));
                y += Row + Gap;
            }

            Rule(ref y, width);
            Title(ref y, width, "Settlements");
            List<CARegionalSettlementPlan> owned = plan.settlements.Where(item =>
                item != null && item.factionKey == group.key)
                .OrderBy(item => item.slot).ToList();
            IReadOnlyList<CACulturalExpression> expressions =
                CACulturalExpressionModel.ForFaction(plan, group);
            if (owned.Count == 0)
                Note(ref y, width, group.authored
                    ? "No settlements. This faction remains in the world."
                    : "No settlements. This generated faction is removed if "
                        + "it remains unconfirmed.");
            for (int index = 0; index < owned.Count; index++)
            {
                CARegionalSettlementPlan place = owned[index];
                CACulturalExpression expression = index < expressions.Count
                    ? expressions[index] : null;
                if (Entry(ref y, width,
                        CARegionalPlanUtility.SettlementName(plan, place),
                        "Settlement · "
                        + CARegionalPlanUtility.TileWords(
                            place.memberTileId)
                        + (expression == null ? "" : "\n"
                            + CACulturalExpression.StatusWords(
                                expression.Status) + ": "
                            + expression.Summary),
                        CARegionalWorldOverlay.FactionColor(group.key)))
                    CARegionMapWidget.SelectSettlement(place.slot);
            }
            bool room = plan.settlements.Count < MaxAuthoredSettlements;
            Rect foundRect = new Rect(0f, y, width, 28f);
            if (Widgets.ButtonText(foundRect,
                    "Add settlement", true, true, room)
                && room)
                AddSettlementFor(group.key);
            TooltipHandler.TipRegion(foundRect, room
                ? "Adds a settlement owned by this faction."
                : "This region already holds the maximum of "
                    + MaxAuthoredSettlements + " settlements.");
            y += 34f;

            Rule(ref y, width);
            bool empty = owned.Count == 0;
            Rect removeRect = new Rect(0f, y, width, 28f);
            if (Widgets.ButtonText(removeRect, "Remove faction", true,
                    true, empty) && empty)
            {
                plan.factions.Remove(group);
                plan.relations.RemoveAll(pair => pair != null
                    && (pair.leftFactionKey == group.key
                        || pair.rightFactionKey == group.key));
                CARegionMapWidget.SelectRegion();
                CARegionalSetupSession.SavePending();
                return;
            }
            TooltipHandler.TipRegion(removeRect, empty
                ? "Removes this faction from the region."
                : "Reassign or remove its settlements first.");
            y += 34f;
        }

        // ---- composition menus ---------------------------------------------

        private static string PopulationGroupKindWords(CASettlementPopulationGroup populationGroup)
        {
            switch (populationGroup.kind)
            {
                case CAPopulationGroupKind.OtherFaction:
                    return "other-faction residents";
                case CAPopulationGroupKind.LocalResidents:
                    return "local residents";
                case CAPopulationGroupKind.Unaffiliated: return "unaffiliated";
                default: return "main population";
            }
        }

        private string PopulationGroupTooltip(CARegionalSettlementPlan place,
            CASettlementPopulationGroup populationGroup)
        {
            CARegionalFactionPlan affiliation = plan.FactionPlan(
                populationGroup.factionKey);
            CARegionalFactionPlan politicalSource = plan.FactionPlan(
                populationGroup.politicalBeliefsFactionKey >= 0
                    ? populationGroup.politicalBeliefsFactionKey
                    : populationGroup.factionKey);
            CARegionalFactionPlan ideoligionSource = plan.FactionPlan(
                populationGroup.ideoligionFactionKey >= 0
                    ? populationGroup.ideoligionFactionKey : populationGroup.factionKey);
            string affiliationWords = affiliation == null ? "None"
                : CARegionalPlanUtility.FactionName(affiliation);
            string beliefsWords = politicalSource != null
                ? CAPoliticalBeliefsModel.Summary(
                    politicalSource.politicalBeliefs)
                : !populationGroup.politicalBeliefsId.NullOrEmpty()
                    ? populationGroup.politicalBeliefsId : "None set";
            string ideoligionWords;
            if (populationGroup.independentIdeoligionKey >= 0)
                ideoligionWords = "Independent Ideoligion";
            else if (ideoligionSource?.LivingIdeo != null)
                ideoligionWords = ideoligionSource.LivingIdeo.name;
            else if (ideoligionSource != null && ModsConfig.IdeologyActive)
                ideoligionWords = "Generated from "
                    + CARegionalPlanUtility.FactionName(ideoligionSource);
            else
                ideoligionWords = ModsConfig.IdeologyActive
                    ? "Individual beliefs" : "Inactive";

            return "Faction affiliation: " + affiliationWords
                + "\nIdeoligion: " + ideoligionWords
                + "\nPolitical beliefs: " + beliefsWords
                + "\nIdeoligion certainty: " + populationGroup.CertaintyWords
                + "\n\nAffiliation is membership. Each belief source can "
                + "follow affiliation or remain independent. Certainty "
                + "affects conversion resistance.";
        }

        private static int MinorityCapacity(CARegionalSettlementPlan place)
        {
            int others = place?.populationGroups?.Where(item => item != null
                && item.kind != CAPopulationGroupKind.Main)
                .Sum(item => Math.Max(0, item.share)) ?? 0;
            return CACreationFlowContracts.MaximumMinorityShare(others);
        }

        private void NormalizePopulationShares(CARegionalSettlementPlan place,
            CASettlementPopulationGroup changed = null)
        {
            if (place?.populationGroups == null) return;
            if (changed != null
                && changed.kind != CAPopulationGroupKind.Main)
            {
                int otherShares = place.populationGroups.Where(item =>
                        item != null && item != changed
                        && item.kind != CAPopulationGroupKind.Main)
                    .Sum(item => Math.Max(0, item.share));
                changed.share = CACreationFlowContracts.ClampMinorityShare(
                    changed.share, otherShares);
            }

            int used = 0;
            foreach (CASettlementPopulationGroup minority in
                place.populationGroups.Where(item => item != null
                    && item.kind != CAPopulationGroupKind.Main))
            {
                minority.share = CACreationFlowContracts.ClampMinorityShare(
                    minority.share, used);
                used += minority.share;
            }
            CASettlementPopulationGroup dominant = place.populationGroups.FirstOrDefault(
                item => item != null
                && item.kind == CAPopulationGroupKind.Main);
            if (dominant != null)
                dominant.share = CACreationFlowContracts.MainPopulationShare(used);
        }

        private void OpenPopulationGroupMenu(CARegionalSettlementPlan place,
            CASettlementPopulationGroup populationGroup)
        {
            Verse.Find.WindowStack.Add(new Dialog_CAPopulationGroupEditor(
                plan, place, populationGroup, delegate
                {
                    NormalizePopulationShares(place, populationGroup);
                    CARegionalSetupSession.SavePending();
                }, populationGroup.kind == CAPopulationGroupKind.Main
                    ? (Action)null : delegate
                    {
                        place.populationGroups.Remove(populationGroup);
                        place.startingProvisions.RemoveAll(item => item != null
                            && item.populationGroupKey == populationGroup.key);
                        NormalizePopulationShares(place);
                        CARegionalSetupSession.SavePending();
                    }));
        }

        private void OpenAddPopulationGroupMenu(CARegionalSettlementPlan place)
        {
            var options = new List<CACreationChoice>();
            int startingShare = Math.Min(15, MinorityCapacity(place));
            foreach (CARegionalFactionPlan other in plan.factions
                .Where(item => item != null
                    && item.key != place.factionKey
                    && !place.populationGroups.Any(group => group != null
                        && group.factionKey == item.key))
                .OrderBy(item => item.key))
            {
                CARegionalFactionPlan local = other;
                options.Add(new CACreationChoice
                {
                    Key = local.key.ToString(),
                    Name = "Residents from "
                        + CARegionalPlanUtility.FactionName(local),
                    Summary = "Add a minority affiliated with this faction.",
                    Traits = local.TechnologySummary + " · " + startingShare
                        + "% starting share",
                    Details = "Affiliation is the initial source for "
                        + "Ideoligion and political beliefs; both can be "
                        + "changed independently after the group is added.",
                    Badge = "Population group",
                    Icon = local.ResolvedFactionDef?.FactionIcon,
                    Accent = CARegionalWorldOverlay.FactionColor(local.key),
                    ConfirmLabel = "Add this population group",
                    Choose = delegate
                    {
                        AddPopulationGroup(place,
                            CAPopulationGroupKind.OtherFaction,
                            CARegionalPlanUtility.FactionName(local), local.key);
                    }
                });
            }
            if (!place.populationGroups.Any(group => group != null
                    && group.kind == CAPopulationGroupKind.Unaffiliated))
                options.Add(new CACreationChoice
                {
                Key = "unaffiliated",
                Name = "Unaffiliated residents",
                Summary = "Add residents who belong to no faction.",
                Traits = startingShare
                    + "% starting share · no faction membership",
                Details = "Ideoligion and political beliefs remain separate "
                    + "choices after the group is added.",
                Badge = "Population group",
                Icon = CACreationUI.Icon("Rimshare/WorldMapIcons/anarchy"),
                Accent = CACreationUI.Unset,
                ConfirmLabel = "Add unaffiliated residents",
                Choose = delegate
                {
                    AddPopulationGroup(place,
                        CAPopulationGroupKind.Unaffiliated,
                        "Unaffiliated", -1);
                }
                });
            CACreationUI.OpenChoices("Add population group",
                "Add a distinct population to this settlement. Its share, "
                + "distribution, affiliation, Ideoligion, and political "
                + "beliefs remain independently editable.", options);
        }

        private void AddPopulationGroup(CARegionalSettlementPlan place,
            CAPopulationGroupKind kind, string label, int factionKey)
        {
            if (place.populationGroups.Count == 0)
                place.populationGroups.Add(new CASettlementPopulationGroup
                {
                    key = 1,
                    kind = CAPopulationGroupKind.Main,
                    label = CARegionalPlanUtility.FactionName(
                        plan.FactionPlan(place.factionKey)),
                    share = 100,
                    factionKey = place.factionKey,
                    ideoligionCertainty = 1
                });
            int key = place.populationGroups.Max(item => item?.key ?? 0) + 1;
            int share = Math.Min(15, MinorityCapacity(place));
            if (share < 5) return;
            var added = new CASettlementPopulationGroup
            {
                key = key,
                kind = kind,
                label = label,
                share = share,
                factionKey = factionKey,
                ideoligionCertainty = 1,
                authored = true
            };
            place.populationGroups.Insert(
                Math.Max(0, place.populationGroups.Count - 1),
                added);
            NormalizePopulationShares(place, added);
            CARegionalSetupSession.SavePending();
        }

        private void OpenArrangementMenu(CARegionalSettlementPlan place,
            CAStartingProvision arrangement)
        {
            var options = new List<CACreationChoice>();
            options.Add(new CACreationChoice
            {
                Key = "generated",
                Name = "Use generated distribution",
                Summary = "Derive distribution from the operator, settlement, "
                    + "services, and realized scale.",
                Traits = "Generated at the owning surface",
                Badge = "Provision distribution",
                Icon = CACreationUI.Icon("Rimshare/WorldMapIcons/cog"),
                Accent = CACreationUI.Generated,
                Selected = !arrangement.distributionAuthored,
                ConfirmLabel = "Use generated distribution",
                Choose = delegate
                {
                    arrangement.distributionAuthored = false;
                    arrangement.authoredDistribution = -1;
                    CASettlementComposition.ReconcileStartingProvisions(plan,
                        place);
                    CARegionalSetupSession.SavePending();
                }
            });
            foreach (CAProvisionDistribution distribution in
                Enum.GetValues(typeof(CAProvisionDistribution)))
            {
                CAProvisionDistribution local = distribution;
                string consequence = distribution
                    == CAProvisionDistribution.Household
                        ? (arrangement.operatorKind
                                == CAProvisionOperator.Household
                            ? "Individual households keep and operate it."
                            : "One household-scale site operates it.")
                        : distribution
                            == CAProvisionDistribution.Neighborhood
                            ? "One to three local sites operate it, constrained "
                                + "by settlement scale and services."
                            : "One central site operates it.";
                options.Add(new CACreationChoice
                {
                    Key = local.ToString(),
                    Name = local.ToString().CapitalizeFirst(),
                    Summary = consequence,
                    Traits = "Distribution only · quantity unchanged",
                    Badge = "Provision distribution",
                    Icon = CACreationUI.Icon(distribution
                        == CAProvisionDistribution.Household
                            ? "Rimshare/WorldMapIcons/flowers"
                            : distribution
                                == CAProvisionDistribution.Neighborhood
                                ? "Rimshare/WorldMapIcons/divided-square"
                                : "Rimshare/WorldMapIcons/factory"),
                    Accent = CACreationUI.Authored,
                    Selected = arrangement.distributionAuthored
                        && arrangement.PreferredDistribution == local,
                    ConfirmLabel = "Use this distribution",
                    Choose = delegate
                    {
                        arrangement.distribution = local;
                        arrangement.authoredDistribution = (int)local;
                        arrangement.distributionAuthored = true;
                        CASettlementComposition.ReconcileStartingProvisions(plan,
                            place);
                        CARegionalSetupSession.SavePending();
                    }
                });
            }
            CACreationUI.OpenChoices("Provision distribution",
                "Choose where this provision is operated or held. The choice "
                + "does not change its quantity, population owner, or purpose.",
                options);
        }

        private void OpenSettlementAuthorityMenu(CARegionalFactionPlan group)
        {
            var options = new List<CACreationChoice>();
            CASettlementAuthority current =
                CARegionalSettlements.SettlementAuthorityOf(plan, group);
            foreach (CASettlementAuthority authority in
                CARegionalSettlements.ActiveSettlementAuthorities)
            {
                CASettlementAuthority local = authority;
                string[] sharedResponsibilities =
                    CARegionalSettlements.SharedResponsibilities(authority);
                string name = CARegionalSettlements
                    .SettlementAuthorityWords(authority).CapitalizeFirst();
                options.Add(new CACreationChoice
                {
                    Key = local.ToString(),
                    Name = name,
                    Summary = sharedResponsibilities.Length == 0
                        ? "settlements remain independent"
                        : "shared: "
                            + string.Join(", ", sharedResponsibilities),
                    Traits = "Faction-wide settlement authority",
                    Badge = "Explicit structure",
                    Icon = CACreationUI.Icon(authority
                        == CASettlementAuthority.Independent
                            ? "Rimshare/WorldMapIcons/anarchy"
                            : "Rimshare/WorldMapIcons/castle"),
                    Accent = CACreationUI.Authored,
                    Selected = group.settlementAuthorityExplicit
                        && current == authority,
                    ConfirmLabel = "Use this authority",
                    Choose = delegate
                    {
                        group.settlementAuthority = (byte)local;
                        group.settlementAuthorityExplicit = true;
                        CARegionalSetupSession.SavePending();
                    }
                });
            }
            options.Add(new CACreationChoice
            {
                Key = "generated",
                Name = "Use faction structure",
                Summary = "Derive settlement authority from the faction's "
                    + "realized structure.",
                Traits = "Current result: "
                    + CARegionalSettlements.SettlementAuthorityWords(current),
                Badge = "Generated structure",
                Icon = CACreationUI.Icon("Rimshare/WorldMapIcons/cog"),
                Accent = CACreationUI.Generated,
                Selected = !group.settlementAuthorityExplicit,
                ConfirmLabel = "Use generated authority",
                Choose = delegate
                {
                    group.settlementAuthorityExplicit = false;
                    group.settlementAuthority = (byte)
                        CARegionalSettlements.DeriveSettlementAuthority(
                            plan, group);
                    CARegionalSetupSession.SavePending();
                }
            });
            CACreationUI.OpenChoices("Settlement authority",
                "Choose which responsibilities this faction's settlements "
                + "share. Federation and inter-faction relations remain "
                + "independent.", options);
        }

        private void OpenFederationMenu(CARegionalFactionPlan group)
        {
            var options = new List<CACreationChoice>
            {
                new CACreationChoice
                {
                    Key = "none",
                    Name = "No federation",
                    Summary = "This faction remains institutionally independent.",
                    Traits = "Relations and alliances remain separate facts",
                    Group = "Membership",
                    Badge = group.federationKey < 0 ? "Current" : "Membership",
                    Icon = CACreationUI.Icon("Rimshare/WorldMapIcons/anarchy"),
                    Accent = CACreationUI.Unset,
                    Selected = group.federationKey < 0,
                    ConfirmLabel = "Leave federation",
                    Choose = delegate
                    {
                        group.federationKey = -1;
                        group.federationKind = null;
                        CARegionalSetupSession.SavePending();
                    }
                }
            };
            foreach (CARegionalFactionPlan other in plan.factions
                .Where(item => item != null && item.key != group.key)
                .OrderBy(item => item.key))
            {
                CARegionalFactionPlan local = other;
                bool together = group.federationKey >= 0
                    && group.federationKey == local.federationKey;
                options.Add(new CACreationChoice
                {
                    Key = "faction:" + local.key,
                    Name = "Federate with "
                        + CARegionalPlanUtility.FactionName(local),
                    Summary = "Join the same federation while remaining "
                        + "separate factions.",
                    Traits = "Shared responsibilities set separately",
                    Group = "Membership",
                    Badge = together ? "Current" : "Membership",
                    Icon = local.ResolvedFactionDef?.FactionIcon,
                    Accent = CARegionalWorldOverlay.FactionColor(local.key),
                    Selected = together,
                    ConfirmLabel = "Join this federation",
                    Choose = delegate
                    {
                        int key = local.federationKey >= 0
                            ? local.federationKey
                            : group.federationKey >= 0
                                ? group.federationKey : NextFederationKey();
                        group.federationKey = key;
                        local.federationKey = key;
                        if (group.federationKind.NullOrEmpty())
                            group.federationKind = local.federationKind
                                ?? "defense";
                        if (local.federationKind.NullOrEmpty())
                            local.federationKind = group.federationKind;
                        CARegionalSetupSession.SavePending();
                    }
                });
            }
            if (group.federationKey >= 0)
            {
                string currentKind = plan.factions.Where(item => item != null
                        && item.federationKey == group.federationKey)
                    .Select(item => item.federationKind)
                    .FirstOrDefault(item => !item.NullOrEmpty()) ?? "defense";
                foreach (string kind in new[]
                    { "defense", "taxes", "diplomacy",
                        "defense and diplomacy" })
                {
                    string localKind = kind;
                    options.Add(new CACreationChoice
                    {
                        Key = "kind:" + localKind,
                        Name = CASettlementAuthorityWriter
                            .FederationKindWords(localKind).CapitalizeFirst(),
                        Summary = "Responsibilities shared by every member "
                            + "of this federation.",
                        Traits = "Faction membership remains unchanged",
                        Group = "Responsibilities",
                        Badge = currentKind == localKind
                            ? "Current" : "Shared terms",
                        Icon = CACreationUI.Icon(localKind.Contains("defense")
                            ? "Rimshare/WorldMapIcons/crenulated-shield"
                            : localKind == "diplomacy"
                                ? "Rimshare/WorldMapIcons/compass"
                                : "Rimshare/WorldMapIcons/cog"),
                        Accent = CACreationUI.Authored,
                        Selected = currentKind == localKind,
                        ConfirmLabel = "Use these shared terms",
                        Choose = delegate
                        {
                            foreach (CARegionalFactionPlan member
                                in plan.factions)
                                if (member != null && member.federationKey
                                    == group.federationKey)
                                    member.federationKind = localKind;
                            CARegionalSetupSession.SavePending();
                        }
                    });
                }
            }
            CACreationUI.OpenChoices("Federation",
                "Choose federation membership and shared responsibilities "
                + "as separate facts. Faction relations remain independent.",
                options);
        }

        private int NextFederationKey()
        {
            int key = 0;
            foreach (CARegionalFactionPlan group in plan.factions)
                if (group != null && group.federationKey >= key)
                    key = group.federationKey + 1;
            return key;
        }

        // A faction card contains its settlements and population summaries.
        private void FactionCard(ref float y, float width,
            CARegionalFactionPlan group)
        {
            List<CARegionalSettlementPlan> owned = plan.settlements.Where(item =>
                    item != null && item.factionKey == group.key)
                .OrderBy(item => item.slot).ToList();
            Color chip = CARegionalWorldOverlay.FactionColor(group.key);
            string character = CAFactionAxes.Characterize(plan, group);

            // measure, then draw the container, then fill it
            const float pad = 8f;
            float characterH = Text.CalcHeight(character, width - pad * 2f);
            float innerH = 40f + characterH + 4f
                + (owned.Count == 0 ? 22f : owned.Count * 40f) + 4f;
            Rect card = new Rect(0f, y, width, innerH + pad * 2f);
            bool overCard = Mouse.IsOver(card);
            Widgets.DrawBoxSolid(card, new Color(0.13f, 0.15f, 0.17f,
                overCard ? 0.95f : 0.75f));
            GUI.color = overCard
                ? new Color(chip.r, chip.g, chip.b, 0.9f)
                : new Color(0.32f, 0.35f, 0.39f);
            Widgets.DrawBox(card, 1);
            GUI.color = Color.white;
            if (overCard)
                CARegionMapWidget.emphasisFactionKey = group.key;

            float cy = y + pad;
            float cx = pad;
            float cw = width - pad * 2f;

            // faction header
            Rect header = new Rect(cx, cy, cw, 36f);
            Widgets.DrawBoxSolid(new Rect(cx, cy + 6f, 10f, 10f), chip);
            Widgets.Label(new Rect(cx + 16f, cy, cw - 16f, 20f),
                CARegionalPlanUtility.FactionName(group));
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.74f, 0.77f, 0.81f);
            Widgets.Label(new Rect(cx + 16f, cy + 18f, cw - 16f, 16f),
                group.TechnologySummary + " · "
                + (owned.Count == 0 ? "landless"
                    : owned.Count + " settlement"
                        + (owned.Count == 1 ? "" : "s"))
                + " · " + FactionStateWords(group));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (Mouse.IsOver(header)) Widgets.DrawHighlight(header);
            if (Widgets.ButtonInvisible(header))
                CARegionMapWidget.SelectFaction(group.key);
            cy += 40f;

            // its character, from the same state generation consumes
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.68f, 0.72f, 0.77f);
            Widgets.Label(new Rect(cx, cy, cw, characterH), character);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            cy += characterH + 4f;

            // its settlements, contained
            if (owned.Count == 0)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.6f, 0.64f, 0.69f);
                Widgets.Label(new Rect(cx + 16f, cy, cw - 16f, 18f),
                    "No settlements in this region.");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                cy += 22f;
            }
            foreach (CARegionalSettlementPlan place in owned)
            {
                Rect row = new Rect(cx + 12f, cy, cw - 12f, 38f);
                if (Mouse.IsOver(row))
                {
                    Widgets.DrawHighlight(row);
                    CARegionMapWidget.hoveredSlot = place.slot;
                }
                Widgets.DrawBoxSolid(new Rect(cx + 12f, cy + 8f, 8f, 8f),
                    chip);
                Widgets.Label(new Rect(cx + 26f, cy, cw - 26f, 20f),
                    CARegionalPlanUtility.SettlementName(plan, place));
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.72f, 0.75f, 0.8f);
                Widgets.Label(new Rect(cx + 26f, cy + 18f, cw - 26f, 16f),
                    PopulationLine(place));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                if (Widgets.ButtonInvisible(row))
                    CARegionMapWidget.SelectSettlement(place.slot);
                cy += 40f;
            }
            y += card.height + 8f;
        }

        // Compact population summary from the stored population groups.
        private string PopulationLine(CARegionalSettlementPlan place)
        {
            if (place.populationGroups == null || place.populationGroups.Count == 0)
                return "Population not set";
            int minorities = place.populationGroups.Count(c => c != null
                && (c.kind == CAPopulationGroupKind.OtherFaction
                    || c.kind == CAPopulationGroupKind.LocalResidents));
            int quarters = place.populationGroups.Count(c => c != null && c.quarter);
            int ideoligions = place.populationGroups.Count(c => c != null
                && (c.ideoligionFactionKey >= 0 || c.independentIdeoligionKey >= 0));
            var parts = new List<string>();
            parts.Add(place.populationGroups.Count == 1 ? "1 population group"
                : place.populationGroups.Count + " population groups");
            if (minorities > 0)
                parts.Add(minorities + " minorit"
                    + (minorities == 1 ? "y" : "ies"));
            if (quarters > 0)
                parts.Add(quarters + " separate quarter"
                    + (quarters == 1 ? "" : "s"));
            if (ideoligions > 0)
                parts.Add(ideoligions + " separate Ideoligion"
                    + (ideoligions == 1 ? "" : "s"));
            return string.Join(" · ", parts.ToArray());
        }

        // ---- a landmark or historical site ---------------------------------

        private void DrawFeaturePanel(ref float y, float width)
        {
            CARegionalCandidateFacts facts =
                CARegionalProjectionPreview.FactsFor(plan);
            CARegionalCandidateFacts.Feature feature = facts?.Features
                .FirstOrDefault(item => item.Tile.tileId
                    == CARegionMapWidget.selectedTileId
                    && (CARegionMapWidget.selectedFeatureDefName.NullOrEmpty()
                        || item.Def?.defName
                            == CARegionMapWidget.selectedFeatureDefName));
            if (feature == null) {
                CARegionMapWidget.SelectRegion();
                DrawRegionOverview(ref y, width);
                return;
            }

            Back(ref y, width, "All of this region");
            Title(ref y, width, feature.Name);
            Readout(ref y, width, "Kind", FeatureKindWords(feature));
            Readout(ref y, width, "Type", feature.Def.LabelCap);
            Readout(ref y, width, "Ground",
                CARegionalPlanUtility.TileWords(feature.Tile.tileId));
            Body(ref y, width, FeatureDescription(feature));
            // (FeatureDescription: the native description where one
            // exists; never generated filler about the type name.)
            List<CARegionalSettlementPlan> here = plan.settlements.Where(item =>
                item != null
                && item.memberTileId == feature.Tile.tileId).ToList();
            if (here.Count == 0) return;
            Rule(ref y, width);
            Title(ref y, width, "Settlements on this ground");
            foreach (CARegionalSettlementPlan place in here)
                if (Entry(ref y, width,
                        CARegionalPlanUtility.SettlementName(plan, place),
                        CARegionalPlanUtility.FactionName(plan.FactionPlan(
                            place.factionKey)),
                        CARegionalWorldOverlay.FactionColor(
                            place.factionKey)))
                    CARegionMapWidget.SelectSettlement(place.slot);
        }

        // Prefer the native feature or mutator description.
        private static string FeatureDescription(
            CARegionalCandidateFacts.Feature feature)
        {
            string native = feature.Def?.description;
            if (native.NullOrEmpty())
            {
                TileMutatorDef mutator =
                    DefDatabase<TileMutatorDef>.GetNamedSilentFail(
                        feature.Def?.defName ?? "");
                if (mutator == null && feature.Def != null)
                    mutator = DefDatabase<TileMutatorDef>.AllDefsListForReading
                        .FirstOrDefault(def => def.label != null
                            && def.label == feature.Def.label);
                native = mutator?.description;
            }
            if (!native.NullOrEmpty()) return native;
            return feature.Historical
                ? "Remains from earlier occupation stand in this area."
                : "This feature changes map generation in this area.";
        }

        private static string FeatureKindWords(
            CARegionalCandidateFacts.Feature feature)
        {
            return feature?.Historical == true ? "Old site"
                : feature?.GeographicFeature == true
                    ? "Geographic feature" : "Landmark";
        }

        // ---- a neighboring faction holding --------------------------------

        private void DrawNeighborPanel(ref float y, float width)
        {
            CARegionalCandidateFacts facts =
                CARegionalProjectionPreview.FactsFor(plan);
            CARegionalCandidateFacts.Neighbor neighbor = facts?.Neighbors
                .FirstOrDefault(item => item.Tile.tileId
                    == CARegionMapWidget.selectedTileId);
            if (neighbor == null) {
                CARegionMapWidget.SelectRegion();
                DrawRegionOverview(ref y, width);
                return;
            }

            Back(ref y, width, "All of this region");
            Title(ref y, width, neighbor.Object.LabelCap.NullOrEmpty()
                ? neighbor.Object.def?.LabelCap.ToString() ?? "unnamed"
                : neighbor.Object.LabelCap.ToString());
            Readout(ref y, width, "Faction",
                neighbor.Faction?.Name ?? "unaligned");
            if (neighbor.Faction != null)
                Readout(ref y, width, "Player faction",
                    neighbor.Faction.PlayerRelationKind.ToString());
            Readout(ref y, width, "Distance",
                neighbor.WorldDistanceTiles.ToString("F0") + " tiles "
                + Bearing(neighbor));
            Readout(ref y, width, "Ground",
                CARegionalPlanUtility.TileWords(neighbor.Tile.tileId));
            Body(ref y, width, "This world object is outside the starting "
                + "region and remains a neighboring holding."
                + (neighbor.InFrame
                    ? " It is close enough to appear inside this area diagram."
                    : ""));
        }

        // ---- operations -----------------------------------------------------

        // Complete open draft state in dependency order: faction beliefs and
        // structure, settlement pattern, population groups, then provisions.
        private void GenerateUnspecified()
        {
            bool patternFilled = !plan.settlementRealizationComplete;
            // Generated relations are realized first because faction defense
            // structure consumes the saved relation, not the tendency.
            CARegionalSettlements.EnsureSettlementPattern(plan);
            int politicalBeliefsFilled = 0;
            int structureFilled = 0;
            foreach (CARegionalFactionPlan group in plan.factions)
            {
                if (group == null) continue;
                group.EnsureCultureAndPolitics(plan);
                string seed = (plan.candidateId ?? "ca") + ":faction:"
                    + group.key;
                politicalBeliefsFilled += CAPoliticalBeliefsModel.GenerateUnset(
                    group.politicalBeliefs, seed + ":politics");
                structureFilled += CAFactionAxes.Derive(plan, group);
            }

            int populationGroupsFilled = 0;
            int provisionsFilled = 0;
            // Faction structure can change derived facilities. Recompute the
            // still-unconfirmed settlement realization before provisions read
            // it; the fixed candidate seed keeps every unrelated fact stable.
            CARegionalSettlements.Invalidate(plan);
            CARegionalSettlements.EnsureSettlementPattern(plan);
            foreach (CARegionalSettlementPlan place in plan.settlements)
            {
                if (place == null) continue;
                bool hadPopulationGroups = place.populationGroups != null
                    && place.populationGroups.Count > 0;
                bool hadProvisions = place.startingProvisions != null
                    && place.startingProvisions.Any(item => item != null
                        && item.active);
                CASettlementComposition.EnsureDerived(plan, place);
                if (!hadPopulationGroups && place.populationGroups.Count > 0)
                    populationGroupsFilled++;
                if (!hadProvisions && place.startingProvisions.Any(item =>
                        item != null && item.active))
                    provisionsFilled++;
            }
            // Open origins draw from nearby existing settlements. Generation
            // never creates additional world population.
            int originsFilled = 0;
            int originsShort = 0;
            List<CARegionalCandidateFacts.Neighbor> pool =
                ReallocatableSettlements();
            int poolCursor = 0;
            foreach (CARegionalSettlementPlan place in plan.settlements)
            {
                if (place == null || place.populationOrigin
                    != CASettlementOrigin.Unset) continue;
                if (poolCursor < pool.Count)
                {
                    place.populationOrigin =
                        CASettlementOrigin.ReallocatedFromWorldPool;
                    place.reallocatedFromTileId =
                        pool[poolCursor++].Tile.tileId;
                    originsFilled++;
                }
                else originsShort++;
            }
            if (originsFilled > 0)
            {
                CARegionalSettlements.Invalidate(plan);
                CARegionalSettlements.EnsureSettlementPattern(plan);
                foreach (CARegionalSettlementPlan place in plan.settlements
                    .Where(item => item != null))
                    CASettlementComposition.ReconcileStartingProvisions(plan,
                        place);
            }
            CARegionalSetupSession.SavePending();
            var filled = new List<string>();
            if (populationGroupsFilled > 0)
                filled.Add(Counted(populationGroupsFilled,
                    "settlement population"));
            if (provisionsFilled > 0)
                filled.Add(Counted(provisionsFilled,
                    "starting-provision set"));
            if (patternFilled) filled.Add("the regional pattern");
            if (politicalBeliefsFilled > 0)
                filled.Add(Counted(politicalBeliefsFilled,
                    "political-belief field"));
            if (structureFilled > 0)
                filled.Add(Counted(structureFilled,
                    "faction-structure field"));
            if (originsFilled > 0)
                filled.Add(Counted(originsFilled, "population origin") + " "
                    + "from the authorized pool");
            Messages.Message((filled.Count == 0
                ? "All settings are already set."
                : "Generated: " + string.Join(", ", filled.ToArray())
                    + ". Explicit settings remained unchanged.")
                + (originsShort > 0
                    ? " " + Counted(originsShort, "settlement")
                        + (originsShort == 1
                            ? " still needs a " : " still need a ")
                        + "population source. Select Scenario override or "
                        + "remove them."
                    : ""),
                MessageTypeDefOf.NeutralEvent, false);
        }

        private string StartingProvisionSummary()
        {
            int arrangements = 0;
            int nodes = 0;
            bool regionReach = false;
            foreach (CARegionalSettlementPlan place in plan.settlements)
            {
                if (place?.startingProvisions == null) continue;
                foreach (CAStartingProvision arrangement in
                    place.startingProvisions)
                {
                    if (arrangement == null || !arrangement.active
                        || arrangement.operatorKind
                            == CAProvisionOperator.Household) continue;
                    arrangements++;
                    nodes += Math.Max(1, arrangement.nodes);
                    regionReach |= arrangement.reach
                        == CAProvisionReach.Region;
                }
            }
            if (arrangements == 0) return "not arranged yet";
            return Counted(arrangements, "arrangement") + " · "
                + Counted(nodes, "node")
                + (regionReach ? " · a reserve serves the region" : "");
        }

        // A faction may exist before it owns a settlement.
        private void NewFaction()
        {
            int key = CARegionalPlanUtility.LowestFreeFactionKey(plan);
            var group = new CARegionalFactionPlan
            {
                key = key,
                source = CARegionalFactionSource.NewWorldFaction,
                authored = true
            };
            EnsureFactionChoice(group);
            plan.factions.Add(group);
            group.customName = RollFactionName(group);
            // Name and faction type are initialized; beliefs and structure
            // remain open until set, preset, or generated.
            CARegionalPlanUtility.EnsureRelationRows(plan);
            CARegionMapWidget.SelectFaction(key);
            CARegionalSetupSession.SavePending();
        }

        private void AddSettlement()
        {
            int key;
            if (plan.factions.Count == 0)
            {
                key = CARegionalPlanUtility.LowestFreeFactionKey(plan);
                var group = new CARegionalFactionPlan { key = key };
                EnsureFactionChoice(group);
                plan.factions.Add(group);
            }
            else
                key = plan.settlements.Count > 0
                    ? plan.settlements[plan.settlements.Count - 1].factionKey
                    : plan.factions.OrderBy(item => item.key)
                        .First().key;
            AddSettlementFor(key);
        }

        private void AddSettlementFor(int key)
        {
            // Settlement placement is independent of the landing-site choice.
            int tile = plan.memberTileIds.FirstOrDefault(id =>
                plan.settlements.All(item => item.memberTileId != id));
            if (tile == 0 && !plan.memberTileIds.Contains(0))
                tile = plan.memberTileIds[0];
            int slot = CARegionalPlanUtility.LowestFreeSlot(plan);
            plan.settlements.Add(new CARegionalSettlementPlan
            {
                slot = slot,
                memberTileId = tile,
                factionKey = key,
                siteClusterKey = slot,
                persistent = true
            });
            CARegionalPlanUtility.EnsureRelationRows(plan);
            CARegionMapWidget.SelectSettlement(slot);
            CARegionalSetupSession.SavePending();
        }

        // Auto-created factions are removed with their last settlement.
        // Explicitly added factions remain.
        private void RemoveUnusedFactions()
        {
            var used = new HashSet<int>(plan.settlements.Select(item =>
                item.factionKey));
            plan.factions.RemoveAll(group => !group.authored
                && !used.Contains(group.key));
            CARegionalPlanUtility.EnsureRelationRows(plan);
        }

        // Read-only by contract: a region row never derives anything.
        private string FactionStateWords(CARegionalFactionPlan group)
        {
            if (group.ResolvedFactionDef == null)
                return "faction type not set";
            int beliefOpen = CAFactionAxes.CountByState(
                group.politicalBeliefs?.positions,
                CAAxisSource.Unset);
            int structureOpen = CAFactionAxes.CountByState(group.factionStructure,
                CAAxisSource.Unset);
            var parts = new List<string>();
            parts.Add(beliefOpen == 0 ? "political beliefs set"
                : beliefOpen + " political fields unset");
            parts.Add(structureOpen == 0 ? "faction structure set"
                : structureOpen + " structure fields unset");
            return string.Join(" · ", parts.ToArray());
        }

        // These descriptions read the same composition state as generation.
        // When an outcome depends on generated pawns, they describe the rule.

        private static string AxisWord(CARegionalFactionPlan group,
            string axisKey)
        {
            return CAFactionAxes.OptionOf(group.factionStructure, axisKey)?.Label;
        }

        private string FactionScopePhrase(CARegionalFactionPlan group,
            int held)
        {
            var parts = new List<string>();
            if (held > 1)
                parts.Add(CARegionalSettlements.SettlementAuthorityWords(
                    CARegionalSettlements.SettlementAuthorityOf(plan, group)));
            if (group.federationKey >= 0)
            {
                string kind = plan.factions
                    .Where(g => g != null
                        && g.federationKey == group.federationKey)
                    .Select(g => g.federationKind)
                    .FirstOrDefault(k => !k.NullOrEmpty()) ?? "defense";
                parts.Add("in a " + CASettlementAuthorityWriter
                    .FederationKindName(kind));
            }
            return parts.Count == 0 ? ""
                : " (" + string.Join(", ", parts.ToArray()) + ")";
        }

        private string GovernmentPhrase(CARegionalFactionPlan group)
        {
            string leadership = AxisWord(group, CAFactionAxes.Leadership);
            string decisions = AxisWord(group, CAFactionAxes.Decisions);
            if (leadership == null && decisions == null)
                return "government undecided";
            return (leadership ?? "leadership not set")
                + (decisions != null ? ", " + decisions : "");
        }

        private string EconomyPhrase(CARegionalFactionPlan group)
        {
            string ownership = AxisWord(group, CAFactionAxes.Ownership);
            string economy = AxisWord(group,
                CAFactionAxes.Economy);
            if (ownership == null && economy == null)
                return "economy undecided";
            return (ownership ?? "ownership not set")
                + (economy != null ? ", " + economy : "");
        }

        private string DefensePhrase(CARegionalFactionPlan group)
        {
            string localOrder = AxisWord(group, CAFactionAxes.LocalOrder);
            string defense = AxisWord(group, CAFactionAxes.Defense);
            if (localOrder == null && defense == null)
                return "defense undecided";
            return (localOrder ?? "local order not set")
                + (defense != null ? ", " + defense : "");
        }

        // Population and Ideoligion summary across settlements.
        private string RegionPopulationWords()
        {
            int populatedSettlements = 0;
            int minorityGroups = 0;
            int quarters = 0;
            int factionIdeoligions = 0;
            int independentIdeoligions = 0;
            foreach (CARegionalSettlementPlan place in plan.settlements)
            {
                if (place?.populationGroups == null || place.populationGroups.Count == 0)
                    continue;
                populatedSettlements++;
                foreach (CASettlementPopulationGroup populationGroup in place.populationGroups)
                {
                    if (populationGroup == null) continue;
                    if (populationGroup.kind == CAPopulationGroupKind.OtherFaction
                        || populationGroup.kind == CAPopulationGroupKind.LocalResidents)
                        minorityGroups++;
                    if (populationGroup.quarter) quarters++;
                    if (populationGroup.independentIdeoligionKey >= 0)
                        independentIdeoligions++;
                    else if (populationGroup.ideoligionFactionKey >= 0)
                        factionIdeoligions++;
                }
            }
            if (populatedSettlements == 0) return null;
            var parts = new List<string>();
            parts.Add(populatedSettlements + " populated settlement"
                + (populatedSettlements == 1 ? "" : "s"));
            if (minorityGroups > 0)
                parts.Add(minorityGroups + " minority population group"
                    + (minorityGroups == 1 ? "" : "s"));
            if (quarters > 0)
                parts.Add(quarters + " separate quarter"
                    + (quarters == 1 ? "" : "s"));
            if (factionIdeoligions > 0)
                parts.Add(factionIdeoligions + " faction-sourced Ideoligion"
                    + (factionIdeoligions == 1 ? "" : "s"));
            if (independentIdeoligions > 0)
                parts.Add(independentIdeoligions + " independent Ideoligion"
                    + (independentIdeoligions == 1 ? "" : "s"));
            return string.Join(" · ", parts.ToArray());
        }

        // Preview uses the same wealth and construction-era resolver as
        // settlement materialization.
        private string MaterialProspect(CARegionalSettlementPlan place,
            CARegionalFactionPlan owner, FactionDef def)
        {
            if (def == null)
                return "Choose an owning faction to determine knowledge, "
                    + "wealth, and construction era.";
            if (plan.candidateId == null)
            {
                plan.candidateId = CARegionalPlanUtility.MintCandidateId();
                CARegionalSetupSession.SavePending();
            }
            TechLevel knowledge = CASettlementAxes.TemplateEraPrior(def);
            bool road = CARegionalPlanUtility.ConstituentHasRoad(
                place.memberTileId);
            bool coast = CARegionalPlanUtility.ConstituentIsCoastal(
                place.memberTileId);
            int wealth;
            int constructionEra;
            int facilities = CASettlementStartingState.ResolveFacilityMask(plan,
                place, def);
            int access = CASettlementStartingState.Access(plan, place);
            int services = CASettlementStartingState.Services(plan, place);
            int civic = CASettlementStartingState.Civic(plan, place);
            CASettlementWealth.Derive(plan.candidateId, place.slot,
                facilities, access, services,
                civic, knowledge, out wealth, out constructionEra);
            int knowledgeTier = CASettlementAxes.CapabilityBasis(knowledge);
            int tier = CASettlementAxes.LocalPracticeCeiling(facilities,
                access, services, civic, knowledge);
            var lines = new List<string>();
            lines.Add("Material wealth: "
                + CASettlementWealth.WealthWords(wealth));
            lines.Add("Founded in its "
                + CASettlementWealth.TierWords(constructionEra)
                + " era · local capability "
                + CASettlementWealth.TierWords(tier)
                + " · faction knowledge "
                + CASettlementWealth.TierWords(knowledgeTier));
            if (constructionEra < tier)
                lines.Add("Older construction remains in roughly half of "
                    + "the settlement walls");
            else
                lines.Add("Construction uses one era throughout");
            lines.Add(tier >= 2
                ? (wealth >= 2
                    ? "Ordinary buildings use glass windows"
                    : "Glass windows remain scarce")
                : wealth >= 2 && tier == 1
                    ? "Some buildings use imported glass windows"
                    : "Windows use shutters or remain open");
            lines.Add("Starting personal silver depends on wealth and "
                + "the Economy setting");
            if (tier < knowledgeTier)
                lines.Add("This settlement lacks the local means to practice "
                    + "all methods known by its faction");
            return string.Join("\n", lines.ToArray());
        }

        // Final count of explicit, generated, and unset state before the plan
        // becomes world state.
        private string FinalRepresentation(string blocker)
        {
            var text = new System.Text.StringBuilder();
            int structureExplicit = 0;
            int structureGenerated = 0;
            int structureUnset = 0;
            int beliefsExplicit = 0;
            int beliefsGenerated = 0;
            int beliefsUnset = 0;
            var tensions = new List<string>();
            foreach (CARegionalFactionPlan group in
                plan.factions)
            {
                if (group == null) continue;
                group.EnsureCultureAndPolitics(plan);
                structureExplicit += CAFactionAxes.CountByState(group.factionStructure,
                    CAAxisSource.Authored);
                structureGenerated += CAFactionAxes.CountByState(group.factionStructure,
                        CAAxisSource.Generated)
                    + CAFactionAxes.CountByState(group.factionStructure,
                        CAAxisSource.Preset);
                structureUnset += CAFactionAxes.CountByState(group.factionStructure,
                    CAAxisSource.Unset);
                beliefsExplicit += CAFactionAxes.CountByState(
                    group.politicalBeliefs.positions, CAAxisSource.Authored);
                beliefsGenerated += CAFactionAxes.CountByState(
                        group.politicalBeliefs.positions,
                        CAAxisSource.Generated)
                    + CAFactionAxes.CountByState(group.politicalBeliefs.positions,
                        CAAxisSource.Preset);
                beliefsUnset += CAFactionAxes.CountByState(
                    group.politicalBeliefs.positions,
                    CAAxisSource.Unset);
                foreach (string tension in CAFactionStructureModel.Tensions(
                    group.politicalBeliefs, group.factionStructure))
                    tensions.Add(CARegionalPlanUtility.FactionName(group)
                        + ": " + tension);
            }
            int explicitPopulations = plan.settlements.Count(b =>
                b?.populationGroups != null && b.populationGroups.Any(c =>
                    c != null && c.authored));
            int generatedPopulations = plan.settlements.Count(b =>
                b?.populationGroups != null && b.populationGroups.Count > 0
                && !b.populationGroups.Any(c => c != null && c.authored));
            int unsetPopulations = plan.settlements.Count(b =>
                b?.populationGroups == null || b.populationGroups.Count == 0);
            int reallocated = plan.settlements.Count(b => b != null
                && b.populationOrigin
                    == CASettlementOrigin.ReallocatedFromWorldPool);
            int overrides = plan.settlements.Count(b => b != null
                && b.populationOrigin
                    == CASettlementOrigin.ScenarioOverride);
            int unsetOrigins = plan.settlements.Count(b => b != null
                && b.populationOrigin == CASettlementOrigin.Unset);

            text.AppendLine("Set:");
            text.AppendLine("  " + Counted(plan.factions.Count(g =>
                    g != null && g.authored), "faction") + " added · "
                + Counted(beliefsExplicit, "political-belief field") + " · "
                + Counted(structureExplicit, "faction-structure field")
                + " · " + Counted(explicitPopulations,
                    "settlement population"));
            text.AppendLine("  " + Counted(reallocated,
                    "population source") + " reallocated · "
                + Counted(overrides, "scenario override"));
            text.AppendLine();
            text.AppendLine("Generated:");
            text.AppendLine("  " + Counted(beliefsGenerated,
                    "political-belief field") + " · "
                + Counted(structureGenerated,
                    "faction-structure field") + " · "
                + Counted(generatedPopulations,
                    "settlement population")
                + (plan.settlementPattern
                    != (byte)CASettlementPattern.Unsettled
                    ? " · settlement pattern" : ""));
            text.AppendLine();
            text.AppendLine("Unset:");
            text.AppendLine("  " + Counted(beliefsUnset,
                    "political-belief field") + " · "
                + Counted(structureUnset,
                    "faction-structure field") + " · "
                + Counted(unsetPopulations, "settlement population"));
            if (plan.settlements.Any(item => item != null))
            {
                text.AppendLine();
                text.AppendLine("Settlement development:");
                foreach (CARegionalSettlementPlan place in plan.settlements
                    .Where(item => item != null).OrderBy(item => item.slot))
                {
                    int facilities =
                        CASettlementStartingState.ResolveFacilityMask(plan,
                            place);
                    int facilityOverrides = CountFacilityOverrides(
                        place.startingFacilityAuthoredMask);
                    text.AppendLine("  "
                        + CARegionalPlanUtility.SettlementName(plan, place)
                        + ": " + CASettlementStartingState.ProfileWords(
                            place.developmentProfile) + " · transport "
                        + CASettlementStartingState.InfrastructureWords(
                            CASettlementStartingState.Access(plan, place))
                        + (place.accessInfrastructure >= 0
                            ? " (set)" : " (generated)") + ", services "
                        + CASettlementStartingState.InfrastructureWords(
                            CASettlementStartingState.Services(plan, place))
                        + (place.serviceInfrastructure >= 0
                            ? " (set)" : " (generated)") + ", public works "
                        + CASettlementStartingState.InfrastructureWords(
                            CASettlementStartingState.Civic(plan, place))
                        + (place.civicInfrastructure >= 0
                            ? " (set)" : " (generated)") + " · "
                        + FacilityNames(facilities).ToLowerInvariant()
                        + " · " + Counted(facilityOverrides,
                            "facility override"));
                }
            }
            if (tensions.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Beliefs and faction structure differ:");
                foreach (string line in tensions.Take(6))
                    text.AppendLine("  " + line);
            }
            if (blocker != null || unsetOrigins > 0)
            {
                text.AppendLine();
                text.AppendLine("Needs attention:");
                if (unsetOrigins > 0)
                    text.AppendLine("  " + Counted(unsetOrigins,
                            "settlement") + " "
                        + (unsetOrigins == 1 ? "has" : "have")
                        + " no population source.");
                if (blocker != null) text.AppendLine("  " + blocker);
            }
            return text.ToString().TrimEnd();
        }

        private static int CountFacilityOverrides(int mask)
        {
            int count = 0;
            int remaining = mask & CASettlementStartingState.AllFacilityMask;
            while (remaining != 0)
            {
                count += remaining & 1;
                remaining >>= 1;
            }
            return count;
        }

        // Nearby world settlements are the available population sources.
        // Reallocation is recorded in the draft and consumed at map generation.
        private List<CARegionalCandidateFacts.Neighbor>
            ReallocatableSettlements(CARegionalSettlementPlan current = null)
        {
            CARegionalCandidateFacts facts =
                CARegionalProjectionPreview.FactsFor(plan);
            var already = new HashSet<int>(plan.settlements
                .Where(item => item != null
                    && item != current
                    && item.populationOrigin
                        == CASettlementOrigin.ReallocatedFromWorldPool)
                .Select(item => item.reallocatedFromTileId));
            return facts == null
                ? new List<CARegionalCandidateFacts.Neighbor>()
                : facts.Neighbors.Where(item => item.Object is Settlement
                    && item.Faction != null && !item.Faction.IsPlayer
                    && !already.Contains(item.Tile.tileId)).ToList();
        }

        private string OriginSummary(CARegionalSettlementPlan place)
        {
            switch (place.populationOrigin)
            {
                case CASettlementOrigin.ReallocatedFromWorldPool:
                {
                    Settlement source = CARegionalPlanUtility
                        .SettlementAtTile(place.reallocatedFromTileId);
                    return source != null
                        ? "Concentrates " + source.LabelCap
                            + " — absorbed when the map begins"
                        : "The source settlement no longer stands — "
                            + "choose another";
                }
                case CASettlementOrigin.ScenarioOverride:
                    return "Scenario override — additional population";
                default:
                    return "No population source selected";
            }
        }

        private void OpenOriginMenu(CARegionalSettlementPlan place)
        {
            var options = new List<CACreationChoice>();
            foreach (CARegionalCandidateFacts.Neighbor candidate in
                ReallocatableSettlements(place).Take(12))
            {
                CARegionalCandidateFacts.Neighbor local = candidate;
                string name = local.Object.LabelCap.NullOrEmpty()
                    ? local.Object.def?.LabelCap.ToString() ?? "Settlement"
                    : local.Object.LabelCap.ToString();
                options.Add(new CACreationChoice
                {
                    Key = local.Tile.tileId.ToString(),
                    Name = name,
                    Summary = (local.Faction?.Name ?? "Unaffiliated") + " · "
                        + local.WorldDistanceTiles.ToString("F0")
                        + " world tiles away",
                    Traits = "Existing world population",
                    Details = "At confirmation this settlement is absorbed "
                        + "into the authored region. Its population moves; it "
                        + "is not duplicated.",
                    Badge = "Population source",
                    Icon = CACreationUI.Icon(
                        "Rimshare/WorldMapIcons/compass"),
                    Accent = CACreationUI.Accent,
                    Selected = place.populationOrigin
                            == CASettlementOrigin.ReallocatedFromWorldPool
                        && place.reallocatedFromTileId == local.Tile.tileId,
                    ConfirmLabel = "Use this population",
                    Choose = delegate
                    {
                        place.populationOrigin =
                            CASettlementOrigin.ReallocatedFromWorldPool;
                        place.reallocatedFromTileId = local.Tile.tileId;
                        CARegionalSetupSession.SavePending();
                    }
                });
            }
            options.Add(new CACreationChoice
            {
                Key = "scenario-override",
                Name = "Scenario population",
                Summary = "Add this settlement beyond the normal world pool.",
                Traits = "Explicit scenario override",
                Details = "This authoring choice adds population instead of "
                    + "moving it from an existing world settlement.",
                Badge = "Population source",
                Icon = CACreationUI.Icon(
                    "Rimshare/WorldMapIcons/forward-sun"),
                Accent = CACreationUI.Preset,
                Selected = place.populationOrigin
                    == CASettlementOrigin.ScenarioOverride,
                ConfirmLabel = "Use scenario population",
                Choose = delegate
                {
                    place.populationOrigin =
                        CASettlementOrigin.ScenarioOverride;
                    place.reallocatedFromTileId = -1;
                    CARegionalSetupSession.SavePending();
                }
            });
            CACreationUI.OpenChoices("Settlement population",
                "Choose where this settlement's starting population comes "
                + "from. This choice changes population accounting, not "
                + "ownership or placement.", options);
        }

        private string FillSummary()
        {
            if (plan.settlements.Count == 0) return "empty";
            int factions = plan.factions.Select(group => group.key)
                .Distinct().Count();
            return plan.settlements.Count + " settlement"
                + (plan.settlements.Count == 1 ? "" : "s") + ", " + factions
                + " faction" + (factions == 1 ? "" : "s");
        }

        private void OpenFillMenu()
        {
            int sources = ReallocatableSettlements().Count;
            int existingFactions = CARegionalPlanUtility
                .EligibleExistingFactions().Count;
            int newFactionTypes = CARegionalPlanUtility
                .EligibleNewFactionDefs().Count;
            string common = "Applying this replaces the current factions, "
                + "settlements, population sources, and relations. The "
                + "region's land, arrival area, and map scale remain unchanged.";
            var options = new List<CACreationChoice>
            {
                new CACreationChoice
                {
                    Key = "empty",
                    Name = "Uninhabited region",
                    Summary = "No factions or settlements begin in this region.",
                    Traits = "0 factions · 0 settlements",
                    Details = common,
                    Badge = "Region template",
                    Icon = CACreationUI.Icon(
                        "Rimshare/WorldMapIcons/at-sea"),
                    Accent = CACreationUI.Preset,
                    ConfirmLabel = "Use this composition",
                    Choose = delegate { ApplyFillGuarded(0); }
                },
                new CACreationChoice
                {
                    Key = "one-existing",
                    Name = "One existing faction",
                    Summary = "One world faction owns every generated settlement.",
                    Traits = "1 faction · up to " + sources + " settlements",
                    Details = common + " Each settlement absorbs one nearby "
                        + "world settlement as its population source.",
                    Badge = "Region template",
                    Icon = CACreationUI.Icon(
                        "Rimshare/WorldMapIcons/castle"),
                    Accent = CACreationUI.Preset,
                    Disabled = sources == 0 || existingFactions < 1,
                    DisabledReason = sources == 0
                        ? "Unavailable: no nearby world settlement can supply "
                            + "a starting population."
                        : existingFactions < 1
                            ? "Unavailable: no eligible existing faction is "
                                + "available."
                            : null,
                    ConfirmLabel = "Use this composition",
                    Choose = delegate { ApplyFillGuarded(1); }
                },
                new CACreationChoice
                {
                    Key = "several-existing",
                    Name = "Several existing factions",
                    Summary = "Generated settlements belong to different world factions.",
                    Traits = "Up to 2 factions · up to " + sources
                        + " settlements",
                    Details = common + " Each settlement absorbs one nearby "
                        + "world settlement as its population source.",
                    Badge = "Region template",
                    Icon = CACreationUI.Icon(
                        "Rimshare/WorldMapIcons/compass"),
                    Accent = CACreationUI.Preset,
                    Disabled = sources == 0 || existingFactions < 2,
                    DisabledReason = sources == 0
                        ? "Unavailable: no nearby world settlement can supply "
                            + "a starting population."
                        : existingFactions < 2
                            ? "Unavailable: fewer than two eligible existing "
                                + "factions are available."
                            : null,
                    ConfirmLabel = "Use this composition",
                    Choose = delegate { ApplyFillGuarded(2); }
                },
                new CACreationChoice
                {
                    Key = "new-local",
                    Name = "New local factions",
                    Summary = "Generated settlements belong to factions formed here.",
                    Traits = "Up to 2 factions · up to " + sources
                        + " settlements",
                    Details = common + " Faction types, beliefs, and structure "
                        + "remain editable after generation.",
                    Badge = "Region template",
                    Icon = CACreationUI.Icon(
                        "Rimshare/WorldMapIcons/forward-sun"),
                    Accent = CACreationUI.Preset,
                    Disabled = sources == 0 || newFactionTypes == 0,
                    DisabledReason = sources == 0
                        ? "Unavailable: no nearby world settlement can supply "
                            + "a starting population."
                        : newFactionTypes == 0
                            ? "Unavailable: no eligible faction type is "
                                + "available for a new local faction."
                            : null,
                    ConfirmLabel = "Use this composition",
                    Choose = delegate { ApplyFillGuarded(3); }
                }
            };
            CACreationUI.OpenChoices("Regional composition",
                "Choose a complete starting composition for this region. "
                + "The result remains fully editable.", options);
        }

        // A fill is a default, not an editor: it may assist, never silently
        // erase authored composition.
        private void ApplyFillGuarded(int preset)
        {
            bool authored = plan.settlements.Count > 0
                || plan.factions.Count > 0;
            if (!authored) { ApplyFill(preset); return; }
            Verse.Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "This replaces every current settlement (" + plan.settlements.Count
                + "), faction (" + plan.factions.Count + ") and "
                + "relation in this region. It does not change the "
                + "geographic area.\n\nEvery settlement it creates uses one nearby "
                + "existing major settlement. That settlement is absorbed "
                + "into this region when the map begins, moving its population "
                + "rather than duplicating it.\n\nReplace the current design?",
                delegate { ApplyFill(preset); }, destructive: true));
        }

        private void ApplyFill(int preset)
        {
            List<Faction> existing = CARegionalPlanUtility
                .EligibleExistingFactions();
            List<FactionDef> newDefs = CARegionalPlanUtility
                .EligibleNewFactionDefs();
            int requiredExisting = preset == 2 ? 2 : preset == 1 ? 1 : 0;
            if (existing.Count < requiredExisting)
            {
                Messages.Message("This fill needs " + requiredExisting
                        + " distinct eligible existing faction"
                        + (requiredExisting == 1 ? "." : "s."),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (preset == 3 && newDefs.Count == 0)
            {
                Messages.Message("No settlement-capable faction type is "
                    + "available.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            plan.settlements.Clear();
            plan.factions.Clear();
            plan.relations.Clear();
            CARegionMapWidget.SelectRegion();
            if (preset == 0)
            {
                CARegionalSetupSession.SavePending();
                return;
            }

            // Fill count comes from nearby world settlements, not region size.
            // A fill never creates a scenario override.
            List<CARegionalCandidateFacts.Neighbor> pool =
                ReallocatableSettlements();
            int slots = Math.Min(pool.Count, plan.memberTileIds.Count);
            if (slots == 0)
            {
                CARegionalSetupSession.SavePending();
                Messages.Message("No nearby major settlements stand near "
                    + "this region, so a fill has nothing to "
                    + "reallocate. Add settlements manually and mark each an "
                    + "explicit scenario override if this start should "
                    + "exceed the world's population.",
                    MessageTypeDefOf.NeutralEvent, false);
                return;
            }
            int groupCount = preset == 1 ? 1 : Math.Min(2, slots);
            for (int i = 0; i < groupCount; i++)
            {
                bool custom = preset == 3;
                Faction existingFaction = custom || existing.Count <= i
                    ? null : existing[i];
                FactionDef factionDef = newDefs.ElementAtOrDefault(i)
                    ?? newDefs.FirstOrDefault();
                plan.factions.Add(new CARegionalFactionPlan
                {
                    key = i + 1,
                    source = custom
                        ? CARegionalFactionSource.NewWorldFaction
                        : CARegionalFactionSource.ExistingWorldFaction,
                    existingFactionLoadId = existingFaction?.loadID ?? -1,
                    customFactionDefName = factionDef?.defName,
                    customName = custom ? "Regional tribe " + (i + 1) : null,
                    playerRelation = FactionRelationKind.Neutral,
                    authorPlayerRelation = custom,
                    visibleInWorld = true
                });
            }
            // Existing settlement placement does not depend on landing site.
            List<int> available = plan.memberTileIds.ToList();
            int count = Math.Min(slots, available.Count);
            for (int i = 0; i < count; i++)
                plan.settlements.Add(new CARegionalSettlementPlan
                {
                    slot = i,
                    memberTileId = available[i],
                    factionKey = preset == 1 ? 1
                        : (i % groupCount) + 1,
                    siteClusterKey = i,
                    persistent = true,
                    // Each filled settlement consumes one world settlement
                    // as its population source at confirmation.
                    populationOrigin =
                        CASettlementOrigin.ReallocatedFromWorldPool,
                    reallocatedFromTileId = pool[i].Tile.tileId
                });
            CARegionalPlanUtility.EnsureRelationRows(plan);
            foreach (CARegionalFactionPlan group in plan.factions
                .Where(item => item != null))
                group.EnsureCultureAndPolitics(plan);
            CARegionalSetupSession.SavePending();
        }

        private void OpenOwnerMenu(CARegionalSettlementPlan place)
        {
            var options = plan.factions.OrderBy(group => group.key)
                .Select(group =>
                {
                    CARegionalFactionPlan local = group;
                    int held = plan.settlements.Count(item => item != null
                        && item.factionKey == local.key);
                    return new CACreationChoice
                    {
                        Key = "faction:" + local.key,
                        Name = CARegionalPlanUtility.FactionName(local),
                        Summary = local.TechnologySummary + " · " + held
                            + " settlement" + (held == 1 ? "" : "s"),
                        Traits = CAFactionAxes.Characterize(plan, local),
                        Details = "Assigns this settlement to the faction. "
                            + "Population source and physical site remain "
                            + "independent.",
                        Badge = "Settlement owner",
                        Accent = CARegionalWorldOverlay.FactionColor(local.key),
                        Selected = place.factionKey == local.key,
                        ConfirmLabel = "Assign this faction",
                        Choose = delegate
                        {
                            place.factionKey = local.key;
                            CARegionalPlanUtility.EnsureRelationRows(plan);
                            CARegionalSetupSession.SavePending();
                        }
                    };
                }).ToList();
            int freeKey = CARegionalPlanUtility.LowestFreeFactionKey(plan);
            options.Add(new CACreationChoice
            {
                Key = "new-faction",
                Name = "Create a new faction",
                Summary = "Add a local faction and assign this settlement to it.",
                Traits = "Editable type · beliefs · structure · relations",
                Details = "The faction remains in the region even if this "
                    + "settlement is later reassigned or removed.",
                Badge = "New owner",
                Icon = CACreationUI.Icon(
                    "Rimshare/WorldMapIcons/forward-sun"),
                Accent = CACreationUI.Authored,
                ConfirmLabel = "Create and assign faction",
                Choose = delegate
                {
                    var group = new CARegionalFactionPlan
                    {
                        key = freeKey,
                        source = CARegionalFactionSource.NewWorldFaction,
                        authored = true
                    };
                    EnsureFactionChoice(group);
                    plan.factions.Add(group);
                    group.customName = RollFactionName(group);
                    place.factionKey = freeKey;
                    CARegionalPlanUtility.EnsureRelationRows(plan);
                    CARegionalSetupSession.SavePending();
                }
            });
            CACreationUI.OpenChoices("Settlement owner",
                "Choose the faction that owns this settlement. Ownership does "
                + "not change its population source or exact ground.", options);
        }

        private void EnsureFactionChoice(CARegionalFactionPlan group)
        {
            group.resolvedFaction = null;
            group.resolvedFactionLoadId = -1;
            group.playerRelation = FactionRelationKind.Neutral;
            group.authorPlayerRelation = group.source
                == CARegionalFactionSource.NewWorldFaction;
            foreach (CARegionalRelationPlan pair in plan.relations.Where(
                item => item != null && (item.leftFactionKey == group.key
                    || item.rightFactionKey == group.key)))
            {
                pair.relation = FactionRelationKind.Neutral;
                pair.authorRelation = false;
            }
            if (group.source == CARegionalFactionSource.ExistingWorldFaction)
            {
                var used = new HashSet<int>(plan.factions.Where(item =>
                        item != null && item != group && item.source
                            == CARegionalFactionSource.ExistingWorldFaction
                        && item.existingFactionLoadId >= 0)
                    .Select(item => item.existingFactionLoadId));
                Faction faction = CARegionalPlanUtility
                    .EligibleExistingFactions().FirstOrDefault(item =>
                        !used.Contains(item.loadID));
                group.existingFactionLoadId = faction?.loadID ?? -1;
            }
            else
            {
                FactionDef def = CARegionalPlanUtility
                    .EligibleNewFactionDefs().FirstOrDefault(item =>
                        item.techLevel <= TechLevel.Industrial)
                    ?? CARegionalPlanUtility.EligibleNewFactionDefs()
                        .FirstOrDefault();
                group.customFactionDefName = def?.defName;
            }
            group.EnsureCultureAndPolitics(plan);
        }

        private string StartingFacilitiesSummary(CARegionalSettlementPlan place)
        {
            int mask = CASettlementStartingState.ResolveFacilityMask(plan, place,
                plan.FactionPlan(place.factionKey)?.ResolvedFactionDef);
            string prefix = place.startingFacilityAuthoredMask == 0
                ? "Generated by " + CASettlementStartingState.ProfileWords(
                    place.developmentProfile).ToLowerInvariant() + ": "
                : "Generated with explicit overrides: ";
            return prefix + FacilityNames(mask);
        }

        private static string FacilityNames(int mask)
        {
            var names = CAStartingFacilityCatalog.All.Where(program =>
                    (mask & program.Bit) != 0)
                .Select(program => program.Label.ToLowerInvariant()).ToList();
            return names.Count == 0 ? "no starting facilities"
                : string.Join(", ", names);
        }

        private void OpenStartingFacilitiesMenu(
            CARegionalSettlementPlan place)
        {
            Verse.Find.WindowStack.Add(new Dialog_CAStartingFacilities(
                plan, place, delegate { StartingSettingsChanged(place); }));
        }

        private void InfrastructureRow(ref float y, float width,
            CARegionalSettlementPlan place, string label, int authored, int resolved,
            int dimension, string tip)
        {
            Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row), label);
            Rect button = new Rect(LabelWidth, y, width - LabelWidth, 28f);
            string value = (authored < 0
                    ? "Generated by " + CASettlementStartingState.ProfileWords(
                        place.developmentProfile).ToLowerInvariant() + ": "
                    : "Set: ")
                + CASettlementStartingState.InfrastructureWords(resolved);
            if (Widgets.ButtonText(button, value))
                OpenInfrastructureMenu(place, dimension);
            TooltipHandler.TipRegion(button, tip);
            y += Row + Gap;
        }

        private void OpenInfrastructureMenu(CARegionalSettlementPlan place,
            int dimension)
        {
            string name = dimension == 0 ? "transport access"
                : dimension == 1 ? "local services" : "public works";
            int current = dimension == 0 ? place.accessInfrastructure
                : dimension == 1 ? place.serviceInfrastructure
                    : place.civicInfrastructure;
            var options = new List<CACreationChoice>
            {
                new CACreationChoice
                {
                    Key = "generated",
                    Name = "Generate " + name,
                    Summary = "Derive this level from the settlement and its area.",
                    Traits = "Other infrastructure dimensions remain unchanged",
                    Badge = "Generated",
                    Icon = CACreationUI.Icon("Rimshare/WorldMapIcons/cog"),
                    Accent = CACreationUI.Generated,
                    Selected = current < 0,
                    ConfirmLabel = "Use generated value",
                    Choose = delegate
                    {
                        SetInfrastructure(place, dimension, -1);
                    }
                }
            };
            string[] descriptions = dimension == 0
                ? new[] {
                    "isolated; supplies remain local",
                    "limited transport access",
                    "reliable regional access",
                    "strong regional access" }
                : dimension == 1
                    ? new[] {
                        "household-scale support only",
                        "limited local services",
                        "established settlement services",
                        "strong settlement services" }
                    : new[] {
                        "few durable public works",
                        "modest public works",
                        "established public works",
                        "extensive public works" };
            for (int value = 0; value <= 3; value++)
            {
                int local = value;
                options.Add(new CACreationChoice
                {
                    Key = local.ToString(),
                    Name = CASettlementStartingState
                        .InfrastructureWords(local).CapitalizeFirst(),
                    Summary = descriptions[local].CapitalizeFirst() + ".",
                    Traits = "Explicit " + name,
                    Badge = "Set value",
                    Icon = CACreationUI.Icon(dimension == 0
                        ? "Rimshare/WorldMapIcons/compass"
                        : dimension == 1
                            ? "Rimshare/WorldMapIcons/flowers"
                            : "Rimshare/WorldMapIcons/factory"),
                    Accent = CACreationUI.Authored,
                    Selected = current == local,
                    ConfirmLabel = "Use this level",
                    Choose = delegate
                    {
                        SetInfrastructure(place, dimension, local);
                    }
                });
            }
            CACreationUI.OpenChoices(name.CapitalizeFirst(),
                "Set this settlement fact directly. The other infrastructure "
                + "dimensions remain independent.", options);
        }

        private void SetInfrastructure(CARegionalSettlementPlan place,
            int dimension, int value)
        {
            if (dimension == 0) place.accessInfrastructure = value;
            else if (dimension == 1) place.serviceInfrastructure = value;
            else place.civicInfrastructure = value;
            StartingSettingsChanged(place);
        }

        private void StartingSettingsChanged(CARegionalSettlementPlan place)
        {
            // Infrastructure and facilities are causes of settlement scale,
            // trade support, and provisions. Replace the prior realization
            // before any consumer reads the edited value.
            plan.confirmed = false;
            plan.operatorAuthored = true;
            CARegionalSettlements.Invalidate(plan);
            CARegionalSettlements.EnsureSettlementPattern(plan);
            CASettlementComposition.ReconcileStartingProvisions(plan, place);
            CARegionalSetupSession.SavePending();
        }

        private void FactionSettingsChanged(CARegionalFactionPlan faction)
        {
            // Political beliefs and current structure can change generated
            // facilities for this faction's settlements. Refresh the saved
            // realization at the edit boundary, then reconcile its provisions.
            plan.confirmed = false;
            plan.operatorAuthored = true;
            CARegionalSettlements.Invalidate(plan);
            CARegionalSettlements.EnsureSettlementPattern(plan);
            foreach (CARegionalSettlementPlan place in plan.settlements
                .Where(item => item != null
                    && item.factionKey == faction.key))
                CASettlementComposition.ReconcileStartingProvisions(plan,
                    place);
            CARegionalSetupSession.SavePending();
        }

        private string PhysicalSiteSummary(CARegionalSettlementPlan place)
        {
            List<CARegionalSettlementPlan> members = plan.settlements.Where(item =>
                    item != null && item.memberTileId == place.memberTileId
                    && item.PhysicalClusterKey == place.PhysicalClusterKey)
                .OrderBy(item => item.slot).ToList();
            return members.Count <= 1 ? "Independent settlement"
                : "One of " + members.Count + " districts in one complex";
        }

        private void OpenPhysicalSiteMenu(CARegionalSettlementPlan place)
        {
            var options = new List<CACreationChoice>
            {
                new CACreationChoice
                {
                    Key = "independent",
                    Name = "Independent settlement",
                    Summary = "This settlement occupies its own physical site.",
                    Traits = "No shared district complex",
                    Badge = "Physical site",
                    Icon = CACreationUI.Icon("Rimshare/WorldMapIcons/compass"),
                    Accent = CACreationUI.Authored,
                    Selected = plan.settlements.Count(item => item != null
                        && item != place
                        && item.memberTileId == place.memberTileId
                        && item.PhysicalClusterKey
                            == place.PhysicalClusterKey) == 0,
                    ConfirmLabel = "Use an independent site",
                    Choose = delegate { MakePhysicalSiteIndependent(place); }
                }
            };
            foreach (IGrouping<int, CARegionalSettlementPlan> cluster in plan.settlements
                .Where(item => item != null && item != place
                    && item.memberTileId == place.memberTileId)
                .GroupBy(item => item.PhysicalClusterKey)
                .OrderBy(item => item.Min(settlement => settlement.slot)))
            {
                int key = cluster.Key;
                string names = string.Join(", ", cluster
                    .OrderBy(item => item.slot)
                    .Select(item => CARegionalPlanUtility.SettlementName(plan,
                        item)).ToArray());
                options.Add(new CACreationChoice
                {
                    Key = "cluster:" + key,
                    Name = "District in an existing complex",
                    Summary = "Shares one physical site with " + names + ".",
                    Traits = "Distinct settlement · shared complex",
                    Badge = "Physical site",
                    Icon = CACreationUI.Icon(
                        "Rimshare/WorldMapIcons/divided-square"),
                    Accent = CACreationUI.Authored,
                    Selected = place.PhysicalClusterKey == key,
                    ConfirmLabel = "Join this complex",
                    Choose = delegate
                    {
                        place.siteClusterKey = key;
                        CARegionalSetupSession.SavePending();
                    }
                });
            }
            CACreationUI.OpenChoices("Physical site",
                "Choose whether this settlement occupies its own site or is "
                + "a district in an existing complex. Ownership and population "
                + "remain unchanged.", options);
        }

        private void MakePhysicalSiteIndependent(CARegionalSettlementPlan place)
        {
            if (place == null) return;
            int currentKey = place.PhysicalClusterKey;
            List<CARegionalSettlementPlan> remaining = plan.settlements.Where(item =>
                    item != null && item != place
                    && item.memberTileId == place.memberTileId
                    && item.PhysicalClusterKey == currentKey)
                .OrderBy(item => item.slot).ToList();
            if (remaining.Count > 0 && currentKey == place.slot)
            {
                int survivingKey = remaining[0].slot;
                foreach (CARegionalSettlementPlan member in remaining)
                    member.siteClusterKey = survivingKey;
            }
            place.siteClusterKey = place.slot;
            CARegionalSetupSession.SavePending();
        }

        private void OpenFactionMenu(CARegionalFactionPlan group)
        {
            var options = new List<CACreationChoice>();
            if (group.source == CARegionalFactionSource.ExistingWorldFaction)
            {
                var used = new HashSet<int>(plan.factions.Where(item =>
                        item != null && item != group && item.source
                            == CARegionalFactionSource.ExistingWorldFaction
                        && item.existingFactionLoadId >= 0)
                    .Select(item => item.existingFactionLoadId));
                foreach (Faction faction in CARegionalPlanUtility
                    .EligibleExistingFactions().Where(item =>
                        !used.Contains(item.loadID)))
                {
                    Faction local = faction;
                    options.Add(new CACreationChoice
                    {
                        Key = "existing:" + local.loadID,
                        Name = local.Name,
                        Summary = local.def.techLevel + " · "
                            + local.def.LabelCap,
                        Traits = local.PlayerRelationKind
                            + " toward the player",
                        Details = "Uses the existing faction's Ideoligion, "
                            + "knowledge, and world relations.",
                        Badge = "Existing faction",
                        Icon = local.def.FactionIcon,
                        Accent = CARegionalWorldOverlay.FactionColor(group.key),
                        Selected = group.existingFactionLoadId == local.loadID,
                        ConfirmLabel = "Use this faction",
                        Choose = delegate
                        {
                            group.existingFactionLoadId = local.loadID;
                            group.resolvedFaction = null;
                            group.resolvedFactionLoadId = -1;
                            CARegionalSetupSession.SavePending();
                        }
                    });
                }
            }
            else
                foreach (FactionDef def in CARegionalPlanUtility
                    .EligibleNewFactionDefs())
                {
                    FactionDef local = def;
                    options.Add(new CACreationChoice
                    {
                        Key = "new:" + local.defName,
                        Name = local.LabelCap.ToString(),
                        Summary = local.techLevel + " faction type",
                        Traits = "Beliefs, culture, structure, and relations editable",
                        Details = local.description.NullOrEmpty()
                            ? "Creates a new local faction of this type."
                            : local.description,
                        Badge = "New faction type",
                        Icon = local.FactionIcon,
                        Accent = CARegionalWorldOverlay.FactionColor(group.key),
                        Selected = group.customFactionDefName == local.defName,
                        ConfirmLabel = "Use this faction type",
                        Choose = delegate
                        {
                            group.customFactionDefName = local.defName;
                            group.resolvedFaction = null;
                            group.resolvedFactionLoadId = -1;
                            CARegionalSetupSession.SavePending();
                        }
                    });
                }
            if (options.Count == 0)
            {
                Messages.Message(group.source
                        == CARegionalFactionSource.ExistingWorldFaction
                        ? "No eligible existing faction is available."
                        : "No eligible faction type is available.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            CACreationUI.OpenChoices(group.source
                    == CARegionalFactionSource.ExistingWorldFaction
                    ? "Existing faction" : "Faction type",
                group.source == CARegionalFactionSource.ExistingWorldFaction
                    ? "Choose the world faction represented in this region."
                    : "Choose the native faction type used by this new local faction.",
                options);
        }

        private string RollFactionName(CARegionalFactionPlan group)
        {
            try
            {
                FactionDef def = group?.ResolvedFactionDef;
                var taken = plan.factions.Where(item => item != null
                        && item != group && !item.customName.NullOrEmpty())
                    .Select(item => item.customName);
                if (def?.factionNameMaker != null)
                    return NameGenerator.GenerateName(def.factionNameMaker,
                        taken, true);
                if (def != null) return def.LabelCap.ToString();
            }
            catch { }
            // Use the faction type when no name maker is available.
            return group?.ResolvedFactionDef?.LabelCap.ToString()
                ?? "Unnamed faction";
        }

        private string RollSettlementName(CARegionalSettlementPlan place)
        {
            try
            {
                FactionDef def = plan.FactionPlan(place.factionKey)
                    ?.ResolvedFactionDef;
                var taken = plan.settlements.Where(item => item != null
                        && item != place && !item.customName.NullOrEmpty())
                    .Select(item => item.customName);
                if (def?.settlementNameMaker != null)
                    return NameGenerator.GenerateName(
                        def.settlementNameMaker, taken, true);
            }
            catch { }
            return "Settlement " + (place.slot + 1);
        }

        // ---- diplomacy helpers ----------------------------------------------

        private static readonly FactionRelationKind[] RelationChoices =
        {
            FactionRelationKind.Neutral, FactionRelationKind.Hostile,
            FactionRelationKind.Ally
        };

        // Relations may use the faction default or an explicit setting.
        private void OpenPlayerRelationMenu(CARegionalFactionPlan group)
        {
            var options = new List<CACreationChoice>
            {
                new CACreationChoice
                {
                    Key = "default",
                    Name = "Faction default",
                    Summary = "Use the faction's existing or generated relation.",
                    Traits = "No Starting Region override",
                    Details = "The owning relation surface remains authoritative.",
                    Badge = !group.authorPlayerRelation ? "Current" : "Default",
                    Icon = RelationIcon(null),
                    Accent = CACreationUI.Generated,
                    Selected = !group.authorPlayerRelation,
                    ConfirmLabel = "Use faction default",
                    Choose = delegate
                    {
                        group.playerRelation = FactionRelationKind.Neutral;
                        group.authorPlayerRelation = false;
                        CARegionalSetupSession.SavePending();
                    }
                }
            };
            foreach (FactionRelationKind kind in RelationChoices)
            {
                FactionRelationKind local = kind;
                options.Add(new CACreationChoice
                {
                    Key = local.ToString(),
                    Name = RelationName(local),
                    Summary = RelationDescription(local, "the player faction"),
                    Traits = "Explicit Starting Region relation",
                    Badge = group.authorPlayerRelation
                            && group.playerRelation == local
                        ? "Current" : "Override",
                    Icon = RelationIcon(local),
                    Accent = RelationColor(local),
                    Selected = group.authorPlayerRelation
                        && group.playerRelation == local,
                    ConfirmLabel = "Set this relation",
                    Choose = delegate
                    {
                        group.playerRelation = local;
                        group.authorPlayerRelation = true;
                        CARegionalSetupSession.SavePending();
                    }
                });
            }
            CACreationUI.OpenChoices("Relation with the player",
                "Set the faction's starting relation with the player colony. "
                + "This does not alter relations with other factions.", options);
        }

        private void OpenPairRelationMenu(CARegionalFactionPlan left,
            CARegionalFactionPlan right, CARegionalRelationPlan pair)
        {
            bool authored = pair?.authorRelation == true;
            var options = new List<CACreationChoice>
            {
                new CACreationChoice
                {
                    Key = "default",
                    Name = "Generated relation",
                    Summary = "Use the saved faction-relation generation contract.",
                    Traits = "No Starting Region override",
                    Badge = !authored ? "Current" : "Default",
                    Icon = RelationIcon(null),
                    Accent = CACreationUI.Generated,
                    Selected = !authored,
                    ConfirmLabel = "Use generated relation",
                    Choose = delegate
                    {
                        plan.SetRelation(left.key, right.key,
                            FactionRelationKind.Neutral, false);
                        CARegionalSetupSession.SavePending();
                    }
                }
            };
            foreach (FactionRelationKind kind in RelationChoices)
            {
                FactionRelationKind local = kind;
                options.Add(new CACreationChoice
                {
                    Key = local.ToString(),
                    Name = RelationName(local),
                    Summary = RelationDescription(local,
                        CARegionalPlanUtility.FactionName(right)),
                    Traits = "Explicit Starting Region relation",
                    Badge = authored && pair.relation == local
                        ? "Current" : "Override",
                    Icon = RelationIcon(local),
                    Accent = RelationColor(local),
                    Selected = authored && pair.relation == local,
                    ConfirmLabel = "Set this relation",
                    Choose = delegate
                    {
                        plan.SetRelation(left.key, right.key, local, true);
                        CARegionalSetupSession.SavePending();
                    }
                });
            }
            CACreationUI.OpenChoices("Faction relation",
                "Set how " + CARegionalPlanUtility.FactionName(left)
                + " and " + CARegionalPlanUtility.FactionName(right)
                + " begin the game.", options);
        }

        private static string RelationName(FactionRelationKind kind)
        {
            if (kind == FactionRelationKind.Ally) return "Allied";
            if (kind == FactionRelationKind.Hostile) return "Hostile";
            return "Neutral";
        }

        private static string RelationDescription(FactionRelationKind kind,
            string other)
        {
            if (kind == FactionRelationKind.Ally)
                return "Begins allied with " + other + ".";
            if (kind == FactionRelationKind.Hostile)
                return "Begins hostile toward " + other + ".";
            return "Begins neither allied nor hostile with " + other + ".";
        }

        private static Texture2D RelationIcon(FactionRelationKind? kind)
        {
            string path = kind == FactionRelationKind.Hostile
                ? "Rimshare/WorldMapIcons/cracked-shield"
                : kind == FactionRelationKind.Ally
                    ? "Rimshare/WorldMapIcons/american-shield"
                    : kind == FactionRelationKind.Neutral
                        ? "Rimshare/WorldMapIcons/divided-square"
                        : "Rimshare/WorldMapIcons/compass";
            return CACreationUI.Icon(path);
        }

        private static Color RelationColor(FactionRelationKind kind)
        {
            if (kind == FactionRelationKind.Hostile)
                return new Color(0.86f, 0.36f, 0.32f);
            if (kind == FactionRelationKind.Ally)
                return new Color(0.42f, 0.78f, 0.48f);
            return CACreationUI.Generated;
        }

        private void OpenSourceMenu(CARegionalFactionPlan group)
        {
            bool existing = group.source
                == CARegionalFactionSource.ExistingWorldFaction;
            var options = new List<CACreationChoice>
            {
                new CACreationChoice
                {
                    Key = "existing",
                    Name = "Existing world faction",
                    Summary = "Represent a faction that already exists on the world map.",
                    Traits = "Keeps Ideoligion · knowledge · world relations",
                    Details = "Choose the exact faction after selecting this source.",
                    Badge = existing ? "Current" : "Faction source",
                    Icon = CACreationUI.Icon(
                        "Rimshare/WorldMapIcons/castle"),
                    Accent = CACreationUI.Accent,
                    Selected = existing,
                    ConfirmLabel = "Use an existing faction",
                    Choose = delegate
                    {
                        if (existing) return;
                        group.source =
                            CARegionalFactionSource.ExistingWorldFaction;
                        EnsureFactionChoice(group);
                        CARegionalSetupSession.SavePending();
                    }
                },
                new CACreationChoice
                {
                    Key = "new",
                    Name = "New local faction",
                    Summary = "Create a faction that begins in this region.",
                    Traits = "Editable culture · beliefs · structure · relations",
                    Details = "Choose its faction type, name, relations, and "
                        + "starting social order after selecting this source. "
                        + "RimWorld generates its Ideoligion from the faction "
                        + "type when the world starts.",
                    Badge = !existing ? "Current" : "Faction source",
                    Icon = CACreationUI.Icon(
                        "Rimshare/WorldMapIcons/forward-sun"),
                    Accent = CACreationUI.Authored,
                    Selected = !existing,
                    ConfirmLabel = "Create a local faction",
                    Choose = delegate
                    {
                        if (!existing) return;
                        group.source = CARegionalFactionSource.NewWorldFaction;
                        EnsureFactionChoice(group);
                        CARegionalSetupSession.SavePending();
                    }
                }
            };
            CACreationUI.OpenChoices("Faction source",
                "Choose whether this society already exists elsewhere in the "
                + "world or begins as a new local faction.", options);
        }

        private static string PlayerRelationLabel(
            CARegionalFactionPlan group)
        {
            if (group.authorPlayerRelation)
                return group.playerRelation.ToString();
            if (group.source == CARegionalFactionSource.ExistingWorldFaction)
            {
                Faction faction = CARegionalPlanUtility.FactionByLoadId(
                    group.existingFactionLoadId);
                return CARegionalPlanUtility.IsEligibleExistingFaction(
                        faction)
                    ? "native, " + faction.PlayerRelationKind
                    : "native";
            }
            Faction resolved = group.resolvedFaction;
            return CARegionalPlanUtility.MatchesFaction(resolved, group)
                ? "generated, " + resolved.PlayerRelationKind
                : "generated default";
        }

        private static string PlayerRelationTooltip(
            CARegionalFactionPlan group)
        {
            return group.authorPlayerRelation
                ? "Explicit relation. Select Use faction default to clear it."
                : "Faction default relation.";
        }

        private static string PairRelationLabel(
            CARegionalFactionPlan left,
            CARegionalFactionPlan right,
            CARegionalRelationPlan pair)
        {
            if (pair?.authorRelation == true) return pair.relation.ToString();
            FactionRelationKind native;
            string sourceDescription;
            return TryDefaultPairRelation(left, right, out native,
                    out sourceDescription)
                ? sourceDescription.ToLower() + ", " + native
                : "generated default";
        }

        private static string PairRelationTooltip(
            CARegionalFactionPlan left,
            CARegionalFactionPlan right,
            CARegionalRelationPlan pair)
        {
            if (FactionsShareWorldFaction(left, right))
                return "Both entries resolve to the same faction and cannot "
                    + "hold a separate relation.";
            if (pair?.authorRelation == true)
                return "Explicit relation. Select Use faction defaults to "
                    + "clear it.";
            return "Faction default relation.";
        }

        private static bool TryDefaultPairRelation(
            CARegionalFactionPlan left,
            CARegionalFactionPlan right,
            out FactionRelationKind relation, out string sourceDescription)
        {
            relation = FactionRelationKind.Neutral;
            sourceDescription = "Native";
            Faction leftFaction;
            Faction rightFaction;
            if (left.source == CARegionalFactionSource.ExistingWorldFaction
                && right.source
                    == CARegionalFactionSource.ExistingWorldFaction)
            {
                leftFaction = CARegionalPlanUtility.FactionByLoadId(
                    left.existingFactionLoadId);
                rightFaction = CARegionalPlanUtility.FactionByLoadId(
                    right.existingFactionLoadId);
                if (!CARegionalPlanUtility.IsEligibleExistingFaction(
                        leftFaction)
                    || !CARegionalPlanUtility.IsEligibleExistingFaction(
                        rightFaction)) return false;
            }
            else
            {
                leftFaction = left.resolvedFaction;
                rightFaction = right.resolvedFaction;
                sourceDescription = "Generated";
                if (!CARegionalPlanUtility.MatchesFaction(leftFaction,
                        left)
                    || !CARegionalPlanUtility.MatchesFaction(
                        rightFaction, right)) return false;
            }
            if (leftFaction == rightFaction)
            {
                relation = FactionRelationKind.Ally;
                sourceDescription = "Same faction";
                return true;
            }
            relation = leftFaction.RelationKindWith(rightFaction);
            return true;
        }

        private static bool FactionsShareWorldFaction(
            CARegionalFactionPlan left,
            CARegionalFactionPlan right)
        {
            if (left == null || right == null) return false;
            if (left.source == CARegionalFactionSource.ExistingWorldFaction
                && right.source
                    == CARegionalFactionSource.ExistingWorldFaction)
            {
                Faction leftFaction = CARegionalPlanUtility.FactionByLoadId(
                    left.existingFactionLoadId);
                Faction rightFaction = CARegionalPlanUtility.FactionByLoadId(
                    right.existingFactionLoadId);
                return leftFaction != null && leftFaction == rightFaction;
            }
            return left.resolvedFaction != null
                && left.resolvedFaction == right.resolvedFaction
                && CARegionalPlanUtility.MatchesFaction(
                    left.resolvedFaction, left)
                && CARegionalPlanUtility.MatchesFaction(
                    right.resolvedFaction, right);
        }

        // ---- primitives -----------------------------------------------------

        // A section title has more space above than below so it remains grouped
        // with its rows.
        private static void Title(ref float y, float width, string text)
        {
            if (y > 4f) y += 10f;
            Text.Font = GameFont.Medium;
            float height = Text.CalcHeight(text, width);
            Widgets.Label(new Rect(0f, y, width, height), text);
            Text.Font = GameFont.Small;
            y += height + 6f;
        }

        private static void Body(ref float y, float width, string text)
        {
            float height = Text.CalcHeight(text, width);
            Widgets.Label(new Rect(0f, y, width, height), text);
            y += height + Gap;
        }

        private static void Note(ref float y, float width, string text)
        {
            GUI.color = new Color(0.74f, 0.77f, 0.81f);
            float height = Text.CalcHeight(text, width);
            Widgets.Label(new Rect(0f, y, width, height), text);
            GUI.color = Color.white;
            y += height + Gap;
        }

        private static void Readout(ref float y, float width, string label,
            string value)
        {
            float height = Math.Max(Row - 6f,
                Text.CalcHeight(value, width - LabelWidth));
            Widgets.Label(new Rect(0f, y, LabelWidth - 6f, height), label);
            GUI.color = new Color(0.83f, 0.87f, 0.91f);
            // Values use sentence case through this shared renderer.
            Widgets.Label(new Rect(LabelWidth, y, width - LabelWidth,
                height), value.CapitalizeFirst());
            GUI.color = Color.white;
            y += height + 2f;
        }

        private static string Counted(int count, string singular,
            string plural = null)
        {
            return count + " " + (count == 1
                ? singular : plural ?? singular + "s");
        }

        private static void Rule(ref float y, float width)
        {
            y += 6f;
            Widgets.DrawLineHorizontal(0f, y, width);
            y += 10f;
        }

        private static void Back(ref float y, float width, string label)
        {
            if (Widgets.ButtonText(new Rect(0f, y, width, 26f),
                    "‹ " + label))
                CARegionMapWidget.SelectRegion();
            y += 32f;
        }

        // Object type and owning-faction link above the panel title.
        private static bool Kind(ref float y, float width, string words,
            Color chip)
        {
            Rect row = new Rect(0f, y, width, 18f);
            Widgets.DrawBoxSolid(new Rect(0f, y + 4f, 10f, 10f), chip);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.65f, 0.70f, 0.76f);
            Widgets.Label(new Rect(16f, y, width - 16f, 18f), words);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 20f;
            return Widgets.ButtonInvisible(row);
        }

        private static bool Entry(ref float y, float width, string title,
            string subtitle, Color chip)
        {
            bool hovered;
            return Entry(ref y, width, 0f, title, subtitle, chip,
                out hovered);
        }

        private static bool Entry(ref float y, float width, string title,
            string subtitle, Color chip, out bool hovered)
        {
            return Entry(ref y, width, 0f, title, subtitle, chip,
                out hovered);
        }

        // Inset rows show faction ownership of settlements.
        private static bool Entry(ref float y, float width, float inset,
            string title, string subtitle, Color chip, out bool hovered)
        {
            float textWidth = Mathf.Max(80f, width - inset - 16f);
            Text.Font = GameFont.Small;
            float titleHeight = Mathf.Max(20f, Text.CalcHeight(title,
                textWidth));
            Text.Font = GameFont.Tiny;
            float subtitleHeight = subtitle.NullOrEmpty() ? 0f
                : Text.CalcHeight(subtitle.CapitalizeFirst(), textWidth);
            Text.Font = GameFont.Small;
            float rowHeight = 6f + titleHeight
                + (subtitleHeight > 0f ? subtitleHeight + 2f : 0f) + 5f;
            Rect row = new Rect(inset, y, width - inset, rowHeight);
            hovered = Mouse.IsOver(row);
            if (hovered) Widgets.DrawHighlight(row);
            Widgets.DrawBoxSolid(new Rect(inset, y + 8f, 10f, 10f), chip);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inset + 16f, y + 1f,
                textWidth, titleHeight), title);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.74f, 0.77f, 0.81f);
            if (subtitleHeight > 0f)
                Widgets.Label(new Rect(inset + 16f,
                    y + titleHeight + 3f, textWidth, subtitleHeight),
                    subtitle.CapitalizeFirst());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += rowHeight + 2f;
            return Widgets.ButtonInvisible(row);
        }
    }
}
