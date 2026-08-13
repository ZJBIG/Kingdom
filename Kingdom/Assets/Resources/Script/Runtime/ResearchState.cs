using System;
using System.Collections.Generic;

public enum ResearchStatus
{
    Locked,
    Available,
    WaitingResources,
    Researching,
    Queued,
    Completed
}

[Serializable]
public sealed class ResearchState
{
    private ExpantaNum progress;
    private ResearchStatus status;
    private bool costPaid;
    private readonly Dictionary<Resource, ExpantaNum> paidResourceCosts = new();

    public Research Definition { get; }
    public ExpantaNum Progress => progress;
    public ResearchStatus Status => status;
    public bool CostPaid => costPaid;
    public int Version { get; private set; }
    public ExpantaNum BaseCost { get; }

    public ExpantaNum ProgressRatio => BaseCost <= ExpantaNum.Zero
        ? ExpantaNum.One
        : ExpantaNum.Clamp01(progress / BaseCost);

    public ResearchState(Research definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (!ExpantaNum.TryParse(definition.BaseCost, out ExpantaNum parsedCost) || parsedCost < ExpantaNum.Zero)
        {
            throw new FormatException(
                $"研究定义“{definition.name}”的基础成本“{definition.BaseCost}”无效。");
        }

        BaseCost = parsedCost;
        status = definition.Prerequisites == null || definition.Prerequisites.Count == 0
            ? ResearchStatus.Available
            : ResearchStatus.Locked;
        costPaid = !definition.HasPositiveResourceRequirement;
    }

    internal void SetProgress(ExpantaNum value) =>
        Change(ref progress, ClampFinite(value, ExpantaNum.Zero, BaseCost, nameof(value)));
    internal void SetStatus(ResearchStatus value) => Change(ref status, value);
    internal void SetCostPaid(bool value) => Change(ref costPaid, value);

    public ExpantaNum GetPaidResourceCost(Resource resource)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));
        return paidResourceCosts.TryGetValue(resource, out ExpantaNum paid)
            ? paid
            : ExpantaNum.Zero;
    }

    internal void SetPaidResourceCost(Resource resource, ExpantaNum amount)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));
        if (!amount.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(amount));
        ExpantaNum normalized = ExpantaNum.Max(ExpantaNum.Zero, amount);
        ExpantaNum previous = GetPaidResourceCost(resource);
        if (previous == normalized)
            return;
        paidResourceCosts[resource] = normalized;
        Version++;
    }

    internal void ResetPaidResourceCosts() => paidResourceCosts.Clear();

    internal void ResetForLoad()
    {
        SetProgress(ExpantaNum.Zero);
        SetCostPaid(!Definition.HasPositiveResourceRequirement);
        ResetPaidResourceCosts();
        SetStatus(Definition.Prerequisites == null || Definition.Prerequisites.Count == 0
            ? ResearchStatus.Available
            : ResearchStatus.Locked);
    }

    internal void Restore(ExpantaNum restoredProgress, bool restoredCostPaid, bool completed)
    {
        Restore(restoredProgress, restoredCostPaid, completed, null);
    }

    internal void Restore(
        ExpantaNum restoredProgress,
        bool restoredCostPaid,
        bool completed,
        IReadOnlyDictionary<Resource, ExpantaNum> restoredPaidResourceCosts)
    {
        if (!restoredProgress.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(restoredProgress));
        if (restoredProgress < ExpantaNum.Zero || restoredProgress > BaseCost)
            throw new ArgumentOutOfRangeException(nameof(restoredProgress));
        SetProgress(restoredProgress);
        ResetPaidResourceCosts();
        if (restoredPaidResourceCosts != null)
        {
            foreach (KeyValuePair<Resource, ExpantaNum> entry in restoredPaidResourceCosts)
            {
                ExpantaNum required = GetRequiredResourceCost(entry.Key);
                if (required <= ExpantaNum.Zero)
                    throw new ArgumentException(
                        "Restored payment contains a resource not required by this research.",
                        nameof(restoredPaidResourceCosts));
                if (!entry.Value.IsFinite || entry.Value < ExpantaNum.Zero)
                    throw new ArgumentOutOfRangeException(nameof(restoredPaidResourceCosts));
                if (entry.Value > required)
                    throw new ArgumentOutOfRangeException(nameof(restoredPaidResourceCosts));
                SetPaidResourceCost(entry.Key, entry.Value);
            }
        }

        // The serialized boolean is only a cache. Recompute payment status
        // from the resource ledger so corrupt or hand-edited saves cannot
        // grant a free research start.
        SetCostPaid(AreAllResourceCostsPaid());
        if (completed)
        {
            if (!AreAllResourceCostsPaid())
                throw new InvalidOperationException(
                    "A research cannot be restored as completed before all resource costs are paid.");
            SetProgress(BaseCost);
            SetStatus(ResearchStatus.Completed);
            SetCostPaid(true);
        }
    }

    private ExpantaNum GetRequiredResourceCost(Resource resource)
    {
        if (resource == null)
            return ExpantaNum.Zero;

        ExpantaNum required = ExpantaNum.Zero;
        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements =
            Definition.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
            if (requirements[i].First == resource)
                required += ExpantaNum.Max(ExpantaNum.Zero, requirements[i].Second);
        return required;
    }

    private bool AreAllResourceCostsPaid()
    {
        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements =
            Definition.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = requirements[i];
            if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                continue;
            if (GetPaidResourceCost(requirement.First) < GetRequiredResourceCost(requirement.First))
                return false;
        }
        return true;
    }

    private void Change(ref ExpantaNum field, ExpantaNum value)
    {
        if (field == value)
            return;
        field = value;
        Version++;
    }

    private void Change(ref ResearchStatus field, ResearchStatus value)
    {
        if (field == value)
            return;
        field = value;
        Version++;
    }

    private void Change(ref bool field, bool value)
    {
        if (field == value)
            return;
        field = value;
        Version++;
    }

    private static ExpantaNum ClampFinite(
        ExpantaNum value,
        ExpantaNum minimum,
        ExpantaNum maximum,
        string parameterName)
    {
        if (!value.IsFinite)
            throw new ArgumentOutOfRangeException(parameterName);
        return ExpantaNum.Clamp(value, minimum, maximum);
    }
}
