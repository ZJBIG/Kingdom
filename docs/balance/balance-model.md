# Kingdom 数值设计基线

## 1. 建筑价格

推荐公式：

`unitCost(k) = baseCost × growth^k`

`batchCost = baseCost.GeometricSeriesCost(growth, owned, count)`

建议 growth：

- 原料建筑：1.12~1.14
- 加工建筑：1.15~1.17
- 研究/基础设施：1.18~1.20
- 住房：1.18~1.22
- 奇观：只允许1座

普通资源没有库存上限，因此建筑价格增长是长期经济的主要刹车之一。

## 2. 生产

`actualRate = baseRate × amount × efficiency × researchModifiers`

不要让每个科技都只增加 5%。关键科技建议：

- 小升级：×1.15~1.30
- 时代关键升级：×1.5~2.5
- 新生产方式：改变配方或解锁新建筑

## 3. 研究

研究力（`ResearchManager.CalculateResearchPower`，与源码同步）：

`ResearchPower = max(0, BaseResearchPower) + Σ( ResearchPowerGranted × Amount × clamp01(Efficiency) × GetBuildingResearchPowerMultiplier(建筑) )`

- `BaseResearchPower = 4`（`ResearchManager.cs`，常量）。
- **加法叠加**：基础值加各建筑贡献之和；各建筑/研究的定向倍率（`GetBuildingResearchPowerMultiplier`）**逐建筑相乘后再累加**，不是先求和再乘一个全局倍率。倍率通道的叠加口径见 §2（`1 + Σ(valueᵢ − 1)`）。
- 上面这条是与源码一致的口径。早前版本写的 `(1 + buildingResearchPower) × globalMultiplier` 是乘法结构且遗漏逐建筑倍率，**已作废**。

研究成本由 `Research` 定义资产直接给定（`ResourceRequirements`），**不是**按目标耗时反推出来的：

- 目标耗时只用于**校准定义资产里的成本数值**，不参与运行时计算。
- 因此"成本 = 目标秒数 × 预期研究力"仅作调参时的估算口径，不是实现公式。

建议目标：

- 首项：30~90秒
- 原始普通节点：2~8分钟
- 原始跃迁：10~20分钟
- 石器时代普通节点：5~20分钟
- 石器时代跃迁：20~60分钟
- 中古时代完整阶段：4~12小时

## 4. 回本时间

第一座建筑的目标回本：

- 原料：45~120秒
- 加工：90~240秒
- 研究：5~15分钟
- 基础设施：通过新增能力衡量

## 5. 食物

Food 是唯一有容量的库存。

在人口系统前，粮食满足率应影响人工建筑效率。

人口系统完成后：

- 每个人消耗粮食
- 食物不足停止增长
- 长期严重不足导致人口下降
- 自动化建筑主要受能源而不是粮食影响

## 6. 领土和生产力

它们不是普通资源容量。

- `TerritoryTotal - TerritoryUsed`
- `TotalProductivity - UsedProductivity`

初始值必须足够小，形成实际选择。

## 7. 大数

不要为了使用 ExpantaNum 而提前进入 `ee`、`^^`、`J`。

数值符号应跟随内容增长：

- 原始/石器：1~1e6
- 中古时代：1e4~1e10
- 工业：1e8~1e20
- 太空：1e18~1e50
- Ultra/Archotech：再逐步进入更高层级

这些只是量级方向。`NewEconomySimulator` 只输出快照、事件和首个差异，不能替代 Unity 运行时或玩家实玩结果；不得以 parity 输出作为当前节奏、平衡或内容决策依据。最终以真实 Unity 运行时、PlayMode 回归和玩家实玩证据为准。

## 8. 时代折算带（批次 2 静态对账推导，暂定）

第 1-7 节的回本/研究带来自早期时代口径；静态对账（`CONTENTADVISE/batch2-payback-and-duration-table.md`，该文件已随 `CONTENTADVISE/` 在提交 a85f0e7 删除，恢复件见 `.codex/archive/recovered-20260915/CONTENTADVISE/`）表明 TL3/TL4 缺少可比带。以下折算带用于后续 TL3/TL4 调参定案，**均为静态推算的暂定值，必须经 Unity 运行时与实玩校准后才能作为最终门槛**：

- 回本（首份，时代折算）：原料建筑 5-15min；加工建筑 15-40min
- 科研单条等待：10-60min
- 时代门节点：0.5-3h，或按"门时长≈上一时代总量的 10-20%"重推
- Spacer 尾盘（最后 4 条）合计控制在 1-2 天内

已按此口径落地的首批调整：快侧四条科研（MechanicalEngineering/Steelmaking/SteamPower/IndustrialWorkshop）成本 ×10 归入 2-5min 目标带；快侧补充观察项（Farm、IrrigationWorks、CeramicKiln、Lumberyard 偏快，TL1 科研 13/16 条偏低）与 Spacer 全量重定标仍待运行时证据定案。
