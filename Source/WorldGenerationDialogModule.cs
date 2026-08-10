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
                float share = (p.stitchedRegionFrequencyMin
                    + p.stitchedRegionFrequencyMax) * 0.5f;
                string frequencySummary = share >= 0.65f
                    ? "Stitched regions are common"
                    : share <= 0.25f
                        ? "Mostly single-tile regions"
                        : "Some stitched regions";
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
                if (!Mathf.Approximately(p.unaffiliatedPopulationShare,
                        defaults.unaffiliatedPopulationShare)) tailored++;
                if (!Mathf.Approximately(p.reallocationSourceVariety,
                        defaults.reallocationSourceVariety)) tailored++;
                if (!Mathf.Approximately(p.localFactionChance,
                        defaults.localFactionChance)) tailored++;
                if (!Mathf.Approximately(p.regionalConflictChance,
                        defaults.regionalConflictChance)) tailored++;
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
        private const float LabelWidth = 320f;
        private const float MenuWidth = 250f;

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
            get { return new Vector2(760f, 720f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f),
                "World tendencies");
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.74f, 0.78f, 0.82f);
            Widgets.Label(new Rect(0f, 36f, inRect.width - 8f, 44f),
                "Defaults for generated regions, settlements, factions, and "
                + "off-map activity. Starting-region choices replace the "
                + "matching regional defaults; off-map activity is world-wide.");
            GUI.color = Color.white;

            Rect outRect = new Rect(0f, 84f, inRect.width,
                inRect.height - 132f);
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

            Section(ref y, width, "Regional geography");
            BandRow(ref y, width, "Generated land",
                StitchedRegionFrequencyBand(p), StitchedRegionFrequencyBands,
                v => SetStitchedRegionFrequency(p, v),
                "Changes the persisted world frequency used when generated "
                + "regions choose single-tile or stitched extent. Available "
                + "connected land constrains the result. Starting Region "
                + "sets the player's region separately.");
            BandRow(ref y, width, "Stitched region size",
                StitchedRegionSizeBand(p), StitchedRegionSizeBands,
                v => SetStitchedRegionSize(p, v),
                "Changes the requested member-area count after a generated "
                + "region is chosen as stitched. Connected land and occupied "
                + "neighbors cap the realized extent; local map scale is "
                + "independent.");

            Section(ref y, width, "Settlement patterns");
            BandRow(ref y, width, "Settlement concentration",
                p.settlementConcentration, SettlementConcentrationBands,
                v => Set(p, ref p.settlementConcentration, v),
                "Changes where generated, pool-authorized settlements are "
                + "placed. Ground capacity and access constrain placement. "
                + "Starting-region settlement positions override it.");
            BandRow(ref y, width, "Urban growth propensity",
                p.urbanGrowthPropensity, UrbanGrowthBands,
                v => Set(p, ref p.urbanGrowthPropensity, v),
                "Raises or lowers the support threshold for urban scale. "
                + "Population, land, access, services, civic development, "
                + "economic capacity, trade links, specialization, regional "
                + "role, and history remain "
                + "required facts.");

            Section(ref y, width, "Frontier holdings");
            BandRow(ref y, width, "Frontier holding frequency",
                p.frontierHoldingFrequency,
                FrontierHoldingFrequencyBands,
                v => Set(p, ref p.frontierHoldingFrequency, v),
                "Changes how many suitable, unoccupied regional areas receive "
                + "a frontier holding. It does not change major settlements "
                + "or the size of any holding.");
            BandRow(ref y, width, "Frontier holding size",
                p.frontierHoldingSize, FrontierHoldingSizeBands,
                v => Set(p, ref p.frontierHoldingSize, v),
                "Changes household size and material form after a holding "
                + "site exists. Local land capacity constrains the result; "
                + "holding count remains unchanged.");

            Section(ref y, width, "Unaffiliated populations");
            BandRow(ref y, width, "Unaffiliated residents",
                p.unaffiliatedPopulationShare,
                UnaffiliatedPopulationBands,
                v => Set(p, ref p.unaffiliatedPopulationShare, v),
                "Changes the generated resident share without faction "
                + "membership. It does not change settlement ownership, "
                + "settlement count, or faction relations.");

            Section(ref y, width, "Neighboring factions");
            BandRow(ref y, width, "Reallocation source variety",
                p.reallocationSourceVariety, ReallocationSourceVarietyBands,
                v => Set(p, ref p.reallocationSourceVariety, v),
                "Changes whether source settlements selected from the "
                + "authorized world pool repeat an owner or introduce another "
                + "eligible owner. It does not set final ownership, population "
                + "shares, settlement count, or relations.");
            BandRow(ref y, width, "Local faction formation",
                p.localFactionChance, LocalFactionBands,
                v => Set(p, ref p.localFactionChance, v),
                "Changes whether a generated owner is a new local faction or "
                + "the source settlement's existing world faction. It does "
                + "not change settlement count or placement.");
            BandRow(ref y, width, "Regional conflict",
                p.regionalConflictChance, RegionalConflictBands,
                v => Set(p, ref p.regionalConflictChance, v),
                "Changes the chance that newly generated faction pairs begin "
                + "hostile. Existing faction relations and starting-region "
                + "relation overrides remain authoritative.");

            Section(ref y, width, "Off-map activity");
            BandRow(ref y, width, "Off-map activity rate",
                p.offMapActivityRate, OffMapActivityBands,
                v => Set(p, ref p.offMapActivityRate, v),
                "Changes the share of unloaded organizations updated on each "
                + "world pulse. Loaded and player-facing organizations remain "
                + "fully active.");

            y += 6f;
            Note(ref y, width, "These choices do not change the number of "
                + "major settlements. World population still controls that.");
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
            new BandOption("Concentrated", 0.82f,
                "Generated settlements favor sites near earlier placements.")
        };
        private static readonly BandOption[] UrbanGrowthBands =
        {
            new BandOption("Low", 0.15f,
                "Urban scale requires a high support score."),
            new BandOption("Typical", 0.45f,
                "Urban scale uses the standard support threshold."),
            new BandOption("High", 0.8f,
                "Urban scale uses a lower support threshold; required facts remain.")
        };
        private static readonly BandOption[] FrontierHoldingFrequencyBands =
        {
            new BandOption("Sparse", 0.15f,
                "Few suitable unoccupied areas receive holdings."),
            new BandOption("Settled", 0.45f,
                "Suitable unoccupied areas regularly receive holdings."),
            new BandOption("Dense", 0.78f,
                "Most suitable unoccupied areas receive holdings.")
        };
        private static readonly BandOption[] FrontierHoldingSizeBands =
        {
            new BandOption("Lone cabins", 0.18f,
                "Realized holdings favor small households and simple material form."),
            new BandOption("Mixed holdings", 0.5f,
                "Land capacity decides between cabins and worked homesteads."),
            new BandOption("Established homesteads", 0.82f,
                "Capable sites favor larger households and established form.")
        };
        private static readonly BandOption[] UnaffiliatedPopulationBands =
        {
            new BandOption("Few", 0.15f,
                "Most residents belong to a faction."),
            new BandOption("Mixed", 0.45f,
                "Some settlements include unaffiliated residents."),
            new BandOption("Many", 0.8f,
                "Unaffiliated residents are common.")
        };
        private static readonly BandOption[] ReallocationSourceVarietyBands =
        {
            new BandOption("Mostly one source", 0.15f,
                "Source selection favors settlements of an owner already represented."),
            new BandOption("Mixed sources", 0.5f,
                "Source selection mixes repeated and different owners."),
            new BandOption("Varied sources", 0.82f,
                "Source selection favors owners not yet represented.")
        };
        private static readonly BandOption[] LocalFactionBands =
        {
            new BandOption("Rare", 0.15f,
                "Generated ownership usually retains the source world faction."),
            new BandOption("Occasional", 0.45f,
                "Some generated owners become new local factions."),
            new BandOption("Common", 0.78f,
                "Generated owners often become new local factions.")
        };
        private static readonly BandOption[] RegionalConflictBands =
        {
            new BandOption("Mostly peaceful", 0.12f,
                "New faction pairs usually receive neutral relations."),
            new BandOption("Mixed", 0.4f,
                "New faction pairs receive neutral and hostile relations."),
            new BandOption("Contentious", 0.75f,
                "New faction pairs often receive hostile relations.")
        };
        private static readonly BandOption[] OffMapActivityBands =
        {
            new BandOption("Quiet", 0.2f,
                "A small share of unloaded organizations updates each pulse."),
            new BandOption("Active", 0.5f,
                "Half of unloaded organizations update each pulse."),
            new BandOption("Busy", 0.82f,
                "Most unloaded organizations update each pulse.")
        };
        private static readonly BandOption[] StitchedRegionFrequencyBands =
        {
            new BandOption("Mostly separate", 0.12f,
                "Most generated regions occupy one world tile."),
            new BandOption("Some stitched regions", 0.4f,
                "Single-tile and larger connected regions both occur."),
            new BandOption("Commonly stitched", 0.72f,
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
            Text.Font = oldFont;
            float bandHeight = Row + 4f + descriptionHeight;
            Rect row = new Rect(0f, y, width, bandHeight);
            Widgets.Label(new Rect(0f, y + 4f, LabelWidth, Row), label);
            if (Widgets.ButtonText(
                    new Rect(LabelWidth + Gap, y, MenuWidth, Row),
                    options[idx].Name))
            {
                var menu = new List<FloatMenuOption>();
                foreach (BandOption option in options)
                {
                    BandOption local = option;
                    menu.Add(new FloatMenuOption(local.Name + " - "
                        + local.Description, delegate
                    {
                        set(local.Value);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(menu));
            }
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.70f, 0.74f, 0.79f);
            Widgets.Label(new Rect(LabelWidth + Gap, y + Row + 2f,
                descriptionWidth, descriptionHeight),
                options[idx].Description);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(row, (tip ?? "") + "\n\n"
                + options[idx].Description);
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
