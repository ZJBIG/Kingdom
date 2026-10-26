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
    DeconstructionUnavailable = 8,
    BuildingTierSuperseded,
    UpgradeUnavailable,
    SectorNotOccupied,
    BuildingLimitReached,
    InteriorExplorationIncomplete
}

public sealed class BuildingDeconstructionPreview
{
    public ExpantaNum Amount { get; }
    public ExpantaNum ProductivityReleased { get; }
    public ExpantaNum PopulationCapacityRemoved { get; }
    public ExpantaNum FoodCapacityRemoved { get; }
    public ExpantaNum FoodOverflow { get; }
    public IReadOnlyDictionary<ResourceDef, ExpantaNum> Refunds { get; }
    internal BuildingDeconstructionPreview(ExpantaNum amount, ExpantaNum productivity,
        ExpantaNum population, ExpantaNum capacity, ExpantaNum overflow,
        Dictionary<ResourceDef, ExpantaNum> refunds)
    {
        Amount = amount; ProductivityReleased = productivity;
        PopulationCapacityRemoved = population; FoodCapacityRemoved = capacity;
        FoodOverflow = overflow;
        Refunds = new System.Collections.ObjectModel.ReadOnlyDictionary<ResourceDef, ExpantaNum>(refunds);
    }
}

public class BuildingManager : Singleton<BuildingManager>
{
    private const double BuildBoundaryTolerance = 1e-8d;

    private static bool CanChangeBuildingAmount(ExpantaNum current, ExpantaNum amount, bool increasing)
    {
        if (!amount.IsFinite || amount < ExpantaNum.One)
            return false;
        // Small quantities can round away entirely at large represented counts.
        ExpantaNum next = increasing ? current + amount : current - amount;
        return next.IsFinite && next >= ExpantaNum.Zero &&
            (increasing ? next > current : next < current);
    }

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
        BuildingDef building,
        BuildingState state,
        ExpantaNum amount)
    {
        var costs = new Dictionary<ResourceDef, ExpantaNum>();
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements = building.ResourceRequirements;
        ExpantaNum multiplier = GetConstructionCostMultiplier(building);
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> pair = requirements[i];
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

        foreach (KeyValuePair<ResourceDef, ExpantaNum> entry in costs)
        {
            if (ExceedsBuildBoundary(
                    entry.Value,
                    ResourceManager.Instance.GetAmount(entry.Key),
                    out _))
                return false;
        }
        return true;
    }
    private const double UpgradeRecoveryRate = 0.8d;
    private const int UpgradeBinarySearchLimit = 256;
    private readonly Dictionary<BuildingDef, BuildingState> states = new();
    private readonly List<BuildingState> orderedStates = new();
    private readonly List<BuildingState> activeBuildingStates = new();
    private readonly HashSet<BuildingState> activeBuildingStateSet = new();
    private readonly HashSet<BuildingState> budgetBuildings = new();
    private readonly HashSet<ResourceDef> budgetResources = new();
    private readonly Dictionary<ResourceDef, ExpantaNum> budgetProduction = new();
    private readonly Dictionary<ResourceDef, ExpantaNum> budgetConsumption = new();
    private readonly List<BudgetResourceInput> budgetResourceInputs = new();
    private readonly struct BudgetResourceInput
    {
        public readonly ResourceState State;
        public readonly ExpantaNum Production;
        public readonly ExpantaNum Consumption;
        public readonly ExpantaNum ExternalProduction;

        public BudgetResourceInput(ResourceState state, ExpantaNum production,
            ExpantaNum consumption, ExpantaNum externalProduction)
        {
            State = state;
            Production = production;
            Consumption = consumption;
            ExternalProduction = externalProduction;
        }
    }
    private readonly Dictionary<ResourceDef, ExpantaNum> candidateProduction = new();
    private readonly Dictionary<ResourceDef, ExpantaNum> candidateConsumption = new();
    private ExpantaNum budgetPowerProduction, budgetPowerConsumption;
    private ExpantaNum budgetLogisticsProduction, budgetLogisticsConsumption;
    private readonly List<Pair<ResourceDef, ExpantaNum>> upgradeAffordabilityDeltas = new();
    // 一个升级目标可以由多个分支汇聚而来，因此这里必须保留全部前置建筑。
    private readonly Dictionary<BuildingDef, List<BuildingDef>> chainPredecessors = new();
    private readonly HashSet<BuildingDef> chainMembers = new();
    private readonly HashSet<BuildingDef> chainRoots = new();
    private readonly Dictionary<SectorDef, List<SectorBuildingDef>> sectorBuildingsBySector = new();
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

    public IReadOnlyDictionary<BuildingDef, BuildingState> States => states;
    internal IReadOnlyList<BuildingState> OrderedStates => orderedStates;
    public ExpantaNum TotalProductivity =>
        ExpantaNum.Max(ExpantaNum.Zero, CalculateRawTotalProductivity());
    public ExpantaNum UsedProductivity
    {
        get
        {
            ExpantaNum used = ExpantaNum.Zero;
            for (int i = 0; i < orderedStates.Count; i++)
                if (orderedStates[i].IsEnabled)
                    used += orderedStates[i].Amount * orderedStates[i].ProductivityConsumption;
            return ExpantaNum.Max(ExpantaNum.Zero, used);
        }
    }
    public ExpantaNum AvailableProductivity =>
        TotalProductivity - UsedProductivity;
    public ExpantaNum LaborOperatingRatio => CalculateLaborOperatingRatio(TotalProductivity, UsedProductivity);

    public static ExpantaNum CalculateLaborOperatingRatio(ExpantaNum supply, ExpantaNum demand) =>
        demand <= ExpantaNum.Zero ? ExpantaNum.One :
            ExpantaNum.Clamp01(ExpantaNum.Max(ExpantaNum.Zero, supply) / demand);

    public ExpantaNum GetLaborOperatingRatio(BuildingDef building) =>
        building != null && building.ProductivityConsumption > ExpantaNum.Zero
            ? LaborOperatingRatio : ExpantaNum.One;

    private ExpantaNum GetPotentialEfficiency(BuildingDef building, ExpantaNum laborRatio) =>
        ExpantaNum.Clamp01(GlobalEfficiencyFactor) *
        (building.ProductivityConsumption > ExpantaNum.Zero ? laborRatio : ExpantaNum.One);
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
            if (orderedStates[i].IsEnabled)
                total += orderedStates[i].Amount * orderedStates[i].ProductivityGranted;
        return total;
    }

    internal void InitializeStartingBuildings()
    {
        IReadOnlyList<BuildingDef> definitions = DataBase<BuildingDef>.All;
        RebuildBuildingChainIndex(GetBuildingDefinitionsForIndex(definitions));
        for (int i = 0; i < definitions.Count; i++)
            EnsureBuilding(definitions[i]);
    }

    private static IReadOnlyList<BuildingDef> GetBuildingDefinitionsForIndex(
        IReadOnlyList<BuildingDef> definitions)
    {
        List<BuildingDef> indexedDefinitions = new(definitions.Count + DataBase<SectorBuildingDef>.All.Count);
        for (int i = 0; i < definitions.Count; i++)
            if (definitions[i] != null)
                indexedDefinitions.Add(definitions[i]);

        IReadOnlyList<SectorBuildingDef> sectorBuildings = DataBase<SectorBuildingDef>.All;
        for (int i = 0; i < sectorBuildings.Count; i++)
        {
            SectorBuildingDef sectorBuilding = sectorBuildings[i];
            if (sectorBuilding != null && !indexedDefinitions.Contains(sectorBuilding))
                indexedDefinitions.Add(sectorBuilding);
        }
        return indexedDefinitions;
    }

    public static void ValidateBuildingChains(IReadOnlyList<BuildingDef> definitions)
    {
        if (definitions == null)
            throw new ArgumentNullException(nameof(definitions));

        ValidateMergedResourceFlows(definitions);
        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] is SectorBuildingDef sectorBuilding)
                SectorBuildingDef.Validate(sectorBuilding);
        }

        var definitionSet = new HashSet<BuildingDef>();
        for (int i = 0; i < definitions.Count; i++)
            if (definitions[i] != null)
                definitionSet.Add(definitions[i]);
        for (int i = 0; i < definitions.Count; i++)
        {
            BuildingDef source = definitions[i];
            if (source == null || source.UpgradeTo == null)
                continue;
            BuildingDef target = source.UpgradeTo;
            if (!definitionSet.Contains(target))
                throw new InvalidOperationException(
                    $"建筑升级链“{source.Id}”指向了未加载的定义。");
            if (source == target)
                throw new InvalidOperationException(
                    $"建筑升级链“{source.Id}”不能升级到自身。");
            ValidateChainEconomy(source);
            ValidateChainEconomy(target);
            if (target is SectorBuildingDef)
                throw new InvalidOperationException(
                    $"Building '{source.Id}' cannot upgrade to sector building '{target.Id}'.");
        }

        var visited = new HashSet<BuildingDef>();
        var visiting = new HashSet<BuildingDef>();
        for (int i = 0; i < definitions.Count; i++)
            ValidateChainNode(definitions[i], visited, visiting);
    }

    public static void ValidateMergedResourceFlows(IReadOnlyList<BuildingDef> definitions)
    {
        if (definitions == null)
            throw new ArgumentNullException(nameof(definitions));

        for (int i = 0; i < definitions.Count; i++)
        {
            definitions[i]?.ValidateResourceFlowDefinitions();
        }
    }

    private static void ValidateChainEconomy(BuildingDef building)
    {
        if (!building.HasValidCostGrowth)
            throw new InvalidOperationException(
                $"建筑升级链成员“{building.Id}”的成本增长倍率无效。");
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements =
            building.ResourceRequirements;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> requirement = requirements[i];
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
        BuildingDef building,
        HashSet<BuildingDef> visited,
        HashSet<BuildingDef> visiting)
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
    public void RebuildBuildingChainIndexForEditor(IReadOnlyList<BuildingDef> definitions) =>
        RebuildBuildingChainIndex(definitions);
    public int GetChainPredecessorCountForEditor(BuildingDef target) =>
        chainPredecessors.TryGetValue(target, out List<BuildingDef> predecessors)
            ? predecessors.Count
            : 0;
    public static ExpantaNum GetConstructionCostMultiplierForEditor(BuildingDef building) =>
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
    public void SetAmountAndRatesForEditor(BuildingState state, ExpantaNum newAmount)
    {
        SetAmountAndRates(state, newAmount);
        if (newAmount > ExpantaNum.Zero && state.PopulationCapacityGranted > ExpantaNum.Zero)
            GameManager.Instance.State.RestoreHousingMilestone(true);
    }
#endif

    private void RebuildBuildingChainIndex(IReadOnlyList<BuildingDef> definitions)
    {
        ValidateBuildingChains(definitions);
        chainPredecessors.Clear();
        chainMembers.Clear();
        chainRoots.Clear();
        sectorBuildingsBySector.Clear();
        for (int i = 0; i < definitions.Count; i++)
        {
            BuildingDef source = definitions[i];
            if (source == null || source.UpgradeTo == null)
                continue;
            if (!chainPredecessors.TryGetValue(source.UpgradeTo, out List<BuildingDef> predecessors))
            {
                predecessors = new List<BuildingDef>();
                chainPredecessors.Add(source.UpgradeTo, predecessors);
            }
            predecessors.Add(source);
            chainMembers.Add(source);
            chainMembers.Add(source.UpgradeTo);
        }
        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] is not SectorBuildingDef sectorBuilding)
                continue;
            if (!sectorBuildingsBySector.TryGetValue(
                    sectorBuilding.Sector,
                    out List<SectorBuildingDef> buildings))
            {
                buildings = new List<SectorBuildingDef>();
                sectorBuildingsBySector.Add(sectorBuilding.Sector, buildings);
            }
            buildings.Add(sectorBuilding);
        }
        foreach (BuildingDef member in chainMembers)
            if (!chainPredecessors.ContainsKey(member))
                chainRoots.Add(member);
        chainIndexInitialized = true;
    }

    private void EnsureBuildingChainIndex()
    {
        if (!chainIndexInitialized)
            RebuildBuildingChainIndex(
                GetBuildingDefinitionsForIndex(DataBase<BuildingDef>.All));
    }

    public BuildingState EnsureBuilding(BuildingDef building)
    {
        if (building == null)
            throw new ArgumentNullException(nameof(building));
        if (TryGetStateByStableId(building, out BuildingState existing))
            return existing;

        building = ResolveBuildingDefinition(building);
        var state = new BuildingState(building);
        states.Add(building, state);
        InsertOrdered(state);

        EnsureBuildingResources(building);
        ObserverNotifications.Publish(BuildingStateAdded, state);
        return state;
    }

    public BuildingState GetState(BuildingDef building)
    {
        if (building == null)
            throw new ArgumentNullException(nameof(building));
        if (TryGetStateByStableId(building, out BuildingState state))
            return state;
        throw new KeyNotFoundException($"建筑状态“{building.Id}”尚未创建。");
    }

    private bool TryGetStateByStableId(BuildingDef building, out BuildingState state)
    {
        state = null;
        if (building == null)
            return false;
        if (states.TryGetValue(building, out state))
            return true;
        foreach (KeyValuePair<BuildingDef, BuildingState> entry in states)
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

    private BuildingDef ResolveBuildingDefinition(BuildingDef building)
    {
        if (TryGetStateByStableId(building, out BuildingState state))
            return state.Definition;
        if (building != null && DataBase<BuildingDef>.TryFind(building.Id, out BuildingDef definition))
            return definition;
        return building;
    }

    public bool ArePrerequisitesMet(BuildingDef building, out BuildFailure failure)
    {
        building = ResolveBuildingDefinition(building);
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

        if (building is SectorBuildingDef sectorBuilding)
        {
            SectorState sectorState = GameManager.Instance.Sectors.GetState(sectorBuilding.Sector);
            if (!sectorState.Occupied)
            {
                failure = BuildFailure.SectorNotOccupied;
                return false;
            }
            if (sectorBuilding.RequiresInteriorExploration && !sectorState.InteriorExplorationCompleted)
            {
                failure = BuildFailure.InteriorExplorationIncomplete;
                return false;
            }
        }

        IReadOnlyList<ResearchDef> requiredResearch = building.RequiredResearch;
        for (int i = 0; i < requiredResearch.Count; i++)
        {
            ResearchDef research = requiredResearch[i];
            if (research == null || !ResearchManager.Instance.IsResearchCompleted(research.Id))
            {
                failure = BuildFailure.ResearchPrerequisiteIncomplete;
                return false;
            }
        }

        WorkshopManager workshop = cachedWorkshopManager;
        if (workshop == null)
            workshop = cachedWorkshopManager = FindObjectOfType<WorkshopManager>();
        IReadOnlyList<WorkshopUpgradeDef> requiredUpgrades = building.RequiredWorkshopUpgrades;
        for (int i = 0; i < requiredUpgrades.Count; i++)
        {
            WorkshopUpgradeDef upgrade = requiredUpgrades[i];
            if (workshop == null || upgrade == null || !workshop.IsPurchased(upgrade))
            {
                failure = BuildFailure.WorkshopPrerequisiteIncomplete;
                return false;
            }
        }

        failure = BuildFailure.None;
        return true;

    }

    public bool CanConstructNew(BuildingDef building)
    {
        building = ResolveBuildingDefinition(building);
        return ArePrerequisitesMet(building, out _) &&
            IsHighestUnlockedChainTier(building);
    }

    public IReadOnlyList<SectorBuildingDef> GetSectorBuildings(SectorDef sector)
    {
        EnsureBuildingChainIndex();
        if (sector == null)
            return Array.Empty<SectorBuildingDef>();
        if (sectorBuildingsBySector.TryGetValue(sector, out List<SectorBuildingDef> buildings) &&
            buildings.Count > 0)
            return buildings;

        // UI callers may hold an equivalent definition instance after a scene
        // reload. Resolve the authored stable ID instead of returning an empty
        // menu solely because the object reference changed.
        string sectorId = sector.Id == null ? string.Empty : sector.Id.Trim();
        if (sectorId.Length == 0)
            return Array.Empty<SectorBuildingDef>();
        foreach (KeyValuePair<SectorDef, List<SectorBuildingDef>> entry in sectorBuildingsBySector)
        {
            SectorDef indexed = entry.Key;
            if (indexed != null && string.Equals(indexed.Id, sectorId, StringComparison.OrdinalIgnoreCase) &&
                entry.Value != null && entry.Value.Count > 0)
                return entry.Value;
        }
        IReadOnlyList<SectorBuildingDef> allSectorBuildings = DataBase<SectorBuildingDef>.All;
        if (allSectorBuildings.Count > 0)
        {
            List<SectorBuildingDef> matches = new();
            for (int i = 0; i < allSectorBuildings.Count; i++)
            {
                SectorBuildingDef building = allSectorBuildings[i];
                SectorDef buildingSector = building == null ? null : building.Sector;
                if (buildingSector != null &&
                    string.Equals(buildingSector.Id, sectorId, StringComparison.OrdinalIgnoreCase))
                    matches.Add(building);
            }
            if (matches.Count > 0)
                return matches;
        }

        // The base Building database is the authoritative merged catalog in
        // some editor/runtime initialization orders. Include its derived
        // SectorBuildingDef entries before declaring the menu empty.
        IReadOnlyList<BuildingDef> allBuildings = DataBase<BuildingDef>.All;
        List<SectorBuildingDef> baseMatches = new();
        for (int i = 0; i < allBuildings.Count; i++)
        {
            if (allBuildings[i] is not SectorBuildingDef building || building.Sector == null)
                continue;
            if (string.Equals(building.Sector.Id, sectorId, StringComparison.OrdinalIgnoreCase))
                baseMatches.Add(building);
        }
        if (baseMatches.Count > 0)
            return baseMatches;
        return Array.Empty<SectorBuildingDef>();
    }

    private bool IsHighestUnlockedChainTier(BuildingDef building)
    {
        EnsureBuildingChainIndex();
        if (!chainMembers.Contains(building))
            return true;

        BuildingDef highest = FindHighestUnlockedTier(building);
        if (highest == building)
            return true;

        // Material-free resource and utility sources must remain buildable:
        // their upgrades can depend on the very supplies needed to restart them.
        return building.ResourceConsumptionRates.Count == 0 &&
            (building.ResourceGenerationRates.Count > 0 || building.FoodProductionRate > ExpantaNum.Zero ||
             building.PowerContribution > ExpantaNum.Zero || building.LogisticsContribution > ExpantaNum.Zero) &&
            highest.ResourceConsumptionRates.Count > 0;
    }

    private BuildingDef FindHighestUnlockedTier(BuildingDef building)
    {
        BuildingDef highest = building;
        while (highest.UpgradeTo != null &&
               ArePrerequisitesMet(highest.UpgradeTo, out _))
            highest = highest.UpgradeTo;
        return highest;
    }

    public void RefreshBuildingChainAvailability()
    {
        EnsureBuildingChainIndex();
        foreach (BuildingDef root in chainRoots)
        {
            RemoveZeroIntermediateStates(root);
        }
    }

    private void RemoveZeroIntermediateStates(BuildingDef root)
    {
        if (!ArePrerequisitesMet(root, out _))
            return;

        BuildingDef highest = FindHighestUnlockedTier(root);
        if (highest == root)
            return;

        var lowerTiers = new List<BuildingDef>();
        BuildingDef current = root;
        while (current != null && current != highest)
        {
            lowerTiers.Add(current);
            current = current.UpgradeTo;
        }

        for (int i = 0; i < lowerTiers.Count; i++)
            RemoveZeroBuildingState(lowerTiers[i]);
    }

    private void RemoveZeroBuildingState(BuildingDef building)
    {
        if (!TryGetStateByStableId(building, out BuildingState state) ||
            state.Amount > ExpantaNum.Zero)
            return;
        states.Remove(state.Definition);
        orderedStates.Remove(state);
    }

    public bool ShouldDisplay(BuildingDef building)
    {
        if (building == null)
            return false;

        if (TryGetStateByStableId(building, out BuildingState state) && state.Amount > ExpantaNum.Zero)
            return true;

        if (!ArePrerequisitesMet(building, out _))
            return false;

        return CanConstructNew(building);

    }

    public bool TryGetUnlockedUpgradeTarget(BuildingDef source, out BuildingDef target)
    {
        target = null;
        if (source == null ||
            !TryGetStateByStableId(source, out BuildingState sourceState) ||
            sourceState.Amount < ExpantaNum.One)
        {
            return false;
        }

        source = sourceState.Definition;
        EnsureBuildingChainIndex();
        if (!chainMembers.Contains(source))
        {
            return false;
        }

        BuildingDef candidate = ResolveBuildingDefinition(source.UpgradeTo);
        while (candidate != null)
        {
            if (!ArePrerequisitesMet(candidate, out _))
                break;
            target = candidate;
            candidate = ResolveBuildingDefinition(candidate.UpgradeTo);
        }

        return target != null;
    }

    public BuildFailure GetBuildFailure(BuildingDef building, ExpantaNum requestedAmount)
    {
        TryBuildInternal(building, requestedAmount, out BuildFailure failure, true);
        return failure;
    }

    public void GetConstructionFailures(BuildingDef building, ExpantaNum amount, bool upgrading,
        List<BuildFailure> destination)
    {
        destination.Clear();
        BuildFailure failure;
        if (upgrading)
            TryUpgradeInternal(building, amount, out failure, true, destination);
        else
            TryBuildInternal(building, amount, out failure, true, destination);
        if (failure != BuildFailure.None && !destination.Contains(failure))
            destination.Add(failure);
    }

    public bool TryBuild(BuildingDef building, ExpantaNum requestedAmount, out BuildFailure failure) =>
        TryBuildInternal(building, requestedAmount, out failure, false);

    private bool TryBuildInternal(BuildingDef building, ExpantaNum requestedAmount,
        out BuildFailure failure, bool previewOnly, List<BuildFailure> failures = null)
    {
        building = ResolveBuildingDefinition(building);
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
        if (!CanChangeBuildingAmount(state.Amount, amount, true))
        {
            failure = BuildFailure.InvalidAmount;
            return false;
        }

        SectorBuildingDef sectorBuilding = building as SectorBuildingDef;
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
                if (failures == null) return false;
                failures.Add(failure);
            }
        }
        else if (state.Amount + amount > new ExpantaNum(sectorBuilding.MaxAmount))
        {
            failure = BuildFailure.BuildingLimitReached;
            return false;
        }

        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements = building.ResourceRequirements;
        var costs = new Dictionary<ResourceDef, ExpantaNum>();
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> pair = requirements[i];
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

        foreach (ResourceDef resource in new List<ResourceDef>(costs.Keys))
        {
            ExpantaNum available = ResourceManager.Instance.GetAmount(resource);
            if (ExceedsBuildBoundary(costs[resource], available, out ExpantaNum effectiveCost))
            {
                failure = BuildFailure.ResourceInsufficient;
                return false;
            }
            costs[resource] = effectiveCost;
        }

        if (failures != null && failures.Count > 0)
        {
            failure = failures[0];
            return false;
        }
        if (previewOnly)
        {
            failure = BuildFailure.None;
            return true;
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

        if (state.PopulationCapacityGranted > ExpantaNum.Zero)
            GameManager.Instance.State.RestoreHousingMilestone(true);
        failure = BuildFailure.None;
        return true;
    }

    private Dictionary<ResourceDef, ExpantaNum> CalculateDeconstructionRefunds(
        BuildingDef building, BuildingState state, ExpantaNum amount)
    {
        building = state.Definition;
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements = building.ResourceRequirements;
        var resourceChanges = new Dictionary<ResourceDef, ExpantaNum>();
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> pair = requirements[i];
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

        return resourceChanges;
    }

    public BuildingDeconstructionPreview GetDeconstructionPreview(BuildingDef building, ExpantaNum requestedAmount)
    {
        if (building == null || !TryGetStateByStableId(building, out BuildingState state)) return null;
        if (IsMegastructure(state.Definition)) return null;
        ExpantaNum amount = BuildingTransactionRules.ClampToAvailable(requestedAmount, state.Amount);
        if (!CanChangeBuildingAmount(state.Amount, amount, false)) return null;
        ExpantaNum capacityRemoved = amount * state.FoodCapacityGranted *
            ProgressionModifierManager.Current.FoodCapacityMultiplier;
        GameState game = GameManager.Instance.State;
        return new BuildingDeconstructionPreview(amount,
            amount * (state.ProductivityConsumption - state.ProductivityGranted),
            amount * state.PopulationCapacityGranted, capacityRemoved,
            ExpantaNum.Max(ExpantaNum.Zero, game.FoodAmount - ExpantaNum.Max(ExpantaNum.Zero,
                game.FoodCapacity - capacityRemoved)),
            CalculateDeconstructionRefunds(building, state, amount));
    }

    public ExpantaNum TotalPowerDemand => GetBuildingFlowDemand(true, LaborOperatingRatio) +
        GameManager.Instance.UltraProject.GetCurrentPowerConsumptionRate() +
        GameManager.Instance.Relic.CurrentPowerConsumptionRate;

    public ExpantaNum TotalLogisticsDemand => GetBuildingFlowDemand(false, LaborOperatingRatio) +
        GameManager.Instance.UltraProject.GetCurrentLogisticsConsumptionRate() +
        GameManager.Instance.Relic.CurrentLogisticsConsumptionRate;

    private ExpantaNum GetBuildingFlowDemand(bool power, ExpantaNum laborRatio)
    {
        ExpantaNum demand = ExpantaNum.Zero;
        for (int i = 0; i < orderedStates.Count; i++)
            if (orderedStates[i].IsEnabled)
                demand += orderedStates[i].Amount * GetPotentialEfficiency(orderedStates[i].Definition, laborRatio) * (power
                ? ExpantaNum.Max(ExpantaNum.Zero, -orderedStates[i].Definition.PowerContribution)
                : ExpantaNum.Max(ExpantaNum.Zero, -orderedStates[i].Definition.LogisticsContribution));
        return demand;
    }

    // Demand is conditional on supply; blocked activities can spend less than this rate.
    public ExpantaNum StrategicFoodConsumptionRate
    {
        get
        {
            GameManager game = GameManager.Instance;
            ExpantaNum rate = game.Relic.CurrentFoodConsumptionRate;
            if (game.UltraProject.State.Status == UltraProjectStatus.Running)
                rate += game.UltraProject.GetCurrentFoodConsumptionRate();
            foreach (SectorState sector in game.Sectors.OrderedStates)
            {
                if (sector.ColonizationActive)
                    rate += game.Sectors.GetExplorationPreview(sector.Definition, game.State,
                        ResourceManager.Instance).FoodCostPerSecond;
                if (sector.CampaignActive)
                    rate += game.Sectors.GetCampaignPreview(sector.Definition, game.State,
                        ResourceManager.Instance).FoodCostPerSecond;
            }
            return rate;
        }
    }

    public bool TryDeconstruct(BuildingDef building, ExpantaNum requestedAmount)
    {
        return TryDeconstruct(building, requestedAmount, out _);
    }

    public bool TryDeconstruct(
        BuildingDef building,
        ExpantaNum requestedAmount,
        out BuildFailure failure)
    {
        if (building == null || !TryGetStateByStableId(building, out BuildingState state))
        {
            failure = BuildFailure.DeconstructionUnavailable;
            return false;
        }

        building = state.Definition;
        if (IsMegastructure(building))
        {
            failure = BuildFailure.DeconstructionUnavailable;
            return false;
        }
        ExpantaNum amount = BuildingTransactionRules.ClampToAvailable(requestedAmount, state.Amount);
        if (!CanChangeBuildingAmount(state.Amount, amount, false))
        {
            failure = BuildFailure.InvalidAmount;
            return false;
        }

        Dictionary<ResourceDef, ExpantaNum> resourceChanges = CalculateDeconstructionRefunds(building, state, amount);
        ExpantaNum previousFoodAmount = GameManager.Instance.State.FoodAmount;
        ExpantaNum previousFoodCapacity = GameManager.Instance.State.FoodCapacity;
        bool usesTerritory = !(building is SectorBuildingDef);
        ExpantaNum previousTerritoryUsed = usesTerritory
            ? GameManager.Instance.State.TerritoryUsed
            : ExpantaNum.Zero;
        ExpantaNum previousPopulationCapacity =
            GameManager.Instance.State.Population.PopulationCapacity;
        ExpantaNum previousPopulationProgress =
            GameManager.Instance.State.Population.PopulationChangeProgress;
        ExpantaNum previousAmount = state.Amount;
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

    public static bool IsMegastructure(BuildingDef building)
    {
        if (!(building is SectorBuildingDef)) return false;
        foreach (WorkshopUpgradeDef stage in DataBase<WorkshopUpgradeDef>.All)
            if (stage.RequiredBuilding == building) return true;
        return false;
    }

    public ExpantaNum GetMaxBuildable(BuildingDef building, ExpantaNum requestedMaximum)
    {
        building = ResolveBuildingDefinition(building);
        if (!CanConstructNew(building))
            return ExpantaNum.Zero;

        BuildingState state = EnsureBuilding(building);
        ExpantaNum result = ExpantaNum.Max(ExpantaNum.Zero, requestedMaximum.Floor());
        if (result < ExpantaNum.One)
            return ExpantaNum.Zero;

        if (building is SectorBuildingDef sectorBuilding)
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
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements = building.ResourceRequirements;
        ResourceManager resourceManager = null;
        ExpantaNum constructionCostMultiplier = ExpantaNum.One;
        ExpantaNum costGrowth = ExpantaNum.One;
        bool constructionParametersInitialized = false;
        for (int i = 0; i < requirements.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> pair = requirements[i];
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
        return CanChangeBuildingAmount(state.Amount, result, true) ? result : ExpantaNum.Zero;
    }

    public void GetUpgradeResourceDeltas(
        BuildingDef source,
        ExpantaNum requestedAmount,
        List<Pair<ResourceDef, ExpantaNum>> destination)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        destination.Clear();
        if (source == null ||
            !TryGetStateByStableId(source, out BuildingState sourceState) ||
            !TryGetUnlockedUpgradeTarget(source, out BuildingDef target))
        {
            return;
        }

        source = sourceState.Definition;
        BuildingState targetState = EnsureBuilding(target);
        target = targetState.Definition;
        ExpantaNum amount = BuildingTransactionRules.ClampToAvailable(
            requestedAmount.Floor(),
            sourceState.Amount);
        if (!CanChangeBuildingAmount(sourceState.Amount, amount, false) ||
            !CanChangeBuildingAmount(targetState.Amount, amount, true))
            return;

        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> sourceRequirements = source.ResourceRequirements;
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> targetRequirements = target.ResourceRequirements;
        AddUpgradeResources(sourceRequirements, destination);
        AddUpgradeResources(targetRequirements, destination);
        ExpantaNum sourceCostMultiplier = ExpantaNum.One;
        ExpantaNum targetCostMultiplier = ExpantaNum.One;
        for (int i = 0; i < destination.Count; i++)
        {
            ResourceDef resource = destination[i].First;
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
            destination[i] = new Pair<ResourceDef, ExpantaNum>(
                resource,
                targetCost - sourceCost * UpgradeRecoveryRate);
        }
    }

    public BuildFailure GetUpgradeFailure(BuildingDef source, ExpantaNum requestedAmount)
    {
        TryUpgradeInternal(source, requestedAmount, out BuildFailure failure, true);
        return failure;
    }

    public bool TryUpgrade(BuildingDef source, ExpantaNum requestedAmount, out BuildFailure failure) =>
        TryUpgradeInternal(source, requestedAmount, out failure, false);

    private bool TryUpgradeInternal(BuildingDef source, ExpantaNum requestedAmount,
        out BuildFailure failure, bool previewOnly, List<BuildFailure> failures = null)
    {
        if (source == null ||
            !TryGetStateByStableId(source, out BuildingState sourceState) ||
            !TryGetUnlockedUpgradeTarget(source, out BuildingDef target))
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
        if (!CanApplyUpgradeConstraints(
                sourceState, targetState, amount, out failure, failures))
            return false;

        var resourceDeltas = new List<Pair<ResourceDef, ExpantaNum>>();
        GetUpgradeResourceDeltas(source, amount, resourceDeltas);
        for (int i = 0; i < resourceDeltas.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> delta = resourceDeltas[i];
            if (delta.First == null || !delta.Second.IsFinite ||
                (delta.Second > ExpantaNum.Zero &&
                 ResourceManager.Instance.GetAmount(delta.First) < delta.Second))
            {
                failure = BuildFailure.ResourceInsufficient;
                return false;
            }
        }

        if (failures != null && failures.Count > 0)
        {
            failure = failures[0];
            return false;
        }
        if (previewOnly)
        {
            failure = BuildFailure.None;
            return true;
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
        var resourceChanges = new Dictionary<ResourceDef, ExpantaNum>();
        for (int i = 0; i < resourceDeltas.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> delta = resourceDeltas[i];
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

    public ExpantaNum GetMaxUpgradeable(BuildingDef source, ExpantaNum requestedMaximum)
    {
        if (source == null ||
            !TryGetStateByStableId(source, out BuildingState sourceState) ||
            !TryGetUnlockedUpgradeTarget(source, out BuildingDef target))
        {
            return ExpantaNum.Zero;
        }

        BuildingState targetState = EnsureBuilding(target);
        ExpantaNum high = ExpantaNum.Min(
            sourceState.Amount,
            ExpantaNum.Max(ExpantaNum.Zero, requestedMaximum.Floor()));
        if (high < ExpantaNum.One)
            return ExpantaNum.Zero;
        ResourceManager resourceManager = null;
        if (CanAffordUpgrade(
                sourceState, targetState, high, ref resourceManager))
            return high;

        ExpantaNum low = ExpantaNum.Zero;
        for (int i = 0; i < UpgradeBinarySearchLimit && high - low > ExpantaNum.One; i++)
        {
            ExpantaNum middle = ((low + high) / 2d).Floor();
            if (middle <= low)
                break;
            if (CanAffordUpgrade(
                    sourceState, targetState, middle, ref resourceManager))
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
        ref ResourceManager resourceManager)
    {
        if (!CanApplyUpgradeConstraints(
                sourceState, targetState, amount, out _))
            return false;
        List<Pair<ResourceDef, ExpantaNum>> deltas = upgradeAffordabilityDeltas;
        GetUpgradeResourceDeltas(sourceState.Definition, amount, deltas);
        for (int i = 0; i < deltas.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> delta = deltas[i];
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
        out BuildFailure failure, List<BuildFailure> failures = null)
    {
        if (!CanChangeBuildingAmount(sourceState.Amount, amount, false) ||
            !CanChangeBuildingAmount(targetState.Amount, amount, true))
        {
            failure = BuildFailure.InvalidAmount;
            return false;
        }

        ExpantaNum territoryDelta =
            (targetState.SpaceCost - sourceState.SpaceCost) * amount;
        if (territoryDelta > GameManager.Instance.State.AvailableTerritory)
        {
            failure = BuildFailure.SpaceInsufficient;
            if (failures == null) return false;
            failures.Add(failure);
        }

        failure = BuildFailure.None;
        return true;
    }

    private static void AddUpgradeResources(
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements,
        List<Pair<ResourceDef, ExpantaNum>> destination)
    {
        for (int i = 0; i < requirements.Count; i++)
        {
            ResourceDef resource = requirements[i].First;
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
                destination.Add(new Pair<ResourceDef, ExpantaNum>(resource, ExpantaNum.Zero));
        }
    }

    private static ExpantaNum FindBaseCost(
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> requirements,
        ResourceDef resource)
    {
        for (int i = 0; i < requirements.Count; i++)
            if (requirements[i].First == resource)
                return requirements[i].Second;
        return ExpantaNum.Zero;
    }

    internal static ExpantaNum GetConstructionCostMultiplier(BuildingDef building)
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
            sourceState.Definition.FoodCapacityGranted *
            modifiers.FoodCapacityMultiplier;
        ExpantaNum targetFoodCapacity =
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
        RefreshEfficienciesCore(LaborOperatingRatio);
        RefreshResearchPower();
    }

    private bool RefreshEfficienciesCore(
        ExpantaNum laborRatio,
        ResourceManager resourceManager = null,
        GameState gameState = null)
    {
        bool changed = false;
        for (int i = 0; i < activeBuildingStates.Count; i++)
        {
            BuildingState state = activeBuildingStates[i];
            if (resourceManager == null)
                resourceManager = ResourceManager.Instance;
            if (gameState == null)
                gameState = GameManager.Instance.State;
            ExpantaNum efficiency = CalculateEfficiency(
                state.Definition, resourceManager, gameState, laborRatio);
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
        // Supply and demand cannot change during this preparation batch.
        // Keep the snapshot local so the next population/building/effect change
        // is observed by the very next preparation or explicit refresh.
        ExpantaNum laborRatio = LaborOperatingRatio;
        ResetEfficienciesForTick(laborRatio);
        int activeBuildingCount = activeBuildingStates.Count;
        // Zero-amount definitions are skipped by every convergence pass and
        // cannot add another dependency step. Do not let the inactive tail
        // inflate the worst-case pass count on every simulation tick.
        int maximumPasses = Math.Max(1, activeBuildingCount + 1);
        int passesUsed = 0;
        bool converged = false;
        ExpantaNum potentialFoodConsumption = ExpantaNum.Zero;
        for (int pass = 0; pass < maximumPasses; pass++)
        {
            passesUsed = pass + 1;
            resourceManager.BeginTick();
            ExpantaNum potentialFoodProduction = ExpantaNum.Zero;
            potentialFoodConsumption = ExpantaNum.Zero;
            ExpantaNum potentialPowerProduction = ExpantaNum.Zero;
            ExpantaNum potentialPowerConsumption = ExpantaNum.Zero;
            ExpantaNum potentialLogisticsProduction = ExpantaNum.Zero;
            ExpantaNum potentialLogisticsConsumption = ExpantaNum.Zero;
            ProgressionModifierState modifiers = ProgressionModifierManager.Current;
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
                potentialFoodConsumption += gameManager.UltraProject.AccumulateTickResourceDemand(resourceManager);
            }
            potentialPowerConsumption += gameManager.Relic.CurrentPowerConsumptionRate;
            potentialLogisticsConsumption += gameManager.Relic.CurrentLogisticsConsumptionRate;
            potentialFoodConsumption += gameManager.Relic.CurrentFoodConsumptionRate;
            IReadOnlyList<Pair<ResourceDef, ExpantaNum>> relicCosts = gameManager.Relic.GetActiveCosts();
            for (int j = 0; j < relicCosts.Count; j++)
                resourceManager.AdjustTickPotentialConsumption(relicCosts[j].First, relicCosts[j].Second);
            gameManager.Sectors.AccumulateOccupiedResourcePotential(resourceManager);
            ExpantaNum happinessMultiplier = gameState.HappinessRewardMultiplier;
            for (int i = 0; i < activeBuildingStates.Count; i++)
            {
                BuildingState state = activeBuildingStates[i];
                ExpantaNum potentialScale =
                    state.Amount * GetPotentialEfficiency(state.Definition, laborRatio);
                ExpantaNum actualScale = state.Amount * state.Efficiency;
                ExpantaNum productionMultiplier =
                    modifiers.GetBuildingProductionMultiplier(state.Definition) *
                    modifiers.GlobalBuildingProductionMultiplier;
                potentialFoodProduction += actualScale * state.Definition.FoodProductionRate
                    * productionMultiplier * gameState.Events.ProductionMultiplier;
                potentialFoodConsumption += potentialScale * state.Definition.FoodConsumptionRate;
                if (state.Definition.PowerContribution > ExpantaNum.Zero)
                    potentialPowerProduction += actualScale * state.Definition.PowerContribution
                    * modifiers.PowerMultiplier
                    * modifiers.GetBuildingPowerProductionMultiplier(state.Definition)
                    * happinessMultiplier;
                else if (state.Definition.PowerContribution < ExpantaNum.Zero)
                    potentialPowerConsumption += potentialScale * -state.Definition.PowerContribution;
                if (state.Definition.LogisticsContribution > ExpantaNum.Zero)
                    potentialLogisticsProduction += actualScale * state.Definition.LogisticsContribution
                    * modifiers.GlobalLogisticsMultiplier
                    * modifiers.GetBuildingLogisticsProductionMultiplier(state.Definition)
                    * happinessMultiplier;
                else if (state.Definition.LogisticsContribution < ExpantaNum.Zero)
                    potentialLogisticsConsumption += potentialScale * -state.Definition.LogisticsContribution;

                IReadOnlyList<Pair<ResourceDef, ExpantaNum>> consumption =
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
            if (!RefreshEfficienciesCore(laborRatio, resourceManager, gameState))
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
        if (EnforceTickResourceBudget(resourceManager, deltaSeconds, laborRatio,
                potentialFoodConsumption + gameState.FoodPopulationConsumptionRate))
        {
            ExpantaNum powerDemand = gameManager.UltraProject.GetCurrentPowerConsumptionRate() +
                gameManager.Relic.CurrentPowerConsumptionRate;
            ExpantaNum logisticsDemand = gameManager.UltraProject.GetCurrentLogisticsConsumptionRate() +
                gameManager.Relic.CurrentLogisticsConsumptionRate;
            foreach (BuildingState state in activeBuildingStates)
            {
                ExpantaNum scale = state.Amount * GetPotentialEfficiency(state.Definition, laborRatio);
                powerDemand += scale * ExpantaNum.Max(ExpantaNum.Zero, -state.Definition.PowerContribution);
                logisticsDemand += scale * ExpantaNum.Max(ExpantaNum.Zero, -state.Definition.LogisticsContribution);
            }
            gameManager.PrepareFlowSatisfaction(
                gameState.PowerProductionRate * gameState.HappinessRewardMultiplier, powerDemand,
                gameState.LogisticsProductionRate * gameState.HappinessRewardMultiplier, logisticsDemand);
        }
        RefreshResearchPower();
    }

    private bool EnforceTickResourceBudget(ResourceManager resources, double seconds, ExpantaNum laborRatio,
        ExpantaNum foodDemand)
    {
        if (seconds <= 0d)
            return false;
        bool corrected = false;
        GameManager game = GameManager.Instance;
        ExpantaNum externalPower = game.UltraProject.GetCurrentPowerConsumptionRate() +
            game.Relic.CurrentPowerConsumptionRate;
        ExpantaNum externalLogistics = game.UltraProject.GetCurrentLogisticsConsumptionRate() +
            game.Relic.CurrentLogisticsConsumptionRate;
        // Only efficiencies change during correction; full-load demand stays fixed.
        ExpantaNum totalPowerDemand = GetBuildingFlowDemand(true, laborRatio) + externalPower;
        ExpantaNum totalLogisticsDemand = GetBuildingFlowDemand(false, laborRatio) + externalLogistics;
        // Bounded efficiency iteration can leave a lossy cycle or happiness feedback unfunded.
        for (int correction = 0; correction < resources.States.Count + 2; correction++)
        {
            game.State.SetFoodAvailability(game.CalculateSegmentFoodAvailability(
                game.State.FoodProductionRate, foodDemand, seconds));
            // Keep full-load and strategic demand while refreshing the corrected supply.
            resources.RefreshTickProduction();
            game.Sectors.AccumulateOccupiedResourcePotential(resources);
            resources.CalculateTickSatisfaction(seconds);
            ResourceDef deficit = null;
            foreach (ResourceState resource in resources.States.Values)
            {
                ExpantaNum available = resource.Amount + resource.TickPotentialProductionRate * seconds;
                if (resource.ConsumptionRate * seconds > available)
                {
                    deficit = resource.Definition;
                    break;
                }
            }
            bool powerChanges = !FitsFlowBudget(
                game.State.PowerProductionRate * game.State.HappinessRewardMultiplier,
                game.State.PowerConsumptionRate, externalPower, totalPowerDemand);
            bool logisticsChanges = !FitsFlowBudget(
                game.State.LogisticsProductionRate * game.State.HappinessRewardMultiplier,
                game.State.LogisticsConsumptionRate, externalLogistics, totalLogisticsDemand);
            if (deficit == null && !powerChanges && !logisticsChanges)
                return corrected;
            corrected = true;

            budgetBuildings.Clear();
            budgetResources.Clear();
            budgetProduction.Clear();
            budgetConsumption.Clear();
            budgetPowerProduction = budgetPowerConsumption = ExpantaNum.Zero;
            budgetLogisticsProduction = budgetLogisticsConsumption = ExpantaNum.Zero;
            if (deficit != null)
                budgetResources.Add(deficit);
            bool foodChanges = false;
            ProgressionModifierState modifiers = ProgressionModifierManager.Current;
            bool changed;
            do
            {
                changed = false;
                for (int i = 0; i < activeBuildingStates.Count; i++)
                {
                    BuildingState state = activeBuildingStates[i];
                    if (budgetBuildings.Contains(state) || state.Efficiency <= ExpantaNum.Zero)
                        continue;
                    BuildingDef definition = state.Definition;
                    bool affected = powerChanges && definition.PowerContribution < ExpantaNum.Zero ||
                        logisticsChanges && definition.LogisticsContribution < ExpantaNum.Zero;
                    IReadOnlyList<Pair<ResourceDef, ExpantaNum>> consumption = definition.ResourceConsumptionRates;
                    for (int j = 0; j < consumption.Count; j++)
                        affected |= consumption[j].Second > ExpantaNum.Zero &&
                            (foodChanges || budgetResources.Contains(consumption[j].First));
                    if (!affected)
                        continue;
                    budgetBuildings.Add(state);
                    changed = true;
                    for (int j = 0; j < consumption.Count; j++)
                        if (consumption[j].Second > ExpantaNum.Zero)
                            budgetResources.Add(consumption[j].First);
                    IReadOnlyList<Pair<ResourceDef, ExpantaNum>> generation = definition.ResourceGenerationRates;
                    for (int j = 0; j < generation.Count; j++)
                        if (generation[j].Second > ExpantaNum.Zero)
                            budgetResources.Add(generation[j].First);
                    ExpantaNum foodContribution = definition.FoodProductionRate *
                        modifiers.GetBuildingProductionMultiplier(definition) *
                        modifiers.GlobalBuildingProductionMultiplier * game.State.Events.ProductionMultiplier *
                        modifiers.GlobalFoodProductionMultiplier - definition.FoodConsumptionRate;
                    foodChanges |= foodContribution != ExpantaNum.Zero;
                    powerChanges |= definition.PowerContribution > ExpantaNum.Zero;
                    logisticsChanges |= definition.LogisticsContribution > ExpantaNum.Zero;
                }
            } while (changed);

            ExpantaNum foodNet = ExpantaNum.Zero;
            ExpantaNum foodProduction = ExpantaNum.Zero;
            foreach (BuildingState state in budgetBuildings)
            {
                BuildingDef definition = state.Definition;
                ExpantaNum scale = state.Amount * state.Efficiency;
                ExpantaNum multiplier = modifiers.GetBuildingProductionMultiplier(definition) *
                    modifiers.GlobalBuildingProductionMultiplier;
                IReadOnlyList<Pair<ResourceDef, ExpantaNum>> generation = definition.ResourceGenerationRates;
                for (int j = 0; j < generation.Count; j++)
                {
                    Pair<ResourceDef, ExpantaNum> rate = generation[j];
                    budgetProduction.TryGetValue(rate.First, out ExpantaNum previous);
                    budgetProduction[rate.First] = previous + scale * rate.Second * multiplier *
                        game.State.Events.ProductionMultiplier *
                        modifiers.GetBuildingResourceProductionMultiplier(definition, rate.First) *
                        modifiers.GetResourceProductionMultiplier(rate.First);
                }
                IReadOnlyList<Pair<ResourceDef, ExpantaNum>> consumption = definition.ResourceConsumptionRates;
                for (int j = 0; j < consumption.Count; j++)
                {
                    Pair<ResourceDef, ExpantaNum> rate = consumption[j];
                    budgetConsumption.TryGetValue(rate.First, out ExpantaNum previous);
                    budgetConsumption[rate.First] = previous + scale * rate.Second * multiplier;
                }
                foodNet += scale * (definition.FoodProductionRate * multiplier *
                    game.State.Events.ProductionMultiplier * modifiers.GlobalFoodProductionMultiplier -
                    definition.FoodConsumptionRate);
                foodProduction += scale * definition.FoodProductionRate * multiplier *
                    game.State.Events.ProductionMultiplier * modifiers.GlobalFoodProductionMultiplier;
                if (definition.PowerContribution > ExpantaNum.Zero)
                    budgetPowerProduction += scale * definition.PowerContribution * modifiers.PowerMultiplier *
                    modifiers.GetBuildingPowerProductionMultiplier(definition);
                else if (definition.PowerContribution < ExpantaNum.Zero)
                    budgetPowerConsumption += scale * -definition.PowerContribution;
                if (definition.LogisticsContribution > ExpantaNum.Zero)
                    budgetLogisticsProduction += scale * definition.LogisticsContribution *
                    modifiers.GlobalLogisticsMultiplier * modifiers.GetBuildingLogisticsProductionMultiplier(definition);
                else if (definition.LogisticsContribution < ExpantaNum.Zero)
                    budgetLogisticsConsumption += scale * -definition.LogisticsContribution;
            }

            ExpantaNum reward = game.State.HappinessRewardMultiplier;
            PrepareBudgetResourceInputs(resources, reward);
            // Settle material feedback first; the next pass reallocates flow to its consumers.
            bool reserveExternalFlow = deficit == null;
            ExpantaNum low = ExpantaNum.One;
            ExpantaNum high = ExpantaNum.One;
            ExpantaNum minimumReward = ExpantaNum.Min(reward,
                HappinessFormula.CalculateRewardMultiplier(HappinessFormula.CalculateMultiplier(
                    game.State.FoodNetRate - foodNet, game.State.Population.Population,
                    HappinessFormula.CalculateFoodAvailability(game.State.FoodAmount,
                        game.State.FoodProductionRate - foodProduction, foodDemand, seconds),
                    modifiers.HappinessBonus)));
            for (int i = 0; i < budgetResourceInputs.Count; i++)
            {
                BudgetResourceInput input = budgetResourceInputs[i];
                ResourceState resource = input.State;
                ExpantaNum available = resource.Amount / seconds + input.ExternalProduction +
                    (resource.ProductionRate - input.Production) * minimumReward -
                    (resource.ConsumptionRate - input.Consumption);
                ExpantaNum deficitRate = input.Consumption - input.Production * minimumReward;
                if (deficitRate > ExpantaNum.Zero)
                    low = ExpantaNum.Min(low, ExpantaNum.Clamp01(available / deficitRate));
            }
            low = LimitFlowBudget(low, game.State.PowerProductionRate, game.State.PowerConsumptionRate,
                budgetPowerProduction, budgetPowerConsumption,
                minimumReward * AvailableBuildingFlowShare(reserveExternalFlow ? externalPower : ExpantaNum.Zero, totalPowerDemand));
            low = LimitFlowBudget(low, game.State.LogisticsProductionRate, game.State.LogisticsConsumptionRate,
                budgetLogisticsProduction, budgetLogisticsConsumption,
                minimumReward * AvailableBuildingFlowShare(reserveExternalFlow ? externalLogistics : ExpantaNum.Zero, totalLogisticsDemand));
            if (!FitsTickResourceBudget(seconds, low, foodNet, foodProduction, foodDemand, reserveExternalFlow, totalPowerDemand, totalLogisticsDemand))
                low *= new ExpantaNum(0.99999d);
            if (!FitsTickResourceBudget(seconds, low, foodNet, foodProduction, foodDemand, reserveExternalFlow, totalPowerDemand, totalLogisticsDemand))
                low = ExpantaNum.Zero;
            // Retain only a feasible lower bound; do not assume feedback is linear or converged.
            for (int step = 0; step < 36; step++)
            {
                ExpantaNum middle = (low + high) * 0.5d;
                if (middle <= low || middle >= high)
                    break;
                if (FitsTickResourceBudget(seconds, middle, foodNet, foodProduction, foodDemand, reserveExternalFlow, totalPowerDemand, totalLogisticsDemand))
                    low = middle;
                else
                    high = middle;
            }
            foreach (BuildingState state in budgetBuildings)
            {
                ExpantaNum efficiency = state.Efficiency * low;
                ExpantaNum scaleDelta = state.Amount * efficiency - state.Amount * state.Efficiency;
                ApplyGameRateDelta(state, scaleDelta, ExpantaNum.Zero);
                state.SetEfficiency(efficiency);
                researchPowerDirty = true;
            }
            // Re-sum positive contributions to retain legitimate rates below cancellation precision.
            resources.ResetBuildingRates();
            foreach (BuildingState state in activeBuildingStates)
                ApplyResourceRateDelta(state, state.Amount * state.Efficiency);
        }
        game.State.SetFoodAvailability(game.CalculateSegmentFoodAvailability(
            game.State.FoodProductionRate, foodDemand, seconds));
        resources.RefreshTickProduction();
        game.Sectors.AccumulateOccupiedResourcePotential(resources);
        resources.CalculateTickSatisfaction(seconds);
        return corrected;
    }

    private static ExpantaNum AvailableBuildingFlowShare(ExpantaNum external, ExpantaNum totalDemand)
    {
        return totalDemand > ExpantaNum.Zero
            ? ExpantaNum.Clamp01(ExpantaNum.One - external / totalDemand)
            : ExpantaNum.One;
    }

    private static bool FitsFlowBudget(ExpantaNum supply, ExpantaNum buildingConsumption,
        ExpantaNum external, ExpantaNum totalDemand)
    {
        // Strategic work uses the same global satisfaction, before any material constraint.
        return buildingConsumption + external * GameManager.CalculateFlowSatisfaction(supply, totalDemand) <= supply;
    }

    private static ExpantaNum LimitFlowBudget(ExpantaNum scale, ExpantaNum production,
        ExpantaNum consumption, ExpantaNum selectedProduction, ExpantaNum selectedConsumption,
        ExpantaNum reward)
    {
        ExpantaNum deficit = selectedConsumption - selectedProduction * reward;
        return deficit > ExpantaNum.Zero
            ? ExpantaNum.Min(scale, ExpantaNum.Clamp01(
                ((production - selectedProduction) * reward - (consumption - selectedConsumption)) / deficit))
            : scale;
    }

    private void PrepareBudgetResourceInputs(ResourceManager resources, ExpantaNum reward)
    {
        budgetResourceInputs.Clear();
        // Preserve resource order; these inputs stay fixed throughout this correction's search.
        foreach (ResourceState resource in resources.States.Values)
        {
            if (!budgetResources.Contains(resource.Definition))
                continue;
            budgetProduction.TryGetValue(resource.Definition, out ExpantaNum production);
            budgetConsumption.TryGetValue(resource.Definition, out ExpantaNum consumption);
            ExpantaNum external = resource.TickPotentialProductionRate - resource.ProductionRate * reward;
            budgetResourceInputs.Add(new BudgetResourceInput(resource, production, consumption, external));
        }
    }

    private bool FitsTickResourceBudget(double seconds,
        ExpantaNum scale, ExpantaNum foodNet, ExpantaNum foodProduction,
        ExpantaNum foodDemand, bool reserveExternalFlow,
        ExpantaNum totalPowerDemand, ExpantaNum totalLogisticsDemand)
    {
        GameState game = GameManager.Instance.State;
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        candidateProduction.Clear();
        candidateConsumption.Clear();
        ExpantaNum candidateFood = ExpantaNum.Zero;
        ExpantaNum candidateFoodProduction = ExpantaNum.Zero;
        ExpantaNum candidatePowerProduction = ExpantaNum.Zero, candidatePowerConsumption = ExpantaNum.Zero;
        ExpantaNum candidateLogisticsProduction = ExpantaNum.Zero, candidateLogisticsConsumption = ExpantaNum.Zero;
        foreach (BuildingState state in budgetBuildings)
        {
            BuildingDef definition = state.Definition;
            // Quantize efficiency before rates, exactly as the committed State does.
            ExpantaNum candidateScale = state.Amount * (state.Efficiency * scale);
            ExpantaNum multiplier = modifiers.GetBuildingProductionMultiplier(definition) *
                modifiers.GlobalBuildingProductionMultiplier;
            IReadOnlyList<Pair<ResourceDef, ExpantaNum>> generation = definition.ResourceGenerationRates;
            for (int j = 0; j < generation.Count; j++)
            {
                Pair<ResourceDef, ExpantaNum> rate = generation[j];
                candidateProduction.TryGetValue(rate.First, out ExpantaNum previous);
                candidateProduction[rate.First] = previous + candidateScale * rate.Second * multiplier *
                    game.Events.ProductionMultiplier *
                    modifiers.GetBuildingResourceProductionMultiplier(definition, rate.First) *
                    modifiers.GetResourceProductionMultiplier(rate.First);
            }
            IReadOnlyList<Pair<ResourceDef, ExpantaNum>> consumption = definition.ResourceConsumptionRates;
            for (int j = 0; j < consumption.Count; j++)
            {
                Pair<ResourceDef, ExpantaNum> rate = consumption[j];
                candidateConsumption.TryGetValue(rate.First, out ExpantaNum previous);
                candidateConsumption[rate.First] = previous + candidateScale * rate.Second * multiplier;
            }
            candidateFood += candidateScale * (definition.FoodProductionRate * multiplier *
                game.Events.ProductionMultiplier * modifiers.GlobalFoodProductionMultiplier - definition.FoodConsumptionRate);
            candidateFoodProduction += candidateScale * definition.FoodProductionRate * multiplier *
                game.Events.ProductionMultiplier * modifiers.GlobalFoodProductionMultiplier;
            if (definition.PowerContribution > ExpantaNum.Zero)
                candidatePowerProduction += candidateScale * definition.PowerContribution * modifiers.PowerMultiplier *
                modifiers.GetBuildingPowerProductionMultiplier(definition);
            else if (definition.PowerContribution < ExpantaNum.Zero)
                candidatePowerConsumption += candidateScale * -definition.PowerContribution;
            if (definition.LogisticsContribution > ExpantaNum.Zero)
                candidateLogisticsProduction += candidateScale * definition.LogisticsContribution *
                modifiers.GlobalLogisticsMultiplier * modifiers.GetBuildingLogisticsProductionMultiplier(definition);
            else if (definition.LogisticsContribution < ExpantaNum.Zero)
                candidateLogisticsConsumption += candidateScale * -definition.LogisticsContribution;
        }
        ExpantaNum reward = HappinessFormula.CalculateRewardMultiplier(
            HappinessFormula.CalculateMultiplier(game.FoodNetRate - foodNet + candidateFood,
                game.Population.Population,
                GameManager.Instance.CalculateSegmentFoodAvailability(
                    game.FoodProductionRate - foodProduction + candidateFoodProduction, foodDemand, seconds),
                ProgressionModifierManager.Current.HappinessBonus));
        for (int i = 0; i < budgetResourceInputs.Count; i++)
        {
            BudgetResourceInput input = budgetResourceInputs[i];
            ResourceState resource = input.State;
            candidateProduction.TryGetValue(resource.Definition, out ExpantaNum newProduction);
            candidateConsumption.TryGetValue(resource.Definition, out ExpantaNum newConsumption);
            ExpantaNum available = resource.Amount + (input.ExternalProduction +
                (resource.ProductionRate - input.Production + newProduction) * reward) * seconds;
            ExpantaNum demand = (resource.ConsumptionRate - input.Consumption + newConsumption) * seconds;
            if (demand > available)
                return false;
        }
        GameManager manager = GameManager.Instance;
        return FitsFlowBudget(
                (game.PowerProductionRate - budgetPowerProduction + candidatePowerProduction) * reward,
                game.PowerConsumptionRate - budgetPowerConsumption + candidatePowerConsumption,
                reserveExternalFlow ? manager.UltraProject.GetCurrentPowerConsumptionRate() + manager.Relic.CurrentPowerConsumptionRate : ExpantaNum.Zero,
                totalPowerDemand) &&
            FitsFlowBudget(
                (game.LogisticsProductionRate - budgetLogisticsProduction + candidateLogisticsProduction) * reward,
                game.LogisticsConsumptionRate - budgetLogisticsConsumption + candidateLogisticsConsumption,
                reserveExternalFlow ? manager.UltraProject.GetCurrentLogisticsConsumptionRate() + manager.Relic.CurrentLogisticsConsumptionRate : ExpantaNum.Zero,
                totalLogisticsDemand);
    }

    private ExpantaNum CalculateEfficiency(
        BuildingDef building,
        ResourceManager resourceManager,
        GameState gameState,
        ExpantaNum laborRatio)
    {
        ExpantaNum resourceSatisfaction = ExpantaNum.One;
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> rates = building.ResourceConsumptionRates;
        for (int i = 0; i < rates.Count; i++)
            resourceSatisfaction = ExpantaNum.Min(
                resourceSatisfaction,
                resourceManager.GetTickSatisfaction(rates[i].First));

        ExpantaNum powerSatisfaction = building.PowerContribution < ExpantaNum.Zero
            ? gameState.PowerSatisfaction
            : ExpantaNum.One;
        ExpantaNum logisticsSatisfaction = building.LogisticsContribution < ExpantaNum.Zero
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
            GetPotentialEfficiency(building, laborRatio),
            resourceSatisfaction,
            happinessConstraint,
            powerSatisfaction,
            logisticsSatisfaction);
    }

    public static bool IsFoodConstraintRequired(BuildingDef building)
    {
        if (building == null)
            return false;
        if (building.FoodProductionRate > ExpantaNum.Zero)
            return false;
        return building.PowerContribution >= ExpantaNum.Zero ||
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

    public bool SetSectorBuildingEnabled(SectorBuildingDef building, bool enabled)
    {
        if (building == null || !TryGetStateByStableId(building, out BuildingState state) ||
            state.Definition is not SectorBuildingDef canonical || state.Amount <= ExpantaNum.Zero)
            return false;
        building = canonical;
        if (state.IsEnabled == enabled)
            return true;
        ExpantaNum efficiency = enabled
            ? CalculateEfficiency(building, ResourceManager.Instance, GameManager.Instance.State, LaborOperatingRatio)
            : ExpantaNum.Zero;
        ApplyRateDelta(state, state.Amount, state.Efficiency, state.Amount, efficiency);
        state.SetEnabled(enabled);
        state.SetEfficiency(efficiency);
        UpdateActiveBuildingIndex(state, state.Amount);
        researchPowerDirty = true;
        RefreshEfficiencies();
        return true;
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
        bool shouldBeActive = state.IsEnabled && amount > ExpantaNum.Zero;
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
        ApplyResourceRateDelta(state, scaleDelta);
        ApplyGameRateDelta(state, scaleDelta, amountDelta, applyCapacityDeltas);
    }

    private static void ApplyGameRateDelta(BuildingState state, ExpantaNum scaleDelta,
        ExpantaNum amountDelta, bool applyCapacityDeltas = true)
    {
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        ExpantaNum productionMultiplier =
            modifiers.GetBuildingProductionMultiplier(state.Definition) *
            modifiers.GlobalBuildingProductionMultiplier;

        GameManager gameManager = GameManager.Instance;
        gameManager.AdjustFoodRates(
            scaleDelta * state.Definition.FoodProductionRate
                * productionMultiplier * gameManager.State.Events.ProductionMultiplier,
            scaleDelta * state.Definition.FoodConsumptionRate);
        if (applyCapacityDeltas)
        {
            gameManager.AdjustFoodCapacity(
                amountDelta * state.Definition.FoodCapacityGranted
                    * modifiers.FoodCapacityMultiplier);
            gameManager.AdjustPopulationCapacity(
                amountDelta * state.Definition.PopulationCapacityGranted);
        }
        gameManager.AdjustPowerRates(
            scaleDelta * ExpantaNum.Max(ExpantaNum.Zero, state.Definition.PowerContribution)
                * modifiers.PowerMultiplier
                * modifiers.GetBuildingPowerProductionMultiplier(state.Definition),
            scaleDelta * ExpantaNum.Max(ExpantaNum.Zero, -state.Definition.PowerContribution));
        gameManager.AdjustLogisticsRates(
            scaleDelta * ExpantaNum.Max(ExpantaNum.Zero, state.Definition.LogisticsContribution)
                * modifiers.GlobalLogisticsMultiplier
                * modifiers.GetBuildingLogisticsProductionMultiplier(state.Definition),
            scaleDelta * ExpantaNum.Max(ExpantaNum.Zero, -state.Definition.LogisticsContribution));
        if (state.Definition.TechLevel >= TechLevel.Spacer)
        {
            gameManager.AdjustFleetPower(scaleDelta * state.Definition.FleetPowerGranted);
            gameManager.AdjustAttackPower(scaleDelta * state.Definition.AttackPowerGranted);
            gameManager.AdjustDefensePower(scaleDelta * state.Definition.DefensePowerGranted);
            gameManager.AdjustMilitaryManpower(scaleDelta * state.Definition.MilitaryManpowerGranted);
        }
    }

    private static void ApplyResourceRateDelta(BuildingState state, ExpantaNum scaleDelta)
    {
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        ExpantaNum productionMultiplier = modifiers.GetBuildingProductionMultiplier(state.Definition) *
            modifiers.GlobalBuildingProductionMultiplier;
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> generation = state.Definition.ResourceGenerationRates;
        ResourceManager resourceManager = null;
        for (int i = 0; i < generation.Count; i++)
        {
            if (resourceManager == null)
                resourceManager = ResourceManager.Instance;
            resourceManager.AdjustProductionRate(
                generation[i].First,
                scaleDelta * generation[i].Second
                * productionMultiplier
                * GameManager.Instance.State.Events.ProductionMultiplier
                * modifiers.GetBuildingResourceProductionMultiplier(
                    state.Definition, generation[i].First)
                * modifiers.GetResourceProductionMultiplier(generation[i].First));
        }

        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> consumption = state.Definition.ResourceConsumptionRates;
        for (int i = 0; i < consumption.Count; i++)
        {
            if (resourceManager == null)
                resourceManager = ResourceManager.Instance;
            resourceManager.AdjustConsumptionRate(
                consumption[i].First,
                scaleDelta * consumption[i].Second * productionMultiplier);
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
            // Storage is physical space: staff shortages stop production, but
            // do not destroy the granary or the food already stored inside it.
            GameManager.Instance.AdjustFoodCapacity(
                state.Amount * state.Definition.FoodCapacityGranted
                * (current.FoodCapacityMultiplier - previous.FoodCapacityMultiplier));
            ExpantaNum scale = state.Amount * state.Efficiency;
            if (scale <= ExpantaNum.Zero)
                continue;

            BuildingDef building = state.Definition;
            ExpantaNum oldBuildingMultiplier =
                previous.GetBuildingProductionMultiplier(building) *
                previous.GlobalBuildingProductionMultiplier;
            ExpantaNum newBuildingMultiplier =
                current.GetBuildingProductionMultiplier(building) *
                current.GlobalBuildingProductionMultiplier;

            IReadOnlyList<Pair<ResourceDef, ExpantaNum>> generation =
                building.ResourceGenerationRates;
            for (int j = 0; j < generation.Count; j++)
            {
                Pair<ResourceDef, ExpantaNum> rate = generation[j];
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
                    scale * rate.Second * (newMultiplier - oldMultiplier)
                    * GameManager.Instance.State.Events.ProductionMultiplier);
            }

            IReadOnlyList<Pair<ResourceDef, ExpantaNum>> consumption =
                building.ResourceConsumptionRates;
            for (int j = 0; j < consumption.Count; j++)
                ResourceManager.Instance.AdjustConsumptionRate(
                    consumption[j].First,
                    scale * consumption[j].Second
                    * (newBuildingMultiplier - oldBuildingMultiplier));

            GameManager.Instance.AdjustFoodRates(
                scale * building.FoodProductionRate * (newBuildingMultiplier - oldBuildingMultiplier)
                * GameManager.Instance.State.Events.ProductionMultiplier, ExpantaNum.Zero);
            ExpantaNum oldPower =
                previous.PowerMultiplier * previous.GetBuildingPowerProductionMultiplier(building);
            ExpantaNum newPower =
                current.PowerMultiplier * current.GetBuildingPowerProductionMultiplier(building);
            GameManager.Instance.AdjustPowerRates(
                scale * ExpantaNum.Max(ExpantaNum.Zero, building.PowerContribution) * (newPower - oldPower),
                ExpantaNum.Zero);
            ExpantaNum oldLogistics =
                previous.GlobalLogisticsMultiplier *
                previous.GetBuildingLogisticsProductionMultiplier(building);
            ExpantaNum newLogistics =
                current.GlobalLogisticsMultiplier *
                current.GetBuildingLogisticsProductionMultiplier(building);
            GameManager.Instance.AdjustLogisticsRates(
                scale * ExpantaNum.Max(ExpantaNum.Zero, building.LogisticsContribution) * (newLogistics - oldLogistics),
                ExpantaNum.Zero);
        }

        GameManager.Instance.AdjustTerritoryTotal(
            current.TerritoryGranted - previous.TerritoryGranted);
        RefreshResearchPower();
    }

    // Events affect building outputs only: inputs, housing, storage and flow remain intact.
    internal void ApplyEventProductionChange(ExpantaNum previous, ExpantaNum current)
    {
        ExpantaNum delta = current - previous;
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        for (int i = 0; i < orderedStates.Count; i++)
        {
            BuildingState state = orderedStates[i];
            BuildingDef building = state.Definition;
            ExpantaNum scale = state.Amount * state.Efficiency * delta *
                modifiers.GetBuildingProductionMultiplier(building) *
                modifiers.GlobalBuildingProductionMultiplier;
            if (scale == ExpantaNum.Zero)
                continue;
            IReadOnlyList<Pair<ResourceDef, ExpantaNum>> generation = building.ResourceGenerationRates;
            for (int j = 0; j < generation.Count; j++)
                ResourceManager.Instance.AdjustProductionRate(generation[j].First,
                    scale * generation[j].Second *
                    modifiers.GetBuildingResourceProductionMultiplier(building, generation[j].First) *
                    modifiers.GetResourceProductionMultiplier(generation[j].First));
            GameManager.Instance.AdjustFoodRates(scale * building.FoodProductionRate, ExpantaNum.Zero);
        }
    }

    private void ResetEfficienciesForTick(ExpantaNum laborRatio)
    {
        for (int i = 0; i < activeBuildingStates.Count; i++)
        {
            BuildingState state = activeBuildingStates[i];
            ExpantaNum startingEfficiency = GetPotentialEfficiency(state.Definition, laborRatio);
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

    private static void EnsureBuildingResources(BuildingDef building)
    {
        ResourceManager resourceManager = null;
        EnsureResources(building.ResourceRequirements, ref resourceManager);
        EnsureResources(building.ResourceGenerationRates, ref resourceManager);
        EnsureResources(building.ResourceConsumptionRates, ref resourceManager);
    }

    private static void EnsureResources(
        IReadOnlyList<Pair<ResourceDef, ExpantaNum>> pairs,
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

            if (state.Definition is not SectorBuildingDef)
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
                Disabled = !state.IsEnabled,
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
            var restoredBuildings = new HashSet<BuildingDef>();
            var restoredAmounts = new Dictionary<BuildingDef, ExpantaNum>();
            var disabledBuildings = new HashSet<BuildingDef>();
            for (int i = 0; i < data.Buildings.Count; i++)
            {
                SaveManager.BuildingStateSaveData saved = data.Buildings[i];
                if (!DataBase<BuildingDef>.TryFind(saved.BuildingId, out BuildingDef definition))
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
                if (saved.Disabled)
                {
                    if (definition is not SectorBuildingDef)
                        throw new InvalidOperationException("Only sector facilities can be disabled.");
                    disabledBuildings.Add(definition);
                }
            }

            ExpantaNum restoredGlobalEfficiencyFactor = Parse(
                data.GlobalEfficiencyFactor,
                nameof(BuildingManager),
                nameof(data.GlobalEfficiencyFactor));
            if (restoredGlobalEfficiencyFactor < ExpantaNum.Zero)
                throw new InvalidOperationException("Building global efficiency cannot be negative.");
            foreach (KeyValuePair<BuildingDef, ExpantaNum> entry in restoredAmounts)
                EnsureBuilding(entry.Key).Restore(entry.Value, disabledBuildings.Contains(entry.Key));
            GlobalEfficiencyFactor = restoredGlobalEfficiencyFactor;
        }
        else
        {
            GlobalEfficiencyFactor = Parse(
                data.GlobalEfficiencyFactor,
                nameof(BuildingManager),
                nameof(data.GlobalEfficiencyFactor));
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
            if (state.Definition is not SectorBuildingDef sectorBuilding)
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
            if (state.Amount > ExpantaNum.Zero && sectorBuilding.RequiresInteriorExploration &&
                !sectors.GetState(sectorBuilding.Sector).InteriorExplorationCompleted)
                throw new InvalidOperationException($"Sector building '{sectorBuilding.Id}' requires completed planetary exploration.");
        }
    }

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
            if (state.IsEnabled && state.Amount > ExpantaNum.Zero)
            {
                activeBuildingStates.Add(state);
                activeBuildingStateSet.Add(state);
            }
        }
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

}
