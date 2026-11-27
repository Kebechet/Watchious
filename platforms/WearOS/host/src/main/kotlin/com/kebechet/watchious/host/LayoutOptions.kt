package com.kebechet.watchious.host

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.RowScope
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
    // A VStack, ScrollView, List row or DigitalCrownRotation: places a child by its HorizontalOptions, within its own
    // width.
    COLUMN,

    // An HStack: places a child by its VerticalOptions, within the row's height.
    ROW,
}

internal enum class LayoutOption { START, CENTER, END, FILL }

internal fun UiNode.horizontalOptions(): LayoutOption? = layoutOption("HorizontalOptions")

internal fun UiNode.verticalOptions(): LayoutOption? = layoutOption("VerticalOptions")

// Whether HorizontalOptions keep a control that stretches across the width (RenderNode's fillWidth) at its own width.
internal fun UiNode.keepsOwnWidth(): Boolean = attribute("HorizontalOptions") in KEEP_WIDTH_OPTIONS

private val KEEP_WIDTH_OPTIONS = setOf("Start", "Center", "End")

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

// Blank text between the markup's tags, the other platform's controls and the ones on the screen's edges draw nothing
// here, and must not take a place (or a stack's spacing) either.
private fun UiNode.takesNoPlace(): Boolean = isBlankText() || isAppleOnly() || isScreenEdge()

// A column's children, each within the column's own width as its HorizontalOptions say: Start, Center and End by
// alignment, Fill by stretching (RenderNode's fillWidth), so that no child widens the column beyond its widest child.
// Every child gets the same box, so a change of options keeps the control's state.
@Composable
internal fun ColumnScope.ColumnChildren(nodes: List<UiNode>, dispatch: EventDispatch) {
    nodes.filterNot { it.takesNoPlace() }.forEach { node ->
        key(node.id) {
            Box(Modifier.align(node.horizontalOptions().horizontal())) {
                RenderNode(node, dispatch, LayoutParent.COLUMN)
            }
        }
    }
}

// A row's children, each within the row's height as its VerticalOptions say (Fill centers it too: no control stretches
// vertically). A stack in a row gets the width its siblings leave, measured after them as SwiftUI shares a row, instead
// of taking it all and leaving them none.
@Composable
internal fun RowScope.RowChildren(nodes: List<UiNode>, dispatch: EventDispatch) {
    nodes.filterNot { it.takesNoPlace() }.forEach { node ->
        key(node.id) {
            val sharesWidth = node.type == "VStack" || node.type == "DigitalCrownRotation"
            Box(
                Modifier
                    .align(node.verticalOptions().vertical())
                    .then(if (sharesWidth) Modifier.weight(1f, fill = false) else Modifier),
            ) {
                RenderNode(node, dispatch, LayoutParent.ROW)
            }
        }
    }
}

// A List row: the whole width of the list, its control placed in it.
@Composable
internal fun ListRow(row: UiNode, dispatch: EventDispatch) {
    Box(Modifier.fillMaxWidth(), contentAlignment = alignment(row.horizontalOptions(), null)) {
        RenderNode(row, dispatch, LayoutParent.COLUMN)
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
        page.children.filterNot { it.takesNoPlace() }.forEach { child ->
            key(child.id) {
                val vertical = child.verticalOptions()
                Box(
                    modifier = (if (vertical != null) Modifier.weight(1f) else Modifier).fillMaxWidth(),
                    contentAlignment = alignment(child.horizontalOptions(), vertical),
                ) {
                    RenderNode(child, dispatch, LayoutParent.COLUMN)
                }
            }
        }
    }
}

private fun LayoutOption?.horizontal(): Alignment.Horizontal = when (this) {
    LayoutOption.START -> Alignment.Start
    LayoutOption.END -> Alignment.End
    LayoutOption.CENTER, LayoutOption.FILL, null -> Alignment.CenterHorizontally
}

private fun LayoutOption?.vertical(): Alignment.Vertical = when (this) {
    LayoutOption.START -> Alignment.Top
    LayoutOption.END -> Alignment.Bottom
    LayoutOption.CENTER, LayoutOption.FILL, null -> Alignment.CenterVertically
}

private fun alignment(horizontal: LayoutOption?, vertical: LayoutOption?): Alignment =
    BiasAlignment(horizontal.bias(), vertical.bias())

private fun LayoutOption?.bias(): Float = when (this) {
    LayoutOption.START -> -1f
    LayoutOption.END -> 1f
    LayoutOption.CENTER, LayoutOption.FILL, null -> 0f
}
