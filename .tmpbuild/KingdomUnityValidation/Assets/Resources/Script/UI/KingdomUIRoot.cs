using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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

    private static readonly Color Background = new(0.035f, 0.055f, 0.065f, 1f);
    private static readonly Color Panel = new(0.10f, 0.13f, 0.14f, 1f);
    private static readonly Color PanelRaised = new(0.16f, 0.19f, 0.19f, 1f);
    private static readonly Color Copper = new(0.76f, 0.50f, 0.25f, 1f);
    private static readonly Color TextPrimary = new(0.92f, 0.89f, 0.80f, 1f);
    private static readonly Color TextSecondary = new(0.63f, 0.69f, 0.67f, 1f);
    private static readonly Color Positive = new(0.37f, 0.72f, 0.58f, 1f);
    private static readonly Color Error = new(0.78f, 0.31f, 0.28f, 1f);
    private const float TopBar = 132f;
    private const float Footer = 92f;
    private const float Nav = 270f;
    private const float Detail = 640f;

    private RectTransform safeArea;
    private RectTransform leftNavigation;
    private RectTransform pageHost;
    private ScrollRect pageScroll;
    private TMP_Text pageTitle;
    private TMP_Text buildingPageTitle;
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
    private Button detailPaymentButton;
    private RectTransform tooltipPanel;
    private TMP_Text tooltipText;
    private string detailActionLabel;
    private TMP_Text topKingdomTitle;
    private TMP_Text topStatus;
    private RectTransform buildingQuantityControls;
    private bool detailBuildingUpgrade;
    private bool detailIsBuilding;
    private Building selectedBuilding;
    private Resource selectedResource;
    private static TMP_FontAsset sharedFontAsset;
    private float liveRefreshTimer;
    private bool topStatusDataErrorLogged;
    private bool runtimeGeometryLogged;
    private bool runtimeGeometryDiagnosticLogged;
    private bool detailGeometryLogged;
    private Vector2 lastDetailViewportSize;
    private Vector2 lastDetailContentSize;
    private BuildingQuantityMode buildingQuantityMode = BuildingQuantityMode.One;
    private ExpantaNum customBuildingQuantity = ExpantaNum.One;
    private TMP_InputField customQuantityInput;
    private static Font sharedSourceFont;
    private readonly Dictionary<BuildingQuantityMode, Button> buildingQuantityButtons = new();
    private readonly Dictionary<string, RectTransform> pages = new();
    private readonly Dictionary<Building, TMP_Text> buildingAmountLabels = new();
    private readonly Dictionary<Building, Image> buildingDeconstructSurfaces = new();
    private readonly Dictionary<Building, Button> buildingDeconstructButtons = new();
    private readonly Dictionary<Building, Button> buildingActionButtons = new();
    private readonly Dictionary<Building, bool> buildingActionUpgradeModes = new();
    private readonly Dictionary<Resource, TMP_Text> resourceAmountLabels = new();
    private readonly Dictionary<Resource, TMP_Text> resourceChangeLabels = new();
    private RectTransform researchGraphViewport;
    private RectTransform researchGraphContent;
    private readonly Dictionary<Research, Button> researchTreeNodes = new();
    private bool researchTreePageBuilt;
    private Coroutine researchTreeWarmupCoroutine;
    private string populatedPage;
    private float nextRowTop;
    private TMP_Text musicCurrentLabel;
    private TMP_Text musicTimeLabel;
    private Slider musicProgressSlider;
    private UnityEngine.Events.UnityAction<float> musicProgressSeekHandler;
    private Slider musicVolumeSlider;
    private Slider musicGapSlider;
    private TMP_Text musicVolumeValueLabel;
    private TMP_Text musicGapValueLabel;
    private RectTransform musicTrackList;
    private bool musicProgressDragging;
    private bool musicPageBuilt;
    private TMP_Text developmentGuidanceText;
    private DevelopmentGuidanceSnapshot developmentGuidanceSnapshot;
    private bool developmentGuidanceErrorLogged;
    private bool developmentGuidanceRuntimeGeometryLogged;

    private void Awake()
    {
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
        ResolveFont();
        PreloadResearchTreeAssets();

        HideLegacyChildren();
        Debug.Log("[KingdomUI] Legacy UI isolated: SafeAreaRoot is the only runtime UI surface");
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
        Debug.Log($"[KingdomUI] Canvas configured: mode={scaler.uiScaleMode}, reference={scaler.referenceResolution}, match={scaler.matchWidthOrHeight}");
    }

    private void ResolveFont()
    {
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
        Debug.Log($"[KingdomUI] Font resolved: source={(source == null ? "null" : source.name)}, shared={(sharedFontAsset == null ? "null" : sharedFontAsset.name)}, default={(TMP_Settings.defaultFontAsset == null ? "null" : TMP_Settings.defaultFontAsset.name)}");
    }

    private void Start() => Invoke(nameof(RebuildCurrentPage), 0.5f);

    private void LateUpdate()
    {
        // CanvasScaler and the SafeAreaFitter can complete their first layout
        // pass after Awake. Re-measure until the runtime hierarchy has a real
        // rectangle; this also repairs old scene instances without rebuilding
        // any page content or resetting a user's scroll position.
        if (!runtimeGeometryLogged)
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
        if (!string.IsNullOrEmpty(populatedPage))
        {
            if (populatedPage == "Research" && researchTreePageBuilt)
            {
                RefreshResearchTreeVisuals();
                return;
            }
            PopulatePage(populatedPage);
        }
    }

    private void HideLegacyChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name != "SafeAreaRoot")
                child.gameObject.SetActive(false);
        }
    }

    private void BuildVisibleShell()
    {
        bool authoredShell = TryBindAuthoredShell();
        if (!authoredShell)
        {
            Debug.LogError("[KingdomUI] Authored scene shell is incomplete. Open Tools/Kingdom/UI/Generate Authored Scene Shell; fixed UI will not be generated at runtime.");
            return;
        }
        if (safeArea.GetComponent<SafeAreaFitter>() == null)
            safeArea.gameObject.AddComponent<SafeAreaFitter>();
        Debug.Log("[KingdomUI] Authored scene shell bound; Detail UI v2 rebuilt and legacy detail UI discarded.");
        EnsureRuntimeCanvasGeometry();
        Canvas.ForceUpdateCanvases();
        Debug.Log($"[KingdomUI] Panel alignment: navigation={GetRectSize(leftNavigation)}, detail={GetRectSize(detailPanel)}, bottomDelta={GetRectBottom(detailPanel) - GetRectBottom(leftNavigation):0.00}, topDelta={GetRectTop(detailPanel) - GetRectTop(leftNavigation):0.00}");
        SetPage("Overview");
        researchTreeWarmupCoroutine = StartCoroutine(WarmResearchTreePage());
    }

    private IEnumerator WarmResearchTreePage()
    {
        // Let the initial overview render first. Build the research page while
        // it is hidden so activating the tab only reuses existing UI objects.
        yield return null;
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
        researchTreeWarmupCoroutine = null;
        Debug.Log("[KingdomUI] Research page warmed and cached before first tab activation");
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
            return;

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

        // Scene/P prefab overrides previously changed these anchors at edit
        // time. Reassert the contract at runtime so both side panels always
        // occupy the same vertical interval below the top bar.
        if (leftNavigation != null)
        {
            leftNavigation.anchorMin = new Vector2(0f, 0f);
            leftNavigation.anchorMax = new Vector2(0f, 1f);
            leftNavigation.offsetMin = Vector2.zero;
            leftNavigation.offsetMax = new Vector2(Nav, -TopBar);
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
            Debug.Log($"[KingdomUI] Runtime geometry pending: root={root.rect.size}, safeArea={(safeArea == null ? Vector2.zero : safeArea.rect.size)}, screen={Screen.width}x{Screen.height}, rootScale={root.localScale}");
        }
        if (!runtimeGeometryLogged && safeArea != null &&
            safeArea.rect.width > 0f && safeArea.rect.height > 0f &&
            leftNavigation != null && detailPanel != null &&
            leftNavigation.rect.height > 0f && detailPanel.rect.height > 0f)
        {
            runtimeGeometryLogged = true;
            Debug.Log($"[KingdomUI] Runtime geometry valid: screen={Screen.width}x{Screen.height}, safeArea={safeArea.rect.size}, navigation={leftNavigation.rect.size}, detail={detailPanel.rect.size}, bottomDelta={GetRectBottom(detailPanel) - GetRectBottom(leftNavigation):0.00}, topDelta={GetRectTop(detailPanel) - GetRectTop(leftNavigation):0.00}");
        }
    }

    // Legacy runtime shell builder retained as historical reference only.
    // The authored prefab is now the sole source of fixed layout.

    private void SetPage(string name)
    {
        if (name == "Research")
            Debug.Log("[KingdomUI] SetPage Research");
        foreach (KeyValuePair<string, RectTransform> pair in pages)
            pair.Value.gameObject.SetActive(pair.Key == name);
        if (pageTitle != null)
        {
            pageTitle.text = PageLabel(name);
            pageTitle.fontSize = 36;
            pageTitle.enabled = true;
            pageTitle.gameObject.SetActive(true);
            RectTransform titleRect = pageTitle.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.offsetMin = new Vector2(34f, -88f);
            titleRect.offsetMax = new Vector2(230f, -28f);
            pageTitle.alignment = TextAlignmentOptions.MidlineLeft;
            pageTitle.transform.SetAsLastSibling();
        }
        if (pageScroll != null)
        {
            pageScroll.content = pages[name];
            pageScroll.verticalNormalizedPosition = 1f;
        }
        populatedPage = name;
        // The page slot is laid out by the parent Canvas.  Force that pass
        // before creating the research viewport so its ScrollRect measures
        // the real visible area instead of the zero/negative Awake-time rect.
        Canvas.ForceUpdateCanvases();
        if (name == "Buildings")
            BuildBuildingQuantityControls(pageHost.parent);
        PopulatePage(name);
        if (name == "Buildings" && buildingPageTitle != null)
        {
            buildingPageTitle.text = "建筑";
            buildingPageTitle.enabled = true;
            buildingPageTitle.gameObject.SetActive(true);
            buildingPageTitle.text = "\u5efa\u7b51";
            buildingPageTitle.transform.SetParent(pageHost.parent, false);
            buildingPageTitle.transform.SetAsLastSibling();
        }
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
            "Music" => "音乐",
            "Sectors" => "星区",
            _ => name
        };
    }

    private void PopulatePage(string name)
    {
        if (!pages.TryGetValue(name, out RectTransform page))
            return;
        if (buildingQuantityControls != null)
            buildingQuantityControls.gameObject.SetActive(name == "Buildings");
        if (buildingPageTitle != null)
            buildingPageTitle.gameObject.SetActive(name == "Buildings");
        if (pageTitle != null)
            pageTitle.gameObject.SetActive(name != "Buildings");
        Transform old = page.Find("DataRows");
        if (old == null)
        {
            Debug.LogError("[KingdomUI] Authored DataRows host is missing for page: " + name);
            return;
        }
        bool reuseResearchPage = name == "Research" && researchTreePageBuilt && old != null;
        bool reuseAuthoredResearchPage = name == "Research" && old != null && old.Find("ResearchGraphViewport") != null;
        bool reuseAuthoredMusicPage = name == "Music" && old != null && old.Find("MusicSurface") != null;
        // DataRows is a fixed child of the authored page Prefab. The optional
        // marker component may be stale in an older serialized Prefab, so the
        // scene-owned object itself is the authoritative host check.
        bool authoredRowsHost = old != null;
        if (!reuseResearchPage && !reuseAuthoredResearchPage && !reuseAuthoredMusicPage && !authoredRowsHost)
        {
            Debug.LogError("[KingdomUI] Page DataRows is not an authored layout host: " + name);
            return;
        }
        if (authoredRowsHost && !reuseAuthoredResearchPage && !reuseAuthoredMusicPage)
            ClearAuthoredRowsHost(old);
        RectTransform rows = old as RectTransform;
        nextRowTop = 0f;
        switch (name)
        {
            case "Resources":
                resourceAmountLabels.Clear();
                resourceChangeLabels.Clear();
                AddResourceRows(rows);
                break;
            case "Buildings":
                buildingAmountLabels.Clear();
                buildingDeconstructSurfaces.Clear();
                buildingDeconstructButtons.Clear();
                buildingActionButtons.Clear();
                buildingActionUpgradeModes.Clear();
                AddBuildingRows(rows);
                break;
            case "Research":
                if (!researchTreePageBuilt)
                    BuildResearchTreePage(rows);
                else
                    RefreshResearchTreeVisuals();
                break;
            case "Workshop":
                AddWorkshopRows(rows);
                break;
            case "Music":
                BuildMusicPage(rows);
                break;
            default:
                break;
        }
        float rowsHeight = Mathf.Max(86f, nextRowTop);
        if (name == "Research")
        {
            // DataRows is the research ScrollRect viewport, not the graph's
            // content. Keep it inside the visible page area so the inner
            // ScrollRect owns the complete vertical drag range.
            Canvas.ForceUpdateCanvases();
            float visibleHeight = pageHost == null ? 720f : pageHost.rect.height;
            rowsHeight = Mathf.Max(240f, visibleHeight);
        }
        else if (name == "Music")
        {
            // Music controls stay in the page viewport; the track list has
            // its own ScrollRect below them so TIME / SEEK remains visible.
            Canvas.ForceUpdateCanvases();
            float visibleHeight = pageHost == null ? 720f : pageHost.rect.height;
            rowsHeight = Mathf.Max(240f, visibleHeight);
        }
        rows.anchorMin = new Vector2(0, 1);
        rows.anchorMax = new Vector2(1, 1);
        rows.pivot = new Vector2(.5f, 1);
        rows.anchoredPosition = new Vector2(0, name == "Research" || name == "Music" ? 0f : -82f);
        rows.sizeDelta = new Vector2(0, rowsHeight);
        // The research graph owns the pan/zoom. Its viewport must be the
        // visible page window, not the old 1400px page canvas; otherwise the
        // inner ScrollRect measures a rectangle larger than the screen and
        // loses the expected vertical drag range.
        page.sizeDelta = new Vector2(0, name == "Research" || name == "Music"
            ? rowsHeight
            : Mathf.Max(1400f, rowsHeight + 180f));
        if (pageScroll != null && pageScroll.content == page)
        {
            pageScroll.StopMovement();
            // Research owns its own two-axis graph ScrollRect. Disable the
            // outer ScrollRect completely so it cannot consume the same touch
            // drag before the graph receives it.
            pageScroll.enabled = name != "Research" && name != "Music";
            pageScroll.vertical = name != "Research" && name != "Music";
            Canvas.ForceUpdateCanvases();
            if (pageScroll.enabled)
                pageScroll.verticalNormalizedPosition = 1f;
        }
        if (name == "Research" && researchGraphGesture != null)
        {
            Canvas.ForceUpdateCanvases();
            researchGraphGesture.RefreshLayoutBounds(true);
        }
    }

    private static void ClearAuthoredRowsHost(Transform host)
    {
        for (int i = host.childCount - 1; i >= 0; i--)
            Destroy(host.GetChild(i).gameObject);
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
    public string EraText { get; internal set; } = string.Empty;
    public IReadOnlyList<string> Blockers { get; internal set; } = Array.Empty<string>();
}

public static class DevelopmentGuidance
{
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
            EraText = gameState == null ? "时代未知" : gameState.TechLevel.GetDescription(),
            Title = "当前时代已稳定",
            Body = "继续扩张生产链，或查看研究树寻找下一阶段目标。"
        };

        if (researchManager != null && researchManager.ActiveResearch != null)
        {
            ResearchState active = researchManager.ActiveResearch;
            Research research = active.Definition;
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
                snapshot.Body = "研究正在推进，保持研究力与资源供应即可。";
                snapshot.Blockers = new[]
                {
                    "进度 " + (active.ProgressRatio * 100).ToGameString() + "%"
                };
            }
            return snapshot;
        }

        if (gameState != null)
        {
            if (gameState.HappinessMultiplier < ExpantaNum.One)
                return BuildStabilitySnapshot(gameState, "幸福度不足", "先提高食物净产出，避免人口与生产效率继续下降。", gameState.FoodNetRate);
            if (gameState.PowerSatisfaction < ExpantaNum.One)
                return BuildStabilitySnapshot(gameState, "电力供应不足", "先补充电力生产，避免工业建筑效率下降。", gameState.PowerProductionRate - gameState.PowerConsumptionRate);
            if (gameState.LogisticsSatisfaction < ExpantaNum.One)
                return BuildStabilitySnapshot(gameState, "物流能力不足", "先补充物流生产，避免后续产业链被运输能力卡住。", gameState.LogisticsProductionRate - gameState.LogisticsConsumptionRate);
        }

        if (researchManager != null)
        {
            IReadOnlyList<Research> definitions = DataBase<Research>.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                Research research = definitions[i];
                if (research == null)
                    continue;
                // The UI can be built during the same frame that the
                // ResearchManager singleton is still creating its states.
                // Do not turn that normal initialization window into a fatal
                // KeyNotFoundException; GetState remains strict for callers
                // that require an initialized state.
                if (!researchManager.States.TryGetValue(research, out ResearchState state))
                    continue;
                if (state.Status != ResearchStatus.Available)
                    continue;
                snapshot.Status = DevelopmentGuidanceStatus.Available;
                snapshot.Title = "开始研究：" + research.Label;
                snapshot.Body = string.IsNullOrWhiteSpace(research.Description)
                    ? "这是当前研究树中最靠前的可用节点。"
                    : research.Description;
                snapshot.Blockers = new[] { "研究力 " + researchManager.ResearchPower.ToGameString() + "/s" };
                return snapshot;
            }
        }

        if (buildingManager != null)
        {
            IReadOnlyList<Building> definitions = DataBase<Building>.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                Building building = definitions[i];
                if (building != null && buildingManager.CanConstructNew(building))
                {
                    snapshot.Status = DevelopmentGuidanceStatus.Available;
                    snapshot.Title = "建设：" + building.Label;
                    snapshot.Body = "当前有可建设的建筑，扩展生产链或研究能力。";
                    snapshot.Blockers = Array.Empty<string>();
                    return snapshot;
                }
            }
        }

        if (workshopManager != null && workshopManager.IsSystemUnlocked)
        {
            IReadOnlyList<WorkshopUpgradeDefinition> definitions = DataBase<WorkshopUpgradeDefinition>.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                WorkshopUpgradeDefinition definition = definitions[i];
                if (definition == null || workshopManager.IsPurchased(definition) || !workshopManager.ArePrerequisitesMet(definition))
                    continue;
                snapshot.Status = DevelopmentGuidanceStatus.Workshop;
                snapshot.Title = "Workshop：" + definition.Label;
                snapshot.Body = "已有可用的 Workshop 升级，查看其资源需求与效果。";
                snapshot.Blockers = Array.Empty<string>();
                return snapshot;
            }
        }

        return snapshot;
    }

    private static DevelopmentGuidanceSnapshot BuildStabilitySnapshot(
        GameState gameState,
        string title,
        string body,
        ExpantaNum netRate)
    {
        return new DevelopmentGuidanceSnapshot
        {
            Status = DevelopmentGuidanceStatus.Stabilize,
            Title = title,
            Body = body,
            Blockers = new[] { "净变化 " + (netRate >= ExpantaNum.Zero ? "+" : "") + netRate.ToGameString() + "/s" },
            EraText = gameState == null ? string.Empty : gameState.TechLevel.GetDescription()
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
            Pair<Resource, ExpantaNum> requirement = state.Definition.ResourceRequirements[i];
            if (requirement == null || requirement.First == null)
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
