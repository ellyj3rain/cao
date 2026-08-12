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
        private static Delegate previousPreviewSizeOverride;
        private static Type previewApiType;
        private static PropertyInfo previewGeneratingProperty;
        private static FieldInfo previewGeneratingField;
        private static MethodInfo previewRefreshMethod;
        private static MethodInfo previewDetermineMapSizeMethod;
        private static FieldInfo previewWidgetField;
        private static PropertyInfo previewTextureProperty;

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
                PlanetLayerDef surface = PlanetLayerDefOf.Surface
                    ?? DefDatabase<PlanetLayerDef>.GetNamedSilentFail("Surface");
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
            try
            {
                Type sizeType = FindType("MapPreview.MapSizeUtility");
                previewApiType = FindType("MapPreview.MapPreviewAPI");
                if (sizeType == null && previewApiType == null) return;

                if (sizeType != null)
                {
                    FieldInfo max = AccessTools.Field(sizeType, "MaxMapSize");
                    if (max != null)
                    {
                        IntVec2 existing = max.GetValue(null) is IntVec2 value
                            ? value
                            : new IntVec2(0, 0);
                        max.SetValue(null, new IntVec2(
                            Math.Max(existing.x,
                                RegionalPreviewInitialDimension),
                            Math.Max(existing.z,
                                RegionalPreviewInitialDimension)));
                    }
                    FieldInfo sizeOverride = AccessTools.Field(sizeType,
                        "MapSizeOverride");
                    if (sizeOverride != null)
                    {
                        previousPreviewSizeOverride = sizeOverride.GetValue(null)
                            as Delegate;
                        Delegate regionalOverride = Delegate.CreateDelegate(
                            sizeOverride.FieldType, AccessTools.Method(
                                typeof(CARegionalCompatibility),
                                nameof(PreviewMapSizeOverride)));
                        sizeOverride.SetValue(null, regionalOverride);
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
                    "DetermineMapSize");
                if (previewDetermineMapSizeMethod != null)
                    previewHarmony.Patch(previewDetermineMapSizeMethod,
                        postfix: new HarmonyMethod(
                            typeof(CARegionalCompatibility),
                            nameof(PreviewDetermineMapSizePostfix)));
                Type previewWindowType = FindType(
                    "MapPreview.MapPreviewWindow");
                MethodInfo tileSelected = AccessTools.Method(
                    previewWindowType, "OnWorldTileSelected");
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
                            nameof(PreviewWindowTileSelectedPrefix)));
                }
                else
                {
                    Log.Warning("[CA][Regional] Map Preview exact-size texture "
                        + "compatibility could not be installed");
                }
                previewRefreshMethod = AccessTools.Method(
                    FindType("MapPreview.WorldInterfaceManager"),
                    "RefreshPreview");
                previewInstalled = true;
                Log.Message("[CA][Regional] Map Preview compatibility installed; "
                    + "regional requests use aggregate bounds and projection "
                    + "steps; preview textures resize to each exact backing "
                    + "frame; inhabited settlement materialization remains "
                    + "preview-inert");
            }
            catch (Exception ex)
            {
                Log.Warning("[CA][Regional] Map Preview compatibility failed: "
                    + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static IntVec2 PreviewMapSizeOverride()
        {
            CARegionalPlan plan = CARegionalSetupSession
                .PendingForCurrentWorld;
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
                CARegionalPlan plan = CARegionalWorldComponent.Current
                    ?.FindRegionContaining(tileId);
                CARegionalPlan pending = CARegionalSetupSession
                    .PendingForCurrentWorld;
                if (plan == null && pending?.memberTileIds != null
                    && pending.memberTileIds.Contains(tileId.tileId))
                    plan = pending;
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
                object rawSize = previewDetermineMapSizeMethod.Invoke(null,
                    new object[] { world, mapParent });
                if (!(rawSize is IntVec2 size) || size.x <= 0 || size.z <= 0)
                    return;
                object widget = previewWidgetField.GetValue(__instance);
                Texture2D texture = widget == null ? null
                    : previewTextureProperty.GetValue(widget, null)
                        as Texture2D;
                if (texture == null
                    || (texture.width == size.x && texture.height == size.z))
                    return;
                if (!texture.Reinitialize(size.x, size.z))
                {
                    Log.Warning("[CA][Regional] Map Preview texture rejected "
                        + "exact regional backing " + size.x + "x" + size.z);
                    return;
                }
                Log.Message("[CA][Regional] Map Preview texture resized to "
                    + size.x + "x" + size.z
                    + " for the exact regional backing frame");
            }
            catch (Exception ex)
            {
                Log.Warning("[CA][Regional] Map Preview exact-size texture "
                    + "resize failed: " + ex.GetType().Name + ": "
                    + ex.Message);
            }
        }

        private static void PreviewDetermineMapSizePostfix(World world,
            MapParent mapParent, ref IntVec2 __result)
        {
            PlanetTile tile = mapParent?.Tile
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
                return;
            }
            CARegionalPlan pending = CARegionalSetupSession
                .PendingForCurrentWorld;
            if (pending != null && pending.memberTileIds != null
                && pending.memberTileIds.Contains(tile.tileId))
            {
                __result = new IntVec2(pending.BackingMapSize.x,
                    pending.BackingMapSize.z);
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
                count = policy.ResolveRequestedExtent(tile, available);
            }
            CARegionalPlan plan = CARegionalSetupSession.EnsurePreviewPlan(
                tile, profile, count);
            if (plan == null) return;
            __result = new IntVec2(plan.BackingMapSize.x,
                plan.BackingMapSize.z);
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
        public string providerOrgKey;

        public void ExposeData()
        {
            Scribe_Values.Look(ref thingId, "thingId", -1);
            Scribe_Values.Look(ref thingDefName, "thingDefName");
            Scribe_Values.Look(ref providerOrgKey, "providerOrgKey");
        }
    }

    public sealed class CARegionalSettlementRecord : IExposable
    {
        public const int CurrentSchemaVersion = 4;
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
        public int communications;
        public int medicine;
        public int production;
        public int logistics;
        public int fortification;
        public int weapons;
        public int training;
        public int organization;
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
        public List<CAProvisionArrangement> provisionArrangements =
            new List<CAProvisionArrangement>();
        public CASettlementProgram settlementProgram =
            new CASettlementProgram();
        public CACulture culture;
        public List<string> populationAssignments = new List<string>();
        // Material wealth and the era represented by the original buildings.
        public int wealth = -1;
        public int constructionEra = -1;
        // Status assignments persist after their first materialization.
        public List<string> statusAssignments = new List<string>();
        // The faction's era baseline and the settlement's physical form at
        // materialization. Local capabilities remain separate fields.
        public int factionEra = -1;
        public int settlementForm = -1;
        public string generationSummary;
        // Receipt of the deterministic cultural read at materialization. The
        // live inspector may recompute from current saved facts as play changes.
        public string culturalExpressionSummary;
        public string culturalExpressionSourceSignature;
        public byte culturalExpressionStatus;
        public List<string> seededAssets = new List<string>();
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
            Scribe_Values.Look(ref communications, "communications", 0);
            Scribe_Values.Look(ref medicine, "medicine", 0);
            Scribe_Values.Look(ref production, "production", 0);
            Scribe_Values.Look(ref logistics, "logistics", 0);
            Scribe_Values.Look(ref fortification, "fortification", 0);
            Scribe_Values.Look(ref weapons, "weapons", 0);
            Scribe_Values.Look(ref training, "training", 0);
            Scribe_Values.Look(ref organization, "organization", 0);
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
            Scribe_Collections.Look(ref provisionArrangements,
                "provisionArrangements",
                LookMode.Deep);
            Scribe_Deep.Look(ref settlementProgram, "settlementProgram");
            Scribe_Deep.Look(ref culture, "culture");
            Scribe_Collections.Look(ref populationAssignments,
                "populationAssignments", LookMode.Value);
            Scribe_Values.Look(ref wealth, "wealth", -1);
            Scribe_Values.Look(ref constructionEra, "constructionEra", -1);
            Scribe_Collections.Look(ref statusAssignments,
                "statusAssignments", LookMode.Value);
            if (populationGroups == null) populationGroups = new List<CASettlementPopulationGroup>();
            if (provisionArrangements == null)
                provisionArrangements = new List<CAProvisionArrangement>();
            if (settlementProgram == null)
                settlementProgram = new CASettlementProgram();
            if (populationAssignments == null)
                populationAssignments = new List<string>();
            if (statusAssignments == null)
                statusAssignments = new List<string>();
            Scribe_Values.Look(ref factionEra, "factionEra", -1);
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
            if (Scribe.mode == LoadSaveMode.PostLoadInit && residentIds == null)
                residentIds = new List<string>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit
                && seededAssets == null)
                seededAssets = new List<string>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit
                && startingStock == null)
                startingStock = new List<CAStartingStockRecord>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit
                && developmentDemandKinds == null)
                developmentDemandKinds = new List<int>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit
                && developmentAssetCandidates == null)
                developmentAssetCandidates = new List<string>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (creationBeneficiaries == null)
                    creationBeneficiaries = new List<string>();
                if (developmentBeneficiaries == null)
                    developmentBeneficiaries = new List<string>();
                if (CASettlementAssetRegistry.ReconcileRecord(this,
                        out string correction))
                    Log.Warning("[CA][Regional] " + (regionalId ?? "settlement")
                        + " readback correction: " + correction);
                CACombatIntent.ObserveEpisode(creationEpisodeId);
                CACombatIntent.ObserveEpisode(developmentEpisodeId);
            }
        }

        internal string FactionEraLabel
        {
            get
            {
                return factionEra > (int)TechLevel.Undefined
                    ? ((TechLevel)factionEra).ToString() : "unknown";
            }
        }

        internal string CapabilityText()
        {
            return "comms " + communications + ", medicine " + medicine
                + ", production " + production + ", logistics " + logistics
                + ", fortification " + fortification + ", weapons " + weapons
                + ", training " + training + ", organization " + organization;
        }

        internal string OperationalRoleText()
        {
            return CARegionalOperationalRoles.Summary(operationalRoleMask);
        }
    }

    public sealed class CARegionalWorldComponent : WorldComponent
    {
        private int authoringDataEpoch = CAAuthoringDataEpoch.Current;
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
            Scribe_Values.Look(ref authoringDataEpoch,
                "CA_authoringDataEpoch", 0);
            bool current = Scribe.mode == LoadSaveMode.Saving
                || CAAuthoringDataEpoch.IsCurrent(authoringDataEpoch);
            if (current)
            {
                Scribe_Collections.Look(ref records,
                    "CA_regionalSettlements", LookMode.Deep);
                Scribe_Collections.Look(ref regions,
                    "CA_regionalPlans", LookMode.Deep);
                Scribe_Deep.Look(ref worldPolicy, "CA_regionalWorldPolicy");
                Scribe_Deep.Look(ref groundwater, "CA_groundwaterTuning");
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && !current)
            {
                records = new List<CARegionalSettlementRecord>();
                regions = new List<CARegionalPlan>();
                worldPolicy = new CARegionalWorldPolicy();
                groundwater = new CAGroundwaterTuning();
                transientDeveloperExerciseRegion = null;
                authoringDataEpoch = CAAuthoringDataEpoch.Current;
                CAAuthoringDataEpoch.RecordDiscard(
                    "regional authoring state");
            }
            if (records == null) records = new List<CARegionalSettlementRecord>();
            if (regions == null) regions = new List<CARegionalPlan>();
            if (worldPolicy == null) worldPolicy = new CARegionalWorldPolicy();
            // A world saved before this existed keeps the values that
            // were in force when it was made, which are the defaults.
            if (groundwater == null)
                groundwater = new CAGroundwaterTuning();
            base.ExposeData();
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
            if (!fromLoad || regions == null || regions.Count == 0) return;
            foreach (CARegionalPlan region in regions.Where(item =>
                item != null))
            {
                if (region.worldPolicy == null)
                    region.worldPolicy = WorldPolicy.Copy();
                string failure;
                if (!TryReserveRegion(region, out failure, true))
                    Log.Error("[CA][Regional] could not restore reservation "
                        + "for " + (region.regionalId ?? "unknown") + ": "
                        + failure);
            }
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
                    settlement?.economicCapacity ?? -1,
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
            int seed = Gen.HashCombineInt(world.info.Seed,
                GenText.StableStringHash(regionKey));
            seed = Gen.HashCombineInt(seed, localMapSize, slot, 0);
            var record = new CARegionalSettlementRecord
            {
                regionalId = "CA-RS-" + unchecked((uint)seed).ToString("X8"),
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
                            independentIdeoligionKey = populationGroup.independentIdeoligionKey,
                            quarter = populationGroup.quarter,
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
                            populationGroupKey = arrangement.populationGroupKey,
                            access = arrangement.access,
                            funding = arrangement.funding,
                            distribution = arrangement.distribution,
                            basisKey = arrangement.basisKey,
                            basisLabel = arrangement.basisLabel,
                            active = true,
                            waterSecured = arrangement.waterSecured,
                            nodes = arrangement.nodes,
                            reach = arrangement.reach
                        });

            CACulturalExpression culturalExpression =
                CACulturalExpressionModel.ForSettlement(localRegion,
                    settlement);
            record.culturalExpressionSummary = culturalExpression.Summary;
            record.culturalExpressionSourceSignature =
                culturalExpression.SourceSignature;
            record.culturalExpressionStatus =
                (byte)culturalExpression.Status;

            Rand.PushState(seed);
            try
            {
                // Knowledge comes from the faction; local practice comes from
                // the settlement's facilities and infrastructure.
                TechLevel knowledge = CASettlementAxes.TemplateEraPrior(
                    faction);
                bool roadLinked = CARegionalPlanUtility.ConstituentHasRoad(
                    settlement?.memberTileId ?? -1);
                bool coastal = CARegionalPlanUtility.ConstituentIsCoastal(
                    settlement?.memberTileId ?? -1);
                int ceiling = CASettlementAxes.LocalPracticeCeiling(
                    record.settlementProgram, record.accessInfrastructure,
                    record.serviceInfrastructure,
                    record.civicInfrastructure, knowledge);
                record.factionEra = (int)knowledge;
                record.settlementForm = (int)CASettlementAxes.Form(
                    settlement?.authoredForm ?? CASettlementAxes.Derive,
                    knowledge);
                record.generationSummary = CASettlementAxes.Provenance(
                    settlement?.authoredForm ?? CASettlementAxes.Derive,
                    record.settlementProgram)
                    + "; settlement program "
                    + record.settlementProgram?.sourceSignature
                    + "; practice ceiling " + ceiling
                    + (roadLinked ? "; road present" : "; no road")
                    + (coastal ? "; coast present" : "; inland");
                record.communications = CASettlementAxes.PracticedCapability(
                    CASettlementAxes.CapCommunications, knowledge,
                    ceiling);
                record.medicine = CASettlementAxes.PracticedCapability(
                    CASettlementAxes.CapMedicine, knowledge, ceiling);
                record.production = CASettlementAxes.PracticedCapability(
                    CASettlementAxes.CapProduction, knowledge, ceiling);
                record.logistics = CASettlementAxes.PracticedCapability(
                    CASettlementAxes.CapLogistics, knowledge, ceiling);
                record.fortification = CASettlementAxes.PracticedCapability(
                    CASettlementAxes.CapFortification, knowledge,
                    ceiling);
                record.weapons = CASettlementAxes.PracticedCapability(
                    CASettlementAxes.CapWeapons, knowledge, ceiling);
                record.training = CASettlementAxes.PracticedCapability(
                    CASettlementAxes.CapTraining, knowledge, ceiling);
                record.organization = CASettlementAxes.PracticedCapability(
                    CASettlementAxes.CapOrganization, knowledge,
                    ceiling);
                // Preview and materialization use the same wealth derivation.
                // Candidate identity is preferred; records without a plan use
                // the regional identity.
                CASettlementWealth.Derive(
                    localRegion?.candidateId ?? record.regionalId,
                    record.slot, record.settlementProgram,
                    record.accessInfrastructure,
                    record.serviceInfrastructure,
                    record.civicInfrastructure, knowledge,
                    out record.wealth, out record.constructionEra);
                // Preserve an authored settlement name.
                record.name = settlement != null
                    && !settlement.customName.NullOrEmpty()
                        ? settlement.customName
                        : GenerateSettlementName(faction, records);
            }
            finally
            {
                Rand.PopState();
            }
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
            foreach (CARegionalSettlementRecord record in world.ForMap(map))
            {
                if (record.localRect == CellRect.Empty) continue;
                ReconcileRecord(record, map);
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
            CARegionalSettlementMarkers.Ensure(region, world.ForMap(map));
            if (reason == "map-generated")
                CARegionalSettlementGenerationAudit.Finish(map);
        }

        internal static void ReconcileRecord(CARegionalSettlementRecord record,
            Map map)
        {
            if (record == null || map == null || record.faction == null) return;
            List<Pawn> residents = CAPopulationProjection.Residents(record,
                map);
            record.residentIds = residents.Select(pawn =>
                pawn.GetUniqueLoadID()).ToList();
            record.populationCurrent = residents.Count;

            int buildings = 0;
            int infrastructure = 0;
            int cultivated = 0;
            List<Thing> all = map.listerThings.AllThings;
            for (int i = 0; i < all.Count; i++)
            {
                Thing thing = all[i];
                if (thing == null || !thing.Spawned
                    || !record.localRect.Contains(thing.Position)) continue;
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
            record.buildingCount = buildings;
            record.infrastructureCount = infrastructure;
            record.cultivatedPlantCount = cultivated;
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
                Log.Error("[CA][Regional] refused settlement materialization "
                    + "for " + (region.regionalId ?? "unknown") + ": "
                    + identityFailure);
                return;
            }
            CAExpandedLandmassProfile profile;
            if (!CAExpandedLandmassProfile.TryFor(region.mapSize,
                    out profile)) return;
            CARegionalPlanResolver.Resolve(region);
            CARegionalProjectionMapComponent projection = map.GetComponent<
                CARegionalProjectionMapComponent>();
            int materialized = 0;
            var materializedSlots = new HashSet<int>();
            var physicalClusters = new Dictionary<string, List<CellRect>>();
            for (int index = 0; index < region.settlements.Count; index++)
            {
                CARegionalSettlementPlan settlement = region.settlements[index];
                if (settlement == null) continue;
                int slot = settlement.slot;
                if (slot < 0 || !materializedSlots.Add(slot))
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
                // Confirmed generation consumes the saved program exactly.
                // It cannot regenerate an authoring omission or accommodate
                // post-confirmation source drift.
                string realizationFailure;
                string programFailure = null;
                bool realizationValid = CARegionalSettlements
                    .TryValidateRealization(region, out realizationFailure);
                bool programValid = realizationValid
                    && CASettlementProgramRegistry.TryValidateSaved(region,
                        settlement, out programFailure);
                if (!realizationValid || !programValid)
                {
                    Log.Error("[CA][Regional] refused invalid confirmed "
                        + "settlement " + slot + ": "
                        + (realizationFailure ?? programFailure));
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
            }

            // Each multi-settlement faction's authority becomes member
            // relations carrying the responsibilities shared at faction
            // level. Authority changes update those relations without
            // rebuilding settlements.
            CASettlementAuthorityWriter.Materialize(region,
                world.ForMap(map));
            // The world map shows one faction-colored marker per settlement.
            CARegionalSettlementMarkers.Ensure(region, world.ForMap(map));

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
            int size = Mathf.Clamp(44 + record.realizedScale * 6
                + record.production * 2 + record.organization, 44, 86);
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
                    + record.realizedScale * 110f
                    + record.training * 110f
                    + record.organization * 70f, 500f, 2400f),
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
                + record.FactionEraLabel + "; " + record.CapabilityText()
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
                    + "; unaffiliated population "
                    + policy.unaffiliatedPopulationShare.ToStringPercent()
                    + "; settlement concentration "
                    + policy.settlementConcentration.ToStringPercent()
                    + "; frontier holding frequency "
                    + policy.frontierHoldingFrequency.ToStringPercent()
                    + "; frontier holding size "
                    + policy.frontierHoldingSize.ToStringPercent()
                    + "; settlement ownership variety "
                    + policy.reallocationSourceVariety.ToStringPercent()
                    + "; local faction formation "
                    + policy.localFactionChance.ToStringPercent()
                    + "; regional conflict "
                    + policy.regionalConflictChance.ToStringPercent()
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
                        + (relation.authorRelation
                            ? "starting-region override"
                            : "realized default"));
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
                        + "; era " + record.FactionEraLabel + "; "
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
