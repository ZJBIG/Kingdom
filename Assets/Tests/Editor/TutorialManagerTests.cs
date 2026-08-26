using System;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
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

        Assert.That(manager.Steps.Count, Is.EqualTo(8));
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
    public void CompletedGoalFeedbackUsesCompletedDirectPredecessor()
    {
        TutorialManager manager = TutorialManager.Ensure();
        Invoke(manager, "RestoreSaveData", new SaveManager.TutorialSaveData
        {
            ActiveStepId = "resources",
            CompletedStepIds = new List<string> { "orientation" }
        }, TechLevel.Animal);

        TutorialStep completed = Invoke(
            manager, "FindPreviousCompletedStep", "resources") as TutorialStep;
        Assert.That(completed, Is.Not.Null);
        Assert.That(completed.Id, Is.EqualTo("orientation"));

        Invoke(manager, "RestoreSaveData", new SaveManager.TutorialSaveData
        {
            ActiveStepId = "resources",
            CompletedStepIds = new List<string>()
        }, TechLevel.Animal);
        Assert.That(Invoke(
            manager, "FindPreviousCompletedStep", "resources"), Is.Null);
    }

    [Test]
    public void EveryTechLevelHasCivilizationContext()
    {
        Array values = Enum.GetValues(typeof(TechLevel));
        for (int i = 0; i < values.Length; i++)
        {
            TechLevel era = (TechLevel)values.GetValue(i);
            Assert.That(InvokeStatic("GetCivilizationContext", era), Is.Not.Empty,
                "Every existing era should explain the mouse civilization revival.");
        }
    }

    [Test]
    public void SpacerGuidanceUsesExistingResearchAndBuildingDefinitions()
    {
        string[] researchIds =
        {
            "FirstContact",
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

        MethodInfo spacerGuidance = typeof(TutorialManager).GetMethod(
            "BuildSpacerGuidance", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(spacerGuidance, Is.Not.Null);
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

        Assert.That(Invoke(manager, "HasVisitedPageForStep", orientation), Is.False);
        Invoke(manager, "RecordPageVisited", "Resources");
        Assert.That(Invoke(manager, "HasVisitedPageForStep", orientation), Is.False);
        Invoke(manager, "RecordPageVisited", "Overview");
        Assert.That(Invoke(manager, "HasVisitedPageForStep", orientation), Is.True);
        Assert.That(Invoke(manager, "HasVisitedPageForStep", resources), Is.False);

        Invoke(manager, "ResetForNewGame");
        Assert.That(Invoke(manager, "HasVisitedPageForStep", orientation), Is.False);
    }

    [Test]
    public void SaveSessionVersionChangesOnlyWhenRuntimeProgressIsRestored()
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

        Invoke(manager, "ResetForNewGame");
        Assert.That(manager.SaveSessionVersion, Is.GreaterThan(restored));
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
        Invoke(state, "RestorePopulation", ExpantaNum.One);
        Invoke(state, "SetFoodAvailability", new ExpantaNum(0.5));
        Assert.That(InvokeStatic("GetNavigationPageForStep", population, state),
            Is.EqualTo("Resources"));
        Assert.That(InvokeStatic("GetNavigationPageForStep", population, new GameState()),
            Is.EqualTo("Buildings"));
    }

    [Test]
    public void PopulationStepRequiresPopulationStateToGrow()
    {
        GameState state = new GameState();
        Invoke(state, "RestorePopulationCapacityExact",
            new ExpantaNum(2), ExpantaNum.Zero);

        Assert.That(InvokeStatic("HasObservedPopulationGrowth", state), Is.False);

        Invoke(state, "RestorePopulation", ExpantaNum.One);
        Assert.That(InvokeStatic("HasObservedPopulationGrowth", state), Is.True);
    }

    [Test]
    public void PopulationCapacityConditionMatchesItsDeclaredMeaning()
    {
        GameObject host = new GameObject("TutorialConditionGameManager");
        GameManager game = host.AddComponent<GameManager>();
        Invoke(game.State, "RestorePopulationCapacityExact",
            new ExpantaNum(2), ExpantaNum.Zero);
        TutorialStep step = new TutorialStep(
            "capacity", "Capacity", "Capacity", TutorialStepKind.Population,
            string.Empty, string.Empty, "game-state",
            "population-capacity-positive", "Buildings");

        try
        {
            Assert.That(InvokeStatic("IsStepComplete", step, game, null, null, null),
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

        Assert.That(InvokeStatic("HasCompletedResearch",
            new List<ResearchState> { state }), Is.False);

        Invoke(state, "SetStatus", ResearchStatus.Completed);
        Assert.That(InvokeStatic("HasCompletedResearch",
            new List<ResearchState> { state }), Is.True);

        UnityEngine.Object.DestroyImmediate(definition);
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

        Assert.That(InvokeStatic("HasOwnedProductionChain",
            new List<BuildingState> { producerState }), Is.False);
        Assert.That(InvokeStatic("HasOwnedProductionChain",
            ownedStates), Is.True);

        MethodInfo findChain = typeof(TutorialManager).GetMethod(
            "TryFindOwnedProductionChain",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[]
            {
                typeof(IEnumerable<BuildingState>),
                typeof(Resource).MakeByRefType(),
                typeof(Resource).MakeByRefType()
            },
            null);
        Assert.That(findChain, Is.Not.Null);
        object[] arguments = { ownedStates, null, null };
        Assert.That(findChain.Invoke(null, arguments), Is.True);
        Assert.That(arguments[1], Is.SameAs(intermediate));
        Assert.That(arguments[2], Is.SameAs(product));

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

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName + " should exist.");
        return method.Invoke(target, arguments);
    }

    private static object InvokeStatic(string methodName, params object[] arguments)
    {
        MethodInfo method = null;
        MethodInfo[] methods = typeof(TutorialManager).GetMethods(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
        {
            if (methods[i].Name != methodName)
                continue;
            ParameterInfo[] parameters = methods[i].GetParameters();
            if (parameters.Length != arguments.Length)
                continue;
            bool matches = true;
            for (int j = 0; j < parameters.Length; j++)
                if (arguments[j] != null &&
                    !parameters[j].ParameterType.IsInstanceOfType(arguments[j]))
                {
                    matches = false;
                    break;
                }
            if (matches)
            {
                method = methods[i];
                break;
            }
        }
        Assert.That(method, Is.Not.Null, methodName + " should exist.");
        return method.Invoke(null, arguments);
    }
}
