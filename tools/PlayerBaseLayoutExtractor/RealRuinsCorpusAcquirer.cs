using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColonistAwareness.Tools.PlayerBaseLayoutExtractor;

internal static class RealRuinsCorpusAcquirer
{
    private const string ApiEndpoint =
        "https://woolstrand.art/maps/random";
    private const string BucketRoot =
        "https://realruinsv2.sfo2.digitaloceanspaces.com/";
    private const int ApiBatchLimit = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<int> AcquireAsync(int requested,
        string cacheDirectory, string outputDirectory, int maxConcurrency)
    {
        Directory.CreateDirectory(cacheDirectory);
        Directory.CreateDirectory(outputDirectory);

        using HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("ColonistAwarenessLayoutCorpus", "1.0"));

        List<RealRuinsApiRow> metadata = await FetchMetadata(client,
            requested);
        Console.WriteLine("metadata\t" + metadata.Count
            + " unique candidates");
        WriteCohortManifest(metadata, requested, outputDirectory);

        ConcurrentBag<RealRuinsScreeningEntry> entries = new();
        int processed = 0;
        ParallelOptions options = new()
        {
            MaxDegreeOfParallelism = maxConcurrency
        };
        await Parallel.ForEachAsync(metadata, options,
            async (row, cancellationToken) =>
            {
                RealRuinsScreeningEntry entry = await AcquireOne(client, row,
                    cacheDirectory, cancellationToken);
                entries.Add(entry);
                int current = Interlocked.Increment(ref processed);
                if (current == metadata.Count || current % 50 == 0)
                {
                    Console.WriteLine("progress\t" + current + "/"
                        + metadata.Count);
                }
            });

        List<RealRuinsScreeningEntry> ordered = entries
            .OrderBy(entry => entry.ExternalId, StringComparer.Ordinal)
            .ToList();
        MarkExactDuplicates(ordered);

        int downloadFailures = ordered.Count(entry =>
            !string.IsNullOrWhiteSpace(entry.Error)
            && !entry.CachedFilePresent);
        int complete = ordered.Count(entry => entry.Complete
            && string.IsNullOrWhiteSpace(entry.DuplicateOf));
        int recovered = ordered.Count(entry => entry.RecoveredTruncation);
        int parseFailures = ordered.Count(entry => entry.CachedFilePresent
            && !string.IsNullOrWhiteSpace(entry.Error));
        int exactDuplicates = ordered.Count(entry =>
            !string.IsNullOrWhiteSpace(entry.DuplicateOf));

        RealRuinsAcquisitionIndex index = new()
        {
            Schema = 1,
            Requested = requested,
            MetadataCandidates = metadata.Count,
            Source = new RealRuinsSourceContract
            {
                MetadataEndpoint = ApiEndpoint,
                SnapshotBucket = BucketRoot,
                SourceFormat = "GZip-compressed XML .bp snapshot",
                PrivacyTransform = "No pawn names, pawn XML, art text, world "
                    + "seed, or raw game ID is written to this index. Related "
                    + "checkpoints use a SHA-256 lineage key."
            },
            Summary = new RealRuinsAcquisitionSummary
            {
                CompleteUnique = complete,
                RecoveredTruncations = recovered,
                ParseFailures = parseFailures,
                DownloadFailures = downloadFailures,
                ExactDuplicates = exactDuplicates
            },
            Entries = ordered
        };
        string indexPath = Path.Combine(outputDirectory,
            "real-ruins-acquisition-index.json");
        File.WriteAllText(indexPath, JsonSerializer.Serialize(index,
            JsonOptions));

        Console.WriteLine("complete\t" + complete + " complete unique\t"
            + recovered + " recovered truncations\t" + parseFailures
            + " parse failures\t" + exactDuplicates + " duplicates\t"
            + downloadFailures + " download failures");
        Console.WriteLine("index\t" + indexPath);
        return downloadFailures == 0 ? 0 : 1;
    }

    private static void WriteCohortManifest(List<RealRuinsApiRow> metadata,
        int requested, string outputDirectory)
    {
        RealRuinsCohortManifest manifest = new()
        {
            Schema = 1,
            Requested = requested,
            Source = ApiEndpoint,
            PrivacyTransform = "The source world seed and raw game ID are "
                + "omitted. Related checkpoints share a SHA-256 lineage key.",
            Candidates = metadata.Select(row => new RealRuinsCohortCandidate
            {
                ExternalId = row.NameInBucket,
                LineageSha256 = LineageHash(row),
                Version = row.Version,
                Biome = row.Biome,
                MapSize = row.MapSize,
                CaptureWidth = row.Width,
                CaptureHeight = row.Height
            }).ToList()
        };
        string path = Path.Combine(outputDirectory,
            "real-ruins-cohort-manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest,
            JsonOptions));
        Console.WriteLine("cohort\t" + path);
    }

    private static async Task<List<RealRuinsApiRow>> FetchMetadata(
        HttpClient client, int requested)
    {
        Dictionary<string, RealRuinsApiRow> unique = new(
            StringComparer.OrdinalIgnoreCase);
        int maximumBatches = Math.Max(8,
            (int)Math.Ceiling((double)requested / ApiBatchLimit) * 4);
        for (int batch = 0;
             unique.Count < requested && batch < maximumBatches;
             batch++)
        {
            int remaining = requested - unique.Count;
            int limit = Math.Min(ApiBatchLimit, remaining);
            string json = await GetStringWithRetries(client,
                ApiEndpoint + "?limit=" + limit);
            List<RealRuinsApiRow>? rows = JsonSerializer.Deserialize<
                List<RealRuinsApiRow>>(json);
            if (rows != null)
            {
                foreach (RealRuinsApiRow row in rows)
                {
                    if (!string.IsNullOrWhiteSpace(row.NameInBucket))
                    {
                        unique.TryAdd(row.NameInBucket, row);
                    }
                }
            }
            if (unique.Count < requested)
            {
                await Task.Delay(200);
            }
        }
        if (unique.Count < requested)
        {
            throw new InvalidOperationException("Real Ruins returned only "
                + unique.Count + " unique metadata rows for a request of "
                + requested + ".");
        }
        return unique.Values
            .OrderBy(row => row.NameInBucket, StringComparer.Ordinal)
            .Take(requested)
            .ToList();
    }

    private static async Task<RealRuinsScreeningEntry> AcquireOne(
        HttpClient client, RealRuinsApiRow row, string cacheDirectory,
        CancellationToken cancellationToken)
    {
        string fileName = row.NameInBucket + ".bp";
        string cachePath = Path.Combine(cacheDirectory, fileName);
        try
        {
            if (!File.Exists(cachePath) || new FileInfo(cachePath).Length == 0)
            {
                byte[] bytes = await GetBytesWithRetries(client,
                    BucketRoot + fileName, cancellationToken);
                string partialPath = cachePath + ".partial-"
                    + Guid.NewGuid().ToString("N");
                try
                {
                    await File.WriteAllBytesAsync(partialPath, bytes,
                        cancellationToken);
                    File.Move(partialPath, cachePath, true);
                }
                finally
                {
                    if (File.Exists(partialPath)) File.Delete(partialPath);
                }
            }

            SaveLayoutRecord record = RealRuinsBlueprintExtractor.Extract(
                cachePath);
            MapLayoutRecord map = record.Maps.Single();
            return new RealRuinsScreeningEntry
            {
                ExternalId = row.NameInBucket,
                SourceSha256 = record.Source.Sha256,
                RawBytes = record.Source.ByteLength,
                CachedFilePresent = true,
                Complete = record.Source.Complete,
                RecoveredTruncation = record.Source.RecoveredTruncation,
                LineageSha256 = LineageHash(row),
                Version = record.Game.Version,
                InGameYear = record.Game.InGameYear,
                Biome = map.InferredBiome,
                MapSize = map.Size.X,
                CaptureWidth = map.CaptureSize?.X ?? 0,
                CaptureHeight = map.CaptureSize?.Z ?? 0,
                PlacedThingCount = map.Derived.PlacedThingCount,
                TerrainCellCount = map.Derived.TerrainCellCount,
                RoofCellCount = map.Derived.RoofCellCount,
                SnapshotPawnCount = map.Population.SnapshotPawnCount,
                SnapshotCorpseCount = map.Population.SnapshotCorpseCount,
                WallLikeThingCount = map.PlacedThings.Count(thing =>
                    thing.ActsAsWall),
                DoorThingCount = map.PlacedThings.Count(thing => thing.IsDoor),
                PlacedDefCounts = map.Derived.PlacedDefCounts,
                TerrainDefCounts = map.Derived.TerrainDefCounts
            };
        }
        catch (Exception exception)
        {
            return new RealRuinsScreeningEntry
            {
                ExternalId = row.NameInBucket,
                CachedFilePresent = File.Exists(cachePath),
                LineageSha256 = LineageHash(row),
                Version = row.Version,
                Biome = row.Biome,
                MapSize = row.MapSize,
                CaptureWidth = row.Width,
                CaptureHeight = row.Height,
                Error = exception.GetType().Name + ": " + exception.Message,
                PlacedDefCounts = new Dictionary<string, int>(
                    StringComparer.Ordinal),
                TerrainDefCounts = new Dictionary<string, int>(
                    StringComparer.Ordinal)
            };
        }
    }

    private static void MarkExactDuplicates(
        List<RealRuinsScreeningEntry> entries)
    {
        Dictionary<string, string> firstByHash = new(StringComparer.Ordinal);
        foreach (RealRuinsScreeningEntry entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.SourceSha256)) continue;
            if (firstByHash.TryGetValue(entry.SourceSha256,
                    out string? existing))
            {
                entry.DuplicateOf = existing;
            }
            else
            {
                firstByHash.Add(entry.SourceSha256, entry.ExternalId);
            }
        }
    }

    private static string LineageHash(RealRuinsApiRow row)
    {
        string value = row.GameId + "|" + row.Seed + "|" + row.TileId
            + "|" + row.MapSize;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            value)));
    }

    private static async Task<string> GetStringWithRetries(HttpClient client,
        string uri)
    {
        Exception? last = null;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                return await client.GetStringAsync(uri);
            }
            catch (Exception exception)
            {
                last = exception;
                int delayMilliseconds = Math.Min(8000,
                    500 * (1 << attempt)) + Random.Shared.Next(0, 251);
                await Task.Delay(delayMilliseconds);
            }
        }
        throw new HttpRequestException("Metadata request failed after 8 "
            + "backoff attempts.", last);
    }

    private static async Task<byte[]> GetBytesWithRetries(HttpClient client,
        string uri, CancellationToken cancellationToken)
    {
        Exception? last = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return await client.GetByteArrayAsync(uri, cancellationToken);
            }
            catch (Exception exception)
            {
                last = exception;
                await Task.Delay(250 * (attempt + 1), cancellationToken);
            }
        }
        throw new HttpRequestException("Snapshot download failed after retries.",
            last);
    }
}

internal sealed class RealRuinsApiRow
{
    [JsonPropertyName("nameInBucket")]
    public string NameInBucket { get; init; } = string.Empty;

    [JsonPropertyName("seed")]
    public string Seed { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("biome")]
    public string Biome { get; init; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }

    [JsonPropertyName("mapSize")]
    public int MapSize { get; init; }

    [JsonPropertyName("gameId")]
    public long GameId { get; init; }

    [JsonPropertyName("tileId")]
    public int TileId { get; init; }
}

internal sealed class RealRuinsCohortManifest
{
    public int Schema { get; init; }
    public int Requested { get; init; }
    public string Source { get; init; } = string.Empty;
    public string PrivacyTransform { get; init; } = string.Empty;
    public required List<RealRuinsCohortCandidate> Candidates { get; init; }
}

internal sealed class RealRuinsCohortCandidate
{
    public string ExternalId { get; init; } = string.Empty;
    public string LineageSha256 { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Biome { get; init; } = string.Empty;
    public int MapSize { get; init; }
    public int CaptureWidth { get; init; }
    public int CaptureHeight { get; init; }
}

internal sealed class RealRuinsAcquisitionIndex
{
    public int Schema { get; init; }
    public int Requested { get; init; }
    public int MetadataCandidates { get; init; }
    public required RealRuinsSourceContract Source { get; init; }
    public required RealRuinsAcquisitionSummary Summary { get; init; }
    public required List<RealRuinsScreeningEntry> Entries { get; init; }
}

internal sealed class RealRuinsSourceContract
{
    public string MetadataEndpoint { get; init; } = string.Empty;
    public string SnapshotBucket { get; init; } = string.Empty;
    public string SourceFormat { get; init; } = string.Empty;
    public string PrivacyTransform { get; init; } = string.Empty;
}

internal sealed class RealRuinsAcquisitionSummary
{
    public int CompleteUnique { get; init; }
    public int RecoveredTruncations { get; init; }
    public int ParseFailures { get; init; }
    public int DownloadFailures { get; init; }
    public int ExactDuplicates { get; init; }
}

internal sealed class RealRuinsScreeningEntry
{
    public string ExternalId { get; init; } = string.Empty;
    public string SourceSha256 { get; init; } = string.Empty;
    public long RawBytes { get; init; }
    public bool CachedFilePresent { get; init; }
    public bool Complete { get; init; }
    public bool RecoveredTruncation { get; init; }
    public string LineageSha256 { get; init; } = string.Empty;
    public string? DuplicateOf { get; set; }
    public string Version { get; init; } = string.Empty;
    public int? InGameYear { get; init; }
    public string? Biome { get; init; }
    public int MapSize { get; init; }
    public int CaptureWidth { get; init; }
    public int CaptureHeight { get; init; }
    public int PlacedThingCount { get; init; }
    public int TerrainCellCount { get; init; }
    public int RoofCellCount { get; init; }
    public int SnapshotPawnCount { get; init; }
    public int SnapshotCorpseCount { get; init; }
    public int WallLikeThingCount { get; init; }
    public int DoorThingCount { get; init; }
    public required Dictionary<string, int> PlacedDefCounts { get; init; }
    public required Dictionary<string, int> TerrainDefCounts { get; init; }
    public string? Error { get; init; }
}
