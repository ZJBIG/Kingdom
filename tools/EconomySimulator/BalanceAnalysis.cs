using System;
using System.Collections.Generic;
using System.Linq;

namespace Kingdom.EconomySimulation;

public static class BalanceAnalysis
{
    internal static (string Resource, double Seconds) SlowestConstructionWait(
        SimulationState state,
        IReadOnlyList<Definition> buildings,
        Definition building)
    {
        string slowestResource = "";
        double slowestWaitSeconds = 0d;
        foreach ((string resource, double cost) in building.ResourceRequirements)
        {
            if (cost <= 0d)
                continue;

            double activeProduction = buildings
                .Where(x => state.Buildings.GetValueOrDefault(x.Id) > 0)
                .Sum(x => x.Generation.GetValueOrDefault(resource) *
                    state.Buildings.GetValueOrDefault(x.Id));
            if (activeProduction <= 0d)
                continue;

            double waitSeconds = cost / activeProduction;
            if (waitSeconds > slowestWaitSeconds)
            {
                slowestWaitSeconds = waitSeconds;
                slowestResource = resource;
            }
        }

        return (slowestResource, slowestWaitSeconds);
    }

    public static void Analyze(
        SimulationResult result,
        IReadOnlyList<Definition> definitions,
        IReadOnlyList<Definition> buildings,
        IReadOnlyList<Definition> research)
    {
        SimulationState state = result.State;
        foreach (Definition building in buildings.Where(x => state.Buildings.ContainsKey(x.Id)))
        {
            foreach (string resource in building.Generation.Keys)
            {
                double consumption = buildings
                    .Where(x => state.Buildings.ContainsKey(x.Id))
                    .Sum(x => x.Consumption.GetValueOrDefault(resource) *
                        state.Buildings.GetValueOrDefault(x.Id));
                bool hasConstructionSink = definitions.Any(x =>
                    x.ResourceRequirements.ContainsKey(resource));
                if (!hasConstructionSink &&
                    consumption > 0d && building.Generation[resource] / consumption > 10d)
                {
                    Warn(result, "Economy explosion", resource,
                        $"{building.Id} output/active demand ratio exceeds 10 and no construction sink exists.",
                        "High", "Reduce the multiplier or add a legitimate sink.");
                }
            }

            foreach (string resource in building.Consumption.Keys)
            {
                if (buildings.Count(x => x.Generation.ContainsKey(resource)) == 1)
                    result.Bottlenecks.Add($"{resource}: single producer");
            }
        }

        foreach (string resource in new[] { "Bronze", "Tin" })
        {
            double production = buildings.Sum(x =>
                x.Generation.GetValueOrDefault(resource) *
                state.Buildings.GetValueOrDefault(x.Id));
            double consumption = buildings.Sum(x =>
                x.Consumption.GetValueOrDefault(resource) *
                state.Buildings.GetValueOrDefault(x.Id));
            double net = production - consumption;
            if (production >= 10d && net >= production * 0.9d)
            {
                Warn(result, "Legacy metal surplus", resource,
                    $"Active production is {production:0.##}/s versus {consumption:0.##}/s consumption; net flow remains {net:0.##}/s.",
                    "Info",
                    "Review durable Industrial/Spacer sinks or active building mix; do not raise Phantom material source rates.");
            }
        }

        foreach (Definition building in buildings)
        {
            // Do not judge a future-era building against an earlier-era
            // production base.  Its producers and prerequisite effects may
            // not be available yet, so doing so reports a bottleneck before
            // the player can even attempt the construction.
            if (building.TechLevel > state.TechLevel ||
                !building.RequiredResearch.All(state.CompletedResearch.Contains) ||
                !building.RequiredWorkshop.All(state.PurchasedWorkshop.Contains))
                continue;

            // Resource quantities are not interchangeable.  Summing Biomass,
            // Glass and Alloy into one cost/flow ratio produced false pacing
            // warnings whenever a building used several different materials.
            // Measure the first-copy wait independently for each required
            // resource and report the slowest material bottleneck.
            (string slowestResource, double slowestWaitSeconds) =
                SlowestConstructionWait(state, buildings, building);
            if (!string.IsNullOrEmpty(slowestResource) &&
                slowestWaitSeconds > 600d)
            {
                // Spacer advanced materials are intentionally slow gates. Keep
                // them visible in the report, but do not make a deliberate
                // late-era accumulation window fail the earlier pacing gate.
                string severity = building.TechLevel >= SimTechLevel.Spacer
                    ? "Info"
                    : "High";
                string guidance = building.TechLevel >= SimTechLevel.Spacer
                    ? "Treat this as an intentional late-era material gate; do not raise its source rate without a progression review."
                    : "Adjust that resource cost or unlock a producer earlier.";
                Warn(result, "Building wait exceeds ten minutes", building.Id,
                    $"First-copy {slowestResource} input takes {slowestWaitSeconds / 60d:0.##} minutes at active production.",
                    severity, guidance);
            }
        }

        foreach (Definition item in research.Where(x => x.TechLevel <= SimTechLevel.Industrial))
        {
            foreach (string resource in item.ResourceRequirements.Keys)
            {
                int producers = buildings.Count(x => x.Generation.ContainsKey(resource));
                if (resource.Equals("WoodLog", StringComparison.OrdinalIgnoreCase))
                    producers++;
                if (producers == 0)
                {
                    Warn(result, "Missing producer", resource,
                        $"Research {item.Id} requires a resource with no producer.",
                        "High", "Add a reachable producer or remove the requirement.");
                }
            }
        }

        if (!state.EraReachedSeconds.ContainsKey(nameof(SimTechLevel.Neolithic)))
            Warn(result, "Pacing failure", "Neolithic",
                "The run did not reach Neolithic within 24 hours.",
                "High", "Repair the Animal main line.");
        if (!state.EraReachedSeconds.ContainsKey(nameof(SimTechLevel.Medieval)))
            Warn(result, "Pacing failure", "Medieval",
                "The run did not reach Medieval within 24 hours.",
                "High", "Repair ResearchPower and the Neolithic main line.");
        double animalDrought = MaximumNoResearchSeconds(state, SimTechLevel.Animal);
        double neolithicDrought = MaximumNoResearchSeconds(state, SimTechLevel.Neolithic);
        if (animalDrought > 300d)
        {
            Warn(result, "Progress drought", "Animal",
                $"Longest interval without an active research target was {animalDrought / 60d:0.##} minutes.",
                "High", "Move an affordable action or ResearchPower source earlier.");
        }
        if (neolithicDrought > 600d)
        {
            Warn(result, "Progress drought", "Neolithic",
                $"Longest interval without an active research target was {neolithicDrought / 60d:0.##} minutes.",
                "High", "Repair producer prerequisites or shorten the decision interval.");
        }

        if (state.TechLevel >= SimTechLevel.Spacer &&
            BuildingSimulator.AvailableTerritory(state, buildings) <= 0d &&
            buildings.Any(x => x.TechLevel >= SimTechLevel.Spacer && x.SpaceCost > 0d))
        {
            Warn(result, "Territory model boundary", "Sector operations",
                "The standalone simulator exhausted research/workshop territory before late Spacer construction; it does not execute Sector occupation or apply Sector territory rewards.",
                "Info", "Treat late construction waits as strategy evidence until Sector operations are modeled.");
        }

        result.Bottlenecks.Sort(StringComparer.OrdinalIgnoreCase);
    }

    public static double MaximumNoResearchSeconds(
        SimulationState state,
        SimTechLevel era)
    {
        double current = 0d;
        double maximum = 0d;
        foreach (TimelineSnapshot snapshot in state.Timeline.Where(x => x.TechLevel == era))
        {
            if (string.IsNullOrEmpty(snapshot.ActiveResearch))
                current += 60d;
            else
                current = 0d;
            maximum = Math.Max(maximum, current);
        }
        return maximum;
    }

    private static void Warn(
        SimulationResult result,
        string type,
        string target,
        string reason,
        string severity,
        string suggestion)
    {
        if (result.Warnings.Any(x => x.Type == type && x.Object == target))
            return;
        result.Warnings.Add(new BalanceWarning
        {
            Type = type,
            Object = target,
            Reason = reason,
            Severity = severity,
            Suggestion = suggestion
        });
    }
}

