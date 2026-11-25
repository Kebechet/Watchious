# Image's MaterialIcon finds its icon by name at run time (Icons.kt in the host), and the live host draws any app, so
# which icons it needs isn't known when it is built: it keeps every icon of the Rounded style, the only one MaterialIcon
# looks in. A .NET app keeps only the ones it lists as WatchiousMaterialIcon items instead (Watchious.Sdk.targets).
-keep class androidx.compose.material.icons.rounded.** { *; }
-keep class androidx.compose.material.icons.automirrored.rounded.** { *; }
