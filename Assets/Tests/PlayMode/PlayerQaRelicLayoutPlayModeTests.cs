using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class PlayerQaRelicLayoutPlayModeTests
{
    private string saveRoot;
    private GameObject cameraHost;
    private RenderTexture target;
    private KingdomUIRoot ui;

    [SetUp]
    public void SetUp()
    {
        saveRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "PlayerQaRelic-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        // Save writers must be destroyed while their paths still point at the fixture.
        foreach (SaveManager writer in Object.FindObjectsOfType<SaveManager>(true))
            if (writer != null) Object.DestroyImmediate(writer.gameObject);
        Scene sample = SceneManager.GetSceneByName("SampleScene");
        if (sample.IsValid() && sample.isLoaded)
            foreach (GameObject host in sample.GetRootGameObjects())
                if (host != null) Object.DestroyImmediate(host);
        foreach (TutorialManager manager in Object.FindObjectsOfType<TutorialManager>(true))
            if (manager != null) Object.DestroyImmediate(manager.gameObject);
        foreach (MusicManager manager in Object.FindObjectsOfType<MusicManager>(true))
            if (manager != null) Object.DestroyImmediate(manager.gameObject);
        foreach (UIButtonSoundManager manager in Object.FindObjectsOfType<UIButtonSoundManager>(true))
            if (manager != null) Object.DestroyImmediate(manager.gameObject);
        if (cameraHost != null) Object.DestroyImmediate(cameraHost);
        if (target != null) { target.Release(); Object.DestroyImmediate(target); }
        yield return null;
        Assert.That(Object.FindObjectsOfType<SaveManager>(true), Is.Empty);
        SaveManager.ClearSaveRootOverrideForTests();
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
        Assert.That(Directory.Exists(saveRoot), Is.False);
    }

    [UnityTest]
    public IEnumerator RelicConfirmation_AtSupportedRenderSizeDoesNotOverlapDescription()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        for (int frame = 0; frame < 240; frame++)
        {
            ui = Object.FindObjectOfType<KingdomUIRoot>();
            if (ui != null && Object.FindObjectOfType<GameBootstrap>()?.Completed == true) break;
            yield return null;
        }
        Assert.That(ui, Is.Not.Null);
        Assert.That(Object.FindObjectOfType<GameBootstrap>().Completed, Is.True);
        Assert.That(SaveManager.Instance.LastLoadCreatedNewGame, Is.True);
        SimulationManager.Instance.SetRunning(false);
        cameraHost = new GameObject("PlayerQaRelicCamera", typeof(Camera));
        Camera camera = cameraHost.GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10);
        target = new RenderTexture(2640, 1200, 24);
        target.Create();
        camera.targetTexture = target;
        Canvas canvas = ui.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 100;
        for (int frame = 0; frame < 5; frame++) yield return null;
        Canvas.ForceUpdateCanvases();

        // Match the existing Relic fixture. These grants establish a controlled
        // late-system state and are never evidence of natural progression.
        GameManager game = GameManager.Instance;
        RelicManager relic = game.Relic;
        Assert.That(relic.Definition, Is.Not.Null);
        game.State.RestoreCoreForEditor(0, TechLevel.Ultra, new ExpantaNum(1e8d), 0L);
        game.State.SetPowerSatisfactionForEditor(ExpantaNum.One);
        game.State.SetLogisticsSatisfactionForEditor(ExpantaNum.One);
        foreach (ResearchDef definition in DataBase<ResearchDef>.All)
        {
            Dictionary<ResourceDef, ExpantaNum> paid = new();
            foreach (Pair<ResourceDef, ExpantaNum> cost in definition.ResourceRequirements)
                if (cost.First != null && cost.Second > ExpantaNum.Zero) paid[cost.First] = cost.Second;
            ResearchState state = ResearchManager.Instance.GetState(definition);
            state.RestoreForEditor(state.BaseCost, true, true, paid);
        }
        ProgressionModifierManager.Rebuild(new List<ResearchState>(ResearchManager.Instance.States.Values),
            new List<WorkshopUpgradeState>(WorkshopManager.Instance.States.Values));
        foreach (ResourceDef definition in DataBase<ResourceDef>.All)
            ResourceManager.Instance.SetAmount(definition, new ExpantaNum(1e9d));
        foreach (BuildingDef definition in relic.Definition.RequiredBuildings)
            BuildingManager.Instance.EnsureBuilding(definition).SetAmountForEditor(ExpantaNum.One);
        game.Sectors.GetState(relic.Definition.Sector).SetUnlockedForEditor(true);
        game.Sectors.GetState(relic.Definition.Sector).SetOccupiedForEditor(true);
        game.UltraProject.State.RestoreForEditor(new UltraProjectStateSaveData
        {
            ProjectId = UltraProjectState.ProjectId, Doctrine = UltraProjectDoctrine.Stable,
            Status = UltraProjectStatus.Ready, CurrentStage = UltraProjectStage.Stabilization,
            StageProgress = "0", CompletedStages = new List<UltraProjectStage> { UltraProjectStage.Prototype }, StateVersion = 1
        });
        TutorialManager.Current.RestoreSaveData(new SaveManager.TutorialSaveData
        {
            StagesInitialized = true,
            CompletedStageIds = new List<string>
            {
                "resources", "building", "population", "research", "production-chain", "era-goal", "workshop", "sectors"
            }
        }, game.State.TechLevel);
        ui.RefreshOnboardingForEditor();
        ui.SetPage("Sectors");
        ui.ShowRelicDetailsForEditor();
        yield return null;
        Click("Investigate");
        Assert.That(relic.State.Status, Is.EqualTo(RelicStatus.Investigating));
        Assert.That(relic.Tick(relic.Definition.Investigation.DurationSeconds.ToDouble() * 2d), Is.True);
        ui.ShowRelicDetailsForEditor();
        Assert.That(relic.State.Status, Is.EqualTo(RelicStatus.AwaitingChoice));
        Click("Dismantle");
        Assert.That(ui.RelicConfirmationPendingForEditor, Is.True);
        for (int frame = 0; frame < 5; frame++) yield return null;
        Canvas.ForceUpdateCanvases();

        RectTransform body = ui.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body") as RectTransform;
        RectTransform actions = ui.RelicActionsForEditor;
        Assert.That(actions.GetComponent<Image>(), Is.Null,
            "Relic text uses the shared detail background without an opaque inner panel.");
        Assert.That(actions.gameObject.activeInHierarchy, Is.True,
            "The measured actions must still belong to the visible relic selection after live refresh.");
        Assert.That(actions.Find("Confirmation").gameObject.activeInHierarchy, Is.True);
        Assert.That(ui.RelicConfirmationPendingForEditor, Is.True);
        Assert.That(body.GetComponent<TMPro.TMP_Text>().text, Does.Contain(relic.Definition.Sector.Id),
            "The selected detail must remain the relic's actual sector; its normal body includes that sector description.");
        ScrollRect scroll = body.parent.parent.GetComponent<ScrollRect>();
        UIDetailRequirementScrollGesture gesture = scroll.GetComponent<UIDetailRequirementScrollGesture>();
        Assert.That(gesture, Is.Not.Null);
        float range = scroll.content.rect.height - scroll.viewport.rect.height;
        Assert.That(range, Is.GreaterThan(0f));
        // Put the real description/actions boundary in view. At the top of a
        // long sector description the actions legitimately remain offscreen.
        float boundaryOffset = -body.offsetMin.y;
        float scrollOffset = Mathf.Clamp(boundaryOffset - scroll.viewport.rect.height * .5f, 0f, range);
        gesture.SetNormalizedPosition(1f - scrollOffset / range);
        yield return null;
        Canvas.ForceUpdateCanvases();
        Assert.That(actions.gameObject.activeInHierarchy && ui.RelicConfirmationPendingForEditor, Is.True);
        Rect bodyBounds = ScreenBounds(body, camera);
        Rect actionBounds = ScreenBounds(actions, camera);
        Rect viewportBounds = ScreenBounds(scroll.viewport, camera);
        Assert.That(actionBounds.yMax, Is.InRange(viewportBounds.yMin, viewportBounds.yMax),
            "The screenshot must expose the actions/description boundary inside the actual viewport.");
        Assert.That(bodyBounds.yMin, Is.InRange(viewportBounds.yMin, viewportBounds.yMax));
        float overlap = Mathf.Max(0f, Mathf.Min(bodyBounds.yMax, actionBounds.yMax) - Mathf.Max(bodyBounds.yMin, actionBounds.yMin));
        Debug.Log($"[PlayerQa] RelicLayout gameView={Screen.width}x{Screen.height} renderTarget={target.width}x{target.height} canvas={((RectTransform)canvas.transform).rect.size} body={bodyBounds} actions={actionBounds} overlapPx={overlap:F3}");
        Assert.That(bodyBounds.width, Is.GreaterThan(0f));
        Assert.That(bodyBounds.height, Is.GreaterThan(0f));
        Assert.That(actionBounds.width, Is.GreaterThan(0f));
        Assert.That(actionBounds.height, Is.GreaterThan(0f));
        Assert.That(actionBounds.yMax, Is.LessThanOrEqualTo(bodyBounds.yMin + 1f),
            "Relic actions must remain below the description at the supported render size.");
    }

    private void Click(string path)
    {
        Button button = ui.RelicActionsForEditor.Find(path)?.GetComponent<Button>();
        Assert.That(button, Is.Not.Null);
        Assert.That(button.gameObject.activeInHierarchy && button.interactable, Is.True, path);
        button.onClick.Invoke();
    }

    private static Rect ScreenBounds(RectTransform rect, Camera camera)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
