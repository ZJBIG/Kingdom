# Ultra能力工坊分工

更新：2026-10-07。以下保留当前核对方法与行为合同，已实现功能不再列为待开发。

本专题保留三个现有工坊的作用对象与消费端核对入口。

- AutonomousMatterOrchestration：建筑生产能力。
- PhaseCognitiveCompute：研究能力。
- InterstellarSupplyMesh：物流能力。
- 实际消费端、叠加规则与额外投入从ProgressionModifierManager/BuildingManager查询，不能把资产倍率解释为当前总倍率再乘一次。
- 购买先原子支付再重建效果，失败不部分授予。

核对入口（仓库相对路径）：Assets/Resources/Datas/Workshop/；Assets/Resources/Script/Manager/ProgressionModifierManager.cs。

本次仅修订文档；未执行真实 Unity 编译。
