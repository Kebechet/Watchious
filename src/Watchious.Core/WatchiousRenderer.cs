using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging;
using Watchious.Protocol;

namespace Watchious;

// Sends the native host only what changed: each render batch becomes one buffer of patches (protocol v2).
public sealed class WatchiousRenderer : Renderer
{
    private readonly IWatchiousHost _host;
    private readonly NativeTree _tree = new();
    private readonly PatchWriter _patches = new();

    public WatchiousRenderer(IServiceProvider services, ILoggerFactory loggerFactory, IWatchiousHost host)
        : base(services, loggerFactory)
    {
        _host = host;
    }

    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    protected override RendererInfo RendererInfo { get; } = new("Watchious", isInteractive: true);

    public Task AddRootComponentAsync(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type componentType)
    {
        return Dispatcher.InvokeAsync(() =>
        {
            var component = InstantiateComponent(componentType);
            var componentId = AssignRootComponentId(component);
            _tree.AddRootComponent(componentId);
            return RenderRootComponentAsync(componentId);
        });
    }

    public Task DispatchNativeEventAsync(ulong eventHandlerId, string eventName, string? value)
    {
        return Dispatcher.InvokeAsync(
            () => DispatchEventAsync(eventHandlerId, null, new NativeEventArgs(eventName, value)));
    }

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        _patches.Begin();
        _tree.Apply(renderBatch, _patches);
        return _patches.HasOperations ? _host.ApplyPatches(_patches.WrittenSpan) : Task.CompletedTask;
    }

    protected override void HandleException(Exception exception)
    {
        _host.ReportError(exception);
    }
}
