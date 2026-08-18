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

        private static void PreviewWindowTileSelectedPrefix(object __instance,
            World world, ref PlanetTile tileId, ref MapParent mapParent)
        {
            if (__instance == null || world == null
                || previewDetermineMapSizeMethod == null
                || previewWidgetField == null
                || previewTextureProperty == null) return;
            try
            {
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
                CARegionalPlan plan = ResolvePreviewPlan(world, tileId);
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
                if (plan == null) return;
                IntVec2 size = new IntVec2(plan.BackingMapSize.x,
                    plan.BackingMapSize.z);
                EnsurePreviewMaximum(size);
                object widget = previewWidgetField.GetValue(__instance);
                Texture2D texture = widget == null ? null
                    : previewTextureProperty.GetValue(widget, null)
                        as Texture2D;
                if (texture != null
                    && (texture.width != size.x || texture.height != size.z)
                    && !texture.Reinitialize(size.x, size.z))
                {
                    Log.Warning("[CA][Regional] Map Preview texture rejected "
                        + "exact regional backing " + size.x + "x" + size.z);
                    return;
                }
                CARegionalGeographyComposition composition =
                    CARegionalGeographyContract.Inspect(plan);
                if (lastPreviewRequestSignature != composition.Signature)
                {
                    lastPreviewRequestSignature = composition.Signature;
                    Log.Message("[CA][Regional][Preview] request "
                        + composition.Signature + " bound to candidate "
                        + (plan.candidateId ?? "unknown") + "; arrival "
                        + plan.startTileId + "; exact backing " + size.x + "x"
                        + size.z + "; texture "
                        + (texture == null ? "unavailable" : texture.width
                            + "x" + texture.height));
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[CA][Regional] Map Preview exact-size texture "
                    + "resize failed: " + ex.GetType().Name + ": "
                    + ex.Message);
            }
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
            int count;
            if (pending?.memberTileIds != null
                && pending.memberTileIds.Contains(tile.tileId))
            {
                count = pending.RequestedRegionTileCount;
            }
            else
            {
                CARegionalWorldPolicy policy = pending?.worldPolicy
                    ?? CARegionalWorldComponent.Current?.WorldPolicy
                    ?? CAWorldTendenciesSession.Policy;
                int available = Math.Max(1, CARegionalBundleBuilder.Build(
                    tile, 12).Count);
                count = CARegionalSetupSession.StickyRegionTileCount > 0
                    ? CARegionalSetupSession.StickyRegionTileCount
                    : policy.ResolveRequestedExtent(tile, available);
            }
            CARegionalPlan plan = CARegionalSetupSession.EnsurePreviewPlan(
                tile, profile, count);
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
            return CARegionalSetupSession.EnsurePreviewPlan(tile, profile,
                count);
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
            __result = (__result ?? Enumerable.Empty<GenStepDef>())
                .Concat(new[]
                {
                    CARegionalDefOf.CA_RegionalProjection,
                    CARegionalDefOf.CA_RegionalWorldLinks
                }).Where(def => def != null).Distinct();
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
            if (map == null || plan == null || map.Size != plan.BackingMapSize
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
        public int thingId = -1;
        public string thingDefName;
        public string providerIdentity;
        public int provisionArrangementKey;
        public int provisionNodeIndex = -1;
        public int count;

        public void ExposeData()
        {
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
        public const int CurrentSchemaVersion = 9;
        public int schemaVersion = CurrentSchemaVersion;
        public string regionalId;
        public string name;
        // Stable region identity. Moving the landing tile does not change it.
        public string regionKey;
        public int slot;
        public int mapSize;
        public int memberTileId = -1;
        public int factionKey;
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
        public Faction faction;
        public string factionDefName;
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
        // Receipt of the faction-owned knowledge used at materialization.
        // The faction remains the owner; the settlement stores no second copy.
        public string factionKnowledgeId;
        public int factionKnowledgeRevision = -1;
        public int factionKnowledgeTier = -1;
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
            Scribe_Values.Look(ref factionKey, "factionKey", 0);
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
            Scribe_Values.Look(ref factionDefName, "factionDefName");
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
            Scribe_Values.Look(ref factionKnowledgeId,
                "factionKnowledgeId");
            Scribe_Values.Look(ref factionKnowledgeRevision,
                "factionKnowledgeRevision", -1);
            Scribe_Values.Look(ref factionKnowledgeTier,
                "factionKnowledgeTier", -1);
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

        internal string FactionKnowledgeLabel
        {
            get
            {
                return factionKnowledgeTier >= 0
                    ? "knowledge tier " + factionKnowledgeTier : "unknown";
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

        private int campaignSchemaVersion = 3;
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
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && readable)
            {
                CACampaignCompatibility.CompleteOwnerLoad(
                    "world.regional", ref campaignSchemaVersion,
                    legacyAuthoringDataEpoch, ValidateCampaignState,
                    MigrateSupportedState);
                RebuildAcceptedReadModels();
            }
            base.ExposeData();
        }

        private string ValidateCampaignState()
        {
            string structureFailure = ValidateOwnerStructure(
                CARegionalPlan.CurrentSchemaVersion);
            if (!structureFailure.NullOrEmpty()) return structureFailure;
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
                if (record.factionKnowledgeId.NullOrEmpty()
                    || record.factionKnowledgeRevision < 0
                    || record.factionKnowledgeTier < 0)
                    return "regional settlement " + key
                        + " has no faction-owned knowledge receipt";
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
            if (campaignSchemaVersion != 2)
                return "regional owner schema " + campaignSchemaVersion
                    + " has no supported migration";
            int savedRegionSchema = regions.Count == 0
                ? 13
                : regions[0]?.schemaVersion ?? -1;
            if (savedRegionSchema != 13)
                return "regional plan schema " + savedRegionSchema
                    + " has no supported B15 migration";
            string structureFailure = ValidateOwnerStructure(
                savedRegionSchema, expectedFoundingSchema: 3,
                expectedRecordSchema: 8);
            if (!structureFailure.NullOrEmpty()) return structureFailure;
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
                foreach (CARegionalFactionPlan faction in region.factions)
                {
                    if (faction == null)
                        return "regional plan " + region.regionalId
                            + " has a null faction";
                    if (!CACultureModel.TryUpgradeFromB10(faction.culture,
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
                    if (!CACultureModel.TryUpgradeFromB10(
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
                if (region.playerFounding.schemaVersion != 3)
                    return "regional founding-plan schema is "
                        + region.playerFounding.schemaVersion;
                if (!CACultureModel.TryUpgradeFromB10(
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
                if (!CACultureModel.TryUpgradeFromB10(record.culture,
                        out CACulture culture, out string cultureFailure))
                    return "regional settlement record " + record.regionalId
                        + "#" + record.slot + " Culture: " + cultureFailure;
                CATechnologicalKnowledge technology = null;
                CAFactionState liveState = record.faction == null ? null
                    : CAFactionStateWorldComponent.Current?.Find(
                        record.faction);
                if (liveState?.technologicalKnowledge?.domains?.Count > 0)
                    technology = liveState.technologicalKnowledge;
                if (technology == null)
                    knowledgeByFaction.TryGetValue(
                        (record.regionalId ?? "") + "#" + record.factionKey,
                        out technology);
                if (technology == null)
                    return "regional settlement record " + record.regionalId
                        + "#" + record.slot
                        + " cannot resolve its faction's Technological Knowledge";
                CARegionalSettlementRecord target = record;
                commits.Add(() =>
                {
                    target.culture = culture;
                    target.factionKnowledgeId = technology.id;
                    target.factionKnowledgeRevision = technology.revision;
                    target.factionKnowledgeTier =
                        CATechnologicalKnowledgeModel.CompatibilityTier(
                            technology);
                    target.schemaVersion =
                        CARegionalSettlementRecord.CurrentSchemaVersion;
                });
            }
            foreach (Action commit in commits) commit();
            return null;
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            if (!fromLoad)
                worldPolicy = CAWorldTendenciesSession.Policy.Copy();
            // A new world is a new set of engine-root receipts. Without this
            // the once-per-subject guard would silence the second world played
            // in a session.
            CARegionalEngineRoot.ForgetAnnouncements();
            if (!fromLoad || regions == null) return;
            if (!ReconcileReservationRegistry(out string failure))
                throw new InvalidOperationException(
                    "Regional reservation registry is invalid: " + failure);
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
                WorldObject conflict = objects.FirstOrDefault(item =>
                    matching.Count == 0 || item != matching[0]);
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
            string realizationFailure;
            if (!CARegionalSettlements.TryValidateRealization(region,
                    out realizationFailure))
                throw new InvalidOperationException("Regional plan "
                    + (region.regionalId ?? "unknown")
                    + " has invalid persisted settlement realization: "
                    + realizationFailure + ".");
            string identityFailure;
            if (!CARegionalPlanUtility.TryValidateStableIdentities(region,
                    out identityFailure))
                throw new InvalidOperationException("Durable region "
                    + (region.regionalId ?? "unknown") + " is invalid: "
                    + identityFailure + ".");
            string factionFailure;
            if (!CARegionalPlanUtility.TryValidateDistinctFactionClaims(region,
                    out factionFailure))
                throw new InvalidOperationException("Durable region "
                    + (region.regionalId ?? "unknown") + " is invalid: "
                    + factionFailure);
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
            if (region.worldPolicy == null)
                region.worldPolicy = WorldPolicy.Copy();
            transientDeveloperExerciseRegion = region;
            Log.Warning("[CA][Regional] transient developer exercise "
                + region.regionalId + " registered for this runtime only; it "
                + "is excluded from durable regional world state and cannot "
                + "be saved; compatibility at registration: "
                + (compatibility?.Summary() ?? "unavailable") + ".");
            return region;
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
            int availableTiles = Math.Max(1, CARegionalBundleBuilder.Build(
                mapTile, 12).Count);
            int regionTileCount = WorldPolicy.ResolveRequestedExtent(mapTile,
                availableTiles);
            CARegionalPlan derived = CARegionalPlanUtility.Create(profile,
                mapTile, false, regionTileCount);
            // The world component is the canonical owner of global tendencies;
            // every realized region carries a snapshot so later composition
            // stages cannot silently construct fresh defaults.
            derived.worldPolicy = WorldPolicy.Copy();
            WorldPolicy.PopulateDerived(derived, profile, parentFaction);
            // Validate before creating reservation world objects. RegisterRegion
            // validates again at the canonical write boundary, but that later
            // check cannot undo reservations already added to the world.
            ValidateDurableRegion(derived);
            string failure;
            if (!TryReserveRegion(derived, out failure))
                throw new InvalidOperationException("Regional footprint "
                    + derived.regionalId + " cannot be reserved: " + failure);
            // Auto-generated major settlements use the same RimWorld pool
            // transfer as authored reallocation. Frontier holdings are absent
            // from this list and never consume major-settlement authorization.
            CARegionalPlanUtility.ConsumeReallocatedSources(derived);
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
                settlement?.factionKey ?? -1);
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
            string culturalBasis = factionGroup?.culture == null
                ? "no carried cultural basis"
                : CACultureModel.Summary(factionGroup.culture);
            string politicalBasis = factionGroup?.politicalBeliefs == null
                ? "no recorded political basis"
                : CAPoliticalBeliefsModel.Summary(
                    factionGroup.politicalBeliefs);
            List<string> beneficiaries = settlement?.populationGroups == null
                ? new List<string> { "settlement residents" }
                : settlement.populationGroups.Where(group => group != null)
                    .Select(group => group.label
                        ?? "population group " + group.key)
                    .Where(label => !label.NullOrEmpty()).Distinct()
                    .OrderBy(label => label, StringComparer.Ordinal).ToList();
            if (beneficiaries.Count == 0)
                beneficiaries.Add("settlement residents");
            var record = new CARegionalSettlementRecord
            {
                regionalId = "CA-RS-" + (localRegion?.regionalId
                    ?? regionKey ?? "region") + "-" + slot,
                regionKey = regionKey,
                slot = slot,
                mapSize = localMapSize,
                memberTileId = settlement?.memberTileId ?? -1,
                factionKey = settlement?.factionKey ?? 0,
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
                culture = settlement?.localCulture?.Copy(),
                persistent = settlement?.persistent ?? true,
                faction = faction,
                factionDefName = faction?.def?.defName ?? "none",
                materializationSummary = "world seed + bundled world-tile member + "
                    + "operator-authored faction; native faction settlement "
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
                            label = populationGroup.label,
                            share = populationGroup.share,
                            factionKey = populationGroup.factionKey,
                            politicalBeliefsFactionKey =
                                populationGroup.politicalBeliefsFactionKey,
                            politicalBeliefsId = ResolvePoliticalBeliefsId(
                                localRegion,
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
            CATechnologicalKnowledge knowledge =
                CATechnologicalKnowledgeRuntime.ForFaction(faction);
            int knowledgeTier = CATechnologicalKnowledgeRuntime
                .CanonicalCompatibilityTier(faction);
            TechLevel compatibilityLevel =
                CATechnologicalKnowledgeRuntime.CanonicalBuildTechLevel(
                    faction);
            bool roadLinked = settlement?.hasRoadAccess == true;
            bool coastal = settlement?.hasCoastalAccess == true;
            record.factionKnowledgeId = knowledge?.id;
            record.factionKnowledgeRevision = knowledge?.revision ?? -1;
            record.factionKnowledgeTier = knowledgeTier;
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
                    : GenerateSettlementName(faction, records);
            records.Add(record);
            return record;
        }

        private static string ResolvePoliticalBeliefsId(CARegionalPlan plan,
            CASettlementPopulationGroup populationGroup)
        {
            if (populationGroup != null && !populationGroup.politicalBeliefsId.NullOrEmpty())
                return populationGroup.politicalBeliefsId;
            int key = populationGroup?.politicalBeliefsFactionKey >= 0
                ? populationGroup.politicalBeliefsFactionKey
                : populationGroup?.factionKey ?? -1;
            CARegionalFactionPlan source = plan?.FactionPlan(key);
            if (source == null) return null;
            return source.politicalBeliefs?.id;
        }

        private static string GenerateSettlementName(Faction faction,
            List<CARegionalSettlementRecord> records)
        {
            try
            {
                if (faction?.def?.settlementNameMaker != null)
                    return NameGenerator.GenerateName(
                        faction.def.settlementNameMaker,
                        records.Where(r => r != null && !r.name.NullOrEmpty())
                            .Select(r => r.name), true);
            }
            catch (Exception ex)
            {
                Log.Warning("[CA][Regional] local settlement naming fell back: "
                    + ex.Message);
            }
            return (faction?.Name ?? "Regional settlement") + " "
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
            if (record == null || map == null || record.faction == null) return;
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
            if (record.culture == null && map != null)
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
            if (record?.faction == null || map == null) return result;
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
            Scribe_Values.Look(ref settlementCenter, "settlementCenter");
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
                && record.factionKey == settlement.factionKey
                && CARegionalPlanUtility.MatchesFaction(record.faction,
                    region.FactionPlan(settlement.factionKey)));
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
                CARegionalFactionPlan group = region.FactionPlan(
                    settlement.factionKey);
                Faction faction = group?.resolvedFaction;
                if (!CARegionalPlanUtility.MatchesFaction(faction, group))
                    faction = record?.faction;
                if (!CARegionalPlanUtility.MatchesFaction(faction, group))
                    faction = null;
                else if (group.resolvedFaction != faction)
                    group.resolvedFaction = faction;
                if (faction == null)
                {
                    Log.Warning("[CA][Regional] authored faction "
                        + settlement.factionKey + " did not resolve for "
                        + "settlement " + slot);
                    continue;
                }
                if (record != null && !record.persistent
                    && record.materializationCount > 0)
                {
                    Log.Message("[CA][Regional] encounter-only settlement "
                        + record.regionalId + " will not rematerialize");
                    continue;
                }
                if (record == null)
                    record = world.Create(map, slot, faction, settlement);
                else if (record.faction != faction)
                {
                    record.faction = faction;
                    record.factionDefName = faction.def.defName;
                    record.materializationSummary += "; faction reference reconciled at "
                        + Find.TickManager.TicksGame;
                }
                record.memberTileId = settlement.memberTileId;
                record.factionKey = settlement.factionKey;
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
                int districts = region.settlements.Count(item => // [morphology lane]
                    item != null
                    && item.memberTileId == settlement.memberTileId
                    && item.PhysicalClusterKey
                        == settlement.PhysicalClusterKey);
                Materialize(record, rect, map,
                    Math.Max(1, districts)); // [morphology lane]
                clusterRects.Add(rect);
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
            int size = Mathf.Clamp(44 + record.realizedScale * 6
                + Math.Min(12, activePrograms), 44, 86);
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
                        score += 6f - water * 6f;
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
                score -= water * 6f;
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
            record.relationAtMaterialization = record.faction
                .PlayerRelationKind.ToString();
            record.goodwillAtMaterialization = record.faction.PlayerGoodwill;
            Lord lord = LordMaker.MakeNewLord(record.faction,
                new LordJob_CARegionalSettlement(record.faction,
                    rect.CenterCell), map);
            // Build the settlement before spawning its population.
            CAMorphologyAdapter.Materialize(map, rect,
                CAMorphologyAdapter.FormFor(record),
                GenText.StableStringHash(record.regionalId),
                record.faction, districts);
            // Add apertures from technology, settlement form, and status.
            CAApertures.CutApertures(map, record);
            MapGenerator.UsedRects.Add(rect);

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
                attackWhenPlayerBecameEnemy = true
            };
            BaseGen.globalSettings.map = map;
            BaseGen.symbolStack.Push("pawnGroup", resolve);
            BaseGen.Generate();

            // Apply the authored population groups to spawned residents.
            CAPopulationProjection.Apply(record, map);

            // Materialize only programs whose exact runtime operator resolves.
            // A settlement map no longer creates an organization as a side
            // effect of reaching this point.
            CADomesticUnitFormation.Reconcile(record, map);
            CAOrganization programOperator =
                CAOrganizationWorldComponent.Current?.ByKey(
                    record.regionalId + "#" + record.slot);
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
                + record.faction.Name + " at " + rect + "; native relation "
                + record.relationAtMaterialization + " goodwill "
                + record.goodwillAtMaterialization + "; tech "
                + record.FactionKnowledgeLabel + "; " + record.CapabilityText()
                + "; declared operational roles "
                + record.OperationalRoleText()
                + "; population " + record.populationCurrent + ", buildings "
                + record.buildingCount);
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
                            + "/faction " + item.factionKey + "/"
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
                    + "; urban growth propensity "
                    + policy.urbanGrowthPropensity.ToStringPercent()
                    + "; off-map activity rate "
                    + policy.offMapActivityRate.ToStringPercent());
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
                        + (record.faction?.Name ?? record.factionDefName)
                        + "; relation " + record.relationAtMaterialization
                        + " goodwill " + record.goodwillAtMaterialization
                        + "; " + record.FactionKnowledgeLabel + "; "
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
