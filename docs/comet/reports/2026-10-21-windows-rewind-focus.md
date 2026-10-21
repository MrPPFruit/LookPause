# Windows 回退键盘目标与焦点确认修正

日期：2026-10-21；分支`codex/windows-receiver`；版本0.1.5，接续0.1.4及此前未提交的本轮诊断。没有改Mac、Android、协议或配对身份。

## 结果与根因

用户原网站在最终实现下完成三轮本机技术链：自动暂停、UNKNOWN保持、恢复、一次Left回退。随后独立确认视频处于Playing、readyState=4且无媒体错误，UIA焦点位于播放器区域；空格暂停／播放及左右键定位也正常。此前0.1.4普通托盘的“已尝试”日志只代表进入执行路径，不能当作回退有效证据。

根因属于把一种媒体控件的焦点当作所有媒体键的共同执行能力。原Windows路径把Left发给播放按钮；本机原生视频中，系统接受了事件但未发生seek。将焦点留在进度滑块虽然能回退，却改变空格语义，不能作为最终方案。最终使用已经验证的媒体区域作为键盘承载候选，核验可聚焦并等待确切焦点；不重新寻找祖先，不按名称或位置猜目标。

还确认聚焦有异步完成：SetFocus返回后的即时比较曾拒绝，稍后系统焦点已到达目标。现在等待明确就绪条件，仍从原ObservedAt计算1500ms预算，每次UIA读取前后核验资格；不是固定睡眠后盲发，也不延长采样时限。同步UIA仍无法被硬中断，超时只能在返回后拒绝。

## 实现边界

- `UiMedia.KeyboardSurface`只引用原来已验证的scope；身份已有scopeId和文档链。它不等于任意Group必然具备视频键盘语义。
- 承载候选须可聚焦、启用、非Offscreen且bounds非空。保留编辑区、文档链、输入代次、实际前台及修饰键保护；SetFocus最多一次。无能力就跳过回退，不降级到播放按钮或seek。
- 焦点确认后再次读取媒体，核验Playing、未结束、同目标／时间线和同一承载面；最终exact焦点与资格仍按原采样预算检查。
- 固定Left仍只提交一组按下／释放，未改变原编码。部分提交只清理释放，不补发按下；清理先于日志，避免诊断异常跳过释放。已尝试原生时间线写入后不补Left。
- 新增固定阶段、跳过原因、SendInput插入数量及失败码诊断，不记用户按键、页面名称、URL或凭据。[SendInput返回值](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)只证明事件进入系统输入流，不证明播放器完成回退。

## 分层验证

环境：Windows 10 Pro 22H2 x64 / 10.0.19045，Chrome155.0.8059.85，.NET SDK10.0.302／运行时10.0.10。

| 层次 | 命令／方法 | 实际结果 |
|---|---|---|
| 自动回归 | `dotnet run --project apps/windows/PausePal.Windows.Tests -c Release --no-restore` | 108/108；新增就绪条件、读取期间撤销／耗尽预算、等待期间撤销、超时与异常等回归。测试替身不触达媒体 |
| 发布 | `dotnet publish apps/windows/PausePal.Windows -c Release --no-restore -o .local/windows-0.1.5 --nologo` | 成功，零警告／错误；整个目录为框架依赖测试包 |
| 静态与复核 | OpenSpec严格验证、git差异检查、方案及最终只读复核 | 无阻断；保留原安全资格，没有焦点／按键重试或额外MediaSession状态 |
| 原策略基线 | 本机Range服务中的MDN CC0原生样片，`rewind-qualification.py --scenario normal --cycles 1` | 系统插入2项、size40、VK37；页面记录keyup但没有后续seek，位置继续前进，未回退 |
| 编码对照 | 同焦点，仅补扫描码及扩展标志 | 页面code成为ArrowLeft，仍未回退；实验撤回，未作为修正交付 |
| 滑块对照 | 同原编码，seek焦点及就绪确认 | 三轮实际seek均发生，样片每次约0.05055秒；但焦点留在seek会改变Space行为，故实验方案撤回。它不是最终版本的通过证据 |
| 最终网站实测 | 同一实际Chrome前台，`rewind-qualification.py --scenario normal --cycles 3`，最终媒体区域焦点实现 | 暂停位置约118、99、81秒，每轮恢复后一次Left；最终独立DOM约68.93秒、paused=false、readyState=4、error=null。UIA焦点为媒体区域而非seek |
| 随后键盘行为 | 浏览器工具对仍聚焦的同一video发标准Space及Left/Right，只读实际属性 | Space能暂停、恢复；暂停态Right与Left分别定位后返回原位置。收尾保持手动暂停，最终往返定位后曾重新缓冲，不计长时间稳定性 |

本机隔离样片使用[MDN公开CC0视频](https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4)，测试服务只监听回环且支持HTTP Range；不支持Range的普通临时服务曾暴露seekable=0，不能用于回退能力验证。网页脚本只用于隔离测试页展示事件，产品没有网页脚本或浏览器扩展依赖。原用户网站、标题、原始日志、截图及媒体不上传。

网页仍是一次Left，实际步长由播放器决定。此网站约1%时长、约18秒，与同页Right对照一致，不是固定五秒。Mac的原生秒回退与网页一步合同不变。用户网站的最终链来自本机pilot，不是S25普通托盘验收；此前108项回归也不能替代实体手机。

独立包`.local/windows-0.1.5`中`PausePal.Windows.dll`的SHA-256为`47129E121EE748EEC458F08C300C23DD678D3DCF670EB5A31C13A4C5F34F9B17`。运行中的托盘仍为0.1.4，未强制终止；用户需正常退出旧托盘再启动新包，配对身份保留。测试服务和临时驱动完成后清理，原用户标签保留在原网站。

## 剩余项与Mac影响

控制栏隐藏后的能力、暂停后时间描述一致性、输入框及完整接管保护的真实矩阵、YouTube/B站/本地、真实S25普通入口继续保持开放。原生焦点候选在该网站可用，不承诺任意可聚焦Group都有相同语义；最终普通输入API仍存在观察到动作的竞态。

Mac应核对动作目标与键盘焦点的实际语义，补Left之后Space／方向键行为；不能直接复制UIA Group、Windows句柄或等待时限。Mac本轮未修改，继续通过[共用规则](../../media-control-policy.md)、[Mac接续](../../macos-media-optimization-handoff.md)及[Issue #1](https://github.com/MrPPFruit/LookPause/issues/1)跟踪。不合main、不归档活动变更。
