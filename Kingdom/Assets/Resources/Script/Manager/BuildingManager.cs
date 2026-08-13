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
    DeconstructionUnavailable,
    BuildingTierSuperseded,
    UpgradeUnavailable
}

public class BuildingManager : Singleton<BuildingManager>
{
    private const double UpgradeRecoveryRate = 0.8d;
    private const int UpgradeBinarySearchLimit = 256;
    private readonly Dictionary<Building, BuildingState> states = new();
    private readonly List<BuildingState> orderedStates = new();
    // 一个升级目标可以由多个分支汇聚而来，因此这里必须保留全部前置建筑。
    private readonly Dictionary<Building, List<Building>> chainPredecessors = new();
    private readonly HashSet<Building> chainMembers = new();
    private bool chainIndexInitialized;

    public IReadOnlyDictionary<Building, BuildingState> States => states;
    internal IReadOnlyList<BuildingState> OrderedStates => orderedStates;
    public ExpantaNum TotalProductivity =>
        ExpantaNum.Max(ExpantaNum.Zero, CalculateRawTotalProductivity());
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
    public ExpantaNum SafePopulationDepartureAllowance =>
        ExpantaNum.Max(
            ExpantaNum.Zero,
            CalculateRawTotalProductivity() - UsedProductivity).Floor();
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
    public event Action<BuildingState> BuildingStateAdded;

    private ExpantaNum CalculateRawTotalProductivity()
    {
        ExpantaNum total =
            GameManager.Instance.State.Population.Population *
            PopulationState.ProductivityGrantedPerPerson *
            ProgressionModifierManager.Current.PopulationProductivityMultiplier +
            ProgressionModifierManager.Current.ProductivityGranted;
        for (int i = 0; i < orderedStates.Count; i++)
            total += orderedStates[i].Amount * orderedStates[i].ProductivityGranted;
        return total;
    }

    internal void InitializeStartingBuildings()
    {
        IReadOnlyList<Building> definitions = DataBase<Building>.All;
        RebuildBuildingChainIndex(definitions);
        for (int i = 0; i < definitions.Count; i++)
            EnsureBuilding(definitions[i]);
    }

    public static void ValidateBuildingChains(IReadOnlyList<Building> definitions)
    {
        if (definitions == null)
            throw new ArgumentNullException(nameof(definitions));

        var definitionSet = new HashSet<Building>();
        for (int i = 0; i < definitions.Count; i++)
            if (definitions[i] != null)
                definitionSet.Add(definitions[i]);
        for (int i = 0; i < definitions.Count; i++)
        {
            Building source = definitions[i];
            if (source == null || source.UpgradeTo == null)
                continue;
            Building target = source.UpgradeTo;
            if (!definitionSet.Contains(target))
                throw new InvalidOperationException(
                    $"建筑升级链“{source.Id}”指向了未加载的定义。");
            if (source == target)
                throw new InvalidOperationException(
                    $"建筑升级链“{source.Id}”不能升级到自身。");
            ValidateChainEconomy(source);
            ValidateChainEconomy(target);
        }

        var visited = new HashSet<Building>();
        var visiting = new HashSet<Building>();
        for (int i = 0; i < definitions.Count; i++)
            ValidateChainNode(definitions[i], visited, visiting);
    }

    private static void ValidateChainEconomy(Building building)
    {
        if (!building.HasValidCostGrowth)
            throw new InvalidOperationException(
                $"建筑升级链成员“{building.Id}”的成本增长倍率无效。");
        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements =
            building.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = requirements[i];
            if (requirement.First == null)
                throw new InvalidOperationException(
                    $"建筑升级链成员“{building.Id}”的资源引用为空。");
            if (requirement.Second.IsNaN ||
                requirement.Second.IsInfinity ||
                requirement.Second < ExpantaNum.Zero)
            {
                throw new InvalidOperationException(
                $"建筑升级链成员“{building.Id}”的资源成本无效。");
            }
        }
    }

    private static void ValidateChainNode(
        Building building,
        HashSet<Building> visited,
        HashSet<Building> visiting)
    {
        if (building == null || visited.Contains(building))
            return;
        if (!visiting.Add(building))
            throw new InvalidOperationException(
                $"建筑升级链在“{building.Id}”处形成循环依赖。");
        ValidateChainNode(building.UpgradeTo, visited, visiting);
        visiting.Remove(building);
        visited.Add(building);
    }

    private void RebuildBuildingChainIndex(IReadOnlyList<Building> definitions)
    {
        ValidateBuildingChains(definitions);
        chainPredecessors.Clear();
        chainMembers.Clear();
        for (int i = 0; i < definitions.Count; i++)
        {
            Building source = definitions[i];
            if (source == null || source.UpgradeTo == null)
                continue;
            if (!chainPredecessors.TryGetValue(source.UpgradeTo, out List<Building> predecessors))
            {
                predecessors = new List<Building>();
                chainPredecessors.Add(source.UpgradeTo, predecessors);
            }
            predecessors.Add(source);
            chainMembers.Add(source);
            chainMembers.Add(source.UpgradeTo);
        }
        chainIndexInitialized = true;
    }

    private void EnsureBuildingChainIndex()
    {
        if (!chainIndexInitialized)
            RebuildBuildingChainIndex(DataBase<Building>.All);
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
        throw new KeyNotFoundException($"建筑状态“{building.Id}”尚未创建。");
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
        IReadOnlyList<WorkshopUpgrade> requiredUpgrades = building.RequiredWorkshopUpgrades;
        for (int i = 0; i < requiredUpgrades.Count; i++)
        {
            WorkshopUpgrade upgrade = requiredUpgrades[i];
            if (workshop == null || upgrade == null || !workshop.IsPurchased(upgrade))
            {
                failure = BuildFailure.WorkshopPrerequisiteIncomplete;
                return false;
            }
        }

        failure = BuildFailure.None;
        return true;
    }

    public bool IsInBuildingChain(Building building)
    {
        if (building == null)
            return false;
        EnsureBuildingChainIndex();
        return chainMembers.Contains(building);
    }

    public bool CanConstructNew(Building building)
    {
        if (!ArePrerequisitesMet(building, out _))
            return false;

        EnsureBuildingChainIndex();
        if (!chainMembers.Contains(building))
            return true;

        if (!HasUnlockedChainPath(
                building,
                new HashSet<Building>(),
                new HashSet<Building>()))
            return false;

        // 当前建筑的直接升级目标一旦可用，就只显示/建造更高阶目标。
        return building.UpgradeTo == null ||
            !ArePrerequisitesMet(building.UpgradeTo, out _);
    }

    public bool ShouldDisplay(Building building)
    {
        if (building == null)
            return false;
        if (states.TryGetValue(building, out BuildingState state) &&
            state.Amount > ExpantaNum.Zero)
        {
            return true;
        }

        if (!ArePrerequisitesMet(building, out _))
            return false;

        // A zero-count chain tier remains visible until its immediate
        // successor is actually displayed. Use the same display predicate
        // for the successor instead of only checking its prerequisites: an
        // unrelated research must not hide this card, and a non-zero lower
        // tier must remain available for upgrade/deconstruction.
        if (IsInBuildingChain(building) && building.UpgradeTo != null)
            return !ShouldDisplay(building.UpgradeTo);

        return CanConstructNew(building);
    }

    public bool TryGetUnlockedUpgradeTarget(Building source, out Building target)
    {
        target = source?.UpgradeTo;
        if (target == null || !ArePrerequisitesMet(target, out _))
        {
            target = null;
            return false;
        }

        EnsureBuildingChainIndex();
        if (!chainMembers.Contains(source) || !chainMembers.Contains(target))
        {
            target = null;
            return false;
        }

        return HasUnlockedChainPath(
            source,
            new HashSet<Building>(),
            new HashSet<Building>());
    }

    private bool HasUnlockedChainPath(
        Building building,
        HashSet<Building> visiting,
        HashSet<Building> visited)
    {
        if (building == null || !ArePrerequisitesMet(building, out _))
            return false;
        if (visited.Contains(building))
            return false;
        if (!visiting.Add(building))
            return false;
        if (!chainPredecessors.TryGetValue(building, out List<Building> predecessors) ||
            predecessors.Count == 0)
        {
            visiting.Remove(building);
            return true;
        }

        for (int i = 0; i < predecessors.Count; i++)
        {
            if (HasUnlockedChainPath(predecessors[i], visiting, visited))
            {
                visiting.Remove(building);
                return true;
            }
        }

        visiting.Remove(building);
        visited.Add(building);
        return false;
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
        if (!CanConstructNew(building))
        {
            failure = BuildFailure.BuildingTierSuperseded;
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

        ExpantaNum requiredProductivity = state.ProductivityConsumption * amount;
        if (AvailableProductivity < requiredProductivity)
        {
            failure = BuildFailure.ProductivityInsufficient;
            return false;
        }

        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = building.ResourceRequirements;
        var costs = new Dictionary<Resource, ExpantaNum>();
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = requirements[i];
            if (pair.First == null)
            {
                failure = BuildFailure.ResourceInsufficient;
                return false;
            }
            ExpantaNum totalCost = pair.Second.GeometricSeriesCost(
                building.CostGrowth,
                state.Amount,
                amount);
            totalCost *= GetConstructionCostMultiplier(building);
            if (totalCost.IsNaN || totalCost < ExpantaNum.Zero)
            {
                failure = BuildFailure.ResourceInsufficient;
                return false;
            }
            costs[pair.First] = costs.TryGetValue(pair.First, out ExpantaNum current)
                ? current + totalCost
                : totalCost;
        }

        ExpantaNum previousFoodAmount = GameManager.Instance.State.FoodAmount;
        ExpantaNum previousFoodCapacity = GameManager.Instance.State.FoodCapacity;
        ExpantaNum previousTerritoryUsed = GameManager.Instance.State.TerritoryUsed;
        ExpantaNum previousPopulationCapacity =
            GameManager.Instance.State.Population.PopulationCapacity;
        ExpantaNum previousPopulationProgress =
            GameManager.Instance.State.Population.PopulationChangeProgress;
        ExpantaNum previousAmount = state.Amount;

        bool paid = ResourceManager.Instance.TryApplyAtomicPayment(
            costs,
            () =>
            {
                GameManager.Instance.CommitConstruction(requiredSpace);
                SetAmountAndRates(state, state.Amount + amount);
                RefreshResearchPower();
            },
            () =>
            {
                GameManager.Instance.RefundConstruction(requiredSpace);
                SetAmountAndRates(state, previousAmount);
                RefreshResearchPower();
                GameManager.Instance.State.RestoreTerritoryUsed(previousTerritoryUsed);
                GameManager.Instance.State.RestorePopulationCapacityExact(
                    previousPopulationCapacity,
                    previousPopulationProgress);
                GameManager.Instance.State.RestoreFoodExact(
                    previousFoodAmount,
                    previousFoodCapacity);
            });
        if (!paid)
        {
            failure = BuildFailure.ResourceInsufficient;
            return false;
        }

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
        var resourceChanges = new Dictionary<Resource, ExpantaNum>();
        ExpantaNum previousFoodAmount = GameManager.Instance.State.FoodAmount;
        ExpantaNum previousFoodCapacity = GameManager.Instance.State.FoodCapacity;
        ExpantaNum previousTerritoryUsed = GameManager.Instance.State.TerritoryUsed;
        ExpantaNum previousPopulationCapacity =
            GameManager.Instance.State.Population.PopulationCapacity;
        ExpantaNum previousPopulationProgress =
            GameManager.Instance.State.Population.PopulationChangeProgress;
        ExpantaNum previousAmount = state.Amount;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = requirements[i];
            ExpantaNum refund = pair.Second.GeometricSeriesCost(
                building.CostGrowth,
                state.Amount - amount,
                amount);
            refund *= GetConstructionCostMultiplier(building);
            ExpantaNum delta = refund * ProgressionModifierManager.Current.DeconstructionReturnRate;
            resourceChanges[pair.First] = resourceChanges.TryGetValue(pair.First, out ExpantaNum current)
                ? current + delta
                : delta;
        }

        if (!ResourceManager.Instance.TryApplyAtomicChanges(
                resourceChanges,
                () =>
                {
                    GameManager.Instance.RefundConstruction(state.SpaceCost * amount);
                    SetAmountAndRates(state, state.Amount - amount);
                    RefreshResearchPower();
                },
                () =>
                {
                    GameManager.Instance.CommitConstruction(state.SpaceCost * amount);
                    SetAmountAndRates(state, previousAmount);
                    RefreshResearchPower();
                    GameManager.Instance.State.RestoreTerritoryUsed(previousTerritoryUsed);
                    GameManager.Instance.State.RestorePopulationCapacityExact(
                        previousPopulationCapacity,
                        previousPopulationProgress);
                    GameManager.Instance.State.RestoreFoodExact(
                        previousFoodAmount,
                        previousFoodCapacity);
                }))
        {
            failure = BuildFailure.DeconstructionUnavailable;
            return false;
        }
        failure = BuildFailure.None;
        return true;
    }

    public ExpantaNum GetMaxBuildable(Building building, ExpantaNum requestedMaximum)
    {
        if (!CanConstructNew(building))
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
                    pair.Second * GetConstructionCostMultiplier(building),
                    building.CostGrowth,
                    state.Amount));
        }

        return ExpantaNum.Max(ExpantaNum.Zero, result);
    }

    public void GetUpgradeResourceDeltas(
        Building source,
        ExpantaNum requestedAmount,
        List<Pair<Resource, ExpantaNum>> destination)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        destination.Clear();
        if (source == null ||
            !states.TryGetValue(source, out BuildingState sourceState) ||
            !TryGetUnlockedUpgradeTarget(source, out Building target))
        {
            return;
        }

        BuildingState targetState = EnsureBuilding(target);
        ExpantaNum amount = BuildingTransactionRules.ClampToAvailable(
            requestedAmount.Floor(),
            sourceState.Amount);
        if (amount < ExpantaNum.One)
            return;

        AddUpgradeResources(source.ResourceRequirements, destination);
        AddUpgradeResources(target.ResourceRequirements, destination);
        for (int i = 0; i < destination.Count; i++)
        {
            Resource resource = destination[i].First;
            ExpantaNum sourceBaseCost = FindBaseCost(source.ResourceRequirements, resource);
            ExpantaNum targetBaseCost = FindBaseCost(target.ResourceRequirements, resource);
            ExpantaNum sourceCost = sourceBaseCost.GeometricSeriesCost(
                source.CostGrowth,
                sourceState.Amount - amount,
                amount);
            ExpantaNum targetCost = targetBaseCost.GeometricSeriesCost(
                target.CostGrowth,
                targetState.Amount,
                amount);
            sourceCost *= GetConstructionCostMultiplier(source);
            targetCost *= GetConstructionCostMultiplier(target);
            destination[i] = new Pair<Resource, ExpantaNum>(
                resource,
                targetCost - sourceCost * UpgradeRecoveryRate);
        }
    }

    public bool TryUpgrade(
        Building source,
        ExpantaNum requestedAmount,
        out BuildFailure failure)
    {
        if (source == null ||
            !states.TryGetValue(source, out BuildingState sourceState) ||
            !TryGetUnlockedUpgradeTarget(source, out Building target))
        {
            failure = BuildFailure.UpgradeUnavailable;
            return false;
        }

        ExpantaNum amount = BuildingTransactionRules.ClampToAvailable(
            requestedAmount.Floor(),
            sourceState.Amount);
        if (amount < ExpantaNum.One)
        {
            failure = BuildFailure.InvalidAmount;
            return false;
        }

        BuildingState targetState = EnsureBuilding(target);
        if (!CanApplyUpgradeConstraints(sourceState, targetState, amount, out failure))
            return false;

        var resourceDeltas = new List<Pair<Resource, ExpantaNum>>();
        GetUpgradeResourceDeltas(source, amount, resourceDeltas);
        for (int i = 0; i < resourceDeltas.Count; i++)
        {
            Pair<Resource, ExpantaNum> delta = resourceDeltas[i];
            if (delta.Second.IsNaN ||
                (delta.Second > ExpantaNum.Zero &&
                 ResourceManager.Instance.GetAmount(delta.First) < delta.Second))
            {
                failure = BuildFailure.ResourceInsufficient;
                return false;
            }
        }

        ExpantaNum territoryDelta =
            (targetState.SpaceCost - sourceState.SpaceCost) * amount;
        ExpantaNum previousFoodAmount = GameManager.Instance.State.FoodAmount;
        ExpantaNum previousFoodCapacity = GameManager.Instance.State.FoodCapacity;
        ExpantaNum previousTerritoryUsed = GameManager.Instance.State.TerritoryUsed;
        ExpantaNum previousPopulationCapacity =
            GameManager.Instance.State.Population.PopulationCapacity;
        ExpantaNum previousPopulationProgress =
            GameManager.Instance.State.Population.PopulationChangeProgress;
        ExpantaNum previousSourceAmount = sourceState.Amount;
        ExpantaNum previousTargetAmount = targetState.Amount;
        var resourceChanges = new Dictionary<Resource, ExpantaNum>();
        for (int i = 0; i < resourceDeltas.Count; i++)
        {
            Pair<Resource, ExpantaNum> delta = resourceDeltas[i];
            if (delta.First == null || !delta.Second.IsFinite)
            {
                failure = BuildFailure.ResourceInsufficient;
                return false;
            }
            resourceChanges[delta.First] = resourceChanges.TryGetValue(delta.First, out ExpantaNum current)
                ? current - delta.Second
                : -delta.Second;
        }
        if (!ResourceManager.Instance.TryApplyAtomicChanges(
                resourceChanges,
                () =>
                {
                    if (territoryDelta > ExpantaNum.Zero)
                        GameManager.Instance.CommitConstruction(territoryDelta);
                    else if (territoryDelta < ExpantaNum.Zero)
                        GameManager.Instance.RefundConstruction(-territoryDelta);
                    SetAmountAndRatesCore(sourceState, sourceState.Amount - amount, false);
                    SetAmountAndRatesCore(targetState, targetState.Amount + amount, false);
                    ApplyUpgradeCapacityDelta(sourceState, targetState, amount);
                    RefreshResearchPower();
                },
                () =>
                {
                    if (territoryDelta > ExpantaNum.Zero)
                        GameManager.Instance.RefundConstruction(territoryDelta);
                    else if (territoryDelta < ExpantaNum.Zero)
                        GameManager.Instance.CommitConstruction(-territoryDelta);
                    SetAmountAndRatesCore(sourceState, previousSourceAmount, false);
                    SetAmountAndRatesCore(targetState, previousTargetAmount, false);
                    ApplyUpgradeCapacityDelta(targetState, sourceState, amount);
                    RefreshResearchPower();
                    GameManager.Instance.State.RestoreTerritoryUsed(previousTerritoryUsed);
                    GameManager.Instance.State.RestorePopulationCapacityExact(
                        previousPopulationCapacity,
                        previousPopulationProgress);
                    GameManager.Instance.State.RestoreFoodExact(
                        previousFoodAmount,
                        previousFoodCapacity);
                }))
        {
            failure = BuildFailure.ResourceInsufficient;
            return false;
        }
        failure = BuildFailure.None;
        return true;
    }

    public ExpantaNum GetMaxUpgradeable(Building source, ExpantaNum requestedMaximum)
    {
        if (source == null ||
            !states.TryGetValue(source, out BuildingState sourceState) ||
            !TryGetUnlockedUpgradeTarget(source, out Building target))
        {
            return ExpantaNum.Zero;
        }

        BuildingState targetState = EnsureBuilding(target);
        ExpantaNum high = ExpantaNum.Min(
            sourceState.Amount,
            ExpantaNum.Max(ExpantaNum.Zero, requestedMaximum.Floor()));
        if (high < ExpantaNum.One)
            return ExpantaNum.Zero;
        if (CanAffordUpgrade(sourceState, targetState, high))
            return high;

        ExpantaNum low = ExpantaNum.Zero;
        for (int i = 0; i < UpgradeBinarySearchLimit && high - low > ExpantaNum.One; i++)
        {
            ExpantaNum middle = ((low + high) / 2d).Floor();
            if (middle <= low)
                break;
            if (CanAffordUpgrade(sourceState, targetState, middle))
                low = middle;
            else
                high = middle;
        }
        return low;
    }

    private bool CanAffordUpgrade(
        BuildingState sourceState,
        BuildingState targetState,
        ExpantaNum amount)
    {
        if (!CanApplyUpgradeConstraints(sourceState, targetState, amount, out _))
            return false;
        var deltas = new List<Pair<Resource, ExpantaNum>>();
        GetUpgradeResourceDeltas(sourceState.Definition, amount, deltas);
        for (int i = 0; i < deltas.Count; i++)
        {
            Pair<Resource, ExpantaNum> delta = deltas[i];
            if (delta.Second.IsNaN ||
                (delta.Second > ExpantaNum.Zero &&
                 ResourceManager.Instance.GetAmount(delta.First) < delta.Second))
            {
                return false;
            }
        }
        return true;
    }

    private bool CanApplyUpgradeConstraints(
        BuildingState sourceState,
        BuildingState targetState,
        ExpantaNum amount,
        out BuildFailure failure)
    {
        ExpantaNum territoryDelta =
            (targetState.SpaceCost - sourceState.SpaceCost) * amount;
        if (territoryDelta > GameManager.Instance.State.AvailableTerritory)
        {
            failure = BuildFailure.SpaceInsufficient;
            return false;
        }

        ExpantaNum preMargin = CalculateRawTotalProductivity() - UsedProductivity;
        ExpantaNum postTotalWithoutTargetGrant =
            CalculateRawTotalProductivity() -
            sourceState.ProductivityGranted * amount;
        ExpantaNum postUsed =
            UsedProductivity -
            sourceState.ProductivityConsumption * amount +
            targetState.ProductivityConsumption * amount;
        if (postTotalWithoutTargetGrant - postUsed < ExpantaNum.Min(ExpantaNum.Zero, preMargin))
        {
            failure = BuildFailure.ProductivityInsufficient;
            return false;
        }

        failure = BuildFailure.None;
        return true;
    }

    private static void AddUpgradeResources(
        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements,
        List<Pair<Resource, ExpantaNum>> destination)
    {
        for (int i = 0; i < requirements.Count; i++)
        {
            Resource resource = requirements[i].First;
            bool found = false;
            for (int j = 0; j < destination.Count; j++)
            {
                if (destination[j].First == resource)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
                destination.Add(new Pair<Resource, ExpantaNum>(resource, ExpantaNum.Zero));
        }
    }

    private static ExpantaNum FindBaseCost(
        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements,
        Resource resource)
    {
        for (int i = 0; i < requirements.Count; i++)
            if (requirements[i].First == resource)
                return requirements[i].Second;
        return ExpantaNum.Zero;
    }

    private static ExpantaNum GetConstructionCostMultiplier(Building building)
    {
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        ExpantaNum efficiency = modifiers.GlobalConstructionMultiplier *
            modifiers.GetBuildingConstructionMultiplier(building);
        if (efficiency <= ExpantaNum.Zero || efficiency.IsNaN || efficiency.IsInfinity)
            return ExpantaNum.One;
        return ExpantaNum.One / efficiency;
    }

    private static void ApplyUpgradeCapacityDelta(
        BuildingState sourceState,
        BuildingState targetState,
        ExpantaNum amount)
    {
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        ExpantaNum sourceFoodCapacity =
            sourceState.Efficiency *
            sourceState.Definition.FoodCapacityGranted *
            modifiers.FoodCapacityMultiplier;
        ExpantaNum targetFoodCapacity =
            targetState.Efficiency *
            targetState.Definition.FoodCapacityGranted *
            modifiers.FoodCapacityMultiplier;
        GameManager.Instance.AdjustFoodCapacity(
            (targetFoodCapacity - sourceFoodCapacity) * amount);
        GameManager.Instance.AdjustPopulationCapacity(
            (targetState.Definition.PopulationCapacityGranted -
             sourceState.Definition.PopulationCapacityGranted) * amount);
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
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
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
                ExpantaNum happinessMultiplier =
                    GameManager.Instance.State.HappinessRewardMultiplier;
                ExpantaNum foodProductionMultiplier =
                    modifiers.GetBuildingFoodProductionMultiplier(state.Definition);

                potentialFoodProduction += potentialScale * state.Definition.FoodProductionRate
                    * productionMultiplier * foodProductionMultiplier;
                potentialFoodConsumption += potentialScale * state.Definition.FoodConsumptionRate;
                potentialPowerProduction += actualScale * state.Definition.PowerProductionRate
                    * modifiers.PowerMultiplier
                    * modifiers.GetBuildingPowerProductionMultiplier(state.Definition)
                    * happinessMultiplier;
                potentialPowerConsumption += potentialScale * state.Definition.PowerConsumptionRate;
                potentialLogisticsProduction += actualScale * state.Definition.LogisticsProductionRate
                    * modifiers.GlobalLogisticsMultiplier
                    * modifiers.GetBuildingLogisticsProductionMultiplier(state.Definition)
                    * happinessMultiplier;
                potentialLogisticsConsumption +=
                    potentialScale * state.Definition.LogisticsConsumptionRate;

                IReadOnlyList<Pair<Resource, ExpantaNum>> consumption =
                    state.Definition.ResourceConsumptionRates;
                for (int j = 0; j < consumption.Count; j++)
                    resourceManager.AdjustTickPotentialConsumption(
                        consumption[j].First,
                        (potentialScale - actualScale) * consumption[j].Second);
            }

            GameManager.Instance.PrepareHappiness(
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
        ExpantaNum happinessConstraint =
            GameManager.Instance.State.HappinessConstraintMultiplier;
        return CalculateEffectiveEfficiency(
            GlobalEfficiencyFactor,
            resourceSatisfaction,
            happinessConstraint,
            powerSatisfaction,
            logisticsSatisfaction);
    }

    public static ExpantaNum CalculateEffectiveEfficiency(
        ExpantaNum globalEfficiency,
        ExpantaNum resourceSatisfaction,
        ExpantaNum happinessConstraint)
    {
        return CalculateEffectiveEfficiency(
            globalEfficiency,
            resourceSatisfaction,
            happinessConstraint,
            ExpantaNum.One,
            ExpantaNum.One);
    }

    public static ExpantaNum CalculateEffectiveEfficiency(
        ExpantaNum globalEfficiency,
        ExpantaNum resourceSatisfaction,
        ExpantaNum happinessConstraint,
        ExpantaNum powerSatisfaction,
        ExpantaNum logisticsSatisfaction)
    {
        return ExpantaNum.Clamp01(
            globalEfficiency *
            ExpantaNum.Clamp01(resourceSatisfaction) *
            ExpantaNum.Clamp01(happinessConstraint) *
            ExpantaNum.Clamp01(powerSatisfaction) *
            ExpantaNum.Clamp01(logisticsSatisfaction));
    }

    private void SetAmountAndRates(BuildingState state, ExpantaNum newAmount) =>
        SetAmountAndRatesCore(state, newAmount, true);

    private void SetAmountAndRatesCore(
        BuildingState state,
        ExpantaNum newAmount,
        bool applyCapacityDeltas)
    {
        ApplyRateDelta(
            state,
            state.Amount,
            state.Efficiency,
            newAmount,
            state.Efficiency,
            applyCapacityDeltas);
        state.SetAmount(newAmount);
    }

    private static void ApplyRateDelta(
        BuildingState state,
        ExpantaNum oldAmount,
        ExpantaNum oldEfficiency,
        ExpantaNum newAmount,
        ExpantaNum newEfficiency,
        bool applyCapacityDeltas = true)
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
        if (applyCapacityDeltas)
        {
            GameManager.Instance.AdjustFoodCapacity(
                scaleDelta * state.Definition.FoodCapacityGranted
                    * modifiers.FoodCapacityMultiplier);
            GameManager.Instance.AdjustPopulationCapacity(
                amountDelta * state.Definition.PopulationCapacityGranted);
        }
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
        if (state.Definition.TechLevel >= TechLevel.Spacer)
        {
            GameManager.Instance.AdjustFleetPower(
                scaleDelta * state.Definition.FleetPowerGranted);
            GameManager.Instance.AdjustAttackPower(
                scaleDelta * state.Definition.AttackPowerGranted);
            GameManager.Instance.AdjustDefensePower(
                scaleDelta * state.Definition.DefensePowerGranted);
            GameManager.Instance.AdjustMilitaryManpower(
                scaleDelta * state.Definition.MilitaryManpowerGranted);
        }
    }

    internal void ApplyProgressionModifierChange(
        ProgressionModifierState previous,
        ProgressionModifierState current)
    {
        if (previous == null || current == null)
            return;

        // FoodCapacityMultiplier 同时作用于基础粮食容量和容量建筑，研究或工坊完成时只结算差值。
        GameManager.Instance.AdjustFoodCapacity(
            GameState.BaseFoodCapacity *
            (current.FoodCapacityMultiplier - previous.FoodCapacityMultiplier));

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
        chainIndexInitialized = false;
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
            var restoredBuildings = new HashSet<Building>();
            var restoredAmounts = new Dictionary<Building, ExpantaNum>();
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
                        throw new InvalidOperationException(
                            $"存档包含未知建筑编号“{saved.BuildingId}”。");
                    continue;
                }
                if (!restoredBuildings.Add(definition))
                    throw new InvalidOperationException(
                        $"存档中的建筑状态重复包含“{definition.Id}”。");
                ExpantaNum amount = Parse(saved.Amount, saved.BuildingId, nameof(saved.Amount));
                if (amount < ExpantaNum.Zero)
                    throw new InvalidOperationException(
                        $"Building amount cannot be negative: {definition.Id}");
                restoredAmounts.Add(definition, amount);
            }

            ExpantaNum restoredGlobalEfficiencyFactor = Parse(
                data.GlobalEfficiencyFactor,
                nameof(BuildingManager),
                nameof(data.GlobalEfficiencyFactor),
                ExpantaNum.One);
            if (restoredGlobalEfficiencyFactor < ExpantaNum.Zero)
                throw new InvalidOperationException("Building global efficiency cannot be negative.");
            foreach (KeyValuePair<Building, ExpantaNum> entry in restoredAmounts)
                EnsureBuilding(entry.Key).Restore(entry.Value);
            GlobalEfficiencyFactor = restoredGlobalEfficiencyFactor;
        }
        else
        {
            GlobalEfficiencyFactor = Parse(
                data.GlobalEfficiencyFactor,
                nameof(BuildingManager),
                nameof(data.GlobalEfficiencyFactor),
                ExpantaNum.One);
        }
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
            throw new FormatException($"{owner}.{field} 中的 ExpantaNum 值“{raw}”无效。");
    }

}
