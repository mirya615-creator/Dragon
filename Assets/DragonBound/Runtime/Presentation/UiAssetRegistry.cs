using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonBound.Presentation
{
    [CreateAssetMenu(
        fileName = "UiAssetRegistry",
        menuName = "DragonBound/UI/Asset Registry")]
    public sealed class UiAssetRegistry : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string key;
            [SerializeField] private UnityEngine.Object[] assets = Array.Empty<UnityEngine.Object>();

            public string Key => key;
            public IReadOnlyList<UnityEngine.Object> Assets => assets;

#if UNITY_EDITOR
            public void Configure(string value, UnityEngine.Object[] objects)
            {
                key = value;
                assets = objects ?? Array.Empty<UnityEngine.Object>();
            }
#endif
        }

        [SerializeField] private string variantId = "V1";
        [SerializeField] private List<Entry> entries = new List<Entry>();

        private readonly Dictionary<string, Entry> entriesByKey =
            new Dictionary<string, Entry>(StringComparer.Ordinal);

        public string VariantId => variantId;
        public IReadOnlyList<Entry> Entries => entries;

        private void OnEnable()
        {
            RebuildIndex();
#if !UNITY_EDITOR
            UiAssets.Install(this);
#endif
        }

        private void OnDisable()
        {
            UiAssets.Uninstall(this);
        }

        public T Load<T>(string key) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (!entriesByKey.TryGetValue(NormalizeKey(key), out var entry)) return null;

            // A prefab registry entry also contains its nested GameObjects. Unity does not
            // guarantee LoadAllAssetsAtPath ordering, so select the prefab root explicitly.
            if (typeof(T) == typeof(GameObject))
            {
                for (var index = 0; index < entry.Assets.Count; index++)
                {
                    if (entry.Assets[index] is GameObject candidate && candidate.transform.parent == null)
                        return candidate as T;
                }
            }

            for (var index = 0; index < entry.Assets.Count; index++)
            {
                if (entry.Assets[index] is T asset) return asset;
            }

            return null;
        }

        public T[] LoadAll<T>(string prefix) where T : UnityEngine.Object
        {
            var normalizedPrefix = NormalizeKey(prefix).TrimEnd('/');
            var result = new List<T>();
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                var entry = entries[entryIndex];
                if (entry == null ||
                    (normalizedPrefix.Length > 0 &&
                     !string.Equals(entry.Key, normalizedPrefix, StringComparison.Ordinal) &&
                     !entry.Key.StartsWith(normalizedPrefix + "/", StringComparison.Ordinal)))
                {
                    continue;
                }

                for (var assetIndex = 0; assetIndex < entry.Assets.Count; assetIndex++)
                {
                    if (entry.Assets[assetIndex] is T asset && !result.Contains(asset)) result.Add(asset);
                }
            }

            return result.ToArray();
        }

        public bool ContainsKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && entriesByKey.ContainsKey(NormalizeKey(key));
        }

        private void RebuildIndex()
        {
            entriesByKey.Clear();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key)) continue;
                entriesByKey[NormalizeKey(entry.Key)] = entry;
            }
        }

        private static string NormalizeKey(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').Trim('/');
        }

#if UNITY_EDITOR
        public void Configure(string id, List<Entry> sourceEntries)
        {
            variantId = id;
            entries = sourceEntries ?? new List<Entry>();
            RebuildIndex();
        }
#endif
    }

    public static class UiAssets
    {
        private static UiAssetRegistry active;

        public static UiAssetRegistry Active => active;

        public static void Activate(UiAssetRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            // Explicit editor/build-profile selection is allowed to replace the previous variant.
            active = registry;
        }

        internal static void Install(UiAssetRegistry registry)
        {
            if (registry == null) return;
            if (active != null && active != registry)
            {
                Debug.LogError(
                    $"Multiple UI asset registries are active: {active.VariantId} and {registry.VariantId}.");
            }

            active = registry;
        }

        internal static void Uninstall(UiAssetRegistry registry)
        {
            if (active == registry) active = null;
        }

        public static T Load<T>(string key) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            TryActivateEditorRegistry();
#endif
            if (active == null)
            {
#if UNITY_EDITOR
                // Keeps the project runnable before the one-time V1 migration has created and
                // activated its registry. Player builds never have this fallback.
                return Resources.Load<T>(key);
#else
                EnsureInstalled();
#endif
            }

            var asset = active.Load<T>(key);
            if (asset == null)
            {
                Debug.LogError($"UI asset is not registered. Variant={active.VariantId}, Key={key}, Type={typeof(T).Name}");
            }

            return asset;
        }

        public static T[] LoadAll<T>(string prefix) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            TryActivateEditorRegistry();
#endif
            if (active == null)
            {
#if UNITY_EDITOR
                return Resources.LoadAll<T>(prefix);
#else
                EnsureInstalled();
#endif
            }

            return active.LoadAll<T>(prefix);
        }

#if UNITY_EDITOR
        private static void TryActivateEditorRegistry()
        {
            if (active != null) return;
            var preloaded = UnityEditor.PlayerSettings.GetPreloadedAssets();
            UiAssetRegistry selected = null;
            for (var index = 0; index < preloaded.Length; index++)
            {
                if (!(preloaded[index] is UiAssetRegistry registry)) continue;
                if (selected != null && selected != registry)
                    throw new InvalidOperationException("Multiple UI asset registries are selected in Player Settings.");
                selected = registry;
            }

            if (selected != null) Install(selected);
        }
#endif

        private static void EnsureInstalled()
        {
            if (active == null)
            {
                throw new InvalidOperationException(
                    "No UiAssetRegistry is active. Select a DragonBound UI version before entering Play Mode or building.");
            }
        }
    }
}
