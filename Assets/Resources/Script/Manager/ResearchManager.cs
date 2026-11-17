using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

public enum ResearchActionResult
{
    Invalid,
    PaidOnly,
    Started,
    Queued,
    QueuedWaitingResources,
    Cancelled,
    AlreadyActive,
    AlreadyQueued,
    Completed,
    Blocked,
    InsufficientResources
}

public enum ResearchPaymentResult
{
    Invalid,
    Paid,
    AlreadyPaid,
    Completed,
    InsufficientResources
}

public class ResearchManager : Singleton<ResearchManager>
{
    public static readonly ExpantaNum BaseResearchPower = new ExpantaNum(10);

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
    public ExpantaNum EffectiveResearchPower => ResearchPower * GlobalEfficiencyFactor *
        ProgressionModifierManager.Current.GlobalResearchMultiplier *
        GameManager.Instance.State.HappinessMultiplier;

    private readonly Dictionary<ResearchDef, ResearchState> states = new();
    private readonly List<ResearchState> orderedStates = new();
    private readonly Queue<ResearchState> researchQueue = new();
    private readonly List<ResearchState> researchQueueRebuildBuffer = new();
    private readonly HashSet<ResearchDef> researchCancellationBuffer = new();
    // Research-tree presentation asks whether each node is queued on every
    // refresh. Keep membership separately so that check is O(1), instead of
    // scanning the queue once per node and allocating a LINQ enumerator.
    private readonly HashSet<ResearchDef> queuedResearches = new();
    private static readonly Dictionary<ResourceDef, ExpantaNum> researchPaymentBuffer = new();
    private readonly List<ResearchState> researchQueueSnapshot = new();
    private static ResourceManager cachedResourceManager;
    private bool researchQueueSnapshotDirty = true;
    private bool suppressCompletionAudio;
    internal bool SuppressCompletionAudio
    {
        get => suppressCompletionAudio;
        set => suppressCompletionAudio = value;
    }

    public IReadOnlyDictionary<ResearchDef, ResearchState> States => states;
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
    public ExpantaNum CurrentResearchSpeed => ActiveResearch != null && ActiveResearch.CostPaid
        ? CalculateResearchSpeed(ActiveResearch) : ExpantaNum.Zero;

    public int TotalResearchCount => orderedStates.Count;
    public string SelectedResearchId { get; private set; } = string.Empty;
    public event Action<ResearchState> ResearchStateAdded;
    public event Action ResearchQueueChanged;
    public event Action<ResearchState> ResearchCompleted;
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
        IReadOnlyList<ResearchDef> researches = DataBase<ResearchDef>.All;
        if (!ResearchValidator.ValidateNoCycles(researches, out string error))
        {
            Debug.LogError(error);
            // Keep the runtime state table usable for diagnostics and save
            // migration even when a content validation issue is present. The
            // validator still reports the graph error; disabling this manager
            // here leaves every unrelated research/save test with an empty
            // state table and hides the actual content failure.
        }

        InitializeResearchStates(researches);
        RebuildProgressionModifiers();
        BuildingManager buildingManager = FindObjectOfType<BuildingManager>();
        RebuildResearchPower(buildingManager?.OrderedStates);
    }

#if UNITY_EDITOR
    public void InitializeForEditor() => Initialize();
    public void ResetForLoadForEditor() => ResetForLoad();
    public void TickOfflineForEditor(double deltaSeconds) => TickOffline(deltaSeconds);
    public void RestoreSaveDataForEditor(SaveManager.ResearchSaveData data) =>
        RestoreSaveData(data);
#endif

    public ResearchState GetState(ResearchDef research)
    {
        if (research == null)
            throw new ArgumentNullException(nameof(research));
        if (TryGetStateByStableId(research, out ResearchState state))
            return state;
        throw new KeyNotFoundException($"研究状态“{research.Id}”尚未创建。");
    }

    private bool TryGetStateByStableId(ResearchDef research, out ResearchState state)
    {
        state = null;
        if (research == null)
            return false;
        if (states.TryGetValue(research, out state))
            return true;
        foreach (KeyValuePair<ResearchDef, ResearchState> entry in states)
        {
            if (entry.Key != null && string.Equals(
                    entry.Key.Id == null ? string.Empty : entry.Key.Id.Trim(),
                    research.Id == null ? string.Empty : research.Id.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                state = entry.Value;
                return state != null;
            }
        }
        return false;
    }

    public bool StartResearch(ResearchDef research)
    {
        if (!TryGetStateByStableId(research, out ResearchState state))
        {
            Debug.LogError("无法启动空的或尚未初始化的研究定义。");
            return false;
        }
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
        {
            RemoveQueuedState(state, false);
            ObserverNotifications.Publish(ResearchQueueChanged);
        }
        return started;
    }

    public ResearchActionResult HandleResearchAction(ResearchDef research)
    {
        if (!TryGetStateByStableId(research, out ResearchState state))
            return ResearchActionResult.Invalid;
        if (state.Status == ResearchStatus.Completed)
            return ResearchActionResult.Completed;
        if (!CanAccessResearch(research))
            return ResearchActionResult.Blocked;
        if (ActiveResearch == state)
            return RemoveQueuedResearch(research)
                ? ResearchActionResult.Cancelled
                : ResearchActionResult.AlreadyActive;
        if (IsQueued(research))
            return RemoveQueuedResearch(research)
                ? ResearchActionResult.Cancelled
                : ResearchActionResult.AlreadyQueued;

        List<ResearchState> batch = BuildPrerequisiteBatch(state);
        for (int i = 0; i < batch.Count; i++)
            EnqueueState(batch[i], false);
        TryStartNextQueuedResearch(false);
        ObserverNotifications.Publish(ResearchQueueChanged);
        if (ActiveResearch == state)
            return ResearchActionResult.Started;
        return IsQueued(research)
            ? (researchQueue.Count > 0 &&
                researchQueue.Peek().Status == ResearchStatus.WaitingResources
                ? ResearchActionResult.QueuedWaitingResources
                : ResearchActionResult.Queued)
            : ResearchActionResult.Blocked;
    }

    public bool IsQueued(ResearchDef research) =>
        research != null && queuedResearches.Contains(research);

    public bool EnqueueResearch(ResearchDef research)
    {
        if (!TryGetStateByStableId(research, out ResearchState state) ||
            state.Status == ResearchStatus.Completed || ActiveResearch == state ||
            IsQueued(research) || !CanAccessResearch(research) ||
            !ArePrerequisitesCompleted(research))
            return false;
        EnqueueState(state, false);
        TryStartNextQueuedResearch(false);
        ObserverNotifications.Publish(ResearchQueueChanged);
        return true;
    }

    public bool RemoveQueuedResearch(ResearchDef research)
    {
        bool isActiveResearch = ActiveResearch != null &&
            ActiveResearch.Definition == research;
        if (research == null || (!isActiveResearch && !IsQueued(research)))
            return false;

        if (isActiveResearch)
            ActiveResearch = null;

        HashSet<ResearchDef> cancelled = researchCancellationBuffer;
        cancelled.Clear();
        CollectCancellation(research, cancelled);

        List<ResearchState> remaining = researchQueueRebuildBuffer;
        remaining.Clear();
        foreach (ResearchState state in researchQueue)
            if (!cancelled.Contains(state.Definition))
                remaining.Add(state);
        RebuildQueue(remaining);

        foreach (ResearchDef cancelledResearch in cancelled)
        {
            ResearchState state = states[cancelledResearch];
            state.SetStatus(ArePrerequisitesCompleted(cancelledResearch)
                ? ResearchStatus.Available
                : ResearchStatus.Locked);
        }
        remaining.Clear();
        cancelled.Clear();
        ObserverNotifications.Publish(ResearchQueueChanged);
        return true;
    }

    public IReadOnlyList<ResearchState> GetCancellationPreview(ResearchDef research)
    {
        var result = new List<ResearchState>();
        if (research == null ||
            ((ActiveResearch == null || ActiveResearch.Definition != research) && !IsQueued(research)))
            return result.AsReadOnly();
        var cancelled = new HashSet<ResearchDef>();
        CollectCancellation(research, cancelled);
        if (ActiveResearch != null && cancelled.Contains(ActiveResearch.Definition))
            result.Add(ActiveResearch);
        foreach (ResearchState state in researchQueue)
            if (cancelled.Contains(state.Definition)) result.Add(state);
        return result.AsReadOnly();
    }

    private void CollectCancellation(ResearchDef research, HashSet<ResearchDef> cancelled)
    {
        cancelled.Add(research);
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

    }

    // Display order includes the active research before the waiting queue.
    public int GetResearchOrderIndex(ResearchDef research)
    {
        if (research == null) return -1;
        if (ActiveResearch?.Definition == research) return 0;
        int index = ActiveResearch == null ? 0 : 1;
        foreach (ResearchState state in researchQueue)
        {
            if (state.Definition == research) return index;
            index++;
        }
        return -1;
    }

    public bool CanMoveQueuedResearch(ResearchDef research, int targetIndex) =>
        TryPrepareQueueMove(research, targetIndex, out _);

    public bool MoveQueuedResearch(ResearchDef research, int targetIndex)
    {
        if (!TryPrepareQueueMove(research, targetIndex, out List<ResearchState> order))
            return false;
        // Suspending research retains its progress and payment ledger. The
        // normal head-payment path starts the new first item without refunds.
        ResearchState previousActive = ActiveResearch;
        bool keepActive = previousActive != null && order[0] == previousActive;
        ActiveResearch = keepActive ? previousActive : null;
        if (keepActive) order.RemoveAt(0);
        for (int i = 0; i < order.Count; i++)
            order[i].SetStatus(ResearchStatus.Queued);
        RebuildQueue(order);
        TryStartNextQueuedResearch(false);
        ObserverNotifications.Publish(ResearchQueueChanged);
        return true;
    }

    private bool TryPrepareQueueMove(ResearchDef research, int targetIndex, out List<ResearchState> order)
    {
        order = null;
        int sourceIndex = GetResearchOrderIndex(research);
        int count = researchQueue.Count + (ActiveResearch == null ? 0 : 1);
        if (sourceIndex < 0 || targetIndex < 0 || targetIndex >= count || sourceIndex == targetIndex)
            return false;
        order = new List<ResearchState>(count);
        if (ActiveResearch != null) order.Add(ActiveResearch);
        order.AddRange(researchQueue);
        ResearchState moving = order[sourceIndex];
        order.RemoveAt(sourceIndex);
        order.Insert(targetIndex, moving);
        var earlier = new HashSet<ResearchDef>();
        for (int i = 0; i < order.Count; i++)
        {
            foreach (ResearchDef prerequisite in order[i].Definition.Prerequisites)
                if (!IsResearchCompleted(prerequisite.Id) && !earlier.Contains(prerequisite)) return false;
            earlier.Add(order[i].Definition);
        }
        return true;
    }

    private static bool DependsOnAny(
        ResearchDef research,
        HashSet<ResearchDef> dependencies)
    {
        IReadOnlyList<ResearchDef> prerequisites = research.Prerequisites;
        if (prerequisites == null)
            return false;
        for (int i = 0; i < prerequisites.Count; i++)
            if (dependencies.Contains(prerequisites[i]))
                return true;
        return false;
    }

    public void TryStartNextQueuedResearch()
    {
        TryStartNextQueuedResearch(true);
    }

    private void TryStartNextQueuedResearch(bool notifyQueueChanged)
    {
        if (ActiveResearch != null)
            return;
        while (researchQueue.Count > 0 && researchQueue.Peek().Status == ResearchStatus.Completed)
            RemoveQueuedState(researchQueue.Peek(), notifyQueueChanged);
        if (researchQueue.Count == 0)
            return;

        ResearchState state = researchQueue.Peek();
        if (!CanAccessResearch(state.Definition) ||
            !ArePrerequisitesCompleted(state.Definition))
            return;

        if (!state.CostPaid)
        {
            // A player action should use the same automatic payment path as
            // the simulation tick. This starts ready research immediately;
            // TryPayResearchCost remains atomic, so insufficient resources
            // leave both the inventory and the paid-cost state unchanged.
            if (TryPayResearchCost(state))
            {
                RemoveQueuedState(state, false);
                if (TryStartResearchNow(state) && notifyQueueChanged)
                    ObserverNotifications.Publish(ResearchQueueChanged);
                return;
            }
            // Only raise the event on an actual status transition; the previous
            // code fired ResearchQueueChanged on every simulation tick while
            // the head was waiting, even when nothing had changed.
            if (state.Status != ResearchStatus.WaitingResources)
            {
                state.SetStatus(ResearchStatus.WaitingResources);
                if (notifyQueueChanged)
                    ObserverNotifications.Publish(ResearchQueueChanged);
            }
            return;
        }

        RemoveQueuedState(state, notifyQueueChanged);
        if (TryStartResearchNow(state))
        {
            if (notifyQueueChanged)
                ObserverNotifications.Publish(ResearchQueueChanged);
        }
    }

    public void SetSelectedResearch(ResearchDef research)
    {
        SelectedResearchId = research == null ? string.Empty : research.Id;
    }

    public ResearchPaymentResult PayResearchCost(ResearchDef research)
    {
        if (!TryGetStateByStableId(research, out ResearchState state))
            return ResearchPaymentResult.Invalid;
        if (!CanAccessResearch(research))
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
            ObserverNotifications.Publish(ResearchQueueChanged);
            return ResearchPaymentResult.InsufficientResources;
        }

        state.SetStatus(IsQueued(research)
            ? ResearchStatus.Queued
            : ArePrerequisitesCompleted(research)
                ? ResearchStatus.Available
                : ResearchStatus.Locked);
        // Payment is a transaction for this one research item only. It must
        // never start or reorder research; the queue action owns scheduling.
        ObserverNotifications.Publish(ResearchQueueChanged);
        return ResearchPaymentResult.Paid;
    }

    public bool CanPayResearchCost(ResearchDef research, out string blocker)
    {
        blocker = string.Empty;
        if (!TryGetStateByStableId(research, out ResearchState state))
        {
            blocker = "研究未初始化";
            return false;
        }
        if (!CanAccessResearch(research))
        {
            blocker = "研究尚未解锁";
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

        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements = research.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> requirement = requirements[i];
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
            if (remaining <= ExpantaNum.Zero)
                continue;
            ExpantaNum available = resourceManager.GetAmount(requirement.First);
            if (available < remaining)
            {
                string label = string.IsNullOrEmpty(requirement.First.Label)
                    ? requirement.First.Id
                    : requirement.First.Label;
                blocker = "资源不足：" + label;
                return false;
            }
        }
        return true;
    }

    private static void LogResearchPaymentBlockers(ResearchState state)
    {
        ResourceManager resourceManager = cachedResourceManager;
        if (state == null || resourceManager == null)
            return;

        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements = state.Definition.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> requirement = requirements[i];
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
            !DataBase<ResearchDef>.TryFind(researchId, out ResearchDef research))
            return false;

        return TryGetStateByStableId(research, out ResearchState state) &&
            state.Status == ResearchStatus.Completed;
    }

    public void Tick(double deltaSeconds)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (ActiveResearch == null)
        {
            ResearchState beforeHead = researchQueue.Count > 0
                ? researchQueue.Peek()
                : null;
            ResearchStatus beforeHeadStatus = beforeHead == null
                ? ResearchStatus.Locked
                : beforeHead.Status;
            // TryStartNextQueuedResearch owns the queue-head payment attempt.
            // Keeping this in one path prevents a simulation tick from
            // attempting the same transaction twice.
            TryStartNextQueuedResearch(false);
            ResearchState afterHead = researchQueue.Count > 0
                ? researchQueue.Peek()
                : null;
            if (beforeHead != afterHead ||
                beforeHeadStatus != (afterHead == null ? ResearchStatus.Locked : afterHead.Status))
                ObserverNotifications.Publish(ResearchQueueChanged);
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
        ExpantaNum speed = CalculateResearchSpeed(current);
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
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        bool previousSuppression = suppressCompletionAudio;
        suppressCompletionAudio = true;
        try
        {
            double remainingSeconds = deltaSeconds;
            while (remainingSeconds > 0d)
            {
                if (ActiveResearch == null)
                    TryStartNextQueuedResearch(false);
                ResearchState current = ActiveResearch;
                if (current == null || !current.CostPaid)
                    return;

                ExpantaNum speed = CalculateResearchSpeed(current);
                if (speed <= ExpantaNum.Zero)
                {
                    current.SetStatus(ResearchStatus.Researching);
                    return;
                }

                if (AdvanceResearchProgress(current.Progress, speed, current.BaseCost,
                        remainingSeconds) < current.BaseCost)
                {
                    Tick(remainingSeconds);
                    return;
                }

                ExpantaNum remainingProgress = ExpantaNum.Max(
                    ExpantaNum.Zero, current.BaseCost - current.Progress);
                double completionSeconds = Math.Min(remainingSeconds,
                    Math.Max(0d, (remainingProgress / speed).ToDouble()));
                // Establish completion before converting time to double; do not round-trip progress.
                current.SetStatus(ResearchStatus.Researching);
                CompleteCurrentResearch(current);
                remainingSeconds -= completionSeconds;
            }
        }
        finally
        {
            suppressCompletionAudio = previousSuppression;
        }
    }

    private ExpantaNum CalculateResearchSpeed(ResearchState current)
    {
        GameState gameState = GameManager.Instance.State;
        return ResearchSpeedEffect(
            gameState.TechLevel,
            current.Definition.TechLevel) * EffectiveResearchPower;
    }

    public static bool TryPayResearchCost(ResearchState state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        if (state.CostPaid)
            return true;

        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements = state.Definition.ResourceRequirements;
        researchPaymentBuffer.Clear();
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> requirement = requirements[i];
            if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                continue;

            ExpantaNum paid = state.GetPaidResourceCost(requirement.First);
            ExpantaNum remaining = ExpantaNum.Max(ExpantaNum.Zero, requirement.Second - paid);
            if (remaining <= ExpantaNum.Zero)
                continue;
            researchPaymentBuffer[requirement.First] = researchPaymentBuffer.TryGetValue(
                requirement.First,
                out ExpantaNum current)
                ? current + remaining
                : remaining;
        }

        if (researchPaymentBuffer.Count == 0)
        {
            state.SetCostPaid(AreAllResourceCostsPaid(state));
            return state.CostPaid;
        }

        ResourceManager resourceManager = cachedResourceManager;
        if (resourceManager == null)
            resourceManager = cachedResourceManager =
                FindObjectOfType<ResourceManager>();
        if (resourceManager == null)
            return false;

        return resourceManager.TryApplyAtomicPayment(
            researchPaymentBuffer,
            () =>
            {
                foreach (KeyValuePair<ResourceDef, ExpantaNum> entry in researchPaymentBuffer)
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
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements = state.Definition.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> requirement = requirements[i];
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
            new HashSet<ResearchDef>(),
            new HashSet<ResearchDef>(),
            result);
        return result;
    }

    private void AppendPrerequisiteBatch(
        ResearchState state,
        HashSet<ResearchDef> visiting,
        HashSet<ResearchDef> included,
        List<ResearchState> result)
    {
        if (state.Status == ResearchStatus.Completed || IsQueued(state.Definition) ||
            ActiveResearch == state || !included.Add(state.Definition))
            return;
        if (!visiting.Add(state.Definition))
            throw new InvalidOperationException(
                $"研究“{state.Definition.Id}”检测到前置循环。");
        IReadOnlyList<ResearchDef> prerequisites = state.Definition.Prerequisites;
        if (prerequisites != null)
            for (int i = 0; i < prerequisites.Count; i++)
            {
                if (!TryGetStateByStableId(prerequisites[i], out ResearchState prerequisiteState))
                    throw new InvalidOperationException(
                        $"Missing research state: {prerequisites[i].Id}");
                AppendPrerequisiteBatch(
                    prerequisiteState, visiting, included, result);
            }
        visiting.Remove(state.Definition);
        result.Add(state);
    }

    private void EnqueueState(ResearchState state, bool notifyQueueChanged = true)
    {
        if (state == null || state.Status == ResearchStatus.Completed ||
            state == ActiveResearch || IsQueued(state.Definition))
            return;
        state.SetStatus(ResearchStatus.Queued);
        researchQueue.Enqueue(state);
        queuedResearches.Add(state.Definition);
        researchQueueSnapshotDirty = true;
        if (notifyQueueChanged)
            ObserverNotifications.Publish(ResearchQueueChanged);
    }

    private void RemoveQueuedState(ResearchState state, bool notifyQueueChanged = true)
    {
        List<ResearchState> remaining = researchQueueRebuildBuffer;
        remaining.Clear();
        foreach (ResearchState value in researchQueue)
            if (value != state)
                remaining.Add(value);
        RebuildQueue(remaining);
        remaining.Clear();
        if (notifyQueueChanged)
            ObserverNotifications.Publish(ResearchQueueChanged);
    }

    private void RebuildQueue(IReadOnlyList<ResearchState> remaining)
    {
        researchQueue.Clear();
        queuedResearches.Clear();
        researchQueueSnapshotDirty = true;
        for (int i = 0; i < remaining.Count; i++)
        {
            researchQueue.Enqueue(remaining[i]);
            queuedResearches.Add(remaining[i].Definition);
        }
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
#if UNITY_EDITOR
        float completionStart = Time.realtimeSinceStartup;
#endif
        current.SetProgress(current.BaseCost);
        current.SetStatus(ResearchStatus.Completed);
        ActiveResearch = null;

        if (current.Definition.AdvancesTechLevel)
            GameManager.Instance.AdvanceTechLevel(current.Definition.TechLevel);

#if UNITY_EDITOR
        float stateTransitionEnd = Time.realtimeSinceStartup;
#endif
        ProgressionModifierState previousModifiers = ProgressionModifierManager.Current;
        RebuildProgressionModifiers();
#if UNITY_EDITOR
        float rebuildEnd = Time.realtimeSinceStartup;
#endif
        BuildingManager.Instance.ApplyProgressionModifierChange(
            previousModifiers,
            ProgressionModifierManager.Current);
        if (current.Definition.Id == "FeudalAdministration")
            GameManager.Instance.State.RestoreFeudalTerritoryReward(true);
        BuildingManager.Instance.RefreshBuildingChainAvailability();
#if UNITY_EDITOR
        float buildingApplyEnd = Time.realtimeSinceStartup;
#endif
        RefreshAvailabilityStatuses();
#if UNITY_EDITOR
        float availabilityEnd = Time.realtimeSinceStartup;
#endif
        // A completion can remove the finished head and start the next item.
        // Publish one queue change after the whole transition instead of one
        // notification for each internal queue mutation.
        TryStartNextQueuedResearch(false);
        ObserverNotifications.Publish(ResearchQueueChanged);
        if (!suppressCompletionAudio)
            ObserverNotifications.Publish(ResearchCompleted, current);
#if UNITY_EDITOR
        float completionEnd = Time.realtimeSinceStartup;
        KingdomEditorPerfLog.Write(
            $"[KingdomPerf] ResearchCompletion id={current.Definition.Id} " +
            $"stateMs={(stateTransitionEnd - completionStart) * 1000f:0.0} " +
            $"rebuildMs={(rebuildEnd - stateTransitionEnd) * 1000f:0.0} " +
            $"buildingApplyMs={(buildingApplyEnd - rebuildEnd) * 1000f:0.0} " +
            $"availabilityMs={(availabilityEnd - buildingApplyEnd) * 1000f:0.0} " +
            $"queueMs={(completionEnd - availabilityEnd) * 1000f:0.0} " +
            $"totalMs={(completionEnd - completionStart) * 1000f:0.0}");
#endif
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

    public bool ArePrerequisitesCompleted(ResearchDef research)
    {
        IReadOnlyList<ResearchDef> prerequisites = research.Prerequisites;
        if (prerequisites == null)
            return true;
        for (int i = 0; i < prerequisites.Count; i++)
            if (!TryGetStateByStableId(prerequisites[i], out ResearchState prerequisiteState) ||
                prerequisiteState.Status != ResearchStatus.Completed)
                return false;
        return true;
    }

    public bool CanAccessResearch(ResearchDef research)
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

    private void InitializeResearchStates(IReadOnlyList<ResearchDef> researches)
    {
        for (int i = 0; i < researches.Count; i++)
        {
            ResearchDef research = researches[i];
            var state = new ResearchState(research);
            states.Add(research, state);
            orderedStates.Add(state);
            ObserverNotifications.Publish(ResearchStateAdded, state);
        }
    }

    public SaveManager.ResearchSaveData CaptureSaveData()
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

#if UNITY_EDITOR
    public void ResetForPerformanceTest()
    {
        ResetForLoad();
        RefreshAvailabilityStatuses();
        ObserverNotifications.Publish(ResearchQueueChanged);
    }
#endif

    internal void RestoreSaveData(SaveManager.ResearchSaveData data)
    {
        if (data == null)
            return;

        ExpantaNum restoredEfficiency = ValidateRestoreInput(data);

        // The save is the complete source of truth for research scheduling.
        // Clear transient scheduling pointers only after validation succeeds.
        ActiveResearch = null;
        researchQueue.Clear();
        queuedResearches.Clear();
        researchQueueSnapshotDirty = true;
        SelectedResearchId = string.Empty;
        for (int i = 0; i < orderedStates.Count; i++)
            orderedStates[i].ResetForLoad();

        if (data.States != null)
        {
            var restoredResearches = new HashSet<ResearchDef>();
            for (int i = 0; i < data.States.Count; i++)
            {
                SaveManager.ResearchStateSaveData saved = data.States[i];
                ResearchDef definition = DataBase<ResearchDef>.Find(saved.ResearchId);
                if (!restoredResearches.Add(definition))
                    throw new InvalidOperationException(
                        $"存档中的研究状态重复包含“{definition.Id}”。");
                if (!TryGetStateByStableId(definition, out ResearchState state))
                    throw new InvalidOperationException(
                        $"存档中的研究状态索引缺少：{definition.Id}");
                IReadOnlyDictionary<ResourceDef, ExpantaNum> paidResourceCosts =
                    RestorePaidResourceCosts(
                        definition,
                        saved.PaidResourceCosts);
                state.Restore(
                    Parse(saved.Progress, saved.ResearchId, nameof(saved.Progress)),
                    saved.Completed,
                    paidResourceCosts);
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
            ResearchDef activeDefinition = DataBase<ResearchDef>.Find(data.ActiveResearchId);
            if (!TryGetStateByStableId(activeDefinition, out ResearchState state))
                throw new InvalidOperationException(
                    $"存档中的研究状态索引缺少：{activeDefinition.Id}");
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
                EnqueueState(restoredQueue[i], false);
        }

        if (string.IsNullOrWhiteSpace(data.SelectedResearchId))
        {
            SelectedResearchId = string.Empty;
        }
        else
        {
            ResearchDef selected = DataBase<ResearchDef>.Find(data.SelectedResearchId);
            SelectedResearchId = selected.Id;
        }
        GlobalEfficiencyFactor = restoredEfficiency;
        TryStartNextQueuedResearch(false);
        if (researchQueue.Count > 0)
            ObserverNotifications.Publish(ResearchQueueChanged);
    }

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

    private ExpantaNum ValidateRestoreInput(SaveManager.ResearchSaveData data)
    {
        // A save is the source of truth. Do not seed validation from the
        // current runtime state, otherwise a direct restore can inherit
        // completed research absent from the save payload.
        var completed = new HashSet<ResearchDef>();
        var fullyPaid = new HashSet<ResearchDef>();
        if (data.States != null)
        {
            var seen = new HashSet<ResearchDef>();
            for (int i = 0; i < data.States.Count; i++)
            {
                SaveManager.ResearchStateSaveData saved = data.States[i];
                ResearchDef definition = DataBase<ResearchDef>.Find(saved.ResearchId);
                if (!seen.Add(definition))
                    throw new InvalidOperationException($"Duplicate research state: {definition.Id}");
                ExpantaNum progress = Parse(saved.Progress, saved.ResearchId, nameof(saved.Progress));
                if (progress < ExpantaNum.Zero)
                    throw new InvalidOperationException(
                        $"Research progress is outside the valid range: {definition.Id}");
                IReadOnlyDictionary<ResourceDef, ExpantaNum> paidCosts =
                    RestorePaidResourceCosts(
                        definition,
                        saved.PaidResourceCosts);
                bool allCostsPaid = AreAllResourceCostsPaid(definition, paidCosts);
                if (allCostsPaid)
                    fullyPaid.Add(definition);
                if ((saved.CostPaid || saved.Completed) && !allCostsPaid)
                    throw new InvalidOperationException(
                        $"Research payment ledger is incomplete: {definition.Id}");
                if (saved.Completed)
                    completed.Add(definition);
            }
        }

        foreach (ResearchDef definition in completed)
            for (int i = 0; i < definition.Prerequisites.Count; i++)
                if (!completed.Contains(definition.Prerequisites[i]))
                    throw new InvalidOperationException($"Completed research prerequisite is missing: {definition.Id}");

        ResearchDef active = null;
        if (!string.IsNullOrWhiteSpace(data.ActiveResearchId))
        {
            active = DataBase<ResearchDef>.Find(data.ActiveResearchId);
            if (completed.Contains(active))
                throw new InvalidOperationException($"Active research is already completed: {active.Id}");
            if (active.HasPositiveResourceRequirement && !fullyPaid.Contains(active))
                throw new InvalidOperationException($"Active research has unpaid resource costs: {active.Id}");
            for (int i = 0; i < active.Prerequisites.Count; i++)
                if (!completed.Contains(active.Prerequisites[i]))
                    throw new InvalidOperationException($"Active research prerequisite is missing: {active.Id}");
        }

        var queued = new HashSet<ResearchDef>();
        if (data.QueuedResearchIds != null)
            for (int i = 0; i < data.QueuedResearchIds.Count; i++)
            {
                string id = data.QueuedResearchIds[i];
                if (string.IsNullOrWhiteSpace(id) ||
                    !DataBase<ResearchDef>.TryFind(id, out ResearchDef definition) ||
                    completed.Contains(definition) || definition == active || !queued.Add(definition))
                    throw new InvalidOperationException($"Invalid queued research: {id}");
                for (int j = 0; j < definition.Prerequisites.Count; j++)
                    if (!completed.Contains(definition.Prerequisites[j]) &&
                        definition.Prerequisites[j] != active &&
                        !queued.Contains(definition.Prerequisites[j]))
                        throw new InvalidOperationException($"Queued research prerequisite is missing: {definition.Id}");
            }

        if (!string.IsNullOrWhiteSpace(data.SelectedResearchId))
            DataBase<ResearchDef>.Find(data.SelectedResearchId);
        ExpantaNum efficiency = Parse(
            data.GlobalEfficiencyFactor, nameof(ResearchManager), nameof(data.GlobalEfficiencyFactor));
        if (efficiency < ExpantaNum.Zero)
            throw new ArgumentOutOfRangeException(nameof(data.GlobalEfficiencyFactor));
        return efficiency;
    }

    private List<ResearchState> ValidateRestoredResearchQueue(
        IReadOnlyList<string> savedQueueIds)
    {
        var result = new List<ResearchState>();
        var queuedDefinitions = new HashSet<ResearchDef>();
        for (int i = 0; i < savedQueueIds.Count; i++)
        {
            string id = savedQueueIds[i];
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException(
                    "存档中的研究队列包含空研究 ID。");
            if (!DataBase<ResearchDef>.TryFind(id, out ResearchDef definition))
                throw new InvalidOperationException(
                    $"存档中的研究队列包含未知研究“{id}”。");

            if (!TryGetStateByStableId(definition, out ResearchState state))
                throw new InvalidOperationException(
                    $"存档中的研究状态索引缺少：{definition.Id}");
            if (state.Status == ResearchStatus.Completed)
                throw new InvalidOperationException(
                    $"存档中的研究队列包含已完成研究“{definition.Id}”。");
            if (state == ActiveResearch)
                throw new InvalidOperationException(
                    $"存档中的研究队列与活动研究“{definition.Id}”冲突。");
            if (!queuedDefinitions.Add(definition))
                throw new InvalidOperationException(
                    $"存档中的研究队列重复包含研究“{definition.Id}”。");

            IReadOnlyList<ResearchDef> prerequisites = definition.Prerequisites;
            for (int j = 0; j < prerequisites.Count; j++)
            {
                ResearchDef prerequisite = prerequisites[j];
                bool satisfied =
                    (TryGetStateByStableId(prerequisite, out ResearchState prerequisiteState) &&
                     prerequisiteState.Status == ResearchStatus.Completed) ||
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

        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        for (int i = 0; i < buildingStates.Count; i++)
        {
            BuildingState state = buildingStates[i];
            if (state == null || state.Amount <= ExpantaNum.Zero)
                continue;

            ExpantaNum contribution = state.Definition.ResearchPowerGranted
                * state.Amount
                * ExpantaNum.Clamp01(state.Efficiency)
                * modifiers.GetBuildingResearchPowerMultiplier(state.Definition);
            if (!contribution.IsNaN && contribution > ExpantaNum.Zero)
                total += contribution;
        }

        return ExpantaNum.Max(ExpantaNum.Zero, total);
    }

    private static ExpantaNum Parse(
        string raw,
        string owner,
        string field)
    {
        if (ExpantaNum.TryParse(raw, out ExpantaNum value) && value.IsFinite)
            return value;
        throw new FormatException($"{owner}.{field} 中的 ExpantaNum 值“{raw}”无效。");
    }

    private static List<SaveManager.ResearchResourceCostSaveData> CapturePaidResourceCosts(ResearchState state)
    {
        var result = new List<SaveManager.ResearchResourceCostSaveData>();
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements = state.Definition.ResourceRequirements;
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


    private static IReadOnlyDictionary<ResourceDef, ExpantaNum> RestorePaidResourceCosts(
        ResearchDef definition,
        List<SaveManager.ResearchResourceCostSaveData> savedCosts)
    {
        var result = new Dictionary<ResourceDef, ExpantaNum>();
        if (savedCosts == null)
            throw new InvalidOperationException(
                $"Research payment ledger is missing: {definition.Id}");

        for (int i = 0; i < savedCosts.Count; i++)
        {
            SaveManager.ResearchResourceCostSaveData saved = savedCosts[i];
            ResourceDef resource = DataBase<ResourceDef>.Find(saved.ResourceId);
            if (result.ContainsKey(resource))
                throw new InvalidOperationException($"Duplicate paid resource cost: {resource.Id}");
            ExpantaNum amount = Parse(saved.Amount, saved.ResourceId, nameof(saved.Amount));
            if (!amount.IsFinite || amount < ExpantaNum.Zero)
                throw new InvalidOperationException($"Paid resource cost must be finite and non-negative: {saved.ResourceId}");
            ExpantaNum required = GetRequiredResourceCost(definition, resource);
            if (required <= ExpantaNum.Zero)
                throw new InvalidOperationException(
                    $"Research payment uses a resource not required by {definition.Id}: {resource.Id}");
            // This is a historical payment ledger. A definition price reduction
            // must not invalidate money already spent in the current save format.
            result.Add(resource, amount);
        }
        return result;
    }

    private static ExpantaNum GetRequiredResourceCost(ResearchDef definition, ResourceDef resource)
    {
        ExpantaNum required = ExpantaNum.Zero;
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements = definition.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
            if (requirements[i].First == resource)
                required += ExpantaNum.Max(ExpantaNum.Zero, requirements[i].Second);
        return required;
    }

    private static bool AreAllResourceCostsPaid(
        ResearchDef definition,
        IReadOnlyDictionary<ResourceDef, ExpantaNum> paidCosts)
    {
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements = definition.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            ResourceDef resource = requirements[i].First;
            ExpantaNum required = ExpantaNum.Max(ExpantaNum.Zero, requirements[i].Second);
            if (required > ExpantaNum.Zero &&
                (!paidCosts.TryGetValue(resource, out ExpantaNum paid) || paid < required))
                return false;
        }
        return true;
    }

}
