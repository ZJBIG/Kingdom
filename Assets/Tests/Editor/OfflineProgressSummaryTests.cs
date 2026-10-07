using System;
using NUnit.Framework;
using UnityEngine;

public sealed class OfflineProgressSummaryTests
{
    private GameObject host;
    private GameManager game;
    private ResourceManager resources;
    private BuildingManager buildings;
    private SaveManager save;

    [SetUp]
    public void SetUp()
    {
        ProgressionModifierManager.Rebuild(null);
        host = new GameObject("OfflineSummaryTestManagers");
        resources = host.AddComponent<ResourceManager>();
        if (resources.States.Count == 0) resources.InitializeForEditor();
        game = host.AddComponent<GameManager>();
        buildings = host.AddComponent<BuildingManager>();
        ResearchManager research = host.AddComponent<ResearchManager>();
        if (research.States.Count == 0) research.InitializeForEditor();
        host.AddComponent<WorkshopManager>().CaptureSaveData();
        host.AddComponent<SimulationManager>();
        save = host.AddComponent<SaveManager>();
        game.InitializeNewGameForEditor();
        game.State.RestorePopulationForEditor(new ExpantaNum(2));
        game.State.RestorePopulationCapacityExactForEditor(new ExpantaNum(2), ExpantaNum.Zero);
        Building building = DataBase<Building>.Find("Lumberyard");
        Assert.That(building, Is.Not.Null);
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(building), ExpantaNum.One);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(host);
        ProgressionModifierManager.Rebuild(null);
    }

    [Test]
    public void ActualSettlement_CopiesResourceFoodAndPopulationDifferences()
    {
        Resource wood = DataBase<Resource>.Find(ResourceManager.StartingResourceId);
        ExpantaNum before = resources.GetAmount(wood);
        ExpantaNum foodBefore = game.State.FoodAmount;
        ExpantaNum populationBefore = game.State.Population.Population;
        Assert.That(save.ApplyOfflineProgressForEditor(100, 110), Is.True);
        OfflineProgressSummary summary = save.LastOfflineSummary;
        Assert.That(summary, Is.Not.Null);
        OfflineResourceChange observed = Find(summary, wood);
        AssertNear(observed.Before, before);
        AssertNear(observed.After, resources.GetAmount(wood));
        Assert.That(observed.Change, Is.GreaterThan(ExpantaNum.Zero));
        AssertNear(summary.FoodBefore, foodBefore);
        AssertNear(summary.FoodAfter, game.State.FoodAmount);
        AssertNear(summary.PopulationBefore, populationBefore);
        AssertNear(summary.PopulationAfter, game.State.Population.Population);
        SimulationManager.Instance.ManualTick(1d);
        Assert.That(resources.GetAmount(wood), Is.GreaterThan(observed.After));
        AssertNear(Find(summary, wood).After, observed.After);
    }

    [Test]
    public void CapAndFoodOverflow_ReportActualStockAndSeparateFullAbsence()
    {
        save.SetMaximumOfflineHoursForEditor(1f / 3600f);
        game.State.RestoreCoreForEditor(0, TechLevel.Animal, game.State.FoodCapacity, 100);
        Assert.That(save.ApplyOfflineProgressForEditor(100, 200), Is.True);
        OfflineProgressSummary summary = save.LastOfflineSummary;
        Assert.That(summary.WallClockSeconds, Is.EqualTo(100d).Within(1e-6));
        Assert.That(summary.SettledSeconds, Is.EqualTo(1d).Within(1e-5));
        AssertNear(summary.FoodAfter, summary.FoodBefore);
        Assert.That(summary.FoodAtCapacity, Is.True);
        Assert.That(game.State.LastSaveUnixSeconds, Is.EqualTo(200L), "Committed timestamp is a protocol integer.");
    }

    [Test]
    public void ZeroOrDuplicateTimestamp_ClearsPreviousSessionSummary()
    {
        Assert.That(save.ApplyOfflineProgressForEditor(100, 101), Is.True);
        Assert.That(save.LastOfflineSummary, Is.Not.Null);
        Assert.That(save.ApplyOfflineProgressForEditor(101, 101), Is.False);
        Assert.That(save.LastOfflineSummary, Is.Null);
        Assert.That(save.LastOfflineProgressSeconds, Is.EqualTo(0d), "No settlement is an exact empty-window sentinel.");
        Assert.That(save.ApplyOfflineProgressForEditor(0, 102), Is.False);
        Assert.That(save.LastOfflineSummary, Is.Null);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ActualResearchSettlement_ReportsOnlyCompletedResearchAndEndMaterialBlocker(bool supplied)
    {
        ResearchManager manager = ResearchManager.Instance;
        Research definition = DataBase<Research>.Find("ControlledFire");
        Assert.That(definition, Is.Not.Null);
        if (!supplied)
        {
            Building lumberyard = DataBase<Building>.Find("Lumberyard");
            buildings.SetAmountAndRatesForEditor(buildings.GetState(lumberyard), ExpantaNum.Zero);
            resources.SetAmount(DataBase<Resource>.Find(ResourceManager.StartingResourceId), ExpantaNum.Zero);
        }
        Assert.That(manager.EnqueueResearch(definition), Is.True);
        ResearchState research = manager.GetState(definition);
        if (supplied)
        {
            Assert.That(manager.ActiveResearch, Is.SameAs(research));
            Assert.That(research.CostPaid, Is.True);
            research.SetProgressForEditor(research.BaseCost - new ExpantaNum("0.01"));
        }
        else
            Assert.That(research.Status, Is.EqualTo(ResearchStatus.WaitingResources));
        Assert.That(save.ApplyOfflineProgressForEditor(100, 101), Is.True);
        OfflineProgressSummary summary = save.LastOfflineSummary;
        if (supplied)
        {
            Assert.That(research.Status, Is.EqualTo(ResearchStatus.Completed));
            Assert.That(summary.CompletedResearches, Has.Count.EqualTo(1), "One actual completion is a discrete count.");
            Assert.That(summary.CompletedResearches[0], Is.SameAs(definition));
            Assert.That(summary.WaitingResearch, Is.Null);
            Assert.That(save.ApplyOfflineProgressForEditor(101, 102), Is.True);
            Assert.That(save.LastOfflineSummary.CompletedResearches, Is.Empty, "Previously completed research is excluded.");
        }
        else
        {
            Assert.That(summary.CompletedResearches, Is.Empty);
            Assert.That(summary.WaitingResearch, Is.SameAs(definition));
            Assert.That(research.CostPaid, Is.False);
        }
    }

    private static OfflineResourceChange Find(OfflineProgressSummary summary, Resource resource)
    {
        foreach (OfflineResourceChange change in summary.Resources)
            if (change.Resource == resource) return change;
        throw new InvalidOperationException("Expected resource difference was not captured.");
    }

    private static void AssertNear(ExpantaNum actual, ExpantaNum expected) =>
        Assert.That((actual - expected).Abs().ToDouble(), Is.LessThan(1e-6));
}
