using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class CandidateEngine
{
    internal static AtlasComposition Compose(WorldAtlas atlas, int rootTileId,
        int extent, int orientation, int? arrivalTileId = null,
        int? localMapSize = null)
    {
        if (extent is not (4 or 6 or 8 or 10 or 12))
            throw new ArgumentOutOfRangeException(nameof(extent),
                "CA extent must be 4, 6, 8, 10, or 12.");
        if (!atlas.IsAvailable(rootTileId))
            throw new InvalidOperationException($"Root tile {rootTileId} is "
                + "blocked or occupied.");
        int normalizedOrientation = ((orientation % 6) + 6) % 6;
        var result = new List<int> { rootTileId };
        var chosen = new HashSet<int> { rootTileId };
        var frontier = new HashSet<int>();
        AddFrontier(rootTileId);
        int sequenceLength = Math.Max(8, extent);
        while (result.Count < sequenceLength && frontier.Count > 0)
        {
            int next = frontier.OrderByDescending(tile =>
                    SelectedNeighborCount(atlas, tile, chosen))
                .ThenBy(tile => normalizedOrientation == 0
                    ? CompactSpan(atlas, rootTileId, result, tile)
                    : HeadingDistance(atlas, rootTileId, tile,
                        normalizedOrientation * 60f))
                .ThenBy(tile => normalizedOrientation == 0
                    ? Heading(atlas, rootTileId, tile)
                    : CompactSpan(atlas, rootTileId, result, tile))
                .ThenBy(tile => tile).First();
            frontier.Remove(next);
            if (!chosen.Add(next)) continue;
            result.Add(next);
            AddFrontier(next);
        }
        List<int> members = result.Take(extent).ToList();
        int arrival = arrivalTileId.HasValue
                && members.Contains(arrivalTileId.Value)
            ? arrivalTileId.Value : rootTileId;
        List<AtlasTileFact> facts = members.Select(atlas.Inspect).ToList();
        var unresolved = new List<string>(atlas.Manifest.UnresolvedObligations);
        foreach (AtlasTileFact fact in facts.Where(item =>
                     !item.DefinitionComplete))
            unresolved.Add($"tile {fact.TileId} has unresolved definitions");
        if (members.Count != extent)
            unresolved.Add($"requested {extent} areas but only "
                + $"{members.Count} valid connected areas were available");
        string signatureMaterial = atlas.Manifest.WorldIdentity + "|"
            + rootTileId + "|" + extent + "|" + normalizedOrientation + "|"
            + arrival + "|" + string.Join(",", members) + "|"
            + string.Join(";", facts.Select(FactSignature));
        bool compositionEvidenceComplete = unresolved.Count == 0;
        var stageBlockers = new List<string>();
        if (!compositionEvidenceComplete)
            stageBlockers.Add("saved-world composition evidence is incomplete");
        // This atlas stops at a reviewed geographic draft. CA's current
        // production model remains the serialization authority. The
        // application gate must also resolve NaturalRockTypesIn for every
        // member and reproduce the production composition signature.
        stageBlockers.Add("the creator-to-plan save step is not connected yet");
        stageBlockers.Add("stone types still require CA's production "
            + "validation");
        return new AtlasComposition
        {
            WorldIdentity = atlas.Manifest.WorldIdentity,
            SourceSaveSha256 = atlas.Manifest.SourceSaveSha256,
            RootTileId = rootTileId,
            RequestedExtent = extent,
            RealizedExtent = members.Count,
            Orientation = normalizedOrientation,
            ArrivalTileId = arrival,
            LocalMapSize = localMapSize,
            MemberTileIds = members,
            Tiles = facts,
            AtlasEvidenceSignature = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(signatureMaterial))),
            CompositionEvidenceComplete = compositionEvidenceComplete,
            StageReady = stageBlockers.Count == 0,
            EvidenceObligations = unresolved.Distinct().ToList(),
            StageBlockers = stageBlockers.Distinct().ToList()
        };

        void AddFrontier(int from)
        {
            foreach (int neighbor in atlas.Topology.NeighborsOf(from))
                if (!chosen.Contains(neighbor) && atlas.IsAvailable(neighbor))
                    frontier.Add(neighbor);
        }
    }

    internal static List<AtlasSearchResult> Search(WorldAtlas atlas,
        AtlasSearchIntent intent, int limit)
    {
        int extent = intent.Extent == 0 ? 12 : intent.Extent;
        IEnumerable<int> roots = Enumerable.Range(0, atlas.TileCount)
            .Where(atlas.IsAvailable);
        if (intent.RootTileIds.Count > 0)
            roots = intent.RootTileIds.Where(atlas.IsAvailable);
        else if (intent.CompositionTerms.Count > 0)
            roots = CandidateRootsForCompositionTerms(atlas,
                intent.CompositionTerms, extent);
        var scored = new List<AtlasSearchResult>();
        foreach (int root in roots)
        {
            AtlasTileFact rootFact = atlas.Inspect(root);
            if (!AllTermsMatch(rootFact.SearchTerms(), intent.RootTerms))
                continue;
            if (!AnyTermGroupMatches(rootFact.SearchTerms(),
                    intent.BiomeTerms)) continue;
            for (int orientation = intent.Orientation.HasValue
                     ? intent.Orientation.Value : 0;
                 orientation <= (intent.Orientation.HasValue
                     ? intent.Orientation.Value : 5); orientation++)
            {
                AtlasComposition composition;
                try
                {
                    composition = Compose(atlas, root, extent, orientation,
                        localMapSize: intent.LocalMapSize);
                }
                catch
                {
                    continue;
                }
                IEnumerable<string> compositionTerms = composition.Tiles
                    .SelectMany(tile => tile.SearchTerms());
                if (!AllTermsMatch(compositionTerms,
                        intent.CompositionTerms)) continue;
                float score = 100f;
                score += ScoreTerms(rootFact.SearchTerms(),
                    intent.PreferredRootTerms, 12f);
                score += ScoreTerms(compositionTerms,
                    intent.PreferredCompositionTerms, 5f);
                score += composition.RealizedExtent == extent ? 10f : -50f;
                score += composition.CompositionEvidenceComplete ? 5f : 0f;
                if (rootFact.Feature != null) score += 1f;
                scored.Add(new AtlasSearchResult
                {
                    Score = score,
                    Composition = composition,
                    MatchSummary = MatchSummary(composition, intent)
                });
            }
        }
        // Location discovery returns sites, not six copies of one site. Shape
        // remains a later authoring decision; the representative below is the
        // strongest orientation for the stated intent and can be turned after
        // the operator selects the root.
        return scored.GroupBy(item => item.Composition.RootTileId)
            .Select(group => group.OrderByDescending(item => item.Score)
                .ThenBy(item => item.Composition.Orientation).First())
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Composition.RootTileId)
            .Take(Math.Max(1, limit)).ToList();
    }

    private static bool AllTermsMatch(IEnumerable<string> values,
        IReadOnlyList<string> required)
    {
        string[] normalized = values.Select(Normalize).Distinct().ToArray();
        return required.All(term => normalized.Any(value =>
            value.Contains(Normalize(term), StringComparison.Ordinal)));
    }

    // A required composition fact can only be selected when its carrier is
    // within the maximum connected reach of the root. Multi-source expansion
    // finds that conservative root set before building six candidate shapes
    // per site. The final composition test remains authoritative.
    private static IEnumerable<int> CandidateRootsForCompositionTerms(
        WorldAtlas atlas, IReadOnlyList<string> required, int extent)
    {
        string[] terms = required.Select(Normalize).Distinct().ToArray();
        HashSet<int>? candidates = null;
        foreach (string term in terms)
        {
            var carriers = new HashSet<int>();
            for (int tile = 0; tile < atlas.TileCount; tile++)
            {
                if (!atlas.IsAvailable(tile)) continue;
                if (atlas.Inspect(tile).SearchTerms().Select(Normalize)
                    .Any(value => value.Contains(term,
                        StringComparison.Ordinal))) carriers.Add(tile);
            }
            if (carriers.Count == 0) return Array.Empty<int>();

            var reachable = new HashSet<int>(carriers);
            var frontier = new HashSet<int>(carriers);
            for (int depth = 1; depth < extent && frontier.Count > 0; depth++)
            {
                var next = new HashSet<int>();
                foreach (int tile in frontier)
                    foreach (int neighbor in atlas.Topology.NeighborsOf(tile))
                        if (atlas.IsAvailable(neighbor)
                            && reachable.Add(neighbor)) next.Add(neighbor);
                frontier = next;
            }
            if (candidates == null) candidates = reachable;
            else candidates.IntersectWith(reachable);
            if (candidates.Count == 0) return Array.Empty<int>();
        }
        return candidates ?? Enumerable.Range(0, atlas.TileCount)
            .Where(atlas.IsAvailable).ToHashSet();
    }

    private static bool AnyTermGroupMatches(IEnumerable<string> values,
        IReadOnlyList<string> alternatives)
    {
        if (alternatives.Count == 0) return true;
        string[] normalized = values.Select(Normalize).Distinct().ToArray();
        return alternatives.Any(term => normalized.Any(value =>
            value.Contains(Normalize(term), StringComparison.Ordinal)));
    }

    private static float ScoreTerms(IEnumerable<string> values,
        IReadOnlyList<string> preferred, float points)
    {
        string[] normalized = values.Select(Normalize).Distinct().ToArray();
        return preferred.Count(term => normalized.Any(value =>
            value.Contains(Normalize(term), StringComparison.Ordinal))) * points;
    }

    private static string MatchSummary(AtlasComposition composition,
        AtlasSearchIntent intent)
    {
        AtlasTileFact root = composition.Tiles[0];
        var parts = new List<string>
        {
            root.Biome.Label,
            root.Hilliness,
            $"{composition.RealizedExtent} connected areas"
        };
        if (root.Feature != null) parts.Add(root.Feature.Name.Length > 0
            ? root.Feature.Name : root.Feature.DefName);
        parts.AddRange(composition.Tiles.SelectMany(tile => tile.Mutators)
            .Select(item => item.Label).Distinct().Take(5));
        return string.Join("; ", parts.Where(part => part.Length > 0));
    }

    private static int SelectedNeighborCount(WorldAtlas atlas, int tile,
        HashSet<int> selected)
    {
        int count = 0;
        foreach (int neighbor in atlas.Topology.NeighborsOf(tile))
            if (selected.Contains(neighbor)) count++;
        return count;
    }

    private static float HeadingDistance(WorldAtlas atlas, int root,
        int candidate, float desired) => MathF.Abs(DeltaAngle(desired,
            Heading(atlas, root, candidate)));

    private static float CompactSpan(WorldAtlas atlas, int root,
        List<int> selected, int candidate)
    {
        Vector3 origin = Vector3.Normalize(atlas.Topology.Centers[root]);
        Vector3 axisX = Vector3.Cross(Vector3.UnitY, origin);
        if (axisX.LengthSquared() < 0.0001f)
            axisX = Vector3.Cross(Vector3.UnitX, origin);
        axisX = Vector3.Normalize(axisX);
        Vector3 axisY = Vector3.Normalize(Vector3.Cross(origin, axisX));
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (int tile in selected.Append(candidate))
        {
            Vector3 center = atlas.Topology.Centers[tile];
            float x = Vector3.Dot(center, axisX);
            float y = Vector3.Dot(center, axisY);
            minX = MathF.Min(minX, x); maxX = MathF.Max(maxX, x);
            minY = MathF.Min(minY, y); maxY = MathF.Max(maxY, y);
        }
        float width = maxX - minX;
        float height = maxY - minY;
        return MathF.Max(width, height) * 1000f + width * height;
    }

    private static float Heading(WorldAtlas atlas, int from, int to)
    {
        if (from == to) return 0f;
        Vector3 root = atlas.Topology.Centers[from];
        Vector3 target = atlas.Topology.Centers[to];
        Vector3 normal = Vector3.Normalize(root);
        Vector3 north = new(0f, atlas.Manifest.SurfaceRadius == 0f
            ? 100f : atlas.Manifest.SurfaceRadius, 0f);
        Vector3 forward = TangentFacing(normal, north);
        Vector3 right = Vector3.Normalize(Vector3.Cross(normal, north));
        Vector3 targetForward = TangentFacing(normal, target);
        float dot = Math.Clamp(Vector3.Dot(forward, targetForward), -1f, 1f);
        float angle = MathF.Acos(dot) * 180f / MathF.PI;
        if (Vector3.Dot(targetForward, right) < 0f) angle = 360f - angle;
        return angle;
    }

    private static Vector3 TangentFacing(Vector3 normal, Vector3 target)
    {
        Vector3 tangent = target - Vector3.Dot(target, normal) * normal;
        return tangent.LengthSquared() < 0.000001f
            ? Vector3.UnitZ : Vector3.Normalize(tangent);
    }

    private static float DeltaAngle(float current, float target)
    {
        float delta = Repeat(target - current, 360f);
        if (delta > 180f) delta -= 360f;
        return delta;
    }

    private static float Repeat(float value, float length) => Math.Clamp(
        value - MathF.Floor(value / length) * length, 0f, length);

    private static string Normalize(string value) => new string(value
        .Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string FactSignature(AtlasTileFact fact) => fact.TileId + ":"
        + fact.Biome.DefName + ":" + fact.HillinessValue + ":"
        + fact.Elevation + ":" + string.Join(",", fact.Mutators
            .Select(item => item.DefName).OrderBy(item => item)) + ":"
        + (fact.Feature?.UniqueId.ToString() ?? "-");
}

internal sealed class AtlasSearchIntent
{
    public List<string> BiomeTerms { get; set; } = new();
    public List<string> RootTerms { get; set; } = new();
    public List<string> CompositionTerms { get; set; } = new();
    public List<string> PreferredRootTerms { get; set; } = new();
    public List<string> PreferredCompositionTerms { get; set; } = new();
    public List<int> RootTileIds { get; set; } = new();
    public int Extent { get; set; } = 12;
    public int? Orientation { get; set; }
    public int? LocalMapSize { get; set; }

    internal static AtlasSearchIntent Load(string path) =>
        JsonSerializer.Deserialize<AtlasSearchIntent>(File.ReadAllText(path),
            JsonOptions.Default) ?? throw new InvalidDataException(
                "Search intent is empty.");
}

internal sealed class AtlasComposition
{
    public string WorldIdentity { get; set; } = "";
    public string SourceSaveSha256 { get; set; } = "";
    public int RootTileId { get; set; }
    public int RequestedExtent { get; set; }
    public int RealizedExtent { get; set; }
    public int Orientation { get; set; }
    public int ArrivalTileId { get; set; }
    public int? LocalMapSize { get; set; }
    public List<int> MemberTileIds { get; set; } = new();
    public List<AtlasTileFact> Tiles { get; set; } = new();
    public string AtlasEvidenceSignature { get; set; } = "";
    public bool CompositionEvidenceComplete { get; set; }
    public bool StageReady { get; set; }
    public List<string> EvidenceObligations { get; set; } = new();
    public List<string> StageBlockers { get; set; } = new();
}

internal sealed class AtlasSearchResult
{
    public float Score { get; set; }
    public string MatchSummary { get; set; } = "";
    public AtlasComposition Composition { get; set; } = new();
}
