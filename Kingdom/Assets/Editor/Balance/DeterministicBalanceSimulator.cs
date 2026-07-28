using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Kingdom.EditorTools
{
    internal static class DeterministicBalanceSimulator
    {
        private const double TickSeconds = 1d;
        private const double DurationSeconds = 86400d;
        private static readonly double[] Horizons = { 600d, 3600d, 14400d, 43200d, 86400d };
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static void Export(
            string reportRoot,
            IReadOnlyList<Resource> resources,
            IReadOnlyList<Building> buildings,
            IReadOnlyList<Research> researches,
            IReadOnlyList<WorkshopUpgradeDefinition> workshops)
        {
            var policies = new[]
            {
                new Policy("FastestEra", 15d, 1, 1.0d),
                new Policy("Balanced", 45d, 1, 0.75d),
                new Policy("LowFrequency", 270d, 1, 1.5d)
            };
            var summaries = new List<string>
            {
                Csv("Scenario", "HorizonSeconds", "TechLevel", "CompletedResearch",
                    "PurchasedWorkshop", "TotalBuildings", "Population",
                    "FoodSatisfactionAverage", "LongestDecisionGapSeconds",
                    "NeolithicAt", "MedievalAt", "IndustrialAt", "IndustrialCompleteAt",
                    "Finite", "NegativeInventory")
            };

            for (int i = 0; i < policies.Length; i++)
            {
                var simulation = new Simulation(
                    policies[i], resources, buildings, researches, workshops);
                simulation.Run(DurationSeconds);
                File.WriteAllLines(
                    Path.Combine(reportRoot, $"economy-events-{policies[i].Name}.csv"),
                    simulation.EventRows,
                    new UTF8Encoding(true));
                for (int h = 0; h < Horizons.Length; h++)
                    summaries.Add(simulation.SummaryAt(Horizons[h]));
            }

            File.WriteAllLines(
                Path.Combine(reportRoot, "deterministic-economy-scenarios-after.csv"),
                summaries,
                new UTF8Encoding(true));
        }

        private sealed class Simulation
        {
            private readonly Policy policy;
            private readonly IReadOnlyList<Resource> resources;
            private readonly IReadOnlyList<Building> buildings;
            private readonly IReadOnlyList<Research> researches;
            private readonly IReadOnlyList<WorkshopUpgradeDefinition> workshops;
            private readonly Dictionary<Resource, double> inventory = new();
            private readonly Dictionary<Building, int> buildingCounts = new();
            private readonly Dictionary<Building, double> buildingEfficiency = new();
            private readonly HashSet<Research> completedResearch = new();
            private readonly HashSet<WorkshopUpgradeDefinition> purchasedWorkshop = new();
            private readonly Dictionary<string, double> milestoneTimes =
                new(StringComparer.Ordinal);
            private readonly List<Snapshot> snapshots = new();
            private Research activeResearch;
            private double activeResearchProgress;
            private double elapsed;
            private double nextDecision;
            private double population;
            private double populationGrowthProgress;
            private double food = 300d;
            private double foodSatisfaction = 1d;
            private double foodSatisfactionIntegral;
            private bool finite = true;
            private bool negativeInventory;
            private TechLevel techLevel = TechLevel.Animal;

            public Simulation(
                Policy policy,
                IReadOnlyList<Resource> resources,
                IReadOnlyList<Building> buildings,
                IReadOnlyList<Research> researches,
                IReadOnlyList<WorkshopUpgradeDefinition> workshops)
            {
                this.policy = policy;
                this.resources = resources;
                this.buildings = buildings;
                this.researches = researches;
                this.workshops = workshops;
                for (int i = 0; i < resources.Count; i++)
                    inventory[resources[i]] = 0d;
                for (int i = 0; i < buildings.Count; i++)
                {
                    buildingCounts[buildings[i]] = 0;
                    buildingEfficiency[buildings[i]] = 1d;
                }
                EventRows.Add(Csv("Seconds", "Type", "Id", "Amount", "TechLevel", "Note"));
            }

            public List<string> EventRows { get; } = new();

            public void Run(double duration)
            {
                while (elapsed < duration && finite)
                {
                    if (elapsed + 0.0001d >= nextDecision)
                    {
                        Decide();
                        nextDecision += policy.DecisionInterval;
                    }
                    AdvanceProduction(TickSeconds);
                    AdvanceResearch(TickSeconds);
                    AdvancePopulation(TickSeconds);
                    elapsed += TickSeconds;
                    foodSatisfactionIntegral += foodSatisfaction * TickSeconds;
                    if (IsSnapshotTime(elapsed))
                        snapshots.Add(CaptureSnapshot());
                    finite = IsFiniteState();
                }
                if (!milestoneTimes.ContainsKey("IndustrialComplete") &&
                    researches.Where(value => value.TechLevel == TechLevel.Industrial)
                        .All(completedResearch.Contains))
                    milestoneTimes["IndustrialComplete"] = elapsed;
                RecordFinalBlocker();
            }

            private void RecordFinalBlocker()
            {
                Research candidate = SelectResearch();
                if (candidate == null || activeResearch != null)
                    return;
                Resource missing = FirstUnaffordable(candidate.ResourceRequirements);
                string note = missing == null
                    ? "research prerequisites or decision policy"
                    : $"inventory={inventory[missing]:0.###}; required=" +
                      $"{D(candidate.ResourceRequirements.First(value => value.First == missing).Second):0.###}";
                EventRows.Add(Csv(
                    elapsed.ToString("0", Invariant),
                    "Blocked",
                    candidate.Id,
                    missing == null ? string.Empty : missing.Id,
                    techLevel.ToString(),
                    note));
            }

            public string SummaryAt(double horizon)
            {
                Snapshot value = snapshots.LastOrDefault(snapshot => snapshot.Time <= horizon) ??
                    CaptureSnapshot();
                return Csv(
                    policy.Name,
                    horizon.ToString("0", Invariant),
                    value.TechLevel.ToString(),
                    value.CompletedResearch.ToString(Invariant),
                    value.PurchasedWorkshop.ToString(Invariant),
                    value.TotalBuildings.ToString(Invariant),
                    value.Population.ToString("0.###", Invariant),
                    value.AverageFoodSatisfaction.ToString("0.######", Invariant),
                    policy.DecisionInterval.ToString("0.###", Invariant),
                    Milestone("NeolithicSettlement"),
                    Milestone("SmithingRevolution"),
                    Milestone("Industrialization"),
                    Milestone("IndustrialComplete"),
                    finite.ToString(),
                    negativeInventory.ToString());
            }

            private void Decide()
            {
                Research candidate = SelectResearch();
                if (activeResearch == null && candidate != null &&
                    CanAfford(candidate.ResourceRequirements))
                {
                    Spend(candidate.ResourceRequirements);
                    activeResearch = candidate;
                    activeResearchProgress = 0d;
                    EventRows.Add(Csv(
                        elapsed.ToString("0", Invariant), "ResearchStart", candidate.Id,
                        "1", techLevel.ToString(), string.Empty));
                }

                PurchaseAvailableWorkshop();

                Building building = SelectBuilding(candidate);
                if (building != null && CanBuild(building) &&
                    CanAfford(BuildCost(building)))
                {
                    Spend(BuildCost(building));
                    buildingCounts[building]++;
                    EventRows.Add(Csv(
                        elapsed.ToString("0", Invariant), "Building", building.Id,
                        buildingCounts[building].ToString(Invariant),
                        techLevel.ToString(), string.Empty));
                }
            }

            private Research SelectResearch()
            {
                if (activeResearch != null)
                    return activeResearch;
                HashSet<Research> eraPath = NextEraPath();
                return researches
                    .Where(IsResearchAvailable)
                    .OrderBy(value => CanAfford(value.ResourceRequirements) ? 0 : 1)
                    .ThenBy(value => ImprovesResearchPower(value) ? 0 : 1)
                    .ThenBy(value => eraPath.Contains(value) ? 0 : 1)
                    .ThenBy(value => value.AdvancesTechLevel ? 0 : 1)
                    .ThenBy(value => value.TechLevel)
                    .ThenBy(ResearchCost)
                    .FirstOrDefault();
            }

            private bool ImprovesResearchPower(Research value)
            {
                if (value.Effects.Any(effect =>
                    effect.Type == ResearchEffectType.GlobalResearchMultiplier))
                    return true;
                return buildings.Any(building =>
                    D(building.ResearchPowerGranted) > 0d &&
                    building.RequiredResearch.Contains(value));
            }

            private HashSet<Research> NextEraPath()
            {
                Research transition = researches.FirstOrDefault(value =>
                    value.AdvancesTechLevel && (int)value.TechLevel == (int)techLevel + 1);
                var result = new HashSet<Research>();
                AddPrerequisites(transition, result);
                return result;
            }

            private static void AddPrerequisites(Research value, HashSet<Research> result)
            {
                if (value == null || !result.Add(value))
                    return;
                for (int i = 0; i < value.Prerequisites.Count; i++)
                    AddPrerequisites(value.Prerequisites[i], result);
            }

            private bool IsResearchAvailable(Research value)
            {
                if (value == null || value.TechLevel > TechLevel.Industrial ||
                    completedResearch.Contains(value))
                    return false;
                bool eraAccessible = value.TechLevel <= techLevel ||
                    value.AdvancesTechLevel && (int)value.TechLevel == (int)techLevel + 1;
                return eraAccessible && value.Prerequisites.All(completedResearch.Contains);
            }

            private void PurchaseAvailableWorkshop()
            {
                bool systemUnlocked = completedResearch.Any(value =>
                    value.Effects.Any(effect =>
                        effect.Type == ResearchEffectType.UnlockSystem &&
                        effect.SystemId == WorkshopManager.WorkshopSystemId));
                if (!systemUnlocked)
                    return;

                WorkshopUpgradeDefinition next = workshops
                    .Where(value => !purchasedWorkshop.Contains(value) &&
                        value.TechLevel >= TechLevel.Industrial &&
                        value.RequiredResearch.All(completedResearch.Contains) &&
                        value.RequiredUpgrades.All(purchasedWorkshop.Contains) &&
                        CanAfford(value.ResourceRequirements))
                    .OrderBy(value => value.SortOrder)
                    .FirstOrDefault();
                if (next == null)
                    return;
                Spend(next.ResourceRequirements);
                purchasedWorkshop.Add(next);
                EventRows.Add(Csv(
                    elapsed.ToString("0", Invariant), "Workshop", next.Id, "1",
                    techLevel.ToString(), string.Empty));
            }

            private Building SelectBuilding(Research candidate)
            {
                List<Building> available = buildings
                    .Where(AreBuildingPrerequisitesMet)
                    .Where(value => value.TechLevel <= TechLevel.Industrial)
                    .ToList();

                Building house = available.FirstOrDefault(value => value.Id == "WoodHouse");
                if (house != null && PopulationCapacity() <= 0d &&
                    CanBuild(house) && CanAfford(BuildCost(house)))
                    return house;

                double foodDemand = population;
                if (FoodProductionPotential() < foodDemand + 2d)
                {
                    Building foodBuilding = available
                        .Where(value => D(value.FoodProductionRate) > 0d)
                        .Where(value => CanAfford(BuildCost(value)))
                        .OrderByDescending(value => D(value.FoodProductionRate))
                        .FirstOrDefault();
                    Building selected = SelectBuildableOrHousing(foodBuilding, house);
                    if (selected != null)
                        return selected;
                }

                if (candidate != null)
                {
                    Resource missing = FirstUnaffordable(candidate.ResourceRequirements);
                    if (missing != null)
                    {
                        Building producer = SelectProducerFor(
                            missing, available, new HashSet<Resource>());
                        Building selected = SelectBuildableOrHousing(producer, house);
                        if (selected != null)
                            return selected;
                    }
                }

                double targetResearchPower = techLevel switch
                {
                    TechLevel.Animal => 2d,
                    TechLevel.Neolithic => 10d,
                    TechLevel.Medieval => 25d,
                    _ => 250d
                };
                if (ResearchPower() < targetResearchPower * policy.ResearchPowerFactor)
                {
                    Building researchBuilding = available
                        .Where(value => D(value.ResearchPowerGranted) > 0d)
                        .Where(value => CanAfford(BuildCost(value)))
                        .OrderByDescending(value => D(value.ResearchPowerGranted))
                        .FirstOrDefault();
                    Building selected = SelectBuildableOrHousing(researchBuilding, house);
                    if (selected != null)
                        return selected;
                }

                Building economic = available
                    .Where(value => buildingCounts[value] < policy.GeneralBuildingTarget)
                    .Where(value => HasEconomicOutput(value))
                    .Where(value => CanAfford(BuildCost(value)))
                    .OrderBy(value => value.TechLevel)
                    .ThenBy(value => buildingCounts[value])
                    .ThenBy(value => value.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                return SelectBuildableOrHousing(economic, house);
            }

            private Building SelectProducerFor(
                Resource resource,
                List<Building> available,
                HashSet<Resource> visiting)
            {
                if (resource == null || !visiting.Add(resource))
                    return null;

                List<Building> producers = available
                    .Where(value => Contains(value.ResourceGenerationRates, resource))
                    .OrderBy(value => buildingCounts[value])
                    .ThenBy(value => value.Id, StringComparer.Ordinal)
                    .ToList();
                for (int i = 0; i < producers.Count; i++)
                {
                    Building producer = producers[i];
                    List<Pair<Resource, ExpantaNum>> cost = BuildCost(producer);
                    Resource missingCost = FirstUnaffordable(cost);
                    if (missingCost != null)
                    {
                        Building upstream =
                            SelectProducerFor(missingCost, available, visiting);
                        if (upstream != null)
                            return upstream;
                        continue;
                    }

                    bool needsFirstCopy = buildingCounts[producer] == 0;
                    bool needsPositiveFlow =
                        NetResourceRate(resource) <= 0.000001d &&
                        buildingCounts[producer] < 5;
                    if (needsFirstCopy || needsPositiveFlow)
                        return producer;
                }
                return null;
            }

            private double NetResourceRate(Resource resource)
            {
                EffectSnapshot effects =
                    EffectSnapshot.Create(completedResearch, purchasedWorkshop);
                return ProductionRates(effects)[resource] -
                    ActualConsumptionRates()[resource];
            }

            private Building SelectBuildableOrHousing(Building candidate, Building house)
            {
                if (candidate == null)
                    return null;
                if (CanBuild(candidate))
                    return candidate;

                double requiredWorkforce =
                    WorkforceUsed() + D(candidate.ProductivityConsumption);
                EffectSnapshot effects =
                    EffectSnapshot.Create(completedResearch, purchasedWorkshop);
                bool waitingForPopulation =
                    requiredWorkforce >
                    population + effects.ProductivityGranted + 0.000001d;
                bool populationAtCapacity =
                    population + 0.000001d >= PopulationCapacity();
                if (waitingForPopulation && populationAtCapacity &&
                    house != null && CanBuild(house) && CanAfford(BuildCost(house)))
                    return house;
                return null;
            }

            private bool AreBuildingPrerequisitesMet(Building value)
            {
                return value != null && value.TechLevel <= techLevel &&
                    value.RequiredResearch.All(completedResearch.Contains) &&
                    value.RequiredWorkshopUpgrades.All(purchasedWorkshop.Contains);
            }

            private bool CanBuild(Building value)
            {
                if (!AreBuildingPrerequisitesMet(value))
                    return false;
                double workforceAfter = WorkforceUsed() + D(value.ProductivityConsumption);
                double territoryAfter = TerritoryUsed() + D(value.SpaceCost);
                EffectSnapshot effects =
                    EffectSnapshot.Create(completedResearch, purchasedWorkshop);
                return workforceAfter <=
                        population + effects.ProductivityGranted + 0.000001d &&
                    territoryAfter <= TerritoryTotal() + 0.000001d;
            }

            private List<Pair<Resource, ExpantaNum>> BuildCost(Building value)
            {
                int owned = buildingCounts[value];
                double growth = Math.Max(1d, D(value.CostGrowth));
                var result = new List<Pair<Resource, ExpantaNum>>();
                for (int i = 0; i < value.ResourceRequirements.Count; i++)
                {
                    Pair<Resource, ExpantaNum> pair = value.ResourceRequirements[i];
                    result.Add(new Pair<Resource, ExpantaNum>(
                        pair.First, new ExpantaNum(D(pair.Second) * Math.Pow(growth, owned))));
                }
                return result;
            }

            private void AdvanceResearch(double delta)
            {
                if (activeResearch == null)
                    return;
                activeResearchProgress += ResearchPower() * delta;
                if (activeResearchProgress + 0.000001d < ResearchCost(activeResearch))
                    return;

                Research completed = activeResearch;
                completedResearch.Add(completed);
                activeResearch = null;
                activeResearchProgress = 0d;
                if (completed.AdvancesTechLevel && completed.TechLevel > techLevel)
                    techLevel = completed.TechLevel;
                milestoneTimes[completed.Id] = elapsed;
                if (!milestoneTimes.ContainsKey("IndustrialComplete") &&
                    researches.Where(value => value.TechLevel == TechLevel.Industrial)
                        .All(completedResearch.Contains))
                    milestoneTimes["IndustrialComplete"] = elapsed;
                EventRows.Add(Csv(
                    elapsed.ToString("0", Invariant), "ResearchComplete", completed.Id,
                    "1", techLevel.ToString(), string.Empty));
            }

            private void AdvanceProduction(double delta)
            {
                var effects = EffectSnapshot.Create(completedResearch, purchasedWorkshop);
                for (int i = 0; i < buildings.Count; i++)
                    buildingEfficiency[buildings[i]] = 1d;
                for (int pass = 0; pass <= buildings.Count; pass++)
                {
                    Dictionary<Resource, double> production = ProductionRates(effects);
                    Dictionary<Resource, double> demand = PotentialConsumptionRates();
                    var satisfaction = new Dictionary<Resource, double>();
                    for (int i = 0; i < resources.Count; i++)
                    {
                        Resource resource = resources[i];
                        double required = demand[resource] * delta;
                        satisfaction[resource] = required <= 0d
                            ? 1d
                            : Clamp01((inventory[resource] + production[resource] * delta) / required);
                    }

                    double powerProduction = FlowProduction(value =>
                        D(value.PowerProductionRate) * effects.PowerMultiplier(value));
                    double powerDemand = FlowPotential(value => D(value.PowerConsumptionRate));
                    double powerSatisfaction = powerDemand <= 0d
                        ? 1d : Clamp01(powerProduction / powerDemand);
                    double logisticsProduction = FlowProduction(value =>
                        D(value.LogisticsProductionRate) * effects.LogisticsMultiplier(value));
                    double logisticsDemand = FlowPotential(value => D(value.LogisticsConsumptionRate));
                    double logisticsSatisfaction = logisticsDemand <= 0d
                        ? 1d : Clamp01(logisticsProduction / logisticsDemand);
                    double foodProduction = FoodProduction(effects);
                    double foodDemand = population + FlowPotential(value => D(value.FoodConsumptionRate));
                    foodSatisfaction = foodDemand <= 0d
                        ? 1d
                        : Clamp01((food + foodProduction * delta) / (foodDemand * delta));

                    bool changed = false;
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        Building building = buildings[i];
                        double resourceEfficiency = 1d;
                        for (int r = 0; r < building.ResourceConsumptionRates.Count; r++)
                            resourceEfficiency *= satisfaction[building.ResourceConsumptionRates[r].First];
                        double next = resourceEfficiency * foodSatisfaction;
                        if (D(building.PowerConsumptionRate) > 0d)
                            next *= powerSatisfaction;
                        if (D(building.LogisticsConsumptionRate) > 0d)
                            next *= logisticsSatisfaction;
                        next = Clamp01(next);
                        if (Math.Abs(next - buildingEfficiency[building]) > 0.0000001d)
                        {
                            buildingEfficiency[building] = next;
                            changed = true;
                        }
                    }
                    if (!changed)
                        break;
                }

                EffectSnapshot finalEffects =
                    EffectSnapshot.Create(completedResearch, purchasedWorkshop);
                Dictionary<Resource, double> finalProduction = ProductionRates(finalEffects);
                Dictionary<Resource, double> finalConsumption = ActualConsumptionRates();
                for (int i = 0; i < resources.Count; i++)
                {
                    Resource resource = resources[i];
                    inventory[resource] +=
                        (finalProduction[resource] - finalConsumption[resource]) * delta;
                    if (inventory[resource] < -0.000001d)
                        negativeInventory = true;
                    inventory[resource] = Math.Max(0d, inventory[resource]);
                }

                double finalFoodProduction = FoodProduction(finalEffects);
                double finalFoodConsumption = population;
                for (int i = 0; i < buildings.Count; i++)
                    finalFoodConsumption += buildingCounts[buildings[i]] *
                        buildingEfficiency[buildings[i]] * D(buildings[i].FoodConsumptionRate);
                food += (finalFoodProduction - finalFoodConsumption) * delta;
                food = Math.Max(0d, Math.Min(FoodCapacity(), food));
            }

            private Dictionary<Resource, double> ProductionRates(EffectSnapshot effects)
            {
                var result = resources.ToDictionary(value => value, _ => 0d);
                Resource wood = resources.FirstOrDefault(value => value.Id == "WoodLog");
                if (wood != null)
                    result[wood] = 1d;
                for (int i = 0; i < buildings.Count; i++)
                {
                    Building building = buildings[i];
                    double scale = buildingCounts[building] * buildingEfficiency[building] *
                        effects.BuildingProductionMultiplier(building);
                    for (int r = 0; r < building.ResourceGenerationRates.Count; r++)
                    {
                        Pair<Resource, ExpantaNum> pair = building.ResourceGenerationRates[r];
                        result[pair.First] += scale * D(pair.Second) *
                            effects.ResourceProductionMultiplier(pair.First);
                    }
                }
                return result;
            }

            private Dictionary<Resource, double> PotentialConsumptionRates()
            {
                var result = resources.ToDictionary(value => value, _ => 0d);
                for (int i = 0; i < buildings.Count; i++)
                {
                    Building building = buildings[i];
                    for (int r = 0; r < building.ResourceConsumptionRates.Count; r++)
                    {
                        Pair<Resource, ExpantaNum> pair = building.ResourceConsumptionRates[r];
                        result[pair.First] += buildingCounts[building] * D(pair.Second);
                    }
                }
                return result;
            }

            private Dictionary<Resource, double> ActualConsumptionRates()
            {
                var result = resources.ToDictionary(value => value, _ => 0d);
                for (int i = 0; i < buildings.Count; i++)
                {
                    Building building = buildings[i];
                    double scale = buildingCounts[building] * buildingEfficiency[building];
                    for (int r = 0; r < building.ResourceConsumptionRates.Count; r++)
                    {
                        Pair<Resource, ExpantaNum> pair = building.ResourceConsumptionRates[r];
                        result[pair.First] += scale * D(pair.Second);
                    }
                }
                return result;
            }

            private double FoodProduction(EffectSnapshot effects)
            {
                double result = 0d;
                for (int i = 0; i < buildings.Count; i++)
                {
                    Building building = buildings[i];
                    result += buildingCounts[building] * buildingEfficiency[building] *
                        D(building.FoodProductionRate) *
                        effects.BuildingProductionMultiplier(building) *
                        effects.BuildingFoodMultiplier(building);
                }
                return result;
            }

            private double FoodProductionPotential()
            {
                double result = 0d;
                for (int i = 0; i < buildings.Count; i++)
                    result += buildingCounts[buildings[i]] * D(buildings[i].FoodProductionRate);
                return result;
            }

            private double FlowProduction(Func<Building, double> selector)
            {
                double result = 0d;
                for (int i = 0; i < buildings.Count; i++)
                    result += buildingCounts[buildings[i]] * buildingEfficiency[buildings[i]] *
                        selector(buildings[i]);
                return result;
            }

            private double FlowPotential(Func<Building, double> selector)
            {
                double result = 0d;
                for (int i = 0; i < buildings.Count; i++)
                    result += buildingCounts[buildings[i]] * selector(buildings[i]);
                return result;
            }

            private void AdvancePopulation(double delta)
            {
                if (population >= PopulationCapacity() || foodSatisfaction <= 0d)
                    return;
                populationGrowthProgress += foodSatisfaction * delta / 60d;
                if (populationGrowthProgress < 1d)
                    return;
                population += 1d;
                populationGrowthProgress = 0d;
            }

            private double ResearchPower()
            {
                EffectSnapshot effects =
                    EffectSnapshot.Create(completedResearch, purchasedWorkshop);
                double result = 1d;
                for (int i = 0; i < buildings.Count; i++)
                {
                    Building building = buildings[i];
                    result += buildingCounts[building] * buildingEfficiency[building] *
                        D(building.ResearchPowerGranted) *
                        effects.BuildingResearchMultiplier(building);
                }
                return result * effects.GlobalResearchMultiplier;
            }

            private double PopulationCapacity() =>
                buildings.Sum(value => buildingCounts[value] * D(value.PopulationCapacityGranted));

            private double FoodCapacity() =>
                500d + buildings.Sum(value =>
                    buildingCounts[value] * buildingEfficiency[value] * D(value.FoodCapacityGranted));

            private double WorkforceUsed() =>
                buildings.Sum(value => buildingCounts[value] * D(value.ProductivityConsumption));

            private double TerritoryUsed() =>
                buildings.Sum(value => buildingCounts[value] * D(value.SpaceCost));

            private double TerritoryTotal()
            {
                EffectSnapshot effects =
                    EffectSnapshot.Create(completedResearch, purchasedWorkshop);
                return 100d + effects.TerritoryGranted;
            }

            private bool CanAfford(IReadOnlyList<Pair<Resource, ExpantaNum>> costs) =>
                FirstUnaffordable(costs) == null;

            private Resource FirstUnaffordable(IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
            {
                for (int i = 0; i < costs.Count; i++)
                {
                    Pair<Resource, ExpantaNum> pair = costs[i];
                    if (pair.First != null &&
                        inventory.TryGetValue(pair.First, out double amount) &&
                        amount + 0.000001d < D(pair.Second))
                        return pair.First;
                }
                return null;
            }

            private void Spend(IReadOnlyList<Pair<Resource, ExpantaNum>> costs)
            {
                for (int i = 0; i < costs.Count; i++)
                {
                    Pair<Resource, ExpantaNum> pair = costs[i];
                    inventory[pair.First] =
                        Math.Max(0d, inventory[pair.First] - D(pair.Second));
                }
            }

            private bool IsFiniteState()
            {
                if (!double.IsFinite(food) || !double.IsFinite(population))
                    return false;
                foreach (double value in inventory.Values)
                    if (!double.IsFinite(value))
                        return false;
                return true;
            }

            private Snapshot CaptureSnapshot() => new()
            {
                Time = elapsed,
                TechLevel = techLevel,
                CompletedResearch = completedResearch.Count,
                PurchasedWorkshop = purchasedWorkshop.Count,
                TotalBuildings = buildingCounts.Values.Sum(),
                Population = population,
                AverageFoodSatisfaction = elapsed <= 0d
                    ? 1d : foodSatisfactionIntegral / elapsed
            };

            private bool IsSnapshotTime(double time)
            {
                for (int i = 0; i < Horizons.Length; i++)
                    if (Math.Abs(time - Horizons[i]) < 0.5d)
                        return true;
                return false;
            }

            private string Milestone(string id) =>
                milestoneTimes.TryGetValue(id, out double time)
                    ? time.ToString("0.###", Invariant)
                    : string.Empty;

            private static bool HasEconomicOutput(Building value) =>
                value.ResourceGenerationRates.Count > 0 ||
                D(value.FoodProductionRate) > 0d ||
                D(value.ResearchPowerGranted) > 0d ||
                D(value.PowerProductionRate) > 0d ||
                D(value.LogisticsProductionRate) > 0d ||
                D(value.PopulationCapacityGranted) > 0d;

            private static bool Contains(
                IReadOnlyList<Pair<Resource, ExpantaNum>> pairs, Resource resource)
            {
                for (int i = 0; i < pairs.Count; i++)
                    if (pairs[i].First == resource && pairs[i].Second > ExpantaNum.Zero)
                        return true;
                return false;
            }
        }

        private sealed class EffectSnapshot
        {
            private readonly Dictionary<Building, double> buildingProduction = new();
            private readonly Dictionary<Building, double> buildingFood = new();
            private readonly Dictionary<Building, double> buildingResearch = new();
            private readonly Dictionary<Building, double> buildingPower = new();
            private readonly Dictionary<Building, double> buildingLogistics = new();
            private readonly Dictionary<Resource, double> resourceProduction = new();
            private double globalBuilding = 1d;
            private double globalPower = 1d;
            private double globalLogistics = 1d;
            public double GlobalResearchMultiplier { get; private set; } = 1d;
            public double TerritoryGranted { get; private set; }
            public double ProductivityGranted { get; private set; }

            public static EffectSnapshot Create(
                IEnumerable<Research> research,
                IEnumerable<WorkshopUpgradeDefinition> workshops)
            {
                var result = new EffectSnapshot();
                foreach (Research value in research)
                    for (int i = 0; i < value.Effects.Count; i++)
                        result.Apply(value.Effects[i]);
                foreach (WorkshopUpgradeDefinition value in workshops)
                    for (int i = 0; i < value.Effects.Count; i++)
                        result.Apply(value.Effects[i]);
                return result;
            }

            public double BuildingProductionMultiplier(Building value) =>
                globalBuilding * Get(buildingProduction, value);
            public double BuildingFoodMultiplier(Building value) => Get(buildingFood, value);
            public double BuildingResearchMultiplier(Building value) => Get(buildingResearch, value);
            public double PowerMultiplier(Building value) => globalPower * Get(buildingPower, value);
            public double LogisticsMultiplier(Building value) =>
                globalLogistics * Get(buildingLogistics, value);
            public double ResourceProductionMultiplier(Resource value) =>
                Get(resourceProduction, value);

            private void Apply(ResearchEffectDefinition effect)
            {
                if (effect == null)
                    return;
                double multiplier = Math.Max(0d, D(effect.Value));
                switch (effect.Type)
                {
                    case ResearchEffectType.BuildingProductionMultiplier:
                        Multiply(buildingProduction, effect.Building, multiplier);
                        break;
                    case ResearchEffectType.BuildingFoodProductionMultiplier:
                        Multiply(buildingFood, effect.Building, multiplier);
                        break;
                    case ResearchEffectType.ResourceProductionMultiplier:
                        Multiply(resourceProduction, effect.Resource, multiplier);
                        break;
                    case ResearchEffectType.GlobalResearchMultiplier:
                        GlobalResearchMultiplier *= multiplier;
                        break;
                    case ResearchEffectType.TerritoryGranted:
                        TerritoryGranted += multiplier;
                        break;
                    case ResearchEffectType.ProductivityGranted:
                        ProductivityGranted += multiplier;
                        break;
                    case ResearchEffectType.GlobalBuildingProductionMultiplier:
                        globalBuilding *= multiplier;
                        break;
                    case ResearchEffectType.BuildingResearchPowerMultiplier:
                        Multiply(buildingResearch, effect.Building, multiplier);
                        break;
                    case ResearchEffectType.BuildingPowerProductionMultiplier:
                        Multiply(buildingPower, effect.Building, multiplier);
                        break;
                    case ResearchEffectType.BuildingLogisticsProductionMultiplier:
                        Multiply(buildingLogistics, effect.Building, multiplier);
                        break;
                    case ResearchEffectType.PowerMultiplier:
                        globalPower *= multiplier;
                        break;
                    case ResearchEffectType.GlobalLogisticsMultiplier:
                        globalLogistics *= multiplier;
                        break;
                }
            }

            private void Apply(WorkshopEffectDefinition effect)
            {
                if (effect == null)
                    return;
                double multiplier = Math.Max(0d, D(effect.Value));
                switch (effect.Type)
                {
                    case WorkshopEffectType.BuildingProductionMultiplier:
                        Multiply(buildingProduction, effect.Building, multiplier);
                        break;
                    case WorkshopEffectType.BuildingFoodProductionMultiplier:
                        Multiply(buildingFood, effect.Building, multiplier);
                        break;
                    case WorkshopEffectType.ResourceProductionMultiplier:
                        Multiply(resourceProduction, effect.Resource, multiplier);
                        break;
                    case WorkshopEffectType.GlobalResearchMultiplier:
                        GlobalResearchMultiplier *= multiplier;
                        break;
                    case WorkshopEffectType.TerritoryGranted:
                        TerritoryGranted += multiplier;
                        break;
                    case WorkshopEffectType.GlobalBuildingProductionMultiplier:
                        globalBuilding *= multiplier;
                        break;
                    case WorkshopEffectType.BuildingResearchPowerMultiplier:
                        Multiply(buildingResearch, effect.Building, multiplier);
                        break;
                    case WorkshopEffectType.BuildingPowerProductionMultiplier:
                        Multiply(buildingPower, effect.Building, multiplier);
                        break;
                    case WorkshopEffectType.BuildingLogisticsProductionMultiplier:
                        Multiply(buildingLogistics, effect.Building, multiplier);
                        break;
                    case WorkshopEffectType.PowerMultiplier:
                        globalPower *= multiplier;
                        break;
                    case WorkshopEffectType.GlobalLogisticsMultiplier:
                        globalLogistics *= multiplier;
                        break;
                }
            }

            private static double Get<T>(Dictionary<T, double> values, T key) where T : class =>
                key != null && values.TryGetValue(key, out double value) ? value : 1d;

            private static void Multiply<T>(
                Dictionary<T, double> values, T key, double multiplier) where T : class
            {
                if (key == null)
                    return;
                values[key] = Get(values, key) * multiplier;
            }
        }

        private sealed class Policy
        {
            public Policy(
                string name, double decisionInterval, int generalBuildingTarget,
                double researchPowerFactor)
            {
                Name = name;
                DecisionInterval = decisionInterval;
                GeneralBuildingTarget = generalBuildingTarget;
                ResearchPowerFactor = researchPowerFactor;
            }

            public string Name { get; }
            public double DecisionInterval { get; }
            public int GeneralBuildingTarget { get; }
            public double ResearchPowerFactor { get; }
        }

        private sealed class Snapshot
        {
            public double Time;
            public TechLevel TechLevel;
            public int CompletedResearch;
            public int PurchasedWorkshop;
            public int TotalBuildings;
            public double Population;
            public double AverageFoodSatisfaction;
        }

        private static double ResearchCost(Research value) =>
            ExpantaNum.TryParse(value.BaseCost, out ExpantaNum parsed)
                ? Math.Max(0d, D(parsed))
                : double.PositiveInfinity;

        private static double D(ExpantaNum value) => value.ToDouble();
        private static double Clamp01(double value) => Math.Max(0d, Math.Min(1d, value));

        private static string Csv(params string[] values) =>
            string.Join(",", values.Select(value =>
                "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\""));
    }
}
