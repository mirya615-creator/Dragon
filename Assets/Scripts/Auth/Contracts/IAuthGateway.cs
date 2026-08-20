using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Authentication boundary used by the Login scene.
/// The current client uses LocalAuthGateway; a server adapter can implement the same contract.
/// </summary>
public interface IAuthGateway
{
    Task<AuthSession> GuestLoginAsync(GuestLoginRequest request, CancellationToken cancellationToken);

    Task<AuthSession> GoogleLoginAsync(
        string idToken,
        DeviceInfoDto deviceInfo,
        CancellationToken cancellationToken);
}
