package com.kebechet.watchious.live

import android.app.Activity
import android.os.Bundle
import com.kebechet.watchious.host.WatchiousComposeHost

// Live mode's host on Wear OS: the Compose host without .NET. The app's C# runs on a computer
// (dotnet run -f net10.0 -- --live), which sends its patches here and gets the taps back (LiveConnection). Start it
// with adb reverse tcp:5293 tcp:5293, then
// adb shell am start -n com.kebechet.watchious.live/.LiveActivity --es watchious_live http://localhost:5293
class LiveActivity : Activity() {
    private lateinit var host: WatchiousComposeHost
    private lateinit var connection: LiveConnection

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        host = WatchiousComposeHost(this)
        setContentView(host.view)
        val url = intent?.getStringExtra("watchious_live") ?: "http://localhost:5293"
        // A restarted app on the computer starts from an empty screen, which a new activity has.
        connection = LiveConnection(url, host) { runOnUiThread { recreate() } }
        host.setEventListener(connection)
        connection.start()
    }

    // Android 12L and older deliver the back button here; newer versions deliver it to the host itself.
    @Deprecated("Android 12L and older only.")
    override fun onBackPressed() {
        if (!host.handleBack()) {
            @Suppress("DEPRECATION")
            super.onBackPressed()
        }
    }

    override fun onDestroy() {
        host.setEventListener(null)
        connection.stop()
        super.onDestroy()
    }
}
