namespace Watchious.Core.Tests;

public sealed class RecordingHost : IWatchiousHost
{
    public List<string> Trees { get; } = [];

    public List<Exception> Errors { get; } = [];

    public UiNode LastTree => UiNode.ParseRoot(Trees[^1]);

    public void Render(string uiTreeJson)
    {
        Trees.Add(uiTreeJson);
    }

    public void ReportError(Exception exception)
    {
        Errors.Add(exception);
    }
}
