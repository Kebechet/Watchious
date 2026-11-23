package com.kebechet.watchious.host

import android.app.Activity
import android.os.Build
import android.window.OnBackInvokedCallback
import android.window.OnBackInvokedDispatcher
import androidx.compose.runtime.staticCompositionLocalOf

// The back button goes back a page while there is one, and leaves the app on the root page. Android 13+ delivers
// it to an OnBackInvokedCallback (always so for apps built for Android 16 on Android 16, where onBackPressed is no
// longer called); older versions call Activity.onBackPressed, which WatchiousActivity forwards to handleBack.
internal class BackNavigation(private val activity: Activity) {
    private var onBack: (() -> Unit)? = null
    private var callback: Any? = null

    fun enable(handler: () -> Unit) {
        onBack = handler
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU && callback == null) {
            val backCallback = OnBackInvokedCallback { onBack?.invoke() }
            activity.onBackInvokedDispatcher.registerOnBackInvokedCallback(
                OnBackInvokedDispatcher.PRIORITY_DEFAULT,
                backCallback,
            )
            callback = backCallback
        }
    }

    fun disable() {
        onBack = null
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            (callback as OnBackInvokedCallback?)?.let(activity.onBackInvokedDispatcher::unregisterOnBackInvokedCallback)
            callback = null
        }
    }

    fun handleBack(): Boolean {
        val handler = onBack ?: return false
        handler()
        return true
    }
}

internal val LocalBackNavigation = staticCompositionLocalOf<BackNavigation?> { null }
