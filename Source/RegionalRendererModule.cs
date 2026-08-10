using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Read-only frame pacing evidence for regional maps. The engine currently
    // reports broken desktop v-sync and the completed RS-006 map consumes about
    // eight logical cores while paused behind its introduction modal. Measure
    // actual frame rate, process CPU, and CA map-drawer work separately before
    // choosing a pacing or renderer correction.
    internal static class CARegionalRendererTelemetry
    {
        private sealed class Sample
        {
            internal readonly System.Diagnostics.Stopwatch wall =
                System.Diagnostics.Stopwatch.StartNew();
            internal TimeSpan processCpu;
            internal int frames;
            internal long drawTicks;
            internal long updateTicks;
            internal int initializedSections;
            internal int dirtySections;
            internal int visibleSections;
            internal int dynamicSections;
            internal int updateCandidates;
            internal long nextReportMs = 5000L;
        }

        private static readonly Dictionary<int, Sample> Samples =
            new Dictionary<int, Sample>();

        internal static long Begin()
        {
            return System.Diagnostics.Stopwatch.GetTimestamp();
        }

        internal static void RecordDraw(Map map, long started,
            int initialized, int dirty, int visible, int dynamic)
        {
            if (map == null) return;
            Sample sample = For(map);
            sample.frames++;
            sample.drawTicks += System.Diagnostics.Stopwatch.GetTimestamp()
                - started;
            sample.initializedSections = initialized;
            sample.dirtySections = dirty;
            sample.visibleSections = visible;
            sample.dynamicSections = dynamic;
            ReportIfDue(map, sample);
        }

        internal static void RecordUpdate(Map map, long started,
            int candidates)
        {
            if (map == null) return;
            Sample sample = For(map);
            sample.updateTicks += System.Diagnostics.Stopwatch.GetTimestamp()
                - started;
            sample.updateCandidates = candidates;
        }

        internal static void Forget(Map map)
        {
            if (map != null) Samples.Remove(map.uniqueID);
        }

        private static Sample For(Map map)
        {
            Sample sample;
            if (Samples.TryGetValue(map.uniqueID, out sample)) return sample;
            sample = new Sample();
            using (System.Diagnostics.Process process =
                System.Diagnostics.Process.GetCurrentProcess())
                sample.processCpu = process.TotalProcessorTime;
            Samples.Add(map.uniqueID, sample);
            return sample;
        }

        private static void ReportIfDue(Map map, Sample sample)
        {
            long wallMs = sample.wall.ElapsedMilliseconds;
            if (wallMs < sample.nextReportMs) return;
            TimeSpan currentCpu;
            using (System.Diagnostics.Process process =
                System.Diagnostics.Process.GetCurrentProcess())
                currentCpu = process.TotalProcessorTime;
            double cpuMs = (currentCpu - sample.processCpu).TotalMilliseconds;
            double seconds = Math.Max(0.001, wallMs / 1000d);
            double frequency = System.Diagnostics.Stopwatch.Frequency;
            double drawMs = sample.drawTicks * 1000d / frequency;
            double updateMs = sample.updateTicks * 1000d / frequency;
            Log.Message("[CA][Regional][Renderer] pacing sample map "
                + map.uniqueID + ": " + sample.frames + " frames in "
                + wallMs + " ms (" + (sample.frames / seconds).ToString("F1")
                + " fps); process CPU " + (cpuMs / wallMs).ToString("F2")
                + " logical cores; CA draw "
                + (drawMs / Math.Max(1, sample.frames)).ToString("F3")
                + " ms/frame; CA update "
                + (updateMs / Math.Max(1, sample.frames)).ToString("F3")
                + " ms/frame; sections initialized "
                + sample.initializedSections + ", dirty "
                + sample.dirtySections + ", visible "
                + sample.visibleSections + ", offscreen dynamic "
                + sample.dynamicSections + ", update candidates "
                + sample.updateCandidates + "; vSyncCount "
                + QualitySettings.vSyncCount + ", targetFrameRate "
                + Application.targetFrameRate + ", display refresh "
                + Screen.currentResolution.refreshRateRatio.value.ToString("F2")
                + " Hz");
            sample.frames = 0;
            sample.drawTicks = 0L;
            sample.updateTicks = 0L;
            sample.wall.Restart();
            sample.processCpu = currentCpu;
            sample.nextReportMs = 30000L;
        }
    }

    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(MapEdgeClipDrawer),
        nameof(MapEdgeClipDrawer.DrawClippers))]
    internal static class CARegionalMapEdgeClipPatch
    {
        private const float ClipSize = 500f;

        private static readonly MaterialPropertyBlock HorizontalBlock =
            new MaterialPropertyBlock();
        private static readonly MaterialPropertyBlock VerticalBlock =
            new MaterialPropertyBlock();
        private static readonly HashSet<int> LoggedMaps = new HashSet<int>();

        [HarmonyPrefix]
        private static bool Prefix(Map map)
        {
            CARegionalProjectionMapComponent projection = map?.GetComponent<
                CARegionalProjectionMapComponent>();
            // Vanilla's dynamic west/east planes are already correct. Its
            // north/south planes are fixed at 1000 cells wide, so only regional
            // maps wider than that require replacement.
            if (projection?.Active != true || map.Size.x <= 1000) return true;
            if (!map.DrawMapClippers) return false;

            Material material = map.MapEdgeMaterial;
            float altitude = AltitudeLayer.WorldClipper.AltitudeFor();
            IntVec3 size = map.Size;

            // West and east own only the playable map's z span. North and south
            // extend through both 500-cell corner caps, producing one complete
            // non-overlapping exterior partition for any regional map width.
            Vector3 scale = new Vector3(ClipSize, 1f, size.z);
            Vector3 center = new Vector3(-ClipSize * 0.5f, 0f,
                size.z * 0.5f);
            Draw(scale, center, material, altitude, HorizontalBlock);
            center = new Vector3(size.x + ClipSize * 0.5f, 0f,
                size.z * 0.5f);
            Draw(scale, center, material, altitude, HorizontalBlock);

            scale = new Vector3(size.x + ClipSize * 2f, 1f, ClipSize);
            center = new Vector3(size.x * 0.5f, 0f,
                size.z + ClipSize * 0.5f);
            Draw(scale, center, material, altitude, VerticalBlock);
            center = new Vector3(size.x * 0.5f, 0f,
                -ClipSize * 0.5f);
            Draw(scale, center, material, altitude, VerticalBlock);

            if (LoggedMaps.Add(map.uniqueID))
                Log.Message("[CA][Regional][Renderer] replaced vanilla's "
                    + "1000-cell north/south map-edge mask with a complete "
                    + (size.x + (int)(ClipSize * 2f)) + "x" + (int)ClipSize
                    + " regional cap on " + size.x + "x" + size.z + " map "
                    + map.uniqueID);
            return false;
        }

        private static void Draw(Vector3 scale, Vector3 center,
            Material material, float altitude, MaterialPropertyBlock block)
        {
            block.SetVector(ShaderPropertyIDs.MainTextureScale, scale);
            block.SetVector(ShaderPropertyIDs.MainTextureOffset, center);
            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(center.WithYOffset(altitude), Quaternion.identity,
                scale);
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0, null, 0,
                block);
        }
    }

    internal static class CARegionalLazySectionUtility
    {
        internal const int SectionSize = 17;
        private static readonly HashSet<int> LazyMapIds = new HashSet<int>();
        internal static readonly FieldInfo MapField = AccessTools.Field(
            typeof(MapDrawer), "map");
        internal static readonly FieldInfo SectionsField = AccessTools.Field(
            typeof(MapDrawer), "sections");
        internal static readonly FieldInfo GlobalField = AccessTools.Field(
            typeof(MapDrawer), "global");
        internal static readonly FieldInfo GlobalDirtyFlagsField =
            AccessTools.Field(typeof(MapDrawer), "globalDirtyFlags");
        internal static readonly MethodInfo EnsureGlobalLayersMethod =
            AccessTools.Method(typeof(MapDrawer),
                "EnsureGlobalLayersInitialized");

        internal static Map MapFor(MapDrawer drawer)
        {
            return MapField?.GetValue(drawer) as Map;
        }

        internal static bool Active(Map map)
        {
            if (map == null) return false;
            if (LazyMapIds.Contains(map.uniqueID)) return true;
            if (map.GetComponent<CARegionalProjectionMapComponent>()
                    ?.Active != true)
                return false;
            Register(map);
            return true;
        }

        internal static void Register(Map map)
        {
            if (map != null) LazyMapIds.Add(map.uniqueID);
        }

        internal static void Unregister(Map map)
        {
            if (map != null) LazyMapIds.Remove(map.uniqueID);
        }

        internal static Section[,] EnsureArray(MapDrawer drawer, Map map)
        {
            Register(map);
            Section[,] sections = SectionsField?.GetValue(drawer)
                as Section[,];
            int width = Mathf.CeilToInt(map.Size.x / (float)SectionSize);
            int height = Mathf.CeilToInt(map.Size.z / (float)SectionSize);
            if (sections == null || sections.GetLength(0) != width
                || sections.GetLength(1) != height)
            {
                sections = new Section[width, height];
                SectionsField?.SetValue(drawer, sections);
            }
            return sections;
        }

        internal static Section EnsureSection(MapDrawer drawer, Map map,
            int x, int z)
        {
            Section[,] sections = EnsureArray(drawer, map);
            if (x < 0 || z < 0 || x >= sections.GetLength(0)
                || z >= sections.GetLength(1))
                return null;
            Section section = sections[x, z];
            if (section != null) return section;
            section = new Section(new IntVec3(x, 0, z), map)
            {
                dirtyFlags = ulong.MaxValue
            };
            sections[x, z] = section;
            return section;
        }
    }

    [HarmonyPatch(typeof(MapDrawer), nameof(MapDrawer.SectionAt))]
    internal static class CARegionalLazySectionAtPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MapDrawer __instance, IntVec3 loc,
            ref Section __result)
        {
            Map map = CARegionalLazySectionUtility.MapFor(__instance);
            if (!CARegionalLazySectionUtility.Active(map)) return true;
            int x = Mathf.FloorToInt(loc.x
                / (float)CARegionalLazySectionUtility.SectionSize);
            int z = Mathf.FloorToInt(loc.z
                / (float)CARegionalLazySectionUtility.SectionSize);
            __result = CARegionalLazySectionUtility.EnsureSection(__instance,
                map, x, z);
            return __result == null;
        }
    }

    [HarmonyPatch(typeof(MapDrawer), nameof(MapDrawer.MapMeshDirty),
        new Type[] { typeof(IntVec3), typeof(ulong), typeof(bool), typeof(bool) })]
    internal static class CARegionalLazyAdjacentSectionPatch
    {
        [HarmonyPrefix]
        private static void Prefix(MapDrawer __instance, IntVec3 loc,
            bool regenAdjacentSections)
        {
            if (!regenAdjacentSections) return;
            Map map = CARegionalLazySectionUtility.MapFor(__instance);
            if (!CARegionalLazySectionUtility.Active(map)) return;
            int sectionX = Mathf.FloorToInt(loc.x
                / (float)CARegionalLazySectionUtility.SectionSize);
            int sectionZ = Mathf.FloorToInt(loc.z
                / (float)CARegionalLazySectionUtility.SectionSize);
            for (int x = sectionX - 1; x <= sectionX + 1; x++)
                for (int z = sectionZ - 1; z <= sectionZ + 1; z++)
                    CARegionalLazySectionUtility.EnsureSection(__instance,
                        map, x, z);
        }
    }

    [HarmonyPatch(typeof(MapDrawer), nameof(MapDrawer.DrawMapMesh))]
    internal static class CARegionalLazyMapDrawPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MapDrawer __instance)
        {
            Map map = CARegionalLazySectionUtility.MapFor(__instance);
            if (!CARegionalLazySectionUtility.Active(map)) return true;
            long telemetryStart = CARegionalRendererTelemetry.Begin();
            CARegionalLazySectionUtility.EnsureGlobalLayersMethod?.Invoke(
                __instance, null);
            Section[,] sections = CARegionalLazySectionUtility.SectionsField
                ?.GetValue(__instance) as Section[,];
            if (sections == null)
                sections = CARegionalLazySectionUtility.EnsureArray(__instance,
                    map);
            List<MapDrawLayer> global = CARegionalLazySectionUtility.GlobalField
                ?.GetValue(__instance) as List<MapDrawLayer>;
            if (sections == null || global == null
                || Find.CameraDriver == null)
                return false;

            CellRect view = Find.CameraDriver.CurrentViewRect
                .ExpandedBy(1).ClipInsideMap(map);
            if (WorldComponent_GravshipController.GravshipRenderInProgess)
                view = view.Encapsulate(WorldComponent_GravshipController
                    .GravshipRenderBounds);
            for (int i = 0; i < global.Count; i++)
            {
                MapDrawLayer layer = global[i];
                if (!layer.Visible) continue;
                if (layer.Dirty)
                {
                    layer.Regenerate();
                    layer.RefreshSubMeshBounds();
                }
                layer.DrawLayer();
            }
            int initialized = 0;
            int dirty = 0;
            int visible = 0;
            int dynamic = 0;
            for (int x = 0; x < sections.GetLength(0); x++)
            {
                for (int z = 0; z < sections.GetLength(1); z++)
                {
                    Section section = sections[x, z];
                    if (section == null) continue;
                    initialized++;
                    if (section.dirtyFlags != 0UL) dirty++;
                    if (view.Overlaps(section.Bounds))
                    {
                        visible++;
                        section.DrawSection();
                    }
                    else
                    {
                        dynamic++;
                        section.DrawDynamicSections(view);
                    }
                }
            }
            CARegionalRendererTelemetry.RecordDraw(map, telemetryStart,
                initialized, dirty, visible, dynamic);
            return false;
        }
    }

    [HarmonyPatch(typeof(MapDrawer), nameof(MapDrawer.WholeMapChanged))]
    internal static class CARegionalLazyWholeMapPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MapDrawer __instance, ulong change)
        {
            Map map = CARegionalLazySectionUtility.MapFor(__instance);
            if (!CARegionalLazySectionUtility.Active(map)) return true;
            ulong globalDirty = CARegionalLazySectionUtility
                .GlobalDirtyFlagsField == null ? 0UL
                : (ulong)CARegionalLazySectionUtility.GlobalDirtyFlagsField
                    .GetValue(__instance);
            CARegionalLazySectionUtility.GlobalDirtyFlagsField?.SetValue(
                __instance, globalDirty | change);
            Section[,] sections = CARegionalLazySectionUtility.SectionsField
                ?.GetValue(__instance) as Section[,];
            if (sections == null) return false;
            for (int x = 0; x < sections.GetLength(0); x++)
                for (int z = 0; z < sections.GetLength(1); z++)
                    if (sections[x, z] != null)
                        sections[x, z].dirtyFlags |= change;
            return false;
        }
    }

    [HarmonyPatch(typeof(MapDrawer), nameof(MapDrawer.RegenerateLayerNow))]
    internal static class CARegionalLazySingleLayerPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MapDrawer __instance, Type type)
        {
            Map map = CARegionalLazySectionUtility.MapFor(__instance);
            if (!CARegionalLazySectionUtility.Active(map)) return true;
            CARegionalLazySectionUtility.EnsureGlobalLayersMethod?.Invoke(
                __instance, null);
            List<MapDrawLayer> global = CARegionalLazySectionUtility.GlobalField
                ?.GetValue(__instance) as List<MapDrawLayer>;
            Section[,] sections = CARegionalLazySectionUtility.SectionsField
                ?.GetValue(__instance) as Section[,];
            if (sections == null)
                sections = CARegionalLazySectionUtility.EnsureArray(__instance,
                    map);
            if (global == null || sections == null) return false;
            for (int i = 0; i < global.Count; i++)
            {
                MapDrawLayer layer = global[i];
                if (layer.GetType() != type || !layer.Visible) continue;
                layer.Regenerate();
                layer.RefreshSubMeshBounds();
            }
            for (int x = 0; x < sections.GetLength(0); x++)
            {
                for (int z = 0; z < sections.GetLength(1); z++)
                {
                    Section section = sections[x, z];
                    if (section == null) continue;
                    SectionLayer layer = section.GetLayer(type);
                    if (layer != null)
                        section.RegenerateSingleLayer(layer);
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(MapDrawer), nameof(MapDrawer.Dispose))]
    internal static class CARegionalLazyDisposePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MapDrawer __instance)
        {
            Map map = CARegionalLazySectionUtility.MapFor(__instance);
            if (!CARegionalLazySectionUtility.Active(map)) return true;
            Section[,] sections = CARegionalLazySectionUtility.SectionsField
                ?.GetValue(__instance) as Section[,];
            if (sections != null)
            {
                for (int x = 0; x < sections.GetLength(0); x++)
                    for (int z = 0; z < sections.GetLength(1); z++)
                        sections[x, z]?.Dispose();
                CARegionalLazySectionUtility.SectionsField.SetValue(__instance,
                    null);
            }
            List<MapDrawLayer> global = CARegionalLazySectionUtility.GlobalField
                ?.GetValue(__instance) as List<MapDrawLayer>;
            if (global != null)
            {
                for (int i = 0; i < global.Count; i++) global[i].Dispose();
                CARegionalLazySectionUtility.GlobalField.SetValue(__instance,
                    null);
            }
            CARegionalLazySectionUtility.Unregister(map);
            CARegionalRendererTelemetry.Forget(map);
            return false;
        }
    }
}
