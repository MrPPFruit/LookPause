# LookPause

个人自用观影小工具。三星 Android 手机本地识别观看方向，主动连接家庭局域网内选定的一台 Windows 或 Mac。桌面自动识别前台视频，不要求逐页授权、选视频或手动绑定。

只允许恢复本工具自行暂停且仍持有恢复资格的视频，不能覆盖用户手动暂停。图像不上传、不默认留存。

当前推进 S25→Mac 最小体验阶段，变更 `mac-phone-experience` 保持 active/UNINTEGRATED。用户已在 S25 安装、配对，YouTube真实转头暂停/回看恢复和手动接管保护已通过。通用路径新增B站与QuickTime暂停/恢复、初始手动暂停保护，以及合格恢复后回退一次的真实播放器技术证据；更新后的S25整链尚未复测。Mac登录启动已修复并核验系统登记；模拟器短时后台新帧、TLS与生命周期通过。按用户决定，长时间、锁屏/息屏与耗电测试DEFERRED。详见 docs/comet/reports/2026-10-17-media-background-followup.md，不能据此宣称全播放器兼容或整阶段交付。

阅读入口：AGENTS.md → docs/product-spec.md → docs/decision-log.md → docs/development-plan.md → protocol/CONTRACT.md → docs/test-plan.md。原件及来源见 docs/source-index.md。

目录：apps/android（Kotlin/Compose/CameraX/MediaPipe）、apps/windows（C#/.NET WinForms/GSMTC）、apps/macos（Swift 菜单栏/Bonjour/播放器适配）；protocol/schemas、protocol/fixtures、tools/protocol-simulator、tests/integration 为后续预留。

目标手机（已安装联调，系统版本仍按用户提供）：三星 S25 / Android 16 / One UI 8.5；Android 主开发机：Mac。真实验证：Chrome 中的 B站、YouTube，以及本地 QuickTime。Mac 日常菜单仅状态、登录后启动开关（默认开启）与退出。原始资料和私人来源索引仅本地保留。

开发入口：Android 构建及当前限制见 apps/android/README.md；Mac 执行 `bash apps/macos/build-app.sh` 生成本地 `.local/LookPause.app`。普通菜单栏进程的系统权限必须独立核验，不能用 Codex/终端的已有权限替代。公开仓库不收录安装包、模型权重、本机日志、测试媒体或真实配对凭据。
