using System;
using System.Collections.Generic;
using System.Text;

public static class EconomyDependencyValidator
{
    public static bool Validate(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Building> buildings,
        IReadOnlyList<Research> researches,
        IReadOnlyList<WorkshopUpgrade> upgrades,
        out string error,
        TechLevel validationCeiling = TechLevel.Archotech)
    {
        if (!ValidateBuildingPrerequisites(buildings, out error))
            return false;
        if (!ResearchValidator.ValidateNoCycles(researches, out error))
            return false;
        if (!ValidateResearchEffects(researches, out error))
            return false;
        if (!ValidateWorkshopPrerequisites(upgrades, out error))
            return false;
        if (!ValidateProductionGraph(resources, buildings, out error))
            return false;
        if (!ValidateDirectUnlockDeadlocks(buildings, researches, upgrades, out error))
            return false;
        if (!ValidateReleasedReachability(
            buildings, researches, upgrades, out error, validationCeiling))
            return false;
        error = string.Empty;
        return true;
    }

    // 内容质量门槛：每条科研必须至少有一个真实效果，防止空效果节点合法通过。
    private static bool ValidateResearchEffects(
        IReadOnlyList<Research> researches,
        out string error)
    {
        for (int i = 0; i < researches.Count; i++)
        {
            Research research = researches[i];
            if (research == null)
                continue;
            if (research.Effects == null || research.Effects.Count == 0)
            {
                error = $"科研“{research.Id}”没有任何效果，必须至少声明一个真实效果。";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    public static bool ValidateWorkshopPrerequisitesForEditor(
        IReadOnlyList<WorkshopUpgrade> upgrades,
        out string error) =>
        ValidateWorkshopPrerequisites(upgrades, out error);
#endif

    private static bool ValidateWorkshopPrerequisites(
        IReadOnlyList<WorkshopUpgrade> upgrades,
        out string error)
    {
        var known = new HashSet<WorkshopUpgrade>();
        for (int i = 0; i < upgrades.Count; i++)
        {
            WorkshopUpgrade upgrade = upgrades[i];
            if (upgrade == null)
            {
                error = "工坊升级定义集合包含空引用。";
                return false;
            }
            known.Add(upgrade);
        }

        for (int i = 0; i < upgrades.Count; i++)
        {
            WorkshopUpgrade upgrade = upgrades[i];
            if (upgrade == null)
                continue;
            if (upgrade.RequiredResearch.Count == 0 && upgrade.RequiredUpgrades.Count == 0)
            {
                error = $"工坊升级“{upgrade.Id}”缺少前置条件。";
                return false;
            }
            var uniqueResearch = new HashSet<Research>();
            for (int j = 0; j < upgrade.RequiredResearch.Count; j++)
            {
                Research prerequisite = upgrade.RequiredResearch[j];
                if (prerequisite == null)
                {
                    error = $"工坊升级“{upgrade.Id}”包含无效的研究前置条件。";
                    return false;
                }
                if (!uniqueResearch.Add(prerequisite))
                {
                    error = $"工坊升级“{upgrade.Id}”重复声明研究前置条件“{prerequisite.Id}”。";
                    return false;
                }
            }
            var uniqueUpgrades = new HashSet<WorkshopUpgrade>();
            for (int j = 0; j < upgrade.RequiredUpgrades.Count; j++)
            {
                WorkshopUpgrade prerequisite = upgrade.RequiredUpgrades[j];
                if (prerequisite == null || !known.Contains(prerequisite))
                {
                    error = $"工坊升级“{upgrade.Id}”包含无效的工坊前置条件。";
                    return false;
                }
                if (!uniqueUpgrades.Add(prerequisite))
                {
                    error = $"工坊升级“{upgrade.Id}”重复声明工坊前置条件“{prerequisite.Id}”。";
                    return false;
                }
                if (ReferenceEquals(upgrade, prerequisite))
                {
                    error = $"工坊升级“{upgrade.Id}”不能要求自身作为前置。";
                    return false;
                }
            }
        }

        var visiting = new HashSet<WorkshopUpgrade>();
        var visited = new HashSet<WorkshopUpgrade>();
        for (int i = 0; i < upgrades.Count; i++)
        {
            WorkshopUpgrade upgrade = upgrades[i];
            if (upgrade != null && HasWorkshopCycle(upgrade, visiting, visited, out error))
                return false;
        }
        error = string.Empty;
        return true;
    }

    private static bool HasWorkshopCycle(
        WorkshopUpgrade current,
        HashSet<WorkshopUpgrade> visiting,
        HashSet<WorkshopUpgrade> visited,
        out string error)
    {
        if (visited.Contains(current))
        {
            error = string.Empty;
            return false;
        }
        if (!visiting.Add(current))
        {
            error = $"工坊前置条件存在循环，涉及“{current.Id}”。";
            return true;
        }
        for (int i = 0; i < current.RequiredUpgrades.Count; i++)
        {
            WorkshopUpgrade prerequisite = current.RequiredUpgrades[i];
            if (prerequisite != null && HasWorkshopCycle(prerequisite, visiting, visited, out error))
                return true;
        }
        visiting.Remove(current);
        visited.Add(current);
        error = string.Empty;
        return false;
    }

    private static bool ValidateReleasedReachability(
        IReadOnlyList<Building> buildings,
        IReadOnlyList<Research> researches,
        IReadOnlyList<WorkshopUpgrade> upgrades,
        out string error,
        TechLevel validationCeiling)
    {
        var resources = new HashSet<Resource>();
        var completedResearch = new HashSet<Research>();
        var purchasedUpgrades = new HashSet<WorkshopUpgrade>();
        var availableBuildings = new HashSet<Building>();
        TechLevel techLevel = TechLevel.Animal;

        for (int i = 0; i < DataBase<Resource>.All.Count; i++)
            if (DataBase<Resource>.All[i] != null &&
                DataBase<Resource>.All[i].Id == ResourceManager.StartingResourceId)
                resources.Add(DataBase<Resource>.All[i]);

        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < researches.Count; i++)
            {
                Research research = researches[i];
                if (research == null || research.TechLevel > validationCeiling ||
                    completedResearch.Contains(research))
                    continue;
                bool eraAccessible = research.TechLevel <= techLevel ||
                    research.AdvancesTechLevel && (int)research.TechLevel == (int)techLevel + 1;
                if (!eraAccessible ||
                    !AllContained(research.Prerequisites, completedResearch) ||
                    !CostsReachable(research.ResourceRequirements, resources))
                    continue;
                completedResearch.Add(research);
                if (research.AdvancesTechLevel && research.TechLevel > techLevel)
                    techLevel = research.TechLevel;
                changed = true;
            }

            bool workshopSystemUnlocked = ContainsId(completedResearch, "IndustrialWorkshop");
            for (int i = 0; i < upgrades.Count; i++)
            {
                WorkshopUpgrade upgrade = upgrades[i];
                if (upgrade == null || upgrade.TechLevel > validationCeiling ||
                    purchasedUpgrades.Contains(upgrade) || !workshopSystemUnlocked)
                    continue;
                if (!AllContained(upgrade.RequiredResearch, completedResearch) ||
                    !AllContained(upgrade.RequiredUpgrades, purchasedUpgrades) ||
                    !CostsReachable(upgrade.ResourceRequirements, resources))
                    continue;
                purchasedUpgrades.Add(upgrade);
                changed = true;
            }

            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                if (building == null || building.TechLevel > validationCeiling ||
                    availableBuildings.Contains(building) || building.TechLevel > techLevel)
                    continue;
                if (!AllContained(building.RequiredResearch, completedResearch) ||
                    !AllContained(building.RequiredWorkshopUpgrades, purchasedUpgrades) ||
                    !CostsReachable(building.ResourceRequirements, resources))
                    continue;
                availableBuildings.Add(building);
                for (int j = 0; j < building.ResourceGenerationRates.Count; j++)
                {
                    Resource output = building.ResourceGenerationRates[j].First;
                    if (output != null)
                        resources.Add(output);
                }
                changed = true;
            }
        }
        while (changed);

        var blockedDefinitions = new List<string>();
        var tracedResearch = new HashSet<Research>();
        for (int i = 0; i < researches.Count; i++)
        {
            Research research = researches[i];
            if (research != null && research.TechLevel <= validationCeiling &&
                !completedResearch.Contains(research))
            {
                blockedDefinitions.Add(DescribeBlockedResearch(
                    research,
                    completedResearch,
                    resources,
                    tracedResearch));
            }
        }
        for (int i = 0; i < upgrades.Count; i++)
        {
            WorkshopUpgrade upgrade = upgrades[i];
            if (upgrade != null && upgrade.TechLevel <= validationCeiling &&
                !purchasedUpgrades.Contains(upgrade))
            {
                blockedDefinitions.Add(DescribeBlockedUpgrade(upgrade, completedResearch, purchasedUpgrades, resources));
            }
        }
        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            if (building != null && building.TechLevel <= validationCeiling &&
                !availableBuildings.Contains(building))
            {
                blockedDefinitions.Add(DescribeBlockedBuilding(
                    building, completedResearch, purchasedUpgrades, resources));
            }
        }

        if (blockedDefinitions.Count > 0)
        {
            error = "存在无法到达的内容定义：\n" + string.Join("\n", blockedDefinitions);
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string DescribeBlockedBuilding(
        Building building,
        HashSet<Research> research,
        HashSet<WorkshopUpgrade> upgrades,
        HashSet<Resource> resources)
    {
        for (int i = 0; i < building.RequiredResearch.Count; i++)
            if (!research.Contains(building.RequiredResearch[i]))
                return $"{building.Id} -> 需要研究 {building.RequiredResearch[i].Id} -> 研究不可达";
        for (int i = 0; i < building.RequiredWorkshopUpgrades.Count; i++)
            if (!upgrades.Contains(building.RequiredWorkshopUpgrades[i]))
                return $"{building.Id} -> 需要工坊升级 {building.RequiredWorkshopUpgrades[i].Id} -> 工坊升级不可达";
        Resource missing = FirstMissingCost(building.ResourceRequirements, resources);
        return missing == null
            ? $"{building.Id} -> 所属时代不可达"
            : $"{building.Id} -> 需要 {missing.Id} -> 没有可达的生产者";
    }

    private static string DescribeBlockedResearch(
        Research value,
        HashSet<Research> research,
        HashSet<Resource> resources,
        HashSet<Research> tracedResearch)
    {
        for (int i = 0; i < value.Prerequisites.Count; i++)
            if (value.Prerequisites[i] == null || !research.Contains(value.Prerequisites[i]))
            {
                if (tracedResearch.Contains(value))
                    return $"{value.Id} -> 共享前置诊断已展开";
                var trace = new StringBuilder();
                AppendResearchTrace(
                    value,
                    research,
                    new HashSet<Research>(),
                    tracedResearch,
                    trace,
                    string.Empty);
                return trace.ToString();
            }
        Resource missing = FirstMissingCost(value.ResourceRequirements, resources);
        return missing == null
            ? $"{value.Id} -> 时代跃迁不可达"
            : $"{value.Id} -> 需要 {missing.Id} -> 没有可达的生产者";
    }

    private static void AppendResearchTrace(
        Research value,
        HashSet<Research> reachable,
        HashSet<Research> visiting,
        HashSet<Research> visited,
        StringBuilder trace,
        string indent)
    {
        if (value == null)
            return;
        if (!visiting.Add(value))
        {
            trace.Append(indent).Append(value.Id).Append(" -> 检测到依赖循环\n");
            return;
        }
        if (visited.Contains(value))
        {
            visiting.Remove(value);
            return;
        }

        for (int i = 0; i < value.Prerequisites.Count; i++)
        {
            Research prerequisite = value.Prerequisites[i];
            if (prerequisite == null || reachable.Contains(prerequisite))
                continue;

            trace.Append(indent)
                .Append(value.Id)
                .Append(" -> 需要 ")
                .Append(prerequisite == null ? "<null>" : prerequisite.Id)
                .Append(" -> 研究不可达\n");
            AppendResearchTrace(
                prerequisite,
                reachable,
                visiting,
                visited,
                trace,
                indent + "  ");
        }

        visiting.Remove(value);
        visited.Add(value);
    }

    private static string DescribeBlockedUpgrade(
        WorkshopUpgrade value,
        HashSet<Research> research,
        HashSet<WorkshopUpgrade> upgrades,
        HashSet<Resource> resources)
    {
        for (int i = 0; i < value.RequiredResearch.Count; i++)
            if (!research.Contains(value.RequiredResearch[i]))
                return $"{value.Id} -> 需要研究 {value.RequiredResearch[i].Id} -> 研究不可达";
        for (int i = 0; i < value.RequiredUpgrades.Count; i++)
            if (!upgrades.Contains(value.RequiredUpgrades[i]))
                return $"{value.Id} -> 需要工坊升级 {value.RequiredUpgrades[i].Id} -> 工坊升级不可达";
        Resource missing = FirstMissingCost(value.ResourceRequirements, resources);
        return missing == null
            ? $"{value.Id} -> 工业工坊系统尚未解锁"
            : $"{value.Id} -> 需要 {missing.Id} -> 没有可达的生产者";
    }

    private static Resource FirstMissingCost(
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs, HashSet<Resource> resources)
    {
        for (int i = 0; i < costs.Count; i++)
            if (costs[i].First != null && !resources.Contains(costs[i].First))
                return costs[i].First;
        return null;
    }

    private static bool CostsReachable(
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs, HashSet<Resource> resources) =>
        FirstMissingCost(costs, resources) == null;

    private static bool AllContained<T>(
        IReadOnlyList<T> required, HashSet<T> completed) where T : class
    {
        for (int i = 0; i < required.Count; i++)
            if (required[i] == null || !completed.Contains(required[i]))
                return false;
        return true;
    }

    private static bool ContainsId(HashSet<Research> values, string id)
    {
        foreach (Research value in values)
            if (value != null && value.Id == id)
                return true;
        return false;
    }

    private static bool ValidateBuildingPrerequisites(
        IReadOnlyList<Building> buildings,
        out string error)
    {
        var research = new HashSet<Research>();
        var upgrades = new HashSet<WorkshopUpgrade>();
        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            if (building == null)
                continue;
            research.Clear();
            for (int j = 0; j < building.RequiredResearch.Count; j++)
            {
                Research value = building.RequiredResearch[j];
                if (value == null)
                {
                    error = $"建筑“{building.Id}”包含空的研究前置条件。";
                    return false;
                }
                if (!research.Add(value))
                {
                    error = $"建筑“{building.Id}”重复声明研究前置条件“{value.Id}”。";
                    return false;
                }
            }
            upgrades.Clear();
            for (int j = 0; j < building.RequiredWorkshopUpgrades.Count; j++)
            {
                WorkshopUpgrade value = building.RequiredWorkshopUpgrades[j];
                if (value == null)
                {
                    error = $"建筑“{building.Id}”包含空的工坊前置条件。";
                    return false;
                }
                if (!upgrades.Add(value))
                {
                    error = $"建筑“{building.Id}”重复声明工坊前置条件“{value.Id}”。";
                    return false;
                }
            }
        }
        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    public static bool ValidateProductionGraphForEditor(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Building> buildings,
        out string error) =>
        ValidateProductionGraph(resources, buildings, out error);
#endif

    private static bool ValidateProductionGraph(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Building> buildings,
        out string error)
    {
        var graph = new Dictionary<Resource, List<Resource>>();
        for (int i = 0; i < resources.Count; i++)
            if (resources[i] != null)
                graph[resources[i]] = new List<Resource>();

        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            if (building == null)
                continue;
            for (int inputIndex = 0; inputIndex < building.ResourceConsumptionRates.Count; inputIndex++)
            {
                Resource input = building.ResourceConsumptionRates[inputIndex].First;
                if (input == null)
                    continue;
                for (int outputIndex = 0; outputIndex < building.ResourceGenerationRates.Count; outputIndex++)
                {
                    Resource output = building.ResourceGenerationRates[outputIndex].First;
                    if (output != null && !graph[input].Contains(output))
                        graph[input].Add(output);
                }
            }
        }

        var cycles = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<Resource>();
        foreach (Resource resource in graph.Keys)
            FindProductionCycles(
                resource,
                graph,
                new HashSet<Resource>(),
                new List<Resource>(),
                cycles,
                visited);

        var blockedCycles = new List<string>();
        foreach (string cycle in cycles)
        {
            if (!IsBootstrappableCycle(cycle, resources, buildings))
                blockedCycles.Add(cycle);
        }

        if (blockedCycles.Count > 0)
        {
            blockedCycles.Sort(StringComparer.Ordinal);
            error = "\u751F\u4EA7\u914D\u65B9\u5B58\u5728\u5FAA\u73AF\u4F9D\u8D56\uFF1A" + Environment.NewLine +
                " - " + string.Join(Environment.NewLine + " - ", blockedCycles);
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsBootstrappableCycle(
        string cycle,
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Building> buildings)
    {
        string[] ids = cycle.Split(new[] { " -> " }, StringSplitOptions.RemoveEmptyEntries);
        var cycleResources = new HashSet<Resource>();
        for (int i = 0; i < ids.Length - 1; i++)
        {
            for (int resourceIndex = 0; resourceIndex < resources.Count; resourceIndex++)
            {
                Resource resource = resources[resourceIndex];
                if (resource != null && resource.Id == ids[i])
                {
                    cycleResources.Add(resource);
                    break;
                }
            }
        }

        if (cycleResources.Count == 0)
            return false;

        for (int buildingIndex = 0; buildingIndex < buildings.Count; buildingIndex++)
        {
            Building building = buildings[buildingIndex];
            if (building == null || !GeneratesAny(building, cycleResources))
                continue;

            bool hasExternalInput = building.ResourceConsumptionRates.Count == 0;
            for (int inputIndex = 0;
                 inputIndex < building.ResourceConsumptionRates.Count && !hasExternalInput;
                 inputIndex++)
            {
                Resource input = building.ResourceConsumptionRates[inputIndex].First;
                if (input == null || !cycleResources.Contains(input))
                    hasExternalInput = true;
            }

            if (hasExternalInput)
                return true;
        }

        return false;
    }

    private static bool GeneratesAny(Building building, HashSet<Resource> resources)
    {
        for (int outputIndex = 0; outputIndex < building.ResourceGenerationRates.Count; outputIndex++)
        {
            Resource output = building.ResourceGenerationRates[outputIndex].First;
            if (output != null && resources.Contains(output))
                return true;
        }

        return false;
    }

    private static void FindProductionCycles(
        Resource current,
        Dictionary<Resource, List<Resource>> graph,
        HashSet<Resource> pathSet,
        List<Resource> path,
        HashSet<string> cycles,
        HashSet<Resource> visited)
    {
        if (visited.Contains(current))
            return;
        if (pathSet.Contains(current))
        {
            int cycleStart = path.IndexOf(current);
            if (cycleStart >= 0)
                cycles.Add(CanonicalizeCycle(path, cycleStart));
            return;
        }
        pathSet.Add(current);
        path.Add(current);
        List<Resource> outputs = graph[current];
        for (int i = 0; i < outputs.Count; i++)
            FindProductionCycles(outputs[i], graph, pathSet, path, cycles, visited);
        path.RemoveAt(path.Count - 1);
        pathSet.Remove(current);
        visited.Add(current);
    }

    private static string CanonicalizeCycle(List<Resource> path, int startIndex)
    {
        int count = path.Count - startIndex;
        var ids = new string[count];
        for (int i = 0; i < count; i++)
            ids[i] = path[startIndex + i].Id;

        int bestOffset = 0;
        for (int offset = 1; offset < count; offset++)
        {
            for (int i = 0; i < count; i++)
            {
                int candidateIndex = (offset + i) % count;
                int bestIndex = (bestOffset + i) % count;
                int comparison = StringComparer.Ordinal.Compare(ids[candidateIndex], ids[bestIndex]);
                if (comparison < 0)
                {
                    bestOffset = offset;
                    break;
                }
                if (comparison > 0)
                    break;
            }
        }

        var orderedIds = new List<string>(count + 1);
        for (int i = 0; i < count; i++)
            orderedIds.Add(ids[(bestOffset + i) % count]);
        orderedIds.Add(orderedIds[0]);
        return string.Join(" -> ", orderedIds);
    }

    private static bool ValidateDirectUnlockDeadlocks(
        IReadOnlyList<Building> buildings,
        IReadOnlyList<Research> researches,
        IReadOnlyList<WorkshopUpgrade> upgrades,
        out string error)
    {
        var producers = new Dictionary<Resource, List<Building>>();
        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            if (building == null)
                continue;
            for (int j = 0; j < building.ResourceGenerationRates.Count; j++)
            {
                Resource resource = building.ResourceGenerationRates[j].First;
                if (resource == null)
                    continue;
                if (!producers.TryGetValue(resource, out List<Building> values))
                {
                    values = new List<Building>();
                    producers.Add(resource, values);
                }
                values.Add(building);
            }
        }

        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            if (building == null)
                continue;
            for (int j = 0; j < building.ResourceRequirements.Count; j++)
            {
                Resource resource = building.ResourceRequirements[j].First;
                if (OnlyProducerIs(producers, resource, building))
                {
                    error = $"{building.Id} -> 需要 {resource.Id} -> 该资源只能由自身生产";
                    return false;
                }
            }
            for (int u = 0; u < building.RequiredWorkshopUpgrades.Count; u++)
            {
                WorkshopUpgrade upgrade = building.RequiredWorkshopUpgrades[u];
                for (int j = 0; j < upgrade.ResourceRequirements.Count; j++)
                {
                    Resource resource = upgrade.ResourceRequirements[j].First;
                    if (OnlyProducerIs(producers, resource, building))
                    {
                        error = $"{building.Id} -> 需要工坊升级 {upgrade.Id} -> 消耗 {resource.Id} " +
                            $"-> 该资源只能由自身生产";
                        return false;
                    }
                }
            }
            for (int r = 0; r < building.RequiredResearch.Count; r++)
            {
                Research research = building.RequiredResearch[r];
                for (int j = 0; j < research.ResourceRequirements.Count; j++)
                {
                    Resource resource = research.ResourceRequirements[j].First;
                    if (OnlyProducerIs(producers, resource, building))
                    {
                        error = $"{building.Id} -> 需要研究 {research.Id} -> 消耗 {resource.Id} " +
                            $"-> 该资源只能由自身生产";
                        return false;
                    }
                }
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool OnlyProducerIs(
        Dictionary<Resource, List<Building>> producers,
        Resource resource,
        Building building) =>
        resource != null &&
        resource.Id != ResourceManager.StartingResourceId &&
        producers.TryGetValue(resource, out List<Building> values) &&
        values.Count == 1 &&
        values[0] == building;
}
