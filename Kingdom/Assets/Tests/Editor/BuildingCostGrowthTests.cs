using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BuildingCostGrowthTests
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
    public void GeometricSeriesCost_UsesOwnedOffset()
    {
        ExpantaNum total = new ExpantaNum(10).GeometricSeriesCost(2, 3, 2);

        Assert.That(total, Is.EqualTo(new ExpantaNum(240)));
    }

    [Test]
    public void MaxAffordableGeometricSeries_ReturnsExactBoundary()
    {
        Assert.That(
            new ExpantaNum(240).MaxAffordableGeometricSeries(10, 2, 3),
            Is.EqualTo(new ExpantaNum(2)));
        Assert.That(
            new ExpantaNum(239).MaxAffordableGeometricSeries(10, 2, 3),
            Is.EqualTo(new ExpantaNum(1)));
    }

    [Test]
    public void BuildingChain_AllowsMultipleBranchesToShareOneUpgradeTarget()
    {
        Building branchA = CreateBuilding("BranchA");
        Building branchB = CreateBuilding("BranchB");
        Building sharedTarget = CreateBuilding("SharedTarget");
        branchA.SetUpgradeToForEditor(sharedTarget);
        branchB.SetUpgradeToForEditor(sharedTarget);

        Assert.DoesNotThrow(() => BuildingManager.ValidateBuildingChains(
            new List<Building> { branchA, branchB, sharedTarget }));
    }

    [Test]
    public void BuildingManager_ChargesGrowthAndRefundsLastBatch()
    {
        CreateManager<GameManager>("Growth-GameManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("Growth-ResourceManager");
        BuildingManager buildingManager = CreateManager<BuildingManager>("Growth-BuildingManager");
        ResearchManager researchManager = CreateManager<ResearchManager>("Growth-ResearchManager");
        RestorePopulation(GameManager.Instance.State, new ExpantaNum(20));
        CompleteResearch(researchManager, "Agriculture");

        Resource wood = DataBase<Resource>.Find("WoodLog");
        Building farm = DataBase<Building>.Find("Farm");
        resourceManager.AddAmount(wood, new ExpantaNum(1000));

        Assert.That(farm.CostGrowth, Is.EqualTo(new ExpantaNum(1.14d)));
        Assert.That(buildingManager.TryBuild(farm, ExpantaNum.One, out BuildFailure firstFailure), Is.True);
        Assert.That(firstFailure, Is.EqualTo(BuildFailure.None));
        Assert.That(
            buildingManager.TryBuild(farm, ExpantaNum.One, out BuildFailure secondFailure),
            Is.True);
        Assert.That(secondFailure, Is.EqualTo(BuildFailure.None));
        Assert.That(resourceManager.GetAmount(wood).ToDouble(), Is.EqualTo(893d).Within(0.000001d));

        Assert.That(buildingManager.TryDeconstruct(farm, ExpantaNum.One, out BuildFailure deconstructFailure), Is.True);
        Assert.That(deconstructFailure, Is.EqualTo(BuildFailure.None));
        Assert.That(resourceManager.GetAmount(wood).ToDouble(), Is.EqualTo(895.85d).Within(0.000001d));
    }

    [Test]
    public void AgricultureCompletion_MakesFarmAvailableToBuildingMenu()
    {
        CreateManager<GameManager>("FarmVisibility-GameManager");
        BuildingManager buildingManager =
            CreateManager<BuildingManager>("FarmVisibility-BuildingManager");
        ResearchManager researchManager =
            CreateManager<ResearchManager>("FarmVisibility-ResearchManager");
        Building farm = DataBase<Building>.Find("Farm");

        Assert.That(buildingManager.ShouldDisplay(farm), Is.False);
        CompleteResearch(researchManager, "Agriculture");

        Assert.That(
            buildingManager.ShouldDisplay(farm),
            Is.True,
            "Farm must become visible as soon as Agriculture is completed.");
    }

    private T CreateManager<T>(string name) where T : Component
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    private Building CreateBuilding(string id)
    {
        Building building = ScriptableObject.CreateInstance<Building>();
        building.SetIdForEditor(id);
        createdObjects.Add(building);
        return building;
    }

    private static void RestorePopulation(GameState state, ExpantaNum population)
    {
        MethodInfo method = typeof(GameState).GetMethod(
            "RestorePopulation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(state, new object[] { population });
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
