# 设计

MAC-SHORTCUT-01由MediaController在START时固定本轮owner（MediaSession或ShortcutSession），STOP及断线同时释放两者。Receiver继续消费统一status/isWatching/lastIntent，网络与图标投影不变。菜单只改内存中的下次选择，不中途换owner。

ShortcutSession独立保存绑定边界与pending，首次离开创建InputWatch；保留原随机标记的Space豁免。AX只读实际前台窗口、文档及exact焦点到窗口的完整祖先链，不找媒体控件、不抢焦点。文档可为AXWebArea或原生窗口非空AXDocument；拒绝编辑焦点／可编辑文本祖先、修饰键、不可见焦点、无文档证据和超时。观察预算1.5秒、连续采样间隔上限2秒，固定Space发送前重新观察并复核最终exact焦点。数字为移植初值，未通过本轮实测冻结。

每轮绑定后持续观察；任何失败解除，不重新授予，直到新START。回看先消费pending再发送，失败不重试。UNKNOWN无输入但保留有效pending，STOP清空。没有原生提交成功回执，状态仅称“已提交，效果未确认”。严格媒体核心不改。
