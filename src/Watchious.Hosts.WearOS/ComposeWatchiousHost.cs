using Android.App;
using Android.Util;
using Android.Views;
using Watchious.Hosting;
using Watchious.Hosts.WearOS.Native;

namespace Watchious.Hosts.WearOS;

public sealed class ComposeWatchiousHost : Java.Lang.Object, IWatchiousHost, IWatchiousEventListener
{
    private const string LogTag = "Watchious";

    private readonly Activity _activity;
    private readonly WatchiousComposeHost _composeHost;
    private WatchiousApp? _app;

    public ComposeWatchiousHost(Activity activity)
    {
        _activity = activity;
        _composeHost = new WatchiousComposeHost(activity);
        _composeHost.SetEventListener(this);
    }

    public View View => _composeHost.View;

    public Task StartAsync(WatchiousApp app)
    {
        _app = app;
        return app.StartAsync(this);
    }

    public void Render(string uiTreeJson)
    {
        _activity.RunOnUiThread(() => _composeHost.Render(uiTreeJson));
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _composeHost.SetEventListener(null);
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
