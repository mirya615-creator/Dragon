package com.drakeforge.mergedefense.googleauth;

import android.app.Activity;
import android.os.CancellationSignal;
import android.util.Log;

import androidx.core.content.ContextCompat;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialCancelationException;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.GetCredentialInterruptedException;
import androidx.credentials.exceptions.GetCredentialProviderConfigurationException;
import androidx.credentials.exceptions.GetCredentialUnknownException;
import androidx.credentials.exceptions.GetCredentialUnsupportedException;
import androidx.credentials.exceptions.NoCredentialException;

import com.google.android.libraries.identity.googleid.GetGoogleIdOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import com.google.android.libraries.identity.googleid.GoogleIdTokenParsingException;
import com.unity3d.player.UnityPlayer;

import org.json.JSONException;
import org.json.JSONObject;

import java.util.concurrent.Executor;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * Credential Manager based Google sign-in bridge for Unity.
 *
 * Called from C# via AndroidJavaClass.CallStatic(...). Results are pushed back with
 * UnityPlayer.UnitySendMessage(gameObject, method, jsonString).
 */
public final class GoogleSignInBridge {

    private static final String TAG = "DBGoogleAuth";

    private static CancellationSignal pendingSignal;
    private static final AtomicInteger generation = new AtomicInteger(0);

    private GoogleSignInBridge() { }

    /**
     * Performs ONE credential query. The two-step strategy (authorized accounts -> all accounts)
     * is driven from the C# side, so this method stays single-shot.
     *
     * @param serverClientId the WEB client id (not the Android client id)
     */
    public static void signIn(String callbackGameObject,
                              String callbackMethod,
                              String requestId,
                              String serverClientId,
                              boolean filterByAuthorizedAccounts) {
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            emitError(callbackGameObject, callbackMethod, requestId,
                    "INTERNAL", 0, "Unity activity is not available.");
            return;
        }
        if (serverClientId == null || serverClientId.length() == 0) {
            emitError(callbackGameObject, callbackMethod, requestId,
                    "INTERNAL", 0, "serverClientId is empty.");
            return;
        }

        cancel();                                   // invalidate any previous attempt
        final int expectedGeneration = generation.get();

        CredentialManager credentialManager = CredentialManager.create(activity);
        Executor mainExecutor = ContextCompat.getMainExecutor(activity);

        GetGoogleIdOption googleIdOption = new GetGoogleIdOption.Builder()
                .setServerClientId(serverClientId)
                .setFilterByAuthorizedAccounts(filterByAuthorizedAccounts)
                .setAutoSelectEnabled(filterByAuthorizedAccounts)   // must be false when not filtering
                .build();

        GetCredentialRequest request = new GetCredentialRequest.Builder()
                .addCredentialOption(googleIdOption)
                .build();

        CancellationSignal signal = new CancellationSignal();
        pendingSignal = signal;

        credentialManager.getCredentialAsync(
                activity,
                request,
                signal,
                mainExecutor,
                new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                    @Override
                    public void onResult(GetCredentialResponse result) {
                        if (expectedGeneration != generation.get()) {
                            Log.d(TAG, "Discarded late result.");
                            return;
                        }
                        pendingSignal = null;
                        handleSuccess(callbackGameObject, callbackMethod, requestId, result);
                    }

                    @Override
                    public void onError(GetCredentialException e) {
                        if (expectedGeneration != generation.get()) {
                            Log.d(TAG, "Discarded late error.");
                            return;
                        }
                        pendingSignal = null;
                        emitError(callbackGameObject, callbackMethod, requestId,
                                mapError(e), 0, describe(e));
                    }
                });
    }

    /** Cancels the in-flight request, if any. Safe to call repeatedly. */
    public static void cancel() {
        generation.incrementAndGet();
        if (pendingSignal != null) {
            try {
                pendingSignal.cancel();
            } catch (Throwable ignored) {
                // nothing actionable
            }
            pendingSignal = null;
        }
    }

    private static void handleSuccess(String gameObject, String method, String requestId,
                                      GetCredentialResponse response) {
        Credential credential = response.getCredential();
        String type = credential.getType();

        if (!(credential instanceof CustomCredential)
                || !GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(type)) {
            emitError(gameObject, method, requestId,
                    "NO_CREDENTIAL", 0, "Unsupported credential type.");
            return;
        }

        try {
            GoogleIdTokenCredential googleCredential =
                    GoogleIdTokenCredential.createFrom(((CustomCredential) credential).getData());

            JSONObject payload = new JSONObject();
            payload.put("idToken", googleCredential.getIdToken());
            payload.put("subject", googleCredential.getId());
            payload.put("displayName", googleCredential.getDisplayName());
            payload.put("pictureUrl", googleCredential.getProfilePictureUri() == null
                    ? "" : googleCredential.getProfilePictureUri().toString());

            emitSuccess(gameObject, method, requestId, payload);
        } catch (GoogleIdTokenParsingException e) {
            emitError(gameObject, method, requestId,
                    "TOKEN_PARSE_FAILED", 0, "Failed to parse Google ID token credential.");
        } catch (JSONException e) {
            emitError(gameObject, method, requestId,
                    "INTERNAL", 0, "Failed to serialize sign-in result.");
        }
    }

    private static String mapError(GetCredentialException e) {
        if (e instanceof GetCredentialCancelationException) return "USER_CANCELED";
        if (e instanceof NoCredentialException) return "NO_CREDENTIAL";
        if (e instanceof GetCredentialInterruptedException) return "INTERRUPTED";
        if (e instanceof GetCredentialUnsupportedException) return "PROVIDER_UNAVAILABLE";
        if (e instanceof GetCredentialProviderConfigurationException) return "PROVIDER_UNAVAILABLE";
        if (e instanceof GetCredentialUnknownException) return "UNKNOWN";
        return "UNKNOWN";
    }

    /** Never includes the ID token — used for both logs and the Unity-side message. */
    private static String describe(GetCredentialException e) {
        return e == null || e.getClass() == null ? "Unknown error." : e.getClass().getName();
    }

    private static void emitSuccess(String gameObject, String method, String requestId,
                                    JSONObject payload) throws JSONException {
        JSONObject json = new JSONObject(payload.toString());
        json.put("requestId", requestId);
        json.put("status", "ok");
        json.put("errorType", "");
        json.put("errorCode", 0);
        json.put("message", "");
        UnityPlayer.UnitySendMessage(gameObject, method, json.toString());
    }

    private static void emitError(String gameObject, String method, String requestId,
                                  String errorType, int errorCode, String message) {
        try {
            JSONObject json = new JSONObject();
            json.put("requestId", requestId);
            json.put("status", "error");
            json.put("errorType", errorType);
            json.put("errorCode", errorCode);
            json.put("message", message == null ? "" : message);
            UnityPlayer.UnitySendMessage(gameObject, method, json.toString());
        } catch (JSONException ignored) {
            // nothing actionable
        }
        Log.w(TAG, "signIn failed: " + (errorType == null ? "?" : errorType));
    }
}
