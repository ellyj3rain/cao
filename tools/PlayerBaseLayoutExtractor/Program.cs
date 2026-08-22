using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ColonistAwareness.Tools.PlayerBaseLayoutExtractor;

internal static partial class Program
{
    internal const int SchemaVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static int Main(string[] args)
    {
        if (args.Length > 0 && (string.Equals(args[0], "relationships",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(args[0], "relationships-evaluate",
                StringComparison.OrdinalIgnoreCase)))
            return RelationshipMiner.Run(args);
        if (!TryReadArguments(args, out ExtractorArguments? arguments,
                out string? argumentError))
        {
            Console.Error.WriteLine(argumentError);
            PrintUsage();
            return 2;
        }

        ExtractorArguments resolvedArguments = arguments!;
        Directory.CreateDirectory(resolvedArguments.OutputDirectory);
        if (!string.IsNullOrWhiteSpace(
                resolvedArguments.RealRuinsProfileIndex))
        {
            return RealRuinsCorpusProfiler.Write(
                resolvedArguments.RealRuinsProfileIndex,
                resolvedArguments.OutputDirectory);
        }
        if (resolvedArguments.RealRuinsAcquireCount > 0)
        {
            return RealRuinsCorpusAcquirer.AcquireAsync(
                    resolvedArguments.RealRuinsAcquireCount,
                    resolvedArguments.RealRuinsCacheDirectory!,
                    resolvedArguments.OutputDirectory,
                    resolvedArguments.MaxConcurrency)
                .GetAwaiter().GetResult();
        }
        IReadOnlyList<string> inputPaths = DiscoverInputPaths(resolvedArguments);
        if (inputPaths.Count == 0)
        {
            Console.Error.WriteLine("No .rws saves or .bp layout snapshots were found.");
            return 3;
        }

        List<LayoutIndexEntry> indexEntries = new();
        int failures = 0;
        foreach (string inputPath in inputPaths)
        {
            try
            {
                SaveLayoutRecord record = ExtractInput(inputPath);
                string outputName = SafeFileStem(record.Source.FileName)
                    + "-" + record.Source.Sha256[..12].ToLowerInvariant()
                    + ".layout.json";
                string outputPath = Path.Combine(resolvedArguments.OutputDirectory,
                    outputName);
                File.WriteAllText(outputPath,
                    JsonSerializer.Serialize(record, JsonOptions));
                indexEntries.Add(new LayoutIndexEntry
                {
                    FileName = record.Source.FileName,
                    SourceKind = record.Source.Kind,
                    Sha256 = record.Source.Sha256,
                    OutputFile = outputName,
                    MapCount = record.Maps.Count,
                    BuiltEntityCount = record.Maps.Sum(map =>
                        map.BuiltEntities.Count),
                    PlacedThingCount = record.Maps.Sum(map =>
                        map.PlacedThings.Count),
                    ZoneCount = record.Maps.Sum(map => map.Zones.Count)
                });
                Console.WriteLine("extracted\t" + record.Source.FileName
                    + "\t" + record.Source.Sha256
                    + "\t" + record.Maps.Count + " map(s)");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine("failed\t" + inputPath + "\t"
                    + exception.GetType().Name + "\t" + exception.Message);
            }
        }

        LayoutIndex index = new()
        {
            Schema = SchemaVersion,
            Entries = indexEntries.OrderBy(entry => entry.FileName,
                StringComparer.OrdinalIgnoreCase).ToList()
        };
        File.WriteAllText(Path.Combine(resolvedArguments.OutputDirectory,
                "layout-index.json"),
            JsonSerializer.Serialize(index, JsonOptions));

        Console.WriteLine("complete\t" + indexEntries.Count
            + " extracted\t" + failures + " failed");
        return failures == 0 ? 0 : 1;
    }

    private static SaveLayoutRecord ExtractInput(string inputPath)
    {
        return Path.GetExtension(inputPath).ToLowerInvariant() switch
        {
            ".rws" => ExtractRimWorldSave(inputPath),
            ".bp" => RealRuinsBlueprintExtractor.Extract(inputPath),
            _ => throw new InvalidDataException(
                "Unsupported layout source: " + inputPath)
        };
    }

    private static SaveLayoutRecord ExtractRimWorldSave(string savePath)
    {
        FileInfo sourceFile = new(savePath);
        string sha256;
        using (FileStream stream = sourceFile.OpenRead())
        {
            sha256 = Convert.ToHexString(SHA256.HashData(stream));
        }

        XDocument document = XDocument.Load(savePath, LoadOptions.None);
        XElement savegame = document.Root
            ?? throw new InvalidDataException("The save has no XML root.");
        XElement meta = savegame.Element("meta")
            ?? throw new InvalidDataException("The save has no meta element.");
        XElement game = savegame.Element("game")
            ?? throw new InvalidDataException("The save has no game element.");

        HashSet<string> playerFactionReferences = PlayerFactionReferences(game);
        Dictionary<string, string> ideologyNames = IdeologyNames(game);
        Dictionary<string, WorldObjectRecord> worldObjects = WorldObjects(game);
        Dictionary<string, string> tileBiomes = TileBiomes(game);

        List<MapLayoutRecord> maps = new();
        XElement? mapsElement = game.Element("maps");
        if (mapsElement != null)
        {
            foreach (XElement mapElement in mapsElement.Elements("li"))
            {
                maps.Add(ExtractMap(mapElement, playerFactionReferences,
                    ideologyNames, worldObjects, tileBiomes));
            }
        }

        return new SaveLayoutRecord
        {
            Schema = SchemaVersion,
            Source = new SaveSourceRecord
            {
                Kind = "rimworld-save",
                FileName = sourceFile.Name,
                ByteLength = sourceFile.Length,
                Sha256 = sha256,
                Complete = true
            },
            Game = new GameContextRecord
            {
                Version = Value(meta, "gameVersion"),
                TicksGame = LongValue(game, "tickManager/ticksGame"),
                Scenario = Value(game, "scenario/name"),
                ModIds = Values(meta, "modIds/li"),
                ModNames = Values(meta, "modNames/li")
            },
            PlayerFactionReferences = playerFactionReferences
                .OrderBy(reference => reference, StringComparer.Ordinal)
                .ToList(),
            Maps = maps
        };
    }

    private static MapLayoutRecord ExtractMap(XElement mapElement,
        HashSet<string> playerFactionReferences,
        Dictionary<string, string> ideologyNames,
        Dictionary<string, WorldObjectRecord> worldObjects,
        Dictionary<string, string> tileBiomes)
    {
        string parentReference = Value(mapElement, "mapInfo/parent");
        worldObjects.TryGetValue(parentReference, out WorldObjectRecord? parent);
        MapSize size = ParseMapSize(Value(mapElement, "mapInfo/size"));

        List<BuiltEntityRecord> builtEntities = new();
        Dictionary<string, int> playerPawnDefs = new(StringComparer.Ordinal);
        Dictionary<string, int> ideologyCounts = new(StringComparer.Ordinal);
        int playerPawnThings = 0;
        int ideologyBearingPlayerPawns = 0;

        XElement? thingsElement = mapElement.Element("things");
        if (thingsElement != null)
        {
            foreach (XElement thing in thingsElement.Elements("thing"))
            {
                string faction = Value(thing, "faction");
                if (!playerFactionReferences.Contains(faction))
                {
                    continue;
                }

                string className = Attribute(thing, "Class");
                string defName = Value(thing, "def");
                if (className == "Pawn")
                {
                    playerPawnThings++;
                    Increment(playerPawnDefs, defName);
                    string ideologyReference = Value(thing, "ideo/ideo");
                    if (string.IsNullOrWhiteSpace(ideologyReference))
                    {
                        XElement? directIdeo = thing.Element("ideo");
                        if (directIdeo != null && !directIdeo.HasElements)
                        {
                            ideologyReference = directIdeo.Value.Trim();
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(ideologyReference))
                    {
                        ideologyBearingPlayerPawns++;
                        ideologyNames.TryGetValue(ideologyReference,
                            out string? ideologyName);
                        Increment(ideologyCounts, ideologyName
                            ?? ideologyReference);
                    }
                }

                BuiltEntityState? state = BuiltState(className);
                if (state == null || !TryParseCell(Value(thing, "pos"),
                        out CellRecord? cell))
                {
                    continue;
                }

                builtEntities.Add(new BuiltEntityRecord
                {
                    Def = defName,
                    Class = className,
                    State = state.Value.ToString().ToLowerInvariant(),
                    Position = cell!,
                    Rotation = Value(thing, "rot"),
                    Stuff = NullIfEmpty(Value(thing, "stuff")),
                    Quality = NullIfEmpty(Value(thing, "qualityInt"))
                });
            }
        }

        List<ZoneRecord> zones = new();
        XElement? allZones = mapElement.Element("zoneManager")
            ?.Element("allZones");
        if (allZones != null)
        {
            foreach (XElement zoneElement in allZones.Elements("li"))
            {
                List<CellRecord> cells = new();
                XElement? zoneCells = zoneElement.Element("cells");
                if (zoneCells != null)
                {
                    foreach (XElement cellElement in zoneCells.Elements("li"))
                    {
                        if (TryParseCell(cellElement.Value,
                                out CellRecord? cell))
                        {
                            cells.Add(cell!);
                        }
                    }
                }
                zones.Add(new ZoneRecord
                {
                    Class = Attribute(zoneElement, "Class", "Zone"),
                    Label = NullIfEmpty(Value(zoneElement, "label")),
                    Cells = cells
                });
            }
        }

        List<CellRecord> occupiedEvidence = builtEntities
            .Select(entity => entity.Position)
            .Concat(zones.SelectMany(zone => zone.Cells))
            .ToList();
        BoundsRecord? builtAnchorBounds = Bounds(builtEntities.Select(entity =>
            entity.Position));
        BoundsRecord? completedAnchorBounds = Bounds(builtEntities
            .Where(entity => entity.State == "completed")
            .Select(entity => entity.Position));
        BoundsRecord? bounds = Bounds(occupiedEvidence);
        Dictionary<string, int> builtDefCounts = builtEntities
            .GroupBy(entity => entity.Def, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.Ordinal);
        Dictionary<string, int> zoneClassCounts = zones
            .GroupBy(zone => zone.Class, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.Ordinal);

        string tileId = parent?.TileId ?? string.Empty;
        tileBiomes.TryGetValue(tileId, out string? inferredBiome);
        return new MapLayoutRecord
        {
            MapId = Value(mapElement, "uniqueID"),
            Generator = Value(mapElement, "generatorDef"),
            Size = size,
            ParentReference = parentReference,
            SettlementName = NullIfEmpty(parent?.Name),
            TileId = NullIfEmpty(tileId),
            InferredBiome = NullIfEmpty(inferredBiome),
            Population = new PopulationContextRecord
            {
                PlayerPawnThings = playerPawnThings,
                IdeologyBearingPlayerPawns = ideologyBearingPlayerPawns,
                PlayerPawnDefs = playerPawnDefs,
                IdeologyCounts = ideologyCounts
            },
            BuiltEntities = builtEntities,
            PlacedThings = new List<PlacedThingRecord>(),
            TerrainCells = new List<TerrainCellRecord>(),
            RoofCells = new List<CellRecord>(),
            Zones = zones,
            Derived = new DerivedLayoutRecord
            {
                BuiltEntityCount = builtEntities.Count,
                CompletedEntityCount = builtEntities.Count(entity =>
                    entity.State == "completed"),
                PlannedEntityCount = builtEntities.Count(entity =>
                    entity.State != "completed"),
                PlacedThingCount = 0,
                TerrainCellCount = 0,
                RoofCellCount = 0,
                ZoneCount = zones.Count,
                ZoneCellCount = zones.Sum(zone => zone.Cells.Count),
                BuiltAnchorBounds = builtAnchorBounds,
                CompletedAnchorBounds = completedAnchorBounds,
                EvidenceBounds = bounds,
                EvidenceDensity = bounds == null || bounds.Area == 0
                    ? 0
                    : Math.Round((double)occupiedEvidence
                        .DistinctBy(cell => (cell.X, cell.Z)).Count()
                        / bounds.Area, 6),
                BuiltDefCounts = builtDefCounts,
                PlacedDefCounts = new Dictionary<string, int>(
                    StringComparer.Ordinal),
                TerrainDefCounts = new Dictionary<string, int>(
                    StringComparer.Ordinal),
                ZoneClassCounts = zoneClassCounts
            }
        };
    }

    private static HashSet<string> PlayerFactionReferences(XElement game)
    {
        HashSet<string> references = new(StringComparer.Ordinal);
        XElement? factions = game.Element("world")?.Element("factionManager")
            ?.Element("allFactions");
        if (factions == null)
        {
            return references;
        }
        foreach (XElement faction in factions.Elements("li"))
        {
            if (Value(faction, "def") != "PlayerColony")
            {
                continue;
            }
            string id = Value(faction, "loadID");
            if (string.IsNullOrWhiteSpace(id)) id = Value(faction, "ID");
            if (!string.IsNullOrWhiteSpace(id)) references.Add("Faction_" + id);
        }
        return references;
    }

    private static Dictionary<string, string> IdeologyNames(XElement game)
    {
        Dictionary<string, string> names = new(StringComparer.Ordinal);
        XElement? ideologies = game.Element("ideoManager")?.Element("ideos");
        if (ideologies == null) return names;
        foreach (XElement ideology in ideologies.Elements("li"))
        {
            string id = Value(ideology, "id");
            string name = Value(ideology, "name");
            if (!string.IsNullOrWhiteSpace(id))
            {
                names["Ideo_" + id] = string.IsNullOrWhiteSpace(name)
                    ? "Ideo_" + id
                    : name;
            }
        }
        return names;
    }

    private static Dictionary<string, WorldObjectRecord> WorldObjects(
        XElement game)
    {
        Dictionary<string, WorldObjectRecord> records = new(
            StringComparer.Ordinal);
        XElement? objects = game.Element("world")?.Element("worldObjects")
            ?.Element("worldObjects");
        if (objects == null) return records;
        foreach (XElement worldObject in objects.Elements("li"))
        {
            string id = Value(worldObject, "ID");
            if (string.IsNullOrWhiteSpace(id)) continue;
            records["WorldObject_" + id] = new WorldObjectRecord
            {
                Name = Value(worldObject, "nameInt"),
                TileId = Value(worldObject, "tile").Split(',')[0].Trim()
            };
        }
        return records;
    }

    private static Dictionary<string, string> TileBiomes(XElement game)
    {
        Dictionary<string, Dictionary<string, int>> counts = new(
            StringComparer.Ordinal);
        XElement? tales = game.Element("taleManager")?.Element("tales");
        if (tales == null) return new Dictionary<string, string>();
        foreach (XElement surroundings in tales.Descendants("surroundings"))
        {
            string tile = Value(surroundings, "tile").Split(',')[0].Trim();
            string biome = Value(surroundings, "biome");
            if (string.IsNullOrWhiteSpace(tile)
                || string.IsNullOrWhiteSpace(biome))
            {
                continue;
            }
            if (!counts.TryGetValue(tile,
                    out Dictionary<string, int>? tileCounts))
            {
                tileCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                counts.Add(tile, tileCounts);
            }
            Increment(tileCounts, biome);
        }
        return counts.ToDictionary(pair => pair.Key, pair => pair.Value
            .OrderByDescending(biome => biome.Value)
            .ThenBy(biome => biome.Key, StringComparer.Ordinal)
            .First().Key, StringComparer.Ordinal);
    }

    private static BuiltEntityState? BuiltState(string className)
    {
        if (className == "Building" || className.StartsWith("Building_",
                StringComparison.Ordinal))
        {
            return BuiltEntityState.Completed;
        }
        if (className.StartsWith("Blueprint", StringComparison.Ordinal))
        {
            return BuiltEntityState.Blueprint;
        }
        if (className == "Frame" || className.StartsWith("Frame_",
                StringComparison.Ordinal))
        {
            return BuiltEntityState.Frame;
        }
        return null;
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

    private static MapSize ParseMapSize(string value)
    {
        Match match = CellPattern().Match(value);
        if (!match.Success) return new MapSize();
        return new MapSize
        {
            X = int.Parse(match.Groups[1].Value,
                CultureInfo.InvariantCulture),
            Z = int.Parse(match.Groups[3].Value,
                CultureInfo.InvariantCulture)
        };
    }

    private static bool TryParseCell(string value, out CellRecord? cell)
    {
        Match match = CellPattern().Match(value);
        if (!match.Success)
        {
            cell = null;
            return false;
        }
        cell = new CellRecord
        {
            X = int.Parse(match.Groups[1].Value,
                CultureInfo.InvariantCulture),
            Z = int.Parse(match.Groups[3].Value,
                CultureInfo.InvariantCulture)
        };
        return true;
    }

    private static string Value(XElement element, string path)
    {
        XElement? current = element;
        foreach (string part in path.Split('/'))
        {
            current = current?.Element(part);
            if (current == null) return string.Empty;
        }
        return current.Value.Trim();
    }

    private static long? LongValue(XElement element, string path)
    {
        return long.TryParse(Value(element, path),
            NumberStyles.Integer, CultureInfo.InvariantCulture,
            out long value)
            ? value
            : null;
    }

    private static List<string> Values(XElement element, string path)
    {
        string[] parts = path.Split('/');
        IEnumerable<XElement> current = new[] { element };
        foreach (string part in parts)
        {
            current = current.SelectMany(item => item.Elements(part));
        }
        return current.Select(item => item.Value.Trim()).ToList();
    }

    private static string Attribute(XElement element, string name,
        string fallback = "")
    {
        return element.Attribute(name)?.Value.Trim() ?? fallback;
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void Increment(Dictionary<string, int> counts, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) key = "unknown";
        counts.TryGetValue(key, out int count);
        counts[key] = count + 1;
    }

    private static IReadOnlyList<string> DiscoverInputPaths(
        ExtractorArguments arguments)
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (string input in arguments.Inputs)
        {
            string fullPath = Path.GetFullPath(input);
            if (File.Exists(fullPath))
            {
                string extension = Path.GetExtension(fullPath);
                if (extension.Equals(".rws", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".bp", StringComparison.OrdinalIgnoreCase))
                {
                    paths.Add(fullPath);
                }
                continue;
            }
            if (!Directory.Exists(fullPath))
            {
                Console.Error.WriteLine("missing input\t" + fullPath);
                continue;
            }
            SearchOption option = arguments.Recursive
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;
            foreach (string savePath in Directory.EnumerateFiles(fullPath,
                         "*.rws", option))
            {
                paths.Add(Path.GetFullPath(savePath));
            }
            foreach (string blueprintPath in Directory.EnumerateFiles(fullPath,
                         "*.bp", option))
            {
                paths.Add(Path.GetFullPath(blueprintPath));
            }
        }
        return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryReadArguments(string[] args,
        out ExtractorArguments? arguments, out string? error)
    {
        arguments = null;
        error = null;
        List<string> inputs = new();
        string? output = null;
        bool recursive = false;
        int acquireRealRuins = 0;
        string? profileRealRuins = null;
        string? realRuinsCache = null;
        int maxConcurrency = 4;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--output" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--recursive":
                    recursive = true;
                    break;
                case "--acquire-real-ruins" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out acquireRealRuins)
                        || acquireRealRuins <= 0)
                    {
                        error = "--acquire-real-ruins requires a positive count.";
                        return false;
                    }
                    break;
                case "--profile-real-ruins" when i + 1 < args.Length:
                    profileRealRuins = args[++i];
                    break;
                case "--cache" when i + 1 < args.Length:
                    realRuinsCache = args[++i];
                    break;
                case "--max-concurrency" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out maxConcurrency)
                        || maxConcurrency < 1 || maxConcurrency > 12)
                    {
                        error = "--max-concurrency must be between 1 and 12.";
                        return false;
                    }
                    break;
                case "--help":
                case "-h":
                    error = "Read-only RimWorld player-base layout extractor.";
                    return false;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        error = "Unknown option: " + args[i];
                        return false;
                    }
                    inputs.Add(args[i]);
                    break;
            }
        }
        if (string.IsNullOrWhiteSpace(output))
        {
            output = Path.Combine(FindRepositoryRoot(), "Corpus",
                "PlayerBaseLayouts", "local", "generated");
        }
        if (acquireRealRuins > 0 && string.IsNullOrWhiteSpace(realRuinsCache))
        {
            realRuinsCache = Path.Combine(FindRepositoryRoot(), "Corpus",
                "PlayerBaseLayouts", ".cache", "real-ruins", "raw");
        }
        if (acquireRealRuins > 0
            && !string.IsNullOrWhiteSpace(profileRealRuins))
        {
            error = "Acquisition and profiling are separate operations.";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(profileRealRuins)
            && !File.Exists(profileRealRuins))
        {
            error = "The Real Ruins acquisition index does not exist: "
                + profileRealRuins;
            return false;
        }
        if (inputs.Count == 0 && acquireRealRuins == 0
            && string.IsNullOrWhiteSpace(profileRealRuins))
        {
            error = "At least one layout file or directory is required.";
            return false;
        }
        arguments = new ExtractorArguments
        {
            OutputDirectory = Path.GetFullPath(output),
            Inputs = inputs,
            Recursive = recursive,
            RealRuinsAcquireCount = acquireRealRuins,
            RealRuinsProfileIndex = string.IsNullOrWhiteSpace(profileRealRuins)
                ? null
                : Path.GetFullPath(profileRealRuins),
            RealRuinsCacheDirectory = string.IsNullOrWhiteSpace(realRuinsCache)
                ? null
                : Path.GetFullPath(realRuinsCache),
            MaxConcurrency = maxConcurrency
        };
        return true;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("usage: dotnet run --project "
            + "tools/PlayerBaseLayoutExtractor -- [--output <directory>] "
            + "[--recursive] <rws-bp-or-directory> [...]");
        Console.Error.WriteLine("   or: dotnet run --project "
            + "tools/PlayerBaseLayoutExtractor -- [--output <directory>] "
            + "[--cache <directory>] --acquire-real-ruins <count> "
            + "[--max-concurrency 4]");
        Console.Error.WriteLine("   or: dotnet run --project "
            + "tools/PlayerBaseLayoutExtractor -- [--output <directory>] "
            + "--profile-real-ruins <acquisition-index.json>");
        Console.Error.WriteLine("Default output and acquisition cache paths "
            + "are local, gitignored directories under "
            + "Corpus/PlayerBaseLayouts/.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            string gitPath = Path.Combine(directory.FullName, ".git");
            if ((Directory.Exists(gitPath) || File.Exists(gitPath))
                && File.Exists(Path.Combine(directory.FullName,
                    "GOVERNANCE.md")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("The repository root could not "
            + "be found. Run this tool inside the CAO checkout or provide "
            + "explicit --output and --cache paths.");
    }

    private static string SafeFileStem(string fileName)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            stem = stem.Replace(invalid, '-');
        }
        stem = Regex.Replace(stem, @"\s+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(stem) ? "rimworld-save" : stem;
    }

    [GeneratedRegex(@"^\s*\((-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\)\s*$")]
    private static partial Regex CellPattern();
}

internal sealed class ExtractorArguments
{
    public required string OutputDirectory { get; init; }
    public required List<string> Inputs { get; init; }
    public bool Recursive { get; init; }
    public int RealRuinsAcquireCount { get; init; }
    public string? RealRuinsProfileIndex { get; init; }
    public string? RealRuinsCacheDirectory { get; init; }
    public int MaxConcurrency { get; init; } = 4;
}

internal sealed class SaveLayoutRecord
{
    public int Schema { get; init; }
    public required SaveSourceRecord Source { get; init; }
    public required GameContextRecord Game { get; init; }
    public required List<string> PlayerFactionReferences { get; init; }
    public required List<MapLayoutRecord> Maps { get; init; }
}

internal sealed class SaveSourceRecord
{
    public required string Kind { get; init; }
    public required string FileName { get; init; }
    public long ByteLength { get; init; }
    public required string Sha256 { get; init; }
    public bool Complete { get; init; }
    public bool RecoveredTruncation { get; init; }
    public string? ExternalId { get; init; }
    public string? SourceReference { get; init; }
}

internal sealed class GameContextRecord
{
    public string Version { get; init; } = string.Empty;
    public long? TicksGame { get; init; }
    public int? InGameYear { get; init; }
    public string Scenario { get; init; } = string.Empty;
    public string? WorldSeed { get; init; }
    public double? PlanetCoverage { get; init; }
    public required List<string> ModIds { get; init; }
    public required List<string> ModNames { get; init; }
}

internal sealed class MapLayoutRecord
{
    public string MapId { get; init; } = string.Empty;
    public string Generator { get; init; } = string.Empty;
    public required MapSize Size { get; init; }
    public string ParentReference { get; init; } = string.Empty;
    public string? SettlementName { get; init; }
    public string? TileId { get; init; }
    public string? InferredBiome { get; init; }
    public CellRecord? CaptureOrigin { get; init; }
    public MapSize? CaptureSize { get; init; }
    public required PopulationContextRecord Population { get; init; }
    public required List<BuiltEntityRecord> BuiltEntities { get; init; }
    public required List<PlacedThingRecord> PlacedThings { get; init; }
    public required List<TerrainCellRecord> TerrainCells { get; init; }
    public required List<CellRecord> RoofCells { get; init; }
    public required List<ZoneRecord> Zones { get; init; }
    public required DerivedLayoutRecord Derived { get; init; }
}

internal sealed class MapSize
{
    public int X { get; init; }
    public int Z { get; init; }
}

internal sealed class PopulationContextRecord
{
    public int PlayerPawnThings { get; init; }
    public int IdeologyBearingPlayerPawns { get; init; }
    public int SnapshotPawnCount { get; init; }
    public int SnapshotCorpseCount { get; init; }
    public required Dictionary<string, int> PlayerPawnDefs { get; init; }
    public required Dictionary<string, int> IdeologyCounts { get; init; }
}

internal sealed class BuiltEntityRecord
{
    public string Def { get; init; } = string.Empty;
    public string Class { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public required CellRecord Position { get; init; }
    public string Rotation { get; init; } = string.Empty;
    public string? Stuff { get; init; }
    public string? Quality { get; init; }
}

internal sealed class PlacedThingRecord
{
    public string Def { get; init; } = string.Empty;
    public required CellRecord Position { get; init; }
    public string Rotation { get; init; } = string.Empty;
    public string? Stuff { get; init; }
    public int? StackCount { get; init; }
    public bool ActsAsWall { get; init; }
    public bool IsDoor { get; init; }
}

internal sealed class TerrainCellRecord
{
    public string Def { get; init; } = string.Empty;
    public required CellRecord Position { get; init; }
}

internal sealed class ZoneRecord
{
    public string Class { get; init; } = string.Empty;
    public string? Label { get; init; }
    public required List<CellRecord> Cells { get; init; }
}

internal sealed class CellRecord
{
    public int X { get; init; }
    public int Z { get; init; }
}

internal sealed class DerivedLayoutRecord
{
    public int BuiltEntityCount { get; init; }
    public int CompletedEntityCount { get; init; }
    public int PlannedEntityCount { get; init; }
    public int PlacedThingCount { get; init; }
    public int TerrainCellCount { get; init; }
    public int RoofCellCount { get; init; }
    public int ZoneCount { get; init; }
    public int ZoneCellCount { get; init; }
    public BoundsRecord? BuiltAnchorBounds { get; init; }
    public BoundsRecord? CompletedAnchorBounds { get; init; }
    public BoundsRecord? EvidenceBounds { get; init; }
    public double EvidenceDensity { get; init; }
    public required Dictionary<string, int> BuiltDefCounts { get; init; }
    public required Dictionary<string, int> PlacedDefCounts { get; init; }
    public required Dictionary<string, int> TerrainDefCounts { get; init; }
    public required Dictionary<string, int> ZoneClassCounts { get; init; }
}

internal sealed class BoundsRecord
{
    public int MinX { get; init; }
    public int MaxX { get; init; }
    public int MinZ { get; init; }
    public int MaxZ { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public long Area { get; init; }
}

internal sealed class WorldObjectRecord
{
    public string Name { get; init; } = string.Empty;
    public string TileId { get; init; } = string.Empty;
}

internal enum BuiltEntityState
{
    Completed,
    Blueprint,
    Frame
}

internal sealed class LayoutIndex
{
    public int Schema { get; init; }
    public required List<LayoutIndexEntry> Entries { get; init; }
}

internal sealed class LayoutIndexEntry
{
    public required string FileName { get; init; }
    public required string SourceKind { get; init; }
    public required string Sha256 { get; init; }
    public required string OutputFile { get; init; }
    public int MapCount { get; init; }
    public int BuiltEntityCount { get; init; }
    public int PlacedThingCount { get; init; }
    public int ZoneCount { get; init; }
}
