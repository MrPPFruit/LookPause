# 前台媒体第一阶段进展

状态：ACTIVE / UNINTEGRATED。尚未完成第一阶段，不进入菜单栏产品、LAN 或手机视觉。自动恢复始终关闭。原始证据见当前会话工具记录；截图、私人页面、文件路径与测试媒体只保留本地，不公开。

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

已尝试只读原生脚本，Chrome 明确拒绝（errorCode=12）。已向用户说明并等待是否开启“允许 Apple 事件中的 JavaScript”：该开关让已获 Chrome 自动化授权的应用可执行网页脚本，权限本身不限单页；LookPause 若采用将固定限制 B站/YouTube 视频操作。尚未收到确认，保持关闭。该候选不需要扩展或扩展开发者模式。

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
