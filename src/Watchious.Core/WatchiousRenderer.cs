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
    private readonly ILogger _logger;
    private readonly NativeTree _tree = new();
    private readonly PatchWriter _patches = new();

    public WatchiousRenderer(IServiceProvider services, ILoggerFactory loggerFactory, IWatchiousHost host)
        : base(services, loggerFactory)
    {
        _host = host;
        _logger = loggerFactory.CreateLogger<WatchiousRenderer>();
    }

    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    // The type of element whose later siblings cover the earlier ones, as pages in a navigation stack ("Page" in
    // Watchious.Controls). An event from a covered one is dropped: the user tapped it before the element that covers it
    // reached the screen, such as the second tap of a double tap on a button that opens a page.
    public string? CoveringElementType { get; init; }

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
        return Dispatcher.InvokeAsync(() =>
        {
            var isCovered = CoveringElementType is not null && _tree.IsCovered(eventHandlerId, CoveringElementType);
            if (isCovered)
            {
                _logger.LogDebug("Dropped the {EventName} event of a covered {ElementType} (handler {EventHandlerId}).",
                    eventName, CoveringElementType, eventHandlerId);
                return Task.CompletedTask;
            }

            return DispatchEventAsync(eventHandlerId, null, new NativeEventArgs(eventName, value));
        });
    }

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        _patches.Begin();
        _tree.Apply(renderBatch, _patches);
        var shown = _patches.HasOperations ? _host.ApplyPatches(_patches.WrittenSpan) : Task.CompletedTask;
        var disposed = renderBatch.DisposedEventHandlerIDs;
        if (disposed.Count > 0)
        {
            // Copied: Blazor reuses the batch's buffers once this returns.
            _ = ForgetEventHandlersWhenShownAsync(shown, disposed.Array.AsSpan(0, disposed.Count).ToArray());
        }

        return shown;
    }

    // When Blazor itself lets go of them: once the host shows the batch that disposed them.
    private async Task ForgetEventHandlersWhenShownAsync(Task shown, ulong[] eventHandlerIds)
    {
        await shown.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await Dispatcher.InvokeAsync(() => _tree.ForgetEventHandlers(eventHandlerIds));
    }

    protected override void HandleException(Exception exception)
    {
        _host.ReportError(exception);
    }
}
