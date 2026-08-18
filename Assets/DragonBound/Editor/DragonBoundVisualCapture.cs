using System;
using System.IO;
using DragonBound.Grid;
using DragonBound.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Editor
{
    public static class DragonBoundVisualCapture
    {
        public const string OutputPath = "Logs/DragonBoundPortraitPreview.png";
        public const string RangePreviewOutputPath = "Logs/DragonBoundRangePreview_720x1280.png";

        public static void CapturePortraitPreview()
        {
            CapturePortraitPreviews(new[]
            {
                new CaptureSpec(1080, 1920, OutputPath)
            });
        }

        public static void CaptureAllPortraitPreviews()
        {
            CapturePortraitPreviews(new[]
            {
                new CaptureSpec(720, 1280, "Logs/DragonBoundPortraitPreview_720x1280.png"),
                new CaptureSpec(1080, 1920, OutputPath),
                new CaptureSpec(1080, 2280, "Logs/DragonBoundPortraitPreview_1080x2280.png")
            });
        }

        public static void CaptureSelectedRangePreview()
        {
            CapturePortraitPreviews(
                new[] { new CaptureSpec(720, 1280, RangePreviewOutputPath) },
                ShowAxeRangePreview);
        }

        private static void CapturePortraitPreviews(CaptureSpec[] captures, Action beforeRender = null)
        {
            var scene = EditorSceneManager.OpenScene(
                DragonBoundPortraitUiBuilder.ScenePath,
                OpenSceneMode.Single);
            var canvas = FindInScene<Canvas>(scene);
            var camera = FindInScene<Camera>(scene);
            if (canvas == null || camera == null)
            {
                throw new InvalidOperationException("Greybox_Main requires a Canvas and Camera for capture.");
            }

            var previousTarget = camera.targetTexture;
            var previousRenderMode = canvas.renderMode;
            var previousWorldCamera = canvas.worldCamera;
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;

                foreach (var capture in captures)
                {
                    Capture(canvas, camera, capture, beforeRender);
                }
            }
            finally
            {
                canvas.renderMode = previousRenderMode;
                canvas.worldCamera = previousWorldCamera;
                camera.targetTexture = previousTarget;
            }
        }

        private static void Capture(Canvas canvas, Camera camera, CaptureSpec capture, Action beforeRender)
        {
            var renderTexture = new RenderTexture(
                capture.Width,
                capture.Height,
                24,
                RenderTextureFormat.ARGB32);
            var texture = new Texture2D(capture.Width, capture.Height, TextureFormat.RGB24, false);
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                Canvas.ForceUpdateCanvases();
                beforeRender?.Invoke();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0f, 0f, capture.Width, capture.Height), 0, 0);
                texture.Apply();
                EnsureNonBlank(texture, capture.OutputPath);

                var outputDirectory = Path.GetDirectoryName(capture.OutputPath);
                if (!string.IsNullOrWhiteSpace(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                File.WriteAllBytes(capture.OutputPath, texture.EncodeToPNG());
                Debug.Log($"DragonBound portrait preview created at {Path.GetFullPath(capture.OutputPath)}.");
            }
            finally
            {
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static void ShowAxeRangePreview()
        {
            var screen = UnityEngine.Object.FindObjectOfType<DragonBoundScreenView>();
            var board = screen != null ? screen.PlayerBoardView : null;
            if (board == null || board.RangePreview == null)
            {
                throw new InvalidOperationException("Player range preview is missing from Greybox_Main.");
            }

            GridCellView targetCell = null;
            foreach (var cell in board.CellViews)
            {
                if (cell != null && cell.CellType == CellType.Battle)
                {
                    targetCell = cell;
                    break;
                }
            }

            if (targetCell == null)
            {
                throw new InvalidOperationException("Player battle cell is missing from Greybox_Main.");
            }

            var range = board.RangePreview;
            var rangeParent = range.rectTransform.parent as RectTransform ?? board.UnitLayer;
            var cellSize = Mathf.Min(targetCell.RectTransform.rect.width, targetCell.RectTransform.rect.height);
            range.rectTransform.anchoredPosition = rangeParent.InverseTransformPoint(targetCell.ContentAnchor.position);
            range.rectTransform.sizeDelta = Vector2.one *
                                            (cellSize * UnitRangeRules.GetRadius(BasicUnitArchetype.Axe) * 2f);
            range.rectTransform.SetAsFirstSibling();
            range.enabled = true;
            range.gameObject.SetActive(true);
        }

        private static void EnsureNonBlank(Texture2D texture, string outputPath)
        {
            var pixels = texture.GetPixels32();
            var first = pixels[0];
            var step = Mathf.Max(1, pixels.Length / 4096);
            for (var index = step; index < pixels.Length; index += step)
            {
                if (!pixels[index].Equals(first))
                {
                    return;
                }
            }

            throw new InvalidOperationException($"Visual capture was blank: {outputPath}");
        }

        private readonly struct CaptureSpec
        {
            public CaptureSpec(int width, int height, string outputPath)
            {
                Width = width;
                Height = height;
                OutputPath = outputPath;
            }

            public int Width { get; }
            public int Height { get; }
            public string OutputPath { get; }
        }

        private static T FindInScene<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
