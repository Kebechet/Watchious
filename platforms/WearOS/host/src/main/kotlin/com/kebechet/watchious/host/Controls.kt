package com.kebechet.watchious.host

import android.content.pm.ApplicationInfo
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.TextRange
import androidx.compose.ui.text.input.TextFieldValue
import androidx.compose.ui.unit.dp
import androidx.wear.compose.foundation.lazy.TransformingLazyColumn
import androidx.wear.compose.foundation.lazy.items
import androidx.wear.compose.foundation.lazy.rememberTransformingLazyColumnState
import androidx.wear.compose.foundation.rotary.RotaryScrollableDefaults
import androidx.wear.compose.material3.CircularProgressIndicator
import androidx.wear.compose.material3.Icon
import androidx.wear.compose.material3.LinearProgressIndicator
import androidx.wear.compose.material3.MaterialTheme
import androidx.wear.compose.material3.Picker
import androidx.wear.compose.material3.PickerDefaults
import androidx.wear.compose.material3.Text
import androidx.wear.compose.material3.rememberPickerState

@Composable
internal fun WatchList(node: UiNode, dispatch: EventDispatch) {
    val state = rememberTransformingLazyColumnState()
    val crown = rememberScreenCrown()
    // Apple's own controls, and the ones on the screen's edges, would otherwise become empty rows.
    val rows = node.children.filterNot { it.isBlankText() || it.isAppleOnly() || it.isScreenEdge() }
    EdgesScreenScaffold(state, crown, dispatch) { contentPadding: PaddingValues ->
        CompositionLocalProvider(LocalScreenCrown provides crown) {
            // The list's own crown handling, like ScrollView's, so that it can let a Picker have the crown.
            TransformingLazyColumn(
                state = state,
                contentPadding = contentPadding,
                modifier = Modifier
                    .fillMaxSize()
                    .screenCrown(crown, RotaryScrollableDefaults.behavior(state)),
                verticalArrangement = Arrangement.spacedBy(4.dp),
                rotaryScrollableBehavior = null,
            ) {
                items(rows, key = { it.id }) { row -> ListRow(row, dispatch) }
            }
        }
    }
}

@Composable
internal fun WatchProgressView(node: UiNode, fillWidth: Modifier) {
    val value = node.attribute("Value")?.toFloatOrNull()
    val text = node.attribute("Text")
    Column(
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(4.dp),
    ) {
        if (!text.isNullOrEmpty()) {
            Text(text, style = MaterialTheme.typography.bodySmall)
        }
        val isLinear = when (val style = node.attribute("Style")) {
            "Linear" -> true
            // Automatic is the platform's own, a ring on Wear OS.
            "Circular", "Automatic", null -> false
            else -> {
                reportUnknownValue(style, "Style", node.type)
                false
            }
        }
        when {
            value == null -> CircularProgressIndicator(modifier = Modifier.size(32.dp))
            isLinear -> LinearProgressIndicator(progress = { value }, modifier = fillWidth)
            else -> CircularProgressIndicator(progress = { value }, modifier = Modifier.size(48.dp))
        }
    }
}

@Composable
internal fun WatchPicker(node: UiNode, dispatch: EventDispatch) {
    val items = node.attribute("Items")?.takeIf { it.isNotEmpty() }?.split('\n').orEmpty()
    if (items.isEmpty()) {
        return
    }

    val selectedIndex = (node.attribute("SelectedIndex")?.toIntOrNull() ?: 0).coerceIn(0, items.size - 1)
    val state = rememberPickerState(
        initialNumberOfOptions = items.size,
        initiallySelectedIndex = selectedIndex,
        shouldRepeatOptions = false,
    )
    LaunchedEffect(items.size) { state.numberOfOptions = items.size }
    LaunchedEffect(selectedIndex) {
        if (!state.isScrollInProgress && state.selectedOptionIndex != selectedIndex) {
            state.scrollToOption(selectedIndex)
        }
    }
    // Reported once the wheel settles: while it turns, the selected option passes every option on the way, and
    // C# answering each of them would move the wheel under the user's finger. Only after a turn: the clamped first
    // value, or the one the wheel takes when Items shrink, isn't the user's choice, and SwiftUI reports neither.
    LaunchedEffect(state) {
        var hasTurned = false
        snapshotFlow { state.isScrollInProgress to state.selectedOptionIndex }
            .collect { (isScrolling, index) ->
                if (isScrolling) {
                    hasTurned = true
                } else if (hasTurned) {
                    hasTurned = false
                    if (index != node.attribute("SelectedIndex")?.toIntOrNull()) {
                        node.dispatch("selected", index.toString(), dispatch)
                    }
                }
            }
    }

    val text = node.attribute("Text")
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        if (!text.isNullOrEmpty()) {
            Text(text, style = MaterialTheme.typography.bodySmall)
        }
        // The crown turns the wheel once it is touched (pickerCrown); Picker's own crown handling waits for a focus that a
        // screen with a scrolling list never gives it.
        Picker(
            state = state,
            contentDescription = { listOfNotNull(text, items.getOrNull(state.selectedOptionIndex)).joinToString(" ") },
            modifier = Modifier
                .size(width = 120.dp, height = 100.dp)
                .pickerCrown(PickerDefaults.rotarySnapBehavior(state)),
            rotaryScrollableBehavior = null,
        ) { optionIndex ->
            Text(items[optionIndex], style = MaterialTheme.typography.titleMedium)
        }
    }
}

@Composable
internal fun WatchTextField(node: UiNode, dispatch: EventDispatch, modifier: Modifier) {
    val text = node.attribute("Text").orEmpty()
    var value by remember { mutableStateOf(TextFieldValue(text, TextRange(text.length))) }
    // The field keeps its own value while the user types, so the cursor stays put; it follows C# only when C#
    // changes the text to something else than the field's own edit.
    LaunchedEffect(text) {
        if (text != value.text) {
            value = TextFieldValue(text, TextRange(text.length))
        }
    }

    val colors = MaterialTheme.colorScheme
    val textStyle = MaterialTheme.typography.bodyMedium
    BasicTextField(
        value = value,
        onValueChange = { newValue ->
            val isChanged = newValue.text != value.text
            value = newValue
            if (isChanged) {
                node.dispatch("changed", newValue.text, dispatch)
            }
        },
        modifier = modifier
            .background(colors.surfaceContainer, RoundedCornerShape(percent = 50))
            .padding(horizontal = 16.dp, vertical = 12.dp),
        textStyle = textStyle.copy(color = colors.onSurface),
        cursorBrush = SolidColor(colors.primary),
        singleLine = true,
        decorationBox = { innerTextField ->
            Box {
                if (value.text.isEmpty()) {
                    Text(node.attribute("Placeholder").orEmpty(), style = textStyle, color = colors.onSurfaceVariant)
                }
                innerTextField()
            }
        },
    )
}

// A system icon, or an app image, which Watchious.Sdk adds to the app as an Android drawable named like the file.
@Composable
internal fun WatchImage(node: UiNode) {
    val materialName = node.attribute("MaterialIcon")
    val iconName = node.attribute("Icon")
    if (!materialName.isNullOrEmpty() || !iconName.isNullOrEmpty()) {
        WatchIcon(node, materialName, iconName)
        return
    }

    val context = LocalContext.current
    val source = node.attribute("Source").orEmpty()
    val resourceId = remember(source) {
        if (source.isEmpty()) 0 else context.resources.getIdentifier(source, "drawable", context.packageName)
    }
    if (resourceId == 0) {
        return
    }

    val width = node.attribute("Width")?.toFloatOrNull()
    val height = node.attribute("Height")?.toFloatOrNull()
    var modifier: Modifier = Modifier
    if (width != null) {
        modifier = modifier.width(width.dp)
    }
    if (height != null) {
        modifier = modifier.height(height.dp)
    }
    Image(
        painter = painterResource(resourceId),
        contentDescription = node.attribute("Description"),
        modifier = modifier,
        contentScale = ContentScale.Fit,
    )
}

// MaterialIcon names any Material icon and wins over Icon, the shared set, which stands in when the name isn't found
// (such as an icon R8 left out of a release build). An icon neither finds is logged, and shows as a warning in a
// debuggable build.
@Composable
private fun WatchIcon(node: UiNode, materialName: String?, iconName: String?) {
    val icon = remember(materialName, iconName) {
        val own = if (materialName.isNullOrEmpty()) null else materialIcon(materialName)
        own ?: iconName?.let { name ->
            watchiousIcon(name) ?: run {
                reportUnknownValue(name, "Icon", node.type)
                null
            }
        }
    }
    if (icon == null) {
        val isDebuggable = (LocalContext.current.applicationInfo.flags and ApplicationInfo.FLAG_DEBUGGABLE) != 0
        if (isDebuggable) {
            Text("⚠️ ${materialName ?: iconName}", color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall)
        }
        return
    }

    // An icon is square, so one side sets both, as SwiftUI's scaledToFit does: Compose scales a vector up only when both
    // sides are fixed.
    val width = node.attribute("Width")?.toFloatOrNull()
    val height = node.attribute("Height")?.toFloatOrNull()
    val modifier = if (width != null || height != null) {
        Modifier.size((width ?: height!!).dp, (height ?: width!!).dp)
    } else {
        Modifier
    }
    Icon(imageVector = icon, contentDescription = node.attribute("Description"), modifier = modifier)
}

internal fun UiNode.isBlankText(): Boolean = type == UiNode.TEXT_TYPE && text.isNullOrBlank()
