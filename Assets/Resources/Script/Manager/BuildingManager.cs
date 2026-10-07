using System;
using System.Collections.Generic;

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
    UpgradeUnavailable,
    SectorNotOccupied,
    BuildingLimitReached
}

public class BuildingManager : Singleton<BuildingManager>
{
    private const double BuildBoundaryTolerance = 1e-8d;

    private static bool ExceedsBuildBoundary(
        ExpantaNum required,
        ExpantaNum available,
        out ExpantaNum effectiveRequired)
    {
        effectiveRequired = required;
        if (!required.IsFinite)
            return true;
        if (available.IsNaN)
            return true;
        if (available.IsInfinity)
            return false;
        if (required <= available)
            return false;

        if (required.ApproximatelyEquals(available, BuildBoundaryTolerance))
        {
            effectiveRequired = available;
            return false;
        }

        return true;
    }

    private static bool IsBuildResourceCostWithinBoundary(
        Building building,
        BuildingState state,
        ExpantaNum amount)
    {
        var costs = new Dictionary<Resource, ExpantaNum>();
        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = building.ResourceRequirements;
        ExpantaNum multiplier = GetConstructionCostMultiplier(building);
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = requirements[i];
            ExpantaNum cost = pair.Second.GeometricSeriesCost(
                building.CostGrowth,
                state.Amount,
                amount) * multiplier;
            if (pair.First == null || cost.IsNaN || cost < ExpantaNum.Zero)
                return false;
            costs[pair.First] = costs.TryGetValue(pair.First, out ExpantaNum current)
                ? current + cost
                : cost;
        }

        foreach (KeyValuePair<Resource, ExpantaNum> entry in costs)
        {
            if (ExceedsBuildBoundary(
                    entry.Value,
                    ResourceManager.Instance.GetAmount(entry.Key),
                    out _))
                return false;
        }
        return true;
    }
    private struct UpgradeProductivitySnapshot
    {
        public bool Initialized;
        public ExpantaNum RawTotal;
        public ExpantaNum Used;
    }

    private const double UpgradeRecoveryRate = 0.8d;
    private const int UpgradeBinarySearchLimit = 256;
    private readonly Dictionary<Building, BuildingState> states = new();
    private readonly List<BuildingState> orderedStates = new();
    private readonly List<BuildingState> activeBuildingStates = new();
    private readonly HashSet<BuildingState> activeBuildingStateSet = new();
    private readonly List<Pair<Resource, ExpantaNum>> upgradeAffordabilityDeltas = new();
    // 一个升级目标可以由多个分支汇聚而来，因此这里必须保留全部前置建筑。
    private readonly Dictionary<Building, List<Building>> chainPredecessors = new();
    private readonly HashSet<Building> chainMembers = new();
    private readonly HashSet<Building> chainRoots = new();
    private readonly Dictionary<SectorDefinition, List<SectorBuilding>> sectorBuildingsBySector = new();
    private bool chainIndexInitialized;
    // FindObjectOfType scans the whole scene and is far too expensive to run
    // on every prerequisite check; ArePrerequisitesMet is called from UI
    // refresh paths dozens of times per frame.
    private WorkshopManager cachedWorkshopManager;
    private ResearchManager cachedResearchManager;
#if UNITY_EDITOR
    private float efficiencyConvergenceLogCooldown;
#endif
    internal int LastEfficiencyPassCount { get; private set; }
    internal int LastActiveBuildingCount { get; private set; }
    internal int ResearchPowerRebuildCount { get; private set; }

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
        TotalProductivity - UsedProductivity;
    public ExpantaNum SafePopulationDepartureAllowance =>
        ExpantaNum.Max(ExpantaNum.Zero, CalculateRawTotalProductivity() - UsedProductivity).Floor();
    private ExpantaNum globalEfficiencyFactor = ExpantaNum.One;
    private bool researchPowerDirty = true;
    public ExpantaNum GlobalEfficiencyFactor
    {
        get => globalEfficiencyFactor;
        set
        {
            if (!value.IsFinite || value < ExpantaNum.Zero)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (globalEfficiencyFactor == value)
                return;
            globalEfficiencyFactor = value;
            researchPowerDirty = true;
        }
    }
    public event Action<BuildingState> BuildingStateAdded;

    private ExpantaNum CalculateRawTotalProductivity()
    {
        GameState gameState = GameManager.Instance.State;
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        ExpantaNum total =
            gameState.Population.Population *
            PopulationState.ProductivityGrantedPerPerson *
            modifiers.PopulationProductivityMultiplier +
            modifiers.ProductivityGranted;
        for (int i = 0; i < orderedStates.Count; i++)
            total += orderedStates[i].Amount * orderedStates[i].ProductivityGranted;
        return total;
    }

    internal void InitializeStartingBuildings()
    {
        IReadOnlyList<Building> definitions = DataBase<Building>.All;
        RebuildBuildingChainIndex(GetBuildingDefinitionsForIndex(definitions));
        for (int i = 0; i < definitions.Count; i++)
            EnsureBuilding(definitions[i]);
    }

    private static IReadOnlyList<Building> GetBuildingDefinitionsForIndex(
        IReadOnlyList<Building> definitions)
    {
        List<Building> indexedDefinitions = new(definitions.Count + DataBase<SectorBuilding>.All.Count);
        for (int i = 0; i < definitions.Count; i++)
            if (definitions[i] != null)
                indexedDefinitions.Add(definitions[i]);

        IReadOnlyList<SectorBuilding> sectorBuildings = DataBase<SectorBuilding>.All;
        for (int i = 0; i < sectorBuildings.Count; i++)
        {
            SectorBuilding sectorBuilding = sectorBuildings[i];
            if (sectorBuilding != null && !indexedDefinitions.Contains(sectorBuilding))
                indexedDefinitions.Add(sectorBuilding);
        }
        return indexedDefinitions;
    }

    public static void ValidateBuildingChains(IReadOnlyList<Building> definitions)
    {
        if (definitions == null)
            throw new ArgumentNullException(nameof(definitions));

        ValidateMergedResourceFlows(definitions);
        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] is SectorBuilding sectorBuilding)
                SectorBuilding.Validate(sectorBuilding);
        }

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
            if (target is SectorBuilding)
                throw new InvalidOperationException(
                    $"Building '{source.Id}' cannot upgrade to sector building '{target.Id}'.");
        }

        var visited = new HashSet<Building>();
        var visiting = new HashSet<Building>();
        for (int i = 0; i < definitions.Count; i++)
            ValidateChainNode(definitions[i], visited, visiting);
    }

    public static void ValidateMergedResourceFlows(IReadOnlyList<Building> definitions)
    {
        if (definitions == null)
            throw new ArgumentNullException(nameof(definitions));

        for (int i = 0; i < definitions.Count; i++)
        {
            definitions[i]?.ValidateResourceFlowDefinitions();
        }
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
                throw new InvalidOperationException($"建筑升级链成员“{building.Id}”的资源成本无效。");
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

#if UNITY_EDITOR
    public void RebuildBuildingChainIndexForEditor(IReadOnlyList<Building> definitions) =>
        RebuildBuildingChainIndex(definitions);
    public int GetChainPredecessorCountForEditor(Building target) =>
        chainPredecessors.TryGetValue(target, out List<Building> predecessors)
            ? predecessors.Count
            : 0;
    public static ExpantaNum GetConstructionCostMultiplierForEditor(Building building) =>
        GetConstructionCostMultiplier(building);
    public void PrepareTickResourceSatisfactionForEditor(double deltaSeconds) =>
        PrepareTickResourceSatisfaction(deltaSeconds);
    public static void ApplyRateDeltaForEditor(
        BuildingState state,
        ExpantaNum oldAmount,
        ExpantaNum oldEfficiency,
        ExpantaNum newAmount,
        ExpantaNum newEfficiency,
        bool applyCapacityDeltas = true) =>
        ApplyRateDelta(
            state, oldAmount, oldEfficiency, newAmount, newEfficiency, applyCapacityDeltas);
    public void ApplyProgressionModifierChangeForEditor(
        ProgressionModifierState previous,
        ProgressionModifierState current) =>
        ApplyProgressionModifierChange(previous, current);
    public void SetAmountAndRatesForEditor(BuildingState state, ExpantaNum newAmount) =>
        SetAmountAndRates(state, newAmount);
#endif

    private void RebuildBuildingChainIndex(IReadOnlyList<Building> definitions)
    {
        ValidateBuildingChains(definitions);
        chainPredecessors.Clear();
        chainMembers.Clear();
        chainRoots.Clear();
        sectorBuildingsBySector.Clear();
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
        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] is not SectorBuilding sectorBuilding)
                continue;
            if (!sectorBuildingsBySector.TryGetValue(
                    sectorBuilding.Sector,
                    out List<SectorBuilding> buildings))
            {
                buildings = new List<SectorBuilding>();
                sectorBuildingsBySector.Add(sectorBuilding.Sector, buildings);
            }
            buildings.Add(sectorBuilding);
        }
        foreach (Building member in chainMembers)
            if (!chainPredecessors.ContainsKey(member))
                chainRoots.Add(member);
        chainIndexInitialized = true;
    }

    private void EnsureBuildingChainIndex()
    {
        if (!chainIndexInitialized)
            RebuildBuildingChainIndex(
                GetBuildingDefinitionsForIndex(DataBase<Building>.All));
    }

    public BuildingState EnsureBuilding(Building building)
    {
        if (building == null)
            throw new ArgumentNullException(nameof(building));
        if (TryGetStateByStableId(building, out BuildingState existing))
            return existing;

        var state = new BuildingState(building);
        states.Add(building, state);
        InsertOrdered(state);

        EnsureBuildingResources(building);
        BuildingStateAdded?.Invoke(state);
        return state;
    }

    public BuildingState GetState(Building building)
    {
        if (building == null)
            throw new ArgumentNullException(nameof(building));
        if (TryGetStateByStableId(building, out BuildingState state))
            return state;
        throw new KeyNotFoundException($"建筑状态“{building.Id}”尚未创建。");
    }

    private bool TryGetStateByStableId(Building building, out BuildingState state)
    {
        state = null;
        if (building == null)
            return false;
        if (states.TryGetValue(building, out state))
            return true;
        foreach (KeyValuePair<Building, BuildingState> entry in states)
        {
            if (entry.Key != null && string.Equals(
                    entry.Key.Id == null ? string.Empty : entry.Key.Id.Trim(),
                    building.Id == null ? string.Empty : building.Id.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                state = entry.Value;
                return state != null;
            }
        }
        return false;
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

        if (building is SectorBuilding sectorBuilding)
        {
            SectorState sectorState = GameManager.Instance.Sectors.GetState(sectorBuilding.Sector);
            if (!sectorState.Occupied)
            {
                failure = BuildFailure.SectorNotOccupied;
                return false;
            }
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

        WorkshopManager workshop = cachedWorkshopManager;
        if (workshop == null)
            workshop = cachedWorkshopManager = FindObjectOfType<WorkshopManager>();
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

    public bool CanConstructNew(Building building)
    {
        return ArePrerequisitesMet(building, out _) &&
            IsHighestUnlockedChainTier(building);
    }

    public IReadOnlyList<SectorBuilding> GetSectorBuildings(SectorDefinition sector)
    {
        EnsureBuildingChainIndex();
        if (sector == null)
            return Array.Empty<SectorBuilding>();
        if (sectorBuildingsBySector.TryGetValue(sector, out List<SectorBuilding> buildings) &&
            buildings.Count > 0)
            return buildings;

        // UI callers may hold an equivalent definition instance after a scene
        // reload. Resolve the authored stable ID instead of returning an empty
        // menu solely because the object reference changed.
        string sectorId = sector.Id == null ? string.Empty : sector.Id.Trim();
        if (sectorId.Length == 0)
            return Array.Empty<SectorBuilding>();
        foreach (KeyValuePair<SectorDefinition, List<SectorBuilding>> entry in sectorBuildingsBySector)
        {
            SectorDefinition indexed = entry.Key;
            if (indexed != null && string.Equals(indexed.Id, sectorId, StringComparison.OrdinalIgnoreCase) &&
                entry.Value != null && entry.Value.Count > 0)
                return entry.Value;
        }
        IReadOnlyList<SectorBuilding> allSectorBuildings = DataBase<SectorBuilding>.All;
        if (allSectorBuildings.Count > 0)
        {
            List<SectorBuilding> matches = new();
            for (int i = 0; i < allSectorBuildings.Count; i++)
            {
                SectorBuilding building = allSectorBuildings[i];
                SectorDefinition buildingSector = building == null ? null : building.Sector;
                if (buildingSector != null &&
                    string.Equals(buildingSector.Id, sectorId, StringComparison.OrdinalIgnoreCase))
                    matches.Add(building);
            }
            if (matches.Count > 0)
                return matches;
        }

        // The base Building database is the authoritative merged catalog in
        // some editor/runtime initialization orders. Include its derived
        // SectorBuilding entries before declaring the menu empty.
        IReadOnlyList<Building> allBuildings = DataBase<Building>.All;
        List<SectorBuilding> baseMatches = new();
        for (int i = 0; i < allBuildings.Count; i++)
        {
            if (allBuildings[i] is not SectorBuilding building || building.Sector == null)
                continue;
            if (string.Equals(building.Sector.Id, sectorId, StringComparison.OrdinalIgnoreCase))
                baseMatches.Add(building);
        }
        if (baseMatches.Count > 0)
            return baseMatches;
        return Array.Empty<SectorBuilding>();
    }

    private bool IsHighestUnlockedChainTier(Building building)
    {
        EnsureBuildingChainIndex();
        if (!chainMembers.Contains(building))
            return true;

        return FindHighestUnlockedTier(building) == building;
    }

    private Building FindHighestUnlockedTier(Building building)
    {
        Building highest = building;
        while (highest.UpgradeTo != null &&
               ArePrerequisitesMet(highest.UpgradeTo, out _))
            highest = highest.UpgradeTo;
        return highest;
    }

    public void RefreshBuildingChainAvailability()
    {
        EnsureBuildingChainIndex();
        foreach (Building root in chainRoots)
        {
            RemoveZeroIntermediateStates(root);
        }
    }

    private void RemoveZeroIntermediateStates(Building root)
    {
        if (!ArePrerequisitesMet(root, out _))
            return;

        Building highest = FindHighestUnlockedTier(root);
        if (highest == root)
            return;

        var lowerTiers = new List<Building>();
        Building current = root;
        while (current != null && current != highest)
        {
            lowerTiers.Add(current);
            current = current.UpgradeTo;
        }

        for (int i = 0; i < lowerTiers.Count; i++)
            RemoveZeroBuildingState(lowerTiers[i]);
    }

    private void RemoveZeroBuildingState(Building building)
    {
        if (!TryGetStateByStableId(building, out BuildingState state) ||
            state.Amount > ExpantaNum.Zero)
            return;
        states.Remove(state.Definition);
        orderedStates.Remove(state);
    }

    public bool ShouldDisplay(Building building)
    {
        if (building == null)
            return false;

        if (TryGetStateByStableId(building, out BuildingState state) && state.Amount > ExpantaNum.Zero)
            return true;

        if (!ArePrerequisitesMet(building, out _))
            return false;

        return CanConstructNew(building);

    }

    public bool TryGetUnlockedUpgradeTarget(Building source, out Building target)
    {
        target = null;
        if (source == null ||
            !TryGetStateByStableId(source, out BuildingState sourceState) ||
            sourceState.Amount < ExpantaNum.One)
        {
            return false;
        }

        EnsureBuildingChainIndex();
        if (!chainMembers.Contains(source))
        {
            return false;
        }

        Building candidate = source.UpgradeTo;
        while (candidate != null)
        {
            if (!ArePrerequisitesMet(candidate, out _))
                break;
            target = candidate;
            candidate = candidate.UpgradeTo;
        }

        return target != null;
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
        if (!amount.IsFinite || amount < ExpantaNum.One)
        {
            failure = BuildFailure.InvalidAmount;
            return false;
        }

        SectorBuilding sectorBuilding = building as SectorBuilding;
        bool usesTerritory = sectorBuilding == null;
        ExpantaNum requiredSpace = ExpantaNum.Zero;
        if (usesTerritory)
        {
            requiredSpace = state.SpaceCost * amount;
            if (ExceedsBuildBoundary(
                    requiredSpace,
                    GameManager.Instance.State.AvailableTerritory,
                    out requiredSpace))
            {
                failure = BuildFailure.SpaceInsufficient;
                return false;
            }
        }
        else if (state.Amount + amount > new ExpantaNum(sectorBuilding.MaxAmount))
        {
            failure = BuildFailure.BuildingLimitReached;
            return false;
        }

        ExpantaNum requiredProductivity = state.ProductivityConsumption * amount;
        if (ExceedsBuildBoundary(requiredProductivity, AvailableProductivity, out requiredProductivity))
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

        foreach (Resource resource in new List<Resource>(costs.Keys))
        {
            ExpantaNum available = ResourceManager.Instance.GetAmount(resource);
            if (ExceedsBuildBoundary(costs[resource], available, out ExpantaNum effectiveCost))
            {
                failure = BuildFailure.ResourceInsufficient;
                return false;
            }
            costs[resource] = effectiveCost;
        }

        ExpantaNum previousFoodAmount = GameManager.Instance.State.FoodAmount;
        ExpantaNum previousFoodCapacity = GameManager.Instance.State.FoodCapacity;
        ExpantaNum previousTerritoryUsed = usesTerritory
            ? GameManager.Instance.State.TerritoryUsed
            : ExpantaNum.Zero;
        ExpantaNum previousPopulationCapacity =
            GameManager.Instance.State.Population.PopulationCapacity;
        ExpantaNum previousPopulationProgress =
            GameManager.Instance.State.Population.PopulationChangeProgress;
        ExpantaNum previousAmount = state.Amount;

        bool paid = ResourceManager.Instance.TryApplyAtomicPayment(
            costs,
            () =>
            {
                if (usesTerritory)
                    GameManager.Instance.CommitConstruction(requiredSpace);
                SetAmountAndRates(state, state.Amount + amount);
                RefreshResearchPower();
            },
            () =>
            {
                if (usesTerritory)
                    GameManager.Instance.RefundConstruction(requiredSpace);
                SetAmountAndRates(state, previousAmount);
                RefreshResearchPower();
                if (usesTerritory)
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
        if (building == null || !TryGetStateByStableId(building, out BuildingState state))
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
        bool usesTerritory = !(building is SectorBuilding);
        ExpantaNum previousTerritoryUsed = usesTerritory
            ? GameManager.Instance.State.TerritoryUsed
            : ExpantaNum.Zero;
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
                    if (usesTerritory)
                        GameManager.Instance.RefundConstruction(state.SpaceCost * amount);
                    SetAmountAndRates(state, state.Amount - amount);
                    RefreshResearchPower();
                },
                () =>
                {
                    if (usesTerritory)
                        GameManager.Instance.CommitConstruction(state.SpaceCost * amount);
                    SetAmountAndRates(state, previousAmount);
                    RefreshResearchPower();
                    if (usesTerritory)
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

        if (building is SectorBuilding sectorBuilding)
        {
            ExpantaNum remaining = new ExpantaNum(sectorBuilding.MaxAmount) - state.Amount;
            result = ExpantaNum.Min(result, ExpantaNum.Max(ExpantaNum.Zero, remaining));
        }
        else if (state.SpaceCost > ExpantaNum.Zero)
        {
            result = ExpantaNum.Min(
                result,
                (GameManager.Instance.State.AvailableTerritory / state.SpaceCost).Floor());
        }
        if (state.ProductivityConsumption > ExpantaNum.Zero)
        {
            result = ExpantaNum.Min(
                result,
                (AvailableProductivity / state.ProductivityConsumption).Floor());
        }

        IReadOnlyList<Pair<Resource, ExpantaNum>> requirements = building.ResourceRequirements;
        ResourceManager resourceManager = null;
        ExpantaNum constructionCostMultiplier = ExpantaNum.One;
        ExpantaNum costGrowth = ExpantaNum.One;
        bool constructionParametersInitialized = false;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = requirements[i];
            if (pair.Second <= ExpantaNum.Zero)
                continue;
            if (resourceManager == null)
                resourceManager = ResourceManager.Instance;
            ExpantaNum availableAmount = resourceManager.GetAmount(pair.First);
            if (!constructionParametersInitialized)
            {
                constructionCostMultiplier = GetConstructionCostMultiplier(building);
                costGrowth = building.CostGrowth;
                constructionParametersInitialized = true;
            }
            result = ExpantaNum.Min(
                result,
                availableAmount.MaxAffordableGeometricSeries(
                    pair.Second * constructionCostMultiplier,
                    costGrowth,
                    state.Amount));
        }

        for (int i = 0; i < 256 && result >= ExpantaNum.One &&
             !IsBuildResourceCostWithinBoundary(building, state, result); i++)
        {
            ExpantaNum next = (result - ExpantaNum.One).Floor();
            if (next >= result)
                break;
            result = next;
        }

        result = ExpantaNum.Max(ExpantaNum.Zero, result);
        return result.IsFinite ? result : ExpantaNum.Zero;
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
            !TryGetStateByStableId(source, out BuildingState sourceState) ||
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

        IReadOnlyList<Pair<Resource, ExpantaNum>> sourceRequirements = source.ResourceRequirements;
        IReadOnlyList<Pair<Resource, ExpantaNum>> targetRequirements = target.ResourceRequirements;
        AddUpgradeResources(sourceRequirements, destination);
        AddUpgradeResources(targetRequirements, destination);
        ExpantaNum sourceCostMultiplier = ExpantaNum.One;
        ExpantaNum targetCostMultiplier = ExpantaNum.One;
        for (int i = 0; i < destination.Count; i++)
        {
            Resource resource = destination[i].First;
            ExpantaNum sourceBaseCost = FindBaseCost(sourceRequirements, resource);
            ExpantaNum targetBaseCost = FindBaseCost(targetRequirements, resource);
            ExpantaNum sourceCost = sourceBaseCost.GeometricSeriesCost(
                source.CostGrowth,
                sourceState.Amount - amount,
                amount);
            ExpantaNum targetCost = targetBaseCost.GeometricSeriesCost(
                target.CostGrowth,
                targetState.Amount,
                amount);
            if (i == 0)
            {
                sourceCostMultiplier = GetConstructionCostMultiplier(source);
                targetCostMultiplier = GetConstructionCostMultiplier(target);
            }
            sourceCost *= sourceCostMultiplier;
            targetCost *= targetCostMultiplier;
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
            !TryGetStateByStableId(source, out BuildingState sourceState) ||
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
        UpgradeProductivitySnapshot productivitySnapshot = default;
        if (!CanApplyUpgradeConstraints(
                sourceState, targetState, amount, ref productivitySnapshot, out failure))
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
            !TryGetStateByStableId(source, out BuildingState sourceState) ||
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
        UpgradeProductivitySnapshot productivitySnapshot = default;
        ResourceManager resourceManager = null;
        if (CanAffordUpgrade(
                sourceState, targetState, high, ref productivitySnapshot, ref resourceManager))
            return high;

        ExpantaNum low = ExpantaNum.Zero;
        for (int i = 0; i < UpgradeBinarySearchLimit && high - low > ExpantaNum.One; i++)
        {
            ExpantaNum middle = ((low + high) / 2d).Floor();
            if (middle <= low)
                break;
            if (CanAffordUpgrade(
                    sourceState, targetState, middle, ref productivitySnapshot, ref resourceManager))
                low = middle;
            else
                high = middle;
        }
        return low;
    }

    private bool CanAffordUpgrade(
        BuildingState sourceState,
        BuildingState targetState,
        ExpantaNum amount,
        ref UpgradeProductivitySnapshot productivitySnapshot,
        ref ResourceManager resourceManager)
    {
        if (!CanApplyUpgradeConstraints(
                sourceState, targetState, amount, ref productivitySnapshot, out _))
            return false;
        List<Pair<Resource, ExpantaNum>> deltas = upgradeAffordabilityDeltas;
        GetUpgradeResourceDeltas(sourceState.Definition, amount, deltas);
        for (int i = 0; i < deltas.Count; i++)
        {
            Pair<Resource, ExpantaNum> delta = deltas[i];
            if (delta.Second.IsNaN)
                return false;
            if (delta.Second > ExpantaNum.Zero)
            {
                if (resourceManager == null)
                    resourceManager = ResourceManager.Instance;
                if (resourceManager.GetAmount(delta.First) < delta.Second)
                    return false;
            }
        }
        return true;
    }

    private bool CanApplyUpgradeConstraints(
        BuildingState sourceState,
        BuildingState targetState,
        ExpantaNum amount,
        ref UpgradeProductivitySnapshot productivitySnapshot,
        out BuildFailure failure)
    {
        ExpantaNum territoryDelta =
            (targetState.SpaceCost - sourceState.SpaceCost) * amount;
        if (territoryDelta > GameManager.Instance.State.AvailableTerritory)
        {
            failure = BuildFailure.SpaceInsufficient;
            return false;
        }

        if (!productivitySnapshot.Initialized)
        {
            productivitySnapshot.RawTotal = CalculateRawTotalProductivity();
            productivitySnapshot.Used = UsedProductivity;
            productivitySnapshot.Initialized = true;
        }
        ExpantaNum preMargin = productivitySnapshot.RawTotal - productivitySnapshot.Used;
        ExpantaNum postTotalWithoutTargetGrant =
            productivitySnapshot.RawTotal -
            sourceState.ProductivityGranted * amount;
        ExpantaNum postUsed =
            productivitySnapshot.Used -
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

    internal static ExpantaNum GetConstructionCostMultiplier(Building building)
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

    private bool RefreshEfficienciesCore(
        ResourceManager resourceManager = null,
        GameState gameState = null)
    {
        bool changed = false;
        for (int i = 0; i < orderedStates.Count; i++)
        {
            BuildingState state = orderedStates[i];
            // An unbuilt definition contributes no rates and cannot affect
            // any satisfaction result. Avoid evaluating every modifier and
            // resource list for the large inactive tail of the definition
            // list on every convergence pass.
            if (state.Amount <= ExpantaNum.Zero)
                continue;
            if (resourceManager == null)
                resourceManager = ResourceManager.Instance;
            if (gameState == null)
                gameState = GameManager.Instance.State;
            ExpantaNum efficiency = CalculateEfficiency(
                state.Definition, resourceManager, gameState);
            if (efficiency == state.Efficiency)
                continue;
            ApplyRateDelta(state, state.Amount, state.Efficiency, state.Amount, efficiency);
            state.SetEfficiency(efficiency);
            researchPowerDirty = true;
            changed = true;
        }
        return changed;
    }

    internal void PrepareTickResourceSatisfaction(double deltaSeconds)
    {
        if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        ResourceManager resourceManager = ResourceManager.Instance;
        GameManager gameManager = null;
        GameState gameState = null;
        ResetEfficienciesForTick();
        int activeBuildingCount = activeBuildingStates.Count;
        // Zero-amount definitions are skipped by every convergence pass and
        // cannot add another dependency step. Do not let the inactive tail
        // inflate the worst-case pass count on every simulation tick.
        int maximumPasses = Math.Max(1, activeBuildingCount + 1);
        int passesUsed = 0;
        bool converged = false;
        for (int pass = 0; pass < maximumPasses; pass++)
        {
            passesUsed = pass + 1;
            resourceManager.BeginTick();
            ExpantaNum potentialFoodProduction = ExpantaNum.Zero;
            ExpantaNum potentialFoodConsumption = ExpantaNum.Zero;
            ExpantaNum potentialPowerProduction = ExpantaNum.Zero;
            ExpantaNum potentialPowerConsumption = ExpantaNum.Zero;
            ExpantaNum potentialLogisticsProduction = ExpantaNum.Zero;
            ExpantaNum potentialLogisticsConsumption = ExpantaNum.Zero;
            ProgressionModifierState modifiers = ProgressionModifierManager.Current;
            ExpantaNum potentialEfficiencyScale = ExpantaNum.Clamp01(GlobalEfficiencyFactor);
            if (gameManager == null)
            {
                gameManager = GameManager.Instance;
                gameState = gameManager.State;
            }
            // Ultra engineering is a runtime load, not a second resource
            // factory. Include its active power/logistics draw in the same
            // flow convergence used by authored buildings.
            potentialPowerConsumption += gameManager.UltraProject.GetCurrentPowerConsumptionRate();
            potentialLogisticsConsumption += gameManager.UltraProject.GetCurrentLogisticsConsumptionRate();
            if (gameManager.UltraProject.State.Status == UltraProjectStatus.Running &&
                ResearchManager.TryGetInstance(out _))
            {
                UltraProjectPreview ultraPreview = gameManager.UltraProject.GetPreview();
                potentialFoodConsumption += ultraPreview.FoodPerSecond;
                IReadOnlyList<Pair<Resource, ExpantaNum>> ultraCosts =
                    ultraPreview.ContinuousCosts;
                for (int j = 0; j < ultraCosts.Count; j++)
                {
                    Pair<Resource, ExpantaNum> cost = ultraCosts[j];
                    if (cost.First != null && cost.Second > ExpantaNum.Zero)
                        resourceManager.AdjustTickPotentialConsumption(
                            cost.First,
                            cost.Second);
                }
            }
            gameManager.Sectors.AccumulateOccupiedResourcePotential(resourceManager);
            ExpantaNum happinessMultiplier = gameState.HappinessRewardMultiplier;
            for (int i = 0; i < activeBuildingStates.Count; i++)
            {
                BuildingState state = activeBuildingStates[i];
                ExpantaNum potentialScale =
                    state.Amount * potentialEfficiencyScale;
                ExpantaNum actualScale = state.Amount * state.Efficiency;
                ExpantaNum productionMultiplier =
                    modifiers.GetBuildingProductionMultiplier(state.Definition) *
                    modifiers.GlobalBuildingProductionMultiplier;
                potentialFoodProduction += potentialScale * state.Definition.FoodProductionRate
                    * productionMultiplier;
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
                        (potentialScale - actualScale) * consumption[j].Second
                        * productionMultiplier);
            }

            gameManager.PrepareHappiness(
                potentialFoodProduction,
                potentialFoodConsumption,
                deltaSeconds);
            gameManager.PrepareFlowSatisfaction(
                potentialPowerProduction,
                potentialPowerConsumption,
                potentialLogisticsProduction,
                potentialLogisticsConsumption);
            resourceManager.CalculateTickSatisfaction(deltaSeconds);
            if (!RefreshEfficienciesCore(resourceManager, gameState))
            {
                converged = true;
                break;
            }
        }
        LastEfficiencyPassCount = passesUsed;
        LastActiveBuildingCount = activeBuildingCount;
#if UNITY_EDITOR
        if (!converged && efficiencyConvergenceLogCooldown <= 0f)
        {
            efficiencyConvergenceLogCooldown = 1f;
            KingdomEditorPerfLog.Write(
                $"[KingdomPerf] EfficiencyConvergenceLimit passes={passesUsed} " +
                $"limit={maximumPasses} states={orderedStates.Count}");
        }
        efficiencyConvergenceLogCooldown = Math.Max(
            0f, efficiencyConvergenceLogCooldown - (float)deltaSeconds);
#endif
        RefreshResearchPower();
    }

    private ExpantaNum CalculateEfficiency(
        Building building,
        ResourceManager resourceManager,
        GameState gameState)
    {
        ExpantaNum resourceSatisfaction = ExpantaNum.One;
        IReadOnlyList<Pair<Resource, ExpantaNum>> rates = building.ResourceConsumptionRates;
        for (int i = 0; i < rates.Count; i++)
            resourceSatisfaction = ExpantaNum.Min(
                resourceSatisfaction,
                resourceManager.GetTickSatisfaction(rates[i].First));

        ExpantaNum powerSatisfaction = building.PowerConsumptionRate > ExpantaNum.Zero
            ? gameState.PowerSatisfaction
            : ExpantaNum.One;
        ExpantaNum logisticsSatisfaction = building.LogisticsConsumptionRate > ExpantaNum.Zero
            ? gameState.LogisticsSatisfaction
            : ExpantaNum.One;
        // Buildings that explicitly consume energy but no food are automated
        // production and must be governed by the power flow, not by the food
        // happiness constraint. Buildings with a food upkeep remain food
        // constrained even when they also consume power.
        bool foodConstraintRequired = IsFoodConstraintRequired(building);
        ExpantaNum happinessConstraint = foodConstraintRequired
            ? gameState.HappinessConstraintMultiplier
            : ExpantaNum.One;
        return CalculateEffectiveEfficiency(
            GlobalEfficiencyFactor,
            resourceSatisfaction,
            happinessConstraint,
            powerSatisfaction,
            logisticsSatisfaction);
    }

    public static bool IsFoodConstraintRequired(Building building)
    {
        if (building == null)
            return false;
        return building.PowerConsumptionRate <= ExpantaNum.Zero ||
            building.FoodConsumptionRate > ExpantaNum.Zero;
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
        UpdateActiveBuildingIndex(state, state.Amount);
        researchPowerDirty = true;
    }

    private void UpdateActiveBuildingIndex(BuildingState state, ExpantaNum amount)
    {
        if (state == null)
            return;
        bool shouldBeActive = amount > ExpantaNum.Zero;
        if (shouldBeActive)
        {
            if (activeBuildingStateSet.Add(state))
            {
                int low = 0;
                int high = activeBuildingStates.Count;
                while (low < high)
                {
                    int middle = low + (high - low) / 2;
                    int comparison = string.Compare(
                        activeBuildingStates[middle].Definition.Id,
                        state.Definition.Id,
                        StringComparison.OrdinalIgnoreCase);
                    if (comparison < 0)
                        low = middle + 1;
                    else
                        high = middle;
                }
                activeBuildingStates.Insert(low, state);
            }
            return;
        }

        if (!activeBuildingStateSet.Remove(state))
            return;
        activeBuildingStates.Remove(state);
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
        ResourceManager resourceManager = null;
        for (int i = 0; i < generation.Count; i++)
        {
            if (resourceManager == null)
                resourceManager = ResourceManager.Instance;
            resourceManager.AdjustProductionRate(
                generation[i].First,
                scaleDelta * generation[i].Second
                * productionMultiplier
                * modifiers.GetBuildingResourceProductionMultiplier(
                    state.Definition, generation[i].First)
                * modifiers.GetResourceProductionMultiplier(generation[i].First));
        }

        IReadOnlyList<Pair<Resource, ExpantaNum>> consumption = state.Definition.ResourceConsumptionRates;
        for (int i = 0; i < consumption.Count; i++)
        {
            if (resourceManager == null)
                resourceManager = ResourceManager.Instance;
            resourceManager.AdjustConsumptionRate(
                consumption[i].First,
                scaleDelta * consumption[i].Second * productionMultiplier);
        }

        GameManager gameManager = GameManager.Instance;
        gameManager.AdjustFoodRates(
            scaleDelta * state.Definition.FoodProductionRate
                * productionMultiplier,
            scaleDelta * state.Definition.FoodConsumptionRate);
        if (applyCapacityDeltas)
        {
            gameManager.AdjustFoodCapacity(
                scaleDelta * state.Definition.FoodCapacityGranted
                    * modifiers.FoodCapacityMultiplier);
            gameManager.AdjustPopulationCapacity(
                amountDelta * state.Definition.PopulationCapacityGranted);
        }
        gameManager.AdjustPowerRates(
            scaleDelta * state.Definition.PowerProductionRate
                * modifiers.PowerMultiplier
                * modifiers.GetBuildingPowerProductionMultiplier(state.Definition),
            scaleDelta * state.Definition.PowerConsumptionRate);
        gameManager.AdjustLogisticsRates(
            scaleDelta * state.Definition.LogisticsProductionRate
                * modifiers.GlobalLogisticsMultiplier
                * modifiers.GetBuildingLogisticsProductionMultiplier(state.Definition),
            scaleDelta * state.Definition.LogisticsConsumptionRate);
        if (state.Definition.TechLevel >= TechLevel.Spacer)
        {
            gameManager.AdjustFleetPower(
                scaleDelta * state.Definition.FleetPowerGranted);
            gameManager.AdjustAttackPower(
                scaleDelta * state.Definition.AttackPowerGranted);
            gameManager.AdjustDefensePower(
                scaleDelta * state.Definition.DefensePowerGranted);
            gameManager.AdjustMilitaryManpower(
                scaleDelta * state.Definition.MilitaryManpowerGranted);
        }
    }

    internal void ApplyProgressionModifierChange(
        ProgressionModifierState previous,
        ProgressionModifierState current)
    {
        if (previous == null || current == null)
            return;

        researchPowerDirty = true;

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
                    oldBuildingMultiplier *
                    previous.GetBuildingResourceProductionMultiplier(building, rate.First) *
                    previous.GetResourceProductionMultiplier(rate.First);
                ExpantaNum newMultiplier =
                    newBuildingMultiplier *
                    current.GetBuildingResourceProductionMultiplier(building, rate.First) *
                    current.GetResourceProductionMultiplier(rate.First);
                ResourceManager.Instance.AdjustProductionRate(
                    rate.First,
                    scale * rate.Second * (newMultiplier - oldMultiplier));
            }

            IReadOnlyList<Pair<Resource, ExpantaNum>> consumption =
                building.ResourceConsumptionRates;
            for (int j = 0; j < consumption.Count; j++)
                ResourceManager.Instance.AdjustConsumptionRate(
                    consumption[j].First,
                    scale * consumption[j].Second
                    * (newBuildingMultiplier - oldBuildingMultiplier));

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
        for (int i = 0; i < activeBuildingStates.Count; i++)
        {
            BuildingState state = activeBuildingStates[i];
            if (state.Efficiency == startingEfficiency)
                continue;
            ApplyRateDelta(
                state,
                state.Amount,
                state.Efficiency,
                state.Amount,
                startingEfficiency);
            state.SetEfficiency(startingEfficiency);
            researchPowerDirty = true;
        }
    }

    private static void EnsureBuildingResources(Building building)
    {
        ResourceManager resourceManager = null;
        EnsureResources(building.ResourceRequirements, ref resourceManager);
        EnsureResources(building.ResourceGenerationRates, ref resourceManager);
        EnsureResources(building.ResourceConsumptionRates, ref resourceManager);
    }

    private static void EnsureResources(
        IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
        ref ResourceManager resourceManager)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            if (resourceManager == null)
                resourceManager = ResourceManager.Instance;
            resourceManager.EnsureResource(pairs[i].First);
        }
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
        researchPowerDirty = true;
        for (int i = 0; i < orderedStates.Count; i++)
        {
            BuildingState state = orderedStates[i];
            if (state.Amount <= ExpantaNum.Zero)
                continue;

            if (state.Definition is not SectorBuilding)
                GameManager.Instance.CommitConstruction(state.SpaceCost * state.Amount);
            ApplyRateDelta(state, ExpantaNum.Zero, ExpantaNum.One, state.Amount, state.Efficiency);
        }
        RebuildActiveBuildingIndex();
        RefreshResearchPower();
    }

    internal void ResetForLoad()
    {
        researchPowerDirty = true;
        activeBuildingStates.Clear();
        activeBuildingStateSet.Clear();
        for (int i = 0; i < orderedStates.Count; i++)
            orderedStates[i].ResetForLoad();
        GlobalEfficiencyFactor = ExpantaNum.One;
        chainIndexInitialized = false;
        RefreshResearchPower();
    }

    public SaveManager.BuildingSaveData CaptureSaveData()
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

        researchPowerDirty = true;

        if (data.Buildings != null)
        {
            var restoredBuildings = new HashSet<Building>();
            var restoredAmounts = new Dictionary<Building, ExpantaNum>();
            for (int i = 0; i < data.Buildings.Count; i++)
            {
                SaveManager.BuildingStateSaveData saved = data.Buildings[i];
                if (!DataBase<Building>.TryFind(saved.BuildingId, out Building definition))
                    throw new InvalidOperationException(
                        $"存档包含未知建筑编号“{saved.BuildingId}”。");
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
        RebuildActiveBuildingIndex();
        RefreshResearchPower();
    }

    internal void ValidateSectorBuildingState(SectorManager sectors)
    {
        if (sectors == null)
            throw new ArgumentNullException(nameof(sectors));
        for (int i = 0; i < orderedStates.Count; i++)
        {
            BuildingState state = orderedStates[i];
            if (state.Definition is not SectorBuilding sectorBuilding)
                continue;
            if (state.Amount > new ExpantaNum(sectorBuilding.MaxAmount))
                throw new InvalidOperationException(
                    $"Sector building '{sectorBuilding.Id}' exceeds its build limit.");
            if (state.Amount > ExpantaNum.Zero &&
                !sectors.GetState(sectorBuilding.Sector).Occupied)
            {
                throw new InvalidOperationException(
                    $"Sector building '{sectorBuilding.Id}' is built before sector '{sectorBuilding.Sector.Id}' is occupied.");
            }
        }
    }

    public override void Save() => SaveManager.Instance.SaveNow(true);

    public override void Load() => SaveManager.Instance.LoadOrCreateGame();

    private void RefreshResearchPower()
    {
        if (!researchPowerDirty)
            return;
        ResearchManager researchManager = cachedResearchManager;
        if (researchManager == null)
            researchManager = cachedResearchManager = FindObjectOfType<ResearchManager>();
        if (researchManager == null)
            return;
        researchManager.RebuildResearchPower(activeBuildingStates);
        ResearchPowerRebuildCount++;
        researchPowerDirty = false;
    }

    private void RebuildActiveBuildingIndex()
    {
        activeBuildingStates.Clear();
        activeBuildingStateSet.Clear();
        for (int i = 0; i < orderedStates.Count; i++)
        {
            BuildingState state = orderedStates[i];
            if (state.Amount > ExpantaNum.Zero)
            {
                activeBuildingStates.Add(state);
                activeBuildingStateSet.Add(state);
            }
        }
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
