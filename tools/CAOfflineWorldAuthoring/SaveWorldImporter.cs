using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static class SaveWorldImporter
{
    private static readonly HashSet<string> SurfaceArrays = new(
        StringComparer.Ordinal)
    {
        "tileBiome", "tileElevation", "tileHilliness", "tileTemperature",
        "tileRainfall", "tileSwampiness", "tileFeature", "tilePollution",
        "tileRoadOrigins", "tileRoadAdjacency", "tileRoadDef",
        "tileRiverOrigins", "tileRiverAdjacency", "tileRiverDef",
        "tileRiverDistances", "tileMutatorTiles", "tileMutatorDefs"
    };

    internal static AtlasManifest Import(string savePath, string outputDirectory,
        string gameRoot, Action<string>? progress = null)
    {
        savePath = Path.GetFullPath(savePath);
        outputDirectory = Path.GetFullPath(outputDirectory);
        if (!File.Exists(savePath)) throw new FileNotFoundException(
            "RimWorld save not found.", savePath);
        Directory.CreateDirectory(outputDirectory);
        string arraysDirectory = Path.Combine(outputDirectory, "arrays");
        Directory.CreateDirectory(arraysDirectory);

        var manifest = new AtlasManifest
        {
            SourceSaveName = Path.GetFileName(savePath),
            SourceSaveLastWriteUtc = File.GetLastWriteTimeUtc(savePath),
            ImportedUtc = DateTimeOffset.UtcNow,
            AtlasDirectory = outputDirectory
        };

        progress?.Invoke("hashing source save");
        manifest.SourceSaveSha256 = HashFile(savePath);
        ParseSave(savePath, arraysDirectory, manifest, progress);
        manifest.WorldIdentity = manifest.SeedString + "|"
            + manifest.PlanetCoverage.ToString("0.########",
                CultureInfo.InvariantCulture) + "|" + manifest.WorldName;
        manifest.ModFingerprint = HashText(string.Join("\n", manifest.ModIds));
        manifest.Defs = DefCatalogReader.Read(gameRoot, manifest.ModIds,
            progress);

        if (!manifest.Arrays.TryGetValue("tileHilliness", out var hilliness))
            throw new InvalidDataException("The save has no surface hilliness "
                + "array.");
        manifest.TileCount = hilliness.Bytes;
        ValidateArrayLengths(manifest);

        string topologyPath = Path.Combine(outputDirectory,
            manifest.TopologyFile);
        if (File.Exists(topologyPath))
        {
            progress?.Invoke("validating existing topology cache");
            PlanetTopology existing = PlanetTopology.Load(topologyPath);
            if (existing.Subdivisions != manifest.SurfaceSubdivisions
                || existing.TileCount != manifest.TileCount)
                throw new InvalidDataException("Existing topology cache does "
                    + "not match this saved surface.");
        }
        else
        {
            PlanetTopology topology = PlanetTopology.BuildFullSurface(
                manifest.SurfaceSubdivisions, progress);
            if (topology.TileCount != manifest.TileCount)
                throw new InvalidDataException("Reconstructed topology has "
                    + $"{topology.TileCount:N0} tiles; save arrays have "
                    + $"{manifest.TileCount:N0}.");
            progress?.Invoke("writing topology cache");
            topology.Save(topologyPath);
        }

        if (manifest.Defs.MissingModIds.Count > 0)
            manifest.UnresolvedObligations.Add("missing active mod content: "
                + string.Join(", ", manifest.Defs.MissingModIds));
        if (manifest.Defs.Biomes.Count == 0)
            manifest.UnresolvedObligations.Add("biome short hashes unresolved");
        if (manifest.Defs.TileMutators.Count == 0
            && manifest.Arrays.TryGetValue("tileMutatorDefs", out var mutators)
            && mutators.Bytes > 0)
            manifest.UnresolvedObligations.Add(
                "tile-mutator short hashes unresolved");

        manifest.Save(outputDirectory);
        progress?.Invoke("atlas complete: " + manifest.WorldIdentity + "; "
            + $"{manifest.TileCount:N0} tiles");
        return manifest;
    }

    private static void ParseSave(string savePath, string arraysDirectory,
        AtlasManifest manifest, Action<string>? progress)
    {
        var settings = new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Prohibit
        };
        using XmlReader reader = XmlReader.Create(savePath, settings);
        var ancestry = new Dictionary<int, string>();
        bool hasNode = reader.Read();
        int arraysFound = 0;
        while (hasNode && !reader.EOF)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                hasNode = reader.Read();
                continue;
            }

            int depth = reader.Depth;
            string name = reader.Name;
            ancestry[depth] = name;
            string parent = Ancestor(ancestry, depth - 1);
            string grandparent = Ancestor(ancestry, depth - 2);

            if (name == "li" && parent == "modIds")
            {
                manifest.ModIds.Add(reader.ReadElementContentAsString());
                hasNode = !reader.EOF;
                continue;
            }
            if (name == "gameVersion" && parent == "meta")
            {
                manifest.GameVersion = reader.ReadElementContentAsString();
                hasNode = !reader.EOF;
                continue;
            }
            if (parent == "info" && grandparent == "world"
                && name is "name" or "seedString" or "planetCoverage"
                    or "persistentRandomValue")
            {
                string value = reader.ReadElementContentAsString();
                switch (name)
                {
                    case "name": manifest.WorldName = value; break;
                    case "seedString": manifest.SeedString = value; break;
                    case "planetCoverage": manifest.PlanetCoverage =
                        ParseFloat(value); break;
                    case "persistentRandomValue":
                        manifest.PersistentRandomValue = ParseInt(value); break;
                }
                hasNode = !reader.EOF;
                continue;
            }

            bool inGrid = ancestry.Values.Contains("grid",
                StringComparer.Ordinal);
            if (inGrid && !manifest.Arrays.ContainsKey("tileBiome")
                && name is "layerId" or "subdivisions" or "radius"
                    or "viewAngle" or "viewCenter")
            {
                string value = reader.ReadElementContentAsString();
                switch (name)
                {
                    case "layerId": manifest.SurfaceLayerId = ParseInt(value);
                        break;
                    case "subdivisions": manifest.SurfaceSubdivisions =
                        ParseInt(value); break;
                    case "radius": manifest.SurfaceRadius = ParseFloat(value);
                        break;
                    case "viewAngle": manifest.SurfaceViewAngle =
                        ParseFloat(value); break;
                    case "viewCenter": manifest.SurfaceViewCenter = value;
                        break;
                }
                hasNode = !reader.EOF;
                continue;
            }

            string baseArrayName = name.EndsWith("Deflate",
                    StringComparison.Ordinal)
                ? name[..^"Deflate".Length] : name;
            if (SurfaceArrays.Contains(baseArrayName))
            {
                progress?.Invoke("extracting " + baseArrayName);
                string encoded = reader.ReadElementContentAsString();
                if (manifest.Arrays.ContainsKey(baseArrayName))
                {
                    // The root surface is serialized first. Odyssey and other
                    // content may add later layers with the same raw field
                    // names; they are not candidates for Starting Region.
                    hasNode = !reader.EOF;
                    continue;
                }
                byte[] source = Convert.FromBase64String(RemoveWhitespace(
                    encoded));
                byte[] raw = name.EndsWith("Deflate", StringComparison.Ordinal)
                    ? Inflate(source) : source;
                string relative = Path.Combine("arrays",
                    baseArrayName + ".bin").Replace('\\', '/');
                string target = Path.Combine(arraysDirectory,
                    baseArrayName + ".bin");
                File.WriteAllBytes(target, raw);
                manifest.Arrays[baseArrayName] = new AtlasArrayRecord
                {
                    File = relative,
                    Bytes = raw.Length,
                    Sha256 = Convert.ToHexString(SHA256.HashData(raw)),
                    Encoding = ArrayEncoding(baseArrayName)
                };
                arraysFound++;
                hasNode = !reader.EOF;
                continue;
            }

            if (name == "li" && parent == "features"
                && grandparent == "features")
            {
                XElement element = (XElement)XNode.ReadFrom(reader);
                manifest.Features.Add(ParseFeature(element));
                hasNode = !reader.EOF;
                continue;
            }
            if (name == "li" && parent == "worldObjects"
                && grandparent == "worldObjects")
            {
                XElement element = (XElement)XNode.ReadFrom(reader);
                AtlasWorldObjectRecord? worldObject = ParseWorldObject(element);
                if (worldObject != null) manifest.WorldObjects.Add(worldObject);
                hasNode = !reader.EOF;
                continue;
            }

            hasNode = reader.Read();
        }
        progress?.Invoke($"extracted {arraysFound} surface arrays, "
            + $"{manifest.Features.Count} world features, "
            + $"{manifest.WorldObjects.Count} map-bearing world objects");
    }

    private static AtlasFeatureRecord ParseFeature(XElement element) => new()
    {
        UniqueId = ParseInt(element.Element("uniqueID")?.Value ?? "0"),
        DefName = element.Element("def")?.Value ?? "",
        Name = element.Element("name")?.Value ?? "",
        DrawCenter = element.Element("drawCenter")?.Value ?? "",
        MaxDrawSizeInTiles = ParseFloat(element.Element("maxDrawSizeInTiles")
            ?.Value ?? "0")
    };

    private static AtlasWorldObjectRecord? ParseWorldObject(XElement element)
    {
        string tileText = element.Element("tile")?.Value ?? "";
        if (tileText.Length == 0) return null;
        int comma = tileText.IndexOf(',');
        if (comma >= 0) tileText = tileText[..comma];
        if (!int.TryParse(tileText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int tileId)) return null;
        return new AtlasWorldObjectRecord
        {
            Class = element.Attribute("Class")?.Value ?? "",
            DefName = element.Element("def")?.Value ?? "",
            LoadId = element.Element("loadID")?.Value ?? "",
            FactionReference = element.Element("faction")?.Value ?? "",
            Name = element.Element("name")?.Value ?? "",
            TileId = tileId
        };
    }

    private static void ValidateArrayLengths(AtlasManifest manifest)
    {
        int tiles = manifest.TileCount;
        Require("tileBiome", tiles * 2);
        Require("tileElevation", tiles * 2);
        Require("tileHilliness", tiles);
        Require("tileTemperature", tiles * 2);
        Require("tileRainfall", tiles * 2);
        Require("tileSwampiness", tiles);
        Require("tileFeature", tiles * 2);
        Require("tilePollution", tiles * 2);
        Require("tileRiverDistances", tiles);

        void Require(string name, int expected)
        {
            if (!manifest.Arrays.TryGetValue(name, out AtlasArrayRecord? record))
                throw new InvalidDataException($"Missing required surface "
                    + $"array {name}.");
            if (record.Bytes != expected)
                throw new InvalidDataException($"Surface array {name} has "
                    + $"{record.Bytes:N0} bytes; expected {expected:N0}.");
        }
    }

    private static byte[] Inflate(byte[] input)
    {
        using var source = new MemoryStream(input);
        using var inflater = new DeflateStream(source,
            CompressionMode.Decompress);
        using var output = new MemoryStream();
        inflater.CopyTo(output);
        return output.ToArray();
    }

    private static string ArrayEncoding(string name) => name switch
    {
        "tileBiome" or "tileElevation" or "tileTemperature"
            or "tileRainfall" or "tileFeature" or "tilePollution"
            or "tileRoadDef" or "tileRiverDef" or "tileMutatorDefs"
                => "little-endian-ushort",
        "tileRoadOrigins" or "tileRiverOrigins" or "tileMutatorTiles"
                => "little-endian-int32",
        _ => "byte"
    };

    private static string Ancestor(Dictionary<int, string> ancestry, int depth)
        => depth >= 0 && ancestry.TryGetValue(depth, out string? value)
            ? value : "";

    private static string RemoveWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
            if (!char.IsWhiteSpace(character)) builder.Append(character);
        return builder.ToString();
    }

    private static float ParseFloat(string value) => float.Parse(value,
        NumberStyles.Float, CultureInfo.InvariantCulture);

    private static int ParseInt(string value) => int.Parse(value,
        NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string HashText(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
