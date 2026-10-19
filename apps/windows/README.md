# PausePal Windows接收端（机制验证中）

已有 C# 只读探针、本机媒体资格核心与安全回归；尚无 LAN 接收端或普通托盘，不能作为产品部署。当前分支为 `codex/windows-receiver`，来自 integration@c030577。最新事实见[Windows媒体阶段报告](../../docs/comet/reports/2026-10-19-windows-media-progress.md)，初始环境见[首轮报告](../../docs/comet/reports/2026-10-19-windows-first-pass.md)。

在 Windows/.NET SDK 10 下构建：

```powershell
dotnet build apps/windows/PausePal.Windows -c Release
```

`PausePal.Windows.exe --probe` 只读当前前台 HWND、GSMTC 状态与通用 UIA 媒体控件。它不执行暂停/恢复，不输出页面正文、地址、文档标题或控件原始标签。诊断仅留本机，不公开原始输出。探针工作进程最多运行8秒；不完整输出不能证明唯一媒体。

`--pilot` 是内部本机资格入口，通过标准输入接受固定的 START、STOP、DISCONNECT、EXIT 与四种方向枚举，可能调用已核验的前台媒体按钮。START 不播放；不提供网络接口、任意按键或命令执行。使用时须保持已选测试窗口为真实前台，不能把浏览器自动化的后台标签当作原生前台证据。当前仅在有明确媒体语义、唯一控件、稳定文档上下文和可核验秒时间线时尝试动作；本地旧版 Windows Media Player 不满足资格。

纯安全回归不触达播放器：

```powershell
dotnet run --project apps/windows/PausePal.Windows.Tests -c Release
```

`tools/qualify-media.py` 驱动本机资格入口，可测试连续轮次或撤销场景；它会实际影响当前合格媒体，只能在隔离测试窗口中使用。脚本超时不会补发媒体操作。最新版三轮驱动尚未通过，不能把回归通过当作该脚本或真实播放器通过。样片生成器 `tools/create-test-video.py` 需 Pillow，仅用于本机测试，不上传生成媒体。

从`codex/experience-integration`开始，依次阅读：

1. [当前状态与未收尾项](../../docs/current-status.md)
2. [Windows开发交接](../../docs/windows-handoff.md)
3. [桌面图标规范与ICO素材](../../docs/desktop-status-icons.md)
4. [Windows Codex接续指令](../../docs/windows-codex-prompt.md)

在Windows上开发与真实测试，Android仍由Mac主开发机维护。预定方向是C#/.NET、WinForms托盘、GSMTC媒体能力优先；具体API可行性、打包和兼容范围须由Windows现场证明，不由Mac结果推断。
