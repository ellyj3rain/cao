using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class AtlasManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string WorldIdentity { get; set; } = "";
    public string WorldName { get; set; } = "";
    public string SeedString { get; set; } = "";
    public float PlanetCoverage { get; set; }
    public int PersistentRandomValue { get; set; }
    public string GameVersion { get; set; } = "";
    public List<string> ModIds { get; set; } = new();
    public string ModFingerprint { get; set; } = "";
    public string SourceSaveName { get; set; } = "";
    public string SourceSaveSha256 { get; set; } = "";
    public DateTimeOffset SourceSaveLastWriteUtc { get; set; }
    public DateTimeOffset ImportedUtc { get; set; }
    public int SurfaceLayerId { get; set; }
    public int SurfaceSubdivisions { get; set; }
    public float SurfaceRadius { get; set; }
    public float SurfaceViewAngle { get; set; }
    public string SurfaceViewCenter { get; set; } = "";
    public int TileCount { get; set; }
    public string TopologyFile { get; set; } = "topology.bin";
    public Dictionary<string, AtlasArrayRecord> Arrays { get; set; } = new();
    public List<AtlasFeatureRecord> Features { get; set; } = new();
    public List<AtlasWorldObjectRecord> WorldObjects { get; set; } = new();
    public AtlasDefCatalog Defs { get; set; } = new();
    public List<string> UnresolvedObligations { get; set; } = new();

    [JsonIgnore]
    public string AtlasDirectory { get; set; } = "";

    internal void Save(string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "manifest.json");
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this,
            JsonOptions.Indented));
        File.Move(temporary, path, true);
    }

    internal static AtlasManifest Load(string directory)
    {
        string path = Path.Combine(directory, "manifest.json");
        AtlasManifest manifest = JsonSerializer.Deserialize<AtlasManifest>(
            File.ReadAllText(path), JsonOptions.Default)
            ?? throw new InvalidDataException("Atlas manifest is empty.");
        manifest.AtlasDirectory = Path.GetFullPath(directory);
        return manifest;
    }
}

internal sealed class AtlasArrayRecord
{
    public string File { get; set; } = "";
    public int Bytes { get; set; }
    public string Sha256 { get; set; } = "";
    public string Encoding { get; set; } = "";
}

internal sealed class AtlasFeatureRecord
{
    public int UniqueId { get; set; }
    public string DefName { get; set; } = "";
    public string Name { get; set; } = "";
    public string DrawCenter { get; set; } = "";
    public float MaxDrawSizeInTiles { get; set; }
}

internal sealed class AtlasWorldObjectRecord
{
    public string Class { get; set; } = "";
    public string DefName { get; set; } = "";
    public string LoadId { get; set; } = "";
    public string FactionReference { get; set; } = "";
    public string Name { get; set; } = "";
    public int TileId { get; set; } = -1;
}

internal sealed class AtlasDefCatalog
{
    public Dictionary<ushort, AtlasDefRecord> Biomes { get; set; } = new();
    public Dictionary<ushort, AtlasDefRecord> TileMutators { get; set; } = new();
    public Dictionary<ushort, AtlasDefRecord> Roads { get; set; } = new();
    public Dictionary<ushort, AtlasDefRecord> Rivers { get; set; } = new();
    public List<AtlasDefRecord> NaturalRocks { get; set; } = new();
    public List<string> MissingModIds { get; set; } = new();
}

internal sealed class AtlasDefRecord
{
    public string DefName { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public string SourceModId { get; set; } = "";
    public bool IsWaterBiome { get; set; }
    public bool Impassable { get; set; }
    public bool IsNaturalRock { get; set; }
    public bool BiomeSpecific { get; set; }
}

internal static class JsonOptions
{
    internal static readonly JsonSerializerOptions Default = Create(false);
    internal static readonly JsonSerializerOptions Indented = Create(true);

    private static JsonSerializerOptions Create(bool indented) => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = indented,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
