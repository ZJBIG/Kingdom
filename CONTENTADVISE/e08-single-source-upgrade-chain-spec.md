# E08 立项章程：三大单源工厂的 Spacer 升级链

- 日期：2026-09-12 · 状态：**立项（设计定稿，待 Unity 验收门通过后实施）**
- 对应审计项：E08（H/M）+ B30/B31（Electronics、Machinery/Concrete/Glass 单源缺口）+ E09 已完成的产能基准
- 依据：`02-resource-chains.md`、`03-buildings-production.md` §2/§4、`batch2-payback-and-duration-table.md`、战役重定标后的补给流（批次 3）

## 1. 问题定义

MachineFactory / WireMill / BuildingMaterialsComplex 三座工业时代建筑是 8 种中间品的唯一来源，`upgradeTo` 均为空：

| 建筑（GUID） | 现有单源产能（基础值/s） | 下游需求（Spacer 均衡/战役重定标后） |
| --- | --- | --- |
| MachineFactory（d7108dfb178fc7742b642aff880bcb8c） | Machinery 1；Engine 0.2；Composite 0.45 | Machinery 战役 26/s + 巨构建造 14400/座；Engine 战役 4.8/s；Composite 幻链维护 |
| WireMill（116d35601a1dec544838b03dbdf70894） | Electronics 0.35 | 均衡约 5/s；战役 11.2/s；科研 64000 |
| BuildingMaterialsComplex（eceb037f89465d9419e3a36f8a208e95） | Glass 1.2；Concrete 2 | 巨构建造单座 21600 Concrete；SolarArray 维护链把玻璃推到约 4.2/s |

整个 Spacer 只能无限堆老建筑（30-40 座 WireMill 量级），升级链设计意图落空。

## 2. 设计方案（3 座新建筑，全部挂在既有 upgradeTo 链上）

| 新建筑 | upgradeTo 来源 | 产能（基础值/s） | 建造成本草案（高级结构材料构成） | 维护/s 草案 |
| --- | --- | --- | --- | --- |
| OrbitalMachiningComplex | MachineFactory | Machinery 4；Engine 0.8；Composite 1.8 | 钛合金 12000；复合 8000；幻金 600；电子 5000；机械 9000 | 钛合金 0.6；电子 0.5；电力 220 |
| OrbitalWireWorks | WireMill | Electronics 1.4 | 钛合金 8000；复合 5000；铜线 6000；玻璃 3000 | 钛合金 0.3；铜线 0.8；电力 180 |
| OrbitalBuildingMaterialsWorks | BuildingMaterialsComplex | Glass 4.8；Concrete 8 | 钛合金 10000；复合 6000；混凝土 15000 | 钛合金 0.4；混凝土 1.5；电力 260 |

- 产能倍率统一约 4x，目标：均衡态 2-4 座续作 + 少量老厂即可覆盖重定标后的战役流与巨构建造，老厂仍保留边际价值（锁定规则"老建筑持续有用"）。
- costGrowth：加工带 1.15-1.17（取 1.16）；maxAmount 不设限；spaceCost 对标同代巨构（约 300-500）。
- 解锁科研：**复用既有 Spacer 科研**，不新增节点——OrbitalMachiningComplex ← DeepSpaceIndustrialIntegration；OrbitalWireWorks ← OrbitalPowerTransmission；OrbitalBuildingMaterialsWorks ← OrbitalEngineering（以各建筑的 requiredResearch 现状在实施时核对，避免改变既有科研语义）。
- 遵守锁定规则：不新增资源、不设容量、无 workforce；建造与维护均消耗高级结构材料；不动任何既有 GUID。

## 3. 实施步骤与验证门

1. 新建 3 个 .asset + .meta（新随机 GUID，Script 字段指向 Building.cs 的 GUID 755e6eabd0fdcbc4c92dd540b39a0ec9 所在定义类）。
2. 三座老工厂 `upgradeTo` 字段指向对应新建筑。
3. 验证：`tools/codex/content-closure-check.ps1`（预期 Building reachable 变为 Spacer 19/19）；Editor 定义校验 + 资源 source/sink 审计；EditMode 全量；回本对照 `batch2` 时代折算加工带 15-40min；PlayMode 冒烟（建造→产能→战役补给净流）。
4. 数值如需微调，以 batch2 表与运行时日志为准，禁止凭感觉改。

## 4. 明确不做

- 不给 Composite/Engine 单独建第三座建筑（由 OrbitalMachiningComplex 承担，避免平行内容）。
- 不动 PhaseMaterial 链（E09 已单独处理）。
- 不在 E08 里顺手改其他建筑数值（精准改动原则）。
