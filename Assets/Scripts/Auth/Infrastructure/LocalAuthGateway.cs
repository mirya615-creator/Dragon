using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Offline-only authentication used during client development.
/// It stores a salted password hash in PlayerPrefs and must not be used as production security.
/// </summary>
public sealed class LocalAuthGateway : IAuthGateway
{
    private const string AccountKeyPrefix = "dragonbound.local-auth.";
    private const string GuestKeyPrefix = "dragonbound.local-guest.";
    private const string EmailCodeKeyPrefix = "dragonbound.local-email-code.";
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100000;
    private const int CodeLifetimeSeconds = 5 * 60;
    private const int CodeCooldownSeconds = 60;
    private const int CodeDailyLimit = 10;
    private const int CodeAttemptLimit = 5;

    public Task<AuthSession> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string normalizedEmail = AuthInputValidator.NormalizeEmail(email);
        string key = GetAccountKey(normalizedEmail);

        if (!PlayerPrefs.HasKey(key))
        {
            throw InvalidCredentials();
        }

        LocalAccountRecord record = JsonUtility.FromJson<LocalAccountRecord>(PlayerPrefs.GetString(key));
        if (record == null || string.IsNullOrEmpty(record.salt) || string.IsNullOrEmpty(record.password_hash))
        {
            throw InvalidCredentials();
        }

        byte[] expectedHash;
        byte[] actualHash;
        try
        {
            byte[] salt = Convert.FromBase64String(record.salt);
            expectedHash = Convert.FromBase64String(record.password_hash);
            actualHash = DerivePasswordHash(password, salt);
        }
        catch (FormatException)
        {
            throw InvalidCredentials();
        }

        if (!FixedTimeEquals(expectedHash, actualHash))
        {
            throw InvalidCredentials();
        }

        AuthSession session = new AuthSession
        {
            PlayerId = record.player_id,
            AccessToken = string.Empty,
            RefreshToken = string.Empty,
            ExpiresIn = 0,
            IsOffline = true,
            IsGuest = false
        };
        return Task.FromResult(session);
    }

    public Task<AuthSession> GuestLoginAsync(GuestLoginRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request == null || string.IsNullOrWhiteSpace(request.device_id) || request.device_id.Length > 512)
        {
            throw new AuthException("INVALID_REQUEST", "Guest login request is invalid.");
        }

        string key = GuestKeyPrefix + HashKey(request.device_id);
        string playerId = PlayerPrefs.GetString(key, string.Empty);
        if (!Guid.TryParse(playerId, out _))
        {
            playerId = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(key, playerId);
            PlayerPrefs.Save();
        }

        return Task.FromResult(new AuthSession
        {
            PlayerId = playerId,
            AccessToken = string.Empty,
            RefreshToken = string.Empty,
            ExpiresIn = 0,
            IsOffline = true,
            IsGuest = true
        });
    }

    public Task SendEmailCodeAsync(
        string email,
        EmailCodePurpose purpose,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string normalizedEmail = AuthInputValidator.NormalizeEmail(email);
        string key = GetEmailCodeKey(normalizedEmail, purpose);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string today = DateTime.UtcNow.ToString("yyyyMMdd");
        LocalEmailCodeRecord record = LoadCodeRecord(key) ?? new LocalEmailCodeRecord();

        if (record.daily_date != today)
        {
            record.daily_date = today;
            record.daily_count = 0;
        }
        if (now < record.cooldown_until)
        {
            throw new AuthException("CODE_SEND_COOLDOWN", "Please wait before requesting another code.");
        }
        if (record.daily_count >= CodeDailyLimit)
        {
            throw new AuthException("CODE_DAILY_LIMIT", "Daily code limit reached.");
        }

        string code = CreateSixDigitCode();
        byte[] salt = new byte[SaltSize];
        using (RandomNumberGenerator random = RandomNumberGenerator.Create())
        {
            random.GetBytes(salt);
        }

        record.salt = Convert.ToBase64String(salt);
        record.code_hash = Convert.ToBase64String(HashCode(code, salt));
        record.expires_at = now + CodeLifetimeSeconds;
        record.cooldown_until = now + CodeCooldownSeconds;
        record.attempts = 0;
        record.daily_count++;
        SaveCodeRecord(key, record);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Development] {purpose} verification code for {normalizedEmail}: {code}");
#endif
        return Task.CompletedTask;
    }

    public Task<AuthSession> VerifyEmailCodeAsync(
        string email,
        string code,
        DeviceInfoDto deviceInfo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string normalizedEmail = AuthInputValidator.NormalizeEmail(email);
        if (!IsSixDigitCode(code))
        {
            throw InvalidCode();
        }

        ConsumeEmailCode(normalizedEmail, code, EmailCodePurpose.Login);

        string accountKey = GetAccountKey(normalizedEmail);
        LocalAccountRecord account = LoadAccount(accountKey);
        if (account == null || !Guid.TryParse(account.player_id, out _))
        {
            account = new LocalAccountRecord
            {
                player_id = Guid.NewGuid().ToString(),
                email = normalizedEmail,
                salt = string.Empty,
                password_hash = string.Empty
            };
            PlayerPrefs.SetString(accountKey, JsonUtility.ToJson(account));
            PlayerPrefs.Save();
        }

        return Task.FromResult(new AuthSession
        {
            PlayerId = account.player_id,
            AccessToken = string.Empty,
            RefreshToken = string.Empty,
            ExpiresIn = 0,
            IsOffline = true,
            IsGuest = false
        });
    }

    public Task RegisterWithEmailCodeAsync(
        string email,
        string password,
        string code,
        DeviceInfoDto deviceInfo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string normalizedEmail = AuthInputValidator.NormalizeEmail(email);
        AuthInputValidator.ValidatePassword(password);
        if (!IsSixDigitCode(code)) throw InvalidCode();

        string accountKey = GetAccountKey(normalizedEmail);
        if (PlayerPrefs.HasKey(accountKey))
        {
            throw new AuthException("IDENTITY_ALREADY_EXISTS", "This email is already registered.");
        }

        ConsumeEmailCode(normalizedEmail, code, EmailCodePurpose.Register);

        byte[] salt = new byte[SaltSize];
        using (RandomNumberGenerator random = RandomNumberGenerator.Create())
        {
            random.GetBytes(salt);
        }
        LocalAccountRecord account = new LocalAccountRecord
        {
            player_id = Guid.NewGuid().ToString(),
            email = normalizedEmail,
            salt = Convert.ToBase64String(salt),
            password_hash = Convert.ToBase64String(DerivePasswordHash(password, salt))
        };
        PlayerPrefs.SetString(accountKey, JsonUtility.ToJson(account));
        PlayerPrefs.Save();
        return Task.CompletedTask;
    }

    private static string GetAccountKey(string normalizedEmail)
    {
        return AccountKeyPrefix + HashKey(normalizedEmail);
    }

    private static string GetEmailCodeKey(string normalizedEmail, EmailCodePurpose purpose)
    {
        return EmailCodeKeyPrefix + purpose.ToString().ToLowerInvariant() + "." + HashKey(normalizedEmail);
    }

    private static void ConsumeEmailCode(string normalizedEmail, string code, EmailCodePurpose purpose)
    {
        string codeKey = GetEmailCodeKey(normalizedEmail, purpose);
        LocalEmailCodeRecord record = LoadCodeRecord(codeKey);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (record == null || string.IsNullOrEmpty(record.code_hash) || now >= record.expires_at ||
            record.attempts >= CodeAttemptLimit)
        {
            throw InvalidCode();
        }

        byte[] expectedHash;
        byte[] actualHash;
        try
        {
            byte[] salt = Convert.FromBase64String(record.salt);
            expectedHash = Convert.FromBase64String(record.code_hash);
            actualHash = HashCode(code, salt);
        }
        catch (FormatException)
        {
            ClearCodeChallenge(record);
            SaveCodeRecord(codeKey, record);
            throw InvalidCode();
        }

        record.attempts++;
        if (!FixedTimeEquals(expectedHash, actualHash))
        {
            if (record.attempts >= CodeAttemptLimit) ClearCodeChallenge(record);
            SaveCodeRecord(codeKey, record);
            throw InvalidCode();
        }

        ClearCodeChallenge(record);
        SaveCodeRecord(codeKey, record);
    }

    private static string HashKey(string value)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Convert.ToBase64String(digest).Replace('/', '_').Replace('+', '-').TrimEnd('=');
        }
    }

    private static LocalAccountRecord LoadAccount(string key)
    {
        if (!PlayerPrefs.HasKey(key)) return null;
        try
        {
            return JsonUtility.FromJson<LocalAccountRecord>(PlayerPrefs.GetString(key));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static LocalEmailCodeRecord LoadCodeRecord(string key)
    {
        if (!PlayerPrefs.HasKey(key)) return null;
        try
        {
            return JsonUtility.FromJson<LocalEmailCodeRecord>(PlayerPrefs.GetString(key));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static void SaveCodeRecord(string key, LocalEmailCodeRecord record)
    {
        PlayerPrefs.SetString(key, JsonUtility.ToJson(record));
        PlayerPrefs.Save();
    }

    private static string CreateSixDigitCode()
    {
        byte[] bytes = new byte[4];
        using (RandomNumberGenerator random = RandomNumberGenerator.Create())
        {
            random.GetBytes(bytes);
        }
        uint value = BitConverter.ToUInt32(bytes, 0) % 1000000;
        return value.ToString("D6");
    }

    private static byte[] HashCode(string code, byte[] salt)
    {
        byte[] codeBytes = Encoding.UTF8.GetBytes(code);
        byte[] content = new byte[salt.Length + codeBytes.Length];
        Buffer.BlockCopy(salt, 0, content, 0, salt.Length);
        Buffer.BlockCopy(codeBytes, 0, content, salt.Length, codeBytes.Length);
        using (SHA256 sha256 = SHA256.Create())
        {
            return sha256.ComputeHash(content);
        }
    }

    private static bool IsSixDigitCode(string code)
    {
        if (code == null || code.Length != 6) return false;
        for (int index = 0; index < code.Length; index++)
        {
            if (code[index] < '0' || code[index] > '9') return false;
        }
        return true;
    }

    private static void ClearCodeChallenge(LocalEmailCodeRecord record)
    {
        record.salt = string.Empty;
        record.code_hash = string.Empty;
        record.expires_at = 0;
        record.attempts = 0;
    }

    private static byte[] DerivePasswordHash(string password, byte[] salt)
    {
        using (Rfc2898DeriveBytes derivation = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
        {
            return derivation.GetBytes(HashSize);
        }
    }

    private static bool FixedTimeEquals(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
        {
            return false;
        }

        int difference = 0;
        for (int index = 0; index < left.Length; index++)
        {
            difference |= left[index] ^ right[index];
        }

        return difference == 0;
    }

    private static AuthException InvalidCredentials()
    {
        return new AuthException("INVALID_CREDENTIALS", "Incorrect email or password.");
    }

    private static AuthException InvalidCode()
    {
        return new AuthException("INVALID_CREDENTIALS", "Invalid or expired verification code.");
    }

    [Serializable]
    private sealed class LocalAccountRecord
    {
        public string player_id;
        public string email;
        public string salt;
        public string password_hash;
    }

    [Serializable]
    private sealed class LocalEmailCodeRecord
    {
        public string salt;
        public string code_hash;
        public long expires_at;
        public long cooldown_until;
        public int attempts;
        public string daily_date;
        public int daily_count;
    }
}
