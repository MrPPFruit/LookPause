package com.lookpause.android

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import androidx.lifecycle.compose.collectAsStateWithLifecycle

class MainActivity : ComponentActivity() {
    private val controller get() = (application as LookPauseApplication).controller
    private val cameraPermission = registerForActivityResult(ActivityResultContracts.RequestPermission()) { allowed ->
        if (allowed) requestNotificationThenStart() else controller.cameraPermissionDenied()
    }
    private val notificationPermission = registerForActivityResult(ActivityResultContracts.RequestPermission()) { controller.start() }
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            val state by controller.state.collectAsStateWithLifecycle()
            MaterialTheme(colorScheme = if (isSystemInDarkTheme()) darkColorScheme(primary = Color(0xff9bd3c2)) else lightColorScheme(primary = Color(0xff35675c))) {
                Scaffold { padding ->
                    Column(Modifier.fillMaxSize().padding(padding).verticalScroll(rememberScrollState()).padding(24.dp), verticalArrangement = Arrangement.spacedBy(20.dp)) {
                        Text("LookPause", style = MaterialTheme.typography.headlineLarge)
                        Text("看向屏幕，继续观看。", style = MaterialTheme.typography.titleMedium)
                        Card(Modifier.fillMaxWidth()) {
                            Column(Modifier.padding(20.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                                Text("连接状态", style = MaterialTheme.typography.labelLarge)
                                Text(state.connection, style = MaterialTheme.typography.bodyLarge)
                                HorizontalDivider()
                                Text("观看方向", style = MaterialTheme.typography.labelLarge)
                                Text(state.recognition)
                                Text("电脑：" + state.media, style = MaterialTheme.typography.bodyMedium)
                            }
                        }
                        Text("选择电脑", style = MaterialTheme.typography.titleMedium)
                        if (state.computers.isEmpty()) Text("正在寻找同一局域网中的 LookPause 电脑…", style = MaterialTheme.typography.bodyMedium)
                        state.computers.forEach { computer ->
                            OutlinedButton(onClick = { controller.connect(computer) }, enabled = !state.connecting && !state.watching,
                                modifier = Modifier.fillMaxWidth().heightIn(min = 48.dp)) {
                                Text(computer.name + if (state.connected && state.selectedId == computer.id) " · 已连接" else "")
                            }
                        }
                        Button(onClick = { if (state.watching) controller.stop() else begin() }, enabled = state.connected,
                            modifier = Modifier.fillMaxWidth().heightIn(min = 52.dp)) {
                            Text(if (state.watching) "停止观影" else "开始观影")
                        }
                        Text("首次连接在 Mac 点一次允许。开始后请短暂看向电脑屏幕建立基准。", style = MaterialTheme.typography.bodyMedium)
                        Text("图像仅在手机本地处理，不保存、不上传。状态不明确时保持视频原样。", style = MaterialTheme.typography.bodySmall)
                    }
                }
            }
        }
    }
    override fun onStart() { super.onStart(); controller.discover() }
    override fun onStop() { controller.stopDiscovery(); super.onStop() }
    private fun begin() {
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED) requestNotificationThenStart()
        else cameraPermission.launch(Manifest.permission.CAMERA)
    }
    private fun requestNotificationThenStart() {
        if (Build.VERSION.SDK_INT >= 33 && ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED)
            notificationPermission.launch(Manifest.permission.POST_NOTIFICATIONS)
        else controller.start()
    }
}
