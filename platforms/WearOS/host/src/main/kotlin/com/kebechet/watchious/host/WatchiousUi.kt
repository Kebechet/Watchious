package com.kebechet.watchious.host

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
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.key
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.wear.compose.foundation.rotary.RotaryScrollableDefaults
import androidx.wear.compose.foundation.rotary.rotaryScrollable
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
        else -> RenderNodes(node.children, dispatch, isInRow)
    }
}

@Composable
private fun WatchScrollView(node: UiNode, dispatch: EventDispatch) {
    val scrollState = rememberScrollState()
    val focusRequester = remember { FocusRequester() }
    ScreenScaffold(scrollState = scrollState) { contentPadding: PaddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .rotaryScrollable(RotaryScrollableDefaults.behavior(scrollState), focusRequester)
                .verticalScroll(scrollState)
                .padding(contentPadding)
                .padding(horizontal = 12.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            RenderNodes(node.children, dispatch)
        }
    }
    LaunchedEffect(Unit) { focusRequester.requestFocus() }
}

@Composable
private fun labelStyle(style: String?): TextStyle = when (style) {
    "Title" -> MaterialTheme.typography.titleMedium
    "Headline" -> MaterialTheme.typography.titleSmall
    "Caption" -> MaterialTheme.typography.bodySmall
    else -> MaterialTheme.typography.bodyMedium
}

// Without Spacing a stack uses the platform's default, as SwiftUI does.
private const val DEFAULT_SPACING = 4f

private fun UiNode.spacing(): Float = attribute("Spacing")?.toFloatOrNull() ?: DEFAULT_SPACING

private fun UiNode.horizontalAlignment(): Alignment.Horizontal = when (attribute("Alignment")) {
    "Leading" -> Alignment.Start
    "Trailing" -> Alignment.End
    else -> Alignment.CenterHorizontally
}

private fun UiNode.verticalAlignment(): Alignment.Vertical = when (attribute("Alignment")) {
    "Top" -> Alignment.Top
    "Bottom" -> Alignment.Bottom
    else -> Alignment.CenterVertically
}

private fun UiNode.joinedText(): String =
    children.filter { it.type == UiNode.TEXT_TYPE }.joinToString("") { it.text.orEmpty() }

internal fun UiNode.dispatch(eventName: String, value: String?, dispatch: EventDispatch) {
    val eventHandlerId = events[eventName] ?: return
    dispatch(eventHandlerId, eventName, value)
}
