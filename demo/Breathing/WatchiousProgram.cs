using Watchious.Hosting;

namespace Breathing;

public static class WatchiousProgram
{
    public static WatchiousApp CreateWatchiousApp()
    {
        var builder = WatchiousApp.CreateBuilder();
        builder.UseWatchiousApp<MainPage>();
        return builder.Build();
    }
}
