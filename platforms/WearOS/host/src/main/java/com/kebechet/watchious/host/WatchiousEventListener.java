package com.kebechet.watchious.host;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

public interface WatchiousEventListener {
    void onEvent(long eventHandlerId, @NonNull String eventName, @Nullable String value);

    // Once per batch passed to WatchiousComposeHost.render, in order, when it is on the display.
    void onPatchesApplied();
}
