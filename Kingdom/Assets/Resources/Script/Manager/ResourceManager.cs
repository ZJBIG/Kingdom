using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResourceManager : Singleton<ResourceManager>
{
    public Text DisplayLabel;
    public Text DisplayDesc;
    public List<Resource> Resources;
    protected override void Initialize()
    {
        base.Initialize();
    }
    protected override void Tick()
    {
        base.Tick();
    }
    public void DisplayResource(Resource resource)
    {
        DisplayLabel.text = resource.Label;
        DisplayDesc.text = resource.Description;
    }
}
