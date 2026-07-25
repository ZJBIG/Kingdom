using System;
using System.Collections.Generic;

public class ResourceManager : Singleton<ResourceManager>
{
    public ExpantaNum GlobalEfficiencyFactor { get; set; } = ExpantaNum.One;

    private readonly Dictionary<Resource, ResourceState> states = new();
    private readonly List<ResourceState> orderedStates = new();

    public IReadOnlyDictionary<Resource, ResourceState> States => states;
    public event Action<ResourceState> ResourceStateAdded;
    public event Action<ResourceState> ResourceStateChanged;

    protected override void Initialize()
    {
        EnsureStartingProduction();
    }

    public ResourceState EnsureResource(Resource resource)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));
        if (states.TryGetValue(resource, out ResourceState existing))
            return existing;

        var state = new ResourceState(resource);
        states.Add(resource, state);
        InsertOrdered(state);
        ResourceStateAdded?.Invoke(state);
        return state;
    }

    public void AddResource(Resource resource) => EnsureResource(resource);

    public ResourceState GetState(Resource resource)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));
        if (states.TryGetValue(resource, out ResourceState state))
            return state;
        throw new KeyNotFoundException($"Resource state '{resource.Id}' has not been created.");
    }

    public ExpantaNum GetAmount(Resource resource) => GetState(resource).Amount;

    public void AddAmount(Resource resource, ExpantaNum delta)
    {
        ResourceState state = EnsureResource(resource);
        state.SetAmount(state.Amount + delta);
        ResourceStateChanged?.Invoke(state);
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
        if (deltaSeconds < 0d)
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
        if (deltaSeconds < 0d)
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
        for (int i = 0; i < orderedStates.Count; i++)
            orderedStates[i].BeginTick();
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
        for (int i = 0; i < orderedStates.Count; i++)
        {
            ResourceState state = orderedStates[i];
            state.SetAmount(AdvanceAmount(
                state.Amount,
                state.ProductionRate,
                state.ConsumptionRate,
                deltaSeconds));
        }

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
            for (int i = 0; i < saveData.Resources.Count; i++)
            {
                SaveManager.ResourceStateSaveData data = saveData.Resources[i];
                Resource resource = DataBase<Resource>.Find(data.ResourceId);
                ResourceState state = EnsureResource(resource);
                state.SetAmount(Parse(data.Amount, resource.Id, nameof(data.Amount)));
            }
        }

        GlobalEfficiencyFactor = Parse(
            saveData.GlobalEfficiencyFactor,
            nameof(ResourceManager),
            nameof(saveData.GlobalEfficiencyFactor),
            ExpantaNum.One);

        EnsureStartingProduction();
    }

    private void EnsureStartingProduction()
    {
        Resource woodLog = DataBase<Resource>.Find("WoodLog");
        ResourceState state = EnsureResource(woodLog);
        if (state.ProductionRate < ExpantaNum.One)
            state.SetProductionRate(ExpantaNum.One);
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
        throw new FormatException($"Invalid ExpantaNum '{raw}' for {owner}.{field}.");
    }
}
