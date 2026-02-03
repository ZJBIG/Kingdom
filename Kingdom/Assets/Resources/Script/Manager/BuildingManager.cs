using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static BuildingDisplayer;

public class BuildingManager : Singleton<BuildingManager>
{

    const string SAVE_FILE = "BuildingDatas.json";

    [SerializeField] private Transform Content;
    [SerializeField] private GameObject BuildingDisplayerPrefab;

    public Dictionary<Building, Pair<BuildingDisplayer, bool>> Displayers = new();

    public BuildingFinder BuildingFinder;
    [HideInInspector] public BigNumber BuildEffort;

    public IEnumerator AutoBuild()
    {
        while (true)
        {
            int count = Displayers.Count(x => x.Value.first.AutoBuild);
            if (count == 0)
            {
                yield return new WaitForSecondsRealtime(1f);
                continue;
            }
            BigNumber perEffort = BuildEffort / count;
            foreach (var (_, pair) in Displayers)
            {
                Building building = pair.first.Building;
                BigNumber add = BigNumber.Floor(perEffort / building.BuildDifficulty);
                if (add == 0)
                    Debug.Log($"Too Hard to auto-build{building.Label}");
                else
                    pair.first.BuildingAmount += add;
            }
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
    public void AddBuilding(Building building)
    {
        if (Displayers.ContainsKey(building))
            throw new System.Exception($"{building.name} has already added");

        BuildingDisplayer Displayer = Instantiate(BuildingDisplayerPrefab).GetComponent<BuildingDisplayer>();
        Displayer.Building = building;
        Displayer.BuildingAmount = 0;
        Displayer.Efficiency = 1;
        Displayer.transform.SetParent(Content.transform);
        Displayers.Add(building, new Pair<BuildingDisplayer, bool>(Displayer, false));

        foreach (var pair in building.ResourceRequirement)
            ResourceManager.Instance.AddResource(pair.first);
        foreach (var pair in building.ResourceGenerateRate)
            ResourceManager.Instance.AddResource(pair.first);
    }






    public override void Save()
    {
        var path = Path.Combine(Application.persistentDataPath, SAVE_FILE);
        var SaveData = new SaveData
        {
            BuildingDisplayerData = new List<BuildingDisplayerData>(),
        };

        foreach (var (_, displayerPair) in Displayers)
        {
            var displayer = displayerPair.first;
            SaveData.BuildingDisplayerData.Add(new BuildingDisplayerData
            {
                BuildingName = displayer.Building?.name,
                AutoBuild = displayer.AutoBuild,
                BuildingAmount = displayer.BuildingAmount,
                Efficiency = displayer.Efficiency
            });
        }

        string json = JsonUtility.ToJson(SaveData, true);

        File.WriteAllText(path, json);
        Debug.Log($"Saved building data to: {path}");
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

        foreach (var data in SaveData.BuildingDisplayerData)
        {
            Building building = BuildingFinder.GetFromString(data.BuildingName);

            AddBuilding(building);

            var displayerPair = Displayers[building];
            var displayer = displayerPair.first;
            displayer.BuildingAmount = data.BuildingAmount;
            displayer.AutoBuild = data.AutoBuild;
            displayerPair.second = data.AutoBuild;
            displayer.Efficiency = data.Efficiency;
        }
    }


    [Serializable]
    private class SaveData
    {
        public List<BuildingDisplayerData> BuildingDisplayerData;
    }
}
