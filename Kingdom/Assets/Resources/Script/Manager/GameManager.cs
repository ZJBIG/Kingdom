using System.Collections;
using TMPro;
using UnityEditor;
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
    public BigNumber UnassignedPopulation, TotalPopulation;
    [SerializeField] private TMP_Text Calendar;
    [SerializeField] private TMP_Text TechLevel;
    [SerializeField] private TMP_Text KingdomName;
    [SerializeField] private TMP_Text Population;
    private void Start()
    {
        resourceFinder = ResourceManager.Instance.ResourceFinder;
        resourceManager = ResourceManager.Instance;
        resourceManager.WoodSet.AddResource(resourceFinder.WoodLog);
    }
    protected override void TickLong()
    {
        base.TickLong();
        UpdateUI();
    }
    private void UpdateUI()
    {
        Calendar.text = "5500年12月21日";
        TechLevel.text = "科技等级:茹毛饮血";
        KingdomName.text = "鹰之团";
        Population.text = $"{UnassignedPopulation}/{TotalPopulation}";
    }
}
