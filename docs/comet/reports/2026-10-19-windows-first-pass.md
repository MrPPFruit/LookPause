# Windows 首轮事实与接续点

日期：2026-10-19。Windows 功能开发未完成，不进入产品集中验收，不关闭 Mac/Android 活动项。

## 基线与环境

工作目录原为空，安全 clone 指定单分支 `codex/experience-integration`，HEAD 为 c0305778235cd43e74c335c369720123a90e186b，未使用 main。远端无 windows-receiver 分支，创建本地 `codex/windows-receiver`。未覆盖或清理用户文件。

实机 Windows 10 专业版 22H2/10.0.19045/x64；SDK 10.0.302，桌面运行时 10.0.10。Windows SDK 本机旧版本 10.0.16299；构建由 .NET 官方 Windows 引用包还原。Git 可用，gh 不在 PATH，未执行任何 GitHub 写入，未改变防火墙或系统权限。

沙箱账户与 clone 后文件所有者不同，普通 Git 触发 dubious ownership。检查时使用仅当前命令的 safe.directory，需当前用户操作则用经批准的工具执行，不添加全局信任例外。OpenSpec 在沙箱身份下无法访问用户安装路径，经当前用户上下文实际运行成功。继承环境存在 NODE_TLS_REJECT_UNAUTHORIZED=0，后续 OpenSpec 命令局部改为 1，没有修改全局环境。

## 未收尾项

Android Lint 1错误/17警告（AND-CLOSE-01/02）与 Mac 专用连接文案仍归 Mac Android owner；不改手机签名/安装包。新版 S25 集中动作验收、Mac 同观察值换片/最后操作竞态、登录后实测仍保持 GAP。锁屏/长时间/耗电保持 DEFERRED。

## 已执行与结果

- 完整读取接续入口、现行规格/决策/协议、状态和交接文档；建立 Windows 独立 OpenSpec change，未归档旧 change。
- 独立只读方案审查已完成：GSMTC 当前会话/应用名不能证明前台；UIA RuntimeId 不是播放项版本。必须关联唯一可见 Document、媒体容器、控件和时间线，监测先于采样，副作用前复查撤销代次。UIA 不能阻塞托盘，扫描与过期结果要有界。
- 新增只读 GSMTC/UIA 探针。`dotnet build apps/windows/PausePal.Windows -c Release --nologo` 实际成功，0警告/0错误，目标 net10.0-windows10.0.19041.0。
- `openspec validate windows-receiver --strict --no-interactive` 实际通过。
- 探针尚未实际运行；没有播放器状态、暂停/恢复或进度证据。没有编写媒体控制或把构建当资格通过。下一步先修复下述测试工具阻塞，再实际探针/一条控制链，通过前不推进网络托盘。

## 实测阻塞

已初始化官方 computer-use 工具并取得当前桌面窗口清单。请求 Chrome 只读界面状态时，工具返回：无法以足够置信度确认当前浏览器 URL 来执行安全策略，Computer Use 本轮已停止，要求停止工作并说明原因。此后未再操作桌面或换工具绕过浏览器安全检查。该错误来自测试工具，不是 PausePal 的播放器判态或系统授权结果，也不能推断用户系统需要开启任何新权限。

上述为首轮阻塞，后续进展见末节。当前尚不需要实体手机动作。未新增产品 UI，因此没有可供审查的 Windows 托盘截图。未生成安装包、配对凭据或手机日志。

## 工作流可用性

已使用 Comet 并建立唯一 tasks。Comet 引用的 Mac canonical OpenSpec skills 在本机不存在，实际使用已装 OpenSpec CLI 创建/验证，未假称执行不存在的 skill。Ponytail 未安装，独立审查使用 lite 的复杂度尺度，没有虚报调用。Windows change 仍停留 design/pending，不能标记 complete。

## 交付界限

LPWIN-01 未通过；LPWIN-02、LPWIN-ICON-01、LPWIN-03/04 尚未实施。只有构建和规格验证通过。产物在 apps/windows/PausePal.Windows/bin/Release/net10.0-windows10.0.19041.0，为资格探针而非接收端。后续仍以 windows-receiver/tasks.md 为唯一计划，完成真实媒体安全证明后才能接手机协议和托盘。

## 后续配置与探针运行

用户授权自主完成可配置项。根据仓库既有提交，将本项目 user.name 配为 MrPPFruit、user.email 配为 49444331+MrPPFruit@users.noreply.github.com，未改全局作者身份。官方 GitHub CLI 2.102.0 便携版下载到忽略目录 .local/tools/gh，下载 SHA-256 与 GitHub 官方发布资产 digest 一致；未改系统 PATH。已连接 GitHub 插件实际返回 MrPPFruit/id49444331，但不代替 CLI 核验。CLI 初始未登录，已启动官方 device flow，请用户自行完成浏览器身份与授权；未代填认证信息。推送仍须实际执行 gh api user --jq .login。

浏览器扩展连接已恢复，成功创建独立公开视频测试标签并读取播放器语义；没有修改浏览器安全设置。后续浏览器测试使用该正式连接，不绕过上一轮已停止的工具上下文。

探针首次运行实际发现 nint 无法直接 JSON 序列化，转换为 Int64 后重跑；诊断原先的子串匹配还会误取 PausePal 按钮。根因是平台数据没有先归一化、诊断语义匹配过宽，说明构建不能代替实际执行。现已完整匹配通用媒体语义，仅输出 PLAY/PAUSE/TIME 等枚举及数值，所有文档名称只在内存计算哈希，不输出标签。新增有界遍历与完整性标记；单次 UIA 调用可能阻塞，因此另有8秒子进程上限。独立审查确认 RuntimeId 只作作用域关联，不能当播放项版本。

修订后实际构建0警告/0错误；探针成功执行，读取 GSMTC 的 chrome.exe 暂停会话和前台 Document，扫描188节点、235ms、前台前后未变，incomplete=false。此时没有可判定媒体控件，因此没有媒体资格结论，更没有控制副作用。认证页面的正文、URL、代码或凭据不进入探针输出。不能把后台 GSMTC 状态当成前台视频证据。

GitHub 登录由用户完成期间不切换其前台页面。下一步在授权完成后核验 CLI 账号并继续独立播放器标签的实际探针；网络/托盘仍按任务门槛推进。
