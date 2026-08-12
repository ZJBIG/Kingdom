using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

public enum SectorOperationFailure
{
    [Description("无")]
    None,
    [Description("未知星区")]
    UnknownSector,
    [Description("星区已经解锁")]
    AlreadyUnlocked,
    [Description("星区已经占领")]
    AlreadyOccupied,
    [Description("前置星区尚未占领")]
    PrerequisiteNotOccupied,
    [Description("需要发射中心")]
    LaunchCenterRequired,
    [Description("星区奖励无效")]
    InvalidReward,
    [Description("星区尚未解锁")]
    NotUnlocked,
    [Description("尚未完成远征")]
    CampaignRequired,
    [Description("远征正在进行")]
    CampaignInProgress,
    [Description("时间增量无效")]
    InvalidDelta,
    [Description("远征补给不足")]
    InsufficientCampaignSupply,
    [Description("探索能力不足")]
    InsufficientExplorationPower,
    [Description("远征成本无效")]
    InvalidCampaignCost,
    [Description("本土星系不允许进行星际战役")]
    CampaignNotAllowedInHomeSystem,
    [Description("星际星系不允许进行殖民")]
    ColonizationNotAllowedInInterstellarSystem,
    [Description("星际星系尚未解锁")]
    InterstellarSystemLocked,
    [Description("殖民正在进行")]
    ColonizationInProgress,
    [Description("舰队没有受损")]
    NoFleetDamage,
    [Description("维修数量无效")]
    InvalidRepairAmount,
    [Description("舰队维修补给不足")]
    InsufficientFleetRepairSupply,
    [Description("舰队仍有未维修的损伤")]
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
    public ExpantaNum SupplySatisfaction { get; }
    public ExpantaNum PowerSatisfaction { get; }
    public ExpantaNum LogisticsSatisfaction { get; }
    public ExpantaNum ProgressPerSecond { get; }
    public ExpantaNum EstimatedSecondsRemaining { get; }
    public ExpantaNum CasualtiesPerSecond { get; }
    public ExpantaNum FoodCostPerSecond { get; }
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceCostsPerSecond { get; }



    internal SectorCampaignPreview(
        bool isValid,
        bool hasSupply,
        ExpantaNum currentProgress,
        ExpantaNum effectivePower,
        ExpantaNum combatRatio,
        ExpantaNum fleetSurvivalFactor,
        ExpantaNum fleetReadiness,
        ExpantaNum currentCasualties,
        ExpantaNum supplySatisfaction,
        ExpantaNum powerSatisfaction,
        ExpantaNum logisticsSatisfaction,
        ExpantaNum progressPerSecond,
        ExpantaNum casualtiesPerSecond,
        ExpantaNum foodCostPerSecond,
        IReadOnlyList<Pair<Resource, ExpantaNum>> resourceRatesPerSecond)
    {
        IsValid = isValid;
        HasSupply = hasSupply;
        CurrentProgress = currentProgress;
        EffectivePower = effectivePower;
        CombatRatio = combatRatio;
        FleetSurvivalFactor = fleetSurvivalFactor;
        FleetReadiness = fleetReadiness;
        CurrentCasualties = currentCasualties;
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
        ProgressPerSecond = ExpantaNum.One / duration;
        EstimatedSecondsRemaining = ExpantaNum.Max(
            ExpantaNum.Zero,
            ExpantaNum.One - ExpantaNum.Clamp01(currentProgress)) / ProgressPerSecond;
        FoodCostPerSecond = foodCostPerSecond;
        ResourceCostsPerSecond = resourceRatesPerSecond;
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
        throw new KeyNotFoundException($"星区状态“{definition.Id}”尚未创建。");
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
            ProgressionModifierManager.Current.MilitaryMultiplier,
            state.CampaignCasualties);
        ExpantaNum combatRatio = CampaignManager.CalculateCombatRatio(effectivePower, definition.EnemyPower);
        ExpantaNum fleetSurvivalFactor = CampaignManager.CalculateFleetSurvivalFactor(
            runtimeState.DefensePower,
            definition.EnemyPower);
        ExpantaNum fleetReadiness = CampaignManager.CalculateFleetReadiness(
            runtimeState.FleetPower,
            state.CampaignCasualties);
        ExpantaNum progressPerSecond = CampaignManager.CalculateProgressRate(combatRatio) *
            definition.CampaignProgressMultiplier *
            ProgressionModifierManager.Current.CampaignProgressMultiplier;
        ExpantaNum casualtiesPerSecond = CampaignManager.CalculateCasualtyAmount(
            combatRatio,
            runtimeState.DefensePower,
            definition.EnemyPower,
            1d,
            ProgressionModifierManager.Current.CampaignCasualtyMultiplier);
        ExpantaNum supplyCostMultiplier =
            ProgressionModifierManager.Current.CampaignSupplyCostMultiplier;
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

        return new SectorCampaignPreview(
            true,
            hasSupply,
            state.CampaignProgress,
            effectivePower,
            combatRatio,
            fleetSurvivalFactor,
            fleetReadiness,
            state.CampaignCasualties,
            runtimeState.SupplySatisfaction,
            runtimeState.PowerSatisfaction,
            runtimeState.LogisticsSatisfaction,
            progressPerSecond,
            casualtiesPerSecond,
            foodCostPerSecond,
            resourceCosts);
    }

    public SectorExplorationPreview GetExplorationPreview(
        SectorDefinition definition,
        GameState runtimeState,
        ResourceManager resourceManager)
    {
        EnsureInitialized();
        if (definition == null || runtimeState == null ||
            !definition.IsHomeSystem || !states.TryGetValue(definition, out SectorState state))
            return new SectorExplorationPreview(
                false,
                false,
                false,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                ExpantaNum.One,
                ExpantaNum.Zero,
                Array.Empty<Pair<Resource, ExpantaNum>>());

        ExpantaNum requiredPower = ExpantaNum.Max(ExpantaNum.Zero, definition.EnemyPower);
        ExpantaNum explorationPower = ExpantaNum.Min(
            ExpantaNum.Max(ExpantaNum.Zero, runtimeState.AttackPower),
            ExpantaNum.Max(ExpantaNum.Zero, runtimeState.DefensePower)) *
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
            foodCostPerSecond,
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

        if (!definition.IsHomeSystem && !IsInterstellarRouteUnlocked())
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
        if (!IsInterstellarRouteUnlocked() ||
            !ProgressionModifierManager.Current.IsSystemUnlocked(ResearchSystem.DeepSpaceFleet))
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

        if (!TryCalculateCampaignCosts(
                definition,
                deltaSeconds,
                ProgressionModifierManager.Current.CampaignSupplyCostMultiplier,
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
        if (ownsLegacyCampaignSlot)
            runtimeState.BeginCampaign(definition.Id);
        state.SetCampaignActive(true);
        ConsumeCampaignCosts(runtimeState, resourceManager, foodCost, resourceCosts);
        ExpantaNum effectivePower = CampaignManager.CalculateEffectivePower(
            runtimeState.AttackPower,
            runtimeState.FleetPower,
            runtimeState.MilitaryManpower,
            runtimeState.SupplySatisfaction,
            runtimeState.PowerSatisfaction,
            runtimeState.LogisticsSatisfaction,
            ProgressionModifierManager.Current.MilitaryMultiplier,
            state.CampaignCasualties);
        ExpantaNum combatRatio = CampaignManager.CalculateCombatRatio(effectivePower, definition.EnemyPower);
        ExpantaNum nextProgress = CampaignManager.AdvanceProgress(
            state.CampaignProgress,
            combatRatio,
            deltaSeconds,
            definition.CampaignProgressMultiplier *
                ProgressionModifierManager.Current.CampaignProgressMultiplier);
        ExpantaNum casualties = CampaignManager.CalculateCasualtyAmount(
            combatRatio,
            runtimeState.DefensePower,
            definition.EnemyPower,
            deltaSeconds,
            ProgressionModifierManager.Current.CampaignCasualtyMultiplier);
        state.SetCampaignProgress(nextProgress);
        state.SetCampaignCombatRatio(combatRatio);
        state.SetCampaignCasualties(state.CampaignCasualties + casualties);
        if (runtimeState.Campaign.Active &&
            string.Equals(runtimeState.Campaign.TargetSectorId, definition.Id, StringComparison.OrdinalIgnoreCase))
            runtimeState.RecordCampaignCombat(combatRatio, casualties);

        if (nextProgress >= ExpantaNum.One)
        {
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
        ExpantaNum requiredExplorationPower =
            ExpantaNum.Max(ExpantaNum.Zero, definition.EnemyPower);
        ExpantaNum explorationMultiplier =
            ProgressionModifierManager.Current.ExplorationPowerMultiplier;
        ExpantaNum availableExplorationPower = ExpantaNum.Min(
            ExpantaNum.Max(ExpantaNum.Zero, runtimeState.AttackPower),
            ExpantaNum.Max(ExpantaNum.Zero, runtimeState.DefensePower)) *
            explorationMultiplier;
        if (availableExplorationPower < requiredExplorationPower)
        {
            failure = SectorOperationFailure.InsufficientExplorationPower;
            return false;
        }
        if (!TryCalculateCosts(
                definition.ColonizationFoodPerSecond,
                definition.ColonizationResourceRatesPerSecond,
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
        state.SetCampaignProgress(
            state.CampaignProgress +
            deltaSeconds / definition.ColonizationDurationSeconds);
        if (state.CampaignProgress >= ExpantaNum.One)
        {
            state.SetCampaignProgress(ExpantaNum.One);
            CompleteOccupation(definition, state, runtimeState);
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
        if (definition == null || !states.TryGetValue(definition, out SectorState state))
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
        if (requestedAmount <= ExpantaNum.Zero)
        {
            failure = SectorOperationFailure.InvalidRepairAmount;
            return false;
        }
        if (runtimeState.Campaign.Casualties <= ExpantaNum.Zero)
        {
            failure = SectorOperationFailure.NoFleetDamage;
            return false;
        }

        ExpantaNum targetAmount = ExpantaNum.Min(
            requestedAmount,
            runtimeState.Campaign.Casualties);
        Resource titaniumAlloy = DataBase<Resource>.Find("TitaniumAlloy");
        Resource composite = DataBase<Resource>.Find("Composite");
        Resource phantomWeave = DataBase<Resource>.Find("PhantomWeave");
        Resource rocketFuel = DataBase<Resource>.Find("RocketFuel");
        ExpantaNum repairCostMultiplier = ProgressionModifierManager.Current.FleetRepairCostMultiplier;
        var costs = new List<Pair<Resource, ExpantaNum>>
        {
            new Pair<Resource, ExpantaNum>(titaniumAlloy, targetAmount * new ExpantaNum(2d) * repairCostMultiplier),
            new Pair<Resource, ExpantaNum>(composite, targetAmount * repairCostMultiplier),
            new Pair<Resource, ExpantaNum>(phantomWeave, targetAmount * repairCostMultiplier),
            new Pair<Resource, ExpantaNum>(rocketFuel, targetAmount * new ExpantaNum(0.5d) * repairCostMultiplier)
        };
        if (!HasResourceCosts(resourceManager, costs))
        {
            failure = SectorOperationFailure.InsufficientFleetRepairSupply;
            return false;
        }

        for (int i = 0; i < costs.Count; i++)
            resourceManager.AddAmount(costs[i].First, -costs[i].Second);
        repairedAmount = runtimeState.RepairCampaignFleet(targetAmount);
        for (int i = 0; i < orderedStates.Count; i++)
        {
            SectorState state = orderedStates[i];
            if (string.Equals(
                    state.Definition.Id,
                    runtimeState.Campaign.TargetSectorId,
                    StringComparison.OrdinalIgnoreCase))
            {
                state.SetCampaignCasualties(
                    ExpantaNum.Max(ExpantaNum.Zero, state.CampaignCasualties - repairedAmount));
                break;
            }
        }
        failure = repairedAmount > ExpantaNum.Zero
            ? SectorOperationFailure.None
            : SectorOperationFailure.NoFleetDamage;
        return repairedAmount > ExpantaNum.Zero;
    }

    private static bool TryRepairFleetForState(
        SectorState state,
        GameState runtimeState,
        ResourceManager resourceManager,
        ExpantaNum requestedAmount,
        out ExpantaNum repairedAmount,
        out SectorOperationFailure failure)
    {
        repairedAmount = ExpantaNum.Zero;
        if (state == null || runtimeState == null || resourceManager == null || requestedAmount <= ExpantaNum.Zero)
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
        ExpantaNum repairCostMultiplier = ProgressionModifierManager.Current.FleetRepairCostMultiplier;
        var costs = new List<Pair<Resource, ExpantaNum>>
        {
            new Pair<Resource, ExpantaNum>(DataBase<Resource>.Find("TitaniumAlloy"), targetAmount * new ExpantaNum(2d) * repairCostMultiplier),
            new Pair<Resource, ExpantaNum>(DataBase<Resource>.Find("Composite"), targetAmount * repairCostMultiplier),
            new Pair<Resource, ExpantaNum>(DataBase<Resource>.Find("PhantomWeave"), targetAmount * repairCostMultiplier),
            new Pair<Resource, ExpantaNum>(DataBase<Resource>.Find("RocketFuel"), targetAmount * new ExpantaNum(0.5d) * repairCostMultiplier)
        };
        if (!HasResourceCosts(resourceManager, costs))
        {
            failure = SectorOperationFailure.InsufficientFleetRepairSupply;
            return false;
        }
        for (int i = 0; i < costs.Count; i++)
            resourceManager.AddAmount(costs[i].First, -costs[i].Second);
        repairedAmount = runtimeState.RepairCampaignFleet(targetAmount);
        state.SetCampaignCasualties(state.CampaignCasualties - repairedAmount);
        if (!runtimeState.Campaign.Active &&
            runtimeState.Campaign.Casualties <= ExpantaNum.Zero)
            runtimeState.CompleteCampaign();
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
            else if (!state.Definition.IsHomeSystem && state.CampaignProgress > ExpantaNum.Zero)
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
        if (definition == null || !states.TryGetValue(definition, out SectorState state) || !state.CampaignActive)
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
            else if (state.Definition.IsHomeSystem && state.CampaignProgress > ExpantaNum.Zero)
            {
                DecayProgress(state, 0.005d / 60d, deltaSeconds);
            }
        }
        return advanced;
    }

    public bool TickOccupiedResourceProduction(double deltaSeconds, ResourceManager resourceManager)
    {
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (resourceManager == null)
            return false;

        bool produced = false;
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
                resourceManager.AddAmount(
                    rate.First,
                    rate.Second * deltaSeconds *
                    ProgressionModifierManager.Current.OccupiedResourceProductionMultiplier);
                produced = true;
            }
        }
        return produced;
    }

    public bool CancelColonization(SectorDefinition definition)
    {
        EnsureInitialized();
        if (definition == null || !states.TryGetValue(definition, out SectorState state) || !state.ColonizationActive)
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

        for (int i = 0; i < orderedStates.Count; i++)
            orderedStates[i].ResetForLoad();

        for (int i = 0; i < data.States.Count; i++)
        {
            SaveManager.SectorStateSaveData saved = data.States[i];
            SectorDefinition definition = DataBase<SectorDefinition>.Find(saved.SectorId);
            SectorState state = GetState(definition);
            if (!ExpantaNum.TryParse(saved.CampaignProgress, out ExpantaNum progress))
                throw new InvalidOperationException(
                    $"星区存档值“{saved.CampaignProgress}”无效，编号为“{saved.SectorId}”。");
            ExpantaNum casualties = string.IsNullOrWhiteSpace(saved.CampaignCasualties)
                ? ExpantaNum.Zero
                : ParseSectorNumber(saved.CampaignCasualties, saved.SectorId, "CampaignCasualties");
            ExpantaNum combatRatio = string.IsNullOrWhiteSpace(saved.CampaignCombatRatio)
                ? ExpantaNum.Zero
                : ParseSectorNumber(saved.CampaignCombatRatio, saved.SectorId, "CampaignCombatRatio");
            state.Restore(
                saved.Unlocked,
                saved.Occupied,
                saved.ColonizationActive,
                saved.CampaignActive,
                progress,
                casualties,
                combatRatio,
                saved.VisitCount);
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

    private static bool IsInterstellarRouteUnlocked()
    {
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        return modifiers.IsSystemUnlocked(ResearchSystem.FirstContact) &&
            modifiers.IsSystemUnlocked(ResearchSystem.InterstellarNavigation);
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
        ExpantaNum supplyCostMultiplier,
        ResourceManager resourceManager,
        out ExpantaNum foodCost,
        out List<Pair<Resource, ExpantaNum>> resourceCosts,
        out SectorOperationFailure failure)
    {
        return TryCalculateCosts(
            definition.CampaignFoodPerSecond * supplyCostMultiplier,
            ScaleCampaignResourceRates(
                definition.CampaignResourceRatesPerSecond,
                supplyCostMultiplier),
            deltaSeconds,
            resourceManager,
            out foodCost,
            out resourceCosts,
            out failure);
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

    private static ExpantaNum ParseSectorNumber(string raw, string sectorId, string field)
    {
        if (ExpantaNum.TryParse(raw, out ExpantaNum value))
            return value;
        throw new InvalidOperationException(
            $"星区存档字段“{sectorId}.{field}”的值“{raw}”无效。");
    }

    private static bool TryCalculateCosts(
        ExpantaNum foodPerSecond,
        IReadOnlyList<Pair<Resource, ExpantaNum>> configuredRatesPerSecond,
        double deltaSeconds,
        ResourceManager resourceManager,
        out ExpantaNum foodCost,
        out List<Pair<Resource, ExpantaNum>> resourceCosts,
        out SectorOperationFailure failure)
    {
        foodCost = ExpantaNum.Max(ExpantaNum.Zero,
            foodPerSecond * deltaSeconds);
        resourceCosts = new List<Pair<Resource, ExpantaNum>>();
        if (foodPerSecond.IsNaN || foodPerSecond < ExpantaNum.Zero)
        {
            failure = SectorOperationFailure.InvalidCampaignCost;
            return false;
        }

        if (configuredRatesPerSecond != null)
        {
            for (int i = 0; i < configuredRatesPerSecond.Count; i++)
            {
                Pair<Resource, ExpantaNum> cost = configuredRatesPerSecond[i];
                if (cost.First == null || cost.Second.IsNaN || cost.Second < ExpantaNum.Zero)
                {
                    failure = SectorOperationFailure.InvalidCampaignCost;
                    return false;
                }
                resourceCosts.Add(new Pair<Resource, ExpantaNum>(
                    cost.First,
                    cost.Second * deltaSeconds));
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
