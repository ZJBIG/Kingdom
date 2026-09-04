# Kingdom 内容进度路线图

## 核心体验

玩家带领鼠族文明从原始聚落发展到跨星际文明，逐步理解：

`资源 → 建筑 → 人口与生产力 → 研究 → 加工链 → 时代目标`

Food 是唯一有库存上限的资源；普通资源不引入容量或仓库系统。

## 当前里程碑

### Milestone A：0~10 分钟开局可玩

- 新档提供有限的 WoodLog 起始库存。
- 教程精确指向真实资源、住房、研究和生产链对象。
- Resources 步骤必须查看 Food 或 WoodLog 详情后完成。
- 记录首个资源详情、建筑、人口、研究、生产链和时代里程碑。

### Milestone B：Animal → StoneAge

- 通过 Agriculture、AnimalHusbandry、ClayExtraction 和真实资源条件完成 StoneAgeSettlement。
- Era 页面区分时代推进硬条件与建议准备度。

### Milestone C：StoneAge → MiddleAge

- 通过青铜、铁器、文字记录和 FeudalAdministration 的真实资源条件推进。
- 中古时代研究节点按建设、生产、研究和人口等不同决策角色组织。

### Milestone D：MiddleAge → Industrial

- 通过 MechanicalEngineering、Steelmaking 和 Industrialization 进入工业时代。
- 工业主线以“当前 / 下一步 / 之后”三步路线呈现，不新增第二套任务系统。

## 后续方向

- 进入 Spacer 后优先完善已有远征补给、舰队维修和星区反馈。
- Research Tree 提供当前目标定位和未满足前置路径高亮；不额外提供无实际收益的当前时代跳转。
- Story 继续作为由真实玩法状态按顺序永久完成的文明记忆，不提供经济奖励；存档 v8 必须包含剧情完成段。
- Ultra / Archotech 在 Milestone A-D 和移动端验收完成前冻结。

## 验收顺序

1. 静态闭包、资源流和 C# / Unity 编译。
2. EditMode 与 PlayMode 测试隔离及全绿。
3. Animal → StoneAge → MiddleAge → Industrial 的无作弊流程。
4. Android ARM64 构建和 Huawei P40 Pro 横屏体验。

离线模拟器仅保留历史诊断用途，不用于替代 Unity 运行时证据或调节当前经济数值。
