namespace Watchious;

public interface IWatchiousHost
{
    void Render(string uiTreeJson);

    void ReportError(Exception exception);
}
