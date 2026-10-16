## Why

当前只有本机定时 Pilot，用户无法用手机真实体验。用户已确认先做 S25→Mac 的最小闭环，将完整播放器兼容性留待后续；不再用旧阶段全部通过阻塞体验。

## What Changes

- Mac 菜单栏被动接收端，状态、默认登录启动开关、退出。
- 局域网发现并直连，首次 Mac 点一次允许，之后保存设备；无扫码/输码。
- 复用通用媒体机制，以持续意图驱动，停止/断线/手动接管取消恢复。
- Android 最小界面、前摄本地观看方向判断及必要前台服务；不传图像/特征。
- 旧 foreground-media-feasibility 的未通过项明确 SUPERSEDED/DEFERRED，不导入为已完成能力合同。

## Capabilities

### New Capabilities

- `mac-phone-experience`: 手机发现配对、Mac 菜单栏、持续意图裁决、本地视觉及真实体验。

### Modified Capabilities

无已归档 canonical 能力。复用旧实现但对整链重新承担验收，不以旧阶段通过为前序交付假设。

## Impact

apps/macos、apps/android、protocol 和必要验证文档。Windows 不变；无扩展、网页脚本、付费服务或通用远程输入。TLS 固定 JSON 意图，双方保存配对凭据。

## Delivery Contract

- Parent outcome: docs/development-plan.md#手机到mac体验阶段
- Parent acceptance: independent
- Root reconciliation: 本变更是用户重新确定范围后的整体验收根节点，替代未完成的旧资格路线；从 1455963 导入实现作为待重新验证的源代码，不依赖旧 change 的交付结论。媒体裁决、真实控制与手机链路的责任均在本变更，不能以旧记录解锁验收。
- [LPEXP-01] Outcome: Mac 普通启动后菜单栏可查看状态、切换登录启动并退出；Negative: 只有命令行探针可用；Evidence: 安装包正常启动、截图、状态和生命周期。
- [LPEXP-02] Outcome: 手机发现选中 Mac，首次批准后可信连接，后续无需重复配对；Negative: 任意 LAN 客户端可控制或失效凭据被接受；Evidence: 真 TLS/认证、拒绝、持久化和重连。
- [LPEXP-03] Outcome: 持续离开/回看意图实际控制前台媒体，手动暂停/接管/停止/断线不误恢复；Negative: 定时模拟成功但真实意图不起作用或旧事件补播；Evidence: 产品入口真实播放器链与撤销场景。
- [LPEXP-04] Outcome: Android 本地识别并只发粗粒度意图，前台服务可在必要后台场景继续；Negative: 上传图像或只有假意图；Evidence: 模型真实推理、权限/服务生命周期与网络负载检查。
- [LPEXP-05] Outcome: 三星 S25 实际完成观看→离开→恢复及手动接管体验；Negative: 模拟器/构建通过被当成三星结果；Evidence: 真机型号/版本、实际步骤和播放器输出。
