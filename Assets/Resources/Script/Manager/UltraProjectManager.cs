using System;
using System.Collections.Generic;

public enum UltraProjectOperationFailure
{
    None,
    DefinitionMissing,
    NotUltra,
    StageLocked,
    PrerequisiteResearchMissing,
    RequiredBuildingMissing,
    InvalidState,
    InsufficientStartupResources,
    InsufficientSupply,
    InvalidDoctrine,
    CampaignDoctrineLocked
}

public sealed class UltraProjectPreview
{
    public UltraProjectStatus Status { get; }
    public UltraProjectStage Stage { get; }
    public UltraProjectDoctrine Doctrine { get; }
    public ExpantaNum Progress { get; }
    public ExpantaNum ProgressPerSecond { get; }
    public ExpantaNum FoodPerSecond { get; }
    public ExpantaNum PowerPerSecond { get; }
    public ExpantaNum LogisticsPerSecond { get; }
    public ExpantaNum SupplySatisfaction { get; }
    public bool CanStart { get; }
    public UltraProjectOperationFailure Failure { get; }
    public IReadOnlyList<Pair<Resource, ExpantaNum>> StartupCosts { get; }
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ContinuousCosts { get; }

    internal UltraProjectPreview(
        UltraProjectState state,
        UltraProjectStageDefinition definition,
        UltraProjectDoctrine doctrine,
        ExpantaNum progressPerSecond,
        ExpantaNum supplySatisfaction,
        bool canStart,
        UltraProjectOperationFailure failure,
        IReadOnlyList<Pair<Resource, ExpantaNum>> continuousCosts,
        ExpantaNum foodPerSecond,
        ExpantaNum powerPerSecond,
        ExpantaNum logisticsPerSecond)
    {
        Status = state.Status;
        Stage = state.CurrentStage == UltraProjectStage.None &&
            state.Status == UltraProjectStatus.Locked
            ? UltraProjectStage.Prototype
            : state.CurrentStage;
        Doctrine = doctrine;
        Progress = state.StageProgress;
        ProgressPerSecond = progressPerSecond;
        FoodPerSecond = foodPerSecond;
        PowerPerSecond = powerPerSecond;
        LogisticsPerSecond = logisticsPerSecond;
        SupplySatisfaction = supplySatisfaction;
        CanStart = canStart;
        Failure = failure;
        StartupCosts = state.LaunchFeePaid
            ? Array.Empty<Pair<Resource, ExpantaNum>>()
            : definition?.OneTimeResourceCosts ?? Array.Empty<Pair<Resource, ExpantaNum>>();
        ContinuousCosts = continuousCosts ?? Array.Empty<Pair<Resource, ExpantaNum>>();
    }
}

/// <summary>
/// Ultra 后期唯一的连续工程状态所有者。工程消耗复用现有库存和资源事务，
/// 不创建新的资源生产建筑、局部库存或独立离线时钟。
/// </summary>
public sealed class UltraProjectManager
{
    public const string DefinitionId = UltraProjectState.ProjectId;
    private readonly UltraProjectState state = new();
    private UltraProjectDefinition definition;
    private UltraProjectOperationFailure lastFailure;

    public UltraProjectState State => state;
    public UltraProjectDefinition Definition
    {
        get
        {
            if (definition != null)
                return definition;

            // Missing content is a recoverable runtime condition. Keep the
            // manager's DefinitionMissing failure path usable instead of
            // allowing DataBase.Find to throw before Tick/Preview can pause
            // or report the problem through the normal state machine.
            DataBase<UltraProjectDefinition>.TryFind(DefinitionId, out definition);
            return definition;
        }
    }
    public UltraProjectOperationFailure LastFailure => lastFailure;
    public bool IsCampaignDoctrineUnlocked =>
        state.Status == UltraProjectStatus.Committed &&
        state.CurrentStage == UltraProjectStage.Completed &&
        state.CompletedStageCount == UltraProjectState.StageCount;

    public UltraProjectManager()
    {
        lastFailure = UltraProjectOperationFailure.None;
    }

    internal void InitializeNew()
    {
        state.InitializeNew();
        lastFailure = UltraProjectOperationFailure.None;
    }

    public bool TryStartStage(UltraProjectDoctrine doctrine, out UltraProjectOperationFailure failure)
    {
        failure = ValidateStart(doctrine);
        lastFailure = failure;
        if (failure != UltraProjectOperationFailure.None)
            return false;

        UltraProjectStageDefinition stage = GetCurrentStageDefinition();
        if (stage == null ||
            (!state.LaunchFeePaid && !HasResources(stage.OneTimeResourceCosts)))
        {
            failure = UltraProjectOperationFailure.InsufficientStartupResources;
            lastFailure = failure;
            return false;
        }

        Dictionary<Resource, ExpantaNum> costs = state.LaunchFeePaid
            ? new Dictionary<Resource, ExpantaNum>()
            : ToCostDictionary(stage.OneTimeResourceCosts);
        UltraProjectStateSaveData previousState = state.CaptureSaveData();
        if (!ResourceManager.Instance.TryApplyAtomicPayment(
                costs,
                () =>
                {
                    if (state.Status == UltraProjectStatus.Locked)
                        state.InitializeNew(doctrine);
                    else if (state.Doctrine != doctrine)
                        state.ChangeDoctrine(doctrine);
                    state.Start();
                },
                () => state.RestoreForTransaction(previousState)))
        {
            failure = UltraProjectOperationFailure.InsufficientStartupResources;
            lastFailure = failure;
            return false;
        }

        lastFailure = UltraProjectOperationFailure.None;
        return true;
    }

    public bool TryPause(out UltraProjectOperationFailure failure)
    {
        failure = state.Status == UltraProjectStatus.Running
            ? UltraProjectOperationFailure.None
            : UltraProjectOperationFailure.InvalidState;
        if (failure == UltraProjectOperationFailure.None)
            state.Pause();
        lastFailure = failure;
        return failure == UltraProjectOperationFailure.None;
    }

    public bool TryResume(out UltraProjectOperationFailure failure)
    {
        failure = state.Status == UltraProjectStatus.Paused
            ? UltraProjectOperationFailure.None
            : UltraProjectOperationFailure.InvalidState;
        if (failure == UltraProjectOperationFailure.None &&
            state.PauseReason == UltraProjectPauseReason.InsufficientSupply)
        {
            UltraProjectStageDefinition stage = GetCurrentStageDefinition();
            if (stage == null)
                failure = UltraProjectOperationFailure.DefinitionMissing;
            else if (CalculateSatisfaction(stage, 1d, state.Doctrine) <= ExpantaNum.Zero)
                failure = UltraProjectOperationFailure.InsufficientSupply;
        }
        if (failure == UltraProjectOperationFailure.None)
            state.Resume();
        lastFailure = failure;
        return failure == UltraProjectOperationFailure.None;
    }

    public bool TryAbandon(out UltraProjectOperationFailure failure)
    {
        failure = state.HasActiveProject
            ? UltraProjectOperationFailure.None
            : UltraProjectOperationFailure.InvalidState;
        if (failure == UltraProjectOperationFailure.None)
            state.Abandon();
        lastFailure = failure;
        return failure == UltraProjectOperationFailure.None;
    }

    public bool TryCommitCompletedStage(out UltraProjectOperationFailure failure)
    {
        if (state.Status == UltraProjectStatus.Committed)
        {
            failure = UltraProjectOperationFailure.None;
            lastFailure = failure;
            return true;
        }

        failure = state.Status == UltraProjectStatus.ReadyToCommit
            ? UltraProjectOperationFailure.None
            : UltraProjectOperationFailure.InvalidState;
        if (failure == UltraProjectOperationFailure.None)
            state.Commit();
        lastFailure = failure;
        return failure == UltraProjectOperationFailure.None;
    }

    public bool TrySetOperationalDoctrine(
        UltraProjectDoctrine doctrine,
        out UltraProjectOperationFailure failure)
    {
        if (!Enum.IsDefined(typeof(UltraProjectDoctrine), doctrine) ||
            doctrine == UltraProjectDoctrine.None)
        {
            failure = UltraProjectOperationFailure.InvalidDoctrine;
        }
        else if (state.Status != UltraProjectStatus.Ready &&
                 state.Status != UltraProjectStatus.Paused &&
                 state.Status != UltraProjectStatus.Committed)
        {
            failure = UltraProjectOperationFailure.InvalidState;
        }
        else
        {
            failure = ValidateDoctrineUnlock(doctrine);
            if (failure == UltraProjectOperationFailure.None)
            {
                if (state.Status == UltraProjectStatus.Committed)
                    RestoreCompletedDoctrine(doctrine);
                else
                    state.ChangeDoctrine(doctrine);
            }
        }

        lastFailure = failure;
        return failure == UltraProjectOperationFailure.None;
    }

    public bool TrySetCampaignDoctrine(
        CampaignDoctrine doctrine,
        out UltraProjectOperationFailure failure)
    {
        if (!Enum.IsDefined(typeof(CampaignDoctrine), doctrine))
        {
            failure = UltraProjectOperationFailure.InvalidDoctrine;
        }
        else if (!IsCampaignDoctrineUnlocked)
        {
            failure = UltraProjectOperationFailure.CampaignDoctrineLocked;
        }
        else
        {
            GameManager.Instance.State.SetCampaignDoctrine(doctrine);
            failure = UltraProjectOperationFailure.None;
        }

        lastFailure = failure;
        return failure == UltraProjectOperationFailure.None;
    }

    public UltraProjectPreview GetPreview()
        => GetPreview(state.Doctrine == UltraProjectDoctrine.None ? UltraProjectDoctrine.Stable : state.Doctrine, false);

    public UltraProjectPreview GetPreview(UltraProjectDoctrine doctrine) => GetPreview(doctrine, true);

    private UltraProjectPreview GetPreview(UltraProjectDoctrine previewDoctrine, bool hypothetical)
    {
        UltraProjectStageDefinition stage = GetCurrentStageDefinition();
        UltraProjectOperationFailure failure = state.Status == UltraProjectStatus.Paused
            ? GetPauseFailure()
            : ValidateStart(previewDoctrine);
        if (!Enum.IsDefined(typeof(UltraProjectDoctrine), previewDoctrine) || previewDoctrine == UltraProjectDoctrine.None)
            failure = UltraProjectOperationFailure.InvalidDoctrine;
        else if (hypothetical && ValidateDoctrineUnlock(previewDoctrine) != UltraProjectOperationFailure.None)
            failure = ValidateDoctrineUnlock(previewDoctrine);
        if (failure == UltraProjectOperationFailure.None &&
            (state.Status == UltraProjectStatus.Locked ||
             state.Status == UltraProjectStatus.Ready) &&
            !state.LaunchFeePaid &&
            stage != null &&
            !HasResources(stage.OneTimeResourceCosts))
        {
            failure = UltraProjectOperationFailure.InsufficientStartupResources;
        }
        ExpantaNum satisfaction = stage == null ? ExpantaNum.Zero : CalculateSatisfaction(stage, 1d, previewDoctrine);
        ExpantaNum postureCostMultiplier = GetOperationalCostMultiplier(previewDoctrine);
        if (hypothetical && stage != null)
        {
            // Compare prospective load against current generation without changing project State.
            BuildingManager buildings = BuildingManager.Instance;
            GameState game = GameManager.Instance.State;
            ExpantaNum powerDemand = buildings.TotalPowerDemand - GetCurrentPowerConsumptionRate() + stage.PowerConsumptionRate * postureCostMultiplier;
            ExpantaNum logisticsDemand = buildings.TotalLogisticsDemand - GetCurrentLogisticsConsumptionRate() + stage.LogisticsConsumptionRate * postureCostMultiplier;
            satisfaction = ExpantaNum.Min(CalculateMaterialSatisfaction(stage, 1d, previewDoctrine),
                ExpantaNum.Min(powerDemand > ExpantaNum.Zero ? ExpantaNum.Clamp01(game.PowerProductionRate * game.HappinessRewardMultiplier / powerDemand) : ExpantaNum.One,
                    logisticsDemand > ExpantaNum.Zero ? ExpantaNum.Clamp01(game.LogisticsProductionRate * game.HappinessRewardMultiplier / logisticsDemand) : ExpantaNum.One));
        }
        ExpantaNum progressRate = stage == null || stage.BaseDurationSeconds <= ExpantaNum.Zero ? ExpantaNum.Zero :
            GetProgressMultiplier(stage, previewDoctrine) * satisfaction / stage.BaseDurationSeconds;
        return new UltraProjectPreview(
            state,
            stage,
            hypothetical ? previewDoctrine : state.Doctrine,
            progressRate,
            satisfaction,
            (state.Status == UltraProjectStatus.Locked ||
             state.Status == UltraProjectStatus.Ready) &&
                failure == UltraProjectOperationFailure.None,
            failure,
            ScaleCosts(stage?.ContinuousResourceCosts, postureCostMultiplier),
            (stage?.FoodConsumptionRate ?? ExpantaNum.Zero) * postureCostMultiplier,
            (stage?.PowerConsumptionRate ?? ExpantaNum.Zero) * postureCostMultiplier,
            (stage?.LogisticsConsumptionRate ?? ExpantaNum.Zero) * postureCostMultiplier);
    }

    public bool Tick(double deltaSeconds)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (state.Status != UltraProjectStatus.Running || deltaSeconds <= 0d)
            return false;

        UltraProjectStageDefinition stage = GetCurrentStageDefinition();
        if (stage == null || stage.BaseDurationSeconds <= ExpantaNum.Zero)
        {
            state.Pause(UltraProjectPauseReason.DefinitionMissing);
            lastFailure = UltraProjectOperationFailure.DefinitionMissing;
            return false;
        }

        ExpantaNum satisfaction = CalculateSatisfaction(stage, deltaSeconds, state.Doctrine);
        if (satisfaction <= ExpantaNum.Zero)
        {
            state.Pause(UltraProjectPauseReason.InsufficientSupply);
            lastFailure = UltraProjectOperationFailure.InsufficientSupply;
            return false;
        }

        ExpantaNum postureCostMultiplier = GetOperationalCostMultiplier();
        double paymentSeconds = deltaSeconds;
        ExpantaNum seconds = new ExpantaNum(paymentSeconds);
        ExpantaNum progressDelta = seconds /
            stage.BaseDurationSeconds * GetProgressMultiplier(stage) * satisfaction;
        ExpantaNum remainingProgress = ExpantaNum.One - state.StageProgress;
        if (progressDelta >= remainingProgress)
        {
            paymentSeconds = FindSecondsToCompleteStage(
                stage, deltaSeconds, remainingProgress, state.Doctrine);
            seconds = new ExpantaNum(paymentSeconds);
            satisfaction = CalculateSatisfaction(stage, paymentSeconds, state.Doctrine);
            progressDelta = ExpantaNum.Min(
                remainingProgress,
                seconds / stage.BaseDurationSeconds * GetProgressMultiplier(stage) * satisfaction);
        }
        Dictionary<Resource, ExpantaNum> payment = new();
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs = AggregateCosts(stage.ContinuousResourceCosts);
        for (int i = 0; i < costs.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = costs[i];
            if (cost.First == null || cost.Second <= ExpantaNum.Zero)
                continue;
            ExpantaNum amount = cost.Second * seconds * postureCostMultiplier * satisfaction;
            payment[cost.First] = payment.TryGetValue(cost.First, out ExpantaNum current)
                ? current + amount
                : amount;
        }

        ExpantaNum foodCost = stage.FoodConsumptionRate * seconds *
            postureCostMultiplier * satisfaction;
        ExpantaNum previousFood = GameManager.Instance.State.FoodAmount;
        ExpantaNum previousCapacity = GameManager.Instance.State.FoodCapacity;
        UltraProjectStateSaveData previousState = state.CaptureSaveData();
        bool committed = ResourceManager.Instance.TryApplyAtomicPayment(
            payment,
            () =>
            {
                if (!GameManager.Instance.State.TryConsumeFood(foodCost))
                    throw new InvalidOperationException("Ultra project Food payment was not available.");
                state.AdvanceStageProgress(progressDelta);
            },
            () =>
            {
                GameManager.Instance.State.RestoreFoodExact(previousFood, previousCapacity);
                state.RestoreForTransaction(previousState);
            });

        if (!committed)
        {
            state.Pause(UltraProjectPauseReason.InsufficientSupply);
            lastFailure = UltraProjectOperationFailure.InsufficientSupply;
            return false;
        }

        lastFailure = UltraProjectOperationFailure.None;
        return true;
    }

    internal UltraProjectStateSaveData CaptureSaveData() => state.CaptureSaveData();

    internal static void ValidateSaveDataForArchive(
        UltraProjectStateSaveData saveData)
    {
        if (saveData == null)
            throw new ArgumentNullException(nameof(saveData));
        if (saveData.Doctrine == UltraProjectDoctrine.Expedition &&
            saveData.Status != UltraProjectStatus.Committed)
        {
            throw new InvalidOperationException(
                "Expedition doctrine requires a committed Ultra civilization project.");
        }

        // Validate on an isolated state before SaveManager mutates any live
        // manager. A malformed Ultra segment is rejected like every other
        // invalid v9 section instead of being partially applied.
        UltraProjectState validator = new();
        validator.RestoreForTransaction(saveData);
    }

    internal void RestoreSaveData(UltraProjectStateSaveData saveData)
    {
        ValidateSaveDataForArchive(saveData);
        state.Restore(saveData);
        lastFailure = UltraProjectOperationFailure.None;
    }

    private UltraProjectOperationFailure GetPauseFailure()
    {
        switch (state.PauseReason)
        {
            case UltraProjectPauseReason.InsufficientSupply:
                return UltraProjectOperationFailure.InsufficientSupply;
            case UltraProjectPauseReason.DefinitionMissing:
                return UltraProjectOperationFailure.DefinitionMissing;
            case UltraProjectPauseReason.Manual:
            case UltraProjectPauseReason.None:
                return UltraProjectOperationFailure.None;
            default:
                return UltraProjectOperationFailure.InvalidState;
        }
    }

    private void RestoreCompletedDoctrine(UltraProjectDoctrine doctrine)
    {
        UltraProjectStateSaveData saveData = state.CaptureSaveData();
        saveData.Doctrine = doctrine;
        saveData.StateVersion = state.Version + 1;
        state.RestoreForTransaction(saveData);
    }

    private UltraProjectOperationFailure ValidateStart(UltraProjectDoctrine doctrine)
    {
        if (GameManager.Instance.State.TechLevel < TechLevel.Ultra)
            return UltraProjectOperationFailure.NotUltra;
        if (!Enum.IsDefined(typeof(UltraProjectDoctrine), doctrine) ||
            doctrine == UltraProjectDoctrine.None)
            return UltraProjectOperationFailure.InvalidDoctrine;
        if (state.Status != UltraProjectStatus.Locked &&
            state.Status != UltraProjectStatus.Ready)
            return UltraProjectOperationFailure.InvalidState;
        UltraProjectOperationFailure doctrineFailure = ValidateDoctrineUnlock(doctrine);
        if (doctrineFailure != UltraProjectOperationFailure.None)
            return doctrineFailure;
        UltraProjectStageDefinition stage = GetCurrentStageDefinition();
        if (stage == null)
            return UltraProjectOperationFailure.DefinitionMissing;
        if (!HasResearch(stage.RequiredResearch) || !HasAdditionalResearch(stage))
            return UltraProjectOperationFailure.PrerequisiteResearchMissing;
        if (!HasBuilding(stage.RequiredBuilding))
            return UltraProjectOperationFailure.RequiredBuildingMissing;
        return UltraProjectOperationFailure.None;
    }

    private UltraProjectStageDefinition GetCurrentStageDefinition()
    {
        UltraProjectStage lookupStage = state.CurrentStage == UltraProjectStage.None &&
            state.Status == UltraProjectStatus.Locked
            ? UltraProjectStage.Prototype
            : state.CurrentStage;
        if (lookupStage == UltraProjectStage.None ||
            lookupStage == UltraProjectStage.Completed)
            return null;
        string id = lookupStage == UltraProjectStage.Prototype
            ? "PhaseStabilityCertification"
            : state.CurrentStage == UltraProjectStage.Stabilization
                ? "MatterAutonomyCertification"
                : "CivilizationContinuityCertification";
        if (Definition == null || Definition.Stages == null)
            return null;
        IReadOnlyList<UltraProjectStageDefinition> stages = Definition.Stages;
        for (int i = 0; i < stages.Count; i++)
            if (stages[i] != null && string.Equals(stages[i].StageId, id, StringComparison.OrdinalIgnoreCase))
                return stages[i];
        return null;
    }

    private bool HasResearch(Research research) =>
        research != null && ResearchManager.Instance.IsResearchCompleted(research.Id);

    private bool HasAdditionalResearch(UltraProjectStageDefinition stage)
    {
        IReadOnlyList<Research> research = stage.AdditionalRequiredResearch;
        if (research == null)
            return true;
        for (int i = 0; i < research.Count; i++)
            if (!HasResearch(research[i]))
                return false;
        return true;
    }

    private bool HasBuilding(Building building)
    {
        return building != null &&
            BuildingManager.Instance.GetState(building).Amount > ExpantaNum.Zero;
    }

    private bool HasResources(IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
    {
        Dictionary<Resource, ExpantaNum> aggregatedCosts = ToCostDictionary(costs);
        foreach (KeyValuePair<Resource, ExpantaNum> cost in aggregatedCosts)
        {
            if (ResourceManager.Instance.GetAmount(cost.Key) < cost.Value)
                return false;
        }
        return true;
    }

    private UltraProjectOperationFailure ValidateDoctrineUnlock(UltraProjectDoctrine doctrine)
    {
        if (doctrine == UltraProjectDoctrine.Surge && state.CompletedStageCount < 1)
            return UltraProjectOperationFailure.StageLocked;
        if (doctrine == UltraProjectDoctrine.Expedition && !IsCampaignDoctrineUnlocked)
            return UltraProjectOperationFailure.CampaignDoctrineLocked;
        return UltraProjectOperationFailure.None;
    }

    private ExpantaNum CalculateSatisfaction(
        UltraProjectStageDefinition stage,
        double deltaSeconds,
        UltraProjectDoctrine doctrine)
    {
        ExpantaNum result = CalculateMaterialSatisfaction(stage, deltaSeconds, doctrine);
        result = ExpantaNum.Min(result, GameManager.Instance.State.PowerSatisfaction);
        result = ExpantaNum.Min(result, GameManager.Instance.State.LogisticsSatisfaction);
        return ExpantaNum.Clamp01(result);
    }

    private ExpantaNum CalculateMaterialSatisfaction(
        UltraProjectStageDefinition stage,
        double deltaSeconds,
        UltraProjectDoctrine doctrine)
    {
        if (stage == null)
            return ExpantaNum.Zero;
        ExpantaNum result = ExpantaNum.One;
        ExpantaNum seconds = new ExpantaNum(deltaSeconds);
        ExpantaNum costMultiplier = GetOperationalCostMultiplier(doctrine);
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs = AggregateCosts(stage.ContinuousResourceCosts);
        for (int i = 0; i < costs.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = costs[i];
            ExpantaNum demand = cost.Second * seconds * costMultiplier;
            if (demand > ExpantaNum.Zero)
                result = ExpantaNum.Min(result, ResourceManager.Instance.GetAmount(cost.First) / demand);
        }
        ExpantaNum foodDemand = stage.FoodConsumptionRate * seconds * costMultiplier;
        if (foodDemand > ExpantaNum.Zero)
            result = ExpantaNum.Min(result, GameManager.Instance.State.FoodAmount / foodDemand);
        return ExpantaNum.Clamp01(result);
    }

    private ExpantaNum GetProgressMultiplier(UltraProjectStageDefinition stage)
        => GetProgressMultiplier(stage, state.Doctrine);

    private static ExpantaNum GetProgressMultiplier(UltraProjectStageDefinition stage, UltraProjectDoctrine doctrine)
    {
        if (doctrine == UltraProjectDoctrine.Surge)
            return stage.RushPostureMultiplier;
        return doctrine == UltraProjectDoctrine.Expedition
            ? stage.RushPostureMultiplier * new ExpantaNum(1.1d)
            : stage.StablePostureMultiplier;
    }

    private ExpantaNum CalculateProgressRate(UltraProjectStageDefinition stage, ExpantaNum satisfaction)
    {
        if (stage == null || stage.BaseDurationSeconds <= ExpantaNum.Zero)
            return ExpantaNum.Zero;
        return GetProgressMultiplier(stage) * satisfaction / stage.BaseDurationSeconds;
    }

    private double FindSecondsToCompleteStage(
        UltraProjectStageDefinition stage,
        double maximumSeconds,
        ExpantaNum targetProgress,
        UltraProjectDoctrine doctrine)
    {
        double low = 0d;
        double high = maximumSeconds;
        for (int i = 0; i < 48; i++)
        {
            double middle = (low + high) * .5d;
            ExpantaNum satisfaction = CalculateSatisfaction(stage, middle, doctrine);
            ExpantaNum progress = new ExpantaNum(middle) /
                stage.BaseDurationSeconds * GetProgressMultiplier(stage) * satisfaction;
            if (progress >= targetProgress)
                high = middle;
            else
                low = middle;
        }
        return high;
    }

    internal ExpantaNum GetCurrentPowerConsumptionRate()
    {
        UltraProjectStageDefinition stage = GetCurrentStageDefinition();
        return state.Status == UltraProjectStatus.Running && stage != null
            ? stage.PowerConsumptionRate * GetOperationalCostMultiplier()
            : ExpantaNum.Zero;
    }

    internal ExpantaNum GetCurrentLogisticsConsumptionRate()
    {
        UltraProjectStageDefinition stage = GetCurrentStageDefinition();
        return state.Status == UltraProjectStatus.Running && stage != null
            ? stage.LogisticsConsumptionRate * GetOperationalCostMultiplier()
            : ExpantaNum.Zero;
    }

    private ExpantaNum GetOperationalCostMultiplier() =>
        GetOperationalCostMultiplier(state.Doctrine);

    private static ExpantaNum GetOperationalCostMultiplier(UltraProjectDoctrine doctrine) =>
        doctrine == UltraProjectDoctrine.Surge
            ? new ExpantaNum(1.25d)
            : doctrine == UltraProjectDoctrine.Expedition
                ? new ExpantaNum(1.35d)
                : ExpantaNum.One;

    private static IReadOnlyList<Pair<Resource, ExpantaNum>> AggregateCosts(
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
    {
        List<Pair<Resource, ExpantaNum>> result = new();
        Dictionary<Resource, int> indexes = new();
        if (costs == null)
            return result;

        for (int i = 0; i < costs.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = costs[i];
            if (cost.First == null || cost.Second <= ExpantaNum.Zero)
                continue;

            if (indexes.TryGetValue(cost.First, out int index))
            {
                Pair<Resource, ExpantaNum> current = result[index];
                result[index] = new Pair<Resource, ExpantaNum>(
                    current.First,
                    current.Second + cost.Second);
            }
            else
            {
                indexes.Add(cost.First, result.Count);
                result.Add(cost);
            }
        }

        return result;
    }

    private static IReadOnlyList<Pair<Resource, ExpantaNum>> ScaleCosts(
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs,
        ExpantaNum multiplier)
    {
        IReadOnlyList<Pair<Resource, ExpantaNum>> aggregated = AggregateCosts(costs);
        List<Pair<Resource, ExpantaNum>> result = new(aggregated.Count);
        for (int i = 0; i < aggregated.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = aggregated[i];
            result.Add(new Pair<Resource, ExpantaNum>(
                cost.First,
                cost.Second * multiplier));
        }
        return result;
    }

    private static Dictionary<Resource, ExpantaNum> ToCostDictionary(
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
    {
        Dictionary<Resource, ExpantaNum> result = new();
        IReadOnlyList<Pair<Resource, ExpantaNum>> aggregated = AggregateCosts(costs);
        for (int i = 0; i < aggregated.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = aggregated[i];
            result.Add(cost.First, cost.Second);
        }
        return result;
    }

#if UNITY_EDITOR
    public void RestoreSaveDataForEditor(UltraProjectStateSaveData saveData) =>
        RestoreSaveData(saveData);
#endif
}
