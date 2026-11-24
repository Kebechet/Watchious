pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}

rootProject.name = "watchious-wearos"
include(":host")
include(":messaging")
include(":health")
// Live mode's host app (Watchious.Preview's --live), the Compose host without .NET.
include(":live")
