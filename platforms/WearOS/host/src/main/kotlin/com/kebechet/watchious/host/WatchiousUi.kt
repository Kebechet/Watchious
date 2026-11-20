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
internal fun WatchiousScreen(nodes: List<UiNode>, dispatch: EventDispatch) {
    MaterialTheme {
        AppScaffold {
            RenderNodes(nodes, dispatch)
        }
    }
}

@Composable
private fun RenderNodes(nodes: List<UiNode>, dispatch: EventDispatch) {
    nodes.forEach { node -> RenderNode(node, dispatch) }
}

@Composable
private fun RenderNode(node: UiNode, dispatch: EventDispatch) {
    when (node.type) {
        "ScrollView" -> WatchScrollView(node, dispatch)
        "VerticalStackLayout" -> Column(
            verticalArrangement = Arrangement.spacedBy(node.spacing().dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            RenderNodes(node.children, dispatch)
        }
        "HorizontalStackLayout" -> Row(
            horizontalArrangement = Arrangement.spacedBy(node.spacing().dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            RenderNodes(node.children, dispatch)
        }
        "Label" -> Text(
            text = node.attribute("Text").orEmpty(),
            style = labelStyle(node.attribute("Style")),
            textAlign = TextAlign.Center,
        )
        "Button" -> Button(
            onClick = { node.dispatch("click", null, dispatch) },
            enabled = node.attribute("IsEnabled") != "false",
            label = { Text(node.attribute("Text").orEmpty()) },
        )
        "Switch" -> {
            val text = node.attribute("Text")
            SwitchButton(
                checked = node.attribute("IsToggled") == "true",
                onCheckedChange = { isChecked -> node.dispatch("toggled", isChecked.toString(), dispatch) },
                modifier = if (text.isNullOrEmpty()) Modifier.wrapContentWidth() else Modifier.fillMaxWidth(),
                label = { if (!text.isNullOrEmpty()) Text(text) },
            )
        }
        UiNode.TEXT_TYPE -> Text(node.text.orEmpty())
        else -> RenderNodes(node.children, dispatch)
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
    "Caption" -> MaterialTheme.typography.bodySmall
    else -> MaterialTheme.typography.bodyMedium
}

private fun UiNode.spacing(): Float = attribute("Spacing")?.toFloatOrNull() ?: 0f

private fun UiNode.dispatch(eventName: String, value: String?, dispatch: EventDispatch) {
    val eventHandlerId = events[eventName] ?: return
    dispatch(eventHandlerId, eventName, value)
}
