using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class GlobalEconomyDefinitionTests
{
    [Test]
    public void ResourcePairDefinitionViewsReuseTheirCachedLists()
    {
        Building building = DataBase<Building>.All.First(value => value != null);
        Research research = DataBase<Research>.All.First(value => value != null);
        WorkshopUpgrade workshop = DataBase<WorkshopUpgrade>.All.First(value => value != null);
        SectorDefinition sector = DataBase<SectorDefinition>.All.First(value => value != null);

        Assert.That(ReferenceEquals(building.ResourceRequirements, building.ResourceRequirements), Is.True);
        Assert.That(ReferenceEquals(building.ResourceGenerationRates, building.ResourceGenerationRates), Is.True);
        Assert.That(ReferenceEquals(building.ResourceConsumptionRates, building.ResourceConsumptionRates), Is.True);
        Assert.That(ReferenceEquals(research.ResourceRequirements, research.ResourceRequirements), Is.True);
        Assert.That(ReferenceEquals(workshop.ResourceRequirements, workshop.ResourceRequirements), Is.True);
        Assert.That(ReferenceEquals(sector.ResourceRewards, sector.ResourceRewards), Is.True);
        Assert.That(ReferenceEquals(sector.OccupiedResourceRatesPerSecond, sector.OccupiedResourceRatesPerSecond), Is.True);
        Assert.That(ReferenceEquals(sector.ColonizationResourceRatesPerSecond, sector.ColonizationResourceRatesPerSecond), Is.True);
        Assert.That(ReferenceEquals(sector.CampaignResourceRatesPerSecond, sector.CampaignResourceRatesPerSecond), Is.True);
    }

    [Test]
    public void EditorReconfigurationInvalidatesResourcePairCaches()
    {
        Resource resource = ScriptableObject.CreateInstance<Resource>();
        Research research = ScriptableObject.CreateInstance<Research>();
        WorkshopUpgrade workshop = ScriptableObject.CreateInstance<WorkshopUpgrade>();
        SectorDefinition sector = ScriptableObject.CreateInstance<SectorDefinition>();
        Building building = ScriptableObject.CreateInstance<Building>();
        try
        {
            Pair<Resource, ExpantaNum> first = new(resource, new ExpantaNum("1"));
            Pair<Resource, ExpantaNum> second = new(resource, new ExpantaNum("2"));

            IReadOnlyList<Pair<Resource, ExpantaNum>> researchBefore = research.ResourceRequirements;
            research.SetResourceRequirementsForEditor(new List<Pair<Resource, ExpantaNum>> { first });
            Assert.That(ReferenceEquals(researchBefore, research.ResourceRequirements), Is.False);
            Assert.That(research.ResourceRequirements[0].Second, Is.EqualTo(first.Second));

            IReadOnlyList<Pair<Resource, ExpantaNum>> workshopBefore = workshop.ResourceRequirements;
            workshop.ConfigureForEditor(null, null,
                new List<Pair<Resource, ExpantaNum>> { first }, null);
            Assert.That(ReferenceEquals(workshopBefore, workshop.ResourceRequirements), Is.False);
            Assert.That(workshop.ResourceRequirements[0].Second, Is.EqualTo(first.Second));

            IReadOnlyList<Pair<Resource, ExpantaNum>> sectorBefore = sector.OccupiedResourceRatesPerSecond;
            sector.SetOccupiedResourceRatesForEditor(new List<Pair<Resource, ExpantaNum>> { second });
            Assert.That(ReferenceEquals(sectorBefore, sector.OccupiedResourceRatesPerSecond), Is.False);
            Assert.That(sector.OccupiedResourceRatesPerSecond[0].Second, Is.GreaterThan(ExpantaNum.Zero));

            IReadOnlyList<Pair<Resource, ExpantaNum>> buildingBefore = building.ResourceRequirements;
            building.ConfigureEconomyForEditor(
                ExpantaNum.One, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
                ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
                ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
                ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
                new List<Pair<Resource, ExpantaNum>> { first }, null, null);
            Assert.That(ReferenceEquals(buildingBefore, building.ResourceRequirements), Is.False);
            Assert.That(building.ResourceRequirements[0].Second, Is.EqualTo(first.Second));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(resource);
            UnityEngine.Object.DestroyImmediate(research);
            UnityEngine.Object.DestroyImmediate(workshop);
            UnityEngine.Object.DestroyImmediate(sector);
            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    [Test]
    public void ProductivityGrantsAreReservedForPopulationBuildings()
    {
        foreach (Building building in DataBase<Building>.All)
        {
            if (building.ProductivityGranted <= ExpantaNum.Zero)
                continue;

            Assert.That(
                building.PopulationCapacityGranted,
                Is.GreaterThan(ExpantaNum.Zero),
                $"建筑 {building.Id} 不能在没有人口容量的情况下直接提供生产力。");
        }
    }

    private static readonly string[] ReleasedResourceIds =
    {
        "Biomass", "Clay", "Cloth", "StoneBrick", "StoneChunk", "WoodLog",
        "Ceramic", "Chemical", "Coke", "Concrete", "CopperWire", "CrudeOil",
        "Electronics", "Engine", "Explosives", "Glass", "Lubricant", "Machinery",
        "RefinedFuel", "Rubber", "Aluminum", "Bronze", "Copper", "Iron",
        "Nickel", "Steel", "Tin", "TitaniumAlloy", "BauxiteOre", "Coal",
        "CopperOre", "IronOre", "NickelConcentrate", "TinOre", "TitaniumConcentrate",
        "Composite", "PhantomAlloy", "PhantomWeave", "PhaseMaterial", "RocketFuel"
    };

    [Test]
    public void MigrationProducesThePlannedDefinitionCounts()
    {
        Assert.That(AssetDatabase.FindAssets("t:Resource", new[] { "Assets" }).Length, Is.EqualTo(40));
        Assert.That(DataBase<Building>.All.Count, Is.EqualTo(66));

    }

    [Test]
    public void DefinitionNumericEditorFieldsRemainStringBacked()
    {
        Type[] definitionTypes =
        {
            typeof(Building),
            typeof(Research),
            typeof(ResearchEffectDefinition),
            typeof(WorkshopUpgrade),
            typeof(WorkshopEffectDefinition),
            typeof(SectorDefinition),
            typeof(ResourceAmountDefinition)
        };

        foreach (Type definitionType in definitionTypes)
        {
            FieldInfo[] fields = definitionType.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            foreach (FieldInfo field in fields)
            {
                Assert.That(
                    field.FieldType,
                    Is.Not.EqualTo(typeof(ExpantaNum)),
                    $"{definitionType.Name}.{field.Name} 不得直接序列化 ExpantaNum；编辑字段应使用 String。" );
            }
        }
    }

    [Test]
    public void 定义序列化字段必须使用字符串数值且统一采用每秒变化率()
    {
        Type[] definitionTypes =
        {
            typeof(ResourceAmountDefinition),
            typeof(Resource),
            typeof(Building),
            typeof(Research),
            typeof(ResearchEffectDefinition),
            typeof(WorkshopUpgrade),
            typeof(WorkshopEffectDefinition),
            typeof(SectorDefinition)
        };
        const BindingFlags flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (Type definitionType in definitionTypes)
        {
            foreach (FieldInfo field in definitionType.GetFields(flags))
            {
                Assert.That(field.Name, Does.Not.Contain("PerMinute"), definitionType.Name);
                Assert.That(field.Name, Does.Not.Contain("perMinute"), definitionType.Name);
                Assert.That(field.Name, Does.Not.Contain("Minute"), definitionType.Name);
                Assert.That(field.Name, Does.Not.Contain("minute"), definitionType.Name);
                bool serialized = field.IsPublic || field.GetCustomAttributes(false).Any(attribute =>
                    attribute.GetType().Name == "SerializeField");
                if (serialized)
                    Assert.That(field.FieldType, Is.Not.EqualTo(typeof(ExpantaNum)), field.Name);
            }

            foreach (PropertyInfo property in definitionType.GetProperties(flags))
            {
                Assert.That(property.Name, Does.Not.Contain("PerMinute"), definitionType.Name);
                Assert.That(property.Name, Does.Not.Contain("perMinute"), definitionType.Name);
                Assert.That(property.Name, Does.Not.Contain("Minute"), definitionType.Name);
                Assert.That(property.Name, Does.Not.Contain("minute"), definitionType.Name);
            }
        }
    }

    [Test]
    public void 所有效果必须填写与效果类型匹配的目标()
    {
        foreach (Research research in DataBase<Research>.All)
        {
            if (research == null || research.Effects == null)
                continue;
            foreach (ResearchEffectDefinition effect in research.Effects)
            {
                if (effect == null)
                    continue;
                if (ResearchEffectNeedsBuilding(effect.Type))
                    Assert.That(effect.Building, Is.Not.Null, research.Id);
                if (effect.Type == ResearchEffectType.ResourceProductionMultiplier)
                    Assert.That(effect.Resource, Is.Not.Null, research.Id);
            }
        }

        foreach (WorkshopUpgrade workshop in DataBase<WorkshopUpgrade>.All)
        {
            if (workshop == null || workshop.Effects == null)
                continue;
            foreach (WorkshopEffectDefinition effect in workshop.Effects)
            {
                if (effect == null)
                    continue;
                if (WorkshopEffectNeedsBuilding(effect.Type))
                    Assert.That(effect.Building, Is.Not.Null, workshop.Id);
                if (effect.Type == WorkshopEffectType.ResourceProductionMultiplier ||
                    effect.Type == WorkshopEffectType.OccupiedResourceProductionMultiplier)
                    Assert.That(effect.Resource, Is.Not.Null, workshop.Id);
            }
        }
    }

    [Test]
    public void RetiredToolsAreAbsentAndTheirStableIdsAreNotReused()
    {
        Assert.That(DataBase<Resource>.Contains("StoneTool"), Is.False);
        Assert.That(DataBase<Resource>.Contains("MetalTool"), Is.False);
        Assert.That(DataBase<Building>.Contains("StoneToolWorkshop"), Is.False);
        Assert.That(DataBase<Building>.Contains("Blacksmith"), Is.False);
        Assert.That(AssetDatabase.FindAssets("StoneTool t:Resource"), Is.Empty);
        Assert.That(AssetDatabase.FindAssets("MetalTool t:Resource"), Is.Empty);
    }

    private static bool ResearchEffectNeedsBuilding(ResearchEffectType type)
    {
        return type == ResearchEffectType.BuildingProductionMultiplier ||
            type == ResearchEffectType.BuildingConstructionMultiplier ||
            type == ResearchEffectType.BuildingResearchPowerMultiplier ||
            type == ResearchEffectType.BuildingPowerProductionMultiplier ||
            type == ResearchEffectType.BuildingLogisticsProductionMultiplier;
    }

    private static bool WorkshopEffectNeedsBuilding(WorkshopEffectType type)
    {
        return type == WorkshopEffectType.BuildingProductionMultiplier ||
            type == WorkshopEffectType.BuildingConstructionMultiplier ||
            type == WorkshopEffectType.BuildingResearchPowerMultiplier ||
            type == WorkshopEffectType.BuildingPowerProductionMultiplier ||
            type == WorkshopEffectType.BuildingLogisticsProductionMultiplier;
    }

    [Test]
    public void LegacyStoneResourceIdsMapToCurrentStableIds()
    {
        Type migrationType =
            typeof(SaveManager).Assembly.GetType("RetiredDefinitionMigration", true);
        MethodInfo normalize = migrationType.GetMethod(
            "NormalizeResourceId",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo normalizeBuilding = migrationType.GetMethod(
            "NormalizeBuildingId",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(
            normalize.Invoke(null, new object[] { "StoneChunk_Marble" }),
            Is.EqualTo("StoneChunk"));
        Assert.That(
            normalize.Invoke(null, new object[] { "StoneBrick_Marble" }),
            Is.EqualTo("StoneBrick"));
        Assert.That(
            normalize.Invoke(null, new object[] { "WoodLog" }),
            Is.EqualTo("WoodLog"));
        Assert.That(
            normalizeBuilding.Invoke(
                null,
                new object[] { "StoneCuttingWorkshop_Marble" }),
            Is.EqualTo("StoneCuttingWorkshop"));
    }

    [Test]
    public void ResearchNoLongerOwnsBuildingUnlockData()
    {
        const BindingFlags flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Assert.That(typeof(Research).GetField("BuildingUnlock", flags), Is.Null);
        Assert.That(Enum.IsDefined(typeof(ResearchEffectType), "UnlockBuilding"), Is.False);
        Assert.That(typeof(ProgressionModifierState).GetField("UnlockedBuildings", flags), Is.Null);
    }

    [Test]
    public void BuildingsOwnAndUseAllPrerequisites()
    {
        Building oilRefinery = DataBase<Building>.Find("OilRefinery");
        Assert.That(oilRefinery.RequiredResearch, Has.Count.EqualTo(2));
        Assert.That(oilRefinery.RequiredWorkshopUpgrades, Has.Count.EqualTo(1));

        foreach (Building building in DataBase<Building>.All)
        {
            AssertUnique(building.RequiredResearch, $"{building.Id} research");
            AssertUnique(building.RequiredWorkshopUpgrades, $"{building.Id} workshop");
        }
    }

    [Test]
    public void EveryWorkshopUpgradeHasAnEffectAndReferencedBuildingsAreDiscoverable()
    {
        int buildingGates = 0;
        foreach (WorkshopUpgrade upgrade in DataBase<WorkshopUpgrade>.All)
        {
            Assert.That(upgrade.Effects, Is.Not.Empty, upgrade.Id);
            Assert.That(
                upgrade.RequiredResearch.Count + upgrade.RequiredUpgrades.Count,
                Is.GreaterThan(0),
                $"{upgrade.Id} must have an explicit prerequisite");
            AssertUnique(upgrade.RequiredResearch, $"{upgrade.Id} research");
            AssertUnique(upgrade.RequiredUpgrades, $"{upgrade.Id} upgrade");
        }
        foreach (Building building in DataBase<Building>.All)
            buildingGates += building.RequiredWorkshopUpgrades.Count;
        Assert.That(buildingGates, Is.GreaterThanOrEqualTo(5));
    }

    [Test]
    public void 生产力建筑必须同时承担明确的独立功能()
    {
        foreach (Building building in DataBase<Building>.All)
        {
            if (building.ProductivityGranted <= ExpantaNum.Zero)
                continue;

            bool hasIndependentFunction =
                building.ResourceGenerationRates.Count > 0 ||
                building.PopulationCapacityGranted > ExpantaNum.Zero ||
                building.ResearchPowerGranted > ExpantaNum.Zero ||
                building.FoodProductionRate > ExpantaNum.Zero ||
                building.FoodCapacityGranted > ExpantaNum.Zero ||
                building.PowerProductionRate > ExpantaNum.Zero ||
                building.LogisticsProductionRate > ExpantaNum.Zero ||
                building.FleetPowerGranted > ExpantaNum.Zero ||
                building.AttackPowerGranted > ExpantaNum.Zero ||
                building.DefensePowerGranted > ExpantaNum.Zero ||
                building.MilitaryManpowerGranted > ExpantaNum.Zero;

            Assert.That(
                hasIndependentFunction,
                Is.True,
                $"建筑 {building.Id} 不能只提供生产力，还必须有明确的资源、能源、人口、物流或战略用途。");
        }
    }

    [Test]
    public void 每个建筑都必须承担明确的独立职责()
    {
        foreach (Building building in DataBase<Building>.All)
        {
            bool hasIndependentFunction =
                building.ResourceGenerationRates.Count > 0 ||
                building.PopulationCapacityGranted > ExpantaNum.Zero ||
                building.ResearchPowerGranted > ExpantaNum.Zero ||
                building.FoodProductionRate > ExpantaNum.Zero ||
                building.FoodCapacityGranted > ExpantaNum.Zero ||
                building.PowerProductionRate > ExpantaNum.Zero ||
                building.LogisticsProductionRate > ExpantaNum.Zero ||
                building.FleetPowerGranted > ExpantaNum.Zero ||
                building.AttackPowerGranted > ExpantaNum.Zero ||
                building.DefensePowerGranted > ExpantaNum.Zero ||
                building.MilitaryManpowerGranted > ExpantaNum.Zero;

            Assert.That(
                hasIndependentFunction,
                Is.True,
                $"建筑 {building.Id} 没有独立的资源、人口、研究、食物、能源、物流或军事职责。");
        }
    }

    [Test]
    public void 建筑不能重复声明完全相同的资源产出()
    {
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        foreach (Building building in DataBase<Building>.All)
        {
            if (building == null || building.ResourceGenerationRates == null ||
                building.ResourceGenerationRates.Count == 0)
                continue;

            var parts = new List<string>();
            foreach (Pair<Resource, ExpantaNum> pair in building.ResourceGenerationRates)
            {
                if (pair == null || pair.First == null || pair.Second <= ExpantaNum.Zero)
                    continue;
                parts.Add(pair.First.Id + "=" + pair.Second.ToString());
            }

            string signature = string.Join("|", parts.OrderBy(value => value, StringComparer.Ordinal));
            Assert.That(
                signatures.Add(signature),
                Is.True,
                $"建筑 {building.Id} 与另一个建筑重复声明了完全相同的资源产出 {signature}。");
        }
    }

    [Test]
    public void EveryResearchAndWorkshopEffectHasAValidTarget()
    {
        foreach (Research research in DataBase<Research>.All)
        {
            Assert.That(research.Effects, Is.Not.Null, research.Id);
            Assert.That(
                research.AdvancesTechLevel || research.Effects.Count > 0,
                Is.True,
                research.Id);
            foreach (ResearchEffectDefinition effect in research.Effects)
            {
                Assert.That(effect, Is.Not.Null, research.Id);
            Assert.That(effect.NumericValue.IsNaN, Is.False, research.Id);
                Assert.That(effect.NumericValue, Is.GreaterThan(ExpantaNum.Zero), research.Id);

                switch (effect.Type)
                {
                    case ResearchEffectType.BuildingProductionMultiplier:
                    case ResearchEffectType.BuildingConstructionMultiplier:
                    case ResearchEffectType.BuildingResearchPowerMultiplier:
                    case ResearchEffectType.BuildingPowerProductionMultiplier:
                    case ResearchEffectType.BuildingLogisticsProductionMultiplier:
                        Assert.That(effect.Building, Is.Not.Null,
                            $"研究 {research.Id} 的建筑效果缺少目标建筑。");
                        break;
                    case ResearchEffectType.ResourceProductionMultiplier:
                        Assert.That(effect.Resource, Is.Not.Null,
                            $"研究 {research.Id} 的资源效果缺少目标资源。");
                        break;
                    default:
                        break;
                }
            }
        }

        foreach (WorkshopUpgrade upgrade in DataBase<WorkshopUpgrade>.All)
        {
            Assert.That(upgrade.Effects, Is.Not.Null, upgrade.Id);
            Assert.That(upgrade.Effects, Is.Not.Empty, upgrade.Id);
            foreach (WorkshopEffectDefinition effect in upgrade.Effects)
            {
                Assert.That(effect, Is.Not.Null, upgrade.Id);
            Assert.That(effect.NumericValue.IsNaN, Is.False, upgrade.Id);
                Assert.That(effect.NumericValue, Is.GreaterThan(ExpantaNum.Zero), upgrade.Id);

                switch (effect.Type)
                {
                    case WorkshopEffectType.BuildingProductionMultiplier:
                    case WorkshopEffectType.BuildingConstructionMultiplier:
                    case WorkshopEffectType.BuildingResearchPowerMultiplier:
                    case WorkshopEffectType.BuildingPowerProductionMultiplier:
                    case WorkshopEffectType.BuildingLogisticsProductionMultiplier:
                        Assert.That(effect.Building, Is.Not.Null,
                            $"工坊 {upgrade.Id} 的建筑效果缺少目标建筑。");
                        break;
                    case WorkshopEffectType.ResourceProductionMultiplier:
                        Assert.That(effect.Resource, Is.Not.Null,
                            $"工坊 {upgrade.Id} 的资源效果缺少目标资源。");
                        break;
                }
            }
        }
    }

    [Test]
    public void 每个研究不能重复声明同一效果目标()
    {
        foreach (Research research in DataBase<Research>.All)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (ResearchEffectDefinition effect in research.Effects)
            {
                Assert.That(effect, Is.Not.Null, research.Id);
                string buildingId = effect.Building == null ? "" : effect.Building.Id;
                string resourceId = effect.Resource == null ? "" : effect.Resource.Id;
                string key = effect.Type + "|" + buildingId + "|" + resourceId;
                Assert.That(
                    keys.Add(key),
                    Is.True,
                    $"研究 {research.Id} 重复声明了效果目标 {key}。");
            }
        }
    }

    [Test]
    public void 每个工坊不能重复声明同一效果目标()
    {
        foreach (WorkshopUpgrade upgrade in DataBase<WorkshopUpgrade>.All)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorkshopEffectDefinition effect in upgrade.Effects)
            {
                Assert.That(effect, Is.Not.Null, upgrade.Id);
                string buildingId = effect.Building == null ? "" : effect.Building.Id;
                string resourceId = effect.Resource == null ? "" : effect.Resource.Id;
                string key = effect.Type + "|" + buildingId + "|" + resourceId;
                Assert.That(
                    keys.Add(key),
                    Is.True,
                    $"工坊 {upgrade.Id} 重复声明了效果目标 {key}。");
            }
        }
    }

    [Test]
    public void 所有效果类型都必须有运行时支持分支()
    {
        foreach (Research research in DataBase<Research>.All)
        {
            foreach (ResearchEffectDefinition effect in research.Effects)
            {
                Assert.That(
                    Enum.IsDefined(typeof(ResearchEffectType), effect.Type),
                    Is.True,
                    $"Research {research.Id} 使用了未定义的 Effect 类型 {effect.Type}。");
                Assert.That(
                    IsSupportedResearchEffect(effect.Type),
                    Is.True,
                    $"ResearchEffectType.{effect.Type} 没有对应的运行时处理分支。");
            }
        }

        foreach (WorkshopUpgrade upgrade in DataBase<WorkshopUpgrade>.All)
        {
            foreach (WorkshopEffectDefinition effect in upgrade.Effects)
            {
                Assert.That(
                    Enum.IsDefined(typeof(WorkshopEffectType), effect.Type),
                    Is.True,
                    $"Workshop {upgrade.Id} 使用了未定义的 Effect 类型 {effect.Type}。");
                Assert.That(
                    IsSupportedWorkshopEffect(effect.Type),
                    Is.True,
                    $"WorkshopEffectType.{effect.Type} 没有对应的运行时处理分支。");
            }
        }
    }

    private static bool IsSupportedResearchEffect(ResearchEffectType type)
    {
        switch (type)
        {
            case ResearchEffectType.BuildingProductionMultiplier:
            case ResearchEffectType.ResourceProductionMultiplier:
            case ResearchEffectType.GlobalResearchMultiplier:
            case ResearchEffectType.GlobalConstructionMultiplier:
            case ResearchEffectType.FoodCapacityMultiplier:
            case ResearchEffectType.ProductivityGranted:
            case ResearchEffectType.TerritoryGranted:
            case ResearchEffectType.MilitaryMultiplier:
            case ResearchEffectType.PowerMultiplier:
            case ResearchEffectType.GlobalBuildingProductionMultiplier:
            case ResearchEffectType.BuildingResearchPowerMultiplier:
            case ResearchEffectType.BuildingPowerProductionMultiplier:
            case ResearchEffectType.BuildingLogisticsProductionMultiplier:
            case ResearchEffectType.GlobalLogisticsMultiplier:
            case ResearchEffectType.PopulationGrowthMultiplier:
            case ResearchEffectType.DeconstructionReturnRate:
            case ResearchEffectType.UnlockIndustrialWorkshop:
            case ResearchEffectType.UnlockHomeSystemSurvey:
            case ResearchEffectType.UnlockDeepSpaceFleet:
            case ResearchEffectType.UnlockInterstellarNavigation:
            case ResearchEffectType.FleetRepairCostMultiplier:
            case ResearchEffectType.OccupiedResourceProductionMultiplier:
            case ResearchEffectType.CampaignProgressMultiplier:
            case ResearchEffectType.CampaignSupplyCostMultiplier:
            case ResearchEffectType.CampaignCasualtyMultiplier:
            case ResearchEffectType.PopulationProductivityMultiplier:
            case ResearchEffectType.ExplorationPowerMultiplier:
            case ResearchEffectType.BuildingConstructionMultiplier:
            case ResearchEffectType.HappinessBonus:
            case ResearchEffectType.GlobalFoodProductionMultiplier:
                return true;
            default:
                return false;
        }
    }

    private static bool IsSupportedWorkshopEffect(WorkshopEffectType type)
    {
        switch (type)
        {
            case WorkshopEffectType.BuildingProductionMultiplier:
            case WorkshopEffectType.ResourceProductionMultiplier:
            case WorkshopEffectType.GlobalResearchMultiplier:
            case WorkshopEffectType.GlobalConstructionMultiplier:
            case WorkshopEffectType.TerritoryGranted:
            case WorkshopEffectType.MilitaryMultiplier:
            case WorkshopEffectType.PowerMultiplier:
            case WorkshopEffectType.GlobalBuildingProductionMultiplier:
            case WorkshopEffectType.BuildingResearchPowerMultiplier:
            case WorkshopEffectType.BuildingPowerProductionMultiplier:
            case WorkshopEffectType.BuildingLogisticsProductionMultiplier:
            case WorkshopEffectType.GlobalLogisticsMultiplier:
            case WorkshopEffectType.FleetRepairCostMultiplier:
            case WorkshopEffectType.PopulationGrowthMultiplier:
            case WorkshopEffectType.OccupiedResourceProductionMultiplier:
            case WorkshopEffectType.CampaignSupplyCostMultiplier:
            case WorkshopEffectType.CampaignCasualtyMultiplier:
            case WorkshopEffectType.BuildingConstructionMultiplier:
            case WorkshopEffectType.ExplorationPowerMultiplier:
            case WorkshopEffectType.GlobalFoodProductionMultiplier:
                return true;
            default:
                return false;
        }
    }

    [Test]
    public void ReleasedResourcesExistAndIntegratedDependencyGraphIsReachable()
    {
        foreach (string id in ReleasedResourceIds)
            Assert.That(DataBase<Resource>.Contains(id), Is.True, id);

        Assert.That(EconomyDependencyValidator.Validate(
            DataBase<Resource>.All,
            DataBase<Building>.All,
            DataBase<Research>.All,
            DataBase<WorkshopUpgrade>.All,
            out string error), Is.True, error);
    }

    [Test]
    public void 所有资源都必须有来源和多个真实消费节点()
    {
        foreach (string id in ReleasedResourceIds)
        {
            Resource resource = DataBase<Resource>.Find(id);
            int sourceCount = 0;
            int sinkCount = 0;

            foreach (Building building in DataBase<Building>.All)
            {
                if (building == null)
                    continue;
                if (HasPositiveResourcePair(building.ResourceGenerationRates, resource))
                    sourceCount++;
                if (HasPositiveResourcePair(building.ResourceRequirements, resource) ||
                    HasPositiveResourcePair(building.ResourceConsumptionRates, resource))
                    sinkCount++;
            }

            foreach (Research research in DataBase<Research>.All)
                if (research != null && HasPositiveResourcePair(research.ResourceRequirements, resource))
                    sinkCount++;

            foreach (WorkshopUpgrade workshop in DataBase<WorkshopUpgrade>.All)
                if (workshop != null && HasPositiveResourcePair(workshop.ResourceRequirements, resource))
                    sinkCount++;

            foreach (SectorDefinition sector in DataBase<SectorDefinition>.All)
            {
                if (sector == null)
                    continue;
                if (HasPositiveResourcePair(sector.CampaignResourceRatesPerSecond, resource) ||
                    HasPositiveResourcePair(sector.ColonizationResourceRatesPerSecond, resource))
                    sinkCount++;
            }

            Assert.That(sourceCount, Is.GreaterThanOrEqualTo(1),
                $"资源 {id} 没有可达的生产来源。");
            Assert.That(sinkCount, Is.GreaterThanOrEqualTo(2),
                $"资源 {id} 少于两个真实消费节点，不能承担长期产业作用。");
        }
    }

    [Test]
    public void 字符串数值字段必须支持ExpantaNum隐式转换()
    {
        ResearchEffectDefinition researchEffect = new ResearchEffectDefinition();
        WorkshopEffectDefinition workshopEffect = new WorkshopEffectDefinition();

        researchEffect.Value = "1.25";
        workshopEffect.Value = "2.5";

        Assert.That(researchEffect.Value.ToDouble(), Is.EqualTo(1.25d).Within(0.000001d));
        Assert.That(workshopEffect.Value.ToDouble(), Is.EqualTo(2.5d).Within(0.000001d));
    }

    private static bool HasPositiveResourcePair(
        IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
        Resource resource)
    {
        if (pairs == null || resource == null)
            return false;
        foreach (Pair<Resource, ExpantaNum> pair in pairs)
            if (pair != null && pair.First == resource && pair.Second > ExpantaNum.Zero)
                return true;
        return false;
    }

    private static void AssertUnique<T>(IReadOnlyList<T> values, string context) where T : class
    {
        var unique = new HashSet<T>();
        for (int i = 0; i < values.Count; i++)
        {
            Assert.That(values[i], Is.Not.Null, context);
            Assert.That(unique.Add(values[i]), Is.True, context);
        }
    }
}
