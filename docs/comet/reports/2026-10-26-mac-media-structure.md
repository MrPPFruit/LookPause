# Mac 局部媒体结构优化与交付边界

日期：2026-10-26。服务活动变更 `mac-phone-experience` 的 LPEXP-03，附属普通入口交付对应 LPEXP-01/05。父结果仍为 S25→Mac 的轻量观影体验；本轮没有修改 Android、Windows、网络协议、权限或用户操作步骤。

## 具体问题与实现

真实 Chrome 原生视频正常播放时，旧 build6 的通用读取返回控件数0、UNKNOWN。AX 采样显示：外层媒体组没有名称，播放控制在一个控制栏，seek直接属于外组，音量在控制栏的局部子组；总时间在 AXCustomContent，当前时间在 AXValueDescription。名称门槛和单字段时间解析因此漏掉正常媒体。

YouTube 导航后的 window.AXDocument 曾保留上一个本地页面地址，而唯一可见 AXWebArea 与当前焦点链已属于新页面。页面的完整时间由按钮 description/title 给出，seek 描述带章节名，不能把章节前缀当时间解析。

实现 owner 和边界：

- `MediaStructure.swift`：封闭局部结构，保留直接同组规则；额外允许上述实测控制栏／音量包装层。唯一播放、seek、音量及媒体动作缺一不可；Foreign、多控制、跨最近文档均拒绝。收起音量只作结构证据，不向它提交动作。
- `GenericPilot.swift`：保留明确媒体名称路径；顶层仅接受唯一可见文档，窗口地址滞后时须由实际焦点祖先链证明。嵌套文档按真实层级关联，时间只取同scope、同最近文档。目标身份增加scope、完整文档祖先及地址。存在WebArea时不选浏览器工具栏的媒体控件。
- `MediaTimeline.swift`：严格读完整时间、已播放时间／总时间的中文或英文描述，拒绝剩余时间、无单位、越界、杂项和冲突。不将任意AX数值范围解释为秒。
- `ResumeRewind.swift`：恢复确认后仍最多一次回退；Left使用实际可聚焦局部媒体表面，不用进度条代替。完整重新扫描后再做最终exact焦点、非编辑、文档、scope、前台和接管核验；暂停态还复核位置。焦点未就绪就跳过，不等待、不重试。

AXCustomContent仅按NSArray／AXCustomContent白名单安全解码，限制数据和内容数量，不解码任意NSObject，不记录原文。依据为[Apple AXCustomContent](https://developer.apple.com/documentation/accessibility/axcustomcontent)与[Chromium AX归档读取实现](https://chromium.googlesource.com/chromium/src/+/refs/heads/main/ui/accessibility/platform/inspect/ax_element_wrapper_mac.mm)。

MediaSession仍唯一拥有暂停／恢复副作用。UNKNOWN持有、初始暂停、输入接管、换目标、结束、停止及断线撤销策略未改变。LAN仍没有按键码、任意脚本、shell或文件接口。

## 验证事实与限制

环境：macOS26.6（25G72）、Apple Silicon、Xcode27.0（27A266a）、Swift6.4。Chrome为用户已有应用。资格接收端复用同一媒体核心，仅监听loopback，不发布Bonjour，不使用手机配对凭据；不能据此称最新S25普通入口整链通过。

用户要求停止代理回归之前取得的证据：

| 层级 | 结果 |
|---|---|
| 早期自动化检查 | `swift test --package-path apps/macos` 15项通过，含10个共用合成case及Mac结构／时间／身份检查；这是当时源码，后续活动文档选择等改动没有再跑测试 |
| 原生媒体真实链 | Chrome无名原生视频3轮暂停→UNKNOWN保持→恢复→一次Left，视频自身事件交叉核对；一轮额外焦点验证，恢复后Space及左右键仍控制媒体 |
| 原生回退步长 | 52.208秒样片每次Left约0.522秒。AX播放时间可能缓存，原有两秒结果阈值报告“回退结果未确认”；没有重发或补播放，不冒称固定5秒回退 |
| 嵌套文档 | 一条真实iframe原生视频完成暂停／UNKNOWN／恢复／Left／STOP；内部事件显示实际回退，停止后暂停 |
| YouTube | 最终网页适配读取代码下3轮暂停／UNKNOWN／恢复／Left；暂停进度与回退后分别为406→401秒等，后续实际进度继续；STOP后只读DOM为暂停、约403.9秒 |
| 初始手动暂停 | 原生样片在START／离开／回看后保持暂停 |
| STOP迟到意图 | 已确认暂停后STOP，迟到ATTENDING按契约拒绝，仍保持暂停 |
| B站／QuickTime及其他边界 | 本轮最终代码未完成重测。B站尝试时系统前台为Codex，返回UNKNOWN、未操作，不算失败播放器或通过；用户随后停止代理回归 |

早期严格AX进度脚本在原生视频上因缓存时间／小步长断言失败。之后局部诊断不再以缓存AX的播放进度增长作为唯一结论，改用样片只读事件核对实际播放／seek；没有放宽产品暂停锚点。YouTube最终记录同时有实际进度增长。以上均不继承为B站、QuickTime或S25通过。

2026-10-26用户明确要求“回归性的测试不用你来……只用查看代码有没有问题”。随后立即关闭本轮自建标签、停止loopback资格接收端与本地媒体服务，不再运行回归。新增活动文档用例未执行；最终源码只做必要Release编译及签名完整性检查。不会以早期15项结果代替最终版本回归。

## 代码审查

独立只读审查发现并修复两项P2：唯一seek不得掩盖同组完整时间的冲突；最终焦点证据不能早于一轮完整AX扫描。复审确认两处闭合，活动文档和按钮完整时间的补充读取未发现新增阻断。最终代码检查还收紧了浏览器外部控制、音量子组类型和隐藏／禁用音量的拒绝条件：收起尺寸已由AX适配器处理，不能再绕过其visible结果。补充用例但不执行；修改后只做Release编译，不追加测试。

仍有边界：辅助功能数据可能缺失、缓存或隐藏；相同控件／上下文／时长／位置的原地换片不可证明；跨进程观察到动作之间仍有竞态；Left语义与步长由播放器决定。焦点／时间证据不足时跳过相关能力。代码审查不能证明所有播放器或三星真实表现。

## 普通入口与整合

Mac build7沿用 `.local/LookPause.app`、com.lookpause.pilot、原签名与配对身份，使用Release产物；普通媒体入口默认使用本轮核心，无额外开关。构建脚本版本号同步到7。未新增UI，未改变自启默认值，不重做自启、图标或手机验收。

本轮从 c0305778235cd43e74c335c369720123a90e186b 的 `codex/experience-integration` 创建 `codex/mac-media-structure`；只选择性引入Windows的共用规则、接续说明和合成fixture，未合并Windows运行源码。Windows参考提交为1f5d597200a53025e5f2785cad9fe39da015d667，其结果不继承为Mac验收。

最终实现／集成commit与推送读回状态在下方收尾记录补充。main保持早期基线，父change active/pending，不归档、不宣布完整产品交付。

用户自行体验时重点反馈：平常所用网页／本地播放器是否能暂停恢复；手动暂停后是否保持；回退不能确认时是否正常继续播放。遇问题只需提供应用／网站、当时的操作和菜单状态，再做定向诊断，不要求现在跑测试矩阵。


### 整合与本机收尾记录

- 实现commit：`fc5713d8c387fc91b62a7645caef1bce9dbffeca`；no-ff代码集成点：`5a26cb59714845e16d54975a0cf79e5c91a9daa0`，目标`codex/experience-integration`。
- GitHub写前`gh api user --jq .login`核验为MrPPFruit，两分支push成功；`git ls-remote`已读回上述完整SHA。main仍`766a1968362bf582ce8fb3b75f8b2d959e1939cb`。后续状态文档提交不改变代码集成点。
- 最终源码执行`swift build --configuration release --package-path apps/macos`通过；由该Release产物原子替换本机build7并沿用既有签名，`codesign --verify --strict`通过，普通进程重新启动。菜单应用的CUA附着超时，但系统进程已核验存在；没有据此声称权限、菜单、网络或媒体功能重新实测通过。
- 最终公开清单为16个文本文件，包含Mac源码／现有测试／规格／共用规则和fixture；静态检查无本机运行目录、安装包、原始日志、私人链接、媒体或凭据，Markdown本地链接与差异空白检查通过。
- 本轮代码已整合并更新普通入口；最终功能验收由用户日常体验反馈。父阶段完整S25、Android静态检查、不可观察换片等GAP保持，锁屏／长时间继续DEFERRED，不合main、不归档。
