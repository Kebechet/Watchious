package com.kebechet.watchious.host

import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.input.pointer.PointerEventPass
import androidx.compose.ui.input.pointer.pointerInput
import androidx.wear.compose.foundation.rotary.RotaryScrollableBehavior
import androidx.wear.compose.foundation.rotary.rotaryScrollable

// What the crown (or the rotating bezel) turns on a screen: its scrolling list, or the Picker on it that was touched
// last, as in Wear OS's own apps. Wear's crown handling takes a turn on its way down to the focused control
// (RotaryInputNode.onPreRotaryScrollEvent), so a list that kept its handling would take every turn from a Picker inside
// it: the list drops it while a Picker has the crown.
internal class ScreenCrown {
    val focusRequester = FocusRequester()
    var picker by mutableStateOf<Any?>(null)
}

internal val LocalScreenCrown = staticCompositionLocalOf<ScreenCrown?> { null }

@Composable
internal fun rememberScreenCrown(): ScreenCrown {
    val crown = remember { ScreenCrown() }
    LaunchedEffect(crown.picker) {
        if (crown.picker == null) {
            crown.focusRequester.requestFocus()
        }
    }
    return crown
}

// The screen's list turns with the crown unless a Picker has it. A touch anywhere on the list takes the crown back; it
// is seen in the Initial pass, before a touched Picker sees it in the Main pass and takes the crown for itself.
internal fun Modifier.screenCrown(crown: ScreenCrown, behavior: RotaryScrollableBehavior): Modifier =
    then(if (crown.picker == null) Modifier.rotaryScrollable(behavior, crown.focusRequester) else Modifier)
        .pointerInput(crown) {
            awaitEachGesture {
                awaitFirstDown(requireUnconsumed = false, pass = PointerEventPass.Initial)
                crown.picker = null
            }
        }

// A Picker turns with the crown once it is touched.
@Composable
internal fun Modifier.pickerCrown(behavior: RotaryScrollableBehavior): Modifier {
    val crown = LocalScreenCrown.current
    val focusRequester = remember { FocusRequester() }
    DisposableEffect(crown) {
        onDispose {
            if (crown?.picker === focusRequester) {
                crown.picker = null
            }
        }
    }
    return rotaryScrollable(behavior, focusRequester)
        .pointerInput(crown) {
            awaitEachGesture {
                awaitFirstDown(requireUnconsumed = false)
                crown?.picker = focusRequester
                focusRequester.requestFocus()
            }
        }
}
