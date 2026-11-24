using Watchious.Essentials;

namespace Watchious.Hosts.watchOS.Essentials;

internal sealed class WatchOSAppInfo : IAppInfo
{
    public string Name => Read(0);

    public string PackageName => Read(1);

    public string VersionString => Read(2);

    // "1.2" or "1" from CFBundleShortVersionString.
    public Version Version
    {
        get
        {
            var text = VersionString;
            return Version.TryParse(text.Contains('.') ? text : text + ".0", out var version) ? version : new Version(0, 0);
        }
    }

    public string BuildString => Read(3);

    private static string Read(int field)
    {
        return WatchOSNative.TakeString(WatchOSNative.AppString(field)) ?? "";
    }
}

// MAUI's folders on iOS: Library for the app's data and Library/Caches for the cache.
internal sealed class WatchOSFileSystem : IFileSystem
{
    private readonly Lazy<string> _cacheDirectory = new(() => WatchOSNative.TakeString(WatchOSNative.AppString(11)) ?? "");
    private readonly Lazy<string> _appDataDirectory = new(() => WatchOSNative.TakeString(WatchOSNative.AppString(10)) ?? "");

    public string CacheDirectory => _cacheDirectory.Value;

    public string AppDataDirectory => _appDataDirectory.Value;
}

// An extended runtime session (platforms/watchOS/Host/AppServices.swift) keeps the app on screen. It ends by itself when
// its time is up (10 minutes for self-care) or watchOS ends it, so KeepScreenOn reads the session.
internal sealed class WatchOSDeviceDisplay : IDeviceDisplay
{
    private const int NoSessionMode = 2;

    public bool KeepScreenOn
    {
        get => WatchOSNative.DisplayIsKeptOn() == 1;
        set
        {
            if (WatchOSNative.DisplayKeepOn(value ? 1 : 0) == NoSessionMode)
            {
                throw new FeatureNotSupportedException(
                    "KeepScreenOn needs an extended runtime session on watchOS: Platforms/watchOS/Info.plist declares its kind in WKBackgroundModes (self-care, mindfulness or physical-therapy).");
            }
        }
    }
}

// AVSpeechSynthesizer (platforms/watchOS/Host/Speech.swift), which speaks one text after the other.
internal sealed class WatchOSTextToSpeech : ITextToSpeech
{
    private const int Spoken = 1;

    public Task<IEnumerable<Locale>> GetLocalesAsync()
    {
        var voices = WatchOSNative.TakeString(WatchOSNative.SpeechVoices()) ?? "";
        IEnumerable<Locale> locales = voices
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t'))
            .Where(parts => parts.Length == 3)
            .Select(parts => ToLocale(parts[0], parts[1], parts[2]))
            .ToList();
        return Task.FromResult(locales);
    }

    // A locale of GetLocalesAsync names its voice; another one picks the language's voice. Cancelling stops whatever the
    // watch is saying, as AVSpeechSynthesizer stops all of its queue.
    public async Task SpeakAsync(string text, SpeechOptions? options = null, CancellationToken cancelToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        cancelToken.ThrowIfCancellationRequested();
        var locale = options?.Locale;
        var language = locale is null ? null : locale.Country is null ? locale.Language : $"{locale.Language}-{locale.Country}";
        var result = await WatchOSCallbacks.RunAsync(
            token => WatchOSNative.SpeechSpeak(text, locale?.Id, language, options?.Pitch ?? -1, options?.Volume ?? -1, token),
            WatchOSNative.SpeechStop,
            cancelToken);
        if (result != Spoken)
        {
            throw new OperationCanceledException("The speech was stopped.");
        }
    }

    // AVSpeechSynthesisVoice's language is a tag such as "en-US".
    private static Locale ToLocale(string tag, string name, string identifier)
    {
        var separator = tag.IndexOf('-', StringComparison.Ordinal);
        return separator < 0
            ? new Locale(tag, null, name, identifier)
            : new Locale(tag[..separator], tag[(separator + 1)..], name, identifier);
    }
}
