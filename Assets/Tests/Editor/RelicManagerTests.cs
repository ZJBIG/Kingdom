using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class RelicManagerTests
{
    private GameObject managersObject;
    private GameManager game;
    private ResourceManager resources;
    private BuildingManager buildings;
    private ResearchManager research;
    private RelicManager relic;
    private RelicDefinition definition;

    [SetUp]
    public void SetUp()
    {
        managersObject = new GameObject("RelicManagerTests");
        game = managersObject.AddComponent<GameManager>();
        resources = managersObject.AddComponent<ResourceManager>();
        buildings = managersObject.AddComponent<BuildingManager>();
        research = managersObject.AddComponent<ResearchManager>();
        game.State.RestoreCoreForEditor(0, TechLevel.Ultra, new ExpantaNum(1e6d), 0L);
        game.State.SetPowerSatisfactionForEditor(ExpantaNum.One);
        game.State.SetLogisticsSatisfactionForEditor(ExpantaNum.One);
        research.InitializeForEditor();
        relic = game.Relic;
        definition = relic.Definition;
        Assert.That(definition, Is.Not.Null);
        for (int i = 0; i < definition.RequiredResearch.Count; i++)
            CompleteResearch(definition.RequiredResearch[i]);
        for (int i = 0; i < definition.RequiredBuildings.Count; i++)
            buildings.EnsureBuilding(definition.RequiredBuildings[i]).SetAmountForEditor(ExpantaNum.One);
        game.Sectors.InitializeDefinitions();
        game.Sectors.GetState(definition.Sector).SetUnlockedForEditor(true);
        game.Sectors.GetState(definition.Sector).SetOccupiedForEditor(true);
        game.UltraProject.State.RestoreForEditor(new UltraProjectStateSaveData
        {
            ProjectId = UltraProjectState.ProjectId,
            SaveVersion = UltraProjectState.CurrentSaveVersion,
            Doctrine = UltraProjectDoctrine.Stable,
            Status = UltraProjectStatus.Ready,
            CurrentStage = UltraProjectStage.Stabilization,
            StageProgress = "0",
            CompletedStages = new List<UltraProjectStage> { UltraProjectStage.Prototype },
            StateVersion = 1
        });
        FundWork(definition.Investigation);
        FundWork(definition.Repair);
        FundWork(definition.ReverseEngineering);
        FundWork(definition.Commission);
        FundCosts(definition.SupportCraftCosts);
    }

    [TearDown]
    public void TearDown()
    {
        if (managersObject != null)
            UnityEngine.Object.DestroyImmediate(managersObject);
    }

    [Test]
    public void StartInvestigation_BeforeUltraRejectsWithoutPayment()
    {
        game.State.RestoreCoreForEditor(0, TechLevel.Spacer, game.State.FoodAmount, 0L);
        AssertStartRejectedWithoutChanges();
    }

    [Test]
    public void StartInvestigation_WithoutOccupiedTargetRejectsWithoutPayment()
    {
        game.Sectors.GetState(definition.Sector).SetOccupiedForEditor(false);
        AssertStartRejectedWithoutChanges();
    }

    [Test]
    public void StartInvestigation_WithoutFirstCertificationRejectsWithoutPayment()
    {
        game.UltraProject.State.InitializeNewForEditor();
        AssertStartRejectedWithoutChanges();
    }

    [Test]
    public void StartInvestigation_WithoutRequiredResearchRejectsWithoutPayment()
    {
        ResearchState required = research.GetState(definition.RequiredResearch[0]);
        required.RestoreForEditor(ExpantaNum.Zero, false, false, new Dictionary<Resource, ExpantaNum>());
        required.SetStatusForEditor(ResearchStatus.Locked);
        AssertStartRejectedWithoutChanges();
    }

    [Test]
    public void StartInvestigation_OneMissingStartupMaterialIsAtomic()
    {
        Assert.That(definition.Investigation.StartupCosts.Count, Is.GreaterThan(0));
        resources.SetAmount(definition.Investigation.StartupCosts[
            definition.Investigation.StartupCosts.Count - 1].First, ExpantaNum.Zero);
        AssertStartRejectedWithoutChanges();
    }

    [TestCase(RelicRoute.Repair)]
    [TestCase(RelicRoute.Dismantle)]
    public void InvestigationAndRoute_CompleteOnceAndNeverSwitchPermanentRoute(RelicRoute route)
    {
        Investigate();
        Assert.That(relic.TryInvestigate(out _), Is.False);
        Assert.That(relic.TryChooseRoute(route, out _), Is.True);
        Assert.That(relic.TryChooseRoute(route == RelicRoute.Repair ? RelicRoute.Dismantle : RelicRoute.Repair, out _), Is.False);
        RelicWorkDefinition work = route == RelicRoute.Repair ? definition.Repair : definition.ReverseEngineering;
        Assert.That(relic.Tick(work.DurationSeconds.ToDouble() * 2d), Is.True);
        Assert.That(relic.State.Status, Is.EqualTo(RelicStatus.Operational));
        Assert.That(relic.State.Route, Is.EqualTo(route));
        int version = relic.State.Version;
        Assert.That(relic.Tick(work.DurationSeconds.ToDouble()), Is.False);
        Assert.That(relic.State.Version, Is.EqualTo(version));
        Assert.That(relic.State.SupportReady, Is.False);
    }

    [Test]
    public void Tick_PartialSupplyAdvancesAndZeroSupplyPausesWithoutSpending()
    {
        Assert.That(relic.TryInvestigate(out _), Is.True);
        Pair<Resource, ExpantaNum> limiting = definition.Investigation.ContinuousCosts[0];
        double seconds = Math.Min(10d, definition.Investigation.DurationSeconds.ToDouble() * .1d);
        resources.SetAmount(limiting.First, limiting.Second * seconds * .4d);
        ExpantaNum foodBefore = game.State.FoodAmount;
        Assert.That(relic.Tick(seconds), Is.True);
        Assert.That(relic.State.Progress, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(relic.State.Progress, Is.LessThan(new ExpantaNum(seconds) / definition.Investigation.DurationSeconds));
        Assert.That(game.State.FoodAmount, Is.LessThan(foodBefore));
        resources.SetAmount(limiting.First, ExpantaNum.Zero);
        RelicStateSaveData before = relic.CaptureSaveDataForEditor();
        Dictionary<Resource, ExpantaNum> balances = Capture(definition.Investigation.ContinuousCosts);
        foodBefore = game.State.FoodAmount;
        Assert.That(relic.Tick(seconds), Is.False);
        Assert.That(relic.State.Suspended, Is.True);
        Assert.That(relic.State.PauseReason, Is.EqualTo(RelicPauseReason.InsufficientSupply));
        Near(relic.State.Progress, ExpantaNum.Parse(before.Progress));
        Near(game.State.FoodAmount, foodBefore);
        AssertBalances(balances);
    }

    [Test]
    public void Tick_LastSliceDoesNotChargeBeyondRemainingWork()
    {
        Assert.That(relic.TryInvestigate(out _), Is.True);
        RelicStateSaveData data = relic.CaptureSaveDataForEditor();
        data.Progress = ".99";
        relic.RestoreSaveDataForEditor(data);
        ExpantaNum remainingSeconds = (ExpantaNum.One - relic.State.Progress) * definition.Investigation.DurationSeconds;
        Dictionary<Resource, ExpantaNum> balances = Capture(definition.Investigation.ContinuousCosts);
        ExpantaNum foodBefore = game.State.FoodAmount;
        Assert.That(relic.Tick(definition.Investigation.DurationSeconds.ToDouble() * 5d), Is.True);
        Assert.That(relic.State.Status, Is.EqualTo(RelicStatus.AwaitingChoice));
        foreach (Pair<Resource, ExpantaNum> cost in definition.Investigation.ContinuousCosts)
            Near(balances[cost.First] - resources.GetAmount(cost.First), cost.Second * remainingSeconds, .05d);
        Near(foodBefore - game.State.FoodAmount, definition.Investigation.FoodConsumptionRate * remainingSeconds, .05d);
    }

    [Test]
    public void ManualSeal_SaveRoundTripResumesProgressWithoutRepayingStartup()
    {
        Assert.That(relic.TryInvestigate(out _), Is.True);
        Assert.That(relic.Tick(definition.Investigation.DurationSeconds.ToDouble() * .1d), Is.True);
        Assert.That(relic.TrySuspend(out _), Is.True);
        RelicStateSaveData data = JsonUtility.FromJson<RelicStateSaveData>(JsonUtility.ToJson(relic.CaptureSaveDataForEditor()));
        Dictionary<Resource, ExpantaNum> balances = Capture(definition.Investigation.StartupCosts);
        relic.RestoreSaveDataForEditor(data);
        Assert.That(relic.Tick(10d), Is.False);
        Assert.That(relic.TryResume(out _), Is.True);
        Near(relic.State.Progress, ExpantaNum.Parse(data.Progress));
        AssertBalances(balances);
    }

    [Test]
    public void RepairCommission_OnlyOnePreparedSupportAndNoDoubleCompletion()
    {
        MakeOperational(RelicRoute.Repair);
        Assert.That(relic.TryBeginCommission(out _), Is.True);
        Assert.That(relic.Tick(definition.Commission.DurationSeconds.ToDouble() * 2d), Is.True);
        Assert.That(relic.State.SupportReady, Is.True);
        Assert.That(relic.State.CompletedCommissions, Is.EqualTo(1));
        Assert.That(relic.TryBeginCommission(out _), Is.False);
        Assert.That(relic.Tick(definition.Commission.DurationSeconds.ToDouble()), Is.False);
        Assert.That(relic.State.CompletedCommissions, Is.EqualTo(1));
        Assert.That(relic.TryCraftSupport(out _), Is.False);
    }

    [Test]
    public void DismantleBlueprint_CraftsSameSingleSupportAndRejectsRepairCommission()
    {
        MakeOperational(RelicRoute.Dismantle);
        Assert.That(relic.TryBeginCommission(out _), Is.False);
        Assert.That(relic.TryCraftSupport(out _), Is.True);
        Assert.That(relic.State.SupportReady, Is.True);
        Dictionary<Resource, ExpantaNum> after = Capture(definition.SupportCraftCosts);
        Assert.That(relic.TryCraftSupport(out _), Is.False);
        AssertBalances(after);
    }

    [Test]
    public void CraftSupport_MissingOneIngredientDoesNotPartiallyPay()
    {
        MakeOperational(RelicRoute.Dismantle);
        resources.SetAmount(definition.SupportCraftCosts[definition.SupportCraftCosts.Count - 1].First, ExpantaNum.Zero);
        Dictionary<Resource, ExpantaNum> balances = Capture(definition.SupportCraftCosts);
        int version = relic.State.Version;
        Assert.That(relic.TryCraftSupport(out _), Is.False);
        Assert.That(relic.State.SupportReady, Is.False);
        Assert.That(relic.State.Version, Is.EqualTo(version));
        AssertBalances(balances);
    }

    [Test]
    public void Support_BindsOnlyMatchingCampaignAndCancellationClearsIt()
    {
        MakeOperational(RelicRoute.Dismantle);
        Assert.That(relic.TryCraftSupport(out _), Is.True);
        SectorDefinition target = DataBase<SectorDefinition>.Find("ProximaB");
        game.State.BeginCampaignForEditor(target.Id);
        Assert.That(relic.TryAssignSupport(out _), Is.True);
        Assert.That(relic.State.SupportReady, Is.False);
        Near(relic.GetCampaignSupplyMultiplier(target), definition.CampaignSupplyMultiplier);
        Assert.That(relic.GetCampaignSupplyMultiplier(target), Is.LessThan(ExpantaNum.One));
        Near(relic.GetCampaignSupplyMultiplier(definition.Sector), ExpantaNum.One);
        Assert.That(relic.TryAssignSupport(out _), Is.False);
        game.State.RestoreCampaignForEditor(false, string.Empty, ExpantaNum.Zero, ExpantaNum.Zero);
        Near(relic.GetCampaignSupplyMultiplier(target), ExpantaNum.One);
        relic.RefreshCampaignSupport();
        Assert.That(relic.State.SupportedSectorId, Is.Empty);
        Assert.That(relic.State.SupportReady, Is.False);
    }

    [Test]
    public void Support_WithoutCampaignCannotConsumePreparedSupport()
    {
        MakeOperational(RelicRoute.Dismantle);
        Assert.That(relic.TryCraftSupport(out _), Is.True);
        int version = relic.State.Version;
        Assert.That(relic.TryAssignSupport(out _), Is.False);
        Assert.That(relic.State.SupportReady, Is.True);
        Assert.That(relic.State.Version, Is.EqualTo(version));
    }

    private void AssertStartRejectedWithoutChanges()
    {
        Dictionary<Resource, ExpantaNum> balances = Capture(definition.Investigation.StartupCosts);
        int version = relic.State.Version;
        Assert.That(relic.TryInvestigate(out _), Is.False);
        Assert.That(relic.State.Status, Is.EqualTo(RelicStatus.Discovered));
        Assert.That(relic.State.Version, Is.EqualTo(version));
        AssertBalances(balances);
    }

    private void Investigate()
    {
        Assert.That(relic.TryInvestigate(out _), Is.True);
        Assert.That(relic.Tick(definition.Investigation.DurationSeconds.ToDouble() * 2d), Is.True);
        Assert.That(relic.State.Status, Is.EqualTo(RelicStatus.AwaitingChoice));
    }

    private void MakeOperational(RelicRoute route)
    {
        Investigate();
        Assert.That(relic.TryChooseRoute(route, out _), Is.True);
        Assert.That(relic.Tick((route == RelicRoute.Repair ? definition.Repair : definition.ReverseEngineering).DurationSeconds.ToDouble() * 2d), Is.True);
    }

    private void CompleteResearch(Research value)
    {
        Dictionary<Resource, ExpantaNum> paid = new();
        foreach (Pair<Resource, ExpantaNum> cost in value.ResourceRequirements)
            if (cost.First != null && cost.Second > ExpantaNum.Zero)
                paid[cost.First] = cost.Second;
        ResearchState state = research.GetState(value);
        state.RestoreForEditor(state.BaseCost, true, true, paid);
    }

    private void FundWork(RelicWorkDefinition work)
    {
        FundCosts(work.StartupCosts);
        FundCosts(work.ContinuousCosts);
    }

    private void FundCosts(IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
    {
        for (int i = 0; i < costs.Count; i++)
            resources.SetAmount(costs[i].First, new ExpantaNum(1e6d));
    }

    private Dictionary<Resource, ExpantaNum> Capture(IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
    {
        Dictionary<Resource, ExpantaNum> result = new();
        for (int i = 0; i < costs.Count; i++)
            result[costs[i].First] = resources.GetAmount(costs[i].First);
        return result;
    }

    private void AssertBalances(Dictionary<Resource, ExpantaNum> balances)
    {
        foreach (KeyValuePair<Resource, ExpantaNum> entry in balances)
            Near(resources.GetAmount(entry.Key), entry.Value);
    }

    private static void Near(ExpantaNum actual, ExpantaNum expected, double tolerance = 1e-8d)
    {
        ExpantaNum margin = ExpantaNum.Max(new ExpantaNum(1e-5d), expected.Abs() * tolerance);
        Assert.That((actual - expected).Abs(), Is.LessThanOrEqualTo(margin));
    }
}
