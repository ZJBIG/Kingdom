using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using NUnit.Framework;

public sealed class EarlyVerticalSlicePacingTests
{
    [Serializable]
    private sealed class PacingCheckpoint
    {
        public string SourceTest;
        public string CreatedUtc;
        public string[] CompletedCommands;
        public int CommandIndex;
        public double ElapsedSeconds;
        public double IndustrialEnteredAt;
        public double SpacerEnteredAt;
        public double GameSeconds;
        public int Session;
        public double SessionEnd;
        public int AccelerationMultiplier;
    }

    [Test]
    public void MetalMine_DoesNotRequireItsOwnOutputs()
    {
        BuildingDef mine = DataBase<BuildingDef>.Find("MetalMine");
        Assert.That(
            mine.ResourceRequirements.Any(pair =>
                pair.First.Id == "BasicMetalOre"),
            Is.False);
        Assert.That(
            mine.ResourceRequirements.Any(pair => pair.First.Id == "BasicMetalIngot"),
            Is.False);
    }

    [Test]
    public void MetalSmelter_DoesNotRequireItsOwnOutput()
    {
        BuildingDef foundry = DataBase<BuildingDef>.Find("MetalSmelter");
        Assert.That(
            foundry.ResourceRequirements.Select(pair => pair.First.Id),
            Does.Not.Contain("BasicMetalIngot"));
    }

    [Test]
    public void IronworkingAndSteelmaking_FollowsReachableBronzeChain()
    {
        ResearchDef iron = DataBase<ResearchDef>.Find("IronworkingAndSteelmaking");
        Assert.That(
            iron.Prerequisites.Select(value => value.Id),
            Does.Contain("BronzeAlloyCrafting"));
    }

    [Test, Category("PacingDiagnostic")]
    public void NewGame_FixedMainlineTrace() => RunMainlineTrace(false, "R:Industrialization");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_ThreeDailyVisitsTrace() => RunMainlineTrace(true, "R:Industrialization");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_IndustrialStartupThreeDailyVisitsTrace() => RunMainlineTrace(true, "L:industrial-startup");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_IndustrialProcessingThreeDailyVisitsTrace() => RunMainlineTrace(true, "L:industrial-processing");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_AerospacePreparationThreeDailyVisitsTrace() => RunMainlineTrace(true, "L:aerospace-preparation");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_OrbitalThreeDailyVisitsTrace() => RunMainlineTrace(true, "R:OrbitalEngineering");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_OrbitalThreeDailyVisitsAtFiveTimesTrace() => RunMainlineTrace(true, "R:OrbitalEngineering", 5);

    [Test, Category("PacingDiagnostic")]
    public void NewGame_FirstObservatoryThreeDailyVisitsTrace() => RunMainlineTrace(true, "L:first-observatory");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_FirstHomeSectorThreeDailyVisitsTrace() => RunMainlineTrace(true, "S:DawnRing");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_AzurePoolThreeDailyVisitsTrace() => RunMainlineTrace(true, "S:AzurePool");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_PhantomMaterialsThreeDailyVisitsTrace() => RunMainlineTrace(true, "B:PhantomMaterialsFabricator");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_PhaseMaterialsThreeDailyVisitsTrace() => RunMainlineTrace(true, "B:PhaseMaterialSynthesisArray");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_ReusableLaunchThreeDailyVisitsTrace() => RunMainlineTrace(true, "W:ReusableLaunchStages");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_OrbitalHabitatThreeDailyVisitsTrace() => RunMainlineTrace(true, "B:OrbitalHabitatMegastructure");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_SolarSystemSixSectorsThreeDailyVisitsTrace() => RunMainlineTrace(true, "S:HeliosCore");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_PlanetProjectsThreeDailyVisitsTrace() => RunMainlineTrace(true, "L:planet-projects-complete");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_FirstInterstellarThreeDailyVisitsTrace() => RunMainlineTrace(true, "C:AlphaCentauri");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_ComprehensiveFactoryThreeDailyVisitsTrace() => RunMainlineTrace(true, "L:comprehensive-running");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_ExpeditionReadyThreeDailyVisitsTrace() => RunMainlineTrace(true, "L:expedition-ready");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_UltraEntryThreeDailyVisitsTrace() => RunMainlineTrace(true, "L:ultra-entry");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_UltraFactoriesThreeDailyVisitsTrace() => RunMainlineTrace(true, "L:ultra-factories");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_UltraProjectThreeDailyVisitsTrace() => RunMainlineTrace(true, "L:project-complete");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_UltraMainlineThreeDailyVisitsTrace() => RunMainlineTrace(true, "L:ultra-mainline-complete");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_UltraMainlineThreeDailyVisitsAtFiveTimesTrace() => RunMainlineTrace(true, "L:ultra-mainline-complete", 5);

    private static readonly string[] MainlineCommands =
    {
        // One hand-authored route: investments are explicit player commands, never strategy selection.
        "B:WoodHouse", "R:Agriculture", "B:Farm", "R:ControlledFire", "R:Quarry", "B:Quarry",
        "B:WoodHouse", "R:KnowledgeSharing", "B:KnowledgeCircle", "B:KnowledgeCircle", "B:KnowledgeCircle", "R:ClayExtraction", "B:ClayPit",
        "B:WoodHouse", "B:Farm", "R:StoneProcessing", "B:StoneCuttingWorkshop", "R:Woodworking", "B:WoodHouse", "B:Lumberyard",
        "R:Mathematics", "R:Calendar", "B:FiberGatheringCamp", "R:AnimalHusbandry", "R:StoneAgeSettlement",
        "R:CivilizationPlanning", "R:StoneAgeTransition",
        "B:WoodHouse", "B:Farm", "B:FiberGatheringCamp", "B:FiberGatheringCamp", "R:Mining",
        "B:MetalMine", "B:MetalMine", "B:CoalMine", "B:CoalMine", "R:CharcoalMaking", "R:Measurement",
        "R:MultiMetalSmeltingFundamentals", "B:MetalSmelter", "R:BronzeAlloyCrafting", "R:IronworkingAndSteelmaking",
        "R:TextileCraft", "B:WeavingWorkshop", "R:CeramicFiring", "B:CeramicKiln", "R:WrittenRecords",
        "B:ScribeHut", "B:ScribeHut", "B:ScribeHut",
        "R:FeudalAdministration", "B:WoodHouse", "B:Farm", "R:Steelmaking", "B:SteelForge", "B:SteelForge", "B:SteelForge",
        "R:MechanicalEngineering", "R:Industrialization",
        "R:FoodStorage", "B:Granary",
        // Paid industrial-startup: housing, food, power, logistics and upstream producers are included.
        "B:CoalMine", "B:CoalMine", "B:ScribeHut", "B:ScribeHut", "B:ScribeHut",
        "R:Bookmaking", "B:Library", "B:Library", "B:Lumberyard", "B:Lumberyard",
        "B:Lumberyard", "B:Quarry", "B:Quarry", "B:Quarry", "B:Quarry",
        "B:Quarry", "B:StoneCuttingWorkshop", "B:StoneCuttingWorkshop", "B:StoneCuttingWorkshop", "B:StoneCuttingWorkshop",
        "B:FiberGatheringCamp", "B:Farm", "B:Farm", "B:Farm", "B:Farm",
        "B:Farm", "R:IrrigationEngineering", "B:IrrigationWorks", "B:IrrigationWorks", "R:UrbanHousing",
        "B:TownHouse", "B:TownHouse", "B:TownHouse", "B:TownHouse", "B:TownHouse",
        "B:TownHouse", "B:TownHouse", "R:TradeRoutes", "B:Caravanserai", "B:Caravanserai",
        "B:Caravanserai", "B:CoalMine", "B:CoalMine", "B:CoalMine", "B:CoalMine",
        "B:CoalMine", "B:CoalMine", "B:CoalMine", "B:CoalMine", "B:CoalMine",
        "B:CoalMine", "B:CoalMine", "B:CoalMine", "B:CoalMine", "B:CoalMine",
        "B:CoalMine", "B:CoalMine", "B:CoalMine", "B:CoalMine", "B:CoalMine",
        "B:CoalMine", "B:CoalMine", "B:CoalMine", "B:MetalMine", "B:MetalMine",
        "B:MetalMine", "B:MetalMine", "B:MetalMine", "B:MetalMine", "B:MetalMine",
        "B:MetalMine", "B:MetalMine", "B:MetalMine", "B:MetalMine", "B:MetalMine",
        "B:MetalSmelter", "B:MetalSmelter", "B:MetalSmelter", "B:MetalSmelter", "B:MetalSmelter",
        "B:MetalSmelter", "B:SteelForge", "B:SteelForge", "B:SteelForge", "B:SteelForge",
        "B:SteelForge", "R:IndustrialWorkshop", "R:Standardization", "R:ElectricalEngineering", "B:WireMill",
        "R:SteamPower", "B:SteamPlant", "R:Coking", "B:CokeOven", "R:PowerGridEngineering",
        "R:PetroleumExtraction", "R:IndustrialChemistry", "B:OilDerrick", "B:OilDerrick", "B:ChemicalPlant",
        "R:PrecisionManufacturing", "B:MachineFactory", "L:industrial-startup",
        // Paid industrial-processing: housing, food, power, logistics and upstream producers are included.
        "R:PublicHealth", "B:IrrigationWorks", "B:IrrigationWorks", "B:IrrigationWorks", "B:IrrigationWorks",
        "B:IrrigationWorks", "B:IrrigationWorks", "B:IrrigationWorks", "B:IrrigationWorks", "B:IrrigationWorks",
        "B:TownHouse", "B:TownHouse", "B:TownHouse", "B:TownHouse", "B:TownHouse",
        "B:TownHouse", "B:TownHouse", "B:CoalMine", "B:CoalMine", "B:CoalMine",
        "B:CoalMine", "B:MetalMine", "B:MetalMine", "B:MetalSmelter", "B:SteelForge",
        "B:SteelForge", "B:SteelForge", "B:Caravanserai", "B:Caravanserai", "B:Caravanserai",
        "B:Caravanserai", "B:OilDerrick", "B:OilDerrick", "B:OilRefinery", "W:DraftingTables",
        "W:PrecisionTooling", "W:PoweredMining", "B:CokeOven", "B:CokeOven", "B:CokeOven",
        "B:CentralPowerStation", "R:ConcreteEngineering", "B:IndustrialStoneworks", "B:IndustrialStoneworks", "W:ReinforcedBoilers",
        "W:RotaryKilns", "B:BuildingMaterialsComplex", "R:ScientificMethod", "R:ModernUniversity", "B:University",
        "R:ScientificInstrumentation", "W:LaboratoryGlassware", "W:ElectricalInstrumentation", "W:PressurizedReactors", "W:ContinuousDistillation",
        "W:ConcreteBatching", "D:StoneCuttingWorkshop", "D:StoneCuttingWorkshop", "D:StoneCuttingWorkshop", "D:Quarry",
        "D:Quarry", "D:Quarry", "D:Quarry", "R:IndustrialHabitationEngineering", "U:TownHouse",
        "B:University", "U:TownHouse", "L:industrial-processing",
        // Paid aerospace-preparation: fund smelting and electronics before their later research bills.
        "R:IndustrialMetalSmelting", "W:CokeOvenOptimization", "W:IntegratedFurnaces", "U:MetalSmelter", "D:MetalSmelter",
        "D:MetalSmelter", "D:MetalSmelter", "D:MetalSmelter", "D:MetalSmelter", "D:MetalSmelter",
        "D:SteelForge", "D:SteelForge", "D:SteelForge", "D:SteelForge", "D:SteelForge",
        "D:SteelForge", "D:SteelForge", "D:SteelForge", "D:SteelForge", "D:SteelForge",
        "B:WireMill", "B:WireMill", "R:RailwayEngineering", "R:ElectricalCommunication", "R:IndustrialExplosives",
        "R:DeepOilDrilling", "W:RotaryDrillingHeads", "W:ControlledBlasting", "U:OilDerrick", "U:OilRefinery",
        "U:CoalMine", "R:FactoryOrganization", "R:MassProduction", "R:LogisticsManagement", "R:IntegratedMetalResourceDevelopment",
        "W:ConveyorSystems", "W:MetalOreSeparationSystem", "U:MetalMine", "D:MetalMine", "D:MetalMine",
        "D:MetalMine", "D:MetalMine", "D:MetalMine", "D:MetalMine", "D:MetalMine",
        "D:MetalMine", "D:MetalMine", "D:MetalMine", "D:MetalMine", "D:MetalMine",
        "D:MetalMine", "B:ChemicalPlant", "R:IndustrialResearchCoordination", "W:IndustrialResearchCoordinationCenter", "U:Caravanserai",
        "D:Caravanserai", "D:Caravanserai", "D:Caravanserai", "R:CombustionEngines", "R:IndustrialAgriculture",
        "W:FuelInjection", "W:AgriculturalMachinery", "U:IrrigationWorks", "U:IrrigationWorks", "D:IrrigationWorks",
        "D:IrrigationWorks", "D:IrrigationWorks", "D:IrrigationWorks", "D:IrrigationWorks", "D:IrrigationWorks",
        "D:IrrigationWorks", "U:TownHouse", "U:TownHouse", "U:TownHouse", "U:TownHouse",
        "R:MechanizedForestry", "W:MechanizedForestryEquipment", "U:Lumberyard", "D:Lumberyard", "D:Lumberyard",
        "U:CokeOven", "D:CokeOven", "D:CoalMine", "D:CoalMine", "D:CoalMine",
        "D:CoalMine", "D:CoalMine", "D:CoalMine", "D:CoalMine", "D:CoalMine",
        "D:CoalMine", "D:CoalMine", "D:CoalMine", "D:CoalMine", "D:CoalMine",
        "D:CoalMine", "D:CoalMine", "D:CoalMine", "D:CoalMine", "D:CoalMine",
        "D:CoalMine", "D:CoalMine", "D:CoalMine", "D:CoalMine", "D:CoalMine",
        "D:CoalMine", "D:CoalMine", "D:CoalMine", "D:CoalMine", "R:AdvancedCeramicEngineering",
        "U:ClayPit", "B:ClayPit", "W:MechanicalLooms", "B:MechanizedTextileMill", "B:CentralPowerStation",
        "B:University", "R:AdvancedMetalMetallurgy", "B:AdvancedMetalSmelter", "R:AdvancedMetalHydrometallurgy", "U:AdvancedMetalSmelter",
        "R:ChlorideTitaniumMetallurgy", "R:TitaniumAlloyEngineering", "B:TitaniumAlloySmelter", "L:aerospace-preparation",
        "R:OrbitalEngineering",
        // First foothold, industrial support, phantom/phase materials and the first paid observatory.
        "B:LaunchCenter", "R:HomeSystemSurvey", "S:DawnRing", "B:DawnOrbitalObservatory",
        "R:LogisticsManagement", "W:StandardizedFreightContainers",
        "W:InsulatedWindings", "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex", "S:AzurePool",
        "U:CokeOven", "U:SteamPlant", "U:Caravanserai", "B:WireMill",
        "B:WireMill", "W:ChlorideReductionRetorts", "B:TitaniumAlloySmelter", "R:MilitaryIndustry",
        "B:MachineFactory", "R:OrbitalStationEngineering", "R:OrbitalPowerTransmission", "R:OrbitalLogisticsInfrastructure",
        "R:DeepSpaceShipbuilding", "R:DeepSpaceFleet", "R:InterstellarNavigation", "R:PhantomMaterials",
        "B:PhantomMaterialsFabricator", "B:IndustrialHabitationComplex", "B:OrbitalSolarArray", "R:DeepSpaceSurvey",
        "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex", "R:PhaseMaterialEngineering", "B:PhaseMaterialSynthesisArray",
        "B:IndustrialHabitationComplex", "W:AutonomousSurveyDrones", "U:University", "L:first-observatory",
        // Grow old industry first, then replace a paid group with the comprehensive factory.
        "W:AdvancedMetalElectrolyticCells", "W:VacuumArcFurnace", "S:Terminus", "R:ModernMedicine", "B:IntegratedMetalMine",
        "B:IntegratedMetalMine", "B:IndustrialMetalSmelter", "B:TitaniumAlloySmelter", "B:TitaniumAlloySmelter", "B:MachineFactory",
        "U:WireMill", "U:WireMill", "R:OrbitalConstructionAutomation", "R:OrbitalVacuumMetallurgy", "R:DeepSpaceIndustrialIntegration",
        "L:orbital-integration", "U:BuildingMaterialsComplex", "R:BioregenerativeLifeSupport", "R:OrbitalAgroecology", "W:ReusableLaunchStages",
        "W:ModularHabitatSystems", "B:OrbitalHabitatMegastructure", "W:OrbitalLifeSupportNetworks", "W:OrbitalAgroponicSystems", "B:OrbitalAgroecologyArray",
        "B:OrbitalAgroecologyArray", "B:OrbitalHabitatMegastructure", "B:OrbitalHabitatMegastructure", "B:OrbitalHabitatMegastructure", "D:IndustrialHabitationComplex",
        "D:IndustrialHabitationComplex", "D:IndustrialHabitationComplex", "D:IndustrialHabitationComplex", "D:IndustrialHabitationComplex", "D:IndustrialHabitationComplex",
        "D:IndustrialHabitationComplex", "D:IndustrialHabitationComplex", "D:IndustrialHabitationComplex", "D:IndustrialHabitationComplex", "B:EarthMoonLogisticsHub",
        "B:OrbitalSolarArray", "B:IndustrialOilExtractionComplex", "B:IntegratedPetrochemicalComplex", "B:IntegratedPetrochemicalComplex", "B:AdvancedMetalRefinery",
        "B:PhantomMaterialsFabricator", "B:PhantomMaterialsFabricator", "B:PhantomMaterialsFabricator", "B:PhaseMaterialSynthesisArray", "B:PhaseMaterialSynthesisArray",
        "B:TitaniumAlloySmelter", "B:TitaniumAlloySmelter", "B:TitaniumAlloySmelter", "B:TitaniumAlloySmelter", "U:MachineFactory",
        "U:MachineFactory", "D:WireMill", "D:WireMill", "R:DeepSpaceThermalExchangeTheory", "W:OrbitalThermalManagement",
        "U:IndustrialCarbonizationRetort", "D:IndustrialCarbonizationRetort", "B:TitaniumAlloySmelter", "R:AutonomousOrbitalMining", "W:AutonomousOrbitalMiningSystems",
        "W:OrbitalVacuumFurnaces", "L:comprehensive-ready", "B:OrbitalResourceExtractionArray", "D:TitaniumAlloySmelter", "D:TitaniumAlloySmelter",
        "D:TitaniumAlloySmelter", "D:TitaniumAlloySmelter", "D:TitaniumAlloySmelter", "D:TitaniumAlloySmelter", "D:TitaniumAlloySmelter",
        "D:OrbitalMachiningComplex", "D:OrbitalMachiningComplex", "D:OrbitalWireWorks", "D:OrbitalBuildingMaterialsWorks", "L:comprehensive-running",
        // Research and supply investments continue while distant campaigns and civilization stages run.
        "W:AutomatedShipyardAssembly", "B:Shipyard", "B:Shipyard", "B:Shipyard", "B:Shipyard",
        "R:OrbitalStructuralDynamicsTheory", "R:DeepSpaceLogisticsReliability", "R:QuantumComputing", "U:DeepSpaceObservatory", "R:GravityAssistTrajectoryTheory",
        "R:DeepSpaceNavigationReliability", "R:GravitationalCommunicationTheory", "R:DeepSpacePowerGridResilience", "R:MatterTransmutationTheory", "R:PhaseFieldNavigation",
        "W:AdvancedCompositeLayup", "W:PhaseFieldContainment", "W:QuantumErrorCorrection", "W:PhaseMaterialCalibration", "W:DeepSpaceNetworkAutomation",
        "W:RedundantNavigationControl", "W:GravitationalWaveReceiverArray", "W:OrbitalPowerBeaming", "W:OrbitalPowerGridStabilization", "W:ControlledMatterTransmutation",
        "U:QuantumComputingArray", "R:InterstellarLogisticsDoctrine", "R:InterstellarCombatLogistics", "W:AutonomousFleetLogistics", "W:InterstellarSupplyDoctrine",
        "W:InterstellarCombatSupplySystems", "R:InterstellarSupplyChainTheory", "W:AutomatedFleetResupplyModules", "L:expedition-ready", "S:ShardCrown",
        "S:ThunderGate", "S:HeliosCore", "C:AlphaCentauri", "A:ProximaB", "R:InterstellarOccupationAdministration",
        "W:InterstellarOccupationGovernance", "R:InterstellarKnowledgeCoordination", "W:InterstellarResearchCoordinationArray", "R:InterstellarCampaignTimingTheory", "R:PhaseMaterialDurabilityTheory",
        "R:MaterialLifecycleControl", "R:TechnologicalSingularity", "C:ProximaB", "L:ultra-entry", "R:PhaseFieldEngineering",
        "B:OrbitalHabitatMegastructure", "B:OrbitalHabitatMegastructure", "B:OrbitalHabitatMegastructure", "D:IndustrialHabitationComplex", "D:IndustrialHabitationComplex",
        "B:PhaseEnergyArray", "L:project-ready", "P:Prototype", "B:AdvancedMetalRefinery", "B:IntegratedPetrochemicalComplex",
        "L:prototype-running", "R:DistributedCognitionProtocol", "R:AutonomousMatterAssembly", "B:UltraComputingNexus", "R:PhaseGridSynchronization",
        "R:InterstellarResourceCoordination", "L:autonomous-ready", "B:AutonomousMatterFabricator", "D:PhantomMaterialsFabricator", "D:PhantomMaterialsFabricator",
        "D:PhantomMaterialsFabricator", "D:PhantomMaterialsFabricator", "D:PhaseMaterialSynthesisArray", "D:PhaseMaterialSynthesisArray", "D:PhaseMaterialSynthesisArray",
        "L:ultra-factories", "K:Prototype", "P:Stabilization", "B:Shipyard", "B:Shipyard",
        "B:OrbitalSolarArray", "R:UltraExpeditionDoctrine", "R:UltraCampaignContinuity", "R:FleetDamageControlTheory", "R:SelfRepairingFleetArchitecture",
        "R:AdaptiveFleetLogistics", "B:Shipyard", "B:Shipyard", "L:expansion-ready", "K:Stabilization",
        "P:Expansion", "R:PhaseEnergyHarmonics", "L:expansion-running", "K:Expansion", "L:project-complete",
        "C:TauCetiFoundry", "A:SiriusResourceBelt", "B:Shipyard", "R:CausalOccupationNetwork", "R:ClosedLoopAgroecology",
        "R:AutonomousCivicProductivity", "R:PredictiveCasualtyControl", "L:final-campaign-supplied", "C:SiriusResourceBelt", "L:ultra-mainline-complete",
        // Optional planetary development follows the mainline endpoint and cannot gate it.
        "R:OrbitalPropellantEngineering", "I:AzurePool", "I:ThunderGate", "I:HeliosCore",
        "B:TerminusMaterialsWorks", "B:ShardCrownOreSorter", "B:HeliosPowerArray", "L:planet-surveys-running",
        "E:AzurePool", "B:AzureIceSeaArk", "E:ThunderGate", "B:ThunderGatePropellantWorks",
        "W:ThunderCryogenicSeparation", "W:ThunderParallelRefining", "W:ThunderOrbitalLoadingRing",
        "E:HeliosCore", "B:DawnDysonSphere", "W:DysonCollectorFleet", "W:DysonStellarBelt", "W:DysonControlNetwork",
        "L:planet-projects-complete"
    };


    private static void RunMainlineTrace(bool visits, string endpoint, int acceleration = 20)
    {
        if (Environment.GetEnvironmentVariable("KINGDOM_RUN_PACING_TRACE") != "1")
            Assert.Ignore("Set KINGDOM_RUN_PACING_TRACE=1 to run the fixed-route diagnostic.");
        Assert.That(UnityEngine.Object.FindObjectOfType<GameManager>(), Is.Null);
        Assert.That(UnityEngine.Object.FindObjectOfType<ResourceManager>(), Is.Null);
        Assert.That(UnityEngine.Object.FindObjectOfType<BuildingManager>(), Is.Null);
        Assert.That(UnityEngine.Object.FindObjectOfType<ResearchManager>(), Is.Null);
        Assert.That(UnityEngine.Object.FindObjectOfType<WorkshopManager>(), Is.Null);
        Assert.That(UnityEngine.Object.FindObjectOfType<SimulationManager>(), Is.Null);
        Assert.That(UnityEngine.Object.FindObjectOfType<SaveManager>(), Is.Null);
        var objects = new List<GameObject>();
        string saveRoot = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
            "Temp", "KingdomPacingTrace-" + Guid.NewGuid().ToString("N"));
        string checkpointInput = Environment.GetEnvironmentVariable("KINGDOM_PACING_CHECKPOINT_INPUT");
        string checkpointOutput = Environment.GetEnvironmentVariable("KINGDOM_PACING_CHECKPOINT_OUTPUT");
        string checkpointBoundary = Environment.GetEnvironmentVariable("KINGDOM_PACING_CHECKPOINT_AT") ?? "R:Industrialization";
        TutorialManager initialTutorial = TutorialManager.Current;
        Action<string> recordSnapshot = null;
        T Create<T>() where T : Component
        {
            var host = new GameObject("MainlineTrace-" + typeof(T).Name);
            objects.Add(host);
            return host.AddComponent<T>();
        }
        try
        {
            Directory.CreateDirectory(saveRoot);
            SaveManager.SetSaveRootOverrideForTests(saveRoot);
            PacingCheckpoint checkpoint = null;
            if (!string.IsNullOrWhiteSpace(checkpointInput))
            {
                Assert.That(visits, Is.True, "Visit checkpoints cannot seed the continuous-play trace.");
                checkpointInput = Path.GetFullPath(checkpointInput);
                checkpoint = JsonUtility.FromJson<PacingCheckpoint>(File.ReadAllText(
                    Path.Combine(checkpointInput, "pacing-checkpoint.json")));
                Assert.That(checkpoint, Is.Not.Null);
                Assert.That(checkpoint.SourceTest, Is.Not.Null.And.Not.Empty);
                Assert.That(checkpoint.CommandIndex, Is.InRange(1, MainlineCommands.Length));
                Assert.That(checkpoint.CompletedCommands,
                    Is.EqualTo(MainlineCommands.Take(checkpoint.CommandIndex).ToArray()),
                    "A changed paid prefix requires a newly captured checkpoint.");
                string boundary = checkpoint.CompletedCommands[checkpoint.CommandIndex - 1];
                Assert.That(boundary.StartsWith("L:", StringComparison.Ordinal) ||
                    boundary == "R:Industrialization" || boundary == "R:OrbitalEngineering", Is.True,
                    "A reusable checkpoint must be a completed paid era or supply checkpoint.");
                Assert.That(checkpoint.ElapsedSeconds, Is.GreaterThanOrEqualTo(0d));
                Assert.That(checkpoint.IndustrialEnteredAt, Is.InRange(0d, checkpoint.ElapsedSeconds));
                Assert.That(checkpoint.Session, Is.GreaterThanOrEqualTo(0));
                Assert.That(checkpoint.SessionEnd - checkpoint.ElapsedSeconds, Is.InRange(0d, 900d));
                Assert.That(checkpoint.AccelerationMultiplier, Is.EqualTo(1).Or.EqualTo(5).Or.EqualTo(20),
                    "The checkpoint must retain a supported selected speed.");
                if (endpoint == "L:ultra-mainline-complete")
                    Assert.That(checkpoint.CompletedCommands, Does.Not.Contain("R:OrbitalEngineering"),
                        "Full-route acceptance must retain all late Food and project investment evidence. Use late checkpoints only for individual stage diagnostics.");
                File.Copy(Path.Combine(checkpointInput, "KingdomSave-v1.json"),
                    Path.Combine(saveRoot, "KingdomSave-v1.json"));
            }
            var game = Create<GameManager>();
            var resources = Create<ResourceManager>();
            resources.InitializeForEditor();
            var buildings = Create<BuildingManager>();
            var researches = Create<ProgressionMilestoneTestResearchManager>();
            researches.EnsureInitialized();
            var workshops = Create<WorkshopManager>();
            workshops.ResetForLoadForEditor();
            var simulation = Create<SimulationManager>();
            var saves = Create<SaveManager>();
            game.InitializeNewGameForEditor();
            game.Sectors.InitializeDefinitions();
            if (checkpoint != null)
            {
                Assert.That(saves.LoadOrCreateGame(), Is.True, "The captured v1 save must load without a new-game fallback.");
                TechLevel checkpointEra = checkpoint.CompletedCommands.Contains("R:TechnologicalSingularity")
                    ? TechLevel.Ultra : checkpoint.CompletedCommands.Contains("R:OrbitalEngineering")
                        ? TechLevel.Spacer : TechLevel.Industrial;
                Assert.That(game.State.TechLevel, Is.EqualTo(checkpointEra));
                Assert.That(researches.IsResearchCompleted("Industrialization"), Is.True);
            }
            simulation.ManualTick(0d);
            saves.SetReady(true);
            simulation.SetRunning(true);
            if (checkpoint != null)
                simulation.SetAccelerationMultiplier(checkpoint.AccelerationMultiplier == 1 ? 1 : acceleration);

            string[] commands = MainlineCommands;
            int commandCount = Array.IndexOf(commands, endpoint) + 1;
            Assert.That(commandCount, Is.GreaterThan(0), "The endpoint must belong to the one command table.");
            bool industrial = endpoint == "R:Industrialization";
            int commandIndex = checkpoint?.CommandIndex ?? 0;
            Assert.That(commandIndex, Is.LessThan(commandCount), "The chosen endpoint must follow the captured prefix.");
            bool requested = false;
            bool paymentRecorded = false;
            double layoutStartedAt = -1d;
            Dictionary<ResourceDef, ExpantaNum> layoutAmountsBefore = null;
            ExpantaNum previousPopulation = game.State.Population.Population;
            double lastMilestone = checkpoint?.ElapsedSeconds ?? 0d;
            double lastProductiveAt = lastMilestone;
            int progressCommandIndex = -1;
            ExpantaNum previousCommandProgress = ExpantaNum.Zero;
            double longestMilestoneGap = 0d;
            double elapsed = checkpoint?.ElapsedSeconds ?? 0d;
            double industrialEnteredAt = checkpoint?.IndustrialEnteredAt ?? -1d;
            double spacerEnteredAt = checkpoint != null && checkpoint.CompletedCommands.Contains("R:OrbitalEngineering")
                ? checkpoint.SpacerEnteredAt : -1d;
            double sessionEnd = checkpoint?.SessionEnd ?? 900d;
            double nextSample = elapsed;
            double lastPopulationReport = elapsed - 60d;
            bool openingChecked = checkpoint != null;
            int session = checkpoint?.Session ?? 0;
            // The existing calendar contract advances one date every ten game seconds.
            double GameSeconds() => game.State.CalendarDays * 10d + game.CalendarElapsedSecondsForEditor;
            if (checkpoint != null)
                Assert.That(GameSeconds(), Is.EqualTo(checkpoint.GameSeconds).Within(1e-5d),
                    "Loading the checkpoint must preserve its actual simulated time.");
            void SaveBoundaryCheckpoint(bool endpointCapture = false)
            {
                if (!visits || string.IsNullOrWhiteSpace(checkpointOutput)) return;
                string output = Path.GetFullPath(checkpointOutput);
                if (endpointCapture) output = Path.Combine(output, "endpoint");
                string savePath = Path.Combine(output, "KingdomSave-v1.json");
                string metadataPath = Path.Combine(output, "pacing-checkpoint.json");
                Assert.That(File.Exists(savePath) || File.Exists(metadataPath), Is.False,
                    "Use a fresh checkpoint output directory to preserve previous evidence.");
                Directory.CreateDirectory(output);
                // Capture a fresh evidence file through the public v1 DTO API.
                // This does not repeatedly replace the loaded temporary primary;
                // atomic primary replacement has its own save transaction tests.
                SaveManager.KingdomSaveData data = saves.CaptureSaveData();
                SaveManager.StampSaveTimestampForEditor(data, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
                var captured = new PacingCheckpoint
                {
                    SourceTest = TestContext.CurrentContext.Test.FullName,
                    CreatedUtc = DateTime.UtcNow.ToString("O"),
                    CompletedCommands = commands.Take(commandIndex).ToArray(),
                    CommandIndex = commandIndex,
                    ElapsedSeconds = elapsed,
                    IndustrialEnteredAt = industrialEnteredAt,
                    SpacerEnteredAt = spacerEnteredAt,
                    GameSeconds = GameSeconds(),
                    Session = session,
                    SessionEnd = sessionEnd,
                    AccelerationMultiplier = simulation.AccelerationMultiplier
                };
                File.WriteAllText(metadataPath, JsonUtility.ToJson(captured, true));
                TestContext.WriteLine($"CHECKPOINT_SAVED path={output} command={commandIndex} calendarSeconds={elapsed:F3} gameSeconds={GameSeconds():F3}");
            }
            var explorationDefinitions = new[]
            {
                DataBase<SectorDef>.Find("AzurePool"),
                DataBase<SectorDef>.Find("ThunderGate"),
                DataBase<SectorDef>.Find("HeliosCore")
            };
            // Stored progress is quantized; this is a progress-equivalent budget, not an exact payment ledger.
            var explorationProgressBudgets = new Dictionary<string, Dictionary<ResourceDef, ExpantaNum>>(StringComparer.Ordinal);
            foreach (var sector in explorationDefinitions)
                explorationProgressBudgets[sector.Id] = new Dictionary<ResourceDef, ExpantaNum>();
            var explorationBefore = new ExpantaNum[explorationDefinitions.Length];
            var auditedFoodOperations = new HashSet<string>(StringComparer.Ordinal);
            var investedProjectStages = new HashSet<UltraProjectStage>();
            ExpantaNum StrategicResourceConsumption(ResourceDef resource)
            {
                ExpantaNum rate = ExpantaNum.Zero;
                if (game.UltraProject.State.Status == UltraProjectStatus.Running)
                    foreach (var cost in game.UltraProject.GetPreview().ContinuousCosts)
                        if (cost.First == resource) rate += cost.Second;
                foreach (SectorState sector in game.Sectors.OrderedStates)
                {
                    if (sector.ColonizationActive)
                        foreach (var cost in game.Sectors.GetExplorationPreview(sector.Definition, game.State, resources).ResourceCostsPerSecond)
                            if (cost.First == resource) rate += cost.Second;
                    if (sector.CampaignActive)
                        foreach (var cost in game.Sectors.GetCampaignPreview(sector.Definition, game.State, resources).ResourceCostsPerSecond)
                            if (cost.First == resource) rate += cost.Second;
                    if (sector.InteriorExplorationActive)
                        foreach (var cost in sector.Definition.InteriorExploration.ResourceRatesPerSecond)
                            if (cost.First == resource) rate += cost.Second;
                }
                return rate;
            }
            double SampleFoodPayment(double frameSeconds)
            {
                if (auditedFoodOperations.Count >= 3 || game.State.FoodNetRate < ExpantaNum.Zero ||
                    game.State.Population.Population < game.State.Population.PopulationCapacity ||
                    buildings.LaborOperatingRatio.ToDouble() < 1d - 1e-6d ||
                    game.State.PowerSatisfaction.ToDouble() < 1d - 1e-6d ||
                    game.State.LogisticsSatisfaction.ToDouble() < 1d - 1e-6d)
                    return 0d;
                double interval = simulation.TickIntervalSecondsForEditor;
                if (game.State.Events.NextBoundaryForEditor(true) <= interval) return 0d;
                ResearchState active = researches.ActiveResearch;
                if (active != null && (!active.CostPaid ||
                    active.BaseCost - active.Progress <= researches.CurrentResearchSpeed * interval))
                    return 0d;
                string kind = null;
                string operationId = null;
                ExpantaNum rate = ExpantaNum.Zero;
                ExpantaNum foodRate = ExpantaNum.Zero;
                Func<ExpantaNum> progress = null;
                if (!auditedFoodOperations.Contains("project") &&
                    game.UltraProject.State.Status == UltraProjectStatus.Running)
                {
                    UltraProjectPreview preview = game.UltraProject.GetPreview();
                    if (preview.SupplySatisfaction.ToDouble() >= 1d - 1e-6d)
                    {
                        kind = "project";
                        operationId = preview.Stage.ToString();
                        rate = preview.ProgressPerSecond;
                        foodRate = preview.FoodPerSecond;
                        progress = () => game.UltraProject.State.StageProgress;
                    }
                }
                if (kind == null)
                    foreach (SectorDef definition in DataBase<SectorDef>.All)
                    {
                        SectorState state = game.Sectors.GetState(definition);
                        if (state.ColonizationActive && !auditedFoodOperations.Contains("colonization"))
                        {
                            SectorExplorationPreview preview = game.Sectors.GetExplorationPreview(definition, game.State, resources);
                            kind = "colonization";
                            rate = preview.ProgressPerSecond;
                            foodRate = preview.FoodCostPerSecond;
                        }
                        else if (state.CampaignActive && !auditedFoodOperations.Contains("campaign"))
                        {
                            SectorCampaignPreview preview = game.Sectors.GetCampaignPreview(definition, game.State, resources);
                            if (!preview.HasSupply || preview.CombatRatio < ExpantaNum.One ||
                                preview.SupplySatisfaction.ToDouble() < 1d - 1e-6d) continue;
                            kind = "campaign";
                            rate = preview.ProgressPerSecond;
                            foodRate = preview.FoodCostPerSecond;
                        }
                        else continue;
                        operationId = definition.Id;
                        progress = () => state.CampaignProgress;
                        break;
                    }
                if (kind == null || rate <= ExpantaNum.Zero || foodRate <= ExpantaNum.Zero ||
                    progress().ToDouble() > 0.9d || game.State.FoodAmount < foodRate * interval * 2d)
                    return 0d;
                int speed = simulation.AccelerationMultiplier;
                double onlineSeconds = speed == 1
                    ? interval - simulation.AccumulatedSecondsForEditor
                    : interval / (2d * speed);
                if (onlineSeconds <= 0d || onlineSeconds > frameSeconds ||
                    simulation.AccumulatedAccelerationSecondsForEditor > 1e-9d ||
                    (speed > 1 && (saves.RemainingOfflineSeconds < interval * 100d ||
                        simulation.AccumulatedSecondsForEditor + onlineSeconds >= interval)))
                    return 0d;
                ExpantaNum before = game.State.FoodAmount;
                ExpantaNum capacity = game.State.FoodCapacity;
                ExpantaNum population = game.State.Population.Population;
                ExpantaNum net = game.State.FoodNetRate;
                ExpantaNum beforeProgress = progress();
                double beforeGameSeconds = GameSeconds();
                long beforeSegments = simulation.PreparedSegmentCountForEditor;
                simulation.Advance(onlineSeconds);
                Assert.That(simulation.PreparedSegmentCountForEditor - beforeSegments, Is.EqualTo(1),
                    "Food accounting samples exactly one real settlement segment.");
                double gameSeconds = GameSeconds() - beforeGameSeconds;
                Assert.That(gameSeconds, Is.GreaterThan(0d));
                Assert.That(game.State.Population.Population, Is.EqualTo(population));
                Assert.That(game.State.FoodCapacity, Is.EqualTo(capacity));
                Assert.That(game.State.FoodNetRate.ToDouble(), Is.EqualTo(net.ToDouble()).Within(1e-4d));
                ExpantaNum delta = progress() - beforeProgress;
                Assert.That(delta.ToDouble(), Is.EqualTo((rate * gameSeconds).ToDouble()).Within(1e-6d));
                ExpantaNum dailyGrowth = net * gameSeconds;
                ExpantaNum overflow = ExpantaNum.Max(ExpantaNum.Zero, before + dailyGrowth - capacity);
                ExpantaNum observedExtra = before + dailyGrowth - overflow - game.State.FoodAmount;
                // Progress is stored with six decimal places. A single 0.1-second tick
                // can lose a few percent when converted back to seconds; bound that
                // quantization separately, then check the full-supply payment precisely.
                ExpantaNum progressPayment = delta / rate * foodRate;
                ExpantaNum expectedExtra = foodRate * gameSeconds;
                Assert.That((progressPayment - expectedExtra).Abs().ToDouble(),
                    Is.LessThanOrEqualTo((new ExpantaNum(0.000001d) / rate * foodRate).ToDouble() + 0.002d));
                Assert.That(observedExtra.ToDouble(), Is.EqualTo(expectedExtra.ToDouble()).Within(
                    Math.Max(0.002d, expectedExtra.ToDouble() * 1e-4d)),
                    kind + " Food payment must be separate from daily population and building demand.");
                auditedFoodOperations.Add(kind);
                TestContext.WriteLine($"FOOD_ACCOUNTING kind={kind} id={operationId} speed={speed} gameSeconds={gameSeconds:F6} " +
                    $"before={before} dailyNet={net} dailyGrowth={dailyGrowth} overflow={overflow} " +
                    $"progressPayment={progressPayment} extraExpected={expectedExtra} extraObserved={observedExtra} after={game.State.FoodAmount}");
                return onlineSeconds;
            }
            void TickOnline(double seconds)
            {
                for (int i = 0; i < explorationDefinitions.Length; i++)
                    explorationBefore[i] = game.Sectors.GetState(explorationDefinitions[i]).InteriorExplorationProgress;
                double sampledSeconds = SampleFoodPayment(seconds);
                simulation.Advance(seconds - sampledSeconds);
                int drainFrames = 0;
                while (simulation.AccumulatedSecondsForEditor >= simulation.TickIntervalSecondsForEditor ||
                       simulation.AccumulatedAccelerationSecondsForEditor > 1e-9d)
                {
                    if (++drainFrames > 10000)
                        Assert.Fail("The real simulation backlog did not drain after the bounded frame budget.");
                    simulation.Advance(0d);
                }
                for (int i = 0; i < explorationDefinitions.Length; i++)
                {
                    SectorDef sector = explorationDefinitions[i];
                    var project = sector.InteriorExploration;
                    ExpantaNum delta = game.Sectors.GetState(sector).InteriorExplorationProgress - explorationBefore[i];
                    if (delta <= ExpantaNum.Zero) continue;
                    foreach (var rate in project.ResourceRatesPerSecond)
                    {
                        var budget = explorationProgressBudgets[sector.Id];
                        budget[rate.First] = (budget.TryGetValue(rate.First, out ExpantaNum previous) ? previous : ExpantaNum.Zero) +
                            rate.Second * project.DurationSeconds * delta;
                    }
                }
            }
            void ResumeAt(double nextVisit)
            {
                double gameBefore = GameSeconds();
                var amountsBefore = resources.States.ToDictionary(pair => pair.Key, pair => pair.Value.Amount);
                // Use the same reserve collector as startup, without writing a save twice per diagnostic visit.
                // Pause/save lifecycle is covered separately; this trace measures the actual economy and reserve drain.
                simulation.OnApplicationPauseForEditor(true);
                game.MarkSaveTimestampForEditor(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (long)Math.Round(nextVisit - elapsed));
                Assert.That(saves.ApplyOfflineProgress(), Is.True);
                simulation.OnApplicationPauseForEditor(false);
                Assert.That(GameSeconds(), Is.EqualTo(gameBefore).Within(1e-6d),
                    "A closed game banks time without advancing its economy.");
                foreach (var pair in amountsBefore)
                    Assert.That((resources.GetAmount(pair.Key) - pair.Value).Abs().ToDouble(),
                        Is.LessThanOrEqualTo(1e-6d), pair.Key.Id + " must not be produced during the closed interval.");
                simulation.SetAccelerationMultiplier(acceleration);
            }
            void RecordExploration(string action)
            {
                foreach (var sector in explorationDefinitions)
                {
                    var preview = game.Sectors.GetInteriorExplorationPreview(sector);
                    TestContext.WriteLine($"INTERIOR action={action} sector={sector.Id} status={preview.Status} progress={preview.Progress} remainingSeconds={preview.EstimatedSecondsRemaining} " +
                        $"missing={(preview.MissingResource != null ? preview.MissingResource.Id : "none")} progressEquivalentBudget={string.Join(",", explorationProgressBudgets[sector.Id].OrderBy(pair => pair.Key.Id, StringComparer.Ordinal).Select(pair => $"{pair.Key.Id}:{pair.Value}"))}");
                }
            }
            void Record(double seconds, string action)
            {
                if (action != "population")
                {
                    longestMilestoneGap = Math.Max(longestMilestoneGap, seconds - lastMilestone);
                    lastMilestone = seconds;
                    lastProductiveAt = seconds;
                    // Model the player reading the current guidance after an action.
                    // These are the same navigation/detail events as the UI; they
                    // award no resources and still evaluate the real completion conditions.
                    TutorialManager tutorial = TutorialManager.Ensure();
                    TutorialStep step = tutorial.Steps.FirstOrDefault(value => value.Id == tutorial.ActiveStepId);
                    if (step != null && step.Kind != TutorialStepKind.LongTerm)
                    {
                        string page = TutorialManager.GetNavigationPageForStepForEditor(step, game.State);
                        tutorial.RecordPageVisitedForEditor(page);
                        if (step.Kind == TutorialStepKind.Resources)
                            tutorial.RecordDetailViewed(page, ResourceManager.StartingResourceId);
                        tutorial.Evaluate();
                    }
                    StoryManager.RefreshProgress();
                    if (action.StartsWith("built:", StringComparison.Ordinal) ||
                        action.StartsWith("paid:", StringComparison.Ordinal) ||
                        action.StartsWith("purchased:", StringComparison.Ordinal))
                    {
                        if (game.UltraProject.State.Status == UltraProjectStatus.Running)
                        {
                            investedProjectStages.Add(game.UltraProject.State.CurrentStage);
                            TestContext.WriteLine($"ACTIVE_INVESTMENT project={game.UltraProject.State.CurrentStage} action={action}");
                        }
                        if (game.State.Campaign.Active)
                            TestContext.WriteLine($"ACTIVE_INVESTMENT campaign=true action={action}");
                    }
                }
                TestContext.WriteLine($"MAINLINE t={seconds:F1} gameSeconds={GameSeconds():F3} action={action} wood={resources.GetAmount(DataBase<ResourceDef>.Find("WoodLog"))} " +
                    $"population={game.State.Population.Population}/{game.State.Population.PopulationCapacity} " +
                    $"foodNet={game.State.FoodNetRate} productivity={buildings.AvailableProductivity} researchSpeed={researches.CurrentResearchSpeed}");
            }
            // A player checks commands once a second; Advance still executes every production tick at its real cadence.
            double step = 1d;
            // The user allows 33%-300% of nominal pacing; include the complete 9 + 12 + 84 day route.
            double horizon = (industrial ? visits ? 28d : 3d : 9d + 12d + 84d) * 86400d;
            int[] visitHours = { 0, 5, 13 }; // Relative to the first visit at 08:00.
            var reportedColonizationShortages = new HashSet<string>();
            string[] waitKinds = { "materials", "research", "food", "power", "logistics", "territory", "productivity", "operation", "player" };
            var waitSeconds = new double[waitKinds.Length];
            UltraProjectStage ProjectStage(string id) => id switch
            {
                "Prototype" => UltraProjectStage.Prototype,
                "Stabilization" => UltraProjectStage.Stabilization,
                "Expansion" => UltraProjectStage.Expansion,
                _ => throw new ArgumentException("Unknown authored project stage: " + id)
            };
            void RecordShortages()
            {
                string command = commands[commandIndex];
                string id = command.Substring(2);
                bool upgrade = command.StartsWith("U:", StringComparison.Ordinal);
                BuildFailure buildingFailure = upgrade
                    ? buildings.GetUpgradeFailure(DataBase<BuildingDef>.Find(id), ExpantaNum.One)
                    : command.StartsWith("B:", StringComparison.Ordinal)
                        ? buildings.GetBuildFailure(DataBase<BuildingDef>.Find(id), ExpantaNum.One)
                        : BuildFailure.None;
                TestContext.WriteLine($"BLOCKED command={command} reason={Blocker()} " +
                    $"buildFailure={buildingFailure}");
                if (command.StartsWith("L:", StringComparison.Ordinal)) return;
                if (command.StartsWith("P:", StringComparison.Ordinal) || command.StartsWith("K:", StringComparison.Ordinal))
                {
                    UltraProjectPreview preview = game.UltraProject.GetPreview(UltraProjectDoctrine.Stable);
                    TestContext.WriteLine($"PROJECT stage={preview.Stage} status={preview.Status} progress={preview.Progress} " +
                        $"satisfaction={preview.SupplySatisfaction} failure={preview.Failure} food={preview.FoodPerSecond} " +
                        $"power={preview.PowerPerSecond} logistics={preview.LogisticsPerSecond}");
                    foreach (var cost in preview.StartupCosts.Concat(preview.ContinuousCosts))
                    {
                        ResourceState resource = resources.GetState(cost.First);
                        TestContext.WriteLine($"PROJECT_SUPPLY resource={cost.First.Id} costOrRate={cost.Second} " +
                            $"amount={resource.Amount} production={resource.ProductionRate} consumption={resource.ConsumptionRate}");
                    }
                    return;
                }
                if (command.StartsWith("S:", StringComparison.Ordinal))
                {
                    var sector = DataBase<SectorDef>.Find(id);
                    ExpantaNum duration = sector.ColonizationDurationSeconds /
                        ExpantaNum.Max(ExpantaNum.One, ProgressionModifierManager.Current.ExplorationPowerMultiplier);
                    TestContext.WriteLine($"COLONIZATION sector={id} progress={game.Sectors.GetState(sector).CampaignProgress} foodNet={game.State.FoodNetRate} foodCostPerSecond={sector.ColonizationFoodPerSecond}");
                    foreach (var cost in sector.ColonizationResourceRatesPerSecond)
                    {
                        var resource = resources.States[cost.First];
                        TestContext.WriteLine($"COLONIZATION supply={resource.Definition.Id} amount={resource.Amount} preparedBudget={cost.Second * duration} costPerSecond={cost.Second} production={resource.ProductionRate} consumption={resource.ConsumptionRate}");
                    }
                    foreach (string resourceId in new[] { "CrudeOil", "Coke", "Coal" })
                    {
                        var resource = resources.States[DataBase<ResourceDef>.Find(resourceId)];
                        TestContext.WriteLine($"FEEDSTOCK resource={resourceId} amount={resource.Amount} production={resource.ProductionRate} consumption={resource.ConsumptionRate}");
                    }
                    foreach (string buildingId in new[] { "ChemicalPlant", "OilDerrick", "IntegratedPetrochemicalComplex", "CokeOven", "IndustrialCarbonizationRetort" })
                    {
                        buildings.States.TryGetValue(DataBase<BuildingDef>.Find(buildingId), out BuildingState state);
                        TestContext.WriteLine($"FEEDSTOCK building={buildingId} amount={state?.Amount ?? ExpantaNum.Zero} efficiency={state?.Efficiency ?? ExpantaNum.Zero}");
                    }
                    return;
                }
                if (command.StartsWith("E:", StringComparison.Ordinal))
                {
                    var sector = DataBase<SectorDef>.Find(id);
                    var preview = game.Sectors.GetInteriorExplorationPreview(sector);
                    TestContext.WriteLine($"INTERIOR-SUPPLY sector={id} status={preview.Status} progress={preview.Progress} remainingSeconds={preview.EstimatedSecondsRemaining} " +
                        $"missing={(preview.MissingResource != null ? preview.MissingResource.Id : "none")}");
                    foreach (var rate in sector.InteriorExploration.ResourceRatesPerSecond)
                    {
                        var resource = resources.States[rate.First];
                        TestContext.WriteLine($"INTERIOR-RATE sector={id} resource={rate.First.Id} rate={rate.Second} amount={resource.Amount} production={resource.ProductionRate} consumption={resource.ConsumptionRate}");
                    }
                    return;
                }
                if (command.StartsWith("C:", StringComparison.Ordinal) || command.StartsWith("A:", StringComparison.Ordinal))
                {
                    var sector = DataBase<SectorDef>.Find(id);
                    var state = game.Sectors.GetState(sector);
                    var preview = game.Sectors.GetCampaignPreview(sector, game.State, resources);
                    TestContext.WriteLine($"CAMPAIGN-SUPPLY sector={id} active={state.CampaignActive} progress={state.CampaignProgress} " +
                        $"casualties={state.CampaignCasualties} power={preview.EffectivePower} supply={preview.HasSupply} " +
                        $"food={game.State.FoodAmount} foodRate={preview.FoodCostPerSecond}");
                    foreach (var rate in sector.CampaignResourceRatesPerSecond)
                    {
                        var resource = resources.States[rate.First];
                        TestContext.WriteLine($"CAMPAIGN-RATE sector={id} resource={rate.First.Id} rate={rate.Second} amount={resource.Amount} " +
                            $"production={resource.ProductionRate} consumption={resource.ConsumptionRate}");
                    }
                    return;
                }
                IReadOnlyList<Pair<ResourceDef, ExpantaNum>> costs;
                if (upgrade)
                {
                    var deltas = new List<Pair<ResourceDef, ExpantaNum>>();
                    buildings.GetUpgradeResourceDeltas(DataBase<BuildingDef>.Find(id), ExpantaNum.One, deltas);
                    costs = deltas;
                }
                else
                    costs = command.StartsWith("W:", StringComparison.Ordinal)
                        ? DataBase<WorkshopUpgradeDef>.Find(id).ResourceRequirements
                        : command.StartsWith("B:", StringComparison.Ordinal)
                            ? DataBase<BuildingDef>.Find(id).ResourceRequirements
                            : DataBase<ResearchDef>.Find(id).ResourceRequirements;
                foreach (var cost in costs)
                {
                    if (cost.Second <= ExpantaNum.Zero) continue;
                    var resource = resources.States[cost.First];
                    ExpantaNum required = cost.Second;
                    if (command.StartsWith("B:", StringComparison.Ordinal))
                    {
                        var definition = DataBase<BuildingDef>.Find(id);
                        required = cost.Second.GeometricSeriesCost(definition.CostGrowth,
                            buildings.GetState(definition).Amount, ExpantaNum.One) *
                            BuildingManager.GetConstructionCostMultiplierForEditor(definition);
                    }
                    if (resource.Amount < required)
                        TestContext.WriteLine($"SUPPLY resource={resource.Definition.Id} amount={resource.Amount} required={required} production={resource.ProductionRate} consumption={resource.ConsumptionRate}");
                }
            }
            string Blocker()
            {
                if (commandIndex >= commandCount) return "player";
                if (game.State.FoodNetRate < ExpantaNum.Zero && game.State.FoodAmount <= ExpantaNum.Zero)
                    return "food";
                if (game.State.PowerSatisfaction < ExpantaNum.One) return "power";
                if (game.State.LogisticsSatisfaction < ExpantaNum.One) return "logistics";
                if (buildings.LaborOperatingRatio < ExpantaNum.One) return "productivity";
                if (commands[commandIndex].StartsWith("S:", StringComparison.Ordinal))
                    return game.Sectors.GetState(DataBase<SectorDef>.Find(commands[commandIndex].Substring(2))).ColonizationActive
                        ? "operation" : "materials";
                if (commands[commandIndex].StartsWith("E:", StringComparison.Ordinal))
                {
                    var preview = game.Sectors.GetInteriorExplorationPreview(
                        DataBase<SectorDef>.Find(commands[commandIndex].Substring(2)));
                    return preview.Status == SectorInteriorExplorationStatus.WaitingForSupply ? "materials" : "operation";
                }
                if (commands[commandIndex].StartsWith("C:", StringComparison.Ordinal) ||
                    commands[commandIndex].StartsWith("A:", StringComparison.Ordinal) ||
                    commands[commandIndex].StartsWith("K:", StringComparison.Ordinal) ||
                    commands[commandIndex].StartsWith("L:", StringComparison.Ordinal))
                    return "operation";
                if (researches.ActiveResearch != null) return "research";
                bool upgrade = commands[commandIndex].StartsWith("U:", StringComparison.Ordinal);
                if (upgrade || commands[commandIndex].StartsWith("B:", StringComparison.Ordinal))
                {
                    BuildingDef definition = DataBase<BuildingDef>.Find(commands[commandIndex].Substring(2));
                    BuildFailure failure = upgrade
                        ? buildings.GetUpgradeFailure(definition, ExpantaNum.One)
                        : buildings.GetBuildFailure(definition, ExpantaNum.One);
                    if (failure == BuildFailure.SpaceInsufficient)
                        return "territory";
                }
                if (commands[commandIndex].StartsWith("R:", StringComparison.Ordinal) &&
                    researches.IsResearchCompleted(commands[commandIndex].Substring(2))) return "player";
                return "materials";
            }
            ExpantaNum CommandProgress()
            {
                if (commandIndex >= commandCount) return ExpantaNum.Zero;
                string command = commands[commandIndex];
                string id = command.Substring(2);
                if (command.StartsWith("L:", StringComparison.Ordinal)) return new ExpantaNum(GameSeconds());
                if (command.StartsWith("K:", StringComparison.Ordinal)) return game.UltraProject.State.StageProgress;
                if (command.StartsWith("R:", StringComparison.Ordinal))
                    return researches.ActiveResearch?.Progress ?? researches.GetState(DataBase<ResearchDef>.Find(id)).Progress;
                if (command.StartsWith("E:", StringComparison.Ordinal))
                    return game.Sectors.GetState(DataBase<SectorDef>.Find(id)).InteriorExplorationProgress;
                if (command.StartsWith("S:", StringComparison.Ordinal) || command.StartsWith("C:", StringComparison.Ordinal) ||
                    command.StartsWith("A:", StringComparison.Ordinal))
                    return game.Sectors.GetState(DataBase<SectorDef>.Find(id)).CampaignProgress;
                return ExpantaNum.Zero;
            }
            recordSnapshot = stage =>
            {
                string next = commandIndex < commandCount ? commands[commandIndex] : "done";
                TestContext.WriteLine($"TRACE_STAGE stage={stage} era={game.State.TechLevel} calendarHours={elapsed / 3600d:F4} " +
                    $"gameSeconds={GameSeconds():F3} calendarDays={game.State.CalendarDays} visits={session + 1} " +
                    $"speed={simulation.AccelerationMultiplier} reserveSeconds={saves.RemainingOfflineSeconds:F3} " +
                    $"command={commandIndex}/{commandCount} next={next} commandProgress={CommandProgress()} " +
                    $"hoursWithoutProgress={(elapsed - lastProductiveAt) / 3600d:F3} blocker={Blocker()} source={checkpointInput ?? "new-game"}");
                TestContext.WriteLine($"TRACE_CAPACITY landUsed={game.State.TerritoryUsed} landTotal={game.State.TerritoryTotal} " +
                    $"population={game.State.Population.Population} housing={game.State.Population.PopulationCapacity} " +
                    $"productivityUsed={buildings.UsedProductivity} productivityTotal={buildings.TotalProductivity} laborRatio={buildings.LaborOperatingRatio} " +
                    $"food={game.State.FoodAmount} foodCapacity={game.State.FoodCapacity} foodProduction={game.State.FoodProductionRate} " +
                    $"foodConsumption={game.State.FoodTotalConsumptionRate} foodNet={game.State.FoodNetRate} strategicFood={buildings.StrategicFoodConsumptionRate} " +
                    $"powerProduction={game.State.PowerProductionRate} powerDemand={buildings.TotalPowerDemand} powerSatisfaction={game.State.PowerSatisfaction} " +
                    $"logisticsProduction={game.State.LogisticsProductionRate} logisticsDemand={buildings.TotalLogisticsDemand} logisticsSatisfaction={game.State.LogisticsSatisfaction} " +
                    $"researchPower={researches.EffectiveResearchPower} researchSpeed={researches.CurrentResearchSpeed} activeResearch={researches.ActiveResearch?.Definition.Id ?? "none"}");
                TestContext.WriteLine($"TRACE_RESOURCE_COUNT count={resources.States.Count}");
                if (game.State.TechLevel >= TechLevel.Ultra)
                {
                    UltraProjectPreview project = game.UltraProject.GetPreview();
                    TestContext.WriteLine($"TRACE_PROJECT stage={project.Stage} status={project.Status} progress={project.Progress} " +
                        $"completed={game.UltraProject.State.CompletedStageCount} food={project.FoodPerSecond} " +
                        $"power={project.PowerPerSecond} logistics={project.LogisticsPerSecond} satisfaction={project.SupplySatisfaction}");
                }
                foreach (var resource in resources.States.Values.OrderBy(value => value.Definition.Id, StringComparer.Ordinal))
                {
                    ExpantaNum strategic = StrategicResourceConsumption(resource.Definition);
                    TestContext.WriteLine($"TRACE_RESOURCE id={resource.Definition.Id} amount={resource.Amount} production={resource.ProductionRate} " +
                        $"effectiveProduction={resource.ProductionRate * game.State.HappinessRewardMultiplier} consumption={resource.ConsumptionRate} " +
                        $"strategicConsumption={strategic} dailyNet={resources.GetNetRate(resource)} net={resources.GetNetRate(resource) - strategic} " +
                        $"satisfaction={resources.GetTickSatisfactionForEditor(resource.Definition)}");
                }
                ExpantaNum authoredFootprint = ExpantaNum.Zero;
                foreach (var building in buildings.States.Values.OrderBy(value => value.Definition.Id, StringComparer.Ordinal))
                {
                    if (building.Amount <= ExpantaNum.Zero) continue;
                    ExpantaNum footprint = building.Amount * building.Definition.SpaceCost;
                    authoredFootprint += footprint;
                    TestContext.WriteLine($"TRACE_BUILDING id={building.Definition.Id} amount={building.Amount} efficiency={building.Efficiency} land={footprint}");
                }
                TestContext.WriteLine($"TRACE_LAYOUT authoredFootprint={authoredFootprint} landUsed={game.State.TerritoryUsed}");
                TestContext.WriteLine("TRACE_STORY completed=" + string.Join(",", StoryManager.Chapters.Where(StoryManager.IsCompleted).Select(chapter => chapter.Id)));
                for (int i = 0; i < waitKinds.Length; i++)
                    TestContext.WriteLine($"TRACE_WAIT reason={waitKinds[i]} onlineSeconds={waitSeconds[i]:F3}");
                if (commandIndex < commandCount) RecordShortages();
            };
            TechLevel observedEra = game.State.TechLevel;
            var recordedStages = new HashSet<string>(StringComparer.Ordinal);
            Record(elapsed, checkpoint == null ? "new-game" : "checkpoint-loaded");
            recordSnapshot(checkpoint == null ? "new-game" : "checkpoint-loaded");
            for (int tick = 0; elapsed <= horizon; tick++)
            {
                double seconds = elapsed;
                if (game.State.TechLevel != observedEra)
                {
                    observedEra = game.State.TechLevel;
                    if (observedEra == TechLevel.Industrial) industrialEnteredAt = elapsed;
                    if (observedEra == TechLevel.Spacer) spacerEnteredAt = elapsed;
                    recordSnapshot("era:" + observedEra);
                }
                if (game.State.Population.Population != previousPopulation && elapsed - lastPopulationReport >= 60d)
                {
                    previousPopulation = game.State.Population.Population;
                    lastPopulationReport = elapsed;
                    Record(seconds, "population");
                }
                while (commandIndex < commandCount)
                {
                    string command = commands[commandIndex];
                    string id = command.Substring(2);
                    bool completed;
                    if (command.StartsWith("L:", StringComparison.Ordinal))
                    {
                        if (!requested)
                        {
                            requested = true;
                            layoutStartedAt = GameSeconds();
                            layoutAmountsBefore = resources.States.ToDictionary(pair => pair.Key, pair => pair.Value.Amount);
                            recordSnapshot("layout-start:" + id);
                        }
                        completed = GameSeconds() - layoutStartedAt >= 60d &&
                            game.State.Population.Population >= game.State.Population.PopulationCapacity;
                        Assert.That(GameSeconds() - layoutStartedAt, Is.LessThan(3600d),
                            "The fully housed layout must support population growth within a normal investment interval.");
                        if (completed)
                        {
                            recordSnapshot("layout-ready:" + id);
                            Assert.That(game.UltraProject.State.Status, Is.Not.EqualTo(UltraProjectStatus.Paused),
                                "A paused project must be supplied and resumed before accepting its operating layout.");
                            Assert.That(buildings.LaborOperatingRatio.ToDouble(), Is.GreaterThanOrEqualTo(1d - 1e-6d));
                            Assert.That(game.State.PowerSatisfaction.ToDouble(), Is.GreaterThanOrEqualTo(1d - 1e-6d));
                            Assert.That(game.State.LogisticsSatisfaction.ToDouble(), Is.GreaterThanOrEqualTo(1d - 1e-6d));
                            Assert.That(game.State.FoodProductionRate.ToDouble(),
                                Is.GreaterThanOrEqualTo((game.State.FoodTotalConsumptionRate + buildings.StrategicFoodConsumptionRate).ToDouble() * 1.15d),
                                "Full housing and active strategic operations both need continuing food headroom.");
                            foreach (BuildingState building in buildings.States.Values)
                                if (building.Amount > ExpantaNum.Zero)
                                    Assert.That(building.Efficiency.ToDouble(), Is.GreaterThanOrEqualTo(1d - 1e-6d),
                                        id + ": " + building.Definition.Id + " must operate effectively.");
                            foreach (ResourceState resource in resources.States.Values)
                            {
                                ExpantaNum fullDemand = resource.ConsumptionRate + StrategicResourceConsumption(resource.Definition);
                                if (fullDemand > ExpantaNum.Zero)
                                    Assert.That(resource.ProductionRate.ToDouble(),
                                        Is.GreaterThanOrEqualTo(fullDemand.ToDouble() * 1.15d - 1e-6d),
                                        id + ": " + resource.Definition.Id + " needs reinvestment headroom at full demand.");
                                if (resource.ProductionRate > ExpantaNum.Zero)
                                    Assert.That(resource.Amount, Is.GreaterThan(layoutAmountsBefore[resource.Definition]),
                                        id + ": " + resource.Definition.Id + " inventory must grow after the paid expansion.");
                            }
                            Record(seconds, "layout-ready:" + id);
                        }
                    }
                    else if (command.StartsWith("B:", StringComparison.Ordinal))
                    {
                        var definition = DataBase<BuildingDef>.Find(id);
                        completed = buildings.TryBuild(definition, ExpantaNum.One, out var failure);
                        Assert.That(failure, Is.EqualTo(BuildFailure.None).Or.EqualTo(BuildFailure.ResourceInsufficient)
                            .Or.EqualTo(BuildFailure.SpaceInsufficient),
                            $"Invalid fixed command {command} at {seconds / 3600d:F2}h: {failure}");
                        if (completed) Record(seconds, "built:" + id);
                    }
                    else if (command.StartsWith("D:", StringComparison.Ordinal))
                    {
                        completed = buildings.TryDeconstruct(DataBase<BuildingDef>.Find(id), ExpantaNum.One, out var failure);
                        Assert.That(completed, Is.True, $"Invalid fixed retirement {command}: {failure}");
                        Record(seconds, "retired:" + id);
                    }
                    else if (command.StartsWith("U:", StringComparison.Ordinal))
                    {
                        var definition = DataBase<BuildingDef>.Find(id);
                        completed = buildings.TryUpgrade(definition, ExpantaNum.One, out var failure);
                        Assert.That(failure, Is.EqualTo(BuildFailure.None).Or.EqualTo(BuildFailure.ResourceInsufficient)
                            .Or.EqualTo(BuildFailure.SpaceInsufficient),
                            $"Invalid fixed upgrade {command} at {seconds / 3600d:F2}h: {failure}");
                        if (completed) Record(seconds, "upgraded:" + id);
                    }
                    else if (command.StartsWith("W:", StringComparison.Ordinal))
                    {
                        var definition = DataBase<WorkshopUpgradeDef>.Find(id);
                        completed = workshops.TryPurchase(definition, out var failure);
                        Assert.That(failure, Is.EqualTo(WorkshopPurchaseFailure.None).Or.EqualTo(WorkshopPurchaseFailure.ResourceInsufficient),
                            $"Invalid fixed command {command} at {seconds / 3600d:F2}h: {failure}");
                        if (completed) Record(seconds, "purchased:" + id);
                    }
                    else if (command.StartsWith("P:", StringComparison.Ordinal))
                    {
                        UltraProjectPreview preview = game.UltraProject.GetPreview(UltraProjectDoctrine.Stable);
                        Assert.That(preview.Stage, Is.EqualTo(ProjectStage(id)));
                        completed = game.UltraProject.TryStartStage(UltraProjectDoctrine.Stable, out var failure);
                        Assert.That(failure, Is.EqualTo(UltraProjectOperationFailure.None)
                            .Or.EqualTo(UltraProjectOperationFailure.InsufficientStartupResources)
                            .Or.EqualTo(UltraProjectOperationFailure.InsufficientSupply),
                            $"Invalid fixed project start {command}: {failure}");
                        if (completed)
                        {
                            Record(seconds, "project-started:" + id);
                            recordSnapshot("project-started:" + id);
                        }
                    }
                    else if (command.StartsWith("K:", StringComparison.Ordinal))
                    {
                        completed = game.UltraProject.State.Status == UltraProjectStatus.ReadyToCommit;
                        if (completed)
                        {
                            Assert.That(game.UltraProject.State.CompletedStageCount, Is.EqualTo((int)ProjectStage(id)),
                                "Only the requested discrete stage can be submitted.");
                            Assert.That(game.UltraProject.TryCommitCompletedStage(out var failure), Is.True, failure.ToString());
                            Record(seconds, "project-submitted:" + id);
                            recordSnapshot("project-submitted:" + id);
                        }
                        else if (game.UltraProject.State.Status == UltraProjectStatus.Paused)
                        {
                            Assert.That(game.UltraProject.State.PauseReason, Is.EqualTo(UltraProjectPauseReason.InsufficientSupply));
                            if (game.UltraProject.TryResume(out var failure)) Record(seconds, "project-resumed:" + id);
                            else Assert.That(failure, Is.EqualTo(UltraProjectOperationFailure.InsufficientSupply));
                        }
                        else Assert.That(game.UltraProject.State.Status, Is.EqualTo(UltraProjectStatus.Running));
                    }
                    else if (command.StartsWith("S:", StringComparison.Ordinal))
                    {
                        var sector = DataBase<SectorDef>.Find(id);
                        var state = game.Sectors.GetState(sector);
                        completed = state.Occupied;
                        if (completed) Record(seconds, "occupied:" + id);
                        else if (!state.ColonizationActive)
                        {
                            // This fixed player prepares the material budget before departure; no payment or game gate is added.
                            ExpantaNum duration = sector.ColonizationDurationSeconds /
                                ExpantaNum.Max(ExpantaNum.One, ProgressionModifierManager.Current.ExplorationPowerMultiplier);
                            bool suppliesReady = true;
                            foreach (var cost in sector.ColonizationResourceRatesPerSecond)
                            {
                                ExpantaNum required = cost.Second * duration;
                                var resource = resources.States[cost.First];
                                if (resource.Amount >= required) continue;
                                if (reportedColonizationShortages.Add($"{id}:{cost.First.Id}"))
                                    TestContext.WriteLine($"COLONIZATION-SHORTAGE sector={id} resource={cost.First.Id} amount={resource.Amount} required={required} production={resource.ProductionRate} consumption={resource.ConsumptionRate}");
                                suppliesReady = false;
                            }
                            if (!suppliesReady) break;
                            Assert.That(game.Sectors.TryUnlock(sector, out var unlockFailure), Is.True,
                                $"Invalid fixed colonization {id}: {unlockFailure}");
                            Assert.That(game.Sectors.TryAdvanceColonization(sector, 0d, game.State, resources, out var failure), Is.True,
                                $"Cannot start fixed colonization {id}: {failure}");
                            Record(seconds, "colonization-started:" + id);
                        }
                    }
                    else if (command.StartsWith("C:", StringComparison.Ordinal) || command.StartsWith("A:", StringComparison.Ordinal))
                    {
                        SectorDef sector = DataBase<SectorDef>.Find(id);
                        SectorState state = game.Sectors.GetState(sector);
                        completed = state.Occupied;
                        if (completed) Record(seconds, "distant-occupied:" + id);
                        if (!state.Unlocked)
                        {
                            Assert.That(game.Sectors.TryUnlock(sector, out var unlockFailure), Is.True,
                                $"Invalid fixed distant unlock {id}: {unlockFailure}");
                            Record(seconds, "distant-unlocked:" + id);
                        }
                        if (!completed && !state.CampaignActive)
                        {
                            SectorCampaignPreview preview = game.Sectors.GetCampaignPreview(sector, game.State, resources);
                            TestContext.WriteLine($"DEPARTURE sector={id} effectivePower={preview.EffectivePower} combatRatio={preview.CombatRatio} " +
                                $"progressPerSecond={preview.ProgressPerSecond} supply={preview.HasSupply} food={game.State.FoodAmount}");
                            if (!preview.HasSupply) break;
                            Assert.That(preview.ProgressPerSecond, Is.GreaterThan(ExpantaNum.Zero),
                                "The explicitly built fleet must be able to advance; ownership alone is insufficient.");
                            Assert.That(game.Sectors.TryAdvanceCampaign(sector, 0d, game.State, resources, out var failure),
                                Is.True, $"Cannot start fixed distant campaign {id}: {failure}");
                            Record(seconds, "campaign-started:" + id);
                        }
                        if (command.StartsWith("A:", StringComparison.Ordinal) && state.CampaignActive) completed = true;
                    }
                    else if (command.StartsWith("E:", StringComparison.Ordinal) || command.StartsWith("I:", StringComparison.Ordinal))
                    {
                        var sector = DataBase<SectorDef>.Find(id);
                        var preview = game.Sectors.GetInteriorExplorationPreview(sector);
                        if (preview.Status == SectorInteriorExplorationStatus.Ready || preview.Status == SectorInteriorExplorationStatus.Paused)
                        {
                            Assert.That(game.Sectors.TryStartInteriorExploration(sector, out var failure), Is.True,
                                $"Invalid fixed exploration {id}: {failure}");
                            Record(seconds, "interior-started:" + id);
                            RecordExploration("started:" + id);
                        }
                        completed = preview.Status == SectorInteriorExplorationStatus.Completed;
                        if (completed)
                        {
                            Record(seconds, "interior-completed:" + id);
                            RecordExploration("completed:" + id);
                        }
                        if (command.StartsWith("I:", StringComparison.Ordinal))
                        {
                            Assert.That(game.Sectors.GetState(sector).InteriorExplorationActive || completed, Is.True,
                                "Starting an optional survey must leave a real active or completed exploration.");
                            completed = true;
                        }
                    }
                    else
                    {
                        var definition = DataBase<ResearchDef>.Find(id);
                        var state = researches.GetState(definition);
                        if (!requested) paymentRecorded = state.CostPaid;
                        if (!requested && state.Status != ResearchStatus.Completed)
                        {
                            var result = researches.HandleResearchAction(definition);
                            Assert.That(result, Is.EqualTo(ResearchActionResult.Started).Or.EqualTo(ResearchActionResult.QueuedWaitingResources).Or.EqualTo(ResearchActionResult.Queued));
                            requested = true;
                            Record(seconds, "queued:" + id);
                        }
                        if (state.CostPaid && !paymentRecorded)
                        {
                            paymentRecorded = true;
                            Record(seconds, "paid:" + id);
                        }
                        completed = state.Status == ResearchStatus.Completed;
                        if (completed)
                        {
                            Assert.That(state.CostPaid, Is.True, "Queued prerequisites must also have been paid in full.");
                            Record(seconds, "completed:" + id);
                        }
                    }
                    if (!completed) break;
                    commandIndex++;
                    if (command == "R:Industrialization")
                    {
                        if (industrialEnteredAt < 0d) industrialEnteredAt = elapsed;
                    }
                    if (command == "R:OrbitalEngineering" && spacerEnteredAt < 0d) spacerEnteredAt = elapsed;
                    if (command == checkpointBoundary && command != endpoint) SaveBoundaryCheckpoint();
                    if ((command == "B:MachineFactory" || command == "B:PhantomMaterialsFabricator" ||
                         command == "B:PhaseMaterialSynthesisArray" || command == "S:HeliosCore" ||
                         command == "B:OrbitalHabitatMegastructure" || command == "B:AzureIceSeaArk") &&
                        recordedStages.Add(command)) recordSnapshot(command);
                    requested = false;
                    paymentRecorded = false;
                }
                if (!openingChecked && elapsed >= 600d)
                {
                    openingChecked = true;
                    Assert.That(commandIndex, Is.GreaterThanOrEqualTo(6), "The fixed opening must establish food and stone production without grants.");
                }
                if (elapsed >= nextSample)
                {
                    nextSample = elapsed + 3600d;
                    Assert.That(resources.States.Values.All(state => state.Amount.IsFinite && state.Amount >= ExpantaNum.Zero), Is.True);
                    TestContext.WriteLine($"MAINLINE sample={seconds:F1} next={(commandIndex < commandCount ? commands[commandIndex] : "done")} " +
                        $"food={game.State.FoodAmount} happiness={game.State.HappinessMultiplier}");
                }
                if (commandIndex >= commandCount) break;
                ExpantaNum commandProgress = CommandProgress();
                if (commandIndex != progressCommandIndex || commandProgress > previousCommandProgress)
                    lastProductiveAt = elapsed;
                progressCommandIndex = commandIndex;
                previousCommandProgress = commandProgress;
                if (elapsed - lastProductiveAt > 7d * 86400d)
                {
                    RecordShortages();
                    for (int i = 0; i < waitKinds.Length; i++)
                        TestContext.WriteLine($"WAIT sampledOnline={waitKinds[i]} seconds={waitSeconds[i]:F1}");
                    Assert.Fail($"DIAGNOSTIC_STOP after seven days without a command result, payment, research progress or operation progress; " +
                        $"next={commands[commandIndex]}, hours={elapsed / 3600d:F1}. Inspect the supply evidence; this stop is not a pacing verdict.");
                }
                if (visits && elapsed >= sessionEnd - 0.00001d)
                {
                    string before = Blocker();
                    session++;
                    double nextVisit = (session / 3) * 86400d + visitHours[session % 3] * 3600d;
                    double offline = nextVisit - elapsed;
                    Assert.That(offline, Is.GreaterThan(0d));
                    ResumeAt(nextVisit);
                    elapsed = nextVisit;
                    sessionEnd = nextVisit + 900d;
                    TestContext.WriteLine($"VISIT session={session} t={elapsed:F1} offline={offline:F1} reserveSeconds={saves.RemainingOfflineSeconds:F3} " +
                        $"speed={simulation.AccelerationMultiplier} gameSeconds={GameSeconds():F3} blockerBefore={before} blockerAfter={Blocker()} next={commands[commandIndex]}");
                    Debug.Log($"Mainline visit={session} hours={elapsed / 3600d:F1} command={commandIndex}/{commandCount} next={commands[commandIndex]}");
                    RecordShortages();
                    RecordExploration("visit:" + session);
                    if (elapsed - lastMilestone > 3d * 86400d)
                        TestContext.WriteLine($"PACING-GAP hoursWithoutAction={(elapsed - lastMilestone) / 3600d:F1} next={commands[commandIndex]}");
                    // Closed intervals only bank time; waits and production advance during the actual visit.
                }
                else
                {
                    string blocker = Blocker();
                    double frameSeconds = visits ? Math.Min(step, sessionEnd - elapsed) : step;
                    waitSeconds[Array.IndexOf(waitKinds, blocker)] += frameSeconds;
                    TickOnline(frameSeconds);
                    elapsed += frameSeconds;
                }
            }
            longestMilestoneGap = Math.Max(longestMilestoneGap, elapsed - lastMilestone);
            for (int i = 0; i < waitKinds.Length; i++)
                TestContext.WriteLine($"WAIT sampledOnline={waitKinds[i]} seconds={waitSeconds[i]:F1}");
            TestContext.WriteLine($"SCHEDULE visits={visits} acceleration={acceleration} elapsedHours={elapsed / 3600d:F4} gameHours={GameSeconds() / 3600d:F4} " +
                $"sessions={session + 1} target={endpoint} hoursSinceIndustrial={(industrialEnteredAt >= 0d ? (elapsed - industrialEnteredAt) / 3600d : 0d):F4} " +
                $"hoursSinceSpacer={(spacerEnteredAt >= 0d ? (elapsed - spacerEnteredAt) / 3600d : 0d):F4}");
            TestContext.WriteLine($"MAINLINE finishedCommands={commandIndex}/{commandCount} longestObservedActionGap={longestMilestoneGap:F1}s");
            TechLevel expectedEra = commandCount <= Array.IndexOf(commands, "R:OrbitalEngineering")
                ? TechLevel.Industrial : TechLevel.Spacer;
            int ultraBoundary = Array.IndexOf(commands, "R:TechnologicalSingularity");
            if (ultraBoundary >= 0 && commandCount > ultraBoundary) expectedEra = TechLevel.Ultra;
            Assert.That(game.State.TechLevel, Is.EqualTo(expectedEra), "Fixed mainline must reach the target through paid commands.");
            Assert.That(commandIndex, Is.EqualTo(commandCount), "Every fixed command must complete within the diagnostic horizon.");
            // The user explicitly widened pacing to 33%-300%; payment and supply assertions stay strict.
            if (visits && industrial)
            {
                TestContext.WriteLine($"PACING_TARGET stage=new-game-to-industrial nominalDays=1-3 normalDays=0.33-9 actualDays={elapsed / 86400d:F4}");
                Assert.That(elapsed, Is.LessThanOrEqualTo(9d * 86400d),
                    "The 1-3 day reference allows up to nine days under the user-approved 300% tolerance.");
            }
            if (visits && commandCount > Array.IndexOf(commands, "R:OrbitalEngineering"))
            {
                double industrialDays = (spacerEnteredAt - industrialEnteredAt) / 86400d;
                TestContext.WriteLine($"PACING_TARGET stage=industrial-to-spacer nominalDays=2-4 normalDays=0.66-12 actualDays={industrialDays:F4}");
                Assert.That(industrialEnteredAt, Is.GreaterThanOrEqualTo(0d));
                Assert.That(industrialDays, Is.InRange(0.66d, 12d + 900d / 86400d),
                    "The approximate 33%-300% window allows one 15-minute visit at its calendar boundary.");
            }
            if (endpoint == "B:PhantomMaterialsFabricator" || endpoint == "B:PhaseMaterialSynthesisArray" ||
                endpoint == "W:ReusableLaunchStages" || endpoint == "B:OrbitalHabitatMegastructure")
            {
                bool phase = endpoint != "B:PhantomMaterialsFabricator";
                string plantId = phase ? "PhaseMaterialSynthesisArray" : "PhantomMaterialsFabricator";
                var product = DataBase<ResourceDef>.Find(phase ? "PhaseMaterial" : "PhantomAlloy");
                var weave = DataBase<ResourceDef>.Find("PhantomWeave");
                ExpantaNum productBefore = resources.GetAmount(product);
                ExpantaNum weaveBefore = resources.GetAmount(weave);
                for (int i = 0; i < Math.Max(1, (int)Math.Ceiling(10d / step)); i++) TickOnline(step);
                Assert.That(resources.GetAmount(product), Is.GreaterThan(productBefore));
                Assert.That(resources.GetAmount(weave), Is.GreaterThan(weaveBefore));
                Assert.That(game.Sectors.GetState(DataBase<SectorDef>.Find("AlphaCentauri")).Occupied, Is.False);
                Assert.That(game.Sectors.GetState(DataBase<SectorDef>.Find("HeliosCore")).Occupied,
                    Is.EqualTo(commands.Take(commandCount).Contains("S:HeliosCore")),
                    "Sector ownership follows the paid route prefix, including earlier phase-material construction.");
                foreach (var resource in resources.States.Values.OrderBy(state => state.Definition.Id, StringComparer.Ordinal))
                {
                    TestContext.WriteLine($"INDUSTRY resource={resource.Definition.Id} amount={resource.Amount} production={resource.ProductionRate} consumption={resource.ConsumptionRate}");
                }
                TestContext.WriteLine($"INDUSTRY building={plantId} efficiency={buildings.GetState(DataBase<BuildingDef>.Find(plantId)).Efficiency} " +
                    $"power={game.State.PowerSatisfaction} logistics={game.State.LogisticsSatisfaction} foodNet={game.State.FoodNetRate}");
                if (phase)
                {
                    Assert.That(buildings.GetState(DataBase<BuildingDef>.Find(plantId)).Efficiency,
                        Is.GreaterThanOrEqualTo(ExpantaNum.One));
                    Assert.That(game.State.PowerSatisfaction, Is.GreaterThanOrEqualTo(ExpantaNum.One));
                    Assert.That(game.State.LogisticsSatisfaction, Is.GreaterThanOrEqualTo(ExpantaNum.One));
                    Assert.That(game.State.FoodNetRate, Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
                    foreach (var resource in resources.States.Values)
                    {
                        Assert.That(resource.ProductionRate, Is.GreaterThanOrEqualTo(resource.ConsumptionRate),
                            $"This fixed phase plant must not depend on accumulated {resource.Definition.Id}.");
                    }
                }
            }
            if (endpoint == "W:ReusableLaunchStages")
                Assert.That(workshops.IsPurchased(DataBase<WorkshopUpgradeDef>.Find("ReusableLaunchStages")), Is.True);
            if (endpoint == "B:OrbitalHabitatMegastructure")
            {
                Assert.That(researches.IsResearchCompleted("OrbitalConstructionAutomation"), Is.True);
                Assert.That(workshops.IsPurchased(DataBase<WorkshopUpgradeDef>.Find("ReusableLaunchStages")), Is.True);
                Assert.That(workshops.IsPurchased(DataBase<WorkshopUpgradeDef>.Find("ModularHabitatSystems")), Is.True);
                BuildingState habitat = buildings.GetState(DataBase<BuildingDef>.Find("OrbitalHabitatMegastructure"));
                Assert.That(habitat.Amount, Is.GreaterThanOrEqualTo(ExpantaNum.One));
                Assert.That(habitat.Efficiency, Is.GreaterThanOrEqualTo(ExpantaNum.One));
                Assert.That(game.State.PowerSatisfaction, Is.GreaterThanOrEqualTo(ExpantaNum.One));
                Assert.That(game.State.LogisticsSatisfaction, Is.GreaterThanOrEqualTo(ExpantaNum.One));
                Assert.That(game.State.FoodNetRate, Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
                foreach (var resource in resources.States.Values)
                {
                    Assert.That(resource.ProductionRate, Is.GreaterThanOrEqualTo(resource.ConsumptionRate),
                        $"The orbital habitat supply chain must not depend on accumulated {resource.Definition.Id}.");
                }
            }
            if (endpoint == "S:HeliosCore")
            {
                foreach (string sectorId in new[] { "DawnRing", "AzurePool", "Terminus", "ShardCrown", "ThunderGate", "HeliosCore" })
                    Assert.That(game.Sectors.GetState(DataBase<SectorDef>.Find(sectorId)).Occupied, Is.True,
                        $"The Sol mainline must occupy {sectorId} before the first interstellar expedition.");
                Assert.That(game.Sectors.GetState(DataBase<SectorDef>.Find("AlphaCentauri")).Occupied, Is.False,
                    "Completing Sol must not silently launch an interstellar expedition.");
            }
            if (endpoint == "C:AlphaCentauri")
            {
                SectorState frontier = game.Sectors.GetState(DataBase<SectorDef>.Find("AlphaCentauri"));
                Assert.That(frontier.Occupied, Is.True, "The first distant outpost requires an actual completed campaign and established outpost.");
                Assert.That(frontier.VisitCount, Is.EqualTo(1), "Occupation count is a discrete permanent record.");
                Assert.That(game.State.Campaign.Active, Is.False, "Completed departure must stop ongoing campaign supply.");
                TestContext.WriteLine($"FRONTIER occupied={frontier.Occupied} visits={frontier.VisitCount} casualties={frontier.CampaignCasualties} " +
                    $"fleet={game.State.FleetPower} attack={game.State.AttackPower} militaryManpower={game.State.MilitaryManpower}");
            }
            if (endpoint == "L:planet-projects-complete")
            {
                foreach (string sectorId in new[] { "AzurePool", "ThunderGate", "HeliosCore" })
                    Assert.That(game.Sectors.GetState(DataBase<SectorDef>.Find(sectorId)).InteriorExplorationCompleted,
                        Is.True, $"Planet project route must finish {sectorId} exploration before its unique building.");
                Assert.That(buildings.GetState(DataBase<BuildingDef>.Find("ThunderGatePropellantWorks")).Amount,
                    Is.GreaterThanOrEqualTo(ExpantaNum.One));
                Assert.That(buildings.GetState(DataBase<BuildingDef>.Find("DawnDysonSphere")).Amount,
                    Is.GreaterThanOrEqualTo(ExpantaNum.One));
                Assert.That(buildings.GetState(DataBase<BuildingDef>.Find("AzureIceSeaArk")).Amount,
                    Is.GreaterThanOrEqualTo(ExpantaNum.One));
                foreach (string upgradeId in new[]
                {
                    "ThunderCryogenicSeparation", "ThunderParallelRefining", "ThunderOrbitalLoadingRing",
                    "DysonCollectorFleet", "DysonStellarBelt", "DysonControlNetwork"
                })
                    Assert.That(workshops.IsPurchased(DataBase<WorkshopUpgradeDef>.Find(upgradeId)), Is.True,
                        $"Planet project route must purchase {upgradeId} as a paid stage.");
                foreach (string buildingId in new[] { "DawnOrbitalObservatory", "EarthMoonLogisticsHub", "TerminusMaterialsWorks",
                    "ShardCrownOreSorter", "HeliosPowerArray", "ThunderGatePropellantWorks", "DawnDysonSphere", "AzureIceSeaArk" })
                {
                    BuildingState facility = buildings.GetState(DataBase<BuildingDef>.Find(buildingId));
                    Assert.That(facility.Amount, Is.GreaterThanOrEqualTo(ExpantaNum.One));
                    Assert.That(facility.Definition.SpaceCost.ToDouble(), Is.EqualTo(0d).Within(1e-7),
                        "All eight sector facilities retain zero local territory cost.");
                }
                ResourceState electronics = resources.GetState(DataBase<ResourceDef>.Find("Electronics"));
                Assert.That(electronics.ProductionRate, Is.GreaterThanOrEqualTo(electronics.ConsumptionRate),
                    "The expanded electronics industry must cover operating projects after their stage payments.");
            }
            if (endpoint == "L:project-complete")
            {
                Assert.That(game.UltraProject.State.IsComplete, Is.True);
                Assert.That(game.UltraProject.State.CompletedStageCount, Is.EqualTo(UltraProjectState.StageCount));
                Assert.That(game.UltraProject.IsCampaignDoctrineUnlocked, Is.True);
            }
            if (endpoint == "L:ultra-mainline-complete")
            {
                Assert.That(game.UltraProject.State.IsComplete, Is.True);
                Assert.That(game.UltraProject.State.CompletedStageCount, Is.EqualTo(UltraProjectState.StageCount));
                Assert.That(game.UltraProject.IsCampaignDoctrineUnlocked, Is.True);
                foreach (UltraProjectStage stage in new[] { UltraProjectStage.Prototype, UltraProjectStage.Stabilization, UltraProjectStage.Expansion })
                    Assert.That(investedProjectStages, Does.Contain(stage), "Each running stage must allow a paid research or supply investment.");
                foreach (string sectorId in new[] { "AlphaCentauri", "ProximaB", "TauCetiFoundry", "SiriusResourceBelt" })
                    Assert.That(game.Sectors.GetState(DataBase<SectorDef>.Find(sectorId)).Occupied, Is.True, sectorId);
                Assert.That(game.State.Campaign.Active, Is.False);
                StoryManager.RefreshProgress();
                foreach (StoryChapter chapter in StoryManager.Chapters)
                    Assert.That(StoryManager.IsCompleted(chapter), Is.True, chapter.Id);
                foreach (string kind in new[] { "colonization", "campaign", "project" })
                    Assert.That(auditedFoodOperations, Does.Contain(kind), "The complete late route must audit each extra Food payment.");
                double spacerDays = (elapsed - spacerEnteredAt) / 86400d;
                TestContext.WriteLine($"PACING_TARGET stage=spacer-to-ultra-mainline nominalDays=14-28 normalDays=4.62-84 actualDays={spacerDays:F4}");
                Assert.That(spacerDays, Is.InRange(4.62d, 84d));
            }
            // Endpoints become reusable evidence only after every acceptance assertion passes.
            SaveBoundaryCheckpoint(endpoint != checkpointBoundary);
        }
        finally
        {
            try
            {
                recordSnapshot?.Invoke("final");
            }
            finally
            {
                TutorialManager loadedTutorial = TutorialManager.Current;
                if (loadedTutorial != null && loadedTutorial != initialTutorial)
                    UnityEngine.Object.DestroyImmediate(loadedTutorial.gameObject);
                for (int i = objects.Count - 1; i >= 0; i--)
                    if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
                ProgressionModifierManager.Rebuild(null);
                SaveManager.ClearSaveRootOverrideForTests();
                if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
                Assert.That(Directory.Exists(saveRoot), Is.False, "The isolated pacing save must be removed after the run.");
            }
        }
    }

}
