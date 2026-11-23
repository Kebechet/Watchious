using Plugin.Maui.WearableMessaging;

namespace Watchious.Samples.Phone;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseWearableMessaging();
        builder.Services.AddSingleton<MainPage>();
        return builder.Build();
    }
}
