using System;
using System.Collections.Generic;

public enum ResearchStatus
{
    Locked,
    Available,
    WaitingResources,
    Researching,
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
                $"Research definition '{definition.name}' has invalid BaseCost '{definition.BaseCost}'.");
        }

        BaseCost = parsedCost;
        status = definition.Prerequisites == null || definition.Prerequisites.Count == 0
            ? ResearchStatus.Available
            : ResearchStatus.Locked;
    }

    internal void SetProgress(ExpantaNum value) =>
        Change(ref progress, ExpantaNum.Clamp(value, ExpantaNum.Zero, BaseCost));
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
        SetCostPaid(false);
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
        SetProgress(restoredProgress);
        SetCostPaid(restoredCostPaid);
        ResetPaidResourceCosts();
        if (restoredPaidResourceCosts != null)
        {
            foreach (KeyValuePair<Resource, ExpantaNum> entry in restoredPaidResourceCosts)
                SetPaidResourceCost(entry.Key, entry.Value);
        }
        if (completed)
        {
            SetProgress(BaseCost);
            SetStatus(ResearchStatus.Completed);
        }
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
}
