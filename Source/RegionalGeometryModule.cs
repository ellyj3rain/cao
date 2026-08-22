using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    internal readonly struct CARegionalEnvelopeEdge
    {
        internal readonly Vector2 from;
        internal readonly Vector2 to;

        internal CARegionalEnvelopeEdge(Vector2 from, Vector2 to)
        {
            this.from = from;
            this.to = to;
        }
    }

    internal sealed class CARegionalEnvelopeGeometry
    {
        internal Vector3 origin;
        internal Vector3 normal;
        internal Vector3 axisX;
        internal Vector3 axisY;
        internal float radius;
        internal float referenceTileSize;
        internal List<Vector2> polygon = new List<Vector2>();
        internal List<List<Vector2>> loops = new List<List<Vector2>>();
        internal List<CARegionalEnvelopeEdge> internalEdges =
            new List<CARegionalEnvelopeEdge>();
        internal bool usedConvexFallback;

        internal Vector2 Project(Vector3 point)
        {
            Vector3 offset = point - origin;
            return new Vector2(Vector3.Dot(offset, axisX),
                Vector3.Dot(offset, axisY));
        }

        internal Vector3 WorldPoint(Vector2 point, float altitude)
        {
            Vector3 tangent = origin + axisX * point.x + axisY * point.y;
            return tangent.normalized * (radius + altitude);
        }
    }

    internal static class CARegionalGeometry
    {
        private const float GeometryEpsilon = 0.0001f;
        internal static bool IsBlocked(PlanetTile tile)
        {
            if (!tile.Valid) return true;
            Tile info = tile.Tile;
            return info?.PrimaryBiome == null
                || info.PrimaryBiome.isWaterBiome
                || info.PrimaryBiome.impassable
                || Verse.Find.World.Impassable(tile);
        }

        internal static CARegionalEnvelopeGeometry BuildEnvelope(
            CARegionalPlan plan, bool smooth = true)
        {
            var geometry = new CARegionalEnvelopeGeometry();
            List<PlanetTile> members = plan?.memberTileIds
                ?.Select(CARegionalPlanUtility.SurfaceTile)
                .Where(tile => tile.Valid).ToList()
                ?? new List<PlanetTile>();
            if (members.Count == 0) return geometry;

            PlanetTile root = plan.BundleRoot.Valid ? plan.BundleRoot
                : members[0];
            Vector3 rootCenter = Verse.Find.WorldGrid.GetTileCenter(root);
            geometry.radius = rootCenter.magnitude;
            geometry.referenceTileSize = root.Layer.GetTileSize(root);
            geometry.normal = rootCenter.sqrMagnitude > GeometryEpsilon
                ? rootCenter.normalized : Vector3.up;
            geometry.origin = geometry.normal * geometry.radius;
            geometry.axisX = Vector3.Cross(Vector3.up, geometry.normal);
            if (geometry.axisX.sqrMagnitude < GeometryEpsilon)
                geometry.axisX = Vector3.Cross(Vector3.right,
                    geometry.normal);
            geometry.axisX.Normalize();
            geometry.axisY = Vector3.Cross(geometry.normal,
                geometry.axisX).normalized;

            geometry.loops = UnionBoundaries(geometry, members);
            geometry.polygon = geometry.loops.OrderByDescending(loop =>
                    Mathf.Abs(PolygonArea(loop))).FirstOrDefault()
                ?? new List<Vector2>();
            if (geometry.polygon.Count < 3)
            {
                geometry.usedConvexFallback = true;
                var points = new List<Vector2>();
                foreach (PlanetTile tile in members)
                {
                    var tileVertices = new List<Vector3>();
                    Verse.Find.WorldGrid.GetTileVertices(tile, tileVertices);
                    for (int i = 0; i < tileVertices.Count; i++)
                        points.Add(geometry.Project(tileVertices[i]));
                }
                geometry.polygon = ConvexHull(points);
                geometry.loops = geometry.polygon.Count >= 3
                    ? new List<List<Vector2>> { geometry.polygon }
                    : new List<List<Vector2>>();
            }
            if (smooth && geometry.polygon.Count >= 3)
            {
                geometry.loops = geometry.loops.Select(SimplifyCollinear)
                    .Where(loop => loop.Count >= 3).ToList();
                geometry.polygon = geometry.loops.OrderByDescending(loop =>
                        Mathf.Abs(PolygonArea(loop))).FirstOrDefault()
                    ?? geometry.polygon;
            }
            return geometry;
        }

        internal static List<int> ClaimedTiles(CARegionalPlan plan)
        {
            return (plan?.memberTileIds ?? new List<int>()).Distinct()
                .OrderBy(id => id).ToList();
        }

        private sealed class BoundaryEdge
        {
            internal VertexKey from;
            internal VertexKey to;
        }

        private readonly struct VertexKey : IEquatable<VertexKey>,
            IComparable<VertexKey>
        {
            private const float Quantization = 100000f;
            internal readonly int x;
            internal readonly int y;
            internal readonly int z;

            internal VertexKey(Vector3 point)
            {
                x = Mathf.RoundToInt(point.x * Quantization);
                y = Mathf.RoundToInt(point.y * Quantization);
                z = Mathf.RoundToInt(point.z * Quantization);
            }

            public bool Equals(VertexKey other)
            {
                return x == other.x && y == other.y && z == other.z;
            }

            public override bool Equals(object value)
            {
                return value is VertexKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked { return ((x * 397) ^ y) * 397 ^ z; }
            }

            public int CompareTo(VertexKey other)
            {
                int result = x.CompareTo(other.x);
                if (result != 0) return result;
                result = y.CompareTo(other.y);
                return result != 0 ? result : z.CompareTo(other.z);
            }
        }

        private readonly struct UndirectedEdgeKey :
            IEquatable<UndirectedEdgeKey>
        {
            private readonly VertexKey first;
            private readonly VertexKey second;

            internal UndirectedEdgeKey(VertexKey left, VertexKey right)
            {
                if (left.CompareTo(right) <= 0)
                {
                    first = left;
                    second = right;
                }
                else
                {
                    first = right;
                    second = left;
                }
            }

            public bool Equals(UndirectedEdgeKey other)
            {
                return first.Equals(other.first)
                    && second.Equals(other.second);
            }

            public override bool Equals(object value)
            {
                return value is UndirectedEdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked { return first.GetHashCode() * 397
                    ^ second.GetHashCode(); }
            }
        }

        private static List<List<Vector2>> UnionBoundaries(
            CARegionalEnvelopeGeometry geometry, List<PlanetTile> members)
        {
            var positions = new Dictionary<VertexKey, Vector3>();
            var edges = new Dictionary<UndirectedEdgeKey, BoundaryEdge>();
            foreach (PlanetTile tile in members)
            {
                var vertices = new List<Vector3>();
                Verse.Find.WorldGrid.GetTileVertices(tile, vertices);
                if (vertices.Count < 3) continue;
                List<VertexKey> keys = vertices.Select(point =>
                    new VertexKey(point)).ToList();
                for (int i = 0; i < keys.Count; i++)
                    positions[keys[i]] = vertices[i];
                var projected = vertices.Select(geometry.Project).ToList();
                bool reverse = PolygonArea(projected) < 0f;
                for (int i = 0; i < keys.Count; i++)
                {
                    int next = (i + 1) % keys.Count;
                    VertexKey from = reverse ? keys[next] : keys[i];
                    VertexKey to = reverse ? keys[i] : keys[next];
                    var key = new UndirectedEdgeKey(from, to);
                    if (edges.TryGetValue(key, out BoundaryEdge shared))
                    {
                        edges.Remove(key);
                        geometry.internalEdges.Add(
                            new CARegionalEnvelopeEdge(
                                geometry.Project(positions[shared.from]),
                                geometry.Project(positions[shared.to])));
                    }
                    else edges[key] = new BoundaryEdge { from = from, to = to };
                }
            }
            if (edges.Count < 3) return new List<List<Vector2>>();

            var remaining = edges.Values.ToList();
            var loops = new List<List<Vector2>>();
            while (remaining.Count > 0)
            {
                BoundaryEdge first = remaining.OrderBy(edge => edge.from)
                    .ThenBy(edge => edge.to).First();
                remaining.Remove(first);
                var vertexIds = new List<VertexKey> { first.from, first.to };
                VertexKey current = first.to;
                int guard = edges.Count + 2;
                while (!current.Equals(first.from) && guard-- > 0)
                {
                    BoundaryEdge next = remaining.FirstOrDefault(edge =>
                        edge.from.Equals(current));
                    if (next == null)
                    {
                        next = remaining.FirstOrDefault(edge =>
                            edge.to.Equals(current));
                        if (next != null)
                        {
                            VertexKey swap = next.from;
                            next.from = next.to;
                            next.to = swap;
                        }
                    }
                    if (next == null) break;
                    remaining.Remove(next);
                    current = next.to;
                    if (!current.Equals(first.from)) vertexIds.Add(current);
                }
                if (!current.Equals(first.from) || vertexIds.Count < 3)
                    continue;
                List<Vector2> loop = vertexIds.Where(positions.ContainsKey)
                    .Select(id => geometry.Project(positions[id])).ToList();
                if (loop.Count >= 3) loops.Add(loop);
            }
            return loops.OrderByDescending(loop => Mathf.Abs(
                PolygonArea(loop))).ToList();
        }

        internal static float PolygonArea(List<Vector2> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 current = polygon[i];
                Vector2 next = polygon[(i + 1) % polygon.Count];
                area += current.x * next.y - next.x * current.y;
            }
            return area * 0.5f;
        }

        private static List<Vector2> ConvexHull(List<Vector2> points)
        {
            List<Vector2> sorted = points.Distinct(new Vector2Comparer())
                .OrderBy(point => point.x).ThenBy(point => point.y).ToList();
            if (sorted.Count <= 2) return sorted;
            var lower = new List<Vector2>();
            foreach (Vector2 point in sorted)
            {
                while (lower.Count >= 2 && Cross(
                    lower[lower.Count - 1] - lower[lower.Count - 2],
                    point - lower[lower.Count - 1]) <= 0f)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(point);
            }
            var upper = new List<Vector2>();
            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                Vector2 point = sorted[i];
                while (upper.Count >= 2 && Cross(
                    upper[upper.Count - 1] - upper[upper.Count - 2],
                    point - upper[upper.Count - 1]) <= 0f)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(point);
            }
            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }

        private static List<Vector2> SimplifyCollinear(List<Vector2> polygon)
        {
            var result = new List<Vector2>(polygon.Count);
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 previous = polygon[(i - 1 + polygon.Count)
                    % polygon.Count];
                Vector2 current = polygon[i];
                Vector2 next = polygon[(i + 1) % polygon.Count];
                Vector2 incoming = current - previous;
                Vector2 outgoing = next - current;
                if (incoming.sqrMagnitude <= GeometryEpsilon
                    || outgoing.sqrMagnitude <= GeometryEpsilon) continue;
                if (Mathf.Abs(Cross(incoming.normalized,
                        outgoing.normalized)) <= GeometryEpsilon
                    && Vector2.Dot(incoming, outgoing) > 0f) continue;
                result.Add(current);
            }
            return result;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private sealed class Vector2Comparer : IEqualityComparer<Vector2>
        {
            public bool Equals(Vector2 left, Vector2 right)
            {
                return (left - right).sqrMagnitude < 0.000001f;
            }

            public int GetHashCode(Vector2 value)
            {
                unchecked
                {
                    int x = Mathf.RoundToInt(value.x * 10000f);
                    int y = Mathf.RoundToInt(value.y * 10000f);
                    return (x * 397) ^ y;
                }
            }
        }
    }

    [StaticConstructorOnStartup]
    public sealed class WorldDrawLayer_CARegionalFootprint : WorldDrawLayer
    {
        private const float FillAltitude = 0.025f;
        private const float BorderAltitude = 0.055f;

        private static readonly Material FootprintFill = MaterialPool.MatFrom(
            BaseContent.WhiteTex, ShaderDatabase.WorldOverlayTransparent,
            new Color(0.10f, 0.54f, 0.68f, 0.12f), 3560);
        private static readonly Material SelectedFootprintFill =
            MaterialPool.MatFrom(BaseContent.WhiteTex,
                ShaderDatabase.WorldOverlayTransparent,
                new Color(0.12f, 0.68f, 0.82f, 0.28f), 3561);
        private static readonly Material FootprintBorder = MaterialPool.MatFrom(
            BaseContent.WhiteTex, ShaderDatabase.WorldOverlayTransparent,
            new Color(0.46f, 0.90f, 1f, 0.62f), 3590);
        private static readonly Material SelectedFootprintBorder =
            MaterialPool.MatFrom(BaseContent.WhiteTex,
                ShaderDatabase.WorldOverlayTransparent,
                new Color(0.66f, 0.96f, 1f, 0.96f), 3591);
        // The arrival area is ARRIVAL-OWNED visual state on the world map:
        // a warm entry-point tint distinct from the cool membership fill.
        // It lives on this layer - not on vanilla tile selection - so it
        // faithfully follows TrySetArrival through a cheap mesh regenerate
        // and never through any geographic regeneration.
        private static readonly Material ArrivalFill = MaterialPool.MatFrom(
            BaseContent.WhiteTex, ShaderDatabase.WorldOverlayTransparent,
            new Color(0.93f, 0.86f, 0.62f, 0.30f), 3592);
        private static readonly Material ArrivalBorder = MaterialPool.MatFrom(
            BaseContent.WhiteTex, ShaderDatabase.WorldOverlayTransparent,
            new Color(0.98f, 0.93f, 0.74f, 0.92f), 3593);

        private string lastKey;

        public override bool VisibleWhenLayerNotSelected => false;
        public override bool VisibleInBackground => false;
        public override bool Visible => base.Visible
            && VisiblePlans().Any();

        public override bool ShouldRegenerate => base.ShouldRegenerate
            || CurrentKey() != lastKey;

        public override IEnumerable Regenerate()
        {
            foreach (object item in base.Regenerate()) yield return item;
            lastKey = CurrentKey();
            if (planetLayer != Verse.Find.WorldGrid.Surface) yield break;

            bool drewAny = false;
            foreach (CARegionalPlan plan in VisiblePlans())
            {
                CARegionalEnvelopeGeometry geometry =
                    CARegionalGeometry.BuildEnvelope(plan, true);
                if (geometry.polygon.Count < 3) continue;
                drewAny = true;

                bool selected = IsSelected(plan);
                LayerSubMesh fill = GetSubMesh(selected
                    ? SelectedFootprintFill : FootprintFill);
                foreach (PlanetTile tile in plan.memberTileIds.Select(
                             CARegionalPlanUtility.SurfaceTile)
                         .Where(tile => tile.Valid))
                    AddTileFill(fill, geometry, tile);

                // The saved plan is one geographic object. Constituent seams
                // are deliberately absent from the primary world-map shape;
                // technical source geometry remains available in the editor.
                LayerSubMesh border = GetSubMesh(selected
                    ? SelectedFootprintBorder : FootprintBorder);
                foreach (List<Vector2> loop in geometry.loops)
                    AddBorderRing(border, geometry, loop);

                // The entry point, drawn on its actual area: warm fill and
                // a rim around the arrival tile's own hex.
                PlanetTile arrival = CARegionalPlanUtility.SurfaceTile(
                    plan.startTileId);
                if (arrival.Valid
                    && plan.memberTileIds.Contains(plan.startTileId))
                {
                    AddTileFill(GetSubMesh(ArrivalFill), geometry, arrival);
                    AddTileRim(GetSubMesh(ArrivalBorder), geometry,
                        arrival);
                }
            }
            if (drewAny) FinalizeMesh(MeshParts.All);
        }

        private static void AddTileRim(LayerSubMesh mesh,
            CARegionalEnvelopeGeometry geometry, PlanetTile tile)
        {
            var vertices = new List<Vector3>();
            Verse.Find.WorldGrid.GetTileVertices(tile, vertices);
            if (vertices.Count < 3) return;
            float width = Mathf.Max(0.00010f,
                geometry.referenceTileSize * 0.010f);
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 from = geometry.Project(vertices[i]);
                Vector2 to = geometry.Project(
                    vertices[(i + 1) % vertices.Count]);
                AddEdgeStrip(mesh, geometry, from, to, width,
                    BorderAltitude);
            }
        }

        private static void AddTileFill(LayerSubMesh mesh,
            CARegionalEnvelopeGeometry geometry, PlanetTile tile)
        {
            var vertices = new List<Vector3>();
            Verse.Find.WorldGrid.GetTileVertices(tile, vertices);
            if (vertices.Count < 3) return;
            var planar = new List<Vector3>(vertices.Count);
            foreach (Vector3 vertex in vertices)
            {
                Vector2 point = geometry.Project(vertex);
                planar.Add(new Vector3(point.x, 0f, point.y));
            }
            List<int> triangles = new Triangulator(planar).Triangulate();
            if (triangles.Count != (vertices.Count - 2) * 3) return;
            int first = mesh.verts.Count;
            foreach (Vector3 vertex in vertices)
            {
                mesh.verts.Add(vertex.normalized
                    * (geometry.radius + FillAltitude));
                // Every selected tile samples the same point on the solid
                // texture, so their shared edges read as one continuous fill.
                mesh.uvs.Add(new Vector2(0.5f, 0.5f));
            }
            foreach (int index in triangles) mesh.tris.Add(first + index);
        }

        private static void AddEdgeStrip(LayerSubMesh mesh,
            CARegionalEnvelopeGeometry geometry, Vector2 from, Vector2 to,
            float width, float altitude)
        {
            Vector2 direction = to - from;
            if (direction.sqrMagnitude < 0.000001f) return;
            direction.Normalize();
            Vector2 offset = new Vector2(-direction.y, direction.x)
                * (width * 0.5f);
            int first = mesh.verts.Count;
            mesh.verts.Add(geometry.WorldPoint(from + offset, altitude));
            mesh.verts.Add(geometry.WorldPoint(from - offset, altitude));
            mesh.verts.Add(geometry.WorldPoint(to - offset, altitude));
            mesh.verts.Add(geometry.WorldPoint(to + offset, altitude));
            for (int i = 0; i < 4; i++)
                mesh.uvs.Add(new Vector2(0.5f, 0.5f));
            mesh.tris.Add(first);
            mesh.tris.Add(first + 2);
            mesh.tris.Add(first + 1);
            mesh.tris.Add(first);
            mesh.tris.Add(first + 3);
            mesh.tris.Add(first + 2);
        }

        private static void AddBorderRing(LayerSubMesh mesh,
            CARegionalEnvelopeGeometry geometry, List<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3) return;
            float width = Mathf.Max(0.00012f,
                geometry.referenceTileSize * 0.012f);
            float halfWidth = width * 0.5f;
            float winding = CARegionalGeometry.PolygonArea(polygon) >= 0f
                ? 1f : -1f;

            int first = mesh.verts.Count;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 previous = polygon[(i - 1 + polygon.Count)
                    % polygon.Count];
                Vector2 current = polygon[i];
                Vector2 next = polygon[(i + 1) % polygon.Count];
                Vector2 previousDirection = (current - previous).normalized;
                Vector2 nextDirection = (next - current).normalized;
                Vector2 previousNormal = new Vector2(
                    -previousDirection.y, previousDirection.x) * winding;
                Vector2 nextNormal = new Vector2(-nextDirection.y,
                    nextDirection.x) * winding;
                Vector2 miter = previousNormal + nextNormal;
                if (miter.sqrMagnitude < 0.0001f) miter = nextNormal;
                miter.Normalize();
                float denominator = Mathf.Max(0.25f,
                    Mathf.Abs(Vector2.Dot(miter, nextNormal)));
                float offset = Mathf.Min(width, halfWidth / denominator);
                Vector2 outer = current - miter * offset;
                Vector2 inner = current + miter * offset;
                mesh.verts.Add(geometry.WorldPoint(outer, BorderAltitude));
                mesh.verts.Add(geometry.WorldPoint(inner, BorderAltitude));
                mesh.uvs.Add(new Vector2(0.5f, 0.5f));
                mesh.uvs.Add(new Vector2(0.5f, 0.5f));
            }

            for (int i = 0; i < polygon.Count; i++)
            {
                int next = (i + 1) % polygon.Count;
                int outer = first + i * 2;
                int inner = outer + 1;
                int nextOuter = first + next * 2;
                int nextInner = nextOuter + 1;
                if (winding > 0f)
                {
                    mesh.tris.Add(outer);
                    mesh.tris.Add(nextInner);
                    mesh.tris.Add(nextOuter);
                    mesh.tris.Add(outer);
                    mesh.tris.Add(inner);
                    mesh.tris.Add(nextInner);
                }
                else
                {
                    mesh.tris.Add(outer);
                    mesh.tris.Add(nextOuter);
                    mesh.tris.Add(nextInner);
                    mesh.tris.Add(outer);
                    mesh.tris.Add(nextInner);
                    mesh.tris.Add(inner);
                }
            }
        }

        private static string CurrentKey()
        {
            return (Verse.Find.World?.info?.Seed ?? 0) + ":"
                + (Verse.Find.WorldSelector?.SelectedTile.tileId ?? -1) + ":"
                + string.Join("|", VisiblePlans().Select(plan =>
                    (plan.regionalId ?? "unknown") + ":"
                    + plan.startTileId + ":"
                    + string.Join(",", plan.memberTileIds
                        ?? new List<int>())));
        }

        private static IEnumerable<CARegionalPlan> VisiblePlans()
        {
            var seen = new HashSet<string>();
            CARegionalPlan pending =
                CARegionalSetupSession.PendingForCurrentWorld;
            if (pending != null)
            {
                seen.Add(PlanKey(pending));
                yield return pending;
            }

            IReadOnlyList<CARegionalPlan> regions =
                CARegionalWorldComponent.Current?.Regions;
            if (regions == null) yield break;
            foreach (CARegionalPlan region in regions.Where(item =>
                         item != null && item.memberTileIds != null
                         && item.memberTileIds.Count > 0)
                     .OrderBy(item => item.regionalId)
                     .ThenBy(item => item.startTileId))
            {
                if (seen.Add(PlanKey(region))) yield return region;
            }
        }

        private static string PlanKey(CARegionalPlan plan)
        {
            // No startTileId. regionalId already encodes the footprint anchor,
            // size, extent and rotation; including the LANDING choice made a
            // landing change invalidate cached geometry that does not depend
            // on it, forcing a needless recompute.
            return (plan.regionalId ?? "unknown") + ":" + plan.mapSize;
        }

        private static bool IsSelected(CARegionalPlan plan)
        {
            PlanetTile tile = Verse.Find.WorldSelector?.SelectedTile
                ?? PlanetTile.Invalid;
            return tile.Valid && plan?.ReservedTileIds != null
                && plan.ReservedTileIds.Contains(tile.tileId);
        }
    }

    internal static class CARegionalGeography
    {
        internal static CARegionalPlan RegionAt(PlanetTile tile)
        {
            if (!tile.Valid) return null;
            CARegionalPlan saved = CARegionalWorldComponent.Current
                ?.FindRegionContaining(tile);
            if (saved != null) return saved;
            CARegionalPlan pending =
                CARegionalSetupSession.PendingForCurrentWorld;
            return pending?.ReservedTileIds != null
                    && pending.ReservedTileIds.Contains(tile.tileId)
                ? pending : null;
        }
    }

    [HarmonyPatch(typeof(WorldInspectPane), "TileInspectString",
        MethodType.Getter)]
    internal static class CARegionalWorldInspectStringPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref string __result)
        {
            PlanetTile tile = Verse.Find.WorldSelector?.SelectedTile
                ?? PlanetTile.Invalid;
            CARegionalPlan region = CARegionalGeography.RegionAt(tile);
            if (region == null) return;
            string prefix = "Geographic region: "
                + CARegionalPlanUtility.RegionName(region)
                + "\n" + region.RegionTileCount + " connected areas - "
                + region.factions.Count + " faction"
                + (region.factions.Count == 1 ? "" : "s")
                + " - " + region.settlements.Count + " settlement"
                + (region.settlements.Count == 1 ? "" : "s");
            __result = prefix + "\n\nSelected land\n" + __result;
        }
    }

    [HarmonyPatch(typeof(WorldInspectPane), nameof(WorldInspectPane.GetLabel))]
    internal static class CARegionalWorldInspectLabelPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref string __result)
        {
            if (Verse.Find.WorldSelector?.NumSelectedObjects > 0) return;
            PlanetTile tile = Verse.Find.WorldSelector?.SelectedTile
                ?? PlanetTile.Invalid;
            CARegionalPlan region = CARegionalGeography.RegionAt(tile);
            if (region != null)
                __result = CARegionalPlanUtility.RegionName(region)
                    + " - " + __result;
        }
    }
}
