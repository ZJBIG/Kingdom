# Ultra 当前行为验收矩阵

更新：2026-10-07。保留验收结构，删除旧686/35计数、过时Failed(NoXml)状态和“R2不得新增State”的门禁。

| 对象 | 必须验证的行为 | 已有覆盖/剩余边界 |
|---|---|---|
| 定义/研究/工坊 | 前置可达、效果有目标和消费端、购买支付后授予 | 现有UltraContentSliceTests/ResearchEffectTests；静态闭包不证明供给充足 |
| 巨构与升级 | 当前材料、Food/Power/Logistics与生产力共同决定实际结算 | 现有LateFactoryConsolidationTests主要锁资产结构；最终材料链六态仍需集成 |
| 文明工程 | 启动原子性、分阶段供给、暂停恢复、放弃不退款、最终提交一次 | UltraProjectManagerTickTests/UltraProjectStateTests；复杂分段离线仍需完整对照 |
| 战役 | 完成边界停止补给、伤亡/维修/占领奖励与实际姿态一致 | Sector/Simulation相关用例；不把fixture当真实全系统parity |
| v9保存 | 工程合法阶段/ProjectId/版本/暂停原因与时代一致，非法状态不部分应用 | 保存/时代校验测试已存在；缺段与显式null语义区别保留 |
| UI | 真实SampleScene启动、详情/姿态绑定、Locked入口导航和实际拖拽 | 真实启动/姿态保存用例存在；Locked专门导航和溢出拖拽仍需补覆盖 |

相关源码改变后按受影响面选择真实Unity编译、EditMode、非零PlayMode与Console；不能用静态数量代替行为。
净流/等待/回本是独立运行结论；外部体验由用户自行验收，不执行或跟踪。
既有XML日期与跳过项见[当前工作树审查](当前工作树审查.md)，下一施工见[动工策划案](动工策划案.md)。
未执行真实 Unity 编译。
