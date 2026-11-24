namespace Watchious.Essentials;

// Notifications the watch app posts itself, now or at a time (a rest timer, a reminder), without a server. They show
// while the app is open too. Showing one asks for the permission first if the user hasn't answered yet, and throws
// PermissionException when it's denied.
public interface ILocalNotifications
{
    bool IsSupported { get; }

    Task<bool> RequestPermissionAsync();

    // A notification with the Id of an earlier one replaces it.
    Task ShowAsync(LocalNotification notification);

    void Cancel(int id);

    void CancelAll();
}

// NotifyTime null shows it now. Wear OS may deliver a scheduled one a little late when the app may not set exact alarms
// (Android 14 and later ask the user for that).
public sealed record LocalNotification(int Id, string Title, string? Body = null, DateTimeOffset? NotifyTime = null);
