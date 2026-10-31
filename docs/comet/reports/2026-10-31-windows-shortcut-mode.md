# Windows快捷键兼容模式交付

日期2026-10-31，Windows0.1.6，分支`codex/windows-shortcut-mode`，基于Windows0.1.5@1f5d597。未改Mac/Android、协议、配对身份或main。

## 调查与决定

现场日志输入监听健康，matchingButtons=0、invokableButtons=0，UIA路径未识别播放控件。公开页面加载ArtPlayer，控件工厂创建div，与当前Button+Invoke过滤不匹配；这是线索，不是该页面真实运行UIA树的完整证据。没有宣称普通识别规则已修好。临时HTML及JS已清理，未上传私人链接或媒体。

用户明确同意独立模式的风险：未知播放状态可能误切换，自动结束／同页换片可能漏检，不回退。正常模式的严格暂停归属规则保留，例外只适用于主动选择的兼容模式。本轮不以音频替代播放状态。

## 实现

托盘新增“下次观影用快捷键兼容模式”，默认关闭、重启不保留，仅手机下一START读取，不中途改当前owner。沿用现有系统菜单、微软雅黑9号及勾选样式，四态图标不变。

ShortcutSession独立于MediaSession，复用现有意图链和InputWatch。START不发键；首次离开绑定实际前台窗口／进程／文档链／exact焦点，离开／回看各一次固定Space，不读取播放控件和状态、不抢焦点、不重试、不追加Left。只报告效果未确认。必须非编辑焦点、无修饰键、输入健康；读取与最终执行保留原1500ms预算。系统收键不证明播放器响应，失败或部分提交解除本次控制；部分提交只清理释放。

首次绑定后键鼠接管、焦点／前台变化、观察不连续／失效、停止和断线清除旧轮。解除后须用户确认播放与焦点再在手机开始，不自行重授。纯鼠标移动不撤销，UNKNOWN不发键、不重授。自身Space仅固定VK、注入标志和本进程随机标记均匹配时豁免；其他注入和真实输入继续撤销。

## 分层验证

Windows10 x64，.NET SDK10.0.302，框架依赖测试包。

- 空骨架阶段新增测试实际失败10项，总113/123；实现后123/123，再补精确输入豁免负例，最终124/124。新增16项覆盖重复意图、UNKNOWN、整次解除、新START、停止、焦点／进程变化、过期／未来采样、读取期间撤销、观察中断及失败无重试。替身不触达媒体。
- `dotnet run --project apps/windows/PausePal.Windows.Tests -c Release --no-restore`：124/124，独立审查者也运行通过。
- `dotnet publish apps/windows/PausePal.Windows -c Release --no-restore -o .local/windows-0.1.6 --nologo`：成功。
- `dotnet run --project apps/windows/PausePal.Windows.Tests -c Release --no-build -- --ui-preview .local/windows-0.1.6-ui`：成功；查看了实际WinForms菜单未勾选／已勾选截图，无遮挡截断。离屏浅／深背景图不等于系统托盘主题／DPI通过。
- `openspec validate windows-shortcut-mode --strict --no-interactive`及Git差异检查通过；独立安全与架构复核无阻断。
- 包中PausePal.Windows.dll SHA256：`14F5148225C8F534982F5915A2876E00FD035A297D988138E3C0D4BE368FCBEA`。

## 真实验收仍开放

本会话无可调用的浏览器／桌面操作接口，未向用户媒体发Space；该站、托盘下一START选择及S25普通入口未实测。运行中0.1.5未被强制替换，需正常退出后启动0.1.6，不同时运行。用户先播放并点击播放器，再手机开始，之后离开／回看；手动操作后应解除且需重新开始，见[集中验收](../../../apps/windows/验收说明.md)。

站点后续即使通过也仅证明快捷键路由，不证明播放状态／暂停归属／结束或自动换片识别。不关闭正常模式已有失败项、Mac/Android GAP或windows-receiver任务。Mac可参考用户最新取舍，本轮未实现Mac模式。
