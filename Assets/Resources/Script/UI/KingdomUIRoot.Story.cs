using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Story is a read-only narrative layer over the real progression state.
public sealed partial class KingdomUIRoot
{
    private bool storyPageBuilt;
    private bool storyScrollInitialized;
    private string storyPageStateSignature = string.Empty;
    private string storyProgressSignature = string.Empty;
    private int storyObservedUnlockCount = -1;
    private TechLevel storyObservedEra;
    private bool storyObservedEraInitialized;
    private readonly Dictionary<string, bool> storyChapterCollapsed =
        new Dictionary<string, bool>(StringComparer.Ordinal);
    private readonly List<TMP_Text> storyBodyTextCache = new();

    private void KeepStoryTextGeometryDrawable()
    {
        if (!pages.TryGetValue("Story", out RectTransform page) || page == null)
            return;
        if (storyBodyTextCache.Count == 0)
            page.GetComponentsInChildren(true, storyBodyTextCache);
        for (int i = 0; i < storyBodyTextCache.Count; i++)
            if (storyBodyTextCache[i] != null && storyBodyTextCache[i].name == "Body")
            {
                storyBodyTextCache[i].alignment = TextAlignmentOptions.TopLeft;
                storyBodyTextCache[i].enableWordWrapping = true;
            }
    }

    private void ObserveStoryProgress(TechLevel era, TutorialManager tutorial,
        out int unlockedCount, out StoryChapter latestChapter)
    {
        unlockedCount = StoryManager.CountUnlocked(era, tutorial);
        latestChapter = StoryManager.FindLatestUnlocked(era, tutorial);
        storyObservedUnlockCount = unlockedCount;
        storyObservedEra = era;
        storyObservedEraInitialized = true;
    }

    private void BuildStoryPage(RectTransform page, bool refresh)
    {
        if (page == null)
            return;

        string previousSignature = storyPageStateSignature;
        string previousProgressSignature = storyProgressSignature;
        bool previousBuilt = storyPageBuilt;
        var previousChildren = new HashSet<Transform>();
        for (int i = 0; i < page.childCount; i++)
            previousChildren.Add(page.GetChild(i));

        try
        {
            BuildStoryPageInternal(page, refresh);
        }
        catch (Exception exception)
        {
            // BuildStoryPageInternal creates the replacement tree before it
            // removes the previous one. If TMP or a definition lookup fails,
            // discard only the incomplete replacement and keep the last
            // readable archive visible.
            for (int i = page.childCount - 1; i >= 0; i--)
            {
                Transform child = page.GetChild(i);
                if (previousChildren.Contains(child))
                    continue;
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
            storyPageStateSignature = previousSignature;
            storyProgressSignature = previousProgressSignature;
            storyPageBuilt = previousBuilt;
            if (!previousBuilt)
                BuildStoryFailureCard(page);
            Debug.LogException(exception);
        }
    }

    private void BuildStoryFailureCard(RectTransform page)
    {
        if (page == null || page.Find("StoryBuildError") != null)
            return;

        try
        {
            RectTransform surface = CreatePanel(page, "StoryBuildError", PanelRaised);
            SetTop(surface, 0f, 260f);
            float width = Mathf.Max(480f, pageHost == null ? 1200f : pageHost.rect.width);
            CreateCard(surface, "StoryBuildErrorCard", "剧情档案暂时不可用",
                "剧情页面初始化遇到问题。王国模拟仍在运行，请查看 Console 中的剧情错误日志。",
                PanelRaised, width, 24f, 220f);
            surface.sizeDelta = new Vector2(0f, 284f);
            page.sizeDelta = new Vector2(0f, 284f);
        }
        catch (Exception fallbackException)
        {
            Debug.LogException(fallbackException);
        }
    }

    private void BuildStoryPageInternal(RectTransform page, bool refresh)
    {
        if (page == null)
            return;

        TutorialManager tutorial = TutorialManager.Current;
        TechLevel era = GameManager.Instance == null || GameManager.Instance.State == null
            ? TechLevel.Animal
            : GameManager.Instance.State.TechLevel;
        string progressSignature = StoryManager.GetProgressSignature(era, tutorial);
        string quickSignature = GetStoryStateSignature(progressSignature);
        if (!refresh && storyPageBuilt && quickSignature == storyPageStateSignature)
            return;

        storyPageStateSignature = quickSignature;
        storyPageBuilt = false;
        storyBodyTextCache.Clear();
        ObserveStoryProgress(era, tutorial, out int unlockedCount,
            out StoryChapter latestChapter);

        RectTransform surface = CreatePanel(page, "StoryOverviewPage", Background);
        SetTop(surface, 0f, 1400f);
        float width = Mathf.Max(480f, pageHost == null ? 1200f : pageHost.rect.width);
        float y = 24f;

        y += CreateCard(surface, "StoryOverviewHeader", "剧情档案\n鼠族文明复兴",
            "这里记录鼠族从灾变后的第一簇火种，到重新掌握机器与星海的文明记忆。",
            PanelRaised, width, y, 190f);
        y += 18f;
        y += CreateCard(surface, "StoryOverviewState",
            "记忆档案状态",
            "已唤醒记忆：" + unlockedCount +
            "/" + StoryManager.Chapters.Count +
            "\n最新记忆记录王国刚刚完成的一项真实行动。",
            Panel, width, y, 150f);
        y += 18f;
        y += CreateCard(surface, "StoryOverviewMeaning", "最新文明记忆",
            BuildStoryMeaningText(latestChapter), Panel, width, y, 180f);

        for (int i = 0; i < StoryManager.Chapters.Count; i++)
        {
            StoryChapter chapter = StoryManager.Chapters[i];
            if (chapter == null)
                continue;

            y += 18f;
            bool unlocked = StoryManager.IsUnlocked(chapter, era, tutorial);
            bool isLatest = unlocked && chapter == latestChapter;
            bool collapsed = unlocked && !isLatest &&
                IsStoryChapterCollapsed(chapter.Id);
            string chapterTitle = chapter.Title + "\n" + chapter.EraLabel;
            string chapterBody;
            if (unlocked)
            {
                chapterBody = collapsed
                    ? chapter.Summary
                    : chapter.Summary + "\n\n" + chapter.Body;
            }
            else
            {
                chapterTitle += " · 尚未唤醒";
                chapterBody = "这段文明记忆尚未回到王国。\n" +
                    StoryManager.GetChapterProgressHint(chapter, era);
            }

            y += CreateCard(surface, "StoryChapter_" + chapter.Id,
                chapterTitle, chapterBody,
                isLatest ? Copper : unlocked ? PanelRaised : Panel,
                width, y, unlocked && !isLatest ? 140f : unlocked ? 220f : 150f,
                unlocked && !isLatest ? chapter.Id : null,
                isLatest,
                GetStoryNavigationPage(chapter),
                GetStoryNavigationTarget(chapter));
        }

        float height = Mathf.Max(pageHost == null ? 720f : pageHost.rect.height, y + 24f);
        surface.sizeDelta = new Vector2(0f, height);
        page.anchorMin = new Vector2(0f, 1f);
        page.anchorMax = new Vector2(1f, 1f);
        page.pivot = new Vector2(.5f, 1f);
        page.sizeDelta = new Vector2(0f, height);
        ClearNewStoryPageExcept(page, surface);
        storyProgressSignature = progressSignature;
        storyPageBuilt = true;
        Debug.Log("[王国剧情] New Overview-style Story shell rendered: era=" +
            era + ", unlocked=" + unlockedCount +
            ", height=" + height);
    }

    private string GetStoryStateSignature(string progressSignature)
    {
        return progressSignature +
            "|feedback:" + (tutorialRecentCompletionFeedback ?? string.Empty) +
            "|action:" + (recentActionFeedback ?? string.Empty);
    }

    private string BuildStoryMeaningText(StoryChapter latestChapter)
    {
        string actionFeedback = string.IsNullOrWhiteSpace(
            tutorialRecentCompletionFeedback)
            ? string.Empty
            : "\n刚刚改变：" + tutorialRecentCompletionFeedback;
        if (!string.IsNullOrWhiteSpace(recentActionFeedback))
            actionFeedback += "\n刚刚发生：" + recentActionFeedback;
        string meaning = latestChapter == null
            ? "完成资源、建筑、研究和生产链行动后，这里会记录它们对鼠族文明复兴的意义。"
            : "最新唤醒：“" + latestChapter.Title + "”。\n完整的文明意义与正文见下方章节。";
        return meaning + actionFeedback;
    }

    private static void ClearNewStoryPageExcept(RectTransform page,
        RectTransform keep)
    {
        for (int i = page.childCount - 1; i >= 0; i--)
        {
            if (page.GetChild(i) == keep)
                continue;
            // Destroy is deferred until the end of the frame. Disable the
            // old tree first so a progress refresh never renders the old and
            // new TMP card trees together for one frame.
            GameObject child = page.GetChild(i).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
    }

    private static RectTransform CreatePanel(Transform parent, string name, Color color)
    {
        GameObject objectToCreate = new GameObject(name, typeof(RectTransform), typeof(Image));
        objectToCreate.transform.SetParent(parent, false);
        Image image = objectToCreate.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return objectToCreate.GetComponent<RectTransform>();
    }

    private static void SetTop(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -y);
        rect.sizeDelta = new Vector2(-24f, height);
    }

    private float CreateCard(RectTransform parent, string name, string title,
        string body, Color color, float width, float y, float minimumHeight,
        string toggleChapterId = null, bool addNavigationButton = false,
        string navigationPage = null, string navigationTargetId = null)
    {
        RectTransform card = CreatePanel(parent, name, color);
        SetTop(card, y, minimumHeight);
        bool hasTopRightAction = !string.IsNullOrEmpty(toggleChapterId) ||
            addNavigationButton;
        float cardWidth = card.rect.width > 1f
            ? card.rect.width
            : Mathf.Max(320f, width - 48f);
        float bodyWidth = Mathf.Max(320f, cardWidth - 32f);
        float titleWidth = hasTopRightAction
            ? Mathf.Max(240f, cardWidth - 330f)
            : bodyWidth;
        TMP_Text titleText = CreateText(card, "Title", title, TextPrimary,
            TextAnchor.UpperLeft,
            titleWidth,
            true);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchoredPosition = new Vector2(16f, -16f);
        float titleHeight = Mathf.Max(48f, titleText.preferredHeight);
        titleRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, titleHeight);
        TMP_Text bodyText = CreateText(card, "Body", body, TextPrimary,
            TextAnchor.UpperLeft, bodyWidth, false);
        bodyText.rectTransform.anchoredPosition =
            new Vector2(16f, -(16f + titleHeight + 18f));
        float bodyHeight = Mathf.Max(48f, bodyText.preferredHeight);
        bodyText.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, bodyHeight);
        // Story cards may extend beyond the outer ScrollRect while still
        // needing valid TMP geometry for the next scroll position.
        float height = Mathf.Max(minimumHeight,
            16f + titleHeight + 18f + bodyHeight + 16f);
        card.sizeDelta = new Vector2(-24f, height);
        if (!string.IsNullOrEmpty(toggleChapterId))
            AddStoryChapterToggle(card, toggleChapterId,
                IsStoryChapterCollapsed(toggleChapterId) ? "展开" : "收起");
        else if (addNavigationButton)
            AddStoryChapterNavigation(card, navigationPage, navigationTargetId);
        return height;
    }

    private TMP_Text CreateText(RectTransform parent, string name, string value,
        Color color, TextAnchor alignment, float width, bool bold)
    {
        GameObject objectToCreate = new GameObject(name,
            typeof(RectTransform), typeof(TextMeshProUGUI));
        objectToCreate.transform.SetParent(parent, false);
        objectToCreate.name = name;
        objectToCreate.SetActive(true);
        RectTransform rect = objectToCreate.GetComponent<RectTransform>();
        // Story cards are positioned manually. Keep text rectangles fixed to
        // the card's top-left instead of stretching them across the parent;
        // this makes preferred-height measurement independent of a later
        // parent resize and prevents TMP glyphs from being displaced by the
        // parent's anchor calculation.
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(Mathf.Max(320f, width), 80f);
        TMP_Text text = objectToCreate.GetComponent<TMP_Text>();
        text.enabled = true;
        TMP_FontAsset requestedFont = sharedFontAsset != null
            ? sharedFontAsset
            : TMP_Settings.defaultFontAsset;
        Material requestedMaterial = requestedFont == null
            ? null
            : requestedFont.material;
        if (requestedFont != null && requestedMaterial != null &&
            requestedMaterial.shader != null)
        {
            text.font = requestedFont;
            text.fontSharedMaterial = requestedMaterial;
        }
        else if (requestedFont != null)
        {
            text.font = requestedFont;
        }
        text.fontSize = 30;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.color = color;
        text.alignment = alignment == TextAnchor.MiddleCenter
            ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.text = value ?? string.Empty;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            Mathf.Max(320f, width));
        text.ForceMeshUpdate(true, true);
        return text;
    }

    private void ToggleStoryChapter(string chapterId)
    {
        if (string.IsNullOrEmpty(chapterId))
            return;
        storyChapterCollapsed[chapterId] = !IsStoryChapterCollapsed(chapterId);
    }

    private bool IsStoryChapterCollapsed(string chapterId)
    {
        return string.IsNullOrEmpty(chapterId) ||
            !storyChapterCollapsed.TryGetValue(chapterId, out bool collapsed) ||
            collapsed;
    }

    private void AddStoryChapterToggle(RectTransform card, string chapterId,
        string label)
    {
        GameObject buttonObject = new GameObject("StoryChapterToggle",
            typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(card, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-4f, -4f);
        buttonRect.sizeDelta = new Vector2(240f, 60f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = Copper;
        image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ApplyButtonColors(button, image.color);
        TMP_Text labelText = CreateText(buttonRect, "Label", label, TextPrimary,
            TextAnchor.MiddleCenter, 220f, true);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(.5f, .5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = Vector2.zero;
        labelText.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(() =>
        {
            UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Detail);
            ToggleStoryChapterInPlace(card, chapterId);
        });
    }

    private void ToggleStoryChapterInPlace(RectTransform card, string chapterId)
    {
        if (card == null || string.IsNullOrEmpty(chapterId))
            return;

        StoryChapter chapter = null;
        for (int i = 0; i < StoryManager.Chapters.Count; i++)
            if (StoryManager.Chapters[i] != null &&
                StoryManager.Chapters[i].Id == chapterId)
            {
                chapter = StoryManager.Chapters[i];
                break;
            }
        if (chapter == null)
            return;

        ToggleStoryChapter(chapterId);
        bool collapsed = IsStoryChapterCollapsed(chapterId);
        TMP_Text body = card.Find("Body")?.GetComponent<TMP_Text>();
        TMP_Text title = card.Find("Title")?.GetComponent<TMP_Text>();
        if (body == null || title == null)
            return;

        body.text = collapsed
            ? chapter.Summary
            : chapter.Summary + "\n\n" + chapter.Body;
        ResizeStoryCard(card, title, body, collapsed ? 140f : 220f);

        TMP_Text label = card.Find("StoryChapterToggle/Label")?.GetComponent<TMP_Text>();
        if (label != null)
            label.text = collapsed ? "展开" : "收起";
    }

    private void ResizeStoryCard(RectTransform card, TMP_Text title,
        TMP_Text body, float minimumHeight)
    {
        if (card == null || title == null || body == null)
            return;
        body.ForceMeshUpdate(true, true);
        float titleHeight = Mathf.Max(48f, title.preferredHeight);
        float bodyHeight = Mathf.Max(48f, body.preferredHeight);
        body.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, bodyHeight);
        float oldHeight = card.rect.height > 0f
            ? card.rect.height : card.sizeDelta.y;
        float newHeight = Mathf.Max(minimumHeight,
            16f + titleHeight + 18f + bodyHeight + 16f);
        card.sizeDelta = new Vector2(card.sizeDelta.x, newHeight);

        float delta = newHeight - oldHeight;
        if (Mathf.Approximately(delta, 0f) || card.parent == null)
            return;

        int cardIndex = card.GetSiblingIndex();
        Transform surface = card.parent;
        for (int i = cardIndex + 1; i < surface.childCount; i++)
        {
            RectTransform following = surface.GetChild(i) as RectTransform;
            if (following != null)
                following.anchoredPosition += new Vector2(0f, -delta);
        }

        RectTransform surfaceRect = surface as RectTransform;
        if (surfaceRect != null)
            surfaceRect.sizeDelta = new Vector2(surfaceRect.sizeDelta.x,
                surfaceRect.sizeDelta.y + delta);
        RectTransform page = surface.parent as RectTransform;
        if (page != null)
            page.sizeDelta = new Vector2(page.sizeDelta.x,
                page.sizeDelta.y + delta);
    }

    private bool RefreshStoryMeaningInPlace(RectTransform page,
        TechLevel era, TutorialManager tutorial, string progressSignature)
    {
        Transform surface = page.Find("StoryOverviewPage");
        RectTransform card = surface?.Find("StoryOverviewMeaning") as RectTransform;
        TMP_Text body = card?.Find("Body")?.GetComponent<TMP_Text>();
        TMP_Text title = card?.Find("Title")?.GetComponent<TMP_Text>();
        if (card == null || body == null || title == null)
            return false;

        body.text = BuildStoryMeaningText(
            StoryManager.FindLatestUnlocked(era, tutorial));
        ResizeStoryCard(card, title, body, 180f);
        Canvas.ForceUpdateCanvases();
        storyPageStateSignature = GetStoryStateSignature(progressSignature);
        return true;
    }

    private void AddStoryChapterNavigation(RectTransform card,
        string pageName, string targetId)
    {
        GameObject buttonObject = new GameObject("StoryChapterNavigation",
            typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(card, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-4f, -4f);
        buttonRect.sizeDelta = new Vector2(240f, 60f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = PanelRaised;
        image.raycastTarget = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ApplyButtonColors(button, image.color);
        TMP_Text label = CreateText(buttonRect, "Label", "前往相关页",
            TextPrimary, TextAnchor.MiddleCenter, 220f, true);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(.5f, .5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = Vector2.zero;
        label.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(() => NavigateToStoryTarget(
            pageName, targetId));
        buttonObject.AddComponent<UIPageScrollDragForwarder>();
    }

    private string GetStoryNavigationPage(StoryChapter chapter)
    {
        if (chapter == null)
            return "Overview";
        if (!string.IsNullOrEmpty(chapter.RequiredWorkshopId) ||
            chapter.RequiresWorkshopPurchase)
            return "Workshop";
        if (!string.IsNullOrEmpty(GetBlockingWorkshopId(chapter.RequiredBuildingId)))
            return "Workshop";
        if (!string.IsNullOrEmpty(chapter.RequiredBuildingId))
            return "Buildings";
        if (!string.IsNullOrEmpty(chapter.RequiredResearchId))
            return "Research";
        if (chapter.RequiresSectorAccess || chapter.RequiresCampaignProgress ||
            chapter.RequiresSectorOccupation)
            return "Sectors";
        return "Overview";
    }

    private string GetStoryNavigationTarget(StoryChapter chapter)
    {
        if (chapter == null)
            return string.Empty;
        if (!string.IsNullOrEmpty(chapter.RequiredWorkshopId))
            return chapter.RequiredWorkshopId;
        string blockingWorkshopId = GetBlockingWorkshopId(
            chapter.RequiredBuildingId);
        if (!string.IsNullOrEmpty(blockingWorkshopId))
            return blockingWorkshopId;
        if (!string.IsNullOrEmpty(chapter.RequiredBuildingId))
            return chapter.RequiredBuildingId;
        if (!string.IsNullOrEmpty(chapter.RequiredResearchId))
            return chapter.RequiredResearchId;
        return string.Empty;
    }

    private static string GetBlockingWorkshopId(string buildingId)
    {
        if (string.IsNullOrEmpty(buildingId) ||
            !DataBase<Building>.TryFind(buildingId, out Building building) ||
            building == null || building.RequiredWorkshopUpgrades == null)
            return string.Empty;

        // WorkshopManager is not required before the Industrial era. Story
        // still renders locked future chapters in earlier eras, so this is a
        // non-authoritative lookup rather than the strict Singleton accessor.
        WorkshopManager workshopManager =
            UnityEngine.Object.FindObjectOfType<WorkshopManager>();
        for (int i = 0; i < building.RequiredWorkshopUpgrades.Count; i++)
        {
            WorkshopUpgrade upgrade = building.RequiredWorkshopUpgrades[i];
            if (upgrade != null &&
                (workshopManager == null || !workshopManager.IsPurchased(upgrade)))
                return upgrade.Id;
        }
        return string.Empty;
    }

    private void NavigateToStoryTarget(string pageName, string targetId)
    {
        if (string.IsNullOrEmpty(pageName) || !pages.ContainsKey(pageName))
            return;
        SetPage(pageName);
        if (string.IsNullOrEmpty(targetId))
            return;
        if (pageName == "Buildings" &&
            DataBase<Building>.TryFind(targetId, out Building building) &&
            building != null)
        {
            ShowBuildingDetails(building);
            return;
        }
        if (pageName == "Research" &&
            DataBase<Research>.TryFind(targetId, out Research research) &&
            research != null)
        {
            ShowResearchDetails(research);
            return;
        }
        if (pageName == "Workshop" &&
            DataBase<WorkshopUpgrade>.TryFind(targetId,
                out WorkshopUpgrade workshop) && workshop != null)
            ShowWorkshopDetails(workshop);
    }

    private void RefreshStoryPageIfChanged()
    {
        if (populatedPage != "Story" || !pages.TryGetValue("Story", out RectTransform page))
            return;
        bool preserveScroll = pageScroll != null && pageScroll.content == page;
        float previousScroll = preserveScroll
            ? pageScroll.verticalNormalizedPosition : 1f;
        string previous = storyPageStateSignature;
        bool wasBuilt = storyPageBuilt;
        TutorialManager tutorial = TutorialManager.Current;
        TechLevel era = GameManager.Instance == null || GameManager.Instance.State == null
            ? TechLevel.Animal
            : GameManager.Instance.State.TechLevel;
        string progressSignature = StoryManager.GetProgressSignature(era, tutorial);
        string currentSignature = GetStoryStateSignature(progressSignature);
        if (wasBuilt && progressSignature == storyProgressSignature &&
            previous != currentSignature && RefreshStoryMeaningInPlace(
                page, era, tutorial, progressSignature))
        {
            if (preserveScroll && pageScroll != null && pageScroll.content == page)
                pageScroll.verticalNormalizedPosition = previousScroll;
            return;
        }
        BuildStoryPage(page, false);
        if (!wasBuilt || previous != storyPageStateSignature)
        {
            bool alreadyBound = pageScroll != null && pageScroll.content == page;
            if (!alreadyBound)
                ConfigureOuterPageScroll("Story", false, true);
            else
                Canvas.ForceUpdateCanvases();
            if (preserveScroll && pageScroll != null && pageScroll.content == page)
                pageScroll.verticalNormalizedPosition = previousScroll;
        }
    }
}
