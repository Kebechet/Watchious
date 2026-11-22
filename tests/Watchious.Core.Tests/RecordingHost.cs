namespace Watchious.Core.Tests;

public sealed class RecordingHost : IWatchiousHost
{
    public List<byte[]> RawBatches { get; } = [];

    public List<List<Patch>> Batches { get; } = [];

    public List<Exception> Errors { get; } = [];

    public HostTree Tree { get; } = new();

    public UiNode LastTree => Tree.Root;

    public List<Patch> LastBatch => Batches[^1];

    public UiNode Button(string text)
    {
        return LastTree.Descendants().Single(node => node.Type == "Button" && node.Content == text);
    }

    public void ApplyPatches(ReadOnlySpan<byte> patches)
    {
        RawBatches.Add(patches.ToArray());
        var batch = Patch.ReadAll(patches);
        Batches.Add(batch);
        Tree.Apply(batch);
    }

    public void ReportError(Exception exception)
    {
        Errors.Add(exception);
    }
}
