namespace Watchious;

public interface IWatchiousHost
{
    // The patches of one render batch (see Protocol.PatchOperation). The span is valid only during the call.
    // Batches must be applied in the order they arrive.
    void ApplyPatches(ReadOnlySpan<byte> patches);

    void ReportError(Exception exception);
}
