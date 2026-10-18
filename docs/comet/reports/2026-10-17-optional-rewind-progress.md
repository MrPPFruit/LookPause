# 2026-10-17 可确认的可选回退

当前结论：按用户最新决定，回退只保留可核验的原生秒时间线；没有该能力就跳过，不猜测Left含义，不影响已成功的暂停/恢复。实现已接入普通Mac默认入口并重新启动；本机资格入口→真实QuickTime/YouTube补测通过。S25新版本整链与父阶段既有GAP未清零，保持active/UNINTEGRATED，不归档或同步canonical spec。

## 目标、根因与改动

唯一局部目标LPEXP-03，来自 docs/development-plan.md 的手机到Mac体验阶段；规格在当前change的“回退能力不足”场景，设计与tasks4.1～4.2对应本报告。Owner为ResumeRewind.swift、MediaSession.swift；相邻调用方GenericPilot.swift及既有main.swift自检一并更新。反例：成功发送Left却执行逐帧，或回退能力缺失导致恢复失败；本轮不以按键提交或AX成功返回充当用户结果。

此前回退在没有可写原生时间线时发送Left，代码只能证明媒体焦点和暂停所有权，不能证明其他播放器的Left语义。用户已明确“能安全回退就回退，不能确认就跳过”，因此移除这条猜测路径，而非增建网站/应用名单或继续观察试错。

当前回退只有一条执行路径：同一前台、已恢复播放、未接管，非网页原生秒时间线，并且数值/上下界有限且有效、AXValue可写，才写入max(下界, 当前秒数−5)。起点、越界、无效范围和未知能力返回无操作；不发送Left或AXDecrement，不补发其他键、不再切换播放。实际写入前的秒数返回给MediaSession作确认基准，避免使用可能滞后的进度摘要。执行失败或能力不足只清理已经完成恢复的临时资格；提交成功后观察进度，一次之后不重试。

现有固定Space暂停/恢复保持原有条件；删除仅服务Left的枚举/参数/方向键标记，输入监听豁免缩小为本进程、本轮随机标记的固定Space。没有改Android、协议、菜单控件、配对或权限，没有新依赖、云服务、按域名/应用名分支。

## 环境与验证

Mac macOS26.6/25G72，Xcode27.0/27A266a，Chrome155.0.8059.82，现有QuickTime本地测试样片。沿用同一源码的loopback-only资格接收端、真实TLS与固定观看意图。独立DOM仅用于只读验证，不进入产品；未启用网页脚本权限或安装扩展。原始日志/媒体/截图留本机忽略目录或临时目录，不公开。

| 场景 | 实际结果 | 证据边界 |
|---|---|---|
| QuickTime初始手动暂停 | START→AWAY→ATTENDING后仍PAUSED，19.992505秒不变 | 无本轮所有权，不自动播放/回退 |
| QuickTime自动暂停→恢复→可选回退 | PAUSED28.903451秒→PLAYING24.333735秒；状态确认“已恢复播放并回退一次” | 新的范围/写入条件与真实输出通过；不是只看API返回 |
| 重复ATTENDING | 本地继续播放至25.590407秒，状态“看向屏幕”，无再次回退 | 同一轮最多一次 |
| YouTube初始手动暂停 | 离开/回看保持PAUSED，独立DOM402.147970秒不变 | 不自动播放/回退 |
| YouTube缺少可核验回退能力 | 自动暂停后独立DOM412.133037秒；ATTENDING正常恢复，显示“已恢复播放，未能安全回退”；约9.399秒后仍播放421.159419秒，净前进约9.026秒 | 网页即使暴露数值时间线也不写入，不后退；跳过不阻断恢复 |
| 跳过后的下一轮AWAY及STOP | 再次自动暂停成功；STOP后的ATTENDING被拒绝NOT_WATCHING_OR_INVALID_INTENT，视频PAUSED约462秒 | 没有因跳过回退卡住会话或错误补播 |
| 输入监听 | 固定Shift诊断PASS，USER_INPUT，播放器命令0 | 系统事件链；不替代S25实际键鼠/视觉复测 |
| 普通Mac入口 | 新进程启动，AX=true、输入监听=true、登录状态enabled，接收端就绪 | 默认代码路径已部署；本轮未重新用S25发送视觉意图 |

命令与结果：

- `bash /Users/ppg/.codex/skills/comet/scripts/comet-state.sh check mac-phone-experience build`：PASS；`openspec instructions apply --change mac-phone-experience --json`读取现有active任务。
- `bash apps/macos/build-probe.sh`、`bash apps/macos/build-app.sh`：PASS，内置self-test PASS；新增8条回退数值边界断言覆盖下界、起点、越界、无效范围、NaN/Infinity。
- `swift build --package-path apps/macos -c release`：PASS。
- `codesign --verify --strict .local/LookPause.app`：PASS。
- `.local/LookPauseMediaProbe.app/Contents/MacOS/LookPauseMediaProbe input-watch-test`：PASS，零媒体命令。
- 资格入口 `receiver-test`，经固定本机TLS客户端发送START/AWAY/ATTENDING/STOP；`generic-state`与独立网页DOM交叉观察结果，见上表。测试端不发布Bonjour，不允许外部设备绕过首配。
- `git diff --check`：PASS。公开文件清单与凭据/原件扫描在提交前执行，推送前核验MrPPFruit。

测试开始时系统仍为Codex前台，第一次返回UNKNOWN/零操作，未计通过；随后通过访达现有应用入口激活QuickTime，通过浏览器窗口/标签菜单选中自建YouTube样片，现场确认前台身份后才计真实播放器结果。未绕过前台限制，也未要求用户继续操作手机。

## 范围与整合

本轮只补测QuickTime和YouTube；B站既有暂停/恢复证据不冒充本轮复测，Space调用的固定键码、权限/焦点/接管条件在代码对照中保持。未新增广泛兼容测试、独立安全审查或新权限。原生应用仍可能不提供可核验时间线，按相同规则跳过；未承诺全应用兼容。长时间后台/锁屏/息屏/耗电继续DEFERRED；S25更新后的整链未测。

目标短分支codex/mac-phone-experience，目标integration分支codex/experience-integration；本轮增量保存在短分支并推送，最终提交和远端一致性在最终回复记录。正式集成commit仍无，尚未合main；父阶段1.3/2.2/2.3等GAP保留，不用局部PASS解锁整阶段交付。

- [GAP] LPEXP-03 — Evidence: tasks4.1～4.2的真实原生回退与网页跳过后暂停/恢复技术链通过；完整媒体身份/全部手动干预来源仍有父结果GAP，不能宣称整个LPEXP-03已交付。
