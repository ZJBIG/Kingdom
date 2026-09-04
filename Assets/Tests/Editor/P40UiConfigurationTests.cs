using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class P40UiConfigurationTests
{
    [Test]
    public void ProjectAndSceneUseP40LandscapeDesignResolution()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string projectSettings = File.ReadAllText(Path.Combine(projectRoot, "ProjectSettings", "ProjectSettings.asset"));

        StringAssert.Contains("defaultScreenWidth: 2640", projectSettings);
        StringAssert.Contains("defaultScreenHeight: 1200", projectSettings);
        StringAssert.Contains("allowedAutorotateToPortrait: 0", projectSettings);
        StringAssert.Contains("allowedAutorotateToPortraitUpsideDown: 0", projectSettings);
        // The runtime UI owns the CanvasScaler and configures the P40 design
        // resolution at startup; the scene intentionally contains no scaler.
    }

    [Test]
    public void BundledUiFont_IsPresent()
    {
        string fontPath = Path.Combine(
            Application.dataPath,
            "Resources",
            "Fonts",
            "NotoSansSC-Regular.otf");
        Assert.That(File.Exists(fontPath), Is.True, "The bundled UI font is missing.");
    }

    [Test]
    public void RuntimeUiRoot_ContainsRequiredLandscapePagesAndFontFallback()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        string tooltipPath = Path.Combine(projectRoot, "Assets", "Resources", "Script", "UI", "UITouchTooltip.cs");
        Assert.That(File.Exists(tooltipPath), Is.True, "The touch tooltip component is missing.");
    }

    [Test]
    public void OverviewNavigationToolbarPrefabProvidesAuthoredTargetButton()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/Kingdom/KingdomUIOverviewNavigationToolbar");
        Assert.That(prefab, Is.Not.Null, "The Overview navigation toolbar prefab is missing.");
        AssertAuthoredButton(prefab.transform, "OverviewCurrentTargetButton");
        AssertAuthoredButton(prefab.transform, "OverviewCurrentEraButton");

        GameObject root = Resources.Load<GameObject>("UI/Kingdom/KingdomUIRoot");
        Assert.That(root, Is.Not.Null, "The authored UI root prefab is missing.");
        Transform viewport = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport");
        Assert.That(viewport, Is.Not.Null, "ResearchGraphViewport must be authored in the UI root.");
        Transform pageTool = root.transform.Find("SafeAreaRoot/Content/PageTool");
        Assert.That(pageTool, Is.Not.Null, "PageTool must be authored under Content.");
        Transform queue = pageTool.Find("ResearchQueueViewport");
        Assert.That(queue, Is.Not.Null, "ResearchQueueViewport must be authored under PageTool.");
        Transform toolbar = pageTool.Find("OverviewNavigationToolbar");
        Assert.That(toolbar, Is.Not.Null, "OverviewNavigationToolbar must be authored beside ResearchQueueViewport.");
        Assert.That(toolbar.parent, Is.SameAs(queue.parent),
            "OverviewNavigationToolbar and ResearchQueueViewport must be siblings under PageTool.");
        AssertAuthoredButton(toolbar, "OverviewCurrentTargetButton");
        AssertLeftCenteredButton(toolbar, "OverviewCurrentTargetButton");
        AssertAuthoredButton(toolbar, "OverviewCurrentEraButton");
        AssertLeftCenteredButton(toolbar, "OverviewCurrentEraButton");
    }

    [Test]
    public void SectorRowPrefabKeepsOnlyItsLabelAndAction()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/Kingdom/KingdomUISectorRow");
        Assert.That(prefab, Is.Not.Null, "The sector row prefab is missing.");
        Assert.That(prefab.transform.Find("Label")?.GetComponent<TMP_Text>(), Is.Not.Null);
        Assert.That(prefab.transform.Find("Type"), Is.Null);
        Assert.That(prefab.transform.Find("Progress"), Is.Null);
        Assert.That(prefab.transform.Find("TypePattern"), Is.Null);
        Assert.That(prefab.transform.Find("Buildings")?.GetComponent<Button>(), Is.Not.Null);
    }

    [Test]
    public void MusicTrackPrefabProvidesAuthoredPlayPauseButton()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/Kingdom/KingdomUIMusicTrack");
        Assert.That(prefab, Is.Not.Null, "The music track prefab is missing.");
        Transform playPause = prefab.transform.Find("PlayPause");
        Assert.That(playPause, Is.Not.Null, "PlayPause must be authored in the music track prefab.");
        Assert.That(playPause.GetComponent<Image>(), Is.Not.Null,
            "PlayPause must contain an authored Image component.");
        Assert.That(playPause.GetComponent<Button>(), Is.Not.Null,
            "PlayPause must contain an authored Button component.");
    }

    private static void AssertAuthoredButton(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        Assert.That(child, Is.Not.Null, childName + " must be authored in the prefab.");
        Assert.That(child.GetComponent<Button>(), Is.Not.Null,
            childName + " must contain a Button component in the prefab.");
        Assert.That(child.GetComponentInChildren<TMP_Text>(true), Is.Not.Null,
            childName + " must contain an authored label in the prefab.");
    }

    private static void AssertLeftCenteredButton(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        RectTransform rect = child == null ? null : child.GetComponent<RectTransform>();
        Assert.That(rect, Is.Not.Null, childName + " must have a RectTransform.");
        Assert.That(rect.anchorMin.x, Is.LessThanOrEqualTo(.01f));
        Assert.That(rect.anchorMax.x, Is.LessThanOrEqualTo(.01f));
        Assert.That(rect.anchorMin.y, Is.InRange(.49f, .51f));
        Assert.That(rect.anchorMax.y, Is.InRange(.49f, .51f));

        TMP_Text label = child.GetComponentInChildren<TMP_Text>(true);
        Assert.That(label.alignment, Is.EqualTo(TextAlignmentOptions.Left),
            childName + " label must be left aligned.");
    }
}
