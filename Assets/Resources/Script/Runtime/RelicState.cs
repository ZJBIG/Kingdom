using System;

public enum RelicStatus
{
    Discovered,
    Investigating,
    AwaitingChoice,
    Repairing,
    ReverseEngineering,
    Operational
}

public enum RelicRoute { None, Repair, Dismantle }
public enum RelicPauseReason { None, Manual, InsufficientSupply }

[Serializable]
public sealed class RelicStateSaveData
{
    public string RelicId;
    public int SaveVersion;
    public RelicStatus Status;
    public RelicRoute Route;
    public string Progress;
    public bool Suspended;
    public RelicPauseReason PauseReason;
    public bool CommissionActive;
    public bool SupportReady;
    public string SupportedSectorId;
    public int CompletedCommissions;
    public int StateVersion;
}

[Serializable]
public sealed class RelicState
{
    public const string RelicId = "EchoFoundryRing";
    public const int CurrentSaveVersion = 1;

    public RelicStatus Status { get; private set; }
    public RelicRoute Route { get; private set; }
    public ExpantaNum Progress { get; private set; }
    public bool Suspended { get; private set; }
    public RelicPauseReason PauseReason { get; private set; }
    public bool CommissionActive { get; private set; }
    public bool SupportReady { get; private set; }
    public string SupportedSectorId { get; private set; }
    public int CompletedCommissions { get; private set; }
    public int Version { get; private set; }

    public RelicState() => InitializeNew();

    internal void InitializeNew()
    {
        Status = RelicStatus.Discovered;
        Route = RelicRoute.None;
        Progress = ExpantaNum.Zero;
        Suspended = false;
        PauseReason = RelicPauseReason.None;
        CommissionActive = false;
        SupportReady = false;
        SupportedSectorId = string.Empty;
        CompletedCommissions = 0;
        Version++;
    }

    internal void StartInvestigation()
    {
        Require(!Suspended && Status == RelicStatus.Discovered);
        Status = RelicStatus.Investigating;
        Version++;
    }

    internal void ChooseRoute(RelicRoute route)
    {
        if (route != RelicRoute.Repair && route != RelicRoute.Dismantle)
            throw new ArgumentOutOfRangeException(nameof(route));
        Require(!Suspended && Status == RelicStatus.AwaitingChoice && Route == RelicRoute.None);
        Route = route;
        Status = route == RelicRoute.Repair ? RelicStatus.Repairing : RelicStatus.ReverseEngineering;
        Version++;
    }

    internal void Advance(ExpantaNum progressDelta)
    {
        if (!progressDelta.IsFinite || progressDelta < ExpantaNum.Zero)
            throw new ArgumentOutOfRangeException(nameof(progressDelta));
        Require(!Suspended && HasActiveProgress(Status, CommissionActive));
        if (progressDelta == ExpantaNum.Zero)
            return;
        ExpantaNum next = Progress + progressDelta;
        if (!next.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(progressDelta));
        bool completed = next >= ExpantaNum.One;
        if (completed && CommissionActive && CompletedCommissions == int.MaxValue)
            throw new InvalidOperationException("The relic commission count cannot increase further.");
        Progress = completed ? ExpantaNum.Zero : next;
        if (completed)
        {
            if (Status == RelicStatus.Investigating)
                Status = RelicStatus.AwaitingChoice;
            else if (CommissionActive)
            {
                CommissionActive = false;
                SupportReady = true;
                CompletedCommissions++;
            }
            else
                Status = RelicStatus.Operational;
        }
        Version++;
    }

    internal void Suspend(RelicPauseReason reason)
    {
        if (reason != RelicPauseReason.Manual && reason != RelicPauseReason.InsufficientSupply)
            throw new ArgumentOutOfRangeException(nameof(reason));
        Require(!Suspended && (reason == RelicPauseReason.Manual || HasActiveProgress(Status, CommissionActive)));
        Suspended = true;
        PauseReason = reason;
        Version++;
    }

    internal void Resume()
    {
        Require(Suspended);
        Suspended = false;
        PauseReason = RelicPauseReason.None;
        Version++;
    }

    internal void BeginCommission()
    {
        Require(CanPrepareSupport() && Route == RelicRoute.Repair);
        CommissionActive = true;
        Version++;
    }

    internal void PrepareSupport()
    {
        Require(CanPrepareSupport() && Route == RelicRoute.Dismantle);
        SupportReady = true;
        Version++;
    }

    internal void ConsumeSupport(string sectorId)
    {
        if (string.IsNullOrWhiteSpace(sectorId) || !string.Equals(sectorId, sectorId.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("A stable sector ID is required.", nameof(sectorId));
        Require(!Suspended && Status == RelicStatus.Operational && SupportReady && string.IsNullOrEmpty(SupportedSectorId));
        SupportReady = false;
        SupportedSectorId = sectorId;
        Version++;
    }

    internal void ClearSupportedCampaign()
    {
        if (string.IsNullOrEmpty(SupportedSectorId))
            return;
        SupportedSectorId = string.Empty;
        Version++;
    }

    internal RelicStateSaveData CaptureSaveData() => new RelicStateSaveData
    {
        RelicId = RelicId,
        SaveVersion = CurrentSaveVersion,
        Status = Status,
        Route = Route,
        Progress = Progress.ToString(),
        Suspended = Suspended,
        PauseReason = PauseReason,
        CommissionActive = CommissionActive,
        SupportReady = SupportReady,
        SupportedSectorId = SupportedSectorId,
        CompletedCommissions = CompletedCommissions,
        StateVersion = Version
    };

    internal void Restore(RelicStateSaveData data)
    {
        Validate(data, out ExpantaNum progress);
        Status = data.Status;
        Route = data.Route;
        Progress = progress;
        Suspended = data.Suspended;
        PauseReason = data.PauseReason;
        CommissionActive = data.CommissionActive;
        SupportReady = data.SupportReady;
        SupportedSectorId = data.SupportedSectorId ?? string.Empty;
        CompletedCommissions = data.CompletedCommissions;
        Version = data.StateVersion;
    }

    internal void RestoreForTransaction(RelicStateSaveData data) => Restore(data);

    private bool CanPrepareSupport() => !Suspended && Status == RelicStatus.Operational &&
        !CommissionActive && !SupportReady && string.IsNullOrEmpty(SupportedSectorId);

    private static bool HasActiveProgress(RelicStatus status, bool commissionActive) =>
        status == RelicStatus.Investigating || status == RelicStatus.Repairing ||
        status == RelicStatus.ReverseEngineering || (status == RelicStatus.Operational && commissionActive);

    private static void Validate(RelicStateSaveData data, out ExpantaNum progress)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        Require(data.SaveVersion == CurrentSaveVersion && string.Equals(data.RelicId, RelicId, StringComparison.Ordinal));
        Require(data.StateVersion > 0 && data.CompletedCommissions >= 0);
        Require(data.Status >= RelicStatus.Discovered && data.Status <= RelicStatus.Operational);
        Require(data.Route >= RelicRoute.None && data.Route <= RelicRoute.Dismantle);
        Require(data.PauseReason >= RelicPauseReason.None && data.PauseReason <= RelicPauseReason.InsufficientSupply);
        if (!ExpantaNum.TryParse(data.Progress, out progress) || !progress.IsFinite ||
            progress < ExpantaNum.Zero || progress >= ExpantaNum.One)
            throw new ArgumentOutOfRangeException(nameof(data.Progress));
        Require(data.Suspended == (data.PauseReason != RelicPauseReason.None));
        Require(data.PauseReason != RelicPauseReason.InsufficientSupply || HasActiveProgress(data.Status, data.CommissionActive));
        bool beforeChoice = data.Status <= RelicStatus.AwaitingChoice;
        Require(beforeChoice ? data.Route == RelicRoute.None : data.Route != RelicRoute.None);
        Require(data.Status != RelicStatus.Repairing || data.Route == RelicRoute.Repair);
        Require(data.Status != RelicStatus.ReverseEngineering || data.Route == RelicRoute.Dismantle);
        Require(!data.CommissionActive || (data.Status == RelicStatus.Operational && data.Route == RelicRoute.Repair));
        Require(HasActiveProgress(data.Status, data.CommissionActive) || progress == ExpantaNum.Zero);
        bool hasSupportedSector = !string.IsNullOrEmpty(data.SupportedSectorId);
        Require(!hasSupportedSector || (!string.IsNullOrWhiteSpace(data.SupportedSectorId) &&
            string.Equals(data.SupportedSectorId, data.SupportedSectorId.Trim(), StringComparison.Ordinal)));
        Require(data.Status == RelicStatus.Operational || (!data.SupportReady && !hasSupportedSector && data.CompletedCommissions == 0));
        Require(!(data.SupportReady && hasSupportedSector));
        Require(!data.CommissionActive || (!data.SupportReady && !hasSupportedSector));
        Require(data.Route == RelicRoute.Repair || data.CompletedCommissions == 0);
    }

    private static void Require(bool condition)
    {
        if (!condition)
            throw new InvalidOperationException("The relic state transition or save combination is invalid.");
    }

#if UNITY_EDITOR
    public void InitializeNewForEditor() => InitializeNew();
    public void StartInvestigationForEditor() => StartInvestigation();
    public void ChooseRouteForEditor(RelicRoute route) => ChooseRoute(route);
    public void AdvanceForEditor(ExpantaNum delta) => Advance(delta);
    public void SuspendForEditor(RelicPauseReason reason) => Suspend(reason);
    public void ResumeForEditor() => Resume();
    public void BeginCommissionForEditor() => BeginCommission();
    public void PrepareSupportForEditor() => PrepareSupport();
    public void ConsumeSupportForEditor(string sectorId) => ConsumeSupport(sectorId);
    public void ClearSupportedCampaignForEditor() => ClearSupportedCampaign();
    public RelicStateSaveData CaptureSaveDataForEditor() => CaptureSaveData();
    public void RestoreForEditor(RelicStateSaveData data) => Restore(data);
#endif
}
