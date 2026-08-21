using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "创建建筑", menuName = "数据/建筑", order = 0)]
public class Building : GameDefinition
{
    private const double DefaultCostGrowthValue = 1.15d;
    public string Label;
    [TextArea]public string Description;
    public TechLevel TechLevel;
    [SerializeField, Tooltip("建筑升级链中的可选下一级建筑。")]
    private Building upgradeTo;
    [SerializeField, Tooltip("每增加一座建筑，材料成本按此倍率增长。")]
    private string costGrowth = DefaultCostGrowthValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
    [Header("功能")]
    [SerializeField, Tooltip("每建造一个该建筑占用的领土。")]
    private string spaceCost = "0";
    [SerializeField, Tooltip("每建造一个该建筑消耗的可用生产力。")]
    private string productivityConsumption = "0";
    [SerializeField, Tooltip("每个已建成建筑提供的可用生产力。")]
    private string productivityGranted = "0";
    [SerializeField, Tooltip("每个已建成建筑提供的人口容量。")]
    private string populationCapacityGranted = "0";
    [SerializeField, Tooltip("每个建筑每秒提供的研究力。")]
    private string researchPowerGranted = "0";
    [SerializeField, Tooltip("每个建筑每秒生产的粮食。")]
    private string foodProductionRate = "0";
    [SerializeField, Tooltip("每个建筑每秒消耗的粮食。")]
    private string foodConsumptionRate = "0";
    [SerializeField, Tooltip("每个建筑提供的食物储存容量。")]
    private string foodCapacityGranted = "0";
    [SerializeField, Tooltip("每个建筑每秒提供的电力流量。")]
    private string powerProductionRate = "0";
    [SerializeField, Tooltip("每个建筑每秒消耗的电力流量。")]
    private string powerConsumptionRate = "0";
    [SerializeField, Tooltip("每个建筑每秒提供的物流吞吐。")]
    private string logisticsProductionRate = "0";
    [SerializeField, Tooltip("每个建筑每秒消耗的物流吞吐。")]
    private string logisticsConsumptionRate = "0";
    [SerializeField, Tooltip("每个建筑提供的有效舰队力量。")]
    private string fleetPowerGranted = "0";
    [SerializeField]
    private string attackPowerGranted = "0";
    [SerializeField]
    private string defensePowerGranted = "0";
    [SerializeField]
    private string militaryManpowerGranted = "0";
    [SerializeField]
    private List<ResourceAmountDefinition> resourceRequirements = new();
    [SerializeField]
    private List<ResourceAmountDefinition> resourceGenerationRates = new();
    [SerializeField]
    private List<ResourceAmountDefinition> resourceConsumptionRates = new();
    [Header("前置条件")]
    [SerializeField]
    private List<Research> requiredResearch = new();
    [SerializeField]
    private List<WorkshopUpgrade> requiredWorkshopUpgrades = new();

    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceRequirements => ResourceAmountDefinitionList.ToPairs(resourceRequirements);
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceGenerationRates => ResourceAmountDefinitionList.ToPairs(resourceGenerationRates);
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceConsumptionRates => ResourceAmountDefinitionList.ToPairs(resourceConsumptionRates);
    public IReadOnlyList<Research> RequiredResearch => requiredResearch;
    public IReadOnlyList<WorkshopUpgrade> RequiredWorkshopUpgrades => requiredWorkshopUpgrades;
    public Building UpgradeTo => upgradeTo;
    public ExpantaNum SpaceCost => spaceCost;
    public ExpantaNum ProductivityConsumption => productivityConsumption;
    public ExpantaNum ProductivityGranted => productivityGranted;
    public ExpantaNum PopulationCapacityGranted => populationCapacityGranted;
    public ExpantaNum ResearchPowerGranted => researchPowerGranted;
    public ExpantaNum FoodProductionRate => foodProductionRate;
    public ExpantaNum FoodConsumptionRate => foodConsumptionRate;
    public ExpantaNum FoodCapacityGranted => foodCapacityGranted;
    public ExpantaNum PowerProductionRate => powerProductionRate;
    public ExpantaNum PowerConsumptionRate => powerConsumptionRate;
    public ExpantaNum LogisticsProductionRate => logisticsProductionRate;
    public ExpantaNum LogisticsConsumptionRate => logisticsConsumptionRate;
    public ExpantaNum FleetPowerGranted => fleetPowerGranted;
    public ExpantaNum AttackPowerGranted => attackPowerGranted;
    public ExpantaNum DefensePowerGranted => defensePowerGranted;
    public ExpantaNum MilitaryManpowerGranted => militaryManpowerGranted;
    private ExpantaNum costGrowthValue => costGrowth;
    public ExpantaNum CostGrowth =>
        costGrowthValue >= ExpantaNum.One ? costGrowthValue : new ExpantaNum(DefaultCostGrowthValue);
    public bool HasValidCostGrowth =>
        !CostGrowth.IsNaN &&
        !CostGrowth.IsInfinity &&
        CostGrowth >= ExpantaNum.One;

#if UNITY_EDITOR
    public void ConfigureEconomyForEditor(
        ExpantaNum growth,
        ExpantaNum territory,
        ExpantaNum productivityConsumptionValue,
        ExpantaNum productivity,
        ExpantaNum populationCapacity,
        ExpantaNum researchPower,
        ExpantaNum foodProduction,
        ExpantaNum foodConsumption,
        ExpantaNum foodCapacity,
        ExpantaNum powerProduction,
        ExpantaNum powerConsumption,
        ExpantaNum logisticsProduction,
        ExpantaNum logisticsConsumption,
        ExpantaNum attack,
        ExpantaNum defense,
        ExpantaNum manpower,
        List<Pair<Resource, ExpantaNum>> requirements,
        List<Pair<Resource, ExpantaNum>> generation,
        List<Pair<Resource, ExpantaNum>> consumption)
    {
        costGrowth = growth.ToString();
        spaceCost = territory.ToString();
        productivityConsumption = productivityConsumptionValue.ToString();
        productivityGranted = productivity.ToString();
        populationCapacityGranted = populationCapacity.ToString();
        researchPowerGranted = researchPower.ToString();
        foodProductionRate = foodProduction.ToString();
        foodConsumptionRate = foodConsumption.ToString();
        foodCapacityGranted = foodCapacity.ToString();
        powerProductionRate = powerProduction.ToString();
        powerConsumptionRate = powerConsumption.ToString();
        logisticsProductionRate = logisticsProduction.ToString();
        logisticsConsumptionRate = logisticsConsumption.ToString();
        attackPowerGranted = attack.ToString();
        defensePowerGranted = defense.ToString();
        militaryManpowerGranted = manpower.ToString();
        resourceRequirements = ResourceAmountDefinitionList.FromPairs(requirements);
        resourceGenerationRates = ResourceAmountDefinitionList.FromPairs(generation);
        resourceConsumptionRates = ResourceAmountDefinitionList.FromPairs(consumption);
    }

    public void SetRequiredResearchForEditor(List<Research> values) =>
        requiredResearch = values ?? new List<Research>();

    public void SetRequiredWorkshopUpgradesForEditor(List<WorkshopUpgrade> values) =>
        requiredWorkshopUpgrades = values ?? new List<WorkshopUpgrade>();

    public void SetUpgradeToForEditor(Building value) => upgradeTo = value;

    public void SetResearchPowerForEditor(ExpantaNum value) =>
        researchPowerGranted = ExpantaNum.Max(ExpantaNum.Zero, value).ToString();
    public void SetPowerFlowForEditor(ExpantaNum production, ExpantaNum consumption)
    {
        powerProductionRate = ExpantaNum.Max(ExpantaNum.Zero, production).ToString();
        powerConsumptionRate = ExpantaNum.Max(ExpantaNum.Zero, consumption).ToString();
    }

    public void SetLogisticsFlowForEditor(ExpantaNum production, ExpantaNum consumption)
    {
        logisticsProductionRate = ExpantaNum.Max(ExpantaNum.Zero, production).ToString();
        logisticsConsumptionRate = ExpantaNum.Max(ExpantaNum.Zero, consumption).ToString();
    }
#endif

}
