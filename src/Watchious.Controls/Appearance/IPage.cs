using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;

namespace Watchious.Controls;

// What the navigation reads from a page it opens, and sends to the watch as that page's settings. PageBase is the
// ready-made one; an app can write its own base class instead. Only a page the navigation opens counts: an IPage
// component used inside another page is just part of that page.
public interface IPage : INotifyPropertyChanged
{
    // MAUI's rule: Background wins over BackgroundColor, and either wins over the theme's (ITheme).
    Color? BackgroundColor { get; }

    Brush? Background { get; }
}

// Watchious' counterpart of MAUI's ContentPage: a page inherits it (@inherits PageBase) and sets its properties in its
// code, for example in OnInitialized.
public abstract class PageBase : ComponentBase, IPage
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public Color? BackgroundColor
    {
        get;
        set => Set(ref field, value);
    }

    public Brush? Background
    {
        get;
        set => Set(ref field, value);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
