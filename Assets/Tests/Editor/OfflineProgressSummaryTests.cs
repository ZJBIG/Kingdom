using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class OfflineProgressSummaryTests
{
    private GameObject host;
    private GameManager game;
    private ResourceManager resources;
    private BuildingManager buildings;
    private SaveManager save;
    private TutorialManager initialTutorial;

    [SetUp]
    public void SetUp()
    {
        initialTutorial = UnityEngine.Object.FindObjectOfType<TutorialManager>(true);
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
        BuildingDef building = DataBase<BuildingDef>.Find("Lumberyard");
        Assert.That(building, Is.Not.Null);
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(building), ExpantaNum.One);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(host);
        TutorialManager tutorial = UnityEngine.Object.FindObjectOfType<TutorialManager>(true);
        if (tutorial != null && tutorial != initialTutorial) UnityEngine.Object.DestroyImmediate(tutorial.gameObject);
        ProgressionModifierManager.Rebuild(null);
    }

    [Test]
    public void CompletedTerritoryRewards_AreNotReappliedAfterSavingAndReloading()
    {
        ResearchManager research = host.GetComponent<ResearchManager>();
        CompleteResearchPrerequisites(DataBase<ResearchDef>.Find("FeudalAdministration"), research);
        SaveManager.KingdomSaveData data = save.CaptureSaveData();
        data.General.TechLevel = TechLevel.Medieval;
        data.General.HasCurrentFeudalTerritoryReward = true;
        // The saved total already contains research and one-time territory rewards.
        data.General.TerritoryTotal = "9000";
        for (int reload = 0; reload < 3; reload++)
        {
            save.ApplySaveDataForEditor(data);
            AssertNear(game.State.TerritoryTotal, new ExpantaNum(9000d));
            data = JsonUtility.FromJson<SaveManager.KingdomSaveData>(JsonUtility.ToJson(save.CaptureSaveData()));
        }
    }

    private static void CompleteResearchPrerequisites(ResearchDef definition, ResearchManager research)
    {
        ResearchState state = research.GetState(definition);
        if (state.Status == ResearchStatus.Completed) return;
        foreach (ResearchDef prerequisite in definition.Prerequisites)
            CompleteResearchPrerequisites(prerequisite, research);
        var paid = new Dictionary<ResourceDef, ExpantaNum>();
        foreach (Pair<ResourceDef, ExpantaNum> cost in definition.ResourceRequirements)
            paid.Add(cost.First, cost.Second);
        state.RestoreForEditor(state.BaseCost, true, true, paid);
    }

    [Test]
    public void ExistingFeudalReward_IsCompensatedOnceAndPersistsItsReceipt()
    {
        CompleteResearchPrerequisites(DataBase<ResearchDef>.Find("FeudalAdministration"), ResearchManager.Instance);
        SaveManager.KingdomSaveData data = save.CaptureSaveData();
        data.General.TechLevel = TechLevel.Medieval;
        data.General.TerritoryTotal = "9000";
        string oldJson = JsonUtility.ToJson(data).Replace("\"HasCurrentFeudalTerritoryReward\":false,", string.Empty);
        Assert.That(oldJson, Does.Not.Contain("HasCurrentFeudalTerritoryReward"));
        save.ApplySaveDataForEditor(JsonUtility.FromJson<SaveManager.KingdomSaveData>(oldJson));
        AssertNear(game.State.TerritoryTotal, new ExpantaNum(11175d));
        Assert.That(game.State.HasCurrentFeudalTerritoryReward, Is.True);
        for (int reload = 0; reload < 3; reload++)
        {
            data = JsonUtility.FromJson<SaveManager.KingdomSaveData>(JsonUtility.ToJson(save.CaptureSaveData()));
            Assert.That(data.General.HasCurrentFeudalTerritoryReward, Is.True);
            save.ApplySaveDataForEditor(data);
            AssertNear(game.State.TerritoryTotal, new ExpantaNum(11175d));
        }
    }

    [Test]
    public void NewFeudalCompletion_Grants2200AndDoesNotReceiveTheOldSaveCompensation()
    {
        ResearchManager research = ResearchManager.Instance;
        ResearchDef feudal = DataBase<ResearchDef>.Find("FeudalAdministration");
        foreach (ResearchDef prerequisite in feudal.Prerequisites) CompleteResearchPrerequisites(prerequisite, research);
        SaveManager.KingdomSaveData data = save.CaptureSaveData();
        data.General.TechLevel = TechLevel.StoneAge;
        save.ApplySaveDataForEditor(data);
        ExpantaNum before = game.State.TerritoryTotal;
        Assert.That(game.State.HasCurrentFeudalTerritoryReward, Is.False);
        foreach (Pair<ResourceDef, ExpantaNum> cost in feudal.ResourceRequirements)
            resources.SetAmount(cost.First, cost.Second * 2d);
        Assert.That(research.EnqueueResearch(feudal), Is.True);
        research.TickOfflineForEditor(1000000d);
        Assert.That(research.GetState(feudal).Status, Is.EqualTo(ResearchStatus.Completed));
        AssertNear(game.State.TerritoryTotal - before, new ExpantaNum(2200d));
        Assert.That(game.State.HasCurrentFeudalTerritoryReward, Is.True);
        ExpantaNum completedTotal = game.State.TerritoryTotal;
        save.ApplySaveDataForEditor(JsonUtility.FromJson<SaveManager.KingdomSaveData>(JsonUtility.ToJson(save.CaptureSaveData())));
        AssertNear(game.State.TerritoryTotal, completedTotal);
    }

    [TestCase(7201d)]
    [TestCase(28801d)]
    public void IncrementalSettlement_PreservesSynchronousRateBoundaries(double seconds)
    {
        SaveManager.KingdomSaveData initial = save.CaptureSaveData();
        save.ApplySaveDataForEditor(initial);
        initial = save.CaptureSaveData();
        SimulationManager simulation = host.GetComponent<SimulationManager>();
        ResourceDef wood = DataBase<ResourceDef>.Find(ResourceManager.StartingResourceId);
        simulation.AdvanceOffline(seconds);
        ExpantaNum expectedWood = resources.GetAmount(wood);
        ExpantaNum expectedFood = game.State.FoodAmount;
        int expectedDay = game.State.CalendarDays;

        save.ApplySaveDataForEditor(initial);
        SimulationManager.OfflineProgressSession session = simulation.BeginOfflineProgress(seconds - 1d);
        session.Advance(double.Epsilon);
        Assert.That(session.Completed, Is.False, "One integration step must leave a long session resumable.");
        Assert.That(session.Progress, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
        session.Extend(1d);
        while (!session.Completed) session.Advance(4d);

        AssertNear(resources.GetAmount(wood), expectedWood);
        AssertNear(game.State.FoodAmount, expectedFood);
        Assert.That(game.State.CalendarDays, Is.EqualTo(expectedDay), "Calendar day is a discrete counter.");
        Assert.That(session.Progress, Is.EqualTo(1f), "One denotes the completed progress protocol state.");
    }

    [Test]
    public void ActualSettlement_CopiesResourceFoodAndPopulationDifferences()
    {
        ResourceDef wood = DataBase<ResourceDef>.Find(ResourceManager.StartingResourceId);
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
        ResearchDef definition = DataBase<ResearchDef>.Find("ControlledFire");
        Assert.That(definition, Is.Not.Null);
        if (!supplied)
        {
            BuildingDef lumberyard = DataBase<BuildingDef>.Find("Lumberyard");
            buildings.SetAmountAndRatesForEditor(buildings.GetState(lumberyard), ExpantaNum.Zero);
            resources.SetAmount(DataBase<ResourceDef>.Find(ResourceManager.StartingResourceId), ExpantaNum.Zero);
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

    private static OfflineResourceChange Find(OfflineProgressSummary summary, ResourceDef resource)
    {
        foreach (OfflineResourceChange change in summary.Resources)
            if (change.Resource == resource) return change;
        throw new InvalidOperationException("Expected resource difference was not captured.");
    }

    private static void AssertNear(ExpantaNum actual, ExpantaNum expected) =>
        Assert.That((actual - expected).Abs().ToDouble(), Is.LessThan(1e-6));
}
