using System;
using System.Collections.Generic;
using UnityEngine;

public enum BuildFailure
{
    None,
    InvalidAmount,
    TechnologyInsufficient,
    AutoBuildUnavailable,
    ResourceInsufficient,
    SpaceInsufficient,
    ProductivityInsufficient,
    WorkforceInsufficient,
    DeconstructionUnavailable
}

public class BuildingManager : Singleton<BuildingManager>
{
    private const double DeconstructionReturnRate = 0.2d;
    private readonly Dictionary<Building, BuildingState> states = new();
    private readonly List<BuildingState> orderedStates = new();

    public IReadOnlyDictionary<Building, BuildingState> States => states;
    internal IReadOnlyList<BuildingState> OrderedStates => orderedStates;
    public ExpantaNum GlobalEfficiencyFactor { get; set; } = ExpantaNum.One;
    public event Action<BuildingState> BuildingStateAdded;

    public BuildingState EnsureBuilding(Building building)
    {
        if (building == null)
            throw new ArgumentNullException(nameof(building));
        if (states.TryGetValue(building, out BuildingState existing))
            return existing;

        var state = new BuildingState(building);
        states.Add(building, state);
        InsertOrdered(state);

        EnsureBuildingResources(building);
        BuildingStateAdded?.Invoke(state);
        return state;
    }

    public void AddBuilding(Building building) => EnsureBuilding(building);

    public BuildingState GetState(Building building)
    {
        if (building == null)
            throw new ArgumentNullException(nameof(building));
        if (states.TryGetValue(building, out BuildingState state))
            return state;
        throw new KeyNotFoundException($"Building state '{building.Id}' has not been created.");
    }

    public bool SetAutoBuild(Building building, bool enabled)
    {
        BuildingState state = EnsureBuilding(building);
        if (enabled && !CanAutoBuild(building))
        {
            state.SetAutoBuild(false);
            return false;
        }

        state.SetAutoBuild(enabled);
        return true;
    }

    public bool CanAutoBuild(Building building)
    {
        if (building == null || GameManager.Instance.State.TechLevel < TechLevel.Neolithic)
            return false;
        return building.TechLevel <= GameManager.Instance.State.TechLevel &&
               building.AutoBuildWorkRequired > ExpantaNum.Zero;
    }

    public ExpantaNum GetAutoBuildWorkRequired(Building building)
    {
        if (!CanAutoBuild(building))
            return ExpantaNum.Zero;

        BuildingState state = EnsureBuilding(building);
        return state.AutoBuildWorkRequired * building.CostGrowth.Pow(state.Amount);
    }

    public bool TryBuild(Building building, ExpantaNum requestedAmount, out BuildFailure failure)
    {
        if (building == null)
        {
            failure = BuildFailure.InvalidAmount;
            return false;
        }

        if (building.TechLevel > GameManager.Instance.State.TechLevel)
        {
            failure = BuildFailure.TechnologyInsufficient;
            return false;
        }

        BuildingState state = EnsureBuilding(building);
        ExpantaNum amount = requestedAmount.Floor();
        if (amount < ExpantaNum.One)
        {
            failure = BuildFailure.InvalidAmount;
            return false;
        }

        ExpantaNum requiredSpace = state.SpaceCost * amount;
        if (GameManager.Instance.State.AvailableTerritory < requiredSpace)
        {
            failure = BuildFailure.SpaceInsufficient;
            return false;
        }

        ExpantaNum requiredWorkforce = state.WorkforceConsumption * amount;
        if (GameManager.Instance.State.Population.AvailableWorkforce < requiredWorkforce)
        {
            failure = BuildFailure.WorkforceInsufficient;
            return false;
        }

        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = building.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = requirements[i];
            ExpantaNum totalCost = pair.Second.GeometricSeriesCost(
                building.CostGrowth,
                state.Amount,
                amount);
            if (totalCost.IsNaN || ResourceManager.Instance.GetAmount(pair.First) < totalCost)
            {
                failure = BuildFailure.ResourceInsufficient;
                return false;
            }
        }

        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = requirements[i];
            ExpantaNum totalCost = pair.Second.GeometricSeriesCost(
                building.CostGrowth,
                state.Amount,
                amount);
            ResourceManager.Instance.AddAmount(pair.First, -totalCost);
        }

        GameManager.Instance.CommitConstruction(
            requiredSpace,
            requiredWorkforce,
            state.ProductivityGranted * amount);
        SetAmountAndRates(state, state.Amount + amount);
        RefreshResearchPower();
        failure = BuildFailure.None;
        return true;
    }

    public bool TryDeconstruct(Building building, ExpantaNum requestedAmount)
    {
        return TryDeconstruct(building, requestedAmount, out _);
    }

    public bool TryDeconstruct(
        Building building,
        ExpantaNum requestedAmount,
        out BuildFailure failure)
    {
        if (building == null || !states.TryGetValue(building, out BuildingState state))
        {
            failure = BuildFailure.DeconstructionUnavailable;
            return false;
        }

        ExpantaNum amount = BuildingTransactionRules.ClampToAvailable(requestedAmount, state.Amount);
        if (amount < ExpantaNum.One)
        {
            failure = BuildFailure.InvalidAmount;
            return false;
        }

        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = building.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = requirements[i];
            ExpantaNum refund = pair.Second.GeometricSeriesCost(
                building.CostGrowth,
                state.Amount - amount,
                amount);
            ResourceManager.Instance.AddAmount(
                pair.First,
                refund * DeconstructionReturnRate);
        }

        GameManager.Instance.RefundConstruction(
            state.SpaceCost * amount,
            state.ProductivityConsumption * amount,
            state.ProductivityGranted * amount);
        SetAmountAndRates(state, state.Amount - amount);
        RefreshResearchPower();
        failure = BuildFailure.None;
        return true;
    }

    public ExpantaNum GetMaxBuildable(Building building, ExpantaNum requestedMaximum)
    {
        if (building == null || building.TechLevel > GameManager.Instance.State.TechLevel)
            return ExpantaNum.Zero;

        BuildingState state = EnsureBuilding(building);
        ExpantaNum result = ExpantaNum.Max(ExpantaNum.Zero, requestedMaximum.Floor());
        if (result < ExpantaNum.One)
            return ExpantaNum.Zero;

        if (state.SpaceCost > ExpantaNum.Zero)
                result = ExpantaNum.Min(result, (GameManager.Instance.State.AvailableTerritory / state.SpaceCost).Floor());
        if (state.ProductivityConsumption > ExpantaNum.Zero)
        {
            result = ExpantaNum.Min(
                result,
                (GameManager.Instance.State.Population.AvailableWorkforce / state.WorkforceConsumption).Floor());
        }

        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = building.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = requirements[i];
            if (pair.Second <= ExpantaNum.Zero)
                continue;
            result = ExpantaNum.Min(
                result,
                ResourceManager.Instance.GetAmount(pair.First).MaxAffordableGeometricSeries(
                    pair.Second,
                    building.CostGrowth,
                    state.Amount));
        }

        return ExpantaNum.Max(ExpantaNum.Zero, result);
    }

    internal void RefreshEfficiencies()
    {
        for (int i = 0; i < orderedStates.Count; i++)
        {
            BuildingState state = orderedStates[i];
            ExpantaNum efficiency = CalculateEfficiency(state.Definition);
            if (efficiency == state.Efficiency)
                continue;
            ApplyRateDelta(state, state.Amount, state.Efficiency, state.Amount, efficiency);
            state.SetEfficiency(efficiency);
        }
        RefreshResearchPower();
    }

    internal void PrepareTickResourceSatisfaction(double deltaSeconds)
    {
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        ResourceManager resourceManager = ResourceManager.Instance;
        resourceManager.BeginTick();
        ExpantaNum potentialFoodProduction = ExpantaNum.Zero;
        ExpantaNum potentialFoodConsumption = ExpantaNum.Zero;
        for (int i = 0; i < orderedStates.Count; i++)
        {
            BuildingState state = orderedStates[i];
            ExpantaNum potentialScale = state.Amount * ExpantaNum.Clamp01(GlobalEfficiencyFactor);
            ExpantaNum actualScale = state.Amount * state.Efficiency;

            potentialFoodProduction += potentialScale * state.Definition.FoodProductionRate;
            potentialFoodConsumption += potentialScale * state.Definition.FoodConsumptionRate;

            IReadOnlyList<Pair<Resource, ExpantaNum>> generation = state.Definition.ResourceGenerationRates;
            for (int j = 0; j < generation.Count; j++)
                resourceManager.AdjustTickPotentialProduction(
                    generation[j].First,
                    (potentialScale - actualScale) * generation[j].Second);

            IReadOnlyList<Pair<Resource, ExpantaNum>> consumption = state.Definition.ResourceConsumptionRates;
            for (int j = 0; j < consumption.Count; j++)
                resourceManager.AdjustTickPotentialConsumption(
                    consumption[j].First,
                    (potentialScale - actualScale) * consumption[j].Second);
        }

        GameManager.Instance.PrepareFoodSatisfaction(
            potentialFoodProduction,
            potentialFoodConsumption,
            deltaSeconds);
        resourceManager.CalculateTickSatisfaction(deltaSeconds);
    }

    internal void AdvanceAutoBuild(double deltaSeconds)
    {
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        int enabledCount = 0;
        for (int i = 0; i < orderedStates.Count; i++)
            if (orderedStates[i].AutoBuild)
                enabledCount++;
        if (enabledCount == 0)
            return;

        ExpantaNum effortPerBuilding = GameManager.Instance.State.Population.AvailableWorkforce / enabledCount;
        for (int i = 0; i < orderedStates.Count; i++)
        {
            BuildingState state = orderedStates[i];
            if (!state.AutoBuild || state.AutoBuildWorkRequired <= ExpantaNum.Zero)
                continue;
            if (!CanAutoBuild(state.Definition))
            {
                state.SetAutoBuild(false);
                continue;
            }

            state.AddAutoBuildProgress(
                effortPerBuilding * deltaSeconds * GetAutoBuildEfficiency(GameManager.Instance.State.TechLevel));
            ExpantaNum workRequired = GetAutoBuildWorkRequired(state.Definition);
            ExpantaNum candidate = (state.AutoBuildProgress / workRequired).Floor();
            if (candidate < ExpantaNum.One)
                continue;

            ExpantaNum affordable = GetMaxBuildable(state.Definition, candidate);
            if (affordable < ExpantaNum.One)
                continue;

            if (TryBuild(state.Definition, affordable, out _))
                state.SpendAutoBuildProgress(state.AutoBuildWorkRequired * affordable);
        }
    }

    private ExpantaNum CalculateEfficiency(Building building)
    {
        ExpantaNum resourceSatisfaction = ExpantaNum.One;
        IReadOnlyList<Pair<Resource, ExpantaNum>> rates = building.ResourceConsumptionRates;
        for (int i = 0; i < rates.Count; i++)
            resourceSatisfaction *= ResourceManager.Instance.GetTickSatisfaction(rates[i].First);

        return CalculateEffectiveEfficiency(
            GlobalEfficiencyFactor,
            resourceSatisfaction,
            GameManager.Instance.State.FoodSatisfaction);
    }

    public static ExpantaNum CalculateEffectiveEfficiency(
        ExpantaNum globalEfficiency,
        ExpantaNum resourceSatisfaction,
        ExpantaNum foodSatisfaction)
    {
        return ExpantaNum.Clamp01(
            globalEfficiency *
            ExpantaNum.Clamp01(resourceSatisfaction) *
            ExpantaNum.Clamp01(foodSatisfaction));
    }

    private static ExpantaNum GetAutoBuildEfficiency(TechLevel techLevel)
    {
        return techLevel switch
        {
            TechLevel.Industrial => new ExpantaNum(1.5d),
            TechLevel.Spacer => new ExpantaNum(2d),
            TechLevel.Ultra => new ExpantaNum(3d),
            TechLevel.Archotech => new ExpantaNum(5d),
            _ => ExpantaNum.One
        };
    }

    private void SetAmountAndRates(BuildingState state, ExpantaNum newAmount)
    {
        ApplyRateDelta(state, state.Amount, state.Efficiency, newAmount, state.Efficiency);
        state.SetAmount(newAmount);
    }

    private static void ApplyRateDelta(
        BuildingState state,
        ExpantaNum oldAmount,
        ExpantaNum oldEfficiency,
        ExpantaNum newAmount,
        ExpantaNum newEfficiency)
    {
        ExpantaNum oldScale = oldAmount * oldEfficiency;
        ExpantaNum newScale = newAmount * newEfficiency;
        ExpantaNum scaleDelta = newScale - oldScale;

        IReadOnlyList<Pair<Resource, ExpantaNum>> generation = state.Definition.ResourceGenerationRates;
        for (int i = 0; i < generation.Count; i++)
            ResourceManager.Instance.AdjustProductionRate(generation[i].First, scaleDelta * generation[i].Second);

        IReadOnlyList<Pair<Resource, ExpantaNum>> consumption = state.Definition.ResourceConsumptionRates;
        for (int i = 0; i < consumption.Count; i++)
            ResourceManager.Instance.AdjustConsumptionRate(consumption[i].First, scaleDelta * consumption[i].Second);

        GameManager.Instance.AdjustFoodRates(
            scaleDelta * state.Definition.FoodProductionRate,
            scaleDelta * state.Definition.FoodConsumptionRate);
        GameManager.Instance.AdjustFoodCapacity(
            scaleDelta * state.Definition.FoodCapacityGranted);
        GameManager.Instance.AdjustPopulationCapacity(
            scaleDelta * state.Definition.PopulationCapacityGranted);
    }

    private static void EnsureBuildingResources(Building building)
    {
        EnsureResources(building.ResourceRequirements);
        EnsureResources(building.ResourceGenerationRates);
        EnsureResources(building.ResourceConsumptionRates);
    }

    private static void EnsureResources(IReadOnlyList<Pair<Resource, ExpantaNum>> pairs)
    {
        for (int i = 0; i < pairs.Count; i++)
            ResourceManager.Instance.EnsureResource(pairs[i].First);
    }

    private void InsertOrdered(BuildingState state)
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

    internal void RecalculateDerivedStateFromBuildings()
    {
        for (int i = 0; i < orderedStates.Count; i++)
        {
            BuildingState state = orderedStates[i];
            if (state.Amount <= ExpantaNum.Zero)
                continue;

            GameManager.Instance.CommitConstruction(
                state.SpaceCost * state.Amount,
                state.ProductivityConsumption * state.Amount,
                state.ProductivityGranted * state.Amount);
            ApplyRateDelta(state, ExpantaNum.Zero, ExpantaNum.One, state.Amount, state.Efficiency);
        }
        RefreshResearchPower();
    }

    internal void ResetForLoad()
    {
        for (int i = 0; i < orderedStates.Count; i++)
            orderedStates[i].ResetForLoad();
        GlobalEfficiencyFactor = ExpantaNum.One;
        RefreshResearchPower();
    }

    internal SaveManager.BuildingSaveData CaptureSaveData()
    {
        var data = new SaveManager.BuildingSaveData
        {
            GlobalEfficiencyFactor = GlobalEfficiencyFactor.ToString(),
            Buildings = new List<SaveManager.BuildingStateSaveData>(states.Count)
        };

        for (int i = 0; i < orderedStates.Count; i++)
        {
            BuildingState state = orderedStates[i];
            data.Buildings.Add(new SaveManager.BuildingStateSaveData
            {
                BuildingId = state.Definition.Id,
                Amount = state.Amount.ToString(),
                AutoBuild = state.AutoBuild,
                AutoBuildProgress = state.AutoBuildProgress.ToString()
            });
        }

        return data;
    }

    internal void RestoreSaveData(SaveManager.BuildingSaveData data)
    {
        if (data == null)
            return;

        if (data.Buildings != null)
        {
            for (int i = 0; i < data.Buildings.Count; i++)
            {
                SaveManager.BuildingStateSaveData saved = data.Buildings[i];
                BuildingState state = EnsureBuilding(DataBase<Building>.Find(saved.BuildingId));
                ExpantaNum amount = Parse(saved.Amount, saved.BuildingId, nameof(saved.Amount));
                ExpantaNum progress = Parse(
                    saved.AutoBuildProgress,
                    saved.BuildingId,
                    nameof(saved.AutoBuildProgress));
                bool autoBuild = saved.AutoBuild && CanAutoBuild(state.Definition);
                state.Restore(
                    amount,
                    autoBuild,
                    autoBuild ? progress : ExpantaNum.Zero);
            }
        }

        GlobalEfficiencyFactor = Parse(
            data.GlobalEfficiencyFactor,
            nameof(BuildingManager),
            nameof(data.GlobalEfficiencyFactor),
            ExpantaNum.One);
        RefreshResearchPower();
    }

    public override void Save() => SaveManager.Instance.SaveNow(true);

    public override void Load() => SaveManager.Instance.LoadOrCreateGame();

    private void RefreshResearchPower()
    {
        ResearchManager researchManager = FindObjectOfType<ResearchManager>();
        researchManager?.RebuildResearchPower(orderedStates);
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
        throw new FormatException($"Invalid ExpantaNum '{raw}' for {owner}.{field}.");
    }

}
