using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : Singleton<ResourceManager>
{
    [SerializeField] private Transform Content;
    [SerializeField] private GameObject ResourceDisplayerPrefab;
    public ResourceFinder ResourceFinder;
    public Dictionary<string, ResourceDisplayerSet> DisplayerSets = new Dictionary<string, ResourceDisplayerSet>();
    [Header("¥Û¿‡")]
    public ResourceDisplayerSet WoodSet;
    public ResourceDisplayerSet OreSet;
    public ResourceDisplayerSet MineralSet;
    public ResourceDisplayerSet IngotSet;
    public ResourceDisplayerSet UltraTechSet;

    protected override void Initialize()
    {
        base.Initialize();
        DisplayerSets.Add("WoodSet", WoodSet);
        DisplayerSets.Add("OreSet", OreSet);
        DisplayerSets.Add("MineralSet", MineralSet);
        DisplayerSets.Add("IngotSet", IngotSet);
        DisplayerSets.Add("UltraTechSet", UltraTechSet);
    }

    public void AddResource(Resource resource)
    {
        ResourceDisplayerSet set = DisplayerSets[resource.DisplayerSet.ToString()];

        if (set.Displayers.ContainsKey(resource))
            return;

        ResourceDisplayer Displayer = Instantiate(ResourceDisplayerPrefab).GetComponent<ResourceDisplayer>();
        Displayer.Resource = resource;
        Displayer.ResourceAmount = 0;
        Displayer.ResourceGrowthRate = 0;
        Displayer.transform.SetParent(set.Content.transform);

        set.Displayers.Add(resource, Displayer);
    }
    public ResourceDisplayer FindResourceDisplayer(Resource resource) => DisplayerSets[resource.DisplayerSet.ToString()].Displayers[resource];
}
