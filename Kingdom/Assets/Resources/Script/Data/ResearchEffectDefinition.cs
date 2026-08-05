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
    UnlockSystem = 9,
    MilitaryMultiplier = 10,
    PowerMultiplier = 11,
    GlobalBuildingProductionMultiplier = 12,
    BuildingResearchPowerMultiplier = 13,
    BuildingPowerProductionMultiplier = 14,
    BuildingLogisticsProductionMultiplier = 15,
    GlobalLogisticsMultiplier = 16,
    PopulationGrowthMultiplier = 17
}

[Serializable]
public sealed class ResearchEffectDefinition
{
    public ResearchEffectType Type;
    public Building Building;
    public Resource Resource;
    public string SystemId;
    public ExpantaNum Value = ExpantaNum.One;
}
