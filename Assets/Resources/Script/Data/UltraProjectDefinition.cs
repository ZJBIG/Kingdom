using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UltraProject", menuName = "Data/Ultra Project", order = 0)]
public sealed class UltraProjectDefinition : GameDefinition
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
    [SerializeField] private Research requiredResearchId;
    [SerializeField] private Building requiredBuildingId;
    [SerializeField] private List<Research> additionalRequiredResearch = new();
    [SerializeField] private List<ResourceAmountDefinition> oneTimeResourceCosts = new();
    [SerializeField] private List<ResourceAmountDefinition> continuousResourceCosts = new();
    [SerializeField] private string baseDurationSeconds = "0";
    [SerializeField] private string stablePostureMultiplier = "1";
    [SerializeField] private string rushPostureMultiplier = "1";
    [SerializeField] private string foodConsumptionRate = "0";
    [SerializeField] private string powerConsumptionRate = "0";
    [SerializeField] private string logisticsConsumptionRate = "0";
    [NonSerialized] private List<Pair<Resource, ExpantaNum>> oneTimeResourceCostsCache;
    [NonSerialized] private List<Pair<Resource, ExpantaNum>> continuousResourceCostsCache;

    public string StageId => stageId;
    public string PrerequisiteStageId => prerequisiteStageId;
    public Research RequiredResearch => requiredResearchId;
    public Building RequiredBuilding => requiredBuildingId;
    public string RequiredResearchId => requiredResearchId == null ? string.Empty : requiredResearchId.Id;
    public string RequiredBuildingId => requiredBuildingId == null ? string.Empty : requiredBuildingId.Id;
    public IReadOnlyList<Research> AdditionalRequiredResearch => additionalRequiredResearch;
    public IReadOnlyList<Pair<Resource, ExpantaNum>> OneTimeResourceCosts =>
        ResourceAmountDefinitionList.ToPairs(oneTimeResourceCosts, ref oneTimeResourceCostsCache);
    public IReadOnlyList<Pair<Resource, ExpantaNum>> ContinuousResourceCosts =>
        ResourceAmountDefinitionList.ToPairs(continuousResourceCosts, ref continuousResourceCostsCache);
    public ExpantaNum BaseDurationSeconds =>
        string.IsNullOrWhiteSpace(baseDurationSeconds) ? ExpantaNum.Zero : baseDurationSeconds;
    public ExpantaNum StablePostureMultiplier =>
        string.IsNullOrWhiteSpace(stablePostureMultiplier) ? ExpantaNum.One : stablePostureMultiplier;
    public ExpantaNum RushPostureMultiplier =>
        string.IsNullOrWhiteSpace(rushPostureMultiplier) ? ExpantaNum.One : rushPostureMultiplier;
    public ExpantaNum FoodConsumptionRate =>
        string.IsNullOrWhiteSpace(foodConsumptionRate) ? ExpantaNum.Zero : foodConsumptionRate;
    public ExpantaNum PowerConsumptionRate =>
        string.IsNullOrWhiteSpace(powerConsumptionRate) ? ExpantaNum.Zero : powerConsumptionRate;
    public ExpantaNum LogisticsConsumptionRate =>
        string.IsNullOrWhiteSpace(logisticsConsumptionRate) ? ExpantaNum.Zero : logisticsConsumptionRate;
}
