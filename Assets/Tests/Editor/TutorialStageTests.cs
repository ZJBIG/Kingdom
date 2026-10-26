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
    public void ResearchPageRequiresAnOwnedHomeAndTutorialWaitsForIt()
    {
        BuildingManager buildings = host.GetComponent<BuildingManager>();
        Assert.That(ProgressionVisibilityRules.IsResearchUnlocked(game.State), Is.False);
        tutorial.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            StagesInitialized = true,
            CompletedStageIds = new List<string> { "resources", "building", "population" }
        }, TechLevel.Animal);
        Assert.That(tutorial.EvaluateStage(), Is.Null);
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(DataBase<BuildingDef>.Find("WoodHouse")), ExpantaNum.One);
        Assert.That(ProgressionVisibilityRules.IsResearchUnlocked(game.State), Is.True);
        Assert.That(tutorial.EvaluateStage()?.StageId, Is.EqualTo("research")); // Stable stage protocol.
    }

    [Test]
    public void FirstHousingUnlockPersistsAfterDeconstructionAndSaveRestore()
    {
        BuildingManager buildings = host.GetComponent<BuildingManager>();
        ResourceManager resources = host.GetComponent<ResourceManager>();
        BuildingDef home = DataBase<BuildingDef>.Find("WoodHouse");
        resources.SetAmount(DataBase<ResourceDef>.Find(ResourceManager.StartingResourceId), ExpantaNum.Zero);
        Assert.That(buildings.TryBuild(home, ExpantaNum.One, out _), Is.False);
        Assert.That(game.State.HasBuiltHousing, Is.False);
        resources.SetAmount(DataBase<ResourceDef>.Find(ResourceManager.StartingResourceId), new ExpantaNum(1000));
        Assert.That(buildings.TryBuild(home, ExpantaNum.One, out _), Is.True);
        Assert.That(buildings.TryDeconstruct(home, ExpantaNum.One), Is.True);
        Assert.That(ProgressionVisibilityRules.IsResearchUnlocked(game.State), Is.True);
        SaveManager.GameSaveData saved = game.CaptureSaveData();
        game.InitializeNewGameForEditor();
        Assert.That(game.State.HasBuiltHousing, Is.False);
        game.RestoreSaveDataForEditor(saved);
        Assert.That(ProgressionVisibilityRules.IsResearchUnlocked(game.State), Is.True);
    }

    [Test]
    public void EraPlanningUnlocksBeforeTheSeparateStoneAgeTransition()
    {
        ResearchManager research = host.GetComponent<ResearchManager>();
        ResearchDef settlement = DataBase<ResearchDef>.Find("StoneAgeSettlement");
        ResearchDef planning = DataBase<ResearchDef>.Find(ProgressionVisibilityRules.EraResearchId);
        ResearchDef transition = DataBase<ResearchDef>.Find("StoneAgeTransition");
        Assert.That(settlement.AdvancesTechLevel, Is.False);
        Assert.That(settlement.TechLevel, Is.EqualTo(TechLevel.Animal)); // Era protocol.
        Assert.That(planning.Prerequisites, Does.Contain(settlement));
        Assert.That(transition.Prerequisites, Does.Contain(planning));
        Assert.That(transition.AdvancesTechLevel, Is.True);
        Assert.That(transition.TechLevel, Is.EqualTo(TechLevel.StoneAge));
        Assert.That(ProgressionVisibilityRules.IsEraUnlocked(research), Is.False);
        research.GetState(settlement).SetStatusForEditor(ResearchStatus.Completed);
        Assert.That(ProgressionVisibilityRules.IsEraUnlocked(research), Is.False);
        research.GetState(planning).SetStatusForEditor(ResearchStatus.Completed);
        Assert.That(ProgressionVisibilityRules.IsEraUnlocked(research), Is.True);
        Assert.That(game.State.TechLevel, Is.EqualTo(TechLevel.Animal));
        Assert.That(research.GetState(transition).Status, Is.Not.EqualTo(ResearchStatus.Completed));
    }

    [Test]
    public void ProductionPageRequiresCompletedFactoryOrganization()
    {
        ResearchManager research = host.GetComponent<ResearchManager>();
        ResearchState factory = research.GetState(DataBase<ResearchDef>.Find("FactoryOrganization"));
        Assert.That(ProgressionVisibilityRules.IsProductionUnlocked(research), Is.False);
        factory.SetStatusForEditor(ResearchStatus.Researching);
        Assert.That(ProgressionVisibilityRules.IsProductionUnlocked(research), Is.False);
        factory.SetStatusForEditor(ResearchStatus.Completed);
        Assert.That(ProgressionVisibilityRules.IsProductionUnlocked(research), Is.True);
    }

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
        host.GetComponent<ResourceManager>().SetAmount(
            DataBase<ResourceDef>.Find(ResourceManager.StartingResourceId), new ExpantaNum(1000));
        tutorial.EvaluateStage();
        tutorial.SkipCurrentStage();
        Assert.That(tutorial.SkippedStageIds, Does.Contain("resources"));
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain("resources"));
        TutorialStageSnapshot next = tutorial.EvaluateStage();
        Assert.That(next, Is.Not.Null, "An affordable building stage remains available after skipping only resources.");
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
        resources.SetAmount(DataBase<ResourceDef>.Find(ResourceManager.StartingResourceId), new ExpantaNum(1000));
        tutorial.EvaluateStage();
        tutorial.SkipCurrentStage();
        TutorialStageSnapshot building = tutorial.EvaluateStage();
        Assert.That(building.StageId, Is.EqualTo("building"));
        tutorial.RecordStagePageVisited(building.Page);
        tutorial.RecordStageTargetViewed(building.Page, building.TargetId);
        foreach (ResourceDef definition in DataBase<ResourceDef>.All)
            resources.SetAmount(definition, ExpantaNum.Zero);
        TutorialStageSnapshot waiting = tutorial.EvaluateStage();
        Assert.That(waiting.Action, Is.EqualTo(TutorialStageAction.Build));
        Assert.That(waiting.CanInteract, Is.False);
        tutorial.SkipCurrentStage();
        Assert.That(tutorial.SkippedStageIds, Does.Contain("building"));
    }

    [Test]
    public void MissingBuildingTargetRebindsWithoutReportingCompletion()
    {
        ResourceManager resources = host.GetComponent<ResourceManager>();
        resources.SetAmount(DataBase<ResourceDef>.Find(ResourceManager.StartingResourceId), new ExpantaNum(1000));
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
        Assert.That(rebound.StageId, Is.EqualTo("building"));
        Assert.That(rebound.Action, Is.EqualTo(TutorialStageAction.Navigate));
        Assert.That(rebound.TargetId, Is.Not.EqualTo("removed-building"));
        Assert.That(DataBase<BuildingDef>.TryFind(rebound.TargetId, out BuildingDef replacement), Is.True);
        Assert.That(host.GetComponent<BuildingManager>().CanConstructNew(replacement), Is.True);
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain("building"));
    }

}
