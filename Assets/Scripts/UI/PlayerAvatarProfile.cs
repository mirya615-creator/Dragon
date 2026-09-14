using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the locally assigned player avatar and loads the authored Profile/0-12 sprites.
/// </summary>
public static class PlayerAvatarProfile
{
    private const string ResourceRoot = "Profile/";
    private const string PreferenceKeyPrefix = "dragonbound.player-avatar.v1.";
    private const int FirstAvatarIndex = 0;
    private const int LastAvatarIndex = 12;
    private const int AvatarCount = LastAvatarIndex - FirstAvatarIndex + 1;

    private static readonly Dictionary<int, Sprite> SpriteCache = new Dictionary<int, Sprite>();

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
        if (SpriteCache.TryGetValue(index, out Sprite cached) && cached != null) return cached;

        Sprite sprite = DragonBound.Presentation.UiAssets.Load<Sprite>(ResourceRoot + index);
        if (sprite == null && index != FirstAvatarIndex)
        {
            sprite = DragonBound.Presentation.UiAssets.Load<Sprite>(ResourceRoot + FirstAvatarIndex);
        }
        if (sprite == null)
        {
            Debug.LogWarning($"Player avatar sprite is missing at Resources/{ResourceRoot}{index}.");
            return null;
        }

        SpriteCache[index] = sprite;
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
