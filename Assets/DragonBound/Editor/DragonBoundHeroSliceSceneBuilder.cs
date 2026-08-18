using System;
using DragonBound.Bootstrap;
using DragonBound.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DragonBound.Editor
{
    public static class DragonBoundHeroSliceSceneBuilder
    {
        public const string BasicScenePath = DragonBoundScenePaths.GreyboxAssetPath;
        public const string HeroScenePath = DragonBoundScenePaths.HeroSliceAssetPath;

        [MenuItem("DragonBound/Hero Slice/Create or Update Scene")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BasicScenePath) == null)
            {
                throw new InvalidOperationException($"Basic scene is missing: {BasicScenePath}");
            }

            var scene = EditorSceneManager.OpenScene(BasicScenePath, OpenSceneMode.Single);
            DragonBoundBootstrap bootstrap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                bootstrap = root.GetComponentInChildren<DragonBoundBootstrap>(true);
                if (bootstrap != null)
                {
                    break;
                }
            }

            if (bootstrap == null)
            {
                throw new InvalidOperationException("Greybox_Main has no DragonBoundBootstrap.");
            }

            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("enableHeroComponents").boolValue = true;
            serialized.FindProperty("heroSliceMode").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, HeroScenePath, true))
            {
                throw new InvalidOperationException($"Unable to save {HeroScenePath}.");
            }

            DragonBoundBuildSettings.UpsertEnabledScenes(BasicScenePath, HeroScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("HeroSlice_Main created with EnableHeroComponents=true and HeroSliceMode=true.");
        }
    }
}
