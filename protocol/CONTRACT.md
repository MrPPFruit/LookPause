# LookPause LAN 契约草案

状态：DRAFT，未冻结；没有 JSON Schema、fixture 或实现。基于原件第 5–6 节，不能直接据此宣称跨端兼容。

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
