# Mac 媒体资格探针

状态：本机技术验证/Pilot 工具，不是菜单栏产品。没有 LAN 或手机连接；固定 Pilot 命令可在满足本轮条件时尝试一次恢复，普通产品入口尚不可达。

构建：在仓库根执行 `bash apps/macos/build-probe.sh`，不安装第三方依赖，产物在 `.local/LookPauseMediaProbe.app`。

探针接受 `self-test`、`front`、`chrome-ax-state`、`chrome-manual-hold-check`、`manual-hold-check`、`quicktime-state`、`pause-test`、`play-test`。后两者仅用于用户授权的明确播放器测试，不代表自动恢复资格成立。QuickTime 相关命令要求它持有系统前台且已打开测试视频；否则拒绝操作。首次 Apple Events 系统权限由用户批准，代码不会更改系统权限。

输出只含窗口 ID、媒体身份摘要、播放状态与进度，不记录文件路径或页面内容。自动恢复始终关闭，手动干预可观察性仍待验证。

## 无扩展网页验证

`chrome-ax-state` 只读检查系统已授权时 Chrome 内 B站/YouTube 窗口的可访问控件；只解析匹配该窗口活动地址的页面，按完整标签匹配播放按钮，避免将自动播放、播放列表等控件误当状态。YouTube 当前英文界面可区分 Play/Pause（包括焦点下的 keyboard shortcut 标签）；B站的“播放/暂停”仅能记为 UNKNOWN。标签规则仅为当前界面资格证据，不保证网站未来版本或全部语言兼容。

`chrome-manual-hold-check` 进一步要求 Chrome 持有系统前台，仅检查聚焦窗口；以新会话没有暂停所有权为前提验证拒绝恢复，不发送播放器指令。`manual-hold-check` 对前台 QuickTime 的手动暂停样例检查同一媒体在短观察期间保持暂停、进度不变。这两个命令只覆盖“初始已手动暂停”的拒绝路径，不能证明工具暂停后手动干预的归因或完整恢复链。

网页的原生明确暂停/播放与恢复资格尚未证明。Chrome 的“允许 Apple 事件中的 JavaScript”当前关闭；验证该无扩展候选需要单独权限确认，尚未开启。浏览器自动化工具只作独立测试观察，不能代替 LookPause 原生能力。

曾准备的有限域名扩展与 Native Messaging 桥接因方案暂缓，仅本地保留在 .local/deferred-browser-extension，不进入当前公共实现。未开启 Chrome 开发者模式、未安装扩展、未注册宿主。

## 轻量通用 Pilot

当前入口：构建后运行 `.local/LookPauseMediaProbe.app/Contents/MacOS/LookPauseMediaProbe generic-state` 或 `pilot-cycle`。后者等 3 秒供测试时切到样例窗口，模拟一次离开，观察到暂停后等待 20 秒再模拟回看；20 秒只是人工核验窗口，不是视觉阈值。未知、歧义或初始暂停不会操作。请仅在已授权的测试媒体上运行。

新路径不按应用 ID 或站点分支，不运行网页脚本。它只在前台聚焦窗口的媒体语义区域内识别现有 Play/Pause 控件，并以 AXPress 操作；使用文档地址摘要和窗口对象关联上下文，不将浏览器易变的音频/通知标题当成媒体身份。音频只读 Core Audio 进程输出活动元数据，不录音、不上传、不单独授予恢复资格。失败的音频切换候选已撤掉：QuickTime 真正暂停后，音频活动仍至少保持 4.2 秒。

本轮同一份实现已在 Chrome/YouTube 和 Safari/本地 Sintel 样例观察到暂停与恢复。YouTube 手动播放后再暂停导致恢复取消，初始暂停不发命令；Safari 暂停后通过固定 Shift 系统事件验证了 USER_INPUT 撤销，之后仍暂停。`input-watch-test` 只发送固定 Shift 按下/松开检查事件监听，不输入字符或操作播放器；有活动修饰键时拒绝测试。

已知限制：QuickTime 隐藏控制条时没有可识别 AX 控件，B站“播放/暂停”没有明确状态，两者在新路径返回 UNKNOWN、零命令；此前 QuickTime 专用探针结果仅作测试对照。应用内部换片、结束、多媒体和最终操作间隙的竞态尚未全部闭合，不能声称全部应用兼容或绝不会误恢复。恢复期间真实键盘、点击、滚轮或上下文/可观察状态变化取消资格；纯鼠标移动不取消。失败不会强行播放或重试。
