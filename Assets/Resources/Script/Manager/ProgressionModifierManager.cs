using System;
using System.Collections.Generic;

public sealed class ProgressionModifierState
{
    private readonly Dictionary<Building, ExpantaNum> buildingProductionMultipliers = new();
    private readonly Dictionary<Building, ExpantaNum> buildingResearchPowerMultipliers = new();
    private readonly Dictionary<Building, ExpantaNum> buildingPowerProductionMultipliers = new();
    private readonly Dictionary<Building, ExpantaNum> buildingLogisticsProductionMultipliers = new();
    private readonly Dictionary<Building, ExpantaNum> buildingConstructionMultipliers = new();
    private readonly Dictionary<Resource, ExpantaNum> resourceProductionMultipliers = new();
    private readonly HashSet<ResearchSystem> unlockedSystems = new();

    public ExpantaNum GlobalResearchMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum GlobalConstructionMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum GlobalBuildingProductionMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum GlobalLogisticsMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum GlobalFoodProductionMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum FoodCapacityMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum ProductivityGranted { get; internal set; } = ExpantaNum.Zero;
    public ExpantaNum TerritoryGranted { get; internal set; } = ExpantaNum.Zero;
    public ExpantaNum MilitaryMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum PowerMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum PopulationGrowthMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum PopulationProductivityMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum HappinessBonus { get; internal set; } = ExpantaNum.Zero;
    public ExpantaNum ExplorationPowerMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum FleetRepairCostMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum OccupiedResourceProductionMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum CampaignProgressMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum CampaignSupplyCostMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum CampaignCasualtyMultiplier { get; internal set; } = ExpantaNum.One;
    public ExpantaNum DeconstructionReturnRate { get; internal set; } = new ExpantaNum(0.05d);

    public IReadOnlyCollection<ResearchSystem> UnlockedSystems => unlockedSystems;
    public bool IsSystemUnlocked(ResearchSystem system) =>
        system != ResearchSystem.None && unlockedSystems.Contains(system);

    public ExpantaNum GetBuildingProductionMultiplier(Building building) =>
        GetMultiplier(buildingProductionMultipliers, building);


    public ExpantaNum GetBuildingResearchPowerMultiplier(Building building) =>
        GetMultiplier(buildingResearchPowerMultipliers, building);

    public ExpantaNum GetBuildingPowerProductionMultiplier(Building building) =>
        GetMultiplier(buildingPowerProductionMultipliers, building);

    public ExpantaNum GetBuildingLogisticsProductionMultiplier(Building building) =>
        GetMultiplier(buildingLogisticsProductionMultipliers, building);

    public ExpantaNum GetBuildingConstructionMultiplier(Building building) =>
        GetMultiplier(buildingConstructionMultipliers, building);

    public ExpantaNum GetResourceProductionMultiplier(Resource resource) =>
        GetMultiplier(resourceProductionMultipliers, resource);

    internal void AddBuildingProductionMultiplier(Building building, ExpantaNum value) =>
        AddMultiplier(buildingProductionMultipliers, building, value);


    internal void AddBuildingResearchPowerMultiplier(Building building, ExpantaNum value) =>
        AddMultiplier(buildingResearchPowerMultipliers, building, value);

    internal void AddBuildingPowerProductionMultiplier(Building building, ExpantaNum value) =>
        AddMultiplier(buildingPowerProductionMultipliers, building, value);

    internal void AddBuildingLogisticsProductionMultiplier(Building building, ExpantaNum value) =>
        AddMultiplier(buildingLogisticsProductionMultipliers, building, value);

    internal void AddBuildingConstructionMultiplier(Building building, ExpantaNum value) =>
        AddMultiplier(buildingConstructionMultipliers, building, value);

    internal void AddResourceProductionMultiplier(Resource resource, ExpantaNum value) =>
        AddMultiplier(resourceProductionMultipliers, resource, value);

    internal void AddGlobalResearchMultiplier(ExpantaNum value) =>
        GlobalResearchMultiplier = AdditiveMultiplier(GlobalResearchMultiplier, value);
    internal void AddGlobalConstructionMultiplier(ExpantaNum value) =>
        GlobalConstructionMultiplier = AdditiveMultiplier(GlobalConstructionMultiplier, value);
    internal void AddGlobalBuildingProductionMultiplier(ExpantaNum value) =>
        GlobalBuildingProductionMultiplier = AdditiveMultiplier(GlobalBuildingProductionMultiplier, value);
    internal void AddGlobalLogisticsMultiplier(ExpantaNum value) =>
        GlobalLogisticsMultiplier = AdditiveMultiplier(GlobalLogisticsMultiplier, value);
    internal void MultiplyGlobalFoodProductionMultiplier(ExpantaNum value) =>
        GlobalFoodProductionMultiplier *= NormalizeMultiplier(value);
    internal void AddFleetRepairCostMultiplier(ExpantaNum value) =>
        FleetRepairCostMultiplier *= NormalizeMultiplier(value);
    internal void AddMilitaryMultiplier(ExpantaNum value) =>
        MilitaryMultiplier = AdditiveMultiplier(MilitaryMultiplier, value);
    internal void AddPowerMultiplier(ExpantaNum value) =>
        PowerMultiplier = AdditiveMultiplier(PowerMultiplier, value);
    internal void AddPopulationGrowthMultiplier(ExpantaNum value) =>
        PopulationGrowthMultiplier = AdditiveMultiplier(PopulationGrowthMultiplier, value);
    internal void AddPopulationProductivityMultiplier(ExpantaNum value) =>
        PopulationProductivityMultiplier =
            AdditiveMultiplier(PopulationProductivityMultiplier, value);
    internal void AddHappinessBonus(ExpantaNum value) =>
        HappinessBonus += value > ExpantaNum.Zero && !value.IsNaN && !value.IsInfinity
            ? value
            : ExpantaNum.Zero;
    internal void AddExplorationPowerMultiplier(ExpantaNum value) =>
        ExplorationPowerMultiplier =
            AdditiveMultiplier(ExplorationPowerMultiplier, value);
    internal void AddOccupiedResourceProductionMultiplier(ExpantaNum value) =>
        OccupiedResourceProductionMultiplier =
            AdditiveMultiplier(OccupiedResourceProductionMultiplier, value);
    internal void AddCampaignProgressMultiplier(ExpantaNum value) =>
        CampaignProgressMultiplier =
            AdditiveMultiplier(CampaignProgressMultiplier, value);
    internal void AddCampaignSupplyCostMultiplier(ExpantaNum value) =>
        CampaignSupplyCostMultiplier *= NormalizeMultiplier(value);
    internal void AddCampaignCasualtyMultiplier(ExpantaNum value) =>
        CampaignCasualtyMultiplier *= NormalizeMultiplier(value);

    internal void SetDeconstructionReturnRate(ExpantaNum value)
    {
        if (value.IsNaN || value.IsInfinity)
            return;
        DeconstructionReturnRate = ExpantaNum.Max(
            DeconstructionReturnRate,
            ExpantaNum.Clamp(value, ExpantaNum.Zero, ExpantaNum.One));
    }

    internal void AddUnlockedSystem(ResearchSystem system)
    {
        if (system != ResearchSystem.None)
            unlockedSystems.Add(system);
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
            ? AdditiveMultiplier(current, value)
            : value;
    }

    private static ExpantaNum AdditiveMultiplier(ExpantaNum current, ExpantaNum value)
    {
        ExpantaNum normalized = NormalizeMultiplier(value);
        return ExpantaNum.Max(ExpantaNum.One, current + (normalized - ExpantaNum.One));
    }

    private static ExpantaNum NormalizeMultiplier(ExpantaNum value) =>
        value > ExpantaNum.Zero && !value.IsNaN ? value : ExpantaNum.One;
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
                ApplyResearchEffects(
                    rebuilt,
                    state.Definition.Effects,
                    state.Definition.TechLevel >= TechLevel.Spacer);
            }
        }
        if (workshopStates != null)
        {
            for (int i = 0; i < workshopStates.Count; i++)
            {
                WorkshopUpgradeState state = workshopStates[i];
                if (state == null || !state.Purchased)
                    continue;
                ApplyWorkshopEffects(
                    rebuilt,
                    state.Definition.Effects,
                    state.Definition.TechLevel >= TechLevel.Spacer);
            }
        }
        Current = rebuilt;
    }

    private static void ApplyResearchEffects(
        ProgressionModifierState modifiers,
        IReadOnlyList<ResearchEffectDefinition> effects,
        bool allowCombatEffects)
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
                modifiers.AddBuildingProductionMultiplier(effect.Building, effect.NumericValue);
                    break;
                case ResearchEffectType.GlobalFoodProductionMultiplier:
                    modifiers.MultiplyGlobalFoodProductionMultiplier(effect.NumericValue);
                    break;
                case ResearchEffectType.ResourceProductionMultiplier:
                modifiers.AddResourceProductionMultiplier(effect.Resource, effect.NumericValue);
                    break;
                case ResearchEffectType.GlobalResearchMultiplier:
                modifiers.AddGlobalResearchMultiplier(effect.NumericValue);
                    break;
                case ResearchEffectType.GlobalConstructionMultiplier:
                modifiers.AddGlobalConstructionMultiplier(effect.NumericValue);
                    break;
                case ResearchEffectType.BuildingConstructionMultiplier:
                modifiers.AddBuildingConstructionMultiplier(effect.Building, effect.NumericValue);
                    break;
                case ResearchEffectType.FoodCapacityMultiplier:
                    modifiers.FoodCapacityMultiplier *=
                        effect.NumericValue > ExpantaNum.Zero && !effect.NumericValue.IsNaN
                            ? effect.NumericValue
                            : ExpantaNum.One;
                    break;
                case ResearchEffectType.ProductivityGranted:
                    modifiers.ProductivityGranted += ExpantaNum.Max(ExpantaNum.Zero, effect.NumericValue);
                    break;
                case ResearchEffectType.TerritoryGranted:
                    modifiers.TerritoryGranted += ExpantaNum.Max(ExpantaNum.Zero, effect.NumericValue);
                    break;
                case ResearchEffectType.UnlockIndustrialWorkshop:
                    modifiers.AddUnlockedSystem(ResearchSystem.IndustrialWorkshop);
                    break;
                case ResearchEffectType.UnlockFirstContact:
                    modifiers.AddUnlockedSystem(ResearchSystem.FirstContact);
                    break;
                case ResearchEffectType.UnlockDeepSpaceFleet:
                    modifiers.AddUnlockedSystem(ResearchSystem.DeepSpaceFleet);
                    break;
                case ResearchEffectType.UnlockInterstellarNavigation:
                    modifiers.AddUnlockedSystem(ResearchSystem.InterstellarNavigation);
                    break;
                case ResearchEffectType.MilitaryMultiplier:
                    if (allowCombatEffects)
                        modifiers.AddMilitaryMultiplier(effect.NumericValue);
                    break;
                case ResearchEffectType.PowerMultiplier:
                    modifiers.AddPowerMultiplier(effect.NumericValue);
                    break;
                case ResearchEffectType.GlobalBuildingProductionMultiplier:
                    modifiers.AddGlobalBuildingProductionMultiplier(effect.NumericValue);
                    break;
                case ResearchEffectType.BuildingResearchPowerMultiplier:
                    modifiers.AddBuildingResearchPowerMultiplier(effect.Building, effect.NumericValue);
                    break;
                case ResearchEffectType.BuildingPowerProductionMultiplier:
                    modifiers.AddBuildingPowerProductionMultiplier(effect.Building, effect.NumericValue);
                    break;
                case ResearchEffectType.BuildingLogisticsProductionMultiplier:
                    modifiers.AddBuildingLogisticsProductionMultiplier(effect.Building, effect.NumericValue);
                    break;
                case ResearchEffectType.GlobalLogisticsMultiplier:
                    modifiers.AddGlobalLogisticsMultiplier(effect.NumericValue);
                    break;
                case ResearchEffectType.FleetRepairCostMultiplier:
                    modifiers.AddFleetRepairCostMultiplier(effect.NumericValue);
                    break;
                case ResearchEffectType.OccupiedResourceProductionMultiplier:
                    modifiers.AddOccupiedResourceProductionMultiplier(effect.NumericValue);
                    break;
                case ResearchEffectType.CampaignProgressMultiplier:
                    modifiers.AddCampaignProgressMultiplier(effect.NumericValue);
                    break;
                case ResearchEffectType.CampaignSupplyCostMultiplier:
                    modifiers.AddCampaignSupplyCostMultiplier(effect.NumericValue);
                    break;
                case ResearchEffectType.CampaignCasualtyMultiplier:
                    modifiers.AddCampaignCasualtyMultiplier(effect.NumericValue);
                    break;
                case ResearchEffectType.PopulationGrowthMultiplier:
                    modifiers.AddPopulationGrowthMultiplier(effect.NumericValue);
                    break;
                case ResearchEffectType.PopulationProductivityMultiplier:
                    modifiers.AddPopulationProductivityMultiplier(effect.NumericValue);
                    break;
                case ResearchEffectType.HappinessBonus:
                    modifiers.AddHappinessBonus(effect.NumericValue);
                    break;
                case ResearchEffectType.ExplorationPowerMultiplier:
                    modifiers.AddExplorationPowerMultiplier(effect.NumericValue);
                    break;
                case ResearchEffectType.DeconstructionReturnRate:
                    modifiers.SetDeconstructionReturnRate(effect.NumericValue);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"研究效果类型 {effect.Type} 没有对应的运行时处理分支。");
            }
        }
    }

    private static void ApplyWorkshopEffects(
        ProgressionModifierState modifiers,
        IReadOnlyList<WorkshopEffectDefinition> effects,
        bool allowCombatEffects)
    {
        if (effects == null)
            return;

        for (int i = 0; i < effects.Count; i++)
        {
            WorkshopEffectDefinition effect = effects[i];
            if (effect == null)
                continue;
            if (allowCombatEffects || effect.Type != WorkshopEffectType.MilitaryMultiplier)
                effect.ApplyTo(modifiers);
        }
    }

}
