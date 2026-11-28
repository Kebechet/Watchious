using Watchious.AppIcon;

// dotnet Watchious.AppIcon.dll <settings file>: draws the icon that the settings file (key=value lines, written by
// Watchious.Sdk.AppIcon.targets) describes. A file and not arguments, so that colors (#), sizes (%) and paths reach it
// unchanged through cmd.exe and sh alike. A problem is printed in MSBuild's error format, which the build shows as an
// error of the app's project.
string? origin = null;
try
{
    if (args.Length != 1)
    {
        throw new AppIconException("give the settings file, and only it.");
    }

    var settingsFile = SettingsFile.Read(args[0]);
    origin = settingsFile.Get("origin");
    var output = settingsFile.Get("output") ?? throw new AppIconException("the settings file names no output folder.");
    var foreground = settingsFile.Get("foreground") ?? throw new AppIconException("the item has no Include, the picture.");
    var size = settingsFile.Get("foreground-size");
    var tint = settingsFile.Get("foreground-tint-color");
    var backgroundColor = settingsFile.Get("background-color");
    var settings = new AppIconSettings(
        foreground,
        size is null ? AppIconSettings.DefaultForegroundSize : AppIconSettings.ParseSize(size),
        tint is null ? null : AppIconSettings.ParseColor(tint, "ForegroundTintColor"),
        backgroundColor is null ? AppIconSettings.DefaultBackgroundColor : AppIconSettings.ParseColor(backgroundColor, "BackgroundColor"),
        settingsFile.Get("background-file"));

    var platform = settingsFile.Get("platform");
    switch (platform)
    {
        case "watchos":
            AppIconRenderer.WriteWatchOS(settings, output);
            break;
        case "wearos":
            AppIconRenderer.WriteWearOS(settings, output);
            break;
        default:
            throw new AppIconException($"'{platform}' isn't a platform: use watchos or wearos.");
    }

    return 0;
}
catch (AppIconException exception)
{
    Console.WriteLine($"{origin ?? "Watchious.AppIcon"} : error WATCHIOUS006 : WatchiousAppIcon: {exception.Message}");
    return 1;
}
catch (Exception exception)
{
    Console.WriteLine($"{origin ?? "Watchious.AppIcon"} : error WATCHIOUS006 : WatchiousAppIcon: the icon couldn't be drawn: {exception.GetType().Name}: {exception.Message}");
    return 1;
}

internal sealed class SettingsFile
{
    private readonly Dictionary<string, string> _values = [];

    public static SettingsFile Read(string path)
    {
        var file = new SettingsFile();
        foreach (var line in File.ReadAllLines(path))
        {
            var separator = line.IndexOf('=');
            if (separator > 0)
            {
                file._values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        return file;
    }

    public string? Get(string key)
    {
        var hasValue = _values.TryGetValue(key, out var value);
        return hasValue && !string.IsNullOrWhiteSpace(value) ? value : null;
    }
}
