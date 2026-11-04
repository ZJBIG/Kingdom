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
    private readonly StringBuilder overviewFeedbackBuilder = new(256);
    private readonly StringBuilder selectedBuildingPrerequisiteSignatureBuilder = new(128);
    private string lastSelectedBuildingPrerequisiteSignature;
    private bool selectedBuildingPrerequisiteOnlyRefresh;
    private WorkshopUpgradeDef lastSelectedBuildingStage;
    private ExpantaNum lastSelectedBuildingPowerSupply;
    private ExpantaNum lastSelectedBuildingPowerDemand;
    private ExpantaNum lastSelectedBuildingLogisticsSupply;
    private ExpantaNum lastSelectedBuildingLogisticsDemand;
    private ExpantaNum lastSelectedBuildingHappiness;
    private ExpantaNum lastSelectedBuildingHappinessReward;
    private ExpantaNum lastSelectedBuildingEventProduction;
    private float nextAutomaticTutorialEvaluation;
    private readonly HashSet<string> observedTutorialFeedbackIds = new(StringComparer.Ordinal);
    private bool developmentGuidanceInitialized;
    private string lastDevelopmentGuidanceLayoutText;
    private float lastDevelopmentGuidanceLayoutWidth;
    private bool lastDevelopmentGuidanceNavigationVisible;

    private void Update()
    {
        if (RefreshLoadingState()) return;
        RefreshOfflineAccelerationToolbar();
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
        RefreshUI(false);
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

    public void RefreshUI() => RefreshUI(true);

    private void RefreshUI(bool forceTutorialEvaluation)
    {
#if UNITY_EDITOR
        float refreshStart = Time.realtimeSinceStartup;
#endif
        CacheRuntimeManagers();
        RefreshEraSurfaces();
        // Keep onboarding and story unlocks current on the page where the
        // player acted. Overview, Era and Story render the resulting snapshot
        // later, but the progression state must not wait for a page switch.
        TutorialManager tutorial = RefreshTutorialSnapshot(forceTutorialEvaluation);
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
                    "；前往剧情页展开阅读。");
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
            if (populatedPage == "Production") RefreshProductionVisibilityIfNeeded();
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
            (forceTutorialEvaluation || !developmentGuidanceInitialized || developmentGuidanceRefreshTimer >= 2f))
        {
            RefreshDevelopmentGuidance();
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
            ConfigureWorkshopPaymentButton(selectedWorkshop);
            if (!pageScrolling && (populatedPage == "Workshop" || populatedPage == "Sectors") &&
                eraPageRefreshTimer >= 1f)
            {
                if (SetWorkshopDetailBody(selectedWorkshop))
                    ResizeDetailBodyIfNeeded(selectedWorkshop.ResourceRequirements?.Count ?? 0, 0);
                eraPageRefreshTimer = 0f;
            }
        }
        else if (selectedResearchNode != null && populatedPage != "Research" &&
            researchDetailLiveRefreshTimer >= 0.75f && (!pageScrolling || refreshScrolledValues))
        {
            if (!pageScrolling)
            {
                string signature = BuildResearchDetailSignature();
                if (!string.Equals(lastResearchDetailSignature, signature, StringComparison.Ordinal))
                {
                    RefreshSelectedResearchDetails(selectedResearchNode);
                    lastResearchDetailSignature = signature;
                }
            }
            RefreshResearchDetailLiveValues(selectedResearchNode);
            researchDetailLiveRefreshTimer = 0f;
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
        if (!pageScrolling && populatedPage == "Resources" && !resourceRowsBuilt)
        {
            SaveCurrentPagePosition();
            PopulatePage("Resources");
        }
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

    private TutorialManager RefreshTutorialSnapshot(bool forceEvaluation)
    {
        TutorialManager tutorial = TutorialManager.Current;
        if (tutorial == null)
        {
            tutorialSnapshot = null;
            tutorialSnapshotSource = null;
            tutorialRecentCompletionFeedback = string.Empty;
            ClearRecentActionFeedback();
            observedTutorialSaveSessionVersion = -1;
            tutorialFeedbackVersion = -1;
            return null;
        }
        bool sessionChanged = tutorialSnapshotSource != tutorial ||
            observedTutorialSaveSessionVersion != tutorial.SaveSessionVersion;
        if (sessionChanged)
        {
            bool hadSession = observedTutorialSaveSessionVersion >= 0;
            tutorialSnapshot = null;
            tutorialSnapshotSource = tutorial;
            tutorialRecentCompletionFeedback = string.Empty;
            ResetRecentActionStateForNewSave();
            observedTutorialSaveSessionVersion = tutorial.SaveSessionVersion;
            tutorialFeedbackVersion = -1;
            HideOnboarding();
            if (hadSession)
            {
                pendingOverviewResearchFocusId = string.Empty;
                researchManagerCache?.SetSelectedResearch(null);
                ShowDetails(string.Empty, string.Empty);
                RefreshResearchTreeVisuals();
            }
        }
        if (recentActionFeedbackVersion >= 0 && recentActionFeedbackVersion != tutorial.SaveSessionVersion)
            ResetRecentActionStateForNewSave();
        if (forceEvaluation || tutorialSnapshot == null || tutorialFeedbackVersion != tutorial.Version ||
            Time.unscaledTime >= nextAutomaticTutorialEvaluation)
        {
            TutorialSnapshot previous = tutorialSnapshot;
            tutorialSnapshot = tutorial.Evaluate();
            nextAutomaticTutorialEvaluation = Time.unscaledTime +
                (string.IsNullOrEmpty(tutorialSnapshot.ActionId) ? .5f : .2f);
            bool actionChanged = previous == null || previous.ActionId != tutorialSnapshot.ActionId ||
                previous.NavigationTargetId != tutorialSnapshot.NavigationTargetId ||
                previous.NavigationPage != tutorialSnapshot.NavigationPage;
            if (actionChanged)
                nextOnboardingRefresh = 0f;
            if (actionChanged || previous.CurrentGoal != tutorialSnapshot.CurrentGoal ||
                previous.Blocker != tutorialSnapshot.Blocker ||
                previous.RecommendedAction != tutorialSnapshot.RecommendedAction ||
                previous.Benefit != tutorialSnapshot.Benefit || previous.NextGoal != tutorialSnapshot.NextGoal ||
                previous.CanAct != tutorialSnapshot.CanAct || previous.IsWaiting != tutorialSnapshot.IsWaiting ||
                previous.CompletedFeedback != tutorialSnapshot.CompletedFeedback ||
                previous.CompletedFeedbackId != tutorialSnapshot.CompletedFeedbackId)
            {
                developmentGuidanceRefreshTimer = 2f;
            }
            if (gameManagerCache != null && gameManagerCache.State != null)
                StoryManager.RefreshProgress(gameManagerCache.State.TechLevel, tutorial);
        }
        tutorialFeedbackVersion = tutorial.Version;
        if (!string.IsNullOrWhiteSpace(tutorialSnapshot.CompletedFeedback))
        {
            string id = tutorialSnapshot.CompletedFeedbackId;
            if (string.IsNullOrEmpty(id) || observedTutorialFeedbackIds.Add(id))
            {
                tutorialRecentCompletionFeedback = tutorialSnapshot.CompletedFeedback;
                if (!string.IsNullOrEmpty(id))
                {
                    onboardingCompletionFeedback = tutorialSnapshot.CompletedFeedback;
                    onboardingCompletionNextGoal = tutorialSnapshot.CurrentGoal;
                    onboardingCompletionUntil = Time.unscaledTime + 4f;
                }
            }
        }
        return tutorial;
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
        ResearchDef selectedEraResearch = selectedResearchNode;
        bool advancedSelectedEra = selectedEraResearch != null && selectedEraResearch.AdvancesTechLevel &&
            selectedEraResearch.TechLevel <= state.TechLevel && IsEraTransitionResearchCompleted(selectedEraResearch);
        if (advancedSelectedEra) selectedEraResearch = eraGoal.Transition;
        PopulatePage("Era", true, eraGoal);
        if (selectedEraResearch != null) ShowResearchDetails(selectedEraResearch, !advancedSelectedEra);
        else if (advancedSelectedEra) ShowDetails("时代进展", "当前时代跃迁已完成。查看中央页面，继续发展王国。");
        if (pageScroll != null)
            pageScroll.verticalNormalizedPosition = normalizedPosition;
    }

    private bool IsPageScrolling()
    {
        if (UIPageScrollDragForwarder.IsRecentlyDragged || StoryBodyScrollRect.IsRecentlyDragged ||
            pageScroll != null && pageScroll.velocity.sqrMagnitude > 0.01f)
            return true;
        if (detailScroll != null && detailScroll.velocity.sqrMagnitude > 0.01f)
            return true;
        if (musicListScroll != null && musicListScroll.velocity.sqrMagnitude > 0.01f)
            return true;
        if (researchGraphGesture != null && researchGraphGesture.IsDragging)
            return true;
        if (productionGesture != null && productionGesture.IsDragging)
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
        Canvas.willRenderCanvases -= SyncOnboardingGeometry;
        HideOnboarding();
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
                " 已生效；在工坊详情查看对应加成。");
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
        // Source availability also depends on other resources and unlocks.
        if (version == lastSelectedResourceVersion && Time.unscaledTime < nextSelectedResourceRefresh) return false;
        lastSelectedResourceVersion = version;
        nextSelectedResourceRefresh = Time.unscaledTime + 1f;
        return true;
    }

    private bool ShouldRefreshSelectedBuilding()
    {
        if (selectedBuilding == null) return false;
        int version = -1;
        BuildingState state = null;
        if (buildingManagerCache != null && buildingManagerCache.States.TryGetValue(selectedBuilding, out state))
            version = state.Version;
        BuildingDef upgradeTarget = null;
        if (state != null && state.Amount > ExpantaNum.Zero && buildingManagerCache != null)
            buildingManagerCache.TryGetUnlockedUpgradeTarget(selectedBuilding, out upgradeTarget);
        ExpantaNum territory = GameManager.Instance.State.AvailableTerritory;
        ExpantaNum productivity = buildingManagerCache == null ? ExpantaNum.Zero : buildingManagerCache.AvailableProductivity;
        ProgressionModifierState modifiers = ProgressionModifierManager.Current;
        int resourceVersion = 0;
        int sourceCount = selectedBuilding.ResourceRequirements.Count;
        int targetCount = upgradeTarget == null ? 0 : upgradeTarget.ResourceRequirements.Count;
        TryGetBuildingDetailStage(selectedBuilding, state, out WorkshopUpgradeDef nextStage);
        int stageCount = nextStage == null ? 0 : nextStage.ResourceRequirements.Count;
        if (resourceManagerCache != null)
            for (int i = 0; i < sourceCount + targetCount + stageCount; i++)
            {
                ResourceDef resource = i < sourceCount ? selectedBuilding.ResourceRequirements[i].First
                    : i < sourceCount + targetCount ? upgradeTarget.ResourceRequirements[i - sourceCount].First
                    : nextStage.ResourceRequirements[i - sourceCount - targetCount].First;
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
        GameState game = GameManager.Instance.State;
        ExpantaNum powerSupply = game.PowerProductionRate * game.HappinessRewardMultiplier;
        ExpantaNum logisticsSupply = game.LogisticsProductionRate * game.HappinessRewardMultiplier;
        ExpantaNum powerDemand = buildingManagerCache == null ? ExpantaNum.Zero : buildingManagerCache.TotalPowerDemand;
        ExpantaNum logisticsDemand = buildingManagerCache == null ? ExpantaNum.Zero : buildingManagerCache.TotalLogisticsDemand;
        bool otherValuesChanged = nextStage != lastSelectedBuildingStage ||
            powerSupply != lastSelectedBuildingPowerSupply || powerDemand != lastSelectedBuildingPowerDemand ||
            logisticsSupply != lastSelectedBuildingLogisticsSupply || logisticsDemand != lastSelectedBuildingLogisticsDemand ||
            game.HappinessMultiplier != lastSelectedBuildingHappiness ||
            game.HappinessRewardMultiplier != lastSelectedBuildingHappinessReward ||
            game.Events.ProductionMultiplier != lastSelectedBuildingEventProduction ||
            version != lastSelectedBuildingVersion ||
            upgradeTarget != lastSelectedBuildingUpgradeTarget ||
            territory != lastSelectedBuildingAvailableTerritory ||
            productivity != lastSelectedBuildingAvailableProductivity ||
            modifiers != lastSelectedBuildingModifiers ||
            resourceVersion != lastSelectedBuildingResourceVersion ||
            ultraInputSignature != lastUltraProjectInputSignature ||
            ultraStateVersion != lastUltraProjectStateVersion;
        if (!otherValuesChanged && !prerequisitesChanged)
            return false;

        lastSelectedBuildingStage = nextStage;
        lastSelectedBuildingPowerSupply = powerSupply;
        lastSelectedBuildingPowerDemand = powerDemand;
        lastSelectedBuildingLogisticsSupply = logisticsSupply;
        lastSelectedBuildingLogisticsDemand = logisticsDemand;
        lastSelectedBuildingHappiness = game.HappinessMultiplier;
        lastSelectedBuildingHappinessReward = game.HappinessRewardMultiplier;
        lastSelectedBuildingEventProduction = game.Events.ProductionMultiplier;
        lastSelectedBuildingVersion = version;
        lastSelectedBuildingUpgradeTarget = upgradeTarget;
        lastSelectedBuildingAvailableTerritory = territory;
        lastSelectedBuildingAvailableProductivity = productivity;
        lastSelectedBuildingModifiers = modifiers;
        lastSelectedBuildingResourceVersion = resourceVersion;
        lastUltraProjectStateVersion = ultraStateVersion;
        lastUltraProjectInputSignature = ultraInputSignature;
        if (prerequisitesChanged)
            lastSelectedBuildingPrerequisiteSignature = prerequisiteSignature.ToString();
        selectedBuildingPrerequisiteOnlyRefresh = prerequisitesChanged && !otherValuesChanged && nextStage == null;
        return true;
    }

    private StringBuilder BuildSelectedBuildingPrerequisiteSignature(BuildingDef building)
    {
        StringBuilder signature = selectedBuildingPrerequisiteSignatureBuilder;
        signature.Clear();
        ResearchManager researchManager = ResearchManager.Instance;
        IReadOnlyList<ResearchDef> researchPrerequisites = building.RequiredResearch;
        signature.Append('R').Append(researchPrerequisites == null ? 0 : researchPrerequisites.Count).Append(':');
        if (researchPrerequisites != null)
            for (int i = 0; i < researchPrerequisites.Count; i++)
            {
                ResearchDef prerequisite = researchPrerequisites[i];
                signature.Append(prerequisite != null && researchManager != null &&
                    researchManager.IsResearchCompleted(prerequisite.Id) ? '1' : '0');
            }

        WorkshopManager workshopManager = WorkshopManager.Instance;
        IReadOnlyList<WorkshopUpgradeDef> workshopPrerequisites = building.RequiredWorkshopUpgrades;
        signature.Append('|').Append('W').Append(workshopPrerequisites == null ? 0 : workshopPrerequisites.Count).Append(':');
        if (workshopPrerequisites != null)
            for (int i = 0; i < workshopPrerequisites.Count; i++)
                signature.Append(workshopPrerequisites[i] != null && workshopManager != null &&
                    workshopManager.IsPurchased(workshopPrerequisites[i]) ? '1' : '0');
        TryGetBuildingDetailStage(building, BuildingManager.Instance?.GetState(building), out WorkshopUpgradeDef nextStage);
        if (nextStage != null)
        {
            signature.Append('|').Append(nextStage.Id).Append(':');
            foreach (ResearchDef required in nextStage.RequiredResearch)
                signature.Append(required != null && researchManager != null &&
                    researchManager.IsResearchCompleted(required.Id) ? '1' : '0');
            foreach (WorkshopUpgradeDef required in nextStage.RequiredUpgrades)
                signature.Append(required != null && workshopManager != null &&
                    workshopManager.IsPurchased(required) ? '1' : '0');
        }
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
                RefreshTutorialSnapshot(false);
                StringBuilder onboarding = developmentGuidanceTextBuilder;
                onboarding.Clear();
                onboarding.Append("<b>聚落下一步</b>\n").Append(tutorialSnapshot.CurrentGoal).Append("\n\n");
                if (!string.IsNullOrWhiteSpace(tutorialSnapshot.Blocker))
                    onboarding.Append("<b>当前条件</b>\n").Append(LinkTutorialMaterial(tutorialSnapshot.Blocker)).Append("\n\n");
                onboarding.Append("<b>现在就做</b>\n").Append(tutorialSnapshot.RecommendedAction);
                if (!string.IsNullOrEmpty(tutorialSnapshot.Benefit))
                    onboarding.Append("\n\n").Append(tutorialSnapshot.Benefit);
                if (!string.IsNullOrEmpty(tutorialSnapshot.NextGoal))
                    onboarding.Append("\n接下来：").Append(tutorialSnapshot.NextGoal);
                AppendUltraProjectOverview(onboarding);
                string overviewText = onboarding.ToString();
                if (!string.Equals(overviewText, developmentGuidanceText.text,
                    StringComparison.Ordinal))
                    developmentGuidanceText.text = overviewText;
                developmentGuidanceText.color = TextPrimary;
                ConfigureDevelopmentGuidanceNavigation(tutorialSnapshot);
                RefreshOverviewFeedback();
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
        body.Append("<b>当前目标</b>\n").Append(snapshot.Title ?? string.Empty).Append("\n\n");
        IReadOnlyList<string> blockers = snapshot.Blockers ?? Array.Empty<string>();
        body.Append("<b>当前阻碍</b>\n");
        if (blockers.Count == 0)
            body.Append("暂无");
        else
            for (int i = 0; i < blockers.Count && i < 3; i++)
            {
                if (i > 0)
                    body.Append("、");
                body.Append(blockers[i]);
            }
        body.Append("\n\n<b>推荐行动</b>\n").Append(snapshot.Body ?? string.Empty);
        AppendUltraProjectOverview(body);
        if (!SignatureEquals(body, developmentGuidanceText.text))
        {
            developmentGuidanceText.text = body.ToString();
            developmentGuidanceText.color = TextPrimary;
        }
        RefreshOverviewFeedback();
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
                    ? "前往星区查看并切换战役姿态"
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
        float width = developmentGuidanceText.rectTransform.rect.width;
        bool navigationVisible = developmentGuidanceNavigationButton != null &&
            developmentGuidanceNavigationButton.gameObject.activeSelf;
        if (developmentGuidanceInitialized &&
            lastDevelopmentGuidanceLayoutText == developmentGuidanceText.text &&
            Mathf.Approximately(lastDevelopmentGuidanceLayoutWidth, width) &&
            lastDevelopmentGuidanceNavigationVisible == navigationVisible)
            return;
        developmentGuidanceText.ForceMeshUpdate();
        LayoutRebuilder.ForceRebuildLayoutImmediate(card);
        developmentGuidanceInitialized = true;
        lastDevelopmentGuidanceLayoutText = developmentGuidanceText.text;
        lastDevelopmentGuidanceLayoutWidth = developmentGuidanceText.rectTransform.rect.width;
        lastDevelopmentGuidanceNavigationVisible = navigationVisible;
    }

    private void RefreshOverviewFeedback()
    {
        if (!pages.TryGetValue("Overview", out RectTransform page)) return;
        Transform card = page.Find("SecondaryCard");
        TMP_Text text = card?.Find("Text")?.GetComponent<TMP_Text>();
        if (text == null) return;
        StringBuilder feedback = overviewFeedbackBuilder;
        feedback.Clear();
        GameState state = gameManagerCache == null ? null : gameManagerCache.State;
        if (state != null)
            feedback.Append("<b>王国现状 · ").Append(state.TechLevel.GetDescription()).Append("</b>\n");
        feedback.Append(DevelopmentGuidance.BuildSituationReport(state, resourceManagerCache?.States.Values));
        AppendOverviewOpportunities(feedback, state);
        int recentHeaderStart = feedback.Length;
        feedback.Append("\n\n<b>最近进展</b>\n");
        int recentContentStart = feedback.Length;
        if (!string.IsNullOrWhiteSpace(tutorialRecentCompletionFeedback))
            feedback.Append(tutorialRecentCompletionFeedback);
        if (!string.IsNullOrWhiteSpace(recentActionFeedback) && recentActionFeedback != tutorialRecentCompletionFeedback)
            feedback.Append("\n").Append(recentActionFeedback);
        if (SaveManager.TryGetInstance(out SaveManager save) && save.LastSaveFailed)
            feedback.Append("\n保存失败：").Append(save.LastSaveError).Append("；请使用保存 / 重试。");
        if (feedback.Length == recentContentStart) feedback.Length = recentHeaderStart;
        string content = feedback.ToString().Trim();
        card.gameObject.SetActive(content.Length > 0);
        if (content.Length > 0 && text.text != content) text.text = content;
    }

    private void AppendOverviewOpportunities(StringBuilder body, GameState state)
    {
        if (state == null) return;
        int shown = 0;
        if (researchManagerCache != null && IsProgressionPageUnlocked("Research"))
        {
            if (researchManagerCache.ActiveResearch == null && researchManagerCache.ResearchQueue.Count == 0)
                body.Append("\n\n研究队列空闲：在研究页选择下一项课题。");
            foreach (ResearchDef definition in DataBase<ResearchDef>.All)
            {
                if (!researchManagerCache.States.TryGetValue(definition, out ResearchState research) ||
                    research.Status != ResearchStatus.Available ||
                    !researchManagerCache.CanAccessResearch(definition)) continue;
                if (shown++ == 0) body.Append("\n可研究："); else body.Append("、");
                body.Append(definition.Label);
                if (shown >= 2) break;
            }
        }
        shown = 0;
        if (workshopManagerCache == null || !workshopManagerCache.IsSystemUnlocked) return;
        foreach (WorkshopUpgradeDef upgrade in DataBase<WorkshopUpgradeDef>.All)
        {
            if (upgrade.TechLevel > state.TechLevel || workshopManagerCache.IsPurchased(upgrade) ||
                !workshopManagerCache.ArePrerequisitesMet(upgrade)) continue;
            if (shown++ == 0) body.Append("\n工坊已解锁未购："); else body.Append("、");
            body.Append(upgrade.Label);
            if (shown >= 2) break;
        }
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
        bool canNavigate = !string.IsNullOrWhiteSpace(pageName) && pages.ContainsKey(pageName) && pageName != "Overview";
        developmentGuidanceNavigationButton.gameObject.SetActive(canNavigate);
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

        if ((pageName == "Research" || pageName == "Era") &&
            DataBase<ResearchDef>.TryFind(targetId, out ResearchDef research) &&
            research != null)
        {
            if (pageName == "Research") NavigateToOverviewResearch(research);
            else ShowResearchDetails(research);
            return;
        }
        if (pageName == "Buildings" &&
            DataBase<BuildingDef>.TryFind(targetId, out BuildingDef building) &&
            building != null)
        {
            ShowBuildingDetails(building);
            return;
        }
        if (pageName == "Resources" &&
            DataBase<ResourceDef>.TryFind(targetId, out ResourceDef resource) &&
            resource != null)
        {
            ShowResourceDetails(resource);
            return;
        }
        if (pageName == "Sectors" &&
            DataBase<SectorDef>.TryFind(targetId, out SectorDef sector) &&
            sector != null)
        {
            GameManager game = GameManager.Instance;
            ShowSectorDetails(sector, game.Sectors, game.State, ResourceManager.Instance);
            return;
        }
        if (pageName == "Workshop" &&
            DataBase<WorkshopUpgradeDef>.TryFind(targetId,
                out WorkshopUpgradeDef workshop) && workshop != null)
            ShowWorkshopDetails(workshop);
    }

    private static string LinkTutorialMaterial(string blocker)
    {
        if (string.IsNullOrEmpty(blocker) || ResourceManager.Instance == null)
            return blocker;
        foreach (ResourceDef resource in DataBase<ResourceDef>.All)
        {
            if (resource == null || string.IsNullOrEmpty(resource.Label))
                continue;
            string shortage = "还缺" + resource.Label + " ";
            int marker = blocker.IndexOf(shortage, StringComparison.Ordinal);
            int index = marker >= 0 ? marker + "还缺".Length : -1;
            if (index < 0 && blocker.StartsWith(resource.Label + "来源已建，", StringComparison.Ordinal))
                index = 0;
            if (index < 0)
                continue;
            return blocker.Substring(0, index) +
                ReviewLink("resource", resource.Id, resource.Label) +
                blocker.Substring(index + resource.Label.Length);
        }
        return blocker;
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

        string currentDate = GetVisibleCalendarDate(state);
        SetTextIfChanged(topKingdomDate, currentDate);
        RefreshUnsafeAreaTickerFeed(state, currentDate);

        ResearchManager researchManager = researchManagerCache;
        ObserveResearchCompletions(researchManager);
        string researchPower = researchManager == null
            ? "0"
            : researchManager.EffectiveResearchPower.ToGameString();

        RefreshTopStatusVisibility();
        BindTopStatusTooltips();
        SetTextIfChanged(topPowerValue, FormatTopFlow(
            state.PowerProductionRate * state.HappinessRewardMultiplier, CalculateRawFlowDemand(usePower: true)));
        SetTextIfChanged(topLogisticsValue, FormatTopFlow(
            state.LogisticsProductionRate * state.HappinessRewardMultiplier, CalculateRawFlowDemand(usePower: false)));
        SetTextIfChanged(topCurrentResearchValue, StableNumber(
            (buildingManagerCache?.UsedProductivity ?? ExpantaNum.Zero).ToGameString() + "/" +
            (buildingManagerCache?.TotalProductivity ?? ExpantaNum.Zero).ToGameString()));

        SetTextIfChanged(topFoodValue,
            StableNumber(state.FoodAmount.ToGameString() + "/" + state.FoodCapacity.ToGameString() + "(" +
                state.FoodNetRate.ToGameString(showPositiveSign: true) + "/s") + " 日常；战略需 " +
            StableNumber((BuildingManager.Instance?.StrategicFoodConsumptionRate ?? ExpantaNum.Zero).ToGameString() + "/s)"));
        SetTextIfChanged(topHappinessValue, StableNumber(FormatTopPercentage(state.HappinessMultiplier * 100) + "%"));
        SetTextIfChanged(topPopulationValue, StableNumber(
            state.Population.Population.ToGameString() + "/" +
            state.Population.PopulationCapacity.ToGameString() + "(" +
            gameManagerCache.CurrentPopulationNetRatePerSecond.ToGameString(
                showPositiveSign: true) + "/s)"));
        SetTextIfChanged(topTerritoryValue, StableNumber(
            state.AvailableTerritory.ToGameString() + "/" + state.TerritoryTotal.ToGameString()));
        SetTextIfChanged(topResearchPowerValue, StableNumber(researchPower + "/s"));
    }

#if UNITY_EDITOR
    public static string FormatTopFlowForEditor(ExpantaNum supply, ExpantaNum demand) =>
        FormatTopFlow(supply, demand);
    public static ExpantaNum CalculateRawFlowDemandForEditor(bool usePower) =>
        CalculateRawFlowDemand(usePower);
    public TutorialSnapshot TutorialSnapshotForEditor => tutorialSnapshot;
    public void RefreshAutomaticUIForEditor() => RefreshUI(false);
    public void RefreshTopInfoForEditor() => RefreshTopInfo();
    public void RefreshLiveCardValuesForEditor() => RefreshLiveCardValues();
    public void EnqueueRecentNoticeForEditor(string notice) => EnqueueRecentNotice(notice);
#endif

    private string GetVisibleCalendarDate(GameState state)
    {
        return researchManagerCache != null && researchManagerCache.IsResearchCompleted("Calendar")
            ? GameManager.CalendarDataToString(state.CalendarDays) : "????/??/??";
    }

    private static string FormatTopPercentage(ExpantaNum value) =>
        value.Abs() < new ExpantaNum(1e6) && (value == ExpantaNum.Zero || value.Abs() >= new ExpantaNum(.001))
            ? value.ToDouble().ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
            : value.ToGameString(significantDigits: 6);

    private static string FormatTopFlow(ExpantaNum supply, ExpantaNum demand)
    {
        ExpantaNum balance = supply - demand;
        Color balanceColor = balance > ExpantaNum.Zero ? Positive :
            balance < ExpantaNum.Zero ? Error : TextSecondary;
        string balanceText = "<color=#" +
            ColorUtility.ToHtmlStringRGB(balanceColor) + ">" +
            balance.ToGameString(showPositiveSign: true) + "</color>";
        return StableNumber(balanceText + "(" + demand.ToGameString() + "/" +
            supply.ToGameString() + ")");
    }

    private static ExpantaNum CalculateRawFlowDemand(bool usePower)
    {
        BuildingManager manager = BuildingManager.Instance;
        if (manager == null)
            return ExpantaNum.Zero;
        return usePower ? manager.TotalPowerDemand : manager.TotalLogisticsDemand;
    }

    private bool topStatusTooltipsBound;
    private void BindTopStatusTooltips()
    {
        if (topStatusTooltipsBound) return;
        BindTopStatusTooltip(topFoodValue, "食物养活居民。数值为库存／储存上限，括号内是每秒日常净变化；负数表示正在消耗存粮。战略需粮列出远行与工程的额外消耗。");
        BindTopStatusTooltip(topHappinessValue, "幸福度影响居民增长与生产。稳定的食物盈余让族人更幸福，断粮则会拖慢发展；各建筑的实际生产效率可在详情中查看。");
        BindTopStatusTooltip(topPopulationValue, "人口／住房容量。居民消耗食物并提供生产力；有空余住房且供粮充足，才有更多族人入住。括号内为每秒人口变化。");
        BindTopStatusTooltip(topTerritoryValue, "空闲土地／领土总量。普通建筑需要占地；土地不足时，研究拓展领土或拆除闲置建筑。");
        BindTopStatusTooltip(topResearchPowerValue, "研究力决定每秒推进多少研究进度。研究所需材料先一次付清，再持续投入研究力；这里已计入当前增益与幸福度。");
        BindTopStatusTooltip(topPowerValue, "电力支撑用电设施。数值为余量（总需求／实际供给）；余量为负时用电建筑减产，应增加供电或停用部分设施。");
        BindTopStatusTooltip(topLogisticsValue, "物流连接生产与远征。数值为余量（总需求／实际供给）；不足时相关设施减产，应增加运输能力或减少需求。");
        BindTopStatusTooltip(topCurrentResearchValue, "生产力：建筑总需求／可供生产力。居民和相关设施提供生产力；不足时建筑按比例减产。增加居民或停用闲置设施，可缓解短缺。");
        topStatusTooltipsBound = true;
    }

    private static void BindTopStatusTooltip(TMP_Text value, string caption)
    {
        if (value != null) value.transform.parent.GetComponent<UITouchTooltip>()?.SetTooltip(caption);
    }

    private void RefreshTopStatusVisibility()
    {
        SetTopStatusVisible(topPowerValue, IsInfrastructureVisible(true));
        SetTopStatusVisible(topLogisticsValue, IsInfrastructureVisible(false));
    }

    private static void SetTopStatusVisible(TMP_Text field, bool visible)
    {
        if (field != null && field.transform.parent.gameObject.activeSelf != visible)
            field.transform.parent.gameObject.SetActive(visible);
    }

    private bool IsInfrastructureVisible(bool power)
    {
        if (buildingManagerCache == null) return false;
        foreach (BuildingDef building in DataBase<BuildingDef>.All)
        {
            if ((power ? building.PowerContribution : building.LogisticsContribution) <= ExpantaNum.Zero)
                continue;
            if (buildingManagerCache.ShouldDisplay(building)) return true;
        }
        return false;
    }

    private void ObserveResearchCompletions(ResearchManager manager)
    {
        if (manager == null)
            return;

        foreach (KeyValuePair<ResearchDef, ResearchState> entry in manager.States)
        {
            ResearchDef research = entry.Key;
            ResearchState state = entry.Value;
            if (research == null || state == null ||
                state.Status != ResearchStatus.Completed ||
                string.IsNullOrEmpty(research.Id))
                continue;

            bool wasObserved = observedCompletedResearchIds.Contains(research.Id);
            observedCompletedResearchIds.Add(research.Id);
            bool tutorialResult = research.Id == "Agriculture" && observedTutorialFeedbackIds.Contains("research") ||
                research.Id == "StoneAgeTransition" && observedTutorialFeedbackIds.Contains("era-goal");
            if (researchCompletionObservationInitialized && !wasObserved && !tutorialResult)
            {
                EnqueueRecentNotice(research.AdvancesTechLevel
                    ? "文明跃迁：" + research.Label + " 完成，王国已进入" +
                        research.TechLevel.GetDescription() + "。"
                    : "研究完成：" + research.Label +
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

        foreach (KeyValuePair<BuildingDef, BuildingState> entry in manager.States)
        {
            BuildingDef building = entry.Key;
            BuildingState state = entry.Value;
            if (building == null || state == null || string.IsNullOrEmpty(building.Id))
                continue;

            ExpantaNum amount = state.Amount;
            if (buildingObservationInitialized &&
                observedBuildingAmounts.TryGetValue(building.Id, out ExpantaNum previous) &&
                amount > previous)
            {
                bool tutorialResult = building.Id == "WoodHouse" && previous <= ExpantaNum.Zero &&
                    observedTutorialFeedbackIds.Contains("building");
                if (!tutorialResult)
                    EnqueueRecentNotice("建设完成：" + building.Label +
                        "；" + TutorialManager.DescribeActualBuildingBenefit(building, manager, gameManagerCache));
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
            if (!observedTutorialFeedbackIds.Contains("population"))
                EnqueueRecentNotice("首位居民已入住；居民提供生产力，也会持续消耗食物。");
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
        if (recentActionFeedbackEntries.Contains(normalized)) return;
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
            : GetVisibleCalendarDate(state);
        RefreshUnsafeAreaTickerFeed(state, date);
    }

    private void RefreshUnsafeAreaTickerFeed(GameState state, string date)
    {
        if (unsafeAreaTicker == null)
            return;

        TechLevel era = state == null ? TechLevel.Animal : state.TechLevel;
        ResearchState activeResearch = researchManagerCache == null
            ? null : researchManagerCache.ActiveResearch;
        if (activeResearch == null && researchManagerCache != null && researchManagerCache.ResearchQueue.Count > 0 &&
            researchManagerCache.ResearchQueue[0].Status == ResearchStatus.WaitingResources)
            activeResearch = researchManagerCache.ResearchQueue[0];
        unsafeAreaTicker.SetFeed(
            UnsafeAreaTickerRules.BuildStatusNotices(state, activeResearch), era, date);
    }

    private void ResetRecentActionStateForNewSave()
    {
        ClearRecentActionFeedback();
        observedTutorialFeedbackIds.Clear();
        onboardingCompletionFeedback = string.Empty;
        onboardingCompletionNextGoal = string.Empty;
        onboardingCompletionUntil = 0f;
        observedCompletedResearchIds.Clear();
        researchCompletionObservationInitialized = false;
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
            foreach (KeyValuePair<BuildingDef, TMP_Text> pair in buildingAmountLabels)
                if (pair.Value != null)
                    if (BuildingManager.Instance.States.TryGetValue(pair.Key, out BuildingState state))
                    {
                        buildingActionUpgradeModes.TryGetValue(pair.Key, out bool upgrade);
                        ExpantaNum actionQuantity = GetSelectedBuildingActionQuantity(pair.Key, upgrade, out bool available);
                        SetTextIfChanged(pair.Value, StableNumber(state.Amount.ToGameString()));
                        if (buildingEffectLabels.TryGetValue(pair.Key, out TMP_Text effectLabel) && effectLabel != null)
                            SetTextIfChanged(effectLabel, FormatBuildingAvailability(state, upgrade, actionQuantity));
                        bool hasBuildingAmount = state.Amount > ExpantaNum.Zero;
                        if (buildingDeconstructButtons.TryGetValue(pair.Key, out Button deconstructButton) && deconstructButton != null)
                        {
                            SetBuildingActionQuantity(
                                deconstructButton, GetSelectedBuildingQuantity(pair.Key, false, true), "拆除");
                            SetBuildingActionButtonState(deconstructButton, hasBuildingAmount);
                        }
                        if (buildingActionButtons.TryGetValue(pair.Key, out Button actionButton) && actionButton != null)
                        {
                            SetBuildingActionQuantity(actionButton, actionQuantity, upgrade ? "升级" : "建造");
                            SetBuildingActionButtonState(
                                actionButton,
                                available);
                        }
                    }
        ResourceManager resourceManager = ResourceManager.Instance;
        if (resourceManager == null)
            return;
        if (CaptureVisibleResourceIds(DataBase<ResourceDef>.All))
            resourceRowsBuilt = false;
        if (populatedPage != "Resources")
            return;
        foreach (KeyValuePair<ResourceDef, TMP_Text> pair in resourceAmountLabels)
        {
            if (!resourceManager.States.TryGetValue(pair.Key, out ResourceState state))
                continue;
            if (pair.Value != null)
                SetTextIfChanged(pair.Value, StableNumber(state.Amount.ToGameString()));
            if (resourceChangeLabels.TryGetValue(pair.Key, out TMP_Text changeLabel) && changeLabel != null)
            {
                ExpantaNum net = resourceManager.GetNetRate(state);
                SetTextIfChanged(changeLabel, StableNumber((net >= ExpantaNum.Zero ? "+" : string.Empty) + net.ToGameString() + "/s"));
                SetColorIfChanged(changeLabel, net >= ExpantaNum.Zero ? Positive : Error);
            }
        }
    }
}
