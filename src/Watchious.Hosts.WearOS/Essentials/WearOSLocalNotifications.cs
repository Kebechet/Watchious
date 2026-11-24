using Android;
using Android.App;
using Android.Content;
using Watchious.Essentials;

[assembly: UsesPermission(Manifest.Permission.PostNotifications)]

namespace Watchious.Hosts.WearOS.Essentials;

// Notifications on the app's own channel; a scheduled one is posted by WatchiousNotificationReceiver when its alarm
// fires. An exact alarm needs the user's consent from Android 14 (the app may declare SCHEDULE_EXACT_ALARM); without it
// the alarm is inexact and Android may deliver it a little late.
internal sealed class WearOSLocalNotifications(Context context, WearOSPermissions permissions) : ILocalNotifications
{
    private const string ScheduledFile = "watchious_notifications";
    private const string ScheduledIds = "scheduled_ids";

    public bool IsSupported => true;

    public Task<bool> RequestPermissionAsync()
    {
        return OperatingSystem.IsAndroidVersionAtLeast(33)
            ? permissions.RequestAnyAsync(Manifest.Permission.PostNotifications)
            : Task.FromResult(true);
    }

    public async Task ShowAsync(LocalNotification notification)
    {
        var isAllowed = await RequestPermissionAsync();
        if (!isAllowed)
        {
            throw new PermissionException("The user didn't allow notifications.");
        }

        if (notification.NotifyTime is null || notification.NotifyTime <= DateTimeOffset.UtcNow)
        {
            WatchiousNotificationReceiver.Post(context, notification.Id, notification.Title, notification.Body);
            return;
        }

        var alarms = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        var trigger = notification.NotifyTime.Value.ToUnixTimeMilliseconds();
        var pending = WatchiousNotificationReceiver.CreateAlarmIntent(context, notification.Id, notification.Title, notification.Body);
        if (!OperatingSystem.IsAndroidVersionAtLeast(31) || alarms.CanScheduleExactAlarms())
        {
            alarms.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, trigger, pending);
        }
        else
        {
            alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, trigger, pending);
        }

        UpdateScheduled(ids => ids.Add(notification.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    public void Cancel(int id)
    {
        CancelAlarm(id);
        UpdateScheduled(ids => ids.Remove(id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        NotificationManager.FromContext(context)?.Cancel(id);
    }

    public void CancelAll()
    {
        var scheduled = ReadScheduled(context);
        foreach (var id in scheduled)
        {
            if (int.TryParse(id, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var notificationId))
            {
                CancelAlarm(notificationId);
            }
        }

        UpdateScheduled(ids => ids.Clear());
        NotificationManager.FromContext(context)?.CancelAll();
    }

    internal static HashSet<string> ReadScheduled(Context context)
    {
        var preferences = context.GetSharedPreferences(ScheduledFile, FileCreationMode.Private)!;
        return [.. preferences.GetStringSet(ScheduledIds, null) ?? []];
    }

    internal static void WriteScheduled(Context context, HashSet<string> ids)
    {
        var preferences = context.GetSharedPreferences(ScheduledFile, FileCreationMode.Private)!;
        using var editor = preferences.Edit()!;
        editor.PutStringSet(ScheduledIds, ids)!.Apply();
    }

    private void CancelAlarm(int id)
    {
        var alarms = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        var pending = WatchiousNotificationReceiver.FindAlarmIntent(context, id);
        if (pending is not null)
        {
            alarms.Cancel(pending);
            pending.Cancel();
        }
    }

    private void UpdateScheduled(Action<HashSet<string>> update)
    {
        var ids = ReadScheduled(context);
        update(ids);
        WriteScheduled(context, ids);
    }
}

// Posts a scheduled notification when its alarm fires, also when the app isn't running.
[BroadcastReceiver(Exported = false)]
internal sealed class WatchiousNotificationReceiver : BroadcastReceiver
{
    private const string ChannelId = "watchious_notifications";
    private const string ExtraId = "watchious_notification_id";
    private const string ExtraTitle = "watchious_notification_title";
    private const string ExtraBody = "watchious_notification_body";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
        {
            return;
        }

        var id = intent.GetIntExtra(ExtraId, 0);
        Post(context, id, intent.GetStringExtra(ExtraTitle) ?? "", intent.GetStringExtra(ExtraBody));
        var scheduled = WearOSLocalNotifications.ReadScheduled(context);
        scheduled.Remove(id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        WearOSLocalNotifications.WriteScheduled(context, scheduled);
    }

    public static void Post(Context context, int id, string title, string? body)
    {
        var manager = NotificationManager.FromContext(context)!;
        if (manager.GetNotificationChannel(ChannelId) is null)
        {
            using var channel = new NotificationChannel(ChannelId, "Notifications", NotificationImportance.High);
            manager.CreateNotificationChannel(channel);
        }

        // Android refuses a notification without an icon; an app without one of its own gets a system icon.
        var icon = context.ApplicationInfo!.Icon != 0 ? context.ApplicationInfo.Icon : Android.Resource.Drawable.IcDialogInfo;
        using var launch = context.PackageManager!.GetLaunchIntentForPackage(context.PackageName!)!;
        var open = PendingIntent.GetActivity(context, id, launch, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        using var builder = new Notification.Builder(context, ChannelId)
            .SetSmallIcon(icon)!
            .SetContentTitle(title)!
            .SetContentText(body)!
            .SetContentIntent(open)!
            .SetAutoCancel(true)!;
        manager.Notify(id, builder.Build());
    }

    public static PendingIntent CreateAlarmIntent(Context context, int id, string title, string? body)
    {
        using var intent = new Intent(context, typeof(WatchiousNotificationReceiver));
        intent.PutExtra(ExtraId, id);
        intent.PutExtra(ExtraTitle, title);
        intent.PutExtra(ExtraBody, body);
        return PendingIntent.GetBroadcast(context, id, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
    }

    public static PendingIntent? FindAlarmIntent(Context context, int id)
    {
        using var intent = new Intent(context, typeof(WatchiousNotificationReceiver));
        return PendingIntent.GetBroadcast(context, id, intent, PendingIntentFlags.Immutable | PendingIntentFlags.NoCreate);
    }
}
