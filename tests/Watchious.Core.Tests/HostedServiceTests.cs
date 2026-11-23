using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Watchious.Core.Tests.Pages;
using Watchious.Hosting;
using Watchious.Messaging;
using Xunit;

namespace Watchious.Core.Tests;

public class HostedServiceTests
{
    [Fact]
    public async Task HostedService_StartsWithTheAppAndStopsWithIt()
    {
        // Arrange
        var service = new RecordingHostedService();
        var builder = WatchiousApp.CreateBuilder();
        builder.Services.AddSingleton(new PageState { Items = ["a"] });
        builder.Services.AddHostedService(_ => service);
        var app = builder.UseWatchiousApp<ClosureListPage>().Build();

        // Act
        await app.StartAsync(new RecordingHost()).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var isStartedWithTheApp = service.IsStarted;
        await app.DisposeAsync();

        // Assert
        isStartedWithTheApp.ShouldBeTrue();
        service.IsStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task PhoneMessaging_IsCreatedWhenTheAppStarts()
    {
        // Arrange
        var isCreated = false;
        var builder = WatchiousApp.CreateBuilder();
        builder.Services.AddSingleton(new PageState { Items = ["a"] });
        builder.Services.AddPhoneMessaging();
        builder.Services.AddSingleton<IPhoneMessaging>(_ =>
        {
            isCreated = true;
            return new UnavailablePhoneMessaging();
        });
        await using var app = builder.UseWatchiousApp<ClosureListPage>().Build();

        // Act
        await app.StartAsync(new RecordingHost()).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        isCreated.ShouldBeTrue();
    }

    private sealed class RecordingHostedService : IHostedService
    {
        public bool IsStarted { get; private set; }

        public bool IsStopped { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            IsStarted = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            IsStopped = true;
            return Task.CompletedTask;
        }
    }
}
