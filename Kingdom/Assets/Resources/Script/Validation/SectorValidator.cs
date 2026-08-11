using System.Collections.Generic;
using System.Text;

public static class SectorValidator
{
    private static readonly ExpantaNum MinimumInterstellarTerritoryReward =
        new ExpantaNum(100000d);
    // 战役进度的极限速度约为 multiplier / 30，远星战役至少应持续半小时。
    private static readonly ExpantaNum MaximumInterstellarProgressMultiplier =
        new ExpantaNum(1d / 60d);

    public static bool ValidateDefinitions(
        IEnumerable<SectorDefinition> sectors,
        out string error)
    {
        if (sectors == null)
        {
            error = "星区验证失败：定义集合为空。";
            return false;
        }

        var definitions = new List<SectorDefinition>();
        var ids = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (SectorDefinition sector in sectors)
        {
            if (sector == null)
            {
                error = "星区验证失败：定义集合包含空引用。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(sector.Id) || !ids.Add(sector.Id))
            {
                error = $"星区验证失败：编号“{sector.Id}”重复或为空。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(sector.Label) || string.IsNullOrWhiteSpace(sector.Description))
            {
                error = $"星区验证失败：“{sector.Id}”缺少名称或描述。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(sector.StarSystemId) ||
                (sector.IsHomeSystem && sector.StarSystemId != SectorDefinition.HomeSystemId) ||
                (!sector.IsHomeSystem && sector.StarSystemId == SectorDefinition.HomeSystemId))
            {
                error = $"星区验证失败：“{sector.Id}”的领域与星系配置无效。";
                return false;
            }
            if (sector.EnemyPower.IsNaN || sector.EnemyPower < ExpantaNum.Zero ||
                sector.TerritoryReward.IsNaN || sector.TerritoryReward <= ExpantaNum.Zero)
            {
                error = $"星区验证失败：“{sector.Id}”的敌方强度或领土奖励无效。";
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
                        error = $"星区验证失败：“{sector.Id}”的前置星区列表无效。";
                        return false;
                    }
                }
            }

            if (sector.IsHomeSystem)
            {
                if (!ValidateCosts(sector.ColonizationFoodPerSecond, sector.ColonizationResourceRatesPerSecond, sector.Id, "探索", out error))
                    return false;
            }
            else if (!ValidateCosts(sector.CampaignFoodPerSecond, sector.CampaignResourceRatesPerSecond, sector.Id, "战役", out error) ||
                     sector.CampaignProgressMultiplier <= ExpantaNum.Zero ||
                     sector.CampaignProgressMultiplier > MaximumInterstellarProgressMultiplier)
            {
                error = $"星区验证失败：“{sector.Id}”的战役节奏或成本无效。";
                return false;
            }
            else if (!ValidateInterstellarCampaignScale(sector, out error))
            {
                return false;
            }
        }

        if (!ValidateNoCycles(definitions, out error))
            return false;

        return ValidateProgressionReachability(definitions, out error);
    }

    private static bool ValidateInterstellarCampaignScale(
        SectorDefinition sector,
        out string error)
    {
        if (sector.TerritoryReward < MinimumInterstellarTerritoryReward)
        {
            error = $"星际战役验证失败：'{sector.Id}' 的领土回报必须至少为 100000。";
            return false;
        }

        IReadOnlyList<Pair<Resource, ExpantaNum>> costs =
            sector.CampaignResourceRatesPerSecond;
        bool usesAdvancedResource = false;
        if (costs != null)
        {
            for (int i = 0; i < costs.Count; i++)
            {
                Pair<Resource, ExpantaNum> cost = costs[i];
                if (cost.First != null && cost.Second > ExpantaNum.Zero &&
                    IsAdvancedCampaignResource(cost.First.Id))
                {
                    usesAdvancedResource = true;
                    break;
                }
            }
        }

        if (!usesAdvancedResource)
        {
            error = $"星际战役验证失败：'{sector.Id}' 必须持续消耗钛合金、复合材料或幽影/相位材料。";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsAdvancedCampaignResource(string resourceId)
    {
        return resourceId == "TitaniumAlloy" ||
            resourceId == "Composite" ||
            resourceId == "PhantomAlloy" ||
            resourceId == "PhantomWeave" ||
            resourceId == "PhaseMaterial";
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
            error = "星区验证失败：定义集合为空。";
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
            error = $"星区验证失败：“{sector.Id}”至少需要一种资源奖励。";
            return false;
        }
        for (int i = 0; i < rewards.Count; i++)
        {
            Pair<Resource, ExpantaNum> reward = rewards[i];
            if (reward.First == null || reward.Second.IsNaN || reward.Second <= ExpantaNum.Zero)
            {
                error = $"星区验证失败：“{sector.Id}”包含无效的资源奖励。";
                return false;
            }
        }
        error = null;
        return true;
    }

    private static bool ValidateCosts(
        ExpantaNum foodPerSecond,
        IReadOnlyList<Pair<Resource, ExpantaNum>> costs,
        string sectorId,
        string operation,
        out string error)
    {
        if (foodPerSecond.IsNaN || foodPerSecond < ExpantaNum.Zero || costs == null)
        {
            error = $"星区验证失败：“{sectorId}”的{operation}成本无效。";
            return false;
        }
        for (int i = 0; i < costs.Count; i++)
        {
            Pair<Resource, ExpantaNum> cost = costs[i];
            if (cost.First == null || cost.Second.IsNaN || cost.Second < ExpantaNum.Zero)
            {
                error = $"星区验证失败：“{sectorId}”包含无效的{operation}资源成本。";
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
            error = "星区验证失败：定义集合包含空引用。";
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
                    error = $"星区验证失败：“{sector.name}”包含空的前置星区。";
                    return false;
                }
                if (!uniquePrerequisites.Add(prerequisite))
                {
                    error = $"星区验证失败：“{sector.name}”重复引用前置星区“{prerequisite.name}”。";
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
        var builder = new StringBuilder("星区依赖循环：");
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
