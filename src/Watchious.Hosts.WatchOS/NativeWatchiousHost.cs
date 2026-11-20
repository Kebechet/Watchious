using System.Text;

namespace Watchious.Hosts.WatchOS;

// The Swift side copies the bytes before the callback returns, so the pinned buffer only has to live for
// the duration of the call.
internal sealed unsafe class NativeWatchiousHost : IWatchiousHost
{
    private readonly delegate* unmanaged<byte*, int, void> _render;
    private readonly delegate* unmanaged<byte*, int, void> _reportError;

    public NativeWatchiousHost(
        delegate* unmanaged<byte*, int, void> render,
        delegate* unmanaged<byte*, int, void> reportError)
    {
        _render = render;
        _reportError = reportError;
    }

    public void Render(string uiTreeJson)
    {
        Send(_render, uiTreeJson);
    }

    public void ReportError(Exception exception)
    {
        Send(_reportError, exception.ToString());
    }

    private static void Send(delegate* unmanaged<byte*, int, void> callback, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        fixed (byte* pointer = bytes)
        {
            callback(pointer, bytes.Length);
        }
    }
}
