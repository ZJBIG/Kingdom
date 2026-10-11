using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class AuditRuntimeRegressionTests
{
    private GameObject host;
    private GameManager game;
    private ResourceManager resources;
    private BuildingManager buildings;
    private ResearchManager research;
    private SaveManager save;
    private string saveRoot;

    [SetUp]
    public void SetUp()
    {
        saveRoot = Path.Combine(Path.GetTempPath(), "KingdomAudit-" + Guid.NewGuid().ToString("N"));
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
        ProgressionModifierManager.Rebuild(null);
        host = new GameObject("AuditRuntimeRegressionManagers");
        resources = host.AddComponent<ResourceManager>();
        if (resources.States.Count == 0) resources.InitializeForEditor();
        game = host.AddComponent<GameManager>();
        buildings = host.AddComponent<BuildingManager>();
        research = host.AddComponent<ResearchManager>();
        if (research.States.Count == 0) research.InitializeForEditor();
        host.AddComponent<WorkshopManager>();
        host.AddComponent<TutorialManager>();
        host.AddComponent<SimulationManager>();
        save = host.AddComponent<SaveManager>();
        game.InitializeNewGameForEditor();
        research.ResetForLoadForEditor();
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(host);
        SaveManager.ClearSaveRootOverrideForTests();
        ProgressionModifierManager.Rebuild(null);
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
    }

    [Test]
    public void ZeroProductivityHousing_PredictionAndSubmissionAgreeDuringDeficit()
    {
        buildings.EnsureBuilding(DataBase<Building>.Find("Lumberyard"))
            .SetAmountForEditor(new ExpantaNum(100));
        resources.SetAmount(DataBase<Resource>.Find("WoodLog"), new ExpantaNum(10000));
        Building housing = DataBase<Building>.Find("WoodHouse");
        Assert.That(buildings.AvailableProductivity, Is.LessThan(ExpantaNum.Zero));
        Assert.That(buildings.GetMaxBuildable(housing, ExpantaNum.One), Is.GreaterThanOrEqualTo(ExpantaNum.One));
        Assert.That(buildings.GetBuildFailure(housing, ExpantaNum.One), Is.EqualTo(BuildFailure.None));
        Assert.That(buildings.TryBuild(housing, ExpantaNum.One, out _), Is.True);
        Building staffed = DataBase<Building>.Find("Lumberyard");
        foreach (Research prerequisite in staffed.RequiredResearch)
        {
            ResearchState prerequisiteState = research.GetState(prerequisite);
            prerequisiteState.SetProgressForEditor(prerequisiteState.BaseCost);
            prerequisiteState.SetStatusForEditor(ResearchStatus.Completed);
        }
        Assert.That(game.State.TechLevel, Is.GreaterThanOrEqualTo(staffed.TechLevel));
        Assert.That(buildings.GetBuildFailure(staffed, ExpantaNum.One), Is.EqualTo(BuildFailure.ProductivityInsufficient));
    }

    [Test]
    public void CancellationPreview_MatchesRemovalAndRetainsPaidProgress()
    {
        Research target = DataBase<Research>.Find("KnowledgeSharing");
        Research prerequisite = target.Prerequisites[0];
        foreach (var pair in resources.States) resources.SetAmount(pair.Key, new ExpantaNum(10000));
        research.HandleResearchAction(target);
        ResearchState active = research.ActiveResearch;
        Assert.That(active.Definition, Is.SameAs(prerequisite));
        active.SetProgressForEditor(active.BaseCost / new ExpantaNum(4));
        ExpantaNum progress = active.Progress;
        var preview = research.GetCancellationPreview(prerequisite);
        Assert.That(preview.Count, Is.GreaterThan(1));
        Assert.That(preview[0], Is.SameAs(active));
        Assert.That(research.RemoveQueuedResearch(prerequisite), Is.True);
        foreach (ResearchState state in preview)
        {
            Assert.That(research.IsQueued(state.Definition), Is.False);
            Assert.That(research.ActiveResearch, Is.Not.SameAs(state));
        }
        Assert.That(active.CostPaid, Is.True);
        Assert.That(active.Progress.ToDouble(), Is.EqualTo(progress.ToDouble()).Within(1e-8));
        Assert.That(research.HandleResearchAction(prerequisite), Is.EqualTo(ResearchActionResult.Started));
        Assert.That(active.Progress.ToDouble(), Is.EqualTo(progress.ToDouble()).Within(1e-8));
    }

    [Test]
    public void QueueMove_RejectsDependencyInversionAndMovesIndependentWaitingHead()
    {
        resources.SetAmount(DataBase<Resource>.Find("WoodLog"), ExpantaNum.Zero);
        Research target = DataBase<Research>.Find("KnowledgeSharing");
        research.HandleResearchAction(target);
        Assert.That(research.CanMoveQueuedResearch(target, 0), Is.False);
        Assert.That(research.MoveQueuedResearch(target, 0), Is.False);
        research.ResetForLoadForEditor();
        Research first = DataBase<Research>.Find("Quarry");
        Research second = DataBase<Research>.Find("Agriculture");
        research.HandleResearchAction(first);
        research.HandleResearchAction(second);
        Assert.That(research.CanMoveQueuedResearch(second, 0), Is.True);
        Assert.That(research.MoveQueuedResearch(second, 0), Is.True);
        Assert.That(research.ResearchQueue[0].Definition, Is.SameAs(second));
        Assert.That(research.ResearchQueue[0].Status, Is.EqualTo(ResearchStatus.WaitingResources));
        Assert.That(research.ResearchQueue[1].Status, Is.EqualTo(ResearchStatus.Queued));
    }

    [Test]
    public void EffectiveResearchSpeed_MatchesTickAndIsZeroWhileWaitingForPayment()
    {
        Research definition = DataBase<Research>.Find("Quarry");
        resources.SetAmount(DataBase<Resource>.Find("WoodLog"), ExpantaNum.Zero);
        research.HandleResearchAction(definition);
        Assert.That(research.CurrentResearchSpeed.ToDouble(), Is.EqualTo(0d).Within(1e-8));
        resources.SetAmount(DataBase<Resource>.Find("WoodLog"), new ExpantaNum(10000));
        research.Tick(0d);
        ExpantaNum speed = research.CurrentResearchSpeed;
        Assert.That(speed, Is.GreaterThan(ExpantaNum.Zero));
        ExpantaNum before = research.ActiveResearch.Progress;
        research.Tick(0.1d);
        Assert.That((research.ActiveResearch.Progress - before).ToDouble(),
            Is.EqualTo((speed * new ExpantaNum(0.1d)).ToDouble()).Within(1e-8));
    }

    [Test]
    public void DeconstructionPreview_UsesSubmittedRefundAndDoesNotChangeState()
    {
        Building housing = DataBase<Building>.Find("WoodHouse");
        resources.SetAmount(DataBase<Resource>.Find("WoodLog"), new ExpantaNum(10000));
        Assert.That(buildings.TryBuild(housing, new ExpantaNum(2), out _), Is.True);
        BuildingState state = buildings.EnsureBuilding(housing);
        ExpantaNum count = state.Amount;
        var preview = buildings.GetDeconstructionPreview(housing, ExpantaNum.One);
        Assert.That(state.Amount, Is.EqualTo(count)); // Discrete building count.
        var before = new System.Collections.Generic.Dictionary<Resource, ExpantaNum>();
        foreach (var pair in preview.Refunds) before.Add(pair.Key, resources.GetAmount(pair.Key));
        Assert.That(buildings.TryDeconstruct(housing, ExpantaNum.One), Is.True);
        foreach (var pair in preview.Refunds)
            Assert.That((resources.GetAmount(pair.Key) - before[pair.Key]).ToDouble(),
                Is.EqualTo(pair.Value.ToDouble()).Within(1e-7));
    }

    [Test]
    public void DeconstructionPreview_PredictsActualFoodOverflowWhenCapacityFalls()
    {
        Building storage = DataBase<Building>.Find("Granary");
        BuildingState state = buildings.EnsureBuilding(storage);
        buildings.SetAmountAndRatesForEditor(state, ExpantaNum.One);
        game.State.RestoreCoreForEditor(0, TechLevel.Animal, game.State.FoodCapacity, 0L);
        ExpantaNum foodBefore = game.State.FoodAmount;
        var preview = buildings.GetDeconstructionPreview(storage, ExpantaNum.One);
        Assert.That(preview.FoodOverflow, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(buildings.TryDeconstruct(storage, ExpantaNum.One), Is.True);
        Assert.That((foodBefore - game.State.FoodAmount).ToDouble(),
            Is.EqualTo(preview.FoodOverflow.ToDouble()).Within(1e-7));
    }

    [Test]
    public void StrategicDemand_IncludesActiveWorkAndDisappearsWhenPaused()
    {
        ExpantaNum idlePower = buildings.TotalPowerDemand;
        ExpantaNum idleLogistics = buildings.TotalLogisticsDemand;
        ExpantaNum idleFood = buildings.StrategicFoodConsumptionRate;
        game.UltraProject.State.StartForEditor(UltraProjectDoctrine.Stable);
        ExpantaNum engineeringPower = buildings.TotalPowerDemand;
        ExpantaNum engineeringLogistics = buildings.TotalLogisticsDemand;
        ExpantaNum engineeringFood = buildings.StrategicFoodConsumptionRate;
        Assert.That(engineeringPower, Is.GreaterThan(idlePower));
        Assert.That(engineeringLogistics, Is.GreaterThan(idleLogistics));
        Assert.That(engineeringFood, Is.GreaterThan(idleFood));
        game.Relic.State.StartInvestigationForEditor();
        Assert.That(buildings.TotalPowerDemand, Is.GreaterThan(engineeringPower));
        Assert.That(buildings.TotalLogisticsDemand, Is.GreaterThan(engineeringLogistics));
        Assert.That(buildings.StrategicFoodConsumptionRate, Is.GreaterThan(engineeringFood));
        game.Relic.State.SuspendForEditor(RelicPauseReason.Manual);
        Assert.That(buildings.TotalPowerDemand.ToDouble(), Is.EqualTo(engineeringPower.ToDouble()).Within(1e-8));
        Assert.That(buildings.StrategicFoodConsumptionRate.ToDouble(), Is.EqualTo(engineeringFood.ToDouble()).Within(1e-8));
        game.UltraProject.State.PauseForEditor();
        Assert.That(buildings.TotalPowerDemand.ToDouble(), Is.EqualTo(idlePower.ToDouble()).Within(1e-8));
        Assert.That(buildings.TotalLogisticsDemand.ToDouble(), Is.EqualTo(idleLogistics.ToDouble()).Within(1e-8));
        Assert.That(buildings.StrategicFoodConsumptionRate.ToDouble(), Is.EqualTo(idleFood.ToDouble()).Within(1e-8));
    }

    [Test]
    public void SaveFeedback_FailureKeepsSuccessTimestampAndSuccessfulRetryClearsError()
    {
        save.SetReady(true);
        Assert.That(save.SaveNow(true), Is.True);
        long successfulAt = save.LastSuccessfulSaveUnixSeconds;
        Assert.That(successfulAt, Is.GreaterThan(0));
        string blockedRoot = Path.Combine(saveRoot, "blocked");
        File.WriteAllText(blockedRoot, "fixture");
        SaveManager.SetSaveRootOverrideForTests(blockedRoot);
        LogAssert.Expect(LogType.Error, new Regex("保存 Kingdom 数据失败"));
        Assert.That(save.SaveNow(true), Is.False);
        Assert.That(save.LastSaveFailed, Is.True);
        Assert.That(save.LastSaveError, Is.Not.Empty);
        Assert.That(save.LastSuccessfulSaveUnixSeconds, Is.EqualTo(successfulAt)); // Protocol timestamp.
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
        Assert.That(save.SaveNow(true), Is.True);
        Assert.That(save.LastSaveFailed, Is.False);
        Assert.That(save.LastSaveError, Is.Empty);
    }

    [Test]
    public void LoadFeedback_DistinguishesFirstStartFromInvalidExistingSave()
    {
        Assert.That(save.LoadOrCreateGame(), Is.False);
        Assert.That(save.LastLoadCreatedNewGame, Is.True);
        Assert.That(save.LastLoadFailed, Is.False);
        Directory.CreateDirectory(saveRoot);
        File.WriteAllText(Path.Combine(saveRoot, "KingdomSave.json"), "{}");
        LogAssert.Expect(LogType.Error, new Regex("读取 Kingdom 存档"));
        Assert.That(save.LoadOrCreateGame(), Is.False);
        Assert.That(save.LastLoadFailed, Is.True);
        Assert.That(save.LastLoadCreatedNewGame, Is.True);
    }

    [Test]
    public void OfflineSummary_CopiesRelicProgressInsteadOfKeepingMutableState()
    {
        game.Relic.State.StartInvestigationForEditor();
        game.Relic.State.AdvanceForEditor(new ExpantaNum(0.25d));
        game.Relic.State.SuspendForEditor(RelicPauseReason.Manual);
        Assert.That(save.ApplyOfflineProgressForEditor(100, 110), Is.True);
        OfflineProgressSummary summary = save.LastOfflineSummary;
        Assert.That(summary.RelicStatusBefore, Is.EqualTo(RelicStatus.Investigating));
        Assert.That(summary.RelicStatusAfter, Is.EqualTo(RelicStatus.Investigating));
        Assert.That(summary.RelicProgressAfter.ToDouble(), Is.EqualTo(0.25d).Within(1e-8));
        Assert.That(summary.RelicPauseReasonBefore, Is.EqualTo(RelicPauseReason.Manual));
        Assert.That(summary.RelicPauseReasonAfter, Is.EqualTo(RelicPauseReason.Manual));
        game.Relic.State.ResumeForEditor();
        game.Relic.State.AdvanceForEditor(new ExpantaNum(0.25d));
        Assert.That(game.Relic.State.Progress, Is.GreaterThan(summary.RelicProgressAfter));
        Assert.That(summary.RelicProgressAfter.ToDouble(), Is.EqualTo(0.25d).Within(1e-8));
        Assert.That(summary.RelicPauseReasonAfter, Is.EqualTo(RelicPauseReason.Manual));
    }

    [Test]
    public void OfflineSummary_ReportsZeroEffectiveResearchSpeedDespitePositiveResearchPower()
    {
        Research definition = DataBase<Research>.Find("Quarry");
        resources.SetAmount(DataBase<Resource>.Find("WoodLog"), new ExpantaNum(10000));
        research.HandleResearchAction(definition);
        Assert.That(research.ActiveResearch, Is.Not.Null);
        research.GlobalEfficiencyFactor = ExpantaNum.Zero;
        Assert.That(save.ApplyOfflineProgressForEditor(100, 110), Is.True);
        Assert.That(research.ResearchPower, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(research.CurrentResearchSpeed.ToDouble(), Is.EqualTo(0d).Within(1e-8));
        Assert.That(save.LastOfflineSummary.ResearchPowerBlocked, Is.True);
    }

    [Test]
    public void OfflineSummary_CapturesRelicPauseWithoutAStatusOrProgressChange()
    {
        game.Relic.State.StartInvestigationForEditor();
        game.Relic.State.SuspendForEditor(RelicPauseReason.InsufficientSupply);
        Assert.That(save.ApplyOfflineProgressForEditor(100, 110), Is.True);
        OfflineProgressSummary summary = save.LastOfflineSummary;
        Assert.That(summary.RelicStatusBefore, Is.EqualTo(summary.RelicStatusAfter));
        Assert.That(summary.RelicProgressAfter.ToDouble(), Is.EqualTo(summary.RelicProgressBefore.ToDouble()).Within(1e-8));
        Assert.That(summary.RelicPauseReasonAfter, Is.EqualTo(RelicPauseReason.InsufficientSupply));
        game.Relic.State.ResumeForEditor();
        Assert.That(summary.RelicPauseReasonAfter, Is.EqualTo(RelicPauseReason.InsufficientSupply));
    }

    [Test]
    public void OfflineSummary_DistinguishesCoveredAndEffectiveEconomySeconds()
    {
        Assert.That(save.ApplyOfflineProgressForEditor(100, 100 + 3 * 3600), Is.True);
        Assert.That(save.LastOfflineSummary.SettledSeconds, Is.EqualTo(10800d).Within(1e-6));
        Assert.That(save.LastOfflineSummary.EffectiveEconomySeconds, Is.EqualTo(9360d).Within(1e-6));
    }
}
