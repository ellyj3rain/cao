using System;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // THE OPENING FLOW IS DESIGNED, NOT RENDERED. One visual system for
    // CAO's region-composition surfaces: a dark, quiet shell with a single
    // accent, generous spacing, and typographic hierarchy -- a premium
    // strategy-tool look that deliberately supersedes the stock beveled
    // panel aesthetic rather than skinning it. Every primitive here is
    // flat, deterministic IMGUI: solid fills, hairline borders, hover
    // states, no bevels, no nested boxes.
    internal static class CAOpeningTheme
    {
        // palette -----------------------------------------------------------
        internal static readonly Color Ink = new Color(0.047f, 0.058f, 0.074f);
        internal static readonly Color Surface = new Color(0.075f, 0.090f, 0.112f);
        internal static readonly Color Raised = new Color(0.104f, 0.124f, 0.150f);
        internal static readonly Color RaisedHover = new Color(0.135f, 0.160f, 0.192f);
        internal static readonly Color Hairline = new Color(0.185f, 0.220f, 0.260f);
        internal static readonly Color TextHi = new Color(0.920f, 0.936f, 0.948f);
        internal static readonly Color TextLo = new Color(0.548f, 0.610f, 0.668f);
        // The action color is a restrained warm off-white, not a brand hue:
        // it means "this is the operation", and nothing else on the screen
        // borrows it. Semantic colors (Warn, Danger, faction colors, water,
        // terrain) carry all other meaning.
        internal static readonly Color Accent = new Color(0.780f, 0.752f, 0.664f);
        internal static readonly Color AccentHover = new Color(0.870f, 0.842f, 0.750f);
        internal static readonly Color AccentInk = new Color(0.075f, 0.070f, 0.055f);
        internal static readonly Color Warn = new Color(0.855f, 0.628f, 0.290f);
        internal static readonly Color Danger = new Color(0.815f, 0.372f, 0.340f);

        // spacing rhythm
        internal const float Pad = 14f;
        internal const float Gap = 10f;

        // primitives --------------------------------------------------------

        internal static void SurfacePanel(Rect rect, bool raised = false)
        {
            Widgets.DrawBoxSolid(rect, raised ? Raised : Surface);
            Border(rect, Hairline);
        }

        // THE STANDARD WINDOW SHELL for CAO flow windows. A window opts in
        // with `doWindowBackground = false` and `Margin => 0f`, then its
        // DoWindowContents body is exactly:
        //     var content = BeginWindowSurface(inRect);
        //     try { ...draw against content, origin (0,0)... }
        //     finally { EndWindowSurface(); }
        // Every existing flow window already follows this pattern inline;
        // new windows use these two calls instead of repeating it.
        internal static Rect BeginWindowSurface(Rect inRect,
            float padding = 14f)
        {
            Widgets.DrawBoxSolid(inRect, Ink);
            SurfacePanel(inRect);
            GUI.BeginGroup(inRect.ContractedBy(padding));
            return new Rect(0f, 0f, inRect.width - padding * 2f,
                inRect.height - padding * 2f);
        }

        internal static void EndWindowSurface()
        {
            GUI.EndGroup();
        }

        internal static void Border(Rect rect, Color color)
        {
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 1f),
                color);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width,
                1f), color);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 1f, rect.height),
                color);
            Widgets.DrawBoxSolid(new Rect(rect.xMax - 1f, rect.y, 1f,
                rect.height), color);
        }

        internal static void Divider(float x, float y, float width)
        {
            Widgets.DrawBoxSolid(new Rect(x, y, width, 1f), Hairline);
        }

        // A quiet sentence-case caption opens each section. The eye
        // navigates by these, so they stay consistent and never compete
        // with the data they introduce.
        internal static float SectionLabel(float x, float y, float width,
            string label)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = TextLo;
            Widgets.Label(new Rect(x, y, width, 18f), label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            return 18f;
        }

        internal static float Heading(float x, float y, float width,
            string title)
        {
            Text.Font = GameFont.Medium;
            GUI.color = TextHi;
            float height = Text.CalcHeight(title, width);
            Widgets.Label(new Rect(x, y, width, height), title);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            return height;
        }

        internal static float Body(float x, float y, float width,
            string text, bool secondary = false)
        {
            Text.Font = GameFont.Small;
            GUI.color = secondary ? TextLo : TextHi;
            float height = Text.CalcHeight(text, width);
            Widgets.Label(new Rect(x, y, width, height), text);
            GUI.color = Color.white;
            return height;
        }

        internal static float Fine(float x, float y, float width,
            string text)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = TextLo;
            float height = Text.CalcHeight(text, width);
            Widgets.Label(new Rect(x, y, width, height), text);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            return height;
        }

        // label: value on one wrapped line -- the data rows of the shell
        internal static float Row(float x, float y, float width,
            string label, string value)
        {
            const float labelWidth = 92f;
            Text.Font = GameFont.Small;
            GUI.color = TextLo;
            Widgets.Label(new Rect(x, y, labelWidth, 22f), label);
            GUI.color = TextHi;
            float valueWidth = width - labelWidth - 6f;
            float height = Mathf.Max(22f,
                Text.CalcHeight(value, valueWidth));
            Widgets.Label(new Rect(x + labelWidth + 6f, y, valueWidth,
                height), value);
            GUI.color = Color.white;
            return height;
        }

        // A state toggle: the active one is a lit surface with an underline
        // bar, not a filled accent pill - the action color stays reserved
        // for actions.
        internal static bool Chip(Rect rect, string label, bool selected,
            string tooltip = null)
        {
            bool hover = Mouse.IsOver(rect);
            Widgets.DrawBoxSolid(rect, selected ? RaisedHover
                : hover ? Raised : Surface);
            Border(rect, selected ? TextLo : Hairline);
            if (selected)
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f,
                    rect.width, 2f), TextHi);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = selected || hover ? TextHi : TextLo;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(rect,
                tooltip);
            return Widgets.ButtonInvisible(rect);
        }

        internal static bool PrimaryButton(Rect rect, string label,
            bool active = true)
        {
            bool hover = active && Mouse.IsOver(rect);
            Widgets.DrawBoxSolid(rect, !active
                ? Raised : hover ? AccentHover : Accent);
            if (!active) Border(rect, Hairline);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = active ? AccentInk : TextLo;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            return active && Widgets.ButtonInvisible(rect);
        }

        internal static bool GhostButton(Rect rect, string label,
            string tooltip = null, bool active = true)
        {
            bool hover = active && Mouse.IsOver(rect);
            if (hover) Widgets.DrawBoxSolid(rect, Raised);
            Border(rect, !active ? new Color(Hairline.r, Hairline.g,
                Hairline.b, 0.5f) : hover ? TextLo : Hairline);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = !active
                ? new Color(TextLo.r, TextLo.g, TextLo.b, 0.45f)
                : hover ? TextHi : TextLo;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(rect,
                tooltip);
            return active && Widgets.ButtonInvisible(rect);
        }

        // A statement band with a left accent bar: warnings, refusals, and
        // the honest facts a player should not have to hunt for.
        internal static float Notice(float x, float y, float width,
            string text, Color tone)
        {
            Text.Font = GameFont.Small;
            float textHeight = Text.CalcHeight(text, width - 22f);
            var band = new Rect(x, y, width, textHeight + 12f);
            Widgets.DrawBoxSolid(band,
                new Color(tone.r, tone.g, tone.b, 0.10f));
            Widgets.DrawBoxSolid(new Rect(x, y, 3f, band.height), tone);
            GUI.color = TextHi;
            Widgets.Label(new Rect(x + 14f, y + 6f, width - 22f,
                textHeight), text);
            GUI.color = Color.white;
            return band.height;
        }
    }
}
