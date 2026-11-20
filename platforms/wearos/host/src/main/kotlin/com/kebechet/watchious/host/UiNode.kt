package com.kebechet.watchious.host

import org.json.JSONArray
import org.json.JSONObject

internal data class UiNode(
    val type: String,
    val attributes: Map<String, String>,
    val events: Map<String, Long>,
    val children: List<UiNode>,
    val text: String?,
) {
    fun attribute(name: String): String? = attributes[name]

    companion object {
        const val TEXT_TYPE = "#text"

        fun parseTree(json: String): List<UiNode> = parseChildren(JSONObject(json).getJSONArray("children"))

        private fun parseChildren(array: JSONArray): List<UiNode> = List(array.length()) { index ->
            parse(array.getJSONObject(index))
        }

        private fun parse(node: JSONObject): UiNode {
            val type = node.getString("type")
            if (type == TEXT_TYPE) {
                return UiNode(type, emptyMap(), emptyMap(), emptyList(), node.optString("text"))
            }

            val attributes = node.getJSONObject("attributes")
            val events = node.getJSONObject("events")
            return UiNode(
                type = type,
                attributes = attributes.keys().asSequence()
                    .filterNot { attributes.isNull(it) }
                    .associateWith { attributes.getString(it) },
                events = events.keys().asSequence().associateWith { events.getLong(it) },
                children = parseChildren(node.getJSONArray("children")),
                text = null,
            )
        }
    }
}
