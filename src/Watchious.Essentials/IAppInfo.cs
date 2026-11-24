namespace Watchious.Essentials;

// MAUI Essentials' IAppInfo: the app's own name and version, as its watch knows them.
public interface IAppInfo
{
    string Name { get; }

    // The bundle identifier on watchOS, the application id on Wear OS.
    string PackageName { get; }

    // ApplicationDisplayVersion, such as "1.2.0".
    string VersionString { get; }

    Version Version { get; }

    // ApplicationVersion, the build number.
    string BuildString { get; }
}
