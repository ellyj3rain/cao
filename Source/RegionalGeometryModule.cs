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

        // GEOGRAPHIC BARRIER COST between two adjacent surface tiles. This
        // is the one shared measure of what separates ground: elevation
        // cliffs, relief transitions, biome ecotones, coast-to-inland
        // breaks, and temperature or rainfall transitions all raise the
        // cost; a shared road or river lowers it (a corridor, not a
        // barrier). The world partition is built from it; candidate
        // selection treats it as evidence when growing, never as a
        // boundary it must reproduce.
        internal static float GeographicBarrierCost(int fromId, int toId)
        {
            PlanetLayer surface = Verse.Find.WorldGrid?.Surface;
            if (surface == null) return 1f;
            PlanetTile fromPlanet = new PlanetTile(fromId, surface);
            PlanetTile toPlanet = new PlanetTile(toId, surface);
            Tile from = fromPlanet.Valid ? fromPlanet.Tile : null;
            Tile to = toPlanet.Valid ? toPlanet.Tile : null;
            if (from == null || to == null) return 1f;

            // Elevation difference: a 1500m cliff or ridge is a full
            // barrier; smaller differences scale linearly.
            float elevDiff = Math.Abs(from.elevation - to.elevation);
            float elevCost = Math.Min(1f, elevDiff / 1500f);

            // Hilliness transition: a two-level jump (flat to large
            // hills, small hills to mountains) is a barrier.
            int fromHill = (int)from.hilliness;
            int toHill = (int)to.hilliness;
            float hillCost = Math.Min(1f,
                Math.Abs(fromHill - toHill) * 0.35f);

            // Biome transition: a mild ecotone boundary.
            float biomeCost = from.PrimaryBiome != to.PrimaryBiome
                ? 0.2f : 0f;

            // Coastal coherence: coastal tiles prefer to join with other
            // coastal tiles, so coastlines form region edges.
            bool fromCoastal = CARegionalPlanUtility
                .ConstituentIsCoastal(fromId);
            bool toCoastal = CARegionalPlanUtility
                .ConstituentIsCoastal(toId);
            float coastCost = (fromCoastal != toCoastal) ? 0.5f : 0f;

            // River corridor: tiles that share a river link prefer to
            // join. A river is a corridor, not a barrier.
            bool shareRiver = CARegionalPlanUtility
                .ConstituentsShareRoute(fromId, toId);
            float riverAffinity = shareRiver ? -0.3f : 0f;

            // Temperature and rainfall transitions are mild
            // environmental barriers (ecotones). The rainfall term was
            // computed and then dropped when this measure was introduced
            // at B20; it now participates, matching the batch record's
            // declared behavior.
            float tempDiff = Math.Abs(from.temperature - to.temperature);
            float tempCost = Math.Min(0.5f, tempDiff / 30f);
            float rainDiff = Math.Abs(from.rainfall - to.rainfall);
            float rainCost = Math.Min(0.4f, rainDiff / 1000f);

            float baseCost = Math.Max(Math.Max(elevCost, hillCost),
                Math.Max(biomeCost, Math.Max(coastCost,
                    Math.Max(tempCost, rainCost))));
            return Math.Max(0f, baseCost + riverAffinity);
        }

        internal static CARegionalEnvelopeGeometry BuildEnvelope(
            CARegionalPlan plan, bool smooth = true)
        {
            return BuildEnvelope(plan?.memberTileIds,
                plan?.bundleRootTileId ?? -1, smooth);
        }

        // The envelope is a function of membership alone; topology records
        // and realized plans share it.
        internal static CARegionalEnvelopeGeometry BuildEnvelope(
            IReadOnlyList<int> memberTileIds, int rootTileId,
            bool smooth = true)
        {
            var geometry = new CARegionalEnvelopeGeometry();
            List<PlanetTile> members = memberTileIds
                ?.Select(CARegionalPlanUtility.SurfaceTile)
                .Where(tile => tile.Valid).ToList()
                ?? new List<PlanetTile>();
            if (members.Count == 0) return geometry;

            PlanetTile rootCandidate =
                CARegionalPlanUtility.SurfaceTile(rootTileId);
            PlanetTile root = rootCandidate.Valid ? rootCandidate
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
    // THE WHOLE PARTITION, VISIBLE. Until this layer, only the selected
    // or registered region ever drew - on a fresh world the globe looked
    // exactly like vanilla, and the authored claim "most of this land
    // lies in joined regions" had no visible evidence anywhere. This
    // layer draws every multi-area partition region's border, muted so
    // geography stays primary; regions whose ground carries settlements
    // draw a shade brighter, so held country reads against empty
    // country at a glance. Single-area regions are just tiles and stay
    // undrawn - outlining them would re-grid the map. The mesh is keyed
    // to the component's world-state revision, never to selection, so
    // it builds once and regenerates only when the world itself changes
    // (carve, settlement founding, capture).
    public sealed class WorldDrawLayer_CARegionalPartition : WorldDrawLayer
    {
        private const float BorderAltitude = 0.045f;
        private static readonly Material PartitionBorder =
            MaterialPool.MatFrom(BaseContent.WhiteTex,
                ShaderDatabase.WorldOverlayTransparent,
                new Color(0.72f, 0.78f, 0.80f, 0.22f), 3588);
        private static readonly Material HeldPartitionBorder =
            MaterialPool.MatFrom(BaseContent.WhiteTex,
                ShaderDatabase.WorldOverlayTransparent,
                new Color(0.86f, 0.83f, 0.66f, 0.40f), 3589);

        private string lastKey;
        // Visible is queried every frame, and the honest answer requires
        // knowing whether any joined region exists - a scan of every
        // partition record. Answered once per world-state revision
        // instead of once per frame.
        private static int joinedCheckRevision = -1;
        private static bool joinedCheckResult;

        public override bool VisibleWhenLayerNotSelected => false;
        public override bool VisibleInBackground => false;
        public override bool Visible => base.Visible && AnyJoinedRegion();

        private static bool AnyJoinedRegion()
        {
            CARegionalWorldComponent component =
                CARegionalWorldComponent.Current;
            if (component?.Topology == null) return false;
            if (component.WorldStateRevision == joinedCheckRevision)
                return joinedCheckResult;
            joinedCheckRevision = component.WorldStateRevision;
            joinedCheckResult = false;
            IReadOnlyList<CARegionalTopologyRecord> topology =
                component.Topology;
            for (int i = 0; i < topology.Count; i++)
                if (topology[i]?.memberTileIds != null
                    && topology[i].memberTileIds.Count >= 2)
                {
                    joinedCheckResult = true;
                    break;
                }
            return joinedCheckResult;
        }

        public override bool ShouldRegenerate => base.ShouldRegenerate
            || CurrentKey() != lastKey;

        private static string CurrentKey()
        {
            CARegionalWorldComponent component =
                CARegionalWorldComponent.Current;
            return (Verse.Find.World?.info?.Seed ?? 0) + ":"
                + (component?.WorldStateRevision ?? 0);
        }

        public override IEnumerable Regenerate()
        {
            foreach (object item in base.Regenerate()) yield return item;
            lastKey = CurrentKey();
            if (planetLayer != Verse.Find.WorldGrid.Surface) yield break;
            CARegionalWorldComponent component =
                CARegionalWorldComponent.Current;
            if (component?.Topology == null) yield break;

            // This layer walks every joined region in the world. Its
            // real cost is unmeasurable statically and matters to
            // whether the globe stays usable, so it reports itself
            // once per rebuild rather than being guessed at.
            var clock = System.Diagnostics.Stopwatch.StartNew();
            bool drewAny = false;
            int drawn = 0;
            foreach (CARegionalTopologyRecord record in component.Topology)
            {
                if (record?.memberTileIds == null
                    || record.memberTileIds.Count < 2) continue;
                // Registered ground draws on the footprint layer with the
                // full treatment; this layer carries only the rest.
                PlanetTile root = CARegionalPlanUtility.SurfaceTile(
                    record.rootTileId);
                if (root.Valid
                    && CARegionalGeography.RegionAt(root) != null)
                    continue;
                CARegionalEnvelopeGeometry geometry = CARegionalGeometry
                    .BuildEnvelope(record.memberTileIds,
                        record.rootTileId, true);
                if (geometry.polygon.Count < 3) continue;
                bool held = record.memberTileIds.Any(id =>
                    component.WorldSettlementStateAt(id) != null);
                LayerSubMesh border = GetSubMesh(held
                    ? HeldPartitionBorder : PartitionBorder);
                foreach (List<Vector2> loop in geometry.loops)
                    WorldDrawLayer_CARegionalFootprint.AddBorderRing(
                        border, geometry, loop);
                drewAny = true;
                if (++drawn % 64 == 0) yield return null;
            }
            if (drewAny) FinalizeMesh(MeshParts.All);
            clock.Stop();
            Log.Message("[CA][Regional][Partition] drew " + drawn
                + " joined region border(s) of "
                + component.Topology.Count + " partition records in "
                + clock.ElapsedMilliseconds + " ms");
        }
    }

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

        private int lastKey = -1;

        public override bool VisibleWhenLayerNotSelected => false;
        public override bool VisibleInBackground => false;
        public override bool Visible => base.Visible
            && (AnyVisiblePlan() || SelectedTopologyRecord() != null);

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

            // SELECTING ANY LAND SELECTS ITS REGION. Unrealized partition
            // regions have no plan yet; the selected one still presents as
            // one geographic object, with the selected treatment.
            CARegionalTopologyRecord selectedRecord =
                SelectedTopologyRecord();
            if (selectedRecord != null)
            {
                CARegionalEnvelopeGeometry geometry = CARegionalGeometry
                    .BuildEnvelope(selectedRecord.memberTileIds,
                        selectedRecord.rootTileId, true);
                if (geometry.polygon.Count >= 3)
                {
                    drewAny = true;
                    LayerSubMesh fill = GetSubMesh(SelectedFootprintFill);
                    foreach (PlanetTile tile in selectedRecord.memberTileIds
                                 .Select(CARegionalPlanUtility.SurfaceTile)
                             .Where(tile => tile.Valid))
                        AddTileFill(fill, geometry, tile);
                    LayerSubMesh border = GetSubMesh(
                        SelectedFootprintBorder);
                    foreach (List<Vector2> loop in geometry.loops)
                        AddBorderRing(border, geometry, loop);
                }
            }
            if (drewAny) FinalizeMesh(MeshParts.All);
        }

        // The topology region under the current selection, when no drawn
        // plan already covers that ground.
        private static CARegionalTopologyRecord SelectedTopologyRecord()
        {
            PlanetTile tile = Verse.Find.WorldSelector?.SelectedTile
                ?? PlanetTile.Invalid;
            if (!tile.Valid) return null;
            if (CARegionalGeography.RegionAt(tile) != null) return null;
            return CARegionalWorldComponent.Current?.TopologyRecordAt(tile);
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

        internal static void AddEdgeStrip(LayerSubMesh mesh,
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

        internal static void AddBorderRing(LayerSubMesh mesh,
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

        // Per-frame key without per-frame string churn: registered regions
        // carry fixed member sets from registration and are covered by the
        // world-state revision; only the in-authoring candidates (pending
        // setup plan, landing candidate) can change members freely, so only
        // they fold their exact identity here.
        private static int CurrentKey()
        {
            CARegionalWorldComponent component =
                CARegionalWorldComponent.Current;
            unchecked
            {
                int key = (Verse.Find.World?.info?.Seed ?? 0) * 397
                    ^ (Verse.Find.WorldSelector?.SelectedTile.tileId ?? -1)
                    ^ ((component?.WorldStateRevision ?? 0) * 31)
                    ^ ((component?.Regions?.Count ?? 0) * 17);
                CARegionalPlan pending =
                    CARegionalSetupSession.PendingForCurrentWorld;
                if (pending != null) key ^= PlanFingerprint(pending);
                CARegionalPlan landing = CALandingAuthoring.Candidate;
                if (landing != null) key ^= PlanFingerprint(landing) * 3;
                return key;
            }
        }

        private static int PlanFingerprint(CARegionalPlan plan)
        {
            unchecked
            {
                int fold = plan.mapSize * 23 ^ plan.startTileId
                    ^ (plan.regionalId ?? "unknown").GetHashCode();
                List<int> members = plan.memberTileIds;
                if (members != null)
                    for (int i = 0; i < members.Count; i++)
                        fold = fold * 31 + members[i];
                return fold;
            }
        }

        private static bool AnyVisiblePlan()
        {
            if (CARegionalSetupSession.PendingForCurrentWorld != null
                || CALandingAuthoring.Candidate != null)
                return true;
            IReadOnlyList<CARegionalPlan> regions =
                CARegionalWorldComponent.Current?.Regions;
            if (regions == null) return false;
            for (int i = 0; i < regions.Count; i++)
                if (regions[i]?.memberTileIds != null
                    && regions[i].memberTileIds.Count > 0)
                    return true;
            return false;
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

            // A landing candidate being authored for a gravship arrival
            // draws here too, so the geography under composition is the
            // geography on the globe.
            CARegionalPlan landing = CALandingAuthoring.Candidate;
            if (landing?.memberTileIds != null
                && landing.memberTileIds.Count > 0)
            {
                seen.Add(PlanKey(landing));
                yield return landing;
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
            // The landing candidate being authored is the active selection
            // by definition.
            if (plan != null && plan == CALandingAuthoring.Candidate)
                return true;
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

        // The partition region for land no plan governs. Realized ground
        // answers through RegionAt first; this is the membership truth for
        // everything else.
        internal static CARegionalTopologyRecord TopologyAt(PlanetTile tile)
        {
            if (!tile.Valid) return null;
            return CARegionalWorldComponent.Current?.TopologyRecordAt(tile);
        }

        // Deterministic geographic name for an unrealized partition region,
        // the same derivation realized plans use: the root's named feature,
        // else its biome.
        internal static string TopologyName(CARegionalTopologyRecord record)
        {
            if (record == null) return "Region";
            PlanetTile root =
                CARegionalPlanUtility.SurfaceTile(record.rootTileId);
            string feature = root.Valid ? root.Tile?.feature?.name : null;
            if (!feature.NullOrEmpty()) return feature + " region";
            string biome = root.Valid
                ? root.Tile?.PrimaryBiome?.label : null;
            return biome.NullOrEmpty() ? "Region"
                : biome.CapitalizeFirst() + " region";
        }

        // World settlements standing on a partition region's members.
        internal static int TopologySettlementCount(
            CARegionalTopologyRecord record)
        {
            if (record?.memberTileIds == null
                || record.memberTileIds.Count == 0) return 0;
            List<Settlement> settlements =
                Verse.Find.WorldObjects?.Settlements;
            if (settlements == null) return 0;
            var members = new HashSet<int>(record.memberTileIds);
            int count = 0;
            for (int i = 0; i < settlements.Count; i++)
                if (settlements[i] != null
                    && members.Contains(settlements[i].Tile.tileId))
                    count++;
            return count;
        }

        // POLITICAL GEOGRAPHY IS DERIVED, NEVER IMPLIED. A region is a
        // geographic container; who holds its ground derives from the
        // settlements actually standing there and their actual relations.
        // Unsettled, single-polity, divided, and contested are all
        // representable, and no click, arrival, or membership implies
        // ownership.
        internal static string PoliticalSummary(
            CARegionalTopologyRecord record)
        {
            if (record?.memberTileIds == null) return null;
            List<Settlement> settlements =
                Verse.Find.WorldObjects?.Settlements;
            if (settlements == null) return "unsettled";
            var members = new HashSet<int>(record.memberTileIds);
            var holders = new List<Faction>();
            for (int i = 0; i < settlements.Count; i++)
            {
                Settlement settlement = settlements[i];
                if (settlement == null
                    || !members.Contains(settlement.Tile.tileId)) continue;
                if (settlement.Faction != null
                    && !holders.Contains(settlement.Faction))
                    holders.Add(settlement.Faction);
            }
            return SummarizeHolders(holders);
        }

        internal static string PoliticalSummary(CARegionalPlan plan)
        {
            if (plan?.settlements == null) return null;
            var keys = plan.settlements.Where(item => item != null)
                .Select(item => item.OwningFactionKey)
                .Where(key => key >= 0).Distinct().ToList();
            if (keys.Count == 0)
                return plan.settlements.Count == 0 ? "unsettled"
                    : "settled, no political owner";
            if (keys.Count == 1)
            {
                CARegionalFactionPlan group = plan.FactionPlan(keys[0]);
                return "held by " + CARegionalPlanUtility.FactionName(group);
            }
            bool contested = false;
            for (int i = 0; i < keys.Count && !contested; i++)
                for (int j = i + 1; j < keys.Count && !contested; j++)
                    if (plan.RelationBetween(keys[i], keys[j])
                        == FactionRelationKind.Hostile)
                        contested = true;
            return (contested ? "contested among " : "divided among ")
                + keys.Count + " polities";
        }

        private static string SummarizeHolders(List<Faction> holders)
        {
            if (holders.Count == 0) return "unsettled";
            if (holders.Count == 1) return "held by " + holders[0].Name;
            bool contested = false;
            for (int i = 0; i < holders.Count && !contested; i++)
                for (int j = i + 1; j < holders.Count && !contested; j++)
                    if (holders[i].HostileTo(holders[j]))
                        contested = true;
            return (contested ? "contested among " : "divided among ")
                + holders.Count + " polities";
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
            if (region != null)
            {
                string held = CARegionalGeography.PoliticalSummary(region);
                string prefix = "Geographic region: "
                    + CARegionalPlanUtility.RegionName(region)
                    + "\n" + region.RegionTileCount + " connected areas - "
                    + region.factions.Count + " faction"
                    + (region.factions.Count == 1 ? "" : "s")
                    + " - " + region.settlements.Count + " settlement"
                    + (region.settlements.Count == 1 ? "" : "s")
                    + (held.NullOrEmpty() ? "" : "\nLand: " + held);
                __result = prefix + "\n\nSelected land\n" + __result;
                return;
            }
            CARegionalTopologyRecord record =
                CARegionalGeography.TopologyAt(tile);
            if (record == null) return;
            int settlements =
                CARegionalGeography.TopologySettlementCount(record);
            // ONE SOURCE FOR WHO HOLDS THE LAND. The world now remembers
            // each region's holders and the dated events that changed
            // them; recomputing a summary here would let the tile pane
            // and the settlement pane disagree about the same region.
            // The remembered record answers, and the derivation remains
            // the fallback for land it has never had to record.
            CARegionalPoliticalRecord political =
                CARegionalWorldComponent.Current
                    ?.PoliticalRecordFor(record.regionId);
            string topologyHeld = political?.StatusLine
                ?? CARegionalGeography.PoliticalSummary(record);
            CARegionalPoliticalEvent latest =
                political?.events?.LastOrDefault();
            string topologyPrefix = "Geographic region: "
                + CARegionalGeography.TopologyName(record)
                + "\n" + record.memberTileIds.Count + " connected area"
                + (record.memberTileIds.Count == 1 ? "" : "s")
                + (settlements > 0
                    ? " - " + settlements + " settlement"
                        + (settlements == 1 ? "" : "s")
                    : "")
                + (topologyHeld.NullOrEmpty() ? ""
                    : "\nLand: " + topologyHeld)
                + (latest == null ? ""
                    : "\n" + CARegionalPoliticalLedger.EventLine(latest));
            __result = topologyPrefix + "\n\nSelected land\n" + __result;
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
            {
                __result = CARegionalPlanUtility.RegionName(region)
                    + " - " + __result;
                return;
            }
            CARegionalTopologyRecord record =
                CARegionalGeography.TopologyAt(tile);
            if (record != null)
                __result = CARegionalGeography.TopologyName(record)
                    + " - " + __result;
        }
    }
}
