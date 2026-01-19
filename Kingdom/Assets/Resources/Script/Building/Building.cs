using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Create", menuName = "Data/Building", order = 0)]
public class Building : ScriptableObject
{
    public string Label;
    public string Description;
    public string BuildDifficulty;
    public string SpaceOccupy;
    public string ProductivityRequirment;
    public List<Research> ResearchRequisites;
    public GameManager.TechnologyLevel TechLevel;
    [Header("¹¦ÄÜ")]
    public string PopulationRequirement;
    public string FoodRequirement;
    public List<Pair<Resource, string>> BuildCost;
    public List<Pair<Resource, string>> ResourceGeneratePerSecond;
    public List<Pair<Resource, string>> ResourceConsumptionPerSecond;
    [Range(0f, 1f)] public float DeconstructReturnPercentage;
}