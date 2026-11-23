using Android.Content;
using Android.Net;
using Android.Runtime;
using Watchious.Essentials;

namespace Watchious.Hosts.WearOS.Essentials;

internal sealed class WearOSConnectivity(Context context) : IConnectivity
{
    public NetworkAccess NetworkAccess
    {
        get
        {
            var manager = context.GetSystemService(Context.ConnectivityService)?.JavaCast<ConnectivityManager>();
            if (manager is null)
            {
                return NetworkAccess.Unknown;
            }

            var network = manager.ActiveNetwork;
            if (network is null)
            {
                return NetworkAccess.None;
            }

            // A watch that reaches the internet through its phone over Bluetooth has an active network too.
            var capabilities = manager.GetNetworkCapabilities(network);
            var hasInternet = capabilities is not null
                && capabilities.HasCapability(NetCapability.Internet)
                && capabilities.HasCapability(NetCapability.Validated);
            return hasInternet ? NetworkAccess.Internet : NetworkAccess.None;
        }
    }
}
