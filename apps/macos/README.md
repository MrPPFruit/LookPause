# Mac 媒体资格探针

状态：技术验证工具，不是菜单栏产品。没有 LAN、手机连接或自动恢复。

构建：在仓库根执行 `bash apps/macos/build-probe.sh`，不安装第三方依赖，产物在 `.local/LookPauseMediaProbe.app`。

探针接受 `self-test`、`front`、`chrome-ax-state`、`chrome-manual-hold-check`、`manual-hold-check`、`quicktime-state`、`pause-test`、`play-test`。后两者仅用于用户授权的明确播放器测试，不代表自动恢复资格成立。QuickTime 相关命令要求它持有系统前台且已打开测试视频；否则拒绝操作。首次 Apple Events 系统权限由用户批准，代码不会更改系统权限。

输出只含窗口 ID、媒体身份摘要、播放状态与进度，不记录文件路径或页面内容。自动恢复始终关闭，手动干预可观察性仍待验证。

## 无扩展网页验证

`chrome-ax-state` 只读检查系统已授权时 Chrome 内 B站/YouTube 窗口的可访问控件；只解析匹配该窗口活动地址的页面，按完整标签匹配播放按钮，避免将自动播放、播放列表等控件误当状态。YouTube 当前英文界面可区分 Play/Pause（包括焦点下的 keyboard shortcut 标签）；B站的“播放/暂停”仅能记为 UNKNOWN。标签规则仅为当前界面资格证据，不保证网站未来版本或全部语言兼容。

`chrome-manual-hold-check` 进一步要求 Chrome 持有系统前台，仅检查聚焦窗口；以新会话没有暂停所有权为前提验证拒绝恢复，不发送播放器指令。`manual-hold-check` 对前台 QuickTime 的手动暂停样例检查同一媒体在短观察期间保持暂停、进度不变。这两个命令只覆盖“初始已手动暂停”的拒绝路径，不能证明工具暂停后手动干预的归因或完整恢复链。

网页的原生明确暂停/播放与恢复资格尚未证明。Chrome 的“允许 Apple 事件中的 JavaScript”当前关闭；验证该无扩展候选需要单独权限确认，尚未开启。浏览器自动化工具只作独立测试观察，不能代替 LookPause 原生能力。

曾准备的有限域名扩展与 Native Messaging 桥接因方案暂缓，仅本地保留在 .local/deferred-browser-extension，不进入当前公共实现。未开启 Chrome 开发者模式、未安装扩展、未注册宿主。
