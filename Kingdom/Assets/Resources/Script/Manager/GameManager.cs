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
    ResourceFinder resourceFinder;
    ResourceManager resourceManager;
    [HideInInspector] public BigNumber UnassignedPopulation, TotalPopulation;
    [SerializeField] private TMP_Text Calendar;
    [SerializeField] private TMP_Text TechLevel;
    [SerializeField] private TMP_Text Food;
    [SerializeField] private TMP_Text KingdomName;
    [SerializeField] private TMP_Text Population;
    [SerializeField] private TMP_Text KingdomSpace;
    private void Start()
    {
        resourceFinder = ResourceManager.Instance.ResourceFinder;
        resourceManager = ResourceManager.Instance;
        resourceManager.WoodSet.AddResource(resourceFinder.WoodLog);
        resourceManager.ResourceGrowthRate[resourceFinder.WoodLog] = BigNumber.E;
    }
    protected override void TickLong()
    {
        base.TickLong();
        UpdateUI();
    }
    private void UpdateUI()
    {
        Calendar.text = $"日历:{System.DateTime.Now}";
        TechLevel.text = "技术等级:茹毛饮血";
        Food.text = "粮食:32121   -32321/s";
        KingdomName.text = "王国:鹰之团";
        Population.text = $"人口:{UnassignedPopulation}/{TotalPopulation}";
        KingdomSpace.text = "领土:1e231";
    }
}
