using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class GoAuthGateway : IAuthGateway
{
    private readonly IUnaryTransport anonymousTransport;
    private readonly IUnaryTransport authenticatedTransport;
    private readonly GoApiContextFactory contexts;

    internal GoAuthGateway(
        IUnaryTransport anonymousTransport,
        IUnaryTransport authenticatedTransport,
        GoApiContextFactory contexts)
    {
        this.anonymousTransport = anonymousTransport ??
            throw new ArgumentNullException(nameof(anonymousTransport));
        this.authenticatedTransport = authenticatedTransport ??
            throw new ArgumentNullException(nameof(authenticatedTransport));
        this.contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
    }

    public async Task<AuthSession> GuestLoginAsync(
        GuestLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.device_id))
            throw new AuthException("INVALID_REQUEST", "Guest device ID is required.");

        try
        {
            GoAuthResponse response = await anonymousTransport.SendAsync<GuestLoginRequest, GoAuthResponse>(
                "POST", "/v1/auth/guest", request, AnonymousContext(), cancellationToken);
            return ToSession(response, true);
        }
        catch (ClientServiceException exception)
        {
            throw new AuthException(exception.Code, exception.Message);
        }
    }

    public async Task<AuthSession> GoogleLoginAsync(
        string idToken,
        DeviceInfoDto deviceInfo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            throw new AuthException("INVALID_CREDENTIALS", "Google ID token is required.");

        try
        {
            var request = new GoGoogleLoginRequest
            {
                id_token = idToken,
                device_info = deviceInfo
            };
            GoAuthResponse response = await anonymousTransport.SendAsync<GoGoogleLoginRequest, GoAuthResponse>(
                "POST", "/v1/auth/google", request, AnonymousContext(), cancellationToken);
            return ToSession(response, false);
        }
        catch (ClientServiceException exception)
        {
            throw new AuthException(exception.Code, exception.Message);
        }
    }

    public async Task LinkGoogleAsync(
        string idToken,
        DeviceInfoDto deviceInfo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            throw new AuthException("INVALID_CREDENTIALS", "Google ID token is required.");
        try
        {
            await authenticatedTransport.SendAsync<GoGoogleLoginRequest, EmptyResponse>(
                "POST",
                "/v1/auth/link/google",
                new GoGoogleLoginRequest { id_token = idToken, device_info = deviceInfo },
                contexts.Create(Guid.NewGuid().ToString("N")),
                cancellationToken);
        }
        catch (ClientServiceException exception)
        {
            throw new AuthException(exception.Code, exception.Message);
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            await authenticatedTransport.SendAsync<object, EmptyResponse>(
                "POST", "/v1/auth/logout", null, contexts.Create(), cancellationToken);
        }
        catch (ClientServiceException exception)
        {
            throw new AuthException(exception.Code, exception.Message);
        }
    }

    internal static AuthSession ToSession(GoAuthResponse response, bool isGuest)
    {
        if (response == null || string.IsNullOrWhiteSpace(response.player_id) ||
            string.IsNullOrWhiteSpace(response.access_token) ||
            string.IsNullOrWhiteSpace(response.refresh_token))
        {
            throw new AuthException("INVALID_RESPONSE", "Authentication response is incomplete.");
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new AuthSession
        {
            SchemaVersion = 1,
            PlayerId = response.player_id,
            AccessToken = response.access_token,
            RefreshToken = response.refresh_token,
            ExpiresIn = response.expires_in,
            IssuedAtUnixTime = now,
            ExpiresAtUnixTime = now + Math.Max(1, response.expires_in),
            IsOffline = false,
            IsGuest = isGuest
        };
    }

    private static UnaryRequestContext AnonymousContext()
    {
        return new UnaryRequestContext { RequestId = Guid.NewGuid().ToString("N") };
    }
}
