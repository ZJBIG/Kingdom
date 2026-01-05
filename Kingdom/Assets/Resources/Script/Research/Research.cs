using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Create", menuName = "Data/Research", order = 0)]
public class Research : ScriptableObject
{
    public string Label;
    public string Description;
    public string BaseCost;
    public List<Research> Prequisites;
    public List<Pair<Resource, string>> ResourceRequirement;
    public GameManager.TechnologyLevel TechLevel;
    [Header("TabPosition")]
    public float x;
    public float y;
}