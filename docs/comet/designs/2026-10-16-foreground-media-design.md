---
comet_change: foreground-media-feasibility
role: technical-design
canonical_spec: openspec
---

# 前台媒体验证实现决策

最小 Swift 原生探针使用固定 Apple Events、NSWorkspace 和 JSON 结果，避免网络和 UI 干扰核心机制。网页适配仅限 B站/YouTube 自动发现，不增加逐页确认。当前优先无扩展辅助功能读取状态，再以固定按键执行；有限权限扩展原型 DEFERRED、未安装，不开启 Chrome 全局 JavaScript 自动化。

首次系统权限由实际用户授权。权限不足返回明确错误；未知状态、前台变化、媒体变化或无手动干预可观察性时不自动恢复。测试视频取公开样例，只存在本地 .local；日志不含私密页面、凭据或人脸。

## Acceptance Design

- [LPMAC-01] Owner: 前台解析器；Observable: 只识别当前前台唯一媒体；Counterexample: 后台播放抢占目标；Evidence: 真实前台/活动页及切换记录。
- [LPMAC-02] Owner: 原生及网页适配器；Observable: 明确暂停/播放和进度保持；Counterexample: 成功回包但视频仍播放；Evidence: LookPause 实际调用和独立观测。
- [LPMAC-03] Owner: 会话裁决；Observable: 资格撤销后不恢复；Counterexample: 布尔所有权跨手动操作或目标变化；Evidence: 最小安全真实链。

未完成保持 active，不归档、不把资格工具当成菜单栏产品。回滚通过独立 Git 分支，不改变既有播放器设置。菜单栏、LAN、Android 和 Windows 为后续阶段。

## 2026-10-16 轻量通用 Pilot 迭代

用户确认以通用性与轻量优先，测试应用不定义支持名单。LPMAC-01/02 使用前台应用及聚焦窗口上下文、通用 AX 媒体控件，不按站点注入脚本；Core Audio 输出活动仅作辅助，不采集音频。LPMAC-03 将本轮操作记录与本机键鼠输入监听、上下文/状态变化关联；原型仅对观察到真实暂停转变的上下文尝试一次恢复，按键/点击/滚轮即取消（纯鼠标移动除外）。不要求补齐所有播放器状态后才体验，不搭评分系统或大规模测试设施。未知、歧义、监听失败仍不盲切换。固定 Pilot 命令模拟一次离开/回看，非任意按键接口、无网络；不进入普通手机或菜单栏入口。短轮询只辅助观察，不能证明未观察到的所有操作或媒体身份，因此结果按 Pilot 实测范围报告，不能称通用安全保证。
