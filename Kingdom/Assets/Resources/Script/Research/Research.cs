using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Create", menuName = "Data/Research", order = 0)]
public class Research : ScriptableObject
{
    public string Label;
    public string Description;
    public List<Pair<Resource,BigNumber>> ResourceRequire;
    public BigNumber ResearchBaseCost;
    public GameManager.TechnologyLevel TechLevel;
    [Header("TabPosition")]
    public float x, y;
}