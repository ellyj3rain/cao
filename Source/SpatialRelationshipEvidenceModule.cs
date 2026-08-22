using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // LEARNED SPATIAL RELATIONSHIPS, AS BOUNDED CONDITIONAL EVIDENCE.
    //
    // The autonomous-building contract routes construction through
    //   native placement validity -> learned spatial relationships ->
    //   candidate ranking
    // and this module is the middle term's runtime face. The governed
    // layout corpus (train partition only, lineage-safe) is mined offline
    // into quantile bands per relationship and condition bucket; the game
    // ships only that non-reconstructive aggregate. At runtime a consumer
    // asks for the band that matches its situation (biome group, cohort
    // scale quartile, falling back to the whole partition) and scores an
    // otherwise-valid candidate against it. Scores are per-relationship
    // and bounded: evidence ranks candidates that native validity already
    // admitted, it never gates validity and never collapses into a single
    // settlement-quality scalar. A missing evidence file degrades to
    // no-preference, never to failure.
    internal static class CASpatialRelationshipEvidence
    {
        private static bool loadAttempted;
        private static Dictionary<string, Dictionary<string,
            CASpatialEvidenceBand>> relationships;
        private static int[] scaleThresholds = { 200, 450, 900 };
        private static string provenance = "unloaded";

        internal static bool Available
        {
            get { EnsureLoaded(); return relationships != null; }
        }

        internal static string Provenance
        {
            get { EnsureLoaded(); return provenance; }
        }

        internal static bool TryBand(string key, string biomeGroup,
            string quartile, out CASpatialEvidenceBand band)
        {
            band = default;
            EnsureLoaded();
            if (relationships == null
                || !relationships.TryGetValue(key, out var bands))
                return false;
            if (bands.TryGetValue("biome:" + biomeGroup + "|scale:"
                    + quartile, out band)) return true;
            if (bands.TryGetValue("biome:" + biomeGroup, out band))
                return true;
            if (bands.TryGetValue("scale:" + quartile, out band))
                return true;
            return bands.TryGetValue("all", out band);
        }

        // +2 inside the interquartile core, +1 inside the 80% band, 0 in
        // the near margin, -1 far outside. Per relationship; consumers
        // combine only what they own.
        internal static int Score(double value, CASpatialEvidenceBand band)
        {
            if (value >= band.P25 && value <= band.P75) return 2;
            if (value >= band.P10 && value <= band.P90) return 1;
            double iqr = Math.Max(1e-6, band.P75 - band.P25);
            if (value >= band.P10 - 1.5 * iqr
                && value <= band.P90 + 1.5 * iqr) return 0;
            return -1;
        }

        internal static string BiomeGroupFor(Map map)
        {
            string biome = map?.Biome?.defName ?? "";
            if (biome.IndexOf("Tropical",
                StringComparison.OrdinalIgnoreCase) >= 0) return "Tropical";
            if (biome.IndexOf("Temperate",
                StringComparison.OrdinalIgnoreCase) >= 0)
                return "Temperate";
            if (biome.IndexOf("Desert",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || biome.IndexOf("Arid",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return "Arid";
            if (biome.IndexOf("Boreal",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || biome.IndexOf("Tundra",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || biome.IndexOf("Ice",
                    StringComparison.OrdinalIgnoreCase) >= 0
                || biome.IndexOf("Cold",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return "Cold";
            return "Other";
        }

        internal static string QuartileFor(int placedThingScale)
        {
            EnsureLoaded();
            if (placedThingScale <= scaleThresholds[0]) return "Q1";
            if (placedThingScale <= scaleThresholds[1]) return "Q2";
            if (placedThingScale <= scaleThresholds[2]) return "Q3";
            return "Q4";
        }

        internal static string Describe(string key,
            CASpatialEvidenceBand band, double value, int score)
        {
            return key + " " + value.ToString("F1") + " vs ["
                + band.P10.ToString("F1") + ".." + band.P90.ToString("F1")
                + "] p50 " + band.P50.ToString("F1") + " (n=" + band.N
                + ") -> " + (score >= 0 ? "+" : "") + score;
        }

        private static void EnsureLoaded()
        {
            if (loadAttempted) return;
            loadAttempted = true;
            try
            {
                string path = null;
                foreach (ModContentPack mod in
                    LoadedModManager.RunningModsListForReading)
                {
                    string candidate = Path.Combine(mod.RootDir,
                        "Evidence", "spatial-relationships.xml");
                    if (File.Exists(candidate)) { path = candidate; break; }
                }
                if (path == null)
                {
                    provenance = "no Evidence/spatial-relationships.xml in "
                        + "any running mod; evidence ranking inert";
                    Log.Message("[CA][Evidence] " + provenance);
                    return;
                }
                var document = new XmlDocument();
                document.Load(path);
                XmlElement root = document.DocumentElement;
                if (root == null) return;
                string thresholds = root.GetAttribute(
                    "scaleQuartileThresholds");
                if (!thresholds.NullOrEmpty())
                {
                    string[] parts = thresholds.Split(',');
                    if (parts.Length == 3)
                        scaleThresholds = new[]
                        {
                            int.Parse(parts[0]), int.Parse(parts[1]),
                            int.Parse(parts[2])
                        };
                }
                var loaded = new Dictionary<string, Dictionary<string,
                    CASpatialEvidenceBand>>(StringComparer.Ordinal);
                foreach (XmlNode node in root.SelectNodes("relationship"))
                {
                    if (!(node is XmlElement relationship)) continue;
                    var bands = new Dictionary<string,
                        CASpatialEvidenceBand>(StringComparer.Ordinal);
                    foreach (XmlNode bandNode in
                        relationship.SelectNodes("band"))
                    {
                        if (!(bandNode is XmlElement band)) continue;
                        bands[band.GetAttribute("bucket")] =
                            new CASpatialEvidenceBand
                            {
                                N = int.Parse(band.GetAttribute("n")),
                                P10 = Parse(band, "p10"),
                                P25 = Parse(band, "p25"),
                                P50 = Parse(band, "p50"),
                                P75 = Parse(band, "p75"),
                                P90 = Parse(band, "p90")
                            };
                    }
                    if (bands.Count > 0)
                        loaded[relationship.GetAttribute("key")] = bands;
                }
                relationships = loaded;
                provenance = "cohort "
                    + root.GetAttribute("sourceCohort") + " partition "
                    + root.GetAttribute("partition") + "; layouts "
                    + root.GetAttribute("layoutsMeasured") + "; "
                    + loaded.Count + " relationships";
                Log.Message("[CA][Evidence] loaded " + provenance);
            }
            catch (Exception e)
            {
                relationships = null;
                provenance = "load failed: " + e.GetType().Name;
                Log.Warning("[CA][Evidence] " + provenance + ": "
                    + e.Message);
            }
        }

        private static double Parse(XmlElement element, string attribute)
        {
            return double.Parse(element.GetAttribute(attribute),
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    internal struct CASpatialEvidenceBand
    {
        internal int N;
        internal double P10;
        internal double P25;
        internal double P50;
        internal double P75;
        internal double P90;
    }
}
