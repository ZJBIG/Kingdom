using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class PlayerQaFreshSessionPlayModeTests
{
    private string saveRoot;
    private KingdomUIRoot root;
    private int width, height;
    private GameObject renderHost;
    private RenderTexture renderTarget;

    [SetUp] public void SetUp()
    {
        width = Screen.width;
        height = Screen.height;
        saveRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "PlayerQa-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [UnityTearDown] public IEnumerator TearDown()
    {
        Scene sample = SceneManager.GetSceneByName("SampleScene");
        if (sample.IsValid() && sample.isLoaded)
            foreach (GameObject host in sample.GetRootGameObjects()) Object.DestroyImmediate(host);
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        if (tutorial != null) Object.DestroyImmediate(tutorial.gameObject);
        if (music != null) Object.DestroyImmediate(music.gameObject);
        if (renderHost != null) Object.DestroyImmediate(renderHost);
        if (renderTarget != null) { renderTarget.Release(); Object.DestroyImmediate(renderTarget); }
        yield return null;
        Assert.That(Object.FindObjectOfType<SaveManager>(), Is.Null);
        SaveManager.ClearSaveRootOverrideForTests();
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
        Assert.That(Directory.Exists(saveRoot), Is.False);
        Screen.SetResolution(width, height, false);
    }

    [UnityTest] public IEnumerator FreshTutorial_RealRaycastsBuildFirstHouseWithoutGrants()
    {
        Screen.SetResolution(2640, 1200, false);
        for (int frame = 0; frame < 5; frame++) yield return null;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
        for (int i = 0; i < 240; i++)
        {
            root = Object.FindObjectOfType<KingdomUIRoot>();
            if (root != null && Object.FindObjectOfType<GameBootstrap>()?.Completed == true) break;
            yield return null;
        }
        Assert.That(root, Is.Not.Null);
        Assert.That(Object.FindObjectOfType<GameBootstrap>().Completed, Is.True);
        // Batch Editor Game View remains 640x480. Use the project's existing
        // supported render-target method; do not claim native desktop input.
        renderHost = new GameObject("PlayerQaSupportedViewport", typeof(Camera));
        Camera renderCamera = renderHost.GetComponent<Camera>();
        renderCamera.transform.position = new Vector3(0, 0, -10);
        renderTarget = new RenderTexture(2640, 1200, 24);
        renderTarget.Create();
        renderCamera.targetTexture = renderTarget;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = renderCamera;
        canvas.planeDistance = 100;
        for (int frame = 0; frame < 5; frame++) yield return null;
        root.RefreshOnboardingForEditor();
        Canvas.ForceUpdateCanvases();
        TestContext.WriteLine($"QA startupMs={watch.Elapsed.TotalMilliseconds:F2} graphics={SystemInfo.graphicsDeviceName} gameView={Screen.width}x{Screen.height} renderTarget=2640x1200");
        SimulationManager.Instance.SetRunning(false);
        var wood = DataBase<ResourceDef>.Find("WoodLog");
        var house = DataBase<BuildingDef>.Find("WoodHouse");
        Assert.That(BuildingManager.Instance.GetState(house).Amount, Is.EqualTo(ExpantaNum.Zero), "Discrete initial building count.");
        double advanced = 0;
        for (int operation = 0; operation < 40; operation++)
        {
            root.RefreshAutomaticUIForEditor();
            root.RefreshOnboardingForEditor();
            Canvas.ForceUpdateCanvases();
            yield return null;
            root.RefreshOnboardingForEditor();
            Canvas.ForceUpdateCanvases();
            var stage = root.OnboardingStageForEditor;
            TestContext.WriteLine($"QA tutorial operation={operation} stage={stage?.StageId} action={stage?.Action} interact={stage?.CanInteract} wood={ResourceManager.Instance.GetAmount(wood)} houses={BuildingManager.Instance.GetState(house).Amount}");
            if (BuildingManager.Instance.GetState(house).Amount >= ExpantaNum.One) break;
            if (stage != null && stage.CanInteract)
            {
                RectTransform target = stage.Action == TutorialStageAction.Inspect
                    ? root.transform.Find("SafeAreaRoot/OnboardingOverlay/Panel/Next") as RectTransform
                    : root.OnboardingTargetForEditor;
                Assert.That(target, Is.Not.Null);
                Click(target);
            }
            else
            {
                // Accelerated waiting only; no inventory, unlock or completion edits.
                for (int tick = 0; tick < 100; tick++) SimulationManager.Instance.ManualTick(.1d);
                advanced += 10;
            }
            yield return null;
        }
        Assert.That(BuildingManager.Instance.GetState(house).Amount, Is.EqualTo(ExpantaNum.One), "Exactly one normal paid UI construction.");
        Assert.That(GameManager.Instance.State.HasBuiltHousing, Is.True);
        root.RefreshAutomaticUIForEditor();
        Canvas.ForceUpdateCanvases();
        yield return null;
        Assert.That(TutorialManager.Current.CompletedStageIds, Does.Contain("resources"));
        Assert.That(TutorialManager.Current.CompletedStageIds, Does.Contain("building"));
        TestContext.WriteLine($"QA milestone=first-house acceleratedSeconds={advanced} wood={ResourceManager.Instance.GetAmount(wood)}");
        for (int i = 0; i < 20; i++)
        {
            TutorialManager.Current.SkipCurrentStage();
            root.RefreshOnboardingForEditor();
            yield return null;
        }
        foreach (string page in new[] { "Overview", "Buildings", "Research", "Resources", "Workshop", "Story" })
        {
            for (int visit = 0; visit < 3; visit++)
            {
                watch.Restart();
                root.SetPage(page);
                Canvas.ForceUpdateCanvases();
                TestContext.WriteLine($"QA page={page} visit={visit} synchronousMs={watch.Elapsed.TotalMilliseconds:F2}");
                yield return null;
                root.SetPage("Overview");
                yield return null;
            }
        }
        watch.Restart();
        int frames = 0;
        double largest = 0;
        double last = Time.realtimeSinceStartupAsDouble;
        while (watch.Elapsed.TotalSeconds < 5)
        {
            yield return null;
            double now = Time.realtimeSinceStartupAsDouble;
            largest = Math.Max(largest, (now - last) * 1000);
            last = now;
            frames++;
        }
        TestContext.WriteLine($"QA idle frames={frames} seconds={watch.Elapsed.TotalSeconds:F3} meanMs={watch.Elapsed.TotalMilliseconds / frames:F3} maxMs={largest:F3} buildings=1 research=0 simulationPaused=true");
        Assert.That(SaveManager.Instance.SaveNow(true), Is.True);
        Assert.That(File.Exists(Path.Combine(saveRoot, "KingdomSave-v1.json")), Is.True);
    }

    private static void Click(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point, pressPosition = point, button = PointerEventData.InputButton.Left, eligibleForClick = true };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits, Is.Not.Empty, "The actual target must be raycast reachable.");
        GameObject hit = hits[0].gameObject;
        TestContext.WriteLine($"QA click target={rect.name} point={point} hit={hit.name} handler={ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit)?.name}");
        pointer.pointerCurrentRaycast = hits[0];
        pointer.pointerPressRaycast = hits[0];
        GameObject pressed = ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler)
            ?? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
        Assert.That(pressed, Is.Not.Null);
        pointer.pointerPress = pressed;
        pointer.rawPointerPress = hit;
        ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerClickHandler);
    }
}
