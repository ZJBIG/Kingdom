using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Relic", menuName = "Data/Relic")]
public sealed class RelicDef : GameDefinition
{
    [SerializeField] private string label;
    [SerializeField, TextArea] private string description;
    [SerializeField] private SectorDef sector;
    [SerializeField] private List<ResearchDef> requiredResearch = new();
    [SerializeField] private List<BuildingDef> requiredBuildings = new();
    [SerializeField] private RelicWorkDefinition investigation = new();
    [SerializeField] private RelicWorkDefinition repair = new();
    [SerializeField] private RelicWorkDefinition reverseEngineering = new();
    [SerializeField] private RelicWorkDefinition commission = new();
    [SerializeField] private List<ResourceAmountDefinition> supportCraftCosts = new();
    [SerializeField] private string campaignSupplyMultiplier = "0.85";
    [NonSerialized] private List<Pair<ResourceDef, ExpantaNum>> supportCraftCostsCache;
    public string Label => label;
    public string Description => description;
    public SectorDef Sector => sector;
    public IReadOnlyList<ResearchDef> RequiredResearch => requiredResearch;
    public IReadOnlyList<BuildingDef> RequiredBuildings => requiredBuildings;
    public RelicWorkDefinition Investigation => investigation;
    public RelicWorkDefinition Repair => repair;
    public RelicWorkDefinition ReverseEngineering => reverseEngineering;
    public RelicWorkDefinition Commission => commission;
    public IReadOnlyList<Pair<ResourceDef, ExpantaNum>> SupportCraftCosts =>
        RelicWorkDefinition.ToValidatedPairs(supportCraftCosts, ref supportCraftCostsCache);
    public ExpantaNum CampaignSupplyMultiplier => campaignSupplyMultiplier;
#if UNITY_EDITOR
    private void OnValidate()
    {
        supportCraftCostsCache = null;
        investigation?.Invalidate();
        repair?.Invalidate();
        reverseEngineering?.Invalidate();
        commission?.Invalidate();
    }
#endif
}

[Serializable]
public sealed class RelicWorkDefinition
{
    [SerializeField] private List<ResourceAmountDefinition> startupCosts = new();
    [SerializeField] private List<ResourceAmountDefinition> continuousCosts = new();
    [SerializeField] private string durationSeconds = "0";
    [SerializeField] private string foodConsumptionRate = "0";
    [SerializeField] private string powerConsumptionRate = "0";
    [SerializeField] private string logisticsConsumptionRate = "0";
    [NonSerialized] private List<Pair<ResourceDef, ExpantaNum>> startupCostsCache;
    [NonSerialized] private List<Pair<ResourceDef, ExpantaNum>> continuousCostsCache;
    [NonSerialized] private bool ratesInitialized;
    [NonSerialized] private ExpantaNum cachedDuration, cachedFood, cachedPower, cachedLogistics;
    public IReadOnlyList<Pair<ResourceDef, ExpantaNum>> StartupCosts =>
        ToValidatedPairs(startupCosts, ref startupCostsCache);
    public IReadOnlyList<Pair<ResourceDef, ExpantaNum>> ContinuousCosts =>
        ToValidatedPairs(continuousCosts, ref continuousCostsCache);
    public ExpantaNum DurationSeconds { get { EnsureRates(); return cachedDuration; } }
    public ExpantaNum FoodConsumptionRate { get { EnsureRates(); return cachedFood; } }
    public ExpantaNum PowerConsumptionRate { get { EnsureRates(); return cachedPower; } }
    public ExpantaNum LogisticsConsumptionRate { get { EnsureRates(); return cachedLogistics; } }
    private void EnsureRates()
    {
        if (ratesInitialized) return;
        if (!ExpantaNum.TryParse(durationSeconds, out cachedDuration) || !cachedDuration.IsFinite || cachedDuration <= ExpantaNum.Zero ||
            !ExpantaNum.TryParse(foodConsumptionRate, out cachedFood) || !cachedFood.IsFinite || cachedFood < ExpantaNum.Zero ||
            !ExpantaNum.TryParse(powerConsumptionRate, out cachedPower) || !cachedPower.IsFinite || cachedPower < ExpantaNum.Zero ||
            !ExpantaNum.TryParse(logisticsConsumptionRate, out cachedLogistics) || !cachedLogistics.IsFinite || cachedLogistics < ExpantaNum.Zero)
            throw new InvalidOperationException("Invalid relic duration or service rate.");
        ratesInitialized = true;
    }
    internal static IReadOnlyList<Pair<ResourceDef, ExpantaNum>> ToValidatedPairs(
        IReadOnlyList<ResourceAmountDefinition> values, ref List<Pair<ResourceDef, ExpantaNum>> cache)
    {
        if (cache != null) return cache;
        if (values == null) throw new InvalidOperationException("Missing relic resource costs.");
        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int i = 0; i < values.Count; i++)
        {
            ResourceAmountDefinition value = values[i];
            if (value == null || value.Resource == null || string.IsNullOrWhiteSpace(value.Resource.Id))
                throw new InvalidOperationException("Invalid relic resource reference.");
            ExpantaNum amount = value.Amount;
            if (!amount.IsFinite || amount <= ExpantaNum.Zero || !ids.Add(value.Resource.Id))
                throw new InvalidOperationException("Invalid or duplicate relic resource cost.");
        }
        return ResourceAmountDefinitionList.ToPairs(values, ref cache);
    }
    internal void Invalidate()
    {
        startupCostsCache = null;
        continuousCostsCache = null;
        ratesInitialized = false;
    }
}
