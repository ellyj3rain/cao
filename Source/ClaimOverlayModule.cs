using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Claims made visible: every materialized settlement's core ground is
    // drawn as a stance-colored border on the map - ally green, neutral
    // white, hostile red. Land politics needs the land to show who holds
    // it. Cached instanced rendering (the DrawFieldEdges full-map-clear
    // trap does not apply here; same discipline as the arrangement
    // renderer).
    public sealed class CAClaimOverlayMapComponent : MapComponent
    {
        private const int InstanceCap = 1023;
        private readonly List<KeyValuePair<Color, List<List<Matrix4x4>>>>
            borders = new List<KeyValuePair<Color, List<List<Matrix4x4>>>>();
        private readonly Dictionary<int, Material> materials =
            new Dictionary<int, Material>();
        private int rebuildTick = -99999;

        public CAClaimOverlayMapComponent(Map map) : base(map) { }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            if (WorldRendererUtility.WorldSelected) return;
            int now = Find.TickManager.TicksGame;
            if (now - rebuildTick > 2500)
            {
                rebuildTick = now;
                Rebuild();
            }
            for (int i = 0; i < borders.Count; i++)
            {
                Material mat = MaterialOf(borders[i].Key);
                List<List<Matrix4x4>> chunks = borders[i].Value;
                for (int c = 0; c < chunks.Count; c++)
                    Graphics.DrawMeshInstanced(MeshPool.plane10, 0, mat,
                        chunks[c]);
            }
        }

        private Material MaterialOf(Color color)
        {
            int key = color.ToOpaque().GetHashCode();
            Material mat;
            if (materials.TryGetValue(key, out mat)) return mat;
            mat = MaterialPool.MatFrom("UI/Overlays/TargetHighlight_Side",
                ShaderDatabase.Transparent, color);
            // DrawMeshInstanced rejects an otherwise valid material unless
            // instancing is explicitly enabled. The other CA instanced
            // overlays already establish this material contract; claims must
            // do the same before their first rendered frame.
            mat.enableInstancing = true;
            materials[key] = mat;
            return mat;
        }

        private void Rebuild()
        {
            borders.Clear();
            CARegionalWorldComponent regional =
                CARegionalWorldComponent.Current;
            if (regional == null) return;
            IReadOnlyList<CARegionalSettlementRecord> records =
                regional.Records;
            for (int i = 0; i < records.Count; i++)
            {
                CARegionalSettlementRecord record = records[i];
                if (record.lastMapId != map.uniqueID
                    || record.localRect == CellRect.Empty) continue;
                Color color = StanceColor(record);
                var chunks = new List<List<Matrix4x4>>();
                var current = new List<Matrix4x4>();
                float alt = AltitudeLayer.MetaOverlays.AltitudeFor()
                    + Rand.ValueSeeded(record.regionalId != null
                        ? record.regionalId.GetHashCode() : i)
                    * 0.03658537f / 10f;
                foreach (IntVec3 cell in record.localRect.EdgeCells)
                {
                    if (!cell.InBounds(map)) continue;
                    Vector3 pos = cell.ToVector3Shifted();
                    pos.y = alt;
                    current.Add(Matrix4x4.TRS(pos,
                        Quaternion.identity, new Vector3(1f, 1f, 1f)));
                    if (current.Count >= InstanceCap)
                    {
                        chunks.Add(current);
                        current = new List<Matrix4x4>();
                    }
                }
                if (current.Count > 0) chunks.Add(current);
                if (chunks.Count > 0)
                    borders.Add(
                        new KeyValuePair<Color, List<List<Matrix4x4>>>(
                            color, chunks));
            }
        }

        private Color StanceColor(CARegionalSettlementRecord record)
        {
            FactionRelationKind kind = FactionRelationKind.Neutral;
            try { kind = record.faction.PlayerRelationKind; }
            catch { }
            Color c = kind == FactionRelationKind.Ally
                ? new Color(0.4f, 0.95f, 0.45f)
                : kind == FactionRelationKind.Hostile
                ? new Color(0.95f, 0.35f, 0.3f)
                : new Color(0.9f, 0.9f, 0.9f);
            c.a = 0.35f;
            return c;
        }
    }
}
