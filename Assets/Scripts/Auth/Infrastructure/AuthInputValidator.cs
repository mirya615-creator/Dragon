using System;
using System.Text;

public static class AuthInputValidator
{
    public static string NormalizeEmail(string email)
    {
        string normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        string[] parts = normalized.Split('@');
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0 ||
            normalized.Length > 254 || ContainsForbiddenWhitespace(normalized))
        {
            throw new AuthException("INVALID_EMAIL", "Enter a valid email address.");
        }

        return normalized;
    }

    public static void ValidatePassword(string password)
    {
        int byteCount = Encoding.UTF8.GetByteCount(password ?? string.Empty);
        if (byteCount < 8 || byteCount > 72)
        {
            throw new AuthException("INVALID_PASSWORD", "Password must be 8-72 UTF-8 bytes.");
        }
    }

    private static bool ContainsForbiddenWhitespace(string value)
    {
        return value.IndexOf(' ') >= 0 || value.IndexOf('\r') >= 0 ||
               value.IndexOf('\n') >= 0 || value.IndexOf('\t') >= 0;
    }
}

