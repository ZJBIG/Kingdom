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
    public void MetalDefinitions_UseSemanticNamesAndDistinctArtwork()
    {
        string[] ids =
        {
            "BasicMetalOre", "AdvancedMetalOre", "SpecialMetalOre", "BasicMetalIngot",
            "AdvancedMetalIngot", "Steel", "TitaniumAlloy"
        };
        var sprites = new HashSet<Sprite>();
        foreach (string id in ids)
        {
            ResourceDef resource = DataBase<ResourceDef>.Find(id);
            Assert.That(resource.name, Is.EqualTo(id), "Asset name must describe its resource identity.");
            Assert.That(resource.Sprite, Is.Not.Null, id);
            Assert.That(sprites.Add(resource.Sprite), Is.True, "Each metal has distinct artwork.");
            Assert.That(AssetDatabase.GetAssetPath(resource), Does.EndWith("/" + id + ".asset"));
            Assert.That(AssetDatabase.GetAssetPath(resource.Sprite),
                Is.EqualTo("Assets/Resources/Art/Metals/MetalInventoryAtlas.png"));
        }
    }

    [Test]
    public void ResourcePairDefinitionViewsReuseTheirCachedLists()
    {
        BuildingDef building = DataBase<BuildingDef>.All.First(value => value != null);
        ResearchDef research = DataBase<ResearchDef>.All.First(value => value != null);
        WorkshopUpgradeDef workshop = DataBase<WorkshopUpgradeDef>.All.First(value => value != null);
        SectorDef sector = DataBase<SectorDef>.All.First(value => value != null);

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
        ResourceDef resource = ScriptableObject.CreateInstance<ResourceDef>();
        ResearchDef research = ScriptableObject.CreateInstance<ResearchDef>();
        WorkshopUpgradeDef workshop = ScriptableObject.CreateInstance<WorkshopUpgradeDef>();
        SectorDef sector = ScriptableObject.CreateInstance<SectorDef>();
        BuildingDef building = ScriptableObject.CreateInstance<BuildingDef>();
        try
        {
            Pair<ResourceDef, ExpantaNum> first = new(resource, new ExpantaNum("1"));
            Pair<ResourceDef, ExpantaNum> second = new(resource, new ExpantaNum("2"));

            IReadOnlyList<Pair<ResourceDef, ExpantaNum>> researchBefore = research.ResourceRequirements;
            research.SetResourceRequirementsForEditor(new List<Pair<ResourceDef, ExpantaNum>> { first });
            Assert.That(ReferenceEquals(researchBefore, research.ResourceRequirements), Is.False);
            Assert.That(research.ResourceRequirements[0].Second, Is.EqualTo(first.Second));

            IReadOnlyList<Pair<ResourceDef, ExpantaNum>> workshopBefore = workshop.ResourceRequirements;
            workshop.ConfigureForEditor(null, null,
                new List<Pair<ResourceDef, ExpantaNum>> { first }, null);
            Assert.That(ReferenceEquals(workshopBefore, workshop.ResourceRequirements), Is.False);
            Assert.That(workshop.ResourceRequirements[0].Second, Is.EqualTo(first.Second));

            IReadOnlyList<Pair<ResourceDef, ExpantaNum>> sectorBefore = sector.OccupiedResourceRatesPerSecond;
            sector.SetOccupiedResourceRatesForEditor(new List<Pair<ResourceDef, ExpantaNum>> { second });
            Assert.That(ReferenceEquals(sectorBefore, sector.OccupiedResourceRatesPerSecond), Is.False);
            Assert.That(sector.OccupiedResourceRatesPerSecond[0].Second, Is.GreaterThan(ExpantaNum.Zero));

            IReadOnlyList<Pair<ResourceDef, ExpantaNum>> buildingBefore = building.ResourceRequirements;
            building.ConfigureEconomyForEditor(
                ExpantaNum.One, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
                ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
                ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
                ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
                new List<Pair<ResourceDef, ExpantaNum>> { first }, null, null);
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
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
        {
            if (building.ProductivityGranted <= ExpantaNum.Zero)
                continue;

            Assert.That(
                building.PopulationCapacityGranted,
                Is.GreaterThan(ExpantaNum.Zero),
                $"建筑 {building.Id} 不能在没有人口容量的情况下直接提供生产力。");
        }
    }

    [Test]
    public void 定义序列化字段必须使用字符串数值且统一采用每秒变化率()
    {
        Type[] definitionTypes =
        {
            typeof(ResourceAmountDefinition),
            typeof(ResourceDef),
            typeof(BuildingDef),
            typeof(ResearchDef),
            typeof(ResearchEffectDefinition),
            typeof(WorkshopUpgradeDef),
            typeof(WorkshopEffectDefinition),
            typeof(SectorDef)
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
    public void BuildingsOwnAndUseAllPrerequisites()
    {
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
        {
            AssertUnique(building.RequiredResearch, $"{building.Id} research");
            AssertUnique(building.RequiredWorkshopUpgrades, $"{building.Id} workshop");
        }
    }

    [Test]
    public void EveryWorkshopUpgradeHasAnEffectAndReferencedBuildingsAreDiscoverable()
    {
        int buildingGates = 0;
        foreach (WorkshopUpgradeDef upgrade in DataBase<WorkshopUpgradeDef>.All)
        {
            Assert.That(upgrade.Effects, Is.Not.Empty, upgrade.Id);
            Assert.That(
                upgrade.RequiredResearch.Count + upgrade.RequiredUpgrades.Count,
                Is.GreaterThan(0),
                $"{upgrade.Id} must have an explicit prerequisite");
            AssertUnique(upgrade.RequiredResearch, $"{upgrade.Id} research");
            AssertUnique(upgrade.RequiredUpgrades, $"{upgrade.Id} upgrade");
        }
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
            buildingGates += building.RequiredWorkshopUpgrades.Count;
        Assert.That(buildingGates, Is.GreaterThan(0));
    }

    [Test]
    public void 每个建筑都必须承担明确的独立职责()
    {
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
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
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
        {
            if (building == null || building.ResourceGenerationRates == null ||
                building.ResourceGenerationRates.Count == 0)
                continue;

            var parts = new List<string>();
            foreach (Pair<ResourceDef, ExpantaNum> pair in building.ResourceGenerationRates)
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
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
        {
            Assert.That(research.Effects, Is.Not.Null, research.Id);
            Assert.That(
                research.AdvancesTechLevel || research.Effects.Count > 0,
                Is.True,
                research.Id);
            foreach (ResearchEffectDefinition effect in research.Effects)
            {
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

        foreach (WorkshopUpgradeDef upgrade in DataBase<WorkshopUpgradeDef>.All)
        {
            Assert.That(upgrade.Effects, Is.Not.Null, upgrade.Id);
            Assert.That(upgrade.Effects, Is.Not.Empty, upgrade.Id);
            foreach (WorkshopEffectDefinition effect in upgrade.Effects)
            {
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
                    case WorkshopEffectType.OccupiedResourceProductionMultiplier:
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
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (ResearchEffectDefinition effect in research.Effects)
            {
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
        foreach (WorkshopUpgradeDef upgrade in DataBase<WorkshopUpgradeDef>.All)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorkshopEffectDefinition effect in upgrade.Effects)
            {
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
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
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

        foreach (WorkshopUpgradeDef upgrade in DataBase<WorkshopUpgradeDef>.All)
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
    public void IntegratedDependencyGraphIsReachable()
    {
        Assert.That(EconomyDependencyValidator.Validate(
            DataBase<ResourceDef>.All,
            DataBase<BuildingDef>.All,
            DataBase<ResearchDef>.All,
            DataBase<WorkshopUpgradeDef>.All,
            out string error), Is.True, error);
    }

    [Test]
    public void 所有资源都必须有来源和多个真实消费节点()
    {
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
        {
            string id = resource.Id;
            int sourceCount = 0;
            int sinkCount = 0;

            foreach (BuildingDef building in DataBase<BuildingDef>.All)
            {
                if (building == null)
                    continue;
                if (HasPositiveResourcePair(building.ResourceGenerationRates, resource))
                    sourceCount++;
                if (HasPositiveResourcePair(building.ResourceRequirements, resource) ||
                    HasPositiveResourcePair(building.ResourceConsumptionRates, resource))
                    sinkCount++;
            }

            foreach (ResearchDef research in DataBase<ResearchDef>.All)
                if (research != null && HasPositiveResourcePair(research.ResourceRequirements, resource))
                    sinkCount++;

            foreach (WorkshopUpgradeDef workshop in DataBase<WorkshopUpgradeDef>.All)
                if (workshop != null && HasPositiveResourcePair(workshop.ResourceRequirements, resource))
                    sinkCount++;

            foreach (SectorDef sector in DataBase<SectorDef>.All)
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

    private static bool HasPositiveResourcePair(
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> pairs,
        ResourceDef resource)
    {
        if (pairs == null || resource == null)
            return false;
        foreach (Pair<ResourceDef, ExpantaNum> pair in pairs)
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
