using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "创建星区", menuName = "数据/星区", order = 0)]
public sealed class SectorDefinition : GameDefinition
{
    public const string HomeSystemId = "Sol";

    public enum SectorDomain
    {
        HomeSystem,
        Interstellar
    }

    public string Label;
    public string Description;
    [SerializeField] private string starSystemId = HomeSystemId;
    [SerializeField] private SectorDomain domain = SectorDomain.HomeSystem;
    [SerializeField] private string enemyPower = "0";
    [SerializeField] private List<SectorDefinition> prerequisiteSectors = new();
    [SerializeField] private string territoryReward = "0";
    [SerializeField] private List<ResourceAmountDefinition> resourceRewards = new();
    [SerializeField] private List<ResourceAmountDefinition> occupiedResourceRatesPerSecond = new();
    [SerializeField] private string colonizationFoodPerSecond = "1";
    [SerializeField] private List<ResourceAmountDefinition> colonizationResourceRatesPerSecond = new();
    // 领地开拓持续时间统一使用秒，避免资源速率与行动时间混用单位。
    [SerializeField] private string colonizationDurationSeconds = "60";
    [SerializeField] private string campaignFoodPerSecond = "1";
    [SerializeField] private List<ResourceAmountDefinition> campaignResourceRatesPerSecond = new();
    [SerializeField] private string campaignProgressMultiplier = "1";
    [SerializeField] private bool repeatable = false;
    [SerializeField] private Sprite background = null;
    [SerializeField] private Sprite icon = null;
    [SerializeField] private float mapX = 0f;
    [SerializeField] private float mapY = 0f;
    [System.NonSerialized] private List<Pair<Resource, ExpantaNum>> resourceRewardsCache;
    [System.NonSerialized] private List<Pair<Resource, ExpantaNum>> occupiedResourceRatesCache;
    [System.NonSerialized] private List<Pair<Resource, ExpantaNum>> colonizationResourceRatesCache;
    [System.NonSerialized] private List<Pair<Resource, ExpantaNum>> campaignResourceRatesCache;

    public ExpantaNum EnemyPower => enemyPower;
    public string StarSystemId => string.IsNullOrWhiteSpace(starSystemId) ? HomeSystemId : starSystemId;
    public SectorDomain Domain => domain;
    public bool IsHomeSystem => domain == SectorDomain.HomeSystem;
    public IReadOnlyList<SectorDefinition> PrerequisiteSectors => prerequisiteSectors;
    public ExpantaNum TerritoryReward => territoryReward;
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceRewards =>
        ResourceAmountDefinitionList.ToPairs(resourceRewards, ref resourceRewardsCache);
    public IReadOnlyList<Pair<Resource, ExpantaNum>> OccupiedResourceRatesPerSecond =>
        ResourceAmountDefinitionList.ToPairs(occupiedResourceRatesPerSecond, ref occupiedResourceRatesCache);
    public ExpantaNum ColonizationFoodPerSecond => colonizationFoodPerSecond;
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ColonizationResourceRatesPerSecond =>
        ResourceAmountDefinitionList.ToPairs(colonizationResourceRatesPerSecond, ref colonizationResourceRatesCache);
    public ExpantaNum ColonizationDurationSeconds => ExpantaNum.Max(ExpantaNum.One, colonizationDurationSeconds);
    public ExpantaNum CampaignFoodPerSecond => campaignFoodPerSecond;
    public IReadOnlyList<Pair<Resource, ExpantaNum>> CampaignResourceRatesPerSecond =>
        ResourceAmountDefinitionList.ToPairs(campaignResourceRatesPerSecond, ref campaignResourceRatesCache);
    public ExpantaNum CampaignProgressMultiplier => ExpantaNum.Clamp01(campaignProgressMultiplier);
    public bool Repeatable => repeatable;
    public Sprite Background => background;
    public Sprite Icon => icon;
    public float MapX => mapX;
    public float MapY => mapY;

#if UNITY_EDITOR
    private void OnValidate()
    {
        resourceRewardsCache = null;
        occupiedResourceRatesCache = null;
        colonizationResourceRatesCache = null;
        campaignResourceRatesCache = null;
    }

    public void SetPrerequisitesForEditor(List<SectorDefinition> values) =>
        prerequisiteSectors = values ?? new List<SectorDefinition>();

    public void SetRewardsForEditor(ExpantaNum territory, List<Pair<Resource, ExpantaNum>> resources)
    {
        territoryReward = ExpantaNum.Max(ExpantaNum.Zero, territory).ToString();
        resourceRewards = ResourceAmountDefinitionList.FromPairs(resources);
        resourceRewardsCache = null;
    }

    public void SetOccupiedResourceRatesForEditor(List<Pair<Resource, ExpantaNum>> resourcesPerSecond)
    {
        occupiedResourceRatesPerSecond = ResourceAmountDefinitionList.FromPairs(resourcesPerSecond);
        occupiedResourceRatesCache = null;
    }

    public void SetLocationForEditor(string systemId, SectorDomain sectorDomain)
    {
        starSystemId = string.IsNullOrWhiteSpace(systemId) ? HomeSystemId : systemId;
        domain = sectorDomain;
    }

    public void SetColonizationCostsForEditor(
        ExpantaNum foodPerSecond,
        List<Pair<Resource, ExpantaNum>> resourcesPerSecond)
    {
        colonizationFoodPerSecond = ExpantaNum.Max(ExpantaNum.Zero, foodPerSecond).ToString();
        colonizationResourceRatesPerSecond = ResourceAmountDefinitionList.FromPairs(resourcesPerSecond);
        colonizationResourceRatesCache = null;
    }

    public void SetCampaignCostsForEditor(
        ExpantaNum foodPerSecond,
        List<Pair<Resource, ExpantaNum>> resourcesPerSecond)
    {
        campaignFoodPerSecond = ExpantaNum.Max(ExpantaNum.Zero, foodPerSecond).ToString();
        campaignResourceRatesPerSecond = ResourceAmountDefinitionList.FromPairs(resourcesPerSecond);
        campaignResourceRatesCache = null;
    }

#endif

}
