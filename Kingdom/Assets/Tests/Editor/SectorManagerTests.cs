using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SectorManagerTests
{
    [Test]
    public void SiriusResourceBeltUsesReadableChineseLabel()
    {
        SectorDefinition sector = Resources.Load<SectorDefinition>("Datas/Sector/SiriusResourceBelt");

        Assert.That(sector, Is.Not.Null);
        Assert.That(sector.Label, Is.EqualTo("天狼资源带"));
        Assert.That(sector.Description, Does.Not.Contain("鍗"));
    }

    [Test]
    public void SiriusResourceBeltRequiresTauCetiIndustrialOutpost()
    {
        SectorDefinition sirius = Resources.Load<SectorDefinition>("Datas/Sector/SiriusResourceBelt");
        SectorDefinition tau = Resources.Load<SectorDefinition>("Datas/Sector/TauCetiFoundry");

        Assert.That(sirius, Is.Not.Null);
        Assert.That(tau, Is.Not.Null);
        Assert.That(sirius.PrerequisiteSectors, Does.Contain(tau));
    }

    [Test]
    public void InterstellarSectorChainRemainsSequential()
    {
        SectorDefinition alpha = Resources.Load<SectorDefinition>("Datas/Sector/AlphaCentauri");
        SectorDefinition proxima = Resources.Load<SectorDefinition>("Datas/Sector/ProximaB");
        SectorDefinition tau = Resources.Load<SectorDefinition>("Datas/Sector/TauCetiFoundry");
        SectorDefinition sirius = Resources.Load<SectorDefinition>("Datas/Sector/SiriusResourceBelt");

        Assert.That(alpha, Is.Not.Null);
        Assert.That(proxima, Is.Not.Null);
        Assert.That(tau, Is.Not.Null);
        Assert.That(sirius, Is.Not.Null);
        Assert.That(proxima.PrerequisiteSectors, Does.Contain(alpha));
        Assert.That(tau.PrerequisiteSectors, Does.Contain(proxima));
        Assert.That(sirius.PrerequisiteSectors, Does.Contain(tau));
    }

    [Test]
    public void SolarSystemChainIncludesAsteroidBeltAndJovianSystemBeforeAlpha()
    {
        SectorDefinition mars = Resources.Load<SectorDefinition>("Datas/Sector/Mars");
        SectorDefinition asteroid = Resources.Load<SectorDefinition>("Datas/Sector/MainAsteroidBelt");
        SectorDefinition jovian = Resources.Load<SectorDefinition>("Datas/Sector/JovianSystem");
        SectorDefinition alpha = Resources.Load<SectorDefinition>("Datas/Sector/AlphaCentauri");

        Assert.That(mars, Is.Not.Null);
        Assert.That(asteroid, Is.Not.Null);
        Assert.That(jovian, Is.Not.Null);
        Assert.That(alpha, Is.Not.Null);
        Assert.That(asteroid.PrerequisiteSectors, Does.Contain(mars));
        Assert.That(jovian.PrerequisiteSectors, Does.Contain(asteroid));
        Assert.That(alpha.PrerequisiteSectors, Does.Contain(jovian));
        Assert.That(asteroid.Domain, Is.EqualTo(SectorDefinition.SectorDomain.HomeSystem));
        Assert.That(jovian.Domain, Is.EqualTo(SectorDefinition.SectorDomain.HomeSystem));
        Assert.That(asteroid.EnemyPower, Is.EqualTo(new ExpantaNum(300d)));
        Assert.That(jovian.EnemyPower, Is.EqualTo(new ExpantaNum(500d)));
        Assert.That(asteroid.TerritoryReward, Is.EqualTo(new ExpantaNum(200000d)));
        Assert.That(jovian.TerritoryReward, Is.EqualTo(new ExpantaNum(400000d)));
        Assert.That(HasResourceReward(asteroid, "TitaniumConcentrate"), Is.True);
        Assert.That(HasResourceReward(asteroid, "NickelConcentrate"), Is.True);
        Assert.That(HasResourceReward(jovian, "RocketFuel"), Is.True);
        Assert.That(HasResourceReward(jovian, "Nickel"), Is.True);
        Assert.That(HasPositiveRate(asteroid.OccupiedResourceRatesPerSecond,
            DataBase<Resource>.Find("TitaniumConcentrate")), Is.True);
        Assert.That(HasPositiveRate(jovian.OccupiedResourceRatesPerSecond,
            DataBase<Resource>.Find("RocketFuel")), Is.True);
    }

    [Test]
    public void InterstellarAccessUnlocksOneOccupiedPrerequisiteAtATime()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition alpha = DataBase<SectorDefinition>.Find("AlphaCentauri");
        SectorDefinition proxima = DataBase<SectorDefinition>.Find("ProximaB");
        SectorDefinition tau = DataBase<SectorDefinition>.Find("TauCetiFoundry");
        SectorDefinition sirius = DataBase<SectorDefinition>.Find("SiriusResourceBelt");

        Assert.That(manager.CanAccess(proxima), Is.False);
        Assert.That(manager.CanAccess(tau), Is.False);
        Assert.That(manager.CanAccess(sirius), Is.False);

        manager.GetState(alpha).SetOccupiedForEditor(true);
        Assert.That(manager.CanAccess(proxima), Is.True);
        Assert.That(manager.CanAccess(tau), Is.False);

        manager.GetState(proxima).SetOccupiedForEditor(true);
        Assert.That(manager.CanAccess(tau), Is.True);
        Assert.That(manager.CanAccess(sirius), Is.False);

        manager.GetState(tau).SetOccupiedForEditor(true);
        Assert.That(manager.CanAccess(sirius), Is.True);
    }

    [Test]
    public void OccupyAppliesRewardExactlyOnceAfterCampaignCompletion()
    {
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        int applied = 0;
        var manager = new SectorManager(_ => applied++);
        manager.InitializeDefinitions();
        SectorState state = manager.GetState(lowOrbit);
        state.SetUnlockedForEditor(true);
        state.SetCampaignProgressForEditor(ExpantaNum.One);

        Assert.That(manager.TryOccupy(lowOrbit, out SectorOperationFailure failure), Is.True);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));
        Assert.That(state.Occupied, Is.True);
        Assert.That(state.VisitCount, Is.EqualTo(1));
        Assert.That(applied, Is.EqualTo(1));
    }

    [Test]
    public void OccupyRollsBackStateWhenRewardApplicationFails()
    {
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        var manager = new SectorManager(_ => throw new InvalidOperationException("reward probe"));
        manager.InitializeDefinitions();
        SectorState state = manager.GetState(lowOrbit);
        state.SetUnlockedForEditor(true);
        state.SetCampaignProgressForEditor(ExpantaNum.One);

        Assert.Throws<InvalidOperationException>(
            () => manager.TryOccupy(lowOrbit, out _));
        Assert.That(state.Occupied, Is.False);
        Assert.That(state.VisitCount, Is.EqualTo(0));
    }

    [Test]
    public void AllSectorPrerequisitesFormAnAcyclicDefinitionGraph()
    {
        IReadOnlyList<SectorDefinition> sectors = DataBase<SectorDefinition>.All;
        var marks = new Dictionary<SectorDefinition, int>();
        for (int i = 0; i < sectors.Count; i++)
            Assert.That(VisitSectorPrerequisites(sectors[i], marks), Is.False,
                $"星区前置关系存在循环：{sectors[i].Id}。");
    }

    private static bool VisitSectorPrerequisites(
        SectorDefinition sector,
        Dictionary<SectorDefinition, int> marks)
    {
        if (sector == null)
            return false;
        if (marks.TryGetValue(sector, out int mark))
            return mark == 1;

        marks[sector] = 1;
        IReadOnlyList<SectorDefinition> prerequisites = sector.PrerequisiteSectors;
        if (prerequisites != null)
        {
            for (int i = 0; i < prerequisites.Count; i++)
            {
                SectorDefinition prerequisite = prerequisites[i];
                if (prerequisite != null &&
                    VisitSectorPrerequisites(prerequisite, marks))
                    return true;
            }
        }

        marks[sector] = 2;
        return false;
    }

    [Test]
    public void InterstellarSectorsUseCampaignSupplyInsteadOfColonization()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        GameState runtimeState = new GameState();
        SectorDefinition[] sectors =
        {
            DataBase<SectorDefinition>.Find("AlphaCentauri"),
            DataBase<SectorDefinition>.Find("ProximaB"),
            DataBase<SectorDefinition>.Find("TauCetiFoundry"),
            DataBase<SectorDefinition>.Find("SiriusResourceBelt")
        };

        for (int i = 0; i < sectors.Length; i++)
        {
            SectorDefinition sector = sectors[i];
            Assert.That(sector.IsHomeSystem, Is.False, sector.Id);
            Assert.That(sector.ColonizationFoodPerSecond,
                Is.EqualTo(ExpantaNum.Zero), sector.Id);
            Assert.That(sector.CampaignFoodPerSecond,
                Is.GreaterThan(ExpantaNum.Zero), sector.Id);
            Assert.That(sector.CampaignResourceRatesPerSecond,
                Is.Not.Empty, sector.Id);

            bool advanced = manager.TryAdvanceColonization(
                sector, 1d, runtimeState, null,
                out SectorOperationFailure failure);
            Assert.That(advanced, Is.False, sector.Id);
            Assert.That(failure, Is.EqualTo(
                SectorOperationFailure.ColonizationNotAllowedInInterstellarSystem),
                sector.Id);
        }
    }

    [Test]
    public void SiriusCampaignConsumesAdvancedStructuralMaterials()
    {
        SectorDefinition sirius = DataBase<SectorDefinition>.Find("SiriusResourceBelt");

        Assert.That(sirius, Is.Not.Null);
        Assert.That(FindCampaignCost(sirius, "PhantomAlloy"),
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(FindCampaignCost(sirius, "PhantomWeave"),
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(FindCampaignCost(sirius, "PhaseMaterial"),
            Is.GreaterThan(ExpantaNum.Zero));
    }

    [Test]
    public void 占领星区会按每秒产出资源并支持离线结算入口()
    {
        GameObject resourceObject = new GameObject("Sector-Occupied-Production-ResourceManager");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            manager.GetState(lowOrbit).SetOccupiedForEditor(true);

            Assert.That(manager.TickOccupiedResourceProduction(10d, resourceManager), Is.True);
            Assert.That(
                resourceManager.GetAmount(DataBase<Resource>.Find("CopperWire")).ToDouble(),
                Is.EqualTo(0.8d).Within(0.000001d));
            Assert.That(
                resourceManager.GetAmount(DataBase<Resource>.Find("Electronics")).ToDouble(),
                Is.EqualTo(0.4d).Within(0.000001d));
        }
        finally
        {
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void 轨道自主采矿工坊必须提升占领区每秒产出()
    {
        GameObject resourceObject = new GameObject("轨道采矿占领区产出测试资源管理器");
        try
        {
            ProgressionModifierManager.Rebuild(null);
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
            WorkshopUpgrade definition =
                DataBase<WorkshopUpgrade>.Find("AutonomousOrbitalMiningSystems");
            WorkshopUpgradeState state = new WorkshopUpgradeState(definition);
            typeof(WorkshopUpgradeState).GetMethod(
                "SetPurchased",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(state, new object[] { true });
            ProgressionModifierManager.Rebuild(null, new[] { state });

            SectorManager manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            manager.GetState(lowOrbit).SetOccupiedForEditor(true);

            Assert.That(manager.TickOccupiedResourceProduction(10d, resourceManager), Is.True);
            Assert.That(
                resourceManager.GetAmount(DataBase<Resource>.Find("CopperWire")).ToDouble(),
                Is.EqualTo(0.48d).Within(0.000001d));
            Assert.That(
                resourceManager.GetAmount(DataBase<Resource>.Find("Electronics")).ToDouble(),
                Is.EqualTo(0.24d).Within(0.000001d));
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void 轨道自主采矿研究与工坊必须叠加占领区每秒产出()
    {
        GameObject resourceObject = new GameObject("轨道采矿研究叠加测试资源管理器");
        try
        {
            Research researchDefinition =
                DataBase<Research>.Find("AutonomousOrbitalMining");
            ResearchState researchState = new ResearchState(researchDefinition);
            typeof(ResearchState).GetMethod(
                "SetStatus",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(researchState, new object[] { ResearchStatus.Completed });

            WorkshopUpgrade workshopDefinition =
                DataBase<WorkshopUpgrade>.Find("AutonomousOrbitalMiningSystems");
            WorkshopUpgradeState workshopState = new WorkshopUpgradeState(workshopDefinition);
            typeof(WorkshopUpgradeState).GetMethod(
                "SetPurchased",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(workshopState, new object[] { true });
            ProgressionModifierManager.Rebuild(
                new[] { researchState },
                new[] { workshopState });

            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
            SectorManager manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            manager.GetState(lowOrbit).SetOccupiedForEditor(true);

            Assert.That(manager.TickOccupiedResourceProduction(10d, resourceManager), Is.True);
            Assert.That(
                resourceManager.GetAmount(DataBase<Resource>.Find("CopperWire")).ToDouble(),
                Is.EqualTo(0.54d).Within(0.000001d));
            Assert.That(
                resourceManager.GetAmount(DataBase<Resource>.Find("Electronics")).ToDouble(),
                Is.EqualTo(0.27d).Within(0.000001d));
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
            Object.DestroyImmediate(resourceObject);
        }
    }

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
                Assert.That(sector.ColonizationFoodPerSecond, Is.GreaterThanOrEqualTo(ExpantaNum.Zero), sector.Id);
                Assert.That(sector.ColonizationResourceRatesPerSecond, Is.Not.Null, sector.Id);
            }
            else
            {
                Assert.That(sector.EnemyPower, Is.GreaterThan(ExpantaNum.Zero), sector.Id);
                Assert.That(sector.CampaignFoodPerSecond, Is.GreaterThan(ExpantaNum.Zero), sector.Id);
                Assert.That(sector.CampaignResourceRatesPerSecond, Is.Not.Null.And.Not.Empty, sector.Id);
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
            for (int i = 0; i < lowOrbit.ColonizationResourceRatesPerSecond.Count; i++)
                resourceManager.SetAmount(
                    lowOrbit.ColonizationResourceRatesPerSecond[i].First,
                    lowOrbit.ColonizationResourceRatesPerSecond[i].Second);

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
            Assert.That(preview.EstimatedSecondsRemaining, Is.EqualTo(new ExpantaNum(600d)));
            Assert.That(preview.ProgressPerSecond, Is.EqualTo(ExpantaNum.One / 600d));
            Assert.That(preview.ProgressPerSecond * lowOrbit.ColonizationDurationSeconds,
                Is.EqualTo(ExpantaNum.One));
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
    public void AlphaCentauriReturnsStructuralMaterialsForItsLongRangeSupplyCost()
    {
        SectorDefinition sector = Resources.Load<SectorDefinition>("Datas/Sector/AlphaCentauri");

        Assert.That(sector, Is.Not.Null);
        Assert.That(HasResourceReward(sector, "TitaniumAlloy"), Is.True);
        Assert.That(HasResourceReward(sector, "Composite"), Is.True);
        Assert.That(sector.ResourceRewards.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(HasCampaignCost(sector, "Composite"), Is.True);
        Assert.That(HasCampaignCost(sector, "PhantomWeave"), Is.True);
        Assert.That(HasCampaignCost(sector, "Machinery"), Is.True);
    }

    [Test]
    public void InterstellarTerritoryRewardsScaleWithEnemyPowerAndAdvancedSectorOutputStaysLimited()
    {
        SectorDefinition alpha = Resources.Load<SectorDefinition>("Datas/Sector/AlphaCentauri");
        SectorDefinition proxima = Resources.Load<SectorDefinition>("Datas/Sector/ProximaB");
        SectorDefinition tau = Resources.Load<SectorDefinition>("Datas/Sector/TauCetiFoundry");
        SectorDefinition sirius = Resources.Load<SectorDefinition>("Datas/Sector/SiriusResourceBelt");

        Assert.That(alpha.TerritoryReward, Is.EqualTo(new ExpantaNum(700000d)));
        Assert.That(proxima.TerritoryReward, Is.EqualTo(new ExpantaNum(1500000d)));
        Assert.That(tau.TerritoryReward, Is.EqualTo(new ExpantaNum(3000000d)));
        Assert.That(sirius.TerritoryReward, Is.EqualTo(new ExpantaNum(7000000d)));
        Assert.That(FindOccupiedResourceRate(sirius, "PhaseMaterial"),
            Is.EqualTo(new ExpantaNum(0.02d)));
        Assert.That(FindOccupiedResourceRate(sirius, "PhantomAlloy"),
            Is.EqualTo(new ExpantaNum(0.12d)));
        Assert.That(FindOccupiedResourceRate(sirius, "PhantomWeave"),
            Is.EqualTo(new ExpantaNum(0.10d)));
    }

    [Test]
    public void InterstellarCampaignsUseTieredLongRangeProgressMultipliers()
    {
        SectorDefinition alpha = Resources.Load<SectorDefinition>("Datas/Sector/AlphaCentauri");
        SectorDefinition proxima = Resources.Load<SectorDefinition>("Datas/Sector/ProximaB");
        SectorDefinition tau = Resources.Load<SectorDefinition>("Datas/Sector/TauCetiFoundry");
        SectorDefinition sirius = Resources.Load<SectorDefinition>("Datas/Sector/SiriusResourceBelt");

        Assert.That(alpha.CampaignProgressMultiplier.ToDouble(), Is.EqualTo(0.015d).Within(0.000001d));
        Assert.That(proxima.CampaignProgressMultiplier.ToDouble(), Is.EqualTo(0.010d).Within(0.000001d));
        Assert.That(tau.CampaignProgressMultiplier.ToDouble(), Is.EqualTo(0.006d).Within(0.000001d));
        Assert.That(sirius.CampaignProgressMultiplier.ToDouble(), Is.EqualTo(0.003d).Within(0.000001d));
        Assert.That(alpha.CampaignProgressMultiplier, Is.GreaterThan(proxima.CampaignProgressMultiplier));
        Assert.That(proxima.CampaignProgressMultiplier, Is.GreaterThan(tau.CampaignProgressMultiplier));
        Assert.That(tau.CampaignProgressMultiplier, Is.GreaterThan(sirius.CampaignProgressMultiplier));
    }

    [Test]
    public void InterstellarCampaignsRemainLongWithOverwhelmingFleetPower()
    {
        Assert.That(GetOverwhelmingPowerCaptureMinutes("AlphaCentauri"), Is.GreaterThan(32d));
        Assert.That(GetOverwhelmingPowerCaptureMinutes("ProximaB"), Is.GreaterThan(48d));
        Assert.That(GetOverwhelmingPowerCaptureMinutes("TauCetiFoundry"), Is.GreaterThan(96d));
        Assert.That(GetOverwhelmingPowerCaptureMinutes("SiriusResourceBelt"), Is.GreaterThan(192d));
    }

    private static double GetOverwhelmingPowerCaptureMinutes(string sectorId)
    {
        SectorDefinition sector = Resources.Load<SectorDefinition>("Datas/Sector/" + sectorId);
        double progressPerSecond = CampaignManager.AdvanceProgress(
            ExpantaNum.Zero,
            new ExpantaNum(100d),
            60d,
            sector.CampaignProgressMultiplier).ToDouble();
        return progressPerSecond > 0d ? 1d / progressPerSecond : double.PositiveInfinity;
    }

    [Test]
    public void InterstellarCampaignsConsumeNickelForFleetStructuralMaintenance()
    {
        SectorDefinition alpha = Resources.Load<SectorDefinition>("Datas/Sector/AlphaCentauri");
        SectorDefinition proxima = Resources.Load<SectorDefinition>("Datas/Sector/ProximaB");
        SectorDefinition tau = Resources.Load<SectorDefinition>("Datas/Sector/TauCetiFoundry");
        SectorDefinition sirius = Resources.Load<SectorDefinition>("Datas/Sector/SiriusResourceBelt");

        Assert.That(FindCampaignCost(alpha, "Nickel").ToDouble(), Is.EqualTo(16d / 60d).Within(0.000001d));
        Assert.That(FindCampaignCost(proxima, "Nickel").ToDouble(), Is.EqualTo(32d / 60d).Within(0.000001d));
        Assert.That(FindCampaignCost(tau, "Nickel").ToDouble(), Is.EqualTo(48d / 60d).Within(0.000001d));
        Assert.That(FindCampaignCost(sirius, "Nickel").ToDouble(), Is.EqualTo(72d / 60d).Within(0.000001d));
    }

    [Test]
    public void InterstellarCampaignRewardsAndSupplyCostsScaleBeyondEarlySpaceflight()
    {
        SectorDefinition alpha = Resources.Load<SectorDefinition>("Datas/Sector/AlphaCentauri");
        SectorDefinition proxima = Resources.Load<SectorDefinition>("Datas/Sector/ProximaB");
        SectorDefinition tau = Resources.Load<SectorDefinition>("Datas/Sector/TauCetiFoundry");
        SectorDefinition sirius = Resources.Load<SectorDefinition>("Datas/Sector/SiriusResourceBelt");

        Assert.That(alpha.TerritoryReward, Is.GreaterThanOrEqualTo(new ExpantaNum(100000d)));
        Assert.That(proxima.TerritoryReward, Is.GreaterThanOrEqualTo(new ExpantaNum(180000d)));
        Assert.That(tau.TerritoryReward, Is.GreaterThanOrEqualTo(new ExpantaNum(220000d)));
        Assert.That(sirius.TerritoryReward, Is.GreaterThanOrEqualTo(new ExpantaNum(500000d)));
        Assert.That(FindCampaignCost(alpha, "RocketFuel"), Is.GreaterThanOrEqualTo(new ExpantaNum(100d / 60d)));
        Assert.That(FindCampaignCost(proxima, "RocketFuel"), Is.GreaterThanOrEqualTo(new ExpantaNum(160d / 60d)));
        Assert.That(FindCampaignCost(tau, "RocketFuel"), Is.GreaterThanOrEqualTo(new ExpantaNum(120d / 60d)));
        Assert.That(FindCampaignCost(sirius, "RocketFuel"), Is.GreaterThanOrEqualTo(new ExpantaNum(220d / 60d)));
        Assert.That(FindCampaignCost(tau, "RocketFuel"), Is.GreaterThan(FindCampaignCost(proxima, "RocketFuel")));
        Assert.That(FindCampaignCost(alpha, "Engine"), Is.GreaterThanOrEqualTo(new ExpantaNum(16d / 60d)));
        Assert.That(FindCampaignCost(proxima, "Engine"), Is.GreaterThanOrEqualTo(new ExpantaNum(32d / 60d)));
        Assert.That(FindCampaignCost(tau, "Engine"), Is.GreaterThanOrEqualTo(new ExpantaNum(48d / 60d)));
        Assert.That(FindCampaignCost(sirius, "Engine"), Is.GreaterThanOrEqualTo(new ExpantaNum(72d / 60d)));
        Assert.That(FindCampaignCost(proxima, "PhaseMaterial"), Is.EqualTo(new ExpantaNum(0.3d)));
        Assert.That(FindCampaignCost(tau, "PhaseMaterial"), Is.GreaterThan(FindCampaignCost(proxima, "PhaseMaterial")));
        Assert.That(FindCampaignCost(sirius, "PhaseMaterial"), Is.GreaterThan(FindCampaignCost(tau, "PhaseMaterial")));
        Assert.That(FindCampaignCost(alpha, "Biomass"), Is.GreaterThanOrEqualTo(new ExpantaNum(0.8d)));
        Assert.That(FindCampaignCost(proxima, "Biomass"), Is.GreaterThanOrEqualTo(new ExpantaNum(1.2d)));
        Assert.That(FindCampaignCost(tau, "Biomass"), Is.GreaterThanOrEqualTo(new ExpantaNum(1.8d)));
        Assert.That(FindCampaignCost(sirius, "Biomass"), Is.GreaterThanOrEqualTo(new ExpantaNum(2.6d)));
        Assert.That(FindCampaignCost(alpha, "Machinery"), Is.EqualTo(new ExpantaNum(0.8d)));
        Assert.That(FindCampaignCost(proxima, "Machinery"), Is.EqualTo(new ExpantaNum(0.9d)));
        Assert.That(FindCampaignCost(tau, "Machinery"), Is.EqualTo(new ExpantaNum(1.2d)));
        Assert.That(FindCampaignCost(sirius, "Machinery"), Is.EqualTo(new ExpantaNum(1.8d)));
    }

    [Test]
    public void 星际远征的一次性资源回报必须匹配后期投入规模()
    {
        SectorDefinition[] sectors =
        {
            Resources.Load<SectorDefinition>("Datas/Sector/AlphaCentauri"),
            Resources.Load<SectorDefinition>("Datas/Sector/ProximaB"),
            Resources.Load<SectorDefinition>("Datas/Sector/TauCetiFoundry"),
            Resources.Load<SectorDefinition>("Datas/Sector/SiriusResourceBelt")
        };
        ExpantaNum[] minimumRewards =
        {
            new ExpantaNum(50000d),
            new ExpantaNum(75000d),
            new ExpantaNum(100000d),
            new ExpantaNum(250000d)
        };

        for (int i = 0; i < sectors.Length; i++)
        {
            Assert.That(sectors[i], Is.Not.Null);
            Assert.That(GetOneTimeResourceRewardTotal(sectors[i]),
                Is.GreaterThanOrEqualTo(minimumRewards[i]), sectors[i].Id);
        }
    }

    [Test]
    public void 星际远征规模必须显著高于近地轨道探索()
    {
        SectorDefinition lowOrbit = Resources.Load<SectorDefinition>("Datas/Sector/LowOrbit");
        SectorDefinition[] interstellarSectors =
        {
            Resources.Load<SectorDefinition>("Datas/Sector/AlphaCentauri"),
            Resources.Load<SectorDefinition>("Datas/Sector/ProximaB"),
            Resources.Load<SectorDefinition>("Datas/Sector/TauCetiFoundry"),
            Resources.Load<SectorDefinition>("Datas/Sector/SiriusResourceBelt")
        };

        Assert.That(lowOrbit, Is.Not.Null);
        for (int i = 0; i < interstellarSectors.Length; i++)
        {
            SectorDefinition sector = interstellarSectors[i];
            Assert.That(sector, Is.Not.Null);
            Assert.That(sector.TerritoryReward,
                Is.GreaterThan(lowOrbit.TerritoryReward * 100d), sector.Id);
            Assert.That(sector.CampaignResourceRatesPerSecond.Count,
                Is.GreaterThanOrEqualTo(8), sector.Id);
            Assert.That(sector.CampaignProgressMultiplier,
                Is.LessThanOrEqualTo(new ExpantaNum(0.015d)), sector.Id);
        }
    }

    [Test]
    public void 星际战役必须持续消耗高真空润滑剂维护远征机械()
    {
        SectorDefinition alpha = Resources.Load<SectorDefinition>("Datas/Sector/AlphaCentauri");
        SectorDefinition proxima = Resources.Load<SectorDefinition>("Datas/Sector/ProximaB");
        SectorDefinition tau = Resources.Load<SectorDefinition>("Datas/Sector/TauCetiFoundry");
        SectorDefinition sirius = Resources.Load<SectorDefinition>("Datas/Sector/SiriusResourceBelt");

        Assert.That(FindCampaignCost(alpha, "Lubricant"), Is.EqualTo(new ExpantaNum(0.05d)));
        Assert.That(FindCampaignCost(proxima, "Lubricant"), Is.EqualTo(new ExpantaNum(0.10d)));
        Assert.That(FindCampaignCost(tau, "Lubricant"), Is.EqualTo(new ExpantaNum(0.15d)));
        Assert.That(FindCampaignCost(sirius, "Lubricant"), Is.EqualTo(new ExpantaNum(0.20d)));
    }

    [Test]
    public void 星际战役的单位进度后勤成本必须随远征阶段递增()
    {
        SectorDefinition alpha = Resources.Load<SectorDefinition>("Datas/Sector/AlphaCentauri");
        SectorDefinition proxima = Resources.Load<SectorDefinition>("Datas/Sector/ProximaB");
        SectorDefinition tau = Resources.Load<SectorDefinition>("Datas/Sector/TauCetiFoundry");
        SectorDefinition sirius = Resources.Load<SectorDefinition>("Datas/Sector/SiriusResourceBelt");
        string[] resources = { "RocketFuel", "Engine", "Nickel", "Lubricant", "Biomass" };

        for (int i = 0; i < resources.Length; i++)
        {
            double alphaCost = FindCampaignCost(alpha, resources[i]).ToDouble() /
                alpha.CampaignProgressMultiplier.ToDouble();
            double proximaCost = FindCampaignCost(proxima, resources[i]).ToDouble() /
                proxima.CampaignProgressMultiplier.ToDouble();
            double tauCost = FindCampaignCost(tau, resources[i]).ToDouble() /
                tau.CampaignProgressMultiplier.ToDouble();
            double siriusCost = FindCampaignCost(sirius, resources[i]).ToDouble() /
                sirius.CampaignProgressMultiplier.ToDouble();

            Assert.That(proximaCost, Is.GreaterThan(alphaCost), resources[i]);
            Assert.That(tauCost, Is.GreaterThan(proximaCost), resources[i]);
            Assert.That(siriusCost, Is.GreaterThan(tauCost), resources[i]);
        }
    }

    [Test]
    public void 所有星际战役都必须是高价值长周期高级材料远征()
    {
        IReadOnlyList<SectorDefinition> sectors = DataBase<SectorDefinition>.All;
        string[] advancedResources =
        {
            "TitaniumAlloy", "Composite", "PhantomAlloy", "PhantomWeave", "PhaseMaterial"
        };

        for (int i = 0; i < sectors.Count; i++)
        {
            SectorDefinition sector = sectors[i];
            if (sector == null || sector.Domain != SectorDefinition.SectorDomain.Interstellar)
                continue;

            Assert.That(sector.TerritoryReward, Is.GreaterThanOrEqualTo(new ExpantaNum(100000d)), sector.Id);
            Assert.That(sector.CampaignProgressMultiplier, Is.GreaterThan(ExpantaNum.Zero), sector.Id);
            Assert.That(sector.CampaignProgressMultiplier, Is.LessThanOrEqualTo(new ExpantaNum(0.015d)), sector.Id);
            Assert.That(sector.CampaignFoodPerSecond, Is.GreaterThan(ExpantaNum.Zero), sector.Id);
            Assert.That(sector.CampaignResourceRatesPerSecond.Count, Is.GreaterThanOrEqualTo(8), sector.Id);

            int advancedSupplyCount = 0;
            for (int resourceIndex = 0; resourceIndex < advancedResources.Length; resourceIndex++)
            {
                if (HasCampaignCost(sector, advancedResources[resourceIndex]))
                    advancedSupplyCount++;
            }

            Assert.That(advancedSupplyCount, Is.GreaterThanOrEqualTo(3), sector.Id);
        }
    }

    private static ExpantaNum FindCampaignCost(SectorDefinition sector, string resourceId)
    {
        for (int i = 0; i < sector.CampaignResourceRatesPerSecond.Count; i++)
            if (sector.CampaignResourceRatesPerSecond[i].First != null &&
                sector.CampaignResourceRatesPerSecond[i].First.Id == resourceId)
                return sector.CampaignResourceRatesPerSecond[i].Second;
        return ExpantaNum.Zero;
    }

    private static ExpantaNum GetColonizationResourceTotal(
        SectorDefinition sector,
        string resourceId)
    {
        ExpantaNum rate = ExpantaNum.Zero;
        for (int i = 0; i < sector.ColonizationResourceRatesPerSecond.Count; i++)
            if (sector.ColonizationResourceRatesPerSecond[i].First != null &&
                sector.ColonizationResourceRatesPerSecond[i].First.Id == resourceId)
                rate += sector.ColonizationResourceRatesPerSecond[i].Second;
        return rate * sector.ColonizationDurationSeconds;
    }

    private static ExpantaNum FindOccupiedResourceRate(
        SectorDefinition sector,
        string resourceId)
    {
        for (int i = 0; i < sector.OccupiedResourceRatesPerSecond.Count; i++)
            if (sector.OccupiedResourceRatesPerSecond[i].First != null &&
                sector.OccupiedResourceRatesPerSecond[i].First.Id == resourceId)
                return sector.OccupiedResourceRatesPerSecond[i].Second;
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

    private static ExpantaNum GetOneTimeResourceRewardTotal(SectorDefinition sector)
    {
        ExpantaNum total = ExpantaNum.Zero;
        for (int i = 0; i < sector.ResourceRewards.Count; i++)
        {
            Pair<Resource, ExpantaNum> reward = sector.ResourceRewards[i];
            if (reward.First != null && reward.Second > ExpantaNum.Zero)
                total += reward.Second;
        }
        return total;
    }

    private static bool HasCampaignCost(SectorDefinition sector, string resourceId)
    {
        for (int i = 0; i < sector.CampaignResourceRatesPerSecond.Count; i++)
            if (sector.CampaignResourceRatesPerSecond[i].First != null &&
                sector.CampaignResourceRatesPerSecond[i].First.Id == resourceId &&
                sector.CampaignResourceRatesPerSecond[i].Second > ExpantaNum.Zero)
                return true;
        return false;
    }

    [Test]
    public void NearEarthSectorChainRequiresLaunchResearchAndSequentialOccupation()
    {
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorDefinition mars = DataBase<SectorDefinition>.Find("Mars");
        Building launchCenter = DataBase<Building>.Find("LaunchCenter");
        Research orbitalEngineering = DataBase<Research>.Find("OrbitalEngineering");

        Assert.That(lowOrbit, Is.Not.Null);
        Assert.That(moon, Is.Not.Null);
        Assert.That(mars, Is.Not.Null);
        Assert.That(launchCenter, Is.Not.Null);
        Assert.That(orbitalEngineering, Is.Not.Null);
        Assert.That(launchCenter.RequiredResearch, Does.Contain(orbitalEngineering));
        Assert.That(lowOrbit.PrerequisiteSectors, Is.Empty);
        Assert.That(moon.PrerequisiteSectors, Does.Contain(lowOrbit));
        Assert.That(mars.PrerequisiteSectors, Does.Contain(moon));
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
    public void SiriusRequiresOccupiedTauCetiBeforeAccess()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition tau = DataBase<SectorDefinition>.Find("TauCetiFoundry");
        SectorDefinition sirius = DataBase<SectorDefinition>.Find("SiriusResourceBelt");

        Assert.That(tau, Is.Not.Null);
        Assert.That(sirius, Is.Not.Null);
        Assert.That(manager.CanAccess(sirius), Is.False,
            "Tau Ceti 未占领时不应开放天狼资源带。");

        manager.GetState(tau).SetOccupiedForEditor(true);

        Assert.That(manager.CanAccess(sirius), Is.True,
            "Tau Ceti 占领后应开放天狼资源带访问。");
    }

    [Test]
    public void SiriusUnlockReportsMissingTauCetiPrerequisite()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition sirius = DataBase<SectorDefinition>.Find("SiriusResourceBelt");

        Assert.That(sirius, Is.Not.Null);
        Assert.That(manager.TryUnlock(sirius, out SectorOperationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.PrerequisiteNotOccupied));
        Assert.That(manager.GetState(sirius).Unlocked, Is.False);
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
        for (int i = 0; i < lowOrbit.ColonizationResourceRatesPerSecond.Count; i++)
                resourceManager.SetAmount(
                    lowOrbit.ColonizationResourceRatesPerSecond[i].First,
                    lowOrbit.ColonizationResourceRatesPerSecond[i].Second);

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
    public void C815_殖民补给不足时Tick不会报告推进成功()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorState state = manager.GetState(moon);
        state.SetUnlockedForEditor(true);
        InvokeSectorStateMethod(state, "SetColonizationActive", true);
        var runtimeState = new GameState();
        InvokeGameStateMethod(runtimeState, "AdjustAttackPower", new ExpantaNum(100000));
        InvokeGameStateMethod(runtimeState, "AdjustDefensePower", new ExpantaNum(100000));

        bool advanced = manager.TickActiveColonization(
            60d,
            runtimeState,
            null,
            out SectorOperationFailure failure);

        Assert.That(advanced, Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.InsufficientCampaignSupply));
        Assert.That(state.CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void InterstellarCampaignUsesOneFleetTargetAtATime()
    {
        GameObject resourceObject = new GameObject("单一星区战役资源管理器");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            SectorDefinition first = DataBase<SectorDefinition>.Find("ProximaB");
            SectorDefinition second = DataBase<SectorDefinition>.Find("TauCetiFoundry");
            foreach (SectorDefinition sector in new[] { first, second })
            {
                for (int i = 0; i < sector.CampaignResourceRatesPerSecond.Count; i++)
                    resourceManager.SetAmount(
                        sector.CampaignResourceRatesPerSecond[i].First,
                        new ExpantaNum(100000));
            }

            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            manager.GetState(first).SetUnlockedForEditor(true);
            manager.GetState(second).SetUnlockedForEditor(true);
            GameState runtimeState = new GameState();
            InvokeProgressionStateMethod(ResearchSystem.FirstContact);
            InvokeProgressionStateMethod(ResearchSystem.InterstellarNavigation);
            InvokeProgressionStateMethod(ResearchSystem.DeepSpaceFleet);
            InvokeGameStateMethod(runtimeState, "AdjustAttackPower", new ExpantaNum(100000));
            InvokeGameStateMethod(runtimeState, "AdjustDefensePower", new ExpantaNum(100000));
            InvokeGameStateMethod(runtimeState, "AdjustMilitaryManpower", new ExpantaNum(100000));
            InvokeGameStateMethod(runtimeState, "SetSupplySatisfaction", ExpantaNum.One);
            InvokeGameStateMethod(runtimeState, "SetPowerSatisfaction", ExpantaNum.One);
            InvokeGameStateMethod(runtimeState, "SetLogisticsSatisfaction", ExpantaNum.One);

            Assert.That(manager.TryAdvanceCampaign(
                first, 0d, runtimeState, resourceManager, out SectorOperationFailure firstFailure), Is.True);
            Assert.That(firstFailure, Is.EqualTo(SectorOperationFailure.None));
            Assert.That(manager.TryAdvanceCampaign(
                second, 60d, runtimeState, resourceManager, out SectorOperationFailure secondFailure), Is.False);
            Assert.That(secondFailure, Is.EqualTo(SectorOperationFailure.CampaignInProgress));
            Assert.That(manager.GetState(second).CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(runtimeState.Campaign.TargetSectorId, Is.EqualTo(first.Id));
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void InterstellarCampaignConsumesEveryConfiguredContinuousResource()
    {
        GameObject resourceObject = new GameObject();
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            SectorDefinition sector = DataBase<SectorDefinition>.Find("AlphaCentauri");
            const double deltaSeconds = 60d;
            ExpantaNum initialAmount = new ExpantaNum(100000d);
            for (int i = 0; i < sector.CampaignResourceRatesPerSecond.Count; i++)
            {
                Pair<Resource, ExpantaNum> rate = sector.CampaignResourceRatesPerSecond[i];
                resourceManager.SetAmount(rate.First, initialAmount);
            }

            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            manager.GetState(sector).SetUnlockedForEditor(true);
            GameState runtimeState = new GameState();
            InvokeProgressionStateMethod(ResearchSystem.FirstContact);
            InvokeProgressionStateMethod(ResearchSystem.InterstellarNavigation);
            InvokeProgressionStateMethod(ResearchSystem.DeepSpaceFleet);
            InvokeGameStateMethod(runtimeState, "AdjustAttackPower", new ExpantaNum(100000));
            InvokeGameStateMethod(runtimeState, "AdjustDefensePower", new ExpantaNum(100000));
            InvokeGameStateMethod(runtimeState, "AdjustMilitaryManpower", new ExpantaNum(100000));
            InvokeGameStateMethod(runtimeState, "SetSupplySatisfaction", ExpantaNum.One);
            InvokeGameStateMethod(runtimeState, "SetPowerSatisfaction", ExpantaNum.One);
            InvokeGameStateMethod(runtimeState, "SetLogisticsSatisfaction", ExpantaNum.One);

            Assert.That(
                manager.TryAdvanceCampaign(
                    sector,
                    deltaSeconds,
                    runtimeState,
                    resourceManager,
                    out SectorOperationFailure failure),
                Is.True);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));
            Assert.That(
                runtimeState.FoodAmount,
                Is.EqualTo(new ExpantaNum(300d) - sector.CampaignFoodPerSecond * deltaSeconds));

            for (int i = 0; i < sector.CampaignResourceRatesPerSecond.Count; i++)
            {
                Pair<Resource, ExpantaNum> rate = sector.CampaignResourceRatesPerSecond[i];
                Assert.That(
                    resourceManager.GetAmount(rate.First),
                    Is.EqualTo(initialAmount - rate.Second * deltaSeconds),
                    rate.First.Id);
            }
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void CancellingGlobalCampaignAlsoStopsItsSectorState()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition sector = DataBase<SectorDefinition>.Find("ProximaB");
        SectorState state = manager.GetState(sector);
        state.SetCampaignActiveForEditor(true);
        GameState runtimeState = new GameState();
        InvokeGameStateMethod(runtimeState, "BeginCampaign", sector.Id);

        Assert.That(manager.CancelCampaign(runtimeState), Is.True);
        Assert.That(state.CampaignActive, Is.False);
        Assert.That(runtimeState.Campaign.Active, Is.False);
    }

    [Test]
    public void CancellingDifferentSectorCannotBreakActiveCampaignTarget()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition activeSector = DataBase<SectorDefinition>.Find("ProximaB");
        SectorDefinition otherSector = DataBase<SectorDefinition>.Find("TauCetiFoundry");
        SectorState otherState = manager.GetState(otherSector);
        otherState.SetCampaignActiveForEditor(true);
        GameState runtimeState = new GameState();
        InvokeGameStateMethod(runtimeState, "BeginCampaign", activeSector.Id);

        Assert.That(manager.CancelCampaign(otherSector, runtimeState), Is.False);
        Assert.That(otherState.CampaignActive, Is.True);
        Assert.That(runtimeState.Campaign.TargetSectorId, Is.EqualTo(activeSector.Id));
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
    public void SectorOccupy_RestoresStateWhenRewardApplicationFails()
    {
        var manager = new SectorManager(_ =>
            throw new System.InvalidOperationException("test reward failure"));
        manager.InitializeDefinitions();
        SectorState state = manager.GetState(DataBase<SectorDefinition>.Find("LowOrbit"));
        state.SetUnlockedForEditor(true);
        state.SetVisitCountForEditor(2);

        Assert.Throws<System.InvalidOperationException>(() =>
            manager.TryOccupy(state.Definition, out _));

        Assert.That(state.Occupied, Is.False);
        Assert.That(state.VisitCount, Is.EqualTo(2));
    }

    [Test]
    public void C705_SectorRewardsUseExistingStrategicResources()
    {
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorDefinition mars = DataBase<SectorDefinition>.Find("Mars");
        Resource composite = DataBase<Resource>.Find("Composite");
        Resource rocketFuel = DataBase<Resource>.Find("RocketFuel");
        Resource titaniumAlloy = DataBase<Resource>.Find("TitaniumAlloy");
        Resource nickel = DataBase<Resource>.Find("Nickel");

        Assert.That(moon.ResourceRewards, Has.Count.EqualTo(2));
        Assert.That(moon.ResourceRewards[0].First, Is.EqualTo(composite));
        Assert.That(moon.ResourceRewards[0].Second, Is.EqualTo(new ExpantaNum(12000)));
        Assert.That(moon.ResourceRewards[1].First, Is.EqualTo(titaniumAlloy));
        Assert.That(moon.ResourceRewards[1].Second, Is.EqualTo(new ExpantaNum(6000)));
        Assert.That(mars.ResourceRewards, Has.Count.EqualTo(2));
        Assert.That(mars.ResourceRewards[0].First, Is.EqualTo(rocketFuel));
        Assert.That(mars.ResourceRewards[0].Second, Is.EqualTo(new ExpantaNum(18000)));
        Assert.That(mars.ResourceRewards[1].First, Is.EqualTo(nickel));
        Assert.That(mars.ResourceRewards[1].Second, Is.EqualTo(new ExpantaNum(10000)));
    }

    [Test]
    public void 近地殖民总成本必须随阶段和回报逐级增长()
    {
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorDefinition mars = DataBase<SectorDefinition>.Find("Mars");

        ExpantaNum lowOrbitFood = lowOrbit.ColonizationFoodPerSecond * lowOrbit.ColonizationDurationSeconds;
        ExpantaNum moonFood = moon.ColonizationFoodPerSecond * moon.ColonizationDurationSeconds;
        ExpantaNum marsFood = mars.ColonizationFoodPerSecond * mars.ColonizationDurationSeconds;
        ExpantaNum moonRocketFuel = GetColonizationResourceTotal(moon, "RocketFuel");
        ExpantaNum marsRocketFuel = GetColonizationResourceTotal(mars, "RocketFuel");

        Assert.That(lowOrbitFood, Is.EqualTo(new ExpantaNum(10d)).Within(0.000001d));
        Assert.That(moonFood, Is.EqualTo(new ExpantaNum(4800d)).Within(0.000001d));
        Assert.That(marsFood, Is.EqualTo(new ExpantaNum(16800d)).Within(0.000001d));
        Assert.That(lowOrbitFood, Is.LessThan(moonFood));
        Assert.That(moonFood, Is.LessThan(marsFood));
        Assert.That(moonRocketFuel, Is.EqualTo(new ExpantaNum(1500d)).Within(0.000001d));
        Assert.That(marsRocketFuel, Is.EqualTo(new ExpantaNum(5400d)).Within(0.000001d));
        Assert.That(moonRocketFuel, Is.LessThan(marsRocketFuel));
        Assert.That(moon.TerritoryReward, Is.GreaterThan(lowOrbit.TerritoryReward));
        Assert.That(mars.TerritoryReward, Is.GreaterThan(moon.TerritoryReward));
    }

    [Test]
    public void 近地星区占领产出必须按阶段承担不同战略职责()
    {
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorDefinition mars = DataBase<SectorDefinition>.Find("Mars");

        Assert.That(FindOccupiedResourceRate(lowOrbit, "Electronics"),
            Is.EqualTo(new ExpantaNum(0.04d)).Within(0.000001d));
        Assert.That(FindOccupiedResourceRate(moon, "TitaniumAlloy"),
            Is.EqualTo(new ExpantaNum(0.18d)).Within(0.000001d));
        Assert.That(FindOccupiedResourceRate(moon, "Composite"),
            Is.EqualTo(new ExpantaNum(0.12d)).Within(0.000001d));
        Assert.That(FindOccupiedResourceRate(mars, "Nickel"),
            Is.EqualTo(new ExpantaNum(0.25d)).Within(0.000001d));
        Assert.That(FindOccupiedResourceRate(mars, "RocketFuel"),
            Is.EqualTo(new ExpantaNum(0.12d)).Within(0.000001d));
        Assert.That(FindOccupiedResourceRate(mars, "Electronics"),
            Is.GreaterThan(FindOccupiedResourceRate(moon, "Electronics")));
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
    public void C804_战役补给不足时Tick不会报告推进成功()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorState state = manager.GetState(moon);
        state.SetUnlockedForEditor(true);
        state.SetCampaignActiveForEditor(true);
        var runtimeState = new GameState();

        bool advanced = manager.TickActiveCampaign(
            60d,
            runtimeState,
            null,
            out SectorOperationFailure failure);

        Assert.That(advanced, Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.InsufficientCampaignSupply));
        Assert.That(state.CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(runtimeState.Campaign.Active, Is.False);
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
        Assert.That(preview.ProgressPerSecond.ToDouble(), Is.EqualTo(0.625d / 60d).Within(0.000001d));
        Assert.That(preview.EstimatedSecondsRemaining.ToDouble(), Is.EqualTo(1.6d * 60d).Within(0.000001d));
        Assert.That(preview.CasualtiesPerSecond, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(preview.FoodCostPerSecond, Is.EqualTo(new ExpantaNum(2)));
        Assert.That(preview.ResourceCostsPerSecond, Has.Count.EqualTo(1));
        Assert.That(preview.ResourceCostsPerSecond[0].First.Id, Is.EqualTo("RocketFuel"));
        Assert.That(preview.HasSupply, Is.False);
    }

    [Test]
    public void 自动化舰队补给模块会直接降低战役预览中的持续消耗()
    {
        WorkshopUpgrade definition = Resources.Load<WorkshopUpgrade>(
            "Datas/Workshop/AutomatedFleetResupplyModules");
        WorkshopUpgradeState workshopState = new WorkshopUpgradeState(definition);
        typeof(WorkshopUpgradeState).GetMethod(
            "SetPurchased",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(workshopState, new object[] { true });

        try
        {
            ProgressionModifierManager.Rebuild(null, new[] { workshopState });
            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
            manager.GetState(moon).SetUnlockedForEditor(true);
            SectorCampaignPreview preview = manager.GetCampaignPreview(
                moon,
                new GameState(),
                null);

            Assert.That(preview.FoodCostPerSecond, Is.EqualTo(new ExpantaNum(1.8d)));
            Assert.That(preview.ResourceCostsPerSecond[0].Second, Is.EqualTo(new ExpantaNum(0.9d)));
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
        }
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
    public void C808b_DuplicateSectorStateIdsAreRejected()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        var duplicateStates = new List<SaveManager.SectorStateSaveData>
        {
            new SaveManager.SectorStateSaveData
            {
                SectorId = moon.Id,
                CampaignProgress = "0",
                CampaignCasualties = "0",
                CampaignCombatRatio = "0"
            },
            new SaveManager.SectorStateSaveData
            {
                SectorId = moon.Id,
                CampaignProgress = "1",
                CampaignCasualties = "0",
                CampaignCombatRatio = "0"
            }
        };

        Assert.Throws<System.InvalidOperationException>(() =>
            manager.RestoreSaveData(new SaveManager.SectorSaveData
            {
                States = duplicateStates
            }));
    }

    [Test]
    public void C808c_InvalidSectorStateIsRejectedBeforeAnyStateIsApplied()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("LowOrbit");
        SectorState moonState = manager.GetState(moon);
        moonState.SetUnlockedForEditor(true);

        Assert.Throws<System.InvalidOperationException>(() =>
            manager.RestoreSaveData(new SaveManager.SectorSaveData
            {
                States = new List<SaveManager.SectorStateSaveData>
                {
                    new SaveManager.SectorStateSaveData
                    {
                        SectorId = moon.Id,
                        Unlocked = true,
                        CampaignProgress = "0.25",
                        CampaignCasualties = "0",
                        CampaignCombatRatio = "0",
                        VisitCount = 2
                    },
                    new SaveManager.SectorStateSaveData
                    {
                        SectorId = lowOrbit.Id,
                        CampaignProgress = "0",
                        CampaignCasualties = "0",
                        CampaignCombatRatio = "0",
                        VisitCount = -1
                    }
                }
            }));

        Assert.That(moonState.Unlocked, Is.True);
        Assert.That(moonState.CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void C808c_UnknownSectorStateIsRejectedBeforeExistingStateIsCleared()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
        SectorState moonState = manager.GetState(moon);
        moonState.SetUnlockedForEditor(true);

        Assert.Throws<InvalidOperationException>(() =>
            manager.RestoreSaveData(new SaveManager.SectorSaveData
            {
                States = new List<SaveManager.SectorStateSaveData>
                {
                    new SaveManager.SectorStateSaveData
                    {
                        SectorId = "MissingSector",
                        CampaignProgress = "0",
                        CampaignCasualties = "0",
                        CampaignCombatRatio = "0"
                    }
                }
            }));

        Assert.That(moonState.Unlocked, Is.True);
    }

    [Test]
    public void C808d_ContradictorySectorFlagsAreRejected()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");

        Assert.Throws<System.InvalidOperationException>(() =>
            manager.RestoreSaveData(new SaveManager.SectorSaveData
            {
                States = new List<SaveManager.SectorStateSaveData>
                {
                    new SaveManager.SectorStateSaveData
                    {
                        SectorId = moon.Id,
                        Unlocked = true,
                        Occupied = true,
                        ColonizationActive = true,
                        CampaignProgress = "0",
                        CampaignCasualties = "0",
                        CampaignCombatRatio = "0"
                    }
                }
            }));
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
            resourceManager.SetAmount(DataBase<Resource>.Find("Composite"), new ExpantaNum(10));
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
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("Composite")), Is.EqualTo(new ExpantaNum(5)));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("PhantomWeave")), Is.EqualTo(new ExpantaNum(5)));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("RocketFuel")), Is.EqualTo(new ExpantaNum(2.5d)));
        }
        finally
        {
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void C808e_CancelledCampaignSaveRemainsInactiveWhileRetainingCasualties()
    {
        GameObject gameObject = new GameObject("C808e-GameManager");
        try
        {
            GameManager gameManager = gameObject.AddComponent<GameManager>();
            InvokeGameStateMethod(gameManager.State, "RestoreCampaign", false, "ProximaB",
                new ExpantaNum(10), new ExpantaNum(0.8d));

            Assert.That(gameManager.State.Campaign.Active, Is.False);
            Assert.That(gameManager.State.Campaign.TargetSectorId, Is.EqualTo("ProximaB"));
            Assert.That(gameManager.State.Campaign.Casualties, Is.EqualTo(new ExpantaNum(10)));
            Assert.That(gameManager.State.Campaign.CombatRatio, Is.EqualTo(new ExpantaNum(0.8d)));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void C808f_ActiveCampaignSaveRequiresKnownTarget()
    {
        GameObject gameObject = new GameObject("C808f-GameManager");
        try
        {
            GameManager gameManager = gameObject.AddComponent<GameManager>();
            System.Reflection.TargetInvocationException exception =
                Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                InvokeGameManagerMethod(
                    gameManager,
                    "RestoreSaveData",
                    new SaveManager.GameSaveData
                    {
                        FoodAmount = "300",
                        CampaignActive = true,
                        CampaignTargetSectorId = "missing-sector-id"
                    }));

            Assert.That(exception.InnerException, Is.TypeOf<System.IO.InvalidDataException>());
            StringAssert.Contains("missing-sector-id", exception.InnerException.Message);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void CampaignRewardFailureRollsBackPaymentAndCompletionState()
    {
        GameObject resourceObject = new GameObject("Campaign-Reward-Rollback-ResourceManager");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            Resource rocketFuel = DataBase<Resource>.Find("RocketFuel");
            resourceManager.SetAmount(rocketFuel, new ExpantaNum(10));

            var manager = new SectorManager(_ =>
                throw new System.InvalidOperationException("test reward failure"));
            manager.InitializeDefinitions();
            SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
            SectorState state = manager.GetState(moon);
            state.SetUnlockedForEditor(true);
            var runtimeState = new GameState();
            InvokeGameStateMethod(runtimeState, "AdjustAttackPower", new ExpantaNum(100));
            InvokeGameStateMethod(runtimeState, "AdjustMilitaryManpower", new ExpantaNum(100));

            Assert.Throws<System.InvalidOperationException>(() =>
                manager.TryAdvanceCampaign(
                    moon,
                    240d,
                    runtimeState,
                    resourceManager,
                    out _));

            Assert.That(runtimeState.FoodAmount, Is.EqualTo(new ExpantaNum(300)));
            Assert.That(resourceManager.GetAmount(rocketFuel), Is.EqualTo(new ExpantaNum(10)));
            Assert.That(state.Occupied, Is.False);
            Assert.That(state.CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(state.CampaignCasualties, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(state.CampaignActive, Is.False);
            Assert.That(runtimeState.Campaign.Active, Is.False);
            Assert.That(runtimeState.Campaign.TargetSectorId, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void CampaignFailureRestoresFoodExactlyWhenCommitChangesCapacity()
    {
        GameObject resourceObject = new GameObject("Campaign-Food-Capacity-Rollback-ResourceManager");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            Resource rocketFuel = DataBase<Resource>.Find("RocketFuel");
            resourceManager.SetAmount(rocketFuel, new ExpantaNum(10));
            var runtimeState = new GameState();
            var manager = new SectorManager(_ =>
            {
                InvokeGameStateMethod(runtimeState, "AdjustFoodCapacity", new ExpantaNum(-400));
                throw new System.InvalidOperationException("test food-capacity failure");
            });
            manager.InitializeDefinitions();
            SectorDefinition moon = DataBase<SectorDefinition>.Find("Moon");
            SectorState state = manager.GetState(moon);
            state.SetUnlockedForEditor(true);
            InvokeGameStateMethod(runtimeState, "AdjustAttackPower", new ExpantaNum(100));
            InvokeGameStateMethod(runtimeState, "AdjustMilitaryManpower", new ExpantaNum(100));

            Assert.Throws<System.InvalidOperationException>(() =>
                manager.TryAdvanceCampaign(moon, 240d, runtimeState, resourceManager, out _));

            Assert.That(runtimeState.FoodAmount, Is.EqualTo(new ExpantaNum(300)));
            Assert.That(runtimeState.FoodCapacity, Is.EqualTo(new ExpantaNum(500)));
            Assert.That(resourceManager.GetAmount(rocketFuel), Is.EqualTo(new ExpantaNum(10)));
            Assert.That(state.Occupied, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void InterstellarCombatSupplySystemsReduceFleetRepairConsumption()
    {
        GameObject resourceObject = new GameObject("星际战斗补给维修测试资源管理器");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            resourceManager.SetAmount(DataBase<Resource>.Find("TitaniumAlloy"), new ExpantaNum(20));
            resourceManager.SetAmount(DataBase<Resource>.Find("Composite"), new ExpantaNum(10));
            resourceManager.SetAmount(DataBase<Resource>.Find("PhantomWeave"), new ExpantaNum(10));
            resourceManager.SetAmount(DataBase<Resource>.Find("RocketFuel"), new ExpantaNum(5));

            WorkshopUpgrade definition =
                Resources.Load<WorkshopUpgrade>("Datas/Workshop/InterstellarCombatSupplySystems");
            WorkshopUpgradeState workshopState = new WorkshopUpgradeState(definition);
            typeof(WorkshopUpgradeState).GetMethod(
                "SetPurchased",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(workshopState, new object[] { true });
            ProgressionModifierManager.Rebuild(null, new[] { workshopState });

            GameState runtimeState = new GameState();
            InvokeGameStateMethod(runtimeState, "BeginCampaign", "ProximaB");
            InvokeGameStateMethod(runtimeState, "RecordCampaignCombat", new ExpantaNum(0.8d), new ExpantaNum(10));
            SectorManager manager = new SectorManager(_ => { });

            Assert.That(manager.TryRepairFleet(
                runtimeState,
                resourceManager,
                new ExpantaNum(5),
                out ExpantaNum repairedAmount,
                out SectorOperationFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));
            Assert.That(repairedAmount, Is.EqualTo(new ExpantaNum(5)));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("TitaniumAlloy")), Is.EqualTo(new ExpantaNum(12)));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("Composite")), Is.EqualTo(new ExpantaNum(6)));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("PhantomWeave")), Is.EqualTo(new ExpantaNum(6)));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("RocketFuel")), Is.EqualTo(new ExpantaNum(3)));
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void 取消远征后舰队损伤必须保留并可维修()
    {
        GameObject resourceObject = new GameObject("取消远征维修测试资源管理器");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            resourceManager.SetAmount(DataBase<Resource>.Find("TitaniumAlloy"), new ExpantaNum(40));
            resourceManager.SetAmount(DataBase<Resource>.Find("Composite"), new ExpantaNum(20));
            resourceManager.SetAmount(DataBase<Resource>.Find("PhantomWeave"), new ExpantaNum(20));
            resourceManager.SetAmount(DataBase<Resource>.Find("RocketFuel"), new ExpantaNum(10));

            SectorDefinition sector = DataBase<SectorDefinition>.Find("ProximaB");
            SectorManager manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            SectorState sectorState = manager.GetState(sector);
            sectorState.SetUnlockedForEditor(true);
            sectorState.SetCampaignActiveForEditor(true);
            sectorState.SetCampaignCasualtiesForEditor(new ExpantaNum(10));

            GameState runtimeState = new GameState();
            InvokeGameStateMethod(runtimeState, "BeginCampaign", sector.Id);
            InvokeGameStateMethod(runtimeState, "RecordCampaignCombat", new ExpantaNum(0.8d), new ExpantaNum(10));

            Assert.That(manager.CancelCampaign(sector, runtimeState), Is.True);
            Assert.That(runtimeState.Campaign.Active, Is.False);
            Assert.That(runtimeState.Campaign.TargetSectorId, Is.EqualTo(sector.Id));
            Assert.That(runtimeState.Campaign.Casualties, Is.EqualTo(new ExpantaNum(10)));

            Assert.That(manager.TryAdvanceCampaign(
                sector, 0d, runtimeState, resourceManager, out SectorOperationFailure blockedFailure), Is.False);
            Assert.That(blockedFailure, Is.EqualTo(SectorOperationFailure.FleetRepairRequired));

            Assert.That(manager.TryRepairFleet(
                sector,
                runtimeState,
                resourceManager,
                new ExpantaNum(5),
                out ExpantaNum repairedAmount,
                out SectorOperationFailure repairFailure), Is.True);
            Assert.That(repairFailure, Is.EqualTo(SectorOperationFailure.None));
            Assert.That(repairedAmount, Is.EqualTo(new ExpantaNum(5)));
            Assert.That(runtimeState.Campaign.Casualties, Is.EqualTo(new ExpantaNum(5)));
            Assert.That(sectorState.CampaignCasualties, Is.EqualTo(new ExpantaNum(5)));

            Assert.That(manager.TryRepairFleet(
                sector,
                runtimeState,
                resourceManager,
                new ExpantaNum(5),
                out repairedAmount,
                out repairFailure), Is.True);
            Assert.That(runtimeState.Campaign.Casualties, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(runtimeState.Campaign.TargetSectorId, Is.Empty);
            Assert.That(sectorState.CampaignCasualties, Is.EqualTo(ExpantaNum.Zero));
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void SectorRepairSynchronizesGlobalAndSectorCasualties()
    {
        GameObject resourceObject = new GameObject("星区维修同步资源管理器");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            resourceManager.SetAmount(DataBase<Resource>.Find("TitaniumAlloy"), new ExpantaNum(20));
            resourceManager.SetAmount(DataBase<Resource>.Find("Composite"), new ExpantaNum(10));
            resourceManager.SetAmount(DataBase<Resource>.Find("PhantomWeave"), new ExpantaNum(10));
            resourceManager.SetAmount(DataBase<Resource>.Find("RocketFuel"), new ExpantaNum(5));

            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            SectorDefinition sector = DataBase<SectorDefinition>.Find("ProximaB");
            SectorState state = manager.GetState(sector);
            state.SetCampaignActiveForEditor(true);
            state.SetCampaignCasualtiesForEditor(new ExpantaNum(10));
            GameState runtimeState = new GameState();
            InvokeGameStateMethod(runtimeState, "BeginCampaign", sector.Id);
            InvokeGameStateMethod(runtimeState, "RecordCampaignCombat", new ExpantaNum(0.8d), new ExpantaNum(10));

            Assert.That(manager.TryRepairFleet(
                sector,
                runtimeState,
                resourceManager,
                ExpantaNum.NaN,
                out ExpantaNum invalidRepairAmount,
                out SectorOperationFailure invalidRepairFailure), Is.False);
            Assert.That(invalidRepairAmount, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(invalidRepairFailure, Is.EqualTo(SectorOperationFailure.InvalidRepairAmount));

            Assert.That(manager.TryRepairFleet(
                sector,
                runtimeState,
                resourceManager,
                new ExpantaNum(5),
                out ExpantaNum repairedAmount,
                out SectorOperationFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));
            Assert.That(repairedAmount, Is.EqualTo(new ExpantaNum(5)));
            Assert.That(state.CampaignCasualties, Is.EqualTo(new ExpantaNum(5)));
            Assert.That(runtimeState.Campaign.Casualties, Is.EqualTo(new ExpantaNum(5)));
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
        Assert.That(preview.ProgressPerSecond, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void InterstellarCampaignsIncreaseSupplyBurdenAndTerritoryRewards()
    {
        SectorDefinition alphaCentauri = DataBase<SectorDefinition>.Find("AlphaCentauri");
        SectorDefinition proximaB = DataBase<SectorDefinition>.Find("ProximaB");
        SectorDefinition tauCetiFoundry = DataBase<SectorDefinition>.Find("TauCetiFoundry");
        SectorDefinition siriusResourceBelt = DataBase<SectorDefinition>.Find("SiriusResourceBelt");

        SectorDefinition[] sectors =
        {
            alphaCentauri,
            proximaB,
            tauCetiFoundry,
            siriusResourceBelt
        };
        for (int i = 0; i < sectors.Length; i++)
        {
            Assert.That(sectors[i], Is.Not.Null);
            Assert.That(sectors[i].CampaignFoodPerSecond, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(sectors[i].CampaignProgressMultiplier, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(sectors[i].CampaignResourceRatesPerSecond.Count, Is.GreaterThanOrEqualTo(8));
            for (int j = 0; j < sectors[i].CampaignResourceRatesPerSecond.Count; j++)
                Assert.That(
                    sectors[i].CampaignResourceRatesPerSecond[j].Second,
                    Is.GreaterThan(ExpantaNum.Zero));
        }

        Assert.That(alphaCentauri.CampaignProgressMultiplier,
            Is.GreaterThan(proximaB.CampaignProgressMultiplier));
        Assert.That(proximaB.CampaignProgressMultiplier,
            Is.GreaterThan(tauCetiFoundry.CampaignProgressMultiplier));
        Assert.That(tauCetiFoundry.CampaignProgressMultiplier,
            Is.GreaterThan(siriusResourceBelt.CampaignProgressMultiplier));
        Assert.That(alphaCentauri.TerritoryReward,
            Is.LessThan(proximaB.TerritoryReward));
        Assert.That(proximaB.TerritoryReward,
            Is.LessThan(tauCetiFoundry.TerritoryReward));
        Assert.That(tauCetiFoundry.TerritoryReward,
            Is.LessThan(siriusResourceBelt.TerritoryReward));

        ExpantaNum previousFoodRate = ExpantaNum.Zero;
        ExpantaNum previousSupplyRate = ExpantaNum.Zero;
        for (int i = 0; i < sectors.Length; i++)
        {
            Assert.That(
                sectors[i].CampaignFoodPerSecond,
                Is.GreaterThan(previousFoodRate),
                $"星际战役 {sectors[i].Id} 的 Food 消耗必须随航线递进。");
            previousFoodRate = sectors[i].CampaignFoodPerSecond;

            ExpantaNum totalSupplyRate = sectors[i].CampaignFoodPerSecond;
            for (int j = 0; j < sectors[i].CampaignResourceRatesPerSecond.Count; j++)
                totalSupplyRate += sectors[i].CampaignResourceRatesPerSecond[j].Second;
            Assert.That(totalSupplyRate, Is.GreaterThan(previousSupplyRate));
            previousSupplyRate = totalSupplyRate;
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

    private static void InvokeProgressionStateMethod(ResearchSystem system)
    {
        var method = typeof(ProgressionModifierState).GetMethod(
            "AddUnlockedSystem",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(ProgressionModifierManager.Current, new object[] { system });
    }

    private static void InvokeSectorStateMethod(SectorState state, string methodName, params object[] arguments)
    {
        var method = typeof(SectorState).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(state, arguments);
    }
    [Test]
    public void 近地轨道到月球再到火星的殖民周期必须逐级延长()
    {
        SectorDefinition lowOrbit = Resources.Load<SectorDefinition>("Datas/Sector/LowOrbit");
        SectorDefinition moon = Resources.Load<SectorDefinition>("Datas/Sector/Moon");
        SectorDefinition mars = Resources.Load<SectorDefinition>("Datas/Sector/Mars");

        Assert.That(lowOrbit, Is.Not.Null);
        Assert.That(moon, Is.Not.Null);
        Assert.That(mars, Is.Not.Null);
        Assert.That(lowOrbit.ColonizationDurationSeconds, Is.EqualTo(new ExpantaNum(600d)));
        Assert.That(moon.ColonizationDurationSeconds, Is.EqualTo(new ExpantaNum(3600d)));
        Assert.That(mars.ColonizationDurationSeconds, Is.EqualTo(new ExpantaNum(7200d)));
        Assert.That(lowOrbit.ColonizationDurationSeconds, Is.LessThan(moon.ColonizationDurationSeconds));
        Assert.That(moon.ColonizationDurationSeconds, Is.LessThan(mars.ColonizationDurationSeconds));
    }
    private static bool HasPositiveRate(
        IReadOnlyList<Pair<Resource, ExpantaNum>> rates,
        Resource resource)
    {
        if (resource == null)
            return false;

        for (int i = 0; i < rates.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = rates[i];
            if (pair.First == resource && pair.Second > ExpantaNum.Zero)
                return true;
        }

        return false;
    }
}
