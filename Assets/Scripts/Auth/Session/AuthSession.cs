using System;

[Serializable]
public sealed class AuthSession
{
    public int SchemaVersion;
    public string PlayerId;
    public string AccessToken;
    public string RefreshToken;
    public int ExpiresIn;
    public long IssuedAtUnixTime;
    public long ExpiresAtUnixTime;
    public bool IsOffline;
    public bool IsGuest;
}
