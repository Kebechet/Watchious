package com.kebechet.watchious.host;

import android.app.Activity;
import android.view.View;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

// The public API that .NET binds. It is written in Java because the Compose compiler adds a `$stable` field to
// every Kotlin class, which the .NET binding generator warns about.
// It works with a plain android.app.Activity, so the .NET app needs no AndroidX bindings. It uses platform types
// only: a public class implementing an AndroidX interface would be dropped by the .NET binding generator, which
// cannot see the unbound AndroidX types.
public final class WatchiousComposeHost {
    private final ComposeHost host;

    public WatchiousComposeHost(@NonNull Activity activity) {
        host = new ComposeHost(activity);
    }

    @NonNull
    public View getView() {
        return host.getView();
    }

    public void render(@NonNull String uiTreeJson) {
        host.render(uiTreeJson);
    }

    public void setEventListener(@Nullable WatchiousEventListener listener) {
        host.setEventListener(listener);
    }
}
