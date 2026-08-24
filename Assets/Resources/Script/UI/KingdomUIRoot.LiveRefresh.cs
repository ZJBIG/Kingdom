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
    private string lastDevelopmentGuidanceNavigationPage;
    private bool developmentGuidanceNavigationInitialized;

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
        topStatusRefreshTimer += Time.unscaledDeltaTime;
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
        if (navigationVisibilityRefreshTimer >= 1f)
        {
            navigationVisibilityRefreshTimer = 0f;
            RefreshNavigationVisibility();
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
        // The top bar is presentation-only and formats several ExpantaNum
        // values, including a scan of building productivity. Two updates per
        // second are sufficient for presentation and avoid repeating that
        // work at the 10 Hz simulation/UI cadence.
        if (topStatusRefreshTimer >= 0.5f)
        {
#if UNITY_EDITOR
            float branchStart = Time.realtimeSinceStartup;
            long topStatusAllocatedStart = GC.GetAllocatedBytesForCurrentThread();
#endif
            RefreshTopStatus();
#if UNITY_EDITOR
            uiRefreshAllocatedBytes += Math.Max(0L, GC.GetAllocatedBytesForCurrentThread() - topStatusAllocatedStart);
            uiRefreshMaximumAllocatedBytes = Math.Max(uiRefreshMaximumAllocatedBytes, GC.GetAllocatedBytesForCurrentThread() - topStatusAllocatedStart);
            RecordUiBranch("top", (Time.realtimeSinceStartup - branchStart) * 1000f);
#endif
            topStatusRefreshTimer = 0f;
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
            researchQueueUiDirty = false;
        }
        if (populatedPage == "Overview" &&
            (developmentGuidanceSnapshot == null || developmentGuidanceRefreshTimer >= 2f))
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
        else if (refreshScrolledValues && selectedWorkshop != null)
        {
            RefreshRequirementRows(selectedWorkshop.ResourceRequirements);
            ConfigureWorkshopPaymentButton(selectedWorkshop, false);
            if (!pageScrolling && workshopRowsUiDirty && populatedPage == "Workshop")
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
        // rebuild. This avoids the 79-node structural scan.
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
        if (version == lastSelectedBuildingVersion && upgrading == lastSelectedBuildingUpgrade &&
            resourceVersion == lastSelectedBuildingResourceVersion)
            return false;
        lastSelectedBuildingVersion = version;
        lastSelectedBuildingUpgrade = upgrading;
        lastSelectedBuildingResourceVersion = resourceVersion;
        return true;
    }

    private void BuildDevelopmentGuidance(RectTransform page)
    {
        if (page == null)
            return;
        developmentGuidanceText = page.Find("PrimaryCard/Text")?.GetComponent<TMP_Text>();
        if (developmentGuidanceText == null)
        {
            Debug.LogError("[王国界面] Overview PrimaryCard/Text is missing; development guidance cannot render.");
            return;
        }
        developmentGuidanceText.enabled = true;
        developmentGuidanceText.gameObject.SetActive(true);
        developmentGuidanceText.alignment = TextAlignmentOptions.TopLeft;
        developmentGuidanceText.fontSize = 24f;
        developmentGuidanceText.enableAutoSizing = true;
        developmentGuidanceText.fontSizeMin = 14f;
        developmentGuidanceText.fontSizeMax = 24f;
        developmentGuidanceText.enableWordWrapping = true;
        developmentGuidanceButton = page.GetComponent<Button>();
        if (developmentGuidanceButton == null)
            developmentGuidanceButton = page.gameObject.AddComponent<Button>();
        Image guidanceHitSurface = page.GetComponent<Image>();
        if (guidanceHitSurface == null)
            guidanceHitSurface = page.gameObject.AddComponent<Image>();
        guidanceHitSurface.color = Color.clear;
        guidanceHitSurface.raycastTarget = true;
        developmentGuidanceButton.targetGraphic = guidanceHitSurface;
        developmentGuidanceButton.transition = Selectable.Transition.None;
        developmentGuidanceButton.interactable = false;
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
                tutorialManager.RecordPageVisited(populatedPage);
                tutorialSnapshot = tutorialManager.Evaluate();
                StringBuilder onboarding = developmentGuidanceTextBuilder;
                onboarding.Clear();
                onboarding.Append("当前时代：").Append(tutorialSnapshot.CurrentEra).Append("\n");
                onboarding.Append("文明复兴：").Append(tutorialSnapshot.CivilizationContext).Append("\n");
                onboarding.Append("人口：").Append(tutorialSnapshot.PopulationText)
                    .Append("  食物：").Append(tutorialSnapshot.FoodText)
                    .Append("  ").Append(tutorialSnapshot.CoreResourceText).Append("\n");
                if (!string.IsNullOrWhiteSpace(tutorialSnapshot.CompletedGoal))
                    onboarding.Append("上一步已完成：").Append(tutorialSnapshot.CompletedGoal).Append("\n");
                onboarding.Append("当前目标：").Append(tutorialSnapshot.CurrentGoal).Append("\n");
                if (!string.IsNullOrWhiteSpace(tutorialSnapshot.NarrativeText))
                    onboarding.Append(tutorialSnapshot.NarrativeText).Append("\n");
                onboarding.Append(tutorialSnapshot.GoalDescription).Append("\n");
                onboarding.Append("下一时代目标：").Append(tutorialSnapshot.NextEraGoal).Append("\n");
                onboarding.Append("当前阻碍：").Append(tutorialSnapshot.Blocker).Append("\n");
                onboarding.Append("推荐行动：").Append(tutorialSnapshot.RecommendedAction);
                if (!SignatureEquals(onboarding, developmentGuidanceText.text))
                    developmentGuidanceText.text = onboarding.ToString();
                developmentGuidanceText.color = TextPrimary;
                if (developmentGuidanceButton != null)
                {
                    string destination = tutorialSnapshot.NavigationPage;
                    bool destinationChanged = !developmentGuidanceNavigationInitialized ||
                        !string.Equals(lastDevelopmentGuidanceNavigationPage, destination, StringComparison.Ordinal);
                    if (destinationChanged)
                        developmentGuidanceButton.onClick.RemoveAllListeners();
                    bool wasInteractable = developmentGuidanceButton.interactable;
                    bool canNavigate = pages.ContainsKey(destination) &&
                        !string.Equals(destination, populatedPage, StringComparison.Ordinal);
                    developmentGuidanceButton.interactable = canNavigate;
                    if (destinationChanged || wasInteractable != canNavigate)
                    {
                        if (!destinationChanged)
                            developmentGuidanceButton.onClick.RemoveAllListeners();
                        if (canNavigate)
                            developmentGuidanceButton.onClick.AddListener(() => SetPage(destination));
                        lastDevelopmentGuidanceNavigationPage = destination;
                        developmentGuidanceNavigationInitialized = true;
                    }
                }
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
            return;
        }
        DevelopmentGuidanceSnapshot snapshot = developmentGuidanceSnapshot;
        StringBuilder body = developmentGuidanceTextBuilder;
        body.Clear();
        if (!string.IsNullOrEmpty(snapshot.EraText))
            body.Append(snapshot.EraText).Append("  |  ");
        body.Append(snapshot.Title ?? string.Empty).Append("\n\n");
        body.Append(snapshot.Body ?? string.Empty);
        IReadOnlyList<string> blockers = snapshot.Blockers ?? Array.Empty<string>();
        for (int i = 0; i < blockers.Count && i < 3; i++)
            body.Append("\n- ").Append(blockers[i]);
        if (!SignatureEquals(body, developmentGuidanceText.text))
        {
            developmentGuidanceText.text = body.ToString();
            developmentGuidanceText.color = TextPrimary;
        }
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

    private void RefreshTopStatus()
    {
        if (topKingdomTitle == null || topStatus == null)
            return;
        CacheRuntimeManagers();
        GameManager gameManager = gameManagerCache;
        GameState state = gameManager == null ? null : gameManager.State;

        SetTextIfChanged(topKingdomTitle, state.KingdomName);
        ExpantaNum populationChange = gameManager == null
            ? ExpantaNum.Zero
            : gameManager.CurrentPopulationNetRatePerSecond;

        string signedPopulationChange = populationChange >= ExpantaNum.Zero
            ? "+" + populationChange.ToGameString()
            : populationChange.ToGameString();
        string signedFoodChange;
        try
        {
            ExpantaNum foodNetRate = state.FoodNetRate;
            signedFoodChange = foodNetRate >= ExpantaNum.Zero
                ? "+" + foodNetRate.ToGameString()
                : foodNetRate.ToGameString();
        }
        catch (NullReferenceException exception)
        {
            signedFoodChange = string.Empty;
            if (!topStatusDataErrorLogged)
            {
                topStatusDataErrorLogged = true;
                Debug.LogException(exception);
            }
        }
        ResearchManager researchManager = researchManagerCache;
        ResearchState activeResearch = researchManager == null ? null : researchManager.ActiveResearch;
        Research activeDefinition = activeResearch == null ? null : activeResearch.Definition;

        ResearchState defState = null;
        if (activeDefinition)
            defState = researchManager.GetState(activeDefinition);
        string currentResearch = activeDefinition == null ? "无"
            : (activeDefinition.Label + (defState != null ? $"[{ResearchProgressText(defState, defState.Status)}]" : ""));


        bool calendarKnown = researchManager != null && researchManager.IsResearchCompleted("Calendar");
        string calendar = calendarKnown ? GameManager.CalendarDataToString(state.CalendarDays) : "????/??/??";
        string researchPower = researchManager == null ? "0" : researchManager.ResearchPower.ToGameString();
        // AvailableProductivity calculates TotalProductivity internally. Read
        // the two values once here so the 10 Hz top-bar refresh does not scan
        // every building twice for the same frame.
        BuildingManager buildingManager = buildingManagerCache;
        ExpantaNum totalProductivity = buildingManager == null
            ? ExpantaNum.Zero
            : buildingManager.TotalProductivity;
        ExpantaNum availableProductivity = buildingManager == null
            ? ExpantaNum.Zero
            : totalProductivity - buildingManager.UsedProductivity;
        string topStatusText =
        "科技水平：" + state.TechLevel.GetDescription() +
        "    当前研究：" + currentResearch +
        "    日期：" + calendar +
        "\n食物：" + state.FoodAmount.ToGameString() + "/" + state.FoodCapacity.ToGameString() + "（" + signedFoodChange + "/s）" +
        "    生产力：" + availableProductivity.ToGameString() + "/" + totalProductivity.ToGameString() +
        "    幸福度：" + state.HappinessScore.ToGameString() + $"({state.HappinessMultiplier.ToGameString()}x）" +
        "\n人口：" + state.Population.Population.ToGameString() + "/" + state.Population.PopulationCapacity.ToGameString() + "（" + signedPopulationChange + "/s）" +
        "    领土：" + state.AvailableTerritory.ToGameString() + "/" + state.TerritoryTotal.ToGameString() +
        "    研究力：" + researchPower + "/s";
        SetTextIfChanged(topStatus, topStatusText);
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
                        else
                            SetTextIfChanged(pair.Value, "0");
                    }
        if (populatedPage != "Resources")
            return;
        ResourceManager resourceManager = ResourceManager.Instance;
        if (resourceManager == null)
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
                ExpantaNum net = state.ProductionRate * productionRewardMultiplier - state.ConsumptionRate;
                SetTextIfChanged(changeLabel, (net >= ExpantaNum.Zero ? "+" : string.Empty) + net.ToGameString() + "/s");
                SetColorIfChanged(changeLabel, net >= ExpantaNum.Zero ? Positive : Error);
            }
        }
    }
}
