using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NUnit.Framework;

public sealed class EarlyVerticalSlicePacingTests
{
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
    public void NewGame_OrbitalThreeDailyVisitsTrace() => RunMainlineTrace(true, "R:OrbitalEngineering");

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
    public void NewGame_PlanetProjectsThreeDailyVisitsTrace() => RunMainlineTrace(true, "B:AzureIceSeaArk");

    [Test, Category("PacingDiagnostic")]
    public void NewGame_FirstInterstellarThreeDailyVisitsTrace() => RunMainlineTrace(true, "C:AlphaCentauri");

    private static readonly string[] MainlineCommands =
    {
        // One hand-authored route: investments are explicit player commands, never strategy selection.
        "B:WoodHouse", "R:Agriculture", "B:Farm", "R:ControlledFire", "R:Quarry", "B:Quarry",
        "B:WoodHouse", "R:KnowledgeSharing", "B:KnowledgeCircle", "B:KnowledgeCircle", "B:KnowledgeCircle", "R:ClayExtraction", "B:ClayPit",
        "B:WoodHouse", "B:Farm", "R:StoneProcessing", "B:StoneCuttingWorkshop", "R:Woodworking", "B:WoodHouse", "B:Lumberyard",
        "R:Mathematics", "R:Calendar", "B:FiberGatheringCamp", "R:AnimalHusbandry", "R:StoneAgeSettlement",
        "B:WoodHouse", "B:Farm", "B:FiberGatheringCamp", "B:FiberGatheringCamp", "R:Mining",
        "B:MetalMine", "B:MetalMine", "B:CoalMine", "B:CoalMine", "R:CharcoalMaking", "R:Measurement",
        "R:MultiMetalSmeltingFundamentals", "B:MetalSmelter", "R:BronzeAlloyCrafting", "R:IronworkingAndSteelmaking",
        "R:TextileCraft", "B:WeavingWorkshop", "R:CeramicFiring", "B:CeramicKiln", "R:WrittenRecords",
        "B:ScribeHut", "B:ScribeHut", "B:ScribeHut",
        "R:FeudalAdministration", "B:WoodHouse", "B:Farm", "R:Steelmaking", "B:SteelForge", "B:SteelForge", "B:SteelForge",
        "R:MechanicalEngineering", "R:Industrialization",
        // Industrial extension of the same route. Workshops are paid player commands too.
        "B:ScribeHut", "B:ScribeHut", "B:ScribeHut",
        "R:TradeRoutes", "B:Caravanserai", "B:Caravanserai", "B:Caravanserai",
        "B:Farm", "B:Farm", "B:Farm", "B:Farm", "B:Farm", "R:UrbanHousing", "B:TownHouse",
        // Expand the existing feedstock chain before powered factories consume its surplus.
        "B:CoalMine", "B:CoalMine", "B:CoalMine", "B:CoalMine", "B:CoalMine",
        "B:CoalMine", "B:CoalMine", "B:CoalMine",
        "B:CoalMine", "B:CoalMine", "B:CoalMine",
        "B:CoalMine", "B:CoalMine", "B:CoalMine", "B:CoalMine", "B:CoalMine",
        "B:MetalMine", "B:MetalMine", "B:MetalMine", "B:MetalMine", "B:MetalMine",
        "B:MetalSmelter", "B:MetalSmelter", "B:MetalSmelter", "B:MetalSmelter",
        "B:SteelForge", "B:SteelForge", "B:SteelForge",
        "B:SteelForge", "B:SteelForge", "B:SteelForge",
        "B:Quarry", "B:Quarry", "B:Quarry",
        "B:StoneCuttingWorkshop", "B:StoneCuttingWorkshop", "B:StoneCuttingWorkshop", "B:StoneCuttingWorkshop",
        "R:Bookmaking", "B:Library", "B:Library",
        "R:IndustrialWorkshop", "R:Standardization", "R:ElectricalEngineering", "B:WireMill",
        "R:SteamPower", "B:SteamPlant", "R:Coking", "B:CokeOven", "B:CokeOven", "B:CokeOven",
        "R:PowerGridEngineering", "B:CentralPowerStation", "R:PetroleumExtraction",
        "R:IndustrialChemistry", "B:OilDerrick", "B:ChemicalPlant",
        "R:PrecisionManufacturing", "W:DraftingTables", "W:PrecisionTooling", "B:TownHouse", "B:MachineFactory",
        "R:ConcreteEngineering", "W:ReinforcedBoilers",
        // Use the current population cap to raise steel output before the next major steel payment.
        "B:TownHouse", "B:CoalMine", "B:SteelForge", "W:RotaryKilns",
        "R:IrrigationEngineering", "B:IrrigationWorks", "B:IrrigationWorks",
        "B:TownHouse", "B:TownHouse", "B:BuildingMaterialsComplex",
        "R:ScientificMethod", "R:ModernUniversity", "B:TownHouse", "B:University", "R:ScientificInstrumentation",
        "W:LaboratoryGlassware", "W:ElectricalInstrumentation", "W:PressurizedReactors", "W:ContinuousDistillation",
        "B:TownHouse", "B:TownHouse", "B:TownHouse",
        "B:IrrigationWorks", "B:IrrigationWorks", "B:IrrigationWorks", "B:IrrigationWorks",
        "B:IrrigationWorks", "B:IrrigationWorks", "B:IrrigationWorks", "B:IrrigationWorks",
        "B:IrrigationWorks", "B:IrrigationWorks", "B:IrrigationWorks", "B:IrrigationWorks",
        "B:IrrigationWorks", "B:IrrigationWorks", "B:IrrigationWorks", "B:IrrigationWorks",
        "R:IndustrialHabitationEngineering", "B:IndustrialHabitationComplex",
        "B:OilDerrick", "B:OilDerrick", "B:IntegratedPetrochemicalComplex",
        "R:RailwayEngineering", "R:ElectricalCommunication", "R:IndustrialExplosives", "B:RailHub",
        "R:FactoryOrganization", "R:MassProduction", "R:CombustionEngines", "R:IndustrialAgriculture",
        "W:FuelInjection", "W:AgriculturalMachinery", "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex",
        "B:PlantingField", "B:PlantingField", "B:PlantingField", "B:PlantingField",
        "R:LogisticsManagement", "R:IntegratedMetalResourceDevelopment",
        "W:PoweredMining", "W:ConveyorSystems", "W:MetalOreSeparationSystem", "B:IntegratedMetalMine", "R:AdvancedMetalMetallurgy", "B:AdvancedMetalSmelter",
        "R:AdvancedMetalHydrometallurgy", "B:AdvancedMetalRefinery", "R:ChlorideTitaniumMetallurgy",
        "R:TitaniumAlloyEngineering", "B:TitaniumAlloySmelter", "R:OrbitalEngineering",
        // Only extend to the first local foothold after the orbital prefix has closed.
        "B:University", "B:University", "B:LaunchCenter", "R:HomeSystemSurvey", "S:DawnRing",
        // Prepare the first planetary operation using the existing industrial supply chain.
        "W:InsulatedWindings", "R:DeepOilDrilling", "B:IndustrialHabitationComplex",
        "W:CokeOvenOptimization", "B:CentralPowerStation", "B:RailHub", "B:WireMill",
        "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex",
        "B:OilDerrick", "B:OilDerrick", "B:OilDerrick", "B:OilDerrick", "B:IndustrialCarbonizationRetort",
        "B:ChemicalPlant", "B:ChemicalPlant", "B:ChemicalPlant", "B:ChemicalPlant", "B:ChemicalPlant",
        // Buy affordable relief for the current bottleneck before longer-term research.
        "B:WireMill", "B:WireMill",
        // Invest in the next capabilities while the departure materials accumulate.
        "R:MilitaryIndustry", "W:LightAlloyFrames", "W:BlockSignalling",
        "B:University", "B:University", "R:OrbitalPowerTransmission",
        "R:OrbitalLogisticsInfrastructure", "R:OrbitalPropellantEngineering",
        "S:AzurePool",
        // Develop the new space before committing to the next planet's larger supply bill.
        "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex",
        "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex",
        "B:EarthMoonLogisticsHub", "B:CentralPowerStation", "B:TitaniumAlloySmelter",
        "B:University", "B:University",
        "R:OrbitalStationEngineering", "R:DeepSpaceShipbuilding", "R:DeepSpaceFleet",
        "R:InterstellarNavigation", "R:PhantomMaterials", "B:PhantomMaterialsFabricator",
        // Invest in orbital construction once its material and industrial prerequisites are available.
        "R:OrbitalConstructionAutomation",
        // Support the new material chain at its sources, rather than consuming accumulated ore and timber.
        "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex",
        "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex",
        "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex",
        "R:MechanizedForestry", "W:MechanizedForestryEquipment", "B:MechanizedLumberyard",
        "B:IndustrialCarbonizationRetort",
        "B:IntegratedMetalMine", "B:IntegratedMetalMine", "B:IntegratedMetalMine", "B:IntegratedMetalMine",
        "B:CentralPowerStation", "B:CentralPowerStation",
        "B:TitaniumAlloySmelter", "B:TitaniumAlloySmelter", "B:TitaniumAlloySmelter",
        "B:MetalSmelter", "B:MachineFactory", "B:PhantomMaterialsFabricator", "B:PhantomMaterialsFabricator",
        // Finish Sol's remaining barren regions before consuming materials on phase/launch milestones.
        "S:Terminus", "S:ShardCrown", "S:ThunderGate", "S:HeliosCore",
        "R:PhaseMaterialEngineering", "B:PhaseMaterialSynthesisArray",
        // The launch workshop needs rubber and phantom materials; expand their full input chains first.
        "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex",
        "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex",
        "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex",
        "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex",
        "B:CoalMine",
        "B:OilDerrick", "B:OilDerrick", "B:OilDerrick", "B:OilDerrick", "B:OilDerrick", "B:OilDerrick", "B:OilDerrick",
        "B:OilDerrick", "B:OilDerrick", "B:OilDerrick", "B:OilDerrick", "B:OilDerrick", "B:OilDerrick", "B:OilDerrick",
        "B:ClayPit", "B:CeramicKiln",
        "B:CentralPowerStation", "B:CentralPowerStation",
        "B:OrbitalWireWorks",
        "B:IntegratedMetalMine", "B:IntegratedMetalMine",
        "B:TitaniumAlloySmelter", "B:TitaniumAlloySmelter", "B:TitaniumAlloySmelter", "B:MachineFactory",
        "B:IntegratedPetrochemicalComplex", "B:IntegratedPetrochemicalComplex", "B:IntegratedPetrochemicalComplex",
        "B:IntegratedPetrochemicalComplex", "B:IntegratedPetrochemicalComplex",
        // Open construction capacity before buying additional heavy industrial support.
        "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex",
        "B:PhantomMaterialsFabricator", "B:PhantomMaterialsFabricator",
        // Rubber is the launch-stage blocker; grow the oil-to-rubber chain before it becomes a long wait.
        // The measured habitat endpoint still consumed slightly more crude than this route supplied.
        "B:OilDerrick", "B:OilDerrick", "B:IntegratedPetrochemicalComplex",
        "R:DeepSpaceSurvey", "R:GravityAssistTrajectoryTheory", "W:ReusableLaunchStages",
        // Expand the current population and logistics base before committing to a habitat megastructure.
        "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex", "B:IndustrialHabitationComplex",
        // Cover the measured titanium-alloy deficit with its actual ore source and one more alloy smelter.
        "B:IntegratedMetalMine", "B:TitaniumAlloySmelter",
        "W:ModularHabitatSystems", "B:OrbitalHabitatMegastructure",
        // Keep the historic S:HeliosCore endpoint as occupation-only; planetary projects extend this same route after it.
        "E:ThunderGate", "B:ThunderGatePropellantWorks",
        "W:ThunderCryogenicSeparation", "W:ThunderParallelRefining", "W:ThunderOrbitalLoadingRing",
        "E:HeliosCore", "B:DawnDysonSphere", "W:DysonCollectorFleet", "W:DysonStellarBelt",
        // The real visit trace exhausted electronics on project upkeep; use the new power to expand its producer.
        "B:OrbitalWireWorks",
        // PhaseMaterialEngineering was completed earlier in the same route; it gates only this final Dyson stage.
        "W:DysonControlNetwork",
        // The Ark requires its full research chain and the already completed local ice-ocean survey.
        "E:AzurePool", "R:BioregenerativeLifeSupport", "R:OrbitalAgroecology", "B:AzureIceSeaArk",
        // Invest in actual fleet/manpower before the first paid, continuously supplied interstellar campaign.
        "W:AutomatedShipyardAssembly", "B:Shipyard", "B:Shipyard", "B:Shipyard",
        "B:Shipyard", "B:Shipyard", "B:Shipyard", "C:AlphaCentauri"
    };


    private static void RunMainlineTrace(bool visits, string endpoint)
    {
        if (Environment.GetEnvironmentVariable("KINGDOM_RUN_PACING_TRACE") != "1")
            Assert.Ignore("Set KINGDOM_RUN_PACING_TRACE=1 to run the fixed-route diagnostic.");
        Assert.That(UnityEngine.Object.FindObjectOfType<GameManager>(), Is.Null);
        Assert.That(UnityEngine.Object.FindObjectOfType<ResourceManager>(), Is.Null);
        Assert.That(UnityEngine.Object.FindObjectOfType<BuildingManager>(), Is.Null);
        Assert.That(UnityEngine.Object.FindObjectOfType<ResearchManager>(), Is.Null);
        Assert.That(UnityEngine.Object.FindObjectOfType<WorkshopManager>(), Is.Null);
        Assert.That(UnityEngine.Object.FindObjectOfType<SimulationManager>(), Is.Null);
        var objects = new List<GameObject>();
        T Create<T>() where T : Component
        {
            var host = new GameObject("MainlineTrace-" + typeof(T).Name);
            objects.Add(host);
            return host.AddComponent<T>();
        }
        try
        {
            var game = Create<GameManager>();
            var resources = Create<ResourceManager>();
            resources.InitializeForEditor();
            var buildings = Create<BuildingManager>();
            var researches = Create<ProgressionMilestoneTestResearchManager>();
            researches.EnsureInitialized();
            var workshops = Create<WorkshopManager>();
            workshops.ResetForLoadForEditor();
            var simulation = Create<SimulationManager>();
            game.InitializeNewGameForEditor();
            game.Sectors.InitializeDefinitions();
            simulation.ManualTick(0d);

            string[] commands = MainlineCommands;
            int commandCount = Array.IndexOf(commands, endpoint) + 1;
            Assert.That(commandCount, Is.GreaterThan(0), "The endpoint must belong to the one command table.");
            bool industrial = endpoint == "R:Industrialization";
            int commandIndex = 0;
            bool requested = false;
            bool paymentRecorded = false;
            ExpantaNum previousPopulation = game.State.Population.Population;
            double lastMilestone = 0d;
            double longestMilestoneGap = 0d;
            var explorationDefinitions = new[]
            {
                DataBase<SectorDef>.Find("AzurePool"),
                DataBase<SectorDef>.Find("ThunderGate"),
                DataBase<SectorDef>.Find("HeliosCore")
            };
            var explorationPaid = new Dictionary<string, Dictionary<ResourceDef, ExpantaNum>>(StringComparer.Ordinal);
            foreach (var sector in explorationDefinitions)
                explorationPaid[sector.Id] = new Dictionary<ResourceDef, ExpantaNum>();
            void TickOnline(double seconds)
            {
                var before = explorationDefinitions.ToDictionary(
                    sector => sector.Id,
                    sector => game.Sectors.GetState(sector).InteriorExplorationProgress,
                    StringComparer.Ordinal);
                simulation.ManualTick(seconds);
                foreach (var sector in explorationDefinitions)
                {
                    var project = sector.InteriorExploration;
                    ExpantaNum delta = game.Sectors.GetState(sector).InteriorExplorationProgress - before[sector.Id];
                    if (delta <= ExpantaNum.Zero) continue;
                    foreach (var rate in project.ResourceRatesPerSecond)
                    {
                        var paid = explorationPaid[sector.Id];
                        paid[rate.First] = (paid.TryGetValue(rate.First, out ExpantaNum previous) ? previous : ExpantaNum.Zero) +
                            rate.Second * project.DurationSeconds * delta;
                    }
                }
            }
            void AdvanceOffline(double seconds)
            {
                var before = explorationDefinitions.ToDictionary(
                    sector => sector.Id,
                    sector => game.Sectors.GetState(sector).InteriorExplorationProgress,
                    StringComparer.Ordinal);
                simulation.AdvanceOffline(seconds);
                foreach (var sector in explorationDefinitions)
                {
                    var project = sector.InteriorExploration;
                    ExpantaNum delta = game.Sectors.GetState(sector).InteriorExplorationProgress - before[sector.Id];
                    if (delta <= ExpantaNum.Zero) continue;
                    foreach (var rate in project.ResourceRatesPerSecond)
                    {
                        var paid = explorationPaid[sector.Id];
                        paid[rate.First] = (paid.TryGetValue(rate.First, out ExpantaNum previous) ? previous : ExpantaNum.Zero) +
                            rate.Second * project.DurationSeconds * delta;
                    }
                }
            }
            void RecordExploration(string action)
            {
                foreach (var sector in explorationDefinitions)
                {
                    var preview = game.Sectors.GetInteriorExplorationPreview(sector);
                    TestContext.WriteLine($"INTERIOR action={action} sector={sector.Id} status={preview.Status} progress={preview.Progress} remainingSeconds={preview.EstimatedSecondsRemaining} " +
                        $"missing={(preview.MissingResource != null ? preview.MissingResource.Id : "none")} paid={string.Join(",", explorationPaid[sector.Id].OrderBy(pair => pair.Key.Id, StringComparer.Ordinal).Select(pair => $"{pair.Key.Id}:{pair.Value}"))}");
                }
            }
            void Record(double seconds, string action)
            {
                if (action != "population")
                {
                    longestMilestoneGap = Math.Max(longestMilestoneGap, seconds - lastMilestone);
                    lastMilestone = seconds;
                }
                TestContext.WriteLine($"MAINLINE t={seconds:F1} action={action} wood={resources.GetAmount(DataBase<ResourceDef>.Find("WoodLog"))} " +
                    $"population={game.State.Population.Population}/{game.State.Population.PopulationCapacity} " +
                    $"foodNet={game.State.FoodNetRate} productivity={buildings.AvailableProductivity} researchSpeed={researches.CurrentResearchSpeed}");
            }
            const double step = 100d; // Coarse fixed-route diagnostic tick; offline still uses AdvanceOffline.
            double horizon = (industrial ? visits ? 28d : 3d : 56d) * 86400d;
            double elapsed = 0d;
            double spacerEnteredAt = -1d;
            double sessionEnd = 1800d;
            int session = 0;
            int[] visitHours = { 0, 5, 13 }; // Relative to the first visit at 08:00.
            var waitSeconds = new double[4];
            var reportedColonizationShortages = new HashSet<string>();
            string[] waitKinds = { "materials", "research", "supply", "player" };
            void RecordShortages()
            {
                string command = commands[commandIndex];
                string id = command.Substring(2);
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
                        var state = buildings.GetState(DataBase<BuildingDef>.Find(buildingId));
                        TestContext.WriteLine($"FEEDSTOCK building={buildingId} amount={state.Amount} efficiency={state.Efficiency}");
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
                if (command.StartsWith("C:", StringComparison.Ordinal))
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
                IReadOnlyList<Pair<ResourceDef, ExpantaNum>> costs = command.StartsWith("W:", StringComparison.Ordinal)
                    ? DataBase<WorkshopUpgradeDef>.Find(id).ResourceRequirements
                    : command.StartsWith("B:", StringComparison.Ordinal)
                        ? DataBase<BuildingDef>.Find(id).ResourceRequirements
                        : DataBase<ResearchDef>.Find(id).ResourceRequirements;
                foreach (var cost in costs)
                {
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
                    return "supply";
                if (game.State.PowerSatisfaction < ExpantaNum.One || game.State.LogisticsSatisfaction < ExpantaNum.One)
                    return "supply";
                if (commands[commandIndex].StartsWith("S:", StringComparison.Ordinal))
                    return game.Sectors.GetState(DataBase<SectorDef>.Find(commands[commandIndex].Substring(2))).ColonizationActive
                        ? "supply" : "materials";
                if (commands[commandIndex].StartsWith("E:", StringComparison.Ordinal))
                {
                    var preview = game.Sectors.GetInteriorExplorationPreview(
                        DataBase<SectorDef>.Find(commands[commandIndex].Substring(2)));
                    return preview.Status == SectorInteriorExplorationStatus.WaitingForSupply ? "supply" : "materials";
                }
                if (commands[commandIndex].StartsWith("C:", StringComparison.Ordinal))
                    return "supply";
                if (researches.ActiveResearch != null) return "research";
                if (commands[commandIndex].StartsWith("B:", StringComparison.Ordinal))
                {
                    BuildFailure failure = buildings.GetBuildFailure(
                        DataBase<BuildingDef>.Find(commands[commandIndex].Substring(2)), ExpantaNum.One);
                    if (failure == BuildFailure.ProductivityInsufficient || failure == BuildFailure.SpaceInsufficient)
                        return "supply";
                }
                if (commands[commandIndex].StartsWith("R:", StringComparison.Ordinal) &&
                    researches.IsResearchCompleted(commands[commandIndex].Substring(2))) return "player";
                return "materials";
            }
            Record(0d, "new-game");
            for (int tick = 0; elapsed <= horizon; tick++)
            {
                double seconds = elapsed;
                if (game.State.Population.Population != previousPopulation)
                {
                    previousPopulation = game.State.Population.Population;
                    Record(seconds, "population");
                }
                while (commandIndex < commandCount)
                {
                    string command = commands[commandIndex];
                    string id = command.Substring(2);
                    bool completed;
                    if (command.StartsWith("B:", StringComparison.Ordinal))
                    {
                        var definition = DataBase<BuildingDef>.Find(id);
                        var failure = buildings.GetBuildFailure(definition, ExpantaNum.One);
                        if (failure == BuildFailure.ProductivityInsufficient &&
                            game.State.Population.Population >= game.State.Population.PopulationCapacity &&
                            game.State.FoodNetRate >= ExpantaNum.Zero)
                            Assert.Fail($"Fixed route needs a housing investment before {command} at {seconds / 3600d:F1}h; population is at capacity and productivity={buildings.AvailableProductivity}.");
                        Assert.That(failure, Is.EqualTo(BuildFailure.None).Or.EqualTo(BuildFailure.ResourceInsufficient)
                            .Or.EqualTo(BuildFailure.ProductivityInsufficient).Or.EqualTo(BuildFailure.SpaceInsufficient),
                            $"Invalid fixed command {command} at {seconds / 3600d:F2}h: {failure}");
                        completed = buildings.GetMaxBuildable(definition, ExpantaNum.One) >= ExpantaNum.One &&
                            buildings.TryBuild(definition, ExpantaNum.One, out _);
                        if (completed) Record(seconds, "built:" + id);
                    }
                    else if (command.StartsWith("W:", StringComparison.Ordinal))
                    {
                        var definition = DataBase<WorkshopUpgradeDef>.Find(id);
                        completed = workshops.TryPurchase(definition, out var failure);
                        Assert.That(failure, Is.EqualTo(WorkshopPurchaseFailure.None).Or.EqualTo(WorkshopPurchaseFailure.ResourceInsufficient),
                            $"Invalid fixed command {command} at {seconds / 3600d:F2}h: {failure}");
                        if (completed) Record(seconds, "purchased:" + id);
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
                    else if (command.StartsWith("C:", StringComparison.Ordinal))
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
                    }
                    else if (command.StartsWith("E:", StringComparison.Ordinal))
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
                    }
                    else
                    {
                        var definition = DataBase<ResearchDef>.Find(id);
                        if (!requested)
                        {
                            var result = researches.HandleResearchAction(definition);
                            Assert.That(result, Is.EqualTo(ResearchActionResult.Started).Or.EqualTo(ResearchActionResult.QueuedWaitingResources).Or.EqualTo(ResearchActionResult.Queued));
                            requested = true;
                            Record(seconds, "queued:" + id);
                        }
                        var state = researches.GetState(definition);
                        if (state.CostPaid && !paymentRecorded)
                        {
                            paymentRecorded = true;
                            Record(seconds, "paid:" + id);
                        }
                        completed = state.Status == ResearchStatus.Completed;
                        if (completed) Record(seconds, "completed:" + id);
                    }
                    if (!completed) break;
                    commandIndex++;
                    if (id == "OrbitalEngineering") spacerEnteredAt = seconds;
                    requested = false;
                    paymentRecorded = false;
                }
                if (tick == (int)(600d / step))
                    Assert.That(commandIndex, Is.GreaterThanOrEqualTo(6), "The fixed opening must establish food and stone production without grants.");
                if (tick % (int)(3600d / step) == 0)
                {
                    Assert.That(resources.States.Values.All(state => state.Amount.IsFinite && state.Amount >= ExpantaNum.Zero), Is.True);
                    TestContext.WriteLine($"MAINLINE sample={seconds:F1} next={(commandIndex < commandCount ? commands[commandIndex] : "done")} " +
                        $"food={game.State.FoodAmount} happiness={game.State.HappinessMultiplier}");
                }
                if (commandIndex >= commandCount) break;
                if (elapsed - lastMilestone > 7d * 86400d)
                {
                    RecordShortages();
                    for (int i = 0; i < waitKinds.Length; i++)
                        TestContext.WriteLine($"WAIT sampledOnline={waitKinds[i]} seconds={waitSeconds[i]:F1}");
                    Assert.Fail($"Diagnostic budget exhausted after seven days without a command result or payment; next={commands[commandIndex]}, hours={elapsed / 3600d:F1}. This does not prove the target unreachable.");
                }
                if (visits && elapsed >= sessionEnd - 0.00001d)
                {
                    string before = Blocker();
                    session++;
                    double nextVisit = (session / 3) * 86400d + visitHours[session % 3] * 3600d;
                    double offline = nextVisit - elapsed;
                    Assert.That(offline, Is.GreaterThan(0d));
                    AdvanceOffline(offline);
                    elapsed = nextVisit;
                    sessionEnd = nextVisit + 900d;
                    TestContext.WriteLine($"VISIT session={session} t={elapsed:F1} offline={offline:F1} blockerBefore={before} blockerAfter={Blocker()} next={commands[commandIndex]}");
                    Debug.Log($"Mainline visit={session} hours={elapsed / 3600d:F1} command={commandIndex}/{commandCount} next={commands[commandIndex]}");
                    RecordShortages();
                    RecordExploration("visit:" + session);
                    if (elapsed - lastMilestone > 3d * 86400d)
                        TestContext.WriteLine($"PACING-GAP hoursWithoutAction={(elapsed - lastMilestone) / 3600d:F1} next={commands[commandIndex]}");
                    // Offline windows retain their full length and endpoint blockers, not invented exact attribution.
                }
                else
                {
                    string blocker = Blocker();
                    waitSeconds[Array.IndexOf(waitKinds, blocker)] += step;
                    TickOnline(step);
                    elapsed += step;
                }
            }
            longestMilestoneGap = Math.Max(longestMilestoneGap, elapsed - lastMilestone);
            for (int i = 0; i < waitKinds.Length; i++)
                TestContext.WriteLine($"WAIT sampledOnline={waitKinds[i]} seconds={waitSeconds[i]:F1}");
            TestContext.WriteLine($"SCHEDULE visits={visits} elapsedHours={elapsed / 3600d:F4} sessions={session + 1} target={endpoint} hoursSinceSpacer={(spacerEnteredAt >= 0d ? (elapsed - spacerEnteredAt) / 3600d : 0d):F4}");
            TestContext.WriteLine($"MAINLINE finishedCommands={commandIndex}/{commandCount} longestObservedActionGap={longestMilestoneGap:F1}s");
            Assert.That(game.State.TechLevel, Is.EqualTo(industrial ? TechLevel.Industrial : TechLevel.Spacer), "Fixed mainline must reach the target through paid commands.");
            Assert.That(commandIndex, Is.EqualTo(commandCount), "Every fixed command must complete within the diagnostic horizon.");
            if (visits && industrial) Assert.That(elapsed, Is.LessThanOrEqualTo(3d * 86400d), "Three daily visits must reach Industrial within the agreed three-day target.");
            if (endpoint == "B:PhantomMaterialsFabricator" || endpoint == "B:PhaseMaterialSynthesisArray" ||
                endpoint == "W:ReusableLaunchStages" || endpoint == "B:OrbitalHabitatMegastructure")
            {
                bool phase = endpoint != "B:PhantomMaterialsFabricator";
                string plantId = phase ? "PhaseMaterialSynthesisArray" : "PhantomMaterialsFabricator";
                var product = DataBase<ResourceDef>.Find(phase ? "PhaseMaterial" : "PhantomAlloy");
                var weave = DataBase<ResourceDef>.Find("PhantomWeave");
                ExpantaNum productBefore = resources.GetAmount(product);
                ExpantaNum weaveBefore = resources.GetAmount(weave);
                for (int i = 0; i < Math.Max(1, (int)Math.Ceiling(10d / step)); i++) simulation.ManualTick(step);
                Assert.That(resources.GetAmount(product), Is.GreaterThan(productBefore));
                Assert.That(resources.GetAmount(weave), Is.GreaterThan(weaveBefore));
                Assert.That(game.Sectors.GetState(DataBase<SectorDef>.Find("AlphaCentauri")).Occupied, Is.False);
                Assert.That(game.Sectors.GetState(DataBase<SectorDef>.Find("HeliosCore")).Occupied, Is.False);
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
            if (endpoint == "B:AzureIceSeaArk" || endpoint == "C:AlphaCentauri")
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
                ResourceState electronics = resources.GetState(DataBase<ResourceDef>.Find("Electronics"));
                Assert.That(electronics.ProductionRate, Is.GreaterThanOrEqualTo(electronics.ConsumptionRate),
                    "The expanded electronics industry must cover operating projects after their stage payments.");
            }
        }
        finally
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            ProgressionModifierManager.Rebuild(null);
        }
    }

}
