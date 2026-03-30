using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static ResourceDisplayer;
using static ResourceDisplayerSet;

public class ResourceManager : Singleton<ResourceManager>
{

    const string SAVE_FILE = "ResourceDatas.json";

    [HideInInspector] public BigNumber GlobalEfficiencyFactor = 1;

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
        DisplayerSets.Add("WoodSet", WoodSet);
        DisplayerSets.Add("OreSet", OreSet);
        DisplayerSets.Add("MineralSet", MineralSet);
        DisplayerSets.Add("IngotSet", IngotSet);
        DisplayerSets.Add("UltraTechSet", UltraTechSet);
    }
    private void Start()
    {
        StartCoroutine(UpdateUI());
    }
    public void AddResource(Resource resource)
    {
        ResourceDisplayerSet set = DisplayerSets[resource.DisplayerSet.ToString()];

        if (set.Displayers.ContainsKey(resource))
            return;

        ResourceDisplayer Displayer = Instantiate(ResourceDisplayerPrefab).GetComponent<ResourceDisplayer>();
        Displayer.Resource = resource;
        Displayer.ResourceAmount = 0;
        Displayer.GenerateRate = 0;
        Displayer.ConsumeRate = 0;
        Displayer.Efficiency = 1;
        Displayer.transform.SetParent(set.Content.transform);

        set.Displayers.Add(resource, Displayer);
    }
    public ResourceDisplayer FindResourceDisplayer(Resource resource) => DisplayerSets[resource.DisplayerSet.ToString()].Displayers[resource];
    IEnumerator UpdateUI()
    {
        while (true)
        {
            foreach (var (_, set) in DisplayerSets)
                set.gameObject.SetActive(set.Displayers.Count != 0);
            yield return new WaitForSecondsRealtime(1f);
        }
    }







    public override void Save()
    {
        var path = Path.Combine(Application.persistentDataPath, SAVE_FILE);
        var SaveData = new SaveData
        {
            ResourceDisplayerSetData = new List<ResourceDisplayerSetData>(),
            GlobalEfficiencyFactor = GlobalEfficiencyFactor
        };

        foreach (var (key, displayerSet) in DisplayerSets)
        {
            var SetData = new ResourceDisplayerSetData();
            SetData.SetName = key;
            SetData.ResourceDisplayerData = new List<ResourceDisplayerData>();
            SetData.Closed = displayerSet.Closed;

            foreach (var (r, displayer) in displayerSet.Displayers)
                SetData.ResourceDisplayerData.Add(new ResourceDisplayerData
                {
                    ResourceName = r.name,
                    GenerateRate = displayer.GenerateRate,
                    ConsumeRate = displayer.ConsumeRate,
                    ResourceAmount = displayer.ResourceAmount,
                    Efficiency = displayer.Efficiency,
                });
            SaveData.ResourceDisplayerSetData.Add(SetData);
        }
        string json = JsonUtility.ToJson(SaveData, true);

        File.WriteAllText(path, json);
        Debug.Log($"Saved resource data to: {path}");
    }
    public override void Load()
    {
        var path = Path.Combine(Application.persistentDataPath, SAVE_FILE);
        if (!File.Exists(path))
            Save();
        var json = File.ReadAllText(path);
        LoadFromJson(json);
    }
    private void LoadFromJson(string json)
    {
        var SaveData = JsonUtility.FromJson<SaveData>(json);

        foreach (var Setdata in SaveData.ResourceDisplayerSetData)
        {
            var set = DisplayerSets[Setdata.SetName];
            set.Closed = Setdata.Closed;
            foreach (var data in Setdata.ResourceDisplayerData)
            {
                Resource resource = ResourceFinder.GetFromString(data.ResourceName);

                AddResource(resource);

                var displayer = set.Displayers[resource];
                displayer.GenerateRate = data.GenerateRate;
                displayer.ConsumeRate = data.ConsumeRate;
                displayer.ResourceAmount = data.ResourceAmount;
                displayer.Efficiency = data.Efficiency;
            }
        }
        GlobalEfficiencyFactor = SaveData.GlobalEfficiencyFactor;
    }


    [Serializable]
    private class SaveData
    {
        public List<ResourceDisplayerSetData> ResourceDisplayerSetData;
        public BigNumber GlobalEfficiencyFactor = 1;
    }
}
