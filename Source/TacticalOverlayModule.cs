using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Module: tactical overlay painting. The player paints command intent onto
    // the map as colored layers - objectives, a defensive line, a fallback
    // line, an offensive line - and CA lanes CONSUME that intent: line
    // deployments decompose a painted line into hold slots, hold deviations
    // route to the fallback line, idle Autonomous fighters man the defensive
    // line when threats are known, and defense taskings anchor on objectives.
    // One layer per kind in v1; painting is a Command architect tab; layers
    // scribe with the map.
    public enum CAOverlayKind
    {
        Objective = 0,
        PrimaryObjective = 1,
        DefensiveLine = 2,
        FallbackLine = 3,
        OffensiveLine = 4
    }

    public class CAOverlayMapComponent : MapComponent
    {
        private List<IntVec3> objectiveCells = new List<IntVec3>();
        private List<IntVec3> primaryObjectiveCells = new List<IntVec3>();
        private List<IntVec3> defensiveLineCells = new List<IntVec3>();
        private List<IntVec3> fallbackLineCells = new List<IntVec3>();
        private List<IntVec3> offensiveLineCells = new List<IntVec3>();

        // Saturated, maximally separated hues - the layers should read at a
        // glance across a whole battlefield, and the tab buttons wear the
        // same colors so the tool and its paint match.
        internal static readonly Color ObjectiveColor = new Color(1f, 0.82f, 0f);
        internal static readonly Color PrimaryColor = new Color(1f, 0.22f, 0.08f);
        internal static readonly Color DefensiveColor = new Color(0.15f, 0.6f, 1f);
        internal static readonly Color FallbackColor = new Color(0.2f, 1f, 0.35f);
        internal static readonly Color OffensiveColor = new Color(1f, 0.15f, 0.85f);

        // Rendering cache. GenDraw.DrawFieldEdges clears a FULL-MAP BoolGrid
        // on every call - five painted layers would clear ~14M grid cells per
        // frame on the largest regional map. Persistent layers instead build
        // their edge matrices once per change (dirty flag) and draw the cached
        // instancing chunks; the mesh, material, edge test, and per-color
        // altitude jitter mirror DrawFieldEdges exactly, chunked to Unity's
        // 1023-instance draw cap.
        private const int InstanceCap = 1023;
        private readonly HashSet<IntVec3>[] cellSets = new HashSet<IntVec3>[5];
        private readonly List<List<Matrix4x4>>[] edgeChunks =
            new List<List<Matrix4x4>>[5];
        private readonly Material[] edgeMaterials = new Material[5];
        private bool renderDirty = true;

        public CAOverlayMapComponent(Map map) : base(map)
        {
            for (int i = 0; i < 5; i++)
            {
                cellSets[i] = new HashSet<IntVec3>();
                edgeChunks[i] = new List<List<Matrix4x4>>();
            }
        }

        public static CAOverlayMapComponent For(Map map)
        {
            return map?.GetComponent<CAOverlayMapComponent>();
        }

        public List<IntVec3> CellsOf(CAOverlayKind kind)
        {
            switch (kind)
            {
                case CAOverlayKind.Objective: return objectiveCells;
                case CAOverlayKind.PrimaryObjective: return primaryObjectiveCells;
                case CAOverlayKind.DefensiveLine: return defensiveLineCells;
                case CAOverlayKind.FallbackLine: return fallbackLineCells;
                default: return offensiveLineCells;
            }
        }

        public bool HasLayer(CAOverlayKind kind)
        {
            return CellsOf(kind).Count > 0;
        }

        public bool Contains(CAOverlayKind kind, IntVec3 cell)
        {
            return cellSets[(int)kind].Contains(cell);
        }

        public void Paint(CAOverlayKind kind, IntVec3 cell)
        {
            if (!cell.InBounds(map)) return;
            if (!cellSets[(int)kind].Add(cell)) return;
            CellsOf(kind).Add(cell);
            renderDirty = true;
        }

        public void Erase(IntVec3 cell)
        {
            bool changed = false;
            for (int i = 0; i < 5; i++)
            {
                if (!cellSets[i].Remove(cell)) continue;
                CellsOf((CAOverlayKind)i).Remove(cell);
                changed = true;
            }
            if (changed) renderDirty = true;
        }

        public bool AnyPaint(IntVec3 cell)
        {
            for (int i = 0; i < 5; i++)
                if (cellSets[i].Contains(cell)) return true;
            return false;
        }

        public IntVec3 NearestCell(CAOverlayKind kind, IntVec3 from,
            float maxDistance)
        {
            var cells = CellsOf(kind);
            IntVec3 best = IntVec3.Invalid;
            float bestDistance = maxDistance;
            for (int i = 0; i < cells.Count; i++)
            {
                float d = from.DistanceTo(cells[i]);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = cells[i];
                }
            }
            return best;
        }

        // Decompose a painted layer into COUNT evenly spread standable slots.
        // The paint is a freeform cell set; chain it nearest-neighbor from an
        // extremal cell so "evenly spread" follows the drawn shape, then sample.
        public List<IntVec3> SlotsAlong(CAOverlayKind kind, int count)
        {
            var result = new List<IntVec3>();
            var cells = CellsOf(kind);
            if (cells.Count == 0 || count <= 0) return result;
            var chain = ChainOrder(cells);
            if (count >= chain.Count)
            {
                for (int i = 0; i < chain.Count; i++)
                    AddStandableSlot(result, chain[i]);
                return result;
            }
            for (int k = 0; k < count; k++)
            {
                int index = count == 1 ? chain.Count / 2
                    : (int)((long)k * (chain.Count - 1) / (count - 1));
                AddStandableSlot(result, chain[index]);
            }
            return result;
        }

        private void AddStandableSlot(List<IntVec3> slots, IntVec3 cell)
        {
            IntVec3 slot = cell.Standable(map) ? cell
                : CellFinder.StandableCellNear(cell, map, 3f, null);
            if (!slot.IsValid) return;
            for (int i = 0; i < slots.Count; i++)
                if (slot.InHorDistOf(slots[i], 1.4f)) return;
            slots.Add(slot);
        }

        private static List<IntVec3> ChainOrder(List<IntVec3> cells)
        {
            var remaining = new List<IntVec3>(cells);
            // Start from the cell farthest from the set centroid - an endpoint
            // for anything line-shaped, harmless for blobs.
            var centroid = Vector3.zero;
            for (int i = 0; i < remaining.Count; i++)
                centroid += remaining[i].ToVector3();
            centroid /= remaining.Count;
            int startIndex = 0;
            float startDistance = -1f;
            for (int i = 0; i < remaining.Count; i++)
            {
                float d = (remaining[i].ToVector3() - centroid).sqrMagnitude;
                if (d > startDistance) { startDistance = d; startIndex = i; }
            }
            var chain = new List<IntVec3>(remaining.Count);
            var current = remaining[startIndex];
            remaining.RemoveAt(startIndex);
            chain.Add(current);
            while (remaining.Count > 0)
            {
                int nearestIndex = 0;
                float nearestDistance = float.MaxValue;
                for (int i = 0; i < remaining.Count; i++)
                {
                    float d = current.DistanceTo(remaining[i]);
                    if (d < nearestDistance)
                    { nearestDistance = d; nearestIndex = i; }
                }
                current = remaining[nearestIndex];
                remaining.RemoveAt(nearestIndex);
                chain.Add(current);
            }
            return chain;
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            if (renderDirty) RebuildRenderCache();
            for (int i = 0; i < 5; i++)
            {
                var chunks = edgeChunks[i];
                if (chunks.Count == 0) continue;
                var material = edgeMaterials[i];
                if (material == null) continue;
                for (int c = 0; c < chunks.Count; c++)
                    Graphics.DrawMeshInstanced(MeshPool.plane10, 0, material,
                        chunks[c]);
            }
        }

        private static Color ColorOf(CAOverlayKind kind)
        {
            switch (kind)
            {
                case CAOverlayKind.Objective: return ObjectiveColor;
                case CAOverlayKind.PrimaryObjective: return PrimaryColor;
                case CAOverlayKind.DefensiveLine: return DefensiveColor;
                case CAOverlayKind.FallbackLine: return FallbackColor;
                default: return OffensiveColor;
            }
        }

        private void RebuildRenderCache()
        {
            renderDirty = false;
            Material edgeSource = MatLoader.LoadMat("Misc/FieldEdge");
            int maxX = map.Size.x;
            int maxZ = map.Size.z;
            for (int layer = 0; layer < 5; layer++)
            {
                var chunks = edgeChunks[layer];
                chunks.Clear();
                var cells = CellsOf((CAOverlayKind)layer);
                if (cells.Count == 0) { edgeMaterials[layer] = null; continue; }
                Color color = ColorOf((CAOverlayKind)layer);
                if (edgeMaterials[layer] == null)
                {
                    Material material = MaterialPool.MatFrom(
                        (Texture2D)edgeSource.mainTexture,
                        ShaderDatabase.Transparent, color, 2900);
                    material.mainTexture.wrapMode = TextureWrapMode.Clamp;
                    material.enableInstancing = true;
                    edgeMaterials[layer] = material;
                }
                float y = Rand.ValueSeeded(
                    color.ToOpaque().GetHashCode()) * 0.03658537f / 10f;
                var set = cellSets[layer];
                List<Matrix4x4> current = null;
                for (int i = 0; i < cells.Count; i++)
                {
                    IntVec3 cell = cells[i];
                    if (!cell.InBounds(map)) continue;
                    for (int rot = 0; rot < 4; rot++)
                    {
                        bool open;
                        switch (rot)
                        {
                            case 0:
                                open = cell.z < maxZ - 1
                                    && !set.Contains(cell + IntVec3.North);
                                break;
                            case 1:
                                open = cell.x < maxX - 1
                                    && !set.Contains(cell + IntVec3.East);
                                break;
                            case 2:
                                open = cell.z > 0
                                    && !set.Contains(cell + IntVec3.South);
                                break;
                            default:
                                open = cell.x > 0
                                    && !set.Contains(cell + IntVec3.West);
                                break;
                        }
                        if (!open) continue;
                        if (current == null || current.Count >= InstanceCap)
                        {
                            current = new List<Matrix4x4>();
                            chunks.Add(current);
                        }
                        current.Add(Matrix4x4.TRS(
                            cell.ToVector3ShiftedWithAltitude(
                                AltitudeLayer.MetaOverlays)
                                + new Vector3(0f, y, 0f),
                            new Rot4(rot).AsQuat, Vector3.one));
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref objectiveCells, "caOverlayObjective",
                LookMode.Value);
            Scribe_Collections.Look(ref primaryObjectiveCells,
                "caOverlayPrimaryObjective", LookMode.Value);
            Scribe_Collections.Look(ref defensiveLineCells,
                "caOverlayDefensiveLine", LookMode.Value);
            Scribe_Collections.Look(ref fallbackLineCells,
                "caOverlayFallbackLine", LookMode.Value);
            Scribe_Collections.Look(ref offensiveLineCells,
                "caOverlayOffensiveLine", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (objectiveCells == null) objectiveCells = new List<IntVec3>();
                if (primaryObjectiveCells == null)
                    primaryObjectiveCells = new List<IntVec3>();
                if (defensiveLineCells == null)
                    defensiveLineCells = new List<IntVec3>();
                if (fallbackLineCells == null)
                    fallbackLineCells = new List<IntVec3>();
                if (offensiveLineCells == null)
                    offensiveLineCells = new List<IntVec3>();
                for (int i = 0; i < 5; i++)
                {
                    cellSets[i].Clear();
                    var cells = CellsOf((CAOverlayKind)i);
                    // Dedupe defensively on load; the sets are the fast truth.
                    for (int j = cells.Count - 1; j >= 0; j--)
                        if (!cellSets[i].Add(cells[j])) cells.RemoveAt(j);
                }
                renderDirty = true;
            }
        }
    }

    // Painting designators. All layers share the native Default2D draw-style
    // family - the game's own style picker offers precise Line/AngledLine,
    // the regular drag rectangle, and oval radius shapes; the drawn cells
    // land exactly where the chosen style says. One consistent hand for
    // every layer.
    public abstract class Designator_CAOverlayPaint : Designator_Cells
    {
        private static DrawStyleCategoryDef cachedStyleCategory;

        protected abstract CAOverlayKind Kind { get; }

        // The tool wears its layer's color.
        public override Color IconDrawColor
        {
            get
            {
                switch (Kind)
                {
                    case CAOverlayKind.Objective:
                        return CAOverlayMapComponent.ObjectiveColor;
                    case CAOverlayKind.PrimaryObjective:
                        return CAOverlayMapComponent.PrimaryColor;
                    case CAOverlayKind.DefensiveLine:
                        return CAOverlayMapComponent.DefensiveColor;
                    case CAOverlayKind.FallbackLine:
                        return CAOverlayMapComponent.FallbackColor;
                    default:
                        return CAOverlayMapComponent.OffensiveColor;
                }
            }
        }

        protected Designator_CAOverlayPaint()
        {
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_PlanAdd;
            useMouseIcon = true;
        }

        public override DrawStyleCategoryDef DrawStyleCategory
        {
            get
            {
                if (cachedStyleCategory == null)
                    cachedStyleCategory =
                        DefDatabase<DrawStyleCategoryDef>.GetNamedSilentFail(
                            "Default2D") ?? DrawStyleCategoryDefOf.Plans;
                return cachedStyleCategory;
            }
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map)) return false;
            var overlay = CAOverlayMapComponent.For(Map);
            if (overlay == null) return false;
            if (overlay.Contains(Kind, c)) return false;
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            CAOverlayMapComponent.For(Map)?.Paint(Kind, c);
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
        }
    }


    public class Designator_CAOverlayObjective : Designator_CAOverlayPaint
    {
        protected override CAOverlayKind Kind => CAOverlayKind.Objective;
        public Designator_CAOverlayObjective()
        {
            defaultLabel = "Objective";
            defaultDesc = "Paint an objective. Autonomous defense weighs known "
                + "threats near objectives and anchors on them.";
            icon = TexCommand.Attack;
        }
    }

    public class Designator_CAOverlayPrimaryObjective : Designator_CAOverlayPaint
    {
        protected override CAOverlayKind Kind => CAOverlayKind.PrimaryObjective;
        public Designator_CAOverlayPrimaryObjective()
        {
            defaultLabel = "Primary objective";
            defaultDesc = "Paint the primary objective. Outranks ordinary "
                + "objectives when defense is assigned.";
            icon = TexCommand.SquadAttack;
        }
    }

    public class Designator_CAOverlayDefensiveLine : Designator_CAOverlayPaint
    {
        protected override CAOverlayKind Kind => CAOverlayKind.DefensiveLine;
        public Designator_CAOverlayDefensiveLine()
        {
            defaultLabel = "Defensive line";
            defaultDesc = "Paint the defensive line. Squads deploy to hold "
                + "slots along it; threat-aware idle Autonomous fighters man "
                + "it on their own.";
            icon = TexCommand.HoldOpen;
        }
    }

    public class Designator_CAOverlayFallbackLine : Designator_CAOverlayPaint
    {
        protected override CAOverlayKind Kind => CAOverlayKind.FallbackLine;
        public Designator_CAOverlayFallbackLine()
        {
            defaultLabel = "Fallback line";
            defaultDesc = "Paint the fallback line. Holders who break off "
                + "withdraw toward it instead of an improvised rear point.";
            icon = TexCommand.ForbidOff;
        }
    }

    public class Designator_CAOverlayOffensiveLine : Designator_CAOverlayPaint
    {
        protected override CAOverlayKind Kind => CAOverlayKind.OffensiveLine;
        public Designator_CAOverlayOffensiveLine()
        {
            defaultLabel = "Offensive line";
            defaultDesc = "Paint the offensive line - jump-off positions. "
                + "Squads advance to slots along it on order.";
            icon = TexCommand.FireAtWill;
        }
    }

    public class Designator_CAOverlayErase : Designator_Cells
    {
        public Designator_CAOverlayErase()
        {
            defaultLabel = "Erase command paint";
            defaultDesc = "Erase painted command layers from dragged cells.";
            icon = TexCommand.ClearPrioritizedWork;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_PlanRemove;
            useMouseIcon = true;
        }

        private static DrawStyleCategoryDef cachedEraseCategory;

        public override DrawStyleCategoryDef DrawStyleCategory
        {
            get
            {
                if (cachedEraseCategory == null)
                    cachedEraseCategory =
                        DefDatabase<DrawStyleCategoryDef>.GetNamedSilentFail(
                            "Default2D") ?? DrawStyleCategoryDefOf.RemovePlans;
                return cachedEraseCategory;
            }
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            var overlay = CAOverlayMapComponent.For(Map);
            return c.InBounds(Map) && overlay != null && overlay.AnyPaint(c);
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            CAOverlayMapComponent.For(Map)?.Erase(c);
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
        }
    }
}
