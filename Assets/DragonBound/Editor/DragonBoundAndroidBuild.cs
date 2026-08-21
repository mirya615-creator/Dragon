using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DragonBound.Editor
{
    public static class DragonBoundAndroidBuild
    {
        public const string OutputPath = "Builds/Android/DragonBound-Greybox.apk";

        public static void BuildApk()
        {
            var outputDirectory = Path.GetDirectoryName(OutputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException("APK output directory is invalid.");
            }

            Directory.CreateDirectory(outputDirectory);
            PlayerSettings.companyName = "DragonBound";
            PlayerSettings.productName = "Drakeforge";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.drakeforge.mergedefense");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false;

            var scenes = new[] { "Assets/DragonBound/Scenes/Greybox_Main.unity" };
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

            Debug.Log($"DragonBound APK created at {Path.GetFullPath(OutputPath)} ({report.summary.totalSize} bytes).");
        }
    }
}
