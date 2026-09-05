plugins {
    id("com.android.application")
}

android {
    namespace = "com.max.kitherramientas"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.max.kitherramientas"
        minSdk = 26
        targetSdk = 36
        versionCode = 10
        versionName = "1.0.0"
    }

    buildTypes {
        release {
            isMinifyEnabled = false
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
}
