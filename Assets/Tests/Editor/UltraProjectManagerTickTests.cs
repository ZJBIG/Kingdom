using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class UltraProjectManagerTickTests
{
    private const double RelativeTolerance = 1e-8d;

    private GameObject managersObject;
    private GameManager gameManager;
    private ResourceManager resourceManager;
    private BuildingManager buildingManager;
    private ResearchManager researchManager;
    private UltraProjectManager projectManager;
    private UltraProjectStageDefinition firstStage;

    [SetUp]
    public void SetUp()
    {
        managersObject = new GameObject("UltraProjectManagerTickTests");
        gameManager = managersObject.AddComponent<GameManager>();
        resourceManager = managersObject.AddComponent<ResourceManager>();
        buildingManager = managersObject.AddComponent<BuildingManager>();
        researchManager = managersObject.AddComponent<ResearchManager>();

        gameManager.State.RestoreCoreForEditor(
            0,
            TechLevel.Ultra,
            new ExpantaNum(1e12d),
            0L);
        gameManager.State.SetPowerSatisfactionForEditor(ExpantaNum.One);
        gameManager.State.SetLogisticsSatisfactionForEditor(ExpantaNum.One);
        projectManager = gameManager.UltraProject;
        firstStage = projectManager.Definition.Stages[0];

        // MonoBehaviour.Initialize is not invoked when managers are created
        // directly in an EditMode fixture. Create the research state table
        // before completing the Ultra prerequisite through the public editor
        // seam.
        researchManager.InitializeForEditor();
        CompleteResearch(firstStage.RequiredResearch);
        buildingManager.EnsureBuilding(firstStage.RequiredBuilding)
            .SetAmountForEditor(ExpantaNum.One);
    }

    [TearDown]
    public void TearDown()
    {
        if (managersObject != null)
            Object.DestroyImmediate(managersObject);
    }

    [Test]
    public void Tick_PartialAdvancedMaterialSupplyProgressesAndOnlyPausesWhenExhausted()
    {
        RestoreRunningProject(ExpantaNum.Zero);
        Pair<Resource, ExpantaNum> limitingCost = firstStage.ContinuousResourceCosts[0];
        const double tickSeconds = 30d;
        ExpantaNum available = limitingCost.Second * tickSeconds * 0.4d;
        SetAllContinuousResources(new ExpantaNum(1e9d));
        resourceManager.SetAmount(limitingCost.First, available);

        ExpantaNum foodBefore = gameManager.State.FoodAmount;
        ExpantaNum otherResourceBefore = resourceManager.GetAmount(
            firstStage.ContinuousResourceCosts[1].First);
        ExpantaNum fullProgressUpperBound = new ExpantaNum(tickSeconds) /
            firstStage.BaseDurationSeconds * firstStage.StablePostureMultiplier;

        Assert.That(projectManager.Tick(tickSeconds), Is.True);

        Assert.That(projectManager.State.Status, Is.EqualTo(UltraProjectStatus.Running));
        Assert.That(projectManager.State.StageProgress, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(projectManager.State.StageProgress, Is.LessThan(fullProgressUpperBound));
        Assert.That(resourceManager.GetAmount(limitingCost.First),
            Is.LessThanOrEqualTo(available * RelativeTolerance));
        Assert.That(resourceManager.GetAmount(firstStage.ContinuousResourceCosts[1].First),
            Is.LessThan(otherResourceBefore));
        Assert.That(resourceManager.GetAmount(firstStage.ContinuousResourceCosts[1].First),
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(gameManager.State.FoodAmount, Is.LessThan(foodBefore));

        ExpantaNum progressAfterPartialSupply = projectManager.State.StageProgress;
        ExpantaNum foodAfterPartialSupply = gameManager.State.FoodAmount;
        ExpantaNum otherResourceAfterPartialSupply = resourceManager.GetAmount(
            firstStage.ContinuousResourceCosts[1].First);
        // Avoid making the zero-supply boundary depend on floating-point residue.
        resourceManager.SetAmount(limitingCost.First, ExpantaNum.Zero);

        Assert.That(projectManager.Tick(1d), Is.False);
        Assert.That(projectManager.State.Status, Is.EqualTo(UltraProjectStatus.Paused));
        Assert.That(projectManager.State.PauseReason,
            Is.EqualTo(UltraProjectPauseReason.InsufficientSupply));
        Assert.That(projectManager.LastFailure,
            Is.EqualTo(UltraProjectOperationFailure.InsufficientSupply));
        Assert.That(projectManager.State.StageProgress, Is.EqualTo(progressAfterPartialSupply));
        Assert.That(gameManager.State.FoodAmount, Is.EqualTo(foodAfterPartialSupply));
        Assert.That(resourceManager.GetAmount(firstStage.ContinuousResourceCosts[1].First),
            Is.EqualTo(otherResourceAfterPartialSupply));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Tick_ZeroFoodOrAdvancedMaterialSupplyDoesNotPartiallyCommit(bool foodIsMissing)
    {
        RestoreRunningProject(ExpantaNum.Zero);
        SetAllContinuousResources(new ExpantaNum(1e9d));
        if (foodIsMissing)
        {
            gameManager.State.RestoreCoreForEditor(0, TechLevel.Ultra, ExpantaNum.Zero, 0L);
        }
        else
        {
            resourceManager.SetAmount(firstStage.ContinuousResourceCosts[0].First,
                ExpantaNum.Zero);
        }

        Dictionary<Resource, ExpantaNum> resourceBalances = CaptureContinuousBalances();
        ExpantaNum foodBefore = gameManager.State.FoodAmount;

        Assert.That(projectManager.Tick(30d), Is.False);

        Assert.That(projectManager.State.Status, Is.EqualTo(UltraProjectStatus.Paused));
        Assert.That(projectManager.State.StageProgress, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(projectManager.LastFailure,
            Is.EqualTo(UltraProjectOperationFailure.InsufficientSupply));
        Assert.That(gameManager.State.FoodAmount, Is.EqualTo(foodBefore));
        foreach (KeyValuePair<Resource, ExpantaNum> entry in resourceBalances)
            Assert.That(resourceManager.GetAmount(entry.Key), Is.EqualTo(entry.Value));
    }

    [Test]
    public void TryStartStage_InsufficientStartupMaterialLeavesEveryBalanceAndStateUntouched()
    {
        Pair<Resource, ExpantaNum> missingCost = firstStage.OneTimeResourceCosts[
            firstStage.OneTimeResourceCosts.Count - 1];
        for (int i = 0; i < firstStage.OneTimeResourceCosts.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = firstStage.OneTimeResourceCosts[i];
            resourceManager.SetAmount(cost.First,
                cost.First == missingCost.First ? ExpantaNum.Zero : cost.Second * 2d);
        }

        Dictionary<Resource, ExpantaNum> balances = CaptureStartupBalances();
        ExpantaNum foodBefore = gameManager.State.FoodAmount;

        Assert.That(projectManager.TryStartStage(
            UltraProjectDoctrine.Stable,
            out UltraProjectOperationFailure failure), Is.False);

        Assert.That(failure,
            Is.EqualTo(UltraProjectOperationFailure.InsufficientStartupResources));
        Assert.That(projectManager.State.Status, Is.EqualTo(UltraProjectStatus.Locked));
        Assert.That(projectManager.State.StageProgress, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(gameManager.State.FoodAmount, Is.EqualTo(foodBefore));
        foreach (KeyValuePair<Resource, ExpantaNum> entry in balances)
            Assert.That(resourceManager.GetAmount(entry.Key), Is.EqualTo(entry.Value));
    }

    [Test]
    public void TryResume_DoesNotChargeStageStartupFeeAgain()
    {
        for (int i = 0; i < firstStage.OneTimeResourceCosts.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = firstStage.OneTimeResourceCosts[i];
            resourceManager.SetAmount(cost.First, cost.Second * 3d);
        }

        Assert.That(projectManager.TryStartStage(
            UltraProjectDoctrine.Stable,
            out UltraProjectOperationFailure startFailure), Is.True);
        Assert.That(startFailure, Is.EqualTo(UltraProjectOperationFailure.None));
        Assert.That(projectManager.State.LaunchFeePaid, Is.True);
        Assert.That(projectManager.TryPause(out UltraProjectOperationFailure pauseFailure), Is.True);
        Assert.That(pauseFailure, Is.EqualTo(UltraProjectOperationFailure.None));

        Dictionary<Resource, ExpantaNum> balancesAfterStartup = CaptureStartupBalances();
        ExpantaNum foodAfterStartup = gameManager.State.FoodAmount;
        ExpantaNum progressAfterStartup = projectManager.State.StageProgress;

        Assert.That(projectManager.TryResume(out UltraProjectOperationFailure resumeFailure), Is.True);
        Assert.That(resumeFailure, Is.EqualTo(UltraProjectOperationFailure.None));

        Assert.That(projectManager.State.Status, Is.EqualTo(UltraProjectStatus.Running));
        Assert.That(projectManager.State.LaunchFeePaid, Is.True);
        Assert.That(projectManager.State.StageProgress, Is.EqualTo(progressAfterStartup));
        Assert.That(gameManager.State.FoodAmount, Is.EqualTo(foodAfterStartup));
        foreach (KeyValuePair<Resource, ExpantaNum> entry in balancesAfterStartup)
            Assert.That(resourceManager.GetAmount(entry.Key), Is.EqualTo(entry.Value));
    }

    [Test]
    public void TryResume_AfterSupplyPauseRequiresPositiveSupply()
    {
        RestoreRunningProject(ExpantaNum.Zero);
        SetAllContinuousResources(new ExpantaNum(1e9d));
        resourceManager.SetAmount(firstStage.ContinuousResourceCosts[0].First,
            ExpantaNum.Zero);

        Assert.That(projectManager.Tick(1d), Is.False);
        Assert.That(projectManager.State.Status, Is.EqualTo(UltraProjectStatus.Paused));

        Assert.That(projectManager.TryResume(
            out UltraProjectOperationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(UltraProjectOperationFailure.InsufficientSupply));
        Assert.That(projectManager.State.Status, Is.EqualTo(UltraProjectStatus.Paused));
        Assert.That(projectManager.State.PauseReason,
            Is.EqualTo(UltraProjectPauseReason.InsufficientSupply));
    }

    [Test]
    public void Tick_CompletionBoundaryCapsContinuousPaymentsAtStageRequirements()
    {
        ExpantaNum startingProgress = new ExpantaNum(0.99d);
        RestoreRunningProject(startingProgress);
        SetAllContinuousResources(new ExpantaNum(1e12d));
        double requestedSeconds = firstStage.BaseDurationSeconds.ToDouble() * 20d;
        ExpantaNum billableSeconds =
            (ExpantaNum.One - startingProgress) * firstStage.BaseDurationSeconds /
            firstStage.StablePostureMultiplier;
        ExpantaNum foodBefore = gameManager.State.FoodAmount;
        Dictionary<Resource, ExpantaNum> balances = CaptureContinuousBalances();

        Assert.That(projectManager.Tick(requestedSeconds), Is.True);

        Assert.That(projectManager.State.Status, Is.EqualTo(UltraProjectStatus.ReadyToCommit));
        Assert.That(projectManager.State.StageProgress, Is.EqualTo(ExpantaNum.One));
        ExpantaNum foodSpent = foodBefore - gameManager.State.FoodAmount;
        ExpantaNum expectedFoodSpend = firstStage.FoodConsumptionRate * billableSeconds;
        ExpantaNum foodTolerance = expectedFoodSpend * 0.05d;
        Assert.That(foodSpent, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(foodSpent,
            Is.GreaterThanOrEqualTo(expectedFoodSpend - foodTolerance));
        Assert.That(foodSpent,
            Is.LessThanOrEqualTo(expectedFoodSpend + foodTolerance));

        foreach (Pair<Resource, ExpantaNum> cost in firstStage.ContinuousResourceCosts)
        {
            ExpantaNum spent = balances[cost.First] - resourceManager.GetAmount(cost.First);
            ExpantaNum expectedSpend = cost.Second * billableSeconds;
            ExpantaNum tolerance = expectedSpend * 0.05d;
            Assert.That(spent, Is.GreaterThan(ExpantaNum.Zero), cost.First.Id);
            Assert.That(spent, Is.GreaterThanOrEqualTo(expectedSpend - tolerance), cost.First.Id);
            Assert.That(spent, Is.LessThanOrEqualTo(expectedSpend + tolerance), cost.First.Id);
        }
    }

    private void CompleteResearch(Research research)
    {
        ResearchState state = researchManager.GetState(research);
        Dictionary<Resource, ExpantaNum> paidCosts = new();
        for (int i = 0; i < research.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = research.ResourceRequirements[i];
            if (cost.First != null && cost.Second > ExpantaNum.Zero)
                paidCosts[cost.First] = cost.Second;
        }
        state.RestoreForEditor(state.BaseCost, true, true, paidCosts);
    }

    private void RestoreRunningProject(ExpantaNum progress)
    {
        projectManager.RestoreSaveDataForEditor(new UltraProjectStateSaveData
        {
            ProjectId = UltraProjectState.ProjectId,
            SaveVersion = UltraProjectState.CurrentSaveVersion,
            Doctrine = UltraProjectDoctrine.Stable,
            Status = UltraProjectStatus.Running,
            CurrentStage = UltraProjectStage.Prototype,
            StageProgress = progress.ToString(),
            CompletedStages = new List<UltraProjectStage>(),
            LaunchFeePaid = true,
            StateVersion = 1
        });
    }

    private void SetAllContinuousResources(ExpantaNum amount)
    {
        for (int i = 0; i < firstStage.ContinuousResourceCosts.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = firstStage.ContinuousResourceCosts[i];
            resourceManager.SetAmount(cost.First, amount);
        }
    }

    private Dictionary<Resource, ExpantaNum> CaptureContinuousBalances()
    {
        Dictionary<Resource, ExpantaNum> balances = new();
        for (int i = 0; i < firstStage.ContinuousResourceCosts.Count; i++)
        {
            Resource resource = firstStage.ContinuousResourceCosts[i].First;
            balances[resource] = resourceManager.GetAmount(resource);
        }
        return balances;
    }

    private Dictionary<Resource, ExpantaNum> CaptureStartupBalances()
    {
        Dictionary<Resource, ExpantaNum> balances = new();
        for (int i = 0; i < firstStage.OneTimeResourceCosts.Count; i++)
        {
            Resource resource = firstStage.OneTimeResourceCosts[i].First;
            balances[resource] = resourceManager.GetAmount(resource);
        }
        return balances;
    }
}
