using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "创建研究", menuName = "数据/研究", order = 0)]
public class Research : GameDefinition
{
    public string Label;
    public string Description;
    public string BaseCost;
    [SerializeField] private List<Research> prerequisites = new();
    public IReadOnlyList<Research> Prerequisites => prerequisites;
    [SerializeField]
    private List<ResourceAmountDefinition> resourceRequirements = new();
    [SerializeField]
    private List<ResearchEffectDefinition> effects = new();

    public IReadOnlyList<Pair<Resource, ExpantaNum>> ResourceRequirements => ResourceAmountDefinitionList.ToPairs(resourceRequirements);
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
    public void SetResourceRequirementsForEditor(
        List<Pair<Resource, ExpantaNum>> values) =>
        resourceRequirements = ResourceAmountDefinitionList.FromPairs(values);

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
