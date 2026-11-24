package com.kebechet.watchious.host

import android.app.Activity
import android.app.Application
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.util.Log
import android.view.View
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.snapshotFlow
import androidx.compose.runtime.withFrameNanos
import androidx.compose.ui.platform.ComposeView
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleOwner
import androidx.lifecycle.LifecycleRegistry
import androidx.lifecycle.setViewTreeLifecycleOwner
import androidx.savedstate.SavedStateRegistry
import androidx.savedstate.SavedStateRegistryController
import androidx.savedstate.SavedStateRegistryOwner
import androidx.savedstate.setViewTreeSavedStateRegistryOwner
import java.util.concurrent.ConcurrentLinkedQueue
import kotlinx.coroutines.flow.first

internal const val LOG_TAG = "Watchious"

internal class ComposeHost(activity: Activity) {
    private val mainHandler = Handler(Looper.getMainLooper())
    private val owner = HostOwner(activity)
    private val tree = UiTree()
    private val backNavigation = BackNavigation(activity)
    private val pendingPatches = ConcurrentLinkedQueue<ByteArray>()
    private var eventListener: WatchiousEventListener? = null

    val view: View = ComposeView(activity).apply {
        setViewTreeLifecycleOwner(owner)
        setViewTreeSavedStateRegistryOwner(owner)
        setContent {
            CompositionLocalProvider(LocalBackNavigation provides backNavigation) {
                WatchiousScreen(tree.root, ::dispatch)
            }
            LaunchedEffect(Unit) { reportFullyDrawnAfterFirstTree(activity) }
        }
    }

    // The first frame Android times on launch is drawn before .NET has rendered anything; reportFullyDrawn marks
    // when the first Watchious screen is on the display ("Fully drawn" in logcat).
    private suspend fun reportFullyDrawnAfterFirstTree(activity: Activity) {
        snapshotFlow { tree.root.children.isNotEmpty() }.first { it }
        // The first wait can resume at the start of the frame that composes the tree, before it is drawn; the
        // second resumes only after that frame was drawn.
        withFrameNanos { }
        withFrameNanos { }
        activity.reportFullyDrawn()
    }

    // Batches arrive from whichever thread .NET rendered on and must be applied in order, so they all go through
    // one queue that only the main thread drains.
    fun render(patches: ByteArray) {
        pendingPatches.add(patches)
        if (Looper.myLooper() == Looper.getMainLooper()) {
            applyPendingPatches()
        } else {
            mainHandler.post(::applyPendingPatches)
        }
    }

    // Until a batch is confirmed, .NET keeps the event handler ids it replaced: the controls on the display still
    // send them.
    private fun applyPendingPatches() {
        while (true) {
            val patches = pendingPatches.poll() ?: return
            try {
                tree.apply(patches)
            } catch (exception: Exception) {
                Log.e(LOG_TAG, "Invalid Watchious patches", exception)
            }
            eventListener?.onPatchesApplied()
        }
    }

    fun handleBack(): Boolean = backNavigation.handleBack()

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
