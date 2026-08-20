using System;

public sealed class ClientServiceException : Exception
{
    public ClientServiceException(
        string code,
        string message,
        bool retryable = false,
        int httpStatus = 0,
        string traceId = "",
        Exception innerException = null)
        : base(message, innerException)
    {
        Code = code ?? string.Empty;
        Retryable = retryable;
        HttpStatus = httpStatus;
        TraceId = traceId ?? string.Empty;
    }

    public string Code { get; }
    public bool Retryable { get; }
    public int HttpStatus { get; }
    public string TraceId { get; }
}
