using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class KingdomStagedOnboardingPlayModeTests
{
    private string saveRoot;
    private KingdomUIRoot root;
    private TutorialManager initialTutorial;
    private MusicManager initialMusic;
    private int initialWidth;
    private int initialHeight;

    [SetUp]
    public void SetUp()
    {
        initialTutorial = Object.FindObjectOfType<TutorialManager>(true);
        initialMusic = Object.FindObjectOfType<MusicManager>(true);
        initialWidth = Screen.width;
        initialHeight = Screen.height;
        saveRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp",
            "StagedOnboarding-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Scene empty = SceneManager.CreateScene("StagedOnboardingTeardown-" + Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(empty);
        Scene sample = SceneManager.GetSceneByName("SampleScene");
        if (sample.IsValid() && sample.isLoaded) yield return SceneManager.UnloadSceneAsync(sample);
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        if (tutorial != null && tutorial != initialTutorial) Object.DestroyImmediate(tutorial.gameObject);
        if (music != null && music != initialMusic) Object.DestroyImmediate(music.gameObject);
        SaveManager.ClearSaveRootOverrideForTests();
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
        Screen.SetResolution(initialWidth, initialHeight, false);
        root = null;
    }

    [UnityTest]
    public IEnumerator HousingStage_RealRaycastsBlockUnrelatedNavigationAndKeepUnaffordableGoal()
    {
        Screen.SetResolution(2640, 1200, false);
        yield return LoadFreshGame();
        Assert.That(root.OnboardingStageForEditor.StageId, Is.EqualTo("building")); // Stable protocol.
        AssertSharedTarget();
        OnboardingInputGate gate = root.GetComponent<OnboardingInputGate>();
        Assert.That(gate.IsActive, Is.True);
        RectTransform unrelated = root.transform.Find("SafeAreaRoot/LeftNavigation/NavigationButtons/Nav_Resources") as RectTransform;
        ClickAt(unrelated);
        yield return null;
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Navigate));
        yield return CaptureReferenceFrame("first-home-overview.png");
        ClickTarget();
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Select));
        ClickTarget();
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Build));
        Assert.That(root.OnboardingStageForEditor.CanInteract, Is.False);
        Assert.That(gate.IsActive, Is.False);
        Assert.That(EventSystem.current.sendNavigationEvents, Is.True);
        AssertSharedTarget();
        yield return CaptureReferenceFrame("home-material-wait.png", false);
    }

    [UnityTest]
    public IEnumerator SkipEndsOnlyCurrentStage_AndDisableReleasesNavigation()
    {
        yield return LoadFreshGame();
        TutorialManager tutorial = TutorialManager.Current;
        ClickAt(OverlayButton("Skip"));
        yield return Settle();
        Assert.That(tutorial.SkippedStageIds, Does.Contain("building"));
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain("building"));
        Assert.That(root.OnboardingStageForEditor, Is.Null);
        Assert.That(tutorial.Evaluate().NavigationTargetId, Is.EqualTo("WoodHouse"));
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.False);
        root.gameObject.SetActive(false);
        yield return null;
        Assert.That(OnboardingInputGate.BlocksNavigation, Is.False);
        Assert.That(EventSystem.current.sendNavigationEvents, Is.True);
        root.gameObject.SetActive(true);
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor, Is.Null);
        PrepareResearchUnlocked();
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.StageId, Is.EqualTo("research"));
    }

    [UnityTest]
    public IEnumerator LostTargetReleasesInput_AndRestoreRebindsStageToLiveControls()
    {
        yield return LoadFreshGame();
        TutorialManager tutorial = TutorialManager.Current;
        SaveManager.TutorialSaveData saved = tutorial.CaptureSaveData();
        RectTransform target = root.OnboardingTargetForEditor;
        target.gameObject.SetActive(false);
        root.RefreshOnboardingForEditor();
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.False);
        Assert.That(EventSystem.current.sendNavigationEvents, Is.True);
        target.gameObject.SetActive(true);
        tutorial.SkipCurrentStage();
        root.RefreshOnboardingForEditor();
        tutorial.RestoreSaveData(saved, GameManager.Instance.State.TechLevel);
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.StageId, Is.EqualTo(saved.ActiveStageId));
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Navigate));
        Assert.That(root.OnboardingTargetForEditor.gameObject.activeInHierarchy, Is.True);
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.True, OnboardingDiagnostic());
        ClickTarget();
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Select),
            "Restoring a session must bind the current navigation and row rather than stale controls.");
    }

    [UnityTest]
    public IEnumerator BuildingStage_ClickBuildPaysCosts_AndShortageReleasesInputUntilAffordable()
    {
        yield return LoadFreshGame();
        SetFixtureResources(new ExpantaNum(1000000));
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.StageId, Is.EqualTo("building"));
        BuildingDef definition = DataBase<BuildingDef>.Find(root.OnboardingStageForEditor.TargetId);
        ClickTarget();
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Select));
        ClickTarget();
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Build));
        yield return CaptureReferenceFrame("building-build-2640x1200.png");
        ExpantaNum beforeCount = BuildingManager.Instance.GetState(definition).Amount;

        SetFixtureResources(ExpantaNum.Zero);
        root.RefreshAutomaticUIForEditor();
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.CanInteract, Is.False);
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.False);
        Assert.That(EventSystem.current.sendNavigationEvents, Is.True);
        Assert.That(TutorialManager.Current.CompletedStageIds, Does.Not.Contain("building"));

        SetFixtureResources(new ExpantaNum(1000000));
        root.RefreshAutomaticUIForEditor();
        root.ShowBuildingDetailsForEditor(definition);
        yield return Settle();
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.True, OnboardingDiagnostic());
        ClickAt(root.transform.Find("SafeAreaRoot/LeftNavigation/NavigationButtons/Nav_Research") as RectTransform);
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Build));
        var beforeResources = new Dictionary<ResourceDef, ExpantaNum>();
        foreach (Pair<ResourceDef, ExpantaNum> cost in definition.ResourceRequirements)
            beforeResources[cost.First] = ResourceManager.Instance.GetAmount(cost.First);
        ClickTarget();
        yield return Settle();
        Assert.That((BuildingManager.Instance.GetState(definition).Amount - beforeCount).ApproximatelyEquals(ExpantaNum.One), Is.True,
            "The real action click must construct exactly one building.");
        bool paidResource = false;
        foreach (var entry in beforeResources)
            paidResource |= ResourceManager.Instance.GetAmount(entry.Key) < entry.Value;
        Assert.That(paidResource, Is.True, "Construction must consume the authored resource costs.");
        Assert.That(TutorialManager.Current.CompletedStageIds, Does.Contain("building"));
    }

    [UnityTest]
    public IEnumerator ResearchStage_ClickNodeAndActionStartsResearch_AndUnpaidStepDoesNotBlock()
    {
        yield return LoadFreshGame();
        PrepareResearchUnlocked();
        TutorialManager tutorial = TutorialManager.Current;
        ResearchDef definition = DataBase<ResearchDef>.Find("Agriculture");
        Assert.That(ResearchManager.Instance.CanAccessResearch(definition) &&
            ResearchManager.Instance.ArePrerequisitesCompleted(definition), Is.True);
        tutorial.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            StagesInitialized = true,
            CompletedStageIds = new List<string> { "resources", "building", "population" },
            ActiveStageId = "research", StageTargetId = definition.Id
        }, GameManager.Instance.State.TechLevel);
        SetFixtureResources(new ExpantaNum(1000000));
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.StageId, Is.EqualTo("research"));
        ClickTarget();
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Select));
        ClickTarget();
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.StartResearch));
        SetFixtureResources(ExpantaNum.Zero);
        root.RefreshAutomaticUIForEditor();
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.CanInteract, Is.False);
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.False);
        Assert.That(EventSystem.current.sendNavigationEvents, Is.True);
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain("research"));
        SetFixtureResources(new ExpantaNum(1000000));
        root.RefreshAutomaticUIForEditor();
        root.ShowResearchDetailsForEditor(definition);
        yield return Settle();
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.True);
        ClickTarget();
        yield return Settle();
        ResearchState state = ResearchManager.Instance.GetState(definition);
        Assert.That(state.Status == ResearchStatus.Researching || state.Status == ResearchStatus.Completed, Is.True,
            "The real action click must start the paid research rather than merely closing the tutorial.");
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain("research"));
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.False);
        yield return CaptureReferenceFrame("agriculture-research-wait.png", false);
        yield return FinishResearchWithTicks();
        Assert.That(tutorial.CompletedStageIds, Does.Contain("research"));
        Assert.That(root.OnboardingStageForEditor.TargetId, Is.EqualTo("Farm"));
    }

    [UnityTest]
    public IEnumerator ActiveGuideLeavesSimulationRunningAndCalendarAdvancing()
    {
        yield return LoadFreshGame();
        SimulationManager.Instance.SetRunning(true);
        yield return Settle();
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.True);
        Assert.That(SimulationManager.Instance.IsRunning, Is.True);
        int before = GameManager.Instance.State.CalendarDays;
        SimulationManager.Instance.ManualTick(11d);
        Assert.That(GameManager.Instance.State.CalendarDays, Is.GreaterThan(before));
        Assert.That(SimulationManager.Instance.IsRunning, Is.True,
            "An active tutorial must not change the simulation run state.");
    }

    [UnityTest]
    public IEnumerator ResearchSelection_BlocksNestedPinAndRoutesPointerToRequiredNode()
    {
        yield return LoadFreshGame();
        PrepareResearchUnlocked();
        ResearchDef definition = DataBase<ResearchDef>.Find("Agriculture");
        TutorialManager.Current.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            StagesInitialized = true,
            CompletedStageIds = new List<string> { "resources", "building", "population" },
            ActiveStageId = "research", StageTargetId = definition.Id
        }, GameManager.Instance.State.TechLevel);
        SetFixtureResources(new ExpantaNum(1000000));
        yield return Settle();
        ClickTarget();
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Select));
        RectTransform target = root.OnboardingTargetForEditor;
        Button node = target.GetComponent<Button>();
        Assert.That(node, Is.Not.Null);
        // The current research prefab has no pin. Nest the real authored pin as
        // a fixture to cover overlapping child controls without changing assets.
        GameObject resourceCard = Resources.Load<GameObject>("UI/Kingdom/KingdomUIResourceCard");
        Button pin = Object.Instantiate(resourceCard.transform.Find("PinButton").GetComponent<Button>(), target);
        // The resource pin is authored against its row's left edge. Center the
        // injected control inside this node so the raycast tests overlapping
        // controls rather than a floating-point point on the node's boundary.
        RectTransform pinRect = (RectTransform)pin.transform;
        pinRect.anchorMin = pinRect.anchorMax = new Vector2(.5f, .5f);
        pinRect.anchoredPosition = Vector2.zero;
        pin.onClick.AddListener(() => UIPinPreferences.SetPinned(UIPinPreferences.Research, definition.Id,
            !UIPinPreferences.IsPinned(UIPinPreferences.Research, definition.Id)));
        root.RefreshOnboardingForEditor();
        Assert.That(pin.gameObject.activeInHierarchy, Is.True);
        Assert.That(OnboardingInputGate.Allows(node.transform), Is.True);
        Assert.That(OnboardingInputGate.Allows(pin.transform), Is.False,
            "A nested action must not inherit the selected node's input permission.");
        string key = "Kingdom.UI.Pin." + UIPinPreferences.Research.Length + ":" + UIPinPreferences.Research + ":" + definition.Id;
        bool hadPreference = PlayerPrefs.HasKey(key);
        int savedPreference = PlayerPrefs.GetInt(key);
        bool wasPinned = UIPinPreferences.IsPinned(UIPinPreferences.Research, definition.Id);
        try
        {
            Canvas.ForceUpdateCanvases();
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, pinRect.TransformPoint(pinRect.rect.center));
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(target, point, camera), Is.True,
                "The injected pin must overlap the required node in the live canvas coordinate system.");
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(node.gameObject),
                "Before dispatch, the live raycast must route the overlapping pin position to the required node.");
            GameObject hit = ClickAt((RectTransform)pin.transform);
            Assert.That(hit, Is.Not.Null, "The node remains clickable beneath its excluded pin graphic.");
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit), Is.SameAs(node.gameObject),
                "The pointer must reach the required node rather than its nested pin action.");
            yield return Settle();
            Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.StartResearch));
            Assert.That(UIPinPreferences.IsPinned(UIPinPreferences.Research, definition.Id), Is.EqualTo(wasPinned),
                "The blocked pin must leave its user preference unchanged.");
        }
        finally
        {
            if (hadPreference) PlayerPrefs.SetInt(key, savedPreference);
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    [UnityTest]
    public IEnumerator ProductionChainStage_UsesVisibleGraphTargetAndCompletesInspection() => InspectUnlockedStage("production-chain");

    [UnityTest]
    public IEnumerator WorkshopStage_UsesUnlockedRecipeAndCompletesInspection() => InspectUnlockedStage("workshop");

    [UnityTest]
    public IEnumerator SectorsStage_UsesUnlockedSectorAndCompletesInspection() => InspectUnlockedStage("sectors");

    [UnityTest]
    public IEnumerator PopulationGrowthDoesNotInterruptAgriculture()
    {
        yield return LoadFreshGame();
        PrepareResearchUnlocked();
        GameManager.Instance.State.Population.RestorePopulationForEditor(ExpantaNum.One);
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.TargetId, Is.EqualTo("Agriculture"));
        Assert.That(TutorialManager.Current.CompletedStepIds, Does.Contain("population"));
        AssertSharedTarget();
    }

    [UnityTest]
    public IEnumerator EraStage_UsesAvailableTransitionAndCompletesInspection() => InspectUnlockedStage("era-goal");

    private IEnumerator InspectUnlockedStage(string stageId)
    {
        yield return LoadFreshGame();
        string[] allStages = { "resources", "building", "population", "farm-goal", "research", "stone-chain", "first-transition", "production-chain", "era-goal", "workshop", "sectors" };
        TutorialManager tutorial = TutorialManager.Current;
        tutorial.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            StagesInitialized = true, CompletedStageIds = new List<string>(allStages)
        }, GameManager.Instance.State.TechLevel);
        root.RefreshOnboardingForEditor();
        GameManager.Instance.AdvanceTechLevelForEditor(TechLevel.Spacer);
        foreach (ResearchDef research in DataBase<ResearchDef>.All)
            ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
        ProgressionModifierManager.Rebuild(new List<ResearchState>(ResearchManager.Instance.States.Values));
        GameManager.Instance.State.RestorePopulationCapacityExactForEditor(new ExpantaNum(100), ExpantaNum.Zero);
        SetFixtureResources(new ExpantaNum(1000000));
        root.RefreshAutomaticUIForEditor();
        root.RefreshNavigationVisibilityForEditor();
        root.SetPage("Overview");
        var completed = new List<string>(allStages);
        completed.Remove(stageId);
        tutorial.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            StagesInitialized = true, CompletedStageIds = completed,
            // This fixture has already left the first era; test later operation lessons.
            CompletedStepIds = new List<string> { "era-goal" }
        }, GameManager.Instance.State.TechLevel);
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor, Is.Not.Null, "The unlocked fixture must expose " + stageId);
        Assert.That(root.OnboardingStageForEditor.StageId, Is.EqualTo(stageId));
        if (root.OnboardingStageForEditor.Action == TutorialStageAction.Navigate)
        {
            ClickTarget();
            yield return Settle();
        }
        if (root.OnboardingStageForEditor.Action == TutorialStageAction.Select)
        {
            Assert.That(IsInsideScreen(root.OnboardingTargetForEditor), Is.True, stageId + " selection target is visible.");
            ClickTarget();
            yield return Settle();
        }
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Inspect), OnboardingDiagnostic());
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.True, OnboardingDiagnostic());
        ClickAt(OverlayButton("Next"));
        yield return Settle();
        Assert.That(tutorial.CompletedStageIds, Does.Contain(stageId));
        Assert.That(tutorial.SkippedStageIds, Does.Not.Contain(stageId));
        Assert.That(root.OnboardingStageForEditor, Is.Null, "Only the requested fixture stage remained incomplete.");
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.False);
    }

    [UnityTest]
    public IEnumerator ClippedTargetCannotPassPointerOutsideViewport_AndReleasesGate()
    {
        yield return LoadFreshGame();
        ClickTarget();
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Select));
        RectTransform target = root.OnboardingTargetForEditor;
        RectMask2D mask = target.GetComponentInParent<RectMask2D>();
        RectTransform viewport = mask.rectTransform;
        Vector3 original = target.position;
        try
        {
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            float width = Mathf.Abs(viewport.InverseTransformPoint(corners[2]).x - viewport.InverseTransformPoint(corners[0]).x);
            Assert.That(width, Is.GreaterThan(8f));
            Vector3 center = viewport.InverseTransformPoint(target.TransformPoint(target.rect.center));
            target.position += viewport.TransformVector(new Vector3(viewport.rect.xMax - width * .25f - center.x, 0, 0));
            Vector3 shiftedCenter = viewport.InverseTransformPoint(target.TransformPoint(target.rect.center));
            Assert.That(shiftedCenter.x, Is.LessThan(viewport.rect.xMax), "Center remains visible while the target edge is clipped.");
            Canvas canvas = root.GetComponent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 clippedPoint = RectTransformUtility.WorldToScreenPoint(camera,
                viewport.TransformPoint(new Vector3(viewport.rect.xMax + 2f, shiftedCenter.y, 0)));
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(target, clippedPoint, camera), Is.True);
            Assert.That(mask.IsRaycastLocationValid(clippedPoint, camera), Is.False);
            Assert.That(root.GetComponent<OnboardingInputGate>().IsRaycastLocationValid(clippedPoint, camera), Is.False,
                "The allowed target rectangle must not bypass its real scroll mask.");
            root.RefreshOnboardingForEditor();
            Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.False,
                "A partially clipped target must withdraw the blocking layer.");
            Assert.That(EventSystem.current.sendNavigationEvents, Is.True);
        }
        finally { target.position = original; }
        yield return Settle();
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.True, OnboardingDiagnostic());
    }

    [UnityTest]
    public IEnumerator QueuedResearchWaitingShowsStatusWithoutBlockingOrCompletingStage()
    {
        yield return LoadFreshGame();
        PrepareResearchUnlocked();
        SetFixtureResources(ExpantaNum.Zero);
        TutorialManager tutorial = TutorialManager.Current;
        ResearchManager research = ResearchManager.Instance;
        ResearchDef agriculture = DataBase<ResearchDef>.Find("Agriculture");
        tutorial.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            StagesInitialized = true, FarmGoalStageSchemaApplied = true,
            CompletedStageIds = new List<string> { "resources", "building", "population" },
            ActiveStageId = "research", StageTargetId = agriculture.Id
        }, TechLevel.Animal);
        yield return Settle();
        ClickTarget();
        yield return Settle();
        ClickTarget();
        yield return Settle();
        var body = root.transform.Find("SafeAreaRoot/OnboardingOverlay/Panel/Body").GetComponent<TMPro.TMP_Text>();
        Assert.That(research.CanPayResearchCost(agriculture, out string blocker), Is.False);
        Assert.That(body.text, Does.Contain(agriculture.Label), "The unpaid prompt must identify the selected research and its real blocker.");

        Assert.That(research.EnqueueResearch(agriculture), Is.True);
        yield return Settle();
        Assert.That(body.text, Is.EqualTo(root.OnboardingStageForEditor.Instruction), "The live panel must retain the queued status instead of replacing it with generic advice.");
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.False);
        Assert.That(OverlayButton("Next").gameObject.activeSelf, Is.False);
        Assert.That(EventSystem.current.sendNavigationEvents, Is.True);
        tutorial.AcknowledgeCurrentStage();
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain("research"));
        string unpaidInstruction = body.text;

        SetFixtureResources(new ExpantaNum(1000000));
        research.PayResearchCost(agriculture);
        yield return Settle();
        Assert.That(research.GetState(agriculture).CostPaid, Is.True);
        Assert.That(body.text, Is.EqualTo(root.OnboardingStageForEditor.Instruction).And.Not.EqualTo(unpaidInstruction));
        Assert.That(OverlayButton("Next").gameObject.activeSelf, Is.False);
        research.Tick(.01d);
        yield return FinishResearchWithTicks();
        Assert.That(tutorial.CompletedStageIds, Does.Contain("research"));
    }

    [UnityTest]
    public IEnumerator FarmGoalShortageNamesBuildingAndResumesAfterResourcesRecover()
    {
        yield return LoadFreshGame();
        PrepareResearchUnlocked();
        SetFixtureResources(ExpantaNum.Zero);
        TutorialManager tutorial = TutorialManager.Current;
        tutorial.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            StagesInitialized = true, FarmGoalStageSchemaApplied = true,
            CompletedStageIds = new List<string> { "resources", "building", "population", "research" }
        }, TechLevel.Animal);
        ResearchManager.Instance.GetState(DataBase<ResearchDef>.Find("Agriculture")).SetStatusForEditor(ResearchStatus.Completed);
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.StageId, Is.EqualTo("farm-goal")); // Stable stage protocol.
        ClickTarget();
        yield return Settle();
        ClickTarget();
        yield return Settle();
        BuildingDef farm = DataBase<BuildingDef>.Find("Farm");
        var body = root.transform.Find("SafeAreaRoot/OnboardingOverlay/Panel/Body").GetComponent<TMPro.TMP_Text>();
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Build));
        Assert.That(root.OnboardingStageForEditor.CanInteract, Is.False);
        Assert.That(body.text, Does.Contain(farm.Label), "The waiting goal must name the building instead of showing only generic conditions.");
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.False);
        Assert.That(OverlayButton("Next").gameObject.activeSelf, Is.False);

        SetFixtureResources(new ExpantaNum(1000000));
        root.RefreshAutomaticUIForEditor();
        root.ShowBuildingDetailsForEditor(farm);
        yield return Settle();
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.True);
        ClickTarget();
        yield return Settle();
        Assert.That(BuildingManager.Instance.GetState(farm).Amount > ExpantaNum.Zero, Is.True);
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain("farm-goal"));
        GameManager.Instance.State.Population.RestorePopulationForEditor(ExpantaNum.One);
        SimulationManager.Instance.ManualTick(1d);
        yield return Settle();
        TutorialManager.Current.Evaluate();
        Assert.That(BuildingManager.Instance.GetState(farm).Efficiency > ExpantaNum.Zero, Is.True,
            "The production fixture must provide actual residents to operate the farm.");
        Assert.That(tutorial.CompletedStageIds, Does.Contain("farm-goal"));
    }

    [UnityTest]
    public IEnumerator FirstSession_RealButtonsAndNaturalProductionReachStoneAge()
    {
        Screen.SetResolution(2640, 1200, false);
        yield return LoadFreshGame();
        double simulatedSeconds = 0d;
        var visited = new HashSet<string>();
        for (int operation = 0; operation < 400 && GameManager.Instance.State.TechLevel == TechLevel.Animal; operation++)
        {
            yield return Settle();
            AssertSharedTarget();
            TutorialSnapshot action = root.TutorialSnapshotForEditor;
            TutorialStageSnapshot stage = root.OnboardingStageForEditor;
            Assert.That(stage, Is.Not.Null, "The natural first-session route must always expose its next action.");
            visited.Add(action.ActionId);
            if (stage.CanInteract)
            {
                Assert.That(stage.Action, Is.Not.EqualTo(TutorialStageAction.Inspect), "Only real operations advance this route.");
                string before = action.ActionId;
                ClickTarget();
                root.RefreshOnboardingForEditor();
                if (stage.Action == TutorialStageAction.Build)
                {
                    Debug.Log("[FirstSession] Built " + before + " at simulatedSeconds=" + simulatedSeconds);
                    if (before == "build:WoodHouse") yield return CaptureReferenceFrame("home-completed.png", false);
                    if (before == "build:Farm") yield return CaptureReferenceFrame("farm-completed.png", false);
                    if (before == "build:StoneCuttingWorkshop") yield return CaptureReferenceFrame("stone-workshop-completed.png", false);
                }
                yield return Settle();
                continue;
            }
            Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.False);
            Assert.That(EventSystem.current.sendNavigationEvents, Is.True);
            Assert.That(OverlayButton("Next").gameObject.activeSelf, Is.False);
            double seconds = 30d;
            ResearchState active = ResearchManager.Instance.ActiveResearch;
            if (active != null && ResearchManager.Instance.CurrentResearchSpeed > ExpantaNum.Zero)
                seconds = System.Math.Min(300d, (active.BaseCost - active.Progress).ToDouble() / ResearchManager.Instance.CurrentResearchSpeed.ToDouble() + .05d);
            SimulationManager.Instance.ManualTick(seconds);
            simulatedSeconds += seconds;
            Debug.Log("[FirstSession] Wait " + action.ActionId + " seconds=" + seconds + " total=" + simulatedSeconds + " blocker=" + action.Blocker);
            Assert.That(simulatedSeconds, Is.LessThan(40000d), "Natural production must remain bounded without grants.");
        }
        Assert.That(GameManager.Instance.State.TechLevel, Is.EqualTo(TechLevel.StoneAge)); // Era protocol.
        TutorialManager.Current.Evaluate();
        foreach (string milestone in new[] { "build:WoodHouse", "research:Agriculture", "build:Farm", "research:Quarry", "build:Quarry", "research:StoneProcessing", "build:StoneCuttingWorkshop", "research:StoneAgeSettlement", "research:CivilizationPlanning", "research:StoneAgeTransition" })
            Assert.That(visited, Does.Contain(milestone), "Natural button route must cover " + milestone);
        Assert.That(TutorialManager.Current.CompletedStepIds, Does.Contain("farm-supply").And.Contain("stone-supply").And.Contain("era-goal"));
        yield return Settle();
        yield return new WaitForSecondsRealtime(1.1f);
        root.RefreshAutomaticUIForEditor();
        yield return CaptureReferenceFrame("stone-age-opened.png", false);
        Debug.Log("[FirstSession] Natural first transition complete. simulatedSeconds=" + simulatedSeconds);
    }

    private void AssertSharedTarget()
    {
        TutorialSnapshot overview = root.TutorialSnapshotForEditor;
        TutorialStageSnapshot overlay = root.OnboardingStageForEditor;
        Assert.That(overview, Is.Not.Null);
        if (overlay == null) return;
        Assert.That(overview.NavigationPage, Is.EqualTo(overlay.Page));
        Assert.That(overview.NavigationTargetId, Is.EqualTo(overlay.TargetId));
    }

    private IEnumerator FinishResearchWithTicks()
    {
        for (int step = 0; step < 50 && ResearchManager.Instance.ActiveResearch != null; step++)
        {
            ResearchState active = ResearchManager.Instance.ActiveResearch;
            ExpantaNum speed = ResearchManager.Instance.CurrentResearchSpeed;
            double seconds = speed > ExpantaNum.Zero ? System.Math.Min(300d, ((active.BaseCost - active.Progress) / speed).ToDouble() + .05d) : 1d;
            SimulationManager.Instance.ManualTick(seconds);
            yield return Settle();
        }
        Assert.That(ResearchManager.Instance.ActiveResearch, Is.Null, "The paid fixture research must finish through simulation ticks.");
    }

    private static void SetFixtureResources(ExpantaNum amount)
    {
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            ResourceManager.Instance.SetAmount(resource, amount);
    }

    [UnityTest]
    public IEnumerator IllustrationPreviewDefersTheGateWithoutCompletingTheStage()
    {
        yield return LoadFreshGame();
        root.SetPage("Buildings");
        yield return Settle();
        string stageId = root.OnboardingStageForEditor.StageId;
        OnboardingInputGate gate = root.GetComponent<OnboardingInputGate>();
        Assert.That(gate.IsActive, Is.True);
        BuildingDef house = DataBase<BuildingDef>.Find("WoodHouse");
        Button artwork = root.GetDeconstructButtonForEditor(house).transform.parent.Find("BuildingIcon").GetComponent<Button>();
        artwork.onClick.Invoke();
        StoryIllustrationPreview preview = root.GetComponentInChildren<StoryIllustrationPreview>(true);
        Assert.That(preview.IsOpen, Is.True);
        root.RefreshOnboardingForEditor();
        yield return null;
        Assert.That(gate.IsActive, Is.False);
        Assert.That(OnboardingInputGate.BlocksNavigation, Is.False);
        Assert.That(TutorialManager.Current.CompletedStageIds, Does.Not.Contain(stageId));
        Assert.That(TutorialManager.Current.SkippedStageIds, Does.Not.Contain(stageId));
        RectTransform close = (RectTransform)preview.transform.Find("Header/Close");
        GameObject closeHit = ClickAt(close);
        Assert.That(closeHit != null && (closeHit.transform == close || closeHit.transform.IsChildOf(close)), Is.True,
            "Close must own the raycast: hit=" + closeHit?.name + " canvas=" + root.GetComponent<Canvas>().renderMode + " rect=" + close.rect + " position=" + close.position);
        Assert.That(preview.IsOpen, Is.False, "The close button must receive a real raycast while a tutorial is pending.");
        yield return Settle();
        Assert.That(gate.IsActive, Is.True);
        Assert.That(root.OnboardingStageForEditor.StageId, Is.EqualTo(stageId));
    }

    private void PrepareResearchUnlocked()
    {
        BuildingManager buildings = BuildingManager.Instance;
        buildings.SetAmountAndRatesForEditor(
            buildings.EnsureBuilding(DataBase<BuildingDef>.Find("WoodHouse")), ExpantaNum.One);
        root.RefreshAutomaticUIForEditor();
        root.RefreshNavigationVisibilityForEditor();
        Assert.That(ProgressionVisibilityRules.IsResearchUnlocked(GameManager.Instance.State), Is.True);
    }

    private IEnumerator LoadFreshGame()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        GameBootstrap bootstrap = null;
        for (int frame = 0; frame < 180; frame++)
        {
            foreach (GameObject sceneRoot in SceneManager.GetSceneByName("SampleScene").GetRootGameObjects())
            {
                if (root == null) root = sceneRoot.GetComponentInChildren<KingdomUIRoot>(true);
                if (bootstrap == null) bootstrap = sceneRoot.GetComponentInChildren<GameBootstrap>(true);
            }
            if (root != null && bootstrap != null && bootstrap.Completed) break;
            yield return null;
        }
        Assert.That(root, Is.Not.Null);
        Assert.That(bootstrap != null && bootstrap.Completed, Is.True);
        SimulationManager.Instance.SetRunning(false);
        TutorialManager.Current.ResetForNewGameForEditor();
        root.SetPage("Overview");
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor, Is.Not.Null);
    }

    private IEnumerator Settle()
    {
        root.RefreshOnboardingForEditor();
        Canvas.ForceUpdateCanvases();
        yield return null;
        root.RefreshOnboardingForEditor();
        Canvas.ForceUpdateCanvases();
        yield return null;
        float deadline = Time.realtimeSinceStartup + 8f;
        for (int frame = 0; frame < 1200 && Time.realtimeSinceStartup < deadline; frame++)
        {
            TutorialStageSnapshot stage = root.OnboardingStageForEditor;
            if (stage == null || !stage.CanInteract ||
                (root.OnboardingTargetForEditor != null && root.GetComponent<OnboardingInputGate>().IsActive))
                yield break;
            root.RefreshOnboardingForEditor();
            Canvas.ForceUpdateCanvases();
            yield return null;
        }
        Assert.Fail("The live tutorial target did not become ready within 8 seconds. " + OnboardingDiagnostic());
    }

    private string OnboardingDiagnostic()
    {
        TutorialStageSnapshot stage = root.OnboardingStageForEditor;
        RectTransform target = root.OnboardingTargetForEditor;
        Button action = root.DetailActionButtonForEditor;
        return "stage=" + (stage == null ? "null" : stage.StageId + "/" + stage.Action + "/" + stage.TargetId +
            " canInteract=" + stage.CanInteract) + " target=" + (target == null ? "null" : target.name +
            " active=" + target.gameObject.activeInHierarchy + " rect=" + target.rect + " position=" + target.position) +
            " action=" + (action == null ? "null" : "active=" + action.gameObject.activeInHierarchy +
            " interactable=" + action.IsInteractable() + " rect=" + ((RectTransform)action.transform).rect +
            " position=" + action.transform.position) + " selectedBuilding=" +
            (root.SelectedBuildingForEditor == null ? "null" : root.SelectedBuildingForEditor.Id);
    }

    private RectTransform OverlayButton(string name)
    {
        Transform button = root.transform.Find("SafeAreaRoot/OnboardingOverlay/Panel/" + name);
        Assert.That(button, Is.Not.Null);
        return (RectTransform)button;
    }

    private void ClickTarget()
    {
        RectTransform target = root.OnboardingTargetForEditor;
        Button targetButton = target.GetComponentInParent<Button>();
        GameObject hit = ClickAt(target);
        Assert.That(hit != null && (hit.transform == target || hit.transform.IsChildOf(target) ||
            (targetButton != null && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) == targetButton.gameObject)), Is.True,
            "The overlay must leave the indicated control reachable. target=" + target.name +
            " hit=" + hit.name + " handler=" + ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit)?.name +
            " expected=" + targetButton?.name);
    }

    private static GameObject ClickAt(RectTransform rect)
    {
        Canvas.ForceUpdateCanvases();
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = position, pressPosition = position, button = PointerEventData.InputButton.Left,
            eligibleForClick = true, clickCount = 1
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        if (hits.Count == 0) return null;
        GameObject hit = hits[0].gameObject;
        pointer.pointerCurrentRaycast = hits[0];
        pointer.pointerPressRaycast = hits[0];
        GameObject pressed = ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
        if (pressed == null) pressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
        pointer.pointerPress = pressed;
        pointer.rawPointerPress = hit;
        if (pressed != null)
        {
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
            if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) == pressed)
                ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerClickHandler);
        }
        return hit;
    }

    private static bool IsInsideScreen(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 bottom = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 top = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        return bottom.x >= -1 && bottom.y >= -1 && top.x <= Screen.width + 1 && top.y <= Screen.height + 1;
    }

    private IEnumerator CaptureReferenceFrame(string filename = "onboarding-2640x1200.png", bool requireGate = true)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.Log("[StagedOnboarding] No GPU screenshot in null graphics mode.");
            yield break;
        }
        Canvas canvas = root.GetComponent<Canvas>();
        root.RefreshTopInfoForEditor();
        RenderMode oldMode = canvas.renderMode;
        Camera oldCamera = canvas.worldCamera;
        float oldDistance = canvas.planeDistance;
        RenderTexture oldActive = RenderTexture.active;
        GameObject cameraObject = new GameObject("OnboardingEvidenceCamera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 1000;
        RenderTexture target = new RenderTexture(2640, 1200, 24);
        Texture2D capture = null;
        try
        {
            target.Create();
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100;
            root.RefreshOnboardingForEditor();
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.EqualTo(requireGate), OnboardingDiagnostic());
            var body = root.transform.Find("SafeAreaRoot/OnboardingOverlay/Panel/Body").GetComponent<TMPro.TMP_Text>();
            body.ForceMeshUpdate();
            Assert.That(body.preferredHeight, Is.LessThanOrEqualTo(body.rectTransform.rect.height + 1f), "Tutorial body must fit its authored panel.");
            camera.Render();
            RenderTexture.active = target;
            capture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            capture.Apply();
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "data", "experience-20261030", "first-session", "screenshots"));
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, filename), capture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = oldActive;
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            canvas.planeDistance = oldDistance;
            camera.targetTexture = null;
            if (capture != null) Object.Destroy(capture);
            target.Release();
            Object.Destroy(target);
            Object.Destroy(cameraObject);
        }
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return Settle();
    }
}
