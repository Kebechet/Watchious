package com.kebechet.watchious.live

import android.util.Log
import com.kebechet.watchious.host.WatchiousComposeHost
import com.kebechet.watchious.host.WatchiousEventListener
import org.json.JSONObject
import java.io.IOException
import java.net.HttpURLConnection
import java.net.URL
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.util.concurrent.Executors
import kotlin.concurrent.thread

private const val LOG_TAG = "WatchiousLive"
private const val PATCH_BATCH: Int = 1
private const val JSON: Int = 2
private const val HEADER_BYTES = 1 + 8 + 4
private const val RETRY_MILLISECONDS = 2000L

// Watchious.Preview's live protocol (WatchiousLive.cs) over plain HTTP: a session per run of the app, its messages by
// long polling, and the host's events and confirmations posted back in their order.
internal class LiveConnection(
    private val baseUrl: String,
    private val host: WatchiousComposeHost,
    private val onAppRestarted: () -> Unit,
) : WatchiousEventListener {
    private val sender = Executors.newSingleThreadExecutor()
    @Volatile private var session: String? = null
    @Volatile private var isStopped = false
    private var hasShownPatches = false
    // The last message of the session this host got, which the next request confirms.
    private var lastSequence = 0L
    private var poller: Thread? = null

    fun start() {
        poller = thread(name = "watchious-live") { poll() }
    }

    fun stop() {
        isStopped = true
        poller?.interrupt()
        val ending = session
        sender.execute {
            if (ending != null) {
                runCatching { request("POST", "/live/stop?session=$ending", null) }
            }
        }
        sender.shutdown()
    }

    override fun onEvent(eventHandlerId: Long, eventName: String, value: String?) {
        val message = JSONObject()
            .put("id", eventHandlerId)
            .put("name", eventName)
            .put("value", value ?: JSONObject.NULL)
        send(message.toString())
    }

    override fun onPatchesApplied() {
        send("{\"applied\":true}")
    }

    private fun poll() {
        while (!isStopped) {
            try {
                val id = session ?: startSession()
                val answer = request("GET", "/live/messages?session=$id&ack=$lastSequence", null)
                if (answer == null) {
                    // The computer no longer knows the session: its app was restarted, or this host was away too long.
                    session = null
                    if (hasShownPatches) {
                        onAppRestarted()
                        return
                    }
                    continue
                }
                readMessages(answer)
            } catch (exception: IOException) {
                Log.w(LOG_TAG, "The computer at $baseUrl can't be reached (dotnet run -f net10.0 -- --live): ${exception.message}")
                sleepBeforeRetry()
            }
        }
    }

    private fun startSession(): String {
        val id = request("POST", "/live/start?platform=wearos", null)?.toString(Charsets.UTF_8)
            ?: throw IOException("The computer didn't start a session.")
        lastSequence = 0
        session = id
        return id
    }

    // Each message is a kind byte, its little-endian sequence number and length, and its bytes. The computer sends again
    // what this host hasn't confirmed, as an answer can be lost on the way, so a message seen before is skipped.
    private fun readMessages(answer: ByteArray) {
        val buffer = ByteBuffer.wrap(answer).order(ByteOrder.LITTLE_ENDIAN)
        while (buffer.remaining() >= HEADER_BYTES) {
            val kind = buffer.get().toInt()
            val sequence = buffer.long
            val length = buffer.int
            if (length < 0 || length > buffer.remaining()) {
                Log.e(LOG_TAG, "A message from the computer is cut short; the computer sends it again.")
                return
            }
            val bytes = ByteArray(length)
            buffer.get(bytes)
            if (sequence <= lastSequence) {
                continue
            }
            lastSequence = sequence
            when (kind) {
                PATCH_BATCH -> {
                    hasShownPatches = true
                    host.render(bytes)
                }
                JSON -> Log.e(LOG_TAG, errorText(bytes))
            }
        }
    }

    // The app's error, which comes as {"error": "..."}.
    private fun errorText(bytes: ByteArray): String {
        val text = bytes.toString(Charsets.UTF_8)
        return runCatching { JSONObject(text).optString("error", text) }.getOrDefault(text)
    }

    private fun send(json: String) {
        val id = session ?: return
        if (isStopped) {
            return
        }
        sender.execute {
            try {
                request("POST", "/live/events?session=$id", json)
            } catch (exception: IOException) {
                Log.w(LOG_TAG, "An event didn't reach the computer: ${exception.message}")
            }
        }
    }

    // The response's bytes, or null when the session is unknown (404).
    private fun request(method: String, path: String, json: String?): ByteArray? {
        val connection = URL(baseUrl.trimEnd('/') + path).openConnection() as HttpURLConnection
        try {
            connection.requestMethod = method
            connection.connectTimeout = 5000
            // Longer than the 20 s the computer holds a request for messages open.
            connection.readTimeout = 40000
            if (json != null) {
                connection.doOutput = true
                connection.setRequestProperty("Content-Type", "application/json")
                connection.outputStream.use { it.write(json.toByteArray(Charsets.UTF_8)) }
            } else if (method == "POST") {
                connection.setFixedLengthStreamingMode(0)
            }
            val status = connection.responseCode
            if (status == HttpURLConnection.HTTP_NOT_FOUND) {
                return null
            }
            if (status !in 200..299) {
                throw IOException("HTTP $status for $path")
            }
            return connection.inputStream.use { it.readBytes() }
        } finally {
            connection.disconnect()
        }
    }

    private fun sleepBeforeRetry() {
        try {
            Thread.sleep(RETRY_MILLISECONDS)
        } catch (_: InterruptedException) {
            isStopped = true
        }
    }
}
