using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Watchious.Hosting;

namespace Watchious.Navigation;

internal sealed class PageRoute(
    RouteTemplate template,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type pageType)
{
    public RouteTemplate Template { get; } = template;

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public Type PageType { get; } = pageType;
}

// Watchious's source generator writes one IWatchiousRoutes class per assembly, with an AddRoute call for every
// @page directive, and names it in this attribute. Build() reads it from the root page's assembly. The typeof
// references in the generated class also keep the pages from being trimmed away, since nothing else references a
// page that is only reached by its route.
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class WatchiousRoutesAttribute(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type routesType)
    : Attribute
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type RoutesType { get; } = routesType;
}

[EditorBrowsable(EditorBrowsableState.Never)]
public interface IWatchiousRoutes
{
    void AddTo(WatchiousAppBuilder builder);
}
