using System.Text.Json;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0) return Usage();
            string command = args[0].ToLowerInvariant();
            Dictionary<string, string> options = ParseOptions(args.Skip(1));
            switch (command)
            {
                case "import":
                    Import(options);
                    break;
                case "catalog":
                    Catalog(Required(options, "root"));
                    break;
                case "inspect":
                    Inspect(options);
                    break;
                case "compose":
                    Compose(options);
                    break;
                case "search":
                    Search(options);
                    break;
                case "render":
                    Render(options);
                    break;
                case "topology":
                    BuildTopology(options);
                    break;
                default:
                    return Usage();
            }
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(exception.StackTrace);
            return 2;
        }
    }

    private static void Import(Dictionary<string, string> options)
    {
        AtlasManifest manifest = SaveWorldImporter.Import(
            Required(options, "save"), Required(options, "out"),
            Required(options, "game-root"),
            message => Console.Error.WriteLine("[atlas] " + message));
        WriteJson(manifest, options.GetValueOrDefault("receipt"));
    }

    private static void Catalog(string root)
    {
        var manifests = new List<AtlasManifest>();
        if (Directory.Exists(root))
            foreach (string file in Directory.EnumerateFiles(root,
                         "manifest.json", SearchOption.AllDirectories))
                try { manifests.Add(AtlasManifest.Load(
                    Path.GetDirectoryName(file)!)); } catch { }
        WriteJson(manifests.Select(manifest => new
        {
            manifest.WorldIdentity,
            manifest.GameVersion,
            manifest.TileCount,
            manifest.SourceSaveName,
            manifest.SourceSaveSha256,
            manifest.ImportedUtc,
            atlasDirectory = manifest.AtlasDirectory,
            manifest.UnresolvedObligations
        }).ToList(), null);
    }

    private static void Inspect(Dictionary<string, string> options)
    {
        WorldAtlas atlas = WorldAtlas.Load(Required(options, "atlas"));
        WriteJson(atlas.Inspect(Int(options, "tile")),
            options.GetValueOrDefault("out"));
    }

    private static void Compose(Dictionary<string, string> options)
    {
        WorldAtlas atlas = WorldAtlas.Load(Required(options, "atlas"));
        AtlasComposition composition = CandidateEngine.Compose(atlas,
            Int(options, "root"), Int(options, "extent"),
            Int(options, "orientation", 0), OptionalInt(options, "arrival"),
            OptionalInt(options, "map-size"));
        WriteJson(composition, options.GetValueOrDefault("out"));
    }

    private static void Search(Dictionary<string, string> options)
    {
        WorldAtlas atlas = WorldAtlas.Load(Required(options, "atlas"));
        AtlasSearchIntent intent = AtlasSearchIntent.Load(
            Required(options, "intent"));
        List<AtlasSearchResult> results = CandidateEngine.Search(atlas, intent,
            Int(options, "limit", 8));
        WriteJson(results, options.GetValueOrDefault("out"));
    }

    private static void Render(Dictionary<string, string> options)
    {
        WorldAtlas atlas = WorldAtlas.Load(Required(options, "atlas"));
        AtlasComposition composition = JsonSerializer
            .Deserialize<AtlasComposition>(File.ReadAllText(
                Required(options, "composition")), JsonOptions.Default)
            ?? throw new InvalidDataException("Composition is empty.");
        SvgRenderer.Render(atlas, composition, Required(options, "out"));
        Console.WriteLine(Path.GetFullPath(Required(options, "out")));
    }

    private static void BuildTopology(Dictionary<string, string> options)
    {
        int subdivisions = Int(options, "subdivisions", 10);
        PlanetTopology topology = PlanetTopology.BuildFullSurface(subdivisions,
            message => Console.Error.WriteLine("[topology] " + message));
        string output = Required(options, "out");
        topology.Save(output);
        WriteJson(new { topology.Subdivisions, topology.TileCount,
            file = Path.GetFullPath(output) }, null);
    }

    private static void WriteJson<T>(T value, string? output)
    {
        string json = JsonSerializer.Serialize(value, JsonOptions.Indented);
        if (string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine(json);
            return;
        }
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        string temporary = output + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, output, true);
        Console.WriteLine(output);
    }

    private static Dictionary<string, string> ParseOptions(
        IEnumerable<string> arguments)
    {
        string[] values = arguments.ToArray();
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < values.Length; i++)
        {
            string current = values[i];
            if (!current.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Expected an option, got " + current);
            string name = current[2..];
            if (i + 1 >= values.Length || values[i + 1].StartsWith("--",
                    StringComparison.Ordinal)) result[name] = "true";
            else result[name] = values[++i];
        }
        return result;
    }

    private static string Required(Dictionary<string, string> options,
        string name) => options.TryGetValue(name, out string? value)
            ? value : throw new ArgumentException("Missing --" + name + ".");

    private static int Int(Dictionary<string, string> options, string name,
        int? fallback = null)
    {
        if (!options.TryGetValue(name, out string? value))
            return fallback ?? throw new ArgumentException("Missing --" + name);
        return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int? OptionalInt(Dictionary<string, string> options,
        string name) => options.TryGetValue(name, out string? value)
        ? int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)
        : null;

    private static int Usage()
    {
        Console.Error.WriteLine("CAOfflineWorldAuthoring commands: import, "
            + "catalog, inspect, compose, search, render, topology");
        return 1;
    }
}
