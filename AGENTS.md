# 项目工作指引

先读 README.md、docs/product-spec.md、docs/decision-log.md、docs/development-plan.md、protocol/CONTRACT.md、docs/test-plan.md；需要完整背景时读 docs/source-index.md 指向的原件。

现行决策优先于定名前原件；原件只读保留，不直接改名或修订内容。正式显示名称 PausePal（2026-10-18 用户确认，原 LookPause）；保留现有工程目录、com.lookpause 安装/配对身份与 _lookpause._tcp. 服务标识，原文 GazePause 只作历史；协议尚未冻结。

Windows 在 Windows 开发及真实测试，Mac 在 Mac 开发及真实测试。Android 主开发机已确认为 Mac，避免两台主机同时改同一工程；Windows 阶段优先复用 S25 已安装应用，不在另一台主机另签 APK 覆盖。平台任务默认只改负责目录；共享协议变更先更新契约、样例与兼容测试，并记录跨端影响。

必须区分静态检查、构建、自动化测试和真机实测；记录命令、结果、环境版本、失败项。不能以构建通过声称播放器或三星设备可用。无法确认播放状态或暂停所有权时禁止自动恢复；手动操作、换媒体、断线及切换目标撤销恢复资格。

不上传或默认保存人脸图像/特征，不记录密钥令牌，不增加通用远程按键、shell、脚本或文件读取接口。

2026-10-16 用户已启动后续开发：可在 Mac 上实施、构建及测试，建立本地 Git 基线并在 MrPPFruit 下创建 GitHub 公开仓库及推送。Android 主开发机为 Mac，目标设备按用户提供为三星 S25 / Android 16 / One UI 8.5；已安装配对并完成部分真实联调，系统/One UI 版本未独立读取核验。不要登记 Codex 项目或新建开发会话。

2026-10-19 Windows 交接：当前完整开发基线在 codex/experience-integration，main仍为早期基线，不能从main误建依赖分支。先读 docs/current-status.md 和 docs/windows-handoff.md；Windows尚无接收端实现，素材不代表支持。交接准备不把Mac/S25剩余验收自动判为完成，也不归档现有active变更。Windows新阶段应由用户在Windows会话启动，环境和API可行性先验证，按交接文档建立本平台验收与集成记录。

现行交互：手机主动选择并连接一台电脑；桌面被动接收。自动识别前台媒体，不要求用户逐页授权、手动选视频或绑定媒体。内部仍须保持精确的目标身份与恢复资格，不能把前台空格键响应当作可靠播放状态。菜单栏日常仅显示状态、登录后启动开关、退出；登录后启动默认开启。首次系统权限及设备配对不属于逐页授权，必须保留安全边界。新增交互仍须说明并获用户确认。

真实验证使用用户 Chrome 中的 B站、YouTube 和本地视频。原始资料、本机日志、测试媒体、私人会话链接与账号信息不上传公开仓库。先核验公开文件清单再提交和推送。
