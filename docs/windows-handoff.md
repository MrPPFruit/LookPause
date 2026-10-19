# Windows开发接续指南

2026-10-19；用途是让Windows上的Codex在当前会话接续开发，不是Windows已完成或可部署的说明。先读[当前状态](current-status.md)，其中Mac/S25的GAP不能因换机器而消失。用户采用Codex对话框接续，完整可复制指令见[windows-codex-prompt.md](windows-codex-prompt.md)。

## 1. 拉取正确代码与阅读顺序

仓库`https://github.com/MrPPFruit/LookPause.git`，开发基线`codex/experience-integration`，不要用旧main。无仓库时在用户可写工作目录克隆；已有仓库先核对remote、branch和dirty状态，保留用户改动，不reset/clean。不要为接续额外注册Codex项目或新建会话。

```powershell
git clone --branch codex/experience-integration https://github.com/MrPPFruit/LookPause.git PausePal
Set-Location PausePal
```

此示例只用于不存在PausePal目录时；它只获取源码，不部署Windows应用。已有目录由Codex检查并安全复用，不能直接覆盖执行。

阅读顺序：AGENTS → README → docs/product-spec → decision-log → development-plan → protocol/CONTRACT → test-plan → current-status → 本文 → desktop-status-icons。原件只作历史，最新用户决定优先。下一步在Windows现场创建本平台OpenSpec change、Acceptance映射和tasks，先环境/媒体机制，逐阶段合到唯一集成分支；不要仅复制Mac的任务勾选或把未完成Mac change当作已complete父交付。

## 2. 已确认的产品行为

显示名PausePal，界面中文。手机在家庭LAN主动选一台电脑，电脑被动接收；首次电脑点一次允许并记住设备，之后不用扫码/输码/逐页授权。手机负责开始/停止和粗粒度方向，电脑自动识别唯一前台媒体，内部保持目标身份和暂停归属。无网站/应用适配名单，不安装浏览器扩展，不开网页脚本权限，不提供任意按键/shell/脚本/文件接口。

Windows目标沿用轻量后台托盘：状态、登录后启动开关、退出；开机自启指登录后启动，沿用桌面默认开启要求，不做登录前服务。四态图标复用[规范](desktop-status-icons.md)和`docs/assets/desktop-status/{light,dark}/*.ico`。权限/首次配对是既有安全步骤；若Windows需要新的用户可见设置、步骤或改变默认行为，先说明并获确认，不借迁移增加控制面板。

锁屏/长时间/耗电继续不纳入本轮。优先保持S25现有安装包可用，Android仍在Mac维护；少量Mac专用提示是已登记文案GAP，不是另起手机工程的理由。

## 3. Windows原生技术方向与必须先证实的问题

既定方向为C#/.NET、WinForms NotifyIcon和GSMTC。建议Windows11及受支持的.NET10 LTS起步，现场核对系统/架构、Git、SDK、Windows SDK及需要的桌面工具；不把建议写成已经支持的版本矩阵。微软当前列.NET10为LTS、支持至2028-11-14，.NET8/9将于2026-11-10结束支持，见[官方支持政策](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)。依赖用到才选，先复用系统API，不先铺全套框架或额外运行服务。

| 责任 | 最小候选 | 必须现场证明的边界 |
|---|---|---|
| 媒体状态与显式控制 | GSMTC播放状态、TryPauseAsync/TryPlayAsync、timeline；能力不足再评估通用UI Automation | [GetCurrentSession](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager.getcurrentsession)只是系统认为用户最可能想控制的会话，**不证明是前台视频**。需核验前台HWND/进程、会话/控件/播放项；同浏览器多个标签/窗口歧义则不操作。不能把SourceAppUserModelId或进程名当作媒体唯一身份 |
| 原生能力与打包 | WinForms+WinRT，先最小资格程序 | [RequestAsync](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager.requestasync)文档列globalMediaControl能力，实际非打包/打包入口差异需测试，不预先保证零权限或直接套UWP包。先证读状态和一条真实控制链，再确定最小分发方式 |
| 可选回退 | 有效原生时间线优先；否则安全焦点下一次Left | [TryChangePlaybackPositionAsync](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession.trychangeplaybackpositionasync)参数为ticks，不是秒；换算并核对范围/实际进度。统一先恢复确认playing再回退一次，不在暂停态先Left；未确认不重试/补播 |
| 发现与认证 | 原生DNS-SD/mDNS候选DnsServiceRegister；TcpListener+SslStream | [DnsServiceRegister](https://learn.microsoft.com/en-us/windows/win32/api/windns/nf-windns-dnsserviceregister)异步，调用返回不等于注册完成，注册随进程结束撤销。必须在现有S25发现列表里实际可见；不能只在本机DNS枚举成功。先评估原生API，不默认安装Bonjour服务或更换发现协议 |
| 手动接管与焦点 | 原生输入/前台通知；必要时有界固定输入 | 真实键/点击/滚轮撤销，纯移动不撤销；本工具自己这轮的固定输入可识别，不允许泛化忽略注入事件。[SendInput受UIPI限制](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)，不得为控制管理员播放器默认提权或关闭系统保护；不可达就降级 |
| 本地身份与托盘 | 当前用户证书/CNG、DPAPI或系统凭据存储；NotifyIcon | Windows生成独立电脑UUID、证书和token，不能复用Mac身份；更新后保持同一电脑身份。UI线程只投影状态，媒体裁决和网络回调不得互相竞态 |

表中都是候选与审查重点，未在Windows运行验证。Windows如何识别前台与同一媒体，是本阶段首要风险；不能因为GSMTC的Pause返回true，就删掉本工具暂停归属、用户接管与换片保护。

## 4. 复用什么，重新实现什么

| 现有资料/owner | Windows应复用的语义 | 不能直接照搬 |
|---|---|---|
| Mac MediaSession.swift | 开始不播放，明确暂停/恢复，确认后取得归属，保留UNKNOWN暂停资格，强撤销清除；先恢复后一次回退 | AppKit/AX对象、定时精度和2秒/0.05秒等Pilot初值，不作为Windows实测结论 |
| GenericPilot.swift内FrontMedia/InputWatch、MediaTimeline.swift、ResumeRewind.swift及MediaSafetyTests | 前台/目标/控件/结束/暂停锚点、焦点、真实进度观察、自身输入与用户接管区分的反例 | 不能只比较窗口标题/媒体标题，也不能把任意静止/音频激活当作播放或暂停 |
| Receiver.swift、WireFrame.swift、PairingIdentity.swift | 单手机、TLS、首次一次允许、后续token、session/严格序号、停止断线、输入边界 | Keychain与openssl路径是Mac实现；Mac认证后部分error不带session，而真实Android读取器要求它，不应照抄这一差异 |
| Android Discovery.kt、LanClient.kt、CredentialStore.kt | 已安装手机的真实协议消费行为、发现过滤与固定身份 | 不在Windows复制Mac的密钥、debug签名、SDK路径或重写手机主流程 |
| DesktopStatus/StatusIcon与ICO | 认证与方向独立于媒体状态，STOP/UNKNOWN空心、重连不继承方向 | 不能仅有素材而不接实际生命周期；托盘主题、DPI缩放与退出/自启需要Windows真实截图和系统登记证据 |

所有源文件均位于`apps/macos/Sources/LookPauseMediaProbe/`或`apps/android/app/src/main/java/com/lookpause/android/`。父阶段的同观察值换片与最终操作竞态为已知限制，Windows不得继承“已解决”的结论。只把实际可确认的唯一前台媒体纳入有界能力，未知不盲切换。

## 5. 与现有Android兼容的线协议检查表

正式契约见`protocol/CONTRACT.md`的“体验协议v1”，schema与fixture仅辅助；前部候选WSS/固定端口/PAUSE-PLAY远程命令不是现行实现。

- DNS-SD类型`_lookpause._tcp.`；服务名可含PausePal和电脑名；TXT是`v=1`、稳定UUID `id`、叶证书DER SHA-256小写64位hex `fp`。动态TCP端口，局域网地址需符合Discovery的私网/链路本地/ULA过滤。Android以id与fp保持身份，不能每次启动更换证书或id。
- TLS1.2+上的UTF-8逐行JSON，非HTTP/WebSocket；单帧含LF最多4096字节，限制空行、队列、速率和连接数。只有固定hello/ready/start/stop/intent/ping/ack/status/error。
- 首次hello含v/kind/deviceId/name，无token；电脑只允许用户批准的手机，ready返回新session和随机32字节token的标准Base64（44字符）。后续hello验证保存token；同一连接一次session，重连新session且需新START。
- 认证后手机每帧session+严格递增Int64 seq；重复、乱序、旧会话拒绝，不能补发旧PLAY（线上根本没有PLAY命令）。手机2秒ping；电脑需ack，现有手机读超时8秒，不能只等方向变化才发响应。电脑按现行6秒无有效事件撤销/断开，首配等待不超过现有手机65秒读窗口。
- Android在ready之后对**每个服务端帧**先核对v和session，包括error。因此认证后ack/status/error均带当前session，关联请求的ack/error还需带seq；首配前error按既有hello错误路径处理。Mac现有部分error缺session会使手机安全断开并显示通用错误，不能把schema允许省略误认成Android能解析；Windows兼容测试要明确覆盖此分支，若调整共享契约先补fixture并说明跨端影响。
- START不播放。AWAY/NO_FACE只在已开始、已知当前playing且目标合格时暂停；确认暂停后才有资格。暂停待确认/已暂停阶段UNKNOWN只保留原确认/监测，ATTENDING才可恢复；UNKNOWN不创资格、延长确认或发送控制。相机故障的UNKNOWN→STOP、手动接管、换片/结束、STOP、断线、超时、切电脑均强撤销。
- 首配为用户确认家庭LAN的TOFU，不承诺抗首次主动中间人。不要安装根证书或信任全部远端证书；手机固定证书指纹规则保持。单次只一台认证手机可控制，未认证不变绿、不操作媒体；token/证书私钥不进日志。

防火墙/发现失败先分层诊断服务注册、接口地址、同LAN、Android实际resolve、TLS证书、认证与session，不用换协议掩盖原因。不要自动关闭防火墙、开放公网或把网络改为专用；确需入站权限时说明应用、私网范围与作用并由用户完成系统授权。

## 6. 在Windows创建的实施任务与完成条件

以下是父结果与验收输入，不是已实施任务；Windows会话据此建立本平台change/tasks，避免维护第二套实施计划。同一阶段只做一个用户可见目标，前项有证据后再进下一项。当前Mac的active change不被这份指南关闭；Windows可行性不能冒称前阶段整体验收。

| Acceptance ID | 阶段/用户结果 | 完成证据 | 代理指标失败反例 |
|---|---|---|---|
| LPWIN-01 | 本机唯一前台媒体安全暂停/恢复 | 先核心读状态，再一条真实播放器链，随后B站/YouTube/本地各连续多轮；初始手动暂停、接管、换片、结束不误播 | 编译/系统Pause成功但实际控制后台标签或手动暂停被播放 |
| LPWIN-02 | S25发现、一次允许、可信连接及停止/重连 | 现有S25包发现Windows，真实TLS/批准/记住/新session；未授权、过长帧、重放/旧会话/超时与停止都拒绝或清资格 | 仅本机模拟器发现，手机看不到；或只凭ACK称播放成功 |
| LPWIN-ICON-01 | 普通托盘与登录启动/退出 | 四态实际跟随手机、停止仍绿；深浅色和不同缩放完整截图；登录登记/开关/退出及再次启动保持身份 | ICO存在但图标固定，或重启换证书需重配对 |
| LPWIN-03 | 同一媒体先恢复后回退一次 | playing确认后原生seek或一次安全Left，实际进度回退且继续播放；重复回看不重复，无安全焦点跳过 | Left发出成功但进度没变，或暂停态回退触发播放 |
| LPWIN-04 | 普通S25→Windows功能交付 | 本机最小技术链通过后一次集中手机验收；形成构建/测试/真机/残余边界分层报告、可运行构建与明确安装方式 | 本机资格入口全PASS但普通托盘/真机不能用 |

先LPWIN-01本机核心；再LPWIN-02网络兼容和LPWIN-ICON-01普通入口；最后LPWIN-03与LPWIN-04集中整链。若前台判定机制不成立，先查根因和最小替代，不先扩Harness/证据框架或逐站适配。默认单代理直接推进，普通任务不叠加代理审查；认证/输入边界等高风险修改按适用Comet规则验证。

## 7. 分支、交付和用户配合

当前唯一跨端integration是`codex/experience-integration`。Windows从其最新远端创建`codex/windows-receiver`短分支；若该分支已有工作，先检查其起点与状态并安全续做。每个可独立验收阶段提交/整合，不能从过期main反复拉平级分支；若未来多阶段必须设Windows integration，先记录唯一目标与最终合回路径，不让两个集成分支同时被称为最新跨端基线。所有GitHub写操作前核验`gh api user --jq .login`为MrPPFruit。开发分支可按用户授权推送，main保持既有完整验收门槛；日志、视频、密钥/签名、私有SDK路径不上传。

未来Windows新会话先报告“环境事实、最小计划与当前唯一目标”，然后自主推进已确认范围。不要反复让用户中途测试；有自动化就先完成机制/真实输出验证，实体头部动作和确需人确认的系统权限留到必要时说明，最终给一次集中验收。缺Git/.NET等免费开发工具先说明并采用官方安装路径；新付费、提权/防火墙扩权、改变产品交互或破坏性操作遵守用户确认边界。

本轮交接材料不保证任意网站/播放器兼容，不宣称Windows已部署。已知Mac/Android剩余项见current-status；用户之后可先完成集中验收再在Windows粘贴接续指令，不需要拷贝Mac本机私有材料。
