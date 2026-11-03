using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Runtime root for the Kingdom mobile landscape UI.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-10000)]
public sealed partial class KingdomUIRoot : MonoBehaviour
{
    private enum BuildingQuantityMode
    {
        One,
        Ten,
        Max,
        Custom
    }

    private static readonly Color Panel = new(0.10f, 0.13f, 0.14f, 1f);
    private static readonly Color PanelRaised = new(0.16f, 0.19f, 0.19f, 1f);
    private static readonly Color BasicMetalIngot = new(0.76f, 0.50f, 0.25f, 1f);
    private static readonly Color BuildableActionColor = new Color32(194, 128, 64, 255);
    private static readonly Color TextPrimary = new(0.92f, 0.89f, 0.80f, 1f);
    private static readonly Color TextSecondary = new(0.63f, 0.69f, 0.67f, 1f);
    private static readonly Color Positive = new(0.37f, 0.72f, 0.58f, 1f);
    private static readonly Color Error = new(0.78f, 0.31f, 0.28f, 1f);
    private const float TopBar = 132f;
    private const float Footer = 92f;
    private const float Detail = 640f;
    private const float UiFontSize = 30f;

    private RectTransform safeArea;
    private UnsafeAreaTicker unsafeAreaTicker;
    private RectTransform leftNavigation;
    private RectTransform pageHost;
    private ScrollRect pageScroll;
    private readonly Dictionary<string, float> pageScrollPositions =
        new(StringComparer.Ordinal);
    private Vector2 researchGraphPosition;
    private float researchGraphScale = 1f;
    private bool researchGraphPositionCached;
    private float musicListScrollPosition = 1f;
    private bool musicListScrollPositionCached;
    private TMP_Text pageTitle;
    private TMP_Text detailBody;
    private RectTransform detailPanel;
    private RectTransform detailScrollViewport;
    private RectTransform detailScrollContent;
    private ScrollRect detailScroll;
    private RectTransform requirementHost;
    private RectTransform requirementContent;
    private UIDetailRequirementScrollGesture requirementGesture;
    private RectTransform flowHost;
    private RectTransform flowContent;
    private Button detailActionButton;
    private TMP_Text detailActionButtonText;
    private RectTransform tooltipPanel;
    private TMP_Text tooltipText;
    private TMP_Text topFoodValue;
    private TMP_Text topHappinessValue;
    private TMP_Text topPopulationValue;
    private TMP_Text topTerritoryValue;
    private TMP_Text topResearchPowerValue;
    private TMP_Text topPowerValue;
    private TMP_Text topLogisticsValue;
    private TMP_Text topCurrentResearchValue;
    private TMP_Text topKingdomDate;
    private RectTransform buildingControls;
    private Toggle showBuildingDetailsToggle;
    // ShowDetails toggle is on for the full detail view; this flag tracks the
    // compact alternative used when that toggle is off.
    private bool useCompactBuildingDetails;
    private bool detailBuildingUpgrade;
    private bool detailIsBuilding;
    private BuildingDef selectedBuilding;
    private ResourceDef selectedResource;
    private WorkshopUpgradeDef selectedWorkshop;
    private static TMP_FontAsset sharedFontAsset;
    private float liveRefreshTimer;
    private float topInfoRefreshTimer;
    private float developmentGuidanceRefreshTimer;
    private float scrollingLiveValueRefreshTimer;
    private float researchDetailLiveRefreshTimer;
    private float buildingStructureRefreshTimer;
#if UNITY_EDITOR
    private float uiSlowRefreshLogCooldown;
    private float uiStatsLogCooldown = 5f;
    private int uiRefreshSampleCount;
    private float uiRefreshTotalMilliseconds;
    private float uiRefreshMaximumMilliseconds;
    private long uiRefreshAllocatedBytes;
    private long uiRefreshMaximumAllocatedBytes;
    private float frameStatsLogCooldown = 5f;
    private int frameSampleCount;
    private float frameTotalMilliseconds;
    private float frameMaximumMilliseconds;
    private int slowFrameCount;
    private int touchFrameCount;
    private int maximumTouchCount;
    private int dragFrameCount;
    private int lastGc0Count;
    private int lastGc1Count;
    private int lastGc2Count;
    private long lastFrameAllocatedBytes;
    private long frameAllocatedBytes;
    private bool frameAllocationCounterAvailable;
    private bool perfTouchGestureActive;
    private float perfTouchGestureStartTime;
    private int perfTouchGestureSamples;
    private float perfTouchGestureDistance;
    private Vector2 perfTouchGestureLastPosition;
    private float perfTouchGestureMaximumFrameMilliseconds;
    private int perfTouchGestureSlowFrameCount;
#endif
    // Live refresh runs ten times per second; avoid global scene searches on
    // every presentation tick.
    private GameManager gameManagerCache;
    private ResearchManager researchManagerCache;
    private ResearchManager researchQueueEventSource;
    private bool researchQueueEventSubscribed;
    private bool researchQueueUiDirty;
    private float researchQueuePollTimer;
    private float navigationVisibilityRefreshTimer = 1f;
    private string lastResearchQueuePollSignature;
    private bool researchDynamicUiDirty = true;
    private float researchDynamicSignatureRefreshTimer = 1f;
#if UNITY_EDITOR
    private int researchQueueEventCount;
    private float researchQueueEventLogCooldown;
#endif
    private ResourceManager resourceManagerCache;
    private BuildingManager buildingManagerCache;
    private WorkshopManager workshopManagerCache;
    private WorkshopManager workshopEventSource;
    private bool workshopRowsUiDirty;
    private int lastSelectedResourceVersion = -1;
    private float nextSelectedResourceRefresh;
    private int lastSelectedBuildingVersion = -1;
    private BuildingDef lastSelectedBuildingUpgradeTarget;
    private ExpantaNum lastSelectedBuildingAvailableTerritory;
    private ExpantaNum lastSelectedBuildingAvailableProductivity;
    private ProgressionModifierState lastSelectedBuildingModifiers;
    private int lastSelectedBuildingResourceVersion = -1;
    private readonly HashSet<string> observedCompletedResearchIds = new();
    private bool researchCompletionObservationInitialized;
    private readonly Dictionary<string, ExpantaNum> observedBuildingAmounts = new();
    private bool buildingObservationInitialized;
    private BuildingManager buildingObservationSource;
    private ExpantaNum observedPopulationWhole;
    private bool populationObservationInitialized;
    private bool populationGrowthNoticeSent;
    private readonly Dictionary<string, ExpantaNum> observedSectorProgress = new();
    private readonly Dictionary<string, ExpantaNum> observedSectorCasualties = new();
    private readonly HashSet<string> observedOccupiedSectorIds = new();
    private bool sectorObservationInitialized;
    private bool runtimeGeometryLogged;
    private bool runtimeGeometryDiagnosticLogged;
    private float runtimeGeometryRetryTimer;
    private int runtimeGeometryRetryCount;
    private bool detailGeometryLogged;
    private Vector2 lastDetailViewportSize;
    private Vector2 lastDetailContentSize;
    private BuildingQuantityMode buildingQuantityMode = BuildingQuantityMode.One;
    private ExpantaNum customBuildingQuantity = ExpantaNum.One;
    private TMP_InputField customQuantityInput;
    private static Font sharedSourceFont;
    private readonly Dictionary<BuildingQuantityMode, Button> buildingQuantityButtons = new();
    private readonly Dictionary<string, RectTransform> pages = new();
    private readonly Dictionary<BuildingDef, TMP_Text> buildingAmountLabels = new();
    private readonly Dictionary<BuildingDef, TMP_Text> buildingEffectLabels = new();
    private readonly Dictionary<BuildingDef, Button> buildingDeconstructButtons = new();
    private readonly Dictionary<BuildingDef, Button> buildingActionButtons = new();
    private readonly Dictionary<Button, TMP_Text> buildingActionButtonTexts = new();
    private readonly Dictionary<Button, Outline> buildingActionButtonOutlines = new();
    private readonly Dictionary<BuildingDef, bool> buildingActionUpgradeModes = new();
    private string lastBuildingDisplaySignature;
    private readonly Dictionary<ResourceDef, TMP_Text> resourceAmountLabels = new();
    private readonly Dictionary<ResourceDef, TMP_Text> resourceChangeLabels = new();
    private readonly HashSet<string> visibleResourceIds = new(StringComparer.OrdinalIgnoreCase);
    private RectTransform researchGraphViewport;
    private RectTransform researchGraphContent;
    private CanvasGroup researchPageVisibilityGroup;
    private readonly Dictionary<ResearchDef, Button> researchTreeNodes = new();
    private readonly Dictionary<Button, Color> navigationBaseColors = new();
    private bool researchTreePageBuilt;
    private Coroutine researchTreeWarmupCoroutine;
    private Coroutine storyWarmupCoroutine;
    private string populatedPage;
    private float nextRowTop;
    private bool resourceRowsBuilt;
    private bool buildingRowsBuilt;
    private bool eraRowsBuilt;
    private readonly List<GameObject> workshopRows = new();
    private bool workshopRowsBuilt;
    private bool sectorRowsBuilt;
    private TMP_Text musicTimeLabel;
    private Slider musicProgressSlider;
    private UnityEngine.Events.UnityAction<float> musicProgressSeekHandler;
    private Slider musicVolumeSlider;
    private Slider musicGapSlider;
    private TMP_Text musicVolumeValueLabel;
    private TMP_Text musicGapValueLabel;
    private Slider sfxVolumeSlider;
    private Button sfxMuteButton;
    private TMP_Text sfxMuteLabel;
    private TMP_Text sfxVolumeValueLabel;
    private bool musicProgressDragging;
    private bool musicPageBuilt;
    private TMP_Text developmentGuidanceText;
    private Button developmentGuidanceNavigationButton;
    private TutorialSnapshot tutorialSnapshot;
    private TutorialManager tutorialSnapshotSource;
    private string tutorialRecentCompletionFeedback = string.Empty;
    private string recentActionFeedback = string.Empty;
    private readonly List<string> recentActionFeedbackEntries = new();
    private int recentActionFeedbackVersion = -1;
    private int observedTutorialSaveSessionVersion = -1;
    private int tutorialFeedbackVersion = -1;
    private DevelopmentGuidanceSnapshot developmentGuidanceSnapshot;
    private bool developmentGuidanceErrorLogged;
    private bool developmentGuidanceRuntimeGeometryLogged;
    private string eraPageStateSignature;
    private float eraPageRefreshTimer;
#if UNITY_EDITOR
    public Button DetailActionButtonForEditor => detailActionButton;
    public TMP_Text TopPopulationValueForEditor => topPopulationValue;
    public Dictionary<ResourceDef, TMP_Text> ResourceChangeLabelsForEditor => resourceChangeLabels;
    public string TutorialRecentCompletionFeedbackForEditor
    {
        get => tutorialRecentCompletionFeedback;
        set => tutorialRecentCompletionFeedback = value;
    }
    public string RecentActionFeedbackForEditor
    {
        get => recentActionFeedback;
        set => recentActionFeedback = value;
    }
    public int RecentActionFeedbackVersionForEditor
    {
        get => recentActionFeedbackVersion;
        set => recentActionFeedbackVersion = value;
    }
    public int TutorialFeedbackVersionForEditor
    {
        get => tutorialFeedbackVersion;
        set => tutorialFeedbackVersion = value;
    }
#endif

    private void Awake()
    {
#if UNITY_EDITOR
        KingdomEditorPerfLog.Write("[KingdomPerf] SessionStart ui=research-connector-batch-v1-canvas-group-frame-budget-era-single-force");
#endif
        RectTransform root = transform as RectTransform;
        transform.localScale = Vector3.one;
        if (root != null)
        {
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
        }

        // Normalize the scene root before CanvasScaler performs its first
        // calculation. The legacy scene stores this root as a zero-sized
        // bottom-left RectTransform; configuring the scaler first can produce
        // a negative body/graph viewport during the first layout pass.
        ConfigureP40CanvasScaler();
        Canvas.ForceUpdateCanvases();
        if (!ResolveFont())
            return;
        PreloadResearchTreeAssets();

        HideLegacyChildren();
        Debug.Log("[王国界面] Legacy UI isolated: SafeAreaRoot is the only runtime UI surface");
        BuildVisibleShell();
    }

    private void ConfigureP40CanvasScaler()
    {
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2640f, 1200f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        // The P40 Pro landscape width is the invariant design dimension.
        // Matching width keeps the central page width positive on editor
        // windows while preserving the 2640x1200 target composition.
        scaler.matchWidthOrHeight = 0f;
        Debug.Log($"[王国界面] Canvas configured: mode={scaler.uiScaleMode}, reference={scaler.referenceResolution}, match={scaler.matchWidthOrHeight}");
    }

    private bool ResolveFont()
    {
        if (sharedFontAsset != null)
        {
            Debug.Log($"[王国界面] 复用共享字体：{sharedFontAsset.name}");
            return true;
        }
        Font source = Resources.Load<Font>("Fonts/NotoSansSC-Regular");
        sharedSourceFont = source;
        if (source != null)
        {
            sharedFontAsset = TMP_FontAsset.CreateFontAsset(source);
            // CreateFontAsset defaults to a static atlas in some TMP
            // versions. That atlas contains only the first glyphs and makes
            // Chinese research labels render as empty cards. Keep the
            // project font, but allow TMP to populate glyphs on demand.
            if (sharedFontAsset != null)
            {
                sharedFontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                TMP_FontAsset fallback = TMP_Settings.defaultFontAsset;
                if (fallback != null && fallback != sharedFontAsset &&
                    (sharedFontAsset.fallbackFontAssetTable == null ||
                     !sharedFontAsset.fallbackFontAssetTable.Contains(fallback)))
                {
                    if (sharedFontAsset.fallbackFontAssetTable == null)
                        sharedFontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
                    sharedFontAsset.fallbackFontAssetTable.Add(fallback);
                }
            }
        }
        if (sharedFontAsset == null)
            sharedFontAsset = TMP_Settings.defaultFontAsset;
        if (sharedFontAsset == null)
        {
            Debug.LogError("[王国界面] Required TMP FontAsset is unavailable; UI initialization was aborted.");
            return false;
        }
        Debug.Log($"[王国界面] 字体解析完成：来源={(source == null ? "空" : source.name)}，共享字体={sharedFontAsset.name}，默认字体={(TMP_Settings.defaultFontAsset == null ? "空" : TMP_Settings.defaultFontAsset.name)}");
        return true;
    }

    private void Start() => Invoke(nameof(RebuildCurrentPage), 0.5f);

    private void LateUpdate()
    {
        if (SaveManager.Instance.IsLoading && !SaveManager.Instance.IsOfflineProgressRunning) return;
        TickOnboarding();
        if (populatedPage == "Story")
            KeepStoryTextGeometryDrawable();
        // CanvasScaler and the SafeAreaFitter can complete their first layout
        // pass after Awake. Re-measure until the runtime hierarchy has a real
        // rectangle; this also repairs old scene instances without rebuilding
        // any page content or resetting a user's scroll position.
        if (runtimeGeometryLogged)
            return;

        if (runtimeGeometryRetryCount >= 8)
            return;

        // A failed first layout must not turn into a ForceUpdateCanvases call
        // every frame. That path dirties the whole Canvas hierarchy and can
        // keep allocating indefinitely on an editor/device whose safe-area
        // values settle late. Retry at a bounded cadence until the first
        // valid geometry is observed.
        runtimeGeometryRetryTimer -= Time.unscaledDeltaTime;
        if (runtimeGeometryRetryTimer > 0f)
            return;
        runtimeGeometryRetryTimer = 0.25f;
        runtimeGeometryRetryCount++;
        EnsureRuntimeCanvasGeometry();
    }

    public void ShowTooltip(string message)
    {
        if (tooltipPanel == null || tooltipText == null)
            return;
        tooltipText.text = message;
        tooltipPanel.gameObject.SetActive(true);
        CancelInvoke(nameof(HideTooltip));
        Invoke(nameof(HideTooltip), 4f);
    }

    private void HideTooltip()
    {
        if (tooltipPanel != null)
            tooltipPanel.gameObject.SetActive(false);
    }

    private void RebuildCurrentPage()
    {
        if (SaveManager.Instance.IsLoading && !SaveManager.Instance.IsOfflineProgressRunning) return;
        if (!string.IsNullOrEmpty(populatedPage))
        {
            if (populatedPage == "Research" && researchTreePageBuilt)
            {
                RefreshResearchTreeVisuals();
                return;
            }
            SaveCurrentPagePosition();
            PopulatePage(populatedPage);
        }
    }

    private void HideLegacyChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name != "SafeAreaRoot" && child.name != "UnsafeAreaTicker" && child.name != "EnvironmentBackground")
                child.gameObject.SetActive(false);
        }
    }

    private void BuildVisibleShell()
    {
        bool authoredShell = TryBindAuthoredShell();
        if (!authoredShell)
        {
            Debug.LogError("[王国界面] Authored scene shell is incomplete. Restore the required static objects in KingdomUIRoot.prefab.");
            return;
        }
        if (safeArea.GetComponent<SafeAreaFitter>() == null)
            safeArea.gameObject.AddComponent<SafeAreaFitter>();
        unsafeAreaTicker.Initialize(safeArea, sharedFontAsset);
        RefreshUnsafeAreaTickerFeed();
        Debug.Log("[王国界面] Authored scene shell bound; Detail UI v2 rebuilt and legacy detail UI discarded.");
        EnsureRuntimeCanvasGeometry();
        Canvas.ForceUpdateCanvases();
        Debug.Log($"[王国界面] Panel alignment: navigation={GetRectSize(leftNavigation)}, detail={GetRectSize(detailPanel)}, bottomDelta={GetRectBottom(detailPanel) - GetRectBottom(leftNavigation):0.00}, topDelta={GetRectTop(detailPanel) - GetRectTop(leftNavigation):0.00}");
        SetPage("Overview");
        DiagnoseUiTextRendering("initial");
        researchTreeWarmupCoroutine = StartCoroutine(WarmResearchTreePage());
        storyWarmupCoroutine = StartCoroutine(WarmStoryPage());
    }

    private IEnumerator WarmResearchTreePage()
    {
        // Let the initial overview render first. Build the research page while
        // it is hidden so activating the tab only reuses existing UI objects.
        yield return null;
        while (SaveManager.Instance.IsLoading) yield return null;
        if (researchTreePageBuilt || !pages.TryGetValue("Research", out RectTransform page))
        {
            researchTreeWarmupCoroutine = null;
            yield break;
        }

        bool wasActive = page.gameObject.activeSelf;
        page.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        PopulatePage("Research");
        page.gameObject.SetActive(wasActive);
        ConfigureOuterPageScroll(
            string.IsNullOrEmpty(populatedPage) ? "Overview" : populatedPage,
            false, true);
        researchTreeWarmupCoroutine = null;
        Debug.Log("[王国界面] Research page warmed and cached before first tab activation");
    }

    private IEnumerator WarmStoryPage()
    {
        // Build the narrative archive after the first Overview frame so its
        // TMP/card creation is not paid by the first Story tab click.
        yield return null;
        while (SaveManager.Instance.IsLoading) yield return null;
        if (researchTreeWarmupCoroutine != null)
            yield return researchTreeWarmupCoroutine;
        if (storyPageBuilt || !pages.TryGetValue("Story", out RectTransform page))
        {
            storyWarmupCoroutine = null;
            yield break;
        }

        bool wasActive = page.gameObject.activeSelf;
        page.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        PopulatePage("Story");
        page.gameObject.SetActive(wasActive);
        ConfigureOuterPageScroll(
            string.IsNullOrEmpty(populatedPage) ? "Overview" : populatedPage,
            false, true);
        storyWarmupCoroutine = null;
        Debug.Log("[王国界面] Story page warmed and cached before first tab activation");
    }

    private static Vector2 GetRectSize(RectTransform rect) => rect == null ? Vector2.zero : rect.rect.size;

    private static float GetRectBottom(RectTransform rect) =>
        rect == null ? 0f : rect.TransformPoint(new Vector3(0f, rect.rect.yMin, 0f)).y;

    private static float GetRectTop(RectTransform rect) =>
        rect == null ? 0f : rect.TransformPoint(new Vector3(0f, rect.rect.yMax, 0f)).y;

    private void EnsureRuntimeCanvasGeometry()
    {
        RectTransform root = transform as RectTransform;
        if (root == null)
        {
            Debug.LogError("[王国界面] Required UI root must use a RectTransform: " +
                gameObject.name);
            return;
        }

        // The scene prefab historically carried a zero-sized root override.
        // Reassert the full-screen Canvas rectangle before measuring children;
        // otherwise the safe-area children inherit height zero and their
        // top offsets produce negative panels.
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.zero;
        root.pivot = Vector2.zero;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.sizeDelta = new Vector2(Screen.width, Screen.height);
        root.localScale = Vector3.one;

        if (safeArea != null)
        {
            Rect screenSafeArea = Screen.safeArea;
            Vector2 screenSize = new(Screen.width, Screen.height);
            if (SafeAreaFitter.TryCalculateAnchors(screenSafeArea, screenSize,
                out Vector2 safeMin, out Vector2 safeMax))
            {
                safeArea.anchorMin = safeMin;
                safeArea.anchorMax = safeMax;
                safeArea.offsetMin = Vector2.zero;
                safeArea.offsetMax = Vector2.zero;
            }
        }

        if (detailPanel != null)
        {
            detailPanel.anchorMin = new Vector2(1f, 0f);
            detailPanel.anchorMax = new Vector2(1f, 1f);
            detailPanel.offsetMin = new Vector2(-Detail, 0f);
            detailPanel.offsetMax = new Vector2(-18f, -TopBar);
        }

        Canvas.ForceUpdateCanvases();
        RefreshDetailScrollGeometry();
        if (!runtimeGeometryDiagnosticLogged && (root.rect.width <= 0f || root.rect.height <= 0f || safeArea == null || safeArea.rect.height <= 0f))
        {
            runtimeGeometryDiagnosticLogged = true;
            Debug.Log($"[王国界面] Runtime geometry pending: root={root.rect.size}, safeArea={(safeArea == null ? Vector2.zero : safeArea.rect.size)}, screen={Screen.width}x{Screen.height}, rootScale={root.localScale}");
        }
        if (!runtimeGeometryLogged && safeArea != null &&
            safeArea.rect.width > 0f && safeArea.rect.height > 0f &&
            leftNavigation != null && detailPanel != null &&
            leftNavigation.rect.height > 0f && detailPanel.rect.height > 0f)
        {
            runtimeGeometryLogged = true;
            Debug.Log($"[王国界面] Runtime geometry valid: screen={Screen.width}x{Screen.height}, safeArea={safeArea.rect.size}, navigation={leftNavigation.rect.size}, detail={detailPanel.rect.size}, bottomDelta={GetRectBottom(detailPanel) - GetRectBottom(leftNavigation):0.00}, topDelta={GetRectTop(detailPanel) - GetRectTop(leftNavigation):0.00}");
        }
    }

    // The authored prefab is now the sole source of fixed layout.

    /// <summary>
    /// Player-facing page navigation. Every internal navigation path (navigation
    /// buttons, era cards, story actions, live refresh) funnels through here, so
    /// it is public rather than an editor-only test hook.
    /// </summary>
    public void SetPage(string name)
    {
        if (!IsProgressionPageUnlocked(name)) return;
        if (storyIllustrationPreview != null) storyIllustrationPreview.Hide();
        if (populatedPage != name)
            HideRelicDetails();
#if UNITY_EDITOR
        float pageSwitchStartTime = Time.realtimeSinceStartup;
#endif
        SaveCurrentPagePosition();
        if (name == "Research")
            Debug.Log("[王国界面] SetPage Research");
        foreach (KeyValuePair<string, RectTransform> pair in pages)
        {
            bool shouldBeActive = pair.Key == name;
            if (pair.Key == "Research")
            {
                SetResearchPageVisible(pair.Value, shouldBeActive);
                continue;
            }
            if (pair.Value.gameObject.activeSelf != shouldBeActive)
                pair.Value.gameObject.SetActive(shouldBeActive);
        }
        if (productionTools != null)
            productionTools.gameObject.SetActive(name == "Production");
#if UNITY_EDITOR
        float pageVisibilityDurationMs =
            (Time.realtimeSinceStartup - pageSwitchStartTime) * 1000f;
#endif
        if (pageTitle != null)
        {
            string label = PageLabel(name);
            if (pageTitle.text != label)
                pageTitle.text = label;
            if (pageTitle.fontSize != UiFontSize)
                pageTitle.fontSize = UiFontSize;
            if (!pageTitle.enabled)
                pageTitle.enabled = true;
            if (!pageTitle.gameObject.activeSelf)
                pageTitle.gameObject.SetActive(true);
            RectTransform titleRect = pageTitle.rectTransform;
            if (titleRect.anchorMin != new Vector2(0f, 1f) ||
                titleRect.anchorMax != new Vector2(0f, 1f) ||
                titleRect.pivot != new Vector2(0f, 1f) ||
                titleRect.offsetMin != new Vector2(34f, -88f) ||
                titleRect.offsetMax != new Vector2(230f, -28f))
            {
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(0f, 1f);
                titleRect.pivot = new Vector2(0f, 1f);
                titleRect.offsetMin = new Vector2(34f, -88f);
                titleRect.offsetMax = new Vector2(230f, -28f);
            }
            if (pageTitle.alignment != TextAlignmentOptions.MidlineLeft)
                pageTitle.alignment = TextAlignmentOptions.MidlineLeft;
            if (titleRect.parent != null &&
                titleRect.GetSiblingIndex() != titleRect.parent.childCount - 1)
                titleRect.SetAsLastSibling();
        }
        populatedPage = name;
        CancelReviewConfirmation();
        RefreshReviewControls();
        RefreshNavigationSelection(name);
        RefreshWorkshopFiltersVisibility();
        TutorialManager.Current?.RecordPageVisited(name);
        TutorialManager.Current?.RecordStagePageVisited(name);
        // The page slot is laid out by the parent Canvas before the first
        // generated page is created. Once a page has been built, forcing a
        // complete Canvas rebuild on every tab switch needlessly walks the
        // cached research graph and all of its connector Graphics.
        bool pageHasCachedLayout = name == "Research"
            ? researchTreePageBuilt
            : name == "Music"
                ? musicPageBuilt
                : name == "Story"
                    ? storyPageBuilt
                : name == "Production" ? productionTree != null
                : name == "Overview" || AreAuthoredRowsBuilt(name);
        bool pageNeedsInitialLayout = !pageHasCachedLayout;
        bool pageGeometryUnavailable = pageHost == null ||
            pageHost.rect.width <= 1f || pageHost.rect.height <= 1f;
        bool storyHasMeasuredHost = name == "Story" && pageHost != null &&
            pageHost.rect.width > 1f && pageHost.rect.height > 1f;
        if ((!storyHasMeasuredHost && pageNeedsInitialLayout) || pageGeometryUnavailable)
            Canvas.ForceUpdateCanvases();
        if (name == "Buildings")
        {
            BuildBuildingControls(pageHost.parent.Find("PageTool"));
            RefreshBuildingQuantityHeader();
        }
#if UNITY_EDITOR
        float populateStartTime = Time.realtimeSinceStartup;
#endif
        PopulatePage(name);
        if (name == "Buildings")
            RefreshBuildingDisplayMembership();
        RestoreSpecialPagePosition(name);
#if UNITY_EDITOR
        float populateDurationMs = (Time.realtimeSinceStartup - populateStartTime) * 1000f;
        float pageSwitchDurationMs = (Time.realtimeSinceStartup - pageSwitchStartTime) * 1000f;
        KingdomEditorPerfLog.Write(
            $"[KingdomPerf] PageSwitch page={name} durationMs={pageSwitchDurationMs:0.0} " +
            $"visibilityMs={pageVisibilityDurationMs:0.0} populateMs={populateDurationMs:0.0} " +
            "researchVisibility=canvas-group");
#endif
    }

#if UNITY_EDITOR
    private void DiagnoseUiTextRendering(string reason)
    {
        if (safeArea == null)
            return;

        Canvas.ForceUpdateCanvases();
        TMP_Text[] texts = safeArea.GetComponentsInChildren<TMP_Text>(true);
        int activeCount = 0;
        int nonEmptyCount = 0;
        int characterCount = 0;
        int vertexCount = 0;
        int alphaVisibleCount = 0;
        int suspiciousCount = 0;
        int logged = 0;
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null || !text.gameObject.activeInHierarchy || !text.enabled)
                continue;
            activeCount++;
            text.ForceMeshUpdate(true, true);
            int chars = text.textInfo == null ? 0 : text.textInfo.characterCount;
            int vertices = 0;
            if (text.textInfo != null && text.textInfo.meshInfo != null)
            {
                for (int meshIndex = 0; meshIndex < text.textInfo.meshInfo.Length; meshIndex++)
                    vertices += text.textInfo.meshInfo[meshIndex].vertices == null
                        ? 0 : text.textInfo.meshInfo[meshIndex].vertices.Length;
            }
            float groupAlpha = 1f;
            string groups = "none";
            Transform parent = text.transform.parent;
            while (parent != null)
            {
                CanvasGroup group = parent.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    groupAlpha *= group.alpha;
                    groups = groups == "none" ? group.name + "=" + group.alpha.ToString("0.###") :
                        groups + "," + group.name + "=" + group.alpha.ToString("0.###");
                }
                parent = parent.parent;
            }
            if (!string.IsNullOrEmpty(text.text))
                nonEmptyCount++;
            characterCount += chars;
            vertexCount += vertices;
            float rendererAlpha = text.canvasRenderer == null ? 0f : text.canvasRenderer.GetAlpha();
            if (rendererAlpha > 0f && groupAlpha > 0f)
                alphaVisibleCount++;
            bool suspicious = !string.IsNullOrEmpty(text.text) &&
                (chars == 0 || vertices == 0 || rendererAlpha <= 0f || groupAlpha <= 0f ||
                 text.font == null || text.fontSharedMaterial == null ||
                 text.rectTransform.rect.width <= 0f || text.rectTransform.rect.height <= 0f);
            if (suspicious)
                suspiciousCount++;
            if (logged < 12 && (!string.IsNullOrEmpty(text.text) || suspicious))
            {
                string shader = text.fontSharedMaterial == null || text.fontSharedMaterial.shader == null
                    ? "null" : text.fontSharedMaterial.shader.name;
                string atlas = text.fontSharedMaterial == null || text.fontSharedMaterial.mainTexture == null
                    ? "null" : text.fontSharedMaterial.mainTexture.name;
                Material renderMaterial = text.materialForRendering;
                string renderShader = renderMaterial == null || renderMaterial.shader == null
                    ? "null" : renderMaterial.shader.name;
                string renderAtlas = renderMaterial == null || renderMaterial.mainTexture == null
                    ? "null" : renderMaterial.mainTexture.name;
                Debug.Log("[王国界面] TMP runtime diagnostic: reason=" + reason +
                    ", name=" + text.name + ", active=" + text.gameObject.activeInHierarchy +
                    ", enabled=" + text.enabled + ", valueLength=" + (text.text == null ? 0 : text.text.Length) +
                    ", characterCount=" + chars + ", vertexCount=" + vertices +
                    ", rect=" + text.rectTransform.rect.size + ", localScale=" + text.transform.lossyScale +
                    ", rendererAlpha=" + rendererAlpha + ", groupAlpha=" + groupAlpha +
                    ", rendererCull=" + (text.canvasRenderer != null && text.canvasRenderer.cull) +
                    ", groups=" + groups + ", font=" + (text.font == null ? "null" : text.font.name) +
                    ", material=" + (text.fontSharedMaterial == null ? "null" : text.fontSharedMaterial.name) +
                    ", shader=" + shader + ", atlas=" + atlas +
                    ", renderMaterial=" + (renderMaterial == null ? "null" : renderMaterial.name) +
                    ", renderShader=" + renderShader + ", renderAtlas=" + renderAtlas);
                logged++;
            }
        }
        Debug.Log("[王国界面] TMP runtime diagnostic summary: reason=" + reason +
            ", total=" + texts.Length + ", active=" + activeCount +
            ", nonEmpty=" + nonEmptyCount + ", characters=" + characterCount +
            ", vertices=" + vertexCount + ", alphaVisible=" + alphaVisibleCount +
            ", suspicious=" + suspiciousCount);
    }
#else
    private void DiagnoseUiTextRendering(string reason) { }
#endif

    private void SetResearchPageVisible(RectTransform page, bool visible)
    {
        if (page == null)
            return;

        // The cached research graph contains thousands of Graphics. Toggling
        // the page GameObject invokes OnDisable/OnEnable across that complete
        // hierarchy on every tab switch. Keep the hierarchy alive and gate its
        // rendering and input at the page root instead.
        if (!page.gameObject.activeSelf)
            page.gameObject.SetActive(true);
        if (researchPageVisibilityGroup == null ||
            researchPageVisibilityGroup.transform != page)
        {
            researchPageVisibilityGroup = page.GetComponent<CanvasGroup>();
            if (researchPageVisibilityGroup == null)
                researchPageVisibilityGroup = page.gameObject.AddComponent<CanvasGroup>();
            researchPageVisibilityGroup.ignoreParentGroups = false;
        }

        float targetAlpha = visible ? 1f : 0f;
        if (!Mathf.Approximately(researchPageVisibilityGroup.alpha, targetAlpha))
            researchPageVisibilityGroup.alpha = targetAlpha;
        if (researchPageVisibilityGroup.interactable != visible)
            researchPageVisibilityGroup.interactable = visible;
        if (researchPageVisibilityGroup.blocksRaycasts != visible)
            researchPageVisibilityGroup.blocksRaycasts = visible;
        if (researchGraphGesture != null && researchGraphGesture.enabled != visible)
            researchGraphGesture.enabled = visible;
    }

    private static string PageLabel(string name)
    {
        return name switch
        {
            "Overview" => "总览",
            "Resources" => "资源",
            "Buildings" => "建筑",
            "Research" => "研究",
            "Era" => "时代",
            "Workshop" => "工坊",
            "Production" => "生产树",
            "Music" => "音乐",
            "Sectors" => "星区",
            "Story" => "剧情",
            _ => name
        };
    }

    private void PopulatePage(
        string name,
        bool refreshEraRows = false,
        EraGoalEvaluation preparedEraGoal = null)
    {
        if (!pages.TryGetValue(name, out RectTransform page))
            return;
        if (name == "Production")
        {
            if (researchQueueViewport != null) researchQueueViewport.gameObject.SetActive(false);
            if (buildingControls != null) buildingControls.gameObject.SetActive(false);
            PopulateProductionPage();
            ConfigureOuterPageScroll(name, false);
            return;
        }
        if (name == "Story")
        {
            if (researchQueueViewport != null)
                researchQueueViewport.gameObject.SetActive(false);
            if (buildingControls != null)
                buildingControls.gameObject.SetActive(false);
            bool storyChanged = BuildStoryPage(page, refreshEraRows);
            Vector2 viewportSize = pageHost == null ? Vector2.zero : pageHost.rect.size;
            ConfigureOuterPageScroll(name, false, storyChanged || viewportSize != storyScrollViewportSize);
            storyScrollViewportSize = viewportSize;
            return;
        }
        if (researchQueueViewport != null)
        {
            bool shouldBeActive = name == "Research";
            if (researchQueueViewport.gameObject.activeSelf != shouldBeActive)
                researchQueueViewport.gameObject.SetActive(shouldBeActive);
        }
        if (buildingControls != null)
        {
            bool shouldBeActive = name == "Buildings";
            if (buildingControls.gameObject.activeSelf != shouldBeActive)
                buildingControls.gameObject.SetActive(shouldBeActive);
        }
        Transform old = page.Find("DataRows");
        if (old == null && name == "Overview")
        {
            page.anchorMin = new Vector2(0f, 1f);
            page.anchorMax = new Vector2(1f, 1f);
            page.pivot = new Vector2(.5f, 1f);
            page.anchoredPosition = Vector2.zero;
            LayoutRebuilder.ForceRebuildLayoutImmediate(page);
            ConfigureOuterPageScroll("Overview", false, true);
            return;
        }
        if (old == null)
        {
            Debug.LogError("[王国界面] Authored DataRows host is missing for page: " + name);
            return;
        }
        bool researchTreeWasBuilt = researchTreePageBuilt;
        bool pageWasBuilt = name == "Research"
            ? researchTreeWasBuilt
            : name == "Music"
                ? musicPageBuilt
                : AreAuthoredRowsBuilt(name);
        bool reuseResearchPage = name == "Research" && researchTreeWasBuilt && old != null;
        bool reuseAuthoredResearchPage = name == "Research" && old != null && old.Find("ResearchGraphViewport") != null;
        bool reuseAuthoredMusicPage = name == "Music" && old != null && old.Find("MusicSurface") != null;
        // DataRows is a fixed child of the authored page Prefab. The optional
        // marker component may be stale in an older serialized Prefab, so the
        // scene-owned object itself is the authoritative host check.
        bool authoredRowsHost = old != null;
        if (!reuseResearchPage && !reuseAuthoredResearchPage && !reuseAuthoredMusicPage && !authoredRowsHost)
        {
            Debug.LogError("[王国界面] Page DataRows is not an authored layout host: " + name);
            return;
        }
        bool reuseWorkshopRows = name == "Workshop" && workshopRows.Count > 0;
        if (authoredRowsHost && !reuseAuthoredResearchPage && !reuseAuthoredMusicPage &&
            !reuseWorkshopRows && !AreAuthoredRowsBuilt(name))
            ClearAuthoredRowsHost(old);
        RectTransform rows = old as RectTransform;
        if (!pageWasBuilt || refreshEraRows)
            nextRowTop = 0f;
        switch (name)
        {
            case "Resources":
                if (resourceRowsBuilt) break;
                resourceAmountLabels.Clear();
                resourceChangeLabels.Clear();
                BuildAuthoredResourceRows(rows);
                resourceRowsBuilt = true;
                break;
            case "Buildings":
                if (buildingRowsBuilt) break;
                buildingAmountLabels.Clear();
                buildingEffectLabels.Clear();
                buildingDeconstructButtons.Clear();
                buildingActionButtons.Clear();
                buildingActionButtonTexts.Clear();
                buildingActionTooltips.Clear();
                buildingActionIcons.Clear();
                buildingActionIconUpgradeModes.Clear();
                buildingActionButtonOutlines.Clear();
                buildingActionUpgradeModes.Clear();
                BuildAuthoredBuildingRows(rows);
                buildingRowsBuilt = true;
                break;
            case "Research":
                if (!researchTreePageBuilt)
                    BuildResearchTreePage(rows);
                else
                {
                    // A cached graph may have changed while hidden, but an
                    // unchanged tab switch must not dirty every node Graphic.
                    // Reuse the normal signatures so only changed state is
                    // applied before the page becomes interactive.
                    researchDynamicUiDirty = true;
                    researchDynamicSignatureRefreshTimer = 0.5f;
                    RefreshResearchDynamicUI();
                }
                break;
            case "Era":
                if (eraRowsBuilt && !refreshEraRows) break;
                BuildEraPage(rows, preparedEraGoal);
                eraRowsBuilt = true;
                break;
            case "Workshop":
                if (workshopRowsBuilt) break;
                buildingActionButtonTexts.Clear();
                buildingActionTooltips.Clear();
                buildingActionIcons.Clear();
                buildingActionIconUpgradeModes.Clear();
                buildingActionButtonOutlines.Clear();
                BuildAuthoredWorkshopRows(rows);
                workshopRowsBuilt = true;
                break;
            case "Music":
                BuildMusicPage(rows);
                break;
            case "Sectors":
                if (sectorRowsBuilt) break;
                BuildSectorRows(rows);
                sectorRowsBuilt = true;
                break;
            default:
                break;
        }
        if (!pageWasBuilt || refreshEraRows)
        {
            float rowsHeight = Mathf.Max(86f, nextRowTop);
            if (name == "Research")
            {
                // DataRows is the research ScrollRect viewport, not the graph's
                // content. Keep it inside the visible page area so the inner
                // ScrollRect owns the complete vertical drag range.
                if (!researchTreeWasBuilt)
                    Canvas.ForceUpdateCanvases();
                float visibleHeight = pageHost == null ? 720f : pageHost.rect.height;
                rowsHeight = Mathf.Max(240f, visibleHeight);
            }
            else if (name == "Music")
            {
                // Music controls stay in the page viewport; the track list has
                // its own ScrollRect below them so TIME / SEEK remains visible.
                if (!pageWasBuilt || pageHost == null ||
                    pageHost.rect.width <= 1f || pageHost.rect.height <= 1f)
                    Canvas.ForceUpdateCanvases();
                float visibleHeight = pageHost == null ? 720f : pageHost.rect.height;
                rowsHeight = Mathf.Max(240f, visibleHeight);
            }

            rows.anchorMin = new Vector2(0, 1);
            rows.anchorMax = new Vector2(1, 1);
            rows.pivot = new Vector2(.5f, 1);
            rows.anchoredPosition = Vector2.zero;
            rows.sizeDelta = new Vector2(0, rowsHeight);
            page.sizeDelta = new Vector2(0, rowsHeight);
        }
        // Every normal page switch must bind the shared ScrollRect to the
        // page that was just populated. Checking the previous content here
        // leaves Resources/Buildings/Era visually active while the ScrollRect
        // still drags the hidden page from the preceding tab.
        if (pageScroll != null)
            ConfigureOuterPageScroll(name, false, true);
        if (name == "Research" && researchGraphGesture != null)
        {
            if (!researchTreeWasBuilt)
                Canvas.ForceUpdateCanvases();
            researchGraphGesture.RefreshLayoutBounds(!researchTreeWasBuilt);
        }
    }

    private static void ClearAuthoredRowsHost(Transform host)
    {
        for (int i = host.childCount - 1; i >= 0; i--)
            Destroy(host.GetChild(i).gameObject);
    }

    private bool AreAuthoredRowsBuilt(string name)
    {
        return name switch
        {
            "Resources" => resourceRowsBuilt,
            "Buildings" => buildingRowsBuilt,
            "Era" => eraRowsBuilt,
            "Workshop" => workshopRowsBuilt,
            "Sectors" => sectorRowsBuilt,
            _ => false
        };
    }

}

public enum DevelopmentGuidanceStatus
{
    Progressing,
    WaitingResources,
    Stabilize,
    Available,
    Workshop,
    Complete
}

public sealed class DevelopmentGuidanceSnapshot
{
    public DevelopmentGuidanceStatus Status { get; internal set; }
    public string Title { get; internal set; } = string.Empty;
    public string Body { get; internal set; } = string.Empty;
    public IReadOnlyList<string> Blockers { get; internal set; } = Array.Empty<string>();
}

public static class DevelopmentGuidance
{
    public static string BuildSituationReport(GameState state, IEnumerable<ResourceState> resources)
    {
        if (state == null) return string.Empty;
        var body = new System.Text.StringBuilder("<b>王国运行简报</b>\n");
        ExpantaNum foodNet = state.FoodNetRate;
        if (foodNet < ExpantaNum.Zero)
            body.Append("粮食正在净消耗；按当前收支，余粮约可维持 ")
                .Append((state.FoodAmount / -foodNet / 60).ToGameString()).Append(" 分钟。优先补足食物来源。");
        else if (foodNet > ExpantaNum.Zero)
            body.Append("粮食净余 ").Append(foodNet.ToGameString()).Append("/秒；新增人口和产业会增加消耗。");
        else
            body.Append("粮食收支持平；扩张人口前先增加食物产出。");
        if (state.PowerConsumptionRate > ExpantaNum.Zero && state.PowerSatisfaction < ExpantaNum.One)
            body.Append("\n电力供给满足 ").Append((state.PowerSatisfaction * 100).ToGameString())
                .Append("%；补充发电，恢复用电产业效率。");
        if (state.LogisticsConsumptionRate > ExpantaNum.Zero && state.LogisticsSatisfaction < ExpantaNum.One)
            body.Append("\n物流供给满足 ").Append((state.LogisticsSatisfaction * 100).ToGameString())
                .Append("%；补充运输，恢复依赖物流的产业效率。");

        ExpantaNum productionReward = state.HappinessRewardMultiplier;
        var declining = new List<ResourceState>();
        if (resources != null)
            foreach (ResourceState resource in resources)
                if (resource.ConsumptionRate > resource.ProductionRate * productionReward)
                    declining.Add(resource);
        declining.Sort((a, b) =>
        {
            int time = (a.Amount / (a.ConsumptionRate - a.ProductionRate * productionReward))
                .CompareTo(b.Amount / (b.ConsumptionRate - b.ProductionRate * productionReward));
            return time != 0 ? time : string.CompareOrdinal(a.Definition.Id, b.Definition.Id);
        });
        if (declining.Count > 0)
        {
            body.Append("\n\n<b>优先检查的生产链</b>（按余量支撑时间排序）");
            for (int i = 0; i < declining.Count && i < 3; i++)
            {
                ResourceState resource = declining[i];
                ExpantaNum loss = resource.ConsumptionRate - resource.ProductionRate * productionReward;
                body.Append("\n").Append(resource.Definition.Label).Append("：净耗 ")
                    .Append(loss.ToGameString()).Append("/秒，余量约 ")
                    .Append((resource.Amount / loss / 60).ToGameString()).Append(" 分钟。");
            }
            body.Append("\n以上按当前速率估算；查看资源详情中的来源与持续投入。");
        }
        WorldEventState events = state.Events;
        if (events.Sequence > 0)
        {
            body.Append("\n\n<b>聚落事件</b>\n");
            if (events.ActiveKind == WorldEventKind.Fire)
                body.Append("失火：人口损失 ").Append(events.PopulationLost)
                    .Append("；建筑资源与粮食产出降低20%。重建还需约 ")
                    .Append(Math.Ceiling(events.RemainingSeconds)).Append(" 秒。");
            else if (events.ActiveKind == WorldEventKind.ProductiveDay)
                body.Append("生产顺利：建筑资源与粮食产出提高15%，还将持续约 ")
                    .Append(Math.Ceiling(events.RemainingSeconds)).Append(" 秒。");
            else if (events.LastKind == WorldEventKind.Aurora)
                body.Append("极光夜：鼠群在夜空下驻足。仅为见闻，没有数值影响。");
            else
                body.Append(events.PopulationLost > 0
                    ? "火灾重建已结束，建筑产出倍率恢复；损失的人口需要重新增长。"
                    : "上次事件已经结束，建筑资源与粮食产出倍率已恢复。");
        }
        return body.ToString();
    }

    public static DevelopmentGuidanceSnapshot Build(
        GameManager gameManager,
        ResearchManager researchManager,
        ResourceManager resourceManager,
        BuildingManager buildingManager,
        WorkshopManager workshopManager)
    {
        GameState gameState = gameManager == null ? null : gameManager.State;
        DevelopmentGuidanceSnapshot snapshot = new DevelopmentGuidanceSnapshot
        {
            Status = DevelopmentGuidanceStatus.Complete,
            Title = "当前时代已稳定",
            Body = "继续扩张生产链，或查看研究树寻找下一阶段目标。"
        };

        ResearchState active = researchManager?.ActiveResearch;
        if (active == null && researchManager != null && researchManager.ResearchQueue.Count > 0 &&
            researchManager.ResearchQueue[0].Status == ResearchStatus.WaitingResources)
            active = researchManager.ResearchQueue[0];
        if (active != null)
        {
            ResearchDef research = active.Definition;
            snapshot.Title = research.Label;
            if (active.Status == ResearchStatus.WaitingResources)
            {
                snapshot.Status = DevelopmentGuidanceStatus.WaitingResources;
                snapshot.Body = "研究等待资源支付，先补齐以下资源。";
                snapshot.Blockers = FindResearchResourceBlockers(active, resourceManager);
            }
            else
            {
                snapshot.Status = DevelopmentGuidanceStatus.Progressing;
                snapshot.Body = "研究正在推进，保持研究力即可；完成后查看它解锁的建筑、生产链或时代条件。";
                snapshot.Blockers = new[]
                {
                    "进度 " + Math.Min(99.9d, active.ProgressRatio.ToDouble() * 100d).ToString("0.0") + "%"
                };
            }
            return snapshot;
        }

        if (gameState != null)
        {
            if (gameState.HappinessMultiplier < ExpantaNum.One)
                return BuildStabilitySnapshot("幸福度不足", "先提高食物净产出，避免人口与生产效率继续下降。", gameState.FoodNetRate);
            if (gameState.PowerSatisfaction < ExpantaNum.One)
                return BuildStabilitySnapshot("电力供应不足", "先补充电力生产，避免工业建筑效率下降。", gameState.PowerProductionRate - gameState.PowerConsumptionRate);
            if (gameState.LogisticsSatisfaction < ExpantaNum.One)
                return BuildStabilitySnapshot("物流能力不足", "先补充物流生产，避免后续产业链被运输能力卡住。", gameState.LogisticsProductionRate - gameState.LogisticsConsumptionRate);
        }

        if (researchManager != null)
        {
            IReadOnlyList<ResearchDef> definitions = DataBase<ResearchDef>.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                ResearchDef research = definitions[i];
                if (research == null)
                    continue;
                // The UI can be built during the same frame that the
                // ResearchManager singleton is still creating its states.
                // Do not turn that normal initialization window into a fatal
                // KeyNotFoundException; GetState remains strict for callers
                // that require an initialized state.
                if (!researchManager.States.TryGetValue(research, out ResearchState state))
                    continue;
                if (state.Status != ResearchStatus.Available || !researchManager.CanAccessResearch(research))
                    continue;
                snapshot.Status = DevelopmentGuidanceStatus.Available;
                snapshot.Title = "开始研究：" + research.Label;
                snapshot.Body = string.IsNullOrWhiteSpace(research.Description)
                    ? "前置研究已完成；查看材料需求，将它加入研究队列。"
                    : research.Description;
                snapshot.Blockers = new[] { "研究力 " + researchManager.EffectiveResearchPower.ToGameString() + "/s" };
                return snapshot;
            }
        }

        if (buildingManager != null)
        {
            IReadOnlyList<BuildingDef> definitions = DataBase<BuildingDef>.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                BuildingDef building = definitions[i];
                if (building != null && !(building is SectorBuildingDef) &&
                    buildingManager.GetBuildFailure(building, ExpantaNum.One) == BuildFailure.None)
                {
                    snapshot.Status = DevelopmentGuidanceStatus.Available;
                    snapshot.Title = "建设：" + building.Label;
                    snapshot.Body = TutorialManager.DescribeBuildingRole(building);
                    snapshot.Blockers = Array.Empty<string>();
                    return snapshot;
                }
            }
        }

        if (workshopManager != null && workshopManager.IsSystemUnlocked)
        {
            IReadOnlyList<WorkshopUpgradeDef> definitions = DataBase<WorkshopUpgradeDef>.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                WorkshopUpgradeDef definition = definitions[i];
                if (definition == null ||
                    (gameState != null && definition.TechLevel > gameState.TechLevel) ||
                    workshopManager.IsPurchased(definition) || !workshopManager.ArePrerequisitesMet(definition))
                    continue;
                snapshot.Status = DevelopmentGuidanceStatus.Workshop;
        snapshot.Title = "工坊：" + definition.Label;
                snapshot.Body = definition.Description;
                snapshot.Blockers = Array.Empty<string>();
                return snapshot;
            }
        }

        return snapshot;
    }

    private static DevelopmentGuidanceSnapshot BuildStabilitySnapshot(
        string title,
        string body,
        ExpantaNum netRate)
    {
        return new DevelopmentGuidanceSnapshot
        {
            Status = DevelopmentGuidanceStatus.Stabilize,
            Title = title,
            Body = body,
            Blockers = new[] { "净变化 " + (netRate >= ExpantaNum.Zero ? "+" : "") + netRate.ToGameString() + "/s" }
        };
    }

    private static IReadOnlyList<string> FindResearchResourceBlockers(
        ResearchState state,
        ResourceManager resourceManager)
    {
        List<string> blockers = new List<string>();
        if (state == null || resourceManager == null || state.Definition.ResourceRequirements == null)
            return blockers;
        for (int i = 0; i < state.Definition.ResourceRequirements.Count; i++)
        {
            Pair<ResourceDef, ExpantaNum> requirement = state.Definition.ResourceRequirements[i];
            if (requirement.First == null)
                continue;
            ExpantaNum remaining = ExpantaNum.Max(
                ExpantaNum.Zero,
                requirement.Second - state.GetPaidResourceCost(requirement.First) - resourceManager.GetAmount(requirement.First));
            if (remaining > ExpantaNum.Zero)
                blockers.Add(requirement.First.Label + " 缺少 " + remaining.ToGameString());
        }
        return blockers;
    }
}
