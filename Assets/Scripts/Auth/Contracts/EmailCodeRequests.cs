using System;

/// <summary>
/// Matches POST /v1/auth/email/code/send.
/// </summary>
[Serializable]
public sealed class SendEmailCodeRequest
{
    public string email;
    public string purpose = "login";
}

/// <summary>
/// Matches POST /v1/auth/email/code/verify.
/// </summary>
[Serializable]
public sealed class VerifyEmailCodeRequest
{
    public string email;
    public string code;
    public DeviceInfoDto device_info;
}

