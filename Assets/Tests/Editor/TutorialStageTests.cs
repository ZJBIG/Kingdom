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
        TutorialManager.Ensure();
        tutorial.ResetForNewGameForEditor();
        for (int i = 0; i < 8; i++) tutorial.AcknowledgeCurrentStage();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        ProgressionModifierManager.Rebuild(null);
    }

    [Test]
    public void NewGameExplainsMissionAndHudBeforeHousingAndCanSkipOnlyIntroduction()
    {
        tutorial.ResetForNewGameForEditor();
        Assert.That(tutorial.EvaluateStage().Title, Is.EqualTo("复兴鼠族"));
        foreach (string target in new[] { "mission", "food", "population", "productivity", "research", "territory", "happiness", "materials" })
        {
            TutorialStageSnapshot stage = tutorial.EvaluateStage();
            Assert.That(stage.StageId, Is.EqualTo("orientation")); // Stable stage/target protocol.
            Assert.That(stage.TargetId, Is.EqualTo(target));
            Assert.That(stage.Action, Is.EqualTo(TutorialStageAction.Inspect));
            Assert.That(tutorial.Evaluate().NavigationTargetId, Is.EqualTo(target));
            Assert.That(stage.Instruction, Is.Not.Empty);
            tutorial.AcknowledgeCurrentStage();
            if (target == "food")
            {
                SaveManager.TutorialSaveData midway = tutorial.CaptureSaveData();
                tutorial.RestoreSaveData(midway, TechLevel.Animal);
                Assert.That(tutorial.EvaluateStage().TargetId, Is.EqualTo("population"), "Reading progress resumes after save/load.");
            }
        }
        Assert.That(tutorial.EvaluateStage().StageId, Is.EqualTo("building"));
        SaveManager.TutorialSaveData saved = tutorial.CaptureSaveData();
        tutorial.RestoreSaveData(saved, TechLevel.Animal);
        Assert.That(tutorial.EvaluateStage().StageId, Is.EqualTo("building"));
        tutorial.ResetForNewGameForEditor();
        tutorial.EvaluateStage();
        tutorial.SkipCurrentStage();
        Assert.That(tutorial.EvaluateStage().StageId, Is.EqualTo("building"));
        Assert.That(tutorial.SkippedStageIds, Does.Not.Contain("building"));
    }

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
        Assert.That(tutorial.Evaluate().NavigationTargetId, Is.EqualTo("WoodHouse"), "Saved lesson completion does not grant real housing.");
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
    public void FreshGameTargetsHousingAndRequiresRealConstruction()
    {
        TutorialStageSnapshot first = tutorial.EvaluateStage();
        Assert.That(first.StageId, Is.EqualTo("building")); // Stable stage protocol.
        Assert.That(first.TargetId, Is.EqualTo("WoodHouse"));
        Assert.That(first.Action, Is.EqualTo(TutorialStageAction.Navigate));
        tutorial.AcknowledgeCurrentStage();
        tutorial.RecordStagePageVisited("Research");
        Assert.That(tutorial.EvaluateStage().Action, Is.EqualTo(TutorialStageAction.Navigate));
        tutorial.RecordStagePageVisited(first.Page);
        tutorial.RecordStageTargetViewed(first.Page, "wrong-id");
        Assert.That(tutorial.EvaluateStage().Action, Is.EqualTo(TutorialStageAction.Select));
        tutorial.RecordStageTargetViewed(first.Page, first.TargetId);
        Assert.That(tutorial.EvaluateStage().Action, Is.EqualTo(TutorialStageAction.Build));
        tutorial.AcknowledgeCurrentStage();
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain("building"));
        Assert.That(tutorial.SkippedStageIds, Is.Empty);
        SetResources(new ExpantaNum(1000));
        Assert.That(host.GetComponent<BuildingManager>().TryBuild(DataBase<BuildingDef>.Find("WoodHouse"), ExpantaNum.One, out _), Is.True);
        AssertUnifiedAction("research", "Research", "Agriculture");
        Assert.That(tutorial.CompletedStageIds, Does.Contain("building"));
    }

    [Test]
    public void SkipOnlyDismissesCurrentStageAndDoesNotReportCompletion()
    {
        host.GetComponent<ResourceManager>().SetAmount(
            DataBase<ResourceDef>.Find(ResourceManager.StartingResourceId), new ExpantaNum(1000));
        tutorial.EvaluateStage();
        tutorial.SkipCurrentStage();
        Assert.That(tutorial.SkippedStageIds, Does.Contain("building"));
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain("building"));
        TutorialStageSnapshot next = tutorial.EvaluateStage();
        Assert.That(next, Is.Null, "Skipping hides only this operation lesson.");
        Assert.That(tutorial.Evaluate().NavigationTargetId, Is.EqualTo("WoodHouse"), "The business goal remains visible in the overview.");
        BuildHome();
        AssertUnifiedAction("research", "Research", "Agriculture");
    }

    [Test]
    public void SkippedHistoryPersistsAndNewGameClearsIt()
    {
        tutorial.EvaluateStage();
        tutorial.SkipCurrentStage();
        SaveManager.TutorialSaveData saved = tutorial.CaptureSaveData();
        tutorial.RestoreSaveData(saved, TechLevel.Animal);
        Assert.That(tutorial.SkippedStageIds, Does.Contain("building"));
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain("building"));
        tutorial.ResetForNewGameForEditor();
        Assert.That(tutorial.SkippedStageIds, Is.Empty);
        Assert.That(tutorial.EvaluateStage().StageId, Is.EqualTo("orientation"));
    }

    [Test]
    public void ExistingV1SaveBaselinesLessonsWithoutReplacingTheCurrentBusinessGoal()
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
        Assert.That(tutorial.Evaluate().NavigationTargetId, Is.EqualTo("WoodHouse"));
        Assert.That(tutorial.EvaluateStage(), Is.Null, "Population is parallel feedback and must not replace the baselined housing lesson.");
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
            CompletedStageIds = new List<string> { "resources", "building", "population", "research", "farm-goal", "stone-chain", "first-transition", "production-chain", "era-goal" }
        }, TechLevel.Animal);
        Assert.That(tutorial.EvaluateStage(), Is.Null);
    }
    [Test]
    public void BuildingPaymentWaitReleasesBlockingAndKeepsStageSkippable()
    {
        ResourceManager resources = host.GetComponent<ResourceManager>();
        resources.SetAmount(DataBase<ResourceDef>.Find(ResourceManager.StartingResourceId), new ExpantaNum(1000));
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
    public void QueuedUnpaidResearchCannotBeAcknowledgedAsCompleted()
    {
        BuildingManager buildings = host.GetComponent<BuildingManager>();
        ResearchManager research = host.GetComponent<ResearchManager>();
        ResearchDef agriculture = DataBase<ResearchDef>.Find("Agriculture");
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(DataBase<BuildingDef>.Find("WoodHouse")), ExpantaNum.One);
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            host.GetComponent<ResourceManager>().SetAmount(resource, ExpantaNum.Zero);
        tutorial.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            StagesInitialized = true,
            FarmGoalStageSchemaApplied = true,
            CompletedStageIds = new List<string> { "resources", "building", "population" },
            ActiveStageId = "research", StageTargetId = agriculture.Id
        }, TechLevel.Animal);
        tutorial.RecordStagePageVisited("Research");
        tutorial.RecordStageTargetViewed("Research", agriculture.Id);
        Assert.That(research.EnqueueResearch(agriculture), Is.True);

        TutorialStageSnapshot waiting = tutorial.EvaluateStage();
        Assert.That(waiting.Action, Is.EqualTo(TutorialStageAction.Inspect));
        Assert.That(waiting.CanInteract, Is.False, "Waiting in the queue is not a completed tutorial action.");
        Assert.That(waiting.Instruction, Does.Contain(tutorial.Evaluate().NextGoal));
        tutorial.AcknowledgeCurrentStage();
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain("research"));
        Assert.That(research.IsQueued(agriculture), Is.True);
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

    [Test]
    public void UnaffordableHomeRemainsTheSharedGoalAndFailedBuildCannotCompleteIt()
    {
        SetResources(ExpantaNum.Zero);
        AssertUnifiedAction("building", "Buildings", "WoodHouse");
        TutorialSnapshot before = tutorial.Evaluate();
        Assert.That(before.CanAct, Is.False);
        Assert.That(before.IsWaiting, Is.True);
        Assert.That(before.Benefit, Is.Not.Empty);
        Assert.That(before.NextGoal, Is.Not.Empty);
        Assert.That(host.GetComponent<BuildingManager>().TryBuild(DataBase<BuildingDef>.Find("WoodHouse"), ExpantaNum.One, out _), Is.False);
        tutorial.AcknowledgeCurrentStage();
        Assert.That(tutorial.CompletedStepIds, Does.Not.Contain("building"));
        AssertUnifiedAction("building", "Buildings", "WoodHouse");
    }

    [Test]
    public void UnaffordableHomeExplainsNavigationBeforeShowingTheWait()
    {
        TutorialSnapshot action = tutorial.Evaluate();
        Assert.That(action.CanAct, Is.False);
        TutorialStageSnapshot navigate = tutorial.EvaluateStage();
        Assert.That(navigate.Action, Is.EqualTo(TutorialStageAction.Navigate));
        Assert.That(navigate.Instruction, Does.Contain(action.Benefit));
        tutorial.RecordStagePageVisited(navigate.Page);
        tutorial.RecordStageTargetViewed(navigate.Page, navigate.TargetId);
        TutorialStageSnapshot waiting = tutorial.EvaluateStage();
        Assert.That(waiting.CanInteract, Is.False);
        Assert.That(waiting.Instruction, Does.Contain(action.Blocker).And.Contain(action.NextGoal));
        Assert.That(waiting.Instruction, Is.Not.EqualTo(navigate.Instruction));
    }

    [Test]
    public void PopulationGrowthIsParallelToAgricultureAndDoesNotReplaceItsTarget()
    {
        BuildHome();
        Assert.That(TutorialManager.HasObservedPopulationGrowth(game.State), Is.False);
        AssertUnifiedAction("research", "Research", "Agriculture");
        game.State.Population.RestorePopulationForEditor(ExpantaNum.One);
        AssertUnifiedAction("research", "Research", "Agriculture");
        Assert.That(tutorial.CompletedStepIds, Does.Contain("population"));
        Assert.That(tutorial.CompletedStageIds, Does.Contain("population"));
    }

    [Test]
    public void PaidResearchWaitsForCompletionAndAgricultureImmediatelyTargetsFarm()
    {
        BuildHome();
        SetResources(new ExpantaNum(10000));
        ResearchManager research = host.GetComponent<ResearchManager>();
        ResearchDef agriculture = DataBase<ResearchDef>.Find("Agriculture");
        AssertUnifiedAction("research", "Research", agriculture.Id);
        Assert.That(research.EnqueueResearch(agriculture), Is.True);
        ResearchState state = research.GetState(agriculture);
        Assert.That(state.CostPaid, Is.True);
        Assert.That(state.Status, Is.EqualTo(ResearchStatus.Researching));
        Assert.That(tutorial.Evaluate().IsWaiting, Is.True);
        Assert.That(tutorial.EvaluateStage().Instruction, Does.Contain(tutorial.Evaluate().NextGoal));
        Assert.That(tutorial.Evaluate().RecommendedAction, Does.Contain("进度与材料").And.Not.Contain("加入研究队列"));
        tutorial.AcknowledgeCurrentStage();
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain("research"));
        Assert.That(tutorial.CompletedStepIds, Does.Not.Contain("research"));
        AssertUnifiedAction("research", "Research", agriculture.Id);
        FinishActiveResearch();
        Assert.That(state.Status, Is.EqualTo(ResearchStatus.Completed));
        AssertUnifiedAction("farm-goal", "Buildings", "Farm");
        Assert.That(tutorial.CompletedStageIds, Does.Contain("research"));
    }

    [Test]
    public void FarmRequiresAPositiveProductionTickAndZeroEfficiencyCannotCompleteIt()
    {
        BuildHome();
        game.State.Population.RestorePopulationForEditor(ExpantaNum.One);
        SetResearchCompleted("Agriculture");
        BuildingManager buildings = host.GetComponent<BuildingManager>();
        BuildingState farm = buildings.EnsureBuilding(DataBase<BuildingDef>.Find("Farm"));
        buildings.SetAmountAndRatesForEditor(farm, ExpantaNum.One);
        AssertUnifiedAction("farm-goal", "Buildings", "Farm");
        Assert.That(tutorial.CompletedStepIds, Does.Not.Contain("farm-supply"));
        Assert.That(tutorial.Evaluate().RecommendedAction, Does.Contain("实际产出").And.Not.Contain("建造 1 座"));
        buildings.GlobalEfficiencyFactor = ExpantaNum.Zero;
        Tick(1d);
        Assert.That(farm.Efficiency <= ExpantaNum.Zero, Is.True);
        tutorial.AcknowledgeCurrentStage();
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain("farm-goal"));
        Assert.That(tutorial.CompletedStepIds, Does.Not.Contain("farm-supply"));
        AssertUnifiedAction("farm-goal", "Buildings", "Farm");
        buildings.GlobalEfficiencyFactor = ExpantaNum.One;
        Tick(1d);
        Assert.That(farm.Efficiency > ExpantaNum.Zero, Is.True);
        Assert.That(game.State.FoodProductionRate > GameState.BaseFoodProductionRate, Is.True);
        AssertUnifiedAction("stone-chain", "Research", "Quarry");
        Assert.That(tutorial.CompletedStageIds, Does.Contain("farm-goal"));
    }

    [Test]
    public void FarmWithoutResidentsExplainsHowToStartProduction()
    {
        BuildHome();
        SetResearchCompleted("Agriculture");
        BuildingManager buildings = host.GetComponent<BuildingManager>();
        BuildingDef farm = DataBase<BuildingDef>.Find("Farm");
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(farm), ExpantaNum.One);
        Tick(1d);
        TutorialSnapshot action = tutorial.Evaluate();
        Assert.That(action.ActionId, Is.EqualTo("produce:Farm")); // Stable action identity.
        Assert.That(buildings.GetLaborOperatingRatio(farm) <= ExpantaNum.Zero, Is.True);
        Assert.That(action.Blocker, Does.Contain("入住"));
        Assert.That(tutorial.EvaluateStage().Instruction, Does.Contain(action.Blocker));
        Assert.That(tutorial.CompletedStepIds, Does.Not.Contain("farm-supply"));
        game.State.Population.RestorePopulationForEditor(ExpantaNum.One);
        Tick(1d);
        Assert.That(tutorial.Evaluate().StepId, Is.EqualTo("stone-chain"));
    }

    [Test]
    public void StoneChainRecognizesEarlyResearchAndBuildingsButRequiresRealOutput()
    {
        BuildHome();
        game.State.Population.RestorePopulationForEditor(ExpantaNum.One);
        SetResources(new ExpantaNum(10000));
        SetResearchCompleted("Agriculture");
        RestoreProductionResults("farm-supply");
        AssertUnifiedAction("stone-chain", "Research", "Quarry");
        SetResearchCompleted("Quarry");
        AssertUnifiedAction("stone-chain", "Buildings", "Quarry");
        BuildingManager buildings = host.GetComponent<BuildingManager>();
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(DataBase<BuildingDef>.Find("Quarry")), ExpantaNum.One);
        ResearchDef processing = DataBase<ResearchDef>.Find("StoneProcessing");
        CompleteFixturePrerequisites(processing);
        AssertUnifiedAction("stone-chain", "Research", processing.Id);
        SetResearchCompleted(processing.Id);
        AssertUnifiedAction("stone-chain", "Buildings", "StoneCuttingWorkshop");
        BuildingDef workshop = DataBase<BuildingDef>.Find("StoneCuttingWorkshop");
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(workshop), ExpantaNum.One);
        ResourceDef product = workshop.ResourceGenerationRates[0].First;
        host.GetComponent<ResourceManager>().SetAmount(product, ExpantaNum.Zero);
        Assert.That(tutorial.Evaluate().ActionId, Is.EqualTo("produce:StoneCuttingWorkshop"));
        Assert.That(tutorial.CompletedStepIds, Does.Not.Contain("stone-supply"));
        buildings.GlobalEfficiencyFactor = ExpantaNum.Zero;
        Tick(1d);
        Assert.That(tutorial.CompletedStepIds, Does.Not.Contain("stone-supply"));
        buildings.GlobalEfficiencyFactor = ExpantaNum.One;
        Tick(1d);
        Assert.That(host.GetComponent<ResourceManager>().GetAmount(product) > ExpantaNum.Zero, Is.True);
        Assert.That(tutorial.Evaluate().StepId, Is.EqualTo("first-transition"));
        Assert.That(tutorial.CompletedStageIds, Does.Contain("stone-chain"));
    }

    [Test]
    public void SettlementMaterialWaitTeachesFirstSourceEvenWithPositiveBaseIncome()
    {
        BuildHome();
        SetResearchCompleted("Agriculture");
        CompleteFixturePrerequisites(DataBase<ResearchDef>.Find("StoneAgeSettlement"));
        CompleteFixturePrerequisites(DataBase<ResearchDef>.Find("Woodworking"));
        SetResearchCompleted("Woodworking");
        RestoreProductionResults("farm-supply", "stone-supply");
        ResourceManager resources = host.GetComponent<ResourceManager>();
        ResourceDef wood = DataBase<ResourceDef>.Find("WoodLog");
        SetResources(new ExpantaNum(10000));
        resources.SetAmount(wood, new ExpantaNum(500));
        BuildingManager buildings = host.GetComponent<BuildingManager>();
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(DataBase<BuildingDef>.Find("WoodHouse")), new ExpantaNum(4));
        Assert.That(resources.States[wood].ProductionRate > resources.States[wood].ConsumptionRate, Is.True);
        AssertUnifiedAction("first-transition", "Buildings", "Lumberyard");
        Assert.That(tutorial.Evaluate().NextGoal, Does.Contain(wood.Label));
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(DataBase<BuildingDef>.Find("Lumberyard")), ExpantaNum.One);
        AssertUnifiedAction("first-transition", "Research", "StoneAgeSettlement");
        SetResources(new ExpantaNum(10000));
        Assert.That(host.GetComponent<ResearchManager>().EnqueueResearch(DataBase<ResearchDef>.Find("StoneAgeSettlement")), Is.True);
        resources.SetAmount(wood, ExpantaNum.Zero);
        AssertUnifiedAction("first-transition", "Research", "StoneAgeSettlement");
        Assert.That(tutorial.Evaluate().CanAct, Is.False, "A paid research keeps its progress instead of redirecting to more buildings.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ResearchWaitIdentifiesTheLastMaterialToBecomeReady(bool stopped)
    {
        BuildHome();
        ResearchDef settlement = DataBase<ResearchDef>.Find("StoneAgeSettlement");
        CompleteFixturePrerequisites(settlement);
        RestoreProductionResults("farm-supply", "stone-supply");
        SetResources(new ExpantaNum(10000));
        BuildingManager buildings = host.GetComponent<BuildingManager>();
        foreach (string id in new[] { "Lumberyard", "Quarry", "ClayPit", "FiberGatheringCamp" })
            buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(DataBase<BuildingDef>.Find(id)), new ExpantaNum(2));
        ResourceManager resources = host.GetComponent<ResourceManager>();
        ResourceDef wood = DataBase<ResourceDef>.Find("WoodLog");
        ResourceDef stone = DataBase<ResourceDef>.Find("StoneChunk");
        foreach (Pair<ResourceDef, ExpantaNum> cost in settlement.ResourceRequirements)
            if (cost.First == wood || cost.First == stone)
                resources.SetAmount(cost.First, cost.Second - new ExpantaNum(100));
        resources.SetProductionRate(wood, new ExpantaNum(10));
        resources.SetConsumptionRate(wood, ExpantaNum.Zero);
        resources.SetProductionRate(stone, stopped ? ExpantaNum.Zero : ExpantaNum.One);
        resources.SetConsumptionRate(stone, ExpantaNum.Zero);
        TutorialSnapshot action = tutorial.Evaluate();
        Assert.That(action.NavigationTargetId, Is.EqualTo(settlement.Id)); // Stable research identity.
        Assert.That(action.Blocker, Does.Contain(stone.Label).And.Not.Contain("还缺" + wood.Label),
            "A shorter wait must not hide the material that determines readiness, including a stopped source.");
        if (stopped)
        {
            Assert.That(action.Blocker, Does.Not.Contain("秒备齐"));
            Assert.That(action.RecommendedAction, Does.Not.Contain("暂离"),
                "A stopped source needs repair guidance instead of offline-wait advice.");
        }
        else
        {
            Assert.That(action.RecommendedAction, Does.Contain("查看缺料来源").And.Contain("暂离"));
            resources.SetAmount(stone, new ExpantaNum(10000));
            Assert.That(tutorial.Evaluate().Blocker, Does.Contain(wood.Label));
        }
    }

    [Test]
    public void SettlementStorageAddsUpstreamSupplyWhenProcessingUsesItsBaseOutput()
    {
        BuildHome();
        CompleteFixturePrerequisites(DataBase<ResearchDef>.Find("StoneAgeSettlement"));
        RestoreProductionResults("farm-supply", "stone-supply");
        SetResources(new ExpantaNum(10000));
        BuildingManager buildings = host.GetComponent<BuildingManager>();
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(DataBase<BuildingDef>.Find("WoodHouse")), new ExpantaNum(4));
        foreach (string id in new[] { "Farm", "Quarry", "StoneCuttingWorkshop" })
            buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(DataBase<BuildingDef>.Find(id)), ExpantaNum.One);
        game.State.Population.RestorePopulationForEditor(new ExpantaNum(5));
        Tick(1d);
        ResourceDef stone = DataBase<ResourceDef>.Find("StoneChunk");
        ResourceManager resources = host.GetComponent<ResourceManager>();
        resources.SetAmount(stone, new ExpantaNum(500));
        Assert.That(resources.States[stone].ProductionRate * game.State.HappinessRewardMultiplier > resources.States[stone].ConsumptionRate, Is.True);
        AssertUnifiedAction("first-transition", "Buildings", "Quarry");
        Assert.That(buildings.TryBuild(DataBase<BuildingDef>.Find("Quarry"), ExpantaNum.One, out _), Is.True);
        AssertUnifiedAction("first-transition", "Research", "StoneAgeSettlement");
    }

    [Test]
    public void InstalledUpstreamSurplusDoesNotAddBuildingsWhileResidentsAreArriving()
    {
        BuildHome();
        CompleteFixturePrerequisites(DataBase<ResearchDef>.Find("StoneAgeSettlement"));
        RestoreProductionResults("farm-supply", "stone-supply");
        SetResources(new ExpantaNum(10000));
        BuildingManager buildings = host.GetComponent<BuildingManager>();
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(DataBase<BuildingDef>.Find("WoodHouse")), new ExpantaNum(4));
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(DataBase<BuildingDef>.Find("Quarry")), new ExpantaNum(2));
        buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(DataBase<BuildingDef>.Find("StoneCuttingWorkshop")), ExpantaNum.One);
        game.State.Population.RestorePopulationForEditor(ExpantaNum.Zero);
        Tick(1d);
        host.GetComponent<ResourceManager>().SetAmount(DataBase<ResourceDef>.Find("StoneChunk"), new ExpantaNum(500));
        for (int refresh = 0; refresh < 3; refresh++)
        {
            AssertUnifiedAction("first-transition", "Research", "StoneAgeSettlement");
            Assert.That(tutorial.Evaluate().CanAct, Is.False);
            Assert.That(tutorial.Evaluate().RecommendedAction, Does.Contain("建筑页"));
        }
    }

    [Test]
    public void FirstSourceExpansionAddsHousingCapacityOnceThenReturnsToTheSource()
    {
        BuildHome();
        SetResearchCompleted("Agriculture");
        CompleteFixturePrerequisites(DataBase<ResearchDef>.Find("StoneAgeSettlement"));
        CompleteFixturePrerequisites(DataBase<ResearchDef>.Find("Woodworking"));
        SetResearchCompleted("Woodworking");
        RestoreProductionResults("farm-supply", "stone-supply");
        SetResources(new ExpantaNum(10000));
        host.GetComponent<ResourceManager>().SetAmount(DataBase<ResourceDef>.Find("WoodLog"), new ExpantaNum(500));
        BuildingManager buildings = host.GetComponent<BuildingManager>();
        foreach (string id in new[] { "Farm", "Quarry", "StoneCuttingWorkshop" })
            buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(DataBase<BuildingDef>.Find(id)), ExpantaNum.One);
        game.State.Population.RestorePopulationForEditor(new ExpantaNum(5));
        AssertUnifiedAction("first-transition", "Buildings", "WoodHouse");
        Assert.That(tutorial.Evaluate().Benefit, Does.Contain("入住"));
        Assert.That(buildings.TryBuild(DataBase<BuildingDef>.Find("WoodHouse"), ExpantaNum.One, out _), Is.True);
        AssertUnifiedAction("first-transition", "Buildings", "Lumberyard");
        Assert.That(game.State.Population.Population < game.State.Population.PopulationCapacity, Is.True,
            "Vacant housing is enough to continue; the lesson must not repeatedly add houses while residents are arriving.");
    }

    [Test]
    public void SettlementFollowsRealPrerequisitesThenPlanningAndTheEraTransition()
    {
        BuildHome();
        SetResources(new ExpantaNum(100000));
        SetResearchCompleted("Agriculture");
        RestoreProductionResults("farm-supply", "stone-supply");
        ResearchManager research = host.GetComponent<ResearchManager>();
        TutorialSnapshot prerequisite = tutorial.Evaluate();
        Assert.That(prerequisite.StepId, Is.EqualTo("first-transition"));
        ResearchDef target = DataBase<ResearchDef>.Find(prerequisite.NavigationTargetId);
        Assert.That(research.ArePrerequisitesCompleted(target), Is.True);
        Assert.That(research.IsResearchCompleted(target.Id), Is.False);
        ResearchDef settlement = DataBase<ResearchDef>.Find("StoneAgeSettlement");
        Assert.That(target, Is.Not.SameAs(settlement));
        Assert.That(prerequisite.NextGoal, Does.Contain(settlement.Label),
            "Completing a prerequisite must still point to the unfinished settlement research.");
        CompleteFixturePrerequisites(settlement);
        AssertUnifiedAction("first-transition", "Research", settlement.Id);
        SetResearchCompleted(settlement.Id);
        AssertUnifiedAction("first-transition", "Research", "CivilizationPlanning");
        SetResearchCompleted("CivilizationPlanning");
        AssertUnifiedAction("first-transition", "Era", "StoneAgeTransition");
        Assert.That(game.State.TechLevel, Is.EqualTo(TechLevel.Animal)); // Era protocol.
    }

    [Test]
    public void ProductionResultsSurviveSaveAndRestoreRebindsWithoutDuplicateProgress()
    {
        BuildHome();
        SetResources(new ExpantaNum(10000));
        SetResearchCompleted("Agriculture");
        RestoreProductionResults("farm-supply", "stone-supply");
        TutorialStageSnapshot stage = tutorial.EvaluateStage();
        tutorial.RecordStagePageVisited(stage.Page);
        tutorial.RecordStageTargetViewed(stage.Page, stage.TargetId);
        SaveManager.TutorialSaveData saved = tutorial.CaptureSaveData();
        tutorial.RestoreSaveData(saved, TechLevel.Animal);
        TutorialStageSnapshot restored = tutorial.EvaluateStage();
        Assert.That(restored.Action, Is.EqualTo(TutorialStageAction.Navigate));
        Assert.That(restored.TargetId, Is.EqualTo(stage.TargetId));
        Assert.That(tutorial.CompletedStepIds, Does.Contain("farm-supply").And.Contain("stone-supply"));
        tutorial.Evaluate();
        int version = tutorial.Version;
        int resultCount = tutorial.CompletedStepIds.Count;
        tutorial.Evaluate();
        tutorial.EvaluateStage();
        Assert.That(tutorial.Version, Is.EqualTo(version), "Observing the same results must not republish progress.");
        Assert.That(tutorial.CompletedStepIds.Count, Is.EqualTo(resultCount)); // Discrete completed-result count.
    }

    [Test]
    public void CompletedFeedbackHasOneStableResultIdAndNewGameClearsPreviousProgress()
    {
        BuildHome();
        TutorialSnapshot first = tutorial.Evaluate();
        Assert.That(first.CompletedFeedbackId, Is.EqualTo("building")); // Result identity used for notification deduplication.
        Assert.That(first.CompletedFeedback, Is.Not.Empty);
        int version = tutorial.Version;
        TutorialSnapshot repeated = tutorial.Evaluate();
        Assert.That(repeated.CompletedFeedbackId, Is.EqualTo(first.CompletedFeedbackId));
        Assert.That(tutorial.Version, Is.EqualTo(version));
        game.InitializeNewGameForEditor();
        tutorial.ResetForNewGameForEditor();
        TutorialSnapshot fresh = tutorial.Evaluate();
        Assert.That(fresh.CompletedFeedbackId, Is.Empty);
        Assert.That(fresh.CompletedFeedback, Is.Empty);
    }

    [Test]
    public void ZeroSecondTickCannotRecordFarmOrStoneProduction()
    {
        BuildHome();
        game.State.Population.RestorePopulationForEditor(ExpantaNum.One);
        SetResources(new ExpantaNum(10000));
        SetResearchCompleted("Agriculture");
        BuildingManager buildings = host.GetComponent<BuildingManager>();
        foreach (string id in new[] { "Farm", "Quarry", "StoneCuttingWorkshop" })
            buildings.SetAmountAndRatesForEditor(buildings.EnsureBuilding(DataBase<BuildingDef>.Find(id)), ExpantaNum.One);
        Tick(0d);
        Assert.That(buildings.GetState(DataBase<BuildingDef>.Find("Farm")).Efficiency > ExpantaNum.Zero, Is.True);
        Assert.That(buildings.GetState(DataBase<BuildingDef>.Find("StoneCuttingWorkshop")).Efficiency > ExpantaNum.Zero, Is.True);
        Assert.That(tutorial.CompletedStepIds, Does.Not.Contain("farm-supply").And.Not.Contain("stone-supply"),
            "Positive current rates and existing stock cannot turn a zero-duration refresh into production.");
        Tick(1d);
        Assert.That(tutorial.CompletedStepIds, Does.Contain("farm-supply").And.Contain("stone-supply"));
    }

    [Test]
    public void ResourceSourceResearchInProgressCannotRestoreTheInteractionGate()
    {
        BuildHome();
        SetResources(new ExpantaNum(10000));
        ResearchManager research = host.GetComponent<ResearchManager>();
        ResearchDef settlement = DataBase<ResearchDef>.Find("StoneAgeSettlement");
        CompleteFixturePrerequisites(settlement);
        ResearchDef woodworking = DataBase<ResearchDef>.Find("Woodworking");
        CompleteFixturePrerequisites(woodworking);
        RestoreProductionResults("farm-supply", "stone-supply");
        ResourceManager resources = host.GetComponent<ResourceManager>();
        ResourceDef wood = DataBase<ResourceDef>.Find(ResourceManager.StartingResourceId);
        ExpantaNum woodCost = ExpantaNum.Zero;
        foreach (Pair<ResourceDef, ExpantaNum> cost in woodworking.ResourceRequirements)
            if (cost.First == wood) woodCost = cost.Second;
        Assert.That(woodCost > ExpantaNum.Zero, Is.True);
        resources.SetAmount(wood, woodCost);
        resources.SetProductionRate(wood, ExpantaNum.Zero);
        resources.SetConsumptionRate(wood, ExpantaNum.Zero);
        Assert.That(research.EnqueueResearch(woodworking), Is.True);
        Assert.That(research.ActiveResearch.Definition, Is.SameAs(woodworking));
        Assert.That(research.ActiveResearch.CostPaid, Is.True);
        TutorialSnapshot action = tutorial.Evaluate();
        Assert.That(action.NavigationTargetId, Is.EqualTo(woodworking.Id)); // Resource source prerequisite ID.
        Assert.That(action.RecommendedAction, Does.Contain("进度与材料").And.Not.Contain("加入研究队列"));
        Assert.That(action.NavigationPage, Is.EqualTo("Research"));
        Assert.That(action.CanAct, Is.False);
        Assert.That(action.IsWaiting, Is.True);
        TutorialStageSnapshot stage = tutorial.EvaluateStage();
        Assert.That(stage.Action, Is.EqualTo(TutorialStageAction.Inspect));
        Assert.That(stage.CanInteract, Is.False);
        tutorial.AcknowledgeCurrentStage();
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain("first-transition"));
    }

    [Test]
    public void FirstHomePaymentCompletesIntegratedResourcesAndKeepsStoryProgress()
    {
        tutorial.Evaluate();
        Assert.That(tutorial.CompletedStepIds, Does.Not.Contain("resources"));
        BuildingManager buildings = host.GetComponent<BuildingManager>();
        BuildingDef home = DataBase<BuildingDef>.Find("WoodHouse");
        Assert.That(buildings.TryBuild(home, ExpantaNum.One, out _), Is.False);
        tutorial.Evaluate();
        Assert.That(tutorial.CompletedStepIds, Does.Not.Contain("resources"));
        SetResources(new ExpantaNum(1000));
        Assert.That(buildings.TryBuild(home, ExpantaNum.One, out _), Is.True);
        tutorial.Evaluate();
        Assert.That(tutorial.CompletedStepIds, Does.Contain("resources"));
        Assert.That(tutorial.CompletedStageIds, Does.Contain("resources"));
        Assert.That(StoryManager.GetProgressSignature(TechLevel.Animal, tutorial), Does.Contain("|t:resources=1"));
        SaveManager.TutorialSaveData saved = tutorial.CaptureSaveData();
        tutorial.RestoreSaveData(saved, TechLevel.Animal);
        Assert.That(tutorial.CompletedStepIds, Does.Contain("resources"));
        Assert.That(tutorial.EvaluateStage().TargetId, Is.EqualTo("Agriculture")); // Stable target ID.
    }

    [Test]
    public void FirstEraTransitionContinuesToOrdinaryGoalsWithoutResettingThemOnRefresh()
    {
        BuildHome();
        SetResources(new ExpantaNum(100000));
        ResearchManager research = host.GetComponent<ResearchManager>();
        ResearchDef transition = DataBase<ResearchDef>.Find("StoneAgeTransition");
        CompleteFixturePrerequisites(transition);
        RestoreProductionResults("farm-supply", "stone-supply");
        AssertUnifiedAction("first-transition", "Era", transition.Id);
        Assert.That(research.EnqueueResearch(transition), Is.True);
        FinishActiveResearch();
        Assert.That(game.State.TechLevel, Is.EqualTo(TechLevel.StoneAge)); // Exact era protocol.
        TutorialSnapshot next = tutorial.Evaluate();
        Assert.That(next.StepId, Is.EqualTo("long-term")); // Authored successor of era-goal.
        Assert.That(next.ActionId, Is.Empty);
        Assert.That(tutorial.CompletedStepIds, Does.Contain("era-goal"));
        Assert.That(tutorial.CompletedStageIds, Does.Contain("first-transition"));
        Assert.That(tutorial.CompletedStageIds, Does.Contain("era-goal"));
        Assert.That(tutorial.EvaluateStage(), Is.Null, "First-session operation lessons must not replay after the transition.");
        int version = tutorial.Version;
        for (int i = 0; i < 3; i++)
        {
            Assert.That(tutorial.Evaluate().StepId, Is.EqualTo(next.StepId));
            Assert.That(tutorial.ActiveStepId, Is.EqualTo(next.StepId));
        }
        Assert.That(tutorial.Version, Is.EqualTo(version));
        SaveManager.TutorialSaveData saved = tutorial.CaptureSaveData();
        tutorial.RestoreSaveData(saved, game.State.TechLevel);
        Assert.That(tutorial.Evaluate().StepId, Is.EqualTo(next.StepId));
    }

    private void AssertUnifiedAction(string stageId, string page, string targetId)
    {
        TutorialSnapshot overview = tutorial.Evaluate();
        TutorialStageSnapshot overlay = tutorial.EvaluateStage();
        Assert.That(overlay, Is.Not.Null);
        Assert.That(overview.StepId, Is.EqualTo(stageId));
        Assert.That(overview.NavigationPage, Is.EqualTo(page));
        Assert.That(overview.NavigationTargetId, Is.EqualTo(targetId));
        Assert.That(overlay.StageId, Is.EqualTo(overview.StepId));
        Assert.That(overlay.Page, Is.EqualTo(overview.NavigationPage));
        Assert.That(overlay.TargetId, Is.EqualTo(overview.NavigationTargetId));
    }

    private void SetResources(ExpantaNum amount)
    {
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            host.GetComponent<ResourceManager>().SetAmount(resource, amount);
    }

    private void BuildHome() => host.GetComponent<BuildingManager>().SetAmountAndRatesForEditor(
        host.GetComponent<BuildingManager>().EnsureBuilding(DataBase<BuildingDef>.Find("WoodHouse")), ExpantaNum.One);

    private void SetResearchCompleted(string id) => host.GetComponent<ResearchManager>()
        .GetState(DataBase<ResearchDef>.Find(id)).SetStatusForEditor(ResearchStatus.Completed);

    private void CompleteFixturePrerequisites(ResearchDef research)
    {
        foreach (ResearchDef prerequisite in research.Prerequisites)
        {
            CompleteFixturePrerequisites(prerequisite);
            SetResearchCompleted(prerequisite.Id);
        }
    }

    private void RestoreProductionResults(params string[] ids) => tutorial.RestoreSaveData(
        new SaveManager.TutorialSaveData
        {
            StagesInitialized = true, FarmGoalStageSchemaApplied = true,
            CompletedStepIds = new List<string>(ids)
        }, TechLevel.Animal);

    private void FinishActiveResearch()
    {
        ResearchManager research = host.GetComponent<ResearchManager>();
        Assert.That(research.CurrentResearchSpeed > ExpantaNum.Zero, Is.True);
        double seconds = ((research.ActiveResearch.BaseCost - research.ActiveResearch.Progress) / research.CurrentResearchSpeed).ToDouble();
        research.Tick(seconds + .01d);
    }

    private void Tick(double seconds)
    {
        SimulationManager simulation = host.GetComponent<SimulationManager>();
        if (simulation == null) simulation = host.AddComponent<SimulationManager>();
        simulation.ManualTick(seconds);
    }

}
