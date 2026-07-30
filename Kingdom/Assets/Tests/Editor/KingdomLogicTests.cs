using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        ProgressionModifierManager.Rebuild(null);
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
    public void AdvanceFood_ClampsProductionAtFoodCapacity()
    {
        Assert.That(
            GameManager.AdvanceFood(995, 10, 0, 1000, 1),
            Is.EqualTo(new ExpantaNum(1000)));
    }

    [Test]
    public void GameState_HoldsCanonicalNewGameValues()
    {
        var state = new GameState();

        Assert.That(state.CalendarDays, Is.EqualTo(0));
        Assert.That(state.KingdomName, Is.EqualTo("鼠托邦"));
        Assert.That(state.TechLevel, Is.EqualTo(TechLevel.Animal));
        Assert.That(state.FoodAmount, Is.EqualTo(new ExpantaNum(300)));
        Assert.That(state.FoodCapacity, Is.EqualTo(new ExpantaNum(500)));
        Assert.That(state.FoodProductionRate, Is.EqualTo(new ExpantaNum(5)));
        Assert.That(state.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.TerritoryTotal, Is.EqualTo(new ExpantaNum(500)));
        Assert.That(state.TerritoryUsed, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.AvailableTerritory, Is.EqualTo(new ExpantaNum(500)));
        Assert.That(state.Population.Population, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.Population.PopulationCapacity, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(PopulationState.FoodConsumptionPerPerson, Is.EqualTo(new ExpantaNum(0.8d)));
        Assert.That(PopulationState.ProductivityGrantedPerPerson, Is.EqualTo(new ExpantaNum(2d)));
        Assert.That(SaveFormat.CurrentVersion, Is.EqualTo(6));
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
    public void ResourceManager_AlwaysCreatesStartingWoodStateAndProduction()
    {
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("Starting-Wood-ResourceManager");
        Resource wood = DataBase<Resource>.Find(ResourceManager.StartingResourceId);

        Assert.That(resourceManager.States.ContainsKey(wood), Is.True);
        Assert.That(resourceManager.GetState(wood).ProductionRate, Is.EqualTo(ExpantaNum.One));
    }

    [Test]
    public void ResourceViewer_TreatsStartingWoodAsVisibleWithoutBuildingSources()
    {
        Resource wood = DataBase<Resource>.Find(ResourceManager.StartingResourceId);
        MethodInfo method = typeof(ResourceViewer).GetMethod(
            "IsResourceManufacturable",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        Assert.That((bool)method.Invoke(null, new object[] { wood }), Is.True);
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
    public void ResearchLineView_DistinguishesSelectedResearchRelationships()
    {
        Research prerequisite = CreateResearch("LinePrerequisite");
        Research target = CreateResearch("LineTarget");
        Research unrelated = CreateResearch("LineUnrelated");

        Assert.That(
            ResearchLineView.GetColor(
                null,
                prerequisite,
                target,
                ResearchStatus.Locked),
            Is.EqualTo(ResearchLineView.UnselectedColor));
        Assert.That(
            ResearchLineView.GetColor(
                target,
                prerequisite,
                target,
                ResearchStatus.Available),
            Is.EqualTo(ResearchLineView.IncompletePrerequisiteColor));
        Assert.That(
            ResearchLineView.GetColor(
                target,
                prerequisite,
                target,
                ResearchStatus.Completed),
            Is.EqualTo(ResearchLineView.CompletedPrerequisiteColor));
        Assert.That(
            ResearchLineView.GetColor(
                prerequisite,
                prerequisite,
                target,
                ResearchStatus.Locked),
            Is.EqualTo(ResearchLineView.AvailableSuccessorColor));
        Assert.That(
            ResearchLineView.GetColor(
                prerequisite,
                prerequisite,
                target,
                ResearchStatus.Completed),
            Is.EqualTo(ResearchLineView.AvailableSuccessorColor));
        Assert.That(
            ResearchLineView.GetColor(
                unrelated,
                prerequisite,
                target,
                ResearchStatus.Completed),
            Is.EqualTo(ResearchLineView.UnselectedColor));

        Research transitivePrerequisite = CreateResearch("LineTransitivePrerequisite");
        Assert.That(
            ResearchLineView.GetColor(
                transitivePrerequisite,
                prerequisite,
                target,
                ResearchStatus.Completed),
            Is.EqualTo(ResearchLineView.UnselectedColor));
    }

    [Test]
    public void ResearchDisplayer_UsesDistinctCompletedAndSelectedOutlineColors()
    {
        Color selected = ResearchDisplayer.GetOutlineColor(
            true,
            ResearchStatus.Completed);
        Color completed = ResearchDisplayer.GetOutlineColor(
            false,
            ResearchStatus.Completed);
        Color inactive = ResearchDisplayer.GetOutlineColor(
            false,
            ResearchStatus.Available);

        Assert.That(selected, Is.EqualTo(ResearchDisplayer.DisplayerFrame.OutlineSel));
        Assert.That(completed, Is.EqualTo(ResearchDisplayer.DisplayerFrame.OutlineCompleted));
        Assert.That(inactive, Is.EqualTo(ResearchDisplayer.DisplayerFrame.OutlineUnsel));
        Assert.That(completed, Is.Not.EqualTo(selected));
    }

    [Test]
    public void GameTick_IntegratesFoodEveryTickAndCalendarSeparately()
    {
        GameManager gameManager = CreateManager<GameManager>("GameManager-Food-Test");
        gameManager.AdjustFoodRates(10, 5);

        gameManager.Tick(9.9d);

        Assert.That(gameManager.State.CalendarDays, Is.EqualTo(0));
        Assert.That(gameManager.State.FoodAmount, Is.EqualTo(new ExpantaNum(399d)));

        gameManager.Tick(0.1d);

        Assert.That(gameManager.State.CalendarDays, Is.EqualTo(1));
        Assert.That(gameManager.State.FoodAmount, Is.EqualTo(new ExpantaNum(400)));

        gameManager.Tick(35d);
        Assert.That(gameManager.State.CalendarDays, Is.EqualTo(4));
        gameManager.Tick(4.9d);
        Assert.That(gameManager.State.CalendarDays, Is.EqualTo(4));
        gameManager.Tick(0.1d);
        Assert.That(gameManager.State.CalendarDays, Is.EqualTo(5));

    }

    [Test]
    public void C305_ResetDerivedEconomyUsesNewBalanceAndPreservesLegacyFood()
    {
        var state = new GameState();
        InvokeGameStateMethod(
            state,
            "RestoreCore",
            3,
            "Legacy",
            TechLevel.Animal,
            new ExpantaNum(10000),
            1L);
        InvokeGameStateMethod(
            state,
            "ResetDerivedEconomy",
            new ExpantaNum(100));

        Assert.That(state.FoodAmount, Is.EqualTo(new ExpantaNum(10000)));
        Assert.That(state.FoodCapacity, Is.EqualTo(new ExpantaNum(10000)));
        Assert.That(state.TerritoryTotal, Is.EqualTo(new ExpantaNum(500)));
        Assert.That(state.TerritoryUsed, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.AvailableTerritory, Is.EqualTo(new ExpantaNum(500)));
        Assert.That(state.Population.Population, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void C401_PopulationGrowthStopsWhenFoodSatisfactionIsZero()
    {
        PopulationState population = new PopulationState();
        InvokePopulationMethod(population, "RestorePopulation", new ExpantaNum(15));
        InvokePopulationMethod(population, "AdjustPopulationCapacity", new ExpantaNum(16));
        InvokePopulationMethod(
            population,
            "AdvancePopulation",
            60d,
            ExpantaNum.Zero,
            ExpantaNum.Zero);
        Assert.That(population.Population, Is.EqualTo(new ExpantaNum(15)));
        Assert.That(population.PopulationChangeProgress, Is.EqualTo(ExpantaNum.Zero));

        InvokePopulationMethod(
            population,
            "AdvancePopulation",
            60d,
            ExpantaNum.One,
            ExpantaNum.Zero);
        Assert.That(population.Population, Is.EqualTo(new ExpantaNum(16)));
    }

    [Test]
    public void PopulationGrowth_UsesLogisticRateAndCapacityDependentDeparture()
    {
        PopulationState growth = new PopulationState();
        InvokePopulationMethod(growth, "AdjustPopulationCapacity", new ExpantaNum(5));
        InvokePopulationMethod(
            growth,
            "AdvancePopulation",
            40d,
            ExpantaNum.One,
            PopulationState.BaseGrowthRatePerSecond * 1.5d,
            ExpantaNum.Zero);
        Assert.That(growth.Population, Is.EqualTo(ExpantaNum.One));

        PopulationState departure = new PopulationState();
        InvokePopulationMethod(departure, "RestorePopulation", new ExpantaNum(2));
        InvokePopulationMethod(
            departure,
            "AdvancePopulation",
            60d,
            ExpantaNum.One,
            PopulationState.BaseGrowthRatePerSecond * 100d,
            new ExpantaNum(2));
        Assert.That(
            departure.Population,
            Is.EqualTo(ExpantaNum.Zero),
            "Over-capacity departure should accelerate with relative excess population.");
    }

    [Test]
    public void C402_TerritoryTracksTotalUsedAndAvailableSeparately()
    {
        TerritoryState territory = new TerritoryState();

        InvokeTerritoryMethod(territory, "AdjustUsed", new ExpantaNum(30));
        Assert.That(territory.TerritoryTotal, Is.EqualTo(new ExpantaNum(500)));
        Assert.That(territory.TerritoryUsed, Is.EqualTo(new ExpantaNum(30)));
        Assert.That(territory.AvailableTerritory, Is.EqualTo(new ExpantaNum(470)));

        InvokeTerritoryMethod(territory, "AdjustUsed", new ExpantaNum(-10));
        Assert.That(territory.TerritoryUsed, Is.EqualTo(new ExpantaNum(20)));
    }

    [Test]
    public void C403_PopulationStateStoresDerivedCapacityAndPopulationChangeProgress()
    {
        PopulationState population = new PopulationState();
        InvokePopulationMethod(population, "RestorePopulation", new ExpantaNum(15));
        InvokePopulationMethod(population, "AdjustPopulationCapacity", new ExpantaNum(20));
        InvokePopulationMethod(
            population,
            "RestorePopulationChangeProgress",
            new ExpantaNum(0.25d));

        Assert.That(population.Population, Is.EqualTo(new ExpantaNum(15)));
        Assert.That(population.PopulationCapacity, Is.EqualTo(new ExpantaNum(20)));
        Assert.That(
            population.PopulationChangeProgress,
            Is.EqualTo(new ExpantaNum(0.25d)));
    }

    [Test]
    public void Productivity_IsDerivedFromPopulationResearchAndOwnedBuildings()
    {
        GameManager gameManager = CreateManager<GameManager>("Productivity-GameManager");
        BuildingManager buildingManager =
            CreateManager<BuildingManager>("Productivity-BuildingManager");
        InvokeGameStateMethod(
            gameManager.State,
            "RestorePopulation",
            new ExpantaNum(10));
        Building building = CreateEconomyBuilding(
            "ProductivityProvider",
            productivityConsumption: 3,
            productivityGranted: 4,
            populationCapacity: 0);
        Research research = CreateResearch("ProductivityResearch");
        research.BaseCost = "1";
        research.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.ProductivityGranted,
                Value = new ExpantaNum(7)
            }
        });
        ResearchState researchState = new ResearchState(research);
        typeof(ResearchState).GetMethod(
                "Restore",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(ExpantaNum), typeof(bool), typeof(bool) },
                null)
            .Invoke(researchState, new object[] { ExpantaNum.Zero, false, true });
        ProgressionModifierManager.Rebuild(new List<ResearchState> { researchState });

        Assert.That(buildingManager.TotalProductivity, Is.EqualTo(new ExpantaNum(27)));
        Assert.That(buildingManager.TryBuild(building, 2, out BuildFailure failure), Is.True);
        Assert.That(failure, Is.EqualTo(BuildFailure.None));
        Assert.That(buildingManager.TotalProductivity, Is.EqualTo(new ExpantaNum(35)));
        Assert.That(buildingManager.UsedProductivity, Is.EqualTo(new ExpantaNum(6)));
        Assert.That(buildingManager.AvailableProductivity, Is.EqualTo(new ExpantaNum(29)));

        buildingManager.GetState(building).SetEfficiencyForEditor(ExpantaNum.Zero);
        Assert.That(buildingManager.TotalProductivity, Is.EqualTo(new ExpantaNum(35)));
        Assert.That(buildingManager.AvailableProductivity, Is.EqualTo(new ExpantaNum(29)));
    }

    [Test]
    public void ProductivityProvider_CannotFundItsOwnConstruction()
    {
        GameManager gameManager = CreateManager<GameManager>("SelfFunding-GameManager");
        BuildingManager buildingManager =
            CreateManager<BuildingManager>("SelfFunding-BuildingManager");
        InvokeGameStateMethod(
            gameManager.State,
            "RestorePopulation",
            new ExpantaNum(2));
        Building building = CreateEconomyBuilding(
            "SelfFundingProvider",
            productivityConsumption: 6,
            productivityGranted: 100,
            populationCapacity: 0);

        Assert.That(
            buildingManager.TryBuild(building, ExpantaNum.One, out BuildFailure failure),
            Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.ProductivityInsufficient));
        Assert.That(buildingManager.GetState(building).Amount, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void OvercommittedProductivity_PreservesBuildingsAndBlocksNewConsumption()
    {
        GameManager gameManager = CreateManager<GameManager>("Overcommit-GameManager");
        BuildingManager buildingManager =
            CreateManager<BuildingManager>("Overcommit-BuildingManager");
        InvokeGameStateMethod(
            gameManager.State,
            "RestorePopulation",
            new ExpantaNum(2));
        Building existing = CreateEconomyBuilding(
            "LegacyOvercommit",
            productivityConsumption: 6,
            productivityGranted: 0,
            populationCapacity: 0);
        BuildingState existingState = buildingManager.EnsureBuilding(existing);
        existingState.SetAmountForEditor(ExpantaNum.One);
        Building candidate = CreateEconomyBuilding(
            "BlockedByOvercommit",
            productivityConsumption: 1,
            productivityGranted: 0,
            populationCapacity: 0);

        Assert.That(buildingManager.TotalProductivity, Is.EqualTo(new ExpantaNum(4)));
        Assert.That(buildingManager.UsedProductivity, Is.EqualTo(new ExpantaNum(6)));
        Assert.That(buildingManager.AvailableProductivity, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(
            buildingManager.TryBuild(candidate, ExpantaNum.One, out BuildFailure failure),
            Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.ProductivityInsufficient));
        Assert.That(existingState.Amount, Is.EqualTo(ExpantaNum.One));
    }

    [Test]
    public void HousingAddsCapacityWithoutAddingProductivity()
    {
        GameManager gameManager = CreateManager<GameManager>("Housing-GameManager");
        BuildingManager buildingManager =
            CreateManager<BuildingManager>("Housing-BuildingManager");
        InvokeGameStateMethod(
            gameManager.State,
            "RestorePopulation",
            new ExpantaNum(5));
        Building house = CreateEconomyBuilding(
            "TestHouse",
            productivityConsumption: 0,
            productivityGranted: 0,
            populationCapacity: 5);

        Assert.That(buildingManager.TryBuild(house, ExpantaNum.One, out _), Is.True);
        Assert.That(gameManager.State.Population.PopulationCapacity, Is.EqualTo(new ExpantaNum(5)));
        Assert.That(buildingManager.TotalProductivity, Is.EqualTo(new ExpantaNum(10)));
    }

    [Test]
    public void HousingUpgrade_UsesMaterialDifferenceAndAppliesCapacityNetOnce()
    {
        GameManager gameManager =
            CreateManager<GameManager>("HousingUpgrade-GameManager");
        BuildingManager buildingManager =
            CreateManager<BuildingManager>("HousingUpgrade-BuildingManager");
        ResearchManager researchManager =
            CreateManager<ResearchManager>("HousingUpgrade-ResearchManager");
        if (researchManager.TotalResearchCount == 0)
        {
            typeof(ResearchManager).GetMethod(
                    "Initialize",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(researchManager, null);
        }
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("HousingUpgrade-ResourceManager");
        Building woodHouse = DataBase<Building>.Find("WoodHouse");
        Building stoneHouse = DataBase<Building>.Find("StoneHouse");
        Research permanentArchitecture =
            DataBase<Research>.Find("PermanentArchitecture");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        Resource stoneBrick = DataBase<Resource>.Find("StoneBrick");
        Resource clay = DataBase<Resource>.Find("Clay");

        resourceManager.SetAmount(wood, new ExpantaNum(1000));
        resourceManager.SetAmount(stoneBrick, new ExpantaNum(1000));
        resourceManager.SetAmount(clay, new ExpantaNum(1000));
        Assert.That(
            buildingManager.TryBuild(woodHouse, ExpantaNum.One, out _),
            Is.True);
        Assert.That(
            gameManager.State.Population.PopulationCapacity,
            Is.EqualTo(new ExpantaNum(5)));

        InvokeGameStateMethod(
            gameManager.State,
            "AdvanceTechLevel",
            TechLevel.Neolithic);
        ResearchState architectureState =
            researchManager.GetState(permanentArchitecture);
        typeof(ResearchState).GetMethod(
                "Restore",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(ExpantaNum), typeof(bool), typeof(bool) },
                null)
            .Invoke(
                architectureState,
                new object[] { ExpantaNum.Zero, true, true });

        Assert.That(buildingManager.CanConstructNew(woodHouse), Is.False);
        Assert.That(buildingManager.CanConstructNew(stoneHouse), Is.True);
        Assert.That(
            buildingManager.ShouldDisplay(woodHouse),
            Is.True,
            "A lower-tier card with a non-zero amount must remain visible after its upgrade is unlocked.");
        Assert.That(
            buildingManager.TryBuild(
                woodHouse,
                ExpantaNum.One,
                out BuildFailure supersededFailure),
            Is.False);
        Assert.That(
            supersededFailure,
            Is.EqualTo(BuildFailure.BuildingTierSuperseded));

        var quote = new List<Pair<Resource, ExpantaNum>>();
        buildingManager.GetUpgradeResourceDeltas(
            woodHouse,
            ExpantaNum.One,
            quote);
        Assert.That(
            quote.Find(pair => pair.First == wood).Second.ToDouble(),
            Is.EqualTo(-4d).Within(0.000001d));
        Assert.That(
            quote.Find(pair => pair.First == stoneBrick).Second.ToDouble(),
            Is.EqualTo(160d).Within(0.000001d));
        Assert.That(
            quote.Find(pair => pair.First == clay).Second.ToDouble(),
            Is.EqualTo(80d).Within(0.000001d));

        Assert.That(
            buildingManager.TryUpgrade(
                woodHouse,
                ExpantaNum.One,
                out BuildFailure failure),
            Is.True);
        Assert.That(failure, Is.EqualTo(BuildFailure.None));
        Assert.That(buildingManager.GetState(woodHouse).Amount, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(buildingManager.GetState(stoneHouse).Amount, Is.EqualTo(ExpantaNum.One));
        Assert.That(
            gameManager.State.Population.PopulationCapacity,
            Is.EqualTo(new ExpantaNum(14)));
        Assert.That(gameManager.State.TerritoryUsed, Is.EqualTo(new ExpantaNum(3)));
        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(new ExpantaNum(924)));
        Assert.That(resourceManager.GetAmount(stoneBrick), Is.EqualTo(new ExpantaNum(840)));
        Assert.That(resourceManager.GetAmount(clay), Is.EqualTo(new ExpantaNum(920)));
        Assert.That(buildingManager.ShouldDisplay(woodHouse), Is.False);
        Assert.That(buildingManager.ShouldDisplay(stoneHouse), Is.True);
    }

    [Test]
    public void HousingRemoval_AllowsOvercapacityWithoutCapacityRatchet()
    {
        GameManager gameManager = CreateManager<GameManager>("HousingRatchet-GameManager");
        BuildingManager buildingManager =
            CreateManager<BuildingManager>("HousingRatchet-BuildingManager");
        Building baseHousing = CreateEconomyBuilding(
            "BaseHousing",
            productivityConsumption: 0,
            productivityGranted: 0,
            populationCapacity: 9);
        Building removableHousing = CreateEconomyBuilding(
            "RemovableHousing",
            productivityConsumption: 0,
            productivityGranted: 0,
            populationCapacity: 5);

        Assert.That(buildingManager.TryBuild(baseHousing, ExpantaNum.One, out _), Is.True);
        Assert.That(buildingManager.TryBuild(removableHousing, ExpantaNum.One, out _), Is.True);
        InvokeGameStateMethod(gameManager.State, "RestorePopulation", new ExpantaNum(14));

        Assert.That(
            buildingManager.TryDeconstruct(removableHousing, ExpantaNum.One, out _),
            Is.True);
        Assert.That(gameManager.State.Population.Population, Is.EqualTo(new ExpantaNum(14)));
        Assert.That(
            gameManager.State.Population.PopulationCapacity,
            Is.EqualTo(new ExpantaNum(9)));
        Assert.That(buildingManager.TotalProductivity, Is.EqualTo(new ExpantaNum(28)));
        gameManager.Tick(1d, buildingManager.SafePopulationDepartureAllowance);
        Assert.That(gameManager.State.FoodAmount.ToDouble(), Is.EqualTo(293.8d).Within(0.000001d));
        Assert.That(gameManager.State.Population.Population, Is.EqualTo(new ExpantaNum(14)));

        Assert.That(buildingManager.TryBuild(removableHousing, ExpantaNum.One, out _), Is.True);
        Assert.That(
            gameManager.State.Population.PopulationCapacity,
            Is.EqualTo(new ExpantaNum(14)));
        Assert.That(
            buildingManager.TryDeconstruct(removableHousing, ExpantaNum.One, out _),
            Is.True);
        Assert.That(buildingManager.TryBuild(removableHousing, ExpantaNum.One, out _), Is.True);
        Assert.That(gameManager.State.Population.Population, Is.EqualTo(new ExpantaNum(14)));
        Assert.That(
            gameManager.State.Population.PopulationCapacity,
            Is.EqualTo(new ExpantaNum(14)));
    }

    [Test]
    public void OvercapacityDeparture_StopsAtProductivitySafetyLineAndRestartsFresh()
    {
        GameManager gameManager = CreateManager<GameManager>("Departure-GameManager");
        BuildingManager buildingManager =
            CreateManager<BuildingManager>("Departure-BuildingManager");
        InvokeGameStateMethod(gameManager.State, "RestorePopulation", new ExpantaNum(14));
        InvokeGameStateMethod(
            gameManager.State,
            "AdjustPopulationCapacity",
            new ExpantaNum(9));
        Building consumer = CreateEconomyBuilding(
            "DepartureConsumer",
            productivityConsumption: 22,
            productivityGranted: 0,
            populationCapacity: 0);
        buildingManager.EnsureBuilding(consumer).SetAmountForEditor(ExpantaNum.One);

        Assert.That(
            buildingManager.SafePopulationDepartureAllowance,
            Is.EqualTo(new ExpantaNum(2)));
        gameManager.Tick(120d, buildingManager.SafePopulationDepartureAllowance);

        Assert.That(gameManager.State.Population.Population, Is.EqualTo(new ExpantaNum(12)));
        Assert.That(buildingManager.TotalProductivity, Is.EqualTo(new ExpantaNum(24)));
        Assert.That(buildingManager.UsedProductivity, Is.EqualTo(new ExpantaNum(22)));
        Assert.That(buildingManager.AvailableProductivity, Is.EqualTo(new ExpantaNum(2)));
        Assert.That(
            gameManager.State.Population.PopulationChangeProgress,
            Is.EqualTo(ExpantaNum.Zero));

        gameManager.Tick(60d, buildingManager.SafePopulationDepartureAllowance);
        Assert.That(gameManager.State.Population.Population, Is.EqualTo(new ExpantaNum(12)));
        Assert.That(
            buildingManager.TryDeconstruct(consumer, ExpantaNum.One, out _),
            Is.True);
        gameManager.Tick(59.9d, buildingManager.SafePopulationDepartureAllowance);
        Assert.That(gameManager.State.Population.Population, Is.EqualTo(new ExpantaNum(11)));
        gameManager.Tick(0.1d, buildingManager.SafePopulationDepartureAllowance);
        Assert.That(gameManager.State.Population.Population, Is.EqualTo(new ExpantaNum(11)));
    }

    [Test]
    public void PopulationChange_LargeTicksAndRegimeChangesAreDeterministic()
    {
        PopulationState largeTick = new PopulationState();
        InvokePopulationMethod(largeTick, "AdjustPopulationCapacity", new ExpantaNum(5));
        InvokePopulationMethod(
            largeTick,
            "AdvancePopulation",
            180d,
            ExpantaNum.One,
            ExpantaNum.Zero);

        PopulationState smallTicks = new PopulationState();
        InvokePopulationMethod(smallTicks, "AdjustPopulationCapacity", new ExpantaNum(5));
        for (int i = 0; i < 1800; i++)
        {
            InvokePopulationMethod(
                smallTicks,
                "AdvancePopulation",
                0.1d,
                ExpantaNum.One,
                ExpantaNum.Zero);
        }

        Assert.That(largeTick.Population, Is.EqualTo(new ExpantaNum(2)));
        Assert.That(smallTicks.Population, Is.EqualTo(largeTick.Population));
        Assert.That(
            smallTicks.PopulationChangeProgress.ToDouble(),
            Is.EqualTo(largeTick.PopulationChangeProgress.ToDouble())
                .Within(0.001d));

        PopulationState thirtyFps = AdvancePopulationAtFixedStep(30, 60d);
        PopulationState sixtyFps = AdvancePopulationAtFixedStep(60, 60d);
        Assert.That(thirtyFps.Population, Is.EqualTo(new ExpantaNum(1)));
        Assert.That(sixtyFps.Population, Is.EqualTo(thirtyFps.Population));
        Assert.That(
            sixtyFps.PopulationChangeProgress.ToDouble(),
            Is.EqualTo(thirtyFps.PopulationChangeProgress.ToDouble())
                .Within(0.000001d));

        InvokePopulationMethod(
            largeTick,
            "AdvancePopulation",
            30d,
            ExpantaNum.One,
            ExpantaNum.Zero);
        Assert.That(
            largeTick.PopulationChangeProgress,
            Is.EqualTo(new ExpantaNum(0.1d)));
        InvokePopulationMethod(
            largeTick,
            "AdjustPopulationCapacity",
            new ExpantaNum(-5));
        Assert.That(
            largeTick.PopulationChangeProgress,
            Is.EqualTo(ExpantaNum.Zero));
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
                TechLevel = TechLevel.Animal,
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
                         Amount = "2"
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

        StringAssert.Contains("\"Version\":4", json);
        StringAssert.Contains("\"ResourceId\":\"WoodLog\"", json);
        StringAssert.DoesNotContain("AssignedMilitary", json);
        StringAssert.DoesNotContain("FoodPerPerson", json);
        StringAssert.DoesNotContain("ProductionRate", json);
        StringAssert.DoesNotContain("ConsumptionRate", json);
        StringAssert.DoesNotContain("FoodProductionRate", json);
        StringAssert.DoesNotContain("FoodConsumptionRate", json);
        StringAssert.DoesNotContain("fileID", json);
    }

    [Test]
    public void SaveApply_RejectsUnsupportedSchema()
    {
        SaveManager saveManager = CreateManager<SaveManager>("Save-Version-SaveManager");
        var data = new SaveManager.KingdomSaveData
        {
            Version = SaveFormat.CurrentVersion - 2
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidDataException>());
        StringAssert.Contains("not current", exception.InnerException.Message);
    }

    [Test]
    public void SaveApply_IsIdempotentAndRecalculatesDerivedRates()
    {
        CreateManager<GameManager>("Save-GameManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("Save-ResourceManager");
        BuildingManager buildingManager = CreateManager<BuildingManager>("Save-BuildingManager");
        CreateManager<ResearchManager>("Save-ResearchManager");
        CreateManager<WorkshopManager>("Save-WorkshopManager");
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
        Assert.That(GameManager.Instance.State.FoodProductionRate, Is.EqualTo(new ExpantaNum(21)));
        Assert.That(GameManager.Instance.State.Population.Population, Is.EqualTo(new ExpantaNum(17)));
        Assert.That(GameManager.Instance.State.Population.PopulationCapacity, Is.EqualTo(new ExpantaNum(10)));
        Assert.That(
            GameManager.Instance.State.Population.PopulationChangeProgress,
            Is.EqualTo(new ExpantaNum(0.25d)));
        Assert.That(GameManager.Instance.State.TerritoryTotal, Is.EqualTo(new ExpantaNum(520)));
        Assert.That(GameManager.Instance.State.TerritoryUsed, Is.EqualTo(new ExpantaNum(14)));
    }

    [Test]
    public void SaveApply_UnknownSelectedResearchIdIsActionable()
    {
        CreateManager<GameManager>("Save-Invalid-GameManager");
        CreateManager<ResourceManager>("Save-Invalid-ResourceManager");
        CreateManager<BuildingManager>("Save-Invalid-BuildingManager");
        CreateManager<ResearchManager>("Save-Invalid-ResearchManager");
        CreateManager<WorkshopManager>("Save-Invalid-WorkshopManager");
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
                TechLevel = TechLevel.Animal,
                FoodAmount = "10000",
                Population = "17",
                PopulationChangeProgress = "0.25",
                TerritoryTotal = "520",
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
                         Amount = "2"
                    },
                    new SaveManager.BuildingStateSaveData
                    {
                         BuildingId = "WoodHouse",
                         Amount = "2"
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

    private static void InvokeGameStateMethod(GameState state, string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(GameState).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(state, arguments);
    }

    private static void InvokePopulationMethod(PopulationState state, string methodName, params object[] arguments)
    {
        if (methodName == "AdvancePopulation" && arguments.Length == 3)
        {
            arguments = new[]
            {
                arguments[0],
                arguments[1],
                (object)PopulationState.BaseGrowthRatePerSecond,
                arguments[2]
            };
        }
        MethodInfo method = typeof(PopulationState).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(state, arguments);
    }

    private static PopulationState AdvancePopulationAtFixedStep(
        int framesPerSecond,
        double durationSeconds)
    {
        var state = new PopulationState();
        InvokePopulationMethod(
            state,
            "AdjustPopulationCapacity",
            new ExpantaNum(5));
        MethodInfo method = typeof(PopulationState).GetMethod(
            "AdvancePopulation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        int steps = (int)Math.Round(durationSeconds * framesPerSecond);
        double deltaSeconds = 1d / framesPerSecond;
        for (int i = 0; i < steps; i++)
        {
            method.Invoke(
                state,
                new object[]
                {
                    deltaSeconds,
                    ExpantaNum.One,
                    PopulationState.BaseGrowthRatePerSecond,
                    ExpantaNum.Zero
                });
        }
        return state;
    }

    private static void InvokeMilitaryMethod(MilitaryState state, string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(MilitaryState).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(state, arguments);
    }

    private static void InvokeTerritoryMethod(TerritoryState state, string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(TerritoryState).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(state, arguments);
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
        Assert.That(state.CostPaid, Is.True);
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
        Assert.That(ResearchManager.ResearchSpeedEffect(TechLevel.Animal, TechLevel.Animal), Is.EqualTo(1d));
        Assert.That(
            ResearchManager.ResearchSpeedEffect(TechLevel.Animal, TechLevel.Medieval),
            Is.EqualTo(1d / 2.5d).Within(1e-12));
    }

    [Test]
    public void CampaignProgress_UsesDeterministicRatioBandsAndSoftcap()
    {
        Assert.That(CampaignManager.CalculateProgressRate(new ExpantaNum(0.69d)), Is.EqualTo(ExpantaNum.Zero));
        Assert.That(CampaignManager.CalculateProgressRate(new ExpantaNum(0.85d)).ToDouble(), Is.EqualTo(0.125d).Within(0.000001d));
        Assert.That(CampaignManager.CalculateProgressRate(ExpantaNum.One).ToDouble(), Is.EqualTo(0.25d).Within(0.000001d));
        Assert.That(CampaignManager.CalculateProgressRate(new ExpantaNum(2d)), Is.EqualTo(ExpantaNum.One));
        Assert.That(CampaignManager.CalculateProgressRate(new ExpantaNum(100d)) < new ExpantaNum(2d), Is.True);
    }

    [Test]
    public void CampaignProgress_ClampsAndRejectsNegativeElapsedTime()
    {
        Assert.That(
            CampaignManager.AdvanceProgress(new ExpantaNum(0.99d), new ExpantaNum(2d), 60d),
            Is.EqualTo(ExpantaNum.One));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CampaignManager.AdvanceProgress(ExpantaNum.Zero, ExpantaNum.One, -0.1d));
    }

    [Test]
    public void CampaignPower_RequiresBothPowerAndLogisticsFlow()
    {
        ExpantaNum full = CampaignManager.CalculateEffectivePower(
            new ExpantaNum(10),
            new ExpantaNum(10),
            new ExpantaNum(20),
            ExpantaNum.One,
            ExpantaNum.One,
            ExpantaNum.One,
            ExpantaNum.One);
        ExpantaNum noPower = CampaignManager.CalculateEffectivePower(
            new ExpantaNum(10),
            new ExpantaNum(10),
            new ExpantaNum(20),
            ExpantaNum.One,
            ExpantaNum.Zero,
            ExpantaNum.One,
            ExpantaNum.One);
        ExpantaNum noLogistics = CampaignManager.CalculateEffectivePower(
            new ExpantaNum(10),
            new ExpantaNum(10),
            new ExpantaNum(20),
            ExpantaNum.One,
            ExpantaNum.One,
            ExpantaNum.Zero,
            ExpantaNum.One);

        Assert.That(full, Is.EqualTo(new ExpantaNum(20)));
        Assert.That(noPower, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(noLogistics, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void MilitaryState_ClampsSupplyAndNonNegativePower()
    {
        var military = new MilitaryState();
        InvokeMilitaryMethod(military, "AdjustAttackPower", new ExpantaNum(10));
        InvokeMilitaryMethod(military, "AdjustAttackPower", new ExpantaNum(-25));
        InvokeMilitaryMethod(military, "SetSupplySatisfaction", new ExpantaNum(2));

        Assert.That(military.AttackPower, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(military.SupplySatisfaction, Is.EqualTo(ExpantaNum.One));
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
    public void ResearchCostPayment_WithNoRequirementsStartsPaid()
    {
        Research research = DataBase<Research>.Find("KnowledgeSharing");
        var state = new ResearchState(research);

        Assert.That(research.ResourceRequirements, Is.Empty);
        Assert.That(research.HasPositiveResourceRequirement, Is.False);
        Assert.That(state.CostPaid, Is.True);
        Assert.That(ResearchManager.TryPayResearchCost(state), Is.True);
        Assert.That(state.CostPaid, Is.True);
    }

    [Test]
    public void ResearchAction_PaysFirstAndStartsOnlyOnSecondClick()
    {
        CreateManager<GameManager>("ResearchAction-GameManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("ResearchAction-ResourceManager");
        ResearchManager researchManager = CreateManager<ResearchManager>("ResearchAction-ResearchManager");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        Research research = DataBase<Research>.Find("ControlledFire");
        resourceManager.SetAmount(wood, 1000);

        Assert.That(
            researchManager.HandleResearchAction(research),
            Is.EqualTo(ResearchActionResult.PaidOnly));
        Assert.That(researchManager.ActiveResearch, Is.Null);
        Assert.That(researchManager.GetState(research).CostPaid, Is.True);

        Assert.That(
            researchManager.HandleResearchAction(research),
            Is.EqualTo(ResearchActionResult.Started));
        Assert.That(researchManager.ActiveResearch.Definition, Is.SameAs(research));
    }

    [Test]
    public void ResearchAction_AddsPaidResearchToQueueWhenAnotherResearchIsActive()
    {
        CreateManager<GameManager>("ResearchQueue-GameManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("ResearchQueue-ResourceManager");
        ResearchManager researchManager = CreateManager<ResearchManager>("ResearchQueue-ResearchManager");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetAmount(wood, 1000);

        Research active = DataBase<Research>.Find("Agriculture");
        Research queued = DataBase<Research>.Find("ControlledFire");
        Assert.That(researchManager.HandleResearchAction(active), Is.EqualTo(ResearchActionResult.Started));
        Assert.That(researchManager.HandleResearchAction(queued), Is.EqualTo(ResearchActionResult.PaidOnly));
        Assert.That(researchManager.HandleResearchAction(queued), Is.EqualTo(ResearchActionResult.Queued));
        Assert.That(researchManager.ResearchQueue.Select(state => state.Definition), Has.Member(queued));
        Assert.That(researchManager.ActiveResearch.Definition, Is.SameAs(active));
    }

    [Test]
    public void ResearchAction_AutoQueuesPrerequisitesInTopologicalOrder()
    {
        CreateManager<GameManager>("ResearchPrerequisite-GameManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("ResearchPrerequisite-ResourceManager");
        ResearchManager researchManager = CreateManager<ResearchManager>("ResearchPrerequisite-ResearchManager");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        Research target = DataBase<Research>.Find("StoneCutting");
        Research prerequisite = DataBase<Research>.Find("Quarry");
        resourceManager.SetAmount(wood, 1000);

        Assert.That(researchManager.HandleResearchAction(target), Is.EqualTo(ResearchActionResult.PaidOnly));
        Assert.That(researchManager.HandleResearchAction(target), Is.EqualTo(ResearchActionResult.Queued));
        Assert.That(researchManager.ActiveResearch.Definition, Is.SameAs(prerequisite));
        Assert.That(researchManager.ResearchQueue.Select(state => state.Definition), Has.Member(target));
        Assert.That(researchManager.GetState(prerequisite).CostPaid, Is.True);
    }

    [Test]
    public void ResearchAction_AutoQueuePaymentIsAtomicWhenPrerequisitesAreUnaffordable()
    {
        CreateManager<GameManager>("ResearchAtomic-GameManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("ResearchAtomic-ResourceManager");
        ResearchManager researchManager = CreateManager<ResearchManager>("ResearchAtomic-ResearchManager");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        Research target = DataBase<Research>.Find("StoneCutting");
        resourceManager.SetAmount(wood, 500);

        Assert.That(researchManager.HandleResearchAction(target), Is.EqualTo(ResearchActionResult.PaidOnly));
        resourceManager.SetAmount(wood, 0);
        Assert.That(
            researchManager.HandleResearchAction(target),
            Is.EqualTo(ResearchActionResult.InsufficientResources));
        Assert.That(researchManager.ActiveResearch, Is.Null);
        Assert.That(researchManager.ResearchQueue, Is.Empty);
        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void Agriculture_WithNoResourceRequirementsCanStartImmediately()
    {
        CreateManager<GameManager>("Agriculture-GameManager");
        ResearchManager researchManager =
            CreateManager<ResearchManager>("Agriculture-ResearchManager");
        Research agriculture = DataBase<Research>.Find("Agriculture");
        ResearchState state = researchManager.GetState(agriculture);

        Assert.That(agriculture.ResourceRequirements, Is.Empty);
        Assert.That(agriculture.HasPositiveResourceRequirement, Is.False);
        Assert.That(state.Status, Is.EqualTo(ResearchStatus.Available));
        Assert.That(state.CostPaid, Is.True);
        Assert.That(researchManager.StartResearch(agriculture), Is.True);
        Assert.That(researchManager.ActiveResearch, Is.SameAs(state));
        Assert.That(state.Status, Is.EqualTo(ResearchStatus.Researching));
    }

    [Test]
    public void DataBase_FindsDefinitionsByStableId()
    {
        Resource wood = DataBase<Resource>.Find("WoodLog");
        Building farm = DataBase<Building>.Find("Farm");
        Research agriculture = DataBase<Research>.Find("Agriculture");

        Assert.That(wood.Id, Is.EqualTo("WoodLog"));
        Assert.That(farm.Id, Is.EqualTo("Farm"));
        Assert.That(agriculture.Id, Is.EqualTo("Agriculture"));
        Assert.That(DataBase<Resource>.Find("woodlog"), Is.SameAs(wood));
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
        Assert.That(farm.ResourceRequirements[0].Second, Is.EqualTo(new ExpantaNum(50)));
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
        Assert.That(state.SpaceCost, Is.EqualTo(new ExpantaNum(4)));
        Assert.That(state.ProductivityConsumption, Is.EqualTo(new ExpantaNum(3)));
        Assert.That(state.ProductivityGranted, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void BuildingDefinitions_SplitCostsFromGrantedProductionAndFood()
    {
        Building farm = DataBase<Building>.Find("Farm");
        Building woodHouse = DataBase<Building>.Find("WoodHouse");

        Assert.That(woodHouse.ProductivityConsumption, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(woodHouse.ProductivityGranted, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(woodHouse.PopulationCapacityGranted, Is.EqualTo(new ExpantaNum(5)));
        Assert.That(farm.FoodProductionRate, Is.EqualTo(new ExpantaNum(8)));
        Assert.That(farm.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void BuildingDisplayer_UsesHeaderHeightWhenCollapsedAndFullExpandedHeight()
    {
        Assert.That(
            BuildingDisplayer.CalculatePreferredHeight(false, 0),
            Is.EqualTo(BuildingDisplayer.HeaderHeight));
        Assert.That(
            BuildingDisplayer.CalculatePreferredHeight(false, 5),
            Is.EqualTo(BuildingDisplayer.HeaderHeight));
        Assert.That(
            BuildingDisplayer.CalculatePreferredHeight(true, 1),
            Is.EqualTo(
                BuildingDisplayer.HeaderHeight +
                BuildingDisplayer.DetailRowHeight +
                BuildingDisplayer.ActionRowHeight));
        Assert.That(
            BuildingDisplayer.CalculatePreferredHeight(true, 3),
            Is.EqualTo(
                BuildingDisplayer.HeaderHeight +
                BuildingDisplayer.DetailRowHeight * 2f +
                BuildingDisplayer.ActionRowHeight));
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
    public void BuildingManager_RejectsBuildingsAboveCurrentTechLevel()
    {
        CreateManager<GameManager>("Tech-Gate-GameManager");
        BuildingManager buildingManager = CreateManager<BuildingManager>("Tech-Gate-BuildingManager");
        Building medievalBuilding = ScriptableObject.CreateInstance<Building>();
        medievalBuilding.TechLevel = TechLevel.Medieval;
        createdObjects.Add(medievalBuilding);

        bool built = buildingManager.TryBuild(
            medievalBuilding,
            ExpantaNum.One,
            out BuildFailure failure);

        Assert.That(built, Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.TechnologyInsufficient));
        Assert.That(buildingManager.States.ContainsKey(medievalBuilding), Is.False);
        Assert.That(
            buildingManager.GetMaxBuildable(medievalBuilding, ExpantaNum.One),
            Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void ResourceViewer_BindsExistingStatesWhenOpenedAfterResourcesWereDiscovered()
    {
        CreateManager<GameManager>("ResourceViewer-GameManager");
        CreateManager<BuildingManager>("ResourceViewer-BuildingManager");
        CreateManager<ResearchManager>("ResourceViewer-ResearchManager");
        CreateManager<WorkshopManager>("ResourceViewer-WorkshopManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("ResourceManager");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.EnsureResource(wood);

        GameObject setObject = new GameObject(
            wood.DisplayerSet.ToString(),
            typeof(RectTransform),
            typeof(Image),
            typeof(ResourceDisplayerSet));
        createdObjects.Add(setObject);
        ResourceDisplayerSet set = setObject.GetComponent<ResourceDisplayerSet>();
        set.Content = new GameObject("Content", typeof(RectTransform)).transform;
        createdObjects.Add(set.Content.gameObject);
        set.Content.SetParent(setObject.transform, false);

        GameObject viewerObject = new GameObject("ResourceViewer", typeof(RectTransform));
        createdObjects.Add(viewerObject);
        viewerObject.SetActive(false);
        setObject.transform.SetParent(viewerObject.transform, false);
        ResourceViewer viewer = viewerObject.AddComponent<ResourceViewer>();
        GameObject displayerPrefab =
            Resources.Load<GameObject>("UI/Resource/ResourceDisplayer");
        Assert.That(displayerPrefab, Is.Not.Null);
        typeof(ResourceViewer)
            .GetField("DisplayerPrefab", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(viewer, displayerPrefab);
        viewerObject.SetActive(true);
        typeof(ResourceViewer)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(viewer, null);
        viewer.RefreshAll();

        Assert.That(set.Displayers.ContainsKey(wood), Is.True);
    }

    [Test]
    public void ResourceViewer_DoesNotCreateDisplayerForLockedIndustrialOutput()
    {
        CreateManager<GameManager>("ResourceViewer-Locked-GameManager");
        CreateManager<BuildingManager>("ResourceViewer-Locked-BuildingManager");
        CreateManager<ResearchManager>("ResourceViewer-Locked-ResearchManager");
        CreateManager<WorkshopManager>("ResourceViewer-Locked-WorkshopManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("ResourceViewer-Locked-ResourceManager");
        Resource electronics = DataBase<Resource>.Find("Electronics");
        resourceManager.EnsureResource(electronics);

        GameObject setObject = new GameObject(
            electronics.DisplayerSet.ToString(),
            typeof(RectTransform),
            typeof(Image),
            typeof(ResourceDisplayerSet));
        createdObjects.Add(setObject);
        ResourceDisplayerSet set = setObject.GetComponent<ResourceDisplayerSet>();
        set.Content = new GameObject("Content", typeof(RectTransform)).transform;
        createdObjects.Add(set.Content.gameObject);
        set.Content.SetParent(setObject.transform, false);

        GameObject viewerObject = new GameObject("ResourceViewer-Locked", typeof(RectTransform));
        createdObjects.Add(viewerObject);
        viewerObject.SetActive(false);
        setObject.transform.SetParent(viewerObject.transform, false);
        ResourceViewer viewer = viewerObject.AddComponent<ResourceViewer>();
        GameObject displayerPrefab = Resources.Load<GameObject>("UI/Resource/ResourceDisplayer");
        Assert.That(displayerPrefab, Is.Not.Null);
        typeof(ResourceViewer)
            .GetField("DisplayerPrefab", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(viewer, displayerPrefab);
        viewerObject.SetActive(true);
        typeof(ResourceViewer)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(viewer, null);
        viewer.RefreshAll();

        Assert.That(set.Displayers.ContainsKey(electronics), Is.False);
        Assert.That(setObject.activeSelf, Is.False);
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
        createdObjects.Add(research);
        return research;
    }

    private Building CreateEconomyBuilding(
        string id,
        double productivityConsumption,
        double productivityGranted,
        double populationCapacity)
    {
        Building building = ScriptableObject.CreateInstance<Building>();
        building.name = id;
        building.SetIdForEditor(id);
        building.TechLevel = TechLevel.Animal;
        building.ConfigureEconomyForEditor(
            new ExpantaNum(1.15d),
            ExpantaNum.Zero,
            new ExpantaNum(productivityConsumption),
            new ExpantaNum(productivityGranted),
            new ExpantaNum(populationCapacity),
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            new List<Pair<Resource, ExpantaNum>>(),
            new List<Pair<Resource, ExpantaNum>>(),
            new List<Pair<Resource, ExpantaNum>>());
        createdObjects.Add(building);
        return building;
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
