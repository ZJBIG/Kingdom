using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class StoryBackgroundInteractionPlayModeTests
{
    private const string SurfacePath = "SafeAreaRoot/Content/PageHost/Story/StoryOverviewPage";
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
            "Temp", "KingdomStoryBackgroundTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Scene empty = SceneManager.CreateScene("StoryBackgroundTeardown-" + Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(empty);
        Scene sample = SceneManager.GetSceneByName("SampleScene");
        if (sample.IsValid() && sample.isLoaded)
            yield return SceneManager.UnloadSceneAsync(sample);
        TutorialManager tutorial = Object.FindObjectOfType<TutorialManager>(true);
        MusicManager music = Object.FindObjectOfType<MusicManager>(true);
        if (tutorial != null && tutorial != initialTutorial) Object.DestroyImmediate(tutorial.gameObject);
        if (music != null && music != initialMusic) Object.DestroyImmediate(music.gameObject);
        SaveManager.ClearSaveRootOverrideForTests();
        if (Directory.Exists(saveRoot))
            Directory.Delete(saveRoot, true);
        root = null;
    }

    [UnityTest]
    public IEnumerator PreviewZoomPanAndAllClosePathsPreserveStoryPosition()
    {
        yield return LoadStory();
        StoryChapterCard card = Card(0);
        card.SetExpanded(true);
        ResizeCard(card, 600);
        StoryBodyScrollRect body = card.transform.Find("BodyViewport").GetComponent<StoryBodyScrollRect>();
        ScrollRect outer = OuterScroll();
        outer.StopMovement();
        body.StopMovement();
        outer.verticalNormalizedPosition = .42f;
        body.verticalNormalizedPosition = .5f;
        Vector2 outerBefore = outer.content.anchoredPosition;
        Vector2 bodyBefore = body.content.anchoredPosition;
        Button open = card.transform.Find("Header/Actions/StoryIllustrationPreview").GetComponent<Button>();
        StoryIllustrationPreview preview = root.transform.Find("SafeAreaRoot/StoryIllustrationPreview")
            .GetComponent<StoryIllustrationPreview>();
        open.onClick.Invoke();
        yield return null;
        Assert.That(preview.IsOpen, Is.True);
        Assert.That(preview.Illustration, Is.SameAs(StoryManager.Chapters[0].Illustration));
        RectTransform viewport = (RectTransform)preview.transform.Find("Viewport");
        RectTransform image = (RectTransform)viewport.Find("Image");
        Vector2 center = RectTransformUtility.WorldToScreenPoint(null, viewport.position);
        var pointer = Pointer(center);
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits.Count, Is.GreaterThan(0));
        Assert.That(hits[0].gameObject.transform.IsChildOf(preview.transform), Is.True,
            "The visible modal must own the first UI raycast.");
        Assert.That(image.rect.width / image.rect.height,
            Is.EqualTo(preview.Illustration.rect.width / preview.Illustration.rect.height).Within(.01f));
        Assert.That(image.rect.width, Is.LessThanOrEqualTo(viewport.rect.width + 1f));
        Assert.That(image.rect.height, Is.LessThanOrEqualTo(viewport.rect.height + 1f));
        float fittedZoom = preview.Zoom;
        preview.ZoomAt(100f, center);
        Assert.That(preview.Zoom, Is.GreaterThan(fittedZoom), "Zooming in enlarges the fitted illustration.");
        float enlargedZoom = preview.Zoom;
        preview.Pan(new Vector2(100000, -100000));
        Vector2 limit = Vector2.Max(Vector2.zero, (image.rect.size - viewport.rect.size) * .5f);
        Assert.That(Mathf.Abs(preview.ViewPosition.x), Is.LessThanOrEqualTo(limit.x + .1f));
        Assert.That(Mathf.Abs(preview.ViewPosition.y), Is.LessThanOrEqualTo(limit.y + .1f));
        preview.ZoomAt(.001f, center);
        Assert.That(preview.Zoom, Is.GreaterThan(0f).And.LessThan(enlargedZoom).And.LessThanOrEqualTo(fittedZoom));
        Assert.That(preview.ViewPosition.magnitude, Is.LessThan(.1f));
        preview.OnScroll(new PointerEventData(EventSystem.current) { position = center, scrollDelta = Vector2.up });
        Assert.That(preview.Zoom, Is.GreaterThan(fittedZoom));
        preview.ZoomAt(100f, center);
        preview.OnBeginDrag(pointer);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, pointer.position, null, out Vector2 dragStart);
        MovePointer(pointer, new Vector2(40, 20));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, pointer.position, null, out Vector2 dragEnd);
        preview.OnDrag(pointer);
        Assert.That(Vector2.Distance(preview.ViewPosition, (dragEnd - dragStart) * 1.5f), Is.LessThan(.1f),
            "Illustration dragging uses the same faster movement as page and graph dragging.");
        preview.OnEndDrag(pointer);
        preview.transform.Find("Header/Reset").GetComponent<Button>().onClick.Invoke();
        Assert.That(preview.Zoom, Is.GreaterThan(0f).And.LessThanOrEqualTo(fittedZoom), "Reset restores the fitted view.");
        Assert.That(preview.ViewPosition.magnitude, Is.LessThan(.1f));
        preview.transform.Find("Header/Close").GetComponent<Button>().onClick.Invoke();
        AssertClosed(preview);
        open.onClick.Invoke();
        preview.transform.Find("Backdrop").GetComponent<Button>().onClick.Invoke();
        AssertClosed(preview);
        open.onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        Vector2 blankLocal = new Vector2(viewport.rect.xMin + .01f, viewport.rect.yMin + .01f);
        Vector2 blankScreen = RectTransformUtility.WorldToScreenPoint(null, viewport.TransformPoint(blankLocal));
        Assert.That(RectTransformUtility.RectangleContainsScreenPoint(image, blankScreen), Is.False,
            "This exercises blank space inside the preview viewport, outside the fitted image.");
        var blankPointer = Pointer(blankScreen);
        blankPointer.eligibleForClick = true;
        var imagePointer = Pointer(RectTransformUtility.WorldToScreenPoint(null, image.position));
        imagePointer.eligibleForClick = true;
        preview.OnPointerDown(imagePointer);
        preview.OnPointerClick(imagePointer);
        Assert.That(preview.IsOpen, Is.True, "Clicking the image keeps its preview open.");
        preview.OnPointerDown(blankPointer);
        preview.OnBeginDrag(blankPointer);
        preview.OnEndDrag(blankPointer);
        preview.OnPointerClick(blankPointer);
        Assert.That(preview.IsOpen, Is.True, "Releasing a drag in blank space must not close the preview.");
        preview.OnPointerDown(blankPointer);
        preview.OnPointerClick(blankPointer);
        AssertClosed(preview);
        Assert.That(Vector2.Distance(outerBefore, outer.content.anchoredPosition), Is.LessThan(1f));
        Assert.That(Vector2.Distance(bodyBefore, body.content.anchoredPosition), Is.LessThan(1f));
        open.onClick.Invoke();
        root.SetPage("Overview");
        AssertClosed(preview);
        root.SetPage("Story");
        yield return null;
        Assert.That(Vector2.Distance(outerBefore, outer.content.anchoredPosition), Is.LessThan(2f));
        open.onClick.Invoke();
        root.gameObject.SetActive(false);
        Assert.That(preview.Illustration, Is.Null);
        root.gameObject.SetActive(true);
        AssertClosed(preview);
    }

    [UnityTest]
    public IEnumerator NestedBodyWheelAndDragSelectOneOwnerAndHandOffAtBoundary()
    {
        yield return LoadStory();
        StoryChapterCard card = Card(0);
        card.SetExpanded(true);
        ResizeCard(card, 600);
        StoryBodyScrollRect inner = card.transform.Find("BodyViewport").GetComponent<StoryBodyScrollRect>();
        ScrollRect outer = OuterScroll();
        Assert.That(inner.IsOverflowing, Is.True);
        outer.StopMovement();
        inner.StopMovement();
        outer.verticalNormalizedPosition = .5f;
        inner.verticalNormalizedPosition = .5f;
        Vector2 outerBefore = outer.content.anchoredPosition;
        Vector2 innerBefore = inner.content.anchoredPosition;
        var pointer = Pointer(RectTransformUtility.WorldToScreenPoint(null, inner.viewport.position));
        pointer.scrollDelta = Vector2.down;
        inner.OnScroll(pointer);
        Assert.That(Vector2.Distance(innerBefore, inner.content.anchoredPosition), Is.GreaterThan(.1f));
        Assert.That(Vector2.Distance(outerBefore, outer.content.anchoredPosition), Is.LessThan(.1f));
        inner.verticalNormalizedPosition = 0;
        innerBefore = inner.content.anchoredPosition;
        inner.OnScroll(pointer);
        Assert.That(Vector2.Distance(outerBefore, outer.content.anchoredPosition), Is.GreaterThan(.1f));
        Assert.That(Vector2.Distance(innerBefore, inner.content.anchoredPosition), Is.LessThan(.1f));
        outer.StopMovement();
        inner.StopMovement();
        outer.verticalNormalizedPosition = .5f;
        inner.verticalNormalizedPosition = .5f;
        outerBefore = outer.content.anchoredPosition;
        inner.OnInitializePotentialDrag(pointer);
        inner.OnBeginDrag(pointer);
        innerBefore = inner.content.anchoredPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(inner.viewport, pointer.position, null, out Vector2 dragStart);
        MovePointer(pointer, Vector2.up * 20);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(inner.viewport, pointer.position, null, out Vector2 dragEnd);
        inner.OnDrag(pointer);
        Assert.That(inner.IsDraggingOuter, Is.False);
        Assert.That(inner.content.anchoredPosition.y - innerBefore.y,
            Is.EqualTo((dragEnd.y - dragStart.y) * 1.5f).Within(.1f));
        Assert.That(Vector2.Distance(outerBefore, outer.content.anchoredPosition), Is.LessThan(.1f));
        inner.verticalNormalizedPosition = 0;
        innerBefore = inner.content.anchoredPosition;
        MovePointer(pointer, Vector2.up * 20);
        inner.OnDrag(pointer);
        Assert.That(inner.IsDraggingOuter, Is.True);
        Assert.That(Vector2.Distance(innerBefore, inner.content.anchoredPosition), Is.LessThan(.1f));
        Assert.That(Vector2.Distance(outerBefore, outer.content.anchoredPosition), Is.GreaterThan(.1f));
        inner.OnEndDrag(pointer);
        Assert.That(inner.IsDragging, Is.False);
        outer.StopMovement();
        ResizeCard(card, 1920);
        Assert.That(inner.IsOverflowing, Is.False);
        outer.verticalNormalizedPosition = .5f;
        outerBefore = outer.content.anchoredPosition;
        inner.OnScroll(pointer);
        Assert.That(Vector2.Distance(outerBefore, outer.content.anchoredPosition), Is.GreaterThan(.1f));
        outer.StopMovement();
        inner.OnInitializePotentialDrag(pointer);
        inner.OnBeginDrag(pointer);
        Assert.That(inner.IsDraggingOuter, Is.True);
        inner.OnEndDrag(pointer);
    }

    [UnityTest]
    public IEnumerator ChapterMeasurementsReuseUnchangedInputsAndInvalidateForContentAndViewportChanges()
    {
        yield return LoadStory();
        StoryChapterCard card = Card(0);
        StoryChapterLayout layout = card.GetComponent<StoryChapterLayout>();
        TMP_Text title = card.transform.Find("Header/Title").GetComponent<TMP_Text>();
        TMP_Text body = card.transform.Find("BodyViewport/Body").GetComponent<TMP_Text>();
        GameObject action = card.transform.Find("Header/Actions").GetChild(0).gameObject;
        LayoutRebuilder.ForceRebuildLayoutImmediate(card.Rect);
        int initial = layout.MeasurementCountForEditor;
        for (int i = 0; i < 3; i++)
            LayoutRebuilder.ForceRebuildLayoutImmediate(card.Rect);
        Assert.That(layout.MeasurementCountForEditor, Is.EqualTo(initial),
            "The measurement counter is discrete: unchanged rebuilds must reuse existing measurements.");
        title.text += "\nLayout cache probe";
        LayoutRebuilder.ForceRebuildLayoutImmediate(card.Rect);
        int afterTitle = layout.MeasurementCountForEditor;
        Assert.That(afterTitle, Is.GreaterThan(initial));
        body.text += "\nBody cache probe";
        LayoutRebuilder.ForceRebuildLayoutImmediate(card.Rect);
        int afterBody = layout.MeasurementCountForEditor;
        Assert.That(afterBody, Is.GreaterThan(afterTitle));
        action.SetActive(!action.activeSelf);
        LayoutRebuilder.ForceRebuildLayoutImmediate(card.Rect);
        int afterAction = layout.MeasurementCountForEditor;
        Assert.That(afterAction, Is.GreaterThan(afterBody));
        card.SetExpanded(!card.IsExpanded);
        int afterExpansion = layout.MeasurementCountForEditor;
        Assert.That(afterExpansion, Is.GreaterThan(afterAction));
        RectTransform viewport = OuterScroll().viewport;
        float originalHeight = viewport.rect.height;
        viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, originalHeight + 50f);
        layout.CalculateLayoutInputVertical();
        int afterViewport = layout.MeasurementCountForEditor;
        Assert.That(afterViewport, Is.GreaterThan(afterExpansion));
        viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, originalHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(card.Rect);
        Debug.Log($"[StoryLayoutCache] initial={initial} unchanged={initial} title={afterTitle} body={afterBody} action={afterAction} expansion={afterExpansion} viewport={afterViewport}");
    }

    [UnityTest]
    public IEnumerator EveryChapterFitsAuthoredBackgroundAndKeepsAllTextAccessibleAtNarrowAndWideWidths()
    {
        yield return LoadStory();
        for (int i = 0; i < StoryManager.Chapters.Count; i++)
            Card(i).SetExpanded(true);
        foreach (float width in new[] { 600f, 1920f })
        {
            for (int i = 0; i < StoryManager.Chapters.Count; i++)
                ResizeCard(Card(i), width);
            yield return null;
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < StoryManager.Chapters.Count; i++)
            {
                StoryChapterCard card = Card(i);
                StoryChapter chapter = StoryManager.Chapters[i];
                TMP_Text title = card.transform.Find("Header/Title").GetComponent<TMP_Text>();
                TMP_Text text = card.transform.Find("BodyViewport/Body").GetComponent<TMP_Text>();
                StoryBodyScrollRect inner = card.transform.Find("BodyViewport").GetComponent<StoryBodyScrollRect>();
                Assert.That(inner.content.anchorMin.y, Is.EqualTo(1f).Within(.001f));
                Assert.That(inner.content.anchorMax.y, Is.EqualTo(1f).Within(.001f));
                Assert.That(inner.content.pivot.y, Is.EqualTo(1f).Within(.001f),
                    "The authored top pivot makes anchoredPosition.y = 0 the top of the body.");
                Vector2 originalPosition = inner.content.anchoredPosition;
                inner.content.anchoredPosition = new Vector2(originalPosition.x + 7f, 100f);
                inner.velocity = new Vector2(20f, 100f);
                card.SetExpanded(true);
                Assert.That(inner.content.anchoredPosition.y, Is.EqualTo(0f).Within(.1f),
                    "Expanding must immediately return the body to its authored top: " + chapter.Id);
                Assert.That(inner.content.anchoredPosition.x, Is.EqualTo(originalPosition.x + 7f).Within(.1f),
                    "Resetting the body must preserve its horizontal position.");
                Assert.That(inner.velocity.magnitude, Is.LessThan(.1f));
                inner.content.anchoredPosition = new Vector2(originalPosition.x, inner.content.anchoredPosition.y);
                Bounds topBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(inner.viewport, inner.content);
                Assert.That(topBounds.max.y, Is.EqualTo(inner.viewport.rect.yMax).Within(2f),
                    "The first line must be reachable immediately after expansion: " + chapter.Id);
                text.ForceMeshUpdate(true, true);
                Assert.That(text.text, Is.EqualTo(chapter.Summary + "\n\n" + chapter.Body));
                Assert.That(text.rectTransform.rect.height, Is.GreaterThanOrEqualTo(text.preferredHeight - 2f));
                Assert.That(inner.viewport.rect.height, Is.GreaterThan(0));
                AssertInside(card.Rect, title.rectTransform);
                RectTransform previousButton = null;
                foreach (Transform button in card.transform.Find("Header/Actions"))
                {
                    if (!button.gameObject.activeSelf) continue;
                    RectTransform buttonRect = (RectTransform)button;
                    AssertInside(card.Rect, buttonRect);
                    TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                    Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + 1f),
                        "Button text must fit: " + button.name);
                    if (previousButton != null)
                        Assert.That(Corners(previousButton)[2].x, Is.LessThanOrEqualTo(Corners(buttonRect)[0].x + .1f),
                            "Action buttons must not overlap.");
                    previousButton = buttonRect;
                }
                if (inner.IsOverflowing)
                {
                    inner.verticalNormalizedPosition = 0;
                    Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(inner.viewport, inner.content);
                    Assert.That(bounds.min.y, Is.EqualTo(inner.viewport.rect.yMin).Within(2f),
                        "The final line must be reachable: " + chapter.Id);
                    inner.verticalNormalizedPosition = 1;
                }
                if (i + 1 < StoryManager.Chapters.Count)
                {
                    Vector3[] current = Corners(card.Rect);
                    Vector3[] following = Corners(Card(i + 1).Rect);
                    Assert.That(current[0].y, Is.GreaterThanOrEqualTo(following[1].y - 1f), chapter.Id);
                }
                Debug.Log($"[StoryBackgroundLayout] chapter={chapter.Id} width={card.Rect.rect.width:0.##} " +
                    $"cardHeight={card.Rect.rect.height:0.##} bodyPreferred={text.preferredHeight:0.##} " +
                    $"bodyViewport={inner.viewport.rect.height:0.##} overflow={inner.IsOverflowing}");
            }
        }
    }

    [UnityTest]
    public IEnumerator AuthoredCardAndPurePreviewRenderToEvidenceImages()
    {
        yield return LoadStory();
        Canvas canvas = root.GetComponent<Canvas>();
        RenderMode originalMode = canvas.renderMode;
        Camera originalCamera = canvas.worldCamera;
        float originalDistance = canvas.planeDistance;
        GameObject cameraObject = new GameObject("StoryEvidenceCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 1000;
        camera.transform.position = new Vector3(0, 0, -10);
        RenderTexture target = new RenderTexture(2640, 1200, 24);
        target.Create();
        camera.targetTexture = target;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100;
            yield return null;
            Canvas.ForceUpdateCanvases();
            StoryChapterCard card = Card(0);
            card.SetExpanded(true);
            Canvas.ForceUpdateCanvases();
            ScrollRect outer = OuterScroll();
            outer.StopMovement();
            Vector3 cardTop = outer.viewport.InverseTransformPoint(card.Rect.TransformPoint(
                new Vector3(card.Rect.rect.center.x, card.Rect.rect.yMax, 0)));
            outer.content.anchoredPosition += Vector2.up * (outer.viewport.rect.yMax - cardTop.y);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(card.Rect.rect.height, Is.LessThanOrEqualTo(outer.viewport.rect.height),
                "The complete illustrated card must fit the story viewport.");
            Bounds illustrationBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(outer.viewport,
                card.transform.Find("IllustrationFrame/Illustration"));
            Assert.That(illustrationBounds.min.y, Is.GreaterThanOrEqualTo(outer.viewport.rect.yMin - 1f),
                "The lower illustration border must not be clipped by the page.");
            Assert.That(illustrationBounds.max.y, Is.LessThanOrEqualTo(outer.viewport.rect.yMax + 1f));
            string evidenceDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "data", "player-qa-fixes-20261025"));
            Directory.CreateDirectory(evidenceDirectory);
            CaptureCanvas(camera, target, Path.Combine(evidenceDirectory, "story-background-card.png"));
            card.transform.Find("Header/Actions/StoryIllustrationPreview").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            CaptureCanvas(camera, target, Path.Combine(evidenceDirectory, "story-background-preview.png"));
        }
        finally
        {
            canvas.renderMode = originalMode;
            canvas.worldCamera = originalCamera;
            canvas.planeDistance = originalDistance;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(cameraObject);
        }
    }

    private static void CaptureCanvas(Camera camera, RenderTexture target, string path)
    {
        RenderTexture previous = RenderTexture.active;
        Texture2D pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            Assert.That(File.Exists(path), Is.True, "Canvas evidence image must be saved.");
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(0), "Canvas evidence image must not be empty.");
            Debug.Log("[StoryBackgroundScreenshot] " + path);
        }
        finally
        {
            RenderTexture.active = previous;
            Object.Destroy(pixels);
        }
    }

    [UnityTest]
    public IEnumerator NewChapterKeepsThePreviousChapterExpandedAndRespectsManualCollapse()
    {
        yield return LoadStory(2);
        Assert.That(Card(1).IsExpanded, Is.True);
        CompleteStoryChapters(3);
        root.RefreshStoryPageIfChangedForEditor();
        yield return null;
        Assert.That(Card(1).IsExpanded, Is.True, "Unlocking another chapter must not close the chapter being read.");
        Assert.That(Card(2).IsExpanded, Is.True, "A newly unlocked latest chapter still opens by default.");
        Card(1).transform.Find("Header/Actions/StoryChapterToggle").GetComponent<Button>().onClick.Invoke();
        Assert.That(Card(1).IsExpanded, Is.False);
        CompleteStoryChapters(4);
        root.RefreshStoryPageIfChangedForEditor();
        yield return null;
        Assert.That(Card(1).IsExpanded, Is.False, "A manual collapse survives subsequent unlocks.");
    }

    [UnityTest]
    public IEnumerator NewChapterPreservesActualPagePositionWhenContentHeightChanges()
    {
        yield return LoadStory(2);
        ScrollRect outer = OuterScroll();
        outer.StopMovement();
        outer.verticalNormalizedPosition = .4f;
        Vector2 before = outer.content.anchoredPosition;
        float heightBefore = outer.content.rect.height;
        CompleteStoryChapters(3);
        root.RefreshStoryPageIfChangedForEditor();
        yield return null;
        Canvas.ForceUpdateCanvases();
        Assert.That(Mathf.Abs(outer.content.rect.height - heightBefore), Is.GreaterThan(1f),
            "The fixture must change the page height to distinguish pixel position from scroll ratio.");
        Assert.That(outer.content.anchoredPosition.y, Is.EqualTo(before.y).Within(1f));
    }

    [UnityTest]
    public IEnumerator NewChapterPreservesExpandedBodyReadingPosition()
    {
        yield return LoadStory(2);
        ScrollRect outer = OuterScroll();
        outer.viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 600f);
        Canvas.ForceUpdateCanvases();
        root.StoryPageBuiltForEditor = false;
        root.RefreshStoryPageIfChangedForEditor();
        yield return null;
        StoryBodyScrollRect body = Card(1).transform.Find("BodyViewport").GetComponent<StoryBodyScrollRect>();
        Assert.That(body.IsOverflowing, Is.True, "The fixture must expose actual nested text overflow.");
        body.StopMovement();
        body.verticalNormalizedPosition = .4f;
        float before = body.content.anchoredPosition.y;
        Assert.That(before, Is.GreaterThan(1f));
        CompleteStoryChapters(3);
        root.RefreshStoryPageIfChangedForEditor();
        yield return null;
        StoryBodyScrollRect replacement = Card(1).transform.Find("BodyViewport").GetComponent<StoryBodyScrollRect>();
        Assert.That(replacement, Is.Not.SameAs(body));
        Assert.That(replacement.IsOverflowing, Is.True);
        Assert.That(replacement.content.anchoredPosition.y, Is.EqualTo(before).Within(1f));
        Card(1).transform.Find("Header/Actions/StoryChapterToggle").GetComponent<Button>().onClick.Invoke();
        Card(1).transform.Find("Header/Actions/StoryChapterToggle").GetComponent<Button>().onClick.Invoke();
        Assert.That(replacement.content.anchoredPosition.y, Is.EqualTo(0f).Within(.1f), "Manually reopening a chapter still starts at its beginning.");
    }

    private static void CompleteStoryChapters(int count)
    {
        var completed = new List<string>();
        for (int i = 0; i < Mathf.Min(count, StoryManager.Chapters.Count); i++)
            completed.Add(StoryManager.Chapters[i].Id);
        StoryManager.RestoreSaveData(new SaveManager.StorySaveData { CompletedChapterIds = completed });
    }

    private IEnumerator LoadStory(int completedChapterCount = int.MaxValue)
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        for (int i = 0; i < 160 && Object.FindObjectOfType<GameBootstrap>()?.Completed != true; i++)
            yield return null;
        root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        Assert.That(Object.FindObjectOfType<GameBootstrap>()?.Completed, Is.True);
        SimulationManager.Instance.SetRunning(false);
        GameManager.Instance.AdvanceTechLevelForEditor(TechLevel.Ultra);
        CompleteStoryChapters(completedChapterCount);
        for (int i = 0; i < 16; i++)
        {
            root.RefreshOnboardingForEditor();
            if (TutorialManager.Current?.EvaluateStage() == null) break;
            TutorialManager.Current.SkipCurrentStage();
        }
        root.RefreshOnboardingForEditor();
        root.StoryPageBuiltForEditor = false;
        root.SetPage("Story");
        yield return null;
        Canvas.ForceUpdateCanvases();
    }

    private StoryChapterCard Card(int index) => root.transform.Find(SurfacePath)
        .Find("StoryChapter_" + StoryManager.Chapters[index].Id).GetComponent<StoryChapterCard>();

    private ScrollRect OuterScroll() => root.transform.Find("SafeAreaRoot/Content/PageHost").GetComponent<ScrollRect>();

    private static void ResizeCard(StoryChapterCard card, float width)
    {
        ((RectTransform)card.Rect.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        LayoutRebuilder.ForceRebuildLayoutImmediate(card.Rect);
        Canvas.ForceUpdateCanvases();
    }

    private static PointerEventData Pointer(Vector2 position) => new PointerEventData(EventSystem.current)
        { button = PointerEventData.InputButton.Left, position = position, pointerId = -1 };

    private static void MovePointer(PointerEventData pointer, Vector2 delta)
    {
        pointer.delta = delta;
        pointer.position += delta;
    }

    private static void AssertClosed(StoryIllustrationPreview preview)
    {
        Assert.That(preview.IsOpen, Is.False);
        Assert.That(preview.gameObject.activeSelf, Is.False);
        Assert.That(preview.Illustration, Is.Null);
    }

    private static Vector3[] Corners(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners;
    }

    private static void AssertInside(RectTransform parent, RectTransform child)
    {
        Vector3[] outer = Corners(parent);
        Vector3[] inner = Corners(child);
        Assert.That(inner[0].x, Is.GreaterThanOrEqualTo(outer[0].x - 1f), child.name);
        Assert.That(inner[2].x, Is.LessThanOrEqualTo(outer[2].x + 1f), child.name);
        Assert.That(inner[0].y, Is.GreaterThanOrEqualTo(outer[0].y - 1f), child.name);
        Assert.That(inner[2].y, Is.LessThanOrEqualTo(outer[2].y + 1f), child.name);
    }
}
