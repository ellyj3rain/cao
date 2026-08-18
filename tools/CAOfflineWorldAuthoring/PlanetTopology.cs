using System.Numerics;

internal sealed class PlanetTopology
{
    private const string Magic = "CAOTOP1";

    internal int Subdivisions { get; private init; }
    internal Vector3[] Centers { get; private init; } = Array.Empty<Vector3>();
    internal int[] NeighborOffsets { get; private init; } = Array.Empty<int>();
    internal int[] Neighbors { get; private init; } = Array.Empty<int>();
    internal int[] PolygonOffsets { get; private init; } = Array.Empty<int>();
    internal Vector3[] PolygonVertices { get; private init; } =
        Array.Empty<Vector3>();

    internal int TileCount => Centers.Length;

    internal ReadOnlySpan<int> NeighborsOf(int tileId)
    {
        ValidateTile(tileId);
        return Neighbors.AsSpan(NeighborOffsets[tileId],
            NeighborOffsets[tileId + 1] - NeighborOffsets[tileId]);
    }

    internal ReadOnlySpan<Vector3> PolygonOf(int tileId)
    {
        ValidateTile(tileId);
        return PolygonVertices.AsSpan(PolygonOffsets[tileId],
            PolygonOffsets[tileId + 1] - PolygonOffsets[tileId]);
    }

    internal void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        using (var stream = File.Create(temporary))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Magic);
            writer.Write(1);
            writer.Write(Subdivisions);
            writer.Write(Centers.Length);
            writer.Write(Neighbors.Length);
            writer.Write(PolygonVertices.Length);
            WriteVectors(writer, Centers);
            WriteInts(writer, NeighborOffsets);
            WriteInts(writer, Neighbors);
            WriteInts(writer, PolygonOffsets);
            WriteVectors(writer, PolygonVertices);
        }
        File.Move(temporary, path, true);
    }

    internal static PlanetTopology Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        string magic = reader.ReadString();
        if (magic != Magic) throw new InvalidDataException(
            $"Unsupported topology cache {magic}.");
        int version = reader.ReadInt32();
        if (version != 1) throw new InvalidDataException(
            $"Unsupported topology version {version}.");
        int subdivisions = reader.ReadInt32();
        int tileCount = reader.ReadInt32();
        int neighborCount = reader.ReadInt32();
        int polygonVertexCount = reader.ReadInt32();
        var result = new PlanetTopology
        {
            Subdivisions = subdivisions,
            Centers = ReadVectors(reader, tileCount),
            NeighborOffsets = ReadInts(reader, tileCount + 1),
            Neighbors = ReadInts(reader, neighborCount),
            PolygonOffsets = ReadInts(reader, tileCount + 1),
            PolygonVertices = ReadVectors(reader, polygonVertexCount)
        };
        if (stream.Position != stream.Length)
            throw new InvalidDataException("Topology cache has trailing data.");
        result.Validate();
        return result;
    }

    internal static PlanetTopology BuildFullSurface(int subdivisions,
        Action<string>? progress = null)
    {
        if (subdivisions < 0 || subdivisions > 12)
            throw new ArgumentOutOfRangeException(nameof(subdivisions));

        List<Vector3> vertices = InitialVertices();
        List<Triangle> triangles = InitialTriangles();
        var generatedTriangles = new List<Triangle>();
        var generatedPolygonVertices = new List<Vector3>();
        var polygonOffsets = new List<int>();
        var tileVertexOffsets = new List<int>();
        var tileVertexValues = new List<int>();
        var vertexTileOffsets = new List<int>();
        var vertexTileValues = new List<int>();
        var vertexTileWriteCounts = Array.Empty<byte>();

        for (int pass = 0; pass < subdivisions + 1; pass++)
        {
            bool lastPass = pass == subdivisions;
            progress?.Invoke($"topology pass {pass + 1}/{subdivisions + 1}: "
                + $"{vertices.Count:N0} vertices, {triangles.Count:N0} faces");
            BuildVertexTriangleLists(vertices.Count, triangles,
                out int[] vertexTriangleOffsets,
                out int[] vertexTriangleValues);
            int oldVertexCount = vertices.Count;
            int oldFaceCount = triangles.Count;
            if (vertices.Capacity < oldVertexCount + oldFaceCount)
                vertices.Capacity = oldVertexCount + oldFaceCount;
            for (int i = 0; i < oldFaceCount; i++)
            {
                Triangle face = triangles[i];
                Vector3 center = (vertices[face.A] + vertices[face.B]
                    + vertices[face.C]) / 3f;
                vertices.Add(Vector3.Normalize(center) * 100f);
            }

            generatedTriangles.Clear();
            if (!lastPass)
                generatedTriangles.Capacity = Math.Max(generatedTriangles.Capacity,
                    oldFaceCount * 3);
            else
            {
                vertexTileOffsets.Clear();
                vertexTileValues.Clear();
                vertexTileOffsets.Capacity = vertices.Count;
                vertexTileValues.Capacity = oldFaceCount * 6;
                for (int i = 0; i < vertices.Count; i++)
                {
                    vertexTileOffsets.Add(vertexTileValues.Count);
                    if (i < oldVertexCount) continue;
                    for (int slot = 0; slot < 6; slot++)
                        vertexTileValues.Add(-1);
                }
                vertexTileWriteCounts = new byte[vertices.Count];
                polygonOffsets.Capacity = oldVertexCount + 1;
                tileVertexOffsets.Capacity = oldVertexCount + 1;
                generatedPolygonVertices.Capacity = oldVertexCount * 6;
                tileVertexValues.Capacity = oldVertexCount * 6;
            }

            var adjacentFaces = new List<int>(6);
            var generatedTileVertices = new List<int>(6);
            for (int vertex = 0; vertex < oldVertexCount; vertex++)
            {
                CopyPackedList(vertexTriangleOffsets, vertexTriangleValues,
                    vertex, adjacentFaces);
                int adjacentCount = adjacentFaces.Count;
                if (!lastPass)
                {
                    for (int adjacentIndex = 0; adjacentIndex < adjacentCount;
                        adjacentIndex++)
                    {
                        int faceId = adjacentFaces[adjacentIndex];
                        int newVertex = oldVertexCount + faceId;
                        int orderedVertex = triangles[faceId]
                            .GetNextOrderedVertex(vertex);
                        int nextFace = -1;
                        for (int other = 0; other < adjacentCount; other++)
                        {
                            if (other == adjacentIndex) continue;
                            Triangle candidate = triangles[adjacentFaces[other]];
                            if (!candidate.Contains(orderedVertex)) continue;
                            nextFace = adjacentFaces[other];
                            break;
                        }
                        if (nextFace >= 0)
                            generatedTriangles.Add(new Triangle(vertex,
                                oldVertexCount + nextFace, newVertex));
                    }
                    continue;
                }

                if (adjacentCount != 5 && adjacentCount != 6) continue;
                int currentAdjacentIndex = 0;
                int currentTriangleVertex = triangles[adjacentFaces[0]]
                    .GetNextOrderedVertex(vertex);
                generatedTileVertices.Clear();
                for (int step = 0; step < adjacentCount; step++)
                {
                    generatedTileVertices.Add(oldVertexCount
                        + adjacentFaces[currentAdjacentIndex]);
                    int nextAdjacentIndex = NextAdjacentTriangleIndex(
                        triangles, adjacentFaces, currentAdjacentIndex,
                        currentTriangleVertex);
                    if (nextAdjacentIndex < 0)
                        throw new InvalidDataException("Could not walk a planet "
                            + $"tile boundary at source vertex {vertex}.");
                    currentTriangleVertex = triangles[
                        adjacentFaces[nextAdjacentIndex]]
                        .GetNextOrderedVertex(vertex);
                    currentAdjacentIndex = nextAdjacentIndex;
                }

                int tileId = polygonOffsets.Count;
                polygonOffsets.Add(generatedPolygonVertices.Count);
                tileVertexOffsets.Add(tileVertexValues.Count);
                foreach (int polygonVertex in generatedTileVertices)
                {
                    generatedPolygonVertices.Add(vertices[polygonVertex]);
                    tileVertexValues.Add(polygonVertex);
                    int start = vertexTileOffsets[polygonVertex];
                    int slot = vertexTileWriteCounts[polygonVertex]++;
                    if (slot >= 6)
                        throw new InvalidDataException("A topology vertex "
                            + "belongs to more than six tiles.");
                    vertexTileValues[start + slot] = tileId;
                }
            }

            triangles = new List<Triangle>(generatedTriangles);
        }

        int tileCount = polygonOffsets.Count;
        polygonOffsets.Add(generatedPolygonVertices.Count);
        tileVertexOffsets.Add(tileVertexValues.Count);
        var centers = new Vector3[tileCount];
        for (int tile = 0; tile < tileCount; tile++)
        {
            Vector3 sum = Vector3.Zero;
            int start = polygonOffsets[tile];
            int end = polygonOffsets[tile + 1];
            for (int i = start; i < end; i++) sum += generatedPolygonVertices[i];
            centers[tile] = sum / (end - start);
        }

        progress?.Invoke($"topology adjacency: {tileCount:N0} tiles");
        BuildTileNeighbors(tileCount, tileVertexOffsets, tileVertexValues,
            vertexTileOffsets, vertexTileValues, out int[] neighborOffsets,
            out int[] neighbors);

        var topology = new PlanetTopology
        {
            Subdivisions = subdivisions,
            Centers = centers,
            NeighborOffsets = neighborOffsets,
            Neighbors = neighbors,
            PolygonOffsets = polygonOffsets.ToArray(),
            PolygonVertices = generatedPolygonVertices.ToArray()
        };
        topology.Validate();
        return topology;
    }

    private static void BuildTileNeighbors(int tileCount,
        List<int> tileVertexOffsets, List<int> tileVertexValues,
        List<int> vertexTileOffsets, List<int> vertexTileValues,
        out int[] offsets, out int[] values)
    {
        offsets = new int[tileCount + 1];
        var result = new List<int>(tileCount * 6);
        Span<int> seen = stackalloc int[6];
        for (int tile = 0; tile < tileCount; tile++)
        {
            offsets[tile] = result.Count;
            int polygonStart = tileVertexOffsets[tile];
            int polygonEnd = tileVertexOffsets[tile + 1];
            int polygonCount = polygonEnd - polygonStart;
            int seenCount = 0;
            for (int edge = 0; edge < polygonCount; edge++)
            {
                int first = tileVertexValues[polygonStart + edge];
                int second = tileVertexValues[polygonStart
                    + ((edge + 1) % polygonCount)];
                int firstStart = vertexTileOffsets[first];
                int secondStart = vertexTileOffsets[second];
                int neighbor = -1;
                for (int a = 0; a < 6 && neighbor < 0; a++)
                {
                    int candidate = vertexTileValues[firstStart + a];
                    if (candidate < 0 || candidate == tile) continue;
                    for (int b = 0; b < 6; b++)
                    {
                        if (vertexTileValues[secondStart + b] != candidate)
                            continue;
                        neighbor = candidate;
                        break;
                    }
                }
                if (neighbor < 0)
                    throw new InvalidDataException($"No neighbor across tile "
                        + $"{tile} edge {edge}.");
                bool duplicate = false;
                for (int i = 0; i < seenCount; i++)
                    if (seen[i] == neighbor) duplicate = true;
                if (duplicate) continue;
                seen[seenCount++] = neighbor;
                result.Add(neighbor);
            }
        }
        offsets[tileCount] = result.Count;
        values = result.ToArray();
    }

    private static void BuildVertexTriangleLists(int vertexCount,
        List<Triangle> triangles, out int[] offsets, out int[] values)
    {
        var counts = new int[vertexCount];
        foreach (Triangle triangle in triangles)
        {
            counts[triangle.A]++;
            counts[triangle.B]++;
            counts[triangle.C]++;
        }
        offsets = new int[vertexCount + 1];
        for (int i = 0; i < vertexCount; i++)
            offsets[i + 1] = offsets[i] + counts[i];
        values = new int[offsets[vertexCount]];
        Array.Clear(counts);
        for (int i = 0; i < triangles.Count; i++)
        {
            Triangle triangle = triangles[i];
            values[offsets[triangle.A] + counts[triangle.A]++] = i;
            values[offsets[triangle.B] + counts[triangle.B]++] = i;
            values[offsets[triangle.C] + counts[triangle.C]++] = i;
        }
    }

    private static void CopyPackedList(int[] offsets, int[] values, int index,
        List<int> destination)
    {
        destination.Clear();
        for (int i = offsets[index]; i < offsets[index + 1]; i++)
            destination.Add(values[i]);
    }

    private static int NextAdjacentTriangleIndex(List<Triangle> triangles,
        List<int> adjacentFaces, int currentIndex, int currentTriangleVertex)
    {
        for (int i = 0; i < adjacentFaces.Count; i++)
        {
            if (i == currentIndex) continue;
            if (triangles[adjacentFaces[i]].Contains(currentTriangleVertex))
                return i;
        }
        return -1;
    }

    private static List<Vector3> InitialVertices()
    {
        float golden = (1f + MathF.Sqrt(5f)) / 2f;
        return new List<Vector3>
        {
            Unit(-1f, golden, 0f), Unit(1f, golden, 0f),
            Unit(-1f, -golden, 0f), Unit(1f, -golden, 0f),
            Unit(0f, -1f, golden), Unit(0f, 1f, golden),
            Unit(0f, -1f, -golden), Unit(0f, 1f, -golden),
            Unit(golden, 0f, -1f), Unit(golden, 0f, 1f),
            Unit(-golden, 0f, -1f), Unit(-golden, 0f, 1f)
        };

        static Vector3 Unit(float x, float y, float z) =>
            Vector3.Normalize(new Vector3(x, y, z)) * 100f;
    }

    private static List<Triangle> InitialTriangles() => new()
    {
        new(0, 11, 5), new(0, 5, 1), new(0, 1, 7), new(0, 7, 10),
        new(0, 10, 11), new(1, 5, 9), new(5, 11, 4), new(11, 10, 2),
        new(10, 7, 6), new(7, 1, 8), new(3, 9, 4), new(3, 4, 2),
        new(3, 2, 6), new(3, 6, 8), new(3, 8, 9), new(4, 9, 5),
        new(2, 4, 11), new(6, 2, 10), new(8, 6, 7), new(9, 8, 1)
    };

    private void Validate()
    {
        if (NeighborOffsets.Length != TileCount + 1
            || PolygonOffsets.Length != TileCount + 1)
            throw new InvalidDataException("Topology offset counts disagree.");
        if (NeighborOffsets[^1] != Neighbors.Length
            || PolygonOffsets[^1] != PolygonVertices.Length)
            throw new InvalidDataException("Topology terminal offsets disagree.");
        for (int tile = 0; tile < TileCount; tile++)
        {
            int neighborCount = NeighborOffsets[tile + 1]
                - NeighborOffsets[tile];
            int vertexCount = PolygonOffsets[tile + 1]
                - PolygonOffsets[tile];
            if ((neighborCount != 5 && neighborCount != 6)
                || neighborCount != vertexCount)
                throw new InvalidDataException($"Tile {tile} has "
                    + $"{neighborCount} neighbors and {vertexCount} vertices.");
        }
    }

    private void ValidateTile(int tileId)
    {
        if ((uint)tileId >= (uint)TileCount)
            throw new ArgumentOutOfRangeException(nameof(tileId));
    }

    private static void WriteInts(BinaryWriter writer, int[] values)
    {
        foreach (int value in values) writer.Write(value);
    }

    private static int[] ReadInts(BinaryReader reader, int count)
    {
        var values = new int[count];
        for (int i = 0; i < count; i++) values[i] = reader.ReadInt32();
        return values;
    }

    private static void WriteVectors(BinaryWriter writer, Vector3[] values)
    {
        foreach (Vector3 value in values)
        {
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(value.Z);
        }
    }

    private static Vector3[] ReadVectors(BinaryReader reader, int count)
    {
        var values = new Vector3[count];
        for (int i = 0; i < count; i++)
            values[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(),
                reader.ReadSingle());
        return values;
    }

    private readonly record struct Triangle(int A, int B, int C)
    {
        internal bool Contains(int vertex) => A == vertex || B == vertex
            || C == vertex;

        internal int GetNextOrderedVertex(int root)
        {
            if (A == root) return B;
            if (B == root) return C;
            return A;
        }
    }
}
