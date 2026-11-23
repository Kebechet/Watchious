package com.kebechet.watchious.host

import java.nio.ByteBuffer
import java.nio.ByteOrder

// Applies patch buffers of Watchious's protocol v2. The format is described in Watchious.Core's
// Protocol/PatchOperation.cs.
internal class UiTree {
    val root = UiNode(ROOT_ID, "#root")
    private val nodes = HashMap<Int, UiNode>().apply { put(ROOT_ID, root) }

    fun apply(patches: ByteArray) {
        val buffer = ByteBuffer.wrap(patches).order(ByteOrder.LITTLE_ENDIAN)
        val version = buffer.getInt()
        check(version == PROTOCOL_VERSION) {
            "Watchious patch protocol version $version isn't supported, this host supports version $PROTOCOL_VERSION."
        }

        while (buffer.hasRemaining()) {
            val operation = buffer.get().toInt()
            val id = buffer.getInt()
            when (operation) {
                CREATE_ELEMENT -> add(UiNode(id, buffer.readString()!!))
                CREATE_TEXT -> add(UiNode(id, UiNode.TEXT_TYPE).apply { text = buffer.readString() })
                SET_ATTRIBUTE -> {
                    val name = buffer.readString()!!
                    val value = buffer.readString()
                    val attributes = find(id).attributes
                    if (value == null) attributes.remove(name) else attributes[name] = value
                }
                REMOVE_ATTRIBUTE -> find(id).attributes.remove(buffer.readString()!!)
                SET_EVENT -> {
                    val name = buffer.readString()!!
                    find(id).events[name] = buffer.getLong()
                }
                REMOVE_EVENT -> find(id).events.remove(buffer.readString()!!)
                SET_TEXT -> find(id).text = buffer.readString()
                INSERT_CHILD -> {
                    val index = buffer.getInt()
                    val child = find(buffer.getInt())
                    find(id).children.add(index, child)
                }
                REMOVE_CHILD -> forget(find(id).children.removeAt(buffer.getInt()))
                MOVE_CHILD -> {
                    val fromIndex = buffer.getInt()
                    val toIndex = buffer.getInt()
                    val children = find(id).children
                    children.add(toIndex, children.removeAt(fromIndex))
                }
                else -> error("Unknown Watchious patch operation $operation.")
            }
        }
    }

    private fun add(node: UiNode) {
        check(nodes.put(node.id, node) == null) { "Watchious node ${node.id} was created twice." }
    }

    private fun find(id: Int): UiNode = nodes[id] ?: error("Watchious patches refer to unknown node $id.")

    private fun forget(node: UiNode) {
        nodes.remove(node.id)
        node.children.forEach(::forget)
    }

    private fun ByteBuffer.readString(): String? {
        val length = getInt()
        if (length < 0) {
            return null
        }
        val value = String(array(), arrayOffset() + position(), length, Charsets.UTF_8)
        position(position() + length)
        return value
    }

    private companion object {
        const val PROTOCOL_VERSION = 2
        const val ROOT_ID = 0

        const val CREATE_ELEMENT = 1
        const val CREATE_TEXT = 2
        const val SET_ATTRIBUTE = 3
        const val REMOVE_ATTRIBUTE = 4
        const val SET_EVENT = 5
        const val REMOVE_EVENT = 6
        const val SET_TEXT = 7
        const val INSERT_CHILD = 8
        const val REMOVE_CHILD = 9
        const val MOVE_CHILD = 10
    }
}
