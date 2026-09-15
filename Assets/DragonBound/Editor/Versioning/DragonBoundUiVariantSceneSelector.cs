using DragonBound.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace DragonBound.Editor.Versioning
{
    [InitializeOnLoad]
    internal static class DragonBoundUiVariantSceneSelector
    {
        static DragonBoundUiVariantSceneSelector()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += SelectForActiveScene;
        }

        private static void OnActiveSceneChanged(Scene previous, Scene next) => SelectForScene(next);

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) SelectForActiveScene();
        }

        private static void SelectForActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SelectForScene(SceneManager.GetActiveScene());
        }

        private static void SelectForScene(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || string.IsNullOrWhiteSpace(scene.path)) return;

            var path = scene.path.Replace('\\', '/');
            if (path.StartsWith(UiVariantProjectPaths.V2Root + "/"))
            {
                if (UiAssets.Active == null || UiAssets.Active.VariantId != "V2")
                    DragonBoundUiVersionTools.SetActiveV2();
                return;
            }

            if (path.StartsWith(UiVariantProjectPaths.V1Root + "/") &&
                (UiAssets.Active == null || UiAssets.Active.VariantId != "V1"))
            {
                DragonBoundUiVersionTools.SetActiveV1();
            }
        }
    }
}
