using System.Collections.Concurrent;
using System.Globalization;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Speech.Tts;
using Android.Views;
using Watchious.Essentials;
using Locale = Watchious.Essentials.Locale;

namespace Watchious.Hosts.WearOS.Essentials;

internal sealed class WearOSAppInfo(Context context) : IAppInfo
{
    private readonly Lazy<PackageInfo> _package = new(() => ReadPackage(context));

    public string Name => context.ApplicationInfo?.LoadLabel(context.PackageManager!) ?? "";

    public string PackageName => context.PackageName ?? "";

    public string VersionString => _package.Value.VersionName ?? "";

    // "1.2" or "1" from ApplicationDisplayVersion.
    public Version Version
    {
        get
        {
            var text = VersionString;
            return Version.TryParse(text.Contains('.') ? text : text + ".0", out var version) ? version : new Version(0, 0);
        }
    }

    public string BuildString => _package.Value.LongVersionCode.ToString(CultureInfo.InvariantCulture);

    private static PackageInfo ReadPackage(Context context)
    {
        var packageManager = context.PackageManager!;
        var packageName = context.PackageName!;
        return OperatingSystem.IsAndroidVersionAtLeast(33)
            ? packageManager.GetPackageInfo(packageName, PackageManager.PackageInfoFlags.Of(0L))!
            : packageManager.GetPackageInfo(packageName, 0)!;
    }
}

// MAUI's folders on Android: the app's files and cache folders.
internal sealed class WearOSFileSystem(Context context) : IFileSystem
{
    public string CacheDirectory => context.CacheDir!.AbsolutePath;

    public string AppDataDirectory => context.FilesDir!.AbsolutePath;
}

internal sealed class WearOSDeviceDisplay(Activity activity) : IDeviceDisplay
{
    private bool _keepScreenOn;

    public bool KeepScreenOn
    {
        get => _keepScreenOn;
        set
        {
            _keepScreenOn = value;
            activity.RunOnUiThread(() =>
            {
                if (value)
                {
                    activity.Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
                }
                else
                {
                    activity.Window?.ClearFlags(WindowManagerFlags.KeepScreenOn);
                }
            });
        }
    }
}

// Android's TextToSpeech, started on first use. Each text is an utterance, which the engine's progress listener
// completes by its id. Its pitch and language are the engine's, so each text sets them.
internal sealed class WearOSTextToSpeech(Context context) : ITextToSpeech, IDisposable
{
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _utterances = new();
    private Task<TextToSpeech>? _engine;
    private int _lastUtterance;

    public async Task<IEnumerable<Locale>> GetLocalesAsync()
    {
        var engine = await GetEngineAsync();
        var languages = engine.AvailableLanguages ?? [];
        return languages
            .Select(locale => new Locale(locale.Language, string.IsNullOrEmpty(locale.Country) ? null : locale.Country, locale.DisplayName, locale.ToLanguageTag()))
            .ToList();
    }

    public async Task SpeakAsync(string text, SpeechOptions? options = null, CancellationToken cancelToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        cancelToken.ThrowIfCancellationRequested();
        var engine = await GetEngineAsync();
        var locale = options?.Locale;
        engine.SetLanguage(locale is null
            ? Java.Util.Locale.Default
            : Java.Util.Locale.ForLanguageTag(locale.Country is null ? locale.Language : $"{locale.Language}-{locale.Country}"));
        engine.SetPitch(options?.Pitch ?? 1);
        var parameters = new Bundle();
        if (options?.Volume is not null)
        {
            parameters.PutFloat(TextToSpeech.Engine.KeyParamVolume, options.Volume.Value);
        }

        var id = Interlocked.Increment(ref _lastUtterance).ToString(CultureInfo.InvariantCulture);
        var spoken = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _utterances[id] = spoken;
        // Stopping ends whatever the watch is saying, as TextToSpeech stops all of its queue.
        await using var stopping = cancelToken.Register(StopSpeaking);
        if (engine.Speak(text, QueueMode.Add, parameters, id) != OperationResult.Success)
        {
            _utterances.TryRemove(id, out _);
            throw new InvalidOperationException("The text-to-speech engine didn't take the text.");
        }

        await spoken.Task;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_engine is not null && _engine.IsCompletedSuccessfully)
            {
                _engine.Result.Shutdown();
            }
        }
    }

    private Task<TextToSpeech> GetEngineAsync()
    {
        lock (_lock)
        {
            return _engine ??= StartEngineAsync();
        }
    }

    private async Task<TextToSpeech> StartEngineAsync()
    {
        var initialized = new TaskCompletionSource<OperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new TextToSpeech(context, new InitListener(initialized));
        var status = await initialized.Task;
        if (status != OperationResult.Success)
        {
            engine.Shutdown();
            throw new FeatureNotSupportedException("This watch has no text-to-speech engine.");
        }

        engine.SetOnUtteranceProgressListener(new ProgressListener(this));
        return engine;
    }

    private void StopSpeaking()
    {
        var engine = _engine;
        if (engine is not null && engine.IsCompletedSuccessfully)
        {
            engine.Result.Stop();
        }
    }

    private void Finish(string? utteranceId, Exception? error, bool isStopped)
    {
        if (utteranceId is null || !_utterances.TryRemove(utteranceId, out var spoken))
        {
            return;
        }

        if (error is not null)
        {
            spoken.TrySetException(error);
        }
        else if (isStopped)
        {
            spoken.TrySetCanceled();
        }
        else
        {
            spoken.TrySetResult();
        }
    }

    private sealed class InitListener(TaskCompletionSource<OperationResult> initialized) : Java.Lang.Object, TextToSpeech.IOnInitListener
    {
        public void OnInit(OperationResult status)
        {
            initialized.TrySetResult(status);
        }
    }

    private sealed class ProgressListener(WearOSTextToSpeech owner) : UtteranceProgressListener
    {
        public override void OnStart(string? utteranceId)
        {
        }

        public override void OnDone(string? utteranceId)
        {
            owner.Finish(utteranceId, error: null, isStopped: false);
        }

        [Obsolete("Android calls OnError(string, int) since API 21; it stays abstract.")]
        public override void OnError(string? utteranceId)
        {
            owner.Finish(utteranceId, new InvalidOperationException("The text-to-speech engine failed."), isStopped: false);
        }

        public override void OnError(string? utteranceId, TextToSpeechError errorCode)
        {
            owner.Finish(utteranceId, new InvalidOperationException($"The text-to-speech engine failed ({errorCode})."), isStopped: false);
        }

        public override void OnStop(string? utteranceId, bool interrupted)
        {
            owner.Finish(utteranceId, error: null, isStopped: true);
        }
    }
}
