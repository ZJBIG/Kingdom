# 工业及后期占地与持续投入审查

已读取“工业时代修复”会话（`01a1c318-48ce-7322-9343-1f0d0e688c57`），保留其 26 栋工业建筑、109 项建造材料的提高结果，以及机器制造厂前置移除和火箭燃料科技门。本轮对现有 46 栋建筑逐项调整，共 279 个数值字段；生产代码只增加一处上游来源保留判断。

用户明确要求按实际链路调整占地、投入与产出，工业及后期扩大吞吐并保留加工损耗。本轮没有使用、运行或修改旧模拟器；已有模拟器改动与本轮前的 diff 相同。

## 调整依据与结果

工业 26 栋定义各取一座的占地合计 **569 → 2290**。初始领土与工业及之前研究可授予领土合计 4900，对应占比 **11.61% → 46.73%**。这个口径包含不同升级档，用于比较定义规模；每栋按采掘、加工、住房或科研角色分别确定占地，具体值见逐字段表。

加工厂先核对主料来源，再扩大持续流量。高级精炼原来 `2 高级矿石 → 2.1 金属锭`，现为 `16 矿石 + 2.8 化工材料 → 12 金属锭/s`。工业冶炼为 `65 基础矿石 + 8 焦炭 → 40 基础锭 + 12 钢/s`，同时保住钢厂升级产出。高级冶炼到精炼的单位生产力产出仍提高约 14.3%。

轨道线材厂和建材厂补回基础金属锭、陶瓷、石材、黏土、焦炭和化工原料。轨道机加工提高钛合金、电子元件和高级锭投入。三座专用厂按工业定向工坊和实际升级倍率调整，保留单座吞吐增长；其单位生产力产出仍低于满工业工坊的工业厂，但没有新增原升级轮廓的退化。

| 工业 → 轨道专用厂 | 单位生产力产出比：本轮前 | 单位生产力产出比：最终 | 最终单座产出相当于工业厂 |
|---|---:|---:|---:|
| 建材三项产物 | 57.14% | 64.29%–65.48% | 2.57–2.62 座 |
| 线材两项产物 | 57.61% | 63.79%–64.81% | 3.28–3.33 座 |
| 机加工三项产物 | 41.15% | 43.21%–46.30% | 1.94–2.08 座 |

同一主料与产物的基础得率体现损耗：轨道混凝土 `64/156≈0.4103`，低于工业 `14/32=0.4375`；轨道铜线 `31/46≈0.6739`，低于工业 `7/10=0.7`；轨道炭化 `110/220=0.5`，低于工业 `28/50=0.56`。不同材料的数量单位没有相加作为“总收益”或回本依据。

| 建筑 | 最终基础投入 /s | 最终基础产出 /s |
|---|---|---|
| 工业机器制造厂 | 7 钢、2 铜线、3 基础锭 | 5 机械、1 引擎、2.4 复材 |
| 工业建筑材料联合厂 | 32 石材、4 焦炭、1.5 化工材料 | 8 玻璃、2.4 陶瓷、14 混凝土 |
| 轨道机加工综合体 | 20 钛合金、10 电子元件、8 高级锭 | 22 机械、4.2 引擎、10.8 复材 |
| 轨道线材厂 | 46 基础锭、4 陶瓷、1.8 钛合金 | 31 铜线、7.2 电子元件 |
| 轨道建材厂 | 156 石材、18 黏土、18 焦炭、9 化工材料、2.5 钛合金 | 36 玻璃、11 陶瓷、64 混凝土 |
| 轨道炭化综合体 | 220 原木及电子元件、幽影、相位等维护材料 | 110 焦炭 |
| 幽影材料厂 | 6 钛合金、4 复材、3 化工材料、2 高级锭及织物、橡胶、玻璃等 | 0.4 幽影合金、0.3 幽影织物 |
| 相位材料合成阵列 | 0.8 幽影合金、0.6 幽影织物、3 钛合金、8 复材及其他维护材料 | 0.45 相位材料 |
| Ultra 自治物质工厂 | 72 钛合金、24 电子元件、18 机械、24 高级锭 | 6 幽影合金、4 幽影织物、2.4 相位材料、24 复材 |

住房、研究、船坞、物流与能源设施逐项提高对应维护材料。例如轨道住宅生物质为 80/s，船坞机械维护为 1.2/s。大型轨道综合资源工厂本来已占地 8000、需要 16000 生产力与 5000 电力，保留其规模和配方。其必要研究与工坊静态递归核对保证建筑倍率至少 2.02；最低倍率下，混凝土 64.64 仍高于专用厂 64，其余对应产物也保持增长。最终真实 Manager 升级检查进一步覆盖三个专用厂到综合厂的所有产物。

## 恢复供料与实现位置

初级油井保留无材料维护，改为占地 54、耗电 16、原油产出 9/s。高级油厂需要润滑油与炸药，初级油井承担油链断供后的恢复入口。

`BuildingManager.IsHighestUnlockedChainTier` 保留已有无材料保底来源规则，并允许继续建造产出被最高已解锁后继持续消耗的旧厂。幽影厂最后一座升级后，仍可重建幽影厂补足综合厂的幽影投入；焦炭来源也得到保留。自身时代、研究和工坊前置仍由原校验判断。

修改位置：`Assets/Resources/Datas/Building/{Industrial,Spacer,Ultra}/` 中 46 份现有资产，`Assets/Resources/Script/Manager/BuildingManager.cs`，以及七份相关测试。原有增长率、ID、GUID、升级链、Food、人口、生产力和研究力字段保持本轮前值。唯一电力字段调整是初级油井；建造费只另按角色调整发射中心的九项材料。

## 验证

验证在原工作树运行 Unity **2022.3.62f3c1**，采用 `-batchmode -force-d3d11 -runTests`，每次独立 XML/log，进程上限 240 秒。没有创建临时 Unity 项目。最终 EditMode 与 PlayMode 均完成真实 Unity 编译，无脚本编译错误；最终 PlayMode 正常退出 0，扩大 EditMode 因三个既有失败退出 2。

| 运行 | 总数 | 通过 | 失败 | 跳过 | 证据 |
|---|---:|---:|---:|---:|---|
| 修改前复现 | 5 | 0 | 5 | 0 | [edit-repro.xml](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/edit-repro.xml) / [edit-repro.log](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/edit-repro.log) |
| 首轮相关检查 | 141 | 137 | 4 | 0 | [edit-targeted.xml](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/edit-targeted.xml) / [edit-targeted.log](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/edit-targeted.log) |
| 首轮扩大检查 | 313 | 302 | 11 | 0 | [edit-expanded.xml](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/edit-expanded.xml) / [edit-expanded.log](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/edit-expanded.log) |
| 配方与预算修正后 | 319 | 316 | 3 | 0 | [edit-corrected.xml](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/edit-corrected.xml) / [edit-corrected.log](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/edit-corrected.log) |
| 最终扩大 EditMode | 324 | 321 | 3 | 0 | [edit-verified.xml](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/edit-verified.xml) / [edit-verified.log](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/edit-verified.log) |
| 首次 PlayMode，夹具建材清空顺序失败 | 3 | 2 | 1 | 0 | [play-final.xml](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/play-final.xml) / [play-final.log](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/play-final.log) |
| 最终 PlayMode | 3 | 3 | 0 | 0 | [play-verified.xml](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/play-verified.xml) / [play-verified.log](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/play-verified.log) |

修改前五项新复现均在真实 Unity 失败，覆盖工业占地偏低、高级精炼无损耗、幽影来源不可重建、轨道配方绕开上游，以及 Ultra 钛合金投入不足。首轮发现的住宅占地与重复产物签名问题已修正。首次 PlayMode 的建材不足来自夹具在建造前清空了自身建材，现顺序为实际建造后再清空产物。

最终新增 Manager 验证：**21 项供料、10 项升级、1 项幽影断供重建全部通过**。供料夹具运行五个 1 秒真实 tick；指定材料只预留 100，其他材料、供电和物流预置充足，并预置带付款台账的完成研究与已购工坊状态。幽影与相位共享复材的用例将幽影库存置零，实际同时运行两座轨道机加工厂、两座幽影厂和一座相位阵列。下表是最终 Unity 日志的真实速率。

| 来源数量 → 消费者 | 指定材料 | 供给 /s | 总消耗 /s | 供给/消耗 |
|---|---|---:|---:|---:|
| 1 × IntegratedMetalMine → IndustrialMetalSmelter | BasicMetalOre | 380.295 | 259.39875 | 1.47 |
| 1 × IntegratedMetalMine → AdvancedMetalRefinery | AdvancedMetalOre | 84.51 | 50.706 | 1.67 |
| 1 × IndustrialMetalSmelter → MachineFactory | Steel | 55.07235 | 35.4942 | 1.55 |
| 1 × WireMill → MachineFactory | CopperWire | 22.183875 | 10.1412 | 2.19 |
| 2 × IndustrialStoneworks → BuildingMaterialsComplex | StoneBrick | 210.336 | 131.46 | 1.60 |
| 4 × IndustrialStoneworks → OrbitalBuildingMaterialsWorks | StoneBrick | 420.672 | 366.21 | 1.15 |
| 1 × AdvancedCeramicsPlant → OrbitalBuildingMaterialsWorks | Clay | 52.1145 | 42.255 | 1.23 |
| 1 × IndustrialMetalSmelter → OrbitalWireWorks | BasicMetalIngot | 159.63 | 107.985 | 1.48 |
| 1 × IntegratedMetalMine → TitaniumAlloySmelter | SpecialMetalOre | 58.3119 | 47.302125 | 1.23 |
| 2 × OrbitalResourceExtractionArray → AutonomousMatterFabricator | TitaniumAlloy | 332.652995 | 169.02 | 1.97 |
| 2 × OrbitalResourceExtractionArray → AutonomousMatterFabricator | Electronics | 73.35468 | 56.34 | 1.30 |
| 1 × OrbitalMachiningComplex → AutonomousMatterFabricator | Machinery | 51.645 | 42.255 | 1.22 |
| 2 × AdvancedMetalRefinery → AutonomousMatterFabricator | AdvancedMetalIngot | 76.059 | 56.34 | 1.35 |
| 2 × PhantomMaterialsFabricator → PhaseMaterialSynthesisArray | PhantomAlloy | 3.15504 | 2.66676 | 1.18 |
| 2 × PhantomMaterialsFabricator → PhaseMaterialSynthesisArray | PhantomWeave | 2.464875 | 2.00007 | 1.23 |
| 2 × OrbitalAgroecologyArray → OrbitalResourceExtractionArray | WoodLog | 1087.045 | 681.1506 | 1.60 |
| 2 × OrbitalAgroecologyArray → OrbitalResourceExtractionArray | Biomass | 5329.764 | 2095.848 | 2.54 |
| 2 × OrbitalAgroecologyArray → OrbitalCarbonizationComplex | WoodLog | 1087.045 | 723.03 | 1.50 |
| 2 × OrbitalMachiningComplex → PhaseMaterialSynthesisArray | Composite | 65.9178 | 26.6676 | 2.47 |
| 1 × OrbitalAgroecologyArray → OrbitalHabitatMegastructure | Biomass | 2664.882 | 187.8 | 14.19 |
| 2 × OrbitalMachiningComplex → PhaseMaterialSynthesisArray + 2 幽影厂 | Composite | 65.9178 | 52.9596 | 1.24 |

最终升级实测保留原有矿业 **8 倍**与纺织 **4 倍**门槛，把它们放到实际倍率、实际升级提交后的资源流验证中：基础矿约 9.64 倍、高级矿约 10.23 倍；纺织产出约 6.76 倍，生物质投入约 4.58 倍。三座专用厂到综合厂的所有产物都增长。

煤电与 Ultra 共用电力预算仍使用原严格总预算断言。旧夹具由舍入后的 `0.013115` 材料支出倒推时长，得出伪超支 300.00642，而真实分配为 299.99922。夹具改为一电站、四煤矿并配足对应人口，使欠供满足率为可精确量化的 0.15625；仍要求正推进、欠供以及总预算不超支，最终通过。没有修改生产预算代码或放宽预算断言。

最终 PlayMode **3/3**：工业原油与机械起步、新局初始化、后期工厂扣料/缺料停产/补料恢复。后者对轨道线材、轨道建材和 Ultra 工厂逐项核对真实库存扣除、无料零产出、补料恢复及拆除。场景 fixture 使用现有临时存档覆盖目录，结束后已清理。

静态闭包修改前后均通过，最终工业及之前研究 83/83、工坊 37/37、建筑 50/50；Spacer 为 47/47、52/52、26/26；Ultra 为 15/15、3/3、3/3。严格资源流检查覆盖全部 79 栋建筑，正金额、资源对唯一性、来源/用途与授权字段检查通过；无同一资源同时生产与消耗。

最终 Console 无脚本编译、导入、NullReference 或 MissingReference 错误。EditMode 日志包含五条现有 `observer-test` 用例有意注入、由 LogAssert 消费的异常，以及不影响测试启动的授权更新提示。没有将这些预期异常计为新失败。

## 保留的既有失败与验证范围

最终扩大 EditMode **321/324**，下面三项仍因 `CivilizationPlanning` 定义没有效果而失败，本轮没有修改该研究：

- `C6IndustrialContentTests.C607_IndustrialProductionGraphHasNoRecipeCycle`
- `GlobalEconomyDefinitionTests.EveryResearchAndWorkshopEffectHasAValidTarget`
- `GlobalEconomyDefinitionTests.IntegratedDependencyGraphIsReachable`

该空效果在本轮前的工业会话验证中已有对照证据。本轮新增配方、供料、升级与场景检查全部通过，综合图校验仍保留这个实际失败。此次验证覆盖受控资源供给和生产行为；完整自然进度、等待时间与长期回本节奏尚未测量。

## 输入与收尾证据

- [input-files.json](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/input-files.json)：本轮前资产、meta、代码与测试原始内容。
- [input-working-tree.patch](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/input-working-tree.patch)：本轮前完整未提交 diff。
- [proposed-values.json](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/proposed-values.json)：最终逐建筑显式值。
- [field-changes.json](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/field-changes.json)：最终 279 项完整前后值。
- [implementation.patch](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/implementation.patch)：仅本轮新增的生产代码与测试差异。
- [definition-verification.json](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/definition-verification.json)：字段、资源流、GUID 与工业旧建造费核对。
- [preservation-verification.json](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/preservation-verification.json)：非本轮既有工作与模拟器 diff 保留核对。
- [closure-verified.md](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/closure-verified.md)：最终静态闭包。
- [edit-verified-command.json](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/edit-verified-command.json)：最终 EditMode 完整过滤器与命令。
- [edit-verified-run.json](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/edit-verified-run.json)：最终 EditMode 进程、时间与退出码。
- [play-verified-command.json](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/play-verified-command.json)：最终 PlayMode 完整过滤器与命令。
- [play-verified-run.json](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/play-verified-run.json)：最终 PlayMode 进程、时间与退出码。
- [font-restored.json](D:/GitHub/Kingdom/data/industrial-feedback-20261110/economy-rebalance/font-restored.json)：字体恢复哈希。

起始与结束 HEAD：`1179f27ce3cc3858961ee4f813d52a81ae5217c5`。最终方案与两次最终运行的输入快照一致。全部建筑 `.meta`、GUID、稳定 ID 与授权字段外序列化保持本轮前值；已有工业建造费逐项相同。非本轮跟踪 diff 完整保留。字体恢复到 `722BFD776012F77B34C3C33AB7BC512BE7DDDDBC4FD9C44709CE0206689C539F`，无字体 diff。没有本轮 Unity 进程、测试存档目录或临时项目残留。原会话的字体备份继续保留；本轮没有重试删除它。

## 全部字段前后值

速率均为资产基础每秒速率，运行时倍率后的结果见上面的真实 Manager 表。连续行属于同一建筑，首行列出文件。

| 建筑 | 字段 | 资源 | 修改前 | 修改后 |
|---|---|---|---:|---:|
| [AdvancedCeramicsPlant](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/AdvancedCeramicsPlant.asset) | `spaceCost` | — | 44 | 140 |
|  | `resourceGenerationRates` | Ceramic（陶瓷） | 2.4 | 10 |
|  | `resourceGenerationRates` | Clay（黏土） | 3 | 12 |
|  | `resourceConsumptionRates` | Coke（焦炭） | 0.88 | 4 |
|  | `resourceConsumptionRates` | Chemical（化工材料） | 0.2 | 1.2 |
|  | `resourceConsumptionRates` | Explosives（工业炸药） | 0.08 | 0.45 |
|  | `resourceConsumptionRates` | Lubricant（润滑剂） | 0.05 | 0.3 |
| [AdvancedMetalRefinery](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/AdvancedMetalRefinery.asset) | `spaceCost` | — | 22 | 84 |
|  | `resourceGenerationRates` | AdvancedMetalIngot（高级金属锭） | 2.1 | 12 |
|  | `resourceConsumptionRates` | AdvancedMetalOre（高级金属矿） | 2 | 16 |
|  | `resourceConsumptionRates` | Chemical（化工材料） | 0.55 | 2.8 |
| [AdvancedMetalSmelter](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/AdvancedMetalSmelter.asset) | `spaceCost` | — | 18 | 72 |
|  | `resourceGenerationRates` | AdvancedMetalIngot（高级金属锭） | 1.2 | 6 |
|  | `resourceConsumptionRates` | AdvancedMetalOre（高级金属矿） | 2 | 10 |
|  | `resourceConsumptionRates` | Chemical（化工材料） | 0.3 | 1.2 |
| [BuildingMaterialsComplex](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/BuildingMaterialsComplex.asset) | `spaceCost` | — | 24 | 100 |
|  | `resourceGenerationRates` | Glass（玻璃） | 1.2 | 8 |
|  | `resourceGenerationRates` | Ceramic（陶瓷） | 0.4 | 2.4 |
|  | `resourceGenerationRates` | Concrete（混凝土） | 2 | 14 |
|  | `resourceConsumptionRates` | StoneBrick（石砖） | 4 | 32 |
|  | `resourceConsumptionRates` | Coke（焦炭） | 0.5 | 4 |
|  | `resourceConsumptionRates` | Chemical（化工材料） | 0.2 | 1.5 |
| [CentralPowerStation](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/CentralPowerStation.asset) | `spaceCost` | — | 28 | 88 |
|  | `resourceConsumptionRates` | Coke（焦炭） | 3 | 14 |
|  | `resourceConsumptionRates` | Coal（煤炭） | 1.5 | 6 |
| [ChemicalPlant](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/ChemicalPlant.asset) | `spaceCost` | — | 18 | 76 |
|  | `resourceGenerationRates` | Chemical（化工材料） | 1.5 | 9 |
|  | `resourceGenerationRates` | Explosives（工业炸药） | 0.35 | 2 |
|  | `resourceGenerationRates` | RocketFuel（火箭燃料） | 0.25 | 1.2 |
|  | `resourceConsumptionRates` | CrudeOil（原油） | 1.5 | 12 |
|  | `resourceConsumptionRates` | Coke（焦炭） | 0.3 | 2 |
| [CokeOven](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/CokeOven.asset) | `spaceCost` | — | 14 | 60 |
|  | `resourceGenerationRates` | Coke（焦炭） | 1.5 | 7.2 |
|  | `resourceConsumptionRates` | Coal（煤炭） | 2 | 10 |
| [IndustrialCarbonizationRetort](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/IndustrialCarbonizationRetort.asset) | `spaceCost` | — | 18 | 96 |
|  | `resourceGenerationRates` | Coke（焦炭） | 4.8 | 28 |
|  | `resourceConsumptionRates` | WoodLog（原木） | 8 | 50 |
| [IndustrialHabitationComplex](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/IndustrialHabitationComplex.asset) | `spaceCost` | — | 42 | 108 |
|  | `resourceConsumptionRates` | Electronics（电子元件） | 0.03 | 0.3 |
|  | `resourceConsumptionRates` | Ceramic（陶瓷） | 0.02 | 0.2 |
|  | `resourceConsumptionRates` | Steel（钢材） | 0.02 | 0.15 |
|  | `resourceConsumptionRates` | Concrete（混凝土） | 0.03 | 0.3 |
| [IndustrialMetalSmelter](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/IndustrialMetalSmelter.asset) | `spaceCost` | — | 18 | 78 |
|  | `resourceGenerationRates` | BasicMetalIngot（基础金属锭） | 7.6 | 40 |
|  | `resourceGenerationRates` | Steel（钢材） | 0.6 | 12 |
|  | `resourceConsumptionRates` | BasicMetalOre（低级金属矿） | 8.8 | 65 |
|  | `resourceConsumptionRates` | Coke（焦炭） | 1.35 | 8 |
| [IndustrialOilExtractionComplex](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/IndustrialOilExtractionComplex.asset) | `spaceCost` | — | 24 | 120 |
|  | `resourceGenerationRates` | CrudeOil（原油） | 12 | 60 |
|  | `resourceConsumptionRates` | Lubricant（润滑剂） | 0.06 | 0.55 |
|  | `resourceConsumptionRates` | Explosives（工业炸药） | 0.18 | 1 |
| [IndustrialStoneworks](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/IndustrialStoneworks.asset) | `spaceCost` | — | 20 | 104 |
|  | `resourceGenerationRates` | StoneChunk（石块） | 12 | 64 |
|  | `resourceGenerationRates` | StoneBrick（石砖） | 5 | 28 |
|  | `resourceConsumptionRates` | Lubricant（润滑剂） | 0.08 | 0.6 |
|  | `resourceConsumptionRates` | Explosives（工业炸药） | 0.06 | 0.5 |
| [IntegratedMetalMine](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/IntegratedMetalMine.asset) | `spaceCost` | — | 18 | 96 |
|  | `resourceGenerationRates` | SpecialMetalOre（特级金属矿） | 1.6 | 12 |
|  | `resourceGenerationRates` | AdvancedMetalOre（高级金属矿） | 3.2 | 20 |
|  | `resourceGenerationRates` | BasicMetalOre（低级金属矿） | 11.2 | 90 |
|  | `resourceConsumptionRates` | Explosives（工业炸药） | 0.15 | 1.2 |
| [IntegratedPetrochemicalComplex](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/IntegratedPetrochemicalComplex.asset) | `spaceCost` | — | 28 | 120 |
|  | `resourceGenerationRates` | RefinedFuel（精炼燃料） | 4 | 18 |
|  | `resourceGenerationRates` | Lubricant（润滑剂） | 1.3 | 6 |
|  | `resourceGenerationRates` | Rubber（橡胶） | 1 | 5 |
|  | `resourceGenerationRates` | Chemical（化工材料） | 0.6 | 4 |
|  | `resourceGenerationRates` | Explosives（工业炸药） | 0.8 | 3.5 |
|  | `resourceConsumptionRates` | CrudeOil（原油） | 7 | 38 |
|  | `resourceConsumptionRates` | Electronics（电子元件） | 0.06 | 0.4 |
| [MachineFactory](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/MachineFactory.asset) | `spaceCost` | — | 20 | 92 |
|  | `resourceGenerationRates` | Machinery（机械部件） | 1 | 5 |
|  | `resourceGenerationRates` | Engine（发动机） | 0.2 | 1 |
|  | `resourceGenerationRates` | Composite（复合材料） | 0.45 | 2.4 |
|  | `resourceConsumptionRates` | Steel（钢材） | 0.8 | 7 |
|  | `resourceConsumptionRates` | CopperWire（铜线） | 0.3 | 2 |
|  | `resourceConsumptionRates` | BasicMetalIngot（基础金属锭） | 0.5 | 3 |
| [MechanizedCoalMine](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/MechanizedCoalMine.asset) | `spaceCost` | — | 22 | 96 |
|  | `resourceGenerationRates` | Coal（煤炭） | 8 | 44 |
|  | `resourceConsumptionRates` | Explosives（工业炸药） | 0.08 | 0.8 |
| [MechanizedLumberyard](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/MechanizedLumberyard.asset) | `spaceCost` | — | 24 | 108 |
|  | `resourceGenerationRates` | WoodLog（原木） | 18 | 90 |
|  | `resourceConsumptionRates` | Coke（焦炭） | 0.35 | 2 |
| [MechanizedTextileMill](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/MechanizedTextileMill.asset) | `spaceCost` | — | 18 | 76 |
|  | `resourceGenerationRates` | Cloth（布料） | 10 | 55 |
|  | `resourceConsumptionRates` | Biomass（生物质） | 20 | 130 |
|  | `resourceConsumptionRates` | Lubricant（润滑剂） | 0.12 | 0.8 |
| [OilDerrick](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/OilDerrick.asset) | `spaceCost` | — | 14 | 54 |
|  | `powerContribution` | — | -8 | -16 |
|  | `resourceGenerationRates` | CrudeOil（原油） | 3 | 9 |
| [OilRefinery](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/OilRefinery.asset) | `spaceCost` | — | 20 | 80 |
|  | `resourceGenerationRates` | RefinedFuel（精炼燃料） | 1.2 | 6 |
|  | `resourceGenerationRates` | Lubricant（润滑剂） | 0.4 | 2 |
|  | `resourceGenerationRates` | Rubber（橡胶） | 0.3 | 1.5 |
|  | `resourceConsumptionRates` | CrudeOil（原油） | 2 | 10 |
|  | `resourceConsumptionRates` | Chemical（化工材料） | 0.2 | 1 |
| [PlantingField](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/PlantingField.asset) | `spaceCost` | — | 16 | 64 |
|  | `resourceGenerationRates` | Biomass（生物质） | 18 | 80 |
|  | `resourceConsumptionRates` | Chemical（化工材料） | 0.25 | 1.6 |
| [RailHub](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/RailHub.asset) | `spaceCost` | — | 25 | 84 |
|  | `resourceConsumptionRates` | Coke（焦炭） | 0.3 | 2.5 |
|  | `resourceConsumptionRates` | Lubricant（润滑剂） | 0.05 | 0.3 |
|  | `resourceConsumptionRates` | Engine（发动机） | 0.05 | 0.35 |
|  | `resourceConsumptionRates` | Machinery（机械部件） | 0.08 | 0.65 |
|  | `resourceConsumptionRates` | Explosives（工业炸药） | 0.03 | 0.25 |
| [SteamPlant](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/SteamPlant.asset) | `spaceCost` | — | 18 | 72 |
|  | `resourceConsumptionRates` | Coal（煤炭） | 2 | 8 |
| [TitaniumAlloySmelter](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/TitaniumAlloySmelter.asset) | `spaceCost` | — | 28 | 112 |
|  | `resourceGenerationRates` | TitaniumAlloy（钛合金） | 0.45 | 2.4 |
|  | `resourceConsumptionRates` | SpecialMetalOre（特级金属矿） | 2.4 | 13 |
|  | `resourceConsumptionRates` | Chemical（化工材料） | 0.8 | 4 |
|  | `resourceConsumptionRates` | Coke（焦炭） | 0.35 | 2 |
|  | `resourceConsumptionRates` | AdvancedMetalIngot（高级金属锭） | 0.12 | 0.9 |
|  | `resourceConsumptionRates` | AdvancedMetalOre（高级金属矿） | 0.18 | 1 |
|  | `resourceConsumptionRates` | Ceramic（陶瓷） | 0.10 | 0.6 |
| [University](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/University.asset) | `spaceCost` | — | 12 | 42 |
|  | `resourceConsumptionRates` | Electronics（电子元件） | 0.02 | 0.25 |
|  | `resourceConsumptionRates` | Glass（玻璃） | 0.05 | 0.5 |
| [WireMill](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Industrial/WireMill.asset) | `spaceCost` | — | 16 | 68 |
|  | `resourceGenerationRates` | CopperWire（铜线） | 1.4 | 7 |
|  | `resourceGenerationRates` | Electronics（电子元件） | 0.35 | 1.6 |
|  | `resourceConsumptionRates` | BasicMetalIngot（基础金属锭） | 1.7 | 10 |
|  | `resourceConsumptionRates` | Ceramic（陶瓷） | 0.1 | 0.7 |
| [DeepSpaceObservatory](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/DeepSpaceObservatory.asset) | `resourceConsumptionRates` | Electronics（电子元件） | 0.12 | 1.2 |
|  | `resourceConsumptionRates` | TitaniumAlloy（钛合金） | 0.05 | 0.3 |
|  | `resourceConsumptionRates` | Ceramic（陶瓷） | 0.08 | 0.5 |
|  | `resourceConsumptionRates` | RocketFuel（火箭燃料） | 0.03 | 0.2 |
|  | `resourceConsumptionRates` | PhantomWeave（幽影纤维） | 0.02 | 0.08 |
|  | `resourceConsumptionRates` | PhaseMaterial（相位材料） | 0.02 | 0.06 |
|  | `resourceConsumptionRates` | Composite（复合材料） | 0.03 | 0.25 |
|  | `resourceConsumptionRates` | Glass（玻璃） | 0.05 | 0.8 |
| [DeepSpaceRelay](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/DeepSpaceRelay.asset) | `resourceConsumptionRates` | Electronics（电子元件） | 0.03 | 0.6 |
|  | `resourceConsumptionRates` | RocketFuel（火箭燃料） | 0.05 | 0.4 |
|  | `resourceConsumptionRates` | PhantomAlloy（幽影合金） | 0.02 | 0.1 |
|  | `resourceConsumptionRates` | PhaseMaterial（相位材料） | 0.025 | 0.08 |
|  | `resourceConsumptionRates` | Steel（钢材） | 0.04 | 0.4 |
|  | `resourceConsumptionRates` | Glass（玻璃） | 0.03 | 0.6 |
|  | `resourceConsumptionRates` | Composite（复合材料） | 0.025 | 0.25 |
| [InterstellarTheoryNexus](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/InterstellarTheoryNexus.asset) | `resourceConsumptionRates` | Electronics（电子元件） | 1.2 | 6 |
|  | `resourceConsumptionRates` | PhantomAlloy（幽影合金） | 0.08 | 0.3 |
|  | `resourceConsumptionRates` | PhantomWeave（幽影纤维） | 0.06 | 0.25 |
|  | `resourceConsumptionRates` | PhaseMaterial（相位材料） | 0.04 | 0.16 |
| [LaunchCenter](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/LaunchCenter.asset) | `spaceCost` | — | 360 | 800 |
|  | `resourceRequirements` | Steel（钢材） | 1200 | 8000 |
|  | `resourceRequirements` | Machinery（机械部件） | 650 | 2500 |
|  | `resourceRequirements` | Electronics（电子元件） | 300 | 1800 |
|  | `resourceRequirements` | RefinedFuel（精炼燃料） | 300 | 2400 |
|  | `resourceRequirements` | Engine（发动机） | 100 | 400 |
|  | `resourceRequirements` | AdvancedMetalIngot（高级金属锭） | 250 | 1600 |
|  | `resourceRequirements` | Concrete（混凝土） | 900 | 6000 |
|  | `resourceRequirements` | TitaniumAlloy（钛合金） | 260 | 1000 |
|  | `resourceRequirements` | Composite（复合材料） | 500 | 2200 |
|  | `resourceConsumptionRates` | RocketFuel（火箭燃料） | 0.08 | 0.6 |
|  | `resourceConsumptionRates` | Engine（发动机） | 0.02 | 0.12 |
|  | `resourceConsumptionRates` | Electronics（电子元件） | 0.03 | 0.25 |
|  | `resourceConsumptionRates` | TitaniumAlloy（钛合金） | 0.03 | 0.15 |
|  | `resourceConsumptionRates` | Composite（复合材料） | 0.025 | 0.2 |
| [OrbitalAgroecologyArray](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/OrbitalAgroecologyArray.asset) | `resourceGenerationRates` | Biomass（生物质） | 180 | 400 |
|  | `resourceGenerationRates` | WoodLog（原木） | 52 | 140 |
|  | `resourceConsumptionRates` | Chemical（化工材料） | 1.2 | 6 |
|  | `resourceConsumptionRates` | Electronics（电子元件） | 0.10 | 0.6 |
|  | `resourceConsumptionRates` | Lubricant（润滑剂） | 0.12 | 0.6 |
|  | `resourceConsumptionRates` | PhantomWeave（幽影纤维） | 0.02 | 0.08 |
|  | `resourceConsumptionRates` | PhaseMaterial（相位材料） | 0.02 | 0.05 |
|  | `resourceConsumptionRates` | Coke（焦炭） | 0.8 | 3 |
|  | `resourceConsumptionRates` | TitaniumAlloy（钛合金） | 0.05 | 0.25 |
|  | `resourceConsumptionRates` | Composite（复合材料） | 0.03 | 0.2 |
| [OrbitalBuildingMaterialsWorks](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/OrbitalBuildingMaterialsWorks.asset) | `resourceGenerationRates` | Glass（玻璃） | 4.8 | 36 |
|  | `resourceGenerationRates` | Ceramic（陶瓷） | 1.6 | 11 |
|  | `resourceGenerationRates` | Concrete（混凝土） | 8 | 64 |
|  | `resourceConsumptionRates` | TitaniumAlloy（钛合金） | 0.4 | 2.5 |
|  | `resourceConsumptionRates` | StoneBrick（石砖） | 0 | 156 |
|  | `resourceConsumptionRates` | Clay（黏土） | 0 | 18 |
|  | `resourceConsumptionRates` | Coke（焦炭） | 0 | 18 |
|  | `resourceConsumptionRates` | Chemical（化工材料） | 0 | 9 |
| [OrbitalCarbonizationComplex](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/OrbitalCarbonizationComplex.asset) | `resourceGenerationRates` | Coke（焦炭） | 18 | 110 |
|  | `resourceConsumptionRates` | WoodLog（原木） | 26 | 220 |
|  | `resourceConsumptionRates` | Electronics（电子元件） | 0.08 | 0.4 |
|  | `resourceConsumptionRates` | PhantomAlloy（幽影合金） | 0.025 | 0.08 |
|  | `resourceConsumptionRates` | PhaseMaterial（相位材料） | 0.012 | 0.04 |
|  | `resourceConsumptionRates` | TitaniumAlloy（钛合金） | 0.06 | 0.2 |
|  | `resourceConsumptionRates` | Composite（复合材料） | 0.04 | 0.2 |
| [OrbitalCryogenicPropellantArray](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/OrbitalCryogenicPropellantArray.asset) | `resourceGenerationRates` | RocketFuel（火箭燃料） | 14 | 30 |
|  | `resourceConsumptionRates` | RefinedFuel（精炼燃料） | 8 | 24 |
|  | `resourceConsumptionRates` | Chemical（化工材料） | 3 | 10 |
|  | `resourceConsumptionRates` | Electronics（电子元件） | 0.12 | 0.6 |
|  | `resourceConsumptionRates` | TitaniumAlloy（钛合金） | 0.04 | 0.2 |
|  | `resourceConsumptionRates` | PhantomAlloy（幽影合金） | 0.025 | 0.1 |
|  | `resourceConsumptionRates` | PhaseMaterial（相位材料） | 0.01 | 0.04 |
|  | `resourceConsumptionRates` | Composite（复合材料） | 0.03 | 0.2 |
| [OrbitalHabitatMegastructure](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/OrbitalHabitatMegastructure.asset) | `resourceConsumptionRates` | TitaniumAlloy（钛合金） | 0.12 | 0.8 |
|  | `resourceConsumptionRates` | Composite（复合材料） | 0.08 | 0.6 |
|  | `resourceConsumptionRates` | Electronics（电子元件） | 0.10 | 0.8 |
|  | `resourceConsumptionRates` | PhantomWeave（幽影纤维） | 0.04 | 0.16 |
|  | `resourceConsumptionRates` | PhaseMaterial（相位材料） | 0.02 | 0.08 |
|  | `resourceConsumptionRates` | Biomass（生物质） | 12 | 80 |
|  | `resourceConsumptionRates` | Chemical（化工材料） | 0.04 | 0.4 |
|  | `resourceConsumptionRates` | Rubber（橡胶） | 0.03 | 0.25 |
|  | `spaceCost` | — | 650 | 1200 |
| [OrbitalMachiningComplex](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/OrbitalMachiningComplex.asset) | `resourceGenerationRates` | Machinery（机械部件） | 4 | 22 |
|  | `resourceGenerationRates` | Engine（发动机） | 0.8 | 4.2 |
|  | `resourceGenerationRates` | Composite（复合材料） | 1.8 | 10.8 |
|  | `resourceConsumptionRates` | TitaniumAlloy（钛合金） | 0.6 | 20 |
|  | `resourceConsumptionRates` | Electronics（电子元件） | 0.5 | 10 |
|  | `resourceConsumptionRates` | AdvancedMetalIngot（高级金属锭） | 0 | 8 |
| [OrbitalSolarArray](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/OrbitalSolarArray.asset) | `resourceConsumptionRates` | Composite（复合材料） | 0.03 | 0.2 |
|  | `resourceConsumptionRates` | TitaniumAlloy（钛合金） | 0.04 | 0.3 |
|  | `resourceConsumptionRates` | AdvancedMetalIngot（高级金属锭） | 0.04 | 0.3 |
|  | `resourceConsumptionRates` | CopperWire（铜线） | 0.03 | 0.4 |
|  | `resourceConsumptionRates` | Electronics（电子元件） | 0.08 | 0.6 |
|  | `resourceConsumptionRates` | Glass（玻璃） | 0.20 | 1.2 |
| [OrbitalStation](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/OrbitalStation.asset) | `resourceConsumptionRates` | RefinedFuel（精炼燃料） | 0.05 | 0.6 |
|  | `resourceConsumptionRates` | Composite（复合材料） | 0.07 | 0.5 |
|  | `resourceConsumptionRates` | Electronics（电子元件） | 0.08 | 0.8 |
|  | `resourceConsumptionRates` | PhantomWeave（幽影纤维） | 0.05 | 0.12 |
|  | `resourceConsumptionRates` | PhaseMaterial（相位材料） | 0.02 | 0.08 |
|  | `resourceConsumptionRates` | TitaniumAlloy（钛合金） | 0.12 | 0.6 |
|  | `resourceConsumptionRates` | AdvancedMetalIngot（高级金属锭） | 0.04 | 0.3 |
|  | `resourceConsumptionRates` | Biomass（生物质） | 2.62 | 16 |
|  | `resourceConsumptionRates` | Rubber（橡胶） | 1.2 | 3 |
|  | `resourceConsumptionRates` | Ceramic（陶瓷） | 0.08 | 0.5 |
|  | `resourceConsumptionRates` | Cloth（布料） | 1.5 | 8 |
|  | `resourceConsumptionRates` | RocketFuel（火箭燃料） | 0.25 | 1.2 |
|  | `resourceConsumptionRates` | Lubricant（润滑剂） | 0.06 | 0.3 |
| [OrbitalWireWorks](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/OrbitalWireWorks.asset) | `resourceGenerationRates` | CopperWire（铜线） | 5.6 | 31 |
|  | `resourceGenerationRates` | Electronics（电子元件） | 1.4 | 7.2 |
|  | `resourceConsumptionRates` | TitaniumAlloy（钛合金） | 0.3 | 1.8 |
|  | `resourceConsumptionRates` | BasicMetalIngot（基础金属锭） | 0 | 46 |
|  | `resourceConsumptionRates` | Ceramic（陶瓷） | 0 | 4 |
| [PhantomMaterialsFabricator](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/PhantomMaterialsFabricator.asset) | `spaceCost` | — | 360 | 600 |
|  | `resourceGenerationRates` | PhantomAlloy（幽影合金） | 0.06 | 0.4 |
|  | `resourceGenerationRates` | PhantomWeave（幽影纤维） | 0.04 | 0.3 |
|  | `resourceConsumptionRates` | TitaniumAlloy（钛合金） | 0.6 | 6 |
|  | `resourceConsumptionRates` | Composite（复合材料） | 0.35 | 4 |
|  | `resourceConsumptionRates` | Chemical（化工材料） | 0.3 | 3 |
|  | `resourceConsumptionRates` | Electronics（电子元件） | 0.04 | 0.4 |
|  | `resourceConsumptionRates` | AdvancedMetalIngot（高级金属锭） | 0.15 | 2 |
|  | `resourceConsumptionRates` | Rubber（橡胶） | 0.08 | 0.8 |
|  | `resourceConsumptionRates` | Glass（玻璃） | 0.12 | 1.5 |
|  | `resourceConsumptionRates` | Cloth（布料） | 0.1 | 2 |
|  | `resourceConsumptionRates` | Coke（焦炭） | 0.06 | 0.8 |
| [PhaseMaterialSynthesisArray](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/PhaseMaterialSynthesisArray.asset) | `spaceCost` | — | 520 | 800 |
|  | `resourceGenerationRates` | PhaseMaterial（相位材料） | 0.08 | 0.45 |
|  | `resourceConsumptionRates` | PhantomAlloy（幽影合金） | 0.11 | 0.8 |
|  | `resourceConsumptionRates` | PhantomWeave（幽影纤维） | 0.09 | 0.6 |
|  | `resourceConsumptionRates` | TitaniumAlloy（钛合金） | 0.08 | 3 |
|  | `resourceConsumptionRates` | Composite（复合材料） | 0.25 | 8 |
|  | `resourceConsumptionRates` | AdvancedMetalIngot（高级金属锭） | 0.05 | 1.2 |
|  | `resourceConsumptionRates` | Electronics（电子元件） | 0.04 | 0.8 |
|  | `resourceConsumptionRates` | Ceramic（陶瓷） | 0.10 | 1.2 |
|  | `resourceConsumptionRates` | Chemical（化工材料） | 0.12 | 1.5 |
|  | `resourceConsumptionRates` | Coke（焦炭） | 0.12 | 1.6 |
| [QuantumComputingArray](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/QuantumComputingArray.asset) | `resourceConsumptionRates` | Electronics（电子元件） | 0.8 | 3.2 |
|  | `resourceConsumptionRates` | PhantomAlloy（幽影合金） | 0.05 | 0.25 |
|  | `resourceConsumptionRates` | PhantomWeave（幽影纤维） | 0.04 | 0.2 |
|  | `resourceConsumptionRates` | AdvancedMetalIngot（高级金属锭） | 0.06 | 0.6 |
|  | `resourceConsumptionRates` | TitaniumAlloy（钛合金） | 0.08 | 0.8 |
|  | `resourceConsumptionRates` | PhaseMaterial（相位材料） | 0.03 | 0.12 |
|  | `resourceConsumptionRates` | Ceramic（陶瓷） | 0.08 | 0.8 |
|  | `resourceConsumptionRates` | Chemical（化工材料） | 0.10 | 0.6 |
| [Shipyard](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Spacer/Shipyard.asset) | `resourceConsumptionRates` | RefinedFuel（精炼燃料） | 0.15 | 1.2 |
|  | `resourceConsumptionRates` | Lubricant（润滑剂） | 0.08 | 0.4 |
|  | `resourceConsumptionRates` | TitaniumAlloy（钛合金） | 0.12 | 0.8 |
|  | `resourceConsumptionRates` | Composite（复合材料） | 0.04 | 0.5 |
|  | `resourceConsumptionRates` | Machinery（机械部件） | 0.18 | 1.2 |
|  | `resourceConsumptionRates` | Engine（发动机） | 0.12 | 0.4 |
|  | `resourceConsumptionRates` | Rubber（橡胶） | 0.10 | 0.5 |
| [AutonomousMatterFabricator](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Ultra/AutonomousMatterFabricator.asset) | `resourceGenerationRates` | PhantomAlloy（幽影合金） | 0.9 | 6 |
|  | `resourceGenerationRates` | PhantomWeave（幽影纤维） | 0.6 | 4 |
|  | `resourceGenerationRates` | PhaseMaterial（相位材料） | 0.4 | 2.4 |
|  | `resourceGenerationRates` | Composite（复合材料） | 5.0 | 24 |
|  | `resourceConsumptionRates` | TitaniumAlloy（钛合金） | 0.9 | 72 |
|  | `resourceConsumptionRates` | Electronics（电子元件） | 0.55 | 24 |
|  | `resourceConsumptionRates` | Machinery（机械部件） | 0.25 | 18 |
|  | `resourceConsumptionRates` | AdvancedMetalIngot（高级金属锭） | 0.3 | 24 |
| [PhaseEnergyArray](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Ultra/PhaseEnergyArray.asset) | `resourceConsumptionRates` | PhantomAlloy（幽影合金） | 0.06 | 0.4 |
|  | `resourceConsumptionRates` | Electronics（电子元件） | 0.05 | 1.2 |
|  | `resourceConsumptionRates` | PhaseMaterial（相位材料） | 0.04 | 0.2 |
| [UltraComputingNexus](D:/GitHub/Kingdom/Assets/Resources/Datas/Building/Ultra/UltraComputingNexus.asset) | `resourceConsumptionRates` | Electronics（电子元件） | 0.12 | 3.6 |
|  | `resourceConsumptionRates` | PhaseMaterial（相位材料） | 0.05 | 0.3 |
