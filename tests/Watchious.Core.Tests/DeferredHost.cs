namespace Watchious.Core.Tests;

// A host like the SwiftUI one, or the Compose one called off the main thread: it applies a batch later, when its
// main thread gets to it, and only then confirms it.
public sealed class DeferredHost : IWatchiousHost
{
    private readonly Queue<(byte[] Patches, TaskCompletionSource Applied)> _pending = new();

    public HostTree Tree { get; } = new();

    public List<Exception> Errors { get; } = [];

    public UiNode Button(string text)
    {
        return Tree.Root.Descendants().Single(node => node.Type == "Button" && node.Content == text);
    }

    public Task ApplyPatches(ReadOnlySpan<byte> patches)
    {
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pending)
        {
            _pending.Enqueue((patches.ToArray(), applied));
        }

        return applied.Task;
    }

    public void ApplyPending()
    {
        while (true)
        {
            (byte[] Patches, TaskCompletionSource Applied) batch;
            lock (_pending)
            {
                if (_pending.Count == 0)
                {
                    return;
                }

                batch = _pending.Dequeue();
            }

            Tree.Apply(Patch.ReadAll(batch.Patches));
            batch.Applied.SetResult();
        }
    }

    public void ReportError(Exception exception)
    {
        Errors.Add(exception);
    }
}
