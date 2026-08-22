using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    internal static class CAWorldTendenciesSession
    {
        private static CARegionalWorldPolicy policy =
            new CARegionalWorldPolicy();
        private static bool operatorEdited;

        internal static CARegionalWorldPolicy Policy
        {
            get { return policy ?? (policy = new CARegionalWorldPolicy()); }
        }

        internal static bool OperatorEdited
        {
            get { return operatorEdited; }
        }

        internal static void Reset(bool operatorInitiated = false)
        {
            policy = new CARegionalWorldPolicy();
            operatorEdited = operatorInitiated;
        }

        internal static void Adopt(CARegionalWorldPolicy saved)
        {
            policy = saved?.Copy() ?? new CARegionalWorldPolicy();
            operatorEdited = false;
        }

        internal static void MarkEdited()
        {
            operatorEdited = true;
        }

        internal static string Summary
        {
            get { return CAWorldAuthoring.StateName(Policy); }
        }
    }

    // THE REPRESENTATION LIBRARY. The causal model is drawn, not
    // narrated: every picture here is computed from the same kernel
    // generation consumes and redraws live as its tendency moves. Text
    // on these surfaces labels and resolves ambiguity; the model itself
    // is carried by spatial state -- runs of joined land, gathering
    // dots, thresholds standing on a scale of real contributing facts,
    // an occupied frontier board, colored origins, a lit distant field.
    // Tones: ink for authored, warm for state derived from authored
    // values, blue solely for what a generated world rolled.
    internal static class CAWorldAuthoring
    {
        internal static readonly Color Consequence =
            new Color(0.73f, 0.70f, 0.58f);
        internal static readonly Color Realized =
            new Color(0.56f, 0.70f, 0.84f);
        internal static readonly Color BarFill =
            new Color(0.62f, 0.60f, 0.52f);
        internal static readonly Color BarRail =
            new Color(0.16f, 0.18f, 0.20f);
        private static readonly Color JoinFill =
            new Color(0.55f, 0.52f, 0.42f, 0.85f);
        private static readonly Color SingleTone =
            new Color(0.28f, 0.31f, 0.34f);
        private static readonly Color PoorTone =
            new Color(0.34f, 0.28f, 0.24f);

        // Muted, distinguishable hues for peoples-of-origin.
        internal static readonly Color[] OriginHues =
        {
            new Color(0.42f, 0.58f, 0.58f),
            new Color(0.66f, 0.55f, 0.36f),
            new Color(0.56f, 0.46f, 0.60f),
            new Color(0.50f, 0.60f, 0.42f),
            new Color(0.52f, 0.55f, 0.66f)
        };

        private static float Unit(int subject, int salt)
        {
            return CAWorldTendencyCausalKernel.Unit(7, subject, salt);
        }

        // THE LAND: one picture for both region tendencies. Cells are
        // world tiles; the join share decides how many begin joined
        // runs, the reach range sizes each run. When a world exists the
        // picture uses its actual roll and says so with the realized
        // tone.
        internal static void DrawRegionDiagram(Rect rect,
            CARegionalWorldPolicy p, bool showRollCaption = true)
        {
            bool rolled = p.realizedStitchedRegionFrequency >= 0f;
            float share = rolled
                ? Mathf.Clamp01(p.realizedStitchedRegionFrequency)
                : FrequencyMid(p);
            int cols = Mathf.Max(6, (int)(rect.width / 31f));
            int rows = Mathf.Max(2, (int)(rect.height / 20f));
            float cw = (rect.width - (cols - 1) * 3f) / cols;
            float ch = (rect.height - (rows - 1) * 3f) / rows;
            int total = cols * rows;
            int spanMin = Mathf.Min(p.stitchedRegionSizeMin,
                p.stitchedRegionSizeMax);
            int spanMax = Mathf.Max(p.stitchedRegionSizeMin,
                p.stitchedRegionSizeMax);
            Color join = rolled ? Color.Lerp(JoinFill, Realized, 0.45f)
                : JoinFill;

            int i = 0;
            while (i < total)
            {
                bool joins = Unit(i, 11) < share;
                int size = 1;
                if (joins)
                    size = Mathf.Min(total - i, spanMin + (int)(
                        Unit(i, 13) * (spanMax - spanMin + 1)));
                if (size <= 1)
                {
                    var cell = CellAt(rect, i, cols, cw, ch);
                    Widgets.DrawBoxSolid(cell, BarRail);
                    DimBorder(cell, SingleTone);
                    i++;
                    continue;
                }
                // A joined run: contiguous tiles, drawn as fused
                // segments per row.
                int first = i;
                int last = i + size - 1;
                int segment = first;
                while (segment <= last)
                {
                    int rowEnd = (segment / cols) * cols + cols - 1;
                    int stop = Mathf.Min(last, rowEnd);
                    Rect a = CellAt(rect, segment, cols, cw, ch);
                    Rect b = CellAt(rect, stop, cols, cw, ch);
                    var run = new Rect(a.x, a.y, b.xMax - a.x, ch);
                    Widgets.DrawBoxSolid(run, join);
                    segment = stop + 1;
                }
                i += size;
            }
            if (rolled && showRollCaption)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = Realized;
                Text.Anchor = TextAnchor.UpperRight;
                Widgets.Label(new Rect(rect.x, rect.yMax + 1f,
                    rect.width, 14f), "as this world rolled: "
                    + share.ToString("F2"));
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
        }

        private static Rect CellAt(Rect rect, int index, int cols,
            float cw, float ch)
        {
            int cx = index % cols;
            int cy = index / cols;
            return new Rect(rect.x + cx * (cw + 3f),
                rect.y + cy * (ch + 3f), cw, ch);
        }

        private static void DimBorder(Rect rect, Color tone)
        {
            GUI.color = tone;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;
        }

        // SETTLEMENT GATHERING: the same settlements, standing apart or
        // drawing together as the tendency moves.
        internal static void DrawGatherField(Rect rect,
            float concentration)
        {
            Widgets.DrawBoxSolid(rect, BarRail);
            var anchorA = new Vector2(rect.x + rect.width * 0.30f,
                rect.y + rect.height * 0.42f);
            var anchorB = new Vector2(rect.x + rect.width * 0.70f,
                rect.y + rect.height * 0.58f);
            for (int i = 0; i < 16; i++)
            {
                var basePos = new Vector2(
                    rect.x + 4f + Unit(i, 21) * (rect.width - 8f),
                    rect.y + 4f + Unit(i, 22) * (rect.height - 8f));
                Vector2 anchor = i % 2 == 0 ? anchorA : anchorB;
                Vector2 pos = Vector2.Lerp(basePos, anchor,
                    concentration * 0.88f);
                Widgets.DrawBoxSolid(new Rect(pos.x - 2f, pos.y - 2f,
                    4f, 4f), BarFill);
            }
        }

        // URBAN DEVELOPMENT: the support scale a settlement must climb,
        // built from its REAL contributing facts (tagged segments that
        // never move), with the town and city bars standing where this
        // tendency puts them. The floors that no tendency waives sit at
        // the bars.
        private static readonly KeyValuePair<string, int>[]
            SupportParts =
        {
            new KeyValuePair<string, int>("pop", 20),
            new KeyValuePair<string, int>("land", 12),
            new KeyValuePair<string, int>("acc", 12),
            new KeyValuePair<string, int>("svc", 18),
            new KeyValuePair<string, int>("civ", 18),
            new KeyValuePair<string, int>("eco", 15),
            new KeyValuePair<string, int>("trd", 15),
            new KeyValuePair<string, int>("spc", 9),
            new KeyValuePair<string, int>("ctr", 6),
            new KeyValuePair<string, int>("his", 12)
        };

        internal static void DrawSupportGauge(Rect rect, float urban,
            bool withTags = true)
        {
            int threshold =
                CAWorldTendencyCausalKernel.UrbanThreshold(urban);
            float railY = rect.y + 12f;
            float railH = withTags ? rect.height - 24f
                : rect.height - 12f;
            float x = rect.x;
            float perPoint = rect.width / 137f;
            bool dark = false;
            foreach (KeyValuePair<string, int> part in SupportParts)
            {
                float w = part.Value * perPoint;
                Widgets.DrawBoxSolid(new Rect(x, railY, w - 1f, railH),
                    dark ? BarRail : SingleTone);
                if (withTags)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.42f, 0.47f, 0.52f);
                    Text.Anchor = TextAnchor.UpperCenter;
                    Widgets.Label(new Rect(x - 6f, railY + railH + 1f,
                        w + 12f, 12f), part.Key);
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                }
                x += w;
                dark = !dark;
            }
            DrawThreshold(rect, perPoint, railY, railH, threshold,
                "town " + threshold);
            DrawThreshold(rect, perPoint, railY, railH, threshold + 12,
                "city " + (threshold + 12));
            var floors = new Rect(rect.x + threshold * perPoint - 24f,
                railY, 48f + 12f * perPoint, railH);
            TooltipHandler.TipRegion(floors, "Population floors never "
                + "move: towns need 500 people, cities 1200.");
        }

        private static void DrawThreshold(Rect rect, float perPoint,
            float railY, float railH, int value, string tag)
        {
            float tx = rect.x + value * perPoint;
            GUI.color = Consequence;
            Widgets.DrawBoxSolid(new Rect(tx - 1f, railY - 3f, 2f,
                railH + 6f), Consequence);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(tx - 34f, rect.y - 1f, 68f, 12f),
                tag);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        // THE FRONTIER: eight suitable areas as a board. The sites
        // tendency occupies them; the holdings tendency decides who
        // lives in each -- resident dots and build fill. The last
        // square is poor land: whatever the tendency, it caps at a
        // cabin, which is the constraint made visible.
        internal static void DrawFrontierBoard(Rect rect, float sites,
            float holds)
        {
            int occupied = CAWorldTendencyCausalKernel
                .FrontierHoldingCount(8, sites);
            float cell = Mathf.Min(rect.height,
                (rect.width - 7f * 8f) / 8f);
            for (int i = 0; i < 8; i++)
            {
                bool poor = i == 7;
                var square = new Rect(rect.x + i * (cell + 8f), rect.y,
                    cell, cell);
                Widgets.DrawBoxSolid(square, poor ? PoorTone : BarRail);
                bool filled = i < occupied;
                int capacity = poor ? 1 : 3;
                int residents = CAWorldTendencyCausalKernel
                    .FrontierResidentCount(capacity, holds);
                int material = CAWorldTendencyCausalKernel
                    .FrontierMaterialLevel(capacity, holds);
                bool homestead = CAWorldTendencyCausalKernel
                    .FrontierForm(residents, material) == 1;
                if (filled)
                {
                    Widgets.DrawBoxSolid(square.ContractedBy(3f),
                        new Color(BarFill.r, BarFill.g, BarFill.b,
                            0.25f + 0.22f * material));
                    for (int r = 0; r < residents; r++)
                        Widgets.DrawBoxSolid(new Rect(
                            square.x + 5f + r * 6f,
                            square.yMax - 9f, 4f, 4f),
                            CAOpeningTheme.TextHi);
                    if (homestead)
                        DimBorder(square, Consequence);
                }
                if (poor)
                    TooltipHandler.TipRegion(square, "Poor land: caps "
                        + "at a cabin whatever the tendency.");
            }
        }

        // ORIGINS: six sourced settlements; how many different peoples
        // they span is the count of distinct hues.
        internal static void DrawOriginPips(Rect rect, float variety)
        {
            int distinct = CAWorldTendencyCausalKernel
                .SourceVarietyTargetDistinct(6, 5, variety);
            float size = Mathf.Min(rect.height, 16f);
            for (int i = 0; i < 6; i++)
            {
                Color hue = OriginHues[
                    (i < distinct ? i : (i - distinct) % distinct)
                    % OriginHues.Length];
                Widgets.DrawBoxSolid(new Rect(rect.x + i * (size + 6f),
                    rect.y + (rect.height - size) * 0.5f, size, size),
                    hue);
            }
        }

        // THE DISTANT WORLD: twenty far-off places and peoples; the lit
        // ones advance each interval.
        internal static void DrawDistantField(Rect rect, float rate)
        {
            int active = CAWorldTendencyCausalKernel
                .OffMapActivityBudget(20, rate);
            float size = Mathf.Min((rect.height - 4f) / 2f,
                (rect.width - 9f * 4f) / 10f);
            for (int i = 0; i < 20; i++)
            {
                int cx = i % 10;
                int cy = i / 10;
                Widgets.DrawBoxSolid(new Rect(
                    rect.x + cx * (size + 4f),
                    rect.y + cy * (size + 4f), size, size),
                    i < active ? BarFill : BarRail);
            }
        }

        // The compact eight-dimension profile glyph shared by the
        // world-params card and the preset cards.
        internal sealed class Dimension
        {
            internal string Short;
            internal string Label;
            internal Func<CARegionalWorldPolicy, float> Norm;
            internal Func<CARegionalWorldPolicy, string> Word;
        }

        internal static readonly Dimension[] Dimensions =
        {
            new Dimension
            {
                Short = "join", Label = "Joined regions",
                Norm = p => FrequencyMid(p),
                Word = p => FrequencyMid(p) >= 0.6f ? "often joined"
                    : FrequencyMid(p) <= 0.22f ? "mostly separate"
                    : "mixed"
            },
            new Dimension
            {
                Short = "reach", Label = "Region reach",
                Norm = p => Mathf.Clamp01(
                    ((p.stitchedRegionSizeMin + p.stitchedRegionSizeMax)
                        * 0.5f - 1f) / 9f),
                Word = p => p.stitchedRegionSizeMin + "–"
                    + p.stitchedRegionSizeMax + " tiles"
            },
            new Dimension
            {
                Short = "gather", Label = "Settlement gathering",
                Norm = p => p.settlementConcentration,
                Word = p => p.settlementConcentration >= 0.65f
                    ? "clustered" : p.settlementConcentration <= 0.35f
                        ? "spread out" : "mixed"
            },
            new Dimension
            {
                Short = "urban", Label = "Urban development",
                Norm = p => p.urbanGrowthPropensity,
                Word = p => p.urbanGrowthPropensity >= 0.65f
                    ? "comes easily" : p.urbanGrowthPropensity <= 0.3f
                        ? "hard-won" : "typical"
            },
            new Dimension
            {
                Short = "sites", Label = "Frontier sites",
                Norm = p => p.frontierHoldingFrequency,
                Word = p => p.frontierHoldingFrequency >= 0.65f
                    ? "common" : p.frontierHoldingFrequency <= 0.28f
                        ? "sparse" : "regular"
            },
            new Dimension
            {
                Short = "holds", Label = "Holdings",
                Norm = p => p.frontierHoldingSize,
                Word = p => p.frontierHoldingSize >= 0.6f ? "homesteads"
                    : p.frontierHoldingSize <= 0.3f ? "lone cabins"
                    : "mixed holdings"
            },
            new Dimension
            {
                Short = "origin", Label = "Settlement origins",
                Norm = p => p.reallocationSourceVariety,
                Word = p => p.reallocationSourceVariety >= 0.65f
                    ? "varied peoples" : p.reallocationSourceVariety
                        <= 0.3f ? "repeated peoples" : "mixed peoples"
            },
            new Dimension
            {
                Short = "distant", Label = "Distant world",
                Norm = p => p.offMapActivityRate,
                Word = p => p.offMapActivityRate >= 0.65f ? "busy"
                    : p.offMapActivityRate <= 0.25f ? "quiet" : "active"
            }
        };

        internal static float FrequencyMid(CARegionalWorldPolicy p)
        {
            return (p.stitchedRegionFrequencyMin
                + p.stitchedRegionFrequencyMax) * 0.5f;
        }

        // THE WORLD VIGNETTE: one composed scene of the world the
        // authored tendencies favor, for the Create World page. The land
        // joins into regions at the authored (or rolled) share and
        // reach; settlements gather or spread across it, colored by
        // their peoples, the largest emerging as towns where urban
        // development allows; frontier holdings dot the empty land; the
        // strip at the right horizon is the distant world, lit where it
        // moves. Every mark derives from the same kernel generation
        // consumes.
        internal static void DrawWorldVignette(Rect rect,
            CARegionalWorldPolicy p)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.09f, 0.10f, 0.115f));
            bool rolled = p.realizedStitchedRegionFrequency >= 0f;
            float s = Mathf.Clamp(rect.height / 200f, 1f, 1.6f);
            var land = new Rect(rect.x + 6f, rect.y + 6f,
                rect.width - 30f, rect.height - 12f);
            DrawRegionDiagram(new Rect(land.x, land.y, land.width,
                land.height), p, false);

            // Settlements: gathered or spread, colored by origin, the
            // first few grown to towns where the urban bar allows.
            int threshold = CAWorldTendencyCausalKernel.UrbanThreshold(
                p.urbanGrowthPropensity);
            int towns = Mathf.Clamp(Mathf.RoundToInt(
                (72 - threshold) / 7f), 0, 3);
            int distinct = CAWorldTendencyCausalKernel
                .SourceVarietyTargetDistinct(6, 5,
                    p.reallocationSourceVariety);
            var anchorA = new Vector2(land.x + land.width * 0.32f,
                land.y + land.height * 0.40f);
            var anchorB = new Vector2(land.x + land.width * 0.68f,
                land.y + land.height * 0.62f);
            for (int i = 0; i < 12; i++)
            {
                var basePos = new Vector2(
                    land.x + 8f + Unit(i, 31) * (land.width - 16f),
                    land.y + 8f + Unit(i, 32) * (land.height - 16f));
                Vector2 pos = Vector2.Lerp(basePos,
                    i % 2 == 0 ? anchorA : anchorB,
                    p.settlementConcentration * 0.85f);
                Color hue = OriginHues[
                    (i < distinct ? i : (i - distinct)
                        % Mathf.Max(1, distinct)) % OriginHues.Length];
                bool town = i < towns;
                float size = (town ? 9f : 5f) * s;
                if (town)
                    Widgets.DrawBoxSolid(new Rect(pos.x - size * 0.5f
                        - 1f, pos.y - size * 0.5f - 1f, size + 2f,
                        size + 2f), CAOpeningTheme.TextHi);
                Widgets.DrawBoxSolid(new Rect(pos.x - size * 0.5f,
                    pos.y - size * 0.5f, size, size), hue);
            }

            // Frontier holdings on the empty land.
            int holdings = CAWorldTendencyCausalKernel
                .FrontierHoldingCount(8, p.frontierHoldingFrequency);
            int material = CAWorldTendencyCausalKernel
                .FrontierMaterialLevel(3, p.frontierHoldingSize);
            for (int i = 0; i < holdings; i++)
            {
                var pos = new Vector2(
                    land.x + 10f + Unit(i, 41) * (land.width - 20f),
                    land.y + 10f + Unit(i, 42) * (land.height - 20f));
                var square = new Rect(pos.x - 3f * s, pos.y - 3f * s,
                    6f * s, 6f * s);
                Widgets.DrawBoxSolid(square, new Color(BarFill.r,
                    BarFill.g, BarFill.b, 0.30f + 0.22f * material));
                GUI.color = SingleTone;
                Widgets.DrawBox(square);
                GUI.color = Color.white;
            }

            // The distant world at the horizon.
            int active = Mathf.RoundToInt(
                Mathf.Clamp01(p.offMapActivityRate) * 8f);
            for (int i = 0; i < 8; i++)
                Widgets.DrawBoxSolid(new Rect(rect.xMax - 16f,
                    rect.y + 8f + i * ((rect.height - 16f) / 8f),
                    8f, (rect.height - 16f) / 8f - 3f),
                    i < active ? BarFill : BarRail);

            if (rolled)
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 4f,
                    rect.height), Realized);
            GUI.color = SingleTone;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;
        }

        internal static void DrawProfile(Rect rect,
            CARegionalWorldPolicy p, bool withShortLabels = false)
        {
            int count = Dimensions.Length;
            float gap = 5f;
            float slot = (rect.width - gap * (count - 1)) / count;
            float barH = withShortLabels ? rect.height - 12f
                : rect.height;
            for (int i = 0; i < count; i++)
            {
                float x = rect.x + i * (slot + gap);
                Widgets.DrawBoxSolid(new Rect(x, rect.y, slot, barH),
                    BarRail);
                float fill = Mathf.Clamp01(Dimensions[i].Norm(p));
                Widgets.DrawBoxSolid(new Rect(x,
                    rect.y + barH * (1f - fill), slot, barH * fill),
                    BarFill);
                if (withShortLabels)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = CAOpeningTheme.TextLo;
                    Text.Anchor = TextAnchor.UpperCenter;
                    Widgets.Label(new Rect(x - gap * 0.5f,
                        rect.y + barH + 1f, slot + gap, 12f),
                        Dimensions[i].Short);
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                }
            }
        }

        internal static string StateName(CARegionalWorldPolicy value)
        {
            CAWorldPreset match = CAWorldPreset.Matching(value);
            if (match != null) return match.Name;
            CAWorldPreset origin = CAWorldPreset.ByKey(
                value?.lastAppliedPresetKey);
            return origin == null ? "Custom world"
                : "Custom — from " + origin.Name;
        }

        internal static bool IsPresetState(CARegionalWorldPolicy value)
        {
            return CAWorldPreset.Matching(value) != null;
        }
    }

    // A preset is a useful authored starting state: a position in the
    // same eight-dimension space the editor owns, never a mode.
    internal sealed class CAWorldPreset
    {
        internal string Key;
        internal string Name;
        internal string Summary;
        internal float FrequencyMin, FrequencyMax;
        internal int SpanMin, SpanMax;
        internal float Concentration, Urban, FrontierFrequency,
            FrontierSize, Variety, OffMap;

        internal void Apply(CARegionalWorldPolicy p)
        {
            p.stitchedRegionFrequencyMin = FrequencyMin;
            p.stitchedRegionFrequencyMax = FrequencyMax;
            p.stitchedRegionSizeMin = SpanMin;
            p.stitchedRegionSizeMax = SpanMax;
            p.settlementConcentration = Concentration;
            p.urbanGrowthPropensity = Urban;
            p.frontierHoldingFrequency = FrontierFrequency;
            p.frontierHoldingSize = FrontierSize;
            p.reallocationSourceVariety = Variety;
            p.offMapActivityRate = OffMap;
            p.lastAppliedPresetKey = Key;
            p.realizedStitchedRegionFrequency = -1f;
        }

        internal CARegionalWorldPolicy Sample()
        {
            var sample = new CARegionalWorldPolicy();
            Apply(sample);
            return sample;
        }

        internal bool Matches(CARegionalWorldPolicy value)
        {
            if (value == null) return false;
            return value.stitchedRegionSizeMin == SpanMin
                && value.stitchedRegionSizeMax == SpanMax
                && Mathf.Approximately(value.stitchedRegionFrequencyMin,
                    FrequencyMin)
                && Mathf.Approximately(value.stitchedRegionFrequencyMax,
                    FrequencyMax)
                && Mathf.Approximately(value.settlementConcentration,
                    Concentration)
                && Mathf.Approximately(value.urbanGrowthPropensity, Urban)
                && Mathf.Approximately(value.frontierHoldingFrequency,
                    FrontierFrequency)
                && Mathf.Approximately(value.frontierHoldingSize,
                    FrontierSize)
                && Mathf.Approximately(value.reallocationSourceVariety,
                    Variety)
                && Mathf.Approximately(value.offMapActivityRate, OffMap);
        }

        internal static CAWorldPreset Matching(
            CARegionalWorldPolicy value)
        {
            return All.FirstOrDefault(preset => preset.Matches(value));
        }

        internal static CAWorldPreset ByKey(string key)
        {
            return key == null ? null
                : All.FirstOrDefault(preset => preset.Key == key);
        }

        internal static readonly CAWorldPreset[] All =
        {
            new CAWorldPreset
            {
                Key = "balanced", Name = "Balanced world",
                Summary = "The middle of every tendency.",
                FrequencyMin = 0.25f, FrequencyMax = 0.55f,
                SpanMin = 3, SpanMax = 5, Concentration = 0.5f,
                Urban = 0.45f, FrontierFrequency = 0.45f,
                FrontierSize = 0.5f, Variety = 0.5f, OffMap = 0.5f
            },
            new CAWorldPreset
            {
                Key = "heartlands", Name = "Settled heartlands",
                Summary = "Joined, clustered land where towns and "
                    + "cities come easily.",
                FrequencyMin = 0.57f, FrequencyMax = 0.87f,
                SpanMin = 3, SpanMax = 5, Concentration = 0.82f,
                Urban = 0.8f, FrontierFrequency = 0.15f,
                FrontierSize = 0.5f, Variety = 0.82f, OffMap = 0.82f
            },
            new CAWorldPreset
            {
                Key = "city-states", Name = "City-states",
                Summary = "Dense, urban, and apart: each region stands "
                    + "alone around its own center.",
                FrequencyMin = 0f, FrequencyMax = 0.2f,
                SpanMin = 2, SpanMax = 3, Concentration = 0.85f,
                Urban = 0.9f, FrontierFrequency = 0.2f,
                FrontierSize = 0.3f, Variety = 0.35f, OffMap = 0.35f
            },
            new CAWorldPreset
            {
                Key = "wide-marches", Name = "Wide marches",
                Summary = "Broad joined countryside: far-reaching "
                    + "regions, scattered towns, a working frontier.",
                FrequencyMin = 0.6f, FrequencyMax = 0.9f,
                SpanMin = 6, SpanMax = 9, Concentration = 0.2f,
                Urban = 0.3f, FrontierFrequency = 0.6f,
                FrontierSize = 0.6f, Variety = 0.5f, OffMap = 0.5f
            },
            new CAWorldPreset
            {
                Key = "open-frontier", Name = "Open frontier",
                Summary = "Separate regions, dispersed settlement, and "
                    + "many worked homesteads.",
                FrequencyMin = 0f, FrequencyMax = 0.27f,
                SpanMin = 2, SpanMax = 3, Concentration = 0.2f,
                Urban = 0.15f, FrontierFrequency = 0.78f,
                FrontierSize = 0.82f, Variety = 0.5f, OffMap = 0.5f
            },
            new CAWorldPreset
            {
                Key = "fractured-rim", Name = "Fractured rim",
                Summary = "A churning periphery: scattered cabins, "
                    + "varied peoples, an active distant world.",
                FrequencyMin = 0.25f, FrequencyMax = 0.55f,
                SpanMin = 3, SpanMax = 5, Concentration = 0.2f,
                Urban = 0.45f, FrontierFrequency = 0.78f,
                FrontierSize = 0.18f, Variety = 0.82f, OffMap = 0.82f
            },
            new CAWorldPreset
            {
                Key = "crossroads", Name = "Crossroads world",
                Summary = "A world defined by movement: every origin "
                    + "distinct, the distant world fully alive.",
                FrequencyMin = 0.3f, FrequencyMax = 0.6f,
                SpanMin = 3, SpanMax = 6, Concentration = 0.5f,
                Urban = 0.6f, FrontierFrequency = 0.3f,
                FrontierSize = 0.4f, Variety = 1f, OffMap = 1f
            },
            new CAWorldPreset
            {
                Key = "backwater", Name = "Quiet backwater",
                Summary = "An inward, sleepy world: peoples repeat and "
                    + "little changes beyond the horizon.",
                FrequencyMin = 0.25f, FrequencyMax = 0.55f,
                SpanMin = 3, SpanMax = 5, Concentration = 0.5f,
                Urban = 0.3f, FrontierFrequency = 0.35f,
                FrontierSize = 0.5f, Variety = 0.15f, OffMap = 0.1f
            },
            new CAWorldPreset
            {
                Key = "imperial-marches", Name = "Imperial marches",
                Summary = "Metropole and marches: clustered urban cores "
                    + "commanding a heavily worked frontier.",
                FrequencyMin = 0.55f, FrequencyMax = 0.85f,
                SpanMin = 4, SpanMax = 7, Concentration = 0.75f,
                Urban = 0.85f, FrontierFrequency = 0.7f,
                FrontierSize = 0.7f, Variety = 0.3f, OffMap = 0.6f
            }
        };
    }

    // THE WORLD-AUTHORING INSTRUMENT. Four facets of one world on one
    // screen, each carried by its representation: drag a control and
    // watch the land join, the settlements gather, the town bar slide
    // along the support scale, the frontier board fill, the origins
    // color, the distant world light. Text labels; the pictures are
    // the model.
    internal sealed class Dialog_CAWorldGeneration : Window
    {
        private readonly CARegionalWorldPolicy policy;

        internal Dialog_CAWorldGeneration(CARegionalWorldPolicy policy)
        {
            this.policy = policy ?? CAWorldTendenciesSession.Policy;
            doCloseX = false;
            doWindowBackground = false;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        protected override float Margin => 0f;

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(Mathf.Min(1010f, UI.screenWidth - 40f),
                    Mathf.Min(830f, UI.screenHeight - 40f));
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.DrawBoxSolid(inRect, CAOpeningTheme.Ink);
            CAOpeningTheme.SurfacePanel(inRect);
            Rect inner = inRect.ContractedBy(CAOpeningTheme.Pad + 4f);
            float x = inner.x;
            float w = inner.width;
            float top = inner.y;

            top += CAOpeningTheme.Heading(x, top, w, "World tendencies")
                + 2f;
            top += CAOpeningTheme.Fine(x, top, w, "How the parts of the "
                + "world you leave open tend to develop; Starting Region "
                + "choices replace the matching tendency.") + 6f;
            top += DrawStatePanel(x, top, w) + 8f;

            float bottomH = 44f;
            var content = new Rect(x, top, w,
                inner.yMax - bottomH - top - 4f);
            DrawWorldPicture(content);

            CAOpeningTheme.Divider(x, inner.yMax - bottomH + 2f, w);
            Text.Font = GameFont.Tiny;
            GUI.color = CAOpeningTheme.TextLo;
            Widgets.Label(new Rect(x, inner.yMax - 30f, w - 170f, 24f),
                "Tendencies, not guarantees — land, population, access, "
                + "relations, and scenario overrides decide realized "
                + "outcomes.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (CAOpeningTheme.PrimaryButton(new Rect(inner.xMax - 150f,
                    inner.yMax - 36f, 150f, 34f), "Done"))
                Close();
        }

        private float DrawStatePanel(float x, float y, float width)
        {
            const float h = 52f;
            var panel = new Rect(x, y, width, h);
            Widgets.DrawBoxSolid(panel, CAOpeningTheme.Raised);
            CAOpeningTheme.Border(panel, CAOpeningTheme.Hairline);
            CAWorldPreset active = CAWorldPreset.Matching(policy);
            float px = x + 10f;
            Text.Font = GameFont.Small;
            GUI.color = active != null ? CAOpeningTheme.TextHi
                : CAOpeningTheme.AccentHover;
            Widgets.Label(new Rect(px, y + 6f, 320f, 22f),
                CAWorldAuthoring.StateName(policy));
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            GUI.color = CAOpeningTheme.TextLo;
            string meaning = active != null ? active.Summary
                : policy.lastAppliedPresetKey != null
                    ? "Diverges from that preset below."
                    : "Authored directly.";
            Widgets.Label(new Rect(px, y + 28f, 380f, 18f), meaning);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            var glyph = new Rect(x + width - 420f, y + 9f, 240f, 22f);
            CAWorldAuthoring.DrawProfile(glyph, policy);
            TooltipHandler.TipRegion(glyph, "This world's tendency "
                + "profile.\n\n" + string.Join("\n",
                    CAWorldAuthoring.Dimensions.Select(dimension =>
                        dimension.Short + " — " + dimension.Label)));

            if (CAOpeningTheme.GhostButton(new Rect(x + width - 166f,
                    y + 10f, 156f, 32f), "World presets...",
                    "Nine authored starting states, compared against "
                    + "your current world."))
                Verse.Find.WindowStack.Add(
                    new Dialog_CAWorldPresets(policy));
            return h;
        }

        private void DrawWorldPicture(Rect content)
        {
            float gap = 10f;
            float panelW = (content.width - gap) / 2f;
            float panelH = (content.height - gap) / 2f;
            Facet(new Rect(content.x, content.y, panelW, panelH),
                "The land", DrawLandFacet);
            Facet(new Rect(content.x + panelW + gap, content.y, panelW,
                panelH), "Settlement", DrawSettlementFacet);
            Facet(new Rect(content.x, content.y + panelH + gap, panelW,
                panelH), "The frontier", DrawFrontierFacet);
            Facet(new Rect(content.x + panelW + gap,
                content.y + panelH + gap, panelW, panelH),
                "The wider world", DrawWiderWorldFacet);
        }

        private void Facet(Rect rect, string title, Action<Rect> body)
        {
            CAOpeningTheme.SurfacePanel(rect);
            Rect inner = rect.ContractedBy(10f);
            CAOpeningTheme.SectionLabel(inner.x, inner.y, inner.width,
                title);
            body(new Rect(inner.x, inner.y + 22f, inner.width,
                inner.height - 22f));
        }

        private void DrawLandFacet(Rect rect)
        {
            float y = rect.y;
            Header(rect.x, ref y, rect.width, "Joined regions",
                CAWorldAuthoring.Dimensions[0].Word(policy),
                "How often regions join adjacent land; the world rolls "
                + "one share from your band. Occupied neighbors end a "
                + "region.");
            var range = new FloatRange(policy.stitchedRegionFrequencyMin,
                policy.stitchedRegionFrequencyMax);
            var rangeRect = new Rect(rect.x, y, rect.width - 4f, 28f);
            Widgets.FloatRange(rangeRect, 91731744, ref range, 0f, 1f,
                null, ToStringStyle.FloatTwo, 0f, GameFont.Tiny, null,
                0.01f);
            if (!Mathf.Approximately(range.min,
                    policy.stitchedRegionFrequencyMin)
                || !Mathf.Approximately(range.max,
                    policy.stitchedRegionFrequencyMax))
            {
                policy.stitchedRegionFrequencyMin = range.min;
                policy.stitchedRegionFrequencyMax = range.max;
                policy.realizedStitchedRegionFrequency = -1f;
                CAWorldTendenciesSession.MarkEdited();
            }
            if (policy.realizedStitchedRegionFrequency >= 0f)
            {
                float t = Mathf.Clamp01(
                    policy.realizedStitchedRegionFrequency);
                Widgets.DrawBoxSolid(new Rect(rangeRect.x + 8f
                    + (rangeRect.width - 16f) * t - 1f,
                    rangeRect.yMax - 13f, 2f, 11f),
                    CAWorldAuthoring.Realized);
            }
            y += 30f;
            Header(rect.x, ref y, rect.width, "Region reach",
                CAWorldAuthoring.Dimensions[1].Word(policy),
                "How many tiles a joined region asks for; adjacent free "
                + "land caps it.");
            var span = new IntRange(policy.stitchedRegionSizeMin,
                policy.stitchedRegionSizeMax);
            Widgets.IntRange(new Rect(rect.x, y, rect.width - 4f, 28f),
                91731745, ref span, 1, 10);
            if (span.min != policy.stitchedRegionSizeMin
                || span.max != policy.stitchedRegionSizeMax)
            {
                policy.stitchedRegionSizeMin = span.min;
                policy.stitchedRegionSizeMax = span.max;
                CAWorldTendenciesSession.MarkEdited();
            }
            y += 32f;
            // The land itself: tiles joining into regions at the share
            // and reach above, live.
            CAWorldAuthoring.DrawRegionDiagram(new Rect(rect.x, y,
                rect.width - 4f, rect.yMax - y - 16f), policy);
        }

        private void DrawSettlementFacet(Rect rect)
        {
            float y = rect.y;
            Header(rect.x, ref y, rect.width, "Settlement gathering",
                CAWorldAuthoring.Dimensions[2].Word(policy),
                "Whether new settlements stand apart or draw together; "
                + "their own ground and routes always score alongside.");
            Slider(rect.x, ref y, rect.width,
                policy.settlementConcentration,
                v => SetValue(ref policy.settlementConcentration, v),
                "spread out", "clustered");
            CAWorldAuthoring.DrawGatherField(new Rect(rect.x, y,
                rect.width - 4f, 40f), policy.settlementConcentration);
            y += 48f;
            Header(rect.x, ref y, rect.width, "Urban development",
                CAWorldAuthoring.Dimensions[3].Word(policy),
                "Where the town and city bars stand on the support a "
                + "settlement gathers from its real facts.");
            Slider(rect.x, ref y, rect.width,
                policy.urbanGrowthPropensity,
                v => SetValue(ref policy.urbanGrowthPropensity, v),
                "hard-won", "comes easily");
            CAWorldAuthoring.DrawSupportGauge(new Rect(rect.x, y,
                rect.width - 4f, 42f), policy.urbanGrowthPropensity);
        }

        private void DrawFrontierFacet(Rect rect)
        {
            float y = rect.y;
            Header(rect.x, ref y, rect.width, "Frontier sites",
                CAWorldAuthoring.Dimensions[4].Word(policy),
                "How many suitable empty areas gain a holding.");
            Slider(rect.x, ref y, rect.width,
                policy.frontierHoldingFrequency,
                v => SetValue(ref policy.frontierHoldingFrequency, v),
                "sparse", "common");
            Header(rect.x, ref y, rect.width, "Holdings",
                CAWorldAuthoring.Dimensions[5].Word(policy),
                "Who lives in each: residents and how built-up. The "
                + "land's capacity caps both.");
            Slider(rect.x, ref y, rect.width,
                policy.frontierHoldingSize,
                v => SetValue(ref policy.frontierHoldingSize, v),
                "lone cabins", "homesteads");
            // The board: sites fill it, holdings decide who lives in
            // each square; the last square is poor land showing its cap.
            CAWorldAuthoring.DrawFrontierBoard(new Rect(rect.x, y + 2f,
                rect.width - 4f, Mathf.Min(44f, rect.yMax - y - 6f)),
                policy.frontierHoldingFrequency,
                policy.frontierHoldingSize);
        }

        private void DrawWiderWorldFacet(Rect rect)
        {
            float y = rect.y;
            Header(rect.x, ref y, rect.width, "Settlement origins",
                CAWorldAuthoring.Dimensions[6].Word(policy),
                "How many different peoples the sourced settlements "
                + "span; only factions actually present can appear.");
            Slider(rect.x, ref y, rect.width,
                policy.reallocationSourceVariety,
                v => SetValue(ref policy.reallocationSourceVariety, v),
                "repeated", "varied");
            CAWorldAuthoring.DrawOriginPips(new Rect(rect.x, y,
                rect.width - 4f, 18f), policy.reallocationSourceVariety);
            y += 26f;
            Header(rect.x, ref y, rect.width, "Distant world",
                CAWorldAuthoring.Dimensions[7].Word(policy),
                "How much the far-off world moves between visits; "
                + "loaded places always stay live.");
            Slider(rect.x, ref y, rect.width, policy.offMapActivityRate,
                v => SetValue(ref policy.offMapActivityRate, v),
                "quiet", "busy");
            CAWorldAuthoring.DrawDistantField(new Rect(rect.x, y,
                rect.width - 4f, Mathf.Min(30f, rect.yMax - y - 2f)),
                policy.offMapActivityRate);
        }

        private void Header(float x, ref float y, float width,
            string label, string stateWord, string meaning)
        {
            Text.Font = GameFont.Small;
            GUI.color = CAOpeningTheme.TextHi;
            Widgets.Label(new Rect(x, y, width - 120f, 20f), label);
            TooltipHandler.TipRegion(new Rect(x, y, width, 20f),
                meaning);
            Text.Font = GameFont.Tiny;
            GUI.color = CAOpeningTheme.TextLo;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(x + width - 150f, y, 150f, 20f),
                stateWord);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 21f;
        }

        private void Slider(float x, ref float y, float width,
            float value, Action<float> set, string leftWord,
            string rightWord)
        {
            var sliderRect = new Rect(x, y + 13f, width - 50f, 20f);
            float next = Widgets.HorizontalSlider(sliderRect, value, 0f,
                1f, true, null, leftWord, rightWord, 0.01f);
            if (!Mathf.Approximately(next, value)) set(next);
            Text.Font = GameFont.Tiny;
            GUI.color = CAOpeningTheme.TextLo;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(x + width - 44f, y + 13f, 44f, 20f),
                next.ToString("F2"));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 36f;
        }

        private void SetValue(ref float field, float value)
        {
            field = value;
            CAWorldTendenciesSession.MarkEdited();
        }
    }

    // THE PRESET BROWSER: comparison first, reading second. Cards carry
    // the profile glyph; the pane shows the same representations twice
    // -- your world beside the preset's -- with changed pairs bright,
    // unchanged pairs dimmed, and numbers only as tags.
    internal sealed class Dialog_CAWorldPresets : Window
    {
        private readonly CARegionalWorldPolicy policy;
        private string selectedKey;

        internal Dialog_CAWorldPresets(CARegionalWorldPolicy policy)
        {
            this.policy = policy;
            CAWorldPreset current = CAWorldPreset.Matching(policy);
            selectedKey = (current ?? CAWorldPreset.ByKey(
                    policy?.lastAppliedPresetKey)
                ?? CAWorldPreset.All[0]).Key;
            doCloseX = false;
            doWindowBackground = false;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        protected override float Margin => 0f;

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(Mathf.Min(960f, UI.screenWidth - 48f),
                    Mathf.Min(760f, UI.screenHeight - 48f));
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.DrawBoxSolid(inRect, CAOpeningTheme.Ink);
            CAOpeningTheme.SurfacePanel(inRect);
            Rect inner = inRect.ContractedBy(CAOpeningTheme.Pad + 4f);
            float top = inner.y;
            top += CAOpeningTheme.Heading(inner.x, top, inner.width,
                "World presets") + 2f;
            top += CAOpeningTheme.Fine(inner.x, top, inner.width,
                "Authored starting states in the same space the editor "
                + "owns; every tendency stays editable after applying.")
                + 8f;

            float bottomH = 44f;
            float listW = 300f;
            var listRect = new Rect(inner.x, top, listW,
                inner.yMax - bottomH - top - 4f);
            var detailRect = new Rect(inner.x + listW + 14f, top,
                inner.width - listW - 14f,
                inner.yMax - bottomH - top - 4f);
            DrawList(listRect);
            DrawComparison(detailRect);

            CAOpeningTheme.Divider(inner.x, inner.yMax - bottomH + 2f,
                inner.width);
            CAWorldPreset selected = CAWorldPreset.ByKey(selectedKey);
            bool alreadyActive = selected != null
                && selected.Matches(policy);
            if (CAOpeningTheme.GhostButton(new Rect(inner.x,
                    inner.yMax - 34f, 120f, 32f), "Close"))
                Close();
            if (CAOpeningTheme.PrimaryButton(new Rect(inner.xMax - 190f,
                    inner.yMax - 36f, 190f, 34f),
                    alreadyActive ? "Already your state"
                        : "Use this preset")
                && !alreadyActive && selected != null)
            {
                selected.Apply(policy);
                CAWorldTendenciesSession.MarkEdited();
                Close();
            }
        }

        private void DrawList(Rect rect)
        {
            float y = rect.y;
            Text.Font = GameFont.Tiny;
            GUI.color = CAOpeningTheme.TextLo;
            Widgets.Label(new Rect(rect.x, y, rect.width, 16f),
                string.Join(" · ", CAWorldAuthoring.Dimensions.Select(
                    dimension => dimension.Short)));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 18f;
            float cardH = Mathf.Min(58f, (rect.height - 18f
                - 6f * (CAWorldPreset.All.Length - 1))
                / CAWorldPreset.All.Length);
            foreach (CAWorldPreset preset in CAWorldPreset.All)
            {
                var card = new Rect(rect.x, y, rect.width, cardH);
                bool isSelected = preset.Key == selectedKey;
                bool isCurrent = preset.Matches(policy);
                Widgets.DrawBoxSolid(card, isSelected
                    ? CAOpeningTheme.RaisedHover : CAOpeningTheme.Raised);
                CAOpeningTheme.Border(card, isSelected
                    ? CAOpeningTheme.Accent : CAOpeningTheme.Hairline);
                Text.Font = GameFont.Small;
                GUI.color = CAOpeningTheme.TextHi;
                Widgets.Label(new Rect(card.x + 8f, card.y + 4f,
                    card.width - 84f, 20f), preset.Name);
                GUI.color = Color.white;
                if (isCurrent)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = CAOpeningTheme.AccentHover;
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(new Rect(card.xMax - 80f, card.y + 4f,
                        72f, 18f), "current");
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                }
                CAWorldAuthoring.DrawProfile(new Rect(card.x + 8f,
                    card.y + cardH - 22f, card.width - 16f, 16f),
                    preset.Sample());
                if (Widgets.ButtonInvisible(card))
                    selectedKey = preset.Key;
                y += cardH + 6f;
            }
        }

        // Your world and the preset's, as the same pictures side by
        // side. Changed pairs draw bright with a numeric tag where the
        // number is the point; unchanged pairs dim.
        private void DrawComparison(Rect rect)
        {
            CAWorldPreset preset = CAWorldPreset.ByKey(selectedKey);
            if (preset == null) return;
            CARegionalWorldPolicy target = preset.Sample();
            bool isCurrent = preset.Matches(policy);
            float y = rect.y;
            Text.Font = GameFont.Small;
            GUI.color = CAOpeningTheme.TextHi;
            Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                preset.Name);
            GUI.color = Color.white;
            y += 20f;
            Text.Font = GameFont.Tiny;
            GUI.color = CAOpeningTheme.TextLo;
            float summaryH = Text.CalcHeight(preset.Summary, rect.width);
            Widgets.Label(new Rect(rect.x, y, rect.width, summaryH),
                preset.Summary);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += summaryH + 6f;

            float columnW = (rect.width - 116f - 10f) / 2f;
            if (!isCurrent)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = CAOpeningTheme.TextLo;
                Widgets.Label(new Rect(rect.x + 116f, y, columnW, 16f),
                    "your world");
                GUI.color = CAOpeningTheme.TextHi;
                Widgets.Label(new Rect(rect.x + 116f + columnW + 10f, y,
                    columnW, 16f), preset.Name.ToLower());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += 18f;
            }

            ComparisonRow(rect.x, ref y, rect.width, columnW, "land",
                LandChanged(policy, target), isCurrent, 46f,
                (r, p) => CAWorldAuthoring.DrawRegionDiagram(r, p),
                policy, target, ReachTag(policy, target));
            ComparisonRow(rect.x, ref y, rect.width, columnW,
                "gathering",
                CAWorldAuthoring.Dimensions[2].Word(policy)
                    != CAWorldAuthoring.Dimensions[2].Word(target),
                isCurrent, 30f, (r, p) =>
                    CAWorldAuthoring.DrawGatherField(r,
                        p.settlementConcentration), policy, target,
                null);
            int nowBar = CAWorldTendencyCausalKernel.UrbanThreshold(
                policy.urbanGrowthPropensity);
            int thenBar = CAWorldTendencyCausalKernel.UrbanThreshold(
                target.urbanGrowthPropensity);
            ComparisonRow(rect.x, ref y, rect.width, columnW, "urban",
                nowBar != thenBar, isCurrent, 34f, (r, p) =>
                    CAWorldAuthoring.DrawSupportGauge(r,
                        p.urbanGrowthPropensity, false), policy, target,
                nowBar != thenBar ? "town " + nowBar + " → " + thenBar
                    : null);
            ComparisonRow(rect.x, ref y, rect.width, columnW,
                "frontier", FrontierChanged(policy, target), isCurrent,
                34f, (r, p) => CAWorldAuthoring.DrawFrontierBoard(r,
                    p.frontierHoldingFrequency, p.frontierHoldingSize),
                policy, target, FrontierTag(policy, target));
            int nowDistinct = CAWorldTendencyCausalKernel
                .SourceVarietyTargetDistinct(6, 5,
                    policy.reallocationSourceVariety);
            int thenDistinct = CAWorldTendencyCausalKernel
                .SourceVarietyTargetDistinct(6, 5,
                    target.reallocationSourceVariety);
            ComparisonRow(rect.x, ref y, rect.width, columnW, "origins",
                nowDistinct != thenDistinct, isCurrent, 20f, (r, p) =>
                    CAWorldAuthoring.DrawOriginPips(r,
                        p.reallocationSourceVariety), policy, target,
                nowDistinct != thenDistinct
                    ? nowDistinct + " → " + thenDistinct + " peoples"
                    : null);
            int nowActive = CAWorldTendencyCausalKernel
                .OffMapActivityBudget(20, policy.offMapActivityRate);
            int thenActive = CAWorldTendencyCausalKernel
                .OffMapActivityBudget(20, target.offMapActivityRate);
            ComparisonRow(rect.x, ref y, rect.width, columnW, "distant",
                nowActive != thenActive, isCurrent, 26f, (r, p) =>
                    CAWorldAuthoring.DrawDistantField(r,
                        p.offMapActivityRate), policy, target,
                nowActive != thenActive
                    ? nowActive + " → " + thenActive + " of 20"
                    : null);

            y += 4f;
            Text.Font = GameFont.Tiny;
            GUI.color = CAOpeningTheme.TextLo;
            const string caveat = "Tendencies, not guarantees.";
            Widgets.Label(new Rect(rect.x, y, rect.width, 16f), caveat);
            y += 16f;
            GUI.color = new Color(0.45f, 0.51f, 0.56f);
            string writes = "join "
                + target.stitchedRegionFrequencyMin.ToString("F2") + "–"
                + target.stitchedRegionFrequencyMax.ToString("F2")
                + " · reach " + target.stitchedRegionSizeMin + "–"
                + target.stitchedRegionSizeMax + " · gather "
                + target.settlementConcentration.ToString("F2")
                + " · urban "
                + target.urbanGrowthPropensity.ToString("F2")
                + " · sites "
                + target.frontierHoldingFrequency.ToString("F2")
                + " · holds "
                + target.frontierHoldingSize.ToString("F2")
                + " · origin "
                + target.reallocationSourceVariety.ToString("F2")
                + " · distant "
                + target.offMapActivityRate.ToString("F2");
            float writesH = Text.CalcHeight(writes, rect.width);
            Widgets.Label(new Rect(rect.x, y, rect.width, writesH),
                writes);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void ComparisonRow(float x, ref float y, float width,
            float columnW, string label, bool changed, bool isCurrent,
            float height,
            Action<Rect, CARegionalWorldPolicy> draw,
            CARegionalWorldPolicy now, CARegionalWorldPolicy then,
            string tag)
        {
            float rowTop = y;
            Text.Font = GameFont.Tiny;
            GUI.color = CAOpeningTheme.TextLo;
            Widgets.Label(new Rect(x, y + 2f, 110f, 16f), label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (!isCurrent)
            {
                draw(new Rect(x + 116f, y, columnW, height), now);
                draw(new Rect(x + 116f + columnW + 10f, y, columnW,
                    height), then);
            }
            else
                draw(new Rect(x + 116f, y, columnW * 2f + 10f, height),
                    then);
            y += height + 2f;
            if (tag != null && !isCurrent)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = CAWorldAuthoring.Consequence;
                Widgets.Label(new Rect(x + 116f, y, columnW * 2f + 10f,
                    14f), tag);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += 15f;
            }
            // The representations draw with their own explicit colors,
            // so an unchanged pair dims under a scrim rather than a
            // tint: the difference between rows IS the comparison.
            if (!changed && !isCurrent)
                Widgets.DrawBoxSolid(new Rect(x, rowTop, width,
                    y - rowTop), new Color(CAOpeningTheme.Ink.r,
                    CAOpeningTheme.Ink.g, CAOpeningTheme.Ink.b, 0.55f));
            y += 5f;
        }

        private static bool LandChanged(CARegionalWorldPolicy now,
            CARegionalWorldPolicy then)
        {
            return Mathf.Abs(CAWorldAuthoring.FrequencyMid(now)
                    - CAWorldAuthoring.FrequencyMid(then)) >= 0.08f
                || now.stitchedRegionSizeMin != then.stitchedRegionSizeMin
                || now.stitchedRegionSizeMax
                    != then.stitchedRegionSizeMax;
        }

        private static string ReachTag(CARegionalWorldPolicy now,
            CARegionalWorldPolicy then)
        {
            if (now.stitchedRegionSizeMin == then.stitchedRegionSizeMin
                && now.stitchedRegionSizeMax
                    == then.stitchedRegionSizeMax) return null;
            return "reach " + now.stitchedRegionSizeMin + "–"
                + now.stitchedRegionSizeMax + " → "
                + then.stitchedRegionSizeMin + "–"
                + then.stitchedRegionSizeMax + " tiles";
        }

        private static bool FrontierChanged(CARegionalWorldPolicy now,
            CARegionalWorldPolicy then)
        {
            return CAWorldTendencyCausalKernel.FrontierHoldingCount(8,
                    now.frontierHoldingFrequency)
                != CAWorldTendencyCausalKernel.FrontierHoldingCount(8,
                    then.frontierHoldingFrequency)
                || CAWorldTendencyCausalKernel.FrontierResidentCount(3,
                    now.frontierHoldingSize)
                != CAWorldTendencyCausalKernel.FrontierResidentCount(3,
                    then.frontierHoldingSize);
        }

        private static string FrontierTag(CARegionalWorldPolicy now,
            CARegionalWorldPolicy then)
        {
            int a = CAWorldTendencyCausalKernel.FrontierHoldingCount(8,
                now.frontierHoldingFrequency);
            int b = CAWorldTendencyCausalKernel.FrontierHoldingCount(8,
                then.frontierHoldingFrequency);
            return a == b ? null : a + " → " + b + " of 8 areas";
        }
    }

    [HarmonyPatch(typeof(Page_CreateWorldParams), nameof(
        Page_CreateWorldParams.Reset))]
    internal static class CAWorldTendenciesResetPatch
    {
        [HarmonyPostfix]
        private static void Postfix(bool ___initialized)
        {
            // PreOpen performs the initial Reset while initialized is false.
            // A later Reset came from the operator and must win over an older
            // disk draft if the same world identity is generated again.
            CAWorldTendenciesSession.Reset(___initialized);
        }
    }

    [HarmonyPatch(typeof(Page_CreateWorldParams), nameof(
        Page_CreateWorldParams.DoWindowContents))]
    internal static class CAWorldTendenciesPagePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Rect rect)
        {
            // ONE world-authoring page, recomposed. Vanilla's planet
            // rows keep the top of the left column and factions keep
            // the right; the left column's remaining half becomes the
            // world's character: the state named in the page's own
            // label grammar, the world vignette these tendencies favor,
            // and the two entries into presets and causal authoring.
            var mainRect = new Rect(rect.x, rect.y + 45f, rect.width,
                rect.height - 45f - 38f);
            float columnW = (mainRect.width - 18f) * 0.5f;
            float rows = 240f
                + (ModsConfig.OdysseyActive ? 40f : 0f)
                + (ModsConfig.BiotechActive ? 40f : 0f)
                + (TutorSystem.TutorialMode ? 0f : 40f);
            float x = mainRect.x;
            float y = mainRect.y + rows + 14f;
            float bottom = mainRect.yMax - 8f;
            CARegionalWorldPolicy p = CAWorldTendenciesSession.Policy;

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(x, y, 200f, 30f), "World character");
            GUI.color = CAWorldAuthoring.IsPresetState(p)
                ? CAOpeningTheme.TextHi : CAOpeningTheme.AccentHover;
            Widgets.Label(new Rect(x + 200f, y, columnW - 200f, 30f),
                CAWorldAuthoring.StateName(p));
            GUI.color = Color.white;
            y += 34f;

            float buttonsY = bottom - 30f;
            float vignetteH = Mathf.Max(90f, buttonsY - 8f - y);
            var vignette = new Rect(x, y, columnW, vignetteH);
            CAWorldAuthoring.DrawWorldVignette(vignette, p);
            TooltipHandler.TipRegion(vignette, "The world these "
                + "tendencies favor: how its land joins, where "
                + "settlement gathers and grows, who founded it, how "
                + "worked its frontier is, and how alive the distant "
                + "world stays.");

            float buttonW = (columnW - 10f) * 0.5f;
            if (Widgets.ButtonText(new Rect(x, buttonsY, buttonW, 30f),
                    "World presets..."))
                Verse.Find.WindowStack.Add(
                    new Dialog_CAWorldPresets(p));
            if (Widgets.ButtonText(new Rect(x + buttonW + 10f,
                    buttonsY, buttonW, 30f), "Author tendencies..."))
                Verse.Find.WindowStack.Add(new Dialog_CAWorldGeneration(
                    CAWorldTendenciesSession.Policy));
        }
    }
}
