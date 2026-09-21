using System;
using System.Collections.Generic;
using DragonBound.Presentation;
using UnityEngine;

namespace DragonBound.Editor.Versioning
{
    /// <summary>
    /// Version-owned logical resource keys. Unlike a generated registry, this asset is
    /// hand-maintained and survives every resource scan.
    /// </summary>
    [CreateAssetMenu(
        fileName = "UiAssetRegistryAliases",
        menuName = "DragonBound/Versioning/UI Asset Registry Aliases")]
    public sealed class DragonBoundUiAssetAliasCatalog : ScriptableObject
    {
        [SerializeField] private string variantId = "V2";
        [SerializeField] private List<UiAssetRegistry.Entry> entries =
            new List<UiAssetRegistry.Entry>();

        public string VariantId => variantId;
        public IReadOnlyList<UiAssetRegistry.Entry> Entries => entries;

        public void Configure(string id, List<UiAssetRegistry.Entry> sourceEntries)
        {
            variantId = id;
            entries = sourceEntries ?? new List<UiAssetRegistry.Entry>();
        }
    }
}
