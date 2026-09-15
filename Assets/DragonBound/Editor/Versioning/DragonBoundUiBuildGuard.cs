using System;
using System.Linq;
using DragonBound.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace DragonBound.Editor.Versioning
{
    /// <summary>
    /// Prevents the standard Unity Build button and older build scripts from bypassing
    /// the V1/V2 dependency-isolation checks.
    /// </summary>
    public sealed class DragonBoundUiBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var registry = PlayerSettings.GetPreloadedAssets()
                .OfType<UiAssetRegistry>()
                .SingleOrDefault();
            if (registry == null)
            {
                throw new BuildFailedException(
                    "No UI version is selected. Use DragonBound > Versioning > Set Active V1/V2 before building.");
            }

            var profiles = AssetDatabase.FindAssets("t:DragonBoundUiBuildProfile")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<DragonBoundUiBuildProfile>)
                .Where(profile => profile != null && profile.AssetRegistry == registry)
                .ToArray();
            if (profiles.Length != 1)
            {
                throw new BuildFailedException(
                    $"Expected exactly one build profile for active registry {registry.VariantId}, found {profiles.Length}.");
            }

            DragonBoundUiVersionTools.ValidateProfile(profiles[0]);
        }
    }
}
