using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Create", menuName = "Data/Research", order = 0)]
public class Research : GameDefinition
{
    public string Label;
    public string Description;
    public string BaseCost;
    [SerializeField] private List<Research> prerequisites = new();
    public IReadOnlyList<Research> Prerequisites => prerequisites;
    [SerializeField]
    private List<Pair<Resource, ExpantaNum>> resourceRequirements = new();
    [SerializeField]
    private List<ResearchEffectDefinition> effects = new();

    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceRequirements => resourceRequirements;
    public IReadOnlyList<ResearchEffectDefinition> Effects => effects;

    public List<Building> BuildingUnlock;
    public TechLevel TechLevel;
    public bool AdvancesTechLevel;
    [Header("TabPosition")]
    public float x;
    public float y;

#if UNITY_EDITOR
    public void SetEffectsForEditor(List<ResearchEffectDefinition> values)
    {
        effects = values ?? new List<ResearchEffectDefinition>();
    }
#endif

#if UNITY_EDITOR
    public void SetPrerequisitesForEditor(List<Research> values)
    {
        prerequisites = values ?? new List<Research>();
    }
#endif

}
