using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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

public static class SimulationReportWriter
{
    public static void Write(
        string root,
        EconomySnapshot snapshot,
        SimulationResult result)
    {
        Directory.CreateDirectory(root);
        Write(root, "EconomySimulationReport.md", Markdown(snapshot, result));
        Write(root, "SimulationTimeline.csv", Timeline(result.State));
        Write(root, "BalanceWarnings.csv", Warnings(result.Warnings));
        Write(root, "ResourceFlowByEra.csv", Flows(snapshot.All, result));
        Write(root, "ResearchCompletionTimeline.csv",
            Events(result, "ResearchCompleted", "ResearchId"));
        Write(root, "BuildingConstructionTimeline.csv",
            Events(result, "BuildingCompleted", "BuildingId"));
        Write(root, "BuildingUpgradeTimeline.csv", UpgradeEvents(result));
        Write(root, "WorkshopPurchaseTimeline.csv",
            Events(result, "WorkshopPurchased", "WorkshopId"));
        Write(root, "DecisionTrace.csv", Decisions(result.State));
        Write(root, "MilestoneSummary.csv", Milestones(result.State));
    }

    private static void Write(string root, string name, string content) =>
        File.WriteAllText(Path.Combine(root, name), content, new UTF8Encoding(false));

    private static string Markdown(EconomySnapshot snapshot, SimulationResult result)
    {
        SimulationState state = result.State;
        var builder = new StringBuilder($"# Economy Simulation Report - {result.Route}\n\n");
        builder.AppendLine("> **STATUS: FROZEN DIAGNOSTIC ONLY.** This standalone output is not a reliable current gameplay, pacing, balance, or Unity acceptance report. Do not use it to tune content; use real Unity playtests and runtime evidence.");
        builder.AppendLine();
        builder.AppendLine("Offline runtime-aligned simulation; no Unity runtime was launched.");
        builder.AppendLine(
            $"Strict snapshot: {snapshot.Resources.Count} resources, " +
            $"{snapshot.Buildings.Count} buildings, {snapshot.Research.Count} research, " +
            $"{snapshot.Workshops.Count} Workshop upgrades.");
        builder.AppendLine();
        builder.AppendLine("## Rules");
        builder.AppendLine();
        builder.AppendLine("- Adaptive ticks: one second in Animal/Neolithic, ten seconds in Medieval, ten minutes in Industrial, and thirty minutes in Spacer/Ultra/Archotech; rates remain per-second and snapshots remain ten-minute aligned.");
        builder.AppendLine($"- Each route runs for a {EconomySimulator.DefaultHorizonDays}-day observation horizon so late-era construction, workshops and supply chains are visible; reports sample every {EconomySimulator.ReportSnapshotIntervalSeconds / 60} minutes.");
        builder.AppendLine("- Research resource costs are paid atomically before progress begins, matching ResearchManager.");
        builder.AppendLine("- Workshop unlocks, prerequisite chains, costs and effects are included.");
        builder.AppendLine("- Strategy decisions are emitted to DecisionTrace.csv with deduplicated reasons.");
        builder.AppendLine("- Research speed uses ResearchPower and runtime era effects.");
        builder.AppendLine("- Buildings use geometric cost growth and commit immediately after payment.");
        builder.AppendLine("- Food starts at +5/s; population consumes 0.8 food/s per person and grows toward housing capacity.");
        builder.AppendLine("- Population growth uses a logistic occupancy factor; over-capacity departure accelerates with relative excess and remains productivity-gated.");
        builder.AppendLine("- Total productivity equals population x2 plus fixed research and owned-building grants; construction checks pre-build available productivity.");
        builder.AppendLine("- Sector occupation/campaigns are not simulated; territory totals therefore include research and Workshop effects only, not Sector territory rewards.");
        TimelineSnapshot? latest = state.Timeline.LastOrDefault();
        builder.AppendLine(
            $"- Productivity-blocked building decision time: total " +
            $"{state.ProductivityWaitingSeconds:0.##} seconds; longest continuous " +
            $"{state.MaximumProductivityWaitingSeconds:0.##} seconds.");
        if (latest != null)
        {
            double utilization = latest.TotalProductivity <= 0d
                ? 0d
                : latest.UsedProductivity / latest.TotalProductivity;
            builder.AppendLine(
                $"- Final population growth: x{latest.PopulationGrowthMultiplier:0.###}, " +
                $"{latest.PopulationGrowthPerSecond:0.###}/s; productivity utilization: " +
                $"{utilization:P1}; territory: {latest.TerritoryUsed:0.###}/{latest.TerritoryTotal:0.###}.");
        }

        foreach (SimTechLevel era in new[]
                 {
                     SimTechLevel.Animal,
                     SimTechLevel.Neolithic,
                     SimTechLevel.Medieval,
                     SimTechLevel.Industrial,
                     SimTechLevel.Spacer
                 })
        {
            double seconds = state.EraReachedSeconds.GetValueOrDefault(era.ToString(), -1d);
            builder.AppendLine();
            builder.AppendLine($"## {era} Age");
            builder.AppendLine();
            builder.AppendLine($"到达时间: {(seconds < 0d ? "不可达" : $"{seconds / 60d:0.##} 分钟")}");
            int completed = state.Events.Count(x =>
                x.Kind == "ResearchCompleted" && x.TechLevel == era);
            builder.AppendLine($"完成研究: {completed}");
        }

        builder.AppendLine();
        builder.AppendLine(
            $"原始时代最长无研究目标: " +
            $"{BalanceAnalysis.MaximumNoResearchSeconds(state, SimTechLevel.Animal) / 60d:0.##} 分钟；" +
            $"新石器时代: " +
            $"{BalanceAnalysis.MaximumNoResearchSeconds(state, SimTechLevel.Neolithic) / 60d:0.##} 分钟。");
        builder.AppendLine();
        builder.AppendLine("## Bottlenecks");
        builder.AppendLine();
        builder.AppendLine(result.Bottlenecks.Count == 0
            ? "None"
            : string.Join("\n", result.Bottlenecks
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(x => $"- {x}")));
        builder.AppendLine();
        builder.AppendLine("## Warnings");
        builder.AppendLine();
        builder.AppendLine(result.Warnings.Count == 0
            ? "None"
            : string.Join("\n", result.Warnings.Select(x =>
                $"- **{x.Severity} {x.Type}** {x.Object}: {x.Reason} Suggestion: {x.Suggestion}")));
        builder.AppendLine();
        builder.AppendLine("## Validation boundary");
        builder.AppendLine();
        builder.AppendLine("Unity 运行验证证据待补充。");
        builder.AppendLine();
        builder.AppendLine("未执行 Huawei P40 Pro 真机验收。");
        return builder.ToString();
    }

    private static string Timeline(SimulationState state)
    {
        var builder = new StringBuilder(
            "Minute,TechLevel,ResearchPower,Population,PopulationGrowthMultiplier,PopulationGrowthPerSecond,PowerProductionPerSecond,PowerConsumptionPerSecond,LogisticsProductionPerSecond,LogisticsConsumptionPerSecond,TotalProductivity,UsedProductivity,AvailableProductivity,ProductivityUtilization,TerritoryUsed,TerritoryTotal,ActiveResearch,Resources,Buildings,ResearchCompleted,WorkshopPurchased\n");
        foreach (TimelineSnapshot snapshot in state.Timeline)
        {
            builder.AppendLine(
                $"{snapshot.Minute},{snapshot.TechLevel},{snapshot.ResearchPower:0.###}," +
                $"{snapshot.Population:0.###},{snapshot.PopulationGrowthMultiplier:0.###}," +
                $"{snapshot.PopulationGrowthPerSecond:0.###}," +
                $"{snapshot.PowerProductionPerSecond:0.###},{snapshot.PowerConsumptionPerSecond:0.###}," +
                $"{snapshot.LogisticsProductionPerSecond:0.###},{snapshot.LogisticsConsumptionPerSecond:0.###}," +
                $"{snapshot.TotalProductivity:0.###}," +
                $"{snapshot.UsedProductivity:0.###}," +
                $"{snapshot.TotalProductivity-snapshot.UsedProductivity:0.###}," +
                $"{(snapshot.TotalProductivity<=0?0:snapshot.UsedProductivity/snapshot.TotalProductivity):0.###}," +
                $"{snapshot.TerritoryUsed:0.###},{snapshot.TerritoryTotal:0.###}," +
                $"{Csv(snapshot.ActiveResearch)},{Csv(snapshot.Resources)}," +
                $"{Csv(snapshot.Buildings)},{Csv(snapshot.ResearchCompleted)}," +
                $"{Csv(snapshot.WorkshopPurchased)}");
        }
        return builder.ToString();
    }

    private static string Events(
        SimulationResult result,
        string kind,
        string idColumn)
    {
        var builder = new StringBuilder($"TimeSeconds,Minute,TechLevel,{idColumn},Count\n");
        foreach (SimulationEvent item in result.State.Events.Where(x => x.Kind == kind))
        {
            builder.AppendLine(
                $"{item.Seconds:0.0},{item.Seconds / 60d:0.##},{item.TechLevel}," +
                $"{Csv(item.Id)},{item.Count}");
        }
        return builder.ToString();
    }

    private static string UpgradeEvents(SimulationResult result)
    {
        var builder = new StringBuilder(
            "TimeSeconds,Minute,TechLevel,Upgrade,TargetCount,Detail\n");
        foreach (SimulationEvent item in result.State.Events.Where(
                     value => value.Kind == "BuildingUpgrade"))
        {
            builder.AppendLine(
                $"{item.Seconds:0.0},{item.Seconds / 60d:0.##},{item.TechLevel}," +
                $"{Csv(item.Id)},{item.Count},{Csv(item.Detail)}");
        }
        return builder.ToString();
    }

    private static string Decisions(SimulationState state)
    {
        var builder = new StringBuilder(
            "FirstSeconds,LastSeconds,Route,Subsystem,Outcome,Candidate,RepeatCount,Reason\n");
        foreach (SimulationDecision decision in state.Decisions)
        {
            builder.AppendLine(
                $"{decision.FirstSeconds:0.###},{decision.LastSeconds:0.###}," +
                $"{decision.Route},{Csv(decision.Subsystem)},{Csv(decision.Outcome)}," +
                $"{Csv(decision.Candidate)},{decision.RepeatCount},{Csv(decision.Reason)}");
        }
        return builder.ToString();
    }

    private static string Flows(
        IReadOnlyList<Definition> definitions,
        SimulationResult result)
    {
        var builder = new StringBuilder(
            "Era,Resource,ProductionPerSecond,ConsumptionPerSecond,NetPerSecond\n");
        foreach (SimTechLevel era in Enum.GetValues<SimTechLevel>()
                     .Where(x => x <= SimTechLevel.Industrial))
        {
            foreach (string resource in definitions
                         .Where(x => x.Kind == DefinitionKind.Resource)
                         .Select(x => x.Id)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                double production = 0d;
                double consumption = 0d;
                foreach (Definition building in definitions.Where(x =>
                             x.Kind == DefinitionKind.Building && x.TechLevel <= era))
                {
                    int count = result.State.Buildings.GetValueOrDefault(building.Id);
                    production += building.Generation.GetValueOrDefault(resource) * count;
                    consumption += building.Consumption.GetValueOrDefault(resource) * count;
                }
                builder.AppendLine(
                    $"{era},{Csv(resource)},{production:0.###},{consumption:0.###}," +
                    $"{production - consumption:0.###}");
            }
        }
        return builder.ToString();
    }

    private static string Warnings(IEnumerable<BalanceWarning> warnings)
    {
        var builder = new StringBuilder("Type,Object,Reason,Severity,Suggestion\n");
        foreach (BalanceWarning warning in warnings)
        {
            builder.AppendLine(string.Join(",",
                Csv(warning.Type),
                Csv(warning.Object),
                Csv(warning.Reason),
                Csv(warning.Severity),
                Csv(warning.Suggestion)));
        }
        return builder.ToString();
    }

    private static string Milestones(SimulationState state)
    {
        var builder = new StringBuilder("Milestone,TimeSeconds,Minute\n");
        foreach (SimTechLevel era in new[]
                 {
                     SimTechLevel.Animal,
                     SimTechLevel.Neolithic,
                     SimTechLevel.Medieval,
                     SimTechLevel.Industrial,
                     SimTechLevel.Spacer
                 })
        {
            double seconds = state.EraReachedSeconds.GetValueOrDefault(era.ToString(), -1d);
            builder.AppendLine($"{era},{seconds:0.###},{(seconds < 0d ? -1d : seconds / 60d):0.###}");
        }
        foreach (int minute in new[] { 10, 60, 240, 720, 1440, 10080, 43200 })
        {
            TimelineSnapshot? snapshot = state.Timeline.LastOrDefault(x => x.Minute <= minute);
            builder.AppendLine(
                $"Snapshot{minute}m,{(snapshot == null ? -1d : snapshot.Minute * 60d):0.###}," +
                $"{snapshot?.Minute ?? -1}");
        }
        return builder.ToString();
    }

    private static string Csv(string value) =>
        "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
}
