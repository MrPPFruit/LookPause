# PausePal Windows接收端（机制验证中）

现有最小 C# 只读资格探针，尚无媒体控制、LAN 接收端或普通托盘，不能作为产品部署。当前分支为 `codex/windows-receiver`，来自 integration@c030577。真实状态见[Windows首轮报告](../../docs/comet/reports/2026-10-19-windows-first-pass.md)。

在 Windows/.NET SDK 10 下构建：

```powershell
dotnet build apps/windows/PausePal.Windows -c Release
```

`PausePal.Windows.exe --probe` 只读当前前台 HWND、GSMTC 状态与通用 UIA 媒体控件。它不执行暂停/恢复，不输出页面正文、地址、文档标题或控件原始标签。诊断仅留本机，不公开原始输出。探针实际执行已有证据，但尚未取得前台媒体资格或控制证据。扫描节点/层级/时间均有界，工作进程最多运行8秒；不完整输出不能证明唯一媒体。

从`codex/experience-integration`开始，依次阅读：

1. [当前状态与未收尾项](../../docs/current-status.md)
2. [Windows开发交接](../../docs/windows-handoff.md)
3. [桌面图标规范与ICO素材](../../docs/desktop-status-icons.md)
4. [Windows Codex接续指令](../../docs/windows-codex-prompt.md)

在Windows上开发与真实测试，Android仍由Mac主开发机维护。预定方向是C#/.NET、WinForms托盘、GSMTC媒体能力优先；具体API可行性、打包和兼容范围须由Windows现场证明，不由Mac结果推断。
