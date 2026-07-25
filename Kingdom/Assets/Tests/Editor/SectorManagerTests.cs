using NUnit.Framework;
using UnityEngine;

public sealed class SectorManagerTests
{
    [Test]
    public void C704_ManagerUsesStableDefinitionOrderAndPrerequisites()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();

        Assert.That(manager.OrderedStates, Has.Count.EqualTo(3));
        Assert.That(manager.OrderedStates[0].Definition.Id, Is.EqualTo("LowOrbit"));
        Assert.That(manager.OrderedStates[1].Definition.Id, Is.EqualTo("Mars"));
        Assert.That(manager.OrderedStates[2].Definition.Id, Is.EqualTo("Moon"));
        Assert.That(manager.CanAccess(DataBase<SectorDefinition>.Find("LowOrbit")), Is.True);
        Assert.That(manager.CanAccess(DataBase<SectorDefinition>.Find("Moon")), Is.False);
    }

    [Test]
    public void C704_SectorStateRoundTripsByStableId()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        SectorState state = manager.GetState(lowOrbit);
        state.SetUnlockedForEditor(true);
        state.SetOccupiedForEditor(true);
        state.SetCampaignProgressForEditor(new ExpantaNum(0.75d));
        state.SetVisitCountForEditor(3);

        SaveManager.SectorSaveData saved = manager.CaptureSaveData();
        manager.InitializeNew();
        Assert.That(manager.GetState(lowOrbit).Occupied, Is.False);

        manager.RestoreSaveData(saved);
        SectorState restored = manager.GetState(lowOrbit);
        Assert.That(restored.Unlocked, Is.True);
        Assert.That(restored.Occupied, Is.True);
        Assert.That(restored.CampaignProgress.ToDouble(), Is.EqualTo(0.75d).Within(0.000001d));
        Assert.That(restored.VisitCount, Is.EqualTo(3));
    }

    [Test]
    public void C704_NonRepeatableSectorCannotBeOccupiedTwice()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorState state = manager.GetState(DataBase<SectorDefinition>.Find("LowOrbit"));
        state.SetUnlockedForEditor(true);

        Assert.That(manager.TryOccupy(state.Definition, out SectorOperationFailure first), Is.True);
        Assert.That(manager.TryOccupy(state.Definition, out SectorOperationFailure second), Is.False);
        Assert.That(second, Is.EqualTo(SectorOperationFailure.AlreadyOccupied));
        Assert.That(state.VisitCount, Is.EqualTo(1));
    }

    [Test]
    public void C705_SectorRewardsUseExistingStrategicResources()
    {
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorDefinition mars = DataBase<SectorDefinition>.Find("Mars");
        Resource composite = DataBase<Resource>.Find("Composite");
        Resource rocketFuel = DataBase<Resource>.Find("RocketFuel");

        Assert.That(moon.ResourceRewards, Has.Count.EqualTo(1));
        Assert.That(moon.ResourceRewards[0].First, Is.EqualTo(composite));
        Assert.That(moon.ResourceRewards[0].Second, Is.EqualTo(new ExpantaNum(75)));
        Assert.That(mars.ResourceRewards, Has.Count.EqualTo(1));
        Assert.That(mars.ResourceRewards[0].First, Is.EqualTo(rocketFuel));
        Assert.That(mars.ResourceRewards[0].Second, Is.EqualTo(new ExpantaNum(100)));
    }

    [Test]
    public void C802_EnemySectorCannotBeOccupiedBeforeCampaignCompletion()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorState state = manager.GetState(moon);
        state.SetUnlockedForEditor(true);

        Assert.That(manager.TryOccupy(moon, out SectorOperationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.CampaignRequired));
    }

    [Test]
    public void C802_CampaignStateTracksTargetAndCasualties()
    {
        var campaign = new CampaignState();
        InvokeCampaignMethod(campaign, "Begin", "Moon");
        InvokeCampaignMethod(campaign, "RecordCombat", new ExpantaNum(0.8d), new ExpantaNum(2));

        Assert.That(campaign.Active, Is.True);
        Assert.That(campaign.TargetSectorId, Is.EqualTo("Moon"));
        Assert.That(campaign.Casualties, Is.EqualTo(new ExpantaNum(2)));
        Assert.That(campaign.CombatRatio, Is.EqualTo(new ExpantaNum(0.8d)));
    }

    [Test]
    public void C803_CampaignCostMathRejectsInsufficientFoodAtomically()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorState state = manager.GetState(moon);
        state.SetUnlockedForEditor(true);
        var runtimeState = new GameState();

        bool advanced = manager.TryAdvanceCampaign(moon, 60d, runtimeState, null, out SectorOperationFailure failure);

        Assert.That(advanced, Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.InsufficientCampaignSupply));
        Assert.That(runtimeState.FoodAmount, Is.EqualTo(new ExpantaNum(300)));
        Assert.That(state.CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void C805_CampaignPreviewReportsDeterministicPowerProgressAndCosts()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorState state = manager.GetState(moon);
        state.SetUnlockedForEditor(true);
        var runtimeState = new GameState();
        InvokeGameStateMethod(runtimeState, "AdjustAttackPower", new ExpantaNum(100));
        InvokeGameStateMethod(runtimeState, "AdjustFleetPower", new ExpantaNum(50));
        InvokeGameStateMethod(runtimeState, "AdjustMilitaryManpower", new ExpantaNum(150));

        SectorCampaignPreview preview = manager.GetCampaignPreview(moon, runtimeState, null);

        Assert.That(preview.IsValid, Is.True);
        Assert.That(preview.CurrentProgress, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(preview.CombatRatio.ToDouble(), Is.EqualTo(1.5d).Within(0.000001d));
        Assert.That(preview.ProgressPerMinute.ToDouble(), Is.EqualTo(0.625d).Within(0.000001d));
        Assert.That(preview.CasualtiesPerMinute, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(preview.FoodCostPerMinute, Is.EqualTo(new ExpantaNum(2)));
        Assert.That(preview.ResourceCostsPerMinute, Has.Count.EqualTo(1));
        Assert.That(preview.ResourceCostsPerMinute[0].First.Id, Is.EqualTo("RocketFuel"));
        Assert.That(preview.HasSupply, Is.False);
    }

    [Test]
    public void C806_InsufficientStrategicResourceLeavesCampaignStateAndFoodUntouched()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        manager.GetState(moon).SetUnlockedForEditor(true);
        var runtimeState = new GameState();

        bool advanced = manager.TryAdvanceCampaign(
            moon,
            60d,
            runtimeState,
            null,
            out SectorOperationFailure failure);

        Assert.That(advanced, Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.InsufficientCampaignSupply));
        Assert.That(runtimeState.FoodAmount, Is.EqualTo(new ExpantaNum(300)));
        Assert.That(runtimeState.Campaign.Active, Is.False);
        Assert.That(manager.GetState(moon).CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void C806_LowCombatConsumesCostsAndRecordsCasualtiesWithoutReward()
    {
        GameObject resourceObject = new GameObject("C806-ResourceManager");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            Resource rocketFuel = DataBase<Resource>.Find("RocketFuel");
            resourceManager.SetAmount(rocketFuel, new ExpantaNum(10));

            var rewards = 0;
            var manager = new SectorManager(_ => rewards++);
            manager.InitializeDefinitions();
            SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
            manager.GetState(moon).SetUnlockedForEditor(true);
            var runtimeState = new GameState();

            bool advanced = manager.TryAdvanceCampaign(
                moon,
                60d,
                runtimeState,
                resourceManager,
                out SectorOperationFailure failure);

            Assert.That(advanced, Is.True);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));
            Assert.That(runtimeState.FoodAmount, Is.EqualTo(new ExpantaNum(298)));
            Assert.That(resourceManager.GetAmount(rocketFuel), Is.EqualTo(new ExpantaNum(9)));
            Assert.That(runtimeState.Campaign.Active, Is.True);
            Assert.That(runtimeState.Campaign.Casualties, Is.EqualTo(new ExpantaNum(1)));
            Assert.That(manager.GetState(moon).CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(rewards, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void C806_RewardsApplyOnlyWhenCampaignCompletes()
    {
        GameObject resourceObject = new GameObject("C806-Reward-ResourceManager");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            Resource rocketFuel = DataBase<Resource>.Find("RocketFuel");
            resourceManager.SetAmount(rocketFuel, new ExpantaNum(10));

            var rewards = 0;
            var manager = new SectorManager(_ => rewards++);
            manager.InitializeDefinitions();
            SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
            manager.GetState(moon).SetUnlockedForEditor(true);
            var runtimeState = new GameState();
            InvokeGameStateMethod(runtimeState, "AdjustAttackPower", new ExpantaNum(100));
            InvokeGameStateMethod(runtimeState, "AdjustMilitaryManpower", new ExpantaNum(100));

            Assert.That(manager.TryAdvanceCampaign(moon, 60d, runtimeState, resourceManager, out _), Is.True);
            Assert.That(rewards, Is.EqualTo(0));
            Assert.That(manager.GetState(moon).CampaignProgress.ToDouble(), Is.EqualTo(0.25d).Within(0.000001d));

            Assert.That(manager.TryAdvanceCampaign(moon, 180d, runtimeState, resourceManager, out _), Is.True);
            Assert.That(rewards, Is.EqualTo(1));
            Assert.That(manager.GetState(moon).Occupied, Is.True);
            Assert.That(runtimeState.Campaign.Active, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(resourceObject);
        }
    }

    private static void InvokeGameStateMethod(GameState state, string methodName, params object[] arguments)
    {
        var method = typeof(GameState).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(state, arguments);
    }

    private static void InvokeCampaignMethod(CampaignState state, string methodName, params object[] arguments)
    {
        var method = typeof(CampaignState).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(state, arguments);
    }
}
