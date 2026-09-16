using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Android production implementation of Sign in with Google through Credential Manager.
/// The Google ID token is kept in memory only and is sent to the Drakeforge backend.
/// </summary>
public sealed class AndroidCredentialManagerGoogleOAuthProvider : IGoogleOAuthProvider
{
    private const string BridgeClassName =
        "com.drakeforge.mergedefense.identity.GoogleCredentialBridge";

    [Serializable]
    private sealed class NativeCredential
    {
        public string id_token;
        public string email;
        public string display_name;
        public string picture_url;
    }

    [Serializable]
    private sealed class GoogleTokenClaims
    {
        public string sub;
        public string email;
        public bool email_verified;
        public string picture;
    }

    [UnityEngine.Scripting.Preserve]
    private sealed class NativeCallback : AndroidJavaProxy
    {
        private readonly Action<string> success;
        private readonly Action<string, string> failure;
        private readonly Action cancelled;

        public NativeCallback(
            Action<string> success,
            Action<string, string> failure,
            Action cancelled)
            : base(BridgeClassName + "$Callback")
        {
            this.success = success;
            this.failure = failure;
            this.cancelled = cancelled;
        }

        [UnityEngine.Scripting.Preserve]
        public void onSuccess(string json) => success?.Invoke(json);
        [UnityEngine.Scripting.Preserve]
        public void onError(string code, string message) => failure?.Invoke(code, message);
        [UnityEngine.Scripting.Preserve]
        public void onCancelled() => cancelled?.Invoke();
    }

    private readonly object sync = new object();
    private readonly string webClientId;
    private TaskCompletionSource<PendingGoogleIdentity> pending;
    private CancellationTokenRegistration cancellationRegistration;
    private NativeCallback callback;

    public AndroidCredentialManagerGoogleOAuthProvider(string webClientId)
    {
        if (string.IsNullOrWhiteSpace(webClientId) ||
            !webClientId.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal))
            throw new ArgumentException("A valid Google Web Client ID is required.", nameof(webClientId));
        this.webClientId = webClientId.Trim();
    }

    public Task<PendingGoogleIdentity> SignInAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if UNITY_ANDROID && !UNITY_EDITOR
        lock (sync)
        {
            if (pending != null)
                throw new InvalidOperationException("A Google sign-in request is already active.");

            TaskCompletionSource<PendingGoogleIdentity> source =
                new TaskCompletionSource<PendingGoogleIdentity>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            pending = source;
            callback = new NativeCallback(CompleteSuccess, CompleteFailure, CompleteCancellation);
            Task<PendingGoogleIdentity> task = source.Task;
            cancellationRegistration = cancellationToken.Register(CancelPendingSignIn);
            if (task.IsCanceled) return task;

            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                       unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass(BridgeClassName))
                {
                    bridge.CallStatic("signIn", activity, webClientId, callback);
                }
            }
            catch (Exception exception)
            {
                CompleteException(new AuthException(
                    "GOOGLE_PROVIDER_UNAVAILABLE",
                    "Google sign-in is unavailable on this device.",
                    false,
                    0,
                    null,
                    exception));
            }

            return task;
        }
#else
        return Task.FromException<PendingGoogleIdentity>(new AuthException(
            "GOOGLE_PROVIDER_UNAVAILABLE",
            "Google sign-in is only available in an Android player build."));
#endif
    }

    public Task ClearCredentialStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity =
                   unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass(BridgeClassName))
            {
                bridge.CallStatic("clearCredentialState", activity);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Unable to clear Google Credential Manager state: " + exception.Message);
        }
#endif
        return Task.CompletedTask;
    }

    public void CancelPendingSignIn()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var bridge = new AndroidJavaClass(BridgeClassName))
                bridge.CallStatic("cancelActiveRequest");
        }
        catch (Exception)
        {
            // Cancellation remains best-effort if the Android activity is already gone.
        }
#endif
        CompleteCancellation();
    }

    private void CompleteSuccess(string json)
    {
        try
        {
            NativeCredential native = JsonUtility.FromJson<NativeCredential>(json);
            if (native == null || string.IsNullOrWhiteSpace(native.id_token))
                throw new AuthException("INVALID_GOOGLE_TOKEN", "Google returned an invalid ID token.");

            GoogleTokenClaims claims = ReadClaims(native.id_token);
            if (claims == null || string.IsNullOrWhiteSpace(claims.sub))
                throw new AuthException("INVALID_GOOGLE_TOKEN", "Google returned an unreadable ID token.");
            string email = !string.IsNullOrWhiteSpace(native.email)
                ? native.email
                : claims?.email;
            if (string.IsNullOrWhiteSpace(email))
                throw new AuthException("INVALID_GOOGLE_TOKEN", "Google account email is unavailable.");

            CompleteResult(new PendingGoogleIdentity
            {
                Subject = claims?.sub,
                Email = email,
                EmailVerified = claims.email_verified,
                PictureUrl = !string.IsNullOrWhiteSpace(native.picture_url)
                    ? native.picture_url
                    : claims?.picture,
                IdToken = native.id_token,
                AvatarSprite = null,
                OwnsAvatarSprite = false
            });
        }
        catch (Exception exception)
        {
            CompleteException(exception is AuthException
                ? exception
                : new AuthException(
                    "INVALID_GOOGLE_TOKEN",
                    "Google returned an unreadable credential.",
                    false,
                    0,
                    null,
                    exception));
        }
    }

    private void CompleteFailure(string code, string message)
    {
        CompleteException(new AuthException(
            string.IsNullOrWhiteSpace(code) ? "GOOGLE_SIGN_IN_FAILED" : code,
            string.IsNullOrWhiteSpace(message)
                ? "Google sign-in failed. Please try again."
                : message,
            string.Equals(code, "NETWORK_ERROR", StringComparison.Ordinal)));
    }

    private void CompleteCancellation()
    {
        TaskCompletionSource<PendingGoogleIdentity> source = TakePending();
        source?.TrySetCanceled();
    }

    private void CompleteResult(PendingGoogleIdentity identity)
    {
        TaskCompletionSource<PendingGoogleIdentity> source = TakePending();
        source?.TrySetResult(identity);
    }

    private void CompleteException(Exception exception)
    {
        TaskCompletionSource<PendingGoogleIdentity> source = TakePending();
        source?.TrySetException(exception);
    }

    private TaskCompletionSource<PendingGoogleIdentity> TakePending()
    {
        lock (sync)
        {
            TaskCompletionSource<PendingGoogleIdentity> source = pending;
            pending = null;
            cancellationRegistration.Dispose();
            NativeCallback retainedCallback = callback;
            callback = null;
            GC.KeepAlive(retainedCallback);
            return source;
        }
    }

    private static GoogleTokenClaims ReadClaims(string idToken)
    {
        string[] parts = idToken.Split('.');
        if (parts.Length != 3) return null;
        string payload = parts[1].Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }
        try
        {
            return JsonUtility.FromJson<GoogleTokenClaims>(
                Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
