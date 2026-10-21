# Mac 媒体识别优化接续

日期：2026-10-21。用户已批准 Windows 取消固定容器名称门槛，并要求 Mac 在本平台评估相同问题。这里是接续说明，Mac 源码尚未由本次 Windows 工作修改，原 Mac／Android 未收尾项仍保留。

统一跟踪：[GitHub Issue #1](https://github.com/MrPPFruit/LookPause/issues/1)。Mac会话可从该Issue和本页接续；创建Issue不等于Mac已开始执行。

## 取得资料与保护已有工作

在现有 Mac 会话及工作目录续做，不另建 Codex 会话。先检查分支、远端和未提交改动，安全获取 `codex/windows-receiver`；不得从早期 main 建立依赖，也不得重置、覆盖或清理现有改动。

可先只读获取，不切换当前工作区：

```sh
git status --short
git fetch origin codex/windows-receiver
git show origin/codex/windows-receiver:docs/media-control-policy.md
git show origin/codex/windows-receiver:tests/media-policy/cases.json
git show origin/codex/windows-receiver:docs/comet/reports/2026-10-21-windows-unnamed-media.md
```

遵循本机 AGENTS、现行 decision-log 和现有 Mac 活动变更；先审查方案，再实施。以上文档／用例可选择性集成到 Mac 开发分支，先比较本机已有版本，不整分支覆盖 Mac 或 Android。Windows 的运行产物、日志、配对身份和系统权限均不能迁移为 Mac 证据。

## 核查位置与建议

`apps/macos/Sources/LookPauseMediaProbe/GenericPilot.swift` 的 `frontMedia` 当前沿祖先传播 `mediaScope`，以描述／角色描述命中视频相关词作为控件进入候选的门槛。同时需要核对 AXWebArea 的地址匹配是否把正常嵌套播放器排除。Mac 的 AX 可能比 UIA 提供更多媒体角色，须以本机完整输入输出为证，不能因为 Windows 不暴露名称就假定 Mac 也不暴露。

按[共用规则](media-control-policy.md)评估：保留已有明确媒体证据；缺少名称时才考虑可证明的局部功能组，拒绝从整页或兄弟框架拼接。直接同组是 Windows 本轮采用的保守结构合同，Mac 若暴露不同层级，应先证明等价的局部关联，再更新共用规则及用例，不机械按控件个数硬凑。

必须联动检查 `MediaSession.swift` 的暂停归属与恢复锚点、`ResumeRewind.swift` 的文档／焦点边界，以及输入撤销。只取消过严的表示形式要求，不取消真实暂停确认、时间单位核验或手动暂停保护。回退仍独立降级，普通操作后可建立新轮。

原生网页视频可能只暴露局部已播放时间，缺少完整时长或单位证据；Windows 已发现此缺口，未解决。请在 Mac 单独采样时间来源和实际效果，不复制 Windows 的数值范围解释，也不把 Mac 的既有三个播放器通过等同于原生网页视频通过。

## 交付要求

接入共用合成用例的 Mac 适配测试，并在 Mac 真实 Chrome 上验证原生视频、已有 YouTube／B站回归、嵌套文档、接管与新轮、初始暂停、结束和停止／断线。仅构建或用例通过时继续保留真实验收 GAP。手机沿用 S25 已安装应用，不因桌面优化重签覆盖。

用 GitHub Issue 汇集跨端进展，正文链接仓库内共用规则和用例；具体平台的实施、环境和真实结果分别回链。规则在仓库内版本化，Issue 承担跟踪与讨论；不要在两个对话中各维护一份逐渐分叉的规则。发布前仍按主账号 MrPPFruit 核验，并排除本机日志、私人页面链接、媒体与凭据。
