using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Watchious.Core.Tests.Pages;
using Watchious.Essentials;
using Watchious.Hosting;
using Watchious.Preview;
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

    // Each host registers the platform's implementation of every service before the app's own, so a page that injects
    // one gets it on both watches and in the preview.
    [Theory]
    [InlineData("src/Watchious.Hosts.watchOS/Essentials/WatchOSEssentials.cs")]
    [InlineData("src/Watchious.Hosts.WearOS/Essentials/WearOSEssentials.cs")]
    [InlineData("src/Watchious.Preview/PreviewEssentials.cs")]
    public void EveryService_IsRegisteredByTheHost(string registrations)
    {
        // Arrange
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), registrations));
        var services = typeof(IHaptics).Assembly.GetExportedTypes().Where(type => type.IsInterface).Select(type => type.Name).ToList();

        // Act
        var missing = services.Where(service => !source.Contains($"AddSingleton<{service}", StringComparison.Ordinal)).ToList();

        // Assert
        services.ShouldContain(nameof(IGeolocation));
        missing.ShouldBeEmpty($"{registrations} doesn't register these services");
    }

    [Fact]
    public void PreviewServices_ResolveForAPage()
    {
        // Arrange
        var services = new ServiceCollection();
        PreviewEssentials.AddTo(services, PreviewPlatform.WearOS);
        using var provider = services.BuildServiceProvider();
        var interfaces = typeof(IHaptics).Assembly.GetExportedTypes().Where(type => type.IsInterface).ToList();

        // Act
        var resolved = interfaces.Select(provider.GetService).ToList();

        // Assert
        resolved.ShouldAllBe(service => service != null);
    }

    [Fact]
    public async Task PreviewSecureStorage_ReadsBackWhatItStored()
    {
        // Arrange
        var services = new ServiceCollection();
        PreviewEssentials.AddTo(services, PreviewPlatform.watchOS);
        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<ISecureStorage>();
        storage.RemoveAll();

        // Act
        await storage.SetAsync("token", "secret");
        var stored = await storage.GetAsync("token");
        var isRemoved = storage.Remove("token");
        var removed = await storage.GetAsync("token");

        // Assert
        stored.ShouldBe("secret");
        isRemoved.ShouldBeTrue();
        removed.ShouldBeNull();
    }

    [Fact]
    public void PreviewSensors_AreUnsupportedAndSayWhy()
    {
        // Arrange
        var services = new ServiceCollection();
        PreviewEssentials.AddTo(services, PreviewPlatform.WearOS);
        using var provider = services.BuildServiceProvider();
        var accelerometer = provider.GetRequiredService<IAccelerometer>();

        // Act
        var exception = Should.Throw<FeatureNotSupportedException>(() => accelerometer.Start(SensorSpeed.UI));

        // Assert
        accelerometer.IsSupported.ShouldBeFalse();
        exception.Message.ShouldContain("accelerometer");
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

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "platforms")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("The repository root wasn't found above the test output.");
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
