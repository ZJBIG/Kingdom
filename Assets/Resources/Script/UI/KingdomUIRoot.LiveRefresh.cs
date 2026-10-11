using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime presentation refresh for live GameState, resource and building values.
/// </summary>
public sealed partial class KingdomUIRoot
{
    private readonly StringBuilder developmentGuidanceTextBuilder = new(512);
    private readonly StringBuilder selectedBuildingPrerequisiteSignatureBuilder = new(128);
    private string lastSelectedBuildingPrerequisiteSignature;
    private bool selectedBuildingPrerequisiteOnlyRefresh;

    private void Update()
    {
#if UNITY_EDITOR
        float frameMilliseconds = Time.unscaledDeltaTime * 1000f;
        bool scrolling = IsPageScrolling();
        int touchCount = Input.touchCount;
        UpdatePerfTouchGesture(touchCount, frameMilliseconds);
        frameStatsLogCooldown = Mathf.Max(0f, frameStatsLogCooldown - Time.unscaledDeltaTime);
        frameSampleCount++;
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        frameAllocationCounterAvailable |= allocatedBytes > 0L;
        if (lastFrameAllocatedBytes == 0L)
            lastFrameAllocatedBytes = allocatedBytes;
        else
            frameAllocatedBytes += Math.Max(0L, allocatedBytes - lastFrameAllocatedBytes);
        lastFrameAllocatedBytes = allocatedBytes;
        frameTotalMilliseconds += frameMilliseconds;
        frameMaximumMilliseconds = Mathf.Max(frameMaximumMilliseconds, frameMilliseconds);
        if (frameMilliseconds >= 20f)
            slowFrameCount++;
        if (frameMilliseconds >= 50f)
        {
            // Keep rare long-frame evidence in the same low-volume log as the
            // rolling counters. This makes a real-device hitch attributable
            // to input/page state instead of only appearing as a max value
            // several seconds later.
            KingdomEditorPerfLog.Write(
                $"[KingdomPerf] LongFrame frame={frameMilliseconds:F2}ms " +
                $"touchCount={touchCount} scrolling={scrolling} page={populatedPage} " +
                $"gc=({GC.CollectionCount(0)},{GC.CollectionCount(1)},{GC.CollectionCount(2)})");
        }
        if (touchCount > 0)
            touchFrameCount++;
        maximumTouchCount = Mathf.Max(maximumTouchCount, touchCount);
        if (scrolling)
            dragFrameCount++;
        if (frameStatsLogCooldown <= 0f)
        {
            float averageMilliseconds = frameSampleCount == 0 ? 0f : frameTotalMilliseconds / frameSampleCount;
            int gc0Delta = GC.CollectionCount(0) - lastGc0Count;
            int gc1Delta = GC.CollectionCount(1) - lastGc1Count;
            int gc2Delta = GC.CollectionCount(2) - lastGc2Count;
            string allocatedKilobytes = frameAllocationCounterAvailable
                ? (frameAllocatedBytes / 1024L).ToString()
                : "NA";
            KingdomEditorPerfLog.Write(
                $"[KingdomPerf] FrameStats samples={frameSampleCount} avg={averageMilliseconds:F2}ms " +
                $"max={frameMaximumMilliseconds:F2}ms slow20={slowFrameCount} " +
                $"touchFrames={touchFrameCount} maxTouch={maximumTouchCount} dragFrames={dragFrameCount} " +
                $"gc0={gc0Delta} gc1={gc1Delta} gc2={gc2Delta} allocKB={allocatedKilobytes} " +
                $"queueEvents={researchQueueEventCount} page={populatedPage}");
            frameStatsLogCooldown = 5f;
            frameSampleCount = 0;
            frameTotalMilliseconds = 0f;
            frameMaximumMilliseconds = 0f;
            slowFrameCount = 0;
            touchFrameCount = 0;
            maximumTouchCount = 0;
            dragFrameCount = 0;
            lastGc0Count += gc0Delta;
            lastGc1Count += gc1Delta;
            lastGc2Count += gc2Delta;
            frameAllocatedBytes = 0L;
            researchQueueEventCount = 0;
        }
#endif
        liveRefreshTimer += Time.unscaledDeltaTime;
        developmentGuidanceRefreshTimer += Time.unscaledDeltaTime;
        scrollingLiveValueRefreshTimer += Time.unscaledDeltaTime;
        researchDetailLiveRefreshTimer += Time.unscaledDeltaTime;
        topInfoRefreshTimer += Time.unscaledDeltaTime;
        researchDynamicSignatureRefreshTimer += Time.unscaledDeltaTime;
        researchQueuePollTimer += Time.unscaledDeltaTime;
        navigationVisibilityRefreshTimer += Time.unscaledDeltaTime;
        buildingStructureRefreshTimer += Time.unscaledDeltaTime;
        eraPageRefreshTimer += Time.unscaledDeltaTime;
        sectorPageRefreshTimer += Time.unscaledDeltaTime;
#if UNITY_EDITOR
        uiSlowRefreshLogCooldown = Mathf.Max(0f, uiSlowRefreshLogCooldown - Time.unscaledDeltaTime);
        uiStatsLogCooldown = Mathf.Max(0f, uiStatsLogCooldown - Time.unscaledDeltaTime);
        researchQueueEventLogCooldown = Mathf.Max(0f, researchQueueEventLogCooldown - Time.unscaledDeltaTime);
#endif
        if (liveRefreshTimer < 0.1f)
            return;
        liveRefreshTimer = 0f;
        RefreshUI();
    }

#if UNITY_EDITOR
    private void UpdatePerfTouchGesture(int touchCount, float frameMilliseconds)
    {
        if (touchCount > 0)
        {
            Vector2 position = Input.GetTouch(0).position;
            if (!perfTouchGestureActive)
            {
                perfTouchGestureActive = true;
                perfTouchGestureStartTime = Time.realtimeSinceStartup;
                perfTouchGestureSamples = 0;
                perfTouchGestureDistance = 0f;
                perfTouchGestureLastPosition = position;
                perfTouchGestureMaximumFrameMilliseconds = 0f;
                perfTouchGestureSlowFrameCount = 0;
            }

            perfTouchGestureSamples++;
            perfTouchGestureDistance += Vector2.Distance(perfTouchGestureLastPosition, position);
            perfTouchGestureLastPosition = position;
            perfTouchGestureMaximumFrameMilliseconds = Mathf.Max(perfTouchGestureMaximumFrameMilliseconds, frameMilliseconds);
            if (frameMilliseconds >= 20f)
                perfTouchGestureSlowFrameCount++;
            return;
        }

        if (!perfTouchGestureActive)
            return;

        float durationMilliseconds = (Time.realtimeSinceStartup - perfTouchGestureStartTime) * 1000f;
        KingdomEditorPerfLog.Write(
            $"[KingdomPerf] TouchGesture duration={durationMilliseconds:F1}ms samples={perfTouchGestureSamples} " +
            $"distance={perfTouchGestureDistance:F1}px maxFrame={perfTouchGestureMaximumFrameMilliseconds:F2}ms " +
            $"slow20={perfTouchGestureSlowFrameCount} page={populatedPage}");
        perfTouchGestureActive = false;
    }
#endif

#if UNITY_EDITOR
    private int uiTopSampleCount;
    private int uiDetailSampleCount;
    private int uiDataFlowSampleCount;
    private int uiLiveCardsSampleCount;
    private int uiQueueSampleCount;
    private float uiTopTotalMilliseconds;
    private float uiTopMaximumMilliseconds;
    private float uiDetailTotalMilliseconds;
    private float uiDetailMaximumMilliseconds;
    private float uiDataFlowTotalMilliseconds;
    private float uiDataFlowMaximumMilliseconds;
    private float uiLiveCardsTotalMilliseconds;
    private float uiLiveCardsMaximumMilliseconds;
    private float uiQueueTotalMilliseconds;
    private float uiQueueMaximumMilliseconds;

    private void RecordUiBranch(string branch, float milliseconds)
    {
        switch (branch)
        {
            case "top":
                uiTopSampleCount++;
                uiTopTotalMilliseconds += milliseconds;
                uiTopMaximumMilliseconds = Mathf.Max(uiTopMaximumMilliseconds, milliseconds);
                break;
            case "detail":
                uiDetailSampleCount++;
                uiDetailTotalMilliseconds += milliseconds;
                uiDetailMaximumMilliseconds = Mathf.Max(uiDetailMaximumMilliseconds, milliseconds);
                break;
            case "dataflow":
                uiDataFlowSampleCount++;
                uiDataFlowTotalMilliseconds += milliseconds;
                uiDataFlowMaximumMilliseconds = Mathf.Max(uiDataFlowMaximumMilliseconds, milliseconds);
                break;
            case "liveCards":
                uiLiveCardsSampleCount++;
                uiLiveCardsTotalMilliseconds += milliseconds;
                uiLiveCardsMaximumMilliseconds = Mathf.Max(uiLiveCardsMaximumMilliseconds, milliseconds);
                break;
            case "queue":
                uiQueueSampleCount++;
                uiQueueTotalMilliseconds += milliseconds;
                uiQueueMaximumMilliseconds = Mathf.Max(uiQueueMaximumMilliseconds, milliseconds);
                break;
        }
    }

    private void LogUiBranchStats()
    {
        if (uiTopSampleCount + uiDetailSampleCount + uiDataFlowSampleCount +
            uiLiveCardsSampleCount + uiQueueSampleCount <= 0)
            return;
        KingdomEditorPerfLog.Write(
            $"[KingdomPerf] RefreshUIBranches " +
            $"top={uiTopSampleCount}:{(uiTopSampleCount == 0 ? 0f : uiTopTotalMilliseconds / uiTopSampleCount):F2}/{uiTopMaximumMilliseconds:F2}ms " +
            $"detail={uiDetailSampleCount}:{(uiDetailSampleCount == 0 ? 0f : uiDetailTotalMilliseconds / uiDetailSampleCount):F2}/{uiDetailMaximumMilliseconds:F2}ms " +
            $"dataflow={uiDataFlowSampleCount}:{(uiDataFlowSampleCount == 0 ? 0f : uiDataFlowTotalMilliseconds / uiDataFlowSampleCount):F2}/{uiDataFlowMaximumMilliseconds:F2}ms " +
            $"liveCards={uiLiveCardsSampleCount}:{(uiLiveCardsSampleCount == 0 ? 0f : uiLiveCardsTotalMilliseconds / uiLiveCardsSampleCount):F2}/{uiLiveCardsMaximumMilliseconds:F2}ms " +
            $"queue={uiQueueSampleCount}:{(uiQueueSampleCount == 0 ? 0f : uiQueueTotalMilliseconds / uiQueueSampleCount):F2}/{uiQueueMaximumMilliseconds:F2}ms " +
            $"page={populatedPage}");
        uiTopSampleCount = 0;
        uiDetailSampleCount = 0;
        uiDataFlowSampleCount = 0;
        uiLiveCardsSampleCount = 0;
        uiQueueSampleCount = 0;
        uiTopTotalMilliseconds = 0f;
        uiTopMaximumMilliseconds = 0f;
        uiDetailTotalMilliseconds = 0f;
        uiDetailMaximumMilliseconds = 0f;
        uiDataFlowTotalMilliseconds = 0f;
        uiDataFlowMaximumMilliseconds = 0f;
        uiLiveCardsTotalMilliseconds = 0f;
        uiLiveCardsMaximumMilliseconds = 0f;
        uiQueueTotalMilliseconds = 0f;
        uiQueueMaximumMilliseconds = 0f;
    }
#endif

    private void RefreshBuildingDisplayMembership()
    {
        if (populatedPage != "Buildings" || BuildingManager.Instance == null)
            return;

        StringBuilder signature = BuildBuildingDisplaySignature();
        if (lastBuildingDisplaySignature == null)
        {
            lastBuildingDisplaySignature = signature.ToString();
            return;
        }
        if (SignatureEquals(signature, lastBuildingDisplaySignature))
            return;

        // 升级完成后，旧层级可能变为零数量；只在显示成员变化时重建列表。
        lastBuildingDisplaySignature = signature.ToString();
        float normalizedPosition = pageScroll == null ? 1f : pageScroll.verticalNormalizedPosition;
        buildingRowsBuilt = false;
        PopulatePage("Buildings");
        if (pageScroll != null)
            pageScroll.verticalNormalizedPosition = normalizedPosition;
    }

    public void RefreshUI()
    {
#if UNITY_EDITOR
        float refreshStart = Time.realtimeSinceStartup;
#endif
        CacheRuntimeManagers();
        // Keep onboarding and story unlocks current on the page where the
        // player acted. Overview, Era and Story render the resulting snapshot
        // later, but the progression state must not wait for a page switch.
        TutorialManager tutorial = TutorialManager.Current;
        if (tutorial != null)
        {
            if (observedTutorialSaveSessionVersion !=
                tutorial.SaveSessionVersion)
            {
                ResetRecentActionStateForNewSave();
                observedTutorialSaveSessionVersion = tutorial.SaveSessionVersion;
            }
            if (recentActionFeedbackVersion >= 0 &&
                recentActionFeedbackVersion != tutorial.SaveSessionVersion)
                ResetRecentActionStateForNewSave();
            if (tutorialSnapshotSource != tutorial)
            {
                tutorialSnapshot = null;
                tutorialSnapshotSource = tutorial;
                tutorialRecentCompletionFeedback = string.Empty;
                ResetRecentActionStateForNewSave();
                tutorialFeedbackVersion = -1;
            }
            tutorialSnapshot = tutorial.Evaluate();
            if (gameManagerCache != null && gameManagerCache.State != null)
                StoryManager.RefreshProgress(gameManagerCache.State.TechLevel, tutorial);
            if (tutorialFeedbackVersion != tutorial.Version)
            {
                if (recentActionFeedbackVersion >= 0 &&
                    recentActionFeedbackVersion != tutorial.SaveSessionVersion)
                    ClearRecentActionFeedback();
                tutorialFeedbackVersion = tutorial.Version;
                tutorialRecentCompletionFeedback =
                    tutorialSnapshot.CompletedFeedback ?? string.Empty;
            }
            else if (!string.IsNullOrWhiteSpace(tutorialSnapshot.CompletedFeedback))
                tutorialRecentCompletionFeedback = tutorialSnapshot.CompletedFeedback;
        }
        else
        {
            tutorialSnapshot = null;
            tutorialSnapshotSource = null;
            tutorialRecentCompletionFeedback = string.Empty;
            ClearRecentActionFeedback();
            observedTutorialSaveSessionVersion = -1;
            tutorialFeedbackVersion = -1;
        }
        ObserveBuildingCompletions(buildingManagerCache);
        ObservePopulationGrowth(gameManagerCache);
        if (tutorial != null && gameManagerCache != null &&
            gameManagerCache.State != null)
        {
            int previousStoryCount = storyObservedUnlockCount;
            bool previousStoryEraInitialized = storyObservedEraInitialized;
            TechLevel previousStoryEra = storyObservedEra;
            ObserveStoryProgress(
                gameManagerCache.State.TechLevel,
                tutorial,
                out int storyCount,
                out StoryChapter latestStoryChapter);
            bool storyProgressChanged = previousStoryCount >= 0 &&
                storyCount > previousStoryCount;
            bool storyEraChanged = previousStoryEraInitialized &&
                previousStoryEra != gameManagerCache.State.TechLevel;
            if (storyProgressChanged || storyEraChanged)
                storyPageBuilt = false;
            if (storyProgressChanged &&
                latestStoryChapter != null &&
                storyNotifiedChapterIds.Add(latestStoryChapter.Id))
            {
                EnqueueRecentNotice("剧情完成：" + latestStoryChapter.Title +
                    "；王国的行动留下了一段永久的文明记忆。");
            }
        }
        if (navigationVisibilityRefreshTimer >= 1f)
        {
            navigationVisibilityRefreshTimer = 0f;
            RefreshNavigationVisibility();
            RefreshReviewControls();
        }
        bool pageScrolling = IsPageScrolling();
        PollResearchQueueVisualIfDue();
        if (pageScrolling)
            // Do not perform the first full research signature scan on the
            // release frame. Let the graph settle, then scan after a quiet
            // interval so touch movement and layout are not coupled.
            researchDynamicSignatureRefreshTimer = 0f;

        // Text-only refreshes keep running while the user is sliding. The
        // previous blanket skip-everything-while-scrolling gate is what
        // froze the top status bar, the detail dataflow and the live card
        // values during a drag or fling.
        // Live cards perform many ExpantaNum formatting operations. Keep the
        // top bar at the normal cadence, but throttle card/detail formatting
        // during a touch drag so Canvas work cannot compete with movement.
        // Card/detail text is presentation-only and is expensive because it
        // formats many ExpantaNum values. Four updates per second are enough
        // for live values and keep the main thread available for touch motion,
        // even if the platform does not report dragging velocity reliably.
        bool refreshScrolledValues = scrollingLiveValueRefreshTimer >= 0.25f;
        if (refreshScrolledValues)
        {
#if UNITY_EDITOR
            float branchStart = Time.realtimeSinceStartup;
            long liveValuesAllocatedStart = GC.GetAllocatedBytesForCurrentThread();
#endif
            RefreshLiveCardValues();
#if UNITY_EDITOR
            uiRefreshAllocatedBytes += Math.Max(0L, GC.GetAllocatedBytesForCurrentThread() - liveValuesAllocatedStart);
            uiRefreshMaximumAllocatedBytes = Math.Max(uiRefreshMaximumAllocatedBytes, GC.GetAllocatedBytesForCurrentThread() - liveValuesAllocatedStart);
            RecordUiBranch("liveCards", (Time.realtimeSinceStartup - branchStart) * 1000f);
#endif
            scrollingLiveValueRefreshTimer = 0f;
        }
        if (topInfoRefreshTimer >= 0.5f)
        {
            RefreshTopInfo();
            topInfoRefreshTimer = 0f;
        }
        if (populatedPage == "Research" &&
            researchQueueUiDirty)
        {
#if UNITY_EDITOR
            float branchStart = Time.realtimeSinceStartup;
#endif
            RefreshResearchQueueToolbar();
#if UNITY_EDITOR
            RecordUiBranch("queue", (Time.realtimeSinceStartup - branchStart) * 1000f);
#endif
            // Layout may remain pending while the page is moving, but the
            // queue text itself has already been refreshed. Do not turn the
            // pending-height flag into a per-frame text rebuild.
            // During the first research-page build this branch can run before
            // the authored queue graphic is bound. Keep the flag until both
            // the graphic and its data source are ready; otherwise the first
            // queue sync is lost and a later research click appears to fix it.
            researchQueueUiDirty = researchQueueViewport == null ||
                researchQueueContent == null || ResearchManager.Instance == null;
        }
        if (populatedPage == "Overview" &&
            (developmentGuidanceSnapshot == null || developmentGuidanceRefreshTimer >= 2f))
        {
            RefreshDevelopmentGuidance();
            RefreshOverviewNavigationToolbar();
            developmentGuidanceRefreshTimer = 0f;
        }
        if (!pageScrolling && populatedPage == "Era" && eraPageRefreshTimer >= 1f)
        {
            RefreshEraPageIfChanged();
            eraPageRefreshTimer = 0f;
        }
        if (!pageScrolling && populatedPage == "Sectors" && sectorPageRefreshTimer >= 1f)
        {
            RefreshSectorRowSummaries();
            sectorPageRefreshTimer = 0f;
        }
        if (!pageScrolling && populatedPage == "Story" &&
            (eraPageRefreshTimer >= 1f || !storyPageBuilt))
        {
            RefreshStoryPageIfChanged();
            eraPageRefreshTimer = 0f;
        }
        if (refreshScrolledValues && selectedResource != null && ShouldRefreshSelectedResource())
        {
#if UNITY_EDITOR
            float branchStart = Time.realtimeSinceStartup;
#endif
            RefreshResourceDetails(selectedResource);
#if UNITY_EDITOR
            RecordUiBranch("detail", (Time.realtimeSinceStartup - branchStart) * 1000f);
#endif
        }
        else if (refreshScrolledValues && selectedBuilding != null && ShouldRefreshSelectedBuilding())
        {
#if UNITY_EDITOR
            float branchStart = Time.realtimeSinceStartup;
#endif
            RefreshSelectedBuildingDetails(selectedBuilding);
#if UNITY_EDITOR
            RecordUiBranch("detail", (Time.realtimeSinceStartup - branchStart) * 1000f);
#endif
        }
        else if (refreshScrolledValues && selectedSectorDefinition != null && populatedPage != "Sectors")
        {
            RefreshSelectedSectorDetails(GameManager.Instance.Sectors,
                GameManager.Instance.State, ResourceManager.Instance);
        }
        else if (refreshScrolledValues && selectedWorkshop != null)
        {
            RefreshRequirementRows(selectedWorkshop.ResourceRequirements);
            ConfigureWorkshopPaymentButton(selectedWorkshop, false);
            if (!pageScrolling && populatedPage == "Workshop")
                SetWorkshopDetailBody(selectedWorkshop);
        }
        else if (!pageScrolling && populatedPage == "Research")
        {
            // Graph panning must not pause the presentation clock. Refreshing
            // text/colors does not rebuild graph geometry, so it is safe while
            // the gesture owns the pointer and keeps the queue/progress live.
            RefreshResearchDynamicUI();
            if (researchDetailLiveRefreshTimer >= 0.75f && selectedResearchNode != null)
            {
                RefreshResearchDetailLiveValues(selectedResearchNode);
                researchDetailLiveRefreshTimer = 0f;
            }
        }
        else if (pageScrolling && populatedPage == "Research" && refreshScrolledValues)
        {
            // Preserve live progress while graph panning, without rebuilding
            // signatures or recoloring all 79 nodes per frame. Queue changes
            // are already handled by the event and 250 ms signature poll
            // above; rebuilding the same queue string here caused avoidable
            // allocations during every drag.
            RefreshActiveResearchProgressVisual(ResearchManager.Instance);
            if (selectedResearchNode != null &&
                researchDetailLiveRefreshTimer >= 0.75f &&
                (researchGraphGesture == null || !researchGraphGesture.IsDragging))
            {
#if UNITY_EDITOR
                float branchStart = Time.realtimeSinceStartup;
#endif
                RefreshResearchDetailLiveValues(selectedResearchNode);
                researchDetailLiveRefreshTimer = 0f;
#if UNITY_EDITOR
                RecordUiBranch("detail", (Time.realtimeSinceStartup - branchStart) * 1000f);
#endif
            }
        }
        if (populatedPage == "Music")
            RefreshMusicPage();
        if (refreshScrolledValues && populatedPage == "Workshop")
        {
            RefreshWorkshopPurchaseButtonStates();
            RefreshWorkshopFilterMembershipIfChanged();
            if (lastRelicWorkshopSignature != GetRelicWorkshopSignature())
                workshopRowsUiDirty = true;
        }

        // Structural refreshes rebuild page rows or re-parent controls; they
        // must not run mid-scroll or they reset the user's scroll position
        // while the gesture is still active.
        bool refreshBuildingStructure = !pageScrolling && buildingStructureRefreshTimer >= 0.5f;
        if (refreshBuildingStructure)
        {
            RefreshBuildingDisplayMembership();
            RefreshBuildingQuantityHeader();
            buildingStructureRefreshTimer = 0f;
        }
        if (!pageScrolling && workshopRowsUiDirty && populatedPage == "Workshop")
            RefreshWorkshopRows();

#if UNITY_EDITOR
        float refreshElapsed = Time.realtimeSinceStartup - refreshStart;
        float refreshMilliseconds = refreshElapsed * 1000f;
        uiRefreshSampleCount++;
        uiRefreshTotalMilliseconds += refreshMilliseconds;
        uiRefreshMaximumMilliseconds = Mathf.Max(uiRefreshMaximumMilliseconds, refreshMilliseconds);
        if (uiStatsLogCooldown <= 0f)
        {
            uiStatsLogCooldown = 5f;
            float averageMilliseconds = uiRefreshSampleCount == 0
                ? 0f
                : uiRefreshTotalMilliseconds / uiRefreshSampleCount;
            KingdomEditorPerfLog.Write(
                $"[KingdomPerf] RefreshUIStats samples={uiRefreshSampleCount} " +
                $"avg={averageMilliseconds:F2}ms max={uiRefreshMaximumMilliseconds:F2}ms page={populatedPage}");
            KingdomEditorPerfLog.Write(
                $"[KingdomPerf] RefreshUIAlloc samples={uiRefreshSampleCount} " +
                $"totalKB={uiRefreshAllocatedBytes / 1024L} maxKB={uiRefreshMaximumAllocatedBytes / 1024L} page={populatedPage}");
            LogUiBranchStats();
            uiRefreshSampleCount = 0;
            uiRefreshTotalMilliseconds = 0f;
            uiRefreshMaximumMilliseconds = 0f;
            uiRefreshAllocatedBytes = 0L;
            uiRefreshMaximumAllocatedBytes = 0L;
        }
        if (uiSlowRefreshLogCooldown <= 0f && refreshElapsed >= 0.01f)
        {
            uiSlowRefreshLogCooldown = 1f;
            string message = $"[KingdomPerf] RefreshUI {refreshElapsed * 1000f:F1}ms page={populatedPage}";
            Debug.Log(message);
            KingdomEditorPerfLog.Write(message);
        }
#endif

    }

    private void RefreshEraPageIfChanged()
    {
        if (gameManagerCache == null || gameManagerCache.State == null)
            return;
        GameState state = gameManagerCache.State;
        EraGoalEvaluation eraGoal = EraGoalEvaluator.Evaluate(
            state.TechLevel,
            researchManagerCache,
            resourceManagerCache);
        // EraGoal and onboarding must advance from the same live state while
        // the player remains on this page. Overview already evaluates the
        // tutorial on its own refresh path; doing it here keeps the era tab
        // from showing a stale goal after a research or population change.
        string signature = BuildEraPageStateSignature(state, eraGoal);
        if (string.Equals(signature, eraPageStateSignature, StringComparison.Ordinal))
            return;

        float normalizedPosition = pageScroll == null ? 1f : pageScroll.verticalNormalizedPosition;
        PopulatePage("Era", true, eraGoal);
        if (pageScroll != null)
            pageScroll.verticalNormalizedPosition = normalizedPosition;
    }

    private bool IsPageScrolling()
    {
        if (UIPageScrollDragForwarder.IsRecentlyDragged ||
            pageScroll != null && pageScroll.velocity.sqrMagnitude > 0.01f)
            return true;
        if (detailScroll != null && detailScroll.velocity.sqrMagnitude > 0.01f)
            return true;
        if (musicListScroll != null && musicListScroll.velocity.sqrMagnitude > 0.01f)
            return true;
        if (researchGraphGesture != null && researchGraphGesture.IsDragging)
            return true;
        if (requirementGesture != null && requirementGesture.IsDragging)
            return true;
        return false;
    }

    private void CacheRuntimeManagers()
    {
        if (gameManagerCache == null) gameManagerCache = FindObjectOfType<GameManager>();
        if (researchManagerCache == null)
            researchManagerCache = FindObjectOfType<ResearchManager>();
        if (researchQueueEventSource != researchManagerCache)
        {
            if (researchQueueEventSource != null)
                researchQueueEventSource.ResearchQueueChanged -= MarkResearchQueueUiDirty;
            researchQueueEventSource = researchManagerCache;
            if (researchQueueEventSource != null)
            {
                researchQueueEventSource.ResearchQueueChanged += MarkResearchQueueUiDirty;
                researchQueueEventSubscribed = true;
                researchQueueUiDirty = true;
                lastResearchQueuePollSignature = null;
            }
            else
                researchQueueEventSubscribed = false;
        }
        if (resourceManagerCache == null) resourceManagerCache = FindObjectOfType<ResourceManager>();
        if (buildingManagerCache == null) buildingManagerCache = FindObjectOfType<BuildingManager>();
        if (workshopManagerCache == null) workshopManagerCache = FindObjectOfType<WorkshopManager>();
        if (workshopEventSource != workshopManagerCache)
        {
            if (workshopEventSource != null)
                workshopEventSource.UpgradeStateChanged -= MarkWorkshopRowsUiDirty;
            workshopEventSource = workshopManagerCache;
            if (workshopEventSource != null)
                workshopEventSource.UpgradeStateChanged += MarkWorkshopRowsUiDirty;
        }
    }

    private void MarkResearchQueueUiDirty()
    {
        researchQueueUiDirty = true;
        researchDynamicUiDirty = true;
        workshopRowsUiDirty = true;
#if UNITY_EDITOR
        researchQueueEventCount++;
        if (researchQueueEventLogCooldown <= 0f)
        {
            researchQueueEventLogCooldown = 0.25f;
            KingdomEditorPerfLog.Write(
                $"[KingdomPerf] ResearchQueueEvent subscribed={researchQueueEventSubscribed} " +
                $"queueCount={(researchManagerCache == null ? -1 : researchManagerCache.ResearchQueue.Count)}");
        }
#endif
    }

    private void PollResearchQueueVisualIfDue()
    {
        if (populatedPage != "Research" || researchManagerCache == null ||
            researchQueuePollTimer < 1f)
            return;

        researchQueuePollTimer = 0f;
        StringBuilder signature = BuildResearchQueueSignatureBuffer();
        if (SignatureEquals(signature, lastResearchQueuePollSignature))
            return;

        // The event is the fast path, but older ResearchManager actions and
        // external state changes do not all raise it. Keep a cheap queue-only
        // fallback so the toolbar cannot remain stale until the next page
        // rebuild. This avoids a full structural scan of the current tree.
        lastResearchQueuePollSignature = signature.ToString();
        researchQueueUiDirty = true;
        // Polling is also the fallback for queue mutations that bypass the
        // manager event. Mark the graph dirty so queued/active node visuals
        // converge on the same refresh, not only the toolbar text.
        researchDynamicUiDirty = true;
        // Do not refresh here. RefreshUI processes the dirty flag below and
        // coalesces event and polling notifications into one queue refresh.
        // Calling the graphic immediately caused the same queue mutation to
        // rebuild the Destroy/Instantiate visuals twice in one UI cycle.
    }

    private void OnDestroy()
    {
        if (researchQueueEventSource != null)
            researchQueueEventSource.ResearchQueueChanged -= MarkResearchQueueUiDirty;
        researchQueueEventSource = null;
        researchQueueEventSubscribed = false;
        if (workshopEventSource != null)
            workshopEventSource.UpgradeStateChanged -= MarkWorkshopRowsUiDirty;
        workshopEventSource = null;
    }

    private void MarkWorkshopRowsUiDirty(WorkshopUpgradeState state)
    {
        workshopRowsUiDirty = true;
        // Workshop purchases also unlock Story chapters. Keep the story page
        // as a presentation layer, but invalidate its cached rendering so a
        // real purchase is visible the next time the page refreshes.
        storyPageBuilt = false;
        if (state != null && state.Definition != null)
        {
            EnqueueRecentNotice("改造完成：" + state.Definition.Label +
                " 已让旧有生产体系承担新的文明任务；查看工坊详情确认实际效果。");
        }
    }

    private void RefreshWorkshopRows()
    {
        if (populatedPage != "Workshop")
            return;
        float normalizedPosition = pageScroll == null ? 1f : pageScroll.verticalNormalizedPosition;
        workshopRowsBuilt = false;
        PopulatePage("Workshop");
        if (pageScroll != null)
            pageScroll.verticalNormalizedPosition = normalizedPosition;
        workshopRowsUiDirty = false;
    }

    private bool ShouldRefreshSelectedResource()
    {
        if (selectedResource == null) return false;
        int version = -1;
        if (resourceManagerCache != null && resourceManagerCache.States.TryGetValue(selectedResource, out ResourceState state))
            version = state.Version;
        if (version == lastSelectedResourceVersion) return false;
        lastSelectedResourceVersion = version;
        return true;
    }

    private bool ShouldRefreshSelectedBuilding()
    {
        if (selectedBuilding == null) return false;
        int version = -1;
        BuildingState state = null;
        if (buildingManagerCache != null && buildingManagerCache.States.TryGetValue(selectedBuilding, out state))
            version = state.Version;
        bool upgrading = state != null && state.Amount > ExpantaNum.Zero &&
            buildingManagerCache != null && buildingManagerCache.TryGetUnlockedUpgradeTarget(selectedBuilding, out _);
        int resourceVersion = 0;
        if (resourceManagerCache != null)
            for (int i = 0; i < selectedBuilding.ResourceRequirements.Count; i++)
            {
                Resource resource = selectedBuilding.ResourceRequirements[i].First;
                if (resource != null && resourceManagerCache.States.TryGetValue(resource, out ResourceState resourceState))
                    resourceVersion = unchecked(resourceVersion * 31 + resourceState.Version);
            }
        int ultraInputSignature = IsUltraProjectAccessBuilding(selectedBuilding) &&
            GameManager.TryGetInstance(out GameManager liveGameManager)
            ? GetUltraProjectInputSignature(liveGameManager.UltraProject) : -1;
        int ultraStateVersion = IsUltraProjectAccessBuilding(selectedBuilding) &&
            GameManager.TryGetInstance(out GameManager stateGameManager) &&
            stateGameManager.UltraProject != null
            ? stateGameManager.UltraProject.State.Version : -1;
        StringBuilder prerequisiteSignature = BuildSelectedBuildingPrerequisiteSignature(selectedBuilding);
        bool prerequisitesChanged = !SignatureEquals(
            prerequisiteSignature, lastSelectedBuildingPrerequisiteSignature);
        bool otherValuesChanged = version != lastSelectedBuildingVersion ||
            upgrading != lastSelectedBuildingUpgrade ||
            resourceVersion != lastSelectedBuildingResourceVersion ||
            ultraInputSignature != lastUltraProjectInputSignature ||
            ultraStateVersion != lastUltraProjectStateVersion;
        if (!otherValuesChanged && !prerequisitesChanged)
            return false;

        lastSelectedBuildingVersion = version;
        lastSelectedBuildingUpgrade = upgrading;
        lastSelectedBuildingResourceVersion = resourceVersion;
        lastUltraProjectStateVersion = ultraStateVersion;
        lastUltraProjectInputSignature = ultraInputSignature;
        if (prerequisitesChanged)
            lastSelectedBuildingPrerequisiteSignature = prerequisiteSignature.ToString();
        selectedBuildingPrerequisiteOnlyRefresh = prerequisitesChanged && !otherValuesChanged;
        return true;
    }

    private StringBuilder BuildSelectedBuildingPrerequisiteSignature(Building building)
    {
        StringBuilder signature = selectedBuildingPrerequisiteSignatureBuilder;
        signature.Clear();
        ResearchManager researchManager = ResearchManager.Instance;
        IReadOnlyList<Research> researchPrerequisites = building.RequiredResearch;
        signature.Append('R').Append(researchPrerequisites == null ? 0 : researchPrerequisites.Count).Append(':');
        if (researchPrerequisites != null)
            for (int i = 0; i < researchPrerequisites.Count; i++)
            {
                Research prerequisite = researchPrerequisites[i];
                signature.Append(prerequisite != null && researchManager != null &&
                    researchManager.IsResearchCompleted(prerequisite.Id) ? '1' : '0');
            }

        WorkshopManager workshopManager = WorkshopManager.Instance;
        IReadOnlyList<WorkshopUpgrade> workshopPrerequisites = building.RequiredWorkshopUpgrades;
        signature.Append('|').Append('W').Append(workshopPrerequisites == null ? 0 : workshopPrerequisites.Count).Append(':');
        if (workshopPrerequisites != null)
            for (int i = 0; i < workshopPrerequisites.Count; i++)
                signature.Append(workshopPrerequisites[i] != null && workshopManager != null &&
                    workshopManager.IsPurchased(workshopPrerequisites[i]) ? '1' : '0');
        return signature;
    }

    private void BuildDevelopmentGuidance(RectTransform page)
    {
        if (page == null)
            return;
        RectTransform primaryCard = page.Find("PrimaryCard") as RectTransform;
        developmentGuidanceText = primaryCard == null
            ? null
            : primaryCard.Find("Text")?.GetComponent<TMP_Text>();
        if (developmentGuidanceText == null)
        {
            Debug.LogError("[王国界面] Overview PrimaryCard/Text is missing; development guidance cannot render.");
            return;
        }
        developmentGuidanceText.enabled = true;
        developmentGuidanceText.gameObject.SetActive(true);

        if (primaryCard != null)
        {
            developmentGuidanceNavigationButton = primaryCard.Find("NavigationButton")?.GetComponent<Button>();
            if (developmentGuidanceNavigationButton == null)
            {
                Debug.LogError("[王国界面] Overview PrimaryCard is missing authored NavigationButton.");
                return;
            }
            developmentGuidanceNavigationButton.onClick.RemoveAllListeners();
            developmentGuidanceNavigationButton.interactable = false;
        }
        Button guidanceButton = page.GetComponent<Button>();
        if (guidanceButton != null)
        {
            guidanceButton.onClick.RemoveAllListeners();
            guidanceButton.interactable = false;
            guidanceButton.enabled = false;
        }
        Canvas.ForceUpdateCanvases();
        Vector2 initialRect = developmentGuidanceText.rectTransform.rect.size;
        if (initialRect.x > 0f && initialRect.y > 0f)
            RefreshDevelopmentGuidance();
        else
            Debug.Log($"[王国界面] Development guidance binding deferred until layout: rect={initialRect}");
    }

    private void RefreshDevelopmentGuidance()
    {
        if (developmentGuidanceText == null)
            return;
        CacheRuntimeManagers();
        TutorialManager tutorialManager = TutorialManager.Current;
        if (tutorialManager != null)
        {
            try
            {
                if (tutorialSnapshotSource != tutorialManager ||
                    tutorialSnapshot == null)
                {
                    tutorialSnapshotSource = tutorialManager;
                    tutorialSnapshot = tutorialManager.Evaluate();
                    if (!string.IsNullOrWhiteSpace(tutorialSnapshot.CompletedFeedback))
                        tutorialRecentCompletionFeedback = tutorialSnapshot.CompletedFeedback;
                }
                StringBuilder onboarding = developmentGuidanceTextBuilder;
                onboarding.Clear();
                onboarding.Append("当前时代：").Append(tutorialSnapshot.CurrentEra).Append("\n");
                onboarding.Append("人口：").Append(tutorialSnapshot.PopulationText)
                    .Append("  食物：").Append(tutorialSnapshot.FoodText)
                    .Append("  ").Append(tutorialSnapshot.CoreResourceText).Append("\n");
                if (gameManagerCache != null && gameManagerCache.State != null &&
                    gameManagerCache.State.TechLevel >= TechLevel.Industrial)
                {
                    GameState guidanceState = gameManagerCache.State;
                    onboarding.Append("电力：").Append(guidanceState.PowerProductionRate.ToGameString())
                        .Append("/s 供给 / ").Append(guidanceState.PowerConsumptionRate.ToGameString())
                        .Append("/s 消耗  物流：").Append(guidanceState.LogisticsProductionRate.ToGameString())
                        .Append("/s 供给 / ").Append(guidanceState.LogisticsConsumptionRate.ToGameString())
                        .Append("/s 消耗\n");
                }
                if (!string.IsNullOrWhiteSpace(tutorialSnapshot.CompletedGoal))
                    onboarding.Append("上一步已完成：").Append(tutorialSnapshot.CompletedGoal).Append("\n");
                if (!string.IsNullOrWhiteSpace(tutorialRecentCompletionFeedback))
                    onboarding.Append("刚刚改变：").Append(tutorialRecentCompletionFeedback).Append("\n");
                if (!string.IsNullOrWhiteSpace(recentActionFeedback))
                    onboarding.Append("刚刚发生：").Append(recentActionFeedback).Append("\n");
                onboarding.Append("当前目标：").Append(tutorialSnapshot.CurrentGoal).Append("\n");
                if (!string.IsNullOrWhiteSpace(tutorialSnapshot.IndustrialCurrentStep))
                {
                    onboarding.Append("工业路线\n当前：").Append(tutorialSnapshot.IndustrialCurrentStep)
                        .Append("\n下一步：").Append(tutorialSnapshot.IndustrialNextStep)
                        .Append("\n之后：").Append(tutorialSnapshot.IndustrialAfterStep).Append("\n");
                }
        onboarding.Append("下一时代目标：").Append(tutorialSnapshot.NextEraGoal).Append("\n");
                onboarding.Append("当前阻碍：").Append(tutorialSnapshot.Blocker).Append("\n");
                onboarding.Append("推荐行动：").Append(tutorialSnapshot.RecommendedAction);
                AppendUltraProjectOverview(onboarding);
                AppendOfflineSummary(onboarding);
                AppendSaveFeedback(onboarding);
                string overviewText = onboarding.ToString();
                if (!string.IsNullOrWhiteSpace(tutorialSnapshot.NextEraGoal))
                    overviewText = overviewText.Replace(
                        tutorialSnapshot.NextEraGoal,
                        BuildOverviewEraTarget(gameManagerCache));
                if (!string.Equals(overviewText, developmentGuidanceText.text,
                    StringComparison.Ordinal))
                    developmentGuidanceText.text = overviewText;
                developmentGuidanceText.color = TextPrimary;
                ConfigureDevelopmentGuidanceNavigation(tutorialSnapshot);
                RefreshDevelopmentGuidanceLayout();
                return;
            }
            catch (Exception exception)
            {
                if (!developmentGuidanceErrorLogged)
                {
                    developmentGuidanceErrorLogged = true;
                    Debug.LogException(exception);
                }
            }
        }
        GameManager gameManager = gameManagerCache;
        try
        {
            developmentGuidanceSnapshot = DevelopmentGuidance.Build(
                gameManager,
                researchManagerCache,
                resourceManagerCache,
                buildingManagerCache,
                workshopManagerCache);
        }
        catch (Exception exception)
        {
            // Guidance is presentation-only. A partially restored manager
            // must never stop the Overview text from rendering.
            if (!developmentGuidanceErrorLogged)
            {
                developmentGuidanceErrorLogged = true;
                Debug.LogException(exception);
            }
            developmentGuidanceText.text = "当前发展指引\n\n正在读取王国状态，请稍候。";
            developmentGuidanceText.color = TextPrimary;
            RefreshDevelopmentGuidanceLayout();
            return;
        }
        DevelopmentGuidanceSnapshot snapshot = developmentGuidanceSnapshot;
        StringBuilder body = developmentGuidanceTextBuilder;
        body.Clear();
        if (!string.IsNullOrEmpty(snapshot.EraText))
            body.Append("当前时代：").Append(snapshot.EraText).Append("\n");
        body.Append("当前目标：").Append(snapshot.Title ?? string.Empty).Append("\n");
        IReadOnlyList<string> blockers = snapshot.Blockers ?? Array.Empty<string>();
        body.Append("当前阻碍：");
        if (blockers.Count == 0)
            body.Append("暂无");
        else
            for (int i = 0; i < blockers.Count && i < 3; i++)
            {
                if (i > 0)
                    body.Append("、");
                body.Append(blockers[i]);
            }
        body.Append("\n推荐行动：").Append(snapshot.Body ?? string.Empty);
        AppendUltraProjectOverview(body);
        AppendOfflineSummary(body);
        AppendSaveFeedback(body);
        if (!SignatureEquals(body, developmentGuidanceText.text))
        {
            developmentGuidanceText.text = body.ToString();
            developmentGuidanceText.color = TextPrimary;
        }
        RefreshDevelopmentGuidanceLayout();
        if (!developmentGuidanceRuntimeGeometryLogged)
        {
            Vector2 rect = developmentGuidanceText.rectTransform.rect.size;
            if (rect.x > 0f && rect.y > 0f)
            {
                developmentGuidanceRuntimeGeometryLogged = true;
                Debug.Log($"[王国界面] Development guidance rendered after layout: rect={rect}, textLength={developmentGuidanceText.text.Length}");
            }
        }
    }

    private void AppendUltraProjectOverview(StringBuilder body)
    {
        GameManager gameManager = gameManagerCache;
        UltraProjectManager manager = gameManager == null ? null : gameManager.UltraProject;
        if (body == null || gameManager == null || gameManager.State == null ||
            gameManager.State.TechLevel < TechLevel.Ultra || manager == null)
            return;

        UltraProjectPreview preview;
        try
        {
            preview = manager.GetPreview();
        }
        catch (Exception exception)
        {
            if (!developmentGuidanceErrorLogged)
            {
                developmentGuidanceErrorLogged = true;
                Debug.LogException(exception);
            }
            return;
        }
        if (preview == null)
            return;

        body.Append("\n\n文明工程：").Append(GetUltraStageLabel(preview.Stage))
            .Append("\n状态：").Append(GetUltraStatusLabel(preview.Status));
        if (preview.Stage != UltraProjectStage.Completed)
            body.Append("  进度：").Append(
                (preview.Progress * new ExpantaNum(100)).ToGameString()).Append('%');

        string blocker = string.Empty;
        string nextStep;
        switch (preview.Status)
        {
            case UltraProjectStatus.Locked:
            case UltraProjectStatus.Ready:
                if (preview.Failure != UltraProjectOperationFailure.None)
                    blocker = GetUltraFailureLabel(preview.Failure);
                nextStep = preview.Failure == UltraProjectOperationFailure.None
                    ? "前往所需巨构详情启动本阶段"
                    : "先排除阻碍，再启动本阶段";
                break;
            case UltraProjectStatus.Running:
                if (preview.SupplySatisfaction < ExpantaNum.One)
                    blocker = "当前供给满足率 " +
                        (preview.SupplySatisfaction * new ExpantaNum(100)).ToGameString() +
                        "%；工程按满足率推进";
                nextStep = preview.SupplySatisfaction < ExpantaNum.One
                    ? "补足食物、电力、物流或高级材料以加快推进"
                    : "维持巨构供给，等待本阶段完成";
                break;
            case UltraProjectStatus.Paused:
                bool supplyRecovered =
                    preview.Failure == UltraProjectOperationFailure.InsufficientSupply &&
                    preview.SupplySatisfaction > ExpantaNum.Zero;
                if (supplyRecovered)
                    blocker = "之前供给不足；当前可按满足率渐进推进";
                else if (preview.Failure != UltraProjectOperationFailure.None)
                    blocker = GetUltraFailureLabel(preview.Failure);
                else
                    blocker = "手动暂停";
                nextStep = supplyRecovered
                    ? "供给已恢复，在巨构详情恢复工程"
                    : preview.Failure == UltraProjectOperationFailure.InsufficientSupply
                    ? "补足供给后，在巨构详情恢复工程"
                    : "前往巨构详情恢复工程";
                break;
            case UltraProjectStatus.ReadyToCommit:
                nextStep = "前往巨构详情提交阶段认证";
                break;
            case UltraProjectStatus.Committed:
                nextStep = !HasRemainingInterstellarTargets() ? "现有工程与远星目标已完成，前往文明记忆回顾成果" : manager.IsCampaignDoctrineUnlocked
                    ? "前往区划查看并切换远征供给姿态"
                    : "文明工程认证已完成";
                break;
            default:
                nextStep = "查看文明工程状态";
                break;
        }

        body.Append("\n阻碍：").Append(
            string.IsNullOrEmpty(blocker) ? "暂无" : blocker)
            .Append("\n下一步：").Append(nextStep);
    }

    private void RefreshDevelopmentGuidanceLayout()
    {
        RectTransform card = developmentGuidanceText == null
            ? null
            : developmentGuidanceText.rectTransform.parent as RectTransform;
        if (card == null)
            return;
        developmentGuidanceText.ForceMeshUpdate();
        LayoutRebuilder.ForceRebuildLayoutImmediate(card);
    }

    private static string BuildOverviewEraTarget(GameManager gameManager)
    {
        if (gameManager == null || gameManager.State == null)
            return "时代目标请查看 Era 页面。";

        TechLevel targetEra = (TechLevel)((int)gameManager.State.TechLevel + 1);
        return EraGoalEvaluator.FindTransition(targetEra) == null
            ? "当前内容已到最后时代。"
            : targetEra.GetDescription() + "（详细条件请查看 Era 页面）";
    }

    private void ConfigureDevelopmentGuidanceNavigation(TutorialSnapshot snapshot)
    {
        if (developmentGuidanceNavigationButton == null)
            return;
        string pageName = snapshot == null ? string.Empty : snapshot.NavigationPage;
        string targetId = snapshot == null ? string.Empty : snapshot.NavigationTargetId;
        if (TryGetUltraOverviewNavigation(out string ultraPageName, out string ultraTargetId))
        {
            pageName = ultraPageName;
            targetId = ultraTargetId;
        }
        bool canNavigate = !string.IsNullOrWhiteSpace(pageName) && pages.ContainsKey(pageName);
        developmentGuidanceNavigationButton.interactable = canNavigate;
        TMP_Text label = developmentGuidanceNavigationButton.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = pageName == "Overview" ? "查看当前目标" : "前往：" + PageLabel(pageName);
            label.enabled = true;
        }
        developmentGuidanceNavigationButton.onClick.RemoveAllListeners();
        if (canNavigate)
            developmentGuidanceNavigationButton.onClick.AddListener(() =>
            {
                UIButtonSoundManager.Play(UIButtonSoundManager.Sound.Detail);
                NavigateToTutorialTarget(snapshot, pageName, targetId);
            });
    }

    private bool TryGetUltraOverviewNavigation(out string pageName, out string targetId)
    {
        pageName = string.Empty;
        targetId = string.Empty;
        GameManager gameManager = gameManagerCache;
        UltraProjectManager project = gameManager == null ? null : gameManager.UltraProject;
        if (gameManager == null || gameManager.State == null || project == null ||
            gameManager.State.TechLevel < TechLevel.Ultra)
            return false;

        if (project.State.Status == UltraProjectStatus.Committed)
        {
            pageName = HasRemainingInterstellarTargets() ? "Sectors" : "Story";
            return pages.ContainsKey(pageName);
        }

        pageName = "Buildings";
        targetId = project.State.CurrentStage == UltraProjectStage.None ||
            project.State.CurrentStage == UltraProjectStage.Prototype
            ? "PhaseEnergyArray"
            : "AutonomousMatterFabricator";
        return pages.ContainsKey(pageName);
    }

    private void NavigateToTutorialTarget(TutorialSnapshot snapshot)
    {
        NavigateToTutorialTarget(snapshot, null, null);
    }

    private void NavigateToTutorialTarget(
        TutorialSnapshot snapshot, string overridePageName, string overrideTargetId)
    {
        string pageName = string.IsNullOrWhiteSpace(overridePageName)
            ? snapshot == null ? string.Empty : snapshot.NavigationPage
            : overridePageName;
        string targetId = string.IsNullOrWhiteSpace(overrideTargetId)
            ? snapshot == null ? string.Empty : snapshot.NavigationTargetId
            : overrideTargetId;
        if (string.IsNullOrWhiteSpace(pageName) || !pages.ContainsKey(pageName))
            return;

        SetPage(pageName);
        if (string.IsNullOrWhiteSpace(targetId))
            return;

        if (pageName == "Research" &&
            DataBase<Research>.TryFind(targetId, out Research research) &&
            research != null)
        {
            ShowResearchDetails(research);
            return;
        }
        if (pageName == "Buildings" &&
            DataBase<Building>.TryFind(targetId, out Building building) &&
            building != null)
        {
            ShowBuildingDetails(building);
            return;
        }
        if (pageName == "Resources" &&
            DataBase<Resource>.TryFind(targetId, out Resource resource) &&
            resource != null)
        {
            ShowResourceDetails(resource);
            return;
        }
        if (pageName == "Sectors" &&
            DataBase<SectorDefinition>.TryFind(targetId, out SectorDefinition sector) &&
            sector != null)
        {
            GameManager game = GameManager.Instance;
            ShowSectorDetails(sector, game.Sectors, game.State, ResourceManager.Instance);
            return;
        }
        if (pageName == "Workshop" &&
            DataBase<WorkshopUpgrade>.TryFind(targetId,
                out WorkshopUpgrade workshop) && workshop != null)
            ShowWorkshopDetails(workshop);
    }

    private void RefreshTopInfo()
    {
        if (topFoodValue == null || topHappinessValue == null ||
            topPopulationValue == null || topTerritoryValue == null ||
            topResearchPowerValue == null || topPowerValue == null ||
            topLogisticsValue == null || topCurrentResearchValue == null)
            return;

        CacheRuntimeManagers();
        GameState state = gameManagerCache == null ? null : gameManagerCache.State;
        if (state == null)
            return;

        string currentDate = GameManager.CalendarDataToString(state.CalendarDays);
        SetTextIfChanged(topKingdomDate, currentDate);
        RefreshUnsafeAreaTickerFeed(state, currentDate);

        ResearchManager researchManager = researchManagerCache;
        ObserveResearchCompletions(researchManager);
        string researchPower = researchManager == null
            ? "0"
            : researchManager.ResearchPower.ToGameString();
        ResearchState activeResearch = researchManager == null
            ? null
            : researchManager.ActiveResearch;
        string currentResearch = activeResearch == null || activeResearch.Definition == null
            ? "\u65e0(0%)"
            : activeResearch.Definition.Label + "(" +
                (activeResearch.ProgressRatio * 100).ToGameString() + "%)";

        SetTopInfoValue(topPowerValue, FormatTopFlow(
            state.PowerProductionRate * state.HappinessRewardMultiplier, CalculateRawFlowDemand(usePower: true)));
        SetTopInfoValue(topLogisticsValue, FormatTopFlow(
            state.LogisticsProductionRate * state.HappinessRewardMultiplier, CalculateRawFlowDemand(usePower: false)));
        SetTopInfoValue(topCurrentResearchValue, currentResearch);

        SetTopInfoValue(topFoodValue,
            state.FoodAmount.ToGameString() + "/" + state.FoodCapacity.ToGameString() + "(" +
            state.FoodNetRate.ToGameString(showPositiveSign: true) + "/s 日常；战略需 " +
            (BuildingManager.Instance?.StrategicFoodConsumptionRate ?? ExpantaNum.Zero).ToGameString() + "/s)");
        SetTopInfoValue(topHappinessValue,
            (state.HappinessMultiplier * 100).ToGameString() + "%(" +
            state.HappinessMultiplier.ToGameString() + "x)");
        SetTopInfoValue(topPopulationValue,
            state.Population.Population.ToGameString() + "/" +
            state.Population.PopulationCapacity.ToGameString() + "(" +
            gameManagerCache.CurrentPopulationNetRatePerSecond.ToGameString(
                showPositiveSign: true) + "/s)");
        SetTopInfoValue(topTerritoryValue,
            state.AvailableTerritory.ToGameString() + "/" + state.TerritoryTotal.ToGameString());
        SetTopInfoValue(topResearchPowerValue, researchPower + "/s");
    }

#if UNITY_EDITOR
    public static string FormatTopFlowForEditor(ExpantaNum supply, ExpantaNum demand) =>
        FormatTopFlow(supply, demand);
    public static ExpantaNum CalculateRawFlowDemandForEditor(bool usePower) =>
        CalculateRawFlowDemand(usePower);
    public void RefreshTopInfoForEditor() => RefreshTopInfo();
    public void RefreshLiveCardValuesForEditor() => RefreshLiveCardValues();
    public void EnqueueRecentNoticeForEditor(string notice) => EnqueueRecentNotice(notice);
#endif

    private static string FormatTopFlow(ExpantaNum supply, ExpantaNum demand)
    {
        ExpantaNum balance = supply - demand;
        Color balanceColor = balance > ExpantaNum.Zero ? Positive :
            balance < ExpantaNum.Zero ? Error : TextSecondary;
        string balanceText = "<color=#" +
            ColorUtility.ToHtmlStringRGB(balanceColor) + ">" +
            balance.ToGameString(showPositiveSign: true) + "</color>";
        return balanceText + "(" + demand.ToGameString() + "/" +
            supply.ToGameString() + ")";
    }

    private static ExpantaNum CalculateRawFlowDemand(bool usePower)
    {
        BuildingManager manager = BuildingManager.Instance;
        if (manager == null)
            return ExpantaNum.Zero;
        return usePower ? manager.TotalPowerDemand : manager.TotalLogisticsDemand;
    }

    private static void SetTopInfoValue(TMP_Text field, string value)
    {
        if (field == null)
            return;
        string authoredLabel = field.text;
        int authoredSeparator = authoredLabel.IndexOf('\uFF1A');
        if (authoredSeparator < 0)
            authoredSeparator = authoredLabel.IndexOf(':');
        if (authoredSeparator >= 0)
        {
            SetTextIfChanged(
                field,
                authoredLabel.Substring(0, authoredSeparator) +
                "\uFF1A" + value);
            return;
        }
        int lineBreak = authoredLabel.IndexOf('\n');
        if (lineBreak >= 0)
            authoredLabel = authoredLabel.Substring(0, lineBreak);
        int separator = authoredLabel.IndexOf('：');
        if (separator < 0)
            separator = authoredLabel.IndexOf(':');
        if (separator >= 0)
            authoredLabel = authoredLabel.Substring(0, separator);
        string display = string.IsNullOrEmpty(authoredLabel)
            ? value
            : authoredLabel + "：" + value;
        SetTextIfChanged(field, display);
    }

    private void ObserveResearchCompletions(ResearchManager manager)
    {
        if (manager == null)
            return;

        foreach (KeyValuePair<Research, ResearchState> entry in manager.States)
        {
            Research research = entry.Key;
            ResearchState state = entry.Value;
            if (research == null || state == null ||
                state.Status != ResearchStatus.Completed ||
                string.IsNullOrEmpty(research.Id))
                continue;

            bool wasObserved = observedCompletedResearchIds.Contains(research.Id);
            observedCompletedResearchIds.Add(research.Id);
            if (researchCompletionObservationInitialized && !wasObserved)
            {
                EnqueueRecentNotice(research.AdvancesTechLevel
                    ? "文明跃迁：" + research.Label + " 完成，王国已进入" +
                        research.TechLevel.GetDescription() + "。"
                    : "知识完成：" + research.Label +
                        "：" + TutorialManager.DescribeResearchRole(research));
            }
        }

        researchCompletionObservationInitialized = true;
    }

    private void ObserveBuildingCompletions(BuildingManager manager)
    {
        if (buildingObservationSource != manager)
        {
            buildingObservationSource = manager;
            observedBuildingAmounts.Clear();
            buildingObservationInitialized = false;
        }
        if (manager == null)
            return;

        foreach (KeyValuePair<Building, BuildingState> entry in manager.States)
        {
            Building building = entry.Key;
            BuildingState state = entry.Value;
            if (building == null || state == null || string.IsNullOrEmpty(building.Id))
                continue;

            ExpantaNum amount = state.Amount;
            if (buildingObservationInitialized &&
                observedBuildingAmounts.TryGetValue(building.Id, out ExpantaNum previous) &&
                amount > previous)
            {
                EnqueueRecentNotice("建设完成：" + building.Label +
                    "；" + TutorialManager.DescribeBuildingRole(building));
            }
            observedBuildingAmounts[building.Id] = amount;
        }

        buildingObservationInitialized = true;
    }

    private void ObservePopulationGrowth(GameManager manager)
    {
        if (manager == null || manager.State == null ||
            manager.State.Population == null)
            return;

        ExpantaNum wholePopulation = manager.State.Population.Population.Floor();
        if (populationObservationInitialized && !populationGrowthNoticeSent &&
            observedPopulationWhole <= ExpantaNum.Zero &&
            wholePopulation > ExpantaNum.Zero)
        {
            EnqueueRecentNotice("人口开始增长；食物、幸福度与人口容量已经形成了可持续的族群基础。");
            populationGrowthNoticeSent = true;
        }

        observedPopulationWhole = wholePopulation;
        populationObservationInitialized = true;
    }

    private void EnqueueRecentNotice(string notice)
    {
        if (string.IsNullOrWhiteSpace(notice))
            return;

        // Keep a small ordered window so simultaneous research/building/story
        // events do not overwrite one another, while the Overview and Story
        // still consume the same concise feedback text.
        string normalized = notice.Trim();
        if (recentActionFeedbackEntries.Count >= 3)
            recentActionFeedbackEntries.RemoveAt(0);
        recentActionFeedbackEntries.Add(normalized);
        recentActionFeedback = string.Join("；", recentActionFeedbackEntries);
        recentActionFeedbackVersion = TutorialManager.Current == null
            ? -1 : TutorialManager.Current.SaveSessionVersion;
        RefreshUnsafeAreaTickerFeed();
        developmentGuidanceRefreshTimer = Mathf.Max(
            developmentGuidanceRefreshTimer, 2f);
    }

    private void ClearRecentActionFeedback()
    {
        recentActionFeedbackEntries.Clear();
        recentActionFeedback = string.Empty;
        recentActionFeedbackVersion = -1;
        RefreshUnsafeAreaTickerFeed();
    }

    private void RefreshUnsafeAreaTickerFeed()
    {
        CacheRuntimeManagers();
        GameState state = gameManagerCache == null ? null : gameManagerCache.State;
        string date = state == null
            ? string.Empty
            : GameManager.CalendarDataToString(state.CalendarDays);
        RefreshUnsafeAreaTickerFeed(state, date);
    }

    private void RefreshUnsafeAreaTickerFeed(GameState state, string date)
    {
        if (unsafeAreaTicker == null)
            return;

        TechLevel era = state == null ? TechLevel.Animal : state.TechLevel;
        // The edge ticker is ambient world news only. Gameplay feedback such as
        // "+5 population" remains in the SafeArea "recently happened" panel.
        unsafeAreaTicker.SetFeed(null, era, date);
    }

    private void ResetRecentActionStateForNewSave()
    {
        ClearRecentActionFeedback();
        observedBuildingAmounts.Clear();
        buildingObservationInitialized = false;
        buildingObservationSource = null;
        observedPopulationWhole = ExpantaNum.Zero;
        populationObservationInitialized = false;
        populationGrowthNoticeSent = false;
        storyNotifiedChapterIds.Clear();
        storyObservedUnlockCount = -1;
        storyObservedEraInitialized = false;
    }

    private void RefreshLiveCardValues()
    {
        // These dictionaries contain rows from pages that are not currently
        // visible. Formatting every building/resource on every live tick
        // creates avoidable ExpantaNum strings and TMP dirties, especially
        // while the research graph owns a touch gesture.
        if (populatedPage == "Buildings" && BuildingManager.Instance != null)
            foreach (KeyValuePair<Building, TMP_Text> pair in buildingAmountLabels)
                if (pair.Value != null)
                    if (BuildingManager.Instance.States.TryGetValue(pair.Key, out BuildingState state))
                    {
                        SetTextIfChanged(pair.Value, state.Amount.ToGameString());
                        if (buildingEffectLabels.TryGetValue(pair.Key, out TMP_Text effectLabel) && effectLabel != null)
                            SetTextIfChanged(effectLabel, FormatBuildingEfficiency(state));
                        bool hasBuildingAmount = state.Amount > ExpantaNum.Zero;
                        if (buildingDeconstructButtons.TryGetValue(pair.Key, out Button deconstructButton) && deconstructButton != null)
                        {
                            SetBuildingActionButtonText(
                                deconstructButton,
                                "\u62c6\u9664x" + GetSelectedBuildingQuantity(pair.Key, false, true).ToGameString());
                            SetBuildingActionButtonState(deconstructButton, hasBuildingAmount);
                        }
                        else if (buildingDeconstructSurfaces.TryGetValue(pair.Key, out Image deconstructSurface) && deconstructSurface != null)
                            SetColorIfChanged(deconstructSurface, hasBuildingAmount ? Error : Panel);
                        if (buildingActionButtons.TryGetValue(pair.Key, out Button actionButton) && actionButton != null &&
                            buildingActionUpgradeModes.TryGetValue(pair.Key, out bool upgrade))
                        {
                            ExpantaNum actionQuantity = GetSelectedBuildingQuantity(pair.Key, upgrade, false);
                            SetBuildingActionButtonText(
                                actionButton,
                                (upgrade ? "\u5347\u7ea7x" : "\u5efa\u9020x") +
                                actionQuantity.ToGameString());
                            SetBuildingActionButtonState(
                                actionButton,
                                CanPerformBuildingAction(pair.Key, upgrade, actionQuantity));
                        }
                    }
        ResourceManager resourceManager = ResourceManager.Instance;
        if (resourceManager == null)
            return;
        if (CaptureVisibleResourceIds(DataBase<Resource>.All))
        {
            resourceRowsBuilt = false;
            if (populatedPage == "Resources")
            {
                resourceAmountLabels.Clear();
                resourceChangeLabels.Clear();
                PopulatePage("Resources");
            }
        }
        if (populatedPage != "Resources")
            return;
        ExpantaNum productionRewardMultiplier = ExpantaNum.One;
        bool productionRewardMultiplierRead = false;
        foreach (KeyValuePair<Resource, TMP_Text> pair in resourceAmountLabels)
        {
            if (!resourceManager.States.TryGetValue(pair.Key, out ResourceState state))
                continue;
            if (pair.Value != null)
                SetTextIfChanged(pair.Value, state.Amount.ToGameString());
            if (resourceChangeLabels.TryGetValue(pair.Key, out TMP_Text changeLabel) && changeLabel != null)
            {
                if (!productionRewardMultiplierRead)
                {
                    productionRewardMultiplier = ResourceManager.GetHappinessRewardMultiplier();
                    productionRewardMultiplierRead = true;
                }
                ExpantaNum net = NormalizeDisplayedNetRate(
                    state.ProductionRate * productionRewardMultiplier,
                    state.ConsumptionRate);
                SetTextIfChanged(changeLabel, (net >= ExpantaNum.Zero ? "+" : string.Empty) + net.ToGameString() + "/s");
                SetColorIfChanged(changeLabel, net >= ExpantaNum.Zero ? Positive : Error);
            }
        }
    }
}
