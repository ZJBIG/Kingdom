using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class InvestmentChoiceTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void TightProductivitySupportsDeferringOrPartiallyUpgradingFarms(bool upgradeOne)
    {
        using var f = new Fixture();
        f.Complete(DataBase<Research>.Find("Agriculture"));
        f.Game.State.Population.RestorePopulationForEditor(new ExpantaNum(6));
        f.Game.State.Population.AdjustPopulationCapacityForEditor(new ExpantaNum(6));
        Building farm = DataBase<Building>.Find("Farm");
        Assert.That(f.Buildings.TryBuild(farm, new ExpantaNum(2), out _), Is.True);
        f.Game.State.AdvanceTechLevelForEditor(TechLevel.StoneAge);
        f.Complete(DataBase<Research>.Find("IrrigationEngineering"));
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
            Resource wood = DataBase<Resource>.Find("WoodLog");
            ExpantaNum stock = f.Resources.GetAmount(wood);
            Assert.That(f.Buildings.TryUpgrade(farm, ExpantaNum.One, out BuildFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(BuildFailure.ProductivityInsufficient));
            Near(f.Resources.GetAmount(wood), stock);
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
        f.Complete(DataBase<Research>.Find("Agriculture"));
        Building house = DataBase<Building>.Find("WoodHouse");
        Assert.That(f.Buildings.TryBuild(house, ExpantaNum.One, out _), Is.True);
        f.Game.State.Population.RestorePopulationForEditor(new ExpantaNum(5));
        Assert.That(f.Buildings.TryBuild(DataBase<Building>.Find("Farm"), ExpantaNum.One, out _), Is.True);
        f.Game.State.AdvanceTechLevelForEditor(TechLevel.StoneAge);
        f.Complete(DataBase<Research>.Find("Masonry"));
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
    public void KnowledgeInvestmentAndDirectEraAdvanceKeepPaymentSeparateFromResearchWork(bool knowledgeFirst)
    {
        using var f = new Fixture();
        Research knowledge = DataBase<Research>.Find("KnowledgeSharing");
        Research era = DataBase<Research>.Find("StoneAgeSettlement");
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
            for (int i = 0; i < 1000 && !f.Research.IsResearchCompleted(knowledge.Id); i++)
                f.Simulation.ManualTick(1d);
            Assert.That(f.Research.IsResearchCompleted(knowledge.Id), Is.True);
            Assert.That(f.Research.ActiveResearch, Is.SameAs(eraState));
            Near(eraState.Progress, ExpantaNum.Zero);
            Assert.That(f.Research.CurrentResearchSpeed, Is.GreaterThan(initialSpeed));
            f.Simulation.ManualTick(1d);
        }
        Assert.That(eraState.Progress, Is.GreaterThan(ExpantaNum.Zero));
        foreach (var cost in era.ResourceRequirements)
            Near(eraState.GetPaidResourceCost(cost.First), cost.Second);
        f.Simulation.ManualTick(10000d);
        Assert.That(f.Research.IsResearchCompleted(era.Id), Is.True);
        Assert.That(f.Game.State.TechLevel, Is.EqualTo(TechLevel.StoneAge)); // Era protocol.
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
            foreach (Resource resource in DataBase<Resource>.All)
                Resources.SetAmount(resource, new ExpantaNum(1000000));
        }
        public void CompletePrerequisites(Research definition)
        {
            foreach (Research prerequisite in definition.Prerequisites) Complete(prerequisite);
        }
        public void Complete(Research definition)
        {
            CompletePrerequisites(definition);
            var paid = new Dictionary<Resource, ExpantaNum>();
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
