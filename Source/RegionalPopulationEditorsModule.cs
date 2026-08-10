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
            Widgets.Label(new Rect(0f, 38f, inRect.width, 44f),
                "Set the group's size, settlement pattern, affiliation, "
                + "Ideoligion, and political beliefs. Belief sources may "
                + "follow affiliation or remain independent.");
            GUI.color = Color.white;

            float y = 92f;
            bool main = population.kind == CAPopulationGroupKind.Main;
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
                    ReadOnlyRow(ref y, inRect.width, "Population share", "0%",
                        "Other groups already use the available minority "
                            + "share. Reduce one of them or remove this group.");
                else
                    SegmentRow(ref y, inRect.width, "Population share",
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
                ReadOnlyRow(ref y, inRect.width, "Population share",
                    population.share + "%",
                    "The main population receives the remainder after "
                    + "minority shares are set.");

            SegmentRow(ref y, inRect.width, "Ideoligion certainty",
                "Directly changes conversion resistance.",
                new[] { "Low", "Normal", "High" },
                Mathf.Clamp(population.ideoligionCertainty, 0, 2), index =>
                {
                    population.ideoligionCertainty = index;
                    MarkChanged();
                });

            if (!main)
                SegmentRow(ref y, inRect.width, "Settlement pattern",
                    "A separate quarter develops its local services separately.",
                    new[] { "Mixed throughout", "Separate quarter" },
                    population.quarter ? 1 : 0, index =>
                    {
                        population.quarter = index == 1;
                        MarkChanged();
                    });

            ChoiceRow(ref y, inRect.width, "Faction affiliation",
                AffiliationWords(),
                "Membership only. Belief sources marked 'Follows affiliation' "
                    + "change with it.",
                main ? null : OpenAffiliations,
                main ? "Owned settlement" : "Chosen");
            ChoiceRow(ref y, inRect.width, "Ideoligion",
                IdeoligionWords(),
                "Sets religious and moral belief only.", OpenIdeoligions,
                IdeoligionSourceState());
            ChoiceRow(ref y, inRect.width, "Political beliefs",
                PoliticalWords(),
                "Sets what this population considers proper; it does not "
                    + "change the settlement's realized order.", OpenPolitics,
                PoliticalSourceState());

            Rect summary = new Rect(0f, y + 8f, inRect.width,
                Mathf.Min(90f, inRect.height - y - 72f));
            Widgets.DrawLightHighlight(summary);
            Text.Font = GameFont.Tiny;
            Widgets.Label(summary.ContractedBy(10f),
                "Affiliation: " + AffiliationWords()
                + "\nIdeoligion: " + IdeoligionWords()
                + "\nPolitical beliefs: " + PoliticalWords());
            Text.Font = GameFont.Small;

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
                        CAFactionAxes.Presets.FirstOrDefault(item =>
                            item.Name == local.politicalBeliefs?.presetName), 3),
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
            Widgets.Label(new Rect(0f, 38f, inRect.width, 44f),
                "Generate facilities from realized settlement facts, or "
                + "override individual programs. Each row owns one facility.");
            GUI.color = Color.white;
            if (Widgets.ButtonText(new Rect(0f, 86f, 210f, 32f),
                    "Facility presets..."))
                OpenPresets();
            CACreationUI.DrawChip(new Rect(222f, 92f,
                inRect.width - 222f, 20f),
                settlement.startingFacilityAuthoredMask == 0
                    ? "Generated" : "Customized",
                settlement.startingFacilityAuthoredMask == 0
                    ? CACreationUI.Generated : CACreationUI.Authored);

            int resolved = CASettlementStartingState.ResolveFacilityMask(plan,
                settlement, plan.FactionPlan(settlement.factionKey)
                    ?.ResolvedFactionDef);
            float y = 132f;
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
                Widgets.Label(new Rect(44f, y + 22f, 270f, 28f),
                    program.Description);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                int selected = !authored ? 0 : included ? 1 : 2;
                CACreationUI.DrawSegment(new Rect(320f, y + 5f,
                    inRect.width - 320f, 34f),
                    new[] { "Generated", "Include", "Omit" }, selected,
                    index =>
                    {
                        CASettlementStartingState.SetFacilityOverride(plan,
                            settlement, program.Bit,
                            index == 0 ? (bool?)null : index == 1);
                        changed?.Invoke();
                    });
                y += 58f;
            }
        }

        private void OpenPresets()
        {
            var options = new List<CACreationChoice>
            {
                Preset("generated", "Generated facilities",
                    "Derive every facility from population, settlement role, "
                        + "infrastructure, faction structure, and knowledge.",
                    0, true, "Rimshare/WorldMapIcons/cog"),
                Preset("sparse", "Sparse settlement",
                    "A hearth, stores, and a dining hall.",
                    CAStartingFacilities.MaskHearth
                        | CAStartingFacilities.MaskStores
                        | CAStartingFacilities.MaskDining,
                    false, "Rimshare/WorldMapIcons/flowers"),
                Preset("established", "Established settlement",
                    "Adds local care and production to the sparse program.",
                    CAStartingFacilities.MaskHearth
                        | CAStartingFacilities.MaskStores
                        | CAStartingFacilities.MaskInfirmary
                        | CAStartingFacilities.MaskWorkshop
                        | CAStartingFacilities.MaskDining,
                    false, "Rimshare/WorldMapIcons/factory"),
                Preset("all", "All starting facilities",
                    "Include every available facility program.",
                    CASettlementStartingState.AllFacilityMask,
                    false, "Rimshare/WorldMapIcons/castle")
            };
            CACreationUI.OpenChoices("Facility presets",
                "Apply a complete facility program. Individual rows remain "
                + "editable afterward.", options);
        }

        private CACreationChoice Preset(string key, string name,
            string summary, int mask, bool generated, string icon)
        {
            return new CACreationChoice
            {
                Key = key,
                Name = name,
                Summary = summary,
                Traits = generated ? "Uses realized settlement facts"
                    : FacilityNames(mask),
                Badge = "Facility preset",
                Icon = CACreationUI.Icon(icon),
                Accent = generated
                    ? CACreationUI.Generated : CACreationUI.Preset,
                Selected = generated
                    ? settlement.startingFacilityAuthoredMask == 0
                    : settlement.startingFacilityAuthoredMask
                        == CASettlementStartingState.AllFacilityMask
                        && settlement.startingFacilityMask == mask,
                ConfirmLabel = "Use this facility program",
                Choose = delegate
                {
                    if (generated)
                        CASettlementStartingState.UseDerivedFacilities(plan,
                            settlement);
                    else
                        CASettlementStartingState.ApplyFacilityPreset(plan,
                            settlement, mask);
                    changed?.Invoke();
                }
            };
        }

        private static string FacilityNames(int mask)
        {
            return string.Join(" · ", CAStartingFacilityCatalog.All
                .Where(item => (mask & item.Bit) != 0)
                .Select(item => item.Label).ToArray());
        }
    }
}
