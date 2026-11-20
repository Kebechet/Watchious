package com.kebechet.watchious.host;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

public interface WatchiousEventListener {
    void onEvent(long eventHandlerId, @NonNull String eventName, @Nullable String value);
}
