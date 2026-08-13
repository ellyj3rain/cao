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
            get
            {
                CARegionalWorldPolicy p = Policy;
                string profile = Dialog_CAWorldGeneration.ProfileName(p);
                if (!profile.NullOrEmpty()) return profile;
                float share = (p.stitchedRegionFrequencyMin
                    + p.stitchedRegionFrequencyMax) * 0.5f;
                string frequencySummary = share >= 0.65f
                    ? "Connected regions are common"
                    : share <= 0.25f
                        ? "Mostly single-tile regions"
                        : "Some connected regions";
                var defaults = new CARegionalWorldPolicy();
                int tailored = 0;
                if (p.stitchedRegionSizeMin != defaults.stitchedRegionSizeMin
                    || p.stitchedRegionSizeMax != defaults.stitchedRegionSizeMax) tailored++;
                if (!Mathf.Approximately(p.settlementConcentration,
                        defaults.settlementConcentration)) tailored++;
                if (!Mathf.Approximately(p.urbanGrowthPropensity,
                        defaults.urbanGrowthPropensity)) tailored++;
                if (!Mathf.Approximately(p.frontierHoldingFrequency,
                        defaults.frontierHoldingFrequency)) tailored++;
                if (!Mathf.Approximately(p.frontierHoldingSize,
                        defaults.frontierHoldingSize)) tailored++;
                if (!Mathf.Approximately(p.reallocationSourceVariety,
                        defaults.reallocationSourceVariety)) tailored++;
                if (!Mathf.Approximately(p.offMapActivityRate,
                        defaults.offMapActivityRate)) tailored++;
                return frequencySummary + (tailored == 0 ? " · defaults elsewhere"
                    : " · " + tailored + " other tailored choice"
                        + (tailored == 1 ? "" : "s"));
            }
        }
    }

    // Named generation bands for parts of the world the player leaves open.
    internal sealed class Dialog_CAWorldGeneration : Window
    {
        private readonly CARegionalWorldPolicy policy;
        private Vector2 scroll;

        private const float Row = 30f;
        private const float Gap = 8f;
        private const float LabelWidth = 250f;

        internal Dialog_CAWorldGeneration(CARegionalWorldPolicy policy)
        {
            this.policy = policy ?? CAWorldTendenciesSession.Policy;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(Mathf.Min(980f, UI.screenWidth - 48f),
                    Mathf.Min(820f, UI.screenHeight - 48f));
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f),
                "World tendencies");
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.74f, 0.78f, 0.82f);
            Widgets.Label(new Rect(0f, 36f, inRect.width - 8f, 44f),
                "Set defaults for places the player does not author directly. "
                + "Each control changes one cause; Starting Region choices "
                + "replace the matching default.");
            GUI.color = Color.white;

            CACreationUI.DrawFlow(new Rect(0f, 86f, inRect.width, 24f), 0);
            if (Widgets.ButtonText(new Rect(0f, 118f, 210f, 32f),
                    "World presets..."))
                OpenWorldPresets();
            if (Widgets.ButtonText(new Rect(220f, 118f, 190f, 32f),
                    "Restore neutral profile"))
                ApplyProfile(NeutralProfile);
            string profile = ProfileName(policy) ?? "Custom profile";
            CACreationUI.DrawChip(new Rect(422f, 124f,
                inRect.width - 422f, 20f), profile,
                ProfileName(policy) == null ? CACreationUI.Authored
                    : CACreationUI.Preset);

            Rect outRect = new Rect(0f, 160f, inRect.width,
                inRect.height - 208f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 20f,
                measuredHeight);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            try
            {
                float y = 0f;
                DrawGroups(ref y, viewRect.width);
                measuredHeight = Mathf.Max(measuredHeight, y + 8f);
            }
            finally { Widgets.EndScrollView(); }

            if (Widgets.ButtonText(new Rect(inRect.width - 170f,
                    inRect.height - 42f, 170f, 38f), "Done"))
                Close();
        }

        private float measuredHeight = 600f;

        private void DrawGroups(ref float y, float width)
        {
            CARegionalWorldPolicy p = policy;

            Section(ref y, width, "Regions");
            BandRow(ref y, width, "Connected regions",
                StitchedRegionFrequencyBand(p), StitchedRegionFrequencyBands,
                v => SetStitchedRegionFrequency(p, v),
                "Direct effect: how often generated regions join adjacent "
                + "world tiles. Connected unoccupied land limits the result. "
                + "Local map size is independent.");
            BandRow(ref y, width, "Region span",
                StitchedRegionSizeBand(p), StitchedRegionSizeBands,
                v => SetStitchedRegionSize(p, v),
                "Direct effect: how many world tiles a connected region asks "
                + "to include. Available adjacent land caps the final span. "
                + "Region frequency and local map size are independent.");

            Section(ref y, width, "Settlements");
            BandRow(ref y, width, "Settlement spacing",
                p.settlementConcentration, SettlementConcentrationBands,
                v => Set(p, ref p.settlementConcentration, v),
                "Direct effect: placement favors open ground or earlier "
                + "settlements. Land capacity, access, and the world "
                + "settlement pool constrain placement. It does not add "
                + "settlements.");
            BandRow(ref y, width, "Urban development",
                p.urbanGrowthPropensity, UrbanGrowthBands,
                v => Set(p, ref p.urbanGrowthPropensity, v),
                "Direct effect: raises or lowers the support threshold for "
                + "urban scale. Population, land, access, services, civic "
                + "development, trade, specialization, regional role, and "
                + "history remain required.");

            Section(ref y, width, "Frontier sites");
            BandRow(ref y, width, "Frontier sites",
                p.frontierHoldingFrequency,
                FrontierHoldingFrequencyBands,
                v => Set(p, ref p.frontierHoldingFrequency, v),
                "Direct effect: how many suitable unoccupied areas receive a "
                + "frontier site. It does not change major settlements, the "
                + "site's residents, or its material form.");
            BandRow(ref y, width, "Holding size",
                p.frontierHoldingSize, FrontierHoldingSizeBands,
                v => Set(p, ref p.frontierHoldingSize, v),
                "Direct effect: resident count and material form after a "
                + "frontier site exists. Local land limits the result. It "
                + "does not create family or institutional relationships; "
                + "the number of sites is independent.");

            Section(ref y, width, "Faction sources");
            BandRow(ref y, width, "Settlement origins",
                p.reallocationSourceVariety, ReallocationSourceVarietyBands,
                v => Set(p, ref p.reallocationSourceVariety, v),
                "Direct effect: how many distinct actual world-faction sources "
                + "are represented when RimWorld settlements are reallocated. "
                + "It creates no faction or relation; Starting Region authors "
                + "those facts explicitly.");

            Section(ref y, width, "Distant world");
            BandRow(ref y, width, "Distant activity",
                p.offMapActivityRate, OffMapActivityBands,
                v => Set(p, ref p.offMapActivityRate, v),
                "Direct effect: how many distant factions and settlements can "
                + "make progress between visits. Loaded and player-facing "
                + "groups remain fully active.");

            y += 6f;
            Note(ref y, width, "Major-settlement count remains controlled by "
                + "RimWorld's world-population setting and explicit scenario "
                + "overrides.");
        }

        // A world preset composes independent controls; it does not create a
        // second generation model. Applying one writes the same policy fields
        // the rows below own, and every row remains independently editable.
        private sealed class WorldProfile
        {
            internal string Name;
            internal string Summary;
            internal string Traits;
            internal string Details;
            internal string IconPath;
            internal Action<CARegionalWorldPolicy> Apply;
        }

        private static readonly WorldProfile NeutralProfile =
            new WorldProfile
            {
                Name = "Balanced world",
                Summary = "The semantic middle for every world tendency.",
                Traits = "Mixed regions · typical settlement · mixed origins",
                Details = "Every control begins at its middle choice. Land, "
                    + "population, access, existing relations, and scenario "
                    + "overrides still determine realized outcomes.",
                IconPath = "Rimshare/WorldMapIcons/compass",
                Apply = p => Configure(p, 0.4f, 0.5f, 0.5f, 0.45f,
                    0.45f, 0.5f, 0.5f, 0.5f)
            };

        private static readonly WorldProfile[] Profiles =
        {
            NeutralProfile,
            new WorldProfile
            {
                Name = "Settled heartlands",
                Summary = "Connected, clustered regions with stronger urban growth.",
                Traits = "Connected land · clustered settlements · fewer frontiers",
                Details = "Favors broad connected regions, clustered placement, "
                    + "urban development, varied actual origins, and established "
                    + "world factions. It does not add major settlements.",
                IconPath = "Rimshare/WorldMapIcons/factory",
                Apply = p => Configure(p, 0.72f, 0.5f, 0.82f, 0.8f,
                    0.15f, 0.5f, 0.82f, 0.82f)
            },
            new WorldProfile
            {
                Name = "Open frontier",
                Summary = "Separate regions with dispersed settlements and homesteads.",
                Traits = "Open ground · many homesteads · mixed origins",
                Details = "Favors separate regions, spread-out placement and "
                    + "many frontier homes. Major-settlement count, factions, "
                    + "relations, and population membership remain factual.",
                IconPath = "Rimshare/WorldMapIcons/forward-sun",
                Apply = p => Configure(p, 0.12f, 0.2f, 0.2f, 0.15f,
                    0.78f, 0.82f, 0.5f, 0.5f)
            },
            new WorldProfile
            {
                Name = "Fractured rim",
                Summary = "Dispersed settlements with varied world origins.",
                Traits = "Varied origins · open ground · active distant world",
                Details = "Favors spread-out settlements, small frontier homes, "
                    + "varied actual sources, and active distant settlements. "
                    + "It does not invent population or political state.",
                IconPath = "Rimshare/WorldMapIcons/cracked-shield",
                Apply = p => Configure(p, 0.4f, 0.5f, 0.2f, 0.45f,
                    0.78f, 0.18f, 0.82f, 0.82f)
            }
        };

        private void OpenWorldPresets()
        {
            var choices = Profiles.Select(profile =>
            {
                WorldProfile local = profile;
                return new CACreationChoice
                {
                    Key = local.Name,
                    Name = local.Name,
                    Summary = local.Summary,
                    Traits = local.Traits,
                    Details = local.Details,
                    Badge = "World preset",
                    Icon = CACreationUI.Icon(local.IconPath),
                    Accent = CACreationUI.Preset,
                    Selected = Matches(policy, local),
                    ConfirmLabel = "Use this world preset",
                    Choose = delegate { ApplyProfile(local); }
                };
            }).ToList();
            CACreationUI.OpenChoices("World presets",
                "Apply a composed starting profile. Every tendency remains "
                + "independent and editable after the preset is applied.",
                choices);
        }

        private void ApplyProfile(WorldProfile profile)
        {
            if (profile == null || policy == null) return;
            profile.Apply(policy);
            policy.realizedStitchedRegionFrequency = -1f;
            CAWorldTendenciesSession.MarkEdited();
        }

        internal static string ProfileName(CARegionalWorldPolicy value)
        {
            WorldProfile profile = Profiles.FirstOrDefault(item =>
                Matches(value, item));
            return profile?.Name;
        }

        private static bool Matches(CARegionalWorldPolicy value,
            WorldProfile profile)
        {
            if (value == null || profile == null) return false;
            var expected = new CARegionalWorldPolicy();
            profile.Apply(expected);
            return value.stitchedRegionSizeMin == expected.stitchedRegionSizeMin
                && value.stitchedRegionSizeMax == expected.stitchedRegionSizeMax
                && Mathf.Approximately(value.stitchedRegionFrequencyMin,
                    expected.stitchedRegionFrequencyMin)
                && Mathf.Approximately(value.stitchedRegionFrequencyMax,
                    expected.stitchedRegionFrequencyMax)
                && Mathf.Approximately(value.settlementConcentration,
                    expected.settlementConcentration)
                && Mathf.Approximately(value.urbanGrowthPropensity,
                    expected.urbanGrowthPropensity)
                && Mathf.Approximately(value.frontierHoldingFrequency,
                    expected.frontierHoldingFrequency)
                && Mathf.Approximately(value.frontierHoldingSize,
                    expected.frontierHoldingSize)
                && Mathf.Approximately(value.reallocationSourceVariety,
                    expected.reallocationSourceVariety)
                && Mathf.Approximately(value.offMapActivityRate,
                    expected.offMapActivityRate);
        }

        private static void Configure(CARegionalWorldPolicy p,
            float regionFrequency, float regionSize,
            float settlementSpacing, float urbanDevelopment,
            float frontierFrequency, float frontierSize,
            float sourceVariety, float offMap)
        {
            p.stitchedRegionFrequencyMin = Mathf.Clamp01(
                regionFrequency - 0.15f);
            p.stitchedRegionFrequencyMax = Mathf.Clamp01(
                regionFrequency + 0.15f);
            if (regionSize < 0.35f)
            { p.stitchedRegionSizeMin = 2; p.stitchedRegionSizeMax = 3; }
            else if (regionSize < 0.65f)
            { p.stitchedRegionSizeMin = 3; p.stitchedRegionSizeMax = 5; }
            else
            { p.stitchedRegionSizeMin = 5; p.stitchedRegionSizeMax = 8; }
            p.settlementConcentration = settlementSpacing;
            p.urbanGrowthPropensity = urbanDevelopment;
            p.frontierHoldingFrequency = frontierFrequency;
            p.frontierHoldingSize = frontierSize;
            p.reallocationSourceVariety = sourceVariety;
            p.offMapActivityRate = offMap;
        }

        // ---- named tendency bands -----------------------------------------

        private struct BandOption
        {
            internal readonly string Name;
            internal readonly float Value;
            internal readonly string Description;
            internal BandOption(string name, float value, string description)
            { Name = name; Value = value; Description = description; }
        }

        private static readonly BandOption[] SettlementConcentrationBands =
        {
            new BandOption("Spread out", 0.2f,
                "Generated settlements favor open areas farther apart."),
            new BandOption("Mixed", 0.5f,
                "Ground and access usually decide between near and far sites."),
            new BandOption("Clustered", 0.82f,
                "Generated settlements favor sites near earlier placements.")
        };
        private static readonly BandOption[] UrbanGrowthBands =
        {
            new BandOption("Harder", 0.15f,
                "Urban scale requires a high support score."),
            new BandOption("Typical", 0.45f,
                "Urban scale uses the standard support threshold."),
            new BandOption("Easier", 0.8f,
                "Urban scale uses a lower support threshold; required facts remain.")
        };
        private static readonly BandOption[] FrontierHoldingFrequencyBands =
        {
            new BandOption("Sparse", 0.15f,
                "Few suitable unoccupied areas receive holdings."),
            new BandOption("Typical", 0.45f,
                "Suitable unoccupied areas regularly receive holdings."),
            new BandOption("Common", 0.78f,
                "Most suitable unoccupied areas receive holdings.")
        };
        private static readonly BandOption[] FrontierHoldingSizeBands =
        {
            new BandOption("Lone cabins", 0.18f,
                "Realized holdings favor few residents and simple material form."),
            new BandOption("Mixed holdings", 0.5f,
                "Land capacity decides between cabins and worked homesteads."),
            new BandOption("Established homesteads", 0.82f,
                "Capable sites favor more residents and established form.")
        };
        private static readonly BandOption[] ReallocationSourceVarietyBands =
        {
            new BandOption("Repeated origins", 0.15f,
                "Source selection favors settlements of an owner already represented."),
            new BandOption("Mixed origins", 0.5f,
                "Source selection mixes repeated and different owners."),
            new BandOption("Varied origins", 0.82f,
                "Source selection favors owners not yet represented.")
        };
        private static readonly BandOption[] OffMapActivityBands =
        {
            new BandOption("Quiet", 0.2f,
                "Change among distant factions and settlements is uncommon."),
            new BandOption("Active", 0.5f,
                "Distant factions and settlements continue to change."),
            new BandOption("Busy", 0.82f,
                "Change among distant factions and settlements is frequent.")
        };
        private static readonly BandOption[] StitchedRegionFrequencyBands =
        {
            new BandOption("Mostly separate", 0.12f,
                "Most generated regions occupy one world tile."),
            new BandOption("Mixed", 0.4f,
                "Single-tile and larger connected regions both occur."),
            new BandOption("Often connected", 0.72f,
                "Larger connected regions are common across the world.")
        };
        private static readonly BandOption[] StitchedRegionSizeBands =
        {
            new BandOption("Compact", 0.2f,
                "Stitched regions usually span two or three world tiles."),
            new BandOption("Broad", 0.5f,
                "Stitched regions usually span three to five world tiles."),
            new BandOption("Sprawling", 0.85f,
                "Stitched regions may span five to eight world tiles.")
        };

        // The controls present one tendency and write its range directly.
        private static float StitchedRegionFrequencyBand(CARegionalWorldPolicy p)
        {
            return (p.stitchedRegionFrequencyMin
                + p.stitchedRegionFrequencyMax) * 0.5f;
        }
        private static void SetStitchedRegionFrequency(CARegionalWorldPolicy p, float v)
        {
            p.stitchedRegionFrequencyMin = Mathf.Clamp01(v - 0.15f);
            p.stitchedRegionFrequencyMax = Mathf.Clamp01(v + 0.15f);
            p.realizedStitchedRegionFrequency = -1f;
            CAWorldTendenciesSession.MarkEdited();
        }
        private static float StitchedRegionSizeBand(CARegionalWorldPolicy p)
        {
            if (p.stitchedRegionSizeMin == 2
                && p.stitchedRegionSizeMax == 3) return 0.2f;
            if (p.stitchedRegionSizeMin == 3
                && p.stitchedRegionSizeMax == 5) return 0.5f;
            if (p.stitchedRegionSizeMin == 5
                && p.stitchedRegionSizeMax == 8) return 0.85f;
            // A noncanonical range can still be displayed by proximity; the
            // three saved defaults and menu-written ranges map exactly.
            float mid = (p.stitchedRegionSizeMin
                + p.stitchedRegionSizeMax) * 0.5f;
            return Mathf.Clamp01((mid - 2f) / 8f);
        }
        private static void SetStitchedRegionSize(CARegionalWorldPolicy p, float v)
        {
            if (v < 0.35f)
            { p.stitchedRegionSizeMin = 2; p.stitchedRegionSizeMax = 3; }
            else if (v < 0.65f)
            { p.stitchedRegionSizeMin = 3; p.stitchedRegionSizeMax = 5; }
            else
            { p.stitchedRegionSizeMin = 5; p.stitchedRegionSizeMax = 8; }
            CAWorldTendenciesSession.MarkEdited();
        }

        private static void Set(CARegionalWorldPolicy p, ref float field,
            float value)
        {
            field = value;
            CAWorldTendenciesSession.MarkEdited();
        }

        // ---- layout primitives, hierarchy in the spacing ------------------

        private static void Section(ref float y, float width, string title)
        {
            y += 14f; // major sections get visible separation above them
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, width, 30f), title);
            Text.Font = GameFont.Small;
            y += 30f;
            GUI.color = new Color(0.32f, 0.34f, 0.38f);
            Widgets.DrawLineHorizontal(0f, y, width);
            GUI.color = Color.white;
            y += 8f;
        }

        private void BandRow(ref float y, float width, string label,
            float current, BandOption[] options, Action<float> set,
            string tip)
        {
            int idx = NearestBand(current, options);
            float descriptionWidth = width - LabelWidth - Gap;
            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            float descriptionHeight = Mathf.Max(24f, Text.CalcHeight(
                options[idx].Description, descriptionWidth));
            float contractHeight = Mathf.Max(24f, Text.CalcHeight(
                tip ?? "", descriptionWidth));
            Text.Font = oldFont;
            float bandHeight = Row + 6f + descriptionHeight
                + contractHeight + 6f;
            Rect row = new Rect(0f, y, width, bandHeight);
            Widgets.Label(new Rect(0f, y + 4f, LabelWidth, Row), label);
            CACreationUI.DrawSegment(new Rect(LabelWidth + Gap, y,
                descriptionWidth, Row),
                options.Select(item => item.Name).ToArray(), idx,
                index => set(options[index].Value));
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.80f, 0.83f, 0.87f);
            Widgets.Label(new Rect(LabelWidth + Gap, y + Row + 2f,
                descriptionWidth, descriptionHeight),
                options[idx].Description);
            GUI.color = new Color(0.61f, 0.66f, 0.71f);
            Widgets.Label(new Rect(LabelWidth + Gap,
                y + Row + 4f + descriptionHeight, descriptionWidth,
                contractHeight), tip ?? "");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(row, options[idx].Description + "\n\n"
                + (tip ?? ""));
            y += bandHeight + 4f;
        }

        private static int NearestBand(float current, BandOption[] options)
        {
            int best = 0;
            float bestDelta = float.MaxValue;
            for (int i = 0; i < options.Length; i++)
            {
                float delta = Mathf.Abs(options[i].Value - current);
                if (delta < bestDelta) { bestDelta = delta; best = i; }
            }
            return best;
        }

        private static void Note(ref float y, float width, string text)
        {
            GUI.color = new Color(0.72f, 0.75f, 0.79f);
            float h = Text.CalcHeight(text, width - 8f);
            Widgets.Label(new Rect(0f, y, width - 8f, h), text);
            GUI.color = Color.white;
            y += h + Gap;
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
            // Vanilla's final row can reach y=320 with all DLC active. This
            // world-level control deliberately sits beneath it.
            float y = 405f;
            float half = (rect.width - 18f) * 0.5f;
            float controlWidth = Mathf.Max(120f, half - 200f);
            Widgets.Label(new Rect(0f, y + 4f, 200f, 30f),
                "World tendencies");
            Rect button = new Rect(200f, y, controlWidth, 30f);
            if (Widgets.ButtonText(button,
                    CAWorldTendenciesSession.Summary + "..."))
                Verse.Find.WindowStack.Add(new Dialog_CAWorldGeneration(
                    CAWorldTendenciesSession.Policy));

            Rect note = new Rect(0f, y + 34f, half, 42f);
            GUI.color = new Color(0.70f, 0.74f, 0.79f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(note, "Defaults for generated regions and settlements. "
                + "Matching Starting Region choices replace these defaults.");
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            TooltipHandler.TipRegion(new Rect(0f, y, half, 76f),
                "Set broad tendencies for generated regions: extent, "
                + "settlement placement, frontier holdings, factions, and "
                + "distant-world activity.");
        }
    }
}
