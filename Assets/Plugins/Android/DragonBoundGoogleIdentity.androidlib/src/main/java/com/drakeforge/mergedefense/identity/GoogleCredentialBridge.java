package com.drakeforge.mergedefense.identity;

import android.app.Activity;
import android.content.MutableContextWrapper;
import android.net.Uri;
import android.os.CancellationSignal;

import androidx.annotation.NonNull;
import androidx.credentials.ClearCredentialStateRequest;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.ClearCredentialException;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.NoCredentialException;

import com.google.android.libraries.identity.googleid.GetGoogleIdOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

import org.json.JSONObject;

import java.util.concurrent.Executor;

/** Thin Credential Manager bridge used by the Unity C# authentication boundary. */
public final class GoogleCredentialBridge {
    public interface Callback {
        void onSuccess(String json);
        void onError(String code, String message);
        void onCancelled();
    }

    private static final Object LOCK = new Object();
    private static CancellationSignal activeCancellation;

    private GoogleCredentialBridge() {
    }

    public static void signIn(
            @NonNull Activity activity,
            @NonNull String serverClientId,
            @NonNull Callback callback) {
        if (serverClientId.trim().isEmpty()) {
            callback.onError("GOOGLE_CLIENT_ID_MISSING", "Google Web Client ID is missing.");
            return;
        }

        cancelActiveRequest();
        CancellationSignal cancellation = new CancellationSignal();
        synchronized (LOCK) {
            activeCancellation = cancellation;
        }
        requestCredential(activity, serverClientId.trim(), true, cancellation, callback);
    }

    public static void cancelActiveRequest() {
        synchronized (LOCK) {
            if (activeCancellation != null) {
                activeCancellation.cancel();
                activeCancellation = null;
            }
        }
    }

    public static void clearCredentialState(@NonNull Activity activity) {
        CredentialManager manager = CredentialManager.create(activity);
        manager.clearCredentialStateAsync(
                new ClearCredentialStateRequest(),
                null,
                mainExecutor(activity),
                new CredentialManagerCallback<Void, ClearCredentialException>() {
                    @Override
                    public void onResult(Void result) {
                    }

                    @Override
                    public void onError(@NonNull ClearCredentialException error) {
                    }
                });
    }

    private static void requestCredential(
            @NonNull Activity activity,
            @NonNull String serverClientId,
            boolean authorizedOnly,
            @NonNull CancellationSignal cancellation,
            @NonNull Callback callback) {
        GetGoogleIdOption googleOption = new GetGoogleIdOption.Builder()
                .setFilterByAuthorizedAccounts(authorizedOnly)
                .setAutoSelectEnabled(false)
                .setServerClientId(serverClientId)
                .build();
        GetCredentialRequest request = new GetCredentialRequest.Builder()
                .addCredentialOption(googleOption)
                .build();

        CredentialManager manager = CredentialManager.create(activity);
        MutableContextWrapper context = new MutableContextWrapper(activity);
        manager.getCredentialAsync(
                context,
                request,
                cancellation,
                mainExecutor(activity),
                new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                    @Override
                    public void onResult(@NonNull GetCredentialResponse result) {
                        clearActive(cancellation);
                        handleCredential(result.getCredential(), callback);
                    }

                    @Override
                    public void onError(@NonNull GetCredentialException error) {
                        if (error instanceof NoCredentialException && authorizedOnly &&
                                !cancellation.isCanceled()) {
                            requestCredential(
                                    activity,
                                    serverClientId,
                                    false,
                                    cancellation,
                                    callback);
                            return;
                        }

                        clearActive(cancellation);
                        if (error instanceof GetCredentialCancellationException ||
                                cancellation.isCanceled()) {
                            callback.onCancelled();
                        } else if (error instanceof NoCredentialException) {
                            callback.onError(
                                    "NO_GOOGLE_ACCOUNT",
                                    "No Google account is available on this device.");
                        } else {
                            callback.onError(
                                    "GOOGLE_CREDENTIAL_ERROR",
                                    safeMessage(error, "Unable to get a Google credential."));
                        }
                    }
                });
    }

    private static void handleCredential(
            @NonNull Credential credential,
            @NonNull Callback callback) {
        if (!(credential instanceof CustomCredential) ||
                !GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(
                        credential.getType())) {
            callback.onError(
                    "UNEXPECTED_CREDENTIAL",
                    "Google returned an unsupported credential type.");
            return;
        }

        try {
            GoogleIdTokenCredential google = GoogleIdTokenCredential.createFrom(
                    ((CustomCredential) credential).getData());
            JSONObject json = new JSONObject();
            json.put("id_token", google.getIdToken());
            json.put("email", google.getId());
            json.put("display_name", nullable(google.getDisplayName()));
            Uri picture = google.getProfilePictureUri();
            json.put("picture_url", picture == null ? "" : picture.toString());
            callback.onSuccess(json.toString());
       } catch (Exception error) {
            callback.onError(
                    "INVALID_GOOGLE_TOKEN",
                    "Google returned an invalid ID token credential.");
        }
    }

    private static Executor mainExecutor(@NonNull Activity activity) {
        return command -> activity.runOnUiThread(command);
    }

    private static void clearActive(CancellationSignal expected) {
        synchronized (LOCK) {
            if (activeCancellation == expected) activeCancellation = null;
        }
    }

    private static String nullable(String value) {
        return value == null ? "" : value;
    }

    private static String safeMessage(Exception error, String fallback) {
        String message = error.getMessage();
        return message == null || message.trim().isEmpty() ? fallback : message;
    }
}
