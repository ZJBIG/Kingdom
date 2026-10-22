using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "创建星区", menuName = "数据/星区", order = 0)]
public sealed class SectorDef : GameDefinition
{
    public const string HomeSystemId = "Sol";

    private struct ParsedExpantaNumCache
    {
        private string source;
        private ExpantaNum value;
        private bool initialized;

        public ExpantaNum Get(string current)
        {
            if (initialized && object.ReferenceEquals(source, current))
                return value;
            ExpantaNum parsed = current;
            source = current;
            value = parsed;
            initialized = true;
            return parsed;
        }
    }

    public enum SectorDomain
    {
        HomeSystem,
        Interstellar
    }

    [SerializeField] private SectorInteriorExplorationDefinition interiorExploration;
    public SectorInteriorExplorationDefinition InteriorExploration =>
        interiorExploration == null || interiorExploration.IsUnconfigured ? null : interiorExploration;

    public string Label;
    public string Description;
    [SerializeField] private string starSystemId = HomeSystemId;
    [SerializeField] private SectorDomain domain = SectorDomain.HomeSystem;
    [SerializeField] private string enemyPower = "0";
    [SerializeField] private List<SectorDef> prerequisiteSectors = new();
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
    [System.NonSerialized] private List<Pair<ResourceDef, ExpantaNum>> resourceRewardsCache;
    [System.NonSerialized] private List<Pair<ResourceDef, ExpantaNum>> occupiedResourceRatesCache;
    [System.NonSerialized] private List<Pair<ResourceDef, ExpantaNum>> colonizationResourceRatesCache;
    [System.NonSerialized] private List<Pair<ResourceDef, ExpantaNum>> campaignResourceRatesCache;
    [System.NonSerialized] private ParsedExpantaNumCache enemyPowerCache;
    [System.NonSerialized] private ParsedExpantaNumCache colonizationFoodPerSecondCache;
    [System.NonSerialized] private ParsedExpantaNumCache colonizationDurationSecondsCache;
    [System.NonSerialized] private ParsedExpantaNumCache campaignFoodPerSecondCache;

    public ExpantaNum EnemyPower => enemyPowerCache.Get(enemyPower);
    public string StarSystemId => string.IsNullOrWhiteSpace(starSystemId) ? HomeSystemId : starSystemId;
    public SectorDomain Domain => domain;
    public bool IsHomeSystem => domain == SectorDomain.HomeSystem;
    public IReadOnlyList<SectorDef> PrerequisiteSectors => prerequisiteSectors;
    public ExpantaNum TerritoryReward => territoryReward;
    public IReadOnlyList<Pair<ResourceDef, ExpantaNum>> ResourceRewards =>
        ResourceAmountDefinitionList.ToPairs(resourceRewards, ref resourceRewardsCache);
    public IReadOnlyList<Pair<ResourceDef, ExpantaNum>> OccupiedResourceRatesPerSecond =>
        ResourceAmountDefinitionList.ToPairs(occupiedResourceRatesPerSecond, ref occupiedResourceRatesCache);
    public ExpantaNum ColonizationFoodPerSecond => colonizationFoodPerSecondCache.Get(colonizationFoodPerSecond);
    public IReadOnlyList<Pair<ResourceDef, ExpantaNum>> ColonizationResourceRatesPerSecond =>
        ResourceAmountDefinitionList.ToPairs(colonizationResourceRatesPerSecond, ref colonizationResourceRatesCache);
    public ExpantaNum ColonizationDurationSeconds =>
        ExpantaNum.Max(ExpantaNum.One, colonizationDurationSecondsCache.Get(colonizationDurationSeconds));
    public ExpantaNum CampaignFoodPerSecond => campaignFoodPerSecondCache.Get(campaignFoodPerSecond);
    public IReadOnlyList<Pair<ResourceDef, ExpantaNum>> CampaignResourceRatesPerSecond =>
        ResourceAmountDefinitionList.ToPairs(campaignResourceRatesPerSecond, ref campaignResourceRatesCache);
    public ExpantaNum CampaignProgressMultiplier => ExpantaNum.Clamp01(campaignProgressMultiplier);
    public bool Repeatable => repeatable;
    public Sprite Background => background;
    public Sprite Icon => icon;
    public float MapX => mapX;
    public float MapY => mapY;

#if UNITY_EDITOR
    public void SetInteriorExplorationForEditor(SectorInteriorExplorationDefinition value) => interiorExploration = value;

    private void OnValidate()
    {
        interiorExploration?.InvalidateCachedRates();
        resourceRewardsCache = null;
        occupiedResourceRatesCache = null;
        colonizationResourceRatesCache = null;
        campaignResourceRatesCache = null;
    }

    public void SetPrerequisitesForEditor(List<SectorDef> values) =>
        prerequisiteSectors = values ?? new List<SectorDef>();

    public void SetRewardsForEditor(ExpantaNum territory, List<Pair<ResourceDef, ExpantaNum>> resources)
    {
        territoryReward = ExpantaNum.Max(ExpantaNum.Zero, territory).ToString();
        resourceRewards = ResourceAmountDefinitionList.FromPairs(resources);
        resourceRewardsCache = null;
    }

    public void SetOccupiedResourceRatesForEditor(List<Pair<ResourceDef, ExpantaNum>> resourcesPerSecond)
    {
        occupiedResourceRatesPerSecond = ResourceAmountDefinitionList.FromPairs(resourcesPerSecond);
        occupiedResourceRatesCache = null;
    }

    public void SetCampaignCostsForEditor(
        ExpantaNum foodPerSecond,
        List<Pair<ResourceDef, ExpantaNum>> resourcesPerSecond)
    {
        campaignFoodPerSecond = ExpantaNum.Max(ExpantaNum.Zero, foodPerSecond).ToString();
        campaignResourceRatesPerSecond = ResourceAmountDefinitionList.FromPairs(resourcesPerSecond);
        campaignResourceRatesCache = null;
    }

#endif

}

[System.Serializable]
public sealed class SectorInteriorExplorationDefinition
{
    [SerializeField] private string label;
    [SerializeField, TextArea] private string description;
    [SerializeField, TextArea] private string completionDescription;
    [SerializeField] private string durationSeconds = "3600";
    [SerializeField] private List<ResearchDef> requiredResearch = new();
    [SerializeField] private List<ResourceAmountDefinition> resourceRatesPerSecond = new();
    [SerializeField] private List<string> milestones = new();
    [System.NonSerialized] private List<Pair<ResourceDef, ExpantaNum>> ratesCache;
    public bool IsUnconfigured => string.IsNullOrWhiteSpace(label) &&
        string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(completionDescription) &&
        (string.IsNullOrWhiteSpace(durationSeconds) || durationSeconds == "3600") && (requiredResearch == null || requiredResearch.Count == 0) &&
        (resourceRatesPerSecond == null || resourceRatesPerSecond.Count == 0) &&
        (milestones == null || milestones.Count == 0);
    public string Label => label;
    public string Description => description;
    public string CompletionDescription => completionDescription;
    [System.NonSerialized] private string durationSource;
    [System.NonSerialized] private ExpantaNum durationCache;
    public ExpantaNum DurationSeconds
    {
        get
        {
            if (!object.ReferenceEquals(durationSource, durationSeconds))
            {
                durationCache = durationSeconds;
                durationSource = durationSeconds;
            }
            return durationCache;
        }
    }
    public IReadOnlyList<ResearchDef> RequiredResearch => requiredResearch;
    public IReadOnlyList<Pair<ResourceDef, ExpantaNum>> ResourceRatesPerSecond =>
        ResourceAmountDefinitionList.ToPairs(resourceRatesPerSecond, ref ratesCache);
    public IReadOnlyList<string> Milestones => milestones;
    internal void InvalidateCachedRates() => ratesCache = null;
#if UNITY_EDITOR
    public void ConfigureForEditor(string title, ExpantaNum duration,
        List<Pair<ResourceDef, ExpantaNum>> rates, ResearchDef research = null)
    {
        label = title;
        description = title;
        completionDescription = title;
        milestones = new List<string> { title, title, title };
        durationSeconds = duration.ToString();
        resourceRatesPerSecond = ResourceAmountDefinitionList.FromPairs(rates);
        ratesCache = null;
        requiredResearch = research == null ? new List<ResearchDef>() : new List<ResearchDef> { research };
    }
#endif
}
