## Why

当前方案需要用户选媒体并逐页授权，与“连接手机后自动控制正在前台观看的视频”冲突。先验证真实媒体控制与恢复安全性，避免用空格键或浏览器工具成功替代产品能力。

## What Changes

- 建立 Mac 前台自动识别及真实媒体探针，覆盖 QuickTime、Chrome B站与 YouTube。
- 消除用户显式绑定流程，内部仍记录暂停目标与恢复资格。
- 权限不足、多目标歧义、手动干预或前台变化时拒绝恢复。
- 本阶段仅技术资格路径，不实现菜单栏产品、LAN 或手机视觉。

## Capabilities

### New Capabilities

- `foreground-media`: 前台唯一媒体识别、明确状态与操作、恢复资格安全。

### Modified Capabilities

无现存 canonical capability。

## Impact

apps/macos、必要浏览器适配、现行规格、测试证据。优先系统工具，无新增模型或付费服务。公开 Git 排除原件、私人链接、日志与测试媒体。

## Delivery Contract

- Parent outcome: docs/development-plan.md#第一阶段前台媒体可行性active
- Parent acceptance: independent
- [LPMAC-01] Outcome: 无手动绑定即可唯一识别当前前台视频；Negative: 后台视频在播放就被误认成前台；Evidence: QuickTime 前台文档、Chrome 前台窗口/活动页/视频与切换真实记录。
- [LPMAC-02] Outcome: 本地及 B站/YouTube 真实状态、明确暂停/播放且进度正确；Negative: 命令成功但视频仍播放或暂停重置进度；Evidence: LookPause 自身调用前后状态、时间推进及真实页面核验。
- [LPMAC-03] Outcome: 仅恢复本工具暂停且未被撤销的同一前台视频；Negative: pauseOwned 为 true 但用户已手动暂停或换页仍恢复；Evidence: 原本暂停、手动干预、前台/媒体变化、结束及权限拒绝的最小真实链。
