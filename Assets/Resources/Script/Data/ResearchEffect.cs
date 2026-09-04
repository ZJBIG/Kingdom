using System;
using System.ComponentModel;

public enum ResearchEffectType
{
    BuildingProductionMultiplier = 1,
    ResourceProductionMultiplier = 3,
    GlobalResearchMultiplier = 4,
    GlobalConstructionMultiplier = 5,
    FoodCapacityMultiplier = 6,
    ProductivityGranted = 7,
    TerritoryGranted = 8,
    MilitaryMultiplier = 10,
    PowerMultiplier = 11,
    GlobalBuildingProductionMultiplier = 12,
    BuildingResearchPowerMultiplier = 13,
    BuildingPowerProductionMultiplier = 14,
    BuildingLogisticsProductionMultiplier = 15,
    GlobalLogisticsMultiplier = 16,
    PopulationGrowthMultiplier = 17,
    DeconstructionReturnRate = 22,
    UnlockIndustrialWorkshop = 18,
    UnlockHomeSystemSurvey = 19,
    UnlockDeepSpaceFleet = 20,
    UnlockInterstellarNavigation = 21,
    FleetRepairCostMultiplier = 23,
    OccupiedResourceProductionMultiplier = 24,
    CampaignProgressMultiplier = 25,
    CampaignSupplyCostMultiplier = 26,
    CampaignCasualtyMultiplier = 27,
    PopulationProductivityMultiplier = 28,
    ExplorationPowerMultiplier = 29,
    BuildingConstructionMultiplier = 30,
    HappinessBonus = 31,
    GlobalFoodProductionMultiplier = 32
}

public enum ResearchSystem
{
    None = 0,
    IndustrialWorkshop = 1,
    HomeSystemSurvey = 2,
    DeepSpaceFleet = 3,
    InterstellarNavigation = 4
}

[Serializable]
public sealed class ResearchEffectDefinition
{
    public ResearchEffectType Type;
    public Building Building;
    public Resource Resource;
    [UnityEngine.SerializeField] private string value = "1";

    public string Value
    {
        get => value;
        set => this.value = value ?? "0";
    }

    public ExpantaNum NumericValue => value;
}
