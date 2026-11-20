import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    id("com.android.library")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
}

android {
    namespace = "com.kebechet.watchious.host"
    compileSdk = 36

    defaultConfig {
        minSdk = 30
    }

    buildFeatures {
        compose = true
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
}

kotlin {
    compilerOptions {
        jvmTarget.set(JvmTarget.JVM_17)
    }
}

dependencies {
    implementation("androidx.wear.compose:compose-material3:1.6.2")
    implementation("androidx.wear.compose:compose-foundation:1.6.2")
    implementation("androidx.lifecycle:lifecycle-runtime:2.9.4")
    implementation("androidx.savedstate:savedstate:1.3.3")
}

// The .NET app can't consume Maven dependencies of a plain AAR, so every Java library the host needs at
// runtime is copied next to it and added to the .NET project as an unbound AndroidLibrary.
tasks.register<Sync>("watchiousRuntimeDependencies") {
    from(configurations.named("releaseRuntimeClasspath"))
    into(layout.buildDirectory.dir("watchious-runtime"))
}

// The .NET binding generator binds this jar, not the AAR: see Watchious.Hosts.WearOS.csproj.
tasks.register<Jar>("watchiousBindingApi") {
    from(tasks.named<JavaCompile>("compileReleaseJavaWithJavac").flatMap { it.destinationDirectory }) {
        include("com/kebechet/watchious/host/WatchiousComposeHost.class")
        include("com/kebechet/watchious/host/WatchiousEventListener.class")
    }
    archiveFileName.set("watchious-host-api.jar")
    destinationDirectory.set(layout.buildDirectory.dir("watchious-binding"))
}
