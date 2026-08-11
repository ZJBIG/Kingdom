using System;
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
    [SerializeField] private List<WorkshopEffectDefinition> effects = new();

    public IReadOnlyList<Research> RequiredResearch => requiredResearch;
    public IReadOnlyList<WorkshopUpgrade> RequiredUpgrades => requiredUpgrades;
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceRequirements => ResourceAmountDefinitionList.ToPairs(resourceRequirements);
    public IReadOnlyList<WorkshopEffectDefinition> Effects => effects;

#if UNITY_EDITOR
    public void ConfigureForEditor(
        List<Research> research,
        List<WorkshopUpgrade> upgrades,
        List<Pair<Resource, ExpantaNum>> requirements,
        List<WorkshopEffectDefinition> upgradeEffects)
    {
        requiredResearch = research ?? new List<Research>();
        requiredUpgrades = upgrades ?? new List<WorkshopUpgrade>();
        resourceRequirements = ResourceAmountDefinitionList.FromPairs(requirements);
        effects = upgradeEffects ?? new List<WorkshopEffectDefinition>();
    }
#endif
}

public enum WorkshopEffectType
{
    BuildingProductionMultiplier,
    BuildingFoodProductionMultiplier,
    ResourceProductionMultiplier,
    GlobalResearchMultiplier,
    GlobalConstructionMultiplier,
    TerritoryGranted,
    MilitaryMultiplier,
    PowerMultiplier,
    GlobalBuildingProductionMultiplier,
    BuildingResearchPowerMultiplier,
    BuildingPowerProductionMultiplier,
    BuildingLogisticsProductionMultiplier,
    GlobalLogisticsMultiplier,
    FleetRepairCostMultiplier,
    PopulationGrowthMultiplier,
    OccupiedResourceProductionMultiplier,
    CampaignSupplyCostMultiplier,
    CampaignCasualtyMultiplier,
    BuildingConstructionMultiplier,
    ExplorationPowerMultiplier
}

[Serializable]
public sealed class WorkshopEffectDefinition
{
    public WorkshopEffectType Type;
    public Building Building;
    public Resource Resource;
    [SerializeField] private string value = "1";

    public ExpantaNum Value
    {
        get => value;
        set => this.value = value.ToString();
    }

    internal void ApplyTo(ProgressionModifierState modifiers)
    {
        if (modifiers == null)
            return;
        ExpantaNum numericValue = Value;
        ExpantaNum multiplier = numericValue > ExpantaNum.Zero && !numericValue.IsNaN ? numericValue : ExpantaNum.One;
        switch (Type)
        {
            case WorkshopEffectType.BuildingProductionMultiplier:
                modifiers.AddBuildingProductionMultiplier(Building, multiplier);
                break;
            case WorkshopEffectType.BuildingFoodProductionMultiplier:
                modifiers.AddBuildingFoodProductionMultiplier(Building, multiplier);
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
