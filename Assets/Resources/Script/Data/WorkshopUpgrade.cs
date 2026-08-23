using System;
using System.ComponentModel;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "鍒涘缓宸ュ潑鍗囩骇", menuName = "鏁版嵁/宸ュ潑鍗囩骇", order = 0)]
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
    [Description("寤虹瓚鐢熶骇鏁堢巼")]
    BuildingProductionMultiplier,
    [Description("璧勬簮鐢熶骇鏁堢巼")]
    ResourceProductionMultiplier = 2,
    [Description("鍏ㄥ眬鐮旂┒鏁堢巼")]
    GlobalResearchMultiplier = 3,
    [Description("鍏ㄥ眬寤洪€犳晥鐜?)]
    GlobalConstructionMultiplier = 4,
    [Description("棰嗗湡澧炲姞")]
    TerritoryGranted = 5,
    [Description("鍐涗簨鑳藉姏")]
    MilitaryMultiplier = 6,
    [Description("鍏ㄥ眬鐢靛姏鏁堢巼")]
    PowerMultiplier = 7,
    [Description("鍏ㄥ眬寤虹瓚鐢熶骇鏁堢巼")]
    GlobalBuildingProductionMultiplier = 8,
    [Description("寤虹瓚鐮旂┒鏁堢巼")]
    BuildingResearchPowerMultiplier = 9,
    [Description("寤虹瓚鐢靛姏浜у嚭")]
    BuildingPowerProductionMultiplier = 10,
    [Description("寤虹瓚鐗╂祦浜у嚭")]
    BuildingLogisticsProductionMultiplier = 11,
    [Description("鍏ㄥ眬鐗╂祦鏁堢巼")]
    GlobalLogisticsMultiplier = 12,
    [Description("鑸伴槦缁翠慨鎴愭湰")]
    FleetRepairCostMultiplier = 13,
    [Description("浜哄彛澧為暱")]
    PopulationGrowthMultiplier = 14,
    [Description("鍗犻璧勬簮浜у嚭")]
    OccupiedResourceProductionMultiplier = 15,
    [Description("杩滃緛琛ョ粰鎴愭湰")]
    CampaignSupplyCostMultiplier = 16,
    [Description("杩滃緛浼や骸")]
    CampaignCasualtyMultiplier = 17,
    [Description("寤虹瓚寤洪€犳晥鐜?)]
    BuildingConstructionMultiplier = 18,
    [Description("鎺㈢储鑳藉姏")]
    ExplorationPowerMultiplier = 19,
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
                    $"宸ュ潑鏁堟灉绫诲瀷 {Type} 娌℃湁瀵瑰簲鐨勮繍琛屾椂澶勭悊鍒嗘敮銆?);
        }
    }
}
