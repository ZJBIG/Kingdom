using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : Singleton<ResourceManager>
{
    public const string StartingResourceId = "WoodLog";

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

    private readonly Dictionary<Resource, ResourceState> states = new();
    private readonly List<ResourceState> orderedStates = new();
    private sealed class TransactionWorkspace
    {
        public readonly Dictionary<Resource, ExpantaNum> Deltas = new();
        public readonly List<KeyValuePair<Resource, ExpantaNum>> Entries = new();
        public readonly List<ResourceState> AddedStates = new();
        public readonly List<ExpantaNum> PreviousAmounts = new();
        public bool InUse;
    }
    private readonly List<TransactionWorkspace> transactionWorkspaces =
        new() { new TransactionWorkspace() };

    public IReadOnlyDictionary<Resource, ResourceState> States => states;
    public event Action<ResourceState> ResourceStateAdded;
    public event Action<ResourceState> ResourceStateChanged;

    protected override void Initialize()
    {
        EnsureAllResourceStates();
        EnsureStartingResource();
    }

    private void EnsureAllResourceStates()
    {
        IReadOnlyList<Resource> definitions = DataBase<Resource>.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            Resource resource = definitions[i];
            if (resource != null)
                EnsureResource(resource);
        }
    }

    public static bool IsStartingResource(Resource resource) =>
        resource != null &&
        string.Equals(resource.Id, StartingResourceId, StringComparison.OrdinalIgnoreCase);

    public ResourceState EnsureResource(Resource resource)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));
        if (states.TryGetValue(resource, out ResourceState existing))
            return existing;

        var state = new ResourceState(resource);
        states.Add(resource, state);
        InsertOrdered(state);
        Publish(ResourceStateAdded, state);
        return state;
    }

    public void AddResource(Resource resource) => EnsureResource(resource);

    public ResourceState GetState(Resource resource)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));
        if (states.TryGetValue(resource, out ResourceState state))
            return state;
        throw new KeyNotFoundException($"资源状态“{resource.Id}”尚未创建。");
    }

    public ExpantaNum GetAmount(Resource resource) => EnsureResource(resource).Amount;

    public void AddAmount(Resource resource, ExpantaNum delta)
    {
        ResourceState state = EnsureResource(resource);
        if (delta == ExpantaNum.Zero)
            return;
        state.SetAmount(state.Amount + delta);
        Publish(ResourceStateChanged, state);
    }

    /// <summary>
    /// Applies a group of resource debits as one state transaction. Every
    /// balance is checked before any balance is changed, then change events
    /// are raised only after the complete debit has been committed.
    /// </summary>
    public bool TryApplyAtomicPayment(IReadOnlyDictionary<Resource, ExpantaNum> costs)
    {
        return TryApplyAtomicPayment(costs, null);
    }

    /// <summary>
    /// Applies a payment and commits related domain state before publishing
    /// resource-change events. This prevents a refresh/reentrant callback from
    /// observing resources deducted while the research is still unpaid.
    /// </summary>
    public bool TryApplyAtomicPayment(
        IReadOnlyDictionary<Resource, ExpantaNum> costs,
        Action commitState,
        Action rollbackState = null)
    {
        if (costs == null)
            throw new ArgumentNullException(nameof(costs));

        TransactionWorkspace workspace = AcquireTransactionWorkspace();
        try
        {
            workspace.Deltas.Clear();
            foreach (KeyValuePair<Resource, ExpantaNum> entry in costs)
            {
                if (entry.Key == null || !entry.Value.IsFinite ||
                    entry.Value <= ExpantaNum.Zero)
                    return false;
                workspace.Deltas[entry.Key] = -entry.Value;
            }
            return ApplyAtomicChanges(workspace.Deltas, commitState, rollbackState, workspace);
        }
        finally
        {
            ReleaseTransactionWorkspace(workspace);
        }
    }

    public bool TryApplyAtomicChanges(
        IReadOnlyDictionary<Resource, ExpantaNum> deltas,
        Action commitState = null,
        Action rollbackState = null)
    {
        if (deltas == null)
            throw new ArgumentNullException(nameof(deltas));

        TransactionWorkspace workspace = AcquireTransactionWorkspace();
        try
        {
            return ApplyAtomicChanges(deltas, commitState, rollbackState, workspace);
        }
        finally
        {
            ReleaseTransactionWorkspace(workspace);
        }
    }

    private bool ApplyAtomicChanges(
        IReadOnlyDictionary<Resource, ExpantaNum> deltas,
        Action commitState,
        Action rollbackState,
        TransactionWorkspace workspace)
    {
        List<KeyValuePair<Resource, ExpantaNum>> entries = workspace.Entries;
        List<ResourceState> addedStates = workspace.AddedStates;
        List<ExpantaNum> previousAmounts = workspace.PreviousAmounts;
        entries.Clear();
        addedStates.Clear();
        previousAmounts.Clear();
        foreach (KeyValuePair<Resource, ExpantaNum> entry in deltas)
        {
            if (entry.Key == null || !entry.Value.IsFinite)
                return false;

            entries.Add(entry);
        }

        // Validate every entry before EnsureResource can add any runtime
        // state. A rejected transaction must not mutate the state registry.
        for (int i = 0; i < entries.Count; i++)
        {
            KeyValuePair<Resource, ExpantaNum> entry = entries[i];
            ResourceState state = states.TryGetValue(entry.Key, out ResourceState existing)
                ? existing
                : null;
            if (entry.Value < ExpantaNum.Zero &&
                (state == null ? ExpantaNum.Zero : state.Amount) < -entry.Value)
                return false;
        }

        // Register states silently until the domain commit succeeds. A
        // failed transaction must not leak a new state or publish an
        // Added notification that observers cannot undo.
        for (int i = 0; i < entries.Count; i++)
        {
            Resource resource = entries[i].Key;
            if (states.ContainsKey(resource))
                continue;

            ResourceState state = new ResourceState(resource);
            states.Add(resource, state);
            InsertOrdered(state);
            addedStates.Add(state);
        }

        for (int i = 0; i < entries.Count; i++)
            previousAmounts.Add(states[entries[i].Key].Amount);

        try
        {
            for (int i = 0; i < entries.Count; i++)
            {
                KeyValuePair<Resource, ExpantaNum> entry = entries[i];
                ResourceState state = states[entry.Key];
                state.SetAmount(state.Amount + entry.Value);
            }

            commitState?.Invoke();
        }
        catch (Exception)
        {
            Exception rollbackException = null;
            try
            {
                try
                {
                    rollbackState?.Invoke();
                }
                catch (Exception exception)
                {
                    rollbackException = exception;
                }
            }
            finally
            {
                for (int i = 0; i < entries.Count; i++)
                    states[entries[i].Key].SetAmount(previousAmounts[i]);

                for (int i = addedStates.Count - 1; i >= 0; i--)
                {
                    ResourceState state = addedStates[i];
                    states.Remove(state.Definition);
                    orderedStates.Remove(state);
                }
            }
            if (rollbackException != null)
                Debug.LogException(rollbackException);
            throw;
        }

        for (int i = 0; i < addedStates.Count; i++)
            Publish(ResourceStateAdded, addedStates[i]);
        for (int i = 0; i < entries.Count; i++)
        {
            ResourceState state = states[entries[i].Key];
            Publish(ResourceStateChanged, state);
        }
        return true;
    }

    private TransactionWorkspace AcquireTransactionWorkspace()
    {
        for (int i = 0; i < transactionWorkspaces.Count; i++)
        {
            TransactionWorkspace workspace = transactionWorkspaces[i];
            if (workspace.InUse)
                continue;
            workspace.InUse = true;
            return workspace;
        }

        TransactionWorkspace created = new();
        created.InUse = true;
        transactionWorkspaces.Add(created);
        return created;
    }

    private static void ReleaseTransactionWorkspace(TransactionWorkspace workspace)
    {
        workspace.InUse = false;
    }

    private static void Publish(Action<ResourceState> handlers, ResourceState state)
    {
        if (handlers == null)
            return;
        Delegate[] invocationList = handlers.GetInvocationList();
        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<ResourceState>)invocationList[i]).Invoke(state);
            }
            catch (Exception exception)
            {
                // Notifications are observers, not part of the committed
                // economy transaction. One broken UI listener must not make
                // a successful payment appear to fail or trigger a refund.
                Debug.LogException(exception);
            }
        }
    }

    public void SetAmount(Resource resource, ExpantaNum amount) => EnsureResource(resource).SetAmount(amount);
    public void SetProductionRate(Resource resource, ExpantaNum rate) => EnsureResource(resource).SetProductionRate(rate);
    public void SetConsumptionRate(Resource resource, ExpantaNum rate) => EnsureResource(resource).SetConsumptionRate(rate);

    internal void ResetDerivedRates()
    {
        foreach (ResourceState state in states.Values)
        {
            state.SetProductionRate(ExpantaNum.Zero);
            state.SetConsumptionRate(ExpantaNum.Zero);
            state.SetEfficiency(ExpantaNum.One);
        }
    }

    internal void ResetForLoad()
    {
        for (int i = 0; i < orderedStates.Count; i++)
            orderedStates[i].ResetForLoad();
        GlobalEfficiencyFactor = ExpantaNum.One;
    }

    public void AdjustProductionRate(Resource resource, ExpantaNum delta)
    {
        ResourceState state = EnsureResource(resource);
        state.SetProductionRate(state.ProductionRate + delta);
    }

    public void AdjustConsumptionRate(Resource resource, ExpantaNum delta)
    {
        ResourceState state = EnsureResource(resource);
        state.SetConsumptionRate(state.ConsumptionRate + delta);
    }

    public static ExpantaNum AdvanceAmount(
        ExpantaNum current,
        ExpantaNum productionRate,
        ExpantaNum consumptionRate,
        double deltaSeconds)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        return ExpantaNum.Max(
            ExpantaNum.Zero,
            current + (productionRate - consumptionRate) * deltaSeconds);
    }

    public static ExpantaNum CalculateSatisfaction(
        ExpantaNum currentInventory,
        ExpantaNum potentialProductionRate,
        ExpantaNum potentialConsumptionRate,
        double deltaSeconds)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        ExpantaNum available = ExpantaNum.Max(ExpantaNum.Zero, currentInventory) +
            ExpantaNum.Max(ExpantaNum.Zero, potentialProductionRate) * deltaSeconds;
        ExpantaNum demand = ExpantaNum.Max(ExpantaNum.Zero, potentialConsumptionRate) * deltaSeconds;
        if (demand <= ExpantaNum.Zero)
            return ExpantaNum.One;

        return ExpantaNum.Clamp01(available / demand);
    }

    internal void BeginTick()
    {
        ExpantaNum happinessMultiplier = GetHappinessRewardMultiplier();
        for (int i = 0; i < orderedStates.Count; i++)
            orderedStates[i].BeginTick(happinessMultiplier);
    }

    internal void AdjustTickPotentialProduction(Resource resource, ExpantaNum delta)
    {
        EnsureResource(resource).AdjustTickPotentialProductionRate(delta);
    }

    internal void AdjustTickPotentialConsumption(Resource resource, ExpantaNum delta)
    {
        EnsureResource(resource).AdjustTickPotentialConsumptionRate(delta);
    }

    internal void CalculateTickSatisfaction(double deltaSeconds)
    {
        for (int i = 0; i < orderedStates.Count; i++)
            orderedStates[i].CalculateTickSatisfaction(deltaSeconds);
    }

    internal ExpantaNum GetTickSatisfaction(Resource resource) =>
        GetState(resource).GetTickSatisfactionOrFallback();

    public void Tick(double deltaSeconds)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        ExpantaNum happinessMultiplier = GetHappinessRewardMultiplier();
        for (int i = 0; i < orderedStates.Count; i++)
        {
            ResourceState state = orderedStates[i];
            state.SetAmount(AdvanceAmount(
                state.Amount,
                state.ProductionRate * happinessMultiplier,
                state.ConsumptionRate,
                deltaSeconds));
        }

    }

    private static ExpantaNum GetHappinessRewardMultiplier()
    {
        GameManager gameManager = GameManager.Instance;
        return gameManager?.State?.HappinessRewardMultiplier ?? ExpantaNum.One;
    }

    private void InsertOrdered(ResourceState state)
    {
        int low = 0;
        int high = orderedStates.Count;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            int comparison = string.Compare(
                orderedStates[middle].Definition.Id,
                state.Definition.Id,
                StringComparison.OrdinalIgnoreCase);
            if (comparison < 0)
                low = middle + 1;
            else
                high = middle;
        }
        orderedStates.Insert(low, state);
    }

    internal SaveManager.ResourceSaveData CaptureSaveData()
    {
        var saveData = new SaveManager.ResourceSaveData
        {
            GlobalEfficiencyFactor = GlobalEfficiencyFactor.ToString(),
            Resources = new List<SaveManager.ResourceStateSaveData>(states.Count)
        };

        foreach (ResourceState state in states.Values)
        {
            saveData.Resources.Add(new SaveManager.ResourceStateSaveData
            {
                ResourceId = state.Definition.Id,
                Amount = state.Amount.ToString()
            });
        }

        return saveData;
    }

    internal void RestoreSaveData(SaveManager.ResourceSaveData saveData)
    {
        if (saveData == null)
            return;

        if (saveData.Resources != null)
        {
            var restoredResources = new HashSet<Resource>();
            var restoredAmounts = new Dictionary<Resource, ExpantaNum>();
            for (int i = 0; i < saveData.Resources.Count; i++)
            {
                SaveManager.ResourceStateSaveData data = saveData.Resources[i];
                string resourceId =
                    RetiredDefinitionMigration.NormalizeResourceId(data.ResourceId);
                if (!DataBase<Resource>.TryFind(resourceId, out Resource resource))
                {
                    if (RetiredDefinitionMigration.IsRetired(data.ResourceId))
                        RetiredDefinitionMigration.LogOnce();
                    else
                        throw new InvalidOperationException(
                            $"存档包含未知资源编号“{data.ResourceId}”。");
                    continue;
                }
                if (!restoredResources.Add(resource))
                    throw new InvalidOperationException(
                        $"存档中的资源状态重复包含“{resource.Id}”。");
                ExpantaNum amount = Parse(data.Amount, resource.Id, nameof(data.Amount));
                if (amount < ExpantaNum.Zero)
                    throw new InvalidOperationException(
                        $"Resource amount cannot be negative: {resource.Id}");
                restoredAmounts.Add(resource, amount);
            }

            ExpantaNum restoredGlobalEfficiencyFactor = Parse(
                saveData.GlobalEfficiencyFactor,
                nameof(ResourceManager),
                nameof(saveData.GlobalEfficiencyFactor),
                ExpantaNum.One);
            if (restoredGlobalEfficiencyFactor < ExpantaNum.Zero)
                throw new InvalidOperationException("Resource global efficiency cannot be negative.");
            foreach (KeyValuePair<Resource, ExpantaNum> entry in restoredAmounts)
                EnsureResource(entry.Key).SetAmount(entry.Value);
            GlobalEfficiencyFactor = restoredGlobalEfficiencyFactor;
        }
        else
        {
            GlobalEfficiencyFactor = Parse(
                saveData.GlobalEfficiencyFactor,
                nameof(ResourceManager),
                nameof(saveData.GlobalEfficiencyFactor),
                ExpantaNum.One);
        }

        EnsureStartingResource();
    }

    internal ResourceState EnsureStartingResource()
    {
        Resource woodLog = DataBase<Resource>.Find(StartingResourceId);
        ResourceState state = EnsureResource(woodLog);
        if (state.ProductionRate < ExpantaNum.One)
            state.SetProductionRate(ExpantaNum.One);
        return state;
    }

    public override void Save() => SaveManager.Instance.SaveNow(true);

    public override void Load() => SaveManager.Instance.LoadOrCreateGame();

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
}
