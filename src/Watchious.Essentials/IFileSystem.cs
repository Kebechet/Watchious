namespace Watchious.Essentials;

// MAUI Essentials' IFileSystem: where the app keeps its files. The watch may clear the cache when its storage runs low;
// AppDataDirectory stays until the app is removed.
public interface IFileSystem
{
    string CacheDirectory { get; }

    string AppDataDirectory { get; }
}
