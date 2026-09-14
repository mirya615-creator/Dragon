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
            UiAssets.Install(this);
        }

        private void OnDisable()
        {
            UiAssets.Uninstall(this);
        }

        public T Load<T>(string key) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (!entriesByKey.TryGetValue(NormalizeKey(key), out var entry)) return null;

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
                    (!string.Equals(entry.Key, normalizedPrefix, StringComparison.Ordinal) &&
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
            EnsureInstalled();
            var asset = active.Load<T>(key);
            if (asset == null)
            {
                Debug.LogError($"UI asset is not registered. Variant={active.VariantId}, Key={key}, Type={typeof(T).Name}");
            }

            return asset;
        }

        public static T[] LoadAll<T>(string prefix) where T : UnityEngine.Object
        {
            EnsureInstalled();
            return active.LoadAll<T>(prefix);
        }

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
