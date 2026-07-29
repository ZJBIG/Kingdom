using System;
using System.Collections.Generic;
using UnityEngine;

public enum BuildFailure
{
    None,
    InvalidAmount,
    TechnologyInsufficient,
    ResearchPrerequisiteIncomplete,
    WorkshopPrerequisiteIncomplete,
    ResourceInsufficient,
    SpaceInsufficient,
    ProductivityInsufficient,
    DeconstructionUnavailable
}

public class BuildingManager : Singleton<BuildingManager>
{
    private const double DeconstructionReturnRate = 0.2d;
    private readonly Dictionary<Building, BuildingState> states = new();
    private readonly List<BuildingState> orderedStates = new();

    public IReadOnlyDictionary<Building, BuildingState> States => states;
    internal IReadOnlyList<BuildingState> OrderedStates => orderedStates;
    public ExpantaNum TotalProductivity
    {
        get
        {
            ExpantaNum total =
                GameManager.Instance.State.Population.Population +
                ProgressionModifierManager.Current.ProductivityGranted;
            for (int i = 0; i < orderedStates.Count; i++)
                total += orderedStates[i].Amount * orderedStates[i].ProductivityGranted;
            return ExpantaNum.Max(ExpantaNum.Zero, total);
        }
    }
    public ExpantaNum UsedProductivity
    {
        get
        {
            ExpantaNum used = ExpantaNum.Zero;
            for (int i = 0; i < orderedStates.Count; i++)
                used += orderedStates[i].Amount * orderedStates[i].ProductivityConsumption;
            return ExpantaNum.Max(ExpantaNum.Zero, used);
        }
    }
    public ExpantaNum AvailableProductivity =>
        ExpantaNum.Max(ExpantaNum.Zero, TotalProductivity - UsedProductivity);
    public ExpantaNum GlobalEfficiencyFactor { get; set; } = ExpantaNum.One;
    public event Action<BuildingState> BuildingStateAdded;

    internal void InitializeStartingBuildings()
    {
        IReadOnlyList<Building> definitions = DataBase<Building>.All;
        for (int i = 0; i < definitions.Count; i++)
            EnsureBuilding(definitions[i]);
    }

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

    public bool ArePrerequisitesMet(Building building, out BuildFailure failure)
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

        IReadOnlyList<Research> requiredResearch = building.RequiredResearch;
        for (int i = 0; i < requiredResearch.Count; i++)
        {
            Research research = requiredResearch[i];
            if (research == null || !ResearchManager.Instance.IsResearchCompleted(research.Id))
            {
                failure = BuildFailure.ResearchPrerequisiteIncomplete;
                return false;
            }
        }

        WorkshopManager workshop = FindObjectOfType<WorkshopManager>();
        IReadOnlyList<WorkshopUpgradeDefinition> requiredUpgrades = building.RequiredWorkshopUpgrades;
        for (int i = 0; i < requiredUpgrades.Count; i++)
        {
            WorkshopUpgradeDefinition upgrade = requiredUpgrades[i];
            if (workshop == null || upgrade == null || !workshop.IsPurchased(upgrade))
            {
                failure = BuildFailure.WorkshopPrerequisiteIncomplete;
                return false;
            }
        }

        failure = BuildFailure.None;
        return true;
    }

    public bool TryBuild(Building building, ExpantaNum requestedAmount, out BuildFailure failure)
    {
        if (building == null)
        {
            failure = BuildFailure.InvalidAmount;
            return false;
        }

        if (!ArePrerequisitesMet(building, out failure))
            return false;

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

        ExpantaNum requiredProductivity = state.ProductivityConsumption * amount;
        if (AvailableProductivity < requiredProductivity)
        {
            failure = BuildFailure.ProductivityInsufficient;
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

        GameManager.Instance.CommitConstruction(requiredSpace);
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

        GameManager.Instance.RefundConstruction(state.SpaceCost * amount);
        SetAmountAndRates(state, state.Amount - amount);
        RefreshResearchPower();
        failure = BuildFailure.None;
        return true;
    }

    public ExpantaNum GetMaxBuildable(Building building, ExpantaNum requestedMaximum)
    {
        if (!ArePrerequisitesMet(building, out _))
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
                (AvailableProductivity / state.ProductivityConsumption).Floor());
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
        RefreshEfficienciesCore();
        RefreshResearchPower();
    }

    private bool RefreshEfficienciesCore()
    {
        bool changed = false;
        for (int i = 0; i < orderedStates.Count; i++)
        {
            BuildingState state = orderedStates[i];
            ExpantaNum efficiency = CalculateEfficiency(state.Definition);
            if (efficiency == state.Efficiency)
                continue;
            ApplyRateDelta(state, state.Amount, state.Efficiency, state.Amount, efficiency);
            state.SetEfficiency(efficiency);
            changed = true;
        }
        return changed;
    }

    internal void PrepareTickResourceSatisfaction(double deltaSeconds)
    {
        if (deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        ResourceManager resourceManager = ResourceManager.Instance;
        ResetEfficienciesForTick();
        int maximumPasses = Math.Max(1, orderedStates.Count + 1);
        for (int pass = 0; pass < maximumPasses; pass++)
        {
            resourceManager.BeginTick();
            ExpantaNum potentialFoodProduction = ExpantaNum.Zero;
            ExpantaNum potentialFoodConsumption = ExpantaNum.Zero;
            ExpantaNum potentialPowerProduction = ExpantaNum.Zero;
            ExpantaNum potentialPowerConsumption = ExpantaNum.Zero;
            ExpantaNum potentialLogisticsProduction = ExpantaNum.Zero;
            ExpantaNum potentialLogisticsConsumption = ExpantaNum.Zero;
            for (int i = 0; i < orderedStates.Count; i++)
            {
                BuildingState state = orderedStates[i];
                ExpantaNum potentialScale =
                    state.Amount * ExpantaNum.Clamp01(GlobalEfficiencyFactor);
                ExpantaNum actualScale = state.Amount * state.Efficiency;
                ProgressionModifierState modifiers = ProgressionModifierManager.Current;
                ExpantaNum productionMultiplier =
                    modifiers.GetBuildingProductionMultiplier(state.Definition) *
                    modifiers.GlobalBuildingProductionMultiplier;
                ExpantaNum foodProductionMultiplier =
                    modifiers.GetBuildingFoodProductionMultiplier(state.Definition);

                potentialFoodProduction += potentialScale * state.Definition.FoodProductionRate
                    * productionMultiplier * foodProductionMultiplier;
                potentialFoodConsumption += potentialScale * state.Definition.FoodConsumptionRate;
                potentialPowerProduction += actualScale * state.Definition.PowerProductionRate
                    * modifiers.PowerMultiplier
                    * modifiers.GetBuildingPowerProductionMultiplier(state.Definition);
                potentialPowerConsumption += potentialScale * state.Definition.PowerConsumptionRate;
                potentialLogisticsProduction += actualScale * state.Definition.LogisticsProductionRate
                    * modifiers.GlobalLogisticsMultiplier
                    * modifiers.GetBuildingLogisticsProductionMultiplier(state.Definition);
                potentialLogisticsConsumption +=
                    potentialScale * state.Definition.LogisticsConsumptionRate;

                IReadOnlyList<Pair<Resource, ExpantaNum>> consumption =
                    state.Definition.ResourceConsumptionRates;
                for (int j = 0; j < consumption.Count; j++)
                    resourceManager.AdjustTickPotentialConsumption(
                        consumption[j].First,
                        (potentialScale - actualScale) * consumption[j].Second);
            }

            GameManager.Instance.PrepareFoodSatisfaction(
                potentialFoodProduction,
                potentialFoodConsumption,
                deltaSeconds);
            GameManager.Instance.PrepareFlowSatisfaction(
                potentialPowerProduction,
                potentialPowerConsumption,
                potentialLogisticsProduction,
                potentialLogisticsConsumption);
            resourceManager.CalculateTickSatisfaction(deltaSeconds);
            if (!RefreshEfficienciesCore())
                break;
        }
        RefreshResearchPower();
    }

    private ExpantaNum CalculateEfficiency(Building building)
    {
        ExpantaNum resourceSatisfaction = ExpantaNum.One;
        IReadOnlyList<Pair<Resource, ExpantaNum>> rates = building.ResourceConsumptionRates;
        for (int i = 0; i < rates.Count; i++)
            resourceSatisfaction *= ResourceManager.Instance.GetTickSatisfaction(rates[i].First);

        ExpantaNum powerSatisfaction = building.PowerConsumptionRate > ExpantaNum.Zero
            ? GameManager.Instance.State.PowerSatisfaction
            : ExpantaNum.One;
        ExpantaNum logisticsSatisfaction = building.LogisticsConsumptionRate > ExpantaNum.Zero
            ? GameManager.Instance.State.LogisticsSatisfaction
            : ExpantaNum.One;
        ExpantaNum foodSatisfaction = CalculateFoodConstraint(
            GameManager.Instance.State.FoodSatisfaction);
        return CalculateEffectiveEfficiency(
            GlobalEfficiencyFactor,
            resourceSatisfaction,
            foodSatisfaction,
            powerSatisfaction,
            logisticsSatisfaction);
    }

    public static ExpantaNum CalculateFoodConstraint(ExpantaNum foodSatisfaction) =>
        ExpantaNum.Clamp01(foodSatisfaction);

    public static ExpantaNum CalculateEffectiveEfficiency(
        ExpantaNum globalEfficiency,
        ExpantaNum resourceSatisfaction,
        ExpantaNum foodSatisfaction)
    {
        return CalculateEffectiveEfficiency(
            globalEfficiency,
            resourceSatisfaction,
            foodSatisfaction,
            ExpantaNum.One,
            ExpantaNum.One);
    }

    public static ExpantaNum CalculateEffectiveEfficiency(
        ExpantaNum globalEfficiency,
        ExpantaNum resourceSatisfaction,
        ExpantaNum foodSatisfaction,
        ExpantaNum powerSatisfaction,
        ExpantaNum logisticsSatisfaction)
    {
        return ExpantaNum.Clamp01(
            globalEfficiency *
            ExpantaNum.Clamp01(resourceSatisfaction) *
            ExpantaNum.Clamp01(foodSatisfaction) *
            ExpantaNum.Clamp01(powerSatisfaction) *
            ExpantaNum.Clamp01(logisticsSatisfaction));
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
        ExpantaNum amountDelta = newAmount - oldAmount;
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        ExpantaNum productionMultiplier =
            modifiers.GetBuildingProductionMultiplier(state.Definition) *
            modifiers.GlobalBuildingProductionMultiplier;

        IReadOnlyList<Pair<Resource, ExpantaNum>> generation = state.Definition.ResourceGenerationRates;
        for (int i = 0; i < generation.Count; i++)
            ResourceManager.Instance.AdjustProductionRate(
                generation[i].First,
                scaleDelta * generation[i].Second
                * productionMultiplier
                * modifiers.GetResourceProductionMultiplier(generation[i].First));

        IReadOnlyList<Pair<Resource, ExpantaNum>> consumption = state.Definition.ResourceConsumptionRates;
        for (int i = 0; i < consumption.Count; i++)
            ResourceManager.Instance.AdjustConsumptionRate(consumption[i].First, scaleDelta * consumption[i].Second);

        GameManager.Instance.AdjustFoodRates(
            scaleDelta * state.Definition.FoodProductionRate
                * productionMultiplier
                * modifiers.GetBuildingFoodProductionMultiplier(state.Definition),
            scaleDelta * state.Definition.FoodConsumptionRate);
        GameManager.Instance.AdjustFoodCapacity(
            scaleDelta * state.Definition.FoodCapacityGranted
                * modifiers.FoodCapacityMultiplier);
        GameManager.Instance.AdjustPopulationCapacity(
            amountDelta * state.Definition.PopulationCapacityGranted);
        GameManager.Instance.AdjustPowerRates(
            scaleDelta * state.Definition.PowerProductionRate
                * modifiers.PowerMultiplier
                * modifiers.GetBuildingPowerProductionMultiplier(state.Definition),
            scaleDelta * state.Definition.PowerConsumptionRate);
        GameManager.Instance.AdjustLogisticsRates(
            scaleDelta * state.Definition.LogisticsProductionRate
                * modifiers.GlobalLogisticsMultiplier
                * modifiers.GetBuildingLogisticsProductionMultiplier(state.Definition),
            scaleDelta * state.Definition.LogisticsConsumptionRate);
        GameManager.Instance.AdjustFleetPower(
            scaleDelta * state.Definition.FleetPowerGranted);
        GameManager.Instance.AdjustAttackPower(
            scaleDelta * state.Definition.AttackPowerGranted);
        GameManager.Instance.AdjustDefensePower(
            scaleDelta * state.Definition.DefensePowerGranted);
        GameManager.Instance.AdjustMilitaryManpower(
            scaleDelta * state.Definition.MilitaryManpowerGranted);
    }

    internal void ApplyProgressionModifierChange(
        ProgressionModifierState previous,
        ProgressionModifierState current)
    {
        if (previous == null || current == null)
            return;

        for (int i = 0; i < orderedStates.Count; i++)
        {
            BuildingState state = orderedStates[i];
            ExpantaNum scale = state.Amount * state.Efficiency;
            if (scale <= ExpantaNum.Zero)
                continue;

            Building building = state.Definition;
            ExpantaNum oldBuildingMultiplier =
                previous.GetBuildingProductionMultiplier(building) *
                previous.GlobalBuildingProductionMultiplier;
            ExpantaNum newBuildingMultiplier =
                current.GetBuildingProductionMultiplier(building) *
                current.GlobalBuildingProductionMultiplier;

            IReadOnlyList<Pair<Resource, ExpantaNum>> generation =
                building.ResourceGenerationRates;
            for (int j = 0; j < generation.Count; j++)
            {
                Pair<Resource, ExpantaNum> rate = generation[j];
                ExpantaNum oldMultiplier =
                    oldBuildingMultiplier * previous.GetResourceProductionMultiplier(rate.First);
                ExpantaNum newMultiplier =
                    newBuildingMultiplier * current.GetResourceProductionMultiplier(rate.First);
                ResourceManager.Instance.AdjustProductionRate(
                    rate.First,
                    scale * rate.Second * (newMultiplier - oldMultiplier));
            }

            ExpantaNum oldFoodMultiplier =
                oldBuildingMultiplier * previous.GetBuildingFoodProductionMultiplier(building);
            ExpantaNum newFoodMultiplier =
                newBuildingMultiplier * current.GetBuildingFoodProductionMultiplier(building);
            GameManager.Instance.AdjustFoodRates(
                scale * building.FoodProductionRate * (newFoodMultiplier - oldFoodMultiplier),
                ExpantaNum.Zero);
            GameManager.Instance.AdjustFoodCapacity(
                scale * building.FoodCapacityGranted
                * (current.FoodCapacityMultiplier - previous.FoodCapacityMultiplier));
            ExpantaNum oldPower =
                previous.PowerMultiplier * previous.GetBuildingPowerProductionMultiplier(building);
            ExpantaNum newPower =
                current.PowerMultiplier * current.GetBuildingPowerProductionMultiplier(building);
            GameManager.Instance.AdjustPowerRates(
                scale * building.PowerProductionRate * (newPower - oldPower),
                ExpantaNum.Zero);
            ExpantaNum oldLogistics =
                previous.GlobalLogisticsMultiplier *
                previous.GetBuildingLogisticsProductionMultiplier(building);
            ExpantaNum newLogistics =
                current.GlobalLogisticsMultiplier *
                current.GetBuildingLogisticsProductionMultiplier(building);
            GameManager.Instance.AdjustLogisticsRates(
                scale * building.LogisticsProductionRate * (newLogistics - oldLogistics),
                ExpantaNum.Zero);
        }

        GameManager.Instance.AdjustTerritoryTotal(
            current.TerritoryGranted - previous.TerritoryGranted);
        RefreshResearchPower();
    }

    private void ResetEfficienciesForTick()
    {
        ExpantaNum startingEfficiency = ExpantaNum.Clamp01(GlobalEfficiencyFactor);
        for (int i = 0; i < orderedStates.Count; i++)
        {
            BuildingState state = orderedStates[i];
            if (state.Efficiency == startingEfficiency)
                continue;
            ApplyRateDelta(
                state,
                state.Amount,
                state.Efficiency,
                state.Amount,
                startingEfficiency);
            state.SetEfficiency(startingEfficiency);
        }
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

            GameManager.Instance.CommitConstruction(state.SpaceCost * state.Amount);
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
                string buildingId =
                    RetiredDefinitionMigration.NormalizeBuildingId(saved.BuildingId);
                if (!DataBase<Building>.TryFind(buildingId, out Building definition))
                {
                    if (RetiredDefinitionMigration.IsRetired(saved.BuildingId))
                        RetiredDefinitionMigration.LogOnce();
                    else
                        Debug.LogWarning($"Ignoring unknown building '{saved.BuildingId}' while loading.");
                    continue;
                }
                BuildingState state = EnsureBuilding(definition);
                ExpantaNum amount = Parse(saved.Amount, saved.BuildingId, nameof(saved.Amount));
                state.Restore(amount);
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
