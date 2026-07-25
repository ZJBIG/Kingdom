using System;
using System.Collections.Generic;
using UnityEngine;

public enum SectorOperationFailure
{
    None,
    UnknownSector,
    AlreadyUnlocked,
    AlreadyOccupied,
    PrerequisiteNotOccupied,
    LaunchCenterRequired,
    InvalidReward,
    NotUnlocked,
    CampaignRequired,
    CampaignInProgress,
    InvalidDelta
}

public sealed class SectorManager
{
    private const string LaunchCenterId = "LaunchCenter";

    private readonly Dictionary<SectorDefinition, SectorState> states = new();
    private readonly List<SectorState> orderedStates = new();
    private readonly Action<SectorDefinition> rewardApplier;
    private bool initialized;

    public SectorManager() : this(ApplyRewards)
    {
    }

    public SectorManager(Action<SectorDefinition> rewardApplier)
    {
        this.rewardApplier = rewardApplier ?? throw new ArgumentNullException(nameof(rewardApplier));
    }

    public IReadOnlyDictionary<SectorDefinition, SectorState> States => states;
    public IReadOnlyList<SectorState> OrderedStates => orderedStates;

    public void InitializeDefinitions()
    {
        if (initialized)
            return;

        IReadOnlyList<SectorDefinition> definitions = DataBase<SectorDefinition>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            SectorDefinition definition = definitions[i];
            if (definition == null || states.ContainsKey(definition))
                continue;
            SectorState state = new SectorState(definition);
            states.Add(definition, state);
            InsertOrdered(state);
        }

        initialized = true;
    }

    public SectorState GetState(SectorDefinition definition)
    {
        EnsureInitialized();
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (states.TryGetValue(definition, out SectorState state))
            return state;
        throw new KeyNotFoundException($"Sector state '{definition.Id}' has not been created.");
    }

    public bool CanAccess(SectorDefinition definition)
    {
        EnsureInitialized();
        if (definition == null || !states.TryGetValue(definition, out SectorState state))
            return false;
        if (state.Occupied || state.Unlocked)
            return true;

        IReadOnlyList<SectorDefinition> prerequisites = definition.PrerequisiteSectors;
        if (prerequisites == null)
            return true;

        for (int i = 0; i < prerequisites.Count; i++)
        {
            if (!states.TryGetValue(prerequisites[i], out SectorState prerequisite) ||
                !prerequisite.Occupied)
                return false;
        }

        return true;
    }

    public bool TryUnlock(SectorDefinition definition, out SectorOperationFailure failure)
    {
        EnsureInitialized();
        if (definition == null || !states.TryGetValue(definition, out SectorState state))
        {
            failure = SectorOperationFailure.UnknownSector;
            return false;
        }
        if (state.Unlocked)
        {
            failure = SectorOperationFailure.AlreadyUnlocked;
            return false;
        }
        if (!CanAccess(definition))
        {
            failure = SectorOperationFailure.PrerequisiteNotOccupied;
            return false;
        }
        if (!HasLaunchCenter())
        {
            failure = SectorOperationFailure.LaunchCenterRequired;
            return false;
        }

        state.SetUnlocked(true);
        failure = SectorOperationFailure.None;
        return true;
    }

    public bool TryOccupy(SectorDefinition definition, out SectorOperationFailure failure)
    {
        EnsureInitialized();
        if (definition == null || !states.TryGetValue(definition, out SectorState state))
        {
            failure = SectorOperationFailure.UnknownSector;
            return false;
        }
        if (!state.Unlocked)
        {
            failure = SectorOperationFailure.NotUnlocked;
            return false;
        }
        if (state.Occupied && !definition.Repeatable)
        {
            failure = SectorOperationFailure.AlreadyOccupied;
            return false;
        }
        if (definition.EnemyPower > ExpantaNum.Zero && state.CampaignProgress < ExpantaNum.One)
        {
            failure = SectorOperationFailure.CampaignRequired;
            return false;
        }
        if (!ValidateRewards(definition))
        {
            failure = SectorOperationFailure.InvalidReward;
            return false;
        }

        state.SetOccupied(true);
        state.SetVisitCount(state.VisitCount + 1);
        rewardApplier(definition);
        failure = SectorOperationFailure.None;
        return true;
    }

    public bool TryAdvanceCampaign(
        SectorDefinition definition,
        double deltaSeconds,
        GameState runtimeState,
        out SectorOperationFailure failure)
    {
        EnsureInitialized();
        if (definition == null || !states.TryGetValue(definition, out SectorState state))
        {
            failure = SectorOperationFailure.UnknownSector;
            return false;
        }
        if (runtimeState == null)
        {
            failure = SectorOperationFailure.InvalidDelta;
            return false;
        }
        if (deltaSeconds < 0d)
        {
            failure = SectorOperationFailure.InvalidDelta;
            return false;
        }
        if (!state.Unlocked)
        {
            failure = SectorOperationFailure.NotUnlocked;
            return false;
        }
        if (state.Occupied)
        {
            failure = SectorOperationFailure.AlreadyOccupied;
            return false;
        }
        if (runtimeState.Campaign.Active &&
            !string.Equals(runtimeState.Campaign.TargetSectorId, definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            failure = SectorOperationFailure.CampaignInProgress;
            return false;
        }
        if (!ValidateRewards(definition))
        {
            failure = SectorOperationFailure.InvalidReward;
            return false;
        }

        if (definition.EnemyPower <= ExpantaNum.Zero)
        {
            state.SetCampaignProgress(ExpantaNum.One);
            CompleteOccupation(definition, state, runtimeState);
            failure = SectorOperationFailure.None;
            return true;
        }

        runtimeState.BeginCampaign(definition.Id);
        ExpantaNum effectivePower = CampaignManager.CalculateEffectivePower(
            runtimeState.AttackPower,
            runtimeState.FleetPower,
            runtimeState.MilitaryManpower,
            runtimeState.SupplySatisfaction,
            ProgressionModifierManager.Current.MilitaryMultiplier);
        ExpantaNum combatRatio = CampaignManager.CalculateCombatRatio(effectivePower, definition.EnemyPower);
        ExpantaNum nextProgress = CampaignManager.AdvanceProgress(
            state.CampaignProgress,
            combatRatio,
            deltaSeconds);
        ExpantaNum casualties = CampaignManager.CalculateCasualtyAmount(combatRatio, deltaSeconds);
        state.SetCampaignProgress(nextProgress);
        runtimeState.RecordCampaignCombat(combatRatio, casualties);

        if (nextProgress >= ExpantaNum.One)
        {
            state.SetCampaignProgress(ExpantaNum.One);
            CompleteOccupation(definition, state, runtimeState);
        }

        failure = SectorOperationFailure.None;
        return true;
    }

    public void InitializeNew()
    {
        EnsureInitialized();
        for (int i = 0; i < orderedStates.Count; i++)
            orderedStates[i].ResetForLoad();
    }

    public SaveManager.SectorSaveData CaptureSaveData()
    {
        EnsureInitialized();
        var data = new SaveManager.SectorSaveData
        {
            States = new List<SaveManager.SectorStateSaveData>(orderedStates.Count)
        };
        for (int i = 0; i < orderedStates.Count; i++)
        {
            SectorState state = orderedStates[i];
            data.States.Add(new SaveManager.SectorStateSaveData
            {
                SectorId = state.Definition.Id,
                Unlocked = state.Unlocked,
                Occupied = state.Occupied,
                CampaignProgress = state.CampaignProgress.ToString(),
                VisitCount = state.VisitCount
            });
        }
        return data;
    }

    public void ResetForLoad()
    {
        EnsureInitialized();
        for (int i = 0; i < orderedStates.Count; i++)
            orderedStates[i].ResetForLoad();
    }

    public void RestoreSaveData(SaveManager.SectorSaveData data)
    {
        EnsureInitialized();
        if (data?.States == null)
            return;

        for (int i = 0; i < data.States.Count; i++)
        {
            SaveManager.SectorStateSaveData saved = data.States[i];
            SectorDefinition definition = DataBase<SectorDefinition>.Find(saved.SectorId);
            SectorState state = GetState(definition);
            if (!ExpantaNum.TryParse(saved.CampaignProgress, out ExpantaNum progress))
                throw new InvalidOperationException(
                    $"Invalid sector save value '{saved.CampaignProgress}' for ID '{saved.SectorId}'.");
            state.Restore(saved.Unlocked, saved.Occupied, progress, saved.VisitCount);
        }
    }

    internal void AppendStateSignature(ref long hash)
    {
        EnsureInitialized();
        for (int i = 0; i < orderedStates.Count; i++)
            hash = hash * 31 + orderedStates[i].Version;
    }

    private void EnsureInitialized()
    {
        if (!initialized)
            InitializeDefinitions();
    }

    private bool HasLaunchCenter()
    {
        if (!DataBase<Building>.TryFind(LaunchCenterId, out Building launchCenter))
            return false;
        BuildingManager buildingManager = UnityEngine.Object.FindObjectOfType<BuildingManager>();
        return buildingManager != null &&
            buildingManager.States.TryGetValue(launchCenter, out BuildingState state) &&
            state.Amount >= ExpantaNum.One;
    }

    private static bool ValidateRewards(SectorDefinition definition)
    {
        if (definition.TerritoryReward.IsNaN || definition.TerritoryReward < ExpantaNum.Zero)
            return false;

        IReadOnlyList<Pair<Resource, ExpantaNum>> rewards = definition.ResourceRewards;
        if (rewards == null)
            return true;
        for (int i = 0; i < rewards.Count; i++)
        {
            Pair<Resource, ExpantaNum> reward = rewards[i];
            if (reward.First == null || reward.Second.IsNaN || reward.Second < ExpantaNum.Zero)
                return false;
        }
        return true;
    }

    private static void ApplyRewards(SectorDefinition definition)
    {
        GameManager.Instance.AdjustTerritoryTotal(definition.TerritoryReward);
        IReadOnlyList<Pair<Resource, ExpantaNum>> rewards = definition.ResourceRewards;
        if (rewards == null)
            return;
        for (int i = 0; i < rewards.Count; i++)
        {
            Pair<Resource, ExpantaNum> reward = rewards[i];
            if (reward.Second > ExpantaNum.Zero)
                ResourceManager.Instance.AddAmount(reward.First, reward.Second);
        }
    }

    private void CompleteOccupation(
        SectorDefinition definition,
        SectorState state,
        GameState runtimeState)
    {
        state.SetOccupied(true);
        state.SetVisitCount(state.VisitCount + 1);
        runtimeState.CompleteCampaign();
        rewardApplier(definition);
    }

    private void InsertOrdered(SectorState state)
    {
        int low = 0;
        int high = orderedStates.Count;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (string.Compare(
                    orderedStates[middle].Definition.Id,
                    state.Definition.Id,
                    StringComparison.OrdinalIgnoreCase) < 0)
                low = middle + 1;
            else
                high = middle;
        }
        orderedStates.Insert(low, state);
    }
}
