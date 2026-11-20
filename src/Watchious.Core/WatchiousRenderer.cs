using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging;

namespace Watchious;

// Sends the whole resolved UI tree after every render batch instead of applying Blazor's diff edits.
// SwiftUI and Compose diff the tree themselves, and a watch screen has at most a few hundred nodes.
public sealed class WatchiousRenderer : Renderer
{
    private readonly IWatchiousHost _host;
    private readonly UiTreeWriter _treeWriter;
    private int? _rootComponentId;

    public WatchiousRenderer(IServiceProvider services, ILoggerFactory loggerFactory, IWatchiousHost host)
        : base(services, loggerFactory)
    {
        _host = host;
        _treeWriter = new UiTreeWriter(GetCurrentRenderTreeFrames);
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
            _rootComponentId = componentId;
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
        if (_rootComponentId is not null)
        {
            _host.Render(_treeWriter.Write(_rootComponentId.Value));
        }

        return Task.CompletedTask;
    }

    protected override void HandleException(Exception exception)
    {
        _host.ReportError(exception);
    }
}
