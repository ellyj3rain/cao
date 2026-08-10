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
        internal string Traits;
        internal string Details;
        internal string Group;
        internal string Badge;
        internal string DisabledReason;
        internal string ConfirmLabel;
        internal Texture2D Icon;
        internal Color Accent = CACreationUI.Accent;
        internal bool Selected;
        internal bool Disabled;
        internal Action Choose;
    }

    internal static class CACreationUI
    {
        internal static readonly Color Accent =
            new Color(0.36f, 0.76f, 0.88f);
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
                case CAAxisSource.Preset: return "Preset";
                case CAAxisSource.Generated: return "Generated";
                default: return "Not set";
            }
        }

        internal static Color SourceColor(CAAxisSource source)
        {
            switch (source)
            {
                case CAAxisSource.Authored: return Authored;
                case CAAxisSource.Preset: return Preset;
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
                if (active)
                    Widgets.DrawBoxSolid(part,
                        new Color(0.18f, 0.42f, 0.50f, 0.72f));
                else if (Mouse.IsOver(part))
                    Widgets.DrawHighlight(part);
                GUI.color = active ? Color.white
                    : new Color(0.78f, 0.81f, 0.85f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(part, labels[i]);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                Widgets.DrawBox(part, 1);
                if (Widgets.ButtonInvisible(part) && !active)
                {
                    choose?.Invoke(i);
                    changed = true;
                }
            }
            return changed;
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
                "Founding society", "Starting pawns"
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
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(1180f, UI.screenWidth - 48f),
            Mathf.Min(790f, UI.screenHeight - 48f));

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f), title);
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

            const float gap = 12f;
            float detailsWidth = Mathf.Clamp(inRect.width * 0.32f,
                300f, 370f);
            Rect grid = new Rect(0f, y,
                inRect.width - detailsWidth - gap,
                inRect.height - y - 46f);
            Rect details = new Rect(grid.xMax + gap, y, detailsWidth,
                grid.height);
            DrawGrid(grid);
            DrawDetails(details);

            if (Widgets.ButtonText(new Rect(0f, inRect.height - 38f,
                    150f, 34f), "Cancel"))
                Close();
        }

        private float DrawGroups(Rect inRect, float y)
        {
            List<string> groups = choices.Select(item => item.Group)
                .Where(item => !item.NullOrEmpty()).Distinct().ToList();
            if (groups.Count <= 1) return y;
            groups.Insert(0, "All");
            float width = Mathf.Min(150f,
                (inRect.width - 4f * (groups.Count - 1)) / groups.Count);
            for (int i = 0; i < groups.Count; i++)
            {
                string local = groups[i];
                bool active = (group.NullOrEmpty() && local == "All")
                    || group == local;
                Rect tab = new Rect(i * (width + 4f), y, width, 26f);
                if (active)
                    Widgets.DrawBoxSolid(tab,
                        new Color(0.18f, 0.42f, 0.50f, 0.72f));
                else if (Mouse.IsOver(tab)) Widgets.DrawHighlight(tab);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(tab, local);
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.DrawBox(tab, 1);
                if (Widgets.ButtonInvisible(tab) && !active)
                {
                    group = local == "All" ? null : local;
                    CACreationChoice first = Filtered().FirstOrDefault();
                    selectedKey = first?.Key;
                    gridScroll = Vector2.zero;
                    detailScroll = Vector2.zero;
                }
            }
            return y + 34f;
        }

        private List<CACreationChoice> Filtered()
        {
            return choices.Where(item => group.NullOrEmpty()
                || item.Group == group).ToList();
        }

        private void DrawGrid(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect outRect = rect.ContractedBy(6f);
            List<CACreationChoice> visible = Filtered();
            int columns = outRect.width >= 570f ? 2 : 1;
            const float gap = 8f;
            const float height = 132f;
            float cardWidth = (outRect.width - 18f
                - gap * (columns - 1)) / columns;
            int rows = (visible.Count + columns - 1) / columns;
            Rect view = new Rect(0f, 0f, outRect.width - 18f,
                Mathf.Max(outRect.height, rows * (height + gap)));
            Widgets.BeginScrollView(outRect, ref gridScroll, view);
            try
            {
                for (int i = 0; i < visible.Count; i++)
                {
                    int row = i / columns;
                    int column = i % columns;
                    DrawChoice(new Rect(column * (cardWidth + gap),
                        row * (height + gap), cardWidth, height), visible[i]);
                }
            }
            finally { Widgets.EndScrollView(); }
        }

        private void DrawChoice(Rect rect, CACreationChoice choice)
        {
            bool focused = choice.Key == selectedKey;
            bool applied = choice.Selected;
            bool hovered = Mouse.IsOver(rect);
            Widgets.DrawBoxSolid(rect, focused
                ? new Color(0.18f, 0.25f, 0.29f, 0.96f)
                : new Color(0.12f, 0.14f, 0.16f,
                    hovered ? 0.96f : 0.78f));
            if (hovered && !focused) Widgets.DrawHighlight(rect);
            Color accent = choice.Accent.a <= 0f
                ? CACreationUI.Accent : choice.Accent;
            if (applied)
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 4f,
                    rect.height), accent);
            GUI.color = focused ? accent
                : new Color(0.34f, 0.37f, 0.41f);
            Widgets.DrawBox(rect, focused ? 2 : 1);
            GUI.color = Color.white;

            if (applied)
            {
                Rect marker = new Rect(rect.xMax - 17f, rect.y + 8f, 9f, 9f);
                Widgets.DrawBoxSolid(marker, accent);
                TooltipHandler.TipRegion(new Rect(rect.xMax - 28f, rect.y,
                    28f, 28f), "This is the currently applied choice.");
            }

            float textX = rect.x + 10f;
            if (choice.Icon != null)
            {
                Rect icon = new Rect(rect.x + 10f, rect.y + 12f, 52f, 52f);
                GUI.color = choice.Disabled
                    ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
                GUI.DrawTexture(icon, choice.Icon, ScaleMode.ScaleToFit,
                    true);
                GUI.color = Color.white;
                textX = icon.xMax + 10f;
            }
            float textWidth = rect.xMax - textX - 10f;
            Text.Font = GameFont.Small;
            GUI.color = choice.Disabled ? ColoredText.SubtleGrayColor
                : Color.white;
            Widgets.Label(new Rect(textX, rect.y + 8f, textWidth, 40f),
                choice.Name ?? "Unnamed choice");
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.72f, 0.76f, 0.81f,
                choice.Disabled ? 0.55f : 1f);
            Widgets.Label(new Rect(textX, rect.y + 48f, textWidth, 46f),
                choice.Summary ?? "No description recorded.");
            GUI.color = Color.white;
            float badgeWidth = 0f;
            if (!choice.Badge.NullOrEmpty())
                badgeWidth = CACreationUI.DrawChip(new Rect(rect.x + 10f,
                    rect.yMax - 28f, rect.width - 20f, 20f),
                    choice.Badge, accent);
            if (!choice.Traits.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.67f, 0.71f, 0.76f);
                float traitX = rect.x + 10f
                    + (badgeWidth > 0f ? badgeWidth + 8f : 0f);
                float traitWidth = rect.xMax - traitX - 8f;
                if (traitWidth >= 60f)
                    Widgets.Label(new Rect(traitX, rect.yMax - 25f,
                        traitWidth, 20f), choice.Traits);
                GUI.color = Color.white;
            }
            Text.Font = GameFont.Small;
            if (Widgets.ButtonInvisible(rect))
            {
                selectedKey = choice.Key;
                detailScroll = Vector2.zero;
            }
        }

        private void DrawDetails(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
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

            Rect contentOut = new Rect(inner.x, inner.y, inner.width,
                inner.height - 48f);
            float textWidth = contentOut.width - 18f;
            Text.Font = GameFont.Small;
            float summaryHeight = Text.CalcHeight(choice.Summary ?? "",
                textWidth);
            float traitsHeight = choice.Traits.NullOrEmpty() ? 0f
                : Text.CalcHeight(choice.Traits, textWidth);
            float detailsHeight = choice.Details.NullOrEmpty() ? 0f
                : Text.CalcHeight(choice.Details, textWidth);
            float disabledHeight = choice.DisabledReason.NullOrEmpty() ? 0f
                : Text.CalcHeight(choice.DisabledReason, textWidth);
            Text.Font = GameFont.Medium;
            float titleHeight = Mathf.Max(32f, Text.CalcHeight(
                choice.Name ?? "Unnamed choice", textWidth));
            Text.Font = GameFont.Small;
            float viewHeight = (choice.Icon == null ? 0f : 72f)
                + titleHeight + 4f
                + (choice.Badge.NullOrEmpty() ? 0f : 28f)
                + (choice.Selected ? 28f : 0f)
                + (choice.DisabledReason.NullOrEmpty()
                    ? 0f : disabledHeight + 12f)
                + summaryHeight + 12f
                + (choice.Traits.NullOrEmpty() ? 0f : traitsHeight + 12f)
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
                if (!choice.Traits.NullOrEmpty())
                {
                    GUI.color = new Color(0.76f, 0.80f, 0.84f);
                    Widgets.Label(new Rect(0f, y, textWidth, traitsHeight),
                        choice.Traits);
                    GUI.color = Color.white;
                    y += traitsHeight + 12f;
                }
                if (!choice.Details.NullOrEmpty())
                {
                    GUI.color = new Color(0.68f, 0.72f, 0.77f);
                    Widgets.Label(new Rect(0f, y, textWidth, detailsHeight),
                        choice.Details);
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
            if (Widgets.ButtonText(use, confirm, true, true,
                    !choice.Disabled) && !choice.Disabled)
            {
                choice.Choose?.Invoke();
                Close();
            }
        }
    }
}
