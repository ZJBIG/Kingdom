using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class AutoBuildTechGateTests
{
    private readonly List<Object> createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(createdObjects[i]);
        createdObjects.Clear();
    }

    [Test]
    public void AutoBuild_IsLockedInAnimalEraAndUnlocksInNeolithic()
    {
        CreateManager<GameManager>("AutoBuild-GameManager");
        BuildingManager buildingManager = CreateManager<BuildingManager>("AutoBuild-BuildingManager");
        Building farm = DataBase<Building>.Find("Farm");

        Assert.That(buildingManager.SetAutoBuild(farm, true), Is.False);
        Assert.That(buildingManager.GetState(farm).AutoBuild, Is.False);

        AdvanceTechLevel(TechLevel.Neolithic);

        Assert.That(buildingManager.SetAutoBuild(farm, true), Is.True);
        Assert.That(buildingManager.GetState(farm).AutoBuild, Is.True);
    }

    [Test]
    public void AutoBuildWorkRequired_GrowsWithOwnedBuildings()
    {
        CreateManager<GameManager>("AutoBuild-Growth-GameManager");
        BuildingManager buildingManager = CreateManager<BuildingManager>("AutoBuild-Growth-BuildingManager");
        Building farm = DataBase<Building>.Find("Farm");
        AdvanceTechLevel(TechLevel.Neolithic);

        Assert.That(
            buildingManager.GetAutoBuildWorkRequired(farm),
            Is.EqualTo(farm.AutoBuildWorkRequired));

        BuildingState state = buildingManager.EnsureBuilding(farm);
        MethodInfo setAmount = typeof(BuildingState).GetMethod(
            "SetAmount",
            BindingFlags.Instance | BindingFlags.NonPublic);
        setAmount.Invoke(state, new object[] { ExpantaNum.One });

        Assert.That(
            buildingManager.GetAutoBuildWorkRequired(farm).ToDouble(),
            Is.EqualTo((farm.AutoBuildWorkRequired * farm.CostGrowth).ToDouble()).Within(0.000001d));
    }

    private void AdvanceTechLevel(TechLevel level)
    {
        MethodInfo method = typeof(GameState).GetMethod(
            "AdvanceTechLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(GameManager.Instance.State, new object[] { level });
    }

    private T CreateManager<T>(string name) where T : Component
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }
}
