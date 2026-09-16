using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class GoogleLoginConfigurationValidation : IPreprocessBuildWithReport
{
    private const string ConfigPath = "Configuration/ClientServiceConfig";
    private const string ExpectedPackage = "com.drakeforge.mergedefense";
    private const string ClientIdSuffix = ".apps.googleusercontent.com";

    public int callbackOrder => -100;

    [MenuItem("DragonBound/Validation/Validate Android Google Login")]
    public static void ValidateFromMenu()
    {
        Validate(true);
        Debug.Log("Android Google Login configuration validation passed.");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.Android) Validate(false);
    }

    private static void Validate(bool allowNonAndroidEditor)
    {
        ClientServiceConfig config = Resources.Load<ClientServiceConfig>(ConfigPath);
        Require(config != null, "ClientServiceConfig is missing.");
        Require(
            !string.IsNullOrWhiteSpace(config.GoogleWebClientId) &&
            config.GoogleWebClientId.EndsWith(ClientIdSuffix, StringComparison.Ordinal),
            "Google Web Client ID is missing or invalid.");
        Require(
            !string.IsNullOrWhiteSpace(config.GoogleAndroidClientId) &&
            config.GoogleAndroidClientId.EndsWith(ClientIdSuffix, StringComparison.Ordinal),
            "Google Android Client ID is missing or invalid.");
        Require(
            !string.Equals(
                config.GoogleWebClientId,
                config.GoogleAndroidClientId,
                StringComparison.Ordinal),
            "Credential Manager serverClientId must be the Web Client ID, not the Android Client ID.");
        Require(
            PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android) == ExpectedPackage,
            "Android package name must be " + ExpectedPackage + ".");
        Require(
            File.Exists(
                "Assets/Plugins/Android/DragonBoundGoogleIdentity.androidlib/build.gradle"),
            "Credential Manager Android library configuration is missing.");
        Require(
            File.Exists(
                "Assets/Plugins/Android/DragonBoundGoogleIdentity.androidlib/src/main/java/" +
                "com/drakeforge/mergedefense/identity/GoogleCredentialBridge.java"),
            "Credential Manager Android bridge is missing.");
        if (!allowNonAndroidEditor)
        {
            Require(
                PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) ==
                ScriptingImplementation.IL2CPP,
                "Android production builds must use IL2CPP.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new BuildFailedException(message);
    }
}
