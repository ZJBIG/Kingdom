using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Create", menuName = "Data/Workshop Upgrade", order = 0)]
public sealed class WorkshopUpgradeDefinition : GameDefinition
{
    public string Label;
    [TextArea] public string Description;
    public string Category;
    public int SortOrder;
    // Legacy content classification retained for serialized asset compatibility.
    // Workshop availability is governed by IndustrialWorkshop plus explicit prerequisites.
    public TechLevel TechLevel = TechLevel.Industrial;
    [SerializeField] private List<Research> requiredResearch = new();
    [SerializeField] private List<WorkshopUpgradeDefinition> requiredUpgrades = new();
    [SerializeField] private List<Pair<Resource, ExpantaNum>> resourceRequirements = new();
    [SerializeField] private List<WorkshopEffectDefinition> effects = new();

    public IReadOnlyList<Research> RequiredResearch => requiredResearch;
    public IReadOnlyList<WorkshopUpgradeDefinition> RequiredUpgrades => requiredUpgrades;
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceRequirements => resourceRequirements;
    public IReadOnlyList<WorkshopEffectDefinition> Effects => effects;

#if UNITY_EDITOR
    public void ConfigureForEditor(
        List<Research> research,
        List<WorkshopUpgradeDefinition> upgrades,
        List<Pair<Resource, ExpantaNum>> requirements,
        List<WorkshopEffectDefinition> upgradeEffects)
    {
        requiredResearch = research ?? new List<Research>();
        requiredUpgrades = upgrades ?? new List<WorkshopUpgradeDefinition>();
        resourceRequirements = requirements ?? new List<Pair<Resource, ExpantaNum>>();
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
    GlobalLogisticsMultiplier
}

[Serializable]
public sealed class WorkshopEffectDefinition
{
    public WorkshopEffectType Type;
    public Building Building;
    public Resource Resource;
    public ExpantaNum Value = ExpantaNum.One;

    internal void ApplyTo(ProgressionModifierState modifiers)
    {
        if (modifiers == null)
            return;
        ExpantaNum multiplier = Value > ExpantaNum.Zero && !Value.IsNaN ? Value : ExpantaNum.One;
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
            case WorkshopEffectType.TerritoryGranted:
                modifiers.TerritoryGranted += ExpantaNum.Max(ExpantaNum.Zero, Value);
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
        }
    }
}
