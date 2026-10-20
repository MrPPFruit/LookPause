# 当前状态、上传范围与剩余收尾

核对日期：2026-10-20。本页是交接快照，现行行为仍以product-spec、decision-log、protocol/CONTRACT及活动change为准；历史报告不覆盖后续决定。

2026-10-20 Windows 后续：用户确认参考 Mac 现有逻辑实施无扩展迁移。0.1.2 已改为明确播放状态直接暂停、同目标明确播放状态确认恢复，不再对明确状态统一要求进度增长；暂停后的有效位置锚点、输入接管、前台和停止／断线撤销仍保留。时间读取改为精确媒体区域内的唯一完整时间，缺失或歧义不得取得恢复资格；接管只取消本轮，不停止观影，后续手动播放并再次离开可建立新轮。74 项自动回归通过，独立构建位于 `.local/windows-0.1.2`，详见[本轮报告](comet/reports/2026-10-20-windows-mac-media-alignment.md)。**新版真实 YouTube 连续链、手机和系统托盘仍待集中验收，不能声称实测故障已解决。**

此前 YouTube 失败已复现：输入监测健康，但 UIA 的进度条和时间文字在多次完整采样中冻结，另有播放控件不可识别的历史失败。0.1.1 仅纠正含糊提示、增加固定类别诊断，57 项回归通过；焦点和属性订阅实验没有解决，已撤回。底层 provider 的新鲜度尚未证明，不是需要用户再授予输入权限，详见[故障报告](comet/reports/2026-10-20-windows-youtube-failure.md)。

## GitHub与版本

仓库：<https://github.com/MrPPFruit/LookPause>。当前完整基线为`codex/experience-integration`，本轮开始已核对远端69b6408与本地一致；本次文档整合后的commit见[交接审计报告](comet/reports/2026-10-19-windows-handoff-audit.md)。`main`仍为766a196早期基线，不能用默认clone后的main开始Windows实现。

跨端基线已有Mac/Android源码、Gradle wrapper、固定模型下载/校验逻辑、协议schema/fixture、相关测试、现行规格/设计/报告和Windows ICO素材。Windows开发在`codex/windows-receiver`，已有C#探针、媒体资格核心、手机接收端/四态托盘/自启与一次回退迁移测试版，尚未合入跨端基线。**源码与测试包不等于三端产品都已验收。**

按项目约定未上传安装包、模型权重、Mac运行目录、手机/电脑配对凭据、签名私钥、原始日志/截图、测试视频与私人来源。Windows不能从GitHub拿到Mac的Keychain或原Android签名，也不应复制这些秘密。`.local`中的临时APK下载地址不是跨电脑部署入口。

## 平台结果

| 范围 | 已实现/已验证 | 未闭合项 |
|---|---|---|
| Mac普通入口 | build6实际运行；状态、登录启动开关、退出；最新10项Swift回归本轮重跑通过，self-test通过 | 新版完整S25集中验收、最新四态在手机实际动作下的观察待用户；登录启动已有登记/开关证据，未做重新登录实测 |
| Mac媒体 | S25在较早普通入口实际驱动YouTube/B站/QuickTime暂停恢复及手动暂停保护；最新UNKNOWN修复与先恢复后一次回退有三播放器各3轮真实技术链 | 最新构建的S25连续多轮、UNKNOWN后回看、手动暂停/换片/结束/重连集中体验未确认；完全相同可观察值的换片和观察→操作竞态仍是安全边界 |
| Android | 代码/构建versionCode=2，ARM64，Mac为主开发机；用户已反馈安装、覆盖改名更新并实际联调。此次源码未改，离线assembleDebug及3项方向单测重新执行通过 | Lint实际运行发现1错误/17警告，见下文；S25安装版本及系统/One UI未独立读取；少量连接/错误文字仍写Mac，Windows正式体验前需在Mac统一改为电脑/所选名称并用原签名更新 |
| 协议 | 体验v1已由Mac/Android实现，固定TLS身份、一次允许配对、会话/序号和停止撤销；正式协议未冻结 | Windows兼容尚未证明，schema本身不是消息安全验证器；认证后error的session字段需按真实Android消费端核对 |
| Windows开发分支 | Windows10 x64/.NET10；GSMTC/UIA探针、媒体安全核心；TLS接收端、DPAPI独立身份、原生发现、四态托盘/自启、恢复后一次回退已接入；0.1.2 对齐 Mac 明确状态主链，74项自动回归及本机真实TLS/DPAPI通过；原生注册/反注册已有前轮证据 | 用户已改为先迁移后集中实测；媒体此前连续进度失败尚未关闭，旧WMP不满足资格，B站/YouTube/本地矩阵、普通托盘/登录启动及现装S25联调仍待用户；离屏UI图不计系统托盘实测，见[本轮报告](comet/reports/2026-10-20-windows-mac-media-alignment.md) |

本轮无ADB设备连接，没有补新的S25动作；不能从构建/单测推断三星相机或后台稳定。Android Lint已运行完成并失败，不用构建通过替代静态检查。

## Android静态检查的真实剩余项

本轮先用默认Gradle缓存重新构建/跑单测；Lint离线缺两个分析器依赖，正常解析官方依赖后实际完成分析，结果为**1 error、17 warnings**。没有通过禁用检查或生成baseline隐藏结果，本轮不改产品源码。

- **AND-CLOSE-01，必须处理或明确有界理由：** AndroidManifest有CAMERA权限、未显式声明相机硬件feature，触发PermissionImpliesUnsupportedChromeOsHardware错误。影响是清单/分发硬件过滤，不证明S25运行崩溃；也不能把Lint建议机械当作扩大ChromeOS兼容的授权。按仅ARM64手机/前摄的现行范围选择正确声明并重跑Lint。
- **AND-CLOSE-02，需要逐项判断的4个警告：** WatchingService的camera前台服务类型在minSdk26下的InlinedApi；LanClient心跳scheduleAtFixedRate的缓存后集中补发风险；CredentialStore所在应用缺显式Android12+ dataExtractionRules；自定义X509TrustManager。前3项分别需低版本调用链/调度语义/备份迁移边界核查；最后一项现行代码有证书有效期与固定SHA-256校验，不是无条件信任全部证书，需保留已批准的自签固定身份规则并做有理由审查，不能为消警告移除校验。
- 其余13个警告：10项版本更新提示、ARM64-only的ChromeOS ABI提示、2项KTX风格建议。固定MediaPipe版本有已记录的模拟器原生崩溃依据，不做批量依赖升级；未支持ChromeOS也不冒称支持。这些提示不全等于产品故障，仍需记录处置，不报“零警告”。

Owner仍为Mac上的Android主工程；本轮交接文档不将它们置为已修复。锁屏后断开的既有反馈不能仅凭心跳警告就归因于补发，尚无对应事件链证据。

## 收尾顺序

1. **Android静态收尾与跨电脑文案。** 先处理AND-CLOSE-01/02并复查；LookPauseApplication与LanClient的Mac专用提示也在Mac统一修正。这些文案不限制电脑发现/协议，但会误导Windows用户。目前不要求重新安装来准备交接；后续Mac主开发机同签名覆盖更新，不在Windows重新生成debug签名替换已安装包。
2. **LPEXP-05：一次集中S25验收。** 复用[最新媒体清单](comet/reports/2026-10-19-unknown-final-acceptance.md)，加上连接空心→开始/看屏幕睁眼→侧看闭眼→停止空心→断开断链。媒体重点是连续3～5轮、UNKNOWN后恢复、先恢复再一次回退、初始/接管后的手动暂停、换片/结束和断线不补播。遇失败记录大致时间和固定状态，不收集人脸资料。
3. **已知媒体安全边界。** 同窗口/控件/上下文/时长/暂停位置全部相同的换片不能被当前观察证明；普通API最终动作也存在竞态。需要明确有界支持合同或补证，不能通过更多同类PASS或一段“已完成”说明关闭。相关证据见[安全审查](comet/reports/2026-10-18-media-boundary-security.md)。
4. **LPWIN系列：Windows本地开发与验收。** 按[Windows交接](windows-handoff.md)执行；Mac已通过的机制可作参考，不能继承为Windows通过。用户在Windows会话明确开始后，先登记本轮Windows唯一目标与其验收，不误归档Mac活动change。
5. **最终交付与main。** 现有mac-phone-experience的2.2/2.3仍未完整闭合，不合main、不归档成complete。完整产品验收前保留integration基线及具体GAP；Windows能力只在Windows阶段实际产出后计入。
6. **历史工作流清理。** foreground-media-feasibility的delivery_outcome已经是superseded，但archived=false、仍列在活动change；后续应按历史归档流程收束且跳过canonical spec同步。本轮不为了看起来清零而将其改为complete，也不把仍需继续的mac-phone-experience一起归档。

长时间后台、锁屏/息屏持续观影、发热与耗电按用户决定继续DEFERRED；已知S25锁屏会断开，不称为支持锁屏观影。精确眼动及全部播放器兼容也没有成立。
