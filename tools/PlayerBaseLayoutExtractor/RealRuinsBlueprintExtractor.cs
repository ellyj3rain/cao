using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

namespace ColonistAwareness.Tools.PlayerBaseLayoutExtractor;

internal static class RealRuinsBlueprintExtractor
{
    private const string BucketRoot =
        "https://realruinsv2.sfo2.digitaloceanspaces.com/";

    public static SaveLayoutRecord Extract(string blueprintPath)
    {
        FileInfo sourceFile = new(blueprintPath);
        string sha256;
        using (FileStream stream = sourceFile.OpenRead())
        {
            sha256 = Convert.ToHexString(SHA256.HashData(stream));
        }

        string xml = ReadCompressedXml(blueprintPath);
        bool complete = xml.TrimEnd().EndsWith("</snapshot>",
            StringComparison.Ordinal);
        bool recoveredTruncation = false;
        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.None);
        }
        catch (XmlException exception) when (!complete)
        {
            int lastCompleteCell = xml.LastIndexOf("</cell>",
                StringComparison.Ordinal);
            if (lastCompleteCell < 0)
            {
                throw new InvalidDataException(
                    "The Real Ruins snapshot is truncated before its first "
                    + "complete cell.", exception);
            }
            string repaired = xml[..(lastCompleteCell + "</cell>".Length)]
                + "</snapshot>";
            try
            {
                document = XDocument.Parse(repaired, LoadOptions.None);
                recoveredTruncation = true;
            }
            catch (XmlException recoveryException)
            {
                throw new InvalidDataException(
                    "The Real Ruins snapshot is not structurally recoverable.",
                    recoveryException);
            }
        }

        XElement root = document.Root
            ?? throw new InvalidDataException(
                "The Real Ruins snapshot has no XML root.");
        if (root.Name.LocalName != "snapshot")
        {
            throw new InvalidDataException(
                "The Real Ruins snapshot root is not <snapshot>.");
        }

        string externalId = Path.GetFileNameWithoutExtension(sourceFile.Name);
        int originX = IntAttribute(root, "x", "originX");
        int originZ = IntAttribute(root, "z", "originZ");
        int width = IntAttribute(root, "width");
        int height = IntAttribute(root, "height");
        int mapSize = IntAttribute(root, "mapSize");
        int? inGameYear = NullableIntAttribute(root, "inGameYear", "year");
        XElement? world = root.Element("world");

        List<PlacedThingRecord> placedThings = new();
        List<TerrainCellRecord> terrainCells = new();
        List<CellRecord> roofCells = new();
        List<CellRecord> occupiedEvidence = new();
        int snapshotPawnCount = 0;
        int snapshotCorpseCount = 0;

        foreach (XElement cellElement in root.Elements("cell"))
        {
            int x = originX + IntAttribute(cellElement, "x");
            int z = originZ + IntAttribute(cellElement, "z");
            CellRecord position = new() { X = x, Z = z };

            XElement? terrain = cellElement.Element("terrain");
            if (terrain != null)
            {
                terrainCells.Add(new TerrainCellRecord
                {
                    Def = Attribute(terrain, "def"),
                    Position = position
                });
                occupiedEvidence.Add(position);
            }
            if (cellElement.Element("roof") != null)
            {
                roofCells.Add(position);
                occupiedEvidence.Add(position);
            }

            foreach (XElement item in cellElement.Elements("item"))
            {
                placedThings.Add(new PlacedThingRecord
                {
                    Def = Attribute(item, "def"),
                    Position = position,
                    Rotation = Attribute(item, "rot"),
                    Stuff = NullIfEmpty(Attribute(item, "stuffDef")),
                    StackCount = NullableIntAttribute(item, "stackCount"),
                    ActsAsWall = BoolAttribute(item, "actsAsWall"),
                    IsDoor = BoolAttribute(item, "isDoor")
                });
                occupiedEvidence.Add(position);
            }

            snapshotPawnCount += cellElement.Elements("pawn").Count();
            snapshotCorpseCount += cellElement.Elements("corpse").Count();
        }

        BoundsRecord? evidenceBounds = Bounds(occupiedEvidence);
        Dictionary<string, int> placedDefCounts = CountsByDef(
            placedThings.Select(thing => thing.Def));
        Dictionary<string, int> terrainDefCounts = CountsByDef(
            terrainCells.Select(cell => cell.Def));
        long captureArea = (long)Math.Max(0, width) * Math.Max(0, height);
        int evidenceCellCount = occupiedEvidence
            .DistinctBy(cell => (cell.X, cell.Z)).Count();

        MapLayoutRecord map = new()
        {
            MapId = Attribute(world, "gameId"),
            Size = new MapSize
            {
                X = mapSize > 0 ? mapSize : width,
                Z = mapSize > 0 ? mapSize : height
            },
            TileId = NullIfEmpty(Attribute(world, "tile", "tileId")),
            InferredBiome = NullIfEmpty(Attribute(root, "biomeDef", "biome")),
            CaptureOrigin = new CellRecord { X = originX, Z = originZ },
            CaptureSize = new MapSize { X = width, Z = height },
            Population = new PopulationContextRecord
            {
                SnapshotPawnCount = snapshotPawnCount,
                SnapshotCorpseCount = snapshotCorpseCount,
                PlayerPawnDefs = new Dictionary<string, int>(
                    StringComparer.Ordinal),
                IdeologyCounts = new Dictionary<string, int>(
                    StringComparer.Ordinal)
            },
            BuiltEntities = new List<BuiltEntityRecord>(),
            PlacedThings = placedThings,
            TerrainCells = terrainCells,
            RoofCells = roofCells,
            Zones = new List<ZoneRecord>(),
            Derived = new DerivedLayoutRecord
            {
                PlacedThingCount = placedThings.Count,
                TerrainCellCount = terrainCells.Count,
                RoofCellCount = roofCells.Count,
                EvidenceBounds = evidenceBounds,
                EvidenceDensity = captureArea == 0
                    ? 0
                    : Math.Round((double)evidenceCellCount / captureArea, 6),
                BuiltDefCounts = new Dictionary<string, int>(
                    StringComparer.Ordinal),
                PlacedDefCounts = placedDefCounts,
                TerrainDefCounts = terrainDefCounts,
                ZoneClassCounts = new Dictionary<string, int>(
                    StringComparer.Ordinal)
            }
        };

        return new SaveLayoutRecord
        {
            Schema = Program.SchemaVersion,
            Source = new SaveSourceRecord
            {
                Kind = "real-ruins-blueprint",
                FileName = sourceFile.Name,
                ByteLength = sourceFile.Length,
                Sha256 = sha256,
                Complete = complete,
                RecoveredTruncation = recoveredTruncation,
                ExternalId = externalId,
                SourceReference = BucketRoot + externalId + ".bp"
            },
            Game = new GameContextRecord
            {
                Version = Attribute(root, "version"),
                InGameYear = inGameYear,
                WorldSeed = NullIfEmpty(Attribute(world, "seed")),
                PlanetCoverage = NullableDoubleAttribute(world,
                    "percentage", "coverage"),
                ModIds = new List<string>(),
                ModNames = new List<string>()
            },
            PlayerFactionReferences = new List<string>(),
            Maps = new List<MapLayoutRecord> { map }
        };
    }

    private static string ReadCompressedXml(string path)
    {
        using FileStream source = File.OpenRead(path);
        using GZipStream gzip = new(source, CompressionMode.Decompress);
        using StreamReader reader = new(gzip);
        return reader.ReadToEnd();
    }

    private static Dictionary<string, int> CountsByDef(
        IEnumerable<string> definitions)
    {
        return definitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition))
            .GroupBy(definition => definition, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.Ordinal);
    }

    private static BoundsRecord? Bounds(IEnumerable<CellRecord> cells)
    {
        List<CellRecord> list = cells.ToList();
        if (list.Count == 0) return null;
        int minX = list.Min(cell => cell.X);
        int maxX = list.Max(cell => cell.X);
        int minZ = list.Min(cell => cell.Z);
        int maxZ = list.Max(cell => cell.Z);
        return new BoundsRecord
        {
            MinX = minX,
            MaxX = maxX,
            MinZ = minZ,
            MaxZ = maxZ,
            Width = maxX - minX + 1,
            Height = maxZ - minZ + 1,
            Area = (long)(maxX - minX + 1) * (maxZ - minZ + 1)
        };
    }

    private static int IntAttribute(XElement? element,
        params string[] names)
    {
        return NullableIntAttribute(element, names) ?? 0;
    }

    private static int? NullableIntAttribute(XElement? element,
        params string[] names)
    {
        string value = Attribute(element, names);
        return int.TryParse(value, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : null;
    }

    private static double? NullableDoubleAttribute(XElement? element,
        params string[] names)
    {
        string value = Attribute(element, names);
        return double.TryParse(value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : null;
    }

    private static bool BoolAttribute(XElement element, string name)
    {
        string value = Attribute(element, name);
        return value == "1" || bool.TryParse(value, out bool parsed) && parsed;
    }

    private static string Attribute(XElement? element,
        params string[] names)
    {
        if (element == null) return string.Empty;
        foreach (string name in names)
        {
            string? value = element.Attribute(name)?.Value;
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return string.Empty;
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
