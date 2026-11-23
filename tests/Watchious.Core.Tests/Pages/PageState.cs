namespace Watchious.Core.Tests.Pages;

// What the patch test pages render. A test changes it, then clicks the page's Refresh button to re-render.
public sealed class PageState
{
    public List<string> Items { get; set; } = [];

    public bool Flag { get; set; }

    public string Text { get; set; } = "";
}
