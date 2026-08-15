using System;

/// <summary>
/// Matches POST /v1/auth/guest. Field names intentionally follow the Go JSON contract.
/// </summary>
[Serializable]
public sealed class GuestLoginRequest
{
    public string device_id;
    public DeviceInfoDto device_info;
}

[Serializable]
public sealed class DeviceInfoDto
{
    public string platform;
    public string device_model;
    public string operating_system;
    public string app_version;
    public string system_language;
}

