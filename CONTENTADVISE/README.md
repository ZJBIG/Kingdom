# CONTENTADVISE 只读内容与平衡审计总览

- 日期：2026-09-12
- 性质：**只读审计**。未修改任何代码、资产、场景或文档；未运行模拟器/Unity/测试。**未执行真实 Unity 编译。**
- 方法：主代理侦察仓库结构后，并行派发 6 个只读子代理分领域扫描（科研树 / 资源链 / 建筑生产 / 区域军事 / 人口工坊节奏 / 既有证据基线），随后主代理对发布到本总览的关键数值做了资产原文抽查复核（标注"已核验"的条目均由主代理二次确认）。
- 输出：本目录下 6 份领域报告 + 2 份 CSV + 本总览。所有结论为**静态推断**，改数值前必须按各行"建议验证方式"复核。

## 文件清单

| 文件 | 内容 |
| --- | --- |
| `README.md` | 本总览 |
| `content-expansion-opportunities.csv` | 内容拓展机会 36 条（编号 E01-E36） |
| `balance-risks.csv` | 平衡风险 34 条（编号 B01-B34） |
| `01-research.md` | 科研树领域报告（129 条科研全量核对） |
| `02-resource-chains.md` | 资源与生产链领域报告（40 资源来源/去向表） |
| `03-buildings-production.md` | 建筑与生产领域报告（66 建筑、回本与电力估算） |
| `04-sector-combat.md` | 区域/军事/领土/战役领域报告（10 星区明细） |
| `05-population-workshop-pacing.md` | 人口/工坊/剧情/节奏领域报告 |
| `06-existing-evidence.md` | 既有证据与文档基线（文档承诺 vs 资产现状） |
| `WORTH-DOING.md` | 70 条发现逐条判定（值得做/可缓/不做）与执行批次 0-6 |
| `worth-doing-verdicts.csv` | 逐条判定表（编号/判定/理由/批次/前置依赖） |
| `assertion-review.md` | 测试断言审查：过时/重复/无意义三类（只读审查，未删改） |
| `assertion-review.csv` | 断言审查逐条清单（47 条：N01-N31 + D01-D16） |

CSV 为 UTF-8（已加 BOM），可直接用 Excel 双击打开；字段内的"交叉印证"列引用 01-06 报告编号。

## 十大平衡风险（先看这些）

1. **远星战役数学上不可支付**（B01，H）：战役补给流对玩家产能差 1-4 个数量级——Sirius 战役要 PhaseMaterial 800/s 而单座合成阵列仅 0.008/s（需约 10 万座）；Engine 1200/s 对 0.2/s；RocketFuel 3666.67/s 对 14/s。囤积路径仅 PhaseMaterial 就约 5.1 年。
2. **战役奖励仅为投入的 0.02%-0.3%**（B02，H）：AlphaCentauri 奖励 85000 对投入 2841 万起；违反"后期战役奖励必须够大"的锁定规则，理性策略是完全跳过远星。
3. **Spacer 科研成本断崖**（B08，H）：TL3→TL4 时代成本总和跳 1243 倍而 ResearchPower 产能仅约 5 倍；纯科研时长约为 Industrial 的 200 倍，与"offline pacing acceptance 失败"方向一致。
4. **TechnologicalSingularity 是零收益死墙**（B09/B34，H）：全树最高成本 5.5e11（=全 Spacer 其余 47 条总和的 14.4 倍）且 `effects:[]`（已核验），完成后零回报；剧情终章 17/18 还依赖它。
5. **生产力消耗笔误嫌疑**（B15，H）：RareMetalMine 与 TitaniumMetallurgicalComplex 的 productivityConsumption 均为 12（已核验），同梯队建筑为 95-130，疑似少写一个 0。
6. **University 支配 Spacer 研究建筑**（B16，H）：每千价值研究力 University 42.8 对 QCA 2.7 / ITN 0.3（RP 已核验 250/500/900/220），Spacer 三座研究建筑沦为一次性门槛物。
7. **IndustrialAgriculture 食物乘数 6.075 离群**（B05，H）：同类次大 1.5；后果是 Industrial 起食物永远过剩、幸福恒顶格、6 条食物工坊变成无效购买。
8. **幸福惩罚过陡 + 食物容量缓冲仅约 8 秒**（B06/B07，H/M）：短暂断供即把建筑砍到不足 10% 产出；Industrial 满载断供 8-17 秒见底即触发人口离场。
9. **单源中间品产能锁死**（B30/B31，H）：Electronics 0.35/s 承接 Spacer 全部智能化需求（均衡需求约 5/s）；Machinery/Concrete/Glass 单源对巨型结构每座 2.5-6.5 小时纯积累。
10. **校验器盲区**（B27，M）：回本目标无校验、costGrowth<1 静默回退 1.15、战役奖励只查>0、新增第 41 个资源会逃逸 source/sink 审计——未来加内容时垃圾数值不会被自动拦截。

## 十大拓展方向（按"解锁面"排序）

1. **战役补给率+奖励整体重定标**（E19/E20，前置项）：不先做这条，远星内容链全部拓展没有意义。
2. **三大单源工厂的 Spacer 升级链**（E08）：Machinery/Electronics/Glass/Concrete/Composite/Engine 八种中间品获得轨道级续作，同时满足"后期复用早期资源"。
3. **PhaseMaterial 产能链修复**（E09）：提速率或加中间品/并联机制，Spacer 巨构玩法才成立。
4. **11 条 Spacer 叶子科研延线**（E02）：占 Spacer 23%，主题天然有续篇，纯资产工作。
5. **中古时代补强**（E14/E15/E26）：补 1 座 40-60 food/s 中古农业建筑（5 座中古建筑食物产能全 0，已核验）+ 1-2 座新角色建筑 + 2-3 章剧情。
6. **ResearchPower 产出链补档**（E01）：Spacer 加 400-600 RP 档建筑；老资源生产向科研（E03）与领土科研过渡档（E05）同批做。
7. **星区经营线**（E21/E23）：每星区 1-2 栋星区建筑 + repeatable 赏金战役（字段已存在，10 区全为 0）。
8. **Spacer 教程段 + Workshop 教程步**（E27/E28）：8 步教程全部是早期内容，Spacer 新系统零引导。
9. **闲置贴图激活为新资源线**（E11/E12）：19 张异宝石、Uranium、Glasteel/Microchips/Hyperalloy 等贴图全库无引用，可直接支撑奢侈资源线与核电第三路线。
10. **Ultra 时代规划**（E10）：最大内容空洞（0 建筑/0 工坊/1 条零效果科研），但注意——**路线图已将 Ultra/Archotech 冻结至 Milestone A-D 验收完成**，当前只应登记缺口，不应提前加内容。

## 重要前提与修正（读报告前必看）

1. **Ultra 冻结是有意决策**：`docs/content/progression-roadmap.md:40` 将 Ultra/Archotech 冻结至 Milestone A-D 验收完成。报告中"Ultra 空缺"类发现是背景事实，不是意外风险。
2. **历史模拟诊断目录为空**：`data/economy-simulation/{Normal,Fast,Conservative}` 实测 0 文件。任何旧模拟数字都不可引用；唯一在档节奏事实是 AGENTS.md 的"offline pacing acceptance currently fails"。模拟器（tools/NewEconomySimulator）已转型为纯维护校验工具，其输出不得当平衡证据。
3. **全部结论为静态推断**：等待时长、回本、折价均基于定义资产+运行时代码推算（各报告已声明假设）。两处结论对假设敏感，定案前必须运行时验证：TitaniumMetallurgicalComplex 净产出为负（B19，折价假设敏感）、University 支配（B16，需先核对工坊每建筑 RP 倍率是否已逆转）。
4. **主代理已复核的数值**（引用时可放心）：productivityConsumption=12（RareMetalMine/TMC asset:22）、Medieval 5 建筑食物产能全 0（asset:26）、University 250 / DSO 220 / QCA 500 / ITN 900 研究力、Sirius 战役流（Food 4666.67/s、PhaseMaterial 800/s 等 asset:48-70）、IndustrialAgriculture 6.075（asset:38）、WireMill Electronics 0.35/s（asset:48）、PhaseMaterialSynthesisArray 0.008/s（asset:56）、TechnologicalSingularity `BaseCost 5.5e11` + `effects:[]`（asset:18/37）。
5. **测试现状**（2026-09-11）：660 用例 658 过 2 失败——FlowEfficiencyTests 精度断言（断言卫生，B24）与 SectorBuildingTests 存档路径行为级失败（B03 同域，扩星区建筑线前必须先修）。
6. **本次任务未更新 `.codex/handoffs/`**：按用户要求输出仅限本目录；本任务为纯分析，无代码/资产变更。

## 建议的最小后续动作（供决策，均未执行）

1. 先修行为级测试失败（SectorBuildingTests）与 B24 断言卫生——它们是后续一切验证的地基。
2. 用静态方式落两份对账表（E34）：50 座 Industrial 建筑回本清单 + 129 科研目标时长对照——它们同时服务 B08/B12/B18 的定案。
3. 按 B15/B05/B09（三个疑似笔误/离群/空效果）逐条与设计意图核对，再决定是否改值。
4. 战役补给/奖励重定标（E19/E20）立项——这是远星内容链的唯一解锁前提。
5. 校验器补盲（B27）：costGrowth 带宽断言、战役奖励下限、空效果检测、新资源纳入审计名单。
