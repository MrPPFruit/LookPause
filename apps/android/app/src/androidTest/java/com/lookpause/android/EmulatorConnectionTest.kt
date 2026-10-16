package com.lookpause.android

import android.Manifest
import android.app.ActivityManager
import android.content.Context
import android.graphics.Bitmap
import android.os.Build
import android.os.SystemClock
import androidx.lifecycle.Lifecycle
import androidx.test.core.app.ActivityScenario
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.*
import org.junit.Assume.assumeTrue
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class EmulatorConnectionTest {
    @Test fun realTlsModelAndCameraLifecycle() {
        // 仅模拟器资格入口；不向产品添加测试按钮、自动配对开关或传入地址的外部入口。
        assumeTrue(Build.MODEL.contains("sdk") || Build.FINGERPRINT.contains("generic"))
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val context = instrumentation.targetContext
        val args = InstrumentationRegistry.getArguments()
        val fp = requireNotNull(args.getString("fingerprint"))
        val port = requireNotNull(args.getString("port")).toInt()
        require(fp.matches(Regex("[0-9a-f]{64}")) && port in 1..65535)
        instrumentation.uiAutomation.grantRuntimePermission(context.packageName, Manifest.permission.CAMERA)
        if (Build.VERSION.SDK_INT >= 33) instrumentation.uiAutomation.grantRuntimePermission(context.packageName, Manifest.permission.POST_NOTIFICATIONS)
        val controller = (context.applicationContext as LookPauseApplication).controller
        ActivityScenario.launch(MainActivity::class.java).use { activity ->
            activity.onActivity { controller.connect(Computer("311b8a40-64d8-44ee-9ad8-e0a13522c25f", "本机资格接收端", "10.0.2.2", port, fp)) }
            waitUntil(10_000) { controller.state.value.connected }
            activity.onActivity { controller.start() }
            waitUntil(25_000) { controller.state.value.recognition.startsWith("未检测到人脸，请") }
            assertTrue(controller.state.value.watching)
            assertTrue(controller.state.value.connected)
            val firstRun = requireNotNull(controller.currentRun)
            context.openFileOutput("qualification-running.png", Context.MODE_PRIVATE).use {
                instrumentation.uiAutomation.takeScreenshot().apply { compress(Bitmap.CompressFormat.PNG, 100, it); recycle() }
            }
            // 模拟场景无真人；只有实际模型返回零脸才能产生上述状态，不伪造方向。
            Thread.sleep(2000)
            assertTrue(controller.state.value.watching)
            activity.onActivity { controller.stop() }
            waitUntil(5_000) { !controller.state.value.watching && !serviceRunning(context) }
            assertTrue(controller.state.value.connected)
            activity.onActivity { controller.connect(Computer("311b8a40-64d8-44ee-9ad8-e0a13522c25f", "本机资格接收端", "10.0.2.2", port, fp)) }
            waitUntil(10_000) { controller.state.value.connected }
            assertFalse(controller.state.value.watching)
            activity.onActivity { controller.start() }
            waitUntil(25_000) { controller.state.value.recognition.startsWith("未检测到人脸，请") }
            controller.failCamera(firstRun, "旧摄像头的迟到故障")
            controller.observe(firstRun, WatchingIntent.AWAY, "旧摄像头的迟到方向")
            controller.serviceEnded(firstRun)
            Thread.sleep(300)
            assertTrue(controller.state.value.watching)
            assertTrue(controller.state.value.recognition.startsWith("未检测到人脸，请"))
            activity.onActivity { controller.stop() }
            waitUntil(5_000) { !controller.state.value.watching && !serviceRunning(context) }
        }
    }
    @Test fun cameraAndTlsRemainLiveWithActivityInBackground() {
        assumeTrue(Build.MODEL.contains("sdk") || Build.FINGERPRINT.contains("generic"))
        val instrumentation = InstrumentationRegistry.getInstrumentation()
        val context = instrumentation.targetContext
        val args = InstrumentationRegistry.getArguments()
        val minutes = (args.getString("backgroundMinutes") ?: "1").toInt()
        require(minutes in 1..30)
        val fp = requireNotNull(args.getString("fingerprint"))
        val port = requireNotNull(args.getString("port")).toInt()
        instrumentation.uiAutomation.grantRuntimePermission(context.packageName, Manifest.permission.CAMERA)
        val controller = (context.applicationContext as LookPauseApplication).controller
        ActivityScenario.launch(MainActivity::class.java).use { activity ->
            try {
                activity.onActivity { controller.connect(Computer("311b8a40-64d8-44ee-9ad8-e0a13522c25f", "本机资格接收端", "10.0.2.2", port, fp)) }
                waitUntil(10_000) { controller.state.value.connected }
                activity.onActivity { controller.start() }
                waitUntil(25_000) { WatchingService.lastFrame > 0 }
                activity.moveToState(Lifecycle.State.CREATED)
                val until = SystemClock.elapsedRealtime() + minutes * 60_000L
                var previousFrame = WatchingService.lastFrame
                while (SystemClock.elapsedRealtime() < until) {
                    Thread.sleep(5000)
                    assertTrue(controller.state.value.connected)
                    assertTrue(controller.state.value.watching)
                    val frame = WatchingService.lastFrame
                    assertTrue("后台模型必须继续处理新帧", frame > previousFrame)
                    assertTrue("相机观察不能过期", SystemClock.elapsedRealtime() - frame < 3000)
                    previousFrame = frame
                }
                activity.moveToState(Lifecycle.State.RESUMED)
            } finally {
                instrumentation.runOnMainSync { controller.stop() }
                waitUntil(5_000) { !serviceRunning(context) }
            }
        }
    }

    private fun waitUntil(timeout: Long, condition: () -> Boolean) {
        val deadline = System.nanoTime() + timeout * 1_000_000
        while (!condition() && System.nanoTime() < deadline) Thread.sleep(100)
        assertTrue("资格路径未在限时内达到预期状态", condition())
    }
    @Suppress("DEPRECATION")
    private fun serviceRunning(context: Context) = context.getSystemService(ActivityManager::class.java)
        .getRunningServices(20).any { it.service.className == WatchingService::class.java.name }
}
