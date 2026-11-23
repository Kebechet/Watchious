using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Watchious.Core.Tests.Pages;
using Watchious.Essentials;
using Watchious.Hosting;
using Xunit;

namespace Watchious.Core.Tests;

public class EssentialsTests
{
    [Fact]
    public void Preferences_ReadBackEveryTypeTheyStore()
    {
        // Arrange
        var preferences = new MemoryPreferences();

        // Act
        preferences.Set("text", "Ada");
        preferences.Set("flag", true);
        preferences.Set("count", 42);
        preferences.Set("ticks", 1L << 40);
        preferences.Set("weight", 82.5);

        // Assert
        preferences.Get("text", null).ShouldBe("Ada");
        preferences.Get("flag", false).ShouldBeTrue();
        preferences.Get("count", 0).ShouldBe(42);
        preferences.Get("ticks", 0L).ShouldBe(1L << 40);
        preferences.Get("weight", 0.0).ShouldBe(82.5);
    }

    [Fact]
    public void Preferences_StoreNumbersCultureInvariant()
    {
        // Arrange
        var preferences = new MemoryPreferences();
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sk-SK");

        try
        {
            // Act
            preferences.Set("weight", 82.5);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        // Assert
        preferences.Stored["weight"].ShouldBe("82.5");
        preferences.Get("weight", 0.0).ShouldBe(82.5);
    }

    [Fact]
    public void Preferences_MissingOrUnreadableValue_ReturnsTheDefault()
    {
        // Arrange
        var preferences = new MemoryPreferences();
        preferences.Set("count", "many");

        // Act
        var count = preferences.Get("count", 7);
        var missing = preferences.Get("missing", 3.5);

        // Assert
        count.ShouldBe(7);
        missing.ShouldBe(3.5);
    }

    [Fact]
    public void Preferences_SetNull_RemovesTheKey()
    {
        // Arrange
        var preferences = new MemoryPreferences();
        preferences.Set("text", "Ada");

        // Act
        preferences.Set("text", null);

        // Assert
        preferences.ContainsKey("text").ShouldBeFalse();
    }

    [Fact]
    public void ConfigureServices_AddsThePlatformServicesToAppsCreatedInside()
    {
        // Arrange
        var haptics = new RecordingHaptics();

        // Act
        WatchiousApp app;
        using (WatchiousPlatform.ConfigureServices(services => services.AddSingleton<IHaptics>(haptics)))
        {
            app = WatchiousApp.CreateBuilder().UseWatchiousApp<StaticPage>().Build();
        }

        var appCreatedLater = WatchiousApp.CreateBuilder().UseWatchiousApp<StaticPage>().Build();

        // Assert
        app.Services.GetRequiredService<IHaptics>().ShouldBeSameAs(haptics);
        appCreatedLater.Services.GetService<IHaptics>().ShouldBeNull();
    }

    [Fact]
    public void ConfigureServices_AppRegistrationsReplaceThePlatformOnes()
    {
        // Arrange
        var appHaptics = new RecordingHaptics();

        // Act
        WatchiousApp app;
        using (WatchiousPlatform.ConfigureServices(services => services.AddSingleton<IHaptics>(new RecordingHaptics())))
        {
            var builder = WatchiousApp.CreateBuilder();
            builder.Services.AddSingleton<IHaptics>(appHaptics);
            app = builder.UseWatchiousApp<StaticPage>().Build();
        }

        // Assert
        app.Services.GetRequiredService<IHaptics>().ShouldBeSameAs(appHaptics);
    }

    private sealed class MemoryPreferences : StringPreferences
    {
        public Dictionary<string, string> Stored { get; } = [];

        public override bool ContainsKey(string key)
        {
            return Stored.ContainsKey(key);
        }

        public override void Remove(string key)
        {
            Stored.Remove(key);
        }

        public override void Clear()
        {
            Stored.Clear();
        }

        protected override string? GetString(string key)
        {
            return Stored.GetValueOrDefault(key);
        }

        protected override void SetString(string key, string value)
        {
            Stored[key] = value;
        }
    }

    private sealed class RecordingHaptics : IHaptics
    {
        public void Play(HapticFeedback feedback)
        {
        }
    }
}
