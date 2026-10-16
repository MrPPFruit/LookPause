# 前台媒体第一阶段进展

状态：ACTIVE / UNINTEGRATED。尚未完成第一阶段，不进入菜单栏产品、LAN 或手机视觉。普通产品入口尚不可达；历史探针恢复关闭，新增固定 Pilot 命令可按本轮条件尝试一次恢复。原始证据见当前会话工具记录；截图、私人页面、文件路径与测试媒体只保留本地，不公开。

## 环境

macOS 26.6 (25G72)、Apple M4 Max / arm64 / 64 GiB；Xcode 27.0 (27A266a)、Swift 6.4、macOS SDK 27.0；QuickTime 10.5 (1252.3)、Chrome 155.0.8059.82。S25 / Android 16 / One UI 8.5 仅用户提供，未连接真机。

原生探针权限诊断为 accessibility=true、listen=true、post=true，只证明当前执行上下文可用，不能推断未来安装产品已经授权。Chrome 原生脚本诊断返回 errorCode=12，明确提示“允许 Apple 事件中的 JavaScript”关闭。未更改该开关、未安装扩展、未注册桥接宿主。

## 当前证据与父结果缺口

| Acceptance | 已取得事实 | 未闭合 GAP |
|---|---|---|
| LPMAC-01 | 非 QuickTime 系统前台时原生操作被拒绝；真实前台 QuickTime 可识别文档；Chrome 原生辅助功能读取活动页面按钮，手动暂停检查仅检查聚焦窗口 | Chrome 活动页/媒体唯一身份、切换/关闭/换片撤销真链待证；不能把后台窗口枚举当成控制目标 |
| LPMAC-02 | QuickTime 明确播放后 playing=true/rate=1，独立 UI 进度推进；设 rate=0 后停止且重复暂停不重置进度。YouTube 原生 AX 可区分明确 Play/Pause；B站 AX 状态 UNKNOWN | 网页尚无 LookPause 明确暂停/播放调用结果；按键切换不能替代明确操作 |
| LPMAC-03 | QuickTime、YouTube 初始手动暂停均被无所有权规则拒绝恢复；B站未知状态拒绝控制且独立观察保持暂停 | 工具暂停后再手动播放/暂停、焦点变化、换片、结束与权限失败的实际撤销链均待证；自动恢复不合格 |

反例：所有测试样例都保持暂停，仍可能只是因为没有任何自动恢复入口；这不能证明未来有效工具暂停可以恢复，也不能证明手动干预后一定撤销资格。没有据此勾选安全验收任务。

## 本轮真实测试

### 本地 QuickTime

公开样例为 W3C 媒体示例所用 Sintel trailer，约 4.37 MB / 52.209s，仅本地保存。前轮已证明 quicktime-state → play-test → pause-test 的真实状态及 UI 变化。

本轮通过真实播放器按钮模拟用户先播放再手动暂停；资格设置助手仅将测试应用置于系统前台，不进入产品代码。运行 manual-hold-check：同一窗口/媒体，前后 playing=false、rate=0、currentTime=13.220480334；结果 PASS、resumeDenied=NO_PAUSE_OWNERSHIP、playerCommands=0。范围仅为初始手动暂停。

### Chrome YouTube

网络恢复后播放公开视频 Sintel（视频 ID eRsGyueVLvQ，约 14:48）。原生 AX 在播放时读到 Pause (k)，聚焦按钮后变为 Pause keyboard shortcut k；暂停对应 Play 两种形式。修正为完整标签匹配，避免把 Autoplay、Save to playlist 或 LookPause 名称当成媒体状态。

真实按钮操作先播放再手动暂停；chrome-manual-hold-check 读到 Play keyboard shortcut k / PAUSED，结果 HELD_UNOWNED_PAUSE、NO_PAUSE_OWNERSHIP、playerCommands=0。独立浏览器观察仍为 Play。网页明确暂停/播放和自动恢复尚未实现；该结果只计初始暂停拒绝路径。

### Chrome B站

前轮真实空格两次使 paused=false → true → false，证明其是切换操作。当前公开视频 ID BV1E2TD6bEQK；内容仅作播放器样例，不作为任何事实来源。

本轮真实点击“播放/暂停”后，独立 DOM 观察 paused=false；再次点击模拟用户暂停后 paused=true、currentTime=22.707665。原生 AX 同一按钮只有“播放/暂停”，AXValue 为空，不能区分状态。chrome-manual-hold-check：state=UNKNOWN、HELD_UNQUALIFIED、NO_PAUSE_OWNERSHIP、playerCommands=0；之后独立 DOM 再读 paused=true、进度仍为 22.707665，截图本地保存。

这是受限拒绝证据，不能称 B站完整支持；独立浏览器工具能读 video.paused，不等于无扩展的原生 LookPause 已能读取。

## 无扩展候选与权限边界

当前采用方向为无扩展。辅助功能标签能够支持部分 YouTube 状态读取，但 B站缺少明确状态，单靠空格不能满足安全恢复。候选为 Chrome 原生 Apple Events 调用固定视频脚本，直接读取状态并执行明确 pause/play，仍需进一步验证所有权与手动干预。

已尝试只读原生脚本，Chrome 明确拒绝（errorCode=12）。曾向用户提出“允许 Apple 事件中的 JavaScript”候选：该开关让已获 Chrome 自动化授权的应用可执行网页脚本，权限本身不限单页。用户随后明确要求通用性与轻量，当前不再将该候选作为推进前提，保持暂缓及关闭；优先验证下文通用 Pilot。该候选不需要扩展或扩展开发者模式。

暂缓扩展、桥接及其模拟测试仅本地保留在 .local/deferred-browser-extension，不进入当前公共实现。历史模拟通过不算当前网页支持证据。

## 验证分层

- 静态：完整按钮标签及聚焦窗口范围人工检查，git diff --check；OpenSpec strict validate 结果以本轮最终工具记录为准。
- 构建：bash apps/macos/build-probe.sh 通过；没有安装第三方依赖。
- 自动化：self-test 通过，覆盖媒体身份、精确状态标签和无所有权/目标变化/未合格拒绝规则；是假设输入的规则检查。
- 真实链：QuickTime 明确状态/播放/暂停；本轮三类初始手动暂停的受限拒绝与独立观察，详见上文。
- 未完成：网页原生明确操作、工具暂停所有权与手动干预撤销链、菜单栏 UI、登录启动、LAN、手机、Windows。

## 整合状态

公开资料基线 main 为 766a196。开发分支 codex/foreground-media-feasibility；本轮网络恢复，已通过 gh api user 确认为 MrPPFruit，推送并用 ls-remote 核验 88c8d94562f49653ceebfad10786d652d19d79f1。该事实替代此前网络 EOF / 未推送记录。本轮后续代码与报告另提交到同一开发分支，最终推送 hash 以 Git 历史及远程核验为准。

尚无合入 main 的最终集成 commit，未称交付。完整 Acceptance 未成立，change 保持 build / pending，不归档或同步 canonical spec。

## 轻量通用 Pilot 本轮增量

用户确认通用性与轻量优先，不指定音频路线，测试应用不等于支持白名单。当前唯一目标仍是前台媒体的最小体验真链；未进入手机/LAN/菜单栏。LPMAC-01/02/03 下新增 GenericPilot.swift，沿用原生探针、现有 AX 读取及 Swift 标准框架；无第三方依赖、新权限、网页脚本或任意远程输入接口。

### 实际机制与范围

前台聚焦窗口 → 媒体语义区域中的唯一 Play/Pause 按钮 → AXPress → 观察状态变化 → 同一窗口/文档上下文内一次恢复。初始暂停/未知零命令；本轮真实键鼠或上下文/状态变化撤销，输入监听丢失或观察中断也取消。地址只输出摘要，易变音频/通知标题不参与有文档地址的身份判断。固定循环等 3 秒准备、20 秒供独立核验；不代表真实视觉时限。真实测试前的应用激活仅为本机样例设置助手，产品代码不抢焦点。

### 新取得的真链证据

| 场景 | 实际结果 | 结论边界 |
|---|---|---|
| Chrome/YouTube Sintel | 通用代码 PAUSE_OBSERVED → RESUME_OBSERVED，2 条操作；独立原生 UI 最终为 Pause | 同一通用路径实际控制，不是网页脚本；未覆盖全部语言/网站 |
| YouTube 工具暂停后手动播放再暂停 | 独立 UI Play → Pause → Play；Pilot RESUME_CANCELLED / CONTEXT_OR_STATE_CHANGED，只有 1 条操作 | 本例由状态变化撤销；自动化按键没有作为系统输入监听证据 |
| YouTube 初始手动暂停 | hint=PAUSED，NO_ACTION / INITIALLY_PAUSED，0 条操作；此时音频活动仍可为 true | 明确 AX 暂停优先，未被音频误覆盖 |
| Safari 播放同一份本地 Sintel 文件 | 同一代码 PAUSE_OBSERVED → RESUME_OBSERVED，2 条操作；没有 Safari 分支，音频为 null 仍可工作 | 独立应用/本地文件样例；两种浏览器样例不能推广所有本地播放器 |
| Safari 自动暂停后系统输入 | 固定 Shift 注入检查 PASS；运行中的 Pilot RESUME_CANCELLED / USER_INPUT，仅 1 条操作；之后仍 PAUSED | 验证系统事件链，不等于所有输入设备/软件来源均已覆盖 |
| B站通用路径 | 播放与手动暂停样例均 UNKNOWN，NO_ACTION / STATE_UNKNOWN，0 条操作；独立 DOM 手动暂停后 paused=true、time=151.98399 | 无误恢复，但没有取得通用自动控制支持 |
| QuickTime 隐藏控制条 | controls=0、UNKNOWN，NO_ACTION / STATE_UNKNOWN，0 条操作 | 专用探针此前可控制，不作为新通用路径通过 |

本地截图保存了 Safari 暂停阶段的“播放”按钮和约 8 秒进度；恢复后的画面另存。初次捕获时已越过较短测试等待窗口，因此将固定核验窗口延长到 20 秒再确认暂停画面；不更改视觉参数。实际暂停后的独立截图和输入撤销结果见本轮工具记录。

### 音频实验的具体否证

原生 QuickTime 对照明确暂停（playing=false、rate=0）后，Core Audio 输出活动在 0.11、0.46、0.84、1.21、1.58、1.96、2.33、2.71、3.08、3.45、3.83、4.2 秒均为 true。试验性固定空格确实使视频暂停到约 8.9137 秒，但音频未在观察窗口转为 inactive，不能据此确认暂停。没有用增大延迟补丁强行解锁恢复；已删除音频触发切换/恢复代码，仅保留输出流元数据观测。不采集声音波形，也未申请音频录制权限。

输入检查初期将所有非零事件标志都当作修饰键而拒绝一次；只读诊断为 flags=536870912（非修饰键标志）、shiftHeld=false，随后改为仅检查 Shift/Control/Option/Command，系统监听检查与 Pilot 真实撤销链均通过。这是资格检查前置条件修正，不是媒体状态兜底。

### 父结果反查与整合

LPMAC-02 已新增跨应用最小真链；LPMAC-03 新增初始暂停、状态接管及系统输入撤销证据，但同一页面内部换片/结束、全部输入来源和操作间隙竞态仍有 GAP。LPMAC-01 对多个媒体、后台抢占与精确媒体版本的整体裁决仍不完整。反例：两个样例按钮识别和暂停/恢复都通过，QuickTime 隐藏按钮、B站模糊按钮仍无通用控制；不能称广泛兼容已达成。

构建/self-test、固定系统输入监听检查已执行；OpenSpec strict validate 与差异检查以本轮最终工具记录为准。新阶段代码仍在 codex/foreground-media-feasibility，未合入 main，没有最终集成 commit；change 保持 ACTIVE / UNINTEGRATED，不能归档 complete。最终开发分支提交和推送 hash 以 Git/远程核验为准。
