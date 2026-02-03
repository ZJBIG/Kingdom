using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Create", menuName = "Data/Building", order = 0)]
public class Building : ScriptableObject
{
    public string Label;
    public string Description;
    public string BuildDifficulty;
    public string SpaceOccupy;
    public string ProductivityRequirement;
    public TechLevel TechLevel;
    [Header("¹¦ÄÜ")]
    public string FoodConsumeRate;
    public List<Pair<Resource, string>> ResourceRequirement;
    public List<Pair<Resource, string>> ResourceGenerateRate;
    public List<Pair<Resource, string>> ResourceConsumeRate;
    [Range(0f, 1f)] public float DeconstructReturnPercentage;
}