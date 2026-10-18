# 2026-10-19 UNKNOWN期间保留自动暂停归属

## 当前结论与映射

唯一局部目标LPEXP-03/tasks9：修复用户真实S25→Mac→YouTube连续观影中“已自动暂停，回看仍不恢复”。映射：父路线图手机到Mac体验 → 当前delta spec/design → tasks9.1～9.3 → MediaSession与既有Receiver/相机STOP链 → 本报告。反例为构建或单轮恢复通过，但UNKNOWN删掉资格后第二轮卡住；另一个反例是简单保留布尔标志导致接管/换片/断线后也补播。

已完成根因、旧版失败回归、最小修复及三播放器各3轮实际进度验证；额外保护补测和普通新版启动在Mac锁屏后待继续，不将部分通过写成完整交付。用户已授权本修复与自动测试，不参与中途测试；仅因系统锁屏请求一次解锁，不要求手机/播放器操作。Android、身份/凭据、线协议字段及权限均无变化，父阶段保持active/pending。

## 故障事实与根因

普通build4在00:38:07.262确认自动暂停；00:38:08.919收到UNKNOWN，08.920日志“识别状态未知，恢复资格已取消”；00:38:18.455明确ATTENDING到达却只进入“看向屏幕”。另一轮00:38:59.228暂停，00:39:00.198/00.508 UNKNOWN后，00:39:01.187 ATTENDING仍不恢复。开始两轮曾恢复/回退成功，说明不是始终缺少权限或回看意图没有抵达。

根因是MediaSession.receive(.unknown)无条件调用cancel→clearOwnership，将方向暂不确定等同于暂停归属失效；后续ATTENDING只有phase=.paused才调用resume。已暂停视频因失去记录被当作无归属暂停，持续回看不会修复它。未定位或修改手机产生UNKNOWN的具体视觉原因，不靠增加识别阈值或宽限TTL掩盖该状态语义错误。

项目内持久回归apps/macos/Tests/test_unknown_recovery.py用原有loopback TLS资格入口复现旧二进制：START→AWAY→确认暂停→UNKNOWN即断言失败、exit1，明确“UNKNOWN删除自动暂停归属”。它只发固定协议意图，用generic-state只读核对真实前台播放器，不添加产品控制或诊断网络接口。

## 最小修复及边界

只改MediaSession的UNKNOWN分支与暂停确认后的状态文案：.pausing继续原2秒暂停结果确认，.paused保持资格并显示“识别暂时未知，视频保持暂停”；输入监听、前台/上下文、控件与暂停锚点监测继续运行。只有随后明确ATTENDING才走原resume二次校验与一次回退。UNKNOWN不延长确认期限、不创建/复活资格、不播放或回退；checking/无资格/resuming/rewinding阶段沿用取消，避免不明确方向下追加副作用。

STOP、断线、超时、输入接管、媒体/进度变化、结束及观察中断仍由原链清资格。Android相机故障原来就先UNKNOWN再STOP，3秒帧中断看门狗也会停止，代码未改；本轮不以静态核查声称真机相机故障注入已通过。

## 连续真实技术链

环境：本机Apple Silicon、macOS26.6（25G72）、Chrome155.0.8059.82、QuickTime10.5。正常接收端当时已停止观影/随后断开，无并发手机控制；独立loopback资格服务运行同一MediaSession，通过固定意图控制真实前台播放器。本轮不是S25真实头部输入，最终真机体验仍待用户。

正式回归每播放器3轮：AWAY确认暂停→两次UNKNOWN，保持暂停/位置不变→再次AWAY不重复暂停→ATTENDING恢复/一次回退→实际进度继续→重复ATTENDING不再回退。第2轮在“正在确认暂停”后立即UNKNOWN；三播放器对应固定诊断日志证明命中.pausing，不只凭脚本发送时序猜测。每组最后再暂停→UNKNOWN→STOP→迟到ATTENDING，按实际kind=error/对应seq/NOT_WATCHING_OR_INVALID_INTENT断言拒绝，暂停位置保持。

| 播放器 | 轮次 | 暂停秒数 | 回退后首次读值 | 随后播放读值 | 结果 |
| --- | --- | --- | --- | --- | --- |
| YouTube | 1 | 42 | 37 | 38 | PASS |
| YouTube | 2 | 39 | 34 | 35 | PASS |
| YouTube | 3 | 36 | 31 | 32 | PASS |
| B站 | 1 | 27 | 24 | 25 | PASS |
| B站 | 2 | 25 | 22 | 23 | PASS |
| B站 | 3 | 23 | 20 | 21 | PASS |
| QuickTime | 1 | 28.103751 | 23.191306 | 24.219492 | PASS |
| QuickTime | 2 | 24.333735 | 19.307048 | 20.220991 | PASS |
| QuickTime | 3 | 20.335234 | 15.422790 | 16.450976 | PASS |

表中是采样值，不将读数差当精确seek秒差；网页本轮Left各一次，原生仍优先时间线。回归日志只记固定裁决与进度，不保存媒体地址、手机身份或图像。旧版失败日志、三组输出和系统裁决仅留本机忽略目录。

## 检查与独立审查

- `swift test --package-path apps/macos`：7项、0失败，既有换控件/时长/暂停位置/结束/固定键输入边界回归保持。
- `swift build -c release --package-path apps/macos`和发布可执行self-test：PASS；`bash apps/macos/build-probe.sh`：PASS。
- 三次项目内真实回归脚本均exit0，共9轮并逐轮断言实际后退与继续播放；旧二进制同序列exit1。
- `bash apps/macos/build-app.sh`：build5生成/self-test PASS，签名指定要求与旧版一致；普通新版启动/权限状态待解锁核验，不以build5存在称默认入口已更新运行。
- OpenSpec严格校验、diff检查通过；后续最终公开清单/整合/推送另记实际结果。

Comet要求的独立只读审查确认核心没有新增安全阻断；发现回归脚本原先错误等待ack/ok=false，而契约实际为error固定拒绝，已在脚本按kind/seq/error修正并由三播放器真链闭合。另补齐.pausing分支发送与实际命中日志。复核确认这两个验证缺口已闭合；审查者没有独立跑真播放器或操作UI，不替代主执行者证据。未知期间手动操作/换片/断线实测仍需补，不用静态审查代替。

## 待继续的交付项

当前Mac已锁屏，generic-state观察到com.apple.loginwindow且无媒体；在此状态下没有继续对后台视频发命令。仍需解锁后：初始手动暂停、未知期间手动接管/换片/结束及持有资格时断线；普通build5启动与原权限核验；最终集中清单和开发分支整合/推送。已有全部观察值复用换片与跨进程竞态P2限制不因本修复消失，main不合并、change不归档。
