using System.Runtime.InteropServices;
using Watchious.Hosting;

namespace Watchious.Hosts.WatchOS;

// Called from the [UnmanagedCallersOnly] exports of the app project. NativeAOT only exports entry points
// from the app's own assembly, so the exports live there and forward here.
public static unsafe class WatchOSBridge
{
    public const int Success = 0;
    public const int AlreadyStarted = 1;
    public const int NotStarted = 2;
    public const int InvalidArgument = 3;
    public const int StartFailed = 4;

    private static WatchiousApp? _app;
    private static NativeWatchiousHost? _host;

    public static int Start(
        Func<WatchiousApp> createApp,
        delegate* unmanaged<byte*, int, void> render,
        delegate* unmanaged<byte*, int, void> reportError)
    {
        if (_app is not null)
        {
            return AlreadyStarted;
        }

        if (render is null || reportError is null)
        {
            return InvalidArgument;
        }

        var host = new NativeWatchiousHost(render, reportError);
        try
        {
            var app = createApp();
            _app = app;
            _host = host;
            ReportFailure(app.StartAsync(host), host);
            return Success;
        }
        catch (Exception exception)
        {
            host.ReportError(exception);
            return StartFailed;
        }
    }

    public static int Dispatch(ulong eventHandlerId, byte* eventNameUtf8, byte* valueUtf8)
    {
        if (_app is null || _host is null)
        {
            return NotStarted;
        }

        var eventName = Marshal.PtrToStringUTF8((nint)eventNameUtf8);
        if (eventName is null)
        {
            return InvalidArgument;
        }

        var value = Marshal.PtrToStringUTF8((nint)valueUtf8);
        ReportFailure(_app.DispatchEventAsync(eventHandlerId, eventName, value), _host);
        return Success;
    }

    private static void ReportFailure(Task task, NativeWatchiousHost host)
    {
        task.ContinueWith(
            failed => host.ReportError(failed.Exception!.GetBaseException()),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
