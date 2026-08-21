using System;

namespace DragonBound.Runes
{
    /// <summary>Content-only UI contract. Resource lookup is intentionally left to the presentation layer.</summary>
    public sealed class RunePresentationData
    {
        public RunePresentationData(string runeId, string displayNameKey, string iconKey, string artAssetKey,
            string rarityThemeKey, string frameKey, string backgroundKey, bool usesGreyboxPlaceholder)
        {
            RuneId = runeId; DisplayNameKey = displayNameKey; IconKey = iconKey; ArtAssetKey = artAssetKey;
            RarityThemeKey = rarityThemeKey; FrameKey = frameKey; BackgroundKey = backgroundKey;
            UsesGreyboxPlaceholder = usesGreyboxPlaceholder;
        }
        public string RuneId { get; }
        public string DisplayNameKey { get; }
        public string IconKey { get; }
        public string ArtAssetKey { get; }
        public string RarityThemeKey { get; }
        public string FrameKey { get; }
        public string BackgroundKey { get; }
        public bool UsesGreyboxPlaceholder { get; }
    }
    public interface IRunePresentationProvider { RunePresentationData Get(string runeId); }
    public sealed class RunePresentationCatalog : IRunePresentationProvider
    {
        public RunePresentationData Get(string runeId)
        {
            var definition = RuneCatalog.Get(runeId);
            if (definition == null) return null;
            return new RunePresentationData(definition.RuneId, definition.DisplayNameKey, definition.IconKey,
                definition.ArtAssetKey, definition.RarityThemeKey, "RuneFrame." + definition.Rarity,
                "RuneBackground.Greybox", true);
        }
    }
}
