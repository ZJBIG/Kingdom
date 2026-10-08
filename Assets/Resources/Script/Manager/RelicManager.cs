using System;
using System.Collections.Generic;

public enum RelicOperationFailure
{
    None, DefinitionMissing, NotUltra, SectorNotOccupied, PrerequisiteResearchMissing,
    RequiredBuildingMissing, CertificationMissing, InvalidState, InsufficientStartupResources,
    InsufficientSupply, SupportAlreadyPrepared, CampaignMissing, InvalidRoute
}

public sealed class RelicManager
{
    private readonly RelicState state = new();
    private RelicDefinition definition;
    public RelicState State => state;
    public RelicDefinition Definition
    {
        get
        {
            if (definition == null)
                DataBase<RelicDefinition>.TryFind(RelicState.RelicId, out definition);
            return definition;
        }
    }
    private bool IsWorking => !state.Suspended &&
        (state.Status == RelicStatus.Investigating || state.Status == RelicStatus.Repairing ||
         state.Status == RelicStatus.ReverseEngineering || state.CommissionActive);
    public RelicWorkDefinition GetCurrentWork()
    {
        RelicDefinition data = Definition;
        if (data == null) return null;
        switch (state.Status)
        {
            case RelicStatus.Discovered:
            case RelicStatus.Investigating: return data.Investigation;
            case RelicStatus.Repairing: return data.Repair;
            case RelicStatus.ReverseEngineering: return data.ReverseEngineering;
            case RelicStatus.Operational: return state.Route == RelicRoute.Repair ? data.Commission : null;
            default: return null;
        }
    }
    public IReadOnlyList<Pair<Resource, ExpantaNum>> GetActiveCosts() => IsWorking
        ? GetCurrentWork()?.ContinuousCosts ?? Array.Empty<Pair<Resource, ExpantaNum>>()
        : Array.Empty<Pair<Resource, ExpantaNum>>();
    public ExpantaNum CurrentFoodConsumptionRate => IsWorking ? GetCurrentWork()?.FoodConsumptionRate ?? ExpantaNum.Zero : ExpantaNum.Zero;
    public ExpantaNum CurrentPowerConsumptionRate => IsWorking ? GetCurrentWork()?.PowerConsumptionRate ?? ExpantaNum.Zero : ExpantaNum.Zero;
    public ExpantaNum CurrentLogisticsConsumptionRate => IsWorking ? GetCurrentWork()?.LogisticsConsumptionRate ?? ExpantaNum.Zero : ExpantaNum.Zero;

    public RelicOperationFailure GetAvailabilityFailure() => GetAvailabilityFailure(true);
    private RelicOperationFailure GetAvailabilityFailure(bool requireBuildings)
    {
        RelicDefinition data = Definition;
        if (data == null || data.Sector == null) return RelicOperationFailure.DefinitionMissing;
        if (GameManager.Instance.State.TechLevel < TechLevel.Ultra) return RelicOperationFailure.NotUltra;
        if (!GameManager.Instance.Sectors.GetState(data.Sector).Occupied) return RelicOperationFailure.SectorNotOccupied;
        for (int i = 0; i < data.RequiredResearch.Count; i++)
            if (data.RequiredResearch[i] == null || !ResearchManager.Instance.IsResearchCompleted(data.RequiredResearch[i].Id))
                return RelicOperationFailure.PrerequisiteResearchMissing;
        for (int i = 0; requireBuildings && i < data.RequiredBuildings.Count; i++)
            if (data.RequiredBuildings[i] == null || BuildingManager.Instance.GetState(data.RequiredBuildings[i]).Amount < ExpantaNum.One)
                return RelicOperationFailure.RequiredBuildingMissing;
        if (!GameManager.Instance.UltraProject.State.IsStageCompleted(UltraProjectStage.Prototype))
            return RelicOperationFailure.CertificationMissing;
        return RelicOperationFailure.None;
    }

    public bool TryInvestigate(out RelicOperationFailure failure) =>
        TryStart(state.Status == RelicStatus.Discovered && !state.Suspended, Definition?.Investigation,
            state.StartInvestigation, out failure);
    public bool TryChooseRoute(RelicRoute route, out RelicOperationFailure failure)
    {
        if (route != RelicRoute.Repair && route != RelicRoute.Dismantle)
        { failure = RelicOperationFailure.InvalidRoute; return false; }
        return TryStart(state.Status == RelicStatus.AwaitingChoice && !state.Suspended,
            route == RelicRoute.Repair ? Definition?.Repair : Definition?.ReverseEngineering,
            () => state.ChooseRoute(route), out failure);
    }
    public bool TrySuspend(out RelicOperationFailure failure)
    {
        failure = state.Suspended ? RelicOperationFailure.InvalidState : RelicOperationFailure.None;
        if (failure != RelicOperationFailure.None) return false;
        state.Suspend(RelicPauseReason.Manual);
        return true;
    }
    public bool TryResume(out RelicOperationFailure failure)
    {
        failure = !state.Suspended ? RelicOperationFailure.InvalidState : GetAvailabilityFailure();
        if (failure == RelicOperationFailure.None && HasProgress() && CalculateSatisfaction(GetCurrentWork(), ExpantaNum.One) <= ExpantaNum.Zero)
            failure = RelicOperationFailure.InsufficientSupply;
        if (failure != RelicOperationFailure.None) return false;
        state.Resume();
        return true;
    }
    public bool TryBeginCommission(out RelicOperationFailure failure)
    {
        return TryStart(CanPrepareSupport(RelicRoute.Repair), Definition?.Commission,
            state.BeginCommission, out failure);
    }
    public bool TryCraftSupport(out RelicOperationFailure failure)
    {
        if (!CanCraftSupport(out failure)) return false;
        return TryPay(Definition.SupportCraftCosts, state.PrepareSupport, out failure);
    }
    public bool CanCraftSupport(out RelicOperationFailure failure)
    {
        failure = CanPrepareSupport(RelicRoute.Dismantle) ? GetAvailabilityFailure() : RelicOperationFailure.InvalidState;
        if (failure != RelicOperationFailure.None) return false;
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs = Definition.SupportCraftCosts;
        ValidateCosts(costs);
        for (int i = 0; i < costs.Count; i++)
            if (ResourceManager.Instance.GetAmount(costs[i].First) < costs[i].Second)
            { failure = RelicOperationFailure.InsufficientStartupResources; return false; }
        return true;
    }
    public bool TryAssignSupport(out RelicOperationFailure failure)
    {
        failure = GetAvailabilityFailure();
        if (failure != RelicOperationFailure.None) return false;
        if (state.Suspended || !state.SupportReady)
        { failure = RelicOperationFailure.InvalidState; return false; }
        var campaign = GameManager.Instance.State.Campaign;
        if (!campaign.Active || !DataBase<SectorDefinition>.TryFind(campaign.TargetSectorId, out SectorDefinition target) || target.IsHomeSystem)
        { failure = RelicOperationFailure.CampaignMissing; return false; }
        state.ConsumeSupport(target.Id);
        return true;
    }
    public ExpantaNum GetCampaignSupplyMultiplier(SectorDefinition sector)
    {
        var campaign = GameManager.Instance.State.Campaign;
        if (sector == null || sector.IsHomeSystem || !campaign.Active || Definition == null ||
            !string.Equals(campaign.TargetSectorId, sector.Id, StringComparison.Ordinal) ||
            !string.Equals(state.SupportedSectorId, sector.Id, StringComparison.Ordinal)) return ExpantaNum.One;
        ExpantaNum multiplier = Definition.CampaignSupplyMultiplier;
        if (!multiplier.IsFinite || multiplier <= ExpantaNum.Zero || multiplier > ExpantaNum.One)
            throw new InvalidOperationException("Invalid relic campaign supply multiplier.");
        return multiplier;
    }
    public void RefreshCampaignSupport()
    {
        var campaign = GameManager.Instance.State.Campaign;
        if (!string.IsNullOrEmpty(state.SupportedSectorId) &&
            (!campaign.Active || !string.Equals(campaign.TargetSectorId, state.SupportedSectorId, StringComparison.Ordinal)))
            state.ClearSupportedCampaign();
    }

    public bool Tick(double deltaSeconds)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        RefreshCampaignSupport();
        if (!IsWorking || deltaSeconds <= 0d) return false;
        if (GetAvailabilityFailure() != RelicOperationFailure.None)
        { state.Suspend(RelicPauseReason.InsufficientSupply); return false; }
        RelicWorkDefinition work = GetCurrentWork();
        ValidateWork(work);
        ExpantaNum seconds = new ExpantaNum(deltaSeconds);
        ExpantaNum satisfaction = CalculateSatisfaction(work, seconds);
        if (satisfaction <= ExpantaNum.Zero)
        { state.Suspend(RelicPauseReason.InsufficientSupply); return false; }
        ExpantaNum remainingProgress = ExpantaNum.One - state.Progress;
        ExpantaNum remainingSeconds = remainingProgress * work.DurationSeconds;
        ExpantaNum paidSeconds = ExpantaNum.Min(seconds * satisfaction, remainingSeconds);
        ExpantaNum progressDelta = paidSeconds >= remainingSeconds ? remainingProgress : paidSeconds / work.DurationSeconds;
        Dictionary<Resource, ExpantaNum> payment = Costs(work.ContinuousCosts, paidSeconds);
        ExpantaNum food = work.FoodConsumptionRate * paidSeconds;
        ExpantaNum previousFood = GameManager.Instance.State.FoodAmount;
        ExpantaNum previousCapacity = GameManager.Instance.State.FoodCapacity;
        RelicStateSaveData previous = state.CaptureSaveData();
        bool committed = ResourceManager.Instance.TryApplyAtomicPayment(payment, () =>
        {
            if (!GameManager.Instance.State.TryConsumeFood(food)) throw new InvalidOperationException("Relic Food payment unavailable.");
            state.Advance(progressDelta);
        }, () =>
        {
            GameManager.Instance.State.RestoreFoodExact(previousFood, previousCapacity);
            state.RestoreForTransaction(previous);
        });
        if (!committed) state.Suspend(RelicPauseReason.InsufficientSupply);
        return committed;
    }
    private bool HasProgress() => state.Status == RelicStatus.Investigating || state.Status == RelicStatus.Repairing ||
        state.Status == RelicStatus.ReverseEngineering || state.CommissionActive;
    private bool CanPrepareSupport(RelicRoute route) => state.Status == RelicStatus.Operational && state.Route == route &&
        !state.Suspended && !state.CommissionActive && !state.SupportReady && string.IsNullOrEmpty(state.SupportedSectorId);
    private bool TryStart(bool valid, RelicWorkDefinition work, Action commit, out RelicOperationFailure failure)
    {
        failure = valid ? GetAvailabilityFailure() : RelicOperationFailure.InvalidState;
        if (failure != RelicOperationFailure.None) return false;
        ValidateWork(work);
        if (CalculateSatisfaction(work, ExpantaNum.One) <= ExpantaNum.Zero)
        { failure = RelicOperationFailure.InsufficientSupply; return false; }
        return TryPay(work.StartupCosts, commit, out failure);
    }
    private bool TryPay(IReadOnlyList<Pair<Resource, ExpantaNum>> costs, Action commit, out RelicOperationFailure failure)
    {
        RelicStateSaveData previous = state.CaptureSaveData();
        bool result = ResourceManager.Instance.TryApplyAtomicPayment(Costs(costs, ExpantaNum.One), commit,
            () => state.RestoreForTransaction(previous));
        failure = result ? RelicOperationFailure.None : RelicOperationFailure.InsufficientStartupResources;
        return result;
    }
    private static Dictionary<Resource, ExpantaNum> Costs(IReadOnlyList<Pair<Resource, ExpantaNum>> costs, ExpantaNum scale)
    {
        ValidateCosts(costs);
        Dictionary<Resource, ExpantaNum> result = new();
        if (costs == null) throw new InvalidOperationException("Missing relic resource costs.");
        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.First == null || !cost.Second.IsFinite || cost.Second <= ExpantaNum.Zero || result.ContainsKey(cost.First))
                throw new InvalidOperationException("Invalid or duplicate relic resource cost.");
            result.Add(cost.First, cost.Second * scale);
        }
        return result;
    }
    private static void ValidateWork(RelicWorkDefinition work)
    {
        if (work == null || !work.DurationSeconds.IsFinite || work.DurationSeconds <= ExpantaNum.Zero ||
            !work.FoodConsumptionRate.IsFinite || work.FoodConsumptionRate < ExpantaNum.Zero ||
            !work.PowerConsumptionRate.IsFinite || work.PowerConsumptionRate < ExpantaNum.Zero ||
            !work.LogisticsConsumptionRate.IsFinite || work.LogisticsConsumptionRate < ExpantaNum.Zero)
            throw new InvalidOperationException("Invalid relic work definition.");
        ValidateCosts(work.StartupCosts);
        ValidateCosts(work.ContinuousCosts);
    }
    private static void ValidateCosts(IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
    {
        if (costs == null) throw new InvalidOperationException("Missing relic costs.");
        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (cost.First == null || string.IsNullOrWhiteSpace(cost.First.Id) || !cost.Second.IsFinite || cost.Second <= ExpantaNum.Zero)
                throw new InvalidOperationException("Invalid relic cost.");
            for (int j = 0; j < i; j++)
                if (string.Equals(costs[j].First.Id, cost.First.Id, StringComparison.Ordinal))
                    throw new InvalidOperationException("Duplicate relic resource cost.");
        }
    }
    private static ExpantaNum CalculateSatisfaction(RelicWorkDefinition work, ExpantaNum seconds)
    {
        ValidateWork(work);
        ExpantaNum result = ExpantaNum.One;
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs = work.ContinuousCosts;
        for (int i = 0; i < costs.Count; i++)
            result = ExpantaNum.Min(result, ResourceManager.Instance.GetAmount(costs[i].First) / (costs[i].Second * seconds));
        ExpantaNum food = work.FoodConsumptionRate * seconds;
        if (food > ExpantaNum.Zero) result = ExpantaNum.Min(result, GameManager.Instance.State.FoodAmount / food);
        if (work.PowerConsumptionRate > ExpantaNum.Zero) result = ExpantaNum.Min(result, GameManager.Instance.State.PowerSatisfaction);
        if (work.LogisticsConsumptionRate > ExpantaNum.Zero) result = ExpantaNum.Min(result, GameManager.Instance.State.LogisticsSatisfaction);
        return ExpantaNum.Clamp01(result);
    }
    internal void InitializeNew() => state.InitializeNew();
    internal RelicStateSaveData CaptureSaveData() { RefreshCampaignSupport(); return state.CaptureSaveData(); }
    internal static void ValidateSaveDataForArchive(RelicStateSaveData data)
    {
        new RelicState().RestoreForTransaction(data);
        if (!string.IsNullOrEmpty(data.SupportedSectorId) &&
            (!DataBase<SectorDefinition>.TryFind(data.SupportedSectorId, out SectorDefinition target) || target.IsHomeSystem))
            throw new InvalidOperationException("Invalid relic supported campaign target.");
    }
    internal void RestoreSaveData(RelicStateSaveData data)
    {
        ValidateSaveDataForArchive(data);
        if (data.Status != RelicStatus.Discovered)
        {
            RelicOperationFailure availability = GetAvailabilityFailure(false);
            if (availability != RelicOperationFailure.None)
                throw new InvalidOperationException("Saved relic progress requires its permanent prerequisites.");
        }
        var campaign = GameManager.Instance.State.Campaign;
        if (!string.IsNullOrEmpty(data.SupportedSectorId) &&
            (!campaign.Active || !string.Equals(campaign.TargetSectorId, data.SupportedSectorId, StringComparison.Ordinal)))
            throw new InvalidOperationException("Saved relic support requires the matching active campaign.");
        state.Restore(data);
    }
#if UNITY_EDITOR
    public void InitializeNewForEditor() => InitializeNew();
    public RelicStateSaveData CaptureSaveDataForEditor() => CaptureSaveData();
    public void RestoreSaveDataForEditor(RelicStateSaveData data) => RestoreSaveData(data);
    public static void ValidateSaveDataForArchiveForEditor(RelicStateSaveData data) => ValidateSaveDataForArchive(data);
#endif
}
