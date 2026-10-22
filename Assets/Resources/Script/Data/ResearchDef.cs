using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "创建研究", menuName = "数据/研究", order = 0)]
public class ResearchDef : GameDefinition
{
    public string Label;
    [TextArea]public string Description;
    public string BaseCost;
    [SerializeField] private List<ResearchDef> prerequisites = new();
    public IReadOnlyList<ResearchDef> Prerequisites => prerequisites;
    [SerializeField]
    private List<ResourceAmountDefinition> resourceRequirements = new();
    [System.NonSerialized] private List<Pair<ResourceDef, ExpantaNum>> resourceRequirementsCache;
    [SerializeField]
    private List<ResearchEffectDefinition> effects = new();

    public IReadOnlyList<Pair<ResourceDef, ExpantaNum>> ResourceRequirements => ResourceAmountDefinitionList.ToPairs(resourceRequirements, ref resourceRequirementsCache);
    public IReadOnlyList<ResearchEffectDefinition> Effects => effects;
    public bool HasPositiveResourceRequirement
    {
        get
        {
            for (int i = 0; i < resourceRequirements.Count; i++)
                if (resourceRequirements[i].Amount > ExpantaNum.Zero)
                    return true;
            return false;
        }
    }

    public TechLevel TechLevel;
    public bool AdvancesTechLevel;
#if UNITY_EDITOR
    private void OnValidate()
    {
        resourceRequirementsCache = null;
    }

    public void SetResourceRequirementsForEditor(
        List<Pair<ResourceDef, ExpantaNum>> values)
    {
        resourceRequirements = ResourceAmountDefinitionList.FromPairs(values);
        resourceRequirementsCache = null;
    }

    public void SetEffectsForEditor(List<ResearchEffectDefinition> values)
    {
        effects = values ?? new List<ResearchEffectDefinition>();
    }
#endif

#if UNITY_EDITOR
    public void SetPrerequisitesForEditor(List<ResearchDef> values)
    {
        prerequisites = values ?? new List<ResearchDef>();
    }
#endif

}
