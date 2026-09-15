using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DragonBound.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DragonBound.Editor.Versioning
{
    public static class DragonBoundUiVersionTools
    {
        private const string V1Root = UiVariantProjectPaths.V1Root;
        private const string V2Root = UiVariantProjectPaths.V2Root;
        private const string V1RegistryPath = V1Root + "/Config/UiAssetRegistryV1.asset";
        private const string V2RegistryPath = V2Root + "/Config/UiAssetRegistryV2.asset";
        private const string V1ProfilePath = V1Root + "/Config/V1BuildProfile.asset";
        private const string V2ProfilePath = V2Root + "/Config/V2BuildProfile.asset";
        private const string LegacyResourcesRoot = "Assets/Resources";
        private const string V1ResourcesRoot = V1Root + "/Content/Resources";
        private const string LegacyUiRoot = "Assets/DragonBound/UI";

        [MenuItem("DragonBound/Versioning/Prepare Version Infrastructure")]
        public static void PrepareInfrastructure()
        {
            EnsureFolder(V1Root + "/Config");
            EnsureFolder(V1Root + "/Scenes");
            EnsureFolder(V2Root + "/Config");
            EnsureFolder(V2Root + "/Content");
            EnsureFolder(V2Root + "/Scenes");

            var v1Registry = GenerateV1Registry();
            var v2Registry = LoadOrCreate<UiAssetRegistry>(V2RegistryPath);
            v2Registry.Configure("V2", new List<UiAssetRegistry.Entry>());
            EditorUtility.SetDirty(v2Registry);

            var login = LoadScene("Login");
            var main = LoadScene("Main");
            var gameplay = LoadScene("Greybox_Main");
            var currentScenes = new[] { login, main, gameplay }.Where(scene => scene != null).ToArray();

            var v1 = LoadOrCreate<DragonBoundUiBuildProfile>(V1ProfilePath);
            v1.Configure(
                "V1", V1Root, V2Root, currentScenes, v1Registry,
                "Drakeforge", "com.drakeforge.mergedefense", "0.1.0", 1,
                "Builds/Android/V1/Drakeforge-V1.apk", !AssetDatabase.IsValidFolder(LegacyResourcesRoot));
            EditorUtility.SetDirty(v1);

            var v2 = LoadOrCreate<DragonBoundUiBuildProfile>(V2ProfilePath);
            v2.Configure(
                "V2", V2Root, V1Root, Array.Empty<SceneAsset>(), v2Registry,
                "Drakeforge V2", "com.drakeforge.mergedefense.v2", "0.1.0", 1,
                "Builds/Android/V2/Drakeforge-V2.apk", true);
            EditorUtility.SetDirty(v2);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SetActiveProfile(v1);
            Debug.Log("DragonBound UI version infrastructure prepared. V2 intentionally remains incomplete until its independent assets and scenes are assigned.");
        }

        [MenuItem("DragonBound/Versioning/Regenerate V1 Asset Registry")]
        public static void RegenerateV1Registry()
        {
            GenerateV1Registry();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("DragonBound/Versioning/Migrate Current UI To V1 (Keep GUIDs)")]
        public static void MigrateCurrentUiToV1()
        {
            EnsureFolder(V1Root + "/Config");
            EnsureFolder(V1Root + "/Content");
            EnsureFolder(V1Root + "/Scenes");

            // Build the key-to-object map before removing the special Resources folder.
            var registry = GenerateV1Registry();
            MoveFolderIfPresent(LegacyResourcesRoot, V1ResourcesRoot);
            MoveFolderIfPresent(LegacyUiRoot + "/Art", V1Root + "/Content/UI/Art");
            MoveFolderIfPresent(LegacyUiRoot + "/Handoff", V1Root + "/Content/UI/Handoff");
            MoveFolderIfPresent(LegacyUiRoot + "/Prefabs", V1Root + "/Content/UI/Prefabs");

            foreach (var sceneName in new[] { "Game", "Greybox_Main", "HeroSlice_Main", "Login", "Main", "UI_Handoff" })
            {
                MoveAssetIfPresent(
                    $"Assets/Scenes/{sceneName}.unity",
                    $"{V1Root}/Scenes/{sceneName}.unity");
            }

            var productionScenes = new[] { LoadScene("Login"), LoadScene("Main"), LoadScene("Greybox_Main") }
                .Where(scene => scene != null)
                .ToArray();
            var profile = LoadOrCreate<DragonBoundUiBuildProfile>(V1ProfilePath);
            profile.Configure(
                "V1", V1Root, V2Root, productionScenes, registry,
                "Drakeforge", "com.drakeforge.mergedefense", "0.1.0", 1,
                "Builds/Android/V1/Drakeforge-V1.apk", true);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SetActiveProfile(profile);
            ValidateProfile(profile);
            Debug.Log("Current UI migrated to isolated V1 folders. Asset GUIDs and serialized references were preserved.");
        }

        [MenuItem("DragonBound/Versioning/Set Active V1")]
        public static void SetActiveV1()
        {
            SetActiveProfile(LoadRequiredProfile(V1ProfilePath));
        }

        [MenuItem("DragonBound/Versioning/Set Active V2")]
        public static void SetActiveV2()
        {
            SetActiveProfile(LoadRequiredProfile(V2ProfilePath));
        }

        [MenuItem("DragonBound/Versioning/Validate V1 Isolation")]
        public static void ValidateV1()
        {
            ValidateProfile(LoadRequiredProfile(V1ProfilePath));
        }

        [MenuItem("DragonBound/Versioning/Validate V2 Isolation")]
        public static void ValidateV2()
        {
            ValidateProfile(LoadRequiredProfile(V2ProfilePath));
        }

        [MenuItem("DragonBound/Build/Build Android V1")]
        public static void BuildV1()
        {
            Build(LoadRequiredProfile(V1ProfilePath));
        }

        [MenuItem("DragonBound/Build/Build Android V2")]
        public static void BuildV2()
        {
            Build(LoadRequiredProfile(V2ProfilePath));
        }

        [MenuItem("DragonBound/Build/Build Android Both")]
        public static void BuildBoth()
        {
            Build(LoadRequiredProfile(V1ProfilePath));
            Build(LoadRequiredProfile(V2ProfilePath));
        }

        public static void ValidateProfile(DragonBoundUiBuildProfile profile)
        {
            if (profile == null) throw new BuildFailedException("UI build profile is missing.");
            if (profile.AssetRegistry == null) throw new BuildFailedException($"{profile.VariantId} asset registry is missing.");
            if (profile.Scenes == null || profile.Scenes.Length == 0 || profile.Scenes.Any(scene => scene == null))
                throw new BuildFailedException($"{profile.VariantId} requires a complete scene set.");
            if (profile.AssetRegistry.Entries.Count == 0)
                throw new BuildFailedException($"{profile.VariantId} asset registry is empty.");

            var registryPath = AssetDatabase.GetAssetPath(profile.AssetRegistry);
            if (!IsBelow(registryPath, profile.AllowedUiRoot))
                throw new BuildFailedException($"{profile.VariantId} registry is outside its UI root: {registryPath}");

            var externalRegistryAssets = profile.AssetRegistry.Entries
                .SelectMany(entry => entry.Assets)
                .Where(asset => asset != null)
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !IsBelow(path, profile.AllowedUiRoot))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (externalRegistryAssets.Length > 0)
            {
                throw new BuildFailedException(
                    $"{profile.VariantId} registry references UI assets outside its isolated root:\n" +
                    string.Join("\n", externalRegistryAssets));
            }

            var externalScenes = profile.Scenes
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !IsBelow(path, profile.AllowedUiRoot))
                .ToArray();
            if (externalScenes.Length > 0)
            {
                throw new BuildFailedException(
                    $"{profile.VariantId} scenes are outside its isolated root:\n" +
                    string.Join("\n", externalScenes));
            }

            var roots = profile.Scenes.Select(AssetDatabase.GetAssetPath).ToList();
            roots.Add(AssetDatabase.GetAssetPath(profile.AssetRegistry));
            var dependencies = AssetDatabase.GetDependencies(roots.ToArray(), true)
                .Select(Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var forbidden = dependencies
                .Where(path => IsBelow(path, profile.ForbiddenUiRoot))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (forbidden.Length > 0)
            {
                throw new BuildFailedException(
                    $"{profile.VariantId} has {forbidden.Length} cross-version UI dependencies:\n" +
                    string.Join("\n", forbidden));
            }

            if (profile.EnforceNoLegacyResources)
            {
                var legacy = dependencies
                    .Where(path => IsBelow(path, LegacyResourcesRoot))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (legacy.Length > 0)
                {
                    throw new BuildFailedException(
                        $"{profile.VariantId} still depends on legacy Assets/Resources content:\n" +
                        string.Join("\n", legacy));
                }
            }

            Debug.Log($"{profile.VariantId} UI isolation validated. Dependencies={dependencies.Length}.");
        }

        private static void Build(DragonBoundUiBuildProfile profile)
        {
            ValidateProfile(profile);
            var previousProductName = PlayerSettings.productName;
            var previousIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            var previousVersion = PlayerSettings.bundleVersion;
            var previousVersionCode = PlayerSettings.Android.bundleVersionCode;
            var previousPreloaded = PlayerSettings.GetPreloadedAssets();
            var previousSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
            var previousExportProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            var previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            try
            {
                PlayerSettings.productName = profile.ProductName;
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, profile.ApplicationIdentifier);
                PlayerSettings.bundleVersion = profile.BundleVersion;
                PlayerSettings.Android.bundleVersionCode = profile.AndroidVersionCode;
                PlayerSettings.SetPreloadedAssets(WithSelectedRegistry(previousPreloaded, profile.AssetRegistry));
                PlayerSettings.SetScriptingDefineSymbolsForGroup(
                    BuildTargetGroup.Android,
                    WithVariantSymbol(previousSymbols, profile.DefineSymbol));
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                EditorUserBuildSettings.buildAppBundle = false;

                var outputDirectory = Path.GetDirectoryName(profile.OutputPath);
                if (!string.IsNullOrWhiteSpace(outputDirectory)) Directory.CreateDirectory(outputDirectory);
                if (Directory.Exists(profile.OutputPath)) Directory.Delete(profile.OutputPath, true);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = profile.Scenes.Select(AssetDatabase.GetAssetPath).ToArray(),
                    locationPathName = profile.OutputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded || !File.Exists(profile.OutputPath))
                    throw new BuildFailedException($"{profile.VariantId} build failed: {report.summary.result}.");

                Debug.Log($"{profile.VariantId} APK created: {Path.GetFullPath(profile.OutputPath)}");
            }
            finally
            {
                PlayerSettings.productName = previousProductName;
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, previousIdentifier);
                PlayerSettings.bundleVersion = previousVersion;
                PlayerSettings.Android.bundleVersionCode = previousVersionCode;
                PlayerSettings.SetPreloadedAssets(previousPreloaded);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, previousSymbols);
                EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExportProject;
                EditorUserBuildSettings.buildAppBundle = previousBuildAppBundle;
                AssetDatabase.SaveAssets();
            }
        }

        private static void SetActiveProfile(DragonBoundUiBuildProfile profile)
        {
            ValidateBasicProfile(profile);
            PlayerSettings.SetPreloadedAssets(
                WithSelectedRegistry(PlayerSettings.GetPreloadedAssets(), profile.AssetRegistry));
            EditorBuildSettings.scenes = profile.Scenes
                .Where(scene => scene != null)
                .Select(scene => new EditorBuildSettingsScene(AssetDatabase.GetAssetPath(scene), true))
                .ToArray();
            EditorPrefs.SetString("DragonBound.ActiveUiVariant", profile.VariantId);
            UiAssets.Activate(profile.AssetRegistry);
            Debug.Log($"Active DragonBound UI variant: {profile.VariantId}");
        }

        private static void ValidateBasicProfile(DragonBoundUiBuildProfile profile)
        {
            if (profile == null || profile.AssetRegistry == null)
                throw new InvalidOperationException("UI build profile or registry is missing.");
        }

        private static UnityEngine.Object[] WithSelectedRegistry(
            IEnumerable<UnityEngine.Object> existing,
            UiAssetRegistry selected)
        {
            return existing
                .Where(asset => asset != null && !(asset is UiAssetRegistry))
                .Concat(new UnityEngine.Object[] { selected })
                .ToArray();
        }

        private static string WithVariantSymbol(string existing, string selected)
        {
            var symbols = existing.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(symbol => !symbol.StartsWith("DRAGONBOUND_UI_", StringComparison.Ordinal))
                .Concat(new[] { selected });
            return string.Join(";", symbols.Distinct(StringComparer.Ordinal));
        }

        private static UiAssetRegistry GenerateRegistry(string variant, string sourceRoot, string outputPath)
        {
            if (!AssetDatabase.IsValidFolder(sourceRoot))
                throw new InvalidOperationException($"Resource source folder does not exist: {sourceRoot}");
            EnsureFolder(Path.GetDirectoryName(outputPath)?.Replace('\\', '/'));
            var registry = LoadOrCreate<UiAssetRegistry>(outputPath);
            var assetsByKey = new Dictionary<string, List<UnityEngine.Object>>(StringComparer.Ordinal);
            var guids = AssetDatabase.FindAssets(string.Empty, new[] { sourceRoot });
            foreach (var guid in guids)
            {
                var path = Normalize(AssetDatabase.GUIDToAssetPath(guid));
                if (AssetDatabase.IsValidFolder(path)) continue;
                var key = ToResourceKey(sourceRoot, path);
                var objects = AssetDatabase.LoadAllAssetsAtPath(path)
                    .Where(asset => asset != null && !(asset is MonoScript))
                    .ToArray();
                if (objects.Length == 0) continue;

                if (!assetsByKey.TryGetValue(key, out var combined))
                {
                    combined = new List<UnityEngine.Object>();
                    assetsByKey.Add(key, combined);
                }

                foreach (var asset in objects)
                {
                    if (!combined.Contains(asset)) combined.Add(asset);
                }
            }

            var entries = new List<UiAssetRegistry.Entry>();
            foreach (var pair in assetsByKey)
            {
                var entry = new UiAssetRegistry.Entry();
                entry.Configure(pair.Key, pair.Value.ToArray());
                entries.Add(entry);
            }

            entries.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
            registry.Configure(variant, entries);
            EditorUtility.SetDirty(registry);
            Debug.Log($"Generated {variant} UI registry with {entries.Count} keys at {outputPath}.");
            return registry;
        }

        private static UiAssetRegistry GenerateV1Registry()
        {
            var sourceRoot = AssetDatabase.IsValidFolder(LegacyResourcesRoot)
                ? LegacyResourcesRoot
                : V1ResourcesRoot;
            return GenerateRegistry("V1", sourceRoot, V1RegistryPath);
        }

        private static SceneAsset LoadScene(string sceneName)
        {
            return AssetDatabase.LoadAssetAtPath<SceneAsset>($"{V1Root}/Scenes/{sceneName}.unity") ??
                   AssetDatabase.LoadAssetAtPath<SceneAsset>($"Assets/Scenes/{sceneName}.unity");
        }

        private static void MoveFolderIfPresent(string source, string destination)
        {
            if (!AssetDatabase.IsValidFolder(source)) return;
            var parent = Normalize(Path.GetDirectoryName(destination));
            EnsureFolder(parent);
            if (AssetDatabase.IsValidFolder(destination))
                throw new InvalidOperationException($"Cannot migrate UI because destination already exists: {destination}");
            MoveAsset(source, destination);
        }

        private static void MoveAssetIfPresent(string source, string destination)
        {
            if (AssetDatabase.LoadMainAssetAtPath(source) == null) return;
            EnsureFolder(Normalize(Path.GetDirectoryName(destination)));
            if (AssetDatabase.LoadMainAssetAtPath(destination) != null)
                throw new InvalidOperationException($"Cannot migrate UI because destination already exists: {destination}");
            MoveAsset(source, destination);
        }

        private static void MoveAsset(string source, string destination)
        {
            var error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrWhiteSpace(error))
                throw new InvalidOperationException($"Failed to move {source} to {destination}: {error}");
        }

        private static string ToResourceKey(string root, string assetPath)
        {
            var relative = assetPath.Substring(Normalize(root).Length).TrimStart('/');
            var extension = Path.GetExtension(relative);
            return string.IsNullOrEmpty(extension)
                ? relative
                : relative.Substring(0, relative.Length - extension.Length);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static DragonBoundUiBuildProfile LoadRequiredProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<DragonBoundUiBuildProfile>(path);
            if (profile == null)
                throw new InvalidOperationException($"Missing {path}. Run Prepare Version Infrastructure first.");
            return profile;
        }

        private static void EnsureFolder(string path)
        {
            path = Normalize(path);
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path)) return;
            var parent = Normalize(Path.GetDirectoryName(path));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static bool IsBelow(string path, string root)
        {
            path = Normalize(path);
            root = Normalize(root);
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }
    }
}
