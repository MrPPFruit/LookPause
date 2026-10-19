## Why

PausePal 尚无 Windows 接收端。当前 Windows 会话从 integration@c030577 开始，先证明前台媒体安全控制，再让现有 S25 应用连接普通托盘入口；Mac/Android 的既有 GAP 不随迁移关闭。

## What Changes

- 新增 Windows 原生媒体观察、暂停归属裁决与有界恢复。
- 验证后接入体验协议 v1、Windows 独立安全身份及四态托盘。
- 区分构建、自动化、真实播放器、普通入口与 S25 验收。

## Capabilities

### New Capabilities
- `windows-receiver`：Windows 前台媒体、局域网接收与托盘生命周期。

### Modified Capabilities
无。共享协议不扩展，Mac/Android 不改动。

## Impact

负责目录 apps/windows，以及本阶段 OpenSpec、Windows 报告和状态入口。依赖系统 UI Automation、GSMTC、.NET 桌面运行时；系统权限不自动扩张。开发分支 codex/windows-receiver，目标 integration，禁止合 main。
