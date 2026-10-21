# Windows 原生媒体时间描述修正与剩余边界

日期：2026-10-21；分支 `codex/windows-receiver`；版本0.1.4。延续[无名媒体组报告](2026-10-21-windows-unnamed-media.md)，不覆盖其中0.1.3的历史结果。用户同意实施并要求向Mac交接；本轮没有修改Mac、Android或手机协议。

## 根因与修正

问题仍属于把平台的一种表现形式误当作完整能力合同：

- Chromium原生媒体时间轴将当前时间放在`aria-valuetext`，总时长放在`aria-description`；Windows分别映射为ValuePattern和FullDescription。原实现只读取前者及HelpText，漏掉明确总时长。见[Chromium时间轴实现](https://github.com/chromium/chromium/blob/main/third_party/blink/renderer/modules/media_controls/elements/media_control_timeline_element.cc)、[Windows辅助功能映射](https://github.com/chromium/chromium/blob/main/ui/accessibility/platform/ax_platform_node_win.cc)及[原生控件字符串](https://chromium.googlesource.com/chromium/src/+/HEAD/ui/strings/ax_strings.grd)。
- .NET托管UIA未提供该字段，因此新增只读标准COM属性桥，读取30159。限定同一实际前台顶层、同一RuntimeId；查找和回读均核验精确身份，异常或缺失返回无能力。不按名称、位置或相似节点替代，不增加浏览器扩展或产品网页脚本。接口顺序据[Windows SDK IDL](https://raw.githubusercontent.com/microsoft/win32metadata/main/generation/WinSDK/RecompiledIdlHeaders/um/UIAutomationClient.idl)核对。
- 仅解析明确“已播放时间／elapsed time”和“总时间／total time”等中英文描述中的有效单位及范围，不接收剩余时间、任意数字、直播或非法范围。独立来源SeekDescription参与原有来源、总时长及暂停锚点核验；完整时间文字歧义仍拒绝，不用新来源绕过。
- 原生音量条收起时，真实UIA节点非Offscreen、启用，但bounds为空。它不是操作目标，不应因未展开而失去结构证明。现在仅对精确音量Slider允许空bounds，仍要求直接同组且唯一；组自身、播放和进度控件仍须可见且bounds非空。未接受Offscreen音量或任意隐藏控件。

独立审查确认COM槽位、线程初始化、引用释放及目标绑定；指出探针负例不得修改GetRuntimeId返回的内部数组，已改为Clone。负例只证明错误ID输入被拒绝，不冒称覆盖了原生查找失败分支。临时原始节点日志与RawView实验均已撤回。

## 本机结果

环境：Windows 10 Pro 22H2 x64 / 10.0.19045；Chrome155.0.8059.85；.NET SDK10.0.302、运行时10.0.10。测试视频为[MDN公开CC0样片](https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4)，本机回环服务承载，原生video controls，真实时长5.055秒。为减少短片结束边界干扰，测试页设0.25倍速及循环；测试前通过浏览器聚焦原生播放按钮，使控制栏可访问。这个准备条件是覆盖边界，不是产品已解决隐藏控件的证据。测试页脚本只设置样片播放速度，不属于产品实现。

| 层次 | 命令／方法 | 实际结果 |
|---|---|---|
| 自动回归 | `dotnet run --project apps/windows/PausePal.Windows.Tests -c Release --no-restore` | 100/100；包含解析、来源／总时长变化撤销、零窗口拒绝及既有协议/TLS/DPAPI，不是真实播放器矩阵 |
| 发布 | `dotnet publish apps/windows/PausePal.Windows -c Release --no-restore -o .local/windows-0.1.4 --nologo` | 成功，零警告／错误；框架依赖测试包，保留整个目录 |
| 静态与独立审查 | `openspec.cmd validate windows-receiver --strict --no-interactive`、`git diff --check`，架构及安全分别只读复核 | 通过；修复RuntimeId数组负例后未发现阻断；不表示真实验收完成 |
| 原生字段实测 | `PausePal.Windows.exe --probe` | SeekDescription、有效时间、Paused；完整描述解析成功，错误RuntimeId输入被拒绝；未输出原始描述 |
| 连续真实链 | `qualify-media.py --scenario normal --cycles 3` | 一次连续3轮自动暂停、UNKNOWN保持及回看恢复通过；三轮确认位置均为1秒，进度增长不作为明确状态主链门槛 |
| 独立实际状态 | 另一次本机pilot暂停／恢复，浏览器仅只读video属性 | Owned时paused=true、currentTime=1.158433；恢复后paused=false、currentTime=2.582968，error=null。产品控制不依赖浏览器工具或DOM |
| 初始暂停 | 在已暂停的样片上另起pilot，START/AWAY/ATTENDING | 提示视频已暂停，未进入Pausing/Owned/Resuming；DOM仍paused=true、currentTime=1.498742 |
| 失败补测 | `qualify-media.py --scenario stop` | 在STOP前等待阶段，Owned位置0随后变1，输入健康、无键鼠操作，核心因锚点变化取消资格；DOM仍paused=true。不能登记STOP场景通过，也不能把已知正常3轮扩大为稳定支持 |
| 隐藏边界 | 控制栏隐藏后ControlView及临时RawView完整只读扫描 | 两者均未提供播放／进度控件，产品拒绝新操作。RawView实验无效后撤回 |

原用户网站返回后仍处于缓冲，未取得该网站完整链证据。用户链接、页面标题、原始日志、样片及截图不公开。0.1.3的嵌套合成控件与兄弟框架结果仍有效，但不冒充真实嵌套视频或该网站通过。

## 仍开放

1. 控制栏隐藏后缺少目标控件，当前无扩展路径尚未解决。未改为盲发空格、模糊命中或音频代替播放状态。
2. 暂停后时间描述变化的完整一致性原因未证明，不能直接归因为确定延迟。没有增加固定等待、漂移容忍或忽略锚点变化来让测试通过。后续须补齐观察与实际状态的时序证据再评估。
3. 用户网站、YouTube/B站/本地完整矩阵、普通托盘/S25、真实回退效果仍待；Mac必须在Mac独立实施及实测。相同全部可观察值的换片、最终动作竞态和同步UIA非硬中断的既有边界仍保留。

独立包`.local/windows-0.1.4`的PausePal.Windows.dll SHA-256为`21526D0DA3D6F4699393B762F00E3360F359C6E2CBA7E99B6002ABF7AD977FE8`。没有替换运行中的0.1.2托盘进程或配对身份。测试服务、临时驱动及样片已清理，原用户标签已返回原网站。

跨端入口继续使用[Issue #1](https://github.com/MrPPFruit/LookPause/issues/1)、[共用规则](../../media-control-policy.md)与[Mac接续](../../macos-media-optimization-handoff.md)。本轮属于有限范围修正，OpenSpec真实验收项保持开放，不合main、不归档为完成。
