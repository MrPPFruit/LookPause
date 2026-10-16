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
