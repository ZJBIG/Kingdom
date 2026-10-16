using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class TutorialStageTests
{
    private GameObject host;
    private TutorialManager tutorial;
    private GameManager game;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Staged tutorial tests");
        ResourceManager resources = host.AddComponent<ResourceManager>();
        resources.InitializeForEditor();
        game = host.AddComponent<GameManager>();
        host.AddComponent<BuildingManager>();
        ResearchManager research = host.AddComponent<ResearchManager>();
        research.InitializeForEditor();
        tutorial = host.AddComponent<TutorialManager>();
        tutorial.ResetForNewGameForEditor();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(host);

    [Test]
    public void FreshGameRequiresNavigationThenMatchingSelectionAndAcknowledgement()
    {
        TutorialStageSnapshot first = tutorial.EvaluateStage();
        Assert.That(first.StageId, Is.EqualTo("resources")); // Stable stage protocol.
        Assert.That(first.Action, Is.EqualTo(TutorialStageAction.Navigate));
        tutorial.AcknowledgeCurrentStage();
        tutorial.RecordStagePageVisited("Research");
        Assert.That(tutorial.EvaluateStage().Action, Is.EqualTo(TutorialStageAction.Navigate));
        tutorial.RecordStagePageVisited(first.Page);
        tutorial.RecordStageTargetViewed(first.Page, "wrong-id");
        Assert.That(tutorial.EvaluateStage().Action, Is.EqualTo(TutorialStageAction.Select));
        tutorial.RecordStageTargetViewed(first.Page, first.TargetId);
        Assert.That(tutorial.EvaluateStage().Action, Is.EqualTo(TutorialStageAction.Inspect));
        tutorial.AcknowledgeCurrentStage();
        Assert.That(tutorial.CompletedStageIds, Does.Contain("resources"));
        Assert.That(tutorial.SkippedStageIds, Is.Empty);
    }

    [Test]
    public void SkipOnlyDismissesCurrentStageAndDoesNotReportCompletion()
    {
        tutorial.EvaluateStage();
        tutorial.SkipCurrentStage();
        Assert.That(tutorial.SkippedStageIds, Does.Contain("resources"));
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain("resources"));
        TutorialStageSnapshot next = tutorial.EvaluateStage();
        Assert.That(next, Is.Not.Null);
        Assert.That(next.StageId, Is.Not.EqualTo("resources"));
    }

    [Test]
    public void SkippedHistoryPersistsAndNewGameClearsIt()
    {
        tutorial.EvaluateStage();
        tutorial.SkipCurrentStage();
        SaveManager.TutorialSaveData saved = tutorial.CaptureSaveData();
        tutorial.RestoreSaveData(saved, TechLevel.Animal);
        Assert.That(tutorial.SkippedStageIds, Does.Contain("resources"));
        Assert.That(tutorial.CompletedStageIds, Is.Empty);
        tutorial.ResetForNewGameForEditor();
        Assert.That(tutorial.SkippedStageIds, Is.Empty);
        Assert.That(tutorial.EvaluateStage().StageId, Is.EqualTo("resources"));
    }

    [Test]
    public void ExistingV1SaveBaselinesUnlockedSystemsAndLaterPopulationStillTriggers()
    {
        tutorial.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            CompletedStepIds = new List<string>()
        }, TechLevel.Animal);
        SaveManager.TutorialSaveData saved = tutorial.CaptureSaveData();
        Assert.That(saved.StagesInitialized, Is.True, "Baseline is established before offline progress or the first UI evaluation.");
        Assert.That(saved.BaselineStageIds, Does.Contain("resources"));
        Assert.That(saved.BaselineStageIds, Does.Contain("building"), "Unlocked buildings are baselined even when unaffordable.");
        Assert.That(saved.CompletedStageIds, Is.Empty);
        game.State.RestorePopulationCapacityExactForEditor(new ExpantaNum(3), ExpantaNum.Zero);
        TutorialStageSnapshot unlocked = tutorial.EvaluateStage();
        Assert.That(unlocked, Is.Not.Null);
        Assert.That(unlocked.StageId, Is.EqualTo("population"));
    }

    [Test]
    public void RestoreReestablishesNavigationInsteadOfKeepingStaleSelection()
    {
        TutorialStageSnapshot first = tutorial.EvaluateStage();
        tutorial.RecordStagePageVisited(first.Page);
        tutorial.RecordStageTargetViewed(first.Page, first.TargetId);
        SaveManager.TutorialSaveData saved = tutorial.CaptureSaveData();
        tutorial.RestoreSaveData(saved, TechLevel.Animal);
        Assert.That(tutorial.EvaluateStage().Action, Is.EqualTo(TutorialStageAction.Navigate));
        Assert.That(tutorial.EvaluateStage().TargetId, Is.EqualTo(first.TargetId)); // Stable selection.
    }

    [Test]
    public void LockedWorkshopAndSectorsDoNotTrigger()
    {
        tutorial.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            StagesInitialized = true,
            CompletedStageIds = new List<string> { "resources", "building", "population", "research", "production-chain", "era-goal" }
        }, TechLevel.Animal);
        Assert.That(tutorial.EvaluateStage(), Is.Null);
    }
    [Test]
    public void BuildingPaymentWaitReleasesBlockingAndKeepsStageSkippable()
    {
        ResourceManager resources = host.GetComponent<ResourceManager>();
        resources.SetAmount(DataBase<Resource>.Find(ResourceManager.StartingResourceId), new ExpantaNum(1000));
        tutorial.EvaluateStage();
        tutorial.SkipCurrentStage();
        TutorialStageSnapshot building = tutorial.EvaluateStage();
        Assert.That(building.StageId, Is.EqualTo("building"));
        tutorial.RecordStagePageVisited(building.Page);
        tutorial.RecordStageTargetViewed(building.Page, building.TargetId);
        foreach (Resource definition in DataBase<Resource>.All)
            resources.SetAmount(definition, ExpantaNum.Zero);
        TutorialStageSnapshot waiting = tutorial.EvaluateStage();
        Assert.That(waiting, Is.Not.Null);
        Assert.That(waiting.Action, Is.EqualTo(TutorialStageAction.Build));
        Assert.That(waiting.CanInteract, Is.False);
        tutorial.SkipCurrentStage();
        Assert.That(tutorial.SkippedStageIds, Does.Contain("building"));
    }

    [Test]
    public void MissingBuildingTargetRebindsWithoutReportingCompletion()
    {
        ResourceManager resources = host.GetComponent<ResourceManager>();
        resources.SetAmount(DataBase<Resource>.Find(ResourceManager.StartingResourceId), new ExpantaNum(1000));
        tutorial.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            StagesInitialized = true,
            ActiveStageId = "building",
            StageTargetId = "removed-building",
            StagePhase = 2,
            StageBuildingAmount = "0",
            CompletedStageIds = new List<string> { "resources" }
        }, TechLevel.Animal);
        TutorialStageSnapshot rebound = tutorial.EvaluateStage();
        Assert.That(rebound, Is.Not.Null);
        Assert.That(rebound.StageId, Is.EqualTo("building"));
        Assert.That(rebound.Action, Is.EqualTo(TutorialStageAction.Navigate));
        Assert.That(rebound.TargetId, Is.Not.EqualTo("removed-building"));
        Assert.That(DataBase<Building>.TryFind(rebound.TargetId, out Building replacement), Is.True);
        Assert.That(host.GetComponent<BuildingManager>().CanConstructNew(replacement), Is.True);
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain("building"));
    }

}
