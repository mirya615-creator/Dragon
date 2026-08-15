using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Development-only Google identity preview. Replace with a system-browser OAuth provider for production.
/// </summary>
public sealed class MockGoogleOAuthProvider : IGoogleOAuthProvider
{
    private const string MockSubject = "mock-google-player";
    private const string MockEmail = "google-player@example.com";

    public async Task<PendingGoogleIdentity> SignInAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        return new PendingGoogleIdentity
        {
            Subject = MockSubject,
            Email = MockEmail,
            EmailVerified = true,
            PictureUrl = string.Empty,
            IdToken = "mock-google:" + MockSubject,
            AvatarSprite = CreateDevelopmentAvatar(),
            OwnsAvatarSprite = true
        };
    }

    public void CancelPendingSignIn()
    {
    }

    private static Sprite CreateDevelopmentAvatar()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "MockGoogleAvatarTexture",
            filterMode = FilterMode.Bilinear
        };
        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radiusSquared = center * center;
        Color32 blue = new Color32(66, 133, 244, 255);
        Color32 red = new Color32(234, 67, 53, 255);
        Color32 yellow = new Color32(251, 188, 5, 255);
        Color32 green = new Color32(52, 168, 83, 255);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float offsetX = x - center;
                float offsetY = y - center;
                if (offsetX * offsetX + offsetY * offsetY > radiusSquared)
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                }
                else if (x < center && y >= center)
                {
                    pixels[y * size + x] = blue;
                }
                else if (x >= center && y >= center)
                {
                    pixels[y * size + x] = red;
                }
                else if (x < center)
                {
                    pixels[y * size + x] = green;
                }
                else
                {
                    pixels[y * size + x] = yellow;
                }
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
