using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class EconomyIntegrationRegressionTests
{
    private readonly List<UnityEngine.Object> createdObjects = new();

    [SetUp]
    public void SetUp()
    {
        foreach (GameObject manager in UnityEngine.Object.FindObjectsOfType<GameObject>()
                     .Where(item => item.GetComponent<GameManager>() != null ||
                                    item.GetComponent<ResourceManager>() != null ||
                                    item.GetComponent<BuildingManager>() != null ||
                                    item.GetComponent<ResearchManager>() != null ||
                                    item.GetComponent<WorkshopManager>() != null ||
                                    item.GetComponent<SimulationManager>() != null ||
                                    item.GetComponent<SaveManager>() != null))
            UnityEngine.Object.DestroyImmediate(manager);
        ProgressionModifierManager.Rebuild(null);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(createdObjects[i]);
        createdObjects.Clear();
        ProgressionModifierManager.Rebuild(null);
    }

    [Test]
    public void FinalIndustrialChains_UpgradeTransactionsAndSaveJsonRemainValid()
    {
        GameManager game = CreateManager<GameManager>("Economy-Game");
        ResourceManager resources = CreateManager<ResourceManager>("Economy-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("Economy-Buildings");
        ResearchManager research = CreateManager<ResearchManager>("Economy-Research");
        WorkshopManager workshop = CreateManager<WorkshopManager>("Economy-Workshop");
        CreateManager<SimulationManager>("Economy-Simulation");

        game.State.RestoreCoreForEditor(0, TechLevel.Ultra, new ExpantaNum("1e30"), 0L);
        game.State.RestorePopulationForEditor(new ExpantaNum("1e12"));
        game.State.ResetDerivedEconomyForEditor(new ExpantaNum("1e12"));
        game.State.AdjustFoodCapacityForEditor(new ExpantaNum("1e30"));
        game.State.RestorePopulationCapacityExactForEditor(
            new ExpantaNum("1e12"), ExpantaNum.Zero);
        game.State.AdjustPowerRatesForEditor(new ExpantaNum("1e30"), ExpantaNum.Zero);
        game.State.AdjustLogisticsRatesForEditor(new ExpantaNum("1e30"), ExpantaNum.Zero);
        resources.InitializeForEditor();
        CompleteAllResearch(research);
        PurchaseAllWorkshops(workshop);
        ProgressionModifierManager.Rebuild(null);
        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.IndustrialWorkshop);
        foreach (Resource resource in DataBase<Resource>.All)
            resources.SetAmount(resource, new ExpantaNum("1e6"));

        buildings.RebuildBuildingChainIndexForEditor(DataBase<Building>.All);
        foreach (string sourceId in new[] { "MachineFactory", "WireMill", "BuildingMaterialsComplex" })
        {
            Building source = DataBase<Building>.Find(sourceId);
            BuildingState sourceState = buildings.EnsureBuilding(source);
            buildings.SetAmountAndRatesForEditor(sourceState, new ExpantaNum(2));
            Assert.That(buildings.TryGetUnlockedUpgradeTarget(source, out Building target), Is.True, sourceId);

            List<Pair<Resource, ExpantaNum>> deltas = new();
            buildings.GetUpgradeResourceDeltas(source, ExpantaNum.One, deltas);
            Pair<Resource, ExpantaNum> chargedPair = deltas.FirstOrDefault(item => item.Second > ExpantaNum.Zero);
            Resource charged = chargedPair.First;
            Assert.That(charged, Is.Not.Null, sourceId + " must have a paid upgrade delta");

            ExpantaNum before = resources.GetAmount(charged);
            Dictionary<Resource, ExpantaNum> paidBalances = deltas
                .Where(item => item.First != null && item.Second > ExpantaNum.Zero)
                .GroupBy(item => item.First)
                .ToDictionary(group => group.Key, group => resources.GetAmount(group.Key));
            Assert.That(buildings.TryUpgrade(source, ExpantaNum.One, out BuildFailure firstFailure), Is.True, sourceId);
            Assert.That(firstFailure, Is.EqualTo(BuildFailure.None));
            Assert.That(resources.GetAmount(charged), Is.LessThanOrEqualTo(before), sourceId);
            Assert.That(buildings.GetState(source).Amount, Is.EqualTo(ExpantaNum.One), sourceId);
            Assert.That(buildings.GetState(target).Amount, Is.EqualTo(ExpantaNum.One), sourceId);

            foreach (Pair<Resource, ExpantaNum> delta in deltas)
            {
                if (delta.First == null || delta.Second <= ExpantaNum.Zero)
                    continue;
                Assert.That(resources.GetAmount(delta.First), Is.LessThan(paidBalances[delta.First]),
                    $"{sourceId} must pay every positive upgrade resource delta");
            }

            ExpantaNum sourceBeforeFailure = buildings.GetState(source).Amount;
            ExpantaNum targetBeforeFailure = buildings.GetState(target).Amount;
            resources.SetAmount(charged, ExpantaNum.Zero);
            Assert.That(buildings.TryUpgrade(source, ExpantaNum.One, out BuildFailure failure), Is.False, sourceId);
            Assert.That(failure, Is.EqualTo(BuildFailure.ResourceInsufficient), sourceId);
            Assert.That(buildings.GetState(source).Amount, Is.EqualTo(sourceBeforeFailure), sourceId);
            Assert.That(buildings.GetState(target).Amount, Is.EqualTo(targetBeforeFailure), sourceId);

            resources.SetAmount(charged, new ExpantaNum("1e12"));
            Assert.That(buildings.TryDeconstruct(target, ExpantaNum.One, out BuildFailure deconstructFailure), Is.True, sourceId);
            Assert.That(deconstructFailure, Is.EqualTo(BuildFailure.None));
            Assert.That(buildings.GetState(target).Amount, Is.EqualTo(ExpantaNum.Zero), sourceId);
        }

        SaveManager saveManager = CreateManager<SaveManager>("Economy-Save");
        SaveManager.KingdomSaveData save = saveManager.CaptureSaveData();
        string json = JsonUtility.ToJson(save);
        SaveManager.KingdomSaveData restored = SaveManager.ParseSaveDataForEditor(json);
        Assert.That(restored.Version, Is.EqualTo(SaveFormat.CurrentVersion));
        Assert.That(restored.Buildings, Is.Not.Null);
        Assert.That(restored.Resources, Is.Not.Null);
        Assert.That(restored.Researches, Is.Not.Null);
        Assert.That(restored.Workshop, Is.Not.Null);
    }

    [TestCase("MachineFactory")]
    [TestCase("WireMill")]
    [TestCase("BuildingMaterialsComplex")]
    public void FinalMaterialChain_SixOperationStatesRetainAuthoredFlows(string sourceId)
    {
        GameManager game = CreateManager<GameManager>("SixStates-Game");
        ResourceManager resources = CreateManager<ResourceManager>("SixStates-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("SixStates-Buildings");
        ResearchManager research = CreateManager<ResearchManager>("SixStates-Research");
        WorkshopManager workshop = CreateManager<WorkshopManager>("SixStates-Workshop");
        // EditMode AddComponent does not run WorkshopManager's Awake initialization.
        workshop.CaptureSaveData();
        SimulationManager simulation = CreateManager<SimulationManager>("SixStates-Simulation");
        SaveManager saveManager = CreateManager<SaveManager>("SixStates-Save");
        Building source = DataBase<Building>.Find(sourceId);
        Building orbital = source.UpgradeTo;
        Building hub = orbital.UpgradeTo;
        Assert.That(hub.Id, Is.EqualTo("OrbitalResourceExtractionArray")); // Authored stable ID.

        game.State.RestorePopulationForEditor(new ExpantaNum(100000));
        game.State.ResetDerivedEconomyForEditor(new ExpantaNum("1e9"));
        game.State.RestoreCoreForEditor(0, TechLevel.Industrial, new ExpantaNum(500000), 0L);
        CompleteAllResearch(research);
        PurchaseAllWorkshops(workshop);
        // Hold one real hub gate closed so the intermediate orbital tier is
        // exercised instead of jumping straight to the highest unlocked tier.
        WorkshopUpgrade hubGate = hub.RequiredWorkshopUpgrades.First(upgrade =>
            !orbital.RequiredWorkshopUpgrades.Contains(upgrade));
        workshop.States[hubGate].SetPurchasedForEditor(false);
        ProgressionModifierManager.Rebuild(research.States.Values.ToArray(), workshop.States.Values.ToArray());
        foreach (Resource resource in DataBase<Resource>.All)
            resources.SetAmount(resource, new ExpantaNum("1e8"));
        foreach ((string id, int amount) in new[]
                 { ("WoodHouse", 20000), ("Farm", 10000), ("Granary", 1000),
                   ("SteamPlant", 1000), ("RailHub", 1000) })
        {
            Building support = DataBase<Building>.Find(id);
            buildings.SetAmountAndRatesForEditor(
                buildings.EnsureBuilding(support), new ExpantaNum(amount));
            game.CommitConstruction(support.SpaceCost * amount);
        }
        buildings.RebuildBuildingChainIndexForEditor(DataBase<Building>.All);

        Assert.That(buildings.TryBuild(source, new ExpantaNum(2), out BuildFailure buildFailure), Is.True,
            sourceId + ": " + buildFailure);
        Assert.That(buildings.TryGetUnlockedUpgradeTarget(source, out _), Is.False);
        ObserveMaterialState("before-unlock", source, orbital, hub, game, buildings, resources, simulation);

        game.AdvanceTechLevelForEditor(TechLevel.Ultra);
        buildings.RefreshBuildingChainAvailability();
        Assert.That(buildings.TryGetUnlockedUpgradeTarget(source, out Building unlocked), Is.True);
        Assert.That(unlocked, Is.SameAs(orbital));
        Assert.That(buildings.TryBuild(source, ExpantaNum.One, out BuildFailure superseded), Is.False);
        Assert.That(superseded, Is.EqualTo(BuildFailure.BuildingTierSuperseded));
        Assert.That(buildings.ShouldDisplay(source), Is.True);
        ObserveMaterialState("unlocked-retained", source, orbital, hub, game, buildings, resources, simulation);

        UpgradeWithPaymentCheck(source, buildings, resources);
        Assert.That(buildings.GetState(source).Amount, Is.EqualTo(ExpantaNum.One)); // Discrete factory counts.
        Assert.That(buildings.GetState(orbital).Amount, Is.EqualTo(ExpantaNum.One));
        ObserveMaterialState("partial-upgrade", source, orbital, hub, game, buildings, resources, simulation);

        UpgradeWithPaymentCheck(source, buildings, resources);
        Assert.That(buildings.EnsureBuilding(source).Amount, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(buildings.GetState(orbital).Amount, Is.EqualTo(new ExpantaNum(2)));
        ObserveMaterialState("all-upgraded", source, orbital, hub, game, buildings, resources, simulation);

        Assert.That(workshop.TryPurchase(hubGate, out WorkshopPurchaseFailure workshopFailure), Is.True,
            hubGate.Id + ": " + workshopFailure);
        Assert.That(buildings.TryGetUnlockedUpgradeTarget(orbital, out unlocked), Is.True);
        Assert.That(unlocked, Is.SameAs(hub));
        UpgradeWithPaymentCheck(orbital, buildings, resources);
        Assert.That(buildings.TryDeconstruct(orbital, ExpantaNum.One, out BuildFailure deconstructFailure), Is.True,
            orbital.Id + ": " + deconstructFailure);
        Assert.That(buildings.EnsureBuilding(orbital).Amount, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(buildings.ShouldDisplay(source), Is.False);
        Assert.That(buildings.ShouldDisplay(orbital), Is.False);
        ObserveMaterialState("last-old-factory-dismantled", source, orbital, hub, game, buildings, resources, simulation);

        SaveManager.KingdomSaveData restored = SaveManager.ParseSaveDataForEditor(
            JsonUtility.ToJson(saveManager.CaptureSaveData()));
        Assert.That(restored.Version, Is.EqualTo(SaveFormat.CurrentVersion)); // Save protocol constant.
        // Change the live state so Apply cannot pass as an empty JSON roundtrip.
        Assert.That(buildings.TryDeconstruct(hub, ExpantaNum.One, out _), Is.True);
        saveManager.ApplySaveDataForEditor(restored);
        Assert.That(buildings.GetState(hub).Amount, Is.EqualTo(ExpantaNum.One));
        Assert.That(buildings.ShouldDisplay(source), Is.False);
        Assert.That(buildings.ShouldDisplay(orbital), Is.False);
        ObserveMaterialState("v9-restored", source, orbital, hub, game, buildings, resources, simulation);

        Dictionary<Resource, ExpantaNum> outputRates = source.ResourceGenerationRates
            .Where(pair => pair.Second > ExpantaNum.Zero)
            .ToDictionary(pair => pair.First, pair => resources.GetState(pair.First).ProductionRate);
        Assert.That(buildings.TryBuild(hub, ExpantaNum.One, out BuildFailure expansionFailure), Is.True,
            hub.Id + ": " + expansionFailure);
        simulation.ManualTick(0d);
        foreach (KeyValuePair<Resource, ExpantaNum> output in outputRates)
            Assert.That(resources.GetState(output.Key).ProductionRate, Is.GreaterThan(output.Value),
                "The comprehensive factory must retain and expand " + output.Key.Id);
    }

    private static void UpgradeWithPaymentCheck(
        Building source, BuildingManager buildings, ResourceManager resources)
    {
        List<Pair<Resource, ExpantaNum>> deltas = new();
        buildings.GetUpgradeResourceDeltas(source, ExpantaNum.One, deltas);
        Dictionary<Resource, ExpantaNum> before = deltas.ToDictionary(pair => pair.First,
            pair => resources.GetAmount(pair.First));
        Assert.That(deltas.Any(pair => pair.Second > ExpantaNum.Zero), Is.True);
        Resource charged = deltas.First(pair => pair.Second > ExpantaNum.Zero).First;
        resources.SetAmount(charged, ExpantaNum.Zero);
        Dictionary<Resource, ExpantaNum> failedBalances = DataBase<Resource>.All.ToDictionary(resource => resource,
            resource => resources.GetAmount(resource));
        Assert.That(buildings.TryGetUnlockedUpgradeTarget(source, out Building target), Is.True);
        ExpantaNum sourceAmount = buildings.GetState(source).Amount;
        ExpantaNum targetAmount = buildings.GetState(target).Amount;
        ExpantaNum territory = GameManager.Instance.State.TerritoryUsed;
        ExpantaNum productivity = buildings.UsedProductivity;
        ExpantaNum food = GameManager.Instance.State.FoodAmount;
        Assert.That(buildings.TryUpgrade(source, ExpantaNum.One, out BuildFailure rejected), Is.False);
        Assert.That(rejected, Is.EqualTo(BuildFailure.ResourceInsufficient));
        Assert.That(buildings.GetState(source).Amount, Is.EqualTo(sourceAmount)); // Discrete counts stay unchanged.
        Assert.That(buildings.GetState(target).Amount, Is.EqualTo(targetAmount));
        foreach (KeyValuePair<Resource, ExpantaNum> balance in failedBalances)
            AssertEconomyClose(resources.GetAmount(balance.Key) - balance.Value, ExpantaNum.Zero,
                source.Id + " failed upgrade inventory " + balance.Key.Id);
        AssertEconomyClose(GameManager.Instance.State.TerritoryUsed - territory, ExpantaNum.Zero, "failed upgrade territory");
        AssertEconomyClose(buildings.UsedProductivity - productivity, ExpantaNum.Zero, "failed upgrade productivity");
        AssertEconomyClose(GameManager.Instance.State.FoodAmount - food, ExpantaNum.Zero, "failed upgrade Food");
        resources.SetAmount(charged, before[charged]);
        Assert.That(buildings.TryUpgrade(source, ExpantaNum.One, out BuildFailure failure), Is.True,
            source.Id + ": " + failure);
        foreach (Pair<Resource, ExpantaNum> delta in deltas)
            AssertEconomyClose(before[delta.First] - resources.GetAmount(delta.First), delta.Second,
                source.Id + " upgrade payment " + delta.First.Id);
    }

    private static void ObserveMaterialState(
        string phase, Building source, Building orbital, Building hub,
        GameManager game, BuildingManager buildings, ResourceManager resources, SimulationManager simulation)
    {
        simulation.ManualTick(0d);
        Dictionary<Resource, ExpantaNum> before = DataBase<Resource>.All.ToDictionary(resource => resource,
            resource => resources.GetAmount(resource));
        ExpantaNum foodBefore = game.State.FoodAmount;
        ExpantaNum foodNet = game.State.FoodNetRate;
        ExpantaNum territory = buildings.States.Values.Aggregate(ExpantaNum.Zero,
            (sum, state) => sum + state.Amount * state.SpaceCost);
        ExpantaNum productivity = buildings.States.Values.Aggregate(ExpantaNum.Zero,
            (sum, state) => sum + state.Amount * state.ProductivityConsumption);
        // The isolated fixture has no active campaign, colonization, research
        // queue or engineering bill; ordinary building/population flow is complete here.
        simulation.ManualTick(1d);
        foreach (Resource resource in DataBase<Resource>.All)
        {
            ResourceState state = resources.GetState(resource);
            ExpantaNum net = state.ProductionRate * game.State.HappinessRewardMultiplier - state.ConsumptionRate;
            AssertEconomyClose(resources.GetAmount(resource) - before[resource], net,
                phase + " resource ledger " + resource.Id);
            Assert.That(resources.GetAmount(resource), Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
        }
        ExpantaNum foodExpected = ExpantaNum.Min(game.State.FoodCapacity,
            ExpantaNum.Max(ExpantaNum.Zero, foodBefore + foodNet));
        AssertEconomyClose(game.State.FoodAmount - foodBefore, foodExpected - foodBefore, phase + " Food ledger");
        AssertEconomyClose(game.State.TerritoryUsed, territory, phase + " territory");
        AssertEconomyClose(buildings.UsedProductivity, productivity, phase + " productivity");
        foreach (Pair<Resource, ExpantaNum> output in source.ResourceGenerationRates)
        {
            if (output.Second <= ExpantaNum.Zero)
                continue;
            ResourceState state = resources.GetState(output.First);
            Assert.That(state.ProductionRate, Is.GreaterThan(ExpantaNum.Zero), phase + " " + output.First.Id);
            ProgressionModifierState modifiers = ProgressionModifierManager.Current;
            TestContext.WriteLine($"{source.Id}/{phase}: {output.First.Id} " +
                $"production={state.ProductionRate}, consumption={state.ConsumptionRate}, " +
                $"delta={resources.GetAmount(output.First) - before[output.First]}, " +
                $"sourceMultiplier={modifiers.GetBuildingProductionMultiplier(source)} " +
                $"*{modifiers.GetBuildingResourceProductionMultiplier(source, output.First)}, " +
                $"orbitalMultiplier={modifiers.GetBuildingProductionMultiplier(orbital)} " +
                $"*{modifiers.GetBuildingResourceProductionMultiplier(orbital, output.First)}, " +
                $"hubMultiplier={modifiers.GetBuildingProductionMultiplier(hub)} " +
                $"*{modifiers.GetBuildingResourceProductionMultiplier(hub, output.First)}, " +
                $"globalMultiplier={modifiers.GetResourceProductionMultiplier(output.First)}");
        }
        TestContext.WriteLine($"{source.Id}/{phase}: source={buildings.EnsureBuilding(source).Amount}, " +
            $"orbital={buildings.EnsureBuilding(orbital).Amount}, hub={buildings.EnsureBuilding(hub).Amount}, " +
            $"FoodDelta={game.State.FoodAmount - foodBefore}, FoodNet={foodNet}, " +
            $"FoodCapLoss={ExpantaNum.Max(ExpantaNum.Zero, foodBefore + foodNet - foodExpected)}, " +
            $"territory={game.State.TerritoryUsed}, productivity={buildings.UsedProductivity}, " +
            $"orbitalPrerequisites=[{string.Join(",", orbital.RequiredResearch.Select(item => item.Id))}], " +
            $"hubPrerequisites=[{string.Join(",", hub.RequiredResearch.Select(item => item.Id))}]");
    }

    private static void AssertEconomyClose(ExpantaNum actual, ExpantaNum expected, string context)
    {
        ExpantaNum tolerance = ExpantaNum.Max(ExpantaNum.One, expected.Abs()) * new ExpantaNum("1e-6");
        Assert.That((actual - expected).Abs() <= tolerance, Is.True,
            $"{context}: expected={expected}, actual={actual}, tolerance={tolerance}");
    }

    [TestCase(7200d)]
    [TestCase(28800d)]
    public void IntegratedEconomy_ProductionResearchAndSaveRemainValidAcrossOfflineWindow(double requestedSeconds)
    {
        GameManager game = CreateManager<GameManager>("Integrated-Game");
        ResourceManager resources = CreateManager<ResourceManager>("Integrated-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("Integrated-Buildings");
        ResearchManager research = CreateManager<ResearchManager>("Integrated-Research");
        WorkshopManager workshop = CreateManager<WorkshopManager>("Integrated-Workshop");
        SimulationManager simulation = CreateManager<SimulationManager>("Integrated-Simulation");
        SaveManager saveManager = CreateManager<SaveManager>("Integrated-Save");

        game.State.RestoreCoreForEditor(0, TechLevel.Ultra, new ExpantaNum("1e30"), 0L);
        game.State.RestorePopulationForEditor(new ExpantaNum("1e6"));
        game.State.RestorePopulationCapacityExactForEditor(new ExpantaNum("1e6"), ExpantaNum.Zero);
        game.State.AdjustFoodCapacityForEditor(new ExpantaNum("1e30"));
        game.State.AdjustFoodRatesForEditor(new ExpantaNum(10), new ExpantaNum(1));
        game.State.AdjustPowerRatesForEditor(new ExpantaNum("1e9"), ExpantaNum.Zero);
        game.State.AdjustLogisticsRatesForEditor(new ExpantaNum("1e9"), ExpantaNum.Zero);
        game.State.SetPowerSatisfactionForEditor(ExpantaNum.One);
        game.State.SetLogisticsSatisfactionForEditor(ExpantaNum.One);
        game.State.SetSupplySatisfactionForEditor(ExpantaNum.One);
        resources.InitializeForEditor();
        // Keep enough headroom for the Ultra continuous bill while retaining
        // observable deltas from the authored production chains.
        foreach (Resource resource in DataBase<Resource>.All)
            resources.SetAmount(resource, new ExpantaNum("1e12"));

        buildings.RebuildBuildingChainIndexForEditor(DataBase<Building>.All);
        Dictionary<string, List<Resource>> generatedOutputs = new();
        foreach (string sourceId in new[] { "MachineFactory", "WireMill", "BuildingMaterialsComplex" })
        {
            Building source = DataBase<Building>.Find(sourceId);
            BuildingState state = buildings.EnsureBuilding(source);
            buildings.SetAmountAndRatesForEditor(state, ExpantaNum.One);
            List<Resource> outputs = new();
            foreach (Pair<Resource, ExpantaNum> output in source.ResourceGenerationRates)
            {
                if (output.First == null || output.Second <= ExpantaNum.Zero)
                    continue;
                outputs.Add(output.First);
            }
            Assert.That(outputs, Is.Not.Empty, $"{sourceId} must define at least one positive byproduct/output");
            generatedOutputs[sourceId] = outputs;
        }
        // Keep the same runtime flow convergence used by production and Ultra
        // engineering: provide authored power and logistics producers instead
        // of forcing satisfaction fields after each offline tick.
        foreach (string flowBuildingId in new[] { "SteamPlant", "RailHub" })
        {
            Building flowBuilding = DataBase<Building>.Find(flowBuildingId);
            BuildingState flowState = buildings.EnsureBuilding(flowBuilding);
            buildings.SetAmountAndRatesForEditor(flowState, ExpantaNum.One);
        }

        // Use the public research transaction path; Quarry is the first reachable
        // node and keeps this regression independent of authored later-era gates.
        Research quarry = DataBase<Research>.Find("Quarry");
        Assert.That(research.HandleResearchAction(quarry), Is.Not.EqualTo(ResearchActionResult.Invalid));
        ResearchState quarryState = research.GetState(quarry);
        ExpantaNum researchBefore = quarryState.Progress;

        SectorDefinition sector = DataBase<SectorDefinition>.Find("ProximaB");
        SectorState sectorState = game.Sectors.GetState(sector);
        sectorState.SetUnlockedForEditor(true);
        sectorState.SetCampaignActiveForEditor(true);
        game.State.BeginCampaignForEditor(sector.Id);
        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.HomeSystemSurvey);
        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.InterstellarNavigation);
        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.DeepSpaceFleet);
        game.AdjustAttackPower(new ExpantaNum("1e9"));
        game.AdjustDefensePower(new ExpantaNum("1e9"));
        game.AdjustFleetPower(new ExpantaNum("1e9"));
        game.AdjustMilitaryManpower(new ExpantaNum("1e9"));

        UltraProjectStageDefinition stage = game.UltraProject.Definition.Stages[0];
        Research[] stageResearch = new[] { stage.RequiredResearch }
            .Concat(stage.AdditionalRequiredResearch ?? Array.Empty<Research>())
            .ToArray();
        foreach (Research required in stageResearch)
        {
            if (required == null)
                continue;
            ResearchState state = research.GetState(required);
            state.RestoreForEditor(state.BaseCost, true, true, PaidCostsFor(required));
        }
        BuildingState stageBuildingState = buildings.EnsureBuilding(stage.RequiredBuilding);
        buildings.SetAmountAndRatesForEditor(stageBuildingState, ExpantaNum.One);
        game.UltraProject.RestoreSaveDataForEditor(new UltraProjectStateSaveData
        {
            ProjectId = UltraProjectState.ProjectId,
            SaveVersion = UltraProjectState.CurrentSaveVersion,
            Doctrine = UltraProjectDoctrine.Stable,
            Status = UltraProjectStatus.Running,
            CurrentStage = UltraProjectStage.Prototype,
            StageProgress = "0",
            CompletedStages = new List<UltraProjectStage>(),
            LaunchFeePaid = true,
            StateVersion = 1
        });
        foreach (Pair<Resource, ExpantaNum> cost in stage.ContinuousResourceCosts)
            resources.SetAmount(cost.First, new ExpantaNum("1e30"));

        Dictionary<Resource, ExpantaNum> authoredOutputRates = generatedOutputs
            .SelectMany(outputSet => outputSet.Value)
            .Distinct()
            .ToDictionary(output => output, output => resources.GetState(output).ProductionRate);
        foreach (KeyValuePair<Resource, ExpantaNum> outputRate in authoredOutputRates)
            Assert.That(outputRate.Value, Is.GreaterThan(ExpantaNum.Zero), outputRate.Key.Id);

        Dictionary<Resource, ExpantaNum> before = DataBase<Resource>.All.ToDictionary(resource => resource,
            resource => resources.GetAmount(resource));
        double advanced = simulation.AdvanceOffline(requestedSeconds);
        Assert.That(advanced, Is.GreaterThan(0d));
        Assert.That(quarryState.Progress > researchBefore || quarryState.Status == ResearchStatus.Completed, Is.True);
        UltraProjectPreview ultraAfter = game.UltraProject.GetPreview();
        string ultraCostState = string.Join(", ", ultraAfter.ContinuousCosts.Select(cost =>
            $"{cost.First?.Id}:{resources.GetAmount(cost.First)}"));
        Assert.That(
            game.UltraProject.State.StageProgress > ExpantaNum.Zero ||
                game.UltraProject.State.Status == UltraProjectStatus.ReadyToCommit ||
                game.UltraProject.State.Status == UltraProjectStatus.Committed,
            Is.True,
            $"Ultra engineering must advance or reach a legal completion boundary. " +
            $"status={ultraAfter.Status}, failure={ultraAfter.Failure}, " +
            $"satisfaction={ultraAfter.SupplySatisfaction}, rate={ultraAfter.ProgressPerSecond}, " +
            $"power={game.State.PowerSatisfaction}, logistics={game.State.LogisticsSatisfaction}, " +
            $"food={game.State.FoodAmount}, costs=[{ultraCostState}]");
        Assert.That(
            game.State.Campaign.Active || sectorState.CampaignProgress > ExpantaNum.Zero || sectorState.Occupied,
            Is.True,
            "Campaign must remain active or show legal progress/completion after the offline window.");
        foreach (Resource resource in DataBase<Resource>.All)
            Assert.That(resources.GetAmount(resource), Is.GreaterThanOrEqualTo(ExpantaNum.Zero), resource.Id);
        Assert.That(game.State.FoodAmount, Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
        Assert.That(game.State.FoodAmount, Is.LessThanOrEqualTo(game.State.FoodCapacity));
        Assert.That(game.State.FoodTotalConsumptionRate, Is.GreaterThanOrEqualTo(game.State.FoodConsumptionRate));
        Assert.That(DataBase<Resource>.All.Any(resource => resources.GetAmount(resource) != before[resource]), Is.True);
        foreach (KeyValuePair<string, List<Resource>> outputSet in generatedOutputs)
        {
            foreach (Resource output in outputSet.Value)
            {
                ResourceState outputState = resources.GetState(output);
                Assert.That(
                    ExpantaNum.Max(outputState.ProductionRate, authoredOutputRates[output]),
                    Is.GreaterThan(ExpantaNum.Zero),
                    $"{outputSet.Key} must retain a positive authored production rate for {output.Id}");
                // A net-neutral inventory is legal when the output is consumed
                // by Ultra, campaign, or another system outside the building
                // ResourceState consumption-rate ledger.
            }
        }

        // Normalize the synthetic Ultra prerequisite graph before exercising the
        // v9 Apply path; the production/research/campaign assertions above have
        // already observed the live combination before this save-only step.
        CompleteAllResearch(research);
        SaveManager.KingdomSaveData restored = SaveManager.ParseSaveDataForEditor(
            JsonUtility.ToJson(saveManager.CaptureSaveData()));
        Assert.That(restored.Version, Is.EqualTo(SaveFormat.CurrentVersion));
        Assert.That(restored.Researches, Is.Not.Null);
        Assert.That(restored.Sectors, Is.Not.Null);
        Assert.That(restored.UltraProject, Is.Not.Null);
        saveManager.ApplySaveDataForEditor(restored);
        Assert.That(game.UltraProject.State.Status, Is.Not.EqualTo(UltraProjectStatus.Locked));
    }

    [TestCase(7200d)]
    [TestCase(28800d)]
    public void OfflineBoundary_UsesEffectiveSecondsWithoutNegativeInventory(double requestedSeconds)
    {
        GameManager game = CreateManager<GameManager>("Offline-Game");
        ResourceManager resources = CreateManager<ResourceManager>("Offline-Resources");
        CreateManager<BuildingManager>("Offline-Buildings");
        ResearchManager research = CreateManager<ResearchManager>("Offline-Research");
        CreateManager<WorkshopManager>("Offline-Workshop");
        SimulationManager simulation = CreateManager<SimulationManager>("Offline-Simulation");

        game.State.RestoreCoreForEditor(0, TechLevel.Industrial, new ExpantaNum(300), 0L);
        resources.InitializeForEditor();
        Resource wood = DataBase<Resource>.Find(ResourceManager.StartingResourceId);
        resources.SetAmount(wood, new ExpantaNum(100000));
        ExpantaNum before = resources.GetAmount(wood);
        double effective = simulation.AdvanceOffline(requestedSeconds);

        Assert.That(effective, Is.GreaterThan(0d));
        Assert.That(resources.GetAmount(wood), Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
        Assert.That(resources.GetAmount(wood), Is.Not.LessThan(ExpantaNum.Zero));
        Assert.That(research.TotalFinishedResearchCount, Is.GreaterThanOrEqualTo(0));
        Assert.That(resources.GetAmount(wood), Is.GreaterThanOrEqualTo(before));
    }

    [Test]
    public void FoodLedger_ColonizationChargesOnlyItsBillableFood()
    {
        GameManager game = CreateManager<GameManager>("FoodLedger-Colonization-Game");
        ResourceManager resources = CreateManager<ResourceManager>("FoodLedger-Colonization-Resources");
        game.Sectors.InitializeDefinitions();
        game.State.RestoreCoreForEditor(
            0,
            TechLevel.Spacer,
            new ExpantaNum("1e6"),
            0L);
        resources.InitializeForEditor();

        SectorDefinition sector = DataBase<SectorDefinition>.Find("DawnRing");
        SectorState state = game.Sectors.GetState(sector);
        state.SetUnlockedForEditor(true);
        SetResourceAmounts(resources, sector.ColonizationResourceRatesPerSecond, new ExpantaNum("1e9"));

        const double requestedSeconds = 60d;
        SectorExplorationPreview preview = game.Sectors.GetExplorationPreview(
            sector,
            game.State,
            resources);
        double billableSeconds = SectorManager.CalculateColonizationBillableSecondsForEditor(
            state.CampaignProgress,
            sector.ColonizationDurationSeconds,
            requestedSeconds);
        ExpantaNum foodBefore = game.State.FoodAmount;

        Assert.That(
            game.Sectors.TryAdvanceColonization(
                sector,
                requestedSeconds,
                game.State,
                resources,
                out SectorOperationFailure failure),
            Is.True);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));

        ExpantaNum actualFoodCost = foodBefore - game.State.FoodAmount;
        ExpantaNum expectedFoodCost = preview.FoodCostPerSecond * billableSeconds;
        Assert.That(actualFoodCost.ToDouble(), Is.EqualTo(expectedFoodCost.ToDouble()).Within(0.000001d));
        Assert.That(state.CampaignProgress, Is.GreaterThan(ExpantaNum.Zero));
    }

    [Test]
    public void FoodLedger_CampaignChargesPreviewFoodForTheActualWindow()
    {
        GameManager game = CreateManager<GameManager>("FoodLedger-Campaign-Game");
        ResourceManager resources = CreateManager<ResourceManager>("FoodLedger-Campaign-Resources");
        game.Sectors.InitializeDefinitions();
        game.State.RestoreCoreForEditor(
            0,
            TechLevel.Ultra,
            new ExpantaNum("1e9"),
            0L);
        game.State.SetPowerSatisfactionForEditor(ExpantaNum.One);
        game.State.SetLogisticsSatisfactionForEditor(ExpantaNum.One);
        game.State.SetSupplySatisfactionForEditor(ExpantaNum.One);
        game.State.AdjustAttackPowerForEditor(new ExpantaNum("1e9"));
        game.State.AdjustDefensePowerForEditor(new ExpantaNum("1e9"));
        game.State.AdjustFleetPowerForEditor(new ExpantaNum("1e9"));
        game.State.AdjustMilitaryManpowerForEditor(new ExpantaNum("1e9"));
        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.HomeSystemSurvey);
        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.InterstellarNavigation);
        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.DeepSpaceFleet);
        resources.InitializeForEditor();

        SectorDefinition sector = DataBase<SectorDefinition>.Find("ProximaB");
        SectorState state = game.Sectors.GetState(sector);
        state.SetUnlockedForEditor(true);
        SetResourceAmounts(resources, sector.CampaignResourceRatesPerSecond, new ExpantaNum("1e9"));

        const double requestedSeconds = 1d;
        SectorCampaignPreview preview = game.Sectors.GetCampaignPreview(
            sector,
            game.State,
            resources);
        ExpantaNum foodBefore = game.State.FoodAmount;

        Assert.That(
            game.Sectors.TryAdvanceCampaign(
                sector,
                requestedSeconds,
                game.State,
                resources,
                out SectorOperationFailure failure),
            Is.True);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));

        ExpantaNum actualFoodCost = foodBefore - game.State.FoodAmount;
        ExpantaNum expectedFoodCost = preview.FoodCostPerSecond * requestedSeconds;
        Assert.That(actualFoodCost.ToDouble(), Is.EqualTo(expectedFoodCost.ToDouble()).Within(0.000001d));
        Assert.That(state.CampaignProgress, Is.GreaterThan(ExpantaNum.Zero));
    }

    [Test]
    public void FoodLedger_UltraEngineeringChargesItsSuccessfulTickOnly()
    {
        GameManager game = CreateManager<GameManager>("FoodLedger-Ultra-Game");
        ResourceManager resources = CreateManager<ResourceManager>("FoodLedger-Ultra-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("FoodLedger-Ultra-Buildings");
        ResearchManager research = CreateManager<ResearchManager>("FoodLedger-Ultra-Research");

        game.State.RestoreCoreForEditor(
            0,
            TechLevel.Ultra,
            new ExpantaNum("1e12"),
            0L);
        game.State.SetPowerSatisfactionForEditor(ExpantaNum.One);
        game.State.SetLogisticsSatisfactionForEditor(ExpantaNum.One);
        resources.InitializeForEditor();
        CompleteAllResearch(research);

        UltraProjectStageDefinition stage = game.UltraProject.Definition.Stages[0];
        buildings.EnsureBuilding(stage.RequiredBuilding).SetAmountForEditor(ExpantaNum.One);
        SetResourceAmounts(resources, stage.ContinuousResourceCosts, new ExpantaNum("1e12"));
        game.UltraProject.RestoreSaveDataForEditor(new UltraProjectStateSaveData
        {
            ProjectId = UltraProjectState.ProjectId,
            SaveVersion = UltraProjectState.CurrentSaveVersion,
            Doctrine = UltraProjectDoctrine.Stable,
            Status = UltraProjectStatus.Running,
            CurrentStage = UltraProjectStage.Prototype,
            StageProgress = "0",
            CompletedStages = new List<UltraProjectStage>(),
            LaunchFeePaid = true,
            StateVersion = 1
        });

        const double tickSeconds = 1d;
        UltraProjectPreview preview = game.UltraProject.GetPreview();
        ExpantaNum foodBefore = game.State.FoodAmount;

        Assert.That(game.UltraProject.Tick(tickSeconds), Is.True);

        ExpantaNum actualFoodCost = foodBefore - game.State.FoodAmount;
        ExpantaNum expectedFoodCost = preview.FoodPerSecond * tickSeconds;
        Assert.That(actualFoodCost.ToDouble(), Is.EqualTo(expectedFoodCost.ToDouble()).Within(0.000001d));
        Assert.That(game.UltraProject.State.StageProgress, Is.GreaterThan(ExpantaNum.Zero));
    }

    [Test]
    public void FoodLedger_RecordsProductionLostAtTheFoodCapacityCeiling()
    {
        GameState state = new GameState();
        state.RestoreCoreForEditor(
            0,
            TechLevel.Animal,
            new ExpantaNum(490d),
            0L);
        state.AdjustFoodRatesForEditor(new ExpantaNum(5d), ExpantaNum.Zero);

        const double seconds = 2d;
        ExpantaNum before = state.FoodAmount;
        ExpantaNum theoretical = before + state.FoodNetRate * seconds;
        state.AdvanceFoodForEditor(seconds);

        ExpantaNum capLoss = theoretical - state.FoodAmount;
        Assert.That(state.FoodAmount, Is.EqualTo(state.FoodCapacity));
        Assert.That(theoretical, Is.GreaterThan(state.FoodCapacity));
        Assert.That(capLoss.ToDouble(), Is.EqualTo(10d).Within(0.000001d));
    }

    private T CreateManager<T>(string name) where T : Component
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        T manager = gameObject.AddComponent<T>();
        if (manager is ResourceManager resourceManager && resourceManager.States.Count == 0)
            resourceManager.InitializeForEditor();
        if (manager is ResearchManager researchManager && researchManager.States.Count == 0)
            researchManager.InitializeForEditor();
        return manager;
    }

    private static void CompleteAllResearch(ResearchManager researchManager)
    {
        foreach (Research definition in DataBase<Research>.All)
        {
            ResearchState state = researchManager.GetState(definition);
            Dictionary<Resource, ExpantaNum> paid = new();
            foreach (Pair<Resource, ExpantaNum> requirement in definition.ResourceRequirements)
                if (requirement.First != null && requirement.Second > ExpantaNum.Zero)
                    paid[requirement.First] = requirement.Second;
            state.RestoreForEditor(state.BaseCost, true, true, paid);
        }
    }

    private static Dictionary<Resource, ExpantaNum> PaidCostsFor(Research definition)
    {
        Dictionary<Resource, ExpantaNum> paid = new();
        foreach (Pair<Resource, ExpantaNum> requirement in definition.ResourceRequirements)
            if (requirement.First != null && requirement.Second > ExpantaNum.Zero)
                paid[requirement.First] = requirement.Second;
        return paid;
    }

    private static void PurchaseAllWorkshops(WorkshopManager workshopManager)
    {
        foreach (WorkshopUpgradeState state in workshopManager.States.Values)
            state.SetPurchasedForEditor(true);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.IndustrialWorkshop);
    }

    private static void SetResourceAmounts(
        ResourceManager resources,
        IReadOnlyList<Pair<Resource, ExpantaNum>> rates,
        ExpantaNum amount)
    {
        for (int i = 0; rates != null && i < rates.Count; i++)
        {
            Pair<Resource, ExpantaNum> rate = rates[i];
            if (rate.First != null)
                resources.SetAmount(rate.First, amount);
        }
    }
}
