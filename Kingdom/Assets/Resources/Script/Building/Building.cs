using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Create", menuName = "Data/Building", order = 0)]
public class Building : GameDefinition
{
    private const double DefaultCostGrowthValue = 1.15d;
    public string Label;
    public string Description;
    public TechLevel TechLevel;
    [SerializeField, Tooltip("Each new building multiplies its material cost by this ratio.")]
    private ExpantaNum costGrowth = new ExpantaNum(DefaultCostGrowthValue);
    [Header("功能")]
    [SerializeField, Tooltip("自动建造一个该建筑需要累计的工作量；0 表示不可自动建造。")]
    private ExpantaNum autoBuildWorkRequired;
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
    [SerializeField]
    private List<Pair<Resource, ExpantaNum>> resourceRequirements = new();
    [SerializeField]
    private List<Pair<Resource, ExpantaNum>> resourceGenerationRates = new();
    [SerializeField]
    private List<Pair<Resource, ExpantaNum>> resourceConsumptionRates = new();

    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceRequirements => resourceRequirements;
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceGenerationRates => resourceGenerationRates;
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceConsumptionRates => resourceConsumptionRates;
    public ExpantaNum AutoBuildWorkRequired => autoBuildWorkRequired;
    public ExpantaNum SpaceCost => spaceCost;
    public ExpantaNum ProductivityConsumption => productivityConsumption;
    public ExpantaNum ProductivityGranted => productivityGranted;
    public ExpantaNum PopulationCapacityGranted => populationCapacityGranted;
    public ExpantaNum ResearchPowerGranted => researchPowerGranted;
    public ExpantaNum FoodProductionRate => foodProductionRate;
    public ExpantaNum FoodConsumptionRate => foodConsumptionRate;
    public ExpantaNum FoodCapacityGranted => foodCapacityGranted;
    public ExpantaNum CostGrowth =>
        costGrowth >= ExpantaNum.One ? costGrowth : new ExpantaNum(DefaultCostGrowthValue);

#if UNITY_EDITOR
    public void SetResearchPowerForEditor(ExpantaNum value) =>
        researchPowerGranted = ExpantaNum.Max(ExpantaNum.Zero, value);
#endif

}
