using System;

public sealed class AuthException : Exception
{
    public AuthException(
        string code,
        string message,
        bool retryable = false,
        int httpStatus = 0,
        string traceId = null,
        Exception innerException = null) : base(message, innerException)
    {
        Code = code;
        Retryable = retryable;
        HttpStatus = httpStatus;
        TraceId = traceId;
    }

    public string Code { get; }
    public bool Retryable { get; }
    public int HttpStatus { get; }
    public string TraceId { get; }
}
