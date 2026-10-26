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
    public IEnumerator ResourceStage_RealRaycastsBlockUnrelatedNavigationAndAdvanceTargetClicks()
    {
        Screen.SetResolution(2640, 1200, false);
        yield return LoadFreshGame();
        OnboardingInputGate gate = root.GetComponent<OnboardingInputGate>();
        Assert.That(gate.IsActive, Is.True);
        Assert.That(root.OnboardingStageForEditor.StageId, Is.EqualTo("resources"), "Stage IDs are protocol constants.");
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Navigate));
        Assert.That(EventSystem.current.sendNavigationEvents, Is.False);
        RectTransform unrelated = root.transform.Find(
            "SafeAreaRoot/LeftNavigation/NavigationButtons/Nav_Buildings") as RectTransform;
        Assert.That(unrelated, Is.Not.Null);
        ClickAt(unrelated);
        yield return null;
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Navigate),
            "A blocked navigation click must leave the required operation pending.");
        Assert.That(root.transform.Find("SafeAreaRoot/Content/PageHost/Buildings").gameObject.activeInHierarchy, Is.False);

        yield return CaptureReferenceFrame();
        ClickTarget();
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Select));
        Assert.That(root.transform.Find("SafeAreaRoot/Content/PageHost/Resources").gameObject.activeInHierarchy, Is.True);
        RectTransform row = root.OnboardingTargetForEditor;
        Assert.That(row, Is.Not.Null);
        Assert.That(IsInsideScreen(row), Is.True, "The selected row must be brought into the visible viewport.");
        yield return CaptureReferenceFrame("resource-select-2640x1200.png");
        ClickTarget();
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor.Action, Is.EqualTo(TutorialStageAction.Inspect));
        ClickAt(OverlayButton("Next"));
        yield return Settle();
        Assert.That(TutorialManager.Current.CompletedStageIds, Does.Contain("resources"));
        Assert.That(TutorialManager.Current.SkippedStageIds, Does.Not.Contain("resources"));
        Assert.That(root.OnboardingStageForEditor == null || root.OnboardingStageForEditor.StageId != "resources", Is.True);
    }

    [UnityTest]
    public IEnumerator SkipEndsOnlyCurrentStage_AndDisableReleasesNavigation()
    {
        yield return LoadFreshGame();
        TutorialManager tutorial = TutorialManager.Current;
        string first = root.OnboardingStageForEditor.StageId;
        ClickAt(OverlayButton("Skip"));
        yield return Settle();
        Assert.That(tutorial.SkippedStageIds, Does.Contain(first));
        Assert.That(tutorial.CompletedStageIds, Does.Not.Contain(first));
        Assert.That(root.OnboardingStageForEditor, Is.Null,
            "With fewer than 80 starting wood, no next affordable building target exists yet.");
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.False);
        SimulationManager.Instance.ManualTick(30d);
        root.RefreshAutomaticUIForEditor();
        yield return Settle();
        Assert.That(root.OnboardingStageForEditor, Is.Not.Null,
            "Skipping resources must leave the next available system eligible for its own introduction.");
        Assert.That(tutorial.SkippedStageIds, Does.Contain(first));
        Assert.That(root.OnboardingStageForEditor.StageId, Is.Not.EqualTo(first));
        root.gameObject.SetActive(false);
        yield return null;
        Assert.That(OnboardingInputGate.BlocksNavigation, Is.False);
        Assert.That(EventSystem.current.sendNavigationEvents, Is.True);
        root.gameObject.SetActive(true);
        yield return Settle();
        Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.True, OnboardingDiagnostic());
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
        ClickAt(OverlayButton("Skip"));
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
        ResearchDef definition = DataBase<ResearchDef>.Find("ControlledFire");
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
        Assert.That(tutorial.CompletedStageIds, Does.Contain("research"));
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
        ResearchDef definition = DataBase<ResearchDef>.Find("ControlledFire");
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
    public IEnumerator PopulationStage_UsesPopulationDisplayAndCompletesInspection() => InspectUnlockedStage("population");

    [UnityTest]
    public IEnumerator EraStage_UsesAvailableTransitionAndCompletesInspection() => InspectUnlockedStage("era-goal");

    private IEnumerator InspectUnlockedStage(string stageId)
    {
        yield return LoadFreshGame();
        string[] allStages = { "resources", "building", "population", "research", "production-chain", "era-goal", "workshop", "sectors" };
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
            StagesInitialized = true, CompletedStageIds = completed
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

    private static void SetFixtureResources(ExpantaNum amount)
    {
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            ResourceManager.Instance.SetAmount(resource, amount);
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
        for (int frame = 0; frame < 180; frame++)
        {
            TutorialStageSnapshot stage = root.OnboardingStageForEditor;
            if (stage == null || !stage.CanInteract ||
                (root.OnboardingTargetForEditor != null && root.GetComponent<OnboardingInputGate>().IsActive))
                yield break;
            root.RefreshOnboardingForEditor();
            Canvas.ForceUpdateCanvases();
            yield return null;
        }
        Assert.Fail("The live tutorial target did not become ready within 180 frames. " + OnboardingDiagnostic());
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
        Assert.That(hit.transform == target || hit.transform.IsChildOf(target) ||
            (targetButton != null && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) == targetButton.gameObject), Is.True,
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

    private IEnumerator CaptureReferenceFrame(string filename = "onboarding-2640x1200.png")
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
            yield return Settle();
            Assert.That(root.GetComponent<OnboardingInputGate>().IsActive, Is.True, OnboardingDiagnostic());
            camera.Render();
            RenderTexture.active = target;
            capture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            capture.Apply();
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "data", "player-qa-fixes-20261025", "onboarding"));
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
