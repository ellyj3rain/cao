using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;

internal sealed class WorldAtlas
{
    private readonly byte[] biome;
    private readonly byte[] elevation;
    private readonly byte[] hilliness;
    private readonly byte[] temperature;
    private readonly byte[] rainfall;
    private readonly byte[] swampiness;
    private readonly byte[] pollution;
    private readonly byte[] feature;
    private readonly byte[] riverDistance;
    private readonly Dictionary<int, List<ushort>> mutatorsByTile;
    private readonly Dictionary<int, List<AtlasLink>> roadsByTile;
    private readonly Dictionary<int, List<AtlasLink>> riversByTile;
    private readonly Dictionary<int, AtlasFeatureRecord> featuresById;
    private readonly Dictionary<int, List<AtlasWorldObjectRecord>>
        objectsByTile;

    internal AtlasManifest Manifest { get; }
    internal PlanetTopology Topology { get; }
    internal int TileCount => Manifest.TileCount;

    private WorldAtlas(AtlasManifest manifest, PlanetTopology topology,
        Dictionary<string, byte[]> arrays)
    {
        Manifest = manifest;
        Topology = topology;
        biome = Required("tileBiome");
        elevation = Required("tileElevation");
        hilliness = Required("tileHilliness");
        temperature = Required("tileTemperature");
        rainfall = Required("tileRainfall");
        swampiness = Required("tileSwampiness");
        pollution = Required("tilePollution");
        feature = Required("tileFeature");
        riverDistance = Required("tileRiverDistances");
        mutatorsByTile = BuildMutators(arrays);
        roadsByTile = BuildLinks(arrays, "tileRoadOrigins",
            "tileRoadAdjacency", "tileRoadDef");
        riversByTile = BuildLinks(arrays, "tileRiverOrigins",
            "tileRiverAdjacency", "tileRiverDef");
        featuresById = manifest.Features.ToDictionary(item => item.UniqueId);
        objectsByTile = manifest.WorldObjects.GroupBy(item => item.TileId)
            .ToDictionary(group => group.Key, group => group.ToList());

        byte[] Required(string name) => arrays.TryGetValue(name,
                out byte[]? value) ? value
            : throw new InvalidDataException($"Atlas array {name} is absent.");
    }

    internal static WorldAtlas Load(string directory)
    {
        AtlasManifest manifest = AtlasManifest.Load(directory);
        PlanetTopology topology = PlanetTopology.Load(Path.Combine(directory,
            manifest.TopologyFile));
        if (topology.TileCount != manifest.TileCount)
            throw new InvalidDataException("Atlas topology and manifest tile "
                + "counts disagree.");
        var arrays = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach ((string name, AtlasArrayRecord record) in manifest.Arrays)
        {
            string path = Path.Combine(directory, record.File.Replace('/',
                Path.DirectorySeparatorChar));
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length != record.Bytes)
                throw new InvalidDataException($"Atlas array {name} changed "
                    + "length after import.");
            arrays[name] = bytes;
        }
        return new WorldAtlas(manifest, topology, arrays);
    }

    internal AtlasTileFact Inspect(int tileId)
    {
        if ((uint)tileId >= (uint)TileCount)
            throw new ArgumentOutOfRangeException(nameof(tileId));
        ushort biomeHash = UShort(biome, tileId);
        Manifest.Defs.Biomes.TryGetValue(biomeHash,
            out AtlasDefRecord? biomeDef);
        ushort featureId = UShort(feature, tileId);
        AtlasFeatureRecord? featureRecord = featureId == ushort.MaxValue
            ? null : featuresById.GetValueOrDefault(featureId);
        List<AtlasDefRecord> mutatorDefs = mutatorsByTile
            .GetValueOrDefault(tileId)?.Select(hash =>
                Manifest.Defs.TileMutators.GetValueOrDefault(hash)
                ?? UnknownDef("mutator", hash)).ToList()
            ?? new List<AtlasDefRecord>();
        Vector3 center = Topology.Centers[tileId];
        (float longitude, float latitude) = LongLat(center,
            Manifest.SurfaceRadius == 0f ? 100f : Manifest.SurfaceRadius);
        byte hillinessValue = hilliness[tileId];
        bool blocked = biomeDef == null || biomeDef.IsWaterBiome
            || biomeDef.Impassable || hillinessValue == 5;
        return new AtlasTileFact
        {
            TileId = tileId,
            Longitude = longitude,
            Latitude = latitude,
            BiomeHash = biomeHash,
            Biome = biomeDef ?? UnknownDef("biome", biomeHash),
            Elevation = UShort(elevation, tileId) - 8192,
            HillinessValue = hillinessValue,
            Hilliness = HillinessLabel(hillinessValue),
            Temperature = UShort(temperature, tileId) / 10f - 300f,
            Rainfall = UShort(rainfall, tileId),
            Swampiness = swampiness[tileId] / 255f,
            Pollution = UShort(pollution, tileId) / 65535f,
            Feature = featureRecord,
            Mutators = mutatorDefs,
            Roads = roadsByTile.GetValueOrDefault(tileId)?.ToList()
                ?? new List<AtlasLink>(),
            Rivers = riversByTile.GetValueOrDefault(tileId)?.ToList()
                ?? new List<AtlasLink>(),
            RiverDistance = riverDistance[tileId],
            WorldObjects = objectsByTile.GetValueOrDefault(tileId)?.ToList()
                ?? new List<AtlasWorldObjectRecord>(),
            Blocked = blocked,
            DefinitionComplete = biomeDef != null
                && mutatorDefs.All(item => !item.DefName.StartsWith("unknown-",
                    StringComparison.Ordinal))
        };
    }

    internal bool IsAvailable(int tileId)
    {
        if ((uint)tileId >= (uint)TileCount || objectsByTile.ContainsKey(tileId))
            return false;
        ushort biomeHash = UShort(biome, tileId);
        if (!Manifest.Defs.Biomes.TryGetValue(biomeHash,
                out AtlasDefRecord? definition)) return false;
        return !definition.IsWaterBiome && !definition.Impassable
            && hilliness[tileId] != 5;
    }

    private Dictionary<int, List<ushort>> BuildMutators(
        Dictionary<string, byte[]> arrays)
    {
        var result = new Dictionary<int, List<ushort>>();
        if (!arrays.TryGetValue("tileMutatorTiles", out byte[]? tiles)
            || !arrays.TryGetValue("tileMutatorDefs", out byte[]? defs))
            return result;
        int count = Math.Min(tiles.Length / 4, defs.Length / 2);
        for (int i = 0; i < count; i++)
        {
            int tile = BinaryPrimitives.ReadInt32LittleEndian(
                tiles.AsSpan(i * 4, 4));
            ushort hash = BinaryPrimitives.ReadUInt16LittleEndian(
                defs.AsSpan(i * 2, 2));
            if (!result.TryGetValue(tile, out List<ushort>? list))
                result[tile] = list = new List<ushort>();
            list.Add(hash);
        }
        return result;
    }

    private Dictionary<int, List<AtlasLink>> BuildLinks(
        Dictionary<string, byte[]> arrays, string originsName,
        string adjacencyName, string defsName)
    {
        var result = new Dictionary<int, List<AtlasLink>>();
        if (!arrays.TryGetValue(originsName, out byte[]? origins)
            || !arrays.TryGetValue(adjacencyName, out byte[]? adjacency)
            || !arrays.TryGetValue(defsName, out byte[]? defs)) return result;
        int count = Math.Min(origins.Length / 4,
            Math.Min(adjacency.Length, defs.Length / 2));
        Dictionary<ushort, AtlasDefRecord> catalog = defsName.Contains("Road",
            StringComparison.Ordinal) ? Manifest.Defs.Roads
            : Manifest.Defs.Rivers;
        for (int i = 0; i < count; i++)
        {
            int origin = BinaryPrimitives.ReadInt32LittleEndian(
                origins.AsSpan(i * 4, 4));
            byte neighborIndex = adjacency[i];
            ReadOnlySpan<int> neighbors = Topology.NeighborsOf(origin);
            if (neighborIndex >= neighbors.Length) continue;
            int destination = neighbors[neighborIndex];
            ushort hash = BinaryPrimitives.ReadUInt16LittleEndian(
                defs.AsSpan(i * 2, 2));
            AtlasDefRecord definition = catalog.GetValueOrDefault(hash)
                ?? UnknownDef(defsName.Contains("Road",
                    StringComparison.Ordinal) ? "road" : "river", hash);
            Add(origin, destination, definition);
            Add(destination, origin, definition);
        }
        return result;

        void Add(int from, int to, AtlasDefRecord definition)
        {
            if (!result.TryGetValue(from, out List<AtlasLink>? list))
                result[from] = list = new List<AtlasLink>();
            list.Add(new AtlasLink { OtherTileId = to,
                Definition = definition });
        }
    }

    private static ushort UShort(byte[] bytes, int index) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index * 2, 2));

    private static (float longitude, float latitude) LongLat(Vector3 center,
        float radius)
    {
        float longitude = MathF.Atan2(center.X, -center.Z) * 180f / MathF.PI;
        float latitude = MathF.Asin(center.Y / radius) * 180f / MathF.PI;
        return (longitude, latitude);
    }

    private static AtlasDefRecord UnknownDef(string kind, ushort hash) => new()
    {
        DefName = $"unknown-{kind}-{hash}",
        Label = $"Unknown {kind} {hash}"
    };

    private static string HillinessLabel(byte value) => value switch
    {
        1 => "Flat",
        2 => "Small hills",
        3 => "Large hills",
        4 => "Mountainous",
        5 => "Impassable",
        _ => "Undefined"
    };
}

internal sealed class AtlasTileFact
{
    public int TileId { get; set; }
    public float Longitude { get; set; }
    public float Latitude { get; set; }
    public ushort BiomeHash { get; set; }
    public AtlasDefRecord Biome { get; set; } = new();
    public int Elevation { get; set; }
    public byte HillinessValue { get; set; }
    public string Hilliness { get; set; } = "";
    public float Temperature { get; set; }
    public int Rainfall { get; set; }
    public float Swampiness { get; set; }
    public float Pollution { get; set; }
    public AtlasFeatureRecord? Feature { get; set; }
    public List<AtlasDefRecord> Mutators { get; set; } = new();
    public List<AtlasLink> Roads { get; set; } = new();
    public List<AtlasLink> Rivers { get; set; } = new();
    public byte RiverDistance { get; set; }
    public List<AtlasWorldObjectRecord> WorldObjects { get; set; } = new();
    public bool Blocked { get; set; }
    public bool DefinitionComplete { get; set; }

    public IEnumerable<string> SearchTerms()
    {
        yield return Biome.DefName;
        yield return Biome.Label;
        yield return Hilliness;
        if (Feature != null)
        {
            yield return Feature.DefName;
            yield return Feature.Name;
        }
        foreach (AtlasDefRecord mutator in Mutators)
        {
            yield return mutator.DefName;
            yield return mutator.Label;
        }
        foreach (AtlasLink road in Roads)
        {
            yield return road.Definition.DefName;
            yield return road.Definition.Label;
        }
        foreach (AtlasLink river in Rivers)
        {
            yield return river.Definition.DefName;
            yield return river.Definition.Label;
        }
    }
}

internal sealed class AtlasLink
{
    public int OtherTileId { get; set; }
    public AtlasDefRecord Definition { get; set; } = new();
}
