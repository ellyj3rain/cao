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
                    Details = "Applying this Culture copies its inherited "
                        + "question distributions, name, and optional visual "
                        + "tradition. Practice history, migration evidence, "
                        + "locality, and transitions remain with the target.",
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

        internal static string CultureIdentity(CACulture culture)
        {
            if (culture == null) return "Culture not set";
            return culture.name ?? "Inherited Culture";
        }


        internal static List<CACreationChoice> PoliticalBeliefSets(
            CAPoliticalBeliefs beliefs, string seed, Action changed)
        {
            var choices = new List<CACreationChoice>();
            foreach (CAPoliticalPatchTemplate template in
                CAPoliticalPatchTemplates.Beliefs)
            {
                CAPoliticalPatchTemplate local = template;
                choices.Add(new CACreationChoice
                {
                    Key = local.Key,
                    Name = local.Label,
                    Summary = local.Summary,
                    CompactSummary = TemplateTraits(local, 2),
                    Traits = TemplateTraits(local, 5),
                    Details = TemplateDetails(local)
                        + "\n\nThis is a partial patch. Applying it adds the "
                        + "listed commitments and preserves every unlisted value.",
                    Group = local.Domain,
                    Badge = "Built-in belief set",
                    Accent = CACreationUI.Authored,
                    Selected = CAPoliticalBeliefsModel.UsesTemplate(
                        beliefs, local),
                    ConfirmLabel = "Add these commitments",
                    Choose = delegate
                    {
                        CAPoliticalBeliefsModel.ApplyTemplate(beliefs, local);
                        changed?.Invoke();
                    }
                });
            }
            foreach (CAUserPoliticalBeliefSet profile in
                CAAuthoringProfileLibrary.PoliticalBeliefSets.OrderBy(item =>
                    item.displayName))
            {
                CAUserPoliticalBeliefSet local = profile;
                choices.Add(new CACreationChoice
                {
                    Key = local.key,
                    Name = local.displayName,
                    Summary = CAPoliticalBeliefsModel.Summary(local.values),
                    CompactSummary = "Saved political belief set",
                    Traits = CAPoliticalBeliefsModel.Summary(local.values),
                    Details = PoliticalDetails(local.values)
                        + "\n\nApplying this set adds its listed commitments "
                        + "to the current world draft and preserves other "
                        + "values. Later edits do not change "
                        + "the saved library copy.",
                    Group = "Saved",
                    Badge = "Saved belief set",
                    Accent = CACreationUI.Authored,
                    Selected = false,
                    ConfirmLabel = "Add saved commitments",
                    Choose = delegate
                    {
                        CAAuthoringProfileLibrary.Apply(local, beliefs);
                        changed?.Invoke();
                    }
                });
            }
            return choices;
        }

        internal static string PoliticalIdentity(
            CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null) return "Political beliefs not set";
            int count = (beliefs.positions ?? new List<CAAxisEntry>())
                .Count(item => item != null
                    && item.source != (byte)CAAxisSource.Unset);
            return count == 0 ? "Political beliefs remain open"
                : count + " political mechanism"
                    + (count == 1 ? "" : "s") + " recorded";
        }

        internal static string CultureDetails(CACulture culture)
        {
            if (culture == null) return "No Culture recorded.";
            CultureDef native = CACultureModel.NativeDef(culture);
            string source = native?.LabelCap.ToString()
                ?? (culture.sourceCultureDefName.NullOrEmpty()
                    ? "neutral fallback"
                    : culture.sourceCultureDefName
                        + " unavailable; neutral fallback");
            List<CACultureQuestionDistribution> questions = CACultureModel
                .PopulationQuestions(culture).ToList();
            int meanings = questions.Count;
            string salient = string.Join(", ", questions
                .OrderByDescending(item => item.salience)
                .ThenBy(item => item.questionKey)
                .Take(3).Select(item => CACultureQuestionRegistry
                    .Find(item.questionKey)?.Label ?? item.questionKey)
                .ToArray());
            return "Culture: " + (culture.name ?? "inherited Culture")
                + ". Cultural questions: " + meanings + (salient.NullOrEmpty()
                    ? "." : " (" + salient + ").")
                + " Constituent sources: " + culture.constituents.Count
                + ". Visual tradition: " + source + ". Continuity: "
                + CACultureHistory.ContinuitySummary(culture) + ".";
        }


        internal static string PoliticalDetails(CAPoliticalBeliefs beliefs)
        {
            if (beliefs == null) return "No political positions recorded.";
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

        private static string TemplateTraits(
            CAPoliticalPatchTemplate template, int count)
        {
            if (template == null) return "No commitments";
            return string.Join(" · ", template.Mechanisms
                .SelectMany(pair => pair.Value.Select(value =>
                    CAFactionAxes.AxisDef(pair.Key)?.Options.FirstOrDefault(
                        option => option.Key == value)?.Label ?? value))
                .Take(Math.Max(1, count)).ToArray());
        }

        private static string TemplateDetails(
            CAPoliticalPatchTemplate template)
        {
            if (template == null) return "No commitments recorded.";
            return string.Join("\n", template.Mechanisms.Select(pair =>
            {
                CAAxisDef axis = CAFactionAxes.AxisDef(pair.Key);
                string mechanisms = string.Join(" + ", pair.Value.Select(
                    value => axis?.Options.FirstOrDefault(option =>
                        option.Key == value)?.Label ?? value));
                return (axis?.Label ?? pair.Key) + ": " + mechanisms + ".";
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
                    : "Saved political belief sets");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 38f, inRect.width, 42f),
                "Saved presets are global. Loading copies their listed values "
                + "into this draft; renaming or deleting the library copy does not alter a world.");
            Rect outer = new Rect(0f, 88f, inRect.width,
                inRect.height - 136f);
            int count = cultures ? CAAuthoringProfileLibrary.Cultures.Count
                : CAAuthoringProfileLibrary.PoliticalBeliefSets.Count;
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
                        : "No political belief sets are saved. Save the current beliefs from their editor first.");
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
                foreach (CAUserPoliticalBeliefSet profile in
                    CAAuthoringProfileLibrary.PoliticalBeliefSets.ToList())
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
            CAUserPoliticalBeliefSet profile)
        {
            DrawProfileRow(width, profile.displayName, delegate
                {
                    CAAuthoringProfileLibrary.Apply(profile, beliefs);
                    changed?.Invoke();
                }, () => Find.WindowStack.Add(new Dialog_CAProfileName(
                    "Rename political belief set", profile.displayName,
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
