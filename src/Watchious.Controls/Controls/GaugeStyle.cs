namespace Watchious.Controls;

// Apple.Gauge's styles. Here rather than in the Apple namespace: a Style value is a C# expression, in which an app's
// own Apple namespace (such as MyApp.Apple) would hide Watchious's, while the tag itself is always found by its full
// name.
public enum GaugeStyle
{
    // SwiftUI's own choice: a bar with the range under it.
    Automatic,
    // A ring that is open at the bottom, with the current value inside.
    Circular,
    // A closed ring that fills like a battery.
    CircularCapacity,
    // A thin bar with a marker at the value.
    Linear,
    // A bar that fills up to the value.
    LinearCapacity,
}
