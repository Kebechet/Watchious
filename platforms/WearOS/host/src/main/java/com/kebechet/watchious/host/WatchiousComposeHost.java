package com.kebechet.watchious.host;

import android.app.Activity;
import android.view.View;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

// The public API that .NET binds. It is written in Java because the Compose compiler adds a `$stable` field to
// every Kotlin class, which the .NET binding generator warns about.
// It works with a plain android.app.Activity, so the .NET app's activity needs no AndroidX base class. It uses
// platform types only: a public class implementing an AndroidX interface would be dropped by the .NET binding
// generator, which cannot see the unbound AndroidX types.
public final class WatchiousComposeHost {
    private final ComposeHost host;

    public WatchiousComposeHost(@NonNull Activity activity) {
        host = new ComposeHost(activity);
    }

    @NonNull
    public View getView() {
        return host.getView();
    }

    // One batch of Watchious patches (protocol v2). Callable from any thread; batches apply in the order they
    // arrive.
    public void render(@NonNull byte[] patches) {
        host.render(patches);
    }

    // Goes back a page if there is one. For Activity.onBackPressed on Android 12L and older; newer versions
    // deliver the back button to the host directly.
    public boolean handleBack() {
        return host.handleBack();
    }

    public void setEventListener(@Nullable WatchiousEventListener listener) {
        host.setEventListener(listener);
    }
}
