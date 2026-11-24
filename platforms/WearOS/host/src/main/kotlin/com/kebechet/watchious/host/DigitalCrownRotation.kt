package com.kebechet.watchious.host

import android.os.SystemClock
import android.view.HapticFeedbackConstants
import android.view.ViewConfiguration
import androidx.compose.foundation.layout.Column
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableDoubleStateOf
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalView
import kotlinx.coroutines.delay
import kotlin.math.roundToLong

// While the crown turns, C#'s answers to earlier steps arrive after newer steps and would set the value back: the
// control keeps its own value until the crown has rested this long, then follows C# again.
private const val CROWN_REST_MILLIS = 500L

// SwiftUI's digitalCrownRotation: the crown or the rotating bezel turns Value from From through Through, one step of By
// per unit of the rotary axis (a detent of a rotating bezel), clockwise up.
@Composable
internal fun WatchDigitalCrownRotation(node: UiNode, dispatch: EventDispatch, isInRow: Boolean) {
    val from = node.attribute("From")?.toDoubleOrNull() ?: 0.0
    val through = maxOf(node.attribute("Through")?.toDoubleOrNull() ?: from, from)
    val by = node.attribute("By")?.toDoubleOrNull()?.takeIf { it > 0 } ?: 1.0
    val sentValue = node.attribute("Value")?.toDoubleOrNull() ?: from

    var value by remember { mutableDoubleStateOf(sentValue) }
    var lastTurnAt by remember { mutableLongStateOf(0L) }
    var partialStep by remember { mutableFloatStateOf(0f) }
    LaunchedEffect(sentValue, lastTurnAt) {
        val rest = lastTurnAt + CROWN_REST_MILLIS - SystemClock.uptimeMillis()
        if (rest > 0) {
            delay(rest)
        }
        value = sentValue
    }

    val view = LocalView.current
    val pixelsPerStep = remember(view) { ViewConfiguration.get(view.context).scaledVerticalScrollFactor }
    Column(
        modifier = Modifier.controlCrown { event ->
            partialStep += event.verticalScrollPixels / pixelsPerStep
            val steps = partialStep.toInt()
            if (steps == 0) {
                return@controlCrown
            }
            partialStep -= steps
            lastTurnAt = SystemClock.uptimeMillis()
            val next = step(value, steps, from, through, by)
            if (next != value) {
                value = next
                view.performHapticFeedback(HapticFeedbackConstants.CLOCK_TICK)
                node.dispatch("changed", next.toString(), dispatch)
            }
        },
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        RenderNodes(node.children, dispatch, isInRow)
    }
}

// The value on By's grid from From, within the range. Rounded to nine decimals: steps of 0.1 would otherwise add up to
// values like 0.30000000000000004.
private fun step(value: Double, steps: Int, from: Double, through: Double, by: Double): Double {
    val index = Math.round((value - from) / by) + steps
    val next = (from + index * by).coerceIn(from, through)
    return (next * 1e9).roundToLong() / 1e9
}
