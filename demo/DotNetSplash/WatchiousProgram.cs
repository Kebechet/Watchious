using Watchious.Controls;
using Watchious.Hosting;

namespace DotNetSplash;

public static class WatchiousProgram
{
    public static WatchiousApp CreateWatchiousApp()
    {
        var builder = WatchiousApp.CreateBuilder();
        builder.UseWatchiousApp<MainPage>();
        // MAUI's splash screen color.
        builder.ConfigureTheme(theme => theme.PageBackgroundColor = Color.FromArgb("#512BD4"));
        return builder.Build();
    }
}
