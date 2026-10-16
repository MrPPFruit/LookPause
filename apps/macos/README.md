# Mac 媒体资格探针

状态：技术验证工具，不是菜单栏产品。没有 LAN、手机连接或自动恢复。

构建：在仓库根执行 `bash apps/macos/build-probe.sh`，不安装第三方依赖，产物在 `.local/LookPauseMediaProbe.app`。

探针只接受 `self-test`、`front`、`chrome-ax-state`、`quicktime-state`、`pause-test`、`play-test`。后两者仅用于用户授权的明确播放器测试，不代表自动恢复资格成立。运行时必须由 QuickTime 持有前台且已打开测试视频；否则拒绝操作。首次 Apple Events 系统权限由用户批准，代码不会更改系统权限。

输出只含窗口 ID、媒体身份摘要、播放状态与进度，不记录文件路径或页面内容。自动恢复始终关闭，手动干预可观察性仍待验证。

## 无扩展网页验证

`chrome-ax-state` 只读检查系统已授权时 Chrome 内 B站/YouTube 窗口的可访问控件；不发送按键、不修改权限、不保存页面正文。当前尚未取得浏览器原生媒体状态证据，不能称网页已支持。

曾准备的有限域名扩展与 Native Messaging 桥接因方案暂缓，仅本地保留在 .local/deferred-browser-extension，不进入当前公共实现。未开启 Chrome 开发者模式、未安装扩展、未注册宿主。
