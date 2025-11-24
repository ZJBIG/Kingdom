using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResourceManager : Singleton<ResourceManager>
{
    public Transform ResourceViewer;
    public GameObject ResourceDisplayerSetPrefab;
    public List<Resource> Resources;
    public Dictionary<Resource, BigNumber> ResourceAmount = new Dictionary<Resource, BigNumber>();
    private Transform Content;
    private void Start()
    {
        Content = ResourceViewer.GetChild(0).GetChild(0);
        foreach (Resource r in Resources)
        {
            ResourceDisplayerSet Displayer = Instantiate(ResourceDisplayerSetPrefab).GetComponent<ResourceDisplayerSet>();
            Displayer.transform.SetParent(Content.transform);
        }
    }
    protected override void Tick()
    {
        base.Tick();
    }
}
