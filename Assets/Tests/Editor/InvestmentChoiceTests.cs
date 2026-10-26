using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class InvestmentChoiceTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void PartiallyStockedTickStartsStarvationOnlyAfterFoodRunsOut(bool offline)
    {
        double automaticProgress;
        using (var f = new Fixture())
        {
            PreparePartiallyStockedPopulation(f);
            if (offline) f.Simulation.AdvanceOffline(60d);
            else f.Simulation.ManualTick(60d);
            automaticProgress = f.Game.State.Population.PopulationChangeProgress.ToDouble();
            // Baseline food5 versus demand8: stock covers30s. Only the other30s
            // accrue departure at shortage3/8. ExpantaNum rounds each rate to
            // 1e-6; 30s accumulates up to15e-6 error plus intermediate rounding.
            Assert.That(automaticProgress,
                Is.EqualTo((3d / 8d) * 10d * 30d / 3600d).Within(2e-5d));
            Near(f.Game.State.Population.Population, new ExpantaNum(10));
            Assert.That(f.Game.State.FoodAmount.ToDouble(), Is.InRange(0d, 1e-7d));
        }
        using (var f = new Fixture())
        {
            PreparePartiallyStockedPopulation(f);
            if (offline) f.Simulation.AdvanceOffline(30d);
            else f.Simulation.ManualTick(30d);
            Assert.That(f.Game.State.Population.PopulationChangeProgress.ToDouble(), Is.InRange(0d, 1e-7d),
                "The full first30 seconds are fed and must not accrue any starvation progress.");
            Assert.That(f.Game.State.FoodAmount.ToDouble(), Is.InRange(0d, 1e-7d));
            if (offline) f.Simulation.AdvanceOffline(30d);
            else f.Simulation.ManualTick(30d);
            Assert.That(f.Game.State.Population.PopulationChangeProgress.ToDouble(),
                Is.EqualTo(automaticProgress).Within(1e-6d),
                "A single60s tick must match an explicit fed30s + unfed30s sequence.");
        }
    }

    private static void PreparePartiallyStockedPopulation(Fixture f)
    {
        f.Game.State.RestoreCoreForEditor(0, TechLevel.Animal, new ExpantaNum(90), 0L);
        f.Game.State.RestorePopulationForEditor(new ExpantaNum(10));
        f.Game.State.RestorePopulationCapacityExactForEditor(new ExpantaNum(10), ExpantaNum.Zero);
    }

    [Test]
    public void OfflinePopulationUsesReducedEffectiveTimeThroughTheSameForwardSimulation()
    {
        ExpantaNum offlinePopulation;
        using (var f = new Fixture())
        {
            PrepareLargeHungryPopulation(f);
            f.Simulation.AdvanceOffline(7800d);
            offlinePopulation = f.Game.State.Population.Population;
            Assert.That(f.Game.State.CalendarDays, Is.EqualTo(780)); // Discrete ten-second calendar days.
        }
        using (var f = new Fixture())
        {
            PrepareLargeHungryPopulation(f);
            for (int i = 0; i < 120; i++) f.Simulation.ManualTick(60d);
            // Past two hours, ten offline minutes contribute six simulation minutes.
            for (int i = 0; i < 10; i++) f.Simulation.ManualTick(36d);
            Assert.That(f.Game.State.Population.Population,
                Is.GreaterThan(ExpantaNum.Zero).And.LessThan(new ExpantaNum("1e12")));
            Near(f.Game.State.Population.Population, offlinePopulation);
            Assert.That(f.Game.State.CalendarDays, Is.EqualTo(756));
        }
    }

    private static void PrepareLargeHungryPopulation(Fixture f)
    {
        f.Game.State.RestoreCoreForEditor(0, TechLevel.Animal, ExpantaNum.Zero, 0L);
        f.Game.State.RestorePopulationForEditor(new ExpantaNum("1e12"));
        f.Game.State.RestorePopulationCapacityExactForEditor(new ExpantaNum("1e12"), ExpantaNum.Zero);
    }

    [Test]
    public void HungryFarmKeepsProducingButStillNeedsLabor()
    {
        using var f = new Fixture();
        f.Complete(DataBase<ResearchDef>.Find("Agriculture"));
        f.Game.State.RestoreCoreForEditor(0, TechLevel.Animal, ExpantaNum.Zero, 0L);
        f.Game.State.RestorePopulationForEditor(new ExpantaNum(20));
        f.Game.State.RestorePopulationCapacityExactForEditor(new ExpantaNum(20), ExpantaNum.Zero);
        BuildingDef farm = DataBase<BuildingDef>.Find("Farm");
        Assert.That(f.Buildings.TryBuild(farm, ExpantaNum.One, out _), Is.True);
        f.Simulation.ManualTick(1d);
        Assert.That(f.Game.State.HappinessConstraintMultiplier, Is.LessThan(ExpantaNum.One));
        Assert.That(f.Buildings.GetState(farm).Efficiency, Is.GreaterThanOrEqualTo(ExpantaNum.One));

        f.Game.State.RestorePopulationForEditor(ExpantaNum.One);
        f.Simulation.ManualTick(1d);
        Assert.That(f.Buildings.GetState(farm).Efficiency,
            Is.GreaterThan(ExpantaNum.Zero).And.LessThan(ExpantaNum.One));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StockedFoodProtectsResidentsAndActualStarvationReducesThemSlowly(bool offline)
    {
        using var f = new Fixture();
        f.Game.State.RestoreCoreForEditor(0, TechLevel.Animal, new ExpantaNum(1000), 0L);
        f.Game.State.RestorePopulationForEditor(new ExpantaNum(20));
        f.Game.State.RestorePopulationCapacityExactForEditor(new ExpantaNum(20), ExpantaNum.Zero);
        void Advance(double seconds)
        {
            if (offline) f.Simulation.AdvanceOffline(seconds);
            else f.Simulation.ManualTick(seconds);
        }
        Advance(30d);
        Assert.That(f.Game.State.FoodNetRate, Is.LessThan(ExpantaNum.Zero));
        Assert.That(f.Game.State.FoodAmount, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(f.Game.State.HappinessConstraintMultiplier, Is.GreaterThanOrEqualTo(ExpantaNum.One));
        Near(f.Game.State.Population.Population, new ExpantaNum(20));

        f.Game.State.RestoreCoreForEditor(0, TechLevel.Animal, ExpantaNum.Zero, 0L);
        Advance(1d);
        Near(f.Game.State.Population.Population, new ExpantaNum(20));
        for (int i = 0; i < 120; i++) Advance(30d);
        Assert.That(f.Game.State.Population.Population,
            Is.GreaterThan(ExpantaNum.Zero).And.LessThan(new ExpantaNum(20)));
        Assert.That(f.Game.State.FoodAmount, Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TightProductivityAllowsFarmUpgradesWithReducedOperation(bool upgradeOne)
    {
        using var f = new Fixture();
        f.Complete(DataBase<ResearchDef>.Find("Agriculture"));
        f.Game.State.Population.RestorePopulationForEditor(new ExpantaNum(6));
        f.Game.State.Population.AdjustPopulationCapacityForEditor(new ExpantaNum(6));
        BuildingDef farm = DataBase<BuildingDef>.Find("Farm");
        Assert.That(f.Buildings.TryBuild(farm, new ExpantaNum(2), out _), Is.True);
        f.Game.State.AdvanceTechLevelForEditor(TechLevel.StoneAge);
        f.Complete(DataBase<ResearchDef>.Find("IrrigationEngineering"));
        f.Simulation.ManualTick(0d);
        ExpantaNum production = f.Game.State.FoodProductionRate;
        ExpantaNum available = f.Buildings.AvailableProductivity;
        if (upgradeOne)
        {
            Assert.That(f.Buildings.TryUpgrade(farm, ExpantaNum.One, out _), Is.True);
            f.Simulation.ManualTick(0d);
            Assert.That(f.Game.State.FoodProductionRate, Is.GreaterThan(production));
            Assert.That(f.Buildings.AvailableProductivity, Is.LessThan(available));
            Assert.That(f.Buildings.GetState(farm).Amount, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(f.Buildings.TryUpgrade(farm, ExpantaNum.One, out BuildFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(BuildFailure.None));
            f.Simulation.ManualTick(0d);
            Assert.That(f.Buildings.LaborOperatingRatio,
                Is.GreaterThan(ExpantaNum.Zero).And.LessThan(ExpantaNum.One));
            Assert.That(f.Game.State.FoodProductionRate, Is.GreaterThan(production));
        }
        else
        {
            Assert.That(f.Buildings.EnsureBuilding(farm.UpgradeTo).Amount, Is.LessThan(ExpantaNum.One));
            Assert.That(f.Game.State.FoodNetRate, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(f.Buildings.AvailableProductivity, Is.GreaterThan(ExpantaNum.Zero));
        }
    }

    [Test]
    public void HousingUpgradeGrantsCapacityBeforeResidentsAndTheirFoodBurden()
    {
        using var f = new Fixture();
        f.Complete(DataBase<ResearchDef>.Find("Agriculture"));
        BuildingDef house = DataBase<BuildingDef>.Find("WoodHouse");
        Assert.That(f.Buildings.TryBuild(house, ExpantaNum.One, out _), Is.True);
        f.Game.State.Population.RestorePopulationForEditor(new ExpantaNum(5));
        Assert.That(f.Buildings.TryBuild(DataBase<BuildingDef>.Find("Farm"), ExpantaNum.One, out _), Is.True);
        f.Game.State.AdvanceTechLevelForEditor(TechLevel.StoneAge);
        f.Complete(DataBase<ResearchDef>.Find("Masonry"));
        f.Simulation.ManualTick(0d);
        ExpantaNum population = f.Game.State.Population.Population;
        ExpantaNum capacity = f.Game.State.Population.PopulationCapacity;
        ExpantaNum productivity = f.Buildings.TotalProductivity;
        ExpantaNum netFood = f.Game.State.FoodNetRate;
        Assert.That(f.Buildings.TryUpgrade(house, ExpantaNum.One, out _), Is.True);
        Assert.That(f.Game.State.Population.PopulationCapacity, Is.GreaterThan(capacity));
        Near(f.Game.State.Population.Population, population);
        Near(f.Buildings.TotalProductivity, productivity);
        Near(f.Game.State.FoodNetRate, netFood);
        for (int i = 0; i < 100; i++) f.Simulation.ManualTick(10d);
        Assert.That(f.Game.State.Population.Population, Is.GreaterThan(population));
        Assert.That(f.Buildings.TotalProductivity, Is.GreaterThan(productivity));
        Assert.That(f.Game.State.FoodNetRate, Is.LessThan(netFood));
        Assert.That(f.Game.State.FoodNetRate, Is.GreaterThan(ExpantaNum.Zero));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void KnowledgeInvestmentAndSettlementKeepPaymentSeparateFromResearchWork(bool knowledgeFirst)
    {
        using var f = new Fixture();
        ResearchDef knowledge = DataBase<ResearchDef>.Find("KnowledgeSharing");
        ResearchDef era = DataBase<ResearchDef>.Find("StoneAgeSettlement");
        f.CompletePrerequisites(knowledge);
        f.CompletePrerequisites(era);
        ProgressionModifierManager.Rebuild(new List<ResearchState>(f.Research.States.Values));
        ResearchState eraState = f.Research.GetState(era);
        f.Research.PayResearchCost(era);
        Assert.That(eraState.CostPaid, Is.True);
        Near(eraState.Progress, ExpantaNum.Zero);
        Assert.That(f.Research.EnqueueResearch(era), Is.True);
        ExpantaNum initialSpeed = f.Research.CurrentResearchSpeed;
        if (knowledgeFirst)
        {
            Assert.That(f.Research.RemoveQueuedResearch(era), Is.True);
            Assert.That(f.Research.EnqueueResearch(knowledge), Is.True);
            Assert.That(f.Research.EnqueueResearch(era), Is.True);
        }
        f.Simulation.ManualTick(1d);
        Assert.That(eraState.CostPaid, Is.True);
        if (knowledgeFirst)
        {
            Near(eraState.Progress, ExpantaNum.Zero);
            Assert.That(f.Research.GetState(knowledge).Progress, Is.GreaterThan(ExpantaNum.Zero));
            double tick = f.Simulation.TickIntervalSecondsForEditor;
            for (int i = 0; i < Math.Ceiling(1000d / tick) && !f.Research.IsResearchCompleted(knowledge.Id); i++)
                f.Simulation.ManualTick(tick);
            Assert.That(f.Research.IsResearchCompleted(knowledge.Id), Is.True);
            Assert.That(f.Research.ActiveResearch, Is.SameAs(eraState));
            Near(eraState.Progress, ExpantaNum.Zero); // The completion tick cannot also advance the next research.
            Assert.That(f.Research.CurrentResearchSpeed, Is.GreaterThan(initialSpeed));
            f.Simulation.ManualTick(1d);
        }
        Assert.That(eraState.Progress, Is.GreaterThan(ExpantaNum.Zero));
        foreach (var cost in era.ResourceRequirements)
            Near(eraState.GetPaidResourceCost(cost.First), cost.Second);
        f.Simulation.ManualTick(10000d);
        Assert.That(f.Research.IsResearchCompleted(era.Id), Is.True);
        Assert.That(f.Game.State.TechLevel, Is.EqualTo(TechLevel.Animal)); // Settlement is no longer an era transition.
    }

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject root = new GameObject("InvestmentChoices");
        public readonly GameManager Game;
        public readonly ResourceManager Resources;
        public readonly BuildingManager Buildings;
        public readonly ResearchManager Research;
        public readonly SimulationManager Simulation;
        public Fixture()
        {
            ProgressionModifierManager.Rebuild(null);
            Resources = root.AddComponent<ResourceManager>();
            Game = root.AddComponent<GameManager>();
            Buildings = root.AddComponent<BuildingManager>();
            Research = root.AddComponent<ResearchManager>();
            Simulation = root.AddComponent<SimulationManager>();
            Research.InitializeForEditor();
            Game.State.RestoreCoreForEditor(0, TechLevel.Animal, new ExpantaNum(300), 0L);
            foreach (ResourceDef resource in DataBase<ResourceDef>.All)
                Resources.SetAmount(resource, new ExpantaNum(1000000));
        }
        public void CompletePrerequisites(ResearchDef definition)
        {
            foreach (ResearchDef prerequisite in definition.Prerequisites) Complete(prerequisite);
        }
        public void Complete(ResearchDef definition)
        {
            CompletePrerequisites(definition);
            var paid = new Dictionary<ResourceDef, ExpantaNum>();
            foreach (var cost in definition.ResourceRequirements) paid[cost.First] = cost.Second;
            Research.GetState(definition).RestoreForEditor(definition.BaseCost, true, true, paid);
        }
        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(root);
            ProgressionModifierManager.Rebuild(null);
        }
    }
    private static void Near(ExpantaNum actual, ExpantaNum expected)
    {
        Assert.That((actual - expected).Abs(), Is.LessThanOrEqualTo(
            ExpantaNum.Max(new ExpantaNum(0.000001), expected.Abs() * 0.00000001)));
    }
}
