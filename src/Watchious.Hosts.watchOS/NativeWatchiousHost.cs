using System.Text;

namespace Watchious.Hosts.watchOS;

// The Swift side copies the bytes before the callback returns, so the pinned buffer only has to live for
// the duration of the call.
internal sealed unsafe class NativeWatchiousHost : IWatchiousHost
{
    private readonly delegate* unmanaged<byte*, int, void> _applyPatches;
    private readonly delegate* unmanaged<byte*, int, void> _reportError;

    public NativeWatchiousHost(
        delegate* unmanaged<byte*, int, void> applyPatches,
        delegate* unmanaged<byte*, int, void> reportError)
    {
        _applyPatches = applyPatches;
        _reportError = reportError;
    }

    public void ApplyPatches(ReadOnlySpan<byte> patches)
    {
        fixed (byte* pointer = patches)
        {
            _applyPatches(pointer, patches.Length);
        }
    }

    public void ReportError(Exception exception)
    {
        var bytes = Encoding.UTF8.GetBytes(exception.ToString());
        fixed (byte* pointer = bytes)
        {
            _reportError(pointer, bytes.Length);
        }
    }
}
