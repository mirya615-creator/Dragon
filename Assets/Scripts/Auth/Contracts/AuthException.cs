using System;

public sealed class AuthException : Exception
{
    public AuthException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

