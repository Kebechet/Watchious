namespace Watchious.Essentials;

// Small values kept between launches: SharedPreferences on Wear OS, UserDefaults on watchOS, like MAUI's
// Preferences.
public interface IPreferences
{
    bool ContainsKey(string key);

    void Remove(string key);

    void Clear();

    string? Get(string key, string? defaultValue);

    bool Get(string key, bool defaultValue);

    int Get(string key, int defaultValue);

    long Get(string key, long defaultValue);

    double Get(string key, double defaultValue);

    void Set(string key, string? value);

    void Set(string key, bool value);

    void Set(string key, int value);

    void Set(string key, long value);

    void Set(string key, double value);
}
