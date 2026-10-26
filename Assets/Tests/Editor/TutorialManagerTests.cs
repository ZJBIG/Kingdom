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

    [TestCase(0, "AzurePool")]
    [TestCase(1, "ThunderGate")]
    [TestCase(2, "AzurePool")]
    [TestCase(3, "AzurePool")]
    [TestCase(4, "AlphaCentauri")]
    [TestCase(5, "AlphaCentauri")]
    [TestCase(6, "AlphaCentauri")]
    [TestCase(7, "AlphaCentauri")]
    [TestCase(8, "AlphaCentauri")]
    public void SpacerGuidanceConnectsLocalDiscoveriesToConstructionAndFirstDistantUnlock(int scenario, string expectedTarget)
    {
        GameObject host = new GameObject("Tutorial-PlanetDevelopment-Guidance");
        try
        {
            ResourceManager resources = host.AddComponent<ResourceManager>();
            resources.InitializeForEditor();
            GameManager game = host.AddComponent<GameManager>();
            BuildingManager buildings = host.AddComponent<BuildingManager>();
            ResearchManager research = host.AddComponent<ResearchManager>();
            research.InitializeForEditor();
            game.State.AdvanceTechLevelForEditor(TechLevel.Spacer);
            foreach (ResearchState state in research.States.Values)
                state.SetStatusForEditor(ResearchStatus.Completed);
            ProgressionModifierManager.Rebuild(new List<ResearchState>(research.States.Values));
            foreach (string id in new[] { "LaunchCenter", "OrbitalStation", "Shipyard" })
                buildings.EnsureBuilding(DataBase<BuildingDef>.Find(id)).SetAmountForEditor(ExpantaNum.One);
            game.Sectors.InitializeDefinitions();
            foreach (SectorState state in game.Sectors.OrderedStates)
            {
                if (!state.Definition.IsHomeSystem)
                {
                    if (scenario < 4)
                    {
                        state.SetUnlockedForEditor(true);
                        state.SetOccupiedForEditor(true);
                    }
                    continue;
                }
                state.SetUnlockedForEditor(true);
                state.SetOccupiedForEditor(true);
                state.SetCampaignProgressForEditor(ExpantaNum.One);
                if (state.Definition.InteriorExploration == null) continue;
                state.SetInteriorExplorationForEditor(ExpantaNum.One);
                foreach (SectorBuildingDef building in buildings.GetSectorBuildings(state.Definition))
                    if (building.RequiresInteriorExploration && (scenario == 4 || state.Definition.Id != "AzurePool"))
                        buildings.EnsureBuilding(building).SetAmountForEditor(ExpantaNum.One);
            }
            SectorState azure = game.Sectors.GetState(DataBase<SectorDef>.Find("AzurePool"));
            if (scenario == 0 || scenario == 1 || scenario == 5) azure.SetInteriorExplorationForEditor(ExpantaNum.Zero);
            if (scenario == 2 || scenario == 6) azure.SetInteriorExplorationForEditor(new ExpantaNum(0.3d), false, true);
            if (scenario == 1)
                game.Sectors.GetState(DataBase<SectorDef>.Find("ThunderGate"))
                    .SetInteriorExplorationForEditor(new ExpantaNum(0.2d), true);
            SectorState alpha = game.Sectors.GetState(DataBase<SectorDef>.Find("AlphaCentauri"));
            if (scenario == 7 || scenario == 8)
            {
                alpha.SetUnlockedForEditor(true);
                alpha.SetCampaignProgressForEditor(scenario == 8 ? ExpantaNum.One : new ExpantaNum(0.37d));
            }
            ExpantaNum before = azure.InteriorExplorationProgress;
            TutorialSnapshot snapshot = new TutorialSnapshot();
            Assert.That(TutorialManager.BuildSpacerGuidanceForEditor(snapshot, game, research, buildings), Is.True);
            // Exact values are navigation protocol IDs; descriptive labels come from definitions.
            Assert.That(snapshot.NavigationPage, Is.EqualTo("Sectors"));
            Assert.That(snapshot.NavigationTargetId, Is.EqualTo(expectedTarget));
            if (scenario == 1)
            {
                var preview = game.Sectors.GetInteriorExplorationPreview(DataBase<SectorDef>.Find("ThunderGate"));
                Assert.That(preview.Status, Is.EqualTo(SectorInteriorExplorationStatus.WaitingForSupply));
                Assert.That(snapshot.Blocker, Does.Contain(preview.MissingResource.Label));
            }
            if (scenario == 3)
                Assert.That(snapshot.Blocker, Does.Contain(DataBase<SectorBuildingDef>.Find("AzureIceSeaArk").Label));
            Assert.That(azure.InteriorExplorationProgress, Is.EqualTo(before), "Guidance must not advance exploration.");
            if (scenario == 5 || scenario == 6)
                Assert.That(snapshot.RecommendedAction, Does.Contain(azure.Definition.InteriorExploration.Label),
                    "Optional local development must remain visible without hiding first departure navigation.");
            if (scenario == 7)
                Assert.That(snapshot.RecommendedAction, Does.Contain("继续远征").And.Not.Contain("占领确认"),
                    "A paused partial campaign must not suggest an occupation that the manager rejects.");
            if (scenario == 8)
                Assert.That(snapshot.RecommendedAction, Does.Contain("确认占领").And.Not.Contain("继续远征"));
            Assert.That(alpha.Unlocked, Is.EqualTo(scenario < 4 || scenario == 7 || scenario == 8), "Guidance must not unlock or start a distant expedition.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            ProgressionModifierManager.Rebuild(null);
        }
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

    [TestCase(0, "Research", "HomeSystemSurvey")]
    [TestCase(1, "Buildings", "LaunchCenter")]
    [TestCase(2, "Research", "OrbitalEngineering")]
    [TestCase(3, "Sectors", "DawnRing")]
    [TestCase(4, "Sectors", "AzurePool")]
    [TestCase(5, "Sectors", "ThunderGate")]
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
            foreach (KeyValuePair<ResearchDef, ResearchState> pair in research.States)
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
                buildings.EnsureBuilding(DataBase<BuildingDef>.Find("LaunchCenter"))
                    .SetAmountForEditor(ExpantaNum.One);

            SectorState dawn = game.Sectors.GetState(
                DataBase<SectorDef>.Find("DawnRing"));
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
                    DataBase<SectorDef>.Find("ThunderGate"));
                active.SetUnlockedForEditor(true);
                active.SetColonizationActiveForEditor(true);
                Assert.That(game.Sectors.CanAccess(
                    DataBase<SectorDef>.Find("HeliosCore")), Is.True,
                    "Active home-system exploration takes priority over another reachable destination.");
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
                    DataBase<SectorDef>.Find("AzurePool")).Occupied, Is.False,
                    "After the first outpost, guidance must continue through the home system.");
            if (expectedPage == "Sectors")
            {
                SectorDef target = DataBase<SectorDef>.Find(expectedTarget);
                Assert.That(target.IsHomeSystem, Is.True);
                Assert.That(game.Sectors.CanAccess(target), Is.True);
                Assert.That(game.Sectors.GetUnlockFailure(target),
                    Is.EqualTo(SectorOperationFailure.None)
                        .Or.EqualTo(SectorOperationFailure.AlreadyUnlocked));
            }
            Assert.That(research.GetState(DataBase<ResearchDef>.Find("DeepSpaceFleet")).Status,
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
            Assert.That(TutorialManager.GetPopulationNavigationTargetForEditor(game, buildings),
                Is.EqualTo("WoodHouse"), "Zero population capacity must first target starting housing.");
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
        ResearchDef definition = ScriptableObject.CreateInstance<ResearchDef>();
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
    public void ResearchGuidanceShowsQueuedPaymentBlockerAndDoesNotRestartResearch()
    {
        var host = new GameObject("Tutorial-QueuedResearch");
        try
        {
            var resources = host.AddComponent<ResourceManager>();
            host.AddComponent<GameManager>();
            host.AddComponent<BuildingManager>();
            var research = host.AddComponent<ResearchManager>();
            research.InitializeForEditor();
            ResearchDef definition = DataBase<ResearchDef>.Find("Agriculture");
            ResourceDef costResource = definition.ResourceRequirements[0].First;
            resources.SetAmount(costResource, ExpantaNum.Zero);
            Assert.That(research.EnqueueResearch(definition), Is.True);
            Assert.That(research.ActiveResearch, Is.Null);
            Assert.That(research.CanPayResearchCost(definition, out string blocker), Is.False);

            TutorialManager tutorial = TutorialManager.Ensure();
            tutorial.RestoreSaveData(new SaveManager.TutorialSaveData
            {
                ActiveStepId = "research"
            }, TechLevel.Animal);
            tutorial.RecordPageVisitedForEditor("Research");
            TutorialSnapshot snapshot = tutorial.Evaluate();
            Assert.That(snapshot.NavigationPage, Is.EqualTo("Research")); // Page protocol.
            Assert.That(snapshot.NavigationTargetId, Is.EqualTo(definition.Id)); // Stable target.
            Assert.That(snapshot.Blocker, Is.EqualTo(blocker)); // Same manager-owned failure.
            Assert.That(research.IsQueued(definition), Is.True);
            Assert.That(resources.GetAmount(costResource), Is.EqualTo(ExpantaNum.Zero));

            for (int i = 0; i < definition.ResourceRequirements.Count; i++)
            {
                Pair<ResourceDef, ExpantaNum> cost = definition.ResourceRequirements[i];
                resources.SetAmount(cost.First, cost.Second);
            }
            research.Tick(0d);
            Assert.That(research.ActiveResearch.Definition, Is.SameAs(definition));
            Assert.That(research.ActiveResearch.CostPaid, Is.True);
            Assert.That(research.IsQueued(definition), Is.False);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [TestCase(TechLevel.Animal, "WoodHouse")]
    [TestCase(TechLevel.StoneAge, "StoneHouse")]
    public void PopulationGuidanceAllowsMoreHousingButRespectsUnlockedChainTier(
        TechLevel era, string expectedId)
    {
        var host = new GameObject("Tutorial-HousingTier");
        try
        {
            host.AddComponent<ResourceManager>();
            var game = host.AddComponent<GameManager>();
            var buildings = host.AddComponent<BuildingManager>();
            var research = host.AddComponent<ResearchManager>();
            research.InitializeForEditor();
            game.State.AdvanceTechLevelForEditor(era);
            foreach (ResearchState state in research.States.Values)
                state.SetStatusForEditor(ResearchStatus.Completed);
            BuildingDef woodHouse = DataBase<BuildingDef>.Find("WoodHouse");
            buildings.EnsureBuilding(woodHouse).SetAmountForEditor(ExpantaNum.One);

            string targetId = TutorialManager.GetPopulationNavigationTargetForEditor(game, buildings);
            Assert.That(targetId, Is.EqualTo(expectedId)); // Stable chain tier ID.
            Assert.That(buildings.CanConstructNew(DataBase<BuildingDef>.Find(targetId)), Is.True);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void ChainGuidancePrioritizesActualPrerequisitesAndRequiresProducedStock()
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
            buildings.EnsureBuilding(DataBase<BuildingDef>.Find("WoodHouse"))
                .SetAmountForEditor(ExpantaNum.One);
            BuildingDef source = DataBase<BuildingDef>.Find("Quarry");
            BuildingDef target = DataBase<BuildingDef>.Find("StoneCuttingWorkshop");
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
            Assert.That(shortage.NavigationPage, Is.EqualTo("Research")); // Page protocol.
            Assert.That(DataBase<ResearchDef>.TryFind(shortage.NavigationTargetId, out ResearchDef prerequisite), Is.True);
            Assert.That(research.IsResearchCompleted(prerequisite.Id), Is.False,
                "Guidance must solve an actual prerequisite rather than treating labor demand as a construction gate.");
            BuildingState processing = buildings.EnsureBuilding(target);
            processing.SetAmountForEditor(ExpantaNum.One);
            ResourceDef product = target.ResourceGenerationRates[0].First;
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
        ResearchDef agriculture = DataBase<ResearchDef>.Find("Agriculture");
        BuildingDef farm = DataBase<BuildingDef>.Find("Farm");
        Assert.That(agriculture.Effects.Count, Is.GreaterThan(0));
        Assert.That(farm.RequiredResearch, Does.Contain(agriculture));
        string role = TutorialManager.DescribeResearchRole(agriculture);
        Assert.That(role, Does.Contain(agriculture.Effects[0].Type.GetDescription()));
        Assert.That(role, Does.Contain(farm.Label));
    }

    [Test]
    public void ProductionChainRequiresMatchingOutputAndInputAcrossBuildings()
    {
        ResourceDef intermediate = CreateResource("TutorialChainIntermediate");
        ResourceDef product = CreateResource("TutorialChainProduct");
        BuildingDef producer = CreateBuilding("TutorialProducer");
        BuildingDef consumer = CreateBuilding("TutorialConsumer");
        producer.ConfigureEconomyForEditor(
            new ExpantaNum(1.15d), ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.One, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero,
            new List<Pair<ResourceDef, ExpantaNum>>(),
            new List<Pair<ResourceDef, ExpantaNum>>
            {
                new Pair<ResourceDef, ExpantaNum>(intermediate, ExpantaNum.One)
            },
            new List<Pair<ResourceDef, ExpantaNum>>());
        consumer.ConfigureEconomyForEditor(
            new ExpantaNum(1.15d), ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.One, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero, ExpantaNum.Zero,
            ExpantaNum.Zero,
            new List<Pair<ResourceDef, ExpantaNum>>(),
            new List<Pair<ResourceDef, ExpantaNum>>
            {
                new Pair<ResourceDef, ExpantaNum>(product, ExpantaNum.One)
            },
            new List<Pair<ResourceDef, ExpantaNum>>
            {
                new Pair<ResourceDef, ExpantaNum>(intermediate, ExpantaNum.One)
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

        ResourceDef consumed = null;
        ResourceDef generated = null;
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

    private static ResourceDef CreateResource(string id)
    {
        ResourceDef resource = ScriptableObject.CreateInstance<ResourceDef>();
        resource.SetIdForEditor(id);
        resource.Label = id;
        return resource;
    }

    private static BuildingDef CreateBuilding(string id)
    {
        BuildingDef building = ScriptableObject.CreateInstance<BuildingDef>();
        building.SetIdForEditor(id);
        building.Label = id;
        return building;
    }
}
