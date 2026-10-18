package com.lookpause.android

import android.app.Application
import android.content.Context
import android.content.Intent
import android.os.Handler
import android.os.Looper
import androidx.core.content.ContextCompat
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import java.util.UUID

class LookPauseApplication : Application() {
    val controller by lazy { AppController(this) }
}

data class PhoneState(
    val computers: List<Computer> = emptyList(), val selectedId: String? = null,
    val connecting: Boolean = false, val connected: Boolean = false, val watching: Boolean = false,
    val connection: String = "正在寻找同一局域网的电脑", val recognition: String = "尚未开始识别",
    val media: String = "尚未开始观影"
)

class AppController(private val context: Context) {
    private val main = Handler(Looper.getMainLooper())
    private val mutable = MutableStateFlow(PhoneState())
    val state = mutable.asStateFlow()
    private val store = CredentialStore(context)
    private val preferences = context.getSharedPreferences("device", Context.MODE_PRIVATE)
    private val deviceId = preferences.getString("id", null) ?: UUID.randomUUID().toString().also {
        preferences.edit().putString("id", it).apply()
    }
    private var client: LanClient? = null
    var currentRun: String? = null
        private set
    private val discovery = Discovery(context,
        { computers -> mutable.update { it.copy(computers = computers) } },
        { mutable.update { it.copy(connection = "暂时无法发现电脑，请确认局域网连接") } })
    fun discover() = discovery.start()
    fun stopDiscovery() = discovery.stop()
    fun connect(computer: Computer) {
        stop()
        val previous = client; client = null; previous?.close()
        mutable.update { it.copy(selectedId = computer.id, connecting = true, connected = false,
            connection = "正在连接；首次请在 Mac 点允许", media = "尚未开始观影") }
        lateinit var next: LanClient
        next = LanClient(computer, deviceId, store,
            ready = { main.post { if (client === next) mutable.update { it.copy(connecting = false, connected = true, connection = "已连接：" + computer.name) } } },
            status = { text -> main.post { if (client === next) mutable.update { it.copy(media = text) } } },
            ended = { text -> main.post { if (client === next) {
                client = null; stop()
                mutable.update { it.copy(connecting = false, connected = false, connection = text) }
            } } })
        client = next; next.connect()
    }
    fun start() {
        if (!state.value.connected || state.value.watching) return
        val run = UUID.randomUUID().toString()
        currentRun = run
        mutable.update { it.copy(watching = true, recognition = "请短暂看向电脑屏幕，正在建立基准") }
        try { ContextCompat.startForegroundService(context, Intent(context, WatchingService::class.java)) }
        catch (_: RuntimeException) { failCamera(run, "无法启动摄像头，请保持应用可见并检查相机权限") }
    }
    fun cameraPermissionDenied() { mutable.update { it.copy(recognition = "需要相机权限才能开始本地识别") } }
    fun cameraReady(run: String) { if (state.value.watching && currentRun == run) client?.send("start") }
    fun observe(run: String, intent: WatchingIntent, description: String) { main.post {
        if (state.value.watching && currentRun == run) {
            mutable.update { it.copy(recognition = description) }
            client?.send("intent", intent)
        }
    } }
    fun stop() {
        currentRun = null
        if (state.value.watching) client?.send("stop")
        mutable.update { it.copy(watching = false, recognition = "识别已停止") }
        context.stopService(Intent(context, WatchingService::class.java))
    }
    fun failCamera(run: String, message: String) { main.post {
        if (state.value.watching && currentRun == run) {
            client?.send("intent", WatchingIntent.UNKNOWN)
            stop()
            mutable.update { it.copy(recognition = message) }
        }
    } }
    fun serviceEnded(run: String) { main.post {
        if (state.value.watching && currentRun == run) { stop(); mutable.update { it.copy(recognition = "摄像头服务已停止") } }
    } }
}
