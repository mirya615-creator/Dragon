using System;
using System.Collections.Generic;
using DragonBound.Core;
using DragonBound.Grid;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    [Flags]
    public enum BoardDebugOverlayOptions
    {
        None = 0,
        ShowCoordinates = 1 << 0,
        ShowCellRoles = 1 << 1,
        ShowCellBounds = 1 << 2,
        ShowPathOrder = 1 << 3,
        ShowRuntimeCoordinates = 1 << 4,
        ShowConfigCoordinates = 1 << 5,
        ShowOwner = 1 << 6,
        ShowPathProgress = 1 << 7,
        ShowAttackRange = 1 << 8
    }

    /// <summary>
    /// Development-only overlay for inspecting the fixed 8 by 10 board. It is an isolated
    /// CanvasGroup with raycasts disabled, so its labels cannot change selection or dragging.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardDebugOverlay : MonoBehaviour
    {
        private const BoardDebugOverlayOptions DefaultOptions =
            BoardDebugOverlayOptions.ShowCoordinates |
            BoardDebugOverlayOptions.ShowCellRoles |
            BoardDebugOverlayOptions.ShowPathOrder;

        private readonly Dictionary<GridPosition, CellDebugVisual> visuals =
            new Dictionary<GridPosition, CellDebugVisual>();
        private readonly Dictionary<TeamSide, float> pathProgress =
            new Dictionary<TeamSide, float>();

        [SerializeField] private bool visibleOnStart;
        [SerializeField] private BoardDebugOverlayOptions options = DefaultOptions;

        private FixedBoardLayoutDefinition layout;
        private Text pathProgressLabel;
        private CanvasGroup canvasGroup;
        private bool isConfigured;

        public bool IsVisible => gameObject.activeSelf;
        public bool BlocksRaycasts => canvasGroup != null && canvasGroup.blocksRaycasts;
        public BoardDebugOverlayOptions Options => options;
        public int CellVisualCount => visuals.Count;

        public static BoardDebugOverlay Create(
            RectTransform parent,
            FixedBoardLayoutDefinition definition)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var existing = parent.Find("DEV_BoardDebugOverlay");
            if (existing != null && existing.TryGetComponent<BoardDebugOverlay>(out var existingOverlay))
            {
                existingOverlay.Configure(definition);
                return existingOverlay;
            }

            var root = new GameObject(
                "DEV_BoardDebugOverlay",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(BoardDebugOverlay));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var overlay = root.GetComponent<BoardDebugOverlay>();
            overlay.Configure(definition);
            overlay.SetVisible(false);
            return overlay;
        }

        public void Configure(FixedBoardLayoutDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (isConfigured && layout != definition)
            {
                throw new InvalidOperationException("A board debug overlay cannot switch layouts during a match.");
            }

            layout = definition;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            if (!isConfigured)
            {
                BuildCellVisuals();
                BuildPathProgressLabel();
                isConfigured = true;
            }

            Refresh();
        }

        public void SetVisible(bool value)
        {
            visibleOnStart = value;
            gameObject.SetActive(value);
        }

        public void SetOptions(BoardDebugOverlayOptions value)
        {
            options = value;
            Refresh();
        }

        public void SetOption(BoardDebugOverlayOptions option, bool enabled)
        {
            options = enabled ? options | option : options & ~option;
            Refresh();
        }

        public bool HasOption(BoardDebugOverlayOptions option)
        {
            return (options & option) == option;
        }

        public void SetPathProgress(TeamSide side, float value)
        {
            pathProgress[side] = Mathf.Clamp01(value);
            RefreshPathProgress();
        }

        public bool TryGetCellLabel(GridPosition position, out Text label)
        {
            if (visuals.TryGetValue(position, out var visual))
            {
                label = visual.Label;
                return label != null;
            }

            label = null;
            return false;
        }

        public bool TryGetCellBounds(GridPosition position, out RectTransform bounds)
        {
            if (visuals.TryGetValue(position, out var visual))
            {
                bounds = visual.Bounds;
                return bounds != null;
            }

            bounds = null;
            return false;
        }

        private void Awake()
        {
            if (!visibleOnStart)
            {
                gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (HasOption(BoardDebugOverlayOptions.ShowPathProgress))
            {
                RefreshPathProgress();
            }
        }

        private void BuildCellVisuals()
        {
            foreach (var cell in layout.CellDefinitions)
            {
                var root = CreateRect($"DEV_Cell_{cell.Coordinate.X}_{cell.Coordinate.Y}", transform);
                ConfigureCellRect(root, cell.Coordinate);

                var bounds = CreateRect("DEV_CellBounds", root);
                Stretch(bounds);
                var boundsImage = bounds.gameObject.AddComponent<Image>();
                boundsImage.color = new Color(0.2f, 0.9f, 1f, 0.012f);
                boundsImage.raycastTarget = false;
                var outline = bounds.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.2f, 0.9f, 1f, 0.78f);
                outline.effectDistance = new Vector2(0.65f, 0.65f);

                var label = CreateText("DEV_CellLabel", root, TextAnchor.UpperLeft, 10);
                label.rectTransform.anchorMin = new Vector2(0.06f, 0.06f);
                label.rectTransform.anchorMax = new Vector2(0.94f, 0.94f);
                label.rectTransform.offsetMin = Vector2.zero;
                label.rectTransform.offsetMax = Vector2.zero;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;

                visuals.Add(cell.Coordinate, new CellDebugVisual(cell, label, bounds));
            }
        }

        private void BuildPathProgressLabel()
        {
            pathProgressLabel = CreateText("DEV_PathProgress", transform, TextAnchor.UpperRight, 12);
            pathProgressLabel.rectTransform.anchorMin = new Vector2(0.58f, 0.95f);
            pathProgressLabel.rectTransform.anchorMax = new Vector2(0.98f, 0.995f);
            pathProgressLabel.rectTransform.offsetMin = Vector2.zero;
            pathProgressLabel.rectTransform.offsetMax = Vector2.zero;
            pathProgressLabel.color = new Color(0.9f, 0.95f, 1f, 0.94f);
        }

        private void Refresh()
        {
            if (!isConfigured)
            {
                return;
            }

            foreach (var visual in visuals.Values)
            {
                visual.Bounds.gameObject.SetActive(HasOption(BoardDebugOverlayOptions.ShowCellBounds));
                visual.Label.text = BuildCellText(visual.Definition);
                visual.Label.gameObject.SetActive(!string.IsNullOrEmpty(visual.Label.text));
            }

            RefreshPathProgress();
        }

        private void RefreshPathProgress()
        {
            if (pathProgressLabel == null)
            {
                return;
            }

            var show = HasOption(BoardDebugOverlayOptions.ShowPathProgress);
            pathProgressLabel.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            pathProgress.TryGetValue(TeamSide.Player, out var playerProgress);
            pathProgress.TryGetValue(TeamSide.AI, out var aiProgress);
            pathProgressLabel.text = $"P {playerProgress:0.000}\nA {aiProgress:0.000}";
        }

        private string BuildCellText(FixedBoardCellDefinition definition)
        {
            var lines = new List<string>(4);
            if (HasOption(BoardDebugOverlayOptions.ShowCellRoles))
            {
                lines.Add(GetRoleCode(definition));
            }

            if (HasOption(BoardDebugOverlayOptions.ShowCoordinates) ||
                HasOption(BoardDebugOverlayOptions.ShowConfigCoordinates))
            {
                lines.Add($"R{FixedBoardLayoutDefinition.ToConfigRow(definition.Coordinate)} C{definition.Coordinate.X}");
            }

            if (HasOption(BoardDebugOverlayOptions.ShowCoordinates) ||
                HasOption(BoardDebugOverlayOptions.ShowRuntimeCoordinates))
            {
                lines.Add($"X{definition.Coordinate.X} Y{definition.Coordinate.Y}");
            }

            if (HasOption(BoardDebugOverlayOptions.ShowOwner) &&
                definition.Owner != FixedBoardCellOwner.None)
            {
                lines.Add(definition.Owner == FixedBoardCellOwner.Player ? "PLAYER" : "AI");
            }

            if (HasOption(BoardDebugOverlayOptions.ShowPathOrder) &&
                TryGetPathOrder(definition, out var order))
            {
                lines.Add(order);
            }

            return string.Join("\n", lines);
        }

        private bool TryGetPathOrder(FixedBoardCellDefinition definition, out string order)
        {
            var path = definition.Owner == FixedBoardCellOwner.Player
                ? layout.PlayerLaneWaypoints
                : definition.Owner == FixedBoardCellOwner.AI
                    ? layout.AiLaneWaypoints
                    : null;
            if (path != null)
            {
                for (var index = 0; index < path.Count; index++)
                {
                    if (path[index] == definition.Coordinate)
                    {
                        order = definition.Owner == FixedBoardCellOwner.Player
                            ? $"P{index:00}"
                            : $"A{index:00}";
                        return true;
                    }
                }
            }

            order = string.Empty;
            return false;
        }

        private static string GetRoleCode(FixedBoardCellDefinition definition)
        {
            switch (definition.Role)
            {
                case FixedBoardCellRole.Deployment:
                    return definition.DeployState == FixedBoardDeployState.Unlocked ? "U" : "L";
                case FixedBoardCellRole.Lane:
                    return "R";
                case FixedBoardCellRole.Spawn:
                    return "S";
                case FixedBoardCellRole.Goal:
                    return "G";
                default:
                    return "?";
            }
        }

        private void ConfigureCellRect(RectTransform target, GridPosition position)
        {
            target.anchorMin = new Vector2(
                position.X / (float)layout.Columns,
                position.Y / (float)layout.Rows);
            target.anchorMax = new Vector2(
                (position.X + 1f) / layout.Columns,
                (position.Y + 1f) / layout.Rows);
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Text CreateText(string name, Transform parent, TextAnchor alignment, int fontSize)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text))
                .GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.95f, 0.98f, 1f, 0.94f);
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform target)
        {
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        private readonly struct CellDebugVisual
        {
            public CellDebugVisual(FixedBoardCellDefinition definition, Text label, RectTransform bounds)
            {
                Definition = definition;
                Label = label;
                Bounds = bounds;
            }

            public FixedBoardCellDefinition Definition { get; }
            public Text Label { get; }
            public RectTransform Bounds { get; }
        }
    }
}
