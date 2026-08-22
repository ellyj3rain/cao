using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // Shared interaction grammar for the creation flow. Large or descriptive
    // choice sets belong in this stable, inspectable surface rather than in a
    // FloatMenu that covers the page the player is trying to author.
    internal sealed class CACreationChoice
    {
        internal string Key;
        internal string Name;
        internal string Summary;
        internal string CompactSummary;
        internal string Traits;
        internal string Details;
        internal string ExpandedDetails { get; set; }
        internal string Group;
        internal string Badge;
        internal string DisabledReason;
        internal string ConfirmLabel;
        internal Texture2D Icon;
        internal Color Accent = CACreationUI.Accent;
        internal bool Selected;
        internal bool Disabled;
        internal Action Choose;
        // Choices that can fail validation keep the dialog open and report the
        // failure at their owning surface. Ordinary choices continue to use
        // Choose.
        internal Func<bool> TryChoose;
    }

    internal static class CACreationUI
    {
        // The kit's focus color is the one CAO action color; provenance
        // colors below stay semantic (authored, generated, preset,
        // inherited) and mean the same thing on every authoring surface.
        internal static readonly Color Accent = CAOpeningTheme.AccentHover;
        internal static readonly Color Preset =
            new Color(0.72f, 0.62f, 0.36f);
        internal static readonly Color Authored =
            new Color(0.38f, 0.78f, 0.56f);
        internal static readonly Color Generated =
            new Color(0.54f, 0.66f, 0.80f);
        internal static readonly Color Inherited =
            new Color(0.66f, 0.62f, 0.76f);
        internal static readonly Color Unset =
            new Color(0.52f, 0.54f, 0.57f);

        internal static Texture2D Icon(string path)
        {
            return path.NullOrEmpty() ? null
                : ContentFinder<Texture2D>.Get(path, false);
        }

        internal static string SourceWords(CAAxisSource source)
        {
            switch (source)
            {
                case CAAxisSource.Authored: return "Chosen";
                case CAAxisSource.Generated: return "Generated";
                default: return "Unset";
            }
        }

        internal static Color SourceColor(CAAxisSource source)
        {
            switch (source)
            {
                case CAAxisSource.Authored: return Authored;
                case CAAxisSource.Generated: return Generated;
                default: return Unset;
            }
        }

        internal static float DrawChip(Rect rect, string words, Color color)
        {
            if (words.NullOrEmpty()) return 0f;
            GameFont prior = Text.Font;
            Text.Font = GameFont.Tiny;
            float width = Mathf.Min(rect.width,
                Text.CalcSize(words).x + 16f);
            Rect chip = new Rect(rect.x, rect.y, width,
                Mathf.Min(20f, rect.height));
            Widgets.DrawBoxSolid(chip,
                new Color(color.r, color.g, color.b, 0.19f));
            GUI.color = color;
            Widgets.DrawBox(chip, 1);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(chip, words);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = prior;
            return width;
        }

        internal static bool DrawSegment(Rect rect, string[] labels,
            int selected, Action<int> choose)
        {
            if (labels == null || labels.Length == 0) return false;
            bool changed = false;
            float width = rect.width / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                Rect part = new Rect(rect.x + i * width, rect.y,
                    width + (i == labels.Length - 1 ? 0f : 1f),
                    rect.height);
                bool active = i == selected;
                if (CAOpeningTheme.Chip(part, labels[i], active)
                    && !active)
                {
                    choose?.Invoke(i);
                    changed = true;
                }
            }
            return changed;
        }

        internal static float DrawSegmentRows(Rect rect, string[] labels,
            int selected, Action<int> choose, float minimumCellWidth = 145f)
        {
            if (labels == null || labels.Length == 0) return 0f;
            const float gap = 4f;
            int columns = Mathf.Clamp(Mathf.FloorToInt(
                (rect.width + gap) / (minimumCellWidth + gap)), 1,
                labels.Length);
            int rows = (labels.Length + columns - 1) / columns;
            float cellWidth = (rect.width - gap * (columns - 1)) / columns;
            for (int i = 0; i < labels.Length; i++)
            {
                int row = i / columns;
                int column = i % columns;
                Rect part = new Rect(rect.x + column * (cellWidth + gap),
                    rect.y + row * (rect.height + gap), cellWidth,
                    rect.height);
                bool active = i == selected;
                if (CAOpeningTheme.Chip(part, labels[i], active)
                    && !active)
                    choose?.Invoke(i);
            }
            return rows * rect.height + (rows - 1) * gap;
        }

        internal static void OpenChoices(string title, string introduction,
            IEnumerable<CACreationChoice> choices)
        {
            Find.WindowStack.Add(new Dialog_CACreationChoices(title,
                introduction, choices));
        }

        internal static void DrawFlow(Rect rect, int active)
        {
            string[] steps =
            {
                "World", "Landing", "Starting region",
                "Starting arrangements", "Starting pawns"
            };
            float width = rect.width / steps.Length;
            GameFont prior = Text.Font;
            Text.Font = GameFont.Tiny;
            float lineY = rect.y + 5f;
            float firstCenter = rect.x + width * 0.5f;
            float lastCenter = rect.x + width * (steps.Length - 0.5f);
            Widgets.DrawBoxSolid(new Rect(firstCenter, lineY - 1f,
                lastCenter - firstCenter, 2f),
                new Color(0.34f, 0.38f, 0.42f, 0.82f));
            for (int i = 0; i < steps.Length; i++)
            {
                float center = rect.x + width * (i + 0.5f);
                float nodeSize = i == active ? 10f : 7f;
                Color node = i == active ? Accent
                    : i < active ? new Color(0.73f, 0.77f, 0.81f)
                        : new Color(0.35f, 0.38f, 0.42f);
                Widgets.DrawBoxSolid(new Rect(center - nodeSize * 0.5f,
                    lineY - nodeSize * 0.5f, nodeSize, nodeSize), node);
                GUI.color = i > active
                    ? new Color(0.57f, 0.61f, 0.66f) : Color.white;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(rect.x + i * width, rect.y + 7f,
                    width, rect.height - 7f), steps[i]);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            Text.Font = prior;
        }
    }

    internal sealed class Dialog_CACreationChoices : Window
    {
        private readonly string title;
        private readonly string introduction;
        private readonly List<CACreationChoice> choices;
        private string selectedKey;
        private string group;
        private string search = "";
        private bool showDetails;
        private Vector2 gridScroll;
        private Vector2 detailScroll;

        internal Dialog_CACreationChoices(string title,
            string introduction, IEnumerable<CACreationChoice> choices)
        {
            this.title = title ?? "Choose";
            this.introduction = introduction;
            this.choices = choices?.Where(item => item != null).ToList()
                ?? new List<CACreationChoice>();
            CACreationChoice initial = this.choices.FirstOrDefault(item =>
                item.Selected) ?? this.choices.FirstOrDefault(item =>
                !item.Disabled) ?? this.choices.FirstOrDefault();
            selectedKey = initial?.Key;
            doCloseX = false;
            doWindowBackground = false;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(1220f, UI.screenWidth - 32f),
            Mathf.Min(820f, UI.screenHeight - 32f));

        protected override float Margin => 0f;

        public override void DoWindowContents(Rect inRect)
        {
            // One CAO surface for every large choice set in the flow.
            inRect = CAOpeningTheme.BeginWindowSurface(inRect);
            try
            {
            CAOpeningTheme.Heading(0f, 0f, inRect.width, title);
            Text.Font = GameFont.Small;
            float introHeight = introduction.NullOrEmpty() ? 0f
                : Mathf.Max(24f, Text.CalcHeight(introduction,
                    inRect.width - 8f));
            if (introHeight > 0f)
            {
                GUI.color = new Color(0.76f, 0.79f, 0.83f);
                Widgets.Label(new Rect(0f, 38f, inRect.width - 8f,
                    introHeight), introduction);
                GUI.color = Color.white;
            }
            float y = 38f + introHeight + (introHeight > 0f ? 8f : 0f);
            y = DrawGroups(inRect, y);
            y = DrawSearch(inRect, y);

            const float gap = 12f;
            float bodyHeight = inRect.height - y - 46f;
            bool split = inRect.width >= 860f;
            if (split)
            {
                float detailsWidth = Mathf.Clamp(inRect.width * 0.35f,
                    330f, 440f);
                Rect grid = new Rect(0f, y,
                    inRect.width - detailsWidth - gap, bodyHeight);
                Rect details = new Rect(grid.xMax + gap, y, detailsWidth,
                    bodyHeight);
                DrawGrid(grid);
                DrawDetails(details);
            }
            else
            {
                Rect switcher = new Rect(0f, y, inRect.width, 30f);
                CACreationUI.DrawSegment(switcher,
                    new[] { "Choices", "Details" }, showDetails ? 1 : 0,
                    value => showDetails = value == 1);
                Rect body = new Rect(0f, y + 38f, inRect.width,
                    bodyHeight - 38f);
                if (showDetails) DrawDetails(body);
                else DrawGrid(body);
            }

            if (CAOpeningTheme.GhostButton(new Rect(0f,
                    inRect.height - 36f, 130f, 34f), "Cancel"))
                Close();
            }
            finally { CAOpeningTheme.EndWindowSurface(); }
        }

        private float DrawSearch(Rect inRect, float y)
        {
            if (choices.Count < 8) return y;
            Widgets.Label(new Rect(0f, y + 5f, 58f, 28f), "Search");
            search = Widgets.TextField(new Rect(62f, y, inRect.width - 62f,
                30f), search ?? "");
            return y + 38f;
        }

        private float DrawGroups(Rect inRect, float y)
        {
            List<string> groups = CAAuthoringCategoryPolicy.NavigableGroups(
                choices.Where(item => !item.Group.NullOrEmpty())
                    .GroupBy(item => item.Group, StringComparer.Ordinal)
                    .Select(items => new KeyValuePair<string, int>(
                        items.Key, items.Count()))).ToList();
            if (groups.Count == 0)
            {
                group = null;
                return y;
            }
            if (!group.NullOrEmpty() && !groups.Contains(group))
                group = null;
            groups.Insert(0, "All");
            int selected = group.NullOrEmpty() ? 0
                : Mathf.Max(0, groups.IndexOf(group));
            // Density: each group states how many choices it holds, so
            // navigation carries information instead of bare names.
            var counts = choices.Where(item => !item.Group.NullOrEmpty())
                .GroupBy(item => item.Group, StringComparer.Ordinal)
                .ToDictionary(items => items.Key, items => items.Count(),
                    StringComparer.Ordinal);
            string[] labels = groups.Select(name => name == "All"
                ? "All (" + choices.Count + ")"
                : counts.TryGetValue(name, out int held)
                    ? name + " (" + held + ")" : name).ToArray();
            float height = CACreationUI.DrawSegmentRows(new Rect(0f, y,
                inRect.width, 26f), labels, selected, index =>
            {
                string local = groups[index];
                group = local == "All" ? null : local;
                CACreationChoice first = Filtered().FirstOrDefault();
                selectedKey = first?.Key;
                gridScroll = Vector2.zero;
                detailScroll = Vector2.zero;
            }, 150f);
            return y + height + 8f;
        }

        private List<CACreationChoice> Filtered()
        {
            string query = search?.Trim();
            return choices.Where(item => (group.NullOrEmpty()
                    || item.Group == group)
                && (query.NullOrEmpty() || SearchWords(item).IndexOf(query,
                    StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        }

        private static string SearchWords(CACreationChoice item)
        {
            return string.Join(" ", new[] { item.Name, item.Summary,
                item.Traits, item.Details, item.Group }
                .Where(value => !value.NullOrEmpty()).ToArray());
        }

        private void DrawGrid(Rect rect)
        {
            CAOpeningTheme.SurfacePanel(rect, true);
            Rect outRect = rect.ContractedBy(6f);
            List<CACreationChoice> visible = Filtered();
            int columns = outRect.width >= 590f ? 2 : 1;
            const float gap = 8f;
            float cardWidth = (outRect.width - 18f
                - gap * (columns - 1)) / columns;
            int rows = (visible.Count + columns - 1) / columns;
            var rowHeights = new float[rows];
            for (int i = 0; i < visible.Count; i++)
            {
                int row = i / columns;
                rowHeights[row] = Mathf.Max(rowHeights[row],
                    ChoiceHeight(visible[i], cardWidth));
            }
            float contentHeight = rowHeights.Sum() + gap * rows;
            Rect view = new Rect(0f, 0f, outRect.width - 18f,
                Mathf.Max(outRect.height, contentHeight));
            Widgets.BeginScrollView(outRect, ref gridScroll, view);
            try
            {
                float rowY = 0f;
                int currentRow = -1;
                for (int i = 0; i < visible.Count; i++)
                {
                    int row = i / columns;
                    int column = i % columns;
                    if (row != currentRow)
                    {
                        if (currentRow >= 0)
                            rowY += rowHeights[currentRow] + gap;
                        currentRow = row;
                    }
                    DrawChoice(new Rect(column * (cardWidth + gap),
                        rowY, cardWidth, rowHeights[row]), visible[i]);
                }
            }
            finally { Widgets.EndScrollView(); }
        }

        private static float ChoiceHeight(CACreationChoice choice,
            float width)
        {
            float iconWidth = choice.Icon == null ? 0f : 62f;
            float iconHeight = choice.Icon == null ? 0f : 52f;
            float textWidth = Mathf.Max(80f, width - 20f - iconWidth);
            Text.Font = GameFont.Small;
            float title = Text.CalcHeight(choice.Name ?? "Unnamed choice",
                textWidth);
            Text.Font = GameFont.Tiny;
            string summary = choice.CompactSummary.NullOrEmpty()
                ? choice.Summary : choice.CompactSummary;
            float body = Text.CalcHeight(summary ?? "", textWidth);
            bool traits = !choice.Traits.NullOrEmpty();
            float traitHeight = traits
                ? Text.CalcHeight(choice.Traits, width - 20f) : 0f;
            Text.Font = GameFont.Small;
            float header = Mathf.Max(iconHeight, title + body + 4f);
            return Mathf.Max(112f, 10f + header
                + (traits ? traitHeight + 8f : 0f) + 36f);
        }

        private void DrawChoice(Rect rect, CACreationChoice choice)
        {
            bool focused = choice.Key == selectedKey;
            bool applied = choice.Selected;
            bool hovered = Mouse.IsOver(rect);
            Widgets.DrawBoxSolid(rect, focused
                ? CAOpeningTheme.RaisedHover
                : hovered ? CAOpeningTheme.Raised
                    : CAOpeningTheme.Surface);
            Color accent = choice.Accent.a <= 0f
                ? CACreationUI.Accent : choice.Accent;
            if (applied)
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 4f,
                    rect.height), accent);
            CAOpeningTheme.Border(rect, focused
                ? CAOpeningTheme.TextLo : CAOpeningTheme.Hairline);

            if (applied)
            {
                Rect marker = new Rect(rect.xMax - 17f, rect.y + 8f, 9f, 9f);
                Widgets.DrawBoxSolid(marker, accent);
                TooltipHandler.TipRegion(new Rect(rect.xMax - 28f, rect.y,
                    28f, 28f), "This is the currently applied choice.");
            }

            float textX = rect.x + 10f;
            float iconHeight = 0f;
            if (choice.Icon != null)
            {
                Rect icon = new Rect(rect.x + 10f, rect.y + 12f, 52f, 52f);
                GUI.color = choice.Disabled
                    ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
                GUI.DrawTexture(icon, choice.Icon, ScaleMode.ScaleToFit,
                    true);
                GUI.color = Color.white;
                textX = icon.xMax + 10f;
                iconHeight = icon.height;
            }
            float textWidth = rect.xMax - textX - 10f;
            Text.Font = GameFont.Small;
            GUI.color = choice.Disabled ? ColoredText.SubtleGrayColor
                : Color.white;
            float titleHeight = Text.CalcHeight(
                choice.Name ?? "Unnamed choice", textWidth);
            Widgets.Label(new Rect(textX, rect.y + 8f, textWidth,
                titleHeight), choice.Name ?? "Unnamed choice");
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.72f, 0.76f, 0.81f,
                choice.Disabled ? 0.55f : 1f);
            string cardSummary = choice.CompactSummary.NullOrEmpty()
                ? choice.Summary : choice.CompactSummary;
            float summaryY = rect.y + 12f + titleHeight;
            float summaryHeight = Text.CalcHeight(cardSummary
                ?? "No description recorded.", textWidth);
            Widgets.Label(new Rect(textX, summaryY, textWidth,
                summaryHeight),
                cardSummary ?? "No description recorded.");
            GUI.color = Color.white;
            float headerHeight = Mathf.Max(iconHeight,
                titleHeight + summaryHeight + 4f);
            float contentY = rect.y + 10f + headerHeight + 8f;
            bool showTraits = !choice.Traits.NullOrEmpty();
            if (showTraits)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.67f, 0.71f, 0.76f);
                float traitHeight = Text.CalcHeight(choice.Traits,
                    rect.width - 20f);
                Widgets.Label(new Rect(rect.x + 10f, contentY,
                    rect.width - 20f, traitHeight), choice.Traits);
                GUI.color = Color.white;
            }
            if (!choice.Badge.NullOrEmpty())
                CACreationUI.DrawChip(new Rect(rect.x + 10f,
                    rect.yMax - 28f, rect.width - 20f, 20f),
                    choice.Badge, accent);
            Text.Font = GameFont.Small;
            if (Widgets.ButtonInvisible(rect))
            {
                selectedKey = choice.Key;
                detailScroll = Vector2.zero;
                if (rect.width < 520f) showDetails = true;
            }
        }

        private void DrawDetails(Rect rect)
        {
            CAOpeningTheme.SurfacePanel(rect, true);
            Rect inner = rect.ContractedBy(12f);
            CACreationChoice choice = choices.FirstOrDefault(item =>
                item.Key == selectedKey);
            if (choice == null)
            {
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(inner, "Choose an option to inspect it.");
                GUI.color = Color.white;
                return;
            }

            bool showTraits = true;
            bool showExplanation = true;
            string explanation = choice.ExpandedDetails.NullOrEmpty()
                ? choice.Details : choice.ExpandedDetails;
            float footerHeight = 42f;
            Rect contentOut = new Rect(inner.x, inner.y, inner.width,
                inner.height - footerHeight);
            float textWidth = contentOut.width - 18f;
            Text.Font = GameFont.Small;
            float summaryHeight = Text.CalcHeight(choice.Summary ?? "",
                textWidth);
            float traitsHeight = !showTraits || choice.Traits.NullOrEmpty() ? 0f
                : Text.CalcHeight(choice.Traits, textWidth);
            float detailsHeight = !showExplanation || explanation.NullOrEmpty()
                ? 0f : Text.CalcHeight(explanation, textWidth);
            float disabledHeight = choice.DisabledReason.NullOrEmpty() ? 0f
                : Text.CalcHeight(choice.DisabledReason, textWidth);
            Text.Font = GameFont.Medium;
            float titleHeight = Mathf.Max(32f, Text.CalcHeight(
                choice.Name ?? "Unnamed choice", textWidth));
            float replacingMeasure = 0f;
            if (!choice.Selected)
            {
                CACreationChoice appliedMeasure = choices.FirstOrDefault(
                    item => item.Selected);
                if (appliedMeasure != null)
                {
                    Text.Font = GameFont.Tiny;
                    replacingMeasure = Text.CalcHeight("Replaces: "
                        + (appliedMeasure.Name ?? "current choice")
                        + " - " + (appliedMeasure.CompactSummary
                            ?? appliedMeasure.Summary ?? ""), textWidth)
                        + 10f;
                }
            }
            Text.Font = GameFont.Small;
            float viewHeight = (choice.Icon == null ? 0f : 72f)
                + titleHeight + 4f
                + (choice.Badge.NullOrEmpty() ? 0f : 28f)
                + (choice.Selected ? 28f : replacingMeasure)
                + (choice.DisabledReason.NullOrEmpty()
                    ? 0f : disabledHeight + 12f)
                + summaryHeight + 12f
                + (!showTraits || choice.Traits.NullOrEmpty()
                    ? 0f : traitsHeight + 12f)
                + detailsHeight + 8f;
            Rect view = new Rect(0f, 0f, textWidth,
                Mathf.Max(contentOut.height, viewHeight));
            Widgets.BeginScrollView(contentOut, ref detailScroll, view);
            try
            {
                float y = 0f;
                if (choice.Icon != null)
                {
                    GUI.DrawTexture(new Rect(0f, y, 64f, 64f), choice.Icon,
                        ScaleMode.ScaleToFit, true);
                    y += 72f;
                }
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, y, textWidth, titleHeight),
                    choice.Name ?? "Unnamed choice");
                y += titleHeight + 4f;
                if (!choice.Badge.NullOrEmpty())
                {
                    Color accent = choice.Accent.a <= 0f
                        ? CACreationUI.Accent : choice.Accent;
                    CACreationUI.DrawChip(new Rect(0f, y, textWidth, 20f),
                        choice.Badge, accent);
                    y += 28f;
                }
                if (choice.Selected)
                {
                    Color accent = choice.Accent.a <= 0f
                        ? CACreationUI.Accent : choice.Accent;
                    CACreationUI.DrawChip(new Rect(0f, y, textWidth, 20f),
                        "Current selection", accent);
                    y += 28f;
                }
                else
                {
                    // COMPARISON, not blind choice: while inspecting an
                    // alternative, the currently applied choice and its
                    // one-line summary stay in view.
                    CACreationChoice applied = choices.FirstOrDefault(
                        item => item.Selected);
                    if (applied != null)
                    {
                        string replacing = "Replaces: " + (applied.Name
                            ?? "current choice")
                            + ((applied.CompactSummary
                                ?? applied.Summary).NullOrEmpty() ? ""
                                : " - " + (applied.CompactSummary
                                    ?? applied.Summary));
                        Text.Font = GameFont.Tiny;
                        GUI.color = CAOpeningTheme.TextLo;
                        float replacingHeight = Text.CalcHeight(replacing,
                            textWidth);
                        Widgets.Label(new Rect(0f, y, textWidth,
                            replacingHeight), replacing);
                        GUI.color = Color.white;
                        Text.Font = GameFont.Small;
                        y += replacingHeight + 10f;
                    }
                }
                if (!choice.DisabledReason.NullOrEmpty())
                {
                    Text.Font = GameFont.Small;
                    GUI.color = ColorLibrary.RedReadable;
                    Widgets.Label(new Rect(0f, y, textWidth,
                        disabledHeight), choice.DisabledReason);
                    GUI.color = Color.white;
                    y += disabledHeight + 12f;
                }
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(0f, y, textWidth, summaryHeight),
                    choice.Summary ?? "");
                y += summaryHeight + 12f;
                if (showTraits && !choice.Traits.NullOrEmpty())
                {
                    GUI.color = new Color(0.76f, 0.80f, 0.84f);
                    Widgets.Label(new Rect(0f, y, textWidth, traitsHeight),
                        choice.Traits);
                    GUI.color = Color.white;
                    y += traitsHeight + 12f;
                }
                if (showExplanation && !explanation.NullOrEmpty())
                {
                    GUI.color = new Color(0.68f, 0.72f, 0.77f);
                    Widgets.Label(new Rect(0f, y, textWidth, detailsHeight),
                        explanation);
                    GUI.color = Color.white;
                }
            }
            finally
            {
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                Widgets.EndScrollView();
            }

            string confirm = choice.ConfirmLabel.NullOrEmpty()
                ? "Use this choice" : choice.ConfirmLabel;
            Rect use = new Rect(inner.x, inner.yMax - 36f,
                inner.width, 34f);
            if (CAOpeningTheme.PrimaryButton(use, confirm,
                    !choice.Disabled) && !choice.Disabled)
            {
                bool accepted;
                if (choice.TryChoose != null)
                    accepted = choice.TryChoose();
                else
                {
                    accepted = choice.Choose != null;
                    choice.Choose?.Invoke();
                }
                if (accepted) Close();
            }
        }
    }
}
