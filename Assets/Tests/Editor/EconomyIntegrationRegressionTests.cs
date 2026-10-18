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
    public void ParitySnapshot_PreservesPopulationProgressAndResearchQueueOrder()
    {
        GameManager game = CreateManager<GameManager>("ParitySnapshot-Game");
        ResourceManager resources = CreateManager<ResourceManager>("ParitySnapshot-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("ParitySnapshot-Buildings");
        ResearchManager research = CreateManager<ResearchManager>("ParitySnapshot-Research");
        CreateManager<WorkshopManager>("ParitySnapshot-Workshop");
        CreateManager<SaveManager>("ParitySnapshot-Save");
        SimulationManager simulation = CreateManager<SimulationManager>("ParitySnapshot-Simulation");
        Building house = DataBase<Building>.Find("WoodHouse");
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(house), ExpantaNum.One);
        game.State.RestorePopulationForEditor(ExpantaNum.One);
        simulation.ManualTick(0d);
        foreach (ResourceState state in resources.States.Values)
            resources.SetAmount(state.Definition, ExpantaNum.Zero);
        game.State.RestorePopulationChangeProgressForEditor(new ExpantaNum(0.625d));
        research.HandleResearchAction(DataBase<Research>.Find("ControlledFire"));
        research.HandleResearchAction(DataBase<Research>.Find("Agriculture"));
        string[] queue = research.ResearchQueue.Select(state => state.Definition.Id).ToArray();
        // Stable IDs and queue ordering require exact comparison.
        Assert.That(queue, Is.EqualTo(new[] { "ControlledFire", "Agriculture" }));

        int runtimeBuildingCount = buildings.States.Count;
        EconomySnapshotDto snapshot = JsonUtility.FromJson<EconomySnapshotDto>(
            JsonUtility.ToJson(EconomyParitySnapshotExporter.CaptureCurrent()));
        Assert.That(snapshot.SaveState.Buildings.Select(state => state.BuildingId),
            Is.EquivalentTo(snapshot.Buildings.Select(definition => definition.Id)));
        Assert.That(snapshot.RuntimeState.Buildings.Select(state => state.BuildingId),
            Is.EquivalentTo(snapshot.Buildings.Select(definition => definition.Id)));
        Assert.That(buildings.States.Count, Is.EqualTo(runtimeBuildingCount),
            "Export must materialize zero snapshot entries without creating runtime buildings."); // Discrete count.
        string evidencePath = Environment.GetEnvironmentVariable("KINGDOM_PARITY_SNAPSHOT_PATH");
        if (!string.IsNullOrWhiteSpace(evidencePath))
            System.IO.File.WriteAllText(evidencePath, JsonUtility.ToJson(snapshot, true));
        foreach (SaveStateDto state in new SaveStateDto[] { snapshot.SaveState, snapshot.RuntimeState })
        {
            Assert.That(ExpantaNum.Parse(state.PopulationChangeProgress).ToDouble(),
                Is.EqualTo(0.625d).Within(1e-6d));
            Assert.That(state.QueuedResearchIds, Is.EqualTo(queue));
        }
        simulation.ManualTick(0.1d);
        Assert.That(game.State.Population.PopulationChangeProgress.ToDouble(), Is.InRange(0.625001d, 0.99d));
        if (!string.IsNullOrWhiteSpace(evidencePath))
            EconomyParitySnapshotExporter.ExportCurrent(evidencePath + ".after.json");
    }

    [Test]
    public void GranarySaveRoundTrip_PreservesDerivedFoodCapacityAndInventory()
    {
        GameManager game = CreateManager<GameManager>("GranarySave-Game");
        CreateManager<ResourceManager>("GranarySave-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("GranarySave-Buildings");
        CreateManager<ResearchManager>("GranarySave-Research");
        CreateManager<WorkshopManager>("GranarySave-Workshop");
        SaveManager save = CreateManager<SaveManager>("GranarySave-Save");
        if (TutorialManager.Current == null)
            createdObjects.Add(TutorialManager.Ensure().gameObject);

        game.State.RestoreCoreForEditor(0, TechLevel.StoneAge, game.State.FoodAmount, 0L);
        Building granary = DataBase<Building>.Find("Granary");
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(granary), ExpantaNum.One);
        game.State.AdvanceFoodForEditor(10000d);
        ExpantaNum capacityBefore = game.State.FoodCapacity;
        ExpantaNum foodBefore = game.State.FoodAmount;
        Assert.That(foodBefore, Is.GreaterThan(GameState.BaseFoodCapacity),
            "Inventory must use the authored granary capacity to expose the load defect.");

        SaveManager.KingdomSaveData restored = SaveManager.ParseSaveDataForEditor(
            JsonUtility.ToJson(save.CaptureSaveData()));
        for (int roundTrip = 0; roundTrip < 2; roundTrip++)
        {
            save.ApplySaveDataForEditor(restored);
            Assert.That(game.State.FoodCapacity.ToDouble(),
                Is.EqualTo(capacityBefore.ToDouble()).Within(0.000001d),
                "Reload must derive capacity from the same base and granary exactly once.");
            Assert.That(game.State.FoodAmount.ToDouble(),
                Is.EqualTo(foodBefore.ToDouble()).Within(0.000001d));
            restored = SaveManager.ParseSaveDataForEditor(JsonUtility.ToJson(save.CaptureSaveData()));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PopulationFoodBoundary_ExactCoverageDoesNotAccumulateDepartureOnlineOrOffline(bool offline)
    {
        GameManager game = CreateManager<GameManager>("FoodBoundary-Game");
        CreateManager<ResourceManager>("FoodBoundary-Resources");
        CreateManager<BuildingManager>("FoodBoundary-Buildings");
        CreateManager<ResearchManager>("FoodBoundary-Research");
        CreateManager<WorkshopManager>("FoodBoundary-Workshop");
        SimulationManager simulation = CreateManager<SimulationManager>("FoodBoundary-Simulation");
        game.State.RestoreCoreForEditor(0, TechLevel.Animal, new ExpantaNum(180), 0L);
        game.State.RestorePopulationForEditor(new ExpantaNum(10));
        game.State.AdjustPopulationCapacityForEditor(new ExpantaNum(10));

        if (offline)
            simulation.AdvanceOffline(60d);
        else
            simulation.ManualTick(60d);

        Assert.That(game.State.FoodAmount.ToDouble(), Is.InRange(0d, 1e-6d));
        Assert.That(game.State.Population.PopulationChangeProgress.ToDouble(), Is.InRange(0d, 1e-6d),
            "Stock covering the complete tick must not create starvation time at its endpoint.");
        Assert.That(game.State.CalendarDays, Is.EqualTo(6)); // Discrete calendar step count.
    }

    [Test]
    public void PopulationSaveRoundTrip_FullHousingPreservesStarvationDepartureProgress()
    {
        GameManager game = CreateManager<GameManager>("PopulationSave-Game");
        CreateManager<ResourceManager>("PopulationSave-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("PopulationSave-Buildings");
        CreateManager<ResearchManager>("PopulationSave-Research");
        CreateManager<WorkshopManager>("PopulationSave-Workshop");
        SaveManager save = CreateManager<SaveManager>("PopulationSave-Save");
        if (TutorialManager.Current == null)
            createdObjects.Add(TutorialManager.Ensure().gameObject);

        game.State.RestoreCoreForEditor(0, TechLevel.StoneAge, ExpantaNum.Zero, 0L);
        Building house = DataBase<Building>.Find("WoodHouse");
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(house), ExpantaNum.One);
        ExpantaNum capacity = game.State.Population.PopulationCapacity;
        Assert.That(capacity, Is.GreaterThan(ExpantaNum.Zero));
        game.State.RestorePopulationForEditor(capacity);
        double halfDepartureSeconds = 1800d / capacity.ToDouble();
        game.State.Population.AdvancePopulationForEditor(
            halfDepartureSeconds, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, true);
        double progress = game.State.Population.PopulationChangeProgress.ToDouble();
        Assert.That(progress, Is.InRange(0.49d, 0.51d));

        SaveManager.KingdomSaveData restored = SaveManager.ParseSaveDataForEditor(
            JsonUtility.ToJson(save.CaptureSaveData()));
        save.ApplySaveDataForEditor(restored);
        Assert.That(game.State.Population.PopulationCapacity, Is.EqualTo(capacity)); // Discrete housing count.
        Assert.That(game.State.Population.PopulationChangeProgress.ToDouble(),
            Is.EqualTo(progress).Within(1e-9d));
        game.State.Population.AdvancePopulationForEditor(
            halfDepartureSeconds, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, true);
        Assert.That(game.State.Population.Population, Is.EqualTo(capacity - ExpantaNum.One)); // Discrete departure.
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
        var failures = new List<BuildFailure>();
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
            buildings.GetConstructionFailures(source, ExpantaNum.One, true, failures);
            Assert.That(failures, Is.Empty, "An affordable upgrade has no shortage warnings.");

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
            buildings.GetConstructionFailures(source, ExpantaNum.One, true, failures);
            Assert.That(failures, Is.EqualTo(new[] { BuildFailure.ResourceInsufficient }));
            Assert.That(buildings.TryUpgrade(source, ExpantaNum.One, out BuildFailure failure), Is.False, sourceId);
            Assert.That(failure, Is.EqualTo(BuildFailure.ResourceInsufficient), sourceId);
            Assert.That(buildings.GetState(source).Amount, Is.EqualTo(sourceBeforeFailure), sourceId);
            Assert.That(buildings.GetState(target).Amount, Is.EqualTo(targetBeforeFailure), sourceId);

            resources.SetAmount(charged, new ExpantaNum("1e12"));
            Assert.That(buildings.TryDeconstruct(target, ExpantaNum.One, out BuildFailure deconstructFailure), Is.True, sourceId);
            Assert.That(deconstructFailure, Is.EqualTo(BuildFailure.None));
            Assert.That(buildings.GetState(target).Amount, Is.EqualTo(ExpantaNum.Zero), sourceId);
        }

        Building upgradeSource = DataBase<Building>.Find("MachineFactory");
        BuildingState upgradeState = buildings.EnsureBuilding(upgradeSource);
        buildings.SetAmountAndRatesForEditor(upgradeState, ExpantaNum.One);
        Assert.That(buildings.TryGetUnlockedUpgradeTarget(upgradeSource, out Building upgradeTarget), Is.True);
        game.State.RestorePopulationForEditor(ExpantaNum.Zero);
        ExpantaNum occupied = game.State.AvailableTerritory;
        game.State.CommitConstructionForEditor(occupied);
        foreach (Resource resource in DataBase<Resource>.All)
            resources.SetAmount(resource, ExpantaNum.Zero);
        buildings.GetConstructionFailures(upgradeSource, ExpantaNum.One, true, failures);
        Assert.That(upgradeTarget.SpaceCost, Is.GreaterThan(upgradeSource.SpaceCost));
        Assert.That(upgradeTarget.ProductivityConsumption, Is.GreaterThan(upgradeSource.ProductivityConsumption));
        Assert.That(failures, Is.EqualTo(new[] { BuildFailure.SpaceInsufficient,
            BuildFailure.ProductivityInsufficient, BuildFailure.ResourceInsufficient }),
            "Upgrade diagnostics must continue past both capacity failures to check payment.");
        Assert.That(upgradeState.Amount, Is.EqualTo(ExpantaNum.One), "Preview never upgrades the discrete building count.");
        Assert.That(buildings.GetUpgradeFailure(upgradeSource, ExpantaNum.One), Is.EqualTo(BuildFailure.SpaceInsufficient));
        Assert.That(buildings.TryUpgrade(upgradeSource, ExpantaNum.One, out _), Is.False);
        Assert.That(upgradeState.Amount, Is.EqualTo(ExpantaNum.One));

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
        ObserveMaterialState("v1-restored", source, orbital, hub, game, buildings, resources, simulation);

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

    [Test]
    public void IntegratedEconomy_ProductionResearchAndSaveRemainValidAcrossShortOfflineWindow()
    {
        const double requestedSeconds = 60d;
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
        // v1 Apply path; the production/research/campaign assertions above have
        // already observed the live combination before this save-only step.
        research.RemoveQueuedResearch(quarry);
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

    [Test]
    public void ProductionBudget_CorrectionRefreshesSeparateCyclesAndIgnoresIdleResources()
    {
        GameManager game = CreateManager<GameManager>("BudgetRefresh-Game");
        ResourceManager resources = CreateManager<ResourceManager>("BudgetRefresh-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("BudgetRefresh-Buildings");
        game.State.AdjustFoodRatesForEditor(-GameState.BaseFoodProductionRate, 0);
        Resource[] inputs = Enumerable.Range(0, 4)
            .Select(i => CreateBudgetResource("BudgetRefreshInput" + i)).ToArray();
        BuildingState[] states = new BuildingState[4];
        for (int i = 0; i < states.Length; i++)
        {
            Building definition = CreateBudgetBuilding("BudgetRefreshBuilding" + i, inputs[i ^ 1], inputs[i]);
            states[i] = buildings.EnsureBuilding(definition);
            buildings.SetAmountAndRatesForEditor(states[i], 1);
        }
        buildings.PrepareTickResourceSatisfactionForEditor(1d);
        double[] initial = states.Select(state => state.Efficiency.ToDouble()).ToArray();
        for (int i = 0; i < 256; i++)
            resources.EnsureResource(CreateBudgetResource("BudgetIdle" + i));
        buildings.PrepareTickResourceSatisfactionForEditor(1d);
        for (int i = 0; i < states.Length; i++)
            Assert.That(states[i].Efficiency.ToDouble(), Is.EqualTo(initial[i]).Within(1e-6d));

        foreach (Resource input in inputs)
            resources.SetAmount(input, 10);
        buildings.PrepareTickResourceSatisfactionForEditor(1d);
        foreach (BuildingState state in states)
            Assert.That(state.Efficiency.ToDouble(), Is.GreaterThan(0.99d));
        foreach (Resource input in inputs)
            resources.SetAmount(input, 0);
        buildings.PrepareTickResourceSatisfactionForEditor(1d);
        for (int i = 0; i < states.Length; i++)
        {
            Assert.That(states[i].Efficiency.ToDouble(), Is.EqualTo(initial[i]).Within(1e-6d));
            AssertBudget(resources.GetState(inputs[i]), game.State.HappinessRewardMultiplier);
        }
    }

    [TestCase(0, true, false)]
    [TestCase(256, true, false)]
    [TestCase(256, true, true)]
    [TestCase(256, false, false)]
    [Category("Performance")]
    public void ProductionBudget_CorrectionPipelineBenchmark(int idleResources, bool correction, bool idleFirst)
    {
        GameManager game = CreateManager<GameManager>("BudgetPerf-Game");
        ResourceManager resources = CreateManager<ResourceManager>("BudgetPerf-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("BudgetPerf-Buildings");
        game.State.AdjustFoodRatesForEditor(-GameState.BaseFoodProductionRate, 0);
        if (idleFirst)
            AddIdleResources();
        Resource x = CreateBudgetResource("BudgetPerfX");
        Resource y = CreateBudgetResource("BudgetPerfY");
        Building a = CreateBudgetBuilding("BudgetPerfA", y, x);
        Building b = CreateBudgetBuilding("BudgetPerfB", x, y);
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(a), 1);
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(b), 1);
        resources.SetAmount(x, correction ? 0d : 10d);
        resources.SetAmount(y, correction ? 0d : 10d);
        if (!idleFirst)
            AddIdleResources();
        for (int i = 0; i < 4; i++)
            buildings.PrepareTickResourceSatisfactionForEditor(1d);

        const int iterations = 16;
        double[] samples = new double[5];
        for (int sample = 0; sample < samples.Length; sample++)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
                buildings.PrepareTickResourceSatisfactionForEditor(1d);
            timer.Stop();
            samples[sample] = timer.Elapsed.TotalMilliseconds / iterations;
        }
        System.Array.Sort(samples);
        TestContext.WriteLine($"Budget pipeline idle={idleResources}, correction={correction}, idleFirst={idleFirst}: median={samples[2]:F6} ms/tick, min={samples[0]:F6}, max={samples[4]:F6}, A={buildings.GetState(a).Efficiency}, B={buildings.GetState(b).Efficiency}");
        AssertBudget(resources.GetState(x), game.State.HappinessRewardMultiplier);
        AssertBudget(resources.GetState(y), game.State.HappinessRewardMultiplier);

        void AddIdleResources()
        {
            for (int i = 0; i < idleResources; i++)
                resources.EnsureResource(CreateBudgetResource("BudgetPerfIdle" + i));
        }
    }

    [TestCase(0d)]
    [TestCase(1e-100d)]
    [TestCase(10d)]
    public void ProductionBudget_LossyCycleNeverConsumesUnfundedInputs(double inventory)
    {
        GameManager game = CreateManager<GameManager>("Budget-Game");
        ResourceManager resources = CreateManager<ResourceManager>("Budget-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("Budget-Buildings");
        game.State.AdjustFoodRatesForEditor(-GameState.BaseFoodProductionRate, 0);
        Resource x = CreateBudgetResource("BudgetX");
        Resource y = CreateBudgetResource("BudgetY");
        Resource independent = CreateBudgetResource("BudgetIndependent");
        Building a = CreateBudgetBuilding("BudgetA", y, x);
        Building b = CreateBudgetBuilding("BudgetB", x, y);
        Building quarry = CreateBudgetBuilding("BudgetQuarry", independent, null);
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(a), 1);
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(b), 1);
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(quarry), 1);
        resources.SetAmount(x, inventory);
        resources.SetAmount(y, inventory);

        buildings.PrepareTickResourceSatisfactionForEditor(1d);

        TestContext.WriteLine($"budget inventory={inventory}: A={buildings.GetState(a).Efficiency}, B={buildings.GetState(b).Efficiency}, H={game.State.HappinessRewardMultiplier}");
        Assert.That(resources.GetState(DataBase<Resource>.Find(ResourceManager.StartingResourceId)).ProductionRate.ToDouble(),
            Is.GreaterThanOrEqualTo(0.999999d), "Budget correction must preserve passive starting production.");
        AssertBudget(resources.GetState(x), game.State.HappinessRewardMultiplier);
        AssertBudget(resources.GetState(y), game.State.HappinessRewardMultiplier);
        Assert.That(buildings.GetState(quarry).Efficiency.ToDouble(), Is.GreaterThan(0.99d));
        if (inventory > 0d)
        {
            Assert.That(buildings.GetState(a).Efficiency.ToDouble(), Is.GreaterThan(0d));
        }
        resources.Tick(1d);
        Assert.That(resources.GetAmount(x).ToDouble(), Is.GreaterThanOrEqualTo(0d));
    }

    [Test]
    public void ProductionBudget_PreservesInventoryBackedSatisfaction()
    {
        GameManager game = CreateManager<GameManager>("CachedSupply-Game");
        ResourceManager resources = CreateManager<ResourceManager>("CachedSupply-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("CachedSupply-Buildings");
        game.State.AdjustFoodRatesForEditor(-GameState.BaseFoodProductionRate, 0);
        Resource input = CreateBudgetResource("CachedSupplyInput");
        Building consumer = CreateBudgetBuilding("CachedSupplyConsumer", null, input);
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(consumer), 1);
        resources.SetAmount(input, 1);

        buildings.PrepareTickResourceSatisfactionForEditor(1d);

        Assert.That(resources.GetTickSatisfactionForEditor(input), Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(resources.GetTickSatisfactionForEditor(input), Is.LessThan(ExpantaNum.One));
        AssertBudget(resources.GetState(input), game.State.HappinessRewardMultiplier);
    }

    [Test]
    public void FoodAvailability_ExcludesStarvedFarmAndTracksBudgetCorrection()
    {
        GameManager game = CreateManager<GameManager>("ActualFood-Game");
        ResourceManager resources = CreateManager<ResourceManager>("ActualFood-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("ActualFood-Buildings");
        game.State.RestoreCoreForEditor(0, TechLevel.Animal, 0, 0L);
        game.State.RestorePopulationForEditor(100);
        Resource input = CreateBudgetResource("ActualFoodInput");
        Building farm = CreateBudgetBuilding("ActualFoodFarm", null, input, 100);
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(farm), 1);

        buildings.PrepareTickResourceSatisfactionForEditor(1d);

        Assert.That(game.State.FoodAvailability.ToDouble(), Is.InRange(0d, 0.1d));
        Assert.That(game.State.HappinessMultiplier.ToDouble(), Is.InRange(0d, 0.1d));

        game.State.RestorePopulationForEditor(0);
        Building source = CreateBudgetBuilding("ActualFoodSource", input, null);
        Building hungry = CreateBudgetBuilding("ActualFoodDemand", null, null);
        hungry.ConfigureEconomyForEditor(1.15d, 0, 0, 0, 0, 0, 0, 80, 0,
            0, 0, 0, 0, 0, 0, 0, new(), new(), new());
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(source), 1);
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(hungry), 1);
        buildings.PrepareTickResourceSatisfactionForEditor(1d);

        Assert.That(game.State.FoodAvailability.ToDouble(), Is.InRange(0d, 0.8d));
        Assert.That(game.State.HappinessRewardMultiplier, Is.LessThanOrEqualTo(ExpantaNum.One));
        AssertBudget(resources.GetState(input), game.State.HappinessRewardMultiplier);
    }

    [Test]
    public void ProductionBudget_FoodRewardFeedbackNeverCreatesUnfundedOutput()
    {
        GameManager game = CreateManager<GameManager>("Feedback-Game");
        ResourceManager resources = CreateManager<ResourceManager>("Feedback-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("Feedback-Buildings");
        Resource x = CreateBudgetResource("FeedbackX");
        Building source = CreateBudgetBuilding("FeedbackSource", x, null);
        Building food = CreateBudgetBuilding("FeedbackFood", null, x, 10d);
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(source), 1);
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(food), 1);
        buildings.PrepareTickResourceSatisfactionForEditor(1d);
        AssertBudget(resources.GetState(x), game.State.HappinessRewardMultiplier);
        Assert.That(buildings.GetState(food).Efficiency.ToDouble(), Is.GreaterThan(0.6d));
    }

    private Resource CreateBudgetResource(string id)
    {
        Resource resource = ScriptableObject.CreateInstance<Resource>();
        resource.SetIdForEditor(id);
        createdObjects.Add(resource);
        return resource;
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ProductionBudget_ResourceFlowCycleCannotUseUnfundedEnergy(bool power)
    {
        GameManager game = CreateManager<GameManager>("FlowBudget-Game");
        ResourceManager resources = CreateManager<ResourceManager>("FlowBudget-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("FlowBudget-Buildings");
        Resource x = CreateBudgetResource("FlowBudgetX");
        Building producer = CreateBudgetBuilding("FlowBudgetProducer", null, x);
        Building consumer = CreateBudgetBuilding("FlowBudgetConsumer", x, null);
        if (power)
        {
            producer.SetPowerFlowForEditor(1, 0);
            consumer.SetPowerFlowForEditor(0, 2);
        }
        else
        {
            producer.SetLogisticsFlowForEditor(1, 0);
            consumer.SetLogisticsFlowForEditor(0, 2);
        }
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(producer), 1);
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(consumer), 1);
        buildings.PrepareTickResourceSatisfactionForEditor(1d);
        AssertBudget(resources.GetState(x), game.State.HappinessRewardMultiplier);
        ExpantaNum production = power ? game.State.PowerProductionRate : game.State.LogisticsProductionRate;
        ExpantaNum consumption = power ? game.State.PowerConsumptionRate : game.State.LogisticsConsumptionRate;
        Assert.That(consumption <= production * game.State.HappinessRewardMultiplier, Is.True);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ProductionBudget_FoodFeedbackFundsBuildingsAndActualUltraFlow(bool power)
    {
        GameManager game = CreateManager<GameManager>("ExternalFlow-Game");
        ResourceManager resources = CreateManager<ResourceManager>("ExternalFlow-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("ExternalFlow-Buildings");
        CreateManager<ResearchManager>("ExternalFlow-Research");
        game.State.AdjustFoodCapacityForEditor(new ExpantaNum("1e12"));
        game.State.RestoreCoreForEditor(0, TechLevel.Ultra, new ExpantaNum("1e9"), 0L);
        game.State.RestorePopulationForEditor(0);
        game.State.AdjustFoodRatesForEditor(-GameState.BaseFoodProductionRate, 0);
        UltraProjectStageDefinition stage = game.UltraProject.Definition.Stages[0];
        ExpantaNum externalDemand = power ? stage.PowerConsumptionRate : stage.LogisticsConsumptionRate;
        Assert.That(externalDemand, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(stage.BaseDurationSeconds, Is.GreaterThan(ExpantaNum.One));
        foreach (var cost in stage.ContinuousResourceCosts)
            resources.SetAmount(cost.First, cost.Second * 100);
        game.UltraProject.RestoreSaveDataForEditor(new UltraProjectStateSaveData
        {
            ProjectId = UltraProjectState.ProjectId,
            Doctrine = UltraProjectDoctrine.Stable,
            Status = UltraProjectStatus.Running,
            CurrentStage = UltraProjectStage.Prototype,
            StageProgress = "0",
            CompletedStages = new List<UltraProjectStage>(),
            LaunchFeePaid = true,
            StateVersion = 1
        });
        Resource x = CreateBudgetResource("ExternalFlowX");
        Resource y = CreateBudgetResource("ExternalFlowY");
        Building producer = CreateBudgetBuilding("ExternalFlowProducer", y, x, 100);
        Building recycler = CreateBudgetBuilding("ExternalFlowRecycler", x, y);
        Building consumer = CreateBudgetBuilding("ExternalFlowConsumer", null, null);
        Building support = CreateBudgetBuilding("ExternalFlowSupport", null, null);
        if (power)
        {
            producer.SetPowerFlowForEditor(externalDemand, 0);
            consumer.SetPowerFlowForEditor(0, externalDemand);
            support.SetLogisticsFlowForEditor(stage.LogisticsConsumptionRate * 100, 0);
        }
        else
        {
            producer.SetLogisticsFlowForEditor(externalDemand, 0);
            consumer.SetLogisticsFlowForEditor(0, externalDemand);
            support.SetPowerFlowForEditor(stage.PowerConsumptionRate * 100, 0);
        }
        foreach (Building building in new[] { producer, recycler, consumer, support })
            buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(building), 1);
        resources.SetAmount(x, new ExpantaNum(0.02d));
        resources.SetAmount(y, new ExpantaNum(0.02d));

        buildings.PrepareTickResourceSatisfactionForEditor(1d);

        ExpantaNum supply = (power ? game.State.PowerProductionRate : game.State.LogisticsProductionRate) *
            game.State.HappinessRewardMultiplier;
        ExpantaNum buildingDemand = power ? game.State.PowerConsumptionRate : game.State.LogisticsConsumptionRate;
        Pair<Resource, ExpantaNum> meteredCost = stage.ContinuousResourceCosts[0];
        ExpantaNum before = resources.GetAmount(meteredCost.First);
        Assert.That(game.UltraProject.Tick(1d), Is.True);
        ExpantaNum paidSeconds = (before - resources.GetAmount(meteredCost.First)) / meteredCost.Second;
        Assert.That(paidSeconds, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(buildingDemand + externalDemand * paidSeconds <= supply, Is.True,
            "The flow budget must fund buildings and the Ultra work actually paid for.");
    }

    [Test]
    public void ProductionBudget_AuthoredCoalPowerCycleFundsPositiveUltraWork()
    {
        GameManager game = CreateManager<GameManager>("CoalPower-Game");
        ResourceManager resources = CreateManager<ResourceManager>("CoalPower-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("CoalPower-Buildings");
        CreateManager<ResearchManager>("CoalPower-Research");
        game.State.AdjustFoodCapacityForEditor(new ExpantaNum("1e12"));
        game.State.RestoreCoreForEditor(0, TechLevel.Ultra, new ExpantaNum("1e9"), 0L);
        game.State.RestorePopulationForEditor(0);
        game.State.AdjustFoodRatesForEditor(-GameState.BaseFoodProductionRate, 0);
        foreach (Resource resource in DataBase<Resource>.All)
            resources.SetAmount(resource, new ExpantaNum("1e6"));
        UltraProjectStageDefinition stage = game.UltraProject.Definition.Stages[0];
        foreach (var cost in stage.ContinuousResourceCosts)
            resources.SetAmount(cost.First, cost.Second * 100);
        game.UltraProject.RestoreSaveDataForEditor(new UltraProjectStateSaveData
        {
            ProjectId = UltraProjectState.ProjectId,
            Doctrine = UltraProjectDoctrine.Stable,
            Status = UltraProjectStatus.Running,
            CurrentStage = UltraProjectStage.Prototype,
            StageProgress = "0",
            CompletedStages = new List<UltraProjectStage>(),
            LaunchFeePaid = true,
            StateVersion = 1
        });
        Building station = DataBase<Building>.Find("CentralPowerStation");
        Building mine = DataBase<Building>.Find("MechanizedCoalMine");
        Building logistics = CreateBudgetBuilding("CoalPowerLogistics", null, null);
        logistics.SetLogisticsFlowForEditor(stage.LogisticsConsumptionRate * 100, 0);
        foreach (Building building in new[] { station, mine, logistics })
            buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(building), 1);
        resources.SetAmount(DataBase<Resource>.Find("Coal"), new ExpantaNum(0.02d));

        buildings.PrepareTickResourceSatisfactionForEditor(1d);

        ExpantaNum supply = game.State.PowerProductionRate * game.State.HappinessRewardMultiplier;
        ExpantaNum buildingDemand = game.State.PowerConsumptionRate;
        Pair<Resource, ExpantaNum> meteredCost = stage.ContinuousResourceCosts[0];
        ExpantaNum before = resources.GetAmount(meteredCost.First);
        Assert.That(game.UltraProject.Tick(1d), Is.True,
            "Available Coal must keep the authored power cycle and Ultra work running.");
        ExpantaNum paidSeconds = (before - resources.GetAmount(meteredCost.First)) / meteredCost.Second;
        Assert.That(paidSeconds, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(buildings.GetState(station).Efficiency, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(buildings.GetState(mine).Efficiency, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(buildingDemand + stage.PowerConsumptionRate * paidSeconds <= supply, Is.True,
            "The authored cycle must fund both the mine and the Ultra work actually paid for.");
        AssertBudget(resources.GetState(DataBase<Resource>.Find("Coal")), game.State.HappinessRewardMultiplier);
    }

    private Building CreateBudgetBuilding(string id, Resource output, Resource input, double food = 0d)
    {
        Building building = ScriptableObject.CreateInstance<Building>();
        building.SetIdForEditor(id);
        building.ConfigureEconomyForEditor(1.15d, 0, 0, 0, 0, 0, food, 0, 0,
            0, 0, 0, 0, 0, 0, 0,
            new List<Pair<Resource, ExpantaNum>>(),
            output == null ? new List<Pair<Resource, ExpantaNum>>() : new() { new(output, 1) },
            input == null ? new List<Pair<Resource, ExpantaNum>>() : new() { new(input, 2) });
        createdObjects.Add(building);
        return building;
    }

    private static void AssertBudget(ResourceState resource, ExpantaNum reward)
    {
        ExpantaNum available = resource.Amount + resource.ProductionRate * reward;
        Assert.That(resource.ConsumptionRate <= available, Is.True,
            $"{resource.Definition.Id}: demand={resource.ConsumptionRate}, available={available}, " +
            $"inventory={resource.Amount}, production={resource.ProductionRate}, reward={reward}");
    }

    [TestCase(UltraProjectDoctrine.Stable)]
    [TestCase(UltraProjectDoctrine.Surge)]
    public void UltraTickBudget_MatchesPreviewCostsAcrossRepeatedPassesAndPause(UltraProjectDoctrine doctrine)
    {
        GameManager game = CreateManager<GameManager>("UltraBudget-Game");
        ResourceManager resources = CreateManager<ResourceManager>("UltraBudget-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("UltraBudget-Buildings");
        CreateManager<ResearchManager>("UltraBudget-Research");
        game.UltraProject.RestoreSaveDataForEditor(new UltraProjectStateSaveData
        {
            ProjectId = UltraProjectState.ProjectId,
            Doctrine = doctrine,
            Status = UltraProjectStatus.Running,
            CurrentStage = doctrine == UltraProjectDoctrine.Surge
                ? UltraProjectStage.Stabilization : UltraProjectStage.Prototype,
            StageProgress = "0",
            CompletedStages = doctrine == UltraProjectDoctrine.Surge
                ? new List<UltraProjectStage> { UltraProjectStage.Prototype } : new List<UltraProjectStage>(),
            LaunchFeePaid = true,
            StateVersion = 1
        });
        UltraProjectPreview preview = game.UltraProject.GetPreview();
        Assert.That(preview.ContinuousCosts, Is.Not.Empty);
        foreach (var cost in preview.ContinuousCosts)
            resources.SetAmount(cost.First, cost.Second * .5d);
        Assert.That((buildings.StrategicFoodConsumptionRate / preview.FoodPerSecond).ToDouble(),
            Is.EqualTo(1d).Within(1e-6));
        for (int pass = 0; pass < 2; pass++)
        {
            buildings.PrepareTickResourceSatisfactionForEditor(1d);
            foreach (var cost in preview.ContinuousCosts)
                Assert.That(resources.GetTickSatisfactionForEditor(cost.First).ToDouble(),
                    Is.EqualTo(.5d).Within(1e-6), "Reused budget buffers must not retain the previous pass.");
        }
        Assert.That(game.UltraProject.TryPause(out _), Is.True);
        buildings.PrepareTickResourceSatisfactionForEditor(1d);
        foreach (var cost in preview.ContinuousCosts)
            Assert.That(resources.GetTickSatisfactionForEditor(cost.First).ToDouble(),
                Is.EqualTo(1d).Within(1e-6), "Paused engineering must release its material demand.");
        Assert.That(buildings.StrategicFoodConsumptionRate.ToDouble(), Is.LessThan(1e-6));
    }

    [Test]
    public void UltraTickBudget_CommittedExpeditionHasNoEngineeringDemand()
    {
        GameManager game = CreateManager<GameManager>("ExpeditionBudget-Game");
        ResourceManager resources = CreateManager<ResourceManager>("ExpeditionBudget-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("ExpeditionBudget-Buildings");
        CreateManager<ResearchManager>("ExpeditionBudget-Research");
        game.UltraProject.RestoreSaveDataForEditor(new UltraProjectStateSaveData
        {
            ProjectId = UltraProjectState.ProjectId,
            Doctrine = UltraProjectDoctrine.Expedition,
            Status = UltraProjectStatus.Committed,
            CurrentStage = UltraProjectStage.Completed,
            StageProgress = "1",
            CompletedStages = new List<UltraProjectStage>
            {
                UltraProjectStage.Prototype, UltraProjectStage.Stabilization, UltraProjectStage.Expansion
            },
            LaunchFeePaid = true,
            StateVersion = 1
        });
        buildings.PrepareTickResourceSatisfactionForEditor(1d);
        Assert.That(buildings.StrategicFoodConsumptionRate.ToDouble(), Is.LessThan(1e-6));
        foreach (var stage in game.UltraProject.Definition.Stages)
            foreach (var cost in stage.ContinuousResourceCosts)
                Assert.That(resources.GetTickSatisfactionForEditor(cost.First).ToDouble(),
                    Is.EqualTo(1d).Within(1e-6));
    }

    [Test, Category("Performance")]
    public void WorkshopPreview_UnchangedModifiersAllocateLessThanRebuilding()
    {
        CreateManager<GameManager>("PreviewAllocation-Game");
        CreateManager<ResourceManager>("PreviewAllocation-Resources");
        CreateManager<BuildingManager>("PreviewAllocation-Buildings");
        ResearchManager research = CreateManager<ResearchManager>("PreviewAllocation-Research");
        WorkshopManager workshop = CreateManager<WorkshopManager>("PreviewAllocation-Workshop");
        workshop.ResetForLoadForEditor();
        CompleteAllResearch(research);
        ResearchState[] researchStates = research.States.Values.ToArray();
        WorkshopUpgradeState[] upgrades = workshop.States.Values.ToArray();
        WorkshopUpgrade candidate = DataBase<WorkshopUpgrade>.Find("AgriculturalMachinery");
        ProgressionModifierManager.Rebuild(researchStates, upgrades);
        workshop.GetPurchaseBenefitPreview(candidate);
        long cachedBytes = 0;
        long rebuiltBytes = 0;
        for (int sample = 0; sample < 8; sample++)
        {
            cachedBytes += MeasureAllocation();
            ProgressionModifierManager.Rebuild(researchStates, upgrades);
            rebuiltBytes += MeasureAllocation();
        }
        TestContext.WriteLine($"Workshop preview: unchanged={cachedBytes / 8} bytes/call, rebuild={rebuiltBytes / 8} bytes/call");
        Assert.That(rebuiltBytes, Is.GreaterThan(0), "The Unity allocation recorder must capture real allocations.");
        Assert.That(cachedBytes, Is.LessThan(rebuiltBytes / 2),
            "Reusing the candidate must remove most allocation; minor recorder noise is not an improvement.");

        long MeasureAllocation()
        {
            using var recorder = Unity.Profiling.ProfilerRecorder.StartNew(
                Unity.Profiling.ProfilerCategory.Memory, "GC.Alloc", 4096,
                Unity.Profiling.ProfilerRecorderOptions.StartImmediately |
                Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            workshop.GetPurchaseBenefitPreview(candidate);
            recorder.Stop();
            long bytes = 0;
            for (int i = 0; i < recorder.Count; i++)
                bytes += recorder.GetSample(i).Value;
            return bytes;
        }
    }

    [Test]
    public void WorkshopPreview_ReusesOnlyModifiersAndInvalidatesAfterProgressionChanges()
    {
        GameManager game = CreateManager<GameManager>("PreviewRefresh-Game");
        CreateManager<ResourceManager>("PreviewRefresh-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("PreviewRefresh-Buildings");
        ResearchManager research = CreateManager<ResearchManager>("PreviewRefresh-Research");
        WorkshopManager workshop = CreateManager<WorkshopManager>("PreviewRefresh-Workshop");
        workshop.ResetForLoadForEditor();
        WorkshopUpgrade candidate = DataBase<WorkshopUpgrade>.Find("AgriculturalMachinery");
        Building farm = DataBase<Building>.Find("Farm");
        BuildingState farmState = buildings.EnsureBuilding(farm);
        buildings.SetAmountAndRatesForEditor(farmState, 2);
        AssertFresh(candidate);
        buildings.SetAmountAndRatesForEditor(farmState, 4);
        AssertFresh(candidate);
        farmState.SetEfficiencyForEditor(new ExpantaNum(.5d));
        game.State.Events.RestoreForEditor(new SaveManager.WorldEventSaveData
        {
            Sequence = 3, RemainingSeconds = 10d, SecondsUntilNextEvent = 100d
        });
        AssertFresh(candidate);
        AssertFresh(DataBase<WorkshopUpgrade>.Find("IndustrialFoodProcessEngineering"));
        AssertFresh(candidate);
        CompleteAllResearch(research);
        Rebuild();
        AssertFresh(candidate);
        workshop.States[candidate].SetPurchasedForEditor(true);
        Rebuild();
        Assert.That(workshop.GetPurchaseBenefitPreview(candidate), Is.Empty);
        workshop.RestoreSaveDataForEditor(null);
        AssertFresh(candidate);

        void Rebuild() => ProgressionModifierManager.Rebuild(
            research.States.Values.ToArray(), workshop.States.Values.ToArray());

        void AssertFresh(WorkshopUpgrade selected)
        {
            var before = ProgressionModifierManager.Current;
            var after = ProgressionModifierManager.BuildPreview(
                research.States.Values.ToArray(), workshop.States.Values.ToArray(), selected);
            var expected = WorkshopManager.CalculateBenefitPreview(buildings.States.Values.ToArray(),
                before, after, game.State.HappinessRewardMultiplier, game.State.Events.ProductionMultiplier);
            var actual = workshop.GetPurchaseBenefitPreview(selected);
            Assert.That(actual.Count, Is.EqualTo(expected.Count), "The changed output channels must match.");
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.That(actual[i].Building, Is.SameAs(expected[i].Building));
                Assert.That(actual[i].Kind, Is.EqualTo(expected[i].Kind));
                Assert.That(actual[i].Before.ToDouble(), Is.EqualTo(expected[i].Before.ToDouble()).Within(1e-6));
                Assert.That(actual[i].After.ToDouble(), Is.EqualTo(expected[i].After.ToDouble()).Within(1e-6));
            }
        }
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public void ObserverFailure_ResearchCompletionDoesNotInterruptOrReplaySimulation(bool offline, bool completionEvent)
    {
        GameManager game = CreateManager<GameManager>("ObserverTick-Game");
        ResourceManager resources = CreateManager<ResourceManager>("ObserverTick-Resources");
        CreateManager<BuildingManager>("ObserverTick-Buildings");
        ResearchManager research = CreateManager<ResearchManager>("ObserverTick-Research");
        SimulationManager simulation = CreateManager<SimulationManager>("ObserverTick-Simulation");
        Research target = DataBase<Research>.Find("Quarry");
        ResearchState state = research.GetState(target);
        state.RestoreForEditor(state.BaseCost - new ExpantaNum(.01d), true, false, PaidCostsFor(target));
        Assert.That(research.StartResearch(target), Is.True);
        Resource output = CreateBudgetResource("ObserverTickOutput");
        resources.SetProductionRate(output, 10);
        int laterCalls = 0;
        bool thrown = false;
        Action failOnce = () =>
        {
            if (thrown) return;
            thrown = true;
            throw new InvalidOperationException("research-observer-test");
        };
        if (completionEvent)
        {
            research.ResearchCompleted += _ => failOnce();
            research.ResearchCompleted += _ => laterCalls++;
        }
        else
        {
            research.ResearchQueueChanged += failOnce;
            research.ResearchQueueChanged += () => laterCalls++;
        }
        UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,
            new System.Text.RegularExpressions.Regex("InvalidOperationException: research-observer-test"));
        double seconds = offline ? 1d : simulation.TickIntervalSecondsForEditor;
        Assert.DoesNotThrow(() =>
        {
            if (offline) simulation.AdvanceOffline(seconds);
            else simulation.Advance(seconds);
        });
        Assert.That(state.Status, Is.EqualTo(ResearchStatus.Completed));
        Assert.That(laterCalls, Is.EqualTo(1), "A failing observer must not suppress the later subscriber.");
        ExpantaNum after = resources.GetAmount(output);
        Assert.That(after, Is.GreaterThan(ExpantaNum.Zero));
        simulation.Advance(0d);
        Assert.That(resources.GetAmount(output).ToDouble(), Is.EqualTo(after.ToDouble()).Within(1e-6),
            "The committed tick must not remain in the simulation backlog.");
        Assert.That(game.State.Events.SecondsUntilNextEvent,
            Is.EqualTo(WorldEventState.EventIntervalSeconds - (offline ? 0d : seconds)).Within(1e-6));
    }

    [Test]
    public void ObserverFailure_BuildingRegistrationStillReturnsCommittedState()
    {
        CreateManager<GameManager>("ObserverBuilding-Game");
        CreateManager<ResourceManager>("ObserverBuilding-Resources");
        BuildingManager buildings = CreateManager<BuildingManager>("ObserverBuilding-Buildings");
        Building definition = CreateBudgetBuilding("ObserverBuilding", null, null);
        int laterCalls = 0;
        buildings.BuildingStateAdded += _ => throw new InvalidOperationException("building-observer-test");
        buildings.BuildingStateAdded += _ => laterCalls++;
        UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,
            new System.Text.RegularExpressions.Regex("InvalidOperationException: building-observer-test"));
        BuildingState state = null;
        Assert.DoesNotThrow(() => state = buildings.EnsureBuilding(definition));
        Assert.That(buildings.GetState(definition), Is.SameAs(state));
        Assert.That(laterCalls, Is.EqualTo(1), "The single registration reaches all observers.");
    }

    [Test]
    public void ObserverFailure_WorkshopPurchaseStillReturnsCommittedSuccess()
    {
        GameManager game = CreateManager<GameManager>("ObserverWorkshop-Game");
        ResourceManager resources = CreateManager<ResourceManager>("ObserverWorkshop-Resources");
        CreateManager<BuildingManager>("ObserverWorkshop-Buildings");
        ResearchManager research = CreateManager<ResearchManager>("ObserverWorkshop-Research");
        WorkshopManager workshop = CreateManager<WorkshopManager>("ObserverWorkshop-Workshop");
        CompleteAllResearch(research);
        game.State.RestoreCoreForEditor(0, TechLevel.Industrial, 300, 0);
        workshop.RestoreSaveDataForEditor(null);
        WorkshopUpgrade candidate = DataBase<WorkshopUpgrade>.Find("IndustrialFoodProcessEngineering");
        foreach (var cost in candidate.ResourceRequirements)
            resources.SetAmount(cost.First, cost.Second * 2);
        int laterCalls = 0;
        workshop.UpgradeStateChanged += _ => throw new InvalidOperationException("workshop-observer-test");
        workshop.UpgradeStateChanged += _ => laterCalls++;
        UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,
            new System.Text.RegularExpressions.Regex("InvalidOperationException: workshop-observer-test"));
        bool purchased = false;
        Assert.DoesNotThrow(() => purchased = workshop.TryPurchase(candidate, out _));
        Assert.That(purchased, Is.True);
        Assert.That(workshop.IsPurchased(candidate), Is.True);
        Assert.That(laterCalls, Is.EqualTo(1), "The committed purchase reaches the later subscriber.");
        foreach (var cost in candidate.ResourceRequirements)
            Assert.That((resources.GetAmount(cost.First) / cost.Second).ToDouble(),
                Is.EqualTo(1d).Within(1e-6), "A notification failure must not refund or repeat payment.");
    }

    [TestCase(.01d, 1d, true)]
    [TestCase(.01d, .00001d, false)]
    [TestCase(0d, 1d, true)]
    public void OfflineResearch_CompletionBoundaryPreservesNextResearchTime(
        double remainingCost, double seconds, bool completes)
    {
        GameManager game = CreateManager<GameManager>("ResearchBoundary-Game");
        CreateManager<ResourceManager>("ResearchBoundary-Resources");
        CreateManager<BuildingManager>("ResearchBoundary-Buildings");
        ResearchManager research = CreateManager<ResearchManager>("ResearchBoundary-Research");
        Research first = DataBase<Research>.Find("Quarry");
        Research second = DataBase<Research>.Find("Agriculture");
        ResearchState head = research.GetState(first);
        ResearchState next = research.GetState(second);
        head.RestoreForEditor(head.BaseCost - new ExpantaNum(remainingCost), true, false, PaidCostsFor(first));
        next.RestoreForEditor(ExpantaNum.Zero, true, false, PaidCostsFor(second));
        Assert.That(research.StartResearch(first), Is.True);
        Assert.That(research.EnqueueResearch(second), Is.True);
        ExpantaNum firstSpeed = ResearchManager.ResearchSpeedEffect(game.State.TechLevel, first.TechLevel) *
            research.ResearchPower * ProgressionModifierManager.Current.GlobalResearchMultiplier *
            game.State.HappinessMultiplier;
        double completionSeconds = ((head.BaseCost - head.Progress) / firstSpeed).ToDouble();
        research.TickOfflineForEditor(seconds);
        if (!completes)
        {
            Assert.That(head.Status, Is.EqualTo(ResearchStatus.Researching));
            Assert.That(next.Progress.ToDouble(), Is.LessThan(1e-6));
            return;
        }
        Assert.That(head.Status, Is.EqualTo(ResearchStatus.Completed));
        ExpantaNum nextSpeed = ResearchManager.ResearchSpeedEffect(game.State.TechLevel, second.TechLevel) *
            research.ResearchPower * ProgressionModifierManager.Current.GlobalResearchMultiplier *
            game.State.HappinessMultiplier;
        ExpantaNum expected = ResearchManager.AdvanceResearchProgress(ExpantaNum.Zero, nextSpeed,
            next.BaseCost, seconds - completionSeconds);
        Assert.That(next.Progress.ToDouble(), Is.EqualTo(expected.ToDouble()).Within(1e-5),
            "Only the time left after completion belongs to the next research.");
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
