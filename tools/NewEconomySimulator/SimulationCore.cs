#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kingdom.NewEconomySimulator;

public interface ISimulationRules
{
    void ApplyTick(SimulationState state, SimulationTickContext context);
}

public interface IPhasedSimulationRules : ISimulationRules
{
    void ApplyPhase(string system, SimulationState state, SimulationTickContext context);
}

public sealed record SimulationTickContext(
    long Tick,
    double DeltaSeconds,
    SimulationMode Mode,
    SimulationEventLog Events)
{
    public double RequestedSeconds { get; init; }
    public bool UseRealtimeResearchCadence { get; init; }
}

public sealed class RuntimeRulesNotMappedException : NotSupportedException
{
    public RuntimeRulesNotMappedException(string message) : base(message) { }
}

public sealed class UnmappedRuntimeRules : ISimulationRules
{
    public void ApplyTick(SimulationState state, SimulationTickContext context) =>
        throw new RuntimeRulesNotMappedException(
            "Kingdom runtime rules are not mapped into NewEconomySimulator; " +
            "provide an ISimulationRules implementation before running a scenario.");
}

public sealed class DeterministicRules : IPhasedSimulationRules
{
    public void ApplyTick(SimulationState state, SimulationTickContext context)
    {
        foreach (var system in SimulationSystems.OrderedSystems)
            ApplyPhase(system, state, context);
    }

    public void ApplyPhase(string system, SimulationState state, SimulationTickContext context) =>
        context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode,
            SimulationEventKind.Diagnostic, system, "phase");
}

public static class SimulationStateFactory
{
    public static SimulationState Create(EconomySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        SnapshotValidation.Normalize(snapshot);
        var validation = SnapshotValidation.Validate(snapshot);
        if (!validation.IsValid)
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));

        var runtime = snapshot.RuntimeState;
        var state = new SimulationState(runtime.Food, runtime.FoodCapacity);
        state.RestoreClock(
            runtime.Tick,
            ParseNumber(runtime.ElapsedSeconds, "RuntimeState.ElapsedSeconds").ToDouble(),
            ParseNumber(runtime.ElapsedSeconds, "RuntimeState.ElapsedSeconds").ToDouble(),
            runtime.CalendarDays,
            runtime.CalendarElapsedSeconds);
        state.SetTechLevel(runtime.TechLevel);
        foreach (var resource in runtime.Resources)
            state.SetResource(resource.ResourceId, resource.Value);

        foreach (var building in snapshot.Buildings)
        {
            var saved = runtime.Buildings.FirstOrDefault(item =>
                string.Equals(item.BuildingId, building.Id, StringComparison.Ordinal));
            state.SetBuilding(building.Id, ParseNumber(saved?.Amount ?? "0", building.Id));
            state.SetBuildingEfficiency(building.Id, ParseNumber(saved?.Efficiency ?? "1", building.Id));
        }

        foreach (var research in snapshot.Research)
        {
            var saved = runtime.Research.FirstOrDefault(item =>
                string.Equals(item.ResearchId, research.Id, StringComparison.Ordinal));
            var hasCost = research.ResourceRequirements.Any(item => ParseNumber(item.Value, item.Value) > ExpantaNum.Zero);
            var paid = (saved?.PaidResourceCosts ?? new List<AmountSnapshot>())
                .ToDictionary(item => item.ResourceId, item => ParseNumber(item.Value, item.Value), StringComparer.Ordinal);
            state.SetResearchState(
                research.Id,
                ParseNumber(saved?.Progress ?? "0", research.Id),
                saved?.CostPaid ?? !hasCost,
                saved?.Completed ?? false,
                paid);
        }

        state.SetActiveResearch(string.IsNullOrWhiteSpace(runtime.ActiveResearchId) ? null : runtime.ActiveResearchId);
        state.SetResearchQueue(runtime.QueuedResearchIds);
        foreach (var workshop in runtime.PurchasedWorkshopIds)
            state.SetWorkshop(workshop, ExpantaNum.One);

        foreach (var sector in snapshot.Sectors)
        {
            var saved = runtime.Sectors.FirstOrDefault(item =>
                string.Equals(item.SectorId, sector.Id, StringComparison.Ordinal));
            state.SetSectorState(
                sector.Id,
                saved?.Unlocked ?? false,
                saved?.Occupied ?? false,
                saved?.ColonizationActive ?? false,
                saved?.CampaignActive ?? false,
                ParseNumber(saved?.Progress ?? "0", sector.Id),
                ParseNumber(saved?.Casualties ?? "0", sector.Id),
                ParseNumber(saved?.CombatRatio ?? "0", sector.Id),
                saved?.VisitCount ?? 0);
            state.SetInteriorExplorationState(sector.Id, saved?.InteriorExplorationActive ?? false,
                ParseNumber(saved?.InteriorExplorationProgress ?? "0", sector.Id), saved?.InteriorExplorationPaused ?? false);
        }

        state.SetPopulation(ParseNumber(runtime.Population, "RuntimeState.Population"));
        var populationCapacity = ExpantaNum.Zero;
        foreach (var definition in snapshot.Buildings)
            populationCapacity += state.BuildingStates[definition.Id].Amount *
                ParseNumber(definition.PopulationCapacityGranted, definition.Id);
        state.SetPopulationCapacity(populationCapacity);
        // Runtime restores progress after deriving housing, which can reset the fraction.
        state.RestorePopulationChangeProgress(ParseNumber(runtime.PopulationChangeProgress,
            "RuntimeState.PopulationChangeProgress"));
        state.SetTerritoryTotal(ParseNumber(runtime.TerritoryTotal, "RuntimeState.TerritoryTotal"));
        state.SetMilitary(
            ParseNumber(runtime.AttackPower, "RuntimeState.AttackPower"),
            ParseNumber(runtime.DefensePower, "RuntimeState.DefensePower"),
            ParseNumber(runtime.FleetPower, "RuntimeState.FleetPower"),
            ParseNumber(runtime.MilitaryManpower, "RuntimeState.MilitaryManpower"));
        state.SetFlowSatisfaction(
            ParseNumber(runtime.SupplySatisfaction, "RuntimeState.SupplySatisfaction"),
            ParseNumber(runtime.PowerSatisfaction, "RuntimeState.PowerSatisfaction"),
            ParseNumber(runtime.LogisticsSatisfaction, "RuntimeState.LogisticsSatisfaction"));
        state.SetCampaign(
            runtime.CampaignActive,
            runtime.CampaignTargetSectorId,
            ParseNumber(runtime.CampaignCasualties, "RuntimeState.CampaignCasualties"),
            ParseNumber(runtime.CampaignCombatRatio, "RuntimeState.CampaignCombatRatio"));
        state.SetCampaignProgress(ParseNumber(runtime.CampaignProgress, "RuntimeState.CampaignProgress"));
        return state;
    }

    private static ExpantaNum ParseNumber(string value, string name)
    {
        if (!ExpantaNum.TryParse(value ?? string.Empty, out var parsed) || !parsed.IsFinite)
            throw new InvalidDataException($"Invalid ExpantaNum for {name}: '{value}'.");
        return parsed;
    }
}

public sealed class SnapshotDrivenRules : IPhasedSimulationRules
{
    private readonly SortedDictionary<string, (string output, ExpantaNum rate)> simpleProduction = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, ExpantaNum> simpleConsumption = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, (ExpantaNum cost, ExpantaNum power)> simpleResearch = new(StringComparer.Ordinal);
    private readonly SortedSet<string> simpleActiveResearch = new(StringComparer.Ordinal);
    private readonly SnapshotDefinitionSet? definitions;
    private TickFrame? frame;
    private SimulationModifiers? cachedModifiers;
    private long cachedModifiersProgressionVersion = -1L;

    public SnapshotDrivenRules(EconomySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        SnapshotValidation.Normalize(snapshot);
        var validation = SnapshotValidation.Validate(snapshot);
        if (!validation.IsValid)
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        definitions = new SnapshotDefinitionSet(snapshot);
    }

    public SnapshotDrivenRules(
        IEnumerable<(string Id, string OutputResource, ExpantaNum Rate)> producers,
        IEnumerable<(string Id, ExpantaNum Rate)> consumers,
        IEnumerable<(string Id, ExpantaNum Cost, ExpantaNum ResearchPower)> researches)
    {
        foreach (var item in producers ?? Array.Empty<(string, string, ExpantaNum)>())
            simpleProduction[item.Id] = (item.OutputResource, item.Rate);
        foreach (var item in consumers ?? Array.Empty<(string, ExpantaNum)>())
            simpleConsumption[item.Id] = item.Rate;
        foreach (var item in researches ?? Array.Empty<(string, ExpantaNum, ExpantaNum)>())
            simpleResearch[item.Id] = (item.Cost, item.ResearchPower);
    }

    public void ApplyTick(SimulationState state, SimulationTickContext context)
    {
        foreach (var system in SimulationSystems.OrderedSystems)
            ApplyPhase(system, state, context);
    }

    public void ApplyPhase(string system, SimulationState state, SimulationTickContext context)
    {
        if (definitions is null)
        {
            ApplySimplePhase(system, state, context);
            return;
        }

        EnsureFrame(state, context);
        switch (system)
        {
            case SimulationSystems.Building:
                context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode,
                    SimulationEventKind.Diagnostic, system, "prepared", frame!.ActiveBuildings);
                break;
            case SimulationSystems.Game:
                ApplyGame(state, context, frame!);
                break;
            case SimulationSystems.OccupiedSector:
                ApplyOccupiedSectors(state, context, frame!);
                break;
            case SimulationSystems.Resource:
                ApplyResources(state, context, frame!);
                break;
            case SimulationSystems.Colonization:
                ApplyColonization(state, context, frame!);
                ApplyInteriorExploration(state, context);
                break;
            case SimulationSystems.Campaign:
                ApplyCampaign(state, context, frame!);
                break;
            case SimulationSystems.Research:
                ApplyResearch(state, context, frame!);
                break;
            case SimulationSystems.Story:
                context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode,
                    SimulationEventKind.Diagnostic, system, "refreshed");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(system), system, "Unknown simulation system.");
        }
    }

    public bool TryStartResearch(SimulationState state, string researchId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(researchId);
        if (definitions is null || state.ActiveResearch is not null)
            return false;
        if (!definitions.Research.TryGetValue(researchId, out var definition) ||
            !CanAccessResearch(state, definition) ||
            !AreResearchPrerequisitesCompleted(state, definition) ||
            state.GetResearchState(researchId).Completed)
            return false;
        if (!TryPayResearch(state, researchId))
            return false;
        state.SetActiveResearch(researchId);
        state.Events?.Add(state.Tick, state.ElapsedSeconds, SimulationMode.Realtime,
            SimulationEventKind.ResearchChanged, researchId, "started");
        return true;
    }

    public bool TryPurchaseWorkshop(SimulationState state, string workshopId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(workshopId);
        if (definitions is null || !definitions.Workshop.TryGetValue(workshopId, out var definition))
            return false;
        if (state.PurchasedWorkshops.Contains(workshopId) ||
            definitions.TechIndex(definition.TechLevel) > definitions.TechIndex(state.TechLevel) ||
            definition.RequiredResearchIds.Any(id => !state.GetResearchState(id).Completed) ||
            definition.RequiredWorkshopIds.Any(id => !state.PurchasedWorkshops.Contains(id)) ||
            (!string.IsNullOrEmpty(definition.RequiredBuildingId) &&
                GetBuildingOwnedCount(state, definition.RequiredBuildingId) < ExpantaNum.One) ||
            (!string.IsNullOrEmpty(definition.RequiredSectorId) &&
                !state.GetSectorState(definition.RequiredSectorId).Occupied))
            return false;

        if (!state.TryPay(definitions.Amounts(definition.ResourceRequirements)))
            return false;
        state.SetWorkshop(workshopId, ExpantaNum.One);
        frame = null;
        state.Events?.Add(state.Tick, state.ElapsedSeconds, SimulationMode.Realtime,
            SimulationEventKind.WorkshopChanged, workshopId, "purchased");
        return true;
    }

    public bool TryPurchaseBuilding(SimulationState state, string buildingId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildingId);
        if (definitions is null || !definitions.Building.TryGetValue(buildingId, out var definition))
            return false;
        var existing = GetBuildingOwnedCount(state, buildingId);
        if (definitions.TechIndex(definition.TechLevel) > definitions.TechIndex(state.TechLevel) ||
            definition.RequiredResearchIds.Any(id => !state.GetResearchState(id).Completed) ||
            definition.RequiredWorkshopIds.Any(id => !state.PurchasedWorkshops.Contains(id)) ||
            state.TerritoryTotal < Parse(definition.SpaceCost) ||
            (definition.MaxAmount > 0 && existing >= new ExpantaNum(definition.MaxAmount)) ||
            (!string.IsNullOrEmpty(definition.SectorId) &&
                (!state.GetSectorState(definition.SectorId).Occupied ||
                 (definition.RequiresInteriorExploration && state.GetSectorState(definition.SectorId).InteriorExplorationProgress < ExpantaNum.One))))
            return false;

        var modifiers = BuildModifiers(state);
        var constructionMultiplier = modifiers.BuildingConstructionMultiplier(buildingId);
        var costs = new Dictionary<string, ExpantaNum>(StringComparer.Ordinal);
        foreach (var requirement in definitions.Amounts(definition.ResourceRequirements))
        {
            var total = SimulationState.GeometricCost(
                requirement.Value,
                Parse(definition.CostGrowth),
                existing,
                1) / constructionMultiplier;
            costs[requirement.Key] = costs.TryGetValue(requirement.Key, out var current) ? current + total : total;
        }
        if (!state.TryPay(costs))
            return false;
        state.SetBuilding(buildingId, new ExpantaNum(existing + 1));
        frame = null;
        state.Events?.Add(state.Tick, state.ElapsedSeconds, SimulationMode.Realtime,
            SimulationEventKind.BuildingChanged, buildingId, "purchased", new ExpantaNum(existing + 1));
        return true;
    }

    private void ApplySimplePhase(string system, SimulationState state, SimulationTickContext context)
    {
        if (system == SimulationSystems.Resource)
        {
            foreach (var item in simpleProduction)
                state.AddResource(item.Value.output, item.Value.rate * context.DeltaSeconds);
            foreach (var item in simpleConsumption)
                state.ConsumeResource(item.Key, item.Value * context.DeltaSeconds);
        }
        else if (system == SimulationSystems.Research)
        {
            foreach (var item in simpleResearch.Where(item => !simpleActiveResearch.Contains(item.Key)))
            {
                simpleActiveResearch.Add(item.Key);
                state.SetResearch(item.Key, "Researching");
            }
            foreach (var id in simpleActiveResearch.ToArray())
            {
                var item = simpleResearch[id];
                var next = state.GetResearchState(id).Progress + item.power * context.DeltaSeconds;
                state.SetResearchState(id, next, true, next >= item.cost);
                if (next >= item.cost)
                    simpleActiveResearch.Remove(id);
            }
        }
        context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode, SimulationEventKind.Diagnostic, system);
    }

    private void EnsureFrame(SimulationState state, SimulationTickContext context)
    {
        if (frame is not null && frame.Tick == context.Tick)
            return;

        var modifiers = BuildModifiers(state);
        var active = state.BuildingStates.Values.Where(item => item.Amount > ExpantaNum.Zero).ToArray();
        foreach (var building in active)
            state.SetBuildingEfficiency(building.Id, ExpantaNum.One);

        var maximumPasses = Math.Max(1, active.Length + 1);
        var resourceSatisfaction = new Dictionary<string, ExpantaNum>(StringComparer.Ordinal);
        ExpantaNum foodAvailability = ExpantaNum.One;
        ExpantaNum happiness = ExpantaNum.One;
        ExpantaNum powerSatisfaction = ExpantaNum.One;
        ExpantaNum logisticsSatisfaction = ExpantaNum.One;
        var foodDemand = state.Population * new ExpantaNum(0.8d);
        foreach (var building in active)
            foodDemand += building.Amount * Parse(definitions!.Building[building.Id].FoodConsumptionRate);

        for (var pass = 0; pass < maximumPasses; pass++)
        {
            var actualProduction = new SortedDictionary<string, ExpantaNum>(StringComparer.Ordinal);
            foreach (var resource in definitions!.Resources.Values)
                Add(actualProduction, resource.Id, Parse(resource.BaseProductionRate) * ExpantaNum.Max(ExpantaNum.One, happiness));
            var potentialConsumption = new SortedDictionary<string, ExpantaNum>(StringComparer.Ordinal);
            var potentialFoodProduction = ExpantaNum.Zero;
            var potentialFoodConsumption = ExpantaNum.Zero;
            var potentialPowerProduction = ExpantaNum.Zero;
            var potentialPowerConsumption = ExpantaNum.Zero;
            var potentialLogisticsProduction = ExpantaNum.Zero;
            var potentialLogisticsConsumption = ExpantaNum.Zero;

            foreach (var buildingState in active)
            {
                var definition = definitions!.Building[buildingState.Id];
                var actualScale = buildingState.Amount * buildingState.Efficiency;
                var potentialScale = buildingState.Amount;
                var productionMultiplier = modifiers.BuildingProductionMultiplier(buildingState.Id);

                foreach (var rate in definitions.Amounts(definition.ResourceGenerationRates))
                    Add(actualProduction, rate.Key, actualScale * rate.Value *
                        productionMultiplier * modifiers.ResourceProductionMultiplier(rate.Key) *
                        modifiers.BuildingResourceProductionMultiplier(buildingState.Id, rate.Key));
                foreach (var rate in definitions.Amounts(definition.ResourceConsumptionRates))
                    Add(potentialConsumption, rate.Key, potentialScale * rate.Value * productionMultiplier);

                potentialFoodProduction += actualScale * Parse(definition.FoodProductionRate) * productionMultiplier;
                potentialFoodConsumption += potentialScale * Parse(definition.FoodConsumptionRate);
                var power = Parse(definition.PowerContribution);
                if (power > ExpantaNum.Zero)
                    potentialPowerProduction += actualScale * power * modifiers.PowerMultiplier *
                        modifiers.BuildingPowerProductionMultiplier(buildingState.Id) * ExpantaNum.Max(ExpantaNum.One, happiness);
                else if (power < ExpantaNum.Zero)
                    potentialPowerConsumption += potentialScale * -power;
                var logistics = Parse(definition.LogisticsContribution);
                if (logistics > ExpantaNum.Zero)
                    potentialLogisticsProduction += actualScale * logistics * modifiers.GlobalLogisticsMultiplier *
                        modifiers.BuildingLogisticsMultiplier(buildingState.Id) * ExpantaNum.Max(ExpantaNum.One, happiness);
                else if (logistics < ExpantaNum.Zero)
                    potentialLogisticsConsumption += potentialScale * -logistics;
            }

            foreach (var sector in state.SectorStates.Values.Where(item => item.Occupied))
            {
                foreach (var rate in definitions!.Amounts(definitions.Sector[sector.Id].OccupiedResourceRatesPerSecond))
                    Add(actualProduction, rate.Key, rate.Value * modifiers.OccupiedResourceMultiplier(rate.Key));
            }

            var populationFood = state.Population * new ExpantaNum(0.8d);
            potentialFoodProduction = (potentialFoodProduction + new ExpantaNum(5d)) *
                modifiers.GlobalFoodProductionMultiplier;
            potentialFoodConsumption += populationFood;
            foodAvailability = CalculateFoodAvailability(
                state.Food, potentialFoodProduction, potentialFoodConsumption, context.DeltaSeconds);

            var actualFoodProduction = ExpantaNum.Zero;
            var actualFoodConsumption = populationFood;
            foreach (var buildingState in active)
            {
                actualFoodProduction += buildingState.Amount * buildingState.Efficiency *
                    Parse(definitions!.Building[buildingState.Id].FoodProductionRate) *
                    modifiers.BuildingProductionMultiplier(buildingState.Id);
                actualFoodConsumption += buildingState.Amount * buildingState.Efficiency *
                Parse(definitions!.Building[buildingState.Id].FoodConsumptionRate);
            }
            actualFoodProduction = (actualFoodProduction + new ExpantaNum(5d)) *
                modifiers.GlobalFoodProductionMultiplier;
            happiness = HappinessFormula.CalculateMultiplier(
                actualFoodProduction - actualFoodConsumption,
                state.Population,
                foodAvailability,
                modifiers.HappinessBonus);
            powerSatisfaction = CalculateFlowSatisfaction(potentialPowerProduction, potentialPowerConsumption);
            logisticsSatisfaction = CalculateFlowSatisfaction(
                potentialLogisticsProduction, potentialLogisticsConsumption);

            resourceSatisfaction.Clear();
            foreach (var resource in definitions!.Resources.Keys)
            {
                actualProduction.TryGetValue(resource, out var production);
                potentialConsumption.TryGetValue(resource, out var consumption);
                resourceSatisfaction[resource] = CalculateSatisfaction(
                    state.GetResource(resource), production, consumption, context.DeltaSeconds);
            }

            var changed = false;
            foreach (var buildingState in active)
            {
                var definition = definitions.Building[buildingState.Id];
                var efficiency = ExpantaNum.One;
                foreach (var rate in definitions.Amounts(definition.ResourceConsumptionRates))
                    efficiency = ExpantaNum.Min(efficiency, resourceSatisfaction[rate.Key]);
                if (Parse(definition.PowerContribution) < ExpantaNum.Zero)
                    efficiency = ExpantaNum.Min(efficiency, powerSatisfaction);
                if (Parse(definition.LogisticsContribution) < ExpantaNum.Zero)
                    efficiency = ExpantaNum.Min(efficiency, logisticsSatisfaction);
                var foodConstraintRequired = Parse(definition.PowerContribution) >= ExpantaNum.Zero ||
                    Parse(definition.FoodConsumptionRate) > ExpantaNum.Zero;
                if (foodConstraintRequired)
                    efficiency = ExpantaNum.Min(efficiency, ExpantaNum.Min(ExpantaNum.One, happiness));
                if (buildingState.Efficiency == efficiency)
                    continue;
                state.SetBuildingEfficiency(buildingState.Id, efficiency);
                changed = true;
            }
            if (!changed)
                break;
        }

        happiness = EnforceTickResourceBudget(state, active, modifiers, foodDemand, context.DeltaSeconds);

        var resourceProduction = new SortedDictionary<string, ExpantaNum>(StringComparer.Ordinal);
        foreach (var resource in definitions!.Resources.Values)
            Add(resourceProduction, resource.Id, Parse(resource.BaseProductionRate));
        var resourceConsumption = new SortedDictionary<string, ExpantaNum>(StringComparer.Ordinal);
        var foodProduction = new ExpantaNum(5d);
        var foodConsumption = state.Population * new ExpantaNum(0.8d);
        var powerProduction = ExpantaNum.Zero;
        var powerConsumption = ExpantaNum.Zero;
        var logisticsProduction = ExpantaNum.Zero;
        var logisticsConsumption = ExpantaNum.Zero;
        var researchPower = new ExpantaNum(10d);
        var foodCapacity = new ExpantaNum(500d);
        var populationCapacity = ExpantaNum.Zero;

        foreach (var buildingState in active)
        {
            var definition = definitions!.Building[buildingState.Id];
            var scale = buildingState.Amount * buildingState.Efficiency;
            var productionMultiplier = modifiers.BuildingProductionMultiplier(buildingState.Id);
            foreach (var rate in definitions.Amounts(definition.ResourceGenerationRates))
                Add(resourceProduction, rate.Key, scale * rate.Value * productionMultiplier *
                    modifiers.ResourceProductionMultiplier(rate.Key) *
                    modifiers.BuildingResourceProductionMultiplier(buildingState.Id, rate.Key));
            foreach (var rate in definitions.Amounts(definition.ResourceConsumptionRates))
                Add(resourceConsumption, rate.Key, scale * rate.Value * productionMultiplier);

            foodProduction += scale * Parse(definition.FoodProductionRate) * productionMultiplier;
            foodConsumption += scale * Parse(definition.FoodConsumptionRate);
            foodCapacity += scale * Parse(definition.FoodCapacityGranted);
            populationCapacity += buildingState.Amount * Parse(definition.PopulationCapacityGranted);
            var power = Parse(definition.PowerContribution);
            if (power > ExpantaNum.Zero)
                powerProduction += scale * power * modifiers.PowerMultiplier * modifiers.BuildingPowerProductionMultiplier(buildingState.Id);
            else if (power < ExpantaNum.Zero)
                powerConsumption += scale * -power;
            var logistics = Parse(definition.LogisticsContribution);
            if (logistics > ExpantaNum.Zero)
                logisticsProduction += scale * logistics * modifiers.GlobalLogisticsMultiplier * modifiers.BuildingLogisticsMultiplier(buildingState.Id);
            else if (logistics < ExpantaNum.Zero)
                logisticsConsumption += scale * -logistics;
            researchPower += buildingState.Amount * Parse(definition.ResearchPowerGranted) *
                buildingState.Efficiency * modifiers.BuildingResearchPowerMultiplier(buildingState.Id);
        }

        foodProduction *= modifiers.GlobalFoodProductionMultiplier;
        foodAvailability = CalculateFoodAvailability(state.Food, foodProduction, foodDemand, context.DeltaSeconds);
        happiness = HappinessFormula.CalculateMultiplier(foodProduction - foodConsumption, state.Population,
            foodAvailability, modifiers.HappinessBonus);
        foodCapacity *= modifiers.FoodCapacityMultiplier;
        state.SetFoodCapacity(foodCapacity);
        state.SetPopulationCapacity(populationCapacity);
        state.SetFlowSatisfaction(state.SupplySatisfaction, powerSatisfaction, logisticsSatisfaction);

        frame = new TickFrame(
            context.Tick,
            context.DeltaSeconds,
            modifiers,
            resourceProduction,
            resourceConsumption,
            foodProduction,
            foodConsumption,
            foodAvailability,
            happiness,
            powerProduction,
            powerConsumption,
            logisticsProduction,
            logisticsConsumption,
            researchPower * modifiers.GlobalResearchMultiplier,
            active.Length,
            state.Population);
    }

    private ExpantaNum EnforceTickResourceBudget(SimulationState state,
        BuildingRuntimeState[] active, SimulationModifiers modifiers,
        ExpantaNum foodDemand, double seconds)
    {
        var production = new Dictionary<string, ExpantaNum>(StringComparer.Ordinal);
        var consumption = new Dictionary<string, ExpantaNum>(StringComparer.Ordinal);
        var external = new Dictionary<string, ExpantaNum>(StringComparer.Ordinal);
        foreach (var sector in state.SectorStates.Values.Where(item => item.Occupied))
            foreach (var rate in definitions!.Amounts(definitions.Sector[sector.Id].OccupiedResourceRatesPerSecond))
                Add(external, rate.Key, rate.Value * modifiers.OccupiedResourceMultiplier(rate.Key));

        ExpantaNum foodNet = ExpantaNum.Zero;
        ExpantaNum happiness = ExpantaNum.One;
        for (var correction = 0; correction <= definitions!.Resources.Count + 2; correction++)
        {
            production.Clear();
            foreach (var resource in definitions!.Resources.Values)
                Add(production, resource.Id, Parse(resource.BaseProductionRate));
            consumption.Clear();
            foodNet = new ExpantaNum(5d) * modifiers.GlobalFoodProductionMultiplier -
                state.Population * new ExpantaNum(0.8d);
            var foodProduction = new ExpantaNum(5d) * modifiers.GlobalFoodProductionMultiplier;
            foreach (var building in active)
                Accumulate(building, production, consumption, ref foodNet, ref foodProduction);
            var foodAvailability = CalculateFoodAvailability(state.Food, foodProduction, foodDemand, seconds);
            happiness = HappinessFormula.CalculateMultiplier(foodNet, state.Population, foodAvailability, modifiers.HappinessBonus);
            if (seconds <= 0d || correction == definitions.Resources.Count + 2)
                return happiness;
            var reward = ExpantaNum.Max(ExpantaNum.One, happiness);
            string? deficit = null;
            foreach (var resource in definitions.Resources.Keys)
            {
                var available = state.GetResource(resource) +
                    (production.GetValueOrDefault(resource) * reward + external.GetValueOrDefault(resource)) * seconds;
                if (consumption.GetValueOrDefault(resource) * seconds > available)
                {
                    deficit = resource;
                    break;
                }
            }
            var powerProduction = active.Aggregate(ExpantaNum.Zero, (sum, item) => sum + Flow(item, true, true));
            var powerConsumption = active.Aggregate(ExpantaNum.Zero, (sum, item) => sum + Flow(item, true, false));
            var logisticsProduction = active.Aggregate(ExpantaNum.Zero, (sum, item) => sum + Flow(item, false, true));
            var logisticsConsumption = active.Aggregate(ExpantaNum.Zero, (sum, item) => sum + Flow(item, false, false));
            var powerChanges = powerConsumption > powerProduction * reward;
            var logisticsChanges = logisticsConsumption > logisticsProduction * reward;
            if (deficit is null && !powerChanges && !logisticsChanges)
                return happiness;

            var affected = new HashSet<BuildingRuntimeState>();
            var resources = new HashSet<string>(StringComparer.Ordinal);
            if (deficit is not null) resources.Add(deficit);
            var foodChanges = false;
            bool changed;
            do
            {
                changed = false;
                foreach (var building in active)
                {
                    if (affected.Contains(building) || building.Efficiency <= ExpantaNum.Zero)
                        continue;
                    var definition = definitions.Building[building.Id];
                    var flowAffected = powerChanges && Parse(definition.PowerContribution) < ExpantaNum.Zero ||
                        logisticsChanges && Parse(definition.LogisticsContribution) < ExpantaNum.Zero;
                    if (!flowAffected && !definitions.Amounts(definition.ResourceConsumptionRates).Any(rate =>
                            rate.Value > ExpantaNum.Zero && (foodChanges || resources.Contains(rate.Key))))
                        continue;
                    affected.Add(building);
                    changed = true;
                    foreach (var rate in definitions.Amounts(definition.ResourceConsumptionRates))
                        if (rate.Value > ExpantaNum.Zero) resources.Add(rate.Key);
                    foreach (var rate in definitions.Amounts(definition.ResourceGenerationRates))
                        if (rate.Value > ExpantaNum.Zero) resources.Add(rate.Key);
                    foodChanges |= Parse(definition.FoodProductionRate) * modifiers.BuildingProductionMultiplier(building.Id) *
                        modifiers.GlobalFoodProductionMultiplier - Parse(definition.FoodConsumptionRate) != ExpantaNum.Zero;
                    powerChanges |= Parse(definition.PowerContribution) > ExpantaNum.Zero;
                    logisticsChanges |= Parse(definition.LogisticsContribution) > ExpantaNum.Zero;
                }
            } while (changed);

            var affectedProduction = new Dictionary<string, ExpantaNum>(StringComparer.Ordinal);
            var affectedConsumption = new Dictionary<string, ExpantaNum>(StringComparer.Ordinal);
            var affectedFoodNet = ExpantaNum.Zero;
            var affectedFoodProduction = ExpantaNum.Zero;
            foreach (var building in affected)
                Accumulate(building, affectedProduction, affectedConsumption, ref affectedFoodNet, ref affectedFoodProduction);
            var selectedPowerProduction = affected.Aggregate(ExpantaNum.Zero, (sum, item) => sum + Flow(item, true, true));
            var selectedPowerConsumption = affected.Aggregate(ExpantaNum.Zero, (sum, item) => sum + Flow(item, true, false));
            var selectedLogisticsProduction = affected.Aggregate(ExpantaNum.Zero, (sum, item) => sum + Flow(item, false, true));
            var selectedLogisticsConsumption = affected.Aggregate(ExpantaNum.Zero, (sum, item) => sum + Flow(item, false, false));
            var minimumReward = ExpantaNum.Min(reward, ExpantaNum.Max(ExpantaNum.One,
                HappinessFormula.CalculateMultiplier(foodNet - affectedFoodNet, state.Population,
                    CalculateFoodAvailability(state.Food, foodProduction - affectedFoodProduction, foodDemand, seconds),
                    modifiers.HappinessBonus)));
            ExpantaNum low = ExpantaNum.One, high = ExpantaNum.One;
            foreach (var resource in resources)
            {
                var available = state.GetResource(resource) / seconds + external.GetValueOrDefault(resource) +
                    (production.GetValueOrDefault(resource) - affectedProduction.GetValueOrDefault(resource)) * minimumReward -
                    (consumption.GetValueOrDefault(resource) - affectedConsumption.GetValueOrDefault(resource));
                var deficitRate = affectedConsumption.GetValueOrDefault(resource) -
                    affectedProduction.GetValueOrDefault(resource) * minimumReward;
                if (deficitRate > ExpantaNum.Zero)
                    low = ExpantaNum.Min(low, ExpantaNum.Clamp01(available / deficitRate));
            }
            low = LimitFlow(low, powerProduction, powerConsumption, selectedPowerProduction, selectedPowerConsumption);
            low = LimitFlow(low, logisticsProduction, logisticsConsumption, selectedLogisticsProduction, selectedLogisticsConsumption);
            var candidateProduction = new Dictionary<string, ExpantaNum>(StringComparer.Ordinal);
            var candidateConsumption = new Dictionary<string, ExpantaNum>(StringComparer.Ordinal);
            if (!Fits(low)) low *= new ExpantaNum(0.99999d);
            if (!Fits(low)) low = ExpantaNum.Zero;
            for (var step = 0; step < 36; step++)
            {
                var middle = (low + high) * 0.5d;
                if (Fits(middle)) low = middle;
                else high = middle;
            }
            foreach (var building in affected)
                state.SetBuildingEfficiency(building.Id, building.Efficiency * low);

            bool Fits(ExpantaNum candidate)
            {
                candidateProduction.Clear();
                candidateConsumption.Clear();
                var candidateFood = ExpantaNum.Zero;
                var candidateFoodProduction = ExpantaNum.Zero;
                var candidatePowerProduction = ExpantaNum.Zero;
                var candidatePowerConsumption = ExpantaNum.Zero;
                var candidateLogisticsProduction = ExpantaNum.Zero;
                var candidateLogisticsConsumption = ExpantaNum.Zero;
                foreach (var building in affected)
                {
                    Accumulate(building, candidateProduction, candidateConsumption, ref candidateFood,
                        ref candidateFoodProduction, candidate);
                    candidatePowerProduction += Flow(building, true, true, candidate);
                    candidatePowerConsumption += Flow(building, true, false, candidate);
                    candidateLogisticsProduction += Flow(building, false, true, candidate);
                    candidateLogisticsConsumption += Flow(building, false, false, candidate);
                }
                var candidateReward = ExpantaNum.Max(ExpantaNum.One, HappinessFormula.CalculateMultiplier(
                    (foodNet - affectedFoodNet) + candidateFood, state.Population,
                    CalculateFoodAvailability(state.Food, foodProduction - affectedFoodProduction + candidateFoodProduction,
                        foodDemand, seconds),
                    modifiers.HappinessBonus));
                foreach (var resource in resources)
                {
                    var available = state.GetResource(resource) +
                        (external.GetValueOrDefault(resource) + (production.GetValueOrDefault(resource) -
                            affectedProduction.GetValueOrDefault(resource) +
                            candidateProduction.GetValueOrDefault(resource)) * candidateReward) * seconds;
                    var demand = (consumption.GetValueOrDefault(resource) -
                        affectedConsumption.GetValueOrDefault(resource) +
                        candidateConsumption.GetValueOrDefault(resource)) * seconds;
                    if (demand > available) return false;
                }
                return powerConsumption - selectedPowerConsumption + candidatePowerConsumption <=
                        (powerProduction - selectedPowerProduction + candidatePowerProduction) * candidateReward &&
                    logisticsConsumption - selectedLogisticsConsumption + candidateLogisticsConsumption <=
                        (logisticsProduction - selectedLogisticsProduction + candidateLogisticsProduction) * candidateReward;
            }

            ExpantaNum LimitFlow(ExpantaNum candidate, ExpantaNum output, ExpantaNum input,
                ExpantaNum selectedOutput, ExpantaNum selectedInput)
            {
                var deficitRate = selectedInput - selectedOutput * minimumReward;
                return deficitRate > ExpantaNum.Zero ? ExpantaNum.Min(candidate, ExpantaNum.Clamp01(
                    ((output - selectedOutput) * minimumReward - (input - selectedInput)) / deficitRate)) : candidate;
            }
        }
        return happiness;

        ExpantaNum Flow(BuildingRuntimeState building, bool power, bool output, ExpantaNum? candidate = null)
        {
            var definition = definitions.Building[building.Id];
            var scale = candidate.HasValue ? building.Amount * (building.Efficiency * candidate.Value) :
                building.Amount * building.Efficiency;
            var contribution = Parse(power ? definition.PowerContribution : definition.LogisticsContribution);
            if (!output)
                return contribution < ExpantaNum.Zero ? scale * -contribution : ExpantaNum.Zero;
            if (contribution <= ExpantaNum.Zero)
                return ExpantaNum.Zero;
            return scale * contribution * (power
                ? modifiers.PowerMultiplier * modifiers.BuildingPowerProductionMultiplier(building.Id)
                : modifiers.GlobalLogisticsMultiplier * modifiers.BuildingLogisticsMultiplier(building.Id));
        }

        void Accumulate(BuildingRuntimeState building, Dictionary<string, ExpantaNum> output,
            Dictionary<string, ExpantaNum> input, ref ExpantaNum netFood, ref ExpantaNum foodOutput,
            ExpantaNum? candidate = null)
        {
            var definition = definitions.Building[building.Id];
            var scale = candidate.HasValue ? building.Amount * (building.Efficiency * candidate.Value) :
                building.Amount * building.Efficiency;
            var multiplier = modifiers.BuildingProductionMultiplier(building.Id);
            foreach (var rate in definitions.Amounts(definition.ResourceGenerationRates))
                Add(output, rate.Key, scale * rate.Value * multiplier *
                    modifiers.ResourceProductionMultiplier(rate.Key) *
                    modifiers.BuildingResourceProductionMultiplier(building.Id, rate.Key));
            foreach (var rate in definitions.Amounts(definition.ResourceConsumptionRates))
                Add(input, rate.Key, scale * rate.Value * multiplier);
            netFood += scale * (Parse(definition.FoodProductionRate) * multiplier *
                modifiers.GlobalFoodProductionMultiplier - Parse(definition.FoodConsumptionRate));
            foodOutput += scale * Parse(definition.FoodProductionRate) * multiplier *
                modifiers.GlobalFoodProductionMultiplier;
        }
    }

    private void ApplyGame(SimulationState state, SimulationTickContext context, TickFrame frame)
    {
        var beforeFood = state.Food;
        var fedSeconds = context.DeltaSeconds;
        var deficit = frame.FoodConsumption - frame.FoodProduction;
        if (deficit > ExpantaNum.Zero && beforeFood < deficit * context.DeltaSeconds)
            fedSeconds = Math.Min(context.DeltaSeconds, Math.Max(0d, (beforeFood / deficit).ToDouble()));
        state.AdvanceFood(frame.FoodProduction, frame.FoodConsumption, context.DeltaSeconds);
        if (state.Food != beforeFood)
            context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode,
                SimulationEventKind.FoodChanged, "Food", "advanced", state.Food - beforeFood);

        var beforePopulation = state.Population;
        var growthRate = new ExpantaNum(1d / 60d) * frame.Modifiers.PopulationGrowthMultiplier;
        state.AdvancePopulation(fedSeconds, CurrentHappiness(state, frame), growthRate, false);
        var unfedSeconds = context.DeltaSeconds - fedSeconds;
        if (unfedSeconds > 0d)
            state.AdvancePopulation(unfedSeconds, CurrentHappiness(state, frame), growthRate, true);
        if (state.Population != beforePopulation)
            context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode,
                SimulationEventKind.Diagnostic, "Population", "changed", state.Population - beforePopulation);
    }

    private void ApplyOccupiedSectors(SimulationState state, SimulationTickContext context, TickFrame frame)
    {
        foreach (var sector in state.SectorStates.Values.Where(item => item.Occupied))
        {
            foreach (var rate in definitions!.Amounts(
                         definitions.Sector[sector.Id].OccupiedResourceRatesPerSecond))
            {
                var amount = rate.Value * context.DeltaSeconds *
                    frame.Modifiers.OccupiedResourceMultiplier(rate.Key);
                if (amount <= ExpantaNum.Zero)
                    continue;
                var before = state.GetResource(rate.Key);
                state.AddResource(rate.Key, amount);
                context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode,
                    SimulationEventKind.ResourceChanged, rate.Key, "sector-production",
                    state.GetResource(rate.Key) - before);
            }
        }
    }

    private void ApplyResources(SimulationState state, SimulationTickContext context, TickFrame frame)
    {
        foreach (var id in definitions!.Resources.Keys)
        {
            frame.ResourceProduction.TryGetValue(id, out var production);
            frame.ResourceConsumption.TryGetValue(id, out var consumption);
            production *= ExpantaNum.Max(ExpantaNum.One, frame.Happiness);
            var before = state.GetResource(id);
            var next = ExpantaNum.Max(
                ExpantaNum.Zero,
                before + (production - consumption) * context.DeltaSeconds);
            state.SetResource(id, next);
            if (next != before)
                context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode,
                    SimulationEventKind.ResourceChanged, id, "advanced", next - before);
        }
    }

    private void ApplyColonization(SimulationState state, SimulationTickContext context, TickFrame frame)
    {
        foreach (var sectorState in state.SectorStates.Values.Where(item => item.ColonizationActive))
        {
            var definition = definitions!.Sector[sectorState.Id];
            var duration = ExpantaNum.Max(ExpantaNum.One, Parse(definition.ColonizationDurationSeconds)) /
                frame.Modifiers.ExplorationPowerMultiplier;
            var remaining = ExpantaNum.Max(ExpantaNum.Zero, ExpantaNum.One - sectorState.Progress);
            var completionSeconds = (remaining * duration).ToDouble();
            var billableSeconds = Math.Min(context.DeltaSeconds, Math.Max(0d, completionSeconds));
            var foodCost = Parse(definition.ColonizationFoodPerSecond) * billableSeconds;
            var resourceCosts = definitions.Amounts(definition.ColonizationResourceRatesPerSecond)
                .ToDictionary(item => item.Key, item => item.Value * billableSeconds, StringComparer.Ordinal);
            if (state.Food < foodCost || !state.TryPay(resourceCosts))
                continue;

            state.SetFood(state.Food - foodCost);
            var nextProgress = ExpantaNum.Clamp01(
                sectorState.Progress + new ExpantaNum(billableSeconds) / duration);
            state.SetSectorState(
                sectorState.Id,
                sectorState.Unlocked,
                nextProgress >= ExpantaNum.One,
                nextProgress < ExpantaNum.One,
                false,
                nextProgress,
                sectorState.Casualties,
                sectorState.CombatRatio,
                sectorState.VisitCount + (nextProgress >= ExpantaNum.One ? 1 : 0));
            context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode,
                SimulationEventKind.SectorChanged, sectorState.Id, "colonization", nextProgress);
            if (nextProgress >= ExpantaNum.One)
                ApplySectorRewards(state, context, definition);
        }

        foreach (var sectorState in state.SectorStates.Values.Where(item =>
                     !item.Occupied && !item.ColonizationActive && item.Progress > ExpantaNum.Zero &&
                     IsHomeSector(definitions!.Sector[item.Id])))
            DecaySector(state, context, sectorState, 0.005d / 60d, context.DeltaSeconds);
    }

    public bool TryStartInteriorExploration(SimulationState state, string sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (definitions == null || !definitions.Sector.TryGetValue(sectorId, out var definition) ||
            definition.InteriorExploration == null) return false;
        var sector = state.GetSectorState(sectorId);
        if (!sector.Occupied || sector.InteriorExplorationActive ||
            sector.InteriorExplorationProgress >= ExpantaNum.One ||
            definition.InteriorExploration.RequiredResearchIds.Any(id => !state.GetResearchState(id).Completed))
            return false;
        state.SetInteriorExplorationState(sectorId, true, sector.InteriorExplorationProgress);
        return true;
    }

    public bool TryPauseInteriorExploration(SimulationState state, string sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (definitions == null || !definitions.Sector.ContainsKey(sectorId)) return false;
        var sector = state.GetSectorState(sectorId);
        if (!sector.InteriorExplorationActive) return false;
        state.SetInteriorExplorationState(sectorId, false, sector.InteriorExplorationProgress, true);
        return true;
    }

    private void ApplyInteriorExploration(SimulationState state, SimulationTickContext context)
    {
        // SectorStates is ordinally sorted, matching Runtime's shared-supply order.
        foreach (var sector in state.SectorStates.Values.Where(item => item.InteriorExplorationActive))
        {
            var exploration = definitions!.Sector[sector.Id].InteriorExploration;
            if (!sector.Occupied || exploration == null ||
                exploration.RequiredResearchIds.Any(id => !state.GetResearchState(id).Completed)) continue;
            var duration = Parse(exploration.DurationSeconds);
            var seconds = ExpantaNum.Min(new ExpantaNum(context.DeltaSeconds),
                (ExpantaNum.One - sector.InteriorExplorationProgress) * duration);
            var rates = definitions.Amounts(exploration.ResourceRatesPerSecond);
            foreach (var rate in rates)
                if (rate.Value > ExpantaNum.Zero)
                    seconds = ExpantaNum.Min(seconds, state.GetResource(rate.Key) / rate.Value);
            if (seconds <= ExpantaNum.Zero) continue;
            var costs = rates.ToDictionary(item => item.Key, item => item.Value * seconds, StringComparer.Ordinal);
            if (!state.TryPay(costs)) continue;
            var progress = seconds >= (ExpantaNum.One - sector.InteriorExplorationProgress) * duration
                ? ExpantaNum.One : ExpantaNum.Clamp01(sector.InteriorExplorationProgress + seconds / duration);
            state.SetInteriorExplorationState(sector.Id, progress < ExpantaNum.One, progress);
            context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode,
                SimulationEventKind.SectorChanged, sector.Id, "interior-exploration", progress);
        }
    }

    private void ApplyCampaign(SimulationState state, SimulationTickContext context, TickFrame frame)
    {
        foreach (var sectorState in state.SectorStates.Values.Where(item => item.CampaignActive))
        {
            var definition = definitions!.Sector[sectorState.Id];
            var combatRatio = CalculateCombatRatio(state, sectorState, frame);
            var progressRate = CalculateCampaignProgressRate(combatRatio) *
                ExpantaNum.Clamp01(
                    Parse(definition.CampaignProgressMultiplier) *
                    frame.Modifiers.CampaignProgressMultiplier);
            var completionSeconds = progressRate > ExpantaNum.Zero
                ? ((ExpantaNum.One - sectorState.Progress) / progressRate).ToDouble()
                : context.DeltaSeconds;
            var billableSeconds = progressRate > ExpantaNum.Zero
                ? Math.Min(context.DeltaSeconds, Math.Max(0d, completionSeconds))
                : context.DeltaSeconds;
            var supplyMultiplier = frame.Modifiers.CampaignSupplyCostMultiplier;
            var foodCost = Parse(definition.CampaignFoodPerSecond) * supplyMultiplier * billableSeconds;
            var resourceCosts = definitions.Amounts(definition.CampaignResourceRatesPerSecond)
                .ToDictionary(
                    item => item.Key,
                    item => item.Value * supplyMultiplier * billableSeconds,
                    StringComparer.Ordinal);
            if (state.Food < foodCost || !state.TryPay(resourceCosts))
                continue;

            state.SetFood(state.Food - foodCost);
            var nextProgress = ExpantaNum.Clamp01(
                sectorState.Progress + progressRate * billableSeconds);
            var casualties = CalculateCampaignCasualties(
                combatRatio,
                state.DefensePower,
                Parse(definition.EnemyPower),
                billableSeconds,
                frame.Modifiers.CampaignCasualtyMultiplier);
            state.SetSectorState(
                sectorState.Id,
                sectorState.Unlocked,
                nextProgress >= ExpantaNum.One,
                false,
                nextProgress < ExpantaNum.One,
                nextProgress,
                sectorState.Casualties + casualties,
                combatRatio,
                sectorState.VisitCount + (nextProgress >= ExpantaNum.One ? 1 : 0));
            if (!state.CampaignActive)
                state.SetCampaign(true, sectorState.Id, casualties, combatRatio);
            else
                state.SetCampaignProgress(nextProgress);
            context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode,
                SimulationEventKind.CampaignChanged, sectorState.Id, "advanced", nextProgress);
            if (nextProgress >= ExpantaNum.One)
            {
                state.SetCampaign(false, "", ExpantaNum.Zero, ExpantaNum.Zero);
                ApplySectorRewards(state, context, definition);
            }
        }

        foreach (var sectorState in state.SectorStates.Values.Where(item =>
                     !item.Occupied && !item.CampaignActive && item.Progress > ExpantaNum.Zero &&
                     !IsHomeSector(definitions!.Sector[item.Id])))
            DecaySector(state, context, sectorState, 0.02d / 60d, context.DeltaSeconds);
    }

    private void ApplyResearch(SimulationState state, SimulationTickContext context, TickFrame frame)
    {
        var remainingSeconds = context.DeltaSeconds;
        while (remainingSeconds > 0d)
        {
            TryStartNextQueuedResearch(state, context);

            if (state.ActiveResearch is not { } researchId)
                return;
            var researchState = state.GetResearchState(researchId);
            if (!researchState.CostPaid)
                return;

            var research = definitions!.Research[researchId];
            researchState = state.GetResearchState(researchId);
            var speed = ResearchSpeedEffect(state.TechLevel, research.TechLevel) *
                frame.ResearchPower * CurrentHappiness(state, frame);
            if (speed <= ExpantaNum.Zero)
                return;
            var remainingProgress = ExpantaNum.Max(
                ExpantaNum.Zero,
                Parse(research.BaseCost) - researchState.Progress);
            var cost = Parse(research.BaseCost);
            var nextProgress = ExpantaNum.Min(cost, researchState.Progress + speed * remainingSeconds);
            // Completion is established before the time conversion, as in Runtime TickOffline.
            if (nextProgress >= cost && context.Mode == SimulationMode.Offline && !context.UseRealtimeResearchCadence)
                remainingSeconds -= Math.Min(remainingSeconds, Math.Max(0d, (remainingProgress / speed).ToDouble()));
            else
                remainingSeconds = 0d;
            state.SetResearchState(
                researchId,
                nextProgress,
                true,
                false,
                researchState.PaidResourceCosts);
            context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode,
                SimulationEventKind.ResearchChanged, researchId, "progress", nextProgress);

            if (nextProgress < Parse(research.BaseCost))
                return;
            state.SetResearchState(
                researchId,
                Parse(research.BaseCost),
                true,
                true,
                researchState.PaidResourceCosts);
            state.SetActiveResearch(null);
            if (research.AdvancesTechLevel)
                state.SetTechLevel(research.TechLevel);
            context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode,
                SimulationEventKind.ResearchChanged, researchId, "completed");
            this.frame = null;
            EnsureFrame(state, context);
            frame = this.frame!;
            TryStartNextQueuedResearch(state, context);
        }
    }

    private void TryStartNextQueuedResearch(SimulationState state, SimulationTickContext context)
    {
        if (state.ActiveResearch is not null)
            return;
        while (state.ResearchQueue.FirstOrDefault() is { } completed &&
               state.GetResearchState(completed).Completed)
            state.DequeueResearch();
        var next = state.ResearchQueue.FirstOrDefault();
        if (next is null || !definitions!.Research.TryGetValue(next, out var definition) ||
            !CanAccessResearch(state, definition) || !AreResearchPrerequisitesCompleted(state, definition))
            return;
        var wasPaid = state.GetResearchState(next).CostPaid;
        if (!TryPayResearch(state, next))
            return;
        state.DequeueResearch();
        state.SetActiveResearch(next);
        if (!wasPaid)
            context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode,
                SimulationEventKind.ResearchChanged, next, "paid");
    }

    private bool TryPayResearch(SimulationState state, string researchId)
    {
        var research = definitions!.Research[researchId];
        var researchState = state.GetResearchState(researchId);
        if (researchState.CostPaid)
            return true;

        var remaining = new Dictionary<string, ExpantaNum>(StringComparer.Ordinal);
        foreach (var requirement in definitions.Amounts(research.ResourceRequirements))
        {
            researchState.PaidResourceCosts.TryGetValue(requirement.Key, out var paid);
            var amount = ExpantaNum.Max(ExpantaNum.Zero, requirement.Value - paid);
            if (amount > ExpantaNum.Zero)
                remaining[requirement.Key] = amount;
        }
        if (remaining.Count == 0)
        {
            state.SetResearchState(
                researchId,
                researchState.Progress,
                true,
                researchState.Completed,
                researchState.PaidResourceCosts);
            return true;
        }
        if (!state.TryPay(remaining))
            return false;

        foreach (var cost in remaining)
            researchState.PaidResourceCosts[cost.Key] = cost.Value;
        state.SetResearchState(
            researchId,
            researchState.Progress,
            true,
            researchState.Completed,
            researchState.PaidResourceCosts);
        return true;
    }

    private void ApplySectorRewards(
        SimulationState state,
        SimulationTickContext context,
        SectorSnapshot definition)
    {
        state.SetTerritoryTotal(state.TerritoryTotal + Parse(definition.TerritoryReward));
        foreach (var reward in definitions!.Amounts(definition.ResourceRewards))
        {
            var before = state.GetResource(reward.Key);
            state.AddResource(reward.Key, reward.Value);
            context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode,
                SimulationEventKind.ResourceChanged, reward.Key, "sector-reward",
                state.GetResource(reward.Key) - before);
        }
        context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode,
            SimulationEventKind.SectorChanged, definition.Id, "occupied");
    }

    private void DecaySector(
        SimulationState state,
        SimulationTickContext context,
        SectorRuntimeState sectorState,
        double ratePerSecond,
        double deltaSeconds)
    {
        var next = ExpantaNum.Clamp01(
            sectorState.Progress - new ExpantaNum(ratePerSecond * deltaSeconds));
        if (next == sectorState.Progress)
            return;
        state.SetSectorState(
            sectorState.Id,
            sectorState.Unlocked,
            sectorState.Occupied,
            sectorState.ColonizationActive,
            sectorState.CampaignActive,
            next,
            sectorState.Casualties,
            sectorState.CombatRatio,
            sectorState.VisitCount);
        context.Events.Add(context.Tick, state.ElapsedSeconds, context.Mode,
            SimulationEventKind.SectorChanged, sectorState.Id, "decayed", next);
    }

    private SimulationModifiers BuildModifiers(SimulationState state)
    {
        if (cachedModifiers is not null &&
            cachedModifiersProgressionVersion == state.ProgressionVersion)
            return cachedModifiers;

        var modifiers = new SimulationModifiers();
        foreach (var research in definitions!.Research.Values)
        {
            if (!state.GetResearchState(research.Id).Completed)
                continue;
            var allowCombat = definitions.TechIndex(research.TechLevel) >=
                definitions.TechIndex("Spacer");
            foreach (var effect in research.Effects)
                modifiers.ApplyResearchEffect(effect, allowCombat);
        }
        foreach (var workshop in definitions.Workshop.Values.Where(item =>
                     state.PurchasedWorkshops.Contains(item.Id)))
            foreach (var effect in workshop.Effects)
                modifiers.ApplyWorkshopEffect(effect);
        cachedModifiers = modifiers;
        cachedModifiersProgressionVersion = state.ProgressionVersion;
        return modifiers;
    }

    private bool CanAccessResearch(SimulationState state, ResearchSnapshot research)
    {
        var current = definitions!.TechIndex(state.TechLevel);
        var target = definitions.TechIndex(research.TechLevel);
        return research.AdvancesTechLevel ? target == current + 1 : current >= target;
    }

    private bool AreResearchPrerequisitesCompleted(SimulationState state, ResearchSnapshot research) =>
        research.PrerequisiteIds.All(id => state.GetResearchState(id).Completed);

    private ExpantaNum CalculateCombatRatio(
        SimulationState state,
        SectorRuntimeState sector,
        TickFrame frame)
    {
        var basePower = state.AttackPower + state.FleetPower;
        if (basePower <= ExpantaNum.Zero)
            return ExpantaNum.Zero;
        var manpowerFactor = ExpantaNum.Clamp01(state.MilitaryManpower / basePower);
        var readiness = ExpantaNum.One / (
            ExpantaNum.One +
            sector.Casualties / ExpantaNum.Max(ExpantaNum.One, state.FleetPower));
        var effectivePower = basePower * manpowerFactor * readiness * state.SupplySatisfaction *
            state.PowerSatisfaction * state.LogisticsSatisfaction * frame.Modifiers.MilitaryMultiplier;
        var enemy = Parse(definitions!.Sector[sector.Id].EnemyPower);
        return enemy <= ExpantaNum.Zero ? ExpantaNum.One : effectivePower / enemy;
    }

    private static ExpantaNum CalculateCampaignProgressRate(ExpantaNum combatRatio)
    {
        if (combatRatio < new ExpantaNum(0.7d))
            return ExpantaNum.Zero;
        if (combatRatio < ExpantaNum.One)
            return (combatRatio - new ExpantaNum(0.7d)) / new ExpantaNum(0.3d) *
                new ExpantaNum(0.25d) / new ExpantaNum(60d);
        if (combatRatio < new ExpantaNum(2d))
            return (new ExpantaNum(0.25d) +
                    (combatRatio - ExpantaNum.One) * new ExpantaNum(0.75d)) /
                new ExpantaNum(60d);
        return (ExpantaNum.One + (combatRatio - new ExpantaNum(2d)) /
            (combatRatio + new ExpantaNum(2d))) / new ExpantaNum(60d);
    }

    private static ExpantaNum CalculateCampaignCasualties(
        ExpantaNum combatRatio,
        ExpantaNum defensePower,
        ExpantaNum enemyPower,
        double deltaSeconds,
        ExpantaNum casualtyMultiplier)
    {
        ExpantaNum casualtyRate;
        if (combatRatio < new ExpantaNum(0.7d))
            casualtyRate = ExpantaNum.One / new ExpantaNum(60d);
        else if (combatRatio < ExpantaNum.One)
            casualtyRate = (ExpantaNum.One - combatRatio) /
                new ExpantaNum(0.3d) / new ExpantaNum(60d);
        else
            casualtyRate = ExpantaNum.Zero;
        var survival = new ExpantaNum(0.35d) +
            ExpantaNum.Clamp01(defensePower / ExpantaNum.Max(ExpantaNum.One, enemyPower)) *
            new ExpantaNum(0.65d);
        return casualtyRate / survival *
            ExpantaNum.Max(ExpantaNum.Zero, casualtyMultiplier) * deltaSeconds;
    }

    private static ExpantaNum CalculateFoodAvailability(
        ExpantaNum current,
        ExpantaNum production,
        ExpantaNum consumption,
        double deltaSeconds)
    {
        var demand = ExpantaNum.Max(ExpantaNum.Zero, consumption) * deltaSeconds;
        if (demand <= ExpantaNum.Zero)
            return ExpantaNum.One;
        var available = ExpantaNum.Max(ExpantaNum.Zero, current) +
            ExpantaNum.Max(ExpantaNum.Zero, production) * deltaSeconds;
        return ExpantaNum.Clamp01(available / demand);
    }

    private static ExpantaNum CurrentHappiness(SimulationState state, TickFrame frame) =>
        HappinessFormula.CalculateMultiplier(
            frame.FoodProduction - frame.FoodConsumption -
                (state.Population - frame.PreparedPopulation) * PopulationState.FoodConsumptionPerPerson,
            state.Population, frame.FoodAvailability, frame.Modifiers.HappinessBonus);

    private static ExpantaNum CalculateFlowSatisfaction(
        ExpantaNum production,
        ExpantaNum consumption)
    {
        var demand = ExpantaNum.Max(ExpantaNum.Zero, consumption);
        if (demand <= ExpantaNum.Zero)
            return ExpantaNum.One;
        return ExpantaNum.Clamp01(ExpantaNum.Max(ExpantaNum.Zero, production) / demand);
    }

    private static ExpantaNum CalculateSatisfaction(
        ExpantaNum inventory,
        ExpantaNum production,
        ExpantaNum consumption,
        double deltaSeconds)
    {
        var demand = ExpantaNum.Max(ExpantaNum.Zero, consumption) * deltaSeconds;
        if (demand <= ExpantaNum.Zero)
            return ExpantaNum.One;
        var available = ExpantaNum.Max(ExpantaNum.Zero, inventory) +
            ExpantaNum.Max(ExpantaNum.Zero, production) * deltaSeconds;
        return ExpantaNum.Clamp01(available / demand);
    }

    private static double ResearchSpeedEffect(string current, string target) =>
        string.Equals(current, target, StringComparison.Ordinal)
            ? 1d
            : 1d / Math.Abs(
                SnapshotDefinitionSet.TechIndexValue(target) -
                SnapshotDefinitionSet.TechIndexValue(current) + 0.5d);

    private static bool IsHomeSector(SectorSnapshot sector) =>
        string.Equals(sector.Domain, "HomeSystem", StringComparison.Ordinal);

    private static long GetBuildingOwnedCount(SimulationState state, string buildingId)
    {
        var amount = state.BuildingStates.TryGetValue(buildingId, out var building)
            ? building.Amount
            : ExpantaNum.Zero;
        if (amount < ExpantaNum.Zero || amount > new ExpantaNum(long.MaxValue))
            throw new InvalidOperationException($"Building count is outside the explicit purchase range: {buildingId}.");
        return (long)amount.ToDouble();
    }

    private static void Add(IDictionary<string, ExpantaNum> values, string id, ExpantaNum amount)
    {
        if (amount <= ExpantaNum.Zero)
            return;
        values[id] = values.TryGetValue(id, out var current) ? current + amount : amount;
    }

    private static ExpantaNum Parse(string value)
    {
        if (!ExpantaNum.TryParse(value ?? string.Empty, out var parsed) || !parsed.IsFinite)
            throw new InvalidDataException($"Invalid ExpantaNum '{value}'.");
        return parsed;
    }

    private sealed record TickFrame(
        long Tick,
        double DeltaSeconds,
        SimulationModifiers Modifiers,
        SortedDictionary<string, ExpantaNum> ResourceProduction,
        SortedDictionary<string, ExpantaNum> ResourceConsumption,
        ExpantaNum FoodProduction,
        ExpantaNum FoodConsumption,
        ExpantaNum FoodAvailability,
        ExpantaNum Happiness,
        ExpantaNum PowerProduction,
        ExpantaNum PowerConsumption,
        ExpantaNum LogisticsProduction,
        ExpantaNum LogisticsConsumption,
        ExpantaNum ResearchPower,
        long ActiveBuildings,
        ExpantaNum PreparedPopulation);
}

public sealed class SimulationCore
{
    public const double DefaultTickSeconds = 0.1d;
    public const double DefaultOfflineTickSeconds = 60d;
    private const double OfflineFullRateSeconds = 2d * 60d * 60d;
    private const double OfflineReducedRateEndSeconds = 8d * 60d * 60d;
    private const double OfflineMiddleRate = 0.60d;
    private const double OfflineLateRate = 0.25d;

    private readonly ISimulationRules rules;
    private readonly EraStepSchedule? eraStepSchedule;
    private double offlineElapsedSeconds;
    private double offlinePendingCalendarSeconds;
    private double offlinePendingSimulationSeconds;

    public SimulationState State { get; }
    public SimulationEventLog Events { get; } = new();
    public double TickSeconds { get; }
    public double OfflineTickSeconds { get; }
    public double OfflinePendingCalendarSeconds => offlinePendingCalendarSeconds;
    public double OfflinePendingSimulationSeconds => offlinePendingSimulationSeconds;

    public SimulationCore(
        SimulationState state,
        ISimulationRules? rules = null,
        double tickSeconds = DefaultTickSeconds,
        double offlineTickSeconds = DefaultOfflineTickSeconds,
        EraStepSchedule? eraStepSchedule = null)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        this.rules = rules ?? new DeterministicRules();
        if (!double.IsFinite(tickSeconds) || tickSeconds <= 0d)
            throw new ArgumentOutOfRangeException(nameof(tickSeconds));
        if (!double.IsFinite(offlineTickSeconds) || offlineTickSeconds <= 0d)
            throw new ArgumentOutOfRangeException(nameof(offlineTickSeconds));
        TickSeconds = tickSeconds;
        OfflineTickSeconds = offlineTickSeconds;
        this.eraStepSchedule = eraStepSchedule;
        State.Events = Events;
    }

    public static SimulationCore FromSnapshot(
        EconomySnapshot snapshot,
        double? tickSeconds = null,
        double offlineTickSeconds = DefaultOfflineTickSeconds,
        EraStepSchedule? eraStepSchedule = null)
    {
        var core = new SimulationCore(
            SimulationStateFactory.Create(snapshot),
            new SnapshotDrivenRules(snapshot),
            tickSeconds ?? snapshot.RuntimeState.SimulationTickIntervalSeconds,
            offlineTickSeconds,
            eraStepSchedule);
        core.offlinePendingCalendarSeconds = snapshot.RuntimeState.OfflinePendingCalendarSeconds;
        core.offlinePendingSimulationSeconds = snapshot.RuntimeState.OfflinePendingSimulationSeconds;
        return core;
    }

    public static SimulationCore FromSnapshotWithEraSteps(
        EconomySnapshot snapshot,
        EraStepSchedule? eraStepSchedule = null) =>
        FromSnapshot(
            snapshot,
            null,
            DefaultOfflineTickSeconds,
            eraStepSchedule ?? EraStepSchedule.Default);

    public SnapshotDrivenRules? SnapshotRules => rules as SnapshotDrivenRules;

    public void Tick(SimulationMode mode = SimulationMode.Realtime)
    {
        double stepSeconds = CurrentStepSeconds(mode);
        if (mode == SimulationMode.Realtime)
        {
            Tick(mode, stepSeconds, stepSeconds);
            return;
        }

        AdvanceOfflineWindow(stepSeconds);
        offlineElapsedSeconds += stepSeconds;
    }

    public void Tick(SimulationMode mode, double requestedSeconds, double effectiveSeconds)
    {
        ValidateDelta(requestedSeconds, nameof(requestedSeconds));
        ValidateDelta(effectiveSeconds, nameof(effectiveSeconds));
        if (effectiveSeconds > requestedSeconds)
            throw new ArgumentOutOfRangeException(nameof(effectiveSeconds));
        if (mode == SimulationMode.Realtime && requestedSeconds > 0d)
            FlushOfflineRemainder();
        TickCore(mode, requestedSeconds, effectiveSeconds);
    }

    private void TickCore(SimulationMode mode, double requestedSeconds, double effectiveSeconds,
        bool useRealtimeResearchCadence = false)
    {
        ValidateDelta(requestedSeconds, nameof(requestedSeconds));
        ValidateDelta(effectiveSeconds, nameof(effectiveSeconds));
        if (effectiveSeconds > requestedSeconds)
            throw new ArgumentOutOfRangeException(
                nameof(effectiveSeconds),
                "Effective seconds cannot exceed requested seconds.");

        var nextTick = checked(State.Tick + 1);
        string startingTechLevel = State.TechLevel;
        string timingDetail = $"tech={startingTechLevel};requested={requestedSeconds:R};effective={effectiveSeconds:R}";
        Events.Add(nextTick, State.ElapsedSeconds, mode, SimulationEventKind.TickStarted,
            startingTechLevel, timingDetail, new ExpantaNum(effectiveSeconds));
        var context = new SimulationTickContext(nextTick, effectiveSeconds, mode, Events)
        {
            RequestedSeconds = requestedSeconds,
            UseRealtimeResearchCadence = useRealtimeResearchCadence
        };
        if (rules is IPhasedSimulationRules phased)
            foreach (var system in SimulationSystems.OrderedSystems)
                phased.ApplyPhase(system, State, context);
        else
            rules.ApplyTick(State, context);
        State.AdvanceClock(effectiveSeconds, requestedSeconds);
        Events.Add(State.Tick, State.ElapsedSeconds, mode, SimulationEventKind.TickCompleted,
            State.TechLevel, timingDetail, new ExpantaNum(effectiveSeconds));
    }

    public void RunRealtime(long ticks)
    {
        if (ticks < 0)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        for (var index = 0L; index < ticks; index++)
            Tick(SimulationMode.Realtime);
    }

    public void RunOffline(double seconds)
    {
        ValidateDelta(seconds, nameof(seconds));
        BeginOfflineSession();
        var remaining = seconds;
        while (remaining > 0d)
        {
            var requestedSeconds = Math.Min(CurrentStepSeconds(SimulationMode.Offline), remaining);
            AdvanceOfflineWindow(requestedSeconds);
            offlineElapsedSeconds += requestedSeconds;
            remaining -= requestedSeconds;
        }
    }

    public void BeginOfflineSession() => offlineElapsedSeconds = 0d;

    private void FlushOfflineRemainder()
    {
        if (offlinePendingSimulationSeconds <= 0d) return;
        TickCore(SimulationMode.Offline, offlinePendingCalendarSeconds, offlinePendingSimulationSeconds, true);
        offlinePendingCalendarSeconds = 0d;
        offlinePendingSimulationSeconds = 0d;
    }

    private void AdvanceOfflineWindow(double calendarSeconds)
    {
        if (!State.CampaignActive && !State.SectorStates.Values.Any(item => item.ColonizationActive))
        {
            FlushOfflineRemainder();
            TickCore(SimulationMode.Offline, calendarSeconds,
                CalculateOfflineEffectiveSeconds(offlineElapsedSeconds, calendarSeconds));
            return;
        }
        // Match Runtime's fixed realtime cadence while operations pay from capped Food.
        var remaining = calendarSeconds;
        var elapsed = offlineElapsedSeconds;
        while (remaining > 0d)
        {
            double rate, boundary;
            if (elapsed < OfflineFullRateSeconds)
            { rate = 1d; boundary = OfflineFullRateSeconds - elapsed; }
            else if (elapsed < OfflineReducedRateEndSeconds)
            { rate = OfflineMiddleRate; boundary = OfflineReducedRateEndSeconds - elapsed; }
            else { rate = OfflineLateRate; boundary = double.PositiveInfinity; }
            var untilTick = TickSeconds - offlinePendingSimulationSeconds;
            var calendarUntilTick = untilTick / rate;
            var delta = Math.Min(remaining, Math.Min(boundary, calendarUntilTick));
            var completesTick = delta >= calendarUntilTick;
            offlinePendingCalendarSeconds += delta;
            offlinePendingSimulationSeconds += completesTick ? untilTick : delta * rate;
            remaining -= delta;
            elapsed += delta;
            if (completesTick)
            {
                TickCore(SimulationMode.Offline, offlinePendingCalendarSeconds, TickSeconds, true);
                offlinePendingCalendarSeconds = 0d;
                offlinePendingSimulationSeconds = 0d;
            }
        }
    }

    public static double CalculateOfflineEffectiveSeconds(
        double elapsedSeconds,
        double requestedSeconds)
    {
        ValidateDelta(elapsedSeconds, nameof(elapsedSeconds));
        ValidateDelta(requestedSeconds, nameof(requestedSeconds));
        if (requestedSeconds == 0d)
            return 0d;
        var fullSeconds = Math.Min(
            requestedSeconds,
            Math.Max(0d, OfflineFullRateSeconds - elapsedSeconds));
        var remaining = requestedSeconds - fullSeconds;
        var middleSeconds = Math.Min(
            remaining,
            Math.Max(0d, OfflineReducedRateEndSeconds -
                Math.Max(elapsedSeconds, OfflineFullRateSeconds)));
        var lateSeconds = remaining - middleSeconds;
        return fullSeconds +
               middleSeconds * OfflineMiddleRate +
               lateSeconds * OfflineLateRate;
    }

    public SimulationStateSummary Summarize() => State.CreateSummary();
    public DetailedSimulationStateSummary SummarizeDetailed() => State.CreateDetailedSummary();

    public double CurrentStepSeconds(SimulationMode mode) => eraStepSchedule is not null
        ? eraStepSchedule.GetSeconds(State.TechLevel, mode)
        : mode switch
        {
            SimulationMode.Realtime => TickSeconds,
            SimulationMode.Offline => OfflineTickSeconds,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown simulation mode.")
        };

    private static void ValidateDelta(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0d)
            throw new ArgumentOutOfRangeException(name);
    }
}

internal sealed class SnapshotDefinitionSet
{
    private static readonly string[] TechLevelOrder =
    [
        "Animal", "StoneAge", "Medieval", "Industrial", "Spacer", "Ultra", "Archotech"
    ];

    public SnapshotDefinitionSet(EconomySnapshot snapshot)
    {
        Resources = snapshot.Resources.ToDictionary(item => item.Id, StringComparer.Ordinal);
        Building = snapshot.Buildings.ToDictionary(item => item.Id, StringComparer.Ordinal);
        Research = snapshot.Research.ToDictionary(item => item.Id, StringComparer.Ordinal);
        Workshop = snapshot.Workshops.ToDictionary(item => item.Id, StringComparer.Ordinal);
        Sector = snapshot.Sectors.ToDictionary(item => item.Id, StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, ResourceSnapshot> Resources { get; }
    public IReadOnlyDictionary<string, BuildingSnapshot> Building { get; }
    public IReadOnlyDictionary<string, ResearchSnapshot> Research { get; }
    public IReadOnlyDictionary<string, WorkshopSnapshot> Workshop { get; }
    public IReadOnlyDictionary<string, SectorSnapshot> Sector { get; }

    public int TechIndex(string techLevel) => TechIndexValue(techLevel);

    public static int TechIndexValue(string techLevel)
    {
        var index = -1;
        for (var position = 0; position < TechLevelOrder.Length; position++)
        {
            if (!string.Equals(TechLevelOrder[position], techLevel, StringComparison.Ordinal))
                continue;
            index = position;
            break;
        }
        return index < 0
            ? throw new InvalidDataException($"Unknown tech level '{techLevel}'.")
            : index;
    }

    public SortedDictionary<string, ExpantaNum> Amounts(IEnumerable<AmountSnapshot> amounts)
    {
        var result = new SortedDictionary<string, ExpantaNum>(StringComparer.Ordinal);
        foreach (var amount in amounts)
        {
            var value = Parse(amount.Value);
            result[amount.ResourceId] = result.TryGetValue(amount.ResourceId, out var current)
                ? current + value
                : value;
        }
        return result;
    }

    public static ExpantaNum Parse(string value)
    {
        if (!ExpantaNum.TryParse(value ?? string.Empty, out var parsed) || !parsed.IsFinite)
            throw new InvalidDataException($"Invalid ExpantaNum '{value}'.");
        return parsed;
    }
}

internal sealed class SimulationModifiers
{
    private readonly Dictionary<string, ExpantaNum> buildingProduction = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, ExpantaNum>> buildingResourceProduction = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExpantaNum> buildingResearchPower = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExpantaNum> buildingPower = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExpantaNum> buildingLogistics = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExpantaNum> buildingConstruction = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExpantaNum> resourceProduction = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExpantaNum> occupiedResourceProduction = new(StringComparer.Ordinal);

    public ExpantaNum GlobalResearchMultiplier { get; private set; } = ExpantaNum.One;
    public ExpantaNum GlobalConstructionMultiplier { get; private set; } = ExpantaNum.One;
    public ExpantaNum GlobalBuildingProductionMultiplier { get; private set; } = ExpantaNum.One;
    public ExpantaNum GlobalLogisticsMultiplier { get; private set; } = ExpantaNum.One;
    public ExpantaNum GlobalFoodProductionMultiplier { get; private set; } = ExpantaNum.One;
    public ExpantaNum FoodCapacityMultiplier { get; private set; } = ExpantaNum.One;
    public ExpantaNum ProductivityGranted { get; private set; }
    public ExpantaNum TerritoryGranted { get; private set; }
    public ExpantaNum MilitaryMultiplier { get; private set; } = ExpantaNum.One;
    public ExpantaNum PowerMultiplier { get; private set; } = ExpantaNum.One;
    public ExpantaNum PopulationGrowthMultiplier { get; private set; } = ExpantaNum.One;
    public ExpantaNum PopulationProductivityMultiplier { get; private set; } = ExpantaNum.One;
    public ExpantaNum HappinessBonus { get; private set; }
    public ExpantaNum ExplorationPowerMultiplier { get; private set; } = ExpantaNum.One;
    public ExpantaNum OccupiedResourceProductionMultiplier { get; private set; } = ExpantaNum.One;
    public ExpantaNum CampaignProgressMultiplier { get; private set; } = ExpantaNum.One;
    public ExpantaNum CampaignSupplyCostMultiplier { get; private set; } = ExpantaNum.One;
    public ExpantaNum CampaignCasualtyMultiplier { get; private set; } = ExpantaNum.One;
    public ExpantaNum DeconstructionReturnRate { get; private set; }

    public ExpantaNum BuildingProductionMultiplier(string buildingId) =>
        GlobalBuildingProductionMultiplier * Get(buildingProduction, buildingId);

    public ExpantaNum ResourceProductionMultiplier(string resourceId) =>
        Get(resourceProduction, resourceId);

    public ExpantaNum BuildingResourceProductionMultiplier(string buildingId, string resourceId)
    {
        if (!buildingResourceProduction.TryGetValue(buildingId, out var resources) ||
            !resources.TryGetValue(resourceId, out var multiplier))
            return ExpantaNum.One;
        return multiplier;
    }

    public ExpantaNum BuildingResearchPowerMultiplier(string buildingId) =>
        Get(buildingResearchPower, buildingId);

    public ExpantaNum BuildingPowerProductionMultiplier(string buildingId) =>
        Get(buildingPower, buildingId);

    public ExpantaNum BuildingLogisticsMultiplier(string buildingId) =>
        Get(buildingLogistics, buildingId);

    public ExpantaNum BuildingConstructionMultiplier(string buildingId)
    {
        var multiplier = GlobalConstructionMultiplier * Get(buildingConstruction, buildingId);
        return multiplier <= ExpantaNum.Zero ? ExpantaNum.One : multiplier;
    }

    public ExpantaNum OccupiedResourceMultiplier(string resourceId) =>
        OccupiedResourceProductionMultiplier * Get(occupiedResourceProduction, resourceId);

    public void ApplyResearchEffect(EffectSnapshot effect, bool allowCombatEffects)
    {
        var value = SnapshotDefinitionSet.Parse(effect.Value);
        switch (effect.Type)
        {
            case "BuildingProductionMultiplier":
                if (!string.IsNullOrEmpty(effect.ResourceId))
                    AddBuildingResource(effect.BuildingId, effect.ResourceId, value);
                else
                    AddSpecific(buildingProduction, effect.BuildingId, value);
                break;
            case "ResourceProductionMultiplier":
                AddSpecific(resourceProduction, effect.ResourceId, value);
                break;
            case "GlobalResearchMultiplier":
                GlobalResearchMultiplier = Additive(GlobalResearchMultiplier, value);
                break;
            case "GlobalConstructionMultiplier":
                GlobalConstructionMultiplier = Additive(GlobalConstructionMultiplier, value);
                break;
            case "FoodCapacityMultiplier":
                FoodCapacityMultiplier = Additive(FoodCapacityMultiplier, value);
                break;
            case "ProductivityGranted":
                ProductivityGranted += ExpantaNum.Max(ExpantaNum.Zero, value);
                break;
            case "TerritoryGranted":
                TerritoryGranted += ExpantaNum.Max(ExpantaNum.Zero, value);
                break;
            case "MilitaryMultiplier":
                if (allowCombatEffects)
                    MilitaryMultiplier = Additive(MilitaryMultiplier, value);
                break;
            case "PowerMultiplier":
                PowerMultiplier = Additive(PowerMultiplier, value);
                break;
            case "GlobalBuildingProductionMultiplier":
                GlobalBuildingProductionMultiplier = Additive(GlobalBuildingProductionMultiplier, value);
                break;
            case "BuildingResearchPowerMultiplier":
                AddSpecific(buildingResearchPower, effect.BuildingId, value);
                break;
            case "BuildingPowerProductionMultiplier":
                AddSpecific(buildingPower, effect.BuildingId, value);
                break;
            case "BuildingLogisticsProductionMultiplier":
                AddSpecific(buildingLogistics, effect.BuildingId, value);
                break;
            case "GlobalLogisticsMultiplier":
                GlobalLogisticsMultiplier = Additive(GlobalLogisticsMultiplier, value);
                break;
            case "PopulationGrowthMultiplier":
                PopulationGrowthMultiplier = Additive(PopulationGrowthMultiplier, value);
                break;
            case "DeconstructionReturnRate":
                DeconstructionReturnRate = ExpantaNum.Max(
                    DeconstructionReturnRate,
                    ExpantaNum.Clamp(value, ExpantaNum.Zero, ExpantaNum.One));
                break;
            case "UnlockIndustrialWorkshop":
            case "UnlockHomeSystemSurvey":
            case "UnlockDeepSpaceFleet":
            case "UnlockInterstellarNavigation":
                break;
            case "FleetRepairCostMultiplier":
                break;
            case "OccupiedResourceProductionMultiplier":
                if (string.IsNullOrEmpty(effect.ResourceId))
                    OccupiedResourceProductionMultiplier = Additive(OccupiedResourceProductionMultiplier, value);
                else
                    AddSpecific(occupiedResourceProduction, effect.ResourceId, value);
                break;
            case "CampaignProgressMultiplier":
                CampaignProgressMultiplier = Additive(CampaignProgressMultiplier, value);
                break;
            case "CampaignSupplyCostMultiplier":
                CampaignSupplyCostMultiplier *= Normalize(value);
                break;
            case "CampaignCasualtyMultiplier":
                CampaignCasualtyMultiplier *= Normalize(value);
                break;
            case "PopulationProductivityMultiplier":
                PopulationProductivityMultiplier = Additive(PopulationProductivityMultiplier, value);
                break;
            case "HappinessBonus":
                HappinessBonus += ExpantaNum.Max(ExpantaNum.Zero, value);
                break;
            case "ExplorationPowerMultiplier":
                ExplorationPowerMultiplier = Additive(ExplorationPowerMultiplier, value);
                break;
            case "BuildingConstructionMultiplier":
                AddSpecific(buildingConstruction, effect.BuildingId, value);
                break;
            case "GlobalFoodProductionMultiplier":
                GlobalFoodProductionMultiplier *= Normalize(value);
                break;
            default:
                throw new InvalidOperationException($"Unsupported research effect type '{effect.Type}'.");
        }
    }

    public void ApplyWorkshopEffect(EffectSnapshot effect)
    {
        var value = SnapshotDefinitionSet.Parse(effect.Value);
        switch (effect.Type)
        {
            case "BuildingProductionMultiplier":
                if (!string.IsNullOrEmpty(effect.ResourceId))
                    AddBuildingResource(effect.BuildingId, effect.ResourceId, value);
                else
                    AddSpecific(buildingProduction, effect.BuildingId, value);
                break;
            case "ResourceProductionMultiplier":
                AddSpecific(resourceProduction, effect.ResourceId, value);
                break;
            case "GlobalResearchMultiplier":
                GlobalResearchMultiplier = Additive(GlobalResearchMultiplier, value);
                break;
            case "GlobalConstructionMultiplier":
                GlobalConstructionMultiplier = Additive(GlobalConstructionMultiplier, value);
                break;
            case "TerritoryGranted":
                TerritoryGranted += ExpantaNum.Max(ExpantaNum.Zero, value);
                break;
            case "MilitaryMultiplier":
                MilitaryMultiplier = Additive(MilitaryMultiplier, value);
                break;
            case "PowerMultiplier":
                PowerMultiplier = Additive(PowerMultiplier, value);
                break;
            case "GlobalBuildingProductionMultiplier":
                GlobalBuildingProductionMultiplier = Additive(GlobalBuildingProductionMultiplier, value);
                break;
            case "BuildingResearchPowerMultiplier":
                AddSpecific(buildingResearchPower, effect.BuildingId, value);
                break;
            case "BuildingPowerProductionMultiplier":
                AddSpecific(buildingPower, effect.BuildingId, value);
                break;
            case "BuildingLogisticsProductionMultiplier":
                AddSpecific(buildingLogistics, effect.BuildingId, value);
                break;
            case "GlobalLogisticsMultiplier":
                GlobalLogisticsMultiplier = Additive(GlobalLogisticsMultiplier, value);
                break;
            case "FleetRepairCostMultiplier":
                break;
            case "PopulationGrowthMultiplier":
                PopulationGrowthMultiplier = Additive(PopulationGrowthMultiplier, value);
                break;
            case "OccupiedResourceProductionMultiplier":
                if (string.IsNullOrEmpty(effect.ResourceId))
                    OccupiedResourceProductionMultiplier = Additive(OccupiedResourceProductionMultiplier, value);
                else
                    AddSpecific(occupiedResourceProduction, effect.ResourceId, value);
                break;
            case "CampaignSupplyCostMultiplier":
                CampaignSupplyCostMultiplier *= Normalize(value);
                break;
            case "CampaignCasualtyMultiplier":
                CampaignCasualtyMultiplier *= Normalize(value);
                break;
            case "BuildingConstructionMultiplier":
                AddSpecific(buildingConstruction, effect.BuildingId, value);
                break;
            case "ExplorationPowerMultiplier":
                ExplorationPowerMultiplier = Additive(ExplorationPowerMultiplier, value);
                break;
            case "GlobalFoodProductionMultiplier":
                GlobalFoodProductionMultiplier *= Normalize(value);
                break;
            default:
                throw new InvalidOperationException($"Unsupported workshop effect type '{effect.Type}'.");
        }
    }

    private static void AddSpecific(
        Dictionary<string, ExpantaNum> values,
        string id,
        ExpantaNum value)
    {
        if (string.IsNullOrEmpty(id))
            return;
        values[id] = values.TryGetValue(id, out var current) ? Additive(current, value) : value;
    }

    private void AddBuildingResource(string buildingId, string resourceId, ExpantaNum value)
    {
        if (string.IsNullOrEmpty(buildingId) || string.IsNullOrEmpty(resourceId))
            return;
        if (!buildingResourceProduction.TryGetValue(buildingId, out var resources))
            buildingResourceProduction[buildingId] = resources = new Dictionary<string, ExpantaNum>(StringComparer.Ordinal);
        resources[resourceId] = resources.TryGetValue(resourceId, out var current)
            ? Additive(current, value)
            : value;
    }

    private static ExpantaNum Get(Dictionary<string, ExpantaNum> values, string id) =>
        values.TryGetValue(id, out var value) ? value : ExpantaNum.One;

    private static ExpantaNum Additive(ExpantaNum current, ExpantaNum value) =>
        ExpantaNum.Max(ExpantaNum.One, current + (Normalize(value) - ExpantaNum.One));

    private static ExpantaNum Normalize(ExpantaNum value) =>
        value > ExpantaNum.Zero && value.IsFinite ? value : ExpantaNum.One;
}
