using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColonistAwareness.Tools.PlayerBaseLayoutExtractor;

internal static class RealRuinsCorpusProfiler
{
    private const string SnapshotBucket =
        "https://realruinsv2.sfo2.digitaloceanspaces.com/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static int Write(string acquisitionIndexPath,
        string outputDirectory)
    {
        byte[] indexBytes = File.ReadAllBytes(acquisitionIndexPath);
        RealRuinsAcquisitionIndex index = JsonSerializer.Deserialize<
                RealRuinsAcquisitionIndex>(indexBytes, JsonOptions)
            ?? throw new InvalidDataException(
                "The Real Ruins acquisition index is empty.");

        List<RealRuinsScreeningEntry> clean = index.Entries
            .Where(entry => entry.Complete
                && !entry.RecoveredTruncation
                && string.IsNullOrWhiteSpace(entry.Error)
                && string.IsNullOrWhiteSpace(entry.DuplicateOf))
            .OrderBy(entry => entry.ExternalId, StringComparer.Ordinal)
            .ToList();
        if (clean.Count == 0)
        {
            throw new InvalidDataException(
                "The acquisition index has no complete unique snapshots.");
        }

        List<int> placedCounts = clean.Select(entry => entry.PlacedThingCount)
            .Order().ToList();
        int q25 = Quantile(placedCounts, 0.25);
        int q50 = Quantile(placedCounts, 0.50);
        int q75 = Quantile(placedCounts, 0.75);
        int p90 = Quantile(placedCounts, 0.90);
        int p99 = Quantile(placedCounts, 0.99);

        List<RealRuinsCleanManifestEntry> manifestEntries = clean
            .Select(entry => new RealRuinsCleanManifestEntry
            {
                ExternalId = entry.ExternalId,
                SourceSha256 = entry.SourceSha256,
                LineageSha256 = entry.LineageSha256,
                Partition = Partition(entry.LineageSha256),
                RelativeScaleQuartile = ScaleQuartile(
                    entry.PlacedThingCount, q25, q50, q75),
                Version = entry.Version,
                Biome = entry.Biome,
                MapSize = entry.MapSize,
                CaptureWidth = entry.CaptureWidth,
                CaptureHeight = entry.CaptureHeight,
                PlacedThingCount = entry.PlacedThingCount,
                TerrainCellCount = entry.TerrainCellCount,
                RoofCellCount = entry.RoofCellCount,
                WallLikeThingCount = entry.WallLikeThingCount,
                DoorThingCount = entry.DoorThingCount
            }).ToList();

        RealRuinsCleanManifest manifest = new()
        {
            Schema = 1,
            Source = new RealRuinsProfileSource
            {
                AcquisitionIndexSha256 = Convert.ToHexString(
                    SHA256.HashData(indexBytes)),
                SnapshotBucket = SnapshotBucket,
                RequestedCandidates = index.Requested
            },
            EvidenceBoundary = "Complete native layout snapshots. No author, "
                + "pawn, research-state, progression-stage, or quality label "
                + "is inferred. Relative scale quartiles describe this cohort "
                + "only.",
            PartitionContract = "SHA-256 lineage key modulo 100: 00-79 train, "
                + "80-89 validation, 90-99 test. Every checkpoint sharing a "
                + "lineage key receives the same partition.",
            Entries = manifestEntries
        };

        RealRuinsBroadProfile profile = new()
        {
            Schema = 1,
            Source = manifest.Source,
            CandidateTotal = index.Entries.Count,
            CompleteUnique = clean.Count,
            RecoveredFragments = index.Entries.Count(entry =>
                entry.RecoveredTruncation),
            DownloadFailures = index.Entries.Count(entry =>
                !entry.CachedFilePresent),
            CompleteLineages = clean.Select(entry => entry.LineageSha256)
                .Distinct(StringComparer.Ordinal).Count(),
            BiomeCount = clean.Select(entry => entry.Biome ?? string.Empty)
                .Distinct(StringComparer.Ordinal).Count(),
            VersionCount = clean.Select(entry => entry.Version)
                .Distinct(StringComparer.Ordinal).Count(),
            PlacedThingQuantiles = new RealRuinsQuantiles
            {
                Minimum = placedCounts[0],
                P25 = q25,
                Median = q50,
                P75 = q75,
                P90 = p90,
                P99 = p99,
                Maximum = placedCounts[^1]
            },
            PartitionCounts = Counts(manifestEntries.Select(entry =>
                entry.Partition)),
            RelativeScaleQuartileCounts = Counts(manifestEntries.Select(
                entry => entry.RelativeScaleQuartile)),
            BiomeCounts = Counts(clean.Select(entry =>
                entry.Biome ?? "unspecified")),
            VersionCounts = Counts(clean.Select(entry => entry.Version))
        };

        Directory.CreateDirectory(outputDirectory);
        string manifestPath = Path.Combine(outputDirectory,
            "real-ruins-broad-clean-manifest.json");
        string profilePath = Path.Combine(outputDirectory,
            "real-ruins-broad-profile.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest,
            JsonOptions));
        File.WriteAllText(profilePath, JsonSerializer.Serialize(profile,
            JsonOptions));
        Console.WriteLine("profile\t" + clean.Count + " complete unique\t"
            + profile.BiomeCount + " biomes\t" + profile.CompleteLineages
            + " lineages");
        Console.WriteLine("manifest\t" + manifestPath);
        Console.WriteLine("summary\t" + profilePath);
        return 0;
    }

    private static int Quantile(IReadOnlyList<int> sorted, double value)
    {
        int index = (int)Math.Floor((sorted.Count - 1) * value);
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private static string ScaleQuartile(int value, int q25, int q50,
        int q75)
    {
        if (value <= q25) return "Q1";
        if (value <= q50) return "Q2";
        if (value <= q75) return "Q3";
        return "Q4";
    }

    private static string Partition(string lineageSha256)
    {
        if (lineageSha256.Length < 8)
        {
            throw new InvalidDataException(
                "A complete snapshot has no valid lineage SHA-256.");
        }
        uint prefix = uint.Parse(lineageSha256[..8],
            NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        uint bucket = prefix % 100;
        if (bucket < 80) return "train";
        if (bucket < 90) return "validation";
        return "test";
    }

    private static SortedDictionary<string, int> Counts(
        IEnumerable<string> values)
    {
        return new SortedDictionary<string, int>(values
            .GroupBy(value => value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.Ordinal), StringComparer.Ordinal);
    }
}

internal sealed class RealRuinsCleanManifest
{
    public int Schema { get; init; }
    public required RealRuinsProfileSource Source { get; init; }
    public string EvidenceBoundary { get; init; } = string.Empty;
    public string PartitionContract { get; init; } = string.Empty;
    public required List<RealRuinsCleanManifestEntry> Entries { get; init; }
}

internal sealed class RealRuinsCleanManifestEntry
{
    public string ExternalId { get; init; } = string.Empty;
    public string SourceSha256 { get; init; } = string.Empty;
    public string LineageSha256 { get; init; } = string.Empty;
    public string Partition { get; init; } = string.Empty;
    public string RelativeScaleQuartile { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string? Biome { get; init; }
    public int MapSize { get; init; }
    public int CaptureWidth { get; init; }
    public int CaptureHeight { get; init; }
    public int PlacedThingCount { get; init; }
    public int TerrainCellCount { get; init; }
    public int RoofCellCount { get; init; }
    public int WallLikeThingCount { get; init; }
    public int DoorThingCount { get; init; }
}

internal sealed class RealRuinsBroadProfile
{
    public int Schema { get; init; }
    public required RealRuinsProfileSource Source { get; init; }
    public int CandidateTotal { get; init; }
    public int CompleteUnique { get; init; }
    public int RecoveredFragments { get; init; }
    public int DownloadFailures { get; init; }
    public int CompleteLineages { get; init; }
    public int BiomeCount { get; init; }
    public int VersionCount { get; init; }
    public required RealRuinsQuantiles PlacedThingQuantiles { get; init; }
    public required SortedDictionary<string, int> PartitionCounts { get; init; }
    public required SortedDictionary<string, int>
        RelativeScaleQuartileCounts { get; init; }
    public required SortedDictionary<string, int> BiomeCounts { get; init; }
    public required SortedDictionary<string, int> VersionCounts { get; init; }
}

internal sealed class RealRuinsProfileSource
{
    public string AcquisitionIndexSha256 { get; init; } = string.Empty;
    public string SnapshotBucket { get; init; } = string.Empty;
    public int RequestedCandidates { get; init; }
}

internal sealed class RealRuinsQuantiles
{
    public int Minimum { get; init; }
    public int P25 { get; init; }
    public int Median { get; init; }
    public int P75 { get; init; }
    public int P90 { get; init; }
    public int P99 { get; init; }
    public int Maximum { get; init; }
}
