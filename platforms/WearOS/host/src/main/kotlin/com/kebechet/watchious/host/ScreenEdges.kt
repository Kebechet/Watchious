package com.kebechet.watchious.host

import androidx.compose.foundation.ScrollState
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxScope
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.rememberScrollState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.compositionLocalOf
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextOverflow
import androidx.wear.compose.foundation.CurvedDirection
import androidx.wear.compose.foundation.CurvedLayout
import androidx.wear.compose.foundation.ScrollInfoProvider
import androidx.wear.compose.foundation.lazy.TransformingLazyColumnState
import androidx.wear.compose.material3.CurvedTextDefaults
import androidx.wear.compose.material3.EdgeButton
import androidx.wear.compose.material3.Icon
import androidx.wear.compose.material3.ScreenScaffold
import androidx.wear.compose.material3.ScrollIndicator
import androidx.wear.compose.material3.Text
import androidx.wear.compose.material3.TimeText
import androidx.wear.compose.material3.curvedText
import androidx.wear.compose.material3.timeTextCurvedText
import androidx.wear.compose.material3.timeTextSeparator

// The Wear-only controls that sit on the round screen's edges (Wear.TimeText, Wear.CurvedText and Wear.EdgeButton in
// C#). Wherever a page puts them, its ScreenScaffold shows them on the edges, and they take no place in the layout.
internal class ScreenEdges(val timeText: UiNode?, val curvedText: UiNode?, val edgeButton: UiNode?) {
    val isEmpty: Boolean get() = timeText == null && curvedText == null && edgeButton == null
}

internal val LocalScreenEdges = compositionLocalOf { ScreenEdges(null, null, null) }

// Apple's own controls (the Apple namespace in C#) have no place on Wear OS.
internal fun UiNode.isAppleOnly(): Boolean = type.startsWith("Apple.")

internal fun UiNode.isScreenEdge(): Boolean = type == "Wear.TimeText" || type == "Wear.CurvedText" || type == "Wear.EdgeButton"

internal fun UiNode.screenEdges(): ScreenEdges {
    var timeText: UiNode? = null
    var curvedText: UiNode? = null
    var edgeButton: UiNode? = null
    fun visit(node: UiNode) {
        when (node.type) {
            "Wear.TimeText" -> timeText = node
            "Wear.CurvedText" -> curvedText = node
            "Wear.EdgeButton" -> edgeButton = node
        }
        node.children.forEach(::visit)
    }
    children.forEach(::visit)
    return ScreenEdges(timeText, curvedText, edgeButton)
}

// Whether a page draws its own ScreenScaffold, which shows the edges; another page gets one from the navigation stack.
internal fun UiNode.hasScreenScaffold(): Boolean = type == "List" || type == "ScrollView" || children.any { it.hasScreenScaffold() }

@Composable
internal fun EdgesScreenScaffold(
    state: TransformingLazyColumnState,
    dispatch: EventDispatch,
    content: @Composable BoxScope.(PaddingValues) -> Unit,
) {
    val edges = LocalScreenEdges.current
    val edgeButton = edges.edgeButton
    if (edgeButton == null) {
        ScreenScaffold(scrollState = state, timeText = edges.timeTextSlot()) { padding -> Edges(edges, padding, content) }
    } else {
        ScreenScaffold(
            scrollState = state,
            edgeButton = { ScreenEdgeButton(edgeButton, dispatch) },
            timeText = edges.timeTextSlot(),
        ) { padding -> Edges(edges, padding, content) }
    }
}

@Composable
internal fun EdgesScreenScaffold(
    state: ScrollState,
    dispatch: EventDispatch,
    content: @Composable BoxScope.(PaddingValues) -> Unit,
) {
    val edges = LocalScreenEdges.current
    val edgeButton = edges.edgeButton
    if (edgeButton == null) {
        ScreenScaffold(scrollState = state, timeText = edges.timeTextSlot()) { padding -> Edges(edges, padding, content) }
    } else {
        ScreenScaffold(
            scrollInfoProvider = ScrollInfoProvider(state),
            edgeButton = { ScreenEdgeButton(edgeButton, dispatch) },
            timeText = edges.timeTextSlot(),
            scrollIndicator = { ScrollIndicator(state) },
        ) { padding -> Edges(edges, padding, content) }
    }
}

// A page without a List or ScrollView: the edges around its content, which doesn't scroll.
@Composable
internal fun EdgesPage(dispatch: EventDispatch, content: @Composable () -> Unit) {
    EdgesScreenScaffold(rememberScrollState(), dispatch) { padding ->
        Box(modifier = Modifier.fillMaxSize()) {
            content()
        }
    }
}

@Composable
private fun Edges(edges: ScreenEdges, padding: PaddingValues, content: @Composable BoxScope.(PaddingValues) -> Unit) {
    Box(modifier = Modifier.fillMaxSize()) {
        // A List inside a ScrollView has a ScreenScaffold of its own, which mustn't show the edges a second time.
        CompositionLocalProvider(LocalScreenEdges provides ScreenEdges(null, null, null)) {
            content(padding)
        }
        edges.curvedText?.let { ScreenCurvedText(it) }
    }
}

private fun ScreenEdges.timeTextSlot(): (@Composable () -> Unit)? {
    val node = timeText ?: return null
    return { ScreenTimeText(node) }
}

@Composable
private fun ScreenTimeText(node: UiNode) {
    val text = node.attribute("Text")
    TimeText { time ->
        if (!text.isNullOrEmpty()) {
            curvedText(text)
            timeTextSeparator()
        }
        timeTextCurvedText(time)
    }
}

// Along the bottom edge, read from left to right.
@Composable
private fun ScreenCurvedText(node: UiNode) {
    CurvedLayout(
        modifier = Modifier.fillMaxSize(),
        anchor = 90f,
        angularDirection = CurvedDirection.Angular.Reversed,
    ) {
        // The edge doesn't scroll, so the text may take the wider sweep of static content; beyond it, an ellipsis.
        curvedText(
            text = node.attribute("Text").orEmpty(),
            maxSweepAngle = CurvedTextDefaults.StaticContentMaxSweepAngle,
            overflow = TextOverflow.Ellipsis,
        )
    }
}

@Composable
private fun ScreenEdgeButton(node: UiNode, dispatch: EventDispatch) {
    EdgeButton(
        onClick = { node.dispatch("click", null, dispatch) },
        enabled = node.attribute("IsEnabled") != "false",
    ) {
        val icon = node.attribute("Icon")?.let(::watchiousIcon)
        if (icon != null) {
            Icon(imageVector = icon, contentDescription = node.attribute("Text"))
        } else {
            Text(node.attribute("Text").orEmpty())
        }
    }
}
