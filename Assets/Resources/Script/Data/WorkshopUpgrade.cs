using System;
using System.ComponentModel;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "创建工坊升级", menuName = "数据/工坊升级", order = 0)]
public sealed class WorkshopUpgrade : GameDefinition
{
    public string Label;
    [TextArea] public string Description;
    public int SortOrder;
    public TechLevel TechLevel = TechLevel.Industrial;
    [SerializeField] private List<Research> requiredResearch = new();
    [SerializeField] private List<WorkshopUpgrade> requiredUpgrades = new();
    [SerializeField] private List<ResourceAmountDefinition> resourceRequirements = new();
    [System.NonSerialized] private List<Pair<Resource, ExpantaNum>> resourceRequirementsCache;
    [SerializeField] private List<WorkshopEffectDefinition> effects = new();

    public IReadOnlyList<Research> RequiredResearch => requiredResearch;
    public IReadOnlyList<WorkshopUpgrade> RequiredUpgrades => requiredUpgrades;
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceRequirements =>
        ResourceAmountDefinitionList.ToPairs(resourceRequirements, ref resourceRequirementsCache);
    public IReadOnlyList<WorkshopEffectDefinition> Effects => effects;

#if UNITY_EDITOR
    private void OnValidate()
    {
        resourceRequirementsCache = null;
    }

    public void ConfigureForEditor(
        List<Research> research,
        List<WorkshopUpgrade> upgrades,
        List<Pair<Resource, ExpantaNum>> requirements,
        List<WorkshopEffectDefinition> upgradeEffects)
    {
        requiredResearch = research ?? new List<Research>();
        requiredUpgrades = upgrades ?? new List<WorkshopUpgrade>();
        resourceRequirements = ResourceAmountDefinitionList.FromPairs(requirements);
        resourceRequirementsCache = null;
        effects = upgradeEffects ?? new List<WorkshopEffectDefinition>();
    }
#endif
}

public enum WorkshopEffectType
{
    [Description("建筑生产效率")]
    BuildingProductionMultiplier,
    [Description("资源生产效率")]
    ResourceProductionMultiplier = 2,
    [Description("全局研究效率")]
    GlobalResearchMultiplier = 3,
    [Description("全局建造效率")]
    GlobalConstructionMultiplier = 4,
    [Description("领土增加")]
    TerritoryGranted = 5,
    [Description("军事能力")]
    MilitaryMultiplier = 6,
    [Description("全局电力效率")]
    PowerMultiplier = 7,
    [Description("全局建筑生产效率")]
    GlobalBuildingProductionMultiplier = 8,
    [Description("建筑研究效率")]
    BuildingResearchPowerMultiplier = 9,
    [Description("建筑电力产出")]
    BuildingPowerProductionMultiplier = 10,
    [Description("建筑物流产出")]
    BuildingLogisticsProductionMultiplier = 11,
    [Description("全局物流效率")]
    GlobalLogisticsMultiplier = 12,
    [Description("舰队维修成本")]
    FleetRepairCostMultiplier = 13,
    [Description("人口增长")]
    PopulationGrowthMultiplier = 14,
    [Description("占领资源产出")]
    OccupiedResourceProductionMultiplier = 15,
    [Description("远征补给成本")]
    CampaignSupplyCostMultiplier = 16,
    [Description("远征伤亡")]
    CampaignCasualtyMultiplier = 17,
    [Description("建筑建造效率")]
    BuildingConstructionMultiplier = 18,
    [Description("探索能力")]
    ExplorationPowerMultiplier = 19,
    [Description("全局粮食生产效率")]
    GlobalFoodProductionMultiplier = 20
}

[Serializable]
public sealed class WorkshopEffectDefinition
{
    public WorkshopEffectType Type;
    public Building Building;
    public Resource Resource;
    [SerializeField] private string value = "1";

    public string Value
    {
        get => value;
        set => this.value = value ?? "0";
    }

    public ExpantaNum NumericValue => value;

    internal void ApplyTo(ProgressionModifierState modifiers)
    {
        if (modifiers == null)
            return;
        ExpantaNum numericValue = NumericValue;
        ExpantaNum multiplier = numericValue > ExpantaNum.Zero && !numericValue.IsNaN ? numericValue : ExpantaNum.One;
        switch (Type)
        {
            case WorkshopEffectType.BuildingProductionMultiplier:
                modifiers.AddBuildingProductionMultiplier(Building, multiplier);
                break;
            case WorkshopEffectType.GlobalFoodProductionMultiplier:
                modifiers.MultiplyGlobalFoodProductionMultiplier(multiplier);
                break;
            case WorkshopEffectType.ResourceProductionMultiplier:
                modifiers.AddResourceProductionMultiplier(Resource, multiplier);
                break;
            case WorkshopEffectType.GlobalResearchMultiplier:
                modifiers.AddGlobalResearchMultiplier(multiplier);
                break;
            case WorkshopEffectType.GlobalConstructionMultiplier:
                modifiers.AddGlobalConstructionMultiplier(multiplier);
                break;
            case WorkshopEffectType.BuildingConstructionMultiplier:
                modifiers.AddBuildingConstructionMultiplier(Building, multiplier);
                break;
            case WorkshopEffectType.ExplorationPowerMultiplier:
                modifiers.AddExplorationPowerMultiplier(multiplier);
                break;
            case WorkshopEffectType.TerritoryGranted:
                modifiers.TerritoryGranted += ExpantaNum.Max(ExpantaNum.Zero, numericValue);
                break;
            case WorkshopEffectType.MilitaryMultiplier:
                modifiers.AddMilitaryMultiplier(multiplier);
                break;
            case WorkshopEffectType.PowerMultiplier:
                modifiers.AddPowerMultiplier(multiplier);
                break;
            case WorkshopEffectType.GlobalBuildingProductionMultiplier:
                modifiers.AddGlobalBuildingProductionMultiplier(multiplier);
                break;
            case WorkshopEffectType.BuildingResearchPowerMultiplier:
                modifiers.AddBuildingResearchPowerMultiplier(Building, multiplier);
                break;
            case WorkshopEffectType.BuildingPowerProductionMultiplier:
                modifiers.AddBuildingPowerProductionMultiplier(Building, multiplier);
                break;
            case WorkshopEffectType.BuildingLogisticsProductionMultiplier:
                modifiers.AddBuildingLogisticsProductionMultiplier(Building, multiplier);
                break;
            case WorkshopEffectType.GlobalLogisticsMultiplier:
                modifiers.AddGlobalLogisticsMultiplier(multiplier);
                break;
            case WorkshopEffectType.FleetRepairCostMultiplier:
                modifiers.AddFleetRepairCostMultiplier(multiplier);
                break;
            case WorkshopEffectType.PopulationGrowthMultiplier:
                modifiers.AddPopulationGrowthMultiplier(multiplier);
                break;
            case WorkshopEffectType.OccupiedResourceProductionMultiplier:
                modifiers.AddOccupiedResourceProductionMultiplier(multiplier);
                break;
            case WorkshopEffectType.CampaignSupplyCostMultiplier:
                modifiers.AddCampaignSupplyCostMultiplier(multiplier);
                break;
            case WorkshopEffectType.CampaignCasualtyMultiplier:
                modifiers.AddCampaignCasualtyMultiplier(multiplier);
                break;
            default:
                throw new InvalidOperationException(
                    $"工坊效果类型 {Type} 没有对应的运行时处理分支。");
        }
    }
}
