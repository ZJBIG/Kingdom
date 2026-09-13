# 断言审查报告（只读——未删除、未修改任何代码）

- 日期：2026-09-12
- 范围：`Assets/Tests/Editor` 40 文件 + `Assets/Tests/PlayMode` 5 文件（669 个测试方法全部覆盖）
- 方法：3 个并行只读子代理（无意义/过时钉值/重复主题）+ 主代理汇总；基线对照 2026-09-10 审计（Editor 757 + PlayMode 64 处 `Is.EqualTo`，约 40 处裸精确相等违规，无逐条清单——本报告首次生成清单）
- 逐条清单见 `assertion-review.csv`。**本报告只审查，未做任何删除或修改。**

## 0. 总量统计（对照 09-10 审计）

| 指标 | 09-10 审计 | 本次复核 | 说明 |
| --- | --- | --- | --- |
| `Is.EqualTo` 行数 | Editor 757 + PlayMode 64 | 822（Editor 758 + PlayMode 64） | FlowEfficiencyTests:66 本轮已改 Within，±1 为统计口径差 |
| `.Within(` 行数 | ~102 | 117 | 本次含非 Is.EqualTo 链的 Within |
| 运算结果裸精确相等违规 | 约 40 | **约 36，集中 5 个文件** | 差额 ≈ 6 处存档字段往返 + 2 处夹取哨兵，本次归为"可辩护"类 |
| 零断言测试 | 未统计 | **0**（669/669 方法体有断言） | 摆设测试不存在 |

## 1. 裸精确相等违规清单（36 处，运算结果类，建议改 Within 或加辩护注释）

25/36 集中在 KingdomLogicTests；审计点名的 `AdvanceFood==120` 与 `CombatRatio==0.8d` 均仍原样存在。

| 位置 | 断言 | 建议 |
| --- | --- | --- |
| KingdomLogicTests.cs:175,185 | AdvanceFood(...)==120 / ==1000 | Within(1e-6) 或加"整数语义精确"注释 |
| KingdomLogicTests.cs:138,678 | Total(4,10)==40；AdvanceAmount(...)==110 | Within |
| KingdomLogicTests.cs:704,712 | 生产率余量 1e-6 / 净率 -0.5 | Within（同文件:66 已有量化注释可复用） |
| KingdomLogicTests.cs:719,720 | CalculateSatisfaction==0.2/0.5 | Within（0.2 二进制不可精确表示） |
| KingdomLogicTests.cs:1132-1149 | TotalProductivity baseline±7/15、Used==6 | Within(1e-9) 或注释 |
| KingdomLogicTests.cs:1198-1200,1227,1452 | 生产力 4/6/-2/10/28 | 同上 |
| KingdomLogicTests.cs:2610 | AdvanceResearchProgress==5 | Within；:2611 ==95 为夹取边界可保留+注释 |
| KingdomLogicTests.cs:2752-2754 | CampaignEffectivePower==20/0/0 | Within |
| KingdomLogicTests.cs:2698 | ResearchSpeedEffect==1d | 补 Within(1e-12)（:2701 已是 Within 风格） |
| FoodEfficiencyTests.cs:28,29,120,122,124 | 食物可用度/消耗/净率计算值 | Within |
| FlowEfficiencyTests.cs:77,100 | HappinessFormula==0.5d（与已修 :66 同类漏网） | Within |
| SectorManagerTests.cs:1345,1486,2196 | CombatRatio==0.8d（存取往返） | Within(1e-12) 或加"存档字段"注释 |
| 可辩护（保留+注释即可） | BuildingCostGrowthTests:23-34（闭式几何边界=函数规格）；三满意度 0.75/0.8/0.9/0.5/0.25 存档字段往返（SectorManagerTests:1885-1887,2493-2495、KingdomLogicTests:561-563）；SupplySatisfaction 夹取哨兵 | 按数值断言规则补"离散/存档兼容"辩护注释 |

## 2. 过时语义 / 死代码断言（本轮修缮后新发现）

| 位置 | 问题 | 建议 |
| --- | --- | --- |
| KingdomLogicTests.cs:978-1003 | 测试名 FoodGatedDeparture 但 `departureAllowance` 是死参数（AdvanceDeparture 全库无调用方，CurrentDepartureRatePerSecond 恒 0）——测试靠死路径空转通过 | 改走食物短缺路径（:121）断言，或注明 allowance 已停用 |
| KingdomLogicTests.cs:1454-1456 | `Tick(1d, SafePopulationDepartureAllowance)` 后人口==14：allowance 实参无效果，"存活"是空真（Housing 主断言本身有效） | 去掉对死实参的表意依赖 |
| KingdomLogicTests.cs:1024-1027 | 对照组：走 3 参重载（食物短缺授权）是活代码 | 保留 |
| 修缮残留核查 | 0.95/6.075 在测试中零命中；0.12 仅剩 P40UiConfigurationTests:84 的合法 InRange | 无需处理 |

## 3. 无意义/弱断言（5 项；含一项重要"翻案"）

| 位置 | 发现 | 判定与建议 |
| --- | --- | --- |
| GlobalEconomyDefinitionTests.cs:19-27 | `ReferenceEquals(building.ResourceRequirements, building.ResourceRequirements) ×9` 曾疑似同义反复 | **翻案：有意义**。getter 经 `ToPairs(..., ref cache)` 返回缓存，两次调用同引用=缓存命中，缓存失效时断言会真失败；测试名 ResourcePairDefinitionViewsReuseTheirCachedLists 与意图一致。**建议保留并加注释**，防后人当恒真误删 |
| BuildingVerticalSliceTests.cs:37-43 | `FoodCapacity_IsLimitedToThePlannedFoodBuildings` 名字承诺全局排他，实际只抽查 4 座；同文件 :176 显示 RailHub/OrbitalHub 也在白名单外持容量 | 改为全建筑遍历+白名单断言（Food 唯一上限规则的测试化） |
| KingdomOnboardingPlayModeTests.cs:485-488 | 强制布局审计在 overflow≤1 时 `Assert.Ignore`——套件保持绿但"实测内容位移"未执行（AGENTS.md 要求实测才算 verified） | 保留 Ignore 但 log 显式标注 SKIPPED-AUDIT，或与溢出条件解耦 |
| GlobalEconomyDefinitionTests.cs:106-112 | `MigrationProducesThePlannedDefinitionCounts` 名称称精确计数，实际只有 `>=40`/`>0` 下限；:111 疑似删除断言残留 | 改精确计数或改名 AtLeast |
| MobileOrientationConfigurationTests.cs:42-43 | `Contains("    Android: 1")` 可匹配 ProjectSettings 任意小节同名行 | 先定位 `scriptingBackend:` 行再断言其后内容 |

另：DoesNotThrow ×7 均为合法正向配对用法（与 Assert.Throws 对称），非吞没；"刚创建对象断非空"零命中。

## 4. 重复与口径漂移（16 项；真重复 8、分层防御 6、口径漂移 2）

**最有价值的两个维护陷阱（口径漂移）：**

| 编号 | 主题 | 位置 | 陷阱 |
| --- | --- | --- | --- |
| AR3 | 时代跃迁 prereqs | ContentVerticalSliceAuditTests:70-90（子集语义）vs ResearchMedievalContentTests:62-65 + SpacerResearchPlacementTests:15-19（EquivalentTo）vs ResearchBalanceTests:252 | CV 的子集清单**已过期**（Singularity 只列 2/4 项前置）——它既不完整也不报错，读者会误以为是完整前置集 |
| AR2 | 星际战役后勤构成 | SectorDefinitionTests:86,:306（≥5 种）vs SectorManagerTests:816、SD:327（≥8 种） | 同一约束两套阈值并存，弱断言被强断言完全蕴含、永不触发；调阈值时只看一处会误判 |

**真重复（同约束同强度多文件，合并候选）：** TerritoryReward≥100000 全量循环（SM:798-812 vs SD:276-283）；殖民时长钉值（SD:158-159 vs SM:2614-2616）；科研图无环校验（ResearchMedievalContentTests:75-81 vs SpacerResearchPlacementTests:135-141）；战役持续消耗高级材料任一（SM:136-142 vs SD:284-293）；CampaignFoodPerSecond>0（4 处）；战役 ≥3600s（SM:130-133 vs SD:339-352，保留 SD 全量版）；OrbitalStation 镍维护 0.04（SM:1453 vs RC:278）；ResearchBalanceTests 文件内 FeudalAdministration=130000 钉两次（:228,:249）。

**分层防御（保留，建议加口径注释）：** source/sink 家族共 6 种口径互补（GE:623 全量 / AM:162 五材料三渠道 / SP:1420 类别数 / RC:121 工业十种 / C5/C6 审核工具 / SC:40）；0.8 人均食物消耗双钉（KL:204 精确 vs SDet:130 parity）；OrbitalHabitat 3000 人三处；KnowledgeCircle growth 精确+区间并存。

## 5. 处置建议汇总（本轮不动，仅排序）

1. **P1 口径漂移**：AR3（CV 前置清单过期）、AR2（≥5/≥8 双阈值）——这是会误导后续调参的活陷阱。
2. **P1 裸相等**：36 处按清单改 Within/关系断言（优先 KingdomLogicTests 25 处）；可辩护 8 处只补注释。
3. **P2 真重复合并**：8 组合并候选（每组保留信息量最大的一处）。
4. **P2 死代码断言**：FoodGatedDeparture 两条（与 09-10 审计的 departureAllowance 死代码项联动处理）。
5. **P3 脆弱文案/视觉**：中文 Label 精确断言 4 处、错误消息 1 处、Image.color 1 处、Story FirstId 顺序钉值（有 UI 语义的可保留+注释）。
6. **P3 名实不符**：Migration 测试改名或改精确计数；FoodCapacity 白名单测试改全量遍历。

## 6. 证据与限制

- 三份子代理扫描（无意义/过时钉值/重复主题），行号以 2026-09-12 工作树为准，后续提交会漂移。
- 本报告为纯静态审查；未运行 Unity（断言行为有效性以 2026-09-12 run6 661/661 通过为背景）。
- 全程未修改任何测试或代码（遵守"先不要删，只做审查"）。
