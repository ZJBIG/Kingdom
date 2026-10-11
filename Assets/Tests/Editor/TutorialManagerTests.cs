using System;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class TutorialManagerTests
{
    [TearDown]
    public void TearDown()
    {
        TutorialManager[] managers = UnityEngine.Object.FindObjectsOfType<TutorialManager>();
        for (int i = 0; i < managers.Length; i++)
            UnityEngine.Object.DestroyImmediate(managers[i].gameObject);
    }

    [Test]
    public void DefaultStepsExposeExtensibleTutorialContract()
    {
        TutorialManager manager = TutorialManager.Ensure();

        Assert.That(manager.Steps.Count, Is.GreaterThanOrEqualTo(8));
        Assert.That(manager.ActiveStepId, Is.EqualTo("orientation"));
        TutorialStep first = null;
        for (int i = 0; i < manager.Steps.Count; i++)
            if (manager.Steps[i].Id == "orientation")
            {
                first = manager.Steps[i];
                break;
            }
        Assert.That(first, Is.Not.Null);
        Assert.That(first.TriggerCondition, Is.Not.Empty);
        Assert.That(first.CompletionCondition, Is.Not.Empty);
        Assert.That(first.NextStepId, Is.EqualTo("resources"));

        TutorialStepDefinition authored = ScriptableObject.CreateInstance<TutorialStepDefinition>();
        authored.Id = "authored-step";
        authored.Title = "Authored";
        authored.Description = "Loaded from a definition asset.";
        authored.NarrativeText = "The mouse clans remember the first fire.";
        authored.Kind = TutorialStepKind.LongTerm;
        authored.NextStepId = string.Empty;
        authored.RewardId = "existing-feedback";
        authored.TriggerCondition = "calendar-days>=1";
        authored.CompletionCondition = "research-complete";
        authored.NavigationPage = "Era";
        TutorialStep runtime = authored.ToRuntime();
        Assert.That(runtime.Id, Is.EqualTo(authored.Id));
        Assert.That(runtime.RewardId, Is.EqualTo(authored.RewardId));
        Assert.That(runtime.TriggerCondition, Is.EqualTo(authored.TriggerCondition));
        Assert.That(runtime.CompletionCondition, Is.EqualTo(authored.CompletionCondition));
        Assert.That(runtime.NavigationPage, Is.EqualTo(authored.NavigationPage));
        Assert.That(runtime.NarrativeText, Is.EqualTo(authored.NarrativeText));

        UnityEngine.Object.DestroyImmediate(authored);
    }

    [Test]
    public void AuthoredTutorialAssetsFormOneReachableChain()
    {
        TutorialStepDefinition[] definitions =
            Resources.LoadAll<TutorialStepDefinition>("Datas/Tutorial");
        Assert.That(definitions, Has.Length.EqualTo(8));

        var byId = new Dictionary<string, TutorialStepDefinition>();
        var navigationPages = new HashSet<string>
        {
            "Overview", "Resources", "Buildings", "Research", "Era",
            "Workshop", "Music", "Sectors", "Story"
        };
        for (int i = 0; i < definitions.Length; i++)
        {
            TutorialStepDefinition definition = definitions[i];
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.Id, Is.Not.Empty);
            Assert.That(navigationPages.Contains(definition.NavigationPage), Is.True,
                "Tutorial step must point to an authored page: " + definition.Id);
            bool unique = !byId.ContainsKey(definition.Id);
            if (unique)
                byId.Add(definition.Id, definition);
            Assert.That(unique, Is.True,
                "Tutorial step IDs must be unique.");
            if (!string.IsNullOrEmpty(definition.NextStepId))
                Assert.That(byId.ContainsKey(definition.NextStepId) ||
                    ContainsId(definitions, definition.NextStepId), Is.True,
                    "Tutorial step points to an unknown next step: " + definition.Id);
        }

        string rootId = FindRootId(definitions);
        Assert.That(rootId, Is.EqualTo("orientation"));
        var visited = new HashSet<string>();
        string currentId = rootId;
        while (!string.IsNullOrEmpty(currentId))
        {
            Assert.That(visited.Add(currentId), Is.True,
                "Tutorial step chain contains a cycle.");
            TutorialStepDefinition current = byId[currentId];
            currentId = current.NextStepId;
        }
        Assert.That(visited, Has.Count.EqualTo(definitions.Length));
        for (int i = 0; i < definitions.Length; i++)
            Assert.That(definitions[i].NarrativeText, Is.Not.Empty,
                "Every onboarding step should carry a short civilization-revival context line.");
    }

    [Test]
    public void ResourcesTutorialRequiresViewingCoreResourceDetails()
    {
        TutorialStepDefinition[] definitions =
            Resources.LoadAll<TutorialStepDefinition>("Datas/Tutorial");
        TutorialStepDefinition resources = null;
        for (int i = 0; i < definitions.Length; i++)
            if (definitions[i] != null && definitions[i].Id == "resources")
            {
                resources = definitions[i];
                break;
            }

        Assert.That(resources, Is.Not.Null);
        Assert.That(resources.Title, Is.EqualTo("理解库存与净产出"));
        StringAssert.Contains("食物", resources.Description);
        StringAssert.Contains("原木", resources.Description);
        Assert.That(resources.CompletionCondition, Is.EqualTo("resource-detail-viewed"));
        StringAssert.DoesNotContain("黏土", resources.NarrativeText);
        StringAssert.DoesNotContain("纤维", resources.NarrativeText);
        StringAssert.DoesNotContain("黏土", resources.Description);
        StringAssert.DoesNotContain("纤维", resources.Description);
    }

    [Test]
    public void TutorialProgressRoundTripsThroughSaveJson()
    {
        var save = new SaveManager.KingdomSaveData
        {
            Version = SaveFormat.CurrentVersion,
            Tutorial = new SaveManager.TutorialSaveData
            {
                ActiveStepId = "era-goal",
                CompletedStepIds = new List<string> { "orientation", "resources" }
            }
        };

        string json = JsonUtility.ToJson(save);
        SaveManager.KingdomSaveData restored =
            JsonUtility.FromJson<SaveManager.KingdomSaveData>(json);
        Assert.That(restored.Tutorial, Is.Not.Null);
        Assert.That(restored.Tutorial.ActiveStepId, Is.EqualTo("era-goal"));
        Assert.That(restored.Tutorial.CompletedStepIds,
            Is.EqualTo(new[] { "orientation", "resources" }));
    }

    [Test]
    public void TutorialDetailViewedStateIsTransientAndDoesNotChangeSaveFormat()
    {
        TutorialManager manager = TutorialManager.Ensure();
        manager.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            ActiveStepId = "resources",
            CompletedStepIds = new List<string> { "orientation" }
        }, TechLevel.Animal);

        manager.RecordDetailViewed("Resources", "Food");
        SaveManager.TutorialSaveData captured = manager.CaptureSaveData();
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured.ActiveStepId, Is.EqualTo("resources"));
        Assert.That(captured.CompletedStepIds, Does.Contain("orientation"));
        Assert.That(captured.CompletedStepIds, Does.Not.Contain("Food"));

        string json = JsonUtility.ToJson(captured);
        StringAssert.DoesNotContain("visitedDetail", json);
        StringAssert.DoesNotContain("Food", json);
    }

    [Test]
    public void CompletedGoalFeedbackUsesCompletedDirectPredecessor()
    {
        TutorialManager manager = TutorialManager.Ensure();
        manager.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            ActiveStepId = "resources",
            CompletedStepIds = new List<string> { "orientation" }
        }, TechLevel.Animal);

        TutorialStep completed = manager.FindPreviousCompletedStepForEditor("resources");
        Assert.That(completed, Is.Not.Null);
        Assert.That(completed.Id, Is.EqualTo("orientation"));

        manager.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            ActiveStepId = "resources",
            CompletedStepIds = new List<string>()
        }, TechLevel.Animal);
        Assert.That(manager.FindPreviousCompletedStepForEditor("resources"), Is.Null);
    }

    [Test]
    public void EveryTechLevelHasCivilizationContext()
    {
        Array values = Enum.GetValues(typeof(TechLevel));
        for (int i = 0; i < values.Length; i++)
        {
            TechLevel era = (TechLevel)values.GetValue(i);
            Assert.That(TutorialManager.GetCivilizationContext(era), Is.Not.Empty,
                "Every existing era should explain the mouse civilization revival.");
        }
    }

    [Test]
    public void SpacerGuidanceUsesExistingResearchAndBuildingDefinitions()
    {
        string[] researchIds =
        {
            "HomeSystemSurvey",
            "DeepSpaceFleet",
            "InterstellarNavigation"
        };
        for (int i = 0; i < researchIds.Length; i++)
            Assert.That(DataBase<Research>.TryFind(
                researchIds[i], out Research research) && research != null,
                "Spacer guidance must reference a real Research: " + researchIds[i]);

        string[] buildingIds =
        {
            "LaunchCenter",
            "OrbitalStation",
            "Shipyard"
        };
        for (int i = 0; i < buildingIds.Length; i++)
            Assert.That(DataBase<Building>.TryFind(
                buildingIds[i], out Building building) && building != null,
                "Spacer guidance must reference a real Building: " + buildingIds[i]);

        Func<TutorialSnapshot, GameManager, ResearchManager, BuildingManager, bool>
            spacerGuidance = TutorialManager.BuildSpacerGuidanceForEditor;
        Assert.That(spacerGuidance, Is.Not.Null);
    }

    [TestCase(0, "Research", "HomeSystemSurvey")]
    [TestCase(1, "Buildings", "LaunchCenter")]
    [TestCase(2, "Research", "OrbitalEngineering")]
    [TestCase(3, "Sectors", "DawnRing")]
    [TestCase(4, "Research", "DeepSpaceFleet")]
    [TestCase(5, "Research", "DeepSpaceFleet")]
    [TestCase(6, "Research", "DeepSpaceFleet")]
    [TestCase(7, "Sectors", "DawnRing")]
    [TestCase(8, "Sectors", "DawnRing")]
    [TestCase(9, "Sectors", "DawnRing")]
    public void SpacerGuidancePrioritizesReachableHomeSystemBeforeDistantFleet(
        int scenario, string expectedPage, string expectedTarget)
    {
        GameObject host = new GameObject("Tutorial-HomeSystem-Guidance");
        try
        {
            ResourceManager resources = host.AddComponent<ResourceManager>();
            if (resources.States.Count == 0)
                resources.InitializeForEditor();
            GameManager game = host.AddComponent<GameManager>();
            BuildingManager buildings = host.AddComponent<BuildingManager>();
            ResearchManager research = host.AddComponent<ResearchManager>();
            if (research.States.Count == 0)
                research.InitializeForEditor();
            game.State.AdvanceTechLevelForEditor(TechLevel.Spacer);
            game.Sectors.InitializeDefinitions();
            var researchStates = new List<ResearchState>();
            foreach (KeyValuePair<Research, ResearchState> pair in research.States)
            {
                bool queued = pair.Key.Id == "DeepSpaceFleet" ||
                    pair.Key.Id == "InterstellarNavigation" ||
                    (scenario == 0 && pair.Key.Id == "HomeSystemSurvey") ||
                    (scenario == 2 && pair.Key.Id == "OrbitalEngineering");
                pair.Value.SetStatusForEditor(queued
                    ? ResearchStatus.Queued : ResearchStatus.Completed);
                researchStates.Add(pair.Value);
            }
            ProgressionModifierManager.Rebuild(researchStates);
            if (scenario != 1 && scenario != 2)
                buildings.EnsureBuilding(DataBase<Building>.Find("LaunchCenter"))
                    .SetAmountForEditor(ExpantaNum.One);

            SectorState dawn = game.Sectors.GetState(
                DataBase<SectorDefinition>.Find("DawnRing"));
            if (scenario == 4)
            {
                dawn.SetUnlockedForEditor(true);
                dawn.SetOccupiedForEditor(true);
                dawn.SetCampaignProgressForEditor(ExpantaNum.One);
            }
            if (scenario == 5 || scenario == 6)
                for (int i = 0; i < game.Sectors.OrderedStates.Count; i++)
                {
                    SectorState state = game.Sectors.OrderedStates[i];
                    if (!state.Definition.IsHomeSystem)
                        continue;
                    bool unfinished = scenario == 5 &&
                        (state.Definition.Id == "HeliosCore" ||
                         state.Definition.Id == "ThunderGate");
                    state.SetOccupiedForEditor(!unfinished);
                    if (!unfinished)
                    {
                        state.SetUnlockedForEditor(true);
                        state.SetCampaignProgressForEditor(ExpantaNum.One);
                    }
                }
            if (scenario == 5)
            {
                SectorState active = game.Sectors.GetState(
                    DataBase<SectorDefinition>.Find("ThunderGate"));
                active.SetUnlockedForEditor(true);
                active.SetColonizationActiveForEditor(true);
                Assert.That(game.Sectors.CanAccess(
                    DataBase<SectorDefinition>.Find("HeliosCore")), Is.True,
                    "Additional reachable and active destinations must not delay distant-fleet guidance.");
            }
            if (scenario == 7 || scenario == 8)
                dawn.SetUnlockedForEditor(true);
            if (scenario == 8)
                dawn.SetCampaignProgressForEditor(ExpantaNum.One);
            if (scenario == 9)
            {
                dawn.SetUnlockedForEditor(true);
                dawn.SetColonizationActiveForEditor(true);
            }

            bool dawnUnlocked = dawn.Unlocked;
            TutorialSnapshot snapshot = new TutorialSnapshot();
            Assert.That(TutorialManager.BuildSpacerGuidanceForEditor(
                snapshot, game, research, buildings), Is.True);
            // Page names and target IDs are exact navigation protocol values.
            Assert.That(snapshot.NavigationPage, Is.EqualTo(expectedPage));
            Assert.That(snapshot.NavigationTargetId, Is.EqualTo(expectedTarget));
            if (scenario == 4)
                Assert.That(game.Sectors.GetState(
                    DataBase<SectorDefinition>.Find("AzurePool")).Occupied, Is.False,
                    "The first home-system outpost completes this goal; others remain optional.");
            if (expectedPage == "Sectors")
            {
                SectorDefinition target = DataBase<SectorDefinition>.Find(expectedTarget);
                Assert.That(target.IsHomeSystem, Is.True);
                Assert.That(game.Sectors.CanAccess(target), Is.True);
                Assert.That(game.Sectors.GetUnlockFailure(target),
                    Is.EqualTo(SectorOperationFailure.None)
                        .Or.EqualTo(SectorOperationFailure.AlreadyUnlocked));
            }
            Assert.That(research.GetState(DataBase<Research>.Find("DeepSpaceFleet")).Status,
                Is.EqualTo(ResearchStatus.Queued), "Guidance must not complete research.");
            Assert.That(dawn.Unlocked, Is.EqualTo(dawnUnlocked),
                "Guidance must not unlock destinations.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            ProgressionModifierManager.Rebuild(null);
        }
    }

    [Test]
    public void TutorialStepRequiresVisitToItsCurrentNavigationPage()
    {
        TutorialManager manager = TutorialManager.Ensure();
        TutorialStep orientation = null;
        TutorialStep resources = null;
        for (int i = 0; i < manager.Steps.Count; i++)
        {
            TutorialStep step = manager.Steps[i];
            if (step.Id == "orientation")
                orientation = step;
            else if (step.Id == "resources")
                resources = step;
        }

        Assert.That(manager.HasVisitedPageForStepForEditor(orientation), Is.False);
        manager.RecordPageVisitedForEditor("Resources");
        Assert.That(manager.HasVisitedPageForStepForEditor(orientation), Is.False);
        manager.RecordPageVisitedForEditor("Overview");
        Assert.That(manager.HasVisitedPageForStepForEditor(orientation), Is.True);
        Assert.That(manager.HasVisitedPageForStepForEditor(resources), Is.False);

        manager.ResetForNewGameForEditor();
        Assert.That(manager.HasVisitedPageForStepForEditor(orientation), Is.False);
    }

    [Test]
    public void SaveSessionVersionChangesOnlyWhenRuntimeProgressIsRestored()
    {
        GameObject dependencyHost = new GameObject("Tutorial-SaveSession-Dependencies");
        dependencyHost.AddComponent<ResourceManager>();
        dependencyHost.AddComponent<GameManager>();
        dependencyHost.AddComponent<BuildingManager>();
        dependencyHost.AddComponent<ResearchManager>();
        try
        {
            TutorialManager manager = TutorialManager.Ensure();
            int initial = manager.SaveSessionVersion;

            manager.RestoreSaveData(new SaveManager.TutorialSaveData
            {
                ActiveStepId = "orientation"
            }, TechLevel.Animal);
            int restored = manager.SaveSessionVersion;
            Assert.That(restored, Is.GreaterThan(initial));

            manager.Evaluate();
            Assert.That(manager.SaveSessionVersion, Is.EqualTo(restored),
                "Tutorial step completion must not create a new save session.");

            manager.ResetForNewGameForEditor();
            Assert.That(manager.SaveSessionVersion, Is.GreaterThan(restored));
        }
        finally
        {
            Object.DestroyImmediate(dependencyHost);
        }
    }

    [Test]
    public void PopulationStepMatchesItsStateDrivenNavigationPage()
    {
        TutorialManager manager = TutorialManager.Ensure();
        TutorialStep population = null;
        for (int i = 0; i < manager.Steps.Count; i++)
            if (manager.Steps[i].Kind == TutorialStepKind.Population)
                population = manager.Steps[i];

        GameState state = new GameState();
        state.RestorePopulationForEditor(ExpantaNum.One);
        state.RestorePopulationCapacityExactForEditor(new ExpantaNum(2), ExpantaNum.Zero);
        state.SetFoodAvailabilityForEditor(new ExpantaNum(0.5));
        Assert.That(TutorialManager.GetNavigationPageForStepForEditor(population, state),
            Is.EqualTo("Resources"));
        state.AdjustFoodRatesForEditor(ExpantaNum.Zero, new ExpantaNum(6));
        Assert.That(TutorialManager.GetNavigationPageForStepForEditor(population, state),
            Is.EqualTo("Resources"));
        Assert.That(TutorialManager.GetNavigationPageForStepForEditor(population, new GameState()),
            Is.EqualTo("Buildings"));
    }

    [Test]
    public void PopulationFoodShortageTargetsFoodDetail()
    {
        GameManager game = new GameObject("Population-Food-Target-GameManager")
            .AddComponent<GameManager>();
        BuildingManager buildings = new GameObject("Population-Food-Target-BuildingManager")
            .AddComponent<BuildingManager>();
        try
        {
            game.State.RestorePopulationCapacityExactForEditor(
                new ExpantaNum(2), ExpantaNum.Zero);
            game.State.SetFoodAvailabilityForEditor(new ExpantaNum(0.5));

            Assert.That(TutorialManager.GetPopulationNavigationTargetForEditor(game, buildings),
                Is.EqualTo("Food"));

            game.State.AdjustFoodRatesForEditor(new ExpantaNum(6), ExpantaNum.Zero);
            game.State.SetFoodAvailabilityForEditor(ExpantaNum.One);
            Assert.That(TutorialManager.GetPopulationNavigationTargetForEditor(game, buildings),
                Is.EqualTo("WoodHouse"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(buildings.gameObject);
            UnityEngine.Object.DestroyImmediate(game.gameObject);
        }
    }

    [Test]
    public void AnimalPopulationStepTargetsWoodHouseWhenCapacityIsZero()
    {
        GameObject gameObject = new GameObject("Population-Target-GameManager");
        GameObject buildingObject = new GameObject("Population-Target-BuildingManager");
        try
        {
            GameManager game = gameObject.AddComponent<GameManager>();
            BuildingManager buildings = buildingObject.AddComponent<BuildingManager>();
            string target = TutorialManager.GetPopulationNavigationTargetForEditor(game, buildings);
            Assert.That(target, Is.EqualTo("WoodHouse"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(buildingObject);
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void PopulationStepRequiresPopulationStateToGrow()
    {
        GameState state = new GameState();
        state.RestorePopulationCapacityExactForEditor(
            new ExpantaNum(2), ExpantaNum.Zero);

        Assert.That(TutorialManager.HasObservedPopulationGrowth(state), Is.False);

        state.RestorePopulationForEditor(ExpantaNum.One);
        Assert.That(TutorialManager.HasObservedPopulationGrowth(state), Is.True);
    }

    [Test]
    public void PopulationCapacityConditionMatchesItsDeclaredMeaning()
    {
        GameObject host = new GameObject("TutorialConditionGameManager");
        GameManager game = host.AddComponent<GameManager>();
        game.State.RestorePopulationCapacityExactForEditor(
            new ExpantaNum(2), ExpantaNum.Zero);
        TutorialStep step = new TutorialStep(
            "capacity", "Capacity", "Capacity", TutorialStepKind.Population,
            string.Empty, string.Empty, "game-state",
            "population-capacity-positive", "Buildings");

        try
        {
            Assert.That(TutorialManager.IsStepCompleteForEditor(step, game, null, null, null),
                Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void ResearchStepRequiresCompletedStatus()
    {
        Research definition = ScriptableObject.CreateInstance<Research>();
        definition.SetIdForEditor("TutorialResearchStatus");
        definition.BaseCost = "1";
        ResearchState state = new ResearchState(definition);

        Assert.That(TutorialManager.HasCompletedResearch(
            new List<ResearchState> { state }), Is.False);

        state.SetStatusForEditor(ResearchStatus.Completed);
        Assert.That(TutorialManager.HasCompletedResearch(
            new List<ResearchState> { state }), Is.True);

        UnityEngine.Object.DestroyImmediate(definition);
    }

    [Test]
    public void ChainGuidanceHandlesPositiveButInsufficientProductivityAndRequiresProducedStock()
    {
        var host = new GameObject("Tutorial-OperatingChain");
        try
        {
            var resources = host.AddComponent<ResourceManager>();
            var game = host.AddComponent<GameManager>();
            var buildings = host.AddComponent<BuildingManager>();
            var research = host.AddComponent<ResearchManager>();
            research.InitializeForEditor();
            game.State.Population.RestorePopulationForEditor(new ExpantaNum(5));
            game.State.Population.AdjustPopulationCapacityForEditor(new ExpantaNum(5));
            Building source = DataBase<Building>.Find("Quarry");
            Building target = DataBase<Building>.Find("StoneCuttingWorkshop");
            buildings.EnsureBuilding(source).SetAmountForEditor(ExpantaNum.One);
            Assert.That(buildings.AvailableProductivity, Is.GreaterThan(ExpantaNum.Zero));
            Assert.That(buildings.AvailableProductivity, Is.LessThan(target.ProductivityConsumption));
            TutorialManager tutorial = TutorialManager.Ensure();
            tutorial.RestoreSaveData(new SaveManager.TutorialSaveData
            {
                ActiveStepId = "production-chain",
                CompletedStepIds = new List<string> { "orientation", "resources", "building", "population", "research" }
            }, TechLevel.Animal);
            tutorial.RecordPageVisitedForEditor("Buildings");
            TutorialSnapshot shortage = tutorial.Evaluate();
            Assert.That(shortage.NavigationPage, Is.EqualTo("Buildings")); // Page protocol.
            Assert.That(shortage.NavigationTargetId, Is.EqualTo("WoodHouse")); // Stable recovery target.
            BuildingState processing = buildings.EnsureBuilding(target);
            processing.SetAmountForEditor(ExpantaNum.One);
            Resource product = target.ResourceGenerationRates[0].First;
            resources.SetAmount(product, ExpantaNum.Zero);
            Assert.That(tutorial.Evaluate().StepId, Is.EqualTo("production-chain"));
            resources.SetAmount(product, ExpantaNum.One);
            processing.SetEfficiencyForEditor(ExpantaNum.Zero);
            Assert.That(tutorial.Evaluate().StepId, Is.EqualTo("production-chain"));
            processing.SetEfficiencyForEditor(ExpantaNum.One);
            Assert.That(tutorial.Evaluate().StepId, Is.Not.EqualTo("production-chain"));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void ResearchRoleIncludesDirectEffectsAndBuildingUnlocksTogether()
    {
        Research agriculture = DataBase<Research>.Find("Agriculture");
        Building farm = DataBase<Building>.Find("Farm");
        Assert.That(agriculture.Effects.Count, Is.GreaterThan(0));
        Assert.That(farm.RequiredResearch, Does.Contain(agriculture));
        string role = TutorialManager.DescribeResearchRole(agriculture);
        Assert.That(role, Does.Contain(agriculture.Effects[0].Type.GetDescription()));
        Assert.That(role, Does.Contain(farm.Label));
    }

    [Test]
    public void ProductionChainRequiresMatchingOutputAndInputAcrossBuildings()
    {
        Resource intermediate = CreateResource("TutorialChainIntermediate");
        Resource product = CreateResource("TutorialChainProduct");
        Building producer = CreateBuilding("TutorialProducer");
        Building consumer = CreateBuilding("TutorialConsumer");
        producer.ConfigureEconomyForEditor(
            new ExpantaNum(1.15d), ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.One, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero,
            new List<Pair<Resource, ExpantaNum>>(),
            new List<Pair<Resource, ExpantaNum>>
            {
                new Pair<Resource, ExpantaNum>(intermediate, ExpantaNum.One)
            },
            new List<Pair<Resource, ExpantaNum>>());
        consumer.ConfigureEconomyForEditor(
            new ExpantaNum(1.15d), ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.One, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero,
            new List<Pair<Resource, ExpantaNum>>(),
            new List<Pair<Resource, ExpantaNum>>
            {
                new Pair<Resource, ExpantaNum>(product, ExpantaNum.One)
            },
            new List<Pair<Resource, ExpantaNum>>
            {
                new Pair<Resource, ExpantaNum>(intermediate, ExpantaNum.One)
            });

        BuildingState producerState = new BuildingState(producer);
        BuildingState consumerState = new BuildingState(consumer);
        producerState.SetAmountForEditor(ExpantaNum.One);
        consumerState.SetAmountForEditor(ExpantaNum.One);
        var ownedStates = new List<BuildingState> { producerState, consumerState };

        Assert.That(TutorialManager.HasOwnedProductionChain(
            new List<BuildingState> { producerState }), Is.False);
        Assert.That(TutorialManager.HasOwnedProductionChain(
            ownedStates), Is.True);

        consumerState.SetEfficiencyForEditor(ExpantaNum.Zero);
        Assert.That(TutorialManager.HasOwnedProductionChain(ownedStates), Is.False);
        consumerState.SetEfficiencyForEditor(ExpantaNum.One);
        producerState.SetEfficiencyForEditor(ExpantaNum.Zero);
        Assert.That(TutorialManager.HasOwnedProductionChain(ownedStates), Is.False);
        producerState.SetEfficiencyForEditor(ExpantaNum.One);

        Resource consumed = null;
        Resource generated = null;
        Assert.That(TutorialManager.TryFindOwnedProductionChainForEditor(
            ownedStates, out consumed, out generated), Is.True);
        Assert.That(consumed, Is.SameAs(intermediate));
        Assert.That(generated, Is.SameAs(product));

        UnityEngine.Object.DestroyImmediate(intermediate);
        UnityEngine.Object.DestroyImmediate(product);
        UnityEngine.Object.DestroyImmediate(producer);
        UnityEngine.Object.DestroyImmediate(consumer);
    }

    private static bool ContainsId(
        IReadOnlyList<TutorialStepDefinition> definitions, string id)
    {
        for (int i = 0; i < definitions.Count; i++)
            if (definitions[i] != null && definitions[i].Id == id)
                return true;
        return false;
    }

    private static string FindRootId(
        IReadOnlyList<TutorialStepDefinition> definitions)
    {
        for (int i = 0; i < definitions.Count; i++)
        {
            bool referenced = false;
            for (int j = 0; j < definitions.Count; j++)
                if (definitions[j] != null && definitions[j].NextStepId == definitions[i].Id)
                {
                    referenced = true;
                    break;
                }
            if (!referenced)
                return definitions[i].Id;
        }
        return string.Empty;
    }

    private static Resource CreateResource(string id)
    {
        Resource resource = ScriptableObject.CreateInstance<Resource>();
        resource.SetIdForEditor(id);
        resource.Label = id;
        return resource;
    }

    private static Building CreateBuilding(string id)
    {
        Building building = ScriptableObject.CreateInstance<Building>();
        building.SetIdForEditor(id);
        building.Label = id;
        return building;
    }
}
