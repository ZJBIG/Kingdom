# Content dependency analysis
================================
UNREACHABLE RESEARCH
================================

Research: RailwayEngineering
Blocked reason:
Missing prerequisite: MechanizedProduction

Research: MassProduction
Blocked reason:
Missing prerequisite: MechanizedProduction

Research: SyntheticFertilizers
Blocked reason:
Missing prerequisite: IndustrialChemistry

Research: PrecisionManufacturing
Blocked reason:
Missing prerequisite: ElectricalEngineering

Research: ModernUniversity
Blocked reason:
Missing prerequisite: ScientificMethod

Research: Bookmaking
Blocked reason:
Missing prerequisite: FeudalAdministration

Research: FactoryOrganization
Blocked reason:
Missing prerequisite: Standardization

Research: Fortification
Blocked reason:
Missing prerequisite: FeudalAdministration

Research: StandingArmy
Blocked reason:
Missing prerequisite: Fortification

Research: CombustionEngines
Blocked reason:
Missing prerequisite: MechanizedProduction

Research: AluminumMetallurgy
Blocked reason:
Missing prerequisite: ScientificInstrumentation

Research: PublicHealth
Blocked reason:
Missing prerequisite: Bookmaking

Research: IndustrialChemistry
Blocked reason:
Missing prerequisite: PetroleumExtraction

Research: ModernSteelmaking
Blocked reason:
Missing prerequisite: Coking

Research: MilitaryIndustry
Blocked reason:
Missing prerequisite: MassProduction

Research: MechanizedProduction
Blocked reason:
Missing prerequisite: PrecisionManufacturing

Research: ScientificMethod
Blocked reason:
Missing prerequisite: Industrialization

Research: PetroleumExtraction
Blocked reason:
Missing prerequisite: Industrialization

Research: IndustrialAgriculture
Blocked reason:
Missing prerequisite: MassProduction

Research: Coking
Blocked reason:
Missing prerequisite: SteamPower

Research: TradeRoutes
Blocked reason:
Missing prerequisite: FeudalAdministration

Research: Steelmaking
Blocked reason:
TechLevel condition: 2

Research: PowerGridEngineering
Blocked reason:
Missing prerequisite: ElectricalEngineering

Research: ElectricalCommunication
Blocked reason:
Missing prerequisite: ElectricalEngineering

Research: GuildSystem
Blocked reason:
Missing prerequisite: FeudalAdministration

Research: MechanicalEngineering
Blocked reason:
Missing prerequisite: FeudalAdministration

Research: ConcreteEngineering
Blocked reason:
Missing prerequisite: IndustrialChemistry

Research: SteamPower
Blocked reason:
Missing prerequisite: Industrialization

Research: FeudalAdministration
Blocked reason:
TechLevel condition: 2

Research: LogisticsManagement
Blocked reason:
Missing prerequisite: RailwayEngineering

Research: UrbanHousing
Blocked reason:
Missing prerequisite: FeudalAdministration

Research: ElectricalEngineering
Blocked reason:
Missing prerequisite: IndustrialChemistry

Research: Gunpowder
Blocked reason:
Missing prerequisite: Steelmaking

Research: IndustrialWorkshop
Blocked reason:
Missing prerequisite: Industrialization

Research: Industrialization
Blocked reason:
Missing prerequisite: MechanicalEngineering

Research: ScientificInstrumentation
Blocked reason:
Missing prerequisite: ScientificMethod

Research: ScholasticInstitutions
Blocked reason:
Missing prerequisite: Bookmaking

Research: Standardization
Blocked reason:
Missing prerequisite: IndustrialWorkshop

Research: IndustrialCopperSmelting
Blocked reason:
Missing prerequisite: Coking

================================
UNREACHABLE BUILDINGS
================================

Building: IndustrialTinSmelter
Blocked reason:
Missing prerequisite: IndustrialCopperSmelting

Building: Glassworks
Blocked reason:
Missing prerequisite: IndustrialChemistry

Building: BauxiteMine
Blocked reason:
Missing prerequisite: AluminumMetallurgy

Building: OilRefinery
Blocked reason:
Missing prerequisite: IndustrialChemistry

Building: RailHub
Blocked reason:
Missing prerequisite: RailwayEngineering

Building: SteelForge
Blocked reason:
Missing prerequisite: Steelmaking

Building: OrbitalStation
Blocked reason:
Missing resource: Steel
Required producer: SteelForge
Producer blocked by: Steelmaking
Required producer: BlastFurnace
Producer blocked by: ModernSteelmaking

Building: Shipyard
Blocked reason:
Missing resource: Steel
Required producer: SteelForge
Producer blocked by: Steelmaking
Required producer: BlastFurnace
Producer blocked by: ModernSteelmaking

Building: ArmsFactory
Blocked reason:
Missing prerequisite: MilitaryIndustry

Building: GuildHall
Blocked reason:
Missing prerequisite: GuildSystem

Building: AluminumSmelter
Blocked reason:
Missing prerequisite: AluminumMetallurgy

Building: WireMill
Blocked reason:
Missing prerequisite: ElectricalEngineering

Building: Castle
Blocked reason:
Missing prerequisite: Fortification

Building: IndustrialBronzeFoundry
Blocked reason:
Missing prerequisite: IndustrialCopperSmelting

Building: ConcreteWorks
Blocked reason:
Missing prerequisite: ConcreteEngineering

Building: LaunchCenter
Blocked reason:
Missing resource: Steel
Required producer: SteelForge
Producer blocked by: Steelmaking
Required producer: BlastFurnace
Producer blocked by: ModernSteelmaking

Building: OilDerrick
Blocked reason:
Missing prerequisite: Industrialization

Building: Caravanserai
Blocked reason:
Missing prerequisite: TradeRoutes

Building: SteamPlant
Blocked reason:
Missing prerequisite: Industrialization

Building: CokeOven
Blocked reason:
Missing prerequisite: SteamPower

Building: RoyalWorkshop
Blocked reason:
Missing prerequisite: GuildSystem

Building: TownHouse
Blocked reason:
Missing prerequisite: UrbanHousing

Building: MachineFactory
Blocked reason:
Missing prerequisite: PrecisionManufacturing

Building: ChemicalPlant
Blocked reason:
Missing prerequisite: PetroleumExtraction

Building: University
Blocked reason:
Missing prerequisite: ModernUniversity

Building: Academy
Blocked reason:
Missing prerequisite: ScholasticInstitutions

Building: IndustrialCopperSmelter
Blocked reason:
Missing prerequisite: IndustrialCopperSmelting

Building: Arsenal
Blocked reason:
Missing prerequisite: Gunpowder

Building: Hospital
Blocked reason:
Missing prerequisite: PublicHealth

Building: Barracks
Blocked reason:
Missing prerequisite: StandingArmy

Building: CentralPowerStation
Blocked reason:
Missing prerequisite: PowerGridEngineering

Building: Library
Blocked reason:
Missing prerequisite: Bookmaking

Building: BlastFurnace
Blocked reason:
Missing prerequisite: ModernSteelmaking

================================
RESOURCE DEADLOCKS
================================
AluminumMetallurgy -> requires BauxiteOre -> producer BauxiteMine requires AluminumMetallurgy
ModernSteelmaking -> requires Steel -> producer BlastFurnace requires ModernSteelmaking

================================
RESEARCH CYCLES
================================
None

Summary
Definitions: 169
Research definitions: 72
Building definitions: 65
Research reachable: NO
Building reachable: NO
Resource deadlock: FOUND
Research cycle: None
Highest TechLevel: 1

================================
REPAIR SUGGESTIONS
================================

Problem:
AluminumMetallurgy -> requires BauxiteOre -> producer BauxiteMine requires AluminumMetallurgy

Possible fixes:
Option A: Remove the blocking resource from the research or construction cost.
Option B: Change the producer requiredResearch or constructionCost.
Option C: Add an alternative producer reachable before the blocked node.

Problem:
ModernSteelmaking -> requires Steel -> producer BlastFurnace requires ModernSteelmaking

Possible fixes:
Option A: Remove the blocking resource from the research or construction cost.
Option B: Change the producer requiredResearch or constructionCost.
Option C: Add an alternative producer reachable before the blocked node.

Summary
Definitions: 169
Research definitions: 72
Building definitions: 65
Research reachable: NO
Building reachable: NO
Resource deadlock: FOUND
Research cycle: None
Highest TechLevel: 1
