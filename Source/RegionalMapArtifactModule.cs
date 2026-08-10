using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace ColonistAwareness
{
    // Regional map artifact, slice 1: the versioned capture contract and
    // the deterministic identity/provenance census.
    //
    // Capture an already-generated regional map as a named, versioned artifact
    // without mutating its source save; later instantiate it into a fresh
    // Game, compose CA faction/settlement authoring over the preserved
    // geography, and start a new colony. This slice deliberately builds
    // the CONTRACT and the CENSUS first - the classification of every
    // saved map element into template-geography, run-state, or
    // policy-decided classes, plus the identity population that a fresh
    // Game must remap - because every later import step depends on that
    // boundary being explicit, versioned, and measured against the real
    // source checkpoint. No UI, no game interaction, no engine types:
    // this class reads a save file stream and writes a manifest, so the
    // gate can run against a disposable copy while the source checkpoint
    // stays byte-identical.
    //
    // Verified engine seams this contract is designed against (decompiled
    // 1.6.4871; orientation "Verified decompiled landmarks"): a raw saved
    // Map is NOT portable across Games - MapInfo retains the old
    // MapParent by reference (Verse/MapInfo.cs:33-39), Map retains its
    // old ID/generation state (Map.cs:626-634), Things retain identity
    // and faction load-references (Thing.cs:1219-1237). Import therefore
    // enters through the NATIVE fresh-game lifecycle: Game.InitNewGame
    // (Game.cs:492-553) -> MapGenerator.GenerateMap
    // (MapGenerator.cs:79-186), which allocates the fresh map ID, binds
    // the fresh MapParent, constructs components, and registers the map -
    // with a dedicated CA template mapGeneratorDef / hydration genstep
    // (via GameInitData.mapGeneratorDef, GameInitData.cs:10-22) replacing
    // geography CONTENTS inside that lifecycle while Scenario
    // PreMapGenerate/PostMapGenerate, Map.FinalizeInit, and PostGameStart
    // all run natively. Map.ExposeData (Map.cs:620-706, components
    // :858-898) defines the capture substrate this census classifies; a
    // template additionally needs a geography-only plan/fingerprint
    // registered before projection access (RegionalSetupModule.cs:
    // 2708-2717, :3051-3065; CARegionalWorldComponent plan ownership at
    // RegionalWorldModule.cs:668-678).

    public static class CARegionalMapArtifactContract
    {
        public const int SchemaVersion = 2;

        // v2: the compressed thing grid is its own first-class boundary -
        // it carries the BULK of natural geography (rocks, most plants)
        // as per-cell def references, exactly the content a template must
        // preserve, with no identities to remap.
        public const string TemplateCompressedThings =
            "TemplateCompressedThings";

        // v2: per-component classification by serialized Class attribute.
        // CA's projection component is regenerable from the plan
        // (RegionalSetupModule.cs:3051-3065), so the template carries the
        // plan fingerprint, not the arrays; behavior components are run
        // state; unknown components stay visible as Unclassified.
        public static string ClassifyComponent(string componentClass)
        {
            if (string.IsNullOrEmpty(componentClass)) return Unclassified;
            if (componentClass.Contains("CARegionalProjectionMapComponent"))
                return TemplateGeography;
            if (componentClass.StartsWith("ColonistAwareness.",
                StringComparison.Ordinal))
                return RunState;
            if (componentClass.Contains("History")
                || componentClass.Contains("Memory")
                || componentClass.Contains("Pawn"))
                return RunState;
            return Unclassified;
        }

        // Classification classes for saved map elements. v1 policy:
        //   TemplateGeography - included in the template verbatim.
        //   TemplateThingCandidate - things included unless owned by a
        //     non-hidden faction or classified run-state by def class.
        //   RunState - never captured; recreated by the fresh run.
        //   PolicyDecided - natural/generated content whose inclusion is
        //     a per-capture choice recorded in provenance.
        public const string TemplateGeography = "TemplateGeography";
        public const string TemplateThingCandidate = "TemplateThingCandidate";
        public const string RunState = "RunState";
        public const string PolicyDecided = "PolicyDecided";
        public const string Unclassified = "Unclassified";

        // v1 classification of Map's direct saved children (element names
        // as serialized by Map.ExposeData and components).
        public static string ClassifyMapChild(string elementName)
        {
            switch (elementName)
            {
                case "mapInfo":
                case "terrainGrid":
                case "roofGrid":
                case "snowGrid":
                case "sandGrid":
                case "waterInfo":
                case "pollutionGrid":
                    return TemplateGeography;
                case "things":
                    return TemplateThingCandidate;
                case "compressedThingMapDeflate":
                    return TemplateCompressedThings;
                case "zoneManager":
                case "reservationManager":
                case "lordManager":
                case "designationManager":
                case "haulDestinationManager":
                case "areaManager":
                case "listerFilthInHomeArea":
                case "autoSlaughterManager":
                case "storyState":
                case "gameConditionManager":
                case "weatherManager":
                case "temperatureCache":
                case "retainedCaravanData":
                case "wildPlantSpawner":
                case "deferredSpawner":
                    return RunState;
                case "fogGrid":
                case "gasGrid":
                    return PolicyDecided;
                case "components":
                    // Per-component classification is a later schema rev;
                    // v1 records the census and flags it for review.
                    return PolicyDecided;
                default:
                    return Unclassified;
            }
        }

        // v1 classification of a thing by its serialized Class attribute.
        public static string ClassifyThingClass(string thingClass)
        {
            if (string.IsNullOrEmpty(thingClass)) return Unclassified;
            if (thingClass == "Pawn") return RunState;
            if (thingClass.StartsWith("Corpse", StringComparison.Ordinal))
                return PolicyDecided;
            if (thingClass.StartsWith("Plant", StringComparison.Ordinal))
                return PolicyDecided;
            if (thingClass.StartsWith("Filth", StringComparison.Ordinal))
                return PolicyDecided;
            if (thingClass.StartsWith("Fire", StringComparison.Ordinal))
                return RunState;
            if (thingClass.StartsWith("Mote", StringComparison.Ordinal))
                return RunState;
            if (thingClass.StartsWith("Blueprint", StringComparison.Ordinal)
                || thingClass.StartsWith("Frame", StringComparison.Ordinal))
                return RunState;
            return TemplateThingCandidate;
        }
    }

    // Streams a .rws save (or a copy of one) and emits the deterministic
    // v1 census manifest for its first map: element classes and counts,
    // thing-class buckets, faction references, pawn/thing identity
    // populations, and provenance. Pure System.* only, so it runs
    // headless against fixtures and never links engine state.
    public static class CARegionalMapArtifactCensus
    {
        public static string Run(string savePath)
        {
            var manifest = new StringBuilder(8192);
            string sha;
            long bytes;
            using (FileStream hashStream = File.OpenRead(savePath))
            using (SHA256 sha256 = SHA256.Create())
            {
                bytes = hashStream.Length;
                sha = BitConverter.ToString(
                    sha256.ComputeHash(hashStream)).Replace("-", "");
            }
            manifest.AppendLine("CARegionalMapArtifact census");
            manifest.AppendLine("schemaVersion="
                + CARegionalMapArtifactContract.SchemaVersion);
            manifest.AppendLine("sourcePath=" + savePath);
            manifest.AppendLine("sourceBytes=" + bytes);
            manifest.AppendLine("sourceSha256=" + sha);

            var childCounts = new SortedDictionary<string, long>(
                StringComparer.Ordinal);
            var childClasses = new SortedDictionary<string, string>(
                StringComparer.Ordinal);
            var thingClassCounts = new SortedDictionary<string, long>(
                StringComparer.Ordinal);
            var thingClassification = new SortedDictionary<string, long>(
                StringComparer.Ordinal);
            var factionRefs = new SortedDictionary<string, long>(
                StringComparer.Ordinal);
            long thingsTotal = 0;
            long pawnCount = 0;
            long thingIdRefs = 0;
            long compressedCells = -1;
            long compressedOccupied = -1;
            var componentClasses = new SortedDictionary<string, long>(
                StringComparer.Ordinal);
            string seedString = null;
            string mapSize = null;

            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                DtdProcessing = DtdProcessing.Prohibit,
                CloseInput = true
            };
            using (XmlReader reader = XmlReader.Create(
                File.OpenRead(savePath), settings))
            {
                int mapDepth = -1;
                int mapsDepth = -1;
                bool inMaps = false;
                bool inFirstMap = false;
                bool firstMapDone = false;
                int thingsDepth = -1;
                // ReadElementContentAsString leaves the reader already
                // positioned on the following node; a plain Read() there
                // would silently skip one element (v2 regression fixed:
                // it swallowed <things> after the compressed grid).
                bool nodePending = false;
                while (true)
                {
                    if (nodePending) nodePending = false;
                    else if (!reader.Read()) break;
                    if (reader.NodeType != XmlNodeType.Element)
                    {
                        if (reader.NodeType == XmlNodeType.EndElement)
                        {
                            if (inFirstMap && reader.Depth == mapDepth)
                            {
                                inFirstMap = false;
                                firstMapDone = true;
                            }
                            if (inMaps && reader.Depth == mapsDepth)
                                inMaps = false;
                        }
                        continue;
                    }
                    string name = reader.LocalName;
                    if (seedString == null && name == "seedString")
                    {
                        seedString = reader.ReadElementContentAsString();
                        nodePending = true;
                        continue;
                    }
                    // The first map is the first <li> directly inside the
                    // <maps> container - tracked structurally, because the
                    // serializer emits earlier unrelated <li> nodes (world
                    // and scenario lists) at similar depths.
                    if (!firstMapDone && name == "maps"
                        && !reader.IsEmptyElement)
                    {
                        inMaps = true;
                        mapsDepth = reader.Depth;
                        continue;
                    }
                    if (inMaps && !inFirstMap && !firstMapDone
                        && name == "li" && reader.Depth == mapsDepth + 1)
                    {
                        inFirstMap = true;
                        mapDepth = reader.Depth;
                        continue;
                    }
                    if (!inFirstMap) continue;

                    if (reader.Depth == mapDepth + 1)
                    {
                        long current;
                        childCounts.TryGetValue(name, out current);
                        childCounts[name] = current + 1;
                        if (!childClasses.ContainsKey(name))
                            childClasses[name] = CARegionalMapArtifactContract
                                .ClassifyMapChild(name);
                        thingsDepth = name == "things" ? reader.Depth : -1;
                    }
                    if (thingsDepth >= 0 && name == "thing"
                        && reader.Depth == thingsDepth + 1)
                    {
                        thingsTotal++;
                        string thingClass =
                            reader.GetAttribute("Class") ?? "(none)";
                        long classCount;
                        thingClassCounts.TryGetValue(thingClass,
                            out classCount);
                        thingClassCounts[thingClass] = classCount + 1;
                        string classification =
                            CARegionalMapArtifactContract.ClassifyThingClass(
                                thingClass);
                        long bucket;
                        thingClassification.TryGetValue(classification,
                            out bucket);
                        thingClassification[classification] = bucket + 1;
                        if (thingClass == "Pawn") pawnCount++;
                    }
                    if (name == "compressedThingMapDeflate" && inFirstMap
                        && !reader.IsEmptyElement)
                    {
                        DecodeCompressedGrid(
                            reader.ReadElementContentAsString(), manifest,
                            ref compressedCells, ref compressedOccupied);
                        nodePending = true;
                        continue;
                    }
                    if (name == "li" && inFirstMap
                        && reader.GetAttribute("Class") != null
                        && childClasses.ContainsKey("components"))
                    {
                        string componentClass =
                            reader.GetAttribute("Class");
                        long componentCount;
                        componentClasses.TryGetValue(componentClass,
                            out componentCount);
                        componentClasses[componentClass] =
                            componentCount + 1;
                    }
                    if (mapSize == null && name == "size" && inFirstMap)
                    {
                        mapSize = reader.ReadElementContentAsString();
                        nodePending = true;
                    }
                    else if (name == "faction" && inFirstMap
                        && !reader.IsEmptyElement)
                    {
                        string factionRef =
                            reader.ReadElementContentAsString();
                        nodePending = true;
                        long refCount;
                        factionRefs.TryGetValue(factionRef, out refCount);
                        factionRefs[factionRef] = refCount + 1;
                    }
                    else if (name == "id" && inFirstMap)
                    {
                        thingIdRefs++;
                    }
                }
            }

            manifest.AppendLine("seedString=" + (seedString ?? "(none)"));
            manifest.AppendLine("firstMapSize=" + (mapSize ?? "(none)"));
            manifest.AppendLine("thingsTotal=" + thingsTotal);
            manifest.AppendLine("pawnThings=" + pawnCount);
            manifest.AppendLine("idElements=" + thingIdRefs);
            manifest.AppendLine("-- map children (element=count:class) --");
            foreach (KeyValuePair<string, long> pair in childCounts)
                manifest.AppendLine(pair.Key + "=" + pair.Value + ":"
                    + childClasses[pair.Key]);
            manifest.AppendLine("-- thing classification totals --");
            foreach (KeyValuePair<string, long> pair in thingClassification)
                manifest.AppendLine(pair.Key + "=" + pair.Value);
            manifest.AppendLine("-- thing classes (top-level) --");
            foreach (KeyValuePair<string, long> pair in thingClassCounts)
                manifest.AppendLine(pair.Key + "=" + pair.Value);
            manifest.AppendLine("-- faction references --");
            foreach (KeyValuePair<string, long> pair in factionRefs)
                manifest.AppendLine(pair.Key + "=" + pair.Value);
            manifest.AppendLine("compressedGridCells=" + compressedCells);
            manifest.AppendLine("compressedGridOccupied="
                + compressedOccupied);
            manifest.AppendLine("-- map components (Class=count:class) --");
            foreach (KeyValuePair<string, long> pair in componentClasses)
                manifest.AppendLine(pair.Key + "=" + pair.Value + ":"
                    + CARegionalMapArtifactContract.ClassifyComponent(
                        pair.Key));
            return manifest.ToString();
        }

        // Decodes the base64+deflate compressed thing grid (one ushort of
        // def reference per cell) far enough to measure the bulk-geography
        // boundary: total cells and occupied cells. Any decode surprise is
        // recorded, never thrown - the census must always complete.
        private static void DecodeCompressedGrid(string base64,
            StringBuilder manifest, ref long cells, ref long occupied)
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
                cells = decoded.Length / 2;
                long filled = 0;
                for (int i = 0; i + 1 < decoded.Length; i += 2)
                    if (decoded[i] != 0 || decoded[i + 1] != 0) filled++;
                occupied = filled;
            }
            catch (Exception error)
            {
                manifest.AppendLine("compressedGridDecodeFailed="
                    + error.GetType().Name);
            }
        }

        // A maps-list item is recognized by its Class attribute or by
        // being a direct child of a <maps> element; the reader is
        // positioned on <li> when called.
        private static bool ReaderIsMapListItem(XmlReader reader)
        {
            string cls = reader.GetAttribute("Class");
            if (cls != null && cls.Contains("Map")) return true;
            // Fall back to structural detection: RimWorld serializes maps
            // as <maps><li>...</li></maps> with no Class attribute; the
            // census treats the first <li> whose immediate prior ancestor
            // path ended in "maps" as the map. XmlReader cannot look up
            // the stack, so track via a shim: callers only reach here for
            // <li> nodes, and non-map <li> nodes are excluded by depth
            // once the first map is found. To stay conservative, accept
            // an <li> only when it carries a mapInfo child - detected
            // lazily by the caller's depth tracking.
            return reader.Depth == 3;
        }
    }
}
