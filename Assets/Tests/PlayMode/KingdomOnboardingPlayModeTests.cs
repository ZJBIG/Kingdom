using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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

        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);

        setPage.Invoke(root, new object[] { "Research" });
        yield return null;
        Assert.That(outerScroll.enabled, Is.False,
            "Research must keep the outer page ScrollRect disabled.");

        setPage.Invoke(root, new object[] { "Overview" });
        yield return null;
        Assert.That(outerScroll.enabled, Is.True);
        Assert.That(outerScroll.vertical, Is.True);
        Assert.That(outerScroll.content, Is.EqualTo(
            pageHostTransform.Find("Overview")));

        setPage.Invoke(root, new object[] { "Story" });
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

        setPage.Invoke(root, new object[] { "Overview" });
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
        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);
        setPage.Invoke(root, new object[] { "Story" });
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
        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);
        setPage.Invoke(root, new object[] { "Story" });
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
        RectTransform titleRect = card.Find("Title") as RectTransform;
        Assert.That(titleRect, Is.Not.Null);
        Assert.That(titleRect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
        Assert.That(titleRect.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));
        Assert.That(titleRect.pivot, Is.EqualTo(new Vector2(0f, 1f)));
        Assert.That(titleRect.anchoredPosition.x, Is.EqualTo(16f).Within(.01f));
        TMP_Text body = card.Find("Body")?.GetComponent<TMP_Text>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body.text, Does.Contain(chapter.Summary));
        Assert.That(body.text, Does.Not.Contain("选择右上角"));
        if (latest == chapter)
        {
            Assert.That(body.text, Does.Contain(chapter.Body));
            Assert.That(card.Find("StoryChapterToggle"), Is.Null,
                "The latest Story chapter must not be collapsible.");
            Button navigation = card.Find("StoryChapterNavigation")?.GetComponent<Button>();
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
        Assert.That(latestCard.Find("StoryChapterToggle"), Is.Null,
            "The latest Story chapter must not be collapsible.");
        Assert.That(latestCard.Find("StoryChapterNavigation")?.GetComponent<Button>(),
            Is.Not.Null);
        Assert.That(latestCard.GetComponent<Image>().color,
            Is.EqualTo(new Color(.76f, .50f, .25f, 1f)));
        body.ForceMeshUpdate(true, true);
        float collapsedHeight = body.rectTransform.rect.height;
        Button toggle = card.Find("StoryChapterToggle")?.GetComponent<Button>();
        Assert.That(toggle, Is.Not.Null,
            "Unlocked Story chapters must provide a non-blocking toggle.");
        RectTransform toggleRect = toggle.transform as RectTransform;
        Assert.That(toggleRect.anchorMin, Is.EqualTo(new Vector2(1f, 1f)));
        Assert.That(toggleRect.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
        Assert.That(toggleRect.rect.width, Is.GreaterThan(0f));
        Assert.That(toggleRect.rect.height, Is.GreaterThan(0f));
        Assert.That(toggle.transform.parent, Is.EqualTo(card));
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
        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);
        setPage.Invoke(root, new object[] { "Story" });
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
            Assert.That(body.gameObject.activeInHierarchy, Is.True);
            Assert.That(body.enabled, Is.True);
            Assert.That(body.font, Is.Not.Null);
            Assert.That(body.fontSharedMaterial, Is.Not.Null);
            Assert.That(body.materialForRendering, Is.Not.Null);
            Assert.That(body.color.a, Is.GreaterThan(0f));
            Assert.That(body.canvasRenderer.cull, Is.False);
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
        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);
        setPage.Invoke(root, new object[] { "Story" });
        yield return null;

        RectTransform storyPage = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Story") as RectTransform;
        Assert.That(storyPage, Is.Not.Null);
        FieldInfo builtField = typeof(KingdomUIRoot).GetField(
            "storyPageBuilt", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(builtField, Is.Not.Null);
        builtField.SetValue(root, false);

        MethodInfo refresh = typeof(KingdomUIRoot).GetMethod(
            "RefreshStoryPageIfChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(refresh, Is.Not.Null);
        refresh.Invoke(root, null);

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
        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);
        setPage.Invoke(root, new object[] { "Story" });
        yield return null;

        FieldInfo feedbackField = typeof(KingdomUIRoot).GetField(
            "tutorialRecentCompletionFeedback",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo builtField = typeof(KingdomUIRoot).GetField(
            "storyPageBuilt", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(feedbackField, Is.Not.Null);
        Assert.That(builtField, Is.Not.Null);
        feedbackField.SetValue(root, "真实行动反馈测试");
        builtField.SetValue(root, false);

        MethodInfo refresh = typeof(KingdomUIRoot).GetMethod(
            "RefreshStoryPageIfChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(refresh, Is.Not.Null);
        refresh.Invoke(root, null);
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
        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo enqueue = typeof(KingdomUIRoot).GetMethod(
            "EnqueueRecentNotice", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo refresh = typeof(KingdomUIRoot).GetMethod(
            "RefreshStoryPageIfChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);
        Assert.That(enqueue, Is.Not.Null);
        Assert.That(refresh, Is.Not.Null);

        setPage.Invoke(root, new object[] { "Story" });
        yield return null;
        Transform surfaceBefore = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Story/StoryOverviewPage");
        Assert.That(surfaceBefore, Is.Not.Null);
        enqueue.Invoke(root, new object[] { "action-feedback-a" });
        enqueue.Invoke(root, new object[] { "action-feedback-b" });
        enqueue.Invoke(root, new object[] { "action-feedback-c" });
        enqueue.Invoke(root, new object[] { "action-feedback-d" });
        refresh.Invoke(root, null);
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
    public IEnumerator OuterPageScroll_RealRaycastDragReportsMeasuredBounds()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);
        setPage.Invoke(root, new object[] { "Overview" });
        yield return null;
        yield return null;

        Transform pageHostTransform = root.transform.Find(
            "SafeAreaRoot/Content/PageHost");
        ScrollRect outerScroll = pageHostTransform?.GetComponent<ScrollRect>();
        Assert.That(outerScroll, Is.Not.Null);
        Assert.That(outerScroll.enabled, Is.True);
        Assert.That(outerScroll.content, Is.Not.Null);

        Canvas.ForceUpdateCanvases();
        float viewportHeight = outerScroll.viewport.rect.height;
        float contentHeight = outerScroll.content.rect.height;
        float overflow = contentHeight - viewportHeight;
        Debug.Log("[王国界面] Outer page real-drag diagnostic: page=Overview" +
            ", enabled=" + outerScroll.enabled +
            ", viewport=" + viewportHeight +
            ", content=" + contentHeight +
            ", overflow=" + overflow +
            ", eventSystem=" + (EventSystem.current != null));
        Assert.That(EventSystem.current, Is.Not.Null);
        Assert.That(viewportHeight, Is.GreaterThan(0f));
        Assert.That(contentHeight, Is.GreaterThan(0f));

        if (overflow <= 1f)
        {
            Assert.Ignore("No measured Overview overflow; background drag was not exercised.");
        }

        Canvas canvas = root.GetComponent<Canvas>() ??
            root.GetComponentInParent<Canvas>();
        GraphicRaycaster raycaster = canvas?.GetComponent<GraphicRaycaster>();
        Assert.That(raycaster, Is.Not.Null);
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
        List<RaycastResult> hits = new();
        raycaster.Raycast(data, hits);
        Assert.That(hits.Count, Is.GreaterThan(0),
            "Overview background must be hit by the GraphicRaycaster.");
        GameObject hit = hits[0].gameObject;
        Assert.That(hit.transform == outerScroll.viewport ||
            hit.transform.IsChildOf(outerScroll.viewport), Is.True,
            "Overview raycast must remain inside the page viewport.");
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
            "A raycasted Overview drag must move the overflowing content.");
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
        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);

        setPage.Invoke(root, new object[] { "Resources" });
        yield return null;
        Transform resourcesRows = root.transform.Find(
            "SafeAreaRoot/Content/PageHost/Resources/DataRows");
        Assert.That(resourcesRows, Is.Not.Null);
        Assert.That(resourcesRows.childCount, Is.GreaterThan(0));
        Assert.That(resourcesRows.GetChild(0).GetComponent<UIPageScrollDragForwarder>(),
            Is.Not.Null,
            "Resource rows must forward drag lifecycle to the shared page ScrollRect.");

        setPage.Invoke(root, new object[] { "Buildings" });
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

        setPage.Invoke(root, new object[] { "Era" });
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
        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);
        setPage.Invoke(root, new object[] { "Story" });
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
    public IEnumerator RuntimeUiTextUsesThirtyPointFont()
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
        Transform leftNavigation = root.transform.Find("SafeAreaRoot/LeftNavigation");
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null)
                continue;
            if (leftNavigation != null && texts[i].transform.IsChildOf(leftNavigation))
                continue;
            Assert.That(texts[i].fontSize, Is.EqualTo(30f).Within(.01f),
                texts[i].name + " must use the 30 point runtime UI font.");
        }
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
        Assert.That(text.text, Does.Contain("下一时代目标"));
        Assert.That(text.text, Does.Contain("当前准备度"),
             "Overview must show real population, productivity and research readiness.");
        Assert.That(text.text, Does.Contain("推荐行动"));
        Assert.That(text.text, Does.Contain("完成方式"));
        Assert.That(text.text, Does.Not.Contain("文明记忆"),
            "Story-only memory labels must not duplicate the Overview action card.");
    }

    [UnityTest]
    public IEnumerator EraPageIncludesOnboardingGuidance()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
        yield return null;
        yield return null;

        KingdomUIRoot root = Object.FindObjectOfType<KingdomUIRoot>();
        Assert.That(root, Is.Not.Null);
        TutorialManager tutorial = TutorialManager.Ensure();
        MethodInfo restoreTutorial = typeof(TutorialManager).GetMethod(
            "RestoreSaveData", BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(restoreTutorial, Is.Not.Null);
        restoreTutorial.Invoke(tutorial, new object[]
        {
            new SaveManager.TutorialSaveData { ActiveStepId = "era-goal" },
            TechLevel.Animal
        });
        MethodInfo setPage = typeof(KingdomUIRoot).GetMethod(
            "SetPage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(setPage, Is.Not.Null);
        setPage.Invoke(root, new object[] { "Era" });
        yield return null;

        Transform rows = root.transform.Find("SafeAreaRoot/Content/PageHost/Era/DataRows");
        Assert.That(rows, Is.Not.Null);
        string rendered = string.Empty;
        TMP_Text[] labels = rows.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
            rendered += labels[i].text + "\n";
        Assert.That(rendered, Does.Contain("当前引导"));
        Assert.That(rendered, Does.Contain("当前目标、阻碍和推荐行动统一显示在 Overview。"));
        Assert.That(rendered, Does.Not.Contain("引导目标"));
        Assert.That(rendered, Does.Not.Contain("引导推荐行动"));
        Assert.That(rendered, Does.Not.Contain("文明复兴阶段"));
        Assert.That(rendered, Does.Contain("本时代能力"));

        FieldInfo eraRowsField = typeof(KingdomUIRoot).GetField(
            "eraTextRows", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(eraRowsField, Is.Not.Null);
        var eraRows = eraRowsField.GetValue(root) as List<GameObject>;
        Assert.That(eraRows, Has.Count.GreaterThan(3));
        Button tutorialAction = eraRows[1].GetComponent<Button>();
        Assert.That(tutorialAction, Is.Not.Null);
        Assert.That(tutorialAction.interactable, Is.True,
            "Era should provide a real navigation link back to the authoritative Overview guidance.");
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
        FieldInfo feedbackField = typeof(KingdomUIRoot).GetField(
            "tutorialRecentCompletionFeedback", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo versionField = typeof(KingdomUIRoot).GetField(
            "tutorialFeedbackVersion", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo actionFeedbackField = typeof(KingdomUIRoot).GetField(
            "recentActionFeedback", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo actionFeedbackVersionField = typeof(KingdomUIRoot).GetField(
            "recentActionFeedbackVersion", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(feedbackField, Is.Not.Null);
        Assert.That(versionField, Is.Not.Null);
        Assert.That(actionFeedbackField, Is.Not.Null);
        Assert.That(actionFeedbackVersionField, Is.Not.Null);

        feedbackField.SetValue(root, "旧存档中的反馈");
        versionField.SetValue(root, tutorial.Version);
        actionFeedbackField.SetValue(root, "old-save-action-feedback");
        actionFeedbackVersionField.SetValue(root, tutorial.SaveSessionVersion - 1);
        tutorial.RestoreSaveData(
            new SaveManager.TutorialSaveData { ActiveStepId = "orientation" },
            TechLevel.Animal);
        root.RefreshUI();

        Assert.That(feedbackField.GetValue(root), Is.EqualTo(string.Empty),
            "Loading a new tutorial version must not display feedback from the previous save.");
        Assert.That(actionFeedbackField.GetValue(root), Is.EqualTo(string.Empty),
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
                StoryManager.Chapters[i].Id == "industrial-scale")
            {
                chapter = StoryManager.Chapters[i];
                break;
            }
        Assert.That(chapter, Is.Not.Null);
        WorkshopUpgrade upgrade = DataBase<WorkshopUpgrade>.Find("PrecisionTooling");
        Assert.That(upgrade, Is.Not.Null);
        Assert.That(WorkshopManager.Instance, Is.Not.Null);

        bool purchased = WorkshopManager.Instance.IsPurchased(upgrade);
        MethodInfo pageMethod = typeof(KingdomUIRoot).GetMethod(
            "GetStoryNavigationPage", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo targetMethod = typeof(KingdomUIRoot).GetMethod(
            "GetStoryNavigationTarget", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(pageMethod, Is.Not.Null);
        Assert.That(targetMethod, Is.Not.Null);

        Assert.That(pageMethod.Invoke(root, new object[] { chapter }),
            Is.EqualTo(purchased ? "Buildings" : "Workshop"));
        Assert.That(targetMethod.Invoke(root, new object[] { chapter }),
            Is.EqualTo(purchased ? "MachineFactory" : "PrecisionTooling"));
    }
}
