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
    private readonly Dictionary<WorkshopUpgradeDefinition, WorkshopUpgradeState> states = new();
    private readonly List<WorkshopUpgradeState> orderedStates = new();

    public IReadOnlyDictionary<WorkshopUpgradeDefinition, WorkshopUpgradeState> States => states;
    internal IReadOnlyList<WorkshopUpgradeState> OrderedStates => orderedStates;
    public event Action<WorkshopUpgradeState> UpgradeStateChanged;

    protected override void Initialize()
    {
        IReadOnlyList<WorkshopUpgradeDefinition> definitions = DataBase<WorkshopUpgradeDefinition>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            var state = new WorkshopUpgradeState(definitions[i]);
            states.Add(definitions[i], state);
            orderedStates.Add(state);
        }
    }

    public bool IsSystemUnlocked =>
        ProgressionModifierManager.Current.IsSystemUnlocked(ResearchSystem.IndustrialWorkshop);

    public bool IsPurchased(WorkshopUpgradeDefinition definition) =>
        definition != null && states.TryGetValue(definition, out WorkshopUpgradeState state) && state.Purchased;

    public bool ArePrerequisitesMet(WorkshopUpgradeDefinition definition)
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
        WorkshopUpgradeDefinition definition,
        out WorkshopPurchaseFailure failure)
    {
        if (definition == null || !states.TryGetValue(definition, out WorkshopUpgradeState state))
        {
            failure = WorkshopPurchaseFailure.InvalidDefinition;
            return false;
        }
        if (!IsSystemUnlocked)
        {
            failure = WorkshopPurchaseFailure.SystemLocked;
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
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = requirements[i];
            if (ResourceManager.Instance.GetAmount(requirement.First) < requirement.Second)
            {
                failure = WorkshopPurchaseFailure.ResourceInsufficient;
                return false;
            }
        }
        for (int i = 0; i < requirements.Count; i++)
            ResourceManager.Instance.AddAmount(requirements[i].First, -requirements[i].Second);

        state.SetPurchased(true);
        RebuildProgression();
        UpgradeStateChanged?.Invoke(state);
        failure = WorkshopPurchaseFailure.None;
        return true;
    }

    internal SaveManager.WorkshopSaveData CaptureSaveData()
    {
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
        ResetForLoad();
        if (data?.PurchasedUpgradeIds != null)
        {
            for (int i = 0; i < data.PurchasedUpgradeIds.Count; i++)
            {
                if (!DataBase<WorkshopUpgradeDefinition>.TryFind(
                        data.PurchasedUpgradeIds[i],
                        out WorkshopUpgradeDefinition definition))
                {
                    Debug.LogWarning($"Ignoring retired workshop upgrade '{data.PurchasedUpgradeIds[i]}'.");
                    continue;
                }
                states[definition].SetPurchased(true);
            }
        }
        RebuildProgression();
    }

    internal void ResetForLoad()
    {
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
