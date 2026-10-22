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

public sealed class PlannerPagesPlayModeTests
{
    private const string Pages = "SafeAreaRoot/Content/PageHost/";
    private string saveRoot;
    private KingdomUIRoot root;
    private TutorialManager initialTutorial;
    private MusicManager initialMusic;

    [SetUp]
    public void SetUp()
    {
        initialTutorial = Object.FindObjectOfType<TutorialManager>(true);
        initialMusic = Object.FindObjectOfType<MusicManager>(true);
        saveRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp",
            "PlannerPagesTests-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Scene empty = SceneManager.CreateScene("PlannerPagesTeardown-" + Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(empty);
        Scene sample = SceneManager.GetSceneByName("SampleScene");
        if (sample.IsValid() && sample.isLoaded) yield return SceneManager.UnloadSceneAsync(sample);
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        if (tutorial != null && tutorial != initialTutorial) Object.DestroyImmediate(tutorial.gameObject);
        if (music != null && music != initialMusic) Object.DestroyImmediate(music.gameObject);
        SaveManager.ClearSaveRootOverrideForTests();
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
        root = null;
    }

    [UnityTest]
    public IEnumerator RealOverviewEraAndAllEraCardDesignsRenderWithoutClipping()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;
        root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        SimulationManager.Instance.SetRunning(false);
        Canvas canvas = root.GetComponent<Canvas>();
        RenderMode originalMode = canvas.renderMode;
        Camera originalCamera = canvas.worldCamera;
        float originalDistance = canvas.planeDistance;
        GameObject cameraObject = new GameObject("PlannerEvidenceCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .045f, .04f);
        camera.transform.position = new Vector3(0, 0, -10);
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 1000;
        RenderTexture target = null;
        GameObject gallery = null;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100;
            foreach (Vector2Int size in new[] { new Vector2Int(2640, 1200), new Vector2Int(1760, 800) })
            {
                target = new RenderTexture(size.x, size.y, 24);
                target.Create();
                camera.targetTexture = target;
                foreach (string page in new[] { "Overview", "Era" })
                {
                    root.SetPage(page);
                    yield return null;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    ScrollRect scroll = root.transform.Find("SafeAreaRoot/Content/PageHost").GetComponent<ScrollRect>();
                    scroll.StopMovement();
                    scroll.verticalNormalizedPosition = 1;
                    if (page == "Overview")
                    {
                        Transform overview = root.transform.Find(Pages + page);
                        RectTransform primary = (RectTransform)overview.Find("PrimaryCard");
                        TMP_Text text = primary.Find("Text").GetComponent<TMP_Text>();
                        Assert.That(text.text, Does.Contain("当前目标"));
                        Assert.That(text.text, Does.Contain("当前阻碍"));
                        Assert.That(text.text, Does.Contain("推荐行动"));
                        AssertInside(primary, text.rectTransform);
                        text.ForceMeshUpdate();
                        Assert.That(text.isTextTruncated, Is.False);
                        Assert.That(root.transform.Find("SafeAreaRoot/Content/PageTool/OverviewNavigationToolbar")
                            .gameObject.activeInHierarchy, Is.False);
                        Transform secondary = overview.Find("SecondaryCard");
                        if (secondary.gameObject.activeInHierarchy)
                        {
                            TMP_Text feedback = secondary.Find("Text").GetComponent<TMP_Text>();
                            Assert.That(feedback.text, Does.Contain("王国运行简报"));
                            Assert.That(feedback.text.Length, Is.GreaterThan("<b>王国运行简报</b>\n".Length));
                        }
                    }
                    else
                    {
                        Transform archive = root.transform.Find(Pages + "Era/DataRows/EraCurrentArchive/Content");
                        Assert.That(archive.childCount, Is.EqualTo(1), "Civilization identity is one combined card.");
                        Assert.That(archive.Find("EraIdentity"), Is.Not.Null);
                    }
                    Capture(camera, target, page.ToLowerInvariant());
                }
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                target = null;
            }
            canvas.enabled = false;
            target = new RenderTexture(2640, 1200, 24);
            target.Create();
            camera.targetTexture = target;
            foreach (string kind in new[] { KingdomUIPrefabLibrary.ResourceCard, KingdomUIPrefabLibrary.BuildingCard })
            {
                gallery = BuildGallery(camera, kind);
                yield return null;
                Canvas.ForceUpdateCanvases();
                RectTransform galleryBounds = (RectTransform)gallery.transform;
                foreach (TMP_Text text in gallery.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    AssertInside(galleryBounds, text.rectTransform);
                    Assert.That(text.rectTransform.rect.height, Is.GreaterThan(0f), text.name);
                    Assert.That(text.textInfo.characterCount, Is.GreaterThan(0), text.name);
                    Assert.That(text.textBounds.size.x, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 2f), text.name);
                    Assert.That(text.textBounds.size.y, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 2f), text.name);
                }
                foreach (Transform row in gallery.transform)
                {
                    TMP_Text label = row.Find("Label").GetComponent<TMP_Text>();
                    TMP_Text metadata = row.Find("Metadata").GetComponent<TMP_Text>();
                    AssertInside((RectTransform)row, label.rectTransform);
                    if (!metadata.gameObject.activeSelf) continue;
                    AssertInside((RectTransform)row, metadata.rectTransform);
                    var labelCorners = new Vector3[4];
                    var metadataCorners = new Vector3[4];
                    label.rectTransform.GetWorldCorners(labelCorners);
                    metadata.rectTransform.GetWorldCorners(metadataCorners);
                    Assert.That(labelCorners[0].y, Is.GreaterThanOrEqualTo(metadataCorners[1].y - 1f),
                        "Wrapped heading must remain above its metadata.");
                }
                foreach (Button button in gallery.GetComponentsInChildren<Button>())
                    AssertInside(galleryBounds, (RectTransform)button.transform);
                Capture(camera, target, kind == KingdomUIPrefabLibrary.ResourceCard ? "resource-era-gallery" : "building-era-gallery");
                Object.Destroy(gallery);
                gallery = null;
                yield return null;
            }
        }
        finally
        {
            canvas.enabled = true;
            canvas.renderMode = originalMode;
            canvas.worldCamera = originalCamera;
            canvas.planeDistance = originalDistance;
            camera.targetTexture = null;
            if (target != null) { target.Release(); Object.Destroy(target); }
            if (gallery != null) Object.Destroy(gallery);
            Object.Destroy(cameraObject);
        }
    }

    private static GameObject BuildGallery(Camera camera, string kind)
    {
        GameObject gallery = new GameObject("EraDesignGallery", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = gallery.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 100;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = gallery.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2640, 1200);
        scaler.matchWidthOrHeight = 0;
        int index = 0;
        foreach (TechLevel era in Enum.GetValues(typeof(TechLevel)))
        {
            GameObject row = KingdomUIPrefabLibrary.Instantiate(KingdomUIRoot.GetEraCollectionPrefabName(kind, era), gallery.transform);
            RectTransform rect = (RectTransform)row.transform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -80 - index * 156);
            rect.sizeDelta = new Vector2(-160, row.GetComponent<LayoutElement>().preferredHeight);
            KingdomUIRoot.ApplyEraRowStyle(row, era, index++);
            if (kind == KingdomUIPrefabLibrary.ResourceCard)
            {
                ResourceDef definition = LongestResource(era);
                Assert.That(definition, Is.Not.Null);
                SetText(row, "Label", definition.Label);
                Image icon = row.transform.Find("Icon").GetComponent<Image>();
                icon.sprite = definition.Sprite; icon.color = definition.Color;
                ResourceManager.Instance.States.TryGetValue(definition, out ResourceState state);
                SetText(row, "Amount", state == null ? "0" : state.Amount.ToGameString());
                SetText(row, "ChangeRate", state == null ? "0/s" : (state.ProductionRate - state.ConsumptionRate).ToGameString() + "/s");
            }
            else
            {
                BuildingDef definition = LongestBuilding(era);
                Assert.That(definition, Is.Not.Null);
                SetText(row, "Label", definition.Label);
                SetText(row, "TechLevel", definition.TechLevel.GetDescription());
                BuildingManager.Instance.States.TryGetValue(definition, out BuildingState state);
                SetText(row, "Amount", state == null ? "0" : state.Amount.ToGameString());
                SetText(row, "Effect", "×" + (state == null ? "1" : state.Efficiency.ToGameString()));
                row.transform.Find("BuildButton").GetComponentInChildren<TMP_Text>().text = "建造 ×1";
                row.transform.Find("DeconstructButton").GetComponentInChildren<TMP_Text>().text = "拆除 ×1";
            }
            Debug.Log("[EraGallery] kind=" + kind + " style=" + era + " label=" + row.transform.Find("Label").GetComponent<TMP_Text>().text);
        }
        return gallery;
    }

    private static ResourceDef LongestResource(TechLevel era)
    {
        ResourceDef result = null;
        foreach (ResourceDef item in DataBase<ResourceDef>.All)
            if (item.TechLevel <= era && (result == null || item.TechLevel > result.TechLevel ||
                item.TechLevel == result.TechLevel && item.Label.Length > result.Label.Length)) result = item;
        return result;
    }

    private static BuildingDef LongestBuilding(TechLevel era)
    {
        BuildingDef result = null;
        foreach (BuildingDef item in DataBase<BuildingDef>.All)
            if (!(item is SectorBuildingDef) && item.TechLevel <= era && (result == null || item.TechLevel > result.TechLevel ||
                item.TechLevel == result.TechLevel && item.Label.Length > result.Label.Length)) result = item;
        return result;
    }

    private static void SetText(GameObject row, string name, string value) =>
        row.transform.Find(name).GetComponent<TMP_Text>().text = value;

    private static void AssertInside(RectTransform outer, RectTransform child)
    {
        var corners = new Vector3[4];
        child.GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector3 point = outer.InverseTransformPoint(corner);
            Assert.That(point.x, Is.InRange(outer.rect.xMin - 1f, outer.rect.xMax + 1f), child.name);
            Assert.That(point.y, Is.InRange(outer.rect.yMin - 1f, outer.rect.yMax + 1f), child.name);
        }
    }

    private static void Capture(Camera camera, RenderTexture target, string name)
    {
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "data", "planner-pages-20261013"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name + "-" + target.width + "x" + target.height + ".png");
        RenderTexture previous = RenderTexture.active;
        Texture2D pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(0));
            Debug.Log("[PlannerScreenshot] " + path);
        }
        finally { RenderTexture.active = previous; Object.Destroy(pixels); }
    }
}
