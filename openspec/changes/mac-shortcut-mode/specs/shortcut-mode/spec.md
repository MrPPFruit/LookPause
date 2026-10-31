## ADDED Requirements
### Requirement: MAC-SHORTCUT-01 显式快捷键兼容
Mac SHALL 默认使用严格模式；选择兼容模式只影响下一START，重启不记忆，开始不发键。兼容模式离开／回看分别最多一次Space，不验证状态、不回退、不重试。
#### Scenario: 绑定与解除
- **WHEN** 用户选择兼容模式、聚焦正在播放的视频并在手机开始，随后离开
- **THEN** 绑定实际前台进程／窗口／文档及exact焦点链，通过权限／非编辑／修饰键／输入健康核验后提交一次Space，状态明确效果未确认。
#### Scenario: 强撤销
- **WHEN** 首次绑定后接管、切换焦点／窗口／文档、观察中断、停止／断线或提交失败
- **THEN** 解除整次控制，迟到意图零输入，必须新START；UNKNOWN不创建资格，重复离开／回看不重复发键。
#### Scenario: 正常模式
- **WHEN** 用户未选择兼容模式
- **THEN** 沿用MediaSession，不因未知播放状态自动切入兼容路径。
