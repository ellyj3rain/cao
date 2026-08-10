using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using RimWorld;
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
                return base.Visible && Find.CurrentMap?.GetComponent<
                    CARegionalProjectionMapComponent>()?.Active == true;
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

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Map map = Find.CurrentMap;
            CARegionalProjectionMapComponent projection = map?.GetComponent<
                CARegionalProjectionMapComponent>();
            if (map == null || projection?.Active != true)
            {
                Widgets.Label(inRect, "No regional map is currently selected.");
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
                DrawMarker(mapRect, uv, map, record.localRect.CenterCell,
                    color, 10f, tooltip);
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
