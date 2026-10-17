package com.geuneda.core;

import android.content.Context;
import android.content.SharedPreferences;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.security.GeneralSecurityException;
import java.security.KeyStore;

import javax.crypto.AEADBadTagException;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/**
 * Geuneda.Core 보안 저장소의 Android 구현(AndroidKeystoreStore.cs). API 23 이상에서 쓴다.
 * 값은 AndroidKeyStore 의 AES-256-GCM 키로 암호화해 앱 전용 SharedPreferences 에 Base64(IV + 암호문 + 태그)로 둔다.
 * 저장 키를 인증 데이터(AAD)로 넣어, 다른 항목의 암호문을 옮겨 넣으면 태그 검사에서 걸린다.
 * 키는 기기 밖으로 나가지 않으므로, 백업에서 복원되었거나 키가 다시 만들어져 복호화할 수 없는 값은 지우고 없는 것으로 본다.
 */
public final class GeunedaSecureStore {
    private static final String KEYSTORE = "AndroidKeyStore";
    private static final String KEY_ALIAS = "geuneda.core.securestore";
    private static final String PREFERENCES = "geuneda.core.securestore";
    private static final String TRANSFORMATION = "AES/GCM/NoPadding";
    private static final int IV_LENGTH = 12;
    private static final int TAG_LENGTH_BITS = 128;

    private GeunedaSecureStore() {
    }

    /** 값을 돌려준다. 없거나 복호화할 수 없는 값(지운다)이면 null 이고, 그 밖의 실패는 예외다. */
    public static synchronized String read(Context context, String key) throws GeneralSecurityException, IOException {
        SharedPreferences preferences = preferences(context);
        String stored = preferences.getString(key, null);

        if (stored == null) {
            return null;
        }

        SecretKey secretKey = findKey();
        byte[] data = decode(stored);

        if (secretKey == null || data == null) {
            remove(preferences, key);
            return null;
        }

        Cipher cipher = Cipher.getInstance(TRANSFORMATION);
        cipher.init(Cipher.DECRYPT_MODE, secretKey, new GCMParameterSpec(TAG_LENGTH_BITS, data, 0, IV_LENGTH));
        cipher.updateAAD(key.getBytes(StandardCharsets.UTF_8));

        try {
            byte[] plain = cipher.doFinal(data, IV_LENGTH, data.length - IV_LENGTH);
            return new String(plain, StandardCharsets.UTF_8);
        } catch (AEADBadTagException e) {
            // 지금 키로 암호화하지 않았거나 다른 항목에서 옮겨진 값
            remove(preferences, key);
            return null;
        }
    }

    /** 값을 암호화해 추가하거나 덮어쓴다. 실패는 예외다. */
    public static synchronized void write(Context context, String key, String value) throws GeneralSecurityException, IOException {
        Cipher cipher = Cipher.getInstance(TRANSFORMATION);
        cipher.init(Cipher.ENCRYPT_MODE, getOrCreateKey());
        cipher.updateAAD(key.getBytes(StandardCharsets.UTF_8));

        byte[] iv = cipher.getIV();

        if (iv == null || iv.length != IV_LENGTH) {
            throw new GeneralSecurityException("Unexpected GCM IV length");
        }

        byte[] encrypted = cipher.doFinal(value.getBytes(StandardCharsets.UTF_8));
        byte[] data = new byte[IV_LENGTH + encrypted.length];
        System.arraycopy(iv, 0, data, 0, IV_LENGTH);
        System.arraycopy(encrypted, 0, data, IV_LENGTH, encrypted.length);

        SharedPreferences preferences = preferences(context);
        commit(preferences, key, preferences.edit().putString(key, Base64.encodeToString(data, Base64.NO_WRAP)));
    }

    /** 값을 지운다. 없어도 성공이고, 실패는 예외다. */
    public static synchronized void delete(Context context, String key) throws IOException {
        remove(preferences(context), key);
    }

    private static SharedPreferences preferences(Context context) {
        return context.getApplicationContext().getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE);
    }

    private static SecretKey findKey() throws GeneralSecurityException, IOException {
        KeyStore keyStore = KeyStore.getInstance(KEYSTORE);
        keyStore.load(null);
        return (SecretKey) keyStore.getKey(KEY_ALIAS, null);
    }

    private static SecretKey getOrCreateKey() throws GeneralSecurityException, IOException {
        SecretKey existing = findKey();

        if (existing != null) {
            return existing;
        }

        KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, KEYSTORE);
        generator.init(new KeyGenParameterSpec.Builder(KEY_ALIAS, KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256)
                .build());
        return generator.generateKey();
    }

    /** 형식이 맞지 않으면 null */
    private static byte[] decode(String stored) {
        try {
            byte[] data = Base64.decode(stored, Base64.NO_WRAP);
            return data.length >= IV_LENGTH + TAG_LENGTH_BITS / 8 ? data : null;
        } catch (IllegalArgumentException e) {
            return null;
        }
    }

    private static void remove(SharedPreferences preferences, String key) throws IOException {
        commit(preferences, key, preferences.edit().remove(key));
    }

    /**
     * 바꾼 항목을 디스크에 쓴다. SharedPreferences.commit 은 디스크 쓰기가 실패해도 메모리 값을 먼저 바꾸므로,
     * 실패하면 그 항목의 메모리 값을 원래대로 되돌리고 예외를 던진다. 되돌리지 않으면 같은 실행의 읽기가 다음 실행에 없을 값을 돌려준다.
     */
    private static void commit(SharedPreferences preferences, String key, SharedPreferences.Editor editor) throws IOException {
        String previous = preferences.getString(key, null);

        if (editor.commit()) {
            return;
        }

        SharedPreferences.Editor rollback = preferences.edit();

        if (previous == null) {
            rollback.remove(key);
        } else {
            rollback.putString(key, previous);
        }

        // apply 는 메모리를 바로 바꾸고 디스크 쓰기만 뒤로 미룬다. 디스크에는 이미 원래 값이 있다
        rollback.apply();
        throw new IOException("Could not write secure store preferences");
    }
}
