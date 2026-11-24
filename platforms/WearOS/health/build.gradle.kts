plugins {
    id("com.android.library")
}

android {
    namespace = "com.kebechet.watchious.health"
    compileSdk = 36

    defaultConfig {
        minSdk = 30
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
}

dependencies {
    implementation("androidx.health:health-services-client:1.1.0")
    // The ongoing activity of a running workout, from Microsoft's Xamarin.AndroidX.Wear.Ongoing in .NET.
    implementation("androidx.wear:wear-ongoing:1.1.0")
    // Health Services returns ListenableFuture, but ships Guava for runtime only.
    compileOnly("com.google.guava:guava:32.0.1-android")
}

apply(from = rootProject.file("gradle/watchious-dotnet.gradle.kts"))

// The .NET binding generator binds this jar, not the AAR: see Watchious.Health.csproj.
tasks.register<Jar>("watchiousBindingApi") {
    from(tasks.named<JavaCompile>("compileReleaseJavaWithJavac").flatMap { it.destinationDirectory }) {
        // Not the anonymous class that implements Health Services' callback, which .NET can't see.
        include("com/kebechet/watchious/health/WatchiousHealthServices.class")
        include("com/kebechet/watchious/health/WatchiousHealthServices\$Listener.class")
        include("com/kebechet/watchious/health/WatchiousHealthServices\$Callback.class")
        include("com/kebechet/watchious/health/WatchiousHealthServices\$RecoveryCallback.class")
        include("com/kebechet/watchious/health/WatchiousPermissionActivity*.class")
    }
    archiveFileName.set("watchious-health-api.jar")
    destinationDirectory.set(layout.buildDirectory.dir("watchious-binding"))
}
