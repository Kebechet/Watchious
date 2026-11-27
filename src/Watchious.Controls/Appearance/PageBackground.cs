namespace Watchious.Controls;

// A page's background as the hosts get it, in MAUI's order: the page's Background, its BackgroundColor, the theme's
// PageBackground, the theme's PageBackgroundColor. Null leaves the platform's black.
internal static class PageBackground
{
    public static string? Resolve(IPage? page, ITheme theme)
    {
        return Of(page?.Background, page?.BackgroundColor) ?? Of(theme.PageBackground, theme.PageBackgroundColor);
    }

    private static string? Of(Brush? brush, Color? color)
    {
        return brush?.ToProtocol() ?? color?.ToProtocol();
    }
}
