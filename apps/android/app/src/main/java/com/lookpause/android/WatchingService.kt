package com.lookpause.android

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Intent
import android.content.pm.ServiceInfo
import android.graphics.Bitmap
import android.graphics.Matrix
import android.os.Handler
import android.os.Looper
import android.os.SystemClock
import android.util.Size
import androidx.camera.core.CameraSelector
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.ImageProxy
import androidx.camera.core.resolutionselector.ResolutionSelector
import androidx.camera.core.resolutionselector.ResolutionStrategy
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.core.app.NotificationCompat
import androidx.core.app.ServiceCompat
import androidx.core.content.ContextCompat
import androidx.lifecycle.LifecycleService
import com.google.mediapipe.framework.image.BitmapImageBuilder
import com.google.mediapipe.tasks.core.BaseOptions
import com.google.mediapipe.tasks.vision.core.RunningMode
import com.google.mediapipe.tasks.vision.facelandmarker.FaceLandmarker
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicBoolean

class WatchingService : LifecycleService() {
    private val controller get() = (application as LookPauseApplication).controller
    private val executor = Executors.newSingleThreadExecutor()
    private val main = Handler(Looper.getMainLooper())
    private val stopped = AtomicBoolean(false)
    private var model: FaceLandmarker? = null
    private var provider: ProcessCameraProvider? = null
    private var analysis: ImageAnalysis? = null
    private val filter = DirectionFilter()
    private var lastAnalysis = 0L
    companion object {
        @Volatile internal var lastFrame = 0L
            private set
    }
    private var startedAt = 0L
    private var lastObservation: Observation? = null
    private var run: String? = null
    private val healthCheck = object : Runnable {
        override fun run() {
            if (stopped.get()) return
            val now = SystemClock.elapsedRealtime()
            if ((lastFrame > 0 && now - lastFrame > 3000) || (lastFrame == 0L && now - startedAt > 15000)) {
                run?.let { controller.failCamera(it, "摄像头观察中断，观影已停止") }; return
            }
            main.postDelayed(this, 1000)
        }
    }
    override fun onCreate() {
        super.onCreate()
        run = controller.currentRun
        if (run == null) { stopSelf(); return }
        val manager = getSystemService(NotificationManager::class.java)
        manager.createNotificationChannel(NotificationChannel("watching", "观影识别", NotificationManager.IMPORTANCE_LOW))
        val open = PendingIntent.getActivity(this, 0, Intent(this, MainActivity::class.java), PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT)
        val notification = NotificationCompat.Builder(this, "watching").setSmallIcon(R.drawable.ic_lookpause)
            .setContentTitle(getString(R.string.watching_notification, getString(R.string.app_name))).setContentText("图像仅在手机处理，点此查看状态或停止")
            .setContentIntent(open).setOngoing(true).build()
        try { ServiceCompat.startForeground(this, 1, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_CAMERA) }
        catch (_: RuntimeException) {
            controller.failCamera(run!!, "无法使用相机前台服务，请检查相机权限")
            stopSelf(); return
        }
        lastFrame = 0L
        startedAt = SystemClock.elapsedRealtime()
        main.post(healthCheck)
        executor.execute {
            try {
                if (stopped.get()) return@execute
                model = FaceLandmarker.createFromOptions(this, FaceLandmarker.FaceLandmarkerOptions.builder()
                    .setBaseOptions(BaseOptions.builder().setModelAssetPath("face_landmarker.task").build())
                    .setRunningMode(RunningMode.VIDEO).setNumFaces(2)
                    .setMinFaceDetectionConfidence(0.65f).setMinFacePresenceConfidence(0.65f)
                    .setMinTrackingConfidence(0.65f).build())
                main.post { if (!stopped.get()) bindCamera() }
            } catch (_: Exception) { controller.failCamera(run!!, "本地识别未能启动，观影已停止") }
        }
    }
    private fun bindCamera() {
        val future = ProcessCameraProvider.getInstance(this)
        future.addListener({
            if (stopped.get()) return@addListener
            try {
                val cameraProvider = future.get()
                provider = cameraProvider
                val imageAnalysis = ImageAnalysis.Builder()
                    .setResolutionSelector(ResolutionSelector.Builder().setResolutionStrategy(
                        ResolutionStrategy(Size(640, 480), ResolutionStrategy.FALLBACK_RULE_CLOSEST_LOWER_THEN_HIGHER)).build())
                    .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST).build()
                analysis = imageAnalysis
                imageAnalysis.setAnalyzer(executor, ::analyze)
                val camera = cameraProvider.bindToLifecycle(this, CameraSelector.DEFAULT_FRONT_CAMERA, imageAnalysis)
                camera.cameraInfo.cameraState.observe(this) { state ->
                    if (state.error != null && !stopped.get()) controller.failCamera(run!!, "相机不可用，观影已停止")
                }
                controller.cameraReady(run!!)
            } catch (_: Exception) { controller.failCamera(run!!, "无法使用前置相机，请检查权限或相机占用") }
        }, ContextCompat.getMainExecutor(this))
    }
    private fun analyze(image: ImageProxy) {
        try {
            if (stopped.get()) return
            val now = SystemClock.elapsedRealtime()
            if (now - lastAnalysis < 330) return
            lastAnalysis = now
            val bitmap = image.toBitmap()
            val rotated = if (image.imageInfo.rotationDegrees == 0) bitmap else Bitmap.createBitmap(bitmap, 0, 0, bitmap.width, bitmap.height,
                Matrix().apply { postRotate(image.imageInfo.rotationDegrees.toFloat()) }, false)
            try {
                val input = BitmapImageBuilder(rotated).build()
                val result = try { model!!.detectForVideo(input, now) } finally { input.close() }
                lastFrame = SystemClock.elapsedRealtime()
                val faces = result.faceLandmarks()
                val pose = if (faces.size == 1) {
                    val points = faces[0]
                    val left = points[33]; val right = points[263]; val nose = points[1]
                    val dx = (right.x() - left.x()) * rotated.width
                    val dy = (right.y() - left.y()) * rotated.height
                    val lengthSquared = dx * dx + dy * dy
                    if (lengthSquared < 0.0036f * rotated.width * rotated.width) null else {
                        val nx = (nose.x() - (left.x() + right.x()) / 2) * rotated.width
                        val ny = (nose.y() - (left.y() + right.y()) / 2) * rotated.height
                        Orientation((nx * dx + ny * dy) / lengthSquared, (ny * dx - nx * dy) / lengthSquared)
                    }
                } else null
                val observation = filter.observe(pose, faces.size, now)
                if (lastObservation != observation) {
                    lastObservation = observation
                    controller.observe(run!!, observation.intent, observation.description)
                }
            } finally {
                if (rotated !== bitmap) rotated.recycle()
                bitmap.recycle()
            }
        } catch (_: Exception) { controller.failCamera(run!!, "识别发生错误，观影已停止") }
        finally { image.close() }
    }
    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        super.onStartCommand(intent, flags, startId)
        return START_NOT_STICKY
    }
    override fun onDestroy() {
        stopped.set(true)
        main.removeCallbacks(healthCheck)
        analysis?.clearAnalyzer(); provider?.unbindAll()
        executor.execute { model?.close(); model = null }
        executor.shutdown()
        stopForeground(STOP_FOREGROUND_REMOVE)
        run?.let { controller.serviceEnded(it) }
        super.onDestroy()
    }
}
