# Economy Simulation Report - Fast

Offline runtime-aligned simulation; no Unity runtime was launched.

## Rules

- Fixed 0.1 second ticks, minute aggregation, stockpiles clamp at zero.
- Research cost is paid progressively; research speed uses ResearchPower and runtime era effect.
- Buildings can repeat and use geometric cost growth; construction is immediate after payment.


# Animal Age

预计时间: 0分钟

研究完成数: 13; 资源归零时间: 0 分钟。

# Neolithic Age

预计时间: 152分钟

研究完成数: 16; 资源归零时间: 0 分钟。

# Medieval Age

预计时间: 不可达

研究完成数: 0; 资源归零时间: 0 分钟。

# Industrial Age

预计时间: 不可达

研究完成数: 0; 资源归零时间: 0 分钟。

## Bottlenecks

- Clay: single producer
- WoodLog: single producer
- StoneChunk: single producer
- PlantFiber: single producer

## Warnings

- **Medium Single point bottleneck** Clay: Only one defined producer serves PotteryKiln. Suggestion: Add an alternative producer or move the dependency later.
- **Medium Single point bottleneck** WoodLog: Only one defined producer serves PotteryKiln. Suggestion: Add an alternative producer or move the dependency later.
- **Medium Single point bottleneck** StoneChunk: Only one defined producer serves StoneCuttingWorkshop. Suggestion: Add an alternative producer or move the dependency later.
- **Medium Single point bottleneck** PlantFiber: Only one defined producer serves WeavingWorkshop. Suggestion: Add an alternative producer or move the dependency later.
- **High Missing producer** Steel: Research AdvancedIndustrialMaterials requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** Chemical: Research AdvancedIndustrialMaterials requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** Pottery: Research AdvancedIndustrialMaterials requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** PlantFiber: Research AnimalFodder requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** WoodLog: Research AnimalHusbandry requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** Bronze: Research ArsenalOrganization requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** Coal: Research ArsenalOrganization requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** Cloth: Research Astronomy requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** Machinery: Research AutomatedAssembly requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** PrecisionParts: Research AutomatedAssembly requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** Electronics: Research AutomatedAssembly requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** StoneChunk: Research Calendar requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** StoneBrick: Research CastleArchitecture requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** Clay: Research CharcoalMaking requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** RefinedFuel: Research CombustionEngines requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** Lubricant: Research CombustionEngines requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** Copper: Research ElectricalEngineering requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** CopperWire: Research Electrification requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** Coke: Research IndustrialAgriculture requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** CrudeOil: Research IndustrialChemistry requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** Engine: Research LogisticsManagement requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** Iron: Research MetallurgicalStandards requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** Rubber: Research MilitaryIndustry requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** Tin: Research Smithing_Bronze requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** CopperOre: Research Smithing_Copper requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** IronOre: Research Smithing_Iron requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.
- **High Missing producer** TinOre: Research Smithing_Tin requires a resource with no producer. Suggestion: Add a reachable producer or remove the requirement.

## Validation boundary

未执行真实 Unity 编译。

未执行 Huawei P40 Pro 真机验收。
