using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Create", menuName = "Data/Building", order = 0)]
public class Building : GameDefinition
{
    private const double DefaultCostGrowthValue = 1.15d;
    public string Label;
    public string Description;
    public TechLevel TechLevel;
    [SerializeField, Tooltip("Optional next tier in a building line. This is definition-only until the housing line runtime is enabled.")]
    private Building upgradeTo;
    [SerializeField, Tooltip("Each new building multiplies its material cost by this ratio.")]
    private ExpantaNum costGrowth = new ExpantaNum(DefaultCostGrowthValue);
    [Header("功能")]
    [SerializeField, Tooltip("每建造一个该建筑占用的领土。")]
    private ExpantaNum spaceCost;
    [SerializeField, Tooltip("每建造一个该建筑消耗的可用生产力。")]
    private ExpantaNum productivityConsumption;
    [SerializeField, Tooltip("每个已建成建筑提供的可用生产力。")]
    private ExpantaNum productivityGranted;
    [SerializeField, Tooltip("每个已建成建筑提供的人口容量。")]
    private ExpantaNum populationCapacityGranted;
    [SerializeField, Tooltip("每个建筑每秒提供的研究力。")]
    private ExpantaNum researchPowerGranted;
    [SerializeField, Tooltip("每个建筑每秒生产的粮食。")]
    private ExpantaNum foodProductionRate;
    [SerializeField, Tooltip("每个建筑每秒消耗的粮食。")]
    private ExpantaNum foodConsumptionRate;
    [SerializeField, Tooltip("每个建筑提供的食物储存容量。")]
    private ExpantaNum foodCapacityGranted;
    [SerializeField, Tooltip("每个建筑每秒提供的电力流量；不是库存。")]
    private ExpantaNum powerProductionRate;
    [SerializeField, Tooltip("每个建筑每秒消耗的电力流量；不是库存。")]
    private ExpantaNum powerConsumptionRate;
    [SerializeField, Tooltip("每个建筑每秒提供的物流吞吐；不是库存。")]
    private ExpantaNum logisticsProductionRate;
    [SerializeField, Tooltip("每个建筑每秒消耗的物流吞吐；不是库存。")]
    private ExpantaNum logisticsConsumptionRate;
    [SerializeField, Tooltip("每个建筑提供的有效舰队力量；不是资源库存。")]
    private ExpantaNum fleetPowerGranted;
    [SerializeField]
    private ExpantaNum attackPowerGranted = ExpantaNum.Zero;
    [SerializeField]
    private ExpantaNum defensePowerGranted = ExpantaNum.Zero;
    [SerializeField]
    private ExpantaNum militaryManpowerGranted = ExpantaNum.Zero;
    [SerializeField]
    private List<Pair<Resource, ExpantaNum>> resourceRequirements = new();
    [SerializeField]
    private List<Pair<Resource, ExpantaNum>> resourceGenerationRates = new();
    [SerializeField]
    private List<Pair<Resource, ExpantaNum>> resourceConsumptionRates = new();
    [Header("Prerequisites")]
    [SerializeField]
    private List<Research> requiredResearch = new();
    [SerializeField]
    private List<WorkshopUpgradeDefinition> requiredWorkshopUpgrades = new();

    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceRequirements => resourceRequirements;
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceGenerationRates => resourceGenerationRates;
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceConsumptionRates => resourceConsumptionRates;
    public IReadOnlyList<Research> RequiredResearch => requiredResearch;
    public IReadOnlyList<WorkshopUpgradeDefinition> RequiredWorkshopUpgrades => requiredWorkshopUpgrades;
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
    public ExpantaNum CostGrowth =>
        costGrowth >= ExpantaNum.One ? costGrowth : new ExpantaNum(DefaultCostGrowthValue);

#if UNITY_EDITOR
    public void ConfigureEconomyForEditor(
        ExpantaNum growth,
        ExpantaNum territory,
        ExpantaNum workforce,
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
        costGrowth = growth;
        spaceCost = territory;
        productivityConsumption = workforce;
        productivityGranted = productivity;
        populationCapacityGranted = populationCapacity;
        researchPowerGranted = researchPower;
        foodProductionRate = foodProduction;
        foodConsumptionRate = foodConsumption;
        foodCapacityGranted = foodCapacity;
        powerProductionRate = powerProduction;
        powerConsumptionRate = powerConsumption;
        logisticsProductionRate = logisticsProduction;
        logisticsConsumptionRate = logisticsConsumption;
        attackPowerGranted = attack;
        defensePowerGranted = defense;
        militaryManpowerGranted = manpower;
        resourceRequirements = requirements ?? new List<Pair<Resource, ExpantaNum>>();
        resourceGenerationRates = generation ?? new List<Pair<Resource, ExpantaNum>>();
        resourceConsumptionRates = consumption ?? new List<Pair<Resource, ExpantaNum>>();
    }

    public void SetRequiredResearchForEditor(List<Research> values) =>
        requiredResearch = values ?? new List<Research>();

    public void SetRequiredWorkshopUpgradesForEditor(List<WorkshopUpgradeDefinition> values) =>
        requiredWorkshopUpgrades = values ?? new List<WorkshopUpgradeDefinition>();

    public void SetUpgradeToForEditor(Building value) => upgradeTo = value;

    public void SetResearchPowerForEditor(ExpantaNum value) =>
        researchPowerGranted = ExpantaNum.Max(ExpantaNum.Zero, value);
    public void SetPowerFlowForEditor(ExpantaNum production, ExpantaNum consumption)
    {
        powerProductionRate = ExpantaNum.Max(ExpantaNum.Zero, production);
        powerConsumptionRate = ExpantaNum.Max(ExpantaNum.Zero, consumption);
    }

    public void SetLogisticsFlowForEditor(ExpantaNum production, ExpantaNum consumption)
    {
        logisticsProductionRate = ExpantaNum.Max(ExpantaNum.Zero, production);
        logisticsConsumptionRate = ExpantaNum.Max(ExpantaNum.Zero, consumption);
    }
#endif

}
