package com.lookpause.android

import android.os.Build
import android.util.Base64
import org.json.JSONObject
import java.io.ByteArrayOutputStream
import java.net.InetSocketAddress
import java.net.Socket
import java.security.MessageDigest
import java.security.cert.CertificateException
import java.security.cert.X509Certificate
import java.util.UUID
import java.util.concurrent.ArrayBlockingQueue
import java.util.concurrent.Executors
import java.util.concurrent.ThreadPoolExecutor
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import javax.net.ssl.SSLContext
import javax.net.ssl.SSLSocket
import javax.net.ssl.X509TrustManager

class LanClient(
    private val computer: Computer, private val deviceId: String, private val store: CredentialStore,
    private val ready: () -> Unit, private val status: (String) -> Unit, private val ended: (String) -> Unit
) {
    private val closed = AtomicBoolean(false)
    private val writer = ThreadPoolExecutor(1, 1, 0, TimeUnit.SECONDS, ArrayBlockingQueue(16))
    private val heartbeat = Executors.newSingleThreadScheduledExecutor()
    @Volatile private var socket: Socket? = null
    @Volatile private var session: String? = null
    private var sequence = 0L

    fun connect() { Thread({ runConnection() }, "LookPause-TLS").start() }
    fun send(kind: String, intent: WatchingIntent? = null) {
        if (closed.get() || session == null) return
        try { writer.execute {
            try {
                val frame = JSONObject().put("v", 1).put("kind", kind).put("session", session).put("seq", ++sequence)
                if (intent != null) frame.put("intent", intent.name)
                write(frame)
            } catch (_: Exception) { close("连接中断，观影已停止") }
        } } catch (_: RuntimeException) { close("连接繁忙，观影已停止") }
    }
    fun close(reason: String = "已断开连接") {
        if (!closed.compareAndSet(false, true)) return
        runCatching { socket?.close() }
        heartbeat.shutdownNow(); writer.shutdownNow(); ended(reason)
    }
    private fun runConnection() {
        try {
            val saved = store.read(computer.id)
            check(saved == null || saved.fingerprint == computer.fingerprint) { "IDENTITY_CHANGED" }
            val trust = object : X509TrustManager {
                override fun getAcceptedIssuers(): Array<X509Certificate> = emptyArray()
                override fun checkClientTrusted(chain: Array<X509Certificate>, type: String) { throw CertificateException() }
                override fun checkServerTrusted(chain: Array<X509Certificate>, type: String) {
                    val leaf = chain.firstOrNull() ?: throw CertificateException()
                    leaf.checkValidity()
                    val actual = MessageDigest.getInstance("SHA-256").digest(leaf.encoded).joinToString("") { "%02x".format(it) }
                    if (actual != computer.fingerprint) throw CertificateException("PIN_MISMATCH")
                }
            }
            val context = SSLContext.getInstance("TLS").apply { init(null, arrayOf(trust), null) }
            val tcp = Socket()
            socket = tcp
            if (closed.get()) { tcp.close(); return }
            tcp.connect(InetSocketAddress(computer.address, computer.port), 5_000)
            val tls = context.socketFactory.createSocket(tcp, computer.address, computer.port, true) as SSLSocket
            socket = tls
            tls.enabledProtocols = tls.supportedProtocols.filter { it == "TLSv1.2" || it == "TLSv1.3" }.toTypedArray()
            tls.soTimeout = 8_000
            tls.startHandshake()
            tls.soTimeout = 65_000 // 首次 Mac 确认窗口最多等待60秒。
            val hello = JSONObject().put("v", 1).put("kind", "hello").put("deviceId", deviceId).put("name", Build.MODEL.take(64))
            if (saved != null) hello.put("token", saved.token)
            write(hello)
            val first = read()
            if (first.optString("kind") == "error") throw IllegalStateException(first.optString("error"))
            check(first.getInt("v") == 1 && first.getString("kind") == "ready")
            val newSession = first.getString("session")
            UUID.fromString(newSession)
            if (saved == null) {
                val token = first.getString("token")
                check(token.length == 44 && Base64.decode(token, Base64.NO_WRAP).size == 32)
                store.write(computer.id, Credential(computer.fingerprint, token))
            }
            if (closed.get()) return
            session = newSession
            tls.soTimeout = 8_000
            ready()
            heartbeat.scheduleAtFixedRate({ send("ping") }, 2, 2, TimeUnit.SECONDS)
            while (!closed.get()) {
                val frame = read()
                check(frame.getInt("v") == 1 && frame.optString("session") == session)
                when (frame.getString("kind")) {
                    "status", "ack" -> if (frame.has("status")) status(frame.getString("status").take(160))
                    "error" -> throw IllegalStateException(frame.optString("error"))
                    else -> error("INVALID_FRAME")
                }
            }
        } catch (error: Exception) {
            close(when (error.message) {
                "IDENTITY_CHANGED" -> "电脑身份已变化，已拒绝连接"
                "PAIRING_REJECTED" -> "Mac 未允许此次配对"
                "BUSY", "PAIRING_UNAVAILABLE" -> "电脑当前不可配对，请稍后重试"
                "INVALID_CREDENTIAL" -> "配对凭据未被接受，未连接"
                else -> "未能连接或连接已中断，请确认同一局域网及 Mac 状态"
            })
        } finally { close() }
    }
    private fun write(frame: JSONObject) {
        val bytes = (frame.toString() + "\n").toByteArray(Charsets.UTF_8)
        require(bytes.size <= 4096)
        check(!closed.get())
        socket!!.getOutputStream().apply { write(bytes); flush() }
    }
    private fun read(): JSONObject {
        val input = socket!!.getInputStream()
        val bytes = ByteArrayOutputStream()
        repeat(4096) {
            val value = input.read()
            check(value >= 0) { "EOF" }
            if (value == 10) { require(bytes.size() > 0); return JSONObject(bytes.toString("UTF-8")) }
            bytes.write(value)
        }
        error("FRAME_TOO_LARGE")
    }
}
