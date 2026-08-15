using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// External Google authorization boundary. Production implementations must use the system browser.
/// </summary>
public interface IGoogleOAuthProvider
{
    Task<PendingGoogleIdentity> SignInAsync(CancellationToken cancellationToken);

    void CancelPendingSignIn();
}

public sealed class PendingGoogleIdentity
{
    public string Subject;
    public string Email;
    public bool EmailVerified;
    public string PictureUrl;
    public string IdToken;
    public Sprite AvatarSprite;
    public bool OwnsAvatarSprite;
}
