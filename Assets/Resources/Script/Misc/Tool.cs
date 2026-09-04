using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct Pair<T1, T2> : IEquatable<Pair<T1, T2>>
{
    [SerializeField] private T1 first;
    [SerializeField] private T2 second;
    public readonly T1 First => first;
    public readonly T2 Second => second;

    public Pair(T1 first, T2 second)
    {
        this.first = first;
        this.second = second;
    }
    public readonly void Deconstruct(out T1 first, out T2 second)
    {
        first = this.first;
        second = this.second;
    }

    public readonly bool Equals(Pair<T1, T2> other) =>
        EqualityComparer<T1>.Default.Equals(first, other.first)
        && EqualityComparer<T2>.Default.Equals(second, other.second);

    public override readonly bool Equals(object obj) =>
        obj is Pair<T1, T2> other && Equals(other);

    public override readonly int GetHashCode()
    {
        unchecked
        {
            int firstHash = EqualityComparer<T1>.Default.GetHashCode(first);
            int secondHash = EqualityComparer<T2>.Default.GetHashCode(second);
            return (firstHash * 397) ^ secondHash;
        }
    }

    public static bool operator ==(Pair<T1, T2> left, Pair<T1, T2> right) => left.Equals(right);
    public static bool operator !=(Pair<T1, T2> left, Pair<T1, T2> right) => !left.Equals(right);
}

[Serializable]
public sealed class ResourceAmountDefinition
{
    [SerializeField] private Resource resource;
    [SerializeField] private string amount = "0";

    public Resource Resource => resource;
    public ExpantaNum Amount => string.IsNullOrWhiteSpace(amount) ? ExpantaNum.Zero : amount;

    public Pair<Resource, ExpantaNum> ToPair() => new Pair<Resource, ExpantaNum>(resource, Amount);

    public static ResourceAmountDefinition FromPair(Pair<Resource, ExpantaNum> value) =>
        new ResourceAmountDefinition
        {
            resource = value.First,
            amount = value.Second.ToString()
        };
}

public static class ResourceAmountDefinitionList
{
    public static void MergeOpposingPairs(
        IReadOnlyList<Pair<Resource, ExpantaNum>> generation,
        IReadOnlyList<Pair<Resource, ExpantaNum>> consumption,
        ref List<Pair<Resource, ExpantaNum>> mergedGeneration,
        ref List<Pair<Resource, ExpantaNum>> mergedConsumption)
    {
        if (mergedGeneration != null && mergedConsumption != null)
            return;

        var netRates = new Dictionary<Resource, ExpantaNum>();
        AddRates(netRates, generation, false);
        AddRates(netRates, consumption, true);

        mergedGeneration = new List<Pair<Resource, ExpantaNum>>();
        mergedConsumption = new List<Pair<Resource, ExpantaNum>>();
        foreach (KeyValuePair<Resource, ExpantaNum> rate in netRates)
        {
            if (rate.Key == null || rate.Value == ExpantaNum.Zero)
                continue;
            if (rate.Value > ExpantaNum.Zero)
                mergedGeneration.Add(new Pair<Resource, ExpantaNum>(rate.Key, rate.Value));
            else
                mergedConsumption.Add(new Pair<Resource, ExpantaNum>(
                    rate.Key, rate.Value.Abs()));
        }
    }

    private static void AddRates(
        Dictionary<Resource, ExpantaNum> netRates,
        IReadOnlyList<Pair<Resource, ExpantaNum>> rates,
        bool subtract)
    {
        if (rates == null)
            return;
        for (int i = 0; i < rates.Count; i++)
        {
            Pair<Resource, ExpantaNum> rate = rates[i];
            if (rate.First == null)
                continue;
            ExpantaNum amount = subtract ? -rate.Second : rate.Second;
            netRates.TryGetValue(rate.First, out ExpantaNum current);
            netRates[rate.First] = current + amount;
        }
    }

    public static List<Pair<Resource, ExpantaNum>> ToPairs(
        IReadOnlyList<ResourceAmountDefinition> values)
    {
        List<Pair<Resource, ExpantaNum>> result = new();
        if (values == null)
            return result;

        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] != null)
                result.Add(values[i].ToPair());
        }

        return result;
    }

    public static IReadOnlyList<Pair<Resource, ExpantaNum>> ToPairs(
        IReadOnlyList<ResourceAmountDefinition> values,
        ref List<Pair<Resource, ExpantaNum>> cache)
    {
        if (cache == null)
            cache = ToPairs(values);
        return cache;
    }

    public static List<ResourceAmountDefinition> FromPairs(
        IReadOnlyList<Pair<Resource, ExpantaNum>> values)
    {
        List<ResourceAmountDefinition> result = new();
        if (values == null)
            return result;

        for (int i = 0; i < values.Count; i++)
            result.Add(ResourceAmountDefinition.FromPair(values[i]));

        return result;
    }
}

public static class Tool
{
    public static string Colorize(this string s, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{s}</color>";
    public static bool NullOrEmpty(this string str) => string.IsNullOrEmpty(str);

    // Keep enum formatting usable without the reflection-based attribute
    // lookup that was removed for the runtime code path. Known player-facing
    // enums below provide localized labels; other enums safely fall back to
    // their stable member name.
    public static string GetDescription(this Enum value) =>
        value == null ? string.Empty : value.ToString();

    public static string GetDescription(this TechLevel value) => value switch
    {
        TechLevel.Animal => "原始时代",
        TechLevel.StoneAge => "石器时代",
        TechLevel.Medieval => "中古时代",
        TechLevel.Industrial => "工业时代",
        TechLevel.Spacer => "太空时代",
        TechLevel.Ultra => "极致时代",
        TechLevel.Archotech => "远古科技时代",
        _ => value.ToString()
    };

    public static string GetDescription(this ResearchEffectType value) => value switch
    {
        ResearchEffectType.BuildingProductionMultiplier => "建筑生产效率",
        ResearchEffectType.ResourceProductionMultiplier => "资源生产效率",
        ResearchEffectType.GlobalResearchMultiplier => "全局研究效率",
        ResearchEffectType.GlobalConstructionMultiplier => "全局建造效率",
        ResearchEffectType.FoodCapacityMultiplier => "粮食容量",
        ResearchEffectType.ProductivityGranted => "生产力增加",
        ResearchEffectType.TerritoryGranted => "领土增加",
        ResearchEffectType.MilitaryMultiplier => "军事能力",
        ResearchEffectType.PowerMultiplier => "全局电力效率",
        ResearchEffectType.GlobalBuildingProductionMultiplier => "全局建筑生产效率",
        ResearchEffectType.BuildingResearchPowerMultiplier => "建筑研究效率",
        ResearchEffectType.BuildingPowerProductionMultiplier => "建筑电力产出",
        ResearchEffectType.BuildingLogisticsProductionMultiplier => "建筑物流产出",
        ResearchEffectType.GlobalLogisticsMultiplier => "全局物流效率",
        ResearchEffectType.PopulationGrowthMultiplier => "人口增长",
        ResearchEffectType.DeconstructionReturnRate => "拆除返还比例",
        ResearchEffectType.UnlockIndustrialWorkshop => "解锁工业工坊",
        ResearchEffectType.UnlockHomeSystemSurvey => "解锁本星系测绘",
        ResearchEffectType.UnlockDeepSpaceFleet => "解锁深空舰队",
        ResearchEffectType.UnlockInterstellarNavigation => "解锁星际航行",
        ResearchEffectType.FleetRepairCostMultiplier => "舰队维修成本",
        ResearchEffectType.OccupiedResourceProductionMultiplier => "占领资源产出",
        ResearchEffectType.CampaignProgressMultiplier => "远征进度效率",
        ResearchEffectType.CampaignSupplyCostMultiplier => "远征补给成本",
        ResearchEffectType.CampaignCasualtyMultiplier => "远征伤亡",
        ResearchEffectType.PopulationProductivityMultiplier => "人口生产力",
        ResearchEffectType.ExplorationPowerMultiplier => "探索能力",
        ResearchEffectType.BuildingConstructionMultiplier => "建筑建造效率",
        ResearchEffectType.HappinessBonus => "幸福度加成",
        ResearchEffectType.GlobalFoodProductionMultiplier => "全局粮食生产效率",
        _ => value.ToString()
    };

    public static string GetDescription(this WorkshopEffectType value) => value switch
    {
        WorkshopEffectType.BuildingProductionMultiplier => "建筑生产效率",
        WorkshopEffectType.ResourceProductionMultiplier => "资源生产效率",
        WorkshopEffectType.GlobalResearchMultiplier => "全局研究效率",
        WorkshopEffectType.GlobalConstructionMultiplier => "全局建造效率",
        WorkshopEffectType.GlobalFoodProductionMultiplier => "全局食物生产效率",
        WorkshopEffectType.GlobalLogisticsMultiplier => "全局物流效率",
        WorkshopEffectType.GlobalBuildingProductionMultiplier => "全局建筑生产效率",
        WorkshopEffectType.BuildingConstructionMultiplier => "建筑建造效率",
        WorkshopEffectType.BuildingResearchPowerMultiplier => "建筑研究力",
        WorkshopEffectType.BuildingPowerProductionMultiplier => "建筑电力产出",
        WorkshopEffectType.BuildingLogisticsProductionMultiplier => "建筑物流产出",
        WorkshopEffectType.ExplorationPowerMultiplier => "探索能力",
        WorkshopEffectType.TerritoryGranted => "领土增加",
        WorkshopEffectType.MilitaryMultiplier => "军事能力",
        WorkshopEffectType.PowerMultiplier => "全局电力效率",
        WorkshopEffectType.FleetRepairCostMultiplier => "舰队维修成本",
        WorkshopEffectType.PopulationGrowthMultiplier => "人口增长",
        WorkshopEffectType.OccupiedResourceProductionMultiplier => "占领资源产出",
        WorkshopEffectType.CampaignSupplyCostMultiplier => "远征补给成本",
        WorkshopEffectType.CampaignCasualtyMultiplier => "远征伤亡",
        _ => value.ToString()
    };

    public static string GetDescription(this ResearchActionResult value) => value switch
    {
        ResearchActionResult.Invalid => "无效操作",
        ResearchActionResult.PaidOnly => "已支付研究成本",
        ResearchActionResult.Started => "研究已开始",
        ResearchActionResult.Queued => "研究已排队",
        ResearchActionResult.QueuedWaitingResources => "研究已排队，等待资源",
        ResearchActionResult.Cancelled => "已取消排队",
        ResearchActionResult.AlreadyActive => "研究已经在进行",
        ResearchActionResult.AlreadyQueued => "研究已经在队列中",
        ResearchActionResult.Completed => "研究已完成",
        ResearchActionResult.Blocked => "研究尚未解锁",
        ResearchActionResult.InsufficientResources => "资源不足",
        _ => value.ToString()
    };

    public static string GetDescription(this SectorOperationFailure value) => value switch
    {
        SectorOperationFailure.None => "无",
        SectorOperationFailure.UnknownSector => "未知星区",
        SectorOperationFailure.AlreadyUnlocked => "星区已经解锁",
        SectorOperationFailure.AlreadyOccupied => "星区已经占领",
        SectorOperationFailure.PrerequisiteNotOccupied => "前置星区尚未占领",
        SectorOperationFailure.LaunchCenterRequired => "需要发射中心",
        SectorOperationFailure.HomeSystemSurveyRequired => "需要完成本星系测绘研究",
        SectorOperationFailure.InvalidReward => "星区奖励无效",
        SectorOperationFailure.NotUnlocked => "星区尚未解锁",
        SectorOperationFailure.CampaignRequired => "尚未完成远征",
        SectorOperationFailure.CampaignInProgress => "远征正在进行",
        SectorOperationFailure.InvalidDelta => "时间增量无效",
        SectorOperationFailure.InsufficientCampaignSupply => "远征补给不足",
        SectorOperationFailure.InsufficientExplorationPower => "探索能力不足",
        SectorOperationFailure.InvalidCampaignCost => "远征成本无效",
        SectorOperationFailure.CampaignNotAllowedInHomeSystem => "本土星系不允许进行星际战役",
        SectorOperationFailure.ColonizationNotAllowedInInterstellarSystem => "星际星系不允许进行殖民",
        SectorOperationFailure.InterstellarSystemLocked => "星际星系尚未解锁",
        SectorOperationFailure.ColonizationInProgress => "殖民正在进行",
        SectorOperationFailure.NoFleetDamage => "舰队没有受损",
        SectorOperationFailure.InvalidRepairAmount => "维修数量无效",
        SectorOperationFailure.InsufficientFleetRepairSupply => "舰队维修补给不足",
        SectorOperationFailure.FleetRepairRequired => "舰队仍有未维修的损伤",
        _ => value.ToString()
    };
}
