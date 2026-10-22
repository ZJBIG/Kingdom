using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Offline, editor-only dependency analysis for the same four graphs used by
/// GameBootstrap: research prerequisites, research resource requirements,
/// building research/cost prerequisites, and resource producers.
/// It never mutates definitions.
/// </summary>
public static class ContentDependencyAnalyzer
{
    [MenuItem("Codex/Content/Analyze Dependency Closure")]
    public static void AnalyzeCurrentAssets()
    {
        DependencyAnalysisResult result = AnalyzeCurrentDefinitions();

        string report = result.FormatReport();
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outputPath = Path.Combine(projectRoot, "data", "content-dependency-analysis.md");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(outputPath, report, new UTF8Encoding(false));
        AssetDatabase.Refresh();
        Debug.Log(report);
        Debug.Log($"Content dependency analysis written to {outputPath}");
    }

    public static void AnalyzeFromCommandLine()
    {
        DependencyAnalysisResult result = AnalyzeCurrentDefinitions();
        string report = result.FormatReport();
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outputPath = Path.Combine(projectRoot, "data", "content-dependency-analysis.md");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(outputPath, report, new UTF8Encoding(false));
        Debug.Log(report);
        Debug.Log($"Content dependency analysis written to {outputPath}");
    }

    private static DependencyAnalysisResult AnalyzeCurrentDefinitions()
    {
        return Analyze(
            DataBase<ResourceDef>.All,
            DataBase<BuildingDef>.All,
            DataBase<ResearchDef>.All,
            new[] { "WoodLog" },
            TechLevel.Animal);
    }

    public static DependencyAnalysisResult Analyze(
        IReadOnlyList<ResourceDef> resources,
        IReadOnlyList<BuildingDef> buildings,
        IReadOnlyList<ResearchDef> researches,
        IReadOnlyCollection<string> startingResourceIds,
        TechLevel startingTechLevel)
    {
        var result = new DependencyAnalysisResult { HighestTechLevel = startingTechLevel };
        var byResearch = new Dictionary<string, ResearchDef>(StringComparer.OrdinalIgnoreCase);
        var byResource = new Dictionary<string, ResourceDef>(StringComparer.OrdinalIgnoreCase);
        var producers = new Dictionary<string, List<BuildingDef>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < resources.Count; i++)
            if (resources[i] != null && !string.IsNullOrEmpty(resources[i].Id)) byResource[resources[i].Id] = resources[i];
        for (int i = 0; i < researches.Count; i++)
            if (researches[i] != null && !string.IsNullOrEmpty(researches[i].Id)) byResearch[researches[i].Id] = researches[i];
        for (int i = 0; i < buildings.Count; i++)
        {
            BuildingDef building = buildings[i];
            if (building == null) continue;
            for (int j = 0; j < building.ResourceGenerationRates.Count; j++)
            {
                ResourceDef output = building.ResourceGenerationRates[j].First;
                if (output == null) continue;
                if (!producers.TryGetValue(output.Id, out List<BuildingDef> values))
                    producers.Add(output.Id, values = new List<BuildingDef>());
                values.Add(building);
            }
        }

        var availableResources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var completedResearch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reachableBuildings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in startingResourceIds)
            if (!string.IsNullOrWhiteSpace(id)) availableResources.Add(id);

        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < researches.Count; i++)
            {
                ResearchDef research = researches[i];
                if (research == null || completedResearch.Contains(research.Id)) continue;
                if (!ResearchCanBeCompleted(research, completedResearch, availableResources, result.HighestTechLevel)) continue;
                completedResearch.Add(research.Id);
                if (research.AdvancesTechLevel && research.TechLevel > result.HighestTechLevel)
                    result.HighestTechLevel = research.TechLevel;
                changed = true;
            }
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingDef building = buildings[i];
                if (building == null || building.TechLevel > result.HighestTechLevel || reachableBuildings.Contains(building.Id)) continue;
                if (!AllResearchComplete(building.RequiredResearch, completedResearch) ||
                    !CostsAvailable(building.ResourceRequirements, availableResources) ||
                    !CostsAvailable(building.ResourceConsumptionRates, availableResources)) continue;
                reachableBuildings.Add(building.Id);
                for (int j = 0; j < building.ResourceGenerationRates.Count; j++)
                    if (building.ResourceGenerationRates[j].First != null)
                        availableResources.Add(building.ResourceGenerationRates[j].First.Id);
                changed = true;
            }
        }
        while (changed);

        for (int i = 0; i < researches.Count; i++)
        {
            ResearchDef research = researches[i];
            if (research != null && !completedResearch.Contains(research.Id))
                result.UnreachableResearch.Add(new BlockedDefinition(research.Id, DescribeResearchBlock(research, completedResearch, availableResources, producers, byResearch)));
        }
        for (int i = 0; i < buildings.Count; i++)
        {
            BuildingDef building = buildings[i];
            if (building != null && !reachableBuildings.Contains(building.Id))
                result.UnreachableBuildings.Add(new BlockedDefinition(building.Id, DescribeBuildingBlock(building, completedResearch, availableResources, producers, byResearch)));
        }
        foreach (KeyValuePair<string, List<BuildingDef>> entry in producers)
            if (entry.Value.Count == 0) result.ResourcesWithoutProducer.Add(entry.Key);
        FindResearchCycles(researches, result.ResearchCycles);
        FindResourceDeadlocks(researches, buildings, producers, result.ResourceDeadlocks);
        result.CompletedResearchCount = completedResearch.Count;
        result.ReachableBuildingCount = reachableBuildings.Count;
        result.ReachableResourceIds = availableResources;
        return result;
    }

    private static bool ResearchCanBeCompleted(ResearchDef research, HashSet<string> completed, HashSet<string> resources, TechLevel tech)
    {
        bool era = research.TechLevel <= tech || (research.AdvancesTechLevel && (int)research.TechLevel == (int)tech + 1);
        return era && AllResearchComplete(research.Prerequisites, completed) && CostsAvailable(research.ResourceRequirements, resources);
    }

    private static bool AllResearchComplete(IReadOnlyList<ResearchDef> values, HashSet<string> completed)
    {
        for (int i = 0; i < values.Count; i++) if (values[i] == null || !completed.Contains(values[i].Id)) return false;
        return true;
    }

    private static bool CostsAvailable(IReadOnlyList<Pair<ResourceDef, ExpantaNum>> values, HashSet<string> resources)
    {
        for (int i = 0; i < values.Count; i++) if (values[i].First == null || !resources.Contains(values[i].First.Id)) return false;
        return true;
    }

    private static string DescribeResearchBlock(ResearchDef value, HashSet<string> completed, HashSet<string> resources, Dictionary<string, List<BuildingDef>> producers, Dictionary<string, ResearchDef> byResearch)
    {
        for (int i = 0; i < value.Prerequisites.Count; i++)
        {
            ResearchDef prerequisite = value.Prerequisites[i];
            if (prerequisite == null) return "Missing prerequisite: <null>";
            if (!completed.Contains(prerequisite.Id)) return "Missing prerequisite: " + prerequisite.Id + "\n" + TraceResearch(prerequisite, completed, resources, producers, byResearch, new HashSet<string>());
        }
        if (value.TechLevel > TechLevel.Animal) return "TechLevel condition: requires " + value.TechLevel;
        for (int i = 0; i < value.ResourceRequirements.Count; i++)
        {
            ResourceDef resource = value.ResourceRequirements[i].First;
            if (resource != null && !resources.Contains(resource.Id)) return DescribeMissingResource(resource.Id, producers, completed, resources, byResearch);
        }
        return "Blocked by an unresolved prerequisite condition.";
    }

    private static string DescribeBuildingBlock(BuildingDef value, HashSet<string> completed, HashSet<string> resources, Dictionary<string, List<BuildingDef>> producers, Dictionary<string, ResearchDef> byResearch)
    {
        for (int i = 0; i < value.RequiredResearch.Count; i++)
            if (value.RequiredResearch[i] == null || !completed.Contains(value.RequiredResearch[i].Id))
                return "Missing required research: " + (value.RequiredResearch[i] == null ? "<null>" : value.RequiredResearch[i].Id);
        for (int i = 0; i < value.ResourceRequirements.Count; i++)
        {
            ResourceDef resource = value.ResourceRequirements[i].First;
            if (resource != null && !resources.Contains(resource.Id)) return DescribeMissingResource(resource.Id, producers, completed, resources, byResearch);
        }
        for (int i = 0; i < value.ResourceConsumptionRates.Count; i++)
        {
            ResourceDef resource = value.ResourceConsumptionRates[i].First;
            if (resource == null) return "Missing continuous input: <null>";
            if (!resources.Contains(resource.Id)) return "Continuous input blocked: " + DescribeMissingResource(resource.Id, producers, completed, resources, byResearch);
        }
        return "TechLevel or unresolved construction condition.";
    }

    private static string DescribeMissingResource(string id, Dictionary<string, List<BuildingDef>> producers, HashSet<string> completed, HashSet<string> resources, Dictionary<string, ResearchDef> byResearch)
    {
        var builder = new StringBuilder("Missing resource:    ").Append(id);
        if (!producers.TryGetValue(id, out List<BuildingDef> values) || values.Count == 0) return builder.Append("\nRequired producer:    <none>").ToString();
        for (int i = 0; i < values.Count; i++)
        {
            BuildingDef producer = values[i];
            builder.Append("\nRequired producer:    ").Append(producer.Id);
            for (int j = 0; j < producer.RequiredResearch.Count; j++)
                if (producer.RequiredResearch[j] != null && !completed.Contains(producer.RequiredResearch[j].Id))
                    builder.Append("\nProducer blocked by:  ").Append(producer.RequiredResearch[j].Id);
            for (int j = 0; j < producer.ResourceRequirements.Count; j++)
                if (producer.ResourceRequirements[j].First != null && !resources.Contains(producer.ResourceRequirements[j].First.Id))
                    builder.Append("\nProducer construction cost blocked by: ").Append(producer.ResourceRequirements[j].First.Id);
        }
        return builder.ToString();
    }

    private static string TraceResearch(ResearchDef value, HashSet<string> completed, HashSet<string> resources, Dictionary<string, List<BuildingDef>> producers, Dictionary<string, ResearchDef> byResearch, HashSet<string> seen)
    {
        if (value == null || completed.Contains(value.Id)) return string.Empty;
        if (!seen.Add(value.Id)) return "Trace: cycle at " + value.Id;
        for (int i = 0; i < value.Prerequisites.Count; i++)
            if (value.Prerequisites[i] != null && !completed.Contains(value.Prerequisites[i].Id))
                return "Trace: " + value.Id + "\n -> " + TraceResearch(value.Prerequisites[i], completed, resources, producers, byResearch, seen);
        for (int i = 0; i < value.ResourceRequirements.Count; i++)
            if (value.ResourceRequirements[i].First != null && !resources.Contains(value.ResourceRequirements[i].First.Id))
                return "Trace: " + value.Id + "\n -> " + DescribeMissingResource(value.ResourceRequirements[i].First.Id, producers, completed, resources, byResearch);
        return "Trace: " + value.Id;
    }

    private static void FindResearchCycles(IReadOnlyList<ResearchDef> values, List<string> output)
    {
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var path = new List<string>();
        for (int i = 0; i < values.Count; i++) if (values[i] != null) VisitResearch(values[i], visiting, visited, path, output);
    }

    private static void VisitResearch(ResearchDef value, HashSet<string> visiting, HashSet<string> visited, List<string> path, List<string> output)
    {
        if (visited.Contains(value.Id)) return;
        if (!visiting.Add(value.Id)) { int start = path.IndexOf(value.Id); output.Add(string.Join(" -> ", path.GetRange(Math.Max(0, start), path.Count - Math.Max(0, start))) + " -> " + value.Id); return; }
        path.Add(value.Id);
        for (int i = 0; i < value.Prerequisites.Count; i++) if (value.Prerequisites[i] != null) VisitResearch(value.Prerequisites[i], visiting, visited, path, output);
        path.RemoveAt(path.Count - 1); visiting.Remove(value.Id); visited.Add(value.Id);
    }

    private static void FindResourceDeadlocks(IReadOnlyList<ResearchDef> researches, IReadOnlyList<BuildingDef> buildings, Dictionary<string, List<BuildingDef>> producers, List<string> output)
    {
        for (int i = 0; i < researches.Count; i++)
        {
            ResearchDef research = researches[i]; if (research == null) continue;
            for (int j = 0; j < research.ResourceRequirements.Count; j++)
            {
                ResourceDef resource = research.ResourceRequirements[j].First; if (resource == null || resource.Id == "WoodLog" || !producers.TryGetValue(resource.Id, out List<BuildingDef> values)) continue;
                bool allRequireResearch = values.Count > 0;
                for (int k = 0; k < values.Count; k++) if (!ContainsResearch(values[k].RequiredResearch, research.Id)) allRequireResearch = false;
                if (allRequireResearch) output.Add(research.Id + " -> requires " + resource.Id + " -> producer requires " + research.Id);
            }
        }
        for (int i = 0; i < buildings.Count; i++)
        {
            BuildingDef building = buildings[i]; if (building == null) continue;
            for (int j = 0; j < building.ResourceRequirements.Count; j++)
            {
                ResourceDef resource = building.ResourceRequirements[j].First; if (resource != null && resource.Id != "WoodLog" && producers.TryGetValue(resource.Id, out List<BuildingDef> values) && values.Count == 1 && values[0] == building)
                    output.Add(building.Id + " -> construction requires " + resource.Id + " -> produced only by " + building.Id);
            }
        }
    }

    private static bool ContainsResearch(IReadOnlyList<ResearchDef> values, string id) { for (int i = 0; i < values.Count; i++) if (values[i] != null && string.Equals(values[i].Id, id, StringComparison.OrdinalIgnoreCase)) return true; return false; }

    public sealed class BlockedDefinition { public readonly string Id; public readonly string Reason; public BlockedDefinition(string id, string reason) { Id = id; Reason = reason; } }
    public sealed class DependencyAnalysisResult
    {
        public TechLevel HighestTechLevel; public int CompletedResearchCount; public int ReachableBuildingCount;
        public HashSet<string> ReachableResourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<BlockedDefinition> UnreachableResearch = new List<BlockedDefinition>(); public readonly List<BlockedDefinition> UnreachableBuildings = new List<BlockedDefinition>();
        public readonly List<string> ResourcesWithoutProducer = new List<string>(); public readonly List<string> ResourceDeadlocks = new List<string>(); public readonly List<string> ResearchCycles = new List<string>();
        public string FormatReport()
        {
            var b = new StringBuilder(); b.AppendLine("# Content dependency analysis"); b.AppendLine(); b.AppendLine("================================"); b.AppendLine("UNREACHABLE RESEARCH"); b.AppendLine("================================");
            if (UnreachableResearch.Count == 0) b.AppendLine("Research: None"); else foreach (BlockedDefinition item in UnreachableResearch) b.AppendLine("\nResearch: " + item.Id + "\n\nBlocked reason:\n" + item.Reason);
            b.AppendLine("\n================================\nUNREACHABLE BUILDINGS\n================================"); if (UnreachableBuildings.Count == 0) b.AppendLine("Building: None"); else foreach (BlockedDefinition item in UnreachableBuildings) b.AppendLine("\nBuilding: " + item.Id + "\n\nBlocked reason:\n" + item.Reason);
            b.AppendLine("\n================================\nRESOURCE DEADLOCKS\n================================"); if (ResourceDeadlocks.Count == 0) b.AppendLine("None"); else foreach (string item in ResourceDeadlocks) b.AppendLine(item);
            b.AppendLine("\n================================\nRESEARCH CYCLES\n================================"); if (ResearchCycles.Count == 0) b.AppendLine("None"); else foreach (string item in ResearchCycles) b.AppendLine(item);
            b.AppendLine("\nSummary"); b.AppendLine("Research reachable: " + (UnreachableResearch.Count == 0 ? "全部" : "否")); b.AppendLine("Building reachable: " + (UnreachableBuildings.Count == 0 ? "全部" : "否")); b.AppendLine("Resource deadlock: " + (ResourceDeadlocks.Count == 0 ? "None" : "发现")); b.AppendLine("Research cycle: " + (ResearchCycles.Count == 0 ? "None" : "发现")); b.AppendLine("Highest TechLevel: " + HighestTechLevel); return b.ToString();
        }
    }
}
