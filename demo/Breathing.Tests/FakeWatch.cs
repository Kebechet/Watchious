using Microsoft.Extensions.DependencyInjection;
using Watchious;
using Watchious.Essentials;
using Watchious.Hosting;

namespace Breathing.Tests;

// A watch for tests: the host the app renders to, counting the batches it sends, and the watch APIs the app uses.
public sealed class FakeWatch : IWatchiousHost
{
    private int _batchCount;

    public int BatchCount => Volatile.Read(ref _batchCount);

    public List<Exception> Errors { get; } = [];

    public FakePreferences Preferences { get; } = new();

    public FakeHaptics Haptics { get; } = new();

    public FakeDeviceDisplay DeviceDisplay { get; } = new();

    public FakeAppLifecycle AppLifecycle { get; } = new();

    // Starts the app as a watch's host does: its watch APIs registered before the app's own services.
    public async Task<WatchiousApp> StartAsync()
    {
        WatchiousApp app;
        using (WatchiousPlatform.ConfigureServices(AddServices))
        {
            app = WatchiousProgram.CreateWatchiousApp();
        }

        await app.StartAsync(this);
        return app;
    }

    public Task ApplyPatches(ReadOnlySpan<byte> patches)
    {
        Interlocked.Increment(ref _batchCount);
        return Task.CompletedTask;
    }

    public void ReportError(Exception exception)
    {
        lock (Errors)
        {
            Errors.Add(exception);
        }
    }

    private void AddServices(IServiceCollection services)
    {
        services.AddSingleton<IPreferences>(Preferences);
        services.AddSingleton<IHaptics>(Haptics);
        services.AddSingleton<IDeviceDisplay>(DeviceDisplay);
        services.AddSingleton<IAppLifecycle>(AppLifecycle);
    }
}

public sealed class FakePreferences : IPreferences
{
    private readonly Dictionary<string, object?> _values = [];

    public bool ContainsKey(string key) => _values.ContainsKey(key);

    public void Remove(string key) => _values.Remove(key);

    public void Clear() => _values.Clear();

    public string? Get(string key, string? defaultValue) => _values.TryGetValue(key, out var value) ? (string?)value : defaultValue;

    public bool Get(string key, bool defaultValue) => _values.TryGetValue(key, out var value) ? (bool)value! : defaultValue;

    public int Get(string key, int defaultValue) => _values.TryGetValue(key, out var value) ? (int)value! : defaultValue;

    public long Get(string key, long defaultValue) => _values.TryGetValue(key, out var value) ? (long)value! : defaultValue;

    public double Get(string key, double defaultValue) => _values.TryGetValue(key, out var value) ? (double)value! : defaultValue;

    public void Set(string key, string? value) => _values[key] = value;

    public void Set(string key, bool value) => _values[key] = value;

    public void Set(string key, int value) => _values[key] = value;

    public void Set(string key, long value) => _values[key] = value;

    public void Set(string key, double value) => _values[key] = value;
}

public sealed class FakeHaptics : IHaptics
{
    public List<HapticFeedback> Played { get; } = [];

    public void Play(HapticFeedback feedback)
    {
        lock (Played)
        {
            Played.Add(feedback);
        }
    }
}

public sealed class FakeDeviceDisplay : IDeviceDisplay
{
    public bool KeepScreenOn { get; set; }
}

public sealed class FakeAppLifecycle : IAppLifecycle
{
    private EventHandler<AppStateChangedEventArgs>? _stateChanged;

    public AppState State { get; private set; } = AppState.Active;

    public bool HasSubscribers => _stateChanged is not null;

    public event EventHandler<AppStateChangedEventArgs>? StateChanged
    {
        add => _stateChanged += value;
        remove => _stateChanged -= value;
    }

    public void ChangeTo(AppState state)
    {
        State = state;
        _stateChanged?.Invoke(this, new AppStateChangedEventArgs(state));
    }
}
