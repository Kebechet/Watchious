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
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicInteger;

// Asks for runtime permissions on behalf of Watchious.Health, which has no activity of its own to get the answer
// in: a transparent activity shows the system's dialogs and reports the denied permissions.
public final class WatchiousPermissionActivity extends Activity {
    public interface Callback {
        void onResult(@NonNull String[] deniedPermissions);
    }

    private static final String PermissionsExtra = "permissions";
    private static final String RequestIdExtra = "requestId";
    private static final AtomicInteger lastRequestId = new AtomicInteger();
    // Each request keeps its own callback, so two requests at once both get their answer.
    private static final Map<Integer, Callback> pendingCallbacks = new ConcurrentHashMap<>();

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

        int requestId = lastRequestId.incrementAndGet();
        pendingCallbacks.put(requestId, callback);
        Intent intent = new Intent(context, WatchiousPermissionActivity.class)
            .putExtra(PermissionsExtra, missing.toArray(new String[0]))
            .putExtra(RequestIdExtra, requestId)
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        context.startActivity(intent);
    }

    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        // Recreated (after a configuration change), the activity gets the answer to the request it already made.
        if (savedInstanceState == null) {
            requestPermissions(requestedPermissions(), 1);
        }
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, @NonNull String[] permissions, @NonNull int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        finish();
        reportResult();
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        // Closed without an answer.
        if (isFinishing()) {
            reportResult();
        }
    }

    // The answer comes from the granted permissions, not from onRequestPermissionsResult's arrays: an interrupted
    // request reports them empty, which would read as all granted.
    private void reportResult() {
        Callback callback = pendingCallbacks.remove(getIntent().getIntExtra(RequestIdExtra, 0));
        if (callback == null) {
            return;
        }

        List<String> denied = new ArrayList<>();
        for (String permission : requestedPermissions()) {
            if (checkSelfPermission(permission) != PackageManager.PERMISSION_GRANTED) {
                denied.add(permission);
            }
        }
        callback.onResult(denied.toArray(new String[0]));
    }

    @NonNull
    private String[] requestedPermissions() {
        String[] permissions = getIntent().getStringArrayExtra(PermissionsExtra);
        return permissions == null ? new String[0] : permissions;
    }
}
