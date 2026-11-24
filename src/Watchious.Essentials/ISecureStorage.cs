namespace Watchious.Essentials;

// MAUI Essentials' ISecureStorage: small secrets such as tokens, encrypted by the platform (the Keychain on watchOS, a
// key in the Android Keystore on Wear OS). A value that can no longer be decrypted reads as missing.
public interface ISecureStorage
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    bool Remove(string key);

    void RemoveAll();
}
