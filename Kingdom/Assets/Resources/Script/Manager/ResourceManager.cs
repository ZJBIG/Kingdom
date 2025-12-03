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
    [Header("¥Û¿‡")]
    public ResourceDisplayerSet WoodSet;
    public ResourceDisplayerSet OreSet;
    public ResourceDisplayerSet MineralSet;
    public ResourceDisplayerSet IngotSet;
    public ResourceDisplayerSet UltraTechSet;
    private void Start()
    {
        StartCoroutine(ResourceUpdate());
    }
    private IEnumerator ResourceUpdate()
    {
        while (true)
        {
            List<Resource> keys = new(ResourceAmount.Keys);
            foreach (var r in keys)
                ResourceAmount[r] += ResourceGrowthRate[r] / 2;
            yield return new WaitForSeconds(0.5f);
        }
    }
}
