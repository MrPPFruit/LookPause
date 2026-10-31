# PausePal Windows 接收端（迁移测试版）

2026-10-31：最新独立包为0.1.6，位于`.local/windows-0.1.6`，分支`codex/windows-shortcut-mode`接续Windows0.1.5。新增显式下一轮快捷键兼容模式，默认关闭，124项自动回归通过；网页及S25真实入口待验。启动与取舍见[集中验收](验收说明.md)，设计与证据见[兼容模式报告](../../docs/comet/reports/2026-10-31-windows-shortcut-mode.md)。下文0.1.5为历史。

已有 C# 媒体资格核心、兼容现有手机协议的 TLS 接收端、独立 DPAPI 身份、原生局域网发现、四态托盘、登录启动和恢复后一次回退。当前分支为 `codex/windows-receiver`，来自 integration@c030577。54 项自动化、本机真实 TLS/DPAPI 和原生发现注册/反注册通过；真实媒体、普通托盘与 S25 集中验收尚未通过。最新事实见[迁移报告](../../docs/comet/reports/2026-10-19-windows-migration.md)，用户步骤见[集中验收说明](验收说明.md)。

当前独立包为0.1.5，双击 `.local/windows-0.1.5/PausePal.Windows.exe` 启动托盘；先退出旧版本，不要并行启动。首次运行自动生成本机身份、自启默认开启，配对需要用户点一次允许。无需导入 Mac 凭据或重装 S25 APK。本机有 .NET 10 Desktop Runtime，测试包为框架依赖分发，必须保留整个输出目录。更新保留当前用户的身份文件，不要删除来绕过认证问题。

在 Windows/.NET SDK 10 下构建：

```powershell
dotnet build apps/windows/PausePal.Windows -c Release
dotnet publish apps/windows/PausePal.Windows -c Release -o .local/windows
```

`PausePal.Windows.exe --probe` 只读当前前台 HWND、GSMTC 状态与通用 UIA 媒体控件。它不执行暂停/恢复，不输出页面正文、地址、文档标题或控件原始标签。诊断仅留本机，不公开原始输出。探针工作进程最多运行8秒；不完整输出不能证明唯一媒体。

`--pilot` 是内部本机资格入口，通过标准输入接受固定的 START、STOP、DISCONNECT、EXIT 与四种方向枚举，可能调用已核验的前台媒体按钮。此模式不启用网络或托盘；START 不播放，不提供任意按键或命令执行。使用时须保持已选测试窗口为真实前台，不能把浏览器自动化的后台标签当作原生前台证据。目标须有明确媒体区域或完整局部控件组、唯一控件及稳定文档关联；恢复还需有效时间锚点。本地旧版 Windows Media Player 不满足资格。

安全及协议回归不触达播放器；TLS/DPAPI 测试使用独立临时身份和回环监听，结束后清理，不修改真实配对或自启：

```powershell
dotnet run --project apps/windows/PausePal.Windows.Tests -c Release
```

可选 `--native-discovery` 测试真实原生注册/反注册，不表示手机已发现；`--ui-preview <目录>` 渲染实际 WinForms 控件和图标，用于离屏布局审查，不表示系统托盘主题/DPI已通过。模式参数用 `dotnet run ... --no-build -- --native-discovery` 传递。

`tools/qualify-media.py` 驱动本机资格入口，可测试连续轮次或撤销场景；它会实际影响当前合格媒体，只能在隔离测试窗口中使用。脚本超时不会补发媒体操作。0.1.5已在用户网站验证三轮恢复／回退及随后Space／方向键，108项自动回归通过。隐藏控件、暂停后描述变化、YouTube及S25矩阵仍开放，见[回退报告](../../docs/comet/reports/2026-10-21-windows-rewind-focus.md)。样片生成器 `tools/create-test-video.py` 需 Pillow，仅用于本机测试，不上传生成媒体。

从`codex/experience-integration`开始，依次阅读：

1. [当前状态与未收尾项](../../docs/current-status.md)
2. [Windows开发交接](../../docs/windows-handoff.md)
3. [桌面图标规范与ICO素材](../../docs/desktop-status-icons.md)
4. [Windows Codex接续指令](../../docs/windows-codex-prompt.md)

在Windows上开发与真实测试，Android仍由Mac主开发机维护。GSMTC 暂仅只读观察，实际控制使用能核验唯一前台的 UIA 控件；不能用系统推荐会话取代前台绑定。用户已调整为先完成迁移再集中实测，此前媒体进度失败仍保持开放，不由 Mac 结果推断为通过。
