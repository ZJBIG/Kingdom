# 全时代经济审查与金属库存整合（2026-10-14）

本轮目标：从开局到现有终点检查经济依赖，减少矿石及金属库存管理负担。审查输入为当前 main / 769446d 的未提交工作树；保留已有 UI、数学及清理变更。第一阶段审查后，用户明确要求立即实施；资产、运行代码、素材和相关测试已完成整合。本文原表保留改动前数据；最新实施与验证记录见 data/metal-consolidation-20261014/handoff.md。

> 后续决定：用户已授权废弃所有旧档。保留40身份/共享库存的方案已被删除，当前为33个真实资源定义、语义ID及全新v1。以下表格属于改动前审查，最终结果见同主题handoff的“后续重构”节。

## 审查范围与口径

覆盖 40 种资源、72 种建筑、143 项研究、86 项工坊、10 个星区、2 个文明工程定义，并核对持续生产、建造/研究/工坊支付和后期战略投入。18 个故事定义只检查经济引用，不改叙事。

现有游戏内容终点为 Ultra；Archotech 是已有枚举，不据此扩建未来时代。建筑可用性累积，已建旧档仍能生产；资源 TechLevel 是展示分组，不能擅自作为新硬门槛。

资源来源/消耗引用扫描覆盖建筑产耗、各类一次成本、星区收益及资源消费、文明工程嵌套成本。40 种资源均有来源和消耗引用。来源闭包不检查数量、人口、电力、物流和等待时长，不代表真人节奏或实际运行验收。

## 从开局到终点

| 阶段 | 当前主要经济角色 | 审查结论与本轮处理 |
|---|---|---|
| Animal | 原木、石材、黏土、基础住房和人口/粮食 | 不需要增加资源或容量；保持已有采集、住房、粮食链 |
| StoneAge | 开采、煤和初级冶炼，铜/锡/青铜技术 | 铜矿、锡矿、铁矿被同一座矿场联产；铜、锡、铁、青铜由同一冶炼生产者联产。库存拆分没有对应四种生产选择，合并为低级矿与基础锭 |
| Medieval | 铁器、钢、线材、书籍及生产升级 | 保留技术研究顺序和钢/线材加工角色。基础材料费用合并，不把钢及线材混入基础锭 |
| Industrial | 机械、电气、原油/化工、铝镍钛加工、工坊 | 发现原油/炸药和机械/高级耗材两处启动环；须先补真实生产闭包，再进行矿锭改造 |
| Spacer | 轨道开采、先进加工、星区与军事补给 | 延续三档矿和两种锭，保留钢、钛合金、电子、复合材料及燃料等有实际功能的加工品，不新增每一种工业物料的轨道替代厂 |
| Ultra | 相态材料、幻影材料、文明工程 | 保留现有高级加工与战略成本；它们都有真实持续用途，不为减少数量而将全部高级材料合并成一项 |

## 已确认的两个生产断点

### 原油与炸药互锁

OilDerrick 持续消费 Explosives 0.08/s。早期炸药生产者 ChemicalPlant 又需要 CrudeOil 1.5/s；IntegratedPetrochemicalComplex 也依赖原油，高级油井同样需要炸药和润滑剂。零原油、零炸药时无法自举。

决定：基础 OilDerrick 去掉炸药持续消费，保留已有建造、研究、生产力、电力和物流条件；高级油井继续需要耗材。不普调产速。

### 机器与其上游互锁

MachineFactory 持续消费 Aluminum 0.08/s、Lubricant 0.1/s、Rubber 0.1/s。铝及炼油上游的建筑/工坊需要 Machinery，机器厂又是早期机械的唯一来源。BuildingManager 对所有投入采用最低满足率，缺任意一种都会停厂，消耗较小也不能自举。

决定：基础 MachineFactory 使用已有基础金属、钢、线材，移除尚需机械建设的高级耗材。高级工厂继续使用高级锭、橡胶和润滑剂。不改变其机械、发动机、复合材料的产出数值。

### 静态门槛漏检

当前 content-closure-check、EconomyDependencyValidator 的可达检查和 ContentProgressionAudit 授予建筑产出之前只检查建造成本，没有要求持续输入来源已可达。IsBootstrappableCycle 将存在一个环外输入当作可自举也不足以覆盖“同时需要全部输入”的生产规则。

主代理按当前资产独立复算：

| 条件 | 时代 | 资源 | 研究 | 工坊 | 建筑 |
|---|---:|---:|---:|---:|---:|
| 当前仅建造费检查 | Ultra | 40 | 143 | 86 | 72 |
| 加入所有持续输入来源要求 | Industrial | 19 | 57 | 7 | 28 |
| 只在内存移除上述两个基础工厂的循环耗材 | Ultra | 40 | 143 | 86 | 72 |

最后一行是未落盘的静态假设实验，不是修改后实机验收。正式检查应在现有累计闭包上补持续输入条件，不增加策略搜索或自动调参。

## 已确定的矿锭方案

| 原资源 | 玩家管理的资源 | 保留的资产身份 |
|---|---|---|
| CopperOre / TinOre / IronOre | 低级金属矿 | CopperOre ID/GUID |
| BauxiteOre / NickelConcentrate | 高级金属矿 | BauxiteOre ID/GUID |
| TitaniumConcentrate | 特级金属矿 | TitaniumConcentrate ID/GUID |
| Copper / Tin / Iron / Bronze | 基础金属锭 | Copper ID/GUID |
| Aluminum / Nickel | 高级金属锭 | Aluminum ID/GUID |
| Steel | 钢材 | Steel ID/GUID |
| TitaniumAlloy | 钛合金 | TitaniumAlloy ID/GUID |

目标是矿 6→3、金属锭及结构合金 8→4，总计 14→7 项库存；保留材料技术里程碑，减少不同材料分别短缺的管理负担。

特级矿和钛合金必须在工业后期可用，不能因原资源展示分组为 Spacer 而延迟到太空：当前工业→太空链需要它们，生产建筑和研究本就在 Industrial。

原材料配方改为同资源时按精确十进制相加，避免重复资源对。定向效果按同语义去重，不能把原来分别作用于多资源的同一个倍率意外累乘到合并资源。产耗映射若形成同资源双向流，应核对并规范为净流，保留战略产出。

不修改真实存档，不新增旧版本迁移或旧 ID 映射。正式实施前需保留资产身份并明确退役定义的读取/展示行为，不能仅改 UI 标签或隐藏旧库存，伪装成真正合并。

## 分阶段提交

1. `docs(economy): audit progression and define metal consolidation`：本报告、当前资源引用清单、原检查与持续输入检查对照、确定性诊断输入边界。
2. `fix(economy): remove industrial bootstrap cycles`：补闭包条件，修两个基础工厂，补空库存非零产出回归。
3. `feat(economy): consolidate metal ores into three tiers`：引用、配方、矿场、资源显示、测试和保留身份处理。
4. `feat(economy): simplify ingots and late-game recipes`：基础锭/高级锭、研究/工坊/星区/工程消费端和定向效果一致化。
5. `test(economy): verify consolidated progression from start to end`：同输入闭包、来源消费检查、真实Unity编译、EditMode、相关PlayMode与最终报告。

每一步完成后检查实际差异并独立提交。只提交本主题文件，不把已有 UI、Math、字体或清理变更混入。

## 当前已执行的检查

- 原有静态检查已运行，显示全部可达；因上面的漏检不计为持续生产闭环通过。报告：data/metal-consolidation-20261014/closure-before.md。
- 持续输入闭包三组对照：continuous-input-closure.json。采用当前 YAML/GUID 定义，所有假设仅在内存中处理。
- 资源全量引用清单：resource-audit.json。下表统计来源/消耗引用次数，非独立工厂数或运行速率。
- NewEconomySimulator 当前源码 no-restore 构建成功，诊断各项 passed=true。它包含工具自测/人工fixture，不能替代游戏资产闭包或 Unity 运行。diagnostic-before.json。
- 未执行真实 Unity 编译。

## 40 种资源明细

| 时代序号 | ID | 名称 | 来源引用 | 消耗引用 |
|---|---|---|---:|---:|
| 0 | Biomass | 生物质 | 3 | 38 |
| 0 | Clay | 黏土 | 2 | 19 |
| 0 | Cloth | 布料 | 3 | 31 |
| 0 | StoneBrick | 石砖 | 2 | 36 |
| 0 | StoneChunk | 石块 | 2 | 19 |
| 0 | WoodLog | 原木 | 3 | 60 |
| 1 | Bronze | 青铜锭 | 2 | 28 |
| 1 | Coal | 煤炭 | 3 | 17 |
| 1 | Copper | 铜锭 | 2 | 8 |
| 1 | CopperOre | 铜矿石 | 3 | 4 |
| 1 | Tin | 锡锭 | 2 | 6 |
| 1 | TinOre | 锡矿石 | 3 | 4 |
| 2 | Iron | 铁锭 | 2 | 6 |
| 2 | IronOre | 铁矿石 | 4 | 4 |
| 2 | Steel | 钢锭 | 2 | 106 |
| 3 | Aluminum | 铝锭 | 2 | 25 |
| 3 | BauxiteOre | 铝土矿 | 3 | 6 |
| 3 | Ceramic | 陶瓷 | 5 | 116 |
| 3 | Chemical | 化工材料 | 2 | 45 |
| 3 | Coke | 焦炭 | 3 | 44 |
| 3 | Concrete | 混凝土 | 3 | 31 |
| 3 | CopperWire | 铜线 | 3 | 40 |
| 3 | CrudeOil | 原油 | 2 | 5 |
| 3 | Electronics | 电子元件 | 9 | 169 |
| 3 | Engine | 发动机 | 3 | 22 |
| 3 | Explosives | 工业炸药 | 2 | 8 |
| 3 | Glass | 玻璃 | 3 | 30 |
| 3 | Lubricant | 润滑剂 | 2 | 25 |
| 3 | Machinery | 机械部件 | 3 | 132 |
| 3 | Nickel | 精镍 | 4 | 24 |
| 3 | NickelConcentrate | 镍精矿 | 3 | 12 |
| 3 | RefinedFuel | 精炼燃料 | 2 | 15 |
| 3 | Rubber | 橡胶 | 2 | 26 |
| 4 | Composite | 复合材料 | 12 | 106 |
| 4 | PhantomAlloy | 幽影合金 | 9 | 94 |
| 4 | PhantomWeave | 幽影纤维 | 9 | 79 |
| 4 | PhaseMaterial | 相位材料 | 9 | 94 |
| 4 | RocketFuel | 火箭燃料 | 5 | 40 |
| 4 | TitaniumAlloy | 钛合金 | 10 | 134 |
| 4 | TitaniumConcentrate | 钛精矿 | 4 | 10 |
