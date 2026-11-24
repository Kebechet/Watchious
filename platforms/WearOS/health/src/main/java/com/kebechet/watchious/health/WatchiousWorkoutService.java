package com.kebechet.watchious.health;

import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.os.Build;
import android.os.IBinder;

import androidx.annotation.Nullable;
import androidx.core.app.NotificationCompat;
import androidx.wear.ongoing.OngoingActivity;

// Keeps the app's process running while the user is away from a running workout, as Wear OS requires for an exercise
// (a foreground service of type health), and shows an ongoing activity on the watch face that brings the user back.
public final class WatchiousWorkoutService extends Service {
    private static final String CHANNEL_ID = "watchious_workout";
    private static final int NOTIFICATION_ID = 7301;

    static void start(Context context) {
        context.startForegroundService(new Intent(context, WatchiousWorkoutService.class));
    }

    static void stop(Context context) {
        context.stopService(new Intent(context, WatchiousWorkoutService.class));
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        NotificationManager notifications = getSystemService(NotificationManager.class);
        notifications.createNotificationChannel(
            new NotificationChannel(CHANNEL_ID, "Workout", NotificationManager.IMPORTANCE_DEFAULT));

        Intent launch = getPackageManager().getLaunchIntentForPackage(getPackageName());
        PendingIntent open = PendingIntent.getActivity(
            this, 0, launch, PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);
        int icon = getApplicationInfo().icon != 0 ? getApplicationInfo().icon : android.R.drawable.ic_media_play;
        NotificationCompat.Builder notification = new NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(icon)
            .setContentTitle(getApplicationInfo().loadLabel(getPackageManager()))
            .setContentText("Workout in progress")
            .setContentIntent(open)
            .setOngoing(true)
            .setCategory(NotificationCompat.CATEGORY_WORKOUT)
            .setVisibility(NotificationCompat.VISIBILITY_PUBLIC);
        new OngoingActivity.Builder(this, NOTIFICATION_ID, notification)
            .setStaticIcon(icon)
            .setTouchIntent(open)
            .build()
            .apply(this);

        // The health type exists from Android 14; before, the manifest's type applies.
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            startForeground(NOTIFICATION_ID, notification.build(), ServiceInfo.FOREGROUND_SERVICE_TYPE_HEALTH);
        } else {
            startForeground(NOTIFICATION_ID, notification.build());
        }
        return START_NOT_STICKY;
    }

    @Nullable
    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }
}
