using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // TWO FIRST-CLASS GEOGRAPHIC VIEWS, and no third abstraction between
    // them.
    //
    //   1. The world map itself is the macrogeographic authoring surface.
    //      CA does not cover or replace it. Composing and choosing arrival
    //      are performed by pointing at actual geography: the tile under
    //      the cursor answers with its own hex outline in a semantic color
    //      and a short statement of what a click will do. No grids of
    //      boxed widgets - the geography is the control.
    //   2. The generated regional preview (Map Preview's engine, CAO's
    //      presentation) is docked beside the column: the real map at the
    //      exact composed backing, zoomable and inspectable, regenerating
    //      whenever composition changes.
    //
    // Region composition is a layer across those two views:
    //   world geography + selected membership -> canonical region
    //     -> generated regional preview -> actual regional map.
    // Arrival stays separately owned: TrySetArrival, which can never
    // regenerate, reframe, or redefine the region.
    internal enum CALandingMode
    {
        Inspect,
        Compose,
        Arrival
    }

    internal static class CAOpeningAugments
    {
        internal static CALandingMode Mode = CALandingMode.Inspect;
        private static float columnMeasuredHeight = 560f;
        private static Page_SelectStartingSite page;
        private static MethodInfo canDoBack;
        private static MethodInfo doBack;
        private static MethodInfo canDoNext;
        private static MethodInfo doNext;
        private static readonly List<Vector3> tileVertices =
            new List<Vector3>();

        // While a plan exists on the landing page, CAO owns the page
        // chrome: the stock title, beveled bottom row, tutorial arrow,
        // world-object gizmo strip and the vanilla inspect pane give way
        // to the column and the on-map interactions. The globe itself -
        // terrain, icons, feature names, selection - stays native.
        internal static bool OwnsPageChrome
        {
            get
            {
                if (CARegionalSetupSession.EditingDialogOpen) return false;
                if (CARegionalSetupSession.PendingForCurrentWorld == null)
                    return false;
                return Verse.Find.WindowStack
                    ?.WindowOfType<Page_SelectStartingSite>() != null;
            }
        }

        internal static bool BeginPageFrame(
            Page_SelectStartingSite instance)
        {
            page = instance;
            return OwnsPageChrome;
        }

        internal static void DrawLanding()
        {
            try
            {
                DrawLandingCore();
            }
            catch (Exception failure)
            {
                Log.ErrorOnce("[CA][Landing] augment draw failed; the "
                    + "stock page remains fully usable: " + failure,
                    889417233);
            }
        }

        private static void DrawLandingCore()
        {
            CAExpandedLandmassProfile profile;
            if (!CAExpandedLandmassProfile.TryFor(
                    Verse.Find.GameInitData.mapSize, out profile)) return;
            if (CARegionalSetupSession.EditingDialogOpen) return;
            CARegionalPlan plan =
                CARegionalSetupSession.PendingForCurrentWorld;
            if (plan == null)
            {
                Mode = CALandingMode.Inspect;
                return;
            }

            // Arrival mode keeps the poller's landing-anchor flow armed so
            // a plain click on any member area works through the same
            // owner; leaving the mode disarms it.
            CARegionalSetupSession.ChoosingLandingAnchor =
                Mode == CALandingMode.Arrival;

            // The page's pass sees events before the windows stacked above
            // it. When the cursor is over any other window - the docked
            // preview, a dialog, the column itself is drawn later but the
            // globe interactions must not fire under it either - the globe
            // interaction stands down so clicks reach what they aim at.
            bool pointerFree = PointerFreeForGlobe();
            if (pointerFree && Mode == CALandingMode.Compose)
                DrawComposeInteraction(plan, profile);
            else if (pointerFree && Mode == CALandingMode.Arrival)
                DrawArrivalInteraction(plan);
            DrawArrivalMarker(plan);

            Rect column = DrawColumn(plan, profile);
            CARegionalPreviewDock.Arrange(column);
        }

        // ---- projecting real geography to the screen ------------------------

        private static Rect lastColumnRect;

        private static bool PointerFreeForGlobe()
        {
            Vector2 mouse = Event.current.mousePosition;
            if (lastColumnRect.width > 0f
                && lastColumnRect.Contains(mouse)) return false;
            List<Window> windows = Verse.Find.WindowStack.Windows.ToList();
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                Window window = windows[i];
                if (window == null
                    || window is Page_SelectStartingSite) continue;
                if (window.windowRect.Contains(mouse)) return false;
            }
            return true;
        }

        private static Vector2? TileScreenPos(PlanetTile tile)
        {
            if (!tile.Valid) return null;
            Vector3 center = Verse.Find.WorldGrid.GetTileCenter(tile);
            Vector3 cam = Verse.Find.WorldCamera.transform.position;
            if (Vector3.Dot(center.normalized,
                    (cam - center).normalized) <= 0.05f) return null;
            Vector3 screen =
                Verse.Find.WorldCamera.WorldToScreenPoint(center)
                / Prefs.UIScale;
            if (screen.z <= 0f) return null;
            return new Vector2(screen.x,
                (float)UI.screenHeight - screen.y);
        }

        private static float TileScreenRadius(PlanetTile tile,
            Vector2 center)
        {
            tileVertices.Clear();
            Verse.Find.WorldGrid.GetTileVertices(tile, tileVertices);
            if (tileVertices.Count == 0) return 24f;
            Vector3 screen = Verse.Find.WorldCamera.WorldToScreenPoint(
                tileVertices[0]) / Prefs.UIScale;
            var vertex = new Vector2(screen.x,
                (float)UI.screenHeight - screen.y);
            return Mathf.Max(10f, Vector2.Distance(center, vertex) * 0.9f);
        }

        // The tile's own hex outline, drawn on the world. This is the
        // interaction feedback: geography answering, not a widget.
        private static void DrawTileOutline(PlanetTile tile, Color color,
            float thickness)
        {
            tileVertices.Clear();
            Verse.Find.WorldGrid.GetTileVertices(tile, tileVertices);
            if (tileVertices.Count < 3) return;
            Vector3 cam = Verse.Find.WorldCamera.transform.position;
            var points = new List<Vector2>(tileVertices.Count);
            foreach (Vector3 vertex in tileVertices)
            {
                if (Vector3.Dot(vertex.normalized,
                        (cam - vertex).normalized) <= 0.02f) return;
                Vector3 screen = Verse.Find.WorldCamera
                    .WorldToScreenPoint(vertex) / Prefs.UIScale;
                if (screen.z <= 0f) return;
                points.Add(new Vector2(screen.x,
                    (float)UI.screenHeight - screen.y));
            }
            for (int i = 0; i < points.Count; i++)
                Widgets.DrawLine(points[i],
                    points[(i + 1) % points.Count], color, thickness);
        }

        internal static void CursorStatement(string text, Color tone)
        {
            Vector2 mouse = Event.current.mousePosition;
            Text.Font = GameFont.Tiny;
            Vector2 size = Text.CalcSize(text);
            var label = new Rect(mouse.x + 14f, mouse.y + 10f,
                size.x + 12f, 18f);
            label.x = Mathf.Min(label.x, UI.screenWidth - label.width - 4f);
            label.y = Mathf.Min(label.y, UI.screenHeight - 22f);
            Widgets.DrawBoxSolid(label,
                new Color(0.05f, 0.06f, 0.07f, 0.88f));
            Widgets.DrawBoxSolid(new Rect(label.x, label.y, 2f,
                label.height), tone);
            GUI.color = CAOpeningTheme.TextHi;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(label, text);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private static bool ClickedNow()
        {
            return Event.current.type == EventType.MouseDown
                && Event.current.button == 0;
        }

        // ---- compose: pointing at geography ---------------------------------

        internal static readonly Color AddTone =
            new Color(0.42f, 0.78f, 0.50f);
        internal static readonly Color RemoveTone =
            new Color(0.82f, 0.38f, 0.33f);
        internal static readonly Color BlockedTone =
            new Color(0.55f, 0.58f, 0.60f);

        private static void DrawComposeInteraction(CARegionalPlan plan,
            CAExpandedLandmassProfile profile)
        {
            var candidates = new Dictionary<int, PlanetTile>();
            var neighbors = new List<PlanetTile>();
            foreach (int memberId in plan.memberTileIds)
            {
                PlanetTile member =
                    CARegionalPlanUtility.SurfaceTile(memberId);
                if (!member.Valid) continue;
                neighbors.Clear();
                member.Layer.GetTileNeighbors(member, neighbors);
                foreach (PlanetTile neighbor in neighbors)
                    if (neighbor.Valid
                        && !plan.memberTileIds.Contains(neighbor.tileId))
                        candidates[neighbor.tileId] = neighbor;
            }

            Vector2 mouse = Event.current.mousePosition;
            int hoveredId = -1;
            bool hoveredMember = false;
            float hoveredDistance = float.MaxValue;
            float hoveredRadius = 24f;
            PlanetTile hoveredTile = PlanetTile.Invalid;

            // Claimable geography shows itself while the tool is active: a
            // quiet outline on every bordering area, in its true place.
            foreach (KeyValuePair<int, PlanetTile> candidate in candidates)
            {
                Vector2? at = TileScreenPos(candidate.Value);
                if (at == null) continue;
                bool blocked =
                    CARegionalGeometry.IsBlocked(candidate.Value);
                Color faint = blocked
                    ? new Color(BlockedTone.r, BlockedTone.g,
                        BlockedTone.b, 0.20f)
                    : new Color(AddTone.r, AddTone.g, AddTone.b, 0.30f);
                DrawTileOutline(candidate.Value, faint, 1.4f);
                float radius = TileScreenRadius(candidate.Value, at.Value);
                float distance = Vector2.Distance(mouse, at.Value);
                if (distance < radius && distance < hoveredDistance)
                {
                    hoveredDistance = distance;
                    hoveredId = candidate.Key;
                    hoveredMember = false;
                    hoveredRadius = radius;
                    hoveredTile = candidate.Value;
                }
            }
            foreach (int memberId in plan.memberTileIds)
            {
                PlanetTile member =
                    CARegionalPlanUtility.SurfaceTile(memberId);
                Vector2? at = TileScreenPos(member);
                if (at == null) continue;
                float radius = TileScreenRadius(member, at.Value);
                float distance = Vector2.Distance(mouse, at.Value);
                if (distance < radius && distance < hoveredDistance)
                {
                    hoveredDistance = distance;
                    hoveredId = memberId;
                    hoveredMember = true;
                    hoveredRadius = radius;
                    hoveredTile = member;
                }
            }

            if (hoveredId < 0 || !hoveredTile.Valid) return;
            if (hoveredMember)
            {
                DrawTileOutline(hoveredTile, RemoveTone, 2.6f);
                CursorStatement("Release "
                    + (hoveredTile.Tile?.PrimaryBiome?.label ?? "this area"),
                    RemoveTone);
                if (ClickedNow())
                {
                    CARegionalSetupSession.ToggleRegionArea(profile, plan,
                        hoveredTile);
                    Event.current.Use();
                }
            }
            else if (CARegionalGeometry.IsBlocked(hoveredTile))
            {
                DrawTileOutline(hoveredTile, BlockedTone, 2.2f);
                CursorStatement((hoveredTile.Tile?.WaterCovered == true
                    ? "Open sea cannot join"
                    : "Impassable ground cannot join"), BlockedTone);
                if (ClickedNow()) Event.current.Use();
            }
            else
            {
                DrawTileOutline(hoveredTile, AddTone, 2.6f);
                CursorStatement("Claim "
                    + (hoveredTile.Tile?.PrimaryBiome?.label ?? "this area"),
                    AddTone);
                if (ClickedNow())
                {
                    CARegionalSetupSession.ToggleRegionArea(profile, plan,
                        hoveredTile);
                    Event.current.Use();
                }
            }
        }

        // ---- arrival: pointing at the region --------------------------------

        private static void DrawArrivalInteraction(CARegionalPlan plan)
        {
            // Valid arrival geography exposes itself: every member wears a
            // quiet outline - green can host the arrival, red cannot - and
            // the hovered one states why.
            Vector2 mouse = Event.current.mousePosition;
            int hoveredId = -1;
            float hoveredDistance = float.MaxValue;
            PlanetTile hoveredTile = PlanetTile.Invalid;
            string hoveredRefusal = null;

            foreach (int memberId in plan.memberTileIds)
            {
                if (memberId == plan.startTileId) continue;
                PlanetTile member =
                    CARegionalPlanUtility.SurfaceTile(memberId);
                Vector2? at = TileScreenPos(member);
                if (at == null) continue;
                string refusal = CARegionalSetupSession.ArrivalRefusalFor(
                    plan, memberId);
                Color faint = refusal == null
                    ? new Color(AddTone.r, AddTone.g, AddTone.b, 0.34f)
                    : new Color(RemoveTone.r, RemoveTone.g, RemoveTone.b,
                        0.26f);
                DrawTileOutline(member, faint, 1.4f);
                float radius = TileScreenRadius(member, at.Value);
                float distance = Vector2.Distance(mouse, at.Value);
                if (distance < radius && distance < hoveredDistance)
                {
                    hoveredDistance = distance;
                    hoveredId = memberId;
                    hoveredTile = member;
                    hoveredRefusal = refusal;
                }
            }

            if (hoveredId < 0 || !hoveredTile.Valid) return;
            if (hoveredRefusal == null)
            {
                DrawTileOutline(hoveredTile, AddTone, 2.6f);
                CursorStatement("Arrive here", AddTone);
                if (ClickedNow())
                {
                    if (CARegionalSetupSession.TrySetArrival(plan,
                            hoveredId, out string refusal))
                        Messages.Message("Arrival area: "
                            + CARegionalPlanUtility.TileSummary(hoveredId),
                            MessageTypeDefOf.TaskCompletion, false);
                    else
                        Messages.Message(refusal,
                            MessageTypeDefOf.RejectInput, false);
                    Event.current.Use();
                }
            }
            else
            {
                DrawTileOutline(hoveredTile, RemoveTone, 2.2f);
                string reason = hoveredRefusal.Length > 46
                    ? hoveredRefusal.Substring(0, 44) + "..."
                    : hoveredRefusal;
                CursorStatement(reason, RemoveTone);
                TooltipHandler.TipRegion(
                    new Rect(mouse.x - 40f, mouse.y - 40f, 80f, 80f),
                    hoveredRefusal);
                if (ClickedNow()) Event.current.Use();
            }
        }

        private static void DrawArrivalMarker(CARegionalPlan plan)
        {
            Vector2? at = TileScreenPos(plan.StartTile);
            if (at == null) return;
            Text.Font = GameFont.Tiny;
            Vector2 size = Text.CalcSize("Arrival");
            var label = new Rect(at.Value.x - (size.x + 10f) * 0.5f,
                at.Value.y - 8f, size.x + 10f, 16f);
            Widgets.DrawBoxSolid(label,
                new Color(0.05f, 0.06f, 0.07f, 0.85f));
            CAOpeningTheme.Border(label, CAOpeningTheme.Accent);
            GUI.color = CAOpeningTheme.AccentHover;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(label, "Arrival");
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(label, "Where the arriving party "
                + "enters this region. Use Choose arrival to change it; "
                + "the region itself never changes with it.");
        }

        // ---- the column -----------------------------------------------------

        private static Rect DrawColumn(CARegionalPlan plan,
            CAExpandedLandmassProfile profile)
        {
            CARegionalSetupSession.CALandingFacts landing =
                CARegionalSetupSession.GatherLandingFacts(plan);
            const float width = 336f;
            float height = Mathf.Min(columnMeasuredHeight,
                UI.screenHeight - 72f - 24f);
            var column = new Rect(UI.screenWidth - width - 12f, 72f,
                width, height);
            lastColumnRect = column;
            CAOpeningTheme.SurfacePanel(column);
            Rect inner = column.ContractedBy(CAOpeningTheme.Pad);
            float w = inner.width;
            float x = inner.x;
            float y = inner.y;

            y += CAOpeningTheme.Heading(x, y, w,
                CARegionalPlanUtility.RegionName(plan));
            y += CAOpeningTheme.Fine(x, y, w, landing.actualAreas
                + " connected areas · " + landing.biomes) + 8f;

            float half = (w - 6f) / 2f;
            bool composing = Mode == CALandingMode.Compose;
            bool arriving = Mode == CALandingMode.Arrival;
            if (CAOpeningTheme.Chip(new Rect(x, y, half, 32f),
                    composing ? "Composing..." : "Compose areas",
                    composing,
                    "Point at the world: bordering areas outline "
                    + "themselves and a click claims them; pointing at a "
                    + "member releases it. Shift-click works at any time."))
                Mode = composing ? CALandingMode.Inspect
                    : CALandingMode.Compose;
            if (CAOpeningTheme.Chip(new Rect(x + half + 6f, y, half, 32f),
                    arriving ? "Choosing..." : "Choose arrival", arriving,
                    "Point at the region: green ground can host the "
                    + "arrival, red states why not. Never changes the "
                    + "region itself."))
                Mode = arriving ? CALandingMode.Inspect
                    : CALandingMode.Arrival;
            y += 32f + 6f;

            if (composing)
            {
                float chipX = x;
                foreach (int preset in
                    CARegionalGeographyComposition.SupportedExtents)
                {
                    var chip = new Rect(chipX, y, 44f, 28f);
                    if (CAOpeningTheme.Chip(chip, preset.ToString(),
                            landing.requestedAreas == preset,
                            "Seek " + preset + " connected areas from "
                            + "the anchor."))
                        CARegionalSetupSession.ChangeRegionExtentTo(
                            profile, plan, preset);
                    chipX += 48f;
                }
                if (CAOpeningTheme.GhostButton(
                        new Rect(chipX + 2f, y, w - (chipX - x) - 2f, 28f),
                        "Turn", "Turn the preset footprint one step "
                        + "around the anchor."))
                    CARegionalSetupSession.RotateRegionFootprint(profile,
                        plan);
                y += 28f + 4f;
            }

            // THE INSPECTED AREA. Clicking a member on the world selects
            // it; the column answers with that area's own ground - the
            // per-area inspection the suppressed vanilla pane used to
            // approximate, in the region's own terms.
            PlanetTile inspected = Verse.Find.WorldInterface.SelectedTile;
            if (inspected.Valid && inspected.Tile != null
                && plan.memberTileIds.Contains(inspected.tileId))
            {
                y += CAOpeningTheme.SectionLabel(x, y, w,
                    CARegionalPlanUtility.TileWords(inspected.tileId)
                    + (inspected.tileId == plan.startTileId
                        ? " · arrival" : ""));
                Tile info = inspected.Tile;
                y += CAOpeningTheme.Row(x, y, w, "Biome",
                    info.PrimaryBiome?.label ?? "unknown") + 3f;
                y += CAOpeningTheme.Row(x, y, w, "Relief",
                    info.hilliness.ToString().ToLower()
                        .Replace("largehills", "hilly")
                        .Replace("smallhills", "gentle hills")
                        .Replace("mountainous", "mountains")) + 3f;
                string rocks = string.Join(", ",
                    Verse.Find.World.NaturalRockTypesIn(inspected)
                        .Select(rock => rock.label));
                y += CAOpeningTheme.Row(x, y, w, "Stone",
                    rocks.NullOrEmpty() ? "none" : rocks) + 3f;
                string marks = string.Join(", ",
                    (info.Mutators ?? Enumerable.Empty<TileMutatorDef>())
                    .Where(def => def != null)
                    .Select(def => def.label ?? def.defName));
                if (!marks.NullOrEmpty())
                {
                    y += CAOpeningTheme.Row(x, y, w, "Features", marks)
                        + 3f;
                    if ((info.Mutators
                        ?? Enumerable.Empty<TileMutatorDef>())
                        .Any(CAFeatureShapeModel.Shapeable))
                    {
                        if (CAOpeningTheme.GhostButton(new Rect(x, y, w,
                                28f), "Shape features...",
                                "Bend this area's features within what "
                                + "they can naturally be. The preview and "
                                + "the real map both generate from the "
                                + "authored shape; untouched features stay "
                                + "exactly as found."))
                            Verse.Find.WindowStack.Add(
                                new Dialog_CAFeatureShapeEditor(plan,
                                    inspected.tileId));
                        y += 31f;
                    }
                }
                CARegionalSettlementPlan occupant = plan.settlements
                    ?.FirstOrDefault(item => item != null
                        && item.memberTileId == inspected.tileId);
                if (occupant != null)
                    y += CAOpeningTheme.Row(x, y, w, "Settlement",
                        CARegionalPlanUtility.SettlementName(plan,
                            occupant)) + 3f;
                else if (inspected.tileId != plan.startTileId)
                {
                    string refusal = CARegionalSetupSession
                        .ArrivalRefusalFor(plan, inspected.tileId);
                    y += CAOpeningTheme.Fine(x, y, w, refusal == null
                        ? "Can host the arrival."
                        : refusal) + 3f;
                }
                y += 5f;
            }

            y += CAOpeningTheme.Row(x, y, w, "Ground", landing.relief)
                + 3f;
            y += CAOpeningTheme.Row(x, y, w, "Stone",
                landing.geology.NullOrEmpty() ? "none here"
                    : landing.geology) + 3f;
            if (!landing.features.NullOrEmpty())
                y += CAOpeningTheme.Row(x, y, w, "Features",
                    landing.features) + 3f;
            y += CAOpeningTheme.Row(x, y, w, "Water", landing.surveyText)
                + 3f;
            float generationY = y;
            float generationH = CAOpeningTheme.Row(x, y, w, "Generation",
                landing.geography.GenerationWords().CapitalizeFirst());
            TooltipHandler.TipRegion(new Rect(x, generationY, w,
                generationH), landing.geography.Tooltip());
            y += generationH + 8f;

            if (landing.actualAreas < landing.requestedAreas)
                y += CAOpeningTheme.Notice(x, y, w, "Connected usable "
                    + "land ends at " + landing.actualAreas + " of "
                    + landing.requestedAreas + " requested areas.",
                    CAOpeningTheme.Warn) + 6f;
            if (landing.claimedSea + landing.claimedImpassable > 0)
                y += CAOpeningTheme.Notice(x, y, w, "Inside the outline "
                    + "but unusable: "
                    + (landing.claimedSea > 0 ? landing.claimedSea
                        + " sea area"
                        + (landing.claimedSea == 1 ? "" : "s") : "")
                    + (landing.claimedSea > 0
                        && landing.claimedImpassable > 0 ? ", " : "")
                    + (landing.claimedImpassable > 0
                        ? landing.claimedImpassable
                            + " impassable mountain area"
                            + (landing.claimedImpassable == 1 ? "" : "s")
                        : "")
                    + " - projected as sea or mountain mass.",
                    CAOpeningTheme.Warn) + 6f;
            if (!landing.reservable)
                y += CAOpeningTheme.Notice(x, y, w, "This land is not "
                    + "available: " + (landing.reservationFailure
                        ?? "something already holds it"),
                    CAOpeningTheme.Danger) + 6f;

            // Relocation is standing behavior, not a mode: say so where
            // the other standing facts live.
            y += CAOpeningTheme.Fine(x, y, w, landing.noteText
                + " Click elsewhere on the world to move the whole "
                + "region.") + 10f;

            // Progression belongs to the same surface as the work: the
            // stock beveled bottom row is suppressed while CAO owns this
            // page, and these are the only operations it carried that
            // matter here.
            CAOpeningTheme.Divider(x, y, w);
            y += 7f;
            float ghostHalf = (w - 6f) / 2f;
            if (CAOpeningTheme.GhostButton(new Rect(x, y, ghostHalf, 30f),
                    "World options",
                    "Local area scale and other advanced settings."))
            {
                try
                {
                    Verse.Find.WindowStack.Add(
                        new Dialog_AdvancedGameConfig());
                }
                catch (Exception failure)
                {
                    Log.Warning("[CA][Workspace] could not open advanced "
                        + "settings: " + failure);
                }
            }
            if (CAOpeningTheme.GhostButton(new Rect(x + ghostHalf + 6f, y,
                    ghostHalf, 30f), "Random site",
                    "Select a random valid starting tile. A designed "
                    + "region asks before it moves."))
            {
                Verse.Find.WorldInterface.SelectedTile =
                    TileFinder.RandomStartingTile();
                Verse.Find.WorldCameraDriver.JumpTo(
                    Verse.Find.WorldGrid.GetTileCenter(
                        Verse.Find.WorldInterface.SelectedTile));
            }
            y += 30f + 6f;
            var back = new Rect(x, y, 96f, 36f);
            var next = new Rect(x + 96f + 6f, y, w - 96f - 6f, 36f);
            if (CAOpeningTheme.GhostButton(back, "Back")) NavigateBack();
            if (CAOpeningTheme.PrimaryButton(next, "Continue"))
                NavigateNext();
            y += 36f;

            columnMeasuredHeight = (y - column.y) + CAOpeningTheme.Pad
                + 4f;
            return column;
        }

        private static void EnsureNavigation()
        {
            if (canDoNext != null) return;
            canDoBack = AccessTools.Method(typeof(Page), "CanDoBack");
            doBack = AccessTools.Method(typeof(Page), "DoBack");
            canDoNext = AccessTools.Method(typeof(Page_SelectStartingSite),
                "CanDoNext");
            doNext = AccessTools.Method(typeof(Page_SelectStartingSite),
                "DoNext");
        }

        private static void NavigateBack()
        {
            EnsureNavigation();
            if (page == null || canDoBack == null || doBack == null)
                return;
            if ((bool)canDoBack.Invoke(page, null))
                doBack.Invoke(page, null);
        }

        private static void NavigateNext()
        {
            EnsureNavigation();
            if (page == null || canDoNext == null || doNext == null)
                return;
            if ((bool)canDoNext.Invoke(page, null))
                doNext.Invoke(page, null);
        }
    }

    // The world-object gizmo strip and the stock inspect pane duplicate,
    // in vanilla layout, what CAO's column and interactions state. They
    // are withheld only while CAO owns the landing page; everywhere else
    // they behave normally. The pane is removed, not blanked, so no empty
    // window is left behind.
    [HarmonyPatch(typeof(WorldGizmoUtility),
        nameof(WorldGizmoUtility.WorldUIOnGUI))]
    internal static class CALandingGizmoPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !CAOpeningAugments.OwnsPageChrome;
        }
    }

    [HarmonyPatch(typeof(WorldInterface), "CheckOpenOrCloseInspectPane")]
    internal static class CALandingInspectPanePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(WorldInterface __instance)
        {
            if (!CAOpeningAugments.OwnsPageChrome) return true;
            if (Verse.Find.WindowStack.IsOpen<WorldInspectPane>())
                Verse.Find.WindowStack.TryRemove(__instance.inspectPane,
                    doCloseSound: false);
            return false;
        }
    }
}
