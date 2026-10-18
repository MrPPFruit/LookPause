# 2026-10-19 先恢复播放，再回退一次

## 目标、实现与结论

唯一局部目标为LPEXP-03：完成用户批准的恢复后一次通用回退，重点保持暂停、恢复与手动暂停保护。映射为父路线图手机到Mac体验 → 当前change的LPEXP-03场景/design/tasks8.1～8.3 → ResumeRewind/MediaSession/GenericPilot → 本报告。Owner为当前Mac开发会话；普通用户入口沿用手机观影与菜单栏。反例是B站仍暂停时先Left造成播放，或重复回看多次回退；测试构建通过不能代替实际播放器输出。

实现与三播放器本机技术链通过；Mac普通入口已更新为0.1.0/build4并重新运行。用户不参与中途测试，本轮不冒称最新S25整链通过。最终集中清单见2026-10-19-final-acceptance.md。Android、配对/凭据、协议及权限均未改动。

MediaSession只在.resuming实际读到同媒体playing后调用回退，回退自身再次解析前台媒体与playing。可写原生秒时间线优先；没有此能力时复用已有Space发送路径的输入框/焦点/修饰键/前台/接管检查，只发一组Left按下/松开，不长按、不重试。原生写入已尝试即不补Left，避免API报错但实际生效时双退。已提交回退进入仅观察阶段，重复回看不再执行，超时只清资格，不补发播放/暂停。固定键豁免同时要求本进程pid、当前轮次随机标记、Space/Left和keyDown，真实输入继续撤销；没有LAN键码或任意输入接口。

## 验证环境与方法

本机Apple Silicon/macOS26.6（25G72）、Chrome155.0.8059.82、QuickTime10.5。本轮用隔离的loopback TLS资格入口发送固定观看意图，运行与普通入口相同的MediaSession；真实播放器通过产品AX/固定媒体键被控制。Cua只准备播放器与进行只读AX/DOM核验，不使用页面脚本写入来代替产品动作，不安装扩展或开启浏览器新权限。此为真实播放器技术链，不能替代S25真实头部动作验证。

| 场景 | 实际输出 | 结论 |
| --- | --- | --- |
| B站初始手动暂停 | START/AWAY/ATTENDING后paused=true，86.739679秒不变；日志“视频已暂停，交由你控制” | 不自动播放，不发送回退 |
| B站自动暂停/恢复/Left | 自动暂停91.187698秒；00:18:18.577恢复Space，00:18:19.874才提交Left；00:18:20.796确认回退；独立DOM为paused=false、90.657786秒 | 先恢复后Left，实际后退并继续播放 |
| B站重复回看 | 本轮只有一次key=123；重复ATTENDING仅变为“看向屏幕” | 不重复回退 |
| YouTube初始手动暂停 | START/AWAY/ATTENDING后paused=true、8.472267秒不变 | 不自动播放，不发送回退 |
| YouTube自动暂停/恢复/Left | 第二轮暂停41.925039秒；00:21:08.528开始恢复，00:21:08.941提交一次Left；独立DOM为paused=false、40.417961秒 | 实际后退并继续播放；第一轮读值较晚不能单独计算退量 |
| YouTube结果观察 | 两轮产品均在约2秒观察窗口后显示“回退结果未确认”，未再次发键；第二轮独立DOM已证明后退 | 保留未确认，不把提交命令当成功；未定位产品观察未确认的完整根因，不增补重试或延长轮询 |
| YouTube自动暂停后手动改变进度 | 42.272570→37.272570秒，仍paused=true；产品撤销旧资格，ATTENDING不播放 | 手动改变暂停位置的保护保留 |
| QuickTime初始手动暂停 | 19.992505秒、开关关闭，START/AWAY/ATTENDING保持 | 不播放或回退 |
| QuickTime正常自动恢复/原生回退 | 暂停22.848578秒；00:22:43.468恢复，00:22:43.488原生回退，00:22:43.717观察确认；原生轮次零Left | 保留原生优先，不双退；稍晚AX读值不用于推算精确秒差 |
| QuickTime停止保护 | 再次自动暂停于29.931637秒，STOP后关闭资格并断开；最终AX仍为关闭/同位置 | 不在收尾补播 |

B站首轮播放按钮焦点不可核验时，产品拒绝Space且零动作；通过正常界面准备播放器焦点后再测成功，未放宽保护。测试网页截图、原始日志、样片与loopback身份都仅留本机；本轮自建标签与资格进程已关闭，普通PausePal继续运行。

## 自动化与独立审查

- `swift test --package-path apps/macos`：7项、0失败；既有换控件/时长/暂停位置/结束优先级回归保留。新增固定键豁免测试覆盖Space/Left、其他轮次、其他pid、flagsChanged与任意其他键码。
- `bash apps/macos/build-probe.sh`：PASS；`swift build -c release --package-path apps/macos`及发布可执行self-test：PASS。
- `bash apps/macos/build-app.sh`：PASS，build4；更新前后指定签名要求一致。普通入口进程在00:24:56实际启动，AX=true、输入监听=true、登录登记状态1（enabled）、接收端就绪，完整进程路径为普通bundle且无资格参数。
- `openspec validate mac-phone-experience --strict`与`git diff --check`：PASS。原生写入报错不补Left由独立代码审查确认，本轮未注入真实AX写入失败，不包装成运行时故障注入覆盖。

依据Comet高风险跨边界规则增加一次独立只读diff审查，未发现可证实的新增合入阻断。审查确认Left入口只在已观察playing后、无远程键码扩张、原生提交后不补Left、重复回看不重发、随机轮次标记/pid/固定键码输入边界保持。审查者未操作播放器或执行测试，不能用静态审查替代上表实际输出。

## 交付边界与父结果

Left语义与秒数仍由播放器决定，某些应用可能无效或触发其他界面动作；这是用户确认的一次有界尝试。跨进程“观察到播放→焦点核验→提交按键”仍有平台竞态，不能绝对保证按键瞬间状态不变。复用全部控件、标题/地址、时长与暂停位置的不可观察换片P2边界仍未解决。锁屏/长时间稳定性/耗电继续DEFERRED，最终新版S25验收待用户，不合main或归档。

- [GAP] LPEXP-01 — Evidence: build4普通菜单入口、原权限及登录登记已核验；实际重新登录自启未补测。
- [GAP] LPEXP-02 — Evidence: 本轮协议/凭据无变化，沿用此前真实配对证据；不新增首配安全保证。
- [GAP] LPEXP-03 — Evidence: 本轮局部恢复后回退与手动暂停技术链通过；全观察值复用换片及跨进程竞态仍限制完整合同。
- [GAP] LPEXP-04 — Evidence: Android未改，沿用此前真实方向输入；S25准确版本/长期后台仍未独立核验。
- [GAP] LPEXP-05 — Evidence: 本轮没有S25实际头部输入，最终集中验收待用户；技术链不冒充真机。

短分支codex/resume-before-rewind实现提交edfd97b，已no-ff合入codex/experience-integration；最终代码集成点5230141f6a2712bf3b8d0e50a6d9e2602f8719fd，两分支在GitHub账号MrPPFruit下推送成功，远端引用已核对。后续状态文档提交不改变该代码集成点，main保持766a196。当前change保持active/pending，局部任务通过不关闭整阶段GAP。
