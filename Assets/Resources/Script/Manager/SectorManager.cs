using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

public enum SectorOperationFailure
{
    None,
    UnknownSector,
    AlreadyUnlocked,
    AlreadyOccupied,
    PrerequisiteNotOccupied,
    LaunchCenterRequired,
    HomeSystemSurveyRequired,
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
    ColonizationInProgress,
    NoFleetDamage,
    InvalidRepairAmount,
    InsufficientFleetRepairSupply,
    FleetRepairRequired
}

public sealed class SectorCampaignPreview
{
    public bool IsValid { get; }
    public bool HasSupply { get; }
    public ExpantaNum CurrentProgress { get; }
    public ExpantaNum EffectivePower { get; }
    public ExpantaNum CombatRatio { get; }
    public ExpantaNum FleetSurvivalFactor { get; }
    public ExpantaNum FleetReadiness { get; }
    public ExpantaNum CurrentCasualties { get; }
    public ExpantaNum EstimatedRepairAmount { get; }
    public IReadOnlyList<Pair<Resource, ExpantaNum>> FleetRepairCosts { get; }
    public ExpantaNum SupplySatisfaction { get; }
    public ExpantaNum PowerSatisfaction { get; }
    public ExpantaNum LogisticsSatisfaction { get; }
    public ExpantaNum ProgressPerSecond { get; }
    public ExpantaNum EstimatedSecondsRemaining { get; }
    public ExpantaNum CasualtiesPerSecond { get; }
    public ExpantaNum FoodCostPerSecond { get; }
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceCostsPerSecond { get; }
    public bool HasOngoingSupplyCost { get; }
    public ExpantaNum EstimatedSupplySeconds { get; }
    internal SectorCampaignPreview(
        bool isValid,
        bool hasSupply,
        ExpantaNum currentProgress,
        ExpantaNum effectivePower,
        ExpantaNum combatRatio,
        ExpantaNum fleetSurvivalFactor,
        ExpantaNum fleetReadiness,
        ExpantaNum currentCasualties,
        ExpantaNum estimatedRepairAmount,
        IReadOnlyList<Pair<Resource, ExpantaNum>> fleetRepairCosts,
        ExpantaNum supplySatisfaction,
        ExpantaNum powerSatisfaction,
        ExpantaNum logisticsSatisfaction,
        ExpantaNum progressPerSecond,
        ExpantaNum casualtiesPerSecond,
        ExpantaNum foodCostPerSecond,
        IReadOnlyList<Pair<Resource, ExpantaNum>> resourceRatesPerSecond,
        bool hasOngoingSupplyCost,
        ExpantaNum estimatedSupplySeconds)
    {
        IsValid = isValid;
        HasSupply = hasSupply;
        CurrentProgress = currentProgress;
        EffectivePower = effectivePower;
        CombatRatio = combatRatio;
        FleetSurvivalFactor = fleetSurvivalFactor;
        FleetReadiness = fleetReadiness;
        CurrentCasualties = currentCasualties;
        EstimatedRepairAmount = estimatedRepairAmount;
        FleetRepairCosts = fleetRepairCosts;
        SupplySatisfaction = supplySatisfaction;
        PowerSatisfaction = powerSatisfaction;
        LogisticsSatisfaction = logisticsSatisfaction;
        ProgressPerSecond = progressPerSecond;
        ExpantaNum remainingProgress = ExpantaNum.One - ExpantaNum.Clamp01(currentProgress);
        EstimatedSecondsRemaining = progressPerSecond > ExpantaNum.Zero
            ? remainingProgress / progressPerSecond
            : ExpantaNum.Zero;
        CasualtiesPerSecond = casualtiesPerSecond;
        FoodCostPerSecond = foodCostPerSecond;
        ResourceCostsPerSecond = resourceRatesPerSecond;
        HasOngoingSupplyCost = hasOngoingSupplyCost;
        EstimatedSupplySeconds = estimatedSupplySeconds;
    }
}

public sealed class SectorExplorationPreview
{
    public bool IsValid { get; }
    public bool HasSupply { get; }
    public bool IsActive { get; }
    public ExpantaNum CurrentProgress { get; }
    public ExpantaNum ExplorationPower { get; }
    public ExpantaNum RequiredPower { get; }
    public ExpantaNum ProgressPerSecond { get; }
    public ExpantaNum EstimatedSecondsRemaining { get; }
    public ExpantaNum FoodCostPerSecond { get; }
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceCostsPerSecond { get; }



    internal SectorExplorationPreview(
        bool isValid,
        bool hasSupply,
        bool isActive,
        ExpantaNum currentProgress,
        ExpantaNum explorationPower,
        ExpantaNum requiredPower,
        ExpantaNum colonizationDurationSeconds,
        ExpantaNum explorationSpeedMultiplier,
        ExpantaNum foodCostPerSecond,
        IReadOnlyList<Pair<Resource, ExpantaNum>> resourceRatesPerSecond)
    {
        IsValid = isValid;
        HasSupply = hasSupply;
        IsActive = isActive;
        CurrentProgress = currentProgress;
        ExplorationPower = explorationPower;
        RequiredPower = requiredPower;
        ExpantaNum duration = ExpantaNum.Max(ExpantaNum.One, colonizationDurationSeconds);
        ExpantaNum speedMultiplier = explorationSpeedMultiplier.IsFinite &&
            explorationSpeedMultiplier > ExpantaNum.Zero
            ? explorationSpeedMultiplier
            : ExpantaNum.One;
        ProgressPerSecond = speedMultiplier / duration;
        EstimatedSecondsRemaining = ExpantaNum.Max(
            ExpantaNum.Zero,
            ExpantaNum.One - ExpantaNum.Clamp01(currentProgress)) / ProgressPerSecond;
        FoodCostPerSecond = foodCostPerSecond;
        ResourceCostsPerSecond = resourceRatesPerSecond;
    }
}

public sealed class SectorManager
{
    private sealed class SectorSnapshot
    {
        public SectorState State;
        public bool Unlocked;
        public bool Occupied;
        public bool ColonizationActive;
        public bool CampaignActive;
        public ExpantaNum Progress;
        public ExpantaNum Casualties;
        public ExpantaNum CombatRatio;
        public int VisitCount;
    }

    private sealed class SectorRestoreData
    {
        public SectorDefinition Definition;
        public bool Unlocked;
        public bool Occupied;
        public bool ColonizationActive;
        public bool CampaignActive;
        public ExpantaNum Progress;
        public ExpantaNum Casualties;
        public ExpantaNum CombatRatio;
        public int VisitCount;
    }

    private const string LaunchCenterId = "LaunchCenter";

    private readonly Dictionary<SectorDefinition, SectorState> states = new();
    private readonly List<SectorState> orderedStates = new();
    private readonly Dictionary<Resource, ExpantaNum> occupiedProductionBuffer = new();
    private readonly List<Pair<Resource, ExpantaNum>> campaignResourceCostBuffer = new();
    private readonly Dictionary<Resource, ExpantaNum> campaignCostAggregationBuffer = new();
    private readonly Action<SectorDefinition> rewardApplier;
    private BuildingManager buildingManagerCache;
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
            if (definition == null || TryGetStateByStableId(definition, out _))
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
        if (TryGetStateByStableId(definition, out SectorState state))
            return state;
        throw new KeyNotFoundException($"星区状态“{definition.Id}”尚未创建。");
    }

    private bool TryGetStateByStableId(
        SectorDefinition definition,
        out SectorState state)
    {
        state = null;
        if (definition == null)
            return false;
        if (states.TryGetValue(definition, out state))
            return true;
        foreach (KeyValuePair<SectorDefinition, SectorState> entry in states)
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

    public SectorCampaignPreview GetCampaignPreview(
        SectorDefinition definition,
        GameState runtimeState,
        ResourceManager resourceManager)
    {
        EnsureInitialized();
        if (definition == null || runtimeState == null ||
            !TryGetStateByStableId(definition, out SectorState state))
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
                Array.Empty<Pair<Resource, ExpantaNum>>(),
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                Array.Empty<Pair<Resource, ExpantaNum>>(),
                false,
                ExpantaNum.Zero);

        ExpantaNum combatRatio = CalculateCampaignCombatRatio(
            definition, state, runtimeState);
        ExpantaNum effectivePower = CalculateCampaignEffectivePower(
            state, runtimeState);
        ExpantaNum fleetSurvivalFactor = CampaignManager.CalculateFleetSurvivalFactor(
            runtimeState.DefensePower,
            definition.EnemyPower);
        ExpantaNum fleetReadiness = CampaignManager.CalculateFleetReadiness(
            runtimeState.FleetPower,
            state.CampaignCasualties);
        ExpantaNum progressPerSecond = CampaignManager.CalculateProgressRate(combatRatio) *
            definition.CampaignProgressMultiplier *
            ProgressionModifierManager.Current.CampaignProgressMultiplier *
            GetCampaignDoctrineProgressMultiplier(runtimeState);
        ExpantaNum casualtiesPerSecond = CampaignManager.CalculateCasualtyAmount(
            combatRatio,
            runtimeState.DefensePower,
            definition.EnemyPower,
            1d,
            ProgressionModifierManager.Current.CampaignCasualtyMultiplier *
            GetCampaignDoctrineCasualtyMultiplier(runtimeState));
        ExpantaNum supplyCostMultiplier =
            ProgressionModifierManager.Current.CampaignSupplyCostMultiplier *
            GetCampaignDoctrineSupplyMultiplier(runtimeState) *
            GetRelicCampaignSupplyMultiplier(definition, runtimeState);
        ExpantaNum foodCostPerSecond = ExpantaNum.Max(
            ExpantaNum.Zero,
            definition.CampaignFoodPerSecond * supplyCostMultiplier);
        IReadOnlyList<Pair<Resource, ExpantaNum>> resourceCosts =
            ScaleCampaignResourceRates(
                definition.CampaignResourceRatesPerSecond,
                supplyCostMultiplier);
        bool hasSupply = runtimeState.FoodAmount >= foodCostPerSecond;
        for (int i = 0; i < resourceCosts.Count && hasSupply; i++)
        {
            Pair<Resource, ExpantaNum> cost = resourceCosts[i];
            hasSupply = resourceManager != null &&
                resourceManager.States.TryGetValue(cost.First, out ResourceState resourceState) &&
                resourceState.Amount >= cost.Second;
        }
        ExpantaNum estimatedSupplySeconds = CalculateSupplyDuration(
            runtimeState,
            resourceManager,
            foodCostPerSecond,
            resourceCosts,
            out bool hasOngoingSupplyCost);
        ExpantaNum estimatedRepairAmount = ExpantaNum.Zero;
        IReadOnlyList<Pair<Resource, ExpantaNum>> fleetRepairCosts =
            Array.Empty<Pair<Resource, ExpantaNum>>();
        if (string.Equals(
                definition.Id,
                runtimeState.Campaign.TargetSectorId,
                StringComparison.OrdinalIgnoreCase) &&
            state.CampaignCasualties > ExpantaNum.Zero &&
            runtimeState.Campaign.Casualties > ExpantaNum.Zero)
        {
            estimatedRepairAmount = ExpantaNum.Min(
                state.CampaignCasualties,
                runtimeState.Campaign.Casualties);
            if (estimatedRepairAmount > ExpantaNum.Zero)
                fleetRepairCosts = CalculateFleetRepairCosts(estimatedRepairAmount);
        }

        return new SectorCampaignPreview(
            true,
            hasSupply,
            state.CampaignProgress,
            effectivePower,
            combatRatio,
            fleetSurvivalFactor,
            fleetReadiness,
            state.CampaignCasualties,
            estimatedRepairAmount,
            fleetRepairCosts,
            runtimeState.SupplySatisfaction,
            runtimeState.PowerSatisfaction,
            runtimeState.LogisticsSatisfaction,
            progressPerSecond,
            casualtiesPerSecond,
            foodCostPerSecond,
            resourceCosts,
            hasOngoingSupplyCost,
            estimatedSupplySeconds);
    }

    public SectorExplorationPreview GetExplorationPreview(
        SectorDefinition definition,
        GameState runtimeState,
        ResourceManager resourceManager)
    {
        EnsureInitialized();
        if (definition == null || runtimeState == null ||
            !definition.IsHomeSystem || !TryGetStateByStableId(definition, out SectorState state))
            return new SectorExplorationPreview(
                false,
                false,
                false,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.One,
                ExpantaNum.One,
                ExpantaNum.Zero,
                Array.Empty<Pair<Resource, ExpantaNum>>());

        // Home-system exploration is a supplied survey operation, not a combat
        // gate. Keep the preview focused on its resource costs.
        ExpantaNum requiredPower = ExpantaNum.Zero;
        ExpantaNum explorationPower = ExpantaNum.Zero;
        ExpantaNum explorationSpeedMultiplier =
            ProgressionModifierManager.Current.ExplorationPowerMultiplier;
        ExpantaNum foodCostPerSecond = ExpantaNum.Max(
            ExpantaNum.Zero,
            definition.ColonizationFoodPerSecond);
        IReadOnlyList<Pair<Resource, ExpantaNum>> resourceCosts =
            definition.ColonizationResourceRatesPerSecond ?? Array.Empty<Pair<Resource, ExpantaNum>>();
        bool hasSupply = runtimeState.FoodAmount >= foodCostPerSecond;
        for (int i = 0; i < resourceCosts.Count && hasSupply; i++)
        {
            Pair<Resource, ExpantaNum> cost = resourceCosts[i];
            hasSupply = resourceManager != null &&
                cost.First != null &&
                resourceManager.States.TryGetValue(cost.First, out ResourceState resourceState) &&
                resourceState.Amount >= cost.Second;
        }
        return new SectorExplorationPreview(
            true,
            hasSupply,
            state.ColonizationActive,
            state.CampaignProgress,
            explorationPower,
            requiredPower,
            definition.ColonizationDurationSeconds,
            explorationSpeedMultiplier,
            foodCostPerSecond,
            resourceCosts);
    }

    private static ExpantaNum CalculateSupplyDuration(
        GameState runtimeState,
        ResourceManager resourceManager,
        ExpantaNum foodCostPerSecond,
        IReadOnlyList<Pair<Resource, ExpantaNum>> resourceCosts,
        out bool hasOngoingSupplyCost)
    {
        hasOngoingSupplyCost = false;
        ExpantaNum duration = ExpantaNum.Zero;
        if (foodCostPerSecond > ExpantaNum.Zero)
        {
            duration = ExpantaNum.Max(
                ExpantaNum.Zero,
                runtimeState.FoodAmount / foodCostPerSecond);
            hasOngoingSupplyCost = true;
        }

        for (int i = 0; resourceCosts != null && i < resourceCosts.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = resourceCosts[i];
            if (cost.First == null || cost.Second <= ExpantaNum.Zero)
                continue;
            ExpantaNum amount = ExpantaNum.Zero;
            if (resourceManager != null &&
                resourceManager.States.TryGetValue(cost.First, out ResourceState resourceState))
                amount = resourceState.Amount;
            ExpantaNum resourceDuration = ExpantaNum.Max(
                ExpantaNum.Zero,
                amount / cost.Second);
            duration = hasOngoingSupplyCost
                ? ExpantaNum.Min(duration, resourceDuration)
                : resourceDuration;
            hasOngoingSupplyCost = true;
        }

        return duration;
    }

    public bool CanAccess(SectorDefinition definition)
    {
        EnsureInitialized();
        if (definition == null || !TryGetStateByStableId(definition, out SectorState state))
            return false;
        if (state.Occupied || state.Unlocked)
            return true;

        IReadOnlyList<SectorDefinition> prerequisites = definition.PrerequisiteSectors;
        if (prerequisites == null)
            return true;

        for (int i = 0; i < prerequisites.Count; i++)
        {
            if (!TryGetStateByStableId(prerequisites[i], out SectorState prerequisite) ||
                !prerequisite.Occupied)
                return false;
        }

        return true;
    }

    public SectorOperationFailure GetUnlockFailure(SectorDefinition definition)
    {
        EnsureInitialized();
        if (definition == null || !TryGetStateByStableId(definition, out SectorState state))
            return SectorOperationFailure.UnknownSector;
        if (state.Unlocked)
            return SectorOperationFailure.AlreadyUnlocked;
        if (!CanAccess(definition))
            return SectorOperationFailure.PrerequisiteNotOccupied;
        if (definition.IsHomeSystem &&
            !ProgressionModifierManager.Current.IsSystemUnlocked(ResearchSystem.HomeSystemSurvey))
            return SectorOperationFailure.HomeSystemSurveyRequired;
        if (!HasLaunchCenter())
            return SectorOperationFailure.LaunchCenterRequired;
        if (!definition.IsHomeSystem && !IsInterstellarRouteUnlocked())
            return SectorOperationFailure.InterstellarSystemLocked;
        return SectorOperationFailure.None;
    }

    public bool TryUnlock(SectorDefinition definition, out SectorOperationFailure failure)
    {
        failure = GetUnlockFailure(definition);
        if (failure != SectorOperationFailure.None)
            return false;
        GetState(definition).SetUnlocked(true);
        return true;
    }

    public bool TryOccupy(SectorDefinition definition, out SectorOperationFailure failure)
    {
        EnsureInitialized();
        if (definition == null || !TryGetStateByStableId(definition, out SectorState state))
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

        bool previousOccupied = state.Occupied;
        int previousVisitCount = state.VisitCount;
        state.SetOccupied(true);
        state.SetVisitCount(state.VisitCount + 1);
        try
        {
            rewardApplier(definition);
        }
        catch
        {
            state.SetOccupied(previousOccupied);
            state.SetVisitCount(previousVisitCount);
            throw;
        }
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
        if (definition == null || !TryGetStateByStableId(definition, out SectorState state))
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
        if (!IsInterstellarRouteUnlocked() ||
            !ProgressionModifierManager.Current.IsSystemUnlocked(ResearchSystem.DeepSpaceFleet))
        {
            failure = SectorOperationFailure.InterstellarSystemLocked;
            return false;
        }
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
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
        if (!runtimeState.Campaign.Active &&
            runtimeState.Campaign.Casualties > ExpantaNum.Zero)
        {
            failure = SectorOperationFailure.FleetRepairRequired;
            return false;
        }
        if (runtimeState.Campaign.Active &&
            !string.Equals(
                runtimeState.Campaign.TargetSectorId,
                definition.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            failure = SectorOperationFailure.CampaignInProgress;
            return false;
        }
        if (!ValidateRewards(definition))
        {
            failure = SectorOperationFailure.InvalidReward;
            return false;
        }

        ExpantaNum combatRatio = CalculateCampaignCombatRatio(
            definition,
            state,
            runtimeState);
        ExpantaNum campaignProgressMultiplier =
            definition.CampaignProgressMultiplier *
            ProgressionModifierManager.Current.CampaignProgressMultiplier *
            GetCampaignDoctrineProgressMultiplier(runtimeState);
        ExpantaNum campaignSupplyCostMultiplier =
            ProgressionModifierManager.Current.CampaignSupplyCostMultiplier *
            GetCampaignDoctrineSupplyMultiplier(runtimeState) *
            GetRelicCampaignSupplyMultiplier(definition, runtimeState);
        if (!campaignProgressMultiplier.IsFinite || campaignProgressMultiplier < ExpantaNum.Zero ||
            !campaignSupplyCostMultiplier.IsFinite || campaignSupplyCostMultiplier < ExpantaNum.Zero)
        {
            failure = SectorOperationFailure.InvalidCampaignCost;
            return false;
        }
        double billableSeconds = CalculateCampaignBillableSeconds(
            state.CampaignProgress,
            combatRatio,
            campaignProgressMultiplier,
            deltaSeconds);

        if (!TryCalculateCampaignCosts(
                definition,
                billableSeconds,
                campaignSupplyCostMultiplier,
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

        bool ownsLegacyCampaignSlot = !runtimeState.Campaign.Active;
        bool previousCampaignActive = runtimeState.Campaign.Active;
        string previousCampaignTarget = runtimeState.Campaign.TargetSectorId;
        ExpantaNum previousCampaignCasualties = runtimeState.Campaign.Casualties;
        ExpantaNum previousCampaignCombatRatio = runtimeState.Campaign.CombatRatio;
        CampaignDoctrine previousCampaignDoctrine = runtimeState.Campaign.Doctrine;
        ExpantaNum previousSectorProgress = state.CampaignProgress;
        ExpantaNum previousSectorCasualties = state.CampaignCasualties;
        ExpantaNum previousSectorCombatRatio = state.CampaignCombatRatio;
        bool previousSectorOccupied = state.Occupied;
        int previousSectorVisitCount = state.VisitCount;
        bool previousSectorCampaignActive = state.CampaignActive;
        if (!TryConsumeCampaignCosts(
                runtimeState,
                resourceManager,
                foodCost,
                resourceCosts,
                () =>
                {
                    if (ownsLegacyCampaignSlot)
                        runtimeState.BeginCampaign(definition.Id);
                    state.SetCampaignActive(true);
                    CommitCampaignAdvance(definition, state, billableSeconds, runtimeState);
                },
                () =>
                {
                    runtimeState.RestoreCampaignExact(
                        previousCampaignActive,
                        previousCampaignTarget,
                        previousCampaignCasualties,
                        previousCampaignCombatRatio,
                        previousCampaignDoctrine);
                    state.RestoreExact(
                        state.Unlocked,
                        previousSectorOccupied,
                        state.ColonizationActive,
                        previousSectorCampaignActive,
                        previousSectorProgress,
                        previousSectorCasualties,
                        previousSectorCombatRatio,
                        previousSectorVisitCount);
                }))
        {
            failure = SectorOperationFailure.InsufficientCampaignSupply;
            return false;
        }

        RefreshRelicCampaignSupport(runtimeState);
        failure = SectorOperationFailure.None;
        return true;
    }

    private bool CommitCampaignAdvance(
        SectorDefinition definition,
        SectorState state,
        double deltaSeconds,
        GameState runtimeState)
    {
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        ExpantaNum combatRatio = CalculateCampaignCombatRatio(
            definition,
            state,
            runtimeState);
        ExpantaNum nextProgress = AdvanceCampaignProgress(
            state.CampaignProgress,
            combatRatio,
            deltaSeconds,
            definition.CampaignProgressMultiplier *
                modifiers.CampaignProgressMultiplier *
                GetCampaignDoctrineProgressMultiplier(runtimeState));
        ExpantaNum casualties = CampaignManager.CalculateCasualtyAmount(
            combatRatio,
            runtimeState.DefensePower,
            definition.EnemyPower,
            deltaSeconds,
            modifiers.CampaignCasualtyMultiplier *
            GetCampaignDoctrineCasualtyMultiplier(runtimeState));
        state.SetCampaignProgress(nextProgress);
        state.SetCampaignCombatRatio(combatRatio);
        state.SetCampaignCasualties(state.CampaignCasualties + casualties);
        if (runtimeState.Campaign.Active &&
            string.Equals(runtimeState.Campaign.TargetSectorId, definition.Id, StringComparison.OrdinalIgnoreCase))
            runtimeState.RecordCampaignCombat(combatRatio, casualties);

        if (nextProgress < ExpantaNum.One)
            return true;

        state.SetCampaignProgress(ExpantaNum.One);
        state.SetCampaignActive(false);
        state.SetCampaignCasualties(ExpantaNum.Zero);
        state.SetCampaignCombatRatio(ExpantaNum.Zero);
        state.SetOccupied(true);
        state.SetVisitCount(state.VisitCount + 1);
        if (runtimeState.Campaign.Active &&
            string.Equals(runtimeState.Campaign.TargetSectorId, definition.Id, StringComparison.OrdinalIgnoreCase))
            runtimeState.CompleteCampaign();
        rewardApplier(definition);
        return true;
    }

    private static ExpantaNum AdvanceCampaignProgress(
        ExpantaNum currentProgress,
        ExpantaNum combatRatio,
        double deltaSeconds,
        ExpantaNum progressMultiplier)
    {
        if (!progressMultiplier.IsFinite || progressMultiplier < ExpantaNum.Zero)
            throw new InvalidOperationException("Campaign progress multiplier is invalid.");
        return ExpantaNum.Clamp01(
            ExpantaNum.Clamp01(currentProgress) +
            CampaignManager.CalculateProgressRate(combatRatio) *
            progressMultiplier * deltaSeconds);
    }

    public bool TryAdvanceColonization(
        SectorDefinition definition,
        double deltaSeconds,
        GameState runtimeState,
        ResourceManager resourceManager,
        out SectorOperationFailure failure)
    {
        EnsureInitialized();
        if (definition == null || !TryGetStateByStableId(definition, out SectorState state))
        {
            failure = SectorOperationFailure.UnknownSector;
            return false;
        }
        if (!definition.IsHomeSystem)
        {
            failure = SectorOperationFailure.ColonizationNotAllowedInInterstellarSystem;
            return false;
        }
        if (runtimeState == null || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
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
        ExpantaNum explorationSpeedMultiplier =
            ProgressionModifierManager.Current.ExplorationPowerMultiplier;
        if (!explorationSpeedMultiplier.IsFinite ||
            explorationSpeedMultiplier <= ExpantaNum.Zero)
            explorationSpeedMultiplier = ExpantaNum.One;
        ExpantaNum effectiveDuration =
            definition.ColonizationDurationSeconds / explorationSpeedMultiplier;
        if (!TryCalculateCosts(
                definition.ColonizationFoodPerSecond,
                definition.ColonizationResourceRatesPerSecond,
                CalculateColonizationBillableSeconds(
                    state.CampaignProgress,
                    effectiveDuration,
                    deltaSeconds),
                resourceManager,
                out ExpantaNum foodCost,
                out List<Pair<Resource, ExpantaNum>> resourceCosts,
                out failure))
            return false;
        double billableSeconds = CalculateColonizationBillableSeconds(
            state.CampaignProgress,
            effectiveDuration,
            deltaSeconds);
        if (runtimeState.FoodAmount < foodCost || !HasResourceCosts(resourceManager, resourceCosts))
        {
            failure = SectorOperationFailure.InsufficientCampaignSupply;
            return false;
        }

        bool previousColonizationActive = state.ColonizationActive;
        ExpantaNum previousColonizationProgress = state.CampaignProgress;
        bool previousColonizationOccupied = state.Occupied;
        int previousColonizationVisitCount = state.VisitCount;
        bool previousSectorCampaignActive = state.CampaignActive;
        ExpantaNum previousSectorCasualties = state.CampaignCasualties;
        ExpantaNum previousSectorCombatRatio = state.CampaignCombatRatio;
        if (!TryConsumeCampaignCosts(
                runtimeState,
                resourceManager,
                foodCost,
                resourceCosts,
                () =>
                {
                    state.SetColonizationActive(true);
                    state.SetCampaignProgress(
                        state.CampaignProgress +
                        billableSeconds / effectiveDuration);
                    if (state.CampaignProgress >= ExpantaNum.One)
                    {
                        state.SetCampaignProgress(ExpantaNum.One);
                        CompleteOccupation(definition, state);
                    }
                },
                () =>
                {
                    state.RestoreExact(
                        state.Unlocked,
                        previousColonizationOccupied,
                        previousColonizationActive,
                        previousSectorCampaignActive,
                        previousColonizationProgress,
                        previousSectorCasualties,
                        previousSectorCombatRatio,
                        previousColonizationVisitCount);
                }))
        {
            failure = SectorOperationFailure.InsufficientCampaignSupply;
            return false;
        }
        failure = SectorOperationFailure.None;
        return true;
    }

    public bool TryRepairFleet(
        SectorDefinition definition,
        GameState runtimeState,
        ResourceManager resourceManager,
        ExpantaNum requestedAmount,
        out ExpantaNum repairedAmount,
        out SectorOperationFailure failure)
    {
        EnsureInitialized();
        if (definition == null || !TryGetStateByStableId(definition, out SectorState state))
        {
            repairedAmount = ExpantaNum.Zero;
            failure = SectorOperationFailure.UnknownSector;
            return false;
        }
        return TryRepairFleetForState(state, runtimeState, resourceManager, requestedAmount, out repairedAmount, out failure);
    }

    public bool TryRepairFleet(
        GameState runtimeState,
        ResourceManager resourceManager,
        ExpantaNum requestedAmount,
        out ExpantaNum repairedAmount,
        out SectorOperationFailure failure)
    {
        EnsureInitialized();
        repairedAmount = ExpantaNum.Zero;
        if (runtimeState == null || resourceManager == null)
        {
            failure = SectorOperationFailure.InvalidRepairAmount;
            return false;
        }
        if (!requestedAmount.IsFinite || requestedAmount <= ExpantaNum.Zero)
        {
            failure = SectorOperationFailure.InvalidRepairAmount;
            return false;
        }
        if (runtimeState.Campaign.Casualties <= ExpantaNum.Zero)
        {
            failure = SectorOperationFailure.NoFleetDamage;
            return false;
        }
        if (string.IsNullOrWhiteSpace(runtimeState.Campaign.TargetSectorId))
        {
            failure = SectorOperationFailure.CampaignRequired;
            return false;
        }

        if (!DataBase<SectorDefinition>.TryFind(
                runtimeState.Campaign.TargetSectorId,
                out SectorDefinition targetDefinition) ||
            targetDefinition == null ||
            !TryGetStateByStableId(targetDefinition, out SectorState targetState))
        {
            failure = SectorOperationFailure.UnknownSector;
            return false;
        }

        return TryRepairFleetForState(
            targetState,
            runtimeState,
            resourceManager,
            requestedAmount,
            out repairedAmount,
            out failure);
    }

    private bool TryRepairFleetForState(
        SectorState state,
        GameState runtimeState,
        ResourceManager resourceManager,
        ExpantaNum requestedAmount,
        out ExpantaNum repairedAmount,
        out SectorOperationFailure failure)
    {
        repairedAmount = ExpantaNum.Zero;
        if (state == null || runtimeState == null || resourceManager == null ||
            !requestedAmount.IsFinite || requestedAmount <= ExpantaNum.Zero)
        {
            failure = SectorOperationFailure.InvalidRepairAmount;
            return false;
        }
        if (!runtimeState.Campaign.Active &&
            runtimeState.Campaign.Casualties <= ExpantaNum.Zero)
        {
            failure = SectorOperationFailure.CampaignRequired;
            return false;
        }
        if (!string.Equals(
                runtimeState.Campaign.TargetSectorId,
                state.Definition.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            failure = SectorOperationFailure.CampaignInProgress;
            return false;
        }
        if (state.CampaignCasualties <= ExpantaNum.Zero)
        {
            failure = SectorOperationFailure.NoFleetDamage;
            return false;
        }
        if (runtimeState.Campaign.Casualties <= ExpantaNum.Zero)
        {
            failure = SectorOperationFailure.NoFleetDamage;
            return false;
        }

        ExpantaNum targetAmount = ExpantaNum.Min(
            requestedAmount,
            ExpantaNum.Min(state.CampaignCasualties, runtimeState.Campaign.Casualties));
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs = CalculateFleetRepairCosts(targetAmount);
        if (!HasResourceCosts(resourceManager, costs))
        {
            failure = SectorOperationFailure.InsufficientFleetRepairSupply;
            return false;
        }
        var payment = AggregateCosts(costs);
        ExpantaNum committedRepairAmount = targetAmount;
        bool previousCampaignActive = runtimeState.Campaign.Active;
        string previousCampaignTarget = runtimeState.Campaign.TargetSectorId;
        ExpantaNum previousCampaignCasualties = runtimeState.Campaign.Casualties;
        ExpantaNum previousCampaignCombatRatio = runtimeState.Campaign.CombatRatio;
        CampaignDoctrine previousCampaignDoctrine = runtimeState.Campaign.Doctrine;
        bool previousSectorCampaignActive = state.CampaignActive;
        ExpantaNum previousSectorCasualties = state.CampaignCasualties;
        bool previousSectorOccupied = state.Occupied;
        bool previousSectorColonizationActive = state.ColonizationActive;
        ExpantaNum previousSectorProgress = state.CampaignProgress;
        ExpantaNum previousSectorCombatRatio = state.CampaignCombatRatio;
        int previousSectorVisitCount = state.VisitCount;
        if (!resourceManager.TryApplyAtomicPayment(payment, () =>
        {
            committedRepairAmount = runtimeState.RepairCampaignFleet(targetAmount);
            state.SetCampaignCasualties(state.CampaignCasualties - committedRepairAmount);
            if (!runtimeState.Campaign.Active &&
                runtimeState.Campaign.Casualties <= ExpantaNum.Zero)
                runtimeState.CompleteCampaign();
        }, () =>
        {
            runtimeState.RestoreCampaignExact(
                previousCampaignActive,
                previousCampaignTarget,
                previousCampaignCasualties,
                previousCampaignCombatRatio,
                previousCampaignDoctrine);
            state.SetCampaignActive(previousSectorCampaignActive);
            state.SetCampaignCasualties(previousSectorCasualties);
            state.RestoreExact(
                state.Unlocked,
                previousSectorOccupied,
                previousSectorColonizationActive,
                previousSectorCampaignActive,
                previousSectorProgress,
                previousSectorCasualties,
                previousSectorCombatRatio,
                previousSectorVisitCount);
        }))
        {
            failure = SectorOperationFailure.InsufficientFleetRepairSupply;
            return false;
        }
        repairedAmount = committedRepairAmount;
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
        if (runtimeState == null)
            return false;
        bool advanced = false;
        for (int i = 0; i < orderedStates.Count; i++)
        {
            SectorState state = orderedStates[i];
            if (state.CampaignActive)
            {
                bool campaignAdvanced = TryAdvanceCampaign(
                    state.Definition,
                    deltaSeconds,
                    runtimeState,
                    resourceManager,
                    out failure);
                advanced |= campaignAdvanced;
            }
            else if (!state.Occupied &&
                !state.Definition.IsHomeSystem &&
                state.CampaignProgress > ExpantaNum.Zero)
            {
                DecayProgress(state, 0.02d / 60d, deltaSeconds);
            }
        }
        return advanced;
    }

    public bool CancelCampaign(SectorDefinition definition)
    {
        return CancelCampaign(definition, null);
    }

    public bool CancelCampaign(SectorDefinition definition, GameState runtimeState)
    {
        EnsureInitialized();
        if (definition == null || !TryGetStateByStableId(definition, out SectorState state) || !state.CampaignActive)
            return false;
        if (runtimeState != null && runtimeState.Campaign.Active &&
            !string.Equals(
                runtimeState.Campaign.TargetSectorId,
                definition.Id,
                StringComparison.OrdinalIgnoreCase))
            return false;
        state.SetCampaignActive(false);
        if (runtimeState != null && runtimeState.Campaign.Active &&
            string.Equals(runtimeState.Campaign.TargetSectorId, definition.Id, StringComparison.OrdinalIgnoreCase))
            runtimeState.CancelCampaign();
        RefreshRelicCampaignSupport(runtimeState);
        return true;
    }

    public bool CancelCampaign(GameState runtimeState)
    {
        EnsureInitialized();
        if (runtimeState == null || !runtimeState.Campaign.Active)
            return false;
        string targetSectorId = runtimeState.Campaign.TargetSectorId;
        for (int i = 0; i < orderedStates.Count; i++)
        {
            SectorState state = orderedStates[i];
            if (string.Equals(
                    state.Definition.Id,
                    targetSectorId,
                    StringComparison.OrdinalIgnoreCase))
            {
                state.SetCampaignActive(false);
                break;
            }
        }
        runtimeState.CancelCampaign();
        RefreshRelicCampaignSupport(runtimeState);
        return true;
    }

    public bool TickActiveColonization(double deltaSeconds, GameState runtimeState, ResourceManager resourceManager, out SectorOperationFailure failure)
    {
        failure = SectorOperationFailure.None;
        if (runtimeState == null || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
        {
            failure = SectorOperationFailure.InvalidDelta;
            return false;
        }
        bool advanced = false;
        for (int i = 0; i < orderedStates.Count; i++)
        {
            SectorState state = orderedStates[i];
            if (state.ColonizationActive)
            {
                bool colonizationAdvanced = TryAdvanceColonization(
                    state.Definition,
                    deltaSeconds,
                    runtimeState,
                    resourceManager,
                    out failure);
                advanced |= colonizationAdvanced;
            }
            else if (!state.Occupied &&
                state.Definition.IsHomeSystem &&
                state.CampaignProgress > ExpantaNum.Zero)
            {
                DecayProgress(state, 0.005d / 60d, deltaSeconds);
            }
        }
        return advanced;
    }

    public bool TickOccupiedResourceProduction(double deltaSeconds, ResourceManager resourceManager)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (resourceManager == null)
            return false;

        occupiedProductionBuffer.Clear();
        bool produced = false;
        ExpantaNum multiplier = ProgressionModifierManager.Current.OccupiedResourceProductionMultiplier;
        for (int i = 0; i < orderedStates.Count; i++)
        {
            SectorState state = orderedStates[i];
            if (state == null || !state.Occupied)
                continue;

            IReadOnlyList<Pair<Resource, ExpantaNum>> rates =
                state.Definition.OccupiedResourceRatesPerSecond;
            if (rates == null)
                continue;
            for (int j = 0; j < rates.Count; j++)
            {
                Pair<Resource, ExpantaNum> rate = rates[j];
                if (rate.First == null || rate.Second <= ExpantaNum.Zero)
                    continue;
                ExpantaNum delta = rate.Second * deltaSeconds * multiplier *
                    ProgressionModifierManager.Current.GetOccupiedResourceProductionMultiplier(rate.First);
                occupiedProductionBuffer[rate.First] =
                    occupiedProductionBuffer.TryGetValue(rate.First, out ExpantaNum existing)
                    ? existing + delta
                    : delta;
                produced = true;
            }
        }
        foreach (KeyValuePair<Resource, ExpantaNum> entry in occupiedProductionBuffer)
            resourceManager.AddAmount(entry.Key, entry.Value);
        return produced;
    }

#if UNITY_EDITOR
    public void AccumulateOccupiedResourcePotentialForEditor(ResourceManager resourceManager) =>
        AccumulateOccupiedResourcePotential(resourceManager);
#endif

    internal void AccumulateOccupiedResourcePotential(ResourceManager resourceManager)
    {
        if (resourceManager == null)
            return;

        ExpantaNum multiplier =
            ProgressionModifierManager.Current.OccupiedResourceProductionMultiplier;
        for (int i = 0; i < orderedStates.Count; i++)
        {
            SectorState state = orderedStates[i];
            if (state == null || !state.Occupied)
                continue;
            IReadOnlyList<Pair<Resource, ExpantaNum>> rates =
                state.Definition.OccupiedResourceRatesPerSecond;
            for (int j = 0; rates != null && j < rates.Count; j++)
            {
                Pair<Resource, ExpantaNum> rate = rates[j];
                if (rate.First != null && rate.Second > ExpantaNum.Zero)
                    resourceManager.AdjustTickPotentialProduction(
                        rate.First,
                        rate.Second * multiplier *
                        ProgressionModifierManager.Current.GetOccupiedResourceProductionMultiplier(rate.First));
            }
        }
    }

#if UNITY_EDITOR
    public static double CalculateCampaignBillableSecondsForEditor(
        ExpantaNum currentProgress,
        ExpantaNum combatRatio,
        ExpantaNum progressMultiplier,
        double requestedSeconds) =>
        CalculateCampaignBillableSeconds(
            currentProgress,
            combatRatio,
            progressMultiplier,
            requestedSeconds);

    public static double CalculateColonizationBillableSecondsForEditor(
        ExpantaNum currentProgress,
        ExpantaNum durationSeconds,
        double requestedSeconds) =>
        CalculateColonizationBillableSeconds(currentProgress, durationSeconds, requestedSeconds);
#endif

    internal static double CalculateCampaignBillableSeconds(
        ExpantaNum currentProgress,
        ExpantaNum combatRatio,
        ExpantaNum progressMultiplier,
        double requestedSeconds)
    {
        if (requestedSeconds <= 0d)
            return 0d;
        // Bill the same effective rate that CommitCampaignAdvance uses. In
        // particular, Surge is intentionally faster than 1x; clamping here
        // would charge a full tick even when the campaign reaches completion
        // earlier within that tick.
        ExpantaNum rate = CampaignManager.CalculateProgressRate(combatRatio) *
            ExpantaNum.Max(ExpantaNum.Zero, progressMultiplier);
        if (rate <= ExpantaNum.Zero)
            return requestedSeconds;
        double secondsToCompletion =
            ((ExpantaNum.One - ExpantaNum.Clamp01(currentProgress)) / rate).ToDouble();
        return Math.Min(requestedSeconds, Math.Max(0d, secondsToCompletion));
    }

    internal static double CalculateColonizationBillableSeconds(
        ExpantaNum currentProgress,
        ExpantaNum durationSeconds,
        double requestedSeconds)
    {
        if (requestedSeconds <= 0d)
            return 0d;
        double remaining = Math.Max(
            0d,
            (ExpantaNum.One - ExpantaNum.Clamp01(currentProgress)).ToDouble());
        return Math.Min(requestedSeconds, remaining * durationSeconds.ToDouble());
    }

    private static ExpantaNum CalculateCampaignCombatRatio(
        SectorDefinition definition,
        SectorState state,
        GameState runtimeState)
    {
        return CampaignManager.CalculateCombatRatio(
            CalculateCampaignEffectivePower(state, runtimeState),
            definition.EnemyPower);
    }

    private static ExpantaNum CalculateCampaignEffectivePower(
        SectorState state, GameState runtimeState)
    {
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        return CampaignManager.CalculateEffectivePower(
            runtimeState.AttackPower,
            runtimeState.FleetPower,
            runtimeState.MilitaryManpower,
            runtimeState.SupplySatisfaction,
            runtimeState.PowerSatisfaction,
            runtimeState.LogisticsSatisfaction,
            modifiers.MilitaryMultiplier,
            state.CampaignCasualties);
    }

    public bool CancelColonization(SectorDefinition definition)
    {
        EnsureInitialized();
        if (definition == null || !TryGetStateByStableId(definition, out SectorState state) || !state.ColonizationActive)
            return false;
        state.SetColonizationActive(false);
        return true;
    }

    private static void DecayProgress(SectorState state, double ratePerSecond, double deltaSeconds)
    {
        if (state == null || deltaSeconds <= 0d)
            return;
        state.SetCampaignProgress(state.CampaignProgress - ratePerSecond * deltaSeconds);
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
                CampaignActive = state.CampaignActive,
                CampaignProgress = state.CampaignProgress.ToString(),
                CampaignCasualties = state.CampaignCasualties.ToString(),
                CampaignCombatRatio = state.CampaignCombatRatio.ToString(),
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

        var restoredSectors = new HashSet<SectorDefinition>();
        var restoredData = new List<SectorRestoreData>(data.States.Count);
        for (int i = 0; i < data.States.Count; i++)
        {
            SaveManager.SectorStateSaveData saved = data.States[i];
            if (saved == null ||
                !DataBase<SectorDefinition>.TryFind(saved.SectorId, out SectorDefinition definition) ||
                definition == null ||
                !TryGetStateByStableId(definition, out _))
                throw new InvalidOperationException(
                    $"Unknown sector in save data: '{saved?.SectorId}'.");
            if (!restoredSectors.Add(definition))
                throw new InvalidOperationException(
                    $"存档中的区域状态重复包含“{definition.Id}”。");
            if (!ExpantaNum.TryParse(saved.CampaignProgress, out ExpantaNum progress))
                throw new InvalidOperationException(
                    $"星区存档值“{saved.CampaignProgress}”无效，编号为“{saved.SectorId}”。");
            ExpantaNum casualties = string.IsNullOrWhiteSpace(saved.CampaignCasualties)
                ? ExpantaNum.Zero
                : ParseSectorNumber(saved.CampaignCasualties, saved.SectorId, "CampaignCasualties");
            ExpantaNum combatRatio = string.IsNullOrWhiteSpace(saved.CampaignCombatRatio)
                ? ExpantaNum.Zero
                : ParseSectorNumber(saved.CampaignCombatRatio, saved.SectorId, "CampaignCombatRatio");
            ValidateRestoredState(
                saved.SectorId,
                saved.Unlocked,
                saved.Occupied,
                saved.ColonizationActive,
                saved.CampaignActive,
                progress,
                casualties,
                combatRatio,
                saved.VisitCount);
            restoredData.Add(new SectorRestoreData
            {
                Definition = definition,
                Unlocked = saved.Unlocked,
                Occupied = saved.Occupied,
                ColonizationActive = saved.ColonizationActive,
                CampaignActive = saved.CampaignActive,
                Progress = progress,
                Casualties = casualties,
                CombatRatio = combatRatio,
                VisitCount = saved.VisitCount
            });
        }

        for (int i = 0; i < orderedStates.Count; i++)
            orderedStates[i].ResetForLoad();
        for (int i = 0; i < restoredData.Count; i++)
        {
            SectorRestoreData restored = restoredData[i];
            GetState(restored.Definition).Restore(
                restored.Unlocked,
                restored.Occupied,
                restored.ColonizationActive,
                restored.CampaignActive,
                restored.Progress,
                restored.Casualties,
                restored.CombatRatio,
                restored.VisitCount);
        }
    }

    internal void ValidateCampaignState(GameState runtimeState)
    {
        EnsureInitialized();
        if (runtimeState == null)
            throw new ArgumentNullException(nameof(runtimeState));

        CampaignState campaign = runtimeState.Campaign;
        if (campaign == null)
            throw new InvalidOperationException("全局战役状态缺失。");
        if (string.IsNullOrWhiteSpace(campaign.TargetSectorId))
        {
            if (campaign.Active || campaign.Casualties > ExpantaNum.Zero)
                throw new InvalidOperationException("全局战役状态缺少目标星区。");
            return;
        }
        if (!DataBase<SectorDefinition>.TryFind(campaign.TargetSectorId, out SectorDefinition definition) ||
            !TryGetStateByStableId(definition, out SectorState state))
            throw new InvalidOperationException(
                $"全局战役目标星区“{campaign.TargetSectorId}”不存在。");
        if (campaign.Active && definition.IsHomeSystem)
            throw new InvalidOperationException(
                $"Active campaign cannot target home sector '{definition.Id}'.");
        if (campaign.Active &&
            (!IsInterstellarRouteUnlocked() ||
             !ProgressionModifierManager.Current.IsSystemUnlocked(ResearchSystem.DeepSpaceFleet)))
            throw new InvalidOperationException(
                "Active campaign is missing the interstellar route or deep-space fleet unlock.");
        if (state.CampaignCasualties != campaign.Casualties)
            throw new InvalidOperationException(
                $"全局战役与星区“{definition.Id}”的伤亡数据不一致。");
        if (campaign.Active && (!state.Unlocked || state.Occupied || !state.CampaignActive))
            throw new InvalidOperationException(
                $"全局战役与星区“{definition.Id}”的活动状态不一致。");
        if (!campaign.Active && campaign.Casualties > ExpantaNum.Zero && state.CampaignActive)
            throw new InvalidOperationException(
                $"已取消的全局战役不能让星区“{definition.Id}”继续处于活动状态。");
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
        BuildingManager buildingManager = buildingManagerCache;
        if (buildingManager == null)
            buildingManager = buildingManagerCache =
                UnityEngine.Object.FindObjectOfType<BuildingManager>();
        return buildingManager != null &&
            buildingManager.States.TryGetValue(launchCenter, out BuildingState state) &&
            state.Amount >= ExpantaNum.One;
    }

    private static bool IsInterstellarRouteUnlocked()
    {
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        return modifiers.IsSystemUnlocked(ResearchSystem.HomeSystemSurvey) &&
            modifiers.IsSystemUnlocked(ResearchSystem.InterstellarNavigation);
    }

    private static bool ValidateRewards(SectorDefinition definition)
    {
        if (definition.TerritoryReward.IsNaN || definition.TerritoryReward < ExpantaNum.Zero)
            return false;

        IReadOnlyList<Pair<Resource, ExpantaNum>> rewards = definition.ResourceRewards;
        if (definition.IsHomeSystem && rewards != null && rewards.Count > 0)
            return false;
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
        IReadOnlyList<Pair<Resource, ExpantaNum>> rewards = definition.ResourceRewards;
        if (definition.IsHomeSystem && rewards != null && rewards.Count > 0)
            throw new InvalidOperationException("Home-system sectors cannot grant resource rewards.");
        var aggregated = new Dictionary<Resource, ExpantaNum>();
        for (int i = 0; rewards != null && i < rewards.Count; i++)
        {
            Pair<Resource, ExpantaNum> reward = rewards[i];
            if (reward.Second <= ExpantaNum.Zero)
                continue;
            ExpantaNum total = aggregated.TryGetValue(reward.First, out ExpantaNum current)
                ? current + reward.Second
                : reward.Second;
            if (!total.IsFinite)
                throw new InvalidOperationException($"Sector reward total is not finite: {reward.First.Id}");
            aggregated[reward.First] = total;
        }

        // Preflight all writes before changing any balance. ValidateRewards
        // normally guarantees this, but this guard keeps the default applier
        // atomic if an asset is modified at runtime or a duplicate reward
        // overflows during aggregation.
        if (definition.TerritoryReward.IsNaN || definition.TerritoryReward < ExpantaNum.Zero)
            throw new InvalidOperationException("Sector territory reward is invalid.");
        foreach (KeyValuePair<Resource, ExpantaNum> reward in aggregated)
        {
            if (reward.Key == null || !reward.Value.IsFinite || reward.Value < ExpantaNum.Zero)
                throw new InvalidOperationException("Sector resource reward is invalid.");
        }

        GameManager gameManager = GameManager.Instance;
        ResourceManager resourceManager = ResourceManager.Instance;
        ExpantaNum previousTerritory = gameManager.State.TerritoryTotal;
        if (aggregated.Count == 0)
        {
            gameManager.AdjustTerritoryTotal(definition.TerritoryReward);
            return;
        }

        if (!resourceManager.TryApplyAtomicChanges(
                aggregated,
                () => gameManager.AdjustTerritoryTotal(definition.TerritoryReward),
                () => gameManager.State.RestoreTerritoryTotal(previousTerritory)))
        {
            throw new InvalidOperationException("Unable to apply sector resource rewards atomically.");
        }
    }

    private bool TryCalculateCampaignCosts(
        SectorDefinition definition,
        double deltaSeconds,
        ExpantaNum supplyCostMultiplier,
        ResourceManager resourceManager,
        out ExpantaNum foodCost,
        out List<Pair<Resource, ExpantaNum>> resourceCosts,
        out SectorOperationFailure failure)
    {
        foodCost = ExpantaNum.Zero;
        resourceCosts = campaignResourceCostBuffer;
        resourceCosts.Clear();
        if (definition == null ||
            double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d ||
            !supplyCostMultiplier.IsFinite || supplyCostMultiplier < ExpantaNum.Zero ||
            !definition.CampaignFoodPerSecond.IsFinite ||
            definition.CampaignFoodPerSecond < ExpantaNum.Zero)
        {
            failure = SectorOperationFailure.InvalidCampaignCost;
            return false;
        }

        foodCost = definition.CampaignFoodPerSecond * supplyCostMultiplier * deltaSeconds;
        if (!foodCost.IsFinite || foodCost < ExpantaNum.Zero)
        {
            failure = SectorOperationFailure.InvalidCampaignCost;
            return false;
        }

        IReadOnlyList<Pair<Resource, ExpantaNum>> configuredRates =
            definition.CampaignResourceRatesPerSecond;

        for (int i = 0; configuredRates != null && i < configuredRates.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = configuredRates[i];
            if (cost.First == null || !cost.Second.IsFinite || cost.Second < ExpantaNum.Zero)
            {
                failure = SectorOperationFailure.InvalidCampaignCost;
                return false;
            }
            ExpantaNum totalCost = cost.Second * supplyCostMultiplier * deltaSeconds;
            if (!totalCost.IsFinite || totalCost < ExpantaNum.Zero)
            {
                failure = SectorOperationFailure.InvalidCampaignCost;
                return false;
            }
            resourceCosts.Add(new Pair<Resource, ExpantaNum>(
                cost.First,
                totalCost));
        }

        if (resourceManager == null && AggregateCosts(resourceCosts).Count > 0)
        {
            failure = SectorOperationFailure.InsufficientCampaignSupply;
            return false;
        }
        failure = SectorOperationFailure.None;
        return true;
    }

    private static IReadOnlyList<Pair<Resource, ExpantaNum>> ScaleCampaignResourceRates(
        IReadOnlyList<Pair<Resource, ExpantaNum>> configuredRates,
        ExpantaNum multiplier)
    {
        if (configuredRates == null || configuredRates.Count == 0)
            return Array.Empty<Pair<Resource, ExpantaNum>>();

        var scaledRates = new List<Pair<Resource, ExpantaNum>>(configuredRates.Count);
        for (int i = 0; i < configuredRates.Count; i++)
        {
            Pair<Resource, ExpantaNum> rate = configuredRates[i];
            scaledRates.Add(new Pair<Resource, ExpantaNum>(
                rate.First,
                rate.Second * multiplier));
        }
        return scaledRates;
    }

    private static ExpantaNum GetCampaignDoctrineProgressMultiplier(GameState runtimeState)
    {
        if (runtimeState == null ||
            !GameManager.TryGetInstance(out GameManager gameManager) ||
            gameManager.UltraProject == null ||
            !gameManager.UltraProject.IsCampaignDoctrineUnlocked)
            return ExpantaNum.One;
        return runtimeState.Campaign.Doctrine == CampaignDoctrine.Surge
            ? new ExpantaNum(1.35d)
            : new ExpantaNum(0.85d);
    }

    private static ExpantaNum GetRelicCampaignSupplyMultiplier(
        SectorDefinition definition, GameState runtimeState)
    {
        return GameManager.TryGetInstance(out GameManager gameManager) &&
            ReferenceEquals(gameManager.State, runtimeState)
            ? gameManager.Relic.GetCampaignSupplyMultiplier(definition)
            : ExpantaNum.One;
    }

    private static void RefreshRelicCampaignSupport(GameState runtimeState)
    {
        if (GameManager.TryGetInstance(out GameManager gameManager) &&
            ReferenceEquals(gameManager.State, runtimeState))
            gameManager.Relic.RefreshCampaignSupport();
    }

    private static ExpantaNum GetCampaignDoctrineSupplyMultiplier(GameState runtimeState)
    {
        if (runtimeState == null ||
            !GameManager.TryGetInstance(out GameManager gameManager) ||
            gameManager.UltraProject == null ||
            !gameManager.UltraProject.IsCampaignDoctrineUnlocked)
            return ExpantaNum.One;
        return runtimeState.Campaign.Doctrine == CampaignDoctrine.Surge
            ? new ExpantaNum(1.35d)
            : new ExpantaNum(1.15d);
    }

    private static ExpantaNum GetCampaignDoctrineCasualtyMultiplier(GameState runtimeState)
    {
        if (runtimeState == null ||
            !GameManager.TryGetInstance(out GameManager gameManager) ||
            gameManager.UltraProject == null ||
            !gameManager.UltraProject.IsCampaignDoctrineUnlocked)
            return ExpantaNum.One;
        return runtimeState.Campaign.Doctrine == CampaignDoctrine.Surge
            ? new ExpantaNum(1.25d)
            : new ExpantaNum(0.75d);
    }

    private static ExpantaNum ParseSectorNumber(string raw, string sectorId, string field)
    {
        if (ExpantaNum.TryParse(raw, out ExpantaNum value))
            return value;
        throw new InvalidOperationException(
            $"星区存档字段“{sectorId}.{field}”的值“{raw}”无效。");
    }

    private static void ValidateRestoredState(
        string sectorId,
        bool unlocked,
        bool occupied,
        bool colonizationActive,
        bool campaignActive,
        ExpantaNum progress,
        ExpantaNum casualties,
        ExpantaNum combatRatio,
        int visitCount)
    {
        if (!progress.IsFinite || progress < ExpantaNum.Zero || progress > ExpantaNum.One)
            throw new InvalidOperationException(
                $"星区“{sectorId}”的 CampaignProgress 必须是 0 到 1 之间的有限值。");
        if (!casualties.IsFinite || casualties < ExpantaNum.Zero)
            throw new InvalidOperationException(
                $"星区“{sectorId}”的 CampaignCasualties 必须是非负有限值。");
        if (!combatRatio.IsFinite || combatRatio < ExpantaNum.Zero)
            throw new InvalidOperationException(
                $"星区“{sectorId}”的 CampaignCombatRatio 必须是非负有限值。");
        if (visitCount < 0)
            throw new InvalidOperationException(
                $"星区“{sectorId}”的 VisitCount 不能为负数。");
        if (occupied && !unlocked)
            throw new InvalidOperationException(
                $"星区“{sectorId}”不能在未解锁时处于占领状态。");
        if (colonizationActive && (!unlocked || occupied))
            throw new InvalidOperationException(
                $"星区“{sectorId}”的殖民状态与解锁/占领状态矛盾。");
        if (campaignActive && (!unlocked || occupied))
            throw new InvalidOperationException(
                $"星区“{sectorId}”的战役状态与解锁/占领状态矛盾。");
        if (campaignActive && progress >= ExpantaNum.One)
            throw new InvalidOperationException(
                $"星区“{sectorId}”的战役不能在完成边界保持活动状态。");
        if (colonizationActive && progress >= ExpantaNum.One)
            throw new InvalidOperationException(
                $"星区“{sectorId}”的殖民不能在完成边界保持活动状态。");
        if (!occupied && !campaignActive && !colonizationActive &&
            progress >= ExpantaNum.One)
            throw new InvalidOperationException(
                $"星区“{sectorId}”不能在未占领且无活动阶段时保留完成进度。");
        if (colonizationActive && campaignActive)
            throw new InvalidOperationException(
                $"星区“{sectorId}”不能同时进行殖民和战役。");
    }

    private bool TryCalculateCosts(
        ExpantaNum foodPerSecond,
        IReadOnlyList<Pair<Resource, ExpantaNum>> configuredRatesPerSecond,
        double deltaSeconds,
        ResourceManager resourceManager,
        out ExpantaNum foodCost,
        out List<Pair<Resource, ExpantaNum>> resourceCosts,
        out SectorOperationFailure failure)
    {
        foodCost = ExpantaNum.Zero;
        resourceCosts = campaignResourceCostBuffer;
        resourceCosts.Clear();
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d ||
            !foodPerSecond.IsFinite || foodPerSecond < ExpantaNum.Zero)
        {
            failure = SectorOperationFailure.InvalidCampaignCost;
            return false;
        }

        foodCost = foodPerSecond * deltaSeconds;
        if (!foodCost.IsFinite || foodCost < ExpantaNum.Zero)
        {
            failure = SectorOperationFailure.InvalidCampaignCost;
            return false;
        }

        if (configuredRatesPerSecond != null)
        {
            for (int i = 0; i < configuredRatesPerSecond.Count; i++)
            {
                Pair<Resource, ExpantaNum> cost = configuredRatesPerSecond[i];
                if (cost.First == null || !cost.Second.IsFinite || cost.Second < ExpantaNum.Zero)
                {
                    failure = SectorOperationFailure.InvalidCampaignCost;
                    return false;
                }
                ExpantaNum totalCost = cost.Second * deltaSeconds;
                if (!totalCost.IsFinite || totalCost < ExpantaNum.Zero)
                {
                    failure = SectorOperationFailure.InvalidCampaignCost;
                    return false;
                }
                resourceCosts.Add(new Pair<Resource, ExpantaNum>(
                    cost.First,
                    totalCost));
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

    private Dictionary<Resource, ExpantaNum> AggregateCosts(
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
    {
        Dictionary<Resource, ExpantaNum> aggregate = campaignCostAggregationBuffer;
        aggregate.Clear();
        if (costs == null)
            return aggregate;
        for (int i = 0; i < costs.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = costs[i];
            if (cost.First == null || cost.Second <= ExpantaNum.Zero)
                continue;
            aggregate[cost.First] = aggregate.TryGetValue(cost.First, out ExpantaNum current)
                ? current + cost.Second
                : cost.Second;
        }
        return aggregate;
    }

    private List<SectorSnapshot> CaptureSectorSnapshots()
    {
        var snapshots = new List<SectorSnapshot>(orderedStates.Count);
        for (int i = 0; i < orderedStates.Count; i++)
        {
            SectorState state = orderedStates[i];
            snapshots.Add(new SectorSnapshot
            {
                State = state,
                Unlocked = state.Unlocked,
                Occupied = state.Occupied,
                ColonizationActive = state.ColonizationActive,
                CampaignActive = state.CampaignActive,
                Progress = state.CampaignProgress,
                Casualties = state.CampaignCasualties,
                CombatRatio = state.CampaignCombatRatio,
                VisitCount = state.VisitCount
            });
        }
        return snapshots;
    }

    private static void RestoreSectorSnapshots(IReadOnlyList<SectorSnapshot> snapshots)
    {
        if (snapshots == null)
            return;
        for (int i = 0; i < snapshots.Count; i++)
        {
            SectorSnapshot snapshot = snapshots[i];
            snapshot.State.RestoreExact(
                snapshot.Unlocked,
                snapshot.Occupied,
                snapshot.ColonizationActive,
                snapshot.CampaignActive,
                snapshot.Progress,
                snapshot.Casualties,
                snapshot.CombatRatio,
                snapshot.VisitCount);
        }
    }

    private static IReadOnlyList<Pair<Resource, ExpantaNum>> CalculateFleetRepairCosts(
        ExpantaNum repairAmount)
    {
        if (!repairAmount.IsFinite || repairAmount <= ExpantaNum.Zero)
            return Array.Empty<Pair<Resource, ExpantaNum>>();

        ExpantaNum multiplier =
            ProgressionModifierManager.Current.FleetRepairCostMultiplier;
        return new List<Pair<Resource, ExpantaNum>>
        {
            new Pair<Resource, ExpantaNum>(
                DataBase<Resource>.Find("TitaniumAlloy"),
                repairAmount * new ExpantaNum(2d) * multiplier),
            new Pair<Resource, ExpantaNum>(
                DataBase<Resource>.Find("Composite"),
                repairAmount * multiplier),
            new Pair<Resource, ExpantaNum>(
                DataBase<Resource>.Find("PhantomWeave"),
                repairAmount * multiplier),
            new Pair<Resource, ExpantaNum>(
                DataBase<Resource>.Find("RocketFuel"),
                repairAmount * new ExpantaNum(0.5d) * multiplier)
        };
    }

    private bool HasResourceCosts(
        ResourceManager resourceManager,
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
    {
        if (costs == null || costs.Count == 0)
            return true;
        for (int i = 0; i < costs.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = costs[i];
            if (cost.First == null || !cost.Second.IsFinite || cost.Second < ExpantaNum.Zero)
                return false;
        }
        if (resourceManager == null)
            return AggregateCosts(costs).Count == 0;
        foreach (KeyValuePair<Resource, ExpantaNum> cost in AggregateCosts(costs))
        {
            if (!resourceManager.States.TryGetValue(cost.Key, out ResourceState state) ||
                state.Amount < cost.Value)
                return false;
        }
        return true;
    }

    private bool TryConsumeCampaignCosts(
        GameState runtimeState,
        ResourceManager resourceManager,
        ExpantaNum foodCost,
        IReadOnlyList<Pair<Resource, ExpantaNum>> resourceCosts,
        Action commitState,
        Action rollbackState)
    {
        if (runtimeState == null || runtimeState.FoodAmount < foodCost)
            return false;
        Dictionary<Resource, ExpantaNum> payment = AggregateCosts(resourceCosts);
        ExpantaNum previousFoodAmount = runtimeState.FoodAmount;
        ExpantaNum previousFoodCapacity = runtimeState.FoodCapacity;
        if (payment.Count == 0)
        {
            if (!runtimeState.TryConsumeFood(foodCost))
                return false;
            try
            {
                commitState?.Invoke();
                return true;
            }
            catch (Exception commitException)
            {
                Exception rollbackException = null;
                try
                {
                    try
                    {
                        rollbackState?.Invoke();
                    }
                    catch (Exception exception)
                    {
                        rollbackException = exception;
                    }
                }
                finally
                {
                    runtimeState.RestoreFoodExact(previousFoodAmount, previousFoodCapacity);
                }
                if (rollbackException != null)
                    Debug.LogException(rollbackException);
                throw commitException;
            }
        }
        if (resourceManager == null || !HasResourceCosts(resourceManager, resourceCosts))
            return false;
        return resourceManager.TryApplyAtomicPayment(
            payment,
            () =>
            {
                if (!runtimeState.TryConsumeFood(foodCost))
                    throw new InvalidOperationException("Food payment became unavailable during sector transaction.");
                commitState?.Invoke();
            },
            () =>
            {
                try { rollbackState?.Invoke(); }
                finally { runtimeState.RestoreFoodExact(previousFoodAmount, previousFoodCapacity); }
            });
    }

    private void CompleteOccupation(
        SectorDefinition definition,
        SectorState state)
    {
        state.SetOccupied(true);
        state.SetColonizationActive(false);
        state.SetVisitCount(state.VisitCount + 1);
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
