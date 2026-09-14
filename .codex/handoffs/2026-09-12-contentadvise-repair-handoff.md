# 2026-09-12 CONTENTADVISE 建议修缮 handoff

## Status

按 `CONTENTADVISE/WORTH-DOING.md` 的批次计划完成批次 0/1/2/3、批次 4 的 B12+B17+B20+B08 前置（balance-model 时代折算带）、批次 5 的 E29+B27+E08 立项。
**Unity 2022.3.62f3c1 EditMode 全量 661/661 通过**（TestResults/EditMode-20260912-run6.xml；Console 无编译错误、无校验器告警）；静态闭合检查 6 次全绿。

## Scope and method

- 输入：`CONTENTADVISE`（70 条判定，E01-E36 / B01-B34）与各领域审计报告。
- 每处资产修改前先读定义与消费端代码（ResearchEffect.cs / ProgressionModifierManager.cs / SectorValidator.cs / SaveManager.cs）并核对测试锁定情况（EarlyVerticalSlicePacingTests、SectorManagerTests、SpacerResearchPlacementTests、ResearchBalanceTests、C6 系列均未锁本次修改值；TerritoryReward 精确值被锁，故未动）。
- 每批修改后运行 `tools/codex/content-closure-check.ps1`，四次全部通过（81/81、47/47、50/50、16/16、40 资源不变）。

## Changes（文件级 before/after）

### 批次 0：测试修复
- `Assets/Tests/Editor/FlowEfficiencyTests.cs:65`：`Is.EqualTo(new ExpantaNum(1d/11d))` → `result.ToDouble() Within 1e-6`。根因：ExpantaNum 构造值量化（0.090909）与公式全精度值精确比较必然失败（B24）。
- `Assets/Tests/Editor/SectorBuildingTests.cs`：SaveManager 创建后补 `LoadOrCreateGame()`（断言返回 False）。根因：SaveNow 的 `!ready` 守卫需要 Bootstrap 完成；KingdomLogicTests 同款模式。插入点在 AdvanceToSpacer 之前，新游戏分支的状态重置不影响后续建造断言。
- 同文件新增 `EarthMoonLogisticsHubStaysUniqueUntilCostGrowthIsFixed`（B20）：锁定 `MaxAmount == 1`（离散计数，精确断言），防止 growth=1.01 被放开后变成线性成本漏洞。

### 批次 1：笔误/死效果修复（6 处资产）
- `Research/Animal/Woodworking.asset`：Type 5 全局建造乘数 0.95 → 1.05（加法堆叠下 <1 恒无效；对齐同代带 1.02-1.05）（B11）。
- `Research/Spacer/MatterStateControlTheory.asset`：删除 Type 22 value 0.12 拆除返还（恒被 Industrialization 0.5 与 OrbitalHabitation 0.9 的 Max 压制，纯死条目）（B11）。注意：该资产 Description 仍提及"提高拆除返还"，按规则未改描述，待设计定夺（要么补 >0.9 的新返还值，要么改文案）。
- `Research/Ultra/TechnologicalSingularity.asset`：`effects: []` → Type 4 全局研究乘数 1.25（带 1.05-1.35，克制取值）（B09）。成本 5.5e11 未动（B34 属节奏项，暂缓）。
- `Research/Industrial/IndustrialAgriculture.asset`：Type 32 全局食物乘数 6.075 → 1.5（同类带 1.2-1.5 顶格；离群 4 倍）（B05）。满载人口-食物核算在新值下仍成立（Industrial 960 人 768/s vs 1 座 PlantingField×新堆叠 ≈2200/s）。
- `Building/Industrial/RareMetalMine.asset`：productivityConsumption 12 → 120（同梯队机械化矿场 120-130）（B15）。
- `Building/Industrial/TitaniumMetallurgicalComplex.asset`：productivityConsumption 12 → 95（对标 NickelRefinery 95）（B15）。

### 批次 2：对账基线（子代理产出，纯文档）
- `CONTENTADVISE/batch2-payback-and-duration-table.md`（E34/B29）：66 建筑回本全量表 + 129 科研时长全量表 + 偏离前 10 定案清单 + 时代折算带建议。
- `CONTENTADVISE/batch2-resource-sink-matrix.md`（E36）：40 资源×时代 sink 矩阵；新发现 StoneChunk 断链（TL3 起 0 消费且 IndustrialStoneworks 反产 12/s）与 DeepSpaceRelay 物流产 25/耗 48 疑似字段写反（待运行时核对）。

### 批次 3：战役重定标（E09/E19/E20/B01/B02/B33）
- `Building/Spacer/PhaseMaterialSynthesisArray.asset`：PhaseMaterial 产率 0.008 → 0.08（审计建议带 0.05-0.15 中值）。
- 四个远星战役补给流按"现实可建建筑数 × 单产 × 时代层级（Alpha 0.5 / Proxima 0.8 / TauCeti 1.2 / Sirius 1.6）"重定，例如 Sirius：RocketFuel 3666.67→360、PhaseMaterial 800→2.5、Engine 1200→4.8、Electronics 1666.67→11.2；Food 流全部保持不变（本就可覆盖）。
- 四战役奖励重定：Alpha 85k→1.5M、Proxima 100k→3M、TauCeti 135k→6M、Sirius 320k→14M（约为非食物投入的 10-15%），TerritoryReward 精确值保持（被 SectorManagerTests:611-614 锁定）。
- `Script/Validation/SectorValidator.cs`：新增 `MinimumInterstellarResourceReward = 1e6` 下限校验（远星战役资源奖励总量），防空洞奖励合法通过（E20/B27 部分）。已确认无测试用小奖励夹具直接调该验证器。

### 批次 4（部分）：成本增长率回归带与节奏批首批（B12/B17/B08 前置）
- `QuantumComputingArray` 1.28→1.20、`DeepSpaceObservatory` 1.24→1.20、`DeepSpaceRelay` 1.24→1.20、`InterstellarTheoryNexus` 1.30→1.20（研究/基础设施带 1.18-1.20）、`PhaseMaterialSynthesisArray` 1.26→1.17（加工带 1.15-1.17）。五座均无 maxAmount 字段（可无限建造），growth 有效。（B17）
- B12 快侧四条科研 ×10（对齐 batch2 表"≥2-5min；确认漏一个数量级"结论）：`MechanicalEngineering` 13392→133920、`Steelmaking` 18144→181440、`SteamPower` 75000→750000、`IndustrialWorkshop` 90000→900000；`EarlyVerticalSlicePacingTests` 钉值同步为 181440/133920（Industrialization 336000 不变）。已确认 75000/90000 无其他测试锁定。
- `docs/balance/balance-model.md` 新增 §8"时代折算带"（原料回本 5-15min；加工 15-40min；科研单条 10-60min；门 0.5-3h 或按上一时代总量 10-20% 重推；Spacer 尾盘合计 1-2 天）——batch2 对账表明确要求先补此口径再定 TL3/TL4 数值；标注为静态暂定、须运行时校准。（B08 前置）
- B08 Spacer 全量重定标、B18 回本带重定、B32 轨道前科研墙：待该口径经运行时/实玩校准后定值（防盲调）。

### 批次 5（无 Unity 依赖的可落地部分）
- `Research/Spacer/OrbitalAgroecology.asset`（E29）：新增 Type 1 建筑产能乘数 1.2 → OrbitalAgroecologyArray（guid 7a4b2c1d9e8f6071528394a5b6c7d8e9），修复"乘数只强化上代 PlantingField、Spacer 唯一食物建筑无联动"的错位。
- `Assets/Tests/Editor/ContentProgressionValidatorTests.cs`（B27）：`EveryReleasedResource_HasSourceAndSink` 移除 27 资源硬编码清单（ReleasedVerticalSliceResourceIds 已删除），审计覆盖全部已定义资源——新增第 41 个资源自动纳入 source/sink 检查。切换前已逐资源核对窄口径 sink（建筑消耗/建造需求/科研需求）：13 个未列资源全部有建筑级 sink（Concrete 22 处、BauxiteOre→AluminumSmelter、RocketFuel→InterstellarNavigation 科研需求、Nickel→PMSA 消耗等）。
- `Script/Validation/EconomyDependencyValidator.cs`（B27）：新增 `ValidateResearchEffects`——效果为空的科研硬失败（内容质量门槛"新科研必须有真实效果"自动化）。切换前已确认全库 129 条科研 `effects: []` 零命中。
- E08 立项：`CONTENTADVISE/e08-single-source-upgrade-chain-spec.md`——三座 Spacer 续作（OrbitalMachiningComplex/OrbitalWireWorks/OrbitalBuildingMaterialsWorks，约 4x 产能、成本维护用高级结构材料、growth 1.16、复用既有 Spacer 科研解锁），待实施。

## Unity 验证（EditMode 全量）

- 命令：`Unity.exe -batchmode -projectPath D:\GitHub\Kingdom -runTests -testPlatform EditMode -testResults TestResults\EditMode-20260912-runN.xml -logFile ...`，共 6 轮。
- run1（660/661）：暴露 SectorBuildingTests 的三层过期测试契约，逐一修复：
  1. **支付台账**：原助手反射 `SetStatus(Completed)` 绕过支付，存档被 "Research payment ledger is incomplete" 拒绝 → 助手改为先 `ResearchManager.TryPayResearchCost`（公开 API）补台账；
  2. **前置连贯性**："Completed research prerequisite is missing" → 助手改为递归完成全部前置链（与运行时 BuildPrerequisiteBatch 语义一致）；
  3. **星区占领连贯性**："星区不能在未解锁时处于占领状态"（SectorState.cs:123）→ 测试补 `SetUnlockedForEditor(true)`。
  同时把测试引导改为在隔离存档根上 `LoadOrCreateGame()`（不再依赖用户默认存档状态）。
- run2：`FindObjectOfType` 需 `UnityEngine.Object.` 限定（测试类非 MonoBehaviour）→ 修复。
- run4/run5：run4 的 Unity 进程退出挂起残留项目锁 → 终止僵尸进程后重跑。
- **run6：661/661 Passed**；Console 检查无编译错误（CompileScripts 3.2s）、无 EconomyDependencyValidator/SectorValidator 告警；`TestResults/Latest-Test-Errors.txt` 已更新为本次通过记录。

## Validation performed

- 静态闭合检查每批后运行共 5 次，全部通过，数值与基线一致。
- 全库扫描复核：`effects: []` 0 命中；旧错误值（6.075/0.95/0.12/0.008/productivityConsumption 12）0 残留。
- 修改前逐条核对测试锁定（详见 Scope）；本次未改任何被锁定数值。
- 折算/等待时长均为静态推算；结果对工坊乘数与 RP 情景的敏感性已在 batch2 文档声明。
- **Unity 2022.3.62f3c1 真实编译与 EditMode 全量通过（661/661）**，Console 检查干净，详见上文"Unity 验证"节。

## Remaining risks and caveats

1. **生产力池收紧**：RareMetalMine/TMC 的 productivityConsumment 大幅上调会吃掉 Spacer 均衡 5-10% 余量的一部分，需要运行时核对生产力满足率（batch2 回本表已含新值推算）。
2. **战役重定标幅度大**（供给侧 100-500 倍下调、奖励侧 10-40 倍上调）：单调性与校验器约束已静态验证，但实际体感、与 Colonization/占领流的互动需 PlayMode 证据。
3. B08（TL3→TL4 科研成本全量重定标）、B18（回本带重定）、B32（轨道前科研墙）仍属节奏调整，其口径已补入 balance-model §8；待运行时/实玩校准后按 batch2 表定值（B12 已在本轮完成）。
4. MatterStateControlTheory 的描述与效果不再一致（见批次 1）；TechnologicalSingularity 成本未动，剧情终章 17/18 仍依赖它。
5. DeepSpaceRelay 物流产 25/耗 48 疑似写反（batch2 新发现）待运行时核对后再动。
6. 批次 5 剩余为新增内容（E02-E08 科研/建筑延线、E14 中古食物建筑、E18/E30 容量、E21/E23 星区、E26-E28 剧情教程、E33 食物工坊重定位、B27 的 growth 分带断言需先建建筑分类元数据），按 kingdom-content-expansion 交付顺序须在 Unity 验收门通过后展开，建议按批独立立项。

## Next concrete action

1. ~~在 Unity 中运行 EditMode 全量测试~~ 已完成：661/661 通过，Console 干净（run6）。
2. 用运行时证据定案三件悬案：生产力满足率（B15 后）、DeepSpaceRelay 物流字段方向、工坊每建筑研究倍率是否逆转 University 支配（B16）。
3. 依据运行时/实玩校准 balance-model §8 时代折算带，随后定值 B08（Spacer 全量重定标）/B18（回本带）/B32（轨道前科研墙）。
4. 实施 E08（章程见 `CONTENTADVISE/e08-single-source-upgrade-chain-spec.md`）：三座 Spacer 续作建筑 + upgradeTo 链接 + 闭合检查与回本复核；随后按 WORTH-DOING 顺序推进批次 5 其余内容（E02/E03/E14/E18/E21/E23/E26-E28/E30）。

## 2026-09-14 补充：Ultra 科幻策划文档交付（未实施）

- 用户授权：完成科幻参考与项目现状核验，交付智能体友好文档/PDF，清理本轮临时产物和不必要临时备份。未授权改变时代冻结或实现玩法。
- 正文：`outputs/Kingdom_Ultra_SF_Design_2026-09-14.md`；同源PDF：`outputs/Kingdom_Ultra_SF_Design_2026-09-14.pdf`。旧2026-09-13 Ultra HTML/PDF及其他任务报告不覆盖、不清理。
- 内容：三体、战锤40K、太空无垠、沙丘、时间之子、基地六类母题；事实/推断/提案/待验证标签；产业闭环→工程验证→遗迹分支顺序；七类机制、三种模式、回声铸造环章节、实现边界与12项未来验收用例。所有玩法仍待批准。
- 已补核战锤官方失落科技远征与Archmagos裂隙通路、基地官方合订本知识保存。STC全史等无正文支持细节排除，不声称完成实时热销排行。
- 关键源码结论：Ultra仅技术奇点；研究value1.25是当前加法堆叠增加0.25而非总速率再乘1.25；Sector无逐目标Ultra门和局部库存/航路图；DeepSpaceRelay基础物流25/48仍待运行核对。
- 文档验证：21页A4，344个正文块全部可提取，0越界/0近空白页，82条书签、7个有效写入的外链；嵌入Markdown与最终正文逐字节一致。抽样目视检查第1、2、11、21页；14个完整本地证据路径存在。没有重跑网页链接健康检查或Unity测试。
- 正文SHA-256：`77a62edbd5985e4d8127e459a5312f78c1a66fac6babd0162040981055e9a195`；PDF SHA-256：`f337f225e10558bb9a0200d6153f65a77a0bb43835b63e90b24d4007b69fcf73`。
- 清理状态：本轮没有创建独立临时备份。尝试将`tmp/ultra_dossier_20260914_build.py`送入回收站时接口返回2，但源路径已不存在；不能确认回收站恢复状态，立即停止后续删除。`tmp/ultra_dossier_20260914_contact.png`仍保留，待确认后处理。未扩大删除到其他任务归档或个人目录。
- 下一动作：确认清理异常与剩余样张处置；若用户批准实施，再从R1/R2最小范围立项，按现行项目与经济技能验证。未执行真实 Unity 编译。
