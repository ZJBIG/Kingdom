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

    public ExpantaNum GlobalEfficiencyFactor { get; set; } = ExpantaNum.One;
    public ExpantaNum ResearchPower { get; private set; } = BaseResearchPower;

    private readonly Dictionary<Research, ResearchState> states = new();
    private readonly Dictionary<TechLevel, int> researchCountByTech = new();
    private readonly List<ResearchState> orderedStates = new();
    private readonly Queue<ResearchState> researchQueue = new();

    public IReadOnlyDictionary<Research, ResearchState> States => states;
    public IReadOnlyDictionary<TechLevel, int> ResearchCountByTech => researchCountByTech;
    public ResearchState ActiveResearch { get; private set; }
    public IReadOnlyList<ResearchState> ResearchQueue => researchQueue.ToList();
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
        research != null && researchQueue.Any(state => state.Definition == research);

    public bool EnqueueResearch(Research research)
    {
        if (research == null || !states.TryGetValue(research, out ResearchState state) ||
            state.Status == ResearchStatus.Completed || ActiveResearch == state ||
            IsQueued(research))
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
        for (int i = 0; i < remaining.Count; i++)
            researchQueue.Enqueue(remaining[i]);

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
            state.SetStatus(ResearchStatus.WaitingResources);
            ResearchQueueChanged?.Invoke();
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
        return ResearchPaymentResult.Paid;
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
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (ActiveResearch == null)
            TryStartNextQueuedResearch();
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
        var outstanding = new Dictionary<Resource, ExpantaNum>();
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = requirements[i];
            if (requirement.Second <= ExpantaNum.Zero)
                continue;

            ExpantaNum paid = state.GetPaidResourceCost(requirement.First);
            ExpantaNum remaining = ExpantaNum.Max(ExpantaNum.Zero, requirement.Second - paid);
            if (remaining <= ExpantaNum.Zero)
                continue;
            outstanding[requirement.First] = outstanding.TryGetValue(
                requirement.First,
                out ExpantaNum current)
                ? current + remaining
                : remaining;
        }

        return ResourceManager.Instance.TryApplyAtomicPayment(
            outstanding,
            () =>
            {
                foreach (KeyValuePair<Resource, ExpantaNum> entry in outstanding)
                {
                    state.SetPaidResourceCost(
                        entry.Key,
                        state.GetPaidResourceCost(entry.Key) + entry.Value);
                }

                state.SetCostPaid(true);
            });
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
        ResearchQueueChanged?.Invoke();
    }

    private void RemoveQueuedState(ResearchState state)
    {
        List<ResearchState> remaining = researchQueue
            .Where(value => value != state)
            .ToList();
        researchQueue.Clear();
        for (int i = 0; i < remaining.Count; i++)
            researchQueue.Enqueue(remaining[i]);
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
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
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

        if (data.States != null)
        {
            for (int i = 0; i < data.States.Count; i++)
            {
                SaveManager.ResearchStateSaveData saved = data.States[i];
                ResearchState state = GetState(DataBase<Research>.Find(saved.ResearchId));
                state.Restore(
                    Parse(saved.Progress, saved.ResearchId, nameof(saved.Progress)),
                    saved.CostPaid,
                    saved.Completed,
                    RestorePaidResourceCosts(saved.PaidResourceCosts));
            }
        }

        ProgressionModifierState previousModifiers = ProgressionModifierManager.Current;
        RebuildProgressionModifiers();
        BuildingManager buildingManager = FindObjectOfType<BuildingManager>();
        buildingManager?.ApplyProgressionModifierChange(
            previousModifiers,
            ProgressionModifierManager.Current);
        RebuildResearchPower(buildingManager?.OrderedStates);
        RefreshAvailabilityStatuses();
        researchQueue.Clear();
        if (!string.IsNullOrWhiteSpace(data.ActiveResearchId))
        {
            ResearchState state = GetState(DataBase<Research>.Find(data.ActiveResearchId));
            if (state.Status != ResearchStatus.Completed && ArePrerequisitesCompleted(state.Definition))
            {
                ActiveResearch = state;
                state.SetStatus(state.CostPaid
                    ? ResearchStatus.Researching
                    : ResearchStatus.WaitingResources);
            }
        }

        if (data.QueuedResearchIds != null)
        {
            HashSet<Research> restoredQueue = new();
            for (int i = 0; i < data.QueuedResearchIds.Count; i++)
            {
                string id = data.QueuedResearchIds[i];
                if (string.IsNullOrWhiteSpace(id) ||
                    !DataBase<Research>.TryFind(id, out Research queued) ||
                    !states.TryGetValue(queued, out ResearchState queuedState) ||
                    queuedState.Status == ResearchStatus.Completed ||
                    queuedState == ActiveResearch || !restoredQueue.Add(queued))
                    continue;
                EnqueueState(queuedState);
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
        if (ExpantaNum.TryParse(raw, out ExpantaNum value))
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

    private static IReadOnlyDictionary<Resource, ExpantaNum> RestorePaidResourceCosts(
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
            result[resource] = Parse(saved.Amount, saved.ResourceId, nameof(saved.Amount));
        }
        return result;
    }

}
