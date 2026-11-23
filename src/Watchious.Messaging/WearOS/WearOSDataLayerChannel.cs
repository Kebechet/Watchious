using Android.App;
using Watchious.Messaging.Native;

namespace Watchious.Messaging.WearOS;

// The Wearable Data Layer through the module's Java API (platforms/WearOS/messaging).
internal sealed class WearOSDataLayerChannel : Java.Lang.Object, IDataLayerChannel, WatchiousDataLayer.IListener
{
    private readonly WatchiousDataLayer _dataLayer;
    private DataLayerPhoneMessaging? _messaging;

    private WearOSDataLayerChannel()
    {
        _dataLayer = new WatchiousDataLayer(Application.Context, this);
    }

    public static IPhoneMessaging CreateMessaging()
    {
        var channel = new WearOSDataLayerChannel();
        var messaging = new DataLayerPhoneMessaging(channel, channel);
        channel._messaging = messaging;
        channel._dataLayer.Start();
        return messaging;
    }

    public Task<bool> IsPhoneConnectedAsync()
    {
        var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dataLayer.IsPhoneReachable(new ReachabilityCallback(result));
        return result.Task;
    }

    public Task SendMessageAsync(string path, byte[] data)
    {
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _dataLayer.SendMessage(path, data, new CompletionCallback(result));
        return result.Task;
    }

    public Task PutDataItemAsync(string path, IReadOnlyDictionary<string, string> values)
    {
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _dataLayer.PutDataItem(path, new Dictionary<string, string>(values), new CompletionCallback(result));
        return result.Task;
    }

    public void OnMessage(string path, byte[] data)
    {
        _messaging?.OnMessage(path, data);
    }

    public void OnDataItem(string path, string key, string? value)
    {
        _messaging?.OnDataItem(path, key, value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dataLayer.Stop();
            _messaging = null;
        }

        base.Dispose(disposing);
    }

    private sealed class ReachabilityCallback(TaskCompletionSource<bool> result)
        : Java.Lang.Object, WatchiousDataLayer.IReachabilityCallback
    {
        public void OnResult(bool isReachable)
        {
            result.TrySetResult(isReachable);
        }
    }

    private sealed class CompletionCallback(TaskCompletionSource result) : Java.Lang.Object, WatchiousDataLayer.ICallback
    {
        public void OnComplete(string? error)
        {
            if (error is null)
            {
                result.TrySetResult();
                return;
            }

            result.TrySetException(new InvalidOperationException(error));
        }
    }
}
