using DragonBound.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    /// <summary>Development-only greybox overlay. It reads diagnostics but cannot receive input.</summary>
    public sealed class PressureRunDiagnosticsPanel : MonoBehaviour
    {
        private PressureRunDiagnostics diagnostics;
        private Text label;

        public static PressureRunDiagnosticsPanel Create(PressureRunDiagnostics value)
        {
            var root = new GameObject(
                "DEV_PressureRunDiagnostics",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(PressureRunDiagnosticsPanel));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            root.GetComponent<GraphicRaycaster>().enabled = false;

            var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.SetParent(root.transform, false);
            backgroundRect.anchorMin = new Vector2(0.01f, 0.68f);
            backgroundRect.anchorMax = new Vector2(0.48f, 0.98f);
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(0.03f, 0.05f, 0.07f, 0.82f);
            backgroundImage.raycastTarget = false;

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(background.transform, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 10f);
            textRect.offsetMax = new Vector2(-14f, -10f);
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 22;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color(0.86f, 0.93f, 0.96f, 1f);
            text.raycastTarget = false;

            var panel = root.GetComponent<PressureRunDiagnosticsPanel>();
            panel.Configure(value, text);
            return panel;
        }

        public void Configure(PressureRunDiagnostics value, Text targetLabel)
        {
            diagnostics = value;
            label = targetLabel;
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            diagnostics = null;
        }

        private void Refresh()
        {
            if (diagnostics == null || label == null)
            {
                return;
            }

            var side = diagnostics.Player;
            label.text =
                $"DEV PRESSURE\n" +
                $"WAVE {side.CurrentWave}  TIME {side.ElapsedRunTime:0.0}s\n" +
                $"HP {side.BaseHP}  RESOURCE {side.CurrentResources}\n" +
                $"RECRUITS {side.SuccessfulRecruitCount}\n" +
                $"BAG {side.RemainingComponentCount}/{side.DeliveredComponentCount + side.RemainingComponentCount}\n" +
                $"SHOVEL {side.ShovelsGenerated}/{side.ShovelsUsed}\n" +
                $"CELLS {side.OpenCellCount}/40\n" +
                $"BOARD {side.OccupiedBoardCellCount}  BENCH {side.BenchOccupiedCount}\n" +
                $"HEROES {side.CurrentHeroCount}\n" +
                $"KILLS {side.KilledEnemies}  LEAKS {side.ReachedGoalEnemies}";
        }
    }
}
