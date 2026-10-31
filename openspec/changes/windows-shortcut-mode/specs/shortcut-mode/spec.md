## ADDED Requirements
### Requirement: 显式选择及风险区分
Windows SHALL 默认使用正常模式；用户可在托盘选择下一次观影的快捷键兼容模式，重启不保留选中。兼容模式不识别播放控件和状态，不承诺暂停归属，文案 SHALL 标明效果未确认。
#### Scenario: 开始及切换
- **WHEN** 用户选择兼容模式并在手机开始观影
- **THEN** 开始不发键，该轮固定使用选定模式，后续切换只影响下一次开始。

### Requirement: 有界快捷键及撤销
兼容模式 SHALL 仅在同一实际前台、同进程、同文档及非编辑焦点、无修饰键、输入观察健康时，离开一次Space及回看一次Space。UNKNOWN不发键，重复意图不重复发键，不回退不重试。
#### Scenario: 未确认效果
- **WHEN** 系统接受Space
- **THEN** 仅报告已发送效果未确认，不当作明确Paused或Playing。
#### Scenario: 接管与迟到
- **WHEN** 首次绑定后输入、切窗、焦点变化、停止、断线、超时、失效或提交失败
- **THEN** 解除本次快捷键控制，迟到意图不发键；必须新START后才能重新绑定。
#### Scenario: 未知期间
- **WHEN** 一次离开已提交后收到UNKNOWN再回看
- **THEN** UNKNOWN不执行操作且不重授；边界仍有效时只消费原pending一次。
