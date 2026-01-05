using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : Singleton<GameManager>
{
    public enum TechnologyLevel
    {
        Primitive,
        Medieval,
        Industrial,
        Spacer,
        Ultra,
        Archotech,
        Ascend
    }
    ResourceFinder resourceFinder;
    ResourceManager resourceManager;
    BuildingFinder buildingFinder;
    BuildingManager buildingManager;

    [HideInInspector] public string Calendar;
    [HideInInspector] public string KingdomName;
    [HideInInspector] public TechnologyLevel TechLevel;
    [HideInInspector] public BigNumber CurrentFood, FoodGrowthRate;
    [HideInInspector] public BigNumber KingdomSpace;
    [HideInInspector] public BigNumber UnassignedPopulation, TotalPopulation;

    [SerializeField] private Transform Top;

    [SerializeField] private TMP_Text Text_Calendar;
    [SerializeField] private TMP_Text Text_TechLevel;
    [SerializeField] private TMP_Text Text_Food;
    [SerializeField] private TMP_Text Text_KingdomName;
    [SerializeField] private TMP_Text Text_Population;
    [SerializeField] private TMP_Text Text_KingdomSpace;


    [Header("Viewer")]
    [SerializeField] private Transform ResourceViewer;
    [SerializeField] private Transform BuildingViewer;
    private void Start()
    {
        resourceFinder = ResourceManager.Instance.ResourceFinder;
        resourceManager = ResourceManager.Instance;
        buildingFinder = BuildingManager.Instance.BuildingFinder;
        buildingManager = BuildingManager.Instance;

        TechLevel = TechnologyLevel.Primitive;
        KingdomSpace = 1e10;
        CurrentFood = 10000;
        TotalPopulation = 10;
        UnassignedPopulation = 10;


        resourceManager.AddResource(resourceManager.WoodSet, resourceFinder.WoodLog);
        buildingManager.AddBuilding(buildingFinder.Lumberyard);
        buildingManager.AddBuilding(buildingFinder.WoodHouse);
    }
    protected override void TickLong()
    {
        base.TickLong();
        UpdateUI();
    }
    private void UpdateUI()
    {
        Text_Calendar.text = $"{System.DateTime.Now.ToString().Colorize(Color.green)}";
        Text_TechLevel.text = $"技术等级:{TechLevel}";
        Text_Food.text = $"粮食:{CurrentFood.ToString()}   {(FoodGrowthRate < 0 ? "-" : "+")}{FoodGrowthRate}/s";
        Text_KingdomName.text = $"国名:{KingdomName}";
        Text_Population.text = $"人口:{UnassignedPopulation.ToString().Replace(".0000", "")}/{TotalPopulation.ToString().Replace(".0000", "")}";
        Text_KingdomSpace.text = $"剩余领土:{KingdomSpace}";
    }
    public void SwitchTopUI()
    {
        TMP_Text Text = Top.GetChild(1).GetComponent<TMP_Text>();
        Text.text = Text.text switch
        {
            "资源" => "建筑",
            _ => "资源"
        };
        ResourceViewer.gameObject.SetActive(Text.text == "资源");
        BuildingViewer.gameObject.SetActive(Text.text == "建筑");
    }
}
