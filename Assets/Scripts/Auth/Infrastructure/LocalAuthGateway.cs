using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Offline-only Google and guest authentication used during client development.
/// </summary>
public sealed class LocalAuthGateway : IAuthGateway
{
    private const string GuestKeyPrefix = "dragonbound.local-guest.";
    private const string GoogleKeyPrefix = "dragonbound.local-google.";

    public Task<AuthSession> GuestLoginAsync(
        GuestLoginRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request == null || string.IsNullOrWhiteSpace(request.device_id) ||
            request.device_id.Length > 512)
        {
            throw new AuthException("INVALID_REQUEST", "Guest login request is invalid.");
        }

        string playerId = GetOrCreatePlayerId(GuestKeyPrefix + HashKey(request.device_id));
        return Task.FromResult(CreateSession(playerId, true));
    }

    public Task<AuthSession> GoogleLoginAsync(
        string idToken,
        DeviceInfoDto deviceInfo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        const string mockPrefix = "mock-google:";
        if (string.IsNullOrWhiteSpace(idToken) ||
            !idToken.StartsWith(mockPrefix, StringComparison.Ordinal))
        {
            throw new AuthException("INVALID_CREDENTIALS", "Google authentication failed.");
        }

        string subject = idToken.Substring(mockPrefix.Length);
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new AuthException("INVALID_CREDENTIALS", "Google authentication failed.");
        }

        string playerId = GetOrCreatePlayerId(GoogleKeyPrefix + HashKey(subject));
        return Task.FromResult(CreateSession(playerId, false));
    }

    private static AuthSession CreateSession(string playerId, bool isGuest)
    {
        return new AuthSession
        {
            SchemaVersion = 1,
            PlayerId = playerId,
            AccessToken = string.Empty,
            RefreshToken = string.Empty,
            ExpiresIn = 0,
            IssuedAtUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ExpiresAtUnixTime = 0,
            IsOffline = true,
            IsGuest = isGuest
        };
    }

    private static string GetOrCreatePlayerId(string key)
    {
        string playerId = PlayerPrefs.GetString(key, string.Empty);
        if (Guid.TryParse(playerId, out _)) return playerId;

        playerId = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(key, playerId);
        PlayerPrefs.Save();
        return playerId;
    }

    private static string HashKey(string value)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Convert.ToBase64String(digest).Replace('/', '_').Replace('+', '-').TrimEnd('=');
        }
    }
}
