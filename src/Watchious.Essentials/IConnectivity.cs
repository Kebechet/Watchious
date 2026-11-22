namespace Watchious.Essentials;

public interface IConnectivity
{
    NetworkAccess NetworkAccess { get; }
}

public enum NetworkAccess
{
    Unknown,
    None,
    Internet,
}
