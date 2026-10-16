# Android 体验端

本阶段面向三星 S25，APK 仅包含 ARM64。JDK17、Android SDK36、NDK28.2.13676358、Gradle8.13；依赖版本固定在 Gradle 文件中。使用当前 Mac 的 Android SDK，在本地 `local.properties` 配置 `sdk.dir`，不要提交本机路径。

```bash
cd apps/android
./gradlew :app:assembleDebug :app:testDebugUnitTest
```

构建会从 Google 官方地址下载固定版本 Face Landmarker 模型并核对 SHA-256，仅进入构建产物；手机运行时不下载模型。MediaPipe 固定为0.10.29：新版 XNNPACK 在 M4 ARM64 模拟器误选 SME/rdsvl 后 SIGILL，已通过实际反汇编及上游 issue6293交叉确认。固定版本已重新核验16KB原生库及APK对齐。

普通入口只有电脑选择、连接/识别/媒体状态、开始/停止。首次配对在 Mac 点一次允许；手机首次开始时请求相机及通知权限。模型在摄像头前台服务中每秒约3帧处理，只发四种粗粒度状态；停止、连接断开或相机故障释放相机并取消本轮资格。凭据使用 Android Keystore 的 AES-GCM 加密保存，不备份、不记录令牌，不上传或保存人脸图像/特征。

当前是头部方向代理，不能宣称精确眼动追踪。基准、方向阈值及去抖时间是 Pilot 初值，仍须在 S25 的实际位置/光照中验证。Android16/One UI版本目前按用户提供；模拟器为API35，不能代替三星的后台、锁屏、息屏和耗电测试。

`EmulatorConnectionTest` 只在模拟器运行，以显式传入的本机资格端口和指纹连接 `10.0.2.2`；固定地址不进入产品入口，资格接收端只监听Mac回环地址且不发布发现服务。测试APK不要安装到日常手机。

2026-10-17 短时后台功能：模拟器在活动页CREATED状态下持续1分钟，每5秒核验真实模型新帧、帧龄、TLS和观影状态，返回后停止服务；另已通过实际模型/相机、重连及旧轮次回调隔离测试。`WatchingService.lastFrame`仅为进程内时间戳，不保存图像或新增网络字段。用户已将30分钟、锁屏/息屏、发热耗电DEFERRED；短时结果不能称为S25后台稳定性通过。
