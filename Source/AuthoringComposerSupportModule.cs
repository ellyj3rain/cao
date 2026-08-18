using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    internal sealed class CAAxisGroupDef
    {
        internal string Key;
        internal string Label;
        internal string[] Axes;
    }

    internal static class CAAuthoringChoices
    {
        internal static readonly CAAxisGroupDef[] PoliticalGroups =
        {
            new CAAxisGroupDef
            {
                Key = "governance", Label = "Governance",
                Axes = new[] { CAFactionAxes.Leadership,
                    CAFactionAxes.Decisions }
            },
            new CAAxisGroupDef
            {
                Key = "civic", Label = "Civic participation",
                Axes = new[] { CAFactionAxes.Participation,
                    CAFactionAxes.Dissent }
            },
            new CAAxisGroupDef
            {
                Key = "economy", Label = "Property and economy",
                Axes = new[] { CAFactionAxes.Ownership,
                    CAFactionAxes.Economy, CAFactionAxes.Work,
                    CAFactionAxes.Support }
            },
            new CAAxisGroupDef
            {
                Key = "membership", Label = "Membership and order",
                Axes = new[] { CAFactionAxes.Membership,
                    CAFactionAxes.Status, CAFactionAxes.LocalOrder }
            },
            new CAAxisGroupDef
            {
                Key = "security", Label = "Security and conflict",
                Axes = new[] { CAFactionAxes.Defense,
                    CAFactionAxes.WarConduct }
            }
        };

        internal static List<CACreationChoice> CultureProfiles(
            CACulture culture, string seed, Action changed)
        {
            var choices = new List<CACreationChoice>();
            foreach (CAUserCultureProfile profile in
                CAAuthoringProfileLibrary.Cultures.OrderBy(item =>
                    item.displayName))
            {
                CAUserCultureProfile local = profile;
                choices.Add(new CACreationChoice
                {
                    Key = local.key,
                    Name = local.displayName,
                    Summary = CACultureModel.Summary(local.values),
                    CompactSummary = "Saved Culture",
                    Traits = CultureDetails(local.values),
                    Details = "Copies this Culture's name, values, and visual "
                        + "style. The population's existing practices and "
                        + "history stay unchanged.",
                    Group = "Saved Cultures",
                    Badge = "Saved Culture",
                    Accent = CACreationUI.Authored,
                    Selected = false,
                    ConfirmLabel = "Use saved Culture",
                    Choose = delegate
                    {
                        CAAuthoringProfileLibrary.Apply(local, culture);
                        changed?.Invoke();
                    }
                });
            }
            return choices;
        }

        internal static List<CACreationChoice> CulturePresets(
            CACulture culture, string sourceIdentity, Action changed)
        {
            var choices = new List<CACreationChoice>();
            foreach (CACulturePresetDef preset in CACulturePresetLibrary.All
                .OrderBy(item => CultureCatalogOrder(item.CatalogGroup))
                .ThenBy(item => item.ApproximatePeriod,
                    StringComparer.Ordinal)
                .ThenBy(item => item.Label, StringComparer.Ordinal))
            {
                CACulturePresetDef local = preset;
                choices.Add(new CACreationChoice
                {
                    Key = local.Key,
                    Name = local.Label,
                    Summary = local.Summary,
                    CompactSummary = local.Summary,
                    Traits = "Differences within the population: "
                        + CultureDiversityWords(local.GlobalDiversity)
                        + (!local.ReferenceContext.NullOrEmpty()
                            ? " · " + local.ReferenceContext : "")
                        + (!local.ApproximatePeriod.NullOrEmpty()
                            ? " · " + local.ApproximatePeriod : ""),
                    Details = local.Rationale + "\n\nSources: "
                        + local.Sources + "\n\nThis preset sets all "
                        + CACultureQuestionRegistry.FixedQuestionCount
                        + " cultural values. You can change any result.",
                    Group = local.CatalogGroup,
                    Badge = local.ApproximatePeriod.NullOrEmpty()
                        ? "Social form" : local.ApproximatePeriod,
                    Accent = CACreationUI.Preset,
                    Selected = CACulturePresetLibrary.Matches(culture,
                        local),
                    ConfirmLabel = "Use preset",
                    Choose = delegate
                    {
                        CACulturePresetLibrary.Apply(culture, local,
                            sourceIdentity ?? "preset:" + local.Key);
                        changed?.Invoke();
                    }
                });
            }
            return choices;
        }

        internal static List<CACreationChoice> SocietyPresets(
            CACulture culture, CAPoliticalBeliefs politicalOrder,
            CATechnologicalKnowledge technologicalKnowledge,
            string sourceIdentity, Action changed)
        {
            var choices = new List<CACreationChoice>();
            foreach (CASocietyPreset preset in CASocietyPresetLibrary.Available
                .OrderBy(item => SocietyCatalogOrder(item.CatalogGroup))
                .ThenBy(item => item.ApproximatePeriod,
                    StringComparer.Ordinal)
                .ThenBy(item => item.Label, StringComparer.Ordinal))
            {
                CASocietyPreset local = preset;
                bool selected = local.Matches(culture, politicalOrder,
                    technologicalKnowledge);
                choices.Add(SocietyPresetChoice(local, selected,
                    "Use society preset", null, delegate
                    {
                        if (!CASocietyPresetLibrary.TryApply(local, culture,
                                politicalOrder, technologicalKnowledge,
                                sourceIdentity,
                                out string failure))
                        {
                            Messages.Message("Could not apply " + local.Label
                                    + ". " + failure,
                                MessageTypeDefOf.RejectInput, false);
                            return false;
                        }
                        changed?.Invoke();
                        Messages.Message(local.Label
                                + " set Culture, Political Order, and "
                                + "Technological Knowledge.",
                            MessageTypeDefOf.NeutralEvent, false);
                        return true;
                    }));
            }
            return choices;
        }

        // Starting Region uses the same society catalog as founding and
        // faction editing. A placement choice applies the recipe once to a
        // newly created ordinary faction and settlement; it does not create a
        // second preset library or a persistent settlement type.
        internal static List<CACreationChoice> SocietyPlacementPresets(
            Func<CASocietyPreset, bool> choose)
        {
            var choices = new List<CACreationChoice>();
            foreach (CASocietyPreset preset in CASocietyPresetLibrary.Available
                .OrderBy(item => SocietyCatalogOrder(item.CatalogGroup))
                .ThenBy(item => item.ApproximatePeriod,
                    StringComparer.Ordinal)
                .ThenBy(item => item.Label, StringComparer.Ordinal))
            {
                CASocietyPreset local = preset;
                choices.Add(SocietyPresetChoice(local, false,
                    "Place this settlement",
                    "Creates one new local faction and settlement. Click an "
                        + "area on the map to place it, then edit it normally.",
                    delegate { return choose?.Invoke(local) == true; }));
            }
            return choices;
        }

        private static CACreationChoice SocietyPresetChoice(
            CASocietyPreset preset, bool selected, string confirmLabel,
            string actionDetails, Func<bool> choose)
        {
            CAPoliticalBeliefs politicalPreview =
                preset.PoliticalPreview();
            CATechnologicalKnowledge technologyPreview =
                preset.TechnologyPreview();
            string details = "Culture\n" + preset.CultureSummary
                + "\n\nPolitical Order\n"
                + CAPoliticalOrderModel.Description(politicalPreview)
                + "\n\nTechnological Knowledge\n"
                + CATechnologicalKnowledgeModel.Summary(technologyPreview)
                + "\n\nThis sets all three faction components together. "
                + "You can change any component afterward.";
            if (!actionDetails.NullOrEmpty())
                details += "\n\n" + actionDetails;
            return new CACreationChoice
            {
                Key = preset.Key,
                Name = preset.Label,
                Summary = preset.Summary,
                CompactSummary = preset.Summary,
                Traits = (!preset.ReferenceRegion.NullOrEmpty()
                        ? preset.ReferenceRegion + " · " : "")
                    + "Political Order: "
                    + CAPoliticalOrderModel.Identity(politicalPreview),
                Details = details,
                Group = preset.CatalogGroup,
                Badge = preset.Saved ? "Saved"
                    : preset.ApproximatePeriod.NullOrEmpty()
                        ? "Social form" : preset.ApproximatePeriod,
                Accent = preset.Saved ? CACreationUI.Authored
                    : CACreationUI.Preset,
                Selected = selected,
                ConfirmLabel = confirmLabel,
                TryChoose = choose
            };
        }

        private static int CultureCatalogOrder(string catalogGroup)
        {
            switch (catalogGroup)
            {
                case "Social forms": return 0;
                case "Historical cultures": return 1;
                default: return int.MaxValue;
            }
        }

        private static int SocietyCatalogOrder(string catalogGroup)
        {
            switch (catalogGroup)
            {
                case "Saved societies": return 0;
                case "Social forms": return 1;
                case "Historical societies": return 2;
                default: return int.MaxValue;
            }
        }

        private static string CultureDiversityWords(int value)
        {
            string[] labels =
                { "narrow", "limited", "mixed", "broad", "very broad" };
            return labels[Mathf.Clamp(value, 0, labels.Length - 1)];
        }

        internal static string CultureIdentity(CACulture culture)
        {
            if (culture == null) return "Culture not set";
            return culture.name ?? "Unnamed Culture";
        }


        internal static string PoliticalIdentity(
            CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null) return "Political Order not set";
            if (CAPoliticalOrderModel.HasVariables(beliefs))
                return CAPoliticalOrderModel.Identity(beliefs);
            int count = (beliefs.positions ?? new List<CAAxisEntry>())
                .Count(item => item != null
                    && item.source != (byte)CAAxisSource.Unset);
            return count == 0 ? "Political Order not set"
                : count + " political choice" + (count == 1 ? "" : "s")
                    + " set";
        }

        internal static string CultureDetails(CACulture culture)
        {
            if (culture == null) return "Culture not set.";
            CultureDef native = CACultureModel.NativeDef(culture);
            string visualStyle = native?.LabelCap.ToString()
                ?? (culture.sourceCultureDefName.NullOrEmpty()
                    ? "neutral"
                    : culture.sourceCultureDefName
                        + " unavailable; neutral style used");
            List<CACultureQuestionDistribution> questions = CACultureModel
                .PopulationQuestions(culture).ToList();
            string mainValues = string.Join(", ", questions
                .OrderByDescending(item => item.salience)
                .ThenBy(item => item.questionKey)
                .Take(3).Select(item =>
                {
                    CACultureQuestionDef definition =
                        CACultureQuestionRegistry.Find(item.questionKey);
                    CACultureDistributionSummary summary =
                        CACultureDistributionKernel.Summarize(item,
                            culture.id ?? item.questionKey);
                    return (definition?.Label ?? "Cultural value") + ": "
                        + summary.Anchor;
                })
                .ToArray());
            string result = culture.name ?? "Unnamed Culture";
            if (!mainValues.NullOrEmpty())
                result += ". Main values: " + mainValues;
            CACultureConstituent[] roots = culture.constituents
                .Where(item => item != null && item.share > 0).ToArray();
            if (roots.Length > 1)
                result += ". Cultural mix: " + string.Join(", ", roots
                    .Select(item => item.share + "% "
                        + (item.label ?? "unnamed")));
            return result + ". Visual style: " + visualStyle + ".";
        }


        internal static string PoliticalDetails(CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null) return "Political Order not set.";
            if (CAPoliticalOrderModel.HasVariables(beliefs))
                return CAPoliticalOrderModel.Description(beliefs);
            return string.Join("\n", CAFactionAxes.Axes.Select(axis =>
            {
                IReadOnlyList<CAAxisOption> options =
                    CAFactionAxes.OptionsOf(beliefs.positions, axis.Key);
                return axis.Label + ": "
                    + (options.Count == 0 ? "Open" : string.Join(" + ",
                        options.Select(option => option.Label))) + ". "
                    + string.Join("; ", options.Select(option =>
                        option.Words));
            }).ToArray());
        }

    }

    internal sealed class Dialog_CAProfileName : Window
    {
        private readonly string title;
        private readonly Action<string> accepted;
        private string value;

        public override Vector2 InitialSize => new Vector2(520f, 190f);

        internal Dialog_CAProfileName(string title, string initial,
            Action<string> accepted)
        {
            this.title = title;
            this.accepted = accepted;
            value = initial ?? "";
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), title);
            value = Widgets.TextField(new Rect(0f, 40f, inRect.width, 30f),
                value);
            if (Widgets.ButtonText(new Rect(inRect.width - 120f, 88f,
                    120f, 32f), "Save") && !value.NullOrEmpty())
            {
                accepted?.Invoke(value.Trim());
                Close();
            }
        }
    }

    internal sealed class Dialog_CAProfileManager : Window
    {
        private readonly CACulture culture;
        private readonly CAPoliticalBeliefs beliefs;
        private readonly Action changed;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(760f, UI.screenWidth - 48f),
            Mathf.Min(620f, UI.screenHeight - 48f));

        internal Dialog_CAProfileManager(CACulture culture,
            CAPoliticalBeliefs beliefs, Action changed)
        {
            this.culture = culture;
            this.beliefs = beliefs;
            this.changed = changed;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            bool cultures = culture != null;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f),
                cultures ? "Saved Cultures"
                    : "Saved Political Orders");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 38f, inRect.width, 42f),
                "Saved presets are global. Loading copies their listed values "
                + "into this draft; renaming or deleting the library copy does not alter a world.");
            Rect outer = new Rect(0f, 88f, inRect.width,
                inRect.height - 136f);
            int count = cultures ? CAAuthoringProfileLibrary.Cultures.Count
                : CAAuthoringProfileLibrary.PoliticalOrders.Count;
            bool compactRows = outer.width - 18f < 560f;
            Rect view = new Rect(0f, 0f, outer.width - 18f,
                Mathf.Max(outer.height,
                    count * (compactRows ? 86f : 48f) + 8f));
            Widgets.BeginScrollView(outer, ref scroll, view);
            rowY = 0f;
            if (count == 0)
            {
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(new Rect(8f, 8f, view.width - 16f, 60f),
                    cultures
                        ? "No Cultures are saved. Save the current Culture from its editor first."
                        : "No Political Orders are saved. Save a Political Order from its editor first.");
                GUI.color = Color.white;
            }
            else if (cultures)
            {
                foreach (CAUserCultureProfile profile in
                    CAAuthoringProfileLibrary.Cultures.ToList())
                    DrawCultureRow(view.width, profile);
            }
            else
            {
                foreach (CAUserPoliticalOrderProfile profile in
                    CAAuthoringProfileLibrary.PoliticalOrders.ToList())
                    DrawPoliticalRow(view.width, profile);
            }
            Widgets.EndScrollView();
        }

        private float rowY;

        private void DrawCultureRow(float width,
            CAUserCultureProfile profile)
        {
            DrawProfileRow(width, profile.displayName, delegate
                {
                    CAAuthoringProfileLibrary.Apply(profile, culture);
                    changed?.Invoke();
                }, () => Find.WindowStack.Add(new Dialog_CAProfileName(
                    "Rename saved Culture", profile.displayName,
                    value => CAAuthoringProfileLibrary.Rename(profile, value))),
                () => CAAuthoringProfileLibrary.Duplicate(profile),
                () => Find.WindowStack.Add(
                    Dialog_MessageBox.CreateConfirmation(
                    "Delete saved profile " + profile.displayName + "?",
                    () => CAAuthoringProfileLibrary.Delete(profile), true)));
        }

        private void DrawPoliticalRow(float width,
            CAUserPoliticalOrderProfile profile)
        {
            DrawProfileRow(width, profile.displayName, delegate
                {
                    CAAuthoringProfileLibrary.Apply(profile, beliefs);
                    changed?.Invoke();
                }, () => Find.WindowStack.Add(new Dialog_CAProfileName(
                    "Rename saved Political Order", profile.displayName,
                    value => CAAuthoringProfileLibrary.Rename(profile, value))),
                () => CAAuthoringProfileLibrary.Duplicate(profile),
                () => Find.WindowStack.Add(
                    Dialog_MessageBox.CreateConfirmation(
                    "Delete saved profile " + profile.displayName + "?",
                    () => CAAuthoringProfileLibrary.Delete(profile), true)));
        }

        private void DrawProfileRow(float width, string name, Action load,
            Action rename, Action duplicate, Action delete)
        {
            bool compact = width < 560f;
            float height = compact ? 78f : 40f;
            Rect row = new Rect(0f, rowY, width, height);
            Widgets.DrawAltRect(row);
            Rect[] buttons = new Rect[4];
            if (compact)
            {
                Widgets.Label(new Rect(8f, row.y + 5f, width - 16f, 26f),
                    name);
                const float gap = 5f;
                float buttonWidth = (width - gap * 3f) / 4f;
                for (int i = 0; i < buttons.Length; i++)
                    buttons[i] = new Rect(i * (buttonWidth + gap),
                        row.y + 39f, buttonWidth, 30f);
            }
            else
            {
                Widgets.Label(new Rect(8f, row.y + 8f, width - 344f, 28f),
                    name);
                buttons[0] = new Rect(width - 328f, row.y + 4f, 72f, 30f);
                buttons[1] = new Rect(width - 250f, row.y + 4f, 72f, 30f);
                buttons[2] = new Rect(width - 172f, row.y + 4f, 72f, 30f);
                buttons[3] = new Rect(width - 94f, row.y + 4f, 86f, 30f);
            }
            Action[] actions = { load, rename, duplicate, delete };
            string[] labels = { "Load", "Rename", "Copy", "Delete" };
            for (int i = 0; i < buttons.Length; i++)
                if (Widgets.ButtonText(buttons[i], labels[i])) actions[i]?.Invoke();
            rowY += compact ? 86f : 48f;
        }

    }
}
