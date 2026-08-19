using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

public enum ResearchActionResult
{
    [Description("无效操作")]
    Invalid,
    [Description("已支付研究成本")]
    PaidOnly,
    [Description("研究已开始")]
    Started,
    [Description("研究已排队")]
    Queued,
    [Description("研究已排队，等待资源")]
    QueuedWaitingResources,
    [Description("已取消排队")]
    Cancelled,
    [Description("研究已经在进行")]
    AlreadyActive,
    [Description("研究已经在队列中")]
    AlreadyQueued,
    [Description("研究已完成")]
    Completed,
    [Description("研究尚未解锁")]
    Blocked,
    [Description("资源不足")]
    InsufficientResources
}

public enum ResearchPaymentResult
{
    [Description("无效操作")]
    Invalid,
    [Description("支付成功")]
    Paid,
    [Description("部分支付")]
    PartiallyPaid,
    [Description("成本已经支付")]
    AlreadyPaid,
    [Description("研究已完成")]
    Completed,
    [Description("资源不足")]
    InsufficientResources
}

public class ResearchManager : Singleton<ResearchManager>
{
    public static readonly ExpantaNum BaseResearchPower = new ExpantaNum(4);

    private ExpantaNum globalEfficiencyFactor = ExpantaNum.One;
    public ExpantaNum GlobalEfficiencyFactor
    {
        get => globalEfficiencyFactor;
        set
        {
            if (!value.IsFinite || value < ExpantaNum.Zero)
                throw new ArgumentOutOfRangeException(nameof(value));
            globalEfficiencyFactor = value;
        }
    }
    public ExpantaNum ResearchPower { get; private set; } = BaseResearchPower;

    private readonly Dictionary<Research, ResearchState> states = new();
    private readonly Dictionary<TechLevel, int> researchCountByTech = new();
    private readonly List<ResearchState> orderedStates = new();
    private readonly Queue<ResearchState> researchQueue = new();
    // Research-tree presentation asks whether each node is queued on every
    // refresh. Keep membership separately so that check is O(1), instead of
    // scanning the queue once per node and allocating a LINQ enumerator.
    private readonly HashSet<Research> queuedResearches = new();
    private readonly List<ResearchState> researchQueueSnapshot = new();
    private static ResourceManager cachedResourceManager;
    private bool researchQueueSnapshotDirty = true;

    public IReadOnlyDictionary<Research, ResearchState> States => states;
    public IReadOnlyDictionary<TechLevel, int> ResearchCountByTech => researchCountByTech;
    public ResearchState ActiveResearch { get; private set; }
    public IReadOnlyList<ResearchState> ResearchQueue
    {
        get
        {
            if (researchQueueSnapshotDirty)
            {
                researchQueueSnapshot.Clear();
                foreach (ResearchState state in researchQueue)
                    researchQueueSnapshot.Add(state);
                researchQueueSnapshotDirty = false;
            }
            return researchQueueSnapshot;
        }
    }
    public int TotalResearchCount => orderedStates.Count;
    public string SelectedResearchId { get; private set; } = string.Empty;
    public event Action<ResearchState> ResearchStateAdded;
    public event Action ResearchQueueChanged;
    internal IReadOnlyList<ResearchState> OrderedStatesForProgression => orderedStates;

    public int TotalFinishedResearchCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < orderedStates.Count; i++)
                if (orderedStates[i].Status == ResearchStatus.Completed)
                    count++;
            return count;
        }
    }

    protected override void Initialize()
    {
        IReadOnlyList<Research> researches = DataBase<Research>.All;
        if (!ResearchValidator.ValidateNoCycles(researches, out string error))
        {
            Debug.LogError(error);
            enabled = false;
            return;
        }

        InitializeResearchStates(researches);
        InitializeResearchCount();
        RebuildProgressionModifiers();
        BuildingManager buildingManager = FindObjectOfType<BuildingManager>();
        RebuildResearchPower(buildingManager?.OrderedStates);
    }

    public ResearchState GetState(Research research)
    {
        if (research == null)
            throw new ArgumentNullException(nameof(research));
        if (states.TryGetValue(research, out ResearchState state))
            return state;
        throw new KeyNotFoundException($"研究状态“{research.Id}”尚未创建。");
    }

    public bool StartResearch(Research research)
    {
        if (research == null || !states.ContainsKey(research))
        {
            Debug.LogError("无法启动空的或尚未初始化的研究定义。");
            return false;
        }
        ResearchState state = states[research];
        if (state.Status == ResearchStatus.Completed ||
            !CanAccessResearch(research) ||
            !ArePrerequisitesCompleted(research))
            return false;

        if (ActiveResearch == state)
            return true;
        if (!state.CostPaid)
            return false;
        bool started = TryStartResearchNow(state);
        if (started)
            ResearchQueueChanged?.Invoke();
        return started;
    }

    public ResearchActionResult HandleResearchAction(Research research)
    {
        if (research == null || !states.TryGetValue(research, out ResearchState state))
            return ResearchActionResult.Invalid;
        if (state.Status == ResearchStatus.Completed)
            return ResearchActionResult.Completed;
        if (ActiveResearch == state)
            return ResearchActionResult.AlreadyActive;
        if (IsQueued(research))
            return RemoveQueuedResearch(research)
                ? ResearchActionResult.Cancelled
                : ResearchActionResult.AlreadyQueued;
        if (!CanAccessResearch(research))
            return ResearchActionResult.Blocked;

        List<ResearchState> batch = BuildPrerequisiteBatch(state);
        for (int i = 0; i < batch.Count; i++)
            EnqueueState(batch[i]);
        TryStartNextQueuedResearch();
        if (ActiveResearch == state)
            return ResearchActionResult.Started;
        return IsQueued(research)
            ? (researchQueue.Count > 0 &&
                researchQueue.Peek().Status == ResearchStatus.WaitingResources
                ? ResearchActionResult.QueuedWaitingResources
                : ResearchActionResult.Queued)
            : ResearchActionResult.Blocked;
    }

    public bool IsQueued(Research research) =>
        research != null && queuedResearches.Contains(research);

    public bool EnqueueResearch(Research research)
    {
        if (research == null || !states.TryGetValue(research, out ResearchState state) ||
            state.Status == ResearchStatus.Completed || ActiveResearch == state ||
            IsQueued(research) || !CanAccessResearch(research) ||
            !ArePrerequisitesCompleted(research))
            return false;
        EnqueueState(state);
        TryStartNextQueuedResearch();
        return true;
    }

    public bool RemoveQueuedResearch(Research research)
    {
        if (research == null || !IsQueued(research))
            return false;

        var cancelled = new HashSet<Research> { research };
        bool changed;
        do
        {
            changed = false;
            foreach (ResearchState queuedState in researchQueue)
            {
                if (cancelled.Contains(queuedState.Definition) ||
                    !DependsOnAny(queuedState.Definition, cancelled))
                    continue;
                cancelled.Add(queuedState.Definition);
                changed = true;
            }
        }
        while (changed);

        List<ResearchState> remaining = researchQueue
            .Where(state => !cancelled.Contains(state.Definition))
            .ToList();
        researchQueue.Clear();
        queuedResearches.Clear();
        researchQueueSnapshotDirty = true;
        for (int i = 0; i < remaining.Count; i++)
        {
            researchQueue.Enqueue(remaining[i]);
            queuedResearches.Add(remaining[i].Definition);
        }
        researchQueueSnapshotDirty = true;

        foreach (Research cancelledResearch in cancelled)
        {
            ResearchState state = states[cancelledResearch];
            state.SetStatus(ArePrerequisitesCompleted(cancelledResearch)
                ? ResearchStatus.Available
                : ResearchStatus.Locked);
        }
        ResearchQueueChanged?.Invoke();
        return true;
    }

    private static bool DependsOnAny(
        Research research,
        HashSet<Research> dependencies)
    {
        IReadOnlyList<Research> prerequisites = research.Prerequisites;
        if (prerequisites == null)
            return false;
        for (int i = 0; i < prerequisites.Count; i++)
            if (dependencies.Contains(prerequisites[i]))
                return true;
        return false;
    }

    public void TryStartNextQueuedResearch()
    {
        if (ActiveResearch != null)
            return;
        while (researchQueue.Count > 0 && researchQueue.Peek().Status == ResearchStatus.Completed)
            RemoveQueuedState(researchQueue.Peek());
        if (researchQueue.Count == 0)
            return;

        ResearchState state = researchQueue.Peek();
        if (!CanAccessResearch(state.Definition) ||
            !ArePrerequisitesCompleted(state.Definition))
            return;

        if (!state.CostPaid)
        {
            // Only raise the event on an actual status transition; the previous
            // code fired ResearchQueueChanged on every simulation tick while
            // the head was waiting, even when nothing had changed.
            if (state.Status != ResearchStatus.WaitingResources)
            {
                state.SetStatus(ResearchStatus.WaitingResources);
                ResearchQueueChanged?.Invoke();
            }
            return;
        }

        RemoveQueuedState(state);
        if (TryStartResearchNow(state))
            ResearchQueueChanged?.Invoke();
    }

    public void SetSelectedResearch(Research research)
    {
        SelectedResearchId = research == null ? string.Empty : research.Id;
    }

    public ResearchPaymentResult PayResearchCost(Research research)
    {
        if (research == null || !states.TryGetValue(research, out ResearchState state))
            return ResearchPaymentResult.Invalid;
        if (state.Status == ResearchStatus.Completed)
            return ResearchPaymentResult.Completed;
        if (state.CostPaid)
            return ResearchPaymentResult.AlreadyPaid;
        if (!TryPayResearchCost(state))
        {
            LogResearchPaymentBlockers(state);
            bool isQueueHead = IsQueued(research) &&
                researchQueue.Peek().Definition == research;
            state.SetStatus(isQueueHead
                ? ResearchStatus.WaitingResources
                : IsQueued(research)
                    ? ResearchStatus.Queued
                : ArePrerequisitesCompleted(research)
                    ? ResearchStatus.Available
                    : ResearchStatus.Locked);
            ResearchQueueChanged?.Invoke();
            return ResearchPaymentResult.InsufficientResources;
        }

        state.SetStatus(IsQueued(research)
            ? ResearchStatus.Queued
            : ArePrerequisitesCompleted(research)
                ? ResearchStatus.Available
                : ResearchStatus.Locked);
        // Payment is a transaction for this one research item only. It must
        // never start or reorder research; the queue action owns scheduling.
        ResearchQueueChanged?.Invoke();
        return state.CostPaid
            ? ResearchPaymentResult.Paid
            : ResearchPaymentResult.PartiallyPaid;
    }

    public bool CanPayResearchCost(Research research, out string blocker)
    {
        blocker = string.Empty;
        if (research == null || !states.TryGetValue(research, out ResearchState state))
        {
            blocker = "研究未初始化";
            return false;
        }
        if (state.Status == ResearchStatus.Completed || state.CostPaid)
            return true;

        ResourceManager resourceManager = cachedResourceManager;
        if (resourceManager == null)
            resourceManager = cachedResourceManager = FindObjectOfType<ResourceManager>();
        if (resourceManager == null)
        {
            blocker = "资源管理器未初始化";
            return false;
        }

        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = research.ResourceRequirements;
        bool anyPayableResource = false;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = requirements[i];
            if (requirement.First == null)
            {
                blocker = "研究存在无效资源需求";
                return false;
            }
            if (requirement.Second <= ExpantaNum.Zero)
                continue;

            ExpantaNum remaining = ExpantaNum.Max(
                ExpantaNum.Zero,
                requirement.Second - state.GetPaidResourceCost(requirement.First));
            ExpantaNum available = resourceManager.GetAmount(requirement.First);
            if (remaining > ExpantaNum.Zero && available > ExpantaNum.Zero)
                anyPayableResource = true;
        }
        return anyPayableResource;
    }

    private static void LogResearchPaymentBlockers(ResearchState state)
    {
        ResourceManager resourceManager = cachedResourceManager;
        if (state == null || resourceManager == null)
            return;

        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = state.Definition.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = requirements[i];
            if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                continue;

            ExpantaNum remaining = ExpantaNum.Max(
                ExpantaNum.Zero,
                requirement.Second - state.GetPaidResourceCost(requirement.First));
            ExpantaNum available = resourceManager.GetAmount(requirement.First);
            if (available < remaining)
            {
                Debug.LogWarning($"[ResearchManager] Atomic payment blocked: research={state.Definition.Id}, resource={requirement.First.Id}, required={remaining.ToGameString()}, available={available.ToGameString()}");
            }
        }
    }

    public bool IsResearchCompleted(string researchId)
    {
        if (string.IsNullOrWhiteSpace(researchId) ||
            !DataBase<Research>.TryFind(researchId, out Research research))
            return false;

        return states.TryGetValue(research, out ResearchState state) &&
            state.Status == ResearchStatus.Completed;
    }

    public void Tick(double deltaSeconds)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (ActiveResearch == null)
        {
            // Auto-pay the queue head as resources become available. A queued
            // research used to sit at "waiting for resources" forever until the
            // player found the payment button, blocking fully-paid items behind
            // it even though the stockpile already covered part of the cost.
            if (researchQueue.Count > 0)
            {
                ResearchState head = researchQueue.Peek();
                if (head != null && !head.CostPaid && TryPayResearchCost(head))
                    ResearchQueueChanged?.Invoke();
            }
            TryStartNextQueuedResearch();
        }
        ResearchState current = ActiveResearch;
        if (current == null)
            return;

        if (!current.CostPaid)
        {
            current.SetStatus(ResearchStatus.WaitingResources);
            return;
        }

        current.SetStatus(ResearchStatus.Researching);
        ExpantaNum speed = ResearchSpeedEffect(
            GameManager.Instance.State.TechLevel,
            current.Definition.TechLevel) * GlobalEfficiencyFactor *
            ResearchPower * ProgressionModifierManager.Current.GlobalResearchMultiplier;
        speed *= GameManager.Instance.State.HappinessMultiplier;
        current.SetProgress(AdvanceResearchProgress(
            current.Progress,
            speed,
            current.BaseCost,
            deltaSeconds));

        if (current.Progress < current.BaseCost)
            return;

        CompleteCurrentResearch(current);
    }

    internal void TickOffline(double deltaSeconds)
    {
        Tick(deltaSeconds);
    }

    public static bool TryPayResearchCost(ResearchState state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        if (state.CostPaid)
            return true;

        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = state.Definition.ResourceRequirements;
        var payable = new Dictionary<Resource, ExpantaNum>();
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = requirements[i];
            if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                continue;

            ExpantaNum paid = state.GetPaidResourceCost(requirement.First);
            ExpantaNum remaining = ExpantaNum.Max(ExpantaNum.Zero, requirement.Second - paid);
            if (remaining <= ExpantaNum.Zero)
                continue;
            ExpantaNum available = ResourceManager.Instance.GetAmount(requirement.First);
            ExpantaNum payment = ExpantaNum.Min(remaining, available);
            if (payment <= ExpantaNum.Zero)
                continue;
            payable[requirement.First] = payable.TryGetValue(
                requirement.First,
                out ExpantaNum current)
                ? current + payment
                : payment;
        }

        if (payable.Count == 0)
            return false;

        return ResourceManager.Instance.TryApplyAtomicPayment(
            payable,
            () =>
            {
                foreach (KeyValuePair<Resource, ExpantaNum> entry in payable)
                {
                    state.SetPaidResourceCost(
                        entry.Key,
                        state.GetPaidResourceCost(entry.Key) + entry.Value);
                }

                state.SetCostPaid(AreAllResourceCostsPaid(state));
            });
    }

    private static bool AreAllResourceCostsPaid(ResearchState state)
    {
        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = state.Definition.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = requirements[i];
            if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                continue;
            if (state.GetPaidResourceCost(requirement.First) < requirement.Second)
                return false;
        }
        return true;
    }

    private List<ResearchState> BuildPrerequisiteBatch(ResearchState target)
    {
        var result = new List<ResearchState>();
        AppendPrerequisiteBatch(
            target,
            new HashSet<Research>(),
            new HashSet<Research>(),
            result);
        return result;
    }

    private void AppendPrerequisiteBatch(
        ResearchState state,
        HashSet<Research> visiting,
        HashSet<Research> included,
        List<ResearchState> result)
    {
        if (state.Status == ResearchStatus.Completed || IsQueued(state.Definition) ||
            ActiveResearch == state || !included.Add(state.Definition))
            return;
        if (!visiting.Add(state.Definition))
            throw new InvalidOperationException(
                $"研究“{state.Definition.Id}”检测到前置循环。");
        IReadOnlyList<Research> prerequisites = state.Definition.Prerequisites;
        if (prerequisites != null)
            for (int i = 0; i < prerequisites.Count; i++)
                AppendPrerequisiteBatch(
                    states[prerequisites[i]], visiting, included, result);
        visiting.Remove(state.Definition);
        result.Add(state);
    }

    private void EnqueueState(ResearchState state)
    {
        if (state == null || state.Status == ResearchStatus.Completed ||
            state == ActiveResearch || IsQueued(state.Definition))
            return;
        state.SetStatus(ResearchStatus.Queued);
        researchQueue.Enqueue(state);
        queuedResearches.Add(state.Definition);
        researchQueueSnapshotDirty = true;
        ResearchQueueChanged?.Invoke();
    }

    private void RemoveQueuedState(ResearchState state)
    {
        List<ResearchState> remaining = researchQueue
            .Where(value => value != state)
            .ToList();
        researchQueue.Clear();
        queuedResearches.Clear();
        researchQueueSnapshotDirty = true;
        for (int i = 0; i < remaining.Count; i++)
        {
            researchQueue.Enqueue(remaining[i]);
            queuedResearches.Add(remaining[i].Definition);
        }
        researchQueueSnapshotDirty = true;
        ResearchQueueChanged?.Invoke();
    }

    private bool TryStartResearchNow(ResearchState state)
    {
        if (state == null || ActiveResearch != null ||
            state.Status == ResearchStatus.Completed || !state.CostPaid ||
            !CanAccessResearch(state.Definition) ||
            !ArePrerequisitesCompleted(state.Definition))
            return false;
        ActiveResearch = state;
        state.SetStatus(ResearchStatus.Researching);
        return true;
    }

    private void CompleteCurrentResearch(ResearchState current)
    {
        current.SetProgress(current.BaseCost);
        current.SetStatus(ResearchStatus.Completed);
        ActiveResearch = null;

        if (current.Definition.AdvancesTechLevel)
            GameManager.Instance.AdvanceTechLevel(current.Definition.TechLevel);

        ProgressionModifierState previousModifiers = ProgressionModifierManager.Current;
        RebuildProgressionModifiers();
        BuildingManager.Instance.ApplyProgressionModifierChange(
            previousModifiers,
            ProgressionModifierManager.Current);
        RefreshAvailabilityStatuses();
        TryStartNextQueuedResearch();
        ResearchQueueChanged?.Invoke();
    }

    private void RefreshAvailabilityStatuses()
    {
        for (int i = 0; i < orderedStates.Count; i++)
        {
            ResearchState state = orderedStates[i];
            if (state.Status == ResearchStatus.Completed)
                continue;
            if (state.Status == ResearchStatus.Queued ||
                state.Status == ResearchStatus.WaitingResources)
                continue;
            state.SetStatus(ArePrerequisitesCompleted(state.Definition)
                ? ResearchStatus.Available
                : ResearchStatus.Locked);
        }
    }

    public bool ArePrerequisitesCompleted(Research research)
    {
        IReadOnlyList<Research> prerequisites = research.Prerequisites;
        if (prerequisites == null)
            return true;
        for (int i = 0; i < prerequisites.Count; i++)
            if (states[prerequisites[i]].Status != ResearchStatus.Completed)
                return false;
        return true;
    }

    public bool CanAccessResearch(Research research)
    {
        if (research == null)
            return false;
        TechLevel currentTechLevel = GameManager.Instance.State.TechLevel;
        if (research.AdvancesTechLevel)
            return (int)research.TechLevel == (int)currentTechLevel + 1;
        return currentTechLevel >= research.TechLevel;
    }

    public static ExpantaNum AdvanceResearchProgress(
        ExpantaNum current,
        ExpantaNum speedPerSecond,
        ExpantaNum baseCost,
        double deltaSeconds)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (!current.IsFinite || !speedPerSecond.IsFinite || !baseCost.IsFinite ||
            current < ExpantaNum.Zero || speedPerSecond < ExpantaNum.Zero ||
            baseCost < ExpantaNum.Zero)
            throw new ArgumentOutOfRangeException(nameof(current));
        return ExpantaNum.Min(baseCost, current + speedPerSecond * deltaSeconds);
    }

    public static double ResearchSpeedEffect(TechLevel current, TechLevel target)
    {
        if (current == target)
            return 1d;
        return 1d / Math.Abs((int)target - (int)current + 0.5d);
    }

    private void InitializeResearchStates(IReadOnlyList<Research> researches)
    {
        for (int i = 0; i < researches.Count; i++)
        {
            Research research = researches[i];
            var state = new ResearchState(research);
            states.Add(research, state);
            orderedStates.Add(state);
            ResearchStateAdded?.Invoke(state);
        }
    }

    private void InitializeResearchCount()
    {
        foreach (TechLevel techLevel in Enum.GetValues(typeof(TechLevel)))
            researchCountByTech[techLevel] = 0;
        for (int i = 0; i < orderedStates.Count; i++)
            researchCountByTech[orderedStates[i].Definition.TechLevel]++;
    }

    internal SaveManager.ResearchSaveData CaptureSaveData()
    {
        var data = new SaveManager.ResearchSaveData
        {
            States = new List<SaveManager.ResearchStateSaveData>(orderedStates.Count),
            ActiveResearchId = ActiveResearch?.Definition.Id,
            SelectedResearchId = SelectedResearchId,
            QueuedResearchIds = researchQueue
                .Select(state => state.Definition.Id)
                .ToList(),
            GlobalEfficiencyFactor = GlobalEfficiencyFactor.ToString()
        };

        for (int i = 0; i < orderedStates.Count; i++)
        {
            ResearchState state = orderedStates[i];
            data.States.Add(new SaveManager.ResearchStateSaveData
            {
                ResearchId = state.Definition.Id,
                Progress = state.Progress.ToString(),
                CostPaid = state.CostPaid,
                Completed = state.Status == ResearchStatus.Completed,
                PaidResourceCosts = CapturePaidResourceCosts(state)
            });
        }

        return data;
    }

    internal void ResetForLoad()
    {
        ActiveResearch = null;
        researchQueue.Clear();
        queuedResearches.Clear();
        researchQueueSnapshotDirty = true;
        SelectedResearchId = string.Empty;
        GlobalEfficiencyFactor = ExpantaNum.One;
        for (int i = 0; i < orderedStates.Count; i++)
            orderedStates[i].ResetForLoad();
        RebuildProgressionModifiers();
        BuildingManager buildingManager = FindObjectOfType<BuildingManager>();
        RebuildResearchPower(buildingManager?.OrderedStates);
    }

    internal void RestoreSaveData(SaveManager.ResearchSaveData data)
    {
        if (data == null)
            return;

        ValidateRestoreInput(data);

        // The save is the complete source of truth for research scheduling.
        // Clear transient scheduling pointers only after validation succeeds.
        ActiveResearch = null;
        researchQueue.Clear();
        queuedResearches.Clear();
        researchQueueSnapshotDirty = true;
        SelectedResearchId = string.Empty;

        if (data.States != null)
        {
            var restoredResearches = new HashSet<Research>();
            for (int i = 0; i < data.States.Count; i++)
            {
                SaveManager.ResearchStateSaveData saved = data.States[i];
                Research definition = DataBase<Research>.Find(saved.ResearchId);
                if (!restoredResearches.Add(definition))
                    throw new InvalidOperationException(
                        $"存档中的研究状态重复包含“{definition.Id}”。");
                ResearchState state = GetState(definition);
                state.Restore(
                    Parse(saved.Progress, saved.ResearchId, nameof(saved.Progress)),
                    saved.CostPaid,
                    saved.Completed,
                    RestorePaidResourceCosts(definition, saved.PaidResourceCosts));
            }
        }

        ValidateRestoredCompletedResearch();

        ProgressionModifierState previousModifiers = ProgressionModifierManager.Current;
        RebuildProgressionModifiers();
        BuildingManager buildingManager = FindObjectOfType<BuildingManager>();
        buildingManager?.ApplyProgressionModifierChange(
            previousModifiers,
            ProgressionModifierManager.Current);
        RebuildResearchPower(buildingManager?.OrderedStates);
        RefreshAvailabilityStatuses();
        if (!string.IsNullOrWhiteSpace(data.ActiveResearchId))
        {
            ResearchState state = GetState(DataBase<Research>.Find(data.ActiveResearchId));
            if (state.Status == ResearchStatus.Completed ||
                !ArePrerequisitesCompleted(state.Definition))
                throw new InvalidOperationException(
                    $"存档中的活动研究“{state.Definition.Id}”状态无效。");

            ActiveResearch = state;
            state.SetStatus(state.CostPaid
                ? ResearchStatus.Researching
                : ResearchStatus.WaitingResources);
        }

        if (data.QueuedResearchIds != null)
        {
            List<ResearchState> restoredQueue =
                ValidateRestoredResearchQueue(data.QueuedResearchIds);
            for (int i = 0; i < restoredQueue.Count; i++)
            {
                EnqueueState(restoredQueue[i]);
            }
        }

        if (string.IsNullOrWhiteSpace(data.SelectedResearchId))
        {
            SelectedResearchId = string.Empty;
        }
        else
        {
            Research selected = DataBase<Research>.Find(data.SelectedResearchId);
            SelectedResearchId = selected.Id;
        }
        GlobalEfficiencyFactor = Parse(
            data.GlobalEfficiencyFactor,
            nameof(ResearchManager),
            nameof(data.GlobalEfficiencyFactor),
            ExpantaNum.One);
        TryStartNextQueuedResearch();
    }

    public override void Save() => SaveManager.Instance.SaveNow(true);

    public override void Load() => SaveManager.Instance.LoadOrCreateGame();

    internal void RebuildProgressionModifiers()
    {
        WorkshopManager workshop = FindObjectOfType<WorkshopManager>();
        ProgressionModifierManager.Rebuild(
            orderedStates,
            workshop?.OrderedStates);
    }

    private void ValidateRestoredCompletedResearch()
    {
        for (int i = 0; i < orderedStates.Count; i++)
        {
            ResearchState state = orderedStates[i];
            if (state.Status != ResearchStatus.Completed)
                continue;
            if (!ArePrerequisitesCompleted(state.Definition))
                throw new InvalidOperationException(
                    $"存档中的研究“{state.Definition.Id}”在前置未完成时被标记为已完成。");
        }
    }

    private void ValidateRestoreInput(SaveManager.ResearchSaveData data)
    {
        var completed = new HashSet<Research>();
        for (int i = 0; i < orderedStates.Count; i++)
            if (orderedStates[i].Status == ResearchStatus.Completed)
                completed.Add(orderedStates[i].Definition);
        if (data.States != null)
        {
            var seen = new HashSet<Research>();
            for (int i = 0; i < data.States.Count; i++)
            {
                SaveManager.ResearchStateSaveData saved = data.States[i];
                Research definition = DataBase<Research>.Find(saved.ResearchId);
                if (!seen.Add(definition))
                    throw new InvalidOperationException($"Duplicate research state: {definition.Id}");
                ExpantaNum progress = Parse(saved.Progress, saved.ResearchId, nameof(saved.Progress));
                if (progress < ExpantaNum.Zero || progress > definition.BaseCost)
                    throw new InvalidOperationException(
                        $"Research progress is outside the valid range: {definition.Id}");
                IReadOnlyDictionary<Resource, ExpantaNum> paidCosts =
                    RestorePaidResourceCosts(definition, saved.PaidResourceCosts);
                if (saved.Completed && !AreAllResourceCostsPaid(definition, paidCosts))
                    throw new InvalidOperationException(
                        $"Completed research has unpaid resource costs: {definition.Id}");
                if (saved.Completed)
                    completed.Add(definition);
                else
                    completed.Remove(definition);
            }
        }

        foreach (Research definition in completed)
            for (int i = 0; i < definition.Prerequisites.Count; i++)
                if (!completed.Contains(definition.Prerequisites[i]))
                    throw new InvalidOperationException($"Completed research prerequisite is missing: {definition.Id}");

        Research active = null;
        if (!string.IsNullOrWhiteSpace(data.ActiveResearchId))
        {
            active = DataBase<Research>.Find(data.ActiveResearchId);
            if (completed.Contains(active))
                throw new InvalidOperationException($"Active research is already completed: {active.Id}");
            for (int i = 0; i < active.Prerequisites.Count; i++)
                if (!completed.Contains(active.Prerequisites[i]))
                    throw new InvalidOperationException($"Active research prerequisite is missing: {active.Id}");
        }

        var queued = new HashSet<Research>();
        if (data.QueuedResearchIds != null)
            for (int i = 0; i < data.QueuedResearchIds.Count; i++)
            {
                string id = data.QueuedResearchIds[i];
                if (string.IsNullOrWhiteSpace(id) ||
                    !DataBase<Research>.TryFind(id, out Research definition) ||
                    completed.Contains(definition) || definition == active || !queued.Add(definition))
                    throw new InvalidOperationException($"Invalid queued research: {id}");
                for (int j = 0; j < definition.Prerequisites.Count; j++)
                    if (!completed.Contains(definition.Prerequisites[j]) &&
                        definition.Prerequisites[j] != active &&
                        !queued.Contains(definition.Prerequisites[j]))
                        throw new InvalidOperationException($"Queued research prerequisite is missing: {definition.Id}");
            }

        if (!string.IsNullOrWhiteSpace(data.SelectedResearchId))
            DataBase<Research>.Find(data.SelectedResearchId);
        Parse(data.GlobalEfficiencyFactor, nameof(ResearchManager), nameof(data.GlobalEfficiencyFactor), ExpantaNum.One);
    }

    private List<ResearchState> ValidateRestoredResearchQueue(
        IReadOnlyList<string> savedQueueIds)
    {
        var result = new List<ResearchState>();
        var queuedDefinitions = new HashSet<Research>();
        for (int i = 0; i < savedQueueIds.Count; i++)
        {
            string id = savedQueueIds[i];
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException(
                    "存档中的研究队列包含空研究 ID。");
            if (!DataBase<Research>.TryFind(id, out Research definition))
                throw new InvalidOperationException(
                    $"存档中的研究队列包含未知研究“{id}”。");

            ResearchState state = states[definition];
            if (state.Status == ResearchStatus.Completed)
                throw new InvalidOperationException(
                    $"存档中的研究队列包含已完成研究“{definition.Id}”。");
            if (state == ActiveResearch)
                throw new InvalidOperationException(
                    $"存档中的研究队列与活动研究“{definition.Id}”冲突。");
            if (!queuedDefinitions.Add(definition))
                throw new InvalidOperationException(
                    $"存档中的研究队列重复包含研究“{definition.Id}”。");

            IReadOnlyList<Research> prerequisites = definition.Prerequisites;
            for (int j = 0; j < prerequisites.Count; j++)
            {
                Research prerequisite = prerequisites[j];
                bool satisfied = states[prerequisite].Status == ResearchStatus.Completed ||
                    (ActiveResearch != null && ActiveResearch.Definition == prerequisite) ||
                    queuedDefinitions.Contains(prerequisite);
                if (!satisfied)
                    throw new InvalidOperationException(
                        $"存档中的研究队列违反前置顺序：“{definition.Id}”需要“{prerequisite.Id}”。");
            }

            result.Add(state);
        }
        return result;
    }

    internal void RebuildResearchPower(IReadOnlyList<BuildingState> buildingStates)
    {
        ResearchPower = CalculateResearchPower(buildingStates, BaseResearchPower);
    }

    public static ExpantaNum CalculateResearchPower(
        IReadOnlyList<BuildingState> buildingStates,
        ExpantaNum baseResearchPower)
    {
        ExpantaNum total = ExpantaNum.Max(ExpantaNum.Zero, baseResearchPower);
        if (buildingStates == null)
            return total;

        for (int i = 0; i < buildingStates.Count; i++)
        {
            BuildingState state = buildingStates[i];
            if (state == null || state.Amount <= ExpantaNum.Zero)
                continue;

            ExpantaNum contribution = state.Definition.ResearchPowerGranted
                * state.Amount
                * ExpantaNum.Clamp01(state.Efficiency)
                * ProgressionModifierManager.Current
                    .GetBuildingResearchPowerMultiplier(state.Definition);
            if (!contribution.IsNaN && contribution > ExpantaNum.Zero)
                total += contribution;
        }

        return ExpantaNum.Max(ExpantaNum.Zero, total);
    }

    private static ExpantaNum Parse(
        string raw,
        string owner,
        string field,
        ExpantaNum fallback = default)
    {
        if (ExpantaNum.TryParse(raw, out ExpantaNum value) && value.IsFinite)
            return value;
        if (string.IsNullOrEmpty(raw))
            return fallback;
            throw new FormatException($"{owner}.{field} 中的 ExpantaNum 值“{raw}”无效。");
    }

    private static List<SaveManager.ResearchResourceCostSaveData> CapturePaidResourceCosts(ResearchState state)
    {
        var result = new List<SaveManager.ResearchResourceCostSaveData>();
        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = state.Definition.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            ExpantaNum paid = state.GetPaidResourceCost(requirements[i].First);
            if (paid > ExpantaNum.Zero)
            {
                result.Add(new SaveManager.ResearchResourceCostSaveData
                {
                    ResourceId = requirements[i].First.Id,
                    Amount = paid.ToString()
                });
            }
        }
        return result;
    }

 #if false
    private static IReadOnlyDictionary<Resource, ExpantaNum> RestorePaidResourceCosts(
        Research definition,
        List<SaveManager.ResearchResourceCostSaveData> savedCosts)
    {
        var result = new Dictionary<Resource, ExpantaNum>();
        if (savedCosts == null)
            return result;

        for (int i = 0; i < savedCosts.Count; i++)
        {
            SaveManager.ResearchResourceCostSaveData saved = savedCosts[i];
            string resourceId =
                RetiredDefinitionMigration.NormalizeResourceId(saved.ResourceId);
            Resource resource = DataBase<Resource>.Find(resourceId);
            if (result.ContainsKey(resource))
                throw new InvalidOperationException(
                    $"瀛樻。涓殑鐮旂┒璧勬簮鏀粯閲嶅鍖呭惈鈥渰resource.Id}鈥濄€?);
                throw new InvalidOperationException($"Duplicate paid resource cost: {resource.Id}");
            ExpantaNum amount = Parse(saved.Amount, saved.ResourceId, nameof(saved.Amount));
            if (amount < ExpantaNum.Zero)
                throw new InvalidOperationException(
                    $"瀛樻。涓殑鐮旂┒璧勬簮鏀粯涓嶈兘涓鸿礋鏁帮細鈥渰saved.ResourceId}鈥濄€?);
                throw new InvalidOperationException($"Paid resource cost cannot be negative: {saved.ResourceId}");
            result.Add(resource, amount);
        }
        return result;
    }
 #endif

    private static IReadOnlyDictionary<Resource, ExpantaNum> RestorePaidResourceCosts(
        Research definition,
        List<SaveManager.ResearchResourceCostSaveData> savedCosts)
    {
        var result = new Dictionary<Resource, ExpantaNum>();
        if (savedCosts == null)
            return result;

        for (int i = 0; i < savedCosts.Count; i++)
        {
            SaveManager.ResearchResourceCostSaveData saved = savedCosts[i];
            string resourceId = RetiredDefinitionMigration.NormalizeResourceId(saved.ResourceId);
            Resource resource = DataBase<Resource>.Find(resourceId);
            if (result.ContainsKey(resource))
                throw new InvalidOperationException($"Duplicate paid resource cost: {resource.Id}");
            ExpantaNum amount = Parse(saved.Amount, saved.ResourceId, nameof(saved.Amount));
            if (amount < ExpantaNum.Zero)
                throw new InvalidOperationException($"Paid resource cost cannot be negative: {saved.ResourceId}");
            ExpantaNum required = GetRequiredResourceCost(definition, resource);
            if (required <= ExpantaNum.Zero)
                throw new InvalidOperationException(
                    $"Research payment uses a resource not required by {definition.Id}: {resource.Id}");
            if (amount > required)
                throw new InvalidOperationException(
                    $"Research payment exceeds the required cost for {definition.Id}: {resource.Id}");
            result.Add(resource, amount);
        }
        return result;
    }

    private static ExpantaNum GetRequiredResourceCost(Research definition, Resource resource)
    {
        ExpantaNum required = ExpantaNum.Zero;
        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = definition.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
            if (requirements[i].First == resource)
                required += ExpantaNum.Max(ExpantaNum.Zero, requirements[i].Second);
        return required;
    }

    private static bool AreAllResourceCostsPaid(
        Research definition,
        IReadOnlyDictionary<Resource, ExpantaNum> paidCosts)
    {
        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = definition.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            Resource resource = requirements[i].First;
            ExpantaNum required = ExpantaNum.Max(ExpantaNum.Zero, requirements[i].Second);
            if (required > ExpantaNum.Zero &&
                (!paidCosts.TryGetValue(resource, out ExpantaNum paid) || paid < required))
                return false;
        }
        return true;
    }

}
