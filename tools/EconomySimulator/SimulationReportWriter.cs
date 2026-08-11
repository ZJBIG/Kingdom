using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Kingdom.EconomySimulation;

public static class BalanceAnalysis
{
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
                if (consumption > 0d && building.Generation[resource] / consumption > 10d)
                {
                    Warn(result, "Economy explosion", resource,
                        $"{building.Id} output/active demand ratio exceeds 10.",
                        "High", "Reduce the multiplier or add a legitimate sink.");
                }
            }

            foreach (string resource in building.Consumption.Keys)
            {
                if (buildings.Count(x => x.Generation.ContainsKey(resource)) == 1)
                    result.Bottlenecks.Add($"{resource}: single producer");
            }
        }

        foreach (Definition building in buildings)
        {
            double cost = building.ResourceRequirements.Values.Sum();
            double flow = buildings
                .Where(x => state.Buildings.ContainsKey(x.Id))
                .Sum(x => x.Generation.Values.Sum());
            if (cost > Math.Max(1d, flow) * 600d)
            {
                Warn(result, "Building wait exceeds ten minutes", building.Id,
                    $"Construction inputs {cost:0.##} exceed ten minutes of active aggregate production.",
                    "High", "Adjust first-copy cost or unlock a producer earlier.");
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
        builder.AppendLine("Offline runtime-aligned simulation; no Unity runtime was launched.");
        builder.AppendLine(
            $"Strict snapshot: {snapshot.Resources.Count} resources, " +
            $"{snapshot.Buildings.Count} buildings, {snapshot.Research.Count} research, " +
            $"{snapshot.Workshops.Count} Workshop upgrades.");
        builder.AppendLine();
        builder.AppendLine("## Rules");
        builder.AppendLine();
        builder.AppendLine("- Fixed one-second ticks with integer minute snapshots.");
        builder.AppendLine("- Research resource costs are paid atomically before progress begins, matching ResearchManager.");
        builder.AppendLine("- Workshop unlocks, prerequisite chains, costs and effects are included.");
        builder.AppendLine("- Strategy decisions are emitted to DecisionTrace.csv with deduplicated reasons.");
        builder.AppendLine("- Research speed uses ResearchPower and runtime era effects.");
        builder.AppendLine("- Buildings use geometric cost growth and commit immediately after payment.");
        builder.AppendLine("- Food starts at +5/s; population consumes 0.8 food/s per person and grows toward housing capacity.");
        builder.AppendLine("- Population growth uses a logistic occupancy factor; over-capacity departure accelerates with relative excess and remains productivity-gated.");
        builder.AppendLine("- Total productivity equals population x2 plus fixed research and owned-building grants; construction checks pre-build available productivity.");
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
                     SimTechLevel.Industrial
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
        builder.AppendLine("未执行真实 Unity 编译。");
        builder.AppendLine();
        builder.AppendLine("未执行 Huawei P40 Pro 真机验收。");
        return builder.ToString();
    }

    private static string Timeline(SimulationState state)
    {
        var builder = new StringBuilder(
            "Minute,TechLevel,ResearchPower,Population,PopulationGrowthMultiplier,PopulationGrowthPerSecond,TotalProductivity,UsedProductivity,AvailableProductivity,ProductivityUtilization,TerritoryUsed,TerritoryTotal,ActiveResearch,Resources,Buildings,ResearchCompleted,WorkshopPurchased\n");
        foreach (TimelineSnapshot snapshot in state.Timeline)
        {
            builder.AppendLine(
                $"{snapshot.Minute},{snapshot.TechLevel},{snapshot.ResearchPower:0.###}," +
                $"{snapshot.Population:0.###},{snapshot.PopulationGrowthMultiplier:0.###}," +
                $"{snapshot.PopulationGrowthPerSecond:0.###},{snapshot.TotalProductivity:0.###}," +
                $"{snapshot.UsedProductivity:0.###}," +
                $"{Math.Max(0,snapshot.TotalProductivity-snapshot.UsedProductivity):0.###}," +
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
                     SimTechLevel.Industrial
                 })
        {
            double seconds = state.EraReachedSeconds.GetValueOrDefault(era.ToString(), -1d);
            builder.AppendLine($"{era},{seconds:0.###},{(seconds < 0d ? -1d : seconds / 60d):0.###}");
        }
        foreach (int minute in new[] { 10, 60, 240, 720, 1440 })
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
