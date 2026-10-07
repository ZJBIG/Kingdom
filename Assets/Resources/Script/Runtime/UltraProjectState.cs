using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public enum UltraProjectStage
{
    None = 0,
    Prototype = 1,
    Stabilization = 2,
    Expansion = 3,
    Completed = 4
}

public enum UltraProjectDoctrine
{
    None = 0,
    Stable = 1,
    Surge = 2,
    Expedition = 3,
    PhaseStability = Stable,
    DeepSpaceIndustry = Surge,
    GatewayHub = Expedition
}

public enum UltraProjectStatus
{
    Locked = 0,
    Ready = 1,
    Running = 2,
    Paused = 3,
    ReadyToCommit = 4,
    Committed = 5
}

public enum UltraProjectPauseReason
{
    None = 0,
    Manual = 1,
    InsufficientSupply = 2,
    DefinitionMissing = 3
}

[Serializable]
public sealed class UltraProjectStateSaveData
{
    public string ProjectId;
    public int SaveVersion;
    public UltraProjectDoctrine Doctrine;
    public UltraProjectStatus Status;
    public UltraProjectStage CurrentStage;
    public string StageProgress;
    public List<UltraProjectStage> CompletedStages;
    public bool LaunchFeePaid;
    public UltraProjectPauseReason PauseReason;
    public int StateVersion;
}

[Serializable]
public sealed class UltraProjectState
{
    public const string ProjectId = "UltraCivilizationEngineering";
    public const int CurrentSaveVersion = 1;
    public const int StageCount = 3;

    private static readonly UltraProjectStage[] OrderedStages =
    {
        UltraProjectStage.Prototype,
        UltraProjectStage.Stabilization,
        UltraProjectStage.Expansion
    };

    private readonly List<UltraProjectStage> completedStages = new();
    private readonly ReadOnlyCollection<UltraProjectStage> completedStagesView;

    public UltraProjectDoctrine Doctrine { get; private set; }
    public UltraProjectStatus Status { get; private set; }
    public UltraProjectStage CurrentStage { get; private set; }
    public ExpantaNum StageProgress { get; private set; }
    public IReadOnlyList<UltraProjectStage> CompletedStages => completedStagesView;
    public int CompletedStageCount => completedStages.Count;
    public bool LaunchFeePaid { get; private set; }
    public UltraProjectPauseReason PauseReason { get; private set; }
    public int Version { get; private set; }

    public bool IsComplete => Status == UltraProjectStatus.Committed;
    public bool HasActiveProject =>
        Status == UltraProjectStatus.Running ||
        Status == UltraProjectStatus.Paused;

    public UltraProjectState()
    {
        completedStagesView = new ReadOnlyCollection<UltraProjectStage>(completedStages);
        InitializeNew();
    }

    public UltraProjectState(UltraProjectDoctrine doctrine)
    {
        completedStagesView = new ReadOnlyCollection<UltraProjectStage>(completedStages);
        InitializeNew(doctrine);
    }

    internal void InitializeNew()
    {
        Doctrine = UltraProjectDoctrine.None;
        Status = UltraProjectStatus.Locked;
        CurrentStage = UltraProjectStage.None;
        StageProgress = ExpantaNum.Zero;
        completedStages.Clear();
        LaunchFeePaid = false;
        PauseReason = UltraProjectPauseReason.None;
        Version++;
    }

    internal void InitializeNew(UltraProjectDoctrine doctrine)
    {
        ValidateDoctrineForSelection(doctrine);
        Doctrine = doctrine;
        Status = UltraProjectStatus.Ready;
        CurrentStage = OrderedStages[0];
        StageProgress = ExpantaNum.Zero;
        completedStages.Clear();
        LaunchFeePaid = false;
        PauseReason = UltraProjectPauseReason.None;
        Version++;
    }

    internal void Start(UltraProjectDoctrine doctrine)
    {
        if (Status == UltraProjectStatus.Locked)
            InitializeNew(doctrine);
        else if (Doctrine != doctrine)
            throw new InvalidOperationException(
                "An Ultra project cannot change doctrine after selection.");

        Start();
    }

    internal void Start()
    {
        if (Status != UltraProjectStatus.Ready)
            throw new InvalidOperationException(
                "An Ultra project can only start from the Ready state.");
        if (!IsValidDoctrine(Doctrine))
            throw new InvalidOperationException(
                "An Ultra project must have a doctrine before it starts.");
        if (!IsValidProgress(StageProgress) || StageProgress != ExpantaNum.Zero)
            throw new InvalidOperationException(
                "A project can only start with zero progress in its current stage.");

        Status = UltraProjectStatus.Running;
        LaunchFeePaid = true;
        PauseReason = UltraProjectPauseReason.None;
        Version++;
    }

    internal void Pause(UltraProjectPauseReason reason = UltraProjectPauseReason.Manual)
    {
        if (Status != UltraProjectStatus.Running)
            throw new InvalidOperationException(
                "An Ultra project can only pause while running.");
        if (reason == UltraProjectPauseReason.None)
            throw new ArgumentOutOfRangeException(nameof(reason));

        Status = UltraProjectStatus.Paused;
        PauseReason = reason;
        Version++;
    }

    internal void Resume()
    {
        if (Status != UltraProjectStatus.Paused)
            throw new InvalidOperationException(
                "An Ultra project can only resume from the Paused state.");

        Status = UltraProjectStatus.Running;
        PauseReason = UltraProjectPauseReason.None;
        Version++;
    }

    internal void ChangeDoctrine(UltraProjectDoctrine doctrine)
    {
        ValidateDoctrineForSelection(doctrine);
        if (Status != UltraProjectStatus.Ready &&
            Status != UltraProjectStatus.Paused)
            throw new InvalidOperationException(
                "An Ultra project doctrine can only change while ready or paused.");
        if (Doctrine == doctrine)
            return;
        Doctrine = doctrine;
        Version++;
    }

    internal void Abandon()
    {
        if (Status != UltraProjectStatus.Running &&
            Status != UltraProjectStatus.Paused)
        {
            throw new InvalidOperationException(
                "Only a running or paused Ultra project can be abandoned.");
        }

        StageProgress = ExpantaNum.Zero;
        Status = UltraProjectStatus.Ready;
        PauseReason = UltraProjectPauseReason.None;
        // LaunchFeePaid remains true: abandoning does not refund consumed costs.
        Version++;
    }

    internal void SetStageProgress(ExpantaNum progress)
    {
        EnsureFinite(progress, nameof(progress));
        if (Status != UltraProjectStatus.Running)
            throw new InvalidOperationException(
                "Stage progress can only change while an Ultra project is running.");
        if (CurrentStage == UltraProjectStage.None ||
            CurrentStage == UltraProjectStage.Completed)
        {
            throw new InvalidOperationException(
                "A completed Ultra project has no active stage progress.");
        }

        ExpantaNum normalized = ExpantaNum.Clamp(
            progress,
            ExpantaNum.Zero,
            ExpantaNum.One);
        if (StageProgress == normalized)
            return;

        StageProgress = normalized;
        Version++;
        if (StageProgress == ExpantaNum.One)
            CompleteCurrentStage();
    }

    internal void AdvanceStageProgress(ExpantaNum delta)
    {
        EnsureFinite(delta, nameof(delta));
        if (delta < ExpantaNum.Zero)
            throw new ArgumentOutOfRangeException(nameof(delta));
        if (delta == ExpantaNum.Zero)
            return;

        ExpantaNum nextProgress = StageProgress + delta;
        EnsureFinite(nextProgress, nameof(delta));
        SetStageProgress(nextProgress);
    }

    internal void MarkReadyToCommit()
    {
        if (Status != UltraProjectStatus.Running &&
            Status != UltraProjectStatus.Paused &&
            Status != UltraProjectStatus.ReadyToCommit)
        {
            throw new InvalidOperationException(
                "An Ultra project can only become ready to commit after running.");
        }
        int currentIndex = GetStageIndex(CurrentStage);
        bool finalStageReady = CurrentStage == UltraProjectStage.Completed &&
            completedStages.Count == StageCount &&
            StageProgress == ExpantaNum.One;
        bool intermediateStageReady = currentIndex >= 0 &&
            currentIndex < OrderedStages.Length - 1 &&
            completedStages.Count == currentIndex + 1 &&
            StageProgress == ExpantaNum.One;
        if (!finalStageReady && !intermediateStageReady)
        {
            throw new InvalidOperationException("The current Ultra project stage is not ready to commit.");
        }
        if (Status == UltraProjectStatus.ReadyToCommit)
            return;

        Status = UltraProjectStatus.ReadyToCommit;
        Version++;
    }

    internal void ReadyToCommit() => MarkReadyToCommit();

    internal void Commit()
    {
        if (Status == UltraProjectStatus.Committed)
            return;
        if (Status != UltraProjectStatus.ReadyToCommit)
            throw new InvalidOperationException(
                "An Ultra project can only be committed from ReadyToCommit.");

        int currentIndex = GetStageIndex(CurrentStage);
        if (CurrentStage == UltraProjectStage.Completed)
        {
            CurrentStage = UltraProjectStage.Completed;
            Status = UltraProjectStatus.Committed;
        }
        else
        {
            CurrentStage = OrderedStages[currentIndex + 1];
            StageProgress = ExpantaNum.Zero;
            Status = UltraProjectStatus.Ready;
            LaunchFeePaid = false;
            PauseReason = UltraProjectPauseReason.None;
        }
        Version++;
    }

    public bool IsStageCompleted(UltraProjectStage stage)
    {
        ValidateStage(stage);
        for (int i = 0; i < completedStages.Count; i++)
            if (completedStages[i] == stage)
                return true;
        return false;
    }

    public UltraProjectStateSaveData CaptureSaveData()
    {
        List<UltraProjectStage> savedCompletedStages =
            new List<UltraProjectStage>(completedStages);
        return new UltraProjectStateSaveData
        {
            ProjectId = ProjectId,
            SaveVersion = CurrentSaveVersion,
            Doctrine = Doctrine,
            Status = Status,
            CurrentStage = CurrentStage,
            StageProgress = StageProgress.ToString(),
            CompletedStages = savedCompletedStages,
            LaunchFeePaid = LaunchFeePaid,
            PauseReason = PauseReason,
            StateVersion = Version
        };
    }

    internal void Restore(UltraProjectStateSaveData saveData)
    {
        ValidateSaveData(saveData, out ExpantaNum restoredProgress);

        Doctrine = saveData.Doctrine;
        Status = saveData.Status;
        CurrentStage = saveData.CurrentStage;
        StageProgress = restoredProgress;
        completedStages.Clear();
        for (int i = 0; i < saveData.CompletedStages.Count; i++)
            completedStages.Add(saveData.CompletedStages[i]);
        LaunchFeePaid = saveData.LaunchFeePaid;
        PauseReason = saveData.PauseReason;
        Version = saveData.StateVersion;
    }

    internal void RestoreForTransaction(UltraProjectStateSaveData saveData) =>
        Restore(saveData);

    private void CompleteCurrentStage()
    {
        int currentIndex = GetStageIndex(CurrentStage);
        if (currentIndex < 0 || currentIndex >= OrderedStages.Length)
            throw new InvalidOperationException("The current Ultra project stage is invalid.");

        completedStages.Add(CurrentStage);
        if (currentIndex == OrderedStages.Length - 1)
        {
            CurrentStage = UltraProjectStage.Completed;
            StageProgress = ExpantaNum.One;
            MarkReadyToCommit();
            return;
        }

        // Keep the completed stage visible until the player commits its new
        // capability. Commit() advances to the next stage transactionally.
        Status = UltraProjectStatus.ReadyToCommit;
        PauseReason = UltraProjectPauseReason.None;
        LaunchFeePaid = true;
        Version++;
    }

    private static void ValidateSaveData(
        UltraProjectStateSaveData saveData,
        out ExpantaNum restoredProgress)
    {
        if (saveData == null)
            throw new ArgumentNullException(nameof(saveData));
        if (saveData.SaveVersion != CurrentSaveVersion)
            throw new InvalidOperationException(
                "The Ultra project save version is not supported.");
        if (!string.Equals(saveData.ProjectId, ProjectId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The Ultra project save belongs to an unsupported project definition.");
        if (saveData.CompletedStages == null)
            throw new InvalidOperationException(
                "The Ultra project save is missing completed stages.");
        // Runtime-created states start at version 1. Zero is the default
        // value of an uninitialized DTO and is not a valid persisted state.
        if (saveData.StateVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(saveData.StateVersion));

        ValidateStatus(saveData.Status);
        ValidateDoctrineForSave(saveData.Doctrine, saveData.Status);
        ValidateCompletedStages(saveData.CompletedStages);
        if (saveData.Doctrine == UltraProjectDoctrine.Surge &&
            saveData.CompletedStages.Count < 1)
        {
            throw new InvalidOperationException(
                "The Surge doctrine requires at least one completed Ultra project stage.");
        }
        ValidateStageForSave(
            saveData.CurrentStage,
            saveData.Status,
            saveData.CompletedStages.Count);

        if (!ExpantaNum.TryParse(saveData.StageProgress, out restoredProgress) ||
            !restoredProgress.IsFinite ||
            restoredProgress < ExpantaNum.Zero ||
            restoredProgress > ExpantaNum.One)
        {
            throw new ArgumentOutOfRangeException(nameof(saveData.StageProgress));
        }

        bool isReadyWithoutProgress =
            saveData.Status == UltraProjectStatus.Locked ||
            saveData.Status == UltraProjectStatus.Ready;
        bool isFinalState =
            saveData.Status == UltraProjectStatus.ReadyToCommit ||
            saveData.Status == UltraProjectStatus.Committed;
        if (isReadyWithoutProgress && restoredProgress != ExpantaNum.Zero)
            throw new InvalidOperationException(
                "A locked or ready Ultra project cannot have stage progress.");
        if (isFinalState && restoredProgress != ExpantaNum.One)
            throw new InvalidOperationException(
                "A committable Ultra project must have full stage progress.");
        if ((saveData.Status == UltraProjectStatus.Running ||
             saveData.Status == UltraProjectStatus.Paused) &&
            restoredProgress == ExpantaNum.One)
        {
            throw new InvalidOperationException(
                "An active Ultra project cannot retain completed stage progress.");
        }

        bool requiresLaunchFee =
            saveData.Status == UltraProjectStatus.Running ||
            saveData.Status == UltraProjectStatus.Paused ||
            saveData.Status == UltraProjectStatus.ReadyToCommit ||
            saveData.Status == UltraProjectStatus.Committed;
        if (requiresLaunchFee && !saveData.LaunchFeePaid)
            throw new InvalidOperationException(
                "An Ultra project with progress must have a paid launch fee.");
        if (saveData.Status == UltraProjectStatus.Committed &&
            saveData.CompletedStages.Count != StageCount)
        {
            throw new InvalidOperationException(
                "A committed Ultra project must contain all completed stages.");
        }
        if (saveData.Status == UltraProjectStatus.Locked && saveData.LaunchFeePaid)
            throw new InvalidOperationException(
                "A locked Ultra project cannot have a paid launch fee.");
        if (saveData.Status == UltraProjectStatus.Paused &&
            saveData.PauseReason == UltraProjectPauseReason.None)
        {
            throw new InvalidOperationException(
                "A paused Ultra project must retain a pause reason.");
        }
        if (saveData.Status != UltraProjectStatus.Paused &&
            saveData.PauseReason != UltraProjectPauseReason.None)
        {
            throw new InvalidOperationException(
                "Only a paused Ultra project may retain a pause reason.");
        }
        if (saveData.PauseReason < UltraProjectPauseReason.None ||
            saveData.PauseReason > UltraProjectPauseReason.DefinitionMissing)
        {
            throw new ArgumentOutOfRangeException(nameof(saveData.PauseReason));
        }
    }

    private static void ValidateCompletedStages(IReadOnlyList<UltraProjectStage> stages)
    {
        if (stages.Count > StageCount)
            throw new ArgumentOutOfRangeException(nameof(stages));
        for (int i = 0; i < stages.Count; i++)
        {
            if (stages[i] != OrderedStages[i])
                throw new InvalidOperationException(
                    "Completed Ultra project stages must be contiguous and ordered.");
        }
    }

    private static void ValidateStageForSave(
        UltraProjectStage stage,
        UltraProjectStatus status,
        int completedStageCount)
    {
        if (status == UltraProjectStatus.Locked)
        {
            if (stage != UltraProjectStage.None || completedStageCount != 0)
                throw new InvalidOperationException(
                    "A locked Ultra project cannot contain stage state.");
            return;
        }

        if (status == UltraProjectStatus.Committed)
        {
            if (stage != UltraProjectStage.Completed ||
                completedStageCount != StageCount)
            {
                throw new InvalidOperationException(
                    "A committable Ultra project must have all stages completed.");
            }
            return;
        }

        if (status == UltraProjectStatus.ReadyToCommit)
        {
            if (completedStageCount <= 0 || completedStageCount > StageCount ||
                (completedStageCount == StageCount
                    ? stage != UltraProjectStage.Completed
                    : stage != OrderedStages[completedStageCount - 1]))
            {
                throw new InvalidOperationException(
                    "A committable Ultra project must retain its completed stage.");
            }
            return;
        }

        if (completedStageCount >= StageCount ||
            stage != OrderedStages[completedStageCount])
        {
            throw new InvalidOperationException(
                "The current Ultra project stage does not follow completed stages.");
        }
    }

    private static void ValidateDoctrineForSave(
        UltraProjectDoctrine doctrine,
        UltraProjectStatus status)
    {
        if (status == UltraProjectStatus.Locked)
        {
            if (doctrine != UltraProjectDoctrine.None)
                throw new InvalidOperationException(
                    "A locked Ultra project cannot have a selected doctrine.");
            return;
        }
        ValidateDoctrineForSelection(doctrine);
    }

    private static void ValidateStatus(UltraProjectStatus status)
    {
        if (status < UltraProjectStatus.Locked ||
            status > UltraProjectStatus.Committed)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    private static void ValidateDoctrineForSelection(UltraProjectDoctrine doctrine)
    {
        if (!IsValidDoctrine(doctrine))
            throw new ArgumentOutOfRangeException(nameof(doctrine));
    }

    private static bool IsValidDoctrine(UltraProjectDoctrine doctrine) =>
        doctrine >= UltraProjectDoctrine.PhaseStability &&
        doctrine <= UltraProjectDoctrine.GatewayHub;

    private static void ValidateStage(UltraProjectStage stage)
    {
        if (stage < UltraProjectStage.Prototype ||
            stage > UltraProjectStage.Expansion)
        {
            throw new ArgumentOutOfRangeException(nameof(stage));
        }
    }

    private static bool IsValidProgress(ExpantaNum progress) =>
        progress.IsFinite &&
        progress >= ExpantaNum.Zero &&
        progress <= ExpantaNum.One;

    private static int GetStageIndex(UltraProjectStage stage)
    {
        for (int i = 0; i < OrderedStages.Length; i++)
            if (OrderedStages[i] == stage)
                return i;
        return -1;
    }

    private static void EnsureFinite(ExpantaNum value, string parameterName)
    {
        if (!value.IsFinite)
            throw new ArgumentOutOfRangeException(parameterName);
    }

#if UNITY_EDITOR
    public void InitializeNewForEditor() => InitializeNew();
    public void InitializeNewForEditor(UltraProjectDoctrine doctrine) =>
        InitializeNew(doctrine);
    public void StartForEditor() => Start();
    public void StartForEditor(UltraProjectDoctrine doctrine) => Start(doctrine);
    public void PauseForEditor() => Pause();
    public void ResumeForEditor() => Resume();
    public void ChangeDoctrineForEditor(UltraProjectDoctrine doctrine) => ChangeDoctrine(doctrine);
    public void AbandonForEditor() => Abandon();
    public void SetStageProgressForEditor(ExpantaNum progress) =>
        SetStageProgress(progress);
    public void AdvanceStageProgressForEditor(ExpantaNum delta) =>
        AdvanceStageProgress(delta);
    public void MarkReadyToCommitForEditor() => MarkReadyToCommit();
    public void ReadyToCommitForEditor() => ReadyToCommit();
    public void CommitForEditor() => Commit();
    public void RestoreForEditor(UltraProjectStateSaveData saveData) =>
        Restore(saveData);
#endif
}
