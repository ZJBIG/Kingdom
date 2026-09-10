# 2026-09-10 只读全仓库重构扫描 handoff

## Status

完成了一次全仓库只读重构扫描，产出主报告
`docs/audits/2026-09-10-full-readonly-refactor-scan.md`。
**未修改任何代码、资产、场景或工具文件。** 本任务为纯审计。

## Scope and method

- 覆盖：`Assets/Resources/Script`（75 文件 / 31,107 行）、`Assets/Tests`（46 文件）、
  `Assets/Editor`（13 文件）、`tools/NewEconomySimulator`、`Assets/Resources/Datas`
  全部内容资产、`docs/` 契约文档。
- 方法：6 个领域并行子代理扫描（Manager / UI / Runtime+Data+Validation /
  数学+模拟器 / 测试+Editor 工具 / 内容资产+文档），部分子代理继续派生了
  二级子代理。主代理对全部 P1 结论做了独立复核。

## Key findings（详见主报告）

- P1 共 11 项：乱码字符串扩散为运行时逻辑（ResearchManager.cs:938/967/1125 +
  DetailPanel.cs:1045 + SectorValidator.cs:278）；EconomyDependencyValidator
  硬编码 Industrial 验证天花板（Spacer/Ultra 可达性不验证）；PopulationState
  epsilon 与 ExpantaNum 舍入粒度冲突（附带 ExpantaNum.cs:2941 的 9e9 阈值
  疑似截断笔误，需作者确认）；5 处同构稳定 ID 线性查找；SectorManager
  TryRepairFleet 双实现 + TryOccupy 互补重复分支 + 硬编码资源成本表；测试层
  356 处反射 API 命中；DetailUI v2 外壳运行时构建无 docs 记录；Sectors/Story/
  Era/Music 四处裸 GameObject UI 创建；Unity↔模拟器公式失同步
  （departureAllowance、UncappedFoodCeiling、GeometricCost 双实现）且无
  parity 防线；SimulationCore.cs 1531 行 8 职责；TutorialManager 19 段复制
  粘贴 + Evaluate 内双重计算。
- 死代码总量粗估 2,300+ 行（ExpantaNum 高级数学族约 700 行、ResearchTree
  布局死代码约 290 行、各 Manager/Runtime/API 零散死项）。
- 规则符合性正面确认：食物唯一上限、无 workforce、批量购买闭式、Manager
  层无反射、事件无泄漏、CanvasScaler 硬合同全部合规。
- 旧 CSV（code-audit-refactor-candidates.csv）复核：4 项已失效（QueuePlay
  已删、Music 图标重复已修、两个未使用字段已清理），其余仍成立但行号漂移；
  `Pair.Deconstruct` 删除建议与 serialized-pairs.md 兼容契约冲突，需先修文档。

## Validation performed

- 主代理对 11 项 P1 中的 12 个具体结论逐一复核：11 项属实、1 项推翻。
  - 推翻项：内容代理声称 StoryArchive 末三章 GUID 断链。经档案引用与
    .meta 逐一比对，引用完全一致（合法 32 字符 guid），子代理误将含前缀的
    整行长度当作 guid 长度。Story 18 章引用链健康，已从主报告 P1 中剔除。
  - 乱码结论经码点级验证确认（PowerShell 控制台编码会显示假象，必须用
    码点数值判断——后续审计如遇中文疑似乱码，务必用此方法）。
- 未执行真实 Unity 编译（纯只读任务，无需编译）。

## Remaining risks and caveats

- 行号以 2026-09-10 工作树为准，后续提交会漂移。
- ExpantaNum.cs:2941 的 `9000000000d` 量化阈值意图需作者裁决（疑似
  9007199254740991d 截断笔误）。
- EconomyDependencyValidator 天花板提升为 Spacer/Ultra 前应先跑一次全量
  静态闭包，确认不会因历史占位内容产生新的启动失败。
- 测试层反射清理是长周期工作，建议按"触碰即替换 + 禁止新增"推进。

## Next concrete action

若开始重构：按主报告第四节顺序执行——先乱码修复与验证器天花板参数化
（正确性优先），再 FindObjectOfType→单例的三处一行级修复与两处
CaptureSaveData 反射替换（低成本高收益），然后 StableIdIndex 统一与
BuildingManager/SimulationCore 拆分。每个批次完成后更新本 handoff。
