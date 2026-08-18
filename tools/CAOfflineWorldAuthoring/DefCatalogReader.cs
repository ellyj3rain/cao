using System.Globalization;
using System.Xml.Linq;

internal static class DefCatalogReader
{
    internal static AtlasDefCatalog Read(string gameRoot,
        IReadOnlyList<string> activeModIds, Action<string>? progress = null)
    {
        gameRoot = Path.GetFullPath(gameRoot);
        Dictionary<string, string> roots = DiscoverContentRoots(gameRoot);
        var rawByType = new Dictionary<string, List<RawDef>>(
            StringComparer.Ordinal)
        {
            ["BiomeDef"] = new(),
            ["TileMutatorDef"] = new(),
            ["RoadDef"] = new(),
            ["RiverDef"] = new(),
            ["ThingDef"] = new()
        };
        var abstractByName = new Dictionary<string, RawDef>(
            StringComparer.Ordinal);
        var catalog = new AtlasDefCatalog();

        foreach (string modId in activeModIds)
        {
            if (!roots.TryGetValue(modId, out string? root))
            {
                catalog.MissingModIds.Add(modId);
                continue;
            }
            progress?.Invoke("indexing definitions: " + modId);
            foreach (string file in EnumerateDefinitionFiles(root))
            {
                XDocument document;
                try
                {
                    document = XDocument.Load(file, LoadOptions.None);
                }
                catch
                {
                    continue;
                }
                XElement? defs = document.Root;
                if (defs == null || defs.Name.LocalName != "Defs") continue;
                foreach (XElement element in defs.Elements())
                {
                    string type = NormalizeType(element.Name.LocalName);
                    string? defName = ChildValue(element, "defName");
                    string? abstractName = element.Attribute("Name")?.Value;
                    var raw = new RawDef(type, defName ?? "", modId,
                        element.Attribute("ParentName")?.Value, element);
                    if (!string.IsNullOrWhiteSpace(abstractName))
                        abstractByName[abstractName] = raw;
                    if (defName == null || !rawByType.ContainsKey(type))
                        continue;
                    int existing = rawByType[type].FindIndex(item =>
                        item.DefName == defName);
                    if (existing >= 0) rawByType[type][existing] = raw;
                    else rawByType[type].Add(raw);
                }
            }
        }

        catalog.Biomes = HashCatalog(rawByType["BiomeDef"], raw =>
            BuildRecord(raw, abstractByName, isBiome: true));
        catalog.TileMutators = HashCatalog(rawByType["TileMutatorDef"], raw =>
            BuildRecord(raw, abstractByName));
        catalog.Roads = HashCatalog(rawByType["RoadDef"], raw =>
            BuildRecord(raw, abstractByName));
        catalog.Rivers = HashCatalog(rawByType["RiverDef"], raw =>
            BuildRecord(raw, abstractByName));
        catalog.NaturalRocks = rawByType["ThingDef"]
            .Where(raw => IsNonResourceNaturalRock(raw, abstractByName))
            .Select(raw => BuildRecord(raw, abstractByName, isRock: true))
            .OrderBy(record => record.DefName, StringComparer.Ordinal)
            .ToList();
        return catalog;
    }

    private static bool IsNonResourceNaturalRock(RawDef raw,
        Dictionary<string, RawDef> abstracts)
    {
        string category = ResolveValue(raw, abstracts,
            new[] { "category" }) ?? "";
        return category == "Building"
            && ResolveBool(raw, abstracts,
                new[] { "building", "isNaturalRock" })
            && !ResolveBool(raw, abstracts,
                new[] { "building", "isResourceRock" })
            && !ResolveBool(raw, abstracts,
                new[] { "building", "mineablePreventNaturalRockOnSurface" })
            && ResolveValue(raw, abstracts,
                new[] { "building", "unsmoothedThing" }) == null;
    }

    private static Dictionary<ushort, AtlasDefRecord> HashCatalog(
        IEnumerable<RawDef> raws, Func<RawDef, AtlasDefRecord> projector)
    {
        var result = new Dictionary<ushort, AtlasDefRecord>();
        var taken = new HashSet<ushort>();
        foreach (RawDef raw in raws.OrderBy(item => item.DefName,
                     StringComparer.Ordinal))
        {
            ushort hash = unchecked((ushort)(StableStringHash(raw.DefName)
                % 65535));
            while (hash == 0 || taken.Contains(hash)) hash++;
            taken.Add(hash);
            result[hash] = projector(raw);
        }
        return result;
    }

    private static AtlasDefRecord BuildRecord(RawDef raw,
        Dictionary<string, RawDef> abstracts, bool isBiome = false,
        bool isRock = false)
    {
        string label = ResolveValue(raw, abstracts, new[] { "label" })
            ?? Humanize(raw.DefName);
        return new AtlasDefRecord
        {
            DefName = raw.DefName,
            Label = label,
            Description = ResolveValue(raw, abstracts,
                new[] { "description" }) ?? "",
            SourceModId = raw.SourceModId,
            IsWaterBiome = isBiome && ResolveBool(raw, abstracts,
                new[] { "isWaterBiome" }),
            Impassable = isBiome && ResolveBool(raw, abstracts,
                new[] { "impassable" }),
            IsNaturalRock = isRock,
            BiomeSpecific = isRock && ResolveBool(raw, abstracts,
                new[] { "building", "biomeSpecific" })
        };
    }

    private static string? ResolveValue(RawDef raw,
        Dictionary<string, RawDef> abstracts, IReadOnlyList<string> path,
        HashSet<string>? visited = null)
    {
        XElement? current = raw.Element;
        foreach (string segment in path)
        {
            current = current.Element(segment);
            if (current == null) break;
        }
        if (current != null) return current.Value;
        if (string.IsNullOrWhiteSpace(raw.ParentName)) return null;
        visited ??= new HashSet<string>(StringComparer.Ordinal);
        if (!visited.Add(raw.ParentName)) return null;
        return abstracts.TryGetValue(raw.ParentName, out RawDef? parent)
            ? ResolveValue(parent, abstracts, path, visited) : null;
    }

    private static bool ResolveBool(RawDef raw,
        Dictionary<string, RawDef> abstracts, IReadOnlyList<string> path)
    {
        string? value = ResolveValue(raw, abstracts, path);
        return value != null && bool.TryParse(value, out bool parsed) && parsed;
    }

    private static Dictionary<string, string> DiscoverContentRoots(
        string gameRoot)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (string parent in ContentParents(gameRoot))
        {
            if (!Directory.Exists(parent)) continue;
            foreach (string directory in Directory.EnumerateDirectories(parent))
            {
                string about = Path.Combine(directory, "About", "About.xml");
                if (!File.Exists(about)) continue;
                try
                {
                    XDocument document = XDocument.Load(about);
                    string? packageId = document.Root?.Element("packageId")
                        ?.Value.Trim();
                    if (!string.IsNullOrWhiteSpace(packageId))
                        result[packageId] = directory;
                }
                catch
                {
                    // A malformed inactive About file does not invalidate the
                    // exact active roots that can still be resolved.
                }
            }
        }
        return result;
    }

    private static IEnumerable<string> ContentParents(string gameRoot)
    {
        yield return Path.Combine(gameRoot, "Data");
        yield return Path.Combine(gameRoot, "Mods");
        DirectoryInfo? common = Directory.GetParent(gameRoot);
        DirectoryInfo? steamApps = common?.Parent;
        if (steamApps != null)
            yield return Path.Combine(steamApps.FullName, "workshop", "content",
                "294100");
    }

    private static IEnumerable<string> EnumerateDefinitionFiles(string root)
    {
        foreach (string file in Directory.EnumerateFiles(root, "*.xml",
                     SearchOption.AllDirectories))
        {
            string normalized = file.Replace('/', '\\');
            if (normalized.Contains("\\Defs\\",
                    StringComparison.OrdinalIgnoreCase)) yield return file;
        }
    }

    private static string NormalizeType(string type)
    {
        int dot = type.LastIndexOf('.');
        return dot >= 0 ? type[(dot + 1)..] : type;
    }

    private static string? ChildValue(XElement element, string name) =>
        element.Element(name)?.Value.Trim();

    private static int StableStringHash(string value)
    {
        int hash = 23;
        foreach (char character in value)
            hash = unchecked(hash * 31 + character);
        return hash;
    }

    private static string Humanize(string value)
    {
        if (value.Length == 0) return value;
        var result = new System.Text.StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (i > 0 && char.IsUpper(character)
                && !char.IsUpper(value[i - 1])) result.Append(' ');
            result.Append(character);
        }
        return result.ToString();
    }

    private sealed record RawDef(string Type, string DefName,
        string SourceModId, string? ParentName, XElement Element);
}
