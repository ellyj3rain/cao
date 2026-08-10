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
                if (!Mathf.Approximately(p.settlementPatternTendency,
                        defaults.settlementPatternTendency)) tailored++;
                if (!Mathf.Approximately(p.cityFormationChance,
                        defaults.cityFormationChance)) tailored++;
                if (!Mathf.Approximately(p.frontierSettlementFrequency,
                        defaults.frontierSettlementFrequency)) tailored++;
                if (!Mathf.Approximately(p.frontierSettlementSize,
                        defaults.frontierSettlementSize)) tailored++;
                if (!Mathf.Approximately(p.unaffiliatedPopulationShare,
                        defaults.unaffiliatedPopulationShare)) tailored++;
                if (!Mathf.Approximately(p.nearbyFactionVariety,
                        defaults.nearbyFactionVariety)) tailored++;
                if (!Mathf.Approximately(p.newLocalFactionChance,
                        defaults.newLocalFactionChance)) tailored++;
                if (!Mathf.Approximately(p.startingConflictChance,
                        defaults.startingConflictChance)) tailored++;
                if (!Mathf.Approximately(p.distantActivity,
                        defaults.distantActivity)) tailored++;
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
                + "off-map activity. Starting-region choices override them.");
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
                "Generated land appears as single-tile maps or larger "
                + "stitched regions. Starting Region sets the player's "
                + "region separately.");
            BandRow(ref y, width, "Stitched region size",
                StitchedRegionSizeBand(p), StitchedRegionSizeBands,
                v => SetStitchedRegionSize(p, v),
                "Land spanned by a generated stitched region.");

            Section(ref y, width, "Settlement patterns");
            BandRow(ref y, width, "Regional pattern",
                p.settlementPatternTendency, SettlementPatternBands,
                v => Set(p, ref p.settlementPatternTendency, v),
                "Generated settlements form a shared cluster or gather "
                + "around one main settlement. Existing settlements are not "
                + "moved.");
            BandRow(ref y, width, "City formation",
                p.cityFormationChance, CityBands,
                v => Set(p, ref p.cityFormationChance, v),
                "Chance that a suitable large town becomes a city. Requires "
                + "enough population, land, and trade.");

            Section(ref y, width, "Frontier settlements");
            BandRow(ref y, width, "Frontier settlement frequency",
                p.frontierSettlementFrequency,
                FrontierSettlementFrequencyBands,
                v => Set(p, ref p.frontierSettlementFrequency, v),
                "Frequency of isolated holdings on suitable land between "
                + "towns.");
            BandRow(ref y, width, "Frontier settlement size",
                p.frontierSettlementSize, FrontierSettlementSizeBands,
                v => Set(p, ref p.frontierSettlementSize, v),
                "Generated frontier sites range from cabins to developed "
                + "homesteads.");

            Section(ref y, width, "Unaffiliated populations");
            BandRow(ref y, width, "Unaffiliated residents",
                p.unaffiliatedPopulationShare,
                UnaffiliatedPopulationBands,
                v => Set(p, ref p.unaffiliatedPopulationShare, v),
                "Typical share of residents without a faction.");

            Section(ref y, width, "Neighboring factions");
            BandRow(ref y, width, "Nearby faction variety",
                p.nearbyFactionVariety, NearbyFactionVarietyBands,
                v => Set(p, ref p.nearbyFactionVariety, v),
                "Frequency of different factions owning nearby settlements.");
            BandRow(ref y, width, "New local factions",
                p.newLocalFactionChance, NewFactionBands,
                v => Set(p, ref p.newLocalFactionChance, v),
                "Frequency of new local factions instead of existing world "
                + "factions.");
            BandRow(ref y, width, "Starting relations",
                p.startingConflictChance, StartingRelationBands,
                v => Set(p, ref p.startingConflictChance, v),
                "At peace with one another, or already at odds.");

            Section(ref y, width, "Distant activity");
            BandRow(ref y, width, "Distant world activity",
                p.distantActivity, DistantActivityBands,
                v => Set(p, ref p.distantActivity, v),
                "How often factions and settlements outside the starting "
                + "region change.");

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

        private static readonly BandOption[] SettlementPatternBands =
        {
            new BandOption("Shared cluster", 0.2f,
                "Settlements usually form one shared cluster."),
            new BandOption("Mixed", 0.5f,
                "Clusters and regions with one main settlement are both likely."),
            new BandOption("One main settlement", 0.82f,
                "Settlements usually gather around one main settlement.")
        };
        private static readonly BandOption[] CityBands =
        {
            new BandOption("Rare", 0.15f,
                "Only unusually well-supported towns become cities."),
            new BandOption("Occasional", 0.45f,
                "Cities form where population, land, and trade align."),
            new BandOption("Common", 0.8f,
                "Suitable towns readily grow into cities.")
        };
        private static readonly BandOption[] FrontierSettlementFrequencyBands =
        {
            new BandOption("Sparse", 0.15f,
                "Only a few lone holdings stand between towns."),
            new BandOption("Settled", 0.45f,
                "Homesteads appear regularly on workable frontier ground."),
            new BandOption("Dense", 0.78f,
                "Many independent holdings occupy suitable wild ground.")
        };
        private static readonly BandOption[] FrontierSettlementSizeBands =
        {
            new BandOption("Lone cabins", 0.18f,
                "Frontier holdings are usually simple household sites."),
            new BandOption("Mixed holdings", 0.5f,
                "Cabins and worked farmsteads both appear."),
            new BandOption("Established homesteads", 0.82f,
                "Frontier sites include developed working holdings.")
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
        private static readonly BandOption[] NearbyFactionVarietyBands =
        {
            new BandOption("Mostly one faction", 0.15f,
                "Nearby settlements usually belong to the same faction."),
            new BandOption("Several factions", 0.5f,
                "Several factions commonly hold nearby settlements."),
            new BandOption("Highly mixed", 0.82f,
                "Many factions hold settlements in the same region.")
        };
        private static readonly BandOption[] NewFactionBands =
        {
            new BandOption("Rare", 0.15f,
                "Few settlements introduce a new local faction."),
            new BandOption("Occasional", 0.45f,
                "New local factions appear here and there."),
            new BandOption("Common", 0.78f,
                "Many settlements introduce a new local faction.")
        };
        private static readonly BandOption[] StartingRelationBands =
        {
            new BandOption("Mostly peaceful", 0.12f,
                "Most neighboring factions begin without a standing dispute."),
            new BandOption("Mixed", 0.4f,
                "Peaceful relations and active rivalries both occur."),
            new BandOption("Contentious", 0.75f,
                "Standing disputes are common between neighbors.")
        };
        private static readonly BandOption[] DistantActivityBands =
        {
            new BandOption("Quiet", 0.2f,
                "Distant factions and settlements change rarely."),
            new BandOption("Active", 0.5f,
                "Distant factions and settlements change regularly."),
            new BandOption("Busy", 0.82f,
                "Distant factions and settlements change often.")
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
                + "Starting-region choices override them.");
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            TooltipHandler.TipRegion(new Rect(0f, y, half, 76f),
                "Set broad tendencies for generated regions: scale, "
                + "settlement patterns, frontier holdings, factions, and "
                + "distant-world activity.");
        }
    }
}
