using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ResearchEffectTests
{
    private readonly List<Object> createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        ProgressionModifierManager.Rebuild(null);
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(createdObjects[i]);
        createdObjects.Clear();
    }

    [Test]
    public void Rebuild_AppliesCompletedEffectsAndIgnoresIncompleteResearch()
    {
        ResearchDef completed = CreateResearch("research-completed");
        completed.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.GlobalResearchMultiplier,
                Value = new ExpantaNum(2d)
            },
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.ProductivityGranted,
                Value = new ExpantaNum(3d)
            },
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.TerritoryGranted,
                Value = new ExpantaNum(4d)
            },
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.PopulationGrowthMultiplier,
                Value = new ExpantaNum(1.2d)
            },
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.PopulationProductivityMultiplier,
                Value = new ExpantaNum(1.3d)
            },
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.UnlockIndustrialWorkshop
            }
        });

        ResearchDef incomplete = CreateResearch("research-incomplete");
        incomplete.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.GlobalResearchMultiplier,
                Value = new ExpantaNum(99d)
            }
        });

        ResearchState completedState = CreateState(completed, true);
        ResearchState incompleteState = CreateState(incomplete, false);
        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            completedState,
            incompleteState
        });

        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        Assert.That(modifiers.GlobalResearchMultiplier.ToDouble(), Is.EqualTo(2d).Within(0.000001d));
        Assert.That(modifiers.ProductivityGranted.ToDouble(), Is.EqualTo(3d).Within(0.000001d));
        Assert.That(modifiers.TerritoryGranted.ToDouble(), Is.EqualTo(4d).Within(0.000001d));
        Assert.That(
            modifiers.PopulationGrowthMultiplier.ToDouble(),
            Is.EqualTo(1.2d).Within(0.000001d));
        Assert.That(
            modifiers.PopulationProductivityMultiplier.ToDouble(),
            Is.EqualTo(1.3d).Within(0.000001d));
        Assert.That(modifiers.IsSystemUnlocked(ResearchSystem.IndustrialWorkshop), Is.True);
    }

    [Test]
    public void Rebuild_AddsRepeatedGlobalEffectsWithoutCompounding()
    {
        ResearchDef first = CreateResearch("research-first");
        first.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.GlobalResearchMultiplier,
                Value = new ExpantaNum(2d)
            }
        });
        ResearchDef second = CreateResearch("research-second");
        second.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.GlobalResearchMultiplier,
                Value = new ExpantaNum(1.5d)
            }
        });

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(first, true),
            CreateState(second, true)
        });

        Assert.That(
            ProgressionModifierManager.Current.GlobalResearchMultiplier.ToDouble(),
            Is.EqualTo(2.5d).Within(0.000001d));
    }

    [Test]
    public void CompletedMedicalResearchAccumulatesPopulationProductivityAcrossEras()
    {
        ResearchDef herbalKnowledge = DataBase<ResearchDef>.Find("HerbalKnowledge");
        ResearchDef publicHealth = DataBase<ResearchDef>.Find("PublicHealth");
        ResearchDef modernMedicine = DataBase<ResearchDef>.Find("ModernMedicine");
        ResearchDef precisionMedicine = DataBase<ResearchDef>.Find("PrecisionMedicine");

        Assert.That(herbalKnowledge, Is.Not.Null);
        Assert.That(publicHealth, Is.Not.Null);
        Assert.That(modernMedicine, Is.Not.Null);
        Assert.That(precisionMedicine, Is.Not.Null);

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(herbalKnowledge, true),
            CreateState(publicHealth, true),
            CreateState(modernMedicine, true),
            CreateState(precisionMedicine, true)
        });

        Assert.That(
            ProgressionModifierManager.Current.PopulationProductivityMultiplier.ToDouble(),
            Is.EqualTo(2.2d).Within(0.000001d));
    }

    [Test]
    public void Rebuild_BuildingProductionEffectCanTargetOneOutputResource()
    {
        ResearchDef research = CreateResearch("targeted-building-output");
        BuildingDef building = CreateDefinition<BuildingDef>("multi-output-building");
        ResourceDef targeted = CreateDefinition<ResourceDef>("targeted-output");
        ResourceDef unrelated = CreateDefinition<ResourceDef>("unrelated-output");
        research.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.BuildingProductionMultiplier,
                Building = building,
                Resource = targeted,
                Value = new ExpantaNum(1.3d)
            }
        });

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(research, true)
        });

        Assert.That(
            ProgressionModifierManager.Current
                .GetBuildingResourceProductionMultiplier(building, targeted)
                .ToDouble(),
            Is.EqualTo(1.3d).Within(0.000001d));
        Assert.That(
            ProgressionModifierManager.Current
                .GetBuildingResourceProductionMultiplier(building, unrelated)
                .ToDouble(),
            Is.EqualTo(1d).Within(0.000001d));
        Assert.That(
            ProgressionModifierManager.Current.GetBuildingProductionMultiplier(building).ToDouble(),
            Is.EqualTo(1d).Within(0.000001d));
    }

    [Test]
    public void HappinessBonusEffectsUseAdditiveFractionValues()
    {
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
        {
            if (research == null || research.Effects == null)
                continue;
            foreach (ResearchEffectDefinition effect in research.Effects)
            {
                if (effect == null || effect.Type != ResearchEffectType.HappinessBonus)
                    continue;
                Assert.That(effect.NumericValue, Is.GreaterThanOrEqualTo(ExpantaNum.Zero), research.Id);
                Assert.That(effect.NumericValue, Is.LessThanOrEqualTo(HappinessFormula.MaximumBonus), research.Id);
            }
        }
    }

    [Test]
    public void DefinitionEffectValues_UseEditableStringsAndRuntimeNumericViews()
    {
        // 不用反射：直接写字符串、读数值视图，既证明「可编辑字符串」与
        // 「运行时数值视图」两侧一致，也比断言属性类型更强。
        ResearchEffectDefinition researchEffect = new ResearchEffectDefinition();
        researchEffect.Value = "1.25";
        Assert.That(researchEffect.Value, Is.EqualTo("1.25"));
        Assert.That(researchEffect.NumericValue.ToDouble(), Is.EqualTo(1.25d).Within(0.000001d));

        WorkshopEffectDefinition workshopEffect = new WorkshopEffectDefinition();
        workshopEffect.Value = "0.5";
        Assert.That(workshopEffect.Value, Is.EqualTo("0.5"));
        Assert.That(workshopEffect.NumericValue.ToDouble(), Is.EqualTo(0.5d).Within(0.000001d));

        // 置空必须归一化为 "0"，而不是保留 null 或在读取数值视图时抛异常。
        researchEffect.Value = null;
        Assert.That(researchEffect.Value, Is.EqualTo("0"));
        Assert.That(researchEffect.NumericValue.ToDouble(), Is.EqualTo(0d).Within(0.000001d));
    }

    [Test]
    public void 已完成医学研究会进入建筑管理器的人口实际生产力()
    {
        GameManager gameManager = CreateManager<GameManager>("医学生产力-游戏管理器");
        BuildingManager buildingManager = CreateManager<BuildingManager>("医学生产力-建筑管理器");
        ResearchDef modernMedicine = DataBase<ResearchDef>.Find("ModernMedicine");
        Assert.That(modernMedicine, Is.Not.Null);

        gameManager.State.RestorePopulationForEditor(new ExpantaNum(5d));

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(modernMedicine, false)
        });
        double productivityBeforeResearch = buildingManager.TotalProductivity.ToDouble();

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(modernMedicine, true)
        });
        Assert.That(
            buildingManager.TotalProductivity.ToDouble(),
            Is.GreaterThan(productivityBeforeResearch));
    }

    [Test]
    public void 医学人口生产力改善过载建筑开工率()
    {
        GameManager gameManager = CreateManager<GameManager>("医学建造门槛-游戏管理器");
        BuildingManager buildingManager = CreateManager<BuildingManager>("医学建造门槛-建筑管理器");
        ResearchDef modernMedicine = DataBase<ResearchDef>.Find("ModernMedicine");
        BuildingDef laborBuilding = CreateDefinition<BuildingDef>("医学劳动力门槛测试建筑");
        CreateManager<ResourceManager>("medical-threshold-resource-manager");
        laborBuilding.TechLevel = TechLevel.Animal;
        laborBuilding.ConfigureEconomyForEditor(
            new ExpantaNum(1.15d),
            ExpantaNum.Zero,
            new ExpantaNum(12d),
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            new List<Pair<ResourceDef, ExpantaNum>>(),
            new List<Pair<ResourceDef, ExpantaNum>>(),
            new List<Pair<ResourceDef, ExpantaNum>>());

        Assert.That(modernMedicine, Is.Not.Null);
        gameManager.State.RestorePopulationForEditor(new ExpantaNum(5d));

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(modernMedicine, false)
        });
        Assert.That(
            buildingManager.TryBuild(laborBuilding, ExpantaNum.One, out BuildFailure baselineFailure),
            Is.True);
        Assert.That(baselineFailure, Is.EqualTo(BuildFailure.None));
        ExpantaNum baselineRatio = buildingManager.GetLaborOperatingRatio(laborBuilding);
        Assert.That(baselineRatio, Is.GreaterThan(ExpantaNum.Zero).And.LessThan(ExpantaNum.One));

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(modernMedicine, true)
        });
        Assert.That(buildingManager.GetLaborOperatingRatio(laborBuilding), Is.GreaterThan(baselineRatio));
    }

    [Test]
    public void CompletedFoodCapacityResearchChangesBaseAndBuildingCapacityOnce()
    {
        CreateManager<GameManager>("粮食容量-游戏管理器");
        CreateManager<ResourceManager>("粮食容量-资源管理器");
        BuildingManager buildingManager = CreateManager<BuildingManager>("粮食容量-建筑管理器");

        ResearchDef foodPreservation = DataBase<ResearchDef>.Find("FoodPreservation");
        BuildingDef granary = DataBase<BuildingDef>.Find("Granary");
        Assert.That(foodPreservation, Is.Not.Null);
        Assert.That(granary, Is.Not.Null);

        BuildingState granaryState = buildingManager.EnsureBuilding(granary);
        granaryState.SetAmountForEditor(ExpantaNum.One);

        ProgressionModifierState previous = new ProgressionModifierState();
        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(foodPreservation, true)
        });
        buildingManager.ApplyProgressionModifierChangeForEditor(
            previous,
            ProgressionModifierManager.Current);

        Assert.That(
            GameManager.Instance.State.FoodCapacity.ToDouble(),
            Is.EqualTo(650d).Within(0.000001d));

        ProgressionModifierState completed = ProgressionModifierManager.Current;
        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(foodPreservation, true)
        });
        buildingManager.ApplyProgressionModifierChangeForEditor(
            completed,
            ProgressionModifierManager.Current);

        Assert.That(
            GameManager.Instance.State.FoodCapacity.ToDouble(),
            Is.EqualTo(650d).Within(0.000001d));
    }

    [Test]
    public void Rebuild_AddsPopulationGrowthEffectsWithoutCompounding()
    {
        ResearchDef first = CreateResearch("population-growth-first");
        first.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.PopulationGrowthMultiplier,
                Value = new ExpantaNum(1.2d)
            },
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.PopulationGrowthMultiplier,
                Value = new ExpantaNum(1.25d)
            }
        });

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(first, true)
        });

        Assert.That(
            ProgressionModifierManager.Current.PopulationGrowthMultiplier.ToDouble(),
            Is.EqualTo(1.45d).Within(0.000001d));
    }

    [Test]
    public void Rebuild_AppliesOccupiedResourceProductionTheory()
    {
        ResearchDef administration = CreateResearch("occupation-administration");
        administration.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.OccupiedResourceProductionMultiplier,
                Value = new ExpantaNum(1.2d)
            }
        });

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(administration, true)
        });

        Assert.That(
            ProgressionModifierManager.Current.OccupiedResourceProductionMultiplier.ToDouble(),
            Is.EqualTo(1.2d).Within(0.000001d));
    }

    [Test]
    public void Rebuild_AppliesCampaignProgressTheory()
    {
        ResearchDef doctrine = CreateResearch("campaign-doctrine");
        doctrine.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.CampaignProgressMultiplier,
                Value = new ExpantaNum(1.15d)
            }
        });

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(doctrine, true)
        });

        Assert.That(
            ProgressionModifierManager.Current.CampaignProgressMultiplier.ToDouble(),
            Is.EqualTo(1.15d).Within(0.000001d));
    }

    [Test]
    public void 轨道能源与深空物流效果必须作用于目标建筑()
    {
        ResearchDef powerTheory = Resources.Load<ResearchDef>(
            "Datas/Research/Spacer/OrbitalPowerTransmission");
        ResearchDef logisticsTheory = Resources.Load<ResearchDef>(
            "Datas/Research/Spacer/OrbitalLogisticsInfrastructure");
        WorkshopUpgradeDef powerWorkshop = Resources.Load<WorkshopUpgradeDef>(
            "Datas/Workshop/OrbitalPowerBeaming");
        WorkshopUpgradeDef logisticsWorkshop = Resources.Load<WorkshopUpgradeDef>(
            "Datas/Workshop/DeepSpaceNetworkAutomation");
        BuildingDef solarArray = DataBase<BuildingDef>.Find("OrbitalSolarArray");
        BuildingDef logisticsHub = DataBase<BuildingDef>.Find("OrbitalStation");
        BuildingDef deepSpaceRelay = DataBase<BuildingDef>.Find("DeepSpaceRelay");

        Assert.That(powerTheory, Is.Not.Null);
        Assert.That(logisticsTheory, Is.Not.Null);
        Assert.That(powerWorkshop, Is.Not.Null);
        Assert.That(logisticsWorkshop, Is.Not.Null);
        Assert.That(solarArray, Is.Not.Null);
        Assert.That(logisticsHub, Is.Not.Null);
        Assert.That(deepSpaceRelay, Is.Not.Null);

        WorkshopUpgradeState powerWorkshopState = new WorkshopUpgradeState(powerWorkshop);
        WorkshopUpgradeState logisticsWorkshopState = new WorkshopUpgradeState(logisticsWorkshop);
        powerWorkshopState.SetPurchasedForEditor(true);
        logisticsWorkshopState.SetPurchasedForEditor(true);

        ProgressionModifierManager.Rebuild(
            new List<ResearchState>
            {
                CreateState(powerTheory, true),
                CreateState(logisticsTheory, true)
            },
            new[] { powerWorkshopState, logisticsWorkshopState });

        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        Assert.That(
            modifiers.GetBuildingPowerProductionMultiplier(solarArray).ToDouble(),
            Is.EqualTo(1.6d).Within(0.000001d));
        Assert.That(
            modifiers.GetBuildingLogisticsProductionMultiplier(logisticsHub).ToDouble(),
            Is.EqualTo(1.5d).Within(0.000001d));
        Assert.That(
            modifiers.GetBuildingLogisticsProductionMultiplier(deepSpaceRelay).ToDouble(),
            Is.EqualTo(1.2d).Within(0.000001d));
    }

    [Test]
    public void 工业研究倍率必须进入建筑资源生产结算()
    {
        CreateManager<GameManager>("工业研究倍率-游戏管理器");
        ResourceManager resourceManager = CreateManager<ResourceManager>("工业研究倍率-资源管理器");
        CreateManager<BuildingManager>("工业研究倍率-建筑管理器");

        ResearchDef deepDrilling = DataBase<ResearchDef>.Find("DeepOilDrilling");
        BuildingDef oilDerrick = DataBase<BuildingDef>.Find("OilDerrick");
        ResourceDef crudeOil = DataBase<ResourceDef>.Find("CrudeOil");
        resourceManager.SetAmount(crudeOil, ExpantaNum.Zero);
        Assert.That(deepDrilling, Is.Not.Null);
        Assert.That(oilDerrick, Is.Not.Null);
        Assert.That(crudeOil, Is.Not.Null);

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(deepDrilling, true)
        });

        BuildingManager.ApplyRateDeltaForEditor(
            new BuildingState(oilDerrick),
            ExpantaNum.Zero,
            ExpantaNum.One,
            ExpantaNum.One,
            ExpantaNum.One,
            true);

        Assert.That(
            ProgressionModifierManager.Current.GetBuildingProductionMultiplier(oilDerrick).ToDouble(),
            Is.EqualTo(1.25d).Within(0.000001d));
    }

    [Test]
    public void 太空研究与工坊倍率必须进入电力和物流结算()
    {
        CreateManager<GameManager>("太空倍率-游戏管理器");
        CreateManager<ResourceManager>("太空倍率-资源管理器");
        CreateManager<BuildingManager>("太空倍率-建筑管理器");

        ResearchDef powerTheory = DataBase<ResearchDef>.Find("OrbitalPowerTransmission");
        ResearchDef logisticsTheory = DataBase<ResearchDef>.Find("OrbitalLogisticsInfrastructure");
        WorkshopUpgradeDef powerWorkshop = DataBase<WorkshopUpgradeDef>.Find("OrbitalPowerBeaming");
        WorkshopUpgradeDef logisticsWorkshop = DataBase<WorkshopUpgradeDef>.Find("AutonomousFleetLogistics");
        BuildingDef solarArray = DataBase<BuildingDef>.Find("OrbitalSolarArray");
        BuildingDef orbitalStation = DataBase<BuildingDef>.Find("OrbitalStation");

        Assert.That(powerTheory, Is.Not.Null);
        Assert.That(logisticsTheory, Is.Not.Null);
        Assert.That(powerWorkshop, Is.Not.Null);
        Assert.That(logisticsWorkshop, Is.Not.Null);
        Assert.That(solarArray, Is.Not.Null);
        Assert.That(orbitalStation, Is.Not.Null);

        WorkshopUpgradeState powerWorkshopState = new WorkshopUpgradeState(powerWorkshop);
        WorkshopUpgradeState logisticsWorkshopState = new WorkshopUpgradeState(logisticsWorkshop);
        powerWorkshopState.SetPurchasedForEditor(true);
        logisticsWorkshopState.SetPurchasedForEditor(true);

        ProgressionModifierManager.Rebuild(
            new List<ResearchState>
            {
                CreateState(powerTheory, true),
                CreateState(logisticsTheory, true)
            },
            new[] { powerWorkshopState, logisticsWorkshopState });

        BuildingManager.ApplyRateDeltaForEditor(
            new BuildingState(solarArray),
            ExpantaNum.Zero,
            ExpantaNum.One,
            ExpantaNum.One,
            ExpantaNum.One,
            true);
        BuildingManager.ApplyRateDeltaForEditor(
            new BuildingState(orbitalStation),
            ExpantaNum.Zero,
            ExpantaNum.One,
            ExpantaNum.One,
            ExpantaNum.One,
            true);

        ExpantaNum expectedPower = solarArray.PowerContribution *
            (powerTheory.Effects.Single(effect => effect.Type == ResearchEffectType.BuildingPowerProductionMultiplier &&
                effect.Building == solarArray).NumericValue +
            powerWorkshop.Effects.Single(effect => effect.Type == WorkshopEffectType.BuildingPowerProductionMultiplier &&
                effect.Building == solarArray).NumericValue - ExpantaNum.One);
        ExpantaNum expectedLogistics = orbitalStation.LogisticsContribution *
            (logisticsTheory.Effects.Single(effect => effect.Type == ResearchEffectType.BuildingLogisticsProductionMultiplier &&
                effect.Building == orbitalStation).NumericValue +
            logisticsWorkshop.Effects.Single(effect => effect.Type == WorkshopEffectType.BuildingLogisticsProductionMultiplier &&
                effect.Building == orbitalStation).NumericValue - ExpantaNum.One);
        Assert.That(expectedPower, Is.GreaterThan(solarArray.PowerContribution));
        Assert.That(expectedLogistics, Is.GreaterThan(orbitalStation.LogisticsContribution));
        Assert.That(GameManager.Instance.State.PowerProductionRate.ToDouble(),
            Is.EqualTo(expectedPower.ToDouble()).Within(0.000001d));
        Assert.That(GameManager.Instance.State.LogisticsProductionRate.ToDouble(),
            Is.EqualTo(expectedLogistics.ToDouble()).Within(0.000001d));
    }

    [Test]
    public void 星际补给链理论与自动化补给模块会降低持续补给倍率()
    {
        ResearchDef theory = Resources.Load<ResearchDef>(
            "Datas/Research/Spacer/InterstellarSupplyChainTheory");
        WorkshopUpgradeDef modules = Resources.Load<WorkshopUpgradeDef>(
            "Datas/Workshop/AutomatedFleetResupplyModules");
        Assert.That(theory, Is.Not.Null);
        Assert.That(modules, Is.Not.Null);

        ResearchState theoryState = CreateState(theory, true);
        WorkshopUpgradeState modulesState = new WorkshopUpgradeState(modules);
        modulesState.SetPurchasedForEditor(true);

        ProgressionModifierManager.Rebuild(
            new List<ResearchState> { theoryState },
            new[] { modulesState });

        Assert.That(
            ProgressionModifierManager.Current.CampaignSupplyCostMultiplier.ToDouble(),
            Is.EqualTo(0.88d * 0.90d).Within(0.000001d));
    }

    [Test]
    public void 舰队损伤控制理论会降低实际战损倍率()
    {
        ResearchDef theory = CreateResearch("fleet-damage-control");
        theory.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.CampaignCasualtyMultiplier,
                Value = new ExpantaNum(0.90d)
            }
        });

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(theory, true)
        });

        Assert.That(
            ProgressionModifierManager.Current.CampaignCasualtyMultiplier.ToDouble(),
            Is.EqualTo(0.90d).Within(0.000001d));
        Assert.That(
            CampaignManager.CalculateCasualtyAmount(
                new ExpantaNum(0.8d),
                60d,
                ProgressionModifierManager.Current.CampaignCasualtyMultiplier).ToDouble(),
            Is.EqualTo(0.6d).Within(0.00001d));
    }

    [Test]
    public void Rebuild_UsesTheHighestCompletedDeconstructionReturnRate()
    {
        ResearchDef early = CreateResearch("deconstruction-early");
        early.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.DeconstructionReturnRate,
                Value = new ExpantaNum(0.10d)
            }
        });
        ResearchDef late = CreateResearch("deconstruction-late");
        late.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.DeconstructionReturnRate,
                Value = new ExpantaNum(0.90d)
            }
        });

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(early, true),
            CreateState(late, true)
        });

        Assert.That(
            ProgressionModifierManager.Current.DeconstructionReturnRate.ToDouble(),
            Is.EqualTo(0.90d).Within(0.000001d));
    }

    [Test]
    public void 已完成研究才会提升真实拆除返还率()
    {
        ResearchDef stoneProcessing = DataBase<ResearchDef>.Find("StoneProcessing");
        ResearchDef urbanHousing = DataBase<ResearchDef>.Find("UrbanHousing");
        ResearchDef industrialization = DataBase<ResearchDef>.Find("Industrialization");
        ResearchDef orbitalStationEngineering = DataBase<ResearchDef>.Find("OrbitalStationEngineering");
        Assert.That(stoneProcessing, Is.Not.Null);
        Assert.That(urbanHousing, Is.Not.Null);
        Assert.That(industrialization, Is.Not.Null);
        Assert.That(orbitalStationEngineering, Is.Not.Null);

        ProgressionModifierManager.Rebuild(new List<ResearchState>());
        Assert.That(
            ProgressionModifierManager.Current.DeconstructionReturnRate.ToDouble(),
            Is.EqualTo(0.05d).Within(0.000001d));

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(stoneProcessing, true),
            CreateState(urbanHousing, false),
            CreateState(industrialization, false),
            CreateState(orbitalStationEngineering, false)
        });
        Assert.That(
            ProgressionModifierManager.Current.DeconstructionReturnRate.ToDouble(),
            Is.EqualTo(0.10d).Within(0.000001d));

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(stoneProcessing, true),
            CreateState(urbanHousing, true),
            CreateState(industrialization, false),
            CreateState(orbitalStationEngineering, false)
        });
        Assert.That(
            ProgressionModifierManager.Current.DeconstructionReturnRate.ToDouble(),
            Is.EqualTo(0.25d).Within(0.000001d));

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(industrialization, false),
            CreateState(orbitalStationEngineering, false)
        });
        Assert.That(
            ProgressionModifierManager.Current.DeconstructionReturnRate.ToDouble(),
            Is.EqualTo(0.05d).Within(0.000001d));

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(industrialization, true),
            CreateState(orbitalStationEngineering, false)
        });
        Assert.That(
            ProgressionModifierManager.Current.DeconstructionReturnRate.ToDouble(),
            Is.EqualTo(0.50d).Within(0.000001d));

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(industrialization, true),
            CreateState(orbitalStationEngineering, true)
        });
        Assert.That(
            ProgressionModifierManager.Current.DeconstructionReturnRate.ToDouble(),
            Is.EqualTo(0.90d).Within(0.000001d));
    }

    [Test]
    public void 轨道居住研究完成后才会提升人口增长倍率()
    {
        ResearchDef orbitalStationEngineering = DataBase<ResearchDef>.Find("OrbitalStationEngineering");
        Assert.That(orbitalStationEngineering, Is.Not.Null);

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(orbitalStationEngineering, false)
        });
        Assert.That(
            ProgressionModifierManager.Current.PopulationGrowthMultiplier.ToDouble(),
            Is.EqualTo(1d).Within(0.000001d));

        ProgressionModifierManager.Rebuild(new List<ResearchState>
        {
            CreateState(orbitalStationEngineering, true)
        });
        Assert.That(
            ProgressionModifierManager.Current.PopulationGrowthMultiplier.ToDouble(),
            Is.EqualTo(1.25d).Within(0.000001d));
    }

    [Test]
    public void 建筑研究力倍率必须实际改变目标建筑的研究力倍率()
    {
        // P2-04「效果实际改变状态/倍率」：效果存在、类型合法、数值非中性，都不等于
        // 它真的改变了倍率。这里断言「生效前为中性值 → 生效后抬离中性值」，且不外溢到
        // 非目标建筑。只断言关系，不写死可调的倍率数值。
        ResearchDef writtenRecords = Resources.Load<ResearchDef>("Datas/Research/StoneAge/WrittenRecords");
        BuildingDef scribeHut = DataBase<BuildingDef>.Find("ScribeHut");
        BuildingDef library = DataBase<BuildingDef>.Find("Library");
        BuildingDef knowledgeCircle = DataBase<BuildingDef>.Find("KnowledgeCircle");

        Assert.That(writtenRecords, Is.Not.Null);
        Assert.That(scribeHut, Is.Not.Null);
        Assert.That(library, Is.Not.Null);
        Assert.That(knowledgeCircle, Is.Not.Null);

        ProgressionModifierManager.Rebuild(new List<ResearchState>());
        Assert.That(
            ProgressionModifierManager.Current.GetBuildingResearchPowerMultiplier(scribeHut).ToDouble(),
            Is.EqualTo(1d).Within(0.000001d),
            "未完成任何研究时，建筑研究力倍率必须是中性值，否则前后比较失去意义。");

        ProgressionModifierManager.Rebuild(
            new List<ResearchState> { CreateState(writtenRecords, true) });

        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        Assert.That(
            modifiers.GetBuildingResearchPowerMultiplier(scribeHut).ToDouble(),
            Is.GreaterThan(1d),
            "WrittenRecords 的 BuildingResearchPowerMultiplier 必须真的改变 ScribeHut 的研究力倍率。");
        Assert.That(
            modifiers.GetBuildingResearchPowerMultiplier(library).ToDouble(),
            Is.GreaterThan(1d),
            "WrittenRecords 的第二个目标建筑 Library 同样必须被改变。");
        Assert.That(
            modifiers.GetBuildingResearchPowerMultiplier(knowledgeCircle).ToDouble(),
            Is.EqualTo(1d).Within(0.000001d),
            "BuildingResearchPowerMultiplier 只能作用于效果指定的建筑，不得外溢。");
    }

    [Test]
    public void 建筑建造成本倍率必须实际改变目标建筑的建造成本倍率()
    {
        ResearchDef automation = Resources.Load<ResearchDef>(
            "Datas/Research/Spacer/OrbitalConstructionAutomation");
        BuildingDef habitat = DataBase<BuildingDef>.Find("OrbitalHabitatMegastructure");
        BuildingDef station = DataBase<BuildingDef>.Find("OrbitalStation");
        BuildingDef solarArray = DataBase<BuildingDef>.Find("OrbitalSolarArray");

        Assert.That(automation, Is.Not.Null);
        Assert.That(habitat, Is.Not.Null);
        Assert.That(station, Is.Not.Null);
        Assert.That(solarArray, Is.Not.Null);

        ProgressionModifierManager.Rebuild(new List<ResearchState>());
        Assert.That(
            ProgressionModifierManager.Current.GetBuildingConstructionMultiplier(habitat).ToDouble(),
            Is.EqualTo(1d).Within(0.000001d),
            "未完成任何研究时，建筑建造成本倍率必须是中性值。");

        ProgressionModifierManager.Rebuild(
            new List<ResearchState> { CreateState(automation, true) });

        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        Assert.That(
            modifiers.GetBuildingConstructionMultiplier(habitat).ToDouble(),
            Is.GreaterThan(1d),
            "OrbitalConstructionAutomation 的 BuildingConstructionMultiplier 必须真的改变目标建筑的建造成本倍率。");
        Assert.That(
            modifiers.GetBuildingConstructionMultiplier(station).ToDouble(),
            Is.GreaterThan(1d));
        Assert.That(
            modifiers.GetBuildingConstructionMultiplier(solarArray).ToDouble(),
            Is.EqualTo(1d).Within(0.000001d),
            "BuildingConstructionMultiplier 只能作用于效果指定的建筑，不得外溢。");
    }

    private ResearchDef CreateResearch(string id)
    {
        ResearchDef research = CreateDefinition<ResearchDef>(id);
        research.BaseCost = "100";
        return research;
    }

    private ResearchState CreateState(ResearchDef research, bool completed)
    {
        ResearchState state = new ResearchState(research);
        var paidCosts = new Dictionary<ResourceDef, ExpantaNum>();
        if (completed)
            foreach (Pair<ResourceDef, ExpantaNum> requirement in research.ResourceRequirements)
                if (requirement.First != null)
                    paidCosts[requirement.First] = requirement.Second;
        state.RestoreForEditor(ExpantaNum.Zero, false, completed, paidCosts);
        return state;
    }

    private T CreateDefinition<T>(string id) where T : ScriptableObject
    {
        T definition = ScriptableObject.CreateInstance<T>();
        createdObjects.Add(definition);
        definition.name = id;
        if (definition is GameDefinition gameDefinition)
            gameDefinition.SetIdForEditor(id);
        return definition;
    }

    private T CreateManager<T>(string name) where T : Component
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }
}
