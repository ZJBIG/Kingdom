using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private void BuildAuthoredMusicPage(RectTransform surface)
    {
        if (surface == null)
            return;
        musicPageBuilt = true;
        musicTrackButtons.Clear();
        RectTransform controls = surface.Find("Controls") as RectTransform;
        if (controls == null || surface.Find("TrackListViewport") == null)
        {
            musicPageBuilt = false;
            Debug.LogError("[KingdomUI] Authored MusicSurface is incomplete; fixed music controls/list are not created at runtime.");
            return;
        }
        musicCurrentLabel = controls?.Find("Current")?.GetComponent<TMP_Text>();
        musicTimeLabel = controls?.Find("Time")?.GetComponent<TMP_Text>();
        musicProgressSlider = controls?.Find("TimeSeek")?.GetComponent<Slider>();
        musicPlayPauseButton = controls?.Find("PlayPause")?.GetComponent<Button>();
        musicVolumeSlider = controls?.Find("Volume")?.GetComponent<Slider>();
        musicGapSlider = controls?.Find("Gap")?.GetComponent<Slider>();
        musicVolumeValueLabel = controls?.Find("VolumeValue")?.GetComponent<TMP_Text>();
        musicGapValueLabel = controls?.Find("GapValue")?.GetComponent<TMP_Text>();
        if (musicProgressSlider != null)
        {
            musicProgressSeekHandler = SeekMusicFromSlider;
            musicProgressSlider.onValueChanged.RemoveAllListeners();
            musicProgressSlider.onValueChanged.AddListener(musicProgressSeekHandler);
            AddPointerStateIfMissing(musicProgressSlider, () => musicProgressDragging = true,
                () => musicProgressDragging = false);
        }
        if (musicVolumeSlider != null)
            AddPointerStateIfMissing(musicVolumeSlider, null, null);
        if (musicGapSlider != null)
            AddPointerStateIfMissing(musicGapSlider, null, null);
        if (musicPlayPauseButton != null)
        {
            musicPlayPauseButton.onClick.RemoveAllListeners();
            musicPlayPauseButton.onClick.AddListener(ToggleMusicPlayback);
            ConfigureMusicButtonText(musicPlayPauseButton, "暂停");
        }
        Button previous = controls?.Find("Previous")?.GetComponent<Button>();
        Button next = controls?.Find("Next")?.GetComponent<Button>();
        Button stop = controls?.Find("Stop")?.GetComponent<Button>();
        if (previous != null) { previous.onClick.RemoveAllListeners(); previous.onClick.AddListener(() => PlayRelativeMusicTrack(-1)); ConfigureMusicButtonText(previous, "上一首"); }
        if (next != null) { next.onClick.RemoveAllListeners(); next.onClick.AddListener(() => PlayRelativeMusicTrack(1)); ConfigureMusicButtonText(next, "下一首"); }
        if (stop != null) { stop.onClick.RemoveAllListeners(); stop.onClick.AddListener(StopMusicPlayback); ConfigureMusicButtonText(stop, "停止"); }
        ConfigureMusicText(controls?.Find("TimeSeekLabel")?.GetComponent<TMP_Text>(), "TIME / SEEK");
        ConfigureMusicText(musicVolumeValueLabel, "音量 100%");
        ConfigureMusicText(musicGapValueLabel, "音乐间隙 5.00");
        ConfigureMusicText(musicCurrentLabel, "正在播放");
        BringMusicTextToFront(musicTimeLabel);
        BringMusicTextToFront(musicVolumeValueLabel);
        BringMusicTextToFront(musicGapValueLabel);
        BringMusicTextToFront(musicCurrentLabel);

        MusicManager manager = FindMusicManager();
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveAllListeners();
            musicVolumeSlider.onValueChanged.AddListener(value =>
            {
                if (manager != null)
                    manager.SetVolume(value);
            });
        }
        if (musicGapSlider != null)
        {
            musicGapSlider.onValueChanged.RemoveAllListeners();
            musicGapSlider.onValueChanged.AddListener(value =>
            {
                if (manager != null)
                    manager.SetGapSeconds(value);
            });
        }
        if (manager != null && manager.Tracks.Count == 0)
            manager.RebuildCatalog();
        BuildAuthoredMusicRows(surface.Find("TrackListViewport") as RectTransform, manager);
    }

    private void AddPointerStateIfMissing(Slider slider, UnityEngine.Events.UnityAction down,
        UnityEngine.Events.UnityAction up)
    {
        if (slider == null || slider.GetComponent<EventTrigger>() != null)
            return;
        AddPointerState(slider, down, up);
    }

    private void BuildAuthoredMusicRows(RectTransform viewport, MusicManager manager)
    {
        if (viewport == null)
            return;
        musicListScroll = viewport.GetComponent<ScrollRect>();
        if (musicListScroll == null)
            musicListScroll = viewport.gameObject.AddComponent<ScrollRect>();
        musicListScroll.viewport = viewport;
        musicListScroll.horizontal = false;
        musicListScroll.vertical = true;
        musicListScroll.movementType = ScrollRect.MovementType.Clamped;
        RectTransform content = viewport.Find("TrackList") as RectTransform;
        if (content == null)
        {
            Debug.LogError("[KingdomUI] Authored Music TrackList content is missing from TrackListViewport.");
            return;
        }
        musicListScroll.content = content;
        musicTrackList = content;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        List<MusicManager.MusicTrack> tracks = BuildMusicDisplayTracks(manager);
        float contentHeight = 0f;
        for (int i = 0; i < tracks.Count; i++)
        {
            MusicManager.MusicTrack track = tracks[i];
            GameObject rowObject = KingdomUIPrefabLibrary.Instantiate(KingdomUIPrefabLibrary.MusicTrack, content);
            if (rowObject == null)
                continue;
            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            float rowHeight = Mathf.Max(1f, rowRect.sizeDelta.y);
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(.5f, 1f);
            rowRect.sizeDelta = new Vector2(0f, rowHeight);
            rowRect.anchoredPosition = new Vector2(0f, -contentHeight);
            Image surfaceImage = rowObject.GetComponent<Image>();
            if (surfaceImage == null)
            {
                Debug.LogError("[KingdomUI] MusicTrack prefab is missing its authored row Image: " + track.Id);
                Destroy(rowObject);
                continue;
            }
            surfaceImage.color = i % 2 == 0 ? ListRowEven : ListRowOdd;
            Button row = rowObject.GetComponent<Button>();
            if (row == null)
            {
                Debug.LogError("[KingdomUI] MusicTrack prefab is missing its authored Button: " + track.Id);
                Destroy(rowObject);
                continue;
            }
            row.targetGraphic = surfaceImage;
            row.transition = Selectable.Transition.ColorTint;
            ApplyButtonColors(row, surfaceImage.color);
            row.onClick.AddListener(() => manager?.PlayTrack(track));
            ConfigureMusicTrackColumn(rowObject, "Label", track.Label, Vector2.zero, new Vector2(.58f, 1f), new Vector2(12, 4), new Vector2(-8, -4), TextAlignmentOptions.MidlineLeft);
            ConfigureMusicTrackColumn(rowObject, "Length", FormatMusicTime(track.Clip == null ? 0f : track.Clip.length), new Vector2(.58f, 0), new Vector2(.78f, 1), new Vector2(0, 4), new Vector2(0, -4), TextAlignmentOptions.Center);
            ConfigureMusicTrackColumn(rowObject, "Type", track.Category, new Vector2(.78f, 0), Vector2.one, new Vector2(8, 4), new Vector2(-12, -4), TextAlignmentOptions.MidlineRight);
            if (rowObject.GetComponent<UIPageScrollDragForwarder>() == null)
            {
                Debug.LogError("[KingdomUI] MusicTrack prefab is missing its authored drag forwarder: " + track.Id);
                Destroy(rowObject);
                continue;
            }
            musicTrackButtons[track.Id] = row;
            contentHeight += rowHeight;
        }
        content.pivot = new Vector2(.5f, 1f);
        content.sizeDelta = new Vector2(0f, Mathf.Max(120f, contentHeight));
        content.anchoredPosition = Vector2.zero;
        content.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        musicListScroll.StopMovement();
        musicListScroll.verticalNormalizedPosition = 1f;
    }

    /// <summary>
    /// Binds the authored shell stored in KingdomUIRoot.prefab. The shell is
    /// deliberately limited to stable layout and page hosts; definition
    /// rows, graph nodes, connectors and music tracks remain repeatable
    /// prefab instances created by their data presenters.
    /// </summary>
    private bool TryBindAuthoredShell()
    {
        safeArea = transform.Find("SafeAreaRoot") as RectTransform;
        if (safeArea == null)
            return false;

        Transform content = safeArea.Find("Content");
        pageHost = content == null ? null : content.Find("PageHost") as RectTransform;
        pageTitle = content == null ? null : content.Find("PageTitle")?.GetComponent<TMP_Text>();
        leftNavigation = safeArea.Find("LeftNavigation") as RectTransform;
        detailPanel = safeArea.Find("DetailPanel") as RectTransform;

        if (pageHost == null || pageTitle == null || leftNavigation == null || detailPanel == null)
            return false;

        pages.Clear();
        string[] pageNames = { "Overview", "Resources", "Buildings", "Research", "Era", "Workshop", "Music", "Sectors" };
        for (int i = 0; i < pageNames.Length; i++)
        {
            RectTransform page = pageHost.Find(pageNames[i]) as RectTransform;
            if (page == null)
                return false;
            pages[pageNames[i]] = page;
            Button navigationButton = leftNavigation.Find("Nav_" + pageNames[i])?.GetComponent<Button>();
            if (navigationButton != null)
            {
                string pageName = pageNames[i];
                navigationButton.onClick.RemoveAllListeners();
                navigationButton.onClick.AddListener(() => SetPage(pageName));
            }
        }

        pageScroll = pageHost.GetComponent<ScrollRect>();
        if (pageScroll == null)
            pageScroll = pageHost.gameObject.AddComponent<ScrollRect>();
        pageScroll.viewport = pageHost;
        pageScroll.horizontal = false;
        pageScroll.vertical = true;
        pageScroll.movementType = ScrollRect.MovementType.Clamped;

        topKingdomTitle = safeArea.Find("TopStatusBar/Title")?.GetComponent<TMP_Text>();
        topStatus = safeArea.Find("TopStatusBar/Status")?.GetComponent<TMP_Text>();
        detailBody = detailPanel.Find("Body")?.GetComponent<TMP_Text>();
        flowHost = detailPanel.Find("BuildingOutput") as RectTransform;
        flowScroll = flowHost == null ? null : flowHost.GetComponent<ScrollRect>();
        flowContent = flowHost == null ? null : flowHost.Find("FlowContent") as RectTransform;
        requirementHost = detailPanel.Find("BuildingRequirements") as RectTransform;
        requirementScroll = requirementHost == null ? null : requirementHost.GetComponent<ScrollRect>();
        requirementContent = requirementHost == null ? null : requirementHost.Find("RequirementContent") as RectTransform;
        detailPaymentButton = detailPanel.Find("Payment")?.GetComponent<Button>();
        detailActionButton = detailPanel.Find("Action")?.GetComponent<Button>();
        ConfigureDetailScroll();
        buildingQuantityControls = content.Find("BuildingQuantityControls") as RectTransform;
        buildingPageTitle = content.Find("BuildingPageTitle")?.GetComponent<TMP_Text>();
        tooltipPanel = safeArea.Find("Tooltip") as RectTransform;
        tooltipText = tooltipPanel == null ? null : tooltipPanel.Find("Text")?.GetComponent<TMP_Text>();

        // These are stable scene-owned objects. Their interaction components
        // are repaired only when a scene author accidentally removes one.
        EnsureAuthoredViewport(pageHost, pageScroll, true);
        if (requirementScroll != null)
            requirementScroll.enabled = false;
        if (flowScroll != null)
            flowScroll.enabled = false;
        if (requirementHost != null && requirementHost.GetComponent<RectMask2D>() != null)
            requirementHost.GetComponent<RectMask2D>().enabled = false;
        if (flowHost != null && flowHost.GetComponent<RectMask2D>() != null)
            flowHost.GetComponent<RectMask2D>().enabled = false;
        Debug.Log($"[KingdomUI] Detail scroll configured: owner=DetailScrollViewport, viewport={detailScrollViewport.rect.size}, content={detailScrollContent.rect.size}, nestedRequirementScroll={requirementScroll != null && requirementScroll.enabled}, nestedFlowScroll={flowScroll != null && flowScroll.enabled}");
        if (requirementHost != null && requirementContent != null)
        {
            // The requirement rows are nested under the unified content, but
            // the host itself is not guaranteed to be the raycast target.
            // Own the gesture on the transparent, raycastable viewport so a
            // drag starting on an icon or TMP label always reaches it.
            requirementGesture = detailScrollViewport.GetComponent<UIDetailRequirementScrollGesture>();
            if (requirementGesture == null)
                requirementGesture = detailScrollViewport.gameObject.AddComponent<UIDetailRequirementScrollGesture>();
            requirementGesture.Initialize(detailScrollViewport, detailScrollContent);
            // The custom gesture is the single owner. ScrollRect would win
            // ExecuteHierarchy on the same viewport and leave row-started
            // drags dependent on component ordering.
            if (detailScroll != null)
                detailScroll.enabled = false;
            requirementGesture.enabled = true;
        }

        return true;
    }

    private void ConfigureDetailScroll()
    {
        if (detailPanel == null)
            return;
        detailScrollViewport = detailPanel.Find("DetailScrollViewport") as RectTransform;
        if (detailScrollViewport == null)
        {
            GameObject viewportObject = new GameObject("DetailScrollViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            detailScrollViewport = viewportObject.GetComponent<RectTransform>();
            detailScrollViewport.SetParent(detailPanel, false);
        }
        detailScrollViewport.anchorMin = Vector2.zero;
        detailScrollViewport.anchorMax = Vector2.one;
        detailScrollViewport.offsetMin = Vector2.zero;
        detailScrollViewport.offsetMax = new Vector2(0f, -182f);
        PlaceDetailScrollViewportInFrontOfBackground();
        Image viewportImage = detailScrollViewport.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewportImage.raycastTarget = true;

        detailScrollContent = detailScrollViewport.Find("DetailScrollContent") as RectTransform;
        if (detailScrollContent == null)
        {
            GameObject contentObject = new GameObject("DetailScrollContent", typeof(RectTransform));
            detailScrollContent = contentObject.GetComponent<RectTransform>();
            detailScrollContent.SetParent(detailScrollViewport, false);
        }
        detailScrollContent.anchorMin = new Vector2(0f, 1f);
        detailScrollContent.anchorMax = new Vector2(1f, 1f);
        detailScrollContent.pivot = new Vector2(.5f, 1f);
        detailScrollContent.anchoredPosition = Vector2.zero;
        detailScrollContent.sizeDelta = new Vector2(0f, Mathf.Max(1f, detailScrollViewport.rect.height));
        ReparentDetailElement(detailBody == null ? null : detailBody.transform, detailScrollContent);
        ReparentDetailElement(flowHost, detailScrollContent);
        ReparentDetailElement(requirementHost, detailScrollContent);

        detailScroll = detailScrollViewport.GetComponent<ScrollRect>();
        detailScroll.viewport = detailScrollViewport;
        detailScroll.content = detailScrollContent;
        detailScroll.horizontal = false;
        detailScroll.vertical = true;
        detailScroll.movementType = ScrollRect.MovementType.Clamped;
        detailScroll.inertia = true;
    }

    private static void ReparentDetailElement(Transform element, RectTransform parent)
    {
        if (element == null || parent == null || element.parent == parent)
            return;
        element.SetParent(parent, false);
    }

    private void PlaceDetailScrollViewportInFrontOfBackground()
    {
        if (detailPanel == null || detailScrollViewport == null)
            return;

        int backgroundIndex = -1;
        foreach (string backgroundName in new[] { "Surface", "Accent" })
        {
            Transform background = detailPanel.Find(backgroundName);
            if (background != null)
                backgroundIndex = Mathf.Max(backgroundIndex, background.GetSiblingIndex());
        }

        int targetIndex = Mathf.Clamp(backgroundIndex + 1, 0, detailPanel.childCount - 1);
        detailScrollViewport.SetSiblingIndex(targetIndex);
        Debug.Log($"[KingdomUI] Detail scroll layer order: backgroundIndex={backgroundIndex}, viewportIndex={detailScrollViewport.GetSiblingIndex()}");
    }

    private void RefreshDetailScrollGeometry()
    {
        if (detailScrollViewport == null || detailScrollContent == null)
            return;

        float viewportHeight = detailScrollViewport.rect.height;
        if (viewportHeight <= 0f)
            return;

        // ConfigureDetailScroll runs during Awake, before the safe-area and
        // side-panel geometry exists. Repair the initial negative/zero
        // content height once the real viewport has been measured.
        float contentHeight = Mathf.Max(viewportHeight, detailScrollContent.rect.height);
        detailScrollContent.sizeDelta = new Vector2(0f, contentHeight);
        if (detailScroll != null)
        {
            detailScroll.viewport = detailScrollViewport;
            detailScroll.content = detailScrollContent;
            detailScroll.horizontal = false;
            detailScroll.vertical = true;
        }
        Debug.Log($"[KingdomUI] Detail scroll geometry repaired: viewport={detailScrollViewport.rect.size}, content={detailScrollContent.rect.size}");
    }

    private static void EnsureAuthoredViewport(RectTransform viewport, ScrollRect scroll,
        bool raycast)
    {
        if (viewport == null || scroll == null)
            return;
        Image image = viewport.GetComponent<Image>();
        if (image == null)
            image = viewport.gameObject.AddComponent<Image>();
        image.raycastTarget = raycast;
        if (viewport.GetComponent<RectMask2D>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
    }
}
