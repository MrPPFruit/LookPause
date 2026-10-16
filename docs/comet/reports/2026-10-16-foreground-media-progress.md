# 前台媒体第一阶段进展

状态：ACTIVE / UNINTEGRATED。尚未完成第一阶段，不进入菜单栏产品、LAN 或手机视觉。自动恢复始终关闭。原始证据见当前会话工具记录；本报告不含私人页面、文件路径或账号。

## 环境

macOS 26.6 (25G72)、Apple M4 Max / arm64 / 64 GiB；Xcode 27.0 (27A266a)、Swift 6.4、macOS SDK 27.0；QuickTime 10.5 (1252.3)、Chrome 155.0.8059.82。S25 / Android 16 / One UI 8.5 仅用户提供，未连接真机。

## 真实结果与限制

| Acceptance | 已取得事实 | 未闭合 GAP |
|---|---|---|
| LPMAC-01 | 系统前台为 Codex 时，原生探针拒绝操作；用户将 QuickTime 置前后，读取同一测试窗口和媒体身份 | Chrome 扩展尚未加载；多窗口、活动页切换真链待测 |
| LPMAC-02 | QuickTime 读取初始 paused/rate=0；明确播放后 playing=true/rate=1，独立 UI 进度推进；明确设 rate=0 后停在约 6.7799s，重复暂停保持同进度 | B站/YouTube 尚无 LookPause 自身调用结果；不能推断网页兼容 |
| LPMAC-03 | 探针没有自动恢复入口；桥接模拟验证原本暂停、状态变化、目标变化拒绝测试播放 | QuickTime 的手动操作来源可观察性未证明；网页真实撤销链未验证，自动恢复不合格 |

公开样例为 W3C 媒体示例所用 Sintel trailer，约 4.37 MB / 52.209s，仅本地保存。第一次下载超时，续传完成，QuickTime 实际打开并播放。

B站首个视频初期读到 PLAYING，随后出现付费试看及错误 87008，不能用它声称完整可用；已换另一首页公开视频，浏览器工具读到 readyState=4、无媒体错误、实际进度推进。上述均为独立浏览器调研，不是 LookPause 真链。YouTube 搜索页初次加载为空，尚无播放器证据。

## 命令与分层结果

- 静态：Node 语法检查两个扩展脚本、Python 编译检查、OpenSpec strict validate、git diff --check 通过。
- 构建：bash apps/macos/build-probe.sh 与 swift build --package-path apps/macos 通过；没有安装第三方依赖。
- 自动化：探针 self-test 及 bridge_transport_test.py 通过。后者只模拟帧与拒绝规则。
- Mac 真实播放：quicktime-state、play-test、pause-test、再次状态读取与独立 QuickTime UI 核对已执行。
- 未执行：扩展安装/权限授权、Chrome 产品调用、菜单栏 UI、登录启动、LAN、手机、Windows。

## 整合状态

公开资料基线 main 为 766a196，已推送 GitHub。当前实现分支 codex/foreground-media-feasibility；尚无最终集成 commit，不称已交付。完整 Acceptance 未成立，change 保持 build / pending，不归档或同步 canonical spec。

## 无扩展方向的新增证据

用户质疑扩展必要性，尚未批准 Chrome 开发者模式/扩展安装；两者均未执行。当前改为优先验证辅助功能状态读取＋固定键盘动作，扩展原型 DEFERRED。

真实 B站公开视频：浏览器工具观测原本 paused=false；模拟页面空格后 paused=true，第二次空格后 paused=false，再暂停清理。该结果证明空格是切换操作，不证明 LookPause 已有真实状态来源或恢复资格。初次尝试将 video 元素作为可聚焦键盘目标超时，改为页面级键盘输入后成功，不能依赖 video 元素可聚焦这一前提。

原生 chrome-ax-state 探针能通过权限检查并读取 3 个 Chrome 窗口的可访问属性名，但未匹配到当前 B站/YouTube 窗口；尚未取得原生浏览器媒体状态。不能据此认定 Chrome 辅助功能普遍不支持，仍需核对真实前台活动页。

自动恢复保持关闭。菜单栏、登录启动、LAN 及手机尚未实现或验证；未用按键成功来解锁下游。

暂缓原型处理：扩展、桥接及其模拟测试仅本地保留在 .local/deferred-browser-extension；当前公共 Swift package 只含原生媒体探针，避免把未采用方案并入实现。此前桥接构建/模拟结果为历史调研，不是当前网页支持能力。
