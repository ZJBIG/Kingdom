using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Kingdom.NewEconomySimulator;

public sealed class BuildingRuntimeState
{
    public BuildingRuntimeState(string id, ExpantaNum amount, ExpantaNum efficiency)
    {
        Id = RequireId(id);
        Amount = amount;
        Efficiency = efficiency;
    }

    public string Id { get; }
    public ExpantaNum Amount { get; internal set; }
    public ExpantaNum Efficiency { get; internal set; }

    private static string RequireId(string id) => string.IsNullOrWhiteSpace(id)
        ? throw new ArgumentException("Building ID is required.", nameof(id))
        : id;
}

public sealed class ResearchRuntimeState
{
    public ResearchRuntimeState(string id)
    {
        Id = string.IsNullOrWhiteSpace(id)
            ? throw new ArgumentException("Research ID is required.", nameof(id))
            : id;
        PaidResourceCosts = new SortedDictionary<string, ExpantaNum>(StringComparer.Ordinal);
    }

    public string Id { get; }
    public ExpantaNum Progress { get; internal set; }
    public bool CostPaid { get; internal set; }
    public bool Completed { get; internal set; }
    public SortedDictionary<string, ExpantaNum> PaidResourceCosts { get; }
}

public sealed class SectorRuntimeState
{
    public SectorRuntimeState(string id)
    {
        Id = string.IsNullOrWhiteSpace(id)
            ? throw new ArgumentException("Sector ID is required.", nameof(id))
            : id;
    }

    public string Id { get; }
    public bool Unlocked { get; internal set; }
    public bool Occupied { get; internal set; }
    public bool ColonizationActive { get; internal set; }
    public bool CampaignActive { get; internal set; }
    public ExpantaNum Progress { get; internal set; }
    public ExpantaNum Casualties { get; internal set; }
    public ExpantaNum CombatRatio { get; internal set; }
    public int VisitCount { get; internal set; }
}

public sealed class SimulationState
{
    private const double SecondsPerDay = 10d;
    private static readonly ExpantaNum PopulationStepEpsilon = new(1e-9d);
    private static readonly ExpantaNum PopulationRemainderEpsilon = new(1e-3d);
    private static readonly ExpantaNum FoodShortageDepartureSeconds = new(3600d);

    private readonly SortedDictionary<string, ExpantaNum> resources = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, ExpantaNum> buildings = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, ExpantaNum> workshops = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, string> research = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, string> sectors = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, BuildingRuntimeState> buildingStates = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, ResearchRuntimeState> researchStates = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, SectorRuntimeState> sectorStates = new(StringComparer.Ordinal);
    private readonly SortedSet<string> purchasedWorkshops = new(StringComparer.Ordinal);
    private readonly Queue<string> researchQueue = new();
    private double calendarElapsedSeconds;

    public SimulationEventLog? Events { get; internal set; }

    public long Tick { get; private set; }
    public double ElapsedSeconds { get; private set; }
    public double RequestedSeconds { get; private set; }
    public string TechLevel { get; private set; } = "Animal";
    public int CalendarDays { get; private set; }
    public ExpantaNum Food { get; private set; }
    public ExpantaNum FoodCapacity { get; private set; }
    public ExpantaNum Population { get; private set; }
    public ExpantaNum PopulationCapacity { get; private set; }
    public ExpantaNum PopulationChangeProgress { get; private set; }
    public ExpantaNum TerritoryTotal { get; private set; }
    public ExpantaNum AttackPower { get; private set; }
    public ExpantaNum DefensePower { get; private set; }
    public ExpantaNum FleetPower { get; private set; }
    public ExpantaNum MilitaryManpower { get; private set; }
    public ExpantaNum SupplySatisfaction { get; private set; } = ExpantaNum.One;
    public ExpantaNum PowerSatisfaction { get; private set; } = ExpantaNum.One;
    public ExpantaNum LogisticsSatisfaction { get; private set; } = ExpantaNum.One;
    public bool CampaignActive { get; private set; }
    public string CampaignTargetSectorId { get; private set; } = "";
    public ExpantaNum CampaignProgress { get; private set; }
    public ExpantaNum CampaignCasualties { get; private set; }
    public ExpantaNum CampaignCombatRatio { get; private set; }
    public string? ActiveResearch { get; private set; }
    public long ProgressionVersion { get; private set; }

    public SimulationState(string food = "0", string foodCapacity = "1")
        : this(ParseAmount(food, nameof(food)), ParseAmount(foodCapacity, nameof(foodCapacity)))
    {
    }

    public SimulationState(ExpantaNum food, ExpantaNum foodCapacity)
    {
        RequireNonNegative(food, nameof(food));
        RequireFoodCapacity(foodCapacity, nameof(foodCapacity));
        FoodCapacity = foodCapacity;
        Food = ExpantaNum.Min(food, FoodCapacity);
    }

    public IReadOnlyDictionary<string, ExpantaNum> Resources => resources;
    public IReadOnlyDictionary<string, ExpantaNum> Buildings => buildings;
    public IReadOnlyDictionary<string, ExpantaNum> Workshops => workshops;
    public IReadOnlyDictionary<string, string> Research => research;
    public IReadOnlyDictionary<string, string> Sectors => sectors;
    public IReadOnlyDictionary<string, BuildingRuntimeState> BuildingStates => buildingStates;
    public IReadOnlyDictionary<string, ResearchRuntimeState> ResearchStates => researchStates;
    public IReadOnlyDictionary<string, SectorRuntimeState> SectorStates => sectorStates;
    public IReadOnlySet<string> PurchasedWorkshops => purchasedWorkshops;
    public IReadOnlyCollection<string> ResearchQueue => researchQueue.ToArray();

    public void RestoreClock(long tick, double elapsedSeconds, double requestedSeconds, int calendarDays)
    {
        if (tick < 0 || calendarDays < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));
        ValidateDelta(elapsedSeconds, nameof(elapsedSeconds));
        ValidateDelta(requestedSeconds, nameof(requestedSeconds));
        Tick = tick;
        ElapsedSeconds = elapsedSeconds;
        RequestedSeconds = requestedSeconds;
        CalendarDays = calendarDays;
        calendarElapsedSeconds = 0d;
    }

    public void AdvanceClock(double effectiveSeconds, double requestedSeconds)
    {
        ValidateDelta(effectiveSeconds, nameof(effectiveSeconds));
        ValidateDelta(requestedSeconds, nameof(requestedSeconds));
        ElapsedSeconds += effectiveSeconds;
        RequestedSeconds += requestedSeconds;
        calendarElapsedSeconds += requestedSeconds;
        while (calendarElapsedSeconds >= SecondsPerDay)
        {
            CalendarDays++;
            calendarElapsedSeconds -= SecondsPerDay;
        }
        Tick = checked(Tick + 1);
    }

    public void SetFood(string amount) => SetFood(ParseAmount(amount, nameof(amount)));

    public void SetFood(ExpantaNum amount)
    {
        RequireNonNegative(amount, nameof(amount));
        Food = ExpantaNum.Min(amount, FoodCapacity);
    }

    public void SetFoodCapacity(string capacity) =>
        SetFoodCapacity(ParseAmount(capacity, nameof(capacity)));

    public void SetFoodCapacity(ExpantaNum capacity)
    {
        RequireFoodCapacity(capacity, nameof(capacity));
        FoodCapacity = capacity;
        Food = ExpantaNum.Min(Food, FoodCapacity);
    }

    public void SetTechLevel(string techLevel)
    {
        TechLevel = string.IsNullOrWhiteSpace(techLevel)
            ? throw new ArgumentException("Tech level is required.", nameof(techLevel))
            : techLevel;
        ProgressionVersion++;
    }

    public void SetPopulation(ExpantaNum population)
    {
        RequireNonNegative(population, nameof(population));
        Population = population.Floor();
    }

    public void SetPopulationCapacity(ExpantaNum capacity)
    {
        RequireNonNegative(capacity, nameof(capacity));
        PopulationCapacity = capacity.Floor();
    }

    public void SetTerritoryTotal(ExpantaNum territory)
    {
        RequireNonNegative(territory, nameof(territory));
        TerritoryTotal = territory;
    }

    public void SetMilitary(ExpantaNum attack, ExpantaNum defense, ExpantaNum fleet, ExpantaNum manpower)
    {
        RequireNonNegative(attack, nameof(attack));
        RequireNonNegative(defense, nameof(defense));
        RequireNonNegative(fleet, nameof(fleet));
        RequireNonNegative(manpower, nameof(manpower));
        AttackPower = attack;
        DefensePower = defense;
        FleetPower = fleet;
        MilitaryManpower = manpower;
    }

    public void SetFlowSatisfaction(ExpantaNum supply, ExpantaNum power, ExpantaNum logistics)
    {
        SupplySatisfaction = ExpantaNum.Clamp01(supply);
        PowerSatisfaction = ExpantaNum.Clamp01(power);
        LogisticsSatisfaction = ExpantaNum.Clamp01(logistics);
    }

    public ExpantaNum GetResource(string id) => resources.TryGetValue(RequireId(id), out var value)
        ? value
        : ExpantaNum.Zero;

    public void SetResource(string id, string amount) =>
        SetResource(id, ParseAmount(amount, nameof(amount)));

    public void SetResource(string id, ExpantaNum amount)
    {
        RequireNonNegative(amount, nameof(amount));
        resources[RequireId(id)] = amount;
    }

    public void AddResource(string id, string amount) =>
        AddResource(id, ParseAmount(amount, nameof(amount)));

    public void AddResource(string id, ExpantaNum amount)
    {
        var key = RequireId(id);
        if (!amount.IsFinite)
            throw new ArgumentOutOfRangeException(nameof(amount), "Simulation amounts must be finite.");
        var next = GetResource(key) + amount;
        RequireNonNegative(next, nameof(amount));
        resources[key] = next;
    }

    public void ConsumeResource(string id, ExpantaNum amount)
    {
        RequireNonNegative(amount, nameof(amount));
        var key = RequireId(id);
        resources[key] = ExpantaNum.Max(ExpantaNum.Zero, GetResource(key) - amount);
    }

    public void SetBuilding(string id, ExpantaNum amount)
    {
        RequireNonNegative(amount, nameof(amount));
        var key = RequireId(id);
        buildings[key] = amount;
        if (buildingStates.TryGetValue(key, out var state)) state.Amount = amount;
        else buildingStates[key] = new BuildingRuntimeState(key, amount, ExpantaNum.One);
    }

    public void SetBuildingEfficiency(string id, ExpantaNum efficiency)
    {
        if (!efficiency.IsFinite || efficiency < ExpantaNum.Zero)
            throw new ArgumentOutOfRangeException(nameof(efficiency));
        if (!buildingStates.TryGetValue(RequireId(id), out var state))
            throw new KeyNotFoundException($"Unknown building state '{id}'.");
        state.Efficiency = ExpantaNum.Clamp01(efficiency);
    }

    public void SetWorkshop(string id, ExpantaNum amount)
    {
        RequireNonNegative(amount, nameof(amount));
        var key = RequireId(id);
        workshops[key] = amount;
        var purchased = amount > ExpantaNum.Zero;
        if (purchased) ProgressionVersion += purchasedWorkshops.Add(key) ? 1L : 0L;
        else ProgressionVersion += purchasedWorkshops.Remove(key) ? 1L : 0L;
    }

    public void SetResearch(string id, string status)
    {
        var key = RequireId(id);
        research[key] = status ?? "";
        if (!researchStates.TryGetValue(key, out var state))
            researchStates[key] = state = new ResearchRuntimeState(key);
        state.Completed = string.Equals(status, "Completed", StringComparison.Ordinal);
    }

    public ResearchRuntimeState GetResearchState(string id)
    {
        var key = RequireId(id);
        return researchStates.TryGetValue(key, out var state)
            ? state
            : throw new KeyNotFoundException($"Unknown research state '{id}'.");
    }

    public void SetResearchState(
        string id,
        ExpantaNum progress,
        bool costPaid,
        bool completed,
        IReadOnlyDictionary<string, ExpantaNum>? paidCosts = null)
    {
        RequireNonNegative(progress, nameof(progress));
        var key = RequireId(id);
        if (!researchStates.TryGetValue(key, out var state))
            researchStates[key] = state = new ResearchRuntimeState(key);
        state.Progress = progress;
        state.CostPaid = costPaid;
        ProgressionVersion += state.Completed == completed ? 0L : 1L;
        state.Completed = completed;
        var orderedPaidCosts = paidCosts ?? new Dictionary<string, ExpantaNum>(StringComparer.Ordinal);
        var paidCostArray = orderedPaidCosts.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
        state.PaidResourceCosts.Clear();
        foreach (var cost in paidCostArray)
        {
            RequireNonNegative(cost.Value, nameof(paidCosts));
            state.PaidResourceCosts[cost.Key] = cost.Value;
        }
        research[key] = completed ? "Completed" : costPaid ? "Researching" : "Available";
    }

    public void SetActiveResearch(string? id)
    {
        ActiveResearch = string.IsNullOrWhiteSpace(id) ? null : id;
    }

    public void SetResearchQueue(IEnumerable<string> ids)
    {
        researchQueue.Clear();
        foreach (var id in (ids ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)))
            researchQueue.Enqueue(id);
    }

    public void EnqueueResearch(string id)
    {
        var key = RequireId(id);
        if (researchQueue.Contains(key) || string.Equals(ActiveResearch, key, StringComparison.Ordinal))
            return;
        researchQueue.Enqueue(key);
    }

    public string? DequeueResearch()
    {
        if (researchQueue.Count == 0) return null;
        var id = researchQueue.Dequeue();
        var remaining = researchQueue.Where(item => !string.Equals(item, id, StringComparison.Ordinal)).ToArray();
        SetResearchQueue(remaining);
        return id;
    }

    public void SetSector(string id, string status) => sectors[RequireId(id)] = status ?? "";

    public SectorRuntimeState GetSectorState(string id)
    {
        var key = RequireId(id);
        return sectorStates.TryGetValue(key, out var state)
            ? state
            : throw new KeyNotFoundException($"Unknown sector state '{id}'.");
    }

    public void SetSectorState(
        string id,
        bool unlocked,
        bool occupied,
        bool colonizationActive,
        bool campaignActive,
        ExpantaNum progress,
        ExpantaNum casualties,
        ExpantaNum combatRatio,
        int visitCount)
    {
        RequireNonNegative(progress, nameof(progress));
        RequireNonNegative(casualties, nameof(casualties));
        RequireNonNegative(combatRatio, nameof(combatRatio));
        if (visitCount < 0) throw new ArgumentOutOfRangeException(nameof(visitCount));
        var key = RequireId(id);
        if (!sectorStates.TryGetValue(key, out var state))
            sectorStates[key] = state = new SectorRuntimeState(key);
        state.Unlocked = unlocked;
        state.Occupied = occupied;
        state.ColonizationActive = colonizationActive;
        state.CampaignActive = campaignActive;
        state.Progress = ExpantaNum.Clamp01(progress);
        state.Casualties = casualties;
        state.CombatRatio = combatRatio;
        state.VisitCount = visitCount;
        sectors[key] = occupied ? "Occupied" : campaignActive ? "Campaign" : colonizationActive ? "Colonizing" : unlocked ? "Unlocked" : "Locked";
    }

    public void SetCampaign(bool active, string targetSectorId, ExpantaNum casualties, ExpantaNum combatRatio)
    {
        RequireNonNegative(casualties, nameof(casualties));
        RequireNonNegative(combatRatio, nameof(combatRatio));
        if (active && string.IsNullOrWhiteSpace(targetSectorId))
            throw new ArgumentException("An active campaign requires a target sector.", nameof(targetSectorId));
        CampaignActive = active;
        CampaignTargetSectorId = targetSectorId ?? "";
        CampaignCasualties = casualties;
        CampaignCombatRatio = combatRatio;
    }

    public void SetCampaignProgress(ExpantaNum progress)
    {
        RequireNonNegative(progress, nameof(progress));
        CampaignProgress = ExpantaNum.Clamp01(progress);
    }

    public void AdvanceFood(ExpantaNum productionRate, ExpantaNum consumptionRate, double deltaSeconds)
    {
        ValidateDelta(deltaSeconds, nameof(deltaSeconds));
        var next = Food + (productionRate - consumptionRate) * deltaSeconds;
        Food = ExpantaNum.Min(ExpantaNum.Max(ExpantaNum.Zero, next), FoodCapacity);
    }

    public void AdvancePopulation(
        double deltaSeconds,
        ExpantaNum happinessMultiplier,
        ExpantaNum growthRatePerSecond,
        bool foodShortageDepartureAllowed)
    {
        ValidateDelta(deltaSeconds, nameof(deltaSeconds));
        if (!happinessMultiplier.IsFinite || !growthRatePerSecond.IsFinite || growthRatePerSecond < ExpantaNum.Zero)
            throw new ArgumentOutOfRangeException(nameof(growthRatePerSecond));
        if (foodShortageDepartureAllowed)
        {
            AdvanceFoodShortageDeparture(deltaSeconds, happinessMultiplier);
            return;
        }
        if (Population >= PopulationCapacity)
        {
            PopulationChangeProgress = ExpantaNum.Zero;
            return;
        }

        var logisticRate = growthRatePerSecond * ExpantaNum.Max(ExpantaNum.One, Population) *
            ExpantaNum.Max(ExpantaNum.Zero, ExpantaNum.One - Population / PopulationCapacity);
        var accumulated = PopulationChangeProgress + ExpantaNum.Max(ExpantaNum.Zero, happinessMultiplier) * deltaSeconds * logisticRate;
        var births = ExpantaNum.Min(PopulationCapacity - Population, (accumulated + PopulationStepEpsilon).Floor());
        if (births > ExpantaNum.Zero) Population += births;
        var remainder = accumulated - births;
        if (births > ExpantaNum.Zero && remainder <= PopulationRemainderEpsilon) remainder = ExpantaNum.Zero;
        PopulationChangeProgress = Population >= PopulationCapacity ? ExpantaNum.Zero : remainder - remainder.Floor();
    }

    public bool TryPay(IReadOnlyDictionary<string, ExpantaNum> costs)
    {
        var ordered = (costs ?? throw new ArgumentNullException(nameof(costs)))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToArray();
        foreach (var cost in ordered)
        {
            if (!cost.Value.IsFinite || cost.Value < ExpantaNum.Zero || GetResource(cost.Key) < cost.Value)
                return false;
        }
        foreach (var cost in ordered)
            SetResource(cost.Key, GetResource(cost.Key) - cost.Value);
        return true;
    }

    public static ExpantaNum GeometricCost(ExpantaNum baseCost, ExpantaNum growth, long owned, long quantity)
    {
        if (owned < 0 || quantity < 0 ||
            !baseCost.IsFinite || baseCost < ExpantaNum.Zero ||
            !growth.IsFinite || growth < ExpantaNum.Zero)
            throw new ArgumentOutOfRangeException();
        if (quantity == 0) return ExpantaNum.Zero;
        var first = baseCost * ExpantaNum.Pow(growth, new ExpantaNum(owned));
        if (growth == ExpantaNum.One) return first * new ExpantaNum(quantity);
        return first * (ExpantaNum.Pow(growth, new ExpantaNum(quantity)) - ExpantaNum.One) / (growth - ExpantaNum.One);
    }

    public SimulationStateSummary CreateSummary() => new(
        Tick,
        ElapsedSeconds,
        Food.ToString(),
        FoodCapacity.ToString(),
        resources.Select(pair => new ResourceSummary(pair.Key, pair.Value.ToString())).ToArray());

    public DetailedSimulationStateSummary CreateDetailedSummary() => new(
        Tick,
        ElapsedSeconds,
        RequestedSeconds,
        TechLevel,
        CalendarDays,
        Food.ToString(),
        FoodCapacity.ToString(),
        Population.ToString(),
        PopulationCapacity.ToString(),
        TerritoryTotal.ToString(),
        AttackPower.ToString(),
        DefensePower.ToString(),
        FleetPower.ToString(),
        MilitaryManpower.ToString(),
        CampaignActive,
        CampaignTargetSectorId,
        CampaignProgress.ToString(),
        CampaignCasualties.ToString(),
        CampaignCombatRatio.ToString(),
        ActiveResearch ?? "",
        researchStates.Values.Where(item => item.Completed).Select(item => item.Id).ToArray(),
        resources.Select(pair => new ResourceSummary(pair.Key, pair.Value.ToString())).ToArray(),
        buildingStates.Values.Select(item => new BuildingSummary(item.Id, item.Amount.ToString(), item.Efficiency.ToString())).ToArray(),
        purchasedWorkshops.Select(id => new WorkshopSummary(id)).ToArray(),
        sectorStates.Values.Select(item => new SectorSummary(
            item.Id,
            item.Unlocked,
            item.Occupied,
            item.ColonizationActive,
            item.CampaignActive,
            item.Progress.ToString(),
            item.Casualties.ToString(),
            item.CombatRatio.ToString(),
            item.VisitCount)).ToArray());

    private void AdvanceFoodShortageDeparture(double deltaSeconds, ExpantaNum happinessMultiplier)
    {
        if (Population <= ExpantaNum.Zero)
        {
            Population = ExpantaNum.Zero;
            PopulationChangeProgress = ExpantaNum.Zero;
            return;
        }

        var shortage = ExpantaNum.Clamp01(ExpantaNum.One - happinessMultiplier);
        var accumulated = PopulationChangeProgress + shortage * ExpantaNum.Max(ExpantaNum.One, Population) / FoodShortageDepartureSeconds * deltaSeconds;
        var departures = ExpantaNum.Min(Population, (accumulated + PopulationStepEpsilon).Floor());
        if (departures > ExpantaNum.Zero) Population -= departures;
        PopulationChangeProgress = Population <= ExpantaNum.Zero ? ExpantaNum.Zero : accumulated - departures;
    }

    private static void ValidateDelta(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0d)
            throw new ArgumentOutOfRangeException(name);
    }

    private static ExpantaNum ParseAmount(string value, string name)
    {
        if (!ExpantaNum.TryParse(value ?? string.Empty, out var parsed) || !parsed.IsFinite)
            throw new ArgumentException($"Invalid ExpantaNum amount for {name}.", name);
        return parsed;
    }

    private static void RequireNonNegative(ExpantaNum value, string name)
    {
        if (!value.IsFinite || value < ExpantaNum.Zero)
            throw new ArgumentOutOfRangeException(name, "Simulation amounts must be finite and non-negative.");
    }

    private static void RequireFoodCapacity(ExpantaNum value, string name)
    {
        if (!value.IsFinite || value < ExpantaNum.One)
            throw new ArgumentOutOfRangeException(name, "Food capacity must be finite and at least one.");
    }

    private static string RequireId(string id) => string.IsNullOrWhiteSpace(id)
        ? throw new ArgumentException("Resource ID is required.", nameof(id))
        : id;
}

public sealed record ResourceSummary(string Id, string Amount);
public sealed record BuildingSummary(string Id, string Amount, string Efficiency);
public sealed record WorkshopSummary(string Id);
public sealed record SectorSummary(
    string Id,
    bool Unlocked,
    bool Occupied,
    bool ColonizationActive,
    bool CampaignActive,
    string Progress,
    string Casualties,
    string CombatRatio,
    int VisitCount);

public sealed record SimulationStateSummary(
    long Tick,
    double ElapsedSeconds,
    string Food,
    string FoodCapacity,
    IReadOnlyList<ResourceSummary> Resources)
{
    public override string ToString()
    {
        var resourcesText = string.Join(";", Resources.Select(resource => $"{resource.Id}={resource.Amount}"));
        return $"tick={Tick};seconds={ElapsedSeconds.ToString("R", CultureInfo.InvariantCulture)};" +
               $"food={Food};foodCapacity={FoodCapacity};resources={resourcesText}";
    }
}

public sealed record DetailedSimulationStateSummary(
    long Tick,
    double ElapsedSeconds,
    double RequestedSeconds,
    string TechLevel,
    int CalendarDays,
    string Food,
    string FoodCapacity,
    string Population,
    string PopulationCapacity,
    string TerritoryTotal,
    string AttackPower,
    string DefensePower,
    string FleetPower,
    string MilitaryManpower,
    bool CampaignActive,
    string CampaignTargetSectorId,
    string CampaignProgress,
    string CampaignCasualties,
    string CampaignCombatRatio,
    string ActiveResearch,
    IReadOnlyList<string> CompletedResearch,
    IReadOnlyList<ResourceSummary> Resources,
    IReadOnlyList<BuildingSummary> Buildings,
    IReadOnlyList<WorkshopSummary> Workshops,
    IReadOnlyList<SectorSummary> Sectors)
{
    public override string ToString()
    {
        var resourcesText = string.Join(";", Resources.Select(item => $"{item.Id}={item.Amount}"));
        var buildingsText = string.Join(";", Buildings.Select(item => $"{item.Id}={item.Amount}:{item.Efficiency}"));
        var workshopsText = string.Join(";", Workshops.Select(item => item.Id));
        var sectorsText = string.Join(";", Sectors.Select(item =>
            $"{item.Id}={item.Occupied}:{item.ColonizationActive}:{item.CampaignActive}:{item.Progress}:{item.VisitCount}"));
        return $"tick={Tick};elapsed={ElapsedSeconds.ToString("R", CultureInfo.InvariantCulture)};" +
               $"requested={RequestedSeconds.ToString("R", CultureInfo.InvariantCulture)};tech={TechLevel};calendar={CalendarDays};" +
               $"food={Food}/{FoodCapacity};population={Population}/{PopulationCapacity};territory={TerritoryTotal};" +
               $"campaign={CampaignActive}:{CampaignTargetSectorId}:{CampaignProgress}:{CampaignCasualties};" +
               $"activeResearch={ActiveResearch};resources={resourcesText};buildings={buildingsText};" +
               $"workshops={workshopsText};sectors={sectorsText}";
    }
}
