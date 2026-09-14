using System;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using DragonBound.Analytics;

namespace DragonBound.Editor
{
    public static class DragonBoundAndroidBuild
    {
        public const string OutputPath = "Builds/Android/DragonBound-Greybox.apk";
        public const string ApprovedFirebaseProjectId = "drakeforge";

        [Serializable]
        private sealed class GoogleServicesRoot
        {
            public GoogleProjectInfo project_info;
            public GoogleClient[] client;
        }

        [Serializable]
        private sealed class GoogleProjectInfo
        {
            public string project_id;
        }

        [Serializable]
        private sealed class GoogleClient
        {
            public GoogleClientInfo client_info;
        }

        [Serializable]
        private sealed class GoogleClientInfo
        {
            public GoogleAndroidClientInfo android_client_info;
        }

        [Serializable]
        private sealed class GoogleAndroidClientInfo
        {
            public string package_name;
        }

        [MenuItem("Tools/DragonBound/Analytics/Validate Android Release Readiness")]
        public static void ValidateAnalyticsReleaseReadiness()
        {
            const string googleServicesPath = "Assets/google-services.json";
            const string desktopServicesPath = "Assets/StreamingAssets/google-services-desktop.json";
            const string analyticsBootstrapPath = "Assets/Scripts/Analytics/FirebaseAnalyticsBootstrap.cs";
            const string analyticsConsentUiPath = "Assets/Scripts/Analytics/AnalyticsConsentUiController.cs";
            const string analyticsManifestPath =
                "Assets/Plugins/Android/DragonBoundAnalytics.androidlib/AndroidManifest.xml";
            var googleServicesPresent = File.Exists(googleServicesPath);
            ReadGoogleServices(googleServicesPath, out var firebaseProjectId, out var firebasePackageName);
            var desktopServicesPresent = File.Exists(desktopServicesPath);
            ReadGoogleServices(desktopServicesPath, out var desktopProjectId, out _);
            var startupScenePath = ResolveFirstEnabledScene();
            var bootstrapGuid = AssetDatabase.AssetPathToGUID(analyticsBootstrapPath);
            var bootstrapInStartupScene = !string.IsNullOrWhiteSpace(startupScenePath) &&
                                          File.Exists(startupScenePath) &&
                                          !string.IsNullOrWhiteSpace(bootstrapGuid) &&
                                          File.ReadAllText(startupScenePath).IndexOf(
                                              "guid: " + bootstrapGuid,
                                              StringComparison.Ordinal) >= 0;
            var bootstrapIsDedicatedRoot = SceneHasDedicatedRootBehaviour(
                startupScenePath,
                bootstrapGuid,
                "AnalyticsBootstrap");

            var architectures = PlayerSettings.Android.targetArchitectures;
            var configuration = new AnalyticsReleaseConfigurationV2
            {
                AndroidPackageName = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android),
                AndroidMinimumApiLevel = (int)PlayerSettings.Android.minSdkVersion,
                UsesIl2Cpp = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) ==
                              ScriptingImplementation.IL2CPP,
                IncludesArm64 = (architectures & AndroidArchitecture.ARM64) != 0,
                IncludesArmV7 = (architectures & AndroidArchitecture.ARMv7) != 0,
                BundleVersion = PlayerSettings.bundleVersion,
                BundleVersionCode = PlayerSettings.Android.bundleVersionCode,
                GoogleServicesFilePresent = googleServicesPresent,
                FirebaseAndroidPackageName = firebasePackageName,
                FirebaseProjectId = firebaseProjectId,
                FirebaseDesktopConfigPresent = desktopServicesPresent,
                FirebaseDesktopProjectId = desktopProjectId,
                ExpectedFirebaseProjectId = ApprovedFirebaseProjectId,
                FirebaseSdkVersion = File.Exists(
                    "Assets/Firebase/Editor/FirebaseAnalytics_version-13.14.0_manifest.txt")
                    ? AnalyticsReleaseReadinessValidatorV2.RequiredFirebaseSdkVersion
                    : string.Empty,
                StartupScenePath = startupScenePath,
                AnalyticsBootstrapInStartupScene = bootstrapInStartupScene,
                AnalyticsBootstrapIsDedicatedRoot = bootstrapIsDedicatedRoot,
                StartupEventSystemEnabled = SceneHasEnabledGameObject(startupScenePath, "EventSystem"),
                NativeCollectionDisabledByDefault = ManifestDisablesAnalyticsCollection(analyticsManifestPath),
                ConsentUiAvailable = File.Exists(analyticsConsentUiPath),
                BuildLane = ResolveReleaseLane(),
                ExplicitConsentRequired = true
            };
            var report = AnalyticsReleaseReadinessValidatorV2.Validate(configuration);
            for (var index = 0; index < report.Issues.Count; index++)
            {
                var issue = report.Issues[index];
                var message = "[Analytics Release] " + issue.Code + ": " + issue.Message;
                if (issue.Severity == AnalyticsReleaseIssueSeverityV2.Error) Debug.LogError(message);
                else Debug.LogWarning(message);
            }

            if (report.IsReady)
            {
                Debug.Log("[Analytics Release] READY. FirebaseProject=" + firebaseProjectId +
                          " Package=" + firebasePackageName + " Lane=" + configuration.BuildLane +
                          " StartupScene=" + startupScenePath);
            }
            else
            {
                Debug.LogError("[Analytics Release] BLOCKED. Resolve all errors before an Android release build.");
            }
        }

        private static void ReadGoogleServices(string path, out string projectId, out string packageName)
        {
            projectId = string.Empty;
            packageName = string.Empty;
            if (!File.Exists(path)) return;

            try
            {
                var root = JsonUtility.FromJson<GoogleServicesRoot>(File.ReadAllText(path));
                projectId = root?.project_info?.project_id ?? string.Empty;
                if (root?.client != null && root.client.Length > 0)
                {
                    packageName = root.client[0]?.client_info?.android_client_info?.package_name ?? string.Empty;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("Analytics release validation could not parse " + path + ": " +
                               exception.Message);
            }
        }

        private static string ResolveFirstEnabledScene()
        {
            var scenes = EditorBuildSettings.scenes;
            for (var index = 0; index < scenes.Length; index++)
            {
                if (scenes[index].enabled) return scenes[index].path;
            }

            return string.Empty;
        }

        private static bool ManifestDisablesAnalyticsCollection(string path)
        {
            if (!File.Exists(path)) return false;
            try
            {
                var document = new XmlDocument();
                document.Load(path);
                var namespaces = new XmlNamespaceManager(document.NameTable);
                namespaces.AddNamespace("android", "http://schemas.android.com/apk/res/android");
                var entry = document.SelectSingleNode(
                    "/manifest/application/meta-data[@android:name='firebase_analytics_collection_enabled']",
                    namespaces);
                var value = entry?.Attributes?["value", "http://schemas.android.com/apk/res/android"]?.Value;
                return string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
            }
            catch (XmlException)
            {
                return false;
            }
        }

        private static bool SceneHasEnabledGameObject(string path, string objectName)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            var inGameObject = false;
            var nameMatches = false;
            var isActive = false;
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (line.StartsWith("--- !u!", StringComparison.Ordinal))
                {
                    if (inGameObject && nameMatches && isActive) return true;
                    inGameObject = line.StartsWith("--- !u!1 ", StringComparison.Ordinal);
                    nameMatches = false;
                    isActive = false;
                    continue;
                }

                if (!inGameObject) continue;
                var trimmed = line.Trim();
                if (trimmed.StartsWith("m_Name:", StringComparison.Ordinal))
                {
                    nameMatches = string.Equals(
                        trimmed.Substring("m_Name:".Length).Trim(),
                        objectName,
                        StringComparison.Ordinal);
                }
                else if (trimmed.StartsWith("m_IsActive:", StringComparison.Ordinal))
                {
                    isActive = string.Equals(
                        trimmed.Substring("m_IsActive:".Length).Trim(),
                        "1",
                        StringComparison.Ordinal);
                }
            }

            return inGameObject && nameMatches && isActive;
        }

        private static bool SceneHasDedicatedRootBehaviour(
            string path,
            string behaviourGuid,
            string requiredObjectName)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) ||
                string.IsNullOrWhiteSpace(behaviourGuid)) return false;

            var lines = File.ReadAllLines(path);
            var behaviourObjectId = string.Empty;
            var behaviourMatches = 0;
            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].StartsWith("--- !u!114 ", StringComparison.Ordinal)) continue;
                var end = FindBlockEnd(lines, index + 1);
                var matches = false;
                var objectId = string.Empty;
                for (var lineIndex = index + 1; lineIndex < end; lineIndex++)
                {
                    var trimmed = lines[lineIndex].Trim();
                    if (trimmed.IndexOf("guid: " + behaviourGuid, StringComparison.Ordinal) >= 0)
                        matches = true;
                    if (trimmed.StartsWith("m_GameObject: {fileID:", StringComparison.Ordinal))
                        objectId = ParseFileId(trimmed);
                }
                if (!matches) continue;
                behaviourMatches++;
                behaviourObjectId = objectId;
            }
            if (behaviourMatches != 1 || string.IsNullOrWhiteSpace(behaviourObjectId)) return false;

            var hostIsDedicated = false;
            var transformId = string.Empty;
            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].StartsWith("--- !u!1 &" + behaviourObjectId, StringComparison.Ordinal))
                    continue;
                var end = FindBlockEnd(lines, index + 1);
                var componentCount = 0;
                var nameMatches = false;
                var active = false;
                for (var lineIndex = index + 1; lineIndex < end; lineIndex++)
                {
                    var trimmed = lines[lineIndex].Trim();
                    if (trimmed.StartsWith("- component: {fileID:", StringComparison.Ordinal))
                    {
                        componentCount++;
                        if (componentCount == 1) transformId = ParseFileId(trimmed);
                    }
                    else if (trimmed.StartsWith("m_Name:", StringComparison.Ordinal))
                        nameMatches = string.Equals(
                            trimmed.Substring("m_Name:".Length).Trim(),
                            requiredObjectName,
                            StringComparison.Ordinal);
                    else if (trimmed == "m_IsActive: 1") active = true;
                }
                hostIsDedicated = componentCount == 2 && nameMatches && active;
                break;
            }
            if (!hostIsDedicated || string.IsNullOrWhiteSpace(transformId)) return false;

            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].StartsWith("--- !u!4 &" + transformId, StringComparison.Ordinal)) continue;
                var end = FindBlockEnd(lines, index + 1);
                for (var lineIndex = index + 1; lineIndex < end; lineIndex++)
                {
                    if (lines[lineIndex].Trim() == "m_Father: {fileID: 0}") return true;
                }
                return false;
            }
            return false;
        }

        private static int FindBlockEnd(string[] lines, int start)
        {
            var index = start;
            while (index < lines.Length &&
                   !lines[index].StartsWith("--- !u!", StringComparison.Ordinal)) index++;
            return index;
        }

        private static string ParseFileId(string line)
        {
            var marker = line.IndexOf("fileID:", StringComparison.Ordinal);
            if (marker < 0) return string.Empty;
            marker += "fileID:".Length;
            var end = line.IndexOf('}', marker);
            return (end < 0 ? line.Substring(marker) : line.Substring(marker, end - marker)).Trim();
        }

        private static string ResolveReleaseLane()
        {
            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
            return symbols.IndexOf("DRAGONBOUND_STAGING", StringComparison.Ordinal) >= 0
                ? AnalyticsBuildLanes.Staging
                : EditorUserBuildSettings.development
                    ? AnalyticsBuildLanes.Qa
                    : AnalyticsBuildLanes.Production;
        }

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

            var scenes = new[]
            {
                "Assets/Scenes/Login.unity",
                "Assets/Scenes/Main.unity",
                "Assets/Scenes/Greybox_Main.unity"
            };
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
