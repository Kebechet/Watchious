using System.ComponentModel;
using Watchious.Controls;

namespace Watchious.Core.Tests.Pages;

// An app's own ITheme that counts who follows its changes.
public sealed class CountingTheme : ITheme
{
    private PropertyChangedEventHandler? _propertyChanged;

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => _propertyChanged += value;
        remove => _propertyChanged -= value;
    }

    public bool HasSubscribers => _propertyChanged is not null;

    public Color? PageBackgroundColor { get; private set; } = Colors.Navy;

    public Brush? PageBackground => null;

    public void ChangeColor(Color color)
    {
        PageBackgroundColor = color;
        _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageBackgroundColor)));
    }
}
