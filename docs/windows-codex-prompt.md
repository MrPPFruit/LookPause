# 粘贴到Windows Codex的接续指令

适用入口：Windows上的**当前Codex对话框**，不是PowerShell，也不依赖Mac本机目录。下面整段复制一次即可；它让Codex获取资料并开始Windows阶段，不是已经存在的Windows安装命令。建议先按current-status完成Mac/Android收尾，再使用。若实际使用时仍有GAP，应如实保留，不自动判为完成。

```text
请在当前Windows会话接续开发PausePal。仓库是https://github.com/MrPPFruit/LookPause.git，完整基线分支是codex/experience-integration，不能从旧main开始。先检查当前目录、Git和是否已有该仓库；没有时克隆到可写的新目录，有则核对remote/分支/未提交改动并安全复用，不覆盖或清理用户文件。不要另建Codex项目或会话。

先依次阅读AGENTS.md、README.md、docs/product-spec.md、docs/decision-log.md、docs/development-plan.md、protocol/CONTRACT.md、docs/test-plan.md、docs/current-status.md、docs/windows-handoff.md和docs/desktop-status-icons.md。以现行决定和真实代码为准，历史报告不代表最新交付。Mac/Android源码和协议已有，Windows尚无实现，现有ICO只代表素材。先报告仍未收尾的Mac/Android事项及其对Windows的影响，不宣称它们已完成；保留已有active/GAP记录。

本轮主目标是Windows轻量后台接收端，在Windows开发和真实测试。按交接文档建立本平台OpenSpec验收与tasks，从最新integration创建codex/windows-receiver开发分支，若已有该分支则先安全核对续做。先检查系统/架构、Git和.NET等环境，先证明GSMTC或通用UI Automation能识别唯一前台媒体并安全暂停/恢复，再接现有LAN体验v1与托盘，按阶段推进。优先复用系统能力与已有规则，不逐站适配、不装浏览器扩展、不改网页脚本权限、不增加通用远程输入接口。

必须保持：START不播放，只恢复本工具已确认暂停且资格有效的同一媒体；手动接管、换片、结束、停止、断线或切电脑撤销；暂停期间UNKNOWN保留原归属并继续监测，明确回看才恢复。回退统一先确认恢复播放，再原生时间线优先或安全焦点下一次Left，不重试、不影响暂停恢复。托盘用已确认的四态图标、状态、登录启动开关和退出；任何未确认的新交互或默认行为先说明获得确认。首次配对/系统权限保留安全边界。

Android主开发机仍为Mac，先用S25现有安装包联调，不在Windows重新签名/覆盖手机包，不复制或上传Mac密钥、手机配对凭据、日志、私人链接或测试视频。能自动完成的实施和真实播放器测试自动推进，不让我中途反复测试；确实需要系统授权或实体手机动作时再说明，完成后给一次集中验收清单。GitHub写操作前确认gh api user --jq .login为MrPPFruit；按阶段提交并推送开发分支，记录集成点，不因局部通过合main。静态检查、构建、资格测试、普通入口和S25真机结果分别报告。先给出环境事实与当前唯一目标，再执行已确认范围内的工作。
```
