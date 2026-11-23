using System.Collections.Concurrent;
using System.Text;

namespace Watchious.Hosts.watchOS;

// The Swift side copies the bytes before the callback returns, so the pinned buffer only has to live for
// the duration of the call.
internal sealed unsafe class NativeWatchiousHost : IWatchiousHost
{
    private readonly delegate* unmanaged<byte*, int, void> _applyPatches;
    private readonly delegate* unmanaged<byte*, int, void> _reportError;
    private readonly ConcurrentQueue<TaskCompletionSource> _pendingBatches = new();

    public NativeWatchiousHost(
        delegate* unmanaged<byte*, int, void> applyPatches,
        delegate* unmanaged<byte*, int, void> reportError)
    {
        _applyPatches = applyPatches;
        _reportError = reportError;
    }

    public Task ApplyPatches(ReadOnlySpan<byte> patches)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingBatches.Enqueue(applied);
        fixed (byte* pointer = patches)
        {
            _applyPatches(pointer, patches.Length);
        }

        return applied.Task;
    }

    // The SwiftUI host confirms each batch once it shows it (watchious_patches_applied), in the order they came.
    public void OnPatchesApplied()
    {
        if (_pendingBatches.TryDequeue(out var applied))
        {
            applied.TrySetResult();
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
