using TMPro;
using UnityEngine;

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

    ResourceFinder ResourceFinder;
    ResourceManager ResourceManager;

    BuildingFinder BuildingFinder;
    BuildingManager BuildingManager;

    [HideInInspector] public string Calendar;
    [HideInInspector] public string KingdomName;
    [HideInInspector] public TechnologyLevel TechLevel;
    [HideInInspector] public BigNumber CurrentFood, FoodGrowthRate;
    [HideInInspector] public BigNumber KingdomSpace;
    [HideInInspector] public BigNumber UnassignedProductivity, TotalProductivity;

    [SerializeField] private Transform Top;

    [SerializeField] private TMP_Text Text_Calendar;
    [SerializeField] private TMP_Text Text_TechLevel;
    [SerializeField] private TMP_Text Text_Food;
    [SerializeField] private TMP_Text Text_KingdomName;
    [SerializeField] private TMP_Text Text_Productivity;
    [SerializeField] private TMP_Text Text_KingdomSpace;


    [Header("Viewer")]
    [SerializeField] private Transform ResourceViewer;
    [SerializeField] private Transform BuildingViewer;
    [SerializeField] private Transform ResearchViewer;
    private void Start()
    {
        ResourceFinder = ResourceManager.Instance.ResourceFinder;
        ResourceManager = ResourceManager.Instance;
        BuildingFinder = BuildingManager.Instance.BuildingFinder;
        BuildingManager = BuildingManager.Instance;

        TechLevel = TechnologyLevel.Spacer;
        KingdomSpace = 1e10;
        CurrentFood = 10000;
        TotalProductivity = 10;
        UnassignedProductivity = 10;

        BuildingManager.AddBuilding(BuildingFinder.Lumberyard);
        BuildingManager.AddBuilding(BuildingFinder.WoodHouse);
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
        Text_Productivity.text = $"生产力:{UnassignedProductivity}/{TotalProductivity}";
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
