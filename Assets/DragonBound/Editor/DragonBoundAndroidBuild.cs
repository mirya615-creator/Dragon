using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DragonBound.Editor
{
    public static class DragonBoundAndroidBuild
    {
        public const string OutputPath = "Builds/Android/Dragon.apk";

        public static void BuildApk()
        {
            var outputDirectory = Path.GetDirectoryName(OutputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException("APK output directory is invalid.");
            }

            Directory.CreateDirectory(outputDirectory);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false;

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new BuildFailedException("No enabled scenes are configured in Build Settings.");
            }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded || !File.Exists(OutputPath))
            {
                throw new BuildFailedException(
                    $"Android build failed: {report.summary.result}, errors={report.summary.totalErrors}.");
            }

            Debug.Log($"Dragon APK created at {Path.GetFullPath(OutputPath)} ({report.summary.totalSize} bytes).");
        }
    }
}
