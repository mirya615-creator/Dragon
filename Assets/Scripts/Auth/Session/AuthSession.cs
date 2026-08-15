using System;
using UnityEngine;

[Serializable]
public sealed class AuthSession
{
    public string PlayerId;
    public string AccessToken;
    public string RefreshToken;
    public int ExpiresIn;
    public bool IsOffline;
    public bool IsGuest;
}

public static class AuthSessionStore
{
    public static AuthSession Current { get; private set; }

    public static void Set(AuthSession session)
    {
        Current = session ?? throw new ArgumentNullException(nameof(session));
        Debug.Log($"PlayerId: {Current.PlayerId}, IsGuest: {Current.IsGuest}");
    }

    public static void Clear()
    {
        Current = null;
    }
}
