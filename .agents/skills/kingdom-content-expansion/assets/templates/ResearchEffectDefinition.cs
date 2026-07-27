using System;
using UnityEngine;

public enum ResearchEffectType
{
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
    [SerializeField] private ResearchEffectType type;
    [SerializeField] private Building building;
    [SerializeField] private Resource resource;
    [SerializeField] private string systemId;
    [SerializeField] private ExpantaNum value = ExpantaNum.One;

    public ResearchEffectType Type => type;
    public Building Building => building;
    public Resource Resource => resource;
    public string SystemId => systemId;
    public ExpantaNum Value => value;
}

// Validate field combinations at Bootstrap/Editor time.
// Do not put runtime state in this definition.
