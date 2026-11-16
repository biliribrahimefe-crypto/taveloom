import java.util.Properties

plugins {
    id("com.android.application")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

// ---- Release signing (see docs/release/android-signing.md) ---------------------------------------------------------------
// The signing configuration lives OUTSIDE the repository: <user home>/.taveloom/signing/key.properties
// (keys: storeFile, storePassword, keyAlias, keyPassword). Nothing in this script prints or logs its values.
// A release build with missing/invalid signing FAILS; it never falls back to the debug key, except in CI when
// TAVELOOM_ALLOW_DEBUG_SIGNING=1 is set explicitly (GitHub Actions test APK). TAVELOOM_SIGNING_PROPERTIES overrides the file
// location (used only by tests of this logic).
val signingFile: File = System.getenv("TAVELOOM_SIGNING_PROPERTIES")?.takeIf { it.isNotBlank() }?.let { File(it) }
    ?: File(System.getProperty("user.home"), ".taveloom/signing/key.properties")
val signingProps = Properties().apply { if (signingFile.isFile) signingFile.inputStream().use { load(it) } }
val signingProblem: String? = run {
    val missing = listOf("storeFile", "storePassword", "keyAlias", "keyPassword").filter { signingProps.getProperty(it).isNullOrBlank() }
    when {
        !signingFile.isFile -> "signing file not found at ${signingFile.path}"
        missing.isNotEmpty() -> "signing file is missing required keys: $missing"
        !File(signingProps.getProperty("storeFile")).isFile -> "the keystore named by storeFile does not exist"
        else -> null
    }
}
// Debug signing is only ever allowed in CI, and only when asked for explicitly.
val allowDebugSigning = System.getenv("TAVELOOM_ALLOW_DEBUG_SIGNING") == "1" && System.getenv("CI") == "true"

gradle.taskGraph.whenReady {
    val releaseBuild = allTasks.any { it.project == project && Regex("(assemble|bundle|package)Release").matches(it.name) }
    if (releaseBuild && signingProblem != null && !allowDebugSigning) {
        throw GradleException(
            "Release signing is not configured: $signingProblem. Refusing to build an unsigned or debug-signed release. " +
                "See docs/release/android-signing.md (CI-only escape hatch: TAVELOOM_ALLOW_DEBUG_SIGNING=1 with CI=true)."
        )
    }
}

android {
    namespace = "com.tavrynt.taveloom"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    defaultConfig {
        // TODO: Specify your own unique Application ID (https://developer.android.com/studio/build/application-id.html).
        applicationId = "com.tavrynt.taveloom"
        // You can update the following values to match your application needs.
        // For more information, see: https://flutter.dev/to/review-gradle-config.
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        // Uses the version code from pubspec.yaml. When using split APKs, 1000 * ABI_VERSION
        // is added automatically by Flutter. (https://developer.android.com/studio/build/configure-apk-splits#configure-APK-versions)
        // You can force using the value of versionCode by specifying the `-P force-version-code-ignoring-abi=true`
        // flag during build.
        versionCode = flutter.versionCode
        versionName = flutter.versionName
    }

    signingConfigs {
        if (signingProblem == null) {
            create("release") {
                keyAlias = signingProps.getProperty("keyAlias")
                keyPassword = signingProps.getProperty("keyPassword")
                storeFile = File(signingProps.getProperty("storeFile"))
                storePassword = signingProps.getProperty("storePassword")
            }
        }
    }

    buildTypes {
        release {
            // No fallback: without valid signing the whole release build is rejected by the task-graph check above.
            signingConfig = when {
                signingProblem == null -> signingConfigs.getByName("release")
                allowDebugSigning -> signingConfigs.getByName("debug")
                else -> null
            }
            // R8 shrinking is OFF by default; set TAVELOOM_R8=1 to build with it. See docs/release/r8-shrinking.md.
            val r8 = System.getenv("TAVELOOM_R8") == "1"
            isMinifyEnabled = r8
            isShrinkResources = r8
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

flutter {
    source = "../.."
}
