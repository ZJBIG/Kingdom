using System;
using UnityEngine;

public enum ResearchEffectType
{
    UnlockBuilding,
    BuildingProductionMultiplier,
    BuildingFoodProductionMultiplier,
    ResourceProductionMultiplier,
    GlobalResearchMultiplier,
    GlobalConstructionMultiplier,
    FoodCapacityMultiplier,
    ProductivityGranted,
    TerritoryGranted,
    UnlockSystem,
    MilitaryMultiplier,
    PowerMultiplier
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
