using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : Singleton<ResourceManager>
{
    [SerializeField] private Transform Content;
    public ResourceFinder ResourceFinder;
    public Dictionary<Resource, ResourceDisplayer> Displayers = new Dictionary<Resource, ResourceDisplayer>();
    [Header("´óÀà")]
    public ResourceDisplayerSet WoodSet;
    public ResourceDisplayerSet OreSet;
    public ResourceDisplayerSet MineralSet;
    public ResourceDisplayerSet IngotSet;
    public ResourceDisplayerSet UltraTechSet;

    public void AddResource(ResourceDisplayerSet set, Resource resource) => set.AddResource(resource);
}
