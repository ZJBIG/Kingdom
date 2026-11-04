using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UltraProject", menuName = "Data/Ultra Project", order = 0)]
public sealed class UltraProjectDef : GameDefinition
{
    [SerializeField] private string label;
    [SerializeField, TextArea] private string description;
    [SerializeField] private TechLevel techLevel = TechLevel.Ultra;
    [SerializeField] private List<UltraProjectStageDefinition> stages = new();

    public string Label => label;
    public string Description => description;
    public TechLevel TechLevel => techLevel;
    public IReadOnlyList<UltraProjectStageDefinition> Stages => stages;
}

[Serializable]
public sealed class UltraProjectStageDefinition
{
    [SerializeField] private string stageId;
    [SerializeField] private string prerequisiteStageId;
    // The field names retain the R2 asset schema, while the values are strong
    // Unity references. This keeps authoring and runtime validation aligned.
    [SerializeField] private ResearchDef requiredResearchId;
    [SerializeField] private BuildingDef requiredBuildingId;
    [SerializeField] private List<ResearchDef> additionalRequiredResearch = new();
    [SerializeField] private List<ResourceAmountDefinition> oneTimeResourceCosts = new();
    [SerializeField] private List<ResourceAmountDefinition> continuousResourceCosts = new();
    [SerializeField] private string baseDurationSeconds = "0";
    [SerializeField] private string stablePostureMultiplier = "1";
    [SerializeField] private string rushPostureMultiplier = "1";
    [SerializeField] private string foodConsumptionRate = "0";
    [SerializeField] private string powerConsumptionRate = "0";
    [SerializeField] private string logisticsConsumptionRate = "0";
    [NonSerialized] private List<Pair<ResourceDef, ExpantaNum>> oneTimeResourceCostsCache;
    [NonSerialized] private List<Pair<ResourceDef, ExpantaNum>> continuousResourceCostsCache;
    [NonSerialized] private string[] numericSources;
    [NonSerialized] private ExpantaNum[] numericValues;

    public string StageId => stageId;
    public string PrerequisiteStageId => prerequisiteStageId;
    public ResearchDef RequiredResearch => requiredResearchId;
    public BuildingDef RequiredBuilding => requiredBuildingId;
    public string RequiredResearchId => requiredResearchId == null ? string.Empty : requiredResearchId.Id;
    public string RequiredBuildingId => requiredBuildingId == null ? string.Empty : requiredBuildingId.Id;
    public IReadOnlyList<ResearchDef> AdditionalRequiredResearch => additionalRequiredResearch;
    public IReadOnlyList<Pair<ResourceDef, ExpantaNum>> OneTimeResourceCosts =>
        ResourceAmountDefinitionList.ToPairs(oneTimeResourceCosts, ref oneTimeResourceCostsCache);
    public IReadOnlyList<Pair<ResourceDef, ExpantaNum>> ContinuousResourceCosts =>
        ResourceAmountDefinitionList.ToPairs(continuousResourceCosts, ref continuousResourceCostsCache);
    public ExpantaNum BaseDurationSeconds =>
        GetNumber(0, baseDurationSeconds, ExpantaNum.Zero);
    public ExpantaNum StablePostureMultiplier =>
        GetNumber(1, stablePostureMultiplier, ExpantaNum.One);
    public ExpantaNum RushPostureMultiplier =>
        GetNumber(2, rushPostureMultiplier, ExpantaNum.One);
    public ExpantaNum FoodConsumptionRate =>
        GetNumber(3, foodConsumptionRate, ExpantaNum.Zero);
    public ExpantaNum PowerConsumptionRate =>
        GetNumber(4, powerConsumptionRate, ExpantaNum.Zero);
    public ExpantaNum LogisticsConsumptionRate =>
        GetNumber(5, logisticsConsumptionRate, ExpantaNum.Zero);

    private ExpantaNum GetNumber(int index, string source, ExpantaNum fallback)
    {
        if (numericSources == null)
        {
            numericSources = new string[6];
            numericValues = new ExpantaNum[6];
        }
        if (source == null)
            return fallback;
        if (numericSources[index] != source)
        {
            numericValues[index] = string.IsNullOrWhiteSpace(source) ? fallback : (ExpantaNum)source;
            numericSources[index] = source;
        }
        return numericValues[index];
    }
}
