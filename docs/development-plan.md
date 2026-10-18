# 开发路线与当前状态

当前唯一主目标：S25→Mac 最小体验闭环，见末尾“手机到Mac体验阶段”。完整精准判态不作为先体验的前提，但未知媒体不盲目切换。菜单栏、接收端、Android 普通入口及本地视觉均已有开发实现；S25→Mac→YouTube、B站、QuickTime的暂停/恢复与初始手动暂停保护已有真实证据。完整验收仍有 GAP，不得视为整阶段已交付。

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

Git 唯一 integration branch 为 codex/experience-integration，从稳定 main 建立并导入 1455963 源码基线。阶段短分支 codex/mac-phone-experience 从 integration 创建；阶段结束先整合到 integration。main 只在 S25 整体验收成立后接收。此前未整合实现标 UNINTEGRATED；当前状态见末尾最新整合记录。

2026-10-17 阶段进展：Android 模拟器真实模型/CameraX、TLS 与重连已验证；S25 普通入口首次配对及 YouTube 观看→离开→恢复已由用户和 Mac 裁决日志交叉确认。手动接管后的暂停保持通过，手机停止识别、Mac 停止裁决及随后断开已有证据。B站/QuickTime 通用识别、同页换片/结束边界、S25 后台/息屏、菜单登录启动/退出功能审查仍为 GAP；本变更保持 active，短分支尚未集成。详见 docs/comet/reports/2026-10-17-mac-phone-experience-progress.md。

2026-10-17 后续功能验证：同一通用核心已在B站与QuickTime取得真实媒体暂停/恢复及初始手动暂停保护；合格恢复后的回退已实现并以实际进度验证。默认登录登记根因已修复，普通进程从notFound登记到enabled，退出动作已观察。模拟器后台1分钟的新帧/TLS及相机生命周期通过。用户明确将长时间后台、锁屏/息屏和耗电DEFERRED，本轮不再等待30分钟。LPEXP-01菜单开关/最终截图、LPEXP-03完整同页换片边界、LPEXP-05更新后的S25整链及2.3独立审查/集成仍按实际证据保留GAP，不用技术子项替代父结果。Owner/代码与证据映射见 docs/comet/reports/2026-10-17-media-background-followup.md。

2026-10-17 最新局部目标仍挂LPEXP-03：回退可选、能力不足跳过且不影响暂停/恢复；当前仅保留可核验原生秒时间线写入，取消猜测Left含义。唯一实施计划为当前change的tasks4.1～4.2，owner为MediaSession/ResumeRewind；不改Android、协议、菜单控件或增加网站适配表。原3.4网页回退是历史原型证据，已被最新决定覆盖。父结果中同页换片、完整S25验收与独立审查/整合的GAP仍保持，不扩张本轮范围。

2026-10-17 S25普通入口复测完成局部tasks5.1（LPEXP-03/05）：YouTube在跳过网页回退后，真实手机离开/回看仍暂停/恢复，初始手动暂停不播放，STOP与重连不继承旧观影。Owner为当前会话，代码owner沿用MediaSession/ResumeRewind/Receiver及既有Android客户端；证据见 docs/comet/reports/2026-10-17-s25-web-retest.md。用户确认一次锁屏导致断开，锁屏持续观影未通过；手机系统版本、B站/本地新版S25整链、同页换片/结束、独立审查/最终集成仍有GAP。长时间后台/耗电继续DEFERRED，不扩大当前阶段。代码基线7b7185e，本轮仅补充证据，开发分支仍codex/mac-phone-experience，未合入integration/main，保持active/UNINTEGRATED。

2026-10-18 当前局部任务为tasks6.1/6.2，仍服务LPEXP-03/05与入口呈现LPEXP-01：真实S25经普通接收端已分别验证B站和QuickTime的初始手动暂停保护及自动暂停/恢复，用户反馈与日志、媒体状态交叉确认。用户确认新显示名PausePal，双端仅更改名称，保留安装/配对/服务身份；双端构建、Mac菜单截图、Android模拟器完整截图通过；S25覆盖更新/新标题/已有配对直连由用户反馈确认，普通进程认证与最终STOP交叉核验。1.3/3.1以前次开关/退出证据加本轮菜单截图闭合，但未做电脑重新登录实测。Owner为当前会话，映射MediaSession/ResumeRewind/Receiver、Android MainActivity/WatchingService及MenuApp/build-app，证据见 docs/comet/reports/2026-10-18-s25-bili-local-pausepal.md。锁屏/长时间后台继续DEFERRED，父阶段同页换片/结束、独立审查/最终集成等GAP保留；本分支未整合，不据此交付整个阶段。

2026-10-18 边界与安全审查收尾（LPEXP-03，关联02/04/05）：tasks7.1/7.2通过，控制身份、时长/暂停锚点及结束优先级已有根因修复、6项回归和本机固定意图→真实媒体输出；独立审查未发现新增整合阻断，普通Mac build3启动/权限/登录登记正常。Owner为GenericPilot/MediaTimeline/MediaSession；证据见 docs/comet/reports/2026-10-18-media-boundary-security.md。同页复用全部可观察值换片的P2限制与最终新版S25验收仍GAP，锁屏/长时间DEFERRED；不得据此关闭父结果或合main。用户不参与中途测试，最终一次清单见2026-10-18-final-acceptance.md。7.3实际整合与远端核验另记最终commit。

2026-10-18 最终代码整合：codex/mac-phone-experience@8dec106已合入codex/experience-integration，集成commit为82a32d41852d59f0a7a28093558952c205988d9c，功能/集成分支均已推送并核验。tasks7.3完成；本阶段不再标UNINTEGRATED，但仍active/pending（P2不可观察换片与最终新版S25验收保留），main保持766a196。后续文档提交不改变代码合并点；细分父结果与远端状态见2026-10-18-experience-integration.md。

2026-10-19 当前局部目标为LPEXP-03/tasks8.1～8.3：用户批准恢复后的一次通用回退，原生优先、无能力则焦点核验后一次Left，统一先确认播放再回退。Owner为ResumeRewind/MediaSession/GenericPilot；三播放器本机技术链、初始手动暂停、重复回看与7项回归及独立diff审查通过，普通Mac build4已更新并保留原权限。真实证据见2026-10-19-resume-before-rewind.md，最终S25一次清单见2026-10-19-final-acceptance.md。短分支从最新integration创建为codex/resume-before-rewind，父结果GAP不据局部通过关闭，不合main，不新增Android/协议/权限或长测。

2026-10-19 本轮最终代码整合为5230141f6a2712bf3b8d0e50a6d9e2602f8719fd（短分支实现edfd97b→codex/experience-integration）；两分支已推送并核验。tasks8.1～8.3完成，普通Mac build4运行中；后续状态文档提交不改变该代码集成点。main保持766a196；父阶段GAP保持active，不归档。
