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
        Building mine = DataBase<Building>.Find("MetalMine");
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
        Building foundry = DataBase<Building>.Find("MetalSmelter");
        Assert.That(
            foundry.ResourceRequirements.Select(pair => pair.First.Id),
            Does.Not.Contain("BasicMetalIngot"));
    }

    [Test]
    public void IronworkingAndSteelmaking_FollowsReachableBronzeChain()
    {
        Research iron = DataBase<Research>.Find("IronworkingAndSteelmaking");
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
        "R:ConcreteEngineering", "W:ReinforcedBoilers", "W:RotaryKilns",
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
        "B:PhantomMaterialsFabricator", "B:PhantomMaterialsFabricator",
        "R:DeepSpaceSurvey", "R:GravityAssistTrajectoryTheory", "W:ReusableLaunchStages"
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
            void Record(double seconds, string action)
            {
                if (action != "population")
                {
                    longestMilestoneGap = Math.Max(longestMilestoneGap, seconds - lastMilestone);
                    lastMilestone = seconds;
                }
                TestContext.WriteLine($"MAINLINE t={seconds:F1} action={action} wood={resources.GetAmount(DataBase<Resource>.Find("WoodLog"))} " +
                    $"population={game.State.Population.Population}/{game.State.Population.PopulationCapacity} " +
                    $"foodNet={game.State.FoodNetRate} productivity={buildings.AvailableProductivity} researchSpeed={researches.CurrentResearchSpeed}");
            }
            const double step = 0.1d; // Match the normal online cadence.
            double horizon = (industrial ? visits ? 28d : 3d : 56d) * 86400d;
            double elapsed = 0d;
            double spacerEnteredAt = -1d;
            double sessionEnd = 1800d;
            int session = 0;
            int[] visitHours = { 0, 5, 13 }; // Relative to the first visit at 08:00.
            var waitSeconds = new double[4];
            string[] waitKinds = { "materials", "research", "supply", "player" };
            void RecordShortages()
            {
                string command = commands[commandIndex];
                string id = command.Substring(2);
                if (command.StartsWith("S:", StringComparison.Ordinal))
                {
                    var sector = DataBase<SectorDefinition>.Find(id);
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
                        var resource = resources.States[DataBase<Resource>.Find(resourceId)];
                        TestContext.WriteLine($"FEEDSTOCK resource={resourceId} amount={resource.Amount} production={resource.ProductionRate} consumption={resource.ConsumptionRate}");
                    }
                    foreach (string buildingId in new[] { "ChemicalPlant", "OilDerrick", "IntegratedPetrochemicalComplex", "CokeOven", "IndustrialCarbonizationRetort" })
                    {
                        var state = buildings.GetState(DataBase<Building>.Find(buildingId));
                        TestContext.WriteLine($"FEEDSTOCK building={buildingId} amount={state.Amount} efficiency={state.Efficiency}");
                    }
                    return;
                }
                IReadOnlyList<Pair<Resource, ExpantaNum>> costs = command.StartsWith("W:", StringComparison.Ordinal)
                    ? DataBase<WorkshopUpgrade>.Find(id).ResourceRequirements
                    : command.StartsWith("B:", StringComparison.Ordinal)
                        ? DataBase<Building>.Find(id).ResourceRequirements
                        : DataBase<Research>.Find(id).ResourceRequirements;
                foreach (var cost in costs)
                {
                    var resource = resources.States[cost.First];
                    ExpantaNum required = cost.Second;
                    if (command.StartsWith("B:", StringComparison.Ordinal))
                    {
                        var definition = DataBase<Building>.Find(id);
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
                    return game.Sectors.GetState(DataBase<SectorDefinition>.Find(commands[commandIndex].Substring(2))).ColonizationActive
                        ? "supply" : "materials";
                if (researches.ActiveResearch != null) return "research";
                if (commands[commandIndex].StartsWith("B:", StringComparison.Ordinal))
                {
                    BuildFailure failure = buildings.GetBuildFailure(
                        DataBase<Building>.Find(commands[commandIndex].Substring(2)), ExpantaNum.One);
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
                        var definition = DataBase<Building>.Find(id);
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
                        var definition = DataBase<WorkshopUpgrade>.Find(id);
                        completed = workshops.TryPurchase(definition, out var failure);
                        Assert.That(failure, Is.EqualTo(WorkshopPurchaseFailure.None).Or.EqualTo(WorkshopPurchaseFailure.ResourceInsufficient),
                            $"Invalid fixed command {command} at {seconds / 3600d:F2}h: {failure}");
                        if (completed) Record(seconds, "purchased:" + id);
                    }
                    else if (command.StartsWith("S:", StringComparison.Ordinal))
                    {
                        var sector = DataBase<SectorDefinition>.Find(id);
                        var state = game.Sectors.GetState(sector);
                        completed = state.Occupied;
                        if (completed) Record(seconds, "occupied:" + id);
                        else if (!state.ColonizationActive)
                        {
                            // This fixed player prepares the material budget before departure; no payment or game gate is added.
                            ExpantaNum duration = sector.ColonizationDurationSeconds /
                                ExpantaNum.Max(ExpantaNum.One, ProgressionModifierManager.Current.ExplorationPowerMultiplier);
                            if (sector.ColonizationResourceRatesPerSecond.Any(cost =>
                                resources.GetAmount(cost.First) < cost.Second * duration)) break;
                            Assert.That(game.Sectors.TryUnlock(sector, out var unlockFailure), Is.True,
                                $"Invalid fixed colonization {id}: {unlockFailure}");
                            Assert.That(game.Sectors.TryAdvanceColonization(sector, 0d, game.State, resources, out var failure), Is.True,
                                $"Cannot start fixed colonization {id}: {failure}");
                            Record(seconds, "colonization-started:" + id);
                        }
                    }
                    else
                    {
                        var definition = DataBase<Research>.Find(id);
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
                if (tick == 6000)
                    Assert.That(commandIndex, Is.GreaterThanOrEqualTo(6), "The fixed opening must establish food and stone production without grants.");
                if (tick % 36000 == 0)
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
                    simulation.AdvanceOffline(offline);
                    elapsed = nextVisit;
                    sessionEnd = nextVisit + 900d;
                    TestContext.WriteLine($"VISIT session={session} t={elapsed:F1} offline={offline:F1} blockerBefore={before} blockerAfter={Blocker()} next={commands[commandIndex]}");
                    Debug.Log($"Mainline visit={session} hours={elapsed / 3600d:F1} command={commandIndex}/{commandCount} next={commands[commandIndex]}");
                    RecordShortages();
                    if (elapsed - lastMilestone > 3d * 86400d)
                        TestContext.WriteLine($"PACING-GAP hoursWithoutAction={(elapsed - lastMilestone) / 3600d:F1} next={commands[commandIndex]}");
                    // Offline windows retain their full length and endpoint blockers, not invented exact attribution.
                }
                else
                {
                    string blocker = Blocker();
                    waitSeconds[Array.IndexOf(waitKinds, blocker)] += step;
                    simulation.ManualTick(step);
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
                endpoint == "W:ReusableLaunchStages")
            {
                bool phase = endpoint != "B:PhantomMaterialsFabricator";
                string plantId = phase ? "PhaseMaterialSynthesisArray" : "PhantomMaterialsFabricator";
                var product = DataBase<Resource>.Find(phase ? "PhaseMaterial" : "PhantomAlloy");
                var weave = DataBase<Resource>.Find("PhantomWeave");
                ExpantaNum productBefore = resources.GetAmount(product);
                ExpantaNum weaveBefore = resources.GetAmount(weave);
                for (int i = 0; i < 100; i++) simulation.ManualTick(step);
                Assert.That(resources.GetAmount(product), Is.GreaterThan(productBefore));
                Assert.That(resources.GetAmount(weave), Is.GreaterThan(weaveBefore));
                Assert.That(game.Sectors.GetState(DataBase<SectorDefinition>.Find("AlphaCentauri")).Occupied, Is.False);
                Assert.That(game.Sectors.GetState(DataBase<SectorDefinition>.Find("HeliosCore")).Occupied, Is.False);
                foreach (var resource in resources.States.Values.OrderBy(state => state.Definition.Id, StringComparer.Ordinal))
                {
                    TestContext.WriteLine($"INDUSTRY resource={resource.Definition.Id} amount={resource.Amount} production={resource.ProductionRate} consumption={resource.ConsumptionRate}");
                }
                TestContext.WriteLine($"INDUSTRY building={plantId} efficiency={buildings.GetState(DataBase<Building>.Find(plantId)).Efficiency} " +
                    $"power={game.State.PowerSatisfaction} logistics={game.State.LogisticsSatisfaction} foodNet={game.State.FoodNetRate}");
                if (phase)
                {
                    Assert.That(buildings.GetState(DataBase<Building>.Find(plantId)).Efficiency,
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
        }
        finally
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            ProgressionModifierManager.Rebuild(null);
        }
    }

}
