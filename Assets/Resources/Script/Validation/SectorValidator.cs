using System.Collections.Generic;
using System.Text;

public static class SectorValidator
{
    private static readonly ExpantaNum MinimumInterstellarTerritoryReward =
        new ExpantaNum(100000d);
    private static readonly ExpantaNum MinimumInterstellarResourceReward =
        new ExpantaNum(1000000d);
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

        // 后期战役奖励必须足以补偿长距离补给投入，防止出现琐碎奖励的合法定义。
        ExpantaNum totalResourceReward = ExpantaNum.Zero;
        IReadOnlyList<Pair<Resource, ExpantaNum>> rewards = sector.ResourceRewards;
        if (rewards != null)
        {
            for (int i = 0; i < rewards.Count; i++)
            {
                if (rewards[i].First != null)
                    totalResourceReward += rewards[i].Second;
            }
        }
        if (totalResourceReward < MinimumInterstellarResourceReward)
        {
            error = $"星际战役验证失败：'{sector.Id}' 的资源奖励总量必须至少为 1000000。";
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

        var cycles = new Dictionary<string, string>();
        var visited = new HashSet<SectorDefinition>();
        var visiting = new HashSet<SectorDefinition>();
        var path = new List<SectorDefinition>();
        foreach (SectorDefinition sector in sectors)
        {
            if (!Visit(sector, visiting, visited, path, cycles, out error))
                return false;
        }

        if (cycles.Count > 0)
        {
            var cycleMessages = new List<string>(cycles.Values);
            cycleMessages.Sort(System.StringComparer.Ordinal);
            error = "鏄熷尯渚濊禆寰幆锛氭娴嬪埌澶氭潯鐙珛寰幆\\n - " +
                string.Join(System.Environment.NewLine + " - ", cycleMessages);
            error = "\u661f\u533a\u4f9d\u8d56\u5faa\u73af\uff1a\u68c0\u6d4b\u5230\u591a\u6761\u72ec\u7acb\u5faa\u73af" +
                System.Environment.NewLine + " - " +
                string.Join(System.Environment.NewLine + " - ", cycleMessages);
            return false;
        }

        error = null;
        return true;
    }

    private static bool ValidateRewards(SectorDefinition sector, out string error)
    {
        IReadOnlyList<Pair<Resource, ExpantaNum>> rewards = sector.ResourceRewards;
        if (sector.IsHomeSystem)
        {
            if (rewards != null && rewards.Count > 0)
            {
                error = $"星区验证失败：“{sector.Id}”本星系探索不得提供资源奖励。";
                return false;
            }
            error = null;
            return true;
        }
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
        Dictionary<string, string> cycles,
        out string error)
    {
        if (sector != null && visited.Contains(sector))
        {
            error = null;
            return true;
        }
        if (sector == null)
        {
            error = "星区验证失败：定义集合包含空引用。";
            return false;
        }
        if (!visiting.Add(sector))
        {
            string cycleKey = BuildCycleKey(path, sector);
            if (!cycles.ContainsKey(cycleKey))
                cycles.Add(cycleKey, BuildCycleDisplayError(path, sector));
            error = null;
            return true;
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
                if (!Visit(prerequisite, visiting, visited, path, cycles, out error))
                    return false;
            }
        }

        path.RemoveAt(path.Count - 1);
        visiting.Remove(sector);
        visited.Add(sector);
        error = null;
        return true;
    }

    private static string BuildCycleKey(List<SectorDefinition> path, SectorDefinition repeated)
    {
        int start = path.IndexOf(repeated);
        if (start < 0)
            start = 0;

        var ids = new List<string>();
        for (int i = start; i < path.Count; i++)
            ids.Add(path[i].Id);
        ids.Add(repeated.Id);

        int cycleLength = ids.Count - 1;
        string best = null;
        for (int offset = 0; offset < cycleLength; offset++)
        {
            var candidate = new StringBuilder();
            for (int i = 0; i < cycleLength; i++)
            {
                if (i > 0)
                    candidate.Append(" -> ");
                candidate.Append(ids[(offset + i) % cycleLength]);
            }
            candidate.Append(" -> ");
            candidate.Append(ids[offset]);

            string candidateText = candidate.ToString();
            if (best == null || string.CompareOrdinal(candidateText, best) < 0)
                best = candidateText;
        }
        return best;
    }

    private static string BuildCycleDisplayError(List<SectorDefinition> path, SectorDefinition repeated)
    {
        int start = path.IndexOf(repeated);
        if (start < 0)
            start = 0;

        var builder = new StringBuilder("\u661f\u533a\u4f9d\u8d56\u5faa\u73af\uff1a");
        for (int i = start; i < path.Count; i++)
        {
            if (i > start)
                builder.Append(" -> ");
            builder.Append(path[i].Id);
        }
        builder.Append(" -> ");
        builder.Append(repeated.Id);
        return builder.ToString();
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
            builder.Append(path[i].Id);
        }
        builder.Append(" -> ");
        builder.Append(repeated.Id);
        return builder.ToString();
    }
}
