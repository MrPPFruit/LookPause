## Context

复用已测试的通用 AX 媒体核心。当前只有固定循环；Android SDK36/JDK17 已有，S25 尚未连接。用户明确局域网直连、Mac 首次点一次允许，日常菜单不增加连接按钮。

## Goals / Non-Goals

目标是实际手机意图驱动媒体，而非扩大资格设施。非目标为全播放器兼容、Windows、云端图像或任意远程输入。

## Decisions

- [LPEXP-01] 同一 Swift 可执行目标提供普通菜单栏入口，AppKit NSStatusItem；SMAppService 登录启动。辅助功能不足显示状态，首次系统授权按 OS 流程。
- [LPEXP-02] 实验服务 _lookpause._tcp.，TLS 流式换行 JSON，最多 4096 字节/帧。Bonjour 提供证书指纹；首次按用户批准的 LAN 信任场景由 Mac 确认手机并签发随机凭据，后续凭据与证书固定。私钥/凭据入 Keychain/Android Keystore。首配属于 TOFU，不能宣称抵抗首配时主动中间人；正式远程/不可信网络不在本版本范围。固定本机 openssl 生成自签证书后删除临时私钥，TLS 协议由系统实现，不自造加密。
- [LPEXP-03] 持续意图裁决器串行拥有状态；暂停/恢复结果用定时观察，取消固定 20 秒回放。START 不播放，STOP/断线/超时清资格；会话与严格递增序号阻止旧事件。输入监听只在暂停所有权有效时启用；持有期间轻量核验上下文与控件，不反复枚举音频。
- [LPEXP-04] Kotlin/Compose 一页、NsdManager、系统 SSLSocket、CameraX + MediaPipe 本地粗粒度方向。开始后短暂基准，AWAY/NO_FACE/ATTENDING 去抖；UNKNOWN/相机错误不当作离开。前台摄像头服务从可见界面启动，退出/停止释放相机。无图像持久化或网络字段。
- [LPEXP-05] 先 Mac 固定意图真链、再模拟器界面、最后 S25 实际识别/控制；构建与模拟器结果不替代真机。

## Risks / Trade-offs

通用 AX 仍无法覆盖模糊/隐藏控件 → 返回未知，不误切换。页面内部换片/结束与平台竞态 → 仍有既有 GAP，不能宣称完整恢复保证。首配无扫码/输码 → 采用用户批准的 LAN TOFU，一次批准并固定后续身份。系统权限、未连接真机 → 保存可安装产物，必要时请求最少物理操作。

## Migration Plan

旧资格路线 SUPERSEDED，限制不写成 canonical 支持。唯一 integration 分支与阶段短分支整合；未通过 S25 验收不合 main。回滚停止接收端/手机服务并回到源码基线；不删除用户播放器资料。
