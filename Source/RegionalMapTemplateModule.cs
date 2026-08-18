using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonistAwareness
{
    // Regional map template, slice 2: the fresh-game hydration vertical
    // slice (terrain geography first).
    //
    // Flow (orientation-verified seams): capture extracts the terrain
    // grids of a source save's first map into a versioned artifact
    // WITHOUT mutating the save; arming (env CA_REGIONAL_TEMPLATE=path,
    // validated at startup) routes the next new game through
    // GameInitData.mapGeneratorDef (consumed at Game.cs:529 ->
    // MapGenerator.GenerateMap, which allocates the fresh map ID, binds
    // the fresh MapParent, and runs the scenario/finalize chain natively);
    // the CA_RegionalTemplate MapGeneratorDef then runs the hydration
    // genstep followed by the native run-state steps
    // (FindPlayerStartSpot, ScenParts, Fog). The old Map object is never
    // deserialized: only labeled geography payloads cross, so no old
    // MapParent, map ID, Thing IDs, faction refs, or run state can leak.
    //
    // v1 scope: topGrid/underGrid/foundationGrid/tempGrid terrain
    // hydration through the PUBLIC TerrainGrid setters (SetTerrain /
    // SetUnderTerrain / SetFoundation keep water/foundation/glow caches
    // coherent during generation, exactly what direct array fill would
    // skip). Roofs, snow, pollution, the compressed thing grid, template
    // thing candidates, plan fingerprint, and composition arrive in later
    // increments per the census contract.

    // Pure capture/validation. System.* only: runs headless against
    // disposable fixtures; the source save is opened strictly for
    // reading.
    public static class CARegionalMapArtifactCapture
    {
        public static readonly string[] TerrainGridLabels =
        {
            "topGrid", "underGrid", "foundationGrid", "tempGrid"
        };

        // Extracts the first map's size and labeled terrain payloads into
        // a versioned artifact file. Returns the manifest line summary.
        public static string CaptureGeography(string savePath,
            string artifactPath)
        {
            string sourceSha;
            using (FileStream hashStream = File.OpenRead(savePath))
            using (SHA256 sha256 = SHA256.Create())
                sourceSha = BitConverter.ToString(
                    sha256.ComputeHash(hashStream)).Replace("-", "");

            string mapSize = null;
            var payloads = new SortedDictionary<string, string>(
                StringComparer.Ordinal);
            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                DtdProcessing = DtdProcessing.Prohibit,
                CloseInput = true
            };
            using (XmlReader reader = XmlReader.Create(
                File.OpenRead(savePath), settings))
            {
                int mapsDepth = -1;
                int mapDepth = -1;
                int terrainDepth = -1;
                bool inMaps = false, inMap = false, done = false;
                bool nodePending = false;
                long sawMaps = 0, sawMapLi = 0, sawTerrainGrid = 0;
                while (!done)
                {
                    if (nodePending) nodePending = false;
                    else if (!reader.Read()) break;
                    if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        if (terrainDepth >= 0
                            && reader.Depth == terrainDepth)
                            terrainDepth = -1;
                        if (inMap && reader.Depth == mapDepth) done = true;
                        if (inMaps && reader.Depth == mapsDepth)
                            inMaps = false;
                        continue;
                    }
                    if (reader.NodeType != XmlNodeType.Element) continue;
                    string name = reader.LocalName;
                    if (!inMaps && name == "maps" && !reader.IsEmptyElement)
                    {
                        inMaps = true;
                        mapsDepth = reader.Depth;
                        sawMaps++;
                        continue;
                    }
                    if (inMaps && !inMap && name == "li"
                        && reader.Depth == mapsDepth + 1)
                    {
                        inMap = true;
                        mapDepth = reader.Depth;
                        sawMapLi++;
                        continue;
                    }
                    if (!inMap) continue;
                    if (mapSize == null && name == "size")
                    {
                        mapSize = reader.ReadElementContentAsString();
                        nodePending = true;
                        continue;
                    }
                    if (name == "terrainGrid" && !reader.IsEmptyElement)
                    {
                        terrainDepth = reader.Depth;
                        sawTerrainGrid++;
                        continue;
                    }
                    if (terrainDepth >= 0 && !reader.IsEmptyElement)
                    {
                        // DataExposeUtility.LookByteArray serializes each
                        // labeled byte array as <labelDeflate>; accept the
                        // bare label too and store under the bare name.
                        string bare = name.EndsWith("Deflate",
                            StringComparison.Ordinal)
                            ? name.Substring(0,
                                name.Length - "Deflate".Length)
                            : name;
                        if (Array.IndexOf(TerrainGridLabels, bare) >= 0)
                        {
                            payloads[bare] =
                                reader.ReadElementContentAsString();
                            nodePending = true;
                        }
                    }
                }
                if (mapSize == null || !payloads.ContainsKey("topGrid"))
                    throw new InvalidDataException("capture incomplete: "
                        + "sawMaps=" + sawMaps + " sawMapLi=" + sawMapLi
                        + " sawTerrainGrid=" + sawTerrainGrid
                        + " size=" + (mapSize ?? "(null)")
                        + " payloads=["
                        + string.Join(",", payloads.Keys) + "]");
            }

            var artifact = new XmlWriterSettings
            {
                Indent = true,
                CloseOutput = true
            };
            using (XmlWriter writer = XmlWriter.Create(
                File.Create(artifactPath), artifact))
            {
                writer.WriteStartElement("caRegionalMapArtifact");
                writer.WriteAttributeString("schema",
                    CARegionalMapArtifactContract.SchemaVersion.ToString());
                writer.WriteElementString("sourceSha256", sourceSha);
                writer.WriteElementString("sourceBytes",
                    new FileInfo(savePath).Length.ToString());
                writer.WriteElementString("capturedUtc",
                    DateTime.UtcNow.ToString("o"));
                writer.WriteElementString("size", mapSize);
                foreach (KeyValuePair<string, string> pair in payloads)
                {
                    writer.WriteStartElement("grid");
                    writer.WriteAttributeString("label", pair.Key);
                    writer.WriteString(pair.Value);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            return "captured size=" + mapSize + " grids=["
                + string.Join(",", payloads.Keys) + "] sourceSha256="
                + sourceSha;
        }

        // Parses and structurally validates an artifact: schema, size,
        // decodable payload per grid label with the expected cell count.
        public static string ValidateArtifact(string artifactPath,
            out int sizeX, out int sizeZ,
            out Dictionary<string, ushort[]> grids)
        {
            sizeX = 0;
            sizeZ = 0;
            grids = new Dictionary<string, ushort[]>(StringComparer.Ordinal);
            var doc = new XmlDocument();
            using (FileStream stream = File.OpenRead(artifactPath))
                doc.Load(stream);
            XmlElement root = doc.DocumentElement;
            if (root == null || root.Name != "caRegionalMapArtifact")
                return "not a CA regional map artifact";
            if (root.GetAttribute("schema")
                != CARegionalMapArtifactContract.SchemaVersion.ToString())
                return "schema mismatch: " + root.GetAttribute("schema");
            XmlNode sizeNode = root.SelectSingleNode("size");
            if (sizeNode == null) return "missing size";
            string[] parts = sizeNode.InnerText.Trim('(', ')')
                .Split(new char[] { ',' });
            if (parts.Length != 3
                || !int.TryParse(parts[0].Trim(), out sizeX)
                || !int.TryParse(parts[2].Trim(), out sizeZ)
                || sizeX <= 0 || sizeZ <= 0)
                return "unparseable size: " + sizeNode.InnerText;
            long cells = (long)sizeX * sizeZ;
            foreach (XmlNode node in root.SelectNodes("grid"))
            {
                string label = ((XmlElement)node).GetAttribute("label");
                ushort[] decoded = DecodeUshortGrid(node.InnerText);
                if (decoded == null)
                    return "grid " + label + " failed to decode";
                if (decoded.Length != cells)
                    return "grid " + label + " has " + decoded.Length
                        + " cells, expected " + cells;
                grids[label] = decoded;
            }
            if (!grids.ContainsKey("topGrid")) return "missing topGrid";
            return null;
        }

        public static ushort[] DecodeUshortGrid(string base64)
        {
            try
            {
                byte[] raw = Convert.FromBase64String(base64);
                byte[] decoded;
                using (var input = new MemoryStream(raw))
                using (var deflate = new System.IO.Compression.DeflateStream(
                    input, System.IO.Compression.CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    deflate.CopyTo(output);
                    decoded = output.ToArray();
                }
                if (decoded.Length % 2 != 0) return null;
                var grid = new ushort[decoded.Length / 2];
                for (int i = 0; i < grid.Length; i++)
                    grid[i] = (ushort)(decoded[i * 2]
                        | decoded[i * 2 + 1] << 8);
                return grid;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    // The hydration genstep: writes the armed artifact's terrain onto the
    // freshly generated map through the public TerrainGrid setters, in
    // native cell order, consuming no Rand.
    public class GenStep_CARegionalTemplateHydration : GenStep
    {
        public override int SeedPart => 1748217643;

        public override void Generate(Map map, GenStepParams parms)
        {
            var armed = CARegionalTemplateArming.ArmedGrids;
            if (armed == null)
            {
                Log.Error("[CA][Regional][Template] hydration genstep ran "
                    + "without an armed artifact; map keeps default "
                    + "terrain");
                return;
            }
            if (map.Size.x != CARegionalTemplateArming.ArmedSizeX
                || map.Size.z != CARegionalTemplateArming.ArmedSizeZ)
            {
                Log.Error("[CA][Regional][Template] map size "
                    + map.Size.x + "x" + map.Size.z + " does not match "
                    + "armed artifact "
                    + CARegionalTemplateArming.ArmedSizeX + "x"
                    + CARegionalTemplateArming.ArmedSizeZ
                    + "; hydration skipped");
                return;
            }
            Stopwatch watch = Stopwatch.StartNew();
            var byHash = new Dictionary<ushort, TerrainDef>();
            foreach (TerrainDef def in
                DefDatabase<TerrainDef>.AllDefsListForReading)
                byHash[def.shortHash] = def;
            ushort[] top = armed["topGrid"];
            ushort[] under;
            armed.TryGetValue("underGrid", out under);
            ushort[] foundation;
            armed.TryGetValue("foundationGrid", out foundation);
            ushort[] temp;
            armed.TryGetValue("tempGrid", out temp);
            int sizeX = map.Size.x;
            int unresolved = 0;
            long setCalls = 0;
            for (int z = 0; z < map.Size.z; z++)
            {
                int rowBase = z * sizeX;
                for (int x = 0; x < sizeX; x++)
                {
                    var cell = new IntVec3(x, 0, z);
                    int index = rowBase + x;
                    TerrainDef topDef = Resolve(byHash, top[index],
                        TerrainDefOf.Soil, ref unresolved);
                    map.terrainGrid.SetTerrain(cell, topDef);
                    setCalls++;
                    if (under != null && under[index] != 0)
                    {
                        TerrainDef def = Resolve(byHash, under[index], null,
                            ref unresolved);
                        if (def != null)
                        {
                            map.terrainGrid.SetUnderTerrain(cell, def);
                            setCalls++;
                        }
                    }
                    if (foundation != null && foundation[index] != 0)
                    {
                        TerrainDef def = Resolve(byHash, foundation[index],
                            null, ref unresolved);
                        if (def != null)
                        {
                            map.terrainGrid.SetFoundation(cell, def);
                            setCalls++;
                        }
                    }
                    if (temp != null && temp[index] != 0)
                    {
                        TerrainDef def = Resolve(byHash, temp[index], null,
                            ref unresolved);
                        if (def != null)
                        {
                            map.terrainGrid.SetTempTerrain(cell, def);
                            setCalls++;
                        }
                    }
                }
            }
            watch.Stop();
            Log.Message("[CA][Regional][Template] hydrated terrain for "
                + map.Size.x + "x" + map.Size.z + " map " + map.uniqueID
                + " in " + watch.ElapsedMilliseconds + " ms: " + setCalls
                + " setter calls, " + unresolved
                + " unresolved terrain hashes (defaulted), artifact "
                + CARegionalTemplateArming.ArmedPath);
        }

        private static TerrainDef Resolve(
            Dictionary<ushort, TerrainDef> byHash, ushort hash,
            TerrainDef fallback, ref int unresolved)
        {
            if (hash == 0) return fallback;
            TerrainDef def;
            if (byHash.TryGetValue(hash, out def)) return def;
            unresolved++;
            return fallback;
        }
    }

    // Startup arming: env CA_REGIONAL_TEMPLATE=<artifact path>. The
    // artifact is fully validated BEFORE any patch is applied; an invalid
    // or absent artifact leaves the game entirely native. When armed, a
    // fresh new game is routed through the CA_RegionalTemplate
    // MapGeneratorDef and the map size is overridden to the artifact's at
    // the exact GenerateMap boundary.
    [StaticConstructorOnStartup]
    internal static class CARegionalTemplateArming
    {
        private const string HarmonyId =
            "ellyj3rain.colonistawareness.regionaljobs.template";

        internal static Dictionary<string, ushort[]> ArmedGrids;
        internal static int ArmedSizeX;
        internal static int ArmedSizeZ;
        internal static string ArmedPath;

        // Arming reads the env var first, then the DevOutput marker file
        // (the same file channel as the debug-command bridge). The marker
        // survives environment-inheritance pitfalls: a Steam-launched game
        // inherits Steam's stale environment, so an env var set after
        // Steam started never reaches the process.
        private const string ArmingMarkerFile =
            "ColonistAwareness.regional-template.txt";

        static CARegionalTemplateArming()
        {
            try
            {
                string path = Environment.GetEnvironmentVariable(
                    "CA_REGIONAL_TEMPLATE");
                if (string.IsNullOrEmpty(path))
                {
                    try
                    {
                        string marker = Path.Combine(
                            Environment.GetFolderPath(
                                Environment.SpecialFolder
                                    .LocalApplicationData)
                            + "Low",
                            "Ludeon Studios",
                            "RimWorld by Ludeon Studios", "DevOutput",
                            ArmingMarkerFile);
                        if (File.Exists(marker))
                            path = File.ReadAllText(marker).Trim();
                    }
                    catch (Exception)
                    {
                        // Marker reading is optional; unreadable means
                        // unarmed, never an error.
                    }
                }
                if (string.IsNullOrEmpty(path)) return;
                if (!File.Exists(path))
                {
                    Log.Warning("[CA][Regional][Template] "
                        + "CA_REGIONAL_TEMPLATE set but not found: "
                        + path);
                    return;
                }
                Dictionary<string, ushort[]> grids;
                int sizeX, sizeZ;
                string failure = CARegionalMapArtifactCapture
                    .ValidateArtifact(path, out sizeX, out sizeZ,
                        out grids);
                if (failure != null)
                {
                    Log.Error("[CA][Regional][Template] artifact "
                        + "validation failed (" + failure
                        + "); native generation retained");
                    return;
                }
                var harmony = new Harmony(HarmonyId);
                harmony.Patch(
                    AccessTools.Method(typeof(Game),
                        nameof(Game.InitNewGame)),
                    prefix: new HarmonyMethod(AccessTools.Method(
                        typeof(CARegionalTemplateArming),
                        nameof(InitNewGamePrefix)))
                    { priority = Priority.First });
                harmony.Patch(
                    AccessTools.Method(typeof(MapGenerator),
                        nameof(MapGenerator.GenerateMap)),
                    prefix: new HarmonyMethod(AccessTools.Method(
                        typeof(CARegionalTemplateArming),
                        nameof(GenerateMapPrefix)))
                    { priority = Priority.First });
                ArmedGrids = grids;
                ArmedSizeX = sizeX;
                ArmedSizeZ = sizeZ;
                ArmedPath = path;
                Log.Message("[CA][Regional][Template] ARMED: next new "
                    + "game hydrates " + sizeX + "x" + sizeZ
                    + " terrain from " + path + " through the native "
                    + "InitNewGame/GenerateMap lifecycle (fresh map ID, "
                    + "parent, scenario chain)");
            }
            catch (Exception error)
            {
                ArmedGrids = null;
                Log.Error("[CA][Regional][Template] arming failed; "
                    + "native generation retained: " + error);
            }
        }

        internal static void InitNewGamePrefix()
        {
            try
            {
                if (ArmedGrids == null || Find.GameInitData == null) return;
                MapGeneratorDef templateDef =
                    DefDatabase<MapGeneratorDef>.GetNamedSilentFail(
                        "CA_RegionalTemplate");
                if (templateDef == null)
                {
                    Log.Error("[CA][Regional][Template] "
                        + "CA_RegionalTemplate MapGeneratorDef missing; "
                        + "native generation retained");
                    return;
                }
                Find.GameInitData.mapGeneratorDef = templateDef;
                Log.Message("[CA][Regional][Template] routed new game "
                    + "through CA_RegionalTemplate");
            }
            catch (Exception error)
            {
                Log.Error("[CA][Regional][Template] routing failed; "
                    + "native generation retained: " + error);
            }
        }

        internal static void GenerateMapPrefix(ref IntVec3 mapSize,
            MapGeneratorDef mapGenerator)
        {
            try
            {
                if (ArmedGrids == null || mapGenerator == null
                    || mapGenerator.defName != "CA_RegionalTemplate")
                    return;
                mapSize = new IntVec3(ArmedSizeX, 1, ArmedSizeZ);
            }
            catch (Exception)
            {
                // Size override is best-effort; a mismatch is caught by
                // the hydration genstep's size gate.
            }
        }
    }
}
