package com.kebechet.watchious.host

import android.util.Log
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.rounded.Chat
import androidx.compose.material.icons.automirrored.rounded.DirectionsBike
import androidx.compose.material.icons.automirrored.rounded.DirectionsRun
import androidx.compose.material.icons.automirrored.rounded.DirectionsWalk
import androidx.compose.material.icons.automirrored.rounded.List
import androidx.compose.material.icons.automirrored.rounded.VolumeUp
import androidx.compose.material.icons.rounded.Add
import androidx.compose.material.icons.rounded.Alarm
import androidx.compose.material.icons.rounded.BatteryFull
import androidx.compose.material.icons.rounded.Bedtime
import androidx.compose.material.icons.rounded.Bolt
import androidx.compose.material.icons.rounded.CalendarToday
import androidx.compose.material.icons.rounded.Check
import androidx.compose.material.icons.rounded.ChevronLeft
import androidx.compose.material.icons.rounded.ChevronRight
import androidx.compose.material.icons.rounded.Close
import androidx.compose.material.icons.rounded.Delete
import androidx.compose.material.icons.rounded.Edit
import androidx.compose.material.icons.rounded.Favorite
import androidx.compose.material.icons.rounded.FitnessCenter
import androidx.compose.material.icons.rounded.Home
import androidx.compose.material.icons.rounded.Info
import androidx.compose.material.icons.rounded.KeyboardArrowDown
import androidx.compose.material.icons.rounded.KeyboardArrowUp
import androidx.compose.material.icons.rounded.LocalFireDepartment
import androidx.compose.material.icons.rounded.LocationOn
import androidx.compose.material.icons.rounded.Lock
import androidx.compose.material.icons.rounded.Mic
import androidx.compose.material.icons.rounded.MusicNote
import androidx.compose.material.icons.rounded.Notifications
import androidx.compose.material.icons.rounded.Pause
import androidx.compose.material.icons.rounded.Person
import androidx.compose.material.icons.rounded.Phone
import androidx.compose.material.icons.rounded.PlayArrow
import androidx.compose.material.icons.rounded.Pool
import androidx.compose.material.icons.rounded.Refresh
import androidx.compose.material.icons.rounded.Remove
import androidx.compose.material.icons.rounded.Schedule
import androidx.compose.material.icons.rounded.Search
import androidx.compose.material.icons.rounded.Settings
import androidx.compose.material.icons.rounded.Share
import androidx.compose.material.icons.rounded.Star
import androidx.compose.material.icons.rounded.Stop
import androidx.compose.material.icons.rounded.Timer
import androidx.compose.material.icons.rounded.Warning
import androidx.compose.material.icons.rounded.WaterDrop
import androidx.compose.material.icons.rounded.WbSunny
import androidx.compose.material.icons.rounded.Wifi
import androidx.compose.ui.graphics.vector.ImageVector

// Watchious's shared icons (Watchious.Controls.Icon) as Material icons of the Rounded style, the style of Wear OS.
// Referenced one by one, so that R8 keeps these and drops the rest of material-icons-extended from a release app.
internal fun watchiousIcon(name: String): ImageVector? = when (name) {
    "Heart" -> Icons.Rounded.Favorite
    "Star" -> Icons.Rounded.Star
    "Bell" -> Icons.Rounded.Notifications
    "Home" -> Icons.Rounded.Home
    "Person" -> Icons.Rounded.Person
    "Settings" -> Icons.Rounded.Settings
    "Info" -> Icons.Rounded.Info
    "Warning" -> Icons.Rounded.Warning
    "Search" -> Icons.Rounded.Search
    "Lock" -> Icons.Rounded.Lock
    "Plus" -> Icons.Rounded.Add
    "Minus" -> Icons.Rounded.Remove
    "Checkmark" -> Icons.Rounded.Check
    "Close" -> Icons.Rounded.Close
    "Edit" -> Icons.Rounded.Edit
    "Delete" -> Icons.Rounded.Delete
    "Refresh" -> Icons.Rounded.Refresh
    "Share" -> Icons.Rounded.Share
    "ChevronLeft" -> Icons.Rounded.ChevronLeft
    "ChevronRight" -> Icons.Rounded.ChevronRight
    "ChevronUp" -> Icons.Rounded.KeyboardArrowUp
    "ChevronDown" -> Icons.Rounded.KeyboardArrowDown
    "Play" -> Icons.Rounded.PlayArrow
    "Pause" -> Icons.Rounded.Pause
    "Stop" -> Icons.Rounded.Stop
    "Microphone" -> Icons.Rounded.Mic
    "Speaker" -> Icons.AutoMirrored.Rounded.VolumeUp
    "Music" -> Icons.Rounded.MusicNote
    "Phone" -> Icons.Rounded.Phone
    "Message" -> Icons.AutoMirrored.Rounded.Chat
    "Location" -> Icons.Rounded.LocationOn
    "Calendar" -> Icons.Rounded.CalendarToday
    "Clock" -> Icons.Rounded.Schedule
    "Timer" -> Icons.Rounded.Timer
    "Alarm" -> Icons.Rounded.Alarm
    "Run" -> Icons.AutoMirrored.Rounded.DirectionsRun
    "Walk" -> Icons.AutoMirrored.Rounded.DirectionsWalk
    "Bike" -> Icons.AutoMirrored.Rounded.DirectionsBike
    "Swim" -> Icons.Rounded.Pool
    "Dumbbell" -> Icons.Rounded.FitnessCenter
    "Flame" -> Icons.Rounded.LocalFireDepartment
    "Bolt" -> Icons.Rounded.Bolt
    "Drop" -> Icons.Rounded.WaterDrop
    "Moon" -> Icons.Rounded.Bedtime
    "Sun" -> Icons.Rounded.WbSunny
    "Battery" -> Icons.Rounded.BatteryFull
    "Wifi" -> Icons.Rounded.Wifi
    "List" -> Icons.AutoMirrored.Rounded.List
    else -> null
}

// Any other Material icon of the Rounded style, by its Compose name: each is a getter on Icons.Rounded (or on
// Icons.AutoMirrored.Rounded for the ones that turn around in right-to-left languages), compiled into a class of its
// own. ⚠️ R8 drops the ones no code references, so a release app keeps them with the rule in consumer-rules.pro.
internal fun materialIcon(name: String): ImageVector? {
    val candidates = listOf(
        Triple("androidx.compose.material.icons.automirrored.rounded", Icons.AutoMirrored.Rounded, Icons.AutoMirrored.Rounded::class.java),
        Triple("androidx.compose.material.icons.rounded", Icons.Rounded, Icons.Rounded::class.java),
    )
    for ((packageName, receiver, receiverClass) in candidates) {
        val icon = runCatching {
            Class.forName("$packageName.${name}Kt").getMethod("get$name", receiverClass).invoke(null, receiver) as ImageVector
        }.getOrNull()
        if (icon != null) {
            return icon
        }
    }

    Log.w(LOG_TAG, "Unknown Material icon $name: no Icons.Rounded.$name or Icons.AutoMirrored.Rounded.$name")
    return null
}
