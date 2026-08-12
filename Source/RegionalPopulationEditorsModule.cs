using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
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
                    "A separate quarter keeps distinct households, gathering "
                        + "space, and provision nodes.",
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
                    + "change the settlement's current rules.", OpenPolitics,
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
                        + " and its provision arrangements?", delegate
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
            float explanationHeight = Text.CalcHeight(explanation,
                labelWidth - 12f);
            Widgets.Label(new Rect(0f, y + 23f, labelWidth - 12f,
                    explanationHeight),
                explanation);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            CACreationUI.DrawSegment(new Rect(labelWidth, y + 4f,
                width - labelWidth, 34f), options, selected, choose);
            y += Mathf.Max(46f, 27f + explanationHeight);
        }

        private static void ReadOnlyRow(ref float y, float width,
            string label, string value, string explanation)
        {
            const float labelWidth = 250f;
            Widgets.Label(new Rect(0f, y + 1f, labelWidth - 12f, 22f), label);
            Text.Font = GameFont.Tiny;
            GUI.color = ColoredText.SubtleGrayColor;
            float explanationHeight = Text.CalcHeight(explanation,
                labelWidth - 12f);
            Widgets.Label(new Rect(0f, y + 23f, labelWidth - 12f,
                    explanationHeight),
                explanation);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Rect valueRect = new Rect(labelWidth, y + 4f,
                width - labelWidth, 34f);
            Widgets.DrawLightHighlight(valueRect);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(valueRect.ContractedBy(10f, 0f), value);
            Text.Anchor = TextAnchor.UpperLeft;
            y += Mathf.Max(46f, 27f + explanationHeight);
        }

        private static void ChoiceRow(ref float y, float width, string label,
            string value, string explanation, Action edit, string state)
        {
            const float labelWidth = 250f;
            Widgets.Label(new Rect(0f, y + 1f, labelWidth - 12f, 22f), label);
            Text.Font = GameFont.Tiny;
            GUI.color = ColoredText.SubtleGrayColor;
            float explanationHeight = Text.CalcHeight(explanation,
                labelWidth - 12f);
            Widgets.Label(new Rect(0f, y + 23f, labelWidth - 12f,
                    explanationHeight),
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
            y += Mathf.Max(50f, 27f + explanationHeight);
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
                    Traits = CAPoliticalBeliefsModel.Summary(
                        local.politicalBeliefs),
                    Badge = "Political-belief source",
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
                + "faction affiliation, and current settlement rules remain "
                + "unchanged.", options);
        }
    }

}
