using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BuildingManager : Singleton<BuildingManager>
{
    [SerializeField] private Transform Content;
    [SerializeField] private GameObject BuildingDisplayerPrefab;

    public Dictionary<Building, Pair<BuildingDisplayer, bool>> Displayers = new();

    public BuildingFinder BuildingFinder;


    [HideInInspector] public BigNumber BuildEffort;

    public void UpdateResourceGrowthRate()
    {
        foreach (var (_, pair) in Displayers)
        {
            Building building = pair.first.Building;
            foreach (var pair2 in building.ResourceGeneratePerSecond)
                ResourceManager.Instance.FindResourceDisplayer(pair2.first).ResourceGrowthRate = 0;
            foreach (var pair2 in building.ResourceConsumptionPerSecond)
                ResourceManager.Instance.FindResourceDisplayer(pair2.first).ResourceGrowthRate = 0;

            foreach (var pair2 in building.ResourceGeneratePerSecond)
                ResourceManager.Instance.FindResourceDisplayer(pair2.first).ResourceGrowthRate += pair2.second * pair.first.BuildingAmount;
            foreach (var pair2 in building.ResourceConsumptionPerSecond)
                ResourceManager.Instance.FindResourceDisplayer(pair2.first).ResourceGrowthRate += pair2.second * pair.first.BuildingAmount;
            GameManager.Instance.FoodGrowthRate += pair.first.BuildingAmount * building.FoodRequirement;
        }
    }
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
        Displayer.transform.SetParent(Content.transform);
        Displayers.Add(building, new Pair<BuildingDisplayer, bool>(Displayer, false));

        foreach (var pair in building.BuildCost)
            ResourceManager.Instance.AddResource(pair.first);
    }
}
