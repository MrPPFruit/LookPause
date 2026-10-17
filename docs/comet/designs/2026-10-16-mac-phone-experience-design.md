---
comet_change: mac-phone-experience
role: technical-design
canonical_spec: openspec
---

# S25 到 Mac 最小体验

复用通用 AX 核心，单一 MainActor 裁决器拥有本轮媒体资格；网络层只认证与传递固定意图，不持有播放器状态。菜单栏 AppKit，手机 Compose/CameraX/MediaPipe，本地视觉，无云端图像。TLS 换行 JSON、会话与严格序号，最多 4096 字节；第一次 LAN TOFU 由 Mac 批准，后续固定证书/凭据。私钥与凭据用系统安全存储；不开放任意按键、脚本或文件读取。

原型普通入口不保证全播放器兼容，未知不操作；同页面内部换片与最终操作竞态保留风险，不把旧资格阶段记录当作已完成产品。Android 基准与去抖为待真机调参的初值。回滚仅停止新接收端与手机服务，保留用户资料。测试先核心/接口、一条真播放器链，再真实手机；模拟器不替代 S25。当前旧路线被替代，新根 change 自身承担全部依赖责任，无已完成父交付假设。

## Acceptance Design

- [LPEXP-01] Owner: Mac AppDelegate；Observable: 菜单栏普通启动/状态/登录启动/退出；Counterexample: 仅 CLI 可达；Evidence: bundle 正常启动与截图。
- [LPEXP-02] Owner: 接收端会话；Observable: 发现、一次批准、可信重连；Counterexample: 未配对设备能控制；Evidence: 双端真实 TLS/拒绝/持久化。
- [LPEXP-03] Owner: MediaSession；Observable: 持续意图实际控制并取消旧恢复；Counterexample: 固定计时器代替视线；Evidence: 产品媒体输出及撤销。
- [LPEXP-04] Owner: Android WatchingService；Observable: 本地推理/服务/只发粗粒度意图；Counterexample: 假数据或传图；Evidence: 相机/模型/网络与服务生命周期。
- [LPEXP-05] Owner: 真机体验验收；Observable: S25 真实闭环；Counterexample: 模拟器冒充三星；Evidence: 型号/版本/真实步骤/播放器输出。

2026-10-17 [LPEXP-03] 用户确认回退是可选补偿，不能妨碍暂停/恢复。不猜测Left含义，不逐站适配；只对可核验原生秒时间线做有界写入，能力不足即跳过。唯一调用方MediaSession在确认恢复成功后尝试一次；写入返回真实前值作为验证基准，无能力/失败不改变播放。详细条件与场景以现行change spec/design/tasks4.1～4.2为准。
