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
    // structure. Settlements own local population, composition, infrastructure,
    // and provision arrangements. Inherited Culture and Ideoligion remain population
    // facts; local Culture records the history subsequently lived here.
    internal sealed class Page_CAStartingRegion : Page
    {
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
        private int compactPane = 1;
        private int lastInspectorSelection = int.MinValue;
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

        public override string PageTitle => "Starting Region";

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
                // Opening this page may inspect and validate, but it cannot
                // realize, normalize, or otherwise author the saved draft.
                if (plan.confirmed && !CARegionalPlanUtility
                    .TryValidateStartingSettlements(plan,
                        out string confirmedFailure))
                    Log.Error("[CA][Regional][StartingRegion] confirmed "
                        + "plan validation failed: " + confirmedFailure);
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
        }

        public override void DoWindowContents(Rect inRect)
        {
            DrawPageTitle(inRect);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 32f, inRect.width, 24f),
                "Set the starting region: factions, settlements, and "
                    + "populations on their actual ground.");

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
            layoutMode = CAStartingRegionLayoutHarness.ModeFor(inRect.width);
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
                        CARegionalSetupSession.SavePending,
                        delegate { compactPane = 2; });
                else DrawInspector(body);
            }
            else
            {
                float railWidth = CAStartingRegionLayoutHarness
                    .ObjectRailWidth(inRect.width, layoutMode);
                float panelWidth = CAStartingRegionLayoutHarness
                    .DetailsWidth(inRect.width, layoutMode);
                Rect rail = new Rect(0f, bodyTop, railWidth, bodyHeight);
                Rect panel = new Rect(inRect.width - panelWidth, bodyTop,
                    panelWidth, bodyHeight);
                Rect mapRect = new Rect(rail.xMax + paneGap, bodyTop,
                    Mathf.Max(440f, panel.x - rail.xMax - paneGap * 2f),
                    bodyHeight);
                DrawObjectRail(rail);
                CARegionMapWidget.Draw(mapRect, plan,
                    CARegionalSetupSession.SavePending, null);
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
            float viewWidth = navigation.width - 18f;
            float contentHeight = ObjectRailContentHeight(viewWidth);
            Rect view = new Rect(0f, 0f, viewWidth,
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

                string regionName = CARegionalPlanUtility.RegionName(plan);
                string regionSubtitle = plan.factions.Count + " faction"
                    + (plan.factions.Count == 1 ? "" : "s")
                    + " - " + plan.settlements.Count + " settlement"
                    + (plan.settlements.Count == 1 ? "" : "s");
                Text.Font = GameFont.Small;
                float regionTitleHeight = Text.CalcHeight(regionName,
                    view.width - 20f);
                Text.Font = GameFont.Tiny;
                float regionSubtitleHeight = Text.CalcHeight(regionSubtitle,
                    view.width - 20f);
                float regionHeight = Mathf.Max(50f, 5f + regionTitleHeight
                    + 2f + regionSubtitleHeight + 4f);
                Rect regionCard = new Rect(0f, y, view.width, regionHeight);
                if (CARegionMapWidget.selectedKind
                    == CARegionSelectionKind.Region)
                    Widgets.DrawHighlightSelected(regionCard);
                else if (Mouse.IsOver(regionCard))
                    Widgets.DrawHighlight(regionCard);
                Widgets.DrawBox(regionCard, 1);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(10f, y + 5f, view.width - 20f,
                    regionTitleHeight), regionName);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.72f, 0.76f, 0.81f);
                Widgets.Label(new Rect(10f, y + 7f + regionTitleHeight,
                    view.width - 20f, regionSubtitleHeight), regionSubtitle);
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(regionCard))
                {
                    CARegionMapWidget.SelectRegion();
                    OpenDetails();
                }
                y += regionHeight + 10f;

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
                    Text.Font = GameFont.Small;
                    string groupName = CARegionalPlanUtility
                        .FactionName(group);
                    float titleHeight = Text.CalcHeight(groupName,
                        view.width - 24f);
                    float groupHeight = Mathf.Max(42f,
                        7f + titleHeight + subtitleHeight + 4f);
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
                    Widgets.Label(new Rect(12f, y + 3f, view.width - 20f,
                        titleHeight), groupName);
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.72f, 0.76f, 0.81f);
                    Widgets.Label(new Rect(12f, y + 5f + titleHeight,
                        view.width - 20f, subtitleHeight), groupSubtitle);
                    GUI.color = Color.white;
                    if (Mouse.IsOver(groupCard))
                        CARegionMapWidget.emphasisFactionKey = group.key;
                    if (Widgets.ButtonInvisible(groupCard))
                    {
                        CARegionMapWidget.SelectFaction(group.key);
                        OpenDetails();
                    }
                    y += groupHeight + 4f;

                    foreach (CARegionalSettlementPlan place in plan.settlements.Where(
                            item => item != null
                                && item.factionKey == group.key)
                        .OrderBy(item => item.slot).ToList())
                    {
                        string placeName = CARegionalPlanUtility
                            .SettlementName(plan, place);
                        float placeHeight = Mathf.Max(30f,
                            Text.CalcHeight(placeName,
                                view.width - 32f) + 10f);
                        Rect placeRow = new Rect(14f, y,
                            view.width - 14f, placeHeight);
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
                            placeRow.y + 5f, placeRow.width - 18f,
                            placeHeight - 10f), placeName);
                        if (Mouse.IsOver(placeRow))
                            CARegionMapWidget.hoveredSlot = place.slot;
                        if (Widgets.ButtonInvisible(placeRow))
                        {
                            CARegionMapWidget.SelectSettlement(place.slot);
                            OpenDetails();
                        }
                        y += placeHeight + 2f;
                    }
                    y += 6f;
                }

                foreach (CARegionalSettlementPlan place in plan.settlements.Where(item =>
                        item != null && plan.FactionPlan(item.factionKey) == null)
                    .OrderBy(item => item.slot).ToList())
                {
                    string orphanLabel = CARegionalPlanUtility
                        .SettlementName(plan, place) + " - choose a faction";
                    Text.Font = GameFont.Tiny;
                    float orphanHeight = Mathf.Max(30f,
                        Text.CalcHeight(orphanLabel, view.width - 20f) + 10f);
                    Rect placeRow = new Rect(0f, y, view.width, orphanHeight);
                    Widgets.DrawBox(placeRow, 1);
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(10f, y + 5f,
                        view.width - 20f, orphanHeight - 10f), orphanLabel);
                    if (Widgets.ButtonInvisible(placeRow))
                    {
                        CARegionMapWidget.SelectSettlement(place.slot);
                        OpenDetails();
                    }
                    y += orphanHeight + 4f;
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

        private float ObjectRailContentHeight(float width)
        {
            float total = 22f;
            string regionName = CARegionalPlanUtility.RegionName(plan);
            string regionSubtitle = plan.factions.Count + " faction"
                + (plan.factions.Count == 1 ? "" : "s") + " - "
                + plan.settlements.Count + " settlement"
                + (plan.settlements.Count == 1 ? "" : "s");
            Text.Font = GameFont.Small;
            float regionTitle = Text.CalcHeight(regionName, width - 20f);
            Text.Font = GameFont.Tiny;
            float regionSub = Text.CalcHeight(regionSubtitle, width - 20f);
            total += Mathf.Max(50f, 11f + regionTitle + regionSub) + 32f;
            foreach (CARegionalFactionPlan group in plan.factions
                .Where(item => item != null).OrderBy(item => item.key))
            {
                int held = plan.settlements.Count(item => item != null
                    && item.factionKey == group.key);
                string subtitle = held + " settlement"
                    + (held == 1 ? "" : "s") + " - "
                    + CAFactionAxes.Characterize(plan, group);
                Text.Font = GameFont.Tiny;
                float subtitleHeight = Text.CalcHeight(subtitle, width - 24f);
                Text.Font = GameFont.Small;
                float titleHeight = Text.CalcHeight(
                    CARegionalPlanUtility.FactionName(group), width - 24f);
                total += Mathf.Max(42f,
                    11f + titleHeight + subtitleHeight) + 4f;
                Text.Font = GameFont.Tiny;
                foreach (CARegionalSettlementPlan place in plan.settlements
                    .Where(item => item != null
                        && item.factionKey == group.key))
                    total += Mathf.Max(30f, Text.CalcHeight(
                        CARegionalPlanUtility.SettlementName(plan, place),
                        width - 32f) + 10f) + 2f;
                total += 6f;
            }
            Text.Font = GameFont.Tiny;
            foreach (CARegionalSettlementPlan place in plan.settlements
                .Where(item => item != null
                    && plan.FactionPlan(item.factionKey) == null))
                total += Mathf.Max(30f, Text.CalcHeight(
                    CARegionalPlanUtility.SettlementName(plan, place)
                        + " - choose a faction", width - 20f) + 10f) + 4f;
            Text.Font = GameFont.Small;
            return total + 12f;
        }

        // ---- the inspector -------------------------------------------------

        private void DrawInspector(Rect panel)
        {
            Widgets.DrawMenuSection(panel);
            Rect outRect = panel.ContractedBy(8f);
            int selection = InspectorSelectionIdentity();
            if (selection != lastInspectorSelection)
            {
                scroll = Vector2.zero;
                measuredHeight = outRect.height;
                lastInspectorSelection = selection;
            }
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

        private void OpenDetails()
        {
            scroll = Vector2.zero;
            if (layoutMode == CARegionLayoutMode.Compact)
                compactPane = 2;
        }

        private int InspectorSelectionIdentity()
        {
            int identity = (int)CARegionMapWidget.selectedKind * 1000003;
            identity ^= CARegionMapWidget.selectedSlot * 101;
            identity ^= CARegionMapWidget.selectedFactionKey * 1009;
            identity ^= CARegionMapWidget.selectedTileId * 9176;
            return identity;
        }

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
            Title(ref y, width, "Factions and Settlements");
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
            Title(ref y, width, "Land and Access");
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
                Title(ref y, width, "Features and Old Sites");
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
                Title(ref y, width, "Regional Pattern");
                Readout(ref y, width, "Settlement pattern",
                    CARegionalSettlements.SettlementProfile(plan) + " - "
                    + plan.settlements.Count + " settlement"
                    + (plan.settlements.Count == 1 ? "" : "s"));
                Readout(ref y, width, "Regional relations",
                    CARegionalSettlements.RelationPatternWords(
                        (CARegionalRelationPattern)
                            plan.regionalRelationPattern));
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
                Title(ref y, width, "Around This Region");
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
            CARegionMapWidget.hoveredSlot = place.slot;

            if (layoutMode == CARegionLayoutMode.Compact)
                DrawSettlementComparison(ref y, width, place);
            // A settlement belongs to a faction. The overline opens that
            // faction without copying its controls onto this screen.
            if (Kind(ref y, width,
                    "Settlement - "
                    + CARegionalPlanUtility.FactionName(owner),
                    CARegionalWorldOverlay.FactionColor(
                        place.factionKey)) && owner != null)
                CARegionMapWidget.SelectFaction(place.factionKey);
            Title(ref y, width,
                CARegionalPlanUtility.SettlementName(plan, place));
            if (plan.settlementRealizationComplete)
            {
                Note(ref y, width,
                    CARegionalSettlements.RoleWords(
                        (CASettlementRole)place.realizedRole) + " · "
                    + CARegionalSettlements.ScaleWords(
                        (CASettlementScale)place.realizedScale) + " · "
                    + place.residentPopulation + " residents · "
                    + CARegionalPlanUtility.TileWords(place.memberTileId));
            }

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

            int siteChoiceCount = PhysicalSiteChoiceCount(place);
            if (CAContextualChoicePresentation.Resolve(siteChoiceCount,
                    essentialWhenFixed: false, blockingWhenEmpty: false)
                == CAContextualChoicePresentation.Mode.Control)
            {
                Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row),
                    "Site layout");
                Rect formRect = new Rect(LabelWidth, y,
                    width - LabelWidth, 28f);
                if (Widgets.ButtonText(formRect, PhysicalSiteSummary(place)))
                    OpenPhysicalSiteMenu(place);
                y += Row + Gap;
            }

            int originChoices = ReallocatableSettlements(place).Count + 1;
            if (CAContextualChoicePresentation.Resolve(originChoices,
                    essentialWhenFixed: false, blockingWhenEmpty: true)
                == CAContextualChoicePresentation.Mode.Control)
            {
                Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row),
                    "Population source");
                Rect originRect = new Rect(LabelWidth, y,
                    width - LabelWidth, 28f);
                if (place.populationOrigin == CASettlementOrigin.Unset)
                    GUI.color = new Color(1f, 0.72f, 0.5f);
                if (Widgets.ButtonText(originRect, OriginSummary(place)))
                    OpenOriginMenu(place);
                GUI.color = Color.white;
                y += Row + Gap;
            }

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
                string populationLabel = populationGroup.label + " · "
                    + PopulationGroupKindWords(populationGroup) + " · "
                    + populationGroup.CertaintyWords
                    + (populationGroup.ideoligionProtected
                        ? " · Ideoligion protected" : "");
                float populationHeight = Mathf.Max(28f, Text.CalcHeight(
                    populationLabel, width - LabelWidth - 12f) + 8f);
                Widgets.Label(new Rect(0f, y + 3f, LabelWidth,
                        populationHeight),
                    populationGroup.share + "%");
                Rect populationGroupRect = new Rect(LabelWidth, y,
                    width - LabelWidth, populationHeight);
                if (populationGroup.authored)
                    GUI.color = new Color(1f, 0.92f, 0.70f);
                if (Widgets.ButtonText(populationGroupRect, populationLabel))
                    OpenPopulationGroupMenu(place, populationGroup);
                GUI.color = Color.white;
                TooltipHandler.TipRegion(populationGroupRect, PopulationGroupTooltip(place,
                    populationGroup));
                y += populationHeight + 2f;
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

            Rule(ref y, width);
            Title(ref y, width, "Culture");
            Readout(ref y, width, "Local Culture",
                place.localCulture?.name ?? "Not recorded");
            Note(ref y, width, SettlementCultureSummary(place));
            if (Widgets.ButtonText(new Rect(0f, y, width, 28f),
                    "Compose local Culture..."))
                OpenSettlementCultureEditor(place);
            y += Row + Gap;

            Rule(ref y, width);
            Title(ref y, width, "Settlement programs");
            Note(ref y, width, CASettlementProgramRegistry.Summary(place));
            DrawSettlementProgramInspector(ref y, width, place);

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

        private void DrawSettlementComparison(ref float y, float width,
            CARegionalSettlementPlan current)
        {
            List<CARegionalSettlementPlan> ordered = plan.factions
                .Where(item => item != null).OrderBy(item => item.key)
                .SelectMany(faction => plan.settlements.Where(item =>
                        item != null && item.factionKey == faction.key)
                    .OrderBy(item => item.slot))
                .Concat(plan.settlements.Where(item => item != null
                        && plan.FactionPlan(item.factionKey) == null)
                    .OrderBy(item => item.slot)).ToList();
            int index = ordered.IndexOf(current);
            const float gap = 4f;
            float button = (width - gap * 3f) / 4f;
            string currentName = CARegionalPlanUtility.SettlementName(plan,
                current);
            Text.Font = GameFont.Small;
            float rowHeight = Mathf.Max(28f,
                Text.CalcHeight(currentName, button - 8f) + 8f);
            if (Widgets.ButtonText(new Rect(0f, y, button, rowHeight),
                    "Objects"))
                compactPane = 0;
            bool previous = index > 0;
            if (Widgets.ButtonText(new Rect(button + gap, y, button,
                    rowHeight),
                    "Previous", true, true, previous) && previous)
                CARegionMapWidget.SelectSettlement(ordered[index - 1].slot);
            GUI.color = CACreationUI.Accent;
            Widgets.DrawBox(new Rect((button + gap) * 2f, y, button,
                rowHeight), 1);
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect currentRect = new Rect((button + gap) * 2f, y, button,
                rowHeight);
            Widgets.Label(currentRect, currentName);
            TooltipHandler.TipRegion(currentRect, currentName + " ("
                + (index + 1) + " of " + ordered.Count + ")");
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            bool nextSettlement = index >= 0 && index + 1 < ordered.Count;
            if (Widgets.ButtonText(new Rect((button + gap) * 3f, y,
                    button, rowHeight), "Next", true, true, nextSettlement)
                && nextSettlement)
                CARegionMapWidget.SelectSettlement(ordered[index + 1].slot);
            y += rowHeight + 8f;
        }

        private void DrawSettlementProgramInspector(ref float y, float width,
            CARegionalSettlementPlan place)
        {
            List<CASettlementProgramAvailability> alternatives =
                CASettlementProgramRegistry.SupportedAlternatives(plan,
                    place).ToList();
            List<CASettlementProgramAvailability> unavailable =
                CASettlementProgramRegistry.Unavailable(plan, place).ToList();
            bool hasPresent = place?.settlementProgram?.entries?.Any(item =>
                item != null && item.blocker.NullOrEmpty()) == true;
            if (!hasPresent && alternatives.Count == 0
                && unavailable.Count == 0) return;
            if (Widgets.ButtonText(new Rect(0f, y, width, 28f),
                    "Inspect settlement composition..."))
                Verse.Find.WindowStack.Add(new Dialog_CASettlementProgram(
                    plan, place));
            y += Row + Gap;
        }

        private string SettlementCultureSummary(
            CARegionalSettlementPlan settlement)
        {
            CACulture culture = settlement?.localCulture;
            if (culture == null) return "Culture not recorded";
            int constituentCount = culture.constituents?.Count(item =>
                item != null && item.share > 0) ?? 0;
            List<CACultureQuestionDistribution> questions = CACultureModel
                .PopulationQuestions(culture).ToList();
            int meanings = questions.Count;
            int practices = (culture.inheritedPractices?.Count ?? 0)
                + (culture.practices?.Count ?? 0);
            int disputes = questions.Count(item => item != null
                && (item.spread >= 0.55f || (item.subgroups?.Any(group =>
                    group != null && Math.Abs(group.meanOffset) >= 0.20f)
                        == true)));
            return Counted(constituentCount, "cultural source") + " · "
                + Counted(meanings, "cultural question") + " · "
                + Counted(practices, "practice") + " · "
                + Counted(disputes, "broad or divided question");
        }

        private void OpenSettlementCultureEditor(
            CARegionalSettlementPlan place)
        {
            Ideo comparator = SettlementIdeoligionComparator(place,
                out CACultureIdeoligionComparison comparison);
            Verse.Find.WindowStack.Add(new Dialog_CACultureEditor(
                place.localCulture,
                CARegionalPlanUtility.SettlementName(plan, place),
                delegate
                {
                    place.localCulture.temporalBasis =
                        "Established before the scenario began.";
                    place.localCulture.maturity =
                        CACultureMaturity.Established;
                    place.localCulture.transitions.Clear();
                    CARegionalSetupSession.SavePending();
                }, CACultureAuthoringBoundary.EstablishedLocal,
                comparator, comparison));
        }

        private Ideo SettlementIdeoligionComparator(
            CARegionalSettlementPlan settlement,
            out CACultureIdeoligionComparison comparison)
        {
            comparison = CACultureIdeoligionComparison.None;
            if (settlement?.populationGroups == null) return null;
            var represented = new List<Ideo>();
            bool unresolved = false;
            bool withoutSingleDoctrine = false;
            bool pending = false;
            int groupCount = 0;
            foreach (CASettlementPopulationGroup group in settlement
                .populationGroups.Where(value => value != null
                    && value.share > 0))
            {
                groupCount++;
                Ideo value = null;
                if (group.nativeIdeoligionId >= 0)
                {
                    value = Find.IdeoManager?.IdeosListForReading?
                        .FirstOrDefault(ideo => ideo != null
                            && ideo.id == group.nativeIdeoligionId);
                    if (value == null) unresolved = true;
                }
                else
                {
                    int sourceKey = group.ideoligionFactionKey >= 0
                        ? group.ideoligionFactionKey : group.factionKey;
                    if (sourceKey < 0)
                        withoutSingleDoctrine = true;
                    else
                    {
                        CARegionalFactionPlan source = plan.FactionPlan(
                            sourceKey);
                        value = source?.LivingIdeo;
                        if (source == null)
                            unresolved = true;
                        else if (value == null && ModsConfig.IdeologyActive
                            && source.source == CARegionalFactionSource
                                .NewWorldFaction)
                            pending = true;
                        else if (value == null && ModsConfig.IdeologyActive
                            && source.source == CARegionalFactionSource
                                .ExistingWorldFaction
                            && CARegionalPlanUtility.FactionByLoadId(
                                source.existingFactionLoadId) == null)
                            unresolved = true;
                        else if (value == null)
                            withoutSingleDoctrine = true;
                    }
                }
                if (value == null || represented.Contains(value)) continue;
                represented.Add(value);
            }
            if (unresolved)
                comparison = CACultureIdeoligionComparison.Unresolved;
            else if (represented.Count > 1
                || (represented.Count > 0
                    && (withoutSingleDoctrine || pending)))
                comparison = CACultureIdeoligionComparison.Mixed;
            else if (groupCount == 0 || pending)
                comparison = CACultureIdeoligionComparison.Pending;
            else if (represented.Count == 0)
                comparison = CACultureIdeoligionComparison.None;
            else
                comparison = CACultureIdeoligionComparison.Single;
            return comparison == CACultureIdeoligionComparison.Single
                ? represented[0] : null;
        }

        private string SettlementRouteWords(
            CARegionalSettlementPlan place)
        {
            var routes = new List<string>();
            if (place.hasRoadAccess)
                routes.Add("road");
            if (place.hasRiverAccess)
                routes.Add("river");
            if (place.hasCoastalAccess)
                routes.Add("coast");
            return routes.Count == 0 ? "no major route on this area"
                : string.Join(", ", routes);
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
            int settlementCount = plan.settlements.Count(item => item != null
                && item.factionKey == group.key);
            Note(ref y, width, CAFactionAxes.Characterize(plan, group)
                + " · " + group.TechnologySummary + " · "
                + settlementCount + " settlement"
                + (settlementCount == 1 ? "" : "s"));

            int existingSources = CARegionalPlanUtility
                .EligibleExistingFactions().Count;
            int newSources = CARegionalPlanUtility
                .EligibleNewFactionDefs().Count;
            int factionSourceChoices = (existingSources > 0 ? 1 : 0)
                + (newSources > 0 ? 1 : 0);
            CAContextualChoicePresentation.Mode sourceMode =
                CAContextualChoicePresentation.Resolve(factionSourceChoices,
                    essentialWhenFixed: true, blockingWhenEmpty: true);
            if (sourceMode == CAContextualChoicePresentation.Mode.Control)
            {
                Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row), "Origin");
                Rect sourceRect = new Rect(LabelWidth, y,
                    width - LabelWidth, 28f);
                if (Widgets.ButtonText(sourceRect, group.source
                        == CARegionalFactionSource.ExistingWorldFaction
                        ? "Existing world faction" : "New faction"))
                    OpenSourceMenu(group);
                y += Row + Gap;
            }
            else if (sourceMode == CAContextualChoicePresentation.Mode.Readout)
                Readout(ref y, width, "Origin", existingSources > 0
                    ? "Existing world faction" : "New faction");
            else if (sourceMode == CAContextualChoicePresentation.Mode.Warning)
                Note(ref y, width, "No eligible faction source is available.");

            Widgets.Label(new Rect(0f, y + 3f, LabelWidth, Row),
                group.source == CARegionalFactionSource.ExistingWorldFaction
                    ? "Existing faction" : "Faction type");
            int factionChoices = group.source
                    == CARegionalFactionSource.ExistingWorldFaction
                ? CARegionalPlanUtility.EligibleExistingFactions().Count
                : CARegionalPlanUtility.EligibleNewFactionDefs().Count;
            Rect factionRect = new Rect(LabelWidth, y, width - LabelWidth,
                28f);
            if (factionChoices > 1)
            {
                if (Widgets.ButtonText(factionRect, group.Summary))
                    OpenFactionMenu(group);
            }
            else Widgets.Label(factionRect, group.Summary);
            TooltipHandler.TipRegion(factionRect, group.source
                == CARegionalFactionSource.ExistingWorldFaction
                ? "Existing knowledge, ideoligion, and settlement rules "
                    + "remain in force."
                : "The faction type supplies world integration, knowledge, "
                + "and settlement rules. Culture, Ideoligion, political "
                    + "beliefs, and current order "
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
            Title(ref y, width, "Population and Culture");
            Readout(ref y, width, "Culture",
                group.culture?.name ?? "Culture not recorded");
            Note(ref y, width, CACultureModel.Summary(group.culture));
            Rect cultureEdit = new Rect(0f, y, width, 28f);
            if (Widgets.ButtonText(cultureEdit, "Compose Culture..."))
                Verse.Find.WindowStack.Add(new Dialog_CACultureEditor(
                    group.culture, group.Summary,
                    CARegionalSetupSession.SavePending,
                    CACultureAuthoringBoundary.Inherited,
                    group.LivingIdeo,
                    !ModsConfig.IdeologyActive
                        ? CACultureIdeoligionComparison.None
                        : group.LivingIdeo != null
                            ? CACultureIdeoligionComparison.Single
                            : group.source == CARegionalFactionSource
                                .NewWorldFaction
                                ? CACultureIdeoligionComparison.Pending
                                : CACultureIdeoligionComparison.None));
            y += Row + Gap;
            Ideo livingIdeoligion = group.LivingIdeo;
            Readout(ref y, width, "Ideoligion", livingIdeoligion != null
                ? livingIdeoligion.name
                : ModsConfig.IdeologyActive
                    ? "Not yet named"
                    : "Inactive");

            Rule(ref y, width);
            Title(ref y, width, "Beliefs and Current Order");
            List<string> institutionalTensions =
                CAFactionStructureModel.Tensions(group.politicalBeliefs,
                    group.factionStructure);
            Note(ref y, width, CAPoliticalBeliefsModel.Summary(
                group.politicalBeliefs) + ". "
                + (institutionalTensions.Count == 0
                    ? "Current order aligns with every set belief."
                    : institutionalTensions.Count
                        + " preferred positions differ from current order."));
            Rect beliefsEdit = new Rect(0f, y, width, 28f);
            if (Widgets.ButtonText(beliefsEdit,
                    "Compare beliefs and current order..."))
                Verse.Find.WindowStack.Add(Dialog_CAAxisEditor.ForStructure(
                    group.factionStructure, group.politicalBeliefs,
                    (plan.candidateId ?? "ca") + ":faction:" + group.key,
                    () => FactionSettingsChanged(group)));
            y += Row + Gap;

            Rule(ref y, width);
            Title(ref y, width, "Settlement Authority");
            // Authority between settlements is independent of political
            // beliefs and the spatial settlement pattern.
            CASettlementAuthority authority =
                CARegionalSettlements.SettlementAuthorityOf(plan, group);
            string authorityWords = CARegionalSettlements
                .SettlementAuthorityWords(authority).CapitalizeFirst() + " · "
                + CARegionalSettlements.PresenceWords(plan, group);
            float authorityHeight;
            Rect authorityRect;
            if (width < 390f)
            {
                float labelHeight = Text.CalcHeight("Settlement authority",
                    width);
                Widgets.Label(new Rect(0f, y, width, labelHeight),
                    "Settlement authority");
                y += labelHeight + 2f;
                authorityHeight = ButtonRowHeight(authorityWords, width);
                authorityRect = new Rect(0f, y, width, authorityHeight);
            }
            else
            {
                float labelWidth = Mathf.Min(LabelWidth, width * 0.34f);
                authorityHeight = Mathf.Max(
                    Text.CalcHeight("Settlement authority", labelWidth - 6f),
                    ButtonRowHeight(authorityWords, width - labelWidth));
                Widgets.Label(new Rect(0f, y + 3f, labelWidth - 6f,
                    authorityHeight), "Settlement authority");
                authorityRect = new Rect(labelWidth, y,
                    width - labelWidth, authorityHeight);
            }
            if (group.settlementAuthorityExplicit)
                GUI.color = new Color(1f, 0.92f, 0.70f);
            if (Widgets.ButtonText(authorityRect, authorityWords))
                OpenSettlementAuthorityMenu(group);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(authorityRect, "Faction-wide "
                + "responsibility for decisions, taxation, justice, and "
                + "defense. " + CARegionalSettlements.Characterize(plan,
                    group));
            y += authorityHeight + Gap;

            Rule(ref y, width);
            Title(ref y, width, "Faction Relations");
            string playerWords = "Player faction: "
                + PlayerRelationLabel(group);
            float playerHeight = ButtonRowHeight(playerWords, width);
            Rect playerRect = new Rect(0f, y, width, playerHeight);
            if (Widgets.ButtonText(playerRect, playerWords))
                OpenPlayerRelationMenu(group);
            TooltipHandler.TipRegion(playerRect,
                PlayerRelationTooltip(group));
            y += playerHeight + Gap;
            string federationKind = group.federationKey < 0 ? null
                : plan.factions.Where(item => item != null
                        && item.federationKey == group.federationKey)
                    .Select(item => item.federationKind)
                    .FirstOrDefault(kind => !kind.NullOrEmpty()) ?? "defense";
            int federationChoices = 1 + plan.factions.Count(item =>
                item != null && item.key != group.key)
                + (group.federationKey >= 0 ? 4 : 0);
            string federationWords = "Federation: "
                + (federationKind.NullOrEmpty() ? "None"
                    : CASettlementAuthorityWriter
                        .FederationKindName(federationKind).CapitalizeFirst());
            float federationHeight = ButtonRowHeight(federationWords, width);
            Rect federationRect = new Rect(0f, y, width, federationHeight);
            if (federationChoices > 1 && Widgets.ButtonText(federationRect,
                    federationWords))
                OpenFederationMenu(group);
            else if (federationChoices == 1)
                Widgets.Label(federationRect, federationWords);
            TooltipHandler.TipRegion(federationRect,
                "Links independent factions and selects the responsibilities "
                + "they share.");
            y += federationHeight + Gap;
            foreach (CARegionalFactionPlan other in plan.factions
                .Where(item => item != null && item.key != group.key)
                .OrderBy(item => item.key).ToList())
            {
                CARegionalRelationPlan pair = plan.RelationPlanBetween(
                    group.key, other.key);
                string label = CARegionalPlanUtility.FactionName(other) + ": "
                    + PairRelationLabel(group, other, pair);
                float pairHeight = Mathf.Max(28f,
                    Text.CalcHeight(label, width - 12f) + 8f);
                Rect pairRect = new Rect(0f, y, width, pairHeight);
                if (FactionsShareWorldFaction(group, other))
                    Widgets.Label(pairRect, label);
                else if (Widgets.ButtonText(pairRect, label))
                    OpenPairRelationMenu(group, other, pair);
                TooltipHandler.TipRegion(pairRect,
                    PairRelationTooltip(group, other, pair));
                y += pairHeight + Gap;
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
            if (populationGroup.nativeIdeoligionId >= 0)
                ideoligionWords = Find.IdeoManager?.IdeosListForReading?
                    .FirstOrDefault(ideo => ideo != null
                        && ideo.id == populationGroup.nativeIdeoligionId)
                    ?.name ?? "Selected Ideoligion unavailable";
            else if (ideoligionSource?.LivingIdeo != null)
                ideoligionWords = ideoligionSource.LivingIdeo.name;
            else if (ideoligionSource != null && ModsConfig.IdeologyActive)
                ideoligionWords = "From "
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
                    PopulationCompositionChanged(place);
                }, populationGroup.kind == CAPopulationGroupKind.Main
                    ? (Action)null : delegate
                    {
                        place.populationGroups.Remove(populationGroup);
                        place.provisionArrangements.RemoveAll(item => item != null
                            && item.populationGroupKey == populationGroup.key);
                        NormalizePopulationShares(place);
                        PopulationCompositionChanged(place);
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
            PopulationCompositionChanged(place);
        }

        private void PopulationCompositionChanged(
            CARegionalSettlementPlan place)
        {
            if (place == null) return;
            CACultureHistory.EnsureSettlementCulture(plan, place);
            CARegionalSettlements.Invalidate(plan);
            CARegionalSettlements.EnsureSettlementPattern(plan);
            CASettlementComposition.ReconcileProvisionArrangements(plan, place);
            CARegionalSetupSession.SavePending();
        }

        private void OpenSettlementAuthorityMenu(CARegionalFactionPlan group)
        {
            var options = new List<CACreationChoice>();
            CASettlementAuthority current;
            bool hasCurrent = CARegionalSettlements.TrySettlementAuthorityOf(
                plan, group, out current);
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
                    Accent = CACreationUI.Authored,
                    Selected = hasCurrent && group.settlementAuthorityExplicit
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
                Key = "current-order",
                Name = "Follow current order",
                Summary = "Derive settlement authority from the faction's "
                    + "current structure.",
                Traits = hasCurrent ? "Current result: "
                    + CARegionalSettlements.SettlementAuthorityWords(current)
                    : "Current result: current order is incomplete",
                Badge = "Current order",
                Accent = CACreationUI.Generated,
                Selected = !group.settlementAuthorityExplicit,
                ConfirmLabel = "Follow current order",
                Choose = delegate
                {
                    group.settlementAuthorityExplicit = false;
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
            int protections = place.populationGroups.Count(c => c != null
                && c.ideoligionProtected);
            int ideoligions = place.populationGroups.Count(c => c != null
                && (c.ideoligionFactionKey >= 0 || c.nativeIdeoligionId >= 0));
            var parts = new List<string>();
            parts.Add(place.populationGroups.Count == 1 ? "1 population group"
                : place.populationGroups.Count + " population groups");
            if (minorities > 0)
                parts.Add(minorities + " minorit"
                    + (minorities == 1 ? "y" : "ies"));
            if (protections > 0)
                parts.Add(protections + " protected Ideoligion group"
                    + (protections == 1 ? "" : "s"));
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
        // structure, settlement pattern, then population groups. Provision
        // contracts remain absent until authored or observed operators exist.
        private void GenerateUnspecified()
        {
            bool patternFilled = !plan.settlementRealizationComplete;
            // Generated relations are realized first because faction defense
            // structure consumes the saved relation, not the tendency.
            CARegionalSettlements.EnsureSettlementPattern(plan);
            int politicalBeliefsFilled = 0;
            int structureFilled = 0;
            int cultureQuestionsFilled = 0;
            foreach (CARegionalFactionPlan group in plan.factions)
            {
                if (group == null) continue;
                group.EnsureCultureAndPolitics(plan);
                string seed = (plan.candidateId ?? "ca") + ":faction:"
                    + group.key;
                cultureQuestionsFilled += CACultureAuthoringKernel
                    .CompleteMissing(group.culture, seed + ":culture",
                        "starting-region generation");
                politicalBeliefsFilled += CAPoliticalBeliefsModel.DeriveUnset(
                    group.politicalBeliefs, seed + ":politics",
                    CAPoliticalContext.ForFaction(plan, group));
                structureFilled += CAFactionAxes.Derive(plan, group);
            }

            int populationGroupsFilled = 0;
            // Current order can change factual settlement context.
            // Recompute the still-unconfirmed realization; the fixed candidate
            // seed keeps every unrelated fact stable.
            CARegionalSettlements.Invalidate(plan);
            CARegionalSettlements.EnsureSettlementPattern(plan);
            foreach (CARegionalSettlementPlan place in plan.settlements)
            {
                if (place == null) continue;
                bool hadPopulationGroups = place.populationGroups != null
                    && place.populationGroups.Count > 0;
                CASettlementComposition.EnsureDerived(plan, place);
                if (!hadPopulationGroups && place.populationGroups.Count > 0)
                    populationGroupsFilled++;
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
            }
            CARegionalSetupSession.SavePending();
            var filled = new List<string>();
            if (populationGroupsFilled > 0)
                filled.Add(Counted(populationGroupsFilled,
                    "settlement population"));
            if (patternFilled) filled.Add("the regional pattern");
            if (politicalBeliefsFilled > 0)
                filled.Add(Counted(politicalBeliefsFilled,
                    "set of political beliefs"));
            if (cultureQuestionsFilled > 0)
                filled.Add(Counted(cultureQuestionsFilled,
                    "Culture question"));
            if (structureFilled > 0)
                filled.Add(Counted(structureFilled,
                    "set of faction rules"));
            if (originsFilled > 0)
                filled.Add(Counted(originsFilled, "population origin") + " "
                    + "from nearby world settlements");
            Messages.Message((filled.Count == 0
                ? "All settings are already set."
                : "Set: " + string.Join(", ", filled.ToArray())
                    + ". Explicit settings remained unchanged.")
                + (originsShort > 0
                    ? " " + Counted(originsShort, "settlement")
                        + (originsShort == 1
                            ? " still needs a " : " still need a ")
                        + "population source. Select Scenario population or "
                        + "remove them."
                    : ""),
                MessageTypeDefOf.NeutralEvent, false);
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
            parts.Add(structureOpen == 0 ? "current order set"
                : structureOpen + " current-order subjects unset");
            return string.Join(" · ", parts.ToArray());
        }

        // These descriptions read the same composition state as generation.
        // When an outcome depends on generated pawns, they describe the rule.

        private static string AxisWord(CARegionalFactionPlan group,
            string axisKey)
        {
            IReadOnlyList<CAAxisOption> options = CAFactionAxes.OptionsOf(
                group.factionStructure, axisKey);
            return options.Count == 0 ? null : string.Join(" + ",
                options.Select(option => option.Label));
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
            int protections = 0;
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
                    if (populationGroup.ideoligionProtected) protections++;
                    if (populationGroup.nativeIdeoligionId >= 0)
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
            if (protections > 0)
                parts.Add(protections + " protected Ideoligion group"
                    + (protections == 1 ? "" : "s"));
            if (factionIdeoligions > 0)
                parts.Add(factionIdeoligions + " faction-sourced Ideoligion"
                    + (factionIdeoligions == 1 ? "" : "s"));
            if (independentIdeoligions > 0)
                parts.Add(independentIdeoligions + " independent Ideoligion"
                    + (independentIdeoligions == 1 ? "" : "s"));
            return string.Join(" · ", parts.ToArray());
        }

        // Concrete region state shown before the plan becomes world state.
        private string FinalRepresentation(string blocker)
        {
            var text = new System.Text.StringBuilder();
            var tensions = new List<string>();
            foreach (CARegionalFactionPlan group in plan.factions)
            {
                if (group == null) continue;
                group.EnsureCultureAndPolitics(plan);
                foreach (string tension in CAFactionStructureModel.Tensions(
                    group.politicalBeliefs, group.factionStructure))
                    tensions.Add(CARegionalPlanUtility.FactionName(group)
                        + ": " + tension);
            }
            int unsetOrigins = plan.settlements.Count(b => b != null
                && b.populationOrigin == CASettlementOrigin.Unset);

            text.AppendLine((plan.regionName ?? "Starting region") + ": "
                + Counted(plan.factions.Count(item => item != null),
                    "faction") + ", "
                + Counted(plan.settlements.Count(item => item != null),
                    "settlement"));
            text.AppendLine("Arrival: "
                + CARegionalPlanUtility.TileWords(plan.startTileId));
            text.AppendLine();
            text.AppendLine("Factions:");
            foreach (CARegionalFactionPlan group in plan.factions
                .Where(item => item != null).OrderBy(item => item.key))
            {
                string ideoligion = group.LivingIdeo?.name
                    ?? (ModsConfig.IdeologyActive
                        ? "Ideoligion not yet named" : "Ideoligion inactive");
                text.AppendLine("  "
                    + CARegionalPlanUtility.FactionName(group)
                    + " — Culture: "
                    + (group.culture?.name ?? "not recorded")
                    + "; Ideoligion: " + ideoligion + ".");
                text.AppendLine("    Political beliefs: "
                    + CAPoliticalBeliefsModel.Summary(
                        group.politicalBeliefs) + ". Current order: "
                    + CAFactionAxes.Characterize(plan, group) + ".");
            }
            text.AppendLine();
            text.AppendLine("Settlements:");
            foreach (CARegionalSettlementPlan place in plan.settlements
                .Where(item => item != null).OrderBy(item => item.slot))
            {
                CARegionalFactionPlan owner = plan.FactionPlan(
                    place.factionKey);
                string population = string.Join(", ",
                    (place.populationGroups
                        ?? new List<CASettlementPopulationGroup>())
                    .Where(item => item != null && item.share > 0)
                    .OrderByDescending(item => item.share)
                    .Select(item => item.share + "% "
                        + (item.label ?? "unnamed population"))
                    .ToArray());
                if (population.NullOrEmpty()) population = "not set";
                string provisions = string.Join(", ",
                    (place.provisionArrangements
                        ?? new List<CAProvisionArrangement>())
                    .Where(item => item != null && item.active)
                    .Select(item => item.basisLabel.NullOrEmpty()
                        ? "provision arrangement" : item.basisLabel)
                    .Distinct().ToArray());
                if (provisions.NullOrEmpty()) provisions = "none recorded";
                text.AppendLine("  "
                    + CARegionalPlanUtility.SettlementName(plan, place)
                    + " — " + CARegionalPlanUtility.FactionName(owner)
                    + "; " + CARegionalPlanUtility.TileWords(
                        place.memberTileId) + ".");
                text.AppendLine("    Population: " + population + ".");
                text.AppendLine("    Culture: "
                    + (place.localCulture?.name ?? "not recorded") + ".");
                text.AppendLine("    Composition: "
                    + CASettlementProgramRegistry.Summary(place)
                    + "; routes: " + SettlementRouteWords(place)
                    + "; provision arrangements: " + provisions + ".");
                text.AppendLine("    Population source: "
                    + OriginSummary(place) + ".");
            }
            if (tensions.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Political beliefs and current faction rules differ:");
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
                    return "Scenario population — additional population";
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
                Traits = "Added for this scenario",
                Details = "This authoring choice adds population instead of "
                    + "moving it from an existing world settlement.",
                Badge = "Population source",
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
                    Summary = "Settlements belong to different world factions.",
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
                    Summary = "Settlements belong to factions formed here.",
                    Traits = "Up to 2 factions · up to " + sources
                        + " settlements",
                    Details = common + " Faction types, beliefs, and structure "
                        + "remain editable after generation.",
                    Badge = "Region template",
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

        private void FactionSettingsChanged(CARegionalFactionPlan faction)
        {
            // Political beliefs and current order can change generated
            // programs for this faction's settlements. Refresh the saved
            // realization at the edit boundary, then reconcile its provisions.
            plan.confirmed = false;
            plan.operatorAuthored = true;
            CARegionalSettlements.Invalidate(plan);
            CARegionalSettlements.EnsureSettlementPattern(plan);
            foreach (CARegionalSettlementPlan place in plan.settlements
                .Where(item => item != null
                    && item.factionKey == faction.key))
                CASettlementComposition.ReconcileProvisionArrangements(plan,
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

        private int PhysicalSiteChoiceCount(CARegionalSettlementPlan place)
        {
            return 1 + plan.settlements.Where(item => item != null
                    && item != place && item.memberTileId == place.memberTileId)
                .Select(item => item.PhysicalClusterKey).Distinct().Count();
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
                    Name = "Use existing relation",
                    Summary = "Keep the relation this faction already holds.",
                    Traits = "Faction relation",
                    Details = null,
                    Badge = !group.authorPlayerRelation ? "Current" : null,
                    Icon = RelationIcon(null),
                    Accent = CACreationUI.Generated,
                    Selected = !group.authorPlayerRelation,
                    ConfirmLabel = "Keep existing relation",
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
                    Traits = "Starting relation",
                    Badge = group.authorPlayerRelation
                            && group.playerRelation == local
                        ? "Current" : null,
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
                    Name = "Use existing relation",
                    Summary = "Use the relation already held by these factions.",
                    Badge = !authored ? "Current" : null,
                    Icon = RelationIcon(null),
                    Accent = CACreationUI.Generated,
                    Selected = !authored,
                    ConfirmLabel = "Keep existing relation",
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
                    Traits = "Starting relation",
                    Badge = authored && pair.relation == local
                        ? "Current" : null,
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
                    ? faction.PlayerRelationKind.ToString()
                    : "Neutral";
            }
            Faction resolved = group.resolvedFaction;
            return CARegionalPlanUtility.MatchesFaction(resolved, group)
                ? resolved.PlayerRelationKind.ToString()
                : FactionRelationKind.Neutral.ToString();
        }

        private static string PlayerRelationTooltip(
            CARegionalFactionPlan group)
        {
            return group.authorPlayerRelation
                ? "Starting relation set for this region."
                : "Relation already held by this faction.";
        }

        private static string PairRelationLabel(
            CARegionalFactionPlan left,
            CARegionalFactionPlan right,
            CARegionalRelationPlan pair)
        {
            if (FactionsShareWorldFaction(left, right))
                return "Same World Faction";
            if (pair?.authorRelation == true) return pair.relation.ToString();
            FactionRelationKind native;
            string sourceDescription;
            return TryDefaultPairRelation(left, right, out native,
                    out sourceDescription)
                ? native.ToString()
                : FactionRelationKind.Neutral.ToString();
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
                return "Starting relation set for this region.";
            return "Relation already held by these factions.";
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
            string shown = value.NullOrEmpty() ? "Not recorded" : value;
            if (width < 390f)
            {
                float labelHeight = Text.CalcHeight(label, width);
                Widgets.Label(new Rect(0f, y, width, labelHeight), label);
                y += labelHeight + 1f;
                GUI.color = new Color(0.83f, 0.87f, 0.91f);
                float valueHeight = Text.CalcHeight(shown, width);
                Widgets.Label(new Rect(0f, y, width, valueHeight), shown);
                GUI.color = Color.white;
                y += valueHeight + 4f;
                return;
            }
            float labelWidth = Mathf.Min(LabelWidth, width * 0.34f);
            float valueWidth = width - labelWidth;
            float height = Math.Max(Row - 6f, Math.Max(
                Text.CalcHeight(label, labelWidth - 6f),
                Text.CalcHeight(shown, valueWidth)));
            Widgets.Label(new Rect(0f, y, labelWidth - 6f, height), label);
            GUI.color = new Color(0.83f, 0.87f, 0.91f);
            Widgets.Label(new Rect(labelWidth, y, valueWidth, height), shown);
            GUI.color = Color.white;
            y += height + 2f;
        }

        private static float ButtonRowHeight(string text, float width)
        {
            return Mathf.Max(28f, Text.CalcHeight(text ?? "",
                Mathf.Max(24f, width - 12f)) + 8f);
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
            Text.Font = GameFont.Tiny;
            float height = Mathf.Max(18f, Text.CalcHeight(words,
                width - 16f));
            Rect row = new Rect(0f, y, width, height);
            Widgets.DrawBoxSolid(new Rect(0f, y + 4f, 10f, 10f), chip);
            GUI.color = new Color(0.65f, 0.70f, 0.76f);
            Widgets.Label(new Rect(16f, y, width - 16f, height), words);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += height + 2f;
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
