using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SectorManagerTests
{
    [Test]
    public void OccupiedSectorProductionContributesToSameTickSatisfaction()
    {
        GameObject gameObject = new GameObject("Sector-SameTick-GameManager");
        GameObject resourceObject = new GameObject("Sector-SameTick-ResourceManager");
        try
        {
            ProgressionModifierManager.Rebuild(null);
            gameObject.AddComponent<GameManager>();
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
            Pair<Resource, ExpantaNum> production = lowOrbit.OccupiedResourceRatesPerSecond
                .First(rate => rate.First != null && rate.Second > ExpantaNum.Zero);
            resourceManager.EnsureResource(production.First);
            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            manager.GetState(lowOrbit).SetOccupiedForEditor(true);

            resourceManager.BeginTickForEditor();
            manager.AccumulateOccupiedResourcePotentialForEditor(resourceManager);
            resourceManager.AdjustTickPotentialConsumptionForEditor(
                production.First,
                production.Second * 2d);
            resourceManager.CalculateTickSatisfactionForEditor(1d);
            ExpantaNum satisfaction = resourceManager.GetTickSatisfactionForEditor(
                production.First);

            Assert.That(satisfaction.ToDouble(), Is.EqualTo(0.5d).Within(1e-9d));
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
            Object.DestroyImmediate(resourceObject);
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void SectorCompletionBillingStopsAtTheCompletionBoundary()
    {
        double campaignSeconds = SectorManager.CalculateCampaignBillableSecondsForEditor(
            new ExpantaNum(0.9d),
            new ExpantaNum(2d),
            ExpantaNum.One,
            60d);
        double colonizationSeconds = SectorManager.CalculateColonizationBillableSecondsForEditor(
            new ExpantaNum(0.9d),
            new ExpantaNum(100d),
            60d);

        Assert.That(campaignSeconds, Is.EqualTo(6d).Within(1e-3d));
        Assert.That(colonizationSeconds, Is.EqualTo(10d).Within(1e-9d));
    }

    [Test]
    public void SurgeCampaignBillingUsesItsFasterCompletionRate()
    {
        double billableSeconds = SectorManager.CalculateCampaignBillableSecondsForEditor(
            new ExpantaNum(0.9d),
            new ExpantaNum(2d),
            new ExpantaNum(1.35d),
            60d);

        Assert.That(billableSeconds, Is.EqualTo(60d / 13.5d).Within(1e-6d));
    }

    [Test]
    public void CampaignCompletionDoesNotDecayAfterRealtimeOrOfflineBoundary()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorState state = manager.GetState(DataBase<SectorDefinition>.Find("ProximaB"));
        state.SetUnlockedForEditor(true);
        state.SetOccupiedForEditor(true);
        state.SetCampaignProgressForEditor(ExpantaNum.One);

        Assert.That(manager.TickActiveCampaign(
            60d,
            new GameState(),
            null,
            out SectorOperationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));
        Assert.That(state.CampaignProgress, Is.EqualTo(ExpantaNum.One));
    }

    [Test]
    public void SectorSaveRejectsActiveCampaignOrColonizationAtCompletionBoundary()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition sector = DataBase<SectorDefinition>.Find("ProximaB");

        Assert.Throws<InvalidOperationException>(() =>
            manager.RestoreSaveData(new SaveManager.SectorSaveData
            {
                States = new List<SaveManager.SectorStateSaveData>
                {
                    new SaveManager.SectorStateSaveData
                    {
                        SectorId = sector.Id,
                        Unlocked = true,
                        CampaignActive = true,
                        CampaignProgress = "1",
                        CampaignCasualties = "0",
                        CampaignCombatRatio = "0"
                    }
                }
            }));

        SectorDefinition home = DataBase<SectorDefinition>.Find("AzurePool");
        Assert.Throws<InvalidOperationException>(() =>
            manager.RestoreSaveData(new SaveManager.SectorSaveData
            {
                States = new List<SaveManager.SectorStateSaveData>
                {
                    new SaveManager.SectorStateSaveData
                    {
                        SectorId = home.Id,
                        Unlocked = true,
                        ColonizationActive = true,
                        CampaignProgress = "1",
                        CampaignCasualties = "0",
                        CampaignCombatRatio = "0"
                    }
                }
            }));
    }

    [Test]
    public void NonFiniteCampaignCostIsRejectedWithoutFreeProgress()
    {
        GameObject resourceObject = new GameObject("Campaign-NonFinite-Cost-ResourceManager");
        SectorDefinition sector = DataBase<SectorDefinition>.Find("ProximaB");
        List<Pair<Resource, ExpantaNum>> originalRates =
            new List<Pair<Resource, ExpantaNum>>(sector.CampaignResourceRatesPerSecond);
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            List<Pair<Resource, ExpantaNum>> invalidRates =
                new List<Pair<Resource, ExpantaNum>>(originalRates);
            invalidRates[0] = new Pair<Resource, ExpantaNum>(
                invalidRates[0].First,
                ExpantaNum.PositiveInfinity);
            sector.SetCampaignCostsForEditor(sector.CampaignFoodPerSecond, invalidRates);

            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            manager.GetState(sector).SetUnlockedForEditor(true);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.HomeSystemSurvey);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.InterstellarNavigation);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.DeepSpaceFleet);
            SetCampaignResources(resourceManager, sector, new ExpantaNum(1000000));
            GameState runtimeState = new GameState();
            SetCampaignFood(runtimeState, new ExpantaNum(1000000));
            ExpantaNum foodBefore = runtimeState.FoodAmount;

            Assert.That(manager.TryAdvanceCampaign(
                sector,
                60d,
                runtimeState,
                resourceManager,
                out SectorOperationFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.InvalidCampaignCost));
            Assert.That(runtimeState.FoodAmount, Is.EqualTo(foodBefore));
            Assert.That(runtimeState.Campaign.Active, Is.False);
            Assert.That(manager.GetState(sector).CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
        }
        finally
        {
            sector.SetCampaignCostsForEditor(sector.CampaignFoodPerSecond, originalRates);
            ProgressionModifierManager.Rebuild(null);
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void RepairWithoutDamageAndWithoutTargetDoesNotChargeOrChangeCampaign()
    {
        GameObject resourceObject = new GameObject("Campaign-NoTarget-Repair-ResourceManager");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            Resource titanium = DataBase<Resource>.Find("TitaniumAlloy");
            resourceManager.SetAmount(titanium, new ExpantaNum(100));
            GameState runtimeState = new GameState();
            ExpantaNum before = resourceManager.GetAmount(titanium);
            var manager = new SectorManager(_ => { });

            Assert.That(manager.TryRepairFleet(
                runtimeState,
                resourceManager,
                ExpantaNum.One,
                out ExpantaNum repaired,
                out SectorOperationFailure failure), Is.False);
            Assert.That(repaired, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.NoFleetDamage));
            Assert.That(resourceManager.GetAmount(titanium), Is.EqualTo(before));
            Assert.That(runtimeState.Campaign.TargetSectorId, Is.Empty);
            Assert.That(runtimeState.Campaign.Casualties, Is.EqualTo(ExpantaNum.Zero));
        }
        finally
        {
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void AtomicResourcePaymentRejectsDuplicateStableIdsInOneRequest()
    {
        GameObject resourceObject = new GameObject("Resource-DuplicateStableId-Test");
        Resource first = ScriptableObject.CreateInstance<Resource>();
        Resource second = ScriptableObject.CreateInstance<Resource>();
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            string stableId = "TestDuplicateResource-" + Guid.NewGuid().ToString("N");
            first.SetIdForEditor(stableId);
            second.SetIdForEditor(stableId);

            bool applied = resourceManager.TryApplyAtomicChanges(
                new Dictionary<Resource, ExpantaNum>
                {
                    [first] = ExpantaNum.One,
                    [second] = ExpantaNum.One
                });

            Assert.That(applied, Is.False);
            Assert.That(resourceManager.States.ContainsKey(first), Is.False);
            Assert.That(resourceManager.States.ContainsKey(second), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(resourceObject);
        }
    }

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
    public void InterstellarCampaignsHaveLongContinuousAdvancedSupplyLoops()
    {
        string[] interstellarIds =
        {
            "AlphaCentauri", "ProximaB", "TauCetiFoundry", "SiriusResourceBelt"
        };
        string[] advancedResourceIds =
        {
            "TitaniumAlloy", "Composite", "PhantomAlloy", "PhantomWeave", "PhaseMaterial"
        };
        ExpantaNum progressRateAtStrongAdvantage =
            CampaignManager.CalculateProgressRate(new ExpantaNum(2d));

        foreach (string id in interstellarIds)
        {
            SectorDefinition sector = DataBase<SectorDefinition>.Find(id);
            Assert.That(sector, Is.Not.Null, id);
            Assert.That(sector.CampaignFoodPerSecond, Is.GreaterThan(ExpantaNum.Zero), id);
            Assert.That(sector.CampaignProgressMultiplier, Is.GreaterThan(ExpantaNum.Zero), id);
            Assert.That(
                1d / (progressRateAtStrongAdvantage * sector.CampaignProgressMultiplier).ToDouble(),
                Is.GreaterThanOrEqualTo(3600d),
                $"{id} must remain a long-running campaign even at combat ratio 2.");
            Assert.That(sector.TerritoryReward, Is.GreaterThanOrEqualTo(new ExpantaNum(500000d)), id);

            Assert.That(
                sector.CampaignResourceRatesPerSecond.Any(rate =>
                    rate.First != null &&
                    advancedResourceIds.Contains(rate.First.Id) &&
                    rate.Second > ExpantaNum.Zero),
                Is.True,
                $"{id} must continuously consume an advanced material.");
        }
    }

    [Test]
    public void SolarSystemChainIncludesAsteroidBeltAndThunderGateBeforeAlpha()
    {
        SectorDefinition mars = Resources.Load<SectorDefinition>("Datas/Sector/Terminus");
        SectorDefinition asteroid = Resources.Load<SectorDefinition>("Datas/Sector/ShardCrown");
        SectorDefinition jovian = Resources.Load<SectorDefinition>("Datas/Sector/ThunderGate");
        SectorDefinition alpha = Resources.Load<SectorDefinition>("Datas/Sector/AlphaCentauri");

        Assert.That(mars, Is.Not.Null);
        Assert.That(asteroid, Is.Not.Null);
        Assert.That(jovian, Is.Not.Null);
        Assert.That(alpha, Is.Not.Null);
        Assert.That(asteroid.PrerequisiteSectors, Does.Contain(mars));
        Assert.That(jovian.PrerequisiteSectors, Does.Contain(asteroid));
        Assert.That(alpha.PrerequisiteSectors, Does.Contain(asteroid));
        Assert.That(asteroid.Domain, Is.EqualTo(SectorDefinition.SectorDomain.HomeSystem));
        Assert.That(jovian.Domain, Is.EqualTo(SectorDefinition.SectorDomain.HomeSystem));
        Assert.That(asteroid.EnemyPower, Is.LessThanOrEqualTo(ExpantaNum.Zero));
        Assert.That(jovian.EnemyPower, Is.LessThanOrEqualTo(ExpantaNum.Zero));
        Assert.That(asteroid.TerritoryReward, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(jovian.TerritoryReward, Is.GreaterThan(asteroid.TerritoryReward));
        Assert.That(asteroid.ResourceRewards, Is.Empty);
        Assert.That(jovian.ResourceRewards, Is.Empty);
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
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
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
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
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
            resourceManager.EnsureResource(DataBase<Resource>.Find("CopperWire"));
            resourceManager.EnsureResource(DataBase<Resource>.Find("Electronics"));
            SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            manager.GetState(lowOrbit).SetOccupiedForEditor(true);

            Assert.That(manager.TickOccupiedResourceProduction(10d, resourceManager), Is.True);
            Assert.That(
                resourceManager.GetAmount(DataBase<Resource>.Find("CopperWire")),
                Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
            Assert.That(
                resourceManager.GetAmount(DataBase<Resource>.Find("Electronics")),
                Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
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
            resourceManager.EnsureResource(DataBase<Resource>.Find("CopperWire"));
            resourceManager.EnsureResource(DataBase<Resource>.Find("Electronics"));
            SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
            WorkshopUpgrade definition =
                DataBase<WorkshopUpgrade>.Find("AutonomousOrbitalMiningSystems");
            WorkshopUpgradeState state = new WorkshopUpgradeState(definition);
            state.SetPurchasedForEditor(true);
            ProgressionModifierManager.Rebuild(null, new[] { state });

            SectorManager manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            manager.GetState(lowOrbit).SetOccupiedForEditor(true);

            Assert.That(manager.TickOccupiedResourceProduction(10d, resourceManager), Is.True);
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("CopperWire")),
                Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("Electronics")),
                Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
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
            researchState.SetStatusForEditor(ResearchStatus.Completed);

            WorkshopUpgrade workshopDefinition =
                DataBase<WorkshopUpgrade>.Find("AutonomousOrbitalMiningSystems");
            WorkshopUpgradeState workshopState = new WorkshopUpgradeState(workshopDefinition);
            workshopState.SetPurchasedForEditor(true);
            ProgressionModifierManager.Rebuild(
                new[] { researchState },
                new[] { workshopState });

            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            resourceManager.EnsureResource(DataBase<Resource>.Find("CopperWire"));
            resourceManager.EnsureResource(DataBase<Resource>.Find("Electronics"));
            SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
            SectorManager manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            manager.GetState(lowOrbit).SetOccupiedForEditor(true);

            Assert.That(manager.TickOccupiedResourceProduction(10d, resourceManager), Is.True);
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("CopperWire")),
                Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("Electronics")),
                Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
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
            if (sector.IsHomeSystem)
                Assert.That(sector.ResourceRewards, Is.Empty, sector.Id);
            else
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
    public void HomeExplorationPreviewShowsResourceSupplyBeforeStarting()
    {
        GameObject resourceObject = new GameObject("Sector-Exploration-Preview-ResourceManager");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
            for (int i = 0; i < lowOrbit.ColonizationResourceRatesPerSecond.Count; i++)
                resourceManager.SetAmount(
                    lowOrbit.ColonizationResourceRatesPerSecond[i].First,
                    lowOrbit.ColonizationResourceRatesPerSecond[i].Second);

            GameState runtimeState = new GameState();
            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();

            SectorExplorationPreview preview = manager.GetExplorationPreview(
                lowOrbit,
                runtimeState,
                resourceManager);

            Assert.That(preview.IsValid, Is.True);
            Assert.That(preview.HasSupply, Is.True);
            Assert.That(preview.ExplorationPower, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(preview.RequiredPower, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(preview.EstimatedSecondsRemaining.ToDouble(), Is.GreaterThan(599.8d));
            Assert.That(preview.EstimatedSecondsRemaining.ToDouble(), Is.LessThan(600.2d));
            Assert.That(preview.ProgressPerSecond, Is.GreaterThan(ExpantaNum.Zero));
            double normalizedProgress =
                (preview.ProgressPerSecond * lowOrbit.ColonizationDurationSeconds).ToDouble();
            Assert.That(normalizedProgress, Is.GreaterThan(.999d));
            Assert.That(normalizedProgress, Is.LessThan(1.001d));
        }
        finally
        {
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void HomeExplorationTechnologyImprovesProgressRate()
    {
        ProgressionModifierManager.Rebuild(null);
        try
        {
            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
            GameState runtimeState = new GameState();
            SectorExplorationPreview baseline = manager.GetExplorationPreview(
                lowOrbit, runtimeState, null);

            ProgressionModifierManager.Current.AddExplorationPowerMultiplierForEditor(
                new ExpantaNum(1.2d));

            SectorExplorationPreview improved = manager.GetExplorationPreview(
                lowOrbit, runtimeState, null);
            Assert.That(improved.ProgressPerSecond, Is.GreaterThan(baseline.ProgressPerSecond));
            Assert.That(improved.EstimatedSecondsRemaining,
                Is.LessThan(baseline.EstimatedSecondsRemaining));
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
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
        Assert.That(FindOccupiedResourceRate(sirius, "PhaseMaterial"), Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(FindOccupiedResourceRate(sirius, "PhantomAlloy"), Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(FindOccupiedResourceRate(sirius, "PhantomWeave"), Is.GreaterThan(ExpantaNum.Zero));
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
        Assert.That(GetOverwhelmingPowerCaptureMinutes("TauCetiFoundry"), Is.GreaterThan(0d));
        Assert.That(GetOverwhelmingPowerCaptureMinutes("SiriusResourceBelt"), Is.GreaterThan(120d));
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

            Assert.That(FindCampaignCost(alpha, "Nickel"), Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(FindCampaignCost(proxima, "Nickel"), Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(FindCampaignCost(tau, "Nickel"), Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(FindCampaignCost(sirius, "Nickel"), Is.GreaterThan(ExpantaNum.Zero));
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
        Assert.That(FindCampaignCost(proxima, "PhaseMaterial"), Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(FindCampaignCost(tau, "PhaseMaterial"), Is.GreaterThan(FindCampaignCost(proxima, "PhaseMaterial")));
        Assert.That(FindCampaignCost(sirius, "PhaseMaterial"), Is.GreaterThan(FindCampaignCost(tau, "PhaseMaterial")));
        Assert.That(FindCampaignCost(alpha, "Biomass"), Is.GreaterThanOrEqualTo(new ExpantaNum(0.8d)));
        Assert.That(FindCampaignCost(proxima, "Biomass"), Is.GreaterThanOrEqualTo(new ExpantaNum(1.2d)));
        Assert.That(FindCampaignCost(tau, "Biomass"), Is.GreaterThanOrEqualTo(new ExpantaNum(1.8d)));
        Assert.That(FindCampaignCost(sirius, "Biomass"), Is.GreaterThanOrEqualTo(new ExpantaNum(2.6d)));
        Assert.That(FindCampaignCost(alpha, "Machinery"), Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(FindCampaignCost(proxima, "Machinery"), Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(FindCampaignCost(tau, "Machinery"), Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(FindCampaignCost(sirius, "Machinery"), Is.GreaterThan(ExpantaNum.Zero));
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
        SectorDefinition lowOrbit = Resources.Load<SectorDefinition>("Datas/Sector/DawnRing");
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

        Assert.That(FindCampaignCost(alpha, "Lubricant"), Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(FindCampaignCost(proxima, "Lubricant"), Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(FindCampaignCost(tau, "Lubricant"), Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(FindCampaignCost(sirius, "Lubricant"), Is.GreaterThan(ExpantaNum.Zero));
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

    private static void SetCampaignResources(
        ResourceManager resourceManager,
        SectorDefinition sector,
        ExpantaNum amount)
    {
        for (int i = 0; i < sector.CampaignResourceRatesPerSecond.Count; i++)
        {
            Pair<Resource, ExpantaNum> rate = sector.CampaignResourceRatesPerSecond[i];
            resourceManager.SetAmount(rate.First, amount);
        }
    }

    private static void SetCampaignFood(GameState runtimeState, ExpantaNum amount)
    {
        runtimeState.RestoreCoreForEditor(
            runtimeState.CalendarDays,
            runtimeState.TechLevel,
            amount,
            runtimeState.LastSaveUnixSeconds);
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
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
        SectorDefinition moon = DataBase<SectorDefinition>.Find("AzurePool");
        SectorDefinition mars = DataBase<SectorDefinition>.Find("Terminus");
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
        Assert.That(manager.OrderedStates.Any(state => state.Definition.Id == "DawnRing"), Is.True);
        Assert.That(manager.OrderedStates.Any(state => state.Definition.Id == "Terminus"), Is.True);
        Assert.That(manager.OrderedStates.Any(state => state.Definition.Id == "AzurePool"), Is.True);
        Assert.That(manager.CanAccess(DataBase<SectorDefinition>.Find("DawnRing")), Is.True);
        Assert.That(manager.CanAccess(DataBase<SectorDefinition>.Find("AzurePool")), Is.False);
    }

    [Test]
    public void HomeSectorUnlockRequiresHomeSystemSurveyResearch()
    {
        ProgressionModifierManager.Rebuild(null);
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");

        Assert.That(manager.GetUnlockFailure(lowOrbit),
            Is.EqualTo(SectorOperationFailure.HomeSystemSurveyRequired));
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
    public void HomeExplorationDoesNotRequireCombatPower()
    {
        GameObject resourceObject = new GameObject("Sector-Home-Exploration-ResourceManager");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
            SectorState state = manager.GetState(lowOrbit);
            state.SetUnlockedForEditor(true);
            GameState runtimeState = new GameState();

            bool advanced = manager.TryAdvanceColonization(
                lowOrbit,
                0d,
                runtimeState,
                resourceManager,
                out SectorOperationFailure failure);

            Assert.That(advanced, Is.True);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));
        }
        finally
        {
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void 本星系探索不使用星区战役()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
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
            SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
        for (int i = 0; i < lowOrbit.ColonizationResourceRatesPerSecond.Count; i++)
                resourceManager.SetAmount(
                    lowOrbit.ColonizationResourceRatesPerSecond[i].First,
                    lowOrbit.ColonizationResourceRatesPerSecond[i].Second);

            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            manager.GetState(lowOrbit).SetUnlockedForEditor(true);
            GameState runtimeState = new GameState();
            runtimeState.AdjustAttackPowerForEditor(new ExpantaNum(100));
            runtimeState.AdjustDefensePowerForEditor(new ExpantaNum(100));

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
        SectorState home = manager.GetState(DataBase<SectorDefinition>.Find("DawnRing"));
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
        SectorDefinition moon = DataBase<SectorDefinition>.Find("AzurePool");
        SectorState state = manager.GetState(moon);
        state.SetUnlockedForEditor(true);
        state.SetColonizationActiveForEditor(true);
        var runtimeState = new GameState();
        runtimeState.AdjustAttackPowerForEditor(new ExpantaNum(100000));
        runtimeState.AdjustDefensePowerForEditor(new ExpantaNum(100000));

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
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.HomeSystemSurvey);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.InterstellarNavigation);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.DeepSpaceFleet);
            runtimeState.AdjustAttackPowerForEditor(new ExpantaNum(100000));
            runtimeState.AdjustDefensePowerForEditor(new ExpantaNum(100000));
            runtimeState.AdjustMilitaryManpowerForEditor(new ExpantaNum(100000));
            runtimeState.AdjustFleetPowerForEditor(new ExpantaNum(100000));
            runtimeState.SetSupplySatisfactionForEditor(ExpantaNum.One);
            runtimeState.SetPowerSatisfactionForEditor(ExpantaNum.One);
            runtimeState.SetLogisticsSatisfactionForEditor(ExpantaNum.One);
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
            ExpantaNum initialAmount = new ExpantaNum(1000000d);
            for (int i = 0; i < sector.CampaignResourceRatesPerSecond.Count; i++)
            {
                Pair<Resource, ExpantaNum> rate = sector.CampaignResourceRatesPerSecond[i];
                resourceManager.SetAmount(rate.First, initialAmount);
            }

            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            manager.GetState(sector).SetUnlockedForEditor(true);
            GameState runtimeState = new GameState();
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.HomeSystemSurvey);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.InterstellarNavigation);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.DeepSpaceFleet);
            runtimeState.AdjustAttackPowerForEditor(new ExpantaNum(100000));
            runtimeState.AdjustDefensePowerForEditor(new ExpantaNum(100000));
            runtimeState.AdjustMilitaryManpowerForEditor(new ExpantaNum(100000));
            runtimeState.SetSupplySatisfactionForEditor(ExpantaNum.One);
            runtimeState.SetPowerSatisfactionForEditor(ExpantaNum.One);
            runtimeState.SetLogisticsSatisfactionForEditor(ExpantaNum.One);
            SetCampaignFood(runtimeState, new ExpantaNum(1000000));
            ExpantaNum foodBeforeCampaign = runtimeState.FoodAmount;

            Assert.That(
                manager.TryAdvanceCampaign(
                    sector,
                    deltaSeconds,
                    runtimeState,
                    resourceManager,
                    out SectorOperationFailure failure),
                Is.True);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));
            Assert.That(runtimeState.FoodAmount, Is.LessThan(foodBeforeCampaign));

            for (int i = 0; i < sector.CampaignResourceRatesPerSecond.Count; i++)
            {
                Pair<Resource, ExpantaNum> rate = sector.CampaignResourceRatesPerSecond[i];
                Assert.That(
                    resourceManager.GetAmount(rate.First),
                    Is.LessThan(initialAmount),
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
        runtimeState.BeginCampaignForEditor(sector.Id);

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
        runtimeState.BeginCampaignForEditor(activeSector.Id);

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
            SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
            SectorDefinition moon = DataBase<SectorDefinition>.Find("AzurePool");
            foreach (SectorDefinition sector in new[] { lowOrbit, moon })
                for (int i = 0; i < sector.ColonizationResourceRatesPerSecond.Count; i++)
                    resourceManager.SetAmount(
                        sector.ColonizationResourceRatesPerSecond[i].First,
                        new ExpantaNum(100000));
            manager.GetState(lowOrbit).SetUnlockedForEditor(true);
            manager.GetState(moon).SetUnlockedForEditor(true);
            GameState runtimeState = new GameState();
            SetCampaignFood(runtimeState, new ExpantaNum(100000));
            runtimeState.AdjustAttackPowerForEditor(new ExpantaNum(1000));
            runtimeState.AdjustDefensePowerForEditor(new ExpantaNum(1000));

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
    public void 本地殖民完成不改变并行的全局星际战役()
    {
        GameObject resourceObject = new GameObject("本地殖民与全局战役隔离资源管理器");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
            SectorState lowOrbitState = manager.GetState(lowOrbit);
            lowOrbitState.SetUnlockedForEditor(true);
            lowOrbitState.SetCampaignProgressForEditor(new ExpantaNum("0.99999"));
            for (int i = 0; i < lowOrbit.ColonizationResourceRatesPerSecond.Count; i++)
                resourceManager.SetAmount(
                    lowOrbit.ColonizationResourceRatesPerSecond[i].First,
                    new ExpantaNum(100000));

            GameState runtimeState = new GameState();
            runtimeState.AdjustAttackPowerForEditor(new ExpantaNum(1000));
            runtimeState.AdjustDefensePowerForEditor(new ExpantaNum(1000));
            runtimeState.BeginCampaignForEditor("ProximaB");
            runtimeState.RecordCampaignCombatForEditor(
                new ExpantaNum(0.8d),
                new ExpantaNum(7));

            Assert.That(manager.TryAdvanceColonization(
                lowOrbit,
                60d,
                runtimeState,
                resourceManager,
                out SectorOperationFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));
            Assert.That(lowOrbitState.Occupied, Is.True);
            Assert.That(runtimeState.Campaign.Active, Is.True);
            Assert.That(runtimeState.Campaign.TargetSectorId, Is.EqualTo("ProximaB"));
            Assert.That(runtimeState.Campaign.Casualties, Is.EqualTo(new ExpantaNum(7)));
            Assert.That(runtimeState.Campaign.CombatRatio, Is.EqualTo(new ExpantaNum(0.8d)));
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
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
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
        SectorState state = manager.GetState(DataBase<SectorDefinition>.Find("DawnRing"));
        state.SetUnlockedForEditor(true);
        state.SetCampaignProgressForEditor(ExpantaNum.One);

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
        SectorState state = manager.GetState(DataBase<SectorDefinition>.Find("DawnRing"));
        state.SetUnlockedForEditor(true);
        state.SetCampaignProgressForEditor(ExpantaNum.One);
        state.SetVisitCountForEditor(2);

        Assert.Throws<System.InvalidOperationException>(() =>
            manager.TryOccupy(state.Definition, out _));

        Assert.That(state.Occupied, Is.False);
        Assert.That(state.VisitCount, Is.EqualTo(2));
    }

    [Test]
    public void C705_HomeSystemExplorationGrantsOnlyTerritory()
    {
        foreach (SectorDefinition sector in DataBase<SectorDefinition>.All)
        {
            if (sector != null && sector.IsHomeSystem)
                Assert.That(sector.ResourceRewards, Is.Empty, sector.Id);
        }
    }

    [Test]
    public void 近地殖民总成本必须随阶段和回报逐级增长()
    {
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
        SectorDefinition moon = DataBase<SectorDefinition>.Find("AzurePool");
        SectorDefinition mars = DataBase<SectorDefinition>.Find("Terminus");

        ExpantaNum lowOrbitFood = lowOrbit.ColonizationFoodPerSecond * lowOrbit.ColonizationDurationSeconds;
        ExpantaNum moonFood = moon.ColonizationFoodPerSecond * moon.ColonizationDurationSeconds;
        ExpantaNum marsFood = mars.ColonizationFoodPerSecond * mars.ColonizationDurationSeconds;
        ExpantaNum moonRocketFuel = GetColonizationResourceTotal(moon, "RocketFuel");
        ExpantaNum marsRocketFuel = GetColonizationResourceTotal(mars, "RocketFuel");

        Assert.That(lowOrbitFood, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(moonFood, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(marsFood, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(lowOrbitFood, Is.LessThan(moonFood));
        Assert.That(moonFood, Is.LessThan(marsFood));
        Assert.That(moonRocketFuel, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(marsRocketFuel, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(moonRocketFuel, Is.LessThan(marsRocketFuel));
        Assert.That(moon.TerritoryReward, Is.GreaterThan(lowOrbit.TerritoryReward));
        Assert.That(mars.TerritoryReward, Is.GreaterThan(moon.TerritoryReward));
    }

    [Test]
    public void 近地星区占领产出必须按阶段承担不同战略职责()
    {
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
        SectorDefinition moon = DataBase<SectorDefinition>.Find("AzurePool");
        SectorDefinition mars = DataBase<SectorDefinition>.Find("Terminus");

        Assert.That(FindOccupiedResourceRate(lowOrbit, "Electronics"),
            Is.EqualTo(new ExpantaNum(0.04d)).Within(0.000001d));
        Assert.That(FindOccupiedResourceRate(moon, "TitaniumAlloy"),
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(FindOccupiedResourceRate(moon, "Composite"), Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(FindOccupiedResourceRate(mars, "Nickel"), Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(FindOccupiedResourceRate(mars, "RocketFuel"), Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(FindOccupiedResourceRate(mars, "Electronics"),
            Is.GreaterThanOrEqualTo(FindOccupiedResourceRate(moon, "Electronics")));
    }

    [Test]
    public void C802_EnemySectorCannotBeOccupiedBeforeCampaignCompletion()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("AzurePool");
        SectorState state = manager.GetState(moon);
        state.SetUnlockedForEditor(true);

        Assert.That(manager.TryOccupy(moon, out SectorOperationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(SectorOperationFailure.CampaignRequired));
    }

    [Test]
    public void C802_CampaignStateTracksTargetAndCasualties()
    {
        var campaign = new CampaignState();
        campaign.BeginForEditor("AzurePool");
        campaign.RecordCombatForEditor(new ExpantaNum(0.8d), new ExpantaNum(2));

        Assert.That(campaign.Active, Is.True);
        Assert.That(campaign.TargetSectorId, Is.EqualTo("AzurePool"));
        Assert.That(campaign.Casualties, Is.EqualTo(new ExpantaNum(2)));
        Assert.That(campaign.CombatRatio, Is.EqualTo(new ExpantaNum(0.8d)));
    }

    [Test]
    public void C803_CampaignCostMathRejectsInsufficientFoodAtomically()
    {
        GameObject resourceObject = new GameObject("C803-Campaign-ResourceManager");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            SectorDefinition sector = DataBase<SectorDefinition>.Find("ProximaB");
            SetCampaignResources(resourceManager, sector, new ExpantaNum(1000000));
            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.HomeSystemSurvey);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.InterstellarNavigation);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.DeepSpaceFleet);
            SectorState state = manager.GetState(sector);
            state.SetUnlockedForEditor(true);
            var runtimeState = new GameState();
            ExpantaNum foodBeforeFailure = runtimeState.FoodAmount;

            bool advanced = manager.TryAdvanceCampaign(
                sector, 120d, runtimeState, resourceManager, out SectorOperationFailure failure);

            Assert.That(advanced, Is.False);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.InsufficientCampaignSupply));
            Assert.That(runtimeState.FoodAmount, Is.Not.LessThan(foodBeforeFailure));
            Assert.That(runtimeState.FoodAmount, Is.Not.GreaterThan(foodBeforeFailure));
            Assert.That(state.CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void C804_战役补给不足时Tick不会报告推进成功()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("ProximaB");
        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.HomeSystemSurvey);
        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.InterstellarNavigation);
        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.DeepSpaceFleet);
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
        ProgressionModifierManager.Rebuild(null);
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("ProximaB");
        SectorState state = manager.GetState(moon);
        state.SetUnlockedForEditor(true);
        var runtimeState = new GameState();
        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.HomeSystemSurvey);
        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.InterstellarNavigation);
        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.DeepSpaceFleet);
        runtimeState.AdjustAttackPowerForEditor(new ExpantaNum(3000));
        runtimeState.AdjustFleetPowerForEditor(new ExpantaNum(50));
        runtimeState.AdjustMilitaryManpowerForEditor(new ExpantaNum(3050));
        runtimeState.SetSupplySatisfactionForEditor(ExpantaNum.One);
        SetCampaignFood(runtimeState, new ExpantaNum(1000000));

        GameObject resourceObject = new GameObject("C805-Campaign-ResourceManager");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            SetCampaignResources(resourceManager, moon, new ExpantaNum(100000));
            SectorCampaignPreview preview = manager.GetCampaignPreview(moon, runtimeState, resourceManager);

            Assert.That(preview.IsValid, Is.True);
            Assert.That(preview.CurrentProgress, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(preview.CombatRatio, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(preview.FleetSurvivalFactor, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(preview.ProgressPerSecond, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(preview.EstimatedSecondsRemaining, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(preview.CasualtiesPerSecond, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(preview.FoodCostPerSecond, Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
            Assert.That(preview.ResourceCostsPerSecond, Is.Not.Null);
            Assert.That(preview.HasOngoingSupplyCost, Is.True);
            Assert.That(preview.EstimatedSupplySeconds, Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
            Assert.That(preview.HasSupply, Is.True);
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void C805_CampaignPreviewMatchesActualSixtySecondSupplyConsumption()
    {
        ProgressionModifierManager.Rebuild(null);
        GameObject gameObject = new GameObject("C805-60-Second-Campaign-GameManager");
        GameObject resourceObject = new GameObject("C805-60-Second-Campaign-ResourceManager");
        try
        {
            GameManager gameManager = gameObject.AddComponent<GameManager>();
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            gameManager.AdjustAttackPower(new ExpantaNum(3000));
            gameManager.AdjustFleetPower(new ExpantaNum(50));
            gameManager.AdjustMilitaryManpower(new ExpantaNum(3050));
            gameManager.SetSupplySatisfaction(ExpantaNum.One);
            SetCampaignFood(gameManager.State, new ExpantaNum(1000000));
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.HomeSystemSurvey);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.InterstellarNavigation);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.DeepSpaceFleet);

            SectorManager manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            SectorDefinition sector = DataBase<SectorDefinition>.Find("ProximaB");
            SectorState state = manager.GetState(sector);
            state.SetUnlockedForEditor(true);
            state.SetCampaignActiveForEditor(true);
            gameManager.State.BeginCampaignForEditor(sector.Id);
            SetCampaignResources(resourceManager, sector, new ExpantaNum(1000000));

            SectorCampaignPreview preview = manager.GetCampaignPreview(
                sector,
                gameManager.State,
                resourceManager);
            ExpantaNum foodBefore = gameManager.State.FoodAmount;
            var expectedResourceRates = new Dictionary<Resource, ExpantaNum>();
            for (int i = 0; i < preview.ResourceCostsPerSecond.Count; i++)
            {
                Pair<Resource, ExpantaNum> rate = preview.ResourceCostsPerSecond[i];
                expectedResourceRates[rate.First] = expectedResourceRates.TryGetValue(
                    rate.First,
                    out ExpantaNum existing)
                    ? existing + rate.Second
                    : rate.Second;
            }
            var resourceBefore = new Dictionary<Resource, ExpantaNum>();
            foreach (Resource resource in expectedResourceRates.Keys)
                resourceBefore[resource] = resourceManager.GetAmount(resource);

            Assert.That(preview.IsValid, Is.True);
            Assert.That(manager.TickActiveCampaign(
                60d,
                gameManager.State,
                resourceManager,
                out SectorOperationFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));

            ExpantaNum actualFoodCost = foodBefore - gameManager.State.FoodAmount;
            ExpantaNum expectedFoodCost = preview.FoodCostPerSecond * 60d;
            Assert.That(actualFoodCost.ToDouble(), Is.EqualTo(expectedFoodCost.ToDouble()).Within(0.000001d));
            foreach (KeyValuePair<Resource, ExpantaNum> expected in expectedResourceRates)
            {
                ExpantaNum actualCost = resourceBefore[expected.Key] - resourceManager.GetAmount(expected.Key);
                ExpantaNum expectedCost = expected.Value * 60d;
                Assert.That(actualCost.ToDouble(), Is.EqualTo(expectedCost.ToDouble()).Within(0.000001d), expected.Key.Id);
            }
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
            Object.DestroyImmediate(resourceObject);
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void 自动化舰队补给模块会直接降低战役预览中的持续消耗()
    {
        WorkshopUpgrade definition = Resources.Load<WorkshopUpgrade>(
            "Datas/Workshop/AutomatedFleetResupplyModules");
        WorkshopUpgradeState workshopState = new WorkshopUpgradeState(definition);
        workshopState.SetPurchasedForEditor(true);

        try
        {
            ProgressionModifierManager.Rebuild(null, new[] { workshopState });
            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            SectorDefinition moon = DataBase<SectorDefinition>.Find("ProximaB");
            manager.GetState(moon).SetUnlockedForEditor(true);
            SectorCampaignPreview preview = manager.GetCampaignPreview(
                moon,
                new GameState(),
                null);

            Assert.That(preview.FoodCostPerSecond, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(preview.ResourceCostsPerSecond[0].Second, Is.GreaterThan(ExpantaNum.Zero));
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
        }
    }

    [Test]
    public void C806_InsufficientStrategicResourceLeavesCampaignStateAndFoodUntouched()
    {
        GameObject resourceObject = new GameObject("C806-Insufficient-ResourceManager");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            SectorDefinition moon = DataBase<SectorDefinition>.Find("ProximaB");
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.HomeSystemSurvey);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.InterstellarNavigation);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.DeepSpaceFleet);
            SetCampaignResources(resourceManager, moon, new ExpantaNum(100000));
            resourceManager.SetAmount(moon.CampaignResourceRatesPerSecond[0].First, ExpantaNum.Zero);
            manager.GetState(moon).SetUnlockedForEditor(true);
            var runtimeState = new GameState();
            runtimeState.SetSupplySatisfactionForEditor(ExpantaNum.One);
            ExpantaNum foodBeforeFailure = runtimeState.FoodAmount;

            bool advanced = manager.TryAdvanceCampaign(
                moon,
                60d,
                runtimeState,
                resourceManager,
                out SectorOperationFailure failure);

            Assert.That(advanced, Is.False);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.InsufficientCampaignSupply));
            Assert.That(runtimeState.FoodAmount, Is.Not.LessThan(foodBeforeFailure));
            Assert.That(runtimeState.FoodAmount, Is.Not.GreaterThan(foodBeforeFailure));
            Assert.That(runtimeState.Campaign.Active, Is.False);
            Assert.That(manager.GetState(moon).CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
            Object.DestroyImmediate(resourceObject);
        }
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
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.HomeSystemSurvey);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.InterstellarNavigation);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.DeepSpaceFleet);
            SectorDefinition moon = DataBase<SectorDefinition>.Find("ProximaB");
            SetCampaignResources(resourceManager, moon, new ExpantaNum(1000000));
            resourceManager.SetAmount(rocketFuel, new ExpantaNum(1000000));
            manager.GetState(moon).SetUnlockedForEditor(true);
            var runtimeState = new GameState();
            runtimeState.AdjustDefensePowerForEditor(new ExpantaNum(80));
            runtimeState.SetSupplySatisfactionForEditor(ExpantaNum.One);
            SetCampaignFood(runtimeState, new ExpantaNum(100000));
            ExpantaNum foodBeforeCampaign = runtimeState.FoodAmount;
            ExpantaNum rocketFuelBeforeCampaign = resourceManager.GetAmount(rocketFuel);

            bool advanced = manager.TryAdvanceCampaign(
                moon,
                1d,
                runtimeState,
                resourceManager,
                out SectorOperationFailure failure);

            Assert.That(advanced, Is.True);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));
            Assert.That(runtimeState.FoodAmount, Is.LessThan(foodBeforeCampaign));
            Assert.That(resourceManager.GetAmount(rocketFuel), Is.LessThan(rocketFuelBeforeCampaign));
            Assert.That(runtimeState.Campaign.Active, Is.True);
            Assert.That(runtimeState.Campaign.Casualties, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(manager.GetState(moon).CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(rewards, Is.EqualTo(0));
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
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
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.HomeSystemSurvey);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.InterstellarNavigation);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.DeepSpaceFleet);
            SectorDefinition moon = DataBase<SectorDefinition>.Find("ProximaB");
            SetCampaignResources(resourceManager, moon, new ExpantaNum(1000000));
            resourceManager.SetAmount(rocketFuel, new ExpantaNum(1000000));
            SectorState state = manager.GetState(moon);
            state.SetUnlockedForEditor(true);
            var runtimeState = new GameState();
            runtimeState.AdjustAttackPowerForEditor(new ExpantaNum(3000));
            runtimeState.AdjustMilitaryManpowerForEditor(new ExpantaNum(3000));
            runtimeState.SetSupplySatisfactionForEditor(ExpantaNum.One);
            SetCampaignFood(runtimeState, new ExpantaNum(1000000));
            ExpantaNum foodBeforeFailure = runtimeState.FoodAmount;

            Assert.That(manager.TryAdvanceCampaign(moon, 1d, runtimeState, resourceManager, out _), Is.True);
            Assert.That(rewards, Is.EqualTo(0));
            Assert.That(state.CampaignProgress, Is.GreaterThan(ExpantaNum.Zero));
            state.SetCampaignProgressForEditor(new ExpantaNum("0.99999"));

            Assert.That(manager.TryAdvanceCampaign(moon, 60d, runtimeState, resourceManager, out _), Is.True);
            Assert.That(rewards, Is.EqualTo(1));
            Assert.That(manager.GetState(moon).Occupied, Is.True);
            Assert.That(runtimeState.Campaign.Active, Is.False);
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
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
            gameManager.State.SetPowerSatisfactionForEditor(new ExpantaNum(0.5d));
            gameManager.State.SetLogisticsSatisfactionForEditor(new ExpantaNum(0.25d));

            SaveManager.GameSaveData saved = gameManager.CaptureSaveData();
            gameManager.ResetDerivedEconomyForEditor();
            gameManager.RestoreMilitarySaveDataForEditor(saved);

            Assert.That(saved.AttackPower, Is.EqualTo("11"));
            Assert.That(saved.DefensePower, Is.EqualTo("13"));
            Assert.That(saved.FleetPower, Is.EqualTo("17"));
            Assert.That(saved.MilitaryManpower, Is.EqualTo("19"));
            Assert.That(gameManager.State.AttackPower, Is.EqualTo(new ExpantaNum(11)));
            Assert.That(gameManager.State.DefensePower, Is.EqualTo(new ExpantaNum(13)));
            Assert.That(gameManager.State.FleetPower, Is.EqualTo(new ExpantaNum(17)));
            Assert.That(gameManager.State.MilitaryManpower, Is.EqualTo(new ExpantaNum(19)));
            Assert.That(gameManager.State.SupplySatisfaction.ToDouble(), Is.EqualTo(0.75d).Within(1e-9d));
            Assert.That(gameManager.State.PowerSatisfaction.ToDouble(), Is.EqualTo(0.5d).Within(1e-9d));
            Assert.That(gameManager.State.LogisticsSatisfaction.ToDouble(), Is.EqualTo(0.25d).Within(1e-9d));
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
            gameManager.State.BeginCampaignForEditor("AzurePool");
            gameManager.State.RecordCampaignCombatForEditor(
                new ExpantaNum(0.8d),
                new ExpantaNum(2));

            gameManager.RestoreSaveDataForEditor(
                new SaveManager.GameSaveData
                {
                    FoodAmount = "300",
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
        SectorDefinition moon = DataBase<SectorDefinition>.Find("AzurePool");
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
        SectorDefinition moon = DataBase<SectorDefinition>.Find("AzurePool");
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
        SectorDefinition moon = DataBase<SectorDefinition>.Find("AzurePool");
        SectorDefinition lowOrbit = DataBase<SectorDefinition>.Find("DawnRing");
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
        SectorDefinition moon = DataBase<SectorDefinition>.Find("AzurePool");
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
        SectorDefinition moon = DataBase<SectorDefinition>.Find("AzurePool");

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

            buildingManager.SetAmountAndRatesForEditor(state, new ExpantaNum(1));
            Assert.That(gameManager.State.AttackPower, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(gameManager.State.DefensePower, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(gameManager.State.MilitaryManpower, Is.EqualTo(ExpantaNum.Zero));

            buildingManager.SetAmountAndRatesForEditor(state, ExpantaNum.Zero);
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
            ExpantaNum titaniumBeforeRepair = resourceManager.GetAmount(DataBase<Resource>.Find("TitaniumAlloy"));
            ExpantaNum compositeBeforeRepair = resourceManager.GetAmount(DataBase<Resource>.Find("Composite"));
            ExpantaNum phantomWeaveBeforeRepair = resourceManager.GetAmount(DataBase<Resource>.Find("PhantomWeave"));
            ExpantaNum rocketFuelBeforeRepair = resourceManager.GetAmount(DataBase<Resource>.Find("RocketFuel"));

            GameState runtimeState = new GameState();
            runtimeState.BeginCampaignForEditor("ProximaB");
            runtimeState.RecordCampaignCombatForEditor(new ExpantaNum(0.8d), new ExpantaNum(10));
            SectorManager manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            SectorState sectorState = manager.GetState(DataBase<SectorDefinition>.Find("ProximaB"));
            sectorState.SetCampaignActiveForEditor(true);
            sectorState.SetCampaignCasualtiesForEditor(new ExpantaNum(10));

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
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("TitaniumAlloy")), Is.LessThan(titaniumBeforeRepair));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("Composite")), Is.LessThan(compositeBeforeRepair));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("PhantomWeave")), Is.LessThan(phantomWeaveBeforeRepair));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("RocketFuel")), Is.LessThan(rocketFuelBeforeRepair));
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
            gameManager.State.RestoreCampaignForEditor(false, "ProximaB",
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
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.HomeSystemSurvey);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.InterstellarNavigation);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.DeepSpaceFleet);
            SectorDefinition moon = DataBase<SectorDefinition>.Find("ProximaB");
            SetCampaignResources(resourceManager, moon, new ExpantaNum(1000000));
            resourceManager.SetAmount(rocketFuel, new ExpantaNum(1000000));
            ExpantaNum rocketFuelBeforeFailure = resourceManager.GetAmount(rocketFuel);
            SectorState state = manager.GetState(moon);
            state.SetUnlockedForEditor(true);
            var runtimeState = new GameState();
            runtimeState.AdjustAttackPowerForEditor(new ExpantaNum(3000));
            runtimeState.AdjustMilitaryManpowerForEditor(new ExpantaNum(3000));
            runtimeState.SetSupplySatisfactionForEditor(ExpantaNum.One);
            SetCampaignFood(runtimeState, new ExpantaNum(1000000));
            ExpantaNum foodBeforeFailure = runtimeState.FoodAmount;
            // Leave only a tiny progress remainder so the low-cost 0.01s tick
            // reaches the commit path while FoodCapacity remains intentionally low.
            state.SetCampaignProgressForEditor(new ExpantaNum("0.99999"));

            Assert.Throws<System.InvalidOperationException>(() =>
                manager.TryAdvanceCampaign(
                    moon,
                    1d,
                    runtimeState,
                    resourceManager,
                    out _));

            Assert.That(runtimeState.FoodAmount, Is.Not.LessThan(foodBeforeFailure));
            Assert.That(runtimeState.FoodAmount, Is.Not.GreaterThan(foodBeforeFailure));
            Assert.That(resourceManager.GetAmount(rocketFuel), Is.Not.LessThan(rocketFuelBeforeFailure));
            Assert.That(resourceManager.GetAmount(rocketFuel), Is.Not.GreaterThan(rocketFuelBeforeFailure));
            Assert.That(state.Occupied, Is.False);
            Assert.That(state.CampaignProgress, Is.EqualTo(new ExpantaNum("0.99999")));
            Assert.That(state.CampaignCasualties, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(state.CampaignActive, Is.False);
            Assert.That(runtimeState.Campaign.Active, Is.False);
            Assert.That(runtimeState.Campaign.TargetSectorId, Is.Empty);
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
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
                runtimeState.AdjustFoodCapacityForEditor(new ExpantaNum(-400));
                throw new System.InvalidOperationException("test food-capacity failure");
            });
            manager.InitializeDefinitions();
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.HomeSystemSurvey);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.InterstellarNavigation);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.DeepSpaceFleet);
            SectorDefinition moon = DataBase<SectorDefinition>.Find("ProximaB");
            SetCampaignResources(resourceManager, moon, new ExpantaNum(100000));
            resourceManager.SetAmount(rocketFuel, new ExpantaNum(1000000));
            ExpantaNum rocketFuelBeforeFailure = resourceManager.GetAmount(rocketFuel);
            ExpantaNum foodBeforeFailure = runtimeState.FoodAmount;
            SectorState state = manager.GetState(moon);
            state.SetUnlockedForEditor(true);
            runtimeState.AdjustAttackPowerForEditor(new ExpantaNum(3000));
            runtimeState.AdjustMilitaryManpowerForEditor(new ExpantaNum(3000));
            runtimeState.SetSupplySatisfactionForEditor(ExpantaNum.One);
            // Leave only a tiny progress remainder so the low-cost 0.01s tick
            // reaches the commit path while FoodCapacity remains intentionally low.
            state.SetCampaignProgressForEditor(new ExpantaNum("0.999999999"));

            Assert.Throws<System.InvalidOperationException>(() =>
                manager.TryAdvanceCampaign(moon, 1d, runtimeState, resourceManager, out _));

            Assert.That(runtimeState.FoodAmount, Is.Not.LessThan(foodBeforeFailure));
            Assert.That(runtimeState.FoodAmount, Is.Not.GreaterThan(foodBeforeFailure));
            Assert.That(runtimeState.FoodCapacity, Is.GreaterThanOrEqualTo(runtimeState.FoodAmount));
            Assert.That(resourceManager.GetAmount(rocketFuel), Is.Not.LessThan(rocketFuelBeforeFailure));
            Assert.That(resourceManager.GetAmount(rocketFuel), Is.Not.GreaterThan(rocketFuelBeforeFailure));
            Assert.That(state.Occupied, Is.False);
        }
        finally
        {
            ProgressionModifierManager.Rebuild(null);
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
            ExpantaNum titaniumBeforeRepair = resourceManager.GetAmount(DataBase<Resource>.Find("TitaniumAlloy"));
            ExpantaNum compositeBeforeRepair = resourceManager.GetAmount(DataBase<Resource>.Find("Composite"));
            ExpantaNum phantomWeaveBeforeRepair = resourceManager.GetAmount(DataBase<Resource>.Find("PhantomWeave"));
            ExpantaNum rocketFuelBeforeRepair = resourceManager.GetAmount(DataBase<Resource>.Find("RocketFuel"));

            WorkshopUpgrade definition =
                Resources.Load<WorkshopUpgrade>("Datas/Workshop/InterstellarCombatSupplySystems");
            WorkshopUpgradeState workshopState = new WorkshopUpgradeState(definition);
            workshopState.SetPurchasedForEditor(true);
            ProgressionModifierManager.Rebuild(null, new[] { workshopState });

            GameState runtimeState = new GameState();
            runtimeState.BeginCampaignForEditor("ProximaB");
            runtimeState.RecordCampaignCombatForEditor(new ExpantaNum(0.8d), new ExpantaNum(10));
            SectorManager manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            SectorState sectorState = manager.GetState(DataBase<SectorDefinition>.Find("ProximaB"));
            sectorState.SetCampaignActiveForEditor(true);
            sectorState.SetCampaignCasualtiesForEditor(new ExpantaNum(10));

            Assert.That(manager.TryRepairFleet(
                runtimeState,
                resourceManager,
                new ExpantaNum(5),
                out ExpantaNum repairedAmount,
                out SectorOperationFailure failure), Is.True);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.None));
            Assert.That(repairedAmount, Is.EqualTo(new ExpantaNum(5)));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("TitaniumAlloy")), Is.LessThan(titaniumBeforeRepair));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("Composite")), Is.LessThan(compositeBeforeRepair));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("PhantomWeave")), Is.LessThan(phantomWeaveBeforeRepair));
            Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("RocketFuel")), Is.LessThan(rocketFuelBeforeRepair));
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
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.HomeSystemSurvey);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.InterstellarNavigation);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.DeepSpaceFleet);
            SectorState sectorState = manager.GetState(sector);
            sectorState.SetUnlockedForEditor(true);
            sectorState.SetCampaignActiveForEditor(true);
            sectorState.SetCampaignCasualtiesForEditor(new ExpantaNum(10));

            GameState runtimeState = new GameState();
            runtimeState.BeginCampaignForEditor(sector.Id);
            runtimeState.RecordCampaignCombatForEditor(new ExpantaNum(0.8d), new ExpantaNum(10));

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
            runtimeState.BeginCampaignForEditor(sector.Id);
            runtimeState.RecordCampaignCombatForEditor(new ExpantaNum(0.8d), new ExpantaNum(10));

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
    public void RepairWithoutResolvableCampaignTargetDoesNotChargeOrMutateGlobalState()
    {
        GameObject resourceObject = new GameObject("Repair-Missing-Target-ResourceManager");
        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            Resource titanium = DataBase<Resource>.Find("TitaniumAlloy");
            Resource composite = DataBase<Resource>.Find("Composite");
            Resource phantomWeave = DataBase<Resource>.Find("PhantomWeave");
            Resource rocketFuel = DataBase<Resource>.Find("RocketFuel");
            resourceManager.SetAmount(titanium, new ExpantaNum(20));
            resourceManager.SetAmount(composite, new ExpantaNum(10));
            resourceManager.SetAmount(phantomWeave, new ExpantaNum(10));
            resourceManager.SetAmount(rocketFuel, new ExpantaNum(5));

            ExpantaNum titaniumBefore = resourceManager.GetAmount(titanium);
            ExpantaNum compositeBefore = resourceManager.GetAmount(composite);
            ExpantaNum phantomWeaveBefore = resourceManager.GetAmount(phantomWeave);
            ExpantaNum rocketFuelBefore = resourceManager.GetAmount(rocketFuel);
            var runtimeState = new GameState();
            runtimeState.RestoreCampaignForEditor(
                false,
                "MissingCampaignTarget",
                new ExpantaNum(10),
                new ExpantaNum(0.8d));
            int campaignVersionBefore = runtimeState.Campaign.Version;
            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();

            bool repaired = manager.TryRepairFleet(
                runtimeState,
                resourceManager,
                new ExpantaNum(5),
                out ExpantaNum repairedAmount,
                out SectorOperationFailure failure);

            Assert.That(repaired, Is.False);
            Assert.That(repairedAmount, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.UnknownSector));
            Assert.That(runtimeState.Campaign.TargetSectorId, Is.EqualTo("MissingCampaignTarget"));
            Assert.That(runtimeState.Campaign.Casualties, Is.EqualTo(new ExpantaNum(10)));
            Assert.That(runtimeState.Campaign.CombatRatio, Is.EqualTo(new ExpantaNum(0.8d)));
            Assert.That(runtimeState.Campaign.Version, Is.EqualTo(campaignVersionBefore));
            Assert.That(resourceManager.GetAmount(titanium), Is.EqualTo(titaniumBefore));
            Assert.That(resourceManager.GetAmount(composite), Is.EqualTo(compositeBefore));
            Assert.That(resourceManager.GetAmount(phantomWeave), Is.EqualTo(phantomWeaveBefore));
            Assert.That(resourceManager.GetAmount(rocketFuel), Is.EqualTo(rocketFuelBefore));
        }
        finally
        {
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void CompletedCampaignBoundaryCannotRestoreAsActive()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition existingSector = DataBase<SectorDefinition>.Find("DawnRing");
        SectorDefinition campaignSector = DataBase<SectorDefinition>.Find("ProximaB");
        SectorState existingState = manager.GetState(existingSector);
        existingState.SetUnlockedForEditor(true);

        Assert.Throws<InvalidOperationException>(() =>
            manager.RestoreSaveData(new SaveManager.SectorSaveData
            {
                States = new List<SaveManager.SectorStateSaveData>
                {
                    new SaveManager.SectorStateSaveData
                    {
                        SectorId = campaignSector.Id,
                        Unlocked = true,
                        CampaignActive = true,
                        CampaignProgress = "1",
                        CampaignCasualties = "0",
                        CampaignCombatRatio = "1"
                    }
                }
            }));

        Assert.That(existingState.Unlocked, Is.True,
            "Invalid campaign boundaries must be rejected before existing sector state is reset.");
    }

    [Test]
    public void CompletedCampaignBoundaryCannotRestoreAsUnoccupiedInactiveState()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition sector = DataBase<SectorDefinition>.Find("ProximaB");

        Assert.Throws<InvalidOperationException>(() =>
            manager.RestoreSaveData(new SaveManager.SectorSaveData
            {
                States = new List<SaveManager.SectorStateSaveData>
                {
                    new SaveManager.SectorStateSaveData
                    {
                        SectorId = sector.Id,
                        Unlocked = true,
                        CampaignProgress = "1",
                        CampaignCasualties = "0",
                        CampaignCombatRatio = "0"
                    }
                }
            }));
    }

    [Test]
    public void NonFiniteCampaignFoodRateIsRejectedBeforeStartingForZeroSeconds()
    {
        GameObject resourceObject = new GameObject("Campaign-NonFinite-Cost-ResourceManager");
        SectorDefinition sector = DataBase<SectorDefinition>.Find("ProximaB");
        ExpantaNum originalFoodRate = sector.CampaignFoodPerSecond;
        var originalResourceRates = new List<Pair<Resource, ExpantaNum>>();
        for (int i = 0; i < sector.CampaignResourceRatesPerSecond.Count; i++)
            originalResourceRates.Add(sector.CampaignResourceRatesPerSecond[i]);

        try
        {
            ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
            var manager = new SectorManager(_ => { });
            manager.InitializeDefinitions();
            ProgressionModifierManager.Rebuild(null);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.HomeSystemSurvey);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.InterstellarNavigation);
            ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.DeepSpaceFleet);
            SectorState state = manager.GetState(sector);
            state.SetUnlockedForEditor(true);
            var runtimeState = new GameState();
            ExpantaNum foodBefore = runtimeState.FoodAmount;
            sector.SetCampaignCostsForEditor(ExpantaNum.PositiveInfinity, originalResourceRates);

            bool advanced = manager.TryAdvanceCampaign(
                sector,
                0d,
                runtimeState,
                resourceManager,
                out SectorOperationFailure failure);

            Assert.That(advanced, Is.False);
            Assert.That(failure, Is.EqualTo(SectorOperationFailure.InvalidCampaignCost));
            Assert.That(state.CampaignActive, Is.False);
            Assert.That(state.CampaignProgress, Is.EqualTo(ExpantaNum.Zero));
            Assert.That(runtimeState.Campaign.Active, Is.False);
            Assert.That(runtimeState.Campaign.TargetSectorId, Is.Empty);
            Assert.That(runtimeState.FoodAmount, Is.EqualTo(foodBefore));
        }
        finally
        {
            sector.SetCampaignCostsForEditor(originalFoodRate, originalResourceRates);
            ProgressionModifierManager.Rebuild(null);
            Object.DestroyImmediate(resourceObject);
        }
    }

    [Test]
    public void C812_CampaignPreviewExposesSupplyAndLogisticsSatisfaction()
    {
        var manager = new SectorManager(_ => { });
        manager.InitializeDefinitions();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("AzurePool");
        var runtimeState = new GameState();
        runtimeState.AdjustAttackPowerForEditor(new ExpantaNum(100));
        runtimeState.AdjustMilitaryManpowerForEditor(new ExpantaNum(100));
        runtimeState.SetSupplySatisfactionForEditor(new ExpantaNum(0.75d));
        runtimeState.SetPowerSatisfactionForEditor(new ExpantaNum(0.5d));
        runtimeState.SetLogisticsSatisfactionForEditor(new ExpantaNum(0.25d));

        SectorCampaignPreview preview = manager.GetCampaignPreview(moon, runtimeState, null);

        Assert.That(preview.SupplySatisfaction, Is.EqualTo(new ExpantaNum(0.75d)));
        Assert.That(preview.PowerSatisfaction, Is.EqualTo(new ExpantaNum(0.5d)));
        Assert.That(preview.LogisticsSatisfaction, Is.EqualTo(new ExpantaNum(0.25d)));
        Assert.That(preview.ProgressPerSecond, Is.GreaterThan(ExpantaNum.Zero));
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

    [Test]
    public void 近地轨道到月球再到火星的殖民周期必须逐级延长()
    {
        SectorDefinition lowOrbit = Resources.Load<SectorDefinition>("Datas/Sector/DawnRing");
        SectorDefinition moon = Resources.Load<SectorDefinition>("Datas/Sector/AzurePool");
        SectorDefinition mars = Resources.Load<SectorDefinition>("Datas/Sector/Terminus");

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
