using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the locally assigned player avatar and resolves it through the active UI variant.
/// </summary>
public static class PlayerAvatarProfile
{
    private const string V1ResourceRoot = "Profile/";
    private const string V2ResourceRoot = "UIResources/Main/Profile/";
    private const string PreferenceKeyPrefix = "dragonbound.player-avatar.v1.";
    private const int FirstAvatarIndex = 0;
    private const int LastAvatarIndex = 12;
    private const int AvatarCount = LastAvatarIndex - FirstAvatarIndex + 1;

    private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

    /// <summary>
    /// Creates the random assignment once for this player and reuses it on every later login.
    /// </summary>
    public static string GetOrCreateAvatarId(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return FirstAvatarIndex.ToString();

        string key = GetPreferenceKey(playerId);
        string stored = PlayerPrefs.GetString(key, string.Empty);
        if (TryParseAvatarId(stored, out int storedIndex)) return storedIndex.ToString();

        string avatarId = CreateRandomAvatarId();
        PlayerPrefs.SetString(key, avatarId);
        PlayerPrefs.Save();
        return avatarId;
    }

    public static string CreateRandomAvatarId()
    {
        return UnityEngine.Random.Range(FirstAvatarIndex, LastAvatarIndex + 1).ToString();
    }

    /// <summary>
    /// Uses a server/local DTO avatar when present. Legacy rows receive a stable player-id fallback.
    /// </summary>
    public static string ResolveAvatarId(string playerId, string avatarId)
    {
        if (TryParseAvatarId(avatarId, out int parsedIndex)) return parsedIndex.ToString();
        return StableAvatarIndex(playerId).ToString();
    }

    public static Sprite LoadSprite(string avatarId)
    {
        int index = TryParseAvatarId(avatarId, out int parsedIndex)
            ? parsedIndex
            : FirstAvatarIndex;
        string variantId = DragonBound.Presentation.UiAssets.Active?.VariantId ?? "V1";
        string resourceRoot = string.Equals(variantId, "V2", StringComparison.Ordinal)
            ? V2ResourceRoot
            : V1ResourceRoot;
        string cacheKey = variantId + ":" + index;
        if (SpriteCache.TryGetValue(cacheKey, out Sprite cached) && cached != null) return cached;

        bool isV2 = string.Equals(variantId, "V2", StringComparison.Ordinal);
        Sprite sprite = isV2
            ? DragonBound.Presentation.UiAssets.Active?.Load<Sprite>(resourceRoot + index)
            : DragonBound.Presentation.UiAssets.Load<Sprite>(resourceRoot + index);
        int resolvedIndex = index;
        if (sprite == null && index != FirstAvatarIndex)
        {
            // Missing V2 portraits intentionally fall back only to V2's avatar 0.
            resolvedIndex = FirstAvatarIndex;
            sprite = isV2
                ? DragonBound.Presentation.UiAssets.Active?.Load<Sprite>(resourceRoot + FirstAvatarIndex)
                : DragonBound.Presentation.UiAssets.Load<Sprite>(resourceRoot + FirstAvatarIndex);
        }
        if (sprite == null)
        {
            Debug.LogWarning(
                $"Player avatar sprite is missing. Variant={variantId}, Key={resourceRoot}{index}.");
            return null;
        }

        // Do not cache a temporary V2 fallback under the missing avatar ID. Once that
        // numbered portrait is added and the registry is regenerated, it takes effect.
        SpriteCache[variantId + ":" + resolvedIndex] = sprite;
        return sprite;
    }

    public static void Apply(Image image, string avatarId)
    {
        if (image == null) return;
        Sprite sprite = LoadSprite(avatarId);
        if (sprite == null) return;
        image.sprite = sprite;
        image.preserveAspect = true;
    }

    private static bool TryParseAvatarId(string avatarId, out int index)
    {
        return int.TryParse(avatarId, out index) &&
               index >= FirstAvatarIndex && index <= LastAvatarIndex;
    }

    private static string GetPreferenceKey(string playerId)
    {
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(playerId.Trim()))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return PreferenceKeyPrefix + encoded;
    }

    private static int StableAvatarIndex(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return FirstAvatarIndex;

        unchecked
        {
            uint hash = 2166136261;
            byte[] bytes = Encoding.UTF8.GetBytes(playerId.Trim());
            for (int index = 0; index < bytes.Length; index++)
            {
                hash ^= bytes[index];
                hash *= 16777619;
            }
            return FirstAvatarIndex + (int)(hash % AvatarCount);
        }
    }
}
