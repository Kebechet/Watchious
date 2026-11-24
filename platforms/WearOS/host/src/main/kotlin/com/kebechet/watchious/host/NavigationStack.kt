package com.kebechet.watchious.host

import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.saveable.rememberSaveableStateHolder
import androidx.wear.compose.foundation.BasicSwipeToDismissBox

private const val NO_PAGE = -1

// The pages of a Watchious NavigationStack element: the top page is shown, and swiping it away reveals the page
// below, as in Wear OS navigation. The back button goes through BackNavigation. Either way the host reports how
// many pages are left, and C# removes the rest.
@Composable
internal fun NavigationStack(node: UiNode, dispatch: EventDispatch) {
    val pages = node.children.filter { it.type == "Page" }
    val top = pages.lastOrNull() ?: return
    val previous = pages.getOrNull(pages.size - 2)

    // Keeps each page's saveable state (such as its scroll position) while another page is on top of it.
    val pageStates = rememberSaveableStateHolder()
    val shownPageIds = remember { mutableSetOf<Int>() }
    val pageIds = pages.map { it.id }.toSet()
    LaunchedEffect(pageIds) {
        (shownPageIds - pageIds).forEach(pageStates::removeState)
        shownPageIds.retainAll(pageIds)
        shownPageIds.addAll(pageIds)
    }

    val goBack by rememberUpdatedState { node.dispatch("back", (pages.size - 1).toString(), dispatch) }
    val backNavigation = LocalBackNavigation.current
    val canGoBack = previous != null
    DisposableEffect(backNavigation, canGoBack) {
        if (canGoBack) {
            backNavigation?.enable { goBack() }
        }
        onDispose { backNavigation?.disable() }
    }

    BasicSwipeToDismissBox(
        onDismissed = { goBack() },
        backgroundKey = previous?.id ?: NO_PAGE,
        contentKey = top.id,
        userSwipeEnabled = canGoBack,
    ) { isBackground ->
        val page = if (isBackground) previous else top
        if (page != null) {
            key(page.id) {
                pageStates.SaveableStateProvider(page.id) {
                    val edges = page.screenEdges()
                    CompositionLocalProvider(LocalScreenEdges provides edges) {
                        if (!edges.isEmpty && !page.hasScreenScaffold()) {
                            EdgesPage(dispatch) { RenderNodes(page.children, dispatch) }
                        } else {
                            RenderNodes(page.children, dispatch)
                        }
                    }
                }
            }
        }
    }
}
