using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class SectorManagerTests
{
    [Test]
    public void AllSectorDefinitionsHaveExecutableLabelsRewardsAndCosts()
    {
        IReadOnlyList<SectorDefinition> sectors = DataBase<SectorDefinition>.All;
        Assert.That(sectors, Is.Not.Empty);
        for (int i = 0; i < sectors.Count; i++)
        {
            SectorDefinition sector = sectors[i];
            Assert.That(sector, Is.Not.Null);
            Assert.That(sector.Label, Is.Not.Null.And.Not.Empty, sector.Id);
            Assert.That(sector.Description, Is.Not.Null.And.Not.Empty, sector.Id);
            Assert.That(sector.EnemyPower.IsNaN, Is.False, sector.Id);
            Assert.That(sector.EnemyPower, Is.GreaterThanOrEqualTo(ExpantaNum.Zero), sector.Id);
            Assert.That(sector.TerritoryReward.IsNaN, Is.False, sector.Id);
            Assert.That(sector.TerritoryReward, Is.GreaterThan(ExpantaNum.Zero), sector.Id);
            Assert.That(sector.ResourceRewards, Is.Not.Null, sector.Id);
            Assert.That(sector.ResourceRewards.Count, Is.GreaterThan(0), sector.Id);
            for (int rewardIndex = 0; rewardIndex < sector.ResourceRewards.Count; rewardIndex++)
            {
                Pair<Resource, ExpantaNum> reward = sector.ResourceRewards[rewardIndex];
                Assert.That(reward.First, Is.Not.Null, sector.Id);
                Assert.That(reward.Second, Is.GreaterThan(ExpantaNum.Zero), sector.Id);
            }

            if (sector.IsHomeSystem)
            {
                Assert.That(sector.ColonizationFoodPerMinute, Is.GreaterThanOrEqualTo(ExpantaNum.Zero), sector.Id);
                Assert.That(sector.ColonizationResourceCosts, Is.Not.Null, sector.Id);
            }
            else
            {
                Assert.That(sector.EnemyPower, Is.GreaterThan(ExpantaNum.Zero), sector.Id);
                Assert.That(sector.CampaignFoodPerMinute, Is.GreaterThan(ExpantaNum.Zero), sector.Id);
                Assert.That(sector.CampaignResourceCosts, Is.Not.Null.And.Not.Empty, sector.Id);
                Assert.That(sector.CampaignProgressMultiplier, Is.GreaterThan(ExpantaNum.Zero), sector.Id);
            }
        }
    }

    [Test]
    public void HomeExplorationPreviewShowsPowerAndSupplyBeforeStarting()
    {
        GameObject resourceObject = new GameObject("Sector-Exploration-Preview-ResourceManager");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
            for (int i = 0; i < lowOrbit.ColonizationResourceCosts.Count; i++)
                resourceManager.SetAmount(
                    lowOrbit.ColonizationResourceCosts[i].First,
                    lowOrbit.ColonizationResourceCosts[i].Second);

            GameState runtimeState = new GameState();
            InvokeGameStateMethod(runtimeState, "AdjustAttackPower", new ExpantaNum(100));
            InvokeGameStateMethod(runtimeState, "AdjustDefensePower", new ExpantaNum(100));
            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();

            SectorExplorationPreview preview = manager.GetExplorationPreview(
                lowOrbit,
                runtimeState,
                resourceManager);

            Assert.That(preview.IsValid, Is.True);
            Assert.That(preview.HasSupply, Is.True);
            Assert.That(preview.ExplorationPower, Is.EqualTo(new ExpantaNum(100)));
            Assert.That(preview.RequiredPower, Is.EqualTo(new ExpantaNum(40)));
            Assert.That(preview.EstimatedMinutesRemaining, Is.EqualTo(ExpantaNum.One));
            Assert.That(preview.ProgressPerMinute, Is.EqualTo(ExpantaNum.One));
        }
        finally
        {
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void SiriusResourceBeltIsAHighCostHighValueSpacerCampaign()
    {
        SectorDefinition sector = Resources.Load<SectorDefinition>("Datas/Sector/SiriusResourceBelt");

        Assert.That(sector, Is.Not.Null);
        Assert.That(sector.Domain, Is.EqualTo(SectorDefinition.SectorDomain.Interstellar));
        Assert.That(sector.StarSystemId, Is.EqualTo("Sirius"));
        Assert.That(sector.EnemyPower, Is.GreaterThan(new ExpantaNum(5000d)));
        Assert.That(sector.TerritoryReward, Is.GreaterThan(new ExpantaNum(100000d)));
        Assert.That(HasResourceReward(sector, "PhantomAlloy"), Is.True);
        Assert.That(HasResourceReward(sector, "PhantomWeave"), Is.True);
        Assert.That(HasResourceReward(sector, "PhaseMaterial"), Is.True);
        Assert.That(HasCampaignCost(sector, "RocketFuel"), Is.True);
        Assert.That(HasCampaignCost(sector, "TitaniumAlloy"), Is.True);
        Assert.That(HasCampaignCost(sector, "PhaseMaterial"), Is.True);
    }

    [Test]
    public void TauCetiFoundryReturnsAdvancedMaterialsForItsIndustrialOutpostCost()
    {
        SectorDefinition sector = Resources.Load<SectorDefinition>("Datas/Sector/TauCetiFoundry");

        Assert.That(sector, Is.Not.Null);
        Assert.That(sector.TerritoryReward, Is.GreaterThan(new ExpantaNum(30000d)));
        Assert.That(HasResourceReward(sector, "Electronics"), Is.True);
        Assert.That(HasResourceReward(sector, "PhantomAlloy"), Is.True);
        Assert.That(HasResourceReward(sector, "PhantomWeave"), Is.True);
        Assert.That(HasCampaignCost(sector, "PhantomAlloy"), Is.True);
        Assert.That(HasCampaignCost(sector, "PhantomWeave"), Is.True);
    }

    [Test]
    public void ProximaReturnsTitaniumForTheNextInterstellarBuildout()
    {
        SectorDefinition sector = Resources.Load<SectorDefinition>("Datas/Sector/ProximaB");

        Assert.That(sector, Is.Not.Null);
        Assert.That(sector.TerritoryReward, Is.GreaterThan(new ExpantaNum(40000d)));
        Assert.That(HasResourceReward(sector, "TitaniumAlloy"), Is.True);
        Assert.That(HasCampaignCost(sector, "PhantomAlloy"), Is.True);
        Assert.That(HasCampaignCost(sector, "PhantomWeave"), Is.True);
    }

    [Test]
    public void InterstellarCampaignsUseTieredLongRangeProgressMultipliers()
    {
        SectorDefinition proxima = Resources.Load<SectorDefinition>("Datas/Sector/ProximaB");
        SectorDefinition tau = Resources.Load<SectorDefinition>("Datas/Sector/TauCetiFoundry");
        SectorDefinition sirius = Resources.Load<SectorDefinition>("Datas/Sector/SiriusResourceBelt");

        Assert.That(proxima.CampaignProgressMultiplier.ToDouble(), Is.EqualTo(0.35d).Within(0.000001d));
        Assert.That(tau.CampaignProgressMultiplier.ToDouble(), Is.EqualTo(0.30d).Within(0.000001d));
        Assert.That(sirius.CampaignProgressMultiplier.ToDouble(), Is.EqualTo(0.20d).Within(0.000001d));
        Assert.That(proxima.CampaignProgressMultiplier, Is.GreaterThan(tau.CampaignProgressMultiplier));
        Assert.That(tau.CampaignProgressMultiplier, Is.GreaterThan(sirius.CampaignProgressMultiplier));
        Assert.That(
            CampaignManager.AdvanceProgress(
                ExpantaNum.Zero,
                new ExpantaNum(1.5d),
                60d,
                new ExpantaNum(0.20d)).ToDouble(),
            Is.EqualTo(0.125d).Within(0.000001d));
    }

    [Test]
    public void InterstellarCampaignsConsumeNickelForFleetStructuralMaintenance()
    {
        SectorDefinition alpha = Resources.Load<SectorDefinition>("Datas/Sector/AlphaCentauri");
        SectorDefinition proxima = Resources.Load<SectorDefinition>("Datas/Sector/ProximaB");
        SectorDefinition tau = Resources.Load<SectorDefinition>("Datas/Sector/TauCetiFoundry");
        SectorDefinition sirius = Resources.Load<SectorDefinition>("Datas/Sector/SiriusResourceBelt");

        Assert.That(FindCampaignCost(alpha, "Nickel").ToDouble(), Is.EqualTo(4d).Within(0.000001d));
        Assert.That(FindCampaignCost(proxima, "Nickel").ToDouble(), Is.EqualTo(8d).Within(0.000001d));
        Assert.That(FindCampaignCost(tau, "Nickel").ToDouble(), Is.EqualTo(12d).Within(0.000001d));
        Assert.That(FindCampaignCost(sirius, "Nickel").ToDouble(), Is.EqualTo(18d).Within(0.000001d));
    }

    private static ExpantaNum FindCampaignCost(SectorDefinition sector, string resourceId)
    {
        for (int i = 0; i < sector.CampaignResourceCosts.Count; i++)
            if (sector.CampaignResourceCosts[i].First != null &&
                sector.CampaignResourceCosts[i].First.Id == resourceId)
                return sector.CampaignResourceCosts[i].Second;
        return ExpantaNum.Zero;
    }

    private static bool HasResourceReward(SectorDefinition sector, string resourceId)
    {
        for (int i = 0; i < sector.ResourceRewards.Count; i++)
            if (sector.ResourceRewards[i].First != null &&
                sector.ResourceRewards[i].First.Id == resourceId &&
                sector.ResourceRewards[i].Second > ExpantaNum.Zero)
                return true;
        return false;
    }

    private static bool HasCampaignCost(SectorDefinition sector, string resourceId)
    {
        for (int i = 0; i < sector.CampaignResourceCosts.Count; i++)
            if (sector.CampaignResourceCosts[i].First != null &&
                sector.CampaignResourceCosts[i].First.Id == resourceId &&
                sector.CampaignResourceCosts[i].Second > ExpantaNum.Zero)
                return true;
        return false;
    }

    [Test]
    public void C704_ManagerUsesStableDefinitionOrderAndPrerequisites()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();

        Assert.That(manager.OrderedStates, Has.Count.GreaterThanOrEqualTo(3));
        Assert.That(manager.OrderedStates[0].Definition.Id, Is.EqualTo("LowOrbit"));
        Assert.That(manager.OrderedStates[1].Definition.Id, Is.EqualTo("Mars"));
        Assert.That(manager.OrderedStates[2].Definition.Id, Is.EqualTo("Moon"));
        Assert.That(manager.CanAccess(DataBase<SectorDefinition>.Find("LowOrbit")), Is.True);
        Assert.That(manager.CanAccess(DataBase<SectorDefinition>.Find("Moon")), Is.False);
    }

    [Test]
    public void 本星系探索需要攻击与防御能力()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        SectorState state = manager.GetState(lowOrbit);
        state.SetUnlockedForEditor(true);
        GameState runtimeState = new GameState();

        bool advanced = manager.TryAdvanceColonization(
            lowOrbit,
            60d,
            runtimeState,
            null,
            out SectorOperationFailure failure);

        Assert.That(advanced, Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.InsufficientExplorationPower));
    }

    [Test]
    public void 本星系探索不使用星区战役()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        SectorState state = manager.GetState(lowOrbit);
        state.SetUnlockedForEditor(true);
        GameState runtimeState = new GameState();

        bool advanced = manager.TryAdvanceCampaign(
            lowOrbit,
            60d,
            runtimeState,
            null,
            out SectorOperationFailure failure);

        Assert.That(advanced, Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.CampaignNotAllowedInHomeSystem));
    }

    [Test]
    public void C813_ZeroDeltaStartsHomeExplorationWithoutAdvancingProgress()
    {
        GameObject resourceObject = new GameObject("C813-Exploration-ResourceManager");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
            for (int i = 0; i < lowOrbit.ColonizationResourceCosts.Count; i++)
                resourceManager.SetAmount(
                    lowOrbit.ColonizationResourceCosts[i].First,
                    lowOrbit.ColonizationResourceCosts[i].Second);

            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            manager.GetState(lowOrbit).SetUnlockedForEditor(true);
            GameState runtimeState = new GameState();
            InvokeGameStateMethod(runtimeState, "AdjustAttackPower", new ExpantaNum(100));
            InvokeGameStateMethod(runtimeState, "AdjustDefensePower", new ExpantaNum(100));

            Assert.That(manager.TryAdvanceColonization(
                lowOrbit, 0d, runtimeState, resourceManager, out SectorOperationFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));
            Assert.That(manager.GetState(lowOrbit).ColonizationActive, Is.True);
            Assert.That(manager.GetState(lowOrbit).CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
        }
        finally
        {
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void C814_CancelledOperationsDecayHomeExplorationSlowerThanInterstellarCampaigns()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorState home = manager.GetState(DataBase<SectorDefinition>.Find("LowOrbit"));
        SectorState interstellar = manager.GetState(DataBase<SectorDefinition>.Find("ProximaB"));
        home.SetUnlockedForEditor(true);
        interstellar.SetUnlockedForEditor(true);
        home.SetCampaignProgressForEditor(new ExpantaNum(0.5d));
        interstellar.SetCampaignProgressForEditor(new ExpantaNum(0.5d));

        manager.TickActiveColonization(600d, new GameState(), null, out _);
        manager.TickActiveCampaign(600d, new GameState(), null, out _);

        Assert.That(home.CampaignProgress.ToDouble(), Is.EqualTo(0.45d).Within(0.000001d));
        Assert.That(interstellar.CampaignProgress.ToDouble(), Is.EqualTo(0.30d).Within(0.000001d));
    }

    [Test]
    public void 本星系可以同时启动并推进多个星区探索()
    {
        GameObject resourceObject = new GameObject("并行探索资源管理器");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            resourceManager.SetAmount(DataBase<Resource>.Find("RefinedFuel"), new ExpantaNum(20));
            resourceManager.SetAmount(DataBase<Resource>.Find("RocketFuel"), new ExpantaNum(5));

            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
            SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
            manager.GetState(lowOrbit).SetUnlockedForEditor(true);
            manager.GetState(moon).SetUnlockedForEditor(true);
            GameState runtimeState = new GameState();
            InvokeGameStateMethod(runtimeState, "AdjustAttackPower", new ExpantaNum(1000));
            InvokeGameStateMethod(runtimeState, "AdjustDefensePower", new ExpantaNum(1000));

            Assert.That(manager.TryAdvanceColonization(
                lowOrbit, 0d, runtimeState, resourceManager, out SectorOperationFailure lowFailure), Is.True);
            Assert.That(lowFailure, Is.EqualTo(SectorOperationFailure.None));
            Assert.That(manager.TryAdvanceColonization(
                moon, 0d, runtimeState, resourceManager, out SectorOperationFailure moonFailure), Is.True);
            Assert.That(moonFailure, Is.EqualTo(SectorOperationFailure.None));
            Assert.That(manager.GetState(lowOrbit).ColonizationActive, Is.True);
            Assert.That(manager.GetState(moon).ColonizationActive, Is.True);

            Assert.That(manager.TickActiveColonization(60d, runtimeState, resourceManager, out SectorOperationFailure tickFailure), Is.True);
            Assert.That(tickFailure, Is.EqualTo(SectorOperationFailure.None));
            Assert.That(manager.GetState(lowOrbit).CampaignProgress, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(manager.GetState(moon).CampaignProgress, Is.GreaterThan(ExpantaNum.Zero));
        }
        finally
        {
            Object.DestroyImmediate(resourceObject);
        }
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

        Assert.That(moon.ResourceRewards, Has.Count.EqualTo(2));
        Assert.That(moon.ResourceRewards[0].First, Is.EqualTo(composite));
        Assert.That(moon.ResourceRewards[0].Second, Is.EqualTo(new ExpantaNum(2500)));
        Assert.That(mars.ResourceRewards, Has.Count.EqualTo(2));
        Assert.That(mars.ResourceRewards[0].First, Is.EqualTo(rocketFuel));
        Assert.That(mars.ResourceRewards[0].Second, Is.EqualTo(new ExpantaNum(5000)));
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
        Assert.That(preview.FleetSurvivalFactor.ToDouble(), Is.EqualTo(0.35d).Within(0.000001d));
        Assert.That(preview.ProgressPerMinute.ToDouble(), Is.EqualTo(0.625d).Within(0.000001d));
        Assert.That(preview.EstimatedMinutesRemaining.ToDouble(), Is.EqualTo(1.6d).Within(0.000001d));
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
            InvokeGameStateMethod(runtimeState, "AdjustDefensePower", new ExpantaNum(80));

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
    public void 战役防御力提高舰队生存率并降低伤亡()
    {
        ExpantaNum unprotected = CampaignManager.CalculateCasualtyAmount(
            new ExpantaNum(0.8d),
            ExpantaNum.Zero,
            new ExpantaNum(80),
            60d);
        ExpantaNum protectedFleet = CampaignManager.CalculateCasualtyAmount(
            new ExpantaNum(0.8d),
            new ExpantaNum(80),
            new ExpantaNum(80),
            60d);

        Assert.That(unprotected.ToDouble(), Is.GreaterThan(protectedFleet.ToDouble()));
        Assert.That(CampaignManager.CalculateFleetSurvivalFactor(
            new ExpantaNum(80), new ExpantaNum(80)).ToDouble(), Is.EqualTo(1d).Within(0.000001d));
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

    [Test]
    public void C807_GameSaveDataRoundTripsMilitaryRuntimeState()
    {
        GameObject gameObject = new GameObject("C807-GameManager");
        try
        {
            GameManager gameManager = gameObject.AddComponent<GameManager>();
            gameManager.AdjustAttackPower(new ExpantaNum(11));
            gameManager.AdjustDefensePower(new ExpantaNum(13));
            gameManager.AdjustFleetPower(new ExpantaNum(17));
            gameManager.AdjustMilitaryManpower(new ExpantaNum(19));
            gameManager.SetSupplySatisfaction(new ExpantaNum(0.75d));
            InvokeGameStateMethod(gameManager.State, "SetPowerSatisfaction", new ExpantaNum(0.5d));
            InvokeGameStateMethod(gameManager.State, "SetLogisticsSatisfaction", new ExpantaNum(0.25d));

            SaveManager.GameSaveData saved = (SaveManager.GameSaveData)InvokeGameManagerMethod(
                gameManager,
                "CaptureSaveData");
            InvokeGameManagerMethod(gameManager, "ResetDerivedEconomy");
            InvokeGameManagerMethod(gameManager, "RestoreMilitarySaveData", saved);

            Assert.That(saved.AttackPower, Is.EqualTo("11"));
            Assert.That(saved.DefensePower, Is.EqualTo("13"));
            Assert.That(saved.FleetPower, Is.EqualTo("17"));
            Assert.That(saved.MilitaryManpower, Is.EqualTo("19"));
            Assert.That(gameManager.State.AttackPower, Is.EqualTo(new ExpantaNum(11)));
            Assert.That(gameManager.State.DefensePower, Is.EqualTo(new ExpantaNum(13)));
            Assert.That(gameManager.State.FleetPower, Is.EqualTo(new ExpantaNum(17)));
            Assert.That(gameManager.State.MilitaryManpower, Is.EqualTo(new ExpantaNum(19)));
            Assert.That(gameManager.State.SupplySatisfaction, Is.EqualTo(new ExpantaNum(0.75d)));
            Assert.That(gameManager.State.PowerSatisfaction, Is.EqualTo(new ExpantaNum(0.5d)));
            Assert.That(gameManager.State.LogisticsSatisfaction, Is.EqualTo(new ExpantaNum(0.25d)));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void C808_InactiveCampaignSaveClearsExistingCampaignState()
    {
        GameObject gameObject = new GameObject("C808-GameManager");
        try
        {
            GameManager gameManager = gameObject.AddComponent<GameManager>();
            InvokeGameStateMethod(gameManager.State, "BeginCampaign", "Moon");
            InvokeGameStateMethod(
                gameManager.State,
                "RecordCampaignCombat",
                new ExpantaNum(0.8d),
                new ExpantaNum(2));

            InvokeGameManagerMethod(
                gameManager,
                "RestoreSaveData",
                new SaveManager.GameSaveData
                {
                    FoodAmount = "300",
                    KingdomName = "Restore Test",
                    TechLevel = TechLevel.Animal,
                    CampaignActive = false,
                    CampaignTargetSectorId = string.Empty
                });

            Assert.That(gameManager.State.Campaign.Active, Is.False);
            Assert.That(gameManager.State.Campaign.TargetSectorId, Is.Empty);
            Assert.That(gameManager.State.Campaign.Casualties, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(gameManager.State.Campaign.CombatRatio, Is.EqualTo(ExpantaNum.Zero));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void C808_PartialSectorSaveClearsMissingSectorStates()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorState moonState = manager.GetState(moon);
        moonState.SetUnlockedForEditor(true);
        moonState.SetCampaignProgressForEditor(new ExpantaNum(0.5d));

        manager.RestoreSaveData(new SaveManager.SectorSaveData
        {
            States = new System.Collections.Generic.List<SaveManager.SectorStateSaveData>()
        });

        Assert.That(moonState.Unlocked, Is.False);
        Assert.That(moonState.CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void C809_PreSpaceMilitaryBuildingsAreRemoved()
    {
        Assert.That(DataBase<Building>.Contains("Barracks"), Is.False);
        Assert.That(DataBase<Building>.Contains("Castle"), Is.False);
        Assert.That(DataBase<Building>.Contains("Arsenal"), Is.False);
        Assert.That(DataBase<Building>.Contains("ArmsFactory"), Is.False);
    }

    [Test]
    public void C810_BuildingManagerDerivesAndRemovesMilitaryPower()
    {
        GameObject gameObject = new GameObject("C810-Military-Managers");
        GameObject resourceObject = new GameObject("C810-ResourceManager");
        GameObject buildingObject = new GameObject("C810-BuildingManager");
        try
        {
            GameManager gameManager = gameObject.AddComponent<GameManager>();
            resourceObject.AddComponent<ResourceManager>();
            BuildingManager buildingManager = buildingObject.AddComponent<BuildingManager>();
            Building library = DataBase<Building>.Find("Library");
            BuildingState state = buildingManager.EnsureBuilding(library);
            var method = typeof(BuildingManager).GetMethod(
                "SetAmountAndRates",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            method.Invoke(buildingManager, new object[] { state, new ExpantaNum(1) });
            Assert.That(gameManager.State.AttackPower, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(gameManager.State.DefensePower, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(gameManager.State.MilitaryManpower, Is.EqualTo(ExpantaNum.Zero));

            method.Invoke(buildingManager, new object[] { state, ExpantaNum.Zero });
            Assert.That(gameManager.State.AttackPower, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(gameManager.State.DefensePower, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(gameManager.State.MilitaryManpower, Is.EqualTo(ExpantaNum.Zero));
        }
        finally
        {
            Object.DestroyImmediate(buildingObject);
            Object.DestroyImmediate(resourceObject);
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void C811_CampaignCasualtiesReduceReadinessAndRepairConsumesStrategicSupply()
    {
        ExpantaNum readyPower = CampaignManager.CalculateEffectivePower(
            new ExpantaNum(100),
            new ExpantaNum(100),
            new ExpantaNum(100),
            ExpantaNum.One,
            ExpantaNum.One,
            ExpantaNum.One,
            ExpantaNum.One,
            ExpantaNum.Zero);
        ExpantaNum damagedPower = CampaignManager.CalculateEffectivePower(
            new ExpantaNum(100),
            new ExpantaNum(100),
            new ExpantaNum(100),
            ExpantaNum.One,
            ExpantaNum.One,
            ExpantaNum.One,
            ExpantaNum.One,
            new ExpantaNum(50));
        Assert.That(damagedPower, Is.LessThan(readyPower));
        Assert.That(CampaignManager.CalculateFleetReadiness(
            new ExpantaNum(100), new ExpantaNum(50)).ToDouble(), Is.EqualTo(2d / 3d).Within(0.000001d));

        GameObject resourceObject = new GameObject("C811-Repair-ResourceManager");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            resourceManager.SetAmount(DataBase<Resource>.Find("TitaniumAlloy"), new ExpantaNum(20));
            resourceManager.SetAmount(DataBase<Resource>.Find("PhantomWeave"), new ExpantaNum(10));
            resourceManager.SetAmount(DataBase<Resource>.Find("RocketFuel"), new ExpantaNum(5));

            GameState runtimeState = new GameState();
            InvokeGameStateMethod(runtimeState, "BeginCampaign", "ProximaB");
            InvokeGameStateMethod(runtimeState, "RecordCampaignCombat", new ExpantaNum(0.8d), new ExpantaNum(10));
            SectorManager manager = new SectorManager(_ => { });

            bool repaired = manager.TryRepairFleet(
                runtimeState,
                resourceManager,
                new ExpantaNum(5),
                out ExpantaNum repairedAmount,
                out SectorOperationFailure failure);

            Assert.That(repaired, Is.True);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));
            Assert.That(repairedAmount, Is.EqualTo(new ExpantaNum(5)));
            Assert.That(runtimeState.Campaign.Casualties, Is.EqualTo(new ExpantaNum(5)));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("TitaniumAlloy")), Is.EqualTo(new ExpantaNum(10)));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("PhantomWeave")), Is.EqualTo(new ExpantaNum(5)));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("RocketFuel")), Is.EqualTo(new ExpantaNum(2.5d)));
        }
        finally
        {
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void C812_CampaignPreviewExposesSupplyAndLogisticsSatisfaction()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        var runtimeState = new GameState();
        InvokeGameStateMethod(runtimeState, "AdjustAttackPower", new ExpantaNum(100));
        InvokeGameStateMethod(runtimeState, "AdjustMilitaryManpower", new ExpantaNum(100));
        InvokeGameStateMethod(runtimeState, "SetSupplySatisfaction", new ExpantaNum(0.75d));
        InvokeGameStateMethod(runtimeState, "SetPowerSatisfaction", new ExpantaNum(0.5d));
        InvokeGameStateMethod(runtimeState, "SetLogisticsSatisfaction", new ExpantaNum(0.25d));

        SectorCampaignPreview preview = manager.GetCampaignPreview(moon, runtimeState, null);

        Assert.That(preview.SupplySatisfaction, Is.EqualTo(new ExpantaNum(0.75d)));
        Assert.That(preview.PowerSatisfaction, Is.EqualTo(new ExpantaNum(0.5d)));
        Assert.That(preview.LogisticsSatisfaction, Is.EqualTo(new ExpantaNum(0.25d)));
        Assert.That(preview.ProgressPerMinute, Is.EqualTo(ExpantaNum.Zero));
    }

    private static void InvokeGameStateMethod(GameState state, string methodName, params object[] arguments)
    {
        var method = typeof(GameState).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(state, arguments);
    }

    private static object InvokeGameManagerMethod(GameManager manager, string methodName, params object[] arguments)
    {
        var method = typeof(GameManager).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(manager, arguments);
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
