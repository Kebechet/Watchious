namespace Watchious;

public sealed class NativeEventArgs(string name, string? value) : EventArgs
{
    public string Name { get; } = name;

    public string? Value { get; } = value;
}
