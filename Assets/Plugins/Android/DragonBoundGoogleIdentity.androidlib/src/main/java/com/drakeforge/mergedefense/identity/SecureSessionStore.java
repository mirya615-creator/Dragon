package com.drakeforge.mergedefense.identity;

import android.content.Context;
import android.content.SharedPreferences;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;

import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import java.security.MessageDigest;

import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/** Stores the Drakeforge session encrypted with an app-private Android Keystore key. */
public final class SecureSessionStore {
    private static final String KEYSTORE = "AndroidKeyStore";
    private static final String PREFERENCES = "dragonbound_secure_auth";
    private static final String TRANSFORMATION = "AES/GCM/NoPadding";

    private SecureSessionStore() {
    }

    public static boolean write(Context context, String storageKey, String value) {
        try {
            Cipher cipher = Cipher.getInstance(TRANSFORMATION);
            cipher.init(Cipher.ENCRYPT_MODE, getOrCreateKey(alias(storageKey)));
            byte[] encrypted = cipher.doFinal(value.getBytes(StandardCharsets.UTF_8));
            String payload = Base64.encodeToString(cipher.getIV(), Base64.NO_WRAP) + ":" +
                    Base64.encodeToString(encrypted, Base64.NO_WRAP);
            return preferences(context).edit().putString(entry(storageKey), payload).commit();
        } catch (Exception ignored) {
            return false;
        }
    }

    public static String read(Context context, String storageKey) {
        String payload = preferences(context).getString(entry(storageKey), null);
        if (payload == null || payload.isEmpty()) return null;

        try {
            String[] parts = payload.split(":", 2);
            if (parts.length != 2) throw new IllegalArgumentException("Invalid encrypted session.");
            byte[] iv = Base64.decode(parts[0], Base64.NO_WRAP);
            byte[] encrypted = Base64.decode(parts[1], Base64.NO_WRAP);
            Cipher cipher = Cipher.getInstance(TRANSFORMATION);
            cipher.init(
                    Cipher.DECRYPT_MODE,
                    getOrCreateKey(alias(storageKey)),
                    new GCMParameterSpec(128, iv));
            return new String(cipher.doFinal(encrypted), StandardCharsets.UTF_8);
        } catch (Exception ignored) {
            preferences(context).edit().remove(entry(storageKey)).apply();
            return null;
        }
    }

    public static void delete(Context context, String storageKey) {
        preferences(context).edit().remove(entry(storageKey)).commit();
    }

    private static SecretKey getOrCreateKey(String alias) throws Exception {
        KeyStore keyStore = KeyStore.getInstance(KEYSTORE);
        keyStore.load(null);
        java.security.Key existing = keyStore.getKey(alias, null);
        if (existing instanceof SecretKey) return (SecretKey) existing;

        KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, KEYSTORE);
        generator.init(new KeyGenParameterSpec.Builder(
                alias,
                KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setRandomizedEncryptionRequired(true)
                .build());
        return generator.generateKey();
    }

    private static SharedPreferences preferences(Context context) {
        return context.getApplicationContext().getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE);
    }

    private static String alias(String storageKey) throws Exception {
        return "dragonbound.auth." + digest(storageKey);
    }

    private static String entry(String storageKey) {
        try {
            return digest(storageKey);
        } catch (Exception ignored) {
            return Integer.toHexString(storageKey.hashCode());
        }
    }

    private static String digest(String value) throws Exception {
        byte[] bytes = MessageDigest.getInstance("SHA-256").digest(
                value.getBytes(StandardCharsets.UTF_8));
        StringBuilder result = new StringBuilder(bytes.length * 2);
        for (byte item : bytes) result.append(String.format("%02x", item & 0xff));
        return result.toString();
    }
}
