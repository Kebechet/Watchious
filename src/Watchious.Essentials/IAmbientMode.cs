namespace Watchious.Essentials;

// The always-on display: when the wrist is lowered the watch dims the app instead of hiding it, and the app should show
// less and update rarely (watchOS's isLuminanceReduced, Wear OS's ambient mode). watchOS dims every app; on Wear OS an
// app stays on the dimmed screen only once it uses this service, which turns ambient support on for its activity.
// AmbientModeChanged is raised on the platform's thread; a page updates itself through InvokeAsync.
public interface IAmbientMode
{
    bool IsSupported { get; }

    bool IsAmbient { get; }

    event EventHandler<AmbientModeChangedEventArgs>? AmbientModeChanged;
}

public sealed class AmbientModeChangedEventArgs(bool isAmbient) : EventArgs
{
    public bool IsAmbient { get; } = isAmbient;
}
