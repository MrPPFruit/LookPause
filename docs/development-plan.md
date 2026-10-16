# 开发路线与当前状态

当前唯一主目标：证明自动识别当前前台视频时，本地与网页均有可验证的安全控制路径。普通产品入口规划为：Mac 登录后菜单栏常驻 → 手机主动连接并开始观影 → 桌面自动裁决前台视频；当前尚不可达。

## 第一阶段：前台媒体可行性（ACTIVE）

变更：foreground-media-feasibility；任务唯一来源：openspec/changes/foreground-media-feasibility/tasks.md。Owner 为当前 Mac 开发会话。

| Acceptance ID | 父结果与规格 | 实现 owner | 必要证据及反例 |
|---|---|---|---|
| LP-MAC-01 | 自动识别唯一前台视频；product-spec.md 前台语义及 change delta | 前台解析/播放器适配 | 真实前台窗口/活动页/媒体身份；反例：后台视频播放就被当成前台 |
| LP-MAC-02 | 明确状态、暂停与播放；product-spec.md 核心规则及 change delta | Mac/网页适配器 | 实际进度与状态变化；反例：脚本成功但视频仍播放或进度重置 |
| LP-MAC-03 | 仅恢复本工具暂停且资格有效的同一视频 | 会话裁决 | 手动操作、换页、换片、关闭、结束后拒绝恢复；反例：布尔标志仍为 true 就误恢复 |

顺序：静态接口 → QuickTime 最小真实链 → Chrome B站/YouTube 最小真实链 → 必要安全边界。浏览器工具读到状态只计调研，不计 LookPause 真链。任何路径不可确认安全性则维持未通过/受限，不进入下游掩盖问题。

范围：apps/macos、必要浏览器适配代码、当前文档/验证记录。第一阶段不实现菜单栏产品、LAN、配对、Android 视觉或 Windows。首次新增敏感权限需实际授权；不得通过关闭浏览器安全保护获得测试通过。

## 后续路线（尚未进入）

1. 最小 Mac 接收端与菜单栏：前阶段合格后补齐协议命名、认证、生命周期、schemas/fixtures；日常仅状态、默认开启的登录启动开关、退出。手机负责连接/开始/停止。首次配对交互另行说明。
2. S25 联调：Mac 为 Android 主开发机，先假视觉意图，再接 CameraX/MediaPipe、本地校准与前台服务。Android 16 / One UI 8.5、后台/锁屏/息屏、发热耗电均用 S25 实测。
3. Windows：仅在 Windows 开发和实测，复用统一协议，不能从 Mac 结果推断 GSMTC 可用。

Git：公开材料基线 main，再创建 codex/foreground-media-feasibility；未验证实现保持 UNINTEGRATED。收尾记录目标分支、最终集成 commit、推送状态。公开仓库不收录私人原件、原始日志、测试媒体与密钥。
