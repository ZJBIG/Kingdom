using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Create", menuName = "Data/Building", order = 0)]
public class Building : ScriptableObject
{
    public string Label;
    public string Description;
    public BigNumber BuildTime;
    public BigNumber SpaceOccupy;
    public List<Pair<Resource, BigNumber>> ResourceRequire;
    public List<Research> ResearchRequisites;
    [Header("¹¦ÄÜ")]
    public bool AutomaticBuilding;
    public BigNumber PopulationCapacity;
    public List<Pair<Resource, BigNumber>> ResourceGenerate;
}