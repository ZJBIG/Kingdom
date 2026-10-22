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
        BuildingDef branchA = CreateBuilding("BranchA");
        BuildingDef branchB = CreateBuilding("BranchB");
        BuildingDef sharedTarget = CreateBuilding("SharedTarget");
        branchA.SetUpgradeToForEditor(sharedTarget);
        branchB.SetUpgradeToForEditor(sharedTarget);

        Assert.DoesNotThrow(() => BuildingManager.ValidateBuildingChains(
            new List<BuildingDef> { branchA, branchB, sharedTarget }));
    }

    [Test]
    public void AllBuildings_DoNotSerializeTheSameResourceAsProductionAndConsumption()
    {
        Assert.DoesNotThrow(() => BuildingManager.ValidateMergedResourceFlows(
            DataBase<BuildingDef>.All));
    }

    [Test]
    public void BuildingDefinition_RejectsUnmergedOpposingResourceFlows()
    {
        ResourceDef resource = ScriptableObject.CreateInstance<ResourceDef>();
        BuildingDef building = CreateBuilding("UnmergedResourceFlow");
        createdObjects.Add(resource);
        building.ConfigureEconomyForEditor(
            new ExpantaNum(1.1d), ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            new List<Pair<ResourceDef, ExpantaNum>>(),
            new List<Pair<ResourceDef, ExpantaNum>> { new(resource, ExpantaNum.One) },
            new List<Pair<ResourceDef, ExpantaNum>> { new(resource, ExpantaNum.One) });

        Assert.Throws<System.InvalidOperationException>(() =>
            BuildingManager.ValidateMergedResourceFlows(new[] { building }));
    }

    [Test]
    public void BuildingChainIndex_PreservesAllSharedTargetPredecessors()
    {
        CreateManager<GameManager>("SharedTarget-GameManager");
        BuildingDef branchA = CreateBuilding("IndexedBranchA");
        BuildingDef branchB = CreateBuilding("IndexedBranchB");
        BuildingDef sharedTarget = CreateBuilding("IndexedSharedTarget");
        branchA.SetUpgradeToForEditor(sharedTarget);
        branchB.SetUpgradeToForEditor(sharedTarget);

        BuildingManager manager = CreateManager<BuildingManager>("SharedTarget-ChainManager");
        manager.RebuildBuildingChainIndexForEditor(
            new List<BuildingDef> { branchA, branchB, sharedTarget });

        Assert.That(manager.GetChainPredecessorCountForEditor(sharedTarget), Is.EqualTo(2));
        Assert.That(manager.TryGetUnlockedUpgradeTarget(branchA, out BuildingDef target), Is.False);
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
        BuildingDef a = CreateBuilding("LinearA");
        BuildingDef b = CreateBuilding("LinearB");
        BuildingDef c = CreateBuilding("LinearC");
        BuildingDef d = CreateBuilding("LinearD");
        a.SetUpgradeToForEditor(b);
        b.SetUpgradeToForEditor(c);
        c.SetUpgradeToForEditor(d);

        manager.RebuildBuildingChainIndexForEditor(new List<BuildingDef> { a, b, c, d });
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
        BuildingDef x1 = CreateBuilding("HighestZeroX1");
        BuildingDef x2 = CreateBuilding("HighestZeroX2");
        BuildingDef x3 = CreateBuilding("HighestZeroX3");
        BuildingDef x4 = CreateBuilding("HighestZeroX4");
        x1.SetUpgradeToForEditor(x2);
        x2.SetUpgradeToForEditor(x3);
        x3.SetUpgradeToForEditor(x4);
        x4.TechLevel = TechLevel.Spacer;

        manager.RebuildBuildingChainIndexForEditor(new List<BuildingDef> { x1, x2, x3, x4 });
        manager.EnsureBuilding(x1).SetAmountForEditor(ExpantaNum.One);
        manager.EnsureBuilding(x2);
        manager.EnsureBuilding(x3);
        manager.EnsureBuilding(x4);
        manager.RefreshBuildingChainAvailability();

        Assert.That(manager.ShouldDisplay(x1), Is.True);
        Assert.That(manager.ShouldDisplay(x2), Is.False);
        Assert.That(manager.ShouldDisplay(x3), Is.True);
        Assert.That(manager.ShouldDisplay(x4), Is.False);
        Assert.That(manager.TryGetUnlockedUpgradeTarget(x1, out BuildingDef target), Is.True);
        Assert.That(target, Is.SameAs(x3));
        Assert.That(manager.CanConstructNew(x3), Is.True);
    }

    [Test]
    public void BuildingChain_PreservesNonZeroSourcesAndTargetsHighestAvailableTier()
    {
        CreateManager<GameManager>("SkipTier-GameManager");
        BuildingManager manager = CreateManager<BuildingManager>("SkipTier-Manager");
        BuildingDef x1 = CreateBuilding("SkipX1");
        BuildingDef x2 = CreateBuilding("SkipX2");
        BuildingDef x3 = CreateBuilding("SkipX3");
        BuildingDef x4 = CreateBuilding("SkipX4");
        BuildingDef x5 = CreateBuilding("SkipX5");
        x1.SetUpgradeToForEditor(x2);
        x2.SetUpgradeToForEditor(x3);
        x3.SetUpgradeToForEditor(x4);
        x4.SetUpgradeToForEditor(x5);
        x4.TechLevel = TechLevel.Animal;
        x5.TechLevel = TechLevel.Spacer;

        manager.RebuildBuildingChainIndexForEditor(
            new List<BuildingDef> { x1, x2, x3, x4, x5 });
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
        Assert.That(manager.TryGetUnlockedUpgradeTarget(x1, out BuildingDef x1Target), Is.True);
        Assert.That(x1Target, Is.SameAs(x4));
        Assert.That(manager.TryGetUnlockedUpgradeTarget(x3, out BuildingDef x3Target), Is.True);
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
        GameManager.Instance.State.RestorePopulationForEditor(new ExpantaNum(20));
        CompleteResearch(researchManager, "Agriculture");

        ResourceDef wood = DataBase<ResourceDef>.Find("WoodLog");
        BuildingDef farm = DataBase<BuildingDef>.Find("Farm");
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
        BuildingDef farm = DataBase<BuildingDef>.Find("Farm");

        Assert.That(buildingManager.ShouldDisplay(farm), Is.False);
        CompleteResearch(researchManager, "Agriculture");

        Assert.That(
            buildingManager.ShouldDisplay(farm),
            Is.True,
            "Farm must become visible as soon as Agriculture is completed.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BuildingTransaction_RejectsSingleUnitChangeThatCannotChangeHugeCount(bool deconstruct)
    {
        CreateManager<GameManager>("HugeCount-GameManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("HugeCount-ResourceManager");
        BuildingManager buildingManager = CreateManager<BuildingManager>("HugeCount-BuildingManager");
        ResourceDef wood = DataBase<ResourceDef>.Find("WoodLog");
        BuildingDef building = CreateTransactionBuilding("HugeCountTransaction", wood);
        BuildingState state = buildingManager.EnsureBuilding(building);
        state.SetAmountForEditor(new ExpantaNum("1e20"));
        resourceManager.SetAmount(wood, new ExpantaNum(1000));
        ExpantaNum previousAmount = state.Amount;
        ExpantaNum previousWood = resourceManager.GetAmount(wood);

        if (deconstruct)
            Assert.That(buildingManager.GetDeconstructionPreview(building, ExpantaNum.One), Is.Null);
        else
        {
            Assert.That(buildingManager.GetBuildFailure(building, ExpantaNum.One), Is.EqualTo(BuildFailure.InvalidAmount));
            Assert.That(buildingManager.GetMaxBuildable(building, ExpantaNum.One), Is.EqualTo(ExpantaNum.Zero)); // Discrete count.
        }

        bool succeeded = deconstruct
            ? buildingManager.TryDeconstruct(building, ExpantaNum.One, out _)
            : buildingManager.TryBuild(building, ExpantaNum.One, out _);

        Assert.That(succeeded, Is.False,
            "A transaction must reject a quantity that cannot change the represented building count.");
        Assert.That(state.Amount, Is.EqualTo(previousAmount)); // Discrete count must remain unchanged on rejection.
        Assert.That(resourceManager.GetAmount(wood).ApproximatelyEquals(previousWood), Is.True,
            "A rejected quantity must neither charge construction costs nor grant a deconstruction refund.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BuildingUpgrade_RejectsSingleUnitChangeThatCannotChangeEitherHugeCount(bool hugeSource)
    {
        CreateManager<GameManager>("HugeUpgrade-GameManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("HugeUpgrade-ResourceManager");
        BuildingManager buildingManager = CreateManager<BuildingManager>("HugeUpgrade-BuildingManager");
        ResourceDef wood = DataBase<ResourceDef>.Find("WoodLog");
        BuildingDef source = CreateTransactionBuilding("HugeUpgradeSource", wood);
        BuildingDef target = CreateTransactionBuilding("HugeUpgradeTarget", wood);
        source.SetUpgradeToForEditor(target);
        buildingManager.RebuildBuildingChainIndexForEditor(new[] { source, target });
        BuildingState sourceState = buildingManager.EnsureBuilding(source);
        BuildingState targetState = buildingManager.EnsureBuilding(target);
        sourceState.SetAmountForEditor(hugeSource ? new ExpantaNum("1e20") : new ExpantaNum(10));
        targetState.SetAmountForEditor(hugeSource ? ExpantaNum.Zero : new ExpantaNum("1e20"));
        resourceManager.SetAmount(wood, new ExpantaNum(1000));
        ExpantaNum previousSource = sourceState.Amount;
        ExpantaNum previousTarget = targetState.Amount;
        ExpantaNum previousWood = resourceManager.GetAmount(wood);
        var deltas = new List<Pair<ResourceDef, ExpantaNum>>();

        Assert.That(buildingManager.GetUpgradeFailure(source, ExpantaNum.One), Is.EqualTo(BuildFailure.InvalidAmount));
        Assert.That(buildingManager.GetMaxUpgradeable(source, ExpantaNum.One), Is.EqualTo(ExpantaNum.Zero)); // Discrete count.
        buildingManager.GetUpgradeResourceDeltas(source, ExpantaNum.One, deltas);
        Assert.That(deltas, Is.Empty);
        Assert.That(buildingManager.TryUpgrade(source, ExpantaNum.One, out BuildFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.InvalidAmount));
        Assert.That(sourceState.Amount, Is.EqualTo(previousSource)); // Discrete count must be preserved.
        Assert.That(targetState.Amount, Is.EqualTo(previousTarget)); // Discrete count must be preserved.
        Assert.That(resourceManager.GetAmount(wood).ApproximatelyEquals(previousWood), Is.True);
    }

    private BuildingDef CreateTransactionBuilding(string id, ResourceDef resource)
    {
        BuildingDef building = CreateBuilding(id);
        building.ConfigureEconomyForEditor(
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            new List<Pair<ResourceDef, ExpantaNum>> { new(resource, new ExpantaNum(10)) },
            new List<Pair<ResourceDef, ExpantaNum>>(),
            new List<Pair<ResourceDef, ExpantaNum>>());
        return building;
    }

    private T CreateManager<T>(string name) where T : Component
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    private BuildingDef CreateBuilding(string id)
    {
        BuildingDef building = ScriptableObject.CreateInstance<BuildingDef>();
        building.SetIdForEditor(id);
        createdObjects.Add(building);
        return building;
    }

    private static void CompleteResearch(ResearchManager manager, string id)
    {
        EnsureInitialized(manager);
        ResearchState state = manager.GetState(DataBase<ResearchDef>.Find(id));
        state.SetStatusForEditor(ResearchStatus.Completed);
    }

    private static void EnsureInitialized(ResearchManager manager)
    {
        if (manager.States.Count > 0)
            return;
        manager.InitializeForEditor();
    }
}
