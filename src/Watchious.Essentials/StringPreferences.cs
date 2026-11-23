using System.Globalization;

namespace Watchious.Essentials;

// Stores every value as a culture-invariant string, so a value written in one language reads back in another, and
// both platforms keep the same format.
public abstract class StringPreferences : IPreferences
{
    public abstract bool ContainsKey(string key);

    public abstract void Remove(string key);

    public abstract void Clear();

    public string? Get(string key, string? defaultValue)
    {
        return GetString(key) ?? defaultValue;
    }

    public bool Get(string key, bool defaultValue)
    {
        return bool.TryParse(GetString(key), out var value) ? value : defaultValue;
    }

    public int Get(string key, int defaultValue)
    {
        return int.TryParse(GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : defaultValue;
    }

    public long Get(string key, long defaultValue)
    {
        return long.TryParse(GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : defaultValue;
    }

    public double Get(string key, double defaultValue)
    {
        return double.TryParse(GetString(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : defaultValue;
    }

    // Setting null removes the key, as in MAUI.
    public void Set(string key, string? value)
    {
        if (value is null)
        {
            Remove(key);
            return;
        }

        SetString(key, value);
    }

    public void Set(string key, bool value)
    {
        SetString(key, value ? "true" : "false");
    }

    public void Set(string key, int value)
    {
        SetString(key, value.ToString(CultureInfo.InvariantCulture));
    }

    public void Set(string key, long value)
    {
        SetString(key, value.ToString(CultureInfo.InvariantCulture));
    }

    public void Set(string key, double value)
    {
        SetString(key, value.ToString("R", CultureInfo.InvariantCulture));
    }

    protected abstract string? GetString(string key);

    protected abstract void SetString(string key, string value);
}
