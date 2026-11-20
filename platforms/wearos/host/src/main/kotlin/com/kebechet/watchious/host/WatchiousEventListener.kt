package com.kebechet.watchious.host

fun interface WatchiousEventListener {
    fun onEvent(eventHandlerId: Long, eventName: String, value: String?)
}
