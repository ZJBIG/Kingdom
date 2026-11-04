using System;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RuntimeHotPathPerformanceTests
{
    private GameObject host;
    private GameManager game;
    private BuildingManager buildings;
    private SimulationManager simulation;
    private ExpantaNum sink;

    [SetUp]
    public void SetUp()
    {
        ProgressionModifierManager.Rebuild(null);
        host = new GameObject("Runtime-Hot-Path-Test");
        game = host.AddComponent<GameManager>();
        host.AddComponent<ResourceManager>();
        buildings = host.AddComponent<BuildingManager>();
        host.AddComponent<ResearchManager>();
        simulation = host.AddComponent<SimulationManager>();
        foreach (BuildingDef definition in DataBase<BuildingDef>.All)
            buildings.EnsureBuilding(definition);
        foreach (SectorBuildingDef definition in DataBase<SectorBuildingDef>.All)
            buildings.EnsureBuilding(definition);
        SetAmount("WoodHouse", 5);
        SetAmount("Farm", 3);
        SetAmount("Quarry", 2);
        game.State.RestorePopulationCapacityExactForEditor(100, 0);
        game.State.RestorePopulationForEditor(10);
    }

    [TearDown]
    public void TearDown()
    {
        if (host != null) Object.DestroyImmediate(host);
        ProgressionModifierManager.Rebuild(null);
    }

    [Test]
    public void BuildingAggregatesMatchAllStatesAfterQuantityAndEnabledChanges()
    {
        AssertAggregates();
        SetAmount("Quarry", 0);
        AssertAggregates();
        SetAmount("Quarry", 4);
        AssertAggregates();
        Assert.That(DataBase<SectorBuildingDef>.All.Count, Is.GreaterThan(0));
        SectorBuildingDef sectorBuilding = DataBase<SectorBuildingDef>.All[0];
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(sectorBuilding), 1);
        AssertAggregates();
        Assert.That(buildings.SetSectorBuildingEnabled(sectorBuilding, false), Is.True);
        AssertAggregates();
        Assert.That(buildings.SetSectorBuildingEnabled(sectorBuilding, true), Is.True);
        AssertAggregates();
    }

    [Test]
    public void HappinessReadsObserveEveryFormulaInputChange()
    {
        GameState state = game.State;
        state.AdjustFoodRatesForEditor(100, 0);
        AssertHappiness(state);
        state.RestorePopulationForEditor(20);
        AssertHappiness(state);
        state.AdjustFoodRatesForEditor(50, 0);
        AssertHappiness(state);
        state.SetFoodAvailabilityForEditor(0.5d);
        AssertHappiness(state);
        state.SetFoodAvailabilityForEditor(1);
        AssertHappiness(state);
        foreach (ResearchDef definition in DataBase<ResearchDef>.All)
            foreach (ResearchEffectDefinition effect in definition.Effects)
                if (effect.Type == ResearchEffectType.HappinessBonus && effect.NumericValue > ExpantaNum.Zero)
                {
                    var completed = new ResearchState(definition);
                    completed.SetStatusForEditor(ResearchStatus.Completed);
                    ProgressionModifierManager.Rebuild(new[] { completed });
                    AssertHappiness(state);
                    ProgressionModifierManager.Rebuild(null);
                    AssertHappiness(state);
                    return;
                }
        Assert.Fail("The happiness bonus regression requires an authored happiness research.");
    }

    [Test]
    public void MeasureSparseBuildingAggregatesHappinessAndCompleteSimulationTicks()
    {
        TestContext.WriteLine($"[HotPathPerf] definitions={buildings.States.Count} fixture=WoodHouse5,Farm3,Quarry2 population=10");
        Measure("building-aggregates", 1000, () =>
        {
            sink = buildings.UsedProductivity + buildings.TotalProductivity +
                buildings.TotalPowerDemand + buildings.TotalLogisticsDemand;
        });
        Measure("happiness-reads", 10000, () => sink = game.State.HappinessMultiplier);
        Measure("manual-tick", 100, () => simulation.ManualTick(0.1d));
        Assert.That(sink.IsFinite, Is.True);
    }

    [Test]
    public void ResourceTickPreservesIdleBalancesAndResumesChangedFlows()
    {
        ResourceManager resources = ResourceManager.Instance;
        ResourceDef resource = DataBase<ResourceDef>.Find("StoneChunk");
        resources.SetProductionRate(resource, 0);
        resources.SetConsumptionRate(resource, 0);
        resources.SetAmount(resource, 123);
        ResourceState state = resources.GetState(resource);
        int version = state.Version;
        resources.Tick(1);
        AssertNear(state.Amount, 123);
        Assert.That(state.Version, Is.EqualTo(version), "An unchanged balance must preserve the discrete version.");
        resources.SetProductionRate(resource, 10);
        ExpantaNum expected = ResourceManager.AdvanceAmount(123,
            10 * game.State.HappinessRewardMultiplier, 0, 1);
        resources.Tick(1);
        AssertNear(state.Amount, expected);
        resources.SetProductionRate(resource, 0);
        resources.SetConsumptionRate(resource, 5);
        resources.Tick(1);
        AssertNear(state.Amount, expected - 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => resources.Tick(-1));
    }

    [TestCase(CampaignDoctrine.Surge)]
    [TestCase(CampaignDoctrine.Stable)]
    public void StrategicFoodMatchesFullPreviewForActiveOperations(CampaignDoctrine doctrine)
    {
        ConfigureStrategicWork(doctrine);
        ExpantaNum expected = game.Sectors.GetCampaignPreview(DataBase<SectorDef>.Find("AlphaCentauri"),
            game.State, ResourceManager.Instance).FoodCostPerSecond +
            game.Sectors.GetExplorationPreview(DataBase<SectorDef>.Find("HeliosCore"),
                game.State, ResourceManager.Instance).FoodCostPerSecond;
        AssertNear(buildings.StrategicFoodConsumptionRate, expected);
    }

    [Test]
    public void MeasureResourceTickAndStrategicFood()
    {
        ResourceManager.Instance.InitializeForEditor();
        TestContext.WriteLine($"[HotPathPerf] resource-definitions={ResourceManager.Instance.States.Count}");
        Measure("resource-tick-sparse", 1000, () => ResourceManager.Instance.Tick(0.1d));
        ConfigureStrategicWork(CampaignDoctrine.Surge);
        Measure("strategic-food-active-campaign", 1000, () => sink = buildings.StrategicFoodConsumptionRate);
    }

    private void ConfigureStrategicWork(CampaignDoctrine doctrine)
    {
        game.UltraProject.RestoreSaveDataForEditor(new UltraProjectStateSaveData
        {
            ProjectId = UltraProjectState.ProjectId,
            Doctrine = UltraProjectDoctrine.Stable,
            Status = UltraProjectStatus.Committed,
            CurrentStage = UltraProjectStage.Completed,
            StageProgress = "1",
            CompletedStages = new System.Collections.Generic.List<UltraProjectStage>
            {
                UltraProjectStage.Prototype, UltraProjectStage.Stabilization, UltraProjectStage.Expansion
            },
            LaunchFeePaid = true,
            StateVersion = 1
        });
        game.Sectors.InitializeDefinitions();
        SectorDef campaign = DataBase<SectorDef>.Find("AlphaCentauri");
        game.Sectors.GetState(campaign).SetCampaignActiveForEditor(true);
        game.Sectors.GetState(DataBase<SectorDef>.Find("HeliosCore")).SetColonizationActiveForEditor(true);
        game.State.BeginCampaignForEditor(campaign.Id);
        game.State.SetCampaignDoctrineForEditor(doctrine);
    }

    private void SetAmount(string id, double amount) =>
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(DataBase<BuildingDef>.Find(id)), amount);

    private void AssertAggregates()
    {
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        ExpantaNum used = ExpantaNum.Zero;
        ExpantaNum total = game.State.Population.Population * PopulationState.ProductivityGrantedPerPerson *
            modifiers.PopulationProductivityMultiplier + modifiers.ProductivityGranted;
        foreach (BuildingState state in buildings.States.Values)
            if (state.IsEnabled)
            {
                used += state.Amount * state.ProductivityConsumption;
                total += state.Amount * state.ProductivityGranted;
            }
        ExpantaNum labor = BuildingManager.CalculateLaborOperatingRatio(ExpantaNum.Max(0, total), ExpantaNum.Max(0, used));
        // This fixture starts no strategic work; its flow demand comes only from buildings.
        ExpantaNum power = ExpantaNum.Zero;
        ExpantaNum logistics = ExpantaNum.Zero;
        foreach (BuildingState state in buildings.States.Values)
            if (state.IsEnabled)
            {
                ExpantaNum scale = state.Amount * ExpantaNum.Clamp01(buildings.GlobalEfficiencyFactor) *
                    (state.ProductivityConsumption > ExpantaNum.Zero ? labor : ExpantaNum.One);
                power += scale * ExpantaNum.Max(0, -state.Definition.PowerContribution);
                logistics += scale * ExpantaNum.Max(0, -state.Definition.LogisticsContribution);
            }
        AssertNear(buildings.UsedProductivity, ExpantaNum.Max(0, used));
        AssertNear(buildings.TotalProductivity, ExpantaNum.Max(0, total));
        AssertNear(buildings.TotalPowerDemand, power);
        AssertNear(buildings.TotalLogisticsDemand, logistics);
    }

    private static void AssertHappiness(GameState state)
    {
        ExpantaNum expected = HappinessFormula.CalculateMultiplier(state.FoodNetRate,
            state.Population.Population, state.FoodAvailability, ProgressionModifierManager.Current.HappinessBonus);
        AssertNear(state.HappinessMultiplier, expected);
        AssertNear(state.HappinessConstraintMultiplier, HappinessFormula.CalculateConstraintMultiplier(expected));
        AssertNear(state.HappinessRewardMultiplier, HappinessFormula.CalculateRewardMultiplier(expected));
    }

    private static void AssertNear(ExpantaNum actual, ExpantaNum expected) =>
        Assert.That(actual.ToDouble(), Is.EqualTo(expected.ToDouble()).Within(1e-5d));

    private static void Measure(string label, int iterations, Action action)
    {
        for (int i = 0; i < 20; i++) action();
        var samples = new double[7];
        var allocations = new double[samples.Length];
        bool counterAvailable = GC.GetAllocatedBytesForCurrentThread() > 0;
        for (int sample = 0; sample < samples.Length; sample++)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++) action();
            timer.Stop();
            samples[sample] = timer.Elapsed.TotalMilliseconds / iterations;
            allocations[sample] = (GC.GetAllocatedBytesForCurrentThread() - allocatedBefore) / (double)iterations;
        }
        Array.Sort(samples);
        Array.Sort(allocations);
        string allocated = counterAvailable ? allocations[allocations.Length / 2].ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "NA";
        TestContext.WriteLine($"[HotPathPerf] {label} median_ms={samples[samples.Length / 2]:F6} median_alloc_bytes={allocated} iterations={iterations} batches={samples.Length}");
    }
}
