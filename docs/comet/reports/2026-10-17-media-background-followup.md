# 2026-10-17 媒体、回退、自启与短时后台验证

结论：本轮功能增量已实现，并取得真实播放器技术证据；普通Mac新版已启动，系统权限与登录登记生效。S25已安装版本的真实YouTube观看闭环沿用前次证据，更新后回退的S25整链没有复测。变更保持active/UNINTEGRATED，不同步canonical spec，不宣称全播放器兼容或整阶段交付。

用户最新范围：先把功能测通，暂不关注长时间稳定性。30分钟后台、锁屏/息屏及耗电DEFERRED，不再作为本轮完成条件，不把主动终止长测当成通过或自然崩溃。

## 环境与执行入口

Mac macOS26.6/25G72，Xcode27.0/27A266a，Chrome155.0.8059.82；Android主开发机仍为Mac。模拟器API35/Android15/ARM64；S25的Android16/One UI8.5仍按用户提供，未取得设备独立版本读取。

普通入口 `.local/LookPause.app`；资格入口由同一Swift源码构建，`receiver-test`仅监听loopback、不发布Bonjour，固定认证观看意图驱动同一MediaSession。测试网页脚本仅作独立只读观察；不进入产品、不启用Chrome Apple Events JavaScript、不安装扩展。本机日志、截图、测试视频及真实配对凭据均留在忽略目录或系统临时目录，不上传公开仓库。

## 父结果、实现与证据

| Acceptance | 本轮实现owner | 实际证据与状态 | 反例/剩余GAP |
|---|---|---|---|
| LPEXP-01 | MenuApp.swift、构建脚本 | 普通进程AX/InputListen均true；默认登录状态notFound→enabled，独立BTM登记无错误；用户验证菜单关闭/重开登录启动与退出；BTM最终Disposition=[enabled, allowed, notified] | 登记enabled不等于下次真实登录已启动；本轮菜单截图捕获时用户已回到对话，未拍到展开状态 |
| LPEXP-02 | 既有Receiver/认证契约 | 资格接收端真实TLS/固定意图复用；普通接收端重新启动且可等待手机 | 本轮不扩大配对能力或冻结正式协议；首配TOFU限制不变 |
| LPEXP-03 | GenericPilot.swift、MediaSession.swift、ResumeRewind.swift | B站/QuickTime/YouTube真实媒体暂停恢复；初始手动暂停零播放；回退有独立实际进度；重复ATTENDING无新增执行，STOP后意图被拒绝；恢复中新的AWAY仍再次暂停 | 隐藏网页AX进度缓存会报告回退未确认；同页换片、多媒体、全语言/全应用边界未全部验证。技术入口不是更新后S25产品整链 |
| LPEXP-04 | WatchingService.kt、EmulatorConnectionTest.kt | 1分钟后台实际新帧/TLS/观影状态持续有效，返回后停止服务；真实模型/CameraX/重连/旧轮次隔离测试通过 | 模拟器通过不能当作三星后台或耗电通过 |
| LPEXP-05 | 既有S25普通客户端 | 前次真实转头/回看/接管与STOP证据保留；本轮曾观察普通接收端断开及后续START/意图 | 不将口述“关机”未经核验地映射到某条日志；新回退未与S25重新联调；长时间稳定性DEFERRED |

双向映射：docs/development-plan.md 手机到Mac体验阶段 → proposal的LPEXP-01～05 → change design/spec → tasks3.1～3.4 → 上表源码owner与证据。本轮没有Windows或新的用户控制流。

## 根因与最小修复

1. 自启：SMAppService首次返回notFound，旧代码只处理notRegistered，默认开启意愿存在但未登记。现在将两种未登记状态一并注册，requiresApproval保留系统决定。普通进程已enabled；菜单同时显示辅助功能/输入监控缺失，避免权限状态误导。
2. B站：模糊“播放/暂停”文案不含状态，但AXDOMClassList为数组、播放器祖先有明确paused标记；进度被拆成多个静态文本。通用解析组合媒体区域时间文本，仅实际前进补充playing，静止不判paused，loading/ended/冲突拒绝。自绘按钮AXPress成功不等于生效，使用核验状态/焦点后的固定Space；一次失败不会盲重试。
3. QuickTime：原生播放控件为AXCheckBox，文案可固定为“播放/暂停”，0/1值才提供判态。原生秒时间线可写，恢复后减5秒，保持播放。结束位置返回UNKNOWN。没有新增脚本或Apple Events自动化权限。
4. YouTube回退：网页ARIA数值被误认为原生可写时间线，AXSetValue成功返回但没有seek；AXDecrement也不能单靠返回值确认。最终只对非网页原生秒时间线写入，网页使用已聚焦播放控件的固定Left。真实网页焦点到WebArea超过原16层，旧检查保守拒绝；改为最多64层，输入框和超界仍拒绝。方向键保留系统numericPad/function语义；单独修改标记没有解决故障，不把它当根因证明。
5. 所有权：进度样本绑定进程、窗口、上下文，不能跨同名窗口借用证据；操作前再核验接管/前台。恢复或回退中收到新离开，不再因中间phase而丢失。只豁免本进程、本轮随机标记的固定Space/Left，不开放远程按键。

## 真实媒体结果

- B站：资格链观察到实际暂停、恢复并回退一次；初始手动暂停后离开/回看，独立DOM显示同一820.966秒且仍暂停。曾发生一次执行前判态不一致，安全拒绝；不将后续样例成功推广为稳定性保证。
- QuickTime：初始手动暂停约19.993秒，离开/回看保持不动；自动暂停后恢复回退，独立原生时间从暂停27.190秒变为播放22.620秒。样片结尾52.209秒返回UNKNOWN，零恢复。
- YouTube：初始手动暂停后离开/回看显示“视频已暂停，交由你控制”；同一资格核心实际暂停/恢复。最终独立DOM暂停688.286850秒；恢复发送到采样间隔6.697秒，采样播放689.704610秒，扣除正常播放耗时后回退约5.279秒。证明来自实际进度，不能只看键盘事件或AX返回值。
- 重复ATTENDING仅产生“看向屏幕”，无新的恢复/回退；恢复期间新AWAY随后再次确认暂停。STOP后ATTENDING返回NOT_WATCHING_OR_INVALID_INTENT，不再播放。
- 本轮固定Shift输入监听诊断PASS、播放器命令0。CUA原生Escape操作未被系统事件监听观察到，不能当作物理键鼠保护的有效测试；实际S25→YouTube人工键鼠接管证据仍来自前次。不能把工具内窗口操作冒充物理事件真链。
- 已确认某些隐藏网页控件的AX进度会滞后真实DOM：自动回退实际生效但2秒确认可能超时，菜单如实显示“回退结果未确认”，不重复执行。此限制保留，未增加浏览器权限或强制露出控制条。

## 构建与自动化

- `bash apps/macos/build-probe.sh`、`bash apps/macos/build-app.sh`：PASS，内置self-test PASS。
- `swift build --package-path apps/macos -c release`：PASS；`codesign --verify --strict .local/LookPause.app`：PASS。
- `.local/LookPauseMediaProbe.app/Contents/MacOS/LookPauseMediaProbe input-watch-test`：PASS，USER_INPUT，零媒体命令。
- Android `:app:assembleDebug :app:assembleDebugAndroidTest :app:testDebugUnitTest --offline`：PASS，3个单元测试。
- SDK `adb shell am instrument -w -e class com.lookpause.android.EmulatorConnectionTest#cameraAndTlsRemainLiveWithActivityInBackground -e backgroundMinutes 1 -e port <loopback-port> -e fingerprint <test-certificate-fingerprint> com.lookpause.android.test/androidx.test.runner.AndroidJUnitRunner`：62.627秒，OK(1test)。每5秒核验实际模型新帧推进、帧龄<3秒、连接/观影仍有效；活动页CREATED→RESUMED，finally停止服务。
- 同一方式执行 `#realTlsModelAndCameraLifecycle`：5.428秒，OK(1test)，真实模型/CameraX、停止、重连及旧轮次迟到回调隔离。
- Gradle connectedDebugAndroidTest离线入口因未缓存UTP依赖失败，改用已安装SDK和已构建测试APK的标准instrumentation命令，没有下载新依赖。Android lint此前的依赖网络阻塞未解决，不能记为PASS。
- 原30分钟instrumentation被主动force-stop，输出Process crashed是人工终止，不计自然失败或长测PASS。新的Android产物只有进程内帧时间戳可见性变化，没有要求S25重装。

## 代码审查与整合边界

本轮直接阅读调用链并检查暂停资格、键盘焦点、固定事件标记、停止/断开及前台变化；修复了恢复期间离开丢失和跨窗口进度借用风险。未进行独立审查，不把作者自审写为独立安全审查。完整同页换片身份与平台最后执行间隙仍有既有GAP。

目标短分支codex/mac-phone-experience；目标整合分支codex/experience-integration。本轮只是该active阶段的功能增量，整阶段GAP未清零，暂不合main或归档。本轮保存并推送该开发分支，具体提交及远端核验在最终回复记录；正式集成commit仍无，能力保持UNINTEGRATED。
