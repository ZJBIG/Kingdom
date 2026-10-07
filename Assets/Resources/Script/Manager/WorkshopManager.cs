using System;
using System.Collections.Generic;
using UnityEngine;

public enum WorkshopPurchaseFailure
{
    None,
    InvalidDefinition,
    SystemLocked,
    TechnologyInsufficient,
    ResearchIncomplete,
    UpgradeIncomplete,
    AlreadyPurchased,
    ResourceInsufficient
}

public enum WorkshopBenefitRateKind
{
    ResourceProduction,
    ResourceConsumption,
    FoodProduction,
    ResearchPower,
    PowerProduction,
    LogisticsProduction
}

public sealed class WorkshopBenefitRatePreview
{
    public Building Building { get; }
    public Resource Resource { get; }
    public WorkshopBenefitRateKind Kind { get; }
    public ExpantaNum Before { get; }
    public ExpantaNum After { get; }
    public ExpantaNum Change => After - Before;

    public WorkshopBenefitRatePreview(Building building, Resource resource,
        WorkshopBenefitRateKind kind, ExpantaNum before, ExpantaNum after)
    {
        Building = building;
        Resource = resource;
        Kind = kind;
        Before = before;
        After = after;
    }
}

public sealed class WorkshopManager : Singleton<WorkshopManager>
{
    private readonly Dictionary<WorkshopUpgrade, WorkshopUpgradeState> states = new();
    private readonly List<WorkshopUpgradeState> orderedStates = new();

    public IReadOnlyDictionary<WorkshopUpgrade, WorkshopUpgradeState> States => states;
    internal IReadOnlyList<WorkshopUpgradeState> OrderedStates => orderedStates;
    public event Action<WorkshopUpgradeState> UpgradeStateChanged;

    protected override void Initialize()
    {
        IReadOnlyList<WorkshopUpgrade> definitions = DataBase<WorkshopUpgrade>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            var state = new WorkshopUpgradeState(definitions[i]);
            states.Add(definitions[i], state);
            orderedStates.Add(state);
        }
    }

    public bool IsSystemUnlocked =>
        ProgressionModifierManager.Current.IsSystemUnlocked(ResearchSystem.IndustrialWorkshop);

    public bool IsPurchased(WorkshopUpgrade definition) =>
        TryGetStateByStableId(definition?.Id, out WorkshopUpgradeState state) && state.Purchased;

    public IReadOnlyList<WorkshopBenefitRatePreview> GetPurchaseBenefitPreview(WorkshopUpgrade definition)
    {
        if (!TryGetStateByStableId(definition?.Id, out WorkshopUpgradeState state))
            return Array.Empty<WorkshopBenefitRatePreview>();
        ProgressionModifierState preview = ProgressionModifierManager.BuildPreview(
            ResearchManager.Instance.OrderedStatesForProgression, orderedStates, state.Definition);
        return CalculateBenefitPreview(BuildingManager.Instance.OrderedStates,
            ProgressionModifierManager.Current, preview, ResourceManager.GetHappinessRewardMultiplier());
    }

    // Hold quantities and efficiencies fixed; the next tick will recalculate input satisfaction.
    public static IReadOnlyList<WorkshopBenefitRatePreview> CalculateBenefitPreview(
        IReadOnlyList<BuildingState> buildings, ProgressionModifierState before,
        ProgressionModifierState after) => CalculateBenefitPreview(buildings, before, after, ExpantaNum.One);

    public static IReadOnlyList<WorkshopBenefitRatePreview> CalculateBenefitPreview(
        IReadOnlyList<BuildingState> buildings, ProgressionModifierState before,
        ProgressionModifierState after, ExpantaNum productionRewardMultiplier)
    {
        var result = new List<WorkshopBenefitRatePreview>();
        AddRate(null, null, WorkshopBenefitRateKind.ResearchPower,
            ResearchManager.BaseResearchPower * before.GlobalResearchMultiplier,
            ResearchManager.BaseResearchPower * after.GlobalResearchMultiplier);
        AddRate(null, null, WorkshopBenefitRateKind.FoodProduction,
            GameState.BaseFoodProductionRate * before.GlobalFoodProductionMultiplier,
            GameState.BaseFoodProductionRate * after.GlobalFoodProductionMultiplier);
        if (buildings == null)
            return result;
        for (int i = 0; i < buildings.Count; i++)
        {
            BuildingState state = buildings[i];
            if (state == null || state.Amount <= ExpantaNum.Zero)
                continue;
            Building building = state.Definition;
            ExpantaNum scale = state.Amount * state.Efficiency;
            ExpantaNum oldProduction = before.GetBuildingProductionMultiplier(building) *
                before.GlobalBuildingProductionMultiplier;
            ExpantaNum newProduction = after.GetBuildingProductionMultiplier(building) *
                after.GlobalBuildingProductionMultiplier;
            foreach (Pair<Resource, ExpantaNum> rate in building.ResourceGenerationRates)
                AddRate(building, rate.First, WorkshopBenefitRateKind.ResourceProduction,
                    scale * rate.Second * oldProduction * productionRewardMultiplier *
                    before.GetBuildingResourceProductionMultiplier(building, rate.First) *
                    before.GetResourceProductionMultiplier(rate.First),
                    scale * rate.Second * newProduction * productionRewardMultiplier *
                    after.GetBuildingResourceProductionMultiplier(building, rate.First) *
                    after.GetResourceProductionMultiplier(rate.First));
            foreach (Pair<Resource, ExpantaNum> rate in building.ResourceConsumptionRates)
                AddRate(building, rate.First, WorkshopBenefitRateKind.ResourceConsumption,
                    scale * rate.Second * oldProduction, scale * rate.Second * newProduction);
            AddRate(building, null, WorkshopBenefitRateKind.FoodProduction,
                scale * building.FoodProductionRate * oldProduction * before.GlobalFoodProductionMultiplier,
                scale * building.FoodProductionRate * newProduction * after.GlobalFoodProductionMultiplier);
            AddRate(building, null, WorkshopBenefitRateKind.ResearchPower,
                scale * building.ResearchPowerGranted * before.GetBuildingResearchPowerMultiplier(building) *
                before.GlobalResearchMultiplier,
                scale * building.ResearchPowerGranted * after.GetBuildingResearchPowerMultiplier(building) *
                after.GlobalResearchMultiplier);
            AddRate(building, null, WorkshopBenefitRateKind.PowerProduction,
                scale * building.PowerProductionRate * before.PowerMultiplier *
                before.GetBuildingPowerProductionMultiplier(building),
                scale * building.PowerProductionRate * after.PowerMultiplier *
                after.GetBuildingPowerProductionMultiplier(building));
            AddRate(building, null, WorkshopBenefitRateKind.LogisticsProduction,
                scale * building.LogisticsProductionRate * before.GlobalLogisticsMultiplier *
                before.GetBuildingLogisticsProductionMultiplier(building),
                scale * building.LogisticsProductionRate * after.GlobalLogisticsMultiplier *
                after.GetBuildingLogisticsProductionMultiplier(building));
        }
        return result;

        void AddRate(Building building, Resource resource, WorkshopBenefitRateKind kind,
            ExpantaNum oldRate, ExpantaNum newRate)
        {
            if (oldRate != newRate)
                result.Add(new WorkshopBenefitRatePreview(building, resource, kind, oldRate, newRate));
        }
    }

    public bool ArePrerequisitesMet(WorkshopUpgrade definition)
    {
        if (definition == null)
            return false;
        for (int i = 0; i < definition.RequiredResearch.Count; i++)
            if (!ResearchManager.Instance.IsResearchCompleted(definition.RequiredResearch[i].Id))
                return false;
        for (int i = 0; i < definition.RequiredUpgrades.Count; i++)
            if (!IsPurchased(definition.RequiredUpgrades[i]))
                return false;
        return true;
    }

    public bool TryPurchase(
        WorkshopUpgrade definition,
        out WorkshopPurchaseFailure failure)
    {
        if (!TryGetStateByStableId(definition?.Id, out WorkshopUpgradeState state))
        {
            failure = WorkshopPurchaseFailure.InvalidDefinition;
            return false;
        }
        if (!IsSystemUnlocked)
        {
            failure = WorkshopPurchaseFailure.SystemLocked;
            return false;
        }
        if (definition.TechLevel > GameManager.Instance.State.TechLevel)
        {
            failure = WorkshopPurchaseFailure.TechnologyInsufficient;
            return false;
        }
        if (state.Purchased)
        {
            failure = WorkshopPurchaseFailure.AlreadyPurchased;
            return false;
        }
        for (int i = 0; i < definition.RequiredResearch.Count; i++)
        {
            if (!ResearchManager.Instance.IsResearchCompleted(definition.RequiredResearch[i].Id))
            {
                failure = WorkshopPurchaseFailure.ResearchIncomplete;
                return false;
            }
        }
        for (int i = 0; i < definition.RequiredUpgrades.Count; i++)
        {
            if (!IsPurchased(definition.RequiredUpgrades[i]))
            {
                failure = WorkshopPurchaseFailure.UpgradeIncomplete;
                return false;
            }
        }

        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = definition.ResourceRequirements;
        var costs = new Dictionary<Resource, ExpantaNum>();
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = requirements[i];
            if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
            {
                if (requirement.First == null)
                {
                    failure = WorkshopPurchaseFailure.InvalidDefinition;
                    return false;
                }
                continue;
            }

            costs[requirement.First] = costs.TryGetValue(
                requirement.First, out ExpantaNum current)
                ? current + requirement.Second
                : requirement.Second;
        }

        bool previousPurchased = state.Purchased;
        bool paid = ResourceManager.Instance.TryApplyAtomicPayment(
            costs,
            () =>
            {
                state.SetPurchased(true);
                RebuildProgression();
            },
            () =>
            {
                state.SetPurchased(previousPurchased);
                RebuildProgression();
            });
        if (!paid)
        {
            failure = WorkshopPurchaseFailure.ResourceInsufficient;
            return false;
        }

        BuildingManager.Instance.RefreshBuildingChainAvailability();
        UpgradeStateChanged?.Invoke(state);
        failure = WorkshopPurchaseFailure.None;
        return true;
    }

    public SaveManager.WorkshopSaveData CaptureSaveData()
    {
        EnsureStateIndex();
        var result = new SaveManager.WorkshopSaveData
        {
            PurchasedUpgradeIds = new List<string>()
        };
        for (int i = 0; i < orderedStates.Count; i++)
            if (orderedStates[i].Purchased)
                result.PurchasedUpgradeIds.Add(orderedStates[i].Definition.Id);
        return result;
    }

    internal void RestoreSaveData(SaveManager.WorkshopSaveData data)
    {
        EnsureStateIndex();
        if (data?.PurchasedUpgradeIds == null)
        {
            ResetForLoad();
            RebuildProgression();
            return;
        }

        var purchasedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var purchasedDefinitions = new List<WorkshopUpgrade>(data.PurchasedUpgradeIds.Count);
        for (int i = 0; i < data.PurchasedUpgradeIds.Count; i++)
        {
            string id = data.PurchasedUpgradeIds[i];
            if (string.IsNullOrWhiteSpace(id) ||
                !DataBase<WorkshopUpgrade>.TryFind(id, out WorkshopUpgrade definition))
                throw new InvalidOperationException(
                    $"存档中的工坊升级 ID 无效：“{id}”。");
            string stableId = definition.Id.Trim();
            if (!purchasedIds.Add(stableId))
                throw new InvalidOperationException(
                    $"存档中的工坊升级重复包含“{definition.Id}”。");
            purchasedDefinitions.Add(definition);
        }

        for (int i = 0; i < purchasedDefinitions.Count; i++)
        {
            WorkshopUpgrade definition = purchasedDefinitions[i];
            if (definition.TechLevel > GameManager.Instance.State.TechLevel)
                throw new InvalidOperationException(
                    $"存档中的工坊升级“{definition.Id}”超出当前科技时代。");
            for (int j = 0; j < definition.RequiredResearch.Count; j++)
            {
                Research prerequisite = definition.RequiredResearch[j];
                if (prerequisite == null ||
                    !ResearchManager.Instance.IsResearchCompleted(prerequisite.Id))
                    throw new InvalidOperationException(
                        $"存档中的工坊升级“{definition.Id}”缺少研究前置。");
            }
        }

        // PurchasedUpgradeIds is a serialized set, not a topological list.
        // Validate upgrade prerequisites only after every ID has been parsed;
        // otherwise a valid save depends on the stable-ID sort order used by
        // CaptureSaveData.
        for (int i = 0; i < purchasedDefinitions.Count; i++)
        {
            WorkshopUpgrade definition = purchasedDefinitions[i];
            for (int j = 0; j < definition.RequiredUpgrades.Count; j++)
            {
                WorkshopUpgrade prerequisite = definition.RequiredUpgrades[j];
                if (prerequisite == null ||
                    string.IsNullOrWhiteSpace(prerequisite.Id) ||
                    !purchasedIds.Contains(prerequisite.Id.Trim()))
                    throw new InvalidOperationException(
                        $"存档中的工坊升级“{definition.Id}”缺少工坊前置。");
            }
        }

        ResetForLoad();
        for (int i = 0; i < purchasedDefinitions.Count; i++)
        {
            string id = purchasedDefinitions[i].Id;
            if (!TryGetStateByStableId(id, out WorkshopUpgradeState state))
                throw new InvalidOperationException(
                    $"工坊状态索引缺少已验证的升级“{id}”。");
            state.SetPurchased(true);
        }
        RebuildProgression();
    }

    private bool TryGetStateByStableId(string id,
        out WorkshopUpgradeState state)
    {
        state = null;
        if (string.IsNullOrWhiteSpace(id))
            return false;
        string stableId = id.Trim();

        // Save data and Resources lookups are keyed by stable IDs. Keep
        // runtime behavior correct if Unity returns equivalent asset
        // instances with different object references after a reload.
        foreach (KeyValuePair<WorkshopUpgrade, WorkshopUpgradeState> entry in states)
        {
            if (entry.Key != null && string.Equals(
                entry.Key.Id == null ? string.Empty : entry.Key.Id.Trim(),
                stableId,
                StringComparison.OrdinalIgnoreCase))
            {
                state = entry.Value;
                return state != null;
            }
        }
        return false;
    }

    private void EnsureStateIndex()
    {
        IReadOnlyList<WorkshopUpgrade> definitions = DataBase<WorkshopUpgrade>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            WorkshopUpgrade definition = definitions[i];
            if (definition == null ||
                TryGetStateByStableId(definition.Id, out _))
                continue;
            WorkshopUpgradeState state = new WorkshopUpgradeState(definition);
            states.Add(definition, state);
            orderedStates.Add(state);
        }
    }

    internal void ResetForLoad()
    {
        EnsureStateIndex();
        for (int i = 0; i < orderedStates.Count; i++)
            orderedStates[i].SetPurchased(false);
    }

#if UNITY_EDITOR
    public void ResetForLoadForEditor() => ResetForLoad();
    public void RestoreSaveDataForEditor(SaveManager.WorkshopSaveData data) =>
        RestoreSaveData(data);
#endif

    internal void RebuildProgression()
    {
        ProgressionModifierState previous = ProgressionModifierManager.Current;
        ProgressionModifierManager.Rebuild(
            ResearchManager.Instance.OrderedStatesForProgression,
            orderedStates);
        BuildingManager.Instance.ApplyProgressionModifierChange(
            previous,
            ProgressionModifierManager.Current);
    }
}
