using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Create", menuName = "Data/Building", order = 0)]
public class Building : GameDefinition
{
    public string Label;
    public string Description;
    public TechLevel TechLevel;
    [Header("功能")]
    [SerializeField, Tooltip("自动建造一个该建筑需要累计的工作量；0 表示不可自动建造。")]
    private ExpantaNum autoBuildWorkRequired;
    [SerializeField, Tooltip("每建造一个该建筑占用的领土。")]
    private ExpantaNum spaceCost;
    [SerializeField, Tooltip("每建造一个该建筑消耗的可用生产力。")]
    private ExpantaNum buildEffort;
    [SerializeField, Tooltip("每个已建成建筑提供的可用生产力。")]
    private ExpantaNum productivityGranted;
    [SerializeField, Tooltip("每个建筑每秒生产的粮食。")]
    private ExpantaNum foodProductionRate;
    [SerializeField, Tooltip("每个建筑每秒消耗的粮食。")]
    private ExpantaNum foodConsumptionRate;
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
    public ExpantaNum BuildEffort => buildEffort;
    public ExpantaNum ProductivityGranted => productivityGranted;
    public ExpantaNum FoodProductionRate => foodProductionRate;
    public ExpantaNum FoodConsumptionRate => foodConsumptionRate;

    [Range(0f, 1f)] public float DeconstructReturnPercentage;

}
