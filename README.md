# PausePal

原名 LookPause；2026-10-18 用户确认仅更改显示名称。安装/配对身份、服务标识和工程路径保持，以兼容已有设备。

个人自用观影小工具。三星 Android 手机本地识别观看方向，主动连接家庭局域网内选定的一台 Windows 或 Mac。桌面自动识别前台视频，不要求逐页授权、选视频或手动绑定。

只允许恢复本工具自行暂停且仍持有恢复资格的视频，不能覆盖用户手动暂停。图像不上传、不默认留存。

当前推进 S25→Mac 最小体验阶段，变更 `mac-phone-experience` 保持 active/UNINTEGRATED。用户已在 S25 安装、配对，YouTube真实转头暂停/回看恢复和手动接管保护已通过；B站与QuickTime通用暂停/恢复及初始手动暂停保护有技术真链证据。按用户最新决定，回退是可选补偿：只对可核验的原生秒时间线执行，网页及能力不足的播放器保留暂停/恢复并跳过回退，不猜测Left含义、不逐站适配。更新后的S25→Mac→YouTube普通入口已复测：网页跳过回退仍暂停/恢复，初始手动暂停保持，停止/重连不补播；2026-10-18已补测B站与QuickTime的S25普通入口：均验证初始手动暂停保护及自动暂停/恢复，详见 docs/comet/reports/2026-10-18-s25-bili-local-pausepal.md。Mac登录启动登记/开关与模拟器短时后台已验证，长时间与耗电DEFERRED；本次S25锁屏后断开，持续锁屏/息屏观影未通过。本轮S25证据见 docs/comet/reports/2026-10-17-s25-web-retest.md；当前回退证据见 docs/comet/reports/2026-10-17-optional-rewind-progress.md；历史播放器结果见 docs/comet/reports/2026-10-17-media-background-followup.md，均不代表全播放器兼容或整阶段交付。

阅读入口：AGENTS.md → docs/product-spec.md → docs/decision-log.md → docs/development-plan.md → protocol/CONTRACT.md → docs/test-plan.md。原件及来源见 docs/source-index.md。

目录：apps/android（Kotlin/Compose/CameraX/MediaPipe）、apps/windows（C#/.NET WinForms/GSMTC）、apps/macos（Swift 菜单栏/Bonjour/播放器适配）；protocol/schemas、protocol/fixtures、tools/protocol-simulator、tests/integration 为后续预留。

目标手机（已安装联调，系统版本仍按用户提供）：三星 S25 / Android 16 / One UI 8.5；Android 主开发机：Mac。真实验证：Chrome 中的 B站、YouTube，以及本地 QuickTime。Mac 日常菜单仅状态、登录后启动开关（默认开启）与退出。原始资料和私人来源索引仅本地保留。

开发入口：Android 构建及当前限制见 apps/android/README.md；Mac 执行 `bash apps/macos/build-app.sh` 生成本地 `.local/LookPause.app`。普通菜单栏进程的系统权限必须独立核验，不能用 Codex/终端的已有权限替代。公开仓库不收录安装包、模型权重、本机日志、测试媒体或真实配对凭据。
