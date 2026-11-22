package com.kebechet.watchious.health;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Bundle;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import java.util.ArrayList;
import java.util.List;

// Asks for runtime permissions on behalf of Watchious.Health, which has no activity of its own to get the answer
// in: a transparent activity shows the system's dialogs and reports the denied permissions.
public final class WatchiousPermissionActivity extends Activity {
    public interface Callback {
        void onResult(@NonNull String[] deniedPermissions);
    }

    private static final String PermissionsExtra = "permissions";

    @Nullable
    private static Callback pendingCallback;

    public static void request(@NonNull Context context, @NonNull String[] permissions, @NonNull Callback callback) {
        List<String> missing = new ArrayList<>();
        for (String permission : permissions) {
            if (context.checkSelfPermission(permission) != PackageManager.PERMISSION_GRANTED) {
                missing.add(permission);
            }
        }
        if (missing.isEmpty()) {
            callback.onResult(new String[0]);
            return;
        }

        pendingCallback = callback;
        Intent intent = new Intent(context, WatchiousPermissionActivity.class)
            .putExtra(PermissionsExtra, missing.toArray(new String[0]))
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        context.startActivity(intent);
    }

    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        String[] permissions = getIntent().getStringArrayExtra(PermissionsExtra);
        requestPermissions(permissions == null ? new String[0] : permissions, 1);
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, @NonNull String[] permissions, @NonNull int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        List<String> denied = new ArrayList<>();
        for (int index = 0; index < permissions.length; index++) {
            if (index >= grantResults.length || grantResults[index] != PackageManager.PERMISSION_GRANTED) {
                denied.add(permissions[index]);
            }
        }
        Callback callback = pendingCallback;
        pendingCallback = null;
        finish();
        if (callback != null) {
            callback.onResult(denied.toArray(new String[0]));
        }
    }
}
