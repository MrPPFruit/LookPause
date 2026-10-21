# PausePal

原名 LookPause；2026-10-18 用户确认仅更改显示名称。安装/配对身份、服务标识和工程路径保持，以兼容已有设备。

2026-10-21 桌面媒体优化：Windows 0.1.3 增加无名控件组识别并修正同顶层子窗口前台通知的误撤销，94项自动回归及本机合成UIA链通过；真实原生视频时间能力与用户网站完整链仍开放。[共用规则](docs/media-control-policy.md)、[合成用例](tests/media-policy/cases.json)、[Mac接续说明](docs/macos-media-optimization-handoff.md)已提供，Mac实施与实测须在Mac独立完成。

**Windows接续入口**：[当前状态及未收尾项](docs/current-status.md) → [Windows开发交接](docs/windows-handoff.md)。跨端完整基线在`codex/experience-integration`，`main`仍为早期基线；Windows实现位于`codex/windows-receiver`。现已有协议接收端、独立身份、原生发现、四态托盘、自启和恢复后一次回退的迁移测试版，54项自动化及本机TLS/DPAPI/原生发现通过；真实媒体与S25集中验收仍待用户。启动与步骤见[Windows验收说明](apps/windows/验收说明.md)，分层证据及边界见[迁移报告](docs/comet/reports/2026-10-19-windows-migration.md)。

当前Mac构建为build6，新增断链/空心眼睛/睁眼/闭眼的连接与方向图标，见[图标规范与Windows待接入边界](docs/desktop-status-icons.md)。保留build5的UNKNOWN暂停归属修复；手动操作、换片、停止/断线保护保持。三播放器各3轮真实技术链与保护补测通过，最新S25集中体验待用户。见 [修复报告](docs/comet/reports/2026-10-19-unknown-pause-recovery.md) 与 [集中验收清单](docs/comet/reports/2026-10-19-unknown-final-acceptance.md)。

个人自用观影小工具。三星 Android 手机本地识别观看方向，主动连接家庭局域网内选定的一台 Windows 或 Mac。桌面自动识别前台视频，不要求逐页授权、选视频或手动绑定。

只允许恢复本工具自行暂停且仍持有恢复资格的视频，不能覆盖用户手动暂停。图像不上传、不默认留存。

当前推进 S25→Mac 最小体验阶段，变更 `mac-phone-experience` 保持 active，已整合至 `codex/experience-integration`，尚未合main或完成整体验收。用户已在 S25 安装、配对，YouTube真实转头暂停/回看恢复和手动接管保护已通过；B站与QuickTime通用暂停/恢复及初始手动暂停保护有技术真链证据。按2026-10-19用户最新决定，回退只在自动恢复且确认播放后执行一次：原生秒时间线优先，无此能力则核验焦点/前台/无接管后尝试一次Left；不重试，不逐站适配，具体效果取决于播放器。此前网页跳过结果为历史证据。更新后的S25→Mac→YouTube普通入口已复测：网页跳过回退仍暂停/恢复，初始手动暂停保持，停止/重连不补播；2026-10-18已补测B站与QuickTime的S25普通入口：均验证初始手动暂停保护及自动暂停/恢复，详见 docs/comet/reports/2026-10-18-s25-bili-local-pausepal.md。Mac登录启动登记/开关与模拟器短时后台已验证，长时间与耗电DEFERRED；本次S25锁屏后断开，持续锁屏/息屏观影未通过。本轮S25证据见 docs/comet/reports/2026-10-17-s25-web-retest.md；当前回退证据见 docs/comet/reports/2026-10-17-optional-rewind-progress.md；历史播放器结果见 docs/comet/reports/2026-10-17-media-background-followup.md，均不代表全播放器兼容或整阶段交付。

阅读入口：AGENTS.md → docs/product-spec.md → docs/decision-log.md → docs/development-plan.md → protocol/CONTRACT.md → docs/test-plan.md。原件及来源见 docs/source-index.md。

目录：apps/android（Kotlin/Compose/CameraX/MediaPipe）、apps/windows（C#/.NET WinForms/GSMTC）、apps/macos（Swift 菜单栏/Bonjour/播放器适配）；protocol/schemas、protocol/fixtures、tools/protocol-simulator、tests/integration 为后续预留。

目标手机（已安装联调，系统版本仍按用户提供）：三星 S25 / Android 16 / One UI 8.5；Android 主开发机：Mac。真实验证：Chrome 中的 B站、YouTube，以及本地 QuickTime。Mac 日常菜单仅状态、登录后启动开关（默认开启）与退出。原始资料和私人来源索引仅本地保留。

开发入口：Android 构建及当前限制见 apps/android/README.md；Mac 执行 `bash apps/macos/build-app.sh` 生成本地 `.local/LookPause.app`。普通菜单栏进程的系统权限必须独立核验，不能用 Codex/终端的已有权限替代。公开仓库不收录安装包、模型权重、本机日志、测试媒体或真实配对凭据。

2026-10-18边界收尾：可观察播放控件/时长/暂停位置变化及结束已补回归和真实媒体验证，独立安全审查完成；Mac普通入口更新为build3。相同全部观察值的换片仍无法完整识别，最新S25集中验收待用户。代码集成点82a32d4已推送；详见 [整合记录](docs/comet/reports/2026-10-18-experience-integration.md) 与 [最终验收清单](docs/comet/reports/2026-10-18-final-acceptance.md)。

2026-10-19恢复后回退阶段的Mac为build4：用户批准“先恢复播放、再一次回退”，原生时间线优先，其余播放器焦点核验后一次Left。B站/YouTube/QuickTime本机技术链与手动暂停保护通过，最新S25集中验收待用户；详见 [本轮报告](docs/comet/reports/2026-10-19-resume-before-rewind.md) 与 [新版验收清单](docs/comet/reports/2026-10-19-final-acceptance.md)。
