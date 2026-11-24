using Android.App;
using Android.Content.PM;
using Android.OS;
using Watchious.Hosting;
using Watchious.Hosts.WearOS.Essentials;

namespace Watchious.Hosts.WearOS;

public abstract class WatchiousActivity : Activity
{
    private WatchiousApp? _app;
    private ComposeWatchiousHost? _host;
    private WearOSPermissions? _permissions;

    protected abstract WatchiousApp CreateWatchiousApp();

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var permissions = new WearOSPermissions(this);
        _permissions = permissions;
        using (WatchiousPlatform.ConfigureServices(services => WearOSEssentials.AddTo(services, this, permissions)))
        {
            _app = CreateWatchiousApp();
        }

        _app.InspectorUrl = ReadInspectorUrl();
        _host = new ComposeWatchiousHost(this);
        SetContentView(_host.View);
        _ = StartAsync(_app, _host);
    }

    // adb shell am start -n <app>/<activity> --es watchious_inspector http://localhost:5292/inspector/app, after
    // adb reverse tcp:5292 tcp:5292. Only debuggable builds take it: the launcher activity is exported, so any app
    // could otherwise point it at a server of its own and read the app's screens.
    private Uri? ReadInspectorUrl()
    {
        var flags = ApplicationInfo?.Flags ?? 0;
        var isDebuggable = (flags & ApplicationInfoFlags.Debuggable) != 0;
        var url = Intent?.GetStringExtra("watchious_inspector");
        return isDebuggable && Uri.TryCreate(url, UriKind.Absolute, out var inspectorUrl) ? inspectorUrl : null;
    }

    // Android 12L and older deliver the back button here; newer versions deliver it to the host's
    // OnBackInvokedCallback, so this override isn't called there.
#pragma warning disable CS0672, CA1422
    public override void OnBackPressed()
    {
        if (_host is not null && _host.HandleBack())
        {
            return;
        }

        base.OnBackPressed();
    }
#pragma warning restore CS0672, CA1422

    // The answers to the permissions Watchious.Essentials asked for (location, notifications).
    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        _permissions?.OnRequestPermissionsResult(requestCode, grantResults);
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
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
