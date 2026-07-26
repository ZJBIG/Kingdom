using System;
using System.Collections.Generic;

public sealed class ProgressionModifierState
{
    private readonly Dictionary<Building, ExpantaNum> buildingProductionMultipliers = new();
    private readonly Dictionary<Building, ExpantaNum> buildingFoodProductionMultipliers = new();
    private readonly Dictionary<Building, ExpantaNum> buildingResearchPowerMultipliers = new();
    private readonly Dictionary<Building, ExpantaNum> buildingPowerProductionMultipliers = new();
    private readonly Dictionary<Building, ExpantaNum> buildingLogisticsProductionMultipliers = new();
    private readonly Dictionary<Resource, ExpantaNum> resourceProductionMultipliers = new();
    private readonly HashSet<string> unlockedSystems = new(StringComparer.OrdinalIgnoreCase);

    public ExpantaNum GlobalResearchMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum GlobalConstructionMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum GlobalBuildingProductionMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum GlobalLogisticsMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum FoodCapacityMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum ProductivityGranted { get; internal set; } = ExpantaNum.Zero;
    public ExpantaNum TerritoryGranted { get; internal set; } = ExpantaNum.Zero;
    public ExpantaNum MilitaryMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum PowerMultiplier { get; internal set; } = ExpantaNum.One;

    public IReadOnlyCollection<string> UnlockedSystems => unlockedSystems;
    public bool IsSystemUnlocked(string systemId) =>
        !string.IsNullOrWhiteSpace(systemId) && unlockedSystems.Contains(systemId);

    public ExpantaNum GetBuildingProductionMultiplier(Building building) =>
        GetMultiplier(buildingProductionMultipliers, building);

    public ExpantaNum GetBuildingFoodProductionMultiplier(Building building) =>
        GetMultiplier(buildingFoodProductionMultipliers, building);

    public ExpantaNum GetBuildingResearchPowerMultiplier(Building building) =>
        GetMultiplier(buildingResearchPowerMultipliers, building);

    public ExpantaNum GetBuildingPowerProductionMultiplier(Building building) =>
        GetMultiplier(buildingPowerProductionMultipliers, building);

    public ExpantaNum GetBuildingLogisticsProductionMultiplier(Building building) =>
        GetMultiplier(buildingLogisticsProductionMultipliers, building);

    public ExpantaNum GetResourceProductionMultiplier(Resource resource) =>
        GetMultiplier(resourceProductionMultipliers, resource);

    internal void AddBuildingProductionMultiplier(Building building, ExpantaNum value) =>
        AddMultiplier(buildingProductionMultipliers, building, value);

    internal void AddBuildingFoodProductionMultiplier(Building building, ExpantaNum value) =>
        AddMultiplier(buildingFoodProductionMultipliers, building, value);

    internal void AddBuildingResearchPowerMultiplier(Building building, ExpantaNum value) =>
        AddMultiplier(buildingResearchPowerMultipliers, building, value);

    internal void AddBuildingPowerProductionMultiplier(Building building, ExpantaNum value) =>
        AddMultiplier(buildingPowerProductionMultipliers, building, value);

    internal void AddBuildingLogisticsProductionMultiplier(Building building, ExpantaNum value) =>
        AddMultiplier(buildingLogisticsProductionMultipliers, building, value);

    internal void AddResourceProductionMultiplier(Resource resource, ExpantaNum value) =>
        AddMultiplier(resourceProductionMultipliers, resource, value);

    internal void AddUnlockedSystem(string systemId)
    {
        if (!string.IsNullOrWhiteSpace(systemId))
            unlockedSystems.Add(systemId);
    }

    private static ExpantaNum GetMultiplier<T>(Dictionary<T, ExpantaNum> values, T key)
    {
        return !ReferenceEquals(key, null) && values.TryGetValue(key, out ExpantaNum value)
            ? value
            : ExpantaNum.One;
    }

    private static void AddMultiplier<T>(Dictionary<T, ExpantaNum> values, T key, ExpantaNum value)
    {
        if (ReferenceEquals(key, null) || value <= ExpantaNum.Zero || value.IsNaN)
            return;
        values[key] = values.TryGetValue(key, out ExpantaNum current)
            ? current * value
            : value;
    }
}

public static class ProgressionModifierManager
{
    public static ProgressionModifierState Current { get; private set; } = new ProgressionModifierState();

    public static void Rebuild(
        IReadOnlyList<ResearchState> researchStates,
        IReadOnlyList<WorkshopUpgradeState> workshopStates = null)
    {
        var rebuilt = new ProgressionModifierState();
        if (researchStates != null)
        {
            for (int i = 0; i < researchStates.Count; i++)
            {
                ResearchState state = researchStates[i];
                if (state?.Status != ResearchStatus.Completed)
                    continue;
                ApplyResearchEffects(rebuilt, state.Definition.Effects);
            }
        }
        if (workshopStates != null)
        {
            for (int i = 0; i < workshopStates.Count; i++)
            {
                WorkshopUpgradeState state = workshopStates[i];
                if (state == null || !state.Purchased)
                    continue;
                ApplyWorkshopEffects(rebuilt, state.Definition.Effects);
            }
        }
        Current = rebuilt;
    }

    private static void ApplyResearchEffects(
        ProgressionModifierState modifiers,
        IReadOnlyList<ResearchEffectDefinition> effects)
    {
        if (effects == null)
            return;

        for (int i = 0; i < effects.Count; i++)
        {
            ResearchEffectDefinition effect = effects[i];
            if (effect == null)
                continue;

            switch (effect.Type)
            {
                case ResearchEffectType.BuildingProductionMultiplier:
                    modifiers.AddBuildingProductionMultiplier(effect.Building, effect.Value);
                    break;
                case ResearchEffectType.BuildingFoodProductionMultiplier:
                    modifiers.AddBuildingFoodProductionMultiplier(effect.Building, effect.Value);
                    break;
                case ResearchEffectType.ResourceProductionMultiplier:
                    modifiers.AddResourceProductionMultiplier(effect.Resource, effect.Value);
                    break;
                case ResearchEffectType.GlobalResearchMultiplier:
                    modifiers.GlobalResearchMultiplier *= NormalizeMultiplier(effect.Value);
                    break;
                case ResearchEffectType.GlobalConstructionMultiplier:
                    modifiers.GlobalConstructionMultiplier *= NormalizeMultiplier(effect.Value);
                    break;
                case ResearchEffectType.FoodCapacityMultiplier:
                    modifiers.FoodCapacityMultiplier *= NormalizeMultiplier(effect.Value);
                    break;
                case ResearchEffectType.ProductivityGranted:
                    modifiers.ProductivityGranted += ExpantaNum.Max(ExpantaNum.Zero, effect.Value);
                    break;
                case ResearchEffectType.TerritoryGranted:
                    modifiers.TerritoryGranted += ExpantaNum.Max(ExpantaNum.Zero, effect.Value);
                    break;
                case ResearchEffectType.UnlockSystem:
                    modifiers.AddUnlockedSystem(effect.SystemId);
                    break;
                case ResearchEffectType.MilitaryMultiplier:
                    modifiers.MilitaryMultiplier *= NormalizeMultiplier(effect.Value);
                    break;
                case ResearchEffectType.PowerMultiplier:
                    modifiers.PowerMultiplier *= NormalizeMultiplier(effect.Value);
                    break;
                case ResearchEffectType.GlobalBuildingProductionMultiplier:
                    modifiers.GlobalBuildingProductionMultiplier *= NormalizeMultiplier(effect.Value);
                    break;
                case ResearchEffectType.BuildingResearchPowerMultiplier:
                    modifiers.AddBuildingResearchPowerMultiplier(effect.Building, effect.Value);
                    break;
                case ResearchEffectType.BuildingPowerProductionMultiplier:
                    modifiers.AddBuildingPowerProductionMultiplier(effect.Building, effect.Value);
                    break;
                case ResearchEffectType.BuildingLogisticsProductionMultiplier:
                    modifiers.AddBuildingLogisticsProductionMultiplier(effect.Building, effect.Value);
                    break;
                case ResearchEffectType.GlobalLogisticsMultiplier:
                    modifiers.GlobalLogisticsMultiplier *= NormalizeMultiplier(effect.Value);
                    break;
            }
        }
    }

    private static void ApplyWorkshopEffects(
        ProgressionModifierState modifiers,
        IReadOnlyList<WorkshopEffectDefinition> effects)
    {
        if (effects == null)
            return;

        for (int i = 0; i < effects.Count; i++)
        {
            WorkshopEffectDefinition effect = effects[i];
            if (effect == null)
                continue;
            effect.ApplyTo(modifiers);
        }
    }

    private static ExpantaNum NormalizeMultiplier(ExpantaNum value) =>
        value > ExpantaNum.Zero && !value.IsNaN ? value : ExpantaNum.One;
}
