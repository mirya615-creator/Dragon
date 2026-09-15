using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonBound.Presentation
{
    public sealed class UiNodeBindingMap : MonoBehaviour
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private string key;
            [SerializeField] private Transform target;

            public string Key => key;
            public Transform Target => target;

#if UNITY_EDITOR
            public void Configure(string value, Transform valueTarget)
            {
                key = value;
                target = valueTarget;
            }
#endif
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        private Dictionary<string, Transform> bindings;

        public bool TryResolve(string key, out Transform target)
        {
            if (bindings == null)
            {
                bindings = new Dictionary<string, Transform>(StringComparer.Ordinal);
                for (var index = 0; index < entries.Count; index++)
                {
                    var entry = entries[index];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.Target == null) continue;
                    bindings[Normalize(entry.Key)] = entry.Target;
                }
            }

            return bindings.TryGetValue(Normalize(key), out target) && target != null;
        }

#if UNITY_EDITOR
        public void Configure(IReadOnlyList<string> keys, IReadOnlyList<Transform> targets)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            if (keys.Count != targets.Count)
                throw new ArgumentException("UI binding keys and targets must have the same count.");

            entries.Clear();
            for (var index = 0; index < keys.Count; index++)
            {
                var entry = new Entry();
                entry.Configure(keys[index], targets[index]);
                entries.Add(entry);
            }

            bindings = null;
        }
#endif

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').Trim('/');
        }
    }

    public static class TransformUiBindingExtensions
    {
        public static Transform FindUi(this Transform root, string semanticKey)
        {
            if (root == null || string.IsNullOrWhiteSpace(semanticKey)) return null;

            var maps = root.GetComponentsInParent<UiNodeBindingMap>(true);
            for (var index = 0; index < maps.Length; index++)
            {
                if (maps[index].TryResolve(semanticKey, out var bound)) return bound;
            }

            var direct = root.Find(semanticKey);
            if (direct != null) return direct;

            var normalized = semanticKey.Replace('\\', '/').Trim('/');
            var separator = normalized.LastIndexOf('/');
            var leafName = separator >= 0 ? normalized.Substring(separator + 1) : normalized;
            Transform match = null;
            var descendants = root.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < descendants.Length; index++)
            {
                var candidate = descendants[index];
                if (candidate == root || !string.Equals(candidate.name, leafName, StringComparison.Ordinal)) continue;
                if (match != null)
                {
                    Debug.LogError(
                        $"UI binding '{semanticKey}' is ambiguous below '{root.name}'. Add an explicit UiNodeBindingMap entry.",
                        root);
                    return null;
                }

                match = candidate;
            }

            return match;
        }
    }
}
