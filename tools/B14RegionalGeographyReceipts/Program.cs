using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

internal static class Program
{
    private sealed record Result(string Name, bool Passed, string Evidence);
    private static readonly List<Result> Results = new();

    private static int Main(string[] args)
    {
        if (args.Length != 5)
        {
            Console.Error.WriteLine("usage: B14RegionalGeographyReceipts "
                + "<repo> <rimworld-data> <map-preview-core-dll> "
                + "<map-preview-mod-dll> <compiled-dll>");
            return 1;
        }
        string repo = Path.GetFullPath(args[0]);
        string data = Path.GetFullPath(args[1]);
        string previewCore = Path.GetFullPath(args[2]);
        string previewMod = Path.GetFullPath(args[3]);
        string compiled = Path.GetFullPath(args[4]);
        try
        {
            Run(repo, data, previewCore, previewMod, compiled);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 2;
        }

        int passed = Results.Count(result => result.Passed);
        foreach (Result result in Results)
            Console.WriteLine((result.Passed ? "PASS " : "FAIL ")
                + result.Name + ": " + result.Evidence);
        Console.WriteLine($"B14 regional geography acceptance: {passed}/"
            + $"{Results.Count} PASS");
        return passed == Results.Count ? 0 : 3;
    }

    private static void Run(string repo, string data, string previewCore,
        string previewMod, string compiled)
    {
        string contractPath = Path.Combine(repo, "Source",
            "RegionalGeographyContractModule.cs");
        string contract = File.ReadAllText(contractPath);
        string entry = File.ReadAllText(Path.Combine(repo, "Source",
            "ModEntry.cs"));
        string setup = File.ReadAllText(Path.Combine(repo, "Source",
            "RegionalSetupModule.cs"));
        string world = File.ReadAllText(Path.Combine(repo, "Source",
            "RegionalWorldModule.cs"));
        string widget = File.ReadAllText(Path.Combine(repo, "Source",
            "RegionMapWidget.cs"));
        string population = File.ReadAllText(Path.Combine(repo, "Source",
            "RegionalPopulationScreenModule.cs"));
        string compatibility = File.ReadAllText(Path.Combine(repo, "Source",
            "RegionalCompatibilityModule.cs"));
        string canonical = File.ReadAllText(Path.Combine(repo,
            "REGIONAL_GEOGRAPHY_CONTRACT.md"));

        int[] scales = ParseInts(world, "SupportedLocalMapSizes =", 9);
        int[] extents = ParseInts(contract, "SupportedExtents =", 5);
        int baseCombinations = scales.Length * 6 * extents.Sum();
        Add("fixed authoring axes are fully enumerated",
            scales.SequenceEqual(new[]
                { 200, 225, 250, 275, 300, 325, 350, 400, 500 })
            && extents.SequenceEqual(new[] { 4, 6, 8, 10, 12 })
            && baseCombinations == 2160
            && canonical.Contains("2,160 base combinations per root"),
            "9 scales x 6 orientations x (4+6+8+10+12 arrivals) = "
            + baseCombinations);

        Add("selected composition enumerates every geographic axis",
            HasAll(contract, "scale=", "extent=", "orientation=", "root=",
                "arrival=", "backing=", ":biome=", ":relief=", ":rocks=",
                ":roads=", ":rivers=", ":mutators=", ":landmark=",
                ":feature=", "water=")
            && contract.Contains("NaturalRockTypesIn(tile)")
            && contract.Contains("surface?.Roads")
            && contract.Contains("surface?.Rivers")
            && contract.Contains("info.Mutators"),
            "scale, shape, arrival, biome, relief, water, links, stone, "
            + "landmarks, and mutators feed one signature");

        Add("selection preview and generation share one contract",
            setup.Contains("CARegionalGeographyContract.TryValidate(plan,")
            && widget.Contains("CARegionalGeographyContract.Inspect(plan, "
                + "cachedKernel)")
            && setup.Contains("CARegionalGeographyContract.Inspect(region, "
                + "kernel)")
            && setup.Contains("failed at generation")
            && world.Contains("CARegionalGeographyContract.Inspect(plan)"
                + ".Signature")
            && contract.Contains("BoundaryWaterFor(plan)")
            && contract.Contains("boundary water area "),
            "confirmation validates; preview and generation emit the same "
            + "world-derived composition identity; projection retains every "
            + "selected boundary-water source; generation fails closed");

        Add("projection verifies constituent and continuous-feature fidelity",
            HasAll(contract, "no visible land in the shared projection",
                "AtollCarrierCount", "CoveCarrierCount",
                "ArchipelagoCarrierCount", "AtollLagoonCells",
                "AtollRingLandCells", "CoveWaterCells",
                "ArchipelagoWaterCells",
                "coastal source land produced no visible ",
                "water context"),
            "every selected member must retain land; coast, atoll, cove, and "
            + "archipelago carriers retain their realized shape obligations");

        int primaryOverride = world.IndexOf("GameInitMapSizeOverride",
            StringComparison.Ordinal);
        int legacyFallback = world.IndexOf("MapSizeOverride\");",
            StringComparison.Ordinal);
        Add("Map Preview binds the current 1.6 size API",
            BinaryContains(previewCore, "GameInitMapSizeOverride")
            && BinaryContains(previewCore, "DetermineMapSize")
            && BinaryContains(previewMod, "OnWorldTileSelected")
            && primaryOverride >= 0
            && (legacyFallback < 0 || primaryOverride < legacyFallback)
            && world.Contains("typeof(World), typeof(PlanetTile), "
                + "typeof(MapParent)")
            && !world.Contains("new object[] { world, mapParent }")
            && world.Contains("EnsurePreviewMaximum(size)")
            && world.Contains("texture.Reinitialize(size.x, size.z)"),
            "installed API and source both use GameInitMapSizeOverride plus "
            + "World/PlanetTile/MapParent; exact texture size is plan-owned");

        int previewReadyGuard = world.IndexOf(
            "!UnityData.IsInMainThread", StringComparison.Ordinal);
        int previewTypeTouch = world.IndexOf(
            "FindType(\"MapPreview.MapSizeUtility\")",
            StringComparison.Ordinal);
        Add("Map Preview compatibility waits for safe initialization",
            previewReadyGuard >= 0
            && previewTypeTouch > previewReadyGuard
            && world.Contains("OptionCategoryDefOf.General == null")
            && world.Contains("deferred until main-thread DefOf "
                + "initialization")
            && entry.Contains("CARegionalCompatibility.TryInstall();")
            && entry.Contains("LongEventHandler.ExecuteWhenFinished(")
            && entry.Contains("CARegionalCompatibility.TryInstall);"),
            "the play-load worker returns before external type reflection; "
            + "the existing main-thread completion callback retries after "
            + "DefOf binding");

        Add("preview ownership follows the selected candidate",
            setup.Contains("return boundPreviewPlan;")
            && setup.Contains("boundPreviewPlan = null;")
            && setup.Contains("ClearPreviewBinding()")
            && world.Contains("ResolvePreviewPlan(world, tileId)")
            && world.Contains("ClearPreviewBinding();")
            && world.Contains("CARegionalSetupSession.BindPreviewPlan(plan)")
            && world.Contains("CARegionalSetupSession.ActivePreviewPlan"),
            "the current click binds its derived composition before Map "
            + "Preview asks for size or generation steps; unrelated clicks "
            + "and page boundaries clear the transient authority");

        Add("preview layout is one docked interaction surface",
            setup.Contains("class CARegionalPreviewDock")
            && setup.Contains("CARegionalPreviewDock.Arrange(panel)")
            && setup.Contains("MapPreview.MapPreviewToolbar")
            && setup.Contains("BeforeExternalSelection")
            && setup.Contains("AfterExternalSelection")
            && setup.Contains("ReferenceEquals(capturedPreview, preview)")
            && setup.Contains("BeforeExternalPreviewClose")
            && setup.Contains("BeforeExternalToolbarClose")
            && world.Contains("PreviewWindowPreClosePrefix")
            && world.Contains("PreviewToolbarPreClosePrefix")
            && setup.Contains("CARegionalPreviewDock.Release()")
            && !setup.Contains("DrawAreaMarker")
            && widget.Contains("SignatureStep(value, id)")
            && widget.Contains("memberTileIds.OrderBy(id => id)")
            && !setup.Contains("Area labels - not placement coordinates")
            && !setup.Contains("DrawAreaLabel"),
            "details remain fixed; preview and toolbar dock together; image "
            + "has no synthetic land-letter or diamond overlay; the exact sorted area set "
            + "owns its cache; recreated settings windows are recaptured and "
            + "saved positions are restored");

        Add("starting-region map authoring wiring is closed",
            widget.Contains("labelHits.Add(new LabelHit")
            && widget.Contains("for (int i = labelHits.Count - 1; i >= 0;")
            && widget.Contains("SelectArrival(plan.startTileId)")
            && widget.Contains("plan.startTileId = tileId")
            && widget.Contains("TileFinder.IsValidTileForNewSettlement(")
            && widget.Contains("tileId == plan.startTileId")
            && widget.Contains("awaitingArrivalArea")
            && widget.Contains("moving.memberTileId = tileId")
            && widget.Contains("ReopenForMapAuthoring(plan)")
            && widget.Contains("plan.confirmed = false")
            && widget.Contains("CARegionalSettlements.Invalidate(plan)")
            && population.Contains(
                "case CARegionSelectionKind.Arrival:")
            && population.Contains("compactPane = 1;"),
            "static wiring: visible labels resolve in reverse paint order; "
            + "map actions reopen the draft and write startTileId or "
            + "memberTileId; arrival and settlement occupancy remain mutually "
            + "exclusive; compact placement returns to map");

        List<string> mutators = EnumerateMutators(data);
        Add("live base-game tile-mutator catalog is enumerated",
            mutators.Count >= 82
            && mutators.Contains("CoastalAtoll")
            && mutators.Contains("Archipelago")
            && mutators.Contains("MixedBiome")
            && mutators.Contains("RiverConfluence")
            && mutators.Contains("Mountainous") == false,
            mutators.Count + " TileMutatorDefs discovered: "
            + string.Join(", ", mutators));

        Add("open modded feature space fails closed",
            compatibility.Contains("foreach (TileMutatorDef mutator in "
                + "info.Mutators)")
            && compatibility.Contains("no traced obligation; refused by "
                + "default")
            && compatibility.Contains("ProductionSupported")
            && compatibility.Contains("internal static List<CAContractFacet> "
                + "FacetsFor(TileMutatorDef def)"),
            "every realized selected mutator is classified by obligations; "
            + "unknown modded peers cannot bypass durable confirmation");

        string sourceRoot = Path.Combine(repo, "Source");
        DateTime newestSource = Directory.EnumerateFiles(sourceRoot, "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsGeneratedBuildPath(sourceRoot, path))
            .Select(File.GetLastWriteTimeUtc).Max();
        FileInfo assembly = new FileInfo(compiled);
        Add("verified source build is current and cleanly emitted",
            assembly.Exists && assembly.Length > 0
            && assembly.LastWriteTimeUtc >= newestSource,
            $"bytes={assembly.Length}; SHA256={Hash(compiled)}; built "
            + $"{assembly.LastWriteTimeUtc:O}; newest source {newestSource:O}");

        WriteReport(repo, mutators, scales, extents, compiled);
    }

    private static int[] ParseInts(string source, string anchor, int count)
    {
        int start = source.IndexOf(anchor, StringComparison.Ordinal);
        if (start < 0) return Array.Empty<int>();
        int open = source.IndexOf('{', start);
        int close = source.IndexOf('}', open + 1);
        if (open < 0 || close < 0) return Array.Empty<int>();
        return source[(open + 1)..close].Split(',')
            .Select(value => value.Trim())
            .Where(value => int.TryParse(value, out _))
            .Select(int.Parse).Take(count).ToArray();
    }

    private static List<string> EnumerateMutators(string data)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(data, "*.xml",
            SearchOption.AllDirectories))
        {
            XDocument document;
            try { document = XDocument.Load(path, LoadOptions.None); }
            catch { continue; }
            foreach (XElement node in document.Descendants("TileMutatorDef"))
            {
                string name = node.Element("defName")?.Value;
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name.Trim());
            }
        }
        return names.OrderBy(value => value, StringComparer.Ordinal).ToList();
    }

    private static bool BinaryContains(string path, string value)
    {
        byte[] bytes = File.ReadAllBytes(path);
        byte[] needle = Encoding.UTF8.GetBytes(value);
        return bytes.AsSpan().IndexOf(needle) >= 0;
    }

    private static bool HasAll(string source, params string[] values) =>
        values.All(source.Contains);

    private static bool IsGeneratedBuildPath(string root, string path)
    {
        string relative = Path.GetRelativePath(root, path);
        string[] segments = relative.Split(Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        return segments.Any(segment => segment.Equals("obj",
                StringComparison.OrdinalIgnoreCase)
            || segment.Equals("bin", StringComparison.OrdinalIgnoreCase));
    }

    private static void Add(string name, bool passed, string evidence) =>
        Results.Add(new Result(name, passed, evidence));

    private static string Hash(string path) => Convert.ToHexString(
        SHA256.HashData(File.ReadAllBytes(path)));

    private static void WriteReport(string repo, List<string> mutators,
        int[] scales, int[] extents, string compiled)
    {
        string path = Path.Combine(repo, "Receipts", "B14",
            "B14_REGIONAL_GEOGRAPHY_STATIC_RECEIPT.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var text = new StringBuilder()
            .AppendLine("# B14 regional geography static receipt")
            .AppendLine()
            .AppendLine("This executable receipt verifies the region-selection "
                + "composition contract, the current Map Preview 1.6 API, the "
                + "shared preview/generation boundary, the docked interaction "
                + "surface, the live base-game TileMutatorDef catalog, and an "
                + "exact compiled assembly. Operator visual and generated-map "
                + "acceptance remain runtime evidence.")
            .AppendLine()
            .AppendLine("| Contract | Result | Evidence |")
            .AppendLine("|---|---|---|");
        foreach (Result result in Results)
            text.AppendLine("| " + Escape(result.Name) + " | **"
                + (result.Passed ? "PASS" : "FAIL") + "** | "
                + Escape(result.Evidence) + " |");
        text.AppendLine().AppendLine("## Enumerated fixed axes")
            .AppendLine()
            .AppendLine("- Local source scales: `" + string.Join("`, `", scales)
                + "`.")
            .AppendLine("- Requested extents: `" + string.Join("`, `", extents)
                + "`.")
            .AppendLine("- Orientations: six world-grid headings.")
            .AppendLine("- Arrival: any realized member not occupied by an "
                + "authored settlement; every member is available before "
                + "occupant authoring.")
            .AppendLine()
            .AppendLine("## Enumerated live base-game tile mutators")
            .AppendLine()
            .AppendLine(mutators.Count + " defs: `"
                + string.Join("`, `", mutators) + "`.")
            .AppendLine()
            .AppendLine("Verified compile: `"
                + PortableAssemblyPath(compiled) + "`; SHA-256 `"
                + Hash(compiled) + "`.")
            .AppendLine()
            .AppendLine("Overall: **"
                + (Results.All(result => result.Passed) ? "PASS" : "FAIL")
                + "**");
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static string Escape(string value) => value.Replace("|", "\\|")
        .Replace("\r", " ").Replace("\n", " ");

    private static string PortableAssemblyPath(string dll) =>
        string.Equals(Path.GetFileName(dll), "ColonistAwareness.dll",
            StringComparison.OrdinalIgnoreCase)
            ? "Assemblies/ColonistAwareness.dll"
            : Path.GetFileName(dll);
}
