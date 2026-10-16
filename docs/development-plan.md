# 开发路线与当前状态

当前唯一主目标：S25→Mac 最小体验闭环，见末尾“手机到Mac体验阶段”。完整精准判态不作为先体验的前提，但未知媒体不盲目切换。菜单栏、接收端、Android 普通入口及本地视觉均已有开发实现；S25→Mac→YouTube 暂停/恢复与手动接管保持暂停已有真实证据。完整验收仍有 GAP，不得视为整阶段已交付。

## 第一阶段：前台媒体可行性（SUPERSEDED，剩余 DEFERRED）

变更：foreground-media-feasibility；任务唯一来源：openspec/changes/foreground-media-feasibility/tasks.md。Owner 为当前 Mac 开发会话。

| Acceptance ID | 父结果与规格 | 实现 owner | 必要证据及反例 |
|---|---|---|---|
| LPMAC-01 | 自动识别唯一前台视频；product-spec.md 前台语义及 change delta | 前台解析/播放器适配 | 真实前台窗口/活动页/媒体身份；反例：后台视频播放就被当成前台 |
| LPMAC-02 | 明确状态、暂停与播放；product-spec.md 核心规则及 change delta | Mac/网页适配器 | 实际进度与状态变化；反例：脚本成功但视频仍播放或进度重置 |
| LPMAC-03 | 仅恢复本工具暂停且资格有效的同一视频 | 会话裁决 | 手动操作、换页、换片、关闭、结束后拒绝恢复；反例：布尔标志仍为 true 就误恢复 |

顺序：静态接口 → QuickTime 最小真实链 → Chrome B站/YouTube 最小真实链 → 必要安全边界。浏览器工具读到状态只计调研，不计 LookPause 真链。任何路径不可确认安全性则维持未通过/受限，不进入下游掩盖问题。

范围：apps/macos、必要浏览器适配代码、当前文档/验证记录。网页优先无扩展的辅助功能状态读取＋固定按键；扩展原型暂缓，未安装。第一阶段不实现菜单栏产品、LAN、配对、Android 视觉或 Windows。首次新增敏感权限需实际授权；不得通过关闭浏览器安全保护获得测试通过。

当前证据：QuickTime 明确播放/暂停真链已取得；YouTube 原生辅助功能可读明确状态，B站标签仅能判 UNKNOWN。三类初始手动暂停样例均未被测试探针恢复，但自动暂停后的归因与恢复链仍未闭合。Chrome 原生固定脚本候选暂缓；新通用 Pilot 已在 Chrome/YouTube 和 Safari 本地文件取得暂停/恢复真链及部分接管撤销证据，B站与 QuickTime 隐藏控件仍未知。详细证据见 docs/comet/reports/2026-10-16-foreground-media-progress.md。

## 旧阶段后续路线（历史规划，现行范围见手机到Mac体验阶段）

1. 最小 Mac 接收端与菜单栏：前阶段合格后补齐协议命名、认证、生命周期、schemas/fixtures；日常仅状态、默认开启的登录启动开关、退出。手机负责连接/开始/停止。首次配对交互另行说明。
2. S25 联调：Mac 为 Android 主开发机，先假视觉意图，再接 CameraX/MediaPipe、本地校准与前台服务。Android 16 / One UI 8.5、后台/锁屏/息屏、发热耗电均用 S25 实测。
3. Windows：仅在 Windows 开发和实测，复用统一协议，不能从 Mac 结果推断 GSMTC 可用。

Git：公开材料基线 main，再创建 codex/foreground-media-feasibility；未验证实现保持 UNINTEGRATED。收尾记录目标分支、最终集成 commit、推送状态。公开仓库不收录私人原件、原始日志、测试媒体与密钥。

验收 ID 别名：LPMAC-01/02/03 分别对应前期方案 LP-MAC-01/02/03，语义不变。使用单前缀形式以兼容现有 Comet 检查器。

## 旧阶段局部计划（历史资格路径）

唯一目标：通用信号 → 前台媒体暂停 → 回看恢复，手动接管优先。入口先为现有本机探针的固定 Pilot 命令，普通菜单栏/手机入口尚不可达。完成条件：同一份代码至少在网页和本地应用观察到实际暂停/恢复；初始手动暂停不播放、暂停后键鼠接管取消恢复。明确不做：逐站脚本、Chrome 新权限、扩展、录音、评分框架、LAN 与手机。全部挂到 LPMAC-01/02/03。

先读 Core Audio 的进程输出活动元数据（不采样或保存音频），通用辅助功能解析当前窗口已有媒体按钮，记录前台上下文及本轮操作；信号不足返回未知。恢复仅限固定本机测试路径，不解锁普通产品默认恢复，也不代表全应用安全保证。

## 手机到Mac体验阶段

用户已确认推进。当前唯一主目标：S25→Mac 的可体验闭环；入口为 Mac 正常启动的菜单栏及手机主界面，手机主动发现/连接/开始/停止。首次配对只在 Mac 点一次允许，后续记住设备，无扫码或输码；开始时短暂看向屏幕建立基准。完成条件为 LPEXP-01～05 的产品入口与 S25 真实输出。明确不做 Windows、广泛播放器适配、云端视觉或任意远程输入。

任务来源 openspec/changes/mac-phone-experience/tasks.md；Owner 当前开发会话。父结果→同名规格/design/tasks→apps/macos、apps/android、protocol→本轮报告，均使用 LPEXP-01～05；counterexample 见 proposal Delivery Contract。旧资格阶段只保留历史证据，不假称全通过；未知播放器仍不操作。

Git 唯一 integration branch 为 codex/experience-integration，从稳定 main 建立并导入 1455963 源码基线。阶段短分支 codex/mac-phone-experience 从 integration 创建；阶段结束先整合到 integration。main 只在 S25 整体验收成立后接收。当前未整合实现标 UNINTEGRATED。

2026-10-17 阶段进展：Android 模拟器真实模型/CameraX、TLS 与重连已验证；S25 普通入口首次配对及 YouTube 观看→离开→恢复已由用户和 Mac 裁决日志交叉确认。手动接管后的暂停保持通过，手机停止识别、Mac 停止裁决及随后断开已有证据。B站/QuickTime 通用识别、同页换片/结束边界、S25 后台/息屏、菜单登录启动/退出功能审查仍为 GAP；本变更保持 active，短分支尚未集成。详见 docs/comet/reports/2026-10-17-mac-phone-experience-progress.md。
