using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Authentication boundary used by the Login scene.
/// The current client uses LocalAuthGateway; a server adapter can implement the same contract.
/// </summary>
public interface IAuthGateway
{
    Task<AuthSession> LoginAsync(string email, string password, CancellationToken cancellationToken);

    Task<AuthSession> GuestLoginAsync(GuestLoginRequest request, CancellationToken cancellationToken);

    Task SendEmailCodeAsync(
        string email,
        EmailCodePurpose purpose,
        CancellationToken cancellationToken);

    Task<AuthSession> VerifyEmailCodeAsync(
        string email,
        string code,
        DeviceInfoDto deviceInfo,
        CancellationToken cancellationToken);

    Task RegisterWithEmailCodeAsync(
        string email,
        string password,
        string code,
        DeviceInfoDto deviceInfo,
        CancellationToken cancellationToken);
}
