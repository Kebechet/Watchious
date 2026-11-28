using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Watchious.Essentials;
using Watchious.Navigation;
using Xunit;

namespace Breathing.Tests;

public sealed class BreathingAppTests
{
    // Calm breathes in for 5 s, so its glow changes, and a batch goes to the watch, at nearly every 100 ms tick.
    private const string Calm = "calm";

    [Fact]
    public async Task FinishedSession_UpdatesTheTotalsOnTheList()
    {
        // Arrange
        var watch = new FakeWatch();
        await using var app = await watch.StartAsync();
        var settings = app.Services.GetRequiredService<BreathingSettings>();
        var batches = watch.BatchCount;

        // Act
        settings.AddFinishedSession(TimeSpan.FromSeconds(50));
        await SettleAsync();

        // Assert
        Assert.True(watch.BatchCount > batches);
        Assert.Empty(watch.Errors);
    }

    [Fact]
    public async Task List_StopsFollowingTheTotals_WhenTheAppEnds()
    {
        // Arrange
        var watch = new FakeWatch();
        var app = await watch.StartAsync();
        var settings = app.Services.GetRequiredService<BreathingSettings>();
        Assert.True(HasChangedSubscribers(settings));

        // Act
        await app.DisposeAsync();
        settings.AddFinishedSession(TimeSpan.FromSeconds(50));
        await SettleAsync();

        // Assert
        Assert.False(HasChangedSubscribers(settings));
        Assert.Empty(watch.Errors);
    }

    [Fact]
    public async Task Session_KeepsTheScreenOnUntilTheUserLeaves()
    {
        // Arrange
        var watch = new FakeWatch();
        await using var app = await watch.StartAsync();
        var navigation = app.Services.GetRequiredService<INavigation>();

        // Act
        await navigation.GoToAsync(Routes.Session(Calm, 3));
        var isOnDuringSession = watch.DeviceDisplay.KeepScreenOn;
        await navigation.GoBackAsync();

        // Assert
        Assert.True(isOnDuringSession);
        Assert.False(watch.DeviceDisplay.KeepScreenOn);
        Assert.Contains(HapticFeedback.Start, watch.Haptics.Played);
    }

    [Fact]
    public async Task Session_WaitsWhileTheAppIsAway_AndGoesOnWhenItIsBack()
    {
        // Arrange
        var watch = new FakeWatch();
        await using var app = await watch.StartAsync();
        await app.Services.GetRequiredService<INavigation>().GoToAsync(Routes.Session(Calm, 3));
        await SettleAsync();

        // Act and assert: away, the session stops changing and lets the screen go.
        watch.AppLifecycle.ChangeTo(AppState.Background);
        await SettleAsync();
        var batchesWhileAway = watch.BatchCount;
        await Task.Delay(TimeSpan.FromMilliseconds(600), TestContext.Current.CancellationToken);
        Assert.Equal(batchesWhileAway, watch.BatchCount);
        Assert.False(watch.DeviceDisplay.KeepScreenOn);

        // Back, it keeps the screen on again and goes on.
        watch.AppLifecycle.ChangeTo(AppState.Active);
        await Task.Delay(TimeSpan.FromMilliseconds(600), TestContext.Current.CancellationToken);
        Assert.True(watch.DeviceDisplay.KeepScreenOn);
        Assert.True(watch.BatchCount > batchesWhileAway);
        Assert.Empty(watch.Errors);
    }

    [Fact]
    public async Task Session_StopsFollowingTheApp_WhenTheUserLeaves()
    {
        // Arrange
        var watch = new FakeWatch();
        await using var app = await watch.StartAsync();
        var navigation = app.Services.GetRequiredService<INavigation>();
        await navigation.GoToAsync(Routes.Session(Calm, 3));
        Assert.True(watch.AppLifecycle.HasSubscribers);

        // Act
        await navigation.GoBackAsync();
        watch.AppLifecycle.ChangeTo(AppState.Background);
        watch.AppLifecycle.ChangeTo(AppState.Active);
        await SettleAsync();

        // Assert
        Assert.False(watch.AppLifecycle.HasSubscribers);
        Assert.False(watch.DeviceDisplay.KeepScreenOn);
        Assert.Empty(watch.Errors);
    }

    // A change outside an event renders through the renderer's dispatcher.
    private static Task SettleAsync()
    {
        return Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
    }

    // BreathingSettings has no use for a subscriber count of its own, so the test reads its event's field.
    private static bool HasChangedSubscribers(BreathingSettings settings)
    {
        var field = typeof(BreathingSettings).GetField(nameof(BreathingSettings.Changed), BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(settings) is not null;
    }
}
