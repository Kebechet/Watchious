using Android.Content;
using Watchious.Essentials;

namespace Watchious.Hosts.WearOS.Essentials;

internal sealed class WearOSPreferences(Context context) : StringPreferences
{
    // Watchious's own file, so Clear() leaves other libraries' preferences alone.
    private const string FileName = "watchious";

    private readonly ISharedPreferences _preferences = context.GetSharedPreferences(FileName, FileCreationMode.Private)!;

    public override bool ContainsKey(string key)
    {
        return _preferences.Contains(key);
    }

    public override void Remove(string key)
    {
        Edit(editor => editor.Remove(key));
    }

    public override void Clear()
    {
        Edit(editor => editor.Clear());
    }

    protected override string? GetString(string key)
    {
        return _preferences.GetString(key, null);
    }

    protected override void SetString(string key, string value)
    {
        Edit(editor => editor.PutString(key, value));
    }

    private void Edit(Func<ISharedPreferencesEditor, ISharedPreferencesEditor?> change)
    {
        using var editor = _preferences.Edit()!;
        change(editor);
        editor.Apply();
    }
}
