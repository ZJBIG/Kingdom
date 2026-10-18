using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class KingdomButtonMaterialPlayModeTests
{
    private string saveRoot;
    private KingdomUIRoot root;
    private TutorialManager initialTutorial;
    private MusicManager initialMusic;

    [SetUp]
    public void SetUp()
    {
        initialTutorial = Object.FindObjectOfType<TutorialManager>(true);
        initialMusic = Object.FindObjectOfType<MusicManager>(true);
        saveRoot = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
            "Temp", "KingdomButtonTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Scene empty = SceneManager.CreateScene("ButtonTestTeardown-" + Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(empty);
        Scene sample = SceneManager.GetSceneByName("SampleScene");
        if (sample.IsValid() && sample.isLoaded)
            yield return SceneManager.UnloadSceneAsync(sample);
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        if (tutorial != null && tutorial != initialTutorial) Object.DestroyImmediate(tutorial.gameObject);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        if (music != null && music != initialMusic) Object.DestroyImmediate(music.gameObject);
        SaveManager.ClearSaveRootOverrideForTests();
        if (Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
        root = null;
    }

    [UnityTest]
    public IEnumerator AllPagesKeepTheirMaterialsThroughRefreshAndSelection()
    {
        yield return LoadPages();
        Button previousNavigation = null;
        foreach (string page in new[] { "Overview", "Resources", "Buildings", "Production", "Research", "Era", "Workshop", "Music", "Sectors", "Story" })
        {
            root.SetPage(page);
            yield return null;
            if (previousNavigation != null && previousNavigation.gameObject.activeInHierarchy)
                Assert.That(previousNavigation.interactable, Is.True, "Leaving a page releases its navigation button.");
            if (page == "Research") yield return WaitForResearchReady();
            Canvas.ForceUpdateCanvases();
            var before = new Dictionary<Button, Sprite>();
            foreach (Button button in root.GetComponentsInChildren<Button>())
            {
                if (!button.gameObject.activeInHierarchy || button.name == "Backdrop") continue;
                if (button.name == "PinButton" && button.targetGraphic is Image pin)
                {
                    Assert.That(pin.sprite == Resources.Load<Sprite>("Art/UI/Pins/PinOutline") ||
                        pin.sprite == Resources.Load<Sprite>("Art/UI/Pins/PinFilled"), Is.True);
                    Assert.That(pin.raycastTarget, Is.False);
                    Assert.That(button.GetComponent<Image>().raycastTarget, Is.True);
                    Assert.That(button.GetComponent<Image>().sprite, Is.Null);
                    Assert.That(button.GetComponent<Image>().color.a, Is.LessThan(.01f));
                    Assert.That(button.GetComponentsInChildren<TMP_Text>(true), Is.Empty);
                    continue;
                }
                if (button.targetGraphic is TMP_Text link)
                {
                    Assert.That(button.name == "Label" && page == "Production" ||
                        button.name == "Name" && page == "Sectors", Is.True,
                        "Only production nodes and sector building names use text navigation targets.");
                    Assert.That(link.raycastTarget, Is.True);
                    Assert.That((link.fontStyle & FontStyles.Underline) != 0, Is.EqualTo(page == "Sectors"),
                        page + "/" + button.name + " authored navigation style");
                    continue;
                }
                Image skin = button.targetGraphic as Image;
                if (skin != null && skin.sprite == null && skin.color.a < .01f) continue;
                Assert.That(skin, Is.Not.Null, page + "/" + button.name);
                before.Add(button, skin.sprite);
            }
            Assert.That(before.Count, Is.GreaterThan(0), page);
            root.RefreshLiveCardValuesForEditor();
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (var pair in before)
                if (pair.Key != null)
                    Assert.That(((Image)pair.Key.targetGraphic).sprite, Is.SameAs(pair.Value),
                        page + "/" + pair.Key.name + " refresh replaced its material.");
            Button navigation = FindButton("Nav_" + page);
            Assert.That(navigation, Is.Not.Null, page);
            if (!navigation.gameObject.activeInHierarchy) continue;
            Assert.That(navigation.interactable, Is.False, "Current page stays selected.");
            previousNavigation = navigation;
        }
        root.SetPage("Buildings");
        yield return null;
        Button one = FindButton("Quantity_One");
        Button ten = FindButton("Quantity_Ten");
        Assert.That(one, Is.Not.Null);
        Assert.That(ten, Is.Not.Null);
        one.onClick.Invoke();
        Color unselected = ((Image)ten.targetGraphic).color;
        Sprite tenMaterial = ((Image)ten.targetGraphic).sprite;
        ten.onClick.Invoke();
        root.RefreshLiveCardValuesForEditor();
        Assert.That(((Image)ten.targetGraphic).sprite, Is.SameAs(tenMaterial));
        Assert.That(((Image)ten.targetGraphic).color, Is.Not.EqualTo(unselected));
        foreach (string name in new[] { "Quantity_One", "Quantity_Ten", "Quantity_Max", "Quantity_Custom" })
        {
            Button button = FindButton(name);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            Assert.That(label.alignment, Is.EqualTo(TextAlignmentOptions.Center), name);
            label.ForceMeshUpdate();
            Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + 1), name);
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(button.transform, label.rectTransform);
            Rect rect = ((RectTransform)button.transform).rect;
            Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(rect.xMin - 1), name);
            Assert.That(bounds.max.x, Is.LessThanOrEqualTo(rect.xMax + 1), name);
        }
    }

    [UnityTest]
    public IEnumerator MusicControls_PlayPauseAndResumeAddressableTrack()
    {
        yield return LoadPages();
        root.SetPage("Music");
        yield return null;
        MusicManager manager = Object.FindObjectOfType<MusicManager>();
        Assert.That(manager, Is.Not.Null);
        Transform musicPage = root.transform.Find("SafeAreaRoot/Content/PageHost/Music");
        Button mute = FindButton("SfxMute");
        Transform controls = mute.transform.parent;
        Bounds muteBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(controls, mute.transform);
        Bounds pauseBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(controls, FindButton("Pause").transform);
        Bounds volumeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(controls, controls.Find("SfxVolume"));
        Assert.That(muteBounds.min.x, Is.GreaterThan(pauseBounds.max.x), "Mute must clear the transport buttons.");
        Assert.That(muteBounds.max.x, Is.LessThan(volumeBounds.min.x), "Mute must clear the sound-effect slider.");
        bool previousMuted = UIButtonSoundManager.SfxMuted;
        bool hadMutePreference = PlayerPrefs.HasKey("Kingdom.Sfx.Mute");
        int previousMutePreference = PlayerPrefs.GetInt("Kingdom.Sfx.Mute");
        try
        {
            UIButtonSoundManager.SetMuted(false);
            mute.onClick.Invoke();
            root.RefreshLiveCardValuesForEditor();
            Assert.That(UIButtonSoundManager.SfxMuted, Is.True);
            TMP_Text caption = mute.GetComponentInChildren<TMP_Text>();
            Assert.That(caption.text, Is.EqualTo("恢复音效"));
            caption.ForceMeshUpdate();
            Assert.That(caption.isTextTruncated, Is.False);
            mute.onClick.Invoke();
            root.RefreshLiveCardValuesForEditor();
            Assert.That(UIButtonSoundManager.SfxMuted, Is.False);
            Assert.That(caption.text, Is.EqualTo("音效静音"));
        }
        finally
        {
            UIButtonSoundManager.SetMuted(previousMuted);
            if (hadMutePreference) PlayerPrefs.SetInt("Kingdom.Sfx.Mute", previousMutePreference);
            else PlayerPrefs.DeleteKey("Kingdom.Sfx.Mute");
            PlayerPrefs.Save();
        }

        Button trackPlay = null;
        for (int frame = 0; frame < 480 && trackPlay == null; frame++)
        {
            foreach (Button button in musicPage.GetComponentsInChildren<Button>())
                if (button.name == "PlayPause" && button.gameObject.activeInHierarchy)
                { trackPlay = button; break; }
            if (trackPlay == null) yield return null;
        }
        Assert.That(trackPlay, Is.Not.Null, "The real music catalog must provide a playable row.");
        Button pause = FindButton("Pause");
        Assert.That(pause, Is.Not.Null);
        Sprite trackSkin = ((Image)trackPlay.targetGraphic).sprite;
        Sprite pauseSkin = ((Image)pause.targetGraphic).sprite;
        Image trackIcon = trackPlay.transform.Find("Icon").GetComponent<Image>();
        Image pauseIcon = pause.transform.Find("Icon").GetComponent<Image>();
        manager.Stop();
        root.RefreshLiveCardValuesForEditor();
        trackPlay.onClick.Invoke();
        float playbackDeadline = Time.realtimeSinceStartup + 30f;
        while (!manager.IsPlaying && Time.realtimeSinceStartup < playbackDeadline)
            yield return null;
        Assert.That(manager.IsPlaying, Is.True, "A real catalog row must start its local audio asset.");
        Sprite pauseAffordance = Resources.Load<Sprite>("Texture/MusicIcons/pause");
        Assert.That(pauseAffordance, Is.Not.Null);
        float visualDeadline = Time.realtimeSinceStartup + 5f;
        while ((pauseIcon.sprite != pauseAffordance || trackIcon.sprite != pauseAffordance) &&
               Time.realtimeSinceStartup < visualDeadline)
            yield return null;
        Assert.That(pauseIcon.sprite, Is.SameAs(pauseAffordance), "Wait for the normal music visual refresh after loading.");
        Assert.That(trackIcon.sprite, Is.SameAs(pauseAffordance));
        Sprite playingIcon = pauseIcon.sprite;
        Assert.That(trackIcon.sprite, Is.SameAs(playingIcon), "Selected row and transport both show pause while playing.");
        pause.onClick.Invoke();
        Assert.That(manager.IsPaused, Is.True);
        Sprite pausedIcon = pauseIcon.sprite;
        Assert.That(pausedIcon, Is.Not.SameAs(playingIcon), "Pause must change the icon to the play affordance.");
        Assert.That(trackIcon.sprite, Is.SameAs(pausedIcon));
        pause.onClick.Invoke();
        Assert.That(manager.IsPlaying, Is.True);
        Assert.That(pauseIcon.sprite, Is.SameAs(playingIcon), "Resume must restore the pause affordance.");
        Assert.That(trackIcon.sprite, Is.SameAs(playingIcon));
        Assert.That(((Image)trackPlay.targetGraphic).sprite, Is.SameAs(trackSkin));
        Assert.That(((Image)pause.targetGraphic).sprite, Is.SameAs(pauseSkin));
        manager.Stop();
        root.RefreshLiveCardValuesForEditor();
        foreach (Button button in root.GetComponentsInChildren<Button>())
        {
            Image icon = button.transform.Find("Icon")?.GetComponent<Image>();
            if (icon == null || !button.gameObject.activeInHierarchy) continue;
            Assert.That(icon.sprite, Is.Not.Null);
            Assert.That(icon.sprite, Is.Not.SameAs(((Image)button.targetGraphic).sprite));
        }
        Canvas canvas = root.GetComponent<Canvas>();
        RenderMode oldMode = canvas.renderMode;
        Camera oldCamera = canvas.worldCamera;
        float oldDistance = canvas.planeDistance;
        GameObject cameraObject = new GameObject("MusicControlsEvidenceCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        RenderTexture target = new RenderTexture(1760, 800, 24);
        target.Create();
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100;
            Canvas.ForceUpdateCanvases();
            Capture(camera, target, "music-controls-1760x800");
        }
        finally
        {
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            canvas.planeDistance = oldDistance;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(cameraObject);
        }
    }

    [UnityTest]
    public IEnumerator RedesignedPagesRenderToActualCanvasEvidence()
    {
        yield return LoadPages();
        Canvas canvas = root.GetComponent<Canvas>();
        RenderMode originalMode = canvas.renderMode;
        Camera originalCamera = canvas.worldCamera;
        float originalDistance = canvas.planeDistance;
        GameObject cameraObject = new GameObject("ButtonEvidenceCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 1000;
        camera.transform.position = new Vector3(0, 0, -10);
        RenderTexture target = null;
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
                foreach (string page in new[] { "Overview", "Resources", "Buildings", "Research", "Era", "Workshop", "Music", "Sectors", "Story" })
                {
                    if (page == "Buildings" && size.x == 2640)
                    {
                        foreach (Resource resource in DataBase<Resource>.All)
                            ResourceManager.Instance.SetAmount(resource, new ExpantaNum(1000000));
                        Building house = DataBase<Building>.Find("WoodHouse");
                        Assert.That(house, Is.Not.Null);
                        Assert.That(BuildingManager.Instance.TryBuild(house, ExpantaNum.One, out BuildFailure failure),
                            Is.True, "Screenshot fixture must expose real enabled actions: " + failure);
                    }
                    if (page == "Workshop")
                    {
                        foreach (Research research in DataBase<Research>.All)
                            ResearchManager.Instance.GetState(research).SetStatusForEditor(ResearchStatus.Completed);
                        ProgressionModifierManager.Current.AddUnlockedSystemForEditor(ResearchSystem.IndustrialWorkshop);
                        root.RefreshNavigationVisibilityForEditor();
                    }
                    root.SetPage(page);
                    yield return null;
                    if (page == "Research") yield return WaitForResearchReady();
                    if (page == "Story")
                    {
                        StoryChapterCard card = root.GetComponentInChildren<StoryChapterCard>();
                        card.SetExpanded(true);
                    }
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    if (page == "Research") AssertVisibleResearchNodes();
                    if (page == "Buildings")
                    {
                        root.RefreshLiveCardValuesForEditor();
                        Building house = DataBase<Building>.Find("WoodHouse");
                        Button deconstruct = root.GetDeconstructButtonForEditor(house);
                        Assert.That(deconstruct.interactable, Is.True);
                        Button build = deconstruct.transform.parent.Find("BuildButton").GetComponent<Button>();
                        if (size.x == 2640) Assert.That(build.interactable, Is.True, "Initial buildable fixture must expose enabled actions.");
                    }
                    if (page == "Resources")
                        root.ShowResourceDetailsForEditor(DataBase<Resource>.All[0]);
                    if (page == "Buildings")
                    {
                        Building house = DataBase<Building>.Find("WoodHouse");
                        root.GetDeconstructButtonForEditor(house).transform.parent.GetComponent<Button>().onClick.Invoke();
                    }
                    Canvas.ForceUpdateCanvases();
                    AssertPageContentGeometry(page);
                    AssertClippingAndBoundDetails(page);
                    AssertBackgroundGeometry(root);
                    Capture(camera, target, page + "-" + size.x + "x" + size.y);
                }
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                target = null;
            }
        }
        finally
        {
            canvas.renderMode = originalMode;
            canvas.worldCamera = originalCamera;
            canvas.planeDistance = originalDistance;
            camera.targetTexture = null;
            if (target != null) { target.Release(); Object.Destroy(target); }
            Object.Destroy(cameraObject);
        }
    }

    private IEnumerator LoadPages()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;
        root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        GameManager.Instance.AdvanceTechLevelForEditor(TechLevel.Ultra);
        var chapters = new List<string>();
        foreach (StoryChapter chapter in StoryManager.Chapters) chapters.Add(chapter.Id);
        StoryManager.RestoreSaveData(new SaveManager.StorySaveData { CompletedChapterIds = chapters });
        root.StoryPageBuiltForEditor = false;
        root.RefreshNavigationVisibilityForEditor();
        yield return null;
    }

    private Button FindButton(string name)
    {
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
            if (button.name == name) return button;
        return null;
    }

    private IEnumerator WaitForResearchReady()
    {
        Transform viewport = null;
        Transform content = null;
        bool ready = false;
        int expected = DataBase<Research>.All.Count;
        Assert.That(expected, Is.GreaterThan(0));
        for (int frame = 0; frame < 480; frame++)
        {
            yield return null;
            viewport = root.transform.Find("SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport");
            content = viewport == null ? null : viewport.Find("ResearchGraphContent");
            int count = 0;
            if (content != null)
                foreach (Transform child in content)
                    if (child.name.StartsWith("ResearchNode_", StringComparison.Ordinal)) count++;
            UIResearchGraphGesture gesture = viewport == null ? null : viewport.GetComponent<UIResearchGraphGesture>();
            ready = count == expected && gesture != null && gesture.IsInitialized;
            if (ready) break;
        }
        Assert.That(ready, Is.True, "Wait for the production graph coroutine, not just the empty authored shell.");
        Assert.That(((RectTransform)viewport).rect.width, Is.GreaterThan(1));
        Assert.That(((RectTransform)viewport).rect.height, Is.GreaterThan(1));
        Assert.That(((RectTransform)content).rect.width, Is.GreaterThan(1));
        Assert.That(((RectTransform)content).rect.height, Is.GreaterThan(1));
    }

    private void AssertVisibleResearchNodes()
    {
        RectTransform viewport = (RectTransform)root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Research/DataRows/ResearchGraphViewport");
        int visible = 0;
        foreach (Transform child in viewport.Find("ResearchGraphContent"))
        {
            if (!child.name.StartsWith("ResearchNode_", StringComparison.Ordinal)) continue;
            Button button = child.GetComponent<Button>();
            Assert.That(button, Is.Not.Null);
            AssertResearchCaptionGeometry((RectTransform)child);
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, child);
            Rect area = viewport.rect;
            if (bounds.max.x > area.xMin && bounds.min.x < area.xMax &&
                bounds.max.y > area.yMin && bounds.min.y < area.yMax && button.gameObject.activeInHierarchy)
            {
                Assert.That(bounds.size.x, Is.GreaterThan(1));
                Assert.That(bounds.size.y, Is.GreaterThan(1));
                visible++;
            }
        }
        Assert.That(visible, Is.GreaterThan(0), "An empty graph image is not valid visual evidence.");
        Debug.Log("[ButtonMaterialResearchScreenshot] visibleNodes=" + visible + " viewport=" + viewport.rect.size);
    }

    private static void AssertResearchCaptionGeometry(RectTransform node)
    {
        Bounds titleBounds = default;
        foreach (string name in new[] { "Label", "Cost", "Progress", "State" })
        {
            TMP_Text label = node.Find(name)?.GetComponent<TMP_Text>();
            label.ForceMeshUpdate();
            bool hasVisibleCharacter = false;
            for (int i = 0; i < label.textInfo.characterCount; i++)
                hasVisibleCharacter |= label.textInfo.characterInfo[i].isVisible;
            Assert.That(hasVisibleCharacter, Is.True, node.name + "/" + name + " must actually draw its caption: text=" + label.text + " font=" + label.fontSize + " rect=" + label.rectTransform.rect.size + " preferredHeight=" + label.preferredHeight + " rendered=" + label.renderedHeight + " chars=" + label.textInfo.characterCount);
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(node, label.rectTransform);
            Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(node.rect.xMin - 1), node.name + "/" + name);
            Assert.That(bounds.max.x, Is.LessThanOrEqualTo(node.rect.xMax + 1), node.name + "/" + name);
            Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(node.rect.yMin - 1), node.name + "/" + name);
            Assert.That(bounds.max.y, Is.LessThanOrEqualTo(node.rect.yMax + 1), node.name + "/" + name);
            Assert.That(label.renderedHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1),
                node.name + "/" + name + " text height must fit its caption slot.");
            if (name == "Label") titleBounds = bounds;
            else Assert.That(bounds.max.y, Is.LessThanOrEqualTo(titleBounds.min.y + 1),
                node.name + "/" + name + " must stay below the title caption.");
        }
    }

    private void AssertPageContentGeometry(string page)
    {
        const string pages = "SafeAreaRoot/Content/PageHost/";
        if (page == "Overview")
        {
            Transform overview = root.transform.Find(pages + "Overview");
            RectTransform primary = overview.Find("PrimaryCard") as RectTransform;
            RectTransform secondary = overview.Find("SecondaryCard") as RectTransform;
            Bounds a = RectTransformUtility.CalculateRelativeRectTransformBounds(overview, primary);
            if (secondary.gameObject.activeInHierarchy)
            {
                Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(overview, secondary);
                Assert.That(b.max.y, Is.LessThanOrEqualTo(a.min.y - 1), "Overview sections must not overlap as guidance text grows.");
            }
            TMP_Text text = primary.Find("Text").GetComponent<TMP_Text>();
            text.ForceMeshUpdate();
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1));
            Transform toolbar = root.transform.Find("SafeAreaRoot/Content/PageTool/OverviewNavigationToolbar");
            foreach (TMP_Text label in toolbar.GetComponentsInChildren<TMP_Text>())
                if (label.transform.parent.GetComponent<Button>() != null)
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + 1), label.text);
                }
        }
        if (page == "Music")
        {
            Transform controls = root.transform.Find(pages + "Music/DataRows/MusicSurface/Controls");
            Bounds icon = RectTransformUtility.CalculateRelativeRectTransformBounds(controls, controls.Find("CurrentCategoryIcon"));
            Bounds label = RectTransformUtility.CalculateRelativeRectTransformBounds(controls, controls.Find("CurrentPlayName"));
            Assert.That(icon.max.x + 12, Is.LessThanOrEqualTo(label.min.x), "The category icon must never cover the song title.");
            TMP_Text name = controls.Find("CurrentPlayName").GetComponent<TMP_Text>();
            name.ForceMeshUpdate();
            Assert.That(name.renderedHeight, Is.LessThanOrEqualTo(name.rectTransform.rect.height + 1));
        }
        if (page == "Buildings")
        {
            Building house = DataBase<Building>.Find("WoodHouse");
            Transform row = root.GetDeconstructButtonForEditor(house).transform.parent;
            float right = float.NegativeInfinity;
            foreach (string column in new[] { "Label", "TechLevel", "Amount", "Effect", "BuildButton", "DeconstructButton" })
            {
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(row, row.Find(column));
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(right), column + " must not overlap the preceding building column.");
                right = bounds.max.x;
            }
            TMP_Text effect = row.Find("Effect").GetComponent<TMP_Text>();
            effect.ForceMeshUpdate();
            Assert.That(effect.renderedHeight, Is.LessThanOrEqualTo(effect.rectTransform.rect.height + 1));
        }
        if (page == "Workshop")
        {
            int purchases = 0;
            foreach (Button button in root.transform.Find(pages + "Workshop").GetComponentsInChildren<Button>())
                if (button.name == "PurchaseButton")
                {
                    purchases++;
                    RectTransform row = button.transform.parent as RectTransform;
                    TMP_Text title = row.Find("Label").GetComponent<TMP_Text>();
                    TMP_Text state = row.Find("TechLevel").GetComponent<TMP_Text>();
                    Bounds a = RectTransformUtility.CalculateRelativeRectTransformBounds(row, title.rectTransform);
                    Bounds b = RectTransformUtility.CalculateRelativeRectTransformBounds(row, state.rectTransform);
                    Assert.That(a.max.x, Is.LessThan(b.min.x), "Workshop names and availability must use separate columns.");
                    title.ForceMeshUpdate();
                    Assert.That(title.renderedHeight, Is.LessThanOrEqualTo(title.rectTransform.rect.height + 1), title.text);
                }
            Assert.That(purchases, Is.GreaterThan(0), "Visual review must include real workshop upgrades, not an empty page.");
        }
    }

    private void AssertClippingAndBoundDetails(string page)
    {
        const string pages = "SafeAreaRoot/Content/PageHost/";
        Image graph = root.transform.Find(pages + "Research/DataRows/ResearchGraphViewport").GetComponent<Image>();
        Assert.That(graph.GetComponent<Mask>().showMaskGraphic, Is.False, "Refresh must not restore the black graph panel.");
        if (page == "Research")
        {
            Canvas.ForceUpdateCanvases();
            RectTransform viewport = graph.rectTransform;
            int clippedLabels = 0;
            foreach (TMP_Text label in graph.GetComponentsInChildren<TMP_Text>())
            {
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, label.rectTransform);
                bool outside = bounds.max.x < viewport.rect.xMin || bounds.min.x > viewport.rect.xMax ||
                    bounds.max.y < viewport.rect.yMin || bounds.min.y > viewport.rect.yMax;
                if (!outside) continue;
                Assert.That(label.canvasRenderer.cull, Is.True, label.name + " outside the viewport must actually be culled.");
                clippedLabels++;
            }
            Assert.That(clippedLabels, Is.GreaterThan(0), "The real graph must exercise clipping beyond the viewport.");
        }
        if (page == "Era")
            foreach (Image image in root.transform.Find(pages + "Era").GetComponentsInChildren<Image>())
                if (image.GetComponent<Button>() == null &&
                    (image.name == "Divider" || image.transform.parent.name == "CardFrame"))
                {
                    Assert.That(image.color.a, Is.GreaterThan(0f), image.name);
                    Assert.That(image.raycastTarget, Is.False, "Era decoration must not intercept interaction.");
                }
        if (page == "Resources" || page == "Buildings")
        {
            TMP_Text body = root.transform.Find("SafeAreaRoot/DetailPanel/DetailUI/DetailScrollViewport/DetailScrollContent/Body").GetComponent<TMP_Text>();
            Assert.That(body.text, Is.Not.Empty, "Evidence must include real bound detail content.");
        }
    }

    private static void AssertBackgroundGeometry(KingdomUIRoot root)
    {
        RectTransform canvas = (RectTransform)root.transform;
        Image background = root.transform.Find("EnvironmentBackground").GetComponent<Image>();
        Assert.That(background.gameObject.activeInHierarchy, Is.True, "The authored environment must survive legacy UI isolation.");
        RectTransform rect = background.rectTransform;
        Assert.That(background.raycastTarget, Is.False);
        Assert.That(rect.rect.width / rect.rect.height,
            Is.EqualTo(background.sprite.rect.width / background.sprite.rect.height).Within(.002f));
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, rect);
        Assert.That(bounds.min.x, Is.LessThanOrEqualTo(canvas.rect.xMin + 1));
        Assert.That(bounds.max.x, Is.GreaterThanOrEqualTo(canvas.rect.xMax - 1));
        Assert.That(bounds.min.y, Is.LessThanOrEqualTo(canvas.rect.yMin + 1));
        Assert.That(bounds.max.y, Is.GreaterThanOrEqualTo(canvas.rect.yMax - 1));
        foreach (string name in new[] { "Content", "DetailPanel", "LeftNavigation", "TopStatusBar", "NotificationBar" })
        {
            Image image = root.transform.Find("SafeAreaRoot/" + name + "/Surface").GetComponent<Image>();
            Assert.That(image.sprite, Is.Not.Null, name);
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced), name);
            Assert.That(image.raycastTarget, Is.False, name);
        }
    }

    private static void Capture(Camera camera, RenderTexture target, string page)
    {
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "data", "scroll-frames-20261013", "pages"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, page.ToLowerInvariant() + ".png");
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
            Debug.Log("[ButtonMaterialScreenshot] " + path);
        }
        finally
        {
            RenderTexture.active = previous;
            Object.Destroy(pixels);
        }
    }
}
