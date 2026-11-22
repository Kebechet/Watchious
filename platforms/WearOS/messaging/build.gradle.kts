plugins {
    id("com.android.library")
}

android {
    namespace = "com.kebechet.watchious.messaging"
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
    implementation("com.google.android.gms:play-services-wearable:20.0.1")
}

apply(from = rootProject.file("gradle/watchious-dotnet.gradle.kts"))

// The .NET binding generator binds this jar, not the AAR: see Watchious.Messaging.csproj.
tasks.register<Jar>("watchiousBindingApi") {
    from(tasks.named<JavaCompile>("compileReleaseJavaWithJavac").flatMap { it.destinationDirectory }) {
        include("com/kebechet/watchious/messaging/WatchiousDataLayer*.class")
    }
    archiveFileName.set("watchious-messaging-api.jar")
    destinationDirectory.set(layout.buildDirectory.dir("watchious-binding"))
}
