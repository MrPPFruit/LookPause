# 连接与观看方向图标：Mac本机验证与Windows边界

日期2026-10-19；用户已明确确认断链/空心/睁眼/闭眼方案。本轮普通入口为Mac菜单栏，手机无需更新，不增加按钮、权限、协议字段或媒体执行动作。父阶段mac-phone-experience仍active/pending，不合main或归档。

## 用户结果与映射

- [PASS] LPEXP-01 — Evidence: 本轮有界图标子项由现有认证会话与方向驱动；tasks10→delta spec/设计→Receiver.desktopStatus/StatusIcon/MenuApp→本机TLS、绘图、普通build6与截图。反例“只有样图但没有连接生命周期”由实际接收链覆盖；不据此关闭整个父阶段。
- [GAP] LPEXP-05 — Evidence: 本轮未新增实体S25转头或普通菜单四态截图；既有S25媒体结果沿用旧报告，更新后的集中图标体验待用户，不用本机意图冒充头部动作。
- [GAP] LPWIN-ICON-01 — Evidence: Windows目录仅.gitkeep，当前执行环境为Mac；统一状态规范及两套多尺寸ICO已准备，Windows接收端/NotifyIcon普通入口、主题/缩放与真实设备结果尚未实现/验证。反例“ICO文件存在但托盘不随手机变化”尚未排除，不能称Windows已支持。

LPEXP-02/03/04没有新增产品行为；父结果的不可观察换片、跨进程竞态与整体验收GAP沿用UNKNOWN修复报告，锁屏/长时间继续DEFERRED。

## 实现与影响

从active认证连接、isWatching、只读lastIntent计算四态，不解析状态文案。连接优先、停止仍为绿色空心，UNKNOWN为空心；ATTENDING睁眼，AWAY/NO_FACE闭眼；待配对与断线是单色断链。已接受意图后显式通知菜单，即使该意图没有改变媒体文案，也刷新图标。只在图标状态变化时创建NSImage，采用20pt原生矢量绘图与深浅两种绿色；复用同一图像切换主题的配色已验证。辅助功能名称与tooltip包含方向语义，现有菜单行/登录开关/退出保持。

媒体核心唯一源码变化是lastIntent增加模块内只读访问，setter仍私有；未改变receive/tick/恢复资格/按键调用。Receiver增加计算属性与UI回调，网络包不变；loopback资格入口仅多输出固定icon枚举，无新网络接口。固定矢量绘图无依赖、图像/人脸记录或动画。

## 命令、实际结果与范围

环境：macOS26.6(25G72)、arm64，现有Xcode/Swift工具链；不安装依赖。

1. `PAUSEPAL_ICON_PREVIEW=... PAUSEPAL_ICON_ASSETS=... swift test --package-path apps/macos`：10项、0失败，含原有7项安全回归和3项状态/生命周期/绘图回归。绘图同时使用深浅色、同一图像复用和实际20pt/4倍预览；7种像素尺寸导出56张PNG，供8个ICO打包。测试工具的闭包错误已修正后重新运行，上述为最终结果。
2. `bash apps/macos/build-app.sh`：build6构建、签名验证及self-test通过。安装/签名身份不变，无需重配对或重新授权；手机安装包不变。
3. `.local/test_status_icons_wire.py`（临时本机诊断，执行后清理）：真实loopback TLS→既有Receiver；固定证书指纹校验，凭据仅内存使用。实际状态序列为disconnected→认证waiting→START waiting→ATTENDING attending→AWAY away→NO_FACE away→UNKNOWN waiting→ATTENDING attending→STOP waiting。STOP后的迟到ATTENDING被拒；重新START waiting，回看attending；断开disconnected；同凭据重连waiting且不继承方向；随后START/ATTENDING后无有效帧超时变disconnected。PASS日志保留.local/status-icons-wire.log；测试时前台Codex无可用媒体，未操作用户视频。不能据此宣称本轮实测了播放器或S25。
4. 普通build6经Finder正常打开，进程56273，无命令行资格参数；01:23:23日志核验AX=true、输入监控=true、登录登记status=1、接收端就绪。完整桌面截图中顶部断链图标可见，无新弹窗/布局遮挡。普通进程不依赖测试入口。
5. 预览应用完整窗口截图已审查：上排浅、下排深，四态均有20pt实际尺寸和4倍展示，填充、轮廓、短睫毛清晰，无裁切/文字重叠。公开素材preview.png只含自产设计，不含实际桌面/私人内容。
6. `python3 tools/export-desktop-icons.py .local/status-icon-assets docs/assets/desktop-status`及`file .../*.ico`：8个合法ICO，各含16/20/24/32/40/48/64像素PNG。此为文件结构验证，未在Windows实际加载。
7. `openspec validate mac-phone-experience --strict`及`git diff --check`通过；公开路径与文本敏感信息检查在提交前执行。原始日志、实际桌面截图、签名细节与临时诊断不提交。

本地截图：01:22:25为预览完整窗口，01:23:30为普通入口桌面/菜单栏；仅保留临时目录。自动化无法附着无窗口菜单应用，未要求用户展开菜单，未把预览截图当作普通菜单四态截图。菜单结构未改；相关文字与刷新路径已核对。

## 收尾与集中验收

用户方便时一次验证：S25连接后空心绿眼；开始且看屏幕后绿色睁眼；转头后绿色闭眼；停止但不退出连接回到空心绿眼；断开变单色断链。视频手动暂停时即使仍看屏幕也应是睁眼，图标不应驱动播放。识别未知回到空心，原暂停归属保护仍依既有规则执行。

本轮从604f25d上的codex/experience-integration创建codex/desktop-status-icons；实现commit为c0f36f7b35fe477c5c631c8d4991a88e89bd4ded，no-ff代码集成点为51d639529f7ecf372e99ef523fd23c08ed04e85e。已在GitHub账号MrPPFruit下推送两分支并读回核验；main仍766a1968362bf582ce8fb3b75f8b2d959e1939cb。后续状态记录提交不改变代码合并点。Mac图标已在普通入口可达，本机技术验证通过，S25集中体验待用户；Windows仅设计/素材可用。两端完整功能不能合称交付。
