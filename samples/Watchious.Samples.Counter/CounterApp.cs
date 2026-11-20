using Watchious.Hosting;

namespace Watchious.Samples.Counter;

public static class CounterApp
{
    public static WatchiousApp CreateWatchiousApp()
    {
        var builder = WatchiousApp.CreateBuilder();
        builder.UseWatchiousApp<CounterPage>();
        return builder.Build();
    }
}
