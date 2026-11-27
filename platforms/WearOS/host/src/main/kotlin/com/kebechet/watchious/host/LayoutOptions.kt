package com.kebechet.watchious.host

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.Composable
import androidx.compose.runtime.key
import androidx.compose.ui.Alignment
import androidx.compose.ui.BiasAlignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

// What a control's HorizontalOptions and VerticalOptions (MAUI's LayoutOptions, see LayoutOptions.cs) place it in. A
// stack ignores its children's options along its own direction, as MAUI does.
internal enum class LayoutParent {
    // A VStack, ScrollView, List row or DigitalCrownRotation: places a child by its HorizontalOptions.
    COLUMN,

    // An HStack: places a child by its VerticalOptions, within the row's height.
    ROW,
}

internal enum class LayoutOption { START, CENTER, END, FILL }

internal fun UiNode.horizontalOptions(): LayoutOption? = layoutOption("HorizontalOptions")

internal fun UiNode.verticalOptions(): LayoutOption? = layoutOption("VerticalOptions")

internal fun UiNode.hasVerticalOptions(): Boolean = attribute("VerticalOptions") != null

private fun UiNode.layoutOption(name: String): LayoutOption? = when (val options = attribute(name)) {
    null -> null
    "Start" -> LayoutOption.START
    "Center" -> LayoutOption.CENTER
    "End" -> LayoutOption.END
    "Fill" -> LayoutOption.FILL
    else -> {
        reportUnknownValue(options, name, type)
        null
    }
}

// Start, Center and End keep the control's own size and put it in the space it is given; Fill takes the whole space,
// with the control stretched (RenderNode's fillWidth) or centered in it. Without options the control stays where its
// parent puts it.
@Composable
internal fun PlacedNode(node: UiNode, dispatch: EventDispatch, parent: LayoutParent) {
    val horizontal = if (parent == LayoutParent.COLUMN) node.horizontalOptions() else null
    val vertical = if (parent == LayoutParent.ROW) node.verticalOptions() else null
    when {
        horizontal != null -> Box(Modifier.fillMaxWidth(), contentAlignment = alignment(horizontal, null)) {
            RenderNode(node, dispatch, parent)
        }
        // The row has the height of its tallest child (RenderNode's HStack), which this box takes.
        vertical != null -> Box(Modifier.fillMaxHeight(), contentAlignment = alignment(null, vertical)) {
            RenderNode(node, dispatch, parent)
        }
        else -> RenderNode(node, dispatch, parent)
    }
}

// A page without a List or ScrollView: its content in the middle of the screen, as on watchOS, unless its controls'
// options put it elsewhere. A control with VerticalOptions takes an equal share of the height the others leave, as a
// SwiftUI frame with an unlimited height does. The padding keeps the content off the round screen's edges and the time.
@Composable
internal fun PageContent(page: UiNode, dispatch: EventDispatch) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 12.dp, vertical = 24.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        page.children.forEach { child ->
            key(child.id) {
                val vertical = child.verticalOptions()
                if (vertical == null) {
                    PlacedNode(child, dispatch, LayoutParent.COLUMN)
                } else {
                    Box(
                        modifier = Modifier.weight(1f).fillMaxWidth(),
                        contentAlignment = alignment(child.horizontalOptions(), vertical),
                    ) {
                        RenderNode(child, dispatch, LayoutParent.COLUMN)
                    }
                }
            }
        }
    }
}

private fun alignment(horizontal: LayoutOption?, vertical: LayoutOption?): Alignment =
    BiasAlignment(horizontal.bias(), vertical.bias())

private fun LayoutOption?.bias(): Float = when (this) {
    LayoutOption.START -> -1f
    LayoutOption.END -> 1f
    LayoutOption.CENTER, LayoutOption.FILL, null -> 0f
}
