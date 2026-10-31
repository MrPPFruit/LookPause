# Mac 快捷键兼容模式

用户明确确认采用Windows相同方案后实施，Mac build8。参考Windows独立分支提交[3ecc0c3](https://github.com/MrPPFruit/LookPause/commit/3ecc0c3ed4045058e1d6fffaa51f5c2ec4f985c1)，没有合并Windows源码或改Android／协议字段。目标MAC-SHORTCUT-01，父路线图关联LPEXP-03的通用观影能力，但不关闭原严格模式、S25及跨端未验收项。

## 实现与普通入口

Mac菜单新增“下次观影用快捷键兼容模式”，默认关闭，重启不保存。勾选状态只供下一次手机START读取；观影中切换不会改变当前owner。先播放视频并聚焦播放器，再在手机开始。开始零按键；首次离开绑定前台进程／窗口／文档／exact焦点祖先链，离开和回看各最多一次Space。UNKNOWN零按键，重复意图不重复提交；没有回退、重试、站点规则、浏览器扩展或新权限。

ShortcutSession独立于原MediaSession，MediaController在START路由，Receiver继续原认证／会话／序号／状态。原严格模式没有自动降级。输入观察复用InputWatch，健康检查增加tap启用状态及系统预检；随机轮次标记及本进程固定键豁免沿用原实现。绑定后观察失败、接管、前台／焦点／文档变化、停止、断线清除本次控制；迟到意图不能重授，必须重新START。纯鼠标移动不撤销。

Mac观察接受AXWebArea文档或原生窗口非空AXDocument，不读取播放控件／状态；拒绝编辑焦点及祖先、可写文本值、修饰键、不可见焦点、缺少文档证据、超时和输入监听失效。发送前重新观察，核验完整祖先／文档／窗口／进程关联及最终exact焦点，不抢焦点。观察1.5秒、连续观察2秒为移植初值，未实测冻结。

这是用户主动选择的保护例外：无法防止原本暂停、结束或同页换片媒体被Space误切换；系统提交也没有播放器执行回执。因此仅显示“已提交，效果未确认”，不声称Paused／Playing或已取得可靠暂停归属。严格模式既有保护不变，跨进程最终观察竞态仍存在。

## 检查与范围

- `swift build --configuration release --package-path apps/macos`通过。
- `swift build --build-tests --package-path apps/macos`仅编译通过，没有执行任何测试。新增两项不触发媒体的路由用例保留给后续获准检查：默认／下次START／STOP迟到意图、重建不记忆选择。不用编译冒充这些用例已经通过。
- 独立审查仅只读代码，覆盖owner隔离、pending消费、重复意图、UNKNOWN、停止／断线、整轮解除、exact焦点与输入豁免，未发现具体合入阻断；没有发送媒体输入。
- 本轮不执行self-test、播放器回归、S25联调、自启或权限重验。用户已明确日常体验后反馈问题，不要求用户中途测试。
- 新版沿用本机应用目录、com.lookpause.pilot、原签名与配对身份，Release产物原子替换并签名校验，普通进程重新启动；不需要重装手机。
- 尝试用CUA附着无窗口菜单应用时超时，无法取得完整展开菜单截图。菜单检查仅到代码层，未验证实际间距／完整文案／勾选呈现；不以截图不存在声称UI已验收，也未请求用户辅助点击。

反例仍开放：代码编译通过但真实播放器不响应Space，或原本暂停反而开始播放。该模式主动接受后一风险，前者由用户实际体验反馈；不能把Windows用例结果继承为Mac结果。变更保持active/pending，不归档为完整产品交付。

## Git整合

短分支codex/mac-shortcut-mode从3f7bd5f上的codex/experience-integration建立；实现及代码集成点在收尾记录补充。main不接收本轮变更；公开清单不包含安装包、原始日志、截图、媒体或配对秘密。
