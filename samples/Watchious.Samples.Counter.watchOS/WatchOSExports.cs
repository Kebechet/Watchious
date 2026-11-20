using System.Runtime.InteropServices;
using Watchious.Hosts.watchOS;

namespace Watchious.Samples.Counter.watchOS;

public static unsafe class WatchOSExports
{
    [UnmanagedCallersOnly(EntryPoint = "watchious_start")]
    public static int Start(delegate* unmanaged<byte*, int, void> render, delegate* unmanaged<byte*, int, void> reportError)
    {
        return WatchOSBridge.Start(CounterApp.CreateWatchiousApp, render, reportError);
    }

    [UnmanagedCallersOnly(EntryPoint = "watchious_dispatch")]
    public static int Dispatch(ulong eventHandlerId, byte* eventNameUtf8, byte* valueUtf8)
    {
        return WatchOSBridge.Dispatch(eventHandlerId, eventNameUtf8, valueUtf8);
    }
}
