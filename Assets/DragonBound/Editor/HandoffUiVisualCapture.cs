using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonBound.Editor
{
    public static class HandoffUiVisualCapture
    {
        [MenuItem("DragonBound/Handoff/Capture UI Handoff Previews")]
        public static void CaptureForAutomation()
        {
            HandoffUiAssetBuilder.BuildIfMissing();
            var scene = EditorSceneManager.OpenScene(HandoffUiAssetBuilder.ScenePath, OpenSceneMode.Single);
            var camera = Find<Camera>(scene);
            if (camera == null) throw new InvalidOperationException("UI_Handoff preview camera is missing.");
            var presenter = Find<DragonBound.HandoffUi.HandoffPreviewPresenter>(scene);
            if (presenter == null) throw new InvalidOperationException("UI_Handoff preview presenter is missing.");
            presenter.Show(DragonBound.HandoffUi.ItemHudState.Available, DragonBound.HandoffUi.MerchantOfferState.Ad);
            Capture(camera, 1080, 1920, "Logs/UI_Handoff_1080x1920.png");
            Capture(camera, 2048, 1536, "Logs/UI_Handoff_2048x1536.png");
        }
        private static void Capture(Camera camera, int width, int height, string path)
        {
            var render = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false); var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            try
            {
                camera.targetTexture = render;
                Canvas.ForceUpdateCanvases();
                var layout = UnityEngine.Object.FindObjectOfType<DragonBound.HandoffUi.HandoffResponsiveLayout>();
                if (layout != null) layout.Apply();
                Canvas.ForceUpdateCanvases();
                var canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
                if (canvas != null) LayoutRebuilder.ForceRebuildLayoutImmediate(canvas.GetComponent<RectTransform>());
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = render;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(render); }
        }
        private static T Find<T>(Scene scene) where T : Component { foreach (var root in scene.GetRootGameObjects()) { var value = root.GetComponentInChildren<T>(true); if (value != null) return value; } return null; }
    }
}
