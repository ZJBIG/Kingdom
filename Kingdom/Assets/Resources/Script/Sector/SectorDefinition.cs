using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Create", menuName = "Data/Sector", order = 0)]
public sealed class SectorDefinition : GameDefinition
{
    public string Label;
    public string Description;
    [SerializeField] private ExpantaNum enemyPower;
    [SerializeField] private List<SectorDefinition> prerequisiteSectors = new();
    [SerializeField] private ExpantaNum territoryReward;
    [SerializeField] private List<Pair<Resource, ExpantaNum>> resourceRewards = new();
    [SerializeField] private ExpantaNum campaignFoodPerMinute = new ExpantaNum(1);
    [SerializeField] private List<Pair<Resource, ExpantaNum>> campaignResourceCosts = new();
    [SerializeField] private bool repeatable;
    [SerializeField] private Sprite background;
    [SerializeField] private Sprite icon;
    [SerializeField] private float mapX;
    [SerializeField] private float mapY;

    public ExpantaNum EnemyPower => enemyPower;
    public IReadOnlyList<SectorDefinition> PrerequisiteSectors => prerequisiteSectors;
    public ExpantaNum TerritoryReward => territoryReward;
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceRewards => resourceRewards;
    public ExpantaNum CampaignFoodPerMinute => campaignFoodPerMinute;
    public IReadOnlyList<Pair<Resource, ExpantaNum>> CampaignResourceCosts => campaignResourceCosts;
    public bool Repeatable => repeatable;
    public Sprite Background => background;
    public Sprite Icon => icon;
    public float MapX => mapX;
    public float MapY => mapY;

#if UNITY_EDITOR
    public void SetPrerequisitesForEditor(List<SectorDefinition> values) =>
        prerequisiteSectors = values ?? new List<SectorDefinition>();

    public void SetRewardsForEditor(ExpantaNum territory, List<Pair<Resource, ExpantaNum>> resources)
    {
        territoryReward = ExpantaNum.Max(ExpantaNum.Zero, territory);
        resourceRewards = resources ?? new List<Pair<Resource, ExpantaNum>>();
    }

    public void SetCampaignCostsForEditor(
        ExpantaNum foodPerMinute,
        List<Pair<Resource, ExpantaNum>> resources)
    {
        campaignFoodPerMinute = ExpantaNum.Max(ExpantaNum.Zero, foodPerMinute);
        campaignResourceCosts = resources ?? new List<Pair<Resource, ExpantaNum>>();
    }
#endif
}
