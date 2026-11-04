using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class OnboardingPerformancePlayModeTests
{
    private string saveRoot;
    private TutorialManager initialTutorial;
    private MusicManager initialMusic;
    private UIButtonSoundManager initialSound;
    private KingdomUIRoot root;

    [SetUp]
    public void SetUp()
    {
        initialTutorial = Object.FindObjectOfType<TutorialManager>(true);
        initialMusic = Object.FindObjectOfType<MusicManager>(true);
        initialSound = Object.FindObjectOfType<UIButtonSoundManager>(true);
        saveRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "OnboardingPerformance-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Scene empty = SceneManager.CreateScene("OnboardingPerformanceTeardown-" + Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(empty);
        Scene sample = SceneManager.GetSceneByName("SampleScene");
        if (sample.IsValid() && sample.isLoaded) yield return SceneManager.UnloadSceneAsync(sample);
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        UIButtonSoundManager sound = Object.FindObjectOfType<UIButtonSoundManager>(true);
        if (tutorial != null && tutorial != initialTutorial) Object.DestroyImmediate(tutorial.gameObject);
        if (music != null && music != initialMusic) Object.DestroyImmediate(music.gameObject);
        if (sound != null && sound != initialSound) Object.DestroyImmediate(sound.gameObject);
        Assert.That(Object.FindObjectOfType<SaveManager>(true), Is.Null);
        SaveManager.ClearSaveRootOverrideForTests();
        string expectedParent = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"));
        Assert.That(Path.GetDirectoryName(saveRoot), Is.EqualTo(expectedParent));
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
    }

    private IEnumerator Load()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        for (int i = 0; i < 160 && Object.FindObjectOfType<GameBootstrap>()?.Completed != true; i++)
            yield return null;
        Assert.That(Object.FindObjectOfType<GameBootstrap>()?.Completed, Is.True);
        SimulationManager.Instance.SetRunning(false);
        root = Object.FindObjectOfType<KingdomUIRoot>();
        root.enabled = false;
        root.SetPage("Overview");
        Canvas.ForceUpdateCanvases();
    }

    [UnityTest, Category("Performance")]
    public IEnumerator MeasureStableOverviewRefresh()
    {
        yield return Load();
        Measure("overview-stable-refresh", 30, root.RefreshAutomaticUIForEditor);
    }

    [UnityTest, Category("Performance")]
    public IEnumerator MeasureStableGateWithCachedResearch()
    {
        yield return Load();
        root.SetPage("Research");
        root.SetPage("Overview");
        GetGate(out OnboardingInputGate gate, out RectTransform target, out RectTransform overlay,
            out Button next, out Button skip);
        Measure("onboarding-gate-cached-research", 100, () => gate.Configure(target, overlay, next, skip));
    }

    [UnityTest]
    public IEnumerator StableOverviewDoesNotRegenerateGuidanceMesh()
    {
        yield return Load();
        root.RefreshOnboardingForEditor();
        root.RefreshUI();
        TMP_Text guidance = root.transform.Find("SafeAreaRoot/Content/PageHost/Overview/PrimaryCard/Text").GetComponent<TMP_Text>();
        guidance.ForceMeshUpdate();
        int meshUpdates = 0;
        Action<TMP_TextInfo> observeMesh = _ => meshUpdates++;
        guidance.OnPreRenderText += observeMesh;
        try
        {
            for (int i = 0; i < 10; i++)
            {
                root.RefreshAutomaticUIForEditor();
                root.RefreshOnboardingForEditor();
            }
        }
        finally { guidance.OnPreRenderText -= observeMesh; }
        Assert.That(meshUpdates, Is.Zero, "Unchanged guidance must not force TMP mesh/layout rebuilds on every automatic refresh.");
        TutorialManager.Current.AcknowledgeCurrentStage();
        root.RefreshUI();
        root.RefreshOnboardingForEditor();
        Assert.That(guidance.text, Does.Contain(root.TutorialSnapshotForEditor.CurrentGoal));
    }

    [UnityTest]
    public IEnumerator ExplicitRefreshImmediatelyRestoresGuidanceText()
    {
        yield return Load();
        root.RefreshUI();
        TMP_Text guidance = root.transform.Find("SafeAreaRoot/Content/PageHost/Overview/PrimaryCard/Text").GetComponent<TMP_Text>();
        string expected = guidance.text;
        Assert.That(expected, Is.Not.Empty);
        guidance.text = string.Empty;
        root.RefreshUI();
        Assert.That(guidance.text, Is.EqualTo(expected),
            "Explicit refresh must update immediately while the automatic refresh timer is still fresh.");
    }

    [UnityTest]
    public IEnumerator SameActionAffordabilityChangeImmediatelyRefreshesGuidance()
    {
        yield return Load();
        TutorialManager tutorial = TutorialManager.Current;
        tutorial.EvaluateStage();
        tutorial.SkipCurrentStage();
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            ResourceManager.Instance.SetAmount(resource, ExpantaNum.Zero);
        root.RefreshOnboardingForEditor();
        root.RefreshUI();
        TutorialSnapshot before = root.TutorialSnapshotForEditor;
        Assert.That(before.ActionId, Is.EqualTo("build:WoodHouse"));
        Assert.That(before.CanAct, Is.False);
        TMP_Text guidance = root.transform.Find("SafeAreaRoot/Content/PageHost/Overview/PrimaryCard/Text").GetComponent<TMP_Text>();
        string previousText = guidance.text;

        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
            ResourceManager.Instance.SetAmount(resource, new ExpantaNum(1000000));
        root.RefreshOnboardingForEditor();
        TutorialSnapshot after = root.TutorialSnapshotForEditor;
        Assert.That(after.ActionId, Is.EqualTo(before.ActionId));
        Assert.That(after.NavigationTargetId, Is.EqualTo(before.NavigationTargetId));
        Assert.That(after.CanAct, Is.True);
        Assert.That(guidance.text, Is.Not.EqualTo(previousText));
        Assert.That(guidance.text, Does.Contain(after.Blocker),
            "The same action becoming affordable must update Overview in the same refresh as onboarding.");
    }

    [UnityTest]
    public IEnumerator GateFiltersControlsCreatedWhileTheTargetIsUnchanged()
    {
        yield return Load();
        GetGate(out OnboardingInputGate gate, out RectTransform target, out RectTransform overlay,
            out Button next, out Button skip);
        gate.Configure(target, overlay, next, skip);
        GameObject inactiveHost = new GameObject("InactiveGateFixture", typeof(RectTransform));
        inactiveHost.transform.SetParent(root.transform, false);
        inactiveHost.SetActive(false);
        GameObject inactive = KingdomUIPrefabLibrary.Instantiate(KingdomUIPrefabLibrary.ActionButton, inactiveHost.transform);
        GameObject active = KingdomUIPrefabLibrary.Instantiate(KingdomUIPrefabLibrary.ActionButton, root.transform);
        gate.Configure(target, overlay, next, skip);
        AssertBlocked(active);
        AssertBlocked(inactive);
        inactiveHost.SetActive(true);
        AssertBlocked(inactive);
        Object.DestroyImmediate(active);
        Object.DestroyImmediate(inactiveHost);
    }

    private static void AssertBlocked(GameObject control)
    {
        OnboardingControlFilter filter = control.GetComponent<OnboardingControlFilter>();
        Assert.That(filter, Is.Not.Null);
        Assert.That(filter.IsRaycastLocationValid(Vector2.zero, null), Is.False,
            "New unrelated controls cannot inherit access from the existing highlighted target.");
    }

    private void GetGate(out OnboardingInputGate gate, out RectTransform target, out RectTransform overlay,
        out Button next, out Button skip)
    {
        gate = root.GetComponent<OnboardingInputGate>();
        target = root.transform.Find("SafeAreaRoot/TopStatusBar/TopInfoShell/InfoGrid") as RectTransform;
        overlay = root.transform.Find("SafeAreaRoot/OnboardingOverlay") as RectTransform;
        overlay.gameObject.SetActive(true);
        next = overlay.Find("Panel/Next").GetComponent<Button>();
        skip = overlay.Find("Panel/Skip").GetComponent<Button>();
    }

    private static void Measure(string label, int iterations, Action action)
    {
        for (int i = 0; i < 10; i++) action();
        double[] samples = new double[5];
        for (int batch = 0; batch < samples.Length; batch++)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++) action();
            samples[batch] = watch.Elapsed.TotalMilliseconds / iterations;
        }
        Array.Sort(samples);
        Debug.Log($"[HotPathPerf2] {label} medianMs={samples[2]:F6} iterations={iterations} batches={samples.Length}");
    }
}
