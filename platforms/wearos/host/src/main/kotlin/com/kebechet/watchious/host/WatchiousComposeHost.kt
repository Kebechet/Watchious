package com.kebechet.watchious.host

import android.app.Activity
import android.app.Application
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.view.View
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.platform.ComposeView
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleOwner
import androidx.lifecycle.LifecycleRegistry
import androidx.lifecycle.setViewTreeLifecycleOwner
import androidx.savedstate.SavedStateRegistry
import androidx.savedstate.SavedStateRegistryController
import androidx.savedstate.SavedStateRegistryOwner
import androidx.savedstate.setViewTreeSavedStateRegistryOwner

// Works with a plain android.app.Activity, so the .NET app needs no AndroidX bindings. The public API uses
// platform types only: a public class implementing an AndroidX interface would be dropped by the .NET
// binding generator, which cannot see the unbound AndroidX types.
class WatchiousComposeHost(activity: Activity) {
    private val mainHandler = Handler(Looper.getMainLooper())
    private val owner = HostOwner(activity)
    private var tree by mutableStateOf(emptyList<UiNode>())
    private var eventListener: WatchiousEventListener? = null

    val view: View = ComposeView(activity).apply {
        setViewTreeLifecycleOwner(owner)
        setViewTreeSavedStateRegistryOwner(owner)
        setContent { WatchiousScreen(tree, ::dispatch) }
    }

    fun render(uiTreeJson: String) {
        val nodes = UiNode.parseTree(uiTreeJson)
        if (Looper.myLooper() == Looper.getMainLooper()) {
            tree = nodes
        } else {
            mainHandler.post { tree = nodes }
        }
    }

    fun setEventListener(listener: WatchiousEventListener?) {
        eventListener = listener
    }

    private fun dispatch(eventHandlerId: Long, eventName: String, value: String?) {
        eventListener?.onEvent(eventHandlerId, eventName, value)
    }

    private inner class HostOwner(activity: Activity) :
        LifecycleOwner,
        SavedStateRegistryOwner,
        Application.ActivityLifecycleCallbacks {
        private val lifecycleRegistry = LifecycleRegistry(this)
        private val savedStateRegistryController = SavedStateRegistryController.create(this)

        override val lifecycle: Lifecycle
            get() = lifecycleRegistry

        override val savedStateRegistry: SavedStateRegistry
            get() = savedStateRegistryController.savedStateRegistry

        init {
            savedStateRegistryController.performRestore(null)
            lifecycleRegistry.currentState = Lifecycle.State.CREATED
            activity.registerActivityLifecycleCallbacks(this)
        }

        override fun onActivityCreated(activity: Activity, savedInstanceState: Bundle?) = Unit

        override fun onActivityStarted(activity: Activity) {
            lifecycleRegistry.currentState = Lifecycle.State.STARTED
        }

        override fun onActivityResumed(activity: Activity) {
            lifecycleRegistry.currentState = Lifecycle.State.RESUMED
        }

        override fun onActivityPaused(activity: Activity) {
            lifecycleRegistry.currentState = Lifecycle.State.STARTED
        }

        override fun onActivityStopped(activity: Activity) {
            lifecycleRegistry.currentState = Lifecycle.State.CREATED
        }

        override fun onActivitySaveInstanceState(activity: Activity, outState: Bundle) = Unit

        override fun onActivityDestroyed(activity: Activity) {
            lifecycleRegistry.currentState = Lifecycle.State.DESTROYED
            activity.unregisterActivityLifecycleCallbacks(this)
            eventListener = null
        }
    }
}
