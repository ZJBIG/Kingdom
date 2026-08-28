using System;
using System.ComponentModel;

public enum ResearchEffectType
{
    [Description("建筑生产效率")]
    BuildingProductionMultiplier = 1,
    [Description("资源生产效率")]
    ResourceProductionMultiplier = 3,
    [Description("全局研究效率")]
    GlobalResearchMultiplier = 4,
    [Description("全局建造效率")]
    GlobalConstructionMultiplier = 5,
    [Description("粮食容量")]
    FoodCapacityMultiplier = 6,
    [Description("生产力增加")]
    ProductivityGranted = 7,
    [Description("领土增加")]
    TerritoryGranted = 8,
    [Description("军事能力")]
    MilitaryMultiplier = 10,
    [Description("全局电力效率")]
    PowerMultiplier = 11,
    [Description("全局建筑生产效率")]
    GlobalBuildingProductionMultiplier = 12,
    [Description("建筑研究效率")]
    BuildingResearchPowerMultiplier = 13,
    [Description("建筑电力产出")]
    BuildingPowerProductionMultiplier = 14,
    [Description("建筑物流产出")]
    BuildingLogisticsProductionMultiplier = 15,
    [Description("全局物流效率")]
    GlobalLogisticsMultiplier = 16,
    [Description("人口增长")]
    PopulationGrowthMultiplier = 17,
    [Description("拆除返还比例")]
    DeconstructionReturnRate = 22,
    [Description("解锁工业工坊")]
    UnlockIndustrialWorkshop = 18,
    [Description("解锁本星系测绘")]
    UnlockHomeSystemSurvey = 19,
    [Description("解锁深空舰队")]
    UnlockDeepSpaceFleet = 20,
    [Description("解锁星际航行")]
    UnlockInterstellarNavigation = 21,
    [Description("舰队维修成本")]
    FleetRepairCostMultiplier = 23,
    [Description("占领资源产出")]
    OccupiedResourceProductionMultiplier = 24,
    [Description("远征进度效率")]
    CampaignProgressMultiplier = 25,
    [Description("远征补给成本")]
    CampaignSupplyCostMultiplier = 26,
    [Description("远征伤亡")]
    CampaignCasualtyMultiplier = 27,
    [Description("人口生产力")]
    PopulationProductivityMultiplier = 28,
    [Description("探索能力")]
    ExplorationPowerMultiplier = 29,
    [Description("建筑建造效率")]
    BuildingConstructionMultiplier = 30,
    [Description("幸福度加成")]
    HappinessBonus = 31,
    [Description("全局粮食生产效率")]
    GlobalFoodProductionMultiplier = 32
}

public enum ResearchSystem
{
    None = 0,
    IndustrialWorkshop = 1,
    HomeSystemSurvey = 2,
    DeepSpaceFleet = 3,
    InterstellarNavigation = 4
}

[Serializable]
public sealed class ResearchEffectDefinition
{
    public ResearchEffectType Type;
    public Building Building;
    public Resource Resource;
    [UnityEngine.SerializeField] private string value = "1";

    public string Value
    {
        get => value;
        set => this.value = value ?? "0";
    }

    public ExpantaNum NumericValue => value;
}
