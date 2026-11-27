package com.kebechet.watchious.host

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.runtime.compositionLocalOf
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.drawscope.DrawScope

// Whether the watch shows the dimmed always-on screen, which C#'s IAmbientMode reports (WatchiousComposeHost.setAmbient).
internal val LocalIsAmbient = compositionLocalOf { false }

// A page's background as C# resolved it (PageBackground.cs): a color as #RRGGBBAA, or "linear x1 y1 x2 y2" or
// "radial cx cy r" followed by each stop's color and offset. Points are relative to the page, and a radial radius is a
// fraction of the page's longer side, as in MAUI.
private class PageFill(val draw: DrawScope.() -> Unit)

// The page drawn on its background, which is left black (the AppScaffold's) on the always-on screen, as Wear OS
// requires there. The same Box with or without a background keeps the page's state when the background changes.
@Composable
internal fun PageBackground(page: UiNode, content: @Composable () -> Unit) {
    val value = page.attribute("Background")
    val fill = remember(value) { parseBackground(value) }
    val isAmbient = LocalIsAmbient.current
    Box(Modifier.fillMaxSize().drawBehind { if (!isAmbient) fill?.draw?.invoke(this) }) {
        content()
    }
}

private fun parseBackground(value: String?): PageFill? {
    if (value.isNullOrEmpty()) {
        return null
    }

    val fill = parseFill(value.split(' '))
    if (fill == null) {
        reportUnknownValue(value, "Background", "Page")
    }
    return fill
}

private fun parseFill(parts: List<String>): PageFill? {
    if (parts.size == 1) {
        val color = parseColor(parts[0]) ?: return null
        return PageFill { drawRect(color) }
    }

    return when (parts[0]) {
        "linear" -> {
            if (parts.size < 5) return null
            val numbers = parts.slice(1..4).map { it.toFloatOrNull() ?: return null }
            val stops = parseStops(parts.drop(5)) ?: return null
            PageFill {
                drawRect(
                    Brush.linearGradient(
                        colorStops = stops,
                        start = Offset(numbers[0] * size.width, numbers[1] * size.height),
                        end = Offset(numbers[2] * size.width, numbers[3] * size.height),
                    ),
                )
            }
        }
        "radial" -> {
            if (parts.size < 4) return null
            val numbers = parts.slice(1..3).map { it.toFloatOrNull() ?: return null }
            val stops = parseStops(parts.drop(4)) ?: return null
            PageFill {
                drawRect(
                    Brush.radialGradient(
                        colorStops = stops,
                        center = Offset(numbers[0] * size.width, numbers[1] * size.height),
                        // Android refuses a radius of 0.
                        radius = maxOf(numbers[2] * maxOf(size.width, size.height), 0.5f),
                    ),
                )
            }
        }
        else -> null
    }
}

private fun parseStops(parts: List<String>): Array<Pair<Float, Color>>? {
    if (parts.size < 4 || parts.size % 2 != 0) {
        return null
    }

    return parts.chunked(2).map { (color, offset) ->
        (offset.toFloatOrNull() ?: return null) to (parseColor(color) ?: return null)
    }.toTypedArray()
}

private fun parseColor(hex: String): Color? {
    if (hex.length != 9 || hex[0] != '#') {
        return null
    }

    val value = hex.substring(1).toLongOrNull(16) ?: return null
    return Color(
        red = ((value shr 24) and 0xFF) / 255f,
        green = ((value shr 16) and 0xFF) / 255f,
        blue = ((value shr 8) and 0xFF) / 255f,
        alpha = (value and 0xFF) / 255f,
    )
}
