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
        // Never raised: adb refuses to replace an installed app with a lower versionCode, and an app switches between
        // Watchious versions, so their live hosts, freely. versionName says which Watchious version this host is of.
        versionCode = 1
        versionName = (project.findProperty("watchiousVersion") as String?) ?: "0.0.0-dev"
    }

    // The live host ships inside Kebechet.Watchious.Sdk (pack.sh builds it), and adb replaces an installed app only with
    // one of the same signature. So every release signs it with this one key, committed on purpose: the live host is a
    // development tool, which nothing trusts for its signature, and anyone may build it. ⚠️ Never sign a real app with
    // this key: its password is public.
    signingConfigs {
        create("live") {
            storeFile = file("watchious-live.keystore")
            storeType = "pkcs12"
            storePassword = "watchious-live"
            keyAlias = "watchious-live"
            keyPassword = "watchious-live"
        }
    }

    buildTypes {
        release {
            // R8: 5 MB instead of 44 MB, mostly material-icons-extended.
            isMinifyEnabled = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            signingConfig = signingConfigs.getByName("live")
        }
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
