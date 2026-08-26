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
        TryGetState(definition, out WorkshopUpgradeState state) && state.Purchased;

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
        if (!TryGetState(definition, out WorkshopUpgradeState state))
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

        bool paid = ResourceManager.Instance.TryApplyAtomicPayment(
            costs,
            () =>
            {
                state.SetPurchased(true);
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

        var purchased = new HashSet<WorkshopUpgrade>();
        for (int i = 0; i < data.PurchasedUpgradeIds.Count; i++)
        {
            string id = data.PurchasedUpgradeIds[i];
            if (string.IsNullOrWhiteSpace(id) ||
                !DataBase<WorkshopUpgrade>.TryFind(id, out WorkshopUpgrade definition))
                throw new InvalidOperationException(
                    $"存档中的工坊升级 ID 无效：“{id}”。");
            if (!purchased.Add(definition))
                throw new InvalidOperationException(
                    $"存档中的工坊升级重复包含“{definition.Id}”。");
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
        foreach (WorkshopUpgrade definition in purchased)
        {
            for (int j = 0; j < definition.RequiredUpgrades.Count; j++)
            {
                WorkshopUpgrade prerequisite = definition.RequiredUpgrades[j];
                if (prerequisite == null || !purchased.Contains(prerequisite))
                    throw new InvalidOperationException(
                        $"存档中的工坊升级“{definition.Id}”缺少工坊前置。");
            }
        }

        ResetForLoad();
        foreach (WorkshopUpgrade definition in purchased)
        {
            if (!TryGetState(definition, out WorkshopUpgradeState state))
                throw new InvalidOperationException(
                    $"工坊状态索引缺少已验证的升级“{definition.Id}”。");
            state.SetPurchased(true);
        }
        RebuildProgression();
    }

    private bool TryGetState(WorkshopUpgrade definition,
        out WorkshopUpgradeState state)
    {
        state = null;
        if (definition == null)
            return false;
        if (states.TryGetValue(definition, out state))
            return true;

        // Save data and Resources lookups are keyed by stable IDs. Keep
        // runtime behavior correct if Unity returns equivalent asset
        // instances with different object references after a reload.
        foreach (KeyValuePair<WorkshopUpgrade, WorkshopUpgradeState> entry in states)
        {
            if (entry.Key != null && string.Equals(
                entry.Key.Id == null ? string.Empty : entry.Key.Id.Trim(),
                definition.Id == null ? string.Empty : definition.Id.Trim(),
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
            if (definition == null || TryGetState(definition, out _))
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
