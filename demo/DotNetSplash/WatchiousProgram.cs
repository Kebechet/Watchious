using Watchious.Hosting;

namespace DotNetSplash;

public static class WatchiousProgram
{
    public static WatchiousApp CreateWatchiousApp()
    {
        var builder = WatchiousApp.CreateBuilder();
        builder.UseWatchiousApp<MainPage>();
        return builder.Build();
    }
}
