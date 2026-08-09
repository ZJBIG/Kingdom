using System;

public enum ResearchEffectType
{
    BuildingProductionMultiplier = 1,
    BuildingFoodProductionMultiplier = 2,
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
    UnlockIndustrialWorkshop = 18,
    UnlockFirstContact = 19,
    UnlockDeepSpaceFleet = 20,
    UnlockInterstellarNavigation = 21
}

public enum ResearchSystem
{
    None = 0,
    IndustrialWorkshop = 1,
    FirstContact = 2,
    DeepSpaceFleet = 3,
    InterstellarNavigation = 4
}

[Serializable]
public sealed class ResearchEffectDefinition
{
    public ResearchEffectType Type;
    public Building Building;
    public Resource Resource;
    public ExpantaNum Value = ExpantaNum.One;
}
