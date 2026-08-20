using System;
using UnityEngine;

/// <summary>
/// Persists the current development session locally. Production refresh tokens must later move
/// to platform secure storage when the Go backend is enabled.
/// </summary>
public sealed class PersistentAuthSessionStore : IAuthSessionStore
{
    private const int CurrentSchemaVersion = 1;
    private const string DefaultSessionKey = "dragonbound.auth.session.v1";
    private readonly string sessionKey;

    public PersistentAuthSessionStore()
        : this(DefaultSessionKey)
    {
    }

    public PersistentAuthSessionStore(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Session storage key is required.", nameof(storageKey));
        sessionKey = storageKey;
        TryRestore(out _);
    }

    public AuthSession Current { get; private set; }

    public bool TryRestore(out AuthSession session)
    {
        session = null;
        if (!PlayerPrefs.HasKey(sessionKey))
        {
            Current = null;
            return false;
        }

        try
        {
            AuthSession restored = JsonUtility.FromJson<AuthSession>(
                PlayerPrefs.GetString(sessionKey, string.Empty));
            if (!IsValid(restored))
            {
                Clear();
                return false;
            }

            Current = restored;
            session = restored;
            return true;
        }
        catch (ArgumentException)
        {
            Clear();
            return false;
        }
    }

    public bool IsValid(AuthSession session)
    {
        if (session == null || session.SchemaVersion != CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(session.PlayerId))
        {
            return false;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return session.ExpiresAtUnixTime <= 0 || now < session.ExpiresAtUnixTime;
    }

    public void Set(AuthSession session)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        session.SchemaVersion = CurrentSchemaVersion;
        if (session.IssuedAtUnixTime <= 0) session.IssuedAtUnixTime = now;
        if (session.ExpiresAtUnixTime <= 0 && session.ExpiresIn > 0)
        {
            session.ExpiresAtUnixTime = now + session.ExpiresIn;
        }
        if (!IsValid(session)) throw new ArgumentException("Auth session is invalid.", nameof(session));

        Current = session;
        PlayerPrefs.SetString(sessionKey, JsonUtility.ToJson(session));
        PlayerPrefs.Save();
        Debug.Log($"Session saved for PlayerId: {session.PlayerId}, IsGuest: {session.IsGuest}");
    }

    public void Clear()
    {
        Current = null;
        PlayerPrefs.DeleteKey(sessionKey);
        PlayerPrefs.Save();
    }
}
