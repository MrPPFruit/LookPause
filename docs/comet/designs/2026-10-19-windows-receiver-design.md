---
comet_change: windows-receiver
role: technical-design
canonical_spec: openspec
---

# Windows 实现决策

首个实现是只读 GSMTC/UIA 资格探针，验证系统可观察面，之后才选择执行接口。不把应用名、窗口标题或系统推荐会话当唯一媒体身份。UIA 可用时优先操作已观察的具体前台控件；不能证明绑定时不退化为全局媒体键。

归属裁决单 owner：记录窗口/活动文档/控件 runtime identity、媒体上下文与暂停位置。低层输入和前台变化递增撤销代次，副作用前重读，已暂停 UNKNOWN 继续监测。原生 API 最终竞态保留为边界。确认时间和精度在 Windows 现场确定。

后续网络只传固定意图，身份本机独立生成并以当前用户 DPAPI 保护。托盘不拥有媒体状态。高风险认证和输入边界有独立审查。实际入口与探针分开取证；相同可观察值换片不能靠更多测试声明解决。

Mac 路径的 canonical OpenSpec skill 在本机不存在，使用已安装 OpenSpec CLI 模板创建和验证 artifacts；不伪称执行不存在的 skill。Ponytail 未安装，以独立方案审查检查复杂度，不虚报已调用。
