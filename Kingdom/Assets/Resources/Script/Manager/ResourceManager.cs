using System.Collections;
using System.Collections.Generic;
using System.Resources;
using UnityEngine;

public class ResourceManager : Singleton<ResourceManager>
{
    [SerializeField] private Transform Content;
    public Dictionary<Resource, BigNumber> ResourceAmount = new Dictionary<Resource, BigNumber>();
    public Dictionary<Resource, BigNumber> ResourceGrowthRate = new Dictionary<Resource, BigNumber>();
    public ResourceFinder ResourceFinder;
    [Header("´óÀà")]
    public ResourceDisplayerSet WoodSet;
    public ResourceDisplayerSet OreSet;
    public ResourceDisplayerSet MineralSet;
    public ResourceDisplayerSet IngotSet;
    public ResourceDisplayerSet UltraTechSet;
    private float Timer;
    protected override void Tick()
    {
        base.Tick();
        Timer += Time.deltaTime;
        if (Timer >= 1)
        {
            Timer = 0;
            ResourceUpdate();
        }
    }
    private void ResourceUpdate()
    {
        List<Resource> keys = new(ResourceAmount.Keys);
        foreach (var r in keys)
            ResourceAmount[r] += ResourceGrowthRate[r];
    }
}
