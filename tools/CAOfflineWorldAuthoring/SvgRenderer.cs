using System.Globalization;
using System.Numerics;
using System.Security;
using System.Text;

internal static class SvgRenderer
{
    internal static void Render(WorldAtlas atlas, AtlasComposition composition,
        string outputPath)
    {
        int root = composition.RootTileId;
        var members = composition.MemberTileIds.ToHashSet();
        var context = new HashSet<int>(members);
        var frontier = new HashSet<int>(members);
        for (int ring = 0; ring < 4; ring++)
        {
            var next = new HashSet<int>();
            foreach (int tile in frontier)
            {
                foreach (int neighbor in atlas.Topology.NeighborsOf(tile))
                {
                    if (context.Add(neighbor)) next.Add(neighbor);
                }
            }
            frontier = next;
        }

        Vector3 normal = Vector3.Normalize(atlas.Topology.Centers[root]);
        Vector3 axisX = Vector3.Cross(Vector3.UnitY, normal);
        if (axisX.LengthSquared() < 0.0001f)
            axisX = Vector3.Cross(Vector3.UnitX, normal);
        axisX = Vector3.Normalize(axisX);
        Vector3 axisY = Vector3.Normalize(Vector3.Cross(normal, axisX));
        Vector3 origin = normal * (atlas.Manifest.SurfaceRadius == 0f
            ? 100f : atlas.Manifest.SurfaceRadius);

        var polygons = new Dictionary<int, List<Vector2>>();
        foreach (int tile in context)
        {
            var polygon = new List<Vector2>();
            foreach (Vector3 vertex in atlas.Topology.PolygonOf(tile))
            {
                Vector3 offset = vertex - origin;
                polygon.Add(new Vector2(Vector3.Dot(offset, axisX),
                    Vector3.Dot(offset, axisY)));
            }
            polygons[tile] = polygon;
        }
        float minX = polygons.Values.SelectMany(item => item).Min(point => point.X);
        float maxX = polygons.Values.SelectMany(item => item).Max(point => point.X);
        float minY = polygons.Values.SelectMany(item => item).Min(point => point.Y);
        float maxY = polygons.Values.SelectMany(item => item).Max(point => point.Y);
        const float mapX = 48f, mapY = 148f, mapWidth = 820f, mapHeight = 760f;
        float scale = MathF.Min(mapWidth / MathF.Max(0.001f, maxX - minX),
            mapHeight / MathF.Max(0.001f, maxY - minY));
        Vector2 Project(Vector2 point) => new(
            mapX + (point.X - minX) * scale,
            mapY + mapHeight - (point.Y - minY) * scale);
        Dictionary<int, List<Vector2>> projectedPolygons = polygons
            .ToDictionary(pair => pair.Key,
                pair => pair.Value.Select(Project).ToList());

        AtlasTileFact rootFact = composition.Tiles.First(item =>
            item.TileId == root);
        var builder = new StringBuilder(96_000);
        builder.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" "
            + "width=\"1280\" height=\"1000\" viewBox=\"0 0 1280 1000\">");
        builder.AppendLine("<rect width=\"1280\" height=\"1000\" fill=\"#0d1518\"/>");
        builder.AppendLine("<defs>"
            + "<linearGradient id=\"ocean\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\">"
            + "<stop offset=\"0\" stop-color=\"#397f91\"/><stop offset=\"1\" stop-color=\"#123e55\"/>"
            + "</linearGradient>"
            + "<pattern id=\"rainforest\" width=\"22\" height=\"22\" patternUnits=\"userSpaceOnUse\">"
            + "<rect width=\"22\" height=\"22\" fill=\"#335e48\"/>"
            + "<circle cx=\"5\" cy=\"7\" r=\"5\" fill=\"#4c8059\"/><circle cx=\"15\" cy=\"5\" r=\"4\" fill=\"#28523e\"/>"
            + "<circle cx=\"13\" cy=\"16\" r=\"6\" fill=\"#3f704d\"/><circle cx=\"2\" cy=\"19\" r=\"3\" fill=\"#244b39\"/>"
            + "</pattern>"
            + "<pattern id=\"shrubland\" width=\"28\" height=\"22\" patternUnits=\"userSpaceOnUse\">"
            + "<rect width=\"28\" height=\"22\" fill=\"#927d50\"/>"
            + "<path d=\"M3 17l4-6 4 6M18 8l3-4 3 4\" stroke=\"#58623b\" stroke-width=\"2\" fill=\"none\"/>"
            + "<path d=\"M0 20h28\" stroke=\"#b69a61\" stroke-width=\"1\"/>"
            + "</pattern>"
            + "<pattern id=\"grassland\" width=\"20\" height=\"20\" patternUnits=\"userSpaceOnUse\">"
            + "<rect width=\"20\" height=\"20\" fill=\"#728052\"/>"
            + "<path d=\"M4 18l2-7 2 7m5 0l1-10 3 10\" stroke=\"#526b42\" fill=\"none\"/>"
            + "</pattern>"
            + "<pattern id=\"desert\" width=\"30\" height=\"18\" patternUnits=\"userSpaceOnUse\">"
            + "<rect width=\"30\" height=\"18\" fill=\"#b79058\"/>"
            + "<path d=\"M0 13Q8 7 16 13T32 13\" stroke=\"#d0ae72\" fill=\"none\"/>"
            + "</pattern>"
            + "<filter id=\"softShadow\" x=\"-30%\" y=\"-30%\" width=\"160%\" height=\"160%\">"
            + "<feDropShadow dx=\"0\" dy=\"2\" stdDeviation=\"2\" flood-color=\"#081014\" flood-opacity=\".75\"/>"
            + "</filter></defs>");
        builder.AppendLine("<style>text{font-family:Segoe UI,Arial,sans-serif}"
            + ".small{font-size:14px;fill:#aebdc6}.body{font-size:17px;fill:#dce6eb}"
            + ".title{font-size:31px;fill:#f2f5f6}.label{font-size:12px;fill:#f6fbfd;"
            + "paint-order:stroke;stroke:#10181c;stroke-width:3px;stroke-linejoin:round}"
            + ".micro{font-size:11px;fill:#dce6eb;paint-order:stroke;stroke:#10181c;stroke-width:2.5px}"
            + "</style>");
        builder.AppendLine($"<text class=\"title\" x=\"48\" y=\"48\">"
            + Escape(atlas.Manifest.WorldName) + " — "
            + Escape(LocalLabel(rootFact)) + " candidate</text>");
        builder.AppendLine($"<text class=\"body\" x=\"48\" y=\"82\">"
            + $"{composition.RealizedExtent} connected areas · orientation "
            + $"{composition.Orientation + 1}/6 · arrival "
            + $"{composition.ArrivalTileId}</text>");
        builder.AppendLine("<text class=\"small\" x=\"48\" y=\"111\">"
            + "EXACT SAVED WORLD · selected region outlined · nearby coast, biomes, relief, roads, and rivers"
            + "</text>");

        foreach (int tile in context)
        {
            AtlasTileFact fact = atlas.Inspect(tile);
            string fill = TileFill(fact);
            string points = string.Join(" ", projectedPolygons[tile]
                .Select(point => F(point.X) + "," + F(point.Y)));
            builder.Append("<polygon points=\"").Append(points)
                .Append("\" fill=\"").Append(fill)
                .AppendLine("\" stroke=\"none\"/>");
            DrawRelief(builder, fact, projectedPolygons[tile]);
        }

        foreach (int tile in members)
        {
            string points = string.Join(" ", projectedPolygons[tile]
                .Select(point => F(point.X) + "," + F(point.Y)));
            builder.AppendLine($"<polygon points=\"{points}\" fill=\"#59cde7\" "
                + "fill-opacity=\".12\" stroke=\"none\"/>");
        }
        DrawSelectionBoundary(builder, members, projectedPolygons);
        DrawLinks(builder, atlas, context, projectedPolygons);
        DrawArrivalMarker(builder, composition, projectedPolygons);
        DrawSavedGeographyCallouts(builder, atlas, members,
            projectedPolygons);

        const float panelX = 910f;
        builder.AppendLine("<rect x=\"892\" y=\"132\" width=\"348\" "
            + "height=\"828\" rx=\"5\" fill=\"#182126\" stroke=\"#52616a\"/>");
        float y = 174f;
        WriteHeading("Formation character", ref y);
        DrawFormationInset(builder, rootFact, panelX, y, 312f, 230f);
        y += 254f;
        WriteHeading("Arrival ground", ref y);
        WriteLine(rootFact.Biome.Label, ref y, "body");
        WriteLine(rootFact.Hilliness + " · " + rootFact.Elevation + "m", ref y);
        WriteLine($"{rootFact.Temperature:0.#}°C · {rootFact.Rainfall}mm rain",
            ref y);
        WriteLine($"{rootFact.Latitude:0.00}°, {rootFact.Longitude:0.00}°", ref y);
        if (rootFact.Feature != null)
            WriteLine((rootFact.Feature.Name.Length > 0 ? rootFact.Feature.Name
                : rootFact.Feature.DefName), ref y);
        foreach (AtlasDefRecord mutator in rootFact.Mutators)
            WriteLine(mutator.Label, ref y);
        y += 12f;
        WriteHeading("Selected region", ref y);
        foreach (IGrouping<string, AtlasTileFact> biomeGroup in composition.Tiles
                     .GroupBy(item => item.Biome.Label)
                     .OrderByDescending(item => item.Count()))
            WriteLine($"{biomeGroup.Count()} × {biomeGroup.Key}", ref y);
        foreach (string featureName in composition.Tiles
                     .Where(item => item.Feature != null)
                     .Select(item => item.Feature!.Name.Length > 0
                         ? item.Feature.Name : item.Feature.DefName)
                     .Distinct().Take(2)) WriteLine(featureName, ref y);
        foreach (string mutator in composition.Tiles.SelectMany(item =>
                     item.Mutators).Select(item => item.Label).Distinct()
                     .Take(5))
            WriteLine(mutator, ref y);
        y += 12f;
        WriteHeading("Authoring state", ref y);
        WriteLine(composition.CompositionEvidenceComplete
            ? "Saved-world composition resolved"
            : "Saved-world evidence gaps remain", ref y, "body");
        WriteLine(composition.StageReady ? "Ready to save as the CA plan"
            : "Not saved; plan validation remains", ref y);
        builder.AppendLine("</svg>");

        Directory.CreateDirectory(Path.GetDirectoryName(
            Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, builder.ToString());

        void WriteHeading(string text, ref float lineY)
        {
            builder.AppendLine($"<text class=\"body\" font-weight=\"600\" "
                + $"x=\"{panelX}\" y=\"{F(lineY)}\">{Escape(text)}</text>");
            lineY += 30f;
        }

        void WriteLine(string text, ref float lineY, string css = "small")
        {
            builder.AppendLine($"<text class=\"{css}\" x=\"{panelX}\" "
                + $"y=\"{F(lineY)}\">{Escape(text)}</text>");
            lineY += 22f;
        }

    }

    private static void DrawRelief(StringBuilder builder,
        AtlasTileFact fact, IReadOnlyList<Vector2> polygon)
    {
        if (fact.Biome.IsWaterBiome || polygon.Count == 0) return;
        Vector2 center = PolygonCenter(polygon);
        float width = polygon.Max(point => point.X)
            - polygon.Min(point => point.X);
        float height = polygon.Max(point => point.Y)
            - polygon.Min(point => point.Y);
        float shiftX = ((fact.TileId * 37) % 17 - 8) * width / 70f;
        float shiftY = ((fact.TileId * 53) % 13 - 6) * height / 65f;
        float x = center.X + shiftX;
        float y = center.Y + shiftY;
        if (fact.HillinessValue <= 1) return;
        if (fact.HillinessValue == 2)
        {
            builder.AppendLine($"<path d=\"M{F(x - width * .20f)} {F(y + height * .10f)}Q"
                + $"{F(x)} {F(y - height * .17f)} {F(x + width * .20f)} {F(y + height * .10f)}\" "
                + "fill=\"none\" stroke=\"#a6a077\" stroke-width=\"2\" opacity=\".72\"/>");
            return;
        }
        int peaks = fact.HillinessValue >= 4 ? 3 : 2;
        for (int i = 0; i < peaks; i++)
        {
            float offset = (i - (peaks - 1) * .5f) * width * .18f;
            float peakHeight = height * (fact.HillinessValue >= 4
                ? .28f : .20f) * (1f - i * .08f);
            float half = width * (fact.HillinessValue >= 4 ? .16f : .13f);
            builder.AppendLine($"<path d=\"M{F(x + offset - half)} {F(y + peakHeight * .45f)}L"
                + $"{F(x + offset)} {F(y - peakHeight)}L{F(x + offset + half)} {F(y + peakHeight * .45f)}Z\" "
                + "fill=\"#6c6b57\" opacity=\".72\"/>");
            if (fact.HillinessValue >= 4)
                builder.AppendLine($"<path d=\"M{F(x + offset - half * .25f)} {F(y - peakHeight * .58f)}L"
                    + $"{F(x + offset)} {F(y - peakHeight)}L{F(x + offset + half * .28f)} {F(y - peakHeight * .55f)}Z\" "
                    + "fill=\"#d7d6c4\" opacity=\".78\"/>");
        }
    }

    private static void DrawSelectionBoundary(StringBuilder builder,
        HashSet<int> members,
        IReadOnlyDictionary<int, List<Vector2>> polygons)
    {
        var edges = new Dictionary<string, (Vector2 A, Vector2 B, int Count)>();
        foreach (int tile in members)
        {
            IReadOnlyList<Vector2> polygon = polygons[tile];
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Count];
                string key = EdgeKey(a, b);
                if (edges.TryGetValue(key, out var edge))
                    edges[key] = (edge.A, edge.B, edge.Count + 1);
                else edges[key] = (a, b, 1);
            }
        }
        foreach ((Vector2 a, Vector2 b, int count) in edges.Values)
        {
            if (count != 1) continue;
            builder.AppendLine($"<line x1=\"{F(a.X)}\" y1=\"{F(a.Y)}\" "
                + $"x2=\"{F(b.X)}\" y2=\"{F(b.Y)}\" stroke=\"#6fe4f4\" "
                + "stroke-width=\"4\" stroke-linecap=\"round\" filter=\"url(#softShadow)\"/>");
        }
    }

    private static string EdgeKey(Vector2 a, Vector2 b)
    {
        string left = MathF.Round(a.X, 1).ToString("0.0",
                CultureInfo.InvariantCulture) + ","
            + MathF.Round(a.Y, 1).ToString("0.0",
                CultureInfo.InvariantCulture);
        string right = MathF.Round(b.X, 1).ToString("0.0",
                CultureInfo.InvariantCulture) + ","
            + MathF.Round(b.Y, 1).ToString("0.0",
                CultureInfo.InvariantCulture);
        return string.CompareOrdinal(left, right) <= 0
            ? left + "|" + right : right + "|" + left;
    }

    private static void DrawArrivalMarker(StringBuilder builder,
        AtlasComposition composition,
        IReadOnlyDictionary<int, List<Vector2>> polygons)
    {
        if (!polygons.TryGetValue(composition.ArrivalTileId,
                out List<Vector2>? polygon)) return;
        Vector2 center = PolygonCenter(polygon);
        builder.AppendLine($"<circle cx=\"{F(center.X)}\" cy=\"{F(center.Y)}\" "
            + "r=\"20\" fill=\"#0c2027\" fill-opacity=\".82\" stroke=\"#8bf4fb\" stroke-width=\"3\"/>");
        builder.AppendLine($"<path d=\"M{F(center.X)} {F(center.Y - 10)}l10 10-10 10-10-10z\" "
            + "fill=\"#8bf4fb\" stroke=\"#0c2027\" stroke-width=\"2\"/>");
        float labelX = center.X + 27f;
        float labelY = center.Y + 5f;
        builder.AppendLine($"<rect x=\"{F(labelX - 5f)}\" y=\"{F(labelY - 19f)}\" "
            + "width=\"72\" height=\"25\" rx=\"3\" fill=\"#0c171c\" fill-opacity=\".88\"/>");
        builder.AppendLine($"<text class=\"small\" x=\"{F(labelX)}\" y=\"{F(labelY)}\">Arrival</text>");
    }

    private static void DrawSavedGeographyCallouts(StringBuilder builder,
        WorldAtlas atlas, HashSet<int> members,
        IReadOnlyDictionary<int, List<Vector2>> polygons)
    {
        string[] visible = { "CoastalAtoll", "Archipelago",
            "CoastalIsland", "Cove", "Fjord", "Bay", "Peninsula",
            "RiverDelta", "RiverIsland" };
        var callouts = new List<(int Tile, string Label)>();
        foreach (int tile in members)
        {
            AtlasTileFact fact = atlas.Inspect(tile);
            AtlasDefRecord? geography = fact.Mutators.FirstOrDefault(item =>
                visible.Contains(item.DefName, StringComparer.Ordinal));
            if (geography != null)
                callouts.Add((tile, geography.Label));
        }
        foreach (((int tile, string label), int index) in callouts.Take(4)
                     .Select((item, index) => (item, index)))
        {
            Vector2 center = PolygonCenter(polygons[tile]);
            float direction = center.X < 450f ? 1f : -1f;
            float rise = index % 2 == 0 ? -34f : 34f;
            float targetX = center.X + direction * 72f;
            float targetY = center.Y + rise;
            float width = MathF.Max(94f, label.Length * 8.2f + 18f);
            float boxX = direction > 0f ? targetX : targetX - width;
            builder.AppendLine($"<path d=\"M{F(center.X)} {F(center.Y)}L{F(targetX)} {F(targetY)}\" "
                + "stroke=\"#d7edf2\" stroke-width=\"1.5\" opacity=\".85\"/>");
            builder.AppendLine($"<rect x=\"{F(boxX)}\" y=\"{F(targetY - 17f)}\" width=\"{F(width)}\" "
                + "height=\"25\" rx=\"4\" fill=\"#0c171c\" fill-opacity=\".90\" "
                + "stroke=\"#61747d\"/>");
            builder.AppendLine($"<text class=\"small\" x=\"{F(boxX + 9f)}\" y=\"{F(targetY + 1f)}\">"
                + Escape(label) + "</text>");
        }
    }

    private static string TileFill(AtlasTileFact fact)
    {
        if (fact.Biome.IsWaterBiome) return "url(#ocean)";
        string biome = fact.Biome.DefName.ToLowerInvariant();
        if (biome.Contains("rainforest") || biome.Contains("forest"))
            return "url(#rainforest)";
        if (biome.Contains("shrub") || biome.Contains("steppe"))
            return "url(#shrubland)";
        if (biome.Contains("desert") || biome.Contains("dune"))
            return "url(#desert)";
        if (biome.Contains("grass") || biome.Contains("savanna"))
            return "url(#grassland)";
        return ColorFor(fact.Biome.DefName);
    }

    private static void DrawFormation(StringBuilder builder,
        AtlasTileFact fact, IReadOnlyList<Vector2> polygon, bool arrival)
    {
        Vector2 center = PolygonCenter(polygon);
        float width = polygon.Max(point => point.X)
            - polygon.Min(point => point.X);
        float height = polygon.Max(point => point.Y)
            - polygon.Min(point => point.Y);
        float rx = width * 0.34f;
        float ry = height * 0.27f;

        if (HasMutator(fact, "CoastalAtoll"))
        {
            builder.AppendLine($"<ellipse cx=\"{F(center.X)}\" cy=\"{F(center.Y)}\" "
                + $"rx=\"{F(rx)}\" ry=\"{F(ry)}\" fill=\"none\" stroke=\"#e3c982\" stroke-width=\"12\" filter=\"url(#softShadow)\"/>");
            builder.AppendLine($"<ellipse cx=\"{F(center.X)}\" cy=\"{F(center.Y)}\" "
                + $"rx=\"{F(rx)}\" ry=\"{F(ry)}\" fill=\"none\" stroke=\"#3e744d\" stroke-width=\"7\"/>");
            builder.AppendLine($"<ellipse cx=\"{F(center.X)}\" cy=\"{F(center.Y)}\" "
                + $"rx=\"{F(rx * .56f)}\" ry=\"{F(ry * .48f)}\" fill=\"#49a4b5\" stroke=\"#8fd2d4\" stroke-width=\"1.5\"/>");
        }
        else if (HasMutator(fact, "Archipelago"))
        {
            DrawIsland(builder, center.X - rx * .55f, center.Y + 2f,
                rx * .56f, ry * .62f);
            DrawIsland(builder, center.X + rx * .36f, center.Y - ry * .45f,
                rx * .42f, ry * .42f);
            DrawIsland(builder, center.X + rx * .55f, center.Y + ry * .55f,
                rx * .28f, ry * .31f);
        }
        else if (HasMutator(fact, "CoastalIsland"))
            DrawIsland(builder, center.X, center.Y, rx * 1.12f, ry * .94f);

        if (HasMutator(fact, "SteamGeysers_Increased"))
        {
            builder.AppendLine($"<path d=\"M{F(center.X - 12)},"
                + $"{F(center.Y + 16)}q8-10 0-20q-8-10 2-20M{F(center.X + 5)},"
                + $"{F(center.Y + 16)}q9-12 1-22\" fill=\"none\" stroke=\"#d8e5dd\" stroke-width=\"3\" opacity=\".9\"/>");
        }
        if (HasMutator(fact, "Sandy"))
            builder.AppendLine($"<path d=\"M{F(center.X - rx)},"
                + $"{F(center.Y + ry * .45f)}q{F(rx * .7f)}-10 {F(rx * 1.4f)} 0\" fill=\"none\" stroke=\"#e0c47d\" stroke-width=\"6\" opacity=\".85\"/>");
        if (HasMutator(fact, "WindyMutator"))
            for (int i = -1; i <= 1; i++)
                builder.AppendLine($"<path d=\"M{F(center.X - rx)} "
                    + $"{F(center.Y + i * 9)}q{F(rx)}-9 {F(rx * 1.8f)} 0\" fill=\"none\" stroke=\"#d7e4da\" stroke-width=\"2\" opacity=\".75\"/>");
        if (arrival)
        {
            builder.AppendLine($"<circle cx=\"{F(center.X)}\" cy=\"{F(center.Y)}\" "
                + $"r=\"{F(MathF.Min(width, height) * .43f)}\" fill=\"none\" stroke=\"#7ce8f4\" stroke-width=\"3\" stroke-dasharray=\"7 5\"/>");
            builder.AppendLine($"<path d=\"M{F(center.X)} {F(center.Y - 13)}l10 10-10 10-10-10z\" fill=\"#91f0f5\" stroke=\"#102c35\" stroke-width=\"2\"/>");
        }
    }

    private static void DrawIsland(StringBuilder builder, float x, float y,
        float rx, float ry)
    {
        builder.AppendLine($"<ellipse cx=\"{F(x)}\" cy=\"{F(y)}\" rx=\"{F(rx)}\" "
            + $"ry=\"{F(ry)}\" fill=\"#dfc47d\" filter=\"url(#softShadow)\"/>");
        builder.AppendLine($"<ellipse cx=\"{F(x)}\" cy=\"{F(y - 1)}\" rx=\"{F(rx * .76f)}\" "
            + $"ry=\"{F(ry * .67f)}\" fill=\"#3e744d\"/>");
        builder.AppendLine($"<circle cx=\"{F(x - rx * .2f)}\" cy=\"{F(y - ry * .12f)}\" "
            + $"r=\"{F(MathF.Max(2f, MathF.Min(rx, ry) * .18f))}\" fill=\"#28553e\"/>");
    }

    private static void DrawLinks(StringBuilder builder, WorldAtlas atlas,
        HashSet<int> context,
        IReadOnlyDictionary<int, List<Vector2>> polygons)
    {
        var drawn = new HashSet<string>();
        foreach (int tile in context)
        {
            AtlasTileFact fact = atlas.Inspect(tile);
            foreach (AtlasLink link in fact.Rivers)
                Draw(link, "#63b5d0", 5f, 1f);
            foreach (AtlasLink link in fact.Roads)
                Draw(link, "#d0b077", 4f, 0f);

            void Draw(AtlasLink link, string color, float width,
                float dash)
            {
                int other = link.OtherTileId;
                if (!context.Contains(other) || !polygons.ContainsKey(other))
                    return;
                string key = Math.Min(tile, other) + ":" + Math.Max(tile, other)
                    + ":" + color;
                if (!drawn.Add(key)) return;
                Vector2 left = PolygonCenter(polygons[tile]);
                Vector2 right = PolygonCenter(polygons[other]);
                builder.AppendLine($"<line x1=\"{F(left.X)}\" y1=\"{F(left.Y)}\" "
                    + $"x2=\"{F(right.X)}\" y2=\"{F(right.Y)}\" stroke=\"{color}\" stroke-width=\"{F(width)}\" opacity=\".9\""
                    + (dash > 0f ? " stroke-dasharray=\"8 4\"" : "")
                    + "/>");
            }
        }
    }

    private static void DrawFormationInset(StringBuilder builder,
        AtlasTileFact fact, float x, float y, float width, float height)
    {
        bool waterFormation = HasMutator(fact, "CoastalAtoll")
            || HasMutator(fact, "Archipelago")
            || HasMutator(fact, "CoastalIsland")
            || HasMutator(fact, "Coast");
        builder.AppendLine($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(width)}\" "
            + $"height=\"{F(height)}\" rx=\"3\" fill=\"{(waterFormation ? "url(#ocean)" : TileFill(fact))}\" stroke=\"#60727b\"/>");
        float cx = x + width * .5f;
        float cy = y + height * .5f;
        if (HasMutator(fact, "CoastalAtoll"))
        {
            builder.AppendLine($"<ellipse cx=\"{F(cx)}\" cy=\"{F(cy)}\" rx=\"112\" ry=\"49\" fill=\"none\" stroke=\"#e5c77c\" stroke-width=\"25\"/>");
            builder.AppendLine($"<ellipse cx=\"{F(cx)}\" cy=\"{F(cy)}\" rx=\"112\" ry=\"49\" fill=\"none\" stroke=\"#3d734a\" stroke-width=\"14\"/>");
            builder.AppendLine($"<ellipse cx=\"{F(cx)}\" cy=\"{F(cy)}\" rx=\"71\" ry=\"27\" fill=\"#52adbc\" stroke=\"#9bd8d6\" stroke-width=\"2\"/>");
            for (int i = 0; i < 14; i++)
            {
                double angle = i * Math.PI * 2d / 14d;
                float tx = cx + MathF.Cos((float)angle) * 105f;
                float ty = cy + MathF.Sin((float)angle) * 43f;
                builder.AppendLine($"<circle cx=\"{F(tx)}\" cy=\"{F(ty)}\" r=\"5\" fill=\"#244f37\"/>");
            }
        }
        else if (HasMutator(fact, "Archipelago"))
        {
            DrawIsland(builder, cx - 70f, cy + 5f, 48f, 25f);
            DrawIsland(builder, cx + 22f, cy - 26f, 38f, 20f);
            DrawIsland(builder, cx + 88f, cy + 26f, 29f, 15f);
            DrawIsland(builder, cx + 25f, cy + 37f, 18f, 10f);
        }
        else if (HasMutator(fact, "CoastalIsland"))
            DrawIsland(builder, cx, cy, 103f, 51f);
        else if (waterFormation)
        {
            builder.AppendLine($"<path d=\"M{F(x)} {F(y + height * .72f)}Q"
                + $"{F(cx)} {F(y + height * .55f)} {F(x + width)} {F(y + height * .7f)}V"
                + $"{F(y + height)}H{F(x)}Z\" fill=\"url(#ocean)\"/>");
            builder.AppendLine($"<path d=\"M{F(x)} {F(y + height * .69f)}Q"
                + $"{F(cx)} {F(y + height * .52f)} {F(x + width)} {F(y + height * .67f)}\" fill=\"none\" stroke=\"#e1c783\" stroke-width=\"7\"/>");
        }
        builder.AppendLine($"<text class=\"micro\" x=\"{F(x + 10f)}\" "
            + $"y=\"{F(y + height - 10f)}\">{Escape(LocalLabel(fact))} - formation implied by saved geography</text>");
    }

    private static Vector2 PolygonCenter(IReadOnlyList<Vector2> polygon)
    {
        if (polygon.Count == 0) return Vector2.Zero;
        Vector2 total = Vector2.Zero;
        foreach (Vector2 point in polygon) total += point;
        return total / polygon.Count;
    }

    private static string LocalLabel(AtlasTileFact fact)
    {
        AtlasDefRecord? mutator = fact.Mutators.FirstOrDefault(item =>
            item.DefName is "CoastalAtoll" or "Archipelago"
                or "CoastalIsland" or "Cove" or "Fjord" or "Bay"
                or "Peninsula" or "RiverDelta" or "RiverIsland")
            ?? fact.Mutators.FirstOrDefault();
        return mutator?.Label ?? fact.Biome.Label;
    }

    private static bool HasMutator(AtlasTileFact fact, string defName) =>
        fact.Mutators.Any(item => string.Equals(item.DefName, defName,
            StringComparison.Ordinal));

    private static string ColorFor(string value)
    {
        int hash = 23;
        foreach (char character in value) hash = unchecked(hash * 31 + character);
        float hue = (uint)hash % 360;
        return Hsl(hue, 34f, 38f);
    }

    private static string Hsl(float hue, float saturation, float lightness)
        => $"hsl({hue.ToString("0", CultureInfo.InvariantCulture)} "
            + $"{saturation.ToString("0", CultureInfo.InvariantCulture)}% "
            + $"{lightness.ToString("0", CultureInfo.InvariantCulture)}%)";

    private static string F(float value) => value.ToString("0.##",
        CultureInfo.InvariantCulture);

    private static string Escape(string value) => SecurityElement.Escape(value)
        ?? "";
}
