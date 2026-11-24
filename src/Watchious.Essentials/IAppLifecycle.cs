namespace Watchious.Essentials;

// Whether the app is on screen and in use: SwiftUI's scene phase on watchOS, the activity's state on Wear OS.
// Lowering the wrist makes a watchOS app Inactive, while a Wear OS app in ambient mode stays Active: IAmbientMode tells
// on both when the screen is dimmed. StateChanged is raised on the platform's thread; a page updates itself through
// InvokeAsync.
public interface IAppLifecycle
{
    AppState State { get; }

    event EventHandler<AppStateChangedEventArgs>? StateChanged;
}

public enum AppState
{
    // On screen and in use.
    Active,

    // On screen but not in use: the wrist is lowered (watchOS), or another window covers the app.
    Inactive,

    // Not on screen. A running workout keeps the app running here.
    Background,
}

public sealed class AppStateChangedEventArgs(AppState state) : EventArgs
{
    public AppState State { get; } = state;
}
