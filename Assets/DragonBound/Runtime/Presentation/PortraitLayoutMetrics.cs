using System;
using DragonBound.Core;
using DragonBound.Grid;
using UnityEngine;

namespace DragonBound.Presentation
{
    public readonly struct PortraitLayoutSnapshot
    {
        public PortraitLayoutSnapshot(Rect bounds)
        {
            if (bounds.width <= 0f || bounds.height <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(bounds));
            }

            Bounds = bounds;
            BottomGuard = Slice(bounds, 0f, 0.03f);
            CallToActionBand = Slice(bounds, 0.03f, 0.13f);
            BenchBand = Slice(bounds, 0.13f, 0.23f);
            PlayerField = Slice(bounds, 0.23f, 0.52f);
            VersusBand = Slice(bounds, 0.52f, 0.60f);
            AiField = Slice(bounds, 0.60f, 0.89f);
            TopHud = Slice(bounds, 0.89f, 1f);
            Arena = Rect.MinMaxRect(bounds.xMin, PlayerField.yMin, bounds.xMax, AiField.yMax);
            CellSize = PortraitLayoutMetrics.FormationCellReferenceSize *
                (bounds.width / PortraitLayoutMetrics.ReferenceResolution.x);

            var ctaWidth = bounds.width * 0.37f;
            var ctaHeight = CallToActionBand.height * 0.68f;
            RecruitButton = new Rect(
                bounds.center.x - (ctaWidth * 0.5f),
                CallToActionBand.center.y - (ctaHeight * 0.5f),
                ctaWidth,
                ctaHeight);
        }

        public Rect Bounds { get; }
        public Rect BottomGuard { get; }
        public Rect BottomSafeArea => BottomGuard;
        public Rect CallToActionBand { get; }
        public Rect BenchBand { get; }
        public Rect PlayerField { get; }
        public Rect VersusBand { get; }
        public Rect AiField { get; }
        public Rect Arena { get; }
        public Rect TopHud { get; }
        public Rect RecruitButton { get; }
        public float CellSize { get; }

        public Rect GetBenchSlot(int index)
        {
            if (index < 0 || index >= 5)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var rowWidth = Bounds.width * 0.70f;
            var gap = Bounds.width * 0.006f;
            var slotWidth = (rowWidth - (gap * 4f)) / 5f;
            var slotHeight = Mathf.Min(slotWidth, BenchBand.height * 0.72f);
            var rowLeft = Bounds.center.x - (rowWidth * 0.5f);
            return new Rect(
                rowLeft + (index * (slotWidth + gap)),
                BenchBand.center.y - (slotHeight * 0.5f),
                slotWidth,
                slotHeight);
        }

        public Rect GetLogicalCell(GridPosition position, CellType cellType)
        {
            if (cellType == CellType.Bench)
            {
                return GetBenchSlot(position.X);
            }

            return GetFormationCell(TeamSide.Player, position);
        }

        public Rect GetFormationCell(TeamSide side, GridPosition position)
        {
            if (position.X < 0 || position.X > 2 || position.Y < 1 || position.Y > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            var field = side == TeamSide.Player ? PlayerField : AiField;
            var centerX = Bounds.xMin + (Bounds.width *
                (0.5f + ((position.X - 1) * PortraitLayoutMetrics.FormationColumnStep)));
            var centerNormalizedY = side == TeamSide.Player
                ? PortraitLayoutMetrics.PlayerFirstRowY - ((position.Y - 1) * PortraitLayoutMetrics.FormationRowStep)
                : PortraitLayoutMetrics.AiFirstRowY + ((position.Y - 1) * PortraitLayoutMetrics.FormationRowStep);
            var centerY = field.yMin + (field.height * centerNormalizedY);
            return Centered(centerX, centerY, CellSize);
        }

        public Rect GetRoad(TeamSide side, bool rightSide)
        {
            var field = side == TeamSide.Player ? PlayerField : AiField;
            var minX = rightSide ? 0.86f : 0.035f;
            var maxX = rightSide ? 0.965f : 0.14f;
            return Rect.MinMaxRect(
                Bounds.xMin + (Bounds.width * minX),
                field.yMin + (field.height * 0.05f),
                Bounds.xMin + (Bounds.width * maxX),
                field.yMax - (field.height * 0.05f));
        }

        private static Rect Slice(Rect bounds, float minNormalizedY, float maxNormalizedY)
        {
            return new Rect(
                bounds.xMin,
                bounds.yMin + (bounds.height * minNormalizedY),
                bounds.width,
                bounds.height * (maxNormalizedY - minNormalizedY));
        }

        private static Rect Centered(float centerX, float centerY, float size)
        {
            return new Rect(centerX - (size * 0.5f), centerY - (size * 0.5f), size, size);
        }
    }

    public static class PortraitLayoutMetrics
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
        public const float FormationCellReferenceSize = 116f;
        public const float FormationColumnStep = 0.125f;
        public const float FormationRowStep = 0.23f;
        public const float PlayerFirstRowY = 0.74f;
        public const float AiFirstRowY = 0.26f;

        public static PortraitLayoutSnapshot Calculate(Rect safeBounds)
        {
            return new PortraitLayoutSnapshot(safeBounds);
        }
    }
}
