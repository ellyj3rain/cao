using System;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // BUILT PLACES, DRAWN AS BUILT PLACES, at GUI scale. The same visual
    // language the rendered reference world uses - roofed building
    // clusters that grow with standing, hut-and-field holdings - for the
    // overlay marks every regional surface draws over its ground: the
    // in-game globe, the regional viewport in both modes, and the setup
    // widget. A mark is a place, never a coded square.
    internal static class CAPlaceGlyphs
    {
        private static readonly Color WallTone =
            new Color(0.30f, 0.27f, 0.22f);
        private static readonly Color RidgeLight =
            new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color GroundShadow =
            new Color(0.05f, 0.05f, 0.04f, 0.45f);
        private static readonly Color FieldLight =
            new Color(0.51f, 0.46f, 0.27f);
        private static readonly Color FieldDark =
            new Color(0.44f, 0.40f, 0.23f);
        private static readonly Color StoneTone =
            new Color(0.41f, 0.39f, 0.36f);

        // Deterministic house offsets (unit space), reused across every
        // consumer so a place always builds the same way.
        private static readonly Vector2[] HouseOffsets =
        {
            new Vector2(0f, 0f),
            new Vector2(-0.9f, 0.45f),
            new Vector2(0.85f, -0.35f),
            new Vector2(-0.55f, -0.75f),
            new Vector2(0.65f, 0.7f),
            new Vector2(-1.15f, -0.15f),
            new Vector2(1.15f, 0.25f),
            new Vector2(0.1f, -1.05f),
            new Vector2(-0.35f, 1.05f),
            new Vector2(1.0f, -0.95f),
            new Vector2(-1.05f, 1.0f),
            new Vector2(0.35f, 1.3f)
        };

        internal static int HouseCountFor(int standing)
        {
            return standing <= 0 ? 1
                : standing == 1 ? 2
                : standing == 2 ? 3
                : standing == 3 ? 5
                : standing == 4 ? 8 : 11;
        }

        // One settlement, centered on `center`. `scale` is the size of a
        // single house in pixels (3.5-6 works for overlays); the cluster
        // footprint grows with standing. `roof` carries the owner.
        internal static void DrawSettlement(Vector2 center, float scale,
            int standing, Color roof)
        {
            int houses = HouseCountFor(standing);
            float spreadX = scale * (standing >= 5 ? 2.4f
                : standing == 4 ? 2.1f : standing >= 2 ? 1.5f : 1.0f);
            float spreadY = spreadX * 0.72f;

            // A soft ground shadow gathers the cluster into one place on
            // any terrain.
            float haloW = spreadX * 2f + scale * 1.6f;
            float haloH = spreadY * 2f + scale * 1.4f;
            Widgets.DrawBoxSolid(new Rect(center.x - haloW * 0.5f,
                center.y - haloH * 0.5f + scale * 0.2f, haloW, haloH),
                GroundShadow);

            // The city wall stands behind its buildings.
            if (standing >= 5)
                DrawRing(center, spreadX + scale * 0.9f,
                    spreadY + scale * 0.8f, StoneTone);

            for (int i = 0; i < houses; i++)
            {
                Vector2 offset = HouseOffsets[i % HouseOffsets.Length];
                float hx = center.x + offset.x * spreadX
                    - (offset.x * spreadX * 0.08f);
                float hy = center.y + offset.y * spreadY;
                bool great = i == 0 && standing >= 4;
                float w = great ? scale * 1.5f : scale;
                float h = great ? scale * 1.25f : scale * 0.85f;
                Color body = i == 0 ? roof
                    : Color.Lerp(roof, WallTone,
                        0.12f + 0.10f * (i % 3));
                // Shadow, body, lit ridge: the three strokes that make a
                // rectangle read as a roof.
                Widgets.DrawBoxSolid(new Rect(hx - w * 0.5f + 1f,
                    hy - h * 0.5f + 1f, w, h), GroundShadow);
                Widgets.DrawBoxSolid(new Rect(hx - w * 0.5f,
                    hy - h * 0.5f, w, h), body);
                Widgets.DrawBoxSolid(new Rect(hx - w * 0.5f,
                    hy - h * 0.5f, w, Mathf.Max(1f, h * 0.3f)),
                    RidgeLight);
            }

            // The keep: cities carry one taller stone building.
            if (standing >= 5)
            {
                float kw = scale * 1.1f;
                float kh = scale * 1.5f;
                Widgets.DrawBoxSolid(new Rect(center.x - kw * 0.5f,
                    center.y - kh * 0.7f, kw, kh), StoneTone);
                Widgets.DrawBoxSolid(new Rect(center.x - kw * 0.5f,
                    center.y - kh * 0.7f, kw, Mathf.Max(1f, kh * 0.22f)),
                    RidgeLight);
            }
        }

        // One frontier holding: a hut beside its worked patch.
        internal static void DrawHolding(Vector2 center, float scale,
            int material)
        {
            float fieldW = scale * (2.0f + material * 0.5f);
            float fieldH = scale * 1.2f;
            float rows = Mathf.Max(2f, fieldH / 2f);
            for (int r = 0; r < rows; r++)
                Widgets.DrawBoxSolid(new Rect(center.x - fieldW * 0.35f,
                    center.y + scale * 0.35f + r * 2f, fieldW, 1.4f),
                    (r & 1) == 0 ? FieldLight : FieldDark);
            float w = scale * (0.9f + material * 0.12f);
            float h = scale * 0.75f;
            Widgets.DrawBoxSolid(new Rect(center.x - w * 0.5f + 1f,
                center.y - h + 1f, w, h), GroundShadow);
            Widgets.DrawBoxSolid(new Rect(center.x - w * 0.5f,
                center.y - h, w, h),
                new Color(0.49f, 0.38f, 0.26f));
            Widgets.DrawBoxSolid(new Rect(center.x - w * 0.5f,
                center.y - h, w, Mathf.Max(1f, h * 0.35f)), RidgeLight);
        }

        // A distant place on the horizon: one small silhouette, lit or
        // dark. The non-spatial distant-world treatment builds from these.
        internal static void DrawDistantPlace(Vector2 center, float scale,
            bool active)
        {
            Color body = active ? new Color(0.62f, 0.52f, 0.36f)
                : new Color(0.16f, 0.17f, 0.19f);
            Widgets.DrawBoxSolid(new Rect(center.x - scale * 0.5f,
                center.y - scale * 0.4f, scale, scale * 0.8f), body);
            Widgets.DrawBoxSolid(new Rect(center.x - scale * 0.5f,
                center.y - scale * 0.4f, scale,
                Mathf.Max(1f, scale * 0.25f)),
                active ? RidgeLight : new Color(1f, 1f, 1f, 0.08f));
            if (active)
                Widgets.DrawBoxSolid(new Rect(center.x - 1f,
                    center.y + scale * 0.05f, 2f, 2f),
                    new Color(0.95f, 0.85f, 0.55f));
        }

        private static void DrawRing(Vector2 center, float radiusX,
            float radiusY, Color tone)
        {
            for (float angle = 0f; angle < 6.2831f; angle += 0.22f)
            {
                float x = center.x + Mathf.Cos(angle) * radiusX;
                float y = center.y + Mathf.Sin(angle) * radiusY;
                Widgets.DrawBoxSolid(new Rect(x - 1f, y - 1f, 2f, 2f),
                    tone);
            }
        }
    }
}
