package com.kebechet.watchious.host

import android.content.pm.ApplicationInfo
import android.util.Log
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.wrapContentWidth
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.key
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.wear.compose.foundation.rotary.RotaryScrollableDefaults
import androidx.wear.compose.material3.AppScaffold
import androidx.wear.compose.material3.Button
import androidx.wear.compose.material3.MaterialTheme
import androidx.wear.compose.material3.ScreenScaffold
import androidx.wear.compose.material3.SwitchButton
import androidx.wear.compose.material3.Text

internal typealias EventDispatch = (eventHandlerId: Long, eventName: String, value: String?) -> Unit

@Composable
internal fun WatchiousScreen(root: UiNode, dispatch: EventDispatch) {
    MaterialTheme {
        AppScaffold {
            RenderNodes(root.children, dispatch)
        }
    }
}

@Composable
internal fun RenderNodes(nodes: List<UiNode>, dispatch: EventDispatch, isInRow: Boolean = false) {
    // The node id keeps a control's Compose state (and animations) with its node when siblings are inserted,
    // removed or moved.
    nodes.forEach { node -> key(node.id) { RenderNode(node, dispatch, isInRow) } }
}

@Composable
internal fun RenderNode(node: UiNode, dispatch: EventDispatch, isInRow: Boolean) {
    // Like MAUI's default HorizontalOptions.Fill: a control spans the width of a vertical stack, but takes only
    // the width it needs in an HStack, where filling would push its siblings out.
    val fillWidth = if (isInRow) Modifier else Modifier.fillMaxWidth()
    when (node.type) {
        "NavigationStack" -> NavigationStack(node, dispatch)
        "ScrollView" -> WatchScrollView(node, dispatch)
        "VStack" -> Column(
            verticalArrangement = Arrangement.spacedBy(node.spacing().dp),
            horizontalAlignment = node.horizontalAlignment(),
        ) {
            RenderNodes(node.children, dispatch)
        }
        "HStack" -> Row(
            horizontalArrangement = Arrangement.spacedBy(node.spacing().dp),
            verticalAlignment = node.verticalAlignment(),
        ) {
            RenderNodes(node.children, dispatch, isInRow = true)
        }
        "Label" -> Text(
            text = node.joinedText(),
            style = labelStyle(node.attribute("Style")),
            textAlign = TextAlign.Center,
        )
        "Button" -> Button(
            onClick = { node.dispatch("click", null, dispatch) },
            modifier = fillWidth,
            enabled = node.attribute("IsEnabled") != "false",
            label = {
                Text(
                    text = node.attribute("Text").orEmpty(),
                    modifier = Modifier.fillMaxWidth(),
                    textAlign = TextAlign.Center,
                )
            },
        )
        "List" -> WatchList(node, dispatch)
        "Image" -> WatchImage(node)
        "ProgressView" -> WatchProgressView(node, fillWidth)
        "Picker" -> WatchPicker(node, dispatch)
        "TextField" -> WatchTextField(node, dispatch, fillWidth)
        "Toggle" -> {
            val text = node.attribute("Text")
            SwitchButton(
                checked = node.attribute("IsOn") == "true",
                onCheckedChange = { isOn -> node.dispatch("toggled", isOn.toString(), dispatch) },
                modifier = if (text.isNullOrEmpty()) Modifier.wrapContentWidth() else fillWidth,
                label = { if (!text.isNullOrEmpty()) Text(text) },
            )
        }
        // Blank text is the whitespace between controls in the Razor markup.
        UiNode.TEXT_TYPE -> node.text?.takeIf { it.isNotBlank() }?.let { Text(it) }
        else -> UnknownNode(node, dispatch, isInRow)
    }
}

// A control this host has no view for: a misspelled element in the Razor markup, or a control that C# has and this host
// doesn't yet. Its children are still shown; a debug build also shows a warning in its place.
@Composable
private fun UnknownNode(node: UiNode, dispatch: EventDispatch, isInRow: Boolean) {
    LaunchedEffect(node.type) {
        Log.w(LOG_TAG, "Unknown control ${node.type}: this host has no view for it")
    }
    val isDebuggable = (LocalContext.current.applicationInfo.flags and ApplicationInfo.FLAG_DEBUGGABLE) != 0
    if (!isDebuggable) {
        RenderNodes(node.children, dispatch, isInRow)
        return
    }
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Text("⚠️ ${node.type}", color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall)
        RenderNodes(node.children, dispatch, isInRow)
    }
}

@Composable
private fun WatchScrollView(node: UiNode, dispatch: EventDispatch) {
    val scrollState = rememberScrollState()
    val crown = rememberScreenCrown()
    ScreenScaffold(scrollState = scrollState) { contentPadding: PaddingValues ->
        CompositionLocalProvider(LocalScreenCrown provides crown) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .screenCrown(crown, RotaryScrollableDefaults.behavior(scrollState))
                    .verticalScroll(scrollState)
                    .padding(contentPadding)
                    .padding(horizontal = 12.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                RenderNodes(node.children, dispatch)
            }
        }
    }
}

@Composable
private fun labelStyle(style: String?): TextStyle = when (style) {
    "Title" -> MaterialTheme.typography.titleMedium
    "Headline" -> MaterialTheme.typography.titleSmall
    "Caption" -> MaterialTheme.typography.bodySmall
    "Body", null -> MaterialTheme.typography.bodyMedium
    else -> {
        reportUnknownValue(style, "Style", "Label")
        MaterialTheme.typography.bodyMedium
    }
}

// A name or value this host doesn't know means it and the C# controls don't match
// (https://github.com/Kebechet/Watchious/issues/3). An unknown value is only logged, and the control uses its default;
// an unknown control is shown as a warning in a debuggable build (UnknownNode).
internal fun reportUnknownValue(value: String?, attribute: String, type: String) {
    Log.w(LOG_TAG, "Unknown $attribute value $value on $type")
}

// Without Spacing a stack uses the platform's default, as SwiftUI does.
private const val DEFAULT_SPACING = 4f

private fun UiNode.spacing(): Float = attribute("Spacing")?.toFloatOrNull() ?: DEFAULT_SPACING

private fun UiNode.horizontalAlignment(): Alignment.Horizontal = when (val alignment = attribute("Alignment")) {
    "Leading" -> Alignment.Start
    "Trailing" -> Alignment.End
    "Center", null -> Alignment.CenterHorizontally
    else -> {
        reportUnknownValue(alignment, "Alignment", type)
        Alignment.CenterHorizontally
    }
}

private fun UiNode.verticalAlignment(): Alignment.Vertical = when (val alignment = attribute("Alignment")) {
    "Top" -> Alignment.Top
    "Bottom" -> Alignment.Bottom
    "Center", null -> Alignment.CenterVertically
    else -> {
        reportUnknownValue(alignment, "Alignment", type)
        Alignment.CenterVertically
    }
}

private fun UiNode.joinedText(): String =
    children.filter { it.type == UiNode.TEXT_TYPE }.joinToString("") { it.text.orEmpty() }

internal fun UiNode.dispatch(eventName: String, value: String?, dispatch: EventDispatch) {
    val eventHandlerId = events[eventName] ?: return
    dispatch(eventHandlerId, eventName, value)
}
