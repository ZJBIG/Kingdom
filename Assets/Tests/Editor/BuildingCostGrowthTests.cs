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

    [TestCase(1d)]
    [TestCase(1.14d)]
    [TestCase(1.15d)]
    [TestCase(1.22d)]
    public void MaxAffordableGeometricSeries_RemainsFiniteAtHugeCurrency(double ratio)
    {
        ExpantaNum currency = new ExpantaNum("1e999");
        ExpantaNum maximum = currency.MaxAffordableGeometricSeries(1, ratio, 0);

        Assert.That(maximum.IsFinite, Is.True);
        Assert.That(maximum.IsInteger(), Is.True);
        Assert.That(
            new ExpantaNum(1).GeometricSeriesCost(ratio, 0, maximum),
            Is.LessThanOrEqualTo(currency));
        if (ratio > 1d)
        {
            Assert.That(
                new ExpantaNum(1).GeometricSeriesCost(ratio, 0, maximum + ExpantaNum.One),
                Is.GreaterThan(currency));
        }
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
    public void AllBuildings_DoNotSerializeTheSameResourceAsProductionAndConsumption()
    {
        Assert.DoesNotThrow(() => BuildingManager.ValidateMergedResourceFlows(
            DataBase<Building>.All));
    }

    [Test]
    public void BuildingDefinition_RejectsUnmergedOpposingResourceFlows()
    {
        Resource resource = ScriptableObject.CreateInstance<Resource>();
        Building building = CreateBuilding("UnmergedResourceFlow");
        createdObjects.Add(resource);
        building.ConfigureEconomyForEditor(
            new ExpantaNum(1.1d), ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            new List<Pair<Resource, ExpantaNum>>(),
            new List<Pair<Resource, ExpantaNum>> { new(resource, ExpantaNum.One) },
            new List<Pair<Resource, ExpantaNum>> { new(resource, ExpantaNum.One) });

        Assert.Throws<System.InvalidOperationException>(() =>
            BuildingManager.ValidateMergedResourceFlows(new[] { building }));
    }

    [Test]
    public void BuildingChainIndex_PreservesAllSharedTargetPredecessors()
    {
        CreateManager<GameManager>("SharedTarget-GameManager");
        Building branchA = CreateBuilding("IndexedBranchA");
        Building branchB = CreateBuilding("IndexedBranchB");
        Building sharedTarget = CreateBuilding("IndexedSharedTarget");
        branchA.SetUpgradeToForEditor(sharedTarget);
        branchB.SetUpgradeToForEditor(sharedTarget);

        BuildingManager manager = CreateManager<BuildingManager>("SharedTarget-ChainManager");
        MethodInfo rebuild = typeof(BuildingManager).GetMethod(
            "RebuildBuildingChainIndex",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(rebuild, Is.Not.Null);
        rebuild.Invoke(manager, new object[] {
            new List<Building> { branchA, branchB, sharedTarget }
        });

        FieldInfo predecessorsField = typeof(BuildingManager).GetField(
            "chainPredecessors",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(predecessorsField, Is.Not.Null);
        var predecessors = (Dictionary<Building, List<Building>>)predecessorsField.GetValue(manager);
        Assert.That(predecessors[sharedTarget], Has.Count.EqualTo(2));
        Assert.That(manager.TryGetUnlockedUpgradeTarget(branchA, out Building target), Is.False);
        Assert.That(target, Is.Null);
        manager.EnsureBuilding(branchA).SetAmountForEditor(ExpantaNum.One);
        manager.EnsureBuilding(branchB).SetAmountForEditor(ExpantaNum.One);
        Assert.That(manager.TryGetUnlockedUpgradeTarget(branchA, out target), Is.True);
        Assert.That(target, Is.SameAs(sharedTarget));
        Assert.That(manager.TryGetUnlockedUpgradeTarget(branchB, out target), Is.True);
        Assert.That(target, Is.SameAs(sharedTarget));
    }

    [Test]
    public void BuildingChain_FourZeroTiersDisplaysOnlyHighestAvailableTier()
    {
        CreateManager<GameManager>("LinearChain-GameManager");
        BuildingManager manager = CreateManager<BuildingManager>("LinearChain-Manager");
        Building a = CreateBuilding("LinearA");
        Building b = CreateBuilding("LinearB");
        Building c = CreateBuilding("LinearC");
        Building d = CreateBuilding("LinearD");
        a.SetUpgradeToForEditor(b);
        b.SetUpgradeToForEditor(c);
        c.SetUpgradeToForEditor(d);

        MethodInfo rebuild = typeof(BuildingManager).GetMethod(
            "RebuildBuildingChainIndex",
            BindingFlags.Instance | BindingFlags.NonPublic);
        rebuild.Invoke(manager, new object[] { new List<Building> { a, b, c, d } });
        manager.EnsureBuilding(a);
        manager.EnsureBuilding(b);
        manager.EnsureBuilding(c);
        manager.EnsureBuilding(d);
        manager.RefreshBuildingChainAvailability();

        Assert.That(manager.ShouldDisplay(a), Is.False);
        Assert.That(manager.ShouldDisplay(b), Is.False);
        Assert.That(manager.ShouldDisplay(c), Is.False);
        Assert.That(manager.ShouldDisplay(d), Is.True);
    }

    [Test]
    public void BuildingChain_KeepsLowerSourceAndShowsHighestZeroTier()
    {
        CreateManager<GameManager>("HighestZero-GameManager");
        BuildingManager manager = CreateManager<BuildingManager>("HighestZero-Manager");
        Building x1 = CreateBuilding("HighestZeroX1");
        Building x2 = CreateBuilding("HighestZeroX2");
        Building x3 = CreateBuilding("HighestZeroX3");
        Building x4 = CreateBuilding("HighestZeroX4");
        x1.SetUpgradeToForEditor(x2);
        x2.SetUpgradeToForEditor(x3);
        x3.SetUpgradeToForEditor(x4);
        x4.TechLevel = TechLevel.Spacer;

        MethodInfo rebuild = typeof(BuildingManager).GetMethod(
            "RebuildBuildingChainIndex",
            BindingFlags.Instance | BindingFlags.NonPublic);
        rebuild.Invoke(manager, new object[] { new List<Building> { x1, x2, x3, x4 } });
        manager.EnsureBuilding(x1).SetAmountForEditor(ExpantaNum.One);
        manager.EnsureBuilding(x2);
        manager.EnsureBuilding(x3);
        manager.EnsureBuilding(x4);
        manager.RefreshBuildingChainAvailability();

        Assert.That(manager.ShouldDisplay(x1), Is.True);
        Assert.That(manager.ShouldDisplay(x2), Is.False);
        Assert.That(manager.ShouldDisplay(x3), Is.True);
        Assert.That(manager.ShouldDisplay(x4), Is.False);
        Assert.That(manager.TryGetUnlockedUpgradeTarget(x1, out Building target), Is.True);
        Assert.That(target, Is.SameAs(x3));
        Assert.That(manager.CanConstructNew(x3), Is.True);
    }

    [Test]
    public void BuildingChain_PreservesNonZeroSourcesAndTargetsHighestAvailableTier()
    {
        CreateManager<GameManager>("SkipTier-GameManager");
        BuildingManager manager = CreateManager<BuildingManager>("SkipTier-Manager");
        Building x1 = CreateBuilding("SkipX1");
        Building x2 = CreateBuilding("SkipX2");
        Building x3 = CreateBuilding("SkipX3");
        Building x4 = CreateBuilding("SkipX4");
        Building x5 = CreateBuilding("SkipX5");
        x1.SetUpgradeToForEditor(x2);
        x2.SetUpgradeToForEditor(x3);
        x3.SetUpgradeToForEditor(x4);
        x4.SetUpgradeToForEditor(x5);
        x4.TechLevel = TechLevel.Animal;
        x5.TechLevel = TechLevel.Spacer;

        MethodInfo rebuild = typeof(BuildingManager).GetMethod(
            "RebuildBuildingChainIndex",
            BindingFlags.Instance | BindingFlags.NonPublic);
        rebuild.Invoke(manager, new object[] {
            new List<Building> { x1, x2, x3, x4, x5 }
        });
        manager.EnsureBuilding(x1).SetAmountForEditor(ExpantaNum.One);
        manager.EnsureBuilding(x2);
        manager.EnsureBuilding(x3).SetAmountForEditor(ExpantaNum.One);
        manager.EnsureBuilding(x4);
        manager.EnsureBuilding(x5);
        manager.RefreshBuildingChainAvailability();

        Assert.That(manager.States.ContainsKey(x1), Is.True);
        Assert.That(manager.States.ContainsKey(x2), Is.False);
        Assert.That(manager.States.ContainsKey(x3), Is.True);
        Assert.That(manager.GetState(x1).Amount, Is.EqualTo(ExpantaNum.One));
        Assert.That(manager.GetState(x3).Amount, Is.EqualTo(ExpantaNum.One));
        Assert.That(manager.ShouldDisplay(x1), Is.True);
        Assert.That(manager.ShouldDisplay(x2), Is.False);
        Assert.That(manager.ShouldDisplay(x3), Is.True);
        Assert.That(manager.ShouldDisplay(x4), Is.True);
        Assert.That(manager.ShouldDisplay(x5), Is.False);
        Assert.That(manager.TryGetUnlockedUpgradeTarget(x1, out Building x1Target), Is.True);
        Assert.That(x1Target, Is.SameAs(x4));
        Assert.That(manager.TryGetUnlockedUpgradeTarget(x3, out Building x3Target), Is.True);
        Assert.That(x3Target, Is.SameAs(x4));
        Assert.That(manager.CanConstructNew(x4), Is.True);
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
        ExpantaNum amountBeforeBuild = resourceManager.GetAmount(wood);

        Assert.That(farm.CostGrowth, Is.EqualTo(new ExpantaNum(1.14d)));
        Assert.That(buildingManager.TryBuild(farm, ExpantaNum.One, out BuildFailure firstFailure), Is.True);
        Assert.That(firstFailure, Is.EqualTo(BuildFailure.None));
        Assert.That(
            buildingManager.TryBuild(farm, ExpantaNum.One, out BuildFailure secondFailure),
            Is.True);
        Assert.That(secondFailure, Is.EqualTo(BuildFailure.None));
        ExpantaNum amountAfterBuild = resourceManager.GetAmount(wood);
        Assert.That(amountAfterBuild, Is.LessThan(amountBeforeBuild));

        Assert.That(buildingManager.TryDeconstruct(farm, ExpantaNum.One, out BuildFailure deconstructFailure), Is.True);
        Assert.That(deconstructFailure, Is.EqualTo(BuildFailure.None));
        Assert.That(resourceManager.GetAmount(wood), Is.GreaterThan(amountAfterBuild));
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
