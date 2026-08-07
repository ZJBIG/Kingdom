using System.Collections;
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

public sealed class KingdomPlayModeTests
{
    private readonly List<Object> createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            if (createdObjects[i] != null)
                Object.Destroy(createdObjects[i]);
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
        Assert.That(gameManager.State.FoodSatisfaction, Is.EqualTo(ExpantaNum.One));
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
            new[] { typeof(ExpantaNum), typeof(bool), typeof(bool) }, null);
        for (int i = 0; i < refinery.RequiredResearch.Count; i++)
            restoreResearch.Invoke(
                researchManager.GetState(refinery.RequiredResearch[i]),
                new object[] { ExpantaNum.Zero, false, true });

        Assert.That(buildingManager.ArePrerequisitesMet(refinery, out failure), Is.False);
        Assert.That(failure, Is.EqualTo(BuildFailure.WorkshopPrerequisiteIncomplete));

        WorkshopUpgradeDefinition upgrade = refinery.RequiredWorkshopUpgrades[0];
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
    public IEnumerator ResearchTree_RuntimeLayoutAndOverflow_AreLoggedAndNonOverlapping()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return new WaitForSecondsRealtime(1.25f);

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null, "SampleScene must contain the runtime KingdomUIRoot.");

        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);
        setPage.Invoke(root, new object[] { "Research" });
        yield return null;
        yield return null;

        Transform viewport = root.transform.Find("SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport");
        Assert.That(viewport, Is.Not.Null, "ResearchGraphViewport must be authored under the isolated SafeAreaRoot.");
        Transform content = viewport.Find("ResearchGraphContent");
        Assert.That(content, Is.Not.Null);
        Assert.That(content.Find("ResearchGraphLineLayer"), Is.Not.Null,
            "ResearchGraphLineLayer must be authored in the scene shell.");
        Assert.That(content.Find("ResearchGraphDragSurface"), Is.Not.Null,
            "ResearchGraphDragSurface must be authored in the scene shell.");
        Transform toolbar = viewport.Find("ResearchTreeToolbar");
        Assert.That(toolbar, Is.Not.Null, "ResearchTreeToolbar must remain beside the research tree.");
        RectTransform toolbarRect = toolbar as RectTransform;
        Assert.That(toolbarRect.anchorMin.y, Is.EqualTo(1f).Within(.001f));
        Assert.That(toolbarRect.anchorMax.y, Is.EqualTo(1f).Within(.001f));
        Assert.That(toolbarRect.offsetMin.y, Is.EqualTo(-82f).Within(.1f));
        Assert.That(toolbarRect.offsetMax.y, Is.EqualTo(-12f).Within(.1f),
            "The toolbar must remain a top strip and must not become a full-screen input mask.");
        Transform search = toolbar.Find("Search");
        Assert.That(search, Is.Not.Null);
        Assert.That(search.gameObject.activeSelf, Is.False,
            "The research search control must be removed from the active UI.");
        TMP_Text queueLabel = toolbar.Find("Queue")?.GetComponent<TMP_Text>();
        Assert.That(queueLabel, Is.Not.Null,
            "ResearchTreeToolbar/Queue must display the current research queue.");
        Assert.That((queueLabel.transform as RectTransform).offsetMin.x,
            Is.EqualTo(168f).Within(.1f),
            "The queue must replace the search field in the reference red-box area.");
        Transform pageHost = root.transform.Find("SafeAreaRoot/Content/PageHost");
        Assert.That(pageHost, Is.Not.Null);
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
        bool rootSafeAreaOnly = root.transform.Find("SafeAreaRoot") != null;
        Assert.That(rootSafeAreaOnly, Is.True);

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
        gesture.OnInitializePotentialDrag(pointer);
        pointer.position = new Vector2(500f, 550f);
        gesture.OnBeginDrag(pointer);
        pointer.position = new Vector2(500f, 750f);
        gesture.OnDrag(pointer);
        Vector2 afterDrag = contentRect.anchoredPosition;
        gesture.OnEndDrag(pointer);
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
        forwarder.OnInitializePotentialDrag(nodePointer);
        nodePointer.position = new Vector2(500f, 550f);
        forwarder.OnBeginDrag(nodePointer);
        nodePointer.position = new Vector2(500f, 750f);
        forwarder.OnDrag(nodePointer);
        Vector2 afterNodeDrag = contentRect.anchoredPosition;
        forwarder.OnEndDrag(nodePointer);
        Assert.That(afterNodeDrag.y, Is.GreaterThan(beforeDrag.y),
            "A drag beginning on a research Button must be forwarded to the graph gesture.");
        Debug.Log($"[KingdomUI] Research node-forwarded drag audit: before={beforeDrag}, after={afterNodeDrag}, delta={afterNodeDrag - beforeDrag}, forwardedVerticalDragMoved={afterNodeDrag.y > beforeDrag.y}");
        Debug.Log($"[KingdomUI] Research runtime playmode audit: nodes={nodeCount}, uniqueCells={cells.Count}, viewport={viewportRect.rect.size}, content={contentRect.rect.size}, horizontalOverflow={horizontalOverflow}, verticalOverflow={verticalOverflow}, canPanHorizontal={canPanHorizontal}, canPanVertical={canPanVertical}, outerPageScrollEnabled={outerPageScroll.enabled}, rootSafeAreaOnly={rootSafeAreaOnly}");
        Assert.That(nodeCount, Is.EqualTo(DataBase<Research>.All.Count));
    }

    [UnityTest]
    public IEnumerator DetailPanel_UsesSingleScrollOwnerAndCenteredResearchLabels()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return new WaitForSecondsRealtime(1.25f);

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null, "KingdomUIRoot was not created after loading SampleScene.");
        Transform detailPanel = root.transform.Find("SafeAreaRoot/DetailPanel");
        Assert.That(detailPanel, Is.Not.Null, "SafeAreaRoot/DetailPanel is missing under KingdomUIRoot.");

        Transform detailViewport = detailPanel.Find("DetailScrollViewport");
        Transform detailContent = detailViewport == null ? null : detailViewport.Find("DetailScrollContent");
        Assert.That(detailViewport, Is.Not.Null, "DetailPanel must expose one runtime scroll viewport.");
        Assert.That(detailContent, Is.Not.Null, "DetailScrollContent is missing under DetailScrollViewport.");
        Assert.That((detailViewport as RectTransform).rect.height, Is.GreaterThan(0f),
            "DetailScrollViewport must have a positive runtime height.");
        Assert.That((detailContent as RectTransform).rect.height, Is.GreaterThan(0f),
            "DetailScrollContent must be repaired after the initial layout pass.");
        ScrollRect detailScroll = detailViewport.GetComponent<ScrollRect>();
        Assert.That(detailScroll, Is.Not.Null);
        Assert.That(detailScroll.enabled, Is.True);
        Assert.That(detailScroll.content, Is.SameAs(detailContent));
        Transform surface = detailPanel.Find("Surface");
        Transform accent = detailPanel.Find("Accent");
        if (surface != null)
            Assert.That(surface.GetSiblingIndex(), Is.LessThan(detailViewport.GetSiblingIndex()));
        if (accent != null)
            Assert.That(accent.GetSiblingIndex(), Is.LessThan(detailViewport.GetSiblingIndex()));

        Transform requirements = detailContent.Find("BuildingRequirements");
        Transform flows = detailContent.Find("BuildingOutput");
        Assert.That(requirements, Is.Not.Null, "BuildingRequirements is missing under DetailScrollContent.");
        Assert.That(flows, Is.Not.Null, "BuildingOutput is missing under DetailScrollContent.");
        Assert.That(requirements.parent, Is.SameAs(detailContent));
        Assert.That(flows.parent, Is.SameAs(detailContent));
        Assert.That(requirements.GetComponent<ScrollRect>().enabled, Is.False);
        Assert.That(flows.GetComponent<ScrollRect>().enabled, Is.False);
        Assert.That(requirements.GetComponent<RectMask2D>().enabled, Is.False);
        Assert.That(flows.GetComponent<RectMask2D>().enabled, Is.False);

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
        yield return null;
        yield return null;

        Transform researchViewport = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport");
        Transform researchContent = researchViewport == null
            ? null
            : researchViewport.Find("ResearchGraphContent");
        Transform firstNode = null;
        if (researchContent != null)
            foreach (Transform child in researchContent)
                if (child.name.StartsWith("ResearchNode_", System.StringComparison.Ordinal))
                {
                    firstNode = child;
                    break;
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

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing serialized field '{name}'.");
        field.SetValue(target, value);
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

