using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class KingdomUIRoot
{
    private string pageScrollDiagnosticSignature;

    private void SaveCurrentPagePosition()
    {
        if (string.IsNullOrEmpty(populatedPage))
            return;

        if (populatedPage == "Research")
        {
            if (researchGraphContent != null)
            {
                researchGraphPosition = researchGraphContent.anchoredPosition;
                researchGraphScale = researchGraphContent.localScale.x;
                researchGraphPositionCached = true;
            }
            return;
        }

        if (populatedPage == "Music")
        {
            if (musicListScroll != null)
            {
                musicListScrollPosition = musicListScroll.verticalNormalizedPosition;
                musicListScrollPositionCached = true;
            }
            return;
        }

        if (pageScroll == null || pageScroll.content == null ||
            !pages.TryGetValue(populatedPage, out RectTransform page) ||
            pageScroll.content != page)
            return;

        pageScrollPositions[populatedPage] =
            Mathf.Clamp01(pageScroll.verticalNormalizedPosition);
    }

    private void RestoreSpecialPagePosition(string pageName)
    {
        if (pageName == "Research")
        {
            if (researchGraphPositionCached && researchGraphContent != null)
            {
                researchGraphGesture?.RestoreView(
                    researchGraphPosition, researchGraphScale);
            }
            return;
        }

        if (pageName == "Music" && musicListScroll != null &&
            musicListScrollPositionCached)
            musicListScroll.verticalNormalizedPosition =
                Mathf.Clamp01(musicListScrollPosition);
    }

    private void ConfigureOuterPageScroll(string pageName, bool resetPosition,
        bool rebuildBounds = false)
    {
        if (pageScroll == null)
        {
            FailRequiredUiBinding("SafeAreaRoot/Content/PageHost/ScrollRect");
            return;
        }
        if (pageHost == null)
        {
            FailRequiredUiBinding("SafeAreaRoot/Content/PageHost");
            return;
        }
        if (!pages.TryGetValue(pageName, out RectTransform content) ||
            content == null)
        {
            FailRequiredUiBinding("SafeAreaRoot/Content/PageHost/" + pageName);
            return;
        }

        bool enabled = pageName != "Research" && pageName != "Music";
        pageScroll.StopMovement();
        pageScroll.viewport = pageHost;
        pageScroll.content = content;
        pageScroll.horizontal = false;
        pageScroll.vertical = enabled;
        pageScroll.movementType = ScrollRect.MovementType.Clamped;
        pageScroll.enabled = enabled;
        // The page presenters assign their final RectTransform size during
        // PopulatePage. Only the post-build call asks for a bounds rebuild;
        // doing this before every page population would add avoidable frame
        // work during tab switches.
        if (rebuildBounds && content.gameObject.activeInHierarchy)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
        }
        if (enabled)
        {
            float position = 1f;
            if (!resetPosition && pageScrollPositions.TryGetValue(pageName,
                out float savedPosition))
                position = Mathf.Clamp01(savedPosition);
            pageScroll.verticalNormalizedPosition = position;
            if (!pageScrollPositions.ContainsKey(pageName))
                pageScrollPositions[pageName] = position;
        }

        Image viewportImage = pageHost.GetComponent<Image>();
        string signature = pageName + ":enabled=" + pageScroll.enabled +
            ":vertical=" + pageScroll.vertical +
            ":viewport=" + pageHost.rect.size +
            ":content=" + content.rect.size +
            ":position=" + content.anchoredPosition +
            ":normalized=" + pageScroll.verticalNormalizedPosition.ToString("0.###") +
            ":raycast=" + (viewportImage != null && viewportImage.raycastTarget) +
            ":eventSystem=" + (EventSystem.current != null);
        if (signature != pageScrollDiagnosticSignature)
        {
            pageScrollDiagnosticSignature = signature;
            Debug.Log("[王国界面] Outer page scroll diagnostic: " + signature);
        }
    }

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
            Debug.LogError("[王国界面] Authored MusicSurface is incomplete; fixed music controls/list are not created at runtime.");
            return;
        }
        musicCurrentCategoryIcon = FindMusicSurfaceChild(surface, "CurrentCategoryIcon")?.GetComponent<Image>();
        musicCurrentPlayName = FindMusicSurfaceChild(surface, "CurrentPlayName")?.GetComponent<TMP_Text>();
        musicTimeLabel = controls?.Find("Time")?.GetComponent<TMP_Text>();
        musicProgressSlider = controls?.Find("TimeSeek")?.GetComponent<Slider>();
        musicVolumeSlider = controls?.Find("Volume")?.GetComponent<Slider>();
        musicGapSlider = controls?.Find("Gap")?.GetComponent<Slider>();
        musicVolumeValueLabel = controls?.Find("VolumeValue")?.GetComponent<TMP_Text>();
        musicGapValueLabel = controls?.Find("GapValue")?.GetComponent<TMP_Text>();
        sfxVolumeSlider = controls?.Find("SfxVolume")?.GetComponent<Slider>();
        sfxMuteButton = controls?.Find("SfxMute")?.GetComponent<Button>();
        sfxVolumeValueLabel = controls?.Find("SfxVolumeValue")?.GetComponent<TMP_Text>();
        if (musicProgressSlider != null)
        {
            lastMusicProgressMaxValue = -1f;
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
        if (sfxVolumeSlider != null)
            AddPointerStateIfMissing(sfxVolumeSlider, null, null);
        Button previous = controls?.Find("Previous")?.GetComponent<Button>();
        Button next = controls?.Find("Next")?.GetComponent<Button>();
        Button pause = controls?.Find("Pause")?.GetComponent<Button>();
        Button legacyStop = controls?.Find("Stop")?.GetComponent<Button>();
        if (pause == null && legacyStop != null)
        {
            legacyStop.gameObject.name = "Pause";
            pause = legacyStop;
        }
        if (previous != null) { previous.onClick.RemoveAllListeners(); previous.onClick.AddListener(() => PlayRelativeMusicTrack(-1)); }
        if (next != null) { next.onClick.RemoveAllListeners(); next.onClick.AddListener(() => PlayRelativeMusicTrack(1)); }
        musicGlobalPauseButton = pause;
        if (pause != null) { pause.onClick.RemoveAllListeners(); pause.onClick.AddListener(ToggleGlobalMusicPause); }
        ConfigureMusicIcon(previous, "previous", false);
        ConfigureMusicIcon(next, "next", false);
        ConfigureMusicIcon(pause, "play", false);

        MusicManager manager = FindMusicManager();
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveAllListeners();
            musicVolumeSlider.onValueChanged.AddListener(value =>
            {
                MusicManager current = FindMusicManager();
                if (current != null)
                    current.SetVolume(value);
            });
        }
        if (musicGapSlider != null)
        {
            musicGapSlider.onValueChanged.RemoveAllListeners();
            musicGapSlider.onValueChanged.AddListener(value =>
            {
                MusicManager current = FindMusicManager();
                if (current != null)
                    current.SetGapSeconds(value);
            });
        }
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider.onValueChanged.AddListener(UIButtonSoundManager.SetVolume);
        }
        if (sfxMuteButton != null)
        {
            sfxMuteButton.onClick.RemoveAllListeners();
            sfxMuteButton.onClick.AddListener(UIButtonSoundManager.ToggleMuted);
        }
        if (manager != null && manager.Tracks.Count == 0)
            manager.RebuildCatalog();
        BuildAuthoredMusicRows(surface.Find("TrackListViewport") as RectTransform, manager);
    }

    private static Transform FindMusicSurfaceChild(Transform surface, string objectName)
    {
        if (surface == null || string.IsNullOrEmpty(objectName))
            return null;
        Transform[] children = surface.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != surface && children[i].name == objectName)
                return children[i];
        }
        Debug.LogError("[王国界面] MusicSurface is missing its authored child: " + objectName);
        return null;
    }


    private void AddPointerStateIfMissing(Slider slider, UnityEngine.Events.UnityAction down,
        UnityEngine.Events.UnityAction up)
    {
        if (slider == null || musicPointerStateSliders.Contains(slider))
            return;
        AddPointerState(slider, down, up);
        musicPointerStateSliders.Add(slider);
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
            Debug.LogError("[王国界面] Authored Music TrackList content is missing from TrackListViewport.");
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
                Debug.LogError("[王国界面] MusicTrack prefab is missing its authored row Image: " + track.Id);
                Destroy(rowObject);
                continue;
            }
            surfaceImage.color = i % 2 == 0 ? ListRowEven : ListRowOdd;
            Button row = rowObject.GetComponent<Button>();
            if (row == null)
            {
                Debug.LogError("[王国界面] MusicTrack prefab is missing its authored Button: " + track.Id);
                Destroy(rowObject);
                continue;
            }
            row.enabled = false;
            row.interactable = false;
            row.targetGraphic = null;
            surfaceImage.raycastTarget = false;
            ConfigureMusicTrackColumn(rowObject, "Label", track.Label, Vector2.zero, new Vector2(.50f, 1f), new Vector2(12, 4), new Vector2(-8, -4), TextAlignmentOptions.MidlineLeft);
            ConfigureMusicTrackColumn(rowObject, "Length", FormatMusicTime(track.DurationSeconds), new Vector2(.50f, 0), new Vector2(.68f, 1), new Vector2(0, 4), new Vector2(0, -4), TextAlignmentOptions.Center);
            ConfigureMusicTrackCategoryIcon(rowObject, track.Category);
            Transform playPauseTransform = rowObject.transform.Find("PlayPause") ?? rowObject.transform.Find("Type");
            if (playPauseTransform == null)
            {
                Debug.LogError("[王国界面] MusicTrack prefab is missing its PlayPause column: " + track.Id);
                Destroy(rowObject);
                continue;
            }
            playPauseTransform.name = "PlayPause";
            RectTransform playPauseRect = playPauseTransform as RectTransform;
            if (playPauseRect != null)
            {
                playPauseRect.anchorMin = new Vector2(.68f, .12f);
                playPauseRect.anchorMax = new Vector2(.82f, .88f);
                playPauseRect.offsetMin = new Vector2(2f, 0f);
                playPauseRect.offsetMax = new Vector2(-2f, 0f);
            }
            Image playPauseImage = playPauseTransform.GetComponent<Image>();
            Button playPause = playPauseTransform.GetComponent<Button>();
            if (playPauseImage == null || playPause == null)
            {
                Debug.LogError("[王国界面] MusicTrack prefab is missing its authored PlayPause Image/Button: " + track.Id);
                Destroy(rowObject);
                continue;
            }
            playPause.targetGraphic = playPauseImage;
            ConfigureMusicTrackIcon(playPause, "play");
            playPause.onClick.RemoveAllListeners();
            playPause.onClick.AddListener(() => ToggleMusicTrack(track));
            if (rowObject.GetComponent<UIPageScrollDragForwarder>() == null)
            {
                Debug.LogError("[王国界面] MusicTrack prefab is missing its authored drag forwarder: " + track.Id);
                Destroy(rowObject);
                continue;
            }
            musicTrackButtons[track.Id] = playPause;
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
            return FailRequiredUiBinding("SafeAreaRoot");

        unsafeAreaTicker = transform.Find("UnsafeAreaTicker")?.GetComponent<UnsafeAreaTicker>();
        if (unsafeAreaTicker == null || !unsafeAreaTicker.IsConfigured)
            return FailRequiredUiBinding("UnsafeAreaTicker (UnsafeAreaTicker)");

        Transform content = safeArea.Find("Content");
        if (content == null)
            return FailRequiredUiBinding("SafeAreaRoot/Content");
        pageHost = content == null ? null : content.Find("PageHost") as RectTransform;
        pageTitle = content == null ? null : content.Find("PageTitle")?.GetComponent<TMP_Text>();
        leftNavigation = safeArea.Find("LeftNavigation") as RectTransform;
        detailPanel = safeArea.Find("DetailPanel") as RectTransform;

        if (pageHost == null)
            return FailRequiredUiBinding("SafeAreaRoot/Content/PageHost");
        if (pageTitle == null)
            return FailRequiredUiBinding("SafeAreaRoot/Content/PageTitle (TMP_Text)");
        if (leftNavigation == null)
            return FailRequiredUiBinding("SafeAreaRoot/LeftNavigation");
        if (detailPanel == null)
            return FailRequiredUiBinding("SafeAreaRoot/DetailPanel");

        pages.Clear();
        string[] pageNames = { "Overview", "Resources", "Buildings", "Research", "Era", "Workshop", "Music", "Sectors", "Story" };
        for (int i = 0; i < pageNames.Length; i++)
        {
            RectTransform page = pageHost.Find(pageNames[i]) as RectTransform;
            if (page == null)
                return FailRequiredUiBinding("SafeAreaRoot/Content/PageHost/" + pageNames[i]);
            pages[pageNames[i]] = page;
            Button navigationButton = FindNavigationButton(pageNames[i]);
            if (navigationButton == null)
            {
                Debug.LogError("[王国界面] Static navigation button is missing: Nav_" + pageNames[i]);
                return false;
            }
            string pageName = pageNames[i];
            navigationButton.onClick.RemoveAllListeners();
            navigationButton.onClick.AddListener(() => SetPage(pageName));
        }
        RefreshNavigationVisibility();

        // Bind the Overview summary while the authored shell is being
        // attached.  This keeps the preview informative even before the
        // first page-population pass and lets the live refresh replace this
        // placeholder as soon as managers finish initializing.
        BuildDevelopmentGuidance(pages["Overview"]);
        BindOverviewNavigationToolbar(content as RectTransform);

        pageScroll = pageHost.GetComponent<ScrollRect>();
        if (pageScroll == null)
            pageScroll = pageHost.gameObject.AddComponent<ScrollRect>();
        pageScroll.viewport = pageHost;
        pageScroll.horizontal = false;
        pageScroll.vertical = true;
        pageScroll.movementType = ScrollRect.MovementType.Clamped;

        Transform topInfo = safeArea.Find("TopStatusBar/TopInfoShell/InfoGrid");
        topKingdomTitle = safeArea.Find("TopStatusBar/Title")?.GetComponent<TMP_Text>();
        topKingdomDate = safeArea.Find("TopStatusBar/Title/Date")?.GetComponent<TMP_Text>();
        topFoodValue = topInfo?.Find("Food/Value")?.GetComponent<TMP_Text>();
        topHappinessValue = topInfo?.Find("Happiness/Value")?.GetComponent<TMP_Text>();
        topPopulationValue = topInfo?.Find("Population/Value")?.GetComponent<TMP_Text>();
        topTerritoryValue = topInfo?.Find("Territory/Value")?.GetComponent<TMP_Text>();
        topResearchPowerValue = topInfo?.Find("ResearchPower/Value")?.GetComponent<TMP_Text>();
        topPowerValue = topInfo?.Find("Power/Value")?.GetComponent<TMP_Text>();
        topLogisticsValue = topInfo?.Find("Logistics/Value")?.GetComponent<TMP_Text>();
        topCurrentResearchValue = topInfo?.Find("CurrentResearch/Value")?.GetComponent<TMP_Text>();
        if (!BuildDetailUI())
            return FailRequiredUiBinding("DetailPanel runtime binding");
        Transform pageTool = content.Find("PageTool");
        if (!BindWorkshopFilters(pageTool))
            return FailRequiredUiBinding("WorkshopFilters under PageTool");
        buildingControls = pageTool?.Find("BuildingControls") as RectTransform;
        if (buildingControls == null)
        {
            Debug.LogError("[王国界面] Authored BuildingControls is missing under SafeAreaRoot/Content/PageTool.");
            return false;
        }
        BuildBuildingControls(pageTool);
        SetupResearchQueueGraphic(buildingControls);
        tooltipPanel = safeArea.Find("Tooltip") as RectTransform;
        tooltipText = tooltipPanel == null ? null : tooltipPanel.Find("Text")?.GetComponent<TMP_Text>();

        // These are stable scene-owned objects. Their interaction components
        // are repaired only when a scene author accidentally removes one.
        EnsureAuthoredViewport(pageHost, pageScroll, true);
        Debug.Log("[王国界面] Detail UI v2 bound; legacy detail hierarchy is inactive.");

        return true;
    }

    private static bool FailRequiredUiBinding(string path)
    {
        Debug.LogError("[王国界面] Required UI component is missing or invalid: " + path);
        return false;
    }

    private void RefreshNavigationVisibility()
    {
        if (leftNavigation == null)
            return;

        bool workshopUnlocked = WorkshopManager.Instance != null &&
            WorkshopManager.Instance.IsSystemUnlocked;
        bool sectorsUnlocked = ProgressionModifierManager.Current.IsSystemUnlocked(
            ResearchSystem.HomeSystemSurvey);

        SetNavigationButtonVisible("Overview", true);
        SetNavigationButtonVisible("Resources", true);
        SetNavigationButtonVisible("Buildings", true);
        SetNavigationButtonVisible("Research", true);
        SetNavigationButtonVisible("Era", true);
        SetNavigationButtonVisible("Workshop", workshopUnlocked);
        SetNavigationButtonVisible("Music", true);
        SetNavigationButtonVisible("Sectors", sectorsUnlocked);
        SetNavigationButtonVisible("Story", true);
    }

    private void RefreshNavigationSelection(string activePage)
    {
        if (leftNavigation == null)
            return;

        string[] pageNames = { "Overview", "Resources", "Buildings", "Research", "Era", "Workshop", "Music", "Sectors", "Story" };
        for (int i = 0; i < pageNames.Length; i++)
        {
            Button button = FindNavigationButton(pageNames[i]);
            if (button != null && button.gameObject.activeInHierarchy)
            {
                ColorBlock colors = button.colors;
                colors.disabledColor = BuildableActionColor;
                button.colors = colors;
                bool selected = string.Equals(pageNames[i], activePage, StringComparison.Ordinal);
                Graphic targetGraphic = button.targetGraphic;
                if (targetGraphic != null && !navigationBaseColors.ContainsKey(button))
                    navigationBaseColors.Add(button, targetGraphic.color);
                if (targetGraphic != null)
                    targetGraphic.color = selected
                        ? BuildableActionColor
                        : navigationBaseColors[button];
                button.transition = selected
                    ? Selectable.Transition.None
                    : Selectable.Transition.ColorTint;
                button.interactable = !selected;
            }
        }
    }

    private void SetNavigationButtonVisible(string pageName, bool visible)
    {
        Transform button = FindNavigationButton(pageName)?.transform;
        if (button != null && button.gameObject.activeSelf != visible)
            button.gameObject.SetActive(visible);
    }

    private Button FindNavigationButton(string pageName)
    {
        Transform button = leftNavigation.Find("NavigationButtons/Nav_" + pageName);
        if (button == null)
            button = leftNavigation.Find("Nav_" + pageName);
        return button == null ? null : button.GetComponent<Button>();
    }

    private void RefreshDetailScrollGeometry()
    {
        if (detailScrollViewport == null || detailScrollContent == null)
            return;

        float viewportHeight = detailScrollViewport.rect.height;
        if (viewportHeight <= 0f)
            return;

        // The replacement Detail UI is created during Awake, before the
        // safe-area and side-panel geometry exists. Repair its initial
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
        Vector2 viewportSize = detailScrollViewport.rect.size;
        Vector2 contentSize = detailScrollContent.rect.size;
        if (!detailGeometryLogged ||
            Vector2.Distance(lastDetailViewportSize, viewportSize) > 0.01f ||
            Vector2.Distance(lastDetailContentSize, contentSize) > 0.01f)
        {
            detailGeometryLogged = true;
            lastDetailViewportSize = viewportSize;
            lastDetailContentSize = contentSize;
            Debug.Log($"[王国界面] Detail UI v2 geometry: viewport={viewportSize}, content={contentSize}");
        }
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
        EnsureNestedCanvas(viewport);
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
    }

    private static void EnsureNestedCanvas(RectTransform owner)
    {
        if (owner == null)
            return;
        Canvas canvas = owner.GetComponent<Canvas>();
        bool addedCanvas = false;
        if (canvas == null)
        {
            canvas = owner.gameObject.AddComponent<Canvas>();
            addedCanvas = true;
        }
        // A nested canvas isolates scrolling redraws from the top bar,
        // navigation and detail panel while preserving the parent's sorting.
        canvas.overrideSorting = false;
        canvas.pixelPerfect = false;
        bool addedRaycaster = owner.GetComponent<GraphicRaycaster>() == null;
        if (addedRaycaster)
            owner.gameObject.AddComponent<GraphicRaycaster>();
#if UNITY_EDITOR
        if (addedCanvas || addedRaycaster || owner.name == "ResearchGraphContent")
            KingdomEditorPerfLog.Write($"[KingdomPerf] CanvasIsolation owner={owner.name} canvas={addedCanvas} raycaster={addedRaycaster}");
#endif
    }

#if UNITY_EDITOR
    public void RefreshNavigationVisibilityForEditor() => RefreshNavigationVisibility();
#endif
}
