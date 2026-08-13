using System;

namespace ColonistAwareness
{
    internal enum CARegionLayoutMode
    {
        Wide,
        Medium,
        Compact
    }

    internal readonly struct CAStartingRegionLayoutMeasurement
    {
        internal readonly int PhysicalWidth;
        internal readonly int PhysicalHeight;
        internal readonly float UiScale;
        internal readonly float PageWidth;
        internal readonly float PageHeight;
        internal readonly float BodyHeight;
        internal readonly float ObjectRailWidth;
        internal readonly float MapWidth;
        internal readonly float DetailsWidth;
        internal readonly float LongComparisonRowHeight;
        internal readonly float LongObjectCardHeight;
        internal readonly float LongRelationRowHeight;
        internal readonly float LongMapBadgeHeight;
        internal readonly float LongContextStripHeight;
        internal readonly CARegionLayoutMode Mode;

        internal CAStartingRegionLayoutMeasurement(int physicalWidth,
            int physicalHeight, float uiScale, float pageWidth,
            float pageHeight, float bodyHeight, float objectRailWidth,
            float mapWidth, float detailsWidth, CARegionLayoutMode mode,
            float comparisonHeight, float objectCardHeight,
            float relationHeight, float badgeHeight, float contextHeight)
        {
            PhysicalWidth = physicalWidth;
            PhysicalHeight = physicalHeight;
            UiScale = uiScale;
            PageWidth = pageWidth;
            PageHeight = pageHeight;
            BodyHeight = bodyHeight;
            ObjectRailWidth = objectRailWidth;
            MapWidth = mapWidth;
            DetailsWidth = detailsWidth;
            LongComparisonRowHeight = comparisonHeight;
            LongObjectCardHeight = objectCardHeight;
            LongRelationRowHeight = relationHeight;
            LongMapBadgeHeight = badgeHeight;
            LongContextStripHeight = contextHeight;
            Mode = mode;
        }

        internal bool Fits
        {
            get
            {
                if (PageWidth < 420f || PageHeight < 240f
                    || BodyHeight < 78f) return false;
                if (LongComparisonRowHeight < 28f
                    || LongObjectCardHeight < 42f
                    || LongRelationRowHeight < 28f
                    || LongMapBadgeHeight < 22f
                    || LongContextStripHeight < 22f) return false;
                if (Mode == CARegionLayoutMode.Compact)
                    return MapWidth == PageWidth && DetailsWidth == PageWidth;
                return ObjectRailWidth >= 190f && MapWidth >= 440f
                    && DetailsWidth >= 430f
                    && ObjectRailWidth + MapWidth + DetailsWidth + 20f
                        <= PageWidth + 0.01f;
            }
        }
    }

    // Pure layout contract shared by the page and current measurement receipts.
    internal static class CAStartingRegionLayoutHarness
    {
        internal static CARegionLayoutMode ModeFor(float width)
        {
            return width >= 1600f ? CARegionLayoutMode.Wide
                : width >= 1180f ? CARegionLayoutMode.Medium
                    : CARegionLayoutMode.Compact;
        }

        internal static float ObjectRailWidth(float width,
            CARegionLayoutMode mode)
        {
            if (mode == CARegionLayoutMode.Wide)
                return Math.Min(280f, Math.Max(240f, width * 0.17f));
            return Math.Min(220f, Math.Max(190f, width * 0.17f));
        }

        internal static float DetailsWidth(float width,
            CARegionLayoutMode mode)
        {
            if (mode == CARegionLayoutMode.Compact) return width;
            if (mode == CARegionLayoutMode.Wide)
                return Math.Min(540f, Math.Max(470f, width * 0.29f));
            return Math.Min(460f, Math.Max(430f, width * 0.36f));
        }

        internal static CAStartingRegionLayoutMeasurement Measure(
            int physicalWidth, int physicalHeight, float uiScale)
        {
            float scale = Math.Max(1f, uiScale);
            float logicalWidth = physicalWidth / scale;
            float logicalHeight = physicalHeight / scale;
            float pageWidth = Math.Min(1880f, logicalWidth - 16f);
            float pageHeight = Math.Min(1040f, logicalHeight - 16f);
            float bodyHeight = pageHeight - 92f - 54f;
            CARegionLayoutMode mode = ModeFor(pageWidth);
            float details = DetailsWidth(pageWidth, mode);
            float comparisonWidth = Math.Max(80f,
                (details - 12f) / 4f - 8f);
            float comparison = Math.Max(28f, MeasureText(
                "The Confederated Hearths of Red Cervexa and Lower Mantis",
                comparisonWidth, scale, 16f) + 8f);
            float cardWidth = mode == CARegionLayoutMode.Compact
                ? Math.Max(190f, pageWidth * 0.34f) : ObjectRailWidth(
                    pageWidth, mode);
            float objectCard = 11f + MeasureText(
                "The Confederated Hearths of Red Cervexa and Lower Mantis",
                cardWidth - 24f, scale, 16f) + MeasureText(
                "12 settlements - independent with shared defense",
                cardWidth - 24f, scale, 13f);
            float relation = Math.Max(28f, MeasureText(
                "The Confederated Hearths of Red Cervexa: Allied",
                details - 12f, scale, 16f) + 8f);
            float badge = Math.Max(22f, MeasureText(
                "The Confederated Hearths of Red Cervexa and Lower Mantis",
                Math.Min(320f, pageWidth * 0.38f) - 12f,
                scale, 13f) + 6f);
            float context = Math.Max(22f, MeasureText(
                "Selected neighboring settlement - 42 tiles from the region",
                Math.Max(80f, pageWidth - 120f), scale, 13f));
            if (mode == CARegionLayoutMode.Compact)
                return new CAStartingRegionLayoutMeasurement(physicalWidth,
                    physicalHeight, scale, pageWidth, pageHeight, bodyHeight,
                    0f, pageWidth, pageWidth, mode, comparison, objectCard,
                    relation, badge, context);
            float rail = ObjectRailWidth(pageWidth, mode);
            float map = Math.Max(440f, pageWidth - rail - details - 20f);
            return new CAStartingRegionLayoutMeasurement(physicalWidth,
                physicalHeight, scale, pageWidth, pageHeight, bodyHeight,
                rail, map, details, mode, comparison, objectCard, relation,
                badge, context);
        }

        // Deterministic conservative text-height model for the receipt
        // matrix. Runtime remains authoritative through Text.CalcHeight.
        private static float MeasureText(string text, float width,
            float uiScale, float fontSize)
        {
            float characterWidth = fontSize * 0.56f * Math.Max(1f, uiScale);
            int perLine = Math.Max(1, (int)(width / characterWidth));
            int lines = Math.Max(1, (int)Math.Ceiling(
                (text?.Length ?? 0) / (double)perLine));
            return lines * (fontSize + 3f);
        }
    }
}
