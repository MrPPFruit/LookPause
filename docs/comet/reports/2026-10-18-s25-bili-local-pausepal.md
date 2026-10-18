# 2026-10-18 S25到Mac补测与PausePal显示名称

当前唯一主目标仍为S25→Mac轻量观影闭环。映射：docs/development-plan.md手机到Mac体验阶段 → LPEXP-03/05与入口呈现LPEXP-01 → 当前change spec/design → tasks6.1/6.2 → MediaSession/ResumeRewind/Receiver、Android普通入口与菜单栏 → 本报告。反例：模拟意图通过但手机不能暂停/恢复；手动暂停被播放；只改文档而安装后的用户入口仍显示旧名。

## 范围与环境

真实媒体使用既有普通菜单栏接收端（代码基线7b7185e，文档基线f11dd8e）、已有S25安装包、Chrome的B站页面与QuickTime的本地样片。没有运行假意图客户端/资格接收端代替手机，没有新增站点适配、权限或改变暂停规则。macOS26.6/25G72、Chrome155.0.8059.82、QuickTime10.5；S25/Android16/OneUI8.5仍为用户提供，未独立读取设置。原始日志/截图/媒体仅保存在本机，不上传公开仓库。

锁屏、自动息屏、长时间后台、发热耗电继续DEFERRED。本轮仍观察到UNKNOWN，它会撤销恢复资格；不把安全拒绝记录抹掉，也不将成功样例扩大为所有识别条件或全部播放器兼容。

## 真实S25观影链

| 场景 | 实际证据 | 结论 |
|---|---|---|
| QuickTime初始手动暂停 | 样片停在10.053374179秒；手机START、离开/回看后仍暂停于同一进度；Mac记录“视频已暂停，交由你控制”，用户确认“一直暂停”，随后STOP | 本轮保护通过 |
| QuickTime自动暂停/恢复 | 22:36:44.196普通进程确认自动暂停，22:36:47.462恢复确认，22:36:47.701确认安全回退一次；原生AX读取暂停点9.367916849秒；用户确认看到了暂停和恢复 | 本轮真实手机到本地播放器链通过；不借用旧轮次时间差作为本轮回退数值 |
| QuickTime之后的未知状态 | 22:36:59.571再次无人脸暂停，随后UNKNOWN取消恢复，回看保持暂停；原生AX读取15.651275711秒、播放开关off；22:37:26.491 STOP | 明确保留的识别限制，未计为又一次恢复通过 |
| B站初始手动暂停 | 手机START后NO_FACE于22:40:12.588裁决“视频已暂停，交由你控制”，回看没有恢复，用户确认一直暂停；后续两次独立DOM均为paused=true、12.904542秒；22:41:10.723 STOP | 本轮保护通过；网页刚暂停后的早期读数12.269634与后续稳定值相差约0.635秒，不声称整个过程数值绝对不变 |
| B站自动暂停/恢复 | 22:42:42.452普通进程确认自动暂停，22:42:46.926确认恢复并跳过回退；独立DOM读到paused=false、50.093151秒，用户确认看到了暂停/恢复 | 本轮真实手机到网页链通过，按能力跳过回退 |

Mac普通进程在动作前记录AX=true、输入监听=true；不是将终端/探针的授权当作普通接收端权限。浏览器/原生UI工具只准备媒体和读取状态，动作来源为真实S25的认证意图及普通裁决器。测试期间没有改动媒体核心、视觉规则或网络协议。

## 名称修改

用户明确选择PausePal。仅统一Android应用标签、主标题、发现提示、观影通知，以及Mac显示名、菜单/可访问名称、首次配对提示和发现服务的可见实例名。界面继续中文，保持眼睛图标与菜单结构；com.lookpause应用ID、_lookpause._tcp服务类型、证书/凭据/Keychain名、UserDefaults键与工程路径均保留。

Android versionCode=2、versionName=0.1.0-pilot.2；旧安装包与新包签名摘要一致，避免要求卸载/重新配对。Mac CFBundleVersion=2，bundle路径及可执行文件名不变。新版Mac普通进程已启动，AX/输入监听均为true，登录启动登记仍为enabled；签名身份要求与旧包完全一致。Android模拟器的新标题及新电脑可见名称已审查。用户对S25覆盖更新、PausePal标题及无需重新配对直连的合并核对反馈“没问题”；Mac另记录已有凭据认证成功，随后START/STOP，并非重新签发首配凭据。S25安装版本未通过调试接口独立读取。Mac菜单完整截图已审查：连接/已停止观影、登录启动勾选和“退出 PausePal”均清晰，无截断/重叠。

## 验证记录

- Comet entry check mac-phone-experience build通过，change保持active，未归档或同步canonical spec。
- Android离线构建第一次误选新缓存目录，缺少AGP插件，失败；改用本机已有默认Gradle缓存后，assembleDebug通过。JDK17使用现有Homebrew安装，未安装新依赖。
- Android模拟器API35仅用于新名称界面审查：安装/启动成功，APK标签PausePal、versionCode2已读取。两次完整截图已审查，标题/间距/卡片/底部说明无重叠；第一次截图列表旧电脑名来自仍运行的旧Mac接收端，第二次在新版Mac启动后已显示PausePal电脑名。
- Swift debug构建与既有self-test通过；没有为名称替换扩建测试框架。
- Mac执行bash apps/macos/build-app.sh构建、签名与self-test通过，Info.plist读取到PausePal/com.lookpause.pilot/CFBundleVersion2，保留原路径与可执行名。普通进程在22:44:15启动，原权限与登录登记正常。
- S25更新/直连由用户反馈核对，Mac普通入口22:46:58认证通过并等待开始；22:47:00 START，22:47:01.630 STOP。用户额外试过开始后及时停止，最终菜单显示已停止；不将这一小段UNKNOWN记为新的播放器验证。
- 名称兼容性检查确认媒体核心、视觉规则、凭据存储、服务类型与应用ID均保持；Mac菜单完整截图和Android完整页面截图均已回看。登录启动开关/退出的前次用户操作证据见2026-10-17-media-background-followup.md，本轮补齐此前缺少的展开菜单截图；未重启/重新登录电脑来验证下次自动唤起。
- OpenSpec strict validate与git diff --check通过；公开暂存清单检查后提交/推送codex/mac-phone-experience，最终提交以包含本报告的Git commit及远端同SHA为准。APK下载服务仅提供固定路径文件，HEAD=200、其他路径404；用户更新后已关闭并确认无法访问；本轮模拟器、日志采集和临时测试标签页亦已清理，Mac普通接收端保留运行。

本轮开发目标分支为codex/mac-phone-experience；integration仍为codex/experience-integration，阶段最终集成commit仍无，main不变。父结果同页换片/结束、完整设备版本与独立审查等GAP不据此通过，保持UNINTEGRATED。
