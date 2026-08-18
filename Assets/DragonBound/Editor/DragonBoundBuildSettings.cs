using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace DragonBound.Editor
{
    internal static class DragonBoundBuildSettings
    {
        public static void UpsertEnabledScenes(params string[] canonicalPaths)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var canonicalPath in canonicalPaths)
            {
                ReplaceSceneEntry(scenes, canonicalPath);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ReplaceSceneEntry(
            List<EditorBuildSettingsScene> scenes,
            string canonicalPath)
        {
            var fileName = Path.GetFileName(canonicalPath);
            for (var index = scenes.Count - 1; index >= 0; index--)
            {
                if (string.Equals(
                        Path.GetFileName(scenes[index].path),
                        fileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    scenes.RemoveAt(index);
                }
            }

            scenes.Add(new EditorBuildSettingsScene(canonicalPath, true));
        }
    }
}
