using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace ColonistAwareness.Tools.PlayerBaseLayoutExtractor;

// THE MISSING MIDDLE OF THE AUTONOMOUS-BUILDING CONTRACT.
//
//   native placement validity -> LEARNED SPATIAL RELATIONSHIPS -> candidate
//   ranking
//
// This miner turns the governed layout corpus into conditional, bounded,
// non-reconstructive relationship evidence: for each relationship the
// corpus contract names (trip chains, protection of food and power, hazard
// separation, medical access from defense, usable circulation, envelopes,
// expansion reserves, alignment rhythm) it measures one value per layout
// and aggregates quantile bands per condition bucket (biome group, cohort
// scale quartile). No layout is copied; no single quality scalar exists;
// a band with fewer than the minimum supporting layouts is suppressed.
//
// Lineage safety: evidence is mined from the TRAIN partition only, using
// the clean manifest's own lineage-keyed partition assignment. The
// EVALUATE mode scores the held-out TEST partition against an existing
// evidence file and reports per-relationship coverage, which is the
// readback that says whether the bands generalize at all.
internal static class RelationshipMiner
{
    private const int MinimumBucketLayouts = 30;

    internal static int Run(string[] args)
    {
        string? manifestPath = null, cachePath = null, outPath = null,
            evidencePath = null, partition = null;
        bool evaluate = string.Equals(args[0], "relationships-evaluate",
            StringComparison.OrdinalIgnoreCase);
        for (int i = 1; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--manifest": manifestPath = args[++i]; break;
                case "--cache": cachePath = args[++i]; break;
                case "--out": outPath = args[++i]; break;
                case "--evidence": evidencePath = args[++i]; break;
                case "--partition": partition = args[++i]; break;
            }
        }
        partition ??= evaluate ? "test" : "train";
        if (manifestPath == null || cachePath == null || outPath == null
            || (evaluate && evidencePath == null))
        {
            Console.Error.WriteLine(
                "usage: relationships --manifest <clean-manifest.json> "
                + "--cache <dir-with-bp> --out <dir> [--partition train]\n"
                + "       relationships-evaluate --manifest <...> --cache "
                + "<...> --evidence <evidence.json> --out <dir> "
                + "[--partition test]");
            return 2;
        }

        CleanManifest manifest = JsonSerializer.Deserialize<CleanManifest>(
            File.ReadAllText(manifestPath), ManifestJson)
            ?? throw new InvalidDataException("empty manifest");
        List<CleanManifestEntry> rows = manifest.Entries
            .Where(entry => string.Equals(entry.Partition, partition,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        Console.WriteLine("partition " + partition + ": " + rows.Count
            + " of " + manifest.Entries.Count + " manifest rows");

        var perLayout = new List<LayoutMeasures>();
        int parsed = 0, missing = 0, failed = 0;
        foreach (CleanManifestEntry row in rows)
        {
            string bp = Path.Combine(cachePath, row.ExternalId + ".bp");
            if (!File.Exists(bp)) { missing++; continue; }
            try
            {
                SaveLayoutRecord record =
                    RealRuinsBlueprintExtractor.Extract(bp);
                MapLayoutRecord? map = record.Maps.FirstOrDefault();
                if (map == null) { failed++; continue; }
                LayoutMeasures measures = Measure(map, row);
                perLayout.Add(measures);
                parsed++;
                if (parsed % 250 == 0)
                    Console.WriteLine("  measured " + parsed);
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine("failed\t" + row.ExternalId + "\t"
                    + exception.GetType().Name);
            }
        }
        Console.WriteLine("measured " + parsed + "; missing " + missing
            + "; failed " + failed);

        Directory.CreateDirectory(outPath);
        if (!evaluate)
        {
            EvidenceFile evidence = Aggregate(perLayout, partition, parsed);
            evidence.ScaleQuartileThresholds = ScaleThresholds(rows);
            string file = Path.Combine(outPath,
                "spatial-relationship-evidence.json");
            File.WriteAllText(file,
                JsonSerializer.Serialize(evidence, OutputJson));
            // The runtime asset: same aggregate, XML, because the game
            // parses XML natively and carries no JSON reader.
            string xml = Path.Combine(outPath, "spatial-relationships.xml");
            WriteXml(evidence, xml);
            Console.WriteLine("evidence written\t" + file + "\t"
                + evidence.Relationships.Count + " relationships\t+ "
                + xml);
            return 0;
        }

        EvidenceFile trained = JsonSerializer.Deserialize<EvidenceFile>(
            File.ReadAllText(evidencePath!), OutputJson)
            ?? throw new InvalidDataException("empty evidence");
        return Holdout(perLayout, trained, partition, outPath);
    }

    // ---- per-layout measurement -----------------------------------------

    private sealed class LayoutMeasures
    {
        public required string BiomeGroup { get; init; }
        public required string ScaleQuartile { get; init; }
        public Dictionary<string, double> Values { get; } = new();
    }

    private static LayoutMeasures Measure(MapLayoutRecord map,
        CleanManifestEntry row)
    {
        var measures = new LayoutMeasures
        {
            BiomeGroup = BiomeGroup(row.Biome),
            ScaleQuartile = row.RelativeScaleQuartile ?? "Q?"
        };

        List<PlacedThingRecord> things = map.PlacedThings;
        var roofed = new HashSet<(int, int)>(
            map.RoofCells.Select(c => (c.X, c.Z)));

        List<(int x, int z)> foodPrep = Positions(things, IsFoodPrep);
        List<(int x, int z)> foodCold = Positions(things, IsFoodCold);
        List<(int x, int z)> storage = Positions(things, IsStorage);
        List<(int x, int z)> beds = Positions(things, IsBed);
        List<(int x, int z)> medical = Positions(things, IsMedical);
        List<(int x, int z)> power = Positions(things, IsPower);
        List<(int x, int z)> hazard = Positions(things, IsHazardPower);
        List<(int x, int z)> defense = Positions(things, IsDefense);
        List<(int x, int z)> dining = Positions(things, IsDining);
        List<(int x, int z)> doors = Positions(things, t => t.IsDoor);
        var occupied = new HashSet<(int, int)>(
            things.Select(t => (t.Position.X, t.Position.Z)));
        var walls = new HashSet<(int, int)>(things
            .Where(t => t.ActsAsWall && !t.IsDoor)
            .Select(t => (t.Position.X, t.Position.Z)));

        List<(int x, int z)> crop = map.Zones
            .Where(zone => zone.Class.Contains("Growing",
                StringComparison.OrdinalIgnoreCase))
            .SelectMany(zone => zone.Cells)
            .Select(c => (c.X, c.Z)).ToList();
        List<(int x, int z)> stockpileGround = map.Zones
            .Where(zone => zone.Class.Contains("Stockpile",
                StringComparison.OrdinalIgnoreCase))
            .SelectMany(zone => zone.Cells)
            .Select(c => (c.X, c.Z)).ToList();
        List<(int x, int z)> anyStorage = storage.Concat(stockpileGround)
            .ToList();

        void chain(string key, List<(int x, int z)> from,
            List<(int x, int z)> to, int sampleCap = 200)
        {
            if (from.Count == 0 || to.Count == 0) return;
            List<double> distances = Sample(from, sampleCap)
                .Select(p => (double)NearestChebyshev(p, to))
                .Where(d => d >= 0).ToList();
            if (distances.Count > 0)
                measures.Values[key] = Median(distances);
        }

        chain("foodprep-to-cold", foodPrep, foodCold);
        chain("foodprep-to-dining", foodPrep, dining);
        chain("crop-to-storage", crop, anyStorage);
        chain("bed-to-dining", beds, dining);
        chain("bed-to-foodprep", beds, foodPrep);
        chain("medical-to-defense", medical, defense);
        chain("hazard-to-bed", hazard, beds);

        if (power.Count > 0)
            measures.Values["power-protected"] = power.Count(p =>
                roofed.Contains(p)) / (double)power.Count;
        if (foodCold.Count + anyStorage.Count > 0)
        {
            List<(int x, int z)> foodStores =
                foodCold.Concat(storage).ToList();
            if (foodStores.Count > 0)
                measures.Values["food-protected"] = foodStores.Count(p =>
                    roofed.Contains(p)) / (double)foodStores.Count;
        }

        BoundsRecord? bounds = map.Derived.EvidenceBounds;
        if (bounds != null && bounds.Area > 0)
        {
            if (defense.Count > 0)
            {
                double half = Math.Max(1,
                    Math.Min(bounds.Width, bounds.Height) / 2.0);
                List<double> edge = defense.Select(p => (double)Math.Min(
                        Math.Min(p.x - bounds.MinX, bounds.MaxX - p.x),
                        Math.Min(p.z - bounds.MinZ, bounds.MaxZ - p.z)))
                    .Select(d => Math.Clamp(d / half, 0, 1)).ToList();
                measures.Values["defense-edge-affinity"] = Median(edge);
            }
            measures.Values["expansion-reserve"] = Math.Clamp(
                1.0 - occupied.Count / (double)bounds.Area, 0, 1);
            measures.Values["envelope-density"] = Math.Clamp(
                occupied.Count / (double)bounds.Area, 0, 1);
        }

        if (doors.Count >= 3)
        {
            int straight = doors.Count(d =>
                (!Blocked(d.x + 1, d.z, walls, occupied)
                    && !Blocked(d.x - 1, d.z, walls, occupied))
                || (!Blocked(d.x, d.z + 1, walls, occupied)
                    && !Blocked(d.x, d.z - 1, walls, occupied)));
            measures.Values["door-passability"] =
                straight / (double)doors.Count;
        }

        var furniture = things.Where(t => !t.ActsAsWall && !t.IsDoor
                && (IsDining(t) || IsBed(t) || IsStorage(t)))
            .GroupBy(t => t.Def)
            .Where(g => g.Count() >= 3).ToList();
        if (furniture.Count > 0)
        {
            int aligned = 0, total = 0;
            foreach (var group in furniture)
            {
                var xs = group.GroupBy(t => t.Position.X)
                    .ToDictionary(g => g.Key, g => g.Count());
                var zs = group.GroupBy(t => t.Position.Z)
                    .ToDictionary(g => g.Key, g => g.Count());
                foreach (PlacedThingRecord t in group)
                {
                    total++;
                    if (xs[t.Position.X] >= 3 || zs[t.Position.Z] >= 3)
                        aligned++;
                }
            }
            if (total > 0)
                measures.Values["samedef-alignment"] =
                    aligned / (double)total;
        }
        return measures;
    }

    private static bool Blocked(int x, int z, HashSet<(int, int)> walls,
        HashSet<(int, int)> occupied)
    {
        return walls.Contains((x, z));
    }

    private static List<(int x, int z)> Positions(
        List<PlacedThingRecord> things, Func<PlacedThingRecord, bool> pick)
    {
        return things.Where(pick)
            .Select(t => (t.Position.X, t.Position.Z)).ToList();
    }

    private static IEnumerable<(int x, int z)> Sample(
        List<(int x, int z)> cells, int cap)
    {
        if (cells.Count <= cap) return cells;
        int stride = cells.Count / cap;
        return cells.Where((_, index) => index % stride == 0).Take(cap);
    }

    private static int NearestChebyshev((int x, int z) from,
        List<(int x, int z)> to)
    {
        int best = int.MaxValue;
        foreach ((int x, int z) in to)
        {
            int d = Math.Max(Math.Abs(x - from.x), Math.Abs(z - from.z));
            if (d < best) best = d;
        }
        return best == int.MaxValue ? -1 : best;
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        int n = values.Count;
        return n % 2 == 1 ? values[n / 2]
            : (values[n / 2 - 1] + values[n / 2]) / 2.0;
    }

    // ---- functional classification (def-name evidence, mod-tolerant) ----

    private static bool Has(PlacedThingRecord t, params string[] needles)
    {
        foreach (string needle in needles)
            if (t.Def.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static bool IsFoodPrep(PlacedThingRecord t) =>
        Has(t, "stove", "butcher", "campfire", "kitchen", "brewery");
    private static bool IsFoodCold(PlacedThingRecord t) =>
        Has(t, "cooler", "freezer", "fridge");
    private static bool IsStorage(PlacedThingRecord t) =>
        Has(t, "shelf", "rack") && !t.ActsAsWall;
    private static bool IsBed(PlacedThingRecord t) =>
        Has(t, "bed") && !Has(t, "animal", "hospital");
    private static bool IsMedical(PlacedThingRecord t) =>
        Has(t, "hospitalbed", "vitalsmonitor");
    private static bool IsPower(PlacedThingRecord t) =>
        Has(t, "generator", "battery", "solar", "windturbine",
            "geothermal", "watermill");
    private static bool IsHazardPower(PlacedThingRecord t) =>
        Has(t, "battery", "chemfuel", "generator");
    private static bool IsDefense(PlacedThingRecord t) =>
        Has(t, "turret", "sandbag", "barricade", "embrasure", "mortar");
    private static bool IsDining(PlacedThingRecord t) =>
        (Has(t, "table") && !Has(t, "butcher", "stonecutter", "machining",
            "tailor", "smithy", "sculpting", "drug", "research", "loading",
            "billiard"))
        || Has(t, "chair", "stool");

    private static string BiomeGroup(string? biome)
    {
        if (string.IsNullOrEmpty(biome)) return "Unknown";
        if (biome.Contains("Tropical", StringComparison.OrdinalIgnoreCase))
            return "Tropical";
        if (biome.Contains("Temperate", StringComparison.OrdinalIgnoreCase))
            return "Temperate";
        if (biome.Contains("Desert", StringComparison.OrdinalIgnoreCase)
            || biome.Contains("Arid", StringComparison.OrdinalIgnoreCase))
            return "Arid";
        if (biome.Contains("Boreal", StringComparison.OrdinalIgnoreCase)
            || biome.Contains("Tundra", StringComparison.OrdinalIgnoreCase)
            || biome.Contains("Ice", StringComparison.OrdinalIgnoreCase)
            || biome.Contains("Cold", StringComparison.OrdinalIgnoreCase))
            return "Cold";
        return "Other";
    }

    // ---- aggregation ------------------------------------------------------

    private static EvidenceFile Aggregate(List<LayoutMeasures> layouts,
        string partition, int parsed)
    {
        string[] keys = layouts.SelectMany(l => l.Values.Keys)
            .Distinct().OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var relationships = new List<RelationshipEvidence>();
        foreach (string key in keys)
        {
            var bands = new Dictionary<string, EvidenceBand>(
                StringComparer.Ordinal);
            void band(string bucket, IEnumerable<LayoutMeasures> pool)
            {
                List<double> values = pool
                    .Where(l => l.Values.ContainsKey(key))
                    .Select(l => l.Values[key]).ToList();
                if (values.Count < MinimumBucketLayouts) return;
                values.Sort();
                bands[bucket] = new EvidenceBand
                {
                    N = values.Count,
                    P10 = Quantile(values, 0.10),
                    P25 = Quantile(values, 0.25),
                    P50 = Quantile(values, 0.50),
                    P75 = Quantile(values, 0.75),
                    P90 = Quantile(values, 0.90)
                };
            }
            band("all", layouts);
            foreach (var biome in layouts.GroupBy(l => l.BiomeGroup))
                band("biome:" + biome.Key, biome);
            foreach (var scale in layouts.GroupBy(l => l.ScaleQuartile))
                band("scale:" + scale.Key, scale);
            foreach (var joint in layouts.GroupBy(l =>
                l.BiomeGroup + "|" + l.ScaleQuartile))
                band("biome:" + joint.First().BiomeGroup + "|scale:"
                    + joint.First().ScaleQuartile, joint);
            if (bands.Count > 0)
                relationships.Add(new RelationshipEvidence
                {
                    Key = key,
                    Bands = bands
                });
        }
        return new EvidenceFile
        {
            Schema = 1,
            BuiltUtc = DateTime.UtcNow.ToString("O"),
            SourceCohort = "PB-RR-BROAD-20260817-V1",
            Partition = partition,
            LayoutsMeasured = parsed,
            MinimumBucketLayouts = MinimumBucketLayouts,
            EvidenceBoundary = "Aggregate quantile bands only; no layout "
                + "coordinates, identities, or reconstructable geometry. "
                + "Bands rank otherwise-valid candidates and never gate "
                + "validity.",
            Relationships = relationships
        };
    }

    private static double Quantile(List<double> sorted, double q)
    {
        if (sorted.Count == 0) return 0;
        double position = (sorted.Count - 1) * q;
        int low = (int)Math.Floor(position);
        int high = (int)Math.Ceiling(position);
        if (low == high) return sorted[low];
        return sorted[low] + (sorted[high] - sorted[low]) * (position - low);
    }

    // Cohort-relative scale is defined by placed-thing count; the runtime
    // needs the partition's own quartile cut points to place a current
    // settlement into the same conditioning frame.
    private static List<int> ScaleThresholds(List<CleanManifestEntry> rows)
    {
        List<double> counts = rows.Select(row =>
            (double)row.PlacedThingCount).OrderBy(v => v).ToList();
        return new List<int>
        {
            (int)Math.Round(Quantile(counts, 0.25)),
            (int)Math.Round(Quantile(counts, 0.50)),
            (int)Math.Round(Quantile(counts, 0.75))
        };
    }

    private static void WriteXml(EvidenceFile evidence, string path)
    {
        var root = new XElement("spatialRelationshipEvidence",
            new XAttribute("schema", evidence.Schema),
            new XAttribute("builtUtc", evidence.BuiltUtc),
            new XAttribute("sourceCohort", evidence.SourceCohort),
            new XAttribute("partition", evidence.Partition),
            new XAttribute("layoutsMeasured", evidence.LayoutsMeasured),
            new XAttribute("minimumBucketLayouts",
                evidence.MinimumBucketLayouts),
            new XAttribute("scaleQuartileThresholds", string.Join(",",
                evidence.ScaleQuartileThresholds ?? new List<int>())),
            new XElement("evidenceBoundary", evidence.EvidenceBoundary));
        foreach (RelationshipEvidence relationship in
            evidence.Relationships)
        {
            var element = new XElement("relationship",
                new XAttribute("key", relationship.Key));
            foreach (KeyValuePair<string, EvidenceBand> pair in
                relationship.Bands.OrderBy(p => p.Key,
                    StringComparer.Ordinal))
                element.Add(new XElement("band",
                    new XAttribute("bucket", pair.Key),
                    new XAttribute("n", pair.Value.N),
                    new XAttribute("p10", Math.Round(pair.Value.P10, 4)),
                    new XAttribute("p25", Math.Round(pair.Value.P25, 4)),
                    new XAttribute("p50", Math.Round(pair.Value.P50, 4)),
                    new XAttribute("p75", Math.Round(pair.Value.P75, 4)),
                    new XAttribute("p90", Math.Round(pair.Value.P90, 4))));
            root.Add(element);
        }
        new XDocument(root).Save(path);
    }

    // ---- holdout evaluation ----------------------------------------------

    private static int Holdout(List<LayoutMeasures> layouts,
        EvidenceFile trained, string partition, string outPath)
    {
        var lines = new List<string>();
        var report = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (RelationshipEvidence relationship in trained.Relationships)
        {
            int inside = 0, total = 0;
            foreach (LayoutMeasures layout in layouts)
            {
                if (!layout.Values.TryGetValue(relationship.Key,
                    out double value)) continue;
                EvidenceBand band = Band(relationship, layout);
                total++;
                if (value >= band.P10 && value <= band.P90) inside++;
            }
            if (total == 0) continue;
            double coverage = inside / (double)total;
            report[relationship.Key] = new
            {
                n = total,
                coverageP10P90 = Math.Round(coverage, 4)
            };
            lines.Add(relationship.Key + "\tn=" + total + "\tcoverage "
                + coverage.ToString("F3"));
        }
        string file = Path.Combine(outPath,
            "spatial-relationship-holdout-" + partition + ".json");
        File.WriteAllText(file, JsonSerializer.Serialize(new
        {
            schema = 1,
            partition,
            layouts = layouts.Count,
            expectation = "an 80% band should hold roughly 0.8 of held-out "
                + "layouts; large deviations mean the bands do not "
                + "generalize and must not ship",
            relationships = report
        }, OutputJson));
        Console.WriteLine(string.Join("\n", lines));
        Console.WriteLine("holdout written\t" + file);
        return 0;
    }

    private static EvidenceBand Band(RelationshipEvidence relationship,
        LayoutMeasures layout)
    {
        return BandFor(relationship, layout.BiomeGroup,
            layout.ScaleQuartile);
    }

    internal static EvidenceBand BandFor(RelationshipEvidence relationship,
        string biomeGroup, string scaleQuartile)
    {
        if (relationship.Bands.TryGetValue("biome:" + biomeGroup
                + "|scale:" + scaleQuartile, out EvidenceBand? joint)
            && joint != null) return joint;
        if (relationship.Bands.TryGetValue("biome:" + biomeGroup,
                out EvidenceBand? biome) && biome != null) return biome;
        if (relationship.Bands.TryGetValue("scale:" + scaleQuartile,
                out EvidenceBand? scale) && scale != null) return scale;
        return relationship.Bands["all"];
    }

    private static readonly JsonSerializerOptions ManifestJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions OutputJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

internal sealed class CleanManifest
{
    public int Schema { get; init; }
    public required List<CleanManifestEntry> Entries { get; init; }
}

internal sealed class CleanManifestEntry
{
    public required string ExternalId { get; init; }
    public string? LineageSha256 { get; init; }
    public required string Partition { get; init; }
    public string? RelativeScaleQuartile { get; init; }
    public string? Biome { get; init; }
    public int PlacedThingCount { get; init; }
}

internal sealed class EvidenceFile
{
    public int Schema { get; init; }
    public string BuiltUtc { get; init; } = string.Empty;
    public string SourceCohort { get; init; } = string.Empty;
    public string Partition { get; init; } = string.Empty;
    public int LayoutsMeasured { get; init; }
    public int MinimumBucketLayouts { get; init; }
    public string EvidenceBoundary { get; init; } = string.Empty;
    public List<int>? ScaleQuartileThresholds { get; set; }
    public required List<RelationshipEvidence> Relationships { get; init; }
}

internal sealed class RelationshipEvidence
{
    public required string Key { get; init; }
    public required Dictionary<string, EvidenceBand> Bands { get; init; }
}

internal sealed class EvidenceBand
{
    public int N { get; init; }
    public double P10 { get; init; }
    public double P25 { get; init; }
    public double P50 { get; init; }
    public double P75 { get; init; }
    public double P90 { get; init; }
}
