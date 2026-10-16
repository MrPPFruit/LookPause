package com.lookpause.android

import android.content.Context
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import android.os.Handler
import android.os.Looper
import java.net.InetAddress
import java.util.UUID

data class Computer(val id: String, val name: String, val address: String, val port: Int, val fingerprint: String)

@Suppress("DEPRECATION")
class Discovery(context: Context, private val changed: (List<Computer>) -> Unit, private val failed: () -> Unit) {
    private val manager = context.getSystemService(NsdManager::class.java)
    private val main = Handler(Looper.getMainLooper())
    private val computers = mutableMapOf<String, Computer>()
    private val pending = ArrayDeque<NsdServiceInfo>()
    private var resolving = false
    private var started = false
    private val listener = object : NsdManager.DiscoveryListener {
        override fun onDiscoveryStarted(type: String) = Unit
        override fun onDiscoveryStopped(type: String) = Unit
        override fun onStartDiscoveryFailed(type: String, code: Int) { main.post { started = false; failed() } }
        override fun onStopDiscoveryFailed(type: String, code: Int) = Unit
        override fun onServiceFound(info: NsdServiceInfo) { main.post {
            if (started && info.serviceType == "_lookpause._tcp." && pending.size < 32 &&
                pending.none { it.serviceName == info.serviceName }) { pending.add(info); resolveNext() }
        } }
        override fun onServiceLost(info: NsdServiceInfo) { main.post {
            pending.removeAll { it.serviceName == info.serviceName }
            computers.remove(info.serviceName); changed(computers.values.sortedBy { it.name })
        } }
    }
    fun start() {
        if (started) return
        started = true
        try { manager.discoverServices("_lookpause._tcp.", NsdManager.PROTOCOL_DNS_SD, listener) }
        catch (_: RuntimeException) { started = false; failed() }
    }
    fun stop() {
        if (started) { started = false; runCatching { manager.stopServiceDiscovery(listener) } }
        pending.clear(); computers.clear(); changed(emptyList())
    }
    private fun resolveNext() {
        if (resolving || pending.isEmpty() || !started) return
        resolving = true
        manager.resolveService(pending.removeFirst(), object : NsdManager.ResolveListener {
            override fun onResolveFailed(info: NsdServiceInfo, code: Int) { main.post { resolving = false; resolveNext() } }
            override fun onServiceResolved(info: NsdServiceInfo) { main.post {
                resolving = false
                if (started) {
                    val id = info.attributes["id"]?.toString(Charsets.UTF_8)
                    val fp = info.attributes["fp"]?.toString(Charsets.UTF_8)
                    val host = info.host
                    if (computers.size < 32 && info.attributes["v"]?.toString(Charsets.UTF_8) == "1" && id != null &&
                        runCatching { UUID.fromString(id) }.isSuccess && fp?.matches(Regex("[0-9a-f]{64}")) == true &&
                        host != null && isLocal(host) && info.port in 1..65535) {
                        computers[info.serviceName] = Computer(id, info.serviceName, host.hostAddress!!, info.port, fp)
                        changed(computers.values.sortedBy { it.name })
                    }
                }
                resolveNext()
            } }
        })
    }
    private fun isLocal(host: InetAddress): Boolean = host.isSiteLocalAddress || host.isLinkLocalAddress ||
        (host.address.size == 16 && (host.address[0].toInt() and 0xfe) == 0xfc)
}
