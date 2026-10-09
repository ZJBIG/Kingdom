using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class KingdomOnboardingPlayModeTests
{
    private string saveRoot;
    [SetUp]
    public void SetUp()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        saveRoot = Path.Combine(projectRoot, "Temp", "KingdomOnboardingPlayModeTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveRoot);
        SaveManager.SetSaveRootOverrideForTests(saveRoot);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Scene empty = SceneManager.CreateScene("OnboardingTeardown-" + Guid.NewGuid().ToString("N"));
        SceneManager.SetActiveScene(empty);
        Scene sample = SceneManager.GetSceneByName("SampleScene");
        if (sample.IsValid() && sample.isLoaded)
            yield return SceneManager.UnloadSceneAsync(sample);
        SaveManager.ClearSaveRootOverrideForTests();
        if (!string.IsNullOrEmpty(saveRoot) && Directory.Exists(saveRoot))
            Directory.Delete(saveRoot, true);
        saveRoot = null;
    }

    [UnityTest]
    public IEnumerator OuterPageScroll_RestoresAfterResearchWarmup()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        Transform pageHostTransform = root.transform.Find(
            "SafeAreaRoot/Content/PageHost");
        Assert.That(pageHostTransform, Is.Not.Null);
        ScrollRect outerScroll = pageHostTransform.GetComponent<ScrollRect>();
        Assert.That(outerScroll, Is.Not.Null);

        root.SetPage("Research");
        yield return null;
        Assert.That(outerScroll.enabled, Is.False,
            "Research must keep the outer page ScrollRect disabled.");

        root.SetPage("Overview");
        yield return null;
        Assert.That(outerScroll.enabled, Is.True);
        Assert.That(outerScroll.vertical, Is.True);
        Assert.That(outerScroll.content, Is.EqualTo(
            pageHostTransform.Find("Overview")));

        root.SetPage("Story");
        yield return null;
        Assert.That(outerScroll.enabled, Is.True);
        Assert.That(outerScroll.vertical, Is.True);
        Assert.That(outerScroll.content, Is.EqualTo(
            pageHostTransform.Find("Story")));
        Assert.That(outerScroll.verticalNormalizedPosition,
            Is.GreaterThan(0.99f), "Story must enter at the top of its content.");
        RectTransform storyPage = pageHostTransform.Find("Story") as RectTransform;
        Assert.That(storyPage, Is.Not.Null);
        Assert.That(storyPage.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
        Assert.That(storyPage.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
        Assert.That(pageHostTransform.Find("Story/StoryOverviewPage"), Is.Not.Null);

        root.SetPage("Overview");
        yield return null;
        Assert.That(outerScroll.enabled, Is.True);
        Assert.That(outerScroll.vertical, Is.True);
        Assert.That(outerScroll.content, Is.EqualTo(
            pageHostTransform.Find("Overview")),
            "Returning from Story must restore Overview as the outer ScrollRect content.");
    }

    [UnityTest]
    public IEnumerator StoryPage_GeneratesNonEmptyTMPTextWithGeometry()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        root.SetPage("Story");
        yield return null;

        Transform rows = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Story/StoryOverviewPage");
        Assert.That(rows, Is.Not.Null);
        Assert.That(rows.Find("StoryOverviewHeader"), Is.Not.Null);
        TMP_Text[] texts = rows.GetComponentsInChildren<TMP_Text>(true);
        Assert.That(texts.Length, Is.GreaterThan(0));

        TMP_Text firstText = null;
        for (int i = 0; i < texts.Length; i++)
            if (!string.IsNullOrEmpty(texts[i].text))
            {
                firstText = texts[i];
                break;
            }
        Assert.That(firstText, Is.Not.Null,
            "Story must create non-empty text values.");
        Assert.That(firstText.font, Is.Not.Null,
            "Story text must have a font asset.");
        Assert.That(firstText.rectTransform.rect.width, Is.GreaterThan(0f));
        Assert.That(firstText.rectTransform.rect.height, Is.GreaterThan(0f));
        firstText.ForceMeshUpdate(true, true);
        Assert.That(firstText.textInfo.characterCount, Is.GreaterThan(0),
            "Story text must generate at least one TMP character.");
    }

    [UnityTest]
    public IEnumerator StoryPage_RendersRealChapterCardsAndAdaptsBodyHeight()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        root.SetPage("Story");
        yield return null;
        Canvas.ForceUpdateCanvases();

        StoryChapter chapter = StoryManager.Chapters[0];
        StoryChapter latest = StoryManager.FindLatestUnlocked(
            GameManager.Instance.State.TechLevel, TutorialManager.Current);
        Assert.That(latest, Is.Not.Null);
        Transform card = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Story/StoryOverviewPage/StoryChapter_" +
            chapter.Id);
        Assert.That(card, Is.Not.Null,
            "Story must render a card for the real first chapter.");
        Assert.That(card.GetComponent<Button>(), Is.Null,
            "The Story card itself must not be a button.");
        RectTransform titleRect = card.Find("Header/Title") as RectTransform;
        Assert.That(titleRect, Is.Not.Null);
        Assert.That(titleRect.rect.width, Is.GreaterThan(0f));
        Assert.That(titleRect.rect.height, Is.GreaterThan(0f));
        var titleCorners = new Vector3[4];
        var cardCorners = new Vector3[4];
        titleRect.GetWorldCorners(titleCorners);
        ((RectTransform)card).GetWorldCorners(cardCorners);
        Assert.That(titleCorners[0].x, Is.GreaterThanOrEqualTo(cardCorners[0].x));
        Assert.That(titleCorners[2].x, Is.LessThanOrEqualTo(cardCorners[2].x));
        Assert.That(titleCorners[2].y, Is.LessThanOrEqualTo(cardCorners[2].y));
        TMP_Text body = card.Find("Body")?.GetComponent<TMP_Text>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body.text, Does.Contain(chapter.Summary));
        Assert.That(body.text, Does.Not.Contain("选择右上角"));
        if (latest == chapter)
        {
            Assert.That(body.text, Does.Contain(chapter.Body));
            Assert.That(card.Find("Header/StoryChapterToggle").gameObject.activeSelf, Is.False,
                "The latest Story chapter must not be collapsible.");
            Button navigation = card.Find("Header/StoryChapterNavigation")?.GetComponent<Button>();
            Assert.That(navigation, Is.Not.Null,
                "The latest Story chapter must provide a navigation button.");
            Assert.That(card.GetComponent<Image>().color,
                Is.EqualTo(new Color(.76f, .50f, .25f, 1f)));
            yield break;
        }
        Assert.That(body.text, Does.Not.Contain(chapter.Body));

        Transform latestCard = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Story/StoryOverviewPage/StoryChapter_" +
            latest.Id);
        Assert.That(latestCard, Is.Not.Null);
        Assert.That(latestCard.Find("Header/StoryChapterToggle").gameObject.activeSelf, Is.False,
            "The latest Story chapter must not be collapsible.");
        Assert.That(latestCard.Find("Header/StoryChapterNavigation")?.GetComponent<Button>(),
            Is.Not.Null);
        Assert.That(latestCard.GetComponent<Image>().color,
            Is.EqualTo(new Color(.76f, .50f, .25f, 1f)));
        body.ForceMeshUpdate(true, true);
        float collapsedHeight = body.rectTransform.rect.height;
        Button toggle = card.Find("Header/StoryChapterToggle")?.GetComponent<Button>();
        Assert.That(toggle, Is.Not.Null,
            "Unlocked Story chapters must provide a non-blocking toggle.");
        RectTransform toggleRect = toggle.transform as RectTransform;
        Assert.That(toggleRect.rect.width, Is.GreaterThan(0f));
        Assert.That(toggleRect.rect.height, Is.GreaterThan(0f));
        Assert.That(toggle.gameObject.activeInHierarchy, Is.True);
        Assert.That(toggle.transform.parent, Is.EqualTo(card.Find("Header")));
        var toggleCorners = new Vector3[4];
        toggleRect.GetWorldCorners(toggleCorners);
        Assert.That(titleCorners[2].x, Is.LessThanOrEqualTo(toggleCorners[0].x),
            "Chapter heading and its toggle must not overlap.");
        Image cardImage = card.GetComponent<Image>();
        Image toggleImage = toggle.GetComponent<Image>();
        Assert.That(cardImage, Is.Not.Null);
        Assert.That(toggleImage, Is.Not.Null);
        Assert.That(toggleImage.color, Is.Not.EqualTo(cardImage.color),
            "The expand/collapse control must be visibly distinct from the card.");
        Assert.That(toggle.colors.pressedColor.r,
            Is.LessThan(toggle.colors.normalColor.r),
            "The expand/collapse button must darken while pressed.");
        Assert.That(toggle.GetComponent<UIPageScrollDragForwarder>(), Is.Null,
            "The Story toggle should use a direct button click without page-drag forwarding.");
        GameObject cardObject = card.gameObject;
        ScrollRect storyScroll = root.transform.Find(
            "SafeAreaRoot/Content/PageHost")?.GetComponent<ScrollRect>();
        Assert.That(storyScroll, Is.Not.Null);
        storyScroll.verticalNormalizedPosition = .5f;
        yield return null;
        float scrollBeforeToggle = storyScroll.verticalNormalizedPosition;
        toggle.onClick.Invoke();
        yield return null;
        Canvas.ForceUpdateCanvases();
        Assert.That(storyScroll.verticalNormalizedPosition,
            Is.EqualTo(scrollBeforeToggle).Within(.05f),
            "Expanding a chapter must preserve the Story viewport position.");
        Assert.That(card.gameObject, Is.SameAs(cardObject),
            "Expanding a chapter must update the existing card in place.");

        card = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Story/StoryOverviewPage/StoryChapter_" +
            chapter.Id);
        Assert.That(card, Is.Not.Null);
        body = card.Find("Body")?.GetComponent<TMP_Text>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body.text, Does.Contain(chapter.Body));
        body.ForceMeshUpdate(true, true);
        float preferredHeight = body.GetPreferredValues(
            body.text, body.rectTransform.rect.width, Mathf.Infinity).y;
        Assert.That(body.rectTransform.rect.height,
            Is.GreaterThanOrEqualTo(preferredHeight - 1f),
            "Chapter body must expand to its TMP content height.");
        Assert.That(body.rectTransform.rect.height, Is.GreaterThan(collapsedHeight));
    }

    [UnityTest]
    public IEnumerator StoryPage_EachChapterBodyHasDrawableTMPGeometry()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        root.SetPage("Story");
        yield return null;
        Canvas.ForceUpdateCanvases();

        Transform story = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Story/StoryOverviewPage");
        Assert.That(story, Is.Not.Null);
        Transform[] cards = story.GetComponentsInChildren<Transform>(true);
        int bodyCount = 0;
        for (int i = 0; i < cards.Length; i++)
        {
            Transform card = cards[i];
            if (card == null || !card.name.StartsWith("StoryChapter_", StringComparison.Ordinal))
                continue;
            TMP_Text body = card.Find("Body")?.GetComponent<TMP_Text>();
            Assert.That(body, Is.Not.Null, card.name + " must have a TMP Body.");
            Assert.That(body, Is.TypeOf<TextMeshProUGUI>());
            Assert.That(body.text, Is.Not.Empty);
            string chapterId = card.name.Substring("StoryChapter_".Length);
            StoryChapter chapter = StoryManager.Chapters.FirstOrDefault(
                candidate => candidate != null && candidate.Id == chapterId);
            Assert.That(chapter, Is.Not.Null, card.name);
            if (!StoryManager.IsUnlocked(chapter, GameManager.Instance.State.TechLevel,
                    TutorialManager.Current))
            {
                Assert.That(body.text, Is.EqualTo("这段文明记忆尚未完成。"), card.name);
                Assert.That(body.text, Does.Not.Contain("先唤醒上一段"), card.name);
            }
            Assert.That(body.gameObject.activeInHierarchy, Is.True);
            Assert.That(body.enabled, Is.True);
            Assert.That(body.font, Is.Not.Null);
            Assert.That(body.fontSharedMaterial, Is.Not.Null);
            Assert.That(body.materialForRendering, Is.Not.Null);
            Assert.That(body.color.a, Is.GreaterThan(0f));
            body.ForceMeshUpdate(true, true);
            Assert.That(body.textInfo.characterCount, Is.GreaterThan(0));
            int vertexCount = 0;
            for (int mesh = 0; mesh < body.textInfo.meshInfo.Length; mesh++)
                vertexCount += body.textInfo.meshInfo[mesh].vertices == null
                    ? 0 : body.textInfo.meshInfo[mesh].vertices.Length;
            Assert.That(vertexCount, Is.GreaterThan(0), card.name + " must have drawable TMP vertices.");
            Assert.That(body.rectTransform.rect.width, Is.GreaterThan(0f));
            Assert.That(body.rectTransform.rect.height, Is.GreaterThan(0f));
            bodyCount++;
        }

        Assert.That(bodyCount, Is.EqualTo(StoryManager.Chapters.Count));
    }

    [UnityTest]
    public IEnumerator StoryRefreshDisablesTheDeferredOldTreeBeforeRebuild()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        root.SetPage("Story");
        yield return null;

        RectTransform storyPage = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Story") as RectTransform;
        Assert.That(storyPage, Is.Not.Null);
        root.StoryPageBuiltForEditor = false;

        root.RefreshStoryPageIfChangedForEditor();

        int activeStorySurfaces = 0;
        for (int i = 0; i < storyPage.childCount; i++)
        {
            Transform child = storyPage.GetChild(i);
            if (child.name == "StoryOverviewPage" && child.gameObject.activeSelf)
                activeStorySurfaces++;
        }
        Assert.That(activeStorySurfaces, Is.EqualTo(1),
            "A Story rebuild must not leave the deferred old TMP tree visible.");
    }

    [UnityTest]
    public IEnumerator StoryDisplaysTheLatestTutorialActionFeedback()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        root.SetPage("Story");
        yield return null;

        root.TutorialRecentCompletionFeedbackForEditor = "真实行动反馈测试";
        root.StoryPageBuiltForEditor = false;

        root.RefreshStoryPageIfChangedForEditor();
        yield return null;

        Transform storyPage = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Story") as Transform;
        Assert.That(storyPage, Is.Not.Null);
        TMP_Text feedbackBody = null;
        Transform[] nodes = storyPage.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].name != "StoryOverviewMeaning" ||
                !nodes[i].gameObject.activeInHierarchy)
                continue;
            feedbackBody = nodes[i].Find("Body")?.GetComponent<TMP_Text>();
            break;
        }
        Assert.That(feedbackBody, Is.Not.Null);
        Assert.That(feedbackBody.text, Does.Contain("刚刚改变：真实行动反馈测试"));
    }

    [UnityTest]
    public IEnumerator StoryDisplaysTheLatestRuntimeActionNotice()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);

        root.SetPage("Story");
        yield return null;
        Transform surfaceBefore = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Story/StoryOverviewPage");
        Assert.That(surfaceBefore, Is.Not.Null);
        root.EnqueueRecentNoticeForEditor("action-feedback-a");
        root.EnqueueRecentNoticeForEditor("action-feedback-b");
        root.EnqueueRecentNoticeForEditor("action-feedback-c");
        root.EnqueueRecentNoticeForEditor("action-feedback-d");
        root.RefreshStoryPageIfChangedForEditor();
        yield return null;

        Transform storyPage = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Story");
        Assert.That(storyPage, Is.Not.Null);
        TMP_Text meaning = storyPage.Find(
            "StoryOverviewPage/StoryOverviewMeaning/Body")?.GetComponent<TMP_Text>();
        Assert.That(meaning, Is.Not.Null);
        Assert.That(meaning.text, Does.Not.Contain("action-feedback-a"));
        Assert.That(meaning.text, Does.Contain("action-feedback-b"));
        Assert.That(meaning.text, Does.Contain("action-feedback-c"));
        Assert.That(meaning.text, Does.Contain("action-feedback-d"));
        Transform surfaceAfter = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Story/StoryOverviewPage");
        Assert.That(surfaceAfter, Is.SameAs(surfaceBefore),
            "A feedback-only Story refresh must update the meaning card in place.");
    }

    [UnityTest]
    public IEnumerator OuterPageScroll_DirectDragReportsMeasuredBounds()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        root.SetPage("Overview");
        yield return null;
        yield return null;

        Transform pageHostTransform = root.transform.Find(
            "SafeAreaRoot/Content/PageHost");
        ScrollRect outerScroll = pageHostTransform?.GetComponent<ScrollRect>();
        Assert.That(outerScroll, Is.Not.Null);
        Assert.That(outerScroll.enabled, Is.True);
        Assert.That(outerScroll.content, Is.Not.Null);
        Image background = pageHostTransform.GetComponent<Image>();
        Assert.That(background, Is.Not.Null,
            "The authored PageHost background must own the Overview drag hit surface.");
        Assert.That(background.raycastTarget, Is.True,
            "Overview background drags require a raycastable authored PageHost surface.");

        Canvas.ForceUpdateCanvases();
        float viewportHeight = outerScroll.viewport.rect.height;
        float contentHeight = outerScroll.content.rect.height;
        float overflow = contentHeight - viewportHeight;
        if (overflow <= 1f)
        {
            // The batch runner uses a 640x480 window, which can make the
            // authored Overview fit exactly after Canvas scaling. Shrink only
            // this test's viewport and keep the authored content larger so the
            // regression exercises a real background drag without adding
            // production whitespace or rows.
            RectTransform viewportRect = pageHostTransform as RectTransform;
            float forcedViewportHeight = Mathf.Max(256f, viewportHeight * .5f);
            viewportRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical, forcedViewportHeight);
            Canvas.ForceUpdateCanvases();
            viewportHeight = outerScroll.viewport.rect.height;
            contentHeight = outerScroll.content.rect.height;
            overflow = contentHeight - viewportHeight;
        }
        Debug.Log("[王国界面] Outer page real-drag diagnostic: page=Overview" +
            ", enabled=" + outerScroll.enabled +
            ", viewport=" + viewportHeight +
            ", content=" + contentHeight +
            ", overflow=" + overflow +
            ", eventSystem=" + (EventSystem.current != null));
        Assert.That(EventSystem.current, Is.Not.Null);
        Assert.That(viewportHeight, Is.GreaterThan(0f));
        Assert.That(contentHeight, Is.GreaterThan(0f));

        Assert.That(overflow, Is.GreaterThan(1f),
            "Overview must contain its authored long-form content so this regression exercises a real overflow drag.");

        PointerEventData data = new(EventSystem.current)
        {
            position = new Vector2(Screen.width * .65f, Screen.height * .55f),
            pressPosition = new Vector2(Screen.width * .65f, Screen.height * .55f),
            button = PointerEventData.InputButton.Left,
            pointerId = -1,
            dragging = false,
            eligibleForClick = true,
            delta = Vector2.zero
        };
        GameObject hit = outerScroll.gameObject;
        Debug.Log("[王国界面] Outer page real-drag diagnostic: hit=" +
            (hit == null ? "null" : hit.name));

        outerScroll.verticalNormalizedPosition = .5f;
        yield return null;
        float before = outerScroll.content.anchoredPosition.y;
        ExecuteEvents.ExecuteHierarchy(hit, data,
            ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.beginDragHandler);
        data.position += Vector2.up * 160f;
        data.delta = Vector2.up * 160f;
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.dragHandler);
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.endDragHandler);
        yield return null;
        float after = outerScroll.content.anchoredPosition.y;
        Debug.Log("[王国界面] Outer page real-drag diagnostic: before=" +
            before + ", after=" + after);
        Assert.That(after, Is.Not.EqualTo(before).Within(.01f),
            "A direct Overview ScrollRect drag must move the overflowing content.");

        // The same background surface must clamp at both ends instead of
        // allowing content to escape the authored viewport.
        outerScroll.StopMovement();
        outerScroll.verticalNormalizedPosition = 1f;
        yield return null;
        data.position = new Vector2(Screen.width * .65f, Screen.height * .55f);
        data.pressPosition = data.position;
        data.delta = Vector2.zero;
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.beginDragHandler);
        data.position += Vector2.down * 2000f;
        data.delta = Vector2.down * 2000f;
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.dragHandler);
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.endDragHandler);
        yield return null;
        Assert.That(outerScroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.01f),
            "Overview background dragging past the top must remain clamped.");

        outerScroll.StopMovement();
        outerScroll.verticalNormalizedPosition = 0f;
        yield return null;
        data.position = new Vector2(Screen.width * .65f, Screen.height * .55f);
        data.pressPosition = data.position;
        data.delta = Vector2.zero;
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.beginDragHandler);
        data.position += Vector2.up * 2000f;
        data.delta = Vector2.up * 2000f;
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.dragHandler);
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.endDragHandler);
        yield return null;
        Assert.That(outerScroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(.01f),
            "Overview background dragging past the bottom must remain clamped.");

        // A short background click must not move the page.
        outerScroll.StopMovement();
        outerScroll.verticalNormalizedPosition = .42f;
        yield return null;
        float shortClickPosition = outerScroll.content.anchoredPosition.y;
        data.position = new Vector2(Screen.width * .65f, Screen.height * .55f);
        data.pressPosition = data.position;
        data.delta = Vector2.zero;
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerUpHandler);
        yield return null;
        Assert.That(outerScroll.content.anchoredPosition.y,
            Is.EqualTo(shortClickPosition).Within(.01f),
            "A short Overview background click must not move the page.");

        // Page switches must preserve the measured position and rebind the
        // same ScrollRect to Overview when returning from Research.
        float expectedNormalized = outerScroll.verticalNormalizedPosition;
        root.SetPage("Research");
        yield return null;
        root.SetPage("Overview");
        yield return null;
        Canvas.ForceUpdateCanvases();
        Assert.That(outerScroll.enabled, Is.True);
        Assert.That(outerScroll.content, Is.EqualTo(pageHostTransform.Find("Overview")));
        Assert.That(outerScroll.verticalNormalizedPosition,
            Is.EqualTo(expectedNormalized).Within(.02f),
            "Returning from Research must restore the Overview drag position.");
    }

    [UnityTest]
    public IEnumerator ResourceAndBuildingRowsShareOuterPageDragForwarder()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        root.SetPage("Resources");
        yield return null;
        Transform resourcesRows = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Resources/DataRows");
        Assert.That(resourcesRows, Is.Not.Null);
        Assert.That(resourcesRows.childCount, Is.GreaterThan(0));
        Assert.That(resourcesRows.GetChild(0).GetComponent<UIPageScrollDragForwarder>(),
            Is.Not.Null,
            "Resource rows must forward drag lifecycle to the shared page ScrollRect.");

        root.SetPage("Buildings");
        yield return null;
        Transform buildingsRows = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Buildings/DataRows");
        Assert.That(buildingsRows, Is.Not.Null);
        Assert.That(buildingsRows.childCount, Is.GreaterThan(0));
        Assert.That(buildingsRows.GetChild(0).GetComponent<UIPageScrollDragForwarder>(),
            Is.Not.Null,
            "Building rows must use the same shared page drag forwarder.");

        ScrollRect pageScroll = root.transform.Find(
            "SafeAreaRoot/Content/PageHost")?.GetComponent<ScrollRect>();
        Assert.That(pageScroll, Is.Not.Null);
        Assert.That(pageScroll.content, Is.EqualTo(
            root.transform.Find("SafeAreaRoot/Content/PageHost/Buildings")));
        Assert.That(pageScroll.enabled, Is.True);

        root.SetPage("Era");
        yield return null;
        Transform eraPage = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Era");
        Assert.That(eraPage, Is.Not.Null);
        Assert.That(pageScroll.content, Is.EqualTo(eraPage),
            "Era must become the shared ScrollRect content after switching tabs.");
        Assert.That(pageScroll.enabled, Is.True);
        Canvas.ForceUpdateCanvases();
        Assert.That(pageScroll.content.rect.height,
            Is.GreaterThan(pageScroll.viewport.rect.height),
            "Era must expose a real vertical drag range.");

        Transform eraRows = eraPage.Find("DataRows");
        Assert.That(eraRows, Is.Not.Null);
        Assert.That(eraRows.childCount, Is.GreaterThan(0));
        Assert.That(eraRows.GetChild(0).GetComponent<UIPageScrollDragForwarder>(),
            Is.Not.Null,
            "Era rows must use the same authored drag forwarder as Resources and Buildings.");

        Canvas.ForceUpdateCanvases();
        float before = pageScroll.content.anchoredPosition.y;
        PointerEventData data = new(EventSystem.current)
        {
            position = new Vector2(Screen.width * .65f, Screen.height * .55f),
            pressPosition = new Vector2(Screen.width * .65f, Screen.height * .55f),
            button = PointerEventData.InputButton.Left,
            pointerId = -1,
            delta = Vector2.zero
        };
        GameObject hit = eraRows.GetChild(0).gameObject;
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.beginDragHandler);
        data.position += Vector2.up * 160f;
        data.delta = Vector2.up * 160f;
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.dragHandler);
        ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.endDragHandler);
        yield return null;
        Assert.That(pageScroll.content.anchoredPosition.y,
            Is.Not.EqualTo(before).Within(.01f),
            "A drag beginning on an Era row must move the shared outer ScrollRect.");
    }

    [UnityTest]
    public IEnumerator StoryPage_NewOverviewShellIntersectsViewport()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        root.SetPage("Story");
        yield return null;
        Canvas.ForceUpdateCanvases();

        Transform card = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Story/StoryOverviewPage/StoryOverviewHeader");
        Assert.That(card, Is.Not.Null);
        TMP_Text body = card.Find("Body")?.GetComponent<TMP_Text>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body.text, Is.Not.Empty);
        body.ForceMeshUpdate(true, true);
        Assert.That(body.textInfo.characterCount, Is.GreaterThan(0));
        Assert.That(body.canvasRenderer.GetAlpha(), Is.GreaterThan(0f));
        Assert.That(body.text, Does.Contain("鼠族"));

        Transform pageHostTransform = root.transform.Find(
            "SafeAreaRoot/Content/PageHost");
        ScrollRect outerScroll = pageHostTransform.GetComponent<ScrollRect>();
        Assert.That(outerScroll.content, Is.EqualTo(
            pageHostTransform.Find("Story")));
        Vector2 viewport = outerScroll.viewport.rect.size;
        Vector2 content = outerScroll.content.rect.size;
        Debug.Log("[王国剧情] Story expanded visibility diagnostic: textLength=" +
             body.text.Length + ", characters=" + body.textInfo.characterCount +
            ", alpha=" + body.canvasRenderer.GetAlpha() +
            ", textRect=" + body.rectTransform.rect.size +
            ", viewport=" + viewport + ", content=" + content);
        Assert.That(viewport.y, Is.GreaterThan(0f));
        Assert.That(content.y, Is.GreaterThan(0f));
        float preferredHeight = body.GetPreferredValues(
            body.text, body.rectTransform.rect.width, Mathf.Infinity).y;
        Assert.That(body.rectTransform.rect.height,
            Is.GreaterThanOrEqualTo(preferredHeight - 1f),
            "Expanded Story body must not be clipped by its own RectTransform.");

        Vector3[] bodyCorners = new Vector3[4];
        Vector3[] viewportCorners = new Vector3[4];
        body.rectTransform.GetWorldCorners(bodyCorners);
        outerScroll.viewport.GetWorldCorners(viewportCorners);
        Rect bodyRect = Rect.MinMaxRect(bodyCorners[0].x, bodyCorners[0].y,
            bodyCorners[2].x, bodyCorners[2].y);
        Rect viewportRect = Rect.MinMaxRect(viewportCorners[0].x, viewportCorners[0].y,
            viewportCorners[2].x, viewportCorners[2].y);
        Assert.That(bodyRect.Overlaps(viewportRect), Is.True,
            "Expanded Story text must intersect the page viewport.");
    }

    [UnityTest]
    public IEnumerator RuntimeUiTextPreservesAuthoredTopBarFontSettings()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        TMP_Text[] texts = root.transform.Find("SafeAreaRoot")
            ?.GetComponentsInChildren<TMP_Text>(true);
        Assert.That(texts, Is.Not.Null);
        TMP_Text title = root.transform.Find("SafeAreaRoot/TopStatusBar/Title")
            ?.GetComponent<TMP_Text>();
        TMP_Text date = root.transform.Find("SafeAreaRoot/TopStatusBar/Title/Date")
            ?.GetComponent<TMP_Text>();
        Assert.That(title, Is.Not.Null);
        Assert.That(date, Is.Not.Null);
        Assert.That(title.fontSizeMin == 30f && title.fontSizeMax == 30f, Is.False);
        Assert.That(date.fontSizeMin == 30f && date.fontSizeMax == 30f, Is.False);
    }

    [UnityTest]
    public IEnumerator OverviewShowsPersistentOnboardingGoal()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;

        Transform primaryCard = GameObject.Find("KingdomUIRoot/SafeAreaRoot/Content/PageHost/Overview/PrimaryCard")?.transform;
        if (primaryCard == null)
        {
            KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
            Assert.That(root, Is.Not.Null);
            primaryCard = root.transform.Find("SafeAreaRoot/Content/PageHost/Overview/PrimaryCard");
        }
        Assert.That(primaryCard, Is.Not.Null);
        TMP_Text text = primaryCard.Find("Text")?.GetComponent<TMP_Text>();
        Assert.That(text, Is.Not.Null);
        Assert.That(text.text, Does.Contain("当前目标"));
        Assert.That(text.text, Does.Contain("当前阻碍"));
        Assert.That(text.text, Does.Contain("推荐行动"));
        Assert.That(text.text, Does.Contain("人口"));
        Assert.That(text.text, Does.Contain("食物"));
        Assert.That(text.text, Does.Contain("下一时代目标"));
        Assert.That(text.text, Does.Not.Contain("完成方式"));
        Assert.That(text.text, Does.Not.Contain("上一步已完成"));
        Assert.That(text.text, Does.Not.Contain("刚刚发生"));
        Assert.That(text.text, Does.Not.Contain("文明记忆"),
            "Story-only memory labels must not duplicate the Overview action card.");
    }

    [UnityTest]
    public IEnumerator CurrentNavigationPageIsNotInteractable()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);

        Button eraButton = root.transform.Find(
            "SafeAreaRoot/LeftNavigation/NavigationButtons/Nav_Era")?.GetComponent<Button>();
        Button resourcesButton = root.transform.Find(
            "SafeAreaRoot/LeftNavigation/NavigationButtons/Nav_Resources")?.GetComponent<Button>();
        Assert.That(eraButton, Is.Not.Null);
        Assert.That(resourcesButton, Is.Not.Null);

        root.SetPage("Era");
        yield return null;
        Assert.That(eraButton.interactable, Is.False);
        Assert.That(eraButton.colors.disabledColor.r,
            Is.EqualTo(194f / 255f).Within(.01f));
        Assert.That(resourcesButton.interactable, Is.True);

        root.SetPage("Resources");
        yield return null;
        Assert.That(resourcesButton.interactable, Is.False);
        Assert.That(resourcesButton.colors.disabledColor.r,
            Is.EqualTo(194f / 255f).Within(.01f));
        Assert.That(eraButton.interactable, Is.True);
    }

    [UnityTest]
    public IEnumerator TutorialFeedbackIsClearedWhenSaveProgressVersionChanges()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        TutorialManager tutorial = TutorialManager.Ensure();
        root.TutorialRecentCompletionFeedbackForEditor = "旧存档中的反馈";
        root.TutorialFeedbackVersionForEditor = tutorial.Version;
        root.RecentActionFeedbackForEditor = "old-save-action-feedback";
        root.RecentActionFeedbackVersionForEditor = tutorial.SaveSessionVersion - 1;
        tutorial.RestoreSaveData(
            new SaveManager.TutorialSaveData { ActiveStepId = "orientation" },
            TechLevel.Animal);
        root.RefreshUI();

        Assert.That(root.TutorialRecentCompletionFeedbackForEditor, Is.EqualTo(string.Empty),
            "Loading a new tutorial version must not display feedback from the previous save.");
        Assert.That(root.RecentActionFeedbackForEditor, Is.EqualTo(string.Empty),
            "Loading a new tutorial version must not display action feedback from the previous save.");
    }

    [UnityTest]
    public IEnumerator IndustrialStoryNavigationFollowsTheRealWorkshopBlocker()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        StoryChapter chapter = null;
        for (int i = 0; i < StoryManager.Chapters.Count; i++)
            if (StoryManager.Chapters[i] != null &&
                StoryManager.Chapters[i].Id == "WorkshopMemory_09")
            {
                chapter = StoryManager.Chapters[i];
                break;
            }
        Assert.That(chapter, Is.Not.Null);
        WorkshopUpgrade upgrade = DataBase<WorkshopUpgrade>.Find("PrecisionTooling");
        Assert.That(upgrade, Is.Not.Null);
        Assert.That(WorkshopManager.Instance, Is.Not.Null);

        Assert.That(root.GetStoryNavigationPageForEditor(chapter),
            Is.EqualTo("Workshop"));
        Assert.That(root.GetStoryNavigationTargetForEditor(chapter),
            Is.EqualTo("PrecisionTooling"));
    }
}
