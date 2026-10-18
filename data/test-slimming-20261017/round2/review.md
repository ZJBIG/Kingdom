# 第二轮测试精简：2026-10-17

## 改动
本轮13份测试净删665行、134处断言和25个[Test]/[UnityTest]方法。Editor净删564行/120断言/21方法；PlayMode净删101行/14断言/4方法。相对第一轮开始累计净删2236行、695处断言、81个普通测试方法及3个TestCase。静态方法标记不等于runner用例数量。

- GlobalEconomyDefinitionTests：合并效果目标验证并保留OccupiedResourceProductionMultiplier资源目标约束；删除重复的字符串序列化检查（更完整版本保留）、生产力建筑职责子集检查及2个闲置helper，删除复制的炼油厂前置数量。
- KingdomUiLifecycleTests：空Manager快照的状态、blocker和可读正文合为同一次调用。
- ResearchBalanceTests：删除旧GetField/BindingFlags结构测试、重复发布研究检查和失去引用的helper；字符串Property查询改强类型Any。固定倍率/文案快照改为目标类型、增益方向和医学递增关系。
- PreSpacerCombatRemovalTests：移除复制的1.03等倍率参数，保留有效增益、正确效果目标及战斗时代隔离。
- SpaceProgressionTests/UltraContentSliceTests/IndustrialWorkshopAvailabilityTests：清除复制的设施规模、消耗数额、准备时长、倍率与费用条数；保留真实材料流、正确目标、有效增益/减耗与设施升级链。重复工坊前置扫描由全量定义检查覆盖。
- SectorDefinitionTests：删除奖励、时长、默认State回读和重复循环/可达性正例；保留运行时ValidateDefinitions总检查及多环反例。独有本星系无战斗/坐标关系与Food后勤检查迁入保留测试。
- AdvancedMaterialProgressionTests：合并同材料集的source/sink扫描，保留生产时代、连续建筑sink、研究/工坊sink、RocketFuel工业入口及禁循环；删除4个重复helper和重复OrbitalStation局部变量。
- PlayMode：导航当前页/旧页恢复并入逐页刷新；教程反馈与最近3条通知合为同场景并保留surface不重建；Research滚动接管并入Story真实导航滚动恢复；Overview教程目标绑定并入两次真实刷新。少4次独立场景加载，不新增helper，不把减少加载次数当实际速度测量。

## 验证
- dotnet build Kingdom.DeveloperTests.csproj --no-restore：exit0，0errors，6条既有TextureImporter.spritesheet废弃警告，13.53s。Editor和PlayMode测试均编译，compile-tests.log。
- 真实Unity EditMode，9个受影响测试类121/121通过，0跳过，1.0873103s；editmode.xml/log。
- Unity写XML后原生退出挂起，60秒包装器终止自启动PID，exit1；runner-status.txt。测试断言通过与runner正常退出分开记录，没有重复诊断许可证/网络。
- git diff --check通过；本轮文件未发现空测试方法或未引用private方法；当前文档未引用被改名/合并的PlayMode方法。
- 未运行PlayMode或全套测试，没有实际加速结论。

## 工作树和证据
此前6份生产修复源码SHA256全部匹配上轮source-inputs.json；没有生产、资产、meta/GUID、存档、提交或推送操作。baseline.json来自第一轮最终计数；after.json/summary.json记录本轮前后，test-inputs.json记录13份最终输入SHA256，pre-existing-changes.patch保留续接时已有工作树。
