# LookPause LAN 契约草案

状态：体验协议已实现，正式协议仍未冻结。下文“体验协议 v1”为本阶段有效契约，已有 schema、fixture 与双端实现；前部候选仅保留历史背景，不能据此宣称 Windows 跨端兼容。

现行语义约束：手机主动连接并发送意图，桌面自动识别前台媒体并核验播放状态；单会话只控制一台电脑及一个当前合格媒体，不要求手机或用户选择 targetId。START 不改变播放；PAUSE 成功且确认归属后才产生 pauseOwned；PLAY 需要同媒体、同前台上下文和有效所有权；手动干预、前台上下文/媒体变更、停止、断线、切换电脑撤销资格。未知或多义目标不操作。取消显式绑定不取消内部目标关联。

候选包络：v/type/id/sessionId/seq/payload；候选类型 hello/auth/command/ack/status/ping/pong/error；候选操作 START/STOP/PAUSE/PLAY/GET_STATUS。ACK 关联请求且应反映真实验证结果；重试同 id 幂等，旧序号拒绝，过期 PLAY 不离线补发。字段必填性、关联规则、序号作用域和缓存策略待定。

候选错误：UNAUTHORIZED、INVALID_SESSION、STALE_SEQUENCE、TARGET_CHANGED、NO_SUPPORTED_PLAYER、PLAYBACK_STATE_UNKNOWN、PERMISSION_DENIED、UNSUPPORTED_OPERATION、COMMAND_FAILED。尚须冻结错误载荷和失败/重试语义。

原文服务类型 _gazepause._tcp.、协议名 GazePause LAN Protocol v1、端口 48761、WSS 与公钥指纹配对均为来源方案；LookPause 更名及发现标识须统一决定。不要宣称协议已冻结。

冻结前必须补齐：

- 配对挑战、二维码内容、凭据签发/刷新/撤销、重配对、身份校验、TLS 信任及认证失败报文。
- hello/auth 的完整方向、字段、时序、版本/能力协商。
- START/STOP、前台自动目标识别/变化、会话建立/结束/超时/重连/切换、撤销授权的生命周期消息与状态迁移；targetId 由桌面生成，手机不指定播放目标，旧目标的 PLAY 必须拒绝。
- ACK/status 错误结构、目标身份/播放项版本、pauseOwned 产生与撤销、并发/竞态处理。
- 心跳、超时、幂等、去重存活期、乱序和旧会话拒绝的可验证规则；原文 5s/15s 为初值。
- schemas、正常及错误 fixtures、三端契约测试和冻结决策记录。

仅开放固定媒体操作；禁止通用远程输入/命令执行。凭据使用系统安全存储，日志不含秘密或图像。

轻量通用 Pilot 目前只在本机固定测试入口模拟离开/回看，不新增 LAN 消息、远程按键或任意执行接口，不以本轮原型结果冻结以上协议。

## 体验协议 v1（本阶段采用，替代以上未冻结候选）

服务 `_lookpause._tcp.`、动态端口；Bonjour TXT 为 v=1、id=电脑 UUID、fp=TLS 叶证书 SHA-256 十六进制。链路为系统 TLS1.2+ 的 UTF-8 JSON 换行帧，每帧含换行最多4096字节，不使用通用远程按键或任意执行字段。共享字段见 pilot-v1.schema.json，样例不含真实凭据。

首次手机在可信家庭 LAN 发现并选中电脑，连接时固定发现的证书指纹；Mac 点一次允许后签发32字节随机 token。之后手机固定电脑 id/证书并以保存 token 认证，禁止静默接受身份变化。首配是用户批准的 TOFU，不保证首配时抵抗主动中间人；不可信网络不在此体验范围。Mac 私钥/token 在 Keychain，Android token 以 Keystore 密钥加密保存；不记录日志。

hello 含 deviceId/name，已配对时还含 token；ready 含新 session，首次另含新 token。每次 TLS 连接重新 START，START 不播放；手机只发送 start/stop/intent/ping，seq 为会话内严格递增正 Int64，旧/重复序号拒绝且零副作用，不重试 PLAY 或旧意图。intent 为 ATTENDING/AWAY/NO_FACE/UNKNOWN。NO_FACE 已由手机去抖；相机故障发送 UNKNOWN 并停止，不能伪装 NO_FACE。

ack 的 ok 只表示请求被认证和接收，不能当作播放器操作成功；其中 status 可能仍为“正在确认”。最终 status 为裁决器实际观察结果，不包含媒体地址/文件名、图像或面部特征。未开始、旧会话、未知操作、未批准设备均拒绝。心跳初值2秒，已认证连接6秒无有效事件即撤销并断开；参数待 S25 实测。STOP、断线、超时不播放，清除所有权；新连接不继承。一次只有一台已认证手机可观影。

Mac暂停期间按键/点击/滚轮、前台/可观察状态改变、输入监听丢失或观察中断取消恢复。通用 AX 对页面内部换片/结束仍有已记录缺口，不能因网络协议完整而宣称媒体全覆盖。

输入边界：每连接每秒最多20帧、最多4个待认证/认证连接；手机发现列表和待解析队列各最多32项。状态及意图诊断只写本机固定枚举/裁决文案，不含手机名称、会话、令牌或媒体地址。Android 每次开始生成独立本地识别轮次，旧轮次异步结果/故障/结束回调不能作用于新轮次。
