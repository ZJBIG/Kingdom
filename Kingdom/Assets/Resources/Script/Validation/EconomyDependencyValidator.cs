using System;
using System.Collections.Generic;

public static class EconomyDependencyValidator
{
    public static bool Validate(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Building> buildings,
        IReadOnlyList<Research> researches,
        IReadOnlyList<WorkshopUpgradeDefinition> upgrades,
        out string error)
    {
        if (!ValidateBuildingPrerequisites(buildings, out error))
            return false;
        if (!ValidateWorkshopPrerequisites(upgrades, out error))
            return false;
        if (!ValidateProductionGraph(resources, buildings, out error))
            return false;
        if (!ValidateDirectUnlockDeadlocks(buildings, researches, upgrades, out error))
            return false;
        if (!ValidateReleasedReachability(buildings, researches, upgrades, out error))
            return false;
        error = string.Empty;
        return true;
    }

    private static bool ValidateWorkshopPrerequisites(
        IReadOnlyList<WorkshopUpgradeDefinition> upgrades,
        out string error)
    {
        var known = new HashSet<WorkshopUpgradeDefinition>();
        for (int i = 0; i < upgrades.Count; i++)
            if (upgrades[i] != null)
                known.Add(upgrades[i]);

        for (int i = 0; i < upgrades.Count; i++)
        {
            WorkshopUpgradeDefinition upgrade = upgrades[i];
            if (upgrade == null)
                continue;
            if (upgrade.RequiredResearch.Count == 0 && upgrade.RequiredUpgrades.Count == 0)
            {
                error = $"Workshop upgrade '{upgrade.Id}' has no prerequisite.";
                return false;
            }
            for (int j = 0; j < upgrade.RequiredUpgrades.Count; j++)
            {
                WorkshopUpgradeDefinition prerequisite = upgrade.RequiredUpgrades[j];
                if (prerequisite == null || !known.Contains(prerequisite))
                {
                    error = $"Workshop upgrade '{upgrade.Id}' has an invalid workshop prerequisite.";
                    return false;
                }
                if (ReferenceEquals(upgrade, prerequisite))
                {
                    error = $"Workshop upgrade '{upgrade.Id}' requires itself.";
                    return false;
                }
            }
        }

        var visiting = new HashSet<WorkshopUpgradeDefinition>();
        var visited = new HashSet<WorkshopUpgradeDefinition>();
        for (int i = 0; i < upgrades.Count; i++)
        {
            WorkshopUpgradeDefinition upgrade = upgrades[i];
            if (upgrade != null && HasWorkshopCycle(upgrade, visiting, visited, out error))
                return false;
        }
        error = string.Empty;
        return true;
    }

    private static bool HasWorkshopCycle(
        WorkshopUpgradeDefinition current,
        HashSet<WorkshopUpgradeDefinition> visiting,
        HashSet<WorkshopUpgradeDefinition> visited,
        out string error)
    {
        if (visited.Contains(current))
        {
            error = string.Empty;
            return false;
        }
        if (!visiting.Add(current))
        {
            error = $"Workshop prerequisite cycle includes '{current.Id}'.";
            return true;
        }
        for (int i = 0; i < current.RequiredUpgrades.Count; i++)
        {
            WorkshopUpgradeDefinition prerequisite = current.RequiredUpgrades[i];
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
        IReadOnlyList<WorkshopUpgradeDefinition> upgrades,
        out string error)
    {
        var resources = new HashSet<Resource>();
        var completedResearch = new HashSet<Research>();
        var purchasedUpgrades = new HashSet<WorkshopUpgradeDefinition>();
        var availableBuildings = new HashSet<Building>();
        TechLevel techLevel = TechLevel.Animal;

        for (int i = 0; i < DataBase<Resource>.All.Count; i++)
            if (DataBase<Resource>.All[i] != null && DataBase<Resource>.All[i].Id == "WoodLog")
                resources.Add(DataBase<Resource>.All[i]);

        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < researches.Count; i++)
            {
                Research research = researches[i];
                if (research == null || research.TechLevel > TechLevel.Industrial ||
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
                WorkshopUpgradeDefinition upgrade = upgrades[i];
                if (upgrade == null || upgrade.TechLevel > TechLevel.Industrial ||
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
                if (building == null || building.TechLevel > TechLevel.Industrial ||
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

        for (int i = 0; i < researches.Count; i++)
        {
            Research research = researches[i];
            if (research != null && research.TechLevel <= TechLevel.Industrial &&
                !completedResearch.Contains(research))
            {
                error = DescribeBlockedResearch(research, completedResearch, resources);
                return false;
            }
        }
        for (int i = 0; i < upgrades.Count; i++)
        {
            WorkshopUpgradeDefinition upgrade = upgrades[i];
            if (upgrade != null && upgrade.TechLevel <= TechLevel.Industrial &&
                !purchasedUpgrades.Contains(upgrade))
            {
                error = DescribeBlockedUpgrade(upgrade, completedResearch, purchasedUpgrades, resources);
                return false;
            }
        }
        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            if (building != null && building.TechLevel <= TechLevel.Industrial &&
                !availableBuildings.Contains(building))
            {
                error = DescribeBlockedBuilding(
                    building, completedResearch, purchasedUpgrades, resources);
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static string DescribeBlockedBuilding(
        Building building,
        HashSet<Research> research,
        HashSet<WorkshopUpgradeDefinition> upgrades,
        HashSet<Resource> resources)
    {
        for (int i = 0; i < building.RequiredResearch.Count; i++)
            if (!research.Contains(building.RequiredResearch[i]))
                return $"{building.Id} -> requires {building.RequiredResearch[i].Id} -> research is unreachable";
        for (int i = 0; i < building.RequiredWorkshopUpgrades.Count; i++)
            if (!upgrades.Contains(building.RequiredWorkshopUpgrades[i]))
                return $"{building.Id} -> requires {building.RequiredWorkshopUpgrades[i].Id} -> workshop upgrade is unreachable";
        Resource missing = FirstMissingCost(building.ResourceRequirements, resources);
        return missing == null
            ? $"{building.Id} -> era is unreachable"
            : $"{building.Id} -> costs {missing.Id} -> no reachable producer";
    }

    private static string DescribeBlockedResearch(
        Research value, HashSet<Research> research, HashSet<Resource> resources)
    {
        for (int i = 0; i < value.Prerequisites.Count; i++)
            if (!research.Contains(value.Prerequisites[i]))
                return $"{value.Id} -> requires {value.Prerequisites[i].Id} -> research is unreachable";
        Resource missing = FirstMissingCost(value.ResourceRequirements, resources);
        return missing == null
            ? $"{value.Id} -> era transition is unreachable"
            : $"{value.Id} -> costs {missing.Id} -> no reachable producer";
    }

    private static string DescribeBlockedUpgrade(
        WorkshopUpgradeDefinition value,
        HashSet<Research> research,
        HashSet<WorkshopUpgradeDefinition> upgrades,
        HashSet<Resource> resources)
    {
        for (int i = 0; i < value.RequiredResearch.Count; i++)
            if (!research.Contains(value.RequiredResearch[i]))
                return $"{value.Id} -> requires {value.RequiredResearch[i].Id} -> research is unreachable";
        for (int i = 0; i < value.RequiredUpgrades.Count; i++)
            if (!upgrades.Contains(value.RequiredUpgrades[i]))
                return $"{value.Id} -> requires {value.RequiredUpgrades[i].Id} -> workshop upgrade is unreachable";
        Resource missing = FirstMissingCost(value.ResourceRequirements, resources);
        return missing == null
            ? $"{value.Id} -> industrial-workshop is not unlocked"
            : $"{value.Id} -> costs {missing.Id} -> no reachable producer";
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
        var upgrades = new HashSet<WorkshopUpgradeDefinition>();
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
                    error = $"Building '{building.Id}' has a null research prerequisite.";
                    return false;
                }
                if (!research.Add(value))
                {
                    error = $"Building '{building.Id}' has duplicate research prerequisite '{value.Id}'.";
                    return false;
                }
            }
            upgrades.Clear();
            for (int j = 0; j < building.RequiredWorkshopUpgrades.Count; j++)
            {
                WorkshopUpgradeDefinition value = building.RequiredWorkshopUpgrades[j];
                if (value == null)
                {
                    error = $"Building '{building.Id}' has a null workshop prerequisite.";
                    return false;
                }
                if (!upgrades.Add(value))
                {
                    error = $"Building '{building.Id}' has duplicate workshop prerequisite '{value.Id}'.";
                    return false;
                }
            }
        }
        error = string.Empty;
        return true;
    }

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

        var visiting = new HashSet<Resource>();
        var visited = new HashSet<Resource>();
        var path = new List<Resource>();
        foreach (Resource resource in graph.Keys)
        {
            if (FindProductionCycle(resource, graph, visiting, visited, path, out error))
                return false;
        }
        error = string.Empty;
        return true;
    }

    private static bool FindProductionCycle(
        Resource current,
        Dictionary<Resource, List<Resource>> graph,
        HashSet<Resource> visiting,
        HashSet<Resource> visited,
        List<Resource> path,
        out string error)
    {
        if (visited.Contains(current))
        {
            error = string.Empty;
            return false;
        }
        if (!visiting.Add(current))
        {
            int start = path.IndexOf(current);
            var ids = new List<string>();
            for (int i = Math.Max(0, start); i < path.Count; i++)
                ids.Add(path[i].Id);
            ids.Add(current.Id);
            error = "Production recipe cycle: " + string.Join(" -> ", ids);
            return true;
        }

        path.Add(current);
        List<Resource> outputs = graph[current];
        for (int i = 0; i < outputs.Count; i++)
            if (FindProductionCycle(outputs[i], graph, visiting, visited, path, out error))
                return true;
        path.RemoveAt(path.Count - 1);
        visiting.Remove(current);
        visited.Add(current);
        error = string.Empty;
        return false;
    }

    private static bool ValidateDirectUnlockDeadlocks(
        IReadOnlyList<Building> buildings,
        IReadOnlyList<Research> researches,
        IReadOnlyList<WorkshopUpgradeDefinition> upgrades,
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
                    error = $"{building.Id} -> costs {resource.Id} -> produced only by {building.Id}";
                    return false;
                }
            }
            for (int u = 0; u < building.RequiredWorkshopUpgrades.Count; u++)
            {
                WorkshopUpgradeDefinition upgrade = building.RequiredWorkshopUpgrades[u];
                for (int j = 0; j < upgrade.ResourceRequirements.Count; j++)
                {
                    Resource resource = upgrade.ResourceRequirements[j].First;
                    if (OnlyProducerIs(producers, resource, building))
                    {
                        error = $"{building.Id} -> requires {upgrade.Id} -> costs {resource.Id} " +
                            $"-> produced only by {building.Id}";
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
                        error = $"{building.Id} -> requires {research.Id} -> costs {resource.Id} " +
                            $"-> produced only by {building.Id}";
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
        resource.Id != "WoodLog" &&
        producers.TryGetValue(resource, out List<Building> values) &&
        values.Count == 1 &&
        values[0] == building;
}
