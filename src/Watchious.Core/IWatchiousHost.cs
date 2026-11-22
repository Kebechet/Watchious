namespace Watchious;

public interface IWatchiousHost
{
    // The patches of one render batch (see Protocol.PatchOperation). The span is valid only during the call.
    // Batches must be applied in the order they arrive. The task completes when the batch is on the display: until
    // then the renderer keeps the event handler ids the batch replaces, which the controls still on the display send
    // (as Blazor's own renderers wait for their browser).
    Task ApplyPatches(ReadOnlySpan<byte> patches);

    void ReportError(Exception exception);
}
