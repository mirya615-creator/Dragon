using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// JSON-over-HTTP implementation of the client unary boundary. Despite the legacy
/// "Unary" name, the Go service contract is ordinary REST/JSON described by OpenAPI.
/// </summary>
public sealed class UnityWebRequestUnaryTransport : IUnaryTransport
{
    [Serializable]
    private sealed class ErrorEnvelope
    {
        public ErrorBody error;
    }

    [Serializable]
    private sealed class ErrorBody
    {
        public string code;
        public string message;
        public string trace_id;
    }

    private readonly string baseUrl;
    private readonly int timeoutSeconds;
    private readonly bool enableLogging;

    public UnityWebRequestUnaryTransport(
        string baseUrl,
        int timeoutSeconds,
        bool enableLogging)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("API base URL is required.", nameof(baseUrl));
        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out Uri uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("API base URL must be an absolute HTTP(S) URL.", nameof(baseUrl));
        }

        this.baseUrl = baseUrl.TrimEnd('/');
        this.timeoutSeconds = Math.Max(1, timeoutSeconds);
        this.enableLogging = enableLogging;
    }

    public async Task<TResponse> SendAsync<TRequest, TResponse>(
        string method,
        string path,
        TRequest request,
        UnaryRequestContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("HTTP method is required.", nameof(method));
        if (string.IsNullOrWhiteSpace(path) || path[0] != '/')
            throw new ArgumentException("API path must start with '/'.", nameof(path));

        cancellationToken.ThrowIfCancellationRequested();
        context = context ?? new UnaryRequestContext();
        string url = baseUrl + path;
        string body = request == null
            ? string.Empty
            : request is IUnaryRawJsonRequest rawRequest
                ? rawRequest.ToJson()
                : JsonUtility.ToJson(request);

        using (var webRequest = new UnityWebRequest(url, method.ToUpperInvariant()))
        {
            webRequest.timeout = timeoutSeconds;
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            if (!string.Equals(method, UnityWebRequest.kHttpVerbGET, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(method, UnityWebRequest.kHttpVerbDELETE, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(body))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                webRequest.SetRequestHeader("Content-Type", "application/json");
            }

            webRequest.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrWhiteSpace(context.AccessToken))
                webRequest.SetRequestHeader("Authorization", "Bearer " + context.AccessToken.Trim());
            if (!string.IsNullOrWhiteSpace(context.IdempotencyKey))
                webRequest.SetRequestHeader("Idempotency-Key", context.IdempotencyKey.Trim());
            if (!string.IsNullOrWhiteSpace(context.RequestId))
                webRequest.SetRequestHeader("X-Trace-Id", context.RequestId.Trim());

            if (enableLogging) Debug.Log($"[API] {method.ToUpperInvariant()} {path}");
            UnityWebRequestAsyncOperation operation = webRequest.SendWebRequest();
            using (cancellationToken.Register(webRequest.Abort))
            {
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
            }

            long status = webRequest.responseCode;
            string responseText = webRequest.downloadHandler == null
                ? string.Empty
                : webRequest.downloadHandler.text;
            if (enableLogging) Debug.Log($"[API] {status} {method.ToUpperInvariant()} {path}");

            if (webRequest.result != UnityWebRequest.Result.Success || status < 200 || status >= 300)
            {
                throw CreateException(webRequest, responseText, context.RequestId);
            }

            if (string.IsNullOrWhiteSpace(responseText) || typeof(TResponse) == typeof(EmptyResponse))
                return default;

            try
            {
                if (typeof(IUnaryRawJsonResponse).IsAssignableFrom(typeof(TResponse)))
                {
                    TResponse rawParsed = Activator.CreateInstance<TResponse>();
                    ((IUnaryRawJsonResponse)(object)rawParsed).ReadJson(responseText);
                    return rawParsed;
                }
                TResponse parsed = JsonUtility.FromJson<TResponse>(responseText);
                if (parsed == null)
                    throw new InvalidOperationException("Response body is empty or incompatible.");
                return parsed;
            }
            catch (Exception exception) when (!(exception is ClientServiceException))
            {
                throw new ClientServiceException(
                    "INVALID_RESPONSE",
                    "Server returned an invalid JSON response.",
                    false,
                    (int)status,
                    webRequest.GetResponseHeader("X-Trace-Id") ?? context.RequestId,
                    exception);
            }
        }
    }

    private static ClientServiceException CreateException(
        UnityWebRequest request,
        string responseText,
        string fallbackTraceId)
    {
        ErrorEnvelope envelope = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(responseText))
                envelope = JsonUtility.FromJson<ErrorEnvelope>(responseText);
        }
        catch (ArgumentException)
        {
            // Preserve the transport error below without exposing an arbitrary response body.
        }

        int status = (int)request.responseCode;
        string code = envelope?.error?.code;
        string message = envelope?.error?.message;
        string traceId = envelope?.error?.trace_id;
        if (string.IsNullOrWhiteSpace(code))
            code = status == 0 ? "NETWORK_ERROR" : "HTTP_" + status;
        if (string.IsNullOrWhiteSpace(message))
            message = status == 0 ? "Unable to reach the game server." : "Game server request failed.";
        if (string.IsNullOrWhiteSpace(traceId))
            traceId = request.GetResponseHeader("X-Trace-Id") ?? fallbackTraceId;

        bool retryable = status == 0 || status == 408 || status == 429 || status >= 500;
        return new ClientServiceException(
            code,
            message,
            retryable,
            status,
            traceId,
            string.IsNullOrWhiteSpace(request.error) ? null : new Exception(request.error),
            responseText);
    }
}

[Serializable]
public sealed class EmptyResponse
{
}
