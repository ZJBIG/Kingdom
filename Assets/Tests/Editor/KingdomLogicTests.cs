using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class KingdomLogicTests
{
    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        var managerObjects = new HashSet<GameObject>();
        AddManagerObjects(managerObjects, UnityEngine.Object.FindObjectsOfType<GameManager>());
        AddManagerObjects(managerObjects, UnityEngine.Object.FindObjectsOfType<ResourceManager>());
        AddManagerObjects(managerObjects, UnityEngine.Object.FindObjectsOfType<BuildingManager>());
        AddManagerObjects(managerObjects, UnityEngine.Object.FindObjectsOfType<ResearchManager>());
        AddManagerObjects(managerObjects, UnityEngine.Object.FindObjectsOfType<WorkshopManager>());
        AddManagerObjects(managerObjects, UnityEngine.Object.FindObjectsOfType<SimulationManager>());
        AddManagerObjects(managerObjects, UnityEngine.Object.FindObjectsOfType<SaveManager>());
        AddManagerObjects(managerObjects, UnityEngine.Object.FindObjectsOfType<TutorialManager>());

        foreach (GameObject managerObject in managerObjects)
            UnityEngine.Object.DestroyImmediate(managerObject);

        createdObjects.Clear();
        ProgressionModifierManager.Rebuild(null);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(createdObjects[i]);
        createdObjects.Clear();
        ProgressionModifierManager.Rebuild(null);
    }

    private static void AddManagerObjects<T>(HashSet<GameObject> managerObjects, T[] managers)
        where T : Component
    {
        for (int i = 0; i < managers.Length; i++)
            if (managers[i] != null)
                managerObjects.Add(managers[i].gameObject);
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
    public void GameSave_RoundTripsSubDayCalendarAccumulator()
    {
        GameManager manager = CreateManager<GameManager>("CalendarAccumulator-GameManager");
        FieldInfo accumulator = typeof(GameManager).GetField(
            "calendarElapsedSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo capture = typeof(GameManager).GetMethod(
            "CaptureSaveData", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo restore = typeof(GameManager).GetMethod(
            "RestoreSaveData", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(accumulator, Is.Not.Null);
        Assert.That(capture, Is.Not.Null);
        Assert.That(restore, Is.Not.Null);

        accumulator.SetValue(manager, 3.5d);
        SaveManager.GameSaveData data = (SaveManager.GameSaveData)capture.Invoke(manager, null);
        accumulator.SetValue(manager, 0d);
        restore.Invoke(manager, new object[] { data });

        Assert.That((double)accumulator.GetValue(manager), Is.EqualTo(3.5d).Within(1e-9d));
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
    public void GameState_RestoresPopulationCapacityAndProgressExactly()
    {
        var state = new GameState();
        InvokeGameStateMethod(state, "AdjustPopulationCapacity", new ExpantaNum(10));
        InvokeGameStateMethod(state, "RestorePopulationChangeProgress", new ExpantaNum(0.35d));

        MethodInfo restore = typeof(GameState).GetMethod(
            "RestorePopulationCapacityExact",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(restore, Is.Not.Null);

        restore.Invoke(
            state,
            new object[] { new ExpantaNum(25), new ExpantaNum(0.6d) });

        Assert.That(state.Population.PopulationCapacity, Is.EqualTo(new ExpantaNum(25)));
        Assert.That(
            state.Population.PopulationChangeProgress,
            Is.EqualTo(new ExpantaNum(0.6d)));
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
        Assert.That(state.TechLevel, Is.EqualTo(TechLevel.Animal));
        Assert.That(state.FoodAmount, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(state.FoodCapacity, Is.GreaterThan(state.FoodAmount));
        Assert.That(state.FoodProductionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(state.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.TerritoryTotal, Is.EqualTo(new ExpantaNum(500)));
        Assert.That(state.TerritoryUsed, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.AvailableTerritory, Is.EqualTo(new ExpantaNum(500)));
        Assert.That(state.Population.Population, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.Population.PopulationCapacity, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(PopulationState.FoodConsumptionPerPerson, Is.EqualTo(new ExpantaNum(0.8d)));
        Assert.That(PopulationState.ProductivityGrantedPerPerson, Is.EqualTo(new ExpantaNum(2d)));
    }

    [Test]
    public void ResourceState_ExistsIndependentlyFromResourceUI()
    {
        Resource resource = ScriptableObject.CreateInstance<Resource>();
        resource.name = "TestResource";
        createdObjects.Add(resource);

        var state = new ResourceState(resource);

        Assert.That(state.Definition, Is.SameAs(resource));
        Assert.That(state.Amount, Is.LessThanOrEqualTo(ExpantaNum.Zero));
        Assert.That(state.ProductionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.ConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.Efficiency, Is.EqualTo(ExpantaNum.One));
    }

    [Test]
    public void 普通资源不应拥有独立容量上限()
    {
        string[] resourceStateMembers = typeof(ResourceState)
            .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(member => member.Name)
            .ToArray();
        string[] resourceMembers = typeof(Resource)
            .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(member => member.Name)
            .ToArray();

        Assert.That(resourceStateMembers.Any(name =>
            name.IndexOf("Capacity", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("MaxAmount", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        Assert.That(resourceMembers.Any(name =>
            name.IndexOf("Capacity", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("MaxAmount", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
    }

    [Test]
    public void 普通资源库存可以超过粮食容量()
    {
        Resource resource = DataBase<Resource>.Find("WoodLog");
        Assert.That(resource, Is.Not.Null);

        GameObject resourceObject = new GameObject("Uncapped-Resource-Manager");
        ResourceManager resourceManager = resourceObject.AddComponent<ResourceManager>();
        ExpantaNum amount = GameState.BaseFoodCapacity + new ExpantaNum(1000d);
        resourceManager.SetAmount(resource, amount);

        Assert.That(resourceManager.GetAmount(resource), Is.Not.LessThan(amount));
        Assert.That(resourceManager.GetAmount(resource), Is.Not.GreaterThan(amount));
        UnityEngine.Object.DestroyImmediate(resourceObject);
    }

    [Test]
    public void ResourceManager_AlwaysCreatesStartingWoodStateAndProduction()
    {
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("Starting-Wood-ResourceManager");
        Resource wood = DataBase<Resource>.Find(ResourceManager.StartingResourceId);

        Assert.That(resourceManager.States.ContainsKey(wood), Is.True);
        Assert.That(resourceManager.GetState(wood).ProductionRate, Is.GreaterThan(ExpantaNum.Zero));
    }

    [Test]
    public void NewGame_StartsWith60WoodLog_AndDoesNotStackOnRepeatInitialization()
    {
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("Starting-Inventory-ResourceManager");
        GameManager gameManager =
            CreateManager<GameManager>("Starting-Inventory-GameManager");
        MethodInfo initializeNewGame = typeof(GameManager).GetMethod(
            "InitializeNewGame", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(initializeNewGame, Is.Not.Null);

        initializeNewGame.Invoke(gameManager, null);
        Resource wood = DataBase<Resource>.Find(ResourceManager.StartingResourceId);
        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(new ExpantaNum(60)));
        initializeNewGame.Invoke(gameManager, null);
        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(new ExpantaNum(60)));
    }

    [TestCase("0")]
    [TestCase("13")]
    public void SaveApply_PreservesExistingWoodLogAmount(string amount)
    {
        CreateManager<GameManager>("Save-Wood-Compatibility-GameManager");
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("Save-Wood-Compatibility-ResourceManager");
        CreateManager<BuildingManager>("Save-Wood-Compatibility-BuildingManager");
        CreateManager<ResearchManager>("Save-Wood-Compatibility-ResearchManager");
        CreateManager<WorkshopManager>("Save-Wood-Compatibility-WorkshopManager");
        SaveManager saveManager =
            CreateManager<SaveManager>("Save-Wood-Compatibility-SaveManager");

        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Resources.Resources[0].Amount = amount;

        InvokeApplySaveData(saveManager, data);

        Resource wood = DataBase<Resource>.Find(ResourceManager.StartingResourceId);
        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(new ExpantaNum(amount)));
    }

    [TestCase("0")]
    [TestCase("13")]
    public void SaveLoad_ExistingWoodLogAmountDoesNotReceiveNewGameGift(string amount)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "KingdomSaveCompatibilityTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            SaveManager.SetSaveRootOverrideForTests(root);
            CreateManager<GameManager>("Save-Compatibility-GameManager");
            ResourceManager resourceManager =
                CreateManager<ResourceManager>("Save-Compatibility-ResourceManager");
            CreateManager<BuildingManager>("Save-Compatibility-BuildingManager");
            CreateManager<ResearchManager>("Save-Compatibility-ResearchManager");
            CreateManager<WorkshopManager>("Save-Compatibility-WorkshopManager");
            SaveManager saveManager =
                CreateManager<SaveManager>("Save-Compatibility-SaveManager");

            Assert.That(saveManager.LoadOrCreateGame(), Is.False);
            Resource wood = DataBase<Resource>.Find(ResourceManager.StartingResourceId);
            resourceManager.SetAmount(wood, new ExpantaNum(amount));
            Assert.That(saveManager.SaveNow(true), Is.True);

            resourceManager.SetAmount(wood, new ExpantaNum(91));
            Assert.That(saveManager.LoadOrCreateGame(), Is.True);
            Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(new ExpantaNum(amount)));
        }
        finally
        {
            SaveManager.ClearSaveRootOverrideForTests();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public void SaveLoad_RecoversFromCorruptPrimaryUsingIsolatedBackup()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "KingdomSaveTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            SaveManager.SetSaveRootOverrideForTests(root);
            CreateManager<GameManager>("Save-Backup-GameManager");
            CreateManager<ResourceManager>("Save-Backup-ResourceManager");
            CreateManager<BuildingManager>("Save-Backup-BuildingManager");
            CreateManager<ResearchManager>("Save-Backup-ResearchManager");
            CreateManager<WorkshopManager>("Save-Backup-WorkshopManager");
            SaveManager saveManager = CreateManager<SaveManager>("Save-Backup-SaveManager");

            Assert.That(saveManager.LoadOrCreateGame(), Is.False);
            Assert.That(saveManager.SaveNow(true), Is.True);
            Resource wood = DataBase<Resource>.Find(ResourceManager.StartingResourceId);
            ResourceManager.Instance.SetAmount(wood, new ExpantaNum(91));
            Assert.That(saveManager.SaveNow(true), Is.True);

            string primaryPath = Path.Combine(root, "KingdomSave.json");
            string backupPath = primaryPath + ".bak";
            Assert.That(File.Exists(backupPath), Is.True);
            File.WriteAllText(primaryPath, "{ invalid json");

            Assert.That(saveManager.LoadOrCreateGame(), Is.True);
            Assert.That(ResourceManager.Instance.GetAmount(wood), Is.EqualTo(new ExpantaNum(60)));
        }
        finally
        {
            SaveManager.ClearSaveRootOverrideForTests();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public void SaveApply_RoundTripsAllRuntimeSectionsWithActiveResearchAndSectorState()
    {
        CreateManager<GameManager>("Save-FullRoundTrip-GameManager");
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("Save-FullRoundTrip-ResourceManager");
        CreateManager<BuildingManager>("Save-FullRoundTrip-BuildingManager");
        ResearchManager researchManager =
            CreateManager<ResearchManager>("Save-FullRoundTrip-ResearchManager");
        CreateManager<WorkshopManager>("Save-FullRoundTrip-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-FullRoundTrip-SaveManager");

        Research target = DataBase<Research>.Find("Quarry");
        var paid = new List<SaveManager.ResearchResourceCostSaveData>();
        for (int i = 0; i < target.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = target.ResourceRequirements[i];
            if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                continue;
            paid.Add(new SaveManager.ResearchResourceCostSaveData
            {
                ResourceId = requirement.First.Id,
                Amount = requirement.Second.ToString()
            });
        }

        SectorDefinition sector = DataBase<SectorDefinition>.All[0];
        SaveManager.KingdomSaveData source = CreateRepresentativeSaveData();
        source.General.CalendarElapsedSeconds = 12.5d;
        source.General.AttackPower = "21";
        source.General.DefensePower = "34";
        source.General.FleetPower = "55";
        source.General.MilitaryManpower = "8";
        source.General.SupplySatisfaction = "0.75";
        source.General.PowerSatisfaction = "0.8";
        source.General.LogisticsSatisfaction = "0.9";
        source.Researches.ActiveResearchId = target.Id;
        source.Researches.States = new List<SaveManager.ResearchStateSaveData>
        {
            new SaveManager.ResearchStateSaveData
            {
                ResearchId = target.Id,
                Progress = "0.4",
                CostPaid = true,
                Completed = false,
                PaidResourceCosts = paid
            }
        };
        source.Workshop = new SaveManager.WorkshopSaveData
        {
            PurchasedUpgradeIds = new List<string>()
        };
        source.Sectors = new SaveManager.SectorSaveData
        {
            States = new List<SaveManager.SectorStateSaveData>
            {
                new SaveManager.SectorStateSaveData
                {
                    SectorId = sector.Id,
                    Unlocked = true,
                    Occupied = false,
                    ColonizationActive = false,
                    CampaignActive = false,
                    CampaignProgress = "0",
                    CampaignCasualties = "0",
                    CampaignCombatRatio = "0",
                    VisitCount = 7
                }
            }
        };
        source.Tutorial = new SaveManager.TutorialSaveData
        {
            ActiveStepId = "resources",
            CompletedStepIds = new List<string> { "orientation" }
        };

        SaveManager.KingdomSaveData roundTripped =
            JsonUtility.FromJson<SaveManager.KingdomSaveData>(JsonUtility.ToJson(source));
        InvokeApplySaveData(saveManager, roundTripped);

        Assert.That(researchManager.ActiveResearch.Definition, Is.SameAs(target));
        Assert.That(researchManager.ActiveResearch.Progress, Is.EqualTo(new ExpantaNum("0.4")));
        Assert.That(GameManager.Instance.Sectors.GetState(sector).Unlocked, Is.True);
        Assert.That(GameManager.Instance.Sectors.GetState(sector).VisitCount, Is.EqualTo(7));
        Assert.That(TutorialManager.Ensure().ActiveStepId, Is.EqualTo("resources"));
        Assert.That(TutorialManager.Ensure().CompletedStepIds, Does.Contain("orientation"));
        FieldInfo accumulator = typeof(GameManager).GetField(
            "calendarElapsedSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(accumulator, Is.Not.Null);
        Assert.That((double)accumulator.GetValue(GameManager.Instance), Is.EqualTo(12.5d).Within(1e-9d));
        Assert.That(GameManager.Instance.State.AttackPower, Is.EqualTo(new ExpantaNum(21)));
        Assert.That(GameManager.Instance.State.DefensePower, Is.EqualTo(new ExpantaNum(34)));
        Assert.That(GameManager.Instance.State.FleetPower, Is.EqualTo(new ExpantaNum(55)));
        Assert.That(GameManager.Instance.State.MilitaryManpower, Is.EqualTo(new ExpantaNum(8)));
        Assert.That(GameManager.Instance.State.SupplySatisfaction, Is.EqualTo(new ExpantaNum(0.75d)));
        Assert.That(GameManager.Instance.State.PowerSatisfaction, Is.EqualTo(new ExpantaNum(0.8d)));
        Assert.That(GameManager.Instance.State.LogisticsSatisfaction, Is.EqualTo(new ExpantaNum(0.9d)));
        Assert.That(resourceManager.GetAmount(DataBase<Resource>.Find("WoodLog")), Is.EqualTo(new ExpantaNum(25)));
    }

    [Test]
    public void SaveApply_RestoresEveryTechLevelWithoutCrossEraStateLeakage()
    {
        CreateManager<GameManager>("Save-AllEras-GameManager");
        CreateManager<ResourceManager>("Save-AllEras-ResourceManager");
        CreateManager<BuildingManager>("Save-AllEras-BuildingManager");
        CreateManager<ResearchManager>("Save-AllEras-ResearchManager");
        CreateManager<WorkshopManager>("Save-AllEras-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-AllEras-SaveManager");

        foreach (TechLevel era in Enum.GetValues(typeof(TechLevel)))
        {
            SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
            data.General.TechLevel = era;
            data.General.CalendarDays = (int)era * 10;
            data.Tutorial = new SaveManager.TutorialSaveData
            {
                ActiveStepId = "missing-transient-step",
                CompletedStepIds = new List<string> { "orientation", "missing-transient-step" }
            };

            Assert.DoesNotThrow(() => InvokeApplySaveData(
                saveManager,
                JsonUtility.FromJson<SaveManager.KingdomSaveData>(JsonUtility.ToJson(data))));
            Assert.That(GameManager.Instance.State.TechLevel, Is.EqualTo(era));
            Assert.That(TutorialManager.Ensure().ActiveStepId, Is.Not.EqualTo("missing-transient-step"));
            Assert.That(TutorialManager.Ensure().CompletedStepIds, Does.Not.Contain("missing-transient-step"));
        }
    }

    [Test]
    public void SaveApply_RestoresResearchQueueAndLocalSectorCampaignTogether()
    {
        CreateManager<GameManager>("Save-QueueCampaign-GameManager");
        CreateManager<ResourceManager>("Save-QueueCampaign-ResourceManager");
        CreateManager<BuildingManager>("Save-QueueCampaign-BuildingManager");
        ResearchManager researchManager =
            CreateManager<ResearchManager>("Save-QueueCampaign-ResearchManager");
        CreateManager<WorkshopManager>("Save-QueueCampaign-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-QueueCampaign-SaveManager");

        Research active = DataBase<Research>.Find("Agriculture");
        Research queued = DataBase<Research>.Find("ControlledFire");
        var states = new List<SaveManager.ResearchStateSaveData>();
        foreach (Research research in new[] { active, queued })
        {
            var paid = new List<SaveManager.ResearchResourceCostSaveData>();
            for (int i = 0; i < research.ResourceRequirements.Count; i++)
            {
                Pair<Resource, ExpantaNum> requirement = research.ResourceRequirements[i];
                if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                    continue;
                paid.Add(new SaveManager.ResearchResourceCostSaveData
                {
                    ResourceId = requirement.First.Id,
                    Amount = requirement.Second.ToString()
                });
            }
            states.Add(new SaveManager.ResearchStateSaveData
            {
                ResearchId = research.Id,
                Progress = "0.2",
                CostPaid = true,
                Completed = false,
                PaidResourceCosts = paid
            });
        }

        SectorDefinition sector = DataBase<SectorDefinition>.All
            .FirstOrDefault(definition => definition != null && !definition.IsHomeSystem);
        Assert.That(sector, Is.Not.Null);
        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.General.TechLevel = TechLevel.Neolithic;
        data.Researches.States = states;
        data.Researches.ActiveResearchId = active.Id;
        data.Researches.QueuedResearchIds = new List<string> { queued.Id };
        data.Sectors = new SaveManager.SectorSaveData
        {
            States = new List<SaveManager.SectorStateSaveData>
            {
                new SaveManager.SectorStateSaveData
                {
                    SectorId = sector.Id,
                    Unlocked = true,
                    Occupied = false,
                    ColonizationActive = false,
                    CampaignActive = true,
                    CampaignProgress = "0.4",
                    CampaignCasualties = "2",
                    CampaignCombatRatio = "1.2",
                    VisitCount = 3
                }
            }
        };

        InvokeApplySaveData(
            saveManager,
            JsonUtility.FromJson<SaveManager.KingdomSaveData>(JsonUtility.ToJson(data)));

        Assert.That(researchManager.ActiveResearch.Definition, Is.SameAs(active));
        Assert.That(researchManager.ResearchQueue.Select(state => state.Definition),
            Has.Member(queued));
        SectorState restored = GameManager.Instance.Sectors.GetState(sector);
        Assert.That(restored.CampaignActive, Is.True);
        Assert.That(restored.CampaignProgress, Is.EqualTo(new ExpantaNum("0.4")));
        Assert.That(restored.CampaignCasualties, Is.EqualTo(new ExpantaNum("2")));
    }

    [Test]
    public void ResourceAdvance_UsesElapsedSecondsAndClampsAtZero()
    {
        Assert.That(ResourceManager.AdvanceAmount(100, 8, 3, 2), Is.EqualTo(new ExpantaNum(110)));
        Assert.That(ResourceManager.AdvanceAmount(5, 0, 10, 1), Is.EqualTo(ExpantaNum.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceManager.AdvanceAmount(0, 0, 0, -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceManager.AdvanceAmount(0, 0, 0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceManager.AdvanceAmount(0, 0, 0, double.PositiveInfinity));
    }

    [Test]
    public void ResourceDerivedRateDelta_SnapsOnlyCancellationResidueToZero()
    {
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("ResourceRate-Cancellation-Test");
        Resource wood = DataBase<Resource>.Find("WoodLog");

        resourceManager.SetProductionRate(wood, new ExpantaNum(1.000001d));
        resourceManager.AdjustProductionRate(wood, new ExpantaNum(-1d));
        Assert.That(resourceManager.GetState(wood).ProductionRate, Is.EqualTo(ExpantaNum.Zero));

        resourceManager.SetConsumptionRate(wood, new ExpantaNum(1.000001d));
        resourceManager.AdjustConsumptionRate(wood, new ExpantaNum(-1d));
        Assert.That(resourceManager.GetState(wood).ConsumptionRate, Is.EqualTo(ExpantaNum.Zero));

        resourceManager.SetProductionRate(wood, new ExpantaNum(0.000002d));
        resourceManager.AdjustProductionRate(wood, new ExpantaNum(-0.000001d));
        Assert.That(
            resourceManager.GetState(wood).ProductionRate,
            Is.EqualTo(new ExpantaNum(0.000001d)),
            "A legitimate small remaining rate must not be truncated.");

        resourceManager.SetProductionRate(wood, new ExpantaNum(0.5d));
        resourceManager.SetConsumptionRate(wood, ExpantaNum.One);
        Assert.That(
            resourceManager.GetState(wood).ProductionRate -
            resourceManager.GetState(wood).ConsumptionRate,
            Is.EqualTo(new ExpantaNum(-0.5d)),
            "A real negative net rate must remain visible.");
    }

    [Test]
    public void ResourceSatisfaction_UsesInventoryAndPotentialProductionForTheTick()
    {
        Assert.That(ResourceManager.CalculateSatisfaction(2, 0, 10, 1), Is.EqualTo(new ExpantaNum(0.2)));
        Assert.That(ResourceManager.CalculateSatisfaction(2, 3, 10, 1), Is.EqualTo(new ExpantaNum(0.5)));
        Assert.That(ResourceManager.CalculateSatisfaction(0, 0, 0, 1), Is.EqualTo(ExpantaNum.One));
        Assert.That(ResourceManager.CalculateSatisfaction(0, 0, 10, 1), Is.EqualTo(ExpantaNum.Zero));
        Assert.That(ResourceManager.CalculateSatisfaction(0, 0, 10, 0), Is.EqualTo(ExpantaNum.One));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceManager.CalculateSatisfaction(0, 0, 10, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceManager.CalculateSatisfaction(0, 0, 10, double.PositiveInfinity));
    }

    [Test]
    public void BuildingEfficiency_UsesTheLeastSatisfiedResourceInput()
    {
        CreateManager<GameManager>("MultiInput-GameManager");
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("MultiInput-ResourceManager");
        BuildingManager buildingManager =
            CreateManager<BuildingManager>("MultiInput-BuildingManager");

        Resource first = ScriptableObject.CreateInstance<Resource>();
        first.SetIdForEditor("MultiInputFirst");
        createdObjects.Add(first);
        Resource second = ScriptableObject.CreateInstance<Resource>();
        second.SetIdForEditor("MultiInputSecond");
        createdObjects.Add(second);
        Building building = ScriptableObject.CreateInstance<Building>();
        building.SetIdForEditor("MultiInputConsumer");
        building.TechLevel = TechLevel.Animal;
        building.ConfigureEconomyForEditor(
            new ExpantaNum(1.15d),
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
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            new List<Pair<Resource, ExpantaNum>>(),
            new List<Pair<Resource, ExpantaNum>>(),
            new List<Pair<Resource, ExpantaNum>>
            {
                new Pair<Resource, ExpantaNum>(first, new ExpantaNum(10)),
                new Pair<Resource, ExpantaNum>(second, new ExpantaNum(10))
            });
        createdObjects.Add(building);

        Assert.That(
            buildingManager.TryBuild(building, ExpantaNum.One, out BuildFailure failure),
            Is.True);
        Assert.That(failure, Is.EqualTo(BuildFailure.None));
        resourceManager.SetAmount(first, new ExpantaNum(8));
        resourceManager.SetAmount(second, new ExpantaNum(5));

        MethodInfo prepare = typeof(BuildingManager).GetMethod(
            "PrepareTickResourceSatisfaction",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(prepare, Is.Not.Null);
        prepare.Invoke(buildingManager, new object[] { 1d });

        Assert.That(
            buildingManager.GetState(building).Efficiency.ToDouble(),
            Is.EqualTo(0.5d).Within(0.000001d));
    }

    [Test]
    public void TopFlowDemand_UsesRawBuildingDemandWithoutEfficiency()
    {
        CreateManager<GameManager>("TopFlow-GameManager");
        BuildingManager buildingManager =
            CreateManager<BuildingManager>("TopFlow-BuildingManager");
        Building building = CreateEconomyBuilding("TopFlow-Consumer", 0d, 0d, 0d);
        building.SetPowerFlowForEditor(ExpantaNum.Zero, new ExpantaNum(7));
        building.SetLogisticsFlowForEditor(ExpantaNum.Zero, new ExpantaNum(3));

        Assert.That(buildingManager.TryBuild(building, new ExpantaNum(2), out BuildFailure failure),
            Is.True);
        Assert.That(failure, Is.EqualTo(BuildFailure.None));

        MethodInfo demand = typeof(KingdomUIRoot).GetMethod(
            "CalculateRawFlowDemand", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(demand, Is.Not.Null);
        Assert.That((ExpantaNum)demand.Invoke(null, new object[] { true }),
            Is.EqualTo(new ExpantaNum(14)));
        Assert.That((ExpantaNum)demand.Invoke(null, new object[] { false }),
            Is.EqualTo(new ExpantaNum(6)));
    }

    [Test]
    public void TopFlowValue_FormatsSignedColoredBalanceBeforeDemandAndSupply()
    {
        MethodInfo format = typeof(KingdomUIRoot).GetMethod(
            "FormatTopFlow", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(format, Is.Not.Null);

        string positive = format.Invoke(null,
            new object[] { new ExpantaNum(8), new ExpantaNum(3) }) as string;
        StringAssert.Contains("<color=#", positive);
        StringAssert.Contains("+5", positive);
        StringAssert.Contains("(3/8)", positive);

        string negative = format.Invoke(null,
            new object[] { new ExpantaNum(3), new ExpantaNum(8) }) as string;
        StringAssert.Contains("-5", negative);
        StringAssert.Contains("(8/3)", negative);
    }

    [Test]
    public void ToGameString_UsesFourSignificantDigitsAndPromotesRoundedThousands()
    {
        Assert.That(new ExpantaNum(1220).ToGameString(), Is.EqualTo("1.22K"));
        Assert.That(new ExpantaNum(999499).ToGameString(), Is.EqualTo("999.5K"));
        Assert.That(new ExpantaNum(999950).ToGameString(), Is.EqualTo("1M"));
    }

    [Test]
    public void ToGameString_UsesCompactNativeNotationForVeryLargeLayeredValues()
    {
        Assert.That(new ExpantaNum("2e10000").ToGameString(), Is.EqualTo("e10000.301"));
        Assert.That(new ExpantaNum("1e100000").ToGameString(), Is.EqualTo("ee5"));
    }

    [Test]
    public void GameTick_IntegratesFoodEveryTickAndCalendarSeparately()
    {
        GameManager gameManager = CreateManager<GameManager>("GameManager-Food-Test");
        gameManager.AdjustFoodRates(10, 5);

        ExpantaNum foodBeforeFirstTick = gameManager.State.FoodAmount;
        gameManager.Tick(9.9d);

        Assert.That(gameManager.State.CalendarDays, Is.EqualTo(0));
        Assert.That(gameManager.State.FoodAmount, Is.GreaterThan(foodBeforeFirstTick));

        ExpantaNum foodBeforeSecondTick = gameManager.State.FoodAmount;
        gameManager.Tick(0.1d);

        Assert.That(gameManager.State.CalendarDays, Is.EqualTo(1));
        Assert.That(gameManager.State.FoodAmount, Is.GreaterThan(foodBeforeSecondTick));

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
            TechLevel.Animal,
            new ExpantaNum(10000),
            1L);
        InvokeGameStateMethod(
            state,
            "ResetDerivedEconomy",
            new ExpantaNum(100));

        Assert.That(state.FoodAmount, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(state.FoodCapacity, Is.GreaterThanOrEqualTo(state.FoodAmount));
        Assert.That(state.TerritoryTotal, Is.EqualTo(new ExpantaNum(500)));
        Assert.That(state.TerritoryUsed, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(state.AvailableTerritory, Is.EqualTo(new ExpantaNum(500)));
        Assert.That(state.Population.Population, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void C401_PopulationGrowthStopsWhenHappinessMultiplierIsZero()
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
        Assert.That(population.Population, Is.EqualTo(new ExpantaNum(15)));
    }

    [Test]
    public void PopulationGrowth_UsesLogisticRateAndFoodGatedDeparture()
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
            Is.EqualTo(new ExpantaNum(2)),
            "Housing over-capacity alone must not make population leave.");
    }

    [Test]
    public void PopulationNetRate_UsesStarvationDepartureBeforePositiveGrowth()
    {
        GameManager gameManager = CreateManager<GameManager>("PopulationNetRate-GameManager");
        CreateManager<BuildingManager>("PopulationNetRate-BuildingManager");
        InvokeGameStateMethod(
            gameManager.State,
            "RestoreCore",
            0,
            TechLevel.Animal,
            ExpantaNum.Zero,
            0L);
        InvokeGameStateMethod(gameManager.State, "RestorePopulation", new ExpantaNum(10));
        InvokeGameStateMethod(gameManager.State, "AdjustPopulationCapacity", new ExpantaNum(20));
        InvokeGameStateMethod(gameManager.State, "AdjustFoodRates", ExpantaNum.Zero, new ExpantaNum(20));

        Assert.That(gameManager.State.FoodNetRate, Is.LessThan(ExpantaNum.Zero));
        Assert.That(gameManager.CurrentPopulationGrowthRatePerSecond, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(gameManager.CurrentPopulationDepartureRatePerSecond, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(
            gameManager.CurrentPopulationNetRatePerSecond,
            Is.EqualTo(-gameManager.CurrentPopulationDepartureRatePerSecond));
    }

    [Test]
    public void PopulationGrowthRate_IsMonotonicWithHappiness()
    {
        PopulationState population = new PopulationState();
        InvokePopulationMethod(population, "RestorePopulation", new ExpantaNum(10));
        InvokePopulationMethod(population, "AdjustPopulationCapacity", new ExpantaNum(100));
        MethodInfo currentGrowth = typeof(PopulationState).GetMethod(
            "CurrentGrowthRatePerSecond",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(currentGrowth, Is.Not.Null);

        ExpantaNum stopped = (ExpantaNum)currentGrowth.Invoke(
            population,
            new object[] { ExpantaNum.Zero, PopulationState.BaseGrowthRatePerSecond });
        ExpantaNum low = (ExpantaNum)currentGrowth.Invoke(
            population,
            new object[] { new ExpantaNum(0.5d), PopulationState.BaseGrowthRatePerSecond });
        ExpantaNum normal = (ExpantaNum)currentGrowth.Invoke(
            population,
            new object[] { ExpantaNum.One, PopulationState.BaseGrowthRatePerSecond });
        ExpantaNum high = (ExpantaNum)currentGrowth.Invoke(
            population,
            new object[] { new ExpantaNum(1.5d), PopulationState.BaseGrowthRatePerSecond });

        Assert.That(stopped, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(low, Is.GreaterThan(stopped));
        Assert.That(normal, Is.GreaterThan(low));
        Assert.That(high, Is.GreaterThan(normal));
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
        CreateManager<ResourceManager>("Productivity-ResourceManager");
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
        ProgressionModifierManager.Rebuild(new List<ResearchState>());
        ExpantaNum baselineProductivity = buildingManager.TotalProductivity;
        typeof(ResearchState).GetMethod(
                "Restore",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(ExpantaNum), typeof(bool), typeof(bool) },
                null)
            .Invoke(researchState, new object[] { ExpantaNum.Zero, false, true });
        ProgressionModifierManager.Rebuild(new List<ResearchState> { researchState });

        Assert.That(
            buildingManager.TotalProductivity,
            Is.EqualTo(baselineProductivity + new ExpantaNum(7)));
        Assert.That(buildingManager.TryBuild(building, 2, out BuildFailure failure), Is.True);
        Assert.That(failure, Is.EqualTo(BuildFailure.None));
        Assert.That(
            buildingManager.TotalProductivity,
            Is.EqualTo(baselineProductivity + new ExpantaNum(15)));
        Assert.That(buildingManager.UsedProductivity, Is.EqualTo(new ExpantaNum(6)));
        Assert.That(
            buildingManager.AvailableProductivity,
            Is.EqualTo(buildingManager.TotalProductivity - new ExpantaNum(6)));

        buildingManager.GetState(building).SetEfficiencyForEditor(ExpantaNum.Zero);
        Assert.That(
            buildingManager.TotalProductivity,
            Is.EqualTo(baselineProductivity + new ExpantaNum(15)));
        Assert.That(
            buildingManager.AvailableProductivity,
            Is.EqualTo(buildingManager.TotalProductivity - new ExpantaNum(6)));
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
        Assert.That(buildingManager.AvailableProductivity, Is.EqualTo(new ExpantaNum(-2)));
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
        CreateManager<ResourceManager>("Housing-ResourceManager");
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
    public void DeconstructionRefund_UsesDefaultAndCompletedResearchRates()
    {
        GameManager gameManager = CreateManager<GameManager>("Deconstruction-GameManager");
        BuildingManager buildingManager =
            CreateManager<BuildingManager>("Deconstruction-BuildingManager");
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("Deconstruction-ResourceManager");
        Building woodHouse = DataBase<Building>.Find("WoodHouse");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        Assert.That(woodHouse, Is.Not.Null);
        Assert.That(wood, Is.Not.Null);

        resourceManager.SetAmount(wood, new ExpantaNum(1000));
        ExpantaNum amountBeforeDefaultBuild = resourceManager.GetAmount(wood);
        ProgressionModifierManager.Rebuild(null);
        Assert.That(buildingManager.TryBuild(woodHouse, ExpantaNum.One, out _), Is.True);
        ExpantaNum amountAfterDefaultBuild = resourceManager.GetAmount(wood);
        Assert.That(amountAfterDefaultBuild, Is.LessThan(amountBeforeDefaultBuild));
        Assert.That(buildingManager.TryDeconstruct(woodHouse, ExpantaNum.One, out _), Is.True);
        Assert.That(resourceManager.GetAmount(wood), Is.GreaterThan(amountAfterDefaultBuild));

        Research research = CreateResearch("DeconstructionIntegrationResearch");
        research.SetEffectsForEditor(new List<ResearchEffectDefinition>
        {
            new ResearchEffectDefinition
            {
                Type = ResearchEffectType.DeconstructionReturnRate,
                Value = new ExpantaNum(0.50d)
            }
        });
        ResearchState state = new ResearchState(research);
        typeof(ResearchState).GetMethod(
                "Restore",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(ExpantaNum), typeof(bool), typeof(bool), typeof(IReadOnlyDictionary<Resource, ExpantaNum>) },
                null)
            .Invoke(state, new object[]
            {
                ExpantaNum.Zero,
                false,
                true,
                new Dictionary<Resource, ExpantaNum>()
            });
        ProgressionModifierManager.Rebuild(new List<ResearchState> { state });

        ExpantaNum amountBeforeResearchBuild = resourceManager.GetAmount(wood);
        Assert.That(buildingManager.TryBuild(woodHouse, ExpantaNum.One, out _), Is.True);
        ExpantaNum amountAfterResearchBuild = resourceManager.GetAmount(wood);
        Assert.That(amountAfterResearchBuild, Is.LessThan(amountBeforeResearchBuild));
        Assert.That(buildingManager.TryDeconstruct(woodHouse, ExpantaNum.One, out _), Is.True);
        Assert.That(resourceManager.GetAmount(wood), Is.GreaterThan(amountAfterResearchBuild));
    }

    [Test]
    public void Deconstruction_UsesBuildingStateWhenDefinitionInstanceIsReplaced()
    {
        CreateManager<GameManager>("BuildingIdentity-GameManager");
        BuildingManager buildingManager =
            CreateManager<BuildingManager>("BuildingIdentity-BuildingManager");
        CreateManager<ResourceManager>("BuildingIdentity-ResourceManager");
        Building originalBuilding = CreateEconomyBuilding(
            "ReplacedBuilding",
            productivityConsumption: 0,
            productivityGranted: 0,
            populationCapacity: 0);
        Building reloadedBuilding = CreateEconomyBuilding(
            "ReplacedBuilding",
            productivityConsumption: 0,
            productivityGranted: 0,
            populationCapacity: 0);

        Assert.That(reloadedBuilding, Is.Not.SameAs(originalBuilding));
        Assert.That(buildingManager.TryBuild(originalBuilding, ExpantaNum.One, out _), Is.True);
        Assert.That(
            buildingManager.TryDeconstruct(reloadedBuilding, ExpantaNum.One, out BuildFailure failure),
            Is.True);
        Assert.That(failure, Is.EqualTo(BuildFailure.None));
        Assert.That(buildingManager.GetState(originalBuilding).Amount, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void HousingUpgrade_UsesMaterialDifferenceAndAppliesCapacityNetOnce()
    {
        // Capacity follows the current net upgrade delta.
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
        Research masonry = DataBase<Research>.Find("Masonry");
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
        ResearchState architectureState = researchManager.GetState(masonry);
        var paidMasonryCosts = new Dictionary<Resource, ExpantaNum>();
        for (int i = 0; i < masonry.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = masonry.ResourceRequirements[i];
            paidMasonryCosts[requirement.First] = requirement.Second;
        }
        typeof(ResearchState).GetMethod(
                "Restore",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(ExpantaNum), typeof(bool), typeof(bool), typeof(IReadOnlyDictionary<Resource, ExpantaNum>) },
                null)
            .Invoke(
                architectureState,
                new object[] { ExpantaNum.Zero, true, true, paidMasonryCosts });

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
        ExpantaNum woodBeforeUpgrade = resourceManager.GetAmount(wood);
        ExpantaNum stoneBrickBeforeUpgrade = resourceManager.GetAmount(stoneBrick);
        ExpantaNum clayBeforeUpgrade = resourceManager.GetAmount(clay);

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
            Is.EqualTo(new ExpantaNum(12)));
        Assert.That(gameManager.State.TerritoryUsed, Is.EqualTo(new ExpantaNum(3)));
        Assert.That(resourceManager.GetAmount(wood), Is.GreaterThan(woodBeforeUpgrade));
        Assert.That(resourceManager.GetAmount(stoneBrick), Is.LessThan(stoneBrickBeforeUpgrade));
        Assert.That(resourceManager.GetAmount(clay), Is.LessThan(clayBeforeUpgrade));
        Assert.That(buildingManager.ShouldDisplay(woodHouse), Is.False);
        Assert.That(buildingManager.ShouldDisplay(stoneHouse), Is.True);
    }

    [Test]
    public void HousingRemoval_AllowsOvercapacityWithoutCapacityRatchet()
    {
        GameManager gameManager = CreateManager<GameManager>("HousingRatchet-GameManager");
        BuildingManager buildingManager =
            CreateManager<BuildingManager>("HousingRatchet-BuildingManager");
        CreateManager<ResourceManager>("HousingRatchet-ResourceManager");
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
        ExpantaNum foodBeforeTick = gameManager.State.FoodAmount;
        gameManager.Tick(1d, buildingManager.SafePopulationDepartureAllowance);
        Assert.That(gameManager.State.FoodAmount, Is.LessThan(foodBeforeTick));
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
    public void FoodShortageSlowlyReducesPopulationWithoutGoingNegative()
    {
        PopulationState population = new PopulationState();
        InvokePopulationMethod(population, "RestorePopulation", new ExpantaNum(5));
        InvokePopulationMethod(
            population,
            "AdjustPopulationCapacity",
            new ExpantaNum(100));

        InvokePopulationMethod(
            population,
            "AdvancePopulation",
            3600d,
            new ExpantaNum(0.5d),
            ExpantaNum.Zero,
            ExpantaNum.Zero);
        Assert.That(population.Population, Is.GreaterThanOrEqualTo(new ExpantaNum(5)),
            "A negative food rate while inventory remains available must not remove population.");

        InvokePopulationMethod(
            population,
            "AdvancePopulation",
            3600d,
            new ExpantaNum(0.5d),
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            true);

        Assert.That(population.Population, Is.EqualTo(new ExpantaNum(3)));
        InvokePopulationMethod(
            population,
            "AdvancePopulation",
            3600d * 10d,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            ExpantaNum.Zero,
            true);
        Assert.That(population.Population, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(population.Population, Is.GreaterThanOrEqualTo(ExpantaNum.Zero));
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

        Assert.That(largeTick.Population, Is.EqualTo(new ExpantaNum(3)));
        Assert.That(smallTicks.Population, Is.EqualTo(new ExpantaNum(2)));
        Assert.That(
            smallTicks.PopulationChangeProgress.ToDouble(),
            Is.EqualTo(0.899083d).Within(0.001d));

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
            largeTick.PopulationChangeProgress.ToDouble(),
            Is.EqualTo(0.6d).Within(0.001d));
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
        GameManager gameManager = CreateManager<GameManager>("SimulationManagers-GameManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("SimulationManagers-ResourceManager");
        CreateManager<BuildingManager>("SimulationManagers-BuildingManager");
        CreateManager<ResearchManager>("SimulationManagers-ResearchManager");
        SimulationManager simulationManager = CreateManager<SimulationManager>("SimulationManagers-SimulationManager");

        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetAmount(wood, ExpantaNum.Zero);
        resourceManager.SetProductionRate(wood, 10);
        ExpantaNum woodBeforeTick = resourceManager.GetAmount(wood);
        InvokeGameStateMethod(
            gameManager.State,
            "AdjustFoodRates",
            new ExpantaNum(-5),
            ExpantaNum.Zero);

        simulationManager.ManualTick(10d);

        Assert.That(gameManager.State.CalendarDays, Is.EqualTo(1));
        Assert.That(resourceManager.GetAmount(wood), Is.GreaterThan(woodBeforeTick));
    }

    [Test]
    public void SimulationManager_UpdateLoopStartsPausedUntilBootstrapRuns()
    {
        SimulationManager simulationManager =
            CreateManager<SimulationManager>("SimulationManagers-Paused-Test");

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
            Version = SaveFormat.MinimumSupportedVersion - 1
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidDataException>());
    }

    [Test]
    public void SaveTimestamp_StampsSerializedPayloadBeforeOfflineResume()
    {
        GameManager gameManager = CreateManager<GameManager>("Save-Timestamp-GameManager");
        InvokeInstanceMethod(gameManager, "MarkSaveTimestamp", 10L);
        SaveManager.GameSaveData captured =
            (SaveManager.GameSaveData)InvokeInstanceMethod(gameManager, "CaptureSaveData");

        Assert.That(captured.LastSaveUnixSeconds, Is.EqualTo(10));
        Assert.That(gameManager.State.LastSaveUnixSeconds, Is.EqualTo(10));

        var data = new SaveManager.KingdomSaveData
        {
            General = new SaveManager.GameSaveData { LastSaveUnixSeconds = 10 }
        };

        InvokeStaticMethod(typeof(SaveManager), "StampSaveTimestamp", data, 42L);

        Assert.That(data.General.LastSaveUnixSeconds, Is.EqualTo(42));
        Assert.That(
            SaveManager.CalculateOfflineElapsedSeconds(42, 42, 3600d),
            Is.EqualTo(0d));
    }

    [TestCase(-1)]
    [TestCase(999)]
    public void SaveApply_RejectsInvalidCalendarOrTechLevel(int calendarDays)
    {
        CreateManager<GameManager>("Save-InvalidCore-GameManager");
        CreateManager<ResourceManager>("Save-InvalidCore-ResourceManager");
        CreateManager<BuildingManager>("Save-InvalidCore-BuildingManager");
        CreateManager<ResearchManager>("Save-InvalidCore-ResearchManager");
        CreateManager<WorkshopManager>("Save-InvalidCore-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-InvalidCore-SaveManager");

        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.General.CalendarDays = calendarDays < 0 ? calendarDays : 3;
        if (calendarDays >= 0)
            data.General.TechLevel = (TechLevel)calendarDays;

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<System.IO.InvalidDataException>());
    }

    [Test]
    public void SaveApply_RejectsNegativeCoreEconomyAndOutOfRangeSatisfaction()
    {
        CreateManager<GameManager>("Save-InvalidValues-GameManager");
        CreateManager<ResourceManager>("Save-InvalidValues-ResourceManager");
        CreateManager<BuildingManager>("Save-InvalidValues-BuildingManager");
        CreateManager<ResearchManager>("Save-InvalidValues-ResearchManager");
        CreateManager<WorkshopManager>("Save-InvalidValues-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-InvalidValues-SaveManager");

        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.General.Population = "-1";
        data.General.PowerSatisfaction = "1.1";

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<System.IO.InvalidDataException>());
    }

    [Test]
    public void SaveApply_RejectsPopulationProgressOutsideUnitInterval()
    {
        CreateManager<GameManager>("Save-InvalidProgress-GameManager");
        CreateManager<ResourceManager>("Save-InvalidProgress-ResourceManager");
        CreateManager<BuildingManager>("Save-InvalidProgress-BuildingManager");
        CreateManager<ResearchManager>("Save-InvalidProgress-ResearchManager");
        CreateManager<WorkshopManager>("Save-InvalidProgress-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-InvalidProgress-SaveManager");

        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.General.PopulationChangeProgress = "1.25";

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<System.IO.InvalidDataException>());
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

        Assert.That(resourceManager.GetAmount(wood), Is.Not.LessThan(firstAmount));
        Assert.That(resourceManager.GetAmount(wood), Is.Not.GreaterThan(firstAmount));
        Assert.That(GameManager.Instance.State.FoodProductionRate, Is.EqualTo(firstFoodRate));
        Assert.That(buildingManager.GetState(farm).Amount, Is.EqualTo(firstBuildingAmount));
        Assert.That(GameManager.Instance.State.FoodProductionRate, Is.GreaterThan(ExpantaNum.Zero));
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

    [Test]
    public void SaveApply_RejectsActiveResearchWithUnpaidResourceCosts()
    {
        CreateManager<GameManager>("Save-UnpaidActive-GameManager");
        CreateManager<ResourceManager>("Save-UnpaidActive-ResourceManager");
        CreateManager<BuildingManager>("Save-UnpaidActive-BuildingManager");
        CreateManager<ResearchManager>("Save-UnpaidActive-ResearchManager");
        CreateManager<WorkshopManager>("Save-UnpaidActive-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-UnpaidActive-SaveManager");

        Research target = DataBase<Research>.Find("Quarry");
        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Researches.ActiveResearchId = target.Id;
        data.Researches.States = new List<SaveManager.ResearchStateSaveData>
        {
            new SaveManager.ResearchStateSaveData
            {
                ResearchId = target.Id,
                Progress = "0",
                CostPaid = false,
                Completed = false,
                PaidResourceCosts = new List<SaveManager.ResearchResourceCostSaveData>()
            }
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains(target.Id, exception.InnerException.Message);
    }

    [Test]
    public void SaveApply_NormalizesFullyPaidActiveResearch()
    {
        CreateManager<GameManager>("Save-FullyPaidActive-GameManager");
        CreateManager<ResourceManager>("Save-FullyPaidActive-ResourceManager");
        CreateManager<BuildingManager>("Save-FullyPaidActive-BuildingManager");
        ResearchManager researchManager =
            CreateManager<ResearchManager>("Save-FullyPaidActive-ResearchManager");
        CreateManager<WorkshopManager>("Save-FullyPaidActive-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-FullyPaidActive-SaveManager");

        Research target = DataBase<Research>.Find("Quarry");
        var paid = new List<SaveManager.ResearchResourceCostSaveData>();
        for (int i = 0; i < target.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = target.ResourceRequirements[i];
            if (requirement.First == null || requirement.Second <= ExpantaNum.Zero)
                continue;
            paid.Add(new SaveManager.ResearchResourceCostSaveData
            {
                ResourceId = requirement.First.Id,
                Amount = requirement.Second.ToString()
            });
        }

        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Researches.ActiveResearchId = target.Id;
        data.Researches.States = new List<SaveManager.ResearchStateSaveData>
        {
            new SaveManager.ResearchStateSaveData
            {
                ResearchId = target.Id,
                Progress = "0",
                CostPaid = false,
                Completed = false,
                PaidResourceCosts = paid
            }
        };

        Assert.DoesNotThrow(() => InvokeApplySaveData(saveManager, data));
        Assert.That(researchManager.ActiveResearch.Definition, Is.EqualTo(target));
        Assert.That(researchManager.ActiveResearch.CostPaid, Is.True);
        Assert.That(researchManager.ActiveResearch.Status, Is.EqualTo(ResearchStatus.Researching));
    }

    [TestCase(5)]
    [TestCase(6)]
    public void SaveApply_RestoresLegacyFullyPaidResearchWithoutLedger(int version)
    {
        CreateManager<GameManager>("Save-LegacyPaid-GameManager");
        CreateManager<ResourceManager>("Save-LegacyPaid-ResourceManager");
        CreateManager<BuildingManager>("Save-LegacyPaid-BuildingManager");
        ResearchManager researchManager =
            CreateManager<ResearchManager>("Save-LegacyPaid-ResearchManager");
        CreateManager<WorkshopManager>("Save-LegacyPaid-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-LegacyPaid-SaveManager");

        Research target = DataBase<Research>.Find("Quarry");
        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Version = version;
        data.Researches.ActiveResearchId = target.Id;
        data.Researches.States = new List<SaveManager.ResearchStateSaveData>
        {
            new SaveManager.ResearchStateSaveData
            {
                ResearchId = target.Id,
                Progress = "0",
                CostPaid = true,
                Completed = false,
                // Legacy saves predate the per-resource payment ledger.
                PaidResourceCosts = null
            }
        };

        Assert.DoesNotThrow(() => InvokeApplySaveData(saveManager, data));
        Assert.That(researchManager.ActiveResearch.Definition, Is.EqualTo(target));
        Assert.That(researchManager.ActiveResearch.CostPaid, Is.True);
        Assert.That(researchManager.ActiveResearch.Status, Is.EqualTo(ResearchStatus.Researching));
    }

    [Test]
    public void SaveApply_RejectsCurrentFullyPaidResearchWithoutLedger()
    {
        CreateManager<GameManager>("Save-CurrentPaidNoLedger-GameManager");
        CreateManager<ResourceManager>("Save-CurrentPaidNoLedger-ResourceManager");
        CreateManager<BuildingManager>("Save-CurrentPaidNoLedger-BuildingManager");
        CreateManager<ResearchManager>("Save-CurrentPaidNoLedger-ResearchManager");
        CreateManager<WorkshopManager>("Save-CurrentPaidNoLedger-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-CurrentPaidNoLedger-SaveManager");

        Research target = DataBase<Research>.Find("Quarry");
        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Researches.ActiveResearchId = target.Id;
        data.Researches.States = new List<SaveManager.ResearchStateSaveData>
        {
            new SaveManager.ResearchStateSaveData
            {
                ResearchId = target.Id,
                Progress = "0",
                CostPaid = true,
                Completed = false,
                // Current saves must carry the exact payment ledger.
                PaidResourceCosts = null
            }
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains(target.Id, exception.InnerException.Message);
    }

    [Test]
    public void SaveApply_RejectsCompletedResearchWithIncompletePrerequisite()
    {
        CreateManager<GameManager>("Save-Completed-GameManager");
        CreateManager<ResourceManager>("Save-Completed-ResourceManager");
        CreateManager<BuildingManager>("Save-Completed-BuildingManager");
        CreateManager<ResearchManager>("Save-Completed-ResearchManager");
        CreateManager<WorkshopManager>("Save-Completed-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-Completed-SaveManager");

        Research target = DataBase<Research>.Find("StoneTools");
        Research prerequisite = target.Prerequisites[0];
        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Researches.States = new List<SaveManager.ResearchStateSaveData>
        {
            new SaveManager.ResearchStateSaveData
            {
                ResearchId = target.Id,
                Progress = target.BaseCost,
                Completed = true,
                CostPaid = true,
                PaidResourceCosts = new List<SaveManager.ResearchResourceCostSaveData>()
            }
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains(target.Id, exception.InnerException.Message);
        Assert.That(
            DataBase<Research>.Find(prerequisite.Id),
            Is.SameAs(prerequisite));
    }

    [Test]
    public void SaveApply_RejectsResearchProgressOutsideDefinitionCost()
    {
        CreateManager<GameManager>("Save-ResearchProgressRange-GameManager");
        CreateManager<ResourceManager>("Save-ResearchProgressRange-ResourceManager");
        CreateManager<BuildingManager>("Save-ResearchProgressRange-BuildingManager");
        CreateManager<ResearchManager>("Save-ResearchProgressRange-ResearchManager");
        CreateManager<WorkshopManager>("Save-ResearchProgressRange-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-ResearchProgressRange-SaveManager");

        Research research = DataBase<Research>.Find("ControlledFire");
        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Researches.States = new List<SaveManager.ResearchStateSaveData>
        {
            new SaveManager.ResearchStateSaveData
            {
                ResearchId = research.Id,
                Progress = research.BaseCost + ExpantaNum.One,
                PaidResourceCosts = new List<SaveManager.ResearchResourceCostSaveData>()
            }
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains(research.Id, exception.InnerException.Message);
    }

    [Test]
    public void SaveApply_RejectsCompletedResearchWithUnpaidCosts()
    {
        CreateManager<GameManager>("Save-CompletedUnpaid-GameManager");
        CreateManager<ResourceManager>("Save-CompletedUnpaid-ResourceManager");
        CreateManager<BuildingManager>("Save-CompletedUnpaid-BuildingManager");
        CreateManager<ResearchManager>("Save-CompletedUnpaid-ResearchManager");
        CreateManager<WorkshopManager>("Save-CompletedUnpaid-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-CompletedUnpaid-SaveManager");

        Research research = DataBase<Research>.All
            .FirstOrDefault(candidate => candidate.ResourceRequirements.Any(
                pair => pair.Second > ExpantaNum.Zero));
        Assert.That(research, Is.Not.Null);
        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Researches.States = new List<SaveManager.ResearchStateSaveData>
        {
            new SaveManager.ResearchStateSaveData
            {
                ResearchId = research.Id,
                Progress = research.BaseCost,
                Completed = true,
                CostPaid = true,
                PaidResourceCosts = new List<SaveManager.ResearchResourceCostSaveData>()
            }
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains(research.Id, exception.InnerException.Message);
    }

    [Test]
    public void SaveApply_RejectsResearchQueueWithUnmetPrerequisiteOrder()
    {
        CreateManager<GameManager>("Save-Queue-GameManager");
        CreateManager<ResourceManager>("Save-Queue-ResourceManager");
        CreateManager<BuildingManager>("Save-Queue-BuildingManager");
        CreateManager<ResearchManager>("Save-Queue-ResearchManager");
        CreateManager<WorkshopManager>("Save-Queue-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-Queue-SaveManager");

        Research target = DataBase<Research>.Find("StoneTools");
        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Researches.QueuedResearchIds = new List<string> { target.Id };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains(target.Id, exception.InnerException.Message);
    }

    [Test]
    public void SaveApply_RejectsUnknownResearchQueueId()
    {
        CreateManager<GameManager>("Save-UnknownQueue-GameManager");
        CreateManager<ResourceManager>("Save-UnknownQueue-ResourceManager");
        CreateManager<BuildingManager>("Save-UnknownQueue-BuildingManager");
        CreateManager<ResearchManager>("Save-UnknownQueue-ResearchManager");
        CreateManager<WorkshopManager>("Save-UnknownQueue-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-UnknownQueue-SaveManager");

        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Researches.QueuedResearchIds = new List<string> { "missing-research-id" };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains("missing-research-id", exception.InnerException.Message);
    }

    [Test]
    public void SaveApply_RejectsDuplicateResearchStateId()
    {
        CreateManager<GameManager>("Save-DuplicateResearch-GameManager");
        CreateManager<ResourceManager>("Save-DuplicateResearch-ResourceManager");
        CreateManager<BuildingManager>("Save-DuplicateResearch-BuildingManager");
        CreateManager<ResearchManager>("Save-DuplicateResearch-ResearchManager");
        CreateManager<WorkshopManager>("Save-DuplicateResearch-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-DuplicateResearch-SaveManager");

        Research research = DataBase<Research>.Find("ControlledFire");
        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Researches.States = new List<SaveManager.ResearchStateSaveData>
        {
            new SaveManager.ResearchStateSaveData
            {
                ResearchId = research.Id,
                Progress = "1",
                CostPaid = false,
                Completed = false,
                PaidResourceCosts = new List<SaveManager.ResearchResourceCostSaveData>()
            },
            new SaveManager.ResearchStateSaveData
            {
                ResearchId = research.Id,
                Progress = "2",
                CostPaid = false,
                Completed = false,
                PaidResourceCosts = new List<SaveManager.ResearchResourceCostSaveData>()
            }
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains(research.Id, exception.InnerException.Message);
    }

    [Test]
    public void SaveApply_RejectsNegativeResearchResourcePayment()
    {
        CreateManager<GameManager>("Save-NegativeResearchPayment-GameManager");
        CreateManager<ResourceManager>("Save-NegativeResearchPayment-ResourceManager");
        CreateManager<BuildingManager>("Save-NegativeResearchPayment-BuildingManager");
        CreateManager<ResearchManager>("Save-NegativeResearchPayment-ResearchManager");
        CreateManager<WorkshopManager>("Save-NegativeResearchPayment-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-NegativeResearchPayment-SaveManager");

        Research research = DataBase<Research>.All
            .FirstOrDefault(candidate => candidate.ResourceRequirements.Count > 0);
        Assert.That(research, Is.Not.Null);
        Resource resource = research.ResourceRequirements[0].First;
        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Researches.States = new List<SaveManager.ResearchStateSaveData>
        {
            new SaveManager.ResearchStateSaveData
            {
                ResearchId = research.Id,
                Progress = "0",
                PaidResourceCosts = new List<SaveManager.ResearchResourceCostSaveData>
                {
                    new SaveManager.ResearchResourceCostSaveData
                    {
                        ResourceId = resource.Id,
                        Amount = "-1"
                    }
                }
            }
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains(resource.Id, exception.InnerException.Message);
    }

    [Test]
    public void SaveApply_RejectsResearchPaymentForUnrequiredResource()
    {
        CreateManager<GameManager>("Save-UnrequiredResearchPayment-GameManager");
        CreateManager<ResourceManager>("Save-UnrequiredResearchPayment-ResourceManager");
        CreateManager<BuildingManager>("Save-UnrequiredResearchPayment-BuildingManager");
        CreateManager<ResearchManager>("Save-UnrequiredResearchPayment-ResearchManager");
        CreateManager<WorkshopManager>("Save-UnrequiredResearchPayment-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-UnrequiredResearchPayment-SaveManager");

        Research research = DataBase<Research>.All
            .FirstOrDefault(candidate => candidate.ResourceRequirements.Count > 0);
        Assert.That(research, Is.Not.Null);
        Resource unrelated = DataBase<Resource>.All
            .FirstOrDefault(resource => !research.ResourceRequirements.Any(pair => pair.First == resource));
        Assert.That(unrelated, Is.Not.Null);
        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Researches.States = new List<SaveManager.ResearchStateSaveData>
        {
            new SaveManager.ResearchStateSaveData
            {
                ResearchId = research.Id,
                Progress = "0",
                PaidResourceCosts = new List<SaveManager.ResearchResourceCostSaveData>
                {
                    new SaveManager.ResearchResourceCostSaveData
                    {
                        ResourceId = unrelated.Id,
                        Amount = "1"
                    }
                }
            }
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains(unrelated.Id, exception.InnerException.Message);
    }

    [Test]
    public void SaveApply_RejectsDuplicateResourceStateId()
    {
        CreateManager<GameManager>("Save-DuplicateResource-GameManager");
        CreateManager<ResourceManager>("Save-DuplicateResource-ResourceManager");
        CreateManager<BuildingManager>("Save-DuplicateResource-BuildingManager");
        CreateManager<ResearchManager>("Save-DuplicateResource-ResearchManager");
        CreateManager<WorkshopManager>("Save-DuplicateResource-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-DuplicateResource-SaveManager");

        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Resources.Resources = new List<SaveManager.ResourceStateSaveData>
        {
            new SaveManager.ResourceStateSaveData { ResourceId = "WoodLog", Amount = "1" },
            new SaveManager.ResourceStateSaveData { ResourceId = "WoodLog", Amount = "2" }
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains("WoodLog", exception.InnerException.Message);
    }

    [Test]
    public void SaveApply_RejectsDuplicateBuildingStateId()
    {
        CreateManager<GameManager>("Save-DuplicateBuilding-GameManager");
        CreateManager<ResourceManager>("Save-DuplicateBuilding-ResourceManager");
        CreateManager<BuildingManager>("Save-DuplicateBuilding-BuildingManager");
        CreateManager<ResearchManager>("Save-DuplicateBuilding-ResearchManager");
        CreateManager<WorkshopManager>("Save-DuplicateBuilding-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-DuplicateBuilding-SaveManager");

        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Buildings.Buildings = new List<SaveManager.BuildingStateSaveData>
        {
            new SaveManager.BuildingStateSaveData { BuildingId = "Farm", Amount = "1" },
            new SaveManager.BuildingStateSaveData { BuildingId = "Farm", Amount = "2" }
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains("Farm", exception.InnerException.Message);
    }

    [Test]
    public void SaveApply_RejectsUnknownResourceStateId()
    {
        CreateManager<GameManager>("Save-UnknownResource-GameManager");
        CreateManager<ResourceManager>("Save-UnknownResource-ResourceManager");
        CreateManager<BuildingManager>("Save-UnknownResource-BuildingManager");
        CreateManager<ResearchManager>("Save-UnknownResource-ResearchManager");
        CreateManager<WorkshopManager>("Save-UnknownResource-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-UnknownResource-SaveManager");

        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Resources.Resources = new List<SaveManager.ResourceStateSaveData>
        {
            new SaveManager.ResourceStateSaveData
            {
                ResourceId = "missing-resource-id",
                Amount = "1"
            }
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains("missing-resource-id", exception.InnerException.Message);
    }

    [Test]
    public void SaveApply_RejectsUnknownBuildingStateId()
    {
        CreateManager<GameManager>("Save-UnknownBuilding-GameManager");
        CreateManager<ResourceManager>("Save-UnknownBuilding-ResourceManager");
        CreateManager<BuildingManager>("Save-UnknownBuilding-BuildingManager");
        CreateManager<ResearchManager>("Save-UnknownBuilding-ResearchManager");
        CreateManager<WorkshopManager>("Save-UnknownBuilding-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-UnknownBuilding-SaveManager");

        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Buildings.Buildings = new List<SaveManager.BuildingStateSaveData>
        {
            new SaveManager.BuildingStateSaveData
            {
                BuildingId = "missing-building-id",
                Amount = "1"
            }
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains("missing-building-id", exception.InnerException.Message);
    }

    [Test]
    public void SaveApply_RejectsUnknownWorkshopUpgradeId()
    {
        CreateManager<GameManager>("Save-UnknownWorkshop-GameManager");
        CreateManager<ResourceManager>("Save-UnknownWorkshop-ResourceManager");
        CreateManager<BuildingManager>("Save-UnknownWorkshop-BuildingManager");
        CreateManager<ResearchManager>("Save-UnknownWorkshop-ResearchManager");
        CreateManager<WorkshopManager>("Save-UnknownWorkshop-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-UnknownWorkshop-SaveManager");

        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Workshop = new SaveManager.WorkshopSaveData
        {
            PurchasedUpgradeIds = new List<string> { "missing-workshop-id" }
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains("missing-workshop-id", exception.InnerException.Message);
    }

    [Test]
    public void SaveApply_RejectsActiveCampaignWithoutMatchingSectorState()
    {
        CreateManager<GameManager>("Save-CampaignMismatch-GameManager");
        CreateManager<ResourceManager>("Save-CampaignMismatch-ResourceManager");
        CreateManager<BuildingManager>("Save-CampaignMismatch-BuildingManager");
        CreateManager<ResearchManager>("Save-CampaignMismatch-ResearchManager");
        CreateManager<WorkshopManager>("Save-CampaignMismatch-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-CampaignMismatch-SaveManager");

        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.General.CampaignActive = true;
        data.General.CampaignTargetSectorId = "AzurePool";
        data.General.CampaignCasualties = "2";
        data.Sectors = new SaveManager.SectorSaveData
        {
            States = new List<SaveManager.SectorStateSaveData>()
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains("AzurePool", exception.InnerException.Message);
    }

    [Test]
    public void SaveApply_RejectsWorkshopUpgradeMissingPrerequisite()
    {
        CreateManager<GameManager>("Save-MissingWorkshopPrerequisite-GameManager");
        CreateManager<ResourceManager>("Save-MissingWorkshopPrerequisite-ResourceManager");
        CreateManager<BuildingManager>("Save-MissingWorkshopPrerequisite-BuildingManager");
        CreateManager<ResearchManager>("Save-MissingWorkshopPrerequisite-ResearchManager");
        CreateManager<WorkshopManager>("Save-MissingWorkshopPrerequisite-WorkshopManager");
        SaveManager saveManager = CreateManager<SaveManager>("Save-MissingWorkshopPrerequisite-SaveManager");

        WorkshopUpgrade dependent = DataBase<WorkshopUpgrade>.All
            .FirstOrDefault(upgrade => upgrade.RequiredUpgrades.Count > 0);
        Assert.That(dependent, Is.Not.Null);
        SaveManager.KingdomSaveData data = CreateRepresentativeSaveData();
        data.Workshop = new SaveManager.WorkshopSaveData
        {
            PurchasedUpgradeIds = new List<string> { dependent.Id }
        };

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => InvokeApplySaveData(saveManager, data));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        StringAssert.Contains(dependent.Id, exception.InnerException.Message);
    }

    [Test]
    public void WorkshopRestore_AcceptsPurchasedIdsInNonTopologicalOrder()
    {
        GameManager gameManager = CreateManager<GameManager>("WorkshopOrder-GameManager");
        CreateManager<ResourceManager>("WorkshopOrder-ResourceManager");
        CreateManager<BuildingManager>("WorkshopOrder-BuildingManager");
        ResearchManager researchManager =
            CreateManager<ResearchManager>("WorkshopOrder-ResearchManager");
        WorkshopManager workshopManager =
            CreateManager<WorkshopManager>("WorkshopOrder-WorkshopManager");
        WorkshopUpgrade dependent =
            DataBase<WorkshopUpgrade>.Find("EnzymaticConversionSystems");
        WorkshopUpgrade prerequisite =
            DataBase<WorkshopUpgrade>.Find("IndustrialFoodProcessEngineering");
        Assert.That(dependent.RequiredUpgrades, Does.Contain(prerequisite));
        InvokeGameStateMethod(
            gameManager.State,
            "AdvanceTechLevel",
            TechLevel.Industrial);
        foreach (WorkshopUpgrade upgrade in new[] { dependent, prerequisite })
            for (int i = 0; i < upgrade.RequiredResearch.Count; i++)
                InvokeResearchStateMethod(
                    researchManager.GetState(upgrade.RequiredResearch[i]),
                    "SetStatus",
                    ResearchStatus.Completed);

        MethodInfo restore = typeof(WorkshopManager).GetMethod(
            "RestoreSaveData",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(restore, Is.Not.Null);
        Assert.DoesNotThrow(() => restore.Invoke(
            workshopManager,
            new object[]
            {
                new SaveManager.WorkshopSaveData
                {
                    PurchasedUpgradeIds = new List<string>
                    {
                        dependent.Id.ToUpperInvariant(),
                        prerequisite.Id.ToLowerInvariant()
                    }
                }
            }));
        Assert.That(workshopManager.IsPurchased(prerequisite), Is.True);
        Assert.That(workshopManager.IsPurchased(dependent), Is.True);
        SaveManager.WorkshopSaveData captured = workshopManager.CaptureSaveData();
        Assert.That(captured.PurchasedUpgradeIds, Does.Contain(prerequisite.Id));
        Assert.That(captured.PurchasedUpgradeIds, Does.Contain(dependent.Id));
    }

    private static SaveManager.KingdomSaveData CreateRepresentativeSaveData()
    {
        return new SaveManager.KingdomSaveData
        {
            Version = SaveFormat.CurrentVersion,
            General = new SaveManager.GameSaveData
            {
                CalendarDays = 3,
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

    private static object InvokeInstanceMethod(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(target, arguments);
    }

    private static object InvokeStaticMethod(Type type, string methodName, params object[] arguments)
    {
        MethodInfo method = type.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(null, arguments);
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
                arguments[2],
                false
            };
        }
        else if (methodName == "AdvancePopulation" && arguments.Length == 4)
        {
            arguments = new[] { arguments[0], arguments[1], arguments[2], arguments[3], (object)false };
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
                    ExpantaNum.Zero,
                    false
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

    private static void InvokeResearchStateMethod(ResearchState state, string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(ResearchState).GetMethod(
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
        Assert.Throws<ArgumentOutOfRangeException>(() => ResearchManager.AdvanceResearchProgress(ExpantaNum.NaN, 1, 10, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResearchManager.AdvanceResearchProgress(0, ExpantaNum.NegativeInfinity, 10, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ResearchManager.AdvanceResearchProgress(0, 1, ExpantaNum.PositiveInfinity, 1));
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
    public void ResearchState_RestoreRecomputesPaymentFromSavedResourceLedger()
    {
        Research research = CreateResearch("RestorePaymentLedger");
        research.BaseCost = "100";
        Resource wood = DataBase<Resource>.Find("WoodLog");
        research.SetResourceRequirementsForEditor(
            new List<Pair<Resource, ExpantaNum>>
            {
                new Pair<Resource, ExpantaNum>(wood, new ExpantaNum(100))
            });
        ResearchState state = new ResearchState(research);
        MethodInfo restore = typeof(ResearchState).GetMethod(
            "Restore",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[]
            {
                typeof(ExpantaNum),
                typeof(bool),
                typeof(bool),
                typeof(IReadOnlyDictionary<Resource, ExpantaNum>)
            },
            null);
        Assert.That(restore, Is.Not.Null);

        restore.Invoke(
            state,
            new object[]
            {
                new ExpantaNum(10),
                true,
                false,
                new Dictionary<Resource, ExpantaNum>()
            });

        Assert.That(state.CostPaid, Is.False);
        Assert.That(state.GetPaidResourceCost(wood), Is.EqualTo(ExpantaNum.Zero));
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
        Assert.That(CampaignManager.CalculateProgressRate(new ExpantaNum(0.85d)).ToDouble(), Is.EqualTo(0.125d / 60d).Within(0.000001d));
        Assert.That(CampaignManager.CalculateProgressRate(ExpantaNum.One).ToDouble(), Is.EqualTo(0.25d / 60d).Within(0.000001d));
        Assert.That(CampaignManager.CalculateProgressRate(new ExpantaNum(2d)).ToDouble(), Is.EqualTo(1d / 60d).Within(0.000001d));
        Assert.That(CampaignManager.CalculateProgressRate(new ExpantaNum(100d)) < new ExpantaNum(2d / 60d), Is.True);
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
    public void CampaignPower_IntermediateLogisticsSatisfactionScalesEffectivePower()
    {
        ExpantaNum halfLogistics = CampaignManager.CalculateEffectivePower(
            new ExpantaNum(10),
            new ExpantaNum(10),
            new ExpantaNum(20),
            ExpantaNum.One,
            ExpantaNum.One,
            new ExpantaNum(0.5d),
            ExpantaNum.One);
        ExpantaNum quarterLogistics = CampaignManager.CalculateEffectivePower(
            new ExpantaNum(10),
            new ExpantaNum(10),
            new ExpantaNum(20),
            ExpantaNum.One,
            ExpantaNum.One,
            new ExpantaNum(0.25d),
            ExpantaNum.One);

        Assert.That(halfLogistics.ToDouble(), Is.EqualTo(10d).Within(0.000001d));
        Assert.That(quarterLogistics.ToDouble(), Is.EqualTo(5d).Within(0.000001d));
        Assert.That(halfLogistics, Is.GreaterThan(quarterLogistics));
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
    public void ResearchCostPayment_IsAtomicAndOnlyPaidOnceWithoutUi()
    {
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("ResourceManager-Test");

        Resource wood = DataBase<Resource>.Find("WoodLog");
        Research research = DataBase<Research>.Find("StoneTools");
        var state = new ResearchState(research);

        resourceManager.SetAmount(wood, 29);
        Resource stone = DataBase<Resource>.Find("StoneChunk");
        resourceManager.SetAmount(stone, 35);
        ExpantaNum woodBeforeFailedPayment = resourceManager.GetAmount(wood);
        ExpantaNum stoneBeforeFailedPayment = resourceManager.GetAmount(stone);
        Assert.That(ResearchManager.TryPayResearchCost(state), Is.False);
        Assert.That(resourceManager.GetAmount(wood), Is.Not.LessThan(woodBeforeFailedPayment));
        Assert.That(resourceManager.GetAmount(wood), Is.Not.GreaterThan(woodBeforeFailedPayment));
        Assert.That(resourceManager.GetAmount(stone), Is.Not.LessThan(stoneBeforeFailedPayment));
        Assert.That(resourceManager.GetAmount(stone), Is.Not.GreaterThan(stoneBeforeFailedPayment));
        Assert.That(state.CostPaid, Is.False);

        resourceManager.AddAmount(wood, 1);
        ExpantaNum woodBeforePayment = resourceManager.GetAmount(wood);
        Assert.That(ResearchManager.TryPayResearchCost(state), Is.True);
        Assert.That(resourceManager.GetAmount(wood), Is.LessThan(woodBeforePayment));
        Assert.That(state.CostPaid, Is.True);

        resourceManager.AddAmount(wood, 100);
        ExpantaNum woodBeforeAlreadyPaidRetry = resourceManager.GetAmount(wood);
        Assert.That(ResearchManager.TryPayResearchCost(state), Is.True);
        Assert.That(resourceManager.GetAmount(wood), Is.Not.LessThan(woodBeforeAlreadyPaidRetry));
        Assert.That(resourceManager.GetAmount(wood), Is.Not.GreaterThan(woodBeforeAlreadyPaidRetry));
    }

    [Test]
    public void ResourceManager_RegistersAllDefinitionsForResearchPaymentAndDisplay()
    {
        ResourceManager resourceManager = CreateManager<ResourceManager>("ResearchResourceState-Test");

        foreach (Resource resource in DataBase<Resource>.All)
        {
            Assert.That(resourceManager.States.ContainsKey(resource), Is.True, resource.Id);
        }
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
    public void ResourceAtomicPayment_RejectsInvalidCostsWithoutMutation()
    {
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("ResourceManager-InvalidAtomicPayment-Test");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetAmount(wood, 100);
        ExpantaNum woodBeforeInvalidPayment = resourceManager.GetAmount(wood);

        Assert.That(resourceManager.TryApplyAtomicPayment(
            new Dictionary<Resource, ExpantaNum>
            {
                [wood] = new ExpantaNum(double.NaN)
            }), Is.False);
        Assert.That(resourceManager.GetAmount(wood), Is.Not.LessThan(woodBeforeInvalidPayment));
        Assert.That(resourceManager.GetAmount(wood), Is.Not.GreaterThan(woodBeforeInvalidPayment));

        Assert.That(resourceManager.TryApplyAtomicPayment(
            new Dictionary<Resource, ExpantaNum>
            {
                [wood] = new ExpantaNum(double.PositiveInfinity)
            }), Is.False);
        Assert.That(resourceManager.GetAmount(wood), Is.Not.LessThan(woodBeforeInvalidPayment));
        Assert.That(resourceManager.GetAmount(wood), Is.Not.GreaterThan(woodBeforeInvalidPayment));
    }

    [Test]
    public void ExpantaNumAndResourceState_RejectBothInfinitySigns()
    {
        Assert.That(new ExpantaNum(double.PositiveInfinity).IsFinite, Is.False);
        Assert.That(new ExpantaNum(double.NegativeInfinity).IsFinite, Is.False);

        ResourceManager resourceManager =
            CreateManager<ResourceManager>("ResourceManager-NonFiniteState-Test");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        ExpantaNum woodBeforeInvalidStateWrites = resourceManager.GetAmount(wood);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => resourceManager.SetAmount(wood, ExpantaNum.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => resourceManager.SetAmount(wood, ExpantaNum.NegativeInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => resourceManager.SetProductionRate(wood, ExpantaNum.NaN));
        Assert.That(resourceManager.GetAmount(wood), Is.Not.LessThan(woodBeforeInvalidStateWrites));
        Assert.That(resourceManager.GetAmount(wood), Is.Not.GreaterThan(woodBeforeInvalidStateWrites));
    }

    [Test]
    public void RuntimeStateBoundaries_RejectNonFiniteValues()
    {
        GameState gameState = new GameState();
        Assert.Throws<TargetInvocationException>(
            () => InvokeGameStateMethod(gameState, "RestoreCore", 0, TechLevel.Animal,
                ExpantaNum.NaN, 0L));
        Assert.Throws<TargetInvocationException>(
            () => InvokeGameStateMethod(gameState, "AdjustFoodRates",
                ExpantaNum.PositiveInfinity, ExpantaNum.Zero));

        PopulationState population = new PopulationState();
        Assert.Throws<TargetInvocationException>(
            () => InvokePopulationMethod(population, "RestorePopulation", ExpantaNum.NaN));

        TerritoryState territory = new TerritoryState();
        Assert.Throws<TargetInvocationException>(
            () => InvokeTerritoryMethod(territory, "AddTotal", ExpantaNum.NegativeInfinity));

        MilitaryState military = new MilitaryState();
        Assert.Throws<TargetInvocationException>(
            () => InvokeMilitaryMethod(military, "AdjustFleetPower", ExpantaNum.PositiveInfinity));

        Research research = DataBase<Research>.Find("Mathematics");
        ResearchState researchState = new ResearchState(research);
        Assert.Throws<TargetInvocationException>(
            () => InvokeResearchStateMethod(researchState, "SetProgress", ExpantaNum.NaN));
        Assert.Throws<TargetInvocationException>(
            () => InvokeResearchStateMethod(researchState, "SetPaidResourceCost",
                DataBase<Resource>.Find("WoodLog"), ExpantaNum.NegativeInfinity));

        ResourceManager resourceManager = CreateManager<ResourceManager>("GlobalFactor-ResourceManager-Test");
        ResearchManager researchManager = CreateManager<ResearchManager>("GlobalFactor-ResearchManager-Test");
        BuildingManager buildingManager = CreateManager<BuildingManager>("GlobalFactor-BuildingManager-Test");
        Assert.Throws<ArgumentOutOfRangeException>(() => resourceManager.GlobalEfficiencyFactor = ExpantaNum.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => researchManager.GlobalEfficiencyFactor = ExpantaNum.PositiveInfinity);
        Assert.Throws<ArgumentOutOfRangeException>(() => buildingManager.GlobalEfficiencyFactor = ExpantaNum.NegativeInfinity);
    }

    [Test]
    public void ResourceAtomicChanges_CombinesRefundAndDebitWithoutPartialMutation()
    {
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("ResourceManager-AtomicChanges-Test");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        Resource stone = DataBase<Resource>.Find("StoneChunk");
        resourceManager.SetAmount(wood, 100);
        resourceManager.SetAmount(stone, 10);
        ExpantaNum woodBeforeFailedChange = resourceManager.GetAmount(wood);
        ExpantaNum stoneBeforeFailedChange = resourceManager.GetAmount(stone);

        Assert.That(resourceManager.TryApplyAtomicChanges(
            new Dictionary<Resource, ExpantaNum>
            {
                [wood] = new ExpantaNum(-60),
                [stone] = new ExpantaNum(-25)
            }), Is.False);
        Assert.That(resourceManager.GetAmount(wood), Is.Not.LessThan(woodBeforeFailedChange));
        Assert.That(resourceManager.GetAmount(wood), Is.Not.GreaterThan(woodBeforeFailedChange));
        Assert.That(resourceManager.GetAmount(stone), Is.Not.LessThan(stoneBeforeFailedChange));
        Assert.That(resourceManager.GetAmount(stone), Is.Not.GreaterThan(stoneBeforeFailedChange));

        Assert.That(resourceManager.TryApplyAtomicChanges(
            new Dictionary<Resource, ExpantaNum>
            {
                [wood] = new ExpantaNum(-60),
                [stone] = new ExpantaNum(25)
            }), Is.True);
        Assert.That(resourceManager.GetAmount(wood), Is.LessThan(woodBeforeFailedChange));
        Assert.That(resourceManager.GetAmount(stone), Is.GreaterThan(stoneBeforeFailedChange));
    }

    [Test]
    public void ResourceAtomicChanges_RollsBackWhenDomainCommitThrows()
    {
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("ResourceManager-AtomicCommitRollback-Test");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetAmount(wood, 100);
        ExpantaNum woodBeforeRollback = resourceManager.GetAmount(wood);

        Assert.Throws<InvalidOperationException>(() =>
            resourceManager.TryApplyAtomicChanges(
                new Dictionary<Resource, ExpantaNum>
                {
                    [wood] = new ExpantaNum(-40)
                },
                () => throw new InvalidOperationException("test commit failure")));

        Assert.That(resourceManager.GetAmount(wood), Is.Not.LessThan(woodBeforeRollback));
        Assert.That(resourceManager.GetAmount(wood), Is.Not.GreaterThan(woodBeforeRollback));
    }

    [Test]
    public void ResourceAtomicChanges_RollsBackExternalDomainCompensationWhenCommitThrows()
    {
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("ResourceManager-CrossDomainRollback-Test");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetAmount(wood, 100);
        ExpantaNum woodBeforeCrossDomainRollback = resourceManager.GetAmount(wood);
        GameState state = new GameState();
        ExpantaNum initialUsed = state.TerritoryUsed;

        Assert.Throws<InvalidOperationException>(() =>
            resourceManager.TryApplyAtomicPayment(
                new Dictionary<Resource, ExpantaNum>
                {
                    [wood] = new ExpantaNum(40)
                },
                () =>
                {
                    InvokeGameStateMethod(state, "CommitConstruction", new ExpantaNum(12));
                    throw new InvalidOperationException("test cross-domain commit failure");
                },
                () => InvokeGameStateMethod(state, "RefundConstruction", new ExpantaNum(12))));

        Assert.That(resourceManager.GetAmount(wood), Is.Not.LessThan(woodBeforeCrossDomainRollback));
        Assert.That(resourceManager.GetAmount(wood), Is.Not.GreaterThan(woodBeforeCrossDomainRollback));
        Assert.That(state.TerritoryUsed, Is.EqualTo(initialUsed));
    }

    [Test]
    public void ResourceAtomicChanges_ObserverFailureDoesNotUndoCommittedPayment()
    {
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("ResourceManager-ObserverFailure-Test");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetAmount(wood, 100);
        ExpantaNum woodBeforeObserverPayment = resourceManager.GetAmount(wood);
        resourceManager.ResourceStateChanged += _ =>
            throw new InvalidOperationException("test observer failure");

        LogAssert.Expect(LogType.Exception, "InvalidOperationException: test observer failure");

        Assert.That(resourceManager.TryApplyAtomicPayment(
            new Dictionary<Resource, ExpantaNum>
            {
                [wood] = new ExpantaNum(40)
            }), Is.True);
        Assert.That(resourceManager.GetAmount(wood), Is.LessThan(woodBeforeObserverPayment));
    }

    [Test]
    public void ResourceAtomicChanges_PreservesCommitFailureWhenRollbackAlsoThrows()
    {
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("ResourceManager-RollbackFailure-Test");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetAmount(wood, 100);
        ExpantaNum woodBeforeRollbackFailure = resourceManager.GetAmount(wood);

        LogAssert.Expect(LogType.Exception, "ApplicationException: rollback failure");
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            resourceManager.TryApplyAtomicPayment(
                new Dictionary<Resource, ExpantaNum>
                {
                    [wood] = new ExpantaNum(40)
                },
                () => throw new InvalidOperationException("commit failure"),
                () => throw new ApplicationException("rollback failure")));

        Assert.That(exception.Message, Is.EqualTo("commit failure"));
        Assert.That(resourceManager.GetAmount(wood), Is.Not.LessThan(woodBeforeRollbackFailure));
        Assert.That(resourceManager.GetAmount(wood), Is.Not.GreaterThan(woodBeforeRollbackFailure));
    }

    [Test]
    public void ResearchCostPayment_RejectsPartialMultiResourcePaymentWithoutMutation()
    {
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("ResourceManager-AtomicResearch-Test");

        Resource wood = DataBase<Resource>.Find("WoodLog");
        Resource stone = DataBase<Resource>.Find("StoneChunk");
        Research research = DataBase<Research>.Find("Mathematics");
        var state = new ResearchState(research);

        resourceManager.SetAmount(wood, 98);
        resourceManager.SetAmount(stone, 48);
        ExpantaNum woodBeforePartialPayment = resourceManager.GetAmount(wood);
        ExpantaNum stoneBeforePartialPayment = resourceManager.GetAmount(stone);
        Assert.That(ResearchManager.TryPayResearchCost(state), Is.False);
        Assert.That(resourceManager.GetAmount(wood), Is.Not.LessThan(woodBeforePartialPayment));
        Assert.That(resourceManager.GetAmount(wood), Is.Not.GreaterThan(woodBeforePartialPayment));
        Assert.That(resourceManager.GetAmount(stone), Is.Not.LessThan(stoneBeforePartialPayment));
        Assert.That(resourceManager.GetAmount(stone), Is.Not.GreaterThan(stoneBeforePartialPayment));
        Assert.That(state.CostPaid, Is.False);

        resourceManager.AddAmount(stone, 1);
        ExpantaNum woodBeforeCompletedPayment = resourceManager.GetAmount(wood);
        ExpantaNum stoneBeforeCompletedPayment = resourceManager.GetAmount(stone);
        Assert.That(ResearchManager.TryPayResearchCost(state), Is.True);
        Assert.That(resourceManager.GetAmount(wood), Is.LessThan(woodBeforeCompletedPayment));
        Assert.That(resourceManager.GetAmount(stone), Is.LessThan(stoneBeforeCompletedPayment));
        Assert.That(state.CostPaid, Is.True);
    }

    [Test]
    public void PayResearchCost_OnlyPaysTheSpecifiedResearch()
    {
        CreateManager<GameManager>("ResearchSinglePayment-GameManager");
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("ResearchSinglePayment-ResourceManager");
        ResearchManager researchManager =
            CreateManager<ResearchManager>("ResearchSinglePayment-ResearchManager");

        Resource wood = DataBase<Resource>.Find("WoodLog");
        Research target = DataBase<Research>.Find("ClayExtraction");
        Research prerequisite = target.Prerequisites[0];
        resourceManager.SetAmount(wood, 40);
        for (int i = 0; i < target.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = target.ResourceRequirements[i];
            resourceManager.SetAmount(requirement.First, requirement.Second + ExpantaNum.One);
        }

        ResearchState targetState = researchManager.GetState(target);
        Assert.That(targetState.CostPaid, Is.False);
        Assert.That(researchManager.GetState(prerequisite).CostPaid, Is.False);

        ExpantaNum woodBeforeTargetPayment = resourceManager.GetAmount(wood);
        Assert.That(
            researchManager.PayResearchCost(target),
            Is.EqualTo(ResearchPaymentResult.Paid));
        Assert.That(resourceManager.GetAmount(wood), Is.LessThan(woodBeforeTargetPayment));

        Assert.That(researchManager.GetState(target).CostPaid, Is.True);
        Assert.That(researchManager.GetState(prerequisite).CostPaid, Is.False);
        Assert.That(researchManager.ActiveResearch, Is.Null);
    }

    [Test]
    public void ResearchAction_QueuesUnpaidOnFirstClickAndPaysAtQueueHead()
    {
        CreateManager<GameManager>("ResearchAction-GameManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("ResearchAction-ResourceManager");
        ResearchManager researchManager = CreateManager<ResearchManager>("ResearchAction-ResearchManager");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        Research research = DataBase<Research>.Find("ControlledFire");
        resourceManager.SetAmount(wood, 1000);
        for (int i = 0; i < research.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = research.ResourceRequirements[i];
            resourceManager.SetAmount(requirement.First, ExpantaNum.Zero);
        }

        Assert.That(
            researchManager.HandleResearchAction(research),
            Is.EqualTo(ResearchActionResult.QueuedWaitingResources));
        Assert.That(researchManager.ActiveResearch, Is.Null);
        Assert.That(researchManager.GetState(research).CostPaid, Is.False);
        for (int i = 0; i < research.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = research.ResourceRequirements[i];
            resourceManager.AddAmount(requirement.First, requirement.Second + ExpantaNum.One);
        }
        Assert.That(researchManager.ActiveResearch, Is.Null);
        Assert.That(researchManager.IsQueued(research), Is.True);

        researchManager.TryStartNextQueuedResearch();
        Assert.That(researchManager.ActiveResearch.Definition, Is.SameAs(research));
        Assert.That(researchManager.GetState(research).CostPaid, Is.True);
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
        InvokeGameStateMethod(
            GameManager.Instance.State,
            "AdvanceTechLevel",
            TechLevel.Neolithic);
        for (int i = 0; i < active.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = active.ResourceRequirements[i];
            resourceManager.SetAmount(requirement.First, requirement.Second + ExpantaNum.One);
        }
        Assert.That(researchManager.PayResearchCost(active), Is.EqualTo(ResearchPaymentResult.Paid));
        for (int i = 0; i < queued.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = queued.ResourceRequirements[i];
            resourceManager.SetAmount(requirement.First, requirement.Second + ExpantaNum.One);
        }
        Assert.That(researchManager.HandleResearchAction(active), Is.EqualTo(ResearchActionResult.Started));
        Assert.That(researchManager.PayResearchCost(queued), Is.EqualTo(ResearchPaymentResult.Paid));
        Assert.That(researchManager.HandleResearchAction(queued), Is.EqualTo(ResearchActionResult.Queued));
        Assert.That(researchManager.ResearchQueue.Select(state => state.Definition), Has.Member(queued));
        Assert.That(researchManager.ActiveResearch.Definition, Is.SameAs(active));
        Assert.That(researchManager.GetState(queued).CostPaid, Is.True);
        Assert.That(researchManager.ActiveResearch.Definition, Is.SameAs(active));
        Assert.That(researchManager.IsQueued(queued), Is.True);
    }

    [Test]
    public void RestoreResearchSave_ClearsPreviousActiveResearchWhenSaveHasNone()
    {
        CreateManager<GameManager>("ResearchRestoreActiveClear-GameManager");
        ResourceManager resourceManager =
            CreateManager<ResourceManager>("ResearchRestoreActiveClear-ResourceManager");
        ResearchManager researchManager =
            CreateManager<ResearchManager>("ResearchRestoreActiveClear-ResearchManager");
        Research active = DataBase<Research>.Find("Agriculture");
        for (int i = 0; i < active.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = active.ResourceRequirements[i];
            resourceManager.SetAmount(requirement.First, requirement.Second + ExpantaNum.One);
        }
        Assert.That(researchManager.PayResearchCost(active), Is.EqualTo(ResearchPaymentResult.Paid));
        Assert.That(researchManager.StartResearch(active), Is.True);
        Assert.That(researchManager.ActiveResearch, Is.Not.Null);

        SaveManager.ResearchSaveData data = new SaveManager.ResearchSaveData
        {
            GlobalEfficiencyFactor = "1",
            States = new List<SaveManager.ResearchStateSaveData>(),
            QueuedResearchIds = new List<string>(),
            SelectedResearchId = string.Empty
        };
        MethodInfo restore = typeof(ResearchManager).GetMethod(
            "RestoreSaveData",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(restore, Is.Not.Null);
        restore.Invoke(researchManager, new object[] { data });

        Assert.That(researchManager.ActiveResearch, Is.Null);
        Assert.That(researchManager.ResearchQueue, Is.Empty);
        Assert.That(researchManager.GetState(active).Status, Is.Not.EqualTo(ResearchStatus.Researching));
        Assert.That(researchManager.GetState(active).Progress, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void ResearchAction_AutoQueuesPrerequisitesInTopologicalOrder()
    {
        CreateManager<GameManager>("ResearchPrerequisite-GameManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("ResearchPrerequisite-ResourceManager");
        ResearchManager researchManager = CreateManager<ResearchManager>("ResearchPrerequisite-ResearchManager");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        Research target = DataBase<Research>.Find("StoneTools");
        resourceManager.SetAmount(wood, 1000);

        Assert.That(
            researchManager.HandleResearchAction(target),
            Is.EqualTo(ResearchActionResult.QueuedWaitingResources)
                .Or.EqualTo(ResearchActionResult.Queued));
        Assert.That(researchManager.ActiveResearch, Is.Not.Null);
        Assert.That(researchManager.ActiveResearch.Definition, Is.Not.EqualTo(target));
        Assert.That(researchManager.ResearchQueue.Select(state => state.Definition), Has.Member(target));
        Assert.That(researchManager.GetState(target).CostPaid, Is.False);
    }

    [Test]
    public void ResearchAction_QueuesWithoutPaymentWhenResourcesAreUnavailable()
    {
        CreateManager<GameManager>("ResearchAtomic-GameManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("ResearchAtomic-ResourceManager");
        ResearchManager researchManager = CreateManager<ResearchManager>("ResearchAtomic-ResearchManager");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        Research target = DataBase<Research>.Find("StoneTools");
        resourceManager.SetAmount(wood, 500);

        resourceManager.SetAmount(wood, 0);
        ExpantaNum woodBeforeUnavailableResearch = resourceManager.GetAmount(wood);
        Assert.That(
            researchManager.HandleResearchAction(target),
            Is.EqualTo(ResearchActionResult.QueuedWaitingResources));
        Assert.That(researchManager.ActiveResearch, Is.Null);
        Assert.That(researchManager.ResearchQueue, Is.Not.Empty);
        Assert.That(resourceManager.GetAmount(wood), Is.Not.LessThan(woodBeforeUnavailableResearch));
        Assert.That(resourceManager.GetAmount(wood), Is.Not.GreaterThan(woodBeforeUnavailableResearch));
    }

    [Test]
    public void EnqueueResearch_RejectsLockedResearchInsteadOfBlockingQueue()
    {
        CreateManager<GameManager>("ResearchDirectQueue-GameManager");
        CreateManager<ResourceManager>("ResearchDirectQueue-ResourceManager");
        ResearchManager researchManager =
            CreateManager<ResearchManager>("ResearchDirectQueue-ResearchManager");

        Research target = DataBase<Research>.Find("StoneTools");
        Assert.That(target.Prerequisites, Is.Not.Empty);
        Assert.That(researchManager.EnqueueResearch(target), Is.False);
        Assert.That(researchManager.ResearchQueue, Is.Empty);
        Assert.That(researchManager.ActiveResearch, Is.Null);
    }

    [Test]
    public void CancellingQueuedPrerequisiteAlsoCancelsQueuedDependents()
    {
        CreateManager<GameManager>("ResearchCancelChain-GameManager");
        ResourceManager resourceManager = CreateManager<ResourceManager>("ResearchCancelChain-ResourceManager");
        ResearchManager researchManager = CreateManager<ResearchManager>("ResearchCancelChain-ResearchManager");
        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetAmount(wood, 1000);

        Research active = DataBase<Research>.Find("Agriculture");
        Research prerequisite = DataBase<Research>.Find("Quarry");
        Research target = DataBase<Research>.Find("StoneTools");
        for (int i = 0; i < target.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = target.ResourceRequirements[i];
            resourceManager.SetAmount(requirement.First, requirement.Second + ExpantaNum.One);
        }
        Assert.That(researchManager.PayResearchCost(active), Is.EqualTo(ResearchPaymentResult.Paid));
        Assert.That(researchManager.HandleResearchAction(active), Is.EqualTo(ResearchActionResult.Started));
        Assert.That(researchManager.HandleResearchAction(target), Is.EqualTo(ResearchActionResult.Queued));
        Assert.That(researchManager.IsQueued(prerequisite), Is.True);
        Assert.That(researchManager.IsQueued(target), Is.True);

        Assert.That(researchManager.RemoveQueuedResearch(prerequisite), Is.True);
        Assert.That(researchManager.IsQueued(prerequisite), Is.False);
        Assert.That(researchManager.IsQueued(target), Is.False);
        Assert.That(researchManager.GetState(target).Status, Is.EqualTo(ResearchStatus.Locked));
    }

    [Test]
    public void CancellingActiveResearchUsesTheQueueRemovalAction()
    {
        CreateManager<GameManager>("ResearchCancelActive-GameManager");
        CreateManager<ResourceManager>("ResearchCancelActive-ResourceManager");
        ResearchManager researchManager =
            CreateManager<ResearchManager>("ResearchCancelActive-ResearchManager");
        Research active = DataBase<Research>.Find("Agriculture");
        for (int i = 0; i < active.ResourceRequirements.Count; i++)
        {
            Pair<Resource, ExpantaNum> requirement = active.ResourceRequirements[i];
            ResourceManager.Instance.SetAmount(
                requirement.First, requirement.Second + ExpantaNum.One);
        }

        Assert.That(researchManager.HandleResearchAction(active),
            Is.EqualTo(ResearchActionResult.Started));
        Assert.That(researchManager.HandleResearchAction(active),
            Is.EqualTo(ResearchActionResult.Cancelled));
        Assert.That(researchManager.ActiveResearch, Is.Null);
        Assert.That(researchManager.IsQueued(active), Is.False);
    }

    [Test]
    public void Agriculture_WithNoResourceRequirementsCanStartImmediately()
    {
        CreateManager<GameManager>("Agriculture-GameManager");
        InvokeGameStateMethod(
            GameManager.Instance.State,
            "AdvanceTechLevel",
            TechLevel.Neolithic);
        ResourceManager resourceManager = CreateManager<ResourceManager>("Agriculture-ResourceManager");
        resourceManager.SetAmount(DataBase<Resource>.Find("WoodLog"), 29);
        ResearchManager researchManager =
            CreateManager<ResearchManager>("Agriculture-ResearchManager");
        Research agriculture = DataBase<Research>.Find("Agriculture");
        ResearchState state = researchManager.GetState(agriculture);

        Assert.That(agriculture.HasPositiveResourceRequirement, Is.True);
        Assert.That(researchManager.PayResearchCost(agriculture), Is.EqualTo(ResearchPaymentResult.Paid));
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
        Assert.That(farm.ResourceRequirements[0].Second, Is.GreaterThan(ExpantaNum.Zero));
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
        Assert.That(farm.FoodProductionRate, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(farm.FoodConsumptionRate, Is.EqualTo(ExpantaNum.Zero));
    }

    [Test]
    public void Managers_RaiseEventsWhenRuntimeDefinitionsAreDiscovered()
    {
        ResourceManager resourceManager = CreateManager<ResourceManager>("ResourceManager");
        BuildingManager buildingManager = CreateManager<BuildingManager>("BuildingManager");
        Resource resource = ScriptableObject.CreateInstance<Resource>();
        resource.SetIdForEditor("EventResource");
        createdObjects.Add(resource);
        Building farm = DataBase<Building>.Find("Farm");

        ResourceState addedResource = null;
        BuildingState addedBuilding = null;
        resourceManager.ResourceStateAdded += state => addedResource = state;
        buildingManager.BuildingStateAdded += state => addedBuilding = state;

        ResourceState resourceState = resourceManager.EnsureResource(resource);
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
        Assert.That(error, Is.EqualTo("研究依赖循环：A -> B -> C -> A"));
    }

    [Test]
    public void ResearchValidator_RejectsNullAndDuplicatePrerequisites()
    {
        Research root = CreateResearch("Root");
        Research dependency = CreateResearch("Dependency");
        root.SetPrerequisitesForEditor(new List<Research> { dependency, dependency });

        Assert.That(ResearchValidator.ValidateNoCycles(new[] { root }, out string duplicateError), Is.False);
        StringAssert.Contains("重复", duplicateError);

        root.SetPrerequisitesForEditor(new List<Research> { null });
        Assert.That(ResearchValidator.ValidateNoCycles(new[] { root }, out string nullError), Is.False);
        StringAssert.Contains("空", nullError);
    }

    private Research CreateResearch(string name)
    {
        Research research = ScriptableObject.CreateInstance<Research>();
        research.name = name;
        research.BaseCost = "1";
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
        T component = gameObject.AddComponent<T>();
        if (component is ResearchManager researchManager && researchManager.States.Count == 0)
            typeof(ResearchManager).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(researchManager, null);
        if (component is ResourceManager resourceManager && resourceManager.States.Count == 0)
            typeof(ResourceManager).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(resourceManager, null);
        return component;
    }

    [Serializable]
    private sealed class PairContainer
    {
        public Pair<int, string> value;
    }
}



