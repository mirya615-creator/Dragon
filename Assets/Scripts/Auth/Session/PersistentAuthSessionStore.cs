using System;
using UnityEngine;

/// <summary>
/// Persists the current development session locally. Production refresh tokens must later move
/// to platform secure storage when the Go backend is enabled.
/// </summary>
public sealed class PersistentAuthSessionStore : IAuthSessionStore
{
    private const string SecureStoreClassName =
        "com.drakeforge.mergedefense.identity.SecureSessionStore";
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
        string serialized = ReadSerializedSession();
        if (string.IsNullOrWhiteSpace(serialized))
        {
            Current = null;
            return false;
        }

        try
        {
            AuthSession restored = JsonUtility.FromJson<AuthSession>(
                serialized);
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
        if (session.ExpiresAtUnixTime <= 0 || now < session.ExpiresAtUnixTime) return true;

        // An expired online access token remains a recoverable session while a refresh
        // token exists. RefreshingUnaryTransport rotates it on the next API request.
        return !session.IsOffline && !string.IsNullOrWhiteSpace(session.RefreshToken);
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
        string serialized = JsonUtility.ToJson(session);
        if (!WriteSerializedSession(serialized))
        {
            Current = null;
            throw new InvalidOperationException("Unable to save the authentication session securely.");
        }
        Debug.Log($"Session saved for PlayerId: {session.PlayerId}, IsGuest: {session.IsGuest}");
    }

    public void Clear()
    {
        Current = null;
        DeleteSerializedSession();
    }

    private string ReadSerializedSession()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity =
                   unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var store = new AndroidJavaClass(SecureStoreClassName))
            {
                string secure = store.CallStatic<string>("read", activity, sessionKey);
                if (!string.IsNullOrWhiteSpace(secure)) return secure;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Unable to read the secure authentication session: " + exception.Message);
        }

        // One-time migration from the previous PlayerPrefs implementation.
        string legacy = PlayerPrefs.GetString(sessionKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(legacy) && WriteSerializedSession(legacy))
        {
            PlayerPrefs.DeleteKey(sessionKey);
            PlayerPrefs.Save();
            return legacy;
        }
        return string.Empty;
#else
        return PlayerPrefs.GetString(sessionKey, string.Empty);
#endif
    }

    private bool WriteSerializedSession(string serialized)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity =
                   unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var store = new AndroidJavaClass(SecureStoreClassName))
                return store.CallStatic<bool>("write", activity, sessionKey, serialized);
        }
        catch (Exception exception)
        {
            Debug.LogError("Unable to write the secure authentication session: " + exception.Message);
            return false;
        }
#else
        PlayerPrefs.SetString(sessionKey, serialized);
        PlayerPrefs.Save();
        return true;
#endif
    }

    private void DeleteSerializedSession()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity =
                   unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var store = new AndroidJavaClass(SecureStoreClassName))
                store.CallStatic("delete", activity, sessionKey);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Unable to delete the secure authentication session: " + exception.Message);
        }
#endif
        PlayerPrefs.DeleteKey(sessionKey);
        PlayerPrefs.Save();
    }
}
