# Windows 迁移测试版

## 范围与依据

用户最新指示改为“直接迁移开发好，由用户测试验证”。因此继续现有 `codex/windows-receiver`，不再等待 Codex 桌面工具恢复后才能实施手机和托盘；此前媒体真实失败、Mac/Android GAP 和活动变更均保留。本轮开始工作树干净，基线为 c73fd9b；未覆盖旧改动、未从 main 重建。

复核 Mac Receiver/WireFrame/PairingIdentity/MenuApp/StatusIcon/ResumeRewind、Android LanClient/Discovery 的实际输入输出。沿用体验协议 v1，不改共享线协议和手机签名。Comet 保持 build/pending；Ponytail 未安装，没有虚报调用，独立代理审查实施方案及安全边界。

## 已实现

- 无参数运行普通 WinForms NotifyIcon 入口，复用八个深浅色四态 ICO；菜单只有固定状态、登录启动开关和退出。首次手机配对单独显示原生确认窗口，默认拒绝、关闭拒绝，单等待请求及每分钟三次限制。
- Windows 独立 UUID、固定自签 TLS 证书与手机令牌，以当前用户 DPAPI 单文件原子保存。损坏停止，不静默更换身份；不安装受信根、不复制 Mac 凭据。证书通过 Windows 用户级临时密钥容器供 Schannel 使用，不设 PersistKeySet，随证书释放。
- TcpListener/SslStream TLS1.2+，普通入口仅接收私网/链路本地/ULA 来源；TCP 接受即占四个名额之一，握手八秒、hello 十秒、首配六十秒、认证有效事件六秒期限。有界 4096 字节 LF 帧、严格 UTF-8/字段/操作、二十帧每秒和十六条发送队列；写入两秒超时。
- 对齐真实 Android ready/ack/status/error 消费规则，认证错误含 session、可关联错误含 seq；旧会话/乱序/重放无副作用，非法业务帧消耗序号但不续命。同设备重连新 session、新 START；另一已认证手机 BUSY。
- 短锁协议核心不等待磁盘、网络、UI 或媒体查询。媒体仍由现有单 worker 拥有；停止/断线/超时入口撤销，迟到关闭与媒体状态核验会话代次。网络任务明确脱离 WinForms 同步上下文。
- 首配保存为未确认凭据，ready 发出后首个有效认证事件条件确认。ready 丢失时，无令牌仍须用户再次明确批准，不会永久困在已配对记录，也不会静默信任；条件保存和确认避免迟到请求覆盖新凭据。
- 原生 DnsServiceRegister 发布 `_lookpause._tcp.`、动态端口及 v/id/fp；异步回调后才显示服务已注册。退出反注册，参数与委托保持至回调完成；没有安装 Bonjour 或修改网络策略。
- 确认自动恢复 playing 与实际进度后消费恢复资格，再尝试一次回退。明确原生可写时间线优先，网页只在同文档非编辑焦点、精确播放器控件焦点、无修饰键、前台/代次/采样有效时固定 Left。异常不重试，部分键输入只释放按键。现有 UIA 前台资格严格边界保持。
- 日志每个约 2 MiB、最多当前及前轮两个文件；增加 Checking 进度、采样年龄与剩余期限，供用户实测失败定位，不含原始页面/消息/凭据/图像。

## 验证证据

环境：Windows 10 Pro 22H2，10.0.19045 x64；.NET SDK 10.0.302、Desktop Runtime 10.0.10。

| 层级 | 命令与结果 |
|---|---|
| 编译 | `dotnet build apps/windows/PausePal.Windows -c Release --no-restore --nologo`，零警告/零错误 |
| 自动化 | `dotnet run --project apps/windows/PausePal.Windows.Tests -c Release --no-restore`，54/54；原 31 项安全回归及协议、身份、回退边界。TLS 用真实本机 SslStream/固定证书验证，DPAPI 用独立临时身份，均清理测试文件，不操作实际媒体 |
| 网络线程边界 | 用不处理 Post 的同步上下文创建接收端，真实 TLS 往返后断开，要求一秒内进入阻塞诊断时撤销已经生效，且没有续体投递到该上下文，已通过；诊断释放前不能等待日志才撤销 |
| 原生发现 | `dotnet run --project apps/windows/PausePal.Windows.Tests -c Release --no-build -- --native-discovery`，实际注册回调成功，反注册完成；注册中提前退出也完成，没有把发起返回值当成功 |
| 界面 | `--ui-preview .local/windows-ui-review`，实际 WinForms 控件离屏渲染并阅图；最长名称时确认窗口原高度不足，已加高并复核。图标和原生菜单阅图不等于系统托盘的真实主题/DPI验收 |
| 分发 | `dotnet publish apps/windows/PausePal.Windows -c Release --no-restore -o .local/windows --nologo`，输出本机可运行的框架依赖测试包，需保留整个目录 |

首轮真实 TLS 自动化为 48/49，服务端明确报 `platform does not support ephemeral keys`。根因是把证书内存导入模式等同于平台 TLS 可使用的密钥模式；按 Windows Schannel 的密钥要求改为用户级临时容器后真实握手通过。不是放宽 TLS、关闭证书校验或猜测网络问题。参考 [X509KeyStorageFlags](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509keystorageflags) 与 [微软密钥生命周期说明](https://support.microsoft.com/en-us/servicing/dotnetframework/windows/private-key-lifetime-on-windows-and-the-january-2021-net-framework-security-and-quality-rollup)。原生发现参考 [DnsServiceRegister](https://learn.microsoft.com/en-us/windows/win32/api/windns/nf-windns-dnsserviceregister)。

## 独立审查与抽象归因

审查指出并修复：非法业务帧不消耗序号、首配落盘与客户端获知凭据之间的提交缺口、过期原生指针取消竞态、焦点检查后未重新核验操作资格、网络续体继承 UI 同步上下文，以及断线撤销排在同步日志后。共同原因是把“已排队/已保存/已观察”的局部状态当成跨边界提交成功，并让非关键副作用占据撤销路径；现在分别以会话代次、凭据条件提交、原生回调生命周期、操作前最终检查和撤销先于诊断约束。

## 尚待用户真实验收

- 最新媒体连续实测此前失败，未由本轮构建/模拟关闭；此前部分中间版真实 Chrome 证据不能覆盖最新版本。B站/YouTube 各多轮、UNKNOWN、初始手动暂停、接管/换片/结束/停止/断线保护及实际回退效果均待用户。
- 旧 Windows Media Player 缺可核验播放项与时间线，已知会被拒绝控制，未实现其兼容。其他本地播放器也必须按实际可观察能力验证，不能宣称全支持。
- 原生服务注册成功不等于 S25 发现成功。S25 现有 APK 的发现、一次允许、记住、开始/停止/重连，以及普通托盘入口、系统防火墙、主题/DPI和实际登录启动待一次集中验证。
- 暂无新系统托盘实景截图；仅离屏控件预览。Computer Use/Chrome 宿主工具不可用未作为本项目产品缺陷处理，也未绕过组织/地区限制。
- Mac 最新集中验收、Android AND-CLOSE-01/02/跨电脑文案仍归原 owner，未改 APK/签名、未关闭父 change、未合 main。

用户步骤见 [Windows 验收说明](../../../apps/windows/验收说明.md)。未经用户最终验收，不归档 Windows change 或宣称 Windows 产品已全部完成。

## 开发分支交付记录

实现集成点 `1be5c09` 已提交并推送 `origin/codex/windows-receiver`。推送前实际 `gh api user --jq .login` 返回 MrPPFruit；公开清单为 Windows 源码、测试、OpenSpec 与中文说明，未含本机二进制、日志、截图、媒体或配对身份。最终代码再跑 54/54 及 publish 通过，严格 OpenSpec 校验通过。修复后的最终独立复核未发现新的确定代码阻断，可以交用户集中验收；原生媒体、S25、系统托盘与登录实景仍开放。
