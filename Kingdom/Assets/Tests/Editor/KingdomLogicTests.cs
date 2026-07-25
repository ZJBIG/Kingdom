using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class KingdomLogicTests
{
    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(createdObjects[i]);
        createdObjects.Clear();
    }

    [TestCase(0, 5500, 1, 1)]
    [TestCase(359, 5500, 12, 30)]
    [TestCase(360, 5501, 1, 1)]
    [TestCase(361, 5501, 1, 2)]
    [TestCase(-1, 5499, 12, 30)]
    public void CalendarIntToData_UsesTwelveThirtyDayMonths(
        int totalDays,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var actual = GameManager.CalendarIntToData(totalDays);

        Assert.That(actual.Year, Is.EqualTo(expectedYear));
        Assert.That(actual.Month, Is.EqualTo(expectedMonth));
        Assert.That(actual.Day, Is.EqualTo(expectedDay));
    }

    [Test]
    public void CalendarIntToData_RejectsInvalidCalendarDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameManager.CalendarIntToData(0, daysPerMonth: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameManager.CalendarIntToData(0, monthsPerYear: 0));
    }

    [Test]
    public void Pair_ProvidesStructuralEqualityAndDictionaryLookup()
    {
        var first = new Pair<int, string>(7, "wood");
        var equivalent = new Pair<int, string>(7, "wood");
        var dictionary = new Dictionary<Pair<int, string>, string> { [first] = "stored" };

        Assert.That(first.First, Is.EqualTo(7));
        Assert.That(first.Second, Is.EqualTo("wood"));
        Assert.That(first, Is.EqualTo(equivalent));
        Assert.That(dictionary[equivalent], Is.EqualTo("stored"));
    }

    [Test]
    public void Pair_PreservesLegacySerializedFieldNames()
    {
        var container = new PairContainer { value = new Pair<int, string>(3, "stone") };
        string json = JsonUtility.ToJson(container);
        PairContainer restored = JsonUtility.FromJson<PairContainer>(json);

        StringAssert.Contains("\"first\"", json);
        StringAssert.Contains("\"second\"", json);
        Assert.That(restored.value, Is.EqualTo(container.value));
    }

    [Test]
    public void BuildingTransactionRules_FloorsAndClampsAmountsBeforeTotals()
    {
        Assert.That(BuildingTransactionRules.TryNormalizePositiveWhole("10.9", out ExpantaNum normalized), Is.True);
        Assert.That(normalized, Is.EqualTo(new ExpantaNum(10)));

        ExpantaNum clamped = BuildingTransactionRules.ClampToAvailable(1000, 10);
        Assert.That(clamped, Is.EqualTo(new ExpantaNum(10)));
        Assert.That(BuildingTransactionRules.Total(4, clamped), Is.EqualTo(new ExpantaNum(40)));
    }

    [TestCase("")]
    [TestCase("not-a-number")]
    [TestCase("0.9")]
    [TestCase("-2")]
    public void BuildingTransactionRules_RejectsInvalidOrSubOneAmounts(string input)
    {
        Assert.That(BuildingTransactionRules.TryNormalizePositiveWhole(input, out _), Is.False);
    }

    [Test]
    public void AdvanceFood_UsesConsumptionRateAndClampsAtZero()
    {
        Assert.That(GameManager.AdvanceFood(100, 5, 3, 10), Is.EqualTo(new ExpantaNum(120)));
        Assert.That(GameManager.AdvanceFood(10, 0, 3, 10), Is.EqualTo(ExpantaNum.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameManager.AdvanceFood(10, 1, 1, -1));
    }

    [Test]
    public void GameState_HoldsCanonicalNewGameValues()
    {
        var state = new GameState();

        Assert.That(state.CalendarDays, Is.EqualTo(0));
        Assert.That(state.KingdomName, Is.EqualTo("鼠托邦"));
        Assert.That(state.TechLevel, Is.EqualTo(TechLevel.Primitive));
        Assert.That(state.FoodAmount, Is.EqualTo(new ExpantaNum(10000)));
        Assert.That(state.FoodProductionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.AvailableSpace, Is.EqualTo(new ExpantaNum(100000)));
        Assert.That(state.AvailableProductivity, Is.EqualTo(new ExpantaNum(100)));
        Assert.That(SaveFormat.CurrentVersion, Is.EqualTo(2));
    }

    [Test]
    public void ResourceState_ExistsIndependentlyFromResourceDisplayer()
    {
        Resource resource = ScriptableObject.CreateInstance<Resource>();
        resource.name = "TestResource";
        createdObjects.Add(resource);

        var state = new ResourceState(resource);

        Assert.That(state.Definition, Is.SameAs(resource));
        Assert.That(state.Amount, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.ProductionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.ConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.Efficiency, Is.EqualTo(ExpantaNum.One));
    }

    [Test]
    public void ResourceAdvance_UsesElapsedSecondsAndClampsAtZero()
    {
        Assert.That(ResourceManager.AdvanceAmount(100, 8, 3, 2), Is.EqualTo(new ExpantaNum(110)));
        Assert.That(ResourceManager.AdvanceAmount(5, 0, 10, 1), Is.EqualTo(ExpantaNum.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceManager.AdvanceAmount(0, 0, 0, -0.1));
    }

    [Test]
    public void ResourceSatisfaction_UsesInventoryAndPotentialProductionForTheTick()
    {
        Assert.That(ResourceManager.CalculateSatisfaction(2, 0, 10, 1), Is.EqualTo(new ExpantaNum(0.2)));
        Assert.That(ResourceManager.CalculateSatisfaction(2, 3, 10, 1), Is.EqualTo(new ExpantaNum(0.5)));
        Assert.That(ResourceManager.CalculateSatisfaction(0, 0, 0, 1), Is.EqualTo(ExpantaNum.One));
        Assert.That(ResourceManager.CalculateSatisfaction(0, 0, 10, 1), Is.EqualTo(ExpantaNum.Zero));
        Assert.That(ResourceManager.CalculateSatisfaction(0, 0, 10, 0), Is.EqualTo(ExpantaNum.One));
    }

    [Test]
    public void ToGameString_PreservesThreeSignificantDigitsForThousands()
    {
        Assert.That(new ExpantaNum(1220).ToGameString(), Is.EqualTo("1.22K"));
    }

    [Test]
    public void ResearchLineView_UsesStableSelectionColors()
    {
        Research prerequisite = CreateResearch("LinePrerequisite");
        Research target = CreateResearch("LineTarget");
        Research unrelated = CreateResearch("LineUnrelated");

        Assert.That(ResearchLineView.GetColor(null, prerequisite, target), Is.EqualTo(ResearchLineView.UnselectedColor));
        Assert.That(ResearchLineView.GetColor(prerequisite, prerequisite, target), Is.EqualTo(ResearchLineView.NextColor));
        Assert.That(ResearchLineView.GetColor(target, prerequisite, target), Is.EqualTo(ResearchLineView.PrerequisiteColor));
        Assert.That(ResearchLineView.GetColor(unrelated, prerequisite, target), Is.EqualTo(ResearchLineView.UnselectedColor));
    }

    [Test]
    public void GameTick_IntegratesFoodEveryTickAndCalendarSeparately()
    {
        GameManager gameManager = CreateManager<GameManager>("GameManager-Food-Test");
        gameManager.AdjustFoodRates(10, 5);

        gameManager.Tick(9.9d);

        Assert.That(gameManager.State.CalendarDays, Is.EqualTo(0));
        Assert.That(gameManager.State.FoodAmount, Is.EqualTo(new ExpantaNum(10024.5)));

        gameManager.Tick(0.1d);

        Assert.That(gameManager.State.CalendarDays, Is.EqualTo(1));
        Assert.That(gameManager.State.FoodAmount, Is.EqualTo(new ExpantaNum(10025)));
    }

    [Test]
    public void SimulationManager_ManualTick_AdvancesCalendarAndResourcesWithoutUi()
    {
        var managerObject = new GameObject("SimulationManagers-Test");
        createdObjects.Add(managerObject);

        GameManager gameManager = managerObject.AddComponent<GameManager>();
        ResourceManager resourceManager = managerObject.AddComponent<ResourceManager>();
        managerObject.AddComponent<BuildingManager>();
        managerObject.AddComponent<ResearchManager>();
        SimulationManager simulationManager = managerObject.AddComponent<SimulationManager>();

        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetAmount(wood, ExpantaNum.Zero);
        resourceManager.SetProductionRate(wood, 10);

        simulationManager.ManualTick(10d);

        Assert.That(gameManager.State.CalendarDays, Is.EqualTo(1));
        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(new ExpantaNum(100)));
    }

    [Test]
    public void SimulationManager_UpdateLoopStartsPausedUntilBootstrapRuns()
    {
        var managerObject = new GameObject("SimulationManagers-Paused-Test");
        createdObjects.Add(managerObject);

        SimulationManager simulationManager = managerObject.AddComponent<SimulationManager>();

        Assert.That(simulationManager.IsRunning, Is.False);
        simulationManager.SetRunning(true);
        Assert.That(simulationManager.IsRunning, Is.True);
        simulationManager.SetRunning(false);
        Assert.That(simulationManager.IsRunning, Is.False);
    }

    [Test]
    public void UnifiedSaveRoot_DoesNotPersistDerivedRatesOrScriptableObjectReferences()
    {
        var saveData = new SaveManager.KingdomSaveData
        {
            Version = SaveFormat.CurrentVersion,
            General = new SaveManager.GameSaveData
            {
                CalendarDays = 7,
                KingdomName = "Test",
                TechLevel = TechLevel.Primitive,
                FoodAmount = "100",
                LastSaveUnixSeconds = 1
            },
            Resources = new SaveManager.ResourceSaveData
            {
                GlobalEfficiencyFactor = "1",
                Resources = new List<SaveManager.ResourceStateSaveData>
                {
                    new SaveManager.ResourceStateSaveData { ResourceId = "WoodLog", Amount = "25" }
                }
            },
            Buildings = new SaveManager.BuildingSaveData
            {
                GlobalEfficiencyFactor = "1",
                Buildings = new List<SaveManager.BuildingStateSaveData>
                {
                    new SaveManager.BuildingStateSaveData
                    {
                        BuildingId = "Farm",
                        Amount = "2",
                        AutoBuild = true,
                        AutoBuildProgress = "3"
                    }
                }
            },
            Researches = new SaveManager.ResearchSaveData
            {
                GlobalEfficiencyFactor = "1",
                States = new List<SaveManager.ResearchStateSaveData>(),
                SelectedResearchId = string.Empty
            }
        };

        string json = JsonUtility.ToJson(saveData);

        StringAssert.Contains("\"Version\":2", json);
        StringAssert.Contains("\"ResourceId\":\"WoodLog\"", json);
        StringAssert.DoesNotContain("ProductionRate", json);
        StringAssert.DoesNotContain("ConsumptionRate", json);
        StringAssert.DoesNotContain("FoodProductionRate", json);
        StringAssert.DoesNotContain("FoodConsumptionRate", json);
        StringAssert.DoesNotContain("fileID", json);
    }

    [Test]
    public void SaveApply_IsIdempotentAndRecalculatesDerivedRates()
    {
        CreateManager<GameManager>("Save-GameManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("Save-ResourceManager");
        BuildingManager buildingManager = CreateManager<BuildingManager>("Save-BuildingManager");
        CreateManager<ResearchManager>("Save-ResearchManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-SaveManager");

        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        InvokeApplySaveData(saveManager, data);

        Resource wood = DataBase<Resource>.Find("WoodLog");
        Building farm = DataBase<Building>.Find("Farm");
        ExpantaNum firstAmount = resourceManager.GetAmount(wood);
        ExpantaNum firstFoodRate = GameManager.Instance.State.FoodProductionRate;
        ExpantaNum firstBuildingAmount = buildingManager.GetState(farm).Amount;

        InvokeApplySaveData(saveManager, data);

        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(firstAmount));
        Assert.That(GameManager.Instance.State.FoodProductionRate, Is.EqualTo(firstFoodRate));
        Assert.That(buildingManager.GetState(farm).Amount, Is.EqualTo(firstBuildingAmount));
        Assert.That(GameManager.Instance.State.FoodProductionRate, Is.EqualTo(new ExpantaNum(10)));
    }

    [Test]
    public void SaveApply_UnknownSelectedResearchIdIsActionable()
    {
        CreateManager<GameManager>("Save-Invalid-GameManager");
        CreateManager<ResourceManager>("Save-Invalid-ResourceManager");
        CreateManager<BuildingManager>("Save-Invalid-BuildingManager");
        CreateManager<ResearchManager>("Save-Invalid-ResearchManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-Invalid-SaveManager");

        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Researches.SelectedResearchId = "missing-research-id";

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<KeyNotFoundException>());
        StringAssert.Contains("missing-research-id", exception.InnerException.Message);
    }

    private static SaveManager.KingdomSaveData CreateRepresentativeSaveData()
    {
        return new SaveManager.KingdomSaveData
        {
            Version = SaveFormat.CurrentVersion,
            General = new SaveManager.GameSaveData
            {
                CalendarDays = 3,
                KingdomName = "Save Test",
                TechLevel = TechLevel.Primitive,
                FoodAmount = "10000",
                LastSaveUnixSeconds = 1
            },
            Resources = new SaveManager.ResourceSaveData
            {
                GlobalEfficiencyFactor = "1",
                Resources = new List<SaveManager.ResourceStateSaveData>
                {
                    new SaveManager.ResourceStateSaveData { ResourceId = "WoodLog", Amount = "25" }
                }
            },
            Buildings = new SaveManager.BuildingSaveData
            {
                GlobalEfficiencyFactor = "1",
                Buildings = new List<SaveManager.BuildingStateSaveData>
                {
                    new SaveManager.BuildingStateSaveData
                    {
                        BuildingId = "Farm",
                        Amount = "2",
                        AutoBuild = true,
                        AutoBuildProgress = "3"
                    }
                }
            },
            Researches = new SaveManager.ResearchSaveData
            {
                GlobalEfficiencyFactor = "1",
                States = new List<SaveManager.ResearchStateSaveData>(),
                SelectedResearchId = string.Empty
            }
        };
    }

    private static void InvokeApplySaveData(SaveManager saveManager, SaveManager.KingdomSaveData data)
    {
        MethodInfo method = typeof(SaveManager).GetMethod(
            "ApplySaveData",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(saveManager, new object[] { data });
    }

    [Test]
    public void AdvanceResearchProgress_UsesElapsedSecondsAndClampsToCost()
    {
        Assert.That(ResearchManager.AdvanceResearchProgress(0, 10, 100, 0.5), Is.EqualTo(new ExpantaNum(5)));
        Assert.That(ResearchManager.AdvanceResearchProgress(90, 10, 95, 1), Is.EqualTo(new ExpantaNum(95)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResearchManager.AdvanceResearchProgress(0, 1, 10, -0.1));
    }

    [Test]
    public void ResearchState_OwnsProgressStatusAndParsedBaseCost()
    {
        Research prerequisite = CreateResearch("Prerequisite");
        prerequisite.BaseCost = "10";
        Research research = CreateResearch("Target");
        research.BaseCost = "1e1000";
        research.SetPrerequisitesForEditor(new List<Research> { prerequisite });

        var state = new ResearchState(research);

        Assert.That(state.Definition, Is.SameAs(research));
        Assert.That(state.Progress, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.Status, Is.EqualTo(ResearchStatus.Locked));
        Assert.That(state.CostPaid, Is.False);
        Assert.That(state.BaseCost, Is.EqualTo(ExpantaNum.Parse("1e1000")));
        Assert.That(state.ProgressRatio, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void ResearchState_WithoutPrerequisitesStartsAvailable()
    {
        Research research = CreateResearch("Available");
        research.BaseCost = "100";

        Assert.That(new ResearchState(research).Status, Is.EqualTo(ResearchStatus.Available));
    }

    [Test]
    public void ResearchState_RejectsInvalidBaseCostAtCreation()
    {
        Research research = CreateResearch("InvalidCost");
        research.BaseCost = "invalid";

        Assert.Throws<FormatException>(() => new ResearchState(research));
    }

    [Test]
    public void ResearchSpeedEffect_IsDeterministicAcrossTechLevels()
    {
        Assert.That(ResearchManager.ResearchSpeedEffect(TechLevel.Primitive, TechLevel.Primitive), Is.EqualTo(1d));
        Assert.That(
            ResearchManager.ResearchSpeedEffect(TechLevel.Primitive, TechLevel.Medieval),
            Is.EqualTo(2d / 3d).Within(1e-12));
    }

    [Test]
    public void ResearchCostPayment_IsIncrementalAndOnlyPaidOnceWithoutUi()
    {
        var managerObject = new GameObject("ResourceManager-Test");
        createdObjects.Add(managerObject);
        ResourceManager resourceManager = managerObject.AddComponent<ResourceManager>();

        Resource wood = DataBase<Resource>.Find("WoodLog");
        Research research = DataBase<Research>.Find("StoneCutting");
        var state = new ResearchState(research);

        resourceManager.SetAmount(wood, 499);
        Assert.That(ResearchManager.TryPayResearchCost(state), Is.False);
        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.CostPaid, Is.False);

        resourceManager.SetAmount(wood, 1);
        Assert.That(ResearchManager.TryPayResearchCost(state), Is.True);
        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.CostPaid, Is.True);

        resourceManager.AddAmount(wood, 100);
        Assert.That(ResearchManager.TryPayResearchCost(state), Is.True);
        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(new ExpantaNum(100)));
    }

    [Test]
    public void DataBase_FindsDefinitionsByStableId()
    {
        Resource gold = DataBase<Resource>.Find("Gold");
        Building farm = DataBase<Building>.Find("Farm");
        Research agriculture = DataBase<Research>.Find("Agriculture");

        Assert.That(gold.Id, Is.EqualTo("Gold"));
        Assert.That(farm.Id, Is.EqualTo("Farm"));
        Assert.That(agriculture.Id, Is.EqualTo("Agriculture"));
        Assert.That(DataBase<Resource>.Find("gold"), Is.SameAs(gold));
    }

    [Test]
    public void DataBase_OffersSafeLookupAndActionableMissingIdErrors()
    {
        Assert.That(DataBase<Resource>.TryFind("does-not-exist", out _), Is.False);
        Assert.That(DataBase<Resource>.TryFind(null, out _), Is.False);

        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
            () => DataBase<Resource>.Find("does-not-exist"));
        StringAssert.Contains("does-not-exist", exception.Message);
        StringAssert.Contains(nameof(Resource), exception.Message);
    }

    [Test]
    public void BuildingDefinitions_ExposeCanonicalTypedResourcePairs()
    {
        Building farm = DataBase<Building>.Find("Farm");

        Assert.That(farm.ResourceRequirements.Count, Is.EqualTo(1));
        Assert.That(farm.ResourceRequirements[0].First, Is.SameAs(DataBase<Resource>.Find("WoodLog")));
        Assert.That(farm.ResourceRequirements[0].Second, Is.EqualTo(new ExpantaNum(15)));
        Assert.That(farm.ResourceGenerationRates, Is.Empty);
        Assert.That(farm.ResourceConsumptionRates, Is.Empty);
    }

    [Test]
    public void BuildingState_OwnsRuntimeValuesAndCachesParsedDefinitionNumbers()
    {
        Building building = DataBase<Building>.Find("Farm");
        var state = new BuildingState(building);

        Assert.That(state.Definition, Is.SameAs(building));
        Assert.That(state.Amount, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.Efficiency, Is.EqualTo(ExpantaNum.One));
        Assert.That(state.AutoBuild, Is.False);
        Assert.That(state.AutoBuildProgress, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.AutoBuildWorkRequired, Is.EqualTo(new ExpantaNum(5)));
        Assert.That(state.SpaceCost, Is.EqualTo(new ExpantaNum(2)));
        Assert.That(state.BuildEffort, Is.EqualTo(new ExpantaNum(2)));
        Assert.That(state.ProductivityGranted, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void BuildingDefinitions_SplitCostsFromGrantedProductionAndFood()
    {
        Building constructionCenter = DataBase<Building>.Find("ConstructionCenter");
        Building farm = DataBase<Building>.Find("Farm");

        Assert.That(constructionCenter.BuildEffort, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(constructionCenter.ProductivityGranted, Is.EqualTo(new ExpantaNum(1000)));
        Assert.That(farm.FoodProductionRate, Is.EqualTo(new ExpantaNum(5)));
        Assert.That(farm.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void Managers_RaiseEventsWhenRuntimeDefinitionsAreDiscovered()
    {
        ResourceManager resourceManager = CreateManager<ResourceManager>("ResourceManager");
        BuildingManager buildingManager = CreateManager<BuildingManager>("BuildingManager");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        Building farm = DataBase<Building>.Find("Farm");

        ResourceState addedResource = null;
        BuildingState addedBuilding = null;
        resourceManager.ResourceStateAdded += state => addedResource = state;
        buildingManager.BuildingStateAdded += state => addedBuilding = state;

        ResourceState resourceState = resourceManager.EnsureResource(wood);
        BuildingState buildingState = buildingManager.EnsureBuilding(farm);

        Assert.That(addedResource, Is.SameAs(resourceState));
        Assert.That(addedBuilding, Is.SameAs(buildingState));
    }

    [Test]
    public void ResourceViewer_BindsExistingStatesWhenOpenedAfterResourcesWereDiscovered()
    {
        ResourceManager resourceManager = CreateManager<ResourceManager>("ResourceManager");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.EnsureResource(wood);

        GameObject setObject = new GameObject("WoodSet", typeof(RectTransform), typeof(Image), typeof(ResourceDisplayerSet));
        createdObjects.Add(setObject);
        ResourceDisplayerSet set = setObject.GetComponent<ResourceDisplayerSet>();
        set.Content = new GameObject("Content", typeof(RectTransform)).transform;
        createdObjects.Add(set.Content.gameObject);
        set.Content.SetParent(setObject.transform, false);

        GameObject viewerObject = new GameObject("ResourceViewer", typeof(RectTransform));
        createdObjects.Add(viewerObject);
        setObject.transform.SetParent(viewerObject.transform, false);
        viewerObject.AddComponent<ResourceViewer>();

        Assert.That(set.Displayers.ContainsKey(wood), Is.True);
    }

    [Test]
    public void ResearchValidator_ReportsCompleteCyclePath()
    {
        Research a = CreateResearch("A");
        Research b = CreateResearch("B");
        Research c = CreateResearch("C");
        a.SetPrerequisitesForEditor(new List<Research> { b });
        b.SetPrerequisitesForEditor(new List<Research> { c });
        c.SetPrerequisitesForEditor(new List<Research> { a });

        bool valid = ResearchValidator.ValidateNoCycles(new[] { a, b, c }, out string error);

        Assert.That(valid, Is.False);
        Assert.That(error, Is.EqualTo("Research dependency cycle: A -> B -> C -> A"));
    }

    [Test]
    public void ResearchValidator_RejectsNullAndDuplicatePrerequisites()
    {
        Research root = CreateResearch("Root");
        Research dependency = CreateResearch("Dependency");
        root.SetPrerequisitesForEditor(new List<Research> { dependency, dependency });

        Assert.That(ResearchValidator.ValidateNoCycles(new[] { root }, out string duplicateError), Is.False);
        StringAssert.Contains("duplicate prerequisite", duplicateError);

        root.SetPrerequisitesForEditor(new List<Research> { null });
        Assert.That(ResearchValidator.ValidateNoCycles(new[] { root }, out string nullError), Is.False);
        StringAssert.Contains("null prerequisite", nullError);
    }

    private Research CreateResearch(string name)
    {
        Research research = ScriptableObject.CreateInstance<Research>();
        research.name = name;
        research.SetPrerequisitesForEditor(new List<Research>());
        research.BuildingUnlock = new List<Building>();
        createdObjects.Add(research);
        return research;
    }

    private T CreateManager<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    [Serializable]
    private sealed class PairContainer
    {
        public Pair<int, string> value;
    }
}
