using Watchious.Essentials;

namespace Watchious.Hosts.watchOS.Essentials;

// SwiftUI's scene phase, which the host reports as it changes (platforms/watchOS/Host/AppState.swift).
internal sealed class WatchOSAppLifecycle : IAppLifecycle, IDisposable
{
    public WatchOSAppLifecycle()
    {
        WatchOSCallbacks.AppLifecycle = this;
    }

    public AppState State => (AppState)WatchOSNative.AppState();

    public event EventHandler<AppStateChangedEventArgs>? StateChanged;

    public void Dispose()
    {
        if (WatchOSCallbacks.AppLifecycle == this)
        {
            WatchOSCallbacks.AppLifecycle = null;
        }
    }

    internal void OnNativeState(int state)
    {
        StateChanged?.Invoke(this, new AppStateChangedEventArgs((AppState)state));
    }
}

// SwiftUI's isLuminanceReduced: watchOS dims every app on the always-on display.
internal sealed class WatchOSAmbientMode : IAmbientMode, IDisposable
{
    public WatchOSAmbientMode()
    {
        WatchOSCallbacks.AmbientMode = this;
    }

    public bool IsSupported => true;

    public bool IsAmbient => WatchOSNative.IsLuminanceReduced() != 0;

    public event EventHandler<AmbientModeChangedEventArgs>? AmbientModeChanged;

    public void Dispose()
    {
        if (WatchOSCallbacks.AmbientMode == this)
        {
            WatchOSCallbacks.AmbientMode = null;
        }
    }

    internal void OnNativeAmbient(bool isAmbient)
    {
        AmbientModeChanged?.Invoke(this, new AmbientModeChangedEventArgs(isAmbient));
    }
}

// The Keychain (platforms/watchOS/Host/SecureStorage.swift).
internal sealed class WatchOSSecureStorage : ISecureStorage
{
    public Task<string?> GetAsync(string key)
    {
        return Task.FromResult(WatchOSNative.TakeString(WatchOSNative.SecureGet(key)));
    }

    public Task SetAsync(string key, string value)
    {
        var status = WatchOSNative.SecureSet(key, value);
        if (status != 0)
        {
            throw new InvalidOperationException($"The Keychain didn't store the value (OSStatus {status}).");
        }

        return Task.CompletedTask;
    }

    public bool Remove(string key)
    {
        return WatchOSNative.SecureRemove(key) != 0;
    }

    public void RemoveAll()
    {
        WatchOSNative.SecureRemoveAll();
    }
}

// UNUserNotificationCenter (platforms/watchOS/Host/Notifications.swift).
internal sealed class WatchOSLocalNotifications : ILocalNotifications
{
    public bool IsSupported => true;

    public async Task<bool> RequestPermissionAsync()
    {
        var result = await WatchOSCallbacks.RunAsync(WatchOSNative.NotificationsRequestPermission);
        return result == 1;
    }

    public async Task ShowAsync(LocalNotification notification)
    {
        var isAllowed = await RequestPermissionAsync();
        if (!isAllowed)
        {
            throw new PermissionException("The user didn't allow notifications.");
        }

        var delay = notification.NotifyTime is null ? 0 : Math.Max(0, (notification.NotifyTime.Value - DateTimeOffset.UtcNow).TotalSeconds);
        var result = await WatchOSCallbacks.RunAsync(token => WatchOSNative.NotificationsShow(notification.Id, notification.Title, notification.Body, delay, token));
        if (result != 1)
        {
            throw new InvalidOperationException("watchOS didn't take the notification; the device log says why.");
        }
    }

    public void Cancel(int id)
    {
        WatchOSNative.NotificationsCancel(id);
    }

    public void CancelAll()
    {
        WatchOSNative.NotificationsCancelAll();
    }
}
