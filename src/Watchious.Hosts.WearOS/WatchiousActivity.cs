using Android.App;
using Android.OS;
using Watchious.Hosting;

namespace Watchious.Hosts.WearOS;

public abstract class WatchiousActivity : Activity
{
    private WatchiousApp? _app;
    private ComposeWatchiousHost? _host;

    protected abstract WatchiousApp CreateWatchiousApp();

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _app = CreateWatchiousApp();
        _host = new ComposeWatchiousHost(this);
        SetContentView(_host.View);
        _ = StartAsync(_app, _host);
    }

    protected override void OnDestroy()
    {
        if (_host is not null)
        {
            _host.Dispose();
            _host = null;
        }

        if (_app is not null)
        {
            _ = _app.DisposeAsync().AsTask();
            _app = null;
        }

        base.OnDestroy();
    }

    private static async Task StartAsync(WatchiousApp app, ComposeWatchiousHost host)
    {
        try
        {
            await host.StartAsync(app);
        }
        catch (Exception exception)
        {
            host.ReportError(exception);
        }
    }
}
