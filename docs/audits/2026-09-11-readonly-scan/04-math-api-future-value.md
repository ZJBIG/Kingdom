# 数学 API 未来价值评估（2026-09-11）

## 扫描范围与方法

- 覆盖文件：
  - `Assets/Resources/Script/Math/ExpantaNum.cs`：3013 行
  - `Assets/Resources/Script/Math/ExpantaNumExtensions.cs`：621 行
- 依据文档：
  - `AGENTS.md`
  - `Assets/Resources/Script/AGENTS.md`
  - `docs/content/progression-roadmap.md`
  - `docs/balance/balance-model.md`
  - `docs/balance/no-resource-caps.md`
  - `docs/testing/content-balance-tests.md`
- 方法：
  1. 逐个枚举 `ExpantaNumExtensions.cs` 的公开声明。
  2. 用 `rg` 在 `Assets` 与 `tools` 中统计每个 API 的调用点，排除定义文件本身。
  3. 区分“外部调用”和“同文件内部调用”，避免把内部依赖误判为业务使用。
  4. 检查 `ExpantaNum.cs` 的私有方法引用链，只记录明显内部死代码，不做删除建议。
  5. 对照进度路线图与数值基线，评估每个未使用 API 的未来价值。
- 本报告为只读审查结果，未修改任何代码、资产或场景。

## API 完整编目

容器类型为 `public static class ExpantaNumExtensions`。以下行号均指
`Assets/Resources/Script/Math/ExpantaNumExtensions.cs`。

### 成本与批量购买

| 位置 | API | 签名 | 外部 / 内部调用 | 状态 | 未来价值评估 |
|---|---|---|---:|---|---|
| `:37` | `GeometricSeriesCost` | `GeometricSeriesCost(this ExpantaNum baseCost, ExpantaNum ratio, ExpantaNum owned, ExpantaNum count)` | 12 / 2 | 已使用 | **高**。建筑、教程、UI 与测试均在用；后续时代仍需要闭式批量总价。 |
| `:70` | `MaxAffordableGeometricSeries` | `MaxAffordableGeometricSeries(this ExpantaNum currency, ExpantaNum baseCost, ExpantaNum ratio, ExpantaNum owned)` | 4 / 0 | 已使用 | **高**。Max 购买与边界测试已用；未来批量购买仍会需要。 |
| `:122` | `ArithmeticSeriesCost` | `ArithmeticSeriesCost(this ExpantaNum baseCost, ExpantaNum increment, ExpantaNum owned, ExpantaNum count)` | 0 / 2 | 未使用 | **低**。当前建筑成本模型明确采用等比增长，不是等差增长；若未来也不用，可直接用闭式公式替代。 |
| `:151` | `MaxAffordableArithmeticSeries` | `MaxAffordableArithmeticSeries(this ExpantaNum currency, ExpantaNum baseCost, ExpantaNum increment, ExpantaNum owned)` | 0 / 0 | 未使用 | **低**。与上一条同理；当前没有等差批量购买机制。 |
| `:190` | `ExponentialCost` | `ExponentialCost(this ExpantaNum baseCost, ExpantaNum growth, ExpantaNum level)` | 0 / 0 | 未使用 | **中**。若未来出现等级制升级或等级制研究成本，可直接使用；若不用，等价于 `baseCost * growth.Pow(level)`。 |

### 软上限与增长

| 位置 | API | 签名 | 外部 / 内部调用 | 状态 | 未来价值评估 |
|---|---|---|---:|---|---|
| `:209` | `Softcap` | `Softcap(this ExpantaNum value, ExpantaNum start, ExpantaNum power)` | 0 / 1 | 未使用 | **中**。战斗比值超过 2 后已有软上限语义，但当前 `CampaignManager` 用专用有理式实现，不是本 API；若未来统一为幂律软上限，此方法有价值。 |
| `:229` | `ApplySoftcaps` | `ApplySoftcaps(this ExpantaNum value, ExpantaNumSoftcapStage[] stages)` | 0 / 0 | 未使用 | **低～中**。若未来需要多段软上限配置，可用；当前没有多段软上限系统。 |
| `:255` | `ReverseSoftcap` | `ReverseSoftcap(this ExpantaNum value, ExpantaNum start, ExpantaNum power)` | 0 / 0 | 未使用 | **中**。若未来要显示“达到目标输出所需的原始输入”，有反解价值；当前没有该 UI。 |
| `:276` | `ScaleAfter` | `ScaleAfter(this ExpantaNum value, ExpantaNum start, ExpantaNum power)` | 0 / 0 | 未使用 | **低**。当前没有绝对增量缩放场景；若不用，可在调用点内联。 |
| `:297` | `CompoundGrowth` | `CompoundGrowth(this ExpantaNum principal, ExpantaNum rate, ExpantaNum periods)` | 0 / 0 | 未使用 | **低**。当前经济是逐 tick 线性产出加等比成本，不是复利模型；若不用，可内联表达式。 |

### 声望与效率

| 位置 | API | 签名 | 外部 / 内部调用 | 状态 | 未来价值评估 |
|---|---|---|---:|---|---|
| `:317` | `PrestigeGain` | `PrestigeGain(this ExpantaNum resource, ExpantaNum requirement, ExpantaNum exponent, ExpantaNum multiplier)` | 0 / 0 | 未使用 | **低**。当前路线图和叙事都没有声望、重生或重置系统；若未来引入元进度，此方法可直接使用。 |
| `:344` | `PrestigeRequirement` | `PrestigeRequirement(this ExpantaNum prestige, ExpantaNum requirement, ExpantaNum exponent, ExpantaNum multiplier)` | 0 / 0 | 未使用 | **低**。若未来加入声望目标显示，可与 `PrestigeGain` 配对使用；当前无对应系统。 |
| `:366` | `PurchaseEfficiency` | `PurchaseEfficiency(this ExpantaNum gain, ExpantaNum cost)` | 0 / 0 | 未使用 | **低**。当前不做策略评分或自动决策；若不用，可直接相除。 |

### 统计分布

| 位置 | API | 签名 | 外部 / 内部调用 | 状态 | 未来价值评估 |
|---|---|---|---:|---|---|
| `:519` | `Erf` | `Erf(ExpantaNum x)` | 0 / 2 | 未使用 | **低**。当前战斗与剧情明确是确定性的；若未来引入概率事件或分布阈值，可作为基础函数。 |
| `:550` | `Erfc` | `Erfc(ExpantaNum x)` | 0 / 0 | 未使用 | **低**。与 `Erf` 同理；当前无概率系统。 |
| `:555` | `NormalPDF` | `NormalPDF(ExpantaNum x, ExpantaNum mu, ExpantaNum sigma)` | 0 / 0 | 未使用 | **低**。当前无概率密度需求；若不用，普通 `double` 场景可用 `System.Math` 替代。 |
| `:568` | `NormalCDF` | `NormalCDF(ExpantaNum x, ExpantaNum mu, ExpantaNum sigma)` | 0 / 1 | 未使用 | **低～中**。若未来加入概率阈值或事件分布，有价值；当前没有对应系统。 |
| `:579` | `NormalProbability` | `NormalProbability(ExpantaNum a, ExpantaNum b, ExpantaNum mu, ExpantaNum sigma)` | 0 / 0 | 未使用 | **低**。当前无区间概率需求。 |
| `:590` | `NormalQuantile` | `NormalQuantile(ExpantaNum p, ExpantaNum mu, ExpantaNum sigma)` | 0 / 0 | 未使用 | **低～中**。若未来需要由目标概率反推阈值，有价值；当前没有对应系统。 |

### 公开参数结构

| 位置 | API | 签名 | 外部 / 内部调用 | 状态 | 未来价值评估 |
|---|---|---|---:|---|---|
| `:9` | `ExpantaNumSoftcapStage` | `public struct ExpantaNumSoftcapStage { public ExpantaNum Start; public ExpantaNum Power; public ExpantaNumSoftcapStage(ExpantaNum start, ExpantaNum power); }` | 0 / 0 | 未使用 | **低～中**。若未来引入多段软上限配置，这是现成载体；目前没有任何调用。 |

## 调用点分布与测试覆盖

- `GeometricSeriesCost`：12 个外部调用点
  - `BuildingManager.cs`：5 处
  - `TutorialManager.cs`：2 处
  - `KingdomUIRoot.DetailPanel.cs`：2 处
  - `BuildingCostGrowthTests.cs`：3 处
- `MaxAffordableGeometricSeries`：4 个外部调用点
  - `BuildingManager.cs`：1 处
  - `BuildingCostGrowthTests.cs`：3 处
- 其余公开 API 在 `Assets` 与 `tools` 中均无外部调用。
- `tools/NewEconomySimulator` 没有直接调用这些扩展；它在
  `tools/NewEconomySimulator/SimulationState.cs:467` 另有一份 `GeometricCost` 双实现。
- 当前只有等比成本族有测试覆盖；等差、软上限、声望与统计族均没有直接测试。

## ExpantaNum 本体观察

以下行号均指 `Assets/Resources/Script/Math/ExpantaNum.cs`。

| 位置 | 观察 |
|---|---|
| `:1014` | `Log1P()` 私有方法没有任何调用。 |
| `:1033` | `ExpM1()` 只被同样未被调用的 `PowM1()` 使用。 |
| `:1052` | `PowM1()` 没有任何调用。 |
| `:2972` | `Log1PDouble()` 只被未被调用的 `Log1P()` 使用。 |
| `:2987` | `ExpM1Double()` 只被未被调用的 `ExpM1()` 使用。 |
| `:3002` | `CreateSpecial()` 没有任何调用。 |

其他容易误判的私有方法均有真实调用：
- `Reciprocal()` 在 `Pow()` 内使用。
- `CloneOperators()` 在算子更新路径中使用。
- `CreateFactorialTable()` 在静态表初始化中使用。

本节仅记录内部死链，不给出删除建议。

## 未来路线建议

| 阶段 | 建议 |
|---|---|
| 当前 / 工业 | 继续保留并使用等比成本族；它是建筑购买、教程和 UI 的核心公式。 |
| Spacer | 若战斗或远征需要统一幂律软上限，可评估 `Softcap` / `ReverseSoftcap`；但当前专用有理式已经满足设计，不能直接替换。 |
| Ultra / Archotech | 只有在明确设计声望、重生或概率系统后，再考虑启用 `Prestige*` 或 `Normal*` 族。 |
| 长期 | 等差与单级指数成本族优先级较低；除非出现明确的新成本模型，否则不建议主动接入。 |

若未来启用任何当前未使用的 API，应先补最小回归测试，再接入 Manager / State / UI，避免把“可用公式”直接当成“已验证行为”。

## 逻辑不变精简建议

- 未来可考虑统一 `ExpantaNumExtensions.GeometricSeriesCost` 与模拟器
  `SimulationState.GeometricCost` 的双实现，以消除公式漂移风险；本轮不改代码。
- 若 `ExpantaNumSoftcapStage` 将来被配置系统使用，可评估把公开可变字段改为只读属性或专用序列化 DTO；这是接口设计优化，不是本轮任务。
- 当前不建议为了“清理”而删除高级未调用 API；它们的价值应由未来现有机制决定。

## 明确不建议动的部分及理由

- 不删除 `Erf`、`Erfc`、`NormalPDF`、`NormalCDF`、`NormalProbability`、
  `NormalQuantile`、`PrestigeGain`、`PrestigeRequirement`、`CompoundGrowth`、
  `ScaleAfter`、`ReverseSoftcap`、`ApplySoftcaps`、`ExponentialCost`、
  `ArithmeticSeriesCost`、`MaxAffordableArithmeticSeries`、`PurchaseEfficiency`
  等高级未调用 API。
- 不把当前 `CampaignManager` 的有理式软上限直接替换为 `Softcap`；两者曲线不同，直接替换会改变玩法行为。
- 不把普通建筑成本改成等差增长；`balance-model.md` 明确要求等比增长。
- 不给 `ExpantaNum` 增加新的 gameplay/UI 便利 API；这会违反 `Assets/Resources/Script/AGENTS.md`。
- 不在本轮处理 `ExpantaNum` 内部死链；本报告只记录事实。

## 与旧审查的交叉验证

- 旧报告“高级数学 API 死链”的结论仍然成立：除等比成本族外，其余公开 API 均无外部调用。
- 需要修正一点：`Softcap` 的外部调用数是 **0**，不是 1；它唯一的调用来自同文件的
  `ApplySoftcaps`，而 `ApplySoftcaps` 本身也未被外部使用。
- 旧报告建议删除或移出高级数学 API；本报告根据当前任务要求改为未来价值评估，不采纳删除建议。

## 验证记录

- 已用 `rg` 复核 `Assets` 与 `tools` 中全部公开 API 的调用点。
- 已核对 `ExpantaNumExtensions.cs` 与 `ExpantaNum.cs` 的当前行数。
- 已检查 `ExpantaNum` 私有方法的引用链。
- 未执行真实 Unity 编译；本任务为纯文档审查。
