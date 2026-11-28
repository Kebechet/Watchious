using Microsoft.Extensions.DependencyInjection;
using Watchious.Controls;
using Watchious.Hosting;

namespace Breathing;

public static class WatchiousProgram
{
    public static WatchiousApp CreateWatchiousApp()
    {
        var builder = WatchiousApp.CreateBuilder();
        builder.UseWatchiousApp<MainPage>();
        builder.Services.AddSingleton<BreathingSettings>();
        // Every page fades from a deep teal to black, except a session, which draws its own glow.
        builder.ConfigureTheme(theme => theme.PageBackground = new LinearGradientBrush(
            [new GradientStop(Color.FromArgb("#0F3F4A"), 0), new GradientStop(Colors.Black, 1)], new Point(0, 0), new Point(0, 1)));
        return builder.Build();
    }
}
