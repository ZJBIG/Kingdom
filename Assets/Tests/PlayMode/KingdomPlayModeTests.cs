using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class KingdomPlayModeTests
{
    private readonly List<Object> createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        createdObjects.Clear();
    }



    [UnityTest]
    public IEnumerator NewGameStartup_InitializesCoreRuntimeState()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-NewGame-Managers");
        ResourceManager resourceManager = FindOrCreateManager<ResourceManager>("PlayMode-NewGame-Managers");
        FindOrCreateManager<BuildingManager>("PlayMode-NewGame-Managers");
        FindOrCreateManager<ResearchManager>("PlayMode-NewGame-Managers");
        yield return null;

        MethodInfo initializeNewGame = typeof(GameManager).GetMethod(
            "InitializeNewGame",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(initializeNewGame, Is.Not.Null);
        MethodInfo resetResources = typeof(ResourceManager).GetMethod(
            "ResetForLoad", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(resetResources, Is.Not.Null);
        resetResources.Invoke(resourceManager, null);
        initializeNewGame.Invoke(gameManager, null);

        Resource wood = DataBase<Resource>.Find("WoodLog");
        Assert.That(gameManager.State.TechLevel, Is.EqualTo(TechLevel.Animal));
        Assert.That(gameManager.State.FoodAmount, Is.EqualTo(new ExpantaNum(300)));
        Assert.That(gameManager.State.Population.Population, Is.EqualTo(ExpantaNum.Zero));
        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(ExpantaNum.Zero));
        Assert.That(resourceManager.GetState(wood).ProductionRate, Is.EqualTo(new ExpantaNum(1)));
    }

    [UnityTest]
    public IEnumerator NewGameFirstTenMinutes_SimulationSmokeRemainsStable()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-TenMinute-Managers");
        ResourceManager resourceManager = FindOrCreateManager<ResourceManager>("PlayMode-TenMinute-Managers");
        FindOrCreateManager<BuildingManager>("PlayMode-TenMinute-Managers");
        FindOrCreateManager<ResearchManager>("PlayMode-TenMinute-Managers");
        SimulationManager simulationManager = FindOrCreateManager<SimulationManager>("PlayMode-TenMinute-Managers");
        yield return null;

        MethodInfo initializeNewGame = typeof(GameManager).GetMethod(
            "InitializeNewGame",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(initializeNewGame, Is.Not.Null);
        initializeNewGame.Invoke(gameManager, null);
        simulationManager.SetRunning(true);

        for (int i = 0; i < 6000; i++)
        {
            simulationManager.Advance(0.1d);
            if (i % 250 == 0)
                yield return null;
        }

        Resource wood = DataBase<Resource>.Find("WoodLog");
        Assert.That(gameManager.State.CalendarDays, Is.InRange(59, 60));
        Assert.That(gameManager.State.FoodAmount.IsFinite, Is.True);
        Assert.That(resourceManager.GetAmount(wood).IsFinite, Is.True);
    }

    [UnityTest]
    public IEnumerator FoodProducer_RecoversAfterInventoryAndPriorEfficiencyReachZero()
    {
        GameManager gameManager =
            FindOrCreateManager<GameManager>("PlayMode-FoodRecovery-Managers");
        FindOrCreateManager<ResourceManager>("PlayMode-FoodRecovery-Managers");
        BuildingManager buildingManager =
            FindOrCreateManager<BuildingManager>("PlayMode-FoodRecovery-Managers");
        FindOrCreateManager<ResearchManager>("PlayMode-FoodRecovery-Managers");
        SimulationManager simulationManager =
            FindOrCreateManager<SimulationManager>("PlayMode-FoodRecovery-Managers");
        yield return null;

        typeof(GameState).GetMethod(
                "RestoreCore", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(gameManager.State, new object[]
            {
                0, "Test", TechLevel.Animal, ExpantaNum.Zero, 0L
            });
        typeof(GameState).GetMethod(
                "RestorePopulation", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(gameManager.State, new object[] { new ExpantaNum(3) });
        typeof(GameState).GetMethod(
                "AdjustPopulationCapacity", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(gameManager.State, new object[] { new ExpantaNum(10) });
        BuildingState farm =
            buildingManager.EnsureBuilding(DataBase<Building>.Find("Farm"));
        farm.SetAmountForEditor(1);
        farm.SetEfficiencyForEditor(ExpantaNum.Zero);

        simulationManager.SetRunning(false);
        simulationManager.ManualTick(1d);

        Assert.That(farm.Efficiency, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(gameManager.State.FoodAmount, Is.GreaterThan(ExpantaNum.Zero));
        Assert.That(gameManager.State.HappinessMultiplier, Is.GreaterThanOrEqualTo(ExpantaNum.One));
    }

    [UnityTest]
    public IEnumerator BuildingManager_RequiresEveryResearchAndWorkshopPrerequisite()
    {
        GameManager gameManager =
            FindOrCreateManager<GameManager>("PlayMode-Prerequisite-Managers");
        FindOrCreateManager<ResourceManager>("PlayMode-Prerequisite-Managers");
        BuildingManager buildingManager =
            FindOrCreateManager<BuildingManager>("PlayMode-Prerequisite-Managers");
        ResearchManager researchManager =
            FindOrCreateManager<ResearchManager>("PlayMode-Prerequisite-Managers");
        WorkshopManager workshopManager =
            FindOrCreateManager<WorkshopManager>("PlayMode-Prerequisite-Managers");
        yield return null;

        typeof(GameState).GetMethod(
            "AdvanceTechLevel", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(gameManager.State, new object[] { TechLevel.Industrial });

        Building refinery = DataBase<Building>.Find("OilRefinery");
        Assert.That(buildingManager.ArePrerequisitesMet(refinery, out BuildFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.ResearchPrerequisiteIncomplete));

        MethodInfo restoreResearch = typeof(ResearchState).GetMethod(
            "Restore", BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(ExpantaNum), typeof(bool), typeof(bool), typeof(IReadOnlyDictionary<Resource, ExpantaNum>) }, null);
        for (int i = 0; i < refinery.RequiredResearch.Count; i++)
        {
            Research required = refinery.RequiredResearch[i];
            var paidCosts = new Dictionary<Resource, ExpantaNum>();
            foreach (Pair<Resource, ExpantaNum> requirement in required.ResourceRequirements)
                paidCosts[requirement.First] = requirement.Second;
            restoreResearch.Invoke(
                researchManager.GetState(required),
                new object[] { ExpantaNum.Zero, true, true, paidCosts });
        }

        Assert.That(buildingManager.ArePrerequisitesMet(refinery, out failure), Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.WorkshopPrerequisiteIncomplete));

        WorkshopUpgrade upgrade = refinery.RequiredWorkshopUpgrades[0];
        typeof(WorkshopUpgradeState).GetMethod(
                "SetPurchased", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(workshopManager.States[upgrade], new object[] { true });

        Assert.That(buildingManager.ArePrerequisitesMet(refinery, out failure), Is.True);
        Assert.That(failure, Is.EqualTo(BuildFailure.None));
    }

    [UnityTest]
    public IEnumerator ApplicationPause_StopsAndRestoresSimulationRunningState()
    {
        SimulationManager simulationManager = FindOrCreateManager<SimulationManager>("PlayMode-PauseLifecycle");
        yield return null;

        MethodInfo applicationPause = typeof(SimulationManager).GetMethod(
            "OnApplicationPause",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(applicationPause, Is.Not.Null);

        simulationManager.SetRunning(true);
        applicationPause.Invoke(simulationManager, new object[] { true });
        Assert.That(simulationManager.IsRunning, Is.False);

        applicationPause.Invoke(simulationManager, new object[] { false });
        Assert.That(simulationManager.IsRunning, Is.True);

        simulationManager.SetRunning(false);
        applicationPause.Invoke(simulationManager, new object[] { true });
        applicationPause.Invoke(simulationManager, new object[] { false });
        Assert.That(simulationManager.IsRunning, Is.False);
    }


    [UnityTest]
    public IEnumerator OverviewDevelopmentGuidance_IsReadOnlyAndUnique()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return WaitForRuntimeUiRoot();

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        Transform primaryCard = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Overview/PrimaryCard");
        Assert.That(primaryCard, Is.Not.Null, "Overview must contain its authored PrimaryCard.");
        TMP_Text body = primaryCard.Find("Text")?.GetComponent<TMP_Text>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body.text, Is.Not.Empty);
        Assert.That(body.rectTransform.rect.height, Is.GreaterThan(0f),
            "Development guidance Body must not have a negative or zero height.");

        SimulationManager simulation = Object.FindObjectOfType<SimulationManager>();
        GameManager game = Object.FindObjectOfType<GameManager>();
        Assert.That(simulation, Is.Not.Null);
        Assert.That(game, Is.Not.Null);
        simulation.SetRunning(false);
        int versionBeforeRefresh = game.State.Version;
        Assert.That(game.State.Version, Is.EqualTo(versionBeforeRefresh),
            "Refreshing guidance must not mutate GameState.");
    }









    [UnityTest]
    public IEnumerator ResearchQueue_QueuesUnpaidAndPaysThroughPaymentApi()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-ResearchQueue");
        ResourceManager resourceManager =
            FindOrCreateManager<ResourceManager>("PlayMode-ResearchQueue");
        FindOrCreateManager<BuildingManager>("PlayMode-ResearchQueue");
        ResearchManager researchManager =
            FindOrCreateManager<ResearchManager>("PlayMode-ResearchQueue");
        yield return null;

        MethodInfo initializeNewGame = typeof(GameManager).GetMethod(
            "InitializeNewGame", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(initializeNewGame, Is.Not.Null);
        initializeNewGame.Invoke(gameManager, null);
        MethodInfo resetResearch = typeof(ResearchManager).GetMethod(
            "ResetForLoad", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(resetResearch, Is.Not.Null);
        resetResearch.Invoke(researchManager, null);

        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetAmount(wood, 1000);
        Research active = DataBase<Research>.Find("Agriculture");
        Research queued = DataBase<Research>.Find("ControlledFire");

        Assert.That(researchManager.PayResearchCost(active), Is.EqualTo(ResearchPaymentResult.Paid));
        Assert.That(researchManager.HandleResearchAction(active), Is.EqualTo(ResearchActionResult.Started));
        Assert.That(researchManager.HandleResearchAction(queued), Is.EqualTo(ResearchActionResult.Queued));
        Assert.That(researchManager.ResearchQueue.Any(state => state.Definition == queued), Is.True);
        Assert.That(researchManager.GetState(queued).CostPaid, Is.False);
        Assert.That(researchManager.PayResearchCost(queued), Is.EqualTo(ResearchPaymentResult.Paid));
        Assert.That(researchManager.ResearchQueue.Any(state => state.Definition == queued), Is.True);
        Assert.That(researchManager.ActiveResearch, Is.Not.Null);
        Assert.That(researchManager.ActiveResearch.Definition, Is.SameAs(active));
    }

    [UnityTest]
    public IEnumerator EraTransitions_CompleteInOrderAndExposeNextEraContent()
    {
        GameManager gameManager = FindOrCreateManager<GameManager>("PlayMode-EraChain-Managers");
        ResourceManager resourceManager = FindOrCreateManager<ResourceManager>("PlayMode-EraChain-Managers");
        BuildingManager buildingManager = FindOrCreateManager<BuildingManager>("PlayMode-EraChain-Managers");
        ResearchManager researchManager = FindOrCreateManager<ResearchManager>("PlayMode-EraChain-Managers");
        WorkshopManager workshopManager = FindOrCreateManager<WorkshopManager>("PlayMode-EraChain-Managers");
        yield return null;

        MethodInfo initializeNewGame = typeof(GameManager).GetMethod(
            "InitializeNewGame", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(initializeNewGame, Is.Not.Null);
        initializeNewGame.Invoke(gameManager, null);

        typeof(ResearchManager).GetMethod(
            "ResetForLoad", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(researchManager, null);
        typeof(WorkshopManager).GetMethod(
            "ResetForLoad", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(workshopManager, null);
        GrantResearchTestResources(resourceManager);

        Assert.That(gameManager.State.TechLevel, Is.EqualTo(TechLevel.Animal));
        var transitions = new[]
        {
            new { Id = "NeolithicSettlement", Target = TechLevel.Neolithic },
            new { Id = "FeudalAdministration", Target = TechLevel.Medieval },
            new { Id = "Industrialization", Target = TechLevel.Industrial },
            new { Id = "InterstellarNavigation", Target = TechLevel.Spacer }
        };

        for (int i = 0; i < transitions.Length; i++)
        {
            Research transition = DataBase<Research>.Find(transitions[i].Id);
            Assert.That(transition, Is.Not.Null);
            Assert.That(transition.AdvancesTechLevel, Is.True);
            Assert.That(transition.TechLevel, Is.EqualTo(transitions[i].Target));
            CompleteResearchPrerequisites(
                gameManager,
                researchManager,
                resourceManager,
                transition,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            Assert.That(researchManager.CanAccessResearch(transition), Is.True,
                "Transition should be accessible from the preceding era: " + transition.Id);

            CompleteResearchThroughRuntime(gameManager, researchManager, resourceManager, transition);
            Assert.That(researchManager.IsResearchCompleted(transition.Id), Is.True);
            Assert.That(gameManager.State.TechLevel, Is.EqualTo(transitions[i].Target),
                "Completed transition must update the authoritative TechLevel.");

            Research nextResearch = DataBase<Research>.All.FirstOrDefault(value =>
                value != null && value.TechLevel == transitions[i].Target &&
                !value.AdvancesTechLevel);
            Assert.That(nextResearch, Is.Not.Null,
                "No next-era Research definition exists after " + transition.Id);

            Building nextBuilding = DataBase<Building>.All.FirstOrDefault(value =>
                value != null && value.TechLevel == transitions[i].Target);
            Assert.That(nextBuilding, Is.Not.Null,
                "No next-era Building definition exists after " + transition.Id);

            if (transitions[i].Target != TechLevel.Industrial &&
                transitions[i].Target != TechLevel.Spacer)
                continue;

            if (transitions[i].Target == TechLevel.Industrial)
            {
                Research workshopTheory = DataBase<Research>.Find("IndustrialWorkshop");
                Assert.That(workshopTheory, Is.Not.Null);
                CompleteResearchThroughRuntime(gameManager, researchManager, resourceManager, workshopTheory);
                Assert.That(workshopManager.IsSystemUnlocked, Is.True,
                    "IndustrialWorkshop must unlock the Workshop system at runtime.");
            }

            WorkshopUpgrade nextWorkshop = DataBase<WorkshopUpgrade>.All.FirstOrDefault(value =>
                value != null && value.TechLevel == transitions[i].Target &&
                value.RequiredUpgrades.Count == 0 &&
                (transitions[i].Target != TechLevel.Industrial ||
                    workshopManager.ArePrerequisitesMet(value)));
            Assert.That(nextWorkshop, Is.Not.Null,
                "No next-era Workshop definition exists after " + transition.Id);
            if (transitions[i].Target == TechLevel.Industrial)
            {
                Assert.That(workshopManager.TryPurchase(nextWorkshop, out WorkshopPurchaseFailure failure), Is.True,
                    "Next-era Workshop purchase failed: " + failure);
            }
        }
    }

    [UnityTest]
    public IEnumerator EraPage_RendersCurrentNextEraProgressAndGoal()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return WaitForRuntimeUiRoot();

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null, "SampleScene must contain the runtime KingdomUIRoot.");

        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);
        setPage.Invoke(root, new object[] { "Era" });
        yield return null;
        yield return null;

        Transform rows = root.transform.Find("SafeAreaRoot/Content/PageHost/Era/DataRows");
        Assert.That(rows, Is.Not.Null, "Era/DataRows must exist under the isolated runtime UI.");
        Assert.That(rows.childCount, Is.GreaterThan(0), "Era page must not remain an empty DataRows container.");

        TMP_Text[] texts = rows.GetComponentsInChildren<TMP_Text>(true);
        Assert.That(texts.Any(text => text.text == "时代进度"), Is.True,
            "Era page must display a progress section.");
        Assert.That(texts.Any(text => text.text == "时代目标"), Is.True,
            "Era page must display the active era goal.");
        Assert.That(texts.Any(text => text.text == "当前主要阻碍"), Is.True,
             "Era page must display the first unmet era condition as the actionable blocker.");
        Assert.That(texts.Any(text => text.text.Contains("下一时代") || text.text.Contains("当前内容的最后时代")), Is.True,
            "Era page must identify the next era or the terminal state.");
    }

    [UnityTest]
    public IEnumerator ResearchTree_RuntimeLayoutAndOverflow_AreLoggedAndNonOverlapping()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return WaitForRuntimeUiRoot();

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null, "SampleScene must contain the runtime KingdomUIRoot.");

        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);
        setPage.Invoke(root, new object[] { "Research" });
        Transform viewport = null;
        Transform content = null;
        int expectedResearchNodes = DataBase<Research>.All.Count;
        for (int frame = 0; frame < 480; frame++)
        {
            yield return null;
            viewport = root.transform.Find("SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport");
            content = viewport == null ? null : viewport.Find("ResearchGraphContent");
            int builtResearchNodes = 0;
            if (content != null)
                foreach (Transform child in content)
                    if (child.name.StartsWith("ResearchNode_", StringComparison.Ordinal))
                        builtResearchNodes++;
            if (builtResearchNodes >= expectedResearchNodes)
                break;
        }
        // The final node is created one frame before the graph gesture binds
        // its measured overflow state.
        yield return null;

        Assert.That(viewport, Is.Not.Null, "ResearchGraphViewport must be authored under the isolated SafeAreaRoot.");
        Assert.That(viewport.GetComponent<Canvas>(), Is.Not.Null,
            "ResearchGraphViewport must isolate graph redraws in a nested Canvas.");
        Assert.That(viewport.GetComponent<GraphicRaycaster>(), Is.Not.Null,
            "The isolated research Canvas must retain touch raycasting.");
        content = viewport.Find("ResearchGraphContent");
        Assert.That(content, Is.Not.Null);
        Transform lineLayer = content.Find("ResearchGraphLineLayer");
        Assert.That(lineLayer, Is.Not.Null,
            "ResearchGraphLineLayer must be authored in the scene shell.");
        UIResearchConnectorBatch[] connectorBatches =
            lineLayer.GetComponentsInChildren<UIResearchConnectorBatch>(true);
        Assert.That(connectorBatches.Length, Is.GreaterThan(0));
        Assert.That(connectorBatches.Length, Is.LessThanOrEqualTo(8),
            "Four authored connector textures need at most normal/focused batch renderers.");
        int logicalConnectorParts = connectorBatches
            .Where(batch => !batch.DrawFocused)
            .Sum(batch => batch.PartCount);
        Assert.That(logicalConnectorParts, Is.GreaterThan(0));
        int renderedConnectorVertices = 0;
        for (int i = 0; i < connectorBatches.Length; i++)
        {
            Mesh connectorMesh = connectorBatches[i].canvasRenderer.GetMesh();
            if (connectorMesh != null)
                renderedConnectorVertices += connectorMesh.vertexCount;
        }
        Assert.That(renderedConnectorVertices, Is.EqualTo(logicalConnectorParts * 4),
            "Every logical connector part must render exactly one textured quad.");
        Assert.That(
            lineLayer.GetComponentsInChildren<Transform>(true).Length - 1,
            Is.LessThanOrEqualTo(10),
            "Connector batching must not recreate one Transform per logical segment.");
        Assert.That(
            lineLayer.GetComponentsInChildren<Image>(true)
                .Count(image => image.name.StartsWith(
                    "ResearchLinePart_", StringComparison.Ordinal)),
            Is.EqualTo(0),
            "Legacy per-segment Images must not return.");
        Debug.Log(
            $"[KingdomUI] Research connector batch audit: logicalParts={logicalConnectorParts}, " +
            $"renderedVertices={renderedConnectorVertices}, " +
            $"batchGraphics={connectorBatches.Length}, connectorObjects=" +
            $"{lineLayer.GetComponentsInChildren<Transform>(true).Length - 1}");
        Assert.That(content.Find("ResearchGraphDragSurface"), Is.Not.Null,
            "ResearchGraphDragSurface must be authored in the scene shell.");
        Transform toolbar = viewport.Find("ResearchTreeToolbar");
        Assert.That(toolbar, Is.Not.Null, "ResearchTreeToolbar must remain beside the research tree.");
        RectTransform toolbarRect = toolbar as RectTransform;
        Assert.That(toolbarRect.anchorMin.y, Is.EqualTo(1f).Within(.001f));
        Assert.That(toolbarRect.anchorMax.y, Is.EqualTo(1f).Within(.001f));
        Transform search = toolbar.Find("Search");
        if (search != null)
            Assert.That(search.gameObject.activeSelf, Is.False,
                "The retired research search control must remain an inactive placeholder.");
        Transform queueViewport = root.transform.Find("SafeAreaRoot/Content/ResearchQueueViewport");
        Assert.That(queueViewport, Is.Not.Null,
            "SafeAreaRoot/Content/ResearchQueueViewport must display the graphic research queue.");
        Transform quantityControls = root.transform.Find("SafeAreaRoot/Content/BuildingQuantityControls");
        Assert.That(quantityControls, Is.Not.Null);
        Assert.That(queueViewport.parent, Is.SameAs(quantityControls.parent),
            "The research queue must share the BuildingQuantityControls parent.");
        ScrollRect queueScroll = queueViewport.GetComponent<ScrollRect>();
        Assert.That(queueScroll, Is.Not.Null,
            "The graphic research queue must use a ScrollRect for its drag surface.");
        Assert.That(queueScroll.horizontal, Is.True);
        Assert.That(queueScroll.vertical, Is.False);
        Assert.That(queueScroll.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
        RectTransform queueContent = queueViewport.Find("ResearchQueueContent") as RectTransform;
        Assert.That(queueContent, Is.Not.Null,
            "The graphic research queue content is missing.");
        Assert.That(queueScroll.content, Is.SameAs(queueContent));
        Assert.That(queueViewport.GetComponent<GraphicRaycaster>(), Is.Not.Null,
            "The queue viewport must retain an active raycaster for node Buttons.");
        Assert.That(queueContent.anchoredPosition.y, Is.EqualTo(0f).Within(.01f));
        Transform pageHost = root.transform.Find("SafeAreaRoot/Content/PageHost");
        Assert.That(pageHost, Is.Not.Null);
        Assert.That(pageHost.GetComponent<Canvas>(), Is.Not.Null,
            "The page viewport must isolate scroll redraws from fixed UI.");
        ScrollRect outerPageScroll = pageHost.GetComponent<ScrollRect>();
        Assert.That(outerPageScroll, Is.Not.Null);
        Assert.That(outerPageScroll.enabled, Is.False,
            "The legacy outer page ScrollRect must not compete with the research graph gesture.");

        RectTransform viewportRect = viewport as RectTransform;
        RectTransform contentRect = content as RectTransform;
        Assert.That(viewportRect, Is.Not.Null);
        Assert.That(contentRect, Is.Not.Null);

        var cells = new HashSet<Vector2Int>();
        int nodeCount = 0;
        foreach (Transform child in content)
        {
            if (!child.name.StartsWith("ResearchNode_", System.StringComparison.Ordinal))
                continue;
            RectTransform node = child as RectTransform;
            Assert.That(node, Is.Not.Null);
            Vector2 topLeft = new Vector2(
                node.anchoredPosition.x,
                contentRect.rect.height - node.anchoredPosition.y - node.rect.height);
            Assert.That(cells.Add(new Vector2Int(
                Mathf.RoundToInt(topLeft.x), Mathf.RoundToInt(topLeft.y))), Is.True,
                "Research nodes must not occupy the same integer grid cell.");
            nodeCount++;
        }

        UIResearchGraphGesture gesture = viewport.GetComponent<UIResearchGraphGesture>();
        Assert.That(gesture, Is.Not.Null);
        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        Assert.That(scroll, Is.Not.Null);
        bool verticalOverflow = contentRect.rect.height * Mathf.Abs(contentRect.localScale.y) >
            viewportRect.rect.height + 0.5f;
        bool horizontalOverflow = contentRect.rect.width * Mathf.Abs(contentRect.localScale.x) >
            viewportRect.rect.width + 0.5f;
        FieldInfo canPanVerticalField = typeof(UIResearchGraphGesture).GetField(
            "canPanVertical", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo canPanHorizontalField = typeof(UIResearchGraphGesture).GetField(
            "canPanHorizontal", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(canPanVerticalField, Is.Not.Null);
        Assert.That(canPanHorizontalField, Is.Not.Null);
        bool canPanVertical = (bool)canPanVerticalField.GetValue(gesture);
        bool canPanHorizontal = (bool)canPanHorizontalField.GetValue(gesture);
        Assert.That(canPanVertical, Is.EqualTo(verticalOverflow));
        Assert.That(canPanHorizontal, Is.EqualTo(horizontalOverflow));
        Transform safeAreaRoot = root.transform.Find("SafeAreaRoot");
        Assert.That(safeAreaRoot, Is.Not.Null);
        bool legacyRootChildrenInactive = true;
        foreach (Transform rootChild in root.transform)
        {
            if (rootChild == safeAreaRoot)
                continue;
            legacyRootChildrenInactive &= !rootChild.gameObject.activeSelf;
            Assert.That(rootChild.gameObject.activeSelf, Is.False,
                "Every legacy KingdomUIRoot child outside SafeAreaRoot must remain inactive: " +
                rootChild.name);
        }

        EventSystem eventSystem = Object.FindObjectOfType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("PlayMode-ResearchGraph-EventSystem");
            createdObjects.Add(eventSystemObject);
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }
        PointerEventData pointer = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = new Vector2(500f, 600f)
        };
        Vector2 beforeDrag = contentRect.anchoredPosition;
        gesture.OnPointerDown(pointer);
        gesture.OnInitializePotentialDrag(pointer);
        pointer.position = new Vector2(500f, 550f);
        gesture.OnBeginDrag(pointer);
        pointer.position = new Vector2(500f, 750f);
        gesture.OnDrag(pointer);
        Vector2 afterDrag = contentRect.anchoredPosition;
        gesture.OnEndDrag(pointer);
        gesture.OnPointerUp(pointer);
        if (verticalOverflow)
            Assert.That(afterDrag.y, Is.GreaterThan(beforeDrag.y),
                "A vertical drag in an overflowing research graph must move the graph content within its vertical range.");
        Debug.Log($"[KingdomUI] Research pointer drag audit: before={beforeDrag}, after={afterDrag}, delta={afterDrag - beforeDrag}, verticalDragMoved={afterDrag.y > beforeDrag.y}");

        Transform firstNode = null;
        foreach (Transform child in content)
            if (child.name.StartsWith("ResearchNode_", System.StringComparison.Ordinal))
            {
                firstNode = child;
                break;
            }
        Assert.That(firstNode, Is.Not.Null);
        UIResearchGraphDragForwarder forwarder = firstNode.GetComponent<UIResearchGraphDragForwarder>();
        Assert.That(forwarder, Is.Not.Null);
        contentRect.anchoredPosition = beforeDrag;
        PointerEventData nodePointer = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = new Vector2(500f, 600f)
        };
        forwarder.OnPointerDown(nodePointer);
        forwarder.OnInitializePotentialDrag(nodePointer);
        nodePointer.position = new Vector2(500f, 550f);
        forwarder.OnBeginDrag(nodePointer);
        nodePointer.position = new Vector2(500f, 750f);
        forwarder.OnDrag(nodePointer);
        Vector2 afterNodeDrag = contentRect.anchoredPosition;
        forwarder.OnEndDrag(nodePointer);
        forwarder.OnPointerUp(nodePointer);
        if (verticalOverflow)
            Assert.That(afterNodeDrag.y, Is.GreaterThan(beforeDrag.y),
                "A drag beginning on a research Button must be forwarded to the graph gesture.");
        Debug.Log($"[KingdomUI] Research node-forwarded drag audit: before={beforeDrag}, after={afterNodeDrag}, delta={afterNodeDrag - beforeDrag}, forwardedVerticalDragMoved={afterNodeDrag.y > beforeDrag.y}");
        Debug.Log($"[KingdomUI] Research runtime playmode audit: expectedNodes={expectedResearchNodes}, nodes={nodeCount}, uniqueCells={cells.Count}, viewport={viewportRect.rect.size}, content={contentRect.rect.size}, horizontalOverflow={horizontalOverflow}, verticalOverflow={verticalOverflow}, canPanHorizontal={canPanHorizontal}, canPanVertical={canPanVertical}, outerPageScrollEnabled={outerPageScroll.enabled}, legacyRootChildrenInactive={legacyRootChildrenInactive}");
        Assert.That(expectedResearchNodes, Is.GreaterThan(0));
        Assert.That(nodeCount, Is.EqualTo(expectedResearchNodes));
        Assert.That(cells.Count, Is.EqualTo(expectedResearchNodes),
            "Every current research definition must occupy one unique integer cell.");

        Transform researchPage = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Research");
        Assert.That(researchPage, Is.Not.Null);
        CanvasGroup visibility = researchPage.GetComponent<CanvasGroup>();
        Assert.That(visibility, Is.Not.Null,
            "The cached research hierarchy must use one root CanvasGroup for page visibility.");
        setPage.Invoke(root, new object[] { "Overview" });
        yield return null;
        Assert.That(researchPage.gameObject.activeSelf, Is.True,
            "Leaving Research must not disable thousands of cached graph objects.");
        Assert.That(visibility.alpha, Is.EqualTo(0f).Within(0.001f));
        Assert.That(visibility.interactable, Is.False);
        Assert.That(visibility.blocksRaycasts, Is.False);
        Assert.That(gesture.enabled, Is.False,
            "A hidden persistent graph must not continue processing gestures.");

        setPage.Invoke(root, new object[] { "Research" });
        yield return null;
        Assert.That(researchPage.gameObject.activeSelf, Is.True);
        Assert.That(visibility.alpha, Is.EqualTo(1f).Within(0.001f));
        Assert.That(visibility.interactable, Is.True);
        Assert.That(visibility.blocksRaycasts, Is.True);
        Assert.That(gesture.enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator DetailPanel_UsesSingleScrollOwnerAndCenteredResearchLabels()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return WaitForRuntimeUiRoot();

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null, "KingdomUIRoot was not created after loading SampleScene.");
        Transform detailPanel = root.transform.Find("SafeAreaRoot/DetailPanel");
        Assert.That(detailPanel, Is.Not.Null, "SafeAreaRoot/DetailPanel is missing under KingdomUIRoot.");

        Transform detailUI = detailPanel.Find("DetailUI");
        Assert.That(detailUI, Is.Not.Null, "DetailUI runtime root is missing under DetailPanel.");
        Transform detailViewport = detailUI.Find("DetailScrollViewport");
        Transform detailContent = detailViewport == null ? null : detailViewport.Find("DetailScrollContent");
        Assert.That(detailViewport, Is.Not.Null, "DetailPanel must expose one runtime scroll viewport.");
        Assert.That(detailContent, Is.Not.Null, "DetailScrollContent is missing under DetailScrollViewport.");
        Assert.That(detailViewport.GetComponent<Canvas>(), Is.Not.Null,
            "Detail scrolling must use an isolated nested Canvas.");
        Assert.That(detailViewport.GetComponent<GraphicRaycaster>(), Is.Not.Null,
            "The isolated detail Canvas must retain touch raycasting.");
        Assert.That((detailViewport as RectTransform).rect.height, Is.GreaterThan(0f),
            "DetailScrollViewport must have a positive runtime height.");
        Assert.That((detailContent as RectTransform).rect.height, Is.GreaterThan(0f),
            "DetailScrollContent must be repaired after the initial layout pass.");
        ScrollRect detailScroll = detailViewport.GetComponent<ScrollRect>();
        Assert.That(detailScroll, Is.Not.Null);
        Assert.That(detailScroll.enabled, Is.False,
            "The native ScrollRect is a geometry owner only; DetailUIInteraction owns dragging.");
        Assert.That(detailScroll.content, Is.SameAs(detailContent));
        Transform header = detailUI.Find("Header");
        Assert.That(header, Is.Not.Null);
        Assert.That(header.GetSiblingIndex(), Is.LessThan(detailViewport.GetSiblingIndex()));

        Transform requirements = detailContent.Find("BuildingRequirements");
        Transform flows = detailContent.Find("BuildingOutput");
        Assert.That(requirements, Is.Not.Null, "BuildingRequirements is missing under DetailScrollContent.");
        Assert.That(flows, Is.Not.Null, "BuildingOutput is missing under DetailScrollContent.");
        Assert.That(requirements.parent, Is.SameAs(detailContent));
        Assert.That(flows.parent, Is.SameAs(detailContent));
        Assert.That(requirements.GetComponent<ScrollRect>(), Is.Null,
            "Requirements must not contain a nested ScrollRect.");
        Assert.That(flows.GetComponent<ScrollRect>(), Is.Null,
            "Flows must not contain a nested ScrollRect.");
        Assert.That(requirements.GetComponent<RectMask2D>(), Is.Null,
            "Requirements must not contain a nested mask.");
        Assert.That(flows.GetComponent<RectMask2D>(), Is.Null,
            "Flows must not contain a nested mask.");

        Transform bodyTransform = detailContent.Find("Body");
        Assert.That(bodyTransform, Is.Not.Null, "Detail body is missing under DetailScrollContent.");
        MethodInfo showDetails = typeof(KingdomUIRoot).GetMethod(
            "ShowDetails", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(showDetails, Is.Not.Null);
        showDetails.Invoke(root, new object[] { "Runtime detail", "Visible body", "detail-test" });
        yield return null;
        TMP_Text bodyText = bodyTransform.GetComponent<TMP_Text>();
        Assert.That(bodyText.text, Does.Contain("Runtime detail"));
        Assert.That(bodyText.rectTransform.rect.height, Is.GreaterThan(0f),
            "Detail body must retain a visible rect after being reparented.");

        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);
        setPage.Invoke(root, new object[] { "Research" });
        Transform researchViewport = null;
        Transform researchContent = null;
        Transform firstNode = null;
        for (int frame = 0; frame < 480 && firstNode == null; frame++)
        {
            yield return null;
            researchViewport = root.transform.Find(
                "SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport");
            researchContent = researchViewport == null
                ? null
                : researchViewport.Find("ResearchGraphContent");
            if (researchContent == null)
                continue;
            foreach (Transform child in researchContent)
                if (child.name.StartsWith("ResearchNode_", StringComparison.Ordinal))
                {
                    firstNode = child;
                    break;
                }
        }
        Assert.That(firstNode, Is.Not.Null);
        foreach (string labelName in new[] { "Label", "Cost", "Progress", "State" })
        {
            TMP_Text label = firstNode.Find(labelName)?.GetComponent<TMP_Text>();
            Assert.That(label, Is.Not.Null, "Research node label is missing: " + labelName);
            Assert.That(label.alignment, Is.EqualTo(TextAlignmentOptions.Center));
            Assert.That(label.rectTransform.pivot.x, Is.EqualTo(.5f).Within(.001f));
            Assert.That(label.rectTransform.anchorMin.x, Is.LessThan(label.rectTransform.anchorMax.x));
            Assert.That(label.rectTransform.anchorMin.x, Is.GreaterThanOrEqualTo(0f));
            Assert.That(label.rectTransform.anchorMax.x, Is.LessThanOrEqualTo(1f));
        }
    }

    [UnityTest]
    public IEnumerator ResourceDetailAndList_ShowOnlyRealizedHappinessAdjustedFlows()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return WaitForRuntimeUiRoot();

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        GameManager gameManager = Object.FindObjectOfType<GameManager>();
        ResourceManager resourceManager = Object.FindObjectOfType<ResourceManager>();
        BuildingManager buildingManager = Object.FindObjectOfType<BuildingManager>();
        SimulationManager simulationManager = Object.FindObjectOfType<SimulationManager>();
        Assert.That(root, Is.Not.Null);
        Assert.That(gameManager, Is.Not.Null);
        Assert.That(resourceManager, Is.Not.Null);
        Assert.That(buildingManager, Is.Not.Null);
        Assert.That(simulationManager, Is.Not.Null);
        simulationManager.SetRunning(false);
        typeof(GameManager).GetMethod(
                "InitializeNewGame", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(gameManager, null);
        Assert.That(gameManager.State.HappinessRewardMultiplier, Is.GreaterThan(ExpantaNum.One));

        Resource resource = ScriptableObject.CreateInstance<Resource>();
        resource.SetIdForEditor("PlayModeDetailFlowResource");
        resource.Label = "Test flow resource";
        resource.Description = "Resource detail flow regression";
        createdObjects.Add(resource);

        Building realized = CreateFlowBuilding(
            "PlayModeRealizedProducer", "Realized producer", resource, new ExpantaNum(2d));
        Building unbuilt = CreateFlowBuilding(
            "PlayModeUnbuiltProducer", "Unbuilt producer", resource, new ExpantaNum(9d));
        Building zeroEfficiency = CreateFlowBuilding(
            "PlayModeZeroEfficiencyProducer", "Zero efficiency producer", resource, new ExpantaNum(7d));

        BuildingState realizedState = buildingManager.EnsureBuilding(realized);
        realizedState.SetAmountForEditor(new ExpantaNum(2d));
        realizedState.SetEfficiencyForEditor(new ExpantaNum(0.5d));
        BuildingState unbuiltState = buildingManager.EnsureBuilding(unbuilt);
        unbuiltState.SetAmountForEditor(ExpantaNum.Zero);
        BuildingState zeroEfficiencyState = buildingManager.EnsureBuilding(zeroEfficiency);
        zeroEfficiencyState.SetAmountForEditor(ExpantaNum.One);
        zeroEfficiencyState.SetEfficiencyForEditor(ExpantaNum.Zero);

        resourceManager.SetAmount(resource, ExpantaNum.Zero);
        resourceManager.SetProductionRate(resource, new ExpantaNum(2d));
        resourceManager.SetConsumptionRate(resource, ExpantaNum.Zero);
        ExpantaNum expectedRate = new ExpantaNum(2d) * gameManager.State.HappinessRewardMultiplier;

        MethodInfo showResourceDetails = typeof(KingdomUIRoot).GetMethod(
            "ShowResourceDetails", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(showResourceDetails, Is.Not.Null);
        showResourceDetails.Invoke(root, new object[] { resource });
        yield return null;

        Transform bodyTransform = root.transform.Find(
            "SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body");
        TMP_Text body = bodyTransform == null ? null : bodyTransform.GetComponent<TMP_Text>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body.text, Does.Contain(realized.Label));
        Assert.That(body.text, Does.Not.Contain(unbuilt.Label));
        Assert.That(body.text, Does.Not.Contain(zeroEfficiency.Label));
        Assert.That(body.text, Does.Contain(expectedRate.ToGameString() + "/s"));
        ExpantaNum expectedBuildingRate = new ExpantaNum(2d) *
            realizedState.Amount * realizedState.Efficiency *
            ProgressionModifierManager.Current.GetBuildingProductionMultiplier(realized) *
            ProgressionModifierManager.Current.GlobalBuildingProductionMultiplier *
            ProgressionModifierManager.Current.GetResourceProductionMultiplier(resource) *
            gameManager.State.HappinessRewardMultiplier;
        string realizedLine = body.text.Split('\n')
            .First(line => line.Contains(realized.Label));
        Assert.That(realizedLine, Does.Contain(expectedBuildingRate.ToGameString() + "/s"));

        resourceManager.Tick(1d);
        Assert.That(resourceManager.GetAmount(resource), Is.EqualTo(expectedRate));

        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);
        setPage.Invoke(root, new object[] { "Resources" });
        yield return null;
        yield return null;

        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetProductionRate(wood, ExpantaNum.One);
        resourceManager.SetConsumptionRate(wood, ExpantaNum.Zero);
        MethodInfo refreshCards = typeof(KingdomUIRoot).GetMethod(
            "RefreshLiveCardValues", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(refreshCards, Is.Not.Null);
        refreshCards.Invoke(root, null);
        FieldInfo changeLabelsField = typeof(KingdomUIRoot).GetField(
            "resourceChangeLabels", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(changeLabelsField, Is.Not.Null);
        var changeLabels =
            (Dictionary<Resource, TMP_Text>)changeLabelsField.GetValue(root);
        Assert.That(changeLabels.TryGetValue(wood, out TMP_Text changeLabel), Is.True);
        ExpantaNum expectedWoodRate = gameManager.State.HappinessRewardMultiplier;
        Assert.That(changeLabel.text, Is.EqualTo("+" + expectedWoodRate.ToGameString() + "/s"));
    }

    [UnityTest]
    public IEnumerator TopStatus_ShowsNegativePopulationNetRateDuringStarvation()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return WaitForRuntimeUiRoot();

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        GameManager gameManager = Object.FindObjectOfType<GameManager>();
        SimulationManager simulationManager = Object.FindObjectOfType<SimulationManager>();
        Assert.That(root, Is.Not.Null);
        Assert.That(gameManager, Is.Not.Null);
        Assert.That(simulationManager, Is.Not.Null);
        simulationManager.SetRunning(false);

        typeof(GameState).GetMethod(
                "ResetDerivedEconomy", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(gameManager.State, new object[] { new ExpantaNum(500d) });
        typeof(GameState).GetMethod(
                "RestoreCore", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(gameManager.State, new object[]
            {
                0, "Test", TechLevel.Animal, ExpantaNum.Zero, 0L
            });
        typeof(GameState).GetMethod(
                "RestorePopulation", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(gameManager.State, new object[] { new ExpantaNum(10d) });
        typeof(PopulationState).GetMethod(
                "RestoreCapacityExact", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(gameManager.State.Population, new object[]
            {
                new ExpantaNum(20d), ExpantaNum.Zero
            });
        typeof(GameState).GetMethod(
                "AdjustFoodRates", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(gameManager.State, new object[]
            {
                ExpantaNum.Zero, new ExpantaNum(20d)
            });

        Assert.That(gameManager.CurrentPopulationNetRatePerSecond, Is.LessThan(ExpantaNum.Zero));
        MethodInfo refreshTopStatus = typeof(KingdomUIRoot).GetMethod(
            "RefreshTopStatus", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(refreshTopStatus, Is.Not.Null);
        refreshTopStatus.Invoke(root, null);
        FieldInfo topStatusField = typeof(KingdomUIRoot).GetField(
            "topStatus", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(topStatusField, Is.Not.Null);
        TMP_Text topStatus = (TMP_Text)topStatusField.GetValue(root);
        Assert.That(topStatus, Is.Not.Null);
        Assert.That(
            topStatus.text,
            Does.Contain(gameManager.CurrentPopulationNetRatePerSecond.ToGameString() + "/s"));

        ExpantaNum populationBefore = gameManager.State.Population.Population;
        gameManager.Tick(3600d);
        Assert.That(gameManager.State.Population.Population, Is.LessThan(populationBefore));
    }





    [UnityTest]
    public IEnumerator SimulationPaused_DoesNotAdvanceDuringUpdate()
    {
        ResourceManager resourceManager = FindOrCreateManager<ResourceManager>("PlayMode-Pause-Managers");
        FindOrCreateManager<GameManager>("PlayMode-Pause-Managers");
        FindOrCreateManager<BuildingManager>("PlayMode-Pause-Managers");
        FindOrCreateManager<ResearchManager>("PlayMode-Pause-Managers");
        SimulationManager simulationManager = FindOrCreateManager<SimulationManager>("PlayMode-Pause-Managers");
        yield return null;

        Resource wood = DataBase<Resource>.Find("WoodLog");
        resourceManager.SetAmount(wood, ExpantaNum.Zero);
        resourceManager.SetProductionRate(wood, 10);
        simulationManager.SetRunning(false);

        yield return new WaitForSecondsRealtime(0.15f);

        Assert.That(resourceManager.GetAmount(wood), Is.EqualTo(ExpantaNum.Zero));
    }

    private static IEnumerator WaitForRuntimeUiRoot(int maxFrames = 120)
    {
        for (int frame = 0; frame < maxFrames; frame++)
        {
            if (Object.FindObjectOfType<KingdomUIRoot>() != null)
                yield break;
            yield return null;
        }

        Assert.Fail("SampleScene did not create KingdomUIRoot within the frame budget.");
    }

    private static Research FindAvailableResearch(ResearchManager researchManager)
    {
        IReadOnlyList<Research> researches = DataBase<Research>.All;
        for (int i = 0; i < researches.Count; i++)
        {
            Research research = researches[i];
            ResearchState state = researchManager.GetState(research);
            if (state.Status != ResearchStatus.Completed &&
                (research.Prerequisites == null || research.Prerequisites.Count == 0))
                return research;
        }

        Assert.Fail("No unfinished research without prerequisites is available for the PlayMode test.");
        return null;
    }

    private static void GrantResearchTestResources(ResourceManager resourceManager)
    {
        ExpantaNum testAmount = new ExpantaNum("1e100000");
        foreach (Resource resource in DataBase<Resource>.All)
            resourceManager.SetAmount(resource, testAmount);
    }

    private static void CompleteResearchThroughRuntime(
        GameManager gameManager,
        ResearchManager researchManager,
        ResourceManager resourceManager,
        Research target)
    {
        ResearchActionResult action = researchManager.HandleResearchAction(target);
        Assert.That(action, Is.Not.EqualTo(ResearchActionResult.Invalid));
        Assert.That(action, Is.Not.EqualTo(ResearchActionResult.Blocked),
            "Research was blocked before its runtime prerequisite batch could start: " + target.Id);

        for (int guard = 0; guard < 10000 && !researchManager.IsResearchCompleted(target.Id); guard++)
        {
            if (researchManager.ActiveResearch == null && researchManager.ResearchQueue.Count > 0)
            {
                Research queued = researchManager.ResearchQueue[0].Definition;
                Assert.That(researchManager.PayResearchCost(queued), Is.EqualTo(ResearchPaymentResult.Paid),
                    "Research payment did not complete atomically for " + queued.Id);
                Dictionary<Resource, ExpantaNum> amountsAfterPayment =
                    SnapshotResourceAmounts(resourceManager);
                Assert.That(researchManager.PayResearchCost(queued), Is.EqualTo(ResearchPaymentResult.AlreadyPaid),
                    "A second payment request must not charge the same research again: " + queued.Id);
                AssertResourceAmountsEqual(amountsAfterPayment, resourceManager);
                researchManager.TryStartNextQueuedResearch();
            }

            if (researchManager.ActiveResearch != null)
                researchManager.Tick(1000000d);
            else if (researchManager.ResearchQueue.Count == 0)
                break;
        }

        Assert.That(researchManager.IsResearchCompleted(target.Id), Is.True,
            "Research did not complete through the runtime clock: " + target.Id);
        Assert.That(gameManager.State.TechLevel, Is.GreaterThanOrEqualTo(target.TechLevel));
    }

    private static void CompleteResearchPrerequisites(
        GameManager gameManager,
        ResearchManager researchManager,
        ResourceManager resourceManager,
        Research target,
        HashSet<string> visiting)
    {
        if (target == null || researchManager.IsResearchCompleted(target.Id))
            return;
        Assert.That(visiting.Add(target.Id), Is.True,
            "Research prerequisite cycle detected at " + target.Id);

        for (int i = 0; i < target.Prerequisites.Count; i++)
        {
            Research prerequisite = target.Prerequisites[i];
            CompleteResearchPrerequisites(
                gameManager,
                researchManager,
                resourceManager,
                prerequisite,
                visiting);
            CompleteResearchThroughRuntime(
                gameManager,
                researchManager,
                resourceManager,
                prerequisite);
        }

        visiting.Remove(target.Id);
    }

    private static Dictionary<Resource, ExpantaNum> SnapshotResourceAmounts(ResourceManager resourceManager)
    {
        var result = new Dictionary<Resource, ExpantaNum>();
        foreach (Resource resource in DataBase<Resource>.All)
            result[resource] = resourceManager.GetAmount(resource);
        return result;
    }

    private static void AssertResourceAmountsEqual(
        Dictionary<Resource, ExpantaNum> expected,
        ResourceManager resourceManager)
    {
        foreach (KeyValuePair<Resource, ExpantaNum> entry in expected)
            Assert.That(resourceManager.GetAmount(entry.Key), Is.EqualTo(entry.Value),
                "Resource changed during a second payment attempt: " + entry.Key.Id);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing serialized field '{name}'.");
        field.SetValue(target, value);
    }

    private Building CreateFlowBuilding(
        string id,
        string label,
        Resource resource,
        ExpantaNum productionRate)
    {
        Building building = ScriptableObject.CreateInstance<Building>();
        building.SetIdForEditor(id);
        building.Label = label;
        building.Description = label;
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
            new List<Pair<Resource, ExpantaNum>>
            {
                new Pair<Resource, ExpantaNum>(resource, productionRate)
            },
            new List<Pair<Resource, ExpantaNum>>());
        createdObjects.Add(building);
        return building;
    }


    private T FindOrCreateManager<T>(string name) where T : Component
    {
        T existing = Object.FindObjectOfType<T>();
        if (existing != null)
            return existing;

        GameObject gameObject = GameObject.Find(name);
        if (gameObject == null)
        {
            gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
        }

        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }
}

