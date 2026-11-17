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

public sealed class KingdomCardPolishPlayModeTests
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
            "CardPolishTests-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Scene empty = SceneManager.CreateScene("CardPolishTeardown-" + Guid.NewGuid().ToString("N"));
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
    public IEnumerator StoryAndEraCardsStaySeparatedAndRenderAtTwoLandscapeSizes()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;
        root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        SimulationManager.Instance.SetRunning(false);
        // Complete only the opening chapters while preserving the initial era for its archive.
        var completed = new List<string>();
        for (int i = 0; i < 3; i++) completed.Add(StoryManager.Chapters[i].Id);
        StoryManager.RestoreSaveData(new SaveManager.StorySaveData { CompletedChapterIds = completed });
        root.StoryPageBuiltForEditor = false;

        Canvas canvas = root.GetComponent<Canvas>();
        RenderMode oldMode = canvas.renderMode;
        Camera oldCamera = canvas.worldCamera;
        float oldDistance = canvas.planeDistance;
        GameObject cameraObject = new GameObject("CardPolishEvidenceCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 1000;
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
                root.SetPage("Story");
                yield return null;
                Canvas.ForceUpdateCanvases();
                StoryChapterCard[] cards = root.transform.Find(Pages + "Story/StoryOverviewPage")
                    .GetComponentsInChildren<StoryChapterCard>();
                Assert.That(cards.Length, Is.GreaterThanOrEqualTo(3));
                foreach (StoryChapterCard card in cards) card.SetExpanded(false);
                yield return null;
                Canvas.ForceUpdateCanvases();
                OuterScroll().StopMovement();
                OuterScroll().verticalNormalizedPosition = 1;
                AssertStoryLayout(cards);
                Bounds firstCard = RectTransformUtility.CalculateRelativeRectTransformBounds(OuterScroll().viewport, cards[0].Rect);
                OuterScroll().content.anchoredPosition += Vector2.up * (OuterScroll().viewport.rect.yMax - firstCard.max.y - 8f);
                Canvas.ForceUpdateCanvases();
                float collapsedHeight = cards[0].Rect.rect.height;
                cards[0].transform.Find("Header/Actions/StoryChapterToggle").GetComponent<Button>().onClick.Invoke();
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(cards[0].IsExpanded, Is.True);
                Assert.That(cards[0].Rect.rect.height, Is.GreaterThan(collapsedHeight));
                AssertStoryLayout(cards);
                cards[0].transform.Find("Header/Actions/StoryChapterToggle").GetComponent<Button>().onClick.Invoke();
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(cards[0].IsExpanded, Is.False);
                Assert.That(cards[0].Rect.rect.height, Is.EqualTo(collapsedHeight).Within(2f));
                AssertStoryLayout(cards);
                AssertForwardedDrag(cards[0].transform.Find("Header/Actions/StoryChapterToggle").gameObject, camera);

                ResearchManager.Instance.GetState(DataBase<ResearchDef>.Find(ProgressionVisibilityRules.EraResearchId)).SetStatusForEditor(ResearchStatus.Completed);

                root.SetPage("Era");
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                OuterScroll().StopMovement();
                OuterScroll().verticalNormalizedPosition = 1;
                Transform archive = root.transform.Find(Pages + "Era/DataRows/EraCurrentArchive");
                Assert.That(archive, Is.Not.Null);
                AssertBoundary(archive);
                RectTransform identityCard = (RectTransform)archive.Find("Content/EraIdentity");
                Assert.That(identityCard, Is.Not.Null);
                AssertBoundary(identityCard);
                TMP_Text identityTitle = identityCard.Find("Title").GetComponent<TMP_Text>();
                TMP_Text identityBody = identityCard.Find("Body").GetComponent<TMP_Text>();
                identityTitle.ForceMeshUpdate();
                identityBody.ForceMeshUpdate();
                Assert.That(identityTitle.textInfo.characterCount, Is.GreaterThan(0));
                Assert.That(identityBody.textInfo.characterCount, Is.GreaterThan(0));
                AssertInside(identityCard, identityTitle.rectTransform);
                AssertInside(identityCard, identityBody.rectTransform);
                Assert.That(identityBody.rectTransform.rect.height, Is.GreaterThanOrEqualTo(identityBody.preferredHeight - 2f));
                RectTransform divider = (RectTransform)identityCard.Find("Divider");
                AssertAbove(identityTitle.rectTransform, divider);
                AssertAbove(divider, identityBody.rectTransform);
                foreach (Transform row in root.transform.Find(Pages + "Era/DataRows").GetComponentsInChildren<Transform>())
                    Assert.That(new[] { "EraDefinition", "EraCapabilities", "EraTransitionStatus",
                        "EraResearchSummary", "EraResourceSummary", "EraPrimaryBlocker" }, Does.Not.Contain(row.name),
                        "Era archive must not restore redundant summary cards.");
                AssertForwardedDrag(archive.gameObject, camera);
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                target = null;
            }
        }
        finally
        {
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            canvas.planeDistance = oldDistance;
            camera.targetTexture = null;
            if (target != null) { target.Release(); Object.Destroy(target); }
            Object.Destroy(cameraObject);
        }
    }

    private void AssertStoryLayout(StoryChapterCard[] cards)
    {
        for (int i = 0; i < cards.Length; i++)
        {
            StoryChapterCard card = cards[i];
            AssertBoundary(card.transform, "Header/Divider");
            TMP_Text title = card.transform.Find("Header/Title").GetComponent<TMP_Text>();
            title.ForceMeshUpdate();
            Assert.That(title.rectTransform.rect.height, Is.GreaterThanOrEqualTo(title.preferredHeight - 2f));
            AssertInside(card.Rect, title.rectTransform);
            AssertAbove(title.rectTransform, (RectTransform)card.transform.Find("BodyViewport"));
            if (i + 1 < cards.Length) AssertAbove(card.Rect, cards[i + 1].Rect);
        }
    }

    private ScrollRect OuterScroll() => root.transform.Find("SafeAreaRoot/Content/PageHost").GetComponent<ScrollRect>();

    private void AssertForwardedDrag(GameObject source, Camera camera)
    {
        Assert.That(source.GetComponent<UIPageScrollDragForwarder>(), Is.Not.Null, source.name);
        ScrollRect scroll = OuterScroll();
        Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = .5f;
        Vector2 before = scroll.content.anchoredPosition;
        var pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(camera, scroll.viewport.position)
        };
        ExecuteEvents.Execute(source, pointer, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.Execute(source, pointer, ExecuteEvents.beginDragHandler);
        pointer.delta = Vector2.up * 120;
        pointer.position += pointer.delta;
        ExecuteEvents.Execute(source, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(source, pointer, ExecuteEvents.endDragHandler);
        Assert.That(Vector2.Distance(before, scroll.content.anchoredPosition), Is.GreaterThan(1f), source.name);
        scroll.StopMovement();
    }

    private static void AssertBoundary(Transform card, string dividerPath = "Divider")
    {
        foreach (string edge in new[] { "Top", "Bottom", "Left", "Right" })
        {
            Image image = card.Find("CardFrame/" + edge).GetComponent<Image>();
            Assert.That(image.color.a, Is.GreaterThan(0f));
            Assert.That(image.raycastTarget, Is.False);
        }
        Image divider = card.Find(dividerPath).GetComponent<Image>();
        Assert.That(divider.raycastTarget, Is.False);
        Assert.That(divider.rectTransform.rect.height, Is.GreaterThan(0f));
    }

    private static void AssertInside(RectTransform parent, RectTransform child)
    {
        Vector3[] corners = Corners(child);
        foreach (Vector3 corner in corners)
        {
            Vector3 local = parent.InverseTransformPoint(corner);
            Assert.That(local.x, Is.InRange(parent.rect.xMin - 1f, parent.rect.xMax + 1f), child.name);
            Assert.That(local.y, Is.InRange(parent.rect.yMin - 1f, parent.rect.yMax + 1f), child.name);
        }
    }

    private static void AssertAbove(RectTransform earlier, RectTransform later) =>
        Assert.That(Corners(earlier)[0].y, Is.GreaterThanOrEqualTo(Corners(later)[1].y - 1f),
            earlier.name + " must not overlap " + later.name);

    private static Vector3[] Corners(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners;
    }
}
