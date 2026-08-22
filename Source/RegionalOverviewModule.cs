using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    public sealed class CARegionalOverviewMapComponent : MapComponent
    {
        private const int MaximumRasterDimension = 1024;

        private static readonly Color32 DeepOcean = new Color32(30, 73, 118, 255);
        private static readonly Color32 ShallowOcean = new Color32(54, 113, 153, 255);
        private static readonly Color32 InlandWater = new Color32(65, 133, 164, 255);
        private static readonly Color32 Sand = new Color32(190, 157, 101, 255);
        private static readonly Color32 Soil = new Color32(91, 124, 78, 255);
        private static readonly Color32 RichSoil = new Color32(66, 112, 70, 255);
        private static readonly Color32 Mud = new Color32(101, 91, 70, 255);
        private static readonly Color32 Gravel = new Color32(121, 117, 105, 255);
        private static readonly Color32 NaturalRock = new Color32(105, 105, 108, 255);
        private static readonly Color32 Road = new Color32(139, 125, 101, 255);
        private static readonly Color32 BuiltFloor = new Color32(113, 106, 99, 255);
        private static readonly Color32 ArtificialStructure = new Color32(54, 57, 61, 255);
        private static readonly Color32 OtherTerrain = new Color32(112, 126, 92, 255);

        private readonly Dictionary<TerrainDef, Color32> terrainColors =
            new Dictionary<TerrainDef, Color32>();

        private Texture2D overviewTexture;
        private Color32[] overviewPixels;
        private int rasterWidth;
        private int rasterHeight;
        private int cellsPerPixel;
        private readonly HashSet<int> dirtyRasterPixels = new HashSet<int>();

        public CARegionalOverviewMapComponent(Map map) : base(map) { }

        internal int RasterWidth => rasterWidth;
        internal int RasterHeight => rasterHeight;
        internal int CellsPerPixel => cellsPerPixel;

        internal Texture2D GetOrBuild()
        {
            if (overviewTexture != null)
            {
                ApplyDirtyPixels();
                return overviewTexture;
            }
            CARegionalProjectionMapComponent projection = map.GetComponent<
                CARegionalProjectionMapComponent>();
            if (projection?.Active != true) return null;

            var timer = Stopwatch.StartNew();
            cellsPerPixel = Math.Max(1, Mathf.CeilToInt(Mathf.Max(
                map.Size.x, map.Size.z) / (float)MaximumRasterDimension));
            rasterWidth = Mathf.CeilToInt(map.Size.x / (float)cellsPerPixel);
            rasterHeight = Mathf.CeilToInt(map.Size.z / (float)cellsPerPixel);
            overviewPixels = new Color32[rasterWidth * rasterHeight];
            Building[] edifices = map.edificeGrid?.InnerArray;

            for (int rz = 0; rz < rasterHeight; rz++)
            {
                for (int rx = 0; rx < rasterWidth; rx++)
                    overviewPixels[rz * rasterWidth + rx] = SamplePixel(
                        rx, rz, edifices);
            }

            overviewTexture = new Texture2D(rasterWidth, rasterHeight,
                TextureFormat.RGBA32, false)
            {
                name = "CA regional strategic overview " + map.uniqueID,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            overviewTexture.SetPixels32(overviewPixels);
            // Keep the small CPU copy readable so isolated map changes can
            // update their raster pixels without rebuilding the whole map.
            overviewTexture.Apply(false, false);
            dirtyRasterPixels.Clear();
            timer.Stop();
            Log.Message("[CA][Regional][Overview] built " + rasterWidth + "x"
                + rasterHeight + " strategic raster for " + map.Size.x + "x"
                + map.Size.z + " map " + map.uniqueID + " at "
                + cellsPerPixel + " cells/pixel in " + timer.ElapsedMilliseconds
                + " ms");
            return overviewTexture;
        }

        internal void MarkDirty(IntVec3 cell, bool includeAdjacent)
        {
            if (overviewTexture == null || cellsPerPixel <= 0
                || !cell.InBounds(map))
                return;
            int radius = includeAdjacent ? 1 : 0;
            for (int z = cell.z - radius; z <= cell.z + radius; z++)
            {
                if (z < 0 || z >= map.Size.z) continue;
                for (int x = cell.x - radius; x <= cell.x + radius; x++)
                {
                    if (x < 0 || x >= map.Size.x) continue;
                    int rx = x / cellsPerPixel;
                    int rz = z / cellsPerPixel;
                    if (rx >= 0 && rx < rasterWidth
                        && rz >= 0 && rz < rasterHeight)
                        dirtyRasterPixels.Add(rz * rasterWidth + rx);
                }
            }
        }

        private void ApplyDirtyPixels()
        {
            if (overviewTexture == null || overviewPixels == null
                || dirtyRasterPixels.Count == 0)
                return;
            Building[] edifices = map.edificeGrid?.InnerArray;
            foreach (int index in dirtyRasterPixels)
            {
                int rx = index % rasterWidth;
                int rz = index / rasterWidth;
                Color32 color = SamplePixel(rx, rz, edifices);
                overviewPixels[index] = color;
                overviewTexture.SetPixel(rx, rz, color);
            }
            overviewTexture.Apply(false, false);
            dirtyRasterPixels.Clear();
        }

        private Color32 SamplePixel(int rx, int rz, Building[] edifices)
        {
            int minX = rx * cellsPerPixel;
            int maxX = Math.Min(map.Size.x, minX + cellsPerPixel);
            int minZ = rz * cellsPerPixel;
            int maxZ = Math.Min(map.Size.z, minZ + cellsPerPixel);
            int red = 0;
            int green = 0;
            int blue = 0;
            int count = 0;
            bool artificial = false;
            for (int z = minZ; z < maxZ; z++)
            {
                int row = z * map.Size.x;
                for (int x = minX; x < maxX; x++)
                {
                    int index = row + x;
                    Color32 color = ColorFor(
                        map.terrainGrid.TerrainAt(index));
                    red += color.r;
                    green += color.g;
                    blue += color.b;
                    count++;
                    Building edifice = edifices != null
                        && index < edifices.Length ? edifices[index] : null;
                    artificial = artificial || (edifice != null
                        && edifice.def?.IsBuildingArtificial == true);
                }
            }
            return artificial ? ArtificialStructure
                : new Color32((byte)(red / Math.Max(1, count)),
                    (byte)(green / Math.Max(1, count)),
                    (byte)(blue / Math.Max(1, count)), 255);
        }

        internal void Invalidate()
        {
            if (overviewTexture != null)
                UnityEngine.Object.Destroy(overviewTexture);
            overviewTexture = null;
            overviewPixels = null;
            rasterWidth = 0;
            rasterHeight = 0;
            cellsPerPixel = 0;
            dirtyRasterPixels.Clear();
            terrainColors.Clear();
        }

        public override void MapRemoved()
        {
            Invalidate();
            base.MapRemoved();
        }

        private Color32 ColorFor(TerrainDef terrain)
        {
            if (terrain == null) return OtherTerrain;
            if (terrainColors.TryGetValue(terrain, out Color32 cached))
                return cached;

            string name = terrain.defName ?? string.Empty;
            Color32 color;
            if (terrain.IsOcean)
                color = name.IndexOf("Deep", StringComparison.OrdinalIgnoreCase) >= 0
                    ? DeepOcean : ShallowOcean;
            else if (terrain.IsWater)
                color = InlandWater;
            else if (name.IndexOf("Sand", StringComparison.OrdinalIgnoreCase) >= 0)
                color = Sand;
            else if (name.IndexOf("Mud", StringComparison.OrdinalIgnoreCase) >= 0)
                color = Mud;
            else if (name.IndexOf("Gravel", StringComparison.OrdinalIgnoreCase) >= 0)
                color = Gravel;
            else if (terrain.IsRock)
                color = NaturalRock;
            else if (terrain.IsRoad)
                color = Road;
            else if (terrain.IsFloor)
                color = BuiltFloor;
            else if (terrain.IsSoil && terrain.fertility > 1.05f)
                color = RichSoil;
            else if (terrain.IsSoil || terrain.fertility > 0.01f)
                color = Soil;
            else
                color = OtherTerrain;
            terrainColors[terrain] = color;
            return color;
        }
    }

    [HarmonyPatch(typeof(MapDrawer), nameof(MapDrawer.MapMeshDirty),
        new Type[] { typeof(IntVec3), typeof(ulong), typeof(bool), typeof(bool) })]
    internal static class CARegionalOverviewDirtyPatch
    {
        private static readonly FieldInfo MapField = AccessTools.Field(
            typeof(MapDrawer), "map");

        [HarmonyPostfix]
        private static void Postfix(MapDrawer __instance, IntVec3 loc,
            ulong dirtyFlags, bool regenAdjacentCells,
            bool regenAdjacentSections)
        {
            ulong visibleChanges = (ulong)MapMeshFlagDefOf.Terrain
                | (ulong)MapMeshFlagDefOf.Buildings;
            if ((dirtyFlags & visibleChanges) == 0UL) return;
            Map map = MapField?.GetValue(__instance) as Map;
            if (map?.GetComponent<CARegionalProjectionMapComponent>()
                    ?.Active != true)
                return;
            map.GetComponent<CARegionalOverviewMapComponent>()?.MarkDirty(loc,
                regenAdjacentCells || regenAdjacentSections);
        }
    }

    [HarmonyPatch(typeof(MapDrawer), nameof(MapDrawer.WholeMapChanged))]
    internal static class CARegionalOverviewWholeMapDirtyPatch
    {
        private static readonly FieldInfo MapField = AccessTools.Field(
            typeof(MapDrawer), "map");

        [HarmonyPostfix]
        private static void Postfix(MapDrawer __instance, ulong change)
        {
            ulong visibleChanges = (ulong)MapMeshFlagDefOf.Terrain
                | (ulong)MapMeshFlagDefOf.Buildings;
            if ((change & visibleChanges) == 0UL) return;
            Map map = MapField?.GetValue(__instance) as Map;
            map?.GetComponent<CARegionalOverviewMapComponent>()?.Invalidate();
        }
    }

    public sealed class MainButtonWorker_CARegionalOverview
        : MainButtonWorker_ToggleTab
    {
        public override bool Visible
        {
            get
            {
                if (!base.Visible) return false;
                if (Find.CurrentMap?.GetComponent<
                        CARegionalProjectionMapComponent>()?.Active == true)
                    return true;
                // A regional world keeps its control plane everywhere: any
                // region can be inspected from the globe, loaded or not.
                CARegionalWorldComponent world =
                    CARegionalWorldComponent.Current;
                return world != null && (world.Regions.Count > 0
                    || world.Topology.Count > 0);
            }
        }
    }

    public sealed class MainTabWindow_CARegionalOverview : MainTabWindow
    {
        private const float HeaderHeight = 32f;
        private const float FooterHeight = 26f;
        private const float MinimumZoom = 1f;
        private const float MaximumZoom = 8f;

        private float zoom = 1f;
        private Vector2 center = new Vector2(0.5f, 0.5f);
        private bool dragging;
        private Vector2 dragOrigin;
        private Vector2 dragCenter;
        private IntVec3 hoverCell = IntVec3.Invalid;

        public MainTabWindow_CARegionalOverview()
        {
            preventCameraMotion = true;
        }

        public override Vector2 RequestedTabSize => new Vector2(
            Mathf.Min(1200f, UI.screenWidth),
            Mathf.Min(900f, UI.screenHeight - 35f));

        // ONE VIEWPORT FOR EVERY REGION. With the loaded regional map in
        // front, the overview reads the actual persisted map. From the
        // globe, the selected region presents through the same viewport:
        // a registered region shows its persisted composition; an
        // unrealized partition region shows the deterministic projection
        // that materialization will realize. The identity is the region's,
        // never the clicked member's - the derived preview is cached by
        // region id, so selecting another member of the same region pans
        // the focus and regenerates nothing.
        private static readonly
            Dictionary<string, CARegionalPlan> worldViewPlans =
                new Dictionary<string, CARegionalPlan>();
        private string worldViewRegionId;
        private int worldViewFocusMember = -1;

        private CARegionalPlan ResolveWorldViewPlan(out int focusMember)
        {
            focusMember = -1;
            if (!WorldRendererUtility.WorldRendered) return null;
            PlanetTile tile = Find.WorldSelector?.SelectedTile
                ?? PlanetTile.Invalid;
            if (!tile.Valid)
            {
                // A selected regional-member world object counts as
                // selecting its region.
                WorldObject selected =
                    Find.WorldSelector?.SingleSelectedObject;
                if (selected != null) tile = selected.Tile;
            }
            if (!tile.Valid) return null;
            CARegionalWorldComponent world =
                CARegionalWorldComponent.Current;
            if (world == null) return null;
            CARegionalPlan registered = world.FindRegionContaining(tile);
            if (registered != null)
            {
                focusMember = registered.memberTileIds.Contains(tile.tileId)
                    ? tile.tileId : -1;
                return registered;
            }
            CARegionalTopologyRecord record = world.TopologyRecordAt(tile);
            if (record == null) return null;
            focusMember = tile.tileId;
            if (worldViewPlans.TryGetValue(record.regionId,
                    out CARegionalPlan cached)
                && cached?.memberTileIds != null
                && cached.memberTileIds.Count
                    == record.memberTileIds.Count)
                return cached;
            // THE PREVIEW MUST BE THE MAP THE PLAYER WOULD GET. This
            // asked the first registered region for its scale and fell
            // back to a literal 250, neither of which is what
            // materialization consults: it resolves the pending setup
            // size, then this game's chosen map size, then the world's
            // initial size. Guessing here meant an unrealized region was
            // previewed at a scale it would never materialize at. Same
            // order as the materialization path, so preview and result
            // agree.
            CAExpandedLandmassProfile profile;
            bool hasProfile = false;
            int chosen = Verse.Find.GameInitData?.mapSize ?? 0;
            if (chosen > 0)
                hasProfile = CAExpandedLandmassProfile.TryFor(chosen,
                    out profile);
            else profile = default(CAExpandedLandmassProfile);
            if (!hasProfile)
            {
                IntVec3 initial = Verse.Find.World?.info?.initialMapSize
                    ?? IntVec3.Zero;
                hasProfile = initial.x > 0 && initial.x == initial.z
                    && CAExpandedLandmassProfile.TryFor(initial.x,
                        out profile);
            }
            if (!hasProfile)
            {
                int registeredScale =
                    world.Regions.FirstOrDefault()?.mapSize ?? 0;
                hasProfile = registeredScale > 0
                    && CAExpandedLandmassProfile.TryFor(registeredScale,
                        out profile);
            }
            if (!hasProfile
                && !CAExpandedLandmassProfile.TryFor(250, out profile))
                return null;
            CARegionalPlan derived = CARegionalPlanUtility
                .CreateFromTopology(profile, record, tile);
            derived.worldPolicy = world.WorldPolicy.Copy();
            world.WorldPolicy.PopulateDerived(derived, profile, null);
            if (worldViewPlans.Count > 12) worldViewPlans.Clear();
            worldViewPlans[record.regionId] = derived;
            return derived;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Map map = Find.CurrentMap;
            CARegionalProjectionMapComponent projection = map?.GetComponent<
                CARegionalProjectionMapComponent>();
            CARegionalPlan worldPlan = ResolveWorldViewPlan(
                out int focusMember);
            bool worldMode = worldPlan != null
                && (projection?.Active != true
                    || (map != null && CARegionalWorldComponent.Current
                        ?.FindRegionForMap(map) != worldPlan));
            if (worldMode)
            {
                DrawWorldRegion(inRect, worldPlan, focusMember);
                return;
            }
            if (map == null || projection?.Active != true)
            {
                Widgets.Label(inRect, "Select a region on the world map, "
                    + "or open this overview from a regional map.");
                return;
            }

            CARegionalOverviewMapComponent overview = map.GetComponent<
                CARegionalOverviewMapComponent>();
            Texture2D texture = overview?.GetOrBuild();
            if (texture == null)
            {
                Widgets.Label(inRect, "The regional overview is not available.");
                return;
            }

            DrawHeader(inRect, map);
            Rect available = new Rect(inRect.x, inRect.y + HeaderHeight,
                inRect.width, inRect.height - HeaderHeight - FooterHeight);
            Rect mapRect = FitRect(available, texture.width,
                texture.height);
            Widgets.DrawBoxSolid(available, new Color(0.035f, 0.04f,
                0.045f, 1f));

            Rect uv = VisibleUv();
            GUI.DrawTextureWithTexCoords(mapRect, texture, uv, true);
            HandleMapInput(mapRect, uv, map);
            DrawCameraFrame(mapRect, uv, map);
            DrawLanding(mapRect, uv, projection);
            DrawSettlements(mapRect, uv, map);
            DrawPawns(mapRect, uv, map);
            DrawFooter(inRect, map, overview);
        }

        private void DrawWorldRegion(Rect inRect, CARegionalPlan plan,
            int focusMember)
        {
            bool regionChanged = worldViewRegionId != plan.regionalId;
            if (regionChanged)
            {
                worldViewRegionId = plan.regionalId;
                zoom = 1f;
                center = new Vector2(0.5f, 0.5f);
                worldViewFocusMember = -1;
            }
            CARegionalProjectionKernel kernel =
                CARegionalProjectionPreview.KernelFor(plan);
            Texture2D ground = CARegionalProjectionPreview.GroundFor(plan);
            if (kernel == null || ground == null)
            {
                Widgets.Label(inRect,
                    "Preparing the regional projection...");
                return;
            }

            CARegionalWorldComponent component =
                CARegionalWorldComponent.Current;
            bool registered = component?.Regions
                .Any(item => item != null
                    && item.regionalId == plan.regionalId) == true;
            Rect titleRect = new Rect(inRect.x, inRect.y,
                inRect.width - 320f, 28f);
            // The persisted political record when the region has one -
            // the remembered arrangement, not a fresh recomputation -
            // with the derived summary as the fallback.
            CARegionalPoliticalRecord political =
                component?.PoliticalRecordFor(plan.regionalId)
                ?? (plan.memberTileIds ?? new List<int>())
                    .Select(id => component?.TopologyRecordAt(id))
                    .Where(record => record != null)
                    .Select(record =>
                        component.PoliticalRecordFor(record.regionId))
                    .FirstOrDefault(record => record != null);
            string held = political?.StatusLine
                ?? CARegionalGeography.PoliticalSummary(plan);
            Widgets.Label(titleRect,
                CARegionalPlanUtility.RegionName(plan) + "  -  "
                + plan.RegionTileCount + " connected area"
                + (plan.RegionTileCount == 1 ? "" : "s") + ", "
                + plan.settlements.Count + " settlement"
                + (plan.settlements.Count == 1 ? "" : "s")
                + (held.NullOrEmpty() ? "" : "  -  " + held)
                + (registered ? "" : "  -  unrealized"));
            Rect zoomRect = new Rect(inRect.xMax - 310f, inRect.y + 2f,
                180f, 24f);
            float changed = Widgets.HorizontalSlider(zoomRect, zoom,
                MinimumZoom, MaximumZoom, false,
                "Zoom " + zoom.ToString("F2") + "x");
            if (!Mathf.Approximately(changed, zoom))
            {
                zoom = changed;
                ClampCenter();
            }
            if (Widgets.ButtonText(new Rect(inRect.xMax - 120f, inRect.y,
                    120f, 28f), "Fit region"))
            {
                zoom = 1f;
                center = new Vector2(0.5f, 0.5f);
            }

            Rect available = new Rect(inRect.x, inRect.y + HeaderHeight,
                inRect.width, inRect.height - HeaderHeight - FooterHeight);
            Rect mapRect = FitRect(available, ground.width, ground.height);
            Widgets.DrawBoxSolid(available, new Color(0.035f, 0.04f,
                0.045f, 1f));

            // Focus the clicked member's subarea once per selection: the
            // member is an address into the region, not a new view.
            if (focusMember >= 0 && focusMember != worldViewFocusMember)
            {
                worldViewFocusMember = focusMember;
                Vector2 anchor = kernel.VisualLandAnchor(focusMember);
                center = new Vector2(
                    (anchor.x + 0.5f) / kernel.Size.x,
                    (anchor.y + 0.5f) / kernel.Size.z);
                if (plan.RegionTileCount > 1 && zoom < 2f) zoom = 2f;
                ClampCenter();
            }

            Rect uv = VisibleUv();
            GUI.DrawTextureWithTexCoords(mapRect, ground, uv, true);
            HandleWorldViewInput(mapRect);

            foreach (CARegionalSettlementPlan settlement in plan.settlements
                .Where(item => item != null))
            {
                Vector2 anchor =
                    kernel.VisualLandAnchor(settlement.memberTileId);
                Vector2 point = WorldViewPoint(anchor, kernel, mapRect, uv);
                if (!mapRect.Contains(point)) continue;
                CARegionalFactionPlan group =
                    plan.FactionPlan(settlement.OwningFactionKey);
                Faction faction = group == null ? null
                    : CARegionalPlanUtility.FactionByLoadId(
                        group.existingFactionLoadId);
                int standing = settlement.realizedScale;
                CAPlaceGlyphs.DrawSettlement(point,
                    Mathf.Clamp(3.6f + standing * 0.3f, 3.6f, 5.2f),
                    standing, faction?.Color
                    ?? new Color(0.85f, 0.8f, 0.55f));
                float hit = 12f + standing * 3f;
                TooltipHandler.TipRegion(new Rect(point.x - hit * 0.5f,
                        point.y - hit * 0.5f, hit, hit),
                    CARegionalPlanUtility.SettlementName(plan, settlement)
                    + "\n" + (faction?.Name
                        ?? CARegionalPlanUtility.FactionName(group))
                    + "\nAssigned area: " + CARegionalPlanUtility
                        .TileWords(settlement.memberTileId));
            }

            // Frontier holdings on the ground the sites tendency filled:
            // hut-and-field farmsteads, resident and material facts in
            // the tooltip.
            foreach (CAFrontierHoldingPlan holding in plan.frontierHoldings
                .Where(item => item != null && item.memberTileId >= 0))
            {
                Vector2 anchor =
                    kernel.VisualLandAnchor(holding.memberTileId);
                Vector2 point = WorldViewPoint(anchor, kernel, mapRect, uv);
                point.y += 8f;
                if (!mapRect.Contains(point)) continue;
                CAPlaceGlyphs.DrawHolding(point, 3.2f,
                    holding.materialLevel);
                TooltipHandler.TipRegion(new Rect(point.x - 7f,
                        point.y - 6f, 14f, 12f),
                    (holding.siteName ?? (holding.form == 1
                        ? "Homestead" : "Cabin"))
                    + "\n" + holding.residentCount + " resident"
                    + (holding.residentCount == 1 ? "" : "s")
                    + ", material level " + holding.materialLevel);
            }

            // The region's named features - landmarks and historical
            // sites the ground actually carries - marked where they
            // stand, same monument glyph the setup preview uses.
            CARegionalCandidateFacts facts =
                CARegionalProjectionPreview.FactsFor(plan);
            foreach (CARegionalCandidateFacts.Feature feature in
                facts?.Features
                ?? Enumerable.Empty<CARegionalCandidateFacts.Feature>())
            {
                if (feature?.Tile.Valid != true) continue;
                Vector2 anchor = kernel.VisualLandAnchor(
                    feature.Tile.tileId);
                Vector2 point = WorldViewPoint(anchor, kernel, mapRect,
                    uv);
                if (!mapRect.Contains(point)) continue;
                Color tone = feature.Historical
                    ? new Color(0.82f, 0.72f, 0.46f)
                    : new Color(0.90f, 0.91f, 0.88f);
                var halo = new Color(0.05f, 0.06f, 0.07f, 0.85f);
                var stem = new Rect(point.x - 1f, point.y - 9f, 2f, 9f);
                var foot = new Rect(point.x - 3f, point.y - 1f, 6f, 2f);
                Widgets.DrawBoxSolid(stem.ExpandedBy(1f), halo);
                Widgets.DrawBoxSolid(foot.ExpandedBy(1f), halo);
                Widgets.DrawBoxSolid(stem, tone);
                Widgets.DrawBoxSolid(foot, tone);
                TooltipHandler.TipRegion(new Rect(point.x - 7f,
                        point.y - 12f, 14f, 15f),
                    feature.Name + "\n" + feature.Def.LabelCap);
            }

            PlanetTile arrival = plan.StartTile;
            if (arrival.Valid && registered)
            {
                Vector2 anchor = kernel.VisualLandAnchor(arrival.tileId);
                Vector2 point = WorldViewPoint(anchor, kernel, mapRect, uv);
                if (mapRect.Contains(point))
                {
                    GUI.color = new Color(0.98f, 0.93f, 0.74f);
                    Widgets.DrawBox(new Rect(point.x - 7f, point.y - 7f,
                        14f, 14f), 2);
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(new Rect(point.x - 7f,
                        point.y - 7f, 14f, 14f), "Arrival area");
                }
            }

            // The region's remembered history, over the map's lower-left
            // corner: the last few dated political events, so what
            // happened to this land is read where the land is shown.
            List<CARegionalPoliticalEvent> events = political?.events;
            if (events != null && events.Count > 0)
            {
                int shown = Math.Min(3, events.Count);
                Text.Font = GameFont.Tiny;
                float lineH = 15f;
                float panelH = shown * lineH + 22f;
                var panel = new Rect(mapRect.x + 6f,
                    mapRect.yMax - panelH - 6f,
                    Mathf.Min(430f, mapRect.width - 12f), panelH);
                Widgets.DrawBoxSolid(panel,
                    new Color(0.03f, 0.04f, 0.05f, 0.82f));
                GUI.color = new Color(0.62f, 0.66f, 0.68f);
                Widgets.Label(new Rect(panel.x + 7f, panel.y + 3f,
                    panel.width - 14f, 15f), "History");
                GUI.color = new Color(0.80f, 0.78f, 0.68f);
                for (int i = 0; i < shown; i++)
                {
                    CARegionalPoliticalEvent item =
                        events[events.Count - shown + i];
                    Widgets.Label(new Rect(panel.x + 7f,
                            panel.y + 19f + i * lineH,
                            panel.width - 14f, lineH),
                        CARegionalPoliticalLedger.EventLine(item));
                }
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            Rect footer = new Rect(inRect.x, inRect.yMax - FooterHeight + 4f,
                inRect.width, FooterHeight - 4f);
            Widgets.Label(footer, registered
                ? "This region is realized; its map returns exactly as it "
                    + "stands. Drag to pan; wheel to zoom."
                : "Deterministic regional projection - first entry "
                    + "materializes exactly this region. Drag to pan; "
                    + "wheel to zoom.");
        }

        private Vector2 WorldViewPoint(Vector2 kernelPoint,
            CARegionalProjectionKernel kernel, Rect mapRect, Rect uv)
        {
            float nx = (kernelPoint.x + 0.5f) / kernel.Size.x;
            float nz = (kernelPoint.y + 0.5f) / kernel.Size.z;
            float x = (nx - uv.x) / uv.width;
            float z = (nz - uv.y) / uv.height;
            return new Vector2(mapRect.x + x * mapRect.width,
                mapRect.yMax - z * mapRect.height);
        }

        private void HandleWorldViewInput(Rect mapRect)
        {
            Event current = Event.current;
            if (!mapRect.Contains(current.mousePosition))
            {
                if (current.type == EventType.MouseUp) dragging = false;
                return;
            }
            if (current.type == EventType.ScrollWheel)
            {
                float factor = current.delta.y < 0f ? 1.22f : 1f / 1.22f;
                float next = Mathf.Clamp(zoom * factor, MinimumZoom,
                    MaximumZoom);
                if (!Mathf.Approximately(next, zoom))
                {
                    zoom = next;
                    ClampCenter();
                }
                current.Use();
                return;
            }
            if (current.type == EventType.MouseDown && current.button == 0)
            {
                dragging = true;
                dragOrigin = current.mousePosition;
                dragCenter = center;
                current.Use();
                return;
            }
            if (current.type == EventType.MouseDrag && dragging
                && current.button == 0)
            {
                Vector2 delta = current.mousePosition - dragOrigin;
                float span = 1f / zoom;
                center.x = dragCenter.x - delta.x / mapRect.width * span;
                center.y = dragCenter.y + delta.y / mapRect.height * span;
                ClampCenter();
                current.Use();
                return;
            }
            if (current.type == EventType.MouseUp && dragging)
            {
                dragging = false;
                current.Use();
            }
        }

        private void DrawHeader(Rect inRect, Map map)
        {
            Rect titleRect = new Rect(inRect.x, inRect.y, 285f, 28f);
            Widgets.Label(titleRect, "Regional overview  " + map.Size.x + " x "
                + map.Size.z);

            Rect zoomRect = new Rect(titleRect.xMax + 6f, inRect.y + 2f,
                Mathf.Max(180f, inRect.width - 417f), 24f);
            float changed = Widgets.HorizontalSlider(zoomRect, zoom,
                MinimumZoom, MaximumZoom, false,
                "Zoom " + zoom.ToString("F2") + "x");
            if (!Mathf.Approximately(changed, zoom))
            {
                zoom = changed;
                ClampCenter();
            }

            Rect fitRect = new Rect(inRect.xMax - 120f, inRect.y, 120f, 28f);
            if (Widgets.ButtonText(fitRect, "Fit whole map"))
            {
                zoom = 1f;
                center = new Vector2(0.5f, 0.5f);
            }
        }

        private void HandleMapInput(Rect mapRect, Rect uv, Map map)
        {
            Event current = Event.current;
            if (!mapRect.Contains(current.mousePosition))
            {
                hoverCell = IntVec3.Invalid;
                if (current.type == EventType.MouseUp) dragging = false;
                return;
            }

            hoverCell = CellAt(current.mousePosition, mapRect, uv, map);
            if (current.type == EventType.ScrollWheel)
            {
                Vector2 pointer = NormalizedAt(current.mousePosition,
                    mapRect, uv);
                float factor = current.delta.y < 0f ? 1.22f : 1f / 1.22f;
                float next = Mathf.Clamp(zoom * factor, MinimumZoom,
                    MaximumZoom);
                if (!Mathf.Approximately(next, zoom))
                {
                    float px = (current.mousePosition.x - mapRect.x)
                        / mapRect.width;
                    float py = 1f - (current.mousePosition.y - mapRect.y)
                        / mapRect.height;
                    zoom = next;
                    float span = 1f / zoom;
                    center.x = pointer.x - (px - 0.5f) * span;
                    center.y = pointer.y - (py - 0.5f) * span;
                    ClampCenter();
                }
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDown
                && (current.button == 1 || (current.button == 0
                    && current.clickCount >= 2)))
            {
                ReturnToTactical(hoverCell);
                current.Use();
                return;
            }
            if (current.type == EventType.MouseDown && current.button == 0)
            {
                dragging = true;
                dragOrigin = current.mousePosition;
                dragCenter = center;
                current.Use();
                return;
            }
            if (current.type == EventType.MouseDrag && dragging
                && current.button == 0)
            {
                Vector2 delta = current.mousePosition - dragOrigin;
                float span = 1f / zoom;
                center.x = dragCenter.x - delta.x / mapRect.width * span;
                center.y = dragCenter.y + delta.y / mapRect.height * span;
                ClampCenter();
                current.Use();
                return;
            }
            if (current.type == EventType.MouseUp && dragging)
            {
                dragging = false;
                current.Use();
            }
        }

        private void DrawCameraFrame(Rect mapRect, Rect uv, Map map)
        {
            if (Find.CameraDriver == null) return;
            CellRect view = Find.CameraDriver.CurrentViewRect.ClipInsideMap(map);
            Rect frame = ScreenRectFor(view, mapRect, uv, map);
            frame = Intersect(frame, mapRect);
            if (frame.width <= 1f || frame.height <= 1f) return;
            Color prior = GUI.color;
            GUI.color = new Color(1f, 0.88f, 0.35f, 0.95f);
            Widgets.DrawBox(frame, 2);
            GUI.color = prior;
        }

        private void DrawLanding(Rect mapRect, Rect uv,
            CARegionalProjectionMapComponent projection)
        {
            CARegionalPlan region = projection.Region;
            if (region == null) return;
            IntVec3 landing = projection.CenterForMember(region.startTileId);
            DrawMarker(mapRect, uv, projection.map, landing,
                Color.white, 8f, "Landing region");
        }

        private void DrawSettlements(Rect mapRect, Rect uv, Map map)
        {
            CARegionalWorldComponent world = CARegionalWorldComponent.Current;
            if (world == null) return;
            foreach (CARegionalSettlementRecord record in world.ForMap(map))
            {
                if (record == null || record.localRect.Area <= 0) continue;
                Color color = record.faction?.Color
                    ?? new Color(0.85f, 0.8f, 0.55f);
                string tooltip = (record.name ?? "Regional settlement")
                    + " (" + record.populationCurrent + ")";
                if ((record.operationalRoleMask
                        & CARegionalOperationalRoles.KnownMask) != 0)
                    tooltip += "\nDeclared roles: "
                        + record.OperationalRoleText();
                Vector2 point = ScreenPointFor(
                    record.localRect.CenterCell, mapRect, uv, map);
                if (!mapRect.Contains(point)) continue;
                int standing = record.realizedScale;
                CAPlaceGlyphs.DrawSettlement(point,
                    Mathf.Clamp(3.4f + standing * 0.3f, 3.4f, 5f),
                    standing, color);
                float hit = 12f + standing * 3f;
                TooltipHandler.TipRegion(new Rect(point.x - hit * 0.5f,
                    point.y - hit * 0.5f, hit, hit), tooltip);
            }
        }

        private void DrawPawns(Rect mapRect, Rect uv, Map map)
        {
            IReadOnlyList<Pawn> pawns = map.mapPawns?.AllPawnsSpawned;
            if (pawns == null) return;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn?.RaceProps?.Humanlike != true) continue;
                Color color;
                if (pawn.Faction == Faction.OfPlayer)
                    color = new Color(0.25f, 1f, 0.55f);
                else if (pawn.HostileTo(Faction.OfPlayer))
                    color = ColorLibrary.RedReadable;
                else if (pawn.Faction?.PlayerRelationKind
                    == FactionRelationKind.Ally)
                    color = new Color(0.35f, 0.9f, 1f);
                else
                    color = new Color(1f, 0.82f, 0.3f);
                DrawMarker(mapRect, uv, map, pawn.Position, color, 5f,
                    pawn.LabelShortCap);
            }
        }

        private void DrawMarker(Rect mapRect, Rect uv, Map map, IntVec3 cell,
            Color color, float size, string tooltip)
        {
            Vector2 point = ScreenPointFor(cell, mapRect, uv, map);
            if (!mapRect.Contains(point)) return;
            Rect marker = new Rect(point.x - size * 0.5f,
                point.y - size * 0.5f, size, size);
            Widgets.DrawBoxSolid(marker, color);
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(marker, tooltip);
        }

        private void DrawFooter(Rect inRect, Map map,
            CARegionalOverviewMapComponent overview)
        {
            Rect footer = new Rect(inRect.x, inRect.yMax - FooterHeight + 4f,
                inRect.width, FooterHeight - 4f);
            string detail = "Drag to pan; wheel to zoom; right-click or "
                + "double-click to return to tactical view";
            if (hoverCell.IsValid)
            {
                TerrainDef terrain = map.terrainGrid.TerrainAt(hoverCell);
                BiomeDef biome = map.GetComponent<
                    CARegionalProjectionMapComponent>()?.BiomeAt(hoverCell)
                    ?? map.Biome;
                detail = "x " + hoverCell.x + ", z " + hoverCell.z + "  |  "
                    + (terrain?.LabelCap.ToString() ?? "unknown terrain")
                    + "  |  " + (biome?.LabelCap.ToString() ?? "unknown biome")
                    + "  |  " + overview.CellsPerPixel + " cells/pixel";
            }
            Widgets.Label(footer, detail);
        }

        private Rect VisibleUv()
        {
            float span = 1f / Mathf.Max(MinimumZoom, zoom);
            return new Rect(center.x - span * 0.5f,
                center.y - span * 0.5f, span, span);
        }

        private void ClampCenter()
        {
            float half = 0.5f / Mathf.Max(MinimumZoom, zoom);
            center.x = Mathf.Clamp(center.x, half, 1f - half);
            center.y = Mathf.Clamp(center.y, half, 1f - half);
        }

        private static Rect FitRect(Rect available, int width, int height)
        {
            float aspect = width / (float)Math.Max(1, height);
            float availableAspect = available.width / available.height;
            if (availableAspect > aspect)
            {
                float fittedWidth = available.height * aspect;
                return new Rect(available.center.x - fittedWidth * 0.5f,
                    available.y, fittedWidth, available.height);
            }
            float fittedHeight = available.width / aspect;
            return new Rect(available.x,
                available.center.y - fittedHeight * 0.5f,
                available.width, fittedHeight);
        }

        private static Vector2 NormalizedAt(Vector2 mouse, Rect mapRect,
            Rect uv)
        {
            float x = (mouse.x - mapRect.x) / mapRect.width;
            float y = 1f - (mouse.y - mapRect.y) / mapRect.height;
            return new Vector2(uv.x + x * uv.width,
                uv.y + y * uv.height);
        }

        private static IntVec3 CellAt(Vector2 mouse, Rect mapRect, Rect uv,
            Map map)
        {
            Vector2 normalized = NormalizedAt(mouse, mapRect, uv);
            return new IntVec3(
                Mathf.Clamp(Mathf.FloorToInt(normalized.x * map.Size.x),
                    0, map.Size.x - 1),
                0,
                Mathf.Clamp(Mathf.FloorToInt(normalized.y * map.Size.z),
                    0, map.Size.z - 1));
        }

        private static Vector2 ScreenPointFor(IntVec3 cell, Rect mapRect,
            Rect uv, Map map)
        {
            float x = (cell.x / (float)map.Size.x - uv.x) / uv.width;
            float z = (cell.z / (float)map.Size.z - uv.y) / uv.height;
            return new Vector2(mapRect.x + x * mapRect.width,
                mapRect.yMax - z * mapRect.height);
        }

        private static Rect ScreenRectFor(CellRect cells, Rect mapRect,
            Rect uv, Map map)
        {
            Vector2 lowerLeft = ScreenPointFor(
                new IntVec3(cells.minX, 0, cells.minZ), mapRect, uv, map);
            Vector2 upperRight = ScreenPointFor(
                new IntVec3(cells.maxX + 1, 0, cells.maxZ + 1),
                mapRect, uv, map);
            return Rect.MinMaxRect(lowerLeft.x, upperRight.y,
                upperRight.x, lowerLeft.y);
        }

        private static Rect Intersect(Rect left, Rect right)
        {
            return Rect.MinMaxRect(Mathf.Max(left.xMin, right.xMin),
                Mathf.Max(left.yMin, right.yMin),
                Mathf.Min(left.xMax, right.xMax),
                Mathf.Min(left.yMax, right.yMax));
        }

        private static void ReturnToTactical(IntVec3 cell)
        {
            if (!cell.IsValid || Find.CameraDriver == null) return;
            Find.MainTabsRoot.EscapeCurrentTab(false);
            Find.CameraDriver.JumpToCurrentMapLoc(cell);
        }
    }
}
