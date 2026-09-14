using System;
using DragonBound.Presentation;
using UnityEditor;
using UnityEngine;

namespace DragonBound.Editor.Versioning
{
    [CreateAssetMenu(
        fileName = "UiBuildProfile",
        menuName = "DragonBound/UI/Build Profile")]
    public sealed class DragonBoundUiBuildProfile : ScriptableObject
    {
        [SerializeField] private string variantId;
        [SerializeField] private string allowedUiRoot;
        [SerializeField] private string forbiddenUiRoot;
        [SerializeField] private SceneAsset[] scenes = Array.Empty<SceneAsset>();
        [SerializeField] private UiAssetRegistry assetRegistry;
        [SerializeField] private string productName;
        [SerializeField] private string applicationIdentifier;
        [SerializeField] private string bundleVersion = "0.1.0";
        [SerializeField] private int androidVersionCode = 1;
        [SerializeField] private string outputPath;
        [SerializeField] private bool enforceNoLegacyResources = true;

        public string VariantId => variantId;
        public string AllowedUiRoot => Normalize(allowedUiRoot);
        public string ForbiddenUiRoot => Normalize(forbiddenUiRoot);
        public SceneAsset[] Scenes => scenes;
        public UiAssetRegistry AssetRegistry => assetRegistry;
        public string ProductName => productName;
        public string ApplicationIdentifier => applicationIdentifier;
        public string BundleVersion => bundleVersion;
        public int AndroidVersionCode => androidVersionCode;
        public string OutputPath => outputPath;
        public bool EnforceNoLegacyResources => enforceNoLegacyResources;
        public string DefineSymbol => "DRAGONBOUND_UI_" + variantId.ToUpperInvariant();

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

#if UNITY_EDITOR
        public void Configure(
            string id,
            string allowedRoot,
            string forbiddenRoot,
            SceneAsset[] buildScenes,
            UiAssetRegistry registry,
            string playerProductName,
            string packageName,
            string version,
            int versionCode,
            string buildOutput,
            bool rejectLegacyResources)
        {
            variantId = id;
            allowedUiRoot = allowedRoot;
            forbiddenUiRoot = forbiddenRoot;
            scenes = buildScenes ?? Array.Empty<SceneAsset>();
            assetRegistry = registry;
            productName = playerProductName;
            applicationIdentifier = packageName;
            bundleVersion = version;
            androidVersionCode = versionCode;
            outputPath = buildOutput;
            enforceNoLegacyResources = rejectLegacyResources;
        }
#endif
    }
}
