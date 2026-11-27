using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Android.App;
using Android.Util;
using Android.Views;
using Watchious.Hosting;
using Watchious.Hosts.WearOS.Native;

namespace Watchious.Hosts.WearOS;

public sealed class ComposeWatchiousHost : Java.Lang.Object, IWatchiousHost, IWatchiousEventListener
{
    private const string LogTag = "Watchious";

    // The hosts by activity, for WearOSAmbientMode, which reports the always-on screen of its activity to that
    // activity's host (where every page's background turns black).
    private static readonly ConditionalWeakTable<Activity, WatchiousComposeHost> HostsByActivity = new();

    private readonly Activity _activity;
    private readonly WatchiousComposeHost _composeHost;
    private readonly ConcurrentQueue<TaskCompletionSource> _pendingBatches = new();
    private WatchiousApp? _app;

    public ComposeWatchiousHost(Activity activity)
    {
        _activity = activity;
        _composeHost = new WatchiousComposeHost(activity);
        _composeHost.SetEventListener(this);
        HostsByActivity.AddOrUpdate(activity, _composeHost);
    }

    public View View => _composeHost.View;

    public bool HandleBack()
    {
        return _composeHost.HandleBack();
    }

    public Task StartAsync(WatchiousApp app)
    {
        _app = app;
        return app.StartAsync(this);
    }

    // The Kotlin host takes batches from any thread and applies them on the main thread in arrival order.
    public Task ApplyPatches(ReadOnlySpan<byte> patches)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingBatches.Enqueue(applied);
        _composeHost.Render(patches.ToArray());
        return applied.Task;
    }

    public void OnPatchesApplied()
    {
        if (_pendingBatches.TryDequeue(out var applied))
        {
            applied.TrySetResult();
        }
    }

    public void ReportError(Exception exception)
    {
        Log.Error(LogTag, exception.ToString());
    }

    public void OnEvent(long eventHandlerId, string eventName, string? value)
    {
        if (_app is null)
        {
            return;
        }

        _ = DispatchEventAsync(_app, (ulong)eventHandlerId, eventName, value);
    }

    internal static void SetAmbient(Activity activity, bool isAmbient)
    {
        if (HostsByActivity.TryGetValue(activity, out var composeHost))
        {
            composeHost.SetAmbient(isAmbient);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _composeHost.SetEventListener(null);
            HostsByActivity.Remove(_activity);
            _app = null;
        }

        base.Dispose(disposing);
    }

    private async Task DispatchEventAsync(WatchiousApp app, ulong eventHandlerId, string eventName, string? value)
    {
        try
        {
            await app.DispatchEventAsync(eventHandlerId, eventName, value);
        }
        catch (Exception exception)
        {
            ReportError(exception);
        }
    }
}
