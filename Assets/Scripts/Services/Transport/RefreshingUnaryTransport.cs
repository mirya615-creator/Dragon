using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Refreshes an expired JWT once and retries the original authenticated request.</summary>
public sealed class RefreshingUnaryTransport : IUnaryTransport
{
    private readonly IUnaryTransport inner;
    private readonly IAuthSessionStore sessionStore;
    private readonly SemaphoreSlim refreshLock = new SemaphoreSlim(1, 1);

    public RefreshingUnaryTransport(IUnaryTransport inner, IAuthSessionStore sessionStore)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    }

    public async Task<TResponse> SendAsync<TRequest, TResponse>(
        string method,
        string path,
        TRequest request,
        UnaryRequestContext context,
        CancellationToken cancellationToken)
    {
        context = context ?? new UnaryRequestContext();
        try
        {
            return await inner.SendAsync<TRequest, TResponse>(
                method, path, request, context, cancellationToken);
        }
        catch (ClientServiceException exception) when (
            exception.HttpStatus == 401 &&
            !string.IsNullOrWhiteSpace(context.AccessToken) &&
            path != "/v1/auth/refresh")
        {
            await RefreshSessionAsync(context.AccessToken, cancellationToken);
            AuthSession refreshed = sessionStore.Current;
            if (refreshed == null || string.IsNullOrWhiteSpace(refreshed.AccessToken))
                throw;

            var retryContext = new UnaryRequestContext
            {
                AccessToken = refreshed.AccessToken,
                IdempotencyKey = context.IdempotencyKey,
                RequestId = context.RequestId,
                ClientVersion = context.ClientVersion,
                ContentVersion = context.ContentVersion
            };
            return await inner.SendAsync<TRequest, TResponse>(
                method, path, request, retryContext, cancellationToken);
        }
    }

    private async Task RefreshSessionAsync(
        string rejectedAccessToken,
        CancellationToken cancellationToken)
    {
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            AuthSession current = sessionStore.Current;
            if (current == null || string.IsNullOrWhiteSpace(current.RefreshToken))
            {
                sessionStore.Clear();
                return;
            }
            if (!string.Equals(current.AccessToken, rejectedAccessToken, StringComparison.Ordinal))
                return;

            try
            {
                GoAuthResponse response = await inner.SendAsync<GoRefreshRequest, GoAuthResponse>(
                    "POST",
                    "/v1/auth/refresh",
                    new GoRefreshRequest { refresh_token = current.RefreshToken },
                    new UnaryRequestContext { RequestId = Guid.NewGuid().ToString("N") },
                    cancellationToken);
                sessionStore.Set(GoAuthGateway.ToSession(response, current.IsGuest));
            }
            catch
            {
                sessionStore.Clear();
                throw;
            }
        }
        finally
        {
            refreshLock.Release();
        }
    }
}
