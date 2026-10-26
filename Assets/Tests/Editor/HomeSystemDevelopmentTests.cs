using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

// Controlled infrastructure fixtures verify runtime behavior, not new-game pacing.
public sealed class HomeSystemDevelopmentTests
{
    private GameObject root;
    private BuildingDef support;
    private string saveRoot;
    private GameManager game;
    private ResourceManager resources;
    private BuildingManager buildings;
    private ResearchManager research;
    private SimulationManager simulation;
    private WorkshopManager workshop;

    [SetUp]
    public void SetUp()
    {
        ProgressionModifierManager.Rebuild(null);
        root = new GameObject(nameof(HomeSystemDevelopmentTests));
        game = root.AddComponent<GameManager>();
        resources = root.AddComponent<ResourceManager>();
        buildings = root.AddComponent<BuildingManager>();
        research = root.AddComponent<ResearchManager>();
        workshop = root.AddComponent<WorkshopManager>();
        workshop.ResetForLoadForEditor();
        simulation = root.AddComponent<SimulationManager>();
        game.AdvanceTechLevelForEditor(TechLevel.Spacer);
        game.State.RestorePopulationForEditor(new ExpantaNum(1e6d));
        research.InitializeForEditor();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        if (support != null) Object.DestroyImmediate(support);
        SaveManager.ClearSaveRootOverrideForTests();
        if (saveRoot != null && Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
        ProgressionModifierManager.Rebuild(null);
    }

    [TestCase("DawnRing", "DawnOrbitalObservatory")]
    [TestCase("AzurePool", "EarthMoonLogisticsHub")]
    [TestCase("AzurePool", "AzureIceSeaArk")]
    [TestCase("Terminus", "TerminusMaterialsWorks")]
    [TestCase("ShardCrown", "ShardCrownOreSorter")]
    [TestCase("HeliosCore", "DawnDysonSphere")]
    [TestCase("ThunderGate", "ThunderGatePropellantWorks")]
    [TestCase("HeliosCore", "HeliosPowerArray")]
    public void AuthoredDevelopmentRequiresOccupationPaysAtomicallyAndRespectsLocalLimit(
        string sectorId, string buildingId)
    {
        SectorBuildingDef building = DataBase<SectorBuildingDef>.Find(buildingId);
        Assert.That(building, Is.Not.Null);
        SectorDef sector = DataBase<SectorDef>.Find(sectorId);
        Assert.That(building.Sector, Is.SameAs(sector));
        Assert.That(building.SpaceCost, Is.EqualTo(ExpantaNum.Zero), "Sector buildings use their local limit.");
        Assert.That(building.HasValidCostGrowth, Is.True);
        Assert.That(building.MaxAmount, Is.GreaterThan(0));
        Assert.That(sector.ResourceRewards, Is.Empty);
        Assert.That(sector.OccupiedResourceRatesPerSecond, Is.Empty);
        foreach (ResearchDef required in building.RequiredResearch) CompleteResearch(required);
        FundConstruction(building);
        var before = SnapshotResources();
        Assert.That(buildings.TryBuild(building, ExpantaNum.One, out BuildFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.SectorNotOccupied));
        AssertBalances(before);
        Occupy(sector, false);
        if (building.RequiresInteriorExploration)
        {
            before = SnapshotResources();
            Assert.That(buildings.TryBuild(building, ExpantaNum.One, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(BuildFailure.InteriorExplorationIncomplete));
            AssertBalances(before);
            CompleteInteriorExploration(sector);
        }
        ResourceDef missing = building.ResourceRequirements[building.ResourceRequirements.Count - 1].First;
        resources.SetAmount(missing, ExpantaNum.Zero);
        before = SnapshotResources();
        Assert.That(buildings.TryBuild(building, ExpantaNum.One, out failure), Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.ResourceInsufficient));
        AssertBalances(before);
        Assert.That(buildings.GetState(building).Amount, Is.EqualTo(ExpantaNum.Zero));
        ExpantaNum territory = game.State.TerritoryUsed;
        for (int i = 0; i < building.MaxAmount; i++)
        {
            FundConstruction(building);
            Assert.That(buildings.TryBuild(building, ExpantaNum.One, out failure), Is.True, failure.ToString());
        }
        Near(game.State.TerritoryUsed, territory);
        before = SnapshotResources();
        Assert.That(buildings.TryBuild(building, ExpantaNum.One, out failure), Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.BuildingLimitReached));
        AssertBalances(before);
        Near(buildings.GetState(building).Amount, new ExpantaNum(building.MaxAmount));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AuthoredPropellantRecipeStopsWithoutChemicalsAndResumesAfterSupply(bool offline)
    {
        AddNeutralSupport();
        // Keep this recipe fixture at population equilibrium. Otherwise the
        // dynamic-population cadence repeatedly quantizes fractional upkeep,
        // so its accumulated rounding no longer measures the recipe ratio.
        game.State.RestorePopulationForEditor(game.State.Population.PopulationCapacity);
        SectorBuildingDef works = DataBase<SectorBuildingDef>.Find("ThunderGatePropellantWorks");
        Occupy(works.Sector);
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            resources.SetAmount(resource, new ExpantaNum(1e6d));
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(works), ExpantaNum.One);
        ResourceDef chemical = DataBase<ResourceDef>.Find("Chemical");
        ResourceDef fuel = DataBase<ResourceDef>.Find("RocketFuel");
        resources.SetAmount(chemical, ExpantaNum.Zero);
        ExpantaNum fuelBefore = resources.GetAmount(fuel);
        Advance(offline);
        Near(resources.GetAmount(fuel), fuelBefore);
        Near(resources.GetAmount(chemical), ExpantaNum.Zero);
        resources.SetAmount(chemical, new ExpantaNum(10000d));
        ExpantaNum chemicalsBefore = resources.GetAmount(chemical);
        var before = SnapshotResources();
        Advance(offline);
        ExpantaNum fuelGain = resources.GetAmount(fuel) - fuelBefore;
        Assert.That(fuelGain, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(chemicalsBefore - resources.GetAmount(chemical), Is.GreaterThan(ExpantaNum.Zero));
        foreach (var input in works.ResourceConsumptionRates)
            Near(before[input.First] - resources.GetAmount(input.First),
                (chemicalsBefore - resources.GetAmount(chemical)) * input.Second / 4d);
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            Assert.That(resources.GetAmount(resource), Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void RemovedHydrogenHasNoRuntimeDefinitionAndProjectsAreUnique()
    {
        Assert.That(DataBase<ResourceDef>.Contains("Hydrogen"), Is.False);
        Assert.That(DataBase<BuildingDef>.Contains("ThunderGateHydrogenCollector"), Is.False);
        Assert.That(DataBase<SectorBuildingDef>.Find("ThunderGatePropellantWorks").MaxAmount, Is.EqualTo(1));
        Assert.That(DataBase<SectorBuildingDef>.Find("DawnDysonSphere").MaxAmount, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SuppliedDawnObservatoryStopsResearchWhileDisabledAndResumesWhenEnabled(bool offline)
    {
        SectorBuildingDef observatory = DataBase<SectorBuildingDef>.Find("DawnOrbitalObservatory");
        foreach (ResearchDef required in observatory.RequiredResearch) CompleteResearch(required);
        ProgressionModifierManager.Rebuild(new List<ResearchState>(research.States.Values));
        AddNeutralSupport();
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            resources.SetAmount(resource, new ExpantaNum(1e9d));
        Occupy(observatory.Sector);
        Advance(offline);
        ExpantaNum baseline = ResearchManager.CalculateResearchPower(
            new List<BuildingState>(buildings.States.Values), ResearchManager.BaseResearchPower);
        FundConstruction(observatory);
        Assert.That(buildings.TryBuild(observatory, ExpantaNum.One, out BuildFailure failure),
            Is.True, failure.ToString());
        var beforeOperation = SnapshotResources();
        Advance(offline);
        ExpantaNum suppliedPower = ResearchManager.CalculateResearchPower(
            new List<BuildingState>(buildings.States.Values), ResearchManager.BaseResearchPower);
        Assert.That(suppliedPower, Is.GreaterThan(baseline));
        Near(research.ResearchPower, suppliedPower);
        foreach (var maintenance in observatory.ResourceConsumptionRates)
            Assert.That(resources.GetAmount(maintenance.First), Is.LessThan(beforeOperation[maintenance.First]),
                "Operating the authored observatory must pay its real maintenance.");
        DisableAndAssertConstructionRetained(observatory);
        var whileDisabled = SnapshotResources();
        Advance(offline);
        Near(ResearchManager.CalculateResearchPower(new List<BuildingState>(buildings.States.Values),
            ResearchManager.BaseResearchPower), baseline);
        Near(research.ResearchPower, baseline);
        foreach (var maintenance in observatory.ResourceConsumptionRates)
            Near(resources.GetAmount(maintenance.First), whileDisabled[maintenance.First]);
        Assert.That(buildings.SetSectorBuildingEnabled(observatory, true), Is.True);
        Advance(offline);
        Near(research.ResearchPower, suppliedPower);
    }

    [Test]
    public void DysonStagesRejectMissingPrerequisitesAndInsufficientPaymentAtomically()
    {
        SectorBuildingDef dyson = PrepareDyson();
        WorkshopUpgradeDef fleet = DataBase<WorkshopUpgradeDef>.Find("DysonCollectorFleet");
        WorkshopUpgradeDef belt = DataBase<WorkshopUpgradeDef>.Find("DysonStellarBelt");
        Assert.That(fleet.RequiredSector, Is.SameAs(dyson.Sector));
        Assert.That(belt.RequiredSector, Is.SameAs(dyson.Sector));
        FundUpgrade(fleet);
        AssertPurchaseRejectedWithoutMutation(fleet, WorkshopPurchaseFailure.BuildingMissing);
        Occupy(dyson.Sector);
        AssertPurchaseRejectedWithoutMutation(fleet, WorkshopPurchaseFailure.BuildingMissing);
        FundConstruction(dyson);
        Assert.That(buildings.TryBuild(dyson, ExpantaNum.One, out BuildFailure buildFailure), Is.True, buildFailure.ToString());
        game.Sectors.GetState(dyson.Sector).SetOccupiedForEditor(false);
        AssertPurchaseRejectedWithoutMutation(fleet, WorkshopPurchaseFailure.SectorNotOccupied);
        Occupy(dyson.Sector);
        ResearchDef required = fleet.RequiredResearch[0];
        research.GetState(required).SetStatusForEditor(ResearchStatus.Available);
        AssertPurchaseRejectedWithoutMutation(fleet, WorkshopPurchaseFailure.ResearchIncomplete);
        CompleteResearch(required);
        FundUpgrade(belt);
        AssertPurchaseRejectedWithoutMutation(belt, WorkshopPurchaseFailure.UpgradeIncomplete);
        ResourceDef missing = fleet.ResourceRequirements[fleet.ResourceRequirements.Count - 1].First;
        resources.SetAmount(missing, ExpantaNum.Zero);
        AssertPurchaseRejectedWithoutMutation(fleet, WorkshopPurchaseFailure.ResourceInsufficient);
        FundUpgrade(fleet);
        Assert.That(workshop.TryPurchase(fleet, out WorkshopPurchaseFailure failure), Is.True, failure.ToString());
        AssertPurchaseRejectedWithoutMutation(fleet, WorkshopPurchaseFailure.AlreadyPurchased);
        game.Sectors.GetState(dyson.Sector).SetOccupiedForEditor(false);
        AssertPurchaseRejectedWithoutMutation(belt, WorkshopPurchaseFailure.SectorNotOccupied);
        Occupy(dyson.Sector);
        FundUpgrade(belt);
        DisableAndAssertConstructionRetained(dyson);
        Assert.That(workshop.TryPurchase(belt, out failure), Is.True, failure.ToString());
        Assert.That(buildings.GetState(dyson).IsEnabled, Is.False,
            "Buying a permanent stage must not start a disabled facility.");
        Near(buildings.GetState(dyson).Efficiency, ExpantaNum.Zero);
        Assert.That(buildings.SetSectorBuildingEnabled(dyson, true), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DysonEachPurchasedStageIncreasesRealPower(bool offline)
    {
        SectorBuildingDef dyson = PrepareDyson();
        AddNeutralSupport();
        foreach (ResourceDef resource in DataBase<ResourceDef>.All) resources.SetAmount(resource, new ExpantaNum(1e9d));
        Occupy(DataBase<SectorDef>.Find("HeliosCore"));
        FundConstruction(dyson);
        Assert.That(buildings.TryBuild(dyson, ExpantaNum.One, out BuildFailure buildFailure), Is.True, buildFailure.ToString());
        if (offline) simulation.AdvanceOffline(1d);
        else simulation.ManualTick(1d);
        ExpantaNum previous = game.State.PowerProductionRate;
        Assert.That(previous, Is.GreaterThan(ExpantaNum.Zero));
        foreach (string id in new[] { "DysonCollectorFleet", "DysonStellarBelt", "DysonControlNetwork" })
        {
            WorkshopUpgradeDef stage = DataBase<WorkshopUpgradeDef>.Find(id);
            FundUpgrade(stage);
            Assert.That(workshop.TryPurchase(stage, out WorkshopPurchaseFailure failure), Is.True, failure.ToString());
            if (offline) simulation.AdvanceOffline(1d);
            else simulation.ManualTick(1d);
            Assert.That(game.State.PowerProductionRate, Is.GreaterThan(previous), id);
            previous = game.State.PowerProductionRate;
        }
        Near(ProgressionModifierManager.Current.GetBuildingPowerProductionMultiplier(dyson), new ExpantaNum(23d));
    }

    [Test]
    public void DysonPurchasedStagesAndDisabledFacilitiesSurviveSaveAndResume()
    {
        saveRoot = Path.Combine(Application.dataPath, "..", "Temp", "PlanetDevelopment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
        SaveManager saves = root.AddComponent<SaveManager>();
        Assert.That(saves.LoadOrCreateGame(), Is.False);
        game.AdvanceTechLevelForEditor(TechLevel.Spacer);
        game.State.RestorePopulationForEditor(new ExpantaNum(1e6d));
        SectorBuildingDef dyson = PrepareDyson();
        SectorBuildingDef works = DataBase<SectorBuildingDef>.Find("ThunderGatePropellantWorks");
        string[] thunderStages = { "ThunderCryogenicSeparation", "ThunderParallelRefining", "ThunderOrbitalLoadingRing" };
        foreach (ResearchDef required in works.RequiredResearch) CompleteResearch(required);
        foreach (string id in thunderStages)
            foreach (ResearchDef required in DataBase<WorkshopUpgradeDef>.Find(id).RequiredResearch) CompleteResearch(required);
        Occupy(DataBase<SectorDef>.Find("HeliosCore"));
        Occupy(works.Sector);
        FundConstruction(works);
        Assert.That(buildings.TryBuild(works, ExpantaNum.One, out BuildFailure worksFailure), Is.True, worksFailure.ToString());
        FundConstruction(dyson);
        Assert.That(buildings.TryBuild(dyson, ExpantaNum.One, out BuildFailure failure), Is.True, failure.ToString());
        foreach (string id in new[] { "DysonCollectorFleet", "DysonStellarBelt", "DysonControlNetwork" })
        {
            WorkshopUpgradeDef stage = DataBase<WorkshopUpgradeDef>.Find(id);
            FundUpgrade(stage);
            Assert.That(workshop.TryPurchase(stage, out WorkshopPurchaseFailure purchaseFailure), Is.True, purchaseFailure.ToString());
        }
        foreach (string id in thunderStages)
        {
            WorkshopUpgradeDef stage = DataBase<WorkshopUpgradeDef>.Find(id);
            FundUpgrade(stage);
            Assert.That(workshop.TryPurchase(stage, out WorkshopPurchaseFailure purchaseFailure), Is.True, purchaseFailure.ToString());
        }
        ExpantaNum fuelMultiplier = ProgressionModifierManager.Current.GetBuildingProductionMultiplier(works);
        ExpantaNum logisticsMultiplier = ProgressionModifierManager.Current.GetBuildingLogisticsProductionMultiplier(works);
        Assert.That(fuelMultiplier, Is.GreaterThan(ExpantaNum.One));
        Assert.That(logisticsMultiplier, Is.GreaterThan(ExpantaNum.One));
        DisableAndAssertConstructionRetained(dyson);
        DisableAndAssertConstructionRetained(works);
        Assert.That(saves.SaveNow(true), Is.True);
        workshop.ResetForLoadForEditor();
        Assert.That(saves.LoadOrCreateGame(), Is.True);
        Near(buildings.GetState(dyson).Amount, ExpantaNum.One);
        Near(buildings.GetState(works).Amount, ExpantaNum.One);
        Assert.That(buildings.GetState(dyson).IsEnabled, Is.False);
        Assert.That(buildings.GetState(works).IsEnabled, Is.False);
        Near(buildings.GetState(dyson).Efficiency, ExpantaNum.Zero);
        Near(buildings.GetState(works).Efficiency, ExpantaNum.Zero);
        Assert.That(game.Sectors.GetState(works.Sector).Occupied, Is.True);
        foreach (string id in new[] { "DysonCollectorFleet", "DysonStellarBelt", "DysonControlNetwork" })
            Assert.That(workshop.IsPurchased(DataBase<WorkshopUpgradeDef>.Find(id)), Is.True);
        foreach (string id in thunderStages)
        {
            WorkshopUpgradeDef stage = DataBase<WorkshopUpgradeDef>.Find(id);
            Assert.That(workshop.IsPurchased(stage), Is.True);
            AssertPurchaseRejectedWithoutMutation(stage, WorkshopPurchaseFailure.AlreadyPurchased);
        }
        SaveManager.KingdomSaveData missingExploration = saves.CaptureSaveData();
        SaveManager.KingdomSaveData missingThunderExploration = saves.CaptureSaveData();
        var beforeResume = SnapshotResources();
        Assert.That(buildings.SetSectorBuildingEnabled(dyson, true), Is.True);
        Near(ProgressionModifierManager.Current.GetBuildingPowerProductionMultiplier(dyson), new ExpantaNum(23d));
        Assert.That(buildings.SetSectorBuildingEnabled(works, true), Is.True);
        AssertBalances(beforeResume);
        Near(ProgressionModifierManager.Current.GetBuildingProductionMultiplier(works), fuelMultiplier);
        Near(ProgressionModifierManager.Current.GetBuildingLogisticsProductionMultiplier(works), logisticsMultiplier);
        var beforeDuplicate = SnapshotResources();
        Assert.That(buildings.TryBuild(works, ExpantaNum.One, out failure), Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.BuildingLimitReached));
        AssertBalances(beforeDuplicate);
        Near(buildings.GetState(works).Amount, ExpantaNum.One);

        SaveManager.KingdomSaveData invalid = saves.CaptureSaveData();
        foreach (SaveManager.SectorStateSaveData sector in invalid.Sectors.States)
            if (sector.SectorId == "HeliosCore") sector.Occupied = false;
        Assert.Throws<InvalidOperationException>(() => saves.ApplySaveDataForEditor(invalid),
            "Purchased stellar stages must retain their required occupied sector.");
        foreach (SaveManager.SectorStateSaveData sector in missingExploration.Sectors.States)
            if (sector.SectorId == "HeliosCore")
            {
                sector.InteriorExplorationProgress = "0";
                sector.InteriorExplorationActive = false;
            }
        Assert.Throws<InvalidOperationException>(() => saves.ApplySaveDataForEditor(missingExploration),
            "Purchased stages retain their exploration prerequisite while their facility is disabled.");
        foreach (SaveManager.SectorStateSaveData sector in missingThunderExploration.Sectors.States)
            if (sector.SectorId == "ThunderGate")
            {
                sector.InteriorExplorationProgress = "0";
                sector.InteriorExplorationActive = false;
            }
        Assert.Throws<InvalidOperationException>(() => saves.ApplySaveDataForEditor(missingThunderExploration),
            "Purchased propellant structures retain their exploration prerequisite while the base is disabled.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ThunderStagesIncreaseActualFuelAndFinalLoadingCapacity(bool offline)
    {
        AddNeutralSupport();
        CompleteResearch(DataBase<ResearchDef>.Find("IndustrialWorkshop"));
        SectorBuildingDef works = DataBase<SectorBuildingDef>.Find("ThunderGatePropellantWorks");
        foreach (ResearchDef required in works.RequiredResearch) CompleteResearch(required);
        foreach (string id in new[] { "ThunderCryogenicSeparation", "ThunderParallelRefining", "ThunderOrbitalLoadingRing" })
            foreach (ResearchDef required in DataBase<WorkshopUpgradeDef>.Find(id).RequiredResearch) CompleteResearch(required);
        ProgressionModifierManager.Rebuild(new List<ResearchState>(research.States.Values));
        Occupy(works.Sector);
        foreach (ResourceDef resource in DataBase<ResourceDef>.All) resources.SetAmount(resource, new ExpantaNum(1e9d));
        FundConstruction(works);
        Assert.That(buildings.TryBuild(works, ExpantaNum.One, out BuildFailure failure), Is.True, failure.ToString());
        ResourceDef fuel = DataBase<ResourceDef>.Find("RocketFuel");
        ExpantaNum initialFuel = resources.GetAmount(fuel);
        Advance(offline);
        ExpantaNum previousGain = (resources.GetAmount(fuel) - initialFuel) / new ExpantaNum(60d);
        ExpantaNum initialLogistics = game.State.LogisticsConsumptionRate;
        foreach (string id in new[] { "ThunderCryogenicSeparation", "ThunderParallelRefining", "ThunderOrbitalLoadingRing" })
        {
            WorkshopUpgradeDef stage = DataBase<WorkshopUpgradeDef>.Find(id);
            FundUpgrade(stage);
            Assert.That(workshop.TryPurchase(stage, out WorkshopPurchaseFailure purchaseFailure), Is.True, purchaseFailure.ToString());
            ExpantaNum before = resources.GetAmount(fuel);
            Advance(offline);
            ExpantaNum gain = (resources.GetAmount(fuel) - before) / new ExpantaNum(60d);
            Assert.That(gain, Is.GreaterThan(previousGain), id);
            previousGain = gain;
        }
        Near(game.State.LogisticsConsumptionRate, initialLogistics);
        DisableAndAssertConstructionRetained(works);
        ExpantaNum fuelWhileDisabled = resources.GetAmount(fuel);
        Advance(offline);
        Assert.That(game.State.LogisticsConsumptionRate, Is.LessThan(initialLogistics));
        Near(resources.GetAmount(fuel), fuelWhileDisabled);
        Assert.That(buildings.SetSectorBuildingEnabled(works, true), Is.True);
        Advance(offline);
        Assert.That(resources.GetAmount(fuel), Is.GreaterThan(fuelWhileDisabled));
        Near(game.State.LogisticsConsumptionRate, initialLogistics);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void IceSeaArkSuppliesFoodAndResearchOnlyWhileMaintained(bool offline)
    {
        AddNeutralSupport();
        SectorBuildingDef ark = DataBase<SectorBuildingDef>.Find("AzureIceSeaArk");
        foreach (ResearchDef required in ark.RequiredResearch) CompleteResearch(required);
        Occupy(ark.Sector);
        foreach (ResourceDef resource in DataBase<ResourceDef>.All) resources.SetAmount(resource, new ExpantaNum(1e9d));
        Advance(offline);
        ExpantaNum baselineFood = game.State.FoodProductionRate;
        ExpantaNum baselineResearch = research.ResearchPower;
        FundConstruction(ark);
        Assert.That(buildings.TryBuild(ark, ExpantaNum.One, out BuildFailure failure), Is.True, failure.ToString());
        Advance(offline);
        Assert.That(game.State.FoodProductionRate, Is.GreaterThan(baselineFood));
        Assert.That(research.ResearchPower, Is.GreaterThan(baselineResearch));
        ResourceDef chemical = DataBase<ResourceDef>.Find("Chemical");
        resources.SetAmount(chemical, ExpantaNum.Zero);
        Advance(offline);
        Near(game.State.FoodProductionRate, baselineFood);
        Near(research.ResearchPower, baselineResearch);
        resources.SetAmount(chemical, new ExpantaNum(1e9d));
        Advance(offline);
        Assert.That(game.State.FoodProductionRate, Is.GreaterThan(baselineFood));
        Assert.That(research.ResearchPower, Is.GreaterThan(baselineResearch));
        DisableAndAssertConstructionRetained(ark);
        Advance(offline);
        Near(game.State.FoodProductionRate, baselineFood);
        Near(research.ResearchPower, baselineResearch);
        Assert.That(buildings.SetSectorBuildingEnabled(ark, true), Is.True);
        Advance(offline);
        Assert.That(game.State.FoodProductionRate, Is.GreaterThan(baselineFood));
        Assert.That(research.ResearchPower, Is.GreaterThan(baselineResearch));
    }

    private void DisableAndAssertConstructionRetained(SectorBuildingDef building)
    {
        BuildingState state = buildings.GetState(building);
        ExpantaNum amount = state.Amount;
        ExpantaNum territory = game.State.TerritoryUsed;
        ExpantaNum foodCapacity = game.State.FoodCapacity;
        ExpantaNum populationCapacity = game.State.Population.PopulationCapacity;
        var balances = SnapshotResources();
        if (BuildingManager.IsMegastructure(building))
        {
            Assert.That(buildings.GetDeconstructionPreview(building, ExpantaNum.One), Is.Null);
            Assert.That(buildings.TryDeconstruct(building, ExpantaNum.One, out BuildFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(BuildFailure.DeconstructionUnavailable));
        }
        else Assert.That(buildings.GetDeconstructionPreview(building, ExpantaNum.One), Is.Not.Null);
        Assert.That(buildings.SetSectorBuildingEnabled(building, false), Is.True);
        Assert.That(state.IsEnabled, Is.False);
        Near(state.Efficiency, ExpantaNum.Zero);
        Near(state.Amount, amount);
        Near(game.State.TerritoryUsed, territory);
        Near(game.State.FoodCapacity, foodCapacity);
        Near(game.State.Population.PopulationCapacity, populationCapacity);
        AssertBalances(balances);
    }

    private SectorBuildingDef PrepareDyson()
    {
        CompleteResearch(DataBase<ResearchDef>.Find("IndustrialWorkshop"));
        SectorBuildingDef dyson = DataBase<SectorBuildingDef>.Find("DawnDysonSphere");
        foreach (ResearchDef required in dyson.RequiredResearch) CompleteResearch(required);
        foreach (string id in new[] { "DysonCollectorFleet", "DysonStellarBelt", "DysonControlNetwork" })
            foreach (ResearchDef required in DataBase<WorkshopUpgradeDef>.Find(id).RequiredResearch) CompleteResearch(required);
        ProgressionModifierManager.Rebuild(new List<ResearchState>(research.States.Values));
        return dyson;
    }

    private void FundUpgrade(WorkshopUpgradeDef upgrade)
    {
        foreach (var cost in upgrade.ResourceRequirements) resources.AddAmount(cost.First, cost.Second);
    }

    private void AssertPurchaseRejectedWithoutMutation(WorkshopUpgradeDef upgrade, WorkshopPurchaseFailure expected)
    {
        var before = SnapshotResources();
        ExpantaNum multiplier = ProgressionModifierManager.Current.GetBuildingPowerProductionMultiplier(
            DataBase<SectorBuildingDef>.Find("DawnDysonSphere"));
        bool purchased = workshop.IsPurchased(upgrade);
        Assert.That(workshop.TryPurchase(upgrade, out WorkshopPurchaseFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(expected));
        Assert.That(workshop.GetPurchaseFailure(upgrade), Is.EqualTo(expected));
        AssertBalances(before);
        Assert.That(workshop.IsPurchased(upgrade), Is.EqualTo(purchased));
        Near(ProgressionModifierManager.Current.GetBuildingPowerProductionMultiplier(
            DataBase<SectorBuildingDef>.Find("DawnDysonSphere")), multiplier);
    }

    private void CompleteResearch(ResearchDef definition)
    {
        ResearchState state = research.GetState(definition);
        if (state.Status == ResearchStatus.Completed) return;
        foreach (ResearchDef prerequisite in definition.Prerequisites) CompleteResearch(prerequisite);
        foreach (var cost in definition.ResourceRequirements) resources.AddAmount(cost.First, cost.Second);
        Assert.That(ResearchManager.TryPayResearchCost(state), Is.True);
        state.SetStatusForEditor(ResearchStatus.Completed);
    }

    private void Occupy(SectorDef definition, bool completeInterior = true)
    {
        foreach (SectorDef prerequisite in definition.PrerequisiteSectors) Occupy(prerequisite);
        game.Sectors.GetState(definition).SetUnlockedForEditor(true);
        game.Sectors.GetState(definition).SetOccupiedForEditor(true);
        if (completeInterior) CompleteInteriorExploration(definition);
    }

    private void CompleteInteriorExploration(SectorDef definition)
    {
        var project = definition.InteriorExploration;
        if (project == null || game.Sectors.GetState(definition).InteriorExplorationCompleted) return;
        foreach (ResearchDef required in project.RequiredResearch) CompleteResearch(required);
        foreach (var cost in project.ResourceRatesPerSecond)
            resources.AddAmount(cost.First, cost.Second * project.DurationSeconds);
        Assert.That(game.Sectors.TryStartInteriorExploration(definition, out SectorOperationFailure failure), Is.True, failure.ToString());
        Assert.That(game.Sectors.TickActiveInteriorExploration(project.DurationSeconds.ToDouble() + 1d,
            game.State, resources, out failure), Is.True, failure.ToString());
        Assert.That(game.Sectors.GetState(definition).InteriorExplorationCompleted, Is.True);
    }

    private void FundConstruction(BuildingDef building)
    {
        foreach (var cost in building.ResourceRequirements)
            resources.AddAmount(cost.First, cost.Second * BuildingManager.GetConstructionCostMultiplierForEditor(building)
                * building.CostGrowth.Pow(new ExpantaNum(building is SectorBuildingDef sector ? sector.MaxAmount : 1)) * 10d);
    }

    private Dictionary<ResourceDef, ExpantaNum> SnapshotResources()
    {
        var snapshot = new Dictionary<ResourceDef, ExpantaNum>();
        foreach (ResourceDef resource in DataBase<ResourceDef>.All) snapshot.Add(resource, resources.GetAmount(resource));
        return snapshot;
    }

    private void AssertBalances(Dictionary<ResourceDef, ExpantaNum> expected)
    {
        foreach (var entry in expected) Near(resources.GetAmount(entry.Key), entry.Value);
    }

    private void Advance(bool offline)
    {
        if (offline) simulation.AdvanceOffline(60d);
        else simulation.ManualTick(60d);
    }

    private void AddNeutralSupport()
    {
        support = ScriptableObject.CreateInstance<BuildingDef>();
        support.SetIdForEditor("PlanetDevelopmentNeutralSupport");
        support.ConfigureEconomyForEditor(
            growth: 1.15d, territory: 0d, productivityConsumptionValue: 0d, productivity: 1e9d,
            populationCapacity: 1e9d, researchPower: 0d, foodProduction: 1e9d, foodConsumption: 0d,
            foodCapacity: 1e9d,
            powerContributionValue: 1e9d,
            logisticsContributionValue: 1e9d, attack: 0d, defense: 0d, manpower: 0d,
            requirements: new List<Pair<ResourceDef, ExpantaNum>>(), generation: new List<Pair<ResourceDef, ExpantaNum>>(),
            consumption: new List<Pair<ResourceDef, ExpantaNum>>());
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(support), ExpantaNum.One);
    }

    private static void Near(ExpantaNum actual, ExpantaNum expected)
    {
        Assert.That((actual - expected).Abs(), Is.LessThanOrEqualTo(ExpantaNum.Max(ExpantaNum.One, expected.Abs()) * 1e-7d));
    }
}

