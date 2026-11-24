import org.jetbrains.kotlin.gradle.dsl.JvmTarget

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "com.kebechet.watchious.live"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.kebechet.watchious.live"
        minSdk = 30
        targetSdk = 36
        versionCode = 1
        versionName = "1.0"
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
    implementation(project(":host"))
}

// Wear Compose 1.7 asks apps for compileSdk 37 and Android Gradle plugin 9.1, which this repository doesn't use yet.
// The host library builds against it with compileSdk 36, and the .NET apps, which ship the same libraries, never run
// this check.
tasks.configureEach {
    if (name.startsWith("check") && name.endsWith("AarMetadata")) {
        enabled = false
    }
}
