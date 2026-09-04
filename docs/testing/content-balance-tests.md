# 内容与数值验收

## 定义验证

- 稳定 ID 非空且唯一
- 所有 Pair 的 Resource 非空
- 同一列表无重复 Resource
- 所有金额非负
- Building costGrowth > 1
- 时代跃迁研究标记正确
- 资源有来源和用途

## 可达性

从新游戏开始静态推演：

- 原始主线可完成
- StoneAgeSettlement 可完成
- 石器资源链可建立
- FeudalAdministration 可完成
- 中古时代可进入

失败时打印完整阻断路径。

## 数值模拟（冻结，仅作诊断参考）

`tools/EconomySimulator` 当前已封停，不能生成或覆盖现行报告。仓库中保留的
`data/economy-simulation` 文件是历史/诊断快照，不是当前玩法、节奏、平衡或
Unity 验收证据。不要根据这些输出调数值；优先使用真实 Unity PlayMode、运行日志
和玩家实玩记录。

模拟器相关的旧时间窗、帧率和策略场景暂不作为验收门槛；不要运行它来
生成当前节奏或平衡结论。需要验证这些行为时，使用 Unity 的 `ManualTick`
和真实运行时状态，在 PlayMode/Editor 测试中记录输入、输出与日志。

## PlayMode

Story regression coverage must also verify the fixed 18-chapter order, 200–300
character bodies, automatic completion after simulation ticks, permanent
completion history, and save/load rejection of missing, unknown, duplicate or
skipped chapter IDs.

当前已有基础 PlayMode 用例，但它们不覆盖完整的新游戏、前十分钟、页面切换、存档读写和后台恢复流程；基础用例通过不能替代这些场景的验收。

必须新增：

- 新游戏启动
- 前10分钟 smoke
- 页面关闭后模拟继续
- 保存/加载
- 后台/恢复
- P40 Pro 横屏与安全区
