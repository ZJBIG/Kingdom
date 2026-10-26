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
    ResourceInsufficient,
    BuildingMissing,
    SectorNotOccupied
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
    public BuildingDef Building { get; }
    public ResourceDef Resource { get; }
    public WorkshopBenefitRateKind Kind { get; }
    public ExpantaNum Before { get; }
    public ExpantaNum After { get; }
    public ExpantaNum Change => After - Before;

    public WorkshopBenefitRatePreview(BuildingDef building, ResourceDef resource,
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
    private readonly Dictionary<WorkshopUpgradeDef, WorkshopUpgradeState> states = new();
    private readonly List<WorkshopUpgradeState> orderedStates = new();
    private ProgressionModifierState previewSource;
    private WorkshopUpgradeDef previewDefinition;
    private ProgressionModifierState previewModifiers;

    public IReadOnlyDictionary<WorkshopUpgradeDef, WorkshopUpgradeState> States => states;
    internal IReadOnlyList<WorkshopUpgradeState> OrderedStates => orderedStates;
    public event Action<WorkshopUpgradeState> UpgradeStateChanged;

    protected override void Initialize()
    {
        IReadOnlyList<WorkshopUpgradeDef> definitions = DataBase<WorkshopUpgradeDef>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            var state = new WorkshopUpgradeState(definitions[i]);
            states.Add(definitions[i], state);
            orderedStates.Add(state);
        }
    }

    public bool IsSystemUnlocked =>
        ProgressionModifierManager.Current.IsSystemUnlocked(ResearchSystem.IndustrialWorkshop);

    public bool IsPurchased(WorkshopUpgradeDef definition) =>
        TryGetStateByStableId(definition?.Id, out WorkshopUpgradeState state) && state.Purchased;

    public bool TryCraftRelicSupport(out RelicOperationFailure failure)
    {
        if (!IsSystemUnlocked)
        {
            failure = RelicOperationFailure.PrerequisiteResearchMissing;
            return false;
        }
        return GameManager.Instance.Relic.TryCraftSupport(out failure);
    }

    public IReadOnlyList<WorkshopBenefitRatePreview> GetPurchaseBenefitPreview(WorkshopUpgradeDef definition)
    {
        if (!TryGetStateByStableId(definition?.Id, out WorkshopUpgradeState state))
            return Array.Empty<WorkshopBenefitRatePreview>();
        ProgressionModifierState current = ProgressionModifierManager.Current;
        // Research, purchases and save restore replace Current; dynamic rates are never cached.
        if (!ReferenceEquals(previewSource, current) || previewDefinition != state.Definition)
        {
            previewModifiers = ProgressionModifierManager.BuildPreview(
                ResearchManager.Instance.OrderedStatesForProgression, orderedStates, state.Definition);
            previewSource = current;
            previewDefinition = state.Definition;
        }
        return CalculateBenefitPreview(BuildingManager.Instance.OrderedStates,
            current, previewModifiers, ResourceManager.GetHappinessRewardMultiplier(),
            GameManager.Instance.State.Events.ProductionMultiplier,
            ResearchManager.Instance.GlobalEfficiencyFactor * GameManager.Instance.State.HappinessMultiplier);
    }

    // Hold quantities and efficiencies fixed; the next tick will recalculate input satisfaction.
    public static IReadOnlyList<WorkshopBenefitRatePreview> CalculateBenefitPreview(
        IReadOnlyList<BuildingState> buildings, ProgressionModifierState before,
        ProgressionModifierState after) => CalculateBenefitPreview(buildings, before, after, ExpantaNum.One);

    public static IReadOnlyList<WorkshopBenefitRatePreview> CalculateBenefitPreview(
        IReadOnlyList<BuildingState> buildings, ProgressionModifierState before,
        ProgressionModifierState after, ExpantaNum productionRewardMultiplier) =>
        CalculateBenefitPreview(buildings, before, after, productionRewardMultiplier, ExpantaNum.One);

    public static IReadOnlyList<WorkshopBenefitRatePreview> CalculateBenefitPreview(
        IReadOnlyList<BuildingState> buildings, ProgressionModifierState before,
        ProgressionModifierState after, ExpantaNum productionRewardMultiplier,
        ExpantaNum eventProductionMultiplier, ExpantaNum? researchRateMultiplier = null)
    {
        var result = new List<WorkshopBenefitRatePreview>();
        ExpantaNum researchRate = researchRateMultiplier ?? ExpantaNum.One;
        AddRate(null, null, WorkshopBenefitRateKind.ResearchPower,
            ResearchManager.BaseResearchPower * before.GlobalResearchMultiplier * researchRate,
            ResearchManager.BaseResearchPower * after.GlobalResearchMultiplier * researchRate);
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
            BuildingDef building = state.Definition;
            ExpantaNum scale = state.Amount * state.Efficiency;
            ExpantaNum oldProduction = before.GetBuildingProductionMultiplier(building) *
                before.GlobalBuildingProductionMultiplier;
            ExpantaNum newProduction = after.GetBuildingProductionMultiplier(building) *
                after.GlobalBuildingProductionMultiplier;
            foreach (Pair<ResourceDef, ExpantaNum> rate in building.ResourceGenerationRates)
                AddRate(building, rate.First, WorkshopBenefitRateKind.ResourceProduction,
                    scale * rate.Second * oldProduction * productionRewardMultiplier * eventProductionMultiplier *
                    before.GetBuildingResourceProductionMultiplier(building, rate.First) *
                    before.GetResourceProductionMultiplier(rate.First),
                    scale * rate.Second * newProduction * productionRewardMultiplier * eventProductionMultiplier *
                    after.GetBuildingResourceProductionMultiplier(building, rate.First) *
                    after.GetResourceProductionMultiplier(rate.First));
            foreach (Pair<ResourceDef, ExpantaNum> rate in building.ResourceConsumptionRates)
                AddRate(building, rate.First, WorkshopBenefitRateKind.ResourceConsumption,
                    scale * rate.Second * oldProduction, scale * rate.Second * newProduction);
            AddRate(building, null, WorkshopBenefitRateKind.FoodProduction,
                scale * building.FoodProductionRate * oldProduction * eventProductionMultiplier * before.GlobalFoodProductionMultiplier,
                scale * building.FoodProductionRate * newProduction * eventProductionMultiplier * after.GlobalFoodProductionMultiplier);
            AddRate(building, null, WorkshopBenefitRateKind.ResearchPower,
                scale * building.ResearchPowerGranted * before.GetBuildingResearchPowerMultiplier(building) *
                before.GlobalResearchMultiplier * researchRate,
                scale * building.ResearchPowerGranted * after.GetBuildingResearchPowerMultiplier(building) *
                after.GlobalResearchMultiplier * researchRate);
            AddRate(building, null, WorkshopBenefitRateKind.PowerProduction,
                scale * ExpantaNum.Max(ExpantaNum.Zero, building.PowerContribution) * before.PowerMultiplier *
                before.GetBuildingPowerProductionMultiplier(building) * productionRewardMultiplier,
                scale * ExpantaNum.Max(ExpantaNum.Zero, building.PowerContribution) * after.PowerMultiplier *
                after.GetBuildingPowerProductionMultiplier(building) * productionRewardMultiplier);
            AddRate(building, null, WorkshopBenefitRateKind.LogisticsProduction,
                scale * ExpantaNum.Max(ExpantaNum.Zero, building.LogisticsContribution) * before.GlobalLogisticsMultiplier *
                before.GetBuildingLogisticsProductionMultiplier(building) * productionRewardMultiplier,
                scale * ExpantaNum.Max(ExpantaNum.Zero, building.LogisticsContribution) * after.GlobalLogisticsMultiplier *
                after.GetBuildingLogisticsProductionMultiplier(building) * productionRewardMultiplier);
        }
        return result;

        void AddRate(BuildingDef building, ResourceDef resource, WorkshopBenefitRateKind kind,
            ExpantaNum oldRate, ExpantaNum newRate)
        {
            if (oldRate != newRate)
                result.Add(new WorkshopBenefitRatePreview(building, resource, kind, oldRate, newRate));
        }
    }

    public bool ArePrerequisitesMet(WorkshopUpgradeDef definition)
    {
        if (!TryGetStateByStableId(definition?.Id, out WorkshopUpgradeState state))
            return false;
        return GetPrerequisiteFailure(state.Definition) == WorkshopPurchaseFailure.None;
    }

    private WorkshopPurchaseFailure GetPrerequisiteFailure(WorkshopUpgradeDef definition)
    {
        for (int i = 0; i < definition.RequiredResearch.Count; i++)
            if (!ResearchManager.Instance.IsResearchCompleted(definition.RequiredResearch[i].Id))
                return WorkshopPurchaseFailure.ResearchIncomplete;
        for (int i = 0; i < definition.RequiredUpgrades.Count; i++)
            if (!IsPurchased(definition.RequiredUpgrades[i]))
                return WorkshopPurchaseFailure.UpgradeIncomplete;
        if (definition.RequiredBuilding != null &&
            (!BuildingManager.Instance.States.TryGetValue(definition.RequiredBuilding, out BuildingState building) ||
                building.Amount < ExpantaNum.One))
            return WorkshopPurchaseFailure.BuildingMissing;
        if (definition.RequiredSector != null &&
            !GameManager.Instance.Sectors.GetState(definition.RequiredSector).Occupied)
            return WorkshopPurchaseFailure.SectorNotOccupied;
        return WorkshopPurchaseFailure.None;
    }

    public WorkshopPurchaseFailure GetPurchaseFailure(WorkshopUpgradeDef definition) =>
        GetPurchaseFailure(definition, out _, out _);

    private WorkshopPurchaseFailure GetPurchaseFailure(WorkshopUpgradeDef definition,
        out WorkshopUpgradeState state, out Dictionary<ResourceDef, ExpantaNum> costs)
    {
        costs = null;
        if (!TryGetStateByStableId(definition?.Id, out state))
            return WorkshopPurchaseFailure.InvalidDefinition;
        definition = state.Definition;
        if (!IsSystemUnlocked)
            return WorkshopPurchaseFailure.SystemLocked;
        if (definition.TechLevel > GameManager.Instance.State.TechLevel)
            return WorkshopPurchaseFailure.TechnologyInsufficient;
        if (state.Purchased)
            return WorkshopPurchaseFailure.AlreadyPurchased;
        WorkshopPurchaseFailure failure = GetPrerequisiteFailure(definition);
        if (failure != WorkshopPurchaseFailure.None)
            return failure;
        costs = new Dictionary<ResourceDef, ExpantaNum>();
        foreach (Pair<ResourceDef, ExpantaNum> requirement in definition.ResourceRequirements)
        {
            if (requirement.First == null || !requirement.Second.IsFinite)
                return WorkshopPurchaseFailure.InvalidDefinition;
            if (requirement.Second <= ExpantaNum.Zero)
                continue;
            costs[requirement.First] = costs.TryGetValue(requirement.First, out ExpantaNum current)
                ? current + requirement.Second : requirement.Second;
        }
        foreach (KeyValuePair<ResourceDef, ExpantaNum> cost in costs)
            if (ResourceManager.Instance.GetAmount(cost.Key) < cost.Value)
                return WorkshopPurchaseFailure.ResourceInsufficient;
        return WorkshopPurchaseFailure.None;
    }

    public bool TryPurchase(
        WorkshopUpgradeDef definition,
        out WorkshopPurchaseFailure failure)
    {
        failure = GetPurchaseFailure(definition, out WorkshopUpgradeState state,
            out Dictionary<ResourceDef, ExpantaNum> costs);
        if (failure != WorkshopPurchaseFailure.None)
            return false;

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
        ObserverNotifications.Publish(UpgradeStateChanged, state);
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
        var purchasedDefinitions = new List<WorkshopUpgradeDef>(data.PurchasedUpgradeIds.Count);
        for (int i = 0; i < data.PurchasedUpgradeIds.Count; i++)
        {
            string id = data.PurchasedUpgradeIds[i];
            if (string.IsNullOrWhiteSpace(id) ||
                !DataBase<WorkshopUpgradeDef>.TryFind(id, out WorkshopUpgradeDef definition))
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
            WorkshopUpgradeDef definition = purchasedDefinitions[i];
            if (definition.TechLevel > GameManager.Instance.State.TechLevel)
                throw new InvalidOperationException(
                    $"存档中的工坊升级“{definition.Id}”超出当前科技时代。");
            for (int j = 0; j < definition.RequiredResearch.Count; j++)
            {
                ResearchDef prerequisite = definition.RequiredResearch[j];
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
            WorkshopUpgradeDef definition = purchasedDefinitions[i];
            for (int j = 0; j < definition.RequiredUpgrades.Count; j++)
            {
                WorkshopUpgradeDef prerequisite = definition.RequiredUpgrades[j];
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

    internal void ValidateSectorPrerequisites(SectorManager sectors)
    {
        for (int i = 0; i < orderedStates.Count; i++)
        {
            WorkshopUpgradeState state = orderedStates[i];
            SectorDef sector = state.Definition.RequiredSector;
            if (state.Purchased && sector != null && !sectors.GetState(sector).Occupied)
                throw new InvalidOperationException(
                    $"存档中的工坊升级“{state.Definition.Id}”缺少已占领星区“{sector.Id}”。");
            if (state.Purchased && state.Definition.RequiredBuilding is SectorBuildingDef local &&
                local.RequiresInteriorExploration && !sectors.GetState(local.Sector).InteriorExplorationCompleted)
                throw new InvalidOperationException(
                    $"存档中的工坊升级“{state.Definition.Id}”缺少已完成星球探索“{local.Sector.Id}”。");
        }
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
        foreach (KeyValuePair<WorkshopUpgradeDef, WorkshopUpgradeState> entry in states)
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
        IReadOnlyList<WorkshopUpgradeDef> definitions = DataBase<WorkshopUpgradeDef>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            WorkshopUpgradeDef definition = definitions[i];
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
