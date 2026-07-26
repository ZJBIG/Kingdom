using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class AutoBuildSimulationTests
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
    public void AutoBuildDisabledThenEnabled_ChangesOnlyWhenEnabled()
    {
        GameObject managers = new GameObject("AutoBuild-Simulation-Managers");
        createdObjects.Add(managers);
        GameManager gameManager = managers.AddComponent<GameManager>();
        ResourceManager resourceManager = managers.AddComponent<ResourceManager>();
        BuildingManager buildingManager = managers.AddComponent<BuildingManager>();
        ResearchManager researchManager = managers.AddComponent<ResearchManager>();

        AdvanceTechLevel(gameManager.State, TechLevel.Neolithic);
        CompleteResearch(researchManager, "Agriculture");
        RestorePopulation(gameManager.State, new ExpantaNum(20));
        Building farm = DataBase<Building>.Find("Farm");
        foreach (Pair<Resource, ExpantaNum> requirement in farm.ResourceRequirements)
            resourceManager.SetAmount(requirement.First, new ExpantaNum(100000));

        BuildingState state = buildingManager.EnsureBuilding(farm);
        Assert.That(buildingManager.SetAutoBuild(farm, false), Is.True);
        buildingManager.AdvanceAutoBuild(1d);
        Assert.That(state.Amount, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.AutoBuildProgress, Is.EqualTo(ExpantaNum.Zero));

        Assert.That(buildingManager.SetAutoBuild(farm, true), Is.True);
        buildingManager.AdvanceAutoBuild(1d);
        Assert.That(
            state.Amount > ExpantaNum.Zero || state.AutoBuildProgress > ExpantaNum.Zero,
            Is.True);
    }

    private static void AdvanceTechLevel(GameState state, TechLevel level)
    {
        MethodInfo method = typeof(GameState).GetMethod(
            "AdvanceTechLevel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(state, new object[] { level });
    }

    private static void RestorePopulation(GameState state, ExpantaNum population)
    {
        MethodInfo method = typeof(GameState).GetMethod(
            "RestorePopulation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(
            state,
            new object[]
            {
                population,
                population,
                ExpantaNum.Zero,
                ExpantaNum.Zero,
                new ExpantaNum(PopulationState.DefaultFoodPerPerson)
            });
    }

    private static void CompleteResearch(ResearchManager manager, string id)
    {
        EnsureInitialized(manager);
        ResearchState state = manager.GetState(DataBase<Research>.Find(id));
        MethodInfo method = typeof(ResearchState).GetMethod(
            "SetStatus",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(state, new object[] { ResearchStatus.Completed });
    }

    private static void EnsureInitialized(ResearchManager manager)
    {
        if (manager.States.Count > 0)
            return;
        MethodInfo method = typeof(ResearchManager).GetMethod(
            "Initialize",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(manager, null);
    }
}
