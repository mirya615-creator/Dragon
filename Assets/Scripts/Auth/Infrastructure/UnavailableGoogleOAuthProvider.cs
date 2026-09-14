using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Fail-closed placeholder used by online mode until a real Google system-browser
/// OAuth SDK returns an ID token accepted by the server.
/// </summary>
public sealed class UnavailableGoogleOAuthProvider : IGoogleOAuthProvider
{
    public Task<PendingGoogleIdentity> SignInAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<PendingGoogleIdentity>(new AuthException(
            "GOOGLE_PROVIDER_NOT_CONFIGURED",
            "Google sign-in is not configured for this build."));
    }

    public void CancelPendingSignIn()
    {
    }
}
