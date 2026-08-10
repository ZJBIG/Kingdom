using System.Collections.Generic;
using System.Text;

public static class SectorValidator
{
    public static bool ValidateDefinitions(
        IEnumerable<SectorDefinition> sectors,
        out string error)
    {
        if (sectors == null)
        {
            error = "Sector validation failed: definition collection is null.";
            return false;
        }

        var definitions = new List<SectorDefinition>();
        var ids = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (SectorDefinition sector in sectors)
        {
            if (sector == null)
            {
                error = "Sector validation failed: definition collection contains null.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(sector.Id) || !ids.Add(sector.Id))
            {
                error = $"Sector validation failed: duplicate or empty ID '{sector.Id}'.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(sector.Label) || string.IsNullOrWhiteSpace(sector.Description))
            {
                error = $"Sector validation failed: '{sector.Id}' requires a label and description.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(sector.StarSystemId) ||
                (sector.IsHomeSystem && sector.StarSystemId != SectorDefinition.HomeSystemId) ||
                (!sector.IsHomeSystem && sector.StarSystemId == SectorDefinition.HomeSystemId))
            {
                error = $"Sector validation failed: '{sector.Id}' has an invalid domain/system pair.";
                return false;
            }
            if (sector.EnemyPower.IsNaN || sector.EnemyPower < ExpantaNum.Zero ||
                sector.TerritoryReward.IsNaN || sector.TerritoryReward <= ExpantaNum.Zero)
            {
                error = $"Sector validation failed: '{sector.Id}' has invalid power or territory reward.";
                return false;
            }
            if (!ValidateRewards(sector, out error))
                return false;
            definitions.Add(sector);
        }

        var known = new HashSet<SectorDefinition>(definitions);
        for (int i = 0; i < definitions.Count; i++)
        {
            SectorDefinition sector = definitions[i];
            IReadOnlyList<SectorDefinition> prerequisites = sector.PrerequisiteSectors;
            var unique = new HashSet<SectorDefinition>();
            if (prerequisites != null)
            {
                for (int j = 0; j < prerequisites.Count; j++)
                {
                    SectorDefinition prerequisite = prerequisites[j];
                    if (prerequisite == null || !known.Contains(prerequisite) ||
                        prerequisite == sector || !unique.Add(prerequisite))
                    {
                        error = $"Sector validation failed: '{sector.Id}' has an invalid prerequisite list.";
                        return false;
                    }
                }
            }

            if (sector.IsHomeSystem)
            {
                if (!ValidateCosts(sector.ColonizationFoodPerMinute, sector.ColonizationResourceCosts, sector.Id, "exploration", out error))
                    return false;
            }
            else if (!ValidateCosts(sector.CampaignFoodPerMinute, sector.CampaignResourceCosts, sector.Id, "campaign", out error) ||
                     sector.CampaignProgressMultiplier <= ExpantaNum.Zero)
            {
                error = $"Sector validation failed: '{sector.Id}' has invalid campaign pacing or costs.";
                return false;
            }
        }

        if (!ValidateNoCycles(definitions, out error))
            return false;

        return ValidateProgressionReachability(definitions, out error);
    }

    /// <summary>
    /// 确保每个远星星区都存在从本星系出发的可玩路径。
    /// 远星星区可以依赖另一个远星星区，但前置链最终必须回到本星系探索区。
    /// 本星系探索不能反向依赖后续远星路线。
    /// </summary>
    public static bool ValidateProgressionReachability(
        IEnumerable<SectorDefinition> sectors,
        out string error)
    {
        if (sectors == null)
        {
            error = "星区进度验证失败：定义集合为空。";
            return false;
        }

        var definitions = new List<SectorDefinition>();
        foreach (SectorDefinition sector in sectors)
        {
            if (sector == null)
            {
                error = "星区进度验证失败：定义集合包含空定义。";
                return false;
            }
            definitions.Add(sector);
        }

        bool hasHomeSector = false;
        for (int i = 0; i < definitions.Count; i++)
            hasHomeSector |= definitions[i].IsHomeSystem;
        if (!hasHomeSector)
        {
            error = "星区进度验证失败：不存在本星系探索区。";
            return false;
        }

        var memo = new Dictionary<SectorDefinition, bool>();
        for (int i = 0; i < definitions.Count; i++)
        {
            SectorDefinition sector = definitions[i];
            IReadOnlyList<SectorDefinition> prerequisites = sector.PrerequisiteSectors;
            if (sector.IsHomeSystem)
            {
                if (prerequisites == null)
                    continue;
                for (int j = 0; j < prerequisites.Count; j++)
                {
                    if (prerequisites[j] != null && !prerequisites[j].IsHomeSystem)
                    {
                        error = $"星区进度验证失败：本星系星区“{sector.Id}”依赖远星星区“{prerequisites[j].Id}”。";
                        return false;
                    }
                }
                continue;
            }

            if (prerequisites == null || prerequisites.Count == 0)
            {
                error = $"星区进度验证失败：远星星区“{sector.Id}”没有本星系入口前置。";
                return false;
            }

            var visiting = new HashSet<SectorDefinition>();
            if (!ReachesHomeSector(sector, memo, visiting))
            {
                error = $"星区进度验证失败：远星星区“{sector.Id}”无法从本星系探索区到达。";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool ReachesHomeSector(
        SectorDefinition sector,
        Dictionary<SectorDefinition, bool> memo,
        HashSet<SectorDefinition> visiting)
    {
        if (sector.IsHomeSystem)
            return true;
        if (memo.TryGetValue(sector, out bool cached))
            return cached;
        if (!visiting.Add(sector))
            return false;

        bool reachesHome = false;
        IReadOnlyList<SectorDefinition> prerequisites = sector.PrerequisiteSectors;
        if (prerequisites != null)
        {
            for (int i = 0; i < prerequisites.Count; i++)
            {
                SectorDefinition prerequisite = prerequisites[i];
                if (prerequisite != null && ReachesHomeSector(prerequisite, memo, visiting))
                {
                    reachesHome = true;
                    break;
                }
            }
        }

        visiting.Remove(sector);
        memo[sector] = reachesHome;
        return reachesHome;
    }

    public static bool ValidateNoCycles(
        IEnumerable<SectorDefinition> sectors,
        out string error)
    {
        if (sectors == null)
        {
            error = "Sector validation failed: definition collection is null.";
            return false;
        }

        var visiting = new HashSet<SectorDefinition>();
        var visited = new HashSet<SectorDefinition>();
        var path = new List<SectorDefinition>();
        foreach (SectorDefinition sector in sectors)
        {
            if (!Visit(sector, visiting, visited, path, out error))
                return false;
        }

        error = null;
        return true;
    }

    private static bool ValidateRewards(SectorDefinition sector, out string error)
    {
        IReadOnlyList<Pair<Resource, ExpantaNum>> rewards = sector.ResourceRewards;
        if (rewards == null || rewards.Count == 0)
        {
            error = $"Sector validation failed: '{sector.Id}' requires at least one resource reward.";
            return false;
        }
        for (int i = 0; i < rewards.Count; i++)
        {
            Pair<Resource, ExpantaNum> reward = rewards[i];
            if (reward.First == null || reward.Second.IsNaN || reward.Second <= ExpantaNum.Zero)
            {
                error = $"Sector validation failed: '{sector.Id}' has an invalid resource reward.";
                return false;
            }
        }
        error = null;
        return true;
    }

    private static bool ValidateCosts(
        ExpantaNum foodPerMinute,
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs,
        string sectorId,
        string operation,
        out string error)
    {
        if (foodPerMinute.IsNaN || foodPerMinute < ExpantaNum.Zero || costs == null)
        {
            error = $"Sector validation failed: '{sectorId}' has invalid {operation} costs.";
            return false;
        }
        for (int i = 0; i < costs.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = costs[i];
            if (cost.First == null || cost.Second.IsNaN || cost.Second < ExpantaNum.Zero)
            {
                error = $"Sector validation failed: '{sectorId}' has an invalid {operation} resource cost.";
                return false;
            }
        }
        error = null;
        return true;
    }

    private static bool Visit(
        SectorDefinition sector,
        HashSet<SectorDefinition> visiting,
        HashSet<SectorDefinition> visited,
        List<SectorDefinition> path,
        out string error)
    {
        if (sector == null)
        {
            error = "Sector validation failed: definition collection contains null.";
            return false;
        }
        if (visited.Contains(sector))
        {
            error = null;
            return true;
        }
        if (!visiting.Add(sector))
        {
            error = BuildCycleError(path, sector);
            return false;
        }

        path.Add(sector);
        var uniquePrerequisites = new HashSet<SectorDefinition>();
        if (sector.PrerequisiteSectors != null)
        {
            foreach (SectorDefinition prerequisite in sector.PrerequisiteSectors)
            {
                if (prerequisite == null)
                {
                    error = $"Sector validation failed: '{sector.name}' contains a null prerequisite.";
                    return false;
                }
                if (!uniquePrerequisites.Add(prerequisite))
                {
                    error = $"Sector validation failed: '{sector.name}' contains duplicate prerequisite '{prerequisite.name}'.";
                    return false;
                }
                if (!Visit(prerequisite, visiting, visited, path, out error))
                    return false;
            }
        }

        path.RemoveAt(path.Count - 1);
        visiting.Remove(sector);
        visited.Add(sector);
        error = null;
        return true;
    }

    private static string BuildCycleError(List<SectorDefinition> path, SectorDefinition repeated)
    {
        int start = path.IndexOf(repeated);
        if (start < 0)
            start = 0;
        var builder = new StringBuilder("Sector dependency cycle: ");
        for (int i = start; i < path.Count; i++)
        {
            if (i > start)
                builder.Append(" -> ");
            builder.Append(path[i].name);
        }
        builder.Append(" -> ");
        builder.Append(repeated.name);
        return builder.ToString();
    }
}
