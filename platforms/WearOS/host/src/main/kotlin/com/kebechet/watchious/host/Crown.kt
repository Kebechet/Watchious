package com.kebechet.watchious.host

import androidx.compose.foundation.focusable
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
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.input.pointer.PointerEventPass
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.input.rotary.RotaryScrollEvent
import androidx.compose.ui.input.rotary.onRotaryScrollEvent
import androidx.wear.compose.foundation.rotary.RotaryScrollableBehavior
import androidx.wear.compose.foundation.rotary.rotaryScrollable

// What the crown (or the rotating bezel) turns on a screen: its scrolling list, or the control on it that was touched
// last (a Picker or a DigitalCrownRotation), as in Wear OS's own apps. Wear's crown handling takes a turn on its way
// down to the focused control (RotaryInputNode.onPreRotaryScrollEvent), so a list that kept its handling would take
// every turn from a control inside it: the list drops it while a control has the crown.
internal class ScreenCrown {
    val focusRequester = FocusRequester()
    var control by mutableStateOf<Any?>(null)
}

internal val LocalScreenCrown = staticCompositionLocalOf<ScreenCrown?> { null }

@Composable
internal fun rememberScreenCrown(): ScreenCrown {
    val crown = remember { ScreenCrown() }
    LaunchedEffect(crown.control) {
        if (crown.control == null) {
            crown.focusRequester.requestFocus()
        }
    }
    return crown
}

// The screen's list turns with the crown unless a control has it. A touch anywhere on the list takes the crown back;
// it is seen in the Initial pass, before a touched control sees it in the Main pass and takes the crown for itself.
internal fun Modifier.screenCrown(crown: ScreenCrown, behavior: RotaryScrollableBehavior): Modifier =
    then(if (crown.control == null) Modifier.rotaryScrollable(behavior, crown.focusRequester) else Modifier)
        .pointerInput(crown) {
            awaitEachGesture {
                awaitFirstDown(requireUnconsumed = false, pass = PointerEventPass.Initial)
                crown.control = null
            }
        }

// A Picker turns with the crown once it is touched.
@Composable
internal fun Modifier.pickerCrown(behavior: RotaryScrollableBehavior): Modifier {
    val focusRequester = rememberControlCrown()
    return rotaryScrollable(behavior, focusRequester)
        .takeCrownOnTouch(focusRequester)
}

// A control that turns a value with the crown, once it is touched; on a screen without a scrolling list nothing else
// wants the crown, so it has it from the start.
@Composable
internal fun Modifier.controlCrown(onTurn: (RotaryScrollEvent) -> Unit): Modifier {
    val focusRequester = rememberControlCrown()
    val crown = LocalScreenCrown.current
    LaunchedEffect(crown) {
        if (crown == null) {
            focusRequester.requestFocus()
        }
    }
    return onRotaryScrollEvent { event ->
        onTurn(event)
        true
    }
        .focusRequester(focusRequester)
        .focusable()
        .takeCrownOnTouch(focusRequester)
}

// A control's focus, which gives the crown back to the screen's list when the control leaves the screen.
@Composable
private fun rememberControlCrown(): FocusRequester {
    val crown = LocalScreenCrown.current
    val focusRequester = remember { FocusRequester() }
    DisposableEffect(crown) {
        onDispose {
            if (crown?.control === focusRequester) {
                crown.control = null
            }
        }
    }
    return focusRequester
}

@Composable
private fun Modifier.takeCrownOnTouch(focusRequester: FocusRequester): Modifier {
    val crown = LocalScreenCrown.current
    return pointerInput(crown) {
        awaitEachGesture {
            awaitFirstDown(requireUnconsumed = false)
            crown?.control = focusRequester
            focusRequester.requestFocus()
        }
    }
}
