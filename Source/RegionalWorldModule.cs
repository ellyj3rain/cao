using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonistAwareness
{
    // RimWorld assigns DefOf fields through reflection during initialization.
#pragma warning disable CS0649
    [DefOf]
    internal static class CARegionalDefOf
    {
        public static GenStepDef CA_RegionalProjection;
        public static GenStepDef CA_RegionalWorldLinks;
        public static GenStepDef CA_RegionalSettlements;
        public static GenStepDef CA_RegionalSettlementPower;
        public static GenStepDef CA_RegionalPlayerStart;
        public static WorldObjectDef CA_RegionalMemberReservation;
        public static StatDef CA_DarkVisionEfficiency;

        static CARegionalDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CARegionalDefOf));
        }
    }

    public sealed class WorldObject_CARegionalMemberReservation : WorldObject
    {
        public string regionalId;
        public int anchorTileId = -1;
        public int mapSize;
        public int memberIndex = -1;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref regionalId, "regionalId");
            Scribe_Values.Look(ref anchorTileId, "anchorTileId", -1);
            Scribe_Values.Look(ref mapSize, "mapSize", 0);
            Scribe_Values.Look(ref memberIndex, "memberIndex", -1);
        }

        public override void Draw()
        {
            // The reservation participates in native world-object occupancy
            // checks but deliberately has no world-map icon.
        }
    }

    [HarmonyPatch(typeof(GenWorldUI), nameof(GenWorldUI.WorldObjectsUnderMouse))]
    internal static class CARegionalReservationWorldUiPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref List<WorldObject> __result)
        {
            __result?.RemoveAll(item =>
                item is WorldObject_CARegionalMemberReservation);
        }
    }

    internal readonly struct CAExpandedLandmassProfile
    {
        internal static readonly int[] SupportedLocalMapSizes =
            { 200, 225, 250, 275, 300, 325, 350, 400, 500 };

        internal readonly int Size;
        internal readonly int DefaultRegionTileCount;
        internal readonly string Label;

        internal CAExpandedLandmassProfile(int size,
            int defaultRegionTileCount, string label)
        {
            Size = size;
            DefaultRegionTileCount = defaultRegionTileCount;
            Label = label;
        }

        internal IntVec3 BackingMapSize(int regionTileCount)
        {
            int count = Math.Max(1, regionTileCount);
            int side = Mathf.CeilToInt(Size * Mathf.Sqrt(count));
            return new IntVec3(side, 1, side);
        }

        internal static bool TryFor(IntVec3 size,
            out CAExpandedLandmassProfile profile)
        {
            profile = default(CAExpandedLandmassProfile);
            if (size.x != size.z) return false;
            return TryFor(size.x, out profile);
        }

        internal static bool TryFor(int size,
            out CAExpandedLandmassProfile profile)
        {
            profile = default(CAExpandedLandmassProfile);
            if (!SupportedLocalMapSizes.Contains(size)) return false;
            profile = new CAExpandedLandmassProfile(size, 4,
                "regional " + size + "-cell source scale");
            return true;
        }
    }

    internal static class CARegionalCompatibility
    {
        // Map Preview allocates its texture when the window opens, before a
        // starting tile has established the regional backing dimensions.  A
        // nominal 8-tile square is sufficient for that short-lived initial
        // allocation; PreviewWindowTileSelectedPrefix then reinitializes the
        // same texture to the exact geometry-fit request before generation.
        private const int RegionalPreviewInitialDimension = 1415;
        private static bool sizesInstalled;
        private static bool footprintLayerInstalled;
        private static bool previewInstalled;
        private static bool previewInstallDeferred;
        private static Delegate previousPreviewSizeOverride;
        private static Type previewApiType;
        private static PropertyInfo previewGeneratingProperty;
        private static FieldInfo previewGeneratingField;
        private static MethodInfo previewRefreshMethod;
        private static MethodInfo previewDetermineMapSizeMethod;
        private static FieldInfo previewMaxMapSizeField;
        private static FieldInfo previewSizeOverrideField;
        private static FieldInfo previewWidgetField;
        private static PropertyInfo previewTextureProperty;
        private static FieldInfo previewTexCoordsField;
        private static PropertyInfo previewMapProperty;
        private static string lastPreviewRequestSignature;

        internal static void TryInstall()
        {
            TryInstallMapSizes();
            TryInstallFootprintLayer();
            TryInstallMapPreviewCompatibility();
        }

        private static void TryInstallFootprintLayer()
        {
            if (footprintLayerInstalled) return;
            try
            {
                // This installer also runs from the Mod constructor, before
                // DefOf initialization.  Resolve directly from the database so
                // an early verification can defer without touching the DefOf
                // static constructor; the scheduled post-load pass will find
                // Surface once defs are ready.
                PlanetLayerDef surface = DefDatabase<PlanetLayerDef>
                    .GetNamedSilentFail("Surface");
                List<Type> layers = surface?.worldDrawLayers;
                if (layers == null)
                {
                    Log.Message("[CA][Regional] persistent regional footprint "
                        + "layer verification deferred until Surface Def "
                        + "injection");
                    return;
                }
                bool declared = layers.Contains(
                    typeof(WorldDrawLayer_CARegionalFootprint));
                if (!declared)
                {
                    int selected = layers.IndexOf(
                        typeof(WorldDrawLayer_SelectedTile));
                    layers.Insert(selected >= 0 ? selected + 1 : layers.Count,
                        typeof(WorldDrawLayer_CARegionalFootprint));
                }
                // The whole-partition layer sits under the footprint
                // treatment: structure first, selection and registration
                // drawn over it.
                if (!layers.Contains(
                        typeof(WorldDrawLayer_CARegionalPartition)))
                    layers.Insert(layers.IndexOf(
                            typeof(WorldDrawLayer_CARegionalFootprint)),
                        typeof(WorldDrawLayer_CARegionalPartition));
                footprintLayerInstalled = true;
                Log.Message("[CA][Regional] persistent regional footprint "
                    + "world layer " + (declared
                        ? "verified from Surface Def"
                        : "installed by runtime fallback"));
            }
            catch (Exception ex)
            {
                Log.Warning("[CA][Regional] could not install persistent "
                    + "regional footprint layer: " + ex.GetType().Name
                    + ": " + ex.Message);
            }
        }

        // Restore the supported map-size list before drawing advanced setup.
        // Unexpected values are logged before replacement.
        internal static void ReassertMapSizes()
        {
            try
            {
                FieldInfo ordinary = AccessTools.Field(
                    typeof(Dialog_AdvancedGameConfig), "MapSizes");
                int[] current = ordinary?.GetValue(null) as int[];
                int[] supported = CAExpandedLandmassProfile
                    .SupportedLocalMapSizes;
                if (current != null && current.SequenceEqual(supported))
                    return;
                Log.Warning("[CA][Regional] advanced map-size list "
                    + "deviated to [" + (current == null ? "null"
                        : string.Join(", ", current))
                    + "]; reasserting the supported catalog");
                sizesInstalled = false;
                TryInstallMapSizes();
            }
            catch (Exception) { }
        }

        private static void TryInstallMapSizes()
        {
            if (sizesInstalled) return;
            try
            {
                FieldInfo ordinary = AccessTools.Field(
                    typeof(Dialog_AdvancedGameConfig), "MapSizes");
                FieldInfo tests = AccessTools.Field(
                    typeof(Dialog_AdvancedGameConfig), "TestMapSizes");
                if (ordinary == null || tests == null)
                    throw new MissingFieldException(
                        "Dialog_AdvancedGameConfig map-size fields");
                int[] supported = CAExpandedLandmassProfile
                    .SupportedLocalMapSizes;
                ordinary.SetValue(null, supported.ToArray());
                tests.SetValue(null, Array.Empty<int>());
                int[] installed = ordinary.GetValue(null) as int[];
                sizesInstalled = installed != null
                    && installed.SequenceEqual(supported);
                if (sizesInstalled)
                    Log.Message("[CA][Regional] advanced map sizes installed: "
                        + string.Join(", ", supported)
                        + "; each is a local source scale independent of "
                        + "regional extent");
                else
                    Log.Warning("[CA][Regional] advanced map-size installation "
                        + "did not survive field verification");
            }
            catch (Exception ex)
            {
                Log.Warning("[CA][Regional] could not extend the advanced "
                    + "map-size selector: " + ex.GetType().Name + ": "
                    + ex.Message);
            }
        }

        private static void TryInstallMapPreviewCompatibility()
        {
            if (previewInstalled) return;
            // AwarenessMod is constructed on RimWorld's play-load worker.
            // Map Preview's toolbar static constructor reads DefOf state and
            // loads textures, so touching it there permanently poisons the
            // type. ModEntry already schedules this installer on the main
            // thread; wait for that call and for DefOf binding to complete.
            if (ModsConfig.IsActive("m00nl1ght.mappreview")
                && (!UnityData.IsInMainThread
                    || OptionCategoryDefOf.General == null))
            {
                if (!previewInstallDeferred)
                {
                    previewInstallDeferred = true;
                    Log.Message("[CA][Regional] Map Preview compatibility "
                        + "deferred until main-thread DefOf initialization");
                }
                return;
            }
            try
            {
                Type sizeType = FindType("MapPreview.MapSizeUtility");
                previewApiType = FindType("MapPreview.MapPreviewAPI");
                if (sizeType == null && previewApiType == null) return;

                if (sizeType != null)
                {
                    previewMaxMapSizeField = AccessTools.Field(sizeType,
                        "MaxMapSize");
                    if (previewMaxMapSizeField != null)
                    {
                        IntVec2 existing = previewMaxMapSizeField.GetValue(null)
                            is IntVec2 value
                            ? value
                            : new IntVec2(0, 0);
                        previewMaxMapSizeField.SetValue(null, new IntVec2(
                            Math.Max(existing.x,
                                RegionalPreviewInitialDimension),
                            Math.Max(existing.z,
                                RegionalPreviewInitialDimension)));
                    }
                    // Map Preview 1.6 names this GameInitMapSizeOverride. Keep
                    // the old name as a narrow fallback for earlier releases,
                    // but never report the current hook healthy unless one of
                    // the actual fields was found.
                    previewSizeOverrideField = AccessTools.Field(sizeType,
                            "GameInitMapSizeOverride")
                        ?? AccessTools.Field(sizeType, "MapSizeOverride");
                    if (previewSizeOverrideField != null)
                    {
                        previousPreviewSizeOverride = previewSizeOverrideField
                            .GetValue(null) as Delegate;
                        Delegate regionalOverride = Delegate.CreateDelegate(
                            previewSizeOverrideField.FieldType,
                            AccessTools.Method(
                                typeof(CARegionalCompatibility),
                                nameof(PreviewMapSizeOverride)));
                        previewSizeOverrideField.SetValue(null,
                            regionalOverride);
                    }
                }
                if (previewApiType != null)
                {
                    previewGeneratingProperty = AccessTools.Property(
                        previewApiType, "IsGeneratingPreview");
                    previewGeneratingField = AccessTools.Field(
                        previewApiType, "IsGeneratingPreview");
                }
                Type requestType = FindType("MapPreview.MapPreviewRequest");
                MethodInfo addPredicate = AccessTools.Method(requestType,
                    "AddDefaultGenStepPredicate");
                addPredicate?.Invoke(null, new object[]
                {
                    new Predicate<GenStepDef>(IsRegionalPreviewStep)
                });

                var previewHarmony = new Harmony(
                    "ellyj3rain.colonistawareness.regionalpreview");
                Type generatorType = FindType("MapPreview.MapPreviewGenerator");
                MethodInfo collect = AccessTools.Method(generatorType,
                    "CollectGenStepsForTile");
                if (collect != null)
                    previewHarmony.Patch(collect, postfix: new HarmonyMethod(
                        typeof(CARegionalCompatibility),
                        nameof(PreviewCollectGenStepsPostfix)));
                MethodInfo constructMinimal = AccessTools.Method(generatorType,
                    "ConstructMinimalMapComponents");
                if (constructMinimal != null)
                    previewHarmony.Patch(constructMinimal,
                        postfix: new HarmonyMethod(
                            typeof(CARegionalCompatibility),
                            nameof(PreviewMinimalComponentsPostfix)));
                previewDetermineMapSizeMethod = AccessTools.Method(sizeType,
                    "DetermineMapSize", new[]
                    {
                        typeof(World), typeof(PlanetTile), typeof(MapParent)
                    });
                if (previewDetermineMapSizeMethod != null)
                    previewHarmony.Patch(previewDetermineMapSizeMethod,
                        postfix: new HarmonyMethod(
                            typeof(CARegionalCompatibility),
                            nameof(PreviewDetermineMapSizePostfix)));
                Type previewWindowType = FindType(
                    "MapPreview.MapPreviewWindow");
                Type previewToolbarType = FindType(
                    "MapPreview.MapPreviewToolbar");
                MethodInfo tileSelected = AccessTools.Method(
                    previewWindowType, "OnWorldTileSelected", new[]
                    {
                        typeof(World), typeof(PlanetTile), typeof(MapParent)
                    });
                previewWidgetField = AccessTools.Field(previewWindowType,
                    "_previewWidget");
                previewTextureProperty = AccessTools.Property(FindType(
                    "MapPreview.MapPreviewWidget"), "Texture");
                if (tileSelected != null && previewWidgetField != null
                    && previewTextureProperty != null
                    && previewDetermineMapSizeMethod != null)
                {
                    previewHarmony.Patch(tileSelected,
                        prefix: new HarmonyMethod(
                            typeof(CARegionalCompatibility),
                            nameof(PreviewWindowTileSelectedPrefix)),
                        postfix: new HarmonyMethod(
                            typeof(CARegionalCompatibility),
                            nameof(PreviewWindowTileSelectedPostfix)));
                }
                else
                {
                    Log.Warning("[CA][Regional] Map Preview exact-size texture "
                        + "compatibility could not be installed");
                }
                // CAO presentation of the regional preview: the engine and
                // window lifecycle stay Map Preview's; what is DRAWN inside
                // the window becomes CAO's regional preview whenever a
                // regional plan is bound - zoomable, pannable, per-cell
                // inspectable, with the arrival marked on the real map.
                MethodInfo previewDoContents = AccessTools.Method(
                    previewWindowType, "DoWindowContents");
                if (previewDoContents != null)
                    previewHarmony.Patch(previewDoContents,
                        prefix: new HarmonyMethod(
                            typeof(CARegionalCompatibility),
                            nameof(PreviewWindowContentsPrefix)));
                Type previewWidgetBaseType =
                    FindType("MapPreview.MapPreviewWidget");
                previewTexCoordsField = AccessTools.Field(
                    previewWidgetBaseType, "TexCoords");
                previewMapProperty = AccessTools.Property(
                    previewWidgetBaseType, "PreviewMap");
                MethodInfo previewPreClose = AccessTools.Method(
                    previewWindowType, "PreClose", Type.EmptyTypes);
                if (previewPreClose != null)
                    previewHarmony.Patch(previewPreClose,
                        prefix: new HarmonyMethod(
                            typeof(CARegionalCompatibility),
                            nameof(PreviewWindowPreClosePrefix)));
                MethodInfo toolbarPreClose = AccessTools.Method(
                    previewToolbarType, "PreClose", Type.EmptyTypes);
                if (toolbarPreClose != null)
                    previewHarmony.Patch(toolbarPreClose,
                        prefix: new HarmonyMethod(
                            typeof(CARegionalCompatibility),
                            nameof(PreviewToolbarPreClosePrefix)));
                previewRefreshMethod = AccessTools.Method(
                    FindType("MapPreview.WorldInterfaceManager"),
                    "RefreshPreview");
                previewInstalled = true;
                bool exactSizeHealthy = previewMaxMapSizeField != null
                    && previewSizeOverrideField != null
                    && previewDetermineMapSizeMethod != null
                    && tileSelected != null && previewWidgetField != null
                    && previewTextureProperty != null;
                if (exactSizeHealthy && collect != null
                    && constructMinimal != null)
                    Log.Message("[CA][Regional] Map Preview compatibility "
                        + "installed against the 1.6 three-argument size API; "
                        + "regional requests use the selected composition's "
                        + "exact backing frame and shared projection steps; "
                        + "inhabited settlement materialization remains "
                        + "preview-inert");
                else
                    Log.Warning("[CA][Regional] Map Preview compatibility is "
                        + "partial: max=" + (previewMaxMapSizeField != null)
                        + ", game-init override="
                        + (previewSizeOverrideField != null)
                        + ", determine-size-3="
                        + (previewDetermineMapSizeMethod != null)
                        + ", tile-selected-3=" + (tileSelected != null)
                        + ", widget=" + (previewWidgetField != null)
                        + ", texture=" + (previewTextureProperty != null)
                        + ", collect=" + (collect != null)
                        + ", minimal-components="
                        + (constructMinimal != null));
            }
            catch (Exception ex)
            {
                Log.Warning("[CA][Regional] Map Preview compatibility failed: "
                    + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static IntVec2 PreviewMapSizeOverride()
        {
            CARegionalPlan plan = CARegionalSetupSession.ActivePreviewPlan;
            if (plan != null)
                return new IntVec2(plan.BackingMapSize.x,
                    plan.BackingMapSize.z);
            if (previousPreviewSizeOverride != null)
            {
                object value = previousPreviewSizeOverride.DynamicInvoke();
                if (value is IntVec2 size) return size;
            }
            return new IntVec2(-1, -1);
        }

        // The signature of the composition whose generated texture the
        // preview window currently holds. Set when a regional request is
        // allowed through to generation; consulted so that selecting
        // anything INSIDE an already-generated composition - a settlement,
        // a member area, a changed arrival - keeps the texture instead of
        // paying a full map generation for the same answer.
        private static string generatedPreviewSignature;

        // ---- CAO presentation of the regional preview -----------------------
        // Navigation state for inspecting the generated region: zoom about
        // the cursor, pan by dragging. Reset whenever the shown composition
        // changes so a new region always opens fully framed.
        private static float previewZoom = 1f;
        private static Vector2 previewPan = new Vector2(0.5f, 0.5f);
        private static string previewNavSignature;

        private static bool PreviewWindowContentsPrefix(object __instance,
            Rect inRect)
        {
            try
            {
                CARegionalPlan plan =
                    CARegionalSetupSession.ActivePreviewPlan;
                if (plan == null || previewWidgetField == null
                    || previewTextureProperty == null) return true;
                object widget = previewWidgetField.GetValue(__instance);
                Texture2D texture = widget == null ? null
                    : previewTextureProperty.GetValue(widget, null)
                        as Texture2D;
                if (texture == null) return true;
                Rect generatedArea = previewTexCoordsField != null
                    ? (Rect)previewTexCoordsField.GetValue(widget)
                    : new Rect(0f, 0f, 1f, 1f);
                if (generatedArea.width <= 0f || generatedArea.height <= 0f)
                    generatedArea = new Rect(0f, 0f, 1f, 1f);
                Map previewMap = previewMapProperty?.GetValue(widget, null)
                    as Map;
                bool generating = IsMapPreviewGenerating();

                CARegionalGeographyComposition composition =
                    CARegionalGeographyContract.Inspect(plan);
                if (composition.Signature != previewNavSignature)
                {
                    previewNavSignature = composition.Signature;
                    previewZoom = 1f;
                    previewPan = new Vector2(0.5f, 0.5f);
                }

                Widgets.DrawBoxSolid(inRect, CAOpeningTheme.Ink);
                Rect frame = inRect.ContractedBy(4f);
                const float headerHeight = 18f;
                Text.Font = GameFont.Tiny;
                GUI.color = CAOpeningTheme.TextLo;
                Widgets.Label(new Rect(frame.x + 4f, frame.y,
                    frame.width - 8f, headerHeight),
                    CARegionalPlanUtility.RegionName(plan) + " Â· "
                    + plan.BackingMapSize.x + "x" + plan.BackingMapSize.z
                    + (generating ? " Â· generating..."
                        : previewZoom > 1.01f
                            ? " Â· " + previewZoom.ToString("F1")
                                + "x Â· drag to pan, scroll to zoom, "
                                + "right-click to reset"
                            : " Â· scroll to zoom Â· hover for ground"));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                // Fit the generated map's true aspect inside the remaining
                // frame; the window is already aspect-framed, so this only
                // absorbs the header strip.
                Rect available = new Rect(frame.x,
                    frame.y + headerHeight + 2f, frame.width,
                    frame.height - headerHeight - 2f);
                float mapAspect = plan.BackingMapSize.z > 0
                    ? plan.BackingMapSize.x / (float)plan.BackingMapSize.z
                    : 1f;
                float w = available.width;
                float h = w / mapAspect;
                if (h > available.height)
                {
                    h = available.height;
                    w = h * mapAspect;
                }
                var map = new Rect(
                    available.x + (available.width - w) * 0.5f,
                    available.y + (available.height - h) * 0.5f, w, h);

                HandlePreviewShapeHandles(map, plan);
                HandlePreviewNavigation(map);

                float half = 0.5f / previewZoom;
                previewPan.x = Mathf.Clamp(previewPan.x, half, 1f - half);
                previewPan.y = Mathf.Clamp(previewPan.y, half, 1f - half);
                var view = new Rect(
                    generatedArea.x + (previewPan.x - half)
                        * generatedArea.width,
                    generatedArea.y + (previewPan.y - half)
                        * generatedArea.height,
                    generatedArea.width / previewZoom,
                    generatedArea.height / previewZoom);
                GUI.DrawTextureWithTexCoords(map, texture, view);
                // THE REGIONAL SEMANTIC LAYER over the real terrain: which
                // authored facts occupy this geography. Member seams are a
                // whisper from the same partition generation consumes;
                // settlements and landmarks sit at their actual projected
                // anchors; the arrival is the ringed entry point.
                Texture2D seams = PreviewSeamOverlayFor(plan);
                if (seams != null)
                {
                    float seamHalf = 0.5f / previewZoom;
                    GUI.DrawTextureWithTexCoords(map, seams, new Rect(
                        previewPan.x - seamHalf, previewPan.y - seamHalf,
                        1f / previewZoom, 1f / previewZoom));
                }
                CAOpeningTheme.Border(map, CAOpeningTheme.Hairline);

                DrawPreviewFeatures(map, plan);
                DrawPreviewSettlements(map, plan);
                DrawPreviewArrival(map, plan);
                HandlePreviewArrivalChoice(map, plan);
                HandlePreviewCompose(map, plan);
                HandlePreviewInspect(map, plan);
                if (previewMap != null && !generating
                    && Mouse.IsOver(map))
                    TipPreviewCell(map, plan, previewMap);

                if (generating)
                {
                    Widgets.DrawBoxSolid(map, new Color(
                        CAOpeningTheme.Ink.r, CAOpeningTheme.Ink.g,
                        CAOpeningTheme.Ink.b, 0.62f));
                    Text.Font = GameFont.Tiny;
                    GUI.color = CAOpeningTheme.TextLo;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(map, "Generating the region...");
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                }
                return false;
            }
            catch (Exception failure)
            {
                Log.ErrorOnce("[CA][Regional][Preview] CAO presentation "
                    + "failed; the stock preview drawing remains: "
                    + failure, 889417234);
                return true;
            }
        }

        // Normalized position (x right, y up, generated-area space) of a
        // screen point inside the current zoomed view, and back.
        private static Vector2 PreviewNormAt(Rect map, Vector2 screen)
        {
            float half = 0.5f / previewZoom;
            float ux = (screen.x - map.x) / map.width;
            float uy = (map.yMax - screen.y) / map.height;
            return new Vector2(
                previewPan.x - half + ux / previewZoom,
                previewPan.y - half + uy / previewZoom);
        }

        private static Vector2 PreviewScreenAt(Rect map, Vector2 norm)
        {
            float half = 0.5f / previewZoom;
            float ux = (norm.x - (previewPan.x - half)) * previewZoom;
            float uy = (norm.y - (previewPan.y - half)) * previewZoom;
            return new Vector2(map.x + ux * map.width,
                map.yMax - uy * map.height);
        }

        private static void HandlePreviewNavigation(Rect map)
        {
            Event current = Event.current;
            if (!map.Contains(current.mousePosition)) return;
            if (current.type == EventType.ScrollWheel)
            {
                Vector2 anchor = PreviewNormAt(map, current.mousePosition);
                float next = Mathf.Clamp(previewZoom
                    * (current.delta.y < 0f ? 1.25f : 0.8f), 1f, 8f);
                if (!Mathf.Approximately(next, previewZoom))
                {
                    // Keep the ground under the cursor under the cursor:
                    // norm = pan - half + u/zoom, solved for the new pan
                    // with the same norm and cursor fraction u.
                    float ux = (current.mousePosition.x - map.x)
                        / map.width;
                    float uy = (map.yMax - current.mousePosition.y)
                        / map.height;
                    previewZoom = next;
                    float half = 0.5f / previewZoom;
                    previewPan = new Vector2(
                        anchor.x - ux / previewZoom + half,
                        anchor.y - uy / previewZoom + half);
                }
                current.Use();
            }
            else if (current.type == EventType.MouseDrag
                && current.button == 0 && previewZoom > 1.01f)
            {
                previewPan.x -= current.delta.x
                    / (map.width * previewZoom);
                previewPan.y += current.delta.y
                    / (map.height * previewZoom);
                current.Use();
            }
            else if (current.type == EventType.MouseDown
                && current.button == 1)
            {
                previewZoom = 1f;
                previewPan = new Vector2(0.5f, 0.5f);
                current.Use();
            }
        }

        // Hairline member-boundary overlay derived from the projection
        // kernel's per-cell ownership - the same partition generation
        // consumes - cached per composition.
        private static Texture2D previewSeamOverlay;
        private static long previewSeamSignature;

        private static Texture2D PreviewSeamOverlayFor(CARegionalPlan plan)
        {
            if (plan?.memberTileIds == null) return null;
            CARegionalProjectionKernel kernel =
                CARegionalProjectionPreview.KernelFor(plan);
            if (kernel?.MemberByCell == null || kernel.Size.x <= 1
                || kernel.Size.z <= 1) return null;
            // The world seed is part of the identity: two different worlds
            // can reuse the same tile ids, and a stale overlay would show
            // the previous world's seams over the new world's terrain.
            long signature = 17L + (Verse.Find.World?.info?.Seed ?? 0);
            signature = signature * 31L + kernel.Size.x;
            signature = signature * 31L + kernel.Size.z;
            foreach (int member in plan.memberTileIds)
                signature = signature * 31L + member;
            if (signature == previewSeamSignature
                && previewSeamOverlay != null) return previewSeamOverlay;
            if (previewSeamOverlay != null)
                UnityEngine.Object.Destroy(previewSeamOverlay);
            int width = kernel.Size.x, height = kernel.Size.z;
            int[] memberByCell = kernel.MemberByCell;
            var pixels = new Color32[width * height];
            var seam = new Color32(10, 12, 14, 78);
            for (int z = 0; z < height - 1; z++)
            {
                int row = z * width;
                for (int x = 0; x < width - 1; x++)
                {
                    int i = row + x;
                    if (i + 1 >= memberByCell.Length
                        || i + width >= memberByCell.Length) continue;
                    int owner = memberByCell[i];
                    if (owner < 0) continue;
                    if ((memberByCell[i + 1] >= 0
                            && memberByCell[i + 1] != owner)
                        || (memberByCell[i + width] >= 0
                            && memberByCell[i + width] != owner))
                        pixels[i] = seam;
                }
            }
            previewSeamOverlay = new Texture2D(width, height,
                TextureFormat.RGBA32, false);
            previewSeamOverlay.filterMode = FilterMode.Bilinear;
            previewSeamOverlay.SetPixels32(pixels);
            previewSeamOverlay.Apply();
            previewSeamSignature = signature;
            return previewSeamOverlay;
        }

        private static void DrawPreviewSettlements(Rect map,
            CARegionalPlan plan)
        {
            if (plan.settlements == null || plan.settlements.Count == 0)
                return;
            CARegionalProjectionKernel kernel =
                CARegionalProjectionPreview.KernelFor(plan);
            if (kernel == null || kernel.Size.x <= 0) return;
            foreach (CARegionalSettlementPlan settlement in
                plan.settlements)
            {
                if (settlement == null) continue;
                Vector2 anchor = kernel.VisualLandAnchor(
                    settlement.memberTileId);
                var norm = new Vector2((anchor.x + 0.5f) / kernel.Size.x,
                    (anchor.y + 0.5f) / kernel.Size.z);
                Vector2 at = PreviewScreenAt(map, norm);
                if (at.x < map.x + 5f || at.x > map.xMax - 5f
                    || at.y < map.y + 5f || at.y > map.yMax - 5f)
                    continue;
                Color color = CARegionalWorldOverlay.FactionColor(
                    settlement.OwningFactionKey);
                // A settlement reads as a settlement: a roofed cluster
                // in its holder's color, growing with the standing the
                // urban tendency gave it.
                int standing = settlement.realizedScale;
                CAPlaceGlyphs.DrawSettlement(at,
                    Mathf.Clamp(3.4f + standing * 0.3f, 3.4f, 5f),
                    standing, color);
                var glyph = new Rect(at.x - 6f, at.y - 6f, 12f, 12f);
                string name = CARegionalPlanUtility.SettlementName(plan,
                    settlement);
                if (!name.NullOrEmpty())
                {
                    Text.Font = GameFont.Tiny;
                    Vector2 size = Text.CalcSize(name);
                    var label = new Rect(at.x - (size.x + 8f) * 0.5f,
                        at.y + 6f, size.x + 8f, 15f);
                    label.x = Mathf.Clamp(label.x, map.x + 2f,
                        map.xMax - label.width - 2f);
                    if (label.yMax < map.yMax - 2f)
                    {
                        Widgets.DrawBoxSolid(label,
                            new Color(0.05f, 0.06f, 0.07f, 0.72f));
                        GUI.color = new Color(0.90f, 0.91f, 0.90f);
                        Text.Anchor = TextAnchor.MiddleCenter;
                        Widgets.Label(label, name);
                        Text.Anchor = TextAnchor.UpperLeft;
                        GUI.color = Color.white;
                    }
                    Text.Font = GameFont.Small;
                }
                TooltipHandler.TipRegion(glyph.ExpandedBy(4f),
                    (name.NullOrEmpty() ? "Settlement" : name) + "\n"
                    + CARegionalPlanUtility.TileWords(
                        settlement.memberTileId)
                    + "\nExact site is chosen from generated terrain.");
            }

            // The frontier tendency's own output, on the same preview:
            // holdings as farmsteads across the region's empty land.
            foreach (CAFrontierHoldingPlan holding in
                plan.frontierHoldings ?? new List<CAFrontierHoldingPlan>())
            {
                if (holding == null || holding.memberTileId < 0) continue;
                Vector2 anchor = kernel.VisualLandAnchor(
                    holding.memberTileId);
                var norm = new Vector2((anchor.x + 0.5f) / kernel.Size.x,
                    (anchor.y + 0.5f) / kernel.Size.z);
                Vector2 at = PreviewScreenAt(map, norm);
                at.y += 10f;
                if (at.x < map.x + 5f || at.x > map.xMax - 5f
                    || at.y < map.y + 5f || at.y > map.yMax - 5f)
                    continue;
                CAPlaceGlyphs.DrawHolding(at, 3f, holding.materialLevel);
                TooltipHandler.TipRegion(new Rect(at.x - 7f, at.y - 7f,
                        14f, 14f),
                    (holding.siteName ?? (holding.form == 1
                        ? "Homestead" : "Cabin"))
                    + "\n" + holding.residentCount + " resident"
                    + (holding.residentCount == 1 ? "" : "s")
                    + ", material level " + holding.materialLevel);
            }
        }

        private static void DrawPreviewFeatures(Rect map,
            CARegionalPlan plan)
        {
            CARegionalCandidateFacts facts =
                CARegionalProjectionPreview.FactsFor(plan);
            CARegionalProjectionKernel kernel =
                CARegionalProjectionPreview.KernelFor(plan);
            if (facts == null || kernel == null || kernel.Size.x <= 0)
                return;
            foreach (CARegionalCandidateFacts.Feature feature in
                facts.Features)
            {
                if (feature?.Tile.Valid != true) continue;
                Vector2 anchor = kernel.VisualLandAnchor(
                    feature.Tile.tileId);
                var norm = new Vector2((anchor.x + 0.5f) / kernel.Size.x,
                    (anchor.y + 0.5f) / kernel.Size.z);
                Vector2 at = PreviewScreenAt(map, norm);
                if (at.x < map.x + 5f || at.x > map.xMax - 5f
                    || at.y < map.y + 8f || at.y > map.yMax - 5f)
                    continue;
                Color color = feature.Historical
                    ? new Color(0.82f, 0.72f, 0.46f)
                    : new Color(0.90f, 0.91f, 0.88f);
                var halo = new Color(0.05f, 0.06f, 0.07f, 0.85f);
                var stem = new Rect(at.x - 1f, at.y - 8f, 2f, 8f);
                var foot = new Rect(at.x - 3f, at.y - 1f, 6f, 2f);
                Widgets.DrawBoxSolid(stem.ExpandedBy(1f), halo);
                Widgets.DrawBoxSolid(foot.ExpandedBy(1f), halo);
                Widgets.DrawBoxSolid(stem, color);
                Widgets.DrawBoxSolid(foot, color);
                TooltipHandler.TipRegion(new Rect(at.x - 7f, at.y - 11f,
                    14f, 14f), feature.Name + "\n"
                    + feature.Def.LabelCap);
            }
        }

        private static void DrawPreviewArrival(Rect map,
            CARegionalPlan plan)
        {
            CARegionalProjectionKernel kernel =
                CARegionalProjectionPreview.KernelFor(plan);
            if (kernel == null || kernel.Size.x <= 0
                || kernel.Size.z <= 0) return;
            Vector2 anchor = kernel.VisualLandAnchor(plan.startTileId);
            var norm = new Vector2((anchor.x + 0.5f) / kernel.Size.x,
                (anchor.y + 0.5f) / kernel.Size.z);
            Vector2 at = PreviewScreenAt(map, norm);
            if (at.x < map.x + 6f || at.x > map.xMax - 6f
                || at.y < map.y + 6f || at.y > map.yMax - 6f) return;
            var halo = new Color(0.05f, 0.06f, 0.07f, 0.85f);
            var mark = new Color(0.95f, 0.93f, 0.86f);
            const float r = 7f;
            const int segments = 12;
            Vector2 previous = at + new Vector2(r, 0f);
            for (int s = 1; s <= segments; s++)
            {
                float angle = s * Mathf.PI * 2f / segments;
                Vector2 next = at + new Vector2(Mathf.Cos(angle) * r,
                    Mathf.Sin(angle) * r);
                Widgets.DrawLine(previous, next, halo, 3.4f);
                Widgets.DrawLine(previous, next, mark, 1.6f);
                previous = next;
            }
            Widgets.DrawBoxSolid(new Rect(at.x - 1.5f, at.y - 1.5f, 3f,
                3f), mark);
            TooltipHandler.TipRegion(new Rect(at.x - r - 2f,
                at.y - r - 2f, (r + 2f) * 2f, (r + 2f) * 2f),
                "Arrival: where the arriving party enters this region.");
        }

        private static void HandlePreviewArrivalChoice(Rect map,
            CARegionalPlan plan)
        {
            if (CAOpeningAugments.Mode != CALandingMode.Arrival) return;
            Event current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0
                || !map.Contains(current.mousePosition)) return;
            CARegionalProjectionKernel kernel =
                CARegionalProjectionPreview.KernelFor(plan);
            if (kernel == null) return;
            Vector2 norm = PreviewNormAt(map, current.mousePosition);
            int x = Mathf.Clamp(Mathf.FloorToInt(norm.x * kernel.Size.x),
                0, kernel.Size.x - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(norm.y * kernel.Size.z),
                0, kernel.Size.z - 1);
            int index = kernel.Indices.CellToIndex(new IntVec3(x, 0, z));
            PlanetTile member = kernel.MemberTileAtIndex(index);
            if (member.Valid)
            {
                if (CARegionalSetupSession.TrySetArrival(plan,
                        member.tileId, out string refusal))
                    Messages.Message("Arrival area: "
                        + CARegionalPlanUtility.TileSummary(member.tileId),
                        MessageTypeDefOf.TaskCompletion, false);
                else
                    Messages.Message(refusal,
                        MessageTypeDefOf.RejectInput, false);
            }
            else
                Messages.Message("Choose land inside the region.",
                    MessageTypeDefOf.RejectInput, false);
            current.Use();
        }

        private static string draggingHandleFeature;
        private static int draggingHandleTile = -1;
        private static Vector2 draggingHandleScreen;

        // Direct spatial editing: while the shape editor is open for one
        // of this plan's areas, each of that area's inland-water basins
        // offers a drag handle at its resolved center; releasing commits
        // the basin's position through the editor's canonical-state seam.
        // Runs before navigation so a handle drag beats the pan.
        private static void HandlePreviewShapeHandles(Rect map,
            CARegionalPlan plan)
        {
            Dialog_CAFeatureShapeEditor editor = Verse.Find.WindowStack
                ?.Windows?.OfType<Dialog_CAFeatureShapeEditor>()
                .FirstOrDefault(item => item.Plan == plan);
            if (editor == null)
            {
                draggingHandleFeature = null;
                draggingHandleTile = -1;
                return;
            }
            CARegionalProjectionKernel kernel =
                CARegionalProjectionPreview.KernelFor(plan);
            if (kernel == null || kernel.Size.x <= 0) return;
            Event current = Event.current;
            foreach (CARegionalProjectionKernel.CAInlandWaterCenter basin
                in kernel.InlandWaterCenters)
            {
                if (basin.TileId != editor.TileId
                    || basin.Kind == CAInlandWaterKind.Basin) continue;
                bool dragging = draggingHandleFeature == basin.FeatureDef
                    && draggingHandleTile == basin.TileId;
                Vector2 at = dragging ? draggingHandleScreen
                    : PreviewScreenAt(map, new Vector2(
                        (basin.Center.x + 0.5f) / kernel.Size.x,
                        (basin.Center.y + 0.5f) / kernel.Size.z));
                if (!dragging && (at.x < map.x + 6f
                    || at.x > map.xMax - 6f || at.y < map.y + 6f
                    || at.y > map.yMax - 6f)) continue;
                DrawHandleDiamond(at, dragging);
                TooltipHandler.TipRegion(new Rect(at.x - 9f, at.y - 9f,
                    18f, 18f),
                    "Drag to move this water body within its area.");
                if (current.type == EventType.MouseDown
                    && current.button == 0
                    && Vector2.Distance(current.mousePosition, at) <= 9f)
                {
                    draggingHandleFeature = basin.FeatureDef;
                    draggingHandleTile = basin.TileId;
                    draggingHandleScreen = at;
                    current.Use();
                }
                else if (dragging && current.type == EventType.MouseDrag)
                {
                    draggingHandleScreen = current.mousePosition;
                    current.Use();
                }
                else if (dragging && current.type == EventType.MouseUp)
                {
                    Vector2 norm = PreviewNormAt(map,
                        draggingHandleScreen);
                    var cell = new Vector2(
                        norm.x * kernel.Size.x - 0.5f,
                        norm.y * kernel.Size.z - 0.5f);
                    editor.CommitWander(basin.FeatureDef,
                        (cell.x - basin.Anchor.x) / basin.Span,
                        (cell.y - basin.Anchor.y) / basin.Span);
                    draggingHandleFeature = null;
                    draggingHandleTile = -1;
                    current.Use();
                }
            }
        }

        private static void DrawHandleDiamond(Vector2 at, bool active)
        {
            var halo = new Color(0.05f, 0.06f, 0.07f, 0.9f);
            Color tone = active ? new Color(0.97f, 0.90f, 0.62f)
                : new Color(0.93f, 0.86f, 0.62f);
            var up = new Vector2(0f, 6f);
            var right = new Vector2(6f, 0f);
            Widgets.DrawLine(at - up, at + right, halo, 3.2f);
            Widgets.DrawLine(at + right, at + up, halo, 3.2f);
            Widgets.DrawLine(at + up, at - right, halo, 3.2f);
            Widgets.DrawLine(at - right, at - up, halo, 3.2f);
            Widgets.DrawLine(at - up, at + right, tone, 1.6f);
            Widgets.DrawLine(at + right, at + up, tone, 1.6f);
            Widgets.DrawLine(at + up, at - right, tone, 1.6f);
            Widgets.DrawLine(at - right, at - up, tone, 1.6f);
        }

        // Inspect acts on the preview exactly as it acts on the world: a
        // click selects the member under the cursor (the column answers
        // with that area's facts), and a double-click opens its
        // feature-shape editor when the area carries shapeable features.
        // The two views stay one authoring surface.
        private static void HandlePreviewInspect(Rect map,
            CARegionalPlan plan)
        {
            if (CAOpeningAugments.Mode != CALandingMode.Inspect) return;
            Event current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0
                || !map.Contains(current.mousePosition)) return;
            CARegionalProjectionKernel kernel =
                CARegionalProjectionPreview.KernelFor(plan);
            if (kernel == null || kernel.Size.x <= 0) return;
            Vector2 norm = PreviewNormAt(map, current.mousePosition);
            if (norm.x < 0f || norm.x >= 1f || norm.y < 0f
                || norm.y >= 1f) return;
            int x = Mathf.Clamp((int)(norm.x * kernel.Size.x), 0,
                kernel.Size.x - 1);
            int z = Mathf.Clamp((int)(norm.y * kernel.Size.z), 0,
                kernel.Size.z - 1);
            PlanetTile member = kernel.MemberTileAtIndex(
                kernel.Indices.CellToIndex(new IntVec3(x, 0, z)));
            if (!member.Valid) return;
            Verse.Find.WorldInterface.SelectedTile = member;
            if (current.clickCount >= 2 && (member.Tile?.Mutators
                    ?? Enumerable.Empty<TileMutatorDef>())
                .Any(CAFeatureShapeModel.Shapeable))
                Verse.Find.WindowStack.Add(
                    new Dialog_CAFeatureShapeEditor(plan, member.tileId));
            current.Use();
        }

        // Compose acts on the preview exactly as it acts on the world:
        // point at the generated map, the member under the cursor answers,
        // a click releases it through the same guarded funnel. The two
        // views stay one authoring surface.
        private static void HandlePreviewCompose(Rect map,
            CARegionalPlan plan)
        {
            if (CAOpeningAugments.Mode != CALandingMode.Compose) return;
            if (!map.Contains(Event.current.mousePosition)) return;
            CARegionalProjectionKernel kernel =
                CARegionalProjectionPreview.KernelFor(plan);
            if (kernel == null || kernel.Size.x <= 0) return;
            Vector2 norm = PreviewNormAt(map, Event.current.mousePosition);
            if (norm.x < 0f || norm.x >= 1f || norm.y < 0f || norm.y >= 1f)
                return;
            int kx = Mathf.Clamp((int)(norm.x * kernel.Size.x), 0,
                kernel.Size.x - 1);
            int kz = Mathf.Clamp((int)(norm.y * kernel.Size.z), 0,
                kernel.Size.z - 1);
            PlanetTile member = kernel.MemberTileAtIndex(
                kernel.Indices.CellToIndex(new IntVec3(kx, 0, kz)));
            if (!member.Valid) return;
            Vector2 anchor = kernel.VisualLandAnchor(member.tileId);
            Vector2 at = PreviewScreenAt(map, new Vector2(
                (anchor.x + 0.5f) / kernel.Size.x,
                (anchor.y + 0.5f) / kernel.Size.z));
            if (at.x >= map.x && at.x <= map.xMax && at.y >= map.y
                && at.y <= map.yMax)
            {
                const float r = 10f;
                const int segments = 12;
                Vector2 previous = at + new Vector2(r, 0f);
                for (int s = 1; s <= segments; s++)
                {
                    float angle = s * Mathf.PI * 2f / segments;
                    Vector2 next = at + new Vector2(
                        Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
                    Widgets.DrawLine(previous, next,
                        CAOpeningAugments.RemoveTone, 1.8f);
                    previous = next;
                }
            }
            CAOpeningAugments.CursorStatement("Release "
                + (member.Tile?.PrimaryBiome?.label ?? "this area"),
                CAOpeningAugments.RemoveTone);
            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0)
            {
                CAExpandedLandmassProfile profile;
                if (CAExpandedLandmassProfile.TryFor(
                        Verse.Find.GameInitData.mapSize, out profile))
                    CARegionalSetupSession.ToggleRegionArea(profile, plan,
                        member);
                Event.current.Use();
            }
        }

        private static void TipPreviewCell(Rect map, CARegionalPlan plan,
            Map previewMap)
        {
            Vector2 norm = PreviewNormAt(map, Event.current.mousePosition);
            if (norm.x < 0f || norm.x >= 1f || norm.y < 0f || norm.y >= 1f)
                return;
            int cellX = Mathf.Clamp((int)(norm.x * previewMap.Size.x), 0,
                previewMap.Size.x - 1);
            int cellZ = Mathf.Clamp((int)(norm.y * previewMap.Size.z), 0,
                previewMap.Size.z - 1);
            string terrain = null;
            try
            {
                terrain = previewMap.terrainGrid
                    ?.TerrainAt(new IntVec3(cellX, 0, cellZ))?.label;
            }
            catch { }
            if (terrain.NullOrEmpty()) return;
            string area = null;
            string water = null;
            CARegionalProjectionKernel kernel =
                CARegionalProjectionPreview.KernelFor(plan);
            if (kernel != null && kernel.Size.x > 0)
            {
                int kx = Mathf.Clamp((int)(norm.x * kernel.Size.x), 0,
                    kernel.Size.x - 1);
                int kz = Mathf.Clamp((int)(norm.y * kernel.Size.z), 0,
                    kernel.Size.z - 1);
                int index = kernel.Indices.CellToIndex(
                    new IntVec3(kx, 0, kz));
                PlanetTile member = kernel.MemberTileAtIndex(index);
                if (member.Valid)
                {
                    area = CARegionalPlanUtility.TileWords(member.tileId);
                    if (CAOpeningAugments.Mode == CALandingMode.Inspect
                        && (member.Tile?.Mutators
                            ?? Enumerable.Empty<TileMutatorDef>())
                        .Any(CAFeatureShapeModel.Shapeable))
                        area += "\nDouble-click to shape this area's "
                            + "features.";
                }
                ushort[] depths = kernel.WaterDepth;
                if (depths != null && index < depths.Length
                    && depths[index] > 0)
                    water = depths[index] < 300 ? "shallow water"
                        : depths[index] < 700 ? "deep water"
                            : "very deep water";
            }
            TooltipHandler.TipRegion(map, new TipSignal(
                terrain.CapitalizeFirst()
                + (water == null ? "" : " Â· " + water)
                + (area == null ? "" : "\n" + area)
                + "\n(" + cellX + " | " + cellZ + ")", 73211905));
        }

        private static bool PreviewWindowTileSelectedPrefix(object __instance,
            World world, ref PlanetTile tileId, ref MapParent mapParent)
        {
            if (__instance == null || world == null
                || previewDetermineMapSizeMethod == null
                || previewWidgetField == null
                || previewTextureProperty == null) return true;
            try
            {
                CARegionalPlan plan = ResolvePreviewPlan(world, tileId);
                if (plan != null)
                {
                    IntVec2 size = new IntVec2(plan.BackingMapSize.x,
                        plan.BackingMapSize.z);
                    CARegionalGeographyComposition composition =
                        CARegionalGeographyContract.Inspect(plan);
                    object widget = previewWidgetField.GetValue(__instance);
                    Texture2D texture = widget == null ? null
                        : previewTextureProperty.GetValue(widget, null)
                            as Texture2D;
                    // Same geography, finished texture at the exact backing
                    // frame: the selection is a question the texture already
                    // answers. Keep the binding fresh and skip regeneration
                    // entirely.
                    if (composition.Signature == generatedPreviewSignature
                        && texture != null
                        && texture.width == size.x
                        && texture.height == size.z
                        && !CARegionalCompatibility.IsMapPreviewGenerating())
                    {
                        CARegionalSetupSession.BindPreviewPlan(plan);
                        return false;
                    }
                }

                // The external window persists its current position while it
                // handles a tile change. Present its own undocked position for
                // that write, then the postfix returns it to CA's temporary
                // page layout. This keeps Map Preview's saved preference intact.
                CARegionalPreviewDock.BeforeExternalSelection(
                    __instance as Window);
                // The global Map Preview size delegate has no tile argument.
                // Clear the preceding request first so a native tile outside
                // the selected composition cannot inherit a regional frame or
                // regional gensteps from the last click.
                CARegionalSetupSession.ClearPreviewBinding();
                if (plan != null)
                {
                    CARegionalSetupSession.BindPreviewPlan(plan);
                    PlanetTile canonical = plan.StartTile;
                    if (canonical.Valid)
                    {
                        tileId = canonical;
                        MapParent canonicalParent = world.worldObjects
                            ?.MapParentAt(canonical);
                        if (canonicalParent != null)
                            mapParent = canonicalParent;
                    }
                }
                if (plan == null)
                {
                    generatedPreviewSignature = null;
                    return true;
                }
                IntVec2 backing = new IntVec2(plan.BackingMapSize.x,
                    plan.BackingMapSize.z);
                EnsurePreviewMaximum(backing);
                object boundWidget = previewWidgetField.GetValue(__instance);
                Texture2D boundTexture = boundWidget == null ? null
                    : previewTextureProperty.GetValue(boundWidget, null)
                        as Texture2D;
                if (boundTexture != null
                    && (boundTexture.width != backing.x
                        || boundTexture.height != backing.z)
                    && !boundTexture.Reinitialize(backing.x, backing.z))
                {
                    Log.Warning("[CA][Regional] Map Preview texture rejected "
                        + "exact regional backing " + backing.x + "x"
                        + backing.z);
                    return true;
                }
                CARegionalGeographyComposition bound =
                    CARegionalGeographyContract.Inspect(plan);
                generatedPreviewSignature = bound.Signature;
                if (lastPreviewRequestSignature != bound.Signature)
                {
                    lastPreviewRequestSignature = bound.Signature;
                    Log.Message("[CA][Regional][Preview] request "
                        + bound.Signature + " bound to candidate "
                        + (plan.candidateId ?? "unknown") + "; arrival "
                        + plan.startTileId + "; exact backing " + backing.x
                        + "x" + backing.z + "; texture "
                        + (boundTexture == null ? "unavailable"
                            : boundTexture.width + "x"
                                + boundTexture.height));
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[CA][Regional] Map Preview exact-size texture "
                    + "resize failed: " + ex.GetType().Name + ": "
                    + ex.Message);
            }
            return true;
        }

        private static void PreviewWindowTileSelectedPostfix()
        {
            CARegionalPreviewDock.AfterExternalSelection();
        }

        private static void PreviewWindowPreClosePrefix(object __instance)
        {
            CARegionalPreviewDock.BeforeExternalPreviewClose(
                __instance as Window);
        }

        private static void PreviewToolbarPreClosePrefix(object __instance)
        {
            CARegionalPreviewDock.BeforeExternalToolbarClose(
                __instance as Window);
        }

        private static void PreviewDetermineMapSizePostfix(World world,
            PlanetTile tile, MapParent mapParent, ref IntVec2 __result)
        {
            if (!tile.Valid) tile = mapParent?.Tile
                ?? Verse.Find.WorldInterface.SelectedTile;
            if (!tile.Valid || tile.Layer != Verse.Find.WorldGrid.Surface)
                return;
            CARegionalPlan existing = CARegionalWorldComponent.Current
                ?.FindRegionContaining(tile);
            if (existing != null)
            {
                CARegionalSetupSession.BindPreviewPlan(existing);
                __result = new IntVec2(existing.BackingMapSize.x,
                    existing.BackingMapSize.z);
                EnsurePreviewMaximum(__result);
                return;
            }
            CARegionalPlan pending = CARegionalSetupSession
                .PendingForCurrentWorld;
            if (pending != null && pending.memberTileIds != null
                && pending.memberTileIds.Contains(tile.tileId))
            {
                __result = new IntVec2(pending.BackingMapSize.x,
                    pending.BackingMapSize.z);
                EnsurePreviewMaximum(__result);
                return;
            }
            CAExpandedLandmassProfile profile =
                default(CAExpandedLandmassProfile);
            int localSize = pending?.mapSize
                ?? Verse.Find.GameInitData?.mapSize
                ?? 0;
            bool hasProfile = CAExpandedLandmassProfile.TryFor(localSize,
                out profile);
            if (!hasProfile && world?.info != null)
            {
                IntVec3 initial = world.info.initialMapSize;
                hasProfile = initial.x == initial.z
                    && CAExpandedLandmassProfile.TryFor(initial.x,
                        out profile);
            }
            if (!hasProfile && __result.x == __result.z)
                hasProfile = CAExpandedLandmassProfile.TryFor(__result.x,
                    out profile);
            if (!hasProfile) return;
            // A CONFIRMED REGION HAS ONE DURABLE PHYSICAL IDENTITY. This
            // entry point built a fresh candidate anchored on whatever tile
            // was clicked, so selecting settlement A and settlement B of the
            // SAME realized region produced two different candidate
            // geographies and the preview re-randomized on every selection.
            // The realized (or pending) region that already contains the
            // tile is the only legitimate answer for its members; a fresh
            // candidate is for tiles that belong to nothing.
            CARegionalPlan realized = CARegionalWorldComponent.Current
                ?.FindRegionContaining(tile);
            if (realized == null && pending?.memberTileIds != null
                && pending.memberTileIds.Contains(tile.tileId))
                realized = pending;
            CARegionalPlan plan;
            if (realized != null)
            {
                plan = realized;
            }
            else if (CARegionalGeometry.IsBlocked(tile))
            {
                // An accidental click on open ocean or an impassable
                // mountain must not hijack the composition with a doomed
                // one-tile footprint; the click simply is not a region.
                return;
            }
            else
            {
                CARegionalWorldPolicy policy = pending?.worldPolicy
                    ?? CARegionalWorldComponent.Current?.WorldPolicy
                    ?? CAWorldTendenciesSession.Policy;
                int available = Math.Max(1, CARegionalBundleBuilder.Build(
                    tile, 12).Count);
                int count = CARegionalSetupSession.StickyRegionTileCount > 0
                    ? CARegionalSetupSession.StickyRegionTileCount
                    : policy.ResolveRequestedExtent(tile, available);
                count = SnapToCatalog(count);
                plan = CARegionalSetupSession.EnsurePreviewPlan(
                    tile, profile, count);
            }
            if (plan == null) return;
            CARegionalSetupSession.BindPreviewPlan(plan);
            __result = new IntVec2(plan.BackingMapSize.x,
                plan.BackingMapSize.z);
            EnsurePreviewMaximum(__result);
        }

        private static CARegionalPlan ResolvePreviewPlan(World world,
            PlanetTile tile)
        {
            if (world == null || !tile.Valid) return null;
            CARegionalPlan plan = CARegionalWorldComponent.Current
                ?.FindRegionContaining(tile);
            CARegionalPlan pending = CARegionalSetupSession
                .PendingForCurrentWorld;
            if (plan == null && pending?.memberTileIds != null
                && pending.memberTileIds.Contains(tile.tileId))
                plan = pending;
            if (plan != null) return plan;
            // Ocean and impassable mountains are not prospective regions;
            // the accidental click keeps whatever was selected before.
            if (CARegionalGeometry.IsBlocked(tile)) return null;

            CAExpandedLandmassProfile profile;
            int localSize = Verse.Find.GameInitData?.mapSize ?? 0;
            bool hasProfile = CAExpandedLandmassProfile.TryFor(localSize,
                out profile);
            if (!hasProfile && world.info != null)
            {
                IntVec3 initial = world.info.initialMapSize;
                hasProfile = initial.x == initial.z
                    && CAExpandedLandmassProfile.TryFor(initial.x,
                        out profile);
            }
            if (!hasProfile) return null;
            CARegionalWorldPolicy policy = pending?.worldPolicy
                ?? CARegionalWorldComponent.Current?.WorldPolicy
                ?? CAWorldTendenciesSession.Policy;
            int available = Math.Max(1,
                CARegionalBundleBuilder.Build(tile, 12).Count);
            int count = CARegionalSetupSession.StickyRegionTileCount > 0
                ? CARegionalSetupSession.StickyRegionTileCount
                : policy.ResolveRequestedExtent(tile, available);
            count = SnapToCatalog(count);
            return CARegionalSetupSession.EnsurePreviewPlan(tile, profile,
                count);
        }

        // A prospective candidate must request an extent the durable
        // catalog actually supports, or its own preview refuses it.
        private static int SnapToCatalog(int count)
        {
            int best = CARegionalGeographyComposition.SupportedExtents[0];
            foreach (int extent in
                CARegionalGeographyComposition.SupportedExtents)
                if (Math.Abs(extent - count) < Math.Abs(best - count))
                    best = extent;
            return best;
        }

        private static void EnsurePreviewMaximum(IntVec2 size)
        {
            if (previewMaxMapSizeField == null || size.x <= 0 || size.z <= 0)
                return;
            IntVec2 existing = previewMaxMapSizeField.GetValue(null)
                is IntVec2 value ? value : new IntVec2(0, 0);
            if (existing.x >= size.x && existing.z >= size.z) return;
            previewMaxMapSizeField.SetValue(null, new IntVec2(
                Math.Max(existing.x, size.x), Math.Max(existing.z, size.z)));
        }

        private static bool IsRegionalPreviewStep(GenStepDef def)
        {
            return def == CARegionalDefOf.CA_RegionalProjection
                || def == CARegionalDefOf.CA_RegionalWorldLinks;
        }

        private static void PreviewCollectGenStepsPostfix(
            ref IEnumerable<GenStepDef> __result)
        {
            CARegionalPlan plan = CARegionalSetupSession.ActivePreviewPlan;
            if (!IsMapPreviewGenerating() || plan == null) return;
            // Ordered like a real generation, not appended after it.
            // Appending ran the projection LAST, so every earlier vanilla
            // consumer of per-cell mutators found the component inactive
            // and failed neutral -- which is exactly why peninsulas,
            // islands, wetlands, icebergs, and reservoirs existed on the
            // world card but never appeared in the preview texture.
            __result = (__result ?? Enumerable.Empty<GenStepDef>())
                .Concat(new[]
                {
                    CARegionalDefOf.CA_RegionalProjection,
                    CARegionalDefOf.CA_RegionalWorldLinks
                }).Where(def => def != null).Distinct()
                .OrderBy(def => def.order).ThenBy(def => def.index);
            Log.Message("[CA][Regional][Preview] bound persisted footprint "
                + (plan.regionalId ?? "unknown") + "; canonical landing "
                + plan.startTileId + "; members "
                + string.Join(",", plan.memberTileIds) + "; backing "
                + plan.BackingMapSize.x + "x" + plan.BackingMapSize.z
                + "; composition "
                + CARegionalGeographyContract.Inspect(plan).Signature
                + "; queued projection and world links before texture");
        }

        private static void PreviewMinimalComponentsPostfix(Map map)
        {
            CARegionalPlan plan = CARegionalSetupSession.ActivePreviewPlan;
            // Membership, not exact size: previews generate at reduced
            // resolution, and the size-equality guard kept this component
            // off every scaled preview -- leaving early mutator consumers
            // to fail neutral, which is why mutator content never appeared
            // in preview terrain.
            if (map == null || plan == null
                || plan.memberTileIds == null
                || !plan.memberTileIds.Contains(map.Tile.tileId)
                || map.GetComponent<CARegionalProjectionMapComponent>() != null)
                return;
            map.components.Add(new CARegionalProjectionMapComponent(map));
        }

        internal static void NotifyPreviewChanged()
        {
            try
            {
                previewRefreshMethod?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Log.Warning("[CA][Regional] could not refresh Map Preview: "
                    + ex.GetType().Name + ": " + ex.Message);
            }
        }

        internal static bool IsMapPreviewGenerating()
        {
            if (previewApiType == null)
                previewApiType = FindType("MapPreview.MapPreviewAPI");
            if (previewApiType == null) return false;
            try
            {
                if (previewGeneratingProperty == null)
                    previewGeneratingProperty = AccessTools.Property(
                        previewApiType, "IsGeneratingPreview");
                if (previewGeneratingProperty != null)
                    return (bool)previewGeneratingProperty.GetValue(null, null);
                if (previewGeneratingField == null)
                    previewGeneratingField = AccessTools.Field(
                        previewApiType, "IsGeneratingPreview");
                return previewGeneratingField != null
                    && (bool)previewGeneratingField.GetValue(null);
            }
            catch
            {
                return false;
            }
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type found = assemblies[i].GetType(fullName, false);
                if (found != null) return found;
            }
            return null;
        }
    }

    // Exact ownership of stock placed during settlement generation. Provider
    // allocation uses the spawned thing identity, never room proximity.
    public sealed class CAStartingStockRecord : IExposable
    {
        public int schemaVersion = 1;
        public int thingId = -1;
        public string thingDefName;
        public string providerIdentity;
        public int provisionArrangementKey;
        public int provisionNodeIndex = -1;
        public int count;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref thingId, "thingId", -1);
            Scribe_Values.Look(ref thingDefName, "thingDefName");
            Scribe_Values.Look(ref providerIdentity, "providerIdentity");
            Scribe_Values.Look(ref provisionArrangementKey,
                "provisionArrangementKey", 0);
            Scribe_Values.Look(ref provisionNodeIndex,
                "provisionNodeIndex", -1);
            Scribe_Values.Look(ref count, "count", 0);
        }
    }
#pragma warning restore CS0649

    public sealed class CARegionalSettlementRecord : IExposable
    {
        public const int CurrentSchemaVersion = 10;
        public int schemaVersion = CurrentSchemaVersion;
        public string regionalId;
        public string name;
        // Stable region identity. Moving the landing tile does not change it.
        public string regionKey;
        public int slot;
        public int mapSize;
        public int memberTileId = -1;
        public CASiteFactionLinks factionLinks = new CASiteFactionLinks();
        public int operationalRoleMask;
        public int residentPopulation = -1;
        public int landCapacity = -1;
        public int economicCapacity = -1;
        public int tradeConnectivity = -1;
        public int specialization = -1;
        public int historicalDevelopment = -1;
        public int urbanSupport = -1;
        public byte realizedRole;
        public byte realizedScale;
        public bool persistent = true;
        // Native owner reference. Null is valid when the site is explicitly
        // unaffiliated; the CA record remains the durable site owner.
        public Faction faction;
        public string generationFactionDefName;
        public CASiteLocalSocietyState localSociety;
        public string relationAtMaterialization;
        public int goodwillAtMaterialization;
        // Domain capability is a persisted evidence-backed read model. It is
        // reconciled only from actors, organizations, operations, knowledge,
        // and material state that already exist.
        public List<CASettlementCapabilityAssessment> capabilities =
            new List<CASettlementCapabilityAssessment>();
        public CellRect localRect = CellRect.Empty;
        public int lastMapId = -1;
        public int populationBaseline;
        public int populationCurrent;
        public int buildingCount;
        public int infrastructureCount;
        public int cultivatedPlantCount;
        public List<string> residentIds = new List<string>();
        // Infrastructure and the realized open settlement program are
        // separate from faction knowledge.
        public int accessInfrastructure;
        public int serviceInfrastructure;
        public int civicInfrastructure;
        // Population groups and provision arrangements are copied from the plan
        // and resolved into pawns, ideoligions, and organizations.
        public List<CASettlementPopulationGroup> populationGroups =
            new List<CASettlementPopulationGroup>();
        public List<CASettlementResidenceAssignment> residenceAssignments =
            new List<CASettlementResidenceAssignment>();
        public List<CAProvisionArrangement> provisionArrangements =
            new List<CAProvisionArrangement>();
        public List<CADomesticProvisionDemand> domesticProvisionDemands =
            new List<CADomesticProvisionDemand>();
        public List<CADomesticUnit> domesticUnits =
            new List<CADomesticUnit>();
        public List<CADomesticMembershipTransition>
            domesticMembershipTransitions =
                new List<CADomesticMembershipTransition>();
        public int nextDomesticUnitSequence = 1;
        public List<CASettlementOperationalFact> operationalFacts =
            new List<CASettlementOperationalFact>();
        public CASettlementProgram settlementProgram =
            new CASettlementProgram();
        public CACulture culture;
        public List<string> populationAssignments = new List<string>();
        // Material wealth and the era represented by the original buildings.
        public int wealth = -1;
        public int constructionEra = -1;
        // Status assignments persist after their first materialization.
        public List<string> statusAssignments = new List<string>();
        // Receipt of the canonical technological knowledge used at
        // materialization. It may resolve from the owning faction or from the
        // site itself; this receipt is not a second authority.
        public string technologicalKnowledgeId;
        public int technologicalKnowledgeRevision = -1;
        public int technologicalKnowledgeTier = -1;
        public int settlementForm = -1;
        public string generationSummary;
        // Receipt of the deterministic cultural read at materialization. The
        // live inspector may recompute from current saved facts as play changes.
        public string culturalExpressionSummary;
        public string culturalExpressionSourceSignature;
        public byte culturalExpressionStatus;
        public List<string> seededAssets = new List<string>();
        public List<CASettlementProgramAssetReceipt> programAssets =
            new List<CASettlementProgramAssetReceipt>();
        public List<CAStartingStockRecord> startingStock =
            new List<CAStartingStockRecord>();
        public int researchStock;
        public int researchMilestones;
        public int lastResearchActivityTick = -1;
        // Machine-readable settlement gates, paths, center, and facilities.
        public CASettlementLayout layout;
        public int firstMaterializationTick = -1;
        public int lastReconciliationTick = -1;
        public int materializationCount;
        public string materializationSummary;
        // Confirmed creation history is a distinct, durable authority receipt.
        // Later NPC development reads the institution and material settlement
        // that exist in play; it does not inherit creation feasibility.
        public string creationBehaviorKey;
        public int creationEpisodeId;
        public int creationAuthorityOrigin;
        public string creationAuthorityIdentity;
        public string creationOwner;
        public string creationTargetOrDemand;
        public int creationCreatedTick = -1;
        public string creationProposer;
        public string creationApprover;
        public string creationLaborSource;
        public List<string> creationBeneficiaries = new List<string>();
        public string creationCulturalBasis;
        public string creationPoliticalBasis;
        public bool creationAuthorized;
        public bool creationExecutable;
        public bool creationMaterialFeasible;
        public string creationProposalSignature;
        public bool creationSitingEvaluated;
        public bool creationSitingFeasible;
        public string creationBlocker;
        public string developmentBehaviorKey;
        public int developmentEpisodeId;
        public int developmentAuthorityOrigin;
        public string developmentAuthorityIdentity;
        public string developmentOwner;
        public string developmentProposer;
        public string developmentApprover;
        public string developmentLaborSource;
        public List<string> developmentBeneficiaries = new List<string>();
        public string developmentCulturalBasis;
        public string developmentPoliticalBasis;
        public string developmentTargetOrDemand;
        public int developmentCreatedTick = -1;
        public bool developmentAuthorized;
        public bool developmentExecutable;
        public bool developmentFundingFeasible;
        public bool developmentMaterialFeasible;
        public bool developmentSitingEvaluated;
        public bool developmentSitingFeasible;
        public List<int> developmentDemandKinds = new List<int>();
        public List<string> developmentAssetCandidates = new List<string>();
        public string developmentFundingBasis;
        public string developmentMaterialBasis;
        public string developmentProposalSignature;
        public string developmentBlocker;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref regionalId, "regionalId");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref regionKey, "regionKey");
            Scribe_Values.Look(ref slot, "slot", -1);
            Scribe_Values.Look(ref mapSize, "mapSize", 0);
            Scribe_Values.Look(ref memberTileId, "memberTileId", -1);
            int legacyFactionKey = -1;
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref legacyFactionKey, "factionKey", -1);
            Scribe_Deep.Look(ref factionLinks, "factionLinks");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (factionLinks == null)
                    factionLinks = new CASiteFactionLinks();
                if (legacyFactionKey >= 0
                    && factionLinks.ownership
                        == CASiteFactionReferenceKind.None)
                    factionLinks.SetRegionalOwner(legacyFactionKey);
            }
            Scribe_Values.Look(ref operationalRoleMask,
                "operationalRoleMask", 0);
            Scribe_Values.Look(ref residentPopulation,
                "residentPopulation", -1);
            Scribe_Values.Look(ref landCapacity, "landCapacity", -1);
            Scribe_Values.Look(ref economicCapacity,
                "economicCapacity", -1);
            Scribe_Values.Look(ref tradeConnectivity,
                "tradeConnectivity", -1);
            Scribe_Values.Look(ref specialization, "specialization", -1);
            Scribe_Values.Look(ref historicalDevelopment,
                "historicalDevelopment", -1);
            Scribe_Values.Look(ref urbanSupport, "urbanSupport", -1);
            Scribe_Values.Look(ref realizedRole, "realizedRole", (byte)0);
            Scribe_Values.Look(ref realizedScale, "realizedScale", (byte)0);
            Scribe_Values.Look(ref persistent, "persistent", true);
            Scribe_References.Look(ref faction, "faction");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref generationFactionDefName,
                    "factionDefName");
            Scribe_Values.Look(ref generationFactionDefName,
                "generationFactionDefName");
            Scribe_Deep.Look(ref localSociety, "localSociety");
            Scribe_Values.Look(ref relationAtMaterialization,
                "relationAtMaterialization");
            Scribe_Values.Look(ref goodwillAtMaterialization,
                "goodwillAtMaterialization", 0);
            Scribe_Collections.Look(ref capabilities, "capabilities",
                LookMode.Deep);
            Scribe_Values.Look(ref localRect, "localRect");
            Scribe_Values.Look(ref lastMapId, "lastMapId", -1);
            Scribe_Values.Look(ref populationBaseline, "populationBaseline", 0);
            Scribe_Values.Look(ref populationCurrent, "populationCurrent", 0);
            Scribe_Values.Look(ref buildingCount, "buildingCount", 0);
            Scribe_Values.Look(ref infrastructureCount,
                "infrastructureCount", 0);
            Scribe_Values.Look(ref cultivatedPlantCount,
                "cultivatedPlantCount", 0);
            Scribe_Collections.Look(ref residentIds, "residentIds",
                LookMode.Value);
            Scribe_Values.Look(ref accessInfrastructure,
                "accessInfrastructure", 0);
            Scribe_Values.Look(ref serviceInfrastructure,
                "serviceInfrastructure", 0);
            Scribe_Values.Look(ref civicInfrastructure,
                "civicInfrastructure", 0);
            Scribe_Collections.Look(ref populationGroups, "populationGroups", LookMode.Deep);
            Scribe_Collections.Look(ref residenceAssignments,
                "residenceAssignments", LookMode.Deep);
            Scribe_Collections.Look(ref provisionArrangements,
                "provisionArrangements",
                LookMode.Deep);
            Scribe_Collections.Look(ref domesticProvisionDemands,
                "domesticProvisionDemands", LookMode.Deep);
            Scribe_Collections.Look(ref domesticUnits, "domesticUnits",
                LookMode.Deep);
            Scribe_Collections.Look(ref domesticMembershipTransitions,
                "domesticMembershipTransitions", LookMode.Deep);
            Scribe_Values.Look(ref nextDomesticUnitSequence,
                "nextDomesticUnitSequence", 1);
            Scribe_Collections.Look(ref operationalFacts,
                "operationalFacts", LookMode.Deep);
            Scribe_Deep.Look(ref settlementProgram, "settlementProgram");
            Scribe_Deep.Look(ref culture, "culture");
            Scribe_Collections.Look(ref populationAssignments,
                "populationAssignments", LookMode.Value);
            Scribe_Values.Look(ref wealth, "wealth", -1);
            Scribe_Values.Look(ref constructionEra, "constructionEra", -1);
            Scribe_Collections.Look(ref statusAssignments,
                "statusAssignments", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Values.Look(ref technologicalKnowledgeId,
                    "factionKnowledgeId");
                Scribe_Values.Look(ref technologicalKnowledgeRevision,
                    "factionKnowledgeRevision", -1);
                Scribe_Values.Look(ref technologicalKnowledgeTier,
                    "factionKnowledgeTier", -1);
            }
            Scribe_Values.Look(ref technologicalKnowledgeId,
                "technologicalKnowledgeId");
            Scribe_Values.Look(ref technologicalKnowledgeRevision,
                "technologicalKnowledgeRevision", -1);
            Scribe_Values.Look(ref technologicalKnowledgeTier,
                "technologicalKnowledgeTier", -1);
            Scribe_Values.Look(ref settlementForm, "settlementForm", -1);
            Scribe_Values.Look(ref generationSummary, "generationSummary");
            Scribe_Values.Look(ref culturalExpressionSummary,
                "culturalExpressionSummary");
            Scribe_Values.Look(ref culturalExpressionSourceSignature,
                "culturalExpressionSourceSignature");
            Scribe_Values.Look(ref culturalExpressionStatus,
                "culturalExpressionStatus", (byte)0);
            Scribe_Collections.Look(ref seededAssets, "seededAssets",
                LookMode.Value);
            Scribe_Collections.Look(ref programAssets, "programAssets",
                LookMode.Deep);
            Scribe_Collections.Look(ref startingStock, "startingStock",
                LookMode.Deep);
            Scribe_Values.Look(ref researchStock, "researchStock", 0);
            Scribe_Values.Look(ref researchMilestones,
                "researchMilestones", 0);
            Scribe_Values.Look(ref lastResearchActivityTick,
                "lastResearchActivityTick", -1);
            Scribe_Deep.Look(ref layout, "layout");
            Scribe_Values.Look(ref firstMaterializationTick,
                "firstMaterializationTick", -1);
            Scribe_Values.Look(ref lastReconciliationTick,
                "lastReconciliationTick", -1);
            Scribe_Values.Look(ref materializationCount,
                "materializationCount", 0);
            Scribe_Values.Look(ref materializationSummary,
                "materializationSummary");
            Scribe_Values.Look(ref creationBehaviorKey,
                "creationBehaviorKey");
            Scribe_Values.Look(ref creationEpisodeId,
                "creationEpisodeId", 0);
            Scribe_Values.Look(ref creationAuthorityOrigin,
                "creationAuthorityOrigin", 0);
            Scribe_Values.Look(ref creationAuthorityIdentity,
                "creationAuthorityIdentity");
            Scribe_Values.Look(ref creationOwner, "creationOwner");
            Scribe_Values.Look(ref creationTargetOrDemand,
                "creationTargetOrDemand");
            Scribe_Values.Look(ref creationCreatedTick,
                "creationCreatedTick", -1);
            Scribe_Values.Look(ref creationProposer, "creationProposer");
            Scribe_Values.Look(ref creationApprover, "creationApprover");
            Scribe_Values.Look(ref creationLaborSource,
                "creationLaborSource");
            Scribe_Collections.Look(ref creationBeneficiaries,
                "creationBeneficiaries", LookMode.Value);
            Scribe_Values.Look(ref creationCulturalBasis,
                "creationCulturalBasis");
            Scribe_Values.Look(ref creationPoliticalBasis,
                "creationPoliticalBasis");
            Scribe_Values.Look(ref creationAuthorized,
                "creationAuthorized", false);
            Scribe_Values.Look(ref creationExecutable,
                "creationExecutable", false);
            Scribe_Values.Look(ref creationMaterialFeasible,
                "creationMaterialFeasible", false);
            Scribe_Values.Look(ref creationProposalSignature,
                "creationProposalSignature");
            Scribe_Values.Look(ref creationSitingEvaluated,
                "creationSitingEvaluated", false);
            Scribe_Values.Look(ref creationSitingFeasible,
                "creationSitingFeasible", false);
            Scribe_Values.Look(ref creationBlocker, "creationBlocker");
            Scribe_Values.Look(ref developmentBehaviorKey,
                "developmentBehaviorKey");
            Scribe_Values.Look(ref developmentEpisodeId,
                "developmentEpisodeId", 0);
            Scribe_Values.Look(ref developmentAuthorityOrigin,
                "developmentAuthorityOrigin", 0);
            Scribe_Values.Look(ref developmentAuthorityIdentity,
                "developmentAuthorityIdentity");
            Scribe_Values.Look(ref developmentOwner,
                "developmentOwner");
            Scribe_Values.Look(ref developmentProposer,
                "developmentProposer");
            Scribe_Values.Look(ref developmentApprover,
                "developmentApprover");
            Scribe_Values.Look(ref developmentLaborSource,
                "developmentLaborSource");
            Scribe_Collections.Look(ref developmentBeneficiaries,
                "developmentBeneficiaries", LookMode.Value);
            Scribe_Values.Look(ref developmentCulturalBasis,
                "developmentCulturalBasis");
            Scribe_Values.Look(ref developmentPoliticalBasis,
                "developmentPoliticalBasis");
            Scribe_Values.Look(ref developmentTargetOrDemand,
                "developmentTargetOrDemand");
            Scribe_Values.Look(ref developmentCreatedTick,
                "developmentCreatedTick", -1);
            Scribe_Values.Look(ref developmentAuthorized,
                "developmentAuthorized", false);
            Scribe_Values.Look(ref developmentExecutable,
                "developmentExecutable", false);
            Scribe_Values.Look(ref developmentFundingFeasible,
                "developmentFundingFeasible", false);
            Scribe_Values.Look(ref developmentMaterialFeasible,
                "developmentMaterialFeasible", false);
            Scribe_Values.Look(ref developmentSitingEvaluated,
                "developmentSitingEvaluated", false);
            Scribe_Values.Look(ref developmentSitingFeasible,
                "developmentSitingFeasible", false);
            Scribe_Collections.Look(ref developmentDemandKinds,
                "developmentDemandKinds", LookMode.Value);
            Scribe_Collections.Look(ref developmentAssetCandidates,
                "developmentAssetCandidates", LookMode.Value);
            Scribe_Values.Look(ref developmentFundingBasis,
                "developmentFundingBasis");
            Scribe_Values.Look(ref developmentMaterialBasis,
                "developmentMaterialBasis");
            Scribe_Values.Look(ref developmentProposalSignature,
                "developmentProposalSignature");
            Scribe_Values.Look(ref developmentBlocker,
                "developmentBlocker");
        }

        internal string TechnologicalKnowledgeLabel
        {
            get
            {
                return technologicalKnowledgeTier >= 0
                    ? "knowledge tier " + technologicalKnowledgeTier
                    : "unknown";
            }
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

        internal string CapabilityText()
        {
            return CASettlementCapabilityInspection.Summary(this);
        }

        internal string OperationalRoleText()
        {
            return CARegionalOperationalRoles.Summary(operationalRoleMask);
        }
    }

    public sealed class CARegionalWorldComponent : WorldComponent
    {
        private sealed class ReservationClaim
        {
            internal CARegionalPlan Region;
            internal int MemberIndex;
        }

        private int campaignSchemaVersion = 5;
        private int legacyAuthoringDataEpoch =
            CACampaignCompatibilityKernel.LegacyB10AuthoringEpoch;
        private List<CARegionalSettlementRecord> records =
            new List<CARegionalSettlementRecord>();
        private List<CARegionalPlan> regions = new List<CARegionalPlan>();
        // Runtime-only home for an explicitly armed developer exercise. It is
        // discoverable by map systems during the disposable run but is never
        // scribed into durable world state.
        private CARegionalPlan transientDeveloperExerciseRegion;
        private CARegionalWorldPolicy worldPolicy =
            new CARegionalWorldPolicy();
        // Groundwater settings fixed when the world is created.
        private CAGroundwaterTuning groundwater =
            new CAGroundwaterTuning();
        // THE WORLD-WIDE REGIONAL PARTITION: every eligible surface tile's
        // deterministic membership, minted once (world generation, or the
        // one-time migration of an older save) and persisted thereafter.
        // Registered plans REALIZE topology regions; they never replace the
        // partition as the membership truth for untouched land.
        private List<CARegionalTopologyRecord> topology =
            new List<CARegionalTopologyRecord>();
        // Derived lookup state, rebuilt from the persisted records.
        private Dictionary<int, CARegionalTopologyRecord> topologyByTile;
        private Dictionary<string, CARegionalTopologyRecord> topologyById;
        private Dictionary<string, List<string>> topologyNeighborCache;
        // Persistent world-settlement state: the world-facing record of who
        // founded each standing settlement, the coarse population and urban
        // class its ground supports, and when it was founded. Extended by
        // the distant-world founding cadence while the campaign runs.
        private List<CARegionalWorldSettlementState> worldSettlementStates =
            new List<CARegionalWorldSettlementState>();
        private Dictionary<int, CARegionalWorldSettlementState>
            worldSettlementByTile;
        private int initialWorldSettlementCount = -1;
        private int lastDistantFoundingCheckTick;
        // Persistent political state per multi-area region: the holder
        // set as last observed, plus the dated events of every change.
        private List<CARegionalPoliticalRecord> politicalRecords =
            new List<CARegionalPoliticalRecord>();
        private Dictionary<string, CARegionalPoliticalRecord>
            politicalById;
        private int lastPoliticalCheckTick;
        // Bumped whenever topology or world-settlement state changes, so
        // world layers can regenerate exactly then and never per-frame.
        private int worldStateRevision;

        internal int WorldStateRevision
        {
            get { return worldStateRevision; }
        }

        internal IReadOnlyList<CARegionalPoliticalRecord> PoliticalRecords
        {
            get { return politicalRecords; }
        }

        internal CARegionalPoliticalRecord PoliticalRecordFor(
            string regionId)
        {
            if (regionId == null) return null;
            if (politicalById == null)
            {
                politicalById = new Dictionary<string,
                    CARegionalPoliticalRecord>();
                foreach (CARegionalPoliticalRecord record in
                    politicalRecords
                    ?? new List<CARegionalPoliticalRecord>())
                    if (record?.regionId != null)
                        politicalById[record.regionId] = record;
            }
            politicalById.TryGetValue(regionId,
                out CARegionalPoliticalRecord found);
            return found;
        }

        internal void AddPoliticalRecord(
            CARegionalPoliticalRecord record)
        {
            if (record?.regionId == null) return;
            politicalRecords.Add(record);
            if (politicalById != null)
                politicalById[record.regionId] = record;
        }

        public CAGroundwaterTuning Groundwater
        {
            get
            {
                if (groundwater == null)
                    groundwater = new CAGroundwaterTuning();
                return groundwater;
            }
        }

        public CARegionalWorldComponent(World world) : base(world)
        {
            CARegionalSetupSession.ResetForNewWorld(world);
            // Page_CreateWorldParams owns the choice, but the world owns the
            // result. Snapshot it as the component is constructed so forced-map
            // scenarios and worldgen-time derived regions cannot fall back to a
            // fresh default merely because no starting-region page exists.
            if (Scribe.mode == LoadSaveMode.Inactive)
                worldPolicy = CAWorldTendenciesSession.Policy.Copy();
        }

        internal static CARegionalWorldComponent Current
        {
            get { return Verse.Find.World
                ?.GetComponent<CARegionalWorldComponent>(); }
        }

        internal IReadOnlyList<CARegionalSettlementRecord> Records
        {
            get { return records; }
        }

        internal IReadOnlyList<CARegionalPlan> Regions
        {
            get
            {
                return transientDeveloperExerciseRegion == null ? regions
                    : regions.Concat(new[]
                        { transientDeveloperExerciseRegion }).ToList();
            }
        }

        internal CARegionalWorldPolicy WorldPolicy
        {
            get { return worldPolicy ?? (worldPolicy =
                new CARegionalWorldPolicy()); }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref campaignSchemaVersion,
                "CA_regionalSchemaVersion", 0);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref legacyAuthoringDataEpoch,
                    "CA_authoringDataEpoch", 0);
            bool readable = CACampaignCompatibility.ShouldReadLiveState(
                "world.regional", campaignSchemaVersion,
                legacyAuthoringDataEpoch);
            if (readable)
            {
                Scribe_Collections.Look(ref records,
                    "CA_regionalSettlements", LookMode.Deep);
                Scribe_Collections.Look(ref regions,
                    "CA_regionalPlans", LookMode.Deep);
                Scribe_Deep.Look(ref worldPolicy, "CA_regionalWorldPolicy");
                Scribe_Deep.Look(ref groundwater, "CA_groundwaterTuning");
                // The transient developer-exercise plan round-trips through
                // its own transient slot so the benchmark matrix's
                // disposable save/reload is structurally complete: the plan
                // never enters the durable `regions` list, it restores as
                // transient, and further saves remain blocked after a
                // reload. Ordinary campaigns scribe null here.
                Scribe_Deep.Look(ref transientDeveloperExerciseRegion,
                    "CA_transientDeveloperExerciseRegion");
                Scribe_Collections.Look(ref topology,
                    "CA_regionalTopology", LookMode.Deep);
                Scribe_Collections.Look(ref worldSettlementStates,
                    "CA_worldSettlementStates", LookMode.Deep);
                Scribe_Values.Look(ref initialWorldSettlementCount,
                    "CA_initialWorldSettlementCount", -1);
                Scribe_Values.Look(ref lastDistantFoundingCheckTick,
                    "CA_lastDistantFoundingCheckTick", 0);
                Scribe_Collections.Look(ref politicalRecords,
                    "CA_regionalPoliticalRecords", LookMode.Deep);
                Scribe_Values.Look(ref lastPoliticalCheckTick,
                    "CA_lastPoliticalCheckTick", 0);
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && readable)
            {
                CACampaignCompatibility.CompleteOwnerLoad(
                    "world.regional", ref campaignSchemaVersion,
                    legacyAuthoringDataEpoch, ValidateCampaignState,
                    MigrateSupportedState);
                RebuildAcceptedReadModels();
                if (topology == null)
                    topology = new List<CARegionalTopologyRecord>();
                RebuildTopologyIndex();
                if (worldSettlementStates == null)
                    worldSettlementStates =
                        new List<CARegionalWorldSettlementState>();
                RebuildWorldSettlementIndex();
                if (politicalRecords == null)
                    politicalRecords =
                        new List<CARegionalPoliticalRecord>();
                politicalById = null;
            }
            base.ExposeData();
        }

        private string ValidateCampaignState()
        {
            return ValidateRegionalState(
                CARegionalPlan.CurrentSchemaVersion,
                CARegionalSettlementRecord.CurrentSchemaVersion,
                requireCurrentSiteState: true);
        }

        private string ValidateRegionalState(int expectedRegionSchema,
            int expectedRecordSchema, bool requireCurrentSiteState)
        {
            string structureFailure = ValidateOwnerStructure(
                expectedRegionSchema,
                CAPlayerFoundingPlan.CurrentSchemaVersion,
                expectedRecordSchema);
            if (!structureFailure.NullOrEmpty()) return structureFailure;
            if (requireCurrentSiteState)
            {
                for (int i = 0; i < regions.Count; i++)
                {
                    string siteFailure = CASiteAffiliationMigration
                        .ValidateCurrentRegionalPlan(regions[i],
                            requireCompleteSites: true);
                    if (!siteFailure.NullOrEmpty())
                        return "regional plan " + regions[i].regionalId
                            + " site state: " + siteFailure;
                }
                for (int i = 0; i < records.Count; i++)
                {
                    CARegionalPlan region = regions.FirstOrDefault(value =>
                        value != null && value.regionalId
                            == records[i]?.regionalId);
                    string siteFailure = CASiteAffiliationMigration
                        .ValidateCurrentRegionalRecord(records[i], region);
                    if (!siteFailure.NullOrEmpty())
                        return "regional settlement "
                            + (records[i]?.regionalId ?? "<missing>") + "#"
                            + (records[i]?.slot ?? -1) + " site state: "
                            + siteFailure;
                }
            }
            for (int i = 0; i < regions.Count; i++)
            {
                CARegionalPlan region = regions[i];
                for (int factionIndex = 0;
                    factionIndex < region.factions.Count; factionIndex++)
                {
                    CARegionalFactionPlan faction =
                        region.factions[factionIndex];
                    if (faction == null)
                        return "regional plan " + region.regionalId
                            + " has null faction " + factionIndex;
                    string cultureFailure = CACultureModel.ValidationFailure(
                        faction.culture, requireSubstantive: true);
                    if (!cultureFailure.NullOrEmpty())
                        return "regional faction " + faction.key
                            + " Culture: " + cultureFailure;
                    if (faction.politicalBeliefs == null
                        || faction.politicalBeliefs.schemaVersion
                            != CAPoliticalBeliefs.CurrentSchemaVersion)
                        return "regional faction " + faction.key
                            + " has a missing or incompatible Political Order";
                    string beliefFailure = CAPoliticalBeliefsModel
                        .ValidationFailure(faction.politicalBeliefs,
                            allowExactLegacy: false);
                    if (!beliefFailure.NullOrEmpty())
                        return "regional faction " + faction.key
                            + " Political Order: " + beliefFailure;
                    string technologyFailure = CATechnologicalKnowledgeModel
                        .ValidationFailure(faction.technologicalKnowledge);
                    if (!technologyFailure.NullOrEmpty())
                        return "regional faction " + faction.key
                            + " Technological Knowledge: "
                            + technologyFailure;
                    string orderFailure = CAPoliticalBeliefsModel
                        .ValidationFailure(faction.factionStructure);
                    if (!orderFailure.NullOrEmpty())
                        return "regional faction " + faction.key
                            + " represented institutions: " + orderFailure;
                }
                for (int settlementIndex = 0;
                    settlementIndex < region.settlements.Count;
                    settlementIndex++)
                {
                    CARegionalSettlementPlan settlement =
                        region.settlements[settlementIndex];
                    if (settlement == null)
                        return "regional plan " + region.regionalId
                            + " has null settlement " + settlementIndex;
                    string cultureFailure = CACultureModel.ValidationFailure(
                        settlement.localCulture,
                        requireSubstantive: true);
                    if (!cultureFailure.NullOrEmpty())
                        return "regional settlement " + settlement.slot
                            + " Culture: " + cultureFailure;
                }
                string foundingFailure = CAPlayerFoundingModel
                    .ValidationFailure(region.playerFounding,
                        requireConfirmed: true);
                if (!foundingFailure.NullOrEmpty())
                    return "regional plan " + region.regionalId
                        + " founding copy: " + foundingFailure;
            }
            for (int i = 0; i < records.Count; i++)
            {
                CARegionalSettlementRecord record = records[i];
                string key = (record.regionalId ?? "") + "#" + record.slot;
                string cultureFailure = CACultureModel.ValidationFailure(
                    record.culture, requireSubstantive: true);
                if (!cultureFailure.NullOrEmpty())
                    return "regional settlement " + key + " Culture: "
                        + cultureFailure;
                if (record.technologicalKnowledgeId.NullOrEmpty()
                    || record.technologicalKnowledgeRevision < 0
                    || record.technologicalKnowledgeTier < 0)
                    return "regional settlement " + key
                        + " has no technological-knowledge receipt";
            }
            return null;
        }

        private string ValidateOwnerStructure(int expectedRegionSchema,
            int expectedFoundingSchema =
                CAPlayerFoundingPlan.CurrentSchemaVersion,
            int expectedRecordSchema =
                CARegionalSettlementRecord.CurrentSchemaVersion)
        {
            if (regions == null || records == null || worldPolicy == null
                || groundwater == null)
                return "regional owner collections are missing";
            var regionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < regions.Count; i++)
            {
                CARegionalPlan region = regions[i];
                if (region == null) return "regional plan " + i + " is null";
                if (region.regionalId.NullOrEmpty()
                    || !regionIds.Add(region.regionalId))
                    return "regional plan identity "
                        + (region.regionalId ?? "<missing>")
                        + " is missing or duplicate";
                if (region.schemaVersion != expectedRegionSchema)
                    return "regional plan " + region.regionalId + " schema is "
                        + region.schemaVersion + ", expected "
                        + expectedRegionSchema;
                string collectionFailure = ValidatePlanCollections(region);
                if (!collectionFailure.NullOrEmpty())
                    return "regional plan " + region.regionalId + ": "
                        + collectionFailure;
                if (!CARegionalPlanUtility.TryValidateStableIdentities(region,
                        expectedRegionSchema, out string identityFailure))
                    return "regional plan " + region.regionalId + ": "
                        + identityFailure;
                if (region.playerFounding == null)
                    return "regional plan " + region.regionalId
                        + " founding copy is missing";
                if (region.playerFounding.schemaVersion
                    != expectedFoundingSchema)
                    return "regional founding-plan schema is "
                        + region.playerFounding.schemaVersion;
            }
            var recordIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < records.Count; i++)
            {
                CARegionalSettlementRecord record = records[i];
                if (record == null)
                    return "regional settlement record " + i + " is null";
                string key = (record.regionalId ?? "") + "#" + record.slot;
                if (record.regionalId.NullOrEmpty() || !recordIds.Add(key))
                    return "regional settlement identity " + key
                        + " is missing or duplicate";
                if (record.schemaVersion
                    != expectedRecordSchema)
                    return "regional settlement " + key + " schema is "
                        + record.schemaVersion + ", expected "
                        + expectedRecordSchema;
                string collectionFailure = ValidateRecordCollections(record);
                if (!collectionFailure.NullOrEmpty())
                    return "regional settlement " + key + ": "
                        + collectionFailure;
            }
            return null;
        }

        private static string ValidatePlanCollections(CARegionalPlan region)
        {
            if (region.memberTileIds == null
                || region.footprintTileIds == null
                || region.factions == null || region.settlements == null
                || region.relations == null || region.frontierHoldings == null
                || region.consumedSources == null || region.worldPolicy == null
                || region.groundwater == null)
                return "owned collections or policy state are missing";
            if (region.relations.Any(item => item == null)
                || region.frontierHoldings.Any(item => item == null))
                return "a relation or frontier holding is null";
            for (int i = 0; i < region.factions.Count; i++)
            {
                CARegionalFactionPlan faction = region.factions[i];
                if (faction == null) return "faction " + i + " is null";
                if (faction.factionStructure == null)
                    return "faction " + faction.key
                        + " represented-institution collection is missing";
                CASettlementAuthority authority =
                    (CASettlementAuthority)faction.settlementAuthority;
                if (!CARegionalSettlements.ActiveSettlementAuthorities
                        .Contains(authority))
                    return "faction " + faction.key
                        + " has invalid settlement authority "
                        + faction.settlementAuthority;
            }
            for (int i = 0; i < region.settlements.Count; i++)
            {
                CARegionalSettlementPlan settlement = region.settlements[i];
                if (settlement == null)
                    return "settlement " + i + " is null";
                string failure = ValidateSettlementPlanCollections(settlement);
                if (!failure.NullOrEmpty())
                    return "settlement " + settlement.slot + ": " + failure;
            }
            for (int i = 0; i < region.frontierHoldings.Count; i++)
                if (region.frontierHoldings[i].residentPawnIds == null)
                    return "frontier holding "
                        + region.frontierHoldings[i].key
                        + " resident collection is missing";
            return null;
        }

        private static string ValidateSettlementPlanCollections(
            CARegionalSettlementPlan settlement)
        {
            if (settlement.populationGroups == null
                || settlement.provisionArrangements == null
                || settlement.domesticProvisionDemands == null
                || settlement.operationalFacts == null
                || settlement.settlementProgram == null
                || settlement.settlementProgram.entries == null)
                return "owned composition or program state is missing";
            if (settlement.populationGroups.Any(item => item == null)
                || settlement.provisionArrangements.Any(item => item == null)
                || settlement.domesticProvisionDemands.Any(item => item == null)
                || settlement.operationalFacts.Any(item => item == null)
                || settlement.settlementProgram.entries.Any(item => item == null))
                return "an owned composition or program record is null";
            foreach (CASettlementOperationalFact fact in
                     settlement.operationalFacts)
                if (fact.culturalSubjects == null)
                    return "operational fact " + (fact.factKey ?? "<missing>")
                        + " has no Culture-subject collection";
            foreach (CASettlementProgramEntry entry in
                     settlement.settlementProgram.entries)
                if (entry.selectedCandidates == null
                    || entry.culturalSubjects == null)
                    return "program " + (entry.programKey ?? "<missing>")
                        + " has missing authoritative collections";
            return null;
        }

        private static string ValidateRecordCollections(
            CARegionalSettlementRecord record)
        {
            if (record.capabilities == null || record.residentIds == null
                || record.populationGroups == null
                || record.residenceAssignments == null
                || record.provisionArrangements == null
                || record.domesticProvisionDemands == null
                || record.domesticUnits == null
                || record.domesticMembershipTransitions == null
                || record.operationalFacts == null
                || record.populationAssignments == null
                || record.statusAssignments == null
                || record.seededAssets == null || record.programAssets == null
                || record.startingStock == null
                || record.creationBeneficiaries == null
                || record.developmentBeneficiaries == null
                || record.developmentDemandKinds == null
                || record.developmentAssetCandidates == null
                || record.settlementProgram == null
                || record.settlementProgram.entries == null)
                return "owned collections or settlement program are missing";
            if (record.capabilities.Any(item => item == null)
                || record.populationGroups.Any(item => item == null)
                || record.residenceAssignments.Any(item => item == null)
                || record.provisionArrangements.Any(item => item == null)
                || record.domesticProvisionDemands.Any(item => item == null)
                || record.domesticUnits.Any(item => item == null)
                || record.domesticMembershipTransitions.Any(item => item == null)
                || record.operationalFacts.Any(item => item == null)
                || record.programAssets.Any(item => item == null)
                || record.startingStock.Any(item => item == null)
                || record.settlementProgram.entries.Any(item => item == null))
                return "an owned settlement record is null";
            foreach (CASettlementCapabilityAssessment capability in
                     record.capabilities)
                if (capability.actorIdentities == null
                    || capability.organizationIdentities == null
                    || capability.operationIdentities == null
                    || capability.knowledgeEvidence == null
                    || capability.materialEvidence == null
                    || capability.historicalEvidence == null)
                    return "capability " + (capability.domain ?? "<missing>")
                        + " has missing evidence collections";
            foreach (CADomesticUnit unit in record.domesticUnits)
            {
                if (unit.sharedProvisionNodeKeys == null
                    || unit.memberships == null)
                    return "domestic unit "
                        + (unit.unitIdentity ?? "<missing>")
                        + " has missing owned collections";
                if (unit.memberships.Any(item => item == null))
                    return "domestic unit "
                        + (unit.unitIdentity ?? "<missing>")
                        + " has a null membership";
            }
            int maxFormationSequence = record.domesticUnits.Count == 0 ? 0
                : record.domesticUnits.Max(unit => unit.formationSequence);
            if (record.nextDomesticUnitSequence <= maxFormationSequence)
                return "next domestic-unit sequence does not follow persisted "
                    + "unit " + maxFormationSequence;
            foreach (CASettlementOperationalFact fact in
                     record.operationalFacts)
                if (fact.culturalSubjects == null)
                    return "operational fact " + (fact.factKey ?? "<missing>")
                        + " has no Culture-subject collection";
            foreach (CASettlementProgramEntry entry in
                     record.settlementProgram.entries)
                if (entry.selectedCandidates == null
                    || entry.culturalSubjects == null)
                    return "program " + (entry.programKey ?? "<missing>")
                        + " has missing authoritative collections";
            if (record.layout != null
                && (record.layout.gates == null
                    || record.layout.gateWidths == null
                    || record.layout.ways == null
                    || record.layout.facilityKinds == null
                    || record.layout.facilityCells == null
                    || record.layout.roads == null
                    || record.layout.roomCells == null
                    || record.layout.roomRoles == null
                    || record.layout.utilities == null
                    || record.layout.utilityKinds == null
                    || record.layout.approaches == null))
                return "settlement layout has missing owned collections";
            return null;
        }

        // Derived views are rebuilt only after the complete persisted owner
        // graph has passed compatibility migration and validation. No load-time
        // path may manufacture missing authoritative campaign state.
        private void RebuildAcceptedReadModels()
        {
            for (int i = 0; i < records.Count; i++)
            {
                CARegionalSettlementRecord record = records[i];
                CASettlementResidenceState.RebuildReadModel(record);
                CASettlementProgramAssets.SyncDerivedViews(record);
                CACombatIntent.ObserveEpisode(record.creationEpisodeId);
                CACombatIntent.ObserveEpisode(record.developmentEpisodeId);
            }
        }

        private string MigrateSupportedState()
        {
            if (campaignSchemaVersion != 2
                && campaignSchemaVersion != 3
                && campaignSchemaVersion != 4)
                return "regional owner schema " + campaignSchemaVersion
                    + " has no supported migration";
            int expectedRegionSchema = campaignSchemaVersion == 2 ? 13
                : campaignSchemaVersion == 3 ? 14 : 15;
            int expectedFoundingSchema = campaignSchemaVersion == 2 ? 3 : 4;
            int expectedRecordSchema = campaignSchemaVersion == 2 ? 8 : 9;
            int savedRegionSchema = regions.Count == 0
                ? expectedRegionSchema
                : regions[0]?.schemaVersion ?? -1;
            if (savedRegionSchema != expectedRegionSchema)
                return "regional plan schema " + savedRegionSchema
                    + " has no supported migration from regional owner "
                    + campaignSchemaVersion;
            string structureFailure = ValidateOwnerStructure(
                savedRegionSchema, expectedFoundingSchema,
                expectedRecordSchema);
            if (!structureFailure.NullOrEmpty()) return structureFailure;
            if (campaignSchemaVersion == 4)
                return MigrateB16SiteState(savedRegionSchema,
                    expectedRecordSchema);
            var commits = new List<Action>();
            var knowledgeByFaction =
                new Dictionary<string, CATechnologicalKnowledge>(
                    StringComparer.Ordinal);
            for (int regionIndex = 0; regionIndex < regions.Count;
                regionIndex++)
            {
                CARegionalPlan region = regions[regionIndex];
                if (region == null)
                    return "regional plan " + regionIndex + " is null";
                if (region.schemaVersion != savedRegionSchema)
                    return "regional plan " + region.regionalId + " schema is "
                        + region.schemaVersion + ", expected "
                        + savedRegionSchema;
                if (region.factions == null || region.settlements == null)
                    return "regional plan " + region.regionalId
                        + " has missing social collections";
                if (!CASiteAffiliationMigration.TryPrepareRegionalPlan(
                        region, savedRegionSchema,
                        requireCompleteSites: true,
                        out List<Action> siteCommits,
                        out string siteFailure))
                    return "regional plan " + region.regionalId + ": "
                        + siteFailure;
                commits.AddRange(siteCommits);
                foreach (CARegionalFactionPlan faction in region.factions)
                {
                    if (faction == null)
                        return "regional plan " + region.regionalId
                            + " has a null faction";
                    if (!CACultureModel.TryUpgradeToCurrent(faction.culture,
                            out CACulture culture,
                            out string cultureFailure))
                        return "regional faction " + faction.key
                            + " Culture: " + cultureFailure;
                    if (!CAPoliticalBeliefsModel.TryUpgradeFromB10(
                            faction.politicalBeliefs,
                            out CAPoliticalBeliefs beliefs,
                            out string beliefFailure))
                        return "regional faction " + faction.key
                            + " Political Order: " + beliefFailure;
                    if (!CAPoliticalBeliefsModel
                            .TryUpgradeMechanismsFromB10(
                                faction.factionStructure,
                                out List<CAAxisEntry> order,
                                out string orderFailure))
                        return "regional faction " + faction.key
                            + " represented institutions: " + orderFailure;
                    CATechnologicalKnowledge technology =
                        faction.technologicalKnowledge?.Copy()
                            ?? new CATechnologicalKnowledge();
                    CATechnologicalKnowledgeModel.SeedFromEngineTemplate(
                        technology, faction.ResolvedFactionDef,
                        "regional:" + region.regionalId + ":faction:"
                            + faction.key + ":technology");
                    CATechnologicalKnowledgeModel.Ensure(technology,
                        "regional:" + region.regionalId + ":faction:"
                            + faction.key + ":technology");
                    string technologyFailure =
                        CATechnologicalKnowledgeModel.ValidationFailure(
                            technology);
                    if (!technologyFailure.NullOrEmpty())
                        return "regional faction " + faction.key
                            + " Technological Knowledge: "
                            + technologyFailure;
                    knowledgeByFaction[(region.regionalId ?? "") + "#"
                        + faction.key] = technology;
                    CARegionalFactionPlan target = faction;
                    commits.Add(() =>
                    {
                        target.culture = culture;
                        target.politicalBeliefs = beliefs;
                        target.technologicalKnowledge = technology;
                        target.factionStructure = order;
                    });
                }
                foreach (CARegionalSettlementPlan settlement in
                         region.settlements)
                {
                    if (settlement == null)
                        return "regional plan " + region.regionalId
                            + " has a null settlement";
                    if (!CACultureModel.TryUpgradeToCurrent(
                            settlement.localCulture,
                            out CACulture localCulture,
                            out string localFailure))
                        return "regional settlement " + settlement.slot
                            + " Culture: " + localFailure;
                    CARegionalSettlementPlan target = settlement;
                    commits.Add(() => target.localCulture = localCulture);
                }
                if (region.playerFounding == null)
                    return "regional plan " + region.regionalId
                        + " founding copy is missing";
                if (region.playerFounding.schemaVersion
                    != expectedFoundingSchema)
                    return "regional founding-plan schema is "
                        + region.playerFounding.schemaVersion;
                if (!CACultureModel.TryUpgradeToCurrent(
                        region.playerFounding.culture,
                        out CACulture foundingCulture,
                        out string foundingCultureFailure))
                    return "regional founding Culture: "
                        + foundingCultureFailure;
                if (!CAPoliticalBeliefsModel.TryUpgradeFromB10(
                        region.playerFounding.politicalBeliefs,
                        out CAPoliticalBeliefs foundingBeliefs,
                        out string foundingBeliefFailure))
                    return "regional founding Political Order: "
                        + foundingBeliefFailure;
                CAPlayerFoundingPlan foundingCandidate =
                    region.playerFounding.Copy();
                foundingCandidate.culture = foundingCulture;
                foundingCandidate.politicalBeliefs = foundingBeliefs;
                Faction player = Verse.Find.World?.factionManager?.OfPlayer;
                CATechnologicalKnowledgeModel.SeedFromEngineTemplate(
                    foundingCandidate.technologicalKnowledge,
                    player?.def,
                    "regional:" + region.regionalId
                        + ":player-founding:technology");
                CATechnologicalKnowledgeModel.Ensure(
                    foundingCandidate.technologicalKnowledge,
                    "regional:" + region.regionalId
                        + ":player-founding:technology");
                foundingCandidate.schemaVersion =
                    CAPlayerFoundingPlan.CurrentSchemaVersion;
                string foundingFailure = CAPlayerFoundingModel
                    .ValidationFailure(foundingCandidate,
                        requireConfirmed: true);
                if (!foundingFailure.NullOrEmpty())
                    return "regional founding copy: " + foundingFailure;
                CARegionalPlan targetRegion = region;
                commits.Add(() =>
                {
                    targetRegion.playerFounding = foundingCandidate;
                    targetRegion.schemaVersion =
                        CARegionalPlan.CurrentSchemaVersion;
                });
            }
            foreach (CARegionalSettlementRecord record in records)
            {
                if (record == null) return "regional settlement record is null";
                CARegionalPlan recordRegion = regions.FirstOrDefault(value =>
                    value != null && value.regionalId == record.regionalId);
                if (recordRegion == null)
                    return "regional settlement record " + record.regionalId
                        + "#" + record.slot + " has no regional plan";
                if (!CASiteAffiliationMigration.TryPrepareRegionalRecord(
                        record, recordRegion, out Action siteCommit,
                        out string siteFailure))
                    return siteFailure;
                commits.Add(siteCommit);
                if (!CACultureModel.TryUpgradeToCurrent(record.culture,
                        out CACulture culture, out string cultureFailure))
                    return "regional settlement record " + record.regionalId
                        + "#" + record.slot + " Culture: " + cultureFailure;
                CATechnologicalKnowledge technology = null;
                if (record.localSociety?.technologicalKnowledge?.domains
                        ?.Count > 0)
                    technology = record.localSociety
                        .technologicalKnowledge;
                CAFactionState liveState = record.faction == null ? null
                    : CAFactionStateWorldComponent.Current?.Find(
                        record.faction);
                if (liveState?.technologicalKnowledge?.domains?.Count > 0)
                    technology = liveState.technologicalKnowledge;
                if (technology == null)
                    knowledgeByFaction.TryGetValue(
                        (record.regionalId ?? "") + "#"
                            + record.OwningFactionKey,
                        out technology);
                if (technology == null)
                    return "regional settlement record " + record.regionalId
                        + "#" + record.slot
                        + " cannot resolve its Technological Knowledge";
                CARegionalSettlementRecord target = record;
                commits.Add(() =>
                {
                    target.culture = culture;
                    target.technologicalKnowledgeId = technology.id;
                    target.technologicalKnowledgeRevision = technology.revision;
                    target.technologicalKnowledgeTier =
                        CATechnologicalKnowledgeModel.CompatibilityTier(
                            technology);
                    target.schemaVersion =
                        CARegionalSettlementRecord.CurrentSchemaVersion;
                });
            }
            foreach (Action commit in commits) commit();
            return null;
        }

        private string MigrateB16SiteState(int expectedRegionSchema,
            int expectedRecordSchema)
        {
            string retainedFailure = ValidateRegionalState(
                expectedRegionSchema, expectedRecordSchema,
                requireCurrentSiteState: false);
            if (!retainedFailure.NullOrEmpty()) return retainedFailure;

            var commits = new List<Action>();
            for (int i = 0; i < regions.Count; i++)
            {
                CARegionalPlan region = regions[i];
                if (!CASiteAffiliationMigration.TryPrepareRegionalPlan(
                        region, expectedRegionSchema,
                        requireCompleteSites: true,
                        out List<Action> siteCommits,
                        out string siteFailure))
                    return "regional plan " + region.regionalId + ": "
                        + siteFailure;
                commits.AddRange(siteCommits);
                commits.Add(() => region.schemaVersion =
                    CARegionalPlan.CurrentSchemaVersion);
            }
            for (int i = 0; i < records.Count; i++)
            {
                CARegionalSettlementRecord record = records[i];
                CARegionalPlan region = regions.FirstOrDefault(value =>
                    value != null && value.regionalId == record.regionalId);
                if (region == null)
                    return "regional settlement " + record.regionalId + "#"
                        + record.slot + " has no owning regional plan";
                if (!CASiteAffiliationMigration.TryPrepareRegionalRecord(
                        record, region, out Action recordCommit,
                        out string recordFailure))
                    return recordFailure;
                commits.Add(recordCommit);
            }
            foreach (Action commit in commits) commit();
            return null;
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            if (!fromLoad)
            {
                worldPolicy = CAWorldTendenciesSession.Policy.Copy();
                // The world's one frequency roll happens at generation; the
                // snapshot above must carry it rather than re-deriving it
                // lazily at first materialization. The derivation is
                // seed-fixed, so this re-assertion after the snapshot equals
                // the value the topology partition consumed.
                worldPolicy.ResolveStitchedRegionFrequency();
            }
            // A new world is a new set of engine-root receipts. Without this
            // the once-per-subject guard would silence the second world played
            // in a session.
            CARegionalEngineRoot.ForgetAnnouncements();
            if (!fromLoad)
                return;
            // Saves created before the world-wide partition existed receive
            // it exactly once here, with registered regional footprints
            // pre-owned so realized ground is never double-claimed.
            EnsureTopology("post-load migration");
            if (topology.Count > 0 && worldSettlementStates.Count == 0)
                RebuildWorldSettlementStates("post-load migration");
            if (regions == null) return;
            if (!ReconcileReservationRegistry(out string failure))
                throw new InvalidOperationException(
                    "Regional reservation registry is invalid: " + failure);
        }

        // ---- Persistent world-settlement state ----

        internal IReadOnlyList<CARegionalWorldSettlementState>
            WorldSettlementStates
        {
            get { return worldSettlementStates; }
        }

        internal CARegionalWorldSettlementState WorldSettlementStateAt(
            int tileId)
        {
            if (worldSettlementByTile == null)
                RebuildWorldSettlementIndex();
            return worldSettlementByTile.TryGetValue(tileId,
                out CARegionalWorldSettlementState state) ? state : null;
        }

        internal void RebuildWorldSettlementIndex()
        {
            worldSettlementByTile =
                new Dictionary<int, CARegionalWorldSettlementState>();
            foreach (CARegionalWorldSettlementState state in
                worldSettlementStates
                ?? new List<CARegionalWorldSettlementState>())
                if (state != null && state.tileId >= 0)
                    worldSettlementByTile[state.tileId] = state;
        }

        // Rebuild state rows against the settlements actually standing:
        // surviving tiles keep their founding record, new settlements gain
        // one, and rows for absorbed or destroyed settlements retire with
        // their world objects (a materialized settlement's truth lives in
        // its regional record instead).
        internal void RebuildWorldSettlementStates(string reason)
        {
            if (worldSettlementByTile == null)
                RebuildWorldSettlementIndex();
            int seed = world?.info?.Seed ?? 0;
            var rebuilt = new List<CARegionalWorldSettlementState>();
            List<Settlement> settlements =
                Verse.Find.WorldObjects?.Settlements;
            foreach (Settlement settlement in settlements
                ?? new List<Settlement>())
            {
                if (settlement == null
                    || settlement.Faction?.IsPlayer == true
                    || settlement.Tile.Layer
                        != Verse.Find.WorldGrid.Surface) continue;
                CARegionalWorldSettlementState prior =
                    WorldSettlementStateAt(settlement.Tile.tileId);
                CARegionalWorldSettlementState state =
                    CARegionalWorldSettlements.BuildState(seed, settlement,
                        prior?.foundedAbsTick ?? 0);
                rebuilt.Add(state);
            }
            // This runs on a play-time cadence as well as at generation,
            // so it must not report change that did not happen: the
            // world-state revision drives world-layer mesh rebuilds, and
            // bumping it unconditionally would regenerate every region
            // border on the globe once a day for nothing.
            bool changed = worldSettlementStates == null
                || worldSettlementStates.Count != rebuilt.Count;
            if (!changed)
                for (int i = 0; i < rebuilt.Count && !changed; i++)
                {
                    CARegionalWorldSettlementState now = rebuilt[i];
                    CARegionalWorldSettlementState before =
                        WorldSettlementStateAt(now.tileId);
                    changed = before == null
                        || before.factionLoadId != now.factionLoadId
                        || before.urbanClass != now.urbanClass
                        || before.population != now.population;
                }
            worldSettlementStates = rebuilt;
            RebuildWorldSettlementIndex();
            if (initialWorldSettlementCount < 0)
                initialWorldSettlementCount = rebuilt.Count;
            if (!changed) return;
            worldStateRevision++;
            Log.Message("[CA][WorldSettlements] " + rebuilt.Count
                + " world settlement states ("
                + rebuilt.Count(state => state.urbanClass >= 4)
                + " town-or-larger) rebuilt at " + reason);
            CARegionalPoliticalLedger.Rebuild(this, reason);
        }

        // THE DISTANT WORLD KEEPS MOVING. Off-map activity's world-facing
        // consumer: at a cadence owned here, distant factions found new
        // settlements through the same placement scoring the world was
        // generated with. The change is persistent, visible on the globe,
        // and carries its founding date.
        private const int DistantFoundingPeriodTicks = 900000; // 15 days

        // Vanilla play changes holders without touching CA state - a
        // settlement captured, destroyed, or defected. A daily ledger
        // pass catches those; every CA-side mutation rebuilds directly.
        private const int PoliticalCheckPeriodTicks = 60000;

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            if (topology == null || topology.Count == 0) return;
            int now = Verse.Find.TickManager?.TicksGame ?? 0;
            if (now - lastPoliticalCheckTick >= PoliticalCheckPeriodTicks)
            {
                lastPoliticalCheckTick = now;
                // Settlements are captured, destroyed, and founded by
                // ordinary play, none of which told this component
                // anything. Without a play-time pass the world's
                // settlement records kept the owner and standing they
                // were generated with, and the inspect line went stale
                // the first time a settlement changed hands. The
                // rebuild is silent when nothing actually differs.
                RebuildWorldSettlementStates("daily check");
                // Relations can turn a shared region contested without
                // any settlement changing, so the ledger runs on its
                // own regardless of whether the states moved.
                CARegionalPoliticalLedger.Rebuild(this, "daily check");
            }
            if (now - lastDistantFoundingCheckTick
                < DistantFoundingPeriodTicks) return;
            lastDistantFoundingCheckTick = now;
            float rate = WorldPolicy.distantFoundingRate;
            int period = now / DistantFoundingPeriodTicks;
            if (!CAWorldTendencyCausalKernel.DistantFoundingRoll(
                    world?.info?.Seed ?? 0, period, rate)) return;
            PlanetLayer surface = Verse.Find.WorldGrid?.Surface;
            if (surface == null) return;
            List<Settlement> standing = Verse.Find.WorldObjects?.Settlements
                ?.Where(item => item != null
                    && item.Tile.Layer == surface
                    && item.Faction?.IsPlayer != true).ToList();
            if (standing == null) return;
            if (initialWorldSettlementCount > 0 && standing.Count
                >= (int)Math.Ceiling(initialWorldSettlementCount * 1.5f))
                return;
            List<Faction> eligible = Verse.Find.World.factionManager
                .AllFactionsListForReading.Where(faction =>
                    !faction.def.isPlayer && !faction.Hidden
                    && !faction.temporary && !faction.defeated
                    && faction.def.settlementGenerationWeight > 0f).ToList();
            if (eligible.Count == 0) return;
            var represented = new HashSet<Faction>(standing
                .Select(item => item.Faction).Where(item => item != null));
            int targetDistinct = CAWorldTendencyCausalKernel
                .SourceVarietyTargetDistinct(standing.Count + 1,
                    eligible.Count, WorldPolicy.reallocationSourceVariety);
            Faction founder = CARegionalWorldSettlements.ChooseFaction(
                eligible, represented, targetDistinct);
            PlanetTile tile = CARegionalWorldSettlements.PlaceSettlementTile(
                surface, founder, WorldPolicy.settlementConcentration,
                standing.Select(item => item.Tile).ToList());
            if (!tile.Valid) return;
            // Never found inside a registered region's reserved footprint:
            // realized ground is governed by its regional plan.
            if (FindRegionContaining(tile) != null) return;
            WorldObject worldObject = WorldObjectMaker.MakeWorldObject(
                surface.Def.SettlementWorldObjectDef);
            worldObject.SetFaction(founder);
            worldObject.Tile = tile;
            if (worldObject is INameableWorldObject nameable)
                nameable.Name = SettlementNameGenerator
                    .GenerateSettlementName(worldObject);
            Verse.Find.WorldObjects.Add(worldObject);
            if (worldObject is Settlement founded)
            {
                CARegionalWorldSettlementState state =
                    CARegionalWorldSettlements.BuildState(
                        world?.info?.Seed ?? 0, founded, now);
                worldSettlementStates.Add(state);
                RebuildWorldSettlementIndex();
                worldStateRevision++;
                CARegionalPoliticalLedger.Rebuild(this,
                    "distant founding");
                Log.Message("[CA][WorldSettlements] distant founding: "
                    + founded.LabelCap + " (" + founder.Name + ") at tile "
                    + tile.tileId + ", " + state.UrbanWord
                    + "; distant founding rate " + rate.ToString("F2"));
            }
        }

        // ---- World-wide regional topology ----

        internal IReadOnlyList<CARegionalTopologyRecord> Topology
        {
            get { return topology; }
        }

        internal void EnsureTopology(string reason)
        {
            if (topology != null && topology.Count > 0) return;
            if (world?.grid?.Surface == null)
            {
                Log.Warning("[CA][Topology] cannot build the partition ("
                    + reason + "): no surface grid");
                return;
            }
            var preAssigned = new HashSet<int>();
            foreach (CARegionalPlan region in Regions)
                if (region?.ReservedTileIds != null)
                    foreach (int id in region.ReservedTileIds)
                        preAssigned.Add(id);
            // The partition is the substrate everything regional stands
            // on, and this runs inside world generation. Every consumer
            // already gates on a non-empty topology and falls through to
            // vanilla behavior without one, so a failure here degrades
            // the world to non-regional rather than refusing to create
            // a world at all.
            var clock = System.Diagnostics.Stopwatch.StartNew();
            string receipt;
            try
            {
                topology = CARegionalTopologyBuilder.Build(world,
                    WorldPolicy, preAssigned, out receipt);
            }
            catch (Exception ex)
            {
                topology = new List<CARegionalTopologyRecord>();
                RebuildTopologyIndex();
                Log.Error("[CA][Topology] the partition could not be "
                    + "built (" + reason + "): " + ex
                    + "\nThis world continues WITHOUT regional topology: "
                    + "settlement placement, region selection, and the "
                    + "regional viewport all fall through to vanilla "
                    + "behavior. Regional tendencies will have no effect "
                    + "in this world.");
                return;
            }
            RebuildTopologyIndex();
            clock.Stop();
            Log.Message(receipt + "; built at " + reason + " in "
                + clock.ElapsedMilliseconds + " ms");
        }

        internal void RebuildTopologyIndex()
        {
            worldStateRevision++;
            topologyByTile =
                new Dictionary<int, CARegionalTopologyRecord>();
            topologyById =
                new Dictionary<string, CARegionalTopologyRecord>();
            topologyNeighborCache = null;
            foreach (CARegionalTopologyRecord record in topology
                ?? new List<CARegionalTopologyRecord>())
            {
                if (record?.memberTileIds == null
                    || string.IsNullOrEmpty(record.regionId)) continue;
                topologyById[record.regionId] = record;
                foreach (int id in record.memberTileIds)
                    topologyByTile[id] = record;
            }
        }

        internal CARegionalTopologyRecord TopologyRecordAt(PlanetTile tile)
        {
            if (!tile.Valid) return null;
            return TopologyRecordAt(tile.tileId);
        }

        internal CARegionalTopologyRecord TopologyRecordAt(int tileId)
        {
            if (topologyByTile == null) RebuildTopologyIndex();
            return topologyByTile.TryGetValue(tileId,
                out CARegionalTopologyRecord record) ? record : null;
        }

        internal CARegionalTopologyRecord TopologyRecordById(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return null;
            if (topologyById == null) RebuildTopologyIndex();
            return topologyById.TryGetValue(regionId,
                out CARegionalTopologyRecord record) ? record : null;
        }

        // Region adjacency, derived from persisted membership: two regions
        // neighbor when any of their member tiles touch. Derived state is
        // rebuilt rather than scribed; membership is the durable truth.
        internal IReadOnlyList<string> TopologyNeighborsOf(
            CARegionalTopologyRecord record)
        {
            if (record?.memberTileIds == null
                || string.IsNullOrEmpty(record.regionId))
                return Array.Empty<string>();
            if (topologyNeighborCache == null)
                topologyNeighborCache =
                    new Dictionary<string, List<string>>();
            if (topologyNeighborCache.TryGetValue(record.regionId,
                    out List<string> cached))
                return cached;
            var neighbors = new List<string>();
            var seen = new HashSet<string> { record.regionId };
            var scratch = new List<PlanetTile>(8);
            PlanetLayer surface = world?.grid?.Surface;
            if (surface != null)
                foreach (int memberId in record.memberTileIds)
                {
                    scratch.Clear();
                    surface.GetTileNeighbors(
                        new PlanetTile(memberId, surface), scratch);
                    foreach (PlanetTile touch in scratch)
                    {
                        CARegionalTopologyRecord other =
                            TopologyRecordAt(touch);
                        if (other == null || !seen.Add(other.regionId))
                            continue;
                        neighbors.Add(other.regionId);
                    }
                }
            neighbors.Sort(StringComparer.Ordinal);
            topologyNeighborCache[record.regionId] = neighbors;
            return neighbors;
        }

        // An authored footprint is canonical geometry: carving it out of
        // the partition is the ONE mutation topology supports. Affected
        // records lose the carved tiles; remainders re-form as connected
        // regions (the largest keeps the identity, splinters mint stable
        // new identities from their lowest member).
        internal void CarveTopologyForRegion(CARegionalPlan plan)
        {
            if (plan?.ReservedTileIds == null || topology == null
                || topology.Count == 0) return;
            // A plan realizing an existing topology region inherits its
            // identity, and the partition already states this membership -
            // but only when the membership genuinely matches. The guard
            // used to key on the id alone, so a plan that adopted an id
            // while covering different ground would have left the record
            // asserting tiles the plan had taken. Everything downstream
            // that treats one regionId as one region depends on this.
            if (!string.IsNullOrEmpty(plan.regionalId))
            {
                CARegionalTopologyRecord sameId =
                    TopologyRecordById(plan.regionalId);
                if (sameId?.memberTileIds != null
                    && new HashSet<int>(sameId.memberTileIds)
                        .SetEquals(plan.ReservedTileIds))
                    return;
            }
            var carved = new HashSet<int>(plan.ReservedTileIds);
            var affected = new List<CARegionalTopologyRecord>();
            foreach (int id in carved)
            {
                CARegionalTopologyRecord record = TopologyRecordAt(id);
                if (record != null && !affected.Contains(record))
                    affected.Add(record);
            }
            if (affected.Count == 0) return;
            PlanetLayer surface = world?.grid?.Surface;
            var scratch = new List<PlanetTile>(8);
            IReadOnlyList<int> NeighborsOf(int id)
            {
                if (surface == null) return Array.Empty<int>();
                scratch.Clear();
                surface.GetTileNeighbors(new PlanetTile(id, surface),
                    scratch);
                return scratch.Select(tile => tile.tileId).ToList();
            }
            int worldSeed = world?.info?.Seed ?? 0;
            foreach (CARegionalTopologyRecord record in affected)
            {
                List<int> remaining = record.memberTileIds
                    .Where(id => !carved.Contains(id)).ToList();
                if (remaining.Count == 0)
                {
                    topology.Remove(record);
                    continue;
                }
                List<List<int>> components = CARegionalTopologyKernel
                    .SplitComponents(remaining, NeighborsOf);
                record.memberTileIds = components[0];
                if (!record.memberTileIds.Contains(record.rootTileId))
                    record.rootTileId = record.memberTileIds[0];
                for (int i = 1; i < components.Count; i++)
                {
                    List<int> splinter = components[i];
                    topology.Add(new CARegionalTopologyRecord
                    {
                        regionId = CARegionalTopologyBuilder.MintRegionId(
                            worldSeed, splinter[0]),
                        rootTileId = splinter[0],
                        memberTileIds = splinter
                    });
                }
            }
            RebuildTopologyIndex();
            Log.Message("[CA][Topology] carved the authored footprint "
                + (plan.regionalId ?? "unknown") + " ("
                + carved.Count + " tiles) out of " + affected.Count
                + " partition region" + (affected.Count == 1 ? "" : "s"));
        }

        internal CARegionalPlan FindRegion(PlanetTile mapTile, int mapSize)
        {
            if (!mapTile.Valid) return null;
            if (transientDeveloperExerciseRegion != null
                && transientDeveloperExerciseRegion.startTileId
                    == mapTile.tileId
                && transientDeveloperExerciseRegion.mapSize == mapSize)
                return transientDeveloperExerciseRegion;
            return regions.FirstOrDefault(region => region != null
                && region.startTileId == mapTile.tileId
                && region.mapSize == mapSize);
        }

        internal CARegionalPlan FindRegionForMap(Map map)
        {
            if (map == null) return null;
            return FindRegionForTile(map.Tile, map.Size);
        }

        // The same lookup, expressed against the parented tile and the backing
        // size rather than a Map. MapGenerator computes its master seed from
        // parent.Tile BEFORE any Map exists, so the engine-root substitution
        // needs a pre-map form of this resolution. Reads no Map state, which
        // also keeps it safe to call from inside the Map.TileInfo getter.
        internal CARegionalPlan FindRegionForTile(PlanetTile mapTile,
            IntVec3 backingSize)
        {
            if (!mapTile.Valid) return null;
            if (transientDeveloperExerciseRegion != null
                && transientDeveloperExerciseRegion.startTileId
                    == mapTile.tileId
                && transientDeveloperExerciseRegion.BackingMapSize
                    == backingSize)
                return transientDeveloperExerciseRegion;
            return regions.FirstOrDefault(region => region != null
                && region.startTileId == mapTile.tileId
                && region.BackingMapSize == backingSize);
        }

        internal CARegionalPlan FindRegionContaining(PlanetTile tile)
        {
            if (!tile.Valid) return null;
            if (transientDeveloperExerciseRegion?.ReservedTileIds != null
                && transientDeveloperExerciseRegion.ReservedTileIds.Contains(
                    tile.tileId))
                return transientDeveloperExerciseRegion;
            return regions.FirstOrDefault(region => region != null
                && region.ReservedTileIds != null
                && region.ReservedTileIds.Contains(tile.tileId));
        }

        internal bool CanReserveRegion(CARegionalPlan region,
            out string failure)
        {
            failure = null;
            if (region == null || region.ReservedTileIds == null
                || region.ReservedTileIds.Count == 0)
            {
                failure = "regional footprint is empty";
                return false;
            }
            if (!CARegionalPlanUtility.TryValidateStableIdentities(region,
                    out failure))
                return false;
            if (Verse.Find.WorldObjects == null)
            {
                failure = "world objects are not initialized";
                return false;
            }
            if (CARegionalDefOf.CA_RegionalMemberReservation == null)
            {
                failure = "regional reservation definition is unavailable";
                return false;
            }

            foreach (int id in region.ReservedTileIds)
            {
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(id);
                if (!tile.Valid)
                {
                    failure = "member tile " + id + " is invalid";
                    return false;
                }

                CARegionalPlan owner = regions.FirstOrDefault(item =>
                    item != null && item != region
                    && item.regionalId != region.regionalId
                    && item.ReservedTileIds != null
                    && item.ReservedTileIds.Contains(id));
                if (owner == null && transientDeveloperExerciseRegion != null
                    && transientDeveloperExerciseRegion != region
                    && transientDeveloperExerciseRegion.regionalId
                        != region.regionalId
                    && transientDeveloperExerciseRegion.ReservedTileIds
                        ?.Contains(id) == true)
                    owner = transientDeveloperExerciseRegion;
                if (owner != null)
                {
                    failure = "member tile " + id + " already belongs to "
                        + owner.regionalId;
                    return false;
                }
                if (id == region.startTileId) continue;

                List<WorldObject> objects = Verse.Find.WorldObjects
                    .ObjectsAt(tile)
                    .ToList();
                List<WorldObject_CARegionalMemberReservation> matching =
                    objects.OfType<WorldObject_CARegionalMemberReservation>()
                        .Where(item => item.regionalId == region.regionalId
                            && item.anchorTileId == region.startTileId
                            && item.mapSize == region.mapSize).ToList();
                if (matching.Count > 1)
                {
                    failure = "member tile " + id + " has duplicate regional "
                        + "reservations";
                    return false;
                }
                // A settlement on a member is fragmentation and must be
                // absorbed before reservation. Transient world content --
                // quest sites, camps, caravans -- coexists on regional
                // ground: it keeps its own identity and its own encounter
                // maps, and reservation does not evict it.
                WorldObject conflict = objects.FirstOrDefault(item =>
                    (matching.Count == 0 || item != matching[0])
                    && item is Settlement);
                if (conflict != null)
                {
                    failure = "member tile " + id + " is occupied by "
                        + (conflict.def?.label ?? conflict.GetType().Name);
                    return false;
                }
            }
            return true;
        }

        internal bool TryReserveRegion(CARegionalPlan region,
            out string failure, bool restoration = false)
        {
            if (!CanReserveRegion(region, out failure)) return false;
            var created = new List<int>();
            for (int i = 0; i < region.ReservedTileIds.Count; i++)
            {
                int id = region.ReservedTileIds[i];
                if (id == region.startTileId) continue;
                PlanetTile tile = CARegionalPlanUtility.SurfaceTile(id);
                bool exists = Verse.Find.WorldObjects.ObjectsAt(tile)
                    .OfType<WorldObject_CARegionalMemberReservation>()
                    .Any(item => item.regionalId == region.regionalId
                        && item.anchorTileId == region.startTileId
                        && item.mapSize == region.mapSize);
                if (exists) continue;
                var reservation = (WorldObject_CARegionalMemberReservation)
                    WorldObjectMaker.MakeWorldObject(
                        CARegionalDefOf.CA_RegionalMemberReservation);
                reservation.regionalId = region.regionalId;
                reservation.anchorTileId = region.startTileId;
                reservation.mapSize = region.mapSize;
                reservation.memberIndex = i;
                reservation.Tile = tile;
                Verse.Find.WorldObjects.Add(reservation);
                created.Add(id);
            }
            Log.Message("[CA][Regional] "
                + (restoration ? "restored/verified" : "committed")
                + " footprint reservation " + region.regionalId
                + "; landing " + region.startTileId + "; reserved world tiles "
                + string.Join(",", region.ReservedTileIds.Where(id =>
                    id != region.startTileId)) + "; created "
                + (created.Count == 0 ? "none" : string.Join(",", created)));
            return true;
        }

        // Regional plans own the intended footprint. Reservation WorldObjects
        // are the engine-bound subordinate registry and are reconciled to that
        // exact set; they never become a second plan authority.
        internal bool ReconcileReservationRegistry(out string failure)
        {
            failure = null;
            if (Verse.Find.WorldObjects == null)
            {
                failure = "the world-object registry is unavailable";
                return false;
            }
            var desired = new Dictionary<int, ReservationClaim>();
            foreach (CARegionalPlan region in regions
                ?? new List<CARegionalPlan>())
            {
                if (region?.ReservedTileIds == null) continue;
                for (int i = 0; i < region.ReservedTileIds.Count; i++)
                {
                    int tileId = region.ReservedTileIds[i];
                    if (tileId == region.startTileId) continue;
                    if (desired.ContainsKey(tileId))
                    {
                        failure = "several durable regions claim member tile "
                            + tileId;
                        return false;
                    }
                    desired.Add(tileId, new ReservationClaim
                    {
                        Region = region,
                        MemberIndex = i
                    });
                }
            }

            var kept = new HashSet<int>();
            List<WorldObject_CARegionalMemberReservation> existing =
                Verse.Find.WorldObjects.AllWorldObjects
                    .OfType<WorldObject_CARegionalMemberReservation>()
                    .ToList();
            foreach (WorldObject_CARegionalMemberReservation reservation in
                existing)
            {
                int tileId = reservation.Tile.tileId;
                bool valid = desired.TryGetValue(tileId,
                        out ReservationClaim claim)
                    && reservation.regionalId == claim.Region.regionalId
                    && reservation.anchorTileId == claim.Region.startTileId
                    && reservation.mapSize == claim.Region.mapSize
                    && kept.Add(tileId);
                if (!valid)
                {
                    Verse.Find.WorldObjects.Remove(reservation);
                    continue;
                }
                reservation.memberIndex = claim.MemberIndex;
            }
            foreach (CARegionalPlan region in regions
                ?? new List<CARegionalPlan>())
                if (region != null && !TryReserveRegion(region, out failure,
                        restoration: true))
                    return false;
            return true;
        }

        internal int ReservationCount(CARegionalPlan region)
        {
            if (region == null || Verse.Find.WorldObjects == null) return 0;
            return Verse.Find.WorldObjects.AllWorldObjects
                .OfType<WorldObject_CARegionalMemberReservation>()
                .Count(item => item.regionalId == region.regionalId
                    && item.anchorTileId == region.startTileId
                    && item.mapSize == region.mapSize);
        }

        internal void ValidateDurableRegion(CARegionalPlan region)
        {
            if (region == null)
                throw new InvalidOperationException("A null region cannot enter "
                    + "durable world state.");
            // Candidate plans may reach map setup through mods and terminal-page
            // variations. Enforce the durable invariant at the canonical owner,
            // not only in one UI path.
            if (region.developerExercise)
                throw new InvalidOperationException("Developer-exercise region "
                    + (region.regionalId ?? "unknown")
                    + " cannot enter durable world state.");
            if (region.operatorAuthored && !region.confirmed)
                throw new InvalidOperationException("Unconfirmed authored region "
                    + (region.regionalId ?? "unknown")
                    + " cannot enter durable world state.");
            ValidateConfirmedComposition(region, "Durable region");
            CACompatibilityReport compatibility =
                CARegionalContentCompatibility.Evaluate(region);
            if (!compatibility.IsValid)
                throw new InvalidOperationException("Durable region "
                    + (region.regionalId ?? "unknown")
                    + " failed the compatibility gate: "
                    + compatibility.ActionableFailure());
        }

        internal bool HasTransientDeveloperExercise
        {
            get { return transientDeveloperExerciseRegion != null; }
        }

        internal CARegionalPlan RegisterRegion(CARegionalPlan region)
        {
            ValidateDurableRegion(region);
            if (region.worldPolicy == null)
                region.worldPolicy = WorldPolicy.Copy();
            CARegionalPlan existing = regions.FirstOrDefault(item =>
                item != null && item.startTileId == region.startTileId
                && item.mapSize == region.mapSize);
            if (existing != null)
            {
                if (region.operatorAuthored && !existing.operatorAuthored)
                {
                    regions.Remove(existing);
                    regions.Add(region);
                    if (region.worldPolicy != null)
                        worldPolicy = region.worldPolicy.Copy();
                    // the ground the player drafted becomes the ground
                    // this world is made of, once
                    if (region.groundwater != null)
                        groundwater = region.groundwater.Copy();
                    if (!ReconcileReservationRegistry(out string failure))
                        throw new InvalidOperationException(
                            "Replacement regional footprint is invalid: "
                            + failure);
                    CarveTopologyForRegion(region);
                    if (worldSettlementStates.Count > 0)
                        RebuildWorldSettlementStates(
                            "regional materialization");
                    return region;
                }
                ValidateDurableRegion(existing);
                return existing;
            }
            regions.Add(region);
            if (region.operatorAuthored && region.worldPolicy != null)
                worldPolicy = region.worldPolicy.Copy();
            if (region.operatorAuthored && region.groundwater != null)
                groundwater = region.groundwater.Copy();
            CarveTopologyForRegion(region);
            if (worldSettlementStates.Count > 0)
                RebuildWorldSettlementStates("regional materialization");
            return region;
        }

        internal CARegionalPlan RegisterTransientDeveloperExercise(
            CARegionalPlan region)
        {
            CACompatibilityReport compatibility = region == null ? null
                : CARegionalContentCompatibility.Evaluate(region);
            if (region == null || !region.confirmed
                || !region.developerExercise)
                throw new InvalidOperationException("A transient regional "
                    + "exercise requires a confirmed candidate carrying the "
                    + "explicit developer-exercise stamp.");
            ValidateConfirmedComposition(region,
                "Transient developer exercise");
            transientDeveloperExerciseRegion = region;
            Log.Warning("[CA][Regional] transient developer exercise "
                + region.regionalId + " registered for this runtime only; it "
                + "is excluded from durable regional world state and cannot "
                + "be saved; compatibility at registration: "
                + (compatibility?.Summary() ?? "unavailable") + ".");
            return region;
        }

        internal static void ValidateConfirmedComposition(CARegionalPlan region,
            string context)
        {
            string regionId = region?.regionalId ?? "unknown";
            if (!CARegionalSettlements.TryValidateRealization(region,
                    out string realizationFailure))
                throw new InvalidOperationException(context + " " + regionId
                    + " has invalid persisted settlement realization: "
                    + realizationFailure + ".");
            if (!CARegionalPlanUtility.TryValidateStableIdentities(region,
                    out string identityFailure))
                throw new InvalidOperationException(context + " " + regionId
                    + " is invalid: " + identityFailure + ".");
            if (!CARegionalPlanUtility.TryValidateDistinctFactionClaims(region,
                    out string factionFailure))
                throw new InvalidOperationException(context + " " + regionId
                    + " is invalid: " + factionFailure);
            if (!CARegionalPlanUtility.TryValidateStartingSettlements(region,
                    out string settlementFailure))
                throw new InvalidOperationException(context + " " + regionId
                    + " is invalid: " + settlementFailure);
            foreach (CARegionalSettlementPlan settlement in (region.settlements
                ?? new List<CARegionalSettlementPlan>())
                .Where(item => item != null))
                if (!CASettlementProgramRegistry.TryValidateSaved(region,
                        settlement, out string programFailure))
                    throw new InvalidOperationException(context + " "
                        + regionId + " has invalid saved settlement "
                        + settlement.slot + ": " + programFailure + ".");
        }

        internal CARegionalPlan EnsureDerivedRegion(PlanetTile mapTile,
            CAExpandedLandmassProfile profile, Faction parentFaction)
        {
            CARegionalPlan existing = FindRegion(mapTile, profile.Size);
            if (existing != null)
            {
                // An existing region must satisfy the current durable
                // contract before its reservations are restored.
                ValidateDurableRegion(existing);
                string existingFailure;
                if (!TryReserveRegion(existing, out existingFailure))
                    throw new InvalidOperationException("Regional footprint "
                        + existing.regionalId + " cannot be reserved: "
                        + existingFailure);
                return existing;
            }
            // A member tile of an already-registered region can never
            // produce a competing plan or a second map. Map opening is
            // redirected to the canonical anchor before generation begins;
            // reaching this point with a member tile means that contract
            // was bypassed, and the only safe answer is to refuse loudly.
            CARegionalPlan containing = FindRegionContaining(mapTile);
            if (containing != null)
                throw new InvalidOperationException("Tile " + mapTile.tileId
                    + " is a member of registered region "
                    + containing.regionalId + " (anchor "
                    + containing.startTileId + "); a constituent tile "
                    + "cannot materialize a competing regional map.");
            // The world's persistent partition states this region's
            // identity and membership; materialization realizes exactly
            // that region, whichever member the engine entered through.
            CARegionalTopologyRecord topologyRecord =
                TopologyRecordAt(mapTile);
            CARegionalPlan derived;
            if (topologyRecord != null)
            {
                derived = CARegionalPlanUtility.CreateFromTopology(profile,
                    topologyRecord, mapTile);
            }
            else
            {
                // Land outside the partition (an ineligible pocket, or a
                // world older than the topology whose migration could not
                // run) falls back to the legacy visit-derived bundle.
                Log.Warning("[CA][Topology] tile " + mapTile.tileId
                    + " has no partition membership; deriving a legacy "
                    + "visit-scoped region");
                int availableTiles = Math.Max(1,
                    CARegionalBundleBuilder.Build(mapTile, 12).Count);
                int regionTileCount = WorldPolicy.ResolveRequestedExtent(
                    mapTile, availableTiles);
                derived = CARegionalPlanUtility.Create(profile, mapTile,
                    false, regionTileCount);
            }
            // The world component is the canonical owner of global tendencies;
            // every realized region carries a snapshot so later composition
            // stages cannot silently construct fresh defaults.
            derived.worldPolicy = WorldPolicy.Copy();
            WorldPolicy.PopulateDerived(derived, profile, parentFaction);
            // Validate before creating reservation world objects. RegisterRegion
            // validates again at the canonical write boundary, but that later
            // check cannot undo reservations already added to the world.
            ValidateDurableRegion(derived);
            // Auto-generated major settlements use the same RimWorld pool
            // transfer as authored reallocation. Frontier holdings are absent
            // from this list and never consume major-settlement authorization.
            // Consumption precedes reservation: settlements standing on the
            // region's own members are absorbed as mandatory sources, and
            // their world objects must be gone before the members can be
            // reserved.
            CARegionalPlanUtility.ConsumeReallocatedSources(derived);
            string failure;
            if (!TryReserveRegion(derived, out failure))
                throw new InvalidOperationException("Regional footprint "
                    + derived.regionalId + " cannot be reserved: " + failure);
            return RegisterRegion(derived);
        }

        internal CARegionalSettlementRecord Find(string regionKey, int slot,
            int mapSize)
        {
            for (int i = 0; i < records.Count; i++)
            {
                CARegionalSettlementRecord record = records[i];
                if (record != null && record.regionKey == regionKey
                    && record.slot == slot && record.mapSize == mapSize)
                    return record;
            }
            return null;
        }

        // The settlement record key is produced here. A regional map
        // keys on the FOOTPRINT (regionalId, minted from the bundle root); a
        // non-regional map keeps the parented tile, which is what it has ever
        // meant there. Nothing may reconstruct this string from map.Tile.
        internal static string RegionKeyFor(CARegionalPlan region, Map map)
        {
            if (region != null && !string.IsNullOrEmpty(region.regionalId))
                return region.regionalId;
            return map == null ? "unknown" : map.Tile.ToString();
        }

        internal string RegionKeyFor(Map map)
        {
            return RegionKeyFor(FindRegionForMap(map), map);
        }

        internal IEnumerable<CARegionalSettlementRecord> ForMap(Map map)
        {
            if (map == null) yield break;
            CARegionalPlan region = FindRegionForMap(map);
            string regionKey = RegionKeyFor(region, map);
            int localMapSize = region?.mapSize ?? map.Size.x;
            for (int i = 0; i < records.Count; i++)
            {
                CARegionalSettlementRecord record = records[i];
                if (record != null && record.regionKey == regionKey
                    && record.mapSize == localMapSize)
                    yield return record;
            }
        }

        internal CARegionalSettlementRecord Create(Map map, int slot,
            Faction faction, CARegionalSettlementPlan settlement = null)
        {
            CARegionalPlan localRegion = FindRegionForMap(map);
            string regionKey = RegionKeyFor(localRegion, map);
            int localMapSize = localRegion?.mapSize ?? map.Size.x;
            if (localRegion != null && settlement != null)
            {
                string realizationFailure;
                string programFailure = null;
                bool realizationValid = CARegionalSettlements
                    .TryValidateRealization(localRegion,
                        out realizationFailure);
                bool programValid = realizationValid
                    && CASettlementProgramRegistry.TryValidateSaved(
                        localRegion, settlement, out programFailure);
                if (!realizationValid || !programValid)
                    throw new InvalidOperationException(
                        "confirmed settlement state is invalid: "
                        + (realizationFailure ?? programFailure));
            }
            int resolvedAccess = settlement == null ? 0
                : CASettlementStartingState.Access(localRegion, settlement);
            int resolvedServices = settlement == null ? 0
                : CASettlementStartingState.Services(localRegion, settlement);
            int resolvedCivic = settlement == null ? 0
                : CASettlementStartingState.Civic(localRegion, settlement);
            CARegionalFactionPlan factionGroup = localRegion?.FactionPlan(
                settlement?.OwningFactionKey ?? -1);
            CABehaviorDecision creationDecision;
            CAIntentContext creationIntent;
            CASettlementDevelopmentProposal creationProposal =
                CASettlementAssetRegistry.BuildCreationProposal(
                    settlement?.settlementProgram,
                    settlement?.landCapacity ?? -1,
                    settlement?.provisionArrangements);
            bool creationAuthorized =
                CASettlementInstitutionalAuthorization
                    .TryAuthorizeCreationHistory(localRegion, settlement,
                        creationProposal, out creationDecision,
                        out creationIntent);
            CACulture siteCulture = settlement?.localCulture
                ?? factionGroup?.culture;
            CAPoliticalBeliefs sitePoliticalOrder = settlement == null
                ? factionGroup?.politicalBeliefs
                : CASiteState.PoliticalOrder(localRegion, settlement);
            string culturalBasis = siteCulture == null
                ? "no carried cultural basis"
                : CACultureModel.Summary(siteCulture);
            string politicalBasis = sitePoliticalOrder == null
                ? "no recorded political basis"
                : CAPoliticalBeliefsModel.Summary(
                    sitePoliticalOrder);
            List<string> beneficiaries = settlement?.populationGroups == null
                ? new List<string> { "settlement residents" }
                : settlement.populationGroups.Where(group => group != null)
                    .Select(group => group.label
                        ?? "population group " + group.key)
                    .Where(label => !label.NullOrEmpty()).Distinct()
                    .OrderBy(label => label, StringComparer.Ordinal).ToList();
            if (beneficiaries.Count == 0)
                beneficiaries.Add("settlement residents");
            // The guarantee point. A plan can arrive here from setup,
            // reallocation, a preview or a probe, and only one of those
            // paths ran the authoring pass - so the culture a
            // settlement's residents brought is settled here, once,
            // before it is copied into the record that outlives the plan.
            // Guarded because this sits between the settlement being
            // decided and the record that carries it into the game. A
            // settlement whose culture could not be settled should
            // still BE a settlement - the material and style
            // authorities read an unanswering culture as no cultural
            // position, which is a legible outcome, whereas a throw
            // here takes the whole settlement out of the world.
            try
            {
                CACultureHistory.EnsureSettlementCulture(localRegion,
                    settlement);
            }
            catch (Exception ex)
            {
                Log.Warning("[CA][Settlement][Culture] could not settle "
                    + "the culture for slot " + slot + " of "
                    + (regionKey ?? "region") + ": " + ex.Message
                    + "; it builds to no cultural position");
            }
            var record = new CARegionalSettlementRecord
            {
                regionalId = CASettlementAuthorityWriter
                    .SettlementRecordId(localRegion, slot)
                    ?? "CA-RS-" + (regionKey ?? "region") + "-" + slot,
                regionKey = regionKey,
                slot = slot,
                mapSize = localMapSize,
                memberTileId = settlement?.memberTileId ?? -1,
                OwningFactionKey = settlement?.OwningFactionKey ?? -1,
                operationalRoleMask = settlement?.operationalRoleMask ?? 0,
                residentPopulation = settlement?.residentPopulation ?? -1,
                landCapacity = settlement?.landCapacity ?? -1,
                economicCapacity = settlement?.economicCapacity ?? -1,
                tradeConnectivity = settlement?.tradeConnectivity ?? -1,
                specialization = settlement?.specialization ?? -1,
                historicalDevelopment = settlement
                    ?.historicalDevelopment ?? -1,
                urbanSupport = settlement?.urbanSupport ?? -1,
                realizedRole = settlement?.realizedRole ?? (byte)0,
                realizedScale = settlement?.realizedScale ?? (byte)0,
                accessInfrastructure = resolvedAccess,
                serviceInfrastructure = resolvedServices,
                civicInfrastructure = resolvedCivic,
                settlementProgram = settlement?.settlementProgram?.Copy()
                    ?? new CASettlementProgram(),
                // Settled just above, so this carries the settlement's
                // real culture. The identity-only object remains for a
                // record with no settlement plan behind it at all - the
                // validator requires an object, and an unanswering one
                // is the designed empty state.
                culture = settlement?.localCulture?.Copy()
                    ?? new CACulture(),
                persistent = settlement?.persistent ?? true,
                faction = faction,
                factionLinks = settlement?.factionLinks?.Copy()
                    ?? new CASiteFactionLinks(),
                localSociety = settlement?.localSociety?.Copy(),
                generationFactionDefName = settlement?.generationFactionDefName
                    ?? faction?.def?.defName,
                materializationSummary = "world seed + bundled world-tile member + "
                    + (faction == null ? "unaffiliated site; "
                        : "operator-authored faction; ")
                    + "native settlement "
                    + "generation; " + (creationAuthorized
                        ? "confirmed history authored by "
                            + creationIntent.AuthorityIdentity
                        : "creation history blocked: "
                            + creationDecision.PrimaryReason),
                creationBehaviorKey = creationAuthorized
                    ? creationIntent.BehaviorKey : null,
                creationEpisodeId = creationAuthorized
                    ? creationIntent.EpisodeId : 0,
                creationAuthorityOrigin = creationAuthorized
                    ? (int)creationIntent.AuthorityOrigin : 0,
                creationAuthorityIdentity = creationAuthorized
                    ? creationIntent.AuthorityIdentity : null,
                creationOwner = creationAuthorized
                    ? creationIntent.OwnershipScope : null,
                creationTargetOrDemand = creationAuthorized
                    ? creationIntent.TargetOrDemand : creationProposal
                        .StableSignature(),
                creationCreatedTick = creationAuthorized
                    ? creationIntent.CreatedTick : -1,
                creationProposer = "creation author",
                creationApprover = localRegion?.confirmed == true
                    ? "confirmed starting-region candidate "
                        + localRegion.candidateId
                    : "unconfirmed starting region",
                creationLaborSource = "materialized settlement history",
                creationBeneficiaries = beneficiaries.ToList(),
                creationCulturalBasis = culturalBasis,
                creationPoliticalBasis = politicalBasis,
                creationAuthorized = creationAuthorized,
                creationExecutable = false,
                creationMaterialFeasible = creationProposal.MaterialFeasible,
                creationProposalSignature = creationProposal.StableSignature(),
                creationSitingEvaluated = false,
                creationSitingFeasible = false,
                creationBlocker = creationAuthorized ? null
                    : creationDecision.PrimaryReason,
                // Institutional development is derived only after a current
                // organization and material settlement exist.
                developmentAuthorized = false,
                developmentExecutable = false,
                developmentFundingFeasible = false,
                developmentMaterialFeasible = false,
                developmentSitingEvaluated = false,
                developmentSitingFeasible = false,
                developmentDemandKinds = new List<int>(),
                developmentAssetCandidates = new List<string>(),
                developmentFundingBasis =
                    "awaiting a current settlement institution",
                developmentMaterialBasis =
                    "awaiting current settlement ground and residents",
                developmentProposalSignature = null,
                developmentCulturalBasis = culturalBasis,
                developmentPoliticalBasis = politicalBasis,
                developmentBlocker = "institutional development has not yet read the materialized settlement"
            };
            // The drafted composition becomes the settlement's own. Deep
            // copies, not shared references - the plan remains a draft
            // and the record is the durable truth.
            if (settlement?.populationGroups != null)
                foreach (CASettlementPopulationGroup populationGroup in settlement.populationGroups)
                    if (populationGroup != null)
                        record.populationGroups.Add(new CASettlementPopulationGroup
                        {
                            key = populationGroup.key,
                            kind = populationGroup.kind,
                            isPrimary = populationGroup.isPrimary,
                            label = populationGroup.label,
                            share = populationGroup.share,
                            factionKey = populationGroup.factionKey,
                            politicalBeliefsFactionKey =
                                populationGroup.politicalBeliefsFactionKey,
                            politicalBeliefsId = ResolvePoliticalBeliefsId(
                                localRegion, settlement,
                                populationGroup),
                            ideoligionCertainty = populationGroup.ideoligionCertainty,
                            ideoligionFactionKey = populationGroup.ideoligionFactionKey,
                            nativeIdeoligionId = populationGroup.nativeIdeoligionId,
                            ideoligionProtected =
                                populationGroup.ideoligionProtected,
                            authored = populationGroup.authored
                        });
            if (settlement?.provisionArrangements != null)
                foreach (CAProvisionArrangement arrangement in
                    settlement.provisionArrangements)
                    if (arrangement != null && arrangement.active)
                        record.provisionArrangements.Add(
                            new CAProvisionArrangement
                        {
                            key = arrangement.key,
                            operatorKind = arrangement.operatorKind,
                            operatorIdentity = arrangement.operatorIdentity,
                            operatorSource = arrangement.operatorSource,
                            populationGroupKey = arrangement.populationGroupKey,
                            access = arrangement.access,
                            accessSource = arrangement.accessSource,
                            funding = arrangement.funding,
                            fundingSource = arrangement.fundingSource,
                            distribution = arrangement.distribution,
                            distributionSource =
                                arrangement.distributionSource,
                            laborSource = arrangement.laborSource,
                            knowledgeSource = arrangement.knowledgeSource,
                            materialSource = arrangement.materialSource,
                            stockSource = arrangement.stockSource,
                            policyKey = arrangement.policyKey,
                            policyValue = arrangement.policyValue,
                            collectionPath = arrangement.collectionPath,
                            basisKey = arrangement.basisKey,
                            basisLabel = arrangement.basisLabel,
                            active = true,
                            operational = arrangement.operational,
                            waterSecured = arrangement.waterSecured,
                            nodes = arrangement.nodes,
                            reach = arrangement.reach
                        });
            if (settlement?.domesticProvisionDemands != null)
                foreach (CADomesticProvisionDemand demand in
                    settlement.domesticProvisionDemands)
                    if (demand != null)
                        record.domesticProvisionDemands.Add(demand.Copy());
            if (settlement?.operationalFacts != null)
                foreach (CASettlementOperationalFact fact in
                    settlement.operationalFacts)
                    if (fact != null && fact.active)
                        record.operationalFacts.Add(fact.Copy());

            CACulturalExpression culturalExpression =
                CACulturalExpressionModel.ForSettlement(localRegion,
                    settlement);
            record.culturalExpressionSummary = culturalExpression.Summary;
            record.culturalExpressionSourceSignature =
                culturalExpression.SourceSignature;
            record.culturalExpressionStatus =
                (byte)culturalExpression.Status;

            // Knowledge and physical form are facts; capability is assessed
            // later from actual actors, operations, organizations, and
            // material state.
            CATechnologicalKnowledge knowledge = settlement == null
                ? CATechnologicalKnowledgeRuntime.ForFaction(faction)
                : CASiteState.Knowledge(localRegion, settlement);
            bool usesLiveFactionKnowledge = faction != null
                && (settlement == null
                    || (CASiteState.HasOwner(settlement.factionLinks)
                        && settlement.localSociety
                            ?.explicitLocalDivergence != true));
            int knowledgeTier = usesLiveFactionKnowledge
                ? CATechnologicalKnowledgeRuntime
                    .CanonicalCompatibilityTier(faction)
                : CATechnologicalKnowledgeModel.CompatibilityTier(
                    knowledge);
            TechLevel compatibilityLevel = usesLiveFactionKnowledge
                ? CATechnologicalKnowledgeRuntime
                    .CanonicalBuildTechLevel(faction)
                : CATechnologicalKnowledgeModel.CompatibilityTechLevel(
                    knowledge);
            bool roadLinked = settlement?.hasRoadAccess == true;
            bool coastal = settlement?.hasCoastalAccess == true;
            record.technologicalKnowledgeId = knowledge?.id;
            record.technologicalKnowledgeRevision = knowledge?.revision ?? -1;
            record.technologicalKnowledgeTier = knowledgeTier;
            record.settlementForm = (int)CASettlementAxes.Form(
                settlement?.authoredForm ?? CASettlementAxes.Derive,
                compatibilityLevel);
            record.generationSummary = CASettlementAxes.Provenance(
                settlement?.authoredForm ?? CASettlementAxes.Derive,
                record.settlementProgram)
                + "; settlement program "
                + record.settlementProgram?.sourceSignature
                + "; capabilities await factual runtime evidence"
                + (roadLinked ? "; road present" : "; no road")
                + (coastal ? "; coast present" : "; inland");
            CASettlementWealth.Derive(record.settlementProgram,
                record.accessInfrastructure, record.serviceInfrastructure,
                record.civicInfrastructure, compatibilityLevel,
                out record.wealth, out record.constructionEra);
            // Preserve an authored settlement name.
            record.name = settlement != null
                && !settlement.customName.NullOrEmpty()
                    ? settlement.customName
                    : GenerateSettlementName(faction,
                        DefDatabase<FactionDef>.GetNamedSilentFail(
                            record.generationFactionDefName), records);
            records.Add(record);
            return record;
        }

        private static string ResolvePoliticalBeliefsId(CARegionalPlan plan,
            CARegionalSettlementPlan settlement,
            CASettlementPopulationGroup populationGroup)
        {
            if (populationGroup != null && !populationGroup.politicalBeliefsId.NullOrEmpty())
                return populationGroup.politicalBeliefsId;
            int key = populationGroup?.politicalBeliefsFactionKey >= 0
                ? populationGroup.politicalBeliefsFactionKey
                : populationGroup?.factionKey ?? -1;
            CARegionalFactionPlan source = plan?.FactionPlan(key);
            if (source != null) return source.politicalBeliefs?.id;
            return populationGroup?.isPrimary == true
                    && (!CASiteState.HasOwner(settlement?.factionLinks)
                        || settlement?.localSociety
                            ?.explicitLocalDivergence == true)
                ? settlement?.localSociety?.politicalOrder?.id : null;
        }

        private static string GenerateSettlementName(Faction faction,
            FactionDef generationDef,
            List<CARegionalSettlementRecord> records)
        {
            try
            {
                RulePackDef nameMaker = faction?.def?.settlementNameMaker
                    ?? generationDef?.settlementNameMaker;
                if (nameMaker != null)
                    return NameGenerator.GenerateName(
                        nameMaker,
                        records.Where(r => r != null && !r.name.NullOrEmpty())
                            .Select(r => r.name), true);
            }
            catch (Exception ex)
            {
                Log.Warning("[CA][Regional] local settlement naming fell back: "
                    + ex.Message);
            }
            return (faction?.Name ?? generationDef?.label
                    ?? "Independent settlement") + " "
                + (records.Count + 1);
        }
    }

    public sealed class CARegionalSettlementMapComponent : MapComponent
    {
        private const int ReconciliationInterval = 7500;

        public CARegionalSettlementMapComponent(Map map) : base(map) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Reconcile("map-loaded");
        }

        public override void MapGenerated()
        {
            Reconcile("map-generated");
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % ReconciliationInterval
                == map.uniqueID % ReconciliationInterval)
                Reconcile("interval");
        }

        internal void Reconcile(string reason)
        {
            CARegionalWorldComponent world = CARegionalWorldComponent.Current;
            CARegionalPlan region = world?.FindRegionForMap(map);
            if (region == null) return;
            if (reason != "interval")
                CARegionalSettlementGenerationAudit.Audit(map, reason);
            int now = Find.TickManager.TicksGame;
            List<CARegionalSettlementRecord> allRecords = world.ForMap(map)
                .Where(record => record != null).ToList();
            List<CARegionalSettlementRecord> records = allRecords
                .Where(record => record != null
                    && record.localRect != CellRect.Empty).ToList();
            int[] buildings = new int[records.Count];
            int[] infrastructure = new int[records.Count];
            int[] cultivated = new int[records.Count];
            int matched = 0;
            List<Thing> all = map.listerThings.AllThings;
            using (CAModuleProfiler.Measure(CAModuleProfileKey.FullMapScan))
            {
                for (int thingIndex = 0; thingIndex < all.Count; thingIndex++)
                {
                    Thing thing = all[thingIndex];
                    if (thing == null || !thing.Spawned) continue;
                    for (int recordIndex = 0; recordIndex < records.Count;
                        recordIndex++)
                    {
                        CARegionalSettlementRecord record =
                            records[recordIndex];
                        if (!record.localRect.Contains(thing.Position))
                            continue;
                        matched++;
                        Building building = thing as Building;
                        if (building != null
                            && building.Faction == record.faction)
                        {
                            buildings[recordIndex]++;
                            if (building.TryGetComp<CompPower>() != null
                                || building.def.IsDoor
                                || building is Building_Bed)
                                infrastructure[recordIndex]++;
                        }
                        Plant plant = thing as Plant;
                        if (plant != null && plant.sown)
                            cultivated[recordIndex]++;
                    }
                }
                CAModuleProfiler.Observe(CAModuleProfileKey.FullMapScan,
                    all.Count, matched);
            }
            for (int recordIndex = 0; recordIndex < records.Count;
                recordIndex++)
            {
                CARegionalSettlementRecord record = records[recordIndex];
                ReconcileRecord(record, map, buildings[recordIndex],
                    infrastructure[recordIndex], cultivated[recordIndex]);
                ReconcileCulturalExpression(record, map);
                record.lastMapId = map.uniqueID;
                record.lastReconciliationTick = now;
                if (reason != "interval")
                    Log.Message("[CA][Regional] " + reason + " receipt "
                        + record.regionalId + " " + record.name + ": population "
                        + record.populationCurrent + "/"
                        + record.populationBaseline + ", buildings "
                        + record.buildingCount + ", infrastructure "
                        + record.infrastructureCount + ", cultivated plants "
                        + record.cultivatedPlantCount);
            }
            CARegionalSettlementMarkers.Ensure(region, allRecords);
            if (reason == "map-generated")
                CARegionalSettlementGenerationAudit.Finish(map);
        }

        internal static void ReconcileRecord(CARegionalSettlementRecord record,
            Map map)
        {
            if (record == null || map == null) return;
            int buildings = 0;
            int infrastructure = 0;
            int cultivated = 0;
            int matched = 0;
            List<Thing> all = map.listerThings.AllThings;
            using (CAModuleProfiler.Measure(CAModuleProfileKey.FullMapScan))
            {
                for (int i = 0; i < all.Count; i++)
                {
                    Thing thing = all[i];
                    if (thing == null || !thing.Spawned
                        || !record.localRect.Contains(thing.Position))
                        continue;
                    matched++;
                    Building building = thing as Building;
                    if (building != null && building.Faction == record.faction)
                    {
                        buildings++;
                        if (building.TryGetComp<CompPower>() != null
                            || building.def.IsDoor
                            || building is Building_Bed)
                            infrastructure++;
                    }
                    Plant plant = thing as Plant;
                    if (plant != null && plant.sown) cultivated++;
                }
                CAModuleProfiler.Observe(CAModuleProfileKey.FullMapScan,
                    all.Count, matched);
            }
            ReconcileRecord(record, map, buildings, infrastructure,
                cultivated);
        }

        private static void ReconcileRecord(
            CARegionalSettlementRecord record, Map map, int buildings,
            int infrastructure, int cultivated)
        {
            CASettlementResidenceState.ReconcileNativeEvents(record, map);
            List<Pawn> residents = CAPopulationProjection.Residents(record,
                map);
            record.residentIds = residents.Select(pawn =>
                pawn.GetUniqueLoadID()).ToList();
            record.populationCurrent = residents.Count;

            record.buildingCount = buildings;
            record.infrastructureCount = infrastructure;
            record.cultivatedPlantCount = cultivated;
            CADomesticUnitFormation.Reconcile(record, map);
            CAOrganization organization = CAOrganizationWorldComponent.Current
                ?.ByKey(record.regionalId + "#" + record.slot);
            CASettlementProgramRuntimeContract.Reconcile(record, map);
            CAProvisionRuntimeResolver.Reconcile(record, map);
            CASettlementSecurityAssignments.Reconcile(record, map,
                organization);
            CASettlementCapabilities.Reconcile(record, map, organization);
        }

        internal static void ReconcileCulturalExpression(
            CARegionalSettlementRecord record, Map map)
        {
            if (record == null) return;
            // The repair below was gated on a null culture, and the
            // record projection assigns an identity-only culture rather
            // than null - so it could never run, and a settlement that
            // reached materialization without one stayed without one.
            // It repairs an unanswering culture, which is what the
            // empty state actually is.
            if (!CASiteState.Answers(record.culture) && map != null)
            {
                CARegionalWorldComponent world =
                    CARegionalWorldComponent.Current;
                CARegionalPlan region = world?.FindRegionForMap(map);
                CARegionalSettlementPlan settlement = region?.settlements?
                    .FirstOrDefault(item => item != null
                        && item.slot == record.slot);
                if (settlement != null)
                {
                    CACultureHistory.EnsureSettlementCulture(region,
                        settlement);
                    record.culture = settlement.localCulture?.Copy();
                }
            }
            CACulturalExpression expression =
                CACulturalExpressionModel.ForMaterializedSettlement(record,
                    map);
            record.culturalExpressionSummary = expression.Summary;
            record.culturalExpressionSourceSignature =
                expression.SourceSignature;
            record.culturalExpressionStatus = (byte)expression.Status;
        }
    }

    // Generation-only evidence for authored settlement drift. BaseGen receipts
    // previously retained only totals, which proved that two Wo'bel buildings
    // disappeared before MapGenerated but could not identify the responsible
    // native phase. Keep an in-memory snapshot of stable Thing IDs and compare
    // it after each later genstep. This never changes map objects or saved data.
    internal static class CARegionalSettlementGenerationAudit
    {
        private sealed class Entry
        {
            internal CARegionalSettlementRecord record;
            internal Dictionary<string, string> buildings;
        }

        private static readonly Dictionary<int, Dictionary<string, Entry>>
            EntriesByMap = new Dictionary<int, Dictionary<string, Entry>>();

        internal static void Capture(CARegionalSettlementRecord record,
            Map map)
        {
            if (record == null || map == null || record.localRect == CellRect.Empty)
                return;
            Dictionary<string, Entry> entries;
            if (!EntriesByMap.TryGetValue(map.uniqueID, out entries))
            {
                entries = new Dictionary<string, Entry>();
                EntriesByMap.Add(map.uniqueID, entries);
            }
            entries[record.regionalId] = new Entry
            {
                record = record,
                buildings = Snapshot(record, map)
            };
        }

        internal static void Audit(Map map, string phase)
        {
            if (map == null) return;
            Dictionary<string, Entry> entries;
            if (!EntriesByMap.TryGetValue(map.uniqueID, out entries)) return;
            foreach (Entry entry in entries.Values)
            {
                Dictionary<string, string> current = Snapshot(entry.record, map);
                List<KeyValuePair<string, string>> removed = entry.buildings
                    .Where(pair => !current.ContainsKey(pair.Key)).ToList();
                List<KeyValuePair<string, string>> added = current
                    .Where(pair => !entry.buildings.ContainsKey(pair.Key))
                    .ToList();
                if (removed.Count == 0 && added.Count == 0) continue;
                Log.Message("[CA][Regional][SettlementAudit] "
                    + entry.record.regionalId + " " + entry.record.name
                    + " after " + phase + ": buildings "
                    + entry.buildings.Count + "->" + current.Count
                    + "; removed " + Describe(removed)
                    + "; added " + Describe(added));
                entry.buildings = current;
            }
        }

        internal static void Finish(Map map)
        {
            if (map != null) EntriesByMap.Remove(map.uniqueID);
        }

        private static Dictionary<string, string> Snapshot(
            CARegionalSettlementRecord record, Map map)
        {
            var result = new Dictionary<string, string>();
            if (record == null || map == null) return result;
            List<Thing> all = map.listerThings.AllThings;
            for (int i = 0; i < all.Count; i++)
            {
                Building building = all[i] as Building;
                if (building == null || !building.Spawned
                    || building.Faction != record.faction
                    || !record.localRect.Contains(building.Position)) continue;
                result[building.GetUniqueLoadID()] = building.def.defName;
            }
            return result;
        }

        private static string Describe(
            List<KeyValuePair<string, string>> changes)
        {
            if (changes.Count == 0) return "[none]";
            string counts = string.Join(",", changes
                .GroupBy(pair => pair.Value)
                .OrderBy(group => group.Key)
                .Select(group => group.Key + "=" + group.Count()));
            string ids = string.Join(",", changes.Take(24)
                .Select(pair => pair.Value + "#" + pair.Key));
            if (changes.Count > 24)
                ids += ",...+" + (changes.Count - 24);
            return "[count " + changes.Count + "; defs " + counts
                + "; ids " + ids + "]";
        }
    }

    public sealed class LordJob_CARegionalSettlement : LordJob
    {
        private Faction faction;
        private IntVec3 settlementCenter;

        public LordJob_CARegionalSettlement() { }

        public LordJob_CARegionalSettlement(Faction faction,
            IntVec3 settlementCenter)
        {
            this.faction = faction;
            this.settlementCenter = settlementCenter;
        }

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            // [politics lane] organizational defense: armed residents man
            // their organization's line when one exists; identical to stock
            // defend-base otherwise.
            var defend = new LordToil_CAOrganizationDefense(
                settlementCenter);
            graph.StartingToil = defend;
            var assault = new LordToil_AssaultColony(true)
            {
                useAvoidGrid = true
            };
            graph.AddToil(assault);

            var becameHostile = new Transition(defend, assault);
            becameHostile.AddTrigger(new Trigger_BecamePlayerEnemy());
            becameHostile.AddPostAction(new TransitionAction_WakeAll());
            graph.AddTransition(becameHostile);

            var peace = new Transition(assault, defend);
            peace.AddTrigger(new Trigger_BecameNonHostileToPlayer());
            graph.AddTransition(peace);
            return graph;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref settlementCenter, "settlementCenter",
                default, forceSave: true);
        }
    }

    internal static class CARegionalGenerationReceipt
    {
        private const string FileName =
            "CA-B14-regional-generation-receipt.txt";

        internal static void Write(CARegionalPlan region, Map map,
            CARegionalWorldComponent world, HashSet<int> materializedSlots,
            string gateFailure = null)
        {
            List<CARegionalSettlementPlan> settlements = (region?.settlements
                    ?? new List<CARegionalSettlementPlan>())
                .Where(item => item != null).OrderBy(item => item.slot)
                .ToList();
            var expectedSlots = new HashSet<int>(settlements.Select(
                item => item.slot));
            var actualSlots = materializedSlots ?? new HashSet<int>();
            Dictionary<int, CARegionalSettlementRecord> records = world == null
                || map == null
                ? new Dictionary<int, CARegionalSettlementRecord>()
                : world.ForMap(map).Where(item => item != null)
                    .GroupBy(item => item.slot)
                    .ToDictionary(group => group.Key, group => group.Last());

            int ownershipResolved = settlements.Count(settlement =>
                records.TryGetValue(settlement.slot,
                    out CARegionalSettlementRecord record)
                && record.OwningFactionKey == settlement.OwningFactionKey
                && (settlement.HasFactionOwner
                    ? CARegionalPlanUtility.MatchesFaction(record.faction,
                        region.FactionPlan(settlement.OwningFactionKey))
                    : record.faction == null));
            int populationsResolved = settlements.Count(settlement =>
                records.TryGetValue(settlement.slot,
                    out CARegionalSettlementRecord record)
                && PopulationMatches(settlement.populationGroups,
                    record.populationGroups));

            bool relationsValid = true;
            bool unsupportedSourceRejected = true;
            var relationFacts = new List<string>();
            foreach (CARegionalRelationPlan relation in (region?.relations
                         ?? new List<CARegionalRelationPlan>())
                     .Where(item => item != null)
                     .OrderBy(item => item.leftFactionKey)
                     .ThenBy(item => item.rightFactionKey))
            {
                string sourceFailure;
                bool valid = CARegionalPlanUtility.TryValidateRelationSource(
                    region, relation, out sourceFailure);
                relationsValid &= valid;
                relationFacts.Add(relation.leftFactionKey + "-"
                    + relation.rightFactionKey + ":" + relation.source + ":"
                    + relation.relation + (valid ? "" : ":INVALID(" +
                        sourceFailure + ")"));
            }
            CARegionalRelationPlan representedRelation = (region?.relations
                    ?? new List<CARegionalRelationPlan>())
                .FirstOrDefault(item => item != null);
            if (representedRelation != null)
            {
                var unsupported = new CARegionalRelationPlan
                {
                    leftFactionKey = representedRelation.leftFactionKey,
                    rightFactionKey = representedRelation.rightFactionKey,
                    relation = representedRelation.relation,
                    source = CARegionalRelationSource.Unset
                };
                unsupportedSourceRejected = !CARegionalPlanUtility
                    .TryValidateRelationSource(region, unsupported,
                        out string _);
            }

            bool gatePassed = gateFailure.NullOrEmpty();
            bool slotsComplete = actualSlots.SetEquals(expectedSlots);
            bool passed = gatePassed && slotsComplete
                && ownershipResolved == settlements.Count
                && populationsResolved == settlements.Count
                && relationsValid && unsupportedSourceRejected;
            string receipt = "[CA][B14][GenerationReceipt] "
                + (passed ? "PASS" : "FAIL") + " region="
                + (region?.regionalId ?? "unknown") + " gate="
                + (gatePassed ? "pass" : gateFailure) + " expected="
                + settlements.Count + " materialized=" + actualSlots.Count
                + " slots=" + string.Join(",", actualSlots.OrderBy(value =>
                    value)) + " ownership=" + ownershipResolved + "/"
                + settlements.Count + " populations=" + populationsResolved
                + "/" + settlements.Count + " relations="
                + string.Join(",", relationFacts) + " unsupportedSourceProbe="
                + (unsupportedSourceRejected ? "rejected" : "accepted");
            if (passed) Log.Message(receipt);
            else Log.Error(receipt);
            try
            {
                System.IO.Directory.CreateDirectory(
                    GenFilePaths.DevOutputFolderPath);
                System.IO.File.WriteAllText(System.IO.Path.Combine(
                    GenFilePaths.DevOutputFolderPath, FileName), receipt
                    + Environment.NewLine);
            }
            catch (Exception exception)
            {
                Log.Warning("[CA][B14][GenerationReceipt] could not write "
                    + "the external receipt: " + exception.Message);
            }
        }

        private static bool PopulationMatches(
            List<CASettlementPopulationGroup> planned,
            List<CASettlementPopulationGroup> recorded)
        {
            planned = planned ?? new List<CASettlementPopulationGroup>();
            recorded = recorded ?? new List<CASettlementPopulationGroup>();
            if (planned.Count != recorded.Count) return false;
            foreach (CASettlementPopulationGroup expected in planned)
            {
                if (expected == null) return false;
                CASettlementPopulationGroup actual = recorded.SingleOrDefault(
                    item => item != null && item.key == expected.key);
                if (actual == null || actual.kind != expected.kind
                    || actual.isPrimary != expected.isPrimary
                    || actual.label != expected.label
                    || actual.share != expected.share
                    || actual.factionKey != expected.factionKey
                    || actual.politicalBeliefsFactionKey
                        != expected.politicalBeliefsFactionKey
                    || actual.ideoligionCertainty
                        != expected.ideoligionCertainty
                    || actual.ideoligionFactionKey
                        != expected.ideoligionFactionKey
                    || actual.nativeIdeoligionId
                        != expected.nativeIdeoligionId
                    || actual.ideoligionProtected
                        != expected.ideoligionProtected
                    || actual.authored != expected.authored)
                    return false;
            }
            return true;
        }
    }

    public sealed class GenStep_CARegionalSettlements : GenStep
    {
        private sealed class Placement
        {
            internal CellRect Rect;
            internal float Score = float.MinValue;
            internal float WaterFraction = 1f;
        }

        public override int SeedPart
        {
            get { return 1557398107; }
        }

        public override void Generate(Map map, GenStepParams parms)
        {
            if (CARegionalCompatibility.IsMapPreviewGenerating()) return;

            CARegionalWorldComponent world = CARegionalWorldComponent.Current;
            if (world == null)
            {
                Log.Error("[CA][Regional] no regional world component during "
                    + "expanded-map materialization");
                return;
            }

            CARegionalPlan region = world.FindRegionForMap(map);
            if (region == null || region.settlements.Count == 0) return;
            string identityFailure;
            if (!CARegionalPlanUtility.TryValidateStableIdentities(region,
                    out identityFailure))
            {
                CARegionalGenerationReceipt.Write(region, map, world,
                    new HashSet<int>(), "stable identity: " + identityFailure);
                Log.Error("[CA][Regional] refused settlement materialization "
                    + "for " + (region.regionalId ?? "unknown") + ": "
                    + identityFailure);
                return;
            }
            CAExpandedLandmassProfile profile;
            if (!CAExpandedLandmassProfile.TryFor(region.mapSize,
                    out profile)) return;
            string realizationFailure;
            if (!CARegionalSettlements.TryValidateRealization(region,
                    out realizationFailure))
            {
                CARegionalGenerationReceipt.Write(region, map, world,
                    new HashSet<int>(), "realization: " + realizationFailure);
                Log.Error("[CA][Regional] refused settlement materialization "
                    + "for " + (region.regionalId ?? "unknown") + ": "
                    + realizationFailure);
                return;
            }
            foreach (CARegionalSettlementPlan settlement in region.settlements)
            {
                if (settlement != null
                    && !CASettlementProgramRegistry.TryValidateSaved(region,
                        settlement, out string programFailure))
                {
                    CARegionalGenerationReceipt.Write(region, map, world,
                        new HashSet<int>(), "settlement " + settlement.slot
                        + " program: " + programFailure);
                    Log.Error("[CA][Regional] refused settlement "
                        + "materialization for "
                        + (region.regionalId ?? "unknown") + ": settlement "
                        + settlement.slot + " program: " + programFailure);
                    return;
                }
            }
            CARegionalPlanResolver.Resolve(region);
            CARegionalProjectionMapComponent projection = map.GetComponent<
                CARegionalProjectionMapComponent>();
            int materialized = 0;
            var seenSlots = new HashSet<int>();
            var materializedSlots = new HashSet<int>();
            var physicalClusters = new Dictionary<string, List<CellRect>>();
            for (int index = 0; index < region.settlements.Count; index++)
            {
                CARegionalSettlementPlan settlement = region.settlements[index];
                if (settlement == null) continue;
                int slot = settlement.slot;
                if (slot < 0 || !seenSlots.Add(slot))
                {
                    Log.Warning("[CA][Regional] skipped authored settlement row "
                        + index + " because stable slot " + slot
                        + " is invalid or duplicated");
                    continue;
                }
                CARegionalSettlementRecord record = world.Find(
                    CARegionalWorldComponent.RegionKeyFor(region, map), slot,
                    region.mapSize);
                CARegionalFactionPlan group = CASiteState.OwnerPlan(region,
                    settlement.factionLinks);
                Faction faction = CASiteState.ResolveOwner(region,
                    settlement.factionLinks);
                if (settlement.HasFactionOwner && faction == null)
                {
                    Log.Warning("[CA][Regional] authored owner did not resolve for "
                        + "settlement " + slot);
                    continue;
                }
                if (group != null && group.resolvedFaction != faction)
                    group.resolvedFaction = faction;
                if (record != null && !record.persistent
                    && record.materializationCount > 0)
                {
                    Log.Message("[CA][Regional] encounter-only settlement "
                        + record.regionalId + " will not rematerialize");
                    continue;
                }
                if (record == null)
                    record = world.Create(map, slot, faction, settlement);
                else if (record.faction != faction
                    || record.OwningFactionKey
                        != settlement.OwningFactionKey)
                {
                    record.faction = faction;
                    record.factionLinks = settlement.factionLinks?.Copy()
                        ?? new CASiteFactionLinks();
                    record.generationFactionDefName = settlement
                        .generationFactionDefName ?? faction?.def?.defName;
                    record.localSociety = settlement.localSociety?.Copy();
                    record.materializationSummary += "; site affiliation reconciled at "
                        + Find.TickManager.TicksGame;
                }
                record.memberTileId = settlement.memberTileId;
                record.factionLinks = settlement.factionLinks?.Copy()
                    ?? new CASiteFactionLinks();
                record.localSociety = settlement.localSociety?.Copy();
                record.operationalRoleMask = settlement.operationalRoleMask;
                record.residentPopulation = settlement.residentPopulation;
                record.landCapacity = settlement.landCapacity;
                record.economicCapacity = settlement.economicCapacity;
                record.tradeConnectivity = settlement.tradeConnectivity;
                record.specialization = settlement.specialization;
                record.historicalDevelopment =
                    settlement.historicalDevelopment;
                record.urbanSupport = settlement.urbanSupport;
                record.realizedRole = settlement.realizedRole;
                record.realizedScale = settlement.realizedScale;
                record.persistent = settlement.persistent;
                // THE RUNTIME BOUNDARY RECEIPT. Every input that legitimately
                // controls physical synthesis, per settlement, side by side
                // in the log -- so when phenotypes converge, the receipt
                // states whether the inputs were identical or a downstream
                // consumer was lossy, instead of leaving it to inference.
                Log.Message("[CA][Settlement][Boundary] "
                    + (settlement.customName ?? ("slot " + settlement.slot))
                    + ": pop " + record.residentPopulation
                    + "; land " + record.landCapacity
                    + "; econ " + record.economicCapacity
                    + "; trade " + record.tradeConnectivity
                    + "; spec " + record.specialization
                    + "; hist " + record.historicalDevelopment
                    + "; urban " + record.urbanSupport
                    + "; roles " + record.operationalRoleMask
                    + "; form " + CASettlementAxes.Form(
                        settlement.authoredForm,
                        CATechnologicalKnowledgeRuntime
                            .CanonicalBuildTechLevel(record.faction))
                    + "; constructRank " + CATechnologicalKnowledgeRuntime
                        .CanonicalRank(record.faction,
                            CATechnologyDomains.Construction,
                            CATechnologyCompetencies.Construct)
                    + "; programs ["
                    + string.Join(", ",
                        (record.settlementProgram?.entries
                            ?? new List<CASettlementProgramEntry>())
                        .Where(e => e != null)
                        .Select(e => e.programKey
                            .Replace("ca.settlement.", "")
                            + ":" + e.count + "x" + e.extent)) + "]"
                    + "; authored pop/land/hist "
                    + settlement.authoredPopulation + "/"
                    + settlement.authoredLandCapacity + "/"
                    + settlement.authoredHistoricalDevelopment);

                CellRect rect;
                IntVec3 preferred = projection?.CenterForMember(
                    settlement.memberTileId) ?? map.Center;
                string physicalKey = settlement.memberTileId + ":"
                    + settlement.PhysicalClusterKey;
                if (!physicalClusters.TryGetValue(physicalKey,
                        out List<CellRect> clusterRects))
                {
                    clusterRects = new List<CellRect>();
                    physicalClusters.Add(physicalKey, clusterRects);
                }
                if (!TryFindSettlementRect(map, record, preferred,
                        clusterRects, out rect))
                {
                    Log.Warning("[CA][Regional] terrain rejected every candidate "
                        + "for " + record.regionalId + " on " + map.Tile);
                    continue;
                }
                // Districts belong to THIS settlement's urban standing. They
                // were counted from how many OTHER settlements shared the
                // member tile and cluster, so a settlement's internal
                // complexity was a property of its neighbours rather than of
                // its own authored development. The cluster count still sets
                // the floor, because co-sited settlements do join, but the
                // settlement's own standing may raise it.
                int clustered = region.settlements.Count(item => // [morphology lane]
                    item != null
                    && item.memberTileId == settlement.memberTileId
                    && item.PhysicalClusterKey
                        == settlement.PhysicalClusterKey);
                // The standing half of that rule was never implemented:
                // quarters came from programs and the floor came from
                // co-siting, so an urban centre and a hamlet sharing a
                // tile built the same internal complexity and the urban
                // tendency had no physical expression at all. A regional
                // centre earns a second quarter, an urban centre a third,
                // a large urban region a fourth. The cluster count
                // remains the floor.
                int standingQuarters = CAWorldTendencyCausalKernel
                    .SettlementQuarters(record.residentPopulation,
                        record.urbanSupport);
                // An anchor settlement (the one the player entered) keeps
                // its vanilla physical layout. CAO still creates the
                // settlement record with its full composition (culture,
                // programs, capabilities, organizations) so the anchor
                // participates in CAO\u2019s world model, but its physical
                // form is whatever RimWorld generated, not a CAO
                // morphology stamp.
                if (settlement.populationOrigin != CASettlementOrigin.Unset)
                {
                    Materialize(record, rect, map,
                        Math.Max(Math.Max(1, clustered),
                            standingQuarters)); // [morphology lane]
                    clusterRects.Add(rect);
                }
                materialized++;
                materializedSlots.Add(slot);
            }

            // Each multi-settlement faction's authority becomes member
            // relations carrying the responsibilities shared at faction
            // level. Authority changes update those relations without
            // rebuilding settlements.
            CASettlementAuthorityWriter.Materialize(region,
                world.ForMap(map));
            // The world map shows one faction-colored marker per settlement.
            CARegionalSettlementMarkers.Ensure(region, world.ForMap(map));
            CARegionalGenerationReceipt.Write(region, map, world,
                materializedSlots);

            Log.Message("[CA][Regional] materialized " + materialized + "/"
                + region.settlements.Count + " authored settlements on "
                + profile.Label + " " + map.Size.x + "x" + map.Size.z
                + " region " + region.regionalId + " ("
                + CARegionalSettlements.SettlementProfile(region)
                + "); bundle tiles "
                + string.Join(",", region.memberTileIds) + "; clear visual "
                + "horizon " + CABattlefieldPerception.ClearRangeFor(map)
                    .ToString("F1") + " cells");
        }

        private static bool TryFindSettlementRect(Map map,
            CARegionalSettlementRecord record, IntVec3 preferredCenter,
            List<CellRect> physicalCluster, out CellRect rect)
        {
            rect = CellRect.Empty;
            int activePrograms = record.settlementProgram?.entries?.Count(
                entry => entry != null && entry.blocker.NullOrEmpty()) ?? 0;
            // A PORT IS A CONSEQUENCE, NOT A TOGGLE. Placement penalized
            // water universally, so every settlement fled the shore and the
            // pier's own honest gate (real waterfront, a bridgeable run,
            // material knowledge) could never fire. A settlement whose
            // program carries trade or transport SEEKS moderate waterfront
            // instead: enough shore for a working pier, never so much that
            // the town stands in the sea. The hard 0.28 rejection stays for
            // everyone.
            bool seeksWaterfront = record.settlementProgram?.entries?.Any(
                entry => entry != null
                    && (entry.programKey
                            == CASettlementProgramCausalKernel.Trade
                        || entry.programKey
                            == CASettlementProgramCausalKernel.Transport))
                ?? false;
            // The ground reserved follows what the settlement's own
            // functions need (program count x extent) bounded by the land it
            // holds -- the same requirements morphology consumes -- never a
            // display scalar, and never one hard ceiling for every
            // settlement.
            int programCells = 0;
            foreach (CASettlementProgramEntry sizingEntry in
                record.settlementProgram?.entries
                    ?? new List<CASettlementProgramEntry>())
                if (sizingEntry != null)
                    programCells += Math.Max(1, sizingEntry.count)
                        * Math.Max(9, sizingEntry.extent * 9);
            int landBand = record.landCapacity < 0 ? 1
                : Math.Min(4, record.landCapacity);
            int sizingResidents = record.residentPopulation >= 18
                    && record.residentPopulation <= 1200
                ? record.residentPopulation
                : record.populationBaseline > 0
                    && record.populationBaseline <= 1200
                ? record.populationBaseline : 10;
            int wanted = (int)Mathf.Sqrt(
                Mathf.Max(sizingResidents * 35f + programCells * 5f, 1f)
                / 0.45f);
            int size = Mathf.Clamp(
                Mathf.Max(44 + record.realizedScale * 6
                        + Math.Min(12, activePrograms),
                    wanted),
                44, 96 + landBand * 16);
            List<CellRect> used = MapGenerator.UsedRects;

            if (record.localRect != CellRect.Empty
                && record.localRect.Width == size
                && record.localRect.Height == size
                && ValidRect(record.localRect, map, used, physicalCluster,
                    out var _))
            {
                rect = record.localRect;
                return true;
            }

            Placement best = new Placement();
            if (physicalCluster != null && physicalCluster.Count > 0)
            {
                foreach (CellRect anchor in physicalCluster)
                {
                    int half = size / 2;
                    var candidates = new[]
                    {
                        new CellRect(anchor.maxX + 5,
                            anchor.CenterCell.z - half, size, size),
                        new CellRect(anchor.minX - size - 4,
                            anchor.CenterCell.z - half, size, size),
                        new CellRect(anchor.CenterCell.x - half,
                            anchor.maxZ + 5, size, size),
                        new CellRect(anchor.CenterCell.x - half,
                            anchor.minZ - size - 4, size, size)
                    };
                    foreach (CellRect candidate in candidates)
                    {
                        float score;
                        if (!ValidRect(candidate, map, used,
                                physicalCluster, out score)) continue;
                        float water = SampleWaterFraction(candidate, map);
                        score += seeksWaterfront
                            ? 6f * Mathf.Max(0f,
                                1f - Mathf.Abs(water - 0.15f) / 0.13f)
                            : 6f - water * 6f;
                        if (score > best.Score)
                        {
                            best.Rect = candidate;
                            best.Score = score;
                            best.WaterFraction = water;
                        }
                    }
                }
                if (best.Rect != CellRect.Empty
                    && best.WaterFraction <= 0.28f)
                {
                    rect = best.Rect;
                    if (seeksWaterfront)
                        Log.Message("[CA][Settlement][Port] " + record.name
                            + ": trade/transport program sought waterfront; "
                            + "clustered rect water fraction "
                            + best.WaterFraction.ToString("F2"));
                    return true;
                }
            }
            for (int attempt = 0; attempt < 48; attempt++)
            {
                float angle = attempt * 137.50776f + Rand.Range(-12f, 12f);
                float radius = attempt == 0 ? 0f
                    : Mathf.Sqrt(attempt / 47f) * 90f;
                int x = preferredCenter.x + Mathf.RoundToInt(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * radius);
                int z = preferredCenter.z + Mathf.RoundToInt(
                    Mathf.Sin(angle * Mathf.Deg2Rad) * radius);
                CellRect candidate = CellRect.CenteredOn(
                    new IntVec3(x, 0, z), size, size);
                float score;
                if (!ValidRect(candidate, map, used, physicalCluster,
                        out score)) continue;
                float water = SampleWaterFraction(candidate, map);
                score += seeksWaterfront
                    ? 6f * Mathf.Max(0f,
                        1f - Mathf.Abs(water - 0.15f) / 0.13f)
                    : -water * 6f;
                if (score > best.Score)
                {
                    best.Rect = candidate;
                    best.Score = score;
                    best.WaterFraction = water;
                }
            }
            if (best.Rect == CellRect.Empty || best.WaterFraction > 0.28f)
                return false;
            rect = best.Rect;
            if (seeksWaterfront)
                Log.Message("[CA][Settlement][Port] " + record.name
                    + ": trade/transport program sought waterfront; chosen "
                    + "rect water fraction "
                    + best.WaterFraction.ToString("F2"));
            return true;
        }

        private static bool ValidRect(CellRect rect, Map map,
            List<CellRect> used, List<CellRect> physicalCluster,
            out float score)
        {
            score = float.MinValue;
            if (!rect.FullyContainedWithin(map.BoundsRect(24))) return false;
            for (int i = 0; i < used.Count; i++)
            {
                int spacing = physicalCluster != null
                    && physicalCluster.Contains(used[i]) ? 4 : 18;
                if (used[i].ExpandedBy(spacing).Overlaps(rect)) return false;
            }

            int samples = 0;
            int standable = 0;
            int road = 0;
            int roofed = 0;
            for (int x = rect.minX; x <= rect.maxX; x += 3)
            {
                for (int z = rect.minZ; z <= rect.maxZ; z += 3)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    samples++;
                    TerrainDef terrain = cell.GetTerrain(map);
                    if (cell.Standable(map)) standable++;
                    if (terrain != null && terrain.IsRoad) road++;
                    if (cell.Roofed(map)) roofed++;
                }
            }
            if (samples == 0) return false;
            float open = standable / (float)samples;
            if (open < 0.30f) return false;
            score = open * 3f + road / (float)samples * 2f
                - roofed / (float)samples;
            return true;
        }

        private static float SampleWaterFraction(CellRect rect, Map map)
        {
            int samples = 0;
            int water = 0;
            for (int x = rect.minX; x <= rect.maxX; x += 3)
            {
                for (int z = rect.minZ; z <= rect.maxZ; z += 3)
                {
                    TerrainDef terrain = new IntVec3(x, 0, z).GetTerrain(map);
                    samples++;
                    if (terrain != null && terrain.IsWater) water++;
                }
            }
            return samples == 0 ? 1f : water / (float)samples;
        }

        private static void Materialize(CARegionalSettlementRecord record,
            CellRect rect, Map map, int districts = 1) // [morphology lane]
        {
            record.localRect = rect;
            record.lastMapId = map.uniqueID;
            int now = Find.TickManager.TicksGame;
            if (record.firstMaterializationTick < 0)
                record.firstMaterializationTick = now;
            record.materializationCount++;
            record.relationAtMaterialization = record.faction == null
                ? "Unaffiliated"
                : record.faction.PlayerRelationKind.ToString();
            record.goodwillAtMaterialization = record.faction?.PlayerGoodwill
                ?? 0;
            Lord lord = LordMaker.MakeNewLord(record.faction,
                new LordJob_CARegionalSettlement(record.faction,
                    rect.CenterCell), map);
            // Build the settlement before spawning its population.
            CAMorphologyAdapter.Materialize(map, rect,
                CAMorphologyAdapter.FormFor(record),
                GenText.StableStringHash(record.regionalId),
                record.faction, districts, 1f, record);
            // Add apertures from technology, settlement form, and status.
            CAApertures.CutApertures(map, record);
            // Fit out the shell. The morphology resolves three thing defs;
            // the operator corpus resolves 151, and a quarter of everything
            // real players build is power. Beds, light, seating, a power
            // spine and site-oriented defence are added here, read from the
            // projection's own per-cell geography.
            CASettlementFitOut.Furnish(map, rect, record,
                GenText.StableStringHash(record.regionalId));
            // The land the settlement lives from: worked fields for its
            // agriculture operations, traced to their operating groups.
            // This is the self-sufficiency substrate and the causal end of
            // the settlement edge -- fabric meets fields where farming is
            // operated, and meets wilderness where it is not.
            CASettlementSubsistence.Materialize(map, rect, record,
                GenText.StableStringHash(record.regionalId));
            CASettlementDevelopmentProposal creationProposal =
                CASettlementAssetRegistry.CreationFromRecord(record);
            CASettlementAssetRegistry.EnsureCreationSitingCapacity(map,
                record, creationProposal, out string programSiteResult);
            MapGenerator.UsedRects.Add(rect);

            if (record.faction == null)
                MaterializeIndependentResidents(record, rect, map, lord);
            else
            {
                var groupParms = new PawnGroupMakerParms
                {
                    tile = map.Tile,
                    faction = record.faction,
                    groupKind = PawnGroupKindDefOf.Settlement,
                    inhabitants = true,
                    // The map population is a playable projection of the saved
                    // regional population, not a fresh population roll. Scale,
                    // training, and organization determine how much of it appears
                    // in this generated settlement encounter.
                    points = Mathf.Clamp(420f
                        + Mathf.Sqrt(Mathf.Max(0, record.residentPopulation)) * 28f
                        + record.realizedScale * 110f, 500f, 2400f),
                    seed = Gen.HashCombineInt(
                        GenText.StableStringHash(record.regionalId),
                        record.materializationCount)
                };
                var resolve = new ResolveParams
                {
                    rect = rect,
                    faction = record.faction,
                    singlePawnLord = lord,
                    pawnGroupKindDef = PawnGroupKindDefOf.Settlement,
                    pawnGroupMakerParams = groupParms,
                    attackWhenPlayerBecameEnemy = true,
                    // Native SymbolResolver_Settlement applies exactly this
                    // predicate to its inhabitants; pushing "pawnGroup"
                    // directly dropped it, letting residents spawn inside
                    // sealed courtyards outside the settlement's reachable
                    // network.
                    singlePawnSpawnCellExtraPredicate = cell =>
                        map.reachability.CanReachMapEdge(cell,
                            TraverseParms.For(TraverseMode.PassDoors))
                };
                BaseGen.globalSettings.map = map;
                BaseGen.symbolStack.Push("pawnGroup", resolve);
                BaseGen.Generate();
            }

            // Apply the authored population groups to spawned residents.
            CAPopulationProjection.Apply(record, map);

            // The confirmed composition owns creation authority. The selected
            // map rect supplies the remaining siting fact. Only their exact
            // conjunction makes the starting program executable.
            bool creationSitingFeasible = CASettlementAssetRegistry
                .CanSiteCreationDemands(map, rect, creationProposal,
                    out string creationSitingBlocker);
            CASettlementAssetRegistry.RecordCreationFacts(record,
                creationProposal, creationSitingFeasible,
                creationSitingBlocker);

            // Materialize only programs whose exact runtime operator resolves.
            // The local settlement organization and placement-authored work
            // relations are realized first because operator resolution is
            // read-only and never creates missing social state.
            CADomesticUnitFormation.Reconcile(record, map);
            CAOrganization programOperator =
                CAProvisionPlacementOperatorMaterializer.Materialize(record,
                    map);
            CASettlementProgramMaterializer.Materialize(programOperator,
                record, map);
            CAProvisionRuntimeResolver.Reconcile(record, map);

            CARegionalSettlementMapComponent.ReconcileRecord(record, map);
            CARegionalSettlementGenerationAudit.Capture(record, map);
            CARegionalSettlementMapComponent.ReconcileCulturalExpression(
                record, map);
            record.populationBaseline = Math.Max(record.populationBaseline,
                record.populationCurrent);
            record.lastReconciliationTick = now;
            Log.Message("[CA][Regional] settlement " + record.regionalId
                + " materialized: " + record.name + " of "
                + (record.faction?.Name ?? "no faction") + " at " + rect
                + "; native relation "
                + record.relationAtMaterialization + " goodwill "
                + record.goodwillAtMaterialization + "; tech "
                + record.TechnologicalKnowledgeLabel + "; "
                + record.CapabilityText()
                + "; declared operational roles "
                + record.OperationalRoleText()
                + "; population " + record.populationCurrent + ", buildings "
                + record.buildingCount);
        }

        private static void MaterializeIndependentResidents(
            CARegionalSettlementRecord record, CellRect rect, Map map,
            Lord lord)
        {
            FactionDef recipe = DefDatabase<FactionDef>.GetNamedSilentFail(
                record.generationFactionDefName);
            PawnKindDef kind = recipe?.pawnGroupMakers?
                .Where(maker => maker?.kindDef
                    == PawnGroupKindDefOf.Settlement)
                .SelectMany(maker => maker.options
                    ?? new List<PawnGenOption>())
                .Where(option => option?.kind?.RaceProps?.Humanlike == true)
                .OrderByDescending(option => option.selectionWeight)
                .Select(option => option.kind).FirstOrDefault()
                ?? PawnKindDefOf.Villager;
            int count = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(
                Mathf.Max(1, record.residentPopulation))), 2, 12);
            Rand.PushState(Gen.HashCombineInt(
                GenText.StableStringHash(record.regionalId),
                record.materializationCount, 17021, record.slot));
            try
            {
                for (int i = 0; i < count; i++)
                {
                    Pawn pawn = PawnGenerator.GeneratePawn(kind, null);
                    IntVec3 cell;
                    if (!CellFinder.TryFindRandomCellInsideWith(
                            rect, cellCandidate =>
                                cellCandidate.Standable(map)
                                && map.reachability.CanReachMapEdge(
                                    cellCandidate, TraverseParms.For(
                                        TraverseMode.PassDoors)),
                            out cell))
                        cell = CellFinder.RandomClosewalkCellNear(
                            rect.CenterCell, map, 18);
                    GenSpawn.Spawn(pawn, cell, map);
                    lord?.AddPawn(pawn);
                }
            }
            finally { Rand.PopState(); }
        }
    }

    [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateMap))]
    internal static class CARegionalMapGenerationPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref IntVec3 mapSize, MapParent parent,
            MapGeneratorDef mapGenerator,
            ref IEnumerable<GenStepWithParams> extraGenStepDefs,
            bool isPocketMap)
        {
            if (isPocketMap || parent == null || !parent.Tile.Valid
                || parent.Tile.Layer != Find.WorldGrid.Surface)
                return;

            bool preview = CARegionalCompatibility.IsMapPreviewGenerating();
            if (preview)
            {
                CARegionalPlan previewRegion =
                    CARegionalSetupSession.ActivePreviewPlan;
                if (previewRegion == null
                    || !previewRegion.memberTileIds.Contains(
                        parent.Tile.tileId)) return;
                mapSize = previewRegion.BackingMapSize;
                var previewSteps = extraGenStepDefs?.ToList()
                    ?? new List<GenStepWithParams>();
                AddOnce(previewSteps, CARegionalDefOf.CA_RegionalProjection);
                AddOnce(previewSteps, CARegionalDefOf.CA_RegionalWorldLinks);
                extraGenStepDefs = previewSteps;
                Log.Message("[CA][Regional] queued terrain-only regional "
                    + "preview for footprint " + previewRegion.regionalId
                    + "; members "
                    + string.Join(",", previewRegion.memberTileIds));
                return;
            }

            CAExpandedLandmassProfile profile;
            IntVec3 requestedMapSize = mapSize;
            if (!CAExpandedLandmassProfile.TryFor(requestedMapSize,
                    out profile)) return;

            CARegionalWorldComponent world = CARegionalWorldComponent.Current;
            if (world == null)
            {
                Log.Warning("[CA][Regional] no world component available for "
                    + "expanded surface map " + parent.Tile);
                return;
            }
            CARegionalPlan region = CARegionalSetupSession
                .PendingForCurrentWorld;
            if (region != null && region.mapSize == profile.Size
                && region.startTileId == parent.Tile.tileId)
            {
                string failure;
                if (region.developerExercise)
                {
                    // Exercise runs are intentionally non-durable: no member
                    // reservations or external source consumption are created.
                    if (!world.CanReserveRegion(region, out failure))
                        throw new InvalidOperationException("Regional exercise "
                            + region.regionalId + " cannot run: " + failure);
                    region = world.RegisterTransientDeveloperExercise(region);
                }
                else
                {
                    world.ValidateDurableRegion(region);
                    if (!world.TryReserveRegion(region, out failure))
                        throw new InvalidOperationException("Regional footprint "
                            + region.regionalId + " cannot be reserved: "
                            + failure);
                    // This is the irreversible boundary: the player has left
                    // every setup page and the engine is creating the map.
                    CARegionalPlanUtility.ConsumeReallocatedSources(region);
                    region = world.RegisterRegion(region);
                }
                CARegionalSetupSession.ClearPending();
            }
            else
                region = world.EnsureDerivedRegion(parent.Tile, profile,
                    parent.Faction);
            CARegionalPlanResolver.Resolve(region);
            // Resolution may create or find RimWorld faction objects, but the
            // confirmed authoring state must remain byte-for-byte causal input.
            // Revalidate before the backing map or any regional genstep exists.
            CARegionalWorldComponent.ValidateConfirmedComposition(region,
                "Resolved regional plan");
            mapSize = region.BackingMapSize;

            var steps = extraGenStepDefs?.ToList()
                ?? new List<GenStepWithParams>();
            AddOnce(steps, CARegionalDefOf.CA_RegionalProjection);
            AddOnce(steps, CARegionalDefOf.CA_RegionalWorldLinks);
            if (region.settlements.Count > 0)
            {
                AddOnce(steps, CARegionalDefOf.CA_RegionalSettlements);
                AddOnce(steps, CARegionalDefOf.CA_RegionalSettlementPower);
            }
            if (mapGenerator == MapGeneratorDefOf.Base_Player
                && parent.Faction == Faction.OfPlayerSilentFail)
                AddOnce(steps, CARegionalDefOf.CA_RegionalPlayerStart);
            extraGenStepDefs = steps;
            Log.Message("[CA][Regional] queued " + profile.Label
                + "; local tile scale " + requestedMapSize.x + "x"
                + requestedMapSize.z + "; aggregate backing " + mapSize.x
                + "x" + mapSize.z + " for surface tile " + parent.Tile
                + "; source members "
                + string.Join(",", region.memberTileIds) + "; landing "
                + region.startTileId + "; authored settlements "
                + region.settlements.Count);
        }

        private static void AddOnce(List<GenStepWithParams> steps,
            GenStepDef def)
        {
            if (def != null && !steps.Any(step => step.def == def))
                steps.Add(new GenStepWithParams(def, default(GenStepParams)));
        }
    }

    // A developer exercise deliberately creates a real Map, factions and
    // settlement records so the runtime can be inspected. Its plan is not
    // scribed, therefore none of those dependent objects may cross a save/load
    // boundary without becoming orphaned. Cover the UI/autosave predicate and
    // the canonical save entry point so mod-driven direct saves fail closed.
    [HarmonyPatch(typeof(GameDataSaveLoader), nameof(
        GameDataSaveLoader.SavingIsTemporarilyDisabled), MethodType.Getter)]
    internal static class CARegionalTransientExerciseSaveStatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref bool __result)
        {
            if (CARegionalWorldComponent.Current
                    ?.HasTransientDeveloperExercise == true)
                __result = true;
        }
    }

    [HarmonyPatch(typeof(GameDataSaveLoader), nameof(GameDataSaveLoader.SaveGame))]
    internal static class CARegionalTransientExerciseSavePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(string fileName)
        {
            if (CARegionalWorldComponent.Current
                    ?.HasTransientDeveloperExercise != true)
                return true;
            // The passive benchmark matrix's own disposable save is the
            // single exercise save that is structurally complete: the
            // transient plan scribes through its transient slot, the file
            // is reloaded inside the same disposable session, and it is
            // deleted at matrix completion. Every other save stays blocked.
            if (CAPassivePlayMatrix.AllowDisposableSave
                && fileName == CAPassivePlayMatrix.DisposableSaveName)
                return true;
            Log.Error("[CA][Regional] blocked save '"
                + (fileName ?? "(unnamed)") + "': a disposable regional "
                + "developer exercise is active and cannot enter durable "
                + "world state.");
            Messages.Message("This disposable regional exercise cannot be "
                + "saved. Return to the menu before starting a lasting game.",
                MessageTypeDefOf.RejectInput, false);
            return false;
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.DeinitAndRemoveMap))]
    internal static class CARegionalMapRemovalPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Map map)
        {
            map?.GetComponent<CARegionalSettlementMapComponent>()
                ?.Reconcile("pre-removal");
        }
    }

    public static partial class CADebugActions
    {
        [DebugAction("Colonist Awareness", "Regional living-world census",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RegionalLivingWorldCensus()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Log.Message("[CA][Regional] census: no current map");
                return;
            }
            CARegionalWorldComponent world = CARegionalWorldComponent.Current;
            CARegionalPlan region = world?.FindRegionForMap(map);
            CAExpandedLandmassProfile profile =
                default(CAExpandedLandmassProfile);
            bool expanded = region != null
                && CAExpandedLandmassProfile.TryFor(region.mapSize,
                    out profile);
            SurfaceTile surface = map.Tile.Tile as SurfaceTile;
            string roads = surface?.Roads.NullOrEmpty() != false ? "none"
                : surface.Roads.Select(r => r.road.defName).Distinct()
                    .ToCommaList();
            string rivers = surface?.Rivers.NullOrEmpty() != false ? "none"
                : surface.Rivers.Select(r => r.river.defName).Distinct()
                    .ToCommaList();
            string rocks = Find.World.NaturalRockTypesIn(map.Tile)
                .Select(r => r.defName).ToCommaList();
            float? coast = Find.World.CoastAngleAt(map.Tile, BiomeDefOf.Ocean);

            int waterCells = 0;
            int shoreCells = 0;
            int roadCells = 0;
            foreach (IntVec3 cell in map.AllCells)
            {
                TerrainDef terrain = cell.GetTerrain(map);
                if (terrain == null) continue;
                if (terrain.IsRoad) roadCells++;
                if (!terrain.IsWater) continue;
                waterCells++;
                for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
                {
                    IntVec3 adjacent = cell + GenAdj.CardinalDirections[i];
                    if (adjacent.InBounds(map)
                        && !adjacent.GetTerrain(map).IsWater)
                    {
                        shoreCells++;
                        break;
                    }
                }
            }
            int naturalRockCells = 0;
            int resourceRockCells = 0;
            List<Thing> things = map.listerThings.AllThings;
            for (int i = 0; i < things.Count; i++)
            {
                ThingDef def = things[i]?.def;
                if (def == null) continue;
                if (def.IsNonResourceNaturalRock) naturalRockCells++;
                if (def.building?.isResourceRock == true) resourceRockCells++;
            }

            Log.Message("[CA][Regional] census map " + map.uniqueID + " "
                + map.Size.x + "x" + map.Size.z + "; profile "
                + (expanded ? profile.Label + " ("
                    + region.RegionTileCount + " source tiles; local "
                    + region.mapSize + "; aggregate area "
                    + ((map.Size.x * map.Size.z)
                        / (float)(region.mapSize * region.mapSize))
                        .ToString("F2") + "x)"
                    : "standard") + "; tile " + map.Tile + "; biome "
                + map.Biome.defName + "; hilliness "
                + map.Tile.Tile.hilliness + "; ocean coast angle "
                + (coast.HasValue ? coast.Value.ToString("F1") : "none")
                + "; roads " + roads + "; rivers " + rivers
                + "; natural rock defs " + rocks + "; water cells "
                + waterCells + "; shore cells " + shoreCells
                + "; road cells " + roadCells + "; natural-rock cells "
                + naturalRockCells + "; resource-rock cells "
                + resourceRockCells + "; resolved clear visual horizon "
                + CABattlefieldPerception.ClearRangeFor(map).ToString("F1"));

            if (region != null)
            {
                Log.Message("[CA][Regional] plan " + region.regionalId
                    + "; origin " + (region.operatorAuthored
                        ? "operator-authored start" : "derived later region")
                    + "; bundle root " + region.bundleRootTileId
                    + "; landing/map anchor " + region.startTileId
                    + "; members " + region.memberTileIds.Count
                    + "; reserved world tiles "
                    + region.ReservedTileIds.Count
                    + "; planned settlements " + region.settlements.Count
                    + "; creation " + region.creationSummary);
                string reservationFailure;
                bool reservationValid = world.CanReserveRegion(region,
                    out reservationFailure);
                int reservationCount = world.ReservationCount(region);
                Log.Message("[CA][Regional] footprint reservation "
                    + reservationCount + "/"
                    + Math.Max(0, region.ReservedTileIds.Count - 1)
                    + " non-anchor world tiles; "
                    + (reservationValid ? "conflict-free" : "conflict: "
                        + reservationFailure));
                foreach (int memberTileId in region.memberTileIds)
                {
                    PlanetTile member = CARegionalPlanUtility.SurfaceTile(
                        memberTileId);
                    SurfaceTile memberSurface = member.Valid
                        ? member.Tile as SurfaceTile : null;
                    string memberRoads = memberSurface?.Roads.NullOrEmpty()
                            != false ? "none" : memberSurface.Roads
                            .Select(link => link.road.defName).Distinct()
                            .ToCommaList();
                    string memberRivers = memberSurface?.Rivers.NullOrEmpty()
                            != false ? "none" : memberSurface.Rivers
                            .Select(link => link.river.defName + "->"
                                + link.neighbor.tileId).Distinct()
                            .ToCommaList();
                    string assignments = region.settlements
                        .Where(item => item.memberTileId == memberTileId)
                        .Select(item => "settlement " + (item.slot + 1)
                            + "/owner " + (item.HasFactionOwner
                                ? item.OwningFactionKey.ToString()
                                : "none") + "/"
                            + (item.persistent ? "persistent" : "encounter"))
                        .ToCommaList();
                    Log.Message("[CA][Regional] member tile " + memberTileId
                        + (memberTileId == region.startTileId ? " [LANDING]" : "")
                        + "; biome "
                        + (memberSurface?.PrimaryBiome?.defName ?? "unknown")
                        + "; hilliness "
                        + (memberSurface?.hilliness.ToString() ?? "unknown")
                        + "; roads " + memberRoads + "; rivers "
                        + memberRivers + "; assignment "
                        + (assignments.NullOrEmpty() ? "none" : assignments));
                }
                CARegionalWorldPolicy policy = world.WorldPolicy;
                Log.Message("[CA][Regional] world tendencies: "
                    + "stitched-region frequency realized "
                    + policy.ResolveStitchedRegionFrequency().ToStringPercent()
                    + "; stitched-region size "
                    + policy.stitchedRegionSizeMin + "-"
                    + policy.stitchedRegionSizeMax
                    + "; settlement concentration "
                    + policy.settlementConcentration.ToStringPercent()
                    + "; frontier holding frequency "
                    + policy.frontierHoldingFrequency.ToStringPercent()
                    + "; frontier holding size "
                    + policy.frontierHoldingSize.ToStringPercent()
                    + "; settlement source variety "
                    + policy.reallocationSourceVariety.ToStringPercent()
                    + "; settlement development "
                    + policy.worldDevelopment.ToStringPercent()
                    + "; distant founding rate "
                    + policy.distantFoundingRate.ToStringPercent());
                foreach (CARegionalFactionPlan group in
                    region.factions.OrderBy(item => item.key))
                    Log.Message("[CA][Regional] faction " + group.key
                        + "; source " + group.source + "; resolved "
                        + (group.resolvedFaction?.Name ?? group.Summary)
                        + "; player relation "
                        + (group.authorPlayerRelation
                            ? group.playerRelation.ToString() : "native"));
                foreach (CARegionalRelationPlan relation in region.relations)
                    Log.Message("[CA][Regional] faction relation "
                        + relation.leftFactionKey + "-" + relation.rightFactionKey
                        + "; " + relation.relation + "; source "
                        + relation.source);
            }
            else if (expanded)
                Log.Warning("[CA][Regional] census found no regional plan for "
                    + "expanded map anchor " + map.Tile);

            int count = 0;
            if (world != null)
            {
                foreach (CARegionalSettlementRecord record in world.ForMap(map))
                {
                    count++;
                    CAFactionState factionState = CAFactionStateWorldComponent
                        .Current?.Find(record.faction);
                    string ideoligion = record.faction?.ideos?.PrimaryIdeo?.name
                        ?? "none";
                    string culture = factionState?.culture?.name ?? "unknown";
                    Log.Message("[CA][Regional] " + record.regionalId + " "
                        + record.name + "; faction "
                        + (record.faction?.Name
                            ?? record.generationFactionDefName
                            ?? "unaffiliated settlement")
                        + "; relation " + record.relationAtMaterialization
                        + " goodwill " + record.goodwillAtMaterialization
                        + "; " + record.TechnologicalKnowledgeLabel + "; "
                        + record.CapabilityText() + "; declared operational "
                        + "roles " + record.OperationalRoleText() + "; rect "
                        + record.localRect + "; population "
                        + record.populationCurrent + "/"
                        + record.populationBaseline + "; buildings "
                        + record.buildingCount + "; infrastructure "
                        + record.infrastructureCount + "; cultivated plants "
                        + record.cultivatedPlantCount + "; residents "
                        + record.residentIds.Count + "; Ideoligion "
                        + ideoligion + "; culture " + culture
                        + "; materializations "
                        + record.materializationCount + "; materialization "
                        + record.materializationSummary);
                }
            }
            if (count == 0)
                Log.Message("[CA][Regional] census: no materialized local "
                    + "settlements recorded on this map");

            Pawn selected = Find.Selector.SingleSelectedThing as Pawn;
            if (selected != null)
                Log.Message("[CA][Vision] " + selected.LabelShort + ": "
                    + CABattlefieldPerception.VisionProfileFor(selected)
                        .TraceText());
        }
    }
}
