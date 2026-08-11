using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    internal static class CAStartingFacilityCatalog
    {
        internal sealed class Program
        {
            internal int Bit;
            internal string Label;
            internal string Description;
            internal string IconPath;
        }

        internal static readonly Program[] All =
        {
            new Program { Bit = CAStartingFacilities.MaskHearth,
                Label = "Hearth", Description = "A working kitchen or communal hearth.",
                IconPath = "Rimshare/WorldMapIcons/flowers" },
            new Program { Bit = CAStartingFacilities.MaskStores,
                Label = "Stores", Description = "Shelving and preserved reserves; enables emergency provisions.",
                IconPath = "Rimshare/WorldMapIcons/divided-square" },
            new Program { Bit = CAStartingFacilities.MaskInfirmary,
                Label = "Infirmary", Description = "A dedicated room for local medical care.",
                IconPath = "Rimshare/WorldMapIcons/american-shield" },
            new Program { Bit = CAStartingFacilities.MaskWorkshop,
                Label = "Workshop", Description = "Supports local production.",
                IconPath = "Rimshare/WorldMapIcons/factory" },
            new Program { Bit = CAStartingFacilities.MaskJail,
                Label = "Jail", Description = "A secure holding room.",
                IconPath = "Rimshare/WorldMapIcons/caged-ball" },
            new Program { Bit = CAStartingFacilities.MaskDining,
                Label = "Dining hall", Description = "Tables for shared meals.",
                IconPath = "Rimshare/WorldMapIcons/carnival-mask" },
            new Program { Bit = CAStartingFacilities.MaskLab,
                Label = "Laboratory", Description = "Supports local research.",
                IconPath = "Rimshare/WorldMapIcons/atom" }
        };
    }

    // One population group, one editor, one independent control per fact.
    // Affiliation, Ideoligion, and political belief intentionally do not
    // infer or overwrite one another.
    internal sealed class Dialog_CAPopulationGroupEditor : Window
    {
        private readonly CARegionalPlan plan;
        private readonly CARegionalSettlementPlan settlement;
        private readonly CASettlementPopulationGroup population;
        private readonly Action changed;
        private readonly Action remove;
        private Vector2 scroll;
        private float viewHeight;

        internal Dialog_CAPopulationGroupEditor(CARegionalPlan plan,
            CARegionalSettlementPlan settlement,
            CASettlementPopulationGroup population, Action changed,
            Action remove)
        {
            this.plan = plan;
            this.settlement = settlement;
            this.population = population;
            this.changed = changed;
            this.remove = remove;
            doCloseX = true;
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(900f, UI.screenWidth - 48f),
            Mathf.Min(680f, UI.screenHeight - 48f));

        public override void DoWindowContents(Rect inRect)
        {
            if (plan == null || settlement == null || population == null)
            {
                Widgets.Label(inRect, "This population group is no longer available.");
                return;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f),
                population.label ?? "Population group");
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.74f, 0.78f, 0.82f);
            const string introduction = "Set the group's size, settlement "
                + "pattern, affiliation, Ideoligion, and political beliefs. "
                + "Belief sources may follow affiliation or remain independent.";
            float introductionHeight = Text.CalcHeight(introduction,
                inRect.width);
            Widgets.Label(new Rect(0f, 38f, inRect.width,
                introductionHeight), introduction);
            GUI.color = Color.white;

            bool main = population.kind == CAPopulationGroupKind.Main;
            float bodyTop = 38f + introductionHeight + 10f;
            const float footerHeight = 55f;
            Rect outRect = new Rect(0f, bodyTop, inRect.width,
                Mathf.Max(80f, inRect.height - bodyTop - footerHeight));
            Rect view = new Rect(0f, 0f, outRect.width - 18f,
                Mathf.Max(outRect.height, viewHeight));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            if (!main)
            {
                int otherShares = settlement.populationGroups.Where(item =>
                        item != null && item != population
                        && item.kind != CAPopulationGroupKind.Main)
                    .Sum(item => Math.Max(0, item.share));
                int maximum = CACreationFlowContracts.MaximumMinorityShare(
                    otherShares);
                int[] shares = new[] { 5, 10, 20, 35 }
                    .Where(value => value <= maximum)
                    .Concat(new[] { population.share })
                    .Where(value => value > 0 && value <= maximum)
                    .Distinct().OrderBy(value => value).ToArray();
                if (shares.Length == 0)
                    ReadOnlyRow(ref y, view.width, "Population share", "0%",
                        "Other groups already use the available minority "
                            + "share. Reduce one of them or remove this group.");
                else
                    SegmentRow(ref y, view.width, "Population share",
                        "Share of this settlement's residents. At least 20% "
                            + "remains for the main population.",
                        shares.Select(value => value + "%").ToArray(),
                        ShareIndex(population.share, shares), index =>
                        {
                            population.share = shares[index];
                            MarkChanged();
                        });
            }
            else
                ReadOnlyRow(ref y, view.width, "Population share",
                    population.share + "%",
                    "The main population receives the remainder after "
                    + "minority shares are set.");

            SegmentRow(ref y, view.width, "Ideoligion certainty",
                "Directly changes conversion resistance.",
                new[] { "Low", "Normal", "High" },
                Mathf.Clamp(population.ideoligionCertainty, 0, 2), index =>
                {
                    population.ideoligionCertainty = index;
                    MarkChanged();
                });

            if (!main)
                SegmentRow(ref y, view.width, "Settlement pattern",
                    "A separate quarter develops its local services separately.",
                    new[] { "Mixed throughout", "Separate quarter" },
                    population.quarter ? 1 : 0, index =>
                    {
                        population.quarter = index == 1;
                        MarkChanged();
                    });

            ChoiceRow(ref y, view.width, "Faction affiliation",
                AffiliationWords(),
                "Membership only. Belief sources marked 'Follows affiliation' "
                    + "change with it.",
                main ? null : OpenAffiliations,
                main ? "Owned settlement" : "Chosen");
            ChoiceRow(ref y, view.width, "Ideoligion",
                IdeoligionWords(),
                "Sets religious and moral belief only.", OpenIdeoligions,
                IdeoligionSourceState());
            ChoiceRow(ref y, view.width, "Political beliefs",
                PoliticalWords(),
                "Sets what this population considers proper; it does not "
                    + "change the settlement's realized order.", OpenPolitics,
                PoliticalSourceState());

            viewHeight = y + 8f;
            Widgets.EndScrollView();

            if (!main && remove != null)
            {
                Rect removeRect = new Rect(0f, inRect.height - 42f,
                    190f, 34f);
                if (Widgets.ButtonText(removeRect, "Remove population group"))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "Remove " + (population.label ?? "this population group")
                        + " and its starting provisions?", delegate
                        {
                            remove();
                            Close();
                        }, destructive: true));
                }
            }
        }

        private void MarkChanged()
        {
            population.authored = true;
            changed?.Invoke();
        }

        private static int ShareIndex(int share, int[] values)
        {
            if (values == null || values.Length == 0) return -1;
            int best = 0;
            int distance = int.MaxValue;
            for (int i = 0; i < values.Length; i++)
            {
                int candidate = Math.Abs(values[i] - share);
                if (candidate < distance)
                { distance = candidate; best = i; }
            }
            return best;
        }

        private static void SegmentRow(ref float y, float width,
            string label, string explanation, string[] options,
            int selected, Action<int> choose)
        {
            const float labelWidth = 250f;
            Widgets.Label(new Rect(0f, y + 1f, labelWidth - 12f, 22f), label);
            Text.Font = GameFont.Tiny;
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(0f, y + 23f, labelWidth - 12f, 22f),
                explanation);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            CACreationUI.DrawSegment(new Rect(labelWidth, y + 4f,
                width - labelWidth, 34f), options, selected, choose);
            y += 52f;
        }

        private static void ReadOnlyRow(ref float y, float width,
            string label, string value, string explanation)
        {
            const float labelWidth = 250f;
            Widgets.Label(new Rect(0f, y + 1f, labelWidth - 12f, 22f), label);
            Text.Font = GameFont.Tiny;
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(0f, y + 23f, labelWidth - 12f, 22f),
                explanation);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Rect valueRect = new Rect(labelWidth, y + 4f,
                width - labelWidth, 34f);
            Widgets.DrawLightHighlight(valueRect);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(valueRect.ContractedBy(10f, 0f), value);
            Text.Anchor = TextAnchor.UpperLeft;
            y += 52f;
        }

        private static void ChoiceRow(ref float y, float width, string label,
            string value, string explanation, Action edit, string state)
        {
            const float labelWidth = 250f;
            Widgets.Label(new Rect(0f, y + 1f, labelWidth - 12f, 22f), label);
            Text.Font = GameFont.Tiny;
            GUI.color = ColoredText.SubtleGrayColor;
            Widgets.Label(new Rect(0f, y + 23f, labelWidth - 12f, 24f),
                explanation);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            const float stateWidth = 132f;
            Rect button = new Rect(labelWidth, y + 4f,
                width - labelWidth - stateWidth - 10f, 34f);
            if (edit == null)
            {
                Widgets.DrawLightHighlight(button);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(button.ContractedBy(10f, 0f), value);
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else if (Widgets.ButtonText(button, value ?? "Not set"))
                edit();
            CACreationUI.DrawChip(new Rect(width - stateWidth, y + 11f,
                stateWidth, 20f), state, edit == null
                    ? CACreationUI.Generated : CACreationUI.Authored);
            y += 56f;
        }

        private string AffiliationWords()
        {
            CARegionalFactionPlan faction = plan.FactionPlan(
                population.factionKey);
            return faction == null ? "None"
                : CARegionalPlanUtility.FactionName(faction);
        }

        private string IdeoligionWords()
        {
            if (population.independentIdeoligionKey >= 0)
                return "Independent Ideoligion";
            CARegionalFactionPlan source = plan.FactionPlan(
                CACreationFlowContracts.EffectiveSourceKey(
                    population.ideoligionFactionKey, population.factionKey));
            return source?.LivingIdeo?.name
                ?? (source == null ? "Individual beliefs"
                    : "Generated from "
                        + CARegionalPlanUtility.FactionName(source));
        }

        private string PoliticalWords()
        {
            CARegionalFactionPlan source = plan.FactionPlan(
                CACreationFlowContracts.EffectiveSourceKey(
                    population.politicalBeliefsFactionKey,
                    population.factionKey));
            if (source != null)
                return CAPoliticalBeliefsModel.Summary(source.politicalBeliefs);
            return population.politicalBeliefsId.NullOrEmpty()
                ? "None set" : population.politicalBeliefsId;
        }

        private string IdeoligionSourceState()
        {
            return population.independentIdeoligionKey < 0
                && CACreationFlowContracts.FollowsAffiliation(
                    population.ideoligionFactionKey)
                ? "Follows affiliation" : "Independent";
        }

        private string PoliticalSourceState()
        {
            return population.politicalBeliefsId.NullOrEmpty()
                && CACreationFlowContracts.FollowsAffiliation(
                    population.politicalBeliefsFactionKey)
                ? "Follows affiliation" : "Independent";
        }

        private void OpenAffiliations()
        {
            var options = new List<CACreationChoice>();
            foreach (CARegionalFactionPlan faction in plan.factions
                .Where(item => item != null).OrderBy(item => item.key))
            {
                CARegionalFactionPlan local = faction;
                options.Add(new CACreationChoice
                {
                    Key = local.key.ToString(),
                    Name = CARegionalPlanUtility.FactionName(local),
                    Summary = local.TechnologySummary,
                    Traits = CAFactionAxes.Characterize(plan, local),
                    Details = "Changes faction membership. Ideoligion and "
                        + "political beliefs change only when their source is "
                        + "set to Use faction affiliation.",
                    Badge = "Faction affiliation",
                    Accent = CARegionalWorldOverlay.FactionColor(local.key),
                    Selected = population.factionKey == local.key,
                    ConfirmLabel = "Use this affiliation",
                    Choose = delegate
                    {
                        population.factionKey = local.key;
                        population.kind = local.key == settlement.factionKey
                            ? CAPopulationGroupKind.LocalResidents
                            : CAPopulationGroupKind.OtherFaction;
                        population.label = CARegionalPlanUtility.FactionName(local);
                        MarkChanged();
                    }
                });
            }
            options.Add(new CACreationChoice
            {
                Key = "none",
                Name = "No faction affiliation",
                Summary = "Residents do not belong to any faction.",
                Details = "Belief sources set to Use faction affiliation "
                    + "become individual until another affiliation is chosen.",
                Badge = "Faction affiliation",
                Icon = CACreationUI.Icon("Rimshare/WorldMapIcons/anarchy"),
                Accent = CACreationUI.Unset,
                Selected = population.factionKey < 0,
                ConfirmLabel = "Leave unaffiliated",
                Choose = delegate
                {
                    population.factionKey = -1;
                    population.kind = CAPopulationGroupKind.Unaffiliated;
                    population.label = "Unaffiliated";
                    MarkChanged();
                }
            });
            CACreationUI.OpenChoices("Faction affiliation",
                "Choose membership. Ideoligion and political beliefs follow "
                + "only when their source is set to Use faction affiliation.",
                options);
        }

        private void OpenIdeoligions()
        {
            var options = new List<CACreationChoice>();
            options.Add(new CACreationChoice
            {
                Key = "affiliation",
                Name = "Use faction affiliation",
                Summary = "Use the Ideoligion of the affiliated faction.",
                Badge = "Ideoligion source",
                Icon = plan.FactionPlan(population.factionKey)?.LivingIdeo?.Icon,
                Accent = CACreationUI.Generated,
                Selected = population.ideoligionFactionKey < 0
                    && population.independentIdeoligionKey < 0,
                ConfirmLabel = "Use affiliated Ideoligion",
                Choose = delegate
                {
                    population.ideoligionFactionKey = -1;
                    population.independentIdeoligionKey = -1;
                    MarkChanged();
                }
            });
            foreach (CARegionalFactionPlan faction in plan.factions
                .Where(item => item != null).OrderBy(item => item.key))
            {
                CARegionalFactionPlan local = faction;
                options.Add(new CACreationChoice
                {
                    Key = "faction:" + local.key,
                    Name = local.LivingIdeo?.name ?? "Generated Ideoligion",
                    Summary = "Ideoligion of "
                        + CARegionalPlanUtility.FactionName(local) + ".",
                    Badge = "Ideoligion source",
                    Icon = local.LivingIdeo?.Icon,
                    Accent = CARegionalWorldOverlay.FactionColor(local.key),
                    Selected = population.independentIdeoligionKey < 0
                        && population.ideoligionFactionKey == local.key,
                    ConfirmLabel = "Use this Ideoligion",
                    Choose = delegate
                    {
                        population.ideoligionFactionKey = local.key;
                        population.independentIdeoligionKey = -1;
                        MarkChanged();
                    }
                });
            }
            options.Add(new CACreationChoice
            {
                Key = "independent",
                Name = "Independent Ideoligion",
                Summary = "Generate a separate Ideoligion for this group.",
                Badge = "Ideoligion source",
                Icon = CACreationUI.Icon("Rimshare/WorldMapIcons/forward-sun"),
                Accent = CACreationUI.Authored,
                Selected = population.independentIdeoligionKey >= 0,
                ConfirmLabel = "Use an independent Ideoligion",
                Choose = delegate
                {
                    population.independentIdeoligionKey =
                        settlement.slot * 100 + population.key;
                    population.ideoligionFactionKey = -1;
                    MarkChanged();
                }
            });
            CACreationUI.OpenChoices("Ideoligion",
                "Choose religious and moral belief only. Faction affiliation "
                + "and political beliefs remain unchanged.", options);
        }

        private void OpenPolitics()
        {
            var options = new List<CACreationChoice>();
            options.Add(new CACreationChoice
            {
                Key = "affiliation",
                Name = "Use faction affiliation",
                Summary = "Use the political beliefs of the affiliated faction.",
                Badge = "Political-belief source",
                Accent = CACreationUI.Generated,
                Selected = population.politicalBeliefsFactionKey < 0
                    && population.politicalBeliefsId.NullOrEmpty(),
                ConfirmLabel = "Use affiliated beliefs",
                Choose = delegate
                {
                    population.politicalBeliefsFactionKey = -1;
                    population.politicalBeliefsId = null;
                    MarkChanged();
                }
            });
            foreach (CARegionalFactionPlan faction in plan.factions
                .Where(item => item != null).OrderBy(item => item.key))
            {
                CARegionalFactionPlan local = faction;
                options.Add(new CACreationChoice
                {
                    Key = local.key.ToString(),
                    Name = CAPoliticalBeliefsModel.Summary(
                        local.politicalBeliefs),
                    Summary = "Political beliefs of "
                        + CARegionalPlanUtility.FactionName(local) + ".",
                    Traits = CAPoliticalBeliefsModel.PresetTraits(
                        CAFactionAxes.Preset(
                            local.politicalBeliefs?.presetName), 3),
                    Badge = "Political-belief source",
                    Icon = CAPoliticalBeliefsModel.Icon(local.politicalBeliefs),
                    Accent = CARegionalWorldOverlay.FactionColor(local.key),
                    Selected = population.politicalBeliefsFactionKey
                        == local.key,
                    ConfirmLabel = "Use these political beliefs",
                    Choose = delegate
                    {
                        population.politicalBeliefsFactionKey = local.key;
                        population.politicalBeliefsId = null;
                        MarkChanged();
                    }
                });
            }
            CACreationUI.OpenChoices("Political beliefs",
                "Choose what this population considers proper. Ideoligion, "
                + "faction affiliation, and realized social order remain "
                + "unchanged.", options);
        }
    }

    internal sealed class Dialog_CAStartingFacilities : Window
    {
        private readonly CARegionalPlan plan;
        private readonly CARegionalSettlementPlan settlement;
        private readonly Action changed;
        private Vector2 scroll;
        private float viewHeight;

        internal Dialog_CAStartingFacilities(CARegionalPlan plan,
            CARegionalSettlementPlan settlement, Action changed)
        {
            this.plan = plan;
            this.settlement = settlement;
            this.changed = changed;
            doCloseX = true;
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(860f, UI.screenWidth - 48f),
            Mathf.Min(650f, UI.screenHeight - 48f));

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f),
                "Starting facilities");
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.74f, 0.78f, 0.82f);
            const string introduction = "Set relative local development, then "
                + "override individual facilities where needed. The profile "
                + "adjusts generated infrastructure; it is not a facility bundle.";
            float introductionHeight = Text.CalcHeight(introduction,
                inRect.width);
            Widgets.Label(new Rect(0f, 38f, inRect.width,
                introductionHeight), introduction);
            GUI.color = Color.white;
            float actionY = 38f + introductionHeight + 8f;
            Widgets.Label(new Rect(0f, actionY + 5f, 170f, 28f),
                "Development profile");
            int profile = (int)settlement.developmentProfile + 1;
            CACreationUI.DrawSegment(new Rect(174f, actionY,
                    inRect.width - 174f, 34f),
                new[] { "Minimal", "Contextual", "Extensive" }, profile,
                index =>
                {
                    settlement.developmentProfile =
                        (CASettlementDevelopmentProfile)(index - 1);
                    changed?.Invoke();
                });
            TooltipHandler.TipRegion(new Rect(174f, actionY,
                    inRect.width - 174f, 34f),
                CASettlementStartingState.ProfileEffect(
                    settlement.developmentProfile));
            actionY += 42f;
            if (Widgets.ButtonText(new Rect(0f, actionY, 270f, 32f),
                    "Return all facilities to generated"))
            {
                CASettlementStartingState.UseDerivedFacilities(plan, settlement);
                changed?.Invoke();
            }
            int resolved = CASettlementStartingState.ResolveFacilityMask(plan,
                settlement, plan.FactionPlan(settlement.factionKey)
                    ?.ResolvedFactionDef);
            int includedCount = CAStartingFacilityCatalog.All.Count(item =>
                (resolved & item.Bit) != 0);
            int overrideCount = CAStartingFacilityCatalog.All.Count(item =>
                (settlement.startingFacilityAuthoredMask & item.Bit) != 0);
            CACreationUI.DrawChip(new Rect(282f, actionY + 6f,
                inRect.width - 282f, 20f),
                CASettlementStartingState.ProfileWords(
                    settlement.developmentProfile) + ": " + includedCount
                    + " included, " + (CAStartingFacilityCatalog.All.Length
                        - includedCount) + " omitted"
                    + (overrideCount == 0 ? "" : " · " + overrideCount
                        + " local override" + (overrideCount == 1 ? "" : "s")),
                overrideCount == 0
                    ? CACreationUI.Generated : CACreationUI.Authored);

            float bodyTop = actionY + 42f;
            Rect outRect = new Rect(0f, bodyTop, inRect.width,
                Mathf.Max(80f, inRect.height - bodyTop - 55f));
            Rect view = new Rect(0f, 0f, outRect.width - 18f,
                Mathf.Max(outRect.height, viewHeight));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            foreach (CAStartingFacilityCatalog.Program program
                in CAStartingFacilityCatalog.All)
            {
                bool authored = (settlement.startingFacilityAuthoredMask
                    & program.Bit) != 0;
                bool included = (resolved & program.Bit) != 0;
                Rect icon = new Rect(0f, y + 3f, 34f, 34f);
                Texture2D texture = CACreationUI.Icon(program.IconPath);
                if (texture != null)
                    GUI.DrawTexture(icon, texture, ScaleMode.ScaleToFit, true);
                Widgets.Label(new Rect(44f, y, 176f, 22f), program.Label);
                Text.Font = GameFont.Tiny;
                GUI.color = ColoredText.SubtleGrayColor;
                float controlsX = Mathf.Max(280f, view.width * 0.51f);
                float descriptionWidth = Mathf.Max(160f, controlsX - 52f);
                string description = program.Description + (authored
                        ? " Explicit local choice."
                        : " Generated after the "
                            + CASettlementStartingState.ProfileWords(
                                settlement.developmentProfile).ToLowerInvariant()
                            + " profile; currently "
                            + (included ? "included." : "omitted."));
                float descriptionHeight = Text.CalcHeight(description,
                    descriptionWidth);
                Widgets.Label(new Rect(44f, y + 22f, descriptionWidth,
                    descriptionHeight), description);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                float rowHeight = Mathf.Max(66f, 28f + descriptionHeight);
                int selected = !authored ? 0 : included ? 1 : 2;
                CACreationUI.DrawSegment(new Rect(controlsX,
                    y + (rowHeight - 34f) * 0.5f,
                    view.width - controlsX, 34f),
                    new[] { included ? "Generated: included"
                            : "Generated: omitted", "Include", "Omit" }, selected,
                    index =>
                    {
                        CASettlementStartingState.SetFacilityOverride(plan,
                            settlement, program.Bit,
                            index == 0 ? (bool?)null : index == 1);
                        changed?.Invoke();
                    });
                y += rowHeight;
            }
            viewHeight = y + 8f;
            Widgets.EndScrollView();
        }

    }
}
