using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DragonBound.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Editor.Versioning
{
    public static class DragonBoundUiVersionTools
    {
        private const string V1Root = UiVariantProjectPaths.V1Root;
        private const string V2Root = UiVariantProjectPaths.V2Root;
        private const string V1RegistryPath = V1Root + "/Config/UiAssetRegistryV1.asset";
        private const string V2RegistryPath = V2Root + "/Config/UiAssetRegistryV2.asset";
        private const string V2AliasesPath = V2Root + "/Config/UiAssetRegistryV2Aliases.asset";
        private const string V1ProfilePath = V1Root + "/Config/V1BuildProfile.asset";
        private const string V2ProfilePath = V2Root + "/Config/V2BuildProfile.asset";
        private const string LegacyResourcesRoot = "Assets/Resources";
        private const string V1ResourcePrefabsRoot = V1Root + "/Content/Resources/prefabs";
        private const string V1ResourcesRoot = V1Root + "/Content/Resources";
        private const string V2ResourcesRoot = V2Root + "/Content/Resources";
        private const string V1ComponentPrefabsRoot = V1Root + "/Content/UI/Prefabs/Components";
        private const string V2ComponentPrefabsRoot = V2Root + "/Content/UI/Prefabs/Components";
        private const string SharedResourcesRoot = "Assets/DragonBound/Shared/Resources";
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
            // The generated registry is intentionally not cleared here. Its V2 logical
            // aliases are preserved in a separate, version-owned catalog.
            var v2Registry = LoadOrCreate<UiAssetRegistry>(V2RegistryPath);
            var v2Aliases = LoadOrCreate<DragonBoundUiAssetAliasCatalog>(V2AliasesPath);
            if (!string.Equals(v2Aliases.VariantId, "V2", StringComparison.Ordinal))
            {
                v2Aliases.Configure("V2", v2Aliases.Entries.ToList());
                EditorUtility.SetDirty(v2Aliases);
            }

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

        [MenuItem("DragonBound/Versioning/Regenerate V2 Asset Registry (Preserve Aliases)")]
        public static void RegenerateV2Registry()
        {
            GenerateV2Registry();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("DragonBound/Versioning/Migrate Runtime Configuration To Shared Resources")]
        public static void MigrateRuntimeConfigurationToSharedResources()
        {
            EnsureFolder("Assets/DragonBound/Shared");
            EnsureFolder(SharedResourcesRoot);
            MoveFolderIfPresent(
                V1ResourcesRoot + "/Configuration",
                SharedResourcesRoot + "/Configuration");
            GenerateV1Registry();
            GenerateV2Registry();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Moved runtime Configuration to shared Resources and regenerated both UI registries.");
        }

        [MenuItem("DragonBound/Versioning/Create V2 Initial Scene And Prefab Copy")]
        public static void CreateV2InitialSceneAndPrefabCopy()
        {
            EnsureFolder(V2Root + "/Config");
            EnsureFolder(V2Root + "/Content");
            EnsureFolder(V2Root + "/Scenes");

            var guidMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var copiedPaths = new List<string>();
            foreach (var sceneName in new[] { "Login", "Main", "Greybox_Main" })
            {
                CopyAssetWithIndependentGuid(
                    UiVariantProjectPaths.V1Scene(sceneName),
                    $"{V2Root}/Scenes/{sceneName}.unity",
                    guidMap,
                    copiedPaths);
            }

            CopyPrefabTree(V1ResourcePrefabsRoot, V2ResourcesRoot + "/prefabs", guidMap, copiedPaths);
            CopyPrefabTree(V1ComponentPrefabsRoot, V2ComponentPrefabsRoot, guidMap, copiedPaths);
            RemapCopiedAssetReferences(copiedPaths, guidMap);

            var registry = GenerateV2Registry();
            var scenes = new[]
            {
                AssetDatabase.LoadAssetAtPath<SceneAsset>($"{V2Root}/Scenes/Login.unity"),
                AssetDatabase.LoadAssetAtPath<SceneAsset>($"{V2Root}/Scenes/Main.unity"),
                AssetDatabase.LoadAssetAtPath<SceneAsset>($"{V2Root}/Scenes/Greybox_Main.unity")
            };
            var profile = LoadOrCreate<DragonBoundUiBuildProfile>(V2ProfilePath);
            profile.Configure(
                "V2", V2Root, V1Root, scenes, registry,
                "Drakeforge V2", "com.drakeforge.mergedefense.v2", "0.1.0", 1,
                "Builds/Android/V2/Drakeforge-V2.apk", true);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Created V2 initial copy: {scenes.Length} scenes, " +
                $"{copiedPaths.Count - scenes.Length} prefabs, {guidMap.Count} independent GUID mappings.");
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

        [MenuItem("DragonBound/Versioning/Configure V2 Main Navigation Selection")]
        public static void ConfigureV2MainNavigationSelection()
        {
            const string scenePath = V2Root + "/Scenes/Main.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var navigation = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .SingleOrDefault(item => item.name == "ButtonNavigation");
            if (navigation == null)
                throw new InvalidOperationException($"ButtonNavigation was not found in {scenePath}.");

            var ranking = FindDirectChildButton(navigation, "RankingBtn", scenePath);
            var main = FindDirectChildButton(navigation, "MainBtn", scenePath);
            var bag = FindDirectChildButton(navigation, "BagBtn", scenePath);
            var selection = navigation.GetComponent<BottomNavigationSelection>() ??
                            navigation.gameObject.AddComponent<BottomNavigationSelection>();
            selection.Configure(
                ranking, RequireImage(ranking, scenePath),
                main, RequireImage(main, scenePath),
                bag, RequireImage(bag, scenePath),
                BottomNavigationSelection.NavigationItem.Main);

            EditorUtility.SetDirty(selection);
            EditorUtility.SetDirty(ranking);
            EditorUtility.SetDirty(main);
            EditorUtility.SetDirty(bag);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Configured V2 Main ButtonNavigation selection colors. Initial selection=Main.");
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

            var currentPreloadedAssets = PlayerSettings.GetPreloadedAssets();
            var desiredPreloadedAssets = WithSelectedRegistry(
                currentPreloadedAssets,
                profile.AssetRegistry);
            if (!AreSameAssets(currentPreloadedAssets, desiredPreloadedAssets))
            {
                PlayerSettings.SetPreloadedAssets(desiredPreloadedAssets);
            }

            var desiredScenes = profile.Scenes
                .Where(scene => scene != null)
                .Select(scene => new EditorBuildSettingsScene(AssetDatabase.GetAssetPath(scene), true))
                .ToArray();
            if (!AreSameScenes(EditorBuildSettings.scenes, desiredScenes))
            {
                EditorBuildSettings.scenes = desiredScenes;
            }

            const string activeVariantKey = "DragonBound.ActiveUiVariant";
            if (EditorPrefs.GetString(activeVariantKey) != profile.VariantId)
            {
                EditorPrefs.SetString(activeVariantKey, profile.VariantId);
            }

            if (UiAssets.Active != profile.AssetRegistry)
            {
                // Static runtime state is cleared by Unity domain reloads. Restore it from the
                // already-persisted profile without rewriting PlayerSettings or build settings.
                UiAssets.Activate(profile.AssetRegistry);
                Debug.Log($"Active DragonBound UI variant: {profile.VariantId}");
            }
        }

        private static bool AreSameAssets(
            IReadOnlyList<UnityEngine.Object> current,
            IReadOnlyList<UnityEngine.Object> desired)
        {
            if (current.Count != desired.Count) return false;
            for (var index = 0; index < current.Count; index++)
            {
                if (current[index] != desired[index]) return false;
            }

            return true;
        }

        private static bool AreSameScenes(
            IReadOnlyList<EditorBuildSettingsScene> current,
            IReadOnlyList<EditorBuildSettingsScene> desired)
        {
            if (current.Count != desired.Count) return false;
            for (var index = 0; index < current.Count; index++)
            {
                if (current[index].enabled != desired[index].enabled ||
                    !string.Equals(current[index].path, desired[index].path, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
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

        private static UiAssetRegistry GenerateRegistry(
            string variant,
            string sourceRoot,
            string outputPath,
            DragonBoundUiAssetAliasCatalog aliases = null)
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
                var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
                var objects = new[] { mainAsset }
                    .Concat(AssetDatabase.LoadAllAssetsAtPath(path))
                    .Where(asset => asset != null && !(asset is MonoScript))
                    .Distinct()
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

            PreserveExistingAliases(registry, assetsByKey, aliases);
            MergeAliases(assetsByKey, aliases, variant);

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

        private static UiAssetRegistry GenerateV2Registry()
        {
            var aliases = LoadOrCreate<DragonBoundUiAssetAliasCatalog>(V2AliasesPath);
            if (!string.Equals(aliases.VariantId, "V2", StringComparison.Ordinal))
            {
                aliases.Configure("V2", aliases.Entries.ToList());
                EditorUtility.SetDirty(aliases);
            }

            return GenerateRegistry("V2", V2ResourcesRoot, V2RegistryPath, aliases);
        }

        private static void PreserveExistingAliases(
            UiAssetRegistry registry,
            IReadOnlyDictionary<string, List<UnityEngine.Object>> generatedAssets,
            DragonBoundUiAssetAliasCatalog aliases)
        {
            if (registry == null || aliases == null)
            {
                return;
            }

            var preserved = new List<UiAssetRegistry.Entry>();
            var catalogChanged = false;
            foreach (var entry in aliases.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                {
                    catalogChanged = true;
                    continue;
                }

                if (!entry.Assets.Any(asset => asset != null))
                {
                    Debug.LogWarning(
                        $"Discarded stale V2 UI alias with no remaining assets: {entry.Key}");
                    catalogChanged = true;
                    continue;
                }

                preserved.Add(entry);
            }

            var knownKeys = new HashSet<string>(
                preserved.Select(entry => entry.Key),
                StringComparer.Ordinal);

            foreach (var entry in registry.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key) ||
                    generatedAssets.ContainsKey(entry.Key) ||
                    knownKeys.Contains(entry.Key))
                {
                    continue;
                }

                if (!entry.Assets.Any(asset => asset != null))
                {
                    Debug.LogWarning(
                        $"Skipped stale V2 UI registry entry with no remaining assets: {entry.Key}");
                    continue;
                }

                var copied = new UiAssetRegistry.Entry();
                copied.Configure(entry.Key, entry.Assets.ToArray());
                preserved.Add(copied);
                knownKeys.Add(entry.Key);
                catalogChanged = true;
            }

            if (catalogChanged)
            {
                aliases.Configure("V2", preserved);
                EditorUtility.SetDirty(aliases);
            }
        }

        private static void MergeAliases(
            IDictionary<string, List<UnityEngine.Object>> generatedAssets,
            DragonBoundUiAssetAliasCatalog aliases,
            string variant)
        {
            if (aliases == null)
            {
                return;
            }

            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var alias in aliases.Entries)
            {
                if (alias == null || string.IsNullOrWhiteSpace(alias.Key))
                {
                    throw new InvalidOperationException($"{variant} UI alias has an empty key.");
                }

                if (!seenKeys.Add(alias.Key))
                {
                    throw new InvalidOperationException(
                        $"{variant} UI alias is duplicated: {alias.Key}");
                }

                if (generatedAssets.ContainsKey(alias.Key))
                {
                    throw new InvalidOperationException(
                        $"{variant} UI alias conflicts with an automatically generated key: {alias.Key}");
                }

                var objects = alias.Assets.Where(asset => asset != null).ToList();
                if (objects.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"{variant} UI alias has no assets: {alias.Key}");
                }

                foreach (var asset in objects)
                {
                    var assetPath = Normalize(AssetDatabase.GetAssetPath(asset));
                    if (string.IsNullOrWhiteSpace(assetPath) || !IsBelow(assetPath, V2Root))
                    {
                        throw new InvalidOperationException(
                            $"{variant} UI alias references an asset outside {V2Root}: {alias.Key} -> {assetPath}");
                    }
                }

                generatedAssets.Add(alias.Key, objects);
            }
        }

        private static UiAssetRegistry GenerateV1Registry()
        {
            var sourceRoot = AssetDatabase.IsValidFolder(LegacyResourcesRoot)
                ? LegacyResourcesRoot
                : V1ResourcesRoot;
            return GenerateRegistry("V1", sourceRoot, V1RegistryPath);
        }

        private static void CopyPrefabTree(
            string sourceRoot,
            string destinationRoot,
            IDictionary<string, string> guidMap,
            ICollection<string> copiedPaths)
        {
            if (!AssetDatabase.IsValidFolder(sourceRoot))
                throw new InvalidOperationException($"V1 prefab folder is missing: {sourceRoot}");
            EnsureFolder(destinationRoot);
            var prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { sourceRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => IsBelow(path, sourceRoot))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var sourcePath in prefabPaths)
            {
                var relative = Normalize(sourcePath).Substring(Normalize(sourceRoot).Length).TrimStart('/');
                CopyAssetWithIndependentGuid(
                    sourcePath,
                    destinationRoot + "/" + relative,
                    guidMap,
                    copiedPaths);
            }
        }

        private static void CopyAssetWithIndependentGuid(
            string sourcePath,
            string destinationPath,
            IDictionary<string, string> guidMap,
            ICollection<string> copiedPaths)
        {
            sourcePath = Normalize(sourcePath);
            destinationPath = Normalize(destinationPath);
            if (AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
                throw new InvalidOperationException($"V1 source asset is missing: {sourcePath}");
            EnsureFolder(Normalize(Path.GetDirectoryName(destinationPath)));
            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) == null &&
                !AssetDatabase.CopyAsset(sourcePath, destinationPath))
            {
                throw new InvalidOperationException($"Failed to copy {sourcePath} to {destinationPath}.");
            }

            var sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            var destinationGuid = AssetDatabase.AssetPathToGUID(destinationPath);
            if (string.IsNullOrWhiteSpace(sourceGuid) || string.IsNullOrWhiteSpace(destinationGuid) ||
                string.Equals(sourceGuid, destinationGuid, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"V2 copy did not receive an independent GUID: {destinationPath}");
            }

            guidMap[sourceGuid] = destinationGuid;
            copiedPaths.Add(destinationPath);
        }

        private static void RemapCopiedAssetReferences(
            IEnumerable<string> copiedPaths,
            IReadOnlyDictionary<string, string> guidMap)
        {
            var utf8 = new UTF8Encoding(false);
            foreach (var assetPath in copiedPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var extension = Path.GetExtension(assetPath);
                if (!string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(assetPath);
                var content = File.ReadAllText(fullPath, utf8);
                var remapped = content;
                foreach (var pair in guidMap)
                {
                    remapped = remapped.Replace("guid: " + pair.Key, "guid: " + pair.Value);
                }

                if (string.Equals(content, remapped, StringComparison.Ordinal)) continue;
                File.WriteAllText(fullPath, remapped, utf8);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
        }

        private static SceneAsset LoadScene(string sceneName)
        {
            return AssetDatabase.LoadAssetAtPath<SceneAsset>($"{V1Root}/Scenes/{sceneName}.unity") ??
                   AssetDatabase.LoadAssetAtPath<SceneAsset>($"Assets/Scenes/{sceneName}.unity");
        }

        private static Button FindDirectChildButton(Transform parent, string childName, string scenePath)
        {
            var child = parent.Cast<Transform>().SingleOrDefault(item => item.name == childName);
            var button = child != null ? child.GetComponent<Button>() : null;
            if (button == null)
                throw new InvalidOperationException($"{childName} Button was not found under ButtonNavigation in {scenePath}.");
            return button;
        }

        private static Image RequireImage(Button button, string scenePath)
        {
            var image = button.GetComponent<Image>();
            if (image == null)
                throw new InvalidOperationException($"{button.name} Image is missing in {scenePath}.");
            return image;
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
