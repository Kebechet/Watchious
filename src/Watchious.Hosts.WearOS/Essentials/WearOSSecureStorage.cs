using System.Text;
using Android.Content;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using Watchious.Essentials;

namespace Watchious.Hosts.WearOS.Essentials;

// Each value encrypted with AES-GCM under a key that never leaves the Android Keystore, and kept in a private
// SharedPreferences file as base64 of [IV length][IV][ciphertext]. A value that no longer decrypts (the key was
// removed, as when the app's data is restored onto another watch) reads as missing.
internal sealed class WearOSSecureStorage(Context context) : ISecureStorage
{
    private const string KeyStoreName = "AndroidKeyStore";
    private const string KeyAlias = "watchious_secure_storage";
    private const string Transformation = "AES/GCM/NoPadding";
    private const int TagLengthBits = 128;

    private readonly ISharedPreferences _preferences = context.GetSharedPreferences("watchious_secure", FileCreationMode.Private)!;
    private readonly Lock _lock = new();

    public Task<string?> GetAsync(string key)
    {
        lock (_lock)
        {
            var stored = _preferences.GetString(key, null);
            return Task.FromResult(stored is null ? null : Decrypt(stored));
        }
    }

    public Task SetAsync(string key, string value)
    {
        lock (_lock)
        {
            using var editor = _preferences.Edit()!;
            editor.PutString(key, Encrypt(value))!.Apply();
        }

        return Task.CompletedTask;
    }

    public bool Remove(string key)
    {
        lock (_lock)
        {
            if (!_preferences.Contains(key))
            {
                return false;
            }

            using var editor = _preferences.Edit()!;
            editor.Remove(key)!.Apply();
            return true;
        }
    }

    public void RemoveAll()
    {
        lock (_lock)
        {
            using var editor = _preferences.Edit()!;
            editor.Clear()!.Apply();
        }
    }

    private static string Encrypt(string value)
    {
        using var cipher = Cipher.GetInstance(Transformation)!;
        cipher.Init(CipherMode.EncryptMode, GetOrCreateKey());
        var iv = cipher.GetIV()!;
        var encrypted = cipher.DoFinal(Encoding.UTF8.GetBytes(value))!;
        return Convert.ToBase64String([(byte)iv.Length, .. iv, .. encrypted]);
    }

    private static string? Decrypt(string stored)
    {
        try
        {
            var bytes = Convert.FromBase64String(stored);
            var ivLength = bytes[0];
            using var cipher = Cipher.GetInstance(Transformation)!;
            cipher.Init(CipherMode.DecryptMode, GetOrCreateKey(), new GCMParameterSpec(TagLengthBits, bytes[1..(1 + ivLength)]));
            return Encoding.UTF8.GetString(cipher.DoFinal(bytes[(1 + ivLength)..])!);
        }
        catch (Exception exception) when (exception is GeneralSecurityException or FormatException or IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static IKey GetOrCreateKey()
    {
        var keyStore = KeyStore.GetInstance(KeyStoreName)!;
        keyStore.Load(null);
        var existing = keyStore.GetKey(KeyAlias, null);
        if (existing is not null)
        {
            return existing;
        }

        var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, KeyStoreName)!;
        using var spec = new KeyGenParameterSpec.Builder(KeyAlias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)!
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)!
            .SetKeySize(256)!
            .Build();
        generator.Init(spec);
        return generator.GenerateKey()!;
    }
}
