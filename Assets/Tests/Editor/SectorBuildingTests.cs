using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class SectorBuildingTests
{
    private readonly List<Object> createdObjects = new();

    [SetUp]
    public void SetUp()
    {
        DestroyManagers<GameManager>();
        DestroyManagers<ResourceManager>();
        DestroyManagers<BuildingManager>();
        DestroyManagers<ResearchManager>();
        DestroyManagers<WorkshopManager>();
        DestroyManagers<SaveManager>();
        DestroyManagers<TutorialManager>();
        createdObjects.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(createdObjects[i]);
        createdObjects.Clear();
        DestroyManagers<TutorialManager>();
        SaveManager.ClearSaveRootOverrideForTests();
    }

    [Test]
    public void SectorBuildingInheritsBuildingWithoutDuplicateBuildingFields()
    {
        Assert.That(typeof(SectorBuilding).BaseType, Is.EqualTo(typeof(Building)));
        FieldInfo[] fields = typeof(SectorBuilding).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        Assert.That(fields, Has.Length.EqualTo(2));
        Assert.That(System.Array.Exists(fields, field => field.Name == "sector"), Is.True);
        Assert.That(System.Array.Exists(fields, field => field.Name == "maxAmount"), Is.True);
    }

    [Test]
    public void EarthMoonLogisticsHubIsOneSectorBuildingDefinition()
    {
        SectorBuilding asset = Resources.Load<SectorBuilding>(
            "Datas/Building/Spacer/EarthMoonLogisticsHub");
        Building fromDatabase = DataBase<Building>.Find("EarthMoonLogisticsHub");

        Assert.That(asset, Is.Not.Null);
        Assert.That(fromDatabase, Is.SameAs(asset));
        Assert.That(asset.Sector, Is.EqualTo(DataBase<SectorDefinition>.Find("AzurePool")));
        Assert.That(asset.MaxAmount, Is.EqualTo(1));
        Assert.That(asset.UpgradeTo, Is.Null);
        Assert.That(asset.SpaceCost, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(asset.TechLevel, Is.EqualTo(TechLevel.Spacer));
        Assert.That(asset.HasValidCostGrowth, Is.True);
        Assert.That(HasPositivePair(asset.ResourceRequirements, "TitaniumAlloy"), Is.True);
        Assert.That(HasPositivePair(asset.ResourceRequirements, "Composite"), Is.True);
        Assert.That(HasPositivePair(asset.ResourceRequirements, "Machinery"), Is.True);
        Assert.That(HasPositivePair(asset.ResourceRequirements, "Electronics"), Is.True);
        Assert.That(HasPositivePair(asset.ResourceRequirements, "RocketFuel"), Is.True);
        Assert.That(asset.LogisticsProductionRate - asset.LogisticsConsumptionRate,
            Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(asset.FleetPowerGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(asset.DefensePowerGranted, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(asset.ProductivityConsumption, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(asset.PowerConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(asset.FoodConsumptionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(HasPositivePair(asset.ResourceConsumptionRates, "Aluminum"), Is.True);
        Assert.That(HasPositivePair(asset.ResourceConsumptionRates, "Electronics"), Is.True);
        Assert.That(HasPositivePair(asset.ResourceConsumptionRates, "RocketFuel"), Is.True);
        Assert.That(File.ReadAllText(
            "Assets/Resources/Datas/Building/Spacer/EarthMoonLogisticsHub.asset"),
            Does.Not.Contain("spaceCost:"));
    }

    [Test]
    public void SectorBuildingValidationRejectsMissingSector()
    {
        SectorBuilding building = ScriptableObject.CreateInstance<SectorBuilding>();
        try
        {
            Assert.Throws<System.InvalidOperationException>(() => SectorBuilding.Validate(building));
        }
        finally
        {
            Object.DestroyImmediate(building);
        }
    }

    [Test]
    public void SectorBuildingValidationRejectsInvalidLimitAndTerritoryCost()
    {
        SectorBuilding building = ScriptableObject.CreateInstance<SectorBuilding>();
        SectorDefinition sector = ScriptableObject.CreateInstance<SectorDefinition>();
        try
        {
            SetPrivateField(building, "sector", sector);
            SetPrivateField(building, "maxAmount", 0);
            Assert.Throws<System.InvalidOperationException>(() => SectorBuilding.Validate(building));

            SetPrivateField(building, "maxAmount", 1);
            SetPrivateField(building, "spaceCost", "1");
            Assert.Throws<System.InvalidOperationException>(() => SectorBuilding.Validate(building));
        }
        finally
        {
            Object.DestroyImmediate(sector);
            Object.DestroyImmediate(building);
        }
    }

    [Test]
    public void OrdinaryBuildingCannotUpgradeToSectorBuilding()
    {
        Building ordinary = ScriptableObject.CreateInstance<Building>();
        SectorBuilding sector = Resources.Load<SectorBuilding>(
            "Datas/Building/Spacer/EarthMoonLogisticsHub");
        try
        {
            ordinary.SetIdForEditor("TestOrdinaryBuilding");
            ordinary.SetUpgradeToForEditor(sector);
            Assert.Throws<System.InvalidOperationException>(() =>
                BuildingManager.ValidateBuildingChains(new Building[] { ordinary, sector }));
        }
        finally
        {
            Object.DestroyImmediate(ordinary);
        }
    }

    [Test]
    public void SectorBuildingCannotParticipateInAnUpgradeChain()
    {
        SectorBuilding sector = ScriptableObject.CreateInstance<SectorBuilding>();
        Building target = ScriptableObject.CreateInstance<Building>();
        SectorDefinition moon = DataBase<SectorDefinition>.Find("AzurePool");
        try
        {
            sector.SetIdForEditor("TestSectorBuilding");
            SetPrivateField(sector, "sector", moon);
            sector.SetUpgradeToForEditor(target);

            Assert.Throws<System.InvalidOperationException>(() => SectorBuilding.Validate(sector));
        }
        finally
        {
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(sector);
        }
    }

    [Test]
    public void SectorBuildingBuildAndSavePathIsOccupiedAndTerritoryFree()
    {
        GameManager gameManager = CreateManager<GameManager>("SectorBuilding-GameManager");
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("SectorBuilding-ResourceManager");
        BuildingManager buildingManager =
            CreateManager<BuildingManager>("SectorBuilding-BuildingManager");
        ResearchManager researchManager =
            CreateManager<ResearchManager>("SectorBuilding-ResearchManager");
        CreateManager<WorkshopManager>("SectorBuilding-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("SectorBuilding-SaveManager");
        SectorBuilding building = Resources.Load<SectorBuilding>(
            "Datas/Building/Spacer/EarthMoonLogisticsHub");
        SectorState moon = gameManager.Sectors.GetState(building.Sector);

        AdvanceToSpacer(gameManager);
        CompleteResearch(researchManager, "TitaniumAlloyEngineering");
        RestorePopulation(gameManager.State, new ExpantaNum(1000d));
        AddEnoughBuildResources(resourceManager, building);

        Assert.That(
            buildingManager.TryBuild(building, ExpantaNum.One, out BuildFailure unoccupiedFailure),
            Is.False);
        Assert.That(unoccupiedFailure, Is.EqualTo(BuildFailure.SectorNotOccupied));

        moon.SetOccupiedForEditor(true);
        ExpantaNum territoryUsed = gameManager.State.TerritoryUsed;
        ExpantaNum territoryAvailable = gameManager.State.AvailableTerritory;
        Assert.That(
            buildingManager.TryBuild(building, ExpantaNum.One, out BuildFailure firstFailure),
            Is.True);
        Assert.That(firstFailure, Is.EqualTo(BuildFailure.None));
        Assert.That(gameManager.State.TerritoryUsed, Is.EqualTo(territoryUsed));
        Assert.That(gameManager.State.AvailableTerritory, Is.EqualTo(territoryAvailable));

        Assert.That(
            buildingManager.TryBuild(building, ExpantaNum.One, out BuildFailure limitFailure),
            Is.False);
        Assert.That(limitFailure, Is.EqualTo(BuildFailure.BuildingLimitReached));
        Assert.That(
            buildingManager.GetMaxBuildable(building, ExpantaNum.One),
            Is.EqualTo(ExpantaNum.Zero));

        Assert.That(
            buildingManager.TryDeconstruct(building, ExpantaNum.One, out BuildFailure deconstructFailure),
            Is.True);
        Assert.That(deconstructFailure, Is.EqualTo(BuildFailure.None));
        Assert.That(gameManager.State.TerritoryUsed, Is.EqualTo(territoryUsed));
        Assert.That(gameManager.State.AvailableTerritory, Is.EqualTo(territoryAvailable));
        AddEnoughBuildResources(resourceManager, building);
        Assert.That(
            buildingManager.TryBuild(building, ExpantaNum.One, out BuildFailure rebuildFailure),
            Is.True);
        Assert.That(rebuildFailure, Is.EqualTo(BuildFailure.None));

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string saveRoot = Path.Combine(
            projectRoot,
            "Temp",
            "SectorBuildingInvalidSave-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        try
        {
            SaveManager.SetSaveRootOverrideForTests(saveRoot);
            Assert.That(saveManager.SaveNow(true), Is.True);

            moon.SetOccupiedForEditor(false);
            Assert.That(saveManager.SaveNow(true), Is.True);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Kingdom.*" + Regex.Escape(building.Id), RegexOptions.Singleline));

            Assert.That(saveManager.LoadOrCreateGame(), Is.True,
                "An inconsistent sector-building primary save should fall back to its valid backup.");
            Assert.That(saveManager.LastLoadCreatedNewGame, Is.False);
            Assert.That(buildingManager.GetState(building).Amount, Is.EqualTo(ExpantaNum.One));
            Assert.That(gameManager.Sectors.GetState(building.Sector).Occupied, Is.True);
        }
        finally
        {
            SaveManager.ClearSaveRootOverrideForTests();
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, true);
        }
    }

    private T CreateManager<T>(string name) where T : Component
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    private static void DestroyManagers<T>() where T : Component
    {
        T[] managers = Object.FindObjectsOfType<T>();
        for (int i = 0; i < managers.Length; i++)
            Object.DestroyImmediate(managers[i].gameObject);
    }

    private static void AdvanceToSpacer(GameManager gameManager)
    {
        MethodInfo method = typeof(GameManager).GetMethod(
            "AdvanceTechLevel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(gameManager, new object[] { TechLevel.Spacer });
    }

    private static void CompleteResearch(ResearchManager manager, string id)
    {
        EnsureInitialized(manager);
        ResearchState state = manager.GetState(DataBase<Research>.Find(id));
        MethodInfo method = typeof(ResearchState).GetMethod(
            "SetStatus", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(state, new object[] { ResearchStatus.Completed });
    }

    private static void EnsureInitialized(ResearchManager manager)
    {
        if (manager.States.Count > 0)
            return;
        MethodInfo method = typeof(ResearchManager).GetMethod(
            "Initialize", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(manager, null);
    }

    private static void RestorePopulation(GameState state, ExpantaNum population)
    {
        MethodInfo method = typeof(GameState).GetMethod(
            "RestorePopulation", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(state, new object[] { population });
    }

    private static void AddEnoughBuildResources(ResourceManager resourceManager, Building building)
    {
        MethodInfo method = typeof(BuildingManager).GetMethod(
            "GetConstructionCostMultiplier", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        ExpantaNum costMultiplier = (ExpantaNum)method.Invoke(
            null, new object[] { building });
        for (int i = 0; i < building.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = building.ResourceRequirements[i];
            resourceManager.AddAmount(
                requirement.First,
                requirement.Second * costMultiplier * new ExpantaNum(2d));
        }
    }

    private static bool HasPositivePair(
        IReadOnlyList<Pair<Resource, ExpantaNum>> pairs,
        string resourceId)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            Pair<Resource, ExpantaNum> pair = pairs[i];
            if (pair.First != null && pair.First.Id == resourceId)
                return pair.Second > ExpantaNum.Zero;
        }
        return false;
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            target.GetType().BaseType.GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(target, value);
    }
}
