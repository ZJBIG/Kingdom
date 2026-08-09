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
    InvalidDelta,
    InsufficientCampaignSupply,
    InsufficientExplorationPower,
    InvalidCampaignCost,
    CampaignNotAllowedInHomeSystem,
    ColonizationNotAllowedInInterstellarSystem,
    InterstellarSystemLocked,
    ColonizationInProgress
}

public sealed class SectorCampaignPreview
{
    public bool IsValid { get; }
    public bool HasSupply { get; }
    public ExpantaNum CurrentProgress { get; }
    public ExpantaNum EffectivePower { get; }
    public ExpantaNum CombatRatio { get; }
    public ExpantaNum FleetSurvivalFactor { get; }
    public ExpantaNum ProgressPerMinute { get; }
    public ExpantaNum CasualtiesPerMinute { get; }
    public ExpantaNum FoodCostPerMinute { get; }
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceCostsPerMinute { get; }

    internal SectorCampaignPreview(
        bool isValid,
        bool hasSupply,
        ExpantaNum currentProgress,
        ExpantaNum effectivePower,
        ExpantaNum combatRatio,
        ExpantaNum fleetSurvivalFactor,
        ExpantaNum progressPerMinute,
        ExpantaNum casualtiesPerMinute,
        ExpantaNum foodCostPerMinute,
        IReadOnlyList<Pair<Resource, ExpantaNum>> resourceCostsPerMinute)
    {
        IsValid = isValid;
        HasSupply = hasSupply;
        CurrentProgress = currentProgress;
        EffectivePower = effectivePower;
        CombatRatio = combatRatio;
        FleetSurvivalFactor = fleetSurvivalFactor;
        ProgressPerMinute = progressPerMinute;
        CasualtiesPerMinute = casualtiesPerMinute;
        FoodCostPerMinute = foodCostPerMinute;
        ResourceCostsPerMinute = resourceCostsPerMinute;
    }
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

    public SectorCampaignPreview GetCampaignPreview(
        SectorDefinition definition,
        GameState runtimeState,
        ResourceManager resourceManager)
    {
        EnsureInitialized();
        if (definition == null || runtimeState == null || !states.TryGetValue(definition, out SectorState state))
            return new SectorCampaignPreview(
                false,
                false,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                Array.Empty<Pair<Resource, ExpantaNum>>());

        ExpantaNum effectivePower = CampaignManager.CalculateEffectivePower(
            runtimeState.AttackPower,
            runtimeState.FleetPower,
            runtimeState.MilitaryManpower,
            runtimeState.SupplySatisfaction,
            runtimeState.PowerSatisfaction,
            runtimeState.LogisticsSatisfaction,
            ProgressionModifierManager.Current.MilitaryMultiplier);
        ExpantaNum combatRatio = CampaignManager.CalculateCombatRatio(effectivePower, definition.EnemyPower);
        ExpantaNum fleetSurvivalFactor = CampaignManager.CalculateFleetSurvivalFactor(
            runtimeState.DefensePower,
            definition.EnemyPower);
        ExpantaNum progressPerMinute = CampaignManager.CalculateProgressRate(combatRatio);
        ExpantaNum casualtiesPerMinute = CampaignManager.CalculateCasualtyAmount(
            combatRatio,
            runtimeState.DefensePower,
            definition.EnemyPower,
            60d);
        ExpantaNum foodCostPerMinute = ExpantaNum.Max(ExpantaNum.Zero, definition.CampaignFoodPerMinute);
        IReadOnlyList<Pair<Resource, ExpantaNum>> resourceCosts = definition.CampaignResourceCosts ??
            Array.Empty<Pair<Resource, ExpantaNum>>();
        bool hasSupply = runtimeState.FoodAmount >= foodCostPerMinute;
        for (int i = 0; i < resourceCosts.Count && hasSupply; i++)
        {
            Pair<Resource, ExpantaNum> cost = resourceCosts[i];
            hasSupply = resourceManager != null &&
                resourceManager.States.TryGetValue(cost.First, out ResourceState resourceState) &&
                resourceState.Amount >= cost.Second;
        }

        return new SectorCampaignPreview(
            true,
            hasSupply,
            state.CampaignProgress,
            effectivePower,
            combatRatio,
            fleetSurvivalFactor,
            progressPerMinute,
            casualtiesPerMinute,
            foodCostPerMinute,
            resourceCosts);
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

        if (!definition.IsHomeSystem &&
            !ProgressionModifierManager.Current.IsSystemUnlocked(ResearchSystem.FirstContact))
        {
            failure = SectorOperationFailure.InterstellarSystemLocked;
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
        if (definition.IsHomeSystem && state.CampaignProgress < ExpantaNum.One)
        {
            failure = SectorOperationFailure.CampaignRequired;
            return false;
        }
        if (!definition.IsHomeSystem && state.CampaignProgress < ExpantaNum.One)
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
        ResourceManager resourceManager = ResourceManager.Instance;
        return TryAdvanceCampaign(
            definition,
            deltaSeconds,
            runtimeState,
            resourceManager,
            out failure);
    }

    public bool TryAdvanceCampaign(
        SectorDefinition definition,
        double deltaSeconds,
        GameState runtimeState,
        ResourceManager resourceManager,
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
        if (definition.IsHomeSystem)
        {
            failure = SectorOperationFailure.CampaignNotAllowedInHomeSystem;
            return false;
        }
        if (!ProgressionModifierManager.Current.IsSystemUnlocked(ResearchSystem.DeepSpaceFleet))
        {
            failure = SectorOperationFailure.InterstellarSystemLocked;
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

        if (!TryCalculateCampaignCosts(
                definition,
                deltaSeconds,
                resourceManager,
                out ExpantaNum foodCost,
                out List<Pair<Resource, ExpantaNum>> resourceCosts,
                out failure))
            return false;
        if (runtimeState.FoodAmount < foodCost)
        {
            failure = SectorOperationFailure.InsufficientCampaignSupply;
            return false;
        }
        if (!HasResourceCosts(resourceManager, resourceCosts))
        {
            failure = SectorOperationFailure.InsufficientCampaignSupply;
            return false;
        }

        runtimeState.BeginCampaign(definition.Id);
        ConsumeCampaignCosts(runtimeState, resourceManager, foodCost, resourceCosts);
        ExpantaNum effectivePower = CampaignManager.CalculateEffectivePower(
            runtimeState.AttackPower,
            runtimeState.FleetPower,
            runtimeState.MilitaryManpower,
            runtimeState.SupplySatisfaction,
            runtimeState.PowerSatisfaction,
            runtimeState.LogisticsSatisfaction,
            ProgressionModifierManager.Current.MilitaryMultiplier);
        ExpantaNum combatRatio = CampaignManager.CalculateCombatRatio(effectivePower, definition.EnemyPower);
        ExpantaNum nextProgress = CampaignManager.AdvanceProgress(
            state.CampaignProgress,
            combatRatio,
            deltaSeconds);
        ExpantaNum casualties = CampaignManager.CalculateCasualtyAmount(
            combatRatio,
            runtimeState.DefensePower,
            definition.EnemyPower,
            deltaSeconds);
        state.SetCampaignProgress(nextProgress);
        runtimeState.RecordCampaignCombat(combatRatio, casualties);

        if (nextProgress >= ExpantaNum.One)
        {
            state.SetCampaignProgress(ExpantaNum.One);
            runtimeState.CompleteCampaign();
        }

        failure = SectorOperationFailure.None;
        return true;
    }

    public bool TryAdvanceColonization(
        SectorDefinition definition,
        double deltaSeconds,
        GameState runtimeState,
        ResourceManager resourceManager,
        out SectorOperationFailure failure)
    {
        EnsureInitialized();
        if (definition == null || !states.TryGetValue(definition, out SectorState state))
        {
            failure = SectorOperationFailure.UnknownSector;
            return false;
        }
        if (!definition.IsHomeSystem)
        {
            failure = SectorOperationFailure.ColonizationNotAllowedInInterstellarSystem;
            return false;
        }
        if (runtimeState == null || deltaSeconds < 0d)
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
        if (runtimeState.Campaign.Active)
        {
            failure = SectorOperationFailure.ColonizationInProgress;
            return false;
        }
        ExpantaNum explorationPower = ExpantaNum.Max(ExpantaNum.Zero, definition.EnemyPower);
        if (runtimeState.AttackPower < explorationPower ||
            runtimeState.DefensePower < explorationPower)
        {
            failure = SectorOperationFailure.InsufficientExplorationPower;
            return false;
        }
        if (!TryCalculateCosts(
                definition.ColonizationFoodPerMinute,
                definition.ColonizationResourceCosts,
                deltaSeconds,
                resourceManager,
                out ExpantaNum foodCost,
                out List<Pair<Resource, ExpantaNum>> resourceCosts,
                out failure))
            return false;
        if (runtimeState.FoodAmount < foodCost || !HasResourceCosts(resourceManager, resourceCosts))
        {
            failure = SectorOperationFailure.InsufficientCampaignSupply;
            return false;
        }

        state.SetColonizationActive(true);
        ConsumeCampaignCosts(runtimeState, resourceManager, foodCost, resourceCosts);
        state.SetCampaignProgress(state.CampaignProgress + deltaSeconds / 60d);
        if (state.CampaignProgress >= ExpantaNum.One)
        {
            state.SetCampaignProgress(ExpantaNum.One);
            CompleteOccupation(definition, state, runtimeState);
        }
        failure = SectorOperationFailure.None;
        return true;
    }

    public bool TickActiveCampaign(
        double deltaSeconds,
        GameState runtimeState,
        ResourceManager resourceManager,
        out SectorOperationFailure failure)
    {
        failure = SectorOperationFailure.None;
        if (runtimeState == null || !runtimeState.Campaign.Active)
            return false;
        SectorDefinition target = null;
        for (int i = 0; i < orderedStates.Count; i++)
            if (string.Equals(orderedStates[i].Definition.Id, runtimeState.Campaign.TargetSectorId, StringComparison.OrdinalIgnoreCase))
            {
                target = orderedStates[i].Definition;
                break;
            }
        if (target == null || target.IsHomeSystem)
        {
            runtimeState.CompleteCampaign();
            failure = SectorOperationFailure.CampaignNotAllowedInHomeSystem;
            return false;
        }
        return TryAdvanceCampaign(target, deltaSeconds, runtimeState, resourceManager, out failure);
    }

    public bool CancelCampaign(GameState runtimeState)
    {
        if (runtimeState == null || !runtimeState.Campaign.Active)
            return false;
        runtimeState.CompleteCampaign();
        return true;
    }

    public bool TickActiveColonization(double deltaSeconds, GameState runtimeState, ResourceManager resourceManager, out SectorOperationFailure failure)
    {
        failure = SectorOperationFailure.None;
        if (runtimeState == null || deltaSeconds < 0d)
        {
            failure = SectorOperationFailure.InvalidDelta;
            return false;
        }
        for (int i = 0; i < orderedStates.Count; i++)
        {
            SectorState state = orderedStates[i];
            if (state.ColonizationActive)
                return TryAdvanceColonization(state.Definition, deltaSeconds, runtimeState, resourceManager, out failure);
        }
        return false;
    }

    public bool CancelColonization(SectorDefinition definition)
    {
        EnsureInitialized();
        if (definition == null || !states.TryGetValue(definition, out SectorState state) || !state.ColonizationActive)
            return false;
        state.SetColonizationActive(false);
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
                ColonizationActive = state.ColonizationActive,
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

        for (int i = 0; i < orderedStates.Count; i++)
            orderedStates[i].ResetForLoad();

        for (int i = 0; i < data.States.Count; i++)
        {
            SaveManager.SectorStateSaveData saved = data.States[i];
            SectorDefinition definition = DataBase<SectorDefinition>.Find(saved.SectorId);
            SectorState state = GetState(definition);
            if (!ExpantaNum.TryParse(saved.CampaignProgress, out ExpantaNum progress))
                throw new InvalidOperationException(
                    $"Invalid sector save value '{saved.CampaignProgress}' for ID '{saved.SectorId}'.");
            state.Restore(saved.Unlocked, saved.Occupied, saved.ColonizationActive, progress, saved.VisitCount);
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

    private static bool TryCalculateCampaignCosts(
        SectorDefinition definition,
        double deltaSeconds,
        ResourceManager resourceManager,
        out ExpantaNum foodCost,
        out List<Pair<Resource, ExpantaNum>> resourceCosts,
        out SectorOperationFailure failure)
    {
        return TryCalculateCosts(
            definition.CampaignFoodPerMinute,
            definition.CampaignResourceCosts,
            deltaSeconds,
            resourceManager,
            out foodCost,
            out resourceCosts,
            out failure);
    }

    private static bool TryCalculateCosts(
        ExpantaNum foodPerMinute,
        IReadOnlyList<Pair<Resource, ExpantaNum>> configured,
        double deltaSeconds,
        ResourceManager resourceManager,
        out ExpantaNum foodCost,
        out List<Pair<Resource, ExpantaNum>> resourceCosts,
        out SectorOperationFailure failure)
    {
        foodCost = ExpantaNum.Max(ExpantaNum.Zero,
            foodPerMinute * deltaSeconds / 60d);
        resourceCosts = new List<Pair<Resource, ExpantaNum>>();
        if (foodPerMinute.IsNaN || foodPerMinute < ExpantaNum.Zero)
        {
            failure = SectorOperationFailure.InvalidCampaignCost;
            return false;
        }

        if (configured != null)
        {
            for (int i = 0; i < configured.Count; i++)
            {
                Pair<Resource, ExpantaNum> cost = configured[i];
                if (cost.First == null || cost.Second.IsNaN || cost.Second < ExpantaNum.Zero)
                {
                    failure = SectorOperationFailure.InvalidCampaignCost;
                    return false;
                }
                resourceCosts.Add(new Pair<Resource, ExpantaNum>(
                    cost.First,
                    cost.Second * deltaSeconds / 60d));
            }
        }

        if (resourceCosts.Count > 0 && resourceManager == null)
        {
            failure = SectorOperationFailure.InsufficientCampaignSupply;
            return false;
        }
        failure = SectorOperationFailure.None;
        return true;
    }

    private static bool HasResourceCosts(
        ResourceManager resourceManager,
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
    {
        if (costs == null || costs.Count == 0)
            return true;
        for (int i = 0; i < costs.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = costs[i];
            if (!resourceManager.States.TryGetValue(cost.First, out ResourceState state) ||
                state.Amount < cost.Second)
                return false;
        }
        return true;
    }

    private static void ConsumeCampaignCosts(
        GameState runtimeState,
        ResourceManager resourceManager,
        ExpantaNum foodCost,
        IReadOnlyList<Pair<Resource, ExpantaNum>> resourceCosts)
    {
        runtimeState.TryConsumeFood(foodCost);
        if (resourceCosts == null)
            return;
        for (int i = 0; i < resourceCosts.Count; i++)
            resourceManager.AddAmount(resourceCosts[i].First, -resourceCosts[i].Second);
    }

    private void CompleteOccupation(
        SectorDefinition definition,
        SectorState state,
        GameState runtimeState)
    {
        state.SetOccupied(true);
        state.SetColonizationActive(false);
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
