using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Watchious.Controls;

// The app's defaults, which the navigation reads for a page that has no value of its own, as MAUI's implicit page style
// does. WatchiousTheme is the built-in one; an app can register its own ITheme in the builder's Services instead.
public interface ITheme : INotifyPropertyChanged
{
    Color? PageBackgroundColor { get; }

    Brush? PageBackground { get; }
}

// The built-in theme: builder.ConfigureTheme(theme => theme.PageBackgroundColor = ...) sets it up, and a page or service
// that injects it can change it while the app runs.
public sealed class WatchiousTheme : ITheme
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public Color? PageBackgroundColor
    {
        get;
        set => Set(ref field, value);
    }

    public Brush? PageBackground
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
