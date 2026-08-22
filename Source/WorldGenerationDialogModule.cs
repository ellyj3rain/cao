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

    // THE AUTHORED-STATE VOCABULARY. The world itself is carried by the
    // rendered reference scene (CAReferenceWorldScene); this library
    // holds the shared naming - state words per tendency, the state
    // name, and the semantic tones: ink for authored, warm for state
    // derived from authored values, blue solely for what a generated
    // world rolled.
    internal static class CAWorldAuthoring
    {
        internal static readonly Color Consequence =
            new Color(0.73f, 0.70f, 0.58f);
        internal static readonly Color Realized =
            new Color(0.56f, 0.70f, 0.84f);
        private static readonly Color SingleTone =
            new Color(0.28f, 0.31f, 0.34f);

        // Per-tendency naming shared by chips, cards, and comparisons.
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
                    + p.stitchedRegionSizeMax + " areas"
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

        // The eight state words as one line: the authored state read at
        // a glance, in the page's own type.
        internal static string StateLine(CARegionalWorldPolicy p)
        {
            return string.Join(" · ", Dimensions.Select(
                dimension => dimension.Word(p)));
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

        // The numbers live once, in CAWorldCharacterValues, so the
        // deterministic receipts exercise the same table the game
        // applies rather than a copy that can drift away from it.
        private CAWorldCharacterValues Values
        {
            get { return CAWorldCharacterValues.ByKey(Key); }
        }

        internal void Apply(CARegionalWorldPolicy p)
        {
            CAWorldCharacterValues values = Values;
            p.stitchedRegionFrequencyMin = values.FrequencyMin;
            p.stitchedRegionFrequencyMax = values.FrequencyMax;
            p.stitchedRegionSizeMin = values.SpanMin;
            p.stitchedRegionSizeMax = values.SpanMax;
            p.settlementConcentration = values.Concentration;
            p.urbanGrowthPropensity = values.Urban;
            p.frontierHoldingFrequency = values.FrontierFrequency;
            p.frontierHoldingSize = values.FrontierSize;
            p.reallocationSourceVariety = values.Variety;
            p.offMapActivityRate = values.OffMap;
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
            CAWorldCharacterValues values = Values;
            return value.stitchedRegionSizeMin == values.SpanMin
                && value.stitchedRegionSizeMax == values.SpanMax
                && Mathf.Approximately(value.stitchedRegionFrequencyMin,
                    values.FrequencyMin)
                && Mathf.Approximately(value.stitchedRegionFrequencyMax,
                    values.FrequencyMax)
                && Mathf.Approximately(value.settlementConcentration,
                    values.Concentration)
                && Mathf.Approximately(value.urbanGrowthPropensity,
                    values.Urban)
                && Mathf.Approximately(value.frontierHoldingFrequency,
                    values.FrontierFrequency)
                && Mathf.Approximately(value.frontierHoldingSize,
                    values.FrontierSize)
                && Mathf.Approximately(value.reallocationSourceVariety,
                    values.Variety)
                && Mathf.Approximately(value.offMapActivityRate,
                    values.OffMap);
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
                Summary = "A little of everything.",
            },
            new CAWorldPreset
            {
                Key = "heartlands", Name = "Settled heartlands",
                Summary = "Densely settled and well developed.",
            },
            new CAWorldPreset
            {
                Key = "city-states", Name = "City-states",
                Summary = "A few large centres, far apart.",
            },
            new CAWorldPreset
            {
                Key = "wide-marches", Name = "Wide marches",
                Summary = "Broad regions, thinly settled.",
            },
            new CAWorldPreset
            {
                Key = "open-frontier", Name = "Open frontier",
                Summary = "Sparse and unclaimed.",
            },
            new CAWorldPreset
            {
                Key = "fractured-rim", Name = "Fractured rim",
                Summary = "Scattered holdings, many factions.",
            },
            new CAWorldPreset
            {
                Key = "crossroads", Name = "Crossroads world",
                Summary = "Every faction represented, always in motion.",
            },
            new CAWorldPreset
            {
                Key = "backwater", Name = "Quiet backwater",
                Summary = "Isolated, and slow to change.",
            },
            new CAWorldPreset
            {
                Key = "imperial-marches", Name = "Imperial marches",
                Summary = "A few powers, holding a worked frontier.",
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
                return new Vector2(Mathf.Min(1180f, UI.screenWidth - 40f),
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
            top += CAOpeningTheme.Fine(x, top, w, "How the world forms "
                + "and develops beyond your direct authoring; inside "
                + "your own starting region, your Starting Region "
                + "choices take over.") + 6f;
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

            if (CAOpeningTheme.GhostButton(new Rect(x + width - 166f,
                    y + 10f, 156f, 32f), "World presets...",
                    "Nine authored starting states, each shown as the "
                    + "world it favors, compared against your current "
                    + "world."))
                Verse.Find.WindowStack.Add(
                    new Dialog_CAWorldPresets(policy));
            return h;
        }

        // ONE SHARED SCENE, MANY CONTROLS. The banded controls stand in
        // one column; beside them the representative world renders under
        // the actual kernels, and moving any control visibly changes the
        // same world. No control carries its own mini-chart; the scene is
        // the feedback.
        // THE ADVANCED SURFACE. Controls on the left in the banded
        // language; the right panel says, in plain words, what this
        // state does to the world. The world itself is seen where it
        // exists - the stitching on the globe, and the map preview of
        // the settlements these tendencies generate.
        // Two columns of controls - all eight tendencies - and the
        // plain-words panel reading the same state, three panels off
        // the same gutters.
        private void DrawWorldPicture(Rect content)
        {
            float controlsW = Mathf.Min(354f,
                (content.width - 24f) * 0.32f);
            DrawControlsColumn(new Rect(content.x, content.y, controlsW,
                content.height), true);
            DrawControlsColumn(new Rect(content.x + controlsW + 12f,
                content.y, controlsW, content.height), false);
            DrawEffectPanel(new Rect(
                content.x + (controlsW + 12f) * 2f, content.y,
                content.width - (controlsW + 12f) * 2f,
                content.height));
        }

        private void DrawControlsColumn(Rect rect, bool firstHalf)
        {
            CAOpeningTheme.SurfacePanel(rect);
            Rect inner = rect.ContractedBy(10f);
            float x = inner.x;
            float w = inner.width;
            float y = inner.y;
            if (!firstHalf)
            {
                DrawControlsSecondHalf(x, y, w);
                return;
            }

            CAOpeningTheme.SectionLabel(x, y, w, "The land");
            y += 20f;
            Header(x, ref y, w, "Joined regions",
                CAWorldAuthoring.Dimensions[0].Word(policy),
                "How much of the world's land joins into multi-area "
                + "regions. The world rolls one share from your band "
                + "when it forms, then divides every part of its land "
                + "accordingly — the regions you select and enter on "
                + "the world map.");
            var range = new FloatRange(policy.stitchedRegionFrequencyMin,
                policy.stitchedRegionFrequencyMax);
            var rangeRect = new Rect(x, y, w - 4f, 28f);
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
            Header(x, ref y, w, "Region reach",
                CAWorldAuthoring.Dimensions[1].Word(policy),
                "How many connected areas a joined region spans. A "
                + "region's whole span becomes one continuous map when "
                + "it is entered; coast and blocked land cap it.");
            var span = new IntRange(policy.stitchedRegionSizeMin,
                policy.stitchedRegionSizeMax);
            Widgets.IntRange(new Rect(x, y, w - 4f, 28f), 91731745,
                ref span, 1, 10);
            if (span.min != policy.stitchedRegionSizeMin
                || span.max != policy.stitchedRegionSizeMax)
            {
                policy.stitchedRegionSizeMin = span.min;
                policy.stitchedRegionSizeMax = span.max;
                CAWorldTendenciesSession.MarkEdited();
            }
            y += 34f;

            CAOpeningTheme.SectionLabel(x, y, w, "Settlement");
            y += 20f;
            Header(x, ref y, w, "Settlement gathering",
                CAWorldAuthoring.Dimensions[2].Word(policy),
                "Where the world's settlements are placed when it forms "
                + "— standing apart or drawing together — and where "
                + "distant peoples later found new ones. Good ground "
                + "always scores alongside.");
            Slider(x, ref y, w, policy.settlementConcentration,
                v => SetValue(ref policy.settlementConcentration, v),
                "spread out", "clustered");
            Header(x, ref y, w, "Urban development",
                CAWorldAuthoring.Dimensions[3].Word(policy),
                "Where the town and city bars stand. Every settlement's "
                + "standing — hamlet to city, shown when you inspect it "
                + "— is judged against these bars from its real support "
                + "facts; the bars cannot supply support a place lacks.");
            Slider(x, ref y, w, policy.urbanGrowthPropensity,
                v => SetValue(ref policy.urbanGrowthPropensity, v),
                "hard-won", "comes easily");
        }

        // What the current state actually does, in the order a player
        // meets it: the land divides, settlements stand and grow, the
        // frontier fills, the wider world moves.
        internal static List<string> EffectLines(
            CARegionalWorldPolicy p)
        {
            int threshold = CAWorldTendencyCausalKernel.UrbanThreshold(
                p.urbanGrowthPropensity);
            float share = p.realizedStitchedRegionFrequency >= 0f
                ? p.realizedStitchedRegionFrequency
                : CAWorldAuthoring.FrequencyMid(p);
            return new List<string>
            {
                "About " + (share * 100f).ToString("F0") + "% of the "
                + "world's land lies in regions that span several areas "
                + "— " + p.stitchedRegionSizeMin + " to "
                + p.stitchedRegionSizeMax + " of them — and each such "
                + "region becomes one continuous map when you enter it. "
                + "The rest of the land stands as single areas.",

                p.settlementConcentration >= 0.65f
                    ? "Settlements draw together: neighbours share "
                    + "country, and long empty stretches lie between "
                    + "the clusters."
                    : p.settlementConcentration <= 0.35f
                    ? "Settlements stand apart, each seeking its own "
                    + "good ground across the world."
                    : "Settlements spread evenly — neither crowded nor "
                    + "isolated.",

                "Where the bar sits for a settlement to count as a "
                + "town or a city, judged from what each place actually "
                + "has - its people, land, access, services and trade. "
                + "You read a settlement's standing by inspecting it on "
                + "the world map. Bar: " + threshold + " of 137 for a "
                + "town, " + (threshold + 12) + " for a city.",

                CAWorldTendencyCausalKernel.FrontierHoldingCount(8,
                    p.frontierHoldingFrequency) + " of every 8 suitable "
                + "empty areas in a region carries a frontier holding, "
                + (p.frontierHoldingSize >= 0.6f
                    ? "and those holdings are established homesteads "
                    + "where the land allows."
                    : p.frontierHoldingSize <= 0.3f
                    ? "and those holdings are lone cabins."
                    : "of mixed size."),

                "The world's settlements are founded by "
                + CAWorldTendencyCausalKernel.SourceVarietyTargetDistinct(
                    6, 5, p.reallocationSourceVariety)
                + " different peoples among every six, and "
                + (p.offMapActivityRate >= 0.65f
                    ? "the distant world stays busy — far-off "
                    + "communities keep acting and founding new places "
                    + "while you play."
                    : p.offMapActivityRate <= 0.25f
                    ? "the distant world is quiet: far-off places "
                    + "change little while you play."
                    : "the distant world keeps moving between your "
                    + "visits.")
            };
        }

        private void DrawEffectPanel(Rect rect)
        {
            CAOpeningTheme.SurfacePanel(rect);
            Rect inner = rect.ContractedBy(12f);
            float y = inner.y;
            CAOpeningTheme.SectionLabel(inner.x, y, inner.width,
                "What this world does");
            y += 24f;
            Text.Font = GameFont.Tiny;
            foreach (string line in EffectLines(policy))
            {
                GUI.color = CAOpeningTheme.TextLo;
                float h = Text.CalcHeight(line, inner.width);
                Widgets.Label(new Rect(inner.x, y, inner.width, h), line);
                y += h + 9f;
            }
            if (policy.realizedStitchedRegionFrequency >= 0f)
            {
                GUI.color = CAWorldAuthoring.Realized;
                string rolled = "This world rolled "
                    + policy.realizedStitchedRegionFrequency.ToString(
                        "F2") + " for its joined-land share.";
                float rh = Text.CalcHeight(rolled, inner.width);
                Widgets.Label(new Rect(inner.x, y, inner.width, rh),
                    rolled);
                y += rh + 9f;
            }
            GUI.color = new Color(0.45f, 0.51f, 0.56f);
            Widgets.Label(new Rect(inner.x, inner.yMax - 30f,
                inner.width, 30f), "Tendencies, not guarantees — land, "
                + "population, access, relations and scenario overrides "
                + "decide what a world actually becomes.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawControlsSecondHalf(float x, float y, float w)
        {
            CAOpeningTheme.SectionLabel(x, y, w, "The frontier");
            y += 20f;
            Header(x, ref y, w, "Frontier sites",
                CAWorldAuthoring.Dimensions[4].Word(policy),
                "How much of each region's suitable empty land carries "
                + "a frontier holding when the region realizes — real "
                + "places on the map, apart from the settlements.");
            Slider(x, ref y, w, policy.frontierHoldingFrequency,
                v => SetValue(ref policy.frontierHoldingFrequency, v),
                "sparse", "common");
            Header(x, ref y, w, "Holdings",
                CAWorldAuthoring.Dimensions[5].Word(policy),
                "Who lives in each: residents and how built-up. The "
                + "land's capacity caps both.");
            Slider(x, ref y, w, policy.frontierHoldingSize,
                v => SetValue(ref policy.frontierHoldingSize, v),
                "lone cabins", "homesteads");
            y += 4f;

            CAOpeningTheme.SectionLabel(x, y, w, "The wider world");
            y += 20f;
            Header(x, ref y, w, "Settlement origins",
                CAWorldAuthoring.Dimensions[6].Word(policy),
                "How many distinct peoples found the world's "
                + "settlements — at world creation, in regions as they "
                + "realize, and among later distant founders. Only "
                + "factions actually present can appear.");
            Slider(x, ref y, w, policy.reallocationSourceVariety,
                v => SetValue(ref policy.reallocationSourceVariety, v),
                "repeated", "varied");
            Header(x, ref y, w, "Distant world",
                CAWorldAuthoring.Dimensions[7].Word(policy),
                "How much the far-off world acts while you play: the "
                + "share of distant communities that advance each "
                + "interval, and how often distant peoples found new "
                + "settlements. Loaded places always stay live.");
            Slider(x, ref y, w, policy.offMapActivityRate,
                v => SetValue(ref policy.offMapActivityRate, v),
                "quiet", "busy");
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
                return new Vector2(
                    Mathf.Min(1240f, UI.screenWidth - 64f),
                    Mathf.Min(714f, UI.screenHeight - 64f));
            }
        }

        // ONE COMPOSED SCREEN THAT FITS ITSELF. Nine worlds as a
        // measured grid - the prose IS the card, row heights come from
        // the text - a delta line only when the selection differs from
        // the current world, and one action band in flow under the
        // content. Every text rect is measured (the fixed-height
        // subtitle clipped at larger UI scales), and the window
        // resizes to its actual content each frame, so dead space
        // cannot exist at any scale.
        private const float Gutter = 14f;
        private Vector2 gridScroll = Vector2.zero;

        public override void DoWindowContents(Rect inRect)
        {
            // MEASURE, THEN PAINT. This dialog set windowRect from
            // inside here and described itself as fitting its own
            // content. It never did: Window.WindowOnGUI assigns
            // windowRect = GUI.Window(ID, windowRect, ...), so the rect
            // is captured before this runs and the return value is
            // written back over anything set during it. The window has
            // always been InitialSize tall, and the empty band under
            // the cards was that, not a spacing choice.
            //
            // Nothing here fights the engine now. The window keeps
            // whatever size it has and paints no background of its own;
            // what the player sees is the panel below, drawn at the
            // height the content actually measures and centred in the
            // window. Content-sized by construction, at any scale.
            float edge = CAOpeningTheme.Pad + 10f;
            float contentW = inRect.width - edge * 2f;

            const string subtitle = "The kind of world to generate.";
            Text.Font = GameFont.Small;
            float subtitleH = Text.CalcHeight(subtitle, contentW);
            float headerH = 4f + 36f + subtitleH + 14f;

            const int columns = 3;
            const float cardPadX = 16f;
            const float nameBand = 36f;
            // The scrollbar width is reserved whether or not the grid
            // scrolls, so prose is measured at the width it is drawn
            // at. Measuring wide and drawing narrow makes every row
            // taller than the height it was given, and the prose spills
            // into the row beneath.
            const float scrollBar = 18f;
            float cardsW = contentW - scrollBar;
            float columnW = (cardsW - Gutter * (columns - 1)) / columns;
            float proseW = columnW - cardPadX * 2f;
            CAWorldPreset[] all = CAWorldPreset.All;
            int rows = (all.Length + columns - 1) / columns;
            var rowHeights = new float[rows];
            for (int i = 0; i < all.Length; i++)
            {
                float h = nameBand + Text.CalcHeight(
                    all[i].Summary, proseW) + 16f;
                int r = i / columns;
                if (h > rowHeights[r]) rowHeights[r] = h;
            }
            float gridHeight = 0f;
            for (int r = 0; r < rows; r++)
                gridHeight += rowHeights[r] + Gutter - 2f;

            const float bandHeight = 50f;
            float chromeH = edge + headerH + 8f + bandHeight + edge;
            float roomForGrid = Mathf.Max(140f, inRect.height - chromeH);
            bool scrolls = gridHeight > roomForGrid;
            float gridShown = scrolls ? roomForGrid : gridHeight;

            float panelH = Mathf.Min(chromeH + gridShown, inRect.height);
            var panel = new Rect(inRect.x,
                inRect.y + (inRect.height - panelH) * 0.5f,
                inRect.width, panelH);
            Widgets.DrawBoxSolid(panel, CAOpeningTheme.Ink);
            CAOpeningTheme.SurfacePanel(panel);
            Rect inner = panel.ContractedBy(edge);
            float y = inner.y + 4f;

            Text.Font = GameFont.Medium;
            GUI.color = CAOpeningTheme.TextHi;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(inner.x, y, inner.width, 34f),
                "World character");
            y += 36f;
            Text.Font = GameFont.Small;
            GUI.color = CAOpeningTheme.TextLo;
            Widgets.Label(new Rect(inner.x, y, inner.width, subtitleH),
                subtitle);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            y += subtitleH + 14f;

            var gridOuter = new Rect(inner.x, y, inner.width, gridShown);
            if (scrolls)
                Widgets.BeginScrollView(gridOuter, ref gridScroll,
                    new Rect(0f, 0f, cardsW, gridHeight));
            // Inside a scroll view the origin is the view, not the
            // window. Outside one the reserved bar width is split so
            // the grid stays centred rather than sitting a bar width to
            // the left of centre.
            float originX = scrolls ? 0f : inner.x + scrollBar * 0.5f;
            float originY = scrolls ? 0f : y;

            for (int i = 0; i < all.Length; i++)
            {
                CAWorldPreset preset = all[i];
                int r = i / columns;
                int c = i % columns;
                float rowY = originY;
                for (int rr = 0; rr < r; rr++)
                    rowY += rowHeights[rr] + Gutter - 2f;
                var card = new Rect(originX + c * (columnW + Gutter),
                    rowY, columnW, rowHeights[r]);
                bool isSelected = preset.Key == selectedKey;
                bool isCurrent = preset.Matches(policy);
                bool hover = Mouse.IsOver(card);
                Widgets.DrawBoxSolid(card, isSelected || hover
                    ? CAOpeningTheme.RaisedHover : CAOpeningTheme.Raised);
                CAOpeningTheme.Border(card, isSelected
                    ? CAOpeningTheme.Accent : CAOpeningTheme.Hairline);
                if (isSelected)
                    Widgets.DrawBoxSolid(new Rect(card.x, card.y,
                        3f, card.height), CAOpeningTheme.Accent);
                Text.Font = GameFont.Medium;
                GUI.color = CAOpeningTheme.TextHi;
                Widgets.Label(new Rect(card.x + cardPadX,
                    card.y + 7f, columnW - cardPadX - 82f, 30f),
                    preset.Name);
                Text.Font = GameFont.Small;
                if (isCurrent)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = CAOpeningTheme.AccentHover;
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(new Rect(card.xMax - 74f,
                        card.y + 12f, 60f, 18f), "current");
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;
                }
                GUI.color = isSelected
                    ? new Color(0.792f, 0.822f, 0.845f)
                    : CAOpeningTheme.TextLo;
                Widgets.Label(new Rect(card.x + cardPadX,
                        card.y + nameBand, proseW,
                        card.height - nameBand - 8f),
                    preset.Summary);
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(card))
                    selectedKey = preset.Key;
            }
            if (scrolls) Widgets.EndScrollView();
            y += gridShown + 8f;

            CAWorldPreset chosen = CAWorldPreset.ByKey(selectedKey);
            float bandY = y + 4f;
            if (CAOpeningTheme.GhostButton(new Rect(inner.x, bandY,
                    110f, 34f), "Close"))
                Close();
            if (CAOpeningTheme.GhostButton(new Rect(inner.x + 122f,
                    bandY, 192f, 34f), "Adjust tendencies...",
                    "Set the eight underlying tendencies directly, and "
                    + "read what each one does."))
                Verse.Find.WindowStack.Add(
                    new Dialog_CAWorldGeneration(policy));
            // Offered only for a world the player actually picked here.
            // The selection fell back to whichever preset was last
            // applied even after the values had been edited away from
            // it, so a world the player had deliberately altered showed
            // that preset as their selection and offered a button that
            // silently restored it - which is indistinguishable from
            // the edits having been discarded on their own.
            if (chosen != null && !chosen.Matches(policy)
                && CAOpeningTheme.PrimaryButton(new Rect(
                    inner.xMax - 200f, bandY - 1f, 200f, 36f),
                    "Use this world"))
            {
                chosen.Apply(policy);
                CAWorldTendenciesSession.MarkEdited();
                Close();
            }
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
            // Page.GetMainRect, mirrored exactly. It was reproduced
            // here 17px too tall, which put the bottom this block
            // clamps against inside the page's own Back/Next strip -
            // so on a short column the entry button could be drawn
            // over the buttons that leave the page.
            var mainRect = new Rect(0f, 45f, rect.width,
                rect.height - 38f - 45f - 17f);
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
            Widgets.Label(new Rect(x + 200f, y, columnW - 200f, 22f),
                CAWorldAuthoring.StateName(p));
            GUI.color = Color.white;
            // The state's one meaning line, in the same place the
            // editor states it.
            CAWorldPreset activePreset = CAWorldPreset.Matching(p);
            string meaning = activePreset != null ? activePreset.Summary
                : p.lastAppliedPresetKey != null
                    ? "Diverges from that preset."
                    : "Authored directly.";
            Text.Font = GameFont.Tiny;
            GUI.color = CAOpeningTheme.TextLo;
            Widgets.Label(new Rect(x + 200f, y + 20f,
                columnW - 200f, 16f), meaning);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 38f;

            // One entry, in the page's own button grammar. The world's
            // character is chosen there in plain language; the eight
            // tendencies live one door further in.
            if (Widgets.ButtonText(new Rect(x + 200f,
                    Mathf.Min(y, bottom - 30f), columnW - 200f, 30f),
                    "Choose world character..."))
                Verse.Find.WindowStack.Add(
                    new Dialog_CAWorldPresets(p));
        }
    }
}
