import java.security.MessageDigest

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
}
android {
    namespace = "com.lookpause.android"
    compileSdk = 36
    ndkVersion = "28.2.13676358"
    defaultConfig {
        applicationId = "com.lookpause.android"
        minSdk = 26
        targetSdk = 36
        versionCode = 2
        versionName = "0.1.0-pilot.2"
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        ndk { abiFilters += "arm64-v8a" }
    }
    buildFeatures { compose = true }
    compileOptions { sourceCompatibility = JavaVersion.VERSION_17; targetCompatibility = JavaVersion.VERSION_17 }
    kotlinOptions { jvmTarget = "17" }
    sourceSets["main"].assets.srcDir(layout.buildDirectory.dir("generated/modelAssets"))
}

// 模型只进入构建产物；固定官方版本和哈希，不提交权重或在手机启动时联网下载。
val prepareFaceModel by tasks.registering {
    val target = layout.buildDirectory.file("generated/modelAssets/face_landmarker.task")
    outputs.file(target)
    doLast {
        val file = target.get().asFile
        val expected = "64184e229b263107bc2b804c6625db1341ff2bb731874b0bcc2fe6544e0bc9ff"
        fun digest() = MessageDigest.getInstance("SHA-256").digest(file.readBytes()).joinToString("") { "%02x".format(it) }
        if (!file.exists()) {
            file.parentFile.mkdirs()
            val connection = uri("https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task").toURL().openConnection()
            connection.connectTimeout = 20_000; connection.readTimeout = 60_000
            try { connection.getInputStream().use { input -> file.outputStream().use { input.copyTo(it) } } }
            catch (error: Exception) { file.delete(); throw error }
        }
        check(digest() == expected) { "本地视觉模型校验失败，请删除构建缓存后重试" }
    }
}
tasks.named("preBuild").configure { dependsOn(prepareFaceModel) }

dependencies {
    implementation(platform("androidx.compose:compose-bom:2025.10.00"))
    implementation("androidx.activity:activity-compose:1.11.0")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.9.4")
    implementation("androidx.lifecycle:lifecycle-service:2.9.4")
    implementation("androidx.camera:camera-camera2:1.5.1")
    implementation("androidx.camera:camera-lifecycle:1.5.1")
    // M4 ARM64 模拟器在新版 XNNPACK 的 SME/rdsvl 分派处 SIGILL，见上游 issue #6293。
    implementation("com.google.mediapipe:tasks-vision:0.10.29")
    testImplementation("junit:junit:4.13.2")
    androidTestImplementation("androidx.test:runner:1.6.2")
    androidTestImplementation("androidx.test.ext:junit:1.2.1")
    androidTestImplementation("androidx.test:core:1.6.1")
}
