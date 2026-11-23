// Applied by each module that ships in a .NET package.
//
// The .NET package takes the module's Maven dependencies from Microsoft's NuGet bindings of them (Xamarin.AndroidX,
// Xamarin.GooglePlayServices...), so NuGet keeps one copy of each library in the app, shared with every other package
// built on them. This task writes the module's POM; .NET for Android's Java dependency verification reads it and fails
// the .NET build when the project references no package for one of these dependencies.
val watchiousDependencies = provider {
    listOf("api", "implementation")
        .flatMap { configurations.getByName(it).dependencies }
        .filterIsInstance<ExternalModuleDependency>()
        .map { "${it.group}:${it.name}:${it.version}" }
}

tasks.register("watchiousPom") {
    val pom = layout.buildDirectory.file("watchious-pom/${project.name}.pom")
    val artifactId = project.name
    inputs.property("dependencies", watchiousDependencies)
    outputs.file(pom)
    doLast {
        val dependencies = watchiousDependencies.get().joinToString("\n") { coordinates ->
            val (group, name, version) = coordinates.split(":")
            "    <dependency><groupId>$group</groupId><artifactId>$name</artifactId><version>$version</version></dependency>"
        }
        pom.get().asFile.writeText(
            """
            |<?xml version="1.0" encoding="UTF-8"?>
            |<project xmlns="http://maven.apache.org/POM/4.0.0">
            |  <modelVersion>4.0.0</modelVersion>
            |  <groupId>com.kebechet.watchious</groupId>
            |  <artifactId>$artifactId</artifactId>
            |  <version>1.0.0</version>
            |  <packaging>aar</packaging>
            |  <dependencies>
            |$dependencies
            |  </dependencies>
            |</project>
            |""".trimMargin()
        )
    }
}

// One `gradlew assembleRelease` makes everything the .NET projects read: the AAR, its POM and the binding jar.
tasks.matching { it.name == "assembleRelease" }.configureEach {
    dependsOn("watchiousPom", "watchiousBindingApi")
}
