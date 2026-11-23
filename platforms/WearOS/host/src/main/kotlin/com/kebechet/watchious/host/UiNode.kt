package com.kebechet.watchious.host

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue

// Every field is Compose state, so a patch recomposes only the controls that read what it changed.
internal class UiNode(val id: Int, val type: String) {
    val attributes = mutableStateMapOf<String, String>()
    val events = mutableStateMapOf<String, Long>()
    val children = mutableStateListOf<UiNode>()
    var text by mutableStateOf<String?>(null)

    fun attribute(name: String): String? = attributes[name]

    companion object {
        const val TEXT_TYPE = "#text"
    }
}
