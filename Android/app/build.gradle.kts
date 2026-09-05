plugins {
    id("com.android.application")
}

android {
    namespace = "com.max.kitherramientas"
    compileSdk = 37

    defaultConfig {
        applicationId = "com.max.kitherramientas"
        minSdk = 26
        targetSdk = 36
        versionCode = 9
        versionName = "0.9.0"
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
