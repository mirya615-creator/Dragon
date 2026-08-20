using System;
using System.Security.Cryptography;
using UnityEngine;

/// <summary>
/// Owns the installation-scoped guest credential used by local and server gateways.
/// PlayerPrefs is acceptable for offline development; production builds should use secure platform storage.
/// </summary>
public sealed class GuestIdentityService : IGuestIdentityProvider
{
    private const string DeviceIdKey = "dragonbound.guest.device-id";
    private const int DeviceIdBytes = 32;

    public GuestLoginRequest CreateRequest()
    {
        return new GuestLoginRequest
        {
            device_id = GetOrCreateDeviceId(),
            device_info = CreateDeviceInfo()
        };
    }

    public DeviceInfoDto CreateDeviceInfo()
    {
        return new DeviceInfoDto
        {
            platform = Application.platform.ToString(),
            device_model = SystemInfo.deviceModel,
            operating_system = SystemInfo.operatingSystem,
            app_version = Application.version,
            system_language = Application.systemLanguage.ToString()
        };
    }

    private static string GetOrCreateDeviceId()
    {
        string existing = PlayerPrefs.GetString(DeviceIdKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        byte[] randomBytes = new byte[DeviceIdBytes];
        using (RandomNumberGenerator random = RandomNumberGenerator.Create())
        {
            random.GetBytes(randomBytes);
        }

        string deviceId = Convert.ToBase64String(randomBytes)
            .Replace('/', '_')
            .Replace('+', '-')
            .TrimEnd('=');
        PlayerPrefs.SetString(DeviceIdKey, deviceId);
        PlayerPrefs.Save();
        return deviceId;
    }
}
