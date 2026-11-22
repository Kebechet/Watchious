using Android.Content;
using Android.OS;
using Watchious.Essentials;

namespace Watchious.Hosts.WearOS.Essentials;

internal sealed class WearOSDeviceInfo(Context context) : IDeviceInfo
{
    public WatchPlatform Platform => WatchPlatform.WearOS;

    public string Manufacturer => Build.Manufacturer ?? "";

    public string Model => Build.Model ?? "";

    // The release is "14" or "13.1", and Version needs at least a major and a minor part.
    public Version OSVersion
    {
        get
        {
            var release = Build.VERSION.Release ?? "";
            return Version.TryParse(release.Contains('.') ? release : release + ".0", out var version)
                ? version
                : new Version(0, 0);
        }
    }

    public bool IsRoundScreen => context.Resources?.Configuration?.IsScreenRound ?? false;
}
