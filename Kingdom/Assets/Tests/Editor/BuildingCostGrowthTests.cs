using System.Collections.Generic;
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
    public void BuildingManager_ChargesGrowthAndRefundsLastBatch()
    {
        CreateManager<GameManager>("Growth-GameManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("Growth-ResourceManager");
        BuildingManager buildingManager = CreateManager<BuildingManager>("Growth-BuildingManager");

        Resource wood = DataBase<Resource>.Find("WoodLog");
        Building farm = DataBase<Building>.Find("Farm");
        resourceManager.AddAmount(wood, new ExpantaNum(1000));

        Assert.That(farm.CostGrowth, Is.EqualTo(new ExpantaNum(1.15d)));
        Assert.That(buildingManager.TryBuild(farm, ExpantaNum.One, out BuildFailure firstFailure), Is.True);
        Assert.That(firstFailure, Is.EqualTo(BuildFailure.None));
        Assert.That(
            buildingManager.TryBuild(farm, ExpantaNum.One, out BuildFailure secondFailure),
            Is.True);
        Assert.That(secondFailure, Is.EqualTo(BuildFailure.None));
        Assert.That(resourceManager.GetAmount(wood).ToDouble(), Is.EqualTo(967.75d).Within(0.000001d));

        Assert.That(buildingManager.TryDeconstruct(farm, ExpantaNum.One, out BuildFailure deconstructFailure), Is.True);
        Assert.That(deconstructFailure, Is.EqualTo(BuildFailure.None));
        Assert.That(resourceManager.GetAmount(wood).ToDouble(), Is.EqualTo(971.2d).Within(0.000001d));
    }

    private T CreateManager<T>(string name) where T : Component
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }
}
