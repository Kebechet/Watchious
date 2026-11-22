using Watchious.Health;
using Watchious.Hosting;
using Watchious.Messaging;

namespace Watchious.Samples.Counter;

public static class CounterApp
{
    public static WatchiousApp CreateWatchiousApp()
    {
        var builder = WatchiousApp.CreateBuilder();
        builder.UseWatchiousApp<CounterPage>();
        builder.Services.AddPhoneMessaging();
        builder.Services.AddWatchiousHealth();
        return builder.Build();
    }
}
