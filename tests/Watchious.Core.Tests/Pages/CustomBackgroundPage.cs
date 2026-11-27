using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Watchious.Controls;

namespace Watchious.Core.Tests.Pages;

// An app's own IPage, without PageBase, that counts who follows its changes.
public sealed class CustomBackgroundPage : ComponentBase, IPage
{
    private PropertyChangedEventHandler? _propertyChanged;

    public static CustomBackgroundPage? LastCreated { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => _propertyChanged += value;
        remove => _propertyChanged -= value;
    }

    public bool HasSubscribers => _propertyChanged is not null;

    public Color? BackgroundColor { get; private set; } = Colors.Teal;

    public Brush? Background => null;

    public void ChangeColor(Color color)
    {
        BackgroundColor = color;
        _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundColor)));
    }

    protected override void OnInitialized()
    {
        LastCreated = this;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<Label>(0);
        builder.AddComponentParameter(1, nameof(Label.ChildContent), (RenderFragment)(content => content.AddContent(0, "Custom")));
        builder.CloseComponent();
    }
}
